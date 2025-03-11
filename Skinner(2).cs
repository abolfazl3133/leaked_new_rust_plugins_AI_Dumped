/* Copyright (C) Whispers88 - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential
 * Written by Whispers88 rustafarian.server@gmail.com, October 2024
 * Version 2.1.0
 *
 *You may not copy, modify, merge, publish, distribute, sublicense, or sell copies of This Software without the Developer’s consent

   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO,
   THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS
   BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE
   GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
   LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Pool;
using System.Text;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("Skinner", "Whispers88", "2.1.0")]
    [Description("Brings automation and ease to skinning items")]
    public class Skinner : CovalencePlugin
    {
        static Skinner skinner;

        #region Perms
        private const string permdefault = "skinner.default";
        private const string permitems = "skinner.items";
        private const string permcraft = "skinner.craft";
        private const string permskininv = "skinner.skininv";
        private const string permskinteam = "skinner.skinteam";
        private const string permskinteamblock = "skinner.skinteamblock";
        private const string permskincon = "skinner.skincon";
        private const string permbypassauth = "skinner.bypassauth";
        private const string permimport = "skinner.import";
        private const string permskinbase = "skinner.skinbase";
        private const string permskinall = "skinner.skinall";
        private const string permskinauto = "skinner.skinauto";
        private const string permskinautotoggle = "skinner.skinautotoggled";
        private const string permskinrequest = "skinner.skinrequest";
        private const string permskintry = "skinner.permskintry";

        private List<string> permissions = new List<string>() { permdefault, permcraft, permitems, permbypassauth, permskincon, permskininv, permimport, permskinbase, permskinall, permskinteam, permskinteamblock, permskinrequest, permskintry, permskinauto, permskinautotoggle };

        #endregion Perms

        #region Init
        private void OnServerInitialized()
        {
            skinner = this;
            _skinNames = new Dictionary<ulong, string>();
            _cachedSkins = new Dictionary<int, List<ulong>>();
            _WaitForSecondsMore = CoroutineEx.waitForSeconds(1f);
            _coffinPrefab = "assets/prefabs/misc/halloween/coffin/coffinstorage.prefab";
            Layermask = LayerMask.GetMask("Deployed", "Construction");
            RPCOpenLootstr = StringPool.Get("RPC_OpenLootPanel");
            RPCOpenLootbytes = Encoding.UTF8.GetBytes("generic_resizable");
            spraycanid = -596876839;

            foreach (string perm in permissions)
                permission.RegisterPermission(perm, this);

            foreach (string perm in config.Cooldowns.Keys)
                permission.RegisterPermission($"skinner.{perm}", this);

            AddCovalenceCommand(config.cmdsskin, "SkinCMD");
            AddCovalenceCommand(config.cmdsskincraft, "DefaultSkinsCMD");
            AddCovalenceCommand(config.cmdsskinitems, "SkinItemCMD");
            AddCovalenceCommand(config.cmdsskininv, "SkinInvCMD");
            AddCovalenceCommand(config.cmdsskincon, "SkinConCMD");
            AddCovalenceCommand(config.cmdtoggleautoskin, "SkinAutoCMD");
            AddCovalenceCommand(config.cmdskinimport, "SkinImportCMD");
            AddCovalenceCommand(config.cmdcollectionimport, "SkinImportCollection");
            AddCovalenceCommand(config.cmdskinbase, "SkinBaseCMD");
            AddCovalenceCommand(config.cmdskinallitems, "SkinAllItemsCMD");
            AddCovalenceCommand(config.cmdskinteam, "SkinTeamCMD");
            AddCovalenceCommand(config.cmdskinrequest, "SkinRequestCMD");
            List<string> tempList = new List<string>(config.cmdskinset);
            tempList.Add("setSelectCMD");
            AddCovalenceCommand(tempList.ToArray(), "SetSelectCMD");
            AddCovalenceCommand(config.cmdskinrequests, "SkinRequestsCMD");

            AddCovalenceCommand(new[] { "sbNextPage" }, "SBNextPageCMD");
            AddCovalenceCommand(new[] { "sbBackPage" }, "SBBackPageCMD");
            AddCovalenceCommand(new[] { "searchCMD" }, "SearchCMD");
            AddCovalenceCommand(new[] { "requestSelectCMD" }, "RequestSelectCMD");

            foreach (var skin in config.ImportedSkinList)
            {
                if (!string.IsNullOrEmpty(skin.Value.itemDisplayname) && !string.IsNullOrEmpty(skin.Value.itemShortname))
                    continue;

                if (_WorkshopSkinIDCollectionList.Contains(skin.Key.ToString()))
                    continue;

                _WorkshopSkinIDCollectionList.Add(skin.Key.ToString());
            }

            if (!config.sprayCanOveride)
                Unsubscribe("OnActiveItemChanged");

            if (!config.useOnItemCraft)
                Unsubscribe("OnItemCraftFinished");

            //InitUI
            InitUI();

            if (getCollectionscouroutine != null)
            {
                Puts("getcollections already running!!");
            }
            else
            {
                getCollectionscouroutine = GetCollectionSkinIDS();
                ServerMgr.Instance.StartCoroutine(getCollectionscouroutine);
            }
        }

        private void Loaded()
        {
            _defaultSkins = Interface.Oxide.DataFileSystem.GetFile("Skinner/DefaultCraftSkins");
            _playerData = Interface.Oxide.DataFileSystem.GetFile("Skinner/PlayerUsageData");
            _skinRequestsData = Interface.Oxide.DataFileSystem.GetFile("Skinner/SkinsRequestData");

            LoadData();
        }
        private void Unload()
        {
            if (getCollectionscouroutine != null)
                ServerMgr.Instance.StopCoroutine(getCollectionscouroutine);
            if (getSteamWorkshopSkinData != null)
                ServerMgr.Instance.StopCoroutine(getSteamWorkshopSkinData);
            if (getSteamWorkshopRequestData != null)
                ServerMgr.Instance.StopCoroutine(getSteamWorkshopRequestData);
            if (notifyDiscordCoroutine != null)
                ServerMgr.Instance.StopCoroutine(notifyDiscordCoroutine);

            foreach (var player in BasePlayer.allPlayerList)
            {
                UI.DestroyUI(player, BSkinPageUI);
                UI.DestroyUI(player, BSkinSearchUI);
                UI.DestroyUI(player, BSkinSetsSelectUI);
                UI.DestroyUI(player, BSkinRequestsUI);

                if (_playerUsageData.TryGetValue(player.userID, out PlayerData playerData))
                    playerData.UpdateLastOnline();
            }

            foreach (var player in BasePlayer.allPlayerList)
            {
                if (player.TryGetComponent<BoxController>(out BoxController boxController))
                {
                    UnityEngine.Object.Destroy(boxController);
                }

                if (player.TryGetComponent<InventoryWatcher>(out InventoryWatcher inventoryWatcher))
                {
                    UnityEngine.Object.Destroy(inventoryWatcher);
                }

                if (player.TryGetComponent<SpraycanController>(out SpraycanController spraycanController))
                {
                    UnityEngine.Object.Destroy(spraycanController);
                }

            }

            SaveData();

            skinner = null;
            _WaitForSecondsMore = null;
        }

        public static Dictionary<ulong, string>? _skinNames;
        public static Dictionary<int, List<ulong>>? _cachedSkins;

        private static ulong maskID = (ulong)1 << 63;
        private ulong SetMask(ulong num)
        {
            return num | maskID;
        }

        public static ulong GetMask(ulong skinID, int itemID, bool redirectSkin = false)
        {
            return redirectSkin ? (ulong)itemID | maskID : skinID;
        }

        public static bool HasMask(ulong uID)
        {
            return (uID & maskID) == maskID;
        }

        public static ulong UnsetMask(ulong num)
        {
            return num & ~maskID;
        }

        private void AddSkin(ulong skinID, int itemID, string displayName, int redirectID = -1)
        {
            if (config.blacklistedskins.Contains(skinID) || config.blacklisteditems.Contains(itemID) || config.blacklisteditems.Contains(redirectID))
                return;

            if (!_cachedSkins.TryGetValue(itemID, out List<ulong> skinsList))
            {
                skinsList = new List<ulong>() { 0ul };
                _cachedSkins[itemID] = skinsList;
            }
            ulong uID = GetMask(skinID, redirectID, redirectID != -1);


            if (!skinsList.Contains(uID))
                skinsList.Add(uID);

            _skinNames[uID] = displayName;
        }

        private int totskins = 0;
        private void GetSkins()
        {
            if ((Steamworks.SteamInventory.Definitions?.Length ?? 0) == 0)
            {
                Puts("Waiting for Steamworks to update skin item definitions");
                Steamworks.SteamInventory.OnDefinitionsUpdated += GetSkins;
                return;
            }
            int sk = 0;
            Puts("Steamworks Updated, Updating Skins");
            Steamworks.SteamInventory.OnDefinitionsUpdated -= GetSkins;

            Dictionary<int, ItemCategory> catDefinitions = new Dictionary<int, ItemCategory>();

            foreach (ItemDefinition itemDef in ItemManager.GetItemDefinitions())
            {
                if (!catDefinitions.ContainsKey(itemDef.itemid))
                {
                    catDefinitions.Add(itemDef.itemid, itemDef.category);
                }

                if (!config.autoImportApproved)
                    break;

                if (itemDef.isRedirectOf != null)
                {
                    AddSkin(0ul, itemDef.isRedirectOf.itemid, string.Empty, itemDef.itemid);
                    continue;
                }

                foreach (var skin in ItemSkinDirectory.ForItem(itemDef))
                {
                    if (skin.id == 0) continue;

                    ItemSkin itemSkin = skin.invItem as ItemSkin;
                    if (itemSkin == null)
                        continue;

                    bool isredirect = itemSkin?.Redirect != null;
                    if (isredirect)
                        AddSkin((ulong)skin.id, itemDef.itemid, skin.invItem?.displayName?.english ?? itemDef.displayName.english, itemSkin.itemDefinition.itemid);
                    else
                        AddSkin((ulong)skin.id, itemDef.itemid, skin.invItem?.displayName?.english ?? itemDef.displayName.english);
                }
            }

            foreach (Steamworks.InventoryDef item in Steamworks.SteamInventory.Definitions)
            {
                if (!config.autoImportApproved)
                    break;

                string shortname = item.GetProperty("itemshortname") == "lr300.item"
                    ? "rifle.lr300"
                    : item.GetProperty("itemshortname");

                if (string.IsNullOrEmpty(shortname) || item.Id < 100)
                    continue;

                ulong skinid;

                if (!ulong.TryParse(item.GetProperty("workshopid"), out skinid))
                {
                    skinid = (ulong)item.Id;
                }

                if (skinid < 1000000) continue;
                ItemDefinition steamitemdef = ItemManager.FindItemDefinition(shortname);

                if (steamitemdef == null)
                    continue;

                if (_cachedSkins.TryGetValue(steamitemdef.itemid, out List<ulong> cachedskinsList))
                {
                    if (cachedskinsList.Contains((ulong)item.Id))
                        continue;
                }

                if (steamitemdef.isRedirectOf != null)
                    AddSkin(skinid, steamitemdef.isRedirectOf.itemid, item.Name, steamitemdef.itemid);
                else
                    AddSkin(skinid, steamitemdef.itemid, item.Name);
            }

            UpdateImportedSkins();

            List<int> skincachekeys = Pool.Get<List<int>>();
            skincachekeys.AddRange(new List<int>(_cachedSkins.Keys));
            for (int i = skincachekeys.Count - 1; i >= 0; i--)
            {
                var item2 = _cachedSkins[skincachekeys[i]];
                if (item2.Count == 1)
                {
                    _cachedSkins.Remove(skincachekeys[i]);
                    continue;
                }

                ItemDefinition itemdef = ItemManager.FindItemDefinition(skincachekeys[i]);
                if (itemdef == null || itemdef?.Blueprint == null)
                {
                    _cachedSkins.Remove(skincachekeys[i]);
                    continue;
                }
                sk += item2.Count;
            }
            Pool.FreeUnmanaged(ref skincachekeys);

            SaveConfig();

            //Re-order to look nice
            List<KeyValuePair<int, List<ulong>>> tempList = new List<KeyValuePair<int, List<ulong>>>(_cachedSkins);

            tempList.Sort((pair1, pair2) => catDefinitions[pair1.Key].CompareTo(catDefinitions[pair2.Key]));

            _cachedSkins.Clear();

            foreach (KeyValuePair<int, List<ulong>> pair in tempList)
            {
                _cachedSkins.Add(pair.Key, pair.Value);
            }

            _cachedSkinKeys = new List<int>(_cachedSkins.Keys);
            Interface.CallHook("OnSkinnerCacheUpdated", _cachedSkins);
            Puts($"{sk} skins were indexed, Skin indexing complete");

        }
        public static List<int>? _cachedSkinKeys;
        private void UpdateImportedSkins()
        {
            List<ulong> keys = Pool.Get<List<ulong>>();
            keys.AddRange(new List<ulong>(config.ImportedSkinList.Keys));

            for (int i = config.ImportedSkinList.Count - 1; i >= 0; i--)
            {
                var whitelistSkin = config.ImportedSkinList[keys[i]];
                if (string.IsNullOrEmpty(whitelistSkin.itemDisplayname) || string.IsNullOrEmpty(whitelistSkin.itemShortname))
                {
                    config.ImportedSkinList.Remove(keys[i]);
                    continue;
                }

                ItemDefinition itemdef = ItemManager.FindItemDefinition(whitelistSkin.itemShortname);

                if (itemdef == null)
                {
                    config.ImportedSkinList.Remove(keys[i]);
                    Puts($"Could not find item definition for {whitelistSkin.itemShortname} {keys[i]}");
                    continue;
                }
                AddSkin(keys[i], itemdef.itemid, whitelistSkin.itemDisplayname);
            }
            Pool.FreeUnmanaged(ref keys);
        }

        #endregion Init

        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty("Skin Commands (skin items in you inventory")]
            public string[] cmdsskin = new[] { "skin", "s", "skinbox", "sb" };

            [JsonProperty("Skin Items Commands (skin items you have already placed")]
            public string[] cmdsskinitems = new[] { "skinitem", "si", "skindeployed", "sd" };

            [JsonProperty("Set default items to be skinned")]
            public string[] cmdsskincraft = new[] { "skincraft", "sc" };

            [JsonProperty("Automatically set all items in you inventory to your default skins")]
            public string[] cmdsskininv = new[] { "skininv", "sinv" };

            [JsonProperty("Automatically set all items a container to your default skins")]
            public string[] cmdsskincon = new[] { "skincon", "scon" };

            [JsonProperty("Automatically skin all deployables in your base")]
            public string[] cmdskinbase = new[] { "skinbase", "skinbuilding" };

            [JsonProperty("Automatically skin all items in your base")]
            public string[] cmdskinallitems = new[] { "skinall", "sa" };

            [JsonProperty("Automatically skin all items that are moved into you inventory")]
            public string[] cmdtoggleautoskin = new[] { "skinauto", "sauto" };

            [JsonProperty("Skin your teams inventories with your skin set")]
            public string[] cmdskinteam = new[] { "skinteam", "st" };

            [JsonProperty("Request workshop skins via workshop ID")]
            public string[] cmdskinrequest = new[] { "skinrequest", "sr" };

            [JsonProperty("Approve workshop skin requests")]
            public string[] cmdskinrequests = new[] { "skinrequests", "srs" };

            [JsonProperty("Set your selected skin set")]
            public string[] cmdskinset = new[] { "skinset", "ss" };

            [JsonProperty("Import Custom Skins")] public string[] cmdskinimport = new[] { "skinimport", "sip" };

            [JsonProperty("Import Workshop Collection Command")] public string[] cmdcollectionimport = new[] { "colimport", "cip" };

            [JsonProperty("Skin Request Notification Discord Webhook")]
            public string DiscordWebhook = "";

            [JsonProperty("Custom Page Change UI Positon anchor/offset 'min x, min y', 'max x', max y'")]
            public string[] uiposition = new[] { "0.5 0.0", "0.5 0.0", "198 60", "400 97" };

            [JsonProperty("Custom Searchbar UI Positon anchor/offset 'min x, min y', 'max x', max y'")]
            public string[] uisearchposition = new[] { "0.5 0.0", "0.5 0.0", "410 635", "572 660" };

            [JsonProperty("Custom Set Selection UI Positon anchor/offset 'min x, min y', 'max x', max y'")]
            public string[] uisetsposition = new[] { "0.5 0.0", "0.5 0.0", "250 610", "573 633" };

            [JsonProperty("Auto import approved skins")]
            public bool autoImportApproved = true;

            [JsonProperty("Remove player data after inactivity (days)")]
            public int removedataTime = 14;

            [JsonProperty("Apply names of skins to skinned items")]
            public bool applySkinNames = true;

            [JsonProperty("Add Search Bar UI")]
            public bool searchbar = true;

            [JsonProperty("Use on itemcraft hook (skin items after crafting - not required when using skinauto)")]
            public bool useOnItemCraft = false;

            [JsonProperty("Override spraycan behaviour")]
            public bool sprayCanOveride = false;

            [JsonProperty("Use spraycan effect when holding spraycan and skinning deployables")]
            public bool sprayCanEffect = true;

            [JsonProperty("Blacklisted Skins (skinID)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ulong> blacklistedskins = new List<ulong>();

            [JsonProperty("Blacklisted Items (itemID)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<int> blacklisteditems = new List<int>();

            [JsonProperty("Import Skin collections (steam workshop ID)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ulong> skinCollectionIDs = new List<ulong>();

            [JsonProperty("Command based cooldowns ('permission' : 'command' seconds")]
            public Dictionary<string, CoolDowns> Cooldowns = new Dictionary<string, CoolDowns>() { { "Default30CD", new CoolDowns() } };

            [JsonProperty("Imported Skins List", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<ulong, ImportedItem> ImportedSkinList = new Dictionary<ulong, ImportedItem>();

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        public struct ImportedItem
        {
            public string itemShortname;
            public string itemDisplayname;
        }

        public struct RequestItem
        {
            public ulong skinID;
            public int itemID;
            public string itemDisplayname;
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }
                var configDict = config.ToDictionary();
                Dictionary<string, object> defaultconObjects = new Dictionary<string, object>();
                foreach (var obj in Config)
                {
                    defaultconObjects.Add(obj.Key, obj.Value);
                }
                if (configDict.Count != defaultconObjects.Count)
                {
                    Puts("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
                else
                {
                    foreach (var key in configDict.Keys)
                    {
                        if (defaultconObjects.ContainsKey(key))
                            continue;

                        Puts("Configuration appears to be outdated; updating and saving");
                        SaveConfig();
                        break;

                    }
                }
            }
            catch
            {
                Puts($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Puts($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion Configuration

        #region Data
        private DynamicConfigFile _defaultSkins;
        private DynamicConfigFile _playerData;
        private DynamicConfigFile _skinRequestsData;

        private Dictionary<ulong, CoolDowns> _playercooldowns = new Dictionary<ulong, CoolDowns>();

        private Dictionary<ulong, int> _playerSelectedSet = new Dictionary<ulong, int>();

        private void LoadData()
        {
            List<ulong> skipdata = Pool.Get<List<ulong>>();

            try
            {
                _playerUsageData = new Dictionary<ulong, PlayerData>();
                long timenow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                //need to read as dec and parse to ulong
                var temp = _playerData.ReadObject<Dictionary<ulong, PlayerDataDec>>();
                foreach (var pair in temp)
                {
                    //Not logged in for two weeks
                    if (timenow - pair.Value.lastonline > config.removedataTime * 86400)
                    {
                        skipdata.Add(pair.Key);
                        continue;
                    }

                    _playerUsageData.Add(pair.Key, new PlayerData() { lastonline = pair.Value.lastonline });

                    foreach (var innerDict in pair.Value.skinusage)
                    {
                        List<ulong> ulongList = new List<ulong>();
                        foreach (decimal d in innerDict.Value)
                        {
                            ulong id = decimal.ToUInt64(d);
                            if (HasMask(id) && config.blacklisteditems.Contains((int)UnsetMask(id)))
                                continue;
                            ulongList.Add(decimal.ToUInt64(d));

                        }
                        _playerUsageData[pair.Key].skinusage.TryAdd(innerDict.Key, ulongList);
                    }
                }
            }
            catch
            {
                _playerUsageData = new Dictionary<ulong, PlayerData>();
            }
            try
            {
                _playerDefaultSkins = new Dictionary<ulong, Dictionary<int, ulong>[]>();
                //need to read as dec and parse to ulong
                var temp = _defaultSkins.ReadObject<Dictionary<ulong, Dictionary<int, decimal>[]>>();
                foreach (var outerPair in temp)
                {
                    if (skipdata.Contains(outerPair.Key))
                        continue;

                    var innerDictArray = new Dictionary<int, ulong>[outerPair.Value.Length];
                    for (int i = 0; i < outerPair.Value.Length; i++)
                    {
                        var innerDict = new Dictionary<int, ulong>();
                        foreach (var innerPair in outerPair.Value[i])
                        {
                            if (HasMask((ulong)innerPair.Value) && config.blacklisteditems.Contains((int)UnsetMask((ulong)innerPair.Value)))
                                continue;
                            innerDict.Add(innerPair.Key, (ulong)innerPair.Value);
                        }
                        innerDictArray[i] = innerDict;
                    }
                    _playerDefaultSkins.Add(outerPair.Key, innerDictArray);
                }
            }
            catch
            {
                _playerDefaultSkins = new Dictionary<ulong, Dictionary<int, ulong>[]>();
            }
            try
            {
                _requestsData = _skinRequestsData.ReadObject<List<RequestItem>>();
            }
            catch
            {
                _requestsData = new List<RequestItem>();
            }
            Pool.FreeUnmanaged(ref skipdata);
        }

        private void SaveData()
        {
            Puts("DataSaved");
            _defaultSkins.WriteObject(_playerDefaultSkins);
            _playerData.WriteObject(_playerUsageData);
            _skinRequestsData.WriteObject(_requestsData);
        }

        private Dictionary<ulong, Dictionary<int, ulong>[]> _playerDefaultSkins;
        private Dictionary<ulong, PlayerData> _playerUsageData;
        private List<RequestItem> _requestsData;

        public class PlayerDataDec
        {
            public Dictionary<int, List<decimal>> skinusage = new Dictionary<int, List<decimal>>();
            public long lastonline { get; set; } = 0;
        }

        public class PlayerData
        {
            public Dictionary<int, List<ulong>> skinusage = new Dictionary<int, List<ulong>>();
            public long lastonline { get; set; } = 0;

            public void AddSkinUsage(ulong skinID, int itemID, int rItemID = 0)
            {
                if (skinID == 0 && rItemID == 0) return;

                if (!skinusage.TryGetValue(itemID, out var list))
                {
                    list = new List<ulong>(6);
                    skinusage[itemID] = list;
                }

                ulong uID = GetMask(skinID, rItemID, rItemID != 0);
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i] != uID)
                        continue;

                    list.RemoveAt(i);
                }

                if (list.Count >= 6)
                {
                    list.RemoveAt(0);
                }

                list.Add(uID);
            }

            public List<ulong>? GetSkinUsage(int itemID)
            {
                if (!skinusage.TryGetValue(itemID, out List<ulong>? usageList))
                {
                    return null;
                }
                return usageList;
            }

            public void UpdateLastOnline()
            {
                lastonline = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
        }

        public class CoolDowns
        {
            public float skin = 30f;
            public float skinitem = 30f;
            public float skincraft = 30f;
            public float skincon = 30f;
            public float skininv = 30f;
            public float skinteam = 30f;
            public float skinbase = 60f;
            public float skinall = 60f;
        }
        #endregion Data

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPerms"] = "You don't have permissions to use this command",
                ["NoBuildingAuth"] = "You must have building auth to use this",
                ["NoObjectsFound"] = "No object found",
                ["NoSkins"] = "No skins available",
                ["ImportSkinArgs"] = "Bad args, Required input skinid",
                ["ImportCollectionArgs"] = "Bad args, Required input collectionID",
                ["SkinIDError"] = "Cannot parse skinid {0}",
                ["NoShortname"] = "No item found for shortname : {0}",
                ["DuplicateSkin"] = "Duplicate Skin ID for : {0} {1}",
                ["SkinImported2"] = "Skin {0} has been imported and saved",
                ["SkinRequested"] = "Skin {0} has been requested",
                ["AlreadyRequesting"] = "Already updating requests data please wait...",
                ["RequestingData"] = "Updating requests data please wait...",
                ["CollectionImported2"] = "Steam Skin Collection {0} has been imported and saved",
                ["CommandCooldown"] = "You can not use this command for another {0}",
                ["CompletedInvSkin"] = "All items in your inventory have been set to your default skins",
                ["CompletedConSkin"] = "All items in {0} have been set to your default skins",
                ["CompletedBuildingSkin"] = "All {0} in your base have been set to your default skins",
                ["CompletedAllSkin"] = "All {0} items in your base have been set to your default skins",
                ["SkinSetSelected"] = "Skin set {0} selected",
                ["SkinSetSelectedArgs"] = "Bad args, Required input set No. 1, 2 or 3",
                ["AutoSkinEnabled"] = "Auto skins enabled",
                ["AutoSkinDisabled"] = "Auto skins disabled",
                ["CompletedTeamSkin"] = "All items in your team have been set skin set {0}",
                ["NoTeam"] = "You need to be in a team to use this feature",
                ["TeamSkinBlockEnabled"] = "You enabled team skins",
                ["TeamSkinBlockDisabled"] = "You disabled team skins"
            }, this);
        }

        #endregion Localization

        #region Commands
        private void SkinAutoCMD(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player == null) return;

            if (!HasPerm(player.UserIDString, permskinauto))
            {
                ChatMessage(iplayer, "NoPerms");
                return;
            }

            if (!player.TryGetComponent<InventoryWatcher>(out InventoryWatcher inventoryWatcher))
            {
                player.gameObject.AddComponent<InventoryWatcher>();
                ChatMessage(iplayer, "AutoSkinEnabled");
                permission.GrantUserPermission(player.UserIDString, permskinautotoggle, this);
                return;
            }
            permission.RevokeUserPermission(player.UserIDString, permskinautotoggle);

            UnityEngine.Object.Destroy(inventoryWatcher);
            ChatMessage(iplayer, "AutoSkinDisabled");
        }

        private void SkinImportCMD(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player != null)
            {
                if (!HasPerm(player.UserIDString, permimport))
                {
                    ChatMessage(iplayer, "NoPerms");
                    return;
                }
            }
            else if (!iplayer.IsServer)
                return;

            if (args.Length < 1)
            {
                ChatMessage(iplayer, "ImportSkinArgs");
                return;
            }

            ulong skinid = 0ul;
            if (!ulong.TryParse(args[0], out skinid))
            {
                ChatMessage(iplayer, "ImportSkinArgs", args[0]);
                return;
            }
            _WorkshopSkinIDCollectionList.Add(skinid.ToString());
            if (getSteamWorkshopSkinData != null)
            {
                Puts("getSteamWorkshopSkinData already running!!");
            }
            else
            {
                getSteamWorkshopSkinData = GetSteamWorkshopSkinData();
                ServerMgr.Instance.StartCoroutine(getSteamWorkshopSkinData);
            }
            ChatMessage(iplayer, "SkinImported2", args[0]);
        }

        private void SkinImportCollection(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player != null)
            {
                if (!HasPerm(player.UserIDString, permimport))
                {
                    ChatMessage(iplayer, "NoPerms");
                    return;
                }
            }
            else if (!iplayer.IsServer)
                return;

            if (args.Length < 1)
            {
                ChatMessage(iplayer, "ImportCollectionArgs");
                return;
            }

            ulong collectionid = 0ul;
            if (!ulong.TryParse(args[0], out collectionid))
            {
                ChatMessage(iplayer, "ImportCollectionArgs", args[0]);
                return;
            }

            config.skinCollectionIDs.Add(collectionid);

            if (getCollectionscouroutine != null)
            {
                Puts("getcollections already running!!");
            }
            else
            {
                getCollectionscouroutine = GetCollectionSkinIDS();
                ServerMgr.Instance.StartCoroutine(getCollectionscouroutine);
            }
            ChatMessage(iplayer, "CollectionImported2", args[0]);
        }

        private void SkinCMD(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player == null) return;

            if (!HasPerm(player.UserIDString, permdefault))
            {
                ChatMessage(iplayer, "NoPerms");
                return;
            }

            if (_viewingcon.TryGetValue(player, out BoxController boxController))
            {
                UnityEngine.Object.Destroy(boxController);
                return;
            }

            //Check for cooldown
            float cdtime = 0;

            //Find shortest cd perm
            foreach (var cdperm in config.Cooldowns)
            {
                if (!HasPerm(player.UserIDString, $"skinner.{cdperm.Key}")) continue;
                if (cdtime == 0)
                    cdtime = cdperm.Value.skin;
                else if (cdtime > cdperm.Value.skin)
                    cdtime = cdperm.Value.skin;
            }

            if (cdtime > 0)
            {
                if (!_playercooldowns.TryGetValue(player.userID, out CoolDowns? coolDowns))
                    _playercooldowns.Add(player.userID, new CoolDowns() { skin = Time.time });
                else
                {
                    if (coolDowns.skin + cdtime > Time.time)
                    {
                        ChatMessage(iplayer, "CommandCooldown", TimeSpan.FromSeconds(coolDowns.skin + cdtime - Time.time).ToString("hh' hrs 'mm' mins 'ss' secs'"));
                        return;
                    }
                    coolDowns.skin = Time.time;
                }
            }

            ItemContainer itemContainer = CreateContainer();

            boxController = player.gameObject.AddComponent<BoxController>();
            boxController.inventory = itemContainer;
            boxController.StartAwake();

            boxController.StartItemSkin();

            _viewingcon.Add(player, boxController);

            player.Invoke(() => StartLootingCon(player, itemContainer), 0.3f);
        }


        private void DefaultSkinsCMD(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player == null) return;

            if (!HasPerm(player.UserIDString, permcraft))
            {
                ChatMessage(iplayer, "NoPerms");
                return;
            }

            if (_viewingcon.TryGetValue(player, out BoxController boxController))
            {
                UnityEngine.Object.Destroy(boxController);
                return;
            }

            //Check for cooldown
            float cdtime = 0;
            //Find shortest cd perm
            foreach (var cdperm in config.Cooldowns)
            {
                if (!HasPerm(player.UserIDString, $"skinner.{cdperm.Key}")) continue;
                if (cdtime == 0)
                    cdtime = cdperm.Value.skincraft;
                else if (cdtime > cdperm.Value.skincraft)
                    cdtime = cdperm.Value.skincraft;
            }

            if (cdtime > 0)
            {
                if (!_playercooldowns.TryGetValue(player.userID, out CoolDowns? coolDowns))
                    _playercooldowns.Add(player.userID, new CoolDowns() { skincraft = Time.time });
                else
                {
                    if (coolDowns.skincraft + cdtime > Time.time)
                    {
                        ChatMessage(iplayer, "CommandCooldown", TimeSpan.FromSeconds(coolDowns.skincraft + cdtime - Time.time).ToString("hh' hrs 'mm' mins 'ss' secs'"));
                        return;
                    }
                    coolDowns.skincraft = Time.time;
                }
            }

            if (!_playerDefaultSkins.ContainsKey(player.userID))
                _playerDefaultSkins.Add(player.userID, new Dictionary<int, ulong>[3]
                {
                    new Dictionary<int, ulong>(),
                    new Dictionary<int, ulong>(),
                    new Dictionary<int, ulong>()
                });

            ItemContainer itemContainer = CreateContainer();

            boxController = player.gameObject.AddComponent<BoxController>();
            boxController.inventory = itemContainer;

            boxController.StartAwake();
            boxController.SkinCraft();

            _viewingcon.TryAdd(player, boxController);

            player.Invoke(() => StartLootingCon(player, itemContainer), 0.3f);
        }

        private void SkinRequestCMD(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player == null) return;

            if (!HasPerm(player.UserIDString, permskinrequest))
            {
                ChatMessage(iplayer, "NoPerms");
                return;
            }

            if (args.Length < 1)
            {
                ChatMessage(iplayer, "ImportSkinArgs");
                return;
            }

            if (!ulong.TryParse(args[0], out ulong skinidResult))
            {
                ChatMessage(iplayer, "ImportSkinArgs");
                return;
            }

            ChatMessage(iplayer, "SkinRequested", skinidResult);

            foreach (var a in _requestsData)
            {
                if (a.skinID == skinidResult)
                    return;
            }

            _requestsData.Add(new RequestItem { skinID = skinidResult });

            if (string.IsNullOrEmpty(config.DiscordWebhook))
                return;


            _discordData.Add(new DiscordData() { SkinID = skinidResult, player = player });
            if (notifyDiscordCoroutine != null)
            {
                Puts("getcollections already running!!");
            }
            else
            {
                notifyDiscordCoroutine = NotifyDiscord();
                ServerMgr.Instance.StartCoroutine(notifyDiscordCoroutine);
            }
        }

        private void SkinRequestsCMD(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player == null) return;

            if (!HasPerm(player.UserIDString, permimport))
            {
                ChatMessage(iplayer, "NoPerms");
                return;
            }

            if (_requestsData.Count == 0)
            {
                ChatMessage(iplayer, "NoRequests");
                return;
            }

            if (getSteamWorkshopRequestData != null)
            {
                ChatMessage(iplayer, "AlreadyRequesting");
                return;
            }
            else
            {
                ChatMessage(iplayer, "RequestingData");
                getSteamWorkshopRequestData = GetSteamWorkshopSkinRequests();
                ServerMgr.Instance.StartCoroutine(getSteamWorkshopRequestData);
            }


            if (_viewingcon.TryGetValue(player, out BoxController boxController))
            {
                UnityEngine.Object.Destroy(boxController);
                return;
            }

            ItemContainer itemContainer = CreateContainer();

            boxController = player.gameObject.AddComponent<BoxController>();
            boxController.inventory = itemContainer;

            boxController.StartAwake();

            _viewingcon.TryAdd(player, boxController);

            player.StartCoroutine(CheckforRequests(player, itemContainer, boxController));
        }

        private static WaitForSeconds _WaitForSecondsMore;

        private IEnumerator CheckforRequests(BasePlayer player, ItemContainer itemContainer, BoxController boxController)
        {
            yield return _WaitForSecondsMore;
            int i = 0;
            for (i = 0; i < 15; i++)
            {
                if (getSteamWorkshopRequestData == null)
                    break;
                ChatMessage(player.IPlayer, "Updating requests data please wait...");
                yield return _WaitForSecondsMore;
            }

            if (i >= 15)
            {
                ChatMessage(player.IPlayer, "Updating requests timed out try again later");
                UnityEngine.Object.Destroy(boxController);
            }
            else
            {
                boxController.SkinRequests();
                StartLooting(player, itemContainer);
            }
        }

        private static int Layermask;
        private void SkinItemCMD(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player == null) return;

            if (!HasPerm(player.UserIDString, permitems))
            {
                ChatMessage(iplayer, "NoPerms");
                return;
            }

            if (!player.CanBuild() && !HasPerm(player.UserIDString, permbypassauth))
            {
                ChatMessage(iplayer, "NoBuildingAuth");
                return;
            }

            RaycastHit raycastHit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out raycastHit, 5f, Layermask))
            {
                ChatMessage(iplayer, "NoObjectsFound");
                return;
            }
            BaseCombatEntity entity = raycastHit.GetEntity() as BaseCombatEntity;
            ItemDefinition? itemDefinition = null;
            if (entity.pickup.itemTarget != null)
            {
                itemDefinition = entity.pickup.itemTarget;
            }
            else if (entity.repair.itemTarget != null)
            {

                itemDefinition = entity.repair.itemTarget;
            }

            if (itemDefinition == null)
            {
                ChatMessage(iplayer, "NoObjectsFound");
                return;
            }

            if (!_cachedSkins.ContainsKey(itemDefinition.isRedirectOf != null ? itemDefinition.isRedirectOf.itemid : itemDefinition.itemid))
            {
                ChatMessage(iplayer, "NoSkins");
                return;
            }

            if (_viewingcon.TryGetValue(player, out BoxController boxController))
            {
                UnityEngine.Object.Destroy(boxController);
                return;
            }

            //Check for cooldown
            float cdtime = 0;
            //Find shortest cd perm
            foreach (var cdperm in config.Cooldowns)
            {
                if (!HasPerm(player.UserIDString, $"skinner.{cdperm.Key}")) continue;
                if (cdtime == 0)
                    cdtime = cdperm.Value.skinitem;
                else if (cdtime > cdperm.Value.skinitem)
                    cdtime = cdperm.Value.skinitem;
            }

            if (cdtime > 0)
            {
                if (!_playercooldowns.TryGetValue(player.userID, out CoolDowns? coolDowns))
                    _playercooldowns.Add(player.userID, new CoolDowns() { skinitem = Time.time });
                else
                {
                    if (coolDowns.skinitem + cdtime > Time.time)
                    {
                        ChatMessage(iplayer, "CommandCooldown", TimeSpan.FromSeconds(coolDowns.skinitem + cdtime - Time.time).ToString("hh' hrs 'mm' mins 'ss' secs'"));
                        return;
                    }
                    coolDowns.skinitem = Time.time;
                }
            }

            if (args.Length > 0 && int.TryParse(args[0], out int setselect))
            {
                Dictionary<int, ulong> cachedskins = GetCachedSkins(player, setselect);

                if (cachedskins.Count < 1)
                    return;

                int itemID = itemDefinition.isRedirectOf != null ? itemDefinition.isRedirectOf.itemid : itemDefinition.itemid;

                if (cachedskins.TryGetValue(itemID, out ulong uID))
                {
                    SkinDeployable(entity, uID);
                }
                return;
            }

            ItemContainer itemContainer = CreateContainer();

            boxController = player.gameObject.AddComponent<BoxController>();
            boxController.inventory = itemContainer;

            boxController.StartAwake();

            boxController.SkinDeplyoables(entity, itemDefinition);

            _viewingcon.TryAdd(player, boxController);

            player.Invoke(() => StartLootingCon(player, itemContainer), 0.3f);
        }

        private void SkinInvCMD(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player == null) return;

            if (!HasPerm(player.UserIDString, permskininv))
            {
                ChatMessage(iplayer, "NoPerms");
                return;
            }

            int setselect = -1;
            if (args.Length > 0 && !int.TryParse(args[0], out setselect))
            {
                setselect = -1;
            }

            Dictionary<int, ulong> cachedskins = GetCachedSkins(player, setselect);

            if (cachedskins.Count < 1)
                return;

            if (player.inventory == null)
                return;

            //Check for cooldown
            float cdtime = 0;
            //Find shortest cd perm
            foreach (var cdperm in config.Cooldowns)
            {
                if (!HasPerm(player.UserIDString, $"skinner.{cdperm.Key}")) continue;
                if (cdtime == 0)
                    cdtime = cdperm.Value.skininv;
                else if (cdtime > cdperm.Value.skininv)
                    cdtime = cdperm.Value.skininv;
            }

            if (cdtime > 0)
            {
                if (!_playercooldowns.TryGetValue(player.userID, out CoolDowns? coolDowns))
                    _playercooldowns.Add(player.userID, new CoolDowns() { skininv = Time.time });
                else
                {
                    if (coolDowns.skininv + cdtime > Time.time)
                    {
                        ChatMessage(iplayer, "CommandCooldown", TimeSpan.FromSeconds(coolDowns.skininv + cdtime - Time.time).ToString("hh' hrs 'mm' mins 'ss' secs'"));
                        return;
                    }
                    coolDowns.skininv = Time.time;
                }
            }

            List<Item> itemstoSkin = Pool.Get<List<Item>>();
            player.inventory.GetAllItems(itemstoSkin);

            for (int i = 0; i < itemstoSkin.Count; i++)
            {
                Item item = itemstoSkin[i];
                if (item == null) continue;

                if (item.IsBackpack())
                {
                    if (item?.contents != null && !item.contents.itemList.IsNullOrEmpty()) //Fix for diving tanks
                    {
                        foreach (var bitem in item.contents.itemList)
                            itemstoSkin.Add(bitem);
                    }
                    continue;
                }


                if (cachedskins.TryGetValue(item.info.isRedirectOf != null ? item.info.isRedirectOf.itemid : item.info.itemid, out ulong uID))
                {
                    //skip blacklisted skin
                    if (config.blacklistedskins.Contains(item.skin) || config.blacklisteditems.Contains(item.info.itemid)) continue;

                    SkinItem(item, uID);
                }
            }

            Pool.FreeUnmanaged(ref itemstoSkin);

            player.SendNetworkUpdateImmediate();
            ChatMessage(iplayer, "CompletedInvSkin");
        }

        private void SkinTeamCMD(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player == null) return;

            if (!HasPerm(player.UserIDString, permskinteam))
            {
                ChatMessage(iplayer, "NoPerms");
                return;
            }

            if (args.Length > 0)
            {
                if (args[0].ToLower() == "toggle")
                {
                    if (HasPerm(player.UserIDString, permskinteamblock))
                    {
                        permission.RevokeUserPermission(player.UserIDString, permskinteamblock);
                        ChatMessage(iplayer, "TeamSkinBlockEnabled");

                    }
                    else
                    {
                        permission.GrantUserPermission(player.UserIDString, permskinteamblock, this);
                        ChatMessage(iplayer, "TeamSkinBlockDisabled");

                    }

                    return;
                }
            }

            int setselect = -1;
            if (args.Length > 0 && !int.TryParse(args[0], out setselect))
            {
                setselect = -1;
            }

            Dictionary<int, ulong> cachedskins = GetCachedSkins(player, setselect);

            if (cachedskins.Count < 1)
                return;

            if (player.Team == null)
            {
                ChatMessage(iplayer, "NoTeam");
                return;
            }

            if (player.inventory == null)
                return;

            RelationshipManager.ServerInstance.playerToTeam.TryGetValue(player.userID, out RelationshipManager.PlayerTeam playerTeam);

            if (playerTeam.members.Count == 0)
                return;

            //Check for cooldown
            float cdtime = 0;
            //Find shortest cd perm
            foreach (var cdperm in config.Cooldowns)
            {
                if (!HasPerm(player.UserIDString, $"skinner.{cdperm.Key}")) continue;
                if (cdtime == 0)
                    cdtime = cdperm.Value.skinteam;
                else if (cdtime > cdperm.Value.skinteam)
                    cdtime = cdperm.Value.skinteam;
            }

            if (cdtime > 0)
            {
                if (!_playercooldowns.TryGetValue(player.userID, out CoolDowns? coolDowns))
                    _playercooldowns.Add(player.userID, new CoolDowns() { skinteam = Time.time });
                else
                {
                    if (coolDowns.skinteam + cdtime > Time.time)
                    {
                        ChatMessage(iplayer, "CommandCooldown", TimeSpan.FromSeconds(coolDowns.skinteam + cdtime - Time.time).ToString("hh' hrs 'mm' mins 'ss' secs'"));
                        return;
                    }
                    coolDowns.skinteam = Time.time;
                }
            }


            List<Item> itemstoSkin = Pool.Get<List<Item>>();
            List<BasePlayer> teamplayersList = new List<BasePlayer>();
            foreach (var teamplayer in playerTeam.members)
            {
                BasePlayer? teamBasePlayer = null;
                foreach (var allPlayer in BasePlayer.allPlayerList)
                {
                    if (allPlayer == null || allPlayer.userID != teamplayer)
                        continue;
                    teamBasePlayer = allPlayer;
                    break;
                }

                if (teamBasePlayer == null || teamBasePlayer.inventory == null)
                    continue;

                teamplayersList.Add(teamBasePlayer);
                if (HasPerm(teamBasePlayer.UserIDString, permskinteamblock))
                    continue;

                if (teamBasePlayer.inventory.containerMain != null)
                    itemstoSkin.AddRange((IEnumerable<Item>)teamBasePlayer.inventory.containerMain.itemList);
                if (teamBasePlayer.inventory.containerBelt != null)
                    itemstoSkin.AddRange((IEnumerable<Item>)teamBasePlayer.inventory.containerBelt.itemList);
                if (teamBasePlayer.inventory.containerWear != null)
                    itemstoSkin.AddRange((IEnumerable<Item>)teamBasePlayer.inventory.containerWear.itemList);

            }

            for (int i = 0; i < itemstoSkin.Count; i++)
            {
                Item item = itemstoSkin[i];
                if (item == null) continue;

                if (item.IsBackpack())
                {
                    if (item?.contents != null && !item.contents.itemList.IsNullOrEmpty()) //Fix for diving tanks
                    {
                        foreach (var bitem in item.contents.itemList)
                            itemstoSkin.Add(bitem);
                    }
                    continue;
                }

                if (cachedskins.TryGetValue(item.info.isRedirectOf != null ? item.info.isRedirectOf.itemid : item.info.itemid, out ulong uID))
                {
                    //skip blacklisted skin
                    if (config.blacklistedskins.Contains(item.skin) || config.blacklisteditems.Contains(item.info.itemid)) continue;

                    SkinItem(item, uID);
                }
            }

            foreach (var teambp in teamplayersList)
            {
                if (!teambp.IsSleeping()) continue;
                SendNetworkUpdate(teambp);
            }

            Pool.FreeUnmanaged(ref itemstoSkin);

            ChatMessage(iplayer, "CompletedTeamSkin", setselect != -1 ? setselect : string.Empty);
        }

        private void SkinConCMD(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player == null) return;

            if (!HasPerm(player.UserIDString, permskincon))
            {
                ChatMessage(iplayer, "NoPerms");
                return;
            }
            if (!player.IsBuildingAuthed() && !HasPerm(player.UserIDString, permbypassauth))
            {
                ChatMessage(iplayer, "NoBuildingAuth");
                return;
            }

            int setselect = -1;
            if (args.Length > 0 && !int.TryParse(args[0], out setselect))
            {
                setselect = -1;
            }

            Dictionary<int, ulong> cachedskins = GetCachedSkins(player, setselect);

            if (cachedskins.Count < 1)
                return;

            RaycastHit raycastHit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out raycastHit, 5f, Layermask))
            {
                ChatMessage(iplayer, "NoObjectsFound");
                return;
            }

            StorageContainer storage = raycastHit.GetEntity() as StorageContainer;
            if (storage == null || storage?.inventory == null)
            {
                ChatMessage(iplayer, "NoObjectsFound");
                return;
            }

            //Check for cooldown
            float cdtime = 0;
            //Find shortest cd perm
            foreach (var cdperm in config.Cooldowns)
            {
                if (!HasPerm(player.UserIDString, $"skinner.{cdperm.Key}")) continue;
                if (cdtime == 0)
                    cdtime = cdperm.Value.skincon;
                else if (cdtime > cdperm.Value.skincon)
                    cdtime = cdperm.Value.skincon;
            }

            if (cdtime > 0)
            {
                if (!_playercooldowns.TryGetValue(player.userID, out CoolDowns? coolDowns))
                    _playercooldowns.Add(player.userID, new CoolDowns() { skincon = Time.time });
                else
                {
                    if (coolDowns.skincon + cdtime > Time.time)
                    {
                        ChatMessage(iplayer, "CommandCooldown", TimeSpan.FromSeconds(coolDowns.skincon + cdtime - Time.time).ToString("hh' hrs 'mm' mins 'ss' secs'"));
                        return;
                    }
                    coolDowns.skincon = Time.time;
                }
            }

            foreach (Item item in storage.inventory.itemList)
            {
                if (item == null) continue;

                if (cachedskins.TryGetValue(item.info.isRedirectOf != null ? item.info.isRedirectOf.itemid : item.info.itemid, out ulong uID))
                {
                    //skip blacklisted skin
                    if (config.blacklistedskins.Contains(item.skin) || config.blacklisteditems.Contains(item.info.itemid)) continue;

                    SkinItem(item, uID);
                }
            }

            ChatMessage(iplayer, "CompletedConSkin", storage.ShortPrefabName);
        }

        private void SkinBaseCMD(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player == null) return;

            if (!HasPerm(player.UserIDString, permskinbase))
            {
                ChatMessage(iplayer, "NoPerms");
                return;
            }
            Dictionary<int, ulong> cachedskins = GetCachedSkins(player);

            if (cachedskins.Count < 1)
                return;

            if (!player.IsBuildingAuthed() && !HasPerm(player.UserIDString, permbypassauth))
            {
                ChatMessage(iplayer, "NoBuildingAuth");
                return;
            }
            BuildingPrivlidge buildingPrivlidge = player.GetBuildingPrivilege();
            if (buildingPrivlidge == null)
            {
                ChatMessage(iplayer, "NoBuildingAuth");
                return;
            }
            BuildingManager.Building buildingManager = buildingPrivlidge.GetBuilding();
            if (buildingManager == null)
            {
                ChatMessage(iplayer, "NoBuildingAuth");
                return;
            }

            //Check for cooldown
            float cdtime = 0;
            //Find shortest cd perm
            foreach (var cdperm in config.Cooldowns)
            {
                if (!HasPerm(player.UserIDString, $"skinner.{cdperm.Key}")) continue;
                if (cdtime == 0)
                    cdtime = cdperm.Value.skinbase;
                else if (cdtime > cdperm.Value.skinbase)
                    cdtime = cdperm.Value.skinbase;
            }

            if (cdtime > 0)
            {
                if (!_playercooldowns.TryGetValue(player.userID, out CoolDowns? coolDowns))
                    _playercooldowns.Add(player.userID, new CoolDowns() { skinbase = Time.time });
                else
                {
                    if (coolDowns.skinbase + cdtime > Time.time)
                    {
                        ChatMessage(iplayer, "CommandCooldown", TimeSpan.FromSeconds(coolDowns.skinbase + cdtime - Time.time).ToString("hh' hrs 'mm' mins 'ss' secs'"));
                        return;
                    }
                    coolDowns.skinbase = Time.time;
                }
            }

            string skinned = "all deployables";
            if (args.Length > 0)
            {
                skinned = $"{args[0]}s";
            }

            foreach (var decayent in buildingManager.decayEntities)
            {
                BaseCombatEntity? baseCombatEntity = decayent?.GetEntity() as BaseCombatEntity;
                if (baseCombatEntity == null || baseCombatEntity.pickup.itemTarget == null) continue;

                if (args.Length > 0)
                {
                    if (!baseCombatEntity.pickup.itemTarget.shortname.Contains(args[0]))
                        continue;
                }

                int itemID = baseCombatEntity.pickup.itemTarget.isRedirectOf != null ? baseCombatEntity.pickup.itemTarget.isRedirectOf.itemid : baseCombatEntity.pickup.itemTarget.itemid;
                if (cachedskins.TryGetValue(itemID, out ulong uID))
                {

                    SkinDeployable(baseCombatEntity, uID);

                }
            }
            ChatMessage(iplayer, "CompletedBuildingSkin", skinned);
        }

        private Dictionary<int, ulong> GetCachedSkins(BasePlayer player, int set = -1)
        {
            Dictionary<int, ulong>[] cachedskins;

            if (set == -1)
            {
                if (!_playerSelectedSet.TryGetValue(player.userID, out set))
                    set = 1;
            }

            if (!_playerDefaultSkins.TryGetValue(player.userID, out cachedskins))
            {
                cachedskins = new Dictionary<int, ulong>[3]
                {
                    new Dictionary<int, ulong>(),
                    new Dictionary<int, ulong>(),
                    new Dictionary<int, ulong>()
                };


                _playerDefaultSkins[player.userID] = cachedskins;
                ChatMessage(player.IPlayer, "NoDefaultSkins");
                return cachedskins[0];
            }

            switch (set)
            {
                case 2:
                    {
                        if (cachedskins[1] == null)
                            cachedskins[1] = new Dictionary<int, ulong>();
                        return cachedskins[1];
                    }
                case 3:
                    {
                        if (cachedskins[2] == null)
                            cachedskins[2] = new Dictionary<int, ulong>();
                        return cachedskins[2];
                    }
            }

            if (cachedskins[0] == null)
            {
                cachedskins[0] = new Dictionary<int, ulong>();
            }

            return cachedskins[0];
        }

        private void SkinAllItemsCMD(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player == null) return;
            if (!HasPerm(player.UserIDString, permskinall))
            {
                ChatMessage(iplayer, "NoPerms");
                return;
            }

            Dictionary<int, ulong> cachedskins = GetCachedSkins(player);

            if (cachedskins.Count < 1)
                return;

            if (!player.IsBuildingAuthed() && !HasPerm(player.UserIDString, permbypassauth))
            {
                ChatMessage(iplayer, "NoBuildingAuth");
                return;
            }
            BuildingPrivlidge buildingPrivlidge = player.GetBuildingPrivilege();
            if (buildingPrivlidge == null)
            {
                ChatMessage(iplayer, "NoBuildingAuth");
                return;
            }
            BuildingManager.Building buildingManager = buildingPrivlidge.GetBuilding();
            if (buildingManager == null)
            {
                ChatMessage(iplayer, "NoBuildingAuth");
                return;
            }

            //Check for cooldown
            float cdtime = 0;
            //Find shortest cd perm
            foreach (var cdperm in config.Cooldowns)
            {
                if (!HasPerm(player.UserIDString, $"skinner.{cdperm.Key}")) continue;
                if (cdtime == 0)
                    cdtime = cdperm.Value.skinall;
                else if (cdtime > cdperm.Value.skinall)
                    cdtime = cdperm.Value.skinall;
            }

            if (cdtime > 0)
            {
                if (!_playercooldowns.TryGetValue(player.userID, out CoolDowns? coolDowns))
                    _playercooldowns.Add(player.userID, new CoolDowns() { skinall = Time.time });
                else
                {
                    if (coolDowns.skinall + cdtime > Time.time)
                    {
                        ChatMessage(iplayer, "CommandCooldown", TimeSpan.FromSeconds(coolDowns.skinall + cdtime - Time.time).ToString("hh' hrs 'mm' mins 'ss' secs'"));
                        return;
                    }
                    coolDowns.skinall = Time.time;
                }
            }

            string skinned = "items";
            ItemDefinition itemdef = null;
            if (args.Length > 0)
            {
                itemdef = ItemManager.FindItemDefinition(args[0]);
                if (itemdef == null)
                {
                    ChatMessage(iplayer, "NoShortname", args[0]);
                    return;
                }
                skinned = itemdef.shortname;
            }

            foreach (var decayent in buildingManager.decayEntities)
            {
                StorageContainer storageContainer = decayent?.GetEntity() as StorageContainer;
                if (storageContainer == null) continue;
                foreach (var item in storageContainer.inventory.itemList)
                {
                    if (itemdef != null)
                    {
                        if (item.info.shortname != itemdef.shortname)
                            continue;
                    }

                    if (cachedskins.TryGetValue(item.info.itemid, out ulong uID))
                    {
                        SkinItem(item, uID);
                    }
                }
            }
            ChatMessage(iplayer, "CompletedAllSkin", skinned);
        }

        public Dictionary<BasePlayer, BoxController> _boxcontrollers = new Dictionary<BasePlayer, BoxController>();

        private void SBNextPageCMD(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player == null) return;
            if (!_viewingcon.TryGetValue(player, out BoxController boxController)) return;

            if (boxController._fillingbox || boxController._clearingbox)
                return;
            boxController.NextPage();
        }

        private void SBBackPageCMD(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player == null) return;
            if (!_viewingcon.TryGetValue(player, out BoxController boxController)) return;
            if (boxController._fillingbox || boxController._clearingbox)
                return;
            boxController.BackPage();
        }

        private void SearchCMD(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player == null) return;

            if (!_viewingcon.TryGetValue(player, out BoxController boxController)) return;

            if (boxController._fillingbox || boxController._clearingbox)
                return;

            string searchtxt = string.Join(",", args).Replace(",", " ");

            if (searchtxt.Trim().ToLower() == "search id or name")
                searchtxt = string.Empty;

            if (boxController.searchtxt == searchtxt) return;

            boxController.searchtxt = searchtxt;
            boxController.SearchUpdate();
        }

        private void SetSelectCMD(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player == null) return;
            if (args.Length != 1)
            {
                ChatMessage(iplayer, "SkinSetSelectedArgs");
                return;
            }
            int setselect = 0;

            if (!int.TryParse(args[0], out setselect))
            {
                ChatMessage(iplayer, "SkinSetSelectedArgs");
                return;
            }

            _playerSelectedSet[player.userID] = setselect;
            ChatMessage(iplayer, "SkinSetSelected", setselect);

            if (player.TryGetComponent<InventoryWatcher>(out InventoryWatcher inventoryWatcher))
            {
                inventoryWatcher.refreshSkins();
            }

            if (!_viewingcon.TryGetValue(player, out BoxController boxController)) return;

            if (boxController._fillingbox || boxController._clearingbox)
                return;

            boxController.setSelect = setselect;
            boxController.SetUpdate();
        }

        private void RequestSelectCMD(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player == null) return;
            if (args.Length != 1)
            {
                return;
            }

            if (!_viewingcon.TryGetValue(player, out BoxController boxController)) return;

            if (boxController._fillingbox || boxController._clearingbox)
                return;

            if (args[0] == "Try" || args[0] == "Approve" || args[0] == "Deny")
            {
                boxController.requestselected = args[0];
            }
            else
            {
                player.EndLooting();
                return;
            }

            boxController.SkinRequests();
        }

        #endregion Commands

        #region Hooks

        private Dictionary<BasePlayer, BoxController> _viewingcon = new Dictionary<BasePlayer, BoxController>();
        private void OnPlayerLootEnd(PlayerLoot instance)
        {
            if (!_viewingcon.TryGetValue(instance.baseEntity, out BoxController boxController)) return;
            if (boxController.boxtype == "craftskins")
            {
                if (instance.baseEntity.TryGetComponent<InventoryWatcher>(out InventoryWatcher inventoryWatcher))
                {
                    inventoryWatcher.refreshSkins();
                }
            }
            UnityEngine.Object.Destroy(boxController);
            _viewingcon.Remove(instance.baseEntity);
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (!HasPerm(player.UserIDString, permskinautotoggle))
                return;
            if (!HasPerm(player.UserIDString, permskinauto))
                return;

            if (!player.TryGetComponent<InventoryWatcher>(out InventoryWatcher inventoryWatcher))
            {
                player.gameObject.AddComponent<InventoryWatcher>();
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string strReason)
        {
            if (player.TryGetComponent<SpraycanController>(out SpraycanController spraycanController))
            {
                UnityEngine.Object.Destroy(spraycanController);
            }
            if (player.TryGetComponent<InventoryWatcher>(out InventoryWatcher inventoryWatcher))
            {
                UnityEngine.Object.Destroy(inventoryWatcher);
            }

            if (_playerUsageData.TryGetValue(player.userID, out PlayerData playerData))
                playerData.UpdateLastOnline();
        }

        private void OnItemCraftFinished(ItemCraftTask task, Item item, ItemCrafter itemCrafter)
        {
            if (task.skinID != 0)
                return;

            BasePlayer player = itemCrafter.owner;

            if (player == null) return;

            if (!HasPerm(player.UserIDString, permdefault))
                return;

            Dictionary<int, ulong> cached = GetCachedSkins(player);

            if (cached.Count < 1)
                return;

            if (!cached.TryGetValue(item.info.itemid, out ulong uID))
                return;

            SkinItem(item, uID);
        }

        private object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (item.text == "1")
            {
                item.RemoveFromContainer();
                item.Remove();
                return true;
            }
            return null;
        }

        #endregion Hooks

        #region Methods
        private static string _coffinPrefab;

        private StorageContainer CreateStorageCon(BasePlayer player)
        {
            StorageContainer storage = GameManager.server.CreateEntity(_coffinPrefab, Vector3.zero) as StorageContainer;

            storage.syncPosition = false;
            storage.limitNetworking = true;
            storage.enableSaving = false;
            storage.Spawn();
            storage.inventory.playerOwner = player;

            UnityEngine.Object.Destroy(storage.gameObject.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.Destroy(storage.gameObject.GetComponent<GroundWatch>());

            return storage;
        }

        private ItemContainer CreateContainer()
        {
            ItemContainer itemContainer = new ItemContainer()
            {
                entityOwner = null,
                allowedContents = ItemContainer.ContentsType.Generic,
            };
            itemContainer.maxStackSize = 0;
            itemContainer.ServerInitialize(null, 48);
            itemContainer.GiveUID();
            return itemContainer;
        }

        private void StartLootingCon(BasePlayer player, ItemContainer itemContainer)
        {
            if (player == null || itemContainer == null)
                return;

            StartLooting(player, itemContainer);
        }

        private void StartLooting(BasePlayer player, ItemContainer itemContainer)
        {
            if (player == null || !player.IsAlive() || !_viewingcon.ContainsKey(player)) return;

            player.inventory.loot.Clear();
            player.inventory.loot.AddContainer(itemContainer);
            player.inventory.loot.entitySource = RelationshipManager.ServerInstance;
            player.inventory.loot.PositionChecks = false;
            player.inventory.loot.MarkDirty();
            player.SendNetworkUpdateImmediate();
            RPCStartLooting(player);
        }

        private static uint RPCOpenLootstr;
        private static byte[]? RPCOpenLootbytes;

        public static void RPCStartLooting(BasePlayer player)
        {
            RpcTarget result = default;
            result.Function = "RPC_OpenLootPanel";
            result.Connections = new SendInfo(player.net.connection);

            NetWrite netWrite = Net.sv.StartWrite();
            netWrite.PacketID(Message.Type.RPCMessage);
            netWrite.EntityID(player.net.ID);
            netWrite.UInt32(RPCOpenLootstr);

            netWrite.BytesWithSize(RPCOpenLootbytes);

            netWrite.Send(result.Connections);

            if (result.UsingPooledConnections)
            {
                Pool.FreeUnmanaged<Connection>(ref result.Connections.connections);
            }
        }
        public void SkinItem(Item item, ulong uID)
        {
            int redirectitemID = 0;
            if (item.info.isRedirectOf != null)
                redirectitemID = item.info.isRedirectOf.itemid;
            if (HasMask(uID))
                redirectitemID = (int)UnsetMask(uID);
            //redirect skins
            if (redirectitemID != 0)
            {
                Item redirectitem = ItemManager.CreateByItemID(redirectitemID, item.amount, !HasMask(uID) ? uID : 0ul);

                if (redirectitem == null) return;

                redirectitem.text = "1";
                redirectitem.maxCondition = item.maxCondition;
                redirectitem.condition = item.condition;

                if (item.contents?.itemList.Count > 0)
                {
                    foreach (var con in item.contents.itemList)
                    {
                        var newCon = ItemManager.Create(con.info, con.amount, con.skin);
                        newCon.condition = con.condition;
                        newCon.maxCondition = con.maxCondition;
                        newCon.MoveToContainer(redirectitem.contents);
                        newCon.MarkDirty();
                    }
                    redirectitem.contents.MarkDirty();
                }

                if (skinner.config.applySkinNames)
                {
                    if (redirectitem.skin != 0ul && _skinNames.TryGetValue(uID, out string displayName))
                        redirectitem.name = displayName;
                }

                BaseEntity held1 = redirectitem.GetHeldEntity();
                if (held1 != null)
                {
                    BaseEntity mainheld = item.GetHeldEntity();
                    if (mainheld != null)
                    {
                        BaseProjectile mainbaseProjectile = mainheld as BaseProjectile;
                        BaseProjectile baseProjectile = held1 as BaseProjectile;
                        if (baseProjectile != null && mainbaseProjectile != null)
                        {
                            baseProjectile.primaryMagazine.contents = mainbaseProjectile.primaryMagazine.contents;
                            baseProjectile.primaryMagazine.ammoType = mainbaseProjectile.primaryMagazine.ammoType;
                        }
                    }
                    held1.skinID = redirectitem.skin;
                    held1.SendNetworkUpdate();
                }

                //Swap item

                NextTick(() => {
                    string text = redirectitem.text;
                    int pos = item.position;
                    ItemContainer parent = item.parent;
                    if (parent == null || item == null)
                    {
                        redirectitem.Remove();
                        return;
                    }

                    DoRemove(item);
                    if (!redirectitem.MoveToContainer(parent, pos, false))
                        redirectitem.Drop(parent.dropPosition, parent.dropVelocity);
                    redirectitem.text = text;
                });
            }
            else
            {
                item.skin = uID;

                if (skinner.config.applySkinNames)
                {
                    if (_skinNames.TryGetValue(uID, out string displayName))
                        item.name = displayName;
                    //item.info.displayName.english = cachedSkin.displayName;
                }

                BaseEntity held = item.GetHeldEntity();
                if (held != null)
                {
                    held.skinID = uID;
                }
                item.MarkDirty();
            }
        }

        public BaseCombatEntity? SkinDeployable(BaseCombatEntity baseCombatEntity, ulong uID)
        {
            ItemDefinition? entDef = null;
            if (baseCombatEntity.pickup.itemTarget != null)
            {
                entDef = baseCombatEntity.pickup.itemTarget;
            }
            else if (baseCombatEntity.repair.itemTarget != null)
            {
                entDef = baseCombatEntity.repair.itemTarget;
            }

            if (entDef == null)
                return null;

            if (!HasMask(uID) && entDef.isRedirectOf == null)
            {
                if (uID == baseCombatEntity.skinID)
                    return baseCombatEntity;

                baseCombatEntity.skinID = uID;
                //Skin Deployable
                if (baseCombatEntity.skinID < 100000)
                {
                    SendNetworkUpdate(baseCombatEntity);
                }
                else
                {
                    baseCombatEntity.SendNetworkUpdateImmediate();
                }

                return baseCombatEntity;
            }
            //re-createentity for redirect skin

            if (HasMask(uID))
                entDef = ItemManager.FindItemDefinition((int)UnsetMask(uID));
            else if (entDef.isRedirectOf != null)
                entDef = entDef.isRedirectOf;

            if (entDef == null)
                return null;

            if (!GetEntityPrefabPath(entDef, out string respath))
                return null;

            Vector3 vector31 = baseCombatEntity.transform.localPosition;
            Quaternion quaternion = baseCombatEntity.transform.localRotation;
            BaseEntity parentEntity = baseCombatEntity.GetParentEntity();
            float single = baseCombatEntity._health;
            EntityRef[] slots = baseCombatEntity.GetSlots();
            ulong ownerID = baseCombatEntity.OwnerID;
            float single1 = (baseCombatEntity != null ? baseCombatEntity.lastAttackedTime : 0f);
            HashSet<PlayerNameID> playerNameIDs = null;

            BuildingPrivlidge buildingPrivlidge = baseCombatEntity.GetBuildingPrivilege();
            if (buildingPrivlidge != null)
            {
                playerNameIDs = new HashSet<PlayerNameID>(buildingPrivlidge.authorizedPlayers);
            }

            bool flag1 = (baseCombatEntity is Door || baseCombatEntity is BuildingPrivlidge);

            Dictionary<ContainerSet, List<Item>> containerSets = new Dictionary<ContainerSet, List<Item>>();
            SaveEntityStorage(baseCombatEntity, containerSets, 0);

            List<ChildPreserveInfo> list = Pool.Get<List<ChildPreserveInfo>>();
            if (!flag1)
            {
                for (int i = 0; i < baseCombatEntity.children.Count; i++)
                {
                    SaveEntityStorage(baseCombatEntity.children[i], containerSets, -1);
                }
            }
            else
            {
                foreach (BaseEntity child in baseCombatEntity.children)
                {
                    ChildPreserveInfo childPreserveInfo = new ChildPreserveInfo()
                    {
                        TargetEntity = child,
                        TargetBone = child.parentBone,
                        LocalPosition = child.transform.localPosition,
                        LocalRotation = child.transform.localRotation,
                    };
                    list.Add(childPreserveInfo);
                }

                foreach (ChildPreserveInfo childPreserveInfo1 in list)
                {
                    childPreserveInfo1.TargetEntity.SetParent(null, true, false);
                }
            }

            baseCombatEntity.Kill(BaseNetworkable.DestroyMode.None);
            BaseEntity newent = GameManager.server.CreateEntity(respath,
                (parentEntity != null ? parentEntity.transform.TransformPoint(vector31) : vector31),
                (parentEntity != null ? parentEntity.transform.rotation * quaternion : quaternion), true);
            newent.SetParent(parentEntity, false, false);
            newent.transform.localPosition = vector31;
            newent.transform.localRotation = quaternion;
            newent.OwnerID = ownerID;

            if (!HasMask(uID))
                newent.skinID = uID;

            DecayEntity decayEntity = newent as DecayEntity;
            if (decayEntity != null)
            {
                decayEntity.AttachToBuilding(null);
            }

            newent.Spawn();
            BaseCombatEntity baseCombatEntity1 = newent as BaseCombatEntity;
            if (baseCombatEntity1 != null)
            {
                baseCombatEntity1.SetHealth(single);
                baseCombatEntity1.lastAttackedTime = single1;
            }

            BuildingPrivlidge buildingPrivlidge1 = newent as BuildingPrivlidge;
            if (buildingPrivlidge1 != null && playerNameIDs != null)
            {
                buildingPrivlidge1.authorizedPlayers = playerNameIDs;
            }

            if (containerSets.Count > 0)
            {
                RestoreEntityStorage(newent, 0, containerSets);
                if (!flag1)
                {
                    for (int j = 0; j < newent.children.Count; j++)
                    {
                        RestoreEntityStorage(newent.children[j], -1, containerSets);
                    }
                }

                foreach (KeyValuePair<ContainerSet, List<Item>> containerSet in containerSets)
                {
                    foreach (Item value in containerSet.Value)
                    {
                        value.Remove(0f);
                    }
                }
            }

            if (flag1)
            {
                foreach (ChildPreserveInfo child in list)
                {
                    child.TargetEntity.SetParent(baseCombatEntity1, child.TargetBone, true, false);
                    child.TargetEntity.transform.localPosition = child.LocalPosition;
                    child.TargetEntity.transform.localRotation = child.LocalRotation;
                    child.TargetEntity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                }

                baseCombatEntity1.SetSlots(slots);
            }

            Pool.FreeUnmanaged<ChildPreserveInfo>(ref list);

            return baseCombatEntity1;

        }

        #region Spraycan Code from Assembly
        private struct ChildPreserveInfo
        {
            public BaseEntity TargetEntity;

            public uint TargetBone;

            public Vector3 LocalPosition;

            public Quaternion LocalRotation;
        }

        private struct ContainerSet
        {
            public int ContainerIndex;

            public uint PrefabId;
        }

        private bool GetEntityPrefabPath(ItemDefinition def, out string resourcePath)
        {
            ItemModDeployable itemModDeployable;
            ItemModEntity itemModEntity;
            ItemModEntityReference itemModEntityReference;
            resourcePath = string.Empty;
            if (def.TryGetComponent<ItemModDeployable>(out itemModDeployable))
            {
                resourcePath = itemModDeployable.entityPrefab.resourcePath;
                return true;
            }
            if (def.TryGetComponent<ItemModEntity>(out itemModEntity))
            {
                resourcePath = itemModEntity.entityPrefab.resourcePath;
                return true;
            }
            if (!def.TryGetComponent<ItemModEntityReference>(out itemModEntityReference))
            {
                return false;
            }
            resourcePath = itemModEntityReference.entityPrefab.resourcePath;
            return true;
        }

        void SaveEntityStorage(BaseEntity baseEntity, Dictionary<ContainerSet, List<Item>> dictionary, int index)
        {
            uint num;
            IItemContainerEntity itemContainerEntity = baseEntity as IItemContainerEntity;
            if (itemContainerEntity != null)
            {
                ContainerSet containerSet = new ContainerSet()
                {
                    ContainerIndex = index
                };
                if (index == 0)
                {
                    num = 0;
                }
                else
                {
                    num = baseEntity.prefabID;
                }
                containerSet.PrefabId = num;
                ContainerSet containerSet1 = containerSet;
                if (dictionary.ContainsKey(containerSet1))
                {
                    return;
                }
                dictionary.Add(containerSet1, new List<Item>());
                foreach (Item item in itemContainerEntity.inventory.itemList)
                {
                    dictionary[containerSet1].Add(item);
                }
                foreach (Item item1 in dictionary[containerSet1])
                {
                    item1.RemoveFromContainer();
                }
            }
        }

        void RestoreEntityStorage(BaseEntity baseEntity, int index, Dictionary<ContainerSet, List<Item>> copy)
        {
            uint num;
            IItemContainerEntity itemContainerEntity = baseEntity as IItemContainerEntity;
            if (itemContainerEntity != null)
            {
                ContainerSet containerSet = new ContainerSet()
                {
                    ContainerIndex = index
                };
                if (index == 0)
                {
                    num = 0;
                }
                else
                {
                    num = baseEntity.prefabID;
                }
                containerSet.PrefabId = num;
                ContainerSet containerSet1 = containerSet;
                if (copy.ContainsKey(containerSet1))
                {
                    foreach (Item item in copy[containerSet1])
                    {
                        item.MoveToContainer(itemContainerEntity.inventory, -1, true, false, null, true);
                    }
                    copy.Remove(containerSet1);
                }
            }
        }
        #endregion Spraycan Code from Assembly

        #endregion Methods

        #region Inventory Watcher
        private class InventoryWatcher : FacepunchBehaviour
        {
            private BasePlayer player;
            bool _enabled = true;
            Dictionary<int, ulong> cachedSkins;

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                cachedSkins = skinner.GetCachedSkins(player);
                subContainerWatch();
            }

            private void subContainerWatch()
            {
                player.inventory.containerBelt.onItemAddedRemoved += skinWatch;
                player.inventory.containerWear.onItemAddedRemoved += skinWatch;
                player.inventory.containerMain.onItemAddedRemoved += skinWatch;
            }

            private void skinWatch(Item item, bool f)
            {
                if (!f || item.text == "1" || (player.inventory?.loot?.entitySource == RelationshipManager.ServerInstance)) return;

                if (cachedSkins.IsNullOrEmpty() || (item.info ?? null) == null) return;

                if (cachedSkins.TryGetValue(item.info?.isRedirectOf == null ? item.info.itemid : item.info.isRedirectOf.itemid, out ulong uID))
                {
                    skinner.SkinItem(item, uID);
                }
            }

            public void refreshSkins()
            {
                cachedSkins = skinner.GetCachedSkins(player);
            }

            private void OnDestroy()
            {
                player.inventory.containerBelt.onItemAddedRemoved -= skinWatch;
                player.inventory.containerWear.onItemAddedRemoved -= skinWatch;
                player.inventory.containerMain.onItemAddedRemoved -= skinWatch;
            }
        }
        #endregion Inventory Watcher

        #region Spraycan Controller

        private static int spraycanid;
        void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (newItem == null)
            {
                if (oldItem != null && oldItem.info?.itemid == spraycanid)
                {
                    SpraycanController spraycanController;
                    if (player.TryGetComponent<SpraycanController>(out spraycanController))
                    {
                        UnityEngine.Object.Destroy(spraycanController);
                    }
                }

                return;
            }

            if (newItem.info.itemid == spraycanid)
            {
                SpraycanController spraycanController;
                if (!player.TryGetComponent<SpraycanController>(out spraycanController))
                {
                    player.gameObject.AddComponent<SpraycanController>();
                    return;
                }
            }

            if (oldItem != null)
            {
                if (oldItem.info.itemid == spraycanid)
                {
                    SpraycanController spraycanController;
                    if (player.TryGetComponent<SpraycanController>(out spraycanController))
                    {
                        UnityEngine.Object.Destroy(spraycanController);
                    }
                }

            }
        }

        private class SpraycanController : FacepunchBehaviour
        {
            private BasePlayer player;
            private SprayCan sprayCan;
            private string skinitem = "SkinItem";

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                sprayCan = player.GetHeldEntity() as SprayCan;
                if (sprayCan == null)
                {
                    Destroy(this);
                }

                StartCheck();
            }

            void CheckForPress()
            {
                if (!player.serverInput.WasJustReleased(BUTTON.FIRE_THIRD))
                    return;

                skinner.SkinItemCMD(player.IPlayer, skinitem, null);
            }

            public void StartCheck()
            {
                InvokeRepeating(CheckForPress, 0.1f, 0.1f);
            }
            public void StopCheck()
            {
                CancelInvoke(CheckForPress);
            }
        }

        #endregion Spraycan Controller

        #region Box Controller
        //black magic
        public Queue<Item> itemPool = new Queue<Item>();
        public class BoxController : FacepunchBehaviour
        {
            public ItemContainer inventory;
            private BasePlayer player;
            private Item? mainitem = null;
            private Item? returnitem = null;
            private Item? returnitemplayer = null;

            private Vector3 ogpos;
            public bool _fillingbox = false;
            public bool _clearingbox = false;
            private ItemDefinition? itemselected = null;
            public BaseCombatEntity? maindeployable = null;
            public string searchtxt = string.Empty;
            public int setSelect = 1;
            public string requestselected = "Try";

            public Dictionary<int, ulong> setSkins;

            bool rebuildsearchUI = false;
            bool rebuildPageUI = false;

            bool fsearchUI = false;
            bool fpageUI = false;
            bool fskinSetsUI = false;
            bool fskinRequestsUI = false;

            public string boxtype = string.Empty;

            private int page = 0;
            private int scpage = 0;

            public void StartAwake()
            {
                player = GetComponent<BasePlayer>();

                ogpos = player.transform.position;

                //disable stacks
                inventory.maxStackSize = 1;
                inventory.onPreItemRemove = Preremove;
            }

            private void Preremove(Item item)
            {
                if (item.amount == 0)
                {
                    bool flag1 = false;

                    foreach (var item2 in inventory.itemList)
                    {
                        if (item2.amount <= 1)
                            continue;
                        flag1 = true;
                        item2.amount = 1;
                    }
                    if (!flag1)
                    {
                        List<Item>? items = GetPlayerItems(item.info.itemid);
                        if (items != null)
                        {
                            foreach (var item1 in items)
                            {
                                if (item1.skin != item.skin) continue;
                                item1.amount -= 1;
                                if (item1.amount < 1)
                                    DoRemove(item1);
                                else
                                    item1.MarkDirty();
                                flag1 = false;
                                break;
                            }

                            if (flag1 && items.Count > 0)
                            {
                                Item item2 = items[0];
                                item2.amount -= 1;
                                if (item2.amount < 1)
                                    DoRemove(item2);
                                else
                                    item2.MarkDirty();
                            }
                            Pool.FreeUnmanaged(ref items);
                        }
                    }
                }

                if (boxtype != "deployableskins" && boxtype != "itemskins")
                    return;

                int itempos = item.position;
                int usagecnt = usageskins == null ? 0 : usageskins.Count;

                if (cachedskins == null || item.position < 0)
                {
                    mainitem = null;
                    return;
                }

                // for inserting new item
                if (posWatchItem != null && item.info.itemid != posWatchItem?.info.itemid && posWatchItem?.parent == null && !posWatchItem.IsDroppedInWorld(true))
                {
                    return;
                }

                //block item insert
                if (item.text == "1")
                {
                    InsertItem(GetMask(item.skin, item.info.itemid, item.info.isRedirectOf != null), itemselected, 1, item.position);
                }

                posWatchItem = null;
                bool isredirect = item.info.isRedirectOf != null;
                if (item.skin != 0 || isredirect)
                {
                    if (!skinner._playerUsageData.TryGetValue(player.userID, out PlayerData? playerData))
                    {
                        skinner._playerUsageData[player.userID] = new PlayerData();
                    }
                    skinner._playerUsageData[player.userID].AddSkinUsage(item.skin, isredirect ? item.info.isRedirectOf.itemid : item.info.itemid, isredirect ? item.info.itemid : 0);
                }
            }

            #region Skin Deployables

            public void SkinDeplyoables(BaseCombatEntity entity, ItemDefinition itemDefinition)
            {
                boxtype = "deployableskins";

                inventory.onItemAddedRemoved = CheckforItemDply;
                inventory.SetFlag(ItemContainer.Flag.IsLocked, true);
                maindeployable = entity;
                rebuildsearchUI = true;
                itemselected = itemDefinition.isRedirectOf != null ? itemDefinition.isRedirectOf : itemDefinition;
                GetDeployableSkins();
            }

            private void CheckforItemDply(Item item, bool b)
            {
                //if item added
                if (b)
                {
                    if (_fillingbox)
                        return;

                    if (item == returnitem)
                    {
                        returnitem = null;
                        return;
                    }

                    returnitemplayer = item;
                    GiveItem(returnitemplayer);
                    return;
                }

                //item removed
                if (_clearingbox || _fillingbox)
                    return;

                searchtxt = string.Empty;
                if (maindeployable == null)
                {
                    item.Remove(0f);
                    player.EndLooting();
                    return;
                }

                if (item == returnitemplayer)
                {
                    returnitemplayer = null;
                    return;
                }

                bool isRedirect = item.info.isRedirectOf != null;
                ulong uID = GetMask(item.skin, item.info.itemid, isRedirect);
                maindeployable = skinner.SkinDeployable(maindeployable, uID);

                ////Spray Can Effects
                if (skinner.config.sprayCanEffect)
                {
                    SprayCan can = player.GetHeldEntity() as SprayCan;
                    if (can != null)
                        can.ClientRPC<int, ulong>(null, "Client_ReskinResult", 1, maindeployable.net.ID.Value);
                }

                returnitem = item;

                item.Remove(0f);
            }

            private void GetDeployableSkins(bool skipchecks = false)
            {
                if (!skipchecks || cachedskins == null)
                {
                    if (!PrepareSkins(itemselected))
                        return;
                }

                //Fill container
                FillSkins(itemselected);

                inventory.SetFlag(ItemContainer.Flag.IsLocked, false);
                inventory.SetFlag(ItemContainer.Flag.NoItemInput, false);

                inventory.MarkDirty();
            }

            private void FillSkins(ItemDefinition itemdef, bool bbreak = true)
            {
                _fillingbox = true;

                if (inventory.itemList.Count > 0)
                    ClearCon();

                int i = 0;
                int usagecnt = usageskins == null ? 0 : usageskins.Count;
                int maxIndex = Math.Min(48, cachedskins.Count + usagecnt - 48 * page);

                for (i = 0; i < maxIndex; i++)
                {
                    ulong cachedskin = i >= usagecnt ? cachedskins[i + (48 * page) - usagecnt] : usageskins[usagecnt - i - 1];
                    InsertItem(cachedskin, itemdef, 1, i);
                }
                _fillingbox = false;

            }

            #endregion Skin Deployables

            #region Skin Items

            private int olditempos;
            private ItemContainer oldcon;
            private Item? posWatchItem;
            private Item? backpack;
            public void StartItemSkin()
            {
                boxtype = "itemskins";
                //enable stacks
                inventory.maxStackSize = 0;

                inventory.onItemAddedRemoved = CheckforItem;

                inventory.onItemAddedToStack = OnItemAddedToStack;

                player.inventory.containerMain.onPreItemRemove += PosWatch;
                player.inventory.containerWear.onPreItemRemove += PosWatch;
                player.inventory.containerBelt.onPreItemRemove += PosWatch;

                backpack = player.inventory.containerWear?.GetSlot(ItemContainer.BackpackSlotIndex) ?? null;

                if (backpack?.contents == null || backpack.contents.itemList.IsNullOrEmpty())
                    return;

                backpack.contents.onPreItemRemove += PosWatch;
            }

            private void OnItemAddedToStack(Item item, int amount)
            {
                item.amount += amount;
                Item newitem = item.SplitItem(amount);
                skinner.NextTick(() => {
                    if (item.amount > 1)
                    {
                        mainitem = GiveItem(newitem);
                        rebuildsearchUI = true;
                    }
                    GetSkins();
                });
            }

            private void PosWatch(Item item)
            {
                if (item.text == "1")
                {
                    return;
                }

                if (backpack != null && item.parent == backpack?.contents)
                {
                    if (backpack.position != ItemContainer.BackpackSlotIndex || player.inventory.containerWear != backpack.parent)
                    {
                        backpack.contents.onPreItemRemove -= PosWatch;
                        backpack = null;
                        return;
                    }
                }
                olditempos = item.position;
                oldcon = item.parent;
                posWatchItem = item;
            }

            private void CheckforItem(Item item, bool b)
            {
                //if item removed
                if (!b)
                {
                    if (_clearingbox || _fillingbox || item == mainitem)
                        return;

                    if (posWatchItem != null && posWatchItem != mainitem)
                    {
                        item.Remove();
                        return;
                    }
                    if (mainitem == null || (mainitem?.parent?.playerOwner ?? null) != player && !(backpack != null && mainitem?.parent == backpack?.contents))
                    {
                        item.Remove();
                        ResetCon();
                        return;
                    }

                    ItemRemoveCheck(item);
                    return;
                }

                //if item added
                if (_fillingbox)
                    return;

                if (item.text == "1")
                {
                    ClearCon();
                    GetSkins();
                    return;
                }

                if (mainitem != null)
                {
                    rebuildPageUI = true;
                    if (item.uid != mainitem.uid)
                    {
                        if (item.info.itemid != mainitem.info.itemid)
                        {
                            if (!string.IsNullOrEmpty(searchtxt))
                                rebuildsearchUI = true;

                            page = 0;
                            searchtxt = string.Empty;
                            cachedskins = null;
                        }
                    }
                }
                else
                {
                    rebuildsearchUI = true;
                }

                mainitem = item;
                //if (!item.MoveToContainer(oldcon, olditempos))
                //    GiveItem(item);

                for (int i = 0; i < inventory.itemList.Count; i++)
                {
                    if (inventory.itemList[i].uid.Value != item.uid.Value)
                        continue;

                    inventory.itemList.RemoveAt(i);
                    break;
                }

                if (oldcon != null && posWatchItem?.uid == item.uid)
                {
                    item.position = olditempos;

                    oldcon.itemList.Add(item);
                    item.parent = oldcon;

                    item.MarkDirty();

                    oldcon.onItemAddedRemoved(item, true);
                }
                else
                {
                    mainitem = GiveItem(item);
                }
                //----------------------//

                //olditempos = -1;
                //oldcon = null;

                //if inv is full add extra slot
                if (player.inventory.containerMain.itemList.Count == player.inventory.containerMain.capacity && player.inventory.containerBelt.itemList.Count == player.inventory.containerBelt.capacity)
                {
                    player.inventory.containerMain.capacity = 25;
                }

                if (mainitem.info.stackable > 1 || mainitem.MaxStackable() > 1)
                    inventory.maxStackSize = 1;
                else
                    inventory.maxStackSize = 0;

                usageskins = null;

                inventory.capacity = 48;

                GetSkins();

            }

            private List<ulong>? cachedskins;
            private List<ulong>? usageskins;
            private void GetSkins(bool skipchecks = false)
            {
                if (mainitem == null)
                {
                    ClearCon();
                    return;
                }

                itemselected = mainitem.info.isRedirectOf == null ? mainitem.info : mainitem.info.isRedirectOf;

                if (!skipchecks || cachedskins == null)
                {
                    if (!PrepareSkins(itemselected))
                        return;
                }

                //Fill container
                FillSkins(itemselected);

                inventory.SetFlag(ItemContainer.Flag.IsLocked, false);
                inventory.SetFlag(ItemContainer.Flag.NoItemInput, false);

                inventory.MarkDirty();

                inventory.capacity = 49;
            }

            private void ResetCon()
            {
                //skinner.ChatMessage(player.IPlayer, "NoSkins");
                ClearCon();
                cachedskins = null;

                if (fsearchUI)
                    fsearchUI = UI.DestroyUI(player, BSkinSearchUI);

                if (fpageUI)
                    fpageUI = UI.DestroyUI(player, BSkinPageUI);

                inventory.SetFlag(ItemContainer.Flag.NoItemInput, false);
                inventory.SetFlag(ItemContainer.Flag.IsLocked, false);
                mainitem = null;
                inventory.maxStackSize = 0;
            }

            private bool searchtextwas = false;
            private bool PrepareSkins(ItemDefinition itemdef)
            {
                //Blacklisted Skin
                if (IsMainItemBlacklisted(mainitem) || IsMainDeployableBlacklisted(maindeployable))
                {
                    ResetCon();
                    return false;
                }

                //No Skins Found
                if (cachedskins == null)
                {
                    if (!_cachedSkins.TryGetValue(itemdef.itemid, out cachedskins))
                    {
                        ResetCon();
                        return false;
                    }

                    searchtextwas = false;
                }

                //Get Skins List
                if (!string.IsNullOrEmpty(searchtxt))
                {
                    if (searchtextwas)
                    {
                        _cachedSkins.TryGetValue(itemdef.itemid, out cachedskins);
                    }

                    List<ulong> cachedskins2 = new List<ulong>();

                    foreach (var cachedSkin in cachedskins)
                    {
                        if (!_skinNames.TryGetValue(cachedSkin, out string displayName))
                            continue;

                        if (displayName.Contains(searchtxt, StringComparison.CurrentCultureIgnoreCase) || cachedSkin.ToString().Contains(searchtxt))
                        {
                            cachedskins2.Add(cachedSkin);
                        }
                    }
                    if (cachedskins2.Count > 0)
                        cachedskins = cachedskins2;

                    searchtextwas = true;
                }

                //try sort skins here
                else if ((boxtype == "itemskins" || boxtype == "deployableskins") && skinner._playerUsageData.TryGetValue(player.userID, out PlayerData playerData))
                {
                    var a = playerData.GetSkinUsage(itemdef.itemid);
                    if (a != null)
                        usageskins = new List<ulong>(a);
                }

                if (page > (cachedskins.Count - 1) / 48)
                    page = 0;

                if (page < 0)
                    page = (cachedskins.Count - 1) / 48;

                //Search Bar UI
                if (skinner.config.searchbar && rebuildsearchUI)
                {
                    if (fsearchUI)
                        fsearchUI = UI.DestroyUI(player, BSkinSearchUI);

                    fsearchUI = UI.AddUI(player, skinner.AddSearchUI(searchtxt));
                    rebuildsearchUI = false;
                }

                //Add UI on first fill
                if (inventory.itemList.Count == 0 || rebuildPageUI)
                {
                    if (fpageUI)
                        fpageUI = UI.DestroyUI(player, BSkinPageUI);

                    //Check for UI
                    if (cachedskins.Count > 48)
                    {
                        fpageUI = UI.AddUI(player, skinner.AddPageUI(page + 1, (cachedskins.Count - 1) / 48 + 1));
                    }
                }
                return true;
            }

            public void QuickRemove(Item item)
            {
                if (item.parent != null)
                {
                    inventory.itemList.Remove(item);
                }
                item.parent = null;
                item.position = 0;

                skinner.itemPool.Enqueue(item);
            }


            private void ItemRemoveCheck(Item item)
            {
                if (mainitem?.parent == null)
                {
                    item.Remove(0);
                    ResetCon();
                    return;
                }

                //quick skin
                if (item.info.itemid == mainitem.info.itemid)
                {
                    mainitem.skin = item.skin;

                    if (skinner.config.applySkinNames)
                    {
                        mainitem.name = item.name;
                        //mainitem.info.displayName.english = item.info.displayName.english;
                    }
                    item.Remove(0f);

                    BaseEntity held1 = mainitem.GetHeldEntity();
                    if (held1 != null)
                    {
                        held1.skinID = mainitem.skin;
                        if (player.svActiveItemID == mainitem.uid)
                        {
                            skinner.SendNetworkUpdate(held1);
                        }
                    }
                    mainitem.MarkDirty();

                    //update last used skins values here
                    if (!skinner._playerUsageData.ContainsKey(player.userID))
                    {
                        skinner._playerUsageData[player.userID] = new PlayerData();
                    }
                    return;
                }

                //logic
                if ((item.info.isRedirectOf != null ? item.info.isRedirectOf.itemid : item.info.itemid) != (mainitem.info.isRedirectOf != null ? mainitem.info.isRedirectOf.itemid : mainitem.info.itemid))
                {
                    item.Remove();
                    ResetCon();
                    return;
                }

                Item newitem = ItemManager.CreateByItemID(item.info.itemid, mainitem.amount, item.skin);

                newitem.maxCondition = mainitem.maxCondition;
                newitem.condition = mainitem.condition;
                newitem.info.name = name;
                newitem.text = mainitem.text;

                item.Remove();

                if (mainitem.contents?.itemList.Count > 0)
                {
                    foreach (var con in mainitem.contents.itemList)
                    {
                        var newCon = ItemManager.Create(con.info, con.amount, con.skin);
                        newCon.condition = con.condition;
                        newCon.maxCondition = con.maxCondition;
                        newCon.MoveToContainer(newitem.contents);
                        newCon.MarkDirty();
                    }
                    newitem.contents.MarkDirty();
                }

                BaseEntity held = newitem.GetHeldEntity();
                if (held != null)
                {
                    BaseEntity mainheld = mainitem.GetHeldEntity();
                    if (mainheld != null)
                    {
                        BaseProjectile mainbaseProjectile = mainheld as BaseProjectile;
                        BaseProjectile baseProjectile = held as BaseProjectile;
                        if (baseProjectile != null && mainbaseProjectile != null)
                        {
                            baseProjectile.canUnloadAmmo = true;
                            baseProjectile.primaryMagazine.contents = mainbaseProjectile.primaryMagazine.contents;
                            baseProjectile.primaryMagazine.ammoType = mainbaseProjectile.primaryMagazine.ammoType;
                        }
                    }
                }

                var parContainer = mainitem.parent;
                var parPos = mainitem.position;
                bool flag1 = posWatchItem?.uid == mainitem.uid;

                mainitem.Remove(0f);
                inventory.SetLocked(true);
                posWatchItem = null;

                skinner.NextTick(() =>
                {
                    DoRemove(mainitem);
                    DoRemove(item);
                    if (parPos == 24 && parContainer == player.inventory.containerMain)
                    {
                        ResetCon();
                        newitem.Drop(player.eyes.position, player.eyes.HeadForward() * 2, player.eyes.rotation);
                    }
                    else
                    {
                        newitem.position = parPos;
                        newitem.parent = parContainer;
                        parContainer.itemList.Add(newitem);
                        newitem.MarkDirty();
                        parContainer.onItemAddedRemoved(newitem, true);
                    }
                    newitem.RecalulateParentEntity(true);
                    posWatchItem = null;
                    mainitem = newitem;
                });

                Invoke(() => {
                    inventory.SetLocked(false);
                }, 0.5f);
            }

            #endregion Skin Items

            #region Set Default Skins

            public void SkinCraft()
            {
                boxtype = "craftskins";

                setSelect = skinner._playerSelectedSet.GetValueOrDefault(player.userID);

                setSkins = skinner.GetCachedSkins(player);

                //if inv is full add extra slot
                if (player.inventory.containerMain.itemList.Count == player.inventory.containerMain.capacity && player.inventory.containerBelt.itemList.Count == player.inventory.containerBelt.capacity)
                {
                    player.inventory.containerMain.capacity = 25;
                }

                GetDefaultSkins();
            }

            private void GetDefaultSkins()
            {
                if (fskinSetsUI)
                    fskinSetsUI = UI.DestroyUI(player, BSkinSetsSelectUI);
                fskinSetsUI = UI.AddUI(player, skinner.AddSetsUI(setSelect));

                if (fsearchUI)
                    fsearchUI = UI.DestroyUI(player, BSkinSearchUI);
                //inventory.SetFlag(ItemContainer.Flag.IsLocked, true);

                if (scpage > (_cachedSkins.Count - 1) / 48)
                    scpage = 0;

                if (scpage < 0)
                    scpage = (_cachedSkins.Count - 1) / 48;


                //Add UI on first fill
                if (inventory.itemList.Count == 0 || rebuildPageUI)
                {
                    if (fpageUI)
                        fpageUI = UI.DestroyUI(player, BSkinPageUI);
                    fpageUI = UI.AddUI(player,
                        skinner.AddPageUI(scpage + 1, (_cachedSkins.Count - 1) / 48 + 1));
                }

                int i = 0;
                _fillingbox = true;
                int maxIndex = Math.Min(48, _cachedSkins.Count - 48 * scpage);

                for (i = 0; i < 48 && i < _cachedSkins.Count - (48) * scpage; i++)
                {
                    if (!setSkins.TryGetValue(_cachedSkinKeys[i + 48 * scpage], out ulong cachedSkin))
                    {
                        cachedSkin = 0ul;
                    }

                    ItemDefinition itemDefinition = ItemManager.FindItemDefinition(_cachedSkinKeys[i + 48 * scpage]);
                    InsertItem(cachedSkin, itemDefinition, 1, i);
                }

                _fillingbox = false;

                //inventory.onItemAddedRemoved -= CheckforSkinSelect;
                inventory.onItemAddedRemoved = CheckforItemSelect;

                inventory.SetFlag(ItemContainer.Flag.IsLocked, false);
                inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);

                inventory.MarkDirty();
            }

            private void CheckforItemSelect(Item item, bool b)
            {
                //if item added
                if (b)
                {
                    if (_fillingbox)
                        return;

                    if (item == returnitem)
                    {
                        returnitem = null;
                        return;
                    }

                    returnitemplayer = item;
                    GiveItem(returnitemplayer);
                    return;
                }

                //item removed
                if (_clearingbox || _fillingbox)
                    return;

                if (item == returnitemplayer)
                {
                    returnitemplayer = null;
                    return;
                }

                itemselected = item.info.isRedirectOf != null ? item.info.isRedirectOf : item.info;
                item.Remove(0f);

                cachedskins = null;
                rebuildsearchUI = true;

                if (!PrepareSkins(itemselected))
                {
                    player.ChatMessage("error in prepare skins");
                    player.EndLooting();
                    //error prepare skins
                    return;
                }

                //Fill container
                FillSkins(itemselected, false);

                inventory.onItemAddedRemoved = CheckforSkinSelect;
                inventory.SetFlag(ItemContainer.Flag.IsLocked, false);
                inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);
                inventory.MarkDirty();
            }

            private void CheckforSkinSelect(Item item, bool b)
            {
                //if item added
                if (b)
                {
                    if (_fillingbox)
                        return;

                    if (item == returnitem)
                    {
                        returnitem = null;
                        return;
                    }

                    returnitemplayer = item;
                    GiveItem(returnitemplayer);
                    return;
                }

                //item removed
                if (_clearingbox || _fillingbox)
                    return;

                if (item == returnitemplayer)
                {
                    returnitemplayer = null;
                    return;
                }

                int itemID = item.info.isRedirectOf != null ? item.info.isRedirectOf.itemid : item.info.itemid;

                setSkins[itemID] = GetMask(item.skin, item.info.itemid, item.info.isRedirectOf != null);

                item.Remove(0f);
                ClearCon();
                itemselected = null;
                GetDefaultSkins();

            }

            #endregion  Set Default Skins

            #region SkinRequests
            public void SkinRequests()
            {
                boxtype = "skinrequests";

                inventory.onItemAddedRemoved = CheckforRequestSelect;

                inventory.SetFlag(ItemContainer.Flag.IsLocked, false);
                inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);

                GetRequestSkins();
            }

            private void GetRequestSkins()
            {
                if (skinner._requestsData.Count == 0)
                {
                    player.EndLooting();
                    return;
                }

                if (inventory.itemList.Count > 0)
                    ClearCon();

                if (fskinRequestsUI)
                    fskinRequestsUI = UI.DestroyUI(player, BSkinRequestsUI);
                fskinRequestsUI = UI.AddUI(player, skinner.AddRequestsUI(requestselected));

                if (page > (skinner._requestsData.Count - 1) / 48)
                    page = 0;

                if (page < 0)
                    page = (skinner._requestsData.Count - 1) / 48;

                //Add UI on first fill
                if (inventory.itemList.Count == 0 || rebuildPageUI)
                {
                    if (fpageUI)
                        fpageUI = UI.DestroyUI(player, BSkinPageUI);

                    if (skinner._requestsData.Count > 48)
                        fpageUI = UI.AddUI(player, skinner.AddPageUI(page + 1, (skinner._requestsData.Count - 1) / 48 + 1));
                }

                int i = 0;
                _fillingbox = true;

                int maxIndex = Math.Min(48, skinner._requestsData.Count - 48 * page);

                for (i = 0; i < 48 && i < skinner._requestsData.Count - (48) * page; i++)
                {
                    RequestItem requestitem = skinner._requestsData[i + 48 * page];
                    ItemDefinition itemDef = ItemManager.FindItemDefinition(requestitem.itemID);
                    InsertItem(requestitem.skinID, itemDef, 1, i);
                }

                _fillingbox = false;

                inventory.MarkDirty();
            }

            private void CheckforRequestSelect(Item item, bool b)
            {
                //if item added
                if (b)
                {
                    if (_fillingbox)
                        return;

                    if (item == returnitem)
                    {
                        returnitem = null;
                        return;
                    }

                    returnitemplayer = item;
                    GiveItem(returnitemplayer);
                    return;
                }

                //item removed
                if (_clearingbox || _fillingbox)
                    return;

                if (item == returnitemplayer)
                {
                    returnitemplayer = null;
                    return;
                }

                itemselected = item.info.isRedirectOf != null ? item.info.isRedirectOf : item.info;
                ulong skinselected = item.skin;

                switch (requestselected)
                {
                    case "Try":
                        if (skinner.HasPerm(player.UserIDString, permskintry))
                        {
                            Item? tryitem = ItemManager.CreateByItemID(itemselected.itemid, 1, skinselected);
                            if (tryitem != null)
                            {
                                tryitem.name = item.name;
                                GiveItem(tryitem, false);
                            }
                        }
                        else
                        {
                            skinner.ChatMessage(player.IPlayer, "NoPerms");
                        }

                        break;
                    case "Approve":
                        skinner.config.ImportedSkinList[skinselected] = new ImportedItem()
                        { itemDisplayname = itemselected.displayName.translated, itemShortname = itemselected.shortname };
                        for (int i = skinner._requestsData.Count - 1; i >= 0; i--)
                        {
                            if (skinner._requestsData[i].skinID == skinselected)
                                skinner._requestsData.RemoveAt(i);
                        }
                        break;
                    case "Deny":
                        for (int i = skinner._requestsData.Count - 1; i >= 0; i--)
                        {
                            if (skinner._requestsData[i].skinID == skinselected)
                                skinner._requestsData.RemoveAt(i);
                        }
                        break;
                }
                item.Remove(0f);
                itemselected = null;

                ClearCon();
                GetRequestSkins();
            }
            #endregion SkinRequests

            #region UI
            public void NextPage()
            {
                rebuildPageUI = true;
                inventory.SetFlag(ItemContainer.Flag.IsLocked, true);

                switch (boxtype)
                {
                    case "itemskins":
                        page += 1;
                        GetSkins();
                        return;
                    case "deployableskins":
                        page += 1;
                        GetDeployableSkins();
                        return;
                    case "skinrequests":
                        page += 1;
                        GetRequestSkins();
                        return;
                    case "craftskins":

                        if (itemselected == null)
                        {
                            scpage += 1;
                            ClearCon();
                            inventory.onItemAddedRemoved = null; // -= CheckforItemSelect;
                            GetDefaultSkins();
                            return;
                        }
                        page += 1;
                        Item dummy = new Item
                        {
                            isServer = true,
                            info = itemselected,
                            amount = 1,
                            uid = new ItemId(Net.sv.TakeUID())
                        };

                        inventory.onItemAddedRemoved = null;//CheckforSkinSelect;
                        CheckforItemSelect(dummy, false);
                        return;
                }
            }

            public void BackPage()
            {
                rebuildPageUI = true;

                inventory.SetFlag(ItemContainer.Flag.IsLocked, true);

                switch (boxtype)
                {
                    case "itemskins":
                        page -= 1;
                        GetSkins();
                        return;
                    case "deployableskins":
                        page -= 1;
                        GetDeployableSkins();
                        return;
                    case "skinrequests":
                        page -= 1;
                        GetRequestSkins();
                        return;
                    case "craftskins":

                        if (itemselected == null)
                        {
                            scpage -= 1;
                            ClearCon();
                            inventory.onItemAddedRemoved = null; // CheckforItemSelect;
                            GetDefaultSkins();
                            return;
                        }
                        page -= 1;
                        Item dummy = new Item
                        {
                            isServer = true,
                            info = itemselected,
                            amount = 1,
                            uid = new ItemId(Net.sv.TakeUID())
                        };

                        inventory.onItemAddedRemoved = null;// CheckforSkinSelect;
                        CheckforItemSelect(dummy, false);
                        return;
                }

            }

            public void SetUpdate()
            {
                if (boxtype != "craftskins") return;
                ClearCon();
                inventory.onItemAddedRemoved = null; //CheckforItemSelect;
                inventory.SetFlag(ItemContainer.Flag.IsLocked, true);
                inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);

                setSkins = skinner.GetCachedSkins(player);
                GetDefaultSkins();
            }
            public void SearchUpdate()
            {
                rebuildsearchUI = true;
                page = 0;

                if (searchtextwas)
                    cachedskins = null;

                switch (boxtype)
                {
                    case "itemskins":
                        GetSkins();
                        return;
                    case "deployableskins":
                        GetDeployableSkins();
                        return;
                    case "craftskins":

                        if (itemselected == null)
                        {
                            ClearCon();
                            inventory.onItemAddedRemoved = null;//CheckforItemSelect;
                            GetDefaultSkins();
                            return;
                        }

                        Item dummy = new Item
                        {
                            isServer = true,
                            info = itemselected,
                            amount = 1,
                            uid = new ItemId(Net.sv.TakeUID())
                        };

                        inventory.onItemAddedRemoved = null; // CheckforSkinSelect;
                        CheckforItemSelect(dummy, false);
                        return;
                }
            }

            #endregion UI

            #region Helpers

            bool IsMainItemBlacklisted(Item mainitem)
            {
                return mainitem != null &&
                       (skinner.config.blacklistedskins.Contains(mainitem.skin) ||
                        skinner.config.blacklisteditems.Contains(mainitem.info.itemid));
            }

            bool IsMainDeployableBlacklisted(BaseCombatEntity maindeployable)
            {
                return maindeployable != null && skinner.config.blacklistedskins.Contains(maindeployable.skinID);
            }
            private Item GiveItem(Item item, bool drop = true)
            {
                List<Item> allitems = Pool.Get<List<Item>>();
                player.inventory.GetAllItems(allitems);

                for (int i = allitems.Count - 1; i >= 0; i--)
                {
                    Item invitem = allitems[i];
                    if (invitem == null) continue;

                    if (invitem.info.itemid != item.info.itemid || invitem.skin != item.skin)
                        continue;

                    //Should work for StackModifier(CanStack) and StackController
                    if (invitem.CanStack(item))
                    {
                        invitem.amount += item.amount;
                        invitem.MarkDirty();

                        item.Remove();
                        Pool.FreeUnmanaged(ref allitems);
                        return invitem;
                    }
                }
                Pool.FreeUnmanaged(ref allitems);

                if (item.MoveToContainer(player.inventory.containerMain, -1))
                {
                    return item;
                }
                if (item.MoveToContainer(player.inventory.containerBelt, -1))
                {
                    return item;
                }
                if (drop)
                    item.Drop(player.IsAlive() ? player.eyes.position : ogpos, player.inventory.containerMain.dropVelocity, new Quaternion());
                else
                    item.Remove();

                return item;
            }

            private void Remove(Item item, bool nextTick = false)
            {
                if (item == null)
                {
                    //player.ChatMessage("mainitem null");
                    return;
                }
                if (nextTick)
                {
                    skinner.NextTick(() =>
                    {
                        Remove(item);
                    });
                    return;
                }
                DoRemove(item);
            }

            private void DoRemove(Item item)
            {
                if (item.uid.Value > 0 && Net.sv != null)
                {
                    Net.sv.ReturnUID(item.uid.Value);
                    item.uid.Value = 0;
                }
                if (item.contents != null)
                {
                    item.contents.Kill();
                    item.contents = null;
                }

                item.RemoveFromWorld();

                if (item.parent != null)
                {
                    for (int i = item.parent.itemList.Count - 1; i >= 0; i--)
                    {
                        Item iteminList = item.parent.itemList[i];

                        if (iteminList.uid.Value != item.uid.Value)
                            continue;

                        item.parent.itemList.RemoveAt(i);
                        break;
                    }
                    item.parent.MarkDirty();
                    item.parent.onItemAddedRemoved(item, false);
                    item.parent = null;
                }

                BaseEntity heldEntity = item.GetHeldEntity();
                if (heldEntity != null)
                {
                    heldEntity.Kill();
                }

                if (item == mainitem)
                    mainitem = null;
            }
            private void ClearCon(bool nexttick = false)
            {
                if (nexttick)
                {
                    skinner.NextTick(() => ClearCon());
                    return;
                }

                if (_clearingbox)
                    return;

                inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);
                inventory.SetFlag(ItemContainer.Flag.IsLocked, true);

                _clearingbox = true;

                for (int i = inventory.itemList.Count - 1; i >= 0; i--)
                {
                    Item item = inventory.itemList[i];

                    if (item == null)
                        continue;

                    if (item.removeTime > 0)
                    {
                        item.Remove();
                        continue;
                    }

                    item.parent = null;
                    item.position = 0;

                    skinner.itemPool.Enqueue(item);
                }

                inventory.itemList.Clear();

                inventory.MarkDirty();

                _clearingbox = false;
                inventory.SetFlag(ItemContainer.Flag.NoItemInput, false);
                inventory.SetFlag(ItemContainer.Flag.IsLocked, false);
            }


            private Item GetItemFromPool(ulong cachedSkin, ItemDefinition itemDef, int pos = -1)
            {
                bool hasmask = HasMask(cachedSkin);
                // Get redirect item
                if (hasmask)
                    itemDef = ItemManager.FindItemDefinition((int)UnsetMask(cachedSkin));

                if (!skinner.itemPool.TryDequeue(out Item item))
                {
                    item = new Item
                    {
                        isServer = true,
                        info = itemDef,
                        amount = 1,
                        skin = hasmask ? 0ul : cachedSkin,
                        text = "1",
                        uid = new ItemId(Net.sv.TakeUID()),
                        position = pos,
                        parent = inventory,
                    };

                    item.condition = (item.maxCondition = itemDef.condition.max);
                    return item;
                }

                item.amount = 1;
                item.info = itemDef;
                item.condition = (item.maxCondition = itemDef.condition.max);
                item.skin = hasmask ? 0ul : cachedSkin;
                item.position = pos;
                item.parent = inventory;
                item.name = string.Empty;

                return item;
            }

            private void InsertItem(ulong cachedSkin, ItemDefinition? itemDef, int amount = 1, int pos = -1)
            {
                Item item = GetItemFromPool(cachedSkin, itemDef, pos);
                //stops stacking

                //item.SetFlag(Item.Flag.IsOn, true);
                item.busyTime = Single.MaxValue;

                inventory.itemList.Add(item);
                if (skinner.config.applySkinNames && _skinNames.TryGetValue(cachedSkin, out string displayName))
                {
                    item.name = displayName;
                    //item.info.displayName.english = cachedSkin.displayName;
                }
            }

            private List<Item>? GetPlayerItems(int itemid = 0)
            {
                List<Item> items = Pool.Get<List<Item>>();
                player.inventory.GetAllItems(items);

                if (backpack?.contents != null && !backpack.contents.itemList.IsNullOrEmpty())
                    items.AddRange(backpack.contents.itemList);

                if (itemid != 0)
                    return items.FindAll(x => x.info.itemid == itemid);
                return items;
            }

            public void OnDestroy()
            {
                _clearingbox = true;

                player.inventory.containerMain.onPreItemRemove -= PosWatch;
                player.inventory.containerWear.onPreItemRemove -= PosWatch;
                player.inventory.containerBelt.onPreItemRemove -= PosWatch;
                if (backpack != null && backpack.contents != null)
                    backpack.contents.onPreItemRemove -= PosWatch;

                if (player.inventory.containerMain.capacity == 25)
                {
                    Item item25 = player.inventory.containerMain.GetSlot(24);

                    if (item25 != null)
                    {
                        if (item25.text != "1")
                        {
                            item25.DropAndTossUpwards(player.eyes.transform.position);
                        }
                        else
                        {
                            DoRemove(item25);
                        }
                    }

                    player.inventory.containerMain.capacity = 24;
                }

                ClearCon();

                inventory.Kill();

                if (fpageUI)
                    UI.DestroyUI(player, BSkinPageUI);
                if (fsearchUI)
                    UI.DestroyUI(player, BSkinSearchUI);
                if (fskinSetsUI)
                    UI.DestroyUI(player, BSkinSetsSelectUI);
                if (fskinRequestsUI)
                    UI.DestroyUI(player, BSkinRequestsUI);

                if (boxtype == "skinrequests")
                    skinner.UpdateImportedSkins();

                player.EndLooting();
                //Destroy(this);
            }
            #endregion Helpers
        }

        #endregion Controller

        #region UI Panel

        #region UI Class

        public string searchUIstring;
        public string pageUIstring;
        public string setsUIstring;
        public string requestsUIstring;

        public class UI
        {
            public static readonly uint AddUIstr = StringPool.Get("AddUI");

            public static bool AddUI(BasePlayer player, byte[] elements)
            {
                NetWrite netWrite = Net.sv.StartWrite();
                netWrite.PacketID(Message.Type.RPCMessage);
                netWrite.EntityID(CommunityEntity.ServerInstance.net.ID);
                netWrite.UInt32(AddUIstr);

                netWrite.BytesWithSize(elements);

                netWrite.Send(new SendInfo(player.net.connection));

                return true;
            }

            public static readonly uint DestroyUIstr = StringPool.Get("DestroyUI");

            public static bool DestroyUI(BasePlayer player, byte[] bytes)
            {
                NetWrite netWrite = Net.sv.StartWrite();
                netWrite.PacketID(Message.Type.RPCMessage);
                netWrite.EntityID(CommunityEntity.ServerInstance.net.ID);
                netWrite.UInt32(DestroyUIstr);

                netWrite.BytesWithSize(bytes);

                netWrite.Send(new SendInfo(player.net.connection));

                return false;
            }

            static public CuiElementContainer CreateElementContainer(string panelName, string color, string aMin, string aMax, string oMin, string oMax, bool useCursor = false, string parent = "Overlay")
            {
                var NewElement = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = { Color = color },
                            RectTransform = { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
                        panelName
                    }
                };
                return NewElement;
            }

            static public void CreatePanel(ref CuiElementContainer container, string panel, string color, string sprite, string material, string anchorMin, string anchorMax, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color, Sprite = sprite, Material = material },
                    RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax },
                    CursorEnabled = cursor
                },
                panel);
            }
            static public void CreateLabel(ref CuiElementContainer container, string panel, string text, int size, string anchorMin, string anchorMax, string colour, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Align = align, Text = text, Color = colour },
                    RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax }
                },
                panel);

            }
            static public void CreateButton(ref CuiElementContainer container, string panel, string bgcolor, string textcolour, string text, int size, string anchorMin, string anchorMax, string command, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = bgcolor, Command = command },
                    RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax, OffsetMin = "0 0", OffsetMax = "0 0" },
                    Text = { Text = text, Color = textcolour, FontSize = size, Align = align }
                },
                panel);
            }

            static public void AddInputField(ref CuiElementContainer container, string panel, string bgcolor, string txtcolor, string text, int size, string command, string anchorMin, string anchorMax)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = bgcolor },
                    RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax },
                },
                panel);
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Align = TextAnchor.MiddleLeft,
                            Color = txtcolor,
                            Command = command,
                            FontSize = size,
                            IsPassword = false,
                            Text = text,
                            HudMenuInput = true
                        },
                        new CuiRectTransformComponent {AnchorMin = anchorMin, AnchorMax = anchorMax }
                    }
                });
            }
        }
        #endregion UI Class

        public const string SkinPageUI = "SkinPageUI";
        public const string SkinSearchUI = "SkinSearchUI";
        public const string SkinSetsSelectUI = "SkinSetsSelectUI";
        public const string SkinRequestsUI = "SkinRequestsUI";

        public static byte[] BSkinPageUI = Encoding.UTF8.GetBytes(SkinPageUI);
        public static byte[] BSkinSearchUI = Encoding.UTF8.GetBytes(SkinSearchUI);
        public static byte[] BSkinSetsSelectUI = Encoding.UTF8.GetBytes(SkinSetsSelectUI);
        public static byte[] BSkinRequestsUI = Encoding.UTF8.GetBytes(SkinRequestsUI);

        private void InitUI()
        {
            pageUIstring = EscapeBrackets(InitPageUI());
            searchUIstring = EscapeBrackets(InitSearchUI());
            cachedsearchUIDefault = Encoding.UTF8.GetBytes(string.Format(searchUIstring, " Search Name or ID"));

            setsUIstring = EscapeBrackets(InitSetsUI());
            requestsUIstring = EscapeBrackets(InitRequestsUI());
        }

        private static Dictionary<ValueTuple<int, int>, byte[]> cacheduiPageDictionary = new Dictionary<ValueTuple<int, int>, byte[]>();
        public byte[] AddPageUI(int minpage, int maxpage)
        {
            if (cacheduiPageDictionary.TryGetValue((minpage, maxpage), out byte[] UIbytes))
                return UIbytes;
            UIbytes = Encoding.UTF8.GetBytes(string.Format(pageUIstring, minpage, maxpage));
            cacheduiPageDictionary.Add((minpage, maxpage), UIbytes);
            return UIbytes;
        }

        private byte[] cachedsearchUIDefault;
        public byte[] AddSearchUI(string searchtxt)
        {
            return string.IsNullOrEmpty(searchtxt) ? cachedsearchUIDefault : Encoding.UTF8.GetBytes(string.Format(searchUIstring, searchtxt));
        }

        private static Dictionary<int, byte[]> cacheduiSetsDictionary = new Dictionary<int, byte[]>();

        public byte[] AddSetsUI(int selectedset)
        {
            if (cacheduiSetsDictionary.TryGetValue(selectedset, out byte[] UIbytes))
                return UIbytes;

            string color1 = "0.5 0.5 0.5 0.2";
            string color2 = "0.5 0.5 0.5 0.2";
            string color3 = "0.5 0.5 0.5 0.2";
            string highlighted = "0.345 0.8 0.192 0.78";
            switch (selectedset)
            {
                case 2:
                    color2 = highlighted;
                    break;
                case 3:
                    color3 = highlighted;
                    break;
                default:
                    color1 = highlighted;
                    break;
            }
            UIbytes = Encoding.UTF8.GetBytes(string.Format(setsUIstring, color1, color2, color3));
            cacheduiSetsDictionary.Add(selectedset, UIbytes);
            return UIbytes;
        }

        private static Dictionary<string, byte[]> cacheduiRequestsDictionary = new Dictionary<string, byte[]>();

        public byte[] AddRequestsUI(string requesttype)
        {
            if (cacheduiRequestsDictionary.TryGetValue(requesttype, out byte[] UIbytes))
                return UIbytes;

            string color1 = "0.5 0.5 0.5 0.2";
            string color2 = "0.5 0.5 0.5 0.2";
            string color3 = "0.5 0.5 0.5 0.2";
            string highlighted = "0.345 0.8 0.192 0.78";
            switch (requesttype)
            {
                case "Approve":
                    color2 = highlighted;
                    break;
                case "Deny":
                    color3 = highlighted;
                    break;
                default:
                    color1 = highlighted;
                    break;
            }
            UIbytes = Encoding.UTF8.GetBytes(string.Format(requestsUIstring, color1, color2, color3));
            cacheduiRequestsDictionary.Add(requesttype, UIbytes);
            return UIbytes;
        }

        private string InitPageUI()
        {
            var con = UI.CreateElementContainer(SkinPageUI, "0.5 0.5 0.5 0.3", config.uiposition[0], config.uiposition[1], config.uiposition[2], config.uiposition[3], false);
            UI.CreateLabel(ref con, SkinPageUI, "{0} of {1}", 14, "0.30 0.00", "0.70 1.0", "0.8 0.8 0.8 0.75", TextAnchor.MiddleCenter);
            UI.CreateButton(ref con, SkinPageUI, "0.5 0.5 0.5 0.0", "0.8 0.8 0.8 0.75", "←", 25, "0.00 0.00", "0.30 1.0", "sbBackPage", TextAnchor.MiddleCenter);
            UI.CreateButton(ref con, SkinPageUI, "0.5 0.5 0.5 0.0", "0.8 0.8 0.8 0.75", "→", 25, "0.70 0.00", "1.0 1.0", "sbNextPage", TextAnchor.MiddleCenter);
            return CuiHelper.ToJson(con);
        }

        public string InitSearchUI()
        {
            var con = UI.CreateElementContainer(SkinSearchUI, "0.5 0.5 0.5 0.5", config.uisearchposition[0], config.uisearchposition[1], config.uisearchposition[2], config.uisearchposition[3], false);
            UI.AddInputField(ref con, SkinSearchUI, "0 0 0 0", "#FFFFFF", "{0}", 14, "searchCMD", "0.00 0.00", "1.00 1.00");
            UI.CreateButton(ref con, SkinSearchUI, "0.9 0.1 0.1 1.0", "0.8 0.8 0.8 0.75", "x", 20, "0.85 0.00", "1.0 0.98", "searchCMD", TextAnchor.MiddleCenter);
            return CuiHelper.ToJson(con);
        }

        public static string EscapeBrackets(string input)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '{')
                {
                    if (i + 1 < input.Length && Char.IsDigit(input[i + 1]))
                    {
                        sb.Append(input[i]);
                    }
                    else
                    {
                        sb.Append(input[i]);
                        sb.Append(input[i]);
                    }
                }
                else if (input[i] == '}')
                {
                    if (i + 1 < input.Length && Char.IsDigit(input[i - 1]))
                    {
                        sb.Append(input[i]);
                    }
                    else
                    {
                        sb.Append(input[i]);
                        sb.Append(input[i]);
                    }
                }
                else
                {
                    sb.Append(input[i]);
                }
            }
            return sb.ToString();
        }

        public string InitSetsUI()
        {
            var con = UI.CreateElementContainer(SkinSetsSelectUI, "0.5 0.5 0.5 0.5", config.uisetsposition[0], config.uisetsposition[1], config.uisetsposition[2], config.uisetsposition[3], false);
            UI.CreateButton(ref con, SkinSetsSelectUI, "{0}", "0.8 0.8 0.8 0.75", "Set 1", 17, "0.00 0.00", "0.31 0.98", "setSelectCMD 1", TextAnchor.MiddleCenter);
            UI.CreateButton(ref con, SkinSetsSelectUI, "{1}", "0.8 0.8 0.8 0.75", "Set 2", 17, "0.34 0.00", "0.65 0.98", "setSelectCMD 2", TextAnchor.MiddleCenter);
            UI.CreateButton(ref con, SkinSetsSelectUI, "{2}", "0.8 0.8 0.8 0.75", "Set 3", 17, "0.68 0.00", "1.00 0.98", "setSelectCMD 3", TextAnchor.MiddleCenter);
            return CuiHelper.ToJson(con);
        }

        public string InitRequestsUI()
        {
            var con = UI.CreateElementContainer(SkinRequestsUI, "0.5 0.5 0.5 0.5", config.uisetsposition[0], config.uisetsposition[1], config.uisetsposition[2], config.uisetsposition[3], false);
            UI.CreateButton(ref con, SkinRequestsUI, "{0}", "0.8 0.8 0.8 0.75", "Try", 17, "0.00 0.00", "0.31 0.98", "requestSelectCMD Try", TextAnchor.MiddleCenter);
            UI.CreateButton(ref con, SkinRequestsUI, "{1}", "0.8 0.8 0.8 0.75", "Approve", 17, "0.34 0.00", "0.65 0.98", "requestSelectCMD Approve", TextAnchor.MiddleCenter);
            UI.CreateButton(ref con, SkinRequestsUI, "{2}", "0.8 0.8 0.8 0.75", "Deny", 17, "0.68 0.00", "1.00 0.98", "requestSelectCMD Deny", TextAnchor.MiddleCenter);
            return CuiHelper.ToJson(con);
        }

        #endregion GUI Panel

        #region Helpers
        private Message.Type RPCMessage = Message.Type.RPCMessage;
        private uint refreshSkin = StringPool.Get("RefreshSkin");
        private void SendNetworkUpdate(BaseEntity ent, SendInfo sendInfo = new SendInfo())
        {
            if (Net.sv.IsConnected())
            {
                if (sendInfo.connections?.Count < 1)
                    sendInfo = new SendInfo(ent.net.group.subscribers);
                NetWrite netWrite = Net.sv.StartWrite();
                netWrite.PacketID(Message.Type.EntityDestroy);
                netWrite.EntityID(ent.net.ID);
                netWrite.UInt8(0);
                netWrite.Send(new SendInfo(ent.net.group.subscribers));
            }

            foreach (var child in ent.children)
            {
                SendNetworkUpdate(child, sendInfo);
            }

            List<Connection> subscribers = Pool.Get<List<Connection>>();
            subscribers.AddRange(ent.net.group.subscribers);

            if (subscribers != null && subscribers.Count > 0)
            {
                for (int i = 0; i < subscribers.Count; i++)
                {
                    BasePlayer item = subscribers[i].player as BasePlayer;
                    if (!(item == null) && ent.ShouldNetworkTo(item))
                    {
                        item.QueueUpdate(0, ent);
                        item.SendEntityUpdate();
                    }
                }
            }
            foreach (var child in ent.children)
            {
                SendNetworkUpdate(child);
            }
            ent.gameObject.SendOnSendNetworkUpdate(ent);
        }
        private void DoRemove(Item item)
        {
            if (item.isServer && item.uid.Value > 0 && Net.sv != null)
            {
                Net.sv.ReturnUID(item.uid.Value);
                item.uid.Value = 0;
            }
            if (item.contents != null)
            {
                item.contents.Kill();
                item.contents = null;
            }
            if (item.isServer)
            {
                item.RemoveFromWorld();
                item.RemoveFromContainer();
            }
            BaseEntity heldEntity = item.GetHeldEntity();
            if (heldEntity.IsValid())
            {
                heldEntity.Kill();
            }
        }

        private bool HasPerm(string id, string perm) => permission.UserHasPermission(id, perm);

        private string GetLang(string langKey, string playerId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(langKey, this, playerId), args);
        }
        private void ChatMessage(IPlayer player, string langKey, params object[] args)
        {
            if (player.IsConnected)
                player.Message(GetLang(langKey, player.Id, args));
            else Puts(GetLang(langKey, player.Id, args));
        }
        #endregion Helpers

        #region SteamWorkshop WebRequests
        private IEnumerator getCollectionscouroutine;
        private List<string> _WorkshopSkinIDCollectionList = new List<string>();
        private IEnumerator GetCollectionSkinIDS()
        {
            string vurl = "https://steamcommunity.com/sharedfiles/filedetails/?id={0}";
            for (int i = 0; i < config.skinCollectionIDs.Count; i++)
            {
                var collectionid = config.skinCollectionIDs[i];
                string downloadHandler;
                UnityWebRequest www = UnityWebRequest.Get(string.Format(vurl, collectionid));
                www.SetRequestHeader("Content-Type", "application/json");
                yield return www.SendWebRequest();

                // Verify that the webrequest was succesful.
                if (www.isNetworkError || www.isHttpError)
                {
                    Puts($"waiting 30 seconds for {www.error}");
                    www.Dispose();
                    i--;
                    yield return new WaitForSeconds(30f);
                    continue;
                }
                downloadHandler = www.downloadHandler.text;
                string[] htmlslines = downloadHandler.Split('\n');
                foreach (string htmlline in htmlslines)
                {
                    string trimmed = htmlline.Trim();
                    if (!trimmed.StartsWith("SharedFileBindMouseHover")) continue;
                    string skinid = trimmed.Split('"')[1].Split('_')[1];
                    //Puts(skinid);
                    ulong skinuL;
                    if (ulong.TryParse(skinid, out skinuL))
                    {
                        if (!config.ImportedSkinList.ContainsKey(skinuL) && !_WorkshopSkinIDCollectionList.Contains(skinid))
                            _WorkshopSkinIDCollectionList.Add(skinid);
                    }
                }
                yield return new WaitForSeconds(0.5f);
            }
            getCollectionscouroutine = null;
            if (getSteamWorkshopSkinData != null)
            {
                Puts("getSteamWorkshopSkinData already running!!");
            }
            else
            {
                getSteamWorkshopSkinData = GetSteamWorkshopSkinData();
                ServerMgr.Instance.StartCoroutine(getSteamWorkshopSkinData);
            }
        }

        private IEnumerator getSteamWorkshopSkinData;
        private IEnumerator GetSteamWorkshopSkinData()
        {
            if (_WorkshopSkinIDCollectionList.Count > 500)
                Puts($"Warning over 500 skins are waiting for import, start up may take longer \n Skins to import - {_WorkshopSkinIDCollectionList.Count}");
            string vurl = "https://steamcommunity.com/sharedfiles/filedetails/?id={0}";
            for (int i = 0; i < _WorkshopSkinIDCollectionList.Count; i++)
            {
                var workshopid = _WorkshopSkinIDCollectionList[i];
                string downloadHandler;
                UnityWebRequest www = UnityWebRequest.Get(string.Format(vurl, workshopid));
                www.SetRequestHeader("Content-Type", "application/json");
                yield return www.SendWebRequest();

                // Verify that the webrequest was succesful.
                if (www.isNetworkError || www.isHttpError)
                {
                    Puts($"waiting 30 seconds for {www.error}");
                    www.Dispose();
                    i--;
                    yield return new WaitForSeconds(30f);
                    continue;
                }
                downloadHandler = www.downloadHandler.text;
                string[] htmlslines = downloadHandler.Split('\n');

                ValueTuple<string, string>? textreturn = htmllines2shortname(htmlslines, workshopid);

                if (textreturn == null)
                {
                    yield return new WaitForSeconds(0.001f);
                    continue;
                }

                ulong uworkshopid;
                if (ulong.TryParse(workshopid, out uworkshopid))
                {
                    config.ImportedSkinList[uworkshopid] = new ImportedItem()
                    { itemDisplayname = textreturn.Value.Item2, itemShortname = textreturn.Value.Item1 };
                }
                else
                {
                    Puts("Failed to parse workshop ID" + workshopid);
                }
                yield return new WaitForSeconds(0.001f);
            }
            getSteamWorkshopSkinData = null;
            _WorkshopSkinIDCollectionList.Clear();
            GetSkins();
        }

        private ValueTuple<string, string>? htmllines2shortname(string[] htmlslines, string workshopid)
        {
            string skinname = "";
            bool titlef = false;
            foreach (string htmlline in htmlslines)
            {
                string trimmed = htmlline.Trim();
                if (!titlef)
                {
                    if (trimmed.StartsWith("<title>"))
                    {
                        titlef = true;
                        skinname = trimmed.Split(':')[2].Split('<')[0];
                    }
                    continue;
                }

                string[] trimsplits = trimmed.Split('\"');
                if (trimsplits.Length < 6) continue;

                string skintype = string.Empty;
                string[] splitByGreater = trimmed.Split('>');

                if (trimsplits[1] == "workshopTags" || trimsplits[3] == "workshopTags")
                {
                    int startIndex = trimsplits[1] == "workshopTags" ? 6 : 4;
                    for (int index = startIndex; index < splitByGreater.Length; index += 2)
                    {
                        skintype = splitByGreater[index].Split('<')[0];
                        if (skintype != "Skin" && skintype != "Version3" && skintype != "version2")
                            break;
                    }
                }

                if (string.IsNullOrEmpty(skintype)) continue;

                //string shortname = string.Empty;
                if (!WorkshopSkinNameConversion.TryGetValue(skintype, out string shortname))
                {
                    Puts($"Cannot find item definition for id: {workshopid} type:{skintype}");
                    break;
                }

                return (shortname, skinname);
            }

            Puts($"Cannot find item definition for id: {workshopid}");

            return null;
        }

        private IEnumerator getSteamWorkshopRequestData;
        private IEnumerator GetSteamWorkshopSkinRequests()
        {
            string vurl = "https://steamcommunity.com/sharedfiles/filedetails/?id={0}";
            for (int i = 0; i < _requestsData.Count; i++)
            {
                RequestItem request = _requestsData[i];
                if (request.itemID != 0 && !string.IsNullOrEmpty(request.itemDisplayname))
                    continue;

                var workshopid = request.skinID.ToString();

                //Puts(string.Format(vurl, workshopid));
                string downloadHandler;
                UnityWebRequest www = UnityWebRequest.Get(string.Format(vurl, workshopid));
                www.SetRequestHeader("Content-Type", "application/json");
                yield return www.SendWebRequest();

                // Verify that the webrequest was succesful.
                if (www.isNetworkError || www.isHttpError)
                {
                    Puts($"waiting 30 seconds for {www.error}");
                    www.Dispose();
                    i--;
                    yield return CoroutineEx.waitForSeconds(30f);
                    continue;
                }
                downloadHandler = www.downloadHandler.text;
                string[] htmlslines = downloadHandler.Split('\n');

                ValueTuple<string, string>? textreturn = htmllines2shortname(htmlslines, workshopid);
                if (textreturn == null)
                {
                    _requestsData.RemoveAt(i);
                    yield return CoroutineEx.waitForSeconds(0.001f);
                    continue;
                }
                ItemDefinition itemDef = ItemManager.FindItemDefinition(textreturn.Value.Item1);
                if (itemDef == null)
                {
                    Puts($"Cannot find item definition for id: {workshopid}");
                    _requestsData.RemoveAt(i);

                    continue;
                }

                _requestsData[i] = new RequestItem { itemID = itemDef.itemid, skinID = request.skinID, itemDisplayname = textreturn.Value.Item2 };

                yield return CoroutineEx.waitForSeconds(0.001f);
            }

            getSteamWorkshopRequestData = null;
        }

        private struct DiscordData
        {
            public ulong SkinID;
            public BasePlayer player;
        }

        private List<DiscordData> _discordData = new List<DiscordData>();
        private IEnumerator notifyDiscordCoroutine;

        private IEnumerator NotifyDiscord()
        {
            int max = _discordData.Count;
            for (int i = _discordData.Count - 1; i >= 0; i--)
            {
                DiscordData discordData = _discordData[i];

                if (discordData.SkinID < 10000)
                    continue;

                string url = $"https://steamcommunity.com/sharedfiles/filedetails/?id={discordData.SkinID}";

                UnityWebRequest head = UnityWebRequest.Get(url);

                head.timeout = 5;

                yield return head.SendWebRequest();

                if (head.result != UnityWebRequest.Result.Success)
                {
                    PrintError(head.error + "Cannot get headers from:" + url);
                    head.Dispose();
                    continue;
                }
                string title = string.Empty;
                string image = string.Empty;

                foreach (var line in head.downloadHandler.text.Split('\n'))
                {
                    var trim = line.Trim();
                    if (trim.StartsWith("<meta property=\"twitter:title\""))
                    {
                        title = trim.Split(':')[3].Split('"')[0];
                    }
                    if (trim.StartsWith("<meta name=\"twitter:image\""))
                    {
                        image = trim.Split('"')[3];
                        break;
                    }
                }

                if (string.IsNullOrEmpty(title) || title == "Error")
                {
                    head.Dispose();
                    Puts($"Could not get content for skin {discordData.SkinID}");
                    for (i = _requestsData.Count - 1; i > 0; i--)
                    {
                        if (_requestsData[i].skinID == discordData.SkinID)
                        {
                            _requestsData.RemoveAt(i);
                            break;
                        }
                    }
                    continue;
                }

                Root root = new Root
                {
                    tts = false,
                    content = string.Empty,
                    username = "Skinner",
                    avatar_url = "https://codefling.com/uploads/monthly_2024_07/Skinner_Thumbnail2-0.png.97a301b396dbeae7f180d1f8002e02d3.png",
                    embeds = new List<Embed>
                    {
                        new Embed
                        {
                            fields = new List<Field>
                            {
                                new Field
                                {
                                    name = "Requested By",
                                    value = $"{discordData.player.displayName} - [{discordData.player.UserIDString}](https://steamcommunity.com/id/{discordData.player.UserIDString})"
                                },
                                new Field
                                {
                                    name = "Skin",
                                    value = $"{title} - [{discordData.SkinID}](https://steamcommunity.com/sharedfiles/filedetails/?id={discordData.SkinID})"
                                }
                            },
                            author = new Author
                            {
                                name = "Skinner",
                                icon_url = "https://codefling.com/uploads/monthly_2024_07/Skinner_Thumbnail2-0.png.97a301b396dbeae7f180d1f8002e02d3.png"
                            },
                            title = $"{server.Name} - New Skin Request",
                            image = new Image { url = image },
                            timestamp = DateTime.Now,
                        }
                    }
                };

                UnityWebRequest sendtoDiscord = UnityWebRequest.Post(config.DiscordWebhook, JsonConvert.SerializeObject(root).ToString(), "application/json");

                yield return sendtoDiscord.SendWebRequest();

                if (sendtoDiscord.result != UnityWebRequest.Result.Success)
                {
                    PrintError(sendtoDiscord.error + "Cannot send to discord:");
                    sendtoDiscord.Dispose();
                    continue;
                }
                head.Dispose();
                sendtoDiscord.Dispose();
            }
            _discordData.RemoveRange(0, max);
            notifyDiscordCoroutine = null;
        }


        #region Discord
        public class Root
        {
            public string content { get; set; }
            public bool tts { get; set; }
            public List<Embed> embeds { get; set; }

            public string avatar_url { get; set; }

            public string username { get; set; }

        }

        public class Embed
        {
            public int id { get; set; }
            public List<Field> fields { get; set; }
            public Author author { get; set; }
            public string title { get; set; }
            public Image image { get; set; }
            public DateTime timestamp { get; set; }
        }

        public class Field
        {
            public int id { get; set; }
            public string name { get; set; }
            public string value { get; set; }
        }

        public class Author
        {
            public string name { get; set; }
            public string icon_url { get; set; }
        }

        public class Image
        {
            public string url { get; set; }
        }

        #endregion Discord

        private Dictionary<string, string> WorkshopSkinNameConversion = new Dictionary<string, string>
        {
            {"Acoustic Guitar","fun.guitar"},
            {"AK47","rifle.ak"},
            {"AK47 Skin","rifle.ak"},
            {"Armored Double Door", "door.double.hinged.toptier"},
            {"Armored Door","door.hinged.toptier"},
            {"Balaclava","mask.balaclava"},
            {"Balaclava Skin","mask.balaclava"},
            {"Bandana","mask.bandana"},
            {"Bearskin Rug", "rug.bear"},
            {"Beenie Hat","hat.beenie"},
            {"Beenie Skin","hat.beenie"},
            {"Bolt Rifle","rifle.bolt"},
            {"BoltRifle Skin","rifle.bolt"},
            {"Bone Club","bone.club"},
            {"Bone Knife","knife.bone"},
            {"Boonie Skin","hat.boonie"},
            {"Boonie Hat","hat.boonie"},
            {"Bucket Helmet","bucket.helmet"},
            {"Burlap Headwrap","burlap.headwrap"},
            {"Burlap Pants","burlap.trousers"},
            {"Burlap Shirt","burlap.shirt"},
            {"Burlap Shoes","burlap.shoes"},
            {"Cap","hat.cap"},
            {"Chair", "chair"},
            {"Coffee Can Helmet","coffeecan.helmet"},
            {"Collared Shirt","shirt.collared"},
            {"Combat Knife","knife.combat"},
            {"Concrete Barricade","barricade.concrete"},
            {"Crossbow","crossbow"},
            {"Custom SMG","smg.2"},
            {"Deer Skull Mask","deer.skull.mask"},
            {"Double Barrel Shotgun","shotgun.double"},
            {"Eoka Pistol","pistol.eoka"},
            {"F1 Grenade","grenade.f1"},
            {"Furnace","furnace"},
            {"Fridge", "fridge"},
            {"Garage Door", "wall.frame.garagedoor"},
            {"Hammer","hammer"},
            {"Hatchet","hatchet"},
            {"Hide Halterneck","attire.hide.helterneck"},
            {"Hide Pants","attire.hide.pants"},
            {"Hide Poncho","attire.hide.poncho"},
            {"Hide Shirt","attire.hide.vest"},
            {"Hide Shoes","attire.hide.boots"},
            {"Hide Skirt","attire.hide.skirt"},
            {"Hoodie","hoodie"},
            {"Hunting Bow","bow.hunting"},
            {"Jacket Skin", "jacket"},
            {"Jackhammer", "jackhammer"},
            {"Large Wood Box","box.wooden.large"},
            {"Leather Gloves","burlap.gloves"},
            {"Long TShirt","tshirt.long"},
            {"Longsword","longsword"},
            {"LR300","rifle.lr300"},
            {"Locker","locker"},
            {"L96", "rifle.l96"},
            {"Metal Chest Plate","metal.plate.torso"},
            {"Metal Facemask","metal.facemask"},
            {"Miner Hat","hat.miner"},
            {"Mp5","smg.mp5"},
            {"M39", "rifle.m39"},
            {"M249", "lmg.m249"},
            {"Pants","pants"},
            {"Pants Skin","pants"},
            {"Pick Axe","pickaxe"},
            {"Pump Shotgun","shotgun.pump"},
            {"Python","pistol.python"},
            {"Reactive Target","target.reactive"},
            {"Revolver","pistol.revolver"},
            {"Riot Helmet","riot.helmet"},
            {"Roadsign Gloves", "roadsign.gloves"},
            {"Roadsign Pants","roadsign.kilt"},
            {"Roadsign Vest","roadsign.jacket"},
            {"Rock","rock"},
            {"Rock Skin","rock"},
            {"Rocket Launcher","rocket.launcher"},
            {"Rug", "rug"},
            {"Rug Bear Skin","rug.bear"},
            {"Salvaged Hammer","hammer.salvaged"},
            {"Salvaged Icepick","icepick.salvaged"},
            {"Sandbag Barricade","barricade.sandbags"},
            {"Satchel Charge","explosive.satchel"},
            {"Semi-Automatic Pistol","pistol.semiauto"},
            {"SemiAutoPistol Skin","pistol.semiauto"},
            {"Semi-Automatic Rifle","rifle.semiauto"},
            {"Sheet Metal Door","door.hinged.metal"},
            {"Sheet Metal Double Door","door.double.hinged.metal"},
            {"Shorts","pants.shorts"},
            {"Sleeping Bag","sleepingbag"},
            {"Snow Jacket","jacket.snow"},
            {"SnowJacket Skin","jacket.snow"},
            {"Spinning Wheel", "spinner.wheel"},
            {"Stone Hatchet","stonehatchet"},
            {"Stone Pick Axe","stone.pickaxe"},
            {"Sword","salvaged.sword"},
            {"Table", "table"},
            {"Tank Top","shirt.tanktop"},
            {"Thompson","smg.thompson"},
            {"TShirt","tshirt"},
            {"TShirt Skin","tshirt"},
            {"Vagabond Jacket","jacket"},
            {"Vending Machine","vending.machine"},
            {"Water Purifier","water.purifier"},
            {"Waterpipe Shotgun","shotgun.waterpipe"},
            {"Wood Storage Box","box.wooden"},
            {"WoodStorage Skin","box.wooden"},
            {"Wooden Door","door.hinged.wood"},
            {"Wooden Double Door", "door.double.hinged.wood" },
            {"Work Boots","shoes.boots"},
            {"Boots Skin","shoes.boots"}
        };

        #endregion SteamWorkshop WebRequests

        #region Public Helpers
        public Dictionary<int, List<ulong>> GetAllCachedSkins()
        {
            return _cachedSkins;
        }

        public bool IsRedirectID(ulong uID)
        {
            return HasMask(uID);
        }

        public int RedirectIDtoItemID(ulong uID)
        {
            return (int)UnsetMask(uID);
        }

        public List<ulong>? GetSkinsItemList(int itemid)
        {
            _cachedSkins.TryGetValue(itemid, out List<ulong>? cachedSkins);
            return cachedSkins;
        }

        #endregion Public Helpers
    }
}