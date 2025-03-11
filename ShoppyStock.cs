using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ShoppyStock", "ThePitereq", "1.2.4")]
    public class ShoppyStock : RustPlugin
    {
        [PluginReference] private readonly Plugin ImageLibrary, PopUpAPI, Economics, ServerRewards, RedeemStorageAPI, DiscordCore, Artifacts, BonusCore, NoEscape, BankSystem, IQEconomic;

        private readonly Dictionary<BasePlayer, ShopCache> shopCache = new Dictionary<BasePlayer, ShopCache>();
        private readonly Dictionary<BasePlayer, StockPosition> stockPosition = new Dictionary<BasePlayer, StockPosition>();
        private readonly List<BasePlayer> openedUis = new List<BasePlayer>();
        private readonly Dictionary<string, List<ItemDefinition>> stockCategories = new Dictionary<string, List<ItemDefinition>>();
        private readonly Dictionary<string, StockItemDefinitionData> itemDefinitions = new Dictionary<string, StockItemDefinitionData>();
        private readonly Dictionary<ulong, PermissionData> cachedListingCount = new Dictionary<ulong, PermissionData>();
        private readonly Dictionary<BasePlayer, Mailbox> mailboxes = new Dictionary<BasePlayer, Mailbox>();
        private readonly Dictionary<BasePlayer, BoxStorage> boxes = new Dictionary<BasePlayer, BoxStorage>();
        private readonly Dictionary<string, Timer> stockTimers = new Dictionary<string, Timer>();
        private readonly Dictionary<BasePlayer, BasePlayer> shopNpc = new Dictionary<BasePlayer, BasePlayer>();
        private readonly Dictionary<BasePlayer, AddItemData> addingItemsCache = new Dictionary<BasePlayer, AddItemData>();
        private readonly Dictionary<BasePlayer, DateTime> lastUsage = new Dictionary<BasePlayer, DateTime>();
        private readonly List<string> newStocks = new List<string>();
        private readonly bool privFeatures = false;
        private bool anyStock = false;
        private bool selling = false;

        private class AddItemData
        {
            public string shopName;
            public string category;
            public bool stockMarket;
            public float var1 = 0;
            public float var2 = 0;
            public int var3 = 0;
        }

        private class ShopCache
        {
            public string currentShop = "";
            public string shopCategory;
            public bool affordableOnly = false;
            public string searchValue = "";
            public int page = 1;
            public int categoryPage = 1;
            public bool transferOnline = false;
            public CurrentListing currentListings;
            public StockCache stockMarket = new StockCache();
            public string lastVisit = "";
        }

        private class StockPosition
        {
            public string currentItem = "";
        }

        private class CurrentListing
        {
            public string listingKey;
            public string shopName;
            public ListingData listing;
            public int amount;
        }

        private class StockCache
        {
            public string shopName = "";
            public string category;
            public bool sellToServerOnly = false;
            public char buySellOrders = ' ';
            public int page = 1;
            public string search = "";
            public int categoryPage = 1;
            public ListingCache listing = new ListingCache();
            public RequestCache request = new RequestCache();
            public bool quickList = false;
        }

        private class ListingCache
        {
            public int page = 1;
            public OrderData selected = null;
            public bool hideOwned = true;
            public bool buyOffers = true;
            public int timestamp = 0;
            public int index = -1;
            public string key = "";
            public int amount = 1;
        }

        private class RequestCache
        {
            public int amount = 1;
            public float price = 0;
            public bool buyRequest = false;
            public string itemShortname = "";
            public ulong itemSkin = 0;
        }

        private void Init()
        {
            config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(config);
            LoadData();
        }

        private void OnServerInitialized()
        {
            if (PopUpAPI == null)
                PrintWarning("PopUpAPI not found! Pop-Up messages will not appear!");
            else if (PopUpAPI.Version.Major < 2)
                PrintWarning("PopUpAPI V2 not found! Update API to make this plugin works properly!");
            if (ImageLibrary == null)
                PrintWarning("ImageLibrary not found! Images will not appear!");
            if (config.debug) Puts("[DEBUG] Starting loading plugin... DEBUG ENABLED");
            foreach (var shop in data.shops)
            {
                if (shop.Value.categories.Count > 0) continue;
                if (config.shops[shop.Key].generateAllDefaultItems)
                {
                    foreach (var item in ItemManager.itemList)
                    {
                        string cat = item.category.ToString();
                        shop.Value.categories.TryAdd(cat, new CategoryData());
                        shop.Value.categories[cat].listings.Add($"{item.shortname}-0", new ListingData() { displayName = item.displayName.english, shortname = item.shortname, price = 1 });
                    }
                }
                else
                {
                    shop.Value.categories.Add("tools", new CategoryData());
                    shop.Value.categories["tools"].listings.Add("pickaxe-0", new ListingData() { displayName = "Pickaxe", shortname = "pickaxe", price = 150 });
                    shop.Value.categories["tools"].listings.Add("axe-0", new ListingData() { displayName = "Axe", shortname = "axe", price = 150 });
                    shop.Value.categories.Add("resources", new CategoryData());
                    shop.Value.categories["resources"].listings.Add("wood-0", new ListingData() { displayName = "Wood", shortname = "wood", price = 765000 });
                }
                Puts($"Generating default shop categories for shop {shop.Key}...");
            }
            if (config.debug) Puts("[DEBUG] Finished generating default categories.");
            foreach (var stock in newStocks)
            {
                data.stockMarkets.TryAdd(stock, new StockData());
                data.stockMarkets[stock].stockConfig = new StockItemData();
                data.stockMarkets[stock].stockConfig.customItems = new Dictionary<string, CustomItemData>()
                {
                    {"coal-2871627272", new CustomItemData() { amount = 1, category = "CustomItems", displayName = "Golden Cloth", shortname = "coal", skin = 2871627272 } }
                };
                data.stockMarkets[stock].stockConfig.priceCalculations.priceBarriers = new Dictionary<float, float>()
                {
                    { 50, 2 }, { 47, 4 }, { 44, 6 }, { 41, 8 }, { 38, 10 }, { 35, 20 }, { 32, 30 }, { 29, 40 }, { 26, 50 }, { 23, 60 }, { 20, 65 }, { 17, 70 }, { 14, 75 }, { 11, 80 }, { 8, 85 }, { 5, 90 }, { 2, 95 }, { 0, 100 }
                };
                data.stockMarkets[stock].stockConfig.priceCalculations.priceDropChart = new Dictionary<float, float>()
                {
                    { 5000, 4 }, { 2500, 4 }, { 1200, 3 }, { 800, 2.5f }, { 500, 2 }, { 250, 1.5f }, { 125, 1.2f }
                };
                data.stockMarkets[stock].stockConfig.priceCalculations.sellPricePentality = new Dictionary<float, PentalityData>()
                {
                    { 10000, new PentalityData() { pentalityLength = 24, percentage = 25 } },
                    { 9000, new PentalityData() { pentalityLength = 20, percentage = 30 } },
                    { 7000, new PentalityData() { pentalityLength = 16, percentage = 40 } },
                    { 5000, new PentalityData() { pentalityLength = 12, percentage = 50 } },
                    { 4000, new PentalityData() { pentalityLength = 10, percentage = 50 } },
                    { 3000, new PentalityData() { pentalityLength = 8, percentage = 50 } },
                    { 2000, new PentalityData() { pentalityLength = 6, percentage = 60 } },
                    { 1000, new PentalityData() { pentalityLength = 6, percentage = 70 } },
                };
                data.stockMarkets[stock].stockConfig.priceCalculations.goalAchievedChart = new Dictionary<float, float>()
                {
                    { 0, 3 }, { 1, 2 }, { 5, 1.7f }, { 10, 1.5f }, { 25, 1.4f }, { 50, 1.3f }, { 75, 1.2f }
                };
                data.stockMarkets[stock].stockConfig.priceCalculations.sellAmountOnlineMultiplier = new Dictionary<int, float>()
                {
                    { 20, 1.5f }, { 40, 2f }, { 60, 3f }, { 80, 4.5f }, { 100, 6f }
                };
                data.stockMarkets[stock].stockConfig.priceCalculations.multplierAmountChance = new Dictionary<float, float>()
                {
                    { 0, 20 }, { 25, 15 }, { 50, 10 }, { 100, 5 }, { 200, 1 }
                };
                data.stockMarkets[stock].stockConfig.priceCalculations.positiveRandomEvents = new List<string>()
                {
                    "HighDemand", "VeryHighDemand", "ExtremeDemand"
                };
                data.stockMarkets[stock].stockConfig.priceCalculations.negativeRandomEvents = new List<string>()
                {
                    "NegativeDemand", "UltraNegativeDemand"
                };
                data.stockMarkets[stock].stockConfig.serverSell = new Dictionary<string, Dictionary<ulong, ServerSellData>>()
                {
                    { "wood", new Dictionary<ulong, ServerSellData>() {
                        { 0, new ServerSellData() { defaultAmount = 50000, displayName = "Wood", maximalPrice = 4, minimalPrice = 0.2f } },
                        { 1567546863, new ServerSellData() { defaultAmount = 100, displayName = "Magic Wood", maximalPrice = 19, minimalPrice = 1f, maxDailySoldPerms = new Dictionary<string, int>() { { "shoppystock.maxdailysell.1000", 1000 }, { "shoppystock.maxdailysell.5000", 5000 } } } },
                    } },
                    { "stones", new Dictionary<ulong, ServerSellData>() {
                        { 0, new ServerSellData() { defaultAmount = 50000, displayName = "Stones", maximalPrice = 5, minimalPrice = 0.4f } }
                    } }
                };
                Puts($"Generating default data values for stock market {stock}...");
            }
            if (config.debug) Puts("[DEBUG] Finished generating default stock market configurations.");
            string today = DateTime.Now.ToShortDateString();
            int removedCount = 0;
            foreach (var shop in data.shops)
                foreach (var user in shop.Value.users)
                    foreach (var dailyPurchase in user.Value.dailyPurchases.ToList())
                        if (dailyPurchase.Key != today)
                        {
                            data.shops[shop.Key].users[user.Key].dailyPurchases.Remove(dailyPurchase.Key);
                            removedCount++;
                        }
            if (removedCount > 0)
                Puts($"Removed {removedCount} old daily limits from data.");
            if (config.debug) Puts("[DEBUG] Finished clearing daily limits.");
            List<KeyValuePair<string, ulong>> imagesToDownload = new List<KeyValuePair<string, ulong>>();
            foreach (var shop in config.shops)
            {
                if (shop.Value.canDeposit && shop.Value.depositItem.url != "")
                    AddImage(shop.Value.depositItem.url, shop.Value.depositItem.shortname, shop.Value.depositItem.skin);
                if (shop.Value.stockConfig.canStockMarket)
                {
                    anyStock = true;
                    if (shop.Value.stockConfig.bankEnabled && shop.Value.stockConfig.bankPermission != "" && shop.Value.stockConfig.bankPermission.ToLower().Contains(Name.ToLower()) && !permission.PermissionExists(shop.Value.stockConfig.bankPermission, this))
                        permission.RegisterPermission(shop.Value.stockConfig.bankPermission, this);
                    if (shop.Value.stockConfig.favouritesEnabled && shop.Value.stockConfig.favouritesPermission != "" && shop.Value.stockConfig.favouritesPermission.ToLower().Contains(Name.ToLower()) && !permission.PermissionExists(shop.Value.stockConfig.favouritesPermission, this))
                        permission.RegisterPermission(shop.Value.stockConfig.favouritesPermission, this);
                    foreach (var perm in shop.Value.stockConfig.sellPriceMultipliers)
                        if (!permission.PermissionExists(perm.Key, this))
                            permission.RegisterPermission(perm.Key, this);
                    foreach (var perm in shop.Value.stockConfig.maxBankIndividualItemsPerms)
                        if (!permission.PermissionExists(perm.Key, this))
                            permission.RegisterPermission(perm.Key, this);
                    foreach (var perm in shop.Value.stockConfig.maxBankStacksPerms)
                        if (!permission.PermissionExists(perm.Key, this))
                            permission.RegisterPermission(perm.Key, this);
                }
                if (shop.Value.iconUrl != "")
                    AddImage(shop.Value.iconUrl, $"UI_ShoppyStock_{shop.Key}_Icon", 0);
                foreach (var category in data.shops[shop.Key].categories)
                {
                    if (category.Value.iconUrl != "")
                        AddImage(category.Value.iconUrl, $"UI_ShoppyStock_Category_{category.Key}", 0);
                    foreach (var (key, item) in category.Value.listings)
                    {
                        if (item.iconUrl != "")
                        {
                            if (item.commands.Count > 0)
                                AddImage(item.iconUrl, key, 0);
                            else
                                AddImage(item.iconUrl, item.shortname, item.skin);
                        }
                        else if (item.skin != 0)
                            imagesToDownload.Add(new KeyValuePair<string, ulong>(item.shortname, item.skin));
                    }
                }
                if (shop.Value.permission != "" && shop.Value.permission.ToLower().Contains(Name.ToLower()) && !permission.PermissionExists(shop.Value.permission, this))
                    permission.RegisterPermission(shop.Value.permission, this);
                foreach (var discount in shop.Value.discounts)
                    if (discount.Key.ToLower().Contains(Name.ToLower()) && !permission.PermissionExists(discount.Key, this))
                        permission.RegisterPermission(discount.Key, this);
                foreach (var category in data.shops[shop.Key].categories.Values)
                {
                    if (category.permission != "" && category.permission.ToLower().Contains(Name.ToLower()) && !permission.PermissionExists(category.permission, this))
                        permission.RegisterPermission(category.permission, this);
                    foreach (var discount in category.discounts)
                        if (discount.Key.ToLower().Contains(Name.ToLower()) && !permission.PermissionExists(discount.Key, this))
                            permission.RegisterPermission(discount.Key, this);
                    foreach (var item in category.listings.Values)
                    {
                        foreach (var discount in item.discounts)
                            if (discount.Key.ToLower().Contains(Name.ToLower()) && !permission.PermissionExists(discount.Key, this))
                                permission.RegisterPermission(discount.Key, this);
                        if (item.permission != "" && item.permission.ToLower().Contains(Name.ToLower()) && !permission.PermissionExists(item.permission, this))
                            permission.RegisterPermission(item.permission, this);
                        if (item.blacklistPermission != "" && item.blacklistPermission.ToLower().Contains(Name.ToLower()) && !permission.PermissionExists(item.blacklistPermission, this))
                            permission.RegisterPermission(item.blacklistPermission, this);
                    }
                }
            }
            if (config.debug) Puts("[DEBUG] Finished loading icons and permissions to shops.");
            if (anyStock)
            {
                if (RedeemStorageAPI == null)
                    PrintWarning("RedeemStorageAPI not found! Players will be not able to redeem items from market!");
                foreach (var item in ItemManager.itemList)
                {
                    if (config.ignoredShortnames.Contains(item.shortname)) continue;
                    string category = item.category.ToString();
                    stockCategories.TryAdd(category, new List<ItemDefinition>());
                    stockCategories[category].Add(item);
                    itemDefinitions.TryAdd($"{item.shortname}-0", new StockItemDefinitionData() { category = category, displayName = item.displayName.english, shortname = item.shortname });
                }
                if (config.debug) Puts("[DEBUG] Finished generating default categories.");
                foreach (var stock in data.stockMarkets)
                {
                    if (!config.shops.ContainsKey(stock.Key))
                    {
                        Puts($"Stock {stock.Key} is somehow missing in your config. Skipping this stock data check...");
                        continue;
                    }
                    int maxHistory = 0;
                    foreach (var timestamp in config.timestamps)
                    {
                        int calc = Mathf.CeilToInt(timestamp.Key / config.shops[stock.Key].stockConfig.updateInterval);
                        if (maxHistory < calc)
                            maxHistory = calc;
                    }
                    foreach (var item in stock.Value.stockConfig.customItems)
                    {
                        itemDefinitions.TryAdd($"{item.Key}", new StockItemDefinitionData() { category = item.Value.category, displayName = item.Value.displayName, shortname = item.Value.shortname, skin = item.Value.skin });
                        if (item.Value.url != "")
                            AddImage(item.Value.url, item.Value.shortname, item.Value.skin);
                        else if (item.Value.skin != 0)
                            imagesToDownload.Add(new KeyValuePair<string, ulong>(item.Value.shortname, item.Value.skin));
                    }
                    foreach (var stockShortname in stock.Value.sellCache)
                    {
                        foreach (var stockItem in stockShortname.Value)
                        {
                            List<float> clearedHistory1 = new List<float>(stockItem.Value.priceHistory.Take(maxHistory));
                            stockItem.Value.priceHistory = clearedHistory1;
                            List<int> clearedHistory2 = new List<int>(stockItem.Value.sellAmountHistory.Take(maxHistory));
                            stockItem.Value.sellAmountHistory = clearedHistory2;
                        }
                    }
                    foreach (var item in stock.Value.stockConfig.serverSell.Values)
                    {
                        foreach (var skin in item.Values)
                        {
                            if (skin.maxDailySold > 0 && !config.enableStockStats) PrintWarning($"You have to enable stock market logs to make daily sell values work. Set \"Enable Stock Market Logging\" to true in main config!");
                            foreach (var perm in skin.maxDailySoldPerms)
                                if (!permission.PermissionExists(perm.Key, this))
                                    permission.RegisterPermission(perm.Key, this);
                        }
                    }
                    foreach (var shortnames in data.stockMarkets[stock.Key].playerData.buyOrders)
                        if (data.stockMarkets[stock.Key].playerData.buyOrders[shortnames.Key].ContainsKey(0))
                            foreach (var listing in data.stockMarkets[stock.Key].playerData.buyOrders[shortnames.Key][0])
                                if (listing.item.skin != 0)
                                    imagesToDownload.Add(new KeyValuePair<string, ulong>(listing.item.shortname, listing.item.skin));
                    foreach (var shortnames in data.stockMarkets[stock.Key].playerData.sellOrders)
                        if (data.stockMarkets[stock.Key].playerData.sellOrders[shortnames.Key].ContainsKey(0))
                            foreach (var listing in data.stockMarkets[stock.Key].playerData.sellOrders[shortnames.Key][0])
                                if (listing.item.skin != 0)
                                    imagesToDownload.Add(new KeyValuePair<string, ulong>(listing.item.shortname, listing.item.skin));
                }
                if (config.debug) Puts("[DEBUG] Finished downloading images to market.");
                if (imagesToDownload.Count > 0)
                    ImageLibrary?.Call("LoadImageList", Name, imagesToDownload);
                Puts($"Loaded {itemDefinitions.Count} stock market item definitions!");
                foreach (var icon in config.categoryIcons)
                    if (icon.Value != "")
                        AddImage(icon.Value, $"UI_ShoppyStock_Category_{icon.Key}", 0);
                foreach (var perm in config.listingPermissions)
                    if (perm.Key.ToLower().Contains(Name.ToLower()) && !permission.PermissionExists(perm.Key, this))
                        permission.RegisterPermission(perm.Key, this);
                foreach (var perm in config.timestamps)
                    if (perm.Value.ToLower().Contains(Name.ToLower()) && !permission.PermissionExists(perm.Value, this))
                        permission.RegisterPermission(perm.Value, this);
                bool setTime = false;
                foreach (var shop in config.shops)
                {
                    if (shop.Value.stockConfig.updateIntervalHourMinutes.Count > 0)
                    {
                        setTime = true;
                        break;
                    }
                }
                if (config.debug) Puts("[DEBUG] Finished misc things to market.");
                if (setTime)
                {
                    stockTimers.TryAdd("hourSelect", timer.Every(50, () =>
                    {
                        int currentMinuite = DateTime.Now.Minute;
                        int notReady = 0;
                        foreach (var shop in config.shops)
                        {
                            if (shop.Value.stockConfig.canStockMarket && !stockTimers.ContainsKey(shop.Key))
                                notReady++;
                        }
                        if (notReady == 0)
                        {
                            Puts($"All start hours set succesfully!");
                            stockTimers["hourSelect"].Destroy();
                        }
                        foreach (var shop in config.shops)
                        {
                            if (!shop.Value.stockConfig.canStockMarket || stockTimers.ContainsKey(shop.Key)) continue;
                            if (!shop.Value.stockConfig.updateIntervalHourMinutes.Contains(currentMinuite)) continue;
                            UpdateStockPrices(shop.Key);
                            stockTimers.TryAdd(shop.Key, timer.Every(shop.Value.stockConfig.updateInterval * 60, () => UpdateStockPrices(shop.Key)));
                        }
                    }));
                }
                else
                {
                    foreach (var shop in config.shops)
                        if (shop.Value.stockConfig.canStockMarket)
                        {
                            UpdateStockPrices(shop.Key);
                            stockTimers.TryAdd(shop.Key, timer.Every(shop.Value.stockConfig.updateInterval * 60, () => UpdateStockPrices(shop.Key)));
                        }
                }
                if (config.debug) Puts("[DEBUG] Finished market basic setup.");
            }
            foreach (var market in data.stockMarkets)
            {
                foreach (var name in market.Value.playerData.buyOrders)
                    foreach (var skin in name.Value)
                        foreach (var listing in skin.Value)
                        {
                            cachedListingCount.TryAdd(listing.sellerId, new PermissionData());
                            cachedListingCount[listing.sellerId].buyListings++;
                        }
                foreach (var name in market.Value.playerData.sellOrders)
                    foreach (var skin in name.Value)
                        foreach (var listing in skin.Value)
                        {
                            cachedListingCount.TryAdd(listing.sellerId, new PermissionData());
                            cachedListingCount[listing.sellerId].sellListings++;
                        }
            }
            if (config.debug) Puts("[DEBUG] Finished making listing count cache.");
            SaveData(true);
            foreach (var category in config.customCategories)
                stockCategories.TryAdd(category, new List<ItemDefinition>());
            LoadMessages();
            AddImage("https://images.pvrust.eu/ui_icons/ShoppyStock/dollar_0.png", "UI_ShoppyStock_DefaultShopIcon", 0);
            AddImage("https://images.pvrust.eu/ui_icons/ShoppyStock/arrow_left_0.png", "UI_ShoppyStock_ArrowLeft", 0);
            AddImage("https://images.pvrust.eu/ui_icons/ShoppyStock/arrow_right_0.png", "UI_ShoppyStock_ArrowRight", 0);
            AddImage("https://images.pvrust.eu/ui_icons/ShoppyStock/search_0.png", "UI_ShoppyStock_Search", 0);
            AddImage("https://images.pvrust.eu/ui_icons/ShoppyStock/category_0.png", "UI_ShoppyStock_DefaultCategoryIcon", 0);
            foreach (var command in config.commands)
                cmd.AddChatCommand(command, this, nameof(MarketCommand));
            foreach (var command in config.quickSellCommands)
                cmd.AddChatCommand(command.Key, this, nameof(MarketSellCommand));
            foreach (var command in config.quickListCommands)
                cmd.AddChatCommand(command.Key, this, nameof(MarketListCommand));
            foreach (var command in config.depositCommands)
                cmd.AddChatCommand(command.Key, this, nameof(DepositCommand));
            foreach (var command in config.shopOpenCommands)
                cmd.AddChatCommand(command.Key, this, nameof(OpenShopCommand));
            foreach (var command in config.stockOpenCommands)
                cmd.AddChatCommand(command.Key, this, nameof(OpenStockCommand));
            permission.RegisterPermission("shoppystock.admin", this);
            permission.RegisterPermission("shoppystock.hidetransfer", this);
            AddCovalenceCommand(config.adminCommand, nameof(CurrencyAdminCommand));
            AddCovalenceCommand("updateprices", nameof(UpdatePricesCommand));
            if (privFeatures)
            {
                cmd.AddChatCommand("additems", this, nameof(AddStockItemsCommand));
                cmd.AddChatCommand("rptop", this, nameof(RPTopTempCommand));
                cmd.AddChatCommand("goldtop", this, nameof(GoldTopTempCommand));
            }
            GeneratePopUpConfig();
            TryPatchingConfig();
            if (config.debug) Puts("[DEBUG] Plugin init finished.");
            /*int days = 0;
            Dictionary<string, long> summedItems= new Dictionary<string, long>();
            foreach (var day in data.stockMarkets["rp"].stats.globalDailyItems)
            {
                if (day.Value.Count < 10) continue;
                foreach (var item in day.Value)
                {
                    summedItems.TryAdd(item.Key, 0);
                    summedItems[item.Key] += item.Value;
                }
                days++;
            }
            Puts($"Days: {days}");
            foreach (var item in summedItems)
                Puts($"Item {item.Key}: {item.Value / days} daily, {item.Value / days / 48} per tick");*/
        }

        private void Unload()
        {
            SaveData();
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "Market_CreateOfferUI");
                CuiHelper.DestroyUi(player, "Market_AnchorUI");
                CuiHelper.DestroyUi(player, "Market_MainUI");
                CuiHelper.DestroyUi(player, "Market_CurrencyInputUI");
            }
            foreach (var mailbox in mailboxes)
            {
                if (mailbox.Value != null)
                {
                    if (mailbox.Key != null && !shopCache[mailbox.Key].stockMarket.request.buyRequest)
                        foreach (var item in mailbox.Value.inventory.itemList.ToList())
                            if (!item.MoveToContainer(mailbox.Key.inventory.containerMain))
                                item.MoveToContainer(mailbox.Key.inventory.containerBelt);
                    mailbox.Value.Kill();
                }
            }
            foreach (var storage in boxes)
            {
                if (storage.Value != null)
                {
                    if (storage.Key != null)
                        foreach (var item in storage.Value.inventory.itemList.ToList())
                            if (!item.MoveToContainer(storage.Key.inventory.containerMain))
                                item.MoveToContainer(storage.Key.inventory.containerBelt);
                    storage.Value.Kill();
                }
            }
        }

        private void TryPatchingConfig()
        {
            bool changes = false;
            foreach (var shop in config.quickListCommands.ToList())
            {
                if (!config.shops.ContainsKey(shop.Value))
                {
                    PrintWarning($"Command {shop.Key} had unassigned shop with name {shop.Value}. It has been replaced with first shop in config.");
                    config.quickListCommands[shop.Key] = config.shops.FirstOrDefault().Key;
                    changes = true;
                }
            }
            foreach (var shop in config.quickSellCommands.ToList())
            {
                if (!config.shops.ContainsKey(shop.Value))
                {
                    PrintWarning($"Command {shop.Key} had unassigned shop with name {shop.Value}. It has been replaced with first shop in config.");
                    config.quickSellCommands[shop.Key] = config.shops.FirstOrDefault().Key;
                    changes = true;
                }
            }
            foreach (var shop in config.depositCommands.ToList())
            {
                if (!config.shops.ContainsKey(shop.Value))
                {
                    PrintWarning($"Command {shop.Key} had unassigned shop with name {shop.Value}. It has been replaced with first shop in config.");
                    config.depositCommands[shop.Key] = config.shops.FirstOrDefault().Key;
                    changes = true;
                }
            }
            foreach (var shop in config.shops.ToList())
            {
                if (shop.Key.Contains(' '))
                    PrintWarning($"Shop {shop.Key} has spaces in their codename. Change them to underscores or plugin will print errors!");
            }
            if (changes)
            {
                Config.WriteObject(config);
                Puts("Saving changes in main configuration file...");
            }
            changes = false;
            foreach (var shop in config.shops)
            {
                foreach (var categories in data.shops[shop.Key].categories)
                {
                    if (categories.Key.Contains(' '))
                    {
                        changes = true;
                        PrintWarning($"Shop's {shop.Key} category {categories.Key} has spaces in their codename. Change them to underscores or plugin will print errors!");
                    }
                    foreach (var listing in categories.Value.listings)
                    {
                        if (listing.Key.Contains(' '))
                        {
                            changes = true;
                            PrintWarning($"Shop's {shop.Key} category {categories.Key} item {listing.Key} has spaces in their codename. Change them to underscores or plugin will print errors!");
                        }
                        foreach (var command in listing.Value.commands)
                            if (command.Contains('{') && !command.Contains("{userId}") && !command.Contains("{userName}"))
                            {
                                changes = true;
                                PrintWarning($"Your ran command in shop {shop.Key} category {categories.Key} item {listing.Key} contains variable names, that are not valid for the plugin. Only working variables are {{userId}} and {{userName}} and they are case sensitive!");
                            }
                    }
                }
                if (shop.Value.stockConfig.canStockMarket)
                    foreach (var customItem in data.stockMarkets[shop.Key].stockConfig.customItems)
                        if (customItem.Key != $"{customItem.Value.shortname}-{customItem.Value.skin}")
                        {
                            changes = true;
                            PrintWarning($"There is an error in your {shop.Key}'s custom stock item. Their codename is \"{customItem.Key}\" but it should be \"{customItem.Value.shortname}-{customItem.Value.skin}\"");
                        }
            }
            if (changes)
                PrintWarning("It's recommended to check your shop/stock data file. There might be some errors!");

        }

        private void OnNewSave()
        {
            config = Config.ReadObject<PluginConfig>();
            LoadData();
            foreach (var shop in data.shops.ToList())
            {
                if (!config.shops.ContainsKey(shop.Key)) continue;
                if (config.shops[shop.Key].wipeCurrency)
                    data.shops[shop.Key].users.Clear();
                else
                {
                    if (config.shops[shop.Key].percentageTook > 0)
                    {
                        float multiplyValue = config.shops[shop.Key].percentageTook / 100f;
                        foreach (var user in shop.Value.users)
                            TakeCurrency(shop.Key, user.Key, Mathf.RoundToInt(user.Value.currencyAmount * multiplyValue));
                    }
                    foreach (var user in shop.Value.users)
                        data.shops[shop.Key].users[user.Key].wipePurchases.Clear();
                }
                if (config.shops[shop.Key].stockConfig.canStockMarket)
                {
                    if (config.shops[shop.Key].stockConfig.wipeBuyListings)
                        data.stockMarkets[shop.Key].playerData.buyOrders.Clear();
                    if (config.shops[shop.Key].stockConfig.wipeSellListings)
                        data.stockMarkets[shop.Key].playerData.sellOrders.Clear();
                    if (config.shops[shop.Key].stockConfig.wipeBankData)
                        data.stockMarkets[shop.Key].playerData.playerBanks.Clear();
                }
            }
            SaveData();
        }

        /*private object OnPlayerCommand(BasePlayer player)
        {
            if (openedUis.Contains(player) || mailboxes.ContainsKey(player)) return false;
            return null;
        }*/

        private void OnPlayerConnected(BasePlayer player)
        {
            foreach (var shop in data.shops)
                if (shop.Value.users.ContainsKey(player.userID))
                    shop.Value.users[player.userID].username = player.IPlayer.Name;
        }

        /*private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || arg.FullString == null) return null;
            if (openedUis.Contains(player) || mailboxes.ContainsKey(player))
            {
                if (arg.cmd.Name.Contains("UI_") || arg.cmd.Name.Contains("endloot")) return null;
                else return false;
            }
            return null;
        }*/

        private void OnPlayerDisconnected(BasePlayer player) => openedUis.Remove(player);


        private object CanUseMailbox(BasePlayer player, Mailbox mailbox)
        {
            if (mailboxes.ContainsKey(player)) return false;
            else return null;
        }

        private void OnPlayerSleep(BasePlayer player)
        {
            if (!player.IsConnected) return;
            CuiHelper.DestroyUi(player, "Market_CreateOfferUI");
            CuiHelper.DestroyUi(player, "Market_AnchorUI");
            CuiHelper.DestroyUi(player, "Market_MainUI");
            CuiHelper.DestroyUi(player, "Market_CurrencyInputUI");
        }

        private void OnLootEntityEnd(BasePlayer player, Mailbox mailbox)
        {
            if (mailbox.net == null || !mailboxes.ContainsKey(player)) return;
            if (!shopCache[player].stockMarket.request.buyRequest)
                foreach (var item in mailbox.InputContainer.itemList.ToList())
                    if (!item.MoveToContainer(player.inventory.containerMain))
                        item.Drop(player.transform.position, Vector3.zero);
            mailboxes.Remove(player);
            if (mailbox.skinID == 1)
            {
                openedUis.Remove(player);
                CuiHelper.DestroyUi(player, "Market_CreateOfferUI");
            }
            else if (mailbox.skinID == 2)
            {
                openedUis.Remove(player);
                CuiHelper.DestroyUi(player, "Market_MainUI");
            }
            else if (mailbox.skinID == 3)
            {
                openedUis.Remove(player);
                CuiHelper.DestroyUi(player, "Market_CurrencyInputUI");
                if (shopCache[player].stockMarket.quickList)
                {
                    shopCache.Remove(player);
                    mailbox.Kill();
                    return;
                }
                if (shopCache[player].lastVisit == "shop")
                    OpenShopUI(player, shopCache[player].currentShop, shopCache[player].searchValue, shopCache[player].shopCategory, shopCache[player].categoryPage, shopCache[player].page);
                else if (shopCache[player].lastVisit == "stock")
                    OpenStockMarketUI(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.search, shopCache[player].stockMarket.category, shopCache[player].stockMarket.page, shopCache[player].stockMarket.categoryPage);
                mailbox.Kill();
                return;
            }
            else if (mailbox.skinID == 4)
            {
                openedUis.Remove(player);
                CuiHelper.DestroyUi(player, "Market_CreateOfferUI");
                mailbox.Kill();
                return;
            }
            if (shopCache[player].stockMarket.quickList)
            {
                shopCache.Remove(player);
                mailbox.Kill();
                return;
            }
            if (shopCache[player].stockMarket.category == "bank_management")
                shopCache[player].stockMarket.category = "";
            OpenStockMarketUI(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.search, shopCache[player].stockMarket.category, shopCache[player].stockMarket.page, shopCache[player].stockMarket.categoryPage);
            mailbox.Kill();
        }

        private void OnLootEntityEnd(BasePlayer player, BoxStorage storage)
        {
            if (storage.net == null || !boxes.ContainsKey(player)) return;
            if (boxes[player] == null)
            {
                boxes.Remove(player);
                openedUis.Remove(player);
                CuiHelper.DestroyUi(player, "Market_MainUI");
                CuiHelper.DestroyUi(player, "Market_MainUI");
                if (!shopCache.ContainsKey(player))
                    storage.Kill();
                return;
            }
            if (storage != boxes[player]) return;
            foreach (var item in storage.inventory.itemList.ToList())
                if (!item.MoveToContainer(player.inventory.containerMain))
                    item.Drop(player.transform.position, Vector3.zero);
            boxes.Remove(player);
            openedUis.Remove(player);
            CuiHelper.DestroyUi(player, "Market_MainUI");
            if (!shopCache.ContainsKey(player))
            {
                storage.Kill();
                return;
            }
            if (!string.IsNullOrEmpty(shopCache[player].stockMarket.shopName))
                OpenStockMarketUI(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.search, shopCache[player].stockMarket.category, shopCache[player].stockMarket.page, shopCache[player].stockMarket.categoryPage);
            storage.Kill();
        }

        private object OnItemSubmit(Item item, Mailbox mailbox, BasePlayer player)
        {
            if (mailbox.net == null || !mailboxes.ContainsKey(player) || item == null) return null;
            if (mailbox.skinID == 1)
                TryMarketAction(item, player);
            else if (mailbox.skinID == 2)
                TryAddToBank(item, player);
            else if (mailbox.skinID == 3)
                TryAddCurrency(item, player);
            else if (mailbox.skinID == 4)
                TryAddItemEntry(item, player);
            return false;
        }

        private void MarketCommand(BasePlayer player)
        {
            if (IsBlocked(player))
            {
                PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("ShopRaidBlocked", player.UserIDString), config.popUpFontSize, config.popUpLength);
                EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                return;
            }
            UpdateStockItemNames(player);
            if (config.openInCached && shopCache.ContainsKey(player))
            {
                CuiHelper.DestroyUi(player, "Market_AnchorUI");
                if (shopCache[player].lastVisit == "shop")
                    OpenShopUI(player, shopCache[player].currentShop, shopCache[player].searchValue, shopCache[player].shopCategory, shopCache[player].categoryPage, shopCache[player].page);
                else if (shopCache[player].stockMarket.shopName != "")
                {
                    if (shopCache[player].stockMarket.category == "bank_management")
                        shopCache[player].stockMarket.category = "";
                    OpenStockMarketUI(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.search, shopCache[player].stockMarket.category, shopCache[player].stockMarket.page, shopCache[player].stockMarket.categoryPage);
                }
                else
                    OpenShopSelectUI(player);
            }
            else
                OpenShopSelectUI(player);
        }

        private void MarketSellCommand(BasePlayer player, string command)
        {
            if (IsBlocked(player))
            {
                PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("ShopRaidBlocked", player.UserIDString), config.popUpFontSize, config.popUpLength);
                EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                return;
            }
            UpdateStockItemNames(player);
            string commandToLower = command.ToLower();
            foreach (var sellCommand in config.quickSellCommands)
                if (sellCommand.Key.ToLower() == commandToLower)
                {
                    string shopKey = sellCommand.Value;
                    if (!data.stockMarkets.ContainsKey(shopKey) || !config.shops.ContainsKey(shopKey) || !config.shops[shopKey].stockConfig.canStockMarket)
                    {
                        PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("NotValidShopAssigned", player.UserIDString, shopKey), config.popUpFontSize, config.popUpLength);
                        return;
                    }
                    shopCache.Remove(player);
                    OpenSellUI(player, shopKey);
                    break;
                }
        }

        private void MarketListCommand(BasePlayer player, string command)
        {
            if (IsBlocked(player))
            {
                PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("ShopRaidBlocked", player.UserIDString), config.popUpFontSize, config.popUpLength);
                EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                return;
            }
            UpdateStockItemNames(player);
            foreach (var listCommand in config.quickListCommands)
                if (listCommand.Key == command)
                {
                    shopCache.Remove(player);
                    shopCache.Add(player, new ShopCache());
                    shopCache[player].stockMarket.quickList = true;
                    shopCache[player].stockMarket.shopName = listCommand.Value;
                    shopCache[player].stockMarket.request.buyRequest = false;
                    CreateBuySellRequest(player, listCommand.Value, false);
                    break;
                }
        }

        private void DepositCommand(BasePlayer player, string command)
        {
            if (IsBlocked(player))
            {
                PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("ShopRaidBlocked", player.UserIDString), config.popUpFontSize, config.popUpLength);
                EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                return;
            }
            foreach (var listCommand in config.depositCommands)
                if (listCommand.Key == command)
                {
                    shopCache.Remove(player);
                    shopCache.Add(player, new ShopCache());
                    shopCache[player].currentShop = listCommand.Value;
                    shopCache[player].stockMarket.quickList = true;
                    OpenDepositMoneyUI(player, listCommand.Value);
                }
        }

        private void OpenShopCommand(BasePlayer player, string command)
        {
            string linkedShop = config.shopOpenCommands[command];
            shopCache[player] = new ShopCache();
            shopCache[player].currentShop = linkedShop;
            OpenShopUI(player, linkedShop);
        }

        private void OpenStockCommand(BasePlayer player, string command)
        {
            string linkedShop = config.stockOpenCommands[command];
            shopCache[player] = new ShopCache();
            shopCache[player].currentShop = linkedShop;
            shopCache[player].stockMarket.shopName = linkedShop;
            OpenStockMarketUI(player, linkedShop);
        }

        private void AddStockItemsCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            if (args.Length == 0) return;
            AddCustomStockItems(player, args[0]);
        }

        private void RPTopTempCommand(BasePlayer player)
        {
            StringBuilder text = new StringBuilder();
            int counter = 1;
            text.AppendLine($"<color=#5c81ed>TOP 30 RP</color>:");
            foreach (var topPlayer in data.shops["rp"].users.OrderByDescending(x => x.Value.currencyAmount).Take(30))
            {
                string color = "5c81ed";
                if (counter == 1) color = "FFD700";
                else if (counter == 2) color = "C0C0C0";
                else if (counter == 3) color = "CD7F32";
                text.AppendLine($"<color=#{color}>{counter}</color>. {topPlayer.Value.username}: {topPlayer.Value.currencyAmount} RP");
                counter++;
            }
            SendReply(player, text.ToString());
        }

        private void GoldTopTempCommand(BasePlayer player)
        {
            StringBuilder text = new StringBuilder();
            int counter = 1;
            text.AppendLine($"<color=#5c81ed>TOP 30 Gold</color>:");
            foreach (var topPlayer in data.shops["gold"].users.OrderByDescending(x => x.Value.currencyAmount).Take(30))
            {
                string color = "5c81ed";
                if (counter == 1) color = "FFD700";
                else if (counter == 2) color = "C0C0C0";
                else if (counter == 3) color = "CD7F32";
                text.AppendLine($"<color=#{color}>{counter}</color>. {topPlayer.Value.username}: {topPlayer.Value.currencyAmount} Gold");
                counter++;
            }
            SendReply(player, text.ToString());
        }

        private void UpdatePricesCommand(IPlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.Id, "shoppystock.admin"))
            {
                player.Message(Lang("NoAdminPermission", player.Id));
                return;
            }
            if (args.Length == 0)
                player.Message("Usage: updateprices <shopKey>");
            else if (args.Length >= 1)
            {
                if (!config.shops.ContainsKey(args[0]))
                    player.Message($"Shop {args[0]} not found! Usage: updateprices <shopKey>");
                else if (!config.shops[args[0]].stockConfig.canStockMarket)
                    player.Message($"Shop {args[0]} has disabled stock market!");
                else
                    UpdateStockPrices(args[0]);
            }
        }

        [ConsoleCommand("UI_ShoppyStock")]
        private void ShoppyStockConsoleCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (config.debug) Puts($"[DEBUG] {player.displayName} ran ShoppyStock command: {arg.cmd.FullName} {arg.FullString}");
            if (config.uiCooldown > 0)
            {
                if (lastUsage.ContainsKey(player))
                    if ((DateTime.Now - lastUsage[player]).TotalSeconds < config.uiCooldown) return;
                lastUsage[player] = DateTime.Now;
            }
            if (player.IsSleeping()) return;
            if (arg.Args[0] == "close")
            {
                CuiHelper.DestroyUi(player, "Market_AnchorUI");
                shopNpc.Remove(player);
                stockPosition.Remove(player);
                openedUis.Remove(player);
            }
            else if (arg.Args[0] == "closeStockInfo")
            {
                CuiHelper.DestroyUi(player, "Market_StockOfferUI");
                stockPosition.Remove(player);
            }
            else if (arg.Args[0] == "closeTransferInfo")
                CuiHelper.DestroyUi(player, "Market_TransferUI");
            else if (arg.Args[0] == "closePurchaseInfo")
                CuiHelper.DestroyUi(player, "Market_PurchaseUI");
            else if (arg.Args[0] == "shops")
                OpenShopSelectUI(player);
            else if (arg.Args[0] == "open")
                OpenShopUI(player, arg.Args[1]);
            else if (arg.Args[0] == "openStock")
                OpenStockMarketUI(player, arg.Args[1]);
            else if (arg.Args[0] == "category")
                OpenShopUI(player, shopCache[player].currentShop, "", arg.Args[1], shopCache[player].categoryPage);
            else if (arg.Args[0] == "affordable")
            {
                if (shopCache[player].affordableOnly)
                    shopCache[player].affordableOnly = false;
                else
                    shopCache[player].affordableOnly = true;
                OpenShopUI(player, shopCache[player].currentShop, shopCache[player].searchValue, shopCache[player].shopCategory, shopCache[player].categoryPage);
            }
            else if (arg.Args[0] == "search" && arg.Args.Length >= 2)
            {
                string searchPhrase = arg.Args[1].Replace("\\", "");
                OpenShopUI(player, shopCache[player].currentShop, searchPhrase, shopCache[player].shopCategory, shopCache[player].categoryPage);
            }
            else if (arg.Args[0] == "page")
                OpenShopUI(player, shopCache[player].currentShop, shopCache[player].searchValue, shopCache[player].shopCategory, shopCache[player].categoryPage, Convert.ToInt32(arg.Args[1]));
            else if (arg.Args[0] == "categoryPage")
                OpenShopUI(player, shopCache[player].currentShop, shopCache[player].searchValue, shopCache[player].shopCategory, Convert.ToInt32(arg.Args[1]), shopCache[player].page);
            else if (arg.Args[0] == "addItem")
            {
                if (arg.Args[1] == "add")
                    OpenCreateItemEntryUI(player, arg.Args[2], arg.Args[3], arg.Args[4] == "1");
                else if (arg.Args[1] == "var1")
                {
                    if (addingItemsCache[player].stockMarket)
                    {
                        float value;
                        string replaced = arg.Args[2].Replace(",", ".");
                        if (float.TryParse(replaced, out value))
                        {
                            addingItemsCache[player].var1 = value;
                            OpenCreateItemEntryUI(player, addingItemsCache[player].shopName, addingItemsCache[player].category, addingItemsCache[player].stockMarket);
                        }
                    }
                    else
                    {
                        int value;
                        if (int.TryParse(arg.Args[2], out value))
                        {
                            addingItemsCache[player].var1 = value;
                            OpenCreateItemEntryUI(player, addingItemsCache[player].shopName, addingItemsCache[player].category, addingItemsCache[player].stockMarket);
                        }
                    }
                }
                else if (arg.Args[1] == "var2")
                {
                    float value;
                    string replaced = arg.Args[2].Replace(",", ".");
                    if (float.TryParse(replaced, out value))
                    {
                        addingItemsCache[player].var2 = value;
                        OpenCreateItemEntryUI(player, addingItemsCache[player].shopName, addingItemsCache[player].category, addingItemsCache[player].stockMarket);
                    }
                }
                else if (arg.Args[1] == "var3")
                {
                    int value;
                    if (int.TryParse(arg.Args[2], out value))
                    {
                        addingItemsCache[player].var3 = value;
                        OpenCreateItemEntryUI(player, addingItemsCache[player].shopName, addingItemsCache[player].category, addingItemsCache[player].stockMarket);
                    }
                }
            }
            else if (arg.Args[0] == "buy")
            {
                string listingKey = arg.Args[1];
                string shopName = shopCache[player].currentShop;
                string category = shopCache[player].shopCategory;
                string search = shopCache[player].searchValue;
                ListingData listing = null;
                if (config.debug) Puts($"[DEBUG] Starting buy request.");
                if (category == "" || search != "")
                {
                    foreach (var cat in data.shops[shopName].categories)
                    {
                        bool found = false;
                        foreach (var item in cat.Value.listings)
                            if (item.Key == listingKey)
                            {
                                listing = item.Value;
                                found = true;
                                break;
                            }
                        if (found)
                            break;
                    }
                }
                else
                {
                    foreach (var item in data.shops[shopName].categories[category].listings)
                        if (item.Key == listingKey)
                        {
                            listing = item.Value;
                            break;
                        }
                }
                if (listing == null) return;
                float percentageDiscount = 0;
                foreach (var discount in config.shops[shopName].discounts)
                    if (permission.UserHasPermission(player.UserIDString, discount.Key))
                    {
                        percentageDiscount = discount.Value;
                        break;
                    }
                foreach (var discount in data.shops[shopName].categories[category].discounts)
                    if (permission.UserHasPermission(player.UserIDString, discount.Key))
                    {
                        if (config.sumDiscounts)
                            percentageDiscount += discount.Value;
                        else
                            percentageDiscount = Mathf.Max(percentageDiscount, discount.Value);
                        break;
                    }
                if (listing.price > 0)
                {
                    int priceDiscounted = listing.price;
                    int itemDiscountedPrice = listing.price;
                    foreach (var discount in listing.discounts)
                        if (permission.UserHasPermission(player.UserIDString, discount.Key))
                        {
                            priceDiscounted = discount.Value;
                            break;
                        }
                    if (percentageDiscount > 0 || priceDiscounted != listing.price)
                    {
                        if (config.sumDiscounts)
                            itemDiscountedPrice = Mathf.RoundToInt(priceDiscounted - (listing.price / 100f * percentageDiscount));
                        else
                            itemDiscountedPrice = Mathf.Min(priceDiscounted, Mathf.RoundToInt(listing.price - (listing.price / 100f * percentageDiscount)));
                    }
                    int playerCurrency = GetCurrencyAmount(shopName, player);
                    if (playerCurrency < itemDiscountedPrice)
                    {
                        PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("NotEnoughCurrency", player.UserIDString), config.popUpFontSize, config.popUpLength);
                        EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                        return;
                    }
                }
                if (config.debug) Puts($"[DEBUG] Currency check finished.");
                if (listing.dailyBuy != 0)
                {
                    string date = DateTime.Now.ToString("dd/MM/yyyy");
                    if (data.shops[shopCache[player].currentShop].users.ContainsKey(player.userID) && data.shops[shopCache[player].currentShop].users[player.userID].dailyPurchases.ContainsKey(date) && data.shops[shopCache[player].currentShop].users[player.userID].dailyPurchases[date].ContainsKey(listingKey) && data.shops[shopCache[player].currentShop].users[player.userID].dailyPurchases[date][listingKey] >= listing.dailyBuy)
                    {
                        PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("DailyLimitReached", player.UserIDString), config.popUpFontSize, config.popUpLength);
                        EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                        return;
                    }
                }
                if (config.debug) Puts($"[DEBUG] Daily buy check finished.");
                if (listing.wipeBuy != 0)
                {
                    if (data.shops[shopCache[player].currentShop].users.ContainsKey(player.userID) && data.shops[shopCache[player].currentShop].users[player.userID].wipePurchases.ContainsKey(listingKey) && data.shops[shopCache[player].currentShop].users[player.userID].wipePurchases[listingKey] >= listing.wipeBuy)
                    {
                        PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("WipeLimitReached", player.UserIDString), config.popUpFontSize, config.popUpLength);
                        EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                        return;
                    }
                }
                if (config.debug) Puts($"[DEBUG] Wipe buy check finished.");
                if (listing.cooldown != 0)
                {
                    if (data.shops[shopCache[player].currentShop].users.ContainsKey(player.userID) && data.shops[shopCache[player].currentShop].users[player.userID].cooldowns.ContainsKey(listingKey))
                    {
                        if (DateTime.Now < data.shops[shopCache[player].currentShop].users[player.userID].cooldowns[listingKey])
                        {
                            string timeFormat = (DateTime.Now - data.shops[shopCache[player].currentShop].users[player.userID].cooldowns[listingKey]).ToString(@"hh\:mm\:ss");
                            PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("PurchaseCooldown", player.UserIDString, timeFormat), config.popUpFontSize, config.popUpLength);
                            EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                            return;
                        }
                    }
                }
                if (config.debug) Puts($"[DEBUG] Cooldown check finished.");
                OpenShopPurchaseUI(player, shopName, listing, listingKey);
            }
            else if (arg.Args[0] == "setAmount" && arg.Args.Length == 2)
            {
                int amount;
                if (!int.TryParse(arg.Args[1], out amount)) return;
                shopCache[player].currentListings.amount = amount;
                OpenShopPurchaseUI(player, shopCache[player].currentListings.shopName, shopCache[player].currentListings.listing, shopCache[player].currentListings.listingKey, shopCache[player].currentListings.amount);
            }
            else if (arg.Args[0] == "cancelPurchase")
                CuiHelper.DestroyUi(player, "Market_PurchaseUI");
            else if (arg.Args[0] == "acceptPurchase")
            {
                string otherPlugin = config.shops[shopCache[player].currentShop].otherPluginCurrency.ToLower();
                int price = shopCache[player].currentListings.listing.price * shopCache[player].currentListings.amount;
                float percentageDiscount = 0;
                foreach (var discount in config.shops[shopCache[player].currentShop].discounts)
                    if (permission.UserHasPermission(player.UserIDString, discount.Key))
                    {
                        percentageDiscount = discount.Value;
                        break;
                    }
                foreach (var discount in data.shops[shopCache[player].currentShop].categories[shopCache[player].shopCategory].discounts)
                    if (permission.UserHasPermission(player.UserIDString, discount.Key))
                    {
                        if (config.sumDiscounts)
                            percentageDiscount += discount.Value;
                        else
                            percentageDiscount = Mathf.Max(percentageDiscount, discount.Value);
                        break;
                    }
                int itemDiscountedPrice = 0;
                if (price > 0)
                {
                    int priceDiscounted = price;
                    itemDiscountedPrice = price;
                    foreach (var discount in shopCache[player].currentListings.listing.discounts)
                        if (permission.UserHasPermission(player.UserIDString, discount.Key))
                        {
                            priceDiscounted = discount.Value;
                            break;
                        }
                    if (percentageDiscount > 0 || priceDiscounted != price)
                    {
                        if (config.sumDiscounts)
                            itemDiscountedPrice = Mathf.RoundToInt(priceDiscounted - (price / 100f * percentageDiscount));
                        else
                            itemDiscountedPrice = Mathf.Min(priceDiscounted, Mathf.RoundToInt(price - (price / 100f * percentageDiscount)));

                    }
                }

                if (shopCache[player].currentListings.listing.pricePerPurchaseMultiplier != 1)
                {
                    int purchases = 0;
                    if (shopCache[player].currentListings.listing.multiplyPricePerDaily)
                    {
                        string date = DateTime.Now.ToString("dd/MM/yyyy");
                        if (data.shops[shopCache[player].currentShop].users.ContainsKey(player.userID) && data.shops[shopCache[player].currentShop].users[player.userID].dailyPurchases.ContainsKey(date) && data.shops[shopCache[player].currentShop].users[player.userID].dailyPurchases[date].ContainsKey(shopCache[player].currentListings.listingKey))
                            purchases = data.shops[shopCache[player].currentShop].users[player.userID].dailyPurchases[date][shopCache[player].currentListings.listingKey];
                    }
                    else
                    {
                        if (data.shops[shopCache[player].currentShop].users.ContainsKey(player.userID) && data.shops[shopCache[player].currentShop].users[player.userID].wipePurchases.ContainsKey(shopCache[player].currentListings.listingKey))
                            purchases = data.shops[shopCache[player].currentShop].users[player.userID].wipePurchases[shopCache[player].currentListings.listingKey];
                    }
                    float power = Mathf.Pow(shopCache[player].currentListings.listing.pricePerPurchaseMultiplier, purchases);
                    itemDiscountedPrice = Mathf.RoundToInt(itemDiscountedPrice * power);
                }
                int playerCurrency = GetCurrencyAmount(shopCache[player].currentShop, player);
                if (playerCurrency < itemDiscountedPrice)
                {
                    PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("NotEnoughCurrency", player.UserIDString), config.popUpFontSize, config.popUpLength);
                    EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                    return;
                }
                if (config.debug) Puts($"[DEBUG] Currency check finished.");
                if (itemDiscountedPrice > 0 && !TakeCurrency(shopCache[player].currentShop, player, itemDiscountedPrice)) return;
                int sumItemAmount = shopCache[player].currentListings.listing.amount * shopCache[player].currentListings.amount;
                int cachedSum = sumItemAmount;
                int maxStackSize = shopCache[player].currentListings.listing.amount;
                if (shopCache[player].currentListings.listing.commands.Count == 0)
                    maxStackSize = ItemManager.FindItemDefinition(shopCache[player].currentListings.listing.shortname).stackable;
                if (config.debug) Puts($"[DEBUG] Starting looping amount.");
                while (sumItemAmount > 0)
                {
                    int createAmount = maxStackSize;
                    if (sumItemAmount < maxStackSize)
                        createAmount = sumItemAmount;
                    if (shopCache[player].currentListings.listing.commands.Count == 0)
                    {

                        Item item = null;
                        if (shopCache[player].currentListings.listing.blueprint)
                        {
                            item = ItemManager.CreateByName("blueprintbase", createAmount, shopCache[player].currentListings.listing.skin);
                            item.blueprintTarget = ItemManager.FindItemDefinition(shopCache[player].currentListings.listing.shortname).itemid;
                        }
                        else
                            item = ItemManager.CreateByName(shopCache[player].currentListings.listing.shortname, createAmount, shopCache[player].currentListings.listing.skin);
                        if (shopCache[player].currentListings.listing.itemName != "")
                            item.name = shopCache[player].currentListings.listing.itemName;
                        if (!item.MoveToContainer(player.inventory.containerMain))
                            item.MoveToContainer(player.inventory.containerBelt);
                    }
                    else
                        foreach (var command in shopCache[player].currentListings.listing.commands)
                        {
                            string commandFormat = command.Replace("{userName}", player.displayName).Replace("{userId}", player.UserIDString).Replace("{username}", player.displayName).Replace("{userid}", player.UserIDString).Replace("{userPosX}", player.transform.position.x.ToString()).Replace("{userPosY}", player.transform.position.y.ToString()).Replace("{userPosZ}", player.transform.position.z.ToString());
                            Server.Command(commandFormat);
                        }
                    sumItemAmount -= createAmount;
                }
                if (config.debug) Puts($"[DEBUG] Finished looping amount.");
                if (shopCache[player].currentListings.listing.dailyBuy != 0 || shopCache[player].currentListings.listing.pricePerPurchaseMultiplier != 1)
                {
                    string date = DateTime.Now.ToString("dd/MM/yyyy");
                    data.shops[shopCache[player].currentShop].users.TryAdd(player.userID, new UserData() { username = player.IPlayer.Name });
                    data.shops[shopCache[player].currentShop].users[player.userID].dailyPurchases.TryAdd(date, new Dictionary<string, int>());
                    data.shops[shopCache[player].currentShop].users[player.userID].dailyPurchases[date].TryAdd(shopCache[player].currentListings.listingKey, 0);
                    data.shops[shopCache[player].currentShop].users[player.userID].dailyPurchases[date][shopCache[player].currentListings.listingKey] += shopCache[player].currentListings.amount;
                }
                if (shopCache[player].currentListings.listing.wipeBuy != 0 || shopCache[player].currentListings.listing.pricePerPurchaseMultiplier != 1)
                {
                    data.shops[shopCache[player].currentShop].users.TryAdd(player.userID, new UserData() { username = player.IPlayer.Name });
                    data.shops[shopCache[player].currentShop].users[player.userID].wipePurchases.TryAdd(shopCache[player].currentListings.listingKey, 0);
                    data.shops[shopCache[player].currentShop].users[player.userID].wipePurchases[shopCache[player].currentListings.listingKey] += shopCache[player].currentListings.amount;
                }
                if (shopCache[player].currentListings.listing.cooldown != 0)
                {
                    data.shops[shopCache[player].currentShop].users.TryAdd(player.userID, new UserData() { username = player.IPlayer.Name });
                    data.shops[shopCache[player].currentShop].users[player.userID].cooldowns.TryAdd(shopCache[player].currentListings.listingKey, DateTime.Now);
                    data.shops[shopCache[player].currentShop].users[player.userID].cooldowns[shopCache[player].currentListings.listingKey] = DateTime.Now + TimeSpan.FromSeconds(shopCache[player].currentListings.listing.cooldown);
                }
                if (config.debug) Puts($"[DEBUG] Limits updated.");
                CurrentListing cachedListing = shopCache[player].currentListings;
                OpenShopUI(player, shopCache[player].currentShop, shopCache[player].searchValue, shopCache[player].shopCategory, shopCache[player].categoryPage, shopCache[player].page);
                string priceFormat = FormatPrice(shopCache[player].currentShop, itemDiscountedPrice);
                string itemName = config.translateItems ? Lang(cachedListing.listingKey, player.UserIDString) : cachedListing.listing.displayName;
                PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("SuccesfullyPurchased", player.UserIDString, cachedSum, itemName, priceFormat), config.popUpFontSize, config.popUpLength);
                EffectNetwork.Send(new Effect("assets/prefabs/misc/casino/slotmachine/effects/payout.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                Interface.CallHook("OnShopItemPurchased", player, shopCache[player].currentShop, itemDiscountedPrice);
                if (config.enableLogs)
                    Puts($"Player {player.displayName} ({player.UserIDString}) purchased x{cachedSum} {cachedListing.listing.displayName} for {priceFormat}.");
            }
            else if (arg.Args[0] == "bankPage")
                OpenBankManagement(player, arg.Args[1], Convert.ToInt32(arg.Args[2]));
            else if (arg.Args[0] == "bankDepositAll")
                DepositInventoryToBank(player, arg.Args[1]);
            else if (arg.Args[0] == "bankWithdraw")
                WithdrawBank(player, arg.Args[1], arg.Args[2], arg.Args[3]);
            else if (arg.Args[0] == "depositMoney")
                OpenDepositMoneyUI(player, arg.Args[1]);
            else if (arg.Args[0] == "transfer")
            {
                if (arg.Args.Length == 1)
                    OpenTransferUI(player);
                else
                {
                    if (arg.Args[1] == "page")
                    {
                        string search = arg.Args.Length > 3 ? arg.Args[3] : "";
                        OpenTransferUI(player, Convert.ToInt32(arg.Args[2]), search);
                    }
                    else if (arg.Args[1] == "search" && arg.Args.Length >= 3)
                    {
                        string searchPhrase = arg.Args[2].Replace("\\", "");
                        OpenTransferUI(player, 1, searchPhrase);
                    }
                    else if (arg.Args[1] == "online")
                    {
                        if (!shopCache[player].transferOnline)
                            shopCache[player].transferOnline = true;
                        else
                            shopCache[player].transferOnline = false;
                        string search = arg.Args.Length > 3 ? arg.Args[3] : "";
                        OpenTransferUI(player, Convert.ToInt32(arg.Args[2]), search);
                    }
                    else if (arg.Args[1] == "user")
                    {
                        OpenUserTransferUI(player, arg.Args[2]);
                    }
                    else if (arg.Args[1] == "currency")
                    {
                        string newCurrency = arg.Args[3];
                        bool next = false;
                        foreach (var shop in config.shops)
                        {
                            if (next && shop.Value.canTransfer)
                            {
                                newCurrency = shop.Key;
                                break;
                            }
                            if (!next && arg.Args[3] == shop.Key)
                                next = true;
                        }
                        if (newCurrency == arg.Args[3] && next)
                            newCurrency = config.shops.Where(x => x.Value.canTransfer).First().Key;
                        OpenUserTransferUI(player, arg.Args[2], newCurrency);
                    }
                    else if (arg.Args[1] == "setAmount" && arg.Args.Length >= 5)
                    {
                        int newAmount;
                        if (int.TryParse(arg.Args[4], out newAmount))
                            OpenUserTransferUI(player, arg.Args[2], arg.Args[3], newAmount);
                    }
                    else if (arg.Args[1] == "increase" || arg.Args[1] == "decrease")
                    {
                        int amount;
                        if (int.TryParse(arg.Args[4], out amount))
                        {
                            int newAmount = 10;
                            if (amount >= 1000000)
                                newAmount = 100000;
                            else if (amount >= 100000)
                                newAmount = 10000;
                            else if (amount >= 10000)
                                newAmount = 1000;
                            else if (amount >= 1000)
                                newAmount = 100;
                            if (arg.Args[1] == "increase")
                                OpenUserTransferUI(player, arg.Args[2], arg.Args[3], amount + newAmount);
                            else if (arg.Args[1] == "decrease")
                                OpenUserTransferUI(player, arg.Args[2], arg.Args[3], amount - newAmount);
                        }
                    }
                    else if (arg.Args[1] == "cancel")
                    {
                        CuiHelper.DestroyUi(player, "Market_TransferUI");
                    }
                    else if (arg.Args[1] == "accept")
                    {
                        ulong userId = Convert.ToUInt64(arg.Args[2]);
                        string shopName = arg.Args[3];
                        int amount = Convert.ToInt32(arg.Args[4]);
                        IPlayer iPlayer = covalence.Players.FindPlayerById(arg.Args[2]);
                        if (iPlayer == null || !TakeCurrency(shopName, player.userID, amount)) return;
                        data.shops[shopName].users.TryAdd(userId, new UserData() { username = iPlayer.Name });
                        GiveCurrency(shopName, userId, amount);
                        string priceFormat = FormatPrice(shopName, amount);
                        PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("CurrencyTransfered", player.UserIDString, iPlayer.Name, priceFormat), config.popUpFontSize, config.popUpLength);
                        BasePlayer onlineReceiver = BasePlayer.FindByID(ulong.Parse(arg.Args[2]));
                        if (onlineReceiver != null)
                            PopUpAPI?.Call("ShowPopUp", onlineReceiver, "Market", Lang("CurrencyReceived", onlineReceiver.UserIDString, player.displayName, priceFormat), config.popUpFontSize, config.popUpLength);
                        EffectNetwork.Send(new Effect("assets/prefabs/misc/easter/painted eggs/effects/eggpickup.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                        if (config.enableLogs)
                            Puts($"Player {player.displayName} ({player.UserIDString}) transfered {priceFormat} to {iPlayer.Name} ({iPlayer.Id}).");
                        CuiHelper.DestroyUi(player, "Market_TransferUI");
                    }
                }
            }
            else if (arg.Args[0] == "stock")
            {
                if (arg.Args.Length == 1)
                    OpenStockMarketSelectUI(player);
                else
                {
                    if (arg.Args[1] == "page")
                        OpenStockMarketUI(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.search, shopCache[player].stockMarket.category, Convert.ToInt32(arg.Args[2]), shopCache[player].stockMarket.categoryPage);
                    else if (arg.Args[1] == "categoryPage")
                        OpenStockMarketUI(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.search, shopCache[player].stockMarket.category, shopCache[player].stockMarket.page, Convert.ToInt32(arg.Args[2]));
                    else if (arg.Args[1] == "category")
                        OpenStockMarketUI(player, shopCache[player].stockMarket.shopName, "", arg.Args[2], 1, shopCache[player].stockMarket.categoryPage);
                    else if (arg.Args[1] == "favourite")
                        ChangeFavouriteItem(player, shopCache[player].stockMarket.shopName, arg.Args[2]);
                    else if (arg.Args[1] == "sellToServer")
                    {
                        shopCache[player].stockMarket.buySellOrders = ' ';
                        if (shopCache[player].stockMarket.sellToServerOnly)
                            shopCache[player].stockMarket.sellToServerOnly = false;
                        else
                            shopCache[player].stockMarket.sellToServerOnly = true;
                        OpenStockMarketUI(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.search, shopCache[player].stockMarket.category, shopCache[player].stockMarket.page, shopCache[player].stockMarket.categoryPage);
                    }
                    else if (arg.Args[1] == "buySellOrders")
                    {
                        shopCache[player].stockMarket.sellToServerOnly = false;
                        if (shopCache[player].stockMarket.buySellOrders == ' ')
                            shopCache[player].stockMarket.buySellOrders = 'B';
                        else if (shopCache[player].stockMarket.buySellOrders == 'B')
                            shopCache[player].stockMarket.buySellOrders = 'S';
                        else if (shopCache[player].stockMarket.buySellOrders == 'S')
                            shopCache[player].stockMarket.buySellOrders = ' ';
                        OpenStockMarketUI(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.search, shopCache[player].stockMarket.category, shopCache[player].stockMarket.page, shopCache[player].stockMarket.categoryPage);
                    }
                    else if (arg.Args[1] == "search" && arg.Args.Length >= 3)
                    {
                        string searchPhrase = arg.Args[2].Replace("\\", "");
                        OpenStockMarketUI(player, shopCache[player].stockMarket.shopName, searchPhrase, shopCache[player].stockMarket.category, shopCache[player].stockMarket.page, shopCache[player].stockMarket.categoryPage);
                    }
                    else if (arg.Args[1] == "buy")
                        OpenStockItemInfoUI(player, shopCache[player].stockMarket.shopName, arg.Args[2]);
                    else if (arg.Args[1] == "buyOffers")
                        OpenStockItemInfoUI(player, shopCache[player].stockMarket.shopName, arg.Args[2], true);
                    else if (arg.Args[1] == "sellOffers")
                        OpenStockItemInfoUI(player, shopCache[player].stockMarket.shopName, arg.Args[2]);
                    else if (arg.Args[1] == "setAlert" && arg.Args.Length > 3)
                    {
                        float target;
                        if (float.TryParse(arg.Args[3], out target))
                        {
                            data.stockMarkets[shopCache[player].stockMarket.shopName].alertData.TryAdd(player.userID, new Dictionary<string, StockAlertData>());
                            data.stockMarkets[shopCache[player].stockMarket.shopName].alertData[player.userID].TryAdd(arg.Args[2], new StockAlertData());
                            data.stockMarkets[shopCache[player].stockMarket.shopName].alertData[player.userID][arg.Args[2]].alertPrice = target;
                            OpenStockItemInfoUI(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.listing.key, shopCache[player].stockMarket.listing.buyOffers, shopCache[player].stockMarket.listing.selected, shopCache[player].stockMarket.listing.index, shopCache[player].stockMarket.listing.amount, shopCache[player].stockMarket.listing.page);
                        }
                    }
                    else if (arg.Args[1] == "setInstaSell" && arg.Args.Length > 3)
                    {
                        float target;
                        if (float.TryParse(arg.Args[3], out target))
                        {
                            data.stockMarkets[shopCache[player].stockMarket.shopName].alertData.TryAdd(player.userID, new Dictionary<string, StockAlertData>());
                            data.stockMarkets[shopCache[player].stockMarket.shopName].alertData[player.userID].TryAdd(arg.Args[2], new StockAlertData());
                            data.stockMarkets[shopCache[player].stockMarket.shopName].alertData[player.userID][arg.Args[2]].instaSellPrice = target;
                            OpenStockItemInfoUI(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.listing.key, shopCache[player].stockMarket.listing.buyOffers, shopCache[player].stockMarket.listing.selected, shopCache[player].stockMarket.listing.index, shopCache[player].stockMarket.listing.amount, shopCache[player].stockMarket.listing.page);
                        }
                    }
                    else if (arg.Args[1] == "sellFromBank")
                        TrySellFromBank(player.userID, shopCache[player].stockMarket.shopName, arg.Args[2]);
                    else if (arg.Args[1] == "openSell")
                        OpenSellUI(player, shopCache[player].stockMarket.shopName);
                    else if (arg.Args[1] == "timestamp")
                    {
                        bool next = false;
                        if (shopCache[player].stockMarket.listing.timestamp == 0)
                            shopCache[player].stockMarket.listing.timestamp = config.timestamps.First().Key;
                        int last = config.timestamps.Last().Key;
                        bool changed = false;
                        foreach (var timestamp in config.timestamps)
                        {
                            if (next && (timestamp.Value == "" || permission.UserHasPermission(player.UserIDString, timestamp.Value)))
                            {
                                shopCache[player].stockMarket.listing.timestamp = timestamp.Key;
                                changed = true;
                                break;
                            }
                            if (shopCache[player].stockMarket.listing.timestamp == timestamp.Key)
                                next = true;
                        }
                        if (!changed)
                            shopCache[player].stockMarket.listing.timestamp = config.timestamps.First().Key;
                        OpenStockItemInfoUI(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.listing.key, shopCache[player].stockMarket.listing.buyOffers, shopCache[player].stockMarket.listing.selected, shopCache[player].stockMarket.listing.index, shopCache[player].stockMarket.listing.amount, shopCache[player].stockMarket.listing.page);
                    }
                    else if (arg.Args[1] == "showBuy")
                    {
                        OpenStockItemInfoUI(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.listing.key, shopCache[player].stockMarket.listing.buyOffers, shopCache[player].stockMarket.listing.selected, Convert.ToInt32(arg.Args[2]), shopCache[player].stockMarket.listing.amount, shopCache[player].stockMarket.listing.page);
                    }
                    else if (arg.Args[1] == "showMyOffers")
                    {
                        if (shopCache[player].stockMarket.listing.hideOwned)
                            shopCache[player].stockMarket.listing.hideOwned = false;
                        else
                            shopCache[player].stockMarket.listing.hideOwned = true;
                        OpenStockItemInfoUI(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.listing.key, shopCache[player].stockMarket.listing.buyOffers, shopCache[player].stockMarket.listing.selected, shopCache[player].stockMarket.listing.index, shopCache[player].stockMarket.listing.amount, shopCache[player].stockMarket.listing.page);
                    }
                    else if (arg.Args[1] == "itemPage")
                    {
                        OpenStockItemInfoUI(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.listing.key, shopCache[player].stockMarket.listing.buyOffers, null, -1, 1, Convert.ToInt32(arg.Args[2]));
                    }
                    else if (arg.Args[1] == "amount" && arg.Args.Length >= 3)
                    {
                        int amount;
                        if (int.TryParse(arg.Args[2], out amount))
                        {
                            if (amount > shopCache[player].stockMarket.listing.selected.item.amount)
                                amount = shopCache[player].stockMarket.listing.selected.item.amount;
                            OpenStockItemInfoUI(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.listing.key, shopCache[player].stockMarket.listing.buyOffers, shopCache[player].stockMarket.listing.selected, shopCache[player].stockMarket.listing.index, amount, shopCache[player].stockMarket.listing.page);
                        }
                    }
                    else if (arg.Args[1] == "buyAction")
                        TryBuyFromStock(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.listing.key, shopCache[player].stockMarket.listing.selected, shopCache[player].stockMarket.listing.amount);
                    else if (arg.Args[1] == "sellAction")
                        TrySellToStock(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.listing.key, shopCache[player].stockMarket.listing.selected, shopCache[player].stockMarket.listing.amount);
                    else if (arg.Args[1] == "closeOffer")
                    {
                        CuiHelper.DestroyUi(player, "Market_StockOfferUI");
                        stockPosition.Remove(player);
                    }
                    else if (arg.Args[1] == "createBuy")
                        CreateBuySellRequest(player, shopCache[player].stockMarket.shopName, true);
                    else if (arg.Args[1] == "createSell")
                        CreateBuySellRequest(player, shopCache[player].stockMarket.shopName, false);
                    else if (arg.Args[1] == "requestAmount" && arg.Args.Length >= 3)
                    {
                        int amount;
                        if (int.TryParse(arg.Args[2], out amount))
                            CreateBuySellRequest(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.request.buyRequest, shopCache[player].stockMarket.request.price, amount);
                    }
                    else if (arg.Args[1] == "requestPrice" && arg.Args.Length >= 3)
                    {
                        float price;
                        string priceString = arg.Args[2].Replace(",", ".");
                        if (float.TryParse(priceString, out price))
                            CreateBuySellRequest(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.request.buyRequest, price, shopCache[player].stockMarket.request.amount);
                    }
                    else if (arg.Args[1] == "refundItem" && arg.Args.Length >= 3)
                    {
                        int index = Convert.ToInt32(arg.Args[2]);
                        OrderData orderData;
                        string[] split = shopCache[player].stockMarket.listing.key.Split('-');
                        string shortname = split[0];
                        ulong skin = Convert.ToUInt64(split[1]);
                        if (shopCache[player].stockMarket.listing.buyOffers)
                            orderData = data.stockMarkets[shopCache[player].stockMarket.shopName].playerData.buyOrders[shortname][skin].Where(x => x.sellerId == player.userID || !x.isCanceled).OrderByDescending(x => x.price).ToList().ElementAt(index);
                        else
                            orderData = data.stockMarkets[shopCache[player].stockMarket.shopName].playerData.sellOrders[shortname][skin].Where(x => x.sellerId == player.userID || !x.isCanceled).OrderBy(x => x.price).ToList().ElementAt(index);
                        if (orderData == null || orderData.sellerId != player.userID || !orderData.isCanceled) return;
                        if (!shopCache[player].stockMarket.listing.buyOffers)
                            TryBuyFromStock(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.listing.key, orderData, orderData.item.amount, false);
                        else
                            RefundBuyRequest(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.listing.key, orderData);
                    }
                    else if (arg.Args[1] == "readdItem" && arg.Args.Length >= 3)
                    {
                        int index = Convert.ToInt32(arg.Args[2]);
                        OrderData orderData;
                        string[] split = shopCache[player].stockMarket.listing.key.Split('-');
                        string shortname = split[0];
                        ulong skin = Convert.ToUInt64(split[1]);
                        if (shopCache[player].stockMarket.listing.buyOffers)
                            orderData = data.stockMarkets[shopCache[player].stockMarket.shopName].playerData.buyOrders[shortname][skin].Where(x => x.sellerId == player.userID || !x.isCanceled).OrderByDescending(x => x.price).ToList().ElementAt(index);
                        else
                            orderData = data.stockMarkets[shopCache[player].stockMarket.shopName].playerData.sellOrders[shortname][skin].Where(x => x.sellerId == player.userID || !x.isCanceled).OrderBy(x => x.price).ToList().ElementAt(index);
                        if (orderData == null || orderData.sellerId != player.userID || !orderData.isCanceled) return;
                        orderData.isCanceled = false;
                        foreach (var stockPlayer in stockPosition.ToList())
                        {
                            if (stockPlayer.Key == null) continue;
                            if (stockPlayer.Key == player) continue;
                            if (stockPlayer.Value.currentItem == "" || stockPlayer.Value.currentItem != shopCache[player].stockMarket.listing.key) continue;
                            OpenStockItemInfoUI(stockPlayer.Key, shopCache[stockPlayer.Key].stockMarket.shopName, shopCache[stockPlayer.Key].stockMarket.listing.key, shopCache[stockPlayer.Key].stockMarket.listing.buyOffers, shopCache[stockPlayer.Key].stockMarket.listing.selected, shopCache[stockPlayer.Key].stockMarket.listing.index, shopCache[stockPlayer.Key].stockMarket.listing.amount, shopCache[stockPlayer.Key].stockMarket.listing.page);
                        }
                        OpenStockItemInfoUI(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.listing.key, shopCache[player].stockMarket.listing.buyOffers, shopCache[player].stockMarket.listing.selected, shopCache[player].stockMarket.listing.index, shopCache[player].stockMarket.listing.amount, shopCache[player].stockMarket.listing.page);
                        if (config.enableLogs)
                            Puts($"Player {player.displayName} ({player.UserIDString}) re-added {shopCache[player].stockMarket.listing.key} listing.");
                        PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("ListingReadded", player.UserIDString), config.popUpFontSize, config.popUpLength);
                        EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/invite_notice.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                    }
                    else if (arg.Args[1] == "cancelListing" && arg.Args.Length >= 3)
                    {
                        int index = Convert.ToInt32(arg.Args[2]);
                        OrderData orderData;
                        string[] split = shopCache[player].stockMarket.listing.key.Split('-');
                        string shortname = split[0];
                        ulong skin = Convert.ToUInt64(split[1]);
                        if (shopCache[player].stockMarket.listing.buyOffers)
                            orderData = data.stockMarkets[shopCache[player].stockMarket.shopName].playerData.buyOrders[shortname][skin].Where(x => x.sellerId == player.userID || !x.isCanceled).OrderByDescending(x => x.price).ToList().ElementAt(index);
                        else
                            orderData = data.stockMarkets[shopCache[player].stockMarket.shopName].playerData.sellOrders[shortname][skin].Where(x => x.sellerId == player.userID || !x.isCanceled).OrderBy(x => x.price).ToList().ElementAt(index);
                        if (orderData == null || orderData.sellerId != player.userID) return;
                        orderData.isCanceled = true;
                        foreach (var stockPlayer in stockPosition.ToList())
                        {
                            if (stockPlayer.Key == null) continue;
                            if (stockPlayer.Key == player) continue;
                            if (stockPlayer.Value.currentItem == "" || stockPlayer.Value.currentItem != shopCache[player].stockMarket.listing.key) continue;
                            OpenStockItemInfoUI(stockPlayer.Key, shopCache[stockPlayer.Key].stockMarket.shopName, shopCache[stockPlayer.Key].stockMarket.listing.key, shopCache[stockPlayer.Key].stockMarket.listing.buyOffers, shopCache[stockPlayer.Key].stockMarket.listing.selected, shopCache[stockPlayer.Key].stockMarket.listing.index, shopCache[stockPlayer.Key].stockMarket.listing.amount, shopCache[stockPlayer.Key].stockMarket.listing.page);
                        }
                        OpenStockItemInfoUI(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.listing.key, shopCache[player].stockMarket.listing.buyOffers, shopCache[player].stockMarket.listing.selected, shopCache[player].stockMarket.listing.index, shopCache[player].stockMarket.listing.amount, shopCache[player].stockMarket.listing.page);
                        if (config.enableLogs)
                            Puts($"Player {player.displayName} ({player.UserIDString}) canceled {shopCache[player].stockMarket.listing.key} listing.");
                        PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("ListingCanceled", player.UserIDString), config.popUpFontSize, config.popUpLength);
                        EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                    }
                    else if (arg.Args[1] == "sellItems" && arg.Args.Length >= 3)
                    {
                        selling = true;
                        string shopName = arg.Args[2];
                        if (!boxes.ContainsKey(player) || boxes[player] == null)
                        {
                            selling = false;
                            return;
                        }
                        int sumPrice = 0;
                        float bonus = 0;
                        if (BonusCore != null)
                            bonus = BonusCore.Call<float>("GetBonus", player, "Other_Price") * 100f;
                        if (config.shops[shopName].stockConfig.sellPriceMultipliers.Count > 0)
                        {
                            foreach (var perm in config.shops[shopName].stockConfig.sellPriceMultipliers)
                            {
                                if (permission.UserHasPermission(player.UserIDString, perm.Key))
                                {
                                    bonus = perm.Value * 100f;
                                    break;
                                }
                            }
                        }
                        string date = DateTime.Now.ToString("dd/MM/yyyy");
                        Dictionary<string, Dictionary<ulong, int>> itemCount = new Dictionary<string, Dictionary<ulong, int>>();
                        Dictionary<string, Dictionary<ulong, int>> sellItemCount = new Dictionary<string, Dictionary<ulong, int>>();
                        Dictionary<string, int> dailyItemCache = new Dictionary<string, int>();
                        foreach (var item in boxes[player].inventory.itemList)
                        {
                            if (!data.stockMarkets[shopName].stockConfig.serverSell.ContainsKey(item.info.shortname) || !data.stockMarkets[shopName].stockConfig.serverSell[item.info.shortname].ContainsKey(item.skin)) continue;
                            if (!data.stockMarkets[shopName].sellCache.ContainsKey(item.info.shortname) || !data.stockMarkets[shopName].sellCache[item.info.shortname].ContainsKey(item.skin))
                                RollPrices(shopName, item.info.shortname, item.skin);
                            ServerSellData ssd = data.stockMarkets[shopName].stockConfig.serverSell[item.info.shortname][item.skin];
                            if (config.enableStockStats && ssd.maxDailySold > 0)
                            {
                                int maxToSell = ssd.maxDailySold;
                                foreach (var perm in ssd.maxDailySoldPerms)
                                {
                                    if (perm.Value > maxToSell && permission.UserHasPermission(player.UserIDString, perm.Key))
                                        maxToSell = perm.Value;
                                }
                                string displayName = ssd.displayName;
                                data.stockMarkets[shopName].stats.playerDailyItems.TryAdd(player.userID, new Dictionary<string, Dictionary<string, int>>());
                                data.stockMarkets[shopName].stats.playerDailyItems[player.userID].TryAdd(date, new Dictionary<string, int>());
                                data.stockMarkets[shopName].stats.playerDailyItems[player.userID][date].TryAdd(displayName, 0);
                                dailyItemCache.TryAdd(displayName, 0);
                                if (data.stockMarkets[shopName].stats.playerDailyItems[player.userID][date][displayName] + item.amount + dailyItemCache[displayName] > maxToSell)
                                {
                                    PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("DailyLimitSellReached", player.UserIDString, maxToSell), config.popUpFontSize, config.popUpLength);
                                    EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                                    continue;
                                }
                                dailyItemCache[displayName] += item.amount;
                            }
                            itemCount.TryAdd(item.info.shortname, new Dictionary<ulong, int>());
                            itemCount[item.info.shortname].TryAdd(item.skin, 0);
                            itemCount[item.info.shortname][item.skin] += item.amount;
                        }
                        foreach (var shortname in itemCount)
                            foreach (var skin in shortname.Value)
                            {
                                float notRoundedPrice = skin.Value * data.stockMarkets[shopName].sellCache[shortname.Key][skin.Key].price;
                                int remainingItems = !config.keepSellRemains ? 0 : Mathf.FloorToInt(notRoundedPrice % 1 / data.stockMarkets[shopName].sellCache[shortname.Key][skin.Key].price);
                                sellItemCount.TryAdd(shortname.Key, new Dictionary<ulong, int>());
                                sellItemCount[shortname.Key].TryAdd(skin.Key, 0);
                                sellItemCount[shortname.Key][skin.Key] = itemCount[shortname.Key][skin.Key] - remainingItems;
                                int finalItemPrice = Mathf.FloorToInt(sellItemCount[shortname.Key][skin.Key] * data.stockMarkets[shopName].sellCache[shortname.Key][skin.Key].price);
                                if (sellItemCount[shortname.Key][skin.Key] == 0 || finalItemPrice == 0)
                                {
                                    sellItemCount[shortname.Key].Remove(skin.Key);
                                    if (sellItemCount[shortname.Key].Count == 0)
                                        sellItemCount.Remove(shortname.Key);
                                    continue;
                                }
                                int finalBonusPrice = 0;
                                if (!data.stockMarkets[shopName].stockConfig.blockedMultipliers.Contains($"{shortname.Key}-{skin.Key}"))
                                    finalBonusPrice = Mathf.FloorToInt(finalItemPrice / 100f * bonus);
                                sumPrice += finalItemPrice + finalBonusPrice;
                                data.stockMarkets[shopName].sellCache[shortname.Key][skin.Key].sellAmount += sellItemCount[shortname.Key][skin.Key];
                                if (data.stockMarkets[shopName].stockConfig.serverSell[shortname.Key][skin.Key].priceParent != "")
                                {
                                    string[] shortSkin = data.stockMarkets[shopName].stockConfig.serverSell[shortname.Key][skin.Key].priceParent.Split('-');
                                    string parentShortname = shortSkin[0];
                                    ulong parentSkin = Convert.ToUInt64(shortSkin[1]);
                                    if (data.stockMarkets[shopName].sellCache.ContainsKey(parentShortname) && data.stockMarkets[shopName].sellCache[parentShortname].ContainsKey(parentSkin))
                                    {
                                        if (!data.stockMarkets[shopName].sellCache.ContainsKey(parentShortname) || !data.stockMarkets[shopName].sellCache[parentShortname].ContainsKey(parentSkin))
                                            RollPrices(shopName, parentShortname, parentSkin);
                                        data.stockMarkets[shopName].sellCache[parentShortname][parentSkin].sellAmount += sellItemCount[shortname.Key][skin.Key];
                                    }
                                }
                            }
                        if (sellItemCount.Count == 0)
                        {
                            selling = false;
                            return;
                        }
                        foreach (var item in boxes[player].inventory.itemList.ToList())
                        {
                            if (!sellItemCount.ContainsKey(item.info.shortname) || !sellItemCount[item.info.shortname].ContainsKey(item.skin)) continue;
                            if (sellItemCount[item.info.shortname][item.skin] <= 0) continue;
                            int takenAmount = 0;
                            if (item.amount > sellItemCount[item.info.shortname][item.skin])
                            {
                                item.amount -= sellItemCount[item.info.shortname][item.skin];
                                takenAmount = sellItemCount[item.info.shortname][item.skin];
                                item.MarkDirty();
                                sellItemCount[item.info.shortname][item.skin] = 0;
                            }
                            else if (item.amount <= sellItemCount[item.info.shortname][item.skin])
                            {
                                sellItemCount[item.info.shortname][item.skin] -= item.amount;
                                takenAmount = item.amount;
                                item.GetHeldEntity()?.Kill();
                                item.DoRemove();
                            }
                            string displayName = item.name != null && item.name != "" ? item.name : item.info.displayName.english;
                            if (config.enableStockStats)
                            {
                                data.stockMarkets[shopName].stats.globalAllItems.TryAdd(displayName, 0);
                                data.stockMarkets[shopName].stats.globalAllItems[displayName] += takenAmount;
                                data.stockMarkets[shopName].stats.globalDailyItems.TryAdd(date, new Dictionary<string, int>());
                                data.stockMarkets[shopName].stats.globalDailyItems[date].TryAdd(displayName, 0);
                                data.stockMarkets[shopName].stats.globalDailyItems[date][displayName] += takenAmount;
                                data.stockMarkets[shopName].stats.playerAllItems.TryAdd(player.userID, new Dictionary<string, int>());
                                data.stockMarkets[shopName].stats.playerAllItems[player.userID].TryAdd(displayName, 0);
                                data.stockMarkets[shopName].stats.playerAllItems[player.userID][displayName] += takenAmount;
                                data.stockMarkets[shopName].stats.playerDailyItems.TryAdd(player.userID, new Dictionary<string, Dictionary<string, int>>());
                                data.stockMarkets[shopName].stats.playerDailyItems[player.userID].TryAdd(date, new Dictionary<string, int>());
                                data.stockMarkets[shopName].stats.playerDailyItems[player.userID][date].TryAdd(displayName, 0);
                                data.stockMarkets[shopName].stats.playerDailyItems[player.userID][date][displayName] += takenAmount;
                            }
                        }
                        if (sumPrice > 0)
                        {
                            if (config.enableStockStats)
                            {
                                data.stockMarkets[shopName].stats.globalDaily.TryAdd(date, 0);
                                data.stockMarkets[shopName].stats.globalDaily[date] += sumPrice;
                                data.stockMarkets[shopName].stats.playerDaily.TryAdd(player.userID, new Dictionary<string, int>());
                                data.stockMarkets[shopName].stats.playerDaily[player.userID].TryAdd(date, 0);
                                data.stockMarkets[shopName].stats.playerDaily[player.userID][date] += sumPrice;
                                data.stockMarkets[shopName].stats.playerAll.TryAdd(player.userID, 0);
                                data.stockMarkets[shopName].stats.playerAll[player.userID] += sumPrice;
                            }
                            GiveCurrency(shopName, player.userID, sumPrice);
                            string currencyFormat = FormatPrice(shopName, sumPrice);
                            PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("ItemsSold", player.UserIDString, currencyFormat), config.popUpFontSize, config.popUpLength);
                            EffectNetwork.Send(new Effect("assets/prefabs/misc/casino/slotmachine/effects/payout.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                            Interface.CallHook("OnMarketItemsSold", player, sumPrice);
                            if (config.enableLogs)
                                Puts($"Player {player.displayName} ({player.UserIDString}) sold items for {currencyFormat}.");
                        }
                        selling = false;
                        OpenSellUI(player, shopName);
                    }
                }
            }
            else if (arg.Args[0] == "withdrawCurrency")
            {
                if (arg.Args.Length < 3) return;
                int currencyAmount;
                if (int.TryParse(arg.Args[2], out currencyAmount))
                {
                    string shopName = arg.Args[1];
                    RegularItemConfig currItem = config.shops[shopName].depositItem;
                    if (string.IsNullOrEmpty(currItem.shortname)) return;
                    int maxStack = ItemManager.FindItemDefinition(currItem.shortname).stackable;
                    if (currencyAmount > maxStack)
                    {
                        PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("TooBigAmountForStack", player.UserIDString, maxStack), config.popUpFontSize, config.popUpLength);
                        EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                        return;
                    }
                    int withdrawedAmount = currencyAmount * config.shops[shopName].depositItem.value;
                    if (!TakeCurrency(shopName, player, withdrawedAmount))
                    {
                        int currAmount = GetCurrencyAmount(shopName, player);
                        PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("NotEnoughCurrencyWithdraw", player.UserIDString, FormatPrice(shopName, currAmount)), config.popUpFontSize, config.popUpLength);
                        EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                        return;
                    }
                    Item currencyItem = ItemManager.CreateByName(currItem.shortname, currencyAmount, currItem.skin);
                    if (!string.IsNullOrEmpty(currItem.displayName))
                        currencyItem.name = currItem.displayName;
                    if (!currencyItem.MoveToContainer(player.inventory.containerMain))
                        if (!currencyItem.MoveToContainer(player.inventory.containerBelt))
                            RedeemStorageAPI?.Call("AddItem", player.userID.Get(), config.storageName, currencyItem);
                    PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("CurrencyWithdrawed", player.UserIDString), config.popUpFontSize, config.popUpLength);
                    EffectNetwork.Send(new Effect("assets/prefabs/misc/casino/slotmachine/effects/payout.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                }
            }
        }

        private void CurrencyAdminCommand(IPlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.Id, "shoppystock.admin"))
            {
                player.Message(Lang("NoAdminPermission", player.Id));
                return;
            }
            if (args.Length < 3)
            {
                player.Message(Lang("AdminCommandHelp", player.Id, config.adminCommand));
                return;
            }
            else
            {
                string shopName = args[0];
                if (!config.shops.ContainsKey(shopName))
                {
                    player.Message(Lang("ShopNotFound", player.Id, shopName));
                    return;
                }
                string action = args[1].ToLower();
                if (action != "give" && action != "take" && action != "clear" && action != "check")
                {
                    player.Message(Lang("AdminCommandHelp", player.Id, config.adminCommand));
                    return;
                }
                KeyValuePair<ulong, UserData> foundUser = new KeyValuePair<ulong, UserData>(0, null);
                ulong userId;
                if (ulong.TryParse(args[2], out userId))
                {
                    if (!data.shops[shopName].users.ContainsKey(userId))
                    {
                        IPlayer foundPlayer = covalence.Players.FindPlayerById(args[2]);
                        if (foundPlayer == null)
                        {
                            player.Message(Lang("UserNotFound", player.Id, args[2]));
                            return;
                        }
                        data.shops[shopName].users.TryAdd(userId, new UserData() { username = foundPlayer.Name });
                    }
                    foundUser = new KeyValuePair<ulong, UserData>(userId, data.shops[shopName].users[userId]);
                }
                else
                {
                    string displayName = args[2];
                    List<KeyValuePair<ulong, UserData>> users = data.shops[shopName].users.Where(x => x.Value.username.Contains(displayName)).ToList();
                    if (users.Count == 0)
                    {
                        player.Message(Lang("UserNotFound", player.Id, displayName));
                        return;
                    }
                    if (users.Count > 1)
                    {
                        player.Message(Lang("TooManyUsersFound", player.Id));
                        return;
                    }
                    foundUser = users[0];
                }
                if (foundUser.Key == 0) return;
                if (action == "give" || action == "take")
                {
                    if (args.Length < 4)
                    {
                        player.Message(Lang("AdminCommandHelp", player.Id, config.adminCommand));
                        return;
                    }
                    int amount;
                    if (!int.TryParse(args[3], out amount))
                    {
                        player.Message(Lang("WrongAmountFormat", player.Id, args[3]));
                        return;
                    }
                    if (action == "give")
                    {
                        data.shops[shopName].users[foundUser.Key].currencyAmount += amount;
                        player.Message(Lang("CurrencyAdded", player.Id, shopName, foundUser.Value.username, amount, data.shops[shopName].users[foundUser.Key].currencyAmount));
                    }
                    else if (action == "take")
                    {
                        data.shops[shopName].users[foundUser.Key].currencyAmount -= amount;
                        player.Message(Lang("CurrencyTaken", player.Id, shopName, foundUser.Value.username, amount, data.shops[shopName].users[foundUser.Key].currencyAmount));
                    }
                }
                else if (action == "clear")
                {
                    data.shops[shopName].users[foundUser.Key].currencyAmount = 0;
                    player.Message(Lang("CurrencyCleared", player.Id, shopName, foundUser.Value.username));
                }
                else if (action == "check")
                    player.Message(Lang("CurrencyCheck", player.Id, shopName, foundUser.Value.username, data.shops[shopName].users[foundUser.Key].currencyAmount));
            }
        }

        private bool IsBlocked(BasePlayer player)
        {
            if (NoEscape == null) return false;
            if (config.noEscapeCombatShop && NoEscape.Call<bool>("IsCombatBlocked", player.UserIDString)) return true;
            if (config.noEscapeRaidShop && NoEscape.Call<bool>("IsRaidBlocked", player.UserIDString)) return true;
            if (config.noEscapeEscapeShop && NoEscape.Call<bool>("IsEscapeBlocked", player.UserIDString)) return true;
            return false;
        }

        private void TryBuyFromStock(BasePlayer player, string shopName, string listingKey, OrderData order, int amount, bool takeCurrency = true)
        {
            if (order == null || (order.sellerId != player.userID && order.isCanceled))
            {
                PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("ItemNoLongerAvailable", player.UserIDString), config.popUpFontSize, config.popUpLength);
                EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                return;
            }
            if (config.debug) Puts($"[DEBUG] Starting buying from stock.");
            if (!takeCurrency)
            {
                bool privil = false;
                bool safeZone = player.InSafeZone();
                BuildingPrivlidge priv = player.GetBuildingPrivilege();
                if (priv != null && priv.IsAuthed(player.userID))
                    privil = true;
                int trueCount = 0;
                if (config.authRefund && privil)
                    trueCount++;
                if (config.safeZoneRefundOnly && safeZone)
                    trueCount++;
                if (trueCount == 0 && (config.authRefund || config.safeZoneRefundOnly))
                {
                    if (config.authRefund && !privil)
                    {
                        PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("NotAuthToRefund", player.UserIDString), config.popUpFontSize, config.popUpLength);
                        EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                    }
                    else if (config.safeZoneRefundOnly && !safeZone)
                    {
                        PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("NotInSafeZone", player.UserIDString), config.popUpFontSize, config.popUpLength);
                        EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                    }
                    return;
                }
            }
            if (amount > order.item.amount)
                amount = order.item.amount;
            int balance = GetCurrencyAmount(shopName, player);
            float totalPrice = order.price * amount;
            int totalTotal = Mathf.CeilToInt(totalPrice);

            if (takeCurrency && balance < totalTotal)
            {
                EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("NotEnoughCurrency", player.UserIDString), config.popUpFontSize, config.popUpLength);
                return;
            }
            else if (!takeCurrency || (takeCurrency && TakeCurrency(shopName, player, totalTotal)))
            {
                if (config.debug) Puts($"[DEBUG] Currency valid.");
                if (takeCurrency)
                    GiveCurrency(shopName, order.sellerId, totalTotal);
                Item outputItem = order.item.ToItem();
                outputItem.amount = amount;
                order.item.amount -= amount;
                string name = config.translateItems ? Lang($"{order.item.shortname}-{order.item.skin}", player.UserIDString) : order.item.displayName == null || order.item.displayName == "" ? outputItem.info.displayName.english : order.item.displayName;
                if (config.debug) Puts($"[DEBUG] Before currency took.");
                if (takeCurrency)
                {
                    string priceFormatted = FormatPrice(shopName, totalTotal);
                    PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("ItemPurchased", player.UserIDString, order.sellerName, name, priceFormatted, amount), config.popUpFontSize, config.popUpLength);
                    EffectNetwork.Send(new Effect("assets/prefabs/misc/casino/slotmachine/effects/payout.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                    Interface.CallHook("OnMarketItemPurchased", player, shopName, order.sellerId, totalTotal);
                    BasePlayer seller = BasePlayer.FindByID(order.sellerId);
                    if (seller != null)
                    {
                        PopUpAPI?.Call("ShowPopUp", seller, "Market", Lang("ItemPurchasedOwner", seller.UserIDString, amount, name, priceFormatted), config.popUpFontSize, config.popUpLength);
                        EffectNetwork.Send(new Effect("assets/prefabs/misc/casino/slotmachine/effects/payout.prefab", seller, 0, Vector3.zero, Vector3.up), seller.net.connection);
                    }
                    if (config.shops[shopName].stockConfig.chatMessSoldItem)
                        foreach (var oPlayer in BasePlayer.activePlayerList)
                            SendReply(oPlayer, Lang("ItemPurchasedBroadcast", oPlayer.UserIDString, player.displayName, amount, name, order.sellerName, Lang(shopName, oPlayer.UserIDString), priceFormatted));
                    if (config.enableLogs)
                        Puts($"Player {player.displayName} ({player.UserIDString}) purchased x{amount} {name} from {order.sellerId} for {priceFormatted}.");
                }
                else
                {
                    if (config.enableLogs)
                        Puts($"Player {player.displayName} ({player.UserIDString}) returned x{amount} {name}.");
                    EffectNetwork.Send(new Effect("assets/prefabs/misc/easter/painted eggs/effects/eggpickup.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                    PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("ItemReturned", player.UserIDString, amount, name, config.storageName), config.popUpFontSize, config.popUpLength);
                }
                if (config.debug) Puts($"[DEBUG] After currency took.");
                if (takeCurrency)
                {
                    if (!outputItem.MoveToContainer(player.inventory.containerMain))
                        if (!outputItem.MoveToContainer(player.inventory.containerBelt))
                            RedeemStorageAPI?.Call("AddItem", player.userID.Get(), config.storageName, outputItem);
                }
                else
                    RedeemStorageAPI?.Call("AddItem", order.sellerId, config.storageName, outputItem);
                string[] split = listingKey.Split('-');
                string shortname = split[0];
                ulong skin = Convert.ToUInt64(split[1]);
                if (order.item.amount <= 0)
                {
                    cachedListingCount[order.sellerId].sellListings--;
                    data.stockMarkets[shopName].playerData.sellOrders[shortname][skin].Remove(order);
                }
                foreach (var stockPlayer in stockPosition.ToList())
                {
                    if (stockPlayer.Key == null) continue;
                    if (stockPlayer.Key == player) continue;
                    if (stockPlayer.Value.currentItem == "" || stockPlayer.Value.currentItem != listingKey) continue;
                    OpenStockItemInfoUI(stockPlayer.Key, shopCache[stockPlayer.Key].stockMarket.shopName, shopCache[stockPlayer.Key].stockMarket.listing.key, shopCache[stockPlayer.Key].stockMarket.listing.buyOffers, shopCache[stockPlayer.Key].stockMarket.listing.selected, shopCache[stockPlayer.Key].stockMarket.listing.index, shopCache[stockPlayer.Key].stockMarket.listing.amount, shopCache[stockPlayer.Key].stockMarket.listing.page);
                }
                if (config.debug) Puts($"[DEBUG] Finishing purchase.");
                OpenStockItemInfoUI(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.listing.key, shopCache[player].stockMarket.listing.buyOffers);
            }
        }

        private void TrySellToStock(BasePlayer player, string shopName, string listingKey, OrderData order, int amount)
        {
            int validItems = 0;
            if (config.debug) Puts($"[DEBUG] Checking items.");
            foreach (var item in player.inventory.containerMain.itemList)
            {
                if (item.info.shortname == order.item.shortname && item.skin == order.item.skin)
                    validItems += item.amount;
            }
            foreach (var item in player.inventory.containerBelt.itemList)
            {
                if (item.info.shortname == order.item.shortname && item.skin == order.item.skin)
                    validItems += item.amount;
            }
            if (validItems == 0)
            {
                PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("NoValidItemsInInventory", player.UserIDString), config.popUpFontSize, config.popUpLength);
                EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                return;
            }
            if (amount > validItems)
                amount = validItems;
            int taken = 0;
            List<Item> invItems = Pool.Get<List<Item>>();
            invItems.AddRange(player.inventory.containerMain.itemList);
            invItems.AddRange(player.inventory.containerBelt.itemList);
            foreach (var item in invItems)
            {
                if (item.info.shortname == order.item.shortname && item.skin == order.item.skin)
                {
                    if (item.amount > amount - taken)
                    {
                        item.amount -= amount - taken;
                        item.MarkDirty();
                        break;
                    }
                    else
                    {
                        taken += item.amount;
                        item.GetHeldEntity()?.Kill();
                        item.DoRemove();
                        if (taken >= amount) break;
                    }
                }
            }
            Pool.FreeUnmanaged(ref invItems);
            if (config.debug) Puts($"[DEBUG] Items checked.");
            ItemDefinition itemDef = ItemManager.FindItemDefinition(order.item.shortname);
            int maxStackSize = itemDef.stackable;
            int totalPrice = Mathf.FloorToInt(amount * order.price);
            GiveCurrency(shopName, player.userID, totalPrice);
            int cachedAmount = amount;
            while (amount != 0)
            {
                int itemAmount = amount > maxStackSize ? maxStackSize : amount;
                Item addItem = order.item.ToItem();
                addItem.amount = itemAmount;
                RedeemStorageAPI?.Call("AddItem", order.sellerId, config.storageName, addItem);
                amount -= itemAmount;
            }
            if (config.debug) Puts($"[DEBUG] Amount loop done.");
            string[] split = listingKey.Split('-');
            string shortname = split[0];
            ulong skin = Convert.ToUInt64(split[1]);
            order.item.amount -= cachedAmount;
            string name = config.translateItems ? Lang($"{order.item.shortname}-{order.item.skin}", player.UserIDString) : order.item.displayName != null && order.item.displayName != "" ? order.item.displayName : itemDef.displayName.english;
            string formattedPrice = FormatPrice(shopName, totalPrice);
            PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("ItemSold", player.UserIDString, order.sellerName, name, formattedPrice), config.popUpFontSize, config.popUpLength);
            var buyer = BasePlayer.FindByID(order.sellerId);
            if (buyer != null)
                PopUpAPI?.Call("ShowPopUp", buyer, "Market", Lang("BuyOrderFulfilled", buyer.UserIDString, player.displayName, cachedAmount, name, formattedPrice), config.popUpFontSize, config.popUpLength);
            EffectNetwork.Send(new Effect("assets/prefabs/misc/casino/slotmachine/effects/payout.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
            Interface.CallHook("OnShopItemSold", player, shopName, order.sellerId, totalPrice);
            if (config.enableLogs)
                Puts($"Player {player.displayName} ({player.UserIDString}) sold x{cachedAmount} {name} to {order.sellerName} ({order.sellerId}) for {formattedPrice}.");
            if (order.item.amount <= 0)
            {
                cachedListingCount[order.sellerId].buyListings--;
                data.stockMarkets[shopName].playerData.buyOrders[shortname][skin].Remove(order);
            }
            foreach (var stockPlayer in stockPosition.ToList())
            {
                if (stockPlayer.Key == null) continue;
                if (stockPlayer.Key == player) continue;
                if (stockPlayer.Value.currentItem == "" || stockPlayer.Value.currentItem != listingKey) continue;
                OpenStockItemInfoUI(stockPlayer.Key, shopCache[stockPlayer.Key].stockMarket.shopName, shopCache[stockPlayer.Key].stockMarket.listing.key, shopCache[stockPlayer.Key].stockMarket.listing.buyOffers, shopCache[stockPlayer.Key].stockMarket.listing.selected, shopCache[stockPlayer.Key].stockMarket.listing.index, shopCache[stockPlayer.Key].stockMarket.listing.amount, shopCache[stockPlayer.Key].stockMarket.listing.page);
            }
            if (config.debug) Puts($"[DEBUG] Item sold to player finished.");
            OpenStockItemInfoUI(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.listing.key, shopCache[player].stockMarket.listing.buyOffers);
        }

        private void RefundBuyRequest(BasePlayer player, string shopName, string listingKey, OrderData order)
        {
            int refundPrice = Mathf.FloorToInt(order.price * order.item.amount);
            GiveCurrency(shopName, player.userID, refundPrice);
            ItemDefinition itemDef = ItemManager.FindItemDefinition(order.item.shortname);
            string name = config.translateItems ? Lang($"{order.item.shortname}-{order.item.skin}", player.UserIDString) : order.item.displayName != null && order.item.displayName != "" ? order.item.displayName : itemDef.displayName.english;
            string formattedPrice = FormatPrice(shopName, refundPrice);
            PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("RefundBuyRequest", player.UserIDString, name, formattedPrice), config.popUpFontSize, config.popUpLength);
            EffectNetwork.Send(new Effect("assets/prefabs/misc/easter/painted eggs/effects/eggpickup.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
            if (config.enableLogs)
                Puts($"Player {player.displayName} ({player.UserIDString}) refunded buy request of x{order.item.amount} {name} for {formattedPrice}.");
            string[] split = listingKey.Split('-');
            string shortname = split[0];
            ulong skin = Convert.ToUInt64(split[1]);
            {
                cachedListingCount[order.sellerId].buyListings--;
                data.stockMarkets[shopName].playerData.buyOrders[shortname][skin].Remove(order);
            }
            OpenStockItemInfoUI(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.listing.key, shopCache[player].stockMarket.listing.buyOffers);
        }

        private void ChangeFavouriteItem(BasePlayer player, string shopName, string listingKey)
        {
            data.stockMarkets[shopName].favourites.TryAdd(player.userID, new List<string>());
            if (data.stockMarkets[shopName].favourites[player.userID].Contains(listingKey))
                data.stockMarkets[shopName].favourites[player.userID].Remove(listingKey);
            else
                data.stockMarkets[shopName].favourites[player.userID].Add(listingKey);
            OpenStockMarketUI(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.search, shopCache[player].stockMarket.category, shopCache[player].stockMarket.page, shopCache[player].stockMarket.categoryPage);
        }

        private void TryMarketAction(Item item, BasePlayer player)
        {
            if (config.debug) Puts($"[DEBUG] Market action checks.");
            PermissionData maxPerm = GetMaxListingAmount(player);
            if (maxPerm == null)
            {
                PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("NoPermissionListing", player.UserIDString), config.popUpFontSize, config.popUpLength);
                EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                return;
            }
            string shopName = shopCache[player].stockMarket.shopName;
            bool buyRequest = shopCache[player].stockMarket.request.buyRequest;
            if (buyRequest && !config.shops[shopName].stockConfig.allowBuyRequests)
            {
                PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("BuyRequestsNotAllowed", player.UserIDString), config.popUpFontSize, config.popUpLength);
                EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                return;
            }
            if (!buyRequest && !config.shops[shopName].stockConfig.allowSellRequests)
            {
                PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("SellRequestsNotAllowed", player.UserIDString), config.popUpFontSize, config.popUpLength);
                EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                return;
            }
            if (cachedListingCount.ContainsKey(player.userID) && ((buyRequest && cachedListingCount[player.userID].buyListings >= maxPerm.buyListings) || (!buyRequest && cachedListingCount[player.userID].sellListings >= maxPerm.sellListings)))
            {
                PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("ListingLimitAcieved", player.UserIDString), config.popUpFontSize, config.popUpLength);
                EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                return;
            }
            if (shopCache[player].stockMarket.request.price <= 0 || shopCache[player].stockMarket.request.amount <= 0)
            {
                PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("CannotSetPriceOrAmountZero", player.UserIDString), config.popUpFontSize, config.popUpLength);
                EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                return;
            }
            if (config.shops[shopName].stockConfig.maxItemPrice > 0 && shopCache[player].stockMarket.request.price > config.shops[shopName].stockConfig.maxItemPrice)
            {
                PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("PriceTooHigh", player.UserIDString, config.shops[shopName].stockConfig.maxItemPrice), config.popUpFontSize, config.popUpLength);
                EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                return;
            }
            if ((item.skin == 0 && config.ignoredShortnames.Contains(item.info.shortname)) || (item.skin != 0 && config.ignoredShortnames.Contains(item.skin.ToString())))
            {
                PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("ItemNotAllowedToList", player.UserIDString), config.popUpFontSize, config.popUpLength);
                EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                return;
            }
            if (config.debug) Puts($"[DEBUG] Market action checks finished.");
            bool moveToUnskinned = false;
            if (item.skin != 0)
            {
                if (!config.allowAllSkinListings && !data.stockMarkets[shopName].stockConfig.customItems.ContainsKey($"{item.info.shortname}-{item.skin}"))
                {
                    PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("ItemNotSupported", player.UserIDString), config.popUpFontSize, config.popUpLength);
                    EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                    return;
                }
                else if (config.allowAllSkinListings && !data.stockMarkets[shopName].stockConfig.customItems.ContainsKey($"{item.info.shortname}-{item.skin}"))
                {
                    List<KeyValuePair<string, ulong>> imagesToDownload = new List<KeyValuePair<string, ulong>>
                    {
                        new KeyValuePair<string, ulong>(item.info.shortname, item.skin)
                    };
                    ImageLibrary?.Call("LoadImageList", Name, imagesToDownload);
                    moveToUnskinned = true;
                }
            }
            if (config.debug) Puts($"[DEBUG] Item skin check finished.");
            int balance = GetCurrencyAmount(shopName, player);
            ulong skinCategory = moveToUnskinned ? 0 : item.skin;
            try
            {
                if (buyRequest)
                {
                    float totalPrice = checked(shopCache[player].stockMarket.request.price * shopCache[player].stockMarket.request.amount);
                    float tax = data.stockMarkets[shopName].stockConfig.buyTax;
                    float totalTax = checked((totalPrice / 100f) * tax);
                    int totalTotal = checked(Mathf.CeilToInt(checked(totalPrice + totalTax)));
                    if (config.debug) Puts($"[DEBUG] Buy request checking start.");
                    if (balance < totalTotal)
                    {
                        PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("NotEnoughCurrency", player.UserIDString), config.popUpFontSize, config.popUpLength);
                        EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                        return;
                    }
                    else if (TakeCurrency(shopName, player, totalTotal))
                    {
                        data.stockMarkets[shopName].playerData.buyOrders.TryAdd(item.info.shortname, new Dictionary<ulong, List<OrderData>>());
                        data.stockMarkets[shopName].playerData.buyOrders[item.info.shortname].TryAdd(skinCategory, new List<OrderData>());
                        string name = config.translateItems ? Lang($"{item.info.shortname}-{item.skin}", player.UserIDString) : item.name != null && item.name != "" ? item.name : item.info.displayName.english;
                        ItemData itemData = ItemData.FromItem(item);
                        itemData.amount = shopCache[player].stockMarket.request.amount;
                        data.stockMarkets[shopName].playerData.buyOrders[item.info.shortname][skinCategory].Add(new OrderData()
                        {
                            item = itemData,
                            price = shopCache[player].stockMarket.request.price,
                            sellerId = player.userID,
                            sellerName = player.displayName
                        });
                        cachedListingCount.TryAdd(player.userID, new PermissionData());
                        cachedListingCount[player.userID].buyListings++;
                        string formattedPrice = FormatPrice(shopName, totalPrice);
                        if (config.enableLogs)
                            Puts($"Player {player.displayName} ({player.UserIDString}) added buy listing of x{shopCache[player].stockMarket.request.amount} {name} for {formattedPrice}.");
                        if (config.shops[shopName].stockConfig.chatMessNewBuyRequest)
                            foreach (var oPlayer in BasePlayer.activePlayerList)
                                SendReply(oPlayer, Lang("AddedBuyListing", oPlayer.UserIDString, player.displayName, shopCache[player].stockMarket.request.amount, name, Lang(shopName, oPlayer.UserIDString), formattedPrice));
                        PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("BuyListingAdded", player.UserIDString, name, formattedPrice), config.popUpFontSize, config.popUpLength);
                        EffectNetwork.Send(new Effect("assets/prefabs/missions/effects/mission_accept.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                        Interface.CallHook("OnMarketBuyRequestCreated", player, shopName, item.info.shortname, item.skin, shopCache[player].stockMarket.request.price);
                    }
                }
                else
                {
                    if (item.amount < 1)
                    {
                        PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("CannotSetPriceOrAmountZero", player.UserIDString), config.popUpFontSize, config.popUpLength);
                        PrintWarning($"Player {player.displayName} ({player.UserIDString}) is trying to add listing of an item that's quanity is below one. You have some problems on server!");
                        EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                        return;
                    }
                    float totalPrice = checked(shopCache[player].stockMarket.request.price * item.amount);
                    float tax = data.stockMarkets[shopName].stockConfig.sellTax;
                    float totalTax = checked((totalPrice / 100f) * tax);
                    int totalTotal = checked(Mathf.CeilToInt(checked(totalPrice + totalTax)));
                    int totalTaxInt = Mathf.CeilToInt(totalTax);
                    if (config.debug) Puts($"[DEBUG] Sell request checking start.");
                    if (balance < totalTaxInt)
                    {
                        PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("NotEnoughCurrency", player.UserIDString), config.popUpFontSize, config.popUpLength);
                        EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                        return;
                    }
                    else if (totalTax <= 0 || TakeCurrency(shopName, player, totalTaxInt))
                    {
                        data.stockMarkets[shopName].playerData.sellOrders.TryAdd(item.info.shortname, new Dictionary<ulong, List<OrderData>>());
                        data.stockMarkets[shopName].playerData.sellOrders[item.info.shortname].TryAdd(skinCategory, new List<OrderData>());
                        string name = config.translateItems ? Lang($"{item.info.shortname}-{item.skin}", player.UserIDString) : item.name != null && item.name != "" ? item.name : item.info.displayName.english;
                        ItemData itemData = ItemData.FromItem(item);
                        data.stockMarkets[shopName].playerData.sellOrders[item.info.shortname][skinCategory].Add(new OrderData()
                        {
                            item = itemData,
                            price = shopCache[player].stockMarket.request.price,
                            sellerId = player.userID,
                            sellerName = player.displayName
                        });
                        cachedListingCount.TryAdd(player.userID, new PermissionData());
                        cachedListingCount[player.userID].sellListings++;
                        string formattedPrice = FormatPrice(shopName, totalTaxInt);
                        if (config.enableLogs)
                            Puts($"Player {player.displayName} ({player.UserIDString}) added sell listing of x{item.amount} {name} for {formattedPrice} and paid {totalTaxInt} tax.");
                        if (config.shops[shopName].stockConfig.chatMessNewSellRequest)
                            foreach (var oPlayer in BasePlayer.activePlayerList)
                                SendReply(oPlayer, Lang("AddedSellListing", oPlayer.UserIDString, player.displayName, item.amount, name, Lang(shopName, oPlayer.UserIDString), formattedPrice));
                        PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("SellListingAdded", player.UserIDString, name, formattedPrice), config.popUpFontSize, config.popUpLength);
                        EffectNetwork.Send(new Effect("assets/prefabs/missions/effects/mission_accept.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                        Interface.CallHook("OnMarketSellRequestCreated", player, shopName, item.info.shortname, item.skin, shopCache[player].stockMarket.request.price);
                    }
                }
                item.GetHeldEntity()?.Kill();
                item.DoRemove();
                if (shopCache.ContainsKey(player) && !shopCache[player].stockMarket.quickList)
                    OpenStockItemInfoUI(player, shopCache[player].stockMarket.shopName, shopCache[player].stockMarket.listing.key, shopCache[player].stockMarket.listing.buyOffers);
                NextTick(() => player.EndLooting());
            }
            catch (OverflowException ex)
            {
                Puts($"Player {player.displayName} ({player.UserIDString}) tried to set shop offer over possible int limit. Cancelling...");
                PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("PriceTooBig", player.UserIDString), config.popUpFontSize, config.popUpLength);
            }
        }

        private void TryAddToBank(Item item, BasePlayer player)
        {
            string shopName = shopCache[player].stockMarket.shopName;
            if (data.stockMarkets[shopName].stockConfig.serverSell.ContainsKey(item.info.shortname) && data.stockMarkets[shopName].stockConfig.serverSell[item.info.shortname].ContainsKey(item.skin))
            {
                StockConfig sc = config.shops[shopName].stockConfig;
                if (item.maxCondition != 0 && item.maxCondition != item.condition)
                {
                    PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("ItemConditionBroken", player.UserIDString), config.popUpFontSize, config.popUpLength);
                    EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                    return;
                }
                data.stockMarkets[shopName].playerData.playerBanks.TryAdd(player.userID, new Dictionary<string, ItemData>());
                var playerBank = data.stockMarkets[shopName].playerData.playerBanks[player.userID];
                string key = $"{item.info.shortname}-{item.skin}";
                if (sc.maxBankIndividualItems > 0 && !playerBank.ContainsKey(key))
                {
                    int maxItems = sc.maxBankIndividualItems;
                    foreach (var perm in sc.maxBankIndividualItemsPerms)
                    {
                        if (perm.Value > maxItems && permission.UserHasPermission(player.UserIDString, perm.Key))
                            maxItems = perm.Value;
                    }
                    if (playerBank.Count >= maxItems)
                    {
                        PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("TooManyIndividualItemsInBank", player.UserIDString, maxItems), config.popUpFontSize, config.popUpLength);
                        EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                        return;
                    }
                }
                if (sc.maxBankStacks > 0 && playerBank.ContainsKey(key))
                {
                    int maxStacks = sc.maxBankStacks;
                    foreach (var perm in sc.maxBankStacksPerms)
                    {
                        if (perm.Value > maxStacks && permission.UserHasPermission(player.UserIDString, perm.Key))
                            maxStacks = perm.Value;
                    }
                    if (playerBank[key].amount + item.amount > item.info.stackable * maxStacks)
                    {
                        PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("TooManyStacksInBank", player.UserIDString, item.info.stackable * maxStacks), config.popUpFontSize, config.popUpLength);
                        EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                        return;
                    }
                }
                if (playerBank.ContainsKey(key))
                    playerBank[key].amount += item.amount;
                else
                {
                    playerBank.Add(key, ItemData.FromItem(item));
                    if (item.instanceData != null)
                        playerBank[key].dataInt = item.instanceData.dataInt;
                    if (item.name != null && item.name != "")
                        playerBank[key].displayName = item.name;
                }
                if (config.enableLogs)
                    Puts($"Player {player.displayName} ({player.UserIDString}) added x{item.amount} {item.info.shortname} to bank.");
                item.GetHeldEntity()?.Kill();
                item.DoRemove();
                OpenBankManagement(player, shopName);
                PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("ItemAddedToBank", player.UserIDString), config.popUpFontSize, config.popUpLength);
                EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/item_unlock.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
            }
            else
            {
                PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("ItemNotForSale", player.UserIDString), config.popUpFontSize, config.popUpLength);
                EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
            }
        }

        private void TryAddCurrency(Item item, BasePlayer player)
        {
            if (item == null) return;
            string shopName = shopCache[player].currentShop;
            if (!config.shops[shopName].canDeposit || item.skin != config.shops[shopName].depositItem.skin || item.info.shortname != config.shops[shopName].depositItem.shortname) return;
            int currencyAmount = item.amount * config.shops[shopName].depositItem.value;
            item.GetHeldEntity()?.Kill();
            item.DoRemove();
            GiveCurrency(shopName, player.userID, currencyAmount);
            string priceFormatted = FormatPrice(shopName, currencyAmount);
            if (config.enableLogs)
                Puts($"Player {player.displayName} ({player.userID}) deposited {priceFormatted}.");
            PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("CurrencyDeposited", player.UserIDString, priceFormatted), config.popUpFontSize, config.popUpLength);
            EffectNetwork.Send(new Effect("assets/prefabs/misc/casino/slotmachine/effects/payout.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
        }

        private void TryAddItemEntry(Item item, BasePlayer player)
        {
            if (item == null) return;
            if (!addingItemsCache.ContainsKey(player)) return;
            string name = !string.IsNullOrEmpty(item.name) ? item.name : item.info.displayName.english;
            string itemName = string.IsNullOrEmpty(item.name) ? "" : item.name;
            string key = $"{item.info.shortname}-{item.skin}";
            if (addingItemsCache[player].stockMarket)
            {
                itemDefinitions.TryAdd(key, new StockItemDefinitionData() { category = addingItemsCache[player].category, shortname = item.info.shortname, skin = item.skin, displayName = name });
                if (item.skin != 0)
                {
                    data.stockMarkets[addingItemsCache[player].shopName].stockConfig.customItems.TryAdd(key, new CustomItemData() { shortname = item.info.shortname, skin = item.skin, displayName = name, amount = item.amount, category = addingItemsCache[player].category });
                    PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("ItemAddedToStock", player.UserIDString, addingItemsCache[player].shopName, addingItemsCache[player].category), config.popUpFontSize, config.popUpLength);
                }
                if (addingItemsCache[player].var1 != 0 && addingItemsCache[player].var2 != 0 && addingItemsCache[player].var3 != 0)
                {
                    data.stockMarkets[addingItemsCache[player].shopName].stockConfig.serverSell.TryAdd(item.info.shortname, new Dictionary<ulong, ServerSellData>());
                    data.stockMarkets[addingItemsCache[player].shopName].stockConfig.serverSell[item.info.shortname].TryAdd(item.skin, new ServerSellData());
                    data.stockMarkets[addingItemsCache[player].shopName].stockConfig.serverSell[item.info.shortname][item.skin] = new ServerSellData() { displayName = name, defaultAmount = addingItemsCache[player].var3, minimalPrice = addingItemsCache[player].var1, maximalPrice = addingItemsCache[player].var2 };
                    RollPrices(addingItemsCache[player].shopName, item.info.shortname, item.skin);
                    PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("ItemAddedToStockAndSell", player.UserIDString, addingItemsCache[player].shopName, addingItemsCache[player].category), config.popUpFontSize, config.popUpLength);
                }
                item.GetHeldEntity()?.Kill();
                item.DoRemove();
            }
            else
            {
                data.shops[addingItemsCache[player].shopName].categories[addingItemsCache[player].category].listings.TryAdd(key, new ListingData() { amount = item.amount, blueprint = item.IsBlueprint(), displayName = name, itemName = itemName, price = Convert.ToInt32(addingItemsCache[player].var1), shortname = item.info.shortname, skin = item.skin });
                item.GetHeldEntity()?.Kill();
                item.DoRemove();
                PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("ItemAddedToShop", player.UserIDString, addingItemsCache[player].shopName, addingItemsCache[player].category), config.popUpFontSize, config.popUpLength);
            }
            SaveData(true);
        }

        private void DepositInventoryToBank(BasePlayer player, string shopName)
        {
            bool changed = false;
            foreach (var item in player.inventory.containerMain.itemList.ToList())
            {
                if (data.stockMarkets[shopName].stockConfig.serverSell.ContainsKey(item.info.shortname) && data.stockMarkets[shopName].stockConfig.serverSell[item.info.shortname].ContainsKey(item.skin))
                {
                    if (item.maxCondition != 0 && item.maxCondition != item.condition) continue;
                    data.stockMarkets[shopName].playerData.playerBanks.TryAdd(player.userID, new Dictionary<string, ItemData>());
                    string key = $"{item.info.shortname}-{item.skin}";
                    if (data.stockMarkets[shopName].playerData.playerBanks[player.userID].ContainsKey(key))
                        data.stockMarkets[shopName].playerData.playerBanks[player.userID][key].amount += item.amount;
                    else
                    {
                        data.stockMarkets[shopName].playerData.playerBanks[player.userID].Add(key, ItemData.FromItem(item));
                        if (item.instanceData != null)
                            data.stockMarkets[shopName].playerData.playerBanks[player.userID][key].dataInt = item.instanceData.dataInt;
                        if (item.name != null && item.name != "")
                            data.stockMarkets[shopName].playerData.playerBanks[player.userID][key].displayName = item.name;
                    }
                    if (config.enableLogs)
                        Puts($"Player {player.displayName} ({player.UserIDString}) added x{item.amount} {item.info.shortname} to bank.");
                    item.GetHeldEntity()?.Kill();
                    item.DoRemove();
                    changed = true;
                }
            }
            if (changed)
            {
                OpenBankManagement(player, shopName);
                PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("ItemAddedToBank", player.UserIDString), config.popUpFontSize, config.popUpLength);
                EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/item_unlock.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
            }
        }

        private void WithdrawBank(BasePlayer player, string shopName, string listingKey, string amountString)
        {
            int amount;
            if (!int.TryParse(amountString, out amount))
            {
                PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("ValueIsNotNumber", player.UserIDString), config.popUpFontSize, config.popUpLength);
                EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                return;
            }
            if (amount <= 0) return;
            ItemData bankItem = data.stockMarkets[shopName].playerData.playerBanks[player.userID][listingKey];
            if (amount > bankItem.amount)
                amount = bankItem.amount;
            Item outputItem = ItemManager.CreateByName(bankItem.shortname, amount, bankItem.skin);
            if (bankItem.displayName != "")
                outputItem.name = bankItem.displayName;
            if (bankItem.dataInt != 0)
                outputItem.instanceData = new ProtoBuf.Item.InstanceData
                {
                    ShouldPool = false,
                    dataInt = bankItem.dataInt
                };
            outputItem.condition = bankItem.condition;
            outputItem.maxCondition = bankItem.maxCondition;
            string name = config.translateItems ? Lang($"{bankItem.shortname}-{bankItem.skin}", player.UserIDString) : bankItem.displayName != null && bankItem.displayName != "" ? bankItem.displayName : outputItem.info.displayName.english;
            bankItem.amount -= amount;
            if (bankItem.amount <= 0)
            {
                data.stockMarkets[shopName].playerData.playerBanks[player.userID].Remove(listingKey);
                if (data.stockMarkets[shopName].playerData.playerBanks[player.userID].Count == 0)
                    data.stockMarkets[shopName].playerData.playerBanks.Remove(player.userID);
            }
            RedeemStorageAPI?.Call("AddItem", player.userID.Get(), config.storageName, outputItem);
            OpenBankManagement(player, shopName);
            if (config.enableLogs)
                Puts($"Player {player.displayName} ({player.UserIDString}) returned x{amount} {name} from bank.");
            PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("ItemReturned", player.UserIDString, amount, name, config.storageName), config.popUpFontSize, config.popUpLength);
            EffectNetwork.Send(new Effect("assets/prefabs/misc/easter/painted eggs/effects/eggpickup.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
        }

        private void TrySellFromBank(ulong playerId, string shopName, string listingKey)
        {
            BasePlayer player = BasePlayer.FindByID(playerId);
            if (!data.stockMarkets[shopName].playerData.playerBanks.ContainsKey(playerId) || !data.stockMarkets[shopName].playerData.playerBanks[playerId].ContainsKey(listingKey))
            {
                if (player != null)
                {
                    PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("NoItemInBank", player.UserIDString), config.popUpFontSize, config.popUpLength);
                    EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                }
                return;
            }
            ItemData bankItem = data.stockMarkets[shopName].playerData.playerBanks[playerId][listingKey];
            int amount = bankItem.amount;
            string[] split = listingKey.Split('-');
            string shortname = split[0];
            ulong skin = Convert.ToUInt64(split[1]);
            data.stockMarkets[shopName].playerData.playerBanks[playerId].Remove(listingKey);
            if (data.stockMarkets[shopName].playerData.playerBanks[playerId].Count == 0)
                data.stockMarkets[shopName].playerData.playerBanks.Remove(playerId);
            float bonus = 0;
            if (player == null && Artifacts != null)
                bonus = Artifacts.Call<float>("GetPriceBonus", playerId);
            else if (player != null && BonusCore != null)
                bonus = BonusCore.Call<float>("GetBonus", player, "Other_Price") * 100f;
            if (config.shops[shopName].stockConfig.sellPriceMultipliers.Count > 0)
            {
                foreach (var perm in config.shops[shopName].stockConfig.sellPriceMultipliers)
                {
                    if (permission.UserHasPermission(player.UserIDString, perm.Key))
                    {
                        bonus = perm.Value * 100f;
                        break;
                    }
                }
            }
            if (!data.stockMarkets[shopName].sellCache.ContainsKey(shortname) || !data.stockMarkets[shopName].sellCache[shortname].ContainsKey(skin))
                RollPrices(shopName, shortname, skin);

            ServerSellData ssd = data.stockMarkets[shopName].stockConfig.serverSell[shortname][skin];
            if (config.enableStockStats && ssd.maxDailySold > 0)
            {
                int maxToSell = ssd.maxDailySold;
                foreach (var perm in ssd.maxDailySoldPerms)
                {
                    if (perm.Value > maxToSell && permission.UserHasPermission(player.UserIDString, perm.Key))
                        maxToSell = perm.Value;
                }
                string displayName = ssd.displayName;
                string date = DateTime.Now.ToString("dd/MM/yyyy");
                data.stockMarkets[shopName].stats.playerDailyItems.TryAdd(playerId, new Dictionary<string, Dictionary<string, int>>());
                data.stockMarkets[shopName].stats.playerDailyItems[playerId].TryAdd(date, new Dictionary<string, int>());
                data.stockMarkets[shopName].stats.playerDailyItems[playerId][date].TryAdd(displayName, 0);
                if (data.stockMarkets[shopName].stats.playerDailyItems[playerId][date][displayName] + amount > maxToSell)
                {
                    if (player != null)
                    {
                        PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("DailyLimitSellReached", player.UserIDString, maxToSell), config.popUpFontSize, config.popUpLength);
                        EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                    }
                    return;
                }
            }
            int basicPrice = Mathf.FloorToInt(amount * data.stockMarkets[shopName].sellCache[shortname][skin].price);
            int sumPrice = basicPrice;
            if (bonus != 0 && !data.stockMarkets[shopName].stockConfig.blockedMultipliers.Contains(listingKey))
                sumPrice += Mathf.FloorToInt(basicPrice / 100f * bonus);
            GiveCurrency(shopName, playerId, sumPrice);
            data.stockMarkets[shopName].sellCache[shortname][skin].sellAmount += amount;
            if (ssd.priceParent != "")
            {
                string[] shortSkin = ssd.priceParent.Split('-');
                string parentShortname = shortSkin[0];
                ulong parentSkin = Convert.ToUInt64(shortSkin[1]);
                if (data.stockMarkets[shopName].sellCache.ContainsKey(parentShortname) && data.stockMarkets[shopName].sellCache[parentShortname].ContainsKey(parentSkin))
                {
                    if (!data.stockMarkets[shopName].sellCache.ContainsKey(parentShortname) || !data.stockMarkets[shopName].sellCache[parentShortname].ContainsKey(parentSkin))
                        RollPrices(shopName, parentShortname, parentSkin);
                    data.stockMarkets[shopName].sellCache[parentShortname][parentSkin].sellAmount += amount;
                }
            }
            if (config.enableStockStats)
            {
                string date = DateTime.Now.ToString("dd/MM/yyyy");
                string displayName = ssd.displayName;
                data.stockMarkets[shopName].stats.globalDaily.TryAdd(date, 0);
                data.stockMarkets[shopName].stats.globalDaily[date] += sumPrice;
                data.stockMarkets[shopName].stats.playerDaily.TryAdd(playerId, new Dictionary<string, int>());
                data.stockMarkets[shopName].stats.playerDaily[playerId].TryAdd(date, 0);
                data.stockMarkets[shopName].stats.playerDaily[playerId][date] += sumPrice;
                data.stockMarkets[shopName].stats.playerAll.TryAdd(playerId, 0);
                data.stockMarkets[shopName].stats.playerAll[playerId] += sumPrice;
                data.stockMarkets[shopName].stats.globalAllItems.TryAdd(displayName, 0);
                data.stockMarkets[shopName].stats.globalAllItems[displayName] += amount;
                data.stockMarkets[shopName].stats.globalDailyItems.TryAdd(date, new Dictionary<string, int>());
                data.stockMarkets[shopName].stats.globalDailyItems[date].TryAdd(displayName, 0);
                data.stockMarkets[shopName].stats.globalDailyItems[date][displayName] += amount;
                data.stockMarkets[shopName].stats.playerAllItems.TryAdd(playerId, new Dictionary<string, int>());
                data.stockMarkets[shopName].stats.playerAllItems[playerId].TryAdd(displayName, 0);
                data.stockMarkets[shopName].stats.playerAllItems[playerId][displayName] += amount;
                data.stockMarkets[shopName].stats.playerDailyItems.TryAdd(playerId, new Dictionary<string, Dictionary<string, int>>());
                data.stockMarkets[shopName].stats.playerDailyItems[playerId].TryAdd(date, new Dictionary<string, int>());
                data.stockMarkets[shopName].stats.playerDailyItems[playerId][date].TryAdd(displayName, 0);
                data.stockMarkets[shopName].stats.playerDailyItems[playerId][date][displayName] += amount;
            }
            string formattedPrice = FormatPrice(shopName, sumPrice);
            if (player != null)
            {
                string itemName = config.translateItems ? Lang(listingKey, player.UserIDString) : ssd.displayName;
                PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("BankItemsSold", player.UserIDString, amount, formattedPrice, itemName), config.popUpFontSize, config.popUpLength);
                EffectNetwork.Send(new Effect("assets/prefabs/misc/casino/slotmachine/effects/payout.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
            }
            if (config.enableLogs)
                Puts($"Player {playerId} sold x{amount} {shortname} from bank for {formattedPrice}.");
        }

        private void OnUseNPC(BasePlayer npc, BasePlayer player)
        {
            foreach (var shop in config.shops)
            {
                if (shop.Value.stockConfig.npcList.Contains(npc.UserIDString))
                {
                    shopNpc.TryAdd(player, null);
                    shopNpc[player] = npc;
                    OpenStockMarketUI(player, shop.Key);
                    break;
                }
                if (shop.Value.npcList.ContainsKey(npc.UserIDString))
                {
                    shopNpc.TryAdd(player, null);
                    shopNpc[player] = npc;
                    OpenShopUI(player, shop.Key);
                    break;
                }
            }
        }

        [ConsoleCommand("chart")]
        private void ChartCommand(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            ServerSellCacheData ssCache = data.stockMarkets["rp"].sellCache[arg.Args[0]][Convert.ToUInt64(arg.Args[1])];
            //Puts($"Price now: {ssCache.price}");
            ulong skin = Convert.ToUInt64(arg.Args[1]);
            int sellAmount = 0;
            if (arg.Args.Length >= 3)
                sellAmount = Convert.ToInt32(arg.Args[2]);
            ssCache.sellAmount = sellAmount;
            RollPrices("rp", arg.Args[0], skin);
            //Puts($"Price after: {ssCache.price}");

            //DateTime now = DateTime.Now;
            //ssCache.sellAmount = Convert.ToInt32(arg.Args[0]);
            //UpdateStockPrices("rp");
            //{
            //Puts($"PRICE: {ssCache.price}");
            //UpdateStockPrices("rp");
            if (arg.Args.Length == 4)
            {
                int counter = 0;
                float maxPrice = 0;
                float sumPrice = 0;
                float targetPrice = Convert.ToSingle(arg.Args[3]);
                bool canSkip = false;
                while (ssCache.price < targetPrice || !canSkip)
                {
                    ssCache.sellAmount = sellAmount;
                    RollPrices("rp", arg.Args[0], skin);
                    counter++;
                    sumPrice += ssCache.price;
                    if (ssCache.price > maxPrice)
                        maxPrice = ssCache.price;
                    if (ssCache.price > targetPrice && counter > 10)
                        canSkip = true;
                    if (counter > 10000) break;
                }
                //Puts($"AVG Price: {sumPrice / counter}");
                //Puts($"Max Price: {maxPrice}");
                Puts($"ROLLS: {counter}, PRICE: {maxPrice}");
            }
        }

        private void GeneratePopUpConfig()
        {
            JObject popUpConfig = new JObject()
            {
                { "key", "Market" },
                { "anchor", "0.5 1" },
                { "name", "Legacy" },
                { "parent", "Hud.Menu" },
                { "background_enabled", true },
                { "background_color", config.colors.color1 },
                { "background_fadeIn", 0.5f },
                { "background_fadeOut", 0.5f },
                { "background_offsetMax", "180 0" },
                { "background_offsetMin", "-180 -65" },
                { "background_smooth", false },
                { "background_url", "" },
                { "background_additionalObjectCount", 1 },
                { "background_detail_0_color", config.colors.color2 },
                { "background_detail_0_offsetMax", "356 65" },
                { "background_detail_0_offsetMin", "4 4" },
                { "background_detail_0_smooth", false },
                { "background_detail_0_url", "" },
                { "text_anchor", "MiddleCenter" },
                { "text_color", config.colors.textColor },
                { "text_fadeIn", 0.5f },
                { "text_fadeOut", 0.5f },
                { "text_font", "RobotoCondensed-Bold.ttf" },
                { "text_offsetMax", "180 0" },
                { "text_offsetMin", "-180 -65" },
                { "text_outlineColor", "0 0 0 0" },
                { "text_outlineSize", "0 0" }
            };
            PopUpAPI?.Call("AddNewPopUpSchema", Name, popUpConfig);
        }

        private void UpdateStockPrices(string shopName)
        {
            foreach (var shortname in data.stockMarkets[shopName].stockConfig.serverSell)
                foreach (var skin in shortname.Value)
                    RollPrices(shopName, shortname.Key, skin.Key);
            Puts($"Updated prices in {shopName} shop!");
            if (config.shops[shopName].stockConfig.chatMessPriceRoll)
                foreach (var player in BasePlayer.activePlayerList)
                    SendReply(player, Lang("PriceRolled", player.UserIDString, Lang(shopName, player.UserIDString)));
            UpdateWebAPI(shopName);
            foreach (var player in data.stockMarkets[shopName].alertData.ToList())
            {
                if (config.shops[shopName].stockConfig.bankPermission != "" && !permission.UserHasPermission(player.Key.ToString(), config.shops[shopName].stockConfig.bankPermission))
                {
                    data.stockMarkets[shopName].alertData.Remove(player.Key);
                    continue;
                }
                foreach (var item in player.Value.ToList())
                {
                    if (!data.stockMarkets[shopName].playerData.playerBanks.ContainsKey(player.Key) || !data.stockMarkets[shopName].playerData.playerBanks[player.Key].ContainsKey(item.Key)) continue;
                    string[] split = item.Key.Split('-');
                    string shortname = split[0];
                    ulong skin = Convert.ToUInt64(split[1]);
                    if (data.stockMarkets[shopName].sellCache.ContainsKey(shortname) && data.stockMarkets[shopName].sellCache[shortname].ContainsKey(skin))
                    {
                        if (item.Value.instaSellPrice != 0 && data.stockMarkets[shopName].sellCache[shortname][skin].price >= item.Value.instaSellPrice)
                        {
                            int amount = data.stockMarkets[shopName].playerData.playerBanks[player.Key][item.Key].amount;
                            TrySellFromBank(player.Key, shopName, item.Key);
                            string userId = player.Key.ToString();
                            if (config.discordMessages)
                            {
                                string itemName = config.translateItems ? Lang($"{shortname}-{skin}", userId) : data.stockMarkets[shopName].stockConfig.serverSell[shortname][skin].displayName;
                                DiscordCore?.Call("API_SendPrivateMessage", userId, Lang("InstaSellDiscordMessage", userId, amount, FormatPrice(shopName, data.stockMarkets[shopName].sellCache[shortname][skin].price), itemName));
                            }
                        }
                        else if (item.Value.alertPrice != 0 && data.stockMarkets[shopName].sellCache[shortname][skin].price >= item.Value.alertPrice)
                        {
                            BasePlayer bPlayer = BasePlayer.FindByID(player.Key);
                            if (bPlayer != null)
                            {
                                string itemName = config.translateItems ? Lang($"{shortname}-{skin}", bPlayer.UserIDString) : data.stockMarkets[shopName].stockConfig.serverSell[shortname][skin].displayName;
                                EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/invite_notice.prefab", bPlayer, 0, Vector3.zero, Vector3.up), bPlayer.net.connection);
                                PopUpAPI?.Call("ShowPopUp", bPlayer, "Market", Lang("AlertPricePopUp", bPlayer.UserIDString, FormatPrice(shopName, data.stockMarkets[shopName].sellCache[shortname][skin].price), itemName), config.popUpFontSize, config.popUpLength);
                            }
                            string userId = player.Key.ToString();
                            if (config.discordMessages)
                            {
                                string itemName = config.translateItems ? Lang($"{shortname}-{skin}", userId) : data.stockMarkets[shopName].stockConfig.serverSell[shortname][skin].displayName;
                                DiscordCore?.Call("API_SendPrivateMessage", userId, Lang("AlertDiscordMessage", userId, FormatPrice(shopName, data.stockMarkets[shopName].sellCache[shortname][skin].price), itemName));
                            }
                        }
                    }
                }
            }
            foreach (var player in stockPosition.ToList())
            {
                if (player.Key == null || !shopCache.ContainsKey(player.Key)) continue;
                if (player.Value.currentItem == "")
                    OpenStockMarketUI(player.Key, shopCache[player.Key].stockMarket.shopName, shopCache[player.Key].stockMarket.search, shopCache[player.Key].stockMarket.category, shopCache[player.Key].stockMarket.page, shopCache[player.Key].stockMarket.categoryPage);
                else
                    OpenStockItemInfoUI(player.Key, shopCache[player.Key].stockMarket.shopName, shopCache[player.Key].stockMarket.listing.key, shopCache[player.Key].stockMarket.listing.buyOffers, shopCache[player.Key].stockMarket.listing.selected, shopCache[player.Key].stockMarket.listing.index, shopCache[player.Key].stockMarket.listing.amount, shopCache[player.Key].stockMarket.listing.page);
            }
        }

        private void UpdateWebAPI(string shopName = "")
        {
            foreach (var shop in config.shops)
            {
                if (!shop.Value.stockConfig.enableWebApi) continue;
                if (shopName != "" && shop.Key != shopName) continue;
                Dictionary<string, float> items = new Dictionary<string, float>();
                foreach (var shortname in data.stockMarkets[shop.Key].sellCache)
                    foreach (var skin in shortname.Value)
                    {
                        string formatted = $"{shortname.Key}-{skin.Key}";
                        if (!itemDefinitions.ContainsKey(formatted)) continue;
                        items.TryAdd(itemDefinitions[formatted].displayName, skin.Value.price);
                    }
                var json = JsonConvert.SerializeObject(items);
                webrequest.Enqueue(shop.Value.stockConfig.webApiLink, $"plugindata={json}", (code, response) =>
                {
                    if (code != 200 || response == null)
                    {
                        Puts($"Market API Error Code: {code}");
                        return;
                    }
                    Puts($"Market API for {shop.Key} shop updated succesfully!");
                }, this, RequestMethod.POST);
            }
        }

        private static readonly bool debugPrices = false;

        private void RollPrices(string shopName, string shortname, ulong skin)
        {
            PriceChangeData ssCalc = data.stockMarkets[shopName].stockConfig.priceCalculations;
            ServerSellData ssData = data.stockMarkets[shopName].stockConfig.serverSell[shortname][skin];
            data.stockMarkets[shopName].sellCache.TryAdd(shortname, new Dictionary<ulong, ServerSellCacheData>());
            data.stockMarkets[shopName].sellCache[shortname].TryAdd(skin, new ServerSellCacheData());
            ServerSellCacheData ssCache = data.stockMarkets[shopName].sellCache[shortname][skin];
            if (debugPrices)
                Puts($"START {ssCache.sellAmount} {ssCache.cachedPrice} {ssCache.action}");
            if (ssData.minimalPrice == ssData.maximalPrice)
            {
                ssCache.sellAmountHistory.Insert(0, ssCache.sellAmount);
                ssCache.sellAmount = 0;
                ssCache.priceHistory.Insert(0, ssCache.price);
                ssCache.price = ssData.maximalPrice;
                ssCache.cachedPrice = ssData.maximalPrice;
                return;
            }
            float percentageOfCachedPrice = ((ssData.maximalPrice - ssData.minimalPrice) / 100f);
            if (debugPrices)
                Puts($"PRICE PERCENTAGE {percentageOfCachedPrice}");
            if (ssData.priceParent != "")
            {
                string[] shortnameAndSkin = ssData.priceParent.Split('-');
                string parentShortname = shortnameAndSkin[0];
                ulong parentSkin = Convert.ToUInt64(shortnameAndSkin[1]);
                float totalChildrenPrice = data.stockMarkets[shopName].sellCache[parentShortname][parentSkin].price + Core.Random.Range(ssData.priceBoostMin, ssData.priceBoostMax);
                if (totalChildrenPrice < ssData.minimalPrice)
                    totalChildrenPrice = ssData.minimalPrice + Core.Random.Range(0f, (percentageOfCachedPrice * ssCalc.regularCurve) / 3f);
                else if (totalChildrenPrice > ssData.maximalPrice)
                    totalChildrenPrice = ssData.maximalPrice - Core.Random.Range(0f, (percentageOfCachedPrice * ssCalc.regularCurve) / 3f);
                if (ssCache.cachedPrice < ssData.minimalPrice)
                    ssCache.cachedPrice = ssData.minimalPrice + Core.Random.Range(0f, (percentageOfCachedPrice * ssCalc.regularCurve) / 3f);
                else if (ssCache.cachedPrice > ssData.maximalPrice)
                    ssCache.cachedPrice = ssData.maximalPrice - Core.Random.Range(0f, (percentageOfCachedPrice * ssCalc.regularCurve) / 3f);
                ssCache.sellAmountHistory.Insert(0, ssCache.sellAmount);
                ssCache.sellAmount = 0;
                ssCache.priceHistory.Insert(0, ssCache.price);
                ssCache.price = totalChildrenPrice;
                if (debugPrices)
                    Puts($"PARENT PRICE {totalChildrenPrice} {ssCache.cachedPrice}");
                return;
            }
            ssCache.actionCount++;
            if (debugPrices)
                Puts($"pentalityCount {ssCache.pentalityCount}");
            if (ssCache.pentalityCount > 0)
                ssCache.pentalityCount--;
            if (debugPrices)
                Puts($"checking rolling new action {ssCache.action} with goal {ssCache.actionGoal}");
            if (ssCache.action == "" || ssCache.actionCount >= ssCache.actionGoal)
            {

                ssCache.actionCount = 0;
                ssCache.actionGoal = Core.Random.Range(ssCalc.sameActionsMin, ssCalc.sameActionsMax + 1);
                int chance = Core.Random.Range(0, 100);
                foreach (var priceBarrier in ssCalc.priceBarriers)
                {
                    if (debugPrices)
                        Puts($"{ssCache.cachedPrice} > {percentageOfCachedPrice} * {priceBarrier.Key} {percentageOfCachedPrice * priceBarrier.Key}");
                    if (ssCache.cachedPrice >= percentageOfCachedPrice * priceBarrier.Key)
                    {
                        if (chance < priceBarrier.Value)
                            ssCache.action = "Increase";
                        else
                            ssCache.action = "Decrease";
                        break;
                    }
                }
                if (debugPrices)
                    Puts($"rilled new action {ssCache.action} with goal {ssCache.actionGoal}");
            }
            float priceChange = 0;
            if (ssCache.action == "Increase" && ssCache.pentalityCount == 0)
            {
                float priceIncrease = Core.Random.Range(0f, percentageOfCachedPrice * ssCalc.regularCurve);
                priceChange += priceIncrease;
                if (debugPrices)
                    Puts($"regular price increased by {priceIncrease:0.###############}");
            }
            else if (ssCache.action == "Decrease")
            {
                float priceDecrease = Core.Random.Range(-(percentageOfCachedPrice * ssCalc.regularCurve), 0f);
                priceChange += priceDecrease;
                if (debugPrices)
                    Puts($"regular price decreased by {priceDecrease:0.###############}");
            }
            float pentalityMultiplier = 1;
            int playerCount = BasePlayer.activePlayerList.Count;
            float onlinePlayerAmountMultiplier = 1;
            foreach (var onlineCount in ssCalc.sellAmountOnlineMultiplier)
                if (playerCount > onlineCount.Key)
                {
                    onlinePlayerAmountMultiplier = onlineCount.Value;
                    break;
                }
            float percentageOfDefaultAmount = ssData.defaultAmount * onlinePlayerAmountMultiplier / 100f;
            if (debugPrices)
                Puts($"percentageOfDefaultAmount {percentageOfDefaultAmount} {onlinePlayerAmountMultiplier}");
            foreach (var sellAmount in ssCalc.priceDropChart)
            {
                if (ssCache.sellAmount > percentageOfDefaultAmount * sellAmount.Key)
                {
                    if (debugPrices)
                        Puts($"pentalityMultiplier of sell amount multiplied by  {sellAmount.Value}");
                    pentalityMultiplier *= sellAmount.Value;
                    break;
                }
            }
            foreach (var pentality in ssCalc.sellPricePentality)
            {
                if (ssCache.sellAmount > percentageOfDefaultAmount * pentality.Key)
                {
                    float cachedMaxPrice = ssData.maximalPrice - (ssData.maximalPrice / 100f * pentality.Value.percentage);
                    if (debugPrices)
                        Puts($"cachedMaxPrice {cachedMaxPrice} = {ssData.maximalPrice} {pentality.Value.percentage} {priceChange}");
                    if (ssCache.cachedPrice > cachedMaxPrice)
                        priceChange -= (ssCache.cachedPrice - cachedMaxPrice) * 1.5f;
                    if (debugPrices)
                        Puts($"price now {priceChange} pentality {pentality.Value.pentalityLength}");
                    ssCache.pentalityCount = pentality.Value.pentalityLength;
                    break;
                }
            }
            if (priceChange > 0)
            {
                foreach (var goal in ssCalc.goalAchievedChart)
                {
                    if (ssCache.sellAmount <= percentageOfDefaultAmount * goal.Key)
                    {
                        if (debugPrices)
                            Puts($"goal achieved {goal.Key} percent multiplied by {goal.Value}, before {priceChange:0.###############}");
                        priceChange *= goal.Value;
                        if (debugPrices)
                            Puts($"goal achieved {goal.Key} percent multiplied by {goal.Value}, now {priceChange:0.###############}");
                        break;
                    }
                }
            }
            float multiplierChance = 0;
            foreach (var chance in ssCalc.multplierAmountChance)
            {
                if (ssCache.sellAmount >= percentageOfDefaultAmount * chance.Key)
                {
                    if (debugPrices)
                        Puts($"multiplier set to {chance.Value} with chance {chance.Key}");
                    multiplierChance = chance.Value;
                    break;
                }
            }
            float summedPrice = ssCache.cachedPrice / pentalityMultiplier + priceChange;
            if (debugPrices)
                Puts($"sum and cached {summedPrice} {ssCache.cachedPrice} {pentalityMultiplier} {priceChange:0.###############}");
            ssCache.cachedPrice = summedPrice;
            if (ssCache.nextPossibleEvent <= 0 && multiplierChance > 0)
            {
                if (debugPrices)
                    Puts("Trying event");
                if (summedPrice > (ssCalc.positiveMinPrice * percentageOfCachedPrice) && summedPrice < (ssCalc.positiveMaxPrice * percentageOfCachedPrice) && multiplierChance > Core.Random.Range(0f, 100f))
                {
                    if (debugPrices)
                        Puts($"rolled positive");
                    int sumWeight = 0;
                    foreach (var multiplier in config.shops[shopName].stockConfig.multiplierEvents)
                        if (multiplier.Value.positiveEffect && ssCalc.positiveRandomEvents.Contains(multiplier.Key))
                            sumWeight += multiplier.Value.weight;
                    if (debugPrices)
                        Puts($"sumweight {sumWeight}");
                    int rolledWeight = Core.Random.Range(0, sumWeight + 1);
                    if (debugPrices)
                        Puts($"rolledWeight {rolledWeight}");
                    sumWeight = 0;
                    foreach (var multiplier in config.shops[shopName].stockConfig.multiplierEvents)
                    {
                        if (!multiplier.Value.positiveEffect || !ssCalc.positiveRandomEvents.Contains(multiplier.Key)) continue;
                        sumWeight += multiplier.Value.weight;
                        if (sumWeight > rolledWeight)
                        {
                            ssCache.bonusMultiplierLength = Core.Random.Range(ssCalc.multiplierMinLength, ssCalc.multiplierMaxLength + 1);
                            ssCache.bonusMultiplier = Core.Random.Range(multiplier.Value.minMultiplier, multiplier.Value.maxMultiplier);
                            ssCache.nextPossibleEvent = ssCalc.minTimeDistance;
                            if (debugPrices)
                                Puts($"BONUS {multiplier.Key} {ssData.displayName} {ssCache.nextPossibleEvent} {ssCache.bonusMultiplier} {ssCache.bonusMultiplierLength}");
                            int percentage = ssCache.bonusMultiplier < 1 ? Mathf.FloorToInt((1f - ssCache.bonusMultiplier) * 100f) : Mathf.FloorToInt((ssCache.bonusMultiplier - 1f) * 100f);
                            if (config.demandsChannelId != "0")
                                DiscordCore?.Call("API_SendMessage", config.demandsChannelId, Lang($"Event_{multiplier.Key}", null, ssData.displayName, percentage));
                            if (config.broadcastDemands)
                                foreach (var player in BasePlayer.activePlayerList)
                                    SendReply(player, Lang($"Event_{multiplier.Key}", player.UserIDString, ssData.displayName, percentage));
                            break;
                        }
                    }
                }
                else if (summedPrice > (ssCalc.negativeMinPrice * percentageOfCachedPrice) && summedPrice < (ssCalc.negativeMaxPrice * percentageOfCachedPrice) && multiplierChance < Core.Random.Range(0f, 100f))
                {
                    if (debugPrices)
                        Puts("rolled negative");
                    int sumWeight = 0;
                    foreach (var multiplier in config.shops[shopName].stockConfig.multiplierEvents)
                        if (!multiplier.Value.positiveEffect && ssCalc.negativeRandomEvents.Contains(multiplier.Key))
                            sumWeight += multiplier.Value.weight;
                    if (debugPrices)
                        Puts($"sumweight {sumWeight}");
                    int rolledWeight = Core.Random.Range(0, sumWeight + 1);
                    if (debugPrices)
                        Puts($"rolledWeight {rolledWeight}");
                    sumWeight = 0;
                    foreach (var multiplier in config.shops[shopName].stockConfig.multiplierEvents)
                    {
                        if (multiplier.Value.positiveEffect || !ssCalc.negativeRandomEvents.Contains(multiplier.Key)) continue;
                        sumWeight += multiplier.Value.weight;
                        if (sumWeight > rolledWeight)
                        {
                            ssCache.bonusMultiplierLength = Core.Random.Range(ssCalc.multiplierMinLength, ssCalc.multiplierMaxLength + 1);
                            ssCache.bonusMultiplier = Core.Random.Range(multiplier.Value.minMultiplier, multiplier.Value.maxMultiplier);
                            ssCache.nextPossibleEvent = ssCalc.minTimeDistance;
                            if (debugPrices)
                                Puts($"BONUS {multiplier.Key} {ssData.displayName} {ssCache.nextPossibleEvent} {ssCache.bonusMultiplier} {ssCache.bonusMultiplierLength}");
                            int percentage = ssCache.bonusMultiplier < 1 ? Mathf.FloorToInt((1f - ssCache.bonusMultiplier) * 100f) : Mathf.FloorToInt((ssCache.bonusMultiplier - 1f) * 100f);
                            if (config.demandsChannelId != "0")
                                DiscordCore?.Call("API_SendMessage", config.demandsChannelId, Lang($"Event_{multiplier.Key}", null, ssData.displayName, percentage));
                            if (config.broadcastDemands)
                                foreach (var player in BasePlayer.activePlayerList)
                                    SendReply(player, Lang($"Event_{multiplier.Key}", player.UserIDString, ssData.displayName, percentage));
                            break;
                        }
                    }
                }
            }
            else
                ssCache.nextPossibleEvent--;
            if (debugPrices)
            {
                Puts($"ssCache.nextPossibleEvent {ssCache.nextPossibleEvent}");
                Puts($"ssCache.bonusMultiplierLength before {ssCache.bonusMultiplierLength} {ssCache.bonusMultiplier}");
            }
            if (ssCache.bonusMultiplierLength > 0)
                ssCache.bonusMultiplierLength--;
            else
                ssCache.bonusMultiplier = 1;
            if (debugPrices)
                Puts($"ssCache.bonusMultiplierLength after {ssCache.bonusMultiplierLength} {ssCache.bonusMultiplier}");
            float totalPrice = summedPrice * ssCache.bonusMultiplier;
            if (debugPrices)
                Puts($"totalPrice before {totalPrice}");
            if (totalPrice < ssData.minimalPrice)
                totalPrice = ssData.minimalPrice + Core.Random.Range(0f, (percentageOfCachedPrice * ssCalc.regularCurve) / 3f);
            else if (totalPrice > ssData.maximalPrice)
                totalPrice = ssData.maximalPrice - Core.Random.Range(0f, (percentageOfCachedPrice * ssCalc.regularCurve) / 3f);
            if (debugPrices)
            {
                Puts($"totalPrice after {totalPrice}");
                Puts($"cachedPrice before {ssCache.cachedPrice}");
            }
            if (ssCache.cachedPrice < ssData.minimalPrice)
                ssCache.cachedPrice = ssData.minimalPrice + Core.Random.Range(0f, (percentageOfCachedPrice * ssCalc.regularCurve) / 3f);
            else if (ssCache.cachedPrice > ssData.maximalPrice)
                ssCache.cachedPrice = ssData.maximalPrice - Core.Random.Range(0f, (percentageOfCachedPrice * ssCalc.regularCurve) / 3f);
            if (debugPrices)
                Puts($"cachedPrice after {ssCache.cachedPrice}");
            ssCache.sellAmountHistory.Insert(0, ssCache.sellAmount);
            ssCache.sellAmount = 0;
            ssCache.priceHistory.Insert(0, ssCache.price);
            ssCache.price = totalPrice;
            if (debugPrices)
                Puts($"NEW {ssCache.price} {priceChange:0.###############} {pentalityMultiplier} {ssCache.bonusMultiplier} {ssCache.nextPossibleEvent}");
        }

        private void UpdateStockItemNames(BasePlayer player)
        {
            if (!privFeatures) return;
            bool changed = false;
            foreach (var item in player.inventory.containerMain.itemList)
            {
                if (item.skin == 0) continue;
                if (item.name == null || item.name == "") continue;
                string key = $"{item.info.shortname}-{item.skin}";
                if (data.stockMarkets["rp"].stockConfig.customItems.ContainsKey(key))
                {
                    if (item.name != data.stockMarkets["rp"].stockConfig.customItems[key].displayName)
                    {
                        data.stockMarkets["rp"].stockConfig.customItems[key].displayName = item.name;
                        changed = true;
                    }
                }
            }
            foreach (var item in player.inventory.containerBelt.itemList)
            {
                if (item.skin == 0) continue;
                if (item.name == null || item.name == "") continue;
                string key = $"{item.info.shortname}-{item.skin}";
                if (data.stockMarkets["rp"].stockConfig.customItems.ContainsKey(key))
                {
                    if (item.name != data.stockMarkets["rp"].stockConfig.customItems[key].displayName)
                    {
                        data.stockMarkets["rp"].stockConfig.customItems[key].displayName = item.name;
                        changed = true;
                    }
                }
            }
            foreach (var item in player.inventory.containerWear.itemList)
            {
                if (item.skin == 0) continue;
                if (item.name == null || item.name == "") continue;
                string key = $"{item.info.shortname}-{item.skin}";
                if (data.stockMarkets["rp"].stockConfig.customItems.ContainsKey(key))
                {
                    if (item.name != data.stockMarkets["rp"].stockConfig.customItems[key].displayName)
                    {
                        data.stockMarkets["rp"].stockConfig.customItems[key].displayName = item.name;
                        changed = true;
                    }
                }
            }
            if (changed)
                SaveData(true, "rp");
        }

        private void AddCustomStockItems(BasePlayer player, string category)
        {
            int count = 0;
            foreach (var item in player.inventory.containerMain.itemList)
            {
                if (item.skin == 0) continue;
                if (item.name == null || item.name == "") continue;
                string key = $"{item.info.shortname}-{item.skin}";
                if (data.stockMarkets["rp"].stockConfig.customItems.ContainsKey(key)) continue;
                data.stockMarkets["rp"].stockConfig.customItems.Add(key, new CustomItemData()
                {
                    shortname = item.info.shortname,
                    skin = item.skin,
                    amount = 1,
                    category = category,
                    displayName = item.name
                });
                count++;
            }
            foreach (var item in player.inventory.containerBelt.itemList)
            {
                if (item.skin == 0) continue;
                if (item.name == null || item.name == "") continue;
                string key = $"{item.info.shortname}-{item.skin}";
                if (data.stockMarkets["rp"].stockConfig.customItems.ContainsKey(key)) continue;
                data.stockMarkets["rp"].stockConfig.customItems.Add(key, new CustomItemData()
                {
                    shortname = item.info.shortname,
                    skin = item.skin,
                    amount = 1,
                    category = category,
                    displayName = item.name
                });
                count++;
            }
            foreach (var item in player.inventory.containerWear.itemList)
            {
                if (item.skin == 0) continue;
                if (item.name == null || item.name == "") continue;
                string key = $"{item.info.shortname}-{item.skin}";
                if (data.stockMarkets["rp"].stockConfig.customItems.ContainsKey(key)) continue;
                data.stockMarkets["rp"].stockConfig.customItems.Add(key, new CustomItemData()
                {
                    shortname = item.info.shortname,
                    skin = item.skin,
                    amount = 1,
                    category = category,
                    displayName = item.name
                });
                count++;
            }
            SaveData(true, "rp");
            Puts($"Added {count} new items to stock market!");
        }

        private PermissionData GetMaxListingAmount(BasePlayer player)
        {
            foreach (var perm in config.listingPermissions)
                if (permission.UserHasPermission(player.UserIDString, perm.Key))
                    return perm.Value;
            return null;
        }

        private string FormatCurrency(string shopName, BasePlayer player)
        {
            float amount = GetCurrencyAmount(shopName, player);
            return FormatPrice(shopName, amount);
        }

        private static string FormatTime(int minutes)
        {
            if (minutes < 60)
                return $"{minutes}m";
            else
            {
                int remainingMinutes = minutes % 60;
                int hours = ((minutes - remainingMinutes) / 60);
                if (hours < 24)
                {
                    if (remainingMinutes > 0)
                        return $"{hours}h {minutes}m";
                    else
                        return $"{hours}h";
                }
                else
                {
                    int remainingHours = hours % 24;
                    int days = ((hours - remainingHours) / 24);
                    if (remainingHours > 0)
                        return $"{days}d {hours}h";
                    else
                        return $"{days}d";
                }
            }
        }

        private int GetCurrencyAmount(string shopName, BasePlayer player) => GetCurrencyAmount(shopName, player.userID);

        private int GetCurrencyAmount(string shopName, ulong userId)
        {
            string otherCurr = config.shops[shopName].otherPluginCurrency.ToLower();
            int itemAmount = 0;
            if (config.shops[shopName].countDepositFromInventory && !string.IsNullOrEmpty(config.shops[shopName].depositItem.shortname))
            {
                BasePlayer player = BasePlayer.FindByID(userId);
                if (player != null)
                {
                    foreach (var item in player.inventory.containerMain.itemList)
                        if (item.info.shortname == config.shops[shopName].depositItem.shortname && item.skin == config.shops[shopName].depositItem.skin)
                            itemAmount += item.amount;
                    foreach (var item in player.inventory.containerBelt.itemList)
                        if (item.info.shortname == config.shops[shopName].depositItem.shortname && item.skin == config.shops[shopName].depositItem.skin)
                            itemAmount += item.amount;
                }
            }
            if (otherCurr == "")
            {
                if (!data.shops.ContainsKey(shopName) || !data.shops[shopName].users.ContainsKey(userId))
                    return itemAmount;
                return itemAmount + data.shops[shopName].users[userId].currencyAmount;
            }
            else
            {
                if ((otherCurr == "serverrewards" || otherCurr == "server rewards") && ServerRewards != null)
                    return itemAmount + ServerRewards.Call<int>("CheckPoints", userId);
                else if (otherCurr == "economics" && Economics != null)
                    return itemAmount + (int)Economics.Call<double>("Balance", userId);
                else if (otherCurr == "iqeconomic" && IQEconomic != null)
                    return itemAmount + IQEconomic.Call<int>("API_GET_BALANCE", userId);
                else if (otherCurr == "banksystem" && BankSystem != null)
                    return itemAmount + BankSystem.Call<int>("Balance", userId);
                else
                    return itemAmount;
            }
        }

        private bool TakeCurrency(string shopName, BasePlayer player, int amount) => TakeCurrency(shopName, player.userID, amount);

        private bool TakeCurrency(string shopName, ulong userId, int amount, bool force = false)
        {
            int itemAmount = 0;
            if (config.shops[shopName].countDepositFromInventory && !string.IsNullOrEmpty(config.shops[shopName].depositItem.shortname))
            {
                BasePlayer player = BasePlayer.FindByID(userId);
                if (player != null)
                {
                    foreach (var item in player.inventory.containerMain.itemList)
                        if (item.info.shortname == config.shops[shopName].depositItem.shortname && item.skin == config.shops[shopName].depositItem.skin)
                            itemAmount += item.amount;
                    foreach (var item in player.inventory.containerBelt.itemList)
                        if (item.info.shortname == config.shops[shopName].depositItem.shortname && item.skin == config.shops[shopName].depositItem.skin)
                            itemAmount += item.amount;
                    if (itemAmount >= amount)
                    {
                        int takenAmount = 0;
                        List<Item> invItems = Pool.Get<List<Item>>();
                        invItems.AddRange(player.inventory.containerMain.itemList);
                        invItems.AddRange(player.inventory.containerBelt.itemList);
                        foreach (var item in invItems)
                        {
                            if (item.info.shortname == config.shops[shopName].depositItem.shortname && item.skin == config.shops[shopName].depositItem.skin)
                            {
                                if (item.amount <= amount - takenAmount)
                                {
                                    takenAmount += item.amount;
                                    item.GetHeldEntity()?.Kill();
                                    item.DoRemove();
                                }
                                else if (item.amount > amount - takenAmount)
                                {
                                    item.amount -= amount - takenAmount;
                                    item.MarkDirty();
                                    break;
                                }
                            }
                        }
                        Pool.FreeUnmanaged(ref invItems);
                        return true;
                    }
                    else if (itemAmount > 0 && data.shops[shopName].users.ContainsKey(userId) && data.shops[shopName].users[userId].currencyAmount < amount && data.shops[shopName].users[userId].currencyAmount + itemAmount > amount)
                    {
                        EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/invite_notice.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                        PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("NotEnoughCurrencySplit", player.UserIDString), config.popUpFontSize, config.popUpLength);
                        return false;
                    }
                }
            }
            string otherCurr = config.shops[shopName].otherPluginCurrency.ToLower();
            if (otherCurr == "")
            {
                if (force)
                {
                    if (!data.shops[shopName].users.ContainsKey(userId))
                        data.shops[shopName].users.Add(userId, new UserData() { username = "Force Created User" });
                    data.shops[shopName].users[userId].currencyAmount -= amount;
                    return true;
                }
                if (!data.shops.ContainsKey(shopName) || !data.shops[shopName].users.ContainsKey(userId))
                    return false;
                if (data.shops[shopName].users[userId].currencyAmount >= amount)
                {
                    data.shops[shopName].users[userId].currencyAmount -= amount;
                    return true;
                }
                else return false;
            }
            else
            {
                if ((otherCurr == "serverrewards" || otherCurr == "server rewards") && ServerRewards != null)
                {
                    if (ServerRewards.Call<int>("CheckPoints", userId) >= amount)
                    {
                        ServerRewards.Call("TakePoints", userId, amount);
                        return true;
                    }
                    else return false;
                }
                else if (otherCurr == "economics" && Economics != null)
                {
                    if (Economics.Call<double>("Balance", userId) >= amount)
                    {
                        Economics.Call("Withdraw", userId, (double)amount);
                        return true;
                    }
                    else return false;
                }
                else if (otherCurr == "iqeconomic" && IQEconomic != null)
                {
                    if (IQEconomic.Call<int>("API_GET_BALANCE", userId) >= amount)
                    {
                        IQEconomic.Call("API_REMOVE_BALANCE", userId, amount);
                        return true;
                    }
                    else return false;
                }
                else if (otherCurr == "banksystem" && BankSystem != null)
                {
                    if (BankSystem.Call<int>("Balance", userId) >= amount)
                    {
                        BankSystem.Call("Withdraw", userId, amount);
                        return true;
                    }
                    else return false;
                }
                else
                    return false;
            }
        }

        private void GiveCurrency(string shopName, ulong sellerId, int amount)
        {
            string otherCurr = config.shops[shopName].otherPluginCurrency.ToLower();
            if (otherCurr == "")
            {
                if (!data.shops[shopName].users.ContainsKey(sellerId))
                {
                    IPlayer player = covalence.Players.FindPlayerById(sellerId.ToString());
                    if (player == null)
                    {
                        Puts($"Player {sellerId} has not been in server and plugin tries to give him currency. Some problems?");
                        return;
                    }
                    data.shops[shopName].users.Add(sellerId, new UserData() { username = player.Name });
                }
                if (config.shops[shopName].currencyToInventory && config.shops[shopName].depositItem.shortname != "")
                {
                    BasePlayer currencyPlayer = BasePlayer.FindByID(sellerId);
                    if (currencyPlayer != null)
                    {
                        Item depositItem = ItemManager.CreateByName(config.shops[shopName].depositItem.shortname, amount, config.shops[shopName].depositItem.skin);
                        if (config.shops[shopName].depositItem.displayName != "")
                            depositItem.name = config.shops[shopName].depositItem.displayName;
                        if (!depositItem.MoveToContainer(currencyPlayer.inventory.containerMain))
                            if (!depositItem.MoveToContainer(currencyPlayer.inventory.containerBelt))
                                depositItem.Drop(currencyPlayer.eyes.position, Vector3.zero);
                    }
                    else
                        data.shops[shopName].users[sellerId].currencyAmount += amount;
                }
                else
                    data.shops[shopName].users[sellerId].currencyAmount += amount;
            }
            else
            {
                if ((otherCurr == "serverrewards" || otherCurr == "server rewards") && ServerRewards != null)
                    ServerRewards.Call("AddPoints", sellerId, amount);
                else if (otherCurr == "economics" && Economics != null)
                    Economics.Call("Deposit", sellerId, (double)amount);
                else if (otherCurr == "iqeconomic" && IQEconomic != null)
                {
                    int balance = IQEconomic.Call<int>("API_GET_BALANCE", sellerId);
                    IQEconomic.Call("API_SET_BALANCE", sellerId, balance + amount);
                }
                else if (otherCurr == "banksystem" && BankSystem != null)
                    BankSystem.Call("Deposit", sellerId, amount);
            }
        }

        private static string FormatPrice(string shopName, float price, bool rounded = false)
        {
            if (shopName != "" && !config.shops[shopName].formatCurrency)
                return string.Format(config.shops[shopName].symbol, price.ToString());
            string priceText = "";
            if (price >= 1000000000)
            {
                if (price > 100000000000)
                    priceText = $"{(price / 1000000000f):0.#}T";
                else if (price > 10000000000)
                    priceText = $"{(price / 1000000000f):0.##}T";
                else
                    priceText = $"{(price / 1000000000f):0.###}T";
            }
            else if (price >= 1000000)
            {
                if (price > 100000000)
                    priceText = $"{(price / 1000000f):0.#}M";
                else if (price > 10000000)
                    priceText = $"{(price / 1000000f):0.##}M";
                else
                    priceText = $"{(price / 1000000f):0.###}M";
            }
            else if (price >= 1000)
            {
                if (price > 100000)
                    priceText = $"{(price / 1000f):0.#}k";
                else if (price > 10000)
                    priceText = $"{(price / 1000f):0.##}k";
                else
                    priceText = $"{(price / 1000f):0.###}k";
            }
            else if (price >= 1)
            {
                if (rounded)
                    priceText = $"{price:0}";
                else
                    priceText = $"{price:0.###}";
            }
            else if (price < 1)
            {
                if (rounded)
                    priceText = $"{price:0}";
                else
                    priceText = $"{price:0.####}";
            }
            if (shopName == "")
                return priceText;
            else
                return string.Format(config.shops[shopName].symbol, priceText);
        }

        private void OpenShopSelectUI(BasePlayer player)
        {
            stockPosition.Remove(player);
            if (config.debug) Puts($"[DEBUG] Opening shop select UI.");
            int shopCount = 0;
            string lastShop = "";
            foreach (var shop in config.shops)
                if ((shop.Value.permission != "" && permission.UserHasPermission(player.UserIDString, shop.Value.permission)) || shop.Value.permission == "")
                {
                    shopCount++;
                    lastShop = shop.Key;
                }
            if (shopCount == 0)
            {
                foreach (var shop in config.shops.Values)
                    if (shop.stockConfig.canStockMarket)
                    {
                        OpenStockMarketSelectUI(player);
                        return;
                    }
                SendReply(player, Lang("NoShopPermission", player.UserIDString));
                return;
            }
            else if (shopCount == 1)
            {
                OpenShopUI(player, lastShop);
                return;
            }
            if (config.debug) Puts($"[DEBUG] Loading container.");
            CuiElementContainer container = new CuiElementContainer();
            if (config.clickingSound)
                EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/notice/loot.copy.fx.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
            if (!openedUis.Contains(player))
            {
                UI_AddAnchor(container, "Market_AnchorUI", "Hud.Menu", "0 0", "1 1"); //Market_MainUI
                UI_AddBlurPanel(container, "Market_AnchorUI", "0.3 0.3 0.3 0.2"); //Panel_BackgroundBlur
                UI_AddPanel(container, "Market_AnchorUI", "0.4 0.4 0.4 0.3", "0 0", "0 0", "0 0", "1 1"); //Panel_BackgroundColor
                UI_AddVignettePanel(container, "Market_AnchorUI", "0 0 0 0.8"); //Panel_Vignette
                UI_AddButton(container, "Market_AnchorUI", "0 0 0 0", "UI_ShoppyStock close", "0 0", "0 0", null, "0 0", "1 1"); //Button_BackgroundClose
                openedUis.Add(player);
                EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/notice/item.select.fx.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
            }
            UI_AddAnchor(container, "Market_MainUI", "Market_AnchorUI", "0.5 0.5", "0.5 0.5"); //Market_MainUI
            UI_AddPanel(container, "Market_MainUI", config.colors.color1, "-529 -287", "529 287"); //Panel_Background
            UI_AddPanel(container, "Market_MainUI", config.colors.color2, "-525 235", "525 283"); //Panel_TopPanel
            int startX = -525;
            if (config.shopButton)
            {
                UI_AddButton(container, "Market_MainUI", config.colors.color2, "UI_ShoppyStock shops", $"{startX} 235", $"{startX + 200} 283", "Market_MainUI_Button"); //Button_Shops
                UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ShopsButton", player.UserIDString), TextAnchor.MiddleCenter, 20, "0 0", "200 48"); //Text_Shops
                startX += 200;
            }
            if (config.stockButton)
            {
                UI_AddButton(container, "Market_MainUI", config.colors.color2, "UI_ShoppyStock stock", $"{startX} 235", $"{startX + 200} 283", "Market_MainUI_Button"); //Button_StockMarket
                UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("StockButton", player.UserIDString), TextAnchor.MiddleCenter, 20, "0 0", "200 48"); //Text_Stock
                startX += 200;
            }
            if (config.transferButton)
            {
                UI_AddButton(container, "Market_MainUI", config.colors.color2, "UI_ShoppyStock transfer", $"{startX} 235", $"{startX + 200} 283", "Market_MainUI_Button"); //Button_Transfer
                UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("TransferButton", player.UserIDString), TextAnchor.MiddleCenter, 20, "0 0", "200 48"); //Text_Transfer
            }
            UI_AddButton(container, "Market_MainUI", config.colors.negativeDark, "UI_ShoppyStock close", "477 235", "525 283", "Market_MainUI_Button"); //Button_Close
            UI_AddText(container, "Market_MainUI_Button", config.colors.negativeLight, "RobotoCondensed-Bold.ttf", "✖", TextAnchor.MiddleCenter, 30, "0 0", "48 48"); //Text_Close
            UI_AddPanel(container, "Market_MainUI", config.colors.color3, "-525 193", "525 231"); //Panel_HintPanel
            UI_AddIcon(container, "Market_MainUI", config.colors.textColor, "-521 195", "-487 229", "assets/icons/info.png"); //Image_CurrencyIcon
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("SelectShopHint", player.UserIDString), TextAnchor.MiddleLeft, 20, "-483 193", "525 231"); //Text_ShopHint
            if (config.debug) Puts($"[DEBUG] Starting listing shops.");
            List<int> widths = new List<int>() { 172, 172, 171, 171, 172, 172 };
            int widthStart = -525;
            int shops = config.shops.Count - 1;
            int shopCounter = 0;
            for (int i = 0; i < 6; i += 0)
            {
                bool found = false;
                if (shopCounter <= shops)
                {
                    KeyValuePair<string, ShopConfig> shop = config.shops.ElementAt(shopCounter);
                    if (shop.Value.permission == "" || (shop.Value.permission != "" && permission.UserHasPermission(player.UserIDString, shop.Value.permission)))
                    {
                        UI_AddPanel(container, "Market_MainUI", config.colors.color4, $"{widthStart} 17", $"{widthStart + 172} 189"); //Panel_CurrencyIconBackground
                        string iconShortname = shop.Value.iconUrl == "" ? "UI_ShoppyStock_DefaultShopIcon" : $"UI_ShoppyStock_{shop.Key}_Icon";
                        UI_AddImage(container, "Market_MainUI", iconShortname, 0, $"{widthStart + 12} 29", $"{widthStart + 160} 177", config.colors.textColor); //Image_CurrencyIcon
                        UI_AddPanel(container, "Market_MainUI", config.colors.color2, $"{widthStart} -13", $"{widthStart + 172} 13"); //Panel_CurrencyNameBackground
                        UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang(shop.Key, player.UserIDString), TextAnchor.MiddleCenter, 14, $"{widthStart} -13", $"{widthStart + 172} 13"); //Text_ShopCurrencyName
                        UI_AddPanel(container, "Market_MainUI", config.colors.color2, $"{widthStart} -43", $"{widthStart + 172} -17"); //Panel_CurrencyTitleBackground
                        UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("Amount", player.UserIDString), TextAnchor.MiddleCenter, 14, $"{widthStart} -43", $"{widthStart + 97} -17"); //Text_CurrencyAmountTitle
                        UI_AddPanel(container, "Market_MainUI", config.colors.color4, $"{widthStart + 97} -43", $"{widthStart + 172} -17"); //Panel_CurrencyAmountBackground
                        UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", FormatCurrency(shop.Key, player), TextAnchor.MiddleCenter, 14, $"{widthStart + 97} -43", $"{widthStart + 172} -17"); //Text_CurrencyAmount
                        UI_AddPanel(container, "Market_MainUI", config.colors.color2, $"{widthStart} -231", $"{widthStart + 172} -47"); //Panel_DescriptionBackground
                        UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang($"{shop.Key}_Description", player.UserIDString), TextAnchor.UpperLeft, 10, $"{widthStart + 4} -227", $"{widthStart + 168} -51"); //Text_CurrencyDescription
                        UI_AddButton(container, "Market_MainUI", config.colors.color2, $"UI_ShoppyStock open {shop.Key}", $"{widthStart} -283", $"{widthStart + 172} -235", "Market_MainUI_Button"); //Button_OpenShop
                        UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("OpenShopButton", player.UserIDString), TextAnchor.MiddleCenter, 20, "0 0", "172 48"); //Text_OpenShop
                    }
                    else
                        found = true;
                }
                else
                    UI_AddPanel(container, "Market_MainUI", config.colors.color4, $"{widthStart} -283", $"{widthStart + 172} 189"); //Panel_CurrencyEmpty
                if (!found)
                {
                    widthStart += widths[i] + 4;
                    i++;
                }
                shopCounter++;
                if (shopCounter > 20) break;
            }
            CuiHelper.DestroyUi(player, "Market_MainUI");
            CuiHelper.AddUi(player, container);
        }

        private void OpenShopUI(BasePlayer player, string shopName, string search = "", string category = "", int categoryPage = 1, int page = 1)
        {
            stockPosition.Remove(player);
            bool affordable = false;
            if (shopCache.ContainsKey(player))
                affordable = shopCache[player].affordableOnly;
            if (categoryPage < 1)
                categoryPage = 1;
            if (page < 1)
                page = 1;
            shopCache.TryAdd(player, new ShopCache());
            shopCache[player] = new ShopCache() { categoryPage = categoryPage, currentShop = shopName, page = page, shopCategory = category, affordableOnly = affordable, searchValue = search, lastVisit = "shop" };
            if (config.debug) Puts($"[DEBUG] Opening shop UI.");
            CuiElementContainer container = new CuiElementContainer();
            if (config.clickingSound)
                EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/notice/loot.copy.fx.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
            if (!openedUis.Contains(player))
            {
                UI_AddAnchor(container, "Market_AnchorUI", "Hud.Menu", "0 0", "1 1"); //Market_MainUI
                UI_AddBlurPanel(container, "Market_AnchorUI", "0.3 0.3 0.3 0.2"); //Panel_BackgroundBlur
                UI_AddPanel(container, "Market_AnchorUI", "0.4 0.4 0.4 0.3", "0 0", "0 0", "0 0", "1 1"); //Panel_BackgroundColor
                UI_AddVignettePanel(container, "Market_AnchorUI", "0 0 0 0.8"); //Panel_Vignette
                UI_AddButton(container, "Market_AnchorUI", "0 0 0 0", "UI_ShoppyStock close", "0 0", "0 0", null, "0 0", "1 1"); //Button_BackgroundClose
                openedUis.Add(player);
                EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/notice/item.select.fx.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
            }
            if (config.debug) Puts($"[DEBUG] Checked anchor.");
            UI_AddAnchor(container, "Market_MainUI", "Market_AnchorUI", "0.5 0.5", "0.5 0.5"); //Market_MainUI
            UI_AddPanel(container, "Market_MainUI", config.colors.color1, "-529 -287", "529 287"); //Panel_Background
            UI_AddPanel(container, "Market_MainUI", config.colors.color2, "-525 235", "525 283"); //Panel_TopPanel
            bool npcShop = shopNpc.ContainsKey(player);
            if (!npcShop)
            {
                int startX = -525;
                if (config.shopButton)
                {
                    UI_AddButton(container, "Market_MainUI", config.colors.color2, "UI_ShoppyStock shops", $"{startX} 235", $"{startX + 200} 283", "Market_MainUI_Button"); //Button_Shops
                    UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ShopsButton", player.UserIDString), TextAnchor.MiddleCenter, 20, "0 0", "200 48"); //Text_Shops
                    startX += 200;
                }
                if (config.stockButton)
                {
                    UI_AddButton(container, "Market_MainUI", config.colors.color2, "UI_ShoppyStock stock", $"{startX} 235", $"{startX + 200} 283", "Market_MainUI_Button"); //Button_StockMarket
                    UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("StockButton", player.UserIDString), TextAnchor.MiddleCenter, 20, "0 0", "200 48"); //Text_Stock
                    startX += 200;
                }
                if (config.transferButton)
                {
                    UI_AddButton(container, "Market_MainUI", config.colors.color2, "UI_ShoppyStock transfer", $"{startX} 235", $"{startX + 200} 283", "Market_MainUI_Button"); //Button_Transfer
                    UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("TransferButton", player.UserIDString), TextAnchor.MiddleCenter, 20, "0 0", "200 48"); //Text_Transfer
                }
            }
            else
                UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", shopNpc[player].displayName, TextAnchor.MiddleLeft, 30, "-518 235", "525 283"); //Text_NpcUsername
            UI_AddButton(container, "Market_MainUI", config.colors.negativeDark, "UI_ShoppyStock close", "477 235", "525 283", "Market_MainUI_Button"); //Button_Close
            UI_AddText(container, "Market_MainUI_Button", config.colors.negativeLight, "RobotoCondensed-Bold.ttf", "✖", TextAnchor.MiddleCenter, 30, "0 0", "48 48"); //Text_Close
            UI_AddPanel(container, "Market_MainUI", config.colors.color3, "-525 193", "525 231"); //Panel_ControlPanel
            UI_AddPanel(container, "Market_MainUI", config.colors.color2, "-517 201", "-225 223"); //Panel_SearchBar
            UI_AddImage(container, "Market_MainUI", "UI_ShoppyStock_Search", 0, "-243 205", "-229 219", config.colors.textColor); //Image_Search
            UI_AddInput(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", TextAnchor.MiddleLeft, 13, search, 24, $"UI_ShoppyStock search", "-513 201", "-225 223"); //Input_SearchField
            if (config.debug) Puts($"[DEBUG] Starting checking categories.");
            Dictionary<string, CategoryData> apporovedCategorties = new Dictionary<string, CategoryData>();
            foreach (var categoryVar in data.shops[shopName].categories)
            {
                if (npcShop && !config.shops[shopName].npcList[shopNpc[player].UserIDString].Contains(categoryVar.Key)) continue;
                if (categoryVar.Value.permission != "" && !permission.UserHasPermission(player.UserIDString, categoryVar.Value.permission)) continue;
                if (categoryVar.Value.blacklistPermissions.Count > 0)
                {
                    bool canShow = false;
                    foreach (var perm in categoryVar.Value.blacklistPermissions)
                        if (!permission.UserHasPermission(player.UserIDString, perm))
                            canShow = true;
                    if (!canShow) continue;
                }
                apporovedCategorties.Add(categoryVar.Key, categoryVar.Value);
            }
            if (apporovedCategorties.Count == 0)
            {
                PrintWarning("Can't find any category for player permissions!");
                return;
            }
            category = category == "" || !apporovedCategorties.ContainsKey(category) ? apporovedCategorties.First().Key : category;
            shopCache[player].shopCategory = category;
            UI_AddButton(container, "Market_MainUI", config.colors.color2, $"UI_ShoppyStock affordable", "-195 201", "-173 223", "Market_MainUI_Button"); //Button_AffordableCheck
            string selected = shopCache[player].affordableOnly ? "✖" : "";
            UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", selected, TextAnchor.MiddleCenter, 14, "0 0", "22 22"); //Text_AffordableChecked
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang("EnoughFunds", player.UserIDString), TextAnchor.MiddleLeft, 13, "-169 201", "31 223"); //Text_AffordableTitle
            if (config.shops[shopName].canDeposit || config.shops[shopName].depositItem.allowWithdraw)
            {
                int startWidth = config.shops[shopName].depositItem.allowWithdraw ? 70 : 150;
                UI_AddButton(container, "Market_MainUI", config.colors.positiveDark, $"UI_ShoppyStock depositMoney {shopName}", $"{startWidth} 201", "230 223", "Market_MainUI_Button"); //Button_Deposit
                if (config.shops[shopName].depositItem.allowWithdraw && config.shops[shopName].canDeposit)
                    UI_AddText(container, "Market_MainUI_Button", config.colors.positiveLight, "RobotoCondensed-Bold.ttf", Lang("DepositOrWithdraw", player.UserIDString), TextAnchor.MiddleCenter, 14, "0 0", "160 22"); //Text_Deposit
                else if (config.shops[shopName].canDeposit)
                    UI_AddText(container, "Market_MainUI_Button", config.colors.positiveLight, "RobotoCondensed-Bold.ttf", Lang("Deposit", player.UserIDString), TextAnchor.MiddleCenter, 14, "0 0", "80 22"); //Text_Deposit
                else
                    UI_AddText(container, "Market_MainUI_Button", config.colors.positiveLight, "RobotoCondensed-Bold.ttf", Lang("WithdrawCurrency", player.UserIDString), TextAnchor.MiddleCenter, 14, "0 0", "80 22"); //Text_Deposit
            }
            if (config.debug) Puts($"[DEBUG] After deposit check.");
            UI_AddPanel(container, "Market_MainUI", config.colors.color2, "230 201", "336 223"); //Panel_BalanceBackground
            string iconShortname = config.shops[shopName].iconUrl == "" ? "UI_ShoppyStock_DefaultShopIcon" : $"UI_ShoppyStock_{shopName}_Icon";
            UI_AddImage(container, "Market_MainUI", iconShortname, 0, "234 205", "248 219", config.colors.textColor); //Image_Currency
            int currencyAmount = GetCurrencyAmount(shopName, player);
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", FormatCurrency(shopName, player), TextAnchor.MiddleCenter, 14, "248 201", "336 223"); //Text_Balance
            UI_AddPanel(container, "Market_MainUI", config.colors.color2, "393 201", "464 223"); //Panel_PageBackground
            Dictionary<string, ListingData> approvedListings = new Dictionary<string, ListingData>();
            int maxPages;
            if (search == "")
            {
                foreach (var item in apporovedCategorties[category].listings)
                {
                    if (shopCache[player].affordableOnly && item.Value.price > currencyAmount) continue;
                    if (item.Value.permission != "" && !permission.UserHasPermission(player.UserIDString, item.Value.permission)) continue;
                    if (item.Value.blacklistPermission != "" && permission.UserHasPermission(player.UserIDString, item.Value.blacklistPermission)) continue;
                    approvedListings.Add(item.Key, item.Value);
                }
                maxPages = Mathf.CeilToInt(approvedListings.Count / 16f);
            }
            else
            {
                search = search.ToLower();
                int items = 0;
                foreach (var categoryItems in apporovedCategorties.Values)
                    foreach (var item in categoryItems.listings)
                    {
                        if (shopCache[player].affordableOnly && item.Value.price > currencyAmount) continue;
                        if (item.Value.permission != "" && !permission.UserHasPermission(player.UserIDString, item.Value.permission)) continue;
                        if (item.Value.blacklistPermission != "" && permission.UserHasPermission(player.UserIDString, item.Value.blacklistPermission)) continue;
                        string itemName = config.translateItems ? Lang(item.Key, player.UserIDString) : item.Value.displayName;
                        if (itemName.ToLower().Contains(search))
                        {
                            items++;
                            approvedListings.Add(item.Key, item.Value);
                        }
                    }
                maxPages = Mathf.CeilToInt(items / 16f);
            }
            if (config.debug) Puts($"[DEBUG] After search function.");
            if (page > maxPages)
            {
                shopCache[player].page = maxPages;
                page = maxPages;
            }
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", $"{page:00}/{maxPages:00}", TextAnchor.MiddleCenter, 14, "393 201", "464 223"); //Text_ListingsPage
            UI_AddButton(container, "Market_MainUI", config.colors.color4, $"UI_ShoppyStock page {page - 1}", "340 201", "393 223", "Market_MainUI_Button"); //Button_ListingsPrevPage
            UI_AddImage(container, "Market_MainUI_Button", "UI_ShoppyStock_ArrowLeft", 0, "10 -5", "42 27", config.colors.textColor); //Image_ArrowLeft
            UI_AddButton(container, "Market_MainUI", config.colors.color4, $"UI_ShoppyStock page {page + 1}", "464 201", "517 223", "Market_MainUI_Button"); //Button_ListingsNextPage
            UI_AddImage(container, "Market_MainUI_Button", "UI_ShoppyStock_ArrowRight", 0, "10 -5", "42 27", config.colors.textColor); //Image_ArrowRight
            UI_AddPanel(container, "Market_MainUI", config.colors.color3, "-525 141", "-215 189"); //Panel_ShopTitle
            UI_AddIcon(container, "Market_MainUI", config.colors.textColor, "-517 149", "-485 181", "assets/icons/cart.png"); //Image_ShopIcon
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang(shopName, player.UserIDString), TextAnchor.MiddleLeft, 20, "-473 141", "-215 189"); //Text_ShopTitle
            UI_AddPanel(container, "Market_MainUI", config.colors.color3, "-211 141", "155 189"); //Panel_Listings1
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ItemName", player.UserIDString), TextAnchor.MiddleLeft, 20, "-199 141", "55 189"); //Text_Listings1Title
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ItemPrice", player.UserIDString), TextAnchor.MiddleCenter, 20, "55 141", "155 189"); //Text_Listings1PriceTitle
            UI_AddPanel(container, "Market_MainUI", config.colors.color3, "159 141", "525 189"); //Panel_Listings2
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ItemName", player.UserIDString), TextAnchor.MiddleLeft, 20, "171 141", "425 189"); //Text_Listings2Title
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ItemPrice", player.UserIDString), TextAnchor.MiddleCenter, 20, "425 141", "525 189"); //Text_Listings2PriceTitle
            int counter = 0;
            int offsetX = -211;
            int offsetY = 91;
            int listingCount = approvedListings.Count;
            if (config.debug) Puts($"[DEBUG] Starting looping items.");
            float percentageDiscount = 0;
            foreach (var discount in config.shops[shopName].discounts)
                if (permission.UserHasPermission(player.UserIDString, discount.Key))
                {
                    percentageDiscount = discount.Value;
                    break;
                }
            foreach (var discount in data.shops[shopName].categories[category].discounts)
                if (permission.UserHasPermission(player.UserIDString, discount.Key))
                {
                    if (config.sumDiscounts)
                        percentageDiscount += discount.Value;
                    else
                        percentageDiscount = Mathf.Max(percentageDiscount, discount.Value);
                    break;
                }
            for (int i = 0; i < 16; i += 0)
            {
                counter++;
                if (counter <= 16 * page - 16) continue;
                if (counter > listingCount)
                {
                    UI_AddPanel(container, "Market_MainUI", config.colors.color2, $"{offsetX} {offsetY}", $"{offsetX + 366} {offsetY + 48}"); //Panel_NoListing
                    i++;
                }
                else
                {
                    KeyValuePair<string, ListingData> listing = approvedListings.ElementAt(counter - 1);
                    int priceDiscounted = listing.Value.price;
                    int itemDiscountedPrice = listing.Value.price;
                    foreach (var discount in listing.Value.discounts)
                        if (permission.UserHasPermission(player.UserIDString, discount.Key))
                        {
                            priceDiscounted = discount.Value;
                            break;
                        }
                    if (percentageDiscount > 0 || priceDiscounted != listing.Value.price)
                    {
                        if (config.sumDiscounts)
                            itemDiscountedPrice = Mathf.RoundToInt(priceDiscounted - (listing.Value.price / 100f * percentageDiscount));
                        else
                            itemDiscountedPrice = Mathf.Min(priceDiscounted, Mathf.RoundToInt(listing.Value.price - (listing.Value.price / 100f * percentageDiscount)));

                    }

                    float multipliedPrice = listing.Value.price;

                    if (listing.Value.pricePerPurchaseMultiplier != 1)
                    {
                        int purchases = 0;
                        if (listing.Value.multiplyPricePerDaily)
                        {
                            string date = DateTime.Now.ToString("dd/MM/yyyy");
                            if (data.shops[shopName].users.ContainsKey(player.userID) && data.shops[shopName].users[player.userID].dailyPurchases.ContainsKey(date) && data.shops[shopName].users[player.userID].dailyPurchases[date].ContainsKey(listing.Key))
                                purchases = data.shops[shopName].users[player.userID].dailyPurchases[date][listing.Key];
                        }
                        else
                        {
                            if (data.shops[shopName].users.ContainsKey(player.userID) && data.shops[shopName].users[player.userID].wipePurchases.ContainsKey(listing.Key))
                                purchases = data.shops[shopName].users[player.userID].wipePurchases[listing.Key];
                        }
                        float power = Mathf.Pow(listing.Value.pricePerPurchaseMultiplier, purchases);
                        itemDiscountedPrice = Mathf.RoundToInt(itemDiscountedPrice * power);
                        multipliedPrice = Mathf.RoundToInt(multipliedPrice * power);
                    }
                    UI_AddButton(container, "Market_MainUI", config.colors.color2, $"UI_ShoppyStock buy {listing.Key}", $"{offsetX} {offsetY}", $"{offsetX + 366} {offsetY + 48}", "Market_MainUI_Button"); //Panel_ListingBackground
                    if (listing.Value.blueprint)
                        UI_AddItemImage(container, "Market_MainUI_Button", "blueprintbase", 0, "12 4", "52 44"); //Image_ListingImage
                    UI_AddItemImage(container, "Market_MainUI_Button", listing.Value.shortname, listing.Value.skin, "12 4", "52 44", listing.Value.commands.Count != 0 ? listing.Key : ""); //Image_ListingImage
                    UI_AddPanel(container, "Market_MainUI_Button", config.colors.color4, "266 0", "366 48"); //Panel_ListingPriceBackground
                    string itemName = config.translateItems ? Lang(listing.Key, player.UserIDString) : listing.Value.displayName;
                    string displayName = listing.Value.amount > 1 ? $"{itemName} x{listing.Value.amount}" : itemName;
                    if (listing.Value.blueprint)
                        displayName += Lang("BlueprintTag", player.UserIDString);
                    int fontSize = 17;
                    int textLength = displayName.Length;
                    if (textLength > 38)
                        fontSize = 13;
                    else if (textLength > 28)
                        fontSize = 15;
                    UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Regular.ttf", displayName, TextAnchor.MiddleLeft, fontSize, "56 0", "266 48"); //Text_ListingName
                    string limit = "";
                    if (listing.Value.dailyBuy != 0)
                    {
                        string date = DateTime.Now.ToString("dd/MM/yyyy");
                        int used = 0;
                        if (data.shops[shopName].users.ContainsKey(player.userID) && data.shops[shopName].users[player.userID].dailyPurchases.ContainsKey(date) && data.shops[shopName].users[player.userID].dailyPurchases[date].ContainsKey(listing.Key))
                            used = data.shops[shopName].users[player.userID].dailyPurchases[date][listing.Key];
                        limit += $"{Lang("DailyLimit", player.UserIDString, used, listing.Value.dailyBuy)}";
                    }
                    if (listing.Value.wipeBuy != 0)
                    {
                        int used = 0;
                        if (data.shops[shopName].users.ContainsKey(player.userID) && data.shops[shopName].users[player.userID].wipePurchases.ContainsKey(listing.Key))
                            used = data.shops[shopName].users[player.userID].wipePurchases[listing.Key];
                        limit += $"{Lang("WipeLimit", player.UserIDString, used, listing.Value.wipeBuy)}";
                    }
                    if (listing.Value.cooldown != 0)
                    {
                        string timeFormat = TimeSpan.FromSeconds(listing.Value.cooldown).ToString(@"hh\:mm\:ss");
                        limit += $"{Lang("CooldownWait", player.UserIDString, timeFormat)}";
                    }
                    if (limit != "")
                        UI_AddText(container, "Market_MainUI_Button", config.colors.color6, "RobotoCondensed-Bold.ttf", limit, TextAnchor.UpperRight, 9, "56 0", "262 46"); //Text_ListingName
                    string colorName = currencyAmount >= itemDiscountedPrice ? config.colors.textColor : config.colors.negativeLight;
                    string displayedPrice = itemDiscountedPrice == listing.Value.price ? FormatPrice(shopName, listing.Value.price) : $"<size=8><color=#F07C4D>{FormatPrice(shopName, multipliedPrice)}</color>\n</size>{FormatPrice(shopName, itemDiscountedPrice)}";
                    UI_AddText(container, "Market_MainUI_Button", colorName, "RobotoCondensed-Regular.ttf", displayedPrice, TextAnchor.MiddleCenter, 17, "266 0", "366 46"); //Text_ListingPrice
                    i++;
                }
                if (offsetY == -259)
                {
                    offsetY = 91;
                    offsetX = 159;
                }
                else
                    offsetY -= 50;
            }
            if (config.debug) Puts($"[DEBUG] Finished looping items.");
            counter = 0;
            offsetY = 91;
            int categoryCount = apporovedCategorties.Count;
            if (categoryPage > categoryCount / 8f)
                categoryPage = Mathf.CeilToInt(categoryCount / 8f);
            if (config.debug) Puts($"[DEBUG] Started looping categories.");
            for (int i = 0; i < 8; i += 0)
            {
                counter++;
                if (counter <= 8 * categoryPage - 8) continue;
                if (counter > categoryCount)
                {
                    UI_AddPanel(container, "Market_MainUI", config.colors.color4, $"-525 {offsetY}", $"-215 {offsetY + 48}"); //Panel_CategoryIcon
                    i++;
                }
                else
                {
                    KeyValuePair<string, CategoryData> categoryListed = apporovedCategorties.ElementAt(counter - 1);
                    UI_AddButton(container, "Market_MainUI", config.colors.color3, $"UI_ShoppyStock category {categoryListed.Key}", $"-525 {offsetY}", $"-215 {offsetY + 48}", "Market_MainUI_Button"); //Button_Category
                    UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang($"Category_{categoryListed.Key}", player.UserIDString), TextAnchor.MiddleLeft, 15, "58 8", "262 40"); //Text_CategoryName
                    string categoryIcon = categoryListed.Value.iconUrl == "" ? "UI_ShoppyStock_DefaultCategoryIcon" : $"UI_ShoppyStock_Category_{categoryListed.Key}";
                    if (categoryListed.Key == category)
                    {
                        UI_AddPanel(container, "Market_MainUI_Button", config.colors.color3, "0 0", "48 48"); //Panel_CategoryIconSelected
                        UI_AddImage(container, "Market_MainUI_Button", categoryIcon, 0, "8 8", "40 40", config.colors.textColor); //Image_CategoryIconSelected
                    }
                    else
                    {
                        UI_AddPanel(container, "Market_MainUI_Button", config.colors.color2, "0 0", "48 48"); //Panel_CategoryIcon
                        UI_AddImage(container, "Market_MainUI_Button", categoryIcon, 0, "8 8", "40 40", config.colors.color6); //Image_CategoryIcon
                    }
                    i++;
                }
                offsetY -= 50;
            }
            if (config.debug) Puts($"[DEBUG] Finished looping categories.");
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", $"{categoryPage:00}/{Mathf.CeilToInt(apporovedCategorties.Count / 8f):00}", TextAnchor.MiddleLeft, 16, "-521 -283", "-450 -261"); //Text_CategoryPageNumber
            UI_AddButton(container, "Market_MainUI", config.colors.color3, $"UI_ShoppyStock categoryPage {categoryPage - 1}", "-325 -283", "-272 -261", "Market_MainUI_Button"); //Button_CategoryPrevPage
            UI_AddImage(container, "Market_MainUI_Button", "UI_ShoppyStock_ArrowLeft", 0, "10 -5", "42 27", config.colors.textColor); //Image_ArrowLeft
            UI_AddButton(container, "Market_MainUI", config.colors.color3, $"UI_ShoppyStock categoryPage {categoryPage + 1}", "-268 -283", "-215 -261", "Market_MainUI_Button"); //Button_CategoryNextPage
            UI_AddImage(container, "Market_MainUI_Button", "UI_ShoppyStock_ArrowRight", 0, "10 -5", "42 27", config.colors.textColor); //Image_ArrowRight
            if (permission.UserHasPermission(player.UserIDString, "shoppystock.admin"))
            {
                UI_AddButton(container, "Market_MainUI", config.colors.positiveDark, $"UI_ShoppyStock addItem add {shopName} {category} 0", "-211 -283", "155 -261", "Market_MainUI_Button"); //Button_AddItem
                UI_AddText(container, "Market_MainUI_Button", config.colors.positiveLight, "RobotoCondensed-Bold.ttf", Lang("AdminAddItem", player.UserIDString), TextAnchor.MiddleCenter, 14, "0 0", "366 22"); //Text_AddItem
            }
            CuiHelper.DestroyUi(player, "Market_MainUI");
            CuiHelper.AddUi(player, container);
        }

        private void OpenShopPurchaseUI(BasePlayer player, string shopName, ListingData listing, string listingKey, int amount = 1)
        {
            if (amount < 1) amount = 1;
            shopCache[player].currentListings = new CurrentListing() { amount = amount, listing = listing, shopName = shopName, listingKey = listingKey };
            CuiElementContainer container = new CuiElementContainer();
            if (config.clickingSound)
                EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/notice/loot.copy.fx.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
            if (config.debug) Puts($"[DEBUG] Opening shop purchase.");
            UI_AddAnchor(container, "Market_PurchaseUI", "Market_AnchorUI", "0 0", "1 1"); //Market_PurchaseUI
            UI_AddBlurPanel(container, "Market_PurchaseUI", "0.3 0.3 0.3 0.2"); //Panel_BackgroundBlur
            UI_AddPanel(container, "Market_PurchaseUI", "0.4 0.4 0.4 0.3", "0 0", "0 0", "0 0", "1 1"); //Panel_BackgroundColor
            UI_AddVignettePanel(container, "Market_PurchaseUI", "0 0 0 0.8"); //Panel_Vignette
            UI_AddAnchor(container, "Market_PurchasePanelUI", "Market_PurchaseUI", "0.5 0.5", "0.5 0.5"); //Market_TransferUI
            UI_AddPanel(container, "Market_PurchasePanelUI", config.colors.color1, "-238 -210", "238 210"); //Panel_Background
            UI_AddPanel(container, "Market_PurchasePanelUI", config.colors.color2, "-234 158", "234 206"); //Panel_TopPanel
            UI_AddIcon(container, "Market_PurchasePanelUI", config.colors.textColor, "-228 164", "-192 200", "assets/icons/cart.png"); //Image_ShopIcon
            UI_AddText(container, "Market_PurchasePanelUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("FinishOfferTitle", player.UserIDString), TextAnchor.MiddleLeft, 26, "-186 158", "186 206"); //Text_Title
            string commande = config.closeOnlySubGui ? "UI_ShoppyStock closePurchaseInfo" : "UI_ShoppyStock close";
            UI_AddButton(container, "Market_PurchasePanelUI", config.colors.negativeDark, commande, "186 158", "234 206", "Market_PurchasePanelUI_Button"); //Button_Close
            UI_AddText(container, "Market_PurchasePanelUI_Button", config.colors.negativeLight, "RobotoCondensed-Regular.ttf", "✖", TextAnchor.MiddleCenter, 30, "0 0", "48 48"); //Text_Close
            UI_AddPanel(container, "Market_PurchasePanelUI", config.colors.color2, "-234 74", "-154 154"); //Panel_IconBackground
            if (listing.blueprint)
                UI_AddItemImage(container, "Market_PurchasePanelUI", "blueprintbase", 0, "-226 82", "-162 146"); //Image_ItemDisplay
            UI_AddItemImage(container, "Market_PurchasePanelUI", listing.shortname, listing.skin, "-226 82", "-162 146", listing.commands.Count != 0 ? listingKey : ""); //Image_ItemDisplay
            UI_AddPanel(container, "Market_PurchasePanelUI", config.colors.color5, "-150 126", "234 154"); //Panel_ItemHintBackground
            UI_AddText(container, "Market_PurchasePanelUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ItemName", player.UserIDString), TextAnchor.MiddleLeft, 16, "-144 126", "138 154"); //Text_ItemHintName
            UI_AddText(container, "Market_PurchasePanelUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ItemPrice", player.UserIDString), TextAnchor.MiddleCenter, 16, "138 126", "234 154"); //Text_ItemHintPrice
            UI_AddPanel(container, "Market_PurchasePanelUI", config.colors.color2, "-150 74", "234 122"); //Panel_ItemInfoBackground
            UI_AddPanel(container, "Market_PurchasePanelUI", config.colors.color5, "139 74", "234 122"); //Panel_ItemInfoPriceBackground
            if (config.debug) Puts($"[DEBUG] Before display name check.");
            string itemName = config.translateItems ? Lang(listingKey, player.UserIDString) : listing.displayName;
            string displayName = listing.amount > 1 ? $"{itemName} x{listing.amount}" : itemName;
            if (listing.blueprint)
                displayName += Lang("BlueprintTag", player.UserIDString);
            int fontSize = 17;
            int textLength = displayName.Length;
            if (textLength > 38)
                fontSize = 13;
            else if (textLength > 28)
                fontSize = 15;
            float percentageDiscount = 0;
            foreach (var discount in config.shops[shopName].discounts)
                if (permission.UserHasPermission(player.UserIDString, discount.Key))
                {
                    percentageDiscount = discount.Value;
                    break;
                }
            foreach (var discount in data.shops[shopName].categories[shopCache[player].shopCategory].discounts)
                if (permission.UserHasPermission(player.UserIDString, discount.Key))
                {
                    if (config.sumDiscounts)
                        percentageDiscount += discount.Value;
                    else
                        percentageDiscount = Mathf.Max(percentageDiscount, discount.Value);
                    break;
                }
            int itemDiscountedPrice = 0;
            if (listing.price > 0)
            {
                int priceDiscounted = listing.price;
                itemDiscountedPrice = listing.price;
                foreach (var discount in listing.discounts)
                    if (permission.UserHasPermission(player.UserIDString, discount.Key))
                    {
                        priceDiscounted = discount.Value;
                        break;
                    }
                if (percentageDiscount > 0 || priceDiscounted != listing.price)
                {
                    if (config.sumDiscounts)
                        itemDiscountedPrice = Mathf.RoundToInt(priceDiscounted - (listing.price / 100f * percentageDiscount));
                    else
                        itemDiscountedPrice = Mathf.Min(priceDiscounted, Mathf.RoundToInt(listing.price - (listing.price / 100f * percentageDiscount)));

                }
            }
            if (listing.pricePerPurchaseMultiplier != 1)
            {
                int purchases = 0;
                if (listing.multiplyPricePerDaily)
                {
                    string date = DateTime.Now.ToString("dd/MM/yyyy");
                    if (data.shops[shopName].users.ContainsKey(player.userID) && data.shops[shopName].users[player.userID].dailyPurchases.ContainsKey(date) && data.shops[shopName].users[player.userID].dailyPurchases[date].ContainsKey(listingKey))
                        purchases = data.shops[shopName].users[player.userID].dailyPurchases[date][listingKey];
                }
                else
                {
                    if (data.shops[shopName].users.ContainsKey(player.userID) && data.shops[shopName].users[player.userID].wipePurchases.ContainsKey(listingKey))
                        purchases = data.shops[shopName].users[player.userID].wipePurchases[listingKey];
                }
                float power = Mathf.Pow(listing.pricePerPurchaseMultiplier, purchases);
                itemDiscountedPrice = Mathf.RoundToInt(itemDiscountedPrice * power);
            }
            UI_AddText(container, "Market_PurchasePanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", displayName, TextAnchor.MiddleLeft, fontSize, "-144 74", "139 122"); //Text_ItemName
            string displayedPrice = itemDiscountedPrice == listing.price ? FormatPrice(shopName, listing.price) : $"<size=8><color=#F07C4D>{FormatPrice(shopName, listing.price)}</color>\n</size>{FormatPrice(shopName, itemDiscountedPrice)}";
            UI_AddText(container, "Market_PurchasePanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", displayedPrice, TextAnchor.MiddleCenter, 17, "139 74", "234 122"); //Text_ItemPrice
            if (config.debug) Puts($"[DEBUG] Before currency check.");
            int balance = GetCurrencyAmount(shopName, player);
            int max = itemDiscountedPrice == 0 ? int.MaxValue : Mathf.FloorToInt(balance / itemDiscountedPrice);
            if (max == 0) return;
            if (config.debug) Puts($"[DEBUG] Before max purchases check.");
            int freeSlots = GetFreeSlots(player);
            int maxStackSize = ItemManager.FindItemDefinition(listing.shortname)?.stackable ?? 1;
            int itemsPerStack = maxStackSize / listing.amount;
            if (max > itemsPerStack * freeSlots)
                max = itemsPerStack * freeSlots;
            if (max == 0)
            {
                EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("NotEnoughSpace", player.UserIDString), config.popUpFontSize, config.popUpLength);
                return;
            }
            if (listing.dailyBuy != 0)
            {
                if (max > listing.dailyBuy)
                    max = listing.dailyBuy;
                string date = DateTime.Now.ToString("dd/MM/yyyy");
                if (data.shops[shopName].users.ContainsKey(player.userID) && data.shops[shopName].users[player.userID].dailyPurchases.ContainsKey(date) && data.shops[shopName].users[player.userID].dailyPurchases[date].ContainsKey(listingKey))
                    if (max > listing.dailyBuy - data.shops[shopName].users[player.userID].dailyPurchases[date][listingKey])
                        max = listing.dailyBuy - data.shops[shopName].users[player.userID].dailyPurchases[date][listingKey];
            }
            if (listing.wipeBuy != 0)
            {
                if (max > listing.wipeBuy)
                    max = listing.wipeBuy;
                if (data.shops[shopName].users.ContainsKey(player.userID) && data.shops[shopName].users[player.userID].wipePurchases.ContainsKey(listingKey))
                    if (max > listing.wipeBuy - data.shops[shopName].users[player.userID].wipePurchases[listingKey])
                        max = listing.wipeBuy - data.shops[shopName].users[player.userID].wipePurchases[listingKey];
            }
            if (listing.cooldown != 0 || listing.pricePerPurchaseMultiplier != 1)
                max = 1;
            if (amount > max)
            {
                amount = max;
                shopCache[player].currentListings.amount = amount;
            }
            if (config.debug) Puts($"[DEBUG] After max purchases check.");
            int height = listing.description ? -68 : -2;
            UI_AddPanel(container, "Market_PurchasePanelUI", config.colors.color4, $"-50 {height}", $"50 {height + 42}"); //Panel_ItemAmountBackground
            UI_AddText(container, "Market_PurchasePanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang("PurchaseAmount", player.UserIDString), TextAnchor.MiddleCenter, 20, $"-92 {height + 42}", $"92 {height + 70}"); //Text_PurchaseAmount
            UI_AddButton(container, "Market_PurchasePanelUI", config.colors.positiveDark, $"UI_ShoppyStock setAmount {amount + 1}", $"50 {height}", $"92 {height + 42}", "Market_PurchasePanelUI_Button"); //Button_Increase
            UI_AddText(container, "Market_PurchasePanelUI_Button", config.colors.positiveLight, "RobotoCondensed-Bold.ttf", "+", TextAnchor.MiddleCenter, 35, "0 0", "42 42"); //Text_Accept
            UI_AddButton(container, "Market_PurchasePanelUI", config.colors.negativeDark, $"UI_ShoppyStock setAmount {amount - 1}", $"-92 {height}", $"-50 {height + 42}", "Market_PurchasePanelUI_Button"); //Button_Decrease
            UI_AddText(container, "Market_PurchasePanelUI_Button", config.colors.negativeLight, "RobotoCondensed-Bold.ttf", "-", TextAnchor.MiddleCenter, 35, "0 0", "42 42"); //Text_Cancel
            UI_AddInput(container, "Market_PurchasePanelUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", TextAnchor.MiddleCenter, 23, amount.ToString(), 7, "UI_ShoppyStock setAmount", "42 0", "142 42"); //Text_Amount
            if (!listing.description)
            {
                UI_AddPanel(container, "Market_PurchasePanelUI", config.colors.color2, "-180 -44", "180 -32"); //Panel_SliderBackground
                float onePart = max / 30f;
                int lineEnd = -180;
                for (int i = 1; i < 30; i++)
                {
                    if (amount > onePart * i)
                        lineEnd += 12;
                    else break;
                }
                UI_AddPanel(container, "Market_PurchasePanelUI", config.colors.color5, "-180 -44", $"{lineEnd} -32"); //Panel_Slider
                UI_AddPanel(container, "Market_PurchasePanelUI", config.colors.positiveDark, $"{lineEnd} -44", $"{lineEnd + 12} -32"); //Panel_SliderEnd
                for (int i = 1; i < 31; i++)
                {
                    if (i == 1)
                        UI_AddButton(container, "Market_PurchasePanelUI", "0 0 0 0", $"UI_ShoppyStock setAmount 1", $"{-180 + ((12 * i) - 12)} -44", $"{-180 + (12 * i)} -32", null); //Button_SliderSetAmount
                    else if (i == 30)
                        UI_AddButton(container, "Market_PurchasePanelUI", "0 0 0 0", $"UI_ShoppyStock setAmount {max}", $"{-180 + ((12 * i) - 12)} -44", $"{-180 + (12 * i)} -32", null); //Button_SliderSetAmount
                    else
                        UI_AddButton(container, "Market_PurchasePanelUI", "0 0 0 0", $"UI_ShoppyStock setAmount {Mathf.FloorToInt(onePart * i)}", $"{-180 + ((12 * i) - 12)} -44", $"{-180 + (12 * i)} -32", null); //Button_SliderSetAmount
                }
                UI_AddText(container, "Market_PurchasePanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", "1", TextAnchor.MiddleRight, 18, "-238 -52", "-184 -24"); //Text_SliderMin
                UI_AddText(container, "Market_PurchasePanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", FormatPrice("", max), TextAnchor.MiddleLeft, 18, "184 -52", "238 -24"); //Text_SliderMax
            }
            else
            {
                UI_AddPanel(container, "Market_PurchasePanelUI", config.colors.color2, "-109 4", "109 68"); //Panel_ItemInfoBackground (1)
                UI_AddText(container, "Market_PurchasePanelUI", config.colors.color1, "RobotoCondensed-Bold.ttf", Lang("DescriptionTextBackground", player.UserIDString), TextAnchor.MiddleCenter, 30, "-109 4", "109 68"); //Text_ItemHintName (1)
                UI_AddText(container, "Market_PurchasePanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang($"Description_{listingKey}", player.UserIDString), TextAnchor.UpperLeft, 11, "-105 5", "105 67"); //Text_PurchaseAmount (1)
            }
            if (config.debug) Puts($"[DEBUG] After slider done.");
            UI_AddPanel(container, "Market_PurchasePanelUI", config.colors.color2, "-192 -122", "192 -74"); //Panel_BalanceAfterBackground
            UI_AddText(container, "Market_PurchasePanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang("BalanceAfterPurchase", player.UserIDString), TextAnchor.MiddleLeft, 17, "-186 -122", "97 -74"); //Text_BalanceAfterPurchase
            UI_AddPanel(container, "Market_PurchasePanelUI", config.colors.color5, "97 -122", "192 -74"); //Panel_BalanceAfterPriceBackground
            UI_AddText(container, "Market_PurchasePanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", FormatPrice(shopName, balance - (itemDiscountedPrice * amount), true), TextAnchor.MiddleCenter, 17, "97 -122", "192 -74"); //Text_BalanceAfterPurchasePrice
            UI_AddPanel(container, "Market_PurchasePanelUI", config.colors.color2, "-192 -174", "192 -126"); //Panel_TotalPriceBackground
            UI_AddText(container, "Market_PurchasePanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang("TotalPrice", player.UserIDString), TextAnchor.MiddleLeft, 17, "-186 -174", "97 -126"); //Text_TotalPrice
            UI_AddPanel(container, "Market_PurchasePanelUI", config.colors.color5, "97 -174", "192 -126"); //Panel_TotalPricePirceBackground
            UI_AddText(container, "Market_PurchasePanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", FormatPrice(shopName, itemDiscountedPrice * amount, true), TextAnchor.MiddleCenter, 17, "97 -174", "192 -126"); //Text_TotalPricePrice
            UI_AddButton(container, "Market_PurchasePanelUI", config.colors.color5, $"UI_ShoppyStock cancelPurchase", "-192 -206", "-2 -178", "Market_PurchasePanelUI_Button"); //Button_CancelPurchase
            UI_AddText(container, "Market_PurchasePanelUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("CancelButton", player.UserIDString), TextAnchor.MiddleCenter, 16, "0 0", "190 28"); //Text_Cancel
            UI_AddButton(container, "Market_PurchasePanelUI", config.colors.positiveDark, $"UI_ShoppyStock acceptPurchase", "2 -206", "192 -178", "Market_PurchasePanelUI_Button"); //Button_AcceptPurchase
            UI_AddText(container, "Market_PurchasePanelUI_Button", config.colors.positiveLight, "RobotoCondensed-Bold.ttf", Lang("ButtonAccept", player.UserIDString), TextAnchor.MiddleCenter, 16, "0 0", "190 28"); //Text_Accept
            if (config.debug) Puts($"[DEBUG] Finishing purchase show.");
            CuiHelper.DestroyUi(player, "Market_PurchaseUI");
            CuiHelper.AddUi(player, container);
        }

        private int GetFreeSlots(BasePlayer player)
        {
            int freeSlots = 0;
            for (int i = 0; i < 6; i++)
                if (player.inventory.containerBelt.GetSlot(i) == null)
                    freeSlots++;
            for (int i = 0; i < 24; i++)
                if (player.inventory.containerMain.GetSlot(i) == null)
                    freeSlots++;
            return freeSlots;
        }

        private void OpenStockMarketSelectUI(BasePlayer player)
        {
            int shops = 0;
            foreach (var shop in config.shops.Values)
                if (shop.stockConfig.canStockMarket)
                    shops++;
            if (shops == 0)
            {
                EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("StockMarketDisabled", player.UserIDString), config.popUpFontSize, config.popUpLength);
                return;
            }
            if (shops == 1)
                foreach (var shop in config.shops)
                    if (shop.Value.stockConfig.canStockMarket)
                    {
                        OpenStockMarketUI(player, shop.Key);
                        return;
                    }
            if (config.clickingSound)
                EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/notice/loot.copy.fx.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
            CuiElementContainer container = new CuiElementContainer();
            if (!openedUis.Contains(player))
            {
                UI_AddAnchor(container, "Market_AnchorUI", "Hud.Menu", "0 0", "1 1"); //Market_MainUI
                UI_AddBlurPanel(container, "Market_AnchorUI", "0.3 0.3 0.3 0.2"); //Panel_BackgroundBlur
                UI_AddPanel(container, "Market_AnchorUI", "0.4 0.4 0.4 0.3", "0 0", "0 0", "0 0", "1 1"); //Panel_BackgroundColor
                UI_AddVignettePanel(container, "Market_AnchorUI", "0 0 0 0.8"); //Panel_Vignette
                UI_AddButton(container, "Market_AnchorUI", "0 0 0 0", "UI_ShoppyStock close", "0 0", "0 0", null, "0 0", "1 1"); //Button_BackgroundClose
                openedUis.Add(player);
                EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/notice/item.select.fx.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
            }
            if (config.debug) Puts($"[DEBUG] Opening market select UI.");
            UI_AddAnchor(container, "Market_MainUI", "Market_AnchorUI", "0.5 0.5", "0.5 0.5"); //Market_MainUI
            UI_AddPanel(container, "Market_MainUI", config.colors.color1, "-529 -287", "529 287"); //Panel_Background
            UI_AddPanel(container, "Market_MainUI", config.colors.color2, "-525 235", "525 283"); //Panel_TopPanel
            int startX = -525;
            if (config.shopButton)
            {
                UI_AddButton(container, "Market_MainUI", config.colors.color2, "UI_ShoppyStock shops", $"{startX} 235", $"{startX + 200} 283", "Market_MainUI_Button"); //Button_Shops
                UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ShopsButton", player.UserIDString), TextAnchor.MiddleCenter, 20, "0 0", "200 48"); //Text_Shops
                startX += 200;
            }
            if (config.stockButton)
            {
                UI_AddButton(container, "Market_MainUI", config.colors.color2, "UI_ShoppyStock stock", $"{startX} 235", $"{startX + 200} 283", "Market_MainUI_Button"); //Button_StockMarket
                UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("StockButton", player.UserIDString), TextAnchor.MiddleCenter, 20, "0 0", "200 48"); //Text_Stock
                startX += 200;
            }
            if (config.transferButton)
            {
                UI_AddButton(container, "Market_MainUI", config.colors.color2, "UI_ShoppyStock transfer", $"{startX} 235", $"{startX + 200} 283", "Market_MainUI_Button"); //Button_Transfer
                UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("TransferButton", player.UserIDString), TextAnchor.MiddleCenter, 20, "0 0", "200 48"); //Text_Transfer
            }
            UI_AddButton(container, "Market_MainUI", config.colors.negativeDark, "UI_ShoppyStock close", "477 235", "525 283", "Market_MainUI_Button"); //Button_Close
            UI_AddText(container, "Market_MainUI_Button", config.colors.negativeLight, "RobotoCondensed-Bold.ttf", "✖", TextAnchor.MiddleCenter, 30, "0 0", "48 48"); //Text_Close
            UI_AddPanel(container, "Market_MainUI", config.colors.color3, "-525 193", "525 231"); //Panel_HintPanel
            UI_AddIcon(container, "Market_MainUI", config.colors.textColor, "-521 195", "-487 229", "assets/icons/info.png"); //Image_CurrencyIcon
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("SelectStockMarketHint", player.UserIDString), TextAnchor.MiddleLeft, 20, "-483 193", "525 231"); //Text_ShopHint
            List<int> widths = new List<int>() { 172, 172, 171, 171, 172, 172 };
            int widthStart = -525;
            int counter = -1;
            if (config.debug) Puts($"[DEBUG] Starting looping stock markets.");
            for (int i = 0; i < 6; i += 0)
            {
                counter++;
                if (i < shops)
                {
                    KeyValuePair<string, ShopConfig> shop = config.shops.ElementAt(counter);
                    if (!shop.Value.stockConfig.canStockMarket) continue;
                    UI_AddPanel(container, "Market_MainUI", config.colors.color4, $"{widthStart} 17", $"{widthStart + 172} 189"); //Panel_CurrencyIconBackground
                    string iconShortname = shop.Value.iconUrl == "" ? "UI_ShoppyStock_DefaultShopIcon" : $"UI_ShoppyStock_{shop.Key}_Icon";
                    UI_AddImage(container, "Market_MainUI", iconShortname, 0, $"{widthStart + 12} 29", $"{widthStart + 160} 177", config.colors.textColor); //Image_CurrencyIcon
                    UI_AddPanel(container, "Market_MainUI", config.colors.color2, $"{widthStart} -13", $"{widthStart + 172} 13"); //Panel_CurrencyNameBackground
                    UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang($"{shop.Key}_StockMarket", player.UserIDString), TextAnchor.MiddleCenter, 14, $"{widthStart} -13", $"{widthStart + 172} 13"); //Text_ShopCurrencyName
                    UI_AddPanel(container, "Market_MainUI", config.colors.color2, $"{widthStart} -43", $"{widthStart + 172} -17"); //Panel_CurrencyTitleBackground
                    UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("Amount", player.UserIDString), TextAnchor.MiddleCenter, 14, $"{widthStart} -43", $"{widthStart + 97} -17"); //Text_CurrencyAmountTitle
                    UI_AddPanel(container, "Market_MainUI", config.colors.color4, $"{widthStart + 97} -43", $"{widthStart + 172} -17"); //Panel_CurrencyAmountBackground
                    UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", FormatCurrency(shop.Key, player), TextAnchor.MiddleCenter, 14, $"{widthStart + 97} -43", $"{widthStart + 172} -17"); //Text_CurrencyAmount
                    UI_AddPanel(container, "Market_MainUI", config.colors.color2, $"{widthStart} -231", $"{widthStart + 172} -47"); //Panel_DescriptionBackground
                    UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang($"{shop.Key}_StockMarketDescription", player.UserIDString), TextAnchor.UpperLeft, 10, $"{widthStart + 4} -227", $"{widthStart + 168} -51"); //Text_CurrencyDescription
                    UI_AddButton(container, "Market_MainUI", config.colors.color2, $"UI_ShoppyStock openStock {shop.Key}", $"{widthStart} -283", $"{widthStart + 172} -235", "Market_MainUI_Button"); //Button_OpenShop
                    UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("OpenStockMarketButton", player.UserIDString), TextAnchor.MiddleCenter, 20, "0 0", "172 48"); //Text_OpenShop
                    i++;
                }
                else
                {
                    UI_AddPanel(container, "Market_MainUI", config.colors.color4, $"{widthStart} -283", $"{widthStart + 172} 189"); //Panel_CurrencyEmpty
                    i++;
                }
                widthStart += widths[i - 1] + 4;
            }
            if (config.debug) Puts($"[DEBUG] Finished looping stock markets.");
            CuiHelper.DestroyUi(player, "Market_MainUI");
            CuiHelper.AddUi(player, container);
        }

        private void OpenStockMarketUI(BasePlayer player, string shopName, string search = "", string category = "", int page = 1, int categoryPage = 1)
        {
            if (categoryPage < 1)
                categoryPage = 1;
            if (page < 1)
                page = 1;
            shopCache.TryAdd(player, new ShopCache());
            shopCache[player].currentShop = shopName;
            shopCache[player].stockMarket.categoryPage = categoryPage;
            shopCache[player].stockMarket.page = page;
            shopCache[player].stockMarket.search = search;
            shopCache[player].stockMarket.shopName = shopName;
            shopCache[player].stockMarket.category = category;
            shopCache[player].lastVisit = "stock";
            if (config.debug) Puts($"[DEBUG] Starting opening stock market.");
            CuiElementContainer container = new CuiElementContainer();
            if (config.clickingSound)
                EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/notice/loot.copy.fx.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
            if (!openedUis.Contains(player))
            {
                UI_AddAnchor(container, "Market_AnchorUI", "Hud.Menu", "0 0", "1 1"); //Market_MainUI
                UI_AddBlurPanel(container, "Market_AnchorUI", "0.3 0.3 0.3 0.2"); //Panel_BackgroundBlur
                UI_AddPanel(container, "Market_AnchorUI", "0.4 0.4 0.4 0.3", "0 0", "0 0", "0 0", "1 1"); //Panel_BackgroundColor
                UI_AddVignettePanel(container, "Market_AnchorUI", "0 0 0 0.8"); //Panel_Vignette
                UI_AddButton(container, "Market_AnchorUI", "0 0 0 0", "UI_ShoppyStock close", "0 0", "0 0", null, "0 0", "1 1"); //Button_BackgroundClose
                openedUis.Add(player);
                EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/notice/item.select.fx.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
            }
            UI_AddAnchor(container, "Market_MainUI", "Market_AnchorUI", "0.5 0.5", "0.5 0.5"); //Market_MainUI
            UI_AddPanel(container, "Market_MainUI", config.colors.color1, "-529 -287", "529 287"); //Panel_Background
            UI_AddPanel(container, "Market_MainUI", config.colors.color2, "-525 235", "525 283"); //Panel_TopPanel
            bool npcShop = shopNpc.ContainsKey(player);
            if (!npcShop)
            {
                int startX = -525;
                if (config.shopButton)
                {
                    UI_AddButton(container, "Market_MainUI", config.colors.color2, "UI_ShoppyStock shops", $"{startX} 235", $"{startX + 200} 283", "Market_MainUI_Button"); //Button_Shops
                    UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ShopsButton", player.UserIDString), TextAnchor.MiddleCenter, 20, "0 0", "200 48"); //Text_Shops
                    startX += 200;
                }
                if (config.stockButton)
                {
                    UI_AddButton(container, "Market_MainUI", config.colors.color2, "UI_ShoppyStock stock", $"{startX} 235", $"{startX + 200} 283", "Market_MainUI_Button"); //Button_StockMarket
                    UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("StockButton", player.UserIDString), TextAnchor.MiddleCenter, 20, "0 0", "200 48"); //Text_Stock
                    startX += 200;
                }
                if (config.transferButton)
                {
                    UI_AddButton(container, "Market_MainUI", config.colors.color2, "UI_ShoppyStock transfer", $"{startX} 235", $"{startX + 200} 283", "Market_MainUI_Button"); //Button_Transfer
                    UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("TransferButton", player.UserIDString), TextAnchor.MiddleCenter, 20, "0 0", "200 48"); //Text_Transfer
                    startX += 200;
                }
            }
            else
                UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", shopNpc[player].displayName, TextAnchor.MiddleLeft, 30, "-518 235", "525 283"); //Text_NpcUsername
            UI_AddButton(container, "Market_MainUI", config.colors.negativeDark, "UI_ShoppyStock close", "477 235", "525 283", "Market_MainUI_Button"); //Button_Close
            UI_AddText(container, "Market_MainUI_Button", config.colors.negativeLight, "RobotoCondensed-Bold.ttf", "✖", TextAnchor.MiddleCenter, 30, "0 0", "48 48"); //Text_Close
            UI_AddPanel(container, "Market_MainUI", config.colors.color3, "-525 193", "525 231"); //Panel_ControlPanel
            UI_AddPanel(container, "Market_MainUI", config.colors.color2, "-517 201", "-225 223"); //Panel_SearchBar
            UI_AddImage(container, "Market_MainUI", "UI_ShoppyStock_Search", 0, "-243 205", "-229 219", config.colors.textColor); //Image_Search
            UI_AddInput(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", TextAnchor.MiddleLeft, 13, search, 24, $"UI_ShoppyStock stock search", "-513 201", "-225 223"); //Input_SearchField
            if (category == "")
            {
                if (config.shops[shopName].stockConfig.defaultCategory != "")
                    category = config.shops[shopName].stockConfig.defaultCategory;
                else
                    category = stockCategories.First().Key;
            }
            UI_AddButton(container, "Market_MainUI", config.colors.color2, $"UI_ShoppyStock stock sellToServer", "-195 201", "-173 223", "Market_MainUI_Button"); //Button_AffordableCheck
            string sellToServerOnly = shopCache[player].stockMarket.sellToServerOnly ? "✖" : "";
            UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", sellToServerOnly, TextAnchor.MiddleCenter, 14, "0 0", "22 22"); //Text_AffordableChecked
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang("SellToServerOnly", player.UserIDString), TextAnchor.MiddleLeft, 13, "-169 201", "-19 223"); //Text_AffordableTitle
            UI_AddButton(container, "Market_MainUI", config.colors.color2, $"UI_ShoppyStock stock buySellOrders", "-19 201", "3 223", "Market_MainUI_Button"); //Button_AffordableCheck
            UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang(shopCache[player].stockMarket.buySellOrders.ToString().ToUpper(), player.UserIDString), TextAnchor.MiddleCenter, 14, "0 0", "22 22"); //Text_AffordableChecked
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang("BuySellOrdersOnly", player.UserIDString), TextAnchor.MiddleLeft, 13, "7 201", "157 223"); //Text_AffordableTitle
            if (config.shops[shopName].canDeposit || config.shops[shopName].depositItem.allowWithdraw)
            {
                int startWidth = config.shops[shopName].depositItem.allowWithdraw ? 70 : 150;
                UI_AddButton(container, "Market_MainUI", config.colors.positiveDark, $"UI_ShoppyStock depositMoney {shopName}", $"{startWidth} 201", "230 223", "Market_MainUI_Button"); //Button_Deposit
                if (config.shops[shopName].depositItem.allowWithdraw && config.shops[shopName].canDeposit)
                    UI_AddText(container, "Market_MainUI_Button", config.colors.positiveLight, "RobotoCondensed-Bold.ttf", Lang("DepositOrWithdraw", player.UserIDString), TextAnchor.MiddleCenter, 14, "0 0", "160 22"); //Text_Deposit
                else if (config.shops[shopName].canDeposit)
                    UI_AddText(container, "Market_MainUI_Button", config.colors.positiveLight, "RobotoCondensed-Bold.ttf", Lang("Deposit", player.UserIDString), TextAnchor.MiddleCenter, 14, "0 0", "80 22"); //Text_Deposit
                else
                    UI_AddText(container, "Market_MainUI_Button", config.colors.positiveLight, "RobotoCondensed-Bold.ttf", Lang("WithdrawCurrency", player.UserIDString), TextAnchor.MiddleCenter, 14, "0 0", "80 22"); //Text_Deposit
            }
            UI_AddPanel(container, "Market_MainUI", config.colors.color2, "230 201", "336 223"); //Panel_BalanceBackground
            string iconShortname = config.shops[shopName].iconUrl == "" ? "UI_ShoppyStock_DefaultShopIcon" : $"UI_ShoppyStock_{shopName}_Icon";
            UI_AddImage(container, "Market_MainUI", iconShortname, 0, "234 205", "248 219", config.colors.textColor); //Image_Currency
            int currencyAmount = GetCurrencyAmount(shopName, player);
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", FormatCurrency(shopName, player), TextAnchor.MiddleCenter, 14, "248 201", "336 223"); //Text_Balance
            UI_AddPanel(container, "Market_MainUI", config.colors.color2, "393 201", "464 223"); //Panel_PageBackground
            if (config.debug) Puts($"[DEBUG] Starting sorting listings.");
            List<string> approvedListings = new List<string>();
            int maxPages;
            if (search == "")
            {
                int items = 0;
                if (category == "my_listings")
                {
                    if (config.debug) Puts($"[DEBUG] Checking category 1.");
                    foreach (var categoryVal in stockCategories)
                        foreach (var item in categoryVal.Value)
                        {
                            if (config.ignoredShortnames.Contains(item.shortname)) continue;
                            if (shopCache[player].stockMarket.sellToServerOnly && (!data.stockMarkets[shopName].stockConfig.serverSell.ContainsKey(item.shortname) || !data.stockMarkets[shopName].stockConfig.serverSell[item.shortname].ContainsKey(0))) continue;
                            if (data.stockMarkets[shopName].playerData.buyOrders.ContainsKey(item.shortname) && data.stockMarkets[shopName].playerData.buyOrders[item.shortname].ContainsKey(0) && data.stockMarkets[shopName].playerData.buyOrders[item.shortname][0].Any(x => x.sellerId == player.userID))
                            {
                                approvedListings.Add($"{item.shortname}-0");
                                items++;
                                continue;
                            }
                            if (data.stockMarkets[shopName].playerData.sellOrders.ContainsKey(item.shortname) && data.stockMarkets[shopName].playerData.sellOrders[item.shortname].ContainsKey(0) && data.stockMarkets[shopName].playerData.sellOrders[item.shortname][0].Any(x => x.sellerId == player.userID))
                            {
                                approvedListings.Add($"{item.shortname}-0");
                                items++;
                                continue;
                            }
                        }
                    foreach (var item in data.stockMarkets[shopName].stockConfig.customItems)
                    {
                        if (data.stockMarkets[shopName].playerData.buyOrders.ContainsKey(item.Value.shortname) && data.stockMarkets[shopName].playerData.buyOrders[item.Value.shortname].ContainsKey(item.Value.skin) && data.stockMarkets[shopName].playerData.buyOrders[item.Value.shortname][item.Value.skin].Any(x => x.sellerId == player.userID))
                        {
                            approvedListings.Add($"{item.Value.shortname}-{item.Value.skin}");
                            items++;
                            continue;
                        }
                        if (data.stockMarkets[shopName].playerData.sellOrders.ContainsKey(item.Value.shortname) && data.stockMarkets[shopName].playerData.sellOrders[item.Value.shortname].ContainsKey(item.Value.skin) && data.stockMarkets[shopName].playerData.sellOrders[item.Value.shortname][item.Value.skin].Any(x => x.sellerId == player.userID))
                        {
                            approvedListings.Add($"{item.Value.shortname}-{item.Value.skin}");
                            items++;
                            continue;
                        }
                    }
                    maxPages = Mathf.CeilToInt(items / 8f);
                }
                else if (category == "favourites")
                {
                    if (config.debug) Puts($"[DEBUG] Checking category 2.");
                    if (data.stockMarkets[shopName].favourites.ContainsKey(player.userID))
                    {
                        foreach (var categoryVal in stockCategories)
                            foreach (var item in categoryVal.Value)
                            {
                                string keyFormat = $"{item.shortname}-0";
                                if (!data.stockMarkets[shopName].favourites[player.userID].Contains(keyFormat)) continue;
                                approvedListings.Add(keyFormat);
                                items++;
                            }
                        foreach (var item in data.stockMarkets[shopName].stockConfig.customItems)
                        {
                            string keyFormat = $"{item.Value.shortname}-{item.Value.skin}";
                            if (!data.stockMarkets[shopName].favourites[player.userID].Contains(keyFormat)) continue;
                            approvedListings.Add(keyFormat);
                            items++;
                        }
                    }
                    maxPages = Mathf.CeilToInt(items / 8f);
                }
                else if (category == "all_items")
                {
                    if (config.debug) Puts($"[DEBUG] Checking category 3.");
                    foreach (var categoryVal in stockCategories)
                        foreach (var item in categoryVal.Value)
                        {
                            if (config.ignoredShortnames.Contains(item.shortname)) continue;
                            if (shopCache[player].stockMarket.sellToServerOnly && (!data.stockMarkets[shopName].stockConfig.serverSell.ContainsKey(item.shortname) || !data.stockMarkets[shopName].stockConfig.serverSell[item.shortname].ContainsKey(0)))
                                continue;
                            if (shopCache[player].stockMarket.buySellOrders == 'B')
                            {
                                if (!data.stockMarkets[shopName].playerData.buyOrders.ContainsKey(item.shortname) || !data.stockMarkets[shopName].playerData.buyOrders[item.shortname].ContainsKey(0)) continue;
                                else if (!data.stockMarkets[shopName].playerData.buyOrders[item.shortname][0].Any(x => !x.isCanceled)) continue;
                            }
                            else if (shopCache[player].stockMarket.buySellOrders == 'S')
                            {
                                if (!data.stockMarkets[shopName].playerData.sellOrders.ContainsKey(item.shortname) || !data.stockMarkets[shopName].playerData.sellOrders[item.shortname].ContainsKey(0)) continue;
                                else if (!data.stockMarkets[shopName].playerData.sellOrders[item.shortname][0].Any(x => !x.isCanceled)) continue;
                            }
                            approvedListings.Add($"{item.shortname}-0");
                            items++;
                        }
                    foreach (var item in data.stockMarkets[shopName].stockConfig.customItems)
                    {
                        if (shopCache[player].stockMarket.sellToServerOnly && (!data.stockMarkets[shopName].stockConfig.serverSell.ContainsKey(item.Value.shortname) || !data.stockMarkets[shopName].stockConfig.serverSell[item.Value.shortname].ContainsKey(item.Value.skin))) continue;
                        if (shopCache[player].stockMarket.buySellOrders == 'B')
                        {
                            if (!data.stockMarkets[shopName].playerData.buyOrders.ContainsKey(item.Value.shortname) || !data.stockMarkets[shopName].playerData.buyOrders[item.Value.shortname].ContainsKey(item.Value.skin)) continue;
                            else if (!data.stockMarkets[shopName].playerData.buyOrders[item.Value.shortname][item.Value.skin].Any(x => !x.isCanceled)) continue;
                        }
                        else if (shopCache[player].stockMarket.buySellOrders == 'S')
                        {
                            if (!data.stockMarkets[shopName].playerData.sellOrders.ContainsKey(item.Value.shortname) || !data.stockMarkets[shopName].playerData.sellOrders[item.Value.shortname].ContainsKey(item.Value.skin)) continue;
                            else if (!data.stockMarkets[shopName].playerData.sellOrders[item.Value.shortname][item.Value.skin].Any(x => !x.isCanceled)) continue;
                        }
                        string keyFormat = $"{item.Value.shortname}-{item.Value.skin}";
                        approvedListings.Add(keyFormat);
                        items++;
                    }
                    maxPages = Mathf.CeilToInt(items / 8f);
                }
                else if (category == "bank_management")
                {
                    if (config.debug) Puts($"[DEBUG] Checking category 4.");
                    OpenBankManagement(player, shopName);
                    return;
                }
                else
                {
                    if (config.debug) Puts($"[DEBUG] Checking category 5.");
                    foreach (var item in stockCategories[category])
                    {
                        if (config.ignoredShortnames.Contains(item.shortname)) continue;
                        if (shopCache[player].stockMarket.sellToServerOnly && (!data.stockMarkets[shopName].stockConfig.serverSell.ContainsKey(item.shortname) || !data.stockMarkets[shopName].stockConfig.serverSell[item.shortname].ContainsKey(0)))
                            continue;
                        if (shopCache[player].stockMarket.buySellOrders == 'B')
                        {
                            if (!data.stockMarkets[shopName].playerData.buyOrders.ContainsKey(item.shortname) || !data.stockMarkets[shopName].playerData.buyOrders[item.shortname].ContainsKey(0)) continue;
                            else if (!data.stockMarkets[shopName].playerData.buyOrders[item.shortname][0].Any(x => !x.isCanceled)) continue;
                        }
                        else if (shopCache[player].stockMarket.buySellOrders == 'S')
                        {
                            if (!data.stockMarkets[shopName].playerData.sellOrders.ContainsKey(item.shortname) || !data.stockMarkets[shopName].playerData.sellOrders[item.shortname].ContainsKey(0)) continue;
                            else if (!data.stockMarkets[shopName].playerData.sellOrders[item.shortname][0].Any(x => !x.isCanceled)) continue;
                        }
                        approvedListings.Add($"{item.shortname}-0");
                        items++;
                    }
                    foreach (var item in data.stockMarkets[shopName].stockConfig.customItems)
                    {
                        if (shopCache[player].stockMarket.sellToServerOnly && (!data.stockMarkets[shopName].stockConfig.serverSell.ContainsKey(item.Value.shortname) || !data.stockMarkets[shopName].stockConfig.serverSell[item.Value.shortname].ContainsKey(item.Value.skin))) continue;
                        if (shopCache[player].stockMarket.buySellOrders == 'B')
                        {
                            if (!data.stockMarkets[shopName].playerData.buyOrders.ContainsKey(item.Value.shortname) || !data.stockMarkets[shopName].playerData.buyOrders[item.Value.shortname].ContainsKey(item.Value.skin)) continue;
                            else if (!data.stockMarkets[shopName].playerData.buyOrders[item.Value.shortname][item.Value.skin].Any(x => !x.isCanceled)) continue;
                        }
                        else if (shopCache[player].stockMarket.buySellOrders == 'S')
                        {
                            if (!data.stockMarkets[shopName].playerData.sellOrders.ContainsKey(item.Value.shortname) || !data.stockMarkets[shopName].playerData.sellOrders[item.Value.shortname].ContainsKey(item.Value.skin)) continue;
                            else if (!data.stockMarkets[shopName].playerData.sellOrders[item.Value.shortname][item.Value.skin].Any(x => !x.isCanceled)) continue;
                        }
                        if (item.Value.category == category)
                        {
                            approvedListings.Add($"{item.Value.shortname}-{item.Value.skin}");
                            items++;
                        }
                    }
                    maxPages = Mathf.CeilToInt(items / 8f);
                }
                if (config.debug) Puts($"[DEBUG] Finishing checking categories.");
            }
            else
            {
                if (config.debug) Puts($"[DEBUG] Starting searcg check.");
                search = search.ToLower();
                int items = 0;
                foreach (var categoryVal in stockCategories)
                {
                    if (config.shops[shopName].stockConfig.itemCategoryBlacklist.Contains(categoryVal.Key)) continue;
                    foreach (var item in categoryVal.Value)
                    {
                        if (config.ignoredShortnames.Contains(item.shortname)) continue;
                        if (shopCache[player].stockMarket.sellToServerOnly && (!data.stockMarkets[shopName].stockConfig.serverSell.ContainsKey(item.shortname) || !data.stockMarkets[shopName].stockConfig.serverSell[item.shortname].ContainsKey(0))) continue;
                        if (shopCache[player].stockMarket.buySellOrders == 'B')
                        {
                            if (!data.stockMarkets[shopName].playerData.buyOrders.ContainsKey(item.shortname) || !data.stockMarkets[shopName].playerData.buyOrders[item.shortname].ContainsKey(0)) continue;
                            else if (!data.stockMarkets[shopName].playerData.buyOrders[item.shortname][0].Any(x => !x.isCanceled)) continue;
                        }
                        else if (shopCache[player].stockMarket.buySellOrders == 'S')
                        {
                            if (!data.stockMarkets[shopName].playerData.sellOrders.ContainsKey(item.shortname) || !data.stockMarkets[shopName].playerData.sellOrders[item.shortname].ContainsKey(0)) continue;
                            else if (!data.stockMarkets[shopName].playerData.sellOrders[item.shortname][0].Any(x => !x.isCanceled)) continue;
                        }
                        string name = config.translateItems ? Lang($"{item.shortname}-0", player.UserIDString) : item.displayName.english;
                        if (name.ToLower().Contains(search))
                        {
                            approvedListings.Add($"{item.shortname}-0");
                            items++;
                        }
                    }
                }
                if (config.debug) Puts($"[DEBUG] Starting search custom item check.");
                foreach (var item in data.stockMarkets[shopName].stockConfig.customItems)
                {
                    if (config.shops[shopName].stockConfig.itemCategoryBlacklist.Contains(item.Value.category)) continue;
                    string name = config.translateItems ? Lang($"{item.Value.shortname}-{item.Value.skin}", player.UserIDString) : item.Value.displayName;
                    if (name.ToLower().Contains(search))
                    {
                        if (shopCache[player].stockMarket.sellToServerOnly && (!data.stockMarkets[shopName].stockConfig.serverSell.ContainsKey(item.Value.shortname) || !data.stockMarkets[shopName].stockConfig.serverSell[item.Value.shortname].ContainsKey(item.Value.skin))) continue;
                        if (shopCache[player].stockMarket.buySellOrders == 'B')
                        {
                            if (!data.stockMarkets[shopName].playerData.buyOrders.ContainsKey(item.Value.shortname) || !data.stockMarkets[shopName].playerData.buyOrders[item.Value.shortname].ContainsKey(item.Value.skin)) continue;
                            else if (!data.stockMarkets[shopName].playerData.buyOrders[item.Value.shortname][item.Value.skin].Any(x => !x.isCanceled)) continue;
                        }
                        else if (shopCache[player].stockMarket.buySellOrders == 'S')
                        {
                            if (!data.stockMarkets[shopName].playerData.sellOrders.ContainsKey(item.Value.shortname) || !data.stockMarkets[shopName].playerData.sellOrders[item.Value.shortname].ContainsKey(item.Value.skin)) continue;
                            else if (!data.stockMarkets[shopName].playerData.sellOrders[item.Value.shortname][item.Value.skin].Any(x => !x.isCanceled)) continue;
                        }
                        approvedListings.Add($"{item.Value.shortname}-{item.Value.skin}");
                        items++;
                    }
                }
                if (config.debug) Puts($"[DEBUG] Finishing search custom item check.");
                maxPages = Mathf.CeilToInt(items / 8f);
            }
            if (page > maxPages)
            {
                shopCache[player].stockMarket.page = maxPages;
                page = maxPages;
            }
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", $"{page:00}/{maxPages:00}", TextAnchor.MiddleCenter, 14, "393 201", "464 223"); //Text_ListingsPage
            UI_AddButton(container, "Market_MainUI", config.colors.color4, $"UI_ShoppyStock stock page {page - 1}", "340 201", "393 223", "Market_MainUI_Button"); //Button_ListingsPrevPage
            UI_AddImage(container, "Market_MainUI_Button", "UI_ShoppyStock_ArrowLeft", 0, "10 -5", "42 27", config.colors.textColor); //Image_ArrowLeft
            UI_AddButton(container, "Market_MainUI", config.colors.color4, $"UI_ShoppyStock stock page {page + 1}", "464 201", "517 223", "Market_MainUI_Button"); //Button_ListingsNextPage
            UI_AddImage(container, "Market_MainUI_Button", "UI_ShoppyStock_ArrowRight", 0, "10 -5", "42 27", config.colors.textColor); //Image_ArrowRight
            UI_AddPanel(container, "Market_MainUI", config.colors.color3, "-525 141", "-215 189"); //Panel_ShopTitle
            UI_AddIcon(container, "Market_MainUI", config.colors.textColor, "-517 149", "-485 181", "assets/icons/cart.png"); //Image_ShopIcon
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang($"{shopName}_StockMarket", player.UserIDString), TextAnchor.MiddleLeft, 20, "-473 141", "-215 189"); //Text_ShopTitle
            UI_AddPanel(container, "Market_MainUI", config.colors.color3, "-211 141", "525 189"); //Panel_Listings1
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ItemName", player.UserIDString), TextAnchor.MiddleLeft, 20, "-199 141", "55 189"); //Text_Listings1Title
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ServerBuyPrice", player.UserIDString), TextAnchor.MiddleCenter, 20, "225 141", "325 189"); //Text_Listings1ServerBuyPriceTitle
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("PlayerBuyPrice", player.UserIDString), TextAnchor.MiddleCenter, 20, "325 141", "425 189"); //Text_Listings1PlayerBuyPriceTitle
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("PlayerSellPrice", player.UserIDString), TextAnchor.MiddleCenter, 20, "425 141", "525 189"); //Text_Listings1PlayerSellPriceTitle
            int counter = 0;
            int offsetY = 91;
            int listingCount = approvedListings.Count;
            if (config.debug) Puts($"[DEBUG] Starting showing listings.");
            for (int i = 0; i < 8; i += 0)
            {
                counter++;
                if (counter <= 8 * page - 8) continue;
                if (counter > listingCount)
                    UI_AddPanel(container, "Market_MainUI", config.colors.color2, $"-211 {offsetY}", $"525 {offsetY + 48}"); //Panel_NoListing
                else
                {
                    string key = approvedListings.ElementAt(counter - 1);
                    StockItemDefinitionData listing = itemDefinitions[key];
                    UI_AddButton(container, "Market_MainUI", config.colors.color2, $"UI_ShoppyStock stock buy {key}", $"-211 {offsetY}", $"525 {offsetY + 48}", "Market_MainUI_Button"); //Panel_ListingBackground
                    ulong iconSkin = listing.skin;
                    if (config.shops[shopName].stockConfig.showCheapestIcon)
                    {
                        if (data.stockMarkets[shopName].playerData.buyOrders.ContainsKey(listing.shortname) && data.stockMarkets[shopName].playerData.buyOrders[listing.shortname].ContainsKey(listing.skin) && data.stockMarkets[shopName].playerData.buyOrders[listing.shortname][listing.skin].Any(x => !x.isCanceled))
                            iconSkin = data.stockMarkets[shopName].playerData.buyOrders[listing.shortname][listing.skin].Where(x => !x.isCanceled).OrderByDescending(x => x.price).First().item.skin;
                        else if (data.stockMarkets[shopName].playerData.sellOrders.ContainsKey(listing.shortname) && data.stockMarkets[shopName].playerData.sellOrders[listing.shortname].ContainsKey(listing.skin) && data.stockMarkets[shopName].playerData.sellOrders[listing.shortname][listing.skin].Any(x => !x.isCanceled))
                            iconSkin = data.stockMarkets[shopName].playerData.sellOrders[listing.shortname][listing.skin].Where(x => !x.isCanceled).OrderBy(x => x.price).First().item.skin;
                    }
                    UI_AddItemImage(container, "Market_MainUI_Button", listing.shortname, iconSkin, "12 4", "52 44"); //Image_ListingImage
                    UI_AddButton(container, "Market_MainUI_Button", "0 0 0 0", $"UI_ShoppyStock stock favourite {key}", "12 34", "22 44", "Market_MainUI_Button2"); //Panel_FavouriteButton
                    string color = data.stockMarkets[shopName].favourites.ContainsKey(player.userID) && data.stockMarkets[shopName].favourites[player.userID].Contains(key) ? "0.75 0.67 0.11 1" : config.colors.textColor;
                    string icon = data.stockMarkets[shopName].favourites.ContainsKey(player.userID) && data.stockMarkets[shopName].favourites[player.userID].Contains(key) ? "assets/icons/favourite_active.png" : "assets/icons/favourite_inactive.png";
                    UI_AddIcon(container, "Market_MainUI_Button2", color, "0 0", "10 10", icon); //Image_FavouriteIcon
                    string displayName = config.translateItems ? Lang(key, player.UserIDString) : listing.displayName;
                    UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Regular.ttf", displayName, TextAnchor.MiddleLeft, 17, "56 0", "436 48"); //Text_ListingName
                    if (data.stockMarkets[shopName].stockConfig.serverSell.ContainsKey(listing.shortname) && data.stockMarkets[shopName].stockConfig.serverSell[listing.shortname].ContainsKey(listing.skin))
                    {
                        if (!data.stockMarkets[shopName].sellCache.ContainsKey(listing.shortname) || !data.stockMarkets[shopName].sellCache[listing.shortname].ContainsKey(listing.skin))
                            RollPrices(shopName, listing.shortname, listing.skin);
                        UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Regular.ttf", FormatPrice(shopName, data.stockMarkets[shopName].sellCache[listing.shortname][listing.skin].price), TextAnchor.MiddleCenter, 17, "436 8", "536 40"); //Text_ListingServerBuyPrice
                    }
                    else
                        UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Regular.ttf", "-", TextAnchor.MiddleCenter, 17, "436 8", "536 40"); //Text_ListingServerBuyPrice
                    UI_AddPanel(container, "Market_MainUI_Button", config.colors.color4, "536 0", "636 48"); //Panel_PlayerBuyPriceBackground
                    if (data.stockMarkets[shopName].playerData.buyOrders.ContainsKey(listing.shortname) && data.stockMarkets[shopName].playerData.buyOrders[listing.shortname].ContainsKey(listing.skin) && data.stockMarkets[shopName].playerData.buyOrders[listing.shortname][listing.skin].Any(x => !x.isCanceled))
                    {
                        float highestPrice = data.stockMarkets[shopName].playerData.buyOrders[listing.shortname][listing.skin].Where(x => !x.isCanceled).OrderByDescending(x => x.price).First().price;
                        UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Regular.ttf", FormatPrice(shopName, highestPrice), TextAnchor.MiddleCenter, 17, "536 8", "636 40"); //Text_ListingPlayerBuyPrice
                    }
                    else
                        UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Regular.ttf", "-", TextAnchor.MiddleCenter, 17, "536 8", "636 40"); //Text_ListingPlayerBuyPrice
                    if (data.stockMarkets[shopName].playerData.sellOrders.ContainsKey(listing.shortname) && data.stockMarkets[shopName].playerData.sellOrders[listing.shortname].ContainsKey(listing.skin) && data.stockMarkets[shopName].playerData.sellOrders[listing.shortname][listing.skin].Any(x => !x.isCanceled))
                    {
                        float lowestPrice = data.stockMarkets[shopName].playerData.sellOrders[listing.shortname][listing.skin].Where(x => !x.isCanceled).OrderBy(x => x.price).First().price;
                        UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Regular.ttf", FormatPrice(shopName, lowestPrice), TextAnchor.MiddleCenter, 17, "636 8", "736 40"); //Text_ListingPlayerSellPrice
                    }
                    else
                        UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Regular.ttf", "-", TextAnchor.MiddleCenter, 17, "636 8", "736 40"); //Text_ListingPlayerSellPrice
                }
                i++;
                offsetY -= 50;
            }
            if (config.debug) Puts($"[DEBUG] Finished showing listings.");
            counter = 0;
            offsetY = 91;
            Dictionary<string, string> approvedCategories = new Dictionary<string, string>();
            string categoryIcon = "";
            if (config.categoryIcons.ContainsKey("my_listings"))
                categoryIcon = config.categoryIcons["my_listings"];
            approvedCategories.Add("my_listings", categoryIcon);
            if (config.shops[shopName].stockConfig.bankEnabled && (config.shops[shopName].stockConfig.bankPermission == "" || permission.UserHasPermission(player.UserIDString, config.shops[shopName].stockConfig.bankPermission)))
            {
                categoryIcon = "";
                if (config.categoryIcons.ContainsKey("bank_management"))
                    categoryIcon = config.categoryIcons["bank_management"];
                approvedCategories.Add("bank_management", categoryIcon);
            }
            if (config.shops[shopName].stockConfig.favouritesEnabled && (config.shops[shopName].stockConfig.favouritesPermission == "" || permission.UserHasPermission(player.UserIDString, config.shops[shopName].stockConfig.favouritesPermission)))
            {
                categoryIcon = "";
                if (config.categoryIcons.ContainsKey("favourites"))
                    categoryIcon = config.categoryIcons["favourites"];
                approvedCategories.Add("favourites", categoryIcon);
            }
            if (config.shops[shopName].stockConfig.allItemsCategoryEnabled)
            {
                categoryIcon = "";
                if (config.categoryIcons.ContainsKey("all_items"))
                    categoryIcon = config.categoryIcons["all_items"];
                approvedCategories.Add("all_items", categoryIcon);
            }
            if (config.debug) Puts($"[DEBUG] Finished adding default categories.");
            if (config.shops[shopName].stockConfig.overwriteCategoryOrder)
            {
                foreach (var categoryOverwrite in config.shops[shopName].stockConfig.categoryOrder)
                {
                    categoryIcon = "";
                    if (config.categoryIcons.ContainsKey(categoryOverwrite))
                        categoryIcon = config.categoryIcons[categoryOverwrite];
                    approvedCategories.TryAdd(categoryOverwrite, categoryIcon);
                }
            }
            foreach (var cat in stockCategories.Keys)
            {
                if (config.shops[shopName].stockConfig.itemCategoryBlacklist.Contains(cat)) continue;
                categoryIcon = "";
                if (config.categoryIcons.ContainsKey(cat))
                    categoryIcon = config.categoryIcons[cat];
                approvedCategories.TryAdd(cat, categoryIcon);
            }
            if (config.debug) Puts($"[DEBUG] Finished adding categories.");
            int categoryCount = approvedCategories.Count;
            if (categoryPage > categoryCount / 8f)
                categoryPage = Mathf.CeilToInt(categoryCount / 8f);
            for (int i = 0; i < 8; i += 0)
            {
                counter++;
                if (counter <= 8 * categoryPage - 8) continue;
                if (counter > categoryCount)
                {
                    UI_AddPanel(container, "Market_MainUI", config.colors.color4, $"-525 {offsetY}", $"-215 {offsetY + 48}"); //Panel_CategoryIcon
                    i++;
                }
                else
                {
                    KeyValuePair<string, string> categoryListed = approvedCategories.ElementAt(counter - 1);
                    string buttonColor = config.colors.color3;
                    string textColor = config.colors.textColor;
                    string darkIconColor = config.colors.color2;
                    string darkColor = config.colors.color6;
                    if (categoryListed.Key == "my_listings" || categoryListed.Key == "bank_management" || categoryListed.Key == "favourites")
                    {
                        buttonColor = config.colors.positiveDark;
                        textColor = config.colors.positiveLight;
                        darkIconColor = "0.307 0.377 0.183 1";
                        darkColor = "0.513 0.596 0.373 1";
                    }
                    UI_AddButton(container, "Market_MainUI", buttonColor, $"UI_ShoppyStock stock category {categoryListed.Key}", $"-525 {offsetY}", $"-215 {offsetY + 48}", "Market_MainUI_Button"); //Button_Category
                    UI_AddText(container, "Market_MainUI_Button", textColor, "RobotoCondensed-Bold.ttf", Lang($"Category_{categoryListed.Key}", player.UserIDString), TextAnchor.MiddleLeft, 15, "58 8", "262 40"); //Text_CategoryName
                    categoryIcon = categoryListed.Value == "" ? "UI_ShoppyStock_DefaultCategoryIcon" : $"UI_ShoppyStock_Category_{categoryListed.Key}";
                    if (categoryListed.Key == category)
                    {
                        UI_AddPanel(container, "Market_MainUI_Button", buttonColor, "0 0", "48 48"); //Panel_CategoryIconSelected
                        UI_AddImage(container, "Market_MainUI_Button", categoryIcon, 0, "8 8", "40 40", textColor); //Image_CategoryIconSelected
                    }
                    else
                    {
                        UI_AddPanel(container, "Market_MainUI_Button", darkIconColor, "0 0", "48 48"); //Panel_CategoryIcon
                        UI_AddImage(container, "Market_MainUI_Button", categoryIcon, 0, "8 8", "40 40", darkColor); //Image_CategoryIcon
                    }
                    i++;
                }
                offsetY -= 50;
            }
            if (config.debug) Puts($"[DEBUG] Finished displaying categories.");
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", $"{categoryPage:00}/{Mathf.CeilToInt(approvedCategories.Count / 8f):00}", TextAnchor.MiddleLeft, 16, "-521 -283", "-450 -261"); //Text_CategoryPageNumber
            UI_AddButton(container, "Market_MainUI", config.colors.color3, $"UI_ShoppyStock stock categoryPage {categoryPage - 1}", "-325 -283", "-272 -261", "Market_MainUI_Button"); //Button_CategoryPrevPage
            UI_AddImage(container, "Market_MainUI_Button", "UI_ShoppyStock_ArrowLeft", 0, "10 -5", "42 27", config.colors.textColor); //Image_ArrowLeft
            UI_AddButton(container, "Market_MainUI", config.colors.color3, $"UI_ShoppyStock stock categoryPage {categoryPage + 1}", "-268 -283", "-215 -261", "Market_MainUI_Button"); //Button_CategoryNextPage
            UI_AddImage(container, "Market_MainUI_Button", "UI_ShoppyStock_ArrowRight", 0, "10 -5", "42 27", config.colors.textColor); //Image_ArrowRight
            if (permission.UserHasPermission(player.UserIDString, "shoppystock.admin") && category != "" && category != "my_listings" && category != "bank_management" && category != "favourites" && category != "all_items")
            {
                UI_AddButton(container, "Market_MainUI", config.colors.positiveDark, $"UI_ShoppyStock addItem add {shopName} {category} 1", "-211 -283", "155 -261", "Market_MainUI_Button"); //Button_AddItem
                UI_AddText(container, "Market_MainUI_Button", config.colors.positiveLight, "RobotoCondensed-Bold.ttf", Lang("AdminAddItem", player.UserIDString), TextAnchor.MiddleCenter, 14, "0 0", "366 22"); //Text_AddItem
            }
            CuiHelper.DestroyUi(player, "Market_MainUI");
            CuiHelper.AddUi(player, container);
        }

        private void OpenStockItemInfoUI(BasePlayer player, string shopName, string listingKey, bool buyOffers = false, OrderData selectedOrder = null, int selectedIndex = -1, int selectedAmount = 1, int page = 1)
        {
            if (page < 1)
                page = 1;
            if (selectedAmount < 1)
                selectedAmount = 1;
            shopCache[player].stockMarket.listing.buyOffers = buyOffers;
            shopCache[player].stockMarket.listing.page = page;
            shopCache[player].stockMarket.listing.selected = selectedOrder;
            shopCache[player].stockMarket.listing.index = selectedIndex;
            shopCache[player].stockMarket.listing.amount = selectedAmount;
            shopCache[player].stockMarket.listing.key = listingKey;
            stockPosition.TryAdd(player, new StockPosition());
            stockPosition[player].currentItem = listingKey;
            if (config.debug) Puts($"[DEBUG] Starting showing stock item info.");
            CuiElementContainer container = new CuiElementContainer();
            if (config.clickingSound)
                EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/notice/loot.copy.fx.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
            string[] split = listingKey.Split('-');
            string shortname = split[0];
            ulong skin = Convert.ToUInt64(split[1]);
            bool isServerSell = data.stockMarkets[shopName].stockConfig.serverSell.ContainsKey(shortname) && data.stockMarkets[shopName].stockConfig.serverSell[shortname].ContainsKey(skin);
            StockItemDefinitionData defaultItem = null;
            CustomItemData customItem = null;
            if (skin == 0)
                defaultItem = itemDefinitions[listingKey];
            else if (data.stockMarkets[shopName].stockConfig.customItems.ContainsKey(listingKey))
                customItem = data.stockMarkets[shopName].stockConfig.customItems[listingKey];
            if (config.debug) Puts($"[DEBUG] Finsihed basic checks.");
            UI_AddAnchor(container, "Market_StockOfferUI", "Market_AnchorUI", "0 0", "1 1"); //Market_StockOfferUI
            UI_AddBlurPanel(container, "Market_StockOfferUI", "0.3 0.3 0.3 0.2"); //Panel_BackgroundBlur
            UI_AddPanel(container, "Market_StockOfferUI", "0.4 0.4 0.4 0.3", "0 0", "0 0", "0 0", "1 1"); //Panel_BackgroundColor
            UI_AddVignettePanel(container, "Market_StockOfferUI", "0 0 0 0.8"); //Panel_Vignette
            UI_AddAnchor(container, "Market_StockOfferPanelUI", "Market_StockOfferUI", "0.5 0.5", "0.5 0.5"); //Market_StockOfferPanelUI
            UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color1, "-498 -263", "498 263"); //Panel_Background
            UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color2, "-494 211", "-144 259"); //Panel_ServerSellOfferTitleBackground
            UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color2, "-140 211", "494 259"); //Panel_TopPanel
            string buttonColor = buyOffers ? config.colors.color3 : config.colors.color2;
            UI_AddButton(container, "Market_StockOfferPanelUI", buttonColor, $"UI_ShoppyStock stock buyOffers {listingKey}", "-140 211", "60 259", "Market_StockOfferPanelUI_Button"); //Button_BuyOffers
            UI_AddText(container, "Market_StockOfferPanelUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("BuyOffersButton", player.UserIDString), TextAnchor.MiddleCenter, 20, "0 0", "200 48"); //Text_BuyOffers
            buttonColor = !buyOffers ? config.colors.color3 : config.colors.color2;
            UI_AddButton(container, "Market_StockOfferPanelUI", buttonColor, $"UI_ShoppyStock stock sellOffers {listingKey}", "60 211", "260 259", "Market_StockOfferPanelUI_Button"); //Button_SellOffers
            UI_AddText(container, "Market_StockOfferPanelUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("SellOffersButton", player.UserIDString), TextAnchor.MiddleCenter, 20, "0 0", "200 48"); //Text_SellOffers
            UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ServerSellOffer", player.UserIDString), TextAnchor.MiddleCenter, 20, "-494 211", "-144 259"); //Text_ServerSellOfferTitle
            string commande = config.closeOnlySubGui ? "UI_ShoppyStock closeStockInfo" : "UI_ShoppyStock close";
            UI_AddButton(container, "Market_StockOfferPanelUI", config.colors.negativeDark, commande, "446 211", "494 259", "Market_StockOfferPanelUI_Button"); //Button_Close
            UI_AddText(container, "Market_StockOfferPanelUI_Button", config.colors.negativeLight, "RobotoCondensed-Regular.ttf", "✖", TextAnchor.MiddleCenter, 30, "0 0", "48 48"); //Text_Close
            string sellerKey = buyOffers ? Lang("Buyer", player.UserIDString) : Lang("Seller", player.UserIDString);
            if (isServerSell)
            {
                if (config.debug) Puts($"[DEBUG] Checking server sell.");
                ServerSellData serverSell = data.stockMarkets[shopName].stockConfig.serverSell[shortname][skin];
                if (!data.stockMarkets[shopName].sellCache.ContainsKey(shortname) || !data.stockMarkets[shopName].sellCache[shortname].ContainsKey(skin))
                    RollPrices(shopName, shortname, skin);
                ServerSellCacheData serverSellCache = data.stockMarkets[shopName].sellCache[shortname][skin];
                UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color3, "-494 181", "-144 207"); //Panel_ServerSellInfoBackground
                UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ServerSellItem", player.UserIDString), TextAnchor.MiddleLeft, 15, "-490 181", "-229 207"); //Text_ServerSellNameTitle
                UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ItemPrice", player.UserIDString), TextAnchor.MiddleCenter, 15, "-229 181", "-144 207"); //Text_ServerSellPriceTitle
                UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color2, "-494 129", "-144 177"); //Panel_ServerSellOfferBackground
                UI_AddItemImage(container, "Market_StockOfferPanelUI", shortname, skin, "-482 133", "-442 173"); //Image_ListingImage
                string name = config.translateItems ? Lang(listingKey, player.UserIDString) : customItem == null ? defaultItem.displayName : customItem.displayName;
                UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", name, TextAnchor.MiddleLeft, 17, "-438 137", "-229 169"); //Text_ListingName
                UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color4, "-229 129", "-144 177"); //Panel_SellPriceBackground
                UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", FormatPrice(shopName, serverSellCache.price), TextAnchor.MiddleCenter, 17, "-229 137", "-144 169"); //Text_SellPrice

                if (config.shops[shopName].stockConfig.bankEnabled && (config.shops[shopName].stockConfig.bankPermission == "" || permission.UserHasPermission(player.UserIDString, config.shops[shopName].stockConfig.bankPermission)))
                {
                    UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color4, "-494 91", "-229 125"); //Panel_SendAlertPriceBackground
                    UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("SendAlertPrice", player.UserIDString), TextAnchor.MiddleLeft, 17, "-490 91", "-229 125"); //Text_SendAlertPrice
                    UI_AddText(container, "Market_StockOfferPanelUI", config.colors.color6, "RobotoCondensed-Regular.ttf", Lang("MustBeDiscord", player.UserIDString), TextAnchor.UpperRight, 8, "-433 112", "-233 124"); //Text_DiscordInfo
                    UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color3, "-229 91", "-144 125"); //Panel_SendAlertPricePriceBackground
                    float alertPrice = data.stockMarkets[shopName].alertData.ContainsKey(player.userID) && data.stockMarkets[shopName].alertData[player.userID].ContainsKey(listingKey) ? data.stockMarkets[shopName].alertData[player.userID][listingKey].alertPrice : 0;
                    UI_AddInput(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", TextAnchor.MiddleCenter, 17, alertPrice.ToString(), 7, $"UI_ShoppyStock stock setAlert {listingKey}", "-229 91", "-144 125"); //Input_SendAlertPricePrice
                    UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color4, "-494 53", "-229 87"); //Panel_InstaSellPriceBackground
                    UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("InstaSellPrice", player.UserIDString), TextAnchor.MiddleLeft, 17, "-490 53", "-229 87"); //Text_InstaSellPrice
                    UI_AddText(container, "Market_StockOfferPanelUI", config.colors.color6, "RobotoCondensed-Regular.ttf", Lang("MustBeDiscord", player.UserIDString), TextAnchor.UpperRight, 8, "-433 74", "-233 86"); //Text_DiscordInfo
                    UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color3, "-229 53", "-144 87"); //Panel_InstaSellPricePriceBackground
                    float instaSellPrice = data.stockMarkets[shopName].alertData.ContainsKey(player.userID) && data.stockMarkets[shopName].alertData[player.userID].ContainsKey(listingKey) ? data.stockMarkets[shopName].alertData[player.userID][listingKey].instaSellPrice : 0;
                    UI_AddInput(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", TextAnchor.MiddleCenter, 17, instaSellPrice.ToString(), 7, $"UI_ShoppyStock stock setInstaSell {listingKey}", "-229 53", "-144 87"); //Input_InstaSellPricePrice
                    UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color2, "-317 26", "-144 49"); //Panel_ItemCountInBankBackground
                    int itemCount = data.stockMarkets[shopName].playerData.playerBanks.ContainsKey(player.userID) && data.stockMarkets[shopName].playerData.playerBanks[player.userID].ContainsKey(listingKey) ? data.stockMarkets[shopName].playerData.playerBanks[player.userID][listingKey].amount : 0;
                    UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang("ItemCountInBank", player.UserIDString, itemCount), TextAnchor.MiddleCenter, 12, "-317 26", "-144 49"); //Text_ItemCountInBank
                    UI_AddButton(container, "Market_StockOfferPanelUI", config.colors.positiveDark, $"UI_ShoppyStock stock sellFromBank {listingKey}", "-317 0", "-144 26", "Market_StockOfferPanelUI_Button"); //Button_SellAllFromBank
                    UI_AddText(container, "Market_StockOfferPanelUI_Button", config.colors.positiveLight, "RobotoCondensed-Bold.ttf", Lang("AutoSellFromBank", player.UserIDString), TextAnchor.MiddleCenter, 20, "0 0", "173 26"); //Text_SelAllFromBank
                }

                UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color2, "-494 26", "-321 49"); //Panel_OpenSellInventoryHintBackground
                UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang("OpenSellInventory", player.UserIDString), TextAnchor.MiddleCenter, 12, "-494 26", "-321 49"); //Text_OpenSellInventoryHint
                UI_AddButton(container, "Market_StockOfferPanelUI", config.colors.positiveDark, $"UI_ShoppyStock stock openSell", "-494 0", "-321 26", "Market_StockOfferPanelUI_Button"); //Button_SellToServer
                UI_AddText(container, "Market_StockOfferPanelUI_Button", config.colors.positiveLight, "RobotoCondensed-Bold.ttf", Lang("SellToServerButton", player.UserIDString), TextAnchor.MiddleCenter, 20, "0 0", "173 26"); //Text_SellToServer

                UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color3, "-494 -30", "-144 -4"); //Panel_PriceHistoryTitleBackground
                UI_AddButton(container, "Market_StockOfferPanelUI", config.colors.color2, $"UI_ShoppyStock stock timestamp", "-229 -30", "-144 -4", "Market_StockOfferPanelUI_Button"); //Button_ChangeTimestamp
                UI_AddText(container, "Market_StockOfferPanelUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ChangeTimestamp", player.UserIDString), TextAnchor.MiddleCenter, 10, "0 0", "86 26"); //Text_ChangeTImestamp
                UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("PriceHistory", player.UserIDString), TextAnchor.MiddleLeft, 15, "-490 -30", "-260 -4"); //Text_PriceHistoryTitle
                UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color2, "-494 -259", "-144 -34"); //Panel_PriceHistoryChartBackground

                if (config.debug) Puts($"[DEBUG] Starting makin chart.");
                int time = shopCache[player].stockMarket.listing.timestamp;
                if (time == 0)
                    time = config.timestamps.First().Key;
                string formattedTime = FormatTime(time);
                UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang("LastTitle", player.UserIDString, formattedTime).ToUpper(), TextAnchor.UpperLeft, 17, "-490 -58", "-390 -36"); //Text_TimestampLength
                UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.positiveDark, "-162 -52", "-148 -38"); //Panel_HighestPriceColorLegend
                UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.negativeDark, "-162 -70", "-148 -56"); //Panel_LowestPriceColorLegend
                UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang("HighestPrice", player.UserIDString), TextAnchor.MiddleRight, 12, "-266 -52", "-166 -38"); //Text_HighestPrice
                UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang("LowestPrice", player.UserIDString), TextAnchor.MiddleRight, 12, "-266 -70", "-166 -56"); //Text_LowestPrice
                UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.textColor, "-468 -246", "-466 -62"); //Panel_VerticalLine
                UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.textColor, "-467 -246", "-152 -244"); //Panel_HorizontalLine
                UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color4, "-466 -230", "-152 -216"); //Panel_BackgroundChartLine
                UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color4, "-466 -202", "-152 -188"); //Panel_BackgroundChartLine
                UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color4, "-466 -174", "-152 -160"); //Panel_BackgroundChartLine
                UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color4, "-466 -146", "-152 -132"); //Panel_BackgroundChartLine
                UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color4, "-466 -118", "-152 -104"); //Panel_BackgroundChartLine
                UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color4, "-466 -90", "-152 -76"); //Panel_BackgroundChartLine
                int requiredRecordAmount = Mathf.CeilToInt(time / config.shops[shopName].stockConfig.updateInterval);
                int splitRecordAmount = Mathf.RoundToInt(requiredRecordAmount / 7);
                if (data.stockMarkets[shopName].sellCache[shortname][skin].priceHistory.Count >= requiredRecordAmount && splitRecordAmount > 0)
                {
                    List<float> sortedList = data.stockMarkets[shopName].sellCache[shortname][skin].priceHistory.Take(splitRecordAmount * 7).OrderBy(x => x).ToList();
                    float topValue = sortedList.Last();
                    float lowestValue = sortedList.First();
                    float valueRange = topValue - lowestValue;
                    float valuePerPixel = valueRange / 160;
                    List<List<float>> prices = new List<List<float>>() {
                        new List<float>(data.stockMarkets[shopName].sellCache[shortname][skin].priceHistory.Take(splitRecordAmount).OrderBy(x => x)),
                        new List<float>(data.stockMarkets[shopName].sellCache[shortname][skin].priceHistory.Skip(splitRecordAmount).Take(splitRecordAmount).OrderBy(x => x)),
                        new List<float>(data.stockMarkets[shopName].sellCache[shortname][skin].priceHistory.Skip(splitRecordAmount * 2).Take(splitRecordAmount).OrderBy(x => x)),
                        new List<float>(data.stockMarkets[shopName].sellCache[shortname][skin].priceHistory.Skip(splitRecordAmount * 3).Take(splitRecordAmount).OrderBy(x => x)),
                        new List<float>(data.stockMarkets[shopName].sellCache[shortname][skin].priceHistory.Skip(splitRecordAmount * 4).Take(splitRecordAmount).OrderBy(x => x)),
                        new List<float>(data.stockMarkets[shopName].sellCache[shortname][skin].priceHistory.Skip(splitRecordAmount * 5).Take(splitRecordAmount).OrderBy(x => x)),
                        new List<float>(data.stockMarkets[shopName].sellCache[shortname][skin].priceHistory.Skip(splitRecordAmount * 6).Take(splitRecordAmount).OrderBy(x => x))
                    };
                    float offset = -186;
                    for (int i = 0; i < 7; i++)
                    {
                        float topBar = (prices[i].Last() - lowestValue) / valuePerPixel;
                        float lowestBar = (prices[i].First() - lowestValue) / valuePerPixel;
                        UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.positiveDark, $"{offset} -240", $"{offset + 11} {-240 + topBar}"); //Panel_HighestPrice7
                        UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.negativeDark, $"{offset + 13} -240", $"{offset + 24} {-240 + lowestBar}"); //Panel_LowestPrice7
                        offset -= 45;
                    }
                    int splitTime = Mathf.FloorToInt(time / 7f);
                    float splitValue = valueRange / 6f;
                    UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", $"-{formattedTime}", TextAnchor.MiddleCenter, 9, "-456 -259", "-432 -246"); //Text_Hour1
                    UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", $"-{FormatTime(splitTime * 6)}", TextAnchor.MiddleCenter, 9, "-411 -259", "-387 -246"); //Text_Hour2
                    UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", $"-{FormatTime(splitTime * 5)}", TextAnchor.MiddleCenter, 9, "-366 -259", "-342 -246"); //Text_Hour3
                    UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", $"-{FormatTime(splitTime * 4)}", TextAnchor.MiddleCenter, 9, "-321 -259", "-297 -246"); //Text_Hour4
                    UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", $"-{FormatTime(splitTime * 3)}", TextAnchor.MiddleCenter, 9, "-276 -259", "-252 -246"); //Text_Hour5
                    UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", $"-{FormatTime(splitTime * 2)}", TextAnchor.MiddleCenter, 9, "-231 -259", "-207 -246"); //Text_Hour6
                    UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", $"-{FormatTime(splitTime)}", TextAnchor.MiddleCenter, 9, "-186 -259", "-162 -246"); //Text_Hour7
                    UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", (lowestValue + (splitValue * 6)).ToString("0.###"), TextAnchor.MiddleRight, 7, "-494 -89", "-470 -77"); //Text_Value1
                    UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", (lowestValue + (splitValue * 5)).ToString("0.###"), TextAnchor.MiddleRight, 7, "-494 -117", "-470 -105"); //Text_Value2
                    UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", (lowestValue + (splitValue * 4)).ToString("0.###"), TextAnchor.MiddleRight, 7, "-494 -145", "-470 -133"); //Text_Value3
                    UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", (lowestValue + (splitValue * 3)).ToString("0.###"), TextAnchor.MiddleRight, 7, "-494 -173", "-470 -161"); //Text_Value4
                    UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", (lowestValue + (splitValue * 2)).ToString("0.###"), TextAnchor.MiddleRight, 7, "-494 -201", "-470 -189"); //Text_Value5
                    UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", (lowestValue + splitValue).ToString("0.###"), TextAnchor.MiddleRight, 7, "-494 -229", "-470 -217"); //Text_Value6
                }
                else
                    UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang("NoPriceData", player.UserIDString), TextAnchor.MiddleCenter, 18, "-466 -246", "-152 -62"); //Text_NoPriceData
                if (config.debug) Puts($"[DEBUG] Finsihed makin chart.");
            }
            else
            {
                UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color2, "-494 -259", "-144 207"); //Panel_NotAvailableForSaleBackground
                UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("NotForServerSale", player.UserIDString), TextAnchor.MiddleCenter, 20, "-494 -259", "-144 207"); //Text_Value6
            }

            UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color3, "-140 181", "494 207"); //Panel_ListingInfoBackground
            UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ItemName", player.UserIDString), TextAnchor.MiddleLeft, 15, "-136 181", "94 207"); //Text_ItemName
            UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("PurchaseAmount", player.UserIDString), TextAnchor.MiddleCenter, 15, "94 181", "194 207"); //Text_OfferAmountTitle
            UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("Details", player.UserIDString), TextAnchor.MiddleCenter, 15, "194 181", "294 207"); //Text_OfferDetailsTitle
            UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", sellerKey, TextAnchor.MiddleCenter, 15, "294 181", "394 207"); //Text_OfferSellerTitle
            UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ItemPrice", player.UserIDString), TextAnchor.MiddleCenter, 15, "394 181", "494 207"); //Text_OfferPriceTitle

            if (config.debug) Puts($"[DEBUG] Orders checkiing start.");
            List<OrderData> orders = null;
            if (buyOffers && data.stockMarkets[shopName].playerData.buyOrders.ContainsKey(shortname) && data.stockMarkets[shopName].playerData.buyOrders[shortname].ContainsKey(skin))
                orders = data.stockMarkets[shopName].playerData.buyOrders[shortname][skin].Where(x => x.sellerId == player.userID || !x.isCanceled).OrderByDescending(x => x.price).ToList();
            else if (!buyOffers && data.stockMarkets[shopName].playerData.sellOrders.ContainsKey(shortname) && data.stockMarkets[shopName].playerData.sellOrders[shortname].ContainsKey(skin))
                orders = data.stockMarkets[shopName].playerData.sellOrders[shortname][skin].Where(x => x.sellerId == player.userID || !x.isCanceled).OrderBy(x => x.price).ToList();
            bool anyOrder = orders != null && orders.Count > 0;
            int offsetY = 129;
            int orderCount = orders != null ? orders.Count : 0;
            int counter = 0;
            if (config.debug) Puts($"[DEBUG] Orders starting looping.");
            for (int i = 0; i < 5; i += 0)
            {
                if (!anyOrder)
                {
                    UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color2, $"-140 {offsetY}", $"494 {offsetY + 48}"); //Panel_NoListing
                    if (i == 0)
                        UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang("NoOffersFound", player.UserIDString), TextAnchor.MiddleCenter, 20, $"-140 {offsetY}", $"494 {offsetY + 48}"); //Text_NoListing
                }
                else
                {
                    counter++;
                    if (counter < page * 5 - 5) continue;
                    if (orderCount < (page * 5) - 4 + i || orderCount < counter)
                    {
                        UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color2, $"-140 {offsetY}", $"494 {offsetY + 48}"); //Panel_NoListing
                        i++;
                        offsetY -= 50;
                        continue;
                    }
                    OrderData order = orders.ElementAt(counter - 1);
                    if (order.sellerId == player.userID && !shopCache[player].stockMarket.listing.hideOwned) continue;
                    if (order.isCanceled && order.sellerId != player.userID) continue;
                    if (selectedIndex == i)
                    {
                        selectedOrder = order;
                        shopCache[player].stockMarket.listing.selected = order;
                        UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.positiveDark, $"-141 {offsetY - 1}", $"495 {offsetY + 49}"); //Panel_ListingSelected
                    }
                    string command = $"UI_ShoppyStock stock showBuy {i}";
                    if (order.isCanceled)
                        command = $"UI_ShoppyStock stock refundItem {counter - 1}";
                    else if (order.sellerId == player.userID)
                        command = $"UI_ShoppyStock stock cancelListing {counter - 1}";
                    UI_AddButton(container, "Market_StockOfferPanelUI", config.colors.color2, command, $"-140 {offsetY}", $"494 {offsetY + 48}", "Market_StockOfferPanelUI_Button"); //Button_Listing
                    UI_AddItemImage(container, "Market_StockOfferPanelUI_Button", order.item.shortname, order.item.skin, "4 4", "44 44"); //Image_ListingImage
                    string displayedName = config.translateItems ? Lang($"{order.item.shortname}-{order.item.skin}", player.UserIDString) : order.item.displayName == null || order.item.displayName == "" ? ItemManager.FindItemDefinition(order.item.shortname).displayName.english : order.item.displayName;
                    int size = 17;
                    int nameLength = displayedName.Length;
                    if (nameLength > 64)
                        size = 12;
                    else if (nameLength > 48)
                        size = 13;
                    else if (nameLength > 32)
                        size = 14;
                    else if (nameLength > 16)
                        size = 15;
                    UI_AddText(container, "Market_StockOfferPanelUI_Button", config.colors.textColor, "RobotoCondensed-Regular.ttf", displayedName, TextAnchor.MiddleLeft, size, "48 0", "234 48"); //Text_ListingName
                    UI_AddText(container, "Market_StockOfferPanelUI_Button", config.colors.textColor, "RobotoCondensed-Regular.ttf", $"x{order.item.amount}", TextAnchor.MiddleCenter, 17, "234 8", "334 40"); //Text_Amount
                    UI_AddPanel(container, "Market_StockOfferPanelUI_Button", config.colors.color4, "334 0", "434 48"); //Panel_DetailsBackground
                    if (order.isCanceled)
                    {
                        UI_AddText(container, "Market_StockOfferPanelUI_Button", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang($"Canceled_Info", player.UserIDString), TextAnchor.MiddleCenter, 12, "334 8", "434 40"); //Text_Details
                        UI_AddButton(container, "Market_StockOfferPanelUI_Button", config.colors.transparentColor1, $"UI_ShoppyStock stock readdItem {counter - 1}", "642 14", "720 32", "Market_StockOfferPanelUI_Button_Button"); //Button_AddBack
                        UI_AddText(container, "Market_StockOfferPanelUI_Button_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("AddBack", player.UserIDString), TextAnchor.MiddleCenter, 10, "0 0", "78 20"); //Text_AddBack
                    }
                    else if (order.item.dataInt != 0)
                        UI_AddText(container, "Market_StockOfferPanelUI_Button", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang($"Genes_Info", player.UserIDString, GrowableGeneEncoding.DecodeIntToGeneString(order.item.dataInt)), TextAnchor.MiddleCenter, 12, "334 8", "434 40"); //Text_Details
                    else if (order.item.maxCondition > 0)
                        UI_AddText(container, "Market_StockOfferPanelUI_Button", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang($"Condition_Info", player.UserIDString, (order.item.condition / order.item.maxCondition * 100).ToString("0.#")), TextAnchor.MiddleCenter, 12, "334 8", "434 40"); //Text_Details
                    else
                    {
                        foreach (var customInfo in config.customItemInfo)
                            if (customInfo.Value.Contains(order.item.skin))
                            {
                                UI_AddText(container, "Market_StockOfferPanelUI_Button", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang($"{customInfo.Key}_Info", player.UserIDString), TextAnchor.MiddleCenter, 12, "334 8", "434 40"); //Text_Details
                                break;
                            }
                    }
                    size = 17;
                    nameLength = order.sellerName.Length;
                    if (nameLength > 14)
                        size = 12;
                    else if (nameLength > 12)
                        size = 13;
                    else if (nameLength > 10)
                        size = 14;
                    else if (nameLength > 8)
                        size = 15;
                    UI_AddText(container, "Market_StockOfferPanelUI_Button", config.colors.textColor, "RobotoCondensed-Regular.ttf", order.sellerName, TextAnchor.MiddleCenter, size, "434 8", "534 40"); //Text_Nickname
                    UI_AddPanel(container, "Market_StockOfferPanelUI_Button", config.colors.color4, "534 0", "634 48"); //Panel_BuyPriceBackground
                    UI_AddText(container, "Market_StockOfferPanelUI_Button", config.colors.textColor, "RobotoCondensed-Regular.ttf", FormatPrice(shopName, order.price), TextAnchor.MiddleCenter, 17, "534 8", "634 40"); //Text_BuyPrice
                    if (order.sellerId == player.userID && shopCache[player].stockMarket.listing.hideOwned)
                    {
                        string color = "0.45 0.237 0.194 0.05";
                        if (order.isCanceled)
                            color = "0.45 0.237 0.194 0.15";
                        UI_AddPanel(container, "Market_StockOfferPanelUI_Button", color, $"0 0", $"634 48"); //Panel_ListingOwned

                    }
                }
                if (config.debug) Puts($"[DEBUG] Orders finished looping.");
                i++;
                offsetY -= 50;
            }
            UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color3, "-140 -101", "494 -75"); //Panel_OfferControlPanel
            UI_AddButton(container, "Market_StockOfferPanelUI", config.colors.color2, $"UI_ShoppyStock stock showMyOffers", "-136 -99", "-114 -77", "Market_StockOfferPanelUI_Button"); //Button_ShowMyOffers
            string ownedHidden = !shopCache[player].stockMarket.listing.hideOwned ? "" : "✖";
            UI_AddText(container, "Market_StockOfferPanelUI_Button", config.colors.textColor, "RobotoCondensed-Regular.ttf", ownedHidden, TextAnchor.MiddleCenter, 14, "0 0", "22 22"); //Text_ShowMyOIffers
            UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang("ShowMyOffers", player.UserIDString), TextAnchor.MiddleLeft, 13, "-110 -99", "40 -77"); //Text_ShowMyOffersTitle
            UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color2, "366 -99", "437 -77"); //Panel_PageBackground
            int maxPages = Mathf.CeilToInt(orderCount / 5f);
            if (page > maxPages)
                page = maxPages;
            UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", $"{page:00}/{maxPages:00}", TextAnchor.MiddleCenter, 14, "366 -99", "437 -77"); //Text_Page
            UI_AddButton(container, "Market_StockOfferPanelUI", config.colors.color4, $"UI_ShoppyStock stock itemPage {page - 1}", "313 -99", "366 -77", "Market_StockOfferPanelUI_Button"); //Button_PrevPage
            UI_AddImage(container, "Market_StockOfferPanelUI_Button", "UI_ShoppyStock_ArrowLeft", 0, "10 -5", "42 27", config.colors.textColor); //Image_ArrowLeft
            UI_AddButton(container, "Market_StockOfferPanelUI", config.colors.color4, $"UI_ShoppyStock stock itemPage {page + 1}", "437 -99", "490 -77", "Market_StockOfferPanelUI_Button"); //Button_NextPage
            UI_AddImage(container, "Market_StockOfferPanelUI_Button", "UI_ShoppyStock_ArrowRight", 0, "10 -5", "42 27", config.colors.textColor); //Image_ArrowRight
            UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color3, "-140 -131", "494 -105"); //Panel_SelectedItemInfoBackground
            UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ItemName", player.UserIDString), TextAnchor.MiddleLeft, 15, "-136 -131", "94 -105"); //Text_SelectedItemName
            UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("PurchaseAmount", player.UserIDString), TextAnchor.MiddleCenter, 15, "94 -131", "194 -105"); //Text_SelectedItemAmount
            UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("Details", player.UserIDString), TextAnchor.MiddleCenter, 15, "194 -131", "294 -105"); //Text_SelectedItemDetails
            UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", sellerKey, TextAnchor.MiddleCenter, 15, "294 -131", "394 -105"); //Text_SelectedItemSeller
            UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ItemPrice", player.UserIDString), TextAnchor.MiddleCenter, 15, "394 -131", "494 -105"); //Text_SelectedItemPrice
            if (config.debug) Puts($"[DEBUG] Checking selected order.");
            if (selectedOrder != null)
            {
                UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color2, "-140 -183", "494 -135"); //Panel_SelectedItemBackground
                UI_AddItemImage(container, "Market_StockOfferPanelUI", selectedOrder.item.shortname, selectedOrder.item.skin, "-136 -179", "-96 -139"); //Image_SelectedItemImage
                string displayedName = config.translateItems ? Lang($"{selectedOrder.item.shortname}-{selectedOrder.item.skin}", player.UserIDString) : selectedOrder.item.displayName == null || selectedOrder.item.displayName == "" ? ItemManager.FindItemDefinition(selectedOrder.item.shortname).displayName.english : selectedOrder.item.displayName;
                int size = 17;
                int nameLength = displayedName.Length;
                if (nameLength > 64)
                    size = 12;
                else if (nameLength > 48)
                    size = 13;
                else if (nameLength > 32)
                    size = 14;
                else if (nameLength > 16)
                    size = 15;
                if (config.debug) Puts($"[DEBUG] Selected after name size length.");
                UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", displayedName, TextAnchor.MiddleLeft, size, "-92 -183", "94 -135"); //Text_SelectedItemName
                UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", $"x{selectedOrder.item.amount}", TextAnchor.MiddleCenter, 17, "94 -175", "194 -143"); //Text_SelectedItemAmount
                UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color4, "194 -183", "294 -135"); //Panel_SelectedItemDetailsBackground
                if (selectedOrder.item.dataInt != 0)
                    UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang($"Genes_Info", player.UserIDString, GrowableGeneEncoding.DecodeIntToGeneString(selectedOrder.item.dataInt)), TextAnchor.MiddleCenter, 12, "194 -175", "294 -143"); //Text_SelectedItemDetails
                else if (selectedOrder.item.maxCondition > 0)
                    UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang($"Condition_Info", player.UserIDString, (selectedOrder.item.condition / selectedOrder.item.maxCondition * 100).ToString("0.#")), TextAnchor.MiddleCenter, 12, "194 -175", "294 -143"); //Text_SelectedItemDetails
                else
                {
                    foreach (var customInfo in config.customItemInfo)
                        if (customInfo.Value.Contains(selectedOrder.item.skin))
                        {
                            UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang($"{customInfo.Key}_Info", player.UserIDString), TextAnchor.MiddleCenter, 12, "194 -175", "294 -143"); //Text_SelectedItemDetails
                            break;
                        }
                }
                size = 17;
                nameLength = selectedOrder.sellerName.Length;
                if (nameLength > 14)
                    size = 12;
                else if (nameLength > 12)
                    size = 13;
                else if (nameLength > 10)
                    size = 14;
                else if (nameLength > 8)
                    size = 15;
                UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", selectedOrder.sellerName, TextAnchor.MiddleCenter, size, "294 -175", "394 -143"); //Text_SelectedItemSeller
                UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color4, "394 -183", "494 -135"); //Panel_SelectedItemPriceBackground
                UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", FormatPrice(shopName, selectedOrder.price), TextAnchor.MiddleCenter, 17, "394 -175", "494 -143"); //Text_SelectedItemPrice
                UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color4, "-140 -221", "69 -187"); //Panel_BalanceBackground
                UI_AddImage(container, "Market_StockOfferPanelUI", $"UI_ShoppyStock_{shopName}_Icon", 0, "-136 -217", "-110 -191", config.colors.textColor); //Image_Currency
                UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", FormatCurrency(shopName, player), TextAnchor.MiddleCenter, 20, "-106 -221", "69 -187"); //Text_Balance
                UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color4, "73 -221", "281 -187"); //Panel_ItemAmountBackground
                UI_AddButton(container, "Market_StockOfferPanelUI", config.colors.positiveDark, $"UI_ShoppyStock stock amount {selectedAmount + 1}", "247 -221", "281 -187", "Market_StockOfferPanelUI_Button"); //Button_Increase
                UI_AddText(container, "Market_StockOfferPanelUI_Button", config.colors.positiveLight, "RobotoCondensed-Bold.ttf", "+", TextAnchor.MiddleCenter, 25, "0 0", "34 34"); //Text_Increase
                UI_AddInput(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", TextAnchor.MiddleCenter, 20, selectedAmount.ToString(), 9, $"UI_ShoppyStock stock amount", "107 -221", "247 -187"); //Input_Amount
                UI_AddButton(container, "Market_StockOfferPanelUI", config.colors.negativeDark, $"UI_ShoppyStock stock amount {selectedAmount - 1}", "73 -221", "107 -187", "Market_StockOfferPanelUI_Button"); //Button_Decrease
                UI_AddText(container, "Market_StockOfferPanelUI_Button", config.colors.negativeLight, "RobotoCondensed-Bold.ttf", "-", TextAnchor.MiddleCenter, 25, "0 0", "34 34"); //Text_Decrease
                UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color4, "285 -221", "494 -187"); //Panel_SumPriceBackground
                UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color3, "394 -221", "494 -187"); //Panel_SumPricePriceBackground
                UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("SumPrice", player.UserIDString), TextAnchor.MiddleLeft, 20, "289 -221", "394 -187"); //Text_SumPrice
                UI_AddText(container, "Market_StockOfferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", FormatPrice(shopName, Mathf.CeilToInt(selectedAmount * selectedOrder.price), true), TextAnchor.MiddleCenter, 20, "394 -221", "494 -187"); //Text_SumPricePrice
                if (buyOffers)
                {
                    UI_AddButton(container, "Market_StockOfferPanelUI", config.colors.positiveDark, $"UI_ShoppyStock stock sellAction", "285 -259", "494 -225", "Market_StockOfferPanelUI_Button"); //Button_Purchase
                    UI_AddText(container, "Market_StockOfferPanelUI_Button", config.colors.positiveLight, "RobotoCondensed-Bold.ttf", Lang("SellButton", player.UserIDString), TextAnchor.MiddleCenter, 20, "0 0", "209 34"); //Text_Purchase
                }
                else
                {
                    UI_AddButton(container, "Market_StockOfferPanelUI", config.colors.positiveDark, $"UI_ShoppyStock stock buyAction", "285 -259", "494 -225", "Market_StockOfferPanelUI_Button"); //Button_Purchase
                    UI_AddText(container, "Market_StockOfferPanelUI_Button", config.colors.positiveLight, "RobotoCondensed-Bold.ttf", Lang("PurchaseButton", player.UserIDString), TextAnchor.MiddleCenter, 20, "0 0", "209 34"); //Text_Purchase
                }
            }
            else
            {
                UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color2, "-140 -221", "494 -135"); //Panel_NoSelectedItem
                UI_AddPanel(container, "Market_StockOfferPanelUI", config.colors.color2, "285 -259", "494 -225"); //Panel_NoSelectedItem
            }
            UI_AddButton(container, "Market_StockOfferPanelUI", config.colors.color2, $"UI_ShoppyStock stock closeOffer", "73 -259", "281 -225", "Market_StockOfferPanelUI_Button"); //Button_GoBack
            UI_AddText(container, "Market_StockOfferPanelUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("CancelButton", player.UserIDString), TextAnchor.MiddleCenter, 20, "0 0", "208 34"); //Text_GoBack
            if (buyOffers)
            {
                UI_AddButton(container, "Market_StockOfferPanelUI", config.colors.color2, $"UI_ShoppyStock stock createBuy", "-140 -259", "69 -225", "Market_StockOfferPanelUI_Button"); //Button_CreateBuyOffer
                UI_AddText(container, "Market_StockOfferPanelUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("CreateBuyOffer", player.UserIDString), TextAnchor.MiddleCenter, 20, "0 0", "209 34"); //Text_CreateBuyOffer
            }
            else
            {
                UI_AddButton(container, "Market_StockOfferPanelUI", config.colors.color2, $"UI_ShoppyStock stock createSell", "-140 -259", "69 -225", "Market_StockOfferPanelUI_Button"); //Button_CreateSellOffer
                UI_AddText(container, "Market_StockOfferPanelUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("CreateSellOffer", player.UserIDString), TextAnchor.MiddleCenter, 20, "0 0", "209 34"); //Text_CreateSellOffer
            }
            if (config.debug) Puts($"[DEBUG] Finsihing listing showing.");
            CuiHelper.DestroyUi(player, "Market_StockOfferUI");
            CuiHelper.AddUi(player, container);
        }

        private void CreateBuySellRequest(BasePlayer player, string shopName, bool buyRequest = false, float price = 0, int amount = 1)
        {
            CuiElementContainer container = new CuiElementContainer();
            if (config.clickingSound)
                EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/notice/loot.copy.fx.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
            Mailbox mailbox = null;
            if (!mailboxes.ContainsKey(player))
            {
                if (config.debug) Puts($"[DEBUG] Creating mailbox.");
                player.EndLooting();
                mailbox = GameManager.server.CreateEntity("assets/prefabs/deployable/mailbox/mailbox.deployed.prefab", new Vector3(player.transform.position.x, player.transform.position.y - 40f, player.transform.position.z)) as Mailbox;
                mailbox.OwnerID = player.userID;
                mailbox.skinID = 1;
                UnityEngine.Object.DestroyImmediate(mailbox.GetComponent<GroundWatch>());
                UnityEngine.Object.DestroyImmediate(mailbox.GetComponent<DestroyOnGroundMissing>());
                mailbox.Spawn();
                mailboxes.Add(player, mailbox);
                mailbox.InputContainer.canAcceptItem = (iItem, i) => true;
                mailbox.InputContainer.onDirty += () => CreateBuySellRequest(player, shopName, buyRequest, price, amount);
                if (buyRequest)
                {
                    string key = shopCache[player].stockMarket.listing.key;
                    string[] nameSplit = key.Split('-');
                    ulong skin = Convert.ToUInt64(nameSplit[1]);
                    Item buyItem = ItemManager.CreateByName(nameSplit[0], 1, skin);
                    if (data.stockMarkets[shopName].stockConfig.customItems.ContainsKey(key) && data.stockMarkets[shopName].stockConfig.customItems[key].displayName != "")
                        buyItem.name = $"{data.stockMarkets[shopName].stockConfig.customItems[key].displayName}";
                    else
                        buyItem.name = $"{buyItem.info.displayName.english}";
                    buyItem.MoveToContainer(mailbox.InputContainer, 0);
                    mailbox.InputContainer.SetLocked(true);
                }
                timer.Once(0.1f, () => {
                    player.inventory.loot.AddContainer(mailbox.InputContainer);
                    player.inventory.loot.entitySource = mailbox;
                    player.inventory.loot.PositionChecks = false;
                    player.inventory.loot.MarkDirty();
                    player.inventory.loot.SendImmediate();
                    player.ClientRPC<string>(RpcTarget.Player("RPC_OpenLootPanel", player), "mailboxentry");
                });
            }
            if (config.debug) Puts($"[DEBUG] Finished checking mailbox.");
            shopCache[player].stockMarket.request.buyRequest = buyRequest;
            shopCache[player].stockMarket.request.price = price;
            shopCache[player].stockMarket.request.amount = amount;
            mailbox = mailboxes[player];
            Item inputItem = mailbox.InputContainer.GetSlot(0);
            UI_AddAnchor(container, "Market_CreateOfferUI", "Hud.Menu", "0.5 0", "0.5 0"); //Market_CreateOfferUI
            if (inputItem != null)
            {
                if (config.debug) Puts($"[DEBUG] Checking input item.");
                shopCache[player].stockMarket.request.itemShortname = inputItem.info.shortname;
                shopCache[player].stockMarket.request.itemSkin = inputItem.skin;
                string title = buyRequest ? Lang("NewBuyRequestTitle", player.UserIDString) : Lang("NewSellRequestTitle", player.UserIDString);
                UI_AddText(container, "Market_CreateOfferUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", title, TextAnchor.MiddleLeft, 21, "192 475", "392 510"); //Text_NewListingTitle
                UI_AddPanel(container, "Market_CreateOfferUI", config.colors.transparentColor2, "192 427", "572 475"); //Panel_ItemBackground
                UI_AddItemImage(container, "Market_CreateOfferUI", inputItem.info.shortname, inputItem.skin, "196 431", "236 471"); //Image_ItemImage
                string name = config.translateItems ? Lang($"{inputItem.info.shortname}-{inputItem.skin}", player.UserIDString) : inputItem.name != null && inputItem.name != "" ? inputItem.name : inputItem.info.displayName.english;
                UI_AddText(container, "Market_CreateOfferUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", name, TextAnchor.MiddleLeft, 12, "240 427", "472 475"); //Text_ItemName
                UI_AddPanel(container, "Market_CreateOfferUI", config.colors.transparentColor1, "472 427", "572 475"); //Panel_ItemAmountBackground
                int inputAmount = buyRequest ? amount : inputItem.amount;
                if (!buyRequest)
                    UI_AddText(container, "Market_CreateOfferUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", $"x{inputItem.amount}", TextAnchor.MiddleCenter, 21, "472 435", "572 467"); //Text_Amount
                else
                    UI_AddInput(container, "Market_CreateOfferUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", TextAnchor.MiddleCenter, 21, amount.ToString(), 8, $"UI_ShoppyStock stock requestAmount", "472 435", "572 467"); //Input_SearchField
                UI_AddPanel(container, "Market_CreateOfferUI", config.colors.transparentColor1, "192 379", "572 427"); //Panel_SetPriceBackground
                UI_AddText(container, "Market_CreateOfferUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang("SetPricePerOne", player.UserIDString), TextAnchor.MiddleCenter, 21, "192 379", "472 427"); //Text_SetPriceTitle
                UI_AddPanel(container, "Market_CreateOfferUI", config.colors.transparentColor1, "472 379", "572 427"); //Panel_PriceBackground
                UI_AddInput(container, "Market_CreateOfferUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", TextAnchor.MiddleCenter, 21, price.ToString(), 8, $"UI_ShoppyStock stock requestPrice", "472 387", "572 419"); //Input_SearchField
                UI_AddPanel(container, "Market_CreateOfferUI", config.colors.transparentColor1, "192 325", "379 373"); //Panel_TotalIncomeBackground
                UI_AddText(container, "Market_CreateOfferUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("TotalIncome", player.UserIDString), TextAnchor.MiddleLeft, 21, "196 325", "309 373"); //Text_TotalIncome
                UI_AddPanel(container, "Market_CreateOfferUI", config.colors.transparentColor1, "309 325", "379 373"); //Panel_IncomeBackground
                float totalPrice = price * inputAmount;
                UI_AddText(container, "Market_CreateOfferUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", FormatPrice(shopName, totalPrice, true), TextAnchor.MiddleCenter, 13, "309 333", "379 365"); //Text_Income
                UI_AddPanel(container, "Market_CreateOfferUI", config.colors.transparentColor1, "385 325", "572 373"); //Panel_ListingTaxBackground
                UI_AddText(container, "Market_CreateOfferUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ListingTax", player.UserIDString), TextAnchor.MiddleLeft, 21, "389 325", "502 373"); //Text_LisingTax
                UI_AddPanel(container, "Market_CreateOfferUI", config.colors.transparentColor1, "502 325", "572 373"); //Panel_TaxAmountBackground
                float taxType = buyRequest ? data.stockMarkets[shopName].stockConfig.buyTax : data.stockMarkets[shopName].stockConfig.sellTax;
                UI_AddText(container, "Market_CreateOfferUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", FormatPrice(shopName, (totalPrice / 100f) * taxType, true), TextAnchor.MiddleCenter, 13, "502 333", "572 365"); //Text_TaxAmount
            }
            if (config.debug) Puts($"[DEBUG] Finishing checking input item.");
            UI_AddText(container, "Market_CreateOfferUI", config.colors.color8, "RobotoCondensed-Bold.ttf", Lang("InputItemHint", player.UserIDString), TextAnchor.MiddleLeft, 13, "201 269", "572 290"); //Text_InputItemHint
            openedUis.Remove(player);
            stockPosition.Remove(player);
            CuiHelper.DestroyUi(player, "Market_AnchorUI");
            CuiHelper.DestroyUi(player, "Market_CreateOfferUI");
            CuiHelper.AddUi(player, container);
        }

        private void OpenBankManagement(BasePlayer player, string shopName, int page = 1)
        {
            if (config.debug) Puts($"[DEBUG] Opening bank management.");
            if (!mailboxes.ContainsKey(player))
            {
                if (config.debug) Puts($"[DEBUG] Creating mailbox.");
                player.EndLooting();
                Mailbox mailbox = GameManager.server.CreateEntity("assets/prefabs/deployable/mailbox/mailbox.deployed.prefab", new Vector3(player.transform.position.x, player.transform.position.y - 40f, player.transform.position.z)) as Mailbox;
                mailbox.OwnerID = player.userID;
                mailbox.skinID = 2;
                UnityEngine.Object.DestroyImmediate(mailbox.GetComponent<GroundWatch>());
                UnityEngine.Object.DestroyImmediate(mailbox.GetComponent<DestroyOnGroundMissing>());
                mailbox.Spawn();
                mailboxes.Add(player, mailbox);
                mailbox.InputContainer.canAcceptItem = (iItem, i) => true;
                timer.Once(0.1f, () => {
                    player.inventory.loot.AddContainer(mailbox.InputContainer);
                    player.inventory.loot.entitySource = mailbox;
                    player.inventory.loot.PositionChecks = false;
                    player.inventory.loot.MarkDirty();
                    player.inventory.loot.SendImmediate();
                    player.ClientRPC<string>(RpcTarget.Player("RPC_OpenLootPanel", player), "mailboxentry");
                });
            }
            if (config.debug) Puts($"[DEBUG] Finished checking mailbox.");
            if (page < 1)
                page = 1;
            CuiElementContainer container = new CuiElementContainer();
            if (config.clickingSound)
                EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/notice/loot.copy.fx.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
            int startY = 324;
            int itemCount = 0;
            if (data.stockMarkets[shopName].playerData.playerBanks.ContainsKey(player.userID) && data.stockMarkets[shopName].playerData.playerBanks[player.userID].Count > 0)
                itemCount = data.stockMarkets[shopName].playerData.playerBanks[player.userID].Count;
            int maxPages = Mathf.CeilToInt(itemCount / 10f);
            if (page > maxPages)
                page = maxPages;
            bool pages = maxPages > 1;
            int startIndex = page * 10 - 10;
            UI_AddAnchor(container, "Market_MainUI", "Hud.Menu", "0.5 0", "0.5 0"); //Market_MainUI
            if (itemCount != 0)
            {
                if (config.debug) Puts($"[DEBUG] Checking item count.");
                int counter = -1;
                foreach (var item in data.stockMarkets[shopName].playerData.playerBanks[player.userID])
                {
                    counter++;
                    if (counter < startIndex) continue;
                    if (counter > startIndex + 10) break;
                    UI_AddPanel(container, "Market_MainUI", config.colors.transparentColor1, $"192 {startY}", $"572 {startY + 26}"); //Panel_BankItemBackground
                    UI_AddItemImage(container, "Market_MainUI", item.Value.shortname, item.Value.skin, $"196 {startY + 4}", $"214 {startY + 22}"); //Image_ItemImage
                    string name = config.translateItems ? Lang($"{item.Value.shortname}-{item.Value.skin}", player.UserIDString) : item.Value.displayName == null || item.Value.displayName == "" ? ItemManager.FindItemDefinition(item.Value.shortname).displayName.english : item.Value.displayName;
                    UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", name, TextAnchor.MiddleLeft, 10, $"220 {startY}", $"422 {startY + 26}"); //Text_ItemName
                    UI_AddInput(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", TextAnchor.MiddleCenter, 12, "0", 7, $"UI_ShoppyStock bankWithdraw {shopName} {item.Key}", $"422 {startY}", $"497 {startY + 26}"); //Input_WithdrawalAmount
                    UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", item.Value.amount.ToString(), TextAnchor.MiddleCenter, 12, $"497 {startY}", $"572 {startY + 26}"); //Text_ItemAmount
                    startY += 28;
                }
            }
            else
            {
                UI_AddPanel(container, "Market_MainUI", config.colors.transparentColor1, $"192 {startY}", $"572 {startY + 26}"); //Panel_BankItemBackground
                UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang("NoItemsFound", player.UserIDString), TextAnchor.MiddleCenter, 12, $"192 {startY}", $"572 {startY + 26}"); //Text_ItemName
                startY += 28;
            }
            if (config.debug) Puts($"[DEBUG] Finsiehd item count.");
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ItemBank", player.UserIDString), TextAnchor.MiddleLeft, 21, $"192 {startY + 21}", $"350 {startY + 55}"); //Text_ItemBankTitle
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("WithdrawalHint", player.UserIDString, config.storageName.ToUpper()), TextAnchor.LowerRight, 9, $"350 {startY + 23}", $"572 {startY + 55}"); //Text_ItemBankHint
            UI_AddPanel(container, "Market_MainUI", config.colors.transparentColor2, $"192 {startY}", $"572 {startY + 21}"); //Panel_InfoBackground
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ItemName", player.UserIDString), TextAnchor.MiddleLeft, 13, $"201 {startY}", $"422 {startY + 21}"); //Text_ItemName
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("WithdrawAmount", player.UserIDString), TextAnchor.MiddleCenter, 13, $"422 {startY}", $"497 {startY + 21}"); //Text_WithdrawalAmount
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ItemAmount", player.UserIDString), TextAnchor.MiddleCenter, 13, $"497 {startY}", $"572 {startY + 21}"); //Text_ItemAmount
            if (pages)
            {
                UI_AddButton(container, "Market_MainUI", config.colors.transparentColor2, $"UI_ShoppyStock bankPage {shopName} {page + 1}", "522 299", "572 320", "Market_MainUI_Button"); //Button_NextPage
                UI_AddImage(container, "Market_MainUI_Button", "UI_ShoppyStock_ArrowRight", 0, "9 -6", "41 26"); //Image_ArrowRight
                UI_AddButton(container, "Market_MainUI", config.colors.transparentColor2, $"UI_ShoppyStock bankPage {shopName} {page - 1}", "414 299", "464 320", "Market_MainUI_Button"); //Button_PrevPage
                UI_AddImage(container, "Market_MainUI_Button", "UI_ShoppyStock_ArrowLeft", 0, "10 -6", "42 26"); //Image_ArrowLeft
                UI_AddPanel(container, "Market_MainUI", config.colors.transparentColor1, "464 299", "522 320"); //Panel_PageNumberBackground
                UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", $"{page:00}/{maxPages:00}", TextAnchor.MiddleCenter, 12, "464 299", "522 320"); //Text_PageNumber
            }
            UI_AddButton(container, "Market_MainUI", config.colors.transparentColor2, $"UI_ShoppyStock bankDepositAll {shopName}", $"347 {startY}", $"422 {startY + 21}", "Market_MainUI_Button"); //Button_DepositAll
            UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("DepositAll", player.UserIDString), TextAnchor.MiddleCenter, 13, "0 0", "75 21"); //Text_DepositAll
            if (config.debug) Puts($"[DEBUG] Pages checked.");
            UI_AddText(container, "Market_MainUI", config.colors.color8, "RobotoCondensed-Bold.ttf", Lang("InputItemHint", player.UserIDString), TextAnchor.MiddleLeft, 13, "201 269", "572 290"); //Text_InputItemHint
            openedUis.Remove(player);
            stockPosition.Remove(player);
            CuiHelper.DestroyUi(player, "Market_AnchorUI");
            CuiHelper.DestroyUi(player, "Market_MainUI");
            CuiHelper.AddUi(player, container);
        }

        private class ItemClass
        {
            public string shortname;
            public ulong skin = 0;
            public float price = 0;
            public int bonusPrice = 0;
            public int amount = 0;
        }

        private void OpenSellUI(BasePlayer player, string shopName = "")
        {
            if (config.debug) Puts($"[DEBUG] Opening sell UI.");
            if (selling) return;
            BoxStorage box = null;
            if (!boxes.ContainsKey(player) || boxes[player] == null || boxes[player].IsDestroyed)
            {
                if (config.debug) Puts($"[DEBUG] Creating box.");
                player.EndLooting();
                box = GameManager.server.CreateEntity("assets/prefabs/deployable/large wood storage/box.wooden.large.prefab", new Vector3(player.transform.position.x, -400, player.transform.position.z)) as BoxStorage;
                box.OwnerID = player.userID;
                UnityEngine.Object.DestroyImmediate(box.GetComponent<GroundWatch>());
                UnityEngine.Object.DestroyImmediate(box.GetComponent<DestroyOnGroundMissing>());
                box.Spawn();
                box.inventory.capacity = 24;
                boxes.Add(player, box);
                box.inventory.onDirty += () => OpenSellUI(player, shopName);
                timer.Once(0.1f, () =>
                {
                    player.inventory.loot.AddContainer(box.inventory);
                    player.inventory.loot.entitySource = box;
                    player.inventory.loot.PositionChecks = false;
                    player.inventory.loot.MarkDirty();
                    player.inventory.loot.SendImmediate();
                    player.ClientRPC<string>(RpcTarget.Player("RPC_OpenLootPanel", player), "generic_resizable");
                });
            }
            else
                box = boxes[player];
            if (config.debug) Puts($"[DEBUG] Finsiehd checking box.");
            Dictionary<string, ItemClass> legitItems = new Dictionary<string, ItemClass>();
            int totalEarning = 0;
            float bonus = 0;
            if (BonusCore != null)
                bonus = BonusCore.Call<float>("GetBonus", player, "Other_Price") * 100f;
            if (config.shops[shopName].stockConfig.sellPriceMultipliers.Count > 0)
            {
                foreach (var perm in config.shops[shopName].stockConfig.sellPriceMultipliers)
                {
                    if (permission.UserHasPermission(player.UserIDString, perm.Key))
                    {
                        bonus = perm.Value * 100f;
                        break;
                    }
                }
            }
            float bonusTotalEarning = 0;
            if (config.debug) Puts($"[DEBUG] Looping items.");
            foreach (var item in boxes[player].inventory.itemList)
            {
                if (!data.stockMarkets[shopName].stockConfig.serverSell.ContainsKey(item.info.shortname) || !data.stockMarkets[shopName].stockConfig.serverSell[item.info.shortname].ContainsKey(item.skin)) continue;
                if (!data.stockMarkets[shopName].sellCache.ContainsKey(item.info.shortname) || !data.stockMarkets[shopName].sellCache[item.info.shortname].ContainsKey(item.skin))
                    RollPrices(shopName, item.info.shortname, item.skin);
                string key = $"{item.info.shortname}-{item.skin}";
                legitItems.TryAdd(key, new ItemClass() { shortname = item.info.shortname, skin = item.skin });
                legitItems[key].amount += item.amount;
            }
            foreach (var item in legitItems)
            {
                float notRoundedPrice = item.Value.amount * data.stockMarkets[shopName].sellCache[item.Value.shortname][item.Value.skin].price;
                int remainingItems = Mathf.FloorToInt(notRoundedPrice % 1 / data.stockMarkets[shopName].sellCache[item.Value.shortname][item.Value.skin].price);
                item.Value.price = Mathf.FloorToInt((item.Value.amount - remainingItems) * data.stockMarkets[shopName].sellCache[item.Value.shortname][item.Value.skin].price);
                totalEarning += (int)item.Value.price;
                if (!data.stockMarkets[shopName].stockConfig.blockedMultipliers.Contains(item.Key))
                {
                    item.Value.bonusPrice = Mathf.FloorToInt(item.Value.price / 100f * bonus);
                    bonusTotalEarning += item.Value.bonusPrice;
                }
            }
            if (config.debug) Puts($"[DEBUG] Finished looping items.");
            int startY = 414;
            int listedItemsCount = legitItems.Count;
            bool moreThan7 = false;
            int listedItems = 0;
            if (listedItemsCount > 7)
            {
                listedItems = 7;
                moreThan7 = true;
            }
            else
                listedItems = listedItemsCount;
            startY += 26 * listedItems;
            CuiElementContainer container = new CuiElementContainer();
            if (config.clickingSound)
                EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/notice/loot.copy.fx.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
            UI_AddAnchor(container, "Market_MainUI", "Hud.Menu", "0.5 0", "0.5 0"); //Market_MainUI
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ItemSellTitle", player.UserIDString), TextAnchor.MiddleLeft, 21, $"192 {startY + 21}", $"392 {startY + 55}"); //Text_ItemSellTitle
            UI_AddPanel(container, "Market_MainUI", config.colors.transparentColor2, $"192 {startY}", $"572 {startY + 21}"); //Panel_InfoBackground
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ItemName", player.UserIDString), TextAnchor.MiddleLeft, 13, $"201 {startY}", $"382 {startY + 21}"); //Text_ItemName
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ItemAmountShort", player.UserIDString), TextAnchor.MiddleCenter, 13, $"392 {startY}", $"457 {startY + 21}"); //Text_ItemAmount
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ItemPrice", player.UserIDString), TextAnchor.MiddleCenter, 13, $"457 {startY}", $"572 {startY + 21}"); //Text_ItemPrice
            List<KeyValuePair<string, ItemClass>> sortedItems = legitItems.OrderByDescending(x => x.Value.price).ToList();
            int counter = -1;
            if (config.debug) Puts($"[DEBUG] Displaying sorted items.");
            foreach (var item in sortedItems)
            {
                counter++;
                startY -= 26;
                if (counter > 6) break;
                if (counter == 6 && moreThan7)
                {
                    UI_AddPanel(container, "Market_MainUI", config.colors.transparentColor1, $"192 {startY}", $"572 {startY + 26}"); //Panel_MoreItemsBackground
                    UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang("AndMoreItems", player.UserIDString, listedItemsCount - 6), TextAnchor.MiddleCenter, 12, $"192 {startY}", $"572 {startY + 26}"); //Text_MoreItems
                    break;
                }
                ServerSellData sellData = data.stockMarkets[shopName].stockConfig.serverSell[item.Value.shortname][item.Value.skin];
                UI_AddPanel(container, "Market_MainUI", config.colors.transparentColor1, $"192 {startY}", $"572 {startY + 26}"); //Panel_BankItemBackground
                UI_AddItemImage(container, "Market_MainUI", item.Value.shortname, item.Value.skin, $"196 {startY + 4}", $"214 {startY + 22}"); //Image_ItemImage
                string itemName = config.translateItems ? Lang($"{item.Value.shortname}-{item.Value.skin}", player.UserIDString) : sellData.displayName;
                UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", itemName, TextAnchor.MiddleLeft, 10, $"220 {startY}", $"392 {startY + 26}"); //Text_ItemName
                UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", item.Value.amount.ToString(), TextAnchor.MiddleCenter, 12, $"392 {startY}", $"457 {startY + 26}"); //Text_ItemAmount
                string priceFormat = item.Value.bonusPrice == 0 ? FormatPrice(shopName, item.Value.price) : $"{FormatPrice(shopName, item.Value.price)}  <color=#bbd988>[+{FormatPrice(shopName, item.Value.bonusPrice)}]</color>";
                UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", priceFormat, TextAnchor.MiddleCenter, 12, $"457 {startY}", $"572 {startY + 26}"); //Text_ItemPrice
            }
            if (config.debug) Puts($"[DEBUG] Finished displaying sorted items.");
            UI_AddPanel(container, "Market_MainUI", config.colors.transparentColor2, "392 391", "457 412"); //Panel_TotalPriceBackground
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("TotalPrice", player.UserIDString), TextAnchor.MiddleCenter, 12, "392 391", "457 412"); //Text_TotalPrice
            UI_AddPanel(container, "Market_MainUI", config.colors.transparentColor1, "457 391", "572 412"); //Panel_TotalPricePriceBackground
            string totalPriceFormat = bonusTotalEarning == 0 ? FormatPrice(shopName, totalEarning) : $"{FormatPrice(shopName, totalEarning)}  <color=#bbd988>[+{FormatPrice(shopName, bonusTotalEarning)}]</color>";
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", totalPriceFormat, TextAnchor.MiddleCenter, 12, "457 391", "572 412"); //Text_TotalPricePrice
            UI_AddPanel(container, "Market_MainUI", config.colors.transparentColor2, "198 72", "430 98"); //Panel_SellBackground
            UI_AddIcon(container, "Market_MainUI", config.colors.textColor, "202 76", "220 94", "assets/icons/info.png"); //Icon_Info
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ClickToSell", player.UserIDString), TextAnchor.LowerLeft, 14, "226 80", "358 98"); //Text_ClickToSell
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("IrreversibleAction", player.UserIDString), TextAnchor.UpperLeft, 8, "227 72", "358 84"); //Text_IrreversibleAction
            UI_AddButton(container, "Market_MainUI", config.colors.positiveDark, $"UI_ShoppyStock stock sellItems {shopName}", "358 72", "430 98", "Market_MainUI_Button"); //Button_Sell
            UI_AddText(container, "Market_MainUI_Button", config.colors.positiveLight, "RobotoCondensed-Bold.ttf", Lang("SellButton", player.UserIDString), TextAnchor.MiddleCenter, 14, "0 0", "72 26"); //Text_Sell
            openedUis.Remove(player);
            stockPosition.Remove(player);
            if (config.debug) Puts($"[DEBUG] Finishing displaying UI.");
            CuiHelper.DestroyUi(player, "Market_AnchorUI");
            CuiHelper.DestroyUi(player, "Market_MainUI");
            CuiHelper.AddUi(player, container);
        }

        private void OpenTransferUI(BasePlayer player, int page = 1, string search = "")
        {
            if (config.debug) Puts($"[DEBUG] Opening transfer UI.");
            bool canTransfer = false;
            foreach (var shop in config.shops)
            {
                if (shop.Value.canTransfer)
                {
                    canTransfer = true;
                    break;
                }
            }
            if (!canTransfer)
            {
                PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("NoTransferAvailable", player.UserIDString), config.popUpFontSize, config.popUpLength);
                EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                return;
            }
            if (page < 1)
                page = 1;
            CuiElementContainer container = new CuiElementContainer();
            if (config.clickingSound)
                EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/notice/loot.copy.fx.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
            if (!openedUis.Contains(player))
            {
                UI_AddAnchor(container, "Market_AnchorUI", "Hud.Menu", "0 0", "1 1"); //Market_MainUI
                UI_AddBlurPanel(container, "Market_AnchorUI", "0.3 0.3 0.3 0.2"); //Panel_BackgroundBlur
                UI_AddPanel(container, "Market_AnchorUI", "0.4 0.4 0.4 0.3", "0 0", "0 0", "0 0", "1 1"); //Panel_BackgroundColor
                UI_AddVignettePanel(container, "Market_AnchorUI", "0 0 0 0.8"); //Panel_Vignette
                openedUis.Add(player);
                EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/notice/item.select.fx.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
            }
            if (config.debug) Puts($"[DEBUG] After basic checks.");
            UI_AddAnchor(container, "Market_MainUI", "Market_AnchorUI", "0.5 0.5", "0.5 0.5"); //Market_MainUI
            UI_AddPanel(container, "Market_MainUI", config.colors.color1, "-529 -287", "529 287"); //Panel_Background
            UI_AddPanel(container, "Market_MainUI", config.colors.color2, "-525 235", "525 283"); //Panel_TopPanel
            int startX = -525;
            if (config.shopButton)
            {
                UI_AddButton(container, "Market_MainUI", config.colors.color2, "UI_ShoppyStock shops", $"{startX} 235", $"{startX + 200} 283", "Market_MainUI_Button"); //Button_Shops
                UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ShopsButton", player.UserIDString), TextAnchor.MiddleCenter, 20, "0 0", "200 48"); //Text_Shops
                startX += 200;
            }
            if (config.stockButton)
            {
                UI_AddButton(container, "Market_MainUI", config.colors.color2, "UI_ShoppyStock stock", $"{startX} 235", $"{startX + 200} 283", "Market_MainUI_Button"); //Button_StockMarket
                UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("StockButton", player.UserIDString), TextAnchor.MiddleCenter, 20, "0 0", "200 48"); //Text_Stock
                startX += 200;
            }
            if (config.transferButton)
            {
                UI_AddButton(container, "Market_MainUI", config.colors.color2, "UI_ShoppyStock transfer", $"{startX} 235", $"{startX + 200} 283", "Market_MainUI_Button"); //Button_Transfer
                UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("TransferButton", player.UserIDString), TextAnchor.MiddleCenter, 20, "0 0", "200 48"); //Text_Transfer
                startX += 200;
            }
            UI_AddButton(container, "Market_MainUI", config.colors.negativeDark, $"UI_ShoppyStock close", "477 235", "525 283", "Market_MainUI_Button"); //Button_Close
            UI_AddText(container, "Market_MainUI_Button", config.colors.negativeLight, "RobotoCondensed-Regular.ttf", "✖", TextAnchor.MiddleCenter, 30, "0 0", "48 48"); //Text_Close
            UI_AddPanel(container, "Market_MainUI", config.colors.color3, "-525 193", "525 231"); //Panel_HintPanel
            UI_AddIcon(container, "Market_MainUI", config.colors.textColor, "-521 195", "-487 229", "assets/icons/info.png"); //Image_CurrencyIcon
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("SelectTransferPlayerHint", player.UserIDString), TextAnchor.MiddleLeft, 20, "-483 193", "525 231"); //Text_ShopHint
            UI_AddPanel(container, "Market_MainUI", config.colors.color2, "-150 163", "150 189"); //Panel_SearchBackground
            UI_AddImage(container, "Market_MainUI", "UI_ShoppyStock_Search", 0, "-146 167", "-128 185", config.colors.textColor); //Image_Search
            UI_AddInput(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", TextAnchor.MiddleLeft, 14, search, 48, $"UI_ShoppyStock transfer search", "-122 163", "150 189"); //Input_Search
            UI_AddButton(container, "Market_MainUI", config.colors.color2, $"UI_ShoppyStock transfer online {page}", "490 165", "512 187", "Market_MainUI_Button"); //Button_BuySellOrders
            shopCache.TryAdd(player, new ShopCache());
            string enabled = shopCache[player].transferOnline ? "✖" : "";
            UI_AddText(container, "Market_MainUI_Button", config.colors.color7, "RobotoCondensed-Bold.ttf", enabled, TextAnchor.MiddleCenter, 14, "0 0", "22 22"); //Text_BuySellOrders
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang("OnlineOnly", player.UserIDString), TextAnchor.MiddleRight, 13, "200 165", "486 187");
            int counter = -1;
            if (config.debug) Puts($"[DEBUG] Checking user list.");
            Dictionary<ulong, UserData> userList = new Dictionary<ulong, UserData>();
            string formatSearch = search.ToLower();
            foreach (var shop in config.shops)
                if (shop.Value.canTransfer)
                    foreach (var user in data.shops[shop.Key].users)
                    {
                        if (user.Key == player.userID) continue;
                        if (search == "" || user.Value.username.ToLower().Contains(formatSearch))
                            userList.TryAdd(user.Key, user.Value);
                    }
            int userCount = userList.Count;
            List<BasePlayer> playerList = null;
            if (shopCache[player].transferOnline)
            {
                userCount = BasePlayer.activePlayerList.Count - 1;
                playerList = BasePlayer.activePlayerList.Where(x => x.userID != player.userID).ToList();
            }
            if (config.debug) Puts($"[DEBUG] After online check.");
            int maxPage = Mathf.CeilToInt(userCount / 30f);
            if (page > maxPage)
                page = maxPage;
            List<int> length = new List<int>() { 347, 348, 347 };
            int i = 0;
            startX = -525;
            int startY = 121;
            if (config.debug) Puts($"[DEBUG] Starting looping users.");
            while (i < 30)
            {
                counter++;
                if (counter < page * 30 - 30) continue;
                int splitter = i % 3;
                int buttonLength = length[splitter];
                if (counter + 1 > userCount)
                    UI_AddPanel(container, "Market_MainUI", config.colors.color2, $"{startX} {startY}", $"{startX + buttonLength} {startY + 38}"); //Panel_EmptyUser
                else
                {
                    if (!shopCache[player].transferOnline)
                    {
                        KeyValuePair<ulong, UserData> userData = userList.ElementAt(counter);
                        if (permission.UserHasPermission(userData.Key.ToString(), "shoppystock.hidetransfer"))
                            continue;
                        UI_AddButton(container, "Market_MainUI", config.colors.color4, $"UI_ShoppyStock transfer user {userData.Key}", $"{startX} {startY}", $"{startX + buttonLength} {startY + 38}", "Market_MainUI_Button"); //Button_TransferUser
                        UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Regular.ttf", userData.Value.username, TextAnchor.MiddleCenter, 20, "0 0", $"{buttonLength} 38"); //Text_TransferUser
                    }
                    else
                    {
                        BasePlayer displayPlayer = playerList.ElementAt(counter);
                        if (permission.UserHasPermission(displayPlayer.UserIDString, "shoppystock.hidetransfer"))
                            continue;
                        UI_AddButton(container, "Market_MainUI", config.colors.color4, $"UI_ShoppyStock transfer user {displayPlayer.userID}", $"{startX} {startY}", $"{startX + buttonLength} {startY + 38}", "Market_MainUI_Button"); //Button_TransferUser
                        UI_AddText(container, "Market_MainUI_Button", config.colors.textColor, "RobotoCondensed-Regular.ttf", displayPlayer.displayName, TextAnchor.MiddleCenter, 20, "0 0", $"{buttonLength} 38"); //Text_TransferUser
                    }
                }
                i++;
                splitter = i % 3;
                if (splitter == 0)
                {
                    startY -= 42;
                    startX = -525;
                }
                else
                    startX += buttonLength + 4;
            }
            if (config.debug) Puts($"[DEBUG] Finishing looping users.");
            UI_AddPanel(container, "Market_MainUI", config.colors.color2, "-35 -283", "36 -261"); //Panel_PageBackground
            UI_AddText(container, "Market_MainUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", $"{page:00}/{maxPage:00}", TextAnchor.MiddleCenter, 14, "-35 -283", "36 -261"); //Text_Page
            UI_AddButton(container, "Market_MainUI", config.colors.color4, $"UI_ShoppyStock transfer page {page - 1} {search}", "-88 -283", "-35 -261", "Market_MainUI_Button"); //Button_PrevPage
            UI_AddImage(container, "Market_MainUI_Button", "UI_ShoppyStock_ArrowLeft", 0, "10 -5", "42 27"); //Image_ArrowLeft
            UI_AddButton(container, "Market_MainUI", config.colors.color4, $"UI_ShoppyStock transfer page {page + 1} {search} ", "36 -283", "89 -261", "Market_MainUI_Button"); //Button_NextPage
            UI_AddImage(container, "Market_MainUI_Button", "UI_ShoppyStock_ArrowRight", 0, "10 -5", "42 27"); //Image_ArrowRight
            CuiHelper.DestroyUi(player, "Market_MainUI");
            CuiHelper.AddUi(player, container);
        }

        private void OpenUserTransferUI(BasePlayer player, string userId, string currency = "", int amount = 0)
        {
            CuiElementContainer container = new CuiElementContainer();
            if (config.clickingSound)
                EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/notice/loot.copy.fx.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
            if (amount < 0)
                amount = 0;
            if (currency == "")
                foreach (var shop in config.shops)
                    if (shop.Value.canTransfer)
                    {
                        currency = shop.Key;
                        break;
                    }
            if (config.debug) Puts($"[DEBUG] Opeing user transfer UI.");
            UI_AddAnchor(container, "Market_TransferUI", "Market_AnchorUI", "0 0", "1 1"); //Market_TransferUI
            UI_AddBlurPanel(container, "Market_TransferUI", "0.3 0.3 0.3 0.2"); //Panel_BackgroundBlur
            UI_AddPanel(container, "Market_TransferUI", "0.4 0.4 0.4 0.3", "0 0", "0 0", "0 0", "1 1"); //Panel_BackgroundColor
            UI_AddVignettePanel(container, "Market_TransferUI", "0 0 0 0.8"); //Panel_Vignette
            UI_AddAnchor(container, "Market_TransferPanelUI", "Market_TransferUI", "0.5 0.5", "0.5 0.5"); //Market_TransferUI
            UI_AddPanel(container, "Market_TransferPanelUI", config.colors.color1, "-238 -161", "238 161"); //Panel_Background
            UI_AddPanel(container, "Market_TransferPanelUI", config.colors.color2, "-234 109", "234 157"); //Panel_TopPanel
            UI_AddIcon(container, "Market_TransferPanelUI", config.colors.textColor, "-228 115", "-192 151", "assets/icons/portion.png"); //Icon_TopPanel
            UI_AddText(container, "Market_TransferPanelUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("TransferUserTitle", player.UserIDString), TextAnchor.MiddleLeft, 26, "-186 109", "186 157"); //Text_Title
            string commande = config.closeOnlySubGui ? "UI_ShoppyStock closeTransferInfo" : "UI_ShoppyStock close";
            UI_AddButton(container, "Market_TransferPanelUI", config.colors.negativeDark, commande, "186 109", "234 157", "Market_TransferPanelUI_Button"); //Button_Close
            UI_AddText(container, "Market_TransferPanelUI_Button", config.colors.negativeLight, "RobotoCondensed-Regular.ttf", "✖", TextAnchor.MiddleCenter, 30, "0 0", "48 48"); //Text_Close
            UI_AddPanel(container, "Market_TransferPanelUI", config.colors.color3, "-234 77", "234 105"); //Panel_InfoBackground
            UI_AddText(container, "Market_TransferPanelUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("Currency", player.UserIDString), TextAnchor.MiddleLeft, 16, "-230 77", "-135 105"); //Text_CurrencyTitle
            UI_AddText(container, "Market_TransferPanelUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("Username", player.UserIDString), TextAnchor.MiddleLeft, 16, "-131 77", "234 105"); //Text_UsernameTitle
            UI_AddButton(container, "Market_TransferPanelUI", config.colors.color2, $"UI_ShoppyStock transfer currency {userId} {currency}", "-234 25", "-139 73", "Market_TransferPanelUI_Button"); //Button_CurrencyBackground
            UI_AddImage(container, "Market_TransferPanelUI_Button", $"UI_ShoppyStock_{currency}_Icon", 0, "8 7", "42 41", config.colors.textColor); //Image_Currency
            string currFormat = string.Format(config.shops[currency].symbol, "");
            int fontSize = currFormat.Length > 6 ? 12 : 17;
            UI_AddText(container, "Market_TransferPanelUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", currFormat, TextAnchor.MiddleCenter, fontSize, "50 0", "95 48"); //Text_CurrencyName
            UI_AddText(container, "Market_TransferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang("ClickToChange", player.UserIDString), TextAnchor.UpperCenter, 8, "-234 9", "-139 24"); //Text_ClickToChange
            UI_AddPanel(container, "Market_TransferPanelUI", config.colors.color2, "-135 25", "234 73"); //Panel_UsernameBackground
            if (config.debug) Puts($"[DEBUG] Checking player.");
            IPlayer iPlayer = covalence.Players.FindPlayerById(userId);
            if (iPlayer == null)
            {
                PopUpAPI?.Call("ShowPopUp", player, "Market", Lang("PlayerCovalenceError", player.UserIDString), config.popUpFontSize, config.popUpLength);
                EffectNetwork.Send(new Effect("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
                return;
            }
            UI_AddText(container, "Market_TransferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", iPlayer.Name, TextAnchor.MiddleLeft, 17, "-131 25", "234 73"); //Text_Username
            UI_AddText(container, "Market_TransferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang("TransferAmount", player.UserIDString), TextAnchor.MiddleCenter, 20, "-92 -15", "92 13"); //Text_TransferAmount
            UI_AddPanel(container, "Market_TransferPanelUI", config.colors.color4, "-75 -57", "75 -15"); //Panel_TransferAmountBackground
            int currencyAmount = GetCurrencyAmount(currency, player);
            if (amount > currencyAmount)
                amount = currencyAmount;
            if (config.debug) Puts($"[DEBUG] After amount check.");
            UI_AddInput(container, "Market_TransferPanelUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", TextAnchor.MiddleCenter, 23, amount.ToString(), 8, $"UI_ShoppyStock transfer setAmount {userId} {currency}", "-75 -57", "75 -15"); //Input_Amount
            UI_AddButton(container, "Market_TransferPanelUI", config.colors.positiveDark, $"UI_ShoppyStock transfer increase {userId} {currency} {amount}", "75 -57", "117 -15", "Market_TransferPanelUI_Button"); //Button_Increase
            UI_AddText(container, "Market_TransferPanelUI_Button", config.colors.positiveLight, "RobotoCondensed-Bold.ttf", "+", TextAnchor.MiddleCenter, 35, "0 0", "42 42"); //Text_Increase
            UI_AddButton(container, "Market_TransferPanelUI", config.colors.negativeDark, $"UI_ShoppyStock transfer decrease {userId} {currency} {amount}", "-117 -57", "-75 -15", "Market_TransferPanelUI_Button"); //Button_Decrease
            UI_AddText(container, "Market_TransferPanelUI_Button", config.colors.negativeLight, "RobotoCondensed-Bold.ttf", "-", TextAnchor.MiddleCenter, 35, "0 0", "42 42"); //Text_Decrease
            UI_AddPanel(container, "Market_TransferPanelUI", config.colors.color2, "-192 -125", "192 -77"); //Panel_BalanceAfterBackground
            UI_AddText(container, "Market_TransferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang("BalanceAfterTransfer", player.UserIDString), TextAnchor.MiddleLeft, 17, "-186 -125", "97 -77"); //Text_BalanceAfterTitle
            UI_AddPanel(container, "Market_TransferPanelUI", config.colors.color3, "97 -125", "192 -77"); //Panel_BalanceAfterBalanceBackground
            UI_AddText(container, "Market_TransferPanelUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", FormatPrice(currency, currencyAmount - amount), TextAnchor.MiddleCenter, 17, "97 -125", "192 -77"); //Text_BalanceAfter
            UI_AddButton(container, "Market_TransferPanelUI", config.colors.color3, $"UI_ShoppyStock transfer cancel", "-192 -157", "-2 -129", "Market_TransferPanelUI_Button"); //Button_CancelTransfer
            UI_AddText(container, "Market_TransferPanelUI_Button", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("CancelButton", player.UserIDString), TextAnchor.MiddleCenter, 16, "0 0", "190 28"); //Text_Cancel
            if (amount > 0)
            {
                UI_AddButton(container, "Market_TransferPanelUI", config.colors.positiveDark, $"UI_ShoppyStock transfer accept {userId} {currency} {amount}", "2 -157", "192 -129", "Market_TransferPanelUI_Button"); //Button_AcceptTransfer
                UI_AddText(container, "Market_TransferPanelUI_Button", config.colors.positiveLight, "RobotoCondensed-Bold.ttf", Lang("TransferButton", player.UserIDString), TextAnchor.MiddleCenter, 16, "0 0", "190 28"); //Text_Accept
            }
            else
            {
                UI_AddButton(container, "Market_TransferPanelUI", config.colors.negativeDark, "", "2 -157", "192 -129", "Market_TransferPanelUI_Button"); //Button_AcceptTransfer
                UI_AddText(container, "Market_TransferPanelUI_Button", config.colors.negativeLight, "RobotoCondensed-Bold.ttf", Lang("InputAmount", player.UserIDString), TextAnchor.MiddleCenter, 16, "0 0", "190 28"); //Text_Accept
            }
            if (config.debug) Puts($"[DEBUG] Finishing transfer ui.");
            CuiHelper.DestroyUi(player, "Market_TransferUI");
            CuiHelper.AddUi(player, container);
        }

        private void OpenDepositMoneyUI(BasePlayer player, string shopName)
        {
            Mailbox mailbox = null;
            if (config.debug) Puts($"[DEBUG] Opening moeny deposit.");
            if (!mailboxes.ContainsKey(player))
            {
                if (config.debug) Puts($"[DEBUG] Creating mailbox.");
                player.EndLooting();
                mailbox = GameManager.server.CreateEntity("assets/prefabs/deployable/mailbox/mailbox.deployed.prefab", new Vector3(player.transform.position.x, player.transform.position.y - 40f, player.transform.position.z)) as Mailbox;
                mailbox.OwnerID = player.userID;
                mailbox.skinID = 3;
                UnityEngine.Object.DestroyImmediate(mailbox.GetComponent<GroundWatch>());
                UnityEngine.Object.DestroyImmediate(mailbox.GetComponent<DestroyOnGroundMissing>());
                mailbox.Spawn();
                mailboxes.Add(player, mailbox);
                mailbox.InputContainer.canAcceptItem = (iItem, i) => {
                    if (iItem.skin != config.shops[shopName].depositItem.skin || iItem.info.shortname != config.shops[shopName].depositItem.shortname) return false;
                    return true;
                };
                mailbox.InputContainer.onDirty += () => OpenDepositMoneyUI(player, shopName);
                timer.Once(0.1f, () => {
                    player.inventory.loot.AddContainer(mailbox.InputContainer);
                    player.inventory.loot.entitySource = mailbox;
                    player.inventory.loot.PositionChecks = false;
                    player.inventory.loot.MarkDirty();
                    player.inventory.loot.SendImmediate();
                    player.ClientRPC<string>(RpcTarget.Player("RPC_OpenLootPanel", player), "mailboxentry");
                });
            }
            if (config.debug) Puts($"[DEBUG] After checking mailbox.");
            CuiElementContainer container = new CuiElementContainer();
            if (config.clickingSound)
                EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/notice/loot.copy.fx.prefab", player, 0, Vector3.zero, Vector3.up), player.net.connection);
            int startHeight = config.shops[shopName].depositItem.allowWithdraw ? 420 : 370;
            UI_AddAnchor(container, "Market_CurrencyInputUI", "Hud.Menu", "0.5 0", "0.5 0"); //Market_CurrencyInputUI
            if (config.shops[shopName].canDeposit)
            {
                UI_AddText(container, "Market_CurrencyInputUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("CurrencyItemTitle", player.UserIDString), TextAnchor.MiddleLeft, 21, $"192 {startHeight}", $"392 {startHeight + 35}"); //Text_CurrencyItemTitle
                UI_AddText(container, "Market_CurrencyInputUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("ItemValue", player.UserIDString), TextAnchor.MiddleCenter, 16, $"472 {startHeight}", $"572 {startHeight + 35}"); //Text_ItemValueTitle
                UI_AddPanel(container, "Market_CurrencyInputUI", config.colors.transparentColor1, $"192 {startHeight - 48}", $"572 {startHeight}"); //Panel_ItemBackground
                UI_AddItemImage(container, "Market_CurrencyInputUI", config.shops[shopName].depositItem.shortname, config.shops[shopName].depositItem.skin, $"196 {startHeight - 44}", $"236 {startHeight - 4}"); //Image_ItemImage
                UI_AddText(container, "Market_CurrencyInputUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", config.shops[shopName].depositItem.displayName, TextAnchor.MiddleLeft, 22, $"240 {startHeight - 48}", $"472 {startHeight}"); //Text_ItemName
                UI_AddPanel(container, "Market_CurrencyInputUI", config.colors.transparentColor1, $"472 {startHeight - 48}", $"572 {startHeight}"); //Panel_ItemAmountBackground
                UI_AddText(container, "Market_CurrencyInputUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", FormatPrice(shopName, config.shops[shopName].depositItem.value), TextAnchor.MiddleCenter, 21, $"472 {startHeight - 40}", $"572 {startHeight - 8}"); //Text_Amount
            }
            if (config.shops[shopName].depositItem.allowWithdraw)
            {
                UI_AddPanel(container, "Market_CurrencyInputUI", config.colors.transparentColor1, "192 320", "572 368"); //Panel_ItemBackground
                UI_AddText(container, "Market_CurrencyInputUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", Lang("CurrencyWithdrawAmount", player.UserIDString), TextAnchor.MiddleLeft, 22, "240 320", "472 368"); //Text_ItemName
                UI_AddPanel(container, "Market_CurrencyInputUI", config.colors.transparentColor1, "472 320", "572 368"); //Panel_ItemAmountBackground
                UI_AddInput(container, "Market_CurrencyInputUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", TextAnchor.MiddleCenter, 21, "", 9, $"UI_ShoppyStock withdrawCurrency {shopName}", "472 328", "572 360"); //Text_Amount
                UI_AddIcon(container, "Market_CurrencyInputUI", config.colors.textColor, "196 324", "236 364", "assets/icons/player_loot.png"); //Icon_Withdraw
            }
            UI_AddText(container, "Market_CurrencyInputUI", config.colors.color8, "RobotoCondensed-Bold.ttf", Lang("InputCurrencyItemHint", player.UserIDString), TextAnchor.MiddleLeft, 13, "201 269", "572 290"); //Text_CurrencyInputItemHint
            openedUis.Remove(player);
            stockPosition.Remove(player);
            if (config.debug) Puts($"[DEBUG] Finishing opening.");
            CuiHelper.DestroyUi(player, "Market_AnchorUI");
            CuiHelper.DestroyUi(player, "Market_CurrencyInputUI");
            CuiHelper.AddUi(player, container);
        }

        private void OpenCreateItemEntryUI(BasePlayer player, string shopName, string category, bool stockMarket = false)
        {
            if (config.debug) Puts($"[DEBUG] Opening bank management.");
            if (!mailboxes.ContainsKey(player))
            {
                if (config.debug) Puts($"[DEBUG] Creating mailbox.");
                player.EndLooting();
                Mailbox mailbox = GameManager.server.CreateEntity("assets/prefabs/deployable/mailbox/mailbox.deployed.prefab", new Vector3(player.transform.position.x, player.transform.position.y - 40f, player.transform.position.z)) as Mailbox;
                mailbox.OwnerID = player.userID;
                mailbox.skinID = 4;
                UnityEngine.Object.DestroyImmediate(mailbox.GetComponent<GroundWatch>());
                UnityEngine.Object.DestroyImmediate(mailbox.GetComponent<DestroyOnGroundMissing>());
                mailbox.Spawn();
                mailboxes.Add(player, mailbox);
                mailbox.InputContainer.onDirty += () => OpenCreateItemEntryUI(player, shopName, category, stockMarket);
                mailbox.InputContainer.canAcceptItem = (iItem, i) => true;
                timer.Once(0.1f, () => {
                    player.inventory.loot.AddContainer(mailbox.InputContainer);
                    player.inventory.loot.entitySource = mailbox;
                    player.inventory.loot.PositionChecks = false;
                    player.inventory.loot.MarkDirty();
                    player.inventory.loot.SendImmediate();
                    player.ClientRPC<string>(RpcTarget.Player("RPC_OpenLootPanel", player), "mailboxentry");
                });
                addingItemsCache.TryAdd(player, new AddItemData());
                addingItemsCache[player] = new AddItemData() { shopName = shopName, category = category, stockMarket = stockMarket };
            }
            if (config.debug) Puts($"[DEBUG] Finished checking mailbox.");
            CuiElementContainer container = new CuiElementContainer();
            UI_AddAnchor(container, "Market_CreateOfferUI", "Hud.Menu", "0.5 0", "0.5 0"); //Market_CreateOfferUI
            Item mailboxItem = mailboxes[player].inventory.GetSlot(0);
            if (mailboxItem != null)
            {
                UI_AddText(container, "Market_CreateOfferUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("NewItemEntry", player.UserIDString), TextAnchor.MiddleLeft, 21, "192 517", "392 552"); //Text_NewItemEntry
                UI_AddText(container, "Market_CreateOfferUI", config.colors.textColor, "RobotoCondensed-Bold.ttf", Lang("MoreInConfig", player.UserIDString, shopName, category, stockMarket), TextAnchor.LowerRight, 9, "372 517", "572 572"); //Text_MoreInConfig
                UI_AddPanel(container, "Market_CreateOfferUI", config.colors.transparentColor2, "192 469", "572 517"); //Panel_ItemBackground
                UI_AddItemImage(container, "Market_CreateOfferUI", mailboxItem.info.shortname, mailboxItem.skin, "196 473", "236 513"); //Image_ItemImage
                string name = config.translateItems ? Lang($"{mailboxItem.info.shortname}-{mailboxItem.skin}", player.UserIDString) : mailboxItem.name != "" ? mailboxItem.name : mailboxItem.info.displayName.english;
                UI_AddText(container, "Market_CreateOfferUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", name, TextAnchor.MiddleLeft, 12, "240 469", "472 517"); //Text_ItemName
                UI_AddPanel(container, "Market_CreateOfferUI", config.colors.transparentColor1, "472 469", "572 517"); //Panel_ItemAmountBackground
                UI_AddText(container, "Market_CreateOfferUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", $"x{mailboxItem.amount}", TextAnchor.MiddleCenter, 21, "472 469", "572 517"); //Text_Amount
                UI_AddPanel(container, "Market_CreateOfferUI", config.colors.transparentColor1, "192 421", "572 469"); //Panel_SetPriceBackground
                string text1 = stockMarket ? Lang("SetMinPrice", player.UserIDString) : Lang("SetPricePerOne", player.UserIDString);
                UI_AddText(container, "Market_CreateOfferUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", text1, TextAnchor.MiddleCenter, 21, "192 421", "472 469"); //Text_SetPriceTitle
                UI_AddPanel(container, "Market_CreateOfferUI", config.colors.transparentColor1, "472 421", "572 469"); //Panel_PriceBackground
                UI_AddInput(container, "Market_CreateOfferUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", TextAnchor.MiddleCenter, 21, addingItemsCache[player].var1.ToString(), 24, $"UI_ShoppyStock addItem var1", "472 421", "572 469"); //Input_Var1
                UI_AddPanel(container, "Market_CreateOfferUI", config.colors.transparentColor2, "192 373", "572 421"); //Panel_SetPrice2Background
                string text2 = stockMarket ? Lang("SetMaxPrice", player.UserIDString) : "";
                UI_AddText(container, "Market_CreateOfferUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", text2, TextAnchor.MiddleCenter, 21, "192 373", "472 421"); //Text_SetPrice2Title
                UI_AddPanel(container, "Market_CreateOfferUI", config.colors.transparentColor1, "472 373", "572 421"); //Panel_Price2Background
                if (text2 != "")
                    UI_AddInput(container, "Market_CreateOfferUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", TextAnchor.MiddleCenter, 21, addingItemsCache[player].var2.ToString(), 24, $"UI_ShoppyStock addItem var2", "472 373", "572 421"); //Input_Var2
                UI_AddPanel(container, "Market_CreateOfferUI", config.colors.transparentColor1, "192 325", "572 373"); //Panel_SetPrice3Background
                string text3 = stockMarket ? Lang("SetDSAC", player.UserIDString) : "";
                UI_AddText(container, "Market_CreateOfferUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", text3, TextAnchor.MiddleCenter, 21, "192 325", "472 373"); //Text_SetPrice3Title
                UI_AddPanel(container, "Market_CreateOfferUI", config.colors.transparentColor1, "472 325", "572 373"); //Panel_Price3Background
                if (text3 != "")
                    UI_AddInput(container, "Market_CreateOfferUI", config.colors.textColor, "RobotoCondensed-Regular.ttf", TextAnchor.MiddleCenter, 21, addingItemsCache[player].var3.ToString(), 24, $"UI_ShoppyStock addItem var3", "472 325", "572 373"); //Input_Var3
            }
            if (config.debug) Puts($"[DEBUG] Finished checking mailbox item and type.");
            UI_AddText(container, "Market_CreateOfferUI", config.colors.color8, "RobotoCondensed-Bold.ttf", Lang("InputItemHint", player.UserIDString), TextAnchor.MiddleLeft, 13, "201 269", "572 290"); //Text_InputItemHint
            openedUis.Remove(player);
            stockPosition.Remove(player);
            CuiHelper.DestroyUi(player, "Market_AnchorUI");
            CuiHelper.DestroyUi(player, "Market_CreateOfferUI");
            CuiHelper.AddUi(player, container);
        }

        private static PluginConfig config = new PluginConfig();

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(config = new PluginConfig()
            {
                commands = new List<string>()
                {
                    "market",
                    "shop",
                    "s",
                    "m"
                },
                quickListCommands = new Dictionary<string, string>()
                {
                    { "list", "rp" },
                },
                quickSellCommands = new Dictionary<string, string>()
                {
                    { "sell", "rp" },
                },
                shopOpenCommands = new Dictionary<string, string>()
                {
                    { "rpshop", "rp" },
                },
                stockOpenCommands = new Dictionary<string, string>()
                {
                    { "rpstock", "rp" },
                },
                shops = new Dictionary<string, ShopConfig>()
                {
                    { "rp", new ShopConfig() {
                        iconUrl = "",
                        symbol = "{0} RP",
                        canTransfer = true,
                        discounts = new Dictionary<string, float>()
                        {
                            { "shoppystock.discount.vip", 5 },
                            { "shoppystock.discount.premium", 15 }
                        },
                        stockConfig = new StockConfig()
                        {
                            canStockMarket = true,
                            bankEnabled = true,
                            bankPermission = "shoppystock.bank.rp",
                            updateIntervalHourMinutes = new List<int>() {0, 30, 60 },
                            itemCategoryBlacklist = new List<string>() { "Items", "Construction" },
                            categoryOrder = new List<string>() { "TeaIngredients", "Traps", "Food" },
                            sellPriceMultipliers = new Dictionary<string, float>()
                            {
                                { "shoppystock.sell.vip", 1.2f },
                                { "shoppystock.sell.super", 1.5f }
                            },
                            maxBankIndividualItemsPerms = new Dictionary<string, int>()
                            {
                                { "shoppystock.stock.maxitems.15", 15 },
                                { "shoppystock.stock.maxitems.30", 30 },
                            },
                            maxBankStacksPerms = new Dictionary<string, int>()
                            {
                                { "shoppystock.stock.maxitems.50", 50 },
                                { "shoppystock.stock.maxitems.250", 250 },
                            },
                            multiplierEvents = new Dictionary<string, MultiplierEventConfig>()
                            {
                                { "ExtremeDemand", new MultiplierEventConfig() { minMultiplier = 1.7f, maxMultiplier = 2.5f, positiveEffect = true, weight = 1 } },
                                { "HighDemand", new MultiplierEventConfig() { minMultiplier = 1.3f, maxMultiplier = 2.3f, positiveEffect = true, weight = 10 } },
                                { "VeryHighDemand", new MultiplierEventConfig() { minMultiplier = 1.1f, maxMultiplier = 1.7f, positiveEffect = true, weight = 30 } },
                                { "NegativeDemand", new MultiplierEventConfig() { minMultiplier = 0.5f, maxMultiplier = 0.9f, positiveEffect = false, weight = 15 } },
                                { "UltraNegativeDemand", new MultiplierEventConfig() { minMultiplier = 0.2f, maxMultiplier = 0.5f, positiveEffect = false, weight = 5 } }
                            }
                        }
                    } },
                    { "gold", new ShopConfig() {
                        iconUrl = "",
                        symbol = "{0} Gold",
                        canTransfer = false
                    } },
                    { "economics", new ShopConfig() {
                        iconUrl = "",
                        symbol = "${0}",
                        canTransfer = false,
                        otherPluginCurrency = "Economics"
                    } }
                },
                listingPermissions = new Dictionary<string, PermissionData>()
                {
                    { "shoppystock.limit.admin", new PermissionData() { buyListings = 1000, sellListings = 1000 } },
                    { "shoppystock.limit.premium", new PermissionData() { buyListings = 40, sellListings = 40 } },
                    { "shoppystock.limit.user", new PermissionData() { buyListings = 20, sellListings = 20 } }
                },
                categoryIcons = new Dictionary<string, string>()
                {
                    { "bank_management", "" },
                    { "my_listings", "" },
                    { "Tools", "" }
                },
                ignoredShortnames = new List<string>()
                {
                    "coal",
                    "habrepair",
                    "mlrs",
                    "minihelicopter.repair",
                    "scraptransportheli.repair",
                    "submarinesolo",
                    "submarineduo",
                    "locomotive",
                    "wagon",
                    "workcart",
                    "door.key",
                    "blueprintbase",
                    "note",
                    "photo",
                    "captainslog",
                    "rhib",
                    "rowboat",
                    "vehicle.chassis",
                    "vehicle.chassis.3mod",
                    "vehicle.chassis.2mod",
                    "vehicle.chassis.4mod",
                    "vehicle.module",
                    "ammo.snowballgun",
                    "apple.spoiled",
                    "wolfmeat.spoiled",
                    "chicken.spoiled",
                    "humanmeat.spoiled",
                    "deermeat.burned",
                    "chicken.burned",
                    "horsemeat.burned",
                    "meat.pork.burned",
                    "humanmeat.burned",
                    "bearmeat.burned",
                    "wolfmeat.burned",
                    "bottle.vodka",
                    "dogtagneutral",
                    "bluedogtags",
                    "reddogtags",
                    "skull.human",
                    "water.salt",
                    "water",
                    "fishing.tackle",
                    "spraycandecal",
                    "snowmobile",
                    "snowmobiletomaha",
                    "door.closer",
                    "wrappedgift"
                },
                customCategories = new List<string>()
                {
                    "CustomTools",
                    "TeaIngredients"
                },
                customItemInfo = new Dictionary<string, List<ulong>>()
                {
                    { "eventItem", new List<ulong>() { 1854574567, 2156573478 } },
                    { "teaIngredient", new List<ulong>() { 1964346733, 2376545778 } }
                },
                timestamps = new Dictionary<int, string>()
                {
                    { 720, "" },
                    { 1440, "" },
                    { 4320, "shoppystock.timestamp.72h" },
                    { 10080, "shoppystock.timestamp.7d" },
                    { 20160, "shoppystock.timestamp.14d" },
                    { 40320, "shoppystock.timestamp.28d" }
                }
            }, true);
        }

        private class PluginConfig
        {
            [JsonProperty("Commands")]
            public List<string> commands = new List<string>();

            [JsonProperty("Quick Sell Commands")]
            public Dictionary<string, string> quickSellCommands = new Dictionary<string, string>();

            [JsonProperty("Quick List Commands")]
            public Dictionary<string, string> quickListCommands = new Dictionary<string, string>();

            [JsonProperty("Currency Deposit Commands")]
            public Dictionary<string, string> depositCommands = new Dictionary<string, string>();

            [JsonProperty("Certain Shop Open Commands")]
            public Dictionary<string, string> shopOpenCommands = new Dictionary<string, string>();

            [JsonProperty("Certain Stock Market Open Commands")]
            public Dictionary<string, string> stockOpenCommands = new Dictionary<string, string>();

            [JsonProperty("Admin Command")]
            public string adminCommand = "curr";

            [JsonProperty("Open In Cached Shop")]
            public bool openInCached = true;

            [JsonProperty("Enable Console Logging")]
            public bool enableLogs = true;

            [JsonProperty("Enable Stock Market Logging")]
            public bool enableStockStats = true;

            [JsonProperty("Make All Items Multilingual (will generate a lot of new translations in lang file)")]
            public bool translateItems = false;

            [JsonProperty("Market UI - Clicking Sound")]
            public bool clickingSound = true;

            [JsonProperty("Market UI - Child-GUI X Marks Close Only Child-GUI")]
            public bool closeOnlySubGui = false;

            [JsonProperty("Market UI - Cooldown Between Actions (in seconds)")]
            public float uiCooldown = 0.5f;

            [JsonProperty("Market UI - Override Custom Skinned Items With Steam Icons (no URLs needed)")]
            public bool overrideCustomSkins = false;

            [JsonProperty("Market UI - Show Shops Button")]
            public bool shopButton = true;

            [JsonProperty("Market UI - Show Stock Market Button")]
            public bool stockButton = true;

            [JsonProperty("Market UI - Show Transfer Button")]
            public bool transferButton = true;

            [JsonProperty("Pop-Ups - Font Size")]
            public int popUpFontSize = 16;

            [JsonProperty("Pop-Ups - Display Time")]
            public float popUpLength = 10f;

            [JsonProperty("NoEscape - Block Using Shop During Combat")]
            public bool noEscapeCombatShop = false;

            [JsonProperty("NoEscape - Block Using Shop During Raids")]
            public bool noEscapeRaidShop = false;

            [JsonProperty("NoEscape - Block Using Shop During Escape")]
            public bool noEscapeEscapeShop = false;

            [JsonProperty("Shops")]
            public Dictionary<string, ShopConfig> shops = new Dictionary<string, ShopConfig>();

            [JsonProperty("Shop - Sum Discount Types")]
            public bool sumDiscounts = false;

            [JsonProperty("Stock Market - Max Listing Permissions")]
            public Dictionary<string, PermissionData> listingPermissions = new Dictionary<string, PermissionData>();

            [JsonProperty("Stock Market - Category Icons")]
            public Dictionary<string, string> categoryIcons = new Dictionary<string, string>();

            [JsonProperty("Stock Market - Ignored Shortnames")]
            public List<string> ignoredShortnames = new List<string>();

            [JsonProperty("Stock Market - Custom Category Keys")]
            public List<string> customCategories = new List<string>();

            [JsonProperty("Stock Market - Custom Item Info Lang Key")]
            public Dictionary<string, List<ulong>> customItemInfo = new Dictionary<string, List<ulong>>();

            [JsonProperty("Stock Market - Timestamps And Required Permissions (in minutes)")]
            public Dictionary<int, string> timestamps = new Dictionary<int, string>();

            [JsonProperty("Stock Market - Refund Storage Name")]
            public string storageName = "shop";

            [JsonProperty("Stock Market - Can Refund Only In Safezone")]
            public bool safeZoneRefundOnly = true;

            [JsonProperty("Stock Market - Keep Sell Item Remains")]
            public bool keepSellRemains = true;

            [JsonProperty("Stock Market - Can Refund When Authed")]
            public bool authRefund = true;

            [JsonProperty("Stock Market - Broadcast Demands On Chat")]
            public bool broadcastDemands = false;

            [JsonProperty("Stock Market - Allow All Skins Being Listed")]
            public bool allowAllSkinListings = false;

            [JsonProperty("Stock Market - Discord Demand News Channel ID (0 to disable)")]
            public string demandsChannelId = "0";

            [JsonProperty("Stock Market - Enable Discord Bank Price Messages")]
            public bool discordMessages = true;

            [JsonProperty("Enable Debug")]
            public bool debug = false;

            [JsonProperty("UI - Color Codes")]
            public ColorConfig colors = new ColorConfig();
        }

        private class ColorConfig
        {
            [JsonProperty("Color #1")]
            public string color1 = "0.145 0.135 0.12 1";

            [JsonProperty("Color #2")]
            public string color2 = "0.185 0.175 0.16 1";

            [JsonProperty("Color #3")]
            public string color3 = "0.25 0.24 0.225 1";

            [JsonProperty("Color #4")]
            public string color4 = "0.225 0.215 0.2 1";

            [JsonProperty("Color #5")]
            public string color5 = "0.252 0.24 0.225 1";

            [JsonProperty("Color #6")]
            public string color6 = "0.35 0.34 0.325 1";

            [JsonProperty("Color #7")]
            public string color7 = "0.91 0.871 0.831 1";

            [JsonProperty("Color #8")]
            public string color8 = "0.765 0.729 0.694 1";

            [JsonProperty("Transparent Color #1")]
            public string transparentColor1 = "0.6 0.6 0.6 0.1";

            [JsonProperty("Transparent Color #2")]
            public string transparentColor2 = "0.6 0.6 0.6 0.25";

            [JsonProperty("Positive Color - Darker")]
            public string positiveDark = "0.439 0.538 0.261 1";

            [JsonProperty("Positive Color - Lighter")]
            public string positiveLight = "0.733 0.851 0.533 1";

            [JsonProperty("Negative Color - Darker")]
            public string negativeDark = "0.45 0.237 0.194 1";

            [JsonProperty("Negative Color - Lighter")]
            public string negativeLight = "0.941 0.486 0.302 1";

            [JsonProperty("Text Color")]
            public string textColor = "0.91 0.87 0.83 1";
        }

        private class ShopConfig
        {
            [JsonProperty("Required Permission")]
            public string permission = "";

            [JsonProperty("Icon URL")]
            public string iconUrl = "";

            [JsonProperty("Other Currency Plugin")]
            public string otherPluginCurrency = "";

            [JsonProperty("Currency Symbol")]
            public string symbol = "{0} RP";

            [JsonProperty("Can Transfer")]
            public bool canTransfer = false;

            [JsonProperty("Can Deposit")]
            public bool canDeposit = false;

            [JsonProperty("Count Deposit Item As Currency From Inventory")]
            public bool countDepositFromInventory = false;

            [JsonProperty("Give Currency To Inventory")]
            public bool currencyToInventory = false;

            [JsonProperty("Format Currency Amount")]
            public bool formatCurrency = true;

            [JsonProperty("Take X Percentage Of Player's Balance On Map Wipe (0 to disable)")]
            public float percentageTook = 0;

            [JsonProperty("Config Generation - Generate With All Default Items")]
            public bool generateAllDefaultItems = false;

            [JsonProperty("Shop Discount Permission (percentage)")]
            public Dictionary<string, float> discounts = new Dictionary<string, float>();

            [JsonProperty("Deposit Item")]
            public RegularItemConfig depositItem = new RegularItemConfig();

            [JsonProperty("NPC List (and their categories)")]
            public Dictionary<string, List<string>> npcList = new Dictionary<string, List<string>>();

            [JsonProperty("Stock Market Configuration")]
            public StockConfig stockConfig = new StockConfig();

            [JsonProperty("Wipe Currency On Wipe")]
            public bool wipeCurrency = false;
        }

        private class RegularItemConfig
        {
            [JsonProperty("Allow Withdraw")]
            public bool allowWithdraw = false;

            [JsonProperty("Shortname")]
            public string shortname = "";

            [JsonProperty("Skin")]
            public ulong skin = 0;

            [JsonProperty("Icon URL (if skin not 0)")]
            public string url = "";

            [JsonProperty("Value")]
            public int value = 1;

            [JsonProperty("Display Name")]
            public string displayName = "";
        }

        private class StockConfig
        {
            [JsonProperty("Enabled")]
            public bool canStockMarket = false;

            [JsonProperty("Wipe Buy Listings Data On Wipe")]
            public bool wipeBuyListings = false;

            [JsonProperty("Wipe Sell Listings Data On Wipe")]
            public bool wipeSellListings = false;

            [JsonProperty("Wipe Bank Data On Wipe")]
            public bool wipeBankData = false;

            [JsonProperty("Enable All Items Category")]
            public bool allItemsCategoryEnabled = true;

            [JsonProperty("Enabled Bank")]
            public bool bankEnabled = false;

            [JsonProperty("Bank Permission")]
            public string bankPermission = "";

            [JsonProperty("Enable Favourite Tab")]
            public bool favouritesEnabled = true;

            [JsonProperty("Allow Players To Create Buy Requests")]
            public bool allowBuyRequests = true;

            [JsonProperty("Allow Players To Create Sell Requests")]
            public bool allowSellRequests = true;

            [JsonProperty("Show Cheapest Offer Skin As Item Icon")]
            public bool showCheapestIcon = false;

            [JsonProperty("Favourite Permission")]
            public string favouritesPermission = "";

            [JsonProperty("Max Price Per Item (0, to disable)")]
            public int maxItemPrice = 0;

            [JsonProperty("Max Bank Individual Items (0, to disable limit)")]
            public int maxBankIndividualItems = 0;

            [JsonProperty("Max Bank Individual Items Permission Overwrites")]
            public Dictionary<string, int> maxBankIndividualItemsPerms = new Dictionary<string, int>();

            [JsonProperty("Max Bank Each Item Stacks Stored (0, to disable limit)")]
            public int maxBankStacks = 0;

            [JsonProperty("Max Bank Each Item Stacks Stored Permission Overwrites")]
            public Dictionary<string, int> maxBankStacksPerms = new Dictionary<string, int>();

            [JsonProperty("Server Offer Price Update Interval (in minutes)")]
            public int updateInterval = 30;

            [JsonProperty("Chat Messages - Show Price Roll Announcement")]
            public bool chatMessPriceRoll = false;

            [JsonProperty("Chat Messages - Show New Buy Request")]
            public bool chatMessNewBuyRequest = false;

            [JsonProperty("Chat Messages - Show New Sell Request")]
            public bool chatMessNewSellRequest = false;

            [JsonProperty("Chat Messages - Show Sold Messages")]
            public bool chatMessSoldItem = false;

            [JsonProperty("Web Price API - Enabled")]
            public bool enableWebApi = false;

            [JsonProperty("Web Price API - Link")]
            public string webApiLink = "";

            [JsonProperty("Default Selected Category")]
            public string defaultCategory = "";

            [JsonProperty("Overwrite Category Order")]
            public bool overwriteCategoryOrder = false;

            [JsonProperty("Sell Price Permission Multipliers")]
            public Dictionary<string, float> sellPriceMultipliers = new Dictionary<string, float>();

            [JsonProperty("Category Priority Order")]
            public List<string> categoryOrder = new List<string>();

            [JsonProperty("NPC List")]
            public List<string> npcList = new List<string>();

            [JsonProperty("Always Run Timer On Hour Minute")]
            public List<int> updateIntervalHourMinutes = new List<int>();

            [JsonProperty("Blacklisted Category Item Shortnames")]
            public List<string> itemCategoryBlacklist = new List<string>();

            [JsonProperty("Multiplier Events")]
            public Dictionary<string, MultiplierEventConfig> multiplierEvents = new Dictionary<string, MultiplierEventConfig>();
        }

        private class MultiplierEventConfig
        {
            [JsonProperty("Negative (false), Positive (true)")]
            public bool positiveEffect;

            [JsonProperty("Weight")]
            public int weight;

            [JsonProperty("Multiplier - Minimal")]
            public float minMultiplier;

            [JsonProperty("Multiplier - Maximal")]
            public float maxMultiplier;

        }

        private static readonly PluginData data = new PluginData();

        private class PluginData
        {
            [JsonProperty("Shops")]
            public Dictionary<string, ShopData> shops = new Dictionary<string, ShopData>();

            [JsonProperty("Stock Markets")]
            public Dictionary<string, StockData> stockMarkets = new Dictionary<string, StockData>();
        }

        private class ShopData
        {
            [JsonProperty("Users")]
            public Dictionary<ulong, UserData> users = new Dictionary<ulong, UserData>();

            [JsonProperty("Categories")]
            public Dictionary<string, CategoryData> categories = new Dictionary<string, CategoryData>();
        }

        private class StockUserData
        {
            [JsonProperty("Player Banks")]
            public Dictionary<ulong, Dictionary<string, ItemData>> playerBanks = new Dictionary<ulong, Dictionary<string, ItemData>>();

            [JsonProperty("Player Sell Orders")]
            public Dictionary<string, Dictionary<ulong, List<OrderData>>> sellOrders = new Dictionary<string, Dictionary<ulong, List<OrderData>>>();

            [JsonProperty("Player Buy Orders")]
            public Dictionary<string, Dictionary<ulong, List<OrderData>>> buyOrders = new Dictionary<string, Dictionary<ulong, List<OrderData>>>();
        }

        private class StockItemData
        {
            [JsonProperty("Buy Listing Tax (percentage)")]
            public float buyTax = 1;

            [JsonProperty("Sell Listing Tax (percentage)")]
            public float sellTax = 3;

            [JsonProperty("Custom Item Listings")]
            public Dictionary<string, CustomItemData> customItems = new Dictionary<string, CustomItemData>();

            [JsonProperty("Blocked Multiplier Listing Keys")]
            public List<string> blockedMultipliers = new List<string>();

            [JsonProperty("Item Price Calculator")]
            public PriceChangeData priceCalculations = new PriceChangeData();

            [JsonProperty("Server Sell Items")]
            public Dictionary<string, Dictionary<ulong, ServerSellData>> serverSell = new Dictionary<string, Dictionary<ulong, ServerSellData>>();
        }

        private class PermissionData
        {
            [JsonProperty("Max Sell Listings")]
            public int sellListings = 3;

            [JsonProperty("Max Buy Listings")]
            public int buyListings = 3;
        }

        private class StockAlertData
        {
            [JsonProperty("Insta-Sell Price")]
            public float instaSellPrice = 0;

            [JsonProperty("Alert Price")]
            public float alertPrice = 0;

        }

        private class StockData
        {
            [JsonProperty("Stock Config")]
            public StockItemData stockConfig = new StockItemData();

            [JsonProperty("Stock Sell Cache")]
            public Dictionary<string, Dictionary<ulong, ServerSellCacheData>> sellCache = new Dictionary<string, Dictionary<ulong, ServerSellCacheData>>();

            [JsonProperty("Player Data")]
            public StockUserData playerData = new StockUserData();

            [JsonProperty("Alert Data")]
            public Dictionary<ulong, Dictionary<string, StockAlertData>> alertData = new Dictionary<ulong, Dictionary<string, StockAlertData>>();

            [JsonProperty("Statistics Data")]
            public EarningsData stats = new EarningsData();

            [JsonProperty("Favourites Data")]
            public Dictionary<ulong, List<string>> favourites = new Dictionary<ulong, List<string>>();
        }

        private class StockItemDefinitionData
        {
            [JsonProperty("Shortname")]
            public string shortname;

            [JsonProperty("Category")]
            public string category;

            [JsonProperty("Display Name")]
            public string displayName;

            [JsonProperty("Skin", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public ulong skin;
        }

        private class OrderData
        {
            [JsonProperty("Seller Name")]
            public string sellerName;

            [JsonProperty("Seller ID")]
            public ulong sellerId;

            [JsonProperty("Price")]
            public float price;

            [JsonProperty("Is Canceled")]
            public bool isCanceled = false;

            [JsonProperty("Item")]
            public ItemData item;
        }
        private class CustomItemData
        {
            [JsonProperty("Shortname")]
            public string shortname;

            [JsonProperty("Amount")]
            public int amount = 1;

            [JsonProperty("Skin")]
            public ulong skin = 0;

            [JsonProperty("Icon URL")]
            public string url = "";

            [JsonProperty("Category")]
            public string category = "";

            [JsonProperty("Display Name")]
            public string displayName = "";
        }

        private class ItemData
        {
            [JsonProperty("Shortname")]
            public string shortname;

            [JsonProperty("Amount")]
            public int amount;

            [JsonProperty("Skin", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public ulong skin;

            [JsonProperty("Is Blueprint", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public bool isBlueprint;

            [JsonProperty("Blueprint Target", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public int blueprintTarget;

            [JsonProperty("Text", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string text;

            [JsonProperty("Fuel", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public float fuel;

            [JsonProperty("Flame Fuel", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public int flameFuel;

            [JsonProperty("Ammo Amount", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public int ammo;

            [JsonProperty("Ammo Type", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public int ammoType;

            [JsonProperty("Display Name", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string displayName;

            [JsonProperty("Condition", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public float condition;

            [JsonProperty("Max Condition", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public float maxCondition = -1;

            [JsonProperty("Data INT", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public int dataInt;

            [JsonProperty("Item Contents", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public List<ItemData> contents = new List<ItemData>();

            public Item ToItem()
            {
                if (amount == 0) return null;
                Item item = ItemManager.CreateByName(shortname, amount, skin);
                if (isBlueprint)
                {
                    item.blueprintTarget = blueprintTarget;
                    return item;
                }
                item.fuel = fuel;
                item.condition = condition;
                if (maxCondition != -1)
                    item.maxCondition = maxCondition;
                if (contents != null)
                {
                    if (contents.Count > 0)
                    {
                        if (item.contents == null)
                        {
                            item.contents = new ItemContainer();
                            item.contents.ServerInitialize(null, contents.Count);
                            item.contents.GiveUID();
                            item.contents.parent = item;
                        }
                        foreach (var contentItem in contents)
                            contentItem.ToItem().MoveToContainer(item.contents);
                    }
                }
                else
                    item.contents = null;
                BaseProjectile.Magazine magazine = item.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine;
                FlameThrower flameThrower = item.GetHeldEntity()?.GetComponent<FlameThrower>();
                if (magazine != null)
                {
                    magazine.contents = ammo;
                    magazine.ammoType = ItemManager.FindItemDefinition(ammoType);
                }
                if (flameThrower != null)
                    flameThrower.ammo = flameFuel;
                if (dataInt > 0)
                {
                    item.instanceData = new ProtoBuf.Item.InstanceData
                    {
                        ShouldPool = false,
                        dataInt = dataInt
                    };
                }
                item.text = text;
                if (displayName != null)
                    item.name = displayName;
                return item;
            }

            public static ItemData FromItem(Item item) => new ItemData
            {
                shortname = item.info.shortname,
                ammo = item.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine?.contents ?? 0,
                ammoType = item.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine?.ammoType?.itemid ?? 0,
                amount = item.amount,
                condition = item.condition,
                maxCondition = item.maxCondition,
                fuel = item.fuel,
                skin = item.skin,
                contents = item.contents?.itemList?.Select(FromItem).ToList(),
                flameFuel = item.GetHeldEntity()?.GetComponent<FlameThrower>()?.ammo ?? 0,
                isBlueprint = item.IsBlueprint(),
                blueprintTarget = item.blueprintTarget,
                dataInt = item.instanceData?.dataInt ?? 0,
                displayName = item.name,
                text = item.text
            };
        }

        private class PriceChangeData
        {
            [JsonProperty("Price Change - Regular Curve (percentage from min to max)")]
            public float regularCurve = 5;

            [JsonProperty("Price Change - Same Price Actions Min")]
            public int sameActionsMin = 2;

            [JsonProperty("Price Change - Same Price Actions Max")]
            public int sameActionsMax = 4;

            [JsonProperty("Price Change - Chances To Increment Based On Current Price Percentage")]
            public Dictionary<float, float> priceBarriers = new Dictionary<float, float>();

            [JsonProperty("Price Drop - Amount Sell Values Penalty Multiplier (percentage from amount)")]
            public Dictionary<float, float> priceDropChart = new Dictionary<float, float>();

            [JsonProperty("Price Drop - Amount Sold Max Price Penalty (percentage from amount)")]
            public Dictionary<float, PentalityData> sellPricePentality = new Dictionary<float, PentalityData>();

            [JsonProperty("Price Increase - Goal Not Achieved (percentage from amount)")]
            public Dictionary<float, float> goalAchievedChart = new Dictionary<float, float>();

            [JsonProperty("Default Sell Amount Calculation - Players Online Multiplier")]
            public Dictionary<int, float> sellAmountOnlineMultiplier = new Dictionary<int, float>();

            [JsonProperty("Price Multipliers - Minimal Time Distance Between Events (in price update ticks)")]
            public int minTimeDistance = 10;

            [JsonProperty("Price Multipliers - Chance Based On Sell Amount (0-100) (percentage from amount)")]
            public Dictionary<float, float> multplierAmountChance = new Dictionary<float, float>();

            [JsonProperty("Price Multipliers - Minimal Actions Time")]
            public int multiplierMinLength = 2;

            [JsonProperty("Price Multipliers - Maximal Actions Time")]
            public int multiplierMaxLength = 4;

            [JsonProperty("Positive Price Multipliers - Max Price (percentage)")]
            public float positiveMaxPrice = 50;

            [JsonProperty("Positive Price Multipliers - Min Price (percentage)")]
            public float positiveMinPrice = 10;

            [JsonProperty("Positive Price Multipliers - Random Events")]
            public List<string> positiveRandomEvents = new List<string>();

            [JsonProperty("Negative Price Multipliers - Max Price (percentage)")]
            public float negativeMaxPrice = 100;

            [JsonProperty("Negative Price Multipliers - Min Price (percentage)")]
            public float negativeMinPrice = 25;

            [JsonProperty("Negative Price Multipliers - Random Events")]
            public List<string> negativeRandomEvents = new List<string>();
        }

        private class ServerSellData
        {
            [JsonProperty("Display Name")]
            public string displayName = "";

            [JsonProperty("Minimal Price")]
            public float minimalPrice;

            [JsonProperty("Maximal Price")]
            public float maximalPrice;

            [JsonProperty("Price Parent (shortname-skin)")]
            public string priceParent = "";

            [JsonProperty("Price Parent - Price Boost Min")]
            public float priceBoostMin = 0;

            [JsonProperty("Price Parent - Price Boost Max")]
            public float priceBoostMax = 0;

            [JsonProperty("Default Sell Amount Calculation")]
            public int defaultAmount = 1000;

            [JsonProperty("Max Items Sold - Daily (0, to disable limit)")]
            public int maxDailySold = 0;

            [JsonProperty("Max Items Sold Permission Overwrites - Daily")]
            public Dictionary<string, int> maxDailySoldPerms = new Dictionary<string, int>();
        }

        private class ServerSellCacheData
        {
            [JsonProperty("Price (without multiplier)")]
            public float cachedPrice;

            [JsonProperty("Multiplier")]
            public float bonusMultiplier;

            [JsonProperty("Multiplier Length")]
            public int bonusMultiplierLength;

            [JsonProperty("Price")]
            public float price;

            [JsonProperty("Price History")]
            public List<float> priceHistory = new List<float>();

            [JsonProperty("Sell Amount")]
            public int sellAmount = 0;

            [JsonProperty("Sell Amount History")]
            public List<int> sellAmountHistory = new List<int>();

            [JsonProperty("Price Drop - Penalty Count")]
            public int pentalityCount = 0;

            [JsonProperty("Price Multipliers - Next Possible Event Event")]
            public int nextPossibleEvent;

            [JsonProperty("Action - Current Action")]
            public string action = "";

            [JsonProperty("Action - Count")]
            public int actionCount;

            [JsonProperty("Action - Goal")]
            public int actionGoal;
        }

        private class PentalityData
        {
            [JsonProperty("Max Price Percentage")]
            public float percentage = 0;

            [JsonProperty("Penalty Length (in price update ticks)")]
            public int pentalityLength = 0;
        }

        private class UserData
        {
            [JsonProperty("Username")]
            public string username;

            [JsonProperty("Currency")]
            public int currencyAmount = 0;

            [JsonProperty("Daily Purchases")]
            public Dictionary<string, Dictionary<string, int>> dailyPurchases = new Dictionary<string, Dictionary<string, int>>();

            [JsonProperty("Wipe Purchases")]
            public Dictionary<string, int> wipePurchases = new Dictionary<string, int>();

            [JsonProperty("Cooldowns")]
            public Dictionary<string, DateTime> cooldowns = new Dictionary<string, DateTime>();
        }

        private class CategoryData
        {
            [JsonProperty("Icon URL")]
            public string iconUrl = "";

            [JsonProperty("Required Permission")]
            public string permission = "";

            [JsonProperty("Display Blacklist Permissions (need to have all)")]
            public List<string> blacklistPermissions = new List<string>();

            [JsonProperty("Category Discount Permission (percentage)")]
            public Dictionary<string, float> discounts = new Dictionary<string, float>();

            [JsonProperty("Listings")]
            public Dictionary<string, ListingData> listings = new Dictionary<string, ListingData>();
        }

        private class ListingData
        {
            [JsonProperty("Command (if set, ignore item)")]
            public List<string> commands = new List<string>();

            [JsonProperty("Shortname")]
            public string shortname;

            [JsonProperty("Skin ID")]
            public ulong skin = 0;

            [JsonProperty("Amount")]
            public int amount = 1;

            [JsonProperty("Item Name")]
            public string itemName = "";

            [JsonProperty("Display Name")]
            public string displayName = "";

            [JsonProperty("Is Blueprint")]
            public bool blueprint = false;

            [JsonProperty("Icon URL (if skin not 0)")]
            public string iconUrl = "";

            [JsonProperty("Price")]
            public int price = 1000;

            [JsonProperty("Price Per Purchase Multiplier")]
            public float pricePerPurchaseMultiplier = 1;

            [JsonProperty("Multiply Price Per Daily (true) Or Per Wipe (false) Purchases")]
            public bool multiplyPricePerDaily = true;

            [JsonProperty("Show Description Field")]
            public bool description = false;

            [JsonProperty("Discount Permission (value)")]
            public Dictionary<string, int> discounts = new Dictionary<string, int>();

            [JsonProperty("Required Permission")]
            public string permission = "";

            [JsonProperty("Display Blacklist Permission")]
            public string blacklistPermission = "";

            [JsonProperty("Daily Buy Max")]
            public int dailyBuy = 0;

            [JsonProperty("Wipe Buy Max")]
            public int wipeBuy = 0;

            [JsonProperty("Cooldown Between Purchases (in seconds, 0 to disable)")]
            public int cooldown = 0;
        }

        private class EarningsData
        {
            [JsonProperty("Global Daily Earnings")]
            public Dictionary<string, int> globalDaily = new Dictionary<string, int>();

            [JsonProperty("Player Daily Earnings")]
            public Dictionary<ulong, Dictionary<string, int>> playerDaily = new Dictionary<ulong, Dictionary<string, int>>();

            [JsonProperty("Player All Earnings")]
            public Dictionary<ulong, int> playerAll = new Dictionary<ulong, int>();

            [JsonProperty("Global Daily Sold Items")]
            public Dictionary<string, Dictionary<string, int>> globalDailyItems = new Dictionary<string, Dictionary<string, int>>();

            [JsonProperty("Global All Sold Items")]
            public Dictionary<string, int> globalAllItems = new Dictionary<string, int>();

            [JsonProperty("Player Daily Sold Items")]
            public Dictionary<ulong, Dictionary<string, Dictionary<string, int>>> playerDailyItems = new Dictionary<ulong, Dictionary<string, Dictionary<string, int>>>();

            [JsonProperty("Player All Sold Items")]
            public Dictionary<ulong, Dictionary<string, int>> playerAllItems = new Dictionary<ulong, Dictionary<string, int>>();
        }


        private void LoadData()
        {
            foreach (var shop in config.shops)
            {
                data.shops.TryAdd(shop.Key, new ShopData());
                data.shops[shop.Key].users = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, UserData>>($"{Name}/UserData/{shop.Key}");
                data.shops[shop.Key].categories = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, CategoryData>>($"{Name}/Shops/{shop.Key}");
                if (shop.Value.stockConfig.canStockMarket)
                {
                    data.stockMarkets.TryAdd(shop.Key, new StockData());
                    data.stockMarkets[shop.Key].stockConfig = Interface.Oxide.DataFileSystem.ReadObject<StockItemData>($"{Name}/StockMarket/Config/{shop.Key}");
                    data.stockMarkets[shop.Key].alertData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<string, StockAlertData>>>($"{Name}/StockMarket/AlertData/{shop.Key}");
                    data.stockMarkets[shop.Key].playerData = Interface.Oxide.DataFileSystem.ReadObject<StockUserData>($"{Name}/StockMarket/PlayerData/{shop.Key}");
                    data.stockMarkets[shop.Key].sellCache = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, Dictionary<ulong, ServerSellCacheData>>>($"{Name}/StockMarket/PriceCache/{shop.Key}");
                    if (config.enableStockStats)
                        data.stockMarkets[shop.Key].stats = Interface.Oxide.DataFileSystem.ReadObject<EarningsData>($"{Name}/StockMarket/Statistics/{shop.Key}");
                    if (shop.Value.stockConfig.favouritesEnabled)
                        data.stockMarkets[shop.Key].favourites = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<string>>>($"{Name}/StockMarket/FavouriteData/{shop.Key}");
                    if (data.stockMarkets[shop.Key].stockConfig.priceCalculations.priceBarriers.Count == 0)
                        newStocks.Add(shop.Key);
                }
            }
            SaveData();
            timer.Every(Core.Random.Range(500, 700), () => SaveData());
        }

        private void SaveData(bool shopSave = false, string onlyOneSave = "")
        {
            foreach (var shop in config.shops)
            {
                if (onlyOneSave != "")
                {
                    if (shop.Key == onlyOneSave)
                        Interface.Oxide.DataFileSystem.WriteObject($"{Name}/StockMarket/Config/{shop.Key}", data.stockMarkets[shop.Key].stockConfig);
                    continue;
                }
                if (data.shops.ContainsKey(shop.Key))
                {
                    Interface.Oxide.DataFileSystem.WriteObject($"{Name}/UserData/{shop.Key}", data.shops[shop.Key].users);
                    if (shopSave)
                        Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Shops/{shop.Key}", data.shops[shop.Key].categories);
                }
                if (shop.Value.stockConfig.canStockMarket)
                {
                    Interface.Oxide.DataFileSystem.WriteObject($"{Name}/StockMarket/AlertData/{shop.Key}", data.stockMarkets[shop.Key].alertData);
                    Interface.Oxide.DataFileSystem.WriteObject($"{Name}/StockMarket/PlayerData/{shop.Key}", data.stockMarkets[shop.Key].playerData);
                    Interface.Oxide.DataFileSystem.WriteObject($"{Name}/StockMarket/PriceCache/{shop.Key}", data.stockMarkets[shop.Key].sellCache);
                    if (config.enableStockStats)
                        Interface.Oxide.DataFileSystem.WriteObject($"{Name}/StockMarket/Statistics/{shop.Key}", data.stockMarkets[shop.Key].stats);
                    if (shop.Value.stockConfig.favouritesEnabled)
                        Interface.Oxide.DataFileSystem.WriteObject($"{Name}/StockMarket/FavouriteData/{shop.Key}", data.stockMarkets[shop.Key].favourites);
                    if (shopSave)
                        Interface.Oxide.DataFileSystem.WriteObject($"{Name}/StockMarket/Config/{shop.Key}", data.stockMarkets[shop.Key].stockConfig);
                }
            }
        }

        private void LoadMessages()
        {
            Dictionary<string, string> langFile = new Dictionary<string, string>()
            {
                ["ShopsButton"] = "SHOPS",
                ["StockButton"] = "STOCK MARKET",
                ["TransferButton"] = "TRANSFER",
                ["SelectShopHint"] = "Select one of available shops to start purchasing!",
                ["Amount"] = "BALANCE",
                ["OpenShopButton"] = "OPEN SHOP",
                ["EnoughFunds"] = "ENOUGH FUNDS",
                ["ItemName"] = "ITEM NAME",
                ["ItemPrice"] = "PRICE",
                ["FinishOfferTitle"] = "PURCHASE ORDER",
                ["PurchaseAmount"] = "AMOUNT",
                ["BalanceAfterPurchase"] = "BALANCE AFTER PURCHASE",
                ["TotalPrice"] = "TOTAL PRICE",
                ["CancelButton"] = "GO BACK",
                ["ButtonAccept"] = "PURCHASE",
                ["NotEnoughCurrency"] = "You don't have <color=#5c81ed>enough currency</color> to purchase this item!",
                ["NotEnoughSpace"] = "You don't have <color=#5c81ed>enough space in inventory</color> to purchase this item!",
                ["SuccesfullyPurchased"] = "You've successfully purchased <color=#5c81ed>x{0} {1}</color> for <color=#5c81ed>{2}</color>!",
                ["DailyLimit"] = "Daily: {0}/{1}",
                ["DailyLimitReached"] = "You've reached <color=#5c81ed>daily limit</color> of purchases!",
                ["WipeLimit"] = " Wipe: {0}/{1}",
                ["CooldownWait"] = " Cooldown: {0}",
                ["WipeLimitReached"] = "You've reached <color=#5c81ed>wipe limit</color> of purchases!",
                ["NoShopPermission"] = "You don't have access to shops!",
                ["SelectStockMarketHint"] = "Select one of available stock market to start purchasing/selling!",
                ["OpenStockMarketButton"] = "OPEN MARKET",
                ["ServerBuyPrice"] = "SELL<size=10>\nTO SERVER\n</size>",
                ["PlayerBuyPrice"] = "PLAYER<size=10>\nBUY OFFER\n</size>",
                ["PlayerSellPrice"] = "PLAYER<size=10>\nSELL OFFER\n</size>",
                ["SellToServerOnly"] = "SERVER SELL ONLY",
                ["BuySellOrdersOnly"] = "BUY/SELL OFFERS ONLY",
                ["ServerSellOffer"] = "SERVER SELL OFFER",
                ["BuyOffersButton"] = "BUY OFFERS",
                ["SellOffersButton"] = "SELL OFFERS",
                ["NotForServerSale"] = "THIS ITEM IS NOT AVAILABLE FOR SELL TO THE SERVER",
                ["Details"] = "DETAILS",
                ["Seller"] = "SELLER",
                ["Buyer"] = "BUYER",
                ["NoOffersFound"] = "NO OFFERS FOUND",
                ["ShowMyOffers"] = "SHOW MY OFFERS",
                ["SelectedItem"] = "SELECTED ITEM",
                ["CreateSellOffer"] = "CREATE SELL OFFER",
                ["CreateBuyOffer"] = "CREATE BUY OFFER",
                ["ServerSellItem"] = "SOLD ITEM",
                ["SendAlertPrice"] = "SEND ALERT PRICE",
                ["InstaSellPrice"] = "INSTA-SELL PRICE",
                ["MustBeDiscord"] = "DISCORD CONNECTION REQUIRED",
                ["OpenSellInventory"] = "OPENS SELL INVENTORY",
                ["ItemCountInBank"] = "ITEMS IN BANK: {0}",
                ["SellToServerButton"] = "SELL TO SERVER",
                ["AutoSellFromBank"] = "SELL FROM BANK",
                ["PriceHistory"] = "PRICE HISTORY CHART",
                ["ChangeTimestamp"] = "SWITCH\nTIMESTAMP",
                ["HighestPrice"] = "HIGHEST PRICE",
                ["LowestPrice"] = "LOWEST PRICE",
                ["SumPrice"] = "SUM PRICE",
                ["PurchaseButton"] = "PURCHASE",
                ["SellButton"] = "SELL",
                ["Category_bank_management"] = "BANK MANAGEMENT",
                ["Category_my_listings"] = "MY LISTINGS",
                ["Category_favourites"] = "FAVOURITES",
                ["Category_all_items"] = "ALL ITEMS",
                ["LastTitle"] = "LAST {0}",
                ["ListingCanceled"] = "You've canceled your listing. You can refund it by clicking again, or add it again by clicking LIST AGAIN button.",
                ["ItemsSold"] = "You've sold your items for <color=#5c81ed>{0}</color>.",
                ["NoAdminPermission"] = "You don't have permission to use this command.",
                ["NoTransferAvailable"] = "You don't have rights to transfer your currencies.",
                ["AdminCommandHelp"] = "Command Usage:\n{0} <shopName> give <userIdOrName> <amount> - Gives to player certain amount of currency.\n{0} <shopName> take <userIdOrName> <amount> - Takes from player certain amount of currency.\n{0} <shopName> clear <userIdOrName> - Clears player's balance.\n{0} <shopName> check <userIdOrName> - Checks player's current balance.",
                ["ShopNotFound"] = "Shop '{0}' has not been found.",
                ["UserNotFound"] = "user with ID/Name '{0}' has not been found.",
                ["TooManyUsersFound"] = "Found more than one user with same nickname. Try input whole nickname or use ID instead.",
                ["WrongAmountFormat"] = "Input value '{0}' is not an integer.",
                ["CurrencyAdded"] = "Added {2} to {1}'s {0} shop balance. Current balance: {3}",
                ["CurrencyTaken"] = "Took {2} from {1}'s {0} shop balance. Current balance: {3}",
                ["CurrencyCleared"] = "Balance of {1}'s {0} shop has been cleared.",
                ["CurrencyCheck"] = "Balance of {1}'s {0} shop: {2}",
                ["NotAuthToRefund"] = "You are <color=#5c81ed>not authorized in cupboard</color>.\nYou cannot refund items.",
                ["NotInSafeZone"] = "You are <color=#5c81ed>not in safe zone</color>.\nYou cannot refund items.",
                ["ItemPurchased"] = "You've purchased <color=#5c81ed>x{3} {1}</color> from <color=#5c81ed>{0}</color>'s shop for <color=#5c81ed>{2}</color>.",
                ["ItemPurchasedOwner"] = "Someone purchased your <color=#5c81ed>x {0} {1}</color> for <color=#5c81ed>{2}</color>.",
                ["BuyOrderFulfilled"] = "{0} sold you <color=#5c81ed>x {1} {2}</color> for <color=#5c81ed>{3}</color>.",
                ["ItemNoLongerAvailable"] = "This item is no longer available!",
                ["ItemReturned"] = "You've returned <color=#5c81ed>x{0} {1}</color>.\nIt should be available in <color=#5c81ed>/redeem {2}</color>.",
                ["NoValidItemsInInventory"] = "You don't have <color=#5c81ed>valid item</color> in inventory.",
                ["ItemSold"] = "You've sold <color=#5c81ed>{1}</color> to <color=#5c81ed>{0}</color> for <color=#5c81ed>{2}</color>.",
                ["RefundBuyRequest"] = "You've canceled your buy request of <color=#5c81ed>{0}</color>.\n<color=#5c81ed>{1}</color> is back on your balance.",
                ["NoPermissionListing"] = "You don't have permission to add new listings!",
                ["ListingLimitAcieved"] = "You've achieved the <color=#5c81ed>maximum limit</color> of active listings!",
                ["ItemNotSupported"] = "This item is <color=#5c81ed>not supported</color> in the market!",
                ["BuyListingAdded"] = "You've added buy request for <color=#5c81ed>{0}</color> for <color=#5c81ed>{1}</color>.",
                ["SellListingAdded"] = "You've added sell request for <color=#5c81ed>{0}</color>. You paid <color=#5c81ed>{1}</color> tax.",
                ["PriceTooBig"] = "Price you've set is too big. Decrease the cost of your offer!",
                ["ItemConditionBroken"] = "You can't add <color=#5c81ed>broken items</color> to bank!",
                ["ItemAddedToBank"] = "Item has been <color=#5c81ed>added</color> to your item bank.",
                ["ItemNotForSale"] = "This item is <color=#5c81ed>not available</color> for server sale!",
                ["ValueIsNotNumber"] = "Input value is <color=#5c81ed>not an integer</color>.",
                ["NoItemInBank"] = "You don't have <color=#5c81ed>this type of item</color> in your bank!",
                ["BankItemsSold"] = "You've sold <color=#5c81ed>x{0}</color> of <color=#5c81ed>{2}</color> from your bank for <color=#5c81ed>{1}</color>.",
                ["InstaSellDiscordMessage"] = "Hey! I sold for you <color=#5c81ed>x{0}</color> of <color=#5c81ed>{2}</color> from your bank for <color=#5c81ed>{1}</color>. You don't need to thank me. ^^",
                ["AlertPricePopUp"] = "There is an alert price for <color=#5c81ed>{1}</color>!\nCurrent price is <color=#5c81ed>{0}</color>!",
                ["AlertDiscordMessage"] = "Hey! There is a good price for <color=#5c81ed>{1}</color>. For now it's <color=#5c81ed>{0}</color>, but it can change anytime!",
                ["StockMarketDisabled"] = "Stock market is disabled!",
                ["Canceled_Info"] = "CANCELED",
                ["Genes_Info"] = "{0}",
                ["Condition_Info"] = "CONDITION\n{0}%",
                ["NewBuyRequestTitle"] = "BUY REQUEST",
                ["NewSellRequestTitle"] = "SELL REQUEST",
                ["SetPricePerOne"] = "PRICE PER ONE",
                ["TotalIncome"] = "TOTAL INCOME",
                ["ListingTax"] = "LISTING TAX",
                ["InputItemHint"] = "If possible, input item here.",
                ["NoItemsFound"] = "NO ITEMS IN BANK",
                ["ItemBank"] = "ITEM BANK",
                ["WithdrawalHint"] = "WITHDRAWED ITEMS GOES TO /REDEEM {0} INVENTORY",
                ["WithdrawAmount"] = "WITHDRAW AMOUNT",
                ["ItemAmount"] = "ITEM AMOUNT",
                ["ItemAmountShort"] = "AMOUNT",
                ["ItemSellTitle"] = "ITEM SELL LIST",
                ["AndMoreItems"] = "AND {0} MORE ITEMS...",
                ["ClickToSell"] = "CLICK TO SELL",
                ["IrreversibleAction"] = "THIS ACTION IS IRREVERSIBLE",
                ["SelectTransferPlayerHint"] = "Select player that you want to transfer currency.",
                ["OnlineOnly"] = "SHOW ONLY ONLINE PLAYERS",
                ["TransferUserTitle"] = "TRANSFER CURRENCY",
                ["Currency"] = "CURRENCY",
                ["Username"] = "USERNAME",
                ["ClickToChange"] = "CLICK TO CHANGE",
                ["PlayerCovalenceError"] = "PLAYER IS ON THE LIST, BUT NOT IN DATABASE. CONTACT ADMINISTRATOR.",
                ["TransferAmount"] = "SET AMOUNT",
                ["BalanceAfterTransfer"] = "BALANCE AFTER TRANSFER",
                ["InputAmount"] = "INPUT CORRECT AMOUNT",
                ["CurrencyTransfered"] = "You've transfered <color=#5c81ed>{1}</color> to <color=#5c81ed>{0}</color>'s balance!",
                ["CurrencyReceived"] = "You've received <color=#5c81ed>{1}</color> from <color=#5c81ed>{0}</color>!",
                ["BlueprintTag"] = " [Blueprint]",
                ["CannotSetPriceOrAmountZero"] = "You cannot set the price or amount to zero!",
                ["ListingReadded"] = "Listing is available again on market!",
                ["Deposit"] = "DEPOSIT",
                ["DepositOrWithdraw"] = "DEPOSIT/WITHDRAW",
                ["WithdrawCurrency"] = "WITHDRAW",
                ["CurrencyDeposited"] = "You've depsoited <color=#5c81ed>{0}</color> to your shop balance!",
                ["CurrencyItemTitle"] = "CURRENCY ITEM",
                ["ItemValue"] = "ITEM VALUE",
                ["CurrencyWithdrawAmount"] = "Widthdraw Amount",
                ["InputCurrencyItemHint"] = "Add currency item here and press SUBMIT button to deposit",
                ["PriceTooHigh"] = "You've set too high price for your listing! Set it to <color=#5c81ed>{0}</color> or lower!",
                ["ShopRaidBlocked"] = "You are currently in fight. You cannot open the shop!",
                ["PurchaseCooldown"] = "This purchase is on cooldown!\nYou can buy this item again in <color=#5c81ed>{0}</color>.",
                ["NoPriceData"] = "NO PRICE HISTORY DATA",
                ["ItemAddedToStock"] = "Your item has been added to the <color=#5c81ed>{0}</color> stock market in category <color=#5c81ed>{1}</color>.",
                ["ItemAddedToStockAndSell"] = "Your item has been added to the <color=#5c81ed>{0}</color> stock market in category <color=#5c81ed>{1}</color>. It's also available to sell from now on.",
                ["ItemAddedToShop"] = "Your item has been added to the <color=#5c81ed>{0}</color> shop in category <color=#5c81ed>{1}</color>.",
                ["NewItemEntry"] = "NEW ITEM ENTRY",
                ["MoreInConfig"] = "MORE OPTIONS AVAILABLE IN CONFIG FILE\nSHOP NAME: {0}\nCATEGORY: {1}\nSTOCK MARKET: {2}",
                ["SetMinPrice"] = "MINIMAL PRICE",
                ["SetMaxPrice"] = "MAXIMAL PRICE",
                ["SetDSAC"] = "SET DSAC",
                ["AdminAddItem"] = "[ADMIN] ADD ITEM ENTRY IN THIS CATEGORY",
                ["B"] = "B",
                ["S"] = "S",
                ["DepositAll"] = "DEPOSIT ALL",
                ["DescriptionTextBackground"] = "DESCRIPTION",
                ["AddBack"] = "LIST AGAIN",
                ["NotEnoughCurrencyWithdraw"] = "You don't have enough currency to withdraw. You can withdraw up to <color=#5c81ed>{0}</color>.",
                ["CurrencyWithdrawed"] = "You've successfully withdrawed your currency!",
                ["TooBigAmountForStack"] = "Withdraw amount is larger than currency item stack size! (<color=#5c81ed>{0}</color>)",
                ["ItemNotAllowedToList"] = "This item is not allowed for listing!",
                ["NotEnoughCurrencySplit"] = "You have enough currency, but it's splitted into 2 wallets! Deposit your currency into virtual wallet or withdraw it to have required amount in one wallet!",
                ["NotValidShopAssigned"] = "There is <color=#5c81ed>{0}</color> shop assigned to this command, but shop doesn't exist or it doesn't have stock market enabled!",
                ["BuyRequestsNotAllowed"] = "You can't create <color=#5c81ed>buy requests</color>!",
                ["SellRequestsNotAllowed"] = "You can't create <color=#5c81ed>sell requests</color>!",
                ["TooManyIndividualItemsInBank"] = "You have too many individual items in bank. You can store only up to <color=#5c81ed>{0}</color> different items!",
                ["TooManyStacksInBank"] = "You have too many items of this type in bank. You can store only up to <color=#5c81ed>{0}</color> items!",
                ["DailyLimitSellReached"] = "You have reached daily limit of <color=#5c81ed>{0}</color> item sales of this type. Wait for the next day to sell more!",
                ["PriceRolled"] = "Prices has been rolled in <color=#5c81ed>{0}</color> shop!",
                ["AddedBuyListing"] = "Player {0} added buy listing of <color=#5c81ed>x{1} {2}</color> in {3} shop for {4}!",
                ["AddedSellListing"] = "Player {0} added sell listing of <color=#5c81ed>x{1} {2}</color> in {3} shop for {4}!",
                ["ItemPurchasedBroadcast"] = "Player {0} purchased <color=#5c81ed>x{1} {2}</color> from {3} in {4} shop for {5}!",

            };
            if (config.translateItems)
                foreach (var item in ItemManager.itemList)
                    langFile.TryAdd($"{item.shortname}-0", item.displayName.english);
            foreach (var shop in config.shops)
            {
                langFile.Add(shop.Key, shop.Key.ToUpper());
                langFile.Add($"{shop.Key}_Description", $"Description of {shop.Key.ToUpper()} shop. Can be changed in oxide/lang/en/ShoppyStock.json");
                if (shop.Value.stockConfig.canStockMarket)
                {
                    langFile.Add($"{shop.Key}_StockMarket", $"{shop.Key.ToUpper()} STOCK MARKET");
                    langFile.Add($"{shop.Key}_StockMarketDescription", $"Description of {shop.Key.ToUpper()} stock market. Can be changed in oxide/lang/en/ShoppyStock.json");
                    if (config.translateItems)
                        foreach (var item in data.stockMarkets[shop.Key].stockConfig.customItems)
                            langFile.TryAdd(item.Key, item.Value.displayName);
                }
                foreach (var category in data.shops[shop.Key].categories)
                {
                    langFile.TryAdd($"Category_{category.Key}", category.Key.ToUpper());
                    foreach (var item in category.Value.listings)
                        if (item.Value.description)
                            langFile.TryAdd($"Description_{item.Key}", $"{item.Value.displayName} description. Can be changed in oxide/lang/en/ShoppyStock.json");
                }
                foreach (var category in stockCategories.Keys)
                    langFile.TryAdd($"Category_{category}", category.ToUpper());
                foreach (var priceEvent in shop.Value.stockConfig.multiplierEvents)
                {
                    if (priceEvent.Value.positiveEffect)
                        langFile.TryAdd($"Event_{priceEvent.Key}", "The price has been drasticly changed on **{0}**! It increased by **{1}%**!");
                    else
                        langFile.TryAdd($"Event_{priceEvent.Key}", "The price has dropped on **{0}**! There was **{1}%** drop!");
                }
                if (config.translateItems)
                    foreach (var category in data.shops[shop.Key].categories)
                        foreach (var item in category.Value.listings)
                            langFile.TryAdd(item.Key, item.Value.displayName);
            }
            foreach (var category in config.customCategories)
                langFile.TryAdd($"Category_{category}", category.ToUpper());
            foreach (var customInfo in config.customItemInfo)
                langFile.TryAdd($"{customInfo.Key}_Info", customInfo.Key.ToUpper());
            lang.RegisterMessages(langFile, this);
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private static void UI_AddAnchor(CuiElementContainer container, string panelName, string parentName, string anchorMin, string anchorMax)
        {
            container.Add(new CuiElement
            {
                Name = panelName,
                Parent = parentName,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax,
                        OffsetMin = "0 0",
                        OffsetMax = "0 0"
                    }
                }
            });
        }

        private static void UI_AddBlurPanel(CuiElementContainer container, string parentName, string color)
        {
            container.Add(new CuiElement
            {
                Parent = parentName,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = color,
                        Material = "assets/content/ui/uibackgroundblur.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = "0 0",
                        OffsetMax = "0 0",
                    },
                    new CuiNeedsCursorComponent(),
                    new CuiNeedsKeyboardComponent()
                }
            });
        }

        private static void UI_AddVignettePanel(CuiElementContainer container, string parentName, string color)
        {
            container.Add(new CuiElement
            {
                Parent = parentName,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = color,
                        Material = "assets/content/ui/namefontmaterial.mat",
                        Sprite = "assets/content/ui/ui.background.transparent.radial.psd"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = "0 0",
                        OffsetMax = "0 0",
                    }
                }
            });
        }

        private static void UI_AddPanel(CuiElementContainer container, string parentName, string color, string offsetMin, string offsetMax, string anchorMin = "0 0", string anchorMax = "0 0")
        {
            container.Add(new CuiElement
            {
                Parent = parentName,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = color,
                        Material = "assets/content/ui/namefontmaterial.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax,
                        OffsetMin = offsetMin,
                        OffsetMax = offsetMax
                    }
                }
            });
        }

        private static void UI_AddText(CuiElementContainer container, string parentName, string color, string font, string text, TextAnchor textAnchor, int fontSize, string offsetMin, string offsetMax)
        {
            container.Add(new CuiElement
            {
                Parent = parentName,
                Components =
                {
                    new CuiTextComponent
                    {
                        Color = color,
                        Text = text,
                        Align = textAnchor,
                        FontSize = fontSize,
                        Font = font
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = offsetMin,
                        OffsetMax = offsetMax
                    }
                }
            });
        }

        private static void UI_AddButton(CuiElementContainer container, string parentName, string buttonColor, string command, string offsetMin, string offsetMax, string buttonName, string anchorMin = "0 0", string anchorMax = "0 0")
        {
            container.Add(new CuiButton
            {
                Text =
                {
                    Text = "",
                },
                Button =
                {
                    Color = buttonColor,
                    Material = "assets/content/ui/namefontmaterial.mat",
                    Command = command,
                },
                RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax,
                    OffsetMin = offsetMin,
                    OffsetMax = offsetMax
                }
            }, parentName, buttonName);
        }

        private void UI_AddImage(CuiElementContainer container, string parentName, string shortname, ulong skin, string offsetMin, string offsetMax, string color = "1 1 1 1")
        {
            container.Add(new CuiElement
            {
                Parent = parentName,
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = GetImage(shortname, skin),
                        Color = color
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = offsetMin,
                        OffsetMax = offsetMax
                    }
                }
            });
        }

        private void UI_AddItemImage(CuiElementContainer container, string parentName, string shortname, ulong skin, string offsetMin, string offsetMax, string listingName = "")
        {
            if (listingName != "")
            {
                UI_AddImage(container, parentName, listingName, 0, offsetMin, offsetMax);
                return;
            }
            if ((skin != 0 && !config.overrideCustomSkins) || ImageLibrary.Call<bool>("HasImage", shortname, skin))
            {
                UI_AddImage(container, parentName, shortname, skin, offsetMin, offsetMax);
                return;
            }
            ItemDefinition itemDef = ItemManager.FindItemDefinition(shortname);
            if (itemDef == null)
            {
                UI_AddImage(container, parentName, shortname, skin, offsetMin, offsetMax);
                return;
            }
            container.Add(new CuiElement
            {
                Parent = parentName,
                Components =
                {
                    new CuiImageComponent
                    {
                        ItemId = itemDef.itemid,
                        SkinId = skin
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = offsetMin,
                        OffsetMax = offsetMax
                    }
                }
            });
        }

        private static void UI_AddIcon(CuiElementContainer container, string parentName, string color, string offsetMin, string offsetMax, string path)
        {
            container.Add(new CuiElement
            {
                Parent = parentName,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = color,
                        Sprite = path
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = offsetMin,
                        OffsetMax = offsetMax
                    }
                }
            });
        }

        private static void UI_AddInput(CuiElementContainer container, string parentName, string color, string font, TextAnchor textAnchor, int size, string searchValue, int charLimit, string command, string offsetMin, string offsetMax)
        {
            container.Add(new CuiElement
            {
                Parent = parentName,
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        FontSize = size,
                        Align = textAnchor,
                        Color =  color,
                        IsPassword = false,
                        CharsLimit = charLimit,
                        Command = command,
                        Font = font,
                        Text = searchValue
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMax = offsetMax,
                        OffsetMin = offsetMin
                    }
                }
            });
        }

        private string GetImage(string name, ulong skin = 0) => ImageLibrary.Call<string>("GetImage", name, skin);

        private void AddImage(string url, string shortname, ulong skin) => ImageLibrary?.CallHook("AddImage", url, shortname, skin);
    }
}