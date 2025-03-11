using Facepunch;
using Oxide.Core;
using System.Text;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using Oxide.Plugins.TPShopExtensionMethods;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using System.Collections;


namespace Oxide.Plugins
{
    [Info("TPShop", "https://topplugin.ru", "11.0.1")]
    [Description("Дополнение для сборки")]
    internal class TPShop : RustPlugin
    {
        
        private void CONFIRMATIONSUI(BasePlayer player, int itemID, int indexCategory, int amount, ThemeType themetype)
        {
            Configuration.InterfaceSettings.ThemeCustomization theme = themetype == ThemeType.Dark ? _config.interfaceSettings.DarkTheme : _config.interfaceSettings.LightTheme;
            Configuration.CategoryShop.Product product = GetCategories(player)[indexCategory].product.Find(x => x.ID == itemID);
            float discount = GetUserDiscount(player);
            int playerLim = GetLimit(player, product, true);
            string limit = playerLim == -1 ? GetLang("TPShop_UI_PATTERN", player.UserIDString) : playerLim + "/" + product.GetLimitLotWipe(player, true);
            CuiElementContainer container = new CuiElementContainer
            {
                new CuiElement
                {
                    Name = ConfirmationsPanel,
                    Parent = PanelProduct,
                    Components = {
                    new CuiRawImageComponent { Color = theme.colorMainBG, Png =GetImage($"{Name}_" +"13") },
                    new CuiRectTransformComponent {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-230.488 -109.615", OffsetMax = "261.007 77.799"}
                }
                },

                new CuiElement
                {
                    Name = "IMAGE_PRODUCT_BACKGROUND",
                    Parent = ConfirmationsPanel,
                    Components = {
                    new CuiRawImageComponent { Color = theme.colortext6, Png =GetImage($"{Name}_" +"15") },
                    new CuiRectTransformComponent {  AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "46.52 -114.22", OffsetMax = "122.52 -42.22" }
                }
                }
            };
            
             if (!string.IsNullOrWhiteSpace(product.Url) || product.Type == ItemType.Kit || product.Type == ItemType.Command)
             {
                 container.Add(new CuiElement
                 {
                     Name = "PRODUCT_IMAGE",
                     Parent = "IMAGE_PRODUCT_BACKGROUND",
                     Components = {
                         new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage(string.IsNullOrWhiteSpace(product.Url) ? "NONE": product.Url)},
                         new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30 -30", OffsetMax = "30 30" }
                     }
                 });
             }
             else if (product.Type == ItemType.CustomItem)
             {
                 container.Add(new CuiElement
                 {
                     Name = "PRODUCT_IMAGE",
                     Parent = "IMAGE_PRODUCT_BACKGROUND",
                     Components = {
                         new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage(product.ShortName + 60, product.SkinID) },
                         new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30 -30", OffsetMax = "30 30" }
                     }
                 });
             }
             else if (product.Type == ItemType.Item)
             {
                 container.Add(new CuiElement
                 {
                     Name = "PRODUCT_IMAGE",
                     Parent = "IMAGE_PRODUCT_BACKGROUND",
                     Components = {
                         new CuiImageComponent { Color = "1 1 1 1", ItemId = ItemManager.FindItemDefinition(product.ShortName).itemid },
                         new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30 -30", OffsetMax = "30 30" }
                     }
                 });
             }
             else
             {
                 container.Add(new CuiElement
                 {
                     Name = "PRODUCT_IMAGE",
                     Parent = "IMAGE_PRODUCT_BACKGROUND",
                     Components = {
                         new CuiImageComponent { Color = "1 1 1 1", ItemId = ItemManager.FindItemDefinition("blueprintbase").itemid },
                         new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30 -30", OffsetMax = "30 30" }
                     }
                 });
                
                 container.Add(new CuiElement
                 {
                     Name = "PRODUCT_IMAGE",
                     Parent = "IMAGE_PRODUCT_BACKGROUND",
                     Components = {
                         new CuiImageComponent { Color = "1 1 1 1", ItemId = ItemManager.FindItemDefinition(product.ShortName).itemid },
                         new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30 -30", OffsetMax = "30 30" }
                     }
                 });
             }

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-118.134 -68.265", OffsetMax = "8.346 -39.543" },
                Text = { Text = product.GetProductName(player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.LowerLeft, Color = theme.colortext7 },
            }, ConfirmationsPanel);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-118.134 -137.191", OffsetMax = "8.346 -70.689" },
                Text = { Text = GetLang("TPShop_UI_PRODUCT_INFO", player.UserIDString, GetDiscountedPrice(product.Price * amount, discount) + _config.economicsCustomization.PrefixBalance, product.Amount * amount, limit), Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = theme.Colortext5 },
            }, ConfirmationsPanel);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-228.122 -8.202", OffsetMax = "-44.678 54.164" },
                Text = { Text = product.GetProductDescription(), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.UpperLeft, Color = theme.Colortext5 },
            }, ConfirmationsPanel);

            container.Add(new CuiElement
            {
                Name = "PRODUCT_BUY_BTN",
                Parent = ConfirmationsPanel,
                Components = {
                    new CuiRawImageComponent { Color = theme.colortext8, Png =GetImage($"{Name}_" +"14") },
                    new CuiRectTransformComponent {AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-162.4 42.9", OffsetMax = "-109.4 63.9"}
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Command = $"UI_HandlerShop PRODUCT_BUY {itemID} {indexCategory} {amount} {themetype}", Color = "0 0 0 0" },
                Text = { Text = GetLang("TPShop_UI_BTN_BUY", player.UserIDString).ToUpper(), Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter }
            }, "PRODUCT_BUY_BTN");
		   		 		  						  	   		  		 			  		 			  			 		  		  
            container.Add(new CuiElement
            {
                Name = "PRODUCT_CLOSE_BTN",
                Parent = ConfirmationsPanel,
                Components = {
                    new CuiRawImageComponent { Color = theme.closeBtnColor2, Png = GetImage($"{Name}_" +"14") },
                    new CuiRectTransformComponent {AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-100 42.9", OffsetMax = "-47 63.9"  }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Close = "CONFIRMATIONS_PANEL", Color = "0 0 0 0" },
                Text = { Text = GetLang("TPShop_UI_PRODUCT_INFO_EXIT", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter }
            }, "PRODUCT_CLOSE_BTN");
		   		 		  						  	   		  		 			  		 			  			 		  		  
            CuiHelper.DestroyUi(player, ConfirmationsPanel);
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("TPShop.refill")]
        private void CmdShopItemsRefill(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel <= 0) return;
            SendReply(arg, GetLang("TPShop_SERVICE_CMD_REFILL"));
            ulong userId = arg.Connection == null || arg.IsRcon ? 0U : arg.Connection.userid;
            if (_pdpw.Contains(userId)) return;
            _pdpw.Add(userId);
            timer.In(15, () =>
            {
                if (_pdpw.Contains(userId))
                    _pdpw.Remove(userId);
            });
        }
                private const string MainLayer = "MAIN_SHOP_LAYER";

        private class CooldownData
        {
            public DateTime Buy = new DateTime(1970, 1, 1, 0, 0, 0);

            public DateTime Sell = new DateTime(1970, 1, 1, 0, 0, 0);
        }
		   		 		  						  	   		  		 			  		 			  			 		  		  
		   		 		  						  	   		  		 			  		 			  			 		  		  
        private void AddLimit(BasePlayer player, Configuration.CategoryShop.Product item, int lots, bool purchase)
        {
            PlayerLimits.GetOrAddPlayer(player.userID).AddItem(item, lots, purchase);
        }
        private void Purchasing(BasePlayer player, int itemId, int category, int amount, ThemeType themetype)
        {
            Configuration.CategoryShop.Product product = GetCategories(player)[category].product.Find(x => x.ID == itemId);

            if (product != null)
            {
                if (IsDuel(player))
                {
                    NotificationUI(player, NotificationType.Error, GetLang("TPShop_UI_NOTIFICATION_IS_DUEL", player.UserIDString));
                    return;
                }
                if (_config.mainSettings.RaidBlock && IsRaid(player))
                {
                    NotificationUI(player, NotificationType.Error, GetLang("TPShop_UI_NOTIFICATION_IS_RAID", player.UserIDString));
                    return;
                }
                if (product.Type == ItemType.Blueprint || product.Type == ItemType.CustomItem || product.Type == ItemType.Item)
                {
                    List<int> stack = GetStacks(product.Definition, product.Amount * amount);
                    int slots = player.inventory.containerBelt.capacity -
                                   player.inventory.containerBelt.itemList.Count +
                                   (player.inventory.containerMain.capacity -
                                    player.inventory.containerMain.itemList.Count);
                    if (slots < stack.Count)
                    {
                        NotificationUI(player, NotificationType.Warning, GetLang("TPShop_UI_NOTIFICATION_NOT_ENOUGH_SPACE", player.UserIDString));
                        return;
                    }
                }

		   		 		  						  	   		  		 			  		 			  			 		  		  
                float price = GetDiscountedPrice(product.Price * amount, GetUserDiscount(player));
                if (GetBalance(player) < price)
                {
                    NotificationUI(player, NotificationType.Error, GetLang("TPShop_UI_NOTIFICATION_INSUFFICIENT_FUNDS", player.UserIDString));
                    return;
                }
                if (product.BuyCooldown > 0)
                {
                    int cooldown = GetCooldownTime(player.userID, product, true);
                    if (cooldown > 0)
                    {
                        NotificationUI(player, NotificationType.Warning, GetLang("TPShop_UI_NOTIFICATION_BUY_RECHARGE", player.UserIDString, product.GetProductName(player.UserIDString), TimeHelper.FormatTime(TimeSpan.FromSeconds(cooldown), 5, lang.GetLanguage(player.UserIDString))));
                        return;
                    }
                    SetCooldown(player, product, true);
                }

                int limit = GetLimit(player, product, true);
                if (limit != -1)
                {
                    if (limit == 0)
                    {
                        NotificationUI(player, NotificationType.Warning, GetLang("TPShop_UI_NOTIFICATION_BUY_LIMIT", player.UserIDString, product.GetProductName(player.UserIDString)));
                        return;
                    }
                    else if (amount > limit)
                    {
                        NotificationUI(player, NotificationType.Warning, GetLang("TPShop_UI_NOTIFICATION_BUY_LIMIT_1", player.UserIDString, product.GetProductName(player.UserIDString), limit));
                        return;
                    }
                    AddLimit(player, product, amount, true);
                }

                switch (_config.economicsCustomization.TypeEconomic)
                {
                    case EconomicsType.Economics:
                        if ((bool)Economics?.Call("Withdraw", player.userID, (double)price))
                        {
                            GiveProduct(player, product, amount);
                            NotificationUI(player, NotificationType.Success, GetLang("TPShop_UI_NOTIFICATION_SUCCESSFUL_PURCHASE", player.UserIDString, product.GetProductName(player.UserIDString)));
                        }
                        break;
                    case EconomicsType.ServerRewards:
                        if (ServerRewards?.Call<object>("TakePoints", player.userID, (int)price) != null)
                        {
                            GiveProduct(player, product, amount);
                            NotificationUI(player, NotificationType.Success, GetLang("TPShop_UI_NOTIFICATION_SUCCESSFUL_PURCHASE", player.UserIDString, product.GetProductName(player.UserIDString)));
                        }
                        break;
                    case EconomicsType.IQEconomic:
                        IQEconomic?.Call("API_REMOVE_BALANCE", player.userID, (int)price);
                        GiveProduct(player, product, amount);
                        NotificationUI(player, NotificationType.Success, GetLang("TPShop_UI_NOTIFICATION_SUCCESSFUL_PURCHASE", player.UserIDString, product.GetProductName(player.UserIDString)));
                        break;
                    
                    /*case EconomicsType.TPEconomic:
                        TPEconomic?.Call("API_PUT_BALANCE_MINUS", player.userID, (float)price);
                        GiveProduct(player, product, amount);
                        NotificationUI(player, NotificationType.Success, GetLang("TPShop_UI_NOTIFICATION_SUCCESSFUL_PURCHASE", player.UserIDString, product.GetProductName(player.UserIDString)));
                        if(TPMenuSystem)
                            TPMenuSystem?.Call("UpdateUIBalance", player);
                        break;*/
                }
            }
        }
        private const string ProductAmountChangeBg = "PRODUCT_AMOUNT_CHANGE_BG";
                
        
        private enum EconomicsType
        {
            Economics,
            ServerRewards,
            IQEconomic,
            Item,
            TPEconomic,
        }
        
                private void NotificationUI(BasePlayer player, NotificationType type, string msg)
        {
            string color = type == NotificationType.Error ? "1.00 0.30 0.31 1.00" : type == NotificationType.Warning ? "0.98 0.68 0.08 1.00" : "0.32 0.77 0.10 1.00";
            ThemeType themeType = _playerThemeSelect[player.userID];
            Configuration.InterfaceSettings.ThemeCustomization theme = themeType == ThemeType.Dark ? _config.interfaceSettings.DarkTheme : _config.interfaceSettings.LightTheme;

            CuiElementContainer container = new CuiElementContainer
            {
                new CuiElement
                {
                    FadeOut = 0.30f,
                    Name = NotificationMain,
                    Parent = "OverlayNonScaled",
                    Components = {
                        new CuiRawImageComponent { Color = theme.colorMainBG,  Png =GetImage($"{Name}_" +"16"), FadeIn = 0.30f },
                        new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "112 -78", OffsetMax = "383 -10" }
                    }
                },

                new CuiElement
                {
                    FadeOut = 0.30f,
                    Name = "NOTIFICATION_POLOSA",
                    Parent = NotificationMain,
                    Components = {
                        new CuiRawImageComponent { Color = color, Png = GetImage($"{Name}_" +"17") , FadeIn = 0.30f},
                        new CuiRectTransformComponent { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-95.99 -1.97", OffsetMax = "75.01 2.03" }
                    }
                },

                new CuiElement
                {
                    FadeOut = 0.30f,
                    Name = "NOTIFICATION_IMG",
                    Parent = NotificationMain,
                    Components = {
                        new CuiRawImageComponent { Color = color, Png = GetImage($"{Name}_" +"10") , FadeIn = 0.30f},
                        new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "13.5 -32.47", OffsetMax = "73.5 28.53" }
                    }
                },

                {
                    new CuiLabel
                    {
                        FadeOut = 0.30f,
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-58 -32.47", OffsetMax = "119.546 28.53" },
                        Text = { Text = msg, Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = theme.Colortext10, FadeIn = 0.30f },
                    },
                    NotificationMain,
                    "NOTIFICATION_TEXT"
                }
            };

            CuiHelper.DestroyUi(player, "NOTIFICATION_POLOSA");
            CuiHelper.DestroyUi(player, "NOTIFICATION_IMG");
            CuiHelper.DestroyUi(player, "NOTIFICATION_TEXT");
            CuiHelper.DestroyUi(player, NotificationMain);
            CuiHelper.AddUi(player, container);
		   		 		  						  	   		  		 			  		 			  			 		  		  
            DeleteNotification(player);
        }

                
        
                private void GiveProduct(BasePlayer player, Configuration.CategoryShop.Product product, int amount)
        {
            switch (product.Type)
            {
                case ItemType.Item:
                    GetStacks(product.Definition, product.Amount * amount)?.ForEach(stack =>
                    {
                        Item newItem = ItemManager.CreateByPartialName(product.ShortName, stack);
                        player.GiveItem(newItem, BaseEntity.GiveItemReason.PickedUp);
                    });
                    break;
                case ItemType.Blueprint:
                    GetStacks(product.Definition, product.Amount * amount)?.ForEach(stack =>
                    {
                        Item itemBp = ItemManager.CreateByItemID(-996920608, stack);
                        if (itemBp.instanceData == null)
                            itemBp.instanceData = new ProtoBuf.Item.InstanceData();
                        itemBp.instanceData.ShouldPool = false;
                        itemBp.instanceData.blueprintAmount = 1;
                        itemBp.instanceData.blueprintTarget = product.Definition.itemid;
                        itemBp.MarkDirty();
                        player.GiveItem(itemBp, BaseEntity.GiveItemReason.PickedUp);
                    });
                    break;
                case ItemType.CustomItem:
                    GetStacks(product.Definition, product.Amount * amount)?.ForEach(stack =>
                    {
                        Item customItem = ItemManager.CreateByPartialName(product.ShortName, stack, product.SkinID);
                        customItem.name = product.Name;
                        player.GiveItem(customItem, BaseEntity.GiveItemReason.PickedUp);
                    });
                    break;
                case ItemType.Command:
                    foreach (string cammand in product.Commands)
                        Server.Command(cammand.Replace("%STEAMID%", player.UserIDString));
                    break;
                case ItemType.Kit:
                    if (Kits)
                        Kits.Call("GiveKit", player, product.KitName);
                    else if (IQKits)
                        IQKits.Call("API_KIT_GIVE", player, product.KitName);
                    break;
            }
        }

        [ConsoleCommand("TPShop.no")]
        private void CmdShopItemsRefillNo(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel <= 0) return;
            ulong userId = arg.Connection == null || arg.IsRcon ? 0U : arg.Connection.userid;
            if (!_pdpw.Contains(userId)) return;
            SendReply(arg, GetLang("TPShop_SERVICE_CMD_REFILL_NO"));
            _pdpw.Remove(userId);
        }
        private float GetDiscountedPrice(float price, float discount)
        {
            float dicsount = (price * discount / 100);
            return (float)Math.Round(price - dicsount, 2);
        }
        
        private void UPDATEAMOUNTUI(BasePlayer player, int categoryIndex, int cat, int amount)
        {
            Configuration.CategoryShop.Product item = GetCategories(player)[cat].product.Find(x => x.ID == categoryIndex);
            if (amount <= 0 || (item.GetLimitLot(player, true) != 0 && amount > item.GetLimitLot(player, true)) || amount >= 100)
                return;
            float discount = GetUserDiscount(player);

            CuiHelper.DestroyUi(player, $"PRODUCT_BACKGROUND_BUTTON_{item.ID}");
            CuiHelper.DestroyUi(player, $"PRODUCT_PRICE_{item.ID}");
            CuiHelper.DestroyUi(player, $"PRODUCT_AMOUNT_CHANGE_{item.ID}");
            CuiHelper.DestroyUi(player, $"PRODUCT_IMAGE_{item.ID}");
            CuiHelper.DestroyUi(player, $"PRODUCT_BUY_BTN_IMAGE_{item.ID}");

            var container = new CuiElementContainer();  
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -55", OffsetMax = "5 34" },
                Text = { Text = $"{GetDiscountedPrice(item.Price, discount)}{_config.economicsCustomization.PrefixBalance}", Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.UpperLeft, Color = "1 1 1 0.7" },
            }, $"PRODUCT_BACKGROUND_{item.ID}", $"PRODUCT_PRICE_{item.ID}");

            var imageItemContainer = UIPRODUCTGETIMAGEITEM(item);
            foreach (var element in imageItemContainer)
            {
                container.Add(element);
            }

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "8 5", OffsetMax = "-8 -50" },
                Button = { Command = $"UI_HandlerShop PRODUCT_BUY_CONFIRMATION {item.ID} {cat} {amount} 1", Color = "1 1 1 0" },
            }, $"PRODUCT_BACKGROUND_{item.ID}", $"PRODUCT_BUY_BTN_{item.ID}");
            container.Add(new CuiElement
            {
                Name = $"PRODUCT_BUY_BTN_IMAGE_{item.ID}",
                Parent = $"PRODUCT_BUY_BTN_{item.ID}",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = GetImage($"{Name}_" +"20"),
                    },

                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = $"PRODUCT_BUY_BTN_IMAGE_{item.ID}",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetLang("TPShop_UI_BTN_BUY", player.UserIDString),
                        FontSize = 11,
                        Align = TextAnchor.MiddleCenter,
                    },

                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                    },
                }
            });
            //ПЕРЕКЛЮЧАТЕЛЬ + -
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "1 1 1 0" },
                    RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-50 45", OffsetMax = "0 70" }
                }, $"PRODUCT_BACKGROUND_{item.ID}", $"PRODUCT_AMOUNT_CHANGE_{item.ID}");
                //КАРТИНКА ПЕРЕКЛЮЧАТЕЛЯ
                container.Add(new CuiElement
                {
                    Name = ProductAmountChangeBg + $"_{item.ID}",
                    Parent = $"PRODUCT_AMOUNT_CHANGE_{item.ID}",
                    Components = {
                    new CuiRawImageComponent { Color ="1 1 1 1", Png =GetImage($"{Name}_" +"9") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-20 -4", OffsetMax = "20 10" }
                }
                });
                //КНОПКА ПЕРЕКЛЮЧАТЕЛЯ -
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "0 -8", OffsetMax = "14.936 8" },
                    Button = { Command = $"UI_HandlerShop AMOUNT_CHANGE_MINUS {item.ID} {cat} {amount} 0", Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, ProductAmountChangeBg + $"_{item.ID}", $"PRODUCT_AMOUNT_MINUS_{item.ID}");
                
                //КНОПКА ПЕРЕКЛЮЧАТЕЛЯ +
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-15.628 -8", OffsetMax = "0 8" },
                    Button = { Command = $"UI_HandlerShop AMOUNT_CHANGE_PLUS {item.ID} {cat} {amount} 0", Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, ProductAmountChangeBg + $"_{item.ID}", $"PRODUCT_AMOUNT_PLUS_{item.ID}");

                //ЦИФЕРКА ПЕРЕКЛЮЧАТЕЛЯ
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-10.064 -8", OffsetMax = "9.372 8" },
                    Text = { Text = $"{amount}", Font = "robotocondensed-regular.ttf", FontSize = 9, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.8" },
                }, ProductAmountChangeBg + $"_{item.ID}", $"PRODUCT_LOT_COUNT_{item.ID}");

            CuiHelper.AddUi(player, container);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (_uiPlayerUseNow.Contains(player.userID))
                _uiPlayerUseNow.Remove(player.userID);
            if (_config.humanNpcs.UseHumanNpcs)
                if (_humanNpcPlayerOpen.ContainsKey(player.userID))
                    _humanNpcPlayerOpen.Remove(player.userID);
        }

        
        private void OnNewSave(string filename)
        {
            _playerDataLimits.Players.Clear();
            _playerDataCooldowns.Clear();
        }
        private const string CategoryLayer = "CATEGORY_LAYER";
        
        
                
        private static Dictionary<string, string> _imageList = new Dictionary<string, string>
        {
            ["0"] = "https://gspics.org/images/2024/06/30/0z0dgL.png",
            ["1"] = "https://i.ibb.co/pzTNbwt/J5WoK4d.png",
            ["2"] = "https://i.ibb.co/mqrck03/ZR7bbZm.png",
            ["3"] = "https://i.ibb.co/PFB7390/lariGpB.png",
            ["4"] = "https://i.ibb.co/qgLpYq3/jjEsmL5.png",
            ["5"] = "https://i.ibb.co/M1HKWvd/jo3WN9H.png",
            ["6"] = "https://i.ibb.co/mF2ShwK/odqNSDS.png",
            ["7"] = "https://i.ibb.co/JCxP15p/xOWHmh0.png",
            ["8"] = "https://i.ibb.co/Tbg6B6w/4-33-16.png",
            ["9"] = "https://i.ibb.co/2gb5MZX/1544.png",
            ["10"] = "https://i.ibb.co/vc8mrkN/z88veYB.png",
            ["11"] = "https://i.ibb.co/0Zc09M3/yENDy73.png",
            ["12"] = "https://i.ibb.co/SBfKg7y/khOBy3x.png",
            ["13"] = "https://i.ibb.co/0F6VtZR/e0y4aM5.png",
            ["14"] = "https://i.ibb.co/nsJ2x7m/d5YRaLB.png",
            ["15"] = "https://i.ibb.co/KK7n7XM/eV9QUa0.png",
            ["16"] = "https://i.ibb.co/wC5cx5R/e0FCvGc.png",
            ["17"] = "https://i.ibb.co/gT5xYtz/Tbv05TC.png",
            ["18"] = "https://i.ibb.co/DtBBX4X/4-4.png",
            ["19"] = "https://i.ibb.co/9qGpNYS/12.png",
            ["20"] = "https://i.ibb.co/BnJCCqY/33324.png",
        };
        public static StringBuilder StringBuilderInstance;

        private void OnLeaveNPC(BasePlayer npc, BasePlayer player)
        {
            if (!_config.humanNpcs.NPCs.ContainsKey(npc.UserIDString)) return;
            if (!_humanNpcPlayerOpen.ContainsKey(player.userID)) return;
            _humanNpcPlayerOpen.Remove(player.userID);
            if (_uiPlayerUseNow.Contains(player.userID))
                _uiPlayerUseNow.Remove(player.userID);
            CuiHelper.DestroyUi(player, "NOTIFICATION_POLOSA");
            CuiHelper.DestroyUi(player, "NOTIFICATION_IMG");
            CuiHelper.DestroyUi(player, "NOTIFICATION_TEXT");
            CuiHelper.DestroyUi(player, NotificationMain);
            CuiHelper.DestroyUi(player, MainLayer);
        }
        
                private string GetImage(string fileName, ulong skin = 0)
        {
            string imageId = (string)plugins.Find("ImageLibrary").CallHook("GetImage", fileName, skin);
            if (!string.IsNullOrEmpty(imageId))
                return imageId;
            return string.Empty;
        }
        private const string ConfirmationsPanelSelling = "CONFIRMATIONS_PANEL_SELLING";

        private readonly List<ulong> _pdpw = new List<ulong>();

        
                private Dictionary<ulong, ThemeType> _playerThemeSelect = new Dictionary<ulong, ThemeType>();
        private void ValidateConfig()
        {
            foreach (Configuration.CategoryShop category in _config.product)
            {
                foreach (Configuration.CategoryShop.Product product in category.product)
                {
                    foreach (KeyValuePair<string, int> buyLimits in product.BuyLimits)
                        if (!permission.PermissionExists(buyLimits.Key))
                            permission.RegisterPermission(buyLimits.Key, this);
                    foreach (KeyValuePair<string, int> buyLimitsWipe in product.BuyLimitsWipe)
                        if (!permission.PermissionExists(buyLimitsWipe.Key))
                            permission.RegisterPermission(buyLimitsWipe.Key, this);
                }
                if (!permission.PermissionExists(category.PermissionCategory))
                    permission.RegisterPermission(category.PermissionCategory, this);
            }
        }
		   		 		  						  	   		  		 			  		 			  			 		  		  
        private void CheckingProducts()
        {
            foreach (Configuration.CategoryShop category in _config.product)
            {
                foreach (Configuration.CategoryShop.Product product in category.product)
                {
                    if (product.ID == 0)
                        product.ID = Core.Random.Range(int.MinValue, int.MaxValue);
		   		 		  						  	   		  		 			  		 			  			 		  		  
                    if (product.Type == ItemType.Item || product.Type == ItemType.Blueprint || product.Type == ItemType.CustomItem)
                    {
                        product.Definition = ItemManager.FindItemDefinition(product.ShortName);
                        if (product.Definition == null)
                        {
                            product.ItemsError = true;
                            PrintError(GetLang("TPShop_SERVICE_CONFIG_1", args: product.ID));
                            continue;
                        }
                    }
                    if (product.Price <= 0)
                    {
                        product.ItemsError = true;
                        PrintError(GetLang("TPShop_SERVICE_CONFIG_2", args: product.ID));
                    }
                }
            }
        }
		   		 		  						  	   		  		 			  		 			  			 		  		  
        private void UIMAIN(BasePlayer player, ThemeType themetype)
        {
            Configuration.InterfaceSettings.ThemeCustomization theme = themetype == ThemeType.Dark ? _config.interfaceSettings.DarkTheme : _config.interfaceSettings.LightTheme;
            float discount = GetUserDiscount(player);
            CuiElementContainer container = new CuiElementContainer();

             container.Add(new CuiElement
            {
                Name = PanelProduct,
                Parent = ".Mains",
                Components ={
                    new CuiRawImageComponent{Png = GetImage($"{Name}_" +"0"),},
                    new CuiRectTransformComponent { AnchorMin = "-0.315 -0.27", AnchorMax = "1.3 1.275", OffsetMax = "0 0" }}
            });
            
            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = "UI_HandlerShop CLOSE_ALL_UI" },
                RectTransform = { AnchorMin = "0.801 0.805", AnchorMax = "0.815 0.83" }
            }, PanelProduct, "CLOSE_MENU_BTN");

            
            if (_config.discountStores.DiscountPerm.Count != 0)
            {
                
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "1 1 1 0" },
                    RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-283 -133", OffsetMax = "-231.91 -113.3" }
                }, PanelProduct, "DISCOUNT_INFO");


		   		 		  						  	   		  		 			  		 			  			 		  		  


            }

		   		 		  						  	   		  		 			  		 			  			 		  		  
            CuiHelper.DestroyUi(player, PanelProduct);
            CuiHelper.AddUi(player, container);
            UIPRODUCT(player, themetype);
            UICATEGORY(player, themetype);
        }

        private void SaveCooldowns()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Player_Cooldowns", _playerDataCooldowns);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (!_playerThemeSelect.ContainsKey(player.userID))
            {
                _playerThemeSelect.Add(player.userID, _config.interfaceSettings.ThemeTypeDefault);
            }
        }
        
        
        private void ThemeSwitch(BasePlayer player, ThemeType themetype)
        {
            string img = themetype == ThemeType.Dark ? GetImage($"{Name}_" +"12") : GetImage($"{Name}_" +"11");
            CuiElementContainer container = new CuiElementContainer
            {
                new CuiElement
                {
                    Name = "THEME_BTN",
                    Parent = PanelProduct,
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = img },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "16.5 12.8", OffsetMax = "38.5 25.8" }
                }
                },

                {
                    new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Button = { Command = $"UI_HandlerShop THEME_SWITCH {themetype}", Color = "0 0 0 0" },
                        Text = { Text = "", }
                    },
                    "THEME_BTN"
                }
            };
            CuiHelper.DestroyUi(player, "THEME_BTN");
            CuiHelper.AddUi(player, container);
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
        }

        private List<int> GetStacks(ItemDefinition items, int amount)
        {
            List<int> list = new List<int>();
            int maxStack = items.stackable;

            while (amount > maxStack)
            {
                amount -= maxStack;
                list.Add(maxStack);
            }

            list.Add(amount);

            return list;
        }

        private class Items
        {
            public string ShortName;
            public string ENdisplayName;
            public string RUdisplayName;
        }
                private void AddDisplayName()
        {
            webrequest.Enqueue($"http://api.skyplugins.ru/api/getitemlist", "", (code, response) =>
            {
                if (code == 200)
                {
                    _itemList = JsonConvert.DeserializeObject<List<Items>>(response);
                    foreach (Items items in _itemList)
                    {
                        _itemName.Add(items.ShortName, new ItemDisplayName { ru = items.RUdisplayName, en = items.ENdisplayName, description = ItemManager.FindItemDefinition(items.ShortName)?.displayDescription?.english });
                    }
                }
                else
                {
                    foreach (ItemDefinition item in ItemManager.itemList)
                    {
                        _itemName.Add(item?.shortname, new ItemDisplayName { ru = item.displayName?.english, en = item.displayName?.english, description = item.displayDescription?.english });
                    }
                }
            }, this);
        }
        private void LoadData()
        {
            try
            {
                _playerDataCooldowns = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerCooldown>>($"{Name}/Player_Cooldowns");
                _playerDataLimits = Interface.Oxide.DataFileSystem.ReadObject<PlayerLimits>($"{Name}/Player_Limits");
                _playerThemeSelect = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, ThemeType>>($"{Name}/Players");
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_playerDataCooldowns == null)
                _playerDataCooldowns = new Dictionary<ulong, PlayerCooldown>();
            if (_playerDataLimits == null)
                _playerDataLimits = new PlayerLimits();
            if (_playerThemeSelect == null)
                _playerThemeSelect = new Dictionary<ulong, ThemeType>();
        }
		   		 		  						  	   		  		 			  		 			  			 		  		  
		   		 		  						  	   		  		 			  		 			  			 		  		  
        
                private PlayerCooldown GetCooldown(ulong player)
        {
            PlayerCooldown cooldown;
            return _playerDataCooldowns.TryGetValue(player, out cooldown) ? cooldown : null;
        }
        private readonly Dictionary<BasePlayer, Timer> PlayerTimer = new Dictionary<BasePlayer, Timer>();
        private const string NotificationMain = "NOTIFICATION_MAIN";
        
        
        private void RefillItems()
        {
            _config.product.Clear();
            if (_apiLoadImage != null)
            {
                ServerMgr.Instance.StopCoroutine(_apiLoadImage);
                _apiLoadImage = null;
            }
            Dictionary<string, List<ItemDefinition>> itemDefinitionsList = new Dictionary<string, List<ItemDefinition>>();

            foreach (ItemDefinition itemDefinition in ItemManager.itemList)
            {
                string categoryName = itemDefinition.category.ToString();

                if (itemDefinitionsList.ContainsKey(categoryName))
                    itemDefinitionsList[categoryName].Add(itemDefinition);
                else
                    itemDefinitionsList.Add(categoryName, new List<ItemDefinition> { itemDefinition });
            }
            int itemId = 1;
            foreach (KeyValuePair<string, List<ItemDefinition>> item in itemDefinitionsList)
            {
                Configuration.CategoryShop category = new Configuration.CategoryShop
                {
                    CategoryName = item.Key,
                    PermissionCategory = string.Empty,
                    product = new List<Configuration.CategoryShop.Product>()
                };

                foreach (ItemDefinition itemDef in item.Value)
                {
                    if (_exclude.Contains(itemDef.shortname))
                        continue;
                    double itemPrice = ItemCostCalculator?.Call<double>("GetItemCost", itemDef) ?? 1;

                    category.product.Add(new Configuration.CategoryShop.Product
                    {

                        Type = ItemType.Item,
                        ID = itemId++,
                        ShortName = itemDef.shortname,
                        Descriptions = string.Empty,
                        Price = (float)itemPrice,
                        Amount = 1,
                        Name = string.Empty,
                        SkinID = 0,
                        Commands = new List<string>(),
                        Url = string.Empty,
                        KitName = string.Empty,
                        BuyCooldown = 0,
                        BuyLimits = new Dictionary<string, int>
                        {
                            ["TPShop.default"] = 10,
                        },
                        BuyLimitsWipe = new Dictionary<string, int>
                        {
                            ["TPShop.default"] = 100,
                        },
		   		 		  						  	   		  		 			  		 			  			 		  		  
                    });
                }
                _config.product.Add(category);
            }
            SaveConfig();
            PrintWarning(GetLang("TPShop_SERVICE_CMD_REFILL_SUCCESSFULLY"));
            NextTick(() => { Interface.Oxide.ReloadPlugin(Name); });
        }

        private class PlayerCooldown
        {
            [JsonProperty(PropertyName = "Player Limits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            private readonly Dictionary<int, CooldownData> _itemsCooldowns = new Dictionary<int, CooldownData>();

            private CooldownData GetCooldown(Configuration.CategoryShop.Product item)
            {
                CooldownData data;
                return _itemsCooldowns.TryGetValue(item.ID, out data) ? data : null;
            }

            public int GetCooldownTime(Configuration.CategoryShop.Product item, bool buy)
            {
                CooldownData data = GetCooldown(item);
                if (data == null)
                    return -1;

                return (int)((buy ? data.Buy : data.Sell).AddSeconds(
                    item.BuyCooldown) - DateTime.Now).TotalSeconds;
            }
            public PlayerCooldown SetCooldown(Configuration.CategoryShop.Product item, bool buy)
            {
                if (!_itemsCooldowns.ContainsKey(item.ID))
                    _itemsCooldowns.Add(item.ID, new CooldownData());

                if (buy)
                    _itemsCooldowns[item.ID].Buy = DateTime.Now;
                else
                    _itemsCooldowns[item.ID].Sell = DateTime.Now;

                return this;
            }
        }
        
                private void SubAndUnSubHumanNpcHook(bool sub = false)
        {
            if (!sub)
            {
                Unsubscribe(nameof(OnUseNPC));
                Unsubscribe(nameof(OnLeaveNPC));
                Unsubscribe(nameof(OnKillNPC));
            }
            else
            {
                Subscribe(nameof(OnUseNPC));
                Subscribe(nameof(OnLeaveNPC));
                Subscribe(nameof(OnKillNPC));
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                    throw new Exception();
                SaveConfig();
            }
            catch
            {
                for (int i = 0; i < 3; i++)
                    PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                LoadDefaultConfig();
            }
            ValidateConfig();
            CheckOnDuplicates();
            SaveConfig();
        }

        private enum ItemType
        {
            Item,
            Blueprint,
            CustomItem,
            Command,
            Kit
        }
        private void SaveLimits()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Player_Limits", _playerDataLimits);
        }
        
        private static TPShop _instance;
        
        [ConsoleCommand("UI_HandlerShop")]
        private void CmdConsoleHandler(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null || !args.HasArgs(1)) return;
            switch (args.Args[0])
            {
                case "PAGE_CATEGORY":
                {
                    int page = int.Parse(args.Args[1]);
                    ThemeType theme = (ThemeType)Enum.Parse(typeof(ThemeType), args.Args[2]);
                    int category = int.Parse(args.Args[3]);
                    UICATEGORY(player, theme, page, category);
                    UIPRODUCT(player, theme, 0, category);
                    break;
                }
                case "CHANGE_CATEGORY":
                {
                    int page = int.Parse(args.Args[1]);
                    int category = int.Parse(args.Args[2]);
                    ThemeType theme = (ThemeType)Enum.Parse(typeof(ThemeType), args.Args[3]);
                    CuiHelper.DestroyUi(player, ConfirmationsPanel);
                    CuiHelper.DestroyUi(player, ConfirmationsPanelSelling);
                    UICATEGORY(player, theme, page, category);
                    UIPRODUCT(player, theme, 0, category);
                    break;
                }
                case "PAGE_PRODUCT":
                {
                    int page = int.Parse(args.Args[1]);
                    int category = int.Parse(args.Args[2]);
                    ThemeType theme = (ThemeType)Enum.Parse(typeof(ThemeType), args.Args[3]);
                    CuiHelper.DestroyUi(player, ConfirmationsPanel);
                    CuiHelper.DestroyUi(player, ConfirmationsPanelSelling);
                    UIPRODUCT(player, theme, page, category);
                    break;
                }
                case "PRODUCT_BUY_CONFIRMATION":
                {
                    int indexItem = int.Parse(args.Args[1]);
                    int indexCategory = int.Parse(args.Args[2]);
                    int amount = int.Parse(args.Args[3]);
                    ThemeType theme = (ThemeType)Enum.Parse(typeof(ThemeType), args.Args[4]);
                    CuiHelper.DestroyUi(player, ConfirmationsPanelSelling);
                    CONFIRMATIONSUI(player, indexItem, indexCategory, amount, theme);
                    break;
                }
                case "PRODUCT_BUY":
                {
                    int indexItem = int.Parse(args.Args[1]);
                    int indexCategory = int.Parse(args.Args[2]);
                    int amount = int.Parse(args.Args[3]);
                    ThemeType theme = (ThemeType)Enum.Parse(typeof(ThemeType), args.Args[4]);
                    Purchasing(player, indexItem, indexCategory, amount, theme);
                    CuiHelper.DestroyUi(player, ConfirmationsPanel);
                    break;
                }
                case "THEME_SWITCH":
                {
                    ThemeType theme = (ThemeType)Enum.Parse(typeof(ThemeType), args.Args[1]);
                    UIMAIN(player, theme == ThemeType.Dark ? ThemeType.Light : ThemeType.Dark);
                    _playerThemeSelect[player.userID] = theme == ThemeType.Dark ? ThemeType.Light : ThemeType.Dark;
                    break;
                }
                case "CLOSE_ALL_UI":
                {
                    if (_config.humanNpcs.UseHumanNpcs && _humanNpcPlayerOpen.ContainsKey(player.userID))
                        _humanNpcPlayerOpen.Remove(player.userID);
                    if (_uiPlayerUseNow.Contains(player.userID))
                        _uiPlayerUseNow.Remove(player.userID);
                    CuiHelper.DestroyUi(player, "NOTIFICATION_POLOSA");
                    CuiHelper.DestroyUi(player, "NOTIFICATION_IMG");
                    CuiHelper.DestroyUi(player, "NOTIFICATION_TEXT");
                    CuiHelper.DestroyUi(player, NotificationMain);
                    CuiHelper.DestroyUi(player, MainLayer);
                    CuiHelper.DestroyUi(player, "Menu_UI");
                    break;
                }
                case "PRODUCT_234":
                {
                    if (_config.humanNpcs.UseHumanNpcs && _humanNpcPlayerOpen.ContainsKey(player.userID))
                        _humanNpcPlayerOpen.Remove(player.userID);
                    if (_uiPlayerUseNow.Contains(player.userID))
                        _uiPlayerUseNow.Remove(player.userID);

                    int indexItem = int.Parse(args.Args[1]);
                    int indexCategory = int.Parse(args.Args[2]);

                    UIPRODUCTNEW(player, indexItem, indexCategory); 

                    break;
                }
                case "AMOUNT_CHANGE_PLUS":
                {
                    int index = int.Parse(args.Args[1]);
                    int indexCategory = int.Parse(args.Args[2]);
                    int amount = int.Parse(args.Args[3]);
                    int type = int.Parse(args.Args[4]);
                    if (type == 0)
                    {
                        CuiHelper.DestroyUi(player, ConfirmationsPanel);
                        UPDATEAMOUNTUI(player, index, indexCategory, amount + 1);
                    }
                    break;
                }
                case "AMOUNT_CHANGE_MINUS":
                {
                    int index = int.Parse(args.Args[1]);
                    int indexCategory = int.Parse(args.Args[2]);
                    int amount = int.Parse(args.Args[3]);
                    int type = int.Parse(args.Args[4]);
                    if (type == 0)
                    {
                        CuiHelper.DestroyUi(player, ConfirmationsPanel);
                        UPDATEAMOUNTUI(player, index, indexCategory, amount - 1);
                    }
                    break;
                }
            }
        }
        private CuiElementContainer UIPRODUCTGETIMAGEITEM(Configuration.CategoryShop.Product item)
        {
            var container = new CuiElementContainer();  
            #region IMAGE
                if (!string.IsNullOrWhiteSpace(item.Url) || item.Type == ItemType.Kit || item.Type == ItemType.Command)
                {
                    container.Add(new CuiElement
                    {
                        Name = $"PRODUCT_IMAGE_{item.ID}",
                        Parent = $"PRODUCT_BACKGROUND_{item.ID}",
                        Components = {
                            new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage(string.IsNullOrWhiteSpace(item.Url) ? "NONE": item.Url) },
                            new CuiRectTransformComponent { AnchorMin = "0.2 0.2", AnchorMax = "0.8 0.8"  }
                        }
                    });
                }
                else if (item.Type == ItemType.CustomItem)
                {
                    container.Add(new CuiElement
                    {
                        Name = $"PRODUCT_IMAGE_{item.ID}",
                        Parent = $"PRODUCT_BACKGROUND_{item.ID}",
                        Components = {
                            new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage(item.ShortName + 60, item.SkinID) },
                            new CuiRectTransformComponent { AnchorMin = "0.2 0.2", AnchorMax = "0.8 0.8"  }
                        }
                    });
                }
                else if (item.Type == ItemType.Item)
                {
                    container.Add(new CuiElement
                    {
                        Name = $"PRODUCT_IMAGE_{item.ID}",
                        Parent = $"PRODUCT_BACKGROUND_{item.ID}",
                        Components = {
                            new CuiImageComponent { Color = "1 1 1 1", ItemId = ItemManager.FindItemDefinition(item.ShortName).itemid },
                            new CuiRectTransformComponent {AnchorMin = "0.2 0.2", AnchorMax = "0.8 0.8"  }
                        }
                    });
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Name = $"PRODUCT_IMAGE_{item.ID}",
                        Parent = $"PRODUCT_BACKGROUND_{item.ID}",
                        Components = {
                            new CuiImageComponent { Color = "1 1 1 1", ItemId = ItemManager.FindItemDefinition("blueprintbase").itemid },
                            new CuiRectTransformComponent { AnchorMin = "0.2 0.2", AnchorMax = "0.8 0.8"  }
                        }
                    });
                    
                    container.Add(new CuiElement
                    {
                        Name = $"PRODUCT_IMAGE_{item.ID}",
                        Parent = $"PRODUCT_BACKGROUND_{item.ID}",
                        Components = {
                            new CuiImageComponent { Color = "1 1 1 1", ItemId = ItemManager.FindItemDefinition(item.ShortName).itemid },
                            new CuiRectTransformComponent { AnchorMin = "0.2 0.2", AnchorMax = "0.8 0.8"  }
                        }
                    });
                }
                #endregion
            return container;
        }
        private void UIPRODUCTNEW(BasePlayer player, int productid, int categoryIndex)
        {
            Configuration.CategoryShop.Product item = GetCategories(player)[categoryIndex].product.Find(x => x.ID == productid);

            CuiHelper.DestroyUi(player, $"PRODUCT_IMAGE_{item.ID}");
            CuiHelper.DestroyUi(player, $"PRODUCT_BUY_BTN_IMAGE_{item.ID}");
            CuiHelper.DestroyUi(player, $"PRODUCT_BACKGROUND_BUTTON_{item.ID}");
            CuiHelper.DestroyUi(player, $"PRODUCT_BACKGROUND_IMAGE_{item.ID}");
            CuiHelper.DestroyUi(player, $"PRODUCT_PRICE_{item.ID}");
            float discount = GetUserDiscount(player);
            var container = new CuiElementContainer();  
            container.Add(new CuiElement
            {
                Name = $"PRODUCT_BACKGROUND_IMAGE_{item.ID}",
                Parent = $"PRODUCT_BACKGROUND_{item.ID}",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = GetImage($"{Name}_" +"19"),
                    },

                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                    }
                }
            });
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -55", OffsetMax = "5 34" },
                Text = { Text = $"{GetDiscountedPrice(item.Price, discount)}{_config.economicsCustomization.PrefixBalance}", Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.UpperLeft, Color = "1 1 1 0.7" },
            }, $"PRODUCT_BACKGROUND_{item.ID}", $"PRODUCT_PRICE_{item.ID}");

            var imageItemContainer = UIPRODUCTGETIMAGEITEM(item);
            foreach (var element in imageItemContainer)
            {
                container.Add(element);
            }

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "8 5", OffsetMax = "-8 -50" },
                Button = { Command = $"UI_HandlerShop PRODUCT_BUY_CONFIRMATION {item.ID} {categoryIndex} 1 1", Color = "1 1 1 0" },
            }, $"PRODUCT_BACKGROUND_{item.ID}", $"PRODUCT_BUY_BTN_{item.ID}");
            container.Add(new CuiElement
            {
                Name = $"PRODUCT_BUY_BTN_IMAGE_{item.ID}",
                Parent = $"PRODUCT_BUY_BTN_{item.ID}",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = GetImage($"{Name}_" +"20"),
                    },

                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = $"PRODUCT_BUY_BTN_IMAGE_{item.ID}",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetLang("TPShop_UI_BTN_BUY", player.UserIDString),
                        FontSize = 11,
                        Align = TextAnchor.MiddleCenter,
                    },

                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                    },
                }
            });
            //ПЕРЕКЛЮЧАТЕЛЬ + -
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "1 1 1 0" },
                    RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-50 45", OffsetMax = "0 70" }
                }, $"PRODUCT_BACKGROUND_{item.ID}", $"PRODUCT_AMOUNT_CHANGE_{item.ID}");
                //КАРТИНКА ПЕРЕКЛЮЧАТЕЛЯ
                container.Add(new CuiElement
                {
                    Name = ProductAmountChangeBg + $"_{item.ID}",
                    Parent = $"PRODUCT_AMOUNT_CHANGE_{item.ID}",
                    Components = {
                    new CuiRawImageComponent { Color ="1 1 1 1", Png =GetImage($"{Name}_" +"9") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-20 -4", OffsetMax = "20 10" }
                }
                });
                //КНОПКА ПЕРЕКЛЮЧАТЕЛЯ -
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "0 -8", OffsetMax = "14.936 8" },
                    Button = { Command = $"UI_HandlerShop AMOUNT_CHANGE_MINUS {productid} {categoryIndex} {1} 0", Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, ProductAmountChangeBg + $"_{item.ID}", $"PRODUCT_AMOUNT_MINUS_{item.ID}");
                
                //КНОПКА ПЕРЕКЛЮЧАТЕЛЯ +
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-15.628 -8", OffsetMax = "0 8" },
                    Button = { Command = $"UI_HandlerShop AMOUNT_CHANGE_PLUS {productid} {categoryIndex} {1} 0", Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, ProductAmountChangeBg + $"_{item.ID}", $"PRODUCT_AMOUNT_PLUS_{item.ID}");

                //ЦИФЕРКА ПЕРЕКЛЮЧАТЕЛЯ
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-10.064 -8", OffsetMax = "9.372 8" },
                    Text = { Text = "1", Font = "robotocondensed-regular.ttf", FontSize = 9, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.8" },
                }, ProductAmountChangeBg + $"_{item.ID}", $"PRODUCT_LOT_COUNT_{item.ID}");

            CuiHelper.AddUi(player, container);
        }
        private void UIPRODUCT(BasePlayer player, ThemeType themetype, int page = 0, int categoryIndex = 0)
        {
            float discount = GetUserDiscount(player);
            List<Configuration.CategoryShop.Product> products = GetCategories(player)[categoryIndex].product;
            CuiElementContainer container = new CuiElementContainer
            {
                {
                    new CuiPanel
                    {
                        CursorEnabled = false,
                        Image = { Color = "1 1 1 0" },
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-189 -173", OffsetMax = "303 162" },
                    },
                    PanelProduct,
                    ProductsLayer
                }
            };

            int i = 0, u = 0;
            foreach (Configuration.CategoryShop.Product item in products.Where(x => !x.ItemsError).Page(page, 24))
            {

                container.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "0 0 0 0.3",

                    },

                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1", /*OffsetMin = $"{12 + (i*88.25)} {-88.25 + (u*(-88.25))}", OffsetMax = $"{88.25 + (i*88.25)} {-12 + (u*(-88.25))}"*/
                        OffsetMin = $"{11 + (i*80)} {-80.5 + (u*-80.5)}", 
                        OffsetMax = $"{80 + (i*80)} {-11 + (u*-80.5)}"
                    },
                }, ProductsLayer, $"PRODUCT_BACKGROUND_{item.ID}");


                container.Add(new CuiElement
                {
                    Name = $"PRODUCT_BACKGROUND_IMAGE_{item.ID}",
                    Parent = $"PRODUCT_BACKGROUND_{item.ID}",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = GetImage($"{Name}_" +"8"),
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                        }
                    }
                });
                
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31 -55", OffsetMax = "5 34" },
                    Text = { Text = $"{GetDiscountedPrice(item.Price, discount)}{_config.economicsCustomization.PrefixBalance}", Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.UpperLeft, Color = "1 1 1 0.7" },
                }, $"PRODUCT_BACKGROUND_{item.ID}", $"PRODUCT_PRICE_{item.ID}");

                var imageItemContainer = UIPRODUCTGETIMAGEITEM(item);
                foreach (var element in imageItemContainer)
                {
                    container.Add(element);
                }

                container.Add(new CuiButton
                {
                    Button ={
                        Command = $"UI_HandlerShop PRODUCT_234 {item.ID} {categoryIndex}",
                        Color = "1 1 1 0"
                    },
                    RectTransform ={
                        AnchorMin = "0 0", AnchorMax = "1 1"
                    }
                }, $"PRODUCT_BACKGROUND_{item.ID}", $"PRODUCT_BACKGROUND_BUTTON_{item.ID}");

                i++;
                if (i >= 6)
                {
                    u++;
                    i = 0;
                }
            }

            //ПЕРЕКЛЮЧЕНИЕ МЕЖДУ СТР
            if (products.Count > 24)
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "1 1 1 0" },
                    RectTransform = { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "580 -205", OffsetMax = "708 -180" }
                }, PanelProduct, PageProducts);

                if (page > 0)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "0.46 1" },
                        Button = { Command = $"UI_HandlerShop PAGE_PRODUCT {page - 1} {categoryIndex} {themetype}", Color = "1 1 1 0" },
                        Text = { Text = "" }
                    }, PageProducts, "PAGE_UP_BTN");
                }
                if (page + 1 < (int)Math.Ceiling((double)products.Count / 24))
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.54 0", AnchorMax = "1 1" },
                        Button = { Command = $"UI_HandlerShop PAGE_PRODUCT {page + 1} {categoryIndex} {themetype}", Color = "1 1 1 0" },
                        Text = { Text = "" }
                    }, PageProducts, "PAGE_DOWN_BTN");
                }
            }
            
            CuiHelper.DestroyUi(player, PageProducts);
            CuiHelper.DestroyUi(player, ProductsLayer);
            CuiHelper.AddUi(player, container);
        }

        private void OnUseNPC(BasePlayer npc, BasePlayer player)
        {
            if (!_config.humanNpcs.NPCs.ContainsKey(npc.UserIDString) ||
                _humanNpcPlayerOpen.ContainsKey(player.userID)) return;
            _humanNpcPlayerOpen.Add(player.userID, npc.UserIDString);
            TPShopUI(player);
        }

        [ConsoleCommand("TPShop.yes")]
        private void CmdShopItemsRefillYes(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel <= 0) return;
            ulong userId = arg.Connection == null || arg.IsRcon ? 0U : arg.Connection.userid;
            if (!_pdpw.Contains(userId)) return;
            PrintWarning(GetLang("TPShop_SERVICE_CMD_REFILL_YES"));
            RefillItems();

            _pdpw.Remove(userId);
        }
        private static int GetItemAmount(Item[] itemsInput, string shortname, ulong skin, bool blueprint = false)
        {
            List<Item> items = new List<Item>();
            foreach (Item item in itemsInput.Where(x => x != null && (blueprint ? x.blueprintTargetDef?.shortname == shortname : x.info?.shortname == shortname) && (skin == 0 || x.skin == skin)))
            {
                if (item.isBroken || item.hasCondition && item.condition < item.info.condition.max) continue;
                items.Add(item);
            }

            return items.Sum(item => item.amount);
        }
        private readonly HashSet<string> _exclude = new HashSet<string>
        {
            "vehicle.chassis","vehicle.module", "tool.camera", "fishing.tackle", "blood", "wolfmeat.spoiled", "apple.spoiled", "humanmeat.spoiled","chicken.spoiled", "meat.pork.burned", "chicken.burned", "deermeat.burned", "wolfmeat.burned", "horsemeat.burned", "humanmeat.burned", "bearmeat.burned", "ammo.rocket.smoke", "blueprintbase", "captainslog", "minihelicopter.repair", "note", "photo", "scraptransportheli.repair", "spiderweb", "spookyspeaker", "habrepair", "door.key", "car.key", "bleach", "ducttape", "glue", "sticks", "skullspikes", "skull.trophy", "map", "battery.small", "coal", "can.beans.empty", "can.tuna.empty", "skull.human", "paper", "researchpaper", "water", "hazmatsuit_scientist_arctic", "attire.banditguard", "scientistsuit_heavy", "frankensteins.monster.01.head", "frankensteins.monster.01.legs", "frankensteins.monster.01.torso", "frankensteins.monster.02.legs", "frankensteins.monster.02.head", "frankensteins.monster.02.torso", "snowmobile", "snowmobiletomaha", "rowboat", "mlrs", "workcart", "submarinesolo", "submarineduo", "rhib", "vehicle.chassis.2mod", "vehicle.chassis.3mod", "vehicle.chassis.4mod", "electric.cabletunnel", "water.salt", "geiger.counter"
        };

        private class PlayerLimitData
        {
            [JsonProperty(PropertyName = "Player Limits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            private readonly Dictionary<int, ItemLimitData> _itemsLimits = new Dictionary<int, ItemLimitData>();

            public void AddItem(Configuration.CategoryShop.Product item, int lots, bool purchase)
            {
                if (!_itemsLimits.ContainsKey(item.ID))
                    _itemsLimits.Add(item.ID, new ItemLimitData());

                if (purchase)
                    _itemsLimits[item.ID].Buy += lots;
                else
                    _itemsLimits[item.ID].Sell += lots;
            }

            public int GetLimit(Configuration.CategoryShop.Product item, bool purchase)
            {
                ItemLimitData data;
                if (_itemsLimits.TryGetValue(item.ID, out data))
                    return purchase ? data.Buy : data.Sell;

                return 0;
            }

            private class ItemLimitData
            {
                public int Sell;

                public int Buy;
            }
        }
		   		 		  						  	   		  		 			  		 			  			 		  		  
        private class PlayerLimits
        {
            [JsonProperty(PropertyName = "List of players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<ulong, PlayerLimitData> Players = new Dictionary<ulong, PlayerLimitData>();

            public static PlayerLimitData GetOrAddPlayer(ulong playerId)
            {
                if (!_instance._playerDataLimits.Players.ContainsKey(playerId))
                    _instance._playerDataLimits.Players.Add(playerId, new PlayerLimitData());

                return _instance._playerDataLimits.Players[playerId];
            }
        }
		   		 		  						  	   		  		 			  		 			  			 		  		  
        private void UICATEGORY(BasePlayer player, ThemeType themetype, int page = 0, int categoryIndex = 0)
        {
            Configuration.InterfaceSettings.ThemeCustomization theme = themetype == ThemeType.Dark ? _config.interfaceSettings.DarkTheme : _config.interfaceSettings.LightTheme;
            CuiElementContainer container = new CuiElementContainer
            {
                {
                    new CuiPanel
                    {
                        CursorEnabled = false,
                        Image = { Color = "1 1 1 0" },
                        RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-304 -510", OffsetMax = "-210 -175" }
                    },
                    PanelProduct,
                    CategoryLayer
                }
            };

            int i = 0, catIndex = page * 24;
            List<Configuration.CategoryShop> category = GetCategories(player);
            foreach (Configuration.CategoryShop cat in category.Page(page, 24))
            {
                bool thisCat = catIndex == categoryIndex;
                container.Add(new CuiElement
                {
                    Name = $"CATEGORY_{i}",
                    Parent =".Mains",
                    Components = {
                        new CuiRawImageComponent { Png = GetImage($"{Name}_" +"18") },
                        new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = $"0 {146 + (i*-28)}", OffsetMax = $"105 {171 + (i*-28)}" }
                    }
                });

                container.Add(new CuiButton
                {
                    Button = { Command = thisCat ? "" : $"UI_HandlerShop CHANGE_CATEGORY {page} {catIndex} {themetype}", Color = "0 0 0 0" },
                    Text = { Text = cat.CategoryName, Font = thisCat ? "robotocondensed-bold.ttf" : "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = thisCat ? theme.colortext3 : theme.colortext4 },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-47.323 -9.649", OffsetMax = "47.323 9.649" }
                }, $"CATEGORY_{i}");
                catIndex++;
                i++;
            }
            
            CuiHelper.DestroyUi(player, CategoryLayer);
            CuiHelper.AddUi(player, container);
        }

        private List<Items> _itemList = new List<Items>();
        private void DeleteUserInShow(BasePlayer player)
        {
            if (_uiPlayerUseNow.Contains(player.userID))
                _uiPlayerUseNow.Remove(player.userID);
        }
        private void TPShopUI(BasePlayer player)
        {
            if(!string.IsNullOrWhiteSpace(_config.mainSettings.PermissionUseShop) && !permission.UserHasPermission(player.UserIDString, _config.mainSettings.PermissionUseShop))
            {
                player.ChatMessage(GetLang("TPShop_SERVICE_5", player.UserIDString));
                return;
            }
            if(GetCategories(player).Count == 0)
            {
                player.ChatMessage(GetLang("TPShop_SERVICE_6", player.UserIDString));
                return;
            }
            if (!_uiPlayerUseNow.Contains(player.userID))
                _uiPlayerUseNow.Add(player.userID);
            else return;
            if (!_playerThemeSelect.ContainsKey(player.userID))
                _playerThemeSelect.Add(player.userID, _config.interfaceSettings.ThemeTypeDefault);
            ThemeType themeType = _playerThemeSelect[player.userID];

            CuiElementContainer container = new CuiElementContainer
            {
                {
                    new CuiPanel
                    {
                        CursorEnabled = true,
                        Image = { Color = "1 1 1 0" },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1"}
                    },
                    ".Mains",
                    MainLayer
                }
            };
            CuiHelper.DestroyUi(player, MainLayer);
            CuiHelper.AddUi(player, container);
            UIMAIN(player, themeType);
        }
        private float GetUserDiscount(BasePlayer player)
        {
            float discounts = 0f;
            foreach (KeyValuePair<string, float> discount in _config.discountStores.DiscountPerm)
            {
                if (!permission.UserHasPermission(player.UserIDString, discount.Key)) continue;
                if (discounts < discount.Value)
                    discounts = discount.Value;
            }
            return discounts;
        }
        
                private static void TakeItem(Item[] playerItems, string shortname, int amount, ulong skinid = 0, bool blueprint = false)
        {
            List<Item> acceptedItems = Pool.GetList<Item>();
            int itemAmount = 0;

            foreach (Item item in playerItems.Where(x => x != null && (blueprint ? x.blueprintTargetDef?.shortname == shortname : x.info?.shortname == shortname) && (skinid == 0 || x.skin == skinid)))
            {
                if (item.isBroken || item.hasCondition && item.condition < item.info.condition.max) continue;
                acceptedItems.Add(item);
                itemAmount += item.amount;
            }

            foreach (Item use in acceptedItems)
            {
                if (use.amount == amount)
                {
                    use.RemoveFromContainer();
                    use.Remove();
                    amount = 0;
                    break;
                }
                if (use.amount > amount)
                {
                    use.MarkDirty();
                    use.amount -= amount;
                    amount = 0;
                    break;
                }
                if (use.amount < amount)
                {
                    amount -= use.amount;
                    use.RemoveFromContainer();
                    use.Remove();
                }
            }
            Pool.FreeList(ref acceptedItems);
        }

        private void DeleteNotification(BasePlayer player)
        {
            Timer timers = timer.Once(3.5f, () =>
            {
                CuiHelper.DestroyUi(player, "NOTIFICATION_POLOSA");
                CuiHelper.DestroyUi(player, "NOTIFICATION_IMG");
                CuiHelper.DestroyUi(player, "NOTIFICATION_TEXT");
                CuiHelper.DestroyUi(player, NotificationMain);
            });

            if (PlayerTimer.ContainsKey(player))
            {
                if (PlayerTimer[player] != null && !PlayerTimer[player].Destroyed) PlayerTimer[player].Destroy();
                PlayerTimer[player] = timers;
            }
            else PlayerTimer.Add(player, timers);
        }
		   		 		  						  	   		  		 			  		 			  			 		  		  
        
        
        private PlayerLimits _playerDataLimits;
        private static Coroutine _apiLoadImage;

        private void SetCooldown(BasePlayer player, Configuration.CategoryShop.Product item, bool purchase)
        {
            if (item.BuyCooldown <= 0)
                return;

            if (_playerDataCooldowns.ContainsKey(player.userID))
                _playerDataCooldowns[player.userID].SetCooldown(item, purchase);
            else
                _playerDataCooldowns.Add(player.userID, new PlayerCooldown().SetCooldown(item, purchase));
        }

        
        private float GetBalance(BasePlayer player)
{
    if (player == null)
    {
        Puts("Player is null in GetBalance method.");
        return 0f;
    }

    switch (_config.economicsCustomization.TypeEconomic)
    {
        case EconomicsType.Economics:
            if (Economics == null)
            {
                Puts("Economics plugin is not loaded.");
                return 0f;
            }
            var economicsBalance = Economics.Call<double>("Balance", player.userID);
            if (economicsBalance == null)
            {
                Puts($"Failed to get balance from Economics plugin for player {player.displayName} ({player.userID}).");
                return 0f;
            }
            return (float)economicsBalance;
            
        case EconomicsType.ServerRewards:
            if (ServerRewards == null)
            {
                Puts("ServerRewards plugin is not loaded.");
                return 0f;
            }
            var serverRewardsBalance = ServerRewards.Call("CheckPoints", player.userID);
            if (serverRewardsBalance == null)
            {
                Puts($"Failed to get balance from ServerRewards plugin for player {player.displayName} ({player.userID}).");
                return 0f;
            }
            return (int)serverRewardsBalance;
            
        case EconomicsType.IQEconomic:
            if (IQEconomic == null)
            {
                Puts("IQEconomic plugin is not loaded.");
                return 0f;
            }
            var iqEconomicBalance = IQEconomic.Call("API_GET_BALANCE", player.userID);
            if (iqEconomicBalance == null)
            {
                Puts($"Failed to get balance from IQEconomic plugin for player {player.displayName} ({player.userID}).");
                return 0f;
            }
            return (int)iqEconomicBalance;
            
        /*case EconomicsType.Item:
            return GetItemAmount(player.inventory.GetAllItems(), _config.economicsCustomization.EconomicShortname, _config.economicsCustomization.EconomicSkinId);
          */  
        case EconomicsType.TPEconomic:
            if (TPEconomic == null)
            {
                Puts("TPEconomic plugin is not loaded.");
                return 0f;
            }
            var tpEconomicBalance = TPEconomic.Call("API_GET_BALANCE", player.userID);
            if (tpEconomicBalance == null)
            {
                Puts($"Failed to get balance from TPEconomic plugin for player {player.displayName} ({player.userID}).");
                return 0f;
            }
            return (float)tpEconomicBalance;
            
        default:
            Puts("Unknown economic type.");
            return 0f;
    }
}


        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        private enum ThemeType
        {
            Light,
            Dark
        }

        private void OnKillNPC(BasePlayer npc, BasePlayer player)
        {
            if (!_config.humanNpcs.NPCs.ContainsKey(npc.UserIDString)) return;
            foreach (KeyValuePair<ulong, string> item in _humanNpcPlayerOpen.Where(x => x.Value == npc.UserIDString))
            {
                if (!_humanNpcPlayerOpen.ContainsKey(item.Key)) continue;
                _humanNpcPlayerOpen.Remove(item.Key);
                if (_uiPlayerUseNow.Contains(player.userID))
                    _uiPlayerUseNow.Remove(player.userID);
                CuiHelper.DestroyUi(player, "NOTIFICATION_POLOSA");
                CuiHelper.DestroyUi(player, "NOTIFICATION_IMG");
                CuiHelper.DestroyUi(player, "NOTIFICATION_TEXT");
                CuiHelper.DestroyUi(player, NotificationMain);
                CuiHelper.DestroyUi(player, MainLayer);
            }
        }

        private Dictionary<ulong, PlayerCooldown> _playerDataCooldowns = new Dictionary<ulong, PlayerCooldown>();
        private void Init()
        {
            _instance = this;
            SubAndUnSubHumanNpcHook();
            LoadData();

            foreach (KeyValuePair<string, float> discount in _config.discountStores.DiscountPerm)
                if (!permission.PermissionExists(discount.Key))
                    permission.RegisterPermission(discount.Key, this);
          /*  foreach (string command in _config.mainSettings.Commands)
                cmd.AddChatCommand(command, this, nameof(TPShopUI));*/
        }
        private IEnumerator DownloadImages()
        {
            foreach (KeyValuePair<string, string> item in _imageList)
                if (!(bool)ImageLibrary?.Call("HasImage", item.Key))
                    ImageLibrary.Call("AddImage", item.Value, $"{Name}_" + item.Key);
            
            foreach (Configuration.CategoryShop img in _config.product)
            {
                foreach (Configuration.CategoryShop.Product typeimg in img.product)
                {
                    if (typeimg.ItemsError)
                        continue;
                    if (!string.IsNullOrWhiteSpace(typeimg.Url))
                    {
                        if (!(bool)ImageLibrary?.Call("HasImage", typeimg.Url))
                            ImageLibrary.Call("AddImage", typeimg.Url, typeimg.Url);
                    }
                    else if (typeimg.Type == ItemType.CustomItem)
                    {
                        if (!(bool)ImageLibrary?.Call("HasImage", typeimg.ShortName + 60, typeimg.SkinID))
                            ImageLibrary.Call("AddImage", $"https://api.skyplugins.ru/api/getskin/v1/b2834852/{typeimg.SkinID}/120", typeimg.ShortName + 60, typeimg.SkinID);
                    }
                    yield return CoroutineEx.waitForSeconds(0.01f);
                }
            }
            _apiLoadImage = null;
            yield return 0;
        }
        private const bool Ru = true;
        
                protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TPShop_UI_TITLE"] = "Каталог товаров",
                ["TPShop_UI_BTN_BUY"] = "Купить",
                ["TPShop_UI_BTN_SALLE"] = "Продать",
                ["TPShop_UI_PRODUCT_INFO"] = "Цена покупки: {0}\nКоличество: {1}\nВаш лимит: {2}",
                ["TPShop_UI_PRODUCT_INFO_EXIT"] = "ЗАКРЫТЬ",
                ["TPShop_UI_PRODUCT_SELL_INFO"] = "Цена продажи: {0}\nКоличество : {1}\nВаш лимит: {2}",
                ["TPShop_UI_NOTIFICATION_IS_DUEL"] = "Вы не можете приобрести предмет во время дуэли!",
                ["TPShop_UI_NOTIFICATION_IS_RAID"] = "Вы не можете приобрести предмет во время рейд/комбат блока!",
                ["TPShop_UI_NOTIFICATION_NOT_ENOUGH_SPACE"] = "Недостаточно места в инвентаре",
                ["TPShop_UI_NOTIFICATION_INSUFFICIENT_FUNDS"] = "У вас недостаточно средств для данной покупки",
                ["TPShop_UI_NOTIFICATION_BUY_RECHARGE"] = "Вы не можете приобрести '{0}'. Вам нужно подождать еще {1}",
                ["TPShop_UI_NOTIFICATION_BUY_LIMIT"] = "Вы больше не можете приобрести '{0}'. Вы превысили лимит за WIPE",
                ["TPShop_UI_NOTIFICATION_BUY_LIMIT_1"] = "Вы не можете приобрести '{0}' в таком количестве. Вы можете купить еще {1} лот(ов)",
                ["TPShop_UI_NOTIFICATION_SUCCESSFUL_PURCHASE"] = "Вы успешно приобрели {0}",
                ["TPShop_UI_NOTIFICATION_SELL_IS_DUEL"] = "Вы не можете продать предмет во время дуэли!",
                ["TPShop_UI_NOTIFICATION_SELL_IS_RAID"] = "Вы не можете продать предмет во время рейд/комбат блока!",
                ["TPShop_UI_NOTIFICATION_NOT_ENOUGH_ITEM"] = "Недостаточно предмета для продажи",
                ["TPShop_UI_NOTIFICATION_SELL_RECHARGE"] = "Вы не можете продать '{0}'. Вам нужно подождать еще {1}",
                ["TPShop_UI_NOTIFICATION_SELL_LIMIT"] = "Вы больше не можете продать '{0}'. Вы превысили лимит продаж за WIPE",
                ["TPShop_UI_NOTIFICATION_SELL_LIMIT_1"] = "Вы не можете продать '{0}' в таком количестве. Вы можете продать еще {1} лот(ов)",
                ["TPShop_UI_NOTIFICATION_SUCCESSFUL_SALE"] = "Вы успешно продали {0}",
                ["TPShop_SERVICE_EXIST_ECONOMICS"] = "У вас отсутствует выбранная экономика. Пожалуйста! Проверьте настройки экономики в конфигурации",
                ["TPShop_SERVICE_CONFIG_1"] = "В товаре с ID '{0}' присутствует ошибка. Товар будет скрыт, проверьте правильно ли вы указали ShortName!",
                ["TPShop_SERVICE_CONFIG_2"] = "В товаре с ID '{0}' присутствует ошибка. Товар будет скрыт, у товара отсутствует или неверная цена!",
                ["TPShop_SERVICE_CMD_REFILL"] = "Выполнения данной команды повлечет за собой изменения конфигурации товаров и категорий. Убедитесь что вы сохранили конфигурацию перед данной операцией. Вы уверены, что хотите продолжить? (TPShop.yes или TPShop.no)",
                ["TPShop_SERVICE_CMD_REFILL_YES"] = "Происходит перезаполнение категорий и товаров. Пожалуйста, ожидайте...",
                ["TPShop_SERVICE_CMD_REFILL_NO"] = "Действие успешно отменено.",
                ["TPShop_SERVICE_CMD_REFILL_SUCCESSFULLY"] = "Заполнения магазина товарами прошло успешно. Перезагружаем плагин для более корректной работы.",
                ["TPShop_SERVICE_CMD_LOCK_CATEGORY"] = "Данная категория не доступна для вас!",
                ["TPShop_SERVICE_UPDATE_PLUGINS"] = "Плагин работает некорректно! Обновите плагин до последней версии.",
                ["TPShop_SERVICE_1"] = "Что то пошло не так. Кажется страрая конфигурация сломана. Обратитесь к разработчику!",
                ["TPShop_SERVICE_2"] = "В старой конфигурации отсутсвуют товары!",
                ["TPShop_SERVICE_3"] = "Перестраиваем конфигурацию...",
                ["TPShop_SERVICE_4"] = "Список товаров успешно перестроен! Перезагружаем плагин...",
                ["TPShop_SERVICE_5"] = "У вас недостаточно прав для использования данной команды!",
                ["TPShop_SERVICE_6"] = "В данный момент магазин пуст.",
                ["TPShop_UI_PATTERN"] = "Отсутствует",
            }, this, "ru");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TPShop_UI_TITLE"] = "Catalog",
                ["TPShop_UI_BTN_BUY"] = "Buy",
                ["TPShop_UI_BTN_SALLE"] = "Sell",
                ["TPShop_UI_PRODUCT_INFO"] = "Purchase Price: {0}\nQuantity: {1}\nYour limit : {2}",
                ["TPShop_UI_PRODUCT_INFO_EXIT"] = "CLOSE",
                ["TPShop_UI_PRODUCT_SELL_INFO"] = "Selling Price: {0}\nQuantity: {1}\nYour limit : {2}",
                ["TPShop_UI_NOTIFICATION_IS_DUEL"] = "You cannot purchase an item during a duel!",
                ["TPShop_UI_NOTIFICATION_IS_RAID"] = "You cannot purchase an item during a raid/combat block!",
                ["TPShop_UI_NOTIFICATION_NOT_ENOUGH_SPACE"] = "Insufficient inventory space",
                ["TPShop_UI_NOTIFICATION_INSUFFICIENT_FUNDS"] = "You do not have enough funds for this purchase",
                ["TPShop_UI_NOTIFICATION_BUY_RECHARGE"] = "You cannot purchase '{0}'. You need to wait some more {1}",
                ["TPShop_UI_NOTIFICATION_BUY_LIMIT"] = "You can no longer purchase '{0}'. You have exceeded the limit for WIPE",
                ["TPShop_UI_NOTIFICATION_BUY_LIMIT_1"] = "You cannot purchase '{0}' in such quantity. You can buy more {1} lot(s)",
                ["TPShop_UI_NOTIFICATION_SUCCESSFUL_PURCHASE"] = "You have successfully purchased {0}",
                ["TPShop_UI_NOTIFICATION_SELL_IS_DUEL"] = "You cannot sell an item during a duel!",
                ["TPShop_UI_NOTIFICATION_SELL_IS_RAID"] = "You cannot sell an item during a raid/combat block!",
                ["TPShop_UI_NOTIFICATION_NOT_ENOUGH_ITEM"] = "Not enough item to sell",
                ["TPShop_UI_NOTIFICATION_SELL_RECHARGE"] = "You cannot sell '{0}'. You need to wait some more {1}",
                ["TPShop_UI_NOTIFICATION_SELL_LIMIT"] = "You can no longer sell '{0}'. You have exceeded the sales limit for WIPE",
                ["TPShop_UI_NOTIFICATION_SELL_LIMIT_1"] = "You cannot sell '{0}' in such quantity. you can sell more {1} lot(s)",
                ["TPShop_UI_NOTIFICATION_SUCCESSFUL_SALE"] = "You have successfully sold {0}",
                ["TPShop_SERVICE_EXIST_ECONOMICS"] = "You are missing the selected economics. Please! Check the economics settings in the configuration",
                ["TPShop_SERVICE_CONFIG_1"] = "Enter product with ID '{0}' There is an error. The product will be hidden, check if you have entered the Short Name correctly!",
                ["TPShop_SERVICE_CONFIG_2"] = "Enter product with ID '{0}' There is an error. The product will be hidden, the product ID is missing or the price is wrong!",
                ["TPShop_SERVICE_CMD_REFILL"] = "Execution of this command will entail changes in the configuration of products and categories. Make sure you save the configuration before this operation. Are you sure you want to continue? (TPShop.yes or TPShop.no)",
                ["TPShop_SERVICE_CMD_REFILL_YES"] = "The categories and products are refilled. Please wait...",
                ["TPShop_SERVICE_CMD_REFILL_NO"] = "Action canceled successfully.",
                ["TPShop_SERVICE_CMD_REFILL_SUCCESSFULLY"] = "Filling the store with goods was successful. Reload the plugin for more correct work.",
                ["TPShop_SERVICE_CMD_LOCK_CATEGORY"] = "This category is not available to you!",
                ["TPShop_SERVICE_UPDATE_PLUGINS"] = "The plugin is not working correctly! Update the plugin to the latest version.",
                ["TPShop_SERVICE_1"] = "Something went wrong. It seems the old configuration is broken. Contact the developer!",
                ["TPShop_SERVICE_2"] = "There are no products in the old configuration!",
                ["TPShop_SERVICE_3"] = "Rebuilding the configuration...",
                ["TPShop_SERVICE_4"] = "The list of products has been successfully rebuilt! Reloading the plugin...",
                ["TPShop_SERVICE_5"] = "You don't have enough rights to use this command!",
                ["TPShop_SERVICE_6"] = "В данный момент магазин пуст.",
                ["TPShop_UI_PATTERN"] = "Absent",
            }, this);
        }
		   		 		  						  	   		  		 			  		 			  			 		  		  
        private enum NotificationType
        {
            Error,
            Warning,
            Success
        }
        private const string PageProducts = "PAGE_PRODUCTS";

        private List<Configuration.CategoryShop> GetCategories(BasePlayer player)
        {
            string npcId = _humanNpcPlayerOpen.ContainsKey(player.userID) ? _humanNpcPlayerOpen[player.userID] : string.Empty;
            if (npcId == string.Empty)
            {
                return _config.product.FindAll(cat => cat != null && (string.IsNullOrEmpty(cat.PermissionCategory) || permission.UserHasPermission(player.UserIDString, cat.PermissionCategory)));
            }
            else
            {
                return _config.product.FindAll(cat => cat != null && _config.humanNpcs.NPCs.ContainsKey(npcId) && _config.humanNpcs.NPCs[npcId].Contains(cat.CategoryName) || _config.humanNpcs.NPCs[npcId].Count == 0);
            }
        }

        private string GetLang(string langKey, string userID = null, params object[] args)
        {
            StringBuilderInstance.Clear();
            if (args == null) return lang.GetMessage(langKey, this, userID);
            StringBuilderInstance.AppendFormat(lang.GetMessage(langKey, this, userID), args);
            return StringBuilderInstance.ToString();
        }

        
                private readonly List<ulong> _uiPlayerUseNow = new List<ulong>();
        private readonly Dictionary<ulong, string> _humanNpcPlayerOpen = new Dictionary<ulong, string>();
        
        
        private static class TimeHelper
        {
            public static string FormatTime(TimeSpan time, int maxSubstr = 5, string language = "ru")
            {
                string result = string.Empty;
                switch (language)
                {
                    case "ru":
                        int i = 0;
                        if (time.Days != 0 && i < maxSubstr)
                        {
                            if (!string.IsNullOrEmpty(result))
                                result += " ";

                            result += $"{Format(time.Days, "д", "д", "д")}";
                            i++;
                        }
                        if (time.Hours != 0 && i < maxSubstr)
                        {
                            if (!string.IsNullOrEmpty(result))
                                result += " ";
		   		 		  						  	   		  		 			  		 			  			 		  		  
                            result += $"{Format(time.Hours, "ч", "ч", "ч")}";
                            i++;
                        }
                        if (time.Minutes != 0 && i < maxSubstr)
                        {
                            if (!string.IsNullOrEmpty(result))
                                result += " ";

                            result += $"{Format(time.Minutes, "м", "м", "м")}";
                            i++;
                        }
                        if (time.Days == 0)
                        {
                            if (time.Seconds != 0 && i < maxSubstr)
                            {
                                if (!string.IsNullOrEmpty(result))
                                    result += " ";

                                result += $"{Format(time.Seconds, "с", "с", "с")}";
                                i++;
                            }
                        }
                        break;
                    default:
                        result = string.Format("{0}{1}{2}{3}",
                            time.Duration().Days > 0
                                ? $"{time.Days:0} day{(time.Days == 1 ? string.Empty : "s")}, "
                                : string.Empty,
                            time.Duration().Hours > 0
                                ? $"{time.Hours:0} hour{(time.Hours == 1 ? string.Empty : "s")}, "
                                : string.Empty,
                            time.Duration().Minutes > 0
                                ? $"{time.Minutes:0} minute{(time.Minutes == 1 ? string.Empty : "s")}, "
                                : string.Empty,
                            time.Duration().Seconds > 0
                                ? $"{time.Seconds:0} second{(time.Seconds == 1 ? string.Empty : "s")}"
                                : string.Empty);

                        if (result.EndsWith(", "))
                            result = result.Substring(0, result.Length - 2);
		   		 		  						  	   		  		 			  		 			  			 		  		  
                        if (string.IsNullOrEmpty(result))
                            result = "0 seconds";
                        break;
                }
                return result;
            }

            private static string Format(int units, string form1, string form2, string form3)
            {
                int tmp = units % 10;

                if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9)
                    return $"{units}{form1}";

                if (tmp >= 2 && tmp <= 4)
                    return $"{units}{form2}";

                return $"{units}{form3}";
            }
        }

        
                private void Unload()
        {
            SaveLimits();
            SaveCooldowns();
            SavePlayerData();
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, MainLayer);
            if (_apiLoadImage != null)
            {
                ServerMgr.Instance.StopCoroutine(_apiLoadImage);
                _apiLoadImage = null;
            }
            StringBuilderInstance = null;
            _instance = null;
        }
        private void OnServerInitialized()
        {
            StringBuilderInstance = new StringBuilder();
            if (!ExistEconomics())
            {
                NextTick(() =>
                {
                    PrintError(GetLang("TPShop_SERVICE_EXIST_ECONOMICS"));
                    Interface.Oxide.UnloadPlugin(Name);
                });
                return;
            }
            AddDisplayName();
            _apiLoadImage = ServerMgr.Instance.StartCoroutine(DownloadImages());
            CheckingProducts();
            if (!string.IsNullOrWhiteSpace(_config.mainSettings.PermissionUseShop) && !permission.PermissionExists(_config.mainSettings.PermissionUseShop))
                permission.RegisterPermission(_config.mainSettings.PermissionUseShop, this);
            if (_config.humanNpcs.UseHumanNpcs)
                SubAndUnSubHumanNpcHook(true);
        }
        
                private bool IsDuel(BasePlayer player)
        {
            if (Battles)
                return (bool)Battles?.Call("IsPlayerOnBattle", player.userID);
            else if (Duel)
                return (bool)Duel?.Call("IsPlayerOnActiveDuel", player);
            else
                return false;
        }
        private class ItemDisplayName
        {
            public string ru;
            public string en;
            public string description;
        }
        private void SavePlayerData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Players", _playerThemeSelect);
        }
        private readonly Dictionary<string, ItemDisplayName> _itemName = new Dictionary<string, ItemDisplayName>();
        
                [PluginReference] private readonly Plugin ImageLibrary, Economics, ServerRewards, IQEconomic, Kits, IQKits, Battles, Duel, ItemCostCalculator, NoEscape, TPEconomic, TPMenuSystem;
        private int GetLimit(BasePlayer player, Configuration.CategoryShop.Product item, bool purchase)
        {
            int limit = item.GetLimitLotWipe(player, purchase);
            if (limit == 0)
                return -1;

            int used = PlayerLimits.GetOrAddPlayer(player.userID).GetLimit(item, purchase);

            return limit - used;
        }
        private int GetCooldownTime(ulong player, Configuration.CategoryShop.Product item, bool purchase)
        {
            return GetCooldown(player)?.GetCooldownTime(item, purchase) ?? -1;
        }
        
                private Configuration _config;
        private bool ExistEconomics()
        {
            switch (_config.economicsCustomization.TypeEconomic)
            {
                case EconomicsType.Economics:
                    return Economics;
                case EconomicsType.ServerRewards:
                    return ServerRewards;
                case EconomicsType.IQEconomic:
                    return IQEconomic;
                case EconomicsType.Item:
                    return true;
                case EconomicsType.TPEconomic:
                    return TPEconomic;
                default:
                    return false;
            }
        }
        private const string ProductsLayer = "PRODUCTS_LAYER";
        private class Configuration
        {
            [JsonProperty(Ru ? "Настройка экономики" : "Economics")]
            public EconomicsCustomization economicsCustomization = new EconomicsCustomization();
            [JsonProperty(Ru ? "Настройка категорий и товаров" : "Setting up categories and products")]
            public List<CategoryShop> product = new List<CategoryShop>();
            [JsonProperty(Ru ? "Настройка интерфейса" : "Configuring the interface")]
            public InterfaceSettings interfaceSettings = new InterfaceSettings();
            [JsonProperty(Ru ? "Скидки по пермешенам" : "Discounts on permissions")]
            public DiscountStores discountStores = new DiscountStores();
            internal class MainSettings
            {
                [JsonProperty(Ru ? "Команды для открытия магазина (чат)" : "Commands for opening a store (chat)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public string[] Commands = { "shop" };

                [JsonProperty(Ru ? "Разрешения для использования шопа (Оставьте пустым если хотите дать доступ всем игрокам)(пример TPShop.use)" : "Permissions to use the shop (Leave empty if you want to give access to all players)(example TPShop.use)")]
                public string PermissionUseShop = "";

                [JsonProperty(Ru ? "Запрещать покупать/продавать во время рейд/комбат блока?" : "Prohibit buying/selling during a raid/combat block?")]
                public bool RaidBlock = true;
            }
            internal class EconomicsCustomization
            {
                [JsonProperty(Ru ? "Экономика (0 - Economics, 1 - ServerRewards, 2 - IQEconomic, 3 - Item, 4 - TPEconomic)" : "Economics (0 - Economics, 1 - ServerRewards, 2 - IQEconomic, 3 - Item, 4 - TPEconomic)")]
                public EconomicsType TypeEconomic = EconomicsType.IQEconomic;
                [JsonProperty(Ru ? "Приставка к балансу (например RP или $ - Не более 2 символов)" : "Prefix to the balance (for example, RP or $ - No more than 2 characters)")]
                public string PrefixBalance = "$";
                [JsonProperty(Ru ? "ShortName предмета (Использовать с типом 3)" : "Item shortname (Use with type 3)")]
                public string EconomicShortname = "";
                [JsonProperty(Ru ? "SkinId предмета (Использовать с типом 3)" : "Item SkinId (Use with Type 3)")]
                public ulong EconomicSkinId = 0;

            }
            internal class CategoryShop
            {
                [JsonProperty(Ru ? "Названия категории" : "Category names")]
                public string CategoryName;
                [JsonProperty(Ru ? "Разрешения для доступа к категории" : "Permissions to access the category")]
                public string PermissionCategory;
                [JsonProperty(Ru ? "Список товаров в данной категории" : "List of products in this category")]
                public List<Product> product = new List<Product>();
                internal class Product
                {
                    [JsonProperty(Ru ? "Тип предмета (0 - Предмет, 1 - Чертёж, 2 - Кастомный предмет, 3 - Команда, 4 - Кит)" : "Item Type (0 - Item, 1 - BluePrint, 2 - Custom item, 3 - Commands, 4 - Kit)")]
                    public ItemType Type;
                    [JsonProperty(Ru ? "Уникальный ID (НЕ ТРОГАТЬ)" : "Unique ID (DO NOT TOUCH)")]
                    public int ID = 0;
                    [JsonProperty("Shortame")]
                    public string ShortName;
                    [JsonProperty(Ru ? "Описания" : "Descriptions")]
                    public string Descriptions;
                    [JsonProperty(Ru ? "Цена" : "Price")]
                    public float Price;
                    [JsonProperty(Ru ? "Количество" : "Quantity")]
                    public int Amount;
                    [JsonProperty(Ru ? "Кастом имя предмета (Использовать с типом предмета 2 , 3 и 4)" : "Custom item name (Use with item type 2, 3 and 4)")]
                    public string Name;
                    [JsonProperty(Ru ? "SkinID предмета (Использовать с типом предмета 2)" : "Item SkinID (Use with item type 2)")]
                    public ulong SkinID;
                    [JsonProperty(Ru ? "Команды (Использовать с типом предмета 3)" : "Commands (Use with item type 3)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                    public List<string> Commands = new List<string>();
                    [JsonProperty(Ru ? "URL картинки" : "Image URL")]
                    public string Url;
                    [JsonProperty(Ru ? "Кит (Kits - kit name. IQKits - kit key)" : "Kits (Kits - kit name. IQKits - kit key)")]
                    public string KitName;
                    [JsonProperty(Ru ? "Задержка покупки (0 - неограниченно)" : "Purchase delay (0 - unlimited)")]
                    public float BuyCooldown;
                    [JsonProperty(Ru ? "Задержка продажи (0 - неограниченно)" : "Sale delay (0 - unlimited)")]
                    public float SellCooldown;
                    [JsonProperty(Ru ? "Максимальное количество лотов за 1 покупку (Максимум 99)" : "Maximum number of lots for 1 purchase (Maximum 99)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                    public Dictionary<string, int> BuyLimits = new Dictionary<string, int>();
                    [JsonProperty(Ru ? "Максимальное количество покупаемых лотов за вайп (0 - неограниченно)" : "The maximum number of purchased lots per wipe (0 - unlimited)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                    public Dictionary<string, int> BuyLimitsWipe = new Dictionary<string, int>();
                    [JsonIgnore]
                    public ItemDefinition Definition;
                    [JsonIgnore]
                    public bool ItemsError = false;

                    public string GetProductName(string userId)
                    {
                        string userLang = _instance.lang.GetLanguage(userId);

                        if (!string.IsNullOrWhiteSpace(Name))
                            return Name;

                        if (!string.IsNullOrWhiteSpace(ShortName) && _instance._itemName.ContainsKey(ShortName))
                            return userLang == "ru" ? _instance._itemName[ShortName].ru : _instance._itemName[ShortName].en;

                        return string.Empty;
                    }
                    public string GetProductDescription()
                    {
                        if (!string.IsNullOrWhiteSpace(Descriptions))
                            return Descriptions;
                        if (!string.IsNullOrWhiteSpace(ShortName) && _instance._itemName.ContainsKey(ShortName) && !string.IsNullOrWhiteSpace(_instance._itemName[ShortName].description))
                            return _instance._itemName[ShortName].description;

                        return string.Empty;
                    }
                    public int GetLimitLot(BasePlayer player, bool purchase)
                    {
                        Dictionary<string, int> dict = BuyLimits;
                        int limit = 0;

                        if (dict.Count == 0)
                            return limit;

                        foreach (KeyValuePair<string, int> discount in dict)
                            if (_instance.permission.UserHasPermission(player.UserIDString, discount.Key))
                                if (limit < discount.Value)
                                    limit = discount.Value;
                        return limit;
                    }

                    public int GetLimitLotWipe(BasePlayer player, bool purchase)
                    {
                        Dictionary<string, int> dict = BuyLimitsWipe;
                        int limit = 0;
                        if (dict.Count == 0)
                            return limit;

                        foreach (KeyValuePair<string, int> discount in dict)
                            if (_instance.permission.UserHasPermission(player.UserIDString, discount.Key))
                                if (limit < discount.Value)
                                    limit = discount.Value;
                        return limit;
                    }
                }
            }
            internal class InterfaceSettings
            {
                [JsonProperty(Ru ? "Включить возможность менять тему?" : "Enable the ability to change the theme?")]
                public bool UseChangeTheme = true;
                [JsonProperty(Ru ? "Тема по умолчанию (0 - светлая, 1 - темная)" : "Default theme (0 - light, 1 - dark)")]
                public ThemeType ThemeTypeDefault = ThemeType.Dark;

                [JsonProperty(Ru ? "Настройки светлой темы UI" : "Light Theme UI Settings")]
                public ThemeCustomization LightTheme = new ThemeCustomization
                {
                    colorMainBG = "1 1 1 1",
                    colorTextTitle = "0 0 0 1",

                    colorImgBalance = "0.26 0.53 0.80 1",
                    colorTextBalance = "0 0 0 1",
                    colortext2 = "0.627451 0.6313726 0.6392157 1",
                    colortext3 = "0 0 0 1",
                    colortext6 = "0.97 0.97 0.98 1.00",
                    colortext7 = "0 0 0 1",
                    colortext8 = "0.38 0.77 0.43 1.00",
                    Colortext10 = "0 0 0 1",
                    colortext4 = "0.51 0.51 0.51 1.00",
                    Colortext5 = "0.55 0.55 0.55 1.00",
                    closeBtnColor2 = "0.8392158 0.3647059 0.3568628 1"
                };
                [JsonProperty(Ru ? "Настройки темной темы UI" : "Dark Theme UI Settings")]
                public ThemeCustomization DarkTheme = new ThemeCustomization
                {
                    colorMainBG = "0.13 0.15 0.16 1.00",
                    colorTextTitle = "0.87 0.87 0.87 1.00",
                    colorImgTitle = "0.62 0.63 0.64 1.00",
                    colorImgBalance = "0.26 0.53 0.80 1",
                    colorTextBalance = "1 1 1 1",
                    colortext2 = "0.627451 0.6313726 0.6392157 1",
                    colortext3 = "0.87 0.87 0.87 1.00",
                    colortext6 = "0.17 0.18 0.21 1.00",
                    colortext7 = "0.87 0.87 0.87 1.00",
                    colortext8 = "0.38 0.77 0.43 1.00",
                    Colortext10 = "1 1 1 1",
                    colortext4 = "0.51 0.51 0.51 1.00",
                    Colortext5 = "0.55 0.55 0.55 1.00",
                    closeBtnColor2 = "0.8392158 0.3647059 0.3568628 1",
                };

                internal class ThemeCustomization
                {
                    [JsonProperty(Ru ? "Цвет основного фона магазина" : "The color of the main background of the store")]
                    public string colorMainBG = "1 1 1 1";
                    [JsonProperty(Ru ? "[TITLE] Цвет текста" : "[TITLE] Text color")]
                    public string colorTextTitle = "0 0 0 1";
                    [JsonProperty(Ru ? "[TITLE] Цвет картинки" : "[TITLE] Picture Color")]
                    public string colorImgTitle = "0 0 0 1";
                    [JsonProperty(Ru ? "Цвет картинки баланса" : "Balance picture color")]
                    public string colorImgBalance = "0.26 0.53 0.80 1";
                    [JsonProperty(Ru ? "Цвет текста баланса" : "Balance text color")]
                    public string colorTextBalance = "0 0 0 1";
                    [JsonProperty(Ru ? "[PRODUCT] Цвет количества предметов (цифры)" : "[PRODUCT] color of the number of items (digits)")]
                    public string colortext2 = "0.627451 0.6313726 0.6392157 1";
                    [JsonProperty(Ru ? "[PRODUCT] Цвет фона товара" : "[PRODUCT] product background color")]
                    public string colortext6 = "0.97 0.97 0.98 1.00";
                    [JsonProperty(Ru ? "[PRODUCT] Цвет текста названия товара" : "[PRODUCT] Text color of the product name")]
                    public string colortext7 = "0 0 0 1";
                    [JsonProperty(Ru ? "[PRODUCT] Цвет кнопки купить" : "[PRODUCT] Buy button color")]
                    public string colortext8 = "0.38 0.77 0.43 1.00";
                    [JsonProperty(Ru ? "[PRODUCT] Цвет кнопки закрыть" : "[PRODUCT] Close button color")]
                    public string closeBtnColor2 = "0.8392158 0.3647059 0.3568628 1";
                    [JsonProperty(Ru ? "[CATEGORY] цвет названия активной категории" : "[CATEGORY] color of the name of the active category")]
                    public string colortext3 = "0 0 0 1";
                    [JsonProperty(Ru ? "[CATEGORY] Цвет названия неактивной категорий" : "[CATEGORY] Color of the inactive category name")]
                    public string colortext4 = "0.51 0.51 0.51 1.00";
                    [JsonProperty(Ru ? "[NOTIFICATIONS] Цвет текста в уведомлении" : "[NOTIFICATIONS] The color of the text in the notification")]
                    public string Colortext10 = "0 0 0 1";
                    [JsonProperty(Ru ? "Цвет дополнительного серого текста" : "Color of additional gray text")]
                    public string Colortext5 = "0.55 0.55 0.55 1.00";
                }
            }
            [JsonProperty(Ru ? "Настройка Human NPC" : "Setting up a Human NPC")]
            public HumanNpcs humanNpcs = new HumanNpcs();
            [JsonProperty(Ru ? "Основные настройки" : "Basic Settings")]
            public MainSettings mainSettings = new MainSettings();

            internal class HumanNpcs
            {
                [JsonProperty(Ru ? "Включить поддержку Human NPC ?" : "Enable Human NPC support ?")]
                public bool UseHumanNpcs = false;
                [JsonProperty(PropertyName = "Human NPC  [NPC ID | list shop category]")]
                public Dictionary<string, List<string>> NPCs = new Dictionary<string, List<string>>();
            }
            internal class DiscountStores
            {
                [JsonProperty(Ru ? "Пермешен/Скидка в %" : "Permissions/Discount in %", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public Dictionary<string, float> DiscountPerm = new Dictionary<string, float>
                {
                    ["TPShop.vip"] = 10.0f
                };
            }
        }
        private const string ConfirmationsPanel = "CONFIRMATIONS_PANEL";

                private bool IsRaid(BasePlayer player)
        {
            if (NoEscape == null)
                return false;
            return (bool)NoEscape?.Call("IsBlocked", player);
        }

        
        private void CheckOnDuplicates()
        {
            HashSet<int> itemsSeen = new HashSet<int>();
            foreach (Configuration.CategoryShop item in _config.product)
                foreach (Configuration.CategoryShop.Product items in item.product)
                    if (!itemsSeen.Add(items.ID))
                    {
                        items.ID = Core.Random.Range(int.MinValue, int.MaxValue);
                    }
        }
        private const string PanelProduct = "MAIN_PANEL_PRODUCTS";

            }
}

namespace Oxide.Plugins.TPShopExtensionMethods
{
    public static class ExtensionMethods
    {
                public static HashSet<TSource> Where<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            HashSet<TSource> result = new HashSet<TSource>();
            using (IEnumerator<TSource> enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) result.Add(enumerator.Current);
            return result;
        }
        
                public static IEnumerable<TSource> Page<TSource>(this IEnumerable<TSource> source, int page, int pageSize)
        {
            return source.Skip((page) * pageSize).Take(pageSize);
        }
        
                public static IEnumerable<TSource> Take<TSource>(this IEnumerable<TSource> source, int count)
        {
            if (count > 0)
            {
                foreach (TSource element in source)
                {
                    yield return element;
                    count--;
                    if (count == 0)
                        break;
                }
            }
        }
        
                public static IEnumerable<TSource> Skip<TSource>(this IEnumerable<TSource> source, int count)
        {
            if (source is IList<TSource>)
            {
                IList<TSource> list = (IList<TSource>)source;
                for (int i = count; i < list.Count; i++)
                {
                    yield return list[i];
                }
            }
            else if (source is IList)
            {
                IList list = (IList)source;
                for (int i = count; i < list.Count; i++)
                {
                    yield return (TSource)list[i];
                }
            }
            else
            {
                // .NET framework
                using (IEnumerator<TSource> e = source.GetEnumerator())
                {
                    while (count > 0 && e.MoveNext())
                        count--;
                    if (count <= 0)
                    {
                        while (e.MoveNext())
                            yield return e.Current;
                    }
                }
            }
        }
        
                public static int Sum<TSource>(this IList<TSource> source, Func<TSource, int> predicate)
        {
            int result = 0;
            for (int i = 0; i < source.Count; i++)
                result += predicate(source[i]);
            return result;
        }
            }
}
