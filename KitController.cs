using Oxide.Game.Rust.Cui;
using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using System.IO;
using Newtonsoft.Json;
using Oxide.Core.Configuration;
using System.Collections;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using System.Threading.Tasks;
using System.Timers;
using Facepunch.Extend;
using Facepunch;
using System.Reflection;

namespace Oxide.Plugins
{
    [Info("KitController", "Amino", "2.2.1")]
    [Description("A new, modern kit system for Rust")]
    public class KitController : RustPlugin
    {
        [PluginReference] Plugin WelcomeController, ImageLibrary, NoEscape, ServerRewards, Economics;

        #region Config
        public class Configuration
        {
            [JsonProperty("Kit Commands")]
            public List<string> KitCommands { get; set; } = new List<string>();
            [JsonProperty(PropertyName = "Default Currency (RP, Economics, item.shortname)")]
            public string DefaultCurrency { get; set; } = "RP";
            [JsonProperty(PropertyName = "Clear data on server wipe")]
            public bool WipePlayerKitsOnServerWipe { get; set; } = true;
            [JsonProperty(PropertyName = "(No Escape) Can't redeem kits on combat blocked")]
            public bool UseNoKitsOnCombatBlock { get; set; } = false;
            [JsonProperty(PropertyName = "(No Escape) Can't redeem kits on raid blocked")]
            public bool UseNoKitsOnRaidBlock { get; set; } = false;
            [JsonProperty(PropertyName = "Kits Title (Panel image will take priority)")]
            public string KitsTitle { get; set; } = "KITS";
            [JsonProperty(PropertyName = "Plugin images")]
            public Images Images { get; set; } = new Images();
            [JsonProperty(PropertyName = "Kits Layout (0, 1)")]
            public int KitsLayout { get; set; } = 0;
            [JsonProperty(PropertyName = "UI Colors (0, 1)")]
            public int KitsColors { get; set; } = 0;
            [JsonProperty(PropertyName = "UI 0")]
            public UIElements UIZero { get; set; }
            [JsonProperty(PropertyName = "UI 1")]
            public UIElements UIOne { get; set; }
            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    DefaultCurrency = "RP",
                    KitCommands = new List<string> { "kits", "kit" },
                    Images = new Images
                    {
                        PanelImage = "https://i.ibb.co/wK0ZYJ3/Kits-Banner.png",
                        EditImage = "https://i.ibb.co/92yTGNd/edit-Button.png",
                        KitPlaceholderImage = "https://i.ibb.co/J58sN3b/Kit-Placeholder.png"
                    },
                    UIZero = new UIElements(),
                    UIOne = new UIElements
                    {
                        MiddlePanelColor = ".17 .17 .17 1",
                        TopPanelColor = ".17 .17 .17 1",
                        BottomPanelColor = ".17 .17 .17 1",
                        CategoryButtonColor = "1 1 1 .08",
                        CategoryPageButtonColor = "1 1 1 .08",
                        KitDisplayPanelColor = "1 1 1 .08",
                        KitImageBackgroundColor = "1 1 1 .08",
                        HeaderCooldownColor = "1 1 1 .08",
                        HeaderMaxUsesColor = "1 1 1 .08",
                        HeaderKitNameColor = "1 1 1 .08",
                        KitsLayoutOneBlankBoxColor = "1 1 1 .08",
                        KitsItemPageButtonColor = "1 1 1 .08",
                        KitsItemsColor = "1 1 1 .08",
                        MockInventoryItemColor = "1 1 1 .08",
                        EditPageMainColor = ".17 .17 .17 1",
                        EditPageSecondaryColor = "1 1 1 .08",
                        ViewPageMainColor = ".17 .17 .17 1",
                        ViewPageSecondaryColor = "1 1 1 .08",
                        KitsPageButtonColor = "1 1 1 .08"
                    }
                };
            }
        }

        public class Images
        {
            [JsonProperty(PropertyName = "Panel Image")]
            public string PanelImage { get; set; }
            [JsonProperty(PropertyName = "Edit Image")]
            public string EditImage { get; set; }
            [JsonProperty(PropertyName = "Kit placeholder image")]
            public string KitPlaceholderImage { get; set; }
        }

        public class UIElements
        {
            public string BlurBackgroundColor { get; set; } = "0 0 0 .4";
            public string TopPanelColor { get; set; } = "0 0 0 .5";
            public string MiddlePanelColor { get; set; } = "0 0 0 .5";
            public string BottomPanelColor { get; set; } = "0 0 0 .5";
            public string CloseText { get; set; } = "X";
            public string CloseTextColor { get; set; } = "1 1 1 1";
            public string KitsPageButtonColor { get; set; } = "0 0 0 .5";
            public string CategoryPageButtonColor { get; set; } = "0 0 0 .5";
            public string CategoryButtonColor { get; set; } = "0 0 0 .5";
            public string ActiveCategoryButtonColor { get; set; } = ".20 .60 .92 .5";
            public string KitDisplayPanelColor { get; set; } = "0 0 0 .5";
            public string KitImageBackgroundColor { get; set; } = "0 0 0 .5";
            public string CanClaimButtonColor { get; set; } = ".39 .89 .4 .8";
            public string CantClaimButtonColor { get; set; } = "1 .31 .31 .8";
            public string CanClaimTextColor { get; set; } = "0.44 1 0.23 1";
            public string CantClaimTextColor { get; set; } = "1 .31 .31 1";
            public string ViewButtonColor { get; set; } = ".39 .64 1 .7";
            public string ViewTextColor { get; set; } = ".61 .81 1 1";
            public string NoPermsViewButtonColor { get; set; } = "0.31 0.68 1 1";
            public string NoPermsViewTextColor { get; set; } = "1 1 1 1";
            public string CantClaimCooldownColor { get; set; } = "1 .31 .31 1";
            public string CanClaimCooldownColor { get; set; } = "1 1 1 1";
            public string CantClaimMaxUsesColor { get; set; } = "1 .31 .31 1";
            public string CanClaimMaxUsesColor { get; set; } = "1 1 1 1";
            public string HeaderCooldownColor { get; set; } = "0 0 0 .4";
            public string HeaderCooldownTextColor { get; set; } = "1 1 1 1";
            public string HeaderMaxUsesColor { get; set; } = "0 0 0 .4";
            public string HeaderMaxUsesTextColor { get; set; } = "1 1 1 1";
            public string HeaderKitNameColor { get; set; } = "0 0 0 .4";
            public string HeaderKitNameTextColor { get; set; } = "1 1 1 1";
            public string NoPermissionOverlayColor { get; set; } = "0 0 0 .5";
            public string KitsLayoutOneBlankBoxColor { get; set; } = "0 0 0 .4";
            public string KitsItemPageButtonColor { get; set; } = "0 0 0 .4";
            public string KitsItemsColor { get; set; } = "0 0 0 .4";
            public string MockInventoryItemColor { get; set; } = "0 0 0 .5";
            public string EditPageMainColor { get; set; } = "0 0 0 .5";
            public string EditPageSecondaryColor { get; set; } = "0 0 0 .5";
            public string ViewPageMainColor { get; set; } = "0 0 0 .5";
            public string ViewPageSecondaryColor { get; set; } = "0 0 0 .5";
        }


        private static Configuration _config;
        private static UIElements _uiColors;
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) LoadDefaultConfig();
                if (_config.KitsColors == 0) _uiColors = _config.UIZero;
                else _uiColors = _config.UIOne;
                SaveConfig();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                PrintWarning("Creating new config file");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => _config = Configuration.DefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(_config);
        #endregion

        #region Data
        public List<aKit> _kitData = new List<aKit>();
        public Dictionary<ulong, bKit> _kitEdits = new Dictionary<ulong, bKit>();
        public Dictionary<ulong, PlayerOptions> _playerOptions = new Dictionary<ulong, PlayerOptions>();
        public List<PlayerUsedKits> _userData = new List<PlayerUsedKits>();
        public bool RespawnSubed = false;
        public static DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        public static double CurrentTime => DateTime.UtcNow.Subtract(Epoch).TotalSeconds;
        public static double LastWipe;
        public bool usingWC = false;

        public class CanRedeem
        {
            public bool Can { get; set; }
            public string Time { get; set; }
        }

        public class ClaimKitResponse
        {
            public bool CanRedeem { get; set; }
            public string Reason { get; set; }
        }

        public class CanPurchase
        {
            public bool CanBuy { get; set; }
            public string Reason { get; set; }
        }

        public class ItemChange
        {
            public Item Item { get; set; }
            public int Amount { get; set; }
        }

        public class aKit
        {
            public string KitName { get; set; }
            public string Permission { get; set; }
            public string Description { get; set; }
            public string Category { get; set; }
            public int Cost { get; set; }
            public string Currency { get; set; } = "rp";
            public int MaxUses { get; set; } = 0;
            public double Cooldown { get; set; } = 0;
            public string KitImage { get; set; }
            public string NoPermissionText { get; set; } = "You need to be VIP to use this kit!";
            public bool IsAutoKit { get; set; }
            public int AutoKitPriority { get; set; }
            public double WipeCooldown { get; set; }
            public bool Hidden { get; set; }
            public List<kitContents> Items { get; set; } = new List<kitContents>();
        }

        public class createItem
        {
            public string Category { get; set; }
            public Item Item { get; set; }
        }

        public class bKit : aKit
        {
            public string ParentName { get; set; }
            public bKit()
            {

            }
            public bKit(aKit source)
            {
                KitName = source.KitName;
                Permission = source.Permission;
                Description = source.Description;
                Category = source.Category;
                MaxUses = source.MaxUses;
                Cost = source.Cost;
                Cooldown = source.Cooldown;
                Currency = source.Currency;
                KitImage = source.KitImage;
                Hidden = source.Hidden;
                NoPermissionText = source.NoPermissionText;
                IsAutoKit = source.IsAutoKit;
                AutoKitPriority = source.AutoKitPriority;
                WipeCooldown = source.WipeCooldown;
                Items = source.Items;
            }
        }

        public class kitContents
        {
            public string Shortname { get; set; }
            public int ItemId { get; set; }
            public ulong Skin { get; set; }
            public int Amount { get; set; }
            public int Ammo { get; set; }
            public string AmmoType { get; set; }
            public int Position { get; set; }
            public string Container { get; set; }
            public bool IsBlueprint { get; set; }
            public List<int> Contents { get; set; }
        }

        public class PlayerOptions
        {
            public string ActiveCategory { get; set; }
            public int KitPage { get; set; }
            public int CategoryPage { get; set; }
            public bool UsingWelcomeController { get; set; }
        }

        public class PlayerUsedKits
        {
            public ulong SteamId { get; set; }
            public List<UsedKits> UsedKits { get; set; }
        }

        public class UsedKits
        {
            public string KitName { get; set; }
            public int TotalUses { get; set; } = 0;
            public double NextUseTime { get; set; } = 0;
        }

        public class MockItem
        {
            public int ItemId { get; set; }
            public ulong SkinId { get; set; }
            public int ItemAmount { get; set; }
            public int ItemPosition { get; set; }
        }
        #endregion

        #region RustKits Data
        public class OldKit
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string RequiredPermission { get; set; } = string.Empty;

            public int MaximumUses { get; set; }
            public int RequiredAuth { get; set; }
            public int Cooldown { get; set; }
            public int Cost { get; set; }

            public bool IsHidden { get; set; }

            public string CopyPasteFile { get; set; } = string.Empty;
            public string KitImage { get; set; } = string.Empty;

            public OldItemData[] MainItems { get; set; } = new OldItemData[0];
            public OldItemData[] WearItems { get; set; } = new OldItemData[0];
            public OldItemData[] BeltItems { get; set; } = new OldItemData[0];
        }


        public class OldItemData
        {
            public string Shortname { get; set; }

            public ulong Skin { get; set; }

            public int Amount { get; set; }

            public float Condition { get; set; }

            public float MaxCondition { get; set; }

            public int Ammo { get; set; }

            public string Ammotype { get; set; }

            public int Position { get; set; }

            public int Frequency { get; set; }

            public string BlueprintShortname { get; set; }

            public OldItemData[] Contents { get; set; }

            internal OldItemData() { }

            internal OldItemData(Item item)
            {
                Shortname = item.info.shortname;
                Amount = item.amount;

                Ammotype = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.ammoType.shortname ?? null;
                Ammo = item.GetHeldEntity() is BaseProjectile ? (item.GetHeldEntity() as BaseProjectile).primaryMagazine.contents :
                               item.GetHeldEntity() is FlameThrower ? (item.GetHeldEntity() as FlameThrower).ammo : 0;

                Position = item.position;
                Skin = item.skin;

                Condition = item.condition;
                MaxCondition = item.maxCondition;

                Frequency = ItemModAssociatedEntity<PagerEntity>.GetAssociatedEntity(item)?.GetFrequency() ?? -1;

                if (item.instanceData != null && item.instanceData.blueprintTarget != 0)
                    BlueprintShortname = ItemManager.FindItemDefinition(item.instanceData.blueprintTarget).shortname;

                Contents = item.contents?.itemList.Select(item1 => new OldItemData(item1)).ToArray();
            }

        }
        #endregion

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ChatTitle"] = "<color=#91b9fa>[ KIT CONTROLLER ]:</color>",
                ["ButtonView"] = "VIEW",
                ["KitViewTitle"] = "KIT VIEW",
                ["ButtonRedeemLimitReached"] = "REDEEM LIMIT REACHED",
                ["ButtonClaim"] = "CLAIM",
                ["ButtonOnCooldown"] = "ON COOLDOWN",
                ["TextNoCooldown"] = "No cooldown",
                ["IsBlocked"] = "You are currently <color=#91b9fa>combat</color> and or <color=#91b9fa>raid</color> blocked!",
                ["ButtonNoPermission"] = "NO PERMISSION",
                ["HeaderMaxUses"] = "MAX USES",
                ["HeaderKitName"] = "KIT NAME",
                ["HeaderCooldown"] = "COOLDOWN",
                ["HeaderImage"] = "IMAGE",
                ["HeaderDescription"] = "DESCRIPTION",
                ["AdminHeaderMaxUses"] = "MAX USES",
                ["AdminHeaderKitName"] = "KIT NAME",
                ["AdminHeaderCooldown"] = "COOLDOWN (SEC)",
                ["AdminHeaderCreateKit"] = "CREATE KIT",
                ["AdminHeaderImage"] = "IMAGE",
                ["AdminHeaderDescription"] = "DESCRIPTION",
                ["ErrNameExists"] = "A KIT WITH THIS NAME ALREADY EXISTS",
                ["ChatOnCooldown"] = "Kit <color=#91b9fa>{0}</color> is on cooldown for <color=#91b9fa>{1}</color>",
                ["ChatMaxRedeem"] = "You've reached the max redeem limit for <color=#91b9fa>{0}</color>",
                ["ChatKitRedeemed"] = "You've redeemed <color=#91b9fa>{0}</color> kit!",
                ["ChatNoPermission"] = "You don't have permission to redeem <color=#91b9fa>{0}</color> kit",
                ["ChatNotFound"] = "Kit <color=#91b9fa>{0}</color> not found",
                ["ChatNoKitItems"] = "No kit items found for <color=#91b9fa>{0}</color>",
                ["ChatWipeCooldown"] = "<color=#91b9fa>{0}</color> is on wipe cooldown for <color=#91b9fa>{1}</color>",
                ["ChatTooManyItems"] = "Too many items in <color=#91b9fa>inventory</color>",
                ["ChatNoPlayerFound"] = "Could not find <color=#91b9fa>{0}</color> on the server",
                ["ChatIsAutoKit"] = "Cannot redeem <color=#91b9fa>{0}</color> because it's an autokit",
                ["ChatKitGivenPlr"] = "Gave <color=#91b9fa>{0}</color> kit to <color=#91b9fa>{1}</color>",
                ["RPPlaceholder"] = "{0} RP",
                ["EconomicsPlaceholder"] = "${0}",
                ["ItemPlaceholder"] = "${0}",
                ["RPNotEnough"] = "You do not have enough RP to purchase <color=#91b9fa>{0}</color> kit!",
                ["EconomicsNotEnough"] = "You do not have enough money to purchase <color=#91b9fa>{0}</color> kit!",
                ["ItemNotEnough"] = "You do not have enough scrap to purchase <color=#91b9fa>{0}</color> kit!",
                ["CurrencyNotFound"] = "Currency not found! Please contact admin!",
            }, this, "en");
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        #endregion

        #region Hooks
        public static Dictionary<string, string> ConvertToDictionary(UIElements uiElements)
        {
            var dictionary = new Dictionary<string, string>();
            foreach (PropertyInfo property in typeof(UIElements).GetProperties())
            {
                string key = property.Name;
                string value = property.GetValue(uiElements)?.ToString();
                dictionary[key] = value;
            }
            return dictionary;
        }

        void OnWCRequestColors(string pluginName)
        {
            if (!pluginName.Equals("KitController", StringComparison.OrdinalIgnoreCase)) return;
            Interface.CallHook("WCSendColors", ConvertToDictionary(_uiColors), pluginName);
        }

        void OnWCSentThemeColors(List<string> pluginNames, Dictionary<string, string> themeColors)
        {
            if (!pluginNames.Contains("KitController")) return;

            _uiColors.TopPanelColor = themeColors["BackgroundColor"];
            _uiColors.MiddlePanelColor = themeColors["BackgroundColor"];
            _uiColors.BottomPanelColor = themeColors["BackgroundColor"];
            _uiColors.KitsPageButtonColor = themeColors["SecondaryColor"];
            _uiColors.CategoryPageButtonColor = themeColors["SecondaryColor"];
            _uiColors.ActiveCategoryButtonColor = themeColors["PrimaryButtonColor"];
            _uiColors.KitDisplayPanelColor = themeColors["SecondaryColor"];
            _uiColors.KitDisplayPanelColor = themeColors["SecondaryColor"];
            _uiColors.CanClaimButtonColor = themeColors["ThirdButtonColor"];
            _uiColors.CantClaimButtonColor = themeColors["SecondaryButtonColor"];

            _uiColors.CanClaimTextColor = "1 1 1 1";
            _uiColors.CantClaimTextColor = "1 1 1 1";
            _uiColors.ViewButtonColor = themeColors["PrimaryButtonColor"];
            _uiColors.ViewTextColor = "1 1 1 1";
            _uiColors.NoPermsViewButtonColor = themeColors["PrimaryButtonColor"];
            _uiColors.NoPermsViewTextColor = "1 1 1 1";
            _uiColors.CantClaimButtonColor = themeColors["SecondaryButtonColor"];
            _uiColors.CantClaimMaxUsesColor = themeColors["SecondaryButtonColor"];

            _uiColors.HeaderCooldownColor = themeColors["SecondaryColor"];
            _uiColors.HeaderMaxUsesColor = themeColors["SecondaryColor"];
            _uiColors.HeaderKitNameColor = themeColors["SecondaryColor"];
            _uiColors.NoPermissionOverlayColor = themeColors["SecondaryColor"];
            _uiColors.KitsLayoutOneBlankBoxColor = themeColors["SecondaryColor"];
            _uiColors.KitsItemPageButtonColor = themeColors["SecondaryColor"];
            _uiColors.KitsItemsColor = themeColors["SecondaryColor"];
            _uiColors.MockInventoryItemColor = themeColors["SecondaryColor"];
            _uiColors.EditPageMainColor = themeColors["SecondaryColor"];
            _uiColors.EditPageSecondaryColor = themeColors["SecondaryColor"];
            _uiColors.ViewPageMainColor = themeColors["SecondaryColor"];
            _uiColors.ViewPageSecondaryColor = themeColors["SecondaryColor"];

            SaveConfig();
        }

        void OnServerInitialized(bool initial)
        {
            RegisterImages();
            LastWipe = SaveRestore.SaveCreatedTime.Subtract(Epoch).TotalSeconds;
            var isUsingWC = Interface.Call("IsUsingPlugin", "KitController");
            if (isUsingWC != null && (isUsingWC is bool)) usingWC = (bool)isUsingWC;
            else usingWC = false;
            RegisterCommandsAndPermissions();
        }

        void Loaded()
        {
            LoadKitData();
            LoadUserData();
            UnsubscribeHooks();
        }

        void OnWCRequestedUIPanel(BasePlayer player, string panelName, string neededPlugin)
        {
            if (!neededPlugin.Equals("kitcontroller", StringComparison.OrdinalIgnoreCase)) return;
            usingWC = true;
            KitCommand(player, null, null);
        }

        void OnPluginLoaded(Plugin plugin)
        {
            if (plugin.Name == "WelcomeController")
            {
                var isUsingWC = Interface.Call("IsUsingPlugin", "KitController");
                if (isUsingWC != null && (isUsingWC is bool)) usingWC = (bool)isUsingWC;
                else usingWC = false;
            }
        }

        void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin.Name == "WelcomeController")
            {
                usingWC = false;
                RegisterCommandsAndPermissions(true);
            }
        }

        void OnServerSave()
        {
            SaveKitList();
            SaveUserList();
        }

        private void Unload()
        {
            if (!Interface.Oxide.IsShuttingDown)
            {
                SaveKitList();
                SaveUserList();

                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(player, "KCMainPanel");
                    CuiHelper.DestroyUi(player, "KCMainEditPanel");
                    CuiHelper.DestroyUi(player, "KCMainViewPanel");
                }
            }

            _config = null;
        }

        void OnNewSave(string filename)
        {
            if (_config.WipePlayerKitsOnServerWipe) _userData.Clear();
        }

        object OnPlayerRespawned(BasePlayer player, BasePlayer.SpawnPoint spawnPoint)
        {
            GiveAutoKit(player);
            return null;
        }

        #endregion

        #region Commands
        [ChatCommand("convertkits")]
        private void ConvertData(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "kitcontroller.admin"))
            {
                SendReply(player, "You do not have kitcontroller.admin permission!");
                return;
            }

            ConvertRustKitsData(player);
        }

        private void AddItemData(ref List<kitContents> kitContents, OldItemData[] oldItemData, string container)
        {
            foreach (var item in oldItemData)
            {
                var isBlueprint = !string.IsNullOrEmpty(item.BlueprintShortname);
                var theItem = ItemManager.FindItemDefinition(isBlueprint ? item.BlueprintShortname : item.Shortname);
                if (theItem == null) continue;

                var contents = new List<int>();
                if (item.Contents != null)
                {
                    foreach (var content in item.Contents)
                    {
                        var theContent = ItemManager.FindItemDefinition(content.Shortname);
                        if (theContent == null) continue;
                        contents.Add(theContent.itemid);
                    }
                }

                kitContents.Add(new kitContents()
                {
                    ItemId = theItem.itemid,
                    Shortname = theItem.shortname,
                    Skin = item.Skin,
                    Amount = item.Amount,
                    Ammo = item.Ammo,
                    AmmoType = item.Ammotype,
                    IsBlueprint = isBlueprint,
                    Position = item.Position,
                    Container = container,
                    Contents = contents
                });
            }
        }

        private void ConvertRustKitsData(BasePlayer player = null)
        {
            try
            {
                DynamicConfigFile kits = Interface.Oxide.DataFileSystem.GetFile("Kits/kits_data");
                kits.Settings.NullValueHandling = NullValueHandling.Ignore;
                Dictionary<string, Dictionary<string, OldKit>> rustKitsStoredData = kits.ReadObject<Dictionary<string, Dictionary<string, OldKit>>>();

                if (rustKitsStoredData == null || rustKitsStoredData.Count == 0)
                {
                    if (player != null) SendReply(player, "Could not locate 'Kits' data file!");
                    return;
                }

                if (player != null) SendReply(player, $"Converting {rustKitsStoredData["_kits"].Count} kits to Kit Controller!");
                foreach (var kit in rustKitsStoredData["_kits"].Values)
                {
                    if (_kitData.Any(x => x.KitName.Equals(kit.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        SendReply(player, $"Cannot add {kit.Name} because it's already in current kit list!");
                        continue;
                    }

                    List<kitContents> kitContents = new List<kitContents>();
                    if (kit.WearItems != null) AddItemData(ref kitContents, kit.WearItems, "attire");
                    if (kit.MainItems != null) AddItemData(ref kitContents, kit.MainItems, "main");
                    if (kit.BeltItems != null) AddItemData(ref kitContents, kit.BeltItems, "belt");

                    aKit newKit = new aKit()
                    {
                        KitName = kit.Name,
                        Permission = kit.RequiredPermission.Contains('.') ? "kitcontroller" + kit.RequiredPermission.Substring(5) : $"kitcontroller.{kit.RequiredPermission}",
                        Description = kit.Description,
                        Cost = kit.Cost,
                        MaxUses = kit.MaximumUses,
                        Cooldown = kit.Cooldown,
                        KitImage = kit.KitImage,
                        Hidden = kit.IsHidden,
                        Items = kitContents
                    };

                    RegisterNewImage($"Kit{kit.Name}", kit.KitImage);

                    SendReply(player, $"Successfully converted {kit.Name}!");
                    _kitData.Add(newKit);
                }


            }
            catch
            {
                if (player != null) SendReply(player, "Could not locate 'Kits' data file!");
            }
        }

        [ConsoleCommand("kc_main")]
        private void ConsoleKit(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (arg == null || player == null) return;

            switch (arg.Args[0])
            {
                case "close":
                    CuiHelper.DestroyUi(player, "KCMainPanel");
                    break;
                case "edit":
                    _kitEdits[player.userID] = new bKit();
                    if (arg.Args.Length > 1)
                    {
                        var kitName = String.Join(" ", arg.Args.Skip(1));
                        _kitEdits[player.userID] = _kitData.Where(x => x.KitName == kitName).Select(x => new bKit(x)).FirstOrDefault();
                        _kitEdits[player.userID].ParentName = kitName;
                    }
                    UICreateEditPage(player);

                    CuiHelper.DestroyUi(player, "KCMainPanel");
                    break;
                case "editclose":
                    CuiHelper.DestroyUi(player, "KCMainEditPanel");
                    _kitEdits.Remove(player.userID);

                    OpenKitUI(player);
                    break;
                case "category":
                    var playerInfo = GetOrLoadPlayerOptions(player);
                    playerInfo.ActiveCategory = string.Join(" ", arg.Args.Skip(1));
                    playerInfo.KitPage = 0;
                    OpenKitUI(player);
                    break;
                case "kitspage":
                    playerInfo = GetOrLoadPlayerOptions(player);
                    playerInfo.KitPage = int.Parse(arg.Args[1]);
                    OpenKitUI(player);
                    break;
                case "categorypage":
                    playerInfo = GetOrLoadPlayerOptions(player);
                    playerInfo.CategoryPage = int.Parse(arg.Args[1]);
                    OpenKitUI(player);
                    break;
            }
        }

        [ConsoleCommand("kc_edit")]
        private void ConsoleEditKits(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (arg == null || player == null) return;

            var editKit = _kitEdits[player.userID];

            switch (arg.Args[0])
            {
                case "deletekit":
                    _kitData.RemoveAll(x => x.KitName.Equals(editKit.ParentName, StringComparison.OrdinalIgnoreCase));
                    CuiHelper.DestroyUi(player, "KCMainEditPanel");
                    _kitEdits.Remove(player.userID);
                    OpenKitUI(player);
                    break;
                case "selectitem":
                    HandleItemEdit(player, int.Parse(arg.Args[1]), arg.Args[2]);
                    break;
                case "shortname":
                    ChangeItem(player, int.Parse(arg.Args[1]), arg.Args[2], arg.Args.Length > 3 ? arg.Args[3] : " ", 0);
                    break;
                case "skin":
                    ChangeItem(player, int.Parse(arg.Args[1]), arg.Args[2], arg.Args.Length > 3 ? arg.Args[3] : " ", 1);
                    break;
                case "amount":
                    ChangeItem(player, int.Parse(arg.Args[1]), arg.Args[2], arg.Args.Length > 3 ? arg.Args[3] : " ", 2);
                    break;
                case "cost":
                    if (arg.Args.Length > 1 && !string.IsNullOrEmpty(arg.Args[1]))
                    {
                        if (int.TryParse(arg.Args[1], out int cost))
                        {
                            if (cost < 1) cost = 0;
                            else editKit.Cost = cost;
                        }
                    }
                    else editKit.Cost = 0; break;
                case "currency":
                    if (arg.Args.Length > 1 && !string.IsNullOrEmpty(arg.Args[1])) editKit.Currency = String.Join(" ", arg.Args.Skip(1));
                    else editKit.Currency = _config.DefaultCurrency;
                    break;
                case "savekit":
                    if (!string.IsNullOrEmpty(editKit.ParentName)) SaveKit(player, true, editKit);
                    else SaveKit(player, false, editKit);
                    break;
                case "image":
                    if (arg.Args.Length > 1 && !string.IsNullOrEmpty(arg.Args[1]))
                    {
                        editKit.KitImage = arg.Args[1];
                        RegisterNewImage($"KitEdit{editKit.KitName}", arg.Args[1]);
                    }
                    else editKit.KitImage = null;
                    break;
                case "name":
                    if (arg.Args.Length > 1 && !string.IsNullOrEmpty(arg.Args[1])) editKit.KitName = String.Join(" ", arg.Args.Skip(1));
                    break;
                case "category":
                    if (arg.Args.Length > 1 && !string.IsNullOrEmpty(arg.Args[1])) editKit.Category = String.Join(" ", arg.Args.Skip(1));
                    else editKit.Category = null;
                    break;
                case "kitinventory":
                    SaveItems(player);
                    break;
                case "autokit":
                    if (editKit.IsAutoKit) editKit.IsAutoKit = false;
                    else editKit.IsAutoKit = true;
                    break;
                case "closeitem":
                    CuiHelper.DestroyUi(player, "KCMainItemEdit");
                    break;
                case "deleteitem":
                    editKit.Items.RemoveAll(x => x.Position == int.Parse(arg.Args[1]) && x.Container == arg.Args[2]);
                    var playerInfo = GetOrLoadPlayerOptions(player);
                    playerInfo.ActiveCategory = null;
                    CuiHelper.DestroyUi(player, "KCMainItemEdit");
                    break;
                case "hidden":
                    if (editKit.Hidden) editKit.Hidden = false;
                    else editKit.Hidden = true;
                    break;
                case "categoryselect":
                    var catString = string.Join(" ", arg.Args.Skip(1));
                    switch (catString)
                    {
                        case "CLOSE":
                            CuiHelper.DestroyUi(player, "KCCategoryDropdown");
                            break;
                        case "ALL":
                            editKit.Category = null;
                            break;
                        default:
                            editKit.Category = catString;
                            break;
                    }
                    break;
                case "categoryui":
                    UICategoryDropdownFunctionality(player);
                    break;
                case "description":
                    if (arg.Args.Length > 1 && !string.IsNullOrEmpty(arg.Args[1])) editKit.Description = String.Join(" ", arg.Args.Skip(1));
                    else editKit.Description = null;
                    break;
                case "nopermtext":
                    if (arg.Args.Length > 1 && !string.IsNullOrEmpty(arg.Args[1])) editKit.NoPermissionText = String.Join(" ", arg.Args.Skip(1));
                    else editKit.NoPermissionText = null;
                    break;
                case "permission":
                    if (arg.Args.Length > 1 && !string.IsNullOrEmpty(arg.Args[1]))
                    {
                        var permission = arg.Args[1];
                        if (!permission.Contains("kitcontroller.")) permission = "kitcontroller." + permission;
                        editKit.Permission = permission;
                    }
                    else editKit.Permission = null;
                    break;
                case "autokitpriority":
                    if (arg.Args.Length > 1 && !string.IsNullOrEmpty(arg.Args[1]))
                    {
                        if (int.TryParse(arg.Args[1], out int priority))
                        {
                            if (priority < 1) priority = 0;
                            else editKit.AutoKitPriority = priority;
                        }
                    }
                    else editKit.AutoKitPriority = 0;
                    break;
                case "cooldown":
                    if (arg.Args.Length > 1 && !string.IsNullOrEmpty(arg.Args[1]))
                    {
                        if (double.TryParse(arg.Args[1], out double cooldown))
                        {
                            if (cooldown < 0) cooldown = 0;
                            else editKit.Cooldown = cooldown;
                        }
                    }
                    else editKit.Cooldown = 0;
                    break;
                case "maxuses":
                    if (arg.Args.Length > 1 && !string.IsNullOrEmpty(arg.Args[1]))
                    {
                        if (int.TryParse(arg.Args[1], out int maxuses))
                        {
                            if (maxuses < 0) maxuses = 0;
                            else editKit.MaxUses = maxuses;
                        }
                    }
                    else editKit.MaxUses = 0;
                    break;
                case "wipecooldown":
                    if (arg.Args.Length > 1 && !string.IsNullOrEmpty(arg.Args[1]))
                    {
                        if (double.TryParse(arg.Args[1], out double wipecooldown))
                        {
                            if (wipecooldown < 0) wipecooldown = 0;
                            else editKit.WipeCooldown = wipecooldown;
                        }
                    }
                    else editKit.WipeCooldown = 0;
                    break;
            }

            if (arg.Args[0] != "savekit" && arg.Args[0] != "selectitem" && arg.Args[0] != "categoryui" && arg.Args[0] != "deletekit")
            {
                if (arg.Args[0] == "image") timer.Once(.3f, () => { if (_kitEdits.ContainsKey(player.userID)) UICreateEditOverlayPage(player); });
                else UICreateEditOverlayPage(player);
            }
        }

        [ConsoleCommand("kc_kit")]
        private void ConsoleKitKits(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (arg == null || player == null) return;

            var playerOptions = GetOrLoadPlayerOptions(player);

            switch (arg.Args[0])
            {
                case "itemspage":
                    UICreateKits(player, playerOptions, int.Parse(arg.Args[1]), string.Join(" ", arg.Args.Skip(2)));
                    break;
                case "claim":
                    var clamKit = ClaimKit(player, string.Join(" ", arg.Args.Skip(1)));
                    SendReply(player, Lang("ChatTitle") + " " + clamKit.Reason);
                    CuiHelper.DestroyUi(player, "KCMainPanel");
                    CuiHelper.DestroyUi(player, "KCMainViewPanel");
                    if (usingWC)
                    {
                        CuiHelper.DestroyUi(player, "BackgroundPanel");
                        CuiHelper.DestroyUi(player, "BackgrounPanel");
                    }
                    break;
                case "view":
                    UICreateViewPage(player, string.Join(" ", arg.Args.Skip(1)));
                    CuiHelper.DestroyUi(player, "KCMainPanel");
                    break;
                case "viewclose":
                    CuiHelper.DestroyUi(player, "KCMainViewPanel");
                    OpenKitUI(player);
                    break;
            }
        }

        [ChatCommand("kitadmin")]
        private void CMDAdminCommands(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            HandleAdminCommands(player, args.Length == 0 ? null : args, true);
        }
        #endregion

        #region Methods
        string GetCurrencyType(aKit kit)
        {
            var kitCurrency = kit.Currency.ToLower();
            switch (kitCurrency)
            {
                case "rp":
                    return Lang("RPPlaceholder", null, $"{kit.Cost}");
                case "economics":
                    return Lang("EconomicsPlaceholder", null, $"{kit.Cost}");
                default:
                    return Lang("ItemPlaceholder", null, $"{kit.Cost}");
            }
        }

        CanPurchase CanBuy(BasePlayer player, aKit kit)
        {
            var kitCurrency = kit.Currency.ToLower();
            switch (kitCurrency)
            {
                case "rp":
                    var points = ServerRewards?.Call("CheckPoints", (ulong)player.userID);
                    if (kit.Cost > (points == null ? 0 : double.Parse($"{points}"))) return new CanPurchase { CanBuy = false, Reason = Lang("RPNotEnough", null, kit.KitName) };
                    else return new CanPurchase { CanBuy = true };
                case "economics":
                    var eco = Economics?.Call("Balance", (ulong)player.userID);
                    if (kit.Cost > (eco == null ? 0 : double.Parse($"{eco}"))) return new CanPurchase { CanBuy = false, Reason = Lang("EconomicsNotEnough", null, kit.KitName) };
                    else return new CanPurchase { CanBuy = true };
                default:
                    var theItem = ItemManager.CreateByName(kit.Currency);
                    if (theItem == null) return new CanPurchase { CanBuy = false, Reason = Lang("CurrencyNotFound") };

                    List<Item> itemList = Facepunch.Pool.Get<List<Item>>();
                    player.inventory.GetAllItems(itemList);
                    var inventoryItemCount = itemList.Where(x => x.info.shortname.Equals(theItem.info.shortname)).Sum(x => x.amount);

                    itemList.Clear();
                    Facepunch.Pool.FreeUnsafe(ref itemList);
                    if (kit.Cost > inventoryItemCount) return new CanPurchase { CanBuy = false, Reason = Lang("ItemNotEnough", null, kit.KitName) };
                    else return new CanPurchase { CanBuy = true };
            }
        }

        void BuyKit(BasePlayer player, aKit kit)
        {
            var kitCurrency = kit.Currency.ToLower();
            switch (kitCurrency)
            {
                case "rp":
                    ServerRewards?.Call("TakePoints", (ulong)player.userID, kit.Cost);
                    break;
                case "economics":
                    Economics?.Call("Withdraw", (ulong)player.userID, (double)kit.Cost);
                    break;
                default:
                    List<ItemChange> needsEditedList = new List<ItemChange>();
                    List<ItemChange> needsRemovedList = new List<ItemChange>();
                    int amountLeft = kit.Cost;

                    List<Item> itemList = Facepunch.Pool.Get<List<Item>>();
                    player.inventory.GetAllItems(itemList);

                    foreach (var item in itemList.Where(x => x.info.shortname.Equals(kit.Currency, StringComparison.OrdinalIgnoreCase)))
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
                    itemList.Clear();

                    Pool.FreeUnsafe(ref itemList);
                    break;
            }
        }

        private void HandleAdminCommands(BasePlayer player, string[] args, bool isChat, ConsoleSystem.Arg arg = null)
        {
            if (!permission.UserHasPermission(player.UserIDString, "kitcontroller.admin"))
            {
                DetermineReply(player, isChat, "You do not have permission to use this command", arg);
                return;
            }
            if (args == null)
            {
                DetermineReply(player, isChat, $"{Lang("ChatTitle")} ADMIN COMMANDS" +
                    "\n/kitadmin delete <kitname>" +
                    "\n/kitadmin give <playerName / playerSteamID> <kitName>" +
                    "\n/kitadmin reset", arg);
            }
            else
            {
                switch (args[0])
                {
                    case "delete":
                        if (args.Length < 2)
                            DetermineReply(player, isChat, "You didn't provide a kitname... The command is kitadmin delete <kitname>", arg);
                        else
                        {
                            var kitInfo = GetKitInfo(args[1]);
                            if (kitInfo != null)
                            {
                                _kitData.RemoveAll(x => x.KitName.Equals(kitInfo.KitName, StringComparison.OrdinalIgnoreCase));
                                DetermineReply(player, isChat, "Kit deleted", arg);
                            }
                            else DetermineReply(player, isChat, "Kit not found", arg);
                        }
                        break;
                    case "give":
                        if (args.Length < 2)
                            DetermineReply(player, isChat, "You didn't provide a player... The command is kitadmin give <player> <kitname>", arg);
                        else if (args.Length < 3)
                            DetermineReply(player, isChat, "You didn't provide a kit name... The command is kitadmin give <player> <kitname>", arg);
                        else
                        {
                            var kitInfo = GetKitInfo(args[2]);
                            if (kitInfo != null)
                            {
                                BasePlayer thePlayer = GetTarget(args[1]);
                                if (thePlayer == null) DetermineReply(player, isChat, Lang("ChatNoPlayerFound", null, args[1]), arg);
                                else
                                {
                                    var claimKit = ClaimKit(thePlayer, args[2], true);
                                    SendReply(player, $"{Lang("ChatTitle")} {claimKit.Reason}");
                                }
                            }
                            else DetermineReply(player, isChat, "Kit not found", arg);
                        }
                        break;
                    case "reset":
                        _userData.Clear();
                        DetermineReply(player, isChat, "Cleared user data", arg);
                        break;
                    default:
                        DetermineReply(player, isChat, "Command not found", arg);
                        break;
                }
            }
        }

        void GiveAutoKit(BasePlayer player)
        {
            aKit kitInfo = null;
            foreach (var kit in _kitData.Where(x => x.IsAutoKit).OrderByDescending(x => x.AutoKitPriority))
            {
                if (permission.UserHasPermission(player.UserIDString, kit.Permission))
                {
                    kitInfo = kit;
                    break;
                }
            }

            if (kitInfo != null && kitInfo.Items.Count > 0)
            {
                var itemsToAdd = new List<createItem>();

                int i = 0;
                foreach (var item in kitInfo.Items)
                {
                    Item createdItem = null;
                    if (item.IsBlueprint)
                    {
                        createdItem = ItemManager.Create(Workbench.GetBlueprintTemplate(), 1, 0);
                        createdItem.blueprintTarget = item.ItemId;
                        createdItem.position = item.Position;

                        createdItem.MarkDirty();
                    }
                    else
                    {
                        createdItem = ItemManager.CreateByItemID(item.ItemId, item.Amount, item.Skin);
                        createdItem.position = item.Position;

                        if (createdItem.GetHeldEntity() != null)
                        {
                            createdItem.GetHeldEntity().skinID = item.Skin;
                            createdItem.GetHeldEntity().SendNetworkUpdateImmediate();
                        }

                        if (createdItem.info.category.ToString() == "Weapon")
                        {
                            if (item.Contents != null)
                                foreach (var attachment in item.Contents)
                                    createdItem.contents.AddItem(ItemManager.FindItemDefinition(attachment), 1);

                            if (item.AmmoType != null)
                            {
                                var weapon = createdItem.GetHeldEntity() as BaseProjectile;
                                weapon.primaryMagazine.ammoType = ItemManager.FindItemDefinition(item.AmmoType);
                                weapon.primaryMagazine.contents = item.Ammo;
                            }
                        }
                    }

                    itemsToAdd.Add(new createItem { Category = item.Container, Item = createdItem });
                    i++;
                }

                player.inventory.Strip();

                foreach (var item in itemsToAdd)
                {
                    switch (item.Category)
                    {
                        case "attire":
                            item.Item.MoveToContainer(player.inventory.containerWear, item.Item.position, true, true);
                            break;
                        case "belt":
                            item.Item.MoveToContainer(player.inventory.containerBelt, item.Item.position, true, true);
                            break;
                        case "main":
                            item.Item.MoveToContainer(player.inventory.containerMain, item.Item.position, true, true);
                            break;
                        default:
                            item.Item.MoveToContainer(player.inventory.containerMain, item.Item.position, true, true);
                            break;
                    }
                }
            }
        }

        private BasePlayer GetTarget(string identifier)
        {
            var thePlayer = BasePlayer.allPlayerList.FirstOrDefault(x =>
                x.UserIDString.Equals(identifier) ||
                x.displayName.ToLower() == identifier.ToLower() ||
                x.displayName.ToLower().Contains(identifier.ToLower())
            );
            return thePlayer;
        }

        private void DetermineReply(BasePlayer player, bool isChat, string theReply, ConsoleSystem.Arg arg = null)
        {
            if (isChat)
            {
                SendReply(player, theReply);
            }
            else
            {
                if (arg.Connection == null)
                    Interface.Oxide.LogInfo(theReply);
                else SendReply(arg, theReply);
            }
        }

        private void UnsubscribeHooks()
        {
            if (!_kitData.Any(x => x.IsAutoKit) && RespawnSubed)
            {
                Unsubscribe(nameof(OnPlayerRespawned));
                RespawnSubed = false;
            };
        }

        private void SubscribeHooks()
        {
            if (_kitData.Any(x => x.IsAutoKit) && !RespawnSubed)
            {
                Subscribe(nameof(OnPlayerRespawned));
                RespawnSubed = true;
            };
        }

        void ChangeItem(BasePlayer player, int pos, string cat, string partPartial, int type = 0)
        {
            var theItem = _kitEdits[player.userID].Items.FirstOrDefault(x => x.Position == pos && x.Container == cat);
            var wasFound = theItem != null;
            if (!wasFound) theItem = new kitContents();

            if (!string.IsNullOrEmpty(partPartial))
                switch (type)
                {
                    case 0:
                        var createdItem = ItemManager.FindItemDefinition(partPartial);
                        if (createdItem != null)
                        {
                            theItem.Shortname = partPartial;
                            theItem.ItemId = createdItem.itemid;
                            theItem.Amount = 0;
                            theItem.Skin = 0;
                        }
                        break;
                    case 1:
                        if (wasFound) theItem.Skin = ulong.Parse(partPartial);
                        break;
                    case 2:
                        if (wasFound) theItem.Amount = int.Parse(partPartial);
                        break;
                }

            theItem.Position = pos;
            theItem.Container = cat;

            if (!wasFound && !string.IsNullOrEmpty(theItem.Shortname))
            {
                theItem.Amount = 1;
                _kitEdits[player.userID].Items.Add(theItem);
            }
            HandleItemEdit(player, pos, cat);
        }

        void ErrorPopup(BasePlayer player, string err)
        {
            var container = new CuiElementContainer();
            var panel = CreatePanel(ref container, "0 .8", ".25 .95", "0 0 0 .7", "Overlay", "KCCreateErr", blur: true);
            CreatePanel(ref container, "0 0", ".01 .99", ".81 .26 .17 1", panel);
            CreateLabel(ref container, ".03 0", "1 1", "0 0 0 0", "1 1 1 1", err, 15, TextAnchor.MiddleCenter, panel);

            CuiHelper.DestroyUi(player, "KCCreateErr");
            CuiHelper.AddUi(player, container);
            timer.Once(2, () => CuiHelper.DestroyUi(player, "KCCreateErr"));
        }

        void SaveKit(BasePlayer player, bool isEdit, bKit editKit)
        {
            var parentName = string.IsNullOrEmpty(editKit.ParentName) ? string.Empty : editKit.ParentName;
            if (string.IsNullOrEmpty(editKit.KitName))
            {
                ErrorPopup(player, "You didn't provide a kit name!");
                return;
            }

            if (!parentName.Equals(editKit.KitName, StringComparison.OrdinalIgnoreCase) && _kitData.Any(x => x.KitName.Equals(editKit.KitName, StringComparison.OrdinalIgnoreCase)))
            {
                ErrorPopup(player, "A kit with this name already exists");
                return;
            }

            if (editKit.IsAutoKit && string.IsNullOrEmpty(editKit.Permission))
            {
                ErrorPopup(player, "Permission needs to be set on an autokit");
                return;
            }

            if (!string.IsNullOrEmpty(editKit.KitImage)) RegisterNewImage($"Kit{editKit.KitName}", editKit.KitImage);
            if (!string.IsNullOrEmpty(editKit.Permission)) permission.RegisterPermission(editKit.Permission, this);

            if (isEdit)
            {
                var kitPos = _kitData.FindIndex(x => x.KitName.Equals(parentName, StringComparison.OrdinalIgnoreCase));
                _kitData[kitPos] = editKit;
            }
            else _kitData.Add(editKit);

            timer.Once(.2f, () =>
            {
                CuiHelper.DestroyUi(player, "KCMainEditPanel");
                OpenKitUI(player);
            });

            SubscribeHooks();
        }

        void SaveItems(BasePlayer player)
        {
            var savedItems = new List<kitContents>();

            foreach (var item in player.inventory.containerWear.itemList) HandleItem(ref savedItems, item, "attire");

            foreach (var item in player.inventory.containerBelt.itemList) HandleItem(ref savedItems, item, "belt");

            foreach (var item in player.inventory.containerMain.itemList) HandleItem(ref savedItems, item, "main");

            _kitEdits[player.userID].Items = savedItems;
        }

        void HandleItem(ref List<kitContents> savedItems, Item item, string section)
        {
            var itemId = item.info.itemid;
            var shortname = item.info.shortname;

            if (item.IsBlueprint())
            {
                itemId = item.blueprintTargetDef.itemid;
                shortname = item.blueprintTargetDef.shortname;
            }

            if (item.info.category.ToString() == "Weapon")
            {
                var mods = new List<int>();
                if (item.contents != null) mods.AddRange(item.contents.itemList.Where(x => x.info.itemid != 0).Select(x => x.info.itemid));


                BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                if (weapon != null && weapon.primaryMagazine != null)
                    savedItems.Add(new kitContents { ItemId = itemId, Shortname = shortname, Skin = item.skin, Position = item.position, Amount = item.amount, AmmoType = weapon.primaryMagazine.ammoType.shortname, Ammo = weapon.primaryMagazine.capacity, Container = section, Contents = mods, IsBlueprint = item.IsBlueprint() });
                else savedItems.Add(new kitContents { ItemId = itemId, Shortname = shortname, Skin = item.skin, Position = item.position, Amount = item.amount, Contents = mods, Container = section, IsBlueprint = item.IsBlueprint() });
            }
            else savedItems.Add(new kitContents { ItemId = itemId, Shortname = shortname, Skin = item.skin, Position = item.position, Amount = item.amount, Container = section, IsBlueprint = item.IsBlueprint() });
        }

        string GetImage(string imageName)
        {
            return ImageLibrary?.Call<string>("GetImage", imageName, 0UL);
        }

        private bool IsBlocked(BasePlayer player)
        {
            if (NoEscape != null)
            {
                if (_config.UseNoKitsOnRaidBlock && NoEscape.Call<bool>("IsRaidBlocked", player.UserIDString)) return true;
                if (_config.UseNoKitsOnCombatBlock && NoEscape.Call<bool>("IsCombatBlocked", player.UserIDString)) return true;
            }
            return false;
        }

        private void RegisterNewImage(string imageName, string imageUrl) => ImageLibrary?.Call("AddImage", imageUrl, imageName, 0UL);

        private void RegisterImages()
        {
            Dictionary<string, string> kitImageList = new Dictionary<string, string>();
            foreach (var kit in _kitData.Where(x => !string.IsNullOrEmpty(x.KitImage)))
            {
                kitImageList.Add($"Kit{kit.KitName}", kit.KitImage);
            }

            Dictionary<string, string> imageList = new Dictionary<string, string>
            {
                { "PanelImage", _config.Images.PanelImage },
                { "EditImage", _config.Images.EditImage },
                { "KitPlaceholder", _config.Images.KitPlaceholderImage }
            };

            ImageLibrary?.Call("ImportImageList", "KitController", imageList, 0UL, true);
            ImageLibrary?.Call("ImportImageList", "KitController", kitImageList, 0UL, true);
        }

        private void RegisterCommandsAndPermissions(bool reRegister = false)
        {
            if (!usingWC) foreach (var command in _config.KitCommands) cmd.AddChatCommand(command, this, KitCommand);

            if (reRegister) return;

            foreach (var kit in _kitData)
                if (!string.IsNullOrEmpty(kit.Permission))
                {
                    if (!kit.Permission.Contains("kitcontroller.")) kit.Permission = "kitcontroller." + kit.Permission;
                    if (!permission.PermissionExists(kit.Permission, this)) permission.RegisterPermission(kit.Permission, this);
                }

            permission.RegisterPermission("kitcontroller.admin", this);
        }

        aKit GetKitInfo(string kitName)
        {
            return _kitData.FirstOrDefault(x => x.KitName.Equals(kitName, StringComparison.OrdinalIgnoreCase));
        }

        ClaimKitResponse ClaimKit(BasePlayer player, string kitName, bool fromAdmin = false, bool fromSpawn = false)
        {
            var kitInfo = GetKitInfo(kitName);
            var isAdmin = permission.UserHasPermission(player.UserIDString, "kitcontroller.admin");
            if (kitInfo == null) return new ClaimKitResponse { CanRedeem = false, Reason = Lang("ChatNotFound", null, kitName) };
            if (!fromSpawn && kitInfo.IsAutoKit && !isAdmin) return new ClaimKitResponse { CanRedeem = false, Reason = Lang("ChatIsAutoKit", null, kitName) };

            var kitUsage = GetKitUsage(player, kitName);
            var userData = GetUserData(player);
            if (!fromAdmin)
            {
                var canUseKit = CanUseKit(player, kitUsage, kitInfo);
                if (!canUseKit.CanRedeem) return new ClaimKitResponse { CanRedeem = false, Reason = canUseKit.Reason };
            }

            var couldNotAssign = new List<createItem>();
            var itemsToAdd = new List<createItem>();
            if (kitInfo.Items != null && kitInfo.Items.Count > 0)
            {
                if (fromSpawn) player.inventory.Strip();
                var mainSpaceLeft = 24 - player.inventory.containerMain.itemList.Count;
                var attireSpaceLeft = 7 - player.inventory.containerWear.itemList.Count;
                var beltSpaceLeft = 6 - player.inventory.containerBelt.itemList.Count;

                bool hasSlot7 = false;
                int usedBelt = 0, usedMain = 0, usedAttire = 0;
                int neededSpace = 0;

                foreach (var kitItem in kitInfo.Items)
                {
                    switch (kitItem.Container)
                    {
                        case "main":
                            usedMain++;
                            break;
                        case "attire":
                            if (kitItem.Position != 7) usedAttire++;
                            else hasSlot7 = true;
                            break;
                        case "belt":
                            usedBelt++;
                            break;
                    }
                }


                if (usedMain > mainSpaceLeft) return new ClaimKitResponse { CanRedeem = false, Reason = Lang("ChatTooManyItems") };
                if (usedAttire > attireSpaceLeft) neededSpace += usedAttire - attireSpaceLeft;
                if (usedBelt > beltSpaceLeft) neededSpace += usedBelt - beltSpaceLeft;

                if (hasSlot7 && player.inventory.containerWear.itemList.Any(x => x.position == 7)) neededSpace += 1;

                if (neededSpace != 0 && neededSpace > (mainSpaceLeft - usedMain)) return new ClaimKitResponse { CanRedeem = false, Reason = Lang("ChatTooManyItems") };

                foreach (var item in kitInfo.Items)
                {
                    var itemList = GetInventoryContainer(player, item.Container);

                    if (itemList == null) continue;
                    Item createdItem = null;
                    if (item.IsBlueprint)
                    {
                        createdItem = ItemManager.Create(Workbench.GetBlueprintTemplate(), 1, 0);
                        createdItem.blueprintTarget = item.ItemId;
                        createdItem.position = item.Position;

                        createdItem.MarkDirty();
                    }
                    else
                    {
                        createdItem = ItemManager.CreateByItemID(item.ItemId);

                        if (createdItem == null) continue;

                        createdItem.skin = item.Skin;
                        createdItem.amount = item.Amount;

                        if (createdItem.GetHeldEntity() != null)
                        {
                            createdItem.GetHeldEntity().skinID = createdItem.skin;
                            createdItem.GetHeldEntity().SendNetworkUpdateImmediate();
                        }

                        if (itemList.itemList.All(x => x.position != item.Position)) createdItem.position = item.Position;

                        if (createdItem.info.category.ToString() == "Weapon")
                        {
                            if (item.Contents != null)
                                foreach (var attachment in item.Contents)
                                    createdItem.contents.AddItem(ItemManager.FindItemDefinition(attachment), 1);

                            if (item.AmmoType != null)
                            {
                                var weapon = createdItem.GetHeldEntity() as BaseProjectile;
                                weapon.primaryMagazine.ammoType = ItemManager.FindItemDefinition(item.AmmoType);
                                weapon.primaryMagazine.contents = item.Ammo;
                            }
                        }
                    }

                    if (itemList.itemList.Any(x => x.position == item.Position)) couldNotAssign.Add(new createItem { Category = item.Container, Item = createdItem });
                    else itemsToAdd.Add(new createItem { Category = item.Container, Item = createdItem });
                }

                if (mainSpaceLeft < couldNotAssign.Count) return new ClaimKitResponse { CanRedeem = false, Reason = Lang("ChatTooManyItems") };

                foreach (var item in itemsToAdd) item.Item.MoveToContainer(GetInventoryContainer(player, item.Category), item.Item.position, true, true);
                foreach (var item in couldNotAssign) item.Item.MoveToContainer(player.inventory.containerMain);

                if (!fromAdmin && !fromSpawn)
                {
                    if (kitUsage == null) userData.UsedKits.Add(new UsedKits { KitName = kitInfo.KitName });
                    kitUsage = GetKitUsage(player, kitName);

                    if (kitInfo.Cooldown != 0) kitUsage.NextUseTime = DateTimeOffset.Now.ToUnixTimeSeconds() + kitInfo.Cooldown;
                    if (kitInfo.MaxUses != 0) kitUsage.TotalUses++;
                }

                return new ClaimKitResponse { CanRedeem = true, Reason = Lang("ChatKitRedeemed", null, kitName) };
            }
            else return new ClaimKitResponse { CanRedeem = false, Reason = Lang("ChatNoKitItems", null, kitName) };

        }

        ItemContainer GetInventoryContainer(BasePlayer player, string container)
        {
            ItemContainer itemList = null;
            switch (container)
            {
                case "attire":
                    itemList = player.inventory.containerWear;
                    break;
                case "belt":
                    itemList = player.inventory.containerBelt;
                    break;
                case "main":
                    itemList = player.inventory.containerMain;
                    break;
                default:
                    itemList = null;
                    break;
            }

            return itemList;
        }
        private UsedKits GetKitUsage(BasePlayer player, string kitName)
        {
            var playerUsedKits = GetUserData(player);
            var kitUsage = playerUsedKits.UsedKits.FirstOrDefault(x => x.KitName.Equals(kitName, StringComparison.OrdinalIgnoreCase));
            return kitUsage;
        }

        ClaimKitResponse CanUseKit(BasePlayer player, UsedKits kitUsage, aKit kitInfo)
        {
            var cooldown = UIGetCooldown(player, kitUsage, kitInfo);
            var maxuses = UIGetMaxUses(player, kitUsage, kitInfo);
            var isAdmin = permission.UserHasPermission(player.UserIDString, "kitcontroller.admin");
            var hasPermission = string.IsNullOrEmpty(kitInfo.Permission) ? true : permission.UserHasPermission(player.UserIDString, kitInfo.Permission);
            var wipeCooldown = UIGetWipeCooldown(player, kitUsage, kitInfo);
            if (!cooldown.Can) return new ClaimKitResponse { CanRedeem = false, Reason = Lang("ChatOnCooldown", null, kitInfo.KitName, cooldown.Time) };
            if (!maxuses.Can) return new ClaimKitResponse { CanRedeem = false, Reason = Lang("ChatMaxRedeem", null, kitInfo.KitName) };
            if (!hasPermission && !isAdmin) return new ClaimKitResponse { CanRedeem = false, Reason = Lang("ChatNoPermission", null, kitInfo.KitName) };
            if (!wipeCooldown.Can) return new ClaimKitResponse { CanRedeem = false, Reason = Lang("ChatWipeCooldown", null, kitInfo.KitName, wipeCooldown.Time) };

            if (kitInfo.Cost > 0)
            {
                var canBuy = CanBuy(player, kitInfo);
                if (!canBuy.CanBuy) return new ClaimKitResponse { CanRedeem = false, Reason = canBuy.Reason };
                BuyKit(player, kitInfo);
            }

            return new ClaimKitResponse { CanRedeem = true, Reason = null };
        }

        CanRedeem UIGetWipeCooldown(BasePlayer player, UsedKits kitUsage, aKit kitInfo)
        {
            TimeSpan cooldownTime = TimeSpan.FromSeconds(LastWipe + kitInfo.WipeCooldown - CurrentTime);
            if (kitInfo.WipeCooldown != 0 && CurrentTime < LastWipe + kitInfo.WipeCooldown)
            {
                return new CanRedeem { Can = false, Time = $"{cooldownTime.Days}d {cooldownTime.Hours}h {cooldownTime.Minutes}m {cooldownTime.Seconds}s" };
            }
            return new CanRedeem { Can = true };
        }

        CanRedeem UIGetCooldown(BasePlayer player, UsedKits kitUsage, aKit kitInfo)
        {
            if (kitUsage != null && kitInfo.Cooldown != 0 && kitUsage.NextUseTime > DateTimeOffset.Now.ToUnixTimeSeconds())
            {
                TimeSpan time = TimeSpan.FromSeconds(kitUsage.NextUseTime - DateTimeOffset.Now.ToUnixTimeSeconds());
                return new CanRedeem { Can = false, Time = $"{time}" };
            }
            else
            {
                TimeSpan time = TimeSpan.FromSeconds(kitInfo.Cooldown);
                return new CanRedeem { Can = true, Time = $"{time}" };
            }
        }

        CanRedeem UIGetMaxUses(BasePlayer player, UsedKits kitUsage, aKit kitInfo)
        {
            if (kitInfo.MaxUses == 0) return new CanRedeem { Can = true, Time = "∞" };

            if (kitUsage != null)
            {
                if (kitInfo.MaxUses <= kitUsage.TotalUses) return new CanRedeem { Can = false, Time = $"{kitUsage.TotalUses}" };
                else return new CanRedeem { Can = true, Time = $"{kitUsage.TotalUses}" };
            }
            else return new CanRedeem { Can = true, Time = "0" };
        }
        #endregion

        #region UI
        [HookMethod("ClaimKit")]
        void APIClaimKit(BasePlayer player, string[] args)
        {
            KitCommand(player, null, args);
        }

        void KitCommand(BasePlayer player, string command, string[] args)
        {
            if (IsBlocked(player))
            {
                SendReply(player, Lang("ChatTitle") + " " + Lang("IsBlocked"));
                return;
            }

            if (args != null && args.Length != 0)
            {
                var clamKit = ClaimKit(player, String.Join(" ", args));
                SendReply(player, Lang("ChatTitle") + " " + clamKit.Reason);
            }
            else OpenKitUI(player);
        }

        void OpenKitUI(BasePlayer player, string parentPanel = null)
        {
            var container = new CuiElementContainer();
            var playerOptions = GetOrLoadPlayerOptions(player);
            var isAdmin = permission.UserHasPermission(player.UserIDString, "kitcontroller.admin");

            Dictionary<string, List<string>> uiPositions;

            if (usingWC)
            {
                uiPositions = new Dictionary<string, List<string>> {
                    { "TopPanel", new List<string> { "0 .86", "1 1" } },
                    { "MiddlePanel", new List<string> { "0 .17", "1 .85" } },
                    { "BottomPanel", new List<string> { "0 .07", "1 .16" } }
                };
            }
            else
            {
                uiPositions = new Dictionary<string, List<string>> {
                    { "TopPanel", new List<string> { ".05 .82", ".95 .95" } },
                    { "MiddlePanel", new List<string> { ".05 .13", ".95 .81" } },
                    { "BottomPanel", new List<string> { ".05 .05", ".95 .12" } }
                };
            }

            parentPanel = usingWC ? "WCSourcePanel" : "Overlay";

            var panel = CreatePanel(ref container, "0 0", "1 1", usingWC ? "0 0 0 0" : _uiColors.BlurBackgroundColor, parentPanel, "KCMainPanel", true, true);
            CreatePanel(ref container, uiPositions["TopPanel"][0], uiPositions["TopPanel"][1], _uiColors.TopPanelColor, panel, "KCTopPanel");
            CreatePanel(ref container, uiPositions["MiddlePanel"][0], uiPositions["MiddlePanel"][1], _uiColors.MiddlePanelColor, panel, "KCKitPanel");
            CreatePanel(ref container, uiPositions["BottomPanel"][0], uiPositions["BottomPanel"][1], _uiColors.BottomPanelColor, panel, "KCCategoryPanel");

            if (string.IsNullOrEmpty(_config.Images.PanelImage)) CreateLabel(ref container, ".22 0", ".78 .99", "0 0 0 0", "1 1 1 1", _config.KitsTitle, 40, TextAnchor.MiddleCenter, "KCTopPanel");
            else CreateImagePanel(ref container, ".22 0", ".78 .99", GetImage("PanelImage"), "KCTopPanel");

            if (!usingWC) CreateButton(ref container, ".92 0", "1 1", "0 0 0 0", _uiColors.CloseTextColor, _uiColors.CloseText, 70, "kc_main close", "KCTopPanel");

            if (isAdmin) CreateButton(ref container, ".009 .1", ".2 .4", ".4 .63 1 1", "1 1 1 1", Lang("AdminHeaderCreateKit"), 20, "kc_main edit", "KCTopPanel");

            UICreateKitCategories(player, playerOptions, ref container);

            CuiHelper.DestroyUi(player, "KCMainPanel");
            CuiHelper.AddUi(player, container);

            UICreateKits(player, playerOptions);
        }

        void UICreateKitCategories(BasePlayer player, PlayerOptions playerOptions, ref CuiElementContainer container)
        {
            var isAdmin = permission.UserHasPermission(player.UserIDString, "kitcontroller.admin");
            var categoryList = new List<string> { "all" };

            foreach (var kit in _kitData.Where(x => !string.IsNullOrEmpty(x.Category)))
                if (!categoryList.Any(x => x.Equals(kit.Category, StringComparison.OrdinalIgnoreCase)))
                    if (isAdmin || !kit.Hidden || string.IsNullOrEmpty(kit.Permission) ? true : permission.UserHasPermission(player.UserIDString, kit.Permission))
                        categoryList.Add(kit.Category);

            if (categoryList.Count > 0)
            {
                var maxPage = categoryList.Count / 5;
                if (playerOptions.CategoryPage > maxPage) playerOptions.CategoryPage = 0;
                if (playerOptions.CategoryPage < 0) playerOptions.CategoryPage = maxPage;

                int i = 0;
                foreach (var category in categoryList.Skip(playerOptions.CategoryPage * 5).Take(5))
                {
                    var activeCat = string.IsNullOrEmpty(playerOptions.ActiveCategory) ? "all" : playerOptions.ActiveCategory;
                    CreateButton(ref container, $"{.057 + (i * .178)} .08", $"{.222 + (i * .178)} .89", activeCat.Equals(category, StringComparison.OrdinalIgnoreCase) ? _uiColors.ActiveCategoryButtonColor : _uiColors.CategoryButtonColor, "1 1 1 1", category.ToUpper(), 20, $"kc_main category {category}", "KCCategoryPanel");
                    i++;
                }

                if (categoryList.Count > 5)
                {
                    CreateButton(ref container, ".005 .08", ".045 .89", _uiColors.CategoryPageButtonColor, "1 1 1 1", "<", 25, $"kc_main categorypage {playerOptions.CategoryPage - 1}", "KCCategoryPanel");
                    CreateButton(ref container, ".948 .08", ".993 .89", _uiColors.CategoryPageButtonColor, "1 1 1 1", ">", 25, $"kc_main categorypage {playerOptions.CategoryPage + 1}", "KCCategoryPanel");
                }
                else
                {
                    CreatePanel(ref container, ".005 .08", ".05 .9", _uiColors.CategoryPageButtonColor, "KCCategoryPanel");
                    CreatePanel(ref container, ".948 .08", ".993 .9", _uiColors.CategoryPageButtonColor, "KCCategoryPanel");
                }
            }
        }

        void UICreateKits(BasePlayer player, PlayerOptions playerOptions, int page = 0, string kitName = null)
        {
            var container = new CuiElementContainer();
            var panel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "KCKitPanel", "KCKitPanelOverlay");
            var isAdmin = permission.UserHasPermission(player.UserIDString, "kitcontroller.admin");

            var kits = _kitData.Where(x => isAdmin || (!x.Hidden || string.IsNullOrEmpty(x.Permission) ? true : permission.UserHasPermission(player.UserIDString, x.Permission) && !x.IsAutoKit));
            if (!string.IsNullOrEmpty(playerOptions.ActiveCategory) && !playerOptions.ActiveCategory.Equals("all", StringComparison.OrdinalIgnoreCase)) kits = _kitData.Where(x => x.Category.Equals(playerOptions.ActiveCategory, StringComparison.OrdinalIgnoreCase) && (isAdmin || (!x.IsAutoKit && (!x.Hidden || string.IsNullOrEmpty(x.Permission) ? true : permission.UserHasPermission(player.UserIDString, x.Permission)))));

            var maxPage = kits.Count() / 6;
            if (playerOptions.KitPage > maxPage) playerOptions.KitPage = 0;
            if (playerOptions.KitPage < 0) playerOptions.KitPage = maxPage;

            if (Decimal.Divide(kits.Count(), 6) > 1)
            {
                CreateButton(ref container, ".01 .015", ".04 .978", _uiColors.KitsPageButtonColor, "1 1 1 1", "<", 20, $"kc_main kitspage {playerOptions.KitPage - 1}", panel);
                CreateButton(ref container, ".96 .015", ".99 .978", _uiColors.KitsPageButtonColor, "1 1 1 1", ">", 20, $"kc_main kitspage {playerOptions.KitPage + 1}", panel);
            }

            int i = 0;
            double topPos = .98;
            foreach (var kit in kits.Skip(playerOptions.KitPage * 6).Take(6))
            {
                if (i == 2)
                {
                    i = 0;
                    topPos -= .327;
                }

                var kitsPanel = CreatePanel(ref container, $"{.045 + (i * .46)} {topPos - .31}", $"{.495 + (i * .46)} {topPos}", _uiColors.KitDisplayPanelColor, panel);
                CreatePanel(ref container, ".01 .03", ".285 .955", _uiColors.KitImageBackgroundColor, kitsPanel);
                CreateImagePanel(ref container, ".01 .03", ".285 .955", string.IsNullOrEmpty(kit.KitImage) ? GetImage("KitPlaceholder") : GetImage($"Kit{kit.KitName}"), kitsPanel, "KCImgPanel0");

                switch (_config.KitsLayout)
                {
                    case 0:
                        UICreateLayoutZero(player, container, kitsPanel, kit, page, kitName);
                        break;
                    case 1:
                        UICreateLayoutOne(player, container, kitsPanel, kit);
                        break;
                    default:
                        UICreateLayoutZero(player, container, kitsPanel, kit, page, kitName);
                        break;
                }

                i++;
            }

            CuiHelper.DestroyUi(player, "KCKitPanelOverlay");
            CuiHelper.AddUi(player, container);
        }

        void UICreateLayoutOne(BasePlayer player, CuiElementContainer container, string kitsPanel, aKit kit)
        {
            var isAdmin = permission.UserHasPermission(player.UserIDString, "kitcontroller.admin");
            var kitUsage = GetKitUsage(player, kit.KitName);
            var coolDown = UIGetCooldown(player, kitUsage, kit);
            var uses = UIGetMaxUses(player, kitUsage, kit);
            var wipeCooldown = UIGetWipeCooldown(player, kitUsage, kit);
            bool hasPermission = isAdmin;
            if (!isAdmin) hasPermission = string.IsNullOrEmpty(kit.Permission) ? true : permission.UserHasPermission(player.UserIDString, kit.Permission);
            var canRedeem = coolDown.Can && uses.Can && hasPermission && wipeCooldown.Can;

            var label = CreateLabel(ref container, ".29 .7", ".985 .955", _uiColors.HeaderKitNameColor, _uiColors.HeaderKitNameTextColor, kit.KitName, 20, TextAnchor.MiddleCenter, kitsPanel);
            if (isAdmin) CreateImageButton(ref container, "0 0", ".113 .97", "0 0 0 0", $"kc_main edit {kit.KitName}", GetImage("EditImage"), label, "KCImgPanel1");

            var isPaid = kit.Cost > 0;

            CreateLabel(ref container, ".29 .55", ".635 .68", _uiColors.HeaderCooldownColor, _uiColors.HeaderCooldownTextColor, Lang("HeaderCooldown"), 9, TextAnchor.MiddleCenter, kitsPanel);
            CreateLabel(ref container, ".29 .23", ".635 .53", _uiColors.HeaderCooldownColor, !coolDown.Can ? _uiColors.CantClaimCooldownColor : _uiColors.CanClaimCooldownColor, kit.Cooldown == 0 ? Lang("TextNoCooldown") : coolDown.Time, 15, TextAnchor.MiddleCenter, kitsPanel);

            CreateLabel(ref container, ".64 .55", ".987 .68", _uiColors.HeaderMaxUsesColor, _uiColors.HeaderMaxUsesTextColor, Lang("HeaderMaxUses"), 9, TextAnchor.MiddleCenter, kitsPanel);
            CreateLabel(ref container, ".64 .23", ".987 .53", _uiColors.HeaderMaxUsesColor, !uses.Can ? _uiColors.CantClaimMaxUsesColor : _uiColors.CanClaimMaxUsesColor, kit.MaxUses == 0 ? uses.Time : uses.Time + " / " + kit.MaxUses, 15, TextAnchor.MiddleCenter, kitsPanel);

            CreateButton(ref container, ".46 .03", ".985 .20", canRedeem ? _uiColors.CanClaimButtonColor : _uiColors.CantClaimButtonColor, canRedeem ? _uiColors.CanClaimTextColor : _uiColors.CantClaimTextColor, isPaid ? GetCurrencyType(kit) : Lang("ButtonClaim"), 19, $"kc_kit claim {kit.KitName}", kitsPanel);
            CreateButton(ref container, ".29 .03", ".45 .20", _uiColors.ViewButtonColor, _uiColors.ViewTextColor, Lang("ButtonView"), 19, $"kc_kit view {kit.KitName}", kitsPanel);

            if (!hasPermission && !isAdmin) UICreatePermissionOverlay(ref container, kitsPanel, kit);
        }

        void UICreateLayoutZero(BasePlayer player, CuiElementContainer container, string kitsPanel, aKit kit, int page = 0, string kitName = null)
        {
            var isAdmin = permission.UserHasPermission(player.UserIDString, "kitcontroller.admin");
            var maxPage = kit.Items.Count / 8;
            var kitUsage = GetKitUsage(player, kit.KitName);
            var coolDown = UIGetCooldown(player, kitUsage, kit);
            var wipeCooldown = UIGetWipeCooldown(player, kitUsage, kit);
            var uses = UIGetMaxUses(player, kitUsage, kit);
            bool hasPermission = isAdmin;
            if (!isAdmin) hasPermission = string.IsNullOrEmpty(kit.Permission) ? true : permission.UserHasPermission(player.UserIDString, kit.Permission);
            var canRedeem = coolDown.Can && uses.Can && hasPermission && wipeCooldown.Can;

            if (!kit.KitName.Equals(kitName, StringComparison.OrdinalIgnoreCase)) page = 0;
            else
            {
                if (page < 0) page = maxPage;
                if (page > maxPage) page = 0;
            }

            var isPaid = kit.Cost > 0;

            var label = CreateLabel(ref container, ".29 .77", ".795 .955", _uiColors.HeaderKitNameColor, _uiColors.HeaderKitNameTextColor, kit.KitName, 20, TextAnchor.MiddleCenter, kitsPanel);
            if (isAdmin) CreateImageButton(ref container, "0 0", ".113 .97", "0 0 0 0", $"kc_main edit {kit.KitName}", GetImage("EditImage"), label, "KCImgPanel2");

            CreateLabel(ref container, ".29 .65", ".539 .75", _uiColors.HeaderCooldownColor, _uiColors.HeaderCooldownTextColor, Lang("HeaderCooldown"), 9, TextAnchor.MiddleCenter, kitsPanel);
            CreateLabel(ref container, ".29 .43", ".539 .63", _uiColors.HeaderCooldownColor, !coolDown.Can ? _uiColors.CantClaimCooldownColor : _uiColors.CanClaimCooldownColor, kit.Cooldown == 0 ? Lang("TextNoCooldown") : coolDown.Time, 15, TextAnchor.MiddleCenter, kitsPanel);

            CreateLabel(ref container, ".546 .65", ".795 .75", _uiColors.HeaderMaxUsesColor, _uiColors.HeaderMaxUsesTextColor, Lang("HeaderMaxUses"), 9, TextAnchor.MiddleCenter, kitsPanel);
            CreateLabel(ref container, ".546 .43", ".795 .63", _uiColors.HeaderMaxUsesColor, !uses.Can ? _uiColors.CantClaimMaxUsesColor : _uiColors.CanClaimMaxUsesColor, kit.MaxUses == 0 ? uses.Time : uses.Time + " / " + kit.MaxUses, 15, TextAnchor.MiddleCenter, kitsPanel);

            CreateButton(ref container, ".802 .77", ".985 .948", canRedeem ? _uiColors.CanClaimButtonColor : _uiColors.CantClaimButtonColor, canRedeem ? _uiColors.CanClaimTextColor : _uiColors.CantClaimTextColor, isPaid ? GetCurrencyType(kit) : Lang("ButtonClaim"), isPaid ? 14 : 19, $"kc_kit claim {kit.KitName}", kitsPanel);
            CreateButton(ref container, ".802 .565", ".985 .744", _uiColors.ViewButtonColor, _uiColors.ViewTextColor, Lang("ButtonView"), 19, $"kc_kit view {kit.KitName}", kitsPanel);

            CreatePanel(ref container, ".802 .43", ".986 .545", _uiColors.KitsLayoutOneBlankBoxColor, kitsPanel);

            CreateButton(ref container, ".29 .03", ".5825 .12", _uiColors.KitsItemPageButtonColor, "1 1 1 1", "<", 9, $"kc_kit itemspage {page - 1} {kit.KitName}", kitsPanel);

            CreateLabel(ref container, ".591 .03", ".685 .128", _uiColors.KitsItemPageButtonColor, "1 1 1 1", $"{page + 1} / {maxPage + 1}", 9, TextAnchor.MiddleCenter, kitsPanel);

            CreateButton(ref container, ".6925 .03", ".985 .12", _uiColors.KitsItemPageButtonColor, "1 1 1 1", ">", 9, $"kc_kit itemspage {page + 1} {kit.KitName}", kitsPanel);

            UIKitItems(container, kitsPanel, kit, page, kitName);
            if (!hasPermission && !isAdmin) UICreatePermissionOverlay(ref container, kitsPanel, kit);
        }

        void UICreatePermissionOverlay(ref CuiElementContainer container, string panel, aKit kit)
        {
            var overlayPanel = CreatePanel(ref container, "0 0", "1 1", _uiColors.NoPermissionOverlayColor, panel, blur: true);
            CreateLabel(ref container, "0 0", "1 1", "0 0 0 0", "1 1 1 1", String.IsNullOrEmpty(kit.NoPermissionText) ? " " : kit.NoPermissionText, 20, TextAnchor.MiddleCenter, overlayPanel);

            CreateButton(ref container, ".3 .05", ".7 .25", _uiColors.NoPermsViewButtonColor, _uiColors.NoPermsViewTextColor, Lang("ButtonView"), 20, $"kc_kit view {kit.KitName}", overlayPanel);
        }

        void UIKitItems(CuiElementContainer container, string panel, aKit kit, int page, string kitName)
        {
            int i = 0;

            var kitItems = kit.KitName.Equals(kitName, StringComparison.OrdinalIgnoreCase) ? kit.Items.Skip(page * 8).Take(8) : kit.Items.Take(8);
            foreach (var item in kitItems)
            {
                if (item.IsBlueprint) CreateItemPanel(ref container, $"{.29 + (i * .088)} .15", $"{.37 + (i * .088)} .41", 0f, "0 0 0 0", -996920608, panel);
                CreateItemPanel(ref container, $"{.29 + (i * .088)} .15", $"{.37 + (i * .088)} .41", 0f, _uiColors.KitsItemsColor, item.ItemId, panel, skinId: item.Skin);
                i++;
            }
        }

        void UICreateMockInventory(ref CuiElementContainer container, BasePlayer player, string panel, List<kitContents> mainInventory, List<kitContents> hotbar, List<kitContents> attire, bool viewPanel = false)
        {
            var row = 0;
            var ind = 0;
            for (int i = 0; i < 24; i++)
            {
                if (ind == 6)
                {
                    row++;
                    ind = 0;
                }
                var itemPanel = CreatePanel(ref container, $"{.06 + (ind * .145)} {.78 - (row * .15)}", $"{.195 + (ind * .145)} {.92 - (row * .15)}", _uiColors.MockInventoryItemColor, panel);
                UIHandlePossibleitem(ref container, mainInventory, i, itemPanel, "main", viewPanel);

                ind++;
            }

            for (int i = 0; i < 8; i++)
            {
                var itemPanel = CreatePanel(ref container, $"{0 + ((i != 7 ? i : i - 1) * .145)} {(i != 7 ? .18 : .03)}", $"{.135 + ((i != 7 ? i : i - 1) * .145)} {(i != 7 ? .32 : .17)}", _uiColors.MockInventoryItemColor, panel);
                UIHandlePossibleitem(ref container, attire, i, itemPanel, "attire", viewPanel);
            }

            for (int i = 0; i < 6; i++)
            {
                var itemPanel = CreatePanel(ref container, $"{0 + (i * .145)} .03", $"{.135 + (i * .145)} .17", _uiColors.MockInventoryItemColor, panel);
                UIHandlePossibleitem(ref container, hotbar, i, itemPanel, "belt", viewPanel);
            }
        }

        void UIHandlePossibleitem(ref CuiElementContainer container, List<kitContents> itemlist, int pos, string itemPanel, string part, bool viewPanel)
        {
            var item = itemlist.FirstOrDefault(x => x.Position == pos);
            if (item != null)
            {
                if (item.IsBlueprint) CreateItemPanel(ref container, ".03 0", ".97 1", 0f, "0 0 0 0", -996920608, itemPanel);
                var itmAmt = $"x{(item.Amount > 9999999 ? $"9999999+" : $"{item.Amount}")}";
                CreateItemPanel(ref container, "0 0", "1 1", .05f, "0 0 0 0", item.ItemId, itemPanel, skinId: item.Skin);
                if (item.Amount > 1) CreateLabel(ref container, $"{(!viewPanel ? "0" : ".02")} 0", "1 .3", "0 0 0 0", "1 1 1 1", itmAmt, viewPanel ? 15 : 10, TextAnchor.LowerLeft, itemPanel);
            }

            if (!viewPanel) CreateButton(ref container, "0 0", "1 1", "0 0 0 0", "1 1 1 1", " ", 15, $"kc_edit selectitem {pos} {part}", itemPanel);
        }
        #endregion

        #region Edit Kits UI
        void UICreateEditPage(BasePlayer player)
        {
            Dictionary<string, List<string>> uiPositions;
            if (usingWC)
            {
                uiPositions = new Dictionary<string, List<string>> {
                    { "TopPanel", new List<string> { "0 .86", "1 1" } },
                    { "MiddlePanel", new List<string> { "0 .17", "1 .85" } },
                    { "BottomPanel", new List<string> { "0 .07", "1 .16" } }
                };
            }
            else
            {
                uiPositions = new Dictionary<string, List<string>> {
                    { "TopPanel", new List<string> { ".05 .82", ".95 .95" } },
                    { "MiddlePanel", new List<string> { ".05 .13", ".95 .81" } },
                    { "BottomPanel", new List<string> { ".05 .05", ".95 .12" } }
                };
            }

            var container = new CuiElementContainer();
            var panel = CreatePanel(ref container, "0 0", "1 1", usingWC ? "0 0 0 0" : _uiColors.BlurBackgroundColor, usingWC ? "WCSourcePanel" : "Overlay", "KCMainEditPanel", true, true);
            CreatePanel(ref container, uiPositions["TopPanel"][0], uiPositions["TopPanel"][1], _uiColors.EditPageMainColor, panel, "KCTopPanel");
            CreatePanel(ref container, uiPositions["MiddlePanel"][0], uiPositions["MiddlePanel"][1], _uiColors.EditPageMainColor, panel, "KCEditPanel");
            CreateButton(ref container, ".92 0", "1 1", "0 0 0 0", _uiColors.CloseTextColor, _uiColors.CloseText, 70, "kc_main editclose", "KCTopPanel");

            CuiHelper.DestroyUi(player, "KCMainEditPanel");
            CuiHelper.AddUi(player, container);

            UICreateEditOverlayPage(player);
        }

        void UICreateEditOverlayPage(BasePlayer player)
        {
            var container = new CuiElementContainer();
            var panel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "KCEditPanel", "KCOverlayEditPanel");
            var editKit = _kitEdits[player.userID];

            var kitInfo = _kitData?.FirstOrDefault(x => x.KitName == editKit.ParentName);
            if (kitInfo == null) kitInfo = new aKit();

            var kitImage = string.Empty;
            if (string.IsNullOrEmpty(editKit.KitImage)) kitImage = GetImage("KitPlaceholder");
            else if (editKit.KitImage == kitInfo.KitImage) kitImage = GetImage($"Kit{editKit.KitName}");
            else kitImage = GetImage($"KitEdit{editKit.KitName}");

            CreateLabel(ref container, ".003 .93", ".25 .99", _uiColors.EditPageSecondaryColor, "1 1 1 1", "KIT NAME", 18, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".003 .78", ".25 .92", "kc_edit name", _uiColors.EditPageSecondaryColor, "1 1 1 1", !string.IsNullOrEmpty(editKit.KitName) ? editKit.KitName : " ", 20, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".253 .93", ".399 .99", _uiColors.EditPageSecondaryColor, "1 1 1 1", "KIT COODLOWN (SEC)", 15, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".253 .78", ".399 .92", "kc_edit cooldown", _uiColors.EditPageSecondaryColor, "1 1 1 1", $"{editKit.Cooldown}", 20, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".402 .93", ".548 .99", _uiColors.EditPageSecondaryColor, "1 1 1 1", "KIT MAX USES", 18, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".402 .78", ".548 .92", "kc_edit maxuses", _uiColors.EditPageSecondaryColor, "1 1 1 1", $"{editKit.MaxUses}", 20, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".551 .93", ".697 .99", _uiColors.EditPageSecondaryColor, "1 1 1 1", "WIPE COOLDOWN (SEC)", 15, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".551 .78", ".697 .92", "kc_edit wipecooldown", _uiColors.EditPageSecondaryColor, "1 1 1 1", $"{editKit.WipeCooldown}", 20, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".4765 .71", ".697 .77", _uiColors.EditPageSecondaryColor, "1 1 1 1", "KIT PERMISSION", 18, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".4765 .56", ".697 .70", "kc_edit permission", _uiColors.EditPageSecondaryColor, "1 1 1 1", !string.IsNullOrEmpty(editKit.Permission) ? editKit.Permission : " ", 20, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".4765 .49", ".697 .55", _uiColors.EditPageSecondaryColor, "1 1 1 1", "KIT IMAGE", 18, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".4765 .34", ".697 .48", "kc_edit image", _uiColors.EditPageSecondaryColor, "1 1 1 1", !string.IsNullOrEmpty(editKit.KitImage) ? editKit.KitImage : " ", 20, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".128 .71", ".25 .77", _uiColors.EditPageSecondaryColor, "1 1 1 1", "AUTOKIT PRIORITY", 12, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".128 .56", ".25 .70", "kc_edit autokitpriority", _uiColors.EditPageSecondaryColor, "1 1 1 1", $"{editKit.AutoKitPriority}", 25, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".253 .71", ".4735 .77", _uiColors.EditPageSecondaryColor, "1 1 1 1", "KIT CATEGORY", 20, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".253 .56", ".4735 .70", "kc_edit category", _uiColors.EditPageSecondaryColor, "1 1 1 1", !string.IsNullOrEmpty(editKit.Category) ? editKit.Category : "All", 20, TextAnchor.MiddleCenter, panel);
            if (_kitData.Count > 0) CreateButton(ref container, ".253 .6", ".28 .66", "0 0 0 0", "1 1 1 1", "+", 20, "kc_edit categoryui", panel);

            CreateButton(ref container, ".003 .175", ".2495 .33", ".3 1 .35 .6", "1 1 1 1", "SAVE KIT", 27, "kc_edit savekit", panel);

            CreateButton(ref container, ".003 .01", ".2495 .165", "1 .14 .14 .6", "1 .14 .14 .7", "DELETE KIT", 27, "kc_edit deletekit", panel);

            var imgPanel = CreatePanel(ref container, ".4765 .01", ".697 .33", _uiColors.EditPageSecondaryColor, panel);
            CreateImagePanel(ref container, ".21 .04", ".79 .96", kitImage, imgPanel, "KCImgPanel3");

            CreateLabel(ref container, ".003 .71", ".125 .77", _uiColors.EditPageSecondaryColor, "1 1 1 1", "IS AUTOKIT", 18, TextAnchor.MiddleCenter, panel);
            var kitAutoKit = CreatePanel(ref container, ".003 .56", ".125 .70", _uiColors.EditPageSecondaryColor, panel);
            CreateButton(ref container, !editKit.IsAutoKit ? ".02 .05" : ".505 .05", !editKit.IsAutoKit ? ".495 .92" : ".96 .92", !editKit.IsAutoKit ? "1 .19 .19 1" : ".19 1 .3 1", "1 1 1 1", " ", 15, "kc_edit autokit", kitAutoKit);

            CreateLabel(ref container, ".70 .93", ".991 .99", _uiColors.EditPageSecondaryColor, "1 1 1 1", "KIT HIDDEN", 18, TextAnchor.MiddleCenter, panel);
            var kitHidden = CreatePanel(ref container, ".70 .78", ".991 .92", _uiColors.EditPageSecondaryColor, panel);
            CreateButton(ref container, !editKit.Hidden ? ".01 .05" : ".505 .05", !editKit.Hidden ? ".495 .92" : ".984 .92", !editKit.Hidden ? "1 .19 .19 1" : ".19 1 .3 1", "1 1 1 1", " ", 15, "kc_edit hidden", kitHidden);

            CreateLabel(ref container, ".003 .49", ".25 .55", _uiColors.EditPageSecondaryColor, "1 1 1 1", "KIT DESCRIPTION", 18, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".003 .34", ".25 .48", "kc_edit description", _uiColors.EditPageSecondaryColor, "1 1 1 1", !string.IsNullOrEmpty(editKit.Description) ? editKit.Description : " ", 15, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".253 .49", ".4735 .55", _uiColors.EditPageSecondaryColor, "1 1 1 1", "NO PERM TEXT", 18, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".253 .34", ".4735 .48", "kc_edit nopermtext", _uiColors.EditPageSecondaryColor, "1 1 1 1", !string.IsNullOrEmpty(editKit.NoPermissionText) ? editKit.NoPermissionText : " ", 15, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".253 .29", ".4735 .33", _uiColors.EditPageSecondaryColor, "1 1 1 1", "COST", 15, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".253 .18", ".4735 .28", "kc_edit cost", _uiColors.EditPageSecondaryColor, "1 1 1 1", $"{editKit.Cost}", 15, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".253 .13", ".4735 .17", _uiColors.EditPageSecondaryColor, "1 1 1 1", "CURRENCY", 15, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".253 .01", ".4735 .12", "kc_edit currency", _uiColors.EditPageSecondaryColor, "1 1 1 1", !string.IsNullOrEmpty(editKit.Currency) ? editKit.Currency : " ", 15, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".70 .71", ".991 .77", _uiColors.EditPageSecondaryColor, "1 1 1 1", "KIT INVENTORY", 18, TextAnchor.MiddleCenter, panel);
            var itemsPanel = CreatePanel(ref container, ".7 .1", ".99 .75", "0 0 0 0", panel, "KCEditItemsDisplay");


            List<kitContents> mainInventory = editKit.Items.Where(x => x.Container == "main").ToList();
            List<kitContents> hotbar = editKit.Items.Where(x => x.Container == "belt").ToList();
            List<kitContents> attire = editKit.Items.Where(x => x.Container == "attire").ToList();

            CreateButton(ref container, ".70 .01", ".991 .107", ".3 1 .35 .6", "1 1 1 1", "UPLOAD INVENTORY", 25, "kc_edit kitinventory", panel);
            UICreateMockInventory(ref container, player, itemsPanel, mainInventory, hotbar, attire);

            CuiHelper.DestroyUi(player, "KCOverlayEditPanel");
            CuiHelper.AddUi(player, container);
        }

        private void UICategoryDropdownFunctionality(BasePlayer player)
        {
            var container = new CuiElementContainer();

            var categoryList = new List<string> { "CLOSE", "All" };
            foreach (var kit in _kitData.Where(x => !string.IsNullOrEmpty(x.Category)))
                if (!categoryList.Any(x => x.Equals(kit.Category, StringComparison.OrdinalIgnoreCase)))
                    categoryList.Add(kit.Category);

            Decimal value = Decimal.Divide(0.94m - (categoryList.Count - 1) * 0.04m, categoryList.Count());
            CreatePanel(ref container, $".253 {.52 - (categoryList.Count * .06)}", $".4735 .55", ".17 .17 .17 1", "KCOverlayEditPanel", "KCCategoryDropdown");

            var i = 0;
            foreach (var category in categoryList)
            {
                Decimal startY = (value + 0.04m) * i + .03m;
                Decimal endY = startY + value;
                CreateButton(ref container, $".02 {startY}", $".97 {endY}", category == "CLOSE" ? "1 .14 .14 .6" : "1 1 1 .08", category == "CLOSE" ? "1 .14 .14 .7" : "1 1 1 1", category.ToUpper(), 19, $"kc_edit categoryselect {category}", "KCCategoryDropdown");
                i++;
            }

            CuiHelper.DestroyUi(player, "KCCategoryDropdown");
            CuiHelper.AddUi(player, container);
        }

        void HandleItemEdit(BasePlayer player, int pos, string cat)
        {
            var container = new CuiElementContainer();
            var theItem = new kitContents();
            theItem = _kitEdits[player.userID].Items.FirstOrDefault(x => x.Position == pos && x.Container == cat);
            var createdItem = theItem != null ? ItemManager.FindItemDefinition(theItem.Shortname) : null;

            var panel = CreatePanel(ref container, usingWC ? "0 .17" : ".05 .13", usingWC ? ".7 .85" : ".678 .81", ".17 .17 .17 1", "KCMainEditPanel", "KCMainItemEdit");
            CreatePanel(ref container, ".025 .6", ".25 .97", "1 1 1 .1", panel);
            if (theItem != null) CreateItemPanel(ref container, ".025 .6", ".25 .97", 0f, "1 1 1 0", theItem.ItemId, panel, skinId: theItem.Skin);

            CreateLabel(ref container, ".26 .735", ".975 .97", "1 1 1 .1", "1 1 1 1", createdItem != null ? createdItem.displayName.english : " ", 50, TextAnchor.MiddleCenter, panel);

            var shortnameInput = CreatePanel(ref container, ".26 .6", ".975 .72", "1 1 1 .1", panel);
            CreateLabel(ref container, "0 0", "1 1", "0 0 0 0", "1 1 1 .02", "INPUT ITEM SHORTNAME", 35, TextAnchor.MiddleCenter, shortnameInput);
            CreateInput(ref container, "0 0", "1 1", $"kc_edit shortname {pos} {cat}", "1 1 1 0", "1 1 1 1", createdItem != null ? createdItem.shortname : " ", 40, TextAnchor.MiddleCenter, shortnameInput);

            var skinIdInput = CreatePanel(ref container, ".025 .45", ".495 .585", "1 1 1 .1", panel);
            CreateLabel(ref container, "0 0", "1 1", "0 0 0 0", "1 1 1 .02", "SKIN ID", 45, TextAnchor.MiddleCenter, skinIdInput);
            CreateInput(ref container, "0 0", "1 1", $"kc_edit skin {pos} {cat}", "1 1 1 0", "1 1 1 1", theItem != null ? $"{theItem.Skin}" : " ", 40, TextAnchor.MiddleCenter, skinIdInput);

            var amountInput = CreatePanel(ref container, ".505 .45", ".975 .585", "1 1 1 .1", panel);
            CreateLabel(ref container, "0 0", "1 1", "0 0 0 0", "1 1 1 .02", "ITEM AMOUNT", 45, TextAnchor.MiddleCenter, amountInput);
            CreateInput(ref container, "0 0", "1 1", $"kc_edit amount {pos} {cat}", "1 1 1 0", "1 1 1 1", theItem != null ? $"{theItem.Amount}" : " ", 40, TextAnchor.MiddleCenter, amountInput);

            CreateButton(ref container, ".025 .3", ".975 .435", "1 .14 .14 .6", "1 .14 .14 .7", "DELETE ITEM", 40, $"kc_edit deleteitem {pos} {cat}", panel);

            CreateButton(ref container, ".025 .03", ".975 .285", "1 1 1 .1", "1 1 1 1", "CLOSE", 40, "kc_edit closeitem", panel);

            CuiHelper.DestroyUi(player, "KCMainItemEdit");
            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region UI View Kit
        void UICreateViewPage(BasePlayer player, string kitName)
        {
            var container = new CuiElementContainer();
            var kitInfo = GetKitInfo(kitName);
            var panel = CreatePanel(ref container, "0 0", "1 1", _uiColors.BlurBackgroundColor, "Overlay", "KCMainViewPanel", true, true);
            CreatePanel(ref container, ".05 .82", ".95 .95", _uiColors.ViewPageMainColor, panel, "KCTopPanel");
            var mainPanel = CreatePanel(ref container, ".05 .13", ".95 .81", _uiColors.ViewPageMainColor, panel, "KCViewPanel");
            CreateLabel(ref container, "0 0", "1 1", "0 0 0 0", "1 1 1 1", Lang("KitViewTitle"), 55, TextAnchor.MiddleCenter, "KCTopPanel");
            CreateButton(ref container, ".92 0", "1 1", "0 0 0 0", _uiColors.CloseTextColor, _uiColors.CloseText, 70, "kc_kit viewclose", "KCTopPanel");

            if (kitInfo != null)
            {
                List<kitContents> mainInventory = kitInfo.Items.Where(x => x.Container == "main").ToList();
                List<kitContents> hotbar = kitInfo.Items.Where(x => x.Container == "belt").ToList();
                List<kitContents> attire = kitInfo.Items.Where(x => x.Container == "attire").ToList();
                var kitUsage = GetKitUsage(player, kitInfo.KitName);
                var coolDown = UIGetCooldown(player, kitUsage, kitInfo);
                var maxUses = UIGetMaxUses(player, kitUsage, kitInfo);
                var isAdmin = permission.UserHasPermission(player.UserIDString, "kitcontroller.admin");
                bool hasPermission = isAdmin;
                if (!isAdmin) hasPermission = string.IsNullOrEmpty(kitInfo.Permission) ? true : permission.UserHasPermission(player.UserIDString, kitInfo.Permission);
                var canRedeem = coolDown.Can && maxUses.Can && hasPermission;

                var itemsPanel = CreatePanel(ref container, ".53 .04", ".96 1", "0 0 0 0", mainPanel, "KCViewKitItems");
                CreateLabel(ref container, ".01 .8", ".5 .98", _uiColors.ViewPageSecondaryColor, "1 1 1 1", kitInfo.KitName, 55, TextAnchor.MiddleCenter, mainPanel);

                CreateLabel(ref container, ".01 .73", ".255 .79", _uiColors.ViewPageSecondaryColor, _uiColors.HeaderCooldownTextColor, Lang("HeaderCooldown"), 20, TextAnchor.MiddleCenter, mainPanel);
                CreateLabel(ref container, ".01 .55", ".255 .72", _uiColors.ViewPageSecondaryColor, !coolDown.Can ? _uiColors.CantClaimCooldownColor : _uiColors.CanClaimCooldownColor, kitInfo.Cooldown == 0 ? Lang("TextNoCooldown") : coolDown.Time, 25, TextAnchor.MiddleCenter, mainPanel);

                CreateLabel(ref container, ".26 .73", ".5 .79", _uiColors.ViewPageSecondaryColor, _uiColors.HeaderMaxUsesTextColor, Lang("HeaderMaxUses"), 20, TextAnchor.MiddleCenter, mainPanel);
                CreateLabel(ref container, ".26 .55", ".5 .72", _uiColors.ViewPageSecondaryColor, !maxUses.Can ? _uiColors.CantClaimMaxUsesColor : _uiColors.CanClaimMaxUsesColor, kitInfo.MaxUses == 0 ? maxUses.Time : maxUses.Time + " / " + kitInfo.MaxUses, 25, TextAnchor.MiddleCenter, mainPanel);

                CreateLabel(ref container, ".01 .48", ".255 .54", _uiColors.ViewPageSecondaryColor, "1 1 1 1", Lang("HeaderDescription"), 20, TextAnchor.MiddleCenter, mainPanel);
                CreateLabel(ref container, ".01 .2", ".255 .47", _uiColors.ViewPageSecondaryColor, "1 1 1 1", kitInfo.Description, 15, TextAnchor.MiddleCenter, mainPanel);

                CreateLabel(ref container, ".26 .48", ".5 .54", _uiColors.ViewPageSecondaryColor, "1 1 1 1", Lang("HeaderImage"), 20, TextAnchor.MiddleCenter, mainPanel);
                CreatePanel(ref container, ".26 .2", ".5 .47", _uiColors.ViewPageSecondaryColor, mainPanel);
                CreateImagePanel(ref container, ".32 .205", ".44 .465", String.IsNullOrEmpty(kitInfo.KitImage) ? GetImage("KitPlaceholder") : GetImage($"Kit{kitInfo.KitName}"), mainPanel, "KCImgPanel4");

                CreateButton(ref container, ".01 .02", ".5 .188", canRedeem ? _uiColors.CanClaimButtonColor : _uiColors.CantClaimButtonColor, canRedeem ? _uiColors.CanClaimTextColor : _uiColors.CantClaimTextColor, Lang("ButtonClaim"), 45, $"kc_kit claim {kitInfo.KitName}", mainPanel);

                UICreateMockInventory(ref container, player, itemsPanel, mainInventory, hotbar, attire, true);
            }

            CuiHelper.DestroyUi(player, "KCMainViewPanel");
            CuiHelper.AddUi(player, container);

        }
        #endregion

        #region Data Handling
        private PlayerOptions GetOrLoadPlayerOptions(BasePlayer player)
        {
            if (!_playerOptions.ContainsKey(player.userID))
            {
                _playerOptions[player.userID] = new PlayerOptions { ActiveCategory = null, KitPage = 0 };
            }
            return _playerOptions[player.userID];
        }

        private void LoadKitData()
        {
            var kitList = Interface.GetMod().DataFileSystem.ReadObject<List<aKit>>($"KitController{Path.DirectorySeparatorChar}kitData");
            if (kitList == null) kitList = new List<aKit>();

            foreach (var kit in kitList.Where(x => x.Category == null))
                kit.Category = "all";

            _kitData = kitList;
        }

        private void LoadUserData()
        {
            var userData = Interface.GetMod().DataFileSystem.ReadObject<List<PlayerUsedKits>>($"KitController{Path.DirectorySeparatorChar}userData");
            if (userData == null) userData = new List<PlayerUsedKits>();

            _userData = userData;
            SaveUserList();
        }

        private PlayerUsedKits GetUserData(BasePlayer player)
        {
            if (!_userData.Any(x => x.SteamId == player.userID))
            {
                _userData.Add(new PlayerUsedKits { SteamId = player.userID, UsedKits = new List<UsedKits>() });
            }
            return _userData.FirstOrDefault(x => x.SteamId == player.userID);
        }

        private void SaveUserList()
        {
            Interface.GetMod().DataFileSystem.WriteObject($"KitController{Path.DirectorySeparatorChar}userData", _userData);
        }

        private void SaveKitList()
        {
            Interface.GetMod().DataFileSystem.WriteObject($"KitController{Path.DirectorySeparatorChar}kitData", _kitData);
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
