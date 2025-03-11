using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Collections;

namespace Oxide.Plugins
{
    [Info("Boloto Wipe Block", "https://discord.gg/TrJ7jnS233", "1.0.4")]
    public class BWipeBlock : RustPlugin
    {
        #region [Vars]
        [PluginReference] private Plugin ImageLibrary = null;

        private const String Layer = "WipeBlock.Layer";
        private const String NLayer = "WipeBlock.Notify";
        private const String IgnorePermission = "WipeBlock.ignore";
        private const Boolean LanguageEn = false;

        static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static Double CurrentTime() { return DateTime.UtcNow.Subtract(epoch).TotalSeconds; }
 
        private Double IsBlocked(ItemDefinition itemDefinition) => IsBlocked(itemDefinition.shortname);
        private Double UnBlockTime(Int32 amount) => SaveRestore.SaveCreatedTime.ToUniversalTime().Subtract(epoch).TotalSeconds + amount;

        private Dictionary<String, Int32> _itemIds = new Dictionary<String, Int32>();
        private List<BasePlayer> openUI = new List<BasePlayer>();
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

        private class PluginConfig
        {
            [JsonProperty(LanguageEn ? "Blocking items" : "Блокировка предметов")]
            public Dictionary<String, Int32> blockItems;

            [JsonProperty(LanguageEn ? "Items that can't be thrown" : "Предметы которые нельзя кидать")]
            public List<String> blockItemsThrown;

            [JsonProperty("Config version")]
            public VersionNumber PluginVersion = new VersionNumber();

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    blockItems = new Dictionary<String, Int32>()
                    {
                        ["pistol.revolver"] = 1800,
                        ["shotgun.Double"] = 1800,
                        ["pistol.semiauto"] = 3600,
                        ["pistol.python"] = 3600,
                        ["pistol.m92"] = 3600,
                        ["pistol.prototype17"] = 3600,
                        ["shotgun.pump"] = 3600,
                        ["coffeecan.helmet"] = 3600,
                        ["roadsign.jacket"] = 3600,
                        ["roadsign.kilt"] = 3600,
                        ["smg.2"] = 4200,
                        ["smg.thompson"] = 4200,
                        ["shotgun.spas12"] = 4200,
                        ["rifle.semiauto"] = 4200,
                        ["smg.mp5"] = 5600,
                        ["rifle.m39"] = 5600,
                        ["metal.facemask"] = 5600,
                        ["metal.facemask.icemask"] = 5600,
                        ["metal.plate.torso"] = 5600,
                        ["metal.plate.torso.icevest"] = 5600,
                        ["metal.facemask.hockey"] = 5600,
                        ["rifle.ak"] = 7200,
                        ["rifle.ak.ice"] = 7200,
                        ["rifle.bolt"] = 7200,
                        ["rifle.l96"] = 7200,
                        ["rifle.lr300"] = 7200,
                        ["hmlmg"] = 75600,
                        ["lmg.m249"] = 75600,
                        ["heavy.plate.helmet"] = 75600,
                        ["heavy.plate.jacket"] = 75600,
                        ["heavy.plate.pants"] = 75600,
                        ["grenade.f1"] = 75600,
                        ["grenade.beancan"] = 75600,
                        ["explosive.satchel"] = 84400,
                        ["submarine.torpedo.straight"] = 84400,
                        ["ammo.rocket.mlrs"] = 84400,
                        ["multiplegrenadelauncher"] = 84400,
                        ["explosive.timed"] = 84400,
                        ["rocket.launcher"] = 84400,
                        ["ammo.rifle.explosive"] = 84400,
                        ["ammo.rocket.basic"] = 84400,
                        ["ammo.rocket.fire"] = 84400,
                        ["ammo.rocket.hv"] = 84400,
                    },
                    blockItemsThrown = new List<String>()
                    {
                        "grenade.flashbang.deployed",
                        "grenade.molotov.deployed",
                    },
                    PluginVersion = new VersionNumber()
                };
            }
        }
        #endregion

        #region [ImageLibrary]
        private Boolean HasImage(String imageName, UInt64 imageId = 0) => (Boolean)ImageLibrary.Call("HasImage", imageName, imageId);
        private Boolean AddImage(String url, String shortname, UInt64 skin = 0) => (Boolean)ImageLibrary?.Call("AddImage", url, shortname, skin);
        private String GetImage(String shortname, UInt64 skin = 0) => (String)ImageLibrary?.Call("GetImage", shortname, skin);
        #endregion

        #region [Oxide-Api]
        private void OnServerInitialized()
        {
            if (!permission.PermissionExists(IgnorePermission))
                permission.RegisterPermission(IgnorePermission, this);

            AddImage("https://i.postimg.cc/5t74ZzBr/C2g6QoA.png", $"{Name}.Background");
            AddImage("https://i.postimg.cc/V6rG9J5S/Group-11-1-1.png", $"ItemFon");
            AddImage("https://i.postimg.cc/Gt5jZ44x/uHTdwjY.png", $"{Name}.BlockFon");

            cmd.AddChatCommand("block", this, "MainUi");

            CheckPlayers();
        }

        private void Unload()
        {
            foreach (var player in openUI)
            {
                if (!player.IsConnected) continue;

                CuiHelper.DestroyUi(player, Layer);
                CuiHelper.DestroyUi(player, NLayer);
            }
        }
        #endregion

        #region [Rust-Api]
        private object CanWearItem(PlayerInventory inventory, Item item)
        {
            var player = inventory.GetComponent<BasePlayer>();
            if (!IsValid(player)) return null;

            var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (Boolean?)null;
            if (isBlocked == false)
            {
                NotifyBlock(player, item.info.shortname);
                return false;
            }

            return null;
        }

        private object CanEquipItem(PlayerInventory inventory, Item item, Int32 targetPos)
        {
            var player = inventory.GetComponent<BasePlayer>();
            if (!IsValid(player)) return null;

            var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (Boolean?)null;
            if (isBlocked == false)
            {
                NotifyBlock(player, item.info.shortname);
                return false;
            }

            return null;
        }

        private object CanMoveItem(Item item, PlayerInventory inventory, ItemContainerId targetContainer)
        {
            if (inventory == null || item == null)
                return null;

            var player = inventory.GetComponent<BasePlayer>();
            if (!IsValid(player)) return null;

            var container = inventory.FindContainer(targetContainer);
            if (container == null || container.entityOwner == null)
                return null;

            var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (Boolean?)null;
            if (container.entityOwner is AutoTurret && isBlocked == false || container.entityOwner is AttackHelicopterTurret && isBlocked == false || container.entityOwner is AttackHelicopterRockets && isBlocked == false)
            {
                NotifyBlock(player, item.info.shortname);
                return true;
            }

            return null;
        }

        private object CanAcceptItem(ItemContainer container, Item item)
        {
            if (container == null || item == null || container.entityOwner == null)
                return null;

            if (container.entityOwner is AutoTurret || container.entityOwner is AttackHelicopterTurret || container.entityOwner is AttackHelicopterRockets)
            {
                var player = item.GetOwnerPlayer();
                if (!IsValid(player)) return null;

                var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (Boolean?)null;
                if (isBlocked == false)
                {
                    NotifyBlock(player, item.info.shortname);
                    return ItemContainer.CanAcceptResult.CannotAcceptRightNow;
                }
            }

            return null;
        }

        private object OnWeaponReload(BaseProjectile projectile, BasePlayer player)
        {
            if (!IsValid(player)) return null;

            var isBlocked = IsBlocked(projectile.primaryMagazine.ammoType.shortname) > 0 ? false : (Boolean?)null;
            if (isBlocked == false)
            {
                SendReply(player, LanguageEn ? $"You <color=#81B67A>can't</color> use this type of ammunition!" : $"Вы <color=#81B67A>не можете</color> использовать этот тип боеприпасов!");
                return false;
            }

            return null;
        }

        private object OnMagazineReload(BaseProjectile projectile, IAmmoContainer desiredAmount, BasePlayer player)
        {
            if (!IsValid(player)) return null;

            NextTick(() =>
            {
                var isBlocked = IsBlocked(projectile.primaryMagazine.ammoType.shortname) > 0 ? false : (Boolean?)null;
                if (isBlocked == false)
                {
                    player.GiveItem(ItemManager.CreateByItemID(projectile.primaryMagazine.ammoType.itemid, projectile.primaryMagazine.contents));
                    projectile.primaryMagazine.contents = 0;
                    projectile.GetItem().LoseCondition(projectile.GetItem().maxCondition);
                    projectile.SendNetworkUpdate();
                    player.SendNetworkUpdate();
                }
            });

            return null;
        }

        private object CanMountEntity(BasePlayer player, MLRS entity)
        {
            if (!IsValid(player)) return null;

            var isBlocked = IsBlocked("ammo.rocket.mlrs") > 0 ? false : (Boolean?)null;
            if (isBlocked == false)
            {
                SendReply(player, LanguageEn ? $"MLRS installation will be available through <color=#eb7d6a>{Convert.ToInt32(Math.Floor(TimeSpan.FromSeconds(IsBlocked("ammo.rocket.mlrs")).TotalHours))}h{TimeSpan.FromSeconds(IsBlocked("ammo.rocket.mlrs")).Minutes}m</color>" : $"Установка MLRS будет доступна через <color=#eb7d6a>{Convert.ToInt32(Math.Floor(TimeSpan.FromSeconds(IsBlocked("ammo.rocket.mlrs")).TotalHours))}ч{TimeSpan.FromSeconds(IsBlocked("ammo.rocket.mlrs")).Minutes}м</color>");
                return false;
            }

            return null;
        }

        private void OnExplosiveDropped(BasePlayer player, BaseEntity entity) => OnExplosiveThrown(player, entity);

        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (!IsValid(player)) return;
            if (!config.blockItemsThrown.Contains(entity.ShortPrefabName)) return;
             
            entity.Kill();
            SendReply(player, LanguageEn ? "You <color=#81B67A>can't</color> throw this item!" : "Вы <color=#81B67A>не можете</color> кидать этот предмет!");
        }
        #endregion

        #region [ConsoleCommand]
        [ConsoleCommand("cmdCloseWipeBlock")]
        private void cmdRemoveUi(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            CuiHelper.DestroyUi(player, Layer);
            openUI.Remove(player);
        }

        [ConsoleCommand("changePage")]
        private void cmdChangePage(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            AvailableItem(player, Int32.Parse(arg.Args[0]));
        }

        [ConsoleCommand("changeBlockPage")]
        private void cmdChangeBlockPage(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            NotAvailableItem(player, Int32.Parse(arg.Args[0]));
        }

        [ConsoleCommand("changeButton")]
        private void cmdChnageButton(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            if (arg.Args[0] == "OpenItem")
            {
                CuiHelper.DestroyUi(player, Layer + ".Main" + ".BlockItem");

                MenuButton(player, arg.Args[0]);
                AvailableItem(player);
            }
            else if (arg.Args[0] == "BlockItem")
            {
                CuiHelper.DestroyUi(player, Layer + ".Main" + ".LayerItem");

                MenuButton(player, arg.Args[0]);
                NotAvailableItem(player);
            }
        }
        #endregion

        #region [Ui]
        private void MainUi(BasePlayer player)
        {
            #region [Vars]
            if (openUI.Contains(player)) return;
            if (!openUI.Contains(player))
                openUI.Add(player);
            CuiElementContainer container = new CuiElementContainer();
            #endregion

            #region [Parrent]
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat", Color = "0 0 0 0.7" }
            }, "Overlay", Layer);

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiRawImageComponent { Png = GetImage($"{Name}.Background"), Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = "0 0 0 0.35", Material = "assets/content/ui/uibackgroundblur.mat", Sprite = "assets/content/ui/ui.background.transparent.radial.psd" }
            }, Layer);
            #endregion

            #region [Main-Ui]
            container.Add(new CuiPanel
            {
                Image = { Color = "0.3773585 0.3755785 0.3755785 0.65" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-200 -205", OffsetMax = "203 166" },
                CursorEnabled = true,
            }, Layer, Layer + ".Main");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-43 -243", OffsetMax = "46 -215" },
                Text = { Text = LanguageEn ? "CLOSE" : "ЗАКРЫТЬ", Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.65" },
                Button = { Color = "0.3773585 0.3755785 0.3755785 0.65", Command = "cmdCloseWipeBlock" }
            }, Layer);
            #endregion

            #region [Text]
            container.Add(new CuiLabel
            {
                Text = { Text = LanguageEn ? "TIME LOCKS" : "ВРЕМЕННЫЕ БЛОКИРОВКИ", Color = "1 1 1 0.85", FontSize = 32, Font = "robotocondensed-bold.ttf", Align = TextAnchor.UpperCenter },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-393 166", OffsetMax = "395 245" },
            }, Layer);
            #endregion

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
            AvailableItem(player);
            MenuButton(player, "OpenItem");
        }

        private void MenuButton(BasePlayer player, String Name)
        {
            #region [Vars]
            CuiElementContainer container = new CuiElementContainer();
            #endregion

            #region [Parrent]
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5" },
                Image = { Color = "0 0 0 0" }
            }, Layer, Layer + ".MenuButton");
            #endregion

            #region [Button]
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-107 174", OffsetMax = "-2 199" },
                Text = { Text = LanguageEn ? "ACCESSIBLE" : "ДОСТУПНЫЕ", Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = Name == "OpenItem" ? $"1 1 1 0.65" : "1 1 1 0.3" },
                Button = { Color = Name == "OpenItem" ? "0.3773585 0.3755785 0.3755785 0.65" : "0.3773585 0.3755785 0.3755785 0.45", Command = "changeButton OpenItem" }
            }, Layer + ".MenuButton");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "10 174", OffsetMax = "122.5 199" },
                Text = { Text = LanguageEn ? "IN BLOCK" : "В БЛОКИРОВКЕ", Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = Name == "BlockItem" ? $"1 1 1 0.65" : "1 1 1 0.3" },
                Button = { Color = Name == "BlockItem" ? "0.3773585 0.3755785 0.3755785 0.65" : "0.3773585 0.3755785 0.3755785 0.45", Command = "changeButton BlockItem" }
            }, Layer + ".MenuButton");
            #endregion

            CuiHelper.DestroyUi(player, Layer + ".MenuButton");
            CuiHelper.AddUi(player, container);
        }

        private void AvailableItem(BasePlayer player, Int32 page = 0)
        {
            #region [Vars]
            CuiElementContainer container = new CuiElementContainer();

            var Items = config.blockItems.Where(x => !BlockTimeGui(x.Key)).OrderBy(x => x.Value).ToList();
            var ItemsBlock = Items.Skip(20 * page).Take(20).ToList();
            #endregion

            #region [Main-Ui]
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = "0 0 0 0" }
            }, Layer + ".Main", Layer + ".Main" + ".LayerItem");
            #endregion

            #region [Items]
            for (Int32 i = 0, x = 0, y = 0; i < 20; i++)
            {

                if (ItemsBlock.Count - 1 >= i)
                {
                    String Name = ItemManager.FindItemDefinition(ItemsBlock[i].Key)?.displayName?.english;

                    if (String.IsNullOrEmpty(Name))
                        Name = "UNKNOWN";

                    if (Name == "Double Barrel Shotgun")
                        Name = "Double Barrel";

                    if (Name == "Semi-Automatic Pistol")
                        Name = "Semi-Automatic";

                    if (Name == "Ice Metal Chest Plate")
                        Name = "Ice Metal Chest";

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{0.036 + x * 0.187} {0.735 - y * 0.226}", AnchorMax = $"{0.206 + x * 0.187} {0.945 - y * 0.226}" },
                        Image = { Color = "0 0 0 0.5" }
                    }, Layer + ".Main" + ".LayerItem", Layer + ".Main" + ".LayerItem" + $".Item{i}");

                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Main" + ".LayerItem" + $".Item{i}",
                        Components =
                        {
                            new CuiImageComponent { ItemId = FindItemID(ItemsBlock[i].Key), SkinId = 0 },
                            new CuiRectTransformComponent { AnchorMin = "0.15 0.25", AnchorMax = "0.85 0.9" }
                        }
                    });

			        container.Add(new CuiElement
			        {
				        Parent = Layer + ".Main" + ".LayerItem" + $".Item{i}",
				        Components =
				        {
					        new CuiTextComponent { Text = $"{Name}", Font = "robotocondensed-regular.ttf", FontSize = 8, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.85" },
					        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 0.28" }
				        }
			        });

                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Main" + ".LayerItem" + $".Item{i}",
                        Components =
                        {
                            new CuiRawImageComponent { Png = GetImage($"ItemFon"), Color = "1 1 1 1" },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                        }
                    });
                }
                else
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{0.036 + x * 0.187} {0.735 - y * 0.226}", AnchorMax = $"{0.206 + x * 0.187} {0.945 - y * 0.226}" },
                        Image = { Color = "0 0 0 0" }
                    }, Layer + ".Main" + ".LayerItem", Layer + ".Main" + ".LayerItem" + $".Item{i}");

                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Main" + ".LayerItem" + $".Item{i}",
                        Components =
                        {
                            new CuiRawImageComponent { Png = GetImage($"{Name}.BlockFon"), Color = "0 0 0 1" },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                        }
                    });

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Image = { Color = "0 0 0 0.5" }
                    }, Layer + ".Main" + ".LayerItem" + $".Item{i}");
                }

                x++;
                if (x == 5)
                {
                    x = 0;
                    y++;
                }
            }
            #endregion

            #region [Page]
            container.Add(new CuiButton
            {
                Button = { Color = "0.3773585 0.3755785 0.3755785 0.65", Command = Items.Skip(20 * (page + 1)).Count() > 0 ? $"changePage {page + 1}" : "" },
                Text = { Text = ">", FontSize = 18, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter, Color = Items.Skip(20 * (page + 1)).Count() > 0 ? "1 1 1 0.65" : "1 1 1 0.15" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "207.5 -13", OffsetMax = "234 13" }
            }, Layer + ".Main" + ".LayerItem");

            container.Add(new CuiButton
            {
                Button = { Color = "0.3773585 0.3755785 0.3755785 0.65", Command = page >= 1 ? $"changePage {page - 1}" : "" },
                Text = { Text = "<", FontSize = 18, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter, Color = page >= 1 ? "1 1 1 0.65" : "1 1 1 0.15" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-234 -13", OffsetMax = "-207.5 13" }
            }, Layer + ".Main" + ".LayerItem");
            #endregion

            CuiHelper.DestroyUi(player, Layer + ".Main" + ".LayerItem");
            CuiHelper.AddUi(player, container);
        }

        private void NotAvailableItem(BasePlayer player, Int32 page = 0)
        {
            #region [Vars]
            CuiElementContainer container = new CuiElementContainer();

            var Items = config.blockItems.Where(x => BlockTimeGui(x.Key)).OrderBy(x => x.Value).ToList();
            var ItemsBlock = Items.Skip(20 * page).Take(20).ToList();
            #endregion

            #region [Main-Ui]
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = "0 0 0 0" }
            }, Layer + ".Main", Layer + ".Main" + ".BlockItem");
            #endregion

            #region [Items]
            for (Int32 i = 0, x = 0, y = 0; i < 20; i++)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{0.036 + x * 0.187} {0.735 - y * 0.226}", AnchorMax = $"{0.206 + x * 0.187} {0.945 - y * 0.226}" },
                    Image = { Color = "0 0 0 0" }
                }, Layer + ".Main" + ".BlockItem", Layer + ".Main" + ".BlockItem" + $".Item{i}");

                if (ItemsBlock.Count - 1 >= i)
                {
                    String Name = ItemManager.FindItemDefinition(ItemsBlock[i].Key)?.displayName?.english;

                    if (String.IsNullOrEmpty(Name))
                        Name = "UNKNOWN";

                    if (Name == "Double Barrel Shotgun")
                        Name = "Double Barrel";

                    if (Name == "Semi-Automatic Pistol")
                        Name = "Semi-Automatic";

                    if (Name == "Ice Metal Chest Plate")
                        Name = "Ice Metal Chest";

                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Main" + ".BlockItem" + $".Item{i}",
                        Components =
                        {
                            new CuiImageComponent { ItemId = FindItemID(ItemsBlock[i].Key), SkinId = 0, Color = "1 1 1 0.35" },
                            new CuiRectTransformComponent { AnchorMin = "0.15 0.25", AnchorMax = "0.85 0.9" }
                        }
                    });

			        container.Add(new CuiElement
			        {
				        Parent = Layer + ".Main" + ".BlockItem" + $".Item{i}",
				        Components =
				        {
					        new CuiTextComponent { Text = $"{Name}", Font = "robotocondensed-regular.ttf", FontSize = 8, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.85" },
					        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 0.28" }
				        }
			        });

                    var time = IsBlocked(ItemsBlock[i].Key);
                    if (time >= 3600)
                    {
                        container.Add(new CuiElement
                        {
                            Parent = Layer + ".Main" + ".BlockItem" + $".Item{i}",
                            Name = Layer + ".Main" + ".BlockItem" + $".Item{i}" + "Time",
                            Components =
                            {
                                new CuiTextComponent { Text = LanguageEn ? $"<color=#FFFFFF>{Convert.ToInt32(Math.Floor(TimeSpan.FromSeconds(time).TotalHours))}h{TimeSpan.FromSeconds(time).Minutes}m</color>" : $"<color=#FFFFFF>{Convert.ToInt32(Math.Floor(TimeSpan.FromSeconds(time).TotalHours))}ч{TimeSpan.FromSeconds(time).Minutes}м</color>", FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                                new CuiRectTransformComponent { AnchorMin = "0.04 0", AnchorMax = "1 1" },
                            }
                        });
                    }
                    else if (time <= 3600 && time > 60)
                    {
                        container.Add(new CuiElement
                        {
                            Parent = Layer + ".Main" + ".BlockItem" + $".Item{i}",
                            Name = Layer + ".Main" + ".BlockItem" + $".Item{i}" + "Time",
                            Components =
                            {
                                new CuiTextComponent { Text = LanguageEn ? $"<color=#FFFFFF>{TimeSpan.FromSeconds(time).Minutes}m</color>" : $"<color=#FFFFFF>{TimeSpan.FromSeconds(time).Minutes}м</color>", FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                                new CuiRectTransformComponent { AnchorMin = "0.04 0", AnchorMax = "1 1" },
                            }
                        });
                    }
                    else if (time <= 60 && time > 0)
                    {
                        container.Add(new CuiElement
                        {
                            Parent = Layer + ".Main" + ".BlockItem" + $".Item{i}",
                            Name = Layer + ".Main" + ".BlockItem" + $".Item{i}" + "Time",
                            Components =
                            {
                                new CuiTextComponent { Text = LanguageEn ? $"<color=#FFFFFF>{TimeSpan.FromSeconds(time).Seconds}s</color>" : $"<color=#FFFFFF>{TimeSpan.FromSeconds(time).Seconds}с</color>", FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                                new CuiRectTransformComponent { AnchorMin = "0.04 0", AnchorMax = "1 1" },
                            }
                        });
                    }
                }

                container.Add(new CuiElement
                {
                    Parent = Layer + ".Main" + ".BlockItem" + $".Item{i}",
                    Components =
                    {
                        new CuiRawImageComponent { Png = GetImage($"{Name}.BlockFon"), Color = "0 0 0 1" },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                    }
                });

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Image = { Color = "0 0 0 0.5" }
                }, Layer + ".Main" + ".BlockItem" + $".Item{i}");

                x++;
                if (x == 5)
                {
                    x = 0;
                    y++;
                }
            }
            #endregion

            #region [Page]
            container.Add(new CuiButton
            {
                Button = { Color = "0.3773585 0.3755785 0.3755785 0.65", Command = Items.Skip(20 * (page + 1)).Count() > 0 ? $"changeBlockPage {page + 1}" : "" },
                Text = { Text = ">", FontSize = 18, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter, Color = Items.Skip(20 * (page + 1)).Count() > 0 ? "1 1 1 0.65" : "1 1 1 0.15" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "207.5 -13", OffsetMax = "234 13" }
            }, Layer + ".Main" + ".BlockItem");

            container.Add(new CuiButton
            {
                Button = { Color = "0.3773585 0.3755785 0.3755785 0.65", Command = page >= 1 ? $"changeBlockPage {page - 1}" : "" },
                Text = { Text = "<", FontSize = 18, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter, Color = page >= 1 ? "1 1 1 0.65" : "1 1 1 0.15" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-234 -13", OffsetMax = "-207.5 13" }
            }, Layer + ".Main" + ".BlockItem");
            #endregion

            CuiHelper.DestroyUi(player, Layer + ".Main" + ".BlockItem");
            CuiHelper.AddUi(player, container);
        }

        private void NotifyBlock(BasePlayer player, String shortname)
        {
            CuiHelper.DestroyUi(player, NLayer);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                Image = { Color = "0.5 0.5 0.5 0.25", Material = "assets/icons/greyout.mat" },
                RectTransform = {AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-245.182 -155.661", OffsetMax = "-2.618 -102.735"},
                CursorEnabled = false,
            }, "Overlay", NLayer);

            container.Add(new CuiElement
            {
                Parent = NLayer,
                Name = NLayer + ".BlockItem",
                Components =
                 {
                    new CuiImageComponent {Color = "0.49 0.44 0.38 0.75", Material = "assets/icons/greyout.mat"},
                    new CuiRectTransformComponent { AnchorMin = "0.01586128 0.08839238", AnchorMax = "0.1925 0.9208925" }
                 }
            });

			container.Add(new CuiElement
			{
				Parent = NLayer + ".BlockItem",
				Components =
				{
					new CuiImageComponent { ItemId = FindItemID(shortname), SkinId = 0 },
					new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
				}
			});

            container.Add(new CuiElement
            {
                Parent = NLayer,
                Components =
                {
                    new CuiTextComponent() { Color = "1 1 1 0.65", Text = LanguageEn ? "Item locked!" : "Предмет заблокирован!", FontSize = 14, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0.215 0.585", AnchorMax = "1 1"},
                    new CuiOutlineComponent{Color = "0 0 0 1", Distance = "0.15 0.15"},
                }
            });

            container.Add(new CuiElement
            {
                Parent = NLayer,
                Components =
                {
                    new CuiTextComponent() { Color = "1 1 1 0.65", Text = LanguageEn ? "Information on blocked items - /block" : "Информация о заблокированных\nпредметах - /block", FontSize = 12, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0.215 0", AnchorMax = "1 0.7"},
                    new CuiOutlineComponent{Color = "0 0 0 1", Distance = "0.15 0.15"},
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.9 0 0 0.65", Material = "assets/icons/greyout.mat", Close = NLayer },
                RectTransform = { AnchorMin = "0.94 0.725", AnchorMax = "0.995 0.98" }
            }, NLayer, "CloseX");

            container.Add(new CuiElement
            {
                Parent = "CloseX",
                Components =
                {
                    new CuiTextComponent() { Color = "1 1 1 0.65", Text = $"✘", FontSize = 12, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"},
                    new CuiOutlineComponent{Color = "0 0 0 1", Distance = "0.35 0.35"},
                }
            });

            timer.Once(15f, () =>
            {
                  CuiHelper.DestroyUi(player, NLayer);
            });
            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region [Func]
        private Double IsBlocked(String shortName)
        {
            if (!config.blockItems.ContainsKey(shortName)) return 0;

            var blockTime = UnBlockTime(config.blockItems[shortName]) - CurrentTime();
            return blockTime > 0 ? blockTime : 0;
        }

        private Boolean BlockTimeGui(String shortName)
        {
            var blockTime = UnBlockTime(config.blockItems[shortName]) - CurrentTime();
            if (blockTime > 0) return true;

            return false;
        }

        private Boolean IsValid(BasePlayer player)
        {
            return player != null && player.userID.IsSteamId() &&
                   !permission.UserHasPermission(player.UserIDString, IgnorePermission);
        }

		private Int32 FindItemID(String shortName)
		{
			Int32 val;
			if (_itemIds.TryGetValue(shortName, out val))
				return val;

			var definition = ItemManager.FindItemDefinition(shortName);
			if (definition == null) return 0;

			val = definition.itemid;
			_itemIds[shortName] = val;
			return val;
		}

        private void CheckBlockedItem(BasePlayer player, Item item)
        {
            if (!IsValid(player)) return;

            var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (Boolean?)null;
            if (isBlocked == false)
            {
                if (item.MoveToContainer(player.inventory.containerMain))
                    player.Command("note.inv", item.info.itemid, item.amount,
                        !String.IsNullOrEmpty(item.name) ? item.name : String.Empty,
                        (Int32) BaseEntity.GiveItemReason.PickedUp);
                else
                    item.Drop(player.inventory.containerMain.dropPosition,
                        player.inventory.containerMain.dropVelocity);
            }
        }

        private void CheckPlayers()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                player.inventory.containerBelt.itemList.ToList().ForEach(item => CheckBlockedItem(player, item));
                player.inventory.containerWear.itemList.ToList().ForEach(item => CheckBlockedItem(player, item));
            }
        }
        #endregion
    }
}