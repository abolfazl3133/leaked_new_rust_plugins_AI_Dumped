using Oxide.Game.Rust.Cui;
using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using System.IO;
using Newtonsoft.Json;
using System.Collections;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Facepunch.Extend;
using Facepunch;
using System.Reflection;

namespace Oxide.Plugins
{
    [Info("SkinController", "Amino", "2.0.10")]
    [Description("An advanced skin system for Rust")]
    public class SkinController : RustPlugin
    {
        [PluginReference] private Plugin Backpacks, Kits, KitController, WelcomeController, ImageLibrary;

        #region Config
        public class SkinnerConfig
        {
            [JsonProperty(PropertyName = "Steam API Key (https://steamcommunity.com/dev/apikey)")]
            public string SteamApiKey { get; set; } = "";
            public SkinCommands Commands { get; set; } = new SkinCommands();
            [JsonProperty(PropertyName = "UI Performance mod")]
            public bool UIPerformanceMode { get; set; } = true;
            [JsonProperty(PropertyName = "Permission: Max outfits (Recommend to keep to a low amount)")]
            public Dictionary<string, int> OutfitPermissions { get; set; } = new Dictionary<string, int>();
            [JsonProperty(PropertyName = "Allow team leaders to skin entire team")]
            public bool AllowSkinTeam { get; set; } = true;
            [JsonProperty(PropertyName = "Skin base cooldown (seconds)")]
            public int BaseSkinCooldownSeconds { get; set; } = 60;
            [JsonProperty(PropertyName = "Skin item cooldown (seconds)")]
            public int ItemSkinCooldownSeconds { get; set; } = 1;
            [JsonProperty(PropertyName = "Wait time between skinning new item (Skin base command)")]
            public float BaseSkinWait { get; set; } = 0.10f;
            [JsonProperty(PropertyName = "Change item name to skin name")]
            public bool ChangeItemName = true;
            [JsonProperty(PropertyName = "Allow skinning items on craft")]
            public bool SkinItemsOnCraft { get; set; } = true;
            [JsonProperty(PropertyName = "Allow skinning items on pickup")]
            public bool SkinItemsOnPickup { get; set; } = true;
            [JsonProperty(PropertyName = "Allow skinning backpack items")]
            public bool SkinItemsInBackpack { get; set; } = true;
            [JsonProperty(PropertyName = "Allow skinning kits on redeemed")]
            public bool SkinKitsOnRedeemed { get; set; } = true;
            [JsonProperty(PropertyName = "Play sound on skin")]
            public bool SoundOnSkin { get; set; } = true;
            [JsonProperty(PropertyName = "UI Title (UIImage will take priority if present)")]
            public string UITitle { get; set; } = "SKINS";
            [JsonProperty(PropertyName = "UI Banner (810 x 88)")]
            public string UIImage { get; set; } = "https://i.ibb.co/0mPRNQD/SkinsP2.png";
            [JsonProperty(PropertyName = "Settings icon")]
            public string SettingsIcon = "https://i.ibb.co/ckPQhd1/Settings-Cog.png";
            [JsonProperty(PropertyName = "Edit icon")]
            public string EditIcon = "https://i.ibb.co/YkcgybW/Pencil-Icon.png";
            [JsonProperty(PropertyName = "Invalid item icon")]
            public string InvalidItemIcon = "https://i.ibb.co/LZXNRth/chk.png";
            [JsonProperty(PropertyName = "If skin id is listed below, an item with that skinID cannot be skinned")]
            public List<string> DissallowedSkinIDs { get; set; } = new List<string>();
            [JsonProperty(PropertyName = "Default Outfit")]
            public List<OutfitDetail> DefaultOutfit { get; set; } = new List<OutfitDetail>();
            public UIElements UISettings = new UIElements();
            [JsonProperty(PropertyName = "Workshop skins (Manually added)")]
            public List<SkinData> AddedSkins { get; set; } = new List<SkinData>();
            [JsonProperty(PropertyName = "Blacklisted Skins")]
            public List<ulong> BlacklistedSkins { get; set; } = new List<ulong>();
            [JsonProperty(PropertyName = "Disallowed skinning of items with x skin ID (If an item has this skin ID it cannot be skinned)")]
            public List<ulong> DisallowedSkinIds { get; set; } = new List<ulong>();
            public static SkinnerConfig DefaultConfig()
            {
                return new SkinnerConfig
                {
                    Commands = new SkinCommands
                    {
                        SkinMenuCommand = new List<string> { "skin", "sb", "skins", "skinbox" },
                        SkinOutfitCommand = new List<string> { "outfits", "outfit", "loadout" },
                        SkinItemCommand = new List<string> { "skinitem", "skini" },
                        SkinBaseCommand = new List<string> { "skinbase" },
                        SkinItemsInContainerCommand = new List<string> { "skincontainer", "skinc" }
                    },
                    DefaultOutfit = new List<OutfitDetail>()
                    {
                        new OutfitDetail { ItemId = -194953424, Shortname = "metal.facemask", SkinId = 0 },
                        new OutfitDetail { ItemId = 1751045826, Shortname = "hoodie", SkinId = 0 },
                        new OutfitDetail { ItemId = 1110385766, Shortname = "metal.plate.torso", SkinId = 0 },
                        new OutfitDetail { ItemId = 1850456855, Shortname = "roadsign.kilt", SkinId = 0 },
                        new OutfitDetail { ItemId = 237239288, Shortname = "pants", SkinId = 0 },
                        new OutfitDetail { ItemId = -1549739227, Shortname = "shoes.boots", SkinId = 0 },
                        new OutfitDetail { ItemId = 1545779598, Shortname = "rifle.ak", SkinId = 0 },
                        new OutfitDetail { ItemId = 442886268, Shortname = "rocket.launcher", SkinId = 0 }
                    },
                    OutfitPermissions = new Dictionary<string, int>()
                    {
                        { "skincontroller.default", 4 },
                        { "skincontroller.vip", 6 },
                        { "skincontroller.vip+", 10 },
                        { "skincontroller.admin", 100 }
                    },
                    DissallowedSkinIDs = new List<string>()
                    {
                        "123456"
                    }
                };
            }
        }

        public class UIElements
        {
            public string UIBackgroundColor { get; set; } = "0 0 0 .6";
            public string UIPrimaryColor { get; set; } = "0 0 0 .6";
            public string UISecondaryColor { get; set; } = "0 0 0 .5";
            public string UIItemButtonColor { get; set; } = "1 1 1 .175";
            public string UISkinPanelColor { get; set; } = "1 1 1 .175";
            public string UISearchPanelColor { get; set; } = "0 0 0 .5";
            public string UICloseButtonColor { get; set; } = "1 .4 0 .5";
            public string UIButtonColor { get; set; } = "1 .4 0 .5";
            public string UISecondaryButtonColor { get; set; } = ".29 .29 .29 .5";
            public string UIActiveButtonColor { get; set; } = "1 .4 0 .5";
            public string UIActiveCategoryColor { get; set; } = ".29 .29 .29 .5";
            public string PrimaryButtonTextColor { get; set; } = ".32 .89 .26 .8";
            public string UISafeButtonColor { get; set; } = "1 .34 .34 .8";
            public string UIDangerButtonColor { get; set; } = "1 .34 .34 .8";
            public string UIScrollBarColor { get; set; } = "1 .4 0 .5";

            public string UIValidItemColor { get; set; } = "0 0 0 .4";
            public string UIInvalidItemColor { get; set; } = "0 0 0 .8";
            public string UISelectedItemColor { get; set; } = "1 .4 0 .5";

            public string UISettingsPanelBlurColor { get; set; } = "0 0 0 .5";
            public string UISettingsPanelOverlayColor { get; set; } = "0 0 0 .2";
            public string UISettingsPanelColor { get; set; } = "1 1 1 .15";
            public string UISecondarySettingsPanelColor { get; set; } = "1 1 1 .2";

            public string UISelectPanelBlurColor { get; set; } = "0 0 0 .5";
            public string UISelectPanelColor { get; set; } = ".17 .17 .17 .7";
            public string UISecondarySelectPanelColor { get; set; } = "1 1 1 .2";
            public string UIApplySelectColor { get; set; } = "1 1 1 .3";
            public string UIFindSelectColor { get; set; } = "1 1 1 .25";
            public string UISkinSelectColor { get; set; } = "1 1 1 .35";

            public string UISkinCommandPanelColor { get; set; } = ".17 .17 .17 1";
            public string UISkinCommandPageButtonColor { get; set; } = "1 1 1 .13";
            public string UISkinCommandPagePanelColor { get; set; } = "1 1 1 .1";
            public string UISkinCommandCloseButtonColor { get; set; } = "1 1 1 .1";
            public string UISkinCommandTitleColor { get; set; } = "1 1 1 .1";
            public string UISkinCommandSkinPanelColor { get; set; } = "1 1 1 .1";

            public string UISavePanelBlurColor { get; set; } = "0 0 0 .5";
            public string UISavePanelColor { get; set; } = ".17 .17 .17 .7";
            public string UISaveTitleColor { get; set; } = "1 1 1 .2";
            public string UISaveSecondaryColor { get; set; } = "1 1 1 .15";
            public string UISaveCloseColor { get; set; } = "1 1 1 .2";
            public string UISaveOverrideColor { get; set; } = "1 1 1 .17";
            public string UISaveColor { get; set; } = "1 .4 0 .4";
        }

        public class SkinCommands
        {
            [JsonProperty(PropertyName = "Skin menu command")]
            public List<string> SkinMenuCommand { get; set; } = new List<string>();
            [JsonProperty(PropertyName = "Outfit UI command")]
            public List<string> SkinOutfitCommand { get; set; } = new List<string>();
            [JsonProperty(PropertyName = "Skin item command")]
            public List<string> SkinItemCommand { get; set; } = new List<string>();
            [JsonProperty(PropertyName = "Skin base command")]
            public List<string> SkinBaseCommand { get; set; } = new List<string>();
            [JsonProperty(PropertyName = "Skin container items command")]
            public List<string> SkinItemsInContainerCommand { get; set; } = new List<string>();
        }

        private static SkinnerConfig _config;
        private static UIElements _uiColors;

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<SkinnerConfig>();
                if (_config.DissallowedSkinIDs == null) _config.DissallowedSkinIDs = new List<string> { "123456" };
                if (_config == null) LoadDefaultConfig();
                _uiColors = _config.UISettings;
                if (_config.UIImage == "https://i.ibb.co/3SNBq4W/Adv-Skinner.png") _config.UIImage = "https://i.ibb.co/0mPRNQD/SkinsP2.png";
                SaveConfig();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                PrintWarning("Creating new config file.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => _config = SkinnerConfig.DefaultConfig();
        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }
        #endregion

        #region Data
        public Dictionary<int, List<SkinData>> _skinList = new Dictionary<int, List<SkinData>>();
        public Dictionary<string, List<int>> _categoryList = new Dictionary<string, List<int>>();
        public Dictionary<int, int> _itemRedirects = new Dictionary<int, int>();
        public bool usingWC = false;
        public Dictionary<ulong, PlayerSettings> _playerSettings = new Dictionary<ulong, PlayerSettings>();
        public List<string> _uiCategories = new List<string>() { "Weapon", "Attire", "Items", "Tool", "Construction" };
        public Dictionary<ulong, BaseEntity> _playerSkinItems = new Dictionary<ulong, BaseEntity>();
        public bool SkinsGenerated = false;
        public List<OptionalSettings> _optionalSettings = new List<OptionalSettings>();
        public Dictionary<ulong, DateTime> _cooldowns = new Dictionary<ulong, DateTime>();

        public class OptionalSettings
        {
            public string Permission = string.Empty;
            public bool CanUse { get; set; } = true;
            public string SettingName { get; set; } = String.Empty;
            public bool Enabled { get; set; } = true;
        }

        public class PlayerOptionalSettings
        {
            public string SettingName { get; set; } = String.Empty;
            public bool Enabled { get; set; } = true;
        }

        public class SkinData
        {
            public string SkinName { get; set; } = String.Empty;
            public ulong SkinID { get; set; } = 0;
            public string ItemShortname { get; set; } = String.Empty;
            public int ItemID { get; set; } = 0;
            public string SkinPermission { get; set; } = String.Empty;
            public Redir Redirect = new Redir();
        }

        public class Redir
        {
            public bool IsRedirect { get; set; } = false;
            public string Shortname { get; set; } = String.Empty;
            public int ItemId { get; set; } = 0;
        }

        public class Outfits
        {
            public string OutfitName { get; set; }
            public bool Favorite { get; set; } = false;
            public List<OutfitDetail> OutfitDetails { get; set; }
        }

        public class OutfitDetail
        {
            public int ItemId { get; set; }
            public ulong SkinId { get; set; }
            public string Shortname { get; set; }
        }

        public class PlayerSettings
        {
            public long LastUsed = 0;
            public int SkinPage = 0;
            public int ItemPage = 0;
            public int LastItemPage = 0;
            public int LoadoutPage = 0;
            public DateTime ItemSkinCooldown { get; set; }
            public List<PlayerOptionalSettings> PlayerOptionalSettings { get; set; } = new List<PlayerOptionalSettings>();
            public bool AdvacedPlacedItemSkinning { get; set; } = true;
            public int CategoryItem { get; set; } = -194953424;
            public string SelectedShortName { get; set; } = "metal.facemask";
            public ulong SelectedSkinId { get; set; } = 0;
            public string Category { get; set; } = "Attire";
            public string SkinsFilter { get; set; } = null;
            public string LoadedItems { get; set; } = null;
            public string ActiveSaveName { get; set; }
            public long BaseSkinCooldown { get; set; } = 0;
            public IndSettings IndSettings { get; set; } = new IndSettings();
            public UIPosition ItemUIPostition = new UIPosition();
            public List<OutfitDetail> SavedItems { get; set; } = new List<OutfitDetail>();
            public List<Outfits> Outfits { get; set; } = new List<Outfits>();
        }

        public ulong FalseSkinId = 99811111172;

        public class IndSettings
        {
            public int ItemID = 0;
            public ulong SkinID = 0;
            public int ItemPos = -1;
            public int SelectedItemId = 0;
            public UIPosition SelectedUIPosition = new UIPosition();
            public UIPosition LastUIPosition = new UIPosition();
            public List<InvItem> InventoryItems = new List<InvItem>();
        }

        public class InvItem
        {
            public int ItemId = 0;
            public ulong SkinId = 0;
            public int NewItemId = 0;
            public int Position = 0;
            public int Category = 0;
            public ulong NewSkinId = 99811111172;
        }

        public class UIPosition
        {
            public string yMin = string.Empty;
            public string yMax = string.Empty;
            public string xMin = string.Empty;
            public string xMax = string.Empty;
        }

        private List<string> Permissions = new List<string>
        {
            "skincontroller.addskins",
            "skincontroller.use",
            "skincontroller.skinoncraft",
            "skincontroller.skinonpickup",
            "skincontroller.skinbase",
            "skincontroller.skinitem",
            "skincontroller.skincontainer",
            "skincontroller.skinonkitredeemed"
        };
        #endregion

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Close"] = "CLOSE",
                ["Settings"] = "SETTINGS",
                ["OutfitText"] = "LOADED OUTFIT: {0}",
                ["ApplyToInv"] = "APPLY TO INVENTORY",
                ["ManageOutfits"] = "MANAGE OUTFITS",
                ["Weapon"] = "WEAPONS",
                ["Attire"] = "CLOTHING",
                ["Items"] = "DEPLOYABLES",
                ["Tool"] = "TOOLS",
                ["Construction"] = "CONSTRUCTION",
                ["Search"] = "SEARCH",
                ["SkinButton"] = "SKIN",
                ["SetButton"] = "SET",
                ["Favorite"] = "FAVORITE",
                ["Favorited"] = "FAVORITED",
                ["NewSet"] = "+ NEW SET",
                ["Delete"] = "DELETE",
                ["Clear"] = "CLEAR",
                ["Edit"] = "EDIT",
                ["Save"] = "SAVE",
                ["SaveNewOutfit"] = "SAVE NEW OUTFIT",
                ["OutfitName"] = "OUTFIT NAME",
                ["OutfitEditor"] = "OUTFIT EDITOR",
                ["NamePlaceholder"] = "SET NAME",
                ["SkinCount"] = "{0} SKINS",
                ["NoOutfit"] = "No outfit",
                ["NoSkinsAvail"] = "NO SKINS AVAILABLE",
                ["OverrideOutfit"] = "OVERRIDE OUTFIT\n[ {0} ]",
                ["AutoSkinPickup"] = "Auto skin on item pickup",
                ["AutoSkinCraft"] = "Auto skin on item crafted",
                ["AutoSkinKitRedeem"] = "Auto skin on kit redeemed",
                ["SimpleSkinItemCommand"] = "Simple skin item command",
                ["FavoriteFallback"] = "Favorite fallback",
                ["OutfitNameSaved"] = "You already have an outfit with the name {0}",
                ["NoSkinFound"] = "Could not find a skin!",
                ["OutfitNameCharLimit"] = "Outfit name cannot be longer than 20 characters",
                ["OutfitSaveCount"] = "You've exceeded your save limit of {0}",
                ["NoPermission"] = "No permission to use this command",
                ["SkinningContainer"] = "Skinning items in container",
                ["NotAuthed"] = "You can only do this in a building authed zone",
                ["NotValidDeployable"] = "Not a valid deployable",
                ["SkinningBase"] = "Skinning base",
            }, this, "en");
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        #endregion

        #region Import Constructors
        public class AddSkinRoot
        {
            public Response response;
        }

        public class Tag
        {
            public string tag { get; set; }
        }

        public class Response
        {
            public int result { get; set; }
            public int resultcount { get; set; }
            public List<PublishedFileDetail> publishedfiledetails { get; set; }
        }

        public class PublishedFileDetail
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
            public List<Tag> tags { get; set; }
        }

        public class CollectionRoot
        {
            public CollectionResponse response { get; set; }
        }

        public class CollectionResponse
        {
            public int result { get; set; }
            public int resultcount { get; set; }
            public List<Collectiondetail> collectiondetails { get; set; }
        }

        public class Collectiondetail
        {
            public string publishedfileid { get; set; }
            public int result { get; set; }
            public List<Child> children { get; set; }
        }

        public class Child
        {
            public string publishedfileid { get; set; }
            public int sortorder { get; set; }
            public int filetype { get; set; }
        }
        #endregion

        #region Import Skins
        private void GetCollection(BasePlayer player, string[] args, bool isChat = false, ConsoleSystem.Arg arg = null)
        {
            if (args == null || args.Length == 0)
            {
                DetermineReply(player, isChat, "No collection id provided", arg);
                return;
            }

            try
            {
                webrequest.Enqueue("https://api.steampowered.com/ISteamRemoteStorage/GetCollectionDetails/v1/", $"?key={_config.SteamApiKey}&collectioncount=1&publishedfileids[0]={args[0]}",
                    (code, response) =>
                    {
                        if (code != 200)
                        {
                            DetermineReply(player, isChat, $"Collection {args[0]} didn't return code 200 on request", arg);
                            return;
                        }

                        if (response == null)
                        {
                            DetermineReply(player, isChat, $"Collection {args[0]} not found, null reply", arg);
                            return;
                        }

                        var collectionResponse = JsonConvert.DeserializeObject<CollectionRoot>(response);
                        if (collectionResponse.response.collectiondetails[0].children == null)
                        {
                            DetermineReply(player, isChat, $"Collection {args[0]} not found", arg);
                            return;
                        }
                        else DetermineReply(player, isChat, $"Importing {collectionResponse.response.collectiondetails[0].children.Count} skins from collection [ {args[0]} ]", arg);

                        GetSkin(player, collectionResponse.response.collectiondetails[0].children.Select(x => x.publishedfileid).ToArray(), isChat, arg);
                    }, this, RequestMethod.POST);
            }
            catch
            {

            }
        }

        void GetSkin(BasePlayer player, string[] args, bool isChat = false, ConsoleSystem.Arg arg = null)
        {
            List<string> strings = new List<string>() { "" };
            List<int> amounts = new List<int>() { 0 };

            int i = 0;
            int page = 0;
            foreach (var skinid in args)
            {
                if (i == 99)
                {
                    strings.Add("");
                    amounts.Add(0);
                    page++;
                    i = 0;
                }

                strings[page] = strings[page] + $"&publishedfileids[{i}]={skinid}";
                i++;

                amounts[page] = amounts[page] + 1;
            }

            i = 0;
            foreach (string skin in strings)
            {
                HandleWorkshopPage(player, isChat, skin, amounts[i], arg);
                i++;
            }

        }

        private void HandleWorkshopPage(BasePlayer player, bool isChat, string str, int amount, ConsoleSystem.Arg arg = null)
        {
            try
            {
                webrequest.Enqueue("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/", $"?itemcount={amount}{str}", (code, response) =>
                {
                    if (code != 200)
                    {
                        DetermineReply(player, isChat, "Error adding skin, not a 200 response.", arg);
                        return;
                    }

                    if (response == null)
                    {
                        DetermineReply(player, isChat, "Error adding skin, not a valid response.", arg);
                        return;
                    }

                    var workshopSkin = JsonConvert.DeserializeObject<AddSkinRoot>(response);
                    DetermineReply(player, isChat, $"Importing {workshopSkin.response.publishedfiledetails.Count} skin(s)!", arg);

                    int pendingSkins = workshopSkin.response.publishedfiledetails.Count;

                    foreach (var skin in workshopSkin.response.publishedfiledetails)
                    {
                        ServerMgr.Instance.StartCoroutine(AddWorkshopSkin(skin, () =>
                        {
                            pendingSkins--;
                            if (pendingSkins == 0)
                            {
                                SaveConfig();
                            }
                        }));
                    }
                }, this, RequestMethod.POST);
            }
            catch
            {
                DetermineReply(player, isChat, $"Error getting skin info.", arg);
            }
        }

        private IEnumerator AddWorkshopSkin(PublishedFileDetail skin, Action onComplete)
        {
            if (skin == null || skin.tags == null || string.IsNullOrEmpty(skin.publishedfileid) || string.IsNullOrEmpty(skin.title))
                yield return null;

            var tags = skin.tags;

            if (tags == null) yield return null;

            ulong skinId = ulong.Parse(skin.publishedfileid);
            if (_config.AddedSkins.Any(x => x.SkinID == skinId)) yield return null;

            ClassedItem foundItem = null;

            if (tags != null) foreach (var tag in tags)
                {
                    if (tag == null || tag?.tag == null) continue;

                    var item = _workshopRename.FirstOrDefault(x => x.Key == tag?.tag?.ToLower().Replace("skin", "").Replace(" ", "").Replace("-", "").Replace(".item", ""));
                    if (item.Value == null) continue;
                    else
                    {
                        if (item.Value.ItemID == 550753330) continue;
                        foundItem = item.Value;
                        break;
                    }
                }
            else yield return null;

            if (foundItem != null)
            {
                bool hasItem = _skinList.ContainsKey(foundItem.ItemID);
                if (!hasItem) yield return null;

                bool isAlready = _skinList[foundItem.ItemID].Any(x => x.SkinID == skinId);

                var skinInfo = new SkinData()
                {
                    SkinName = skin.title,
                    ItemID = foundItem.ItemID,
                    ItemShortname = foundItem.Shortname,
                    SkinID = skinId
                };

                _config.AddedSkins.Add(skinInfo);
                if (!isAlready) _skinList[foundItem.ItemID]?.Add(skinInfo);
            }

            onComplete?.Invoke();
        }

        private void DetermineReply(BasePlayer player, bool isChat, string reply, ConsoleSystem.Arg arg = null)
        {
            if (isChat) SendReply(player, reply);
            else
            {
                if (arg.Connection == null) Interface.Oxide.LogInfo(reply);
                else SendReply(arg, reply);
            }
        }

        private bool CheckPlayerSkinPermission(BasePlayer player, bool isChat, ConsoleSystem.Arg arg = null)
        {
            if (isChat)
            {
                if (!permission.UserHasPermission(player.UserIDString, "skincontroller.addskins"))
                {
                    CreateGameTip(player, 3, 1, "You do not have permission");
                    return false;
                }
                else return true;
            }

            if (arg.Connection != null)
            {
                if (!permission.UserHasPermission(arg.Player().UserIDString, "skincontroller.addskins"))
                {
                    SendReply(arg, "You do not have permission");
                    return false;
                }
                else return true;
            }

            return true;
        }
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
            if (!pluginName.Equals("SkinController", StringComparison.OrdinalIgnoreCase)) return;
            Interface.CallHook("WCSendColors", ConvertToDictionary(_uiColors), pluginName);
        }

        void OnWCSentThemeColors(List<string> pluginNames, Dictionary<string, string> themeColors)
        {
            if (!pluginNames.Contains("SkinController")) return;

            _config.UISettings.UIBackgroundColor = themeColors["BackgroundColor"];
            _config.UISettings.UIPrimaryColor = themeColors["BackgroundColor"];
            _config.UISettings.UISecondaryColor = themeColors["SecondaryColor"];
            _config.UISettings.UICloseButtonColor = themeColors["PrimaryButtonColor"];
            _config.UISettings.UIButtonColor = themeColors["PrimaryButtonColor"];
            _config.UISettings.UIActiveButtonColor = themeColors["PrimaryButtonColor"];
            _config.UISettings.UIScrollBarColor = themeColors["PrimaryButtonColor"];
            _config.UISettings.UISelectedItemColor = themeColors["PrimaryButtonColor"];
            _config.UISettings.UISaveColor = themeColors["ThirdButtonColor"];

            _config.UISettings.UISafeButtonColor = themeColors["SecondaryButtonColor"];
            _config.UISettings.UIDangerButtonColor = themeColors["SecondaryButtonColor"];
            SaveConfig();
        }

        void OnServerInitialized(bool initial)
        {
            var isUsingWC = Interface.Call("IsUsingPlugin", "SkinController");
            if (isUsingWC != null && (isUsingWC is bool)) usingWC = (bool)isUsingWC;
            else usingWC = false;

            _categoryList.Add("Weapon", new List<int> { 1545779598, -1812555177, 1318558775, -2069578888, 1588298435, -778367295, 442886268 });
            _categoryList.Add("Attire", new List<int> { -194953424, 1751045826, 1110385766, 1850456855, 237239288, -1549739227, -699558439, -803263829, -2002277461, 1266491000 });
            _categoryList.Add("Tool", new List<int> { 1488979457 });
            _categoryList.Add("Construction", new List<int> { 1390353317 });
            _categoryList.Add("Items", new List<int> { 1534542921 });

            if (_config.SkinItemsOnCraft) _optionalSettings.Add(new OptionalSettings { CanUse = true, Enabled = true, SettingName = "AutoSkinCraft", Permission = "skincontroller.skinoncraft" });
            if (_config.SkinItemsOnPickup) _optionalSettings.Add(new OptionalSettings { CanUse = true, Enabled = true, SettingName = "AutoSkinPickup", Permission = "skincontroller.skinonpickup" });
            if (_config.SkinKitsOnRedeemed) _optionalSettings.Add(new OptionalSettings { CanUse = true, Enabled = true, SettingName = "AutoSkinKitRedeem", Permission = "skincontroller.skinonkitredeemed" });
            _optionalSettings.Add(new OptionalSettings { CanUse = true, Enabled = true, SettingName = "FavoriteFallback" });
            _optionalSettings.Add(new OptionalSettings { CanUse = true, Enabled = true, SettingName = "SimpleSkinItemCommand" });

            HandleCommands();
            HandlePermissions();
            ImportImages();
            RequestSkins();
            StatLastUpdatedCheck();
            UnsubscribeHooks();
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null || !player.userID.Get().IsSteamId()) return;

            SavePlayerSettings(player);
            _playerSettings.Remove(player.userID);
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                SavePlayerSettings(player);
                CuiHelper.DestroyUi(player, "SCMainPanel");
            }

            _config = null;
        }

        void OnServerSave()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (_playerSettings.ContainsKey(player.userID)) SavePlayerSettings(player);
            }
        }

        void OnWCRequestedUIPanel(BasePlayer player, string panelName, string neededPlugin)
        {
            if (!neededPlugin.Equals("SkinController", StringComparison.OrdinalIgnoreCase)) return;
            usingWC = true;
            UIMain(player, null, null);
        }

        void OnPluginLoaded(Plugin plugin)
        {
            if (plugin.Name == "WelcomeController")
            {
                var isUsingWC = Interface.Call("IsUsingPlugin", "SkinController");
                if (isUsingWC != null && (isUsingWC is bool)) usingWC = (bool)isUsingWC;
                else usingWC = false;
            }
        }

        void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin.Name == "WelcomeController")
            {
                usingWC = false;
                HandleCommands();
            }
        }

        private void OnKitRedeemed(BasePlayer player, string kitName)
        {
            var playerSettings = GetOrLoadPlayerSettings(player);
            if (!playerSettings.PlayerOptionalSettings.Any(x => x.SettingName == "AutoSkinKitRedeem" && x.Enabled)) return;
            if (!permission.UserHasPermission(player.UserIDString, "skincontroller.skinonkitredeemed")) return;

            List<Item> itemList = Pool.Get<List<Item>>();
            player.inventory.GetAllItems(itemList);

            if(itemList != null) SkinContainer(player, itemList, ref playerSettings, true);

            Pool.FreeUnmanaged(ref itemList);
        }

        private void OnItemPickup(Item item, BasePlayer player)
        {
            var playerSettings = GetOrLoadPlayerSettings(player);

            if (!playerSettings.PlayerOptionalSettings.Any(x => x.SettingName == "AutoSkinPickup" && x.Enabled)) return;
            if (!permission.UserHasPermission(player.UserIDString, "skincontroller.skinonpickup")) return;

            SkinData skinInfo = FindSkin(player, ref playerSettings, item);
            if (skinInfo == null) return;

            if (item.skin == skinInfo.SkinID) return;

            bool isOrigional = !skinInfo.Redirect.IsRedirect;
            bool wasRedirect = CheckItemRedirect(item.info.itemid) != item.info.itemid;

            if (skinInfo.Redirect.IsRedirect || wasRedirect) NextTick(() => RedirectItem(player, item, skinInfo, wasRedirect, isOrigional));
            else SkinItem(item, skinInfo);

            return;
        }

        void OnItemCraftFinished(ItemCraftTask task, Item item, ItemCrafter crafter)
        {
            if (task == null || item?.info == null || crafter == null || crafter.owner == null) return;

            var playerSettings = GetOrLoadPlayerSettings(crafter.owner);
            if (!playerSettings.PlayerOptionalSettings.Any(x => x.SettingName == "AutoSkinCraft" && x.Enabled) || !permission.UserHasPermission(crafter.owner.UserIDString, "skincontroller.skinoncraft")) return;

            SkinData skinInfo = FindSkin(crafter.owner, ref playerSettings, item);
            if (skinInfo == null) return;
            SkinItem(item, skinInfo);
            return;
        }
        #endregion

        #region Console Commands
        [ConsoleCommand("skincontroller.addskin")]
        private void CMDImportSkins(ConsoleSystem.Arg arg)
        {
            var args = arg.Args;
            if (args == null || args.Length == 0)
            {
                DetermineReply(arg.Player(), false, "No skins provided.", arg);
                return;
            }

            var hasPermission = CheckPlayerSkinPermission(arg.Player(), false, arg);
            if (hasPermission) GetSkin(arg.Player(), arg.Args, false, arg);
        }


        [ConsoleCommand("skincontroller.addcollection")]
        private void CMDImportCollection(ConsoleSystem.Arg arg)
        {
            var hasPermission = CheckPlayerSkinPermission(arg.Player(), false, arg);
            if (hasPermission) GetCollection(arg.Player(), arg.Args, false, arg);
        }

        [ConsoleCommand("sc_main")]
        private void CMDMain(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            if (!CheckCooldown(player.userID)) return;

            var playerSettings = GetOrLoadPlayerSettings(player);

            switch (arg.Args[0])
            {
                case "trycollection":
                    GetCollection(player, arg.Args.Skip(1).ToArray(), false, arg);
                    break;
                case "tryskin":
                    GetSkin(player, arg.Args.Skip(1).ToArray(), false, arg);
                    break;
                case "close":
                    if (usingWC) CuiHelper.DestroyUi(player, "WCMainPanel");
                    else CuiHelper.DestroyUi(player, "SCMainPanel");
                    break;
                case "item":
                    SelectItem(player, playerSettings, arg.Args);
                    break;
                case "category":
                    SelectCategory(player, playerSettings, arg.Args);
                    break;
                case "editloadoutitem":
                    EditLoadoutItem(player, playerSettings, arg.Args);
                    break;
                case "findskins":
                    FindSkins(player, playerSettings, arg.Args);
                    CuiHelper.DestroyUi(player, "SCSelectPanel");
                    break;
                case "skinsfilter":
                    SkinsFilter(player, playerSettings, arg.Args);
                    break;
                case "indfilter":
                    IndSkinsFilter(player, playerSettings, arg.Args);
                    break;
                case "commandfilter":
                    CommandSkinsFilter(player, playerSettings, arg.Args);
                    break;
                case "outfits":
                    playerSettings.LoadoutPage = 0;
                    UIInit(player, 1);
                    break;
                case "skinitem":
                    SkinData skinInfo = _skinList[int.Parse(arg.Args[1])].FirstOrDefault(x => x.SkinID == ulong.Parse(arg.Args[2]));
                    if (skinInfo == null) return;

                    List<Item> itemList = Pool.Get<List<Item>>();
                    player.inventory.GetAllItems(itemList);

                    if(itemList != null) SkinContainer(player, itemList, ref playerSettings, false, skinInfo, isInventory: true);

                    Pool.FreeUnmanaged(ref itemList);

                    CuiHelper.DestroyUi(player, "SCSelectPanel");
                    break;
                case "skininventory":

                    itemList = Pool.Get<List<Item>>();
                    player.inventory.GetAllItems(itemList);

                    if (itemList != null) SkinContainer(player, itemList, ref playerSettings, true, isInventory: true);

                    Pool.FreeUnmanaged(ref itemList);

                    break;
                case "selectindskin":
                    SelectSkin(player, playerSettings, arg.Args);
                    break;
                case "setskin":
                    SetOutfitSkin(player, playerSettings, int.Parse(arg.Args[1]), ulong.Parse(arg.Args[2]));
                    CuiHelper.DestroyUi(player, "SCSelectPanel");
                    break;
                case "selectinvitem":
                    SelectInventoryItem(player, ref playerSettings, arg.Args);
                    break;
                case "deselectinvitem":
                    DeselectInventoryItem(player, ref playerSettings, arg.Args);
                    break;
                case "indpage":
                    UIInit(player, 2);
                    break;
                case "itempage":
                    playerSettings.ItemPage = int.Parse(arg.Args[1]);
                    UIWriteItemPanel(player, playerSettings);
                    break;
                case "applyskins":
                    if (playerSettings.IndSettings.InventoryItems != null)
                    {
                        SkinContainer(player, player.inventory.containerMain.itemList, ref playerSettings, false, null, playerSettings.IndSettings.InventoryItems.Where(x => x.Category == 0).ToList());
                        SkinContainer(player, player.inventory.containerBelt.itemList, ref playerSettings, false, null, playerSettings.IndSettings.InventoryItems.Where(x => x.Category == 1).ToList());
                        SkinContainer(player, player.inventory.containerWear.itemList, ref playerSettings, false, null, playerSettings.IndSettings.InventoryItems.Where(x => x.Category == 2).ToList());
                    }

                    playerSettings.IndSettings.InventoryItems = new List<InvItem>();

                    foreach (var item in player.inventory.containerMain.itemList) playerSettings.IndSettings.InventoryItems.Add(new InvItem { Category = 0, ItemId = item.info.itemid, Position = item.position, SkinId = item.skin });
                    foreach (var item in player.inventory.containerBelt.itemList) playerSettings.IndSettings.InventoryItems.Add(new InvItem { Category = 1, ItemId = item.info.itemid, Position = item.position, SkinId = item.skin });
                    foreach (var item in player.inventory.containerWear.itemList) playerSettings.IndSettings.InventoryItems.Add(new InvItem { Category = 2, ItemId = item.info.itemid, Position = item.position, SkinId = item.skin });

                    UIWriteInventoryPanel(player, playerSettings);
                    break;
                case "selectpage":
                    playerSettings.SkinPage = int.Parse(arg.Args[1]);
                    UIWriteIndSkinPanel(player, playerSettings);
                    break;
                case "skinextra":
                    skinInfo = _skinList[int.Parse(arg.Args[1])].FirstOrDefault(x => x.SkinID == ulong.Parse(arg.Args[2]));
                    if (skinInfo == null) return;
                    UIWriteSelectPanel(player, skinInfo);
                    break;
                case "applyloadouttoindskins":
                    ApplyLoadoutToIndSkins(player, playerSettings);
                    break;
                case "clearfilter":
                    playerSettings.SkinsFilter = null;
                    playerSettings.SkinPage = 0;
                    SkinsFilter(player, playerSettings, arg.Args);
                    break;
                case "skinspage":
                    playerSettings.SkinPage = int.Parse(arg.Args[1]);
                    UIWriteSkinsPanel(player, playerSettings);
                    break;
                case "commandspage":
                    playerSettings.SkinPage = int.Parse(arg.Args[1]);
                    UIAddCommandSkins(player, ref playerSettings);
                    break;
                case "closeselect":
                    CuiHelper.DestroyUi(player, "SCSelectPanel");
                    break;
                case "closecommand":
                    CuiHelper.DestroyUi(player, "UIMainSkinPanel");
                    break;
                case "outfitpage":
                    UIInit(player, 0);
                    break;
                case "settings":
                    UIWriteSettingsUI(player, ref playerSettings);
                    break;
                case "closesettings":
                    CuiHelper.DestroyUi(player, "SCSettingsPanel");
                    break;
                case "setting":
                    var settingNumber = int.Parse(arg.Args[1]);
                    var theSetting = playerSettings.PlayerOptionalSettings[settingNumber];
                    if (theSetting.Enabled) theSetting.Enabled = false;
                    else theSetting.Enabled = true;

                    AlterSetting(player, ref playerSettings, settingNumber);
                    break;
                case "selectcommandskin":
                    var playerItem = GetPlayerItem(player);
                    if (playerItem == null) return;

                    ulong skinId = ulong.Parse(arg.Args[1]);
                    if (playerItem.skinID != skinId)
                    {
                        playerItem.skinID = skinId;
                        playerItem.SendNetworkUpdateImmediate();
                    }

                    CuiHelper.DestroyUi(player, "UIMainSkinPanel");
                    RemovePlayerItem(player);
                    break;
                case "deleteloadoutitem":
                    DeleteLoadoutItem(player, ref playerSettings, arg.Args);
                    break;
                case "alterloadoutitem":
                    UIAlterLoadoutItem(player, ref playerSettings, arg.Args);
                    break;
                case "closeloadoutitem":
                    CloseLoadoutItem(player, ref playerSettings, arg.Args);
                    break;
                case "blacklist":
                    BlacklistSkin(player, ref playerSettings, arg.Args);
                    break;
                case "favorite":
                    FavoriteOutfit(player, ref playerSettings, arg.Args);
                    break;
                case "skinteam":
                    SkinTeam(player, ref playerSettings);
                    break;
                case "selectoutfit":
                    SelectOutfit(player, ref playerSettings, arg.Args);
                    break;
            }
        }

        [ConsoleCommand("sc_outfit")]
        private void CMDOutfit(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            if (!CheckCooldown(player.userID)) return;

            var playerSettings = GetOrLoadPlayerSettings(player);

            switch (arg.Args[0])
            {
                case "new":
                    NewOutfitSet(player, ref playerSettings);
                    break;
                case "delete":
                    DeleteOutfit(player, ref playerSettings);
                    break;
                case "clear":
                    ClearOutfitSkins(player, ref playerSettings);
                    break;
                case "save":
                    UIWriteOutfitEdit(player, ref playerSettings);
                    break;
                case "close":
                    UIInit(player, 0);
                    break;
                case "outfitname":
                    HandleOutfitName(player, ref playerSettings, arg.Args);
                    break;
                case "savenew":
                    HandleOutfitSaveOverride(player, ref playerSettings, true);
                    break;
                case "override":
                    HandleOutfitSaveOverride(player, ref playerSettings);
                    break;
                case "editclose":
                    CuiHelper.DestroyUi(player, "SCOutfitEditPanel");
                    break;
                case "outfitpage":
                    playerSettings.LoadoutPage = int.Parse(arg.Args[1]);
                    UIWriteAllLoadouts(player, playerSettings);
                    break;
            }
        }
        #endregion

        #region Chat Commands
        [ChatCommand("addskin")]
        void CmdAddSkin(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;

            if (args == null || args.Length == 0)
            {
                DetermineReply(player, true, "No skins provided.");
                return;
            }

            var hasPermission = CheckPlayerSkinPermission(player, true);
            if (hasPermission) GetSkin(player, args, true);
        }

        [ChatCommand("addcollection")]
        void CmdAddCollection(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;

            var hasPermission = CheckPlayerSkinPermission(player, true);
            if (hasPermission) GetCollection(player, args, true);
        }

        void SkinItemRaycast(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;

            if (!permission.UserHasPermission(player.UserIDString, "skincontroller.skinitem") && !player.IsAdmin)
            {
                CreateGameTip(player, 3, 1, Lang("NoPermission", player.UserIDString));
                return;
            }

            if (player.IsBuildingBlocked() && !player.IsAdmin)
            {
                CreateGameTip(player, 3, 1, Lang("NotAuthed", player.UserIDString));
                return;
            }

            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 10f, LayerMask.GetMask("Deployed", "Construction")))
            {
                CreateGameTip(player, 3, 1, Lang("NotValidDeployable", player.UserIDString));
                return;
            }

            BaseEntity entity = hit.GetEntity();
            if (entity == null)
            {
                CreateGameTip(player, 3, 1, Lang("NotValidDeployable", player.UserIDString));
                return;
            }

            PlayerSettings playerSettings = GetOrLoadPlayerSettings(player);

            if (_config.DisallowedSkinIds.Contains(entity.skinID))
            {
                CreateGameTip(player, 3, 1, Lang("NotValidDeployable", player.UserIDString));
                return;
            }

            if (playerSettings.PlayerOptionalSettings.FirstOrDefault(x => x.SettingName == "SimpleSkinItemCommand").Enabled)
            {
                GetPlayerItem(player, entity);
                playerSettings.SkinPage = 0;
                playerSettings.SkinsFilter = null;
                UIWriteSkinCommandUI(player, playerSettings);
            }
            else FindBaseSkins(player, ref playerSettings, entity, true);

        }

        void SkinBase(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, "skincontroller.skinbase") && !player.IsAdmin)
            {
                CreateGameTip(player, 3, 1, Lang("NoPermission", player.UserIDString));
                return;
            }

            PlayerSettings playerSettings = GetOrLoadPlayerSettings(player);
            if (playerSettings.BaseSkinCooldown - DateTimeOffset.Now.ToUnixTimeSeconds() > 0)
            {
                CreateGameTip(player, 3, 1, "On cooldown");
                return;
            }

            if (player.IsBuildingBlocked() && !player.IsAdmin)
            {
                CreateGameTip(player, 3, 1, Lang("NotAuthed", player.UserIDString));
                return;
            }

            playerSettings.BaseSkinCooldown = DateTimeOffset.Now.ToUnixTimeSeconds() + _config.BaseSkinCooldownSeconds;
            var entities = Physics.OverlapSphere(player.transform.position, 30, LayerMask.GetMask("Deployed", "Construction")).Select(x => x.ToBaseEntity()).Distinct().ToList();
            var currentBuilding = player.GetBuildingPrivilege()?.GetBuilding()?.ID;
            CreateGameTip(player, 3, 2, Lang("SkinningBase", player.UserIDString));
            InvokeHandler.Instance.StartCoroutine(SkinPlayerBase(player, playerSettings, entities, currentBuilding));
        }

        void SkinPlacedContainer(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;

            if (!permission.UserHasPermission(player.UserIDString, "skincontroller.skincontainer"))
            {
                CreateGameTip(player, 3, 1, Lang("NoPermission"));
                return;
            }

            if (player.IsBuildingBlocked())
            {
                CreateGameTip(player, 3, 1, Lang("NotAuthed"));
                return;
            }

            RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, 10f, LayerMask.GetMask("Deployed")))
            {
                BaseEntity entity = hit.GetEntity();
                if (entity != null)
                {
                    List<string> containerList = new List<string>() { "box.wooden.large", "woodbox_deployed", "coffinstorage", "locker.deployed" };
                    if (!containerList.Contains(entity.ShortPrefabName)) return;

                    var playerSettings = GetOrLoadPlayerSettings(player);
                    StorageContainer container = entity as StorageContainer;
                    if(container.inventory.itemList != null) SkinContainer(player, container.inventory.itemList, ref playerSettings, true);
                    CreateGameTip(player, 3, 2, Lang("SkinningContainer"));
                }
            }
        }
        #endregion

        #region Skinning Methods
        private IEnumerator SkinPlayerBase(BasePlayer player, PlayerSettings playerSettings, List<BaseEntity> entities, uint? currentBuilding)
        {
            foreach (var entity in entities.Where(x => x.GetBuildingPrivilege()?.GetBuilding()?.ID == currentBuilding))
            {
                if (_config.DisallowedSkinIds.Contains(entity.skinID)) continue;
                FindBaseSkins(player, ref playerSettings, entity, false);
                yield return new WaitForSeconds(_config.BaseSkinWait);
            }
        }

        void ChatApplyOutfit(BasePlayer player, string[] args)
        {
            if (!CheckCooldown(player.userID))
            {
                SendReply(player, "On Cooldown");
                return;
            }

            var playerSettings = GetOrLoadPlayerSettings(player);

            var theOutfit = playerSettings.Outfits.FirstOrDefault(x => x.OutfitName.Equals(String.Join(" ", args), StringComparison.OrdinalIgnoreCase));
            if (theOutfit == null)
            {
                SendReply(player, "Could not find the loadout");
                return;
            }

            List<Item> itemList = Pool.Get<List<Item>>();
            player.inventory.GetAllItems(itemList);

            if(itemList != null) SkinContainer(player, itemList, ref playerSettings, true);

            Pool.FreeUnmanaged(ref itemList);
        }

        void FindBaseSkins(BasePlayer player, ref PlayerSettings playerSettings, BaseEntity entity, bool singular)
        {
            if (playerSettings.SavedItems.Any(x => x.Shortname == convertedShortnames(entity.ShortPrefabName)))
            {
                SkinEntity(player, entity, playerSettings.SavedItems);
                return;
            }
            else if (playerSettings.Outfits.Count > 0 && playerSettings.PlayerOptionalSettings.FirstOrDefault(x => x.SettingName == "FavoriteFallback").Enabled)
            {
                var favoriteOutfit = playerSettings.Outfits.FirstOrDefault(x => x.Favorite);
                if (favoriteOutfit != null)
                {
                    bool foundSkin = SkinEntity(player, entity, favoriteOutfit.OutfitDetails);
                    //if(!foundSkin && singular) CreateGameTip(player, 3, 1, Lang("NoSkinFound", player.UserIDString));
                    if (foundSkin) return;
                }
            }

            GetPlayerItem(player, entity);
            playerSettings.SkinPage = 0;
            playerSettings.SkinsFilter = null;
            if (singular) UIWriteSkinCommandUI(player, playerSettings);
            return;
        }

        void RedirectItem(BasePlayer player, Item item, SkinData skinInfo, bool wasRedirect, bool isOrigional)
        {
            if (item == null) return;
            ItemContainer container = item.parent;
            if (container == null) return;

            ItemDefinition ammoType = null;
            int contents = 0;
            int capacity = 0;
            float condition = 0f;
            float maxCondition = 0f;
            int position = item.position;

            if (item.hasCondition)
            {
                condition = item.condition;
                maxCondition = item.maxCondition;
            }

            BaseProjectile itemWeapon = item.GetHeldEntity() as BaseProjectile;
            if (itemWeapon != null && itemWeapon.primaryMagazine != null)
            {
                ammoType = itemWeapon.primaryMagazine.ammoType;
                contents = itemWeapon.primaryMagazine.contents;
                capacity = itemWeapon.primaryMagazine.capacity;
            }

            Item newItem = ItemManager.CreateByName(wasRedirect && isOrigional ? skinInfo.ItemShortname : skinInfo.Redirect.Shortname, item.amount);
            if (newItem == null) return;

            newItem.condition = condition;
            newItem.maxCondition = maxCondition;

            if (item.contents != null)
            {
                foreach (var mod in item.contents.itemList)
                {
                    Item attachment = ItemManager.CreateByName(mod.info.shortname, mod.amount);
                    if (attachment == null) continue;

                    attachment.condition = mod.condition;
                    attachment.maxCondition = mod.maxCondition;

                    attachment?.MoveToContainer(mod?.contents);
                }
            }

            BaseProjectile newWeapon = newItem.GetHeldEntity() as BaseProjectile;
            if (itemWeapon?.primaryMagazine != null)
            {
                newWeapon.SetAmmoCount(contents);
                newWeapon.primaryMagazine.ammoType = ammoType;
                newWeapon.primaryMagazine.capacity = capacity;

                newWeapon.ForceModsChanged();
            }

            item.Remove();
            ItemManager.DoRemoves();

            if (newItem != null && container != null) newItem.MoveToContainer(container, position);

            if (isOrigional) SkinItem(newItem, skinInfo);
        }

        private bool SkinEntity(BasePlayer player, BaseEntity entity, List<OutfitDetail> outfit)
        {
            var foundskin = outfit.FirstOrDefault(x => x.Shortname == convertedShortnames(entity.ShortPrefabName));
            if (foundskin != null)
            {
                if (entity.skinID != foundskin.SkinId)
                {
                    entity.skinID = foundskin.SkinId;
                    entity.SendNetworkUpdateImmediate();
                }
                return true;
            }
            return false;
        }

        void SetOutfitSkin(BasePlayer player, PlayerSettings playerSettings, int itemId, ulong skinId)
        {
            var skinInfo = FindSkin(player, ref playerSettings, itemId: itemId, skinId: skinId);
            if (skinInfo == null) return;

            var foundItem = playerSettings.SavedItems.FindIndex(x => x.ItemId == itemId);
            if (foundItem != -1)
            {
                playerSettings.SavedItems[foundItem].SkinId = skinInfo.SkinID;
            }
            else playerSettings.SavedItems.Add(new OutfitDetail { ItemId = skinInfo.ItemID, Shortname = skinInfo.ItemShortname, SkinId = skinId });

            UIWriteLoadoutPanel(player, playerSettings);
        }

        SkinData FindSkin(BasePlayer player, ref PlayerSettings playerSettings, Item item = null, int itemId = 0, ulong skinId = 0, bool useFirst = false)
        {
            if (itemId == 0 || useFirst)
            {
                if (!useFirst) itemId = item.info.itemid;
                itemId = CheckItemRedirect(itemId);

                var activeItem = playerSettings.SavedItems.FirstOrDefault(x => x.ItemId == itemId);
                if (activeItem != null)
                {
                    var foundSkinData = _skinList.ContainsKey(itemId) ? _skinList[itemId].FirstOrDefault(x => x.SkinID == activeItem.SkinId) : null;
                    if (foundSkinData != null) return foundSkinData;
                }

                if (playerSettings.Outfits.Count > 0 && playerSettings.PlayerOptionalSettings.FirstOrDefault(x => x.SettingName == "FavoriteFallback").Enabled)
                {
                    var favoriteOutfit = playerSettings.Outfits.FirstOrDefault(x => x.Favorite);
                    if (favoriteOutfit == null) return null;

                    var outfitItem = favoriteOutfit.OutfitDetails.FirstOrDefault(x => x.ItemId == itemId);
                    if (outfitItem != null)
                    {
                        var foundSkinData = _skinList.ContainsKey(itemId) ? _skinList[itemId].FirstOrDefault(x => x.SkinID == outfitItem.SkinId) : null;
                        if (foundSkinData != null) return foundSkinData;
                    }
                }
            }
            else
            {
                var foundSkinData = _skinList.ContainsKey(itemId) ? _skinList[itemId].FirstOrDefault(x => x.SkinID == skinId) : null;
                if (foundSkinData != null) return foundSkinData;
            }

            return null;
        }

        public class RedirItems
        {
            public Item Item { get; set; }
            public SkinData SkinData { get; set; }
            public bool WasRedirect { get; set; }
            public bool IsOrigional { get; set; }
        }

        void SkinContainer(BasePlayer player, List<Item> items, ref PlayerSettings playerSettings, bool findSkins = false, SkinData skinInfo = null, List<InvItem> inventoryItems = null, bool isInventory = true)
        {
            if (items == null || items.Count == 0 || player == null || playerSettings == null) return;

            Dictionary<int, SkinData> foundSkins = new Dictionary<int, SkinData>();
            bool isSkinInfoOriginalNull = skinInfo == null;

            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i];
                if (item == null) continue;
                if (_config.DisallowedSkinIds.Contains(item.skin)) continue;

                int redirectItemId = CheckItemRedirect(item.info.itemid);

                if (inventoryItems != null)
                {
                    InvItem invItem = inventoryItems.FirstOrDefault(x => x.NewSkinId != FalseSkinId && x.NewSkinId >= 0 && x.Position == item.position && x.ItemId == item.info.itemid);
                    if (invItem != null)
                    {
                        skinInfo = FindSkin(player, ref playerSettings, item, redirectItemId, invItem.NewSkinId);
                    }
                }
                else if (findSkins)
                {
                    if (!foundSkins.TryGetValue(item.info.itemid, out skinInfo))
                    {
                        skinInfo = FindSkin(player, ref playerSettings, item);
                        if (skinInfo != null) foundSkins[item.info.itemid] = skinInfo;
                    }
                }

                if (skinInfo == null || (!isSkinInfoOriginalNull && redirectItemId != skinInfo.ItemID)) continue;

                bool isOriginal = !skinInfo.Redirect.IsRedirect;
                bool wasRedirect = redirectItemId != item.info.itemid;

                if (skinInfo.Redirect.IsRedirect || wasRedirect)
                {
                    RedirectItem(player, item, skinInfo, wasRedirect, isOriginal);
                }
                else
                {
                    SkinItem(item, skinInfo);
                }

                if (isInventory)
                {
                   // Backpacks?.Call("API_MutateBackpackItems", player.userID, new Dictionary<string, object> { ["ItemId"] = item.info.itemid }, new Dictionary<string, object> { ["SkinId"] = skinInfo.SkinID });
                }

                if (isSkinInfoOriginalNull) skinInfo = null;
            }

            if (_config.SoundOnSkin) PlaySound(player);
        }

        private void UnsubscribeHooks()
        {
            if (!_config.SkinItemsOnCraft) Unsubscribe(nameof(OnItemCraftFinished));
            if (!_config.SkinItemsOnPickup) Unsubscribe(nameof(OnItemPickup));
            if (!_config.SkinKitsOnRedeemed) Unsubscribe(nameof(OnKitRedeemed));
        }

        void SkinItem(Item item, SkinData skinInfo)
        {
            item.skin = skinInfo.SkinID;
            if (_config.ChangeItemName) item.name = skinInfo.SkinName;

            BaseEntity heldEntity = item.GetHeldEntity();
            if (heldEntity)
            {
                heldEntity.skinID = skinInfo.SkinID;
                heldEntity.SendNetworkUpdate();
            }

            item.MarkDirty();
        }
        #endregion

        #region Methods
        void SelectOutfit(BasePlayer player, ref PlayerSettings playerSettings, string[] args)
        {
            string loadedItems = playerSettings.LoadedItems;
            string newName = String.Join(" ", args.Skip(1));

            if (!string.IsNullOrEmpty(playerSettings.LoadedItems) && loadedItems.Equals(newName, StringComparison.OrdinalIgnoreCase))
            {
                playerSettings.LoadedItems = null;
                LoadDefaultOutfit(player, ref playerSettings);
            }
            else
            {
                playerSettings.LoadedItems = newName;
                playerSettings.SavedItems = playerSettings.Outfits.FirstOrDefault(x => x.OutfitName.Equals(newName, StringComparison.OrdinalIgnoreCase)).OutfitDetails;
            }

            UIWriteMainActiveLoadout(player, playerSettings);
        }

        void SkinTeam(BasePlayer player, ref PlayerSettings playerSettings)
        {
            foreach (var member in player.Team?.members)
            {
                BasePlayer teamMember = BasePlayer.FindByID(member);
                if (teamMember == null || !teamMember.IsConnected) continue;

                List<Item> itemList = Pool.Get<List<Item>>();
                player.inventory.GetAllItems(itemList);

                SkinContainer(player, itemList, ref playerSettings, true);

                Pool.FreeUnmanaged(ref itemList);
            }
        }

        void FavoriteOutfit(BasePlayer player, ref PlayerSettings playerSettings, string[] args)
        {
            int UIPos = int.Parse(args[1]);
            int newFavoriteInt = UIPos;
            int oldFavoriteInt = playerSettings.Outfits.FindIndex(x => x.Favorite);

            var uiPage = UIPos == 0 ? 1 : (newFavoriteInt + 6) / 6;
            int oldUIPage = oldFavoriteInt == 0 ? 1 : (oldFavoriteInt + 6) / 6;

            if (newFavoriteInt == oldFavoriteInt) return;

            Outfits oldFavorite = null;
            Outfits newFavorite = playerSettings.Outfits[newFavoriteInt];

            if (oldFavoriteInt != -1)
            {
                oldFavorite = playerSettings.Outfits[oldFavoriteInt];
                oldFavorite.Favorite = false;
                if (oldUIPage == uiPage) UIWriteAlterFavoriteButton(player, ref playerSettings, oldFavoriteInt, oldFavorite);
            }

            newFavorite.Favorite = true;
            UIWriteAlterFavoriteButton(player, ref playerSettings, UIPos, newFavorite);
        }

        void BlacklistSkin(BasePlayer player, ref PlayerSettings playerSettings, string[] args)
        {
            ulong skinId = ulong.Parse(args[2]);
            int itemId = int.Parse(args[1]);
            _config.AddedSkins.RemoveAll(x => x.SkinID == skinId);
            _skinList[itemId].RemoveAll(x => x.SkinID == skinId);

            _config.BlacklistedSkins.Add(skinId);

            SaveConfig();
            CuiHelper.DestroyUi(player, "SCSelectPanel");
            UIWriteSkinsPanel(player, playerSettings);
        }

        void CloseLoadoutItem(BasePlayer player, ref PlayerSettings playerSettings, string[] args)
        {
            int i = int.Parse(args[1]);
            OutfitDetail skinInfo = playerSettings.SavedItems[i];
            UIAlterLoadoutItemSelect(player, ref playerSettings, skinInfo, i);
        }

        void DeleteLoadoutItem(BasePlayer player, ref PlayerSettings playerSettings, string[] args)
        {
            playerSettings.SavedItems.RemoveAt(int.Parse(args[1]));
            UIWriteLoadoutPanel(player, playerSettings);
        }

        void HandleOutfitSaveOverride(BasePlayer player, ref PlayerSettings playerSettings, bool isSave = false)
        {
            string saveName = playerSettings.ActiveSaveName;
            string loadedName = playerSettings.LoadedItems == null ? string.Empty : playerSettings.LoadedItems;
            int saveCount = GetSaveCount(player);
            if (string.IsNullOrEmpty(saveName))
            {
                UIAlterSaveButton(player, playerSettings, "Invalid name", _uiColors.UIDangerButtonColor);
                return;
            }

            if ((isSave || (!string.IsNullOrEmpty(loadedName) && !loadedName.Equals(saveName, StringComparison.OrdinalIgnoreCase))) && playerSettings.Outfits.Any(x => x.OutfitName.Equals(saveName, StringComparison.OrdinalIgnoreCase)))
            {
                UIAlterSaveButton(player, playerSettings, "Name already exists", _uiColors.UIDangerButtonColor);
                return;
            }

            if (saveName.Length > 20)
            {
                UIAlterSaveButton(player, playerSettings, "Too many characters", _uiColors.UIDangerButtonColor);
                return;
            }

            if (playerSettings.Outfits.Count >= saveCount)
            {
                UIAlterSaveButton(player, playerSettings, "Max saves reached!", _uiColors.UIDangerButtonColor);
                return;
            }

            var outfit = isSave ? new Outfits() : playerSettings.Outfits.FirstOrDefault(x => x.OutfitName.Equals(loadedName, StringComparison.OrdinalIgnoreCase));
            if (outfit == null)
            {
                UIAlterSaveButton(player, playerSettings, "Error! Contact admin!", _uiColors.UIDangerButtonColor);
                return;
            }

            outfit.OutfitName = saveName;
            outfit.OutfitDetails = new List<OutfitDetail>(playerSettings.SavedItems.Select(item => new OutfitDetail
            {
                ItemId = item.ItemId,
                Shortname = item.Shortname,
                SkinId = item.SkinId
            }));

            if (isSave)
            {
                if (playerSettings.Outfits.Count == 0) outfit.Favorite = true;
                playerSettings.Outfits.Add(outfit);
            }

            playerSettings.LoadedItems = saveName;
            playerSettings.ActiveSaveName = null;
            CuiHelper.DestroyUi(player, "SCOutfitEditPanel");

            UIInit(player, 1);
        }

        private int GetSaveCount(BasePlayer player)
        {
            if (_config.OutfitPermissions.Count != 0)
            {
                var sortedPermissions = _config.OutfitPermissions.OrderByDescending(pair => pair.Value).ToDictionary(pair => pair.Key, pair => pair.Value);
                var maxSaves = 0;
                foreach (var perm in sortedPermissions)
                {
                    if (!permission.UserHasPermission(player.UserIDString, perm.Key)) continue;
                    else
                    {
                        maxSaves = perm.Value;
                        break;
                    }
                }
                return maxSaves;
            }
            return 10;
        }

        void HandleOutfitName(BasePlayer player, ref PlayerSettings playerSettings, string[] args)
        {
            if (args.Length < 2) playerSettings.ActiveSaveName = null;
            else
            {
                string joinedString = String.Join(" ", args.Skip(1));
                bool isOnlyLetters = joinedString.All(c => char.IsLetter(c) || c == ' ');
                playerSettings.ActiveSaveName = joinedString.Equals("SET NAME", StringComparison.OrdinalIgnoreCase) ? null : joinedString;
            }

            UIAlterOutfitName(player, ref playerSettings);
        }

        private string convertedShortnames(string deployedShortname)
        {
            if (_namesToConvert.ContainsKey(deployedShortname)) return _namesToConvert[deployedShortname];
            return deployedShortname;
        }

        private void CreateGameTip(BasePlayer player, float length, int type, string text)
        {
            if (player == null) return;
            player.SendConsoleCommand("gametip.hidegametip");
            player.SendConsoleCommand("gametip.showtoast", type, text);
            timer.Once(length, () => player?.SendConsoleCommand("gametip.hidegametip"));
        }

        int CheckItemRedirect(int itemId)
        {
            if (_itemRedirects.TryGetValue(itemId, out var redirect));
            else redirect = itemId;

            return redirect;
        }

        void LoadDefaultOutfit(BasePlayer player, ref PlayerSettings playerSettings)
        {
            playerSettings.SavedItems = new List<OutfitDetail>(_config.DefaultOutfit.Select(x => new OutfitDetail()
            {
                ItemId = x.ItemId,
                Shortname = x.Shortname,
                SkinId = x.SkinId
            }));
        }

        void DeleteOutfit(BasePlayer player, ref PlayerSettings playerSettings)
        {
            if (string.IsNullOrEmpty(playerSettings.LoadedItems))
            {
                LoadDefaultOutfit(player, ref playerSettings);
                UIWriteMainActiveLoadout(player, playerSettings);
                return;
            }

            var outfitName = playerSettings.LoadedItems;

            var theOutfit = playerSettings.Outfits.FirstOrDefault(x => x.OutfitName.Equals(outfitName, StringComparison.OrdinalIgnoreCase));
            if (theOutfit == null) return;

            bool wasFavorite = theOutfit.Favorite;

            playerSettings.Outfits.Remove(theOutfit);
            playerSettings.SavedItems = new List<OutfitDetail>();
            playerSettings.LoadedItems = null;

            if (wasFavorite && playerSettings.Outfits.Count > 0) playerSettings.Outfits[0].Favorite = true;
            else LoadDefaultOutfit(player, ref playerSettings);

            UIWriteMainActiveLoadout(player, playerSettings);
        }

        void NewOutfitSet(BasePlayer player, ref PlayerSettings playerSettings)
        {
            playerSettings.LoadedItems = null;
            playerSettings.SavedItems = new List<OutfitDetail>();
            LoadDefaultOutfit(player, ref playerSettings);

            UIWriteMainActiveLoadout(player, playerSettings);
        }

        void ClearOutfitSkins(BasePlayer player, ref PlayerSettings playerSettings)
        {
            foreach (var skin in playerSettings.SavedItems)
            {
                skin.SkinId = 0;
            }
        }

        void ApplyLoadoutToIndSkins(BasePlayer player, PlayerSettings playerSettings)
        {
            ulong CurrentSkinID = playerSettings.IndSettings.SkinID;

            int i = -1;
            foreach (var item in playerSettings.IndSettings.InventoryItems)
            {
                i++;
                SkinData skinInfo = FindSkin(player, ref playerSettings, null, item.ItemId, useFirst: true);
                if (skinInfo == null) continue;

                playerSettings.IndSettings.SkinID = skinInfo.SkinID;

                AlterInvItem(player, item.Category, item.Position, playerSettings, true, i);
            }

            playerSettings.IndSettings.SkinID = CurrentSkinID;
        }

        void PlaySound(BasePlayer player)
        {
            Effect effect = new Effect();
            effect.Init(Effect.Type.Generic, player, 0, Vector3.zero, Vector3.forward, player.limitNetworking ? player.Connection : null);
            effect.pooledString = "assets/prefabs/missions/portal/proceduraldungeon/effects/appear.prefab";

            if (player.limitNetworking) EffectNetwork.Send(effect, player.Connection);
            else EffectNetwork.Send(effect);
        }

        void IndSkinsFilter(BasePlayer player, PlayerSettings playerSettings, string[] args)
        {
            if (args == null || args.Length < 1) playerSettings.SkinsFilter = string.Empty;
            else
            {
                string joinedString = String.Join(" ", args.Skip(1));
                bool isOnlyLetters = joinedString.All(c => char.IsLetter(c) || c == ' ');
                playerSettings.SkinsFilter = joinedString.Equals("SEARCH", StringComparison.OrdinalIgnoreCase) ? null : joinedString;
            }

            playerSettings.SkinPage = 0;
            UIWriteIndSkinPanel(player, playerSettings);
        }

        void CommandSkinsFilter(BasePlayer player, PlayerSettings playerSettings, string[] args)
        {
            if (args == null || args.Length < 1) playerSettings.SkinsFilter = string.Empty;
            else
            {
                string joinedString = String.Join(" ", args.Skip(1));
                bool isOnlyLetters = joinedString.All(c => char.IsLetter(c) || c == ' ');
                playerSettings.SkinsFilter = joinedString.Equals("SEARCH", StringComparison.OrdinalIgnoreCase) ? null : joinedString;
            }

            playerSettings.SkinPage = 0;
            UIAddCommandSkins(player, ref playerSettings);
        }

        void SkinsFilter(BasePlayer player, PlayerSettings playerSettings, string[] args)
        {
            if (args == null || args.Length < 1) playerSettings.SkinsFilter = string.Empty;
            else
            {
                string joinedString = String.Join(" ", args.Skip(1));
                bool isOnlyLetters = joinedString.All(c => char.IsLetter(c) || c == ' ');
                playerSettings.SkinsFilter = joinedString.Equals("SEARCH", StringComparison.OrdinalIgnoreCase) ? null : joinedString;
            }

            if (playerSettings.SkinsFilter != null)
            {
                playerSettings.SkinPage = 0;
                UIWriteItemPanel(player, playerSettings);
                UIWriteCategoryPanel(player, playerSettings);
                UIWriteSkinsPanel(player, playerSettings);
            }
        }

        void FindSkins(BasePlayer player, PlayerSettings playerSettings, string[] args)
        {
            string skinName = args[1];

            List<OutfitDetail> outfitItems = new List<OutfitDetail>();

            foreach (var item in _skinList)
            {
                OutfitDetail foundSkin = null;

                foreach (var skin in item.Value)
                {
                    if (skin.SkinName.Contains(skinName))
                    {
                        foundSkin = new OutfitDetail()
                        {
                            SkinId = skin.SkinID,
                            ItemId = skin.ItemID,
                            Shortname = skin.ItemShortname
                        };

                        break;
                    }
                }

                if (foundSkin != null) outfitItems.Add(foundSkin);
            }

            if (outfitItems.Count > 0)
            {
                playerSettings.SavedItems = new List<OutfitDetail>(outfitItems);
                UIWriteLoadoutPanel(player, playerSettings);
            }
        }

        void EditLoadoutItem(BasePlayer player, PlayerSettings playerSettings, string[] args)
        {
            int i = int.Parse(args[1]);
            OutfitDetail skinInfo = playerSettings.SavedItems[i];
            int itemId = skinInfo.ItemId;

            var category = _categoryList.FirstOrDefault(x => x.Value.Contains(itemId));
            if (playerSettings.Category != category.Key)
            {
                playerSettings.Category = category.Key;
                UIWriteCategoryPanel(player, playerSettings);
            }

            if (playerSettings.CategoryItem != itemId)
            {
                playerSettings.CategoryItem = itemId;

                UIWriteItemPanel(player, playerSettings);
                UIWriteSkinsPanel(player, playerSettings);
            }

            UIAlterLoadoutItemSelect(player, ref playerSettings, skinInfo, i);
        }

        void SelectCategory(BasePlayer player, PlayerSettings playerSettings, string[] args)
        {
            if (playerSettings.Category == args[1]) return;

            playerSettings.Category = args[1];
            playerSettings.CategoryItem = _categoryList[args[1]][0];
            playerSettings.ItemPage = 0;
            playerSettings.SkinPage = 0;
            if (!string.IsNullOrEmpty(playerSettings.SkinsFilter)) playerSettings.SkinsFilter = string.Empty;
            UIWriteCategoryPanel(player, playerSettings);
            UIWriteItemPanel(player, playerSettings);
            UIWriteSkinsPanel(player, playerSettings);
        }

        void SelectItem(BasePlayer player, PlayerSettings playerSettings, string[] args)
        {
            var lastItem = playerSettings.CategoryItem;
            var currentItem = int.Parse(args[1]);

            if (lastItem == currentItem) return;
            playerSettings.CategoryItem = currentItem;
            playerSettings.SkinPage = 0;

            if (!string.IsNullOrEmpty(playerSettings.SkinsFilter))
            {
                playerSettings.SkinsFilter = string.Empty;
                UIWriteItemPanel(player, playerSettings);
                UIWriteCategoryPanel(player, playerSettings);
            }
            else
            {
                AlterAddItem(player, $"{lastItem}", playerSettings.ItemUIPostition.yMin, playerSettings.ItemUIPostition.yMax, playerSettings);
                AlterAddItem(player, args[1], args[2], args[3], playerSettings);
            }

            UIWriteSkinsPanel(player, playerSettings);
        }

        void SelectSkin(BasePlayer player, PlayerSettings playerSettings, string[] args)
        {
            var ui = playerSettings.IndSettings.LastUIPosition;
            var lastSkinId = $"{playerSettings.IndSettings.SkinID}";

            playerSettings.IndSettings.SkinID = ulong.Parse(args[2]);
            playerSettings.IndSettings.SelectedItemId = int.Parse(args[7]);

            AlterAddSkin(player, lastSkinId, ui.xMin, ui.yMin, ui.xMax, ui.yMax, ref playerSettings);

            AlterAddSkin(player, args[2], args[3], args[4], args[5], args[6], ref playerSettings);

            if (playerSettings.IndSettings.ItemPos != -1)
            {
                var UIPos = playerSettings.IndSettings.SelectedUIPosition;
                var theItem = playerSettings.IndSettings.InventoryItems[playerSettings.IndSettings.ItemPos];

                AlterInvItem(player, theItem.Category, theItem.Position, playerSettings, true, playerSettings.IndSettings.ItemPos);
            }
        }

        void SelectInvItem(BasePlayer player, PlayerSettings playerSettings, bool isSelecting, int arrayId, string[] args, int redirId)
        {
            if (playerSettings.IndSettings.ItemPos != arrayId)
            {
                playerSettings.IndSettings.ItemPos = arrayId;
                playerSettings.IndSettings.SelectedUIPosition = new UIPosition()
                {
                    xMin = args[3],
                    yMin = args[4],
                    xMax = args[5],
                    yMax = args[6]
                };
            }

            AlterInvItem(player, int.Parse(args[1]), int.Parse(args[2]), playerSettings, isSelecting, arrayId);
        }

        void SelectInventoryItem(BasePlayer player, ref PlayerSettings playerSettings, string[] args)
        {
            int foundItem = playerSettings.IndSettings.InventoryItems.FindIndex(x => x.Category == int.Parse(args[1]) && x.Position == int.Parse(args[2]));
            if (foundItem == -1) return;
            InvItem theItem = playerSettings.IndSettings.InventoryItems[foundItem];
            int theItemRedir = CheckItemRedirect(theItem.ItemId);

            if (theItem.ItemId != playerSettings.IndSettings.ItemID)
            {
                playerSettings.IndSettings.ItemID = theItemRedir;
                playerSettings.IndSettings.SkinID = _skinList[theItemRedir].First().SkinID;
                playerSettings.IndSettings.SelectedItemId = theItem.ItemId;

                if (playerSettings.IndSettings.ItemPos != foundItem)
                {
                    playerSettings.IndSettings.ItemPos = foundItem;
                    playerSettings.IndSettings.SelectedUIPosition = new UIPosition()
                    {
                        xMin = args[3],
                        yMin = args[4],
                        xMax = args[5],
                        yMax = args[6]
                    };
                }

                UIWriteIndSkinPanel(player, playerSettings);
            }
            else SelectInvItem(player, playerSettings, true, foundItem, args, theItemRedir);
        }

        void DeselectInventoryItem(BasePlayer player, ref PlayerSettings playerSettings, string[] args)
        {
            var foundItem = playerSettings.IndSettings.InventoryItems.FindIndex(x => x.Category == int.Parse(args[1]) && x.Position == int.Parse(args[2]));
            if (foundItem == -1) return;

            InvItem theItem = playerSettings.IndSettings.InventoryItems[foundItem];
            int redirId = CheckItemRedirect(theItem.ItemId);
            if (redirId != playerSettings.IndSettings.ItemID)
            {
                playerSettings.IndSettings.SkinID = _skinList[redirId].First().SkinID;
                playerSettings.IndSettings.ItemID = redirId;
                UIWriteIndSkinPanel(player, playerSettings);
                return;
            }

            if (theItem.NewSkinId != playerSettings.IndSettings.SkinID)
            {
                theItem.NewSkinId = playerSettings.IndSettings.SkinID;
                theItem.NewItemId = 0;
                SelectInvItem(player, playerSettings, true, foundItem, args, redirId);
                return;
            }

            theItem.NewSkinId = FalseSkinId;
            theItem.NewItemId = 0;
            SelectInvItem(player, playerSettings, false, foundItem, args, redirId);
        }

        private void HandleCommands(bool reRegister = false)
        {
            if (!usingWC) foreach (var command in _config.Commands.SkinMenuCommand)
                    cmd.AddChatCommand(command, this, UIMain);

            if (!usingWC) foreach (var command in _config.Commands.SkinOutfitCommand)
                    cmd.AddChatCommand(command, this, UIOutfit);

            if (reRegister) return;

            foreach (var command in _config.Commands.SkinItemCommand)
                cmd.AddChatCommand(command, this, SkinItemRaycast);

            foreach (var command in _config.Commands.SkinBaseCommand)
                cmd.AddChatCommand(command, this, SkinBase);

            foreach (var command in _config.Commands.SkinItemsInContainerCommand)
                cmd.AddChatCommand(command, this, SkinPlacedContainer);
        }

        private void HandlePermissions()
        {
            foreach (var perm in Permissions)
            {
                if (!permission.PermissionExists(perm, this))
                {
                    permission.RegisterPermission(perm, this);
                }
            }

            foreach (var perm in _config.OutfitPermissions)
            {
                if (!permission.PermissionExists(perm.Key, this))
                {
                    permission.RegisterPermission(perm.Key, this);
                }
            }
        }

        void StatLastUpdatedCheck() => timer.Every(600f, CheckLastUsed);

        void CheckLastUsed()
        {
            List<ulong> remove = new List<ulong>();
            foreach (var player in _playerSettings)
            {
                if (player.Value.LastUsed + 600 < DateTimeOffset.Now.ToUnixTimeSeconds())
                {
                    remove.Add(player.Key);
                }
            }

            foreach (var player in remove)
            {
                SavePlayerSettings(null, player);
                _playerSettings.Remove(player);
            }
        }

        bool CheckCooldown(ulong steamID)
        {
            if (_cooldowns.ContainsKey(steamID))
            {
                if (_cooldowns[steamID].Subtract(DateTime.Now).TotalSeconds >= 0) return false;
                else
                {
                    _cooldowns[steamID] = DateTime.Now.AddSeconds(0.5f);
                    return true;
                }
            }
            else _cooldowns[steamID] = DateTime.Now.AddSeconds(0.5f);
            return true;
        }

        bool CheckItemCooldown(ref PlayerSettings playerSettings)
        {
            if (playerSettings.ItemSkinCooldown.Subtract(DateTime.Now).TotalSeconds >= 0) return false;
            else
            {
                playerSettings.ItemSkinCooldown = DateTime.Now.AddSeconds(_config.ItemSkinCooldownSeconds);
                return true;
            }
        }

        private BaseEntity GetPlayerItem(BasePlayer player, BaseEntity item = null)
        {
            bool containsKey = _playerSkinItems.ContainsKey(player.userID);
            if (item == null) return _playerSkinItems[player.userID];

            _playerSkinItems[player.userID] = item;
            return _playerSkinItems[player.userID];
        }

        private void RemovePlayerItem(BasePlayer player)
        {
            _playerSkinItems.Remove(player.userID);
        }

        private PlayerSettings GetOrLoadPlayerSettings(BasePlayer player)
        {
            if (!_playerSettings.ContainsKey(player.userID))
            {
                LoadSavedPlayerSettings(player);
            }

            if (_playerSettings[player.userID].SavedItems.Count == 0 && _playerSettings[player.userID].LoadedItems == null) _playerSettings[player.userID].SavedItems = new List<OutfitDetail>(_config.DefaultOutfit.Select(item => new OutfitDetail
            {
                ItemId = item.ItemId,
                Shortname = item.Shortname,
                SkinId = item.SkinId
            }));

            _playerSettings[player.userID].LastUsed = DateTimeOffset.Now.ToUnixTimeSeconds();

            return _playerSettings[player.userID];
        }

        private void LoadSavedPlayerSettings(BasePlayer player)
        {
            var playerOptions = Interface.GetMod().DataFileSystem.ReadObject<PlayerSettings>($"SkinController{Path.DirectorySeparatorChar}PlayerOptions{Path.DirectorySeparatorChar}{player.userID}") ?? new PlayerSettings();
            playerOptions.SkinsFilter = null;
            playerOptions.SkinPage = 0;
            playerOptions.ItemPage = 0;
            playerOptions.Category = "Attire";
            playerOptions.CategoryItem = _categoryList["Attire"].First();

            if (playerOptions.Outfits.Count > 0)
            {
                var outfit = playerOptions.Outfits[0];
                playerOptions.LoadedItems = outfit.OutfitName;
                playerOptions.SavedItems = outfit.OutfitDetails;
            }

            List<PlayerOptionalSettings> settingsToAdd = new List<PlayerOptionalSettings>();
            List<string> settingsToRemove = new List<string>();
            foreach (var setting in _optionalSettings)
            {
                bool canUse = true;
                if (!string.IsNullOrEmpty(setting.Permission) && !permission.UserHasPermission(player.UserIDString, setting.Permission)) canUse = false;

                var checkSetting = playerOptions.PlayerOptionalSettings.FirstOrDefault(x => x.SettingName == setting.SettingName);
                if (checkSetting != null && !canUse) settingsToRemove.Add(setting.SettingName);
                if (checkSetting == null && canUse) settingsToAdd.Add(new PlayerOptionalSettings { Enabled = true, SettingName = setting.SettingName });
            }

            foreach (var setting in settingsToRemove) playerOptions.PlayerOptionalSettings.RemoveAll(x => x.SettingName == setting);
            foreach (var setting in settingsToAdd) playerOptions.PlayerOptionalSettings.Add(setting);

            _playerSettings[player.userID] = playerOptions;
        }

        private void SavePlayerSettings(BasePlayer player = null, ulong steamId = 0)
        {
            if (player != null) steamId = player.userID;
            if (!_playerSettings.ContainsKey(steamId)) return;
            Interface.GetMod().DataFileSystem.WriteObject($"SkinController{Path.DirectorySeparatorChar}PlayerOptions{Path.DirectorySeparatorChar}{steamId}", _playerSettings[steamId]);
        }

        private string GetImage(string imageName)
        {
            if (ImageLibrary == null)
            {
                PrintError("Could not load images due to no Image Library");
                return null;
            }

            return ImageLibrary?.Call<string>("GetImage", imageName, 0UL, false);
        }

        void ImportImages()
        {
            Dictionary<string, string> images = new Dictionary<string, string> {
                { "UIImage", _config.UIImage },
                { "SettingsIcon", _config.SettingsIcon },
                { "EditIcon", _config.EditIcon },
                { "InvalidItemIcon", _config.InvalidItemIcon }
            };

            ImageLibrary?.Call("ImportImageList", "SkinController", images, 0UL, true, null);
        }
        #endregion

        #region Load Skins
        private void LoadSkinList()
        {
            _skinList = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<int, List<SkinData>>>($"SkinController{Path.DirectorySeparatorChar}workshopskins");
        }

        private void SaveSkins()
        {
            Interface.GetMod().DataFileSystem.WriteObject($"SkinController{Path.DirectorySeparatorChar}workshopskins", _skinList);
        }

        void RequestSkins()
        {
            LoadSkinList();

            if ((Steamworks.SteamInventory.Definitions?.Length ?? 0) == 0) Steamworks.SteamInventory.OnDefinitionsUpdated += StartSkinRequest;
            else StartSkinRequest();
        }

        void StartSkinRequest() => ServerMgr.Instance.StartCoroutine(ProcessSkins());

        void DLCSkins(ref List<SkinData> dlcSkins, ItemDefinition item)
        {
            foreach (var skin in ItemSkinDirectory.ForItem(item))
                if (skin.id == 0 || _config.BlacklistedSkins.Contains((ulong)skin.id)) continue;
                else dlcSkins.Add(new SkinData()
                {
                    SkinID = (ulong)skin.id,
                    ItemID = item.itemid,
                    ItemShortname = item.shortname,
                    SkinName = skin.invItem.displayName.english,
                    Redirect = CheckItemRedirect(skin, item),
                });
        }

        private IEnumerator ProcessSkins()
        {
            Puts("Processing skins");

            foreach (var item in ItemManager.itemList)
            {
                List<SkinData> skinsList = new List<SkinData>();
                skinsList.Add(new SkinData { ItemID = item.itemid, ItemShortname = item.shortname, SkinID = 0, SkinName = item.displayName.english });

                var displayName = item.displayName.english.ToLower().Replace("skin", "").Replace(" ", "").Replace("-", "");
                if (!_workshopRename.ContainsKey(displayName)) _workshopRename.Add(displayName, new ClassedItem { ItemID = item.itemid, Shortname = item.shortname });
                if (!_workshopRename.ContainsKey(item.shortname)) _workshopRename.Add(item.shortname, new ClassedItem { ItemID = item.itemid, Shortname = item.shortname });
                if (!_workshopRename.ContainsKey(item.shortname.Replace(".", ""))) _workshopRename.Add(item.shortname.Replace(".", ""), new ClassedItem { ItemID = item.itemid, Shortname = item.shortname });

                var category = item.category.ToString();

                if (!_categoryList.ContainsKey(category)) _categoryList.Add(category, new List<int>());
                if (!_categoryList[category].Contains(item.itemid)) _categoryList[category].Add(item.itemid);

                DLCSkins(ref skinsList, item);

                if (!_skinList.ContainsKey(item.itemid)) _skinList.Add(item.itemid, skinsList);
                else
                {
                    foreach (var skin in skinsList)
                    {
                        int foundItem = _skinList[item.itemid].FindIndex(x => x.SkinID == skin.SkinID);
                        if (foundItem == -1) _skinList[item.itemid].Add(skin);
                        else _skinList[item.itemid][foundItem].Redirect = skin.Redirect;
                    }
                }

                yield return CoroutineEx.waitForEndOfFrame;
            }

            foreach (Steamworks.InventoryDef itemDefinition in Steamworks.SteamInventory.Definitions)
            {
                if (itemDefinition.Id < 100) continue;

                string itemShortname = itemDefinition.GetProperty("itemshortname");
                if (string.IsNullOrEmpty(itemShortname)) continue;
                if (_workshopRename.ContainsKey(itemShortname)) itemShortname = _workshopRename[itemShortname].Shortname;

                var theItem = ItemManager.FindItemDefinition(itemShortname);
                if (theItem == null) continue;
                int itemId = theItem.itemid;

                if (_config.BlacklistedSkins.Contains((ulong)itemDefinition.Id)) continue;

                if (_workshopRename.ContainsKey(itemShortname)) itemShortname = _workshopRename[itemShortname].Shortname;

                ulong skinId;
                if (!ulong.TryParse(itemDefinition.GetProperty("workshopid"), out skinId)) skinId = (ulong)itemDefinition.Id;
                if (skinId < 100000) continue;

                var skinData = new SkinData()
                {
                    SkinID = skinId,
                    ItemID = itemId,
                    ItemShortname = itemShortname,
                    SkinName = itemDefinition.Name
                };


                if (_skinList.ContainsKey(itemId))
                {
                    var currentSkins = _skinList[itemId];
                    if (currentSkins.Any(x => x.SkinID == (ulong)itemId || x.SkinID == skinId || x.SkinName == itemDefinition.Name)) continue;

                    currentSkins.Add(skinData);
                }
                else _skinList.Add(itemId, new List<SkinData>() { skinData });
            }

            Dictionary<int, string> removalList = new Dictionary<int, string>();
            Dictionary<ulong, int> skinRemovalList = new Dictionary<ulong, int>();

            foreach (var cat in _categoryList)
                foreach (var item in cat.Value)
                {
                    if (_skinList.ContainsKey(item) && _skinList[item].Count < 2)
                    {
                        removalList[item] = cat.Key;
                    }
                }

            foreach (var item in removalList)
            {
                if (_categoryList.ContainsKey(item.Value) && _categoryList[item.Value].Contains(item.Key)) _categoryList[item.Value].Remove(item.Key);
                if (_skinList.ContainsKey(item.Key)) _skinList.Remove(item.Key);
            }

            foreach (var item in _config.AddedSkins) if (_skinList.ContainsKey(item.ItemID) && !_skinList[item.ItemID].Any(x => x.SkinID == item.SkinID)) _skinList[item.ItemID].Add(item);

            foreach (var item in _skinList)
                foreach (var skin in item.Value)
                {
                    if (_config.BlacklistedSkins.Contains(skin.SkinID)) skinRemovalList.Add(skin.SkinID, item.Key);
                }

            foreach (var item in skinRemovalList) if (_skinList.ContainsKey(item.Value)) _skinList[item.Value].RemoveAll(x => x.SkinID == item.Key);

            _skinList.Remove(-1366326648);
            _categoryList["Tool"].Remove(-1366326648);

            Puts($"Successfully generated {_skinList.Keys.Count} items containing a total of {_skinList.Sum(x => x.Value.Count)} skins!");
            SkinsGenerated = true;

            SaveSkins();

            yield return null;
        }
        Redir CheckItemRedirect(ItemSkinDirectory.Skin skin, ItemDefinition itemDefinition)
        {
            Redir redirect = new Redir();

            ItemSkin itemRedirect = skin.invItem as ItemSkin;
            if (itemRedirect == null || itemRedirect.Redirect == null) return redirect;

            _itemRedirects[itemRedirect.Redirect.itemid] = itemDefinition.itemid;

            redirect.IsRedirect = true;
            redirect.Shortname = itemRedirect.Redirect.shortname;
            redirect.ItemId = itemRedirect.Redirect.itemid;

            return redirect;
        }

        public class ClassedItem
        {
            public int ItemID { get; set; }
            public string Shortname { get; set; }
        }

        private readonly Dictionary<string, ClassedItem> _workshopRename = new Dictionary<string, ClassedItem>
        {
            {"ak47", new ClassedItem { ItemID = 1545779598, Shortname = "rifle.ak" }},
            {"balaclava", new ClassedItem { ItemID = -2012470695, Shortname = "mask.balaclava" }},
            {"bandana", new ClassedItem { ItemID = -702051347, Shortname = "mask.bandana" }},
            {"bearrug", new ClassedItem { ItemID = -1104881824, Shortname = "rug.bear" }},
            {"bearskinrug", new ClassedItem { ItemID = -1104881824, Shortname = "rug.bear" }},
            {"beenie", new ClassedItem { ItemID = 1675639563, Shortname = "hat.beenie" }},
            {"boltrifle", new ClassedItem { ItemID = 1588298435, Shortname = "rifle.bolt" }},
            {"boonie", new ClassedItem { ItemID =   -23994173, Shortname = "hat.boonie" }},
            {"buckethat", new ClassedItem { ItemID = 850280505, Shortname = "bucket.helmet" }},
            {"burlapgloves", new ClassedItem { ItemID = 1366282552, Shortname = "burlap.gloves" }},
            {"burlappants", new ClassedItem { ItemID = 1992974553, Shortname = "burlap.trousers" }},
            {"cap", new ClassedItem { ItemID = -1022661119, Shortname = "hat.cap" }},
            {"collaredshirt", new ClassedItem { ItemID = -2025184684, Shortname = "shirt.collared" }},
            {"deerskullmask", new ClassedItem { ItemID = -1903165497, Shortname = "deer.skull.mask" }},
            {"hideshirt", new ClassedItem { ItemID = 196700171, Shortname = "attire.hide.vest" }},
            {"hideshoes", new ClassedItem { ItemID = 794356786, Shortname = "attire.hide.boots" }},
            {"l96", new ClassedItem { ItemID = -778367295, Shortname = "rifle.l96" }},
            {"leather.gloves", new ClassedItem { ItemID = 1366282552, Shortname = "burlap.gloves" }},
            {"longtshirt", new ClassedItem { ItemID = 935692442, Shortname = "tshirt.long" }},
            {"lr300", new ClassedItem { ItemID = -1812555177, Shortname = "rifle.lr300" }},
            {"lr300.item", new ClassedItem { ItemID = -1812555177, Shortname = "rifle.lr300" }},
            {"m39", new ClassedItem { ItemID = 28201841, Shortname = "rifle.m39" }},
            {"minerhat", new ClassedItem { ItemID = -1539025626, Shortname = "hat.miner" }},
            {"mp5", new ClassedItem { ItemID = 1318558775, Shortname = "smg.mp5" }},
            {"pipeshotgun", new ClassedItem { ItemID = -1367281941, Shortname = "shotgun.waterpipe" }},
            {"python", new ClassedItem { ItemID = 1373971859, Shortname = "pistol.python" }},
            {"roadsignvest", new ClassedItem { ItemID = -2002277461, Shortname = "roadsign.jacket" }},
            {"roadsignpants", new ClassedItem { ItemID = 1850456855, Shortname = "roadsign.kilt" }},
            {"semiautopistol", new ClassedItem { ItemID = 818877484, Shortname = "pistol.semiauto" }},
            {"sword", new ClassedItem { ItemID = 1326180354, Shortname = "salvaged.sword" }},
            {"snowjacket", new ClassedItem { ItemID = -48090175, Shortname = "jacket.snow" }},
            {"tshirt", new ClassedItem { ItemID = 223891266, Shortname = "tshirt" }},
            {"vagabondjacket", new ClassedItem { ItemID = -1163532624, Shortname = "jacket" }},
            {"woodendoubledoor", new ClassedItem { ItemID = -1336109173, Shortname = "door.double.hinged.wood" }},
            {"woodstorage", new ClassedItem { ItemID = -180129657, Shortname = "box.wooden" }},
            {"workboots", new ClassedItem { ItemID = -1549739227, Shortname = "shoes.boots" }},
            {"semi-automaticrifle", new ClassedItem { ItemID = -904863145, Shortname = "rifle.semiauto" } }
        };

        private readonly Dictionary<string, string> _namesToConvert = new Dictionary<string, string> {
            { "woodbox_deployed", "box.wooden" },
            { "waterpurifier.deployed", "water.purifier" },
            { "vendingmachine.deployed", "vending.machine" },
            { "box.wooden.large", "box.wooden.large" },
            { "rug.bear.deployed", "rug.bear" },
            { "door.hinged.toptier", "door.hinged.toptier" },
            { "door.hinged.metal", "door.hinged.metal" },
            { "wall.frame.garagedoor", "wall.frame.garagedoor" },
            { "rug.deployed", "rug" },
            { "locker.deployed", "locker" },
            { "door.double.hinged.metal", "door.double.hinged.metal" },
            { "door.double.hinged.wood", "door.double.hinged.wood" },
            { "barricade.concrete", "barricade.concrete" },
            { "sleepingbag_leather_deployed", "sleepingbag" },
            { "table.deployed", "table" },
            { "door.double.hinged.toptier", "door.double.hinged.toptier" },
            { "fridge.deployed", "fridge" },
            { "furnace", "furnace" },
            { "barricade.sandbags", "barricade.sandbags" },
            { "chair.deployed", "chair" },
            { "door.hinged.wood", "door.hinged.wood" }
        };
        #endregion

        #region Skin Individual
        void UIWriteInventoryPanel(BasePlayer player, PlayerSettings playerSettings)
        {
            var container = new CuiElementContainer();

            var panel = CreatePanel(ref container, "0 0", ".4965 .89", _uiColors.UIPrimaryColor, "SCMainOverlay", "SCInventoryPanel");
            CreateButton(ref container, ".015 .12", ".491 .22", "1 1 1 .15", "1 1 1 1", "OUTFIT PAGE", 15, "sc_main outfitpage", panel);
            CreateButton(ref container, ".504 .12", ".983 .22", "1 1 1 .15", "1 1 1 1", "APPLY TO INVENTORY", 15, "sc_main applyskins", panel);
            CreateButton(ref container, ".015 .01", ".491 .109", "1 1 1 .15", "1 1 1 1", "CLOSE", 15, "sc_main close", panel);
            CreateButton(ref container, ".504 .01", ".983 .109", "1 1 1 .15", "1 1 1 1", "APPLY LOADOUT", 15, "sc_main applyloadouttoindskins", panel);

            CuiHelper.DestroyUi(player, "SCInventoryPanel");
            CuiHelper.AddUi(player, container);

            UICreateInventory(player, playerSettings);
        }

        void UICreateInventory(BasePlayer player, PlayerSettings playerSettings)
        {
            var container = new CuiElementContainer();

            // Main
            for (int i = 0; i < 24; i++)
            {
                KeyValuePair<string, string> minMax = GenerateItemPosition(0, i);

                InvItem item = playerSettings.IndSettings.InventoryItems.FirstOrDefault(x => x.Category == 0 && x.Position == i);
                UIHandlePossibleitem(ref container, ref playerSettings, minMax.Key, minMax.Value, i, item);
            }

            // Attire
            double column = 0;
            for (int i = 0; i < 8; i++)
            {
                KeyValuePair<string, string> minMax = GenerateItemPosition(2, i);

                InvItem item = playerSettings.IndSettings.InventoryItems.FirstOrDefault(x => x.Category == 2 && x.Position == i);
                UIHandlePossibleitem(ref container, ref playerSettings, minMax.Key, minMax.Value, i, item);
            }

            // Hotbar
            column = 0;
            for (int i = 0; i < 6; i++)
            {
                KeyValuePair<string, string> minMax = GenerateItemPosition(1, i);

                InvItem item = playerSettings.IndSettings.InventoryItems.FirstOrDefault(x => x.Category == 1 && x.Position == i);
                UIHandlePossibleitem(ref container, ref playerSettings, minMax.Key, minMax.Value, i, item);

                column++;
            }

            CuiHelper.AddUi(player, container);
        }

        KeyValuePair<string, string> GenerateItemPosition(int category, int position)
        {
            KeyValuePair<string, string> minMax = new KeyValuePair<string, string>();

            switch (category)
            {
                case 0:
                    int row = position / 6;
                    int column = position % 6;

                    minMax = new KeyValuePair<string, string>($"{.08 + (column * .14)} {.864 - (row * .126)}", $"{.21 + (column * .14)} {.98 - (row * .126)}");
                    break;
                case 1:
                    minMax = new KeyValuePair<string, string>($"{.015 + (position * .14)} .233", $"{.145 + (position * .14)} .349");
                    break;
                case 2:
                    double columnPosition = position < 7 ? position : position - 1;

                    minMax = new KeyValuePair<string, string>($"{.015 + (columnPosition * .14)} {(position == 7 ? .233 : .359)}", $"{.145 + (columnPosition * .14)} {(position == 7 ? .349 : .475)}");
                    break;
            }

            return minMax;
        }


        void UIHandlePossibleitem(ref CuiElementContainer container, ref PlayerSettings playerSettings, string pos1, string pos2, int i, InvItem item = null)
        {
            if (item == null)
            {
                container.Add(new CuiElement()
                {
                    Parent = "SCInventoryPanel",
                    Name = $"itemslot_{i}",
                    Components = {
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = pos1,
                            AnchorMax = pos2
                        },
                        new CuiImageComponent()
                        {
                            Color = _uiColors.UISkinPanelColor
                        }
                    }
                });
                return;
            }

            string category = null;
            int redirId = CheckItemRedirect(item.ItemId);
            foreach (var cat in _categoryList)
            {
                if (cat.Value.Contains(redirId))
                {
                    category = cat.Key;
                    break;
                }
            }

            var itemPanel = $"{item.Category}_{item.Position}";
            container.Add(new CuiElement()
            {
                Parent = "SCInventoryPanel",
                Name = itemPanel,
                Components = {
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = pos1,
                            AnchorMax = pos2
                        },
                        new CuiImageComponent()
                        {
                            Color = _uiColors.UISkinPanelColor
                        }
                    }
            });

            container.Add(new CuiElement
            {
                Parent = itemPanel,
                Name = $"{item.Category}_{item.Position}_item",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{.1f} {.1f + .004f}",
                        AnchorMax = $"{1 - .1f - .004f} {1 - .1f - .02f}"
                    },
                    new CuiImageComponent {ItemId = item.ItemId, SkinId = item.SkinId}
                },
            });

            if (category != null)
            {
                container.Add(new CuiElement
                {
                    Parent = itemPanel,
                    Name = $"{item.Category}_{item.Position}_command",
                    Components =
                    {
                        new CuiButtonComponent()
                        {
                            Command = $"sc_main selectinvitem {item.Category} {item.Position} {pos1} {pos2}",
                            Color = "0 0 0 0"
                        }
                    },
                });

                if (playerSettings.IndSettings.ItemID == 0)
                {
                    playerSettings.IndSettings.ItemID = CheckItemRedirect(item.ItemId);
                    playerSettings.IndSettings.SelectedItemId = item.ItemId;
                }

            }
            else CreateImagePanel(ref container, "0 0", "1 .99", GetImage("InvalidItemIcon"), itemPanel);
        }

        void AlterInvItem(BasePlayer player, int category, int position, PlayerSettings playerSettings, bool isSelecting = true, int arrayId = -1, OutfitDetail oDetail = null)
        {
            var theItem = playerSettings.IndSettings.InventoryItems[arrayId];
            if (theItem == null) return;

            var currentSkinId = playerSettings.IndSettings.SkinID;
            int iId = theItem.ItemId;

            if (oDetail != null)
            {
                theItem.NewSkinId = oDetail.SkinId;
                theItem.NewItemId = oDetail.ItemId;
                iId = theItem.NewItemId;
            }
            else
            {
                if (isSelecting)
                {
                    theItem.NewSkinId = currentSkinId;
                    theItem.NewItemId = playerSettings.IndSettings.SelectedItemId;
                    iId = theItem.NewItemId;
                }
                else
                {
                    theItem.NewSkinId = FalseSkinId;
                    theItem.NewItemId = 0;
                }
            }

            var container = new CuiElementContainer();
            var itemPanel = $"{category}_{position}";
            container.Add(new CuiElement()
            {
                Name = itemPanel,
                Components = {
                    new CuiImageComponent()
                    {
                        Color = isSelecting ? _uiColors.UIActiveButtonColor : _uiColors.UISkinPanelColor
                    }
                },
                Update = true
            });

            container.Add(new CuiElement
            {
                Name = $"{category}_{position}_item",
                Components =
                {
                    new CuiImageComponent {ItemId = iId, SkinId = isSelecting ? theItem.NewSkinId : theItem.SkinId}
                },
                Update = true
            });

            container.Add(new CuiElement
            {
                Parent = itemPanel,
                Name = $"{category}_{position}_command",
                Components =
                {
                    new CuiButtonComponent()
                    {
                        Command =  $"sc_main {(isSelecting ? "deselectinvitem" : "selectinvitem")} {category} {position}",
                        Color = "0 0 0 0"
                    }
                },
                Update = true
            });

            CuiHelper.AddUi(player, container);
        }

        /*void AlterInvItem(BasePlayer player, string itemId, int cat, int pos, string xMin, string yMin, string xMax, string yMax, PlayerSettings playerSettings, bool isSelecting = true, int arrayId = -1)
        {
            var theItem = playerSettings.IndSettings.InventoryItems[arrayId];
            if (theItem == null) return;

            var currentSkinId = playerSettings.IndSettings.SkinID;
            int iId = theItem.ItemId;
            if (isSelecting)
            {
                theItem.NewSkinId = currentSkinId;
                theItem.NewItemId = playerSettings.IndSettings.SelectedItemId;
                iId = theItem.NewItemId;
            }
            else
            {
                theItem.NewSkinId = FalseSkinId;
                theItem.NewItemId = 0;
            }

            var container = new CuiElementContainer();
            var itemPanel = CreateItemPanel(ref container, $"{xMin} {yMin}", $"{xMax} {yMax}", .1f, isSelecting ? _uiColors.UIActiveButtonColor : _uiColors.UISkinPanelColor, iId, "SCInventoryPanel", $"{cat}_{pos}", isSelecting ? theItem.NewSkinId : theItem.SkinId);
            CreateButton(ref container, "0 0", "1 1", "0 0 0 0", "0 0 0 0", " ", 15, $"sc_main {(isSelecting ? "deselectinvitem" : "selectinvitem")} {cat} {pos} {xMin} {yMin} {xMax} {yMax}", itemPanel);

            CuiHelper.DestroyUi(player, $"{cat}_{pos}");
            CuiHelper.AddUi(player, container);
        }
        */
        void UIWriteIndSkinPanel(BasePlayer player, PlayerSettings playerSettings)
        {
            var container = new CuiElementContainer();

            if (playerSettings.IndSettings.ItemID == 0)
            {
                int itemId = CheckItemRedirect(_categoryList.First().Value.First());
                playerSettings.IndSettings.ItemID = itemId;
                playerSettings.IndSettings.SelectedItemId = itemId;
            }

            var item = ItemManager.FindItemDefinition(playerSettings.IndSettings.ItemID);

            List<SkinData> skins = new List<SkinData>();
            if (!string.IsNullOrEmpty(playerSettings.SkinsFilter))
            {
                foreach (var items in _skinList[playerSettings.IndSettings.ItemID])
                {
                    if (items.SkinName.Contains(playerSettings.SkinsFilter, System.Globalization.CompareOptions.OrdinalIgnoreCase)) skins.Add(items);

                }
            }
            else skins = _skinList[playerSettings.IndSettings.ItemID];

            var maxPage = skins.Count() / 42;
            if (maxPage < playerSettings.SkinPage) playerSettings.SkinPage = 0;
            if (playerSettings.SkinPage < 0) playerSettings.SkinPage = maxPage;

            var panel = CreatePanel(ref container, ".5035 0", "1 .89", _uiColors.UIPrimaryColor, "SCMainOverlay", "CSIndSkinPanel");
            CreateLabel(ref container, ".01 .93", ".99 .99", _uiColors.UISecondaryColor, "1 1 1 1", item.displayName.english, 20, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".01 .088", ".99 .152", "sc_main indfilter", _uiColors.UISearchPanelColor, "1 1 1 .6", string.IsNullOrEmpty(playerSettings.SkinsFilter) ? Lang("Search", player.UserIDString) : playerSettings.SkinsFilter, 15, TextAnchor.MiddleCenter, panel);

            CreateButton(ref container, ".01 .01", ".349 .077", _uiColors.UISearchPanelColor, "1 1 1 1", "<", 15, $"sc_main selectpage {playerSettings.SkinPage - 1}", panel);
            CreateLabel(ref container, ".36 .01", ".642 .08", _uiColors.UISearchPanelColor, "1 1 1 1", $"{playerSettings.SkinPage + 1} / {maxPage + 1}", 15, TextAnchor.MiddleCenter, panel);
            CreateButton(ref container, ".65 .01", ".99 .077", _uiColors.UISearchPanelColor, "1 1 1 1", ">", 15, $"sc_main selectpage {playerSettings.SkinPage + 1}", panel);

            int i = 0;
            int row = 0;
            foreach (var skin in skins.Skip(playerSettings.SkinPage * 42).Take(42))
            {
                UIAddIndSkin(ref container, player, "CSIndSkinPanel", i, skin, row, ref playerSettings);
                i++;
                if (i % 7 == 0) row++;
            }

            CuiHelper.DestroyUi(player, "CSIndSkinPanel");
            CuiHelper.AddUi(player, container);
        }

        void UIAddIndSkin(ref CuiElementContainer container, BasePlayer player, string panel, int i, SkinData skinInfo, int row, ref PlayerSettings playerSettings)
        {
            bool activeButton = playerSettings.IndSettings.SkinID == skinInfo.SkinID;

            var xMin = $"{.01 + (i % 7 * .141)}";
            var yMin = $"{.8 - (row * .128)}";
            var xMax = $"{.143 + (i % 7 * .141)}";
            var yMax = $"{.92 - (row * .128)}";

            if (activeButton)
            {
                playerSettings.IndSettings.LastUIPosition = new UIPosition()
                {
                    yMin = yMin,
                    yMax = yMax,
                    xMin = xMin,
                    xMax = xMax
                };
            }

            var itemId = skinInfo.Redirect.IsRedirect ? skinInfo.Redirect.ItemId : playerSettings.IndSettings.ItemID;
            ulong skinId = skinInfo.Redirect.IsRedirect ? 0 : skinInfo.SkinID;
            CreateSkinPanel(ref container, $"{xMin} {yMin}", $"{xMax} {yMax}", .1f, activeButton ? _uiColors.UIActiveButtonColor : _uiColors.UISkinPanelColor, itemId, panel, $"{skinInfo.SkinID}", skinId, i * .07f, true, $"sc_main selectindskin {itemId} {skinInfo.SkinID} {xMin} {yMin} {xMax} {yMax} {(skinInfo.Redirect.IsRedirect ? skinInfo.Redirect.ItemId : skinInfo.ItemID)}");

            return;
        }

        void AlterAddSkin(BasePlayer player, string panelName, string xMin, string yMin, string xMax, string yMax, ref PlayerSettings playerSettings)
        {
            ulong skinId = ulong.Parse(panelName);

            bool activeButton = playerSettings.IndSettings.SkinID == skinId;
            if (activeButton)
            {
                playerSettings.IndSettings.LastUIPosition = new UIPosition()
                {
                    yMin = $"{yMin}",
                    yMax = $"{yMax}",
                    xMin = $"{xMin}",
                    xMax = $"{xMax}"
                };
            }

            var container = new CuiElementContainer();

            SkinData skinInfo = _skinList[playerSettings.IndSettings.ItemID].FirstOrDefault(x => x.SkinID == skinId);
            var itemId = skinInfo.Redirect.IsRedirect ? skinInfo.Redirect.ItemId : playerSettings.IndSettings.ItemID;

            CreateSkinPanel(ref container, $"{xMin} {yMin}", $"{xMax} {yMax}", .1f, activeButton ? _uiColors.UIActiveButtonColor : _uiColors.UISkinPanelColor, itemId, "CSIndSkinPanel", panelName, skinId, 0f, true, $"sc_main selectindskin {itemId} {skinId} {xMin} {yMin} {xMax} {yMax} {(skinInfo.Redirect.IsRedirect ? skinInfo.Redirect.ItemId : skinInfo.ItemID)}");

            CuiHelper.DestroyUi(player, panelName);
            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region Skin Command UI
        void UIWriteSkinCommandUI(BasePlayer player, PlayerSettings playerSettings)
        {
            var container = new CuiElementContainer();

            playerSettings.SkinPage = 0;

            CreatePanel(ref container, ".7 .28", ".99 .9", _uiColors.UISkinCommandPanelColor, "Overlay", "UIMainSkinPanel", isMainPanel: true);
            CreateButton(ref container, ".01 .01", ".988 .07", _uiColors.UISkinCommandCloseButtonColor, "1 1 1 1", Lang("Close", player.UserIDString), 15, "sc_main closecommand", "UIMainSkinPanel");

            CuiHelper.DestroyUi(player, "UIMainSkinPanel");
            CuiHelper.AddUi(player, container);

            UIAddCommandSkins(player, ref playerSettings);
        }

        void UIAddCommandSkins(BasePlayer player, ref PlayerSettings playerSettings)
        {
            var playerItem = GetPlayerItem(player);
            if (playerItem == null) return;

            var container = new CuiElementContainer();

            var panel = CreatePanel(ref container, "0 .07", "1 1", "0 0 0 0", "UIMainSkinPanel", "UIMainSkinPanelOverlay");

            var itemInfo = _skinList.SelectMany(x => x.Value).FirstOrDefault(y => y.ItemShortname == convertedShortnames(playerItem.ShortPrefabName));
            if (itemInfo == null)
            {
                CreateLabel(ref container, "0 .4", "1 .6", "0 0 0 0", "1 1 1 .3", "NO SKINS", 30, TextAnchor.MiddleCenter, panel);
                CuiHelper.DestroyUi(player, "UIMainSkinPanelOverlay");
                CuiHelper.AddUi(player, container);
                return;
            }
            int itemId = itemInfo.ItemID;
            ItemDefinition item = ItemManager.FindItemDefinition(itemId);

            List<SkinData> skins = new List<SkinData>();
            if (!string.IsNullOrEmpty(playerSettings.SkinsFilter))
            {
                foreach (var items in _skinList[item.itemid])
                {
                    if (items.SkinName.Contains(playerSettings.SkinsFilter, System.Globalization.CompareOptions.OrdinalIgnoreCase)) skins.Add(items);

                }
            }
            else skins = _skinList[item.itemid];

            if (skins.Count < 1)
            {
                CreateLabel(ref container, "0 0", "1 1", "0 0 0 0", "1 1 1 .3", "NO SKINS", 30, TextAnchor.MiddleCenter, panel);
                CuiHelper.DestroyUi(player, "UIMainSkinPanelOverlay");
                CuiHelper.AddUi(player, container);
                return;
            }

            var maxPage = skins.Count() / 42;
            if (maxPage < playerSettings.SkinPage) playerSettings.SkinPage = 0;
            if (playerSettings.SkinPage < 0) playerSettings.SkinPage = maxPage;

            CreateLabel(ref container, ".01 .93", ".99 .99", _uiColors.UISkinCommandTitleColor, "1 1 1 1", item.displayName.english, 20, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".01 .088", ".99 .152", "sc_main commandfilter", _uiColors.UISkinCommandTitleColor, "1 1 1 .6", string.IsNullOrEmpty(playerSettings.SkinsFilter) ? Lang("Search", player.UserIDString) : playerSettings.SkinsFilter, 15, TextAnchor.MiddleCenter, panel);

            CreateButton(ref container, ".01 .01", ".349 .077", _uiColors.UISkinCommandPageButtonColor, "1 1 1 1", "<", 15, $"sc_main commandspage {playerSettings.SkinPage - 1}", panel);
            CreateLabel(ref container, ".36 .01", ".642 .08", _uiColors.UISkinCommandPagePanelColor, "1 1 1 1", $"{playerSettings.SkinPage + 1} / {maxPage + 1}", 15, TextAnchor.MiddleCenter, panel);
            CreateButton(ref container, ".65 .01", ".99 .077", _uiColors.UISkinCommandPageButtonColor, "1 1 1 1", ">", 15, $"sc_main commandspage {playerSettings.SkinPage + 1}", panel);

            int i = 0;
            int row = 0;
            foreach (var skin in skins.Skip(playerSettings.SkinPage * 42).Take(42))
            {
                UIAddCommandSkin(ref container, player, "UIMainSkinPanelOverlay", i, skin, row, ref playerSettings);
                i++;
                if (i % 7 == 0) row++;
            }

            CuiHelper.DestroyUi(player, "UIMainSkinPanelOverlay");
            CuiHelper.AddUi(player, container);
        }

        void UIAddCommandSkin(ref CuiElementContainer container, BasePlayer player, string panel, int i, SkinData skinInfo, int row, ref PlayerSettings playerSettings)
        {
            var xMin = $"{.01 + (i % 7 * .141)}";
            var yMin = $"{.8 - (row * .128)}";
            var xMax = $"{.143 + (i % 7 * .141)}";
            var yMax = $"{.92 - (row * .128)}";

            CreateSkinPanel(ref container, $"{xMin} {yMin}", $"{xMax} {yMax}", .1f, _uiColors.UISkinCommandSkinPanelColor, skinInfo.ItemID, panel, $"{skinInfo.SkinID}", skinInfo.SkinID, i * .07f, true, $"sc_main selectcommandskin {skinInfo.SkinID}");

            return;
        }

        #endregion

        #region Outfit UI
        void UIWriteOutfitEdit(BasePlayer player, ref PlayerSettings playerSettings)
        {
            var container = new CuiElementContainer();

            playerSettings.ActiveSaveName = playerSettings.LoadedItems;
            CreatePanel(ref container, "0 0", "1 1", _uiColors.UISavePanelBlurColor, "SCMainOverlay", "SCOutfitEditPanel", true);
            var panel = CreatePanel(ref container, ".25 .3", ".75 .7", _uiColors.UISavePanelColor, "SCOutfitEditPanel", "SCOutfitEditPanelOverlay");

            CreateLabel(ref container, ".01 .85", ".988 .98", _uiColors.UISaveTitleColor, "1 1 1 1", Lang("OutfitEditor", player.UserIDString), 23, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".02 .73", ".98 .83", _uiColors.UISaveSecondaryColor, "1 1 1 1", Lang("OutfitName", player.UserIDString), 14, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".02 .46", ".98 .71", "sc_outfit outfitname", _uiColors.UISaveSecondaryColor, "1 1 1 1", string.IsNullOrEmpty(playerSettings.ActiveSaveName) ? "SET NAME" : playerSettings.ActiveSaveName, 18, TextAnchor.MiddleCenter, panel, "SCOutfitName");
            CreateLabel(ref container, ".02 .335", ".98 .44", _uiColors.UISaveSecondaryColor, "1 1 1 1", $"{playerSettings.SavedItems.Count} SKINS", 14, TextAnchor.MiddleCenter, panel);

            if (!string.IsNullOrEmpty(playerSettings.LoadedItems)) CreateButton(ref container, ".505 .17", ".976 .313", _uiColors.UISaveOverrideColor, "1 1 1 1", Lang("OverrideOutfit", player.UserIDString, playerSettings.LoadedItems), 20, "sc_outfit override", panel);
            else CreateLabel(ref container, ".505 .17", ".98 .315", _uiColors.UISaveOverrideColor, "1 1 1 1", "- - -", 20, TextAnchor.MiddleCenter, panel);

            CreateButton(ref container, ".02 .17", ".492 .313", _uiColors.UISaveColor, "1 1 1 1", Lang("SaveNewOutfit", player.UserIDString), 20, "sc_outfit savenew", panel, panelName: "SCSaveButton");

            CreateButton(ref container, ".01 .02", ".985 .15", _uiColors.UISaveCloseColor, "1 1 1 1", Lang("Close", player.UserIDString), 20, "sc_outfit editclose", panel);

            CuiHelper.DestroyUi(player, "SCOutfitEditPanel");
            CuiHelper.AddUi(player, container);
        }

        void UIAlterOutfitName(BasePlayer player, ref PlayerSettings playerSettings)
        {
            var container = new CuiElementContainer();

            CreateInput(ref container, ".02 .46", ".98 .71", "sc_outfit outfitname", _uiColors.UISaveSecondaryColor, "1 1 1 1", string.IsNullOrEmpty(playerSettings.ActiveSaveName) ? "SET NAME" : playerSettings.ActiveSaveName, 18, TextAnchor.MiddleCenter, "SCOutfitEditPanelOverlay", "SCOutfitName");

            CuiHelper.DestroyUi(player, "SCOutfitName");
            CuiHelper.AddUi(player, container);
        }

        void UIAlterSaveButton(BasePlayer player, PlayerSettings playerSettings, string text, string color, bool finish = false)
        {
            var container = new CuiElementContainer();

            CreateButton(ref container, ".02 .17", ".492 .313", color, "1 1 1 1", text, 20, "sc_outfit savenew", "SCOutfitEditPanelOverlay", panelName: "SCSaveButton");
            if (!finish) timer.In(2, () => UIAlterSaveButton(player, playerSettings, Lang("SaveNewOutfit", player.UserIDString), _uiColors.UISaveColor, true));

            CuiHelper.DestroyUi(player, "SCSaveButton");
            CuiHelper.AddUi(player, container);
        }

        void UIWriteMainActiveLoadout(BasePlayer player, PlayerSettings playerSettings)
        {
            var container = new CuiElementContainer();

            var panel = CreatePanel(ref container, "0 .68", "1 .89", _uiColors.UIPrimaryColor, "SCMainOverlay", "SCActiveLoadoutPanel");
            var label = CreateLabel(ref container, "0 .8", "1 .995", "0 0 0 .5", "1 1 1 1", $" {(Lang("OutfitText", player.UserIDString, !string.IsNullOrEmpty(playerSettings.LoadedItems) ? playerSettings.LoadedItems : Lang("NoOutfit")))}", 16, TextAnchor.MiddleLeft, panel);
            var buttonLength = .095;
            var items = playerSettings.SavedItems;

            CreateButton(ref container, ".5075 .12", ".5995 .8", _uiColors.UISafeButtonColor, "1 1 1 1", Lang("NewSet", player.UserIDString), 12, "sc_outfit new", label);
            CreateButton(ref container, ".605 .12", ".697 .8", _uiColors.UIDangerButtonColor, "1 1 1 1", Lang("Delete", player.UserIDString), 12, "sc_outfit delete", label);
            CreateButton(ref container, ".7025 .12", ".795 .8", _uiColors.UIDangerButtonColor, "1 1 1 1", Lang("Clear", player.UserIDString), 12, "sc_outfit clear", label);
            CreateButton(ref container, ".8 .12", ".8925 .8", _uiColors.UISecondaryButtonColor, "1 1 1 1", Lang("Edit", player.UserIDString), 12, "sc_outfit close", label);
            CreateButton(ref container, ".8975 .12", ".995 .8", _uiColors.UIButtonColor, "1 1 1 1", Lang("Save", player.UserIDString), 12, "sc_outfit save", label);

            AddScrollView(ref container, ".005 .05", ".99 .75", $"{(playerSettings.SavedItems.Count < 11 ? 1 : (0 + (buttonLength * items.Count)))}", "0 0 0 0", panel, "SCLoadoutScrollPanel", true);

            int i = 0;
            foreach (var item in items)
            {
                UIAddLoadoutItem(ref container, player, "SCLoadoutScrollPanel", items.Count, i, item, playerSettings, buttonLength, false);
                i++;
            }

            CuiHelper.DestroyUi(player, "SCActiveLoadoutPanel");
            CuiHelper.AddUi(player, container);
        }

        void UIWriteAlterFavoriteButton(BasePlayer player, ref PlayerSettings playerSettings, int i, Outfits outfit)
        {
            var container = new CuiElementContainer();

            CreateButton(ref container, ".8 .2", ".985 .78", outfit.Favorite ? _uiColors.UISecondaryButtonColor : _uiColors.UIButtonColor, "1 1 1 1", outfit.Favorite ? "FAVORITED" : "FAVORITE", 13, $"sc_main favorite {i}", $"loadout_title_{i}", panelName: $"loadout_favorite_{i}");

            CuiHelper.DestroyUi(player, $"loadout_favorite_{i}");
            CuiHelper.AddUi(player, container);
        }

        void UIWriteAllLoadouts(BasePlayer player, PlayerSettings playerSettings)
        {
            var container = new CuiElementContainer();

            var panel = CreatePanel(ref container, "0 0", "1 .67", _uiColors.UIPrimaryColor, "SCMainOverlay", "SCAllLoadoutPanel");
            var outfits = playerSettings.Outfits;

            CreateButton(ref container, ".005 .01", ".2 .055", _uiColors.UIButtonColor, "1 1 1 1", Lang("Close", player.UserIDString), 12, "sc_outfit close", panel);

            var maxPage = (outfits.Count - 1) / 6;
            if (maxPage < playerSettings.LoadoutPage) playerSettings.LoadoutPage = 0;
            if (playerSettings.LoadoutPage < 0) playerSettings.LoadoutPage = maxPage;

            if (maxPage > 1) CreateButton(ref container, ".35 .01", ".425 .055", _uiColors.UIButtonColor, "1 1 1 1", "<", 12, $"sc_outfit outfitpage {playerSettings.LoadoutPage - 1}", panel);
            CreateLabel(ref container, ".43 .01", ".575 .056", _uiColors.UIButtonColor, "1 1 1 1", $"{playerSettings.LoadoutPage + 1} / {maxPage + 1}", 12, TextAnchor.MiddleCenter, panel);
            if (maxPage > 1) CreateButton(ref container, ".58 .01", ".65 .055", _uiColors.UIButtonColor, "1 1 1 1", ">", 12, $"sc_outfit outfitpage {playerSettings.LoadoutPage + 1}", panel);

            int i = 0;
            int indx = 0;
            int row = 0;
            foreach (var outfit in outfits.Skip(playerSettings.LoadoutPage * 6).Take(6))
            {
                UIAddLoadout(ref container, player, playerSettings, outfit, "SCAllLoadoutPanel", i, row, indx);

                if (i > 0)
                {
                    row++;
                    i = 0;
                }
                else i++;

                indx++;
            }

            CuiHelper.DestroyUi(player, "SCAllLoadoutPanel");
            CuiHelper.AddUi(player, container);
        }

        void UIAddLoadout(ref CuiElementContainer container, BasePlayer player, PlayerSettings playerSettings, Outfits outfit, string panel, int i, int row, int indx)
        {
            int arrayInt = playerSettings.LoadoutPage * 6 + indx;

            var loadoutPanel = CreatePanel(ref container, $"{.005 + (i * .4975)} {.685 - (row * .31)}", $"{.4975 + (i % 8 * .4975)} {.985 - (row * .31)}", _uiColors.UISecondaryColor, panel);
            var titlePanel = CreateLabel(ref container, "0 .8", "1 .995", _uiColors.UISecondaryColor, "1 1 1 1", $" {outfit.OutfitName}", 15, TextAnchor.MiddleLeft, loadoutPanel, $"loadout_title_{arrayInt}");
            CreateButton(ref container, ".8 .2", ".985 .78", outfit.Favorite ? _uiColors.UISecondaryButtonColor : _uiColors.UIButtonColor, "1 1 1 1", outfit.Favorite ? "FAVORITED" : "FAVORITE", 13, $"sc_main favorite {arrayInt}", titlePanel, panelName: $"loadout_favorite_{arrayInt}");

            int itemNumber = 0;
            foreach (var item in outfit.OutfitDetails)
            {
                double yMax = itemNumber % 2 == 0 ? .5 : .7;
                double xMin = .01 + (itemNumber * .08);

                if (itemNumber >= 11)
                {
                    CreateLabel(ref container, $"{xMin} {yMax - .39}", $"{xMin + .1} {yMax}", "0 0 0 0", "1 1 1 1", $"+{outfit.OutfitDetails.Count - 11}", 15, TextAnchor.MiddleCenter, loadoutPanel);
                    break;
                }

                CreateMicroItem(ref container, $"{xMin} {yMax - .39}", $"{xMin + .1} {yMax}", item.ItemId, item.SkinId, loadoutPanel);

                itemNumber++;
            }

            CreateButton(ref container, "0 0", "1 .8", "0 0 0 0", "1 1 1 1", " ", 15, $"sc_main selectoutfit {outfit.OutfitName}", loadoutPanel);

            return;
        }
        #endregion

        #region Settings UI
        void UIWriteSettingsUI(BasePlayer player, ref PlayerSettings playerSettings)
        {
            var container = new CuiElementContainer();

            CreatePanel(ref container, "0 0", "1 1", _uiColors.UISettingsPanelBlurColor, "SCMainOverlay", "SCSettingsPanel", true);
            CreatePanel(ref container, "0 0", "1 1", _uiColors.UISettingsPanelOverlayColor, "SCSettingsPanel");
            var panel = CreatePanel(ref container, ".2 .1", ".8 .9", _uiColors.UISettingsPanelColor, "SCSettingsPanel", "SCSettingsPanelOverlay");
            var label = CreateLabel(ref container, ".01 .93", ".99 .99", _uiColors.UISecondarySettingsPanelColor, "1 1 1 1", Lang("Settings", player.UserIDString), 18, TextAnchor.MiddleCenter, panel);

            CreateButton(ref container, ".94 0", "1 1", "0 0 0 0", "1 1 1 .8", "X", 18, "sc_main closesettings", label);

            int i = 0;
            foreach (var setting in playerSettings.PlayerOptionalSettings)
            {
                var settingsPanel = CreatePanel(ref container, $".03 {.78 - (i * .13)}", $".97 {.9 - (i * .13)}", _uiColors.UISecondarySettingsPanelColor, panel, $"setting_{i}_panel");
                CreateButton(ref container, "0 0", ".2 1", "0 0 0 0", setting.Enabled ? ".13 .75 .21 1" : ".75 .13 .13 1", "ENABLED", 18, $"sc_main setting {i}", settingsPanel, panelName: $"setting_{i}");
                CreateLabel(ref container, ".2 0", "1 1", "0 0 0 0", "1 1 1 1", Lang(setting.SettingName, player.UserIDString), 20, TextAnchor.MiddleLeft, settingsPanel);
                i++;
            }

            CuiHelper.AddUi(player, container);
        }

        void AlterSetting(BasePlayer player, ref PlayerSettings playerSettings, int i)
        {
            var container = new CuiElementContainer();

            var setting = playerSettings.PlayerOptionalSettings[i];
            CreateButton(ref container, "0 0", ".2 1", "0 0 0 0", setting.Enabled ? ".13 .75 .21 1" : ".75 .13 .13 1", setting.Enabled ? "ENABLED" : "DISABLED", 18, $"sc_main setting {i}", $"setting_{i}_panel", panelName: $"setting_{i}");

            CuiHelper.DestroyUi(player, $"setting_{i}");
            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region Main UI
        void UIMain(BasePlayer player, string command, string[] args) => UIInit(player, 2, true);
        void UIOutfit(BasePlayer player, string command, string[] args)
        {
            if (args.Length > 0) ChatApplyOutfit(player, args);
            else UIInit(player, 0, true);
        }

        void UIInit(BasePlayer player, int panel, bool fromCommand = false)
        {
            if (!permission.UserHasPermission(player.UserIDString, "skincontroller.use"))
            {
                SendReply(player, "You do not have permission to use this command.");
                return;
            }

            if (!SkinsGenerated)
            {
                SendReply(player, "Skins not generated");
                return;
            }

            if (fromCommand && !CheckCooldown(player.userID))
            {
                SendReply(player, "Cooldown");
                return;
            }

            var container = new CuiElementContainer();

            CreatePanel(ref container, "0 0", "1 1", usingWC ? "0 0 0 0" : _uiColors.UIBackgroundColor, usingWC ? "WCSourcePanel" : "Overlay", "SCMainPanel", true, true);
            CreatePanel(ref container, usingWC ? "0 0" : ".15 .1", usingWC ? ".995 1" : ".85 .9", "0 0 0 0", "SCMainPanel", "SCMainOverlay");

            CuiHelper.DestroyUi(player, "SCMainPanel");
            CuiHelper.AddUi(player, container);

            var playerSettings = GetOrLoadPlayerSettings(player);

            playerSettings.SkinPage = 0;

            if (panel == 0)
            {
                UIWriteTopPanel(player);
                UIWriteCategoryPanel(player, playerSettings);
                UIWriteItemPanel(player, playerSettings);
                UIWriteLoadoutPanel(player, playerSettings);
                UIWriteSkinsPanel(player, playerSettings);
            }
            else if (panel == 1)
            {
                UIWriteTopPanel(player);
                UIWriteMainActiveLoadout(player, playerSettings);
                UIWriteAllLoadouts(player, playerSettings);
            }
            else if (panel == 2)
            {
                playerSettings.IndSettings = new IndSettings();
                foreach (var item in player.inventory.containerMain.itemList) playerSettings.IndSettings.InventoryItems.Add(new InvItem { Category = 0, ItemId = item.info.itemid, Position = item.position, SkinId = item.skin });
                foreach (var item in player.inventory.containerBelt.itemList) playerSettings.IndSettings.InventoryItems.Add(new InvItem { Category = 1, ItemId = item.info.itemid, Position = item.position, SkinId = item.skin });
                foreach (var item in player.inventory.containerWear.itemList) playerSettings.IndSettings.InventoryItems.Add(new InvItem { Category = 2, ItemId = item.info.itemid, Position = item.position, SkinId = item.skin });


                UIWriteTopPanel(player);
                UIWriteInventoryPanel(player, playerSettings);
                UIWriteIndSkinPanel(player, playerSettings);
            }
        }

        void UIWriteTopPanel(BasePlayer player)
        {
            var container = new CuiElementContainer();

            var panel = CreatePanel(ref container, "0 .9", "1 1", _uiColors.UIPrimaryColor, "SCMainOverlay", "SCTopPanel");

            if (!string.IsNullOrEmpty(_config.UIImage)) CreateImagePanel(ref container, ".2 0", ".8 .99", GetImage("UIImage"), panel);
            else CreateLabel(ref container, ".2 0", ".8 .99", "0 0 0 0", "1 1 1 1", _config.UITitle, 45, TextAnchor.MiddleCenter, panel);

            CreateImageButton(ref container, ".005 .45", ".039 .92", "0 0 0 0", "sc_main settings", GetImage("SettingsIcon"), panel);

            CuiHelper.DestroyUi(player, "SCTopPanel");
            CuiHelper.AddUi(player, container);
        }

        void UIWriteSelectPanel(BasePlayer player, SkinData skinInfo)
        {
            bool admin = permission.UserHasPermission(player.UserIDString, "skincontroller.admin");
            var container = new CuiElementContainer();

            CreatePanel(ref container, "0 0", "1 1", _uiColors.UISelectPanelBlurColor, "SCMainOverlay", "SCSelectPanel", true);
            var panel = CreatePanel(ref container, ".2 .35", ".8 .65", _uiColors.UISelectPanelColor, "SCSelectPanel", "SCSelectPanelOverlay");
            CreateSkinPanel(ref container, ".01 .03", ".25 .79", .05f, _uiColors.UISecondarySelectPanelColor, skinInfo.ItemID, panel, skinId: skinInfo.SkinID);
            var label = CreateLabel(ref container, admin ? ".2 .82" : ".01 .82", ".99 .97", _uiColors.UISecondarySelectPanelColor, "1 1 1 1", skinInfo.SkinName, 18, TextAnchor.MiddleCenter, panel);

            if (admin) CreateButton(ref container, ".01 .82", ".189 .968", _uiColors.UIApplySelectColor, "1 1 1 1", "BLACKLIST", 20, $"sc_main blacklist {skinInfo.ItemID} {skinInfo.SkinID}", panel);

            CreateButton(ref container, ".26 .425", ".58 .785", _uiColors.UISkinPanelColor, "1 1 1 1", "APPLY", 20, $"sc_main skinitem {skinInfo.ItemID} {skinInfo.SkinID}", panel);
            CreateButton(ref container, ".59 .425", ".985 .785", _uiColors.UIFindSelectColor, "1 1 1 1", "FIND MATCHING SKINS", 20, $"sc_main findskins {skinInfo.SkinName}", panel);
            CreateButton(ref container, ".26 .03", ".988 .39", _uiColors.UIApplySelectColor, "1 1 1 1", "ADD TO OUTFIT", 20, $"sc_main setskin {skinInfo.ItemID} {skinInfo.SkinID}", panel);

            CreateButton(ref container, ".94 0", "1 1", "0 0 0 0", "1 1 1 .8", "X", 18, "sc_main closeselect", label);

            CuiHelper.AddUi(player, container);
        }

        #region Loadout Panel
        void UIWriteLoadoutPanel(BasePlayer player, PlayerSettings playerSettings)
        {
            var container = new CuiElementContainer();

            var panel = CreatePanel(ref container, "0 .69", "1 .89", _uiColors.UIPrimaryColor, "SCMainOverlay", "SCLoadoutPanel");
            var items = playerSettings.SavedItems;
            var buttonLength = .092;

            AddScrollView(ref container, ".005 .05", ".99 .75", $"{(playerSettings.SavedItems.Count < 11 ? 1 : (0 + (buttonLength * playerSettings.SavedItems.Count)))}", "0 0 0 0", panel, "SCLoadoutScrollPanel", true);
            var label = CreateLabel(ref container, "0 .81", "1 .995", "0 0 0 .5", "1 1 1 1", $" {(Lang("OutfitText", player.UserIDString, !string.IsNullOrEmpty(playerSettings.LoadedItems) ? playerSettings.LoadedItems : Lang("NoOutfit")))}", 16, TextAnchor.MiddleLeft, panel);
            CreateButton(ref container, ".87 .13", ".995 .78", _uiColors.UIButtonColor, "1 1 1 1", Lang("ManageOutfits"), 13, "sc_main outfits", label);
            CreateButton(ref container, ".735 .13", ".865 .78", _uiColors.UISecondaryButtonColor, "1 1 1 1", Lang("ApplyToInv"), 13, "sc_main skininventory", label);
            CreateButton(ref container, ".6 .13", ".73 .78", _uiColors.UISecondaryButtonColor, "1 1 1 1", "INDIVIDUAL PAGE", 13, "sc_main indpage", label);
            if (_config.AllowSkinTeam && player.Team != null && player.userID == player.Team.teamLeader) CreateButton(ref container, ".465 .13", ".595 .78", _uiColors.UISecondaryButtonColor, "1 1 1 1", "SKIN TEAM", 13, "sc_main skinteam", label);

            int i = 0;
            foreach (var item in items)
            {
                UIAddLoadoutItem(ref container, player, "SCLoadoutScrollPanel", items.Count, i, item, playerSettings, buttonLength);
                i++;
            }

            CuiHelper.DestroyUi(player, "SCLoadoutPanel");
            CuiHelper.AddUi(player, container);
        }

        void UIAlterLoadoutItem(BasePlayer player, ref PlayerSettings playerSettings, string[] args)
        {
            var container = new CuiElementContainer();

            int i = int.Parse(args[1]);

            var buttonLength = .092;
            var count = playerSettings.SavedItems.Count;
            var panelDepth = buttonLength * count;
            var space = count < 11 ? .005 : .005 / panelDepth;
            var rowDepth = count < 11 ? buttonLength : buttonLength / panelDepth;

            var topHeight = rowDepth + ((i - 1) * rowDepth);

            CreateSelectLoadoutItem(ref container, $"{topHeight} .07", $"{topHeight + rowDepth - space} 1", "1 1 1 .15", "SCLoadoutScrollPanel", $"loadout_{i}", i: i);

            CuiHelper.DestroyUi(player, $"loadout_{i}");
            CuiHelper.AddUi(player, container);
        }

        void UIAlterLoadoutItemSelect(BasePlayer player, ref PlayerSettings playerSettings, OutfitDetail skinInfo, int i)
        {
            var container = new CuiElementContainer();

            var buttonLength = .092;
            var count = playerSettings.SavedItems.Count;
            var panelDepth = buttonLength * count;
            var space = count < 11 ? .005 : .005 / panelDepth;
            var rowDepth = count < 11 ? buttonLength : buttonLength / panelDepth;

            var topHeight = rowDepth + ((i - 1) * rowDepth);

            CreateSkinPanel(ref container, $"{topHeight} .07", $"{topHeight + rowDepth - space} 1", .1f, "1 1 1 .15", skinInfo.ItemId, "SCLoadoutScrollPanel", $"loadout_{i}", skinInfo.SkinId, useCommand: true, command: $"sc_main alterloadoutitem {i}");

            CuiHelper.DestroyUi(player, $"loadout_{i}");
            CuiHelper.AddUi(player, container);
        }

        void UIAddLoadoutItem(ref CuiElementContainer container, BasePlayer player, string panel, double panelCount, int i, OutfitDetail skinInfo, PlayerSettings playerSettings, double buttonLength, bool createButtons = true)
        {
            var panelDepth = buttonLength * playerSettings.SavedItems.Count;
            var space = panelCount < 11 ? .005 : .005 / panelDepth;
            var rowDepth = playerSettings.SavedItems.Count < 11 ? buttonLength : buttonLength / panelDepth;

            var topHeight = rowDepth + ((i - 1) * rowDepth);

            CreateSkinPanel(ref container, $"{topHeight} .07", $"{topHeight + rowDepth - space} 1", .1f, "1 1 1 .15", skinInfo.ItemId, panel, $"loadout_{i}", skinInfo.SkinId, useCommand: createButtons, command: $"sc_main alterloadoutitem {i}");

            return;
        }
        #endregion

        #region Category Panel
        void UIWriteCategoryPanel(BasePlayer player, PlayerSettings playerSettings)
        {
            var container = new CuiElementContainer();

            var panel = CreatePanel(ref container, ".077 .61", "1 .68", _uiColors.UIPrimaryColor, "SCMainOverlay", "SCCategoryPanel");
            int i = 0;
            foreach (var category in _uiCategories)
            {
                CreateButton(ref container, $"{.008 + (i * .198)} .15", $"{.198 + (i * .198)} .82", !string.IsNullOrEmpty(playerSettings.SkinsFilter) || playerSettings.Category == category ? _uiColors.UIActiveCategoryColor : _uiColors.UIButtonColor, "1 1 1 1", Lang(category, player.UserIDString), 17, $"sc_main category {category}", panel);
                i++;
            }

            CuiHelper.DestroyUi(player, "SCCategoryPanel");
            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region Item Panel
        void UIWriteItemPanel(BasePlayer player, PlayerSettings playerSettings)
        {
            var container = new CuiElementContainer();

            var panel = CreatePanel(ref container, "0 0", ".07 .68", _uiColors.UIPrimaryColor, "SCMainOverlay", "SCItemPanel");
            var items = _categoryList[playerSettings.Category];

            var maxPage = items.Count() / 6;
            if (maxPage < playerSettings.ItemPage) playerSettings.ItemPage = 0;
            if (playerSettings.ItemPage < 0) playerSettings.ItemPage = maxPage;

            CreateButton(ref container, ".1 .93", ".88 .978", _uiColors.UIItemButtonColor, "1 1 1 1", "LAST", 10, $"sc_main itempage {playerSettings.ItemPage - 1}", panel);
            CreateButton(ref container, ".1 .02", ".88 .07", _uiColors.UIItemButtonColor, "1 1 1 1", "NEXT", 10, $"sc_main itempage {playerSettings.ItemPage + 1}", panel);

            int i = 0;
            bool isFilter = !string.IsNullOrEmpty(playerSettings.SkinsFilter);
            foreach (var item in items.Skip(6 * playerSettings.ItemPage).Take(6))
            {
                UIAddItem(ref container, panel, i, item, ref playerSettings, isFilter);
                i++;
            }

            CuiHelper.DestroyUi(player, "SCItemPanel");
            CuiHelper.AddUi(player, container);

        }

        void UIAddItem(ref CuiElementContainer container, string panel, int i, int itemId, ref PlayerSettings playerSettings, bool isFilter)
        {
            var yMin = $"{.79 - (i * .141)}";
            var yMax = $"{.92 - (i * .141)}";

            bool activeButton = playerSettings.CategoryItem == itemId ? true : false;
            if (activeButton)
            {
                playerSettings.LastItemPage = playerSettings.ItemPage;
                playerSettings.ItemUIPostition = new UIPosition()
                {
                    yMin = yMin,
                    yMax = yMax
                };
            }

            if (!activeButton && isFilter) activeButton = true;

            CreateItemButton(ref container, $".1 {yMin}", $".88 {yMax}", .1f, activeButton ? _uiColors.UIActiveButtonColor : _uiColors.UIItemButtonColor, itemId, panel, $"{itemId}", command: $"sc_main item {itemId} {yMin} {yMax}");
            return;
        }

        void AlterAddItem(BasePlayer player, string panelName, string yMin, string yMax, PlayerSettings playerSettings, bool isOld = false)
        {
            int itemId = int.Parse(panelName);

            bool activeButton = playerSettings.CategoryItem == itemId ? true : false;
            if (activeButton)
            {
                playerSettings.LastItemPage = playerSettings.ItemPage;
                playerSettings.ItemUIPostition = new UIPosition()
                {
                    yMin = $"{yMin}",
                    yMax = $"{yMax}"
                };
            }

            if (!activeButton && playerSettings.LastItemPage != playerSettings.ItemPage) return;

            var container = new CuiElementContainer();
            CreateItemButton(ref container, $".1 {yMin}", $".88 {yMax}", .1f, activeButton ? _uiColors.UIActiveButtonColor : _uiColors.UIItemButtonColor, itemId, "SCItemPanel", panelName, command: $"sc_main item {panelName} {yMin} {yMax}");

            CuiHelper.DestroyUi(player, panelName);
            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region Skins Panel
        void UIWriteSkinsPanel(BasePlayer player, PlayerSettings playerSettings)
        {
            var container = new CuiElementContainer();
            List<SkinData> skins;

            if (!string.IsNullOrEmpty(playerSettings.SkinsFilter)) skins = _skinList.SelectMany(item => item.Value).Where(skin => skin.SkinName.Contains(playerSettings.SkinsFilter, StringComparison.OrdinalIgnoreCase)).ToList();
            else skins = _skinList[playerSettings.CategoryItem];

            skins = skins.Where(x => string.IsNullOrEmpty(x.SkinPermission) || permission.UserHasPermission(player.UserIDString, x.SkinPermission)).ToList();

            var maxPage = (skins.Count - 1) / 24;
            if (maxPage < playerSettings.SkinPage) playerSettings.SkinPage = 0;
            if (playerSettings.SkinPage < 0) playerSettings.SkinPage = maxPage;

            var panel = CreatePanel(ref container, ".077 0", "1 .6", _uiColors.UIPrimaryColor, "SCMainOverlay", "SCSkinPanel");

            CreateButton(ref container, ".007 .016", ".1 .086", _uiColors.UISearchPanelColor, "1 1 1 1", "<", 15, $"sc_main skinspage {playerSettings.SkinPage - 1}", panel);
            CreateLabel(ref container, ".105 .016", ".23 .087", _uiColors.UISearchPanelColor, "1 1 1 1", $"{playerSettings.SkinPage + 1} / {maxPage + 1}", 15, TextAnchor.MiddleCenter, panel);
            CreateButton(ref container, ".235 .016", ".33 .086", _uiColors.UISearchPanelColor, "1 1 1 1", ">", 15, $"sc_main skinspage {playerSettings.SkinPage + 1}", panel);

            CreateButton(ref container, ".336 .016", ".37 .086", _uiColors.UISearchPanelColor, "1 1 1 1", "X", 15, "sc_main clearfilter", panel);

            CreateInput(ref container, ".375 .016", usingWC ? ".992 .087" : ".75 .086", "sc_main skinsfilter", _uiColors.UISearchPanelColor, "1 1 1 .6", string.IsNullOrEmpty(playerSettings.SkinsFilter) ? Lang("Search", player.UserIDString) : playerSettings.SkinsFilter, 15, TextAnchor.MiddleCenter, panel);
            if (!usingWC) CreateButton(ref container, ".755 .016", ".993 .086", _uiColors.UIButtonColor, "1 1 1 1", Lang("Close", player.UserIDString), 15, "sc_main close", panel);

            int i = 0;
            int row = 0;
            int SkinCount = skins.Count;
            foreach (var skin in skins.Skip(playerSettings.SkinPage * 24).Take(24))
            {
                var xMin = $"{.007 + (i % 8 * .124)}";
                var yMin = $"{.696 - (row * .298)}";
                var xMax = $"{.126 + (i % 8 * .124)}";
                var yMax = $"{.985 - (row * .298)}";

                int itemId = skin.Redirect.IsRedirect ? skin.Redirect.ItemId : skin.ItemID;
                ulong skinId = skin.Redirect.IsRedirect ? 0 : skin.SkinID;
                if (_config.UIPerformanceMode) CreateSkinPanel(ref container, $"{xMin} {yMin}", $"{xMax} {yMax}", .1f, _uiColors.UISkinPanelColor, itemId, panel, null, skinId, i * .1f, true, $"sc_main skinextra {skin.ItemID} {skin.SkinID}");
                else
                {
                    var skinPanel = CreateItemPanel(ref container, $"{xMin} {yMin}", $"{xMax} {yMax}", .1f, _uiColors.UISkinPanelColor, skin.ItemID, panel, null, skin.SkinID, i * .1f);
                    CreateLabel(ref container, "0 .8", "1 1", "1 1 1 .15", "1 1 1 1", skin.SkinName, 8, TextAnchor.MiddleCenter, skinPanel);
                    CreateButton(ref container, ".025 .61", ".3 .76", "1 1 1 .15", "1 1 1 1", "++", 9, $"sc_main findskins {skin.SkinName}", skinPanel);

                    CreateButton(ref container, ".025 .025", ".475 .15", "1 1 1 .15", "1 1 1 1", Lang("SkinButton"), 10, $"sc_main skinitem {skin.ItemID} {skin.SkinID}", skinPanel);
                    CreateButton(ref container, ".525 .025", ".975 .15", "1 1 1 .15", "1 1 1 1", Lang("SetButton"), 10, $"sc_main setskin {skin.ItemID} {skin.SkinID}", skinPanel);
                }
                i++;
                if (i % 8 == 0) row++;
            }

            CuiHelper.DestroyUi(player, "SCSkinPanel");
            CuiHelper.AddUi(player, container);
        }

        #endregion
        #endregion

        #region UI Methods
        private static string CreateSkinPanel(ref CuiElementContainer container, string anchorMin, string anchorMax, float padding, string color, int itemId, string parent = "Overlay",
        string panelName = null, ulong skinId = 0L, float fadeIn = 0f, bool useCommand = false, string command = "")
        {
            var panel = CreatePanel(ref container, anchorMin, anchorMax, color, parent, panelName, FadeIn: fadeIn);

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
                },
            });

            if (useCommand) container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = $"{command}" }
            }, panel);

            return panelName;
        }

        static public void AddScrollView(ref CuiElementContainer container, string anchorMin, string anchorMax, string offSetMin, string color, string parent = "Overlay", string panelName = null, bool horizontal = false)
        {
            container.Add(new CuiElement
            {
                Name = panelName,
                Parent = parent,
                Components = {
                        new CuiImageComponent {
                            FadeIn = 0.2f,
                            Color = color
                        },
                        new CuiScrollViewComponent {
                            Horizontal = horizontal,
                            Vertical = horizontal == false,
                            MovementType = UnityEngine.UI.ScrollRect.MovementType.Elastic,
                            Elasticity = 0.25f,
                            Inertia = true,
                            DecelerationRate = 0.3f,
                            ContentTransform = new CuiRectTransform()
                            {
                                AnchorMin = $"0 {(horizontal ? "0" : offSetMin)}",
                                AnchorMax = $"{(horizontal ? offSetMin : "1")} 1",
                            },
                            ScrollSensitivity = 30.0f,
                            VerticalScrollbar = new CuiScrollbar {
                                Invert = false,
                                AutoHide = false,
                                HandleSprite = "assets/content/ui/ui.rounded.tga",
                                HandleColor = _uiColors.UIScrollBarColor,
                                HighlightColor = "0.17 0.17 0.17 .5",
                                TrackSprite = "assets/content/ui/ui.background.tile.psd",
                                TrackColor = ".09 .09 .09 .2",
                                Size = 3,
                                PressedColor = ".17 .17 .17 .7"
                            },
                            HorizontalScrollbar = new CuiScrollbar {
                                Invert = true,
                                AutoHide = false,
                                HandleSprite = "assets/content/ui/ui.rounded.tga",
                                HandleColor = _uiColors.UIScrollBarColor,
                                HighlightColor = "0.17 0.17 0.17 .5",
                                TrackSprite = "assets/content/ui/ui.background.tile.psd",
                                TrackColor = ".09 .09 .09 .2",
                                Size = 3,
                                PressedColor = ".17 .17 .17 .7"
                            }
                        },
                        new CuiRectTransformComponent {AnchorMin = anchorMin, AnchorMax = anchorMax}
                    }
            });
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
                        Text = labelText == null ? " " : labelText,
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

        private static string CreateLabel(ref CuiElementContainer container, string anchorMin, string anchorMax, string backgroundColor, string textColor,
        string labelText, int fontSize, TextAnchor alignment, string parent = "Overlay",
        string labelName = null)
        {
            var panel = container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = anchorMin, AnchorMax = anchorMax                },
                Image = { Color = backgroundColor }
            }, parent, labelName);
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

        private static string CreateItemButton(ref CuiElementContainer container, string anchorMin, string anchorMax, float padding, string color, int itemId, string parent = "Overlay",
        string panelName = null, ulong skinId = 0L, float fadeIn = 0f, string command = "")
        {
            var panel = CreatePanel(ref container, anchorMin, anchorMax, color, parent, panelName, FadeIn: fadeIn);

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
                },
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = $"{command}" }
            }, panel);

            return panel;
        }

        private static string CreateItemPanel(ref CuiElementContainer container, string anchorMin, string anchorMax, float padding, string color, int itemId, string parent = "Overlay",
        string panelName = null, ulong skinId = 0L, float fadeIn = 0f)
        {
            var panel = CreatePanel(ref container, anchorMin, anchorMax, color, parent, panelName, FadeIn: fadeIn);

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
                },
            });

            return panel;
        }

        private static void CreateMicroItem(ref CuiElementContainer container, string anchorMin, string anchorMax, int itemId, ulong skinId, string parent = "Overlay")
        {
            container.Add(new CuiElement
            {
                Parent = parent,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax
                    },
                    new CuiImageComponent {ItemId = itemId, SkinId = skinId }
                },
            });

            return;
        }

        private static string CreateSelectLoadoutItem(ref CuiElementContainer container, string anchorMin, string anchorMax, string panelColor, string parent = "Overlay", string panelName = null, bool blur = false, bool isMainPanel = false, float FadeIn = 0f, int i = 0)
        {
            var panel = container.Add(new CuiPanel
            {
                RectTransform =
            {
                AnchorMin = anchorMin,
                AnchorMax = anchorMax
            },
                Image = { Color = panelColor, FadeIn = FadeIn }
            }, parent, panelName);

            container.Add(new CuiButton
            {
                Button = { Color = _uiColors.UIButtonColor, Command = $"sc_main deleteloadoutitem {i}" },
                Text = { Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FontSize = 15, Text = "DELETE" },
                RectTransform = { AnchorMin = ".04 .65", AnchorMax = ".94 .95" }
            }, panel);

            container.Add(new CuiButton
            {
                Button = { Color = _uiColors.UIButtonColor, Command = $"sc_main editloadoutitem {i}" },
                Text = { Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FontSize = 15, Text = "EDIT" },
                RectTransform = { AnchorMin = ".04 .3", AnchorMax = ".94 .6" }
            }, panel);

            container.Add(new CuiButton
            {
                Button = { Color = _uiColors.UIButtonColor, Command = $"sc_main closeloadoutitem {i}" },
                Text = { Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FontSize = 13, Text = "CLOSE" },
                RectTransform = { AnchorMin = ".04 .05", AnchorMax = ".94 .25" }
            }, panel);

            return panel;
        }

        private static string CreatePanel(ref CuiElementContainer container, string anchorMin, string anchorMax, string panelColor, string parent = "Overlay", string panelName = null, bool blur = false, bool isMainPanel = false, float FadeIn = 0f)
        {
            CuiPanel panel = new CuiPanel
            {
                RectTransform =
            {
                AnchorMin = anchorMin,
                AnchorMax = anchorMax
            },
                Image = { Color = panelColor, FadeIn = FadeIn }
            };

            if (blur) panel.Image.Material = "assets/content/ui/uibackgroundblur.mat";
            if (isMainPanel) panel.CursorEnabled = true;
            return container.Add(panel, parent, panelName);
        }

        private static void CreateImagePanel(ref CuiElementContainer container, string anchorMin, string anchorMax, string panelImage, string parent = "Overlay",
        string panelName = null)
        {
            container.Add(new CuiElement
            {
                Parent = parent,
                Name = panelName,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax

                    },
                    new CuiRawImageComponent {Png = panelImage},
                }
            });
        }

        private static void CreateImageButton(ref CuiElementContainer container, string anchorMin, string anchorMax, string buttonColor, string buttonCommand, string panelImage, string parent = "Overlay", string panelName = null)
        {
            var panel = CreatePanel(ref container, anchorMin, anchorMax, buttonColor, parent, panelName);

            container.Add(new CuiElement
            {
                Parent = panel,
                Components =
                {
                    new CuiRawImageComponent {Png = panelImage },
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = $"{buttonCommand}" }
            }, panel);
        }

        private static string CreateButton(ref CuiElementContainer container, string anchorMin, string anchorMax, string buttonColor, string textColor, string buttonText,
        int fontSize, string buttonCommand, string parent = "Overlay",
        TextAnchor labelAnchor = TextAnchor.MiddleCenter, string panelName = null)
        {
            var panel = container.Add(new CuiButton
            {
                Button = { Color = buttonColor, Command = $"{buttonCommand}" },
                Text = { Align = labelAnchor, Color = textColor, FontSize = fontSize, Text = buttonText },
                RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax }
            }, parent, panelName);
            return panel;
        }
        #endregion

    }
}
