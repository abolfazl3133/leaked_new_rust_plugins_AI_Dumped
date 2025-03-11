using Oxide.Game.Rust.Cui;
using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using System.IO;
using Newtonsoft.Json;
using Oxide.Core.Configuration;
using System.Collections;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using System.Globalization;
using Facepunch;
using Newtonsoft.Json.Converters;
using System.Threading.Tasks;
using System.Timers;
using Facepunch.Extend;

namespace Oxide.Plugins
{
    [Info("ShopController", "Amino", "1.0.8")]
    [Description("A sleek shop system for Rust")]
    public class ShopController : RustPlugin
    {
        [PluginReference] private Plugin ServerRewards, Economics, ImageLibrary, WelcomeController, NoEscape;

        #region Config
        public static Configuration _config;
        public UIElements _uiColors;
        public class Configuration
        {
            [JsonProperty("Commands")]
            public List<string> Commands { get; set; } = new List<string>();
            [JsonProperty(PropertyName = "Currency (RP, Economics, item.shortname)")]
            public string Currency { get; set; } = "RP";
            [JsonProperty(PropertyName = "Use Cart System")]
            public bool UseCartSystem { get; set; } = true;
            [JsonProperty(PropertyName = "Allow Players to Save Carts")]
            public bool AllowedToSaveCarts { get; set; } = true;
            [JsonProperty(PropertyName = "Commands section enabled")]
            public bool UseCommandsSection { get; set; } = true;
            [JsonProperty(PropertyName = "Sell back enabled")]
            public bool SellBackEnabled { get; set; } = true;
            [JsonProperty(PropertyName = "Clear redeemed items on wipe")]
            public bool ClearDataOnWipe { get; set; } = true;
            [JsonProperty(PropertyName = "Block players from opening shop if raid blocked")]
            public bool RaidblockBlock { get; set; } = true;
            [JsonProperty(PropertyName = "Block players from opening shop if combat blocked")]
            public bool CombatblockBlock { get; set; } = true;
            [JsonProperty(PropertyName = "Plugin images")]
            public Images Images { get; set; } = new Images();
            [JsonProperty(PropertyName = "UI Elements")]
            public UIElements UIElements { get; set; } = new UIElements();
            public Dictionary<string, List<ItemOption>> ItemList = new Dictionary<string, List<ItemOption>>();
            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    Commands = new List<string> { "shop", "s" },
                    Images = new Images
                    {
                        CommandImagePlaceholder = "https://i.ibb.co/MNNw1W1/Buy-Command3.png",
                        SellBackImage = "https://i.ibb.co/CWrwbQH/Money-Bag2.png"
                    }
                };
            }
        }

        public class Images
        {
            [JsonProperty(PropertyName = "Command Image Placeholder")]
            public string CommandImagePlaceholder { get; set; }
            [JsonProperty(PropertyName = "Sell Back Image")]
            public string SellBackImage { get; set; }
        }

        public class UIElements
        {
            public string BlurBackgroundColor { get; set; } = "0 0 0 .4";
            public string MainPanelColor { get; set; } = ".17 .17 .17 1";
            public string ThingBackgroundColor { get; set; } = "1 1 1 .1";
            public string PrimaryButtonColor { get; set; } = ".39 .76 1 .4";
            public string PrimaryButtonTextColor { get; set; } = ".39 .76 1 .6";
            public string SecondaryButtonColor { get; set; } = "0.46 0.46 0.46 .4";
            public string SecondaryButtonTextColor { get; set; } = "0.46 0.46 0.46 .5";
            public string ThirdButtonColor { get; set; } = "0.1 1 0.16 .4";
            public string ThirdButtonTextColor { get; set; } = "0.1 1 0.16 .6";
            public string FourthButtonColor { get; set; } = "1 0.17 0.17 .4";
            public string FourthButtonTextColor { get; set; } = "1 0.17 0.17 .6";
            public string CooldownLabelColor { get; set; } = "0.74 0.23 0.15 1";
            public string CooldownLabelTextColor { get; set; } = "0.99 0.34 0.24 1";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) LoadDefaultConfig();
                _uiColors = _config.UIElements;
                SaveConfig();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                PrintWarning("Creating new config file.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => _config = Configuration.DefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(_config);
        #endregion

        #region Data & Constructors
        public Dictionary<string, List<ItemOption>> _itemList = new Dictionary<string, List<ItemOption>>();
        public Dictionary<ulong, PlayerSettings> _playerSettings = new Dictionary<ulong, PlayerSettings>();
        public List<CommandOption> _commandList = new List<CommandOption>();
        public bool usingWC = false;
        System.Random rnd = new System.Random();
        public static DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        public static double LastWipe;
        public class CommandOption
        {
            public string CommandName { get; set; }
            public string CommandImage { get; set; }
            public int CommandPrice { get; set; }
            public string CommandExpiry { get; set; } = "Never";
            public double CommandCooldown { get; set; }
            public List<string> Commands { get; set; } = new List<string>();
            public bool CommandEnabled { get; set; }
            public int CommandSpecialId { get; set; }
            public CommandOption()
            {
            }
            public CommandOption Clone()
            {
                return new CommandOption
                {
                    CommandName = this.CommandName,
                    CommandImage = this.CommandImage,
                    CommandPrice = this.CommandPrice,
                    CommandExpiry = this.CommandExpiry,
                    CommandCooldown = this.CommandCooldown,
                    CommandEnabled = this.CommandEnabled,
                    CommandSpecialId = this.CommandSpecialId,
                    Commands = new List<string>(this.Commands),
                };
            }
        }

        public class ItemOption
        {
            public int ItemId { get; set; }
            public string Shortname { get; set; }
            public string DisplayName { get; set; }
            public ulong SkinId { get; set; } = 0;
            public bool InUse { get; set; }
            public int Amount { get; set; }
            public int Cost { get; set; }
            public double Cooldown { get; set; }
            public int SpecialId { get; set; }
            public ItemOption()
            {
            }
            public ItemOption(ItemOption other)
            {
                this.ItemId = other.ItemId;
                this.Shortname = other.Shortname;
                this.DisplayName = other.DisplayName;
                this.SkinId = other.SkinId;
                this.InUse = other.InUse;
                this.Amount = other.Amount;
                this.Cost = other.Cost;
                this.Cooldown = other.Cooldown;
                this.SpecialId = other.SpecialId;
            }
            public ItemOption Clone()
            {
                return new ItemOption
                {
                    ItemId = this.ItemId,
                    Shortname = this.Shortname,
                    DisplayName = this.DisplayName,
                    SkinId = this.SkinId,
                    InUse = this.InUse,
                    Amount = this.Amount,
                    Cost = this.Cost,
                    Cooldown = this.Cooldown,
                    SpecialId = this.SpecialId,
                };
            }
        }

        public class CartItem
        {
            public string Text { get; set; }
            public int SpecialId { get; set; }
            public int ItemId { get; set; }
            public int Count { get; set; }
            public int Cost { get; set; }
            public CartItem()
            {
            }
            public CartItem(CartItem other)
            {
                this.Text = other.Text;
                this.SpecialId = other.SpecialId;
                this.Count = other.Count;
                this.Cost = other.Cost;
            }
            public CartItem Clone()
            {
                return new CartItem
                {
                    Text = this.Text,
                    SpecialId = this.SpecialId,
                    Count = this.Count,
                    Cost = this.Cost
                };
            }
        }

        public class ItemChange
        {
            public Item Item { get; set; }
            public int Amount { get; set; }
        }

        public class CanAddItem
        {
            public bool canAdd { get; set; }
            public string Reason { get; set; }
        }

        public class PlayerSettings
        {
            public string Category { get; set; }
            public int CategoryPage = 0;
            public int ItemPage = 0;
            public string EditCategory = "Attire";
            public int EditCategoryPage = 0;
            public int EditItemPage = 0;
            public string EditItemFilter { get; set; }
            public double LastWipe { get; set; }
            public int EditItem = 0;
            public bool WC = false;
            public string ItemFilter { get; set; }
            public ItemOption ItemOption = null;
            public CommandOption CommandOption = null;
            public int CommandsPage = 0;
            public int CommandsEditPage = 0;
            public int CommandsEditSelectPage = 0;
            public int CurrentCartPage = 0;
            public int CurrentSavedCartPage = 0;
            public string CartView = null;
            public string CurrentCart = null;
            public List<CartItem> CurrentCartItems = new List<CartItem>();
            public Dictionary<string, List<CartItem>> Carts = new Dictionary<string, List<CartItem>>();
            public List<RedeemedItems> RedeemedItems = new List<RedeemedItems>();
            public List<RedeemedItems> RedeemedCommands = new List<RedeemedItems>();
        }
        public class RedeemedItems
        {
            public int SpecialId { get; set; }
            public double Cooldown { get; set; }
        }

        public class MethodResponse
        {
            public bool CanBuy { get; set; }
            public string Title { get; set; }
            public double Amount { get; set; }
        }
        #endregion

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Close"] = "CLOSE",
                ["StoreName"] = "SHOP",
                ["NoPermission"] = "You do not have permisson to use this command",
                ["FreeButton"] = "FREE!",
                ["AddButton"] = "ADD",
                ["CartLabel"] = "CART",
                ["BuyCart"] = "PURCHASE CART",
                ["CartOptions"] = "CART OPTIONS",
                ["SaveCart"] = "SAVE CART",
                ["SavedCarts"] = "SAVED CARTS",
                ["ItemsButton"] = "ITEMS",
                ["CommandsButton"] = "COMMANDS",
                ["BuySavedCart"] = "BUY ${0}",
                ["Load"] = "LOAD",
                ["Override"] = "OVERRIDE",
                ["Delete"] = "DELETE",
                ["SuccessfulItemPurchase"] = "Successfully purchased <color=#4f8aff>{0}x</color> <color=#82acff>{1}</color> for <color=#4f8aff>${2}</color>!",
                ["NotEnoughRoom"] = "Not enough space in your inventory to purchase!",
                ["NotEnoughCurrency"] = "You do not have enough <color=#4f8aff>{0}</color> to purchase this!",
                ["CommandPrice"] = "COST",
                ["CommandExpiry"] = "EXPIRES",
                ["CommandPurchase"] = "PURCHASE",
                ["InsufficientFunds"] = "Insufficient funds!",
                ["CurrencyNotFound"] = "Could not find currency, please message admins.",
                ["SuccessfullyPurchasedCommand"] = "You have successfully purchased <color=#4f8aff>{0}</color> for <color=#4f8aff>${1}</color>!",
                ["SuccessSaveCart"] = "Successfully saved your cart!",
                ["SuccessDeletedCart"] = "Successfully deleted your cart!",
                ["SuccessOverrodeCart"] = "Successfully overrode your cart!",
                ["SuccessLoadedCart"] = "Successfully loaded your cart!",
                ["SellBackButton"] = "SELL",
                ["RPPlaceholder"] = "RP: {0}",
                ["EcomonicsPlaceholder"] = "${0}",
                ["ItemsPlaceholder"] = "${0}",
                ["SoldBackToShop"] = "Item successfully sold back to shop!",
                ["CombatRaidblocked"] = "You are currently combat / raidblocked and cannot access the shop!"
            }, this, "en");
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        #endregion

        #region Hooks
        private void Unload()
        {
            if (!Interface.Oxide.IsShuttingDown)
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(player, "SOCMainPanel");
                    SavePlayerSettings(player);
                }

            SaveCommandList();
            _config = null;
        }

        void OnServerSave()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                SavePlayerSettings(player);
            }
        }

        void OnServerInitialized(bool initial)
        {
            var isUsingWC = Interface.Call("IsUsingPlugin", "ShopController");
            if (isUsingWC != null && (isUsingWC is bool)) usingWC = (bool)isUsingWC;
            else usingWC = false;

            RegisterCommandsAndPermissions();
            GenerateCommandList();
            RegisterImages();
            if ((Steamworks.SteamInventory.Definitions?.Length ?? 0) == 0) Steamworks.SteamInventory.OnDefinitionsUpdated += StartItemRequest;
            else StartItemRequest();
            LastWipe = SaveRestore.SaveCreatedTime.Subtract(Epoch).TotalSeconds;
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null || !player.userID.Get().IsSteamId()) return;
            SavePlayerSettings(player);
            _playerSettings.Remove(player.userID);

        }

        void OnWCRequestedUIPanel(BasePlayer player, string panelName, string neededPlugin)
        {
            if (!neededPlugin.Equals("ShopController", StringComparison.OrdinalIgnoreCase)) return;
            usingWC = true;
            CMDOpenShop(player, null, null);
        }

        void OnPluginLoaded(Plugin plugin)
        {
            if (plugin.Name == "WelcomeController")
            {
                var isUsingWC = Interface.Call("IsUsingPlugin", "ShopController");
                if (isUsingWC != null && (isUsingWC is bool)) usingWC = (bool)isUsingWC;
                else usingWC = false;
            }
        }

        void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin.Name == "WelcomeController")
            {
                usingWC = false;
                RegisterCommandsAndPermissions();
            }
        }
        #endregion

        #region Commands
        [ConsoleCommand("soc_main")]
        private void CMDSocMain(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            var playerSettings = GetPlayerSettings(player);
            switch (arg.Args[0])
            {
                case "close":
                    CuiHelper.DestroyUi(player, "SOCMainPanel");
                    break;
                case "itempage":
                    playerSettings.ItemPage = int.Parse(arg.Args[1]);
                    UIGenerateShopItems(player, playerSettings);
                    break;
                case "itemfilter":
                    string joinedString = String.Join(" ", arg.Args.Skip(1));
                    bool isOnlyLetters = joinedString.All(c => char.IsLetter(c) || c == ' ');
                    if (isOnlyLetters && joinedString != "SEARCH") playerSettings.ItemFilter = joinedString;
                    else playerSettings.ItemFilter = null;
                    playerSettings.CommandsPage = 0;
                    playerSettings.ItemPage = 0;
                    UIGenerateShopSearch(player, playerSettings);
                    UIGenerateShopItems(player, playerSettings);
                    UIGenerateShopCategories(player, playerSettings);
                    break;
                case "buyitem":
                    var theItem = _itemList.SelectMany(x => x.Value).Where(x => x.SpecialId == int.Parse(arg.Args[1])).Select(x => new ItemOption(x)).FirstOrDefault();

                    PurchaseItem(player, playerSettings, theItem);
                    UIGenerateShopItems(player, playerSettings);
                    UIGenerateShopSearch(player, playerSettings);
                    break;
                case "category":
                    playerSettings.Category = arg.Args[1];
                    UIGenerateShopItems(player, playerSettings);
                    UIGenerateShopCategories(player, playerSettings);
                    break;
                case "categorypage":
                    playerSettings.CategoryPage = int.Parse(arg.Args[1]);
                    UIGenerateShopCategories(player, playerSettings);
                    break;
                case "additem":
                    var specialId = int.Parse(arg.Args[1]);
                    var cost = int.Parse(arg.Args[2]);
                    var itemId = int.Parse(arg.Args[3]);
                    var inCart = playerSettings.CurrentCartItems.FirstOrDefault(x => x.SpecialId == specialId);
                    if (inCart != null) inCart.Count++;
                    else playerSettings.CurrentCartItems.Add(new CartItem { Count = 1, SpecialId = specialId, Cost = cost, ItemId = itemId });
                    UIGenerateCart(player, playerSettings);
                    break;
                case "itempanel":
                    UIGenerateTopBar(player, playerSettings, false);
                    UIGenerateShopItems(player, playerSettings);
                    UIGenerateShopCategories(player, playerSettings);
                    break;
                case "commandpanel":
                    UIGenerateShopCategories(player, playerSettings, true);
                    UIGenerateCommands(player, playerSettings);
                    break;
                case "commandspage":
                    playerSettings.CommandsPage = int.Parse(arg.Args[1]);
                    UIGenerateShopCategories(player, playerSettings, true);
                    UIGenerateCommands(player, playerSettings);
                    break;
                case "buycommand":
                    var theCommand = _commandList.FirstOrDefault(x => x.CommandSpecialId == int.Parse(arg.Args[1]));

                    BuyCommand(player, playerSettings, theCommand);
                    UIGenerateShopItems(player, playerSettings);
                    UIGenerateShopSearch(player, playerSettings);
                    break;
                case "sellback":
                    theItem = _itemList.SelectMany(x => x.Value).FirstOrDefault(x => x.SpecialId == int.Parse(arg.Args[1]));
                    if (theItem == null) return;

                    var canSell = SellBack(player, theItem);
                    if (canSell.CanBuy)
                    {
                        SendReply(player, Lang("SoldBackToShop"));
                        UICreatePopup(player, Lang("SoldBackToShop"), true);
                    }
                    else
                    {
                        SendReply(player, canSell.Title);
                        UICreatePopup(player, canSell.Title, false);
                    }
                    UIGenerateShopSearch(player, playerSettings);
                    break;
            }
        }

        [ConsoleCommand("soc_cart")]
        private void CMDSocCart(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            List<string> dontOpenFor = new List<string> { "view" };

            var playerSettings = GetPlayerSettings(player);
            switch (arg.Args[0])
            {
                case "removeitem":
                    var specialId = int.Parse(arg.Args[1]);
                    var theItem = playerSettings.CurrentCartItems.FirstOrDefault(x => x.SpecialId == specialId);
                    if (theItem != null)
                    {
                        theItem.Count--;
                        if (theItem.Count <= 0) playerSettings.CurrentCartItems.Remove(theItem);
                    }
                    break;
                case "deleteitem":
                    specialId = int.Parse(arg.Args[1]);
                    playerSettings.CurrentCartItems.RemoveAll(x => x.SpecialId == specialId);
                    break;
                case "page":
                    playerSettings.CurrentCartPage = int.Parse(arg.Args[1]);
                    break;
                case "save":
                    playerSettings.Carts.Add($"{rnd.Next(1, 9999999)}", playerSettings.CurrentCartItems.Select(x => new CartItem(x)).ToList());
                    UICreatePopup(player, Lang("SuccessSaveCart"), true);
                    break;
                case "savedpage":
                    playerSettings.CurrentSavedCartPage = int.Parse(arg.Args[1]);
                    break;
                case "view":
                    playerSettings.CartView = arg.Args[1];
                    break;
                case "closeview":
                    playerSettings.CartView = null;
                    break;
                case "buycart":
                    PurchaseCart(player, playerSettings);
                    UIGenerateShopItems(player, playerSettings);
                    UIGenerateShopSearch(player, playerSettings);
                    break;
                case "load":
                    playerSettings.CurrentCartItems = playerSettings.Carts[playerSettings.CartView].Select(x => new CartItem(x)).ToList();
                    playerSettings.CurrentCart = playerSettings.CartView;
                    playerSettings.CartView = null;
                    UICreatePopup(player, Lang("SuccessLoadedCart"), true);
                    break;
                case "override":
                    playerSettings.Carts[playerSettings.CartView] = playerSettings.CurrentCartItems.Select(x => new CartItem(x)).ToList();
                    playerSettings.CartView = null;
                    UICreatePopup(player, Lang("SuccessOverrodeCart"), true);
                    break;
                case "delete":
                    playerSettings.Carts.Remove(playerSettings.CartView);
                    playerSettings.CurrentSavedCartPage = 0;
                    playerSettings.CartView = null;
                    UICreatePopup(player, Lang("SuccessDeletedCart"), true);
                    break;
            }

            UIGenerateCart(player, playerSettings, arg.Args[0] == "view");
        }

        [ConsoleCommand("soc_edit")]
        private void CMDSocEdit(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            var playerSettings = GetPlayerSettings(player);
            switch (arg.Args[0])
            {
                case "close":
                    CMDOpenShop(player, null, null);
                    break;
                case "open":
                    UIGenerateTopBar(player, playerSettings, true);
                    CuiHelper.DestroyUi(player, "SOCShopMiddlePanelOverlay");
                    CuiHelper.DestroyUi(player, "SOCShopBottomPanelOverlay");

                    playerSettings.ItemOption = _itemList["Attire"].First();
                    playerSettings.EditCategory = "Attire";
                    playerSettings.EditItem = playerSettings.ItemOption.ItemId;

                    UIGenerateEditPage(player, playerSettings);
                    break;
                case "category":
                    playerSettings.EditCategory = arg.Args[1];
                    playerSettings.ItemOption = _itemList[playerSettings.EditCategory].First();
                    playerSettings.EditItemPage = 0;
                    playerSettings.EditItem = playerSettings.ItemOption.ItemId;

                    UIGenerateEditItems(player, playerSettings);
                    UIGenerateItemEdit(player, playerSettings);
                    UIGenerateEditCategories(player, playerSettings);
                    break;
                case "categorypage":
                    playerSettings.EditCategoryPage = int.Parse(arg.Args[1]);
                    UIGenerateEditCategories(player, playerSettings);
                    break;
                case "item":
                    var neededItems = _itemList.SelectMany(x => x.Value);
                    if (string.IsNullOrEmpty(playerSettings.EditItemFilter)) neededItems = _itemList[playerSettings.EditCategory];

                    playerSettings.ItemOption = neededItems.FirstOrDefault(x => x.ItemId == int.Parse(arg.Args[1]));
                    if (playerSettings.ItemOption == null) return;
                    playerSettings.EditItem = playerSettings.ItemOption.ItemId;
                    UIGenerateEditItems(player, playerSettings);
                    UIGenerateItemEdit(player, playerSettings);
                    break;
                case "itempage":
                    playerSettings.EditItemPage = int.Parse(arg.Args[1]);
                    UIGenerateEditItems(player, playerSettings);
                    break;
                case "itemfilter":
                    if (arg.Args.Length < 2) playerSettings.EditItemFilter = null;
                    else
                    {
                        string joinedString = String.Join(" ", arg.Args.Skip(1));
                        bool isOnlyLetters = joinedString.All(c => char.IsLetter(c) || c == ' ');
                        if (isOnlyLetters) playerSettings.EditItemFilter = joinedString;
                    }

                    UIGenerateEditItems(player, playerSettings);
                    UIGenerateEditCategories(player, playerSettings);
                    break;
                case "displayname":
                    playerSettings.ItemOption.DisplayName = arg.Args.Length > 1 ? string.Join(" ", arg.Args.Skip(1)) : playerSettings.ItemOption.DisplayName;
                    UIGenerateEditItems(player, playerSettings);
                    UIGenerateItemEdit(player, playerSettings);
                    break;
                case "enabled":
                    if (playerSettings.ItemOption.InUse) playerSettings.ItemOption.InUse = false;
                    else playerSettings.ItemOption.InUse = true;
                    UIGenerateItemEdit(player, playerSettings);

                    bool en = playerSettings.ItemOption.InUse;
                    UICreatePopup(player, $"Successfully {(en ? "enabled" : "disabled")} item", en ? true : false);

                    break;
                case "amount":
                    if (arg.Args.Length > 1 && !string.IsNullOrEmpty(arg.Args[1]))
                    {
                        if (int.TryParse(arg.Args[1], out int amount))
                        {
                            if (amount < 1) playerSettings.ItemOption.Amount = 1;
                            else playerSettings.ItemOption.Amount = amount;
                        }
                    }

                    UIGenerateItemEdit(player, playerSettings);
                    break;
                case "cooldown":
                    if (arg.Args.Length > 1 && !string.IsNullOrEmpty(arg.Args[1]))
                    {
                        if (double.TryParse(arg.Args[1], out double amount))
                        {
                            if (amount < 1) playerSettings.ItemOption.Cooldown = 0;
                            else playerSettings.ItemOption.Cooldown = amount;
                        }
                    }

                    UIGenerateItemEdit(player, playerSettings);
                    break;
                case "skinid":
                    if (arg.Args.Length > 1 && !string.IsNullOrEmpty(arg.Args[1]))
                    {
                        if (ulong.TryParse(arg.Args[1], out ulong skinid))
                        {
                            if (skinid < 1) playerSettings.ItemOption.SkinId = 0;
                            else playerSettings.ItemOption.SkinId = skinid;
                        }
                    }

                    UIGenerateItemEdit(player, playerSettings);
                    break;
                case "cost":
                    if (arg.Args.Length > 1 && !string.IsNullOrEmpty(arg.Args[1]))
                    {
                        if (int.TryParse(arg.Args[1], out int cost))
                        {
                            if (cost < 1) playerSettings.ItemOption.SkinId = 0;
                            else playerSettings.ItemOption.Cost = cost;
                        }
                    }

                    UIGenerateItemEdit(player, playerSettings);
                    break;
                case "itempanel":
                    UIGenerateEditItems(player, playerSettings);
                    UIGenerateItemEdit(player, playerSettings);
                    UIGenerateEditCategories(player, playerSettings);
                    break;
                case "commandpanel":
                    playerSettings.CommandOption = null;
                    UIGenerateEditCommands(player, playerSettings);
                    UIGenerateCommandEdit(player, playerSettings);
                    UIGenerateEditCategories(player, playerSettings, true);
                    break;
            }

            SaveConfig();
        }

        [ConsoleCommand("soc_editcommand")]
        private void CMDSocEditCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            var playerSettings = GetPlayerSettings(player);
            switch (arg.Args[0])
            {
                case "close":
                    CMDOpenShop(player, null, null);
                    break;
                case "selectcommand":
                    var foundCommand = _commandList.FirstOrDefault(x => x.CommandSpecialId == int.Parse(arg.Args[1])).Clone();
                    playerSettings.CommandOption = foundCommand;
                    if (!string.IsNullOrEmpty(playerSettings.CommandOption.CommandImage)) RegisterNewImage($"EditCommand{playerSettings.CommandOption.CommandName}", playerSettings.CommandOption.CommandImage);

                    UIGenerateCommandEdit(player, playerSettings);
                    UIGenerateEditCommands(player, playerSettings);
                    break;
                case "altercommand":
                    var joinedString = string.Join(" ", arg.Args.Skip(2));
                    var index = int.Parse(arg.Args[1]);

                    if (string.IsNullOrEmpty(arg.Args[2])) playerSettings.CommandOption.Commands.RemoveAt(index);
                    else playerSettings.CommandOption.Commands[index] = joinedString;
                    UIGenerateCommandEdit(player, playerSettings);
                    break;
                case "addcommand":
                    joinedString = string.Join(" ", arg.Args.Skip(1));

                    if (!string.IsNullOrEmpty(joinedString) && joinedString != "INSERT COMMAND") playerSettings.CommandOption.Commands.Add(joinedString);
                    UIGenerateCommandEdit(player, playerSettings);
                    break;
                case "addnewcommand":
                    playerSettings.CommandOption = new CommandOption();

                    UIGenerateEditCommands(player, playerSettings);
                    UIGenerateCommandEdit(player, playerSettings);
                    break;
                case "deletecommand":
                    playerSettings.CommandOption.Commands.RemoveAt(int.Parse(arg.Args[1]));
                    UIGenerateCommandEdit(player, playerSettings);
                    break;
                case "deletesavedcommand":
                    _commandList.RemoveAll(x => x.CommandSpecialId == playerSettings.CommandOption.CommandSpecialId);
                    playerSettings.CommandOption = null;

                    UICreatePopup(player, "Successfully deleted command", false);

                    UIGenerateEditCommands(player, playerSettings);
                    UIGenerateCommandEdit(player, playerSettings);
                    break;
                case "page":
                    playerSettings.CommandsEditPage = int.Parse(arg.Args[1]);
                    UIGenerateCommandEdit(player, playerSettings);
                    break;
                case "commandpage":
                    playerSettings.CommandsEditSelectPage = int.Parse(arg.Args[1]);
                    UIGenerateEditCommands(player, playerSettings);
                    break;
                case "displayname":
                    playerSettings.CommandOption.CommandName = String.Join(" ", arg.Args.Skip(1));
                    UIGenerateCommandEdit(player, playerSettings);
                    break;
                case "savecommand":
                    if (playerSettings.CommandOption.CommandSpecialId == 0)
                    {
                        var cmdClone = playerSettings.CommandOption.Clone();
                        cmdClone.CommandSpecialId = rnd.Next(1, 9999999);
                        playerSettings.CommandOption.CommandSpecialId = cmdClone.CommandSpecialId;
                        _commandList.Add(cmdClone);
                    }
                    else _commandList[_commandList.FindIndex(x => x.CommandSpecialId == playerSettings.CommandOption.CommandSpecialId)] = playerSettings.CommandOption.Clone();
                    RegisterNewImage($"Command{playerSettings.CommandOption.CommandName}", playerSettings.CommandOption.CommandImage);

                    timer.Once(.1f, () => { UIGenerateCommandEdit(player, playerSettings); });
                    UIGenerateEditCommands(player, playerSettings);

                    UICreatePopup(player, "Successfully saved command", true);
                    break;
                case "image":
                    if (arg.Args.Length < 2) playerSettings.CommandOption.CommandImage = null;
                    else
                    {
                        playerSettings.CommandOption.CommandImage = arg.Args[1];
                        RegisterNewImage($"EditCommand{playerSettings.CommandOption.CommandName}", arg.Args[1]);
                        timer.Once(.2f, () => { UIGenerateCommandEdit(player, playerSettings); });
                    }
                    break;
                case "expiry":
                    playerSettings.CommandOption.CommandExpiry = String.Join(" ", arg.Args.Skip(1));
                    UIGenerateCommandEdit(player, playerSettings);
                    break;
                case "price":
                    if (arg.Args.Length > 1 && !string.IsNullOrEmpty(arg.Args[1]))
                    {
                        if (int.TryParse(arg.Args[1], out int amount))
                        {
                            if (amount < 1) playerSettings.CommandOption.CommandPrice = 0;
                            else playerSettings.CommandOption.CommandPrice = amount;
                        }
                    }

                    UIGenerateCommandEdit(player, playerSettings);
                    break;
                case "cooldown":
                    if (arg.Args.Length > 1 && !string.IsNullOrEmpty(arg.Args[1]))
                    {
                        if (int.TryParse(arg.Args[1], out int amount))
                        {
                            if (amount < 1) playerSettings.CommandOption.CommandCooldown = 0;
                            else playerSettings.CommandOption.CommandCooldown = amount;
                        }
                    }

                    UIGenerateCommandEdit(player, playerSettings);
                    break;
            }
        }
        #endregion

        #region UI
        private bool IsBlocked(BasePlayer player)
        {
            if (NoEscape != null)
            {
                if (_config.RaidblockBlock && NoEscape.Call<bool>("IsRaidBlocked", player.UserIDString)) return true;
                if (_config.CombatblockBlock && NoEscape.Call<bool>("IsCombatBlocked", player.UserIDString)) return true;
            }
            return false;
        }

        private void CMDOpenShop(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, "shopcontroller.use"))
            {
                SendReply(player, Lang("NoPermission"));
                return;
            }

            if (IsBlocked(player))
            {
                SendReply(player, Lang("CombatRaidblocked", player.UserIDString));
                if (usingWC) CuiHelper.DestroyUi(player, "BackgrounPanel");
                return;
            }

            var playerSettings = GetPlayerSettings(player);
            if (string.IsNullOrEmpty(playerSettings.Category)) playerSettings.Category = _itemList.FirstOrDefault(x => x.Value.Any(y => y.InUse)).Key;

            var container = new CuiElementContainer();
            CreatePanel(ref container, "0 0", "1 1", usingWC ? "0 0 0 0" : _uiColors.BlurBackgroundColor, usingWC ? "WCSourcePanel" : "Overlay", "SOCMainPanel", true, true);
            CreatePanel(ref container, usingWC ? "0 0" : ".15 .1", usingWC ? "1 1" : ".85 .9", "0 0 0 0", "SOCMainPanel", "SOCShopPanel");
            UIOpenShop(player, ref container, playerSettings);
        }

        private void UIOpenShop(BasePlayer player, ref CuiElementContainer container, PlayerSettings playerSettings)
        {
            CreatePanel(ref container, "0 .9", "1 1", _uiColors.MainPanelColor, "SOCShopPanel", "SOCShopTopPanel");
            CreatePanel(ref container, "0 .11", "1 .89", _uiColors.MainPanelColor, "SOCShopPanel", "SOCShopMiddlePanel");
            CreatePanel(ref container, "0 0", "1 .1", _uiColors.MainPanelColor, "SOCShopPanel", "SOCShopBottomPanel");
            CreatePanel(ref container, "0 -.08", "1 -.01", _uiColors.MainPanelColor, "SOCShopPanel", "SOCShopSearchPanel");

            CuiHelper.DestroyUi(player, "SOCMainPanel");
            CuiHelper.AddUi(player, container);

            UIGenerateTopBar(player, playerSettings, false);
            UIGenerateShopItems(player, playerSettings);
            UIGenerateShopCategories(player, playerSettings);
            UIGenerateShopSearch(player, playerSettings);
        }

        void UIGenerateEditCommands(BasePlayer player, PlayerSettings playerSettings)
        {
            var container = new CuiElementContainer();
            var panel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "SOCShopEditItemsPanel", "SOCShopEditItemsPanelOverlay");

            var maxPage = _commandList.Count / 10;
            if (playerSettings.CommandsEditSelectPage > maxPage) playerSettings.CommandsEditSelectPage = 0;
            if (playerSettings.CommandsEditSelectPage < 0) playerSettings.CommandsEditSelectPage = maxPage;

            var pageItems = _commandList.Skip(9 * playerSettings.CommandsEditSelectPage).Take(9);

            if (_commandList.Count != 0 && playerSettings.CommandOption == null) playerSettings.CommandOption = _commandList.First().Clone();

            int i = 0;
            foreach (var command in pageItems)
            {
                var isButton = playerSettings.CommandOption != null && playerSettings.CommandOption.CommandSpecialId == command.CommandSpecialId;
                CreateButton(ref container, $".019 {.9052 - (i * .0908)}", $".975 {.985 - (i * .0908)}", isButton ? _uiColors.PrimaryButtonColor : _uiColors.SecondaryButtonColor, isButton ? _uiColors.PrimaryButtonTextColor : _uiColors.SecondaryButtonTextColor, command.CommandName, 20, $"soc_editcommand selectcommand {command.CommandSpecialId}", panel);
                i++;
            }

            if (_commandList.Count > 9)
            {
                CreateButton(ref container, ".019 .11", ".49 .168", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, "<", 20, $"soc_editcommand commandpage {playerSettings.CommandsEditSelectPage - 1}", panel);
                CreateButton(ref container, ".51 .11", ".975 .168", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, ">", 20, $"soc_editcommand commandpage {playerSettings.CommandsEditSelectPage + 1}", panel);
            }
            else
            {
                CreatePanel(ref container, ".019 .11", ".49 .17", _uiColors.SecondaryButtonColor, panel);
                CreatePanel(ref container, ".51 .11", ".975 .17", _uiColors.SecondaryButtonColor, panel);
            }

            CreateButton(ref container, ".019 .01", ".975 .098", _uiColors.PrimaryButtonColor, _uiColors.PrimaryButtonTextColor, "CREATE NEW COMMAND", 20, "soc_editcommand addnewcommand", panel);

            CuiHelper.DestroyUi(player, "SOCShopEditItemsPanelOverlay");
            CuiHelper.AddUi(player, container);
        }

        void UIGenerateCommandEdit(BasePlayer player, PlayerSettings playerSettings)
        {
            if (playerSettings.CommandOption == null) playerSettings.CommandOption = new CommandOption();
            var commandOption = playerSettings.CommandOption;

            var container = new CuiElementContainer();
            var panel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "SOCShopItemEditPanel", "SOCShopItemEditPanelOverlay");

            CreateLabel(ref container, ".008 .93", ".989 .985", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, "COMMAND DISPLAY NAME", 15, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".008 .77", ".989 .92", "soc_editcommand displayname", _uiColors.ThingBackgroundColor, "1 1 1 1", String.IsNullOrEmpty(commandOption.CommandName) ? " " : commandOption.CommandName, 30, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".504 .705", ".7425 .76", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, "EXPIRY (STRING)", 15, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".504 .6", ".7425 .695", "soc_editcommand expiry", _uiColors.ThingBackgroundColor, "1 1 1 .1", String.IsNullOrEmpty(commandOption.CommandExpiry) ? " " : commandOption.CommandExpiry, 15, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".7505 .705", ".989 .76", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, "PRICE", 15, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".7505 .6", ".989 .695", "soc_editcommand price", _uiColors.ThingBackgroundColor, "1 1 1 .1", $"{commandOption.CommandPrice}", 15, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".504 .535", ".7425 .59", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, "IMAGE", 15, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".504 .43", ".7425 .525", "soc_editcommand image", _uiColors.ThingBackgroundColor, "1 1 1 .1", String.IsNullOrEmpty(commandOption.CommandImage) ? " " : commandOption.CommandImage, 15, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".7505 .535", ".989 .59", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, "COOLDOWN (SEC)", 15, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".7505 .43", ".989 .525", "soc_editcommand cooldown", _uiColors.ThingBackgroundColor, "1 1 1 .1", $"{commandOption.CommandCooldown}", 15, TextAnchor.MiddleCenter, panel);

            var imgPanel = CreatePanel(ref container, ".504 .01", ".989 .42", _uiColors.ThingBackgroundColor, panel);
            CreateImagePanel(ref container, ".225 .05", ".775 .95", String.IsNullOrEmpty(playerSettings.CommandOption.CommandImage) ? GetImage("CommandPlaceholder") : GetImage($"EditCommand{playerSettings.CommandOption.CommandName}"), imgPanel);

            CreateButton(ref container, ".008 .01", ".237 .098", _uiColors.ThirdButtonColor, _uiColors.ThirdButtonTextColor, "SAVE COMMAND", 18, "soc_editcommand savecommand", panel);
            CreateButton(ref container, ".247 .01", ".494 .098", _uiColors.FourthButtonColor, _uiColors.FourthButtonTextColor, "DELETE COMMAND", 18, "soc_editcommand deletesavedcommand", panel);

            CreateLabel(ref container, ".008 .705", ".496 .76", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, "COMMANDS TO RUN | VARS: <color=#e27aff>{id}</color> <color=#a77aff>{name}</color> <color=#907aff>{x}</color> <color=#7aa0ff>{y}</color> <color=#7ac5ff>{z}</color>", 13, TextAnchor.MiddleCenter, panel);

            var maxPage = playerSettings.CommandOption.Commands.Count / 5;
            if (playerSettings.CommandsEditPage > maxPage) playerSettings.CommandsEditPage = 0;
            if (playerSettings.CommandsEditPage < 0) playerSettings.CommandsEditPage = maxPage;

            var pageItems = playerSettings.CommandOption.Commands.Skip(5 * playerSettings.CommandsEditPage).Take(5);

            int i = 0;
            foreach (var command in pageItems)
            {
                var inputPnl = CreateInput(ref container, $".008 {.6 - (i * .105)}", $".496 {.695 - (i * .105)}", $"soc_editcommand altercommand {i}", _uiColors.ThingBackgroundColor, "1 1 1 .1", command, 15, TextAnchor.MiddleCenter, panel);
                CreateButton(ref container, ".01 0", ".12 1", "0 0 0 0", "1 1 1 .15", "X", 20, $"soc_editcommand deletecommand {i}", inputPnl);
                i++;
            }

            if (pageItems.Count() < 5) CreateInput(ref container, $".008 {.6 - (i * .105)}", $".496 {.695 - (i * .105)}", "soc_editcommand addcommand", _uiColors.ThingBackgroundColor, "1 1 1 .1", "INSERT COMMAND", 15, TextAnchor.MiddleCenter, panel);

            if (playerSettings.CommandOption.Commands.Count > 4)
            {
                CreateButton(ref container, ".008 .11", ".237 .17", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, "<", 15, $"soc_editcommand page {playerSettings.CommandsEditPage - 1}", panel);
                CreateButton(ref container, ".247 .11", ".494 .17", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, ">", 15, $"soc_editcommand page {playerSettings.CommandsEditPage + 1}", panel);
            }
            else
            {
                CreatePanel(ref container, ".008 .11", ".239 .17", _uiColors.SecondaryButtonColor, panel);
                CreatePanel(ref container, ".247 .11", ".496 .17", _uiColors.SecondaryButtonColor, panel);
            }

            CuiHelper.DestroyUi(player, "SOCShopItemEditPanelOverlay");
            CuiHelper.AddUi(player, container);
        }

        private void UIGenerateTopBar(BasePlayer player, PlayerSettings playerSettings, bool isEditPage = false)
        {
            var isAdmin = permission.UserHasPermission(player.UserIDString, "shopcontroller.admin");
            var container = new CuiElementContainer();
            CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "SOCShopTopPanel", "SOCShopTopPanelOverlay");
            if (!usingWC) CreateButton(ref container, ".9 0", "1 .95", "0 0 0 0", "1 1 1 1", "X", 40, "soc_main close", "SOCShopTopPanelOverlay");
            if (isAdmin)
            {
                if (isEditPage) CreateButton(ref container, ".005 .08", ".15 .87", _uiColors.ThingBackgroundColor, "1 1 1 1", "BACK", 20, "soc_edit close", "SOCShopTopPanelOverlay");
                else CreateButton(ref container, ".005 .08", ".15 .87", _uiColors.ThingBackgroundColor, "1 1 1 1", "EDIT", 20, "soc_edit open", "SOCShopTopPanelOverlay");
            }

            CreateLabel(ref container, ".35 .08", ".65 .9", "0 0 0 0", "1 1 1 1", Lang("StoreName"), 40, TextAnchor.MiddleCenter, "SOCShopTopPanelOverlay");

            CuiHelper.DestroyUi(player, "SOCShopTopPanelOverlay");
            CuiHelper.AddUi(player, container);
        }

        void UIGenerateEditPage(BasePlayer player, PlayerSettings playerSettings)
        {
            var container = new CuiElementContainer();
            var panel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "SOCShopMiddlePanel", "SOCShopMiddlePanelOverlay");

            CreatePanel(ref container, ".006 .01", ".3 .985", _uiColors.ThingBackgroundColor, panel, "SOCShopEditItemsPanel");
            CreatePanel(ref container, ".305 .01", ".993 .985", _uiColors.ThingBackgroundColor, panel, "SOCShopItemEditPanel");

            CuiHelper.DestroyUi(player, "SOCShopMiddlePanelOverlay");
            CuiHelper.AddUi(player, container);

            UIGenerateEditItems(player, playerSettings);
            UIGenerateItemEdit(player, playerSettings);
            UIGenerateEditCategories(player, playerSettings);
            UIGenerateEditShopSearch(player, playerSettings);
        }

        void UIGenerateItemEdit(BasePlayer player, PlayerSettings playerSettings)
        {
            var container = new CuiElementContainer();
            var panel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "SOCShopItemEditPanel", "SOCShopItemEditPanelOverlay");

            CreateLabel(ref container, ".008 .93", ".989 .985", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, "ITEM DISPLAY NAME", 15, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".008 .77", ".989 .92", "soc_edit displayname", _uiColors.ThingBackgroundColor, "1 1 1 1", playerSettings.ItemOption.DisplayName, 30, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".008 .705", ".496 .76", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, "ITEM ID", 15, TextAnchor.MiddleCenter, panel);
            CreateLabel(ref container, ".008 .56", ".496 .695", _uiColors.ThingBackgroundColor, "1 1 1 1", $"{playerSettings.ItemOption.ItemId}", 15, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".504 .705", ".989 .76", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, "ITEM SHORTNAME", 15, TextAnchor.MiddleCenter, panel);
            CreateLabel(ref container, ".504 .56", ".989 .695", _uiColors.ThingBackgroundColor, "1 1 1 1", playerSettings.ItemOption.Shortname, 15, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".008 .495", ".496 .55", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, "ITEM ENABLED", 15, TextAnchor.MiddleCenter, panel);
            var enabledPanel = CreatePanel(ref container, ".008 .35", ".496 .485", _uiColors.ThingBackgroundColor, panel);
            CreateButton(ref container, playerSettings.ItemOption.InUse ? ".505 .1" : ".02 .1", playerSettings.ItemOption.InUse ? ".974 .9" : ".495 .9", playerSettings.ItemOption.InUse ? _uiColors.ThirdButtonColor : _uiColors.FourthButtonColor, "1 1 1 1", " ", 15, "soc_edit enabled", enabledPanel);

            CreateLabel(ref container, ".008 .285", ".246 .34", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, "ITEM SKIN ID", 15, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".008 .16", ".246 .275", "soc_edit skinid", _uiColors.ThingBackgroundColor, "1 1 1 1", $"{playerSettings.ItemOption.SkinId}", 20, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".254 .285", ".496 .34", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, "ITEM COOLDOWN (SEC)", 15, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".254 .16", ".496 .275", "soc_edit cooldown", _uiColors.ThingBackgroundColor, "1 1 1 1", $"{playerSettings.ItemOption.Cooldown}", 20, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".008 .095", ".246 .15", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, "ITEM COST", 15, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".008 .01", ".246 .085", "soc_edit cost", _uiColors.ThingBackgroundColor, "1 1 1 1", $"{playerSettings.ItemOption.Cost}", 20, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".254 .095", ".496 .15", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, "ITEM AMOUNT", 15, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".254 .01", ".496 .085", "soc_edit amount", _uiColors.ThingBackgroundColor, "1 1 1 1", $"{playerSettings.ItemOption.Amount}", 20, TextAnchor.MiddleCenter, panel);

            CreatePanel(ref container, ".504 .01", ".989 .55", _uiColors.ThingBackgroundColor, panel);
            CreateItemPanel(ref container, ".55 .01", ".939 .55", .05f, "0 0 0 0", playerSettings.ItemOption.ItemId, panel, skinId: playerSettings.ItemOption.SkinId);

            CuiHelper.DestroyUi(player, "SOCShopItemEditPanelOverlay");
            CuiHelper.AddUi(player, container);
        }

        void UIGenerateEditItems(BasePlayer player, PlayerSettings playerSettings)
        {
            var container = new CuiElementContainer();
            var panel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "SOCShopEditItemsPanel", "SOCShopEditItemsPanelOverlay");

            CreateInput(ref container, ".02 .91", ".975 .985", "soc_edit itemfilter", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, string.IsNullOrEmpty(playerSettings.EditItemFilter) ? "SEARCH" : playerSettings.EditItemFilter, 15, TextAnchor.MiddleCenter, panel);

            var activeItems = _itemList[playerSettings.EditCategory ?? "Attire"];

            if (!string.IsNullOrEmpty(playerSettings.EditItemFilter))
                activeItems = _itemList
            .SelectMany(y => y.Value.Where(x => CultureInfo.CurrentCulture.CompareInfo.IndexOf(x.DisplayName, playerSettings.EditItemFilter, CompareOptions.IgnoreCase) >= 0)).ToList();

            var maxPage = (activeItems.Count - 1) / 9;
            if (playerSettings.EditItemPage > maxPage) playerSettings.EditItemPage = 0;
            if (playerSettings.EditItemPage < 0) playerSettings.EditItemPage = maxPage;

            int i = 0;
            int startIndex = playerSettings.EditItemPage * 9;
            int endIndex = Math.Min(startIndex + 9, activeItems.Count);
            for (int index = startIndex; index < endIndex; index++)
            {
                var item = activeItems[index];
                var itemPanel = CreatePanel(ref container, $".02 {.82 - (i * .09)}", $".975 {.9 - (i * .09)}", playerSettings.EditItem == item.ItemId ? _uiColors.PrimaryButtonColor : _uiColors.SecondaryButtonColor, panel);
                CreateItemPanel(ref container, "0 0", ".15 1", .1f, "0 0 0 0", item.ItemId, itemPanel);
                CreateLabel(ref container, ".18 0", "1 1", "0 0 0 0", playerSettings.EditItem == item.ItemId ? _uiColors.PrimaryButtonTextColor : _uiColors.SecondaryButtonTextColor, item.DisplayName, 14, TextAnchor.MiddleLeft, itemPanel);
                CreateButton(ref container, "0 0", "1 1", "0 0 0 0", "1 1 1 1", " ", 15, $"soc_edit item {item.ItemId}", itemPanel);
                i++;
            }

            if (activeItems.Count > 9)
            {
                CreateButton(ref container, ".02 .01", ".49 .089", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, "<", 25, $"soc_edit itempage {playerSettings.EditItemPage - 1}", panel);
                CreateButton(ref container, ".51 .01", ".972 .089", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, ">", 25, $"soc_edit itempage {playerSettings.EditItemPage + 1}", panel);
            }

            CuiHelper.DestroyUi(player, "SOCShopEditItemsPanelOverlay");
            CuiHelper.AddUi(player, container);
        }

        void UIGenerateEditCategories(BasePlayer player, PlayerSettings playerSettings, bool isCommands = false)
        {
            var container = new CuiElementContainer();
            CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "SOCShopBottomPanel", "SOCShopBottomPanelOverlay");

            if (!isCommands)
            {
                var allCategories = _itemList.Keys.ToList();

                var maxPage = (allCategories.Count - 1) / 5;
                if (playerSettings.EditCategoryPage > maxPage) playerSettings.EditCategoryPage = 0;
                if (playerSettings.EditCategoryPage < 0) playerSettings.EditCategoryPage = maxPage;

                var i = 0;
                if (allCategories.Count > 5)
                {
                    CreateButton(ref container, ".007 .1", ".06 .87", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, "<", 25, $"soc_edit categorypage {playerSettings.EditCategoryPage - 1}", "SOCShopBottomPanelOverlay");
                    CreateButton(ref container, ".94 .1", ".99 .87", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, ">", 25, $"soc_edit categorypage {playerSettings.EditCategoryPage + 1}", "SOCShopBottomPanelOverlay");
                }

                int startIndex = playerSettings.EditCategoryPage * 5;
                int endIndex = Math.Min(startIndex + 5, allCategories.Count);
                for (int index = startIndex; index < endIndex; index++)
                {
                    var category = allCategories[index];
                    var colorType = playerSettings.EditCategory.Equals(category, StringComparison.OrdinalIgnoreCase) || !string.IsNullOrEmpty(playerSettings.EditItemFilter);
                    CreateButton(ref container, $"{.068 + (i * .1742)} .1", $"{.2342 + (i * .1742)} .87", colorType ? _uiColors.PrimaryButtonColor : _uiColors.SecondaryButtonColor, colorType ? _uiColors.PrimaryButtonTextColor : _uiColors.SecondaryButtonTextColor, category.ToUpper(), 20, $"soc_edit category {category}", "SOCShopBottomPanelOverlay");
                    i++;
                }
            }
            else CreatePanel(ref container, ".007 .1", ".99 .87", _uiColors.SecondaryButtonColor, "SOCShopBottomPanelOverlay");

            CuiHelper.DestroyUi(player, "SOCShopBottomPanelOverlay");
            CuiHelper.AddUi(player, container);
        }

        void UIGenerateEditShopSearch(BasePlayer player, PlayerSettings playerSettings)
        {
            var container = new CuiElementContainer();
            var panel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "SOCShopSearchPanel", "SOCShopSearchPanelOverlay");

            CreateButton(ref container, ".007 .12", ".496 .81", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, Lang("ItemsButton"), 20, "soc_edit itempanel", panel);
            CreateButton(ref container, ".504 .12", ".99 .81", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, Lang("CommandsButton"), 20, "soc_edit commandpanel", panel);

            CuiHelper.DestroyUi(player, "SOCShopSearchPanelOverlay");
            CuiHelper.AddUi(player, container);
        }

        void UIGenerateShopItems(BasePlayer player, PlayerSettings playerSettings)
        {
            var container = new CuiElementContainer();
            var panel = CreatePanel(ref container, "0 0", "1 1", "1 1 1 0", "SOCShopMiddlePanel", "SOCShopMiddlePanelOverlay");
            if (_config.UseCartSystem) CreatePanel(ref container, ".667 .007", ".995 .99", _uiColors.ThingBackgroundColor, "SOCShopMiddlePanelOverlay", "SOCShopCartPanel");

            var itemList = string.IsNullOrEmpty(playerSettings.Category) ? _itemList.First().Value : _itemList[playerSettings.Category];
            itemList = itemList.Where(x => x.InUse).ToList();

            if (!string.IsNullOrEmpty(playerSettings.ItemFilter))
                itemList = itemList.Where(x => x.DisplayName.Contains(playerSettings.ItemFilter, System.Globalization.CompareOptions.IgnoreCase)).ToList();

            int itemsPerPage = _config.UseCartSystem ? 12 : 18;
            int maxPage = itemList.Count / itemsPerPage;
            if (playerSettings.ItemPage > maxPage) playerSettings.ItemPage = 0;
            if (playerSettings.ItemPage < 0) playerSettings.ItemPage = maxPage;

            var i = 0;
            var buttonTop = .99;
            foreach (var item in itemList.Skip(playerSettings.ItemPage * itemsPerPage).Take(itemsPerPage))
            {
                if (i == (_config.UseCartSystem ? 4 : 6))
                {
                    buttonTop -= .331;
                    i = 0;
                }

                var itemPanel = CreatePanel(ref container, $"{.005 + (i * .1655)} {buttonTop - .321}", $"{.1655 + (i * .1655)} {buttonTop}", _uiColors.ThingBackgroundColor, panel);
                CreateItemPanel(ref container, "0 0", "1 1", .2f, "0 0 0 0", item.ItemId, itemPanel, skinId: item.SkinId);
                CreateLabel(ref container, "0 .85", ".99 .988", _uiColors.ThingBackgroundColor, "1 1 1 1", item.DisplayName, 15, TextAnchor.MiddleCenter, itemPanel);

                if (_config.SellBackEnabled) CreateImageButton(ref container, ".02 .65", ".19 .82", "0 0 0 0", $"soc_main sellback {item.SpecialId}", GetImage("SellBackImage"), itemPanel);

                CreateLabel(ref container, ".05 .2", ".95 .35", "0 0 0 0", "1 1 1 1", $"x{item.Amount}", 17, TextAnchor.MiddleLeft, itemPanel);
                CreateButton(ref container, ".02 .02", _config.UseCartSystem ? ".6 .18" : ".965 .18", _uiColors.PrimaryButtonColor, _uiColors.PrimaryButtonTextColor, item.Cost == 0 ? Lang("FreeButton") : $"${item.Cost}", 17, $"soc_main buyitem {item.SpecialId} {item.Cost} {item.Amount} {item.Cooldown}", itemPanel);
                if (_config.UseCartSystem) CreateButton(ref container, ".63 .02", ".965 .18", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, Lang("AddButton"), 17, $"soc_main additem {item.SpecialId} {item.Cost} {item.ItemId}", itemPanel);

                var isPurchased = playerSettings.RedeemedItems.FirstOrDefault(x => x.SpecialId == item.SpecialId);
                if (isPurchased != null && isPurchased.Cooldown > DateTimeOffset.Now.ToUnixTimeSeconds())
                {
                    CreatePanel(ref container, "0 0", "1 .99", "0 0 0 .2", itemPanel, blur: true);
                    CreateLabel(ref container, ".1 .35", ".9 .65", _uiColors.CooldownLabelColor, _uiColors.CooldownLabelTextColor, $"{TimeSpan.FromSeconds(isPurchased.Cooldown - DateTimeOffset.Now.ToUnixTimeSeconds())}", 20, TextAnchor.MiddleCenter, itemPanel);
                }

                i++;
            }

            if (itemList.Count > itemsPerPage)
            {
                var left = CreatePanel(ref container, "-.04 0", "-.007 .995", _uiColors.MainPanelColor, panel);
                var right = CreatePanel(ref container, "1.006 0", "1.041 .995", _uiColors.MainPanelColor, panel);

                CreateButton(ref container, ".1 .007", ".83 .99", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, "<", 15, $"soc_main itempage {playerSettings.ItemPage - 1}", left);
                CreateButton(ref container, ".13 .007", ".83 .99", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, ">", 15, $"soc_main itempage {playerSettings.ItemPage + 1}", right);
            }

            CuiHelper.DestroyUi(player, "SOCShopMiddlePanelOverlay");
            CuiHelper.AddUi(player, container);

            if (_config.UseCartSystem) UIGenerateCart(player, playerSettings);
        }

        void UIGenerateCart(BasePlayer player, PlayerSettings playerSettings, bool isView = false)
        {
            var container = new CuiElementContainer();

            var panel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "SOCShopCartPanel", "SOCShopCartPanelOverlay");
            CreateLabel(ref container, "0 .95", ".99 .995", _uiColors.ThingBackgroundColor, "1 1 1 1", Lang("CartLabel"), 17, TextAnchor.MiddleCenter, panel);

            UIGenerateCartItems(ref container, playerSettings, panel);
            if (_config.AllowedToSaveCarts) UIGenerateCarts(ref container, playerSettings, panel, isView);

            CuiHelper.DestroyUi(player, "SOCShopCartPanelOverlay");
            CuiHelper.AddUi(player, container);
        }

        void UIGenerateCartItems(ref CuiElementContainer container, PlayerSettings playerSettings, string cartPanel)
        {
            List<CartItem> items = new List<CartItem>();
            int cartCost = 0;
            int itemsOnPage = _config.AllowedToSaveCarts ? 10 : 16;

            int maxPage = (playerSettings.CurrentCartItems.Count - 1) / itemsOnPage;
            if (playerSettings.CurrentCartPage > maxPage) playerSettings.CurrentCartPage = 0;
            if (playerSettings.CurrentCartPage < 0) playerSettings.CurrentCartPage = maxPage;

            var itemList = _itemList.Values.SelectMany(list => list).ToDictionary(item => item.SpecialId, item => item);
            foreach (var item in playerSettings.CurrentCartItems)
            {
                if (itemList.TryGetValue(item.SpecialId, out var foundItem))
                {
                    cartCost += foundItem.Cost * item.Count;
                    items.Add(new CartItem { Text = (foundItem.DisplayName.Length > 17 ? foundItem.DisplayName.Substring(0, 15) + ".." : foundItem.DisplayName), SpecialId = item.SpecialId, Count = item.Count * foundItem.Amount, ItemId = foundItem.ItemId });

                    if (playerSettings.CurrentCart != null)
                    {
                        var cartItem = playerSettings.Carts[playerSettings.CurrentCart].FindIndex(x => x.SpecialId == item.SpecialId);
                        if (cartItem > -1) playerSettings.Carts[playerSettings.CurrentCart][cartItem].Cost = foundItem.Cost;
                    }
                }
            }

            int i = 0;
            var buttonLeft = .02;
            foreach (var item in items.Skip(playerSettings.CurrentCartPage * itemsOnPage).Take(itemsOnPage))
            {
                if (i == itemsOnPage / 2)
                {
                    buttonLeft = .51;
                    i = 0;
                }

                var itemPanel = CreatePanel(ref container, $"{buttonLeft} {.84 - (i * .1)}", $"{buttonLeft + .47} {.93 - (i * .1)}", _uiColors.ThingBackgroundColor, cartPanel);
                CreateItemPanel(ref container, "0 0", ".3 1", .1f, "1 1 1 0", item.ItemId, itemPanel);
                CreateLabel(ref container, ".32 .5", "1 .9", "0 0 0 0", "1 1 1 1", item.Text, 13, TextAnchor.MiddleLeft, itemPanel);
                CreateLabel(ref container, ".32 .1", ".9 .5", "0 0 0 0", "1 1 1 1", $"x{item.Count}", 13, TextAnchor.MiddleLeft, itemPanel);

                CreateButton(ref container, ".85 .05", ".96 .45", _uiColors.PrimaryButtonColor, _uiColors.PrimaryButtonTextColor, "X", 13, $"soc_cart deleteitem {item.SpecialId}", itemPanel);
                CreateButton(ref container, ".71 .05", ".825 .45", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, "-", 13, $"soc_cart removeitem {item.SpecialId}", itemPanel);

                i++;
            }

            if (playerSettings.CurrentCartItems.Count > itemsOnPage)
            {
                CreateButton(ref container, ".02 .38", ".327 .427", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, "<", 13, $"soc_cart page {playerSettings.CurrentCartPage - 1}", cartPanel);
                CreateButton(ref container, ".67 .38", ".978 .427", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, ">", 13, $"soc_cart page {playerSettings.CurrentCartPage + 1}", cartPanel);
                CreateLabel(ref container, ".35 .38", ".65 .43", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, $"{playerSettings.CurrentCartPage + 1} / {maxPage + 1}", 13, TextAnchor.MiddleCenter, cartPanel);
            }
            else CreatePanel(ref container, ".02 .38", ".98 .43", _uiColors.ThingBackgroundColor, cartPanel);

            CreateButton(ref container, ".02 .32", ".327 .367", _uiColors.PrimaryButtonColor, _uiColors.PrimaryButtonTextColor, Lang("SaveCart"), 11, "soc_cart save", cartPanel);
            CreateButton(ref container, ".35 .32", ".648 .367", _uiColors.ThirdButtonColor, _uiColors.ThirdButtonTextColor, Lang("BuyCart"), 11, "soc_cart buycart", cartPanel);
            CreateLabel(ref container, ".672 .32", ".98 .37", _uiColors.ThingBackgroundColor, "1 1 1 1", $"${cartCost}", 13, TextAnchor.MiddleCenter, cartPanel);
        }

        void UIGenerateCarts(ref CuiElementContainer container, PlayerSettings playerSettings, string cartPanel, bool isView = false)
        {
            CreateLabel(ref container, ".02 .27", ".98 .31", _uiColors.ThingBackgroundColor, "1 1 1 1", Lang("SavedCarts"), 13, TextAnchor.MiddleCenter, cartPanel);

            if (!isView)
            {
                int maxPage = (playerSettings.Carts.Count - 1) / 3;
                if (playerSettings.CurrentSavedCartPage > maxPage) playerSettings.CurrentSavedCartPage = 0;
                if (playerSettings.CurrentSavedCartPage < 0) playerSettings.CurrentSavedCartPage = maxPage;

                int i = 0;
                foreach (var cart in playerSettings.Carts.Skip(playerSettings.CurrentSavedCartPage * 3).Take(3))
                {
                    var cartCost = cart.Value.Sum(x => x.Count * (x.Cost == 0 ? 1 : x.Cost));
                    var theLabel = CreateLabel(ref container, $".02 {.2 - (i * .07)}", $".98 {.26 - (i * .07)}", _uiColors.ThingBackgroundColor, "1 1 1 1", $"  {Lang("CART")} {i + (playerSettings.CurrentSavedCartPage * 3)}", 13, TextAnchor.MiddleLeft, cartPanel);
                    CreateButton(ref container, ".7 .1", ".98 .82", _uiColors.ThirdButtonColor, _uiColors.ThirdButtonTextColor, Lang("BuySavedCart", null, cart.Value.Sum(x => (x.Count * (x.Cost == 0 ? 1 : x.Cost)))), 10, $"soc_cart buycart {cart.Key}", theLabel);
                    CreateButton(ref container, ".62 .1", ".68 .82", _uiColors.PrimaryButtonColor, _uiColors.PrimaryButtonTextColor, "i", 13, $"soc_cart view {cart.Key}", theLabel);
                    i++;
                }

                if (playerSettings.Carts.Count > 3)
                {
                    CreateButton(ref container, ".02 .01", ".327 .047", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, "<", 13, $"soc_cart savedpage {playerSettings.CurrentSavedCartPage - 1}", cartPanel);
                    CreateButton(ref container, ".67 .01", ".978 .047", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, ">", 13, $"soc_cart savedpage {playerSettings.CurrentSavedCartPage + 1}", cartPanel);
                    CreateLabel(ref container, ".35 .01", ".65 .05", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, $"{playerSettings.CurrentSavedCartPage + 1} / {maxPage + 1}", 13, TextAnchor.MiddleCenter, cartPanel);
                }
                else CreatePanel(ref container, ".02 .01", ".98 .05", _uiColors.ThingBackgroundColor, cartPanel);
            }
            else
            {
                CreateLabel(ref container, ".02 .203", ".98 .26", _uiColors.ThingBackgroundColor, "1 1 1 1", Lang("CartOptions"), 13, TextAnchor.MiddleCenter, cartPanel);
                CreateButton(ref container, ".02 .11", ".31 .19", _uiColors.ThirdButtonColor, _uiColors.ThirdButtonTextColor, Lang("Load"), 13, "soc_cart load", cartPanel);
                CreateButton(ref container, ".33 .11", ".66 .19", _uiColors.PrimaryButtonColor, _uiColors.PrimaryButtonTextColor, Lang("Override"), 13, "soc_cart override", cartPanel);
                CreateButton(ref container, ".68 .11", ".98 .19", _uiColors.FourthButtonColor, _uiColors.FourthButtonTextColor, Lang("Delete"), 13, "soc_cart delete", cartPanel);
                CreateButton(ref container, ".02 .01", ".978 .1", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, Lang("Close"), 15, "soc_cart closeview", cartPanel);
            }

        }

        void UIGenerateCommands(BasePlayer player, PlayerSettings playerSettings)
        {
            var container = new CuiElementContainer();
            var panel = CreatePanel(ref container, "0 0", "1 1", "1 1 1 0", "SOCShopMiddlePanel", "SOCShopMiddlePanelOverlay");

            int maxPage = (_commandList.Count - 1) / 6;
            if (playerSettings.CommandsPage > maxPage) playerSettings.CommandsPage = 0;
            if (playerSettings.CommandsPage < 0) playerSettings.CommandsPage = maxPage;

            var i = 0;
            var buttonTop = .985;
            foreach (var command in _commandList.Skip(playerSettings.CommandsPage * 6).Take(6))
            {
                if (i == 2)
                {
                    i = 0;
                    buttonTop -= .33;
                }

                var commandPanel = CreatePanel(ref container, $"{.006 + (i * .497)} {buttonTop - .315}", $"{.494 + (i * .497)} {buttonTop}", _uiColors.ThingBackgroundColor, panel);
                CreateImagePanel(ref container, ".013 .04", ".318 .96", string.IsNullOrEmpty(command.CommandImage) ? GetImage("CommandPlaceholder") : GetImage($"Command{command.CommandName}"), commandPanel);
                CreateLabel(ref container, ".33 .7", ".985 .96", _uiColors.ThingBackgroundColor, "1 1 1 1", command.CommandName, 20, TextAnchor.MiddleCenter, commandPanel);

                CreateLabel(ref container, ".33 .57", ".6535 .68", _uiColors.ThingBackgroundColor, "1 1 1 1", Lang("CommandPrice"), 11, TextAnchor.MiddleCenter, commandPanel);
                CreateLabel(ref container, ".6615 .57", ".985 .68", _uiColors.ThingBackgroundColor, "1 1 1 1", Lang("CommandExpiry"), 11, TextAnchor.MiddleCenter, commandPanel);

                CreateLabel(ref container, ".33 .27", ".6535 .55", _uiColors.ThingBackgroundColor, "1 1 1 1", $"${command.CommandPrice}", 15, TextAnchor.MiddleCenter, commandPanel);
                CreateLabel(ref container, ".6615 .27", ".985 .55", _uiColors.ThingBackgroundColor, "1 1 1 1", command.CommandExpiry, 15, TextAnchor.MiddleCenter, commandPanel);

                CreateButton(ref container, ".33 .04", ".982 .245", _uiColors.ThirdButtonColor, _uiColors.ThirdButtonTextColor, Lang("CommandPurchase"), 18, $"soc_main buycommand {command.CommandSpecialId}", commandPanel);

                var isPurchased = playerSettings.RedeemedCommands.FirstOrDefault(x => x.SpecialId == command.CommandSpecialId);
                if (isPurchased != null && isPurchased.Cooldown > DateTimeOffset.Now.ToUnixTimeSeconds())
                {
                    CreatePanel(ref container, "0 0", "1 .99", "0 0 0 .2", commandPanel, blur: true);
                    CreateLabel(ref container, ".1 .35", ".9 .65", _uiColors.CooldownLabelColor, _uiColors.CooldownLabelTextColor, $"{TimeSpan.FromSeconds(isPurchased.Cooldown - DateTimeOffset.Now.ToUnixTimeSeconds())}", 20, TextAnchor.MiddleCenter, commandPanel);
                }
                i++;
            }

            CuiHelper.DestroyUi(player, "SOCShopMiddlePanelOverlay");
            CuiHelper.AddUi(player, container);
        }

        void UIGenerateShopCategories(BasePlayer player, PlayerSettings playerSettings, bool isCommands = false)
        {
            var container = new CuiElementContainer();
            CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "SOCShopBottomPanel", "SOCShopBottomPanelOverlay");

            if (!isCommands)
            {
                var validCategories = new List<string>();

                foreach (var category in _itemList)
                {
                    foreach (var item in category.Value)
                    {
                        if (item.InUse)
                        {
                            validCategories.Add(category.Key);
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(playerSettings.Category)) playerSettings.Category = validCategories.Count != 0 ? validCategories.First() : null;


                var maxPage = (validCategories.Count - 1) / 5;
                if (playerSettings.CategoryPage > maxPage) playerSettings.CategoryPage = 0;
                if (playerSettings.CategoryPage < 0) playerSettings.CategoryPage = maxPage;

                var i = 0;
                if (validCategories.Count > 5)
                {
                    CreateButton(ref container, ".007 .1", ".06 .87", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, "<", 25, $"soc_main categorypage {playerSettings.CategoryPage - 1}", "SOCShopBottomPanelOverlay");
                    CreateButton(ref container, ".94 .1", ".99 .87", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, ">", 25, $"soc_main categorypage {playerSettings.CategoryPage + 1}", "SOCShopBottomPanelOverlay");
                }
                else
                {
                    CreatePanel(ref container, ".007 .1", ".06 .89", _uiColors.SecondaryButtonColor, "SOCShopBottomPanelOverlay");
                    CreatePanel(ref container, ".94 .1", ".992 .89", _uiColors.SecondaryButtonColor, "SOCShopBottomPanelOverlay");
                }

                int startIndex = playerSettings.CategoryPage * 5;
                int endIndex = Math.Min(startIndex + 5, validCategories.Count);
                for (int index = startIndex; index < endIndex; index++)
                {
                    var category = validCategories[index];
                    CreateButton(ref container, $"{.068 + (i * .1742)} .1", $"{.2342 + (i * .1742)} .87", playerSettings.Category == category || !string.IsNullOrEmpty(playerSettings.ItemFilter) ? _uiColors.PrimaryButtonColor : _uiColors.SecondaryButtonColor, playerSettings.Category == category || !string.IsNullOrEmpty(playerSettings.ItemFilter) ? _uiColors.PrimaryButtonTextColor : _uiColors.SecondaryButtonTextColor, category.ToUpper(), 20, $"soc_main category {category}", "SOCShopBottomPanelOverlay");
                    i++;
                }
            }
            else
            {
                if (_commandList.Count > 6)
                {
                    CreateButton(ref container, ".007 .1", ".495 .87", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, "<", 25, $"soc_main commandspage {playerSettings.CommandsPage - 1}", "SOCShopBottomPanelOverlay");
                    CreateButton(ref container, ".505 .1", ".99 .87", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, ">", 25, $"soc_main commandspage {playerSettings.CommandsPage + 1}", "SOCShopBottomPanelOverlay");
                }
                else CreatePanel(ref container, ".007 .1", ".99 .87", _uiColors.SecondaryButtonColor, "SOCShopBottomPanelOverlay");
            }

            CuiHelper.DestroyUi(player, "SOCShopBottomPanelOverlay");
            CuiHelper.AddUi(player, container);
        }

        void UIGenerateShopSearch(BasePlayer player, PlayerSettings playerSettings, bool isCommands = false)
        {
            var container = new CuiElementContainer();
            var panel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "SOCShopSearchPanel", "SOCShopSearchPanelOverlay");

            var shopValue = GetShopValue(player);
            string valueMsg = null;
            switch (shopValue.Title)
            {
                case "RP":
                    valueMsg = Lang("RPPlaceholder", null, shopValue.Amount);
                    break;
                case "Eco":
                    valueMsg = Lang("EcomonicsPlaceholder", null, shopValue.Amount);
                    break;
                default:
                    valueMsg = Lang("ItemsPlaceholder", null, shopValue.Amount);
                    break;
            }

            CreateLabel(ref container, ".007 .12", isCommands ? ".496 .85" : ".246 .85", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, valueMsg, 20, TextAnchor.MiddleCenter, panel);

            if (!isCommands) CreateInput(ref container, ".254 .12", ".496 .85", "soc_main itemfilter", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, string.IsNullOrEmpty(playerSettings.ItemFilter) ? "SEARCH" : playerSettings.ItemFilter, 20, TextAnchor.MiddleCenter, panel);
            CreateButton(ref container, ".504 .12", _config.UseCommandsSection ? ".746 .81" : ".99 .81", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, Lang("ItemsButton"), 20, "soc_main itempanel", panel);
            if (_config.UseCommandsSection) CreateButton(ref container, ".754 .12", ".99 .81", _uiColors.SecondaryButtonColor, _uiColors.SecondaryButtonTextColor, Lang("CommandsButton"), 20, "soc_main commandpanel", panel);

            CuiHelper.DestroyUi(player, "SOCShopSearchPanelOverlay");
            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region Methods
        MethodResponse GetShopValue(BasePlayer player)
        {
            if (_config.Currency.Equals("RP", StringComparison.OrdinalIgnoreCase))
            {
                var point = ServerRewards?.Call("CheckPoints", (ulong)player.userID);
                return new MethodResponse { Title = "RP", Amount = (point == null ? 0 : double.Parse($"{point}")) };
            }

            if (_config.Currency.Equals("Economics", StringComparison.OrdinalIgnoreCase))
            {
                var point = Economics?.Call("Balance", (ulong)player.userID);
                return new MethodResponse { Title = "Eco", Amount = (point == null ? 0 : double.Parse($"{point}")) };
            }

            var theItem = ItemManager.CreateByName(_config.Currency);
            if (theItem == null) return new MethodResponse { Title = "N/A", Amount = 0 };

            List<Item> itemList = Pool.Get<List<Item>>();
            player.inventory.GetAllItems(itemList);

            var inventoryItemCount = itemList.Where(x => x.info.shortname.Equals(theItem.info.shortname)).Sum(x => x.amount);

            itemList.Clear();
            Pool.FreeUnsafe(ref itemList);

            return new MethodResponse { Title = theItem.info.displayName.english, Amount = inventoryItemCount };
        }

        MethodResponse SellBack(BasePlayer player, ItemOption theItem)
        {
            List<ItemChange> needsEditedList = new List<ItemChange>();
            List<ItemChange> needsRemovedList = new List<ItemChange>();

            var theItems = new List<Item>();

            List<Item> itemList = Pool.Get<List<Item>>();
            player.inventory.GetAllItems(itemList);

            foreach (var item in itemList)
                if (item.info.shortname == theItem.Shortname)
                {
                    theItems.Add(item);
                }

            if (theItems.Sum(x => x.amount) < theItem.Amount) return new MethodResponse { CanBuy = false, Title = Lang("InsufficientFunds") };

            var amountLeft = theItem.Amount;
            foreach (var item in theItems)
            {
                if (amountLeft >= item.amount)
                {
                    needsRemovedList.Add(new ItemChange { Amount = item.amount, Item = item });
                    amountLeft -= item.amount;
                }

                if (amountLeft != 0 && amountLeft < item.amount)
                {
                    needsEditedList.Add(new ItemChange { Amount = item.amount - amountLeft, Item = item });
                    amountLeft = 0;
                }

                if (amountLeft == 0) break;
            }

            foreach (var item in needsRemovedList)
            {
                item.Item.GetHeldEntity()?.Kill();
                item.Item.Remove();
            }

            foreach (var item in needsEditedList)
            {
                item.Item.amount = item.Amount;
                item.Item.MarkDirty();
            }

            var shopValue = GetShopValue(player);
            var currency = shopValue.Title;
            if (_config.Currency.Equals("RP", StringComparison.OrdinalIgnoreCase))
            {
                ServerRewards?.Call("AddPoints", (ulong)player.userID, theItem.Cost);
                return new MethodResponse { CanBuy = true };
            }

            if (_config.Currency.Equals("Economics", StringComparison.OrdinalIgnoreCase))
            {
                Economics?.Call("Deposit", (ulong)player.userID, (double)theItem.Cost);
                return new MethodResponse { CanBuy = true };
            }

            var foundItem = ItemManager.FindItemDefinition(currency);
            if (foundItem == null)
            {
                return new MethodResponse { CanBuy = false, Title = "Item not found (Contact admin)" };
            }

            var createdItem = ItemManager.CreateByItemID(foundItem.itemid, theItem.Cost);
            if (!player.inventory.containerMain.IsFull())
            {
                createdItem.MoveToContainer(player.inventory.containerMain, ignoreStackLimit: true);
                return new MethodResponse { CanBuy = true };
            }

            if (!player.inventory.containerBelt.IsFull())
            {
                createdItem.MoveToContainer(player.inventory.containerMain, -1, true, true);
                return new MethodResponse { CanBuy = true };
            }

            createdItem.Drop(player.transform.position, Vector3.zero);

            itemList.Clear();
            Pool.FreeUnsafe(ref itemList);

            return new MethodResponse { CanBuy = true };
        }
        MethodResponse CanBuy(BasePlayer player, int neededAmount, bool isSellBack = false)
        {
            var shopValue = GetShopValue(player);
            if (shopValue.Title == "N/A") return new MethodResponse { CanBuy = false, Title = Lang("CurrencyNotFound") };

            var currentAmount = shopValue.Amount;
            var currency = shopValue.Title;

            if (currentAmount - neededAmount < 0) return new MethodResponse { CanBuy = false, Title = Lang("InsufficientFunds") };
            if (neededAmount == 0) return new MethodResponse { CanBuy = true };

            if (currency.Equals("RP", StringComparison.OrdinalIgnoreCase))
            {
                ServerRewards?.Call("TakePoints", (ulong)player.userID, neededAmount);
                if (neededAmount == 0) return new MethodResponse { CanBuy = true };
            }

            if (currency.Equals("Eco", StringComparison.OrdinalIgnoreCase))
            {
                Economics?.Call("Withdraw", (ulong)player.userID, (double)neededAmount);
                if (neededAmount == 0) return new MethodResponse { CanBuy = true };
            }

            List<ItemChange> needsEditedList = new List<ItemChange>();
            List<ItemChange> needsRemovedList = new List<ItemChange>();
            int amountLeft = neededAmount;

            List<Item> itemList = Pool.Get<List<Item>>();
            player.inventory.GetAllItems(itemList);

            foreach (var item in itemList.Where(x => x.info.shortname.Equals(currency, StringComparison.OrdinalIgnoreCase)))
            {
                if (amountLeft >= item.amount)
                {
                    needsRemovedList.Add(new ItemChange { Amount = item.amount, Item = item });
                    amountLeft -= item.amount;
                }
                else if (amountLeft < item.amount)
                {
                    needsEditedList.Add(new ItemChange { Amount = item.amount - amountLeft, Item = item });
                    amountLeft = 0;
                }
            }

            foreach (var item in needsRemovedList)
            {
                item.Item.GetHeldEntity()?.Kill();
                item.Item.Remove();
            }

            foreach (var item in needsEditedList)
            {
                item.Item.amount = item.Amount;
                item.Item.MarkDirty();
            }

            itemList.Clear();
            Pool.FreeUnsafe(ref itemList);
            return new MethodResponse { CanBuy = true };
        }

        void BuyCommand(BasePlayer player, PlayerSettings playerSettings, CommandOption theCommand)
        {
            var purchasedCommand = playerSettings.RedeemedCommands.FirstOrDefault(x => x.SpecialId == theCommand.CommandSpecialId);
            if (purchasedCommand != null && purchasedCommand.Cooldown > DateTimeOffset.Now.ToUnixTimeSeconds()) return;

            var canBuy = CanBuy(player, theCommand.CommandPrice);
            if (!canBuy.CanBuy)
            {
                SendReply(player, canBuy.Title);
                UICreatePopup(player, canBuy.Title, false);
                return;
            }

            foreach (var command in theCommand.Commands)
            {
                var alteredCommand = command.Replace("{id}", player.UserIDString).Replace("{name}", player.displayName).Replace("{x}", $"{player.ServerPosition.x}")
                    .Replace("{y}", $"{player.ServerPosition.y}").Replace("{z}", $"{player.ServerPosition.z}");
                Server.Command(alteredCommand);
            }

            if (purchasedCommand == null) playerSettings.RedeemedCommands.Add(new RedeemedItems { SpecialId = theCommand.CommandSpecialId, Cooldown = theCommand.CommandCooldown + DateTimeOffset.Now.ToUnixTimeSeconds() });
            else purchasedCommand.Cooldown = theCommand.CommandCooldown + DateTimeOffset.Now.ToUnixTimeSeconds();

            SendReply(player, Lang("SuccessfullyPurchasedCommand", null, theCommand.CommandName, theCommand.CommandPrice));
            UICreatePopup(player, Lang("SuccessfullyPurchasedCommand", null, theCommand.CommandName, theCommand.CommandPrice), false);
        }

        void UICreatePopup(BasePlayer player, string msg, bool isGood)
        {
            var container = new CuiElementContainer();
            var panel = CreatePanel(ref container, usingWC ? "0 .02" : ".15 .91", usingWC ? ".22 .12" : ".851 .975", _uiColors.MainPanelColor, usingWC ? "WCColorPanel" : "SOCMainPanel", "SOCCreateErr");
            CreatePanel(ref container, "0 .015", ".003 .98", isGood ? "0.3 0.86 0.17 1" : "0.86 0.17 0.17 1", panel);
            CreateLabel(ref container, "0 0", "1 1", "0 0 0 0", "1 1 1 1", msg, 20, TextAnchor.MiddleCenter, panel);

            CuiHelper.DestroyUi(player, "SOCCreateErr");
            CuiHelper.AddUi(player, container);
            timer.Once(2, () => CuiHelper.DestroyUi(player, "SOCCreateErr"));
        }

        void PurchaseCart(BasePlayer player, PlayerSettings playerSettings, string currentCart = null)
        {
            var currentCartItems = currentCart != null ? playerSettings.Carts[currentCart] : playerSettings.CurrentCartItems;
            var updatedItemList = new Dictionary<ItemOption, bool>();

            var allItems = _itemList.SelectMany(x => x.Value).Where(x => x.InUse).ToDictionary(x => x.SpecialId);
            var currentTime = DateTimeOffset.Now.ToUnixTimeSeconds();

            var redeemedItemsLookup = playerSettings.RedeemedItems.ToDictionary(x => x.SpecialId);

            foreach (var item in currentCartItems)
            {
                if (!allItems.TryGetValue(item.SpecialId, out var foundItem)) return;

                if (redeemedItemsLookup.TryGetValue(item.SpecialId, out var isPurchased) && isPurchased.Cooldown > currentTime) continue;

                updatedItemList.Add(new ItemOption
                {
                    Amount = foundItem.Amount * item.Count,
                    ItemId = foundItem.ItemId,
                    SpecialId = foundItem.SpecialId,
                    Cost = foundItem.Cost * item.Count,
                    SkinId = foundItem.SkinId,
                    Cooldown = foundItem.Cooldown
                }, isPurchased != null);
            }

            var shopValue = GetShopValue(player);
            if (shopValue.Title == "N/A")
            {
                SendReply(player, Lang("CurrencyNotFound", player.UserIDString));
                return;
            }

            var currentAmount = shopValue.Amount;
            var currency = shopValue.Title;
            var neededAmount = currentCartItems.Sum(x => x.Cost);

            if (currentAmount < neededAmount)
            {
                SendReply(player, Lang("InsufficientFunds", player.UserIDString));
                return;
            }

            int totalSlotsNeeded = updatedItemList.Sum(x => (int)Math.Ceiling((double)x.Key.Amount / ItemManager.CreateByItemID(x.Key.ItemId).MaxStackable()));
            int slotsOpen = player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count;

            if (totalSlotsNeeded > slotsOpen)
            {
                SendReply(player, Lang("NotEnoughRoom", player.UserIDString));
                return;
            }

            List<ItemChange> needsEditedList = new List<ItemChange>();
            List<ItemChange> needsAddedList = new List<ItemChange>();

            List<Item> itemList = Pool.Get<List<Item>>();
            player.inventory.GetAllItems(itemList);

            foreach (var theItem in updatedItemList)
            {
                var itemsInInventory = itemList.Where(x => x.info.itemid == theItem.Key.ItemId).ToList();

                foreach (var item in itemsInInventory)
                {
                    var difference = item.MaxStackable() - item.amount;
                    if (difference <= 0) continue;

                    if (difference > theItem.Key.Amount)
                    {
                        needsEditedList.Add(new ItemChange { Item = item, Amount = theItem.Key.Amount });
                        theItem.Key.Amount = 0;
                    }
                    else
                    {
                        needsEditedList.Add(new ItemChange { Item = item, Amount = difference });
                        theItem.Key.Amount -= difference;
                    }
                }

                while (theItem.Key.Amount > 0 && slotsOpen > 0)
                {
                    var itemInfo = ItemManager.CreateByItemID(theItem.Key.ItemId);
                    var maxStack = itemInfo.MaxStackable();
                    int amountToAdd = Math.Min(maxStack, theItem.Key.Amount);
                    needsAddedList.Add(new ItemChange { Item = itemInfo, Amount = amountToAdd });
                    theItem.Key.Amount -= amountToAdd;
                    slotsOpen--;
                }

                if (theItem.Key.Amount > 0)
                {
                    SendReply(player, Lang("NotEnoughRoom", player.UserIDString));
                    return;
                }
            }

            DeductCurrency(player, currency, neededAmount);

            ApplyInventoryChanges(player, needsEditedList, needsAddedList);

            UICreatePopup(player, "Successfully purchased cart", true);

            UpdateRedeemedItems(playerSettings, updatedItemList);
            itemList.Clear();

            Pool.FreeUnsafe(ref itemList);
        }

        void DeductCurrency(BasePlayer player, string currency, int neededAmount)
        {
            if (currency.Equals("RP", StringComparison.OrdinalIgnoreCase))
            {
                ServerRewards?.Call("TakePoints", (ulong)player.userID, neededAmount);
            }
            else if (currency.Equals("Eco", StringComparison.OrdinalIgnoreCase))
            {
                Economics?.Call("Withdraw", (ulong)player.userID, (double)neededAmount);
            }
            else
            {

                List<Item> itemList = Pool.Get<List<Item>>();
                player.inventory.GetAllItems(itemList);

                int amountLeft = neededAmount;
                foreach (var item in itemList.Where(x => x.info.shortname.Equals(currency, StringComparison.OrdinalIgnoreCase)))
                {
                    if (amountLeft <= 0) break;

                    int deductionAmount = Math.Min(item.amount, amountLeft);
                    amountLeft -= deductionAmount;
                    item.amount -= deductionAmount;

                    if (item.amount <= 0) item.Remove();
                    else item.MarkDirty();
                }
                itemList.Clear();

                Pool.FreeUnsafe(ref itemList);
            }
        }

        void ApplyInventoryChanges(BasePlayer player, List<ItemChange> needsEditedList, List<ItemChange> needsAddedList)
        {
            foreach (var item in needsEditedList)
            {
                item.Item.amount += item.Amount;
                item.Item.MarkDirty();
            }

            foreach (var item in needsAddedList)
            {
                var createdItem = ItemManager.CreateByItemID(item.Item.info.itemid);
                createdItem.amount = item.Amount;
                createdItem.MoveToContainer(player.inventory.containerMain);
            }
        }

        void UpdateRedeemedItems(PlayerSettings playerSettings, Dictionary<ItemOption, bool> updatedItemList)
        {
            var currentTime = DateTimeOffset.Now.ToUnixTimeSeconds();
            foreach (var item in updatedItemList)
            {
                if (!item.Value)
                    playerSettings.RedeemedItems.Add(new RedeemedItems { Cooldown = item.Key.Cooldown + currentTime, SpecialId = item.Key.SpecialId });
                else
                    playerSettings.RedeemedItems.FirstOrDefault(x => x.SpecialId == item.Key.SpecialId).Cooldown = item.Key.Cooldown + currentTime;
            }
        }

        void PurchaseItem(BasePlayer player, PlayerSettings playerSettings, ItemOption theItem)
        {
            var isPurchased = playerSettings.RedeemedItems.FirstOrDefault(x => x.SpecialId == theItem.SpecialId);

            if (isPurchased != null && isPurchased.Cooldown > DateTimeOffset.Now.ToUnixTimeSeconds()) return;

            var canHold = CheckInvAndBuy(player, new List<ItemOption> { theItem }, playerSettings);
            if (canHold.canAdd)
            {
                RedeemedItems redeemedItem = isPurchased;
                if (isPurchased == null) playerSettings.RedeemedItems.Add(new RedeemedItems { Cooldown = theItem.Cooldown + DateTimeOffset.Now.ToUnixTimeSeconds(), SpecialId = theItem.SpecialId });
                else isPurchased.Cooldown = theItem.Cooldown + DateTimeOffset.Now.ToUnixTimeSeconds();

                UIGenerateShopItems(player, playerSettings);
                UICreatePopup(player, Lang("SuccessfulItemPurchase", null, theItem.Amount, theItem.DisplayName, theItem.Cost), true);
            }
            else UICreatePopup(player, canHold.Reason, false);
        }

        private CanAddItem CheckInvAndBuy(BasePlayer player, List<ItemOption> theItems, PlayerSettings playerSettings)
        {
            theItems = theItems.Select(x => new ItemOption(x)).ToList();

            List<Item> itemList = Pool.Get<List<Item>>();
            player.inventory.GetAllItems(itemList);

            var inventoryContainsItem = itemList.Where(x => theItems.Any(y => y.ItemId == x.info.itemid));
            List<ItemChange> needsEditedList = new List<ItemChange>();
            List<ItemChange> needsAddedList = new List<ItemChange>();

            foreach (var theItem in theItems)
            {
                if (inventoryContainsItem != null)
                {
                    foreach (var item in inventoryContainsItem.Where(x => x.info.itemid == theItem.ItemId))
                    {
                        var difference = item.MaxStackable() - item.amount;
                        if (difference <= 0) continue;

                        if (difference > theItem.Amount)
                        {
                            needsEditedList.Add(new ItemChange { Item = item, Amount = theItem.Amount });
                            theItem.Amount = 0;
                        }
                        else
                        {
                            needsEditedList.Add(new ItemChange { Item = item, Amount = difference });
                            theItem.Amount -= difference;
                        }
                    }
                }

                if (theItem.Amount > 0)
                {
                    var slotsOpen = player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count;
                    if (slotsOpen <= 0) return new CanAddItem { canAdd = false, Reason = Lang("NotEnoughRoom") };

                    var itemInfo = ItemManager.CreateByItemID(theItem.ItemId);
                    var maxStack = itemInfo.MaxStackable();

                    var amountOfNeededSlots = theItem.Amount / maxStack;
                    if (amountOfNeededSlots > slotsOpen) return new CanAddItem { canAdd = false, Reason = Lang("NotEnoughRoom") };

                    for (int i = 0; i < slotsOpen; i++)
                    {
                        if (maxStack > theItem.Amount)
                        {
                            needsAddedList.Add(new ItemChange { Item = itemInfo, Amount = theItem.Amount });
                            theItem.Amount = 0;
                        }
                        else
                        {
                            needsAddedList.Add(new ItemChange { Item = itemInfo, Amount = maxStack });
                            theItem.Amount -= maxStack;
                        }

                        if (theItem.Amount <= 0) break;
                    }
                }

                if (theItem.Amount > 0) return new CanAddItem { canAdd = false, Reason = Lang("NotEnoughRoom") };
            }

            var canBuy = CanBuy(player, theItems.Sum(x => x.Cost));
            if (!canBuy.CanBuy)
            {
                SendReply(player, canBuy.Title);
                return new CanAddItem { canAdd = false, Reason = canBuy.Title };
            }

            foreach (var item in needsEditedList)
            {
                item.Item.amount += item.Amount;
            }

            foreach (var item in needsAddedList)
            {
                var createdItem = ItemManager.CreateByItemID(item.Item.info.itemid);
                createdItem.amount = item.Amount;
                createdItem.MoveToContainer(player.inventory.containerMain);
            }

            player.inventory.containerMain.MarkDirty();

            itemList.Clear();
            Pool.FreeUnsafe(ref itemList);

            return new CanAddItem { canAdd = true };
        }

        private void SavePlayerSettings(BasePlayer player)
        {
            if (!_playerSettings.ContainsKey(player.userID)) return;
            Interface.GetMod().DataFileSystem.WriteObject($"ShopController{Path.DirectorySeparatorChar}PlayerOptions{Path.DirectorySeparatorChar}{player.userID}", _playerSettings[player.userID]);
        }

        private PlayerSettings GetPlayerSettings(BasePlayer player)
        {
            if (!_playerSettings.ContainsKey(player.userID))
            {
                LoadSavedPlayerSettings(player);
            }

            return _playerSettings[player.userID];
        }

        private void LoadSavedPlayerSettings(BasePlayer player)
        {
            var playerSettings = Interface.GetMod().DataFileSystem.ReadObject<PlayerSettings>($"ShopController{Path.DirectorySeparatorChar}PlayerOptions{Path.DirectorySeparatorChar}{player.userID}") ?? new PlayerSettings { Category = _itemList.Count > 0 ? _itemList.Keys.First() : null };

            playerSettings.CurrentSavedCartPage = 0;
            playerSettings.CurrentCartPage = 0;
            playerSettings.CategoryPage = 0;
            playerSettings.EditCategoryPage = 0;
            playerSettings.EditItemPage = 0;
            playerSettings.ItemPage = 0;
            playerSettings.CartView = null;
            playerSettings.Category = null;
            playerSettings.EditItemFilter = null;
            playerSettings.ItemFilter = null;
            playerSettings.ItemOption = new ItemOption();
            playerSettings.CommandOption = new CommandOption();
            if (_config.ClearDataOnWipe && playerSettings.LastWipe != LastWipe)
            {
                playerSettings.LastWipe = LastWipe;
                playerSettings.RedeemedCommands = new List<RedeemedItems>();
                playerSettings.RedeemedItems = new List<RedeemedItems>();
            }

            if (!string.IsNullOrEmpty(playerSettings.CurrentCart) && !playerSettings.Carts.ContainsKey(playerSettings.CurrentCart)) playerSettings.CurrentCart = playerSettings.Carts.Count > 0 ? playerSettings.Carts.First().Key : null;

            _playerSettings[player.userID] = playerSettings;
        }

        private void RegisterCommandsAndPermissions()
        {
            if (!usingWC) foreach (var command in _config.Commands)
                    cmd.AddChatCommand(command, this, CMDOpenShop);

            permission.RegisterPermission("shopcontroller.admin", this);
            permission.RegisterPermission("shopcontroller.use", this);
        }

        private void StartItemRequest()
        {
            ServerMgr.Instance.StartCoroutine(GenerateItemList());
        }
        private IEnumerator GenerateItemList()
        {
            var itemList = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<string, List<ItemOption>>>($"ShopController{Path.DirectorySeparatorChar}itemList");
            if (itemList != null && itemList.Count > 0)
            {
                _config.ItemList = itemList;
                _itemList = new Dictionary<string, List<ItemOption>>();
                SaveItemList();
            }

            _itemList = _config.ItemList;

            foreach (ItemDefinition itemDefinition in ItemManager.itemList)
            {
                if (!_itemList.ContainsKey(itemDefinition.category.ToString())) _itemList.Add(itemDefinition.category.ToString(), new List<ItemOption>());
                if (!_itemList[itemDefinition.category.ToString()].Any(x => x.ItemId == itemDefinition.itemid)) _itemList[itemDefinition.category.ToString()].Add(new ItemOption { Shortname = itemDefinition.shortname, DisplayName = itemDefinition.displayName.english, ItemId = itemDefinition.itemid, InUse = false, Cost = 0, SpecialId = rnd.Next(1, 100000000) });
                yield return CoroutineEx.waitForEndOfFrame;
            }

            SaveConfig();
        }

        private void GenerateCommandList()
        {
            _commandList = Interface.GetMod().DataFileSystem.ReadObject<List<CommandOption>>($"ShopController{Path.DirectorySeparatorChar}commandList");
        }

        private void SaveCommandList()
        {
            Interface.GetMod().DataFileSystem.WriteObject($"ShopController{Path.DirectorySeparatorChar}commandList", _commandList);
        }

        private void SaveItemList()
        {
            Interface.GetMod().DataFileSystem.WriteObject($"ShopController{Path.DirectorySeparatorChar}itemList", _itemList);
        }

        private void RegisterImages()
        {
            Dictionary<string, string> commandImageList = new Dictionary<string, string>();
            foreach (var kit in _commandList.Where(x => !string.IsNullOrEmpty(x.CommandImage)))
            {
                commandImageList.Add($"Command{kit.CommandName}", kit.CommandImage);
            }

            commandImageList.Add("CommandPlaceholder", _config.Images.CommandImagePlaceholder);
            commandImageList.Add("SellBackImage", _config.Images.SellBackImage);

            ImageLibrary?.Call("ImportImageList", "ShopController", commandImageList, 0UL, true);
        }

        private void RegisterNewImage(string imageName, string imageUrl) => ImageLibrary?.Call("AddImage", imageUrl, imageName, 0UL);

        string GetImage(string imageName)
        {
            return ImageLibrary?.Call<string>("GetImage", imageName, 0UL);
        }
        #endregion

        #region UI Methods
        private static string CreateItemPanel(ref CuiElementContainer container, string anchorMin, string anchorMax, float padding, string color, int itemId, string parent = "Overlay", string panelName = null, ulong skinId = 0L)
        {
            var panel = container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax
                },
                Image = { Color = color }
            }, parent, panelName);

            container.Add(new CuiElement
            {
                Parent = panel,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{padding} {padding + .004f}",
                        AnchorMax = $"{1 - padding - .004f} {1 - padding - .02f}"
                    },
                    new CuiImageComponent {ItemId = itemId, SkinId = skinId}
                }
            });

            return panel;
        }

        private static string CreateLabel(ref CuiElementContainer container, string anchorMin, string anchorMax, string panelColor, string textColor, string labelText, int fontSize, TextAnchor alignment, string parent = "Overlay", string panelName = null)
        {
            var panel = CreatePanel(ref container, anchorMin, anchorMax, panelColor, parent, panelName);
            container.Add(new CuiLabel
            {
                Text =
                {
                    Color = textColor,
                    Text = labelText,
                    Align = alignment,
                    FontSize = fontSize,
                    Font = "robotocondensed-bold.ttf"
                }
            }, panel);
            return panel;
        }

        private static string CreatePanel(ref CuiElementContainer container, string anchorMin, string anchorMax, string panelColor, string parent = "Overlay", string panelName = null, bool blur = false, bool isMainPanel = false)
        {
            CuiPanel panel = new CuiPanel
            {
                RectTransform =
            {
                AnchorMin = anchorMin,
                AnchorMax = anchorMax
            },
                Image = { Color = panelColor }
            };

            if (blur) panel.Image.Material = "assets/content/ui/uibackgroundblur.mat";
            if (isMainPanel) panel.CursorEnabled = true;
            return container.Add(panel, parent, panelName);
        }

        private static void CreateImagePanel(ref CuiElementContainer container, string anchorMin, string anchorMax, string panelImage, string parent = "Overlay", string panelName = null, bool isUrl = false)
        {
            var panel = new CuiElement
            {
                Parent = parent,
                Name = panelName,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax
                    }
                }
            };

            if (isUrl) panel.Components.Add(new CuiRawImageComponent { Url = panelImage });
            else panel.Components.Add(new CuiRawImageComponent { Png = panelImage });

            container.Add(panel);
        }

        private static void CreateImageButton(ref CuiElementContainer container, string anchorMin, string anchorMax, string buttonColor, string buttonCommand, string panelImage, string parent = "Overlay", string panelName = null)
        {
            var panel = CreatePanel(ref container, anchorMin, anchorMax, buttonColor, parent, panelName);
            CreateImagePanel(ref container, "0 0", "1 1", panelImage, panel);

            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = $"{buttonCommand}" }
            }, panel);
        }

        private static string CreateButton(ref CuiElementContainer container, string anchorMin, string anchorMax, string buttonColor, string textColor, string buttonText, int fontSize, string buttonCommand, string parent = "Overlay", TextAnchor labelAnchor = TextAnchor.MiddleCenter)
        {
            var panel = CreatePanel(ref container, anchorMin, anchorMax, "0 0 0 0", parent);

            container.Add(new CuiButton
            {
                Button = { Color = buttonColor, Command = $"{buttonCommand}" },
                Text = { Align = labelAnchor, Color = textColor, FontSize = fontSize, Text = buttonText }
            }, panel);
            return panel;
        }

        private static string CreateInput(ref CuiElementContainer container, string anchorMin, string anchorMax, string command, string backgroundColor, string textColor, string labelText, int fontSize, TextAnchor alignment, string parent = "Overlay", string labelName = null)
        {
            var panel = CreatePanel(ref container, anchorMin, anchorMax, backgroundColor, parent, labelName);

            container.Add(new CuiElement
            {
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Color = textColor,
                        Text = labelText,
                        Align = alignment,
                        FontSize = fontSize,
                        Font = "robotocondensed-bold.ttf",
                        NeedsKeyboard = true,
                        Command = command
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                },
                Parent = panel
            });

            return panel;
        }
        #endregion

    }
}
