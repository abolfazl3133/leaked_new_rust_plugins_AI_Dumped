/* Copyright (C) Whispers88 - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential
 * Written by Whispers88 rustafarian.server@gmail.com, February 2023
 */

using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;


namespace Oxide.Plugins
{
    /*ПЛАГИН БЫЛ ПОФИКШЕН С ПОМОЩЬЮ ПРОГРАММЫ СКАЧАНОЙ С https://discord.gg/dNGbxafuJn */ [Info("Skinner", "https://discord.gg/dNGbxafuJn", "1.5.6")]
    [Description("Brings automation and ease to skinning items")]
    public class Skinner : CovalencePlugin
    {
        static Skinner skinner;
        private DynamicConfigFile _defaultSkins;
        private DynamicConfigFile _defaultSkins2;
        private DynamicConfigFile _defaultSkins3;

        private const string permdefault = "skinner.default";
        private const string permitems = "skinner.items";
        private const string permcraft = "skinner.craft";
        private const string permskininv = "skinner.skininv";
        private const string permskincon = "skinner.skincon";
        private const string permbypassauth = "skinner.bypassauth";
        private const string permimport = "skinner.import";
        private const string permskinbase = "skinner.skinbase";
        private const string permskinall = "skinner.skinall";

        private static List<string> _registeredhooks = new List<string> { "OnLootEntityEnd" };//, "CanStackItem" };//, "OnMaxStackable" };
        private Dictionary<ulong, Dictionary<string, CachedSkin>> _playerDefaultSkins = new Dictionary<ulong, Dictionary<string, CachedSkin>>();
        private Dictionary<ulong, Dictionary<string, CachedSkin>> _playerDefaultSkins2 = new Dictionary<ulong, Dictionary<string, CachedSkin>>();
        private Dictionary<ulong, Dictionary<string, CachedSkin>> _playerDefaultSkins3 = new Dictionary<ulong, Dictionary<string, CachedSkin>>();
        private Dictionary<ulong, int> _playerSelectedSet = new Dictionary<ulong, int>();

        private List<string> permissions = new List<string>() { permdefault, permcraft, permitems, permbypassauth, permskincon, permskininv, permimport, permskinbase, permskinall };
        private Dictionary<ulong, CoolDowns> _playercooldowns = new Dictionary<ulong, CoolDowns>();
        public class CoolDowns
        {
            public float skin = 30f;
            public float skinitem = 30f;
            public float skincraft = 30f;
            public float skincon = 30f;
            public float skininv = 30f;
            public float skinbase = 60f;
            public float skinall = 60f;
        }

        #region Init
        private void OnServerInitialized()
        {
            skinner = this;

            foreach (string perm in permissions)
                permission.RegisterPermission(perm, this);

            foreach (string perm in config.Cooldowns.Keys)
                permission.RegisterPermission($"skinner.{perm}", this);

            AddCovalenceCommand(config.cmdsskin, "SkinCMD");
            AddCovalenceCommand(config.cmdsskincraft, "DefaultSkinsCMD");
            AddCovalenceCommand(config.cmdsskinitems, "SkinItemCMD");
            AddCovalenceCommand(config.cmdsskininv, "SkinInvCMD");
            AddCovalenceCommand(config.cmdsskincon, "SkinConCMD");
            AddCovalenceCommand(config.cmdskinimport, "SkinImportCMD");
            AddCovalenceCommand(config.cmdcollectionimport, "SkinImportCollection");
            AddCovalenceCommand(config.cmdskinbase, "SkinBaseCMD");
            AddCovalenceCommand(config.cmdskinallitems, "SkinAllItemsCMD");
            AddCovalenceCommand(new[] { "sbNextPage" }, "SBNextPageCMD");
            AddCovalenceCommand(new[] { "sbBackPage" }, "SBBackPageCMD");
            AddCovalenceCommand(new[] { "searchCMD" }, "SearchCMD");
            AddCovalenceCommand(new[] { "setSelectCMD", "skinset", "ss" }, "SetSelectCMD");

            //Convert Old Skins Config
            foreach (var oldskin in config.Importedskins)
            {
                if (!config.ImportedSkinList.ContainsKey(oldskin.Key))
                    config.ImportedSkinList.Add(oldskin.Key, new ImportedItem());
            }
            config.Importedskins.Clear();


            foreach (var skin in config.ImportedSkinList)
            {
                if (string.IsNullOrEmpty(skin.Value?.itemDisplayname ?? string.Empty) || string.IsNullOrEmpty(skin.Value?.itemShortname ?? string.Empty))
                {
                    if (!_WorkshopSkinIDCollectionList.Contains(skin.Key.ToString()))
                        _WorkshopSkinIDCollectionList.Add(skin.Key.ToString());
                }
            }

            //GetSkins();

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
            _defaultSkins = Interface.Oxide.DataFileSystem.GetFile("DefaultCraftSkins");
            _defaultSkins2 = Interface.Oxide.DataFileSystem.GetFile("DefaultCraftSkins2");
            _defaultSkins3 = Interface.Oxide.DataFileSystem.GetFile("DefaultCraftSkins3");
            LoadData();
        }
        private void Unload()
        {
            SaveData();
            foreach (var player in _viewingcon)
            {
                BaseEntity baseEntity = player.inventory.loot?.entitySource ?? null;
                if (baseEntity != null)
                {
                    BoxController boxController;
                    if (baseEntity.TryGetComponent<BoxController>(out boxController))
                    {
                        UnityEngine.GameObject.Destroy(boxController);
                    }
                }
            }


            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "SkinPageUI");
                CuiHelper.DestroyUi(player, "SkinSearchUI");
                CuiHelper.DestroyUi(player, "SkinSetsSelectUI");
                SpraycanController spraycanController;
                if (player.TryGetComponent<SpraycanController>(out spraycanController))
                {
                    UnityEngine.GameObject.Destroy(spraycanController);
                }
            }

            if (getCollectionscouroutine != null)
                ServerMgr.Instance.StopCoroutine(getCollectionscouroutine);
            if (getSteamWorkshopSkinData != null)
                ServerMgr.Instance.StopCoroutine(getSteamWorkshopSkinData);
        }

        public class CachedSkin
        {
            public string shortname = string.Empty;
            public string displayName = string.Empty;
            public ulong skinid = 0;
            public bool redirect = false;
            public string redirectshortname = string.Empty;
            public ItemCategory category;
        }

        private Dictionary<string, List<CachedSkin>> _skinsCache = new Dictionary<string, List<CachedSkin>>();
        private Dictionary<string, string> _redirectSkins = new Dictionary<string, string>();
        private Dictionary<string, CachedSkin> _defaultskins = new Dictionary<string, CachedSkin>();
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

            if (_skinsCache.Count < 1)
            {
                foreach (ItemDefinition itemDef in ItemManager.GetItemDefinitions())
                {
                    List<CachedSkin> skins = new List<CachedSkin>()
                        { new CachedSkin() { skinid = 0uL, shortname = itemDef.shortname } };

                    foreach (var skin in ItemSkinDirectory.ForItem(itemDef))
                    {
                        if (config.blacklistedskins.Contains((ulong)skin.id)) continue;
                        if (skin.id == 0) continue;
                        CachedSkin cachedSkin = new CachedSkin
                        {
                            skinid = Convert.ToUInt64(skin.id),
                            shortname = itemDef.shortname,
                            displayName = skin.invItem?.displayName?.english ?? itemDef.displayName.english
                        };
                        ItemSkin itemSkin = skin.invItem as ItemSkin;
                        if (itemSkin != null && itemSkin.Redirect != null)
                        {
                            cachedSkin.redirect = true;
                            cachedSkin.redirectshortname = itemSkin.Redirect.shortname;
                            if (!_redirectSkins.ContainsKey(cachedSkin.redirectshortname))
                                _redirectSkins.Add(cachedSkin.redirectshortname, itemDef.shortname);
                        }

                        skins.Add(cachedSkin);
                    }

                    if (skins.Count > 1)
                    {
                        _skinsCache.Add(itemDef.shortname, skins);
                    }
                }

                foreach (Steamworks.InventoryDef item in Steamworks.SteamInventory.Definitions)
                {
                    string shortname = item.GetProperty("itemshortname") == "lr300.item"
                        ? "rifle.lr300"
                        : item.GetProperty("itemshortname");
                    if (string.IsNullOrEmpty(shortname) || item.Id < 100)
                        continue;

                    ulong skinid;

                    if (config.blacklistedskins.Contains((ulong)item.Id)) continue;
                    if (!ulong.TryParse(item.GetProperty("workshopid"), out skinid))
                    {
                        skinid = (ulong)item.Id;
                    }

                    if (skinid < 100000) continue;
                    List<CachedSkin> skins;
                    if (_skinsCache.TryGetValue(shortname, out skins))
                    {
                        if (skins.Where(x => x.skinid == skinid || x.skinid == (ulong)item.Id).Count() > 0) continue;

                        skins.Add(new CachedSkin
                        { skinid = Convert.ToUInt64(skinid), shortname = shortname, displayName = item.Name });
                    }
                    else
                    {
                        _skinsCache.Add(shortname,
                            new List<CachedSkin>()
                            {
                                new CachedSkin { skinid = 0, shortname = shortname },
                                new CachedSkin
                                {
                                    skinid = Convert.ToUInt64(skinid), shortname = shortname, displayName = item.Name
                                }
                            });
                    }
                }
            }

            foreach (var whitelistSkin in config.ImportedSkinList)
            {
                ItemDefinition itemdef = ItemManager.FindItemDefinition(whitelistSkin.Value.itemShortname);
                if (itemdef == null)
                {
                    Puts($"Could not find item definition for {whitelistSkin.Value.itemShortname} {whitelistSkin.Key}");
                    continue;
                }
                List<CachedSkin> skins2;
                if (_skinsCache.TryGetValue(itemdef.shortname, out skins2))
                {
                    bool skip = false;
                    foreach (var a in skins2)
                    {
                        if (a.skinid == whitelistSkin.Key)
                        {
                            skip = true;
                            break;
                        }

                    }
                    if (!skip)
                        skins2.Add(new CachedSkin { skinid = whitelistSkin.Key, shortname = itemdef.shortname, displayName = whitelistSkin.Value.itemDisplayname });
                }
                else
                {
                    Puts($"No default skins for {whitelistSkin.Value.itemShortname} trying to apply custom skin{whitelistSkin.Key}");
                    _skinsCache.Add(itemdef.shortname, new List<CachedSkin>() { new CachedSkin() { shortname = itemdef.shortname, displayName = whitelistSkin.Value.itemDisplayname, skinid = whitelistSkin.Key } });
                }
            }

            foreach (var item2 in _skinsCache.ToList())
            {
                if (_redirectSkins.ContainsKey(item2.Key))
                    continue;
                int skinsamt = item2.Value.Count;
                sk += skinsamt;
                if (skinsamt == 1)
                {
                    _skinsCache.Remove(item2.Key);
                    continue;
                }

                ItemDefinition itemdef = ItemManager.FindItemDefinition(item2.Key);
                if (itemdef == null || itemdef?.Blueprint == null)
                {
                    _skinsCache.Remove(item2.Key);
                    continue;
                }

                //if (!itemdef.Blueprint.userCraftable)
                //    continue;
                if (!_defaultskins.ContainsKey(item2.Key))
                    _defaultskins.Add(item2.Key, new CachedSkin() { shortname = item2.Key, category = itemdef.category });
            }

            SaveConfig();

            //Re-order to look nice
            _defaultskins = _defaultskins.OrderBy(key => key.Value.category).ToDictionary(x => x.Key, x => x.Value);
            Puts($"{sk} skins were indexed, Skin indexing complete");

            if (!config.sprayCanOveride)
                Unsubscribe("OnActiveItemChanged");
        }

        #endregion Init

        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty("Skin Commands (skin items in you inventory")]
            public string[] cmdsskin = new[] { "skin", "s" };

            [JsonProperty("Skin Items Commands (skin items you have already placed")]
            public string[] cmdsskinitems = new[] { "skinitem", "si" };

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

            [JsonProperty("Import Custom Skins")] public string[] cmdskinimport = new[] { "skinimport", "sip" };

            [JsonProperty("Import Workshop Collection Command")] public string[] cmdcollectionimport = new[] { "colimport", "cip" };

            [JsonProperty("Custom Page Change UI Positon 'min x, min y', 'max x', max y'")]
            public string[] uiposition = new[] { "0.66 0.05", "0.82 0.1" };

            [JsonProperty("Custom Searchbar UI Positon 'min x, min y', 'max x', max y'")]
            public string[] uisearchposition = new[] { "0.70 0.88", "0.82 0.91" };

            [JsonProperty("Custom Set Selection UI Positon 'min x, min y', 'max x', max y'")]
            public string[] uisetsposition = new[] { "0.70 0.845", "0.93 0.875" };

            [JsonProperty("Apply names of skins to skinned items")]
            public bool applySkinNames = true;

            [JsonProperty("Add Search Bar UI")]
            public bool searchbar = true;

            [JsonProperty("Override spraycan behaviour")]
            public bool sprayCanOveride = false;

            [JsonProperty("Use spraycan effect when holding spraycan and skinning deployables")]
            public bool sprayCanEffect = true;

            [JsonProperty("Blacklisted Skins (skinID)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ulong> blacklistedskins = new List<ulong>();

            [JsonProperty("Import Skin collections (steam workshop ID)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ulong> skinCollectionIDs = new List<ulong>();

            [JsonProperty("Command based cooldowns ('permission' : 'command' seconds")]
            public Dictionary<string, CoolDowns> Cooldowns = new Dictionary<string, CoolDowns>() { { "Default30CD", new CoolDowns() } };


            [JsonProperty("Imported Skins List", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<ulong, ImportedItem> ImportedSkinList = new Dictionary<ulong, ImportedItem>();


            [JsonProperty("Imported Skins (skinid : 'shortnamestring', skinid2 : 'shortnamestring2'", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<ulong, string> Importedskins = new Dictionary<ulong, string>() { { 861142659, "vending.machine" } };


            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        public class ImportedItem
        {
            public string itemShortname = string.Empty;
            public string itemDisplayname = string.Empty;
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

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    Puts("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
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
        private void LoadData()
        {
            try
            {
                _playerDefaultSkins = _defaultSkins.ReadObject<Dictionary<ulong, Dictionary<string, CachedSkin>>>();
            }
            catch
            {
                _playerDefaultSkins = new Dictionary<ulong, Dictionary<string, CachedSkin>>();
            }
            try
            {
                _playerDefaultSkins2 = _defaultSkins2.ReadObject<Dictionary<ulong, Dictionary<string, CachedSkin>>>();
            }
            catch
            {
                _playerDefaultSkins2 = new Dictionary<ulong, Dictionary<string, CachedSkin>>();
            }
            try
            {
                _playerDefaultSkins3 = _defaultSkins3.ReadObject<Dictionary<ulong, Dictionary<string, CachedSkin>>>();
            }
            catch
            {
                _playerDefaultSkins3 = new Dictionary<ulong, Dictionary<string, CachedSkin>>();
            }
        }

        private void SaveData()
        {
            _defaultSkins.WriteObject(_playerDefaultSkins);
            _defaultSkins2.WriteObject(_playerDefaultSkins2);
            _defaultSkins3.WriteObject(_playerDefaultSkins3);
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
                ["CollectionImported2"] = "Steam Skin Collection {0} has been imported and saved",
                ["CommandCooldown"] = "You can not use this command for another {0}",
                ["CompletedInvSkin"] = "All items in your inventory have been set to your default skins",
                ["CompletedConSkin"] = "All items in {0} have been set to your default skins",
                ["CompletedBuildingSkin"] = "All {0} in your base have been set to your default skins",
                ["CompletedAllSkin"] = "All {0} items in your base have been set to your default skins",
                ["SkinSetSelected"] = "Skin set {0} selected",
                ["SkinSetSelectedArgs"] = "Bad args, Required input set No. 1, 2 or 3"
            }, this);
        }

        #endregion Localization

        #region Commands
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
            player.EndLooting();

            if (!HasPerm(player.UserIDString, permdefault))
            {
                ChatMessage(iplayer, "NoPerms");
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
                CoolDowns coolDowns;
                if (!_playercooldowns.TryGetValue(player.userID, out coolDowns))
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

            StorageContainer storageContainer = CreateStorageCon(player);

            BoxController boxController;
            if (!storageContainer.TryGetComponent<BoxController>(out boxController))
            {
                storageContainer.Kill();
                return;

            }
            boxController.StartItemSkin();

            if (!_viewingcon.Contains(player))
                _viewingcon.Add(player);
            if (_viewingcon.Count == 1)
                SubscribeToHooks();

            timer.Once(0.3f, () =>
            {
                StartLooting(player, storageContainer);
            });
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
                CoolDowns coolDowns;
                if (!_playercooldowns.TryGetValue(player.userID, out coolDowns))
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
                _playerDefaultSkins.Add(player.userID, new Dictionary<string, CachedSkin>());

            StorageContainer storageContainer = CreateStorageCon(player);

            BoxController boxController;
            if (!storageContainer.TryGetComponent<BoxController>(out boxController))
            {
                storageContainer.Kill();
                return;
            }
            boxController.GetDefaultSkins();

            if (!_viewingcon.Contains(player))
                _viewingcon.Add(player);

            if (_viewingcon.Count == 1)
                SubscribeToHooks();

            timer.Once(0.3f, () =>
            {
                StartLooting(player, storageContainer);
            });
        }

        private static int Layermask = LayerMask.GetMask("Deployed", "Construction");
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
            if (entity == null || entity?.pickup.itemTarget == null)
            {
                ChatMessage(iplayer, "NoObjectsFound");
                return;
            }
            if (!_skinsCache.ContainsKey(entity.pickup.itemTarget.shortname))
            {
                ChatMessage(iplayer, "NoSkins");
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
                CoolDowns coolDowns;
                if (!_playercooldowns.TryGetValue(player.userID, out coolDowns))
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

            StorageContainer storageContainer = CreateStorageCon(player);
            BoxController boxController;
            if (!storageContainer.TryGetComponent<BoxController>(out boxController))
            {
                storageContainer.Kill();
                return;
            }
            boxController.SkinDeplyoables(entity);

            if (!_viewingcon.Contains(player))
                _viewingcon.Add(player);

            if (_viewingcon.Count == 1)
                SubscribeToHooks();

            timer.Once(0.3f, () =>
            {
                StartLooting(player, storageContainer);
            });
        }
        private void SkinInvCMD(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player == null) return;
            player.EndLooting();

            if (!HasPerm(player.UserIDString, permskininv))
            {
                ChatMessage(iplayer, "NoPerms");
                return;
            }

            Dictionary<string, CachedSkin> cachedskins = GetCachedSkins(player);
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
                CoolDowns coolDowns;
                if (!_playercooldowns.TryGetValue(player.userID, out coolDowns))
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
            List<Item> itemstoSkin = new List<Item>();
            foreach(Item item in Enumerable.Concat(player.inventory.containerMain?.itemList ?? Enumerable.Empty<Item>(), Enumerable.Concat(player.inventory.containerBelt?.itemList ?? Enumerable.Empty<Item>(), player.inventory.containerWear?.itemList ?? Enumerable.Empty<Item>())))
            {
                if(item.IsBackpack())
                {
                    if(item.contents.itemList.Count > 0)
                    {
                        itemstoSkin.AddRange(item.contents.itemList);
                    }
                }
                itemstoSkin.Add(item);
            }
            foreach (Item item in itemstoSkin)
            {
                if (item == null) continue;
                CachedSkin cachedSkin;
                if (cachedskins.TryGetValue(item.info.shortname, out cachedSkin))
                {
                    //skip blacklisted skin
                    if (config.blacklistedskins.Contains(item.skin)) continue;

                    if (cachedSkin.redirect) continue;
                    item.skin = cachedSkin.skinid;
                    BaseEntity held = item.GetHeldEntity();

                    if (held != null)
                    {
                        held.skinID = cachedSkin.skinid;
                        held.SendNetworkUpdate();
                    }
                }
            }
            player.inventory.containerWear.MarkDirty();
            player.inventory.containerBelt.MarkDirty();
            player.SendNetworkUpdateImmediate();
            ChatMessage(iplayer, "CompletedInvSkin");
        }

        private void SkinConCMD(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player == null) return;
            player.EndLooting();

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

            Dictionary<string, CachedSkin> cachedskins = GetCachedSkins(player);
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
                CoolDowns coolDowns;
                if (!_playercooldowns.TryGetValue(player.userID, out coolDowns))
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
                CachedSkin cachedSkin;
                if (cachedskins.TryGetValue(item.info.shortname, out cachedSkin))
                {
                    //skip blacklisted skin
                    if (config.blacklistedskins.Contains(item.skin)) continue;

                    if (cachedSkin.redirect) continue;
                    item.skin = cachedSkin.skinid;
                    BaseEntity held = item.GetHeldEntity();

                    if (held != null)
                    {
                        held.skinID = cachedSkin.skinid;
                        held.SendNetworkUpdate();
                    }
                }
            }
            storage.SendNetworkUpdateImmediate();
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
            Dictionary<string, CachedSkin> cachedskins = GetCachedSkins(player);
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
                CoolDowns coolDowns;
                if (!_playercooldowns.TryGetValue(player.userID, out coolDowns))
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

            string skinned = "all deplyoables";
            if (args.Length > 0)
            {
                skinned = $"{args[0]}s";
            }

            foreach (var decayent in buildingManager.decayEntities)
            {

                BaseCombatEntity baseCombatEntity = decayent?.GetEntity() as BaseCombatEntity;
                if (baseCombatEntity == null || baseCombatEntity.pickup.itemTarget == null) continue;
                CachedSkin cachedSkin;

                if (args.Length > 0)
                {
                    if (!baseCombatEntity.pickup.itemTarget.shortname.Contains(args[0]))
                        continue;
                }
                if (cachedskins.TryGetValue(baseCombatEntity.pickup.itemTarget.shortname, out cachedSkin))
                {
                    if (baseCombatEntity.skinID == cachedSkin.skinid)
                        continue;
                    baseCombatEntity.skinID = cachedSkin.skinid;
                    if (baseCombatEntity.skinID == 0uL || baseCombatEntity.skinID < 100000)
                    {
                        SendNetworkUpdate(baseCombatEntity, player);
                    }
                    else
                        baseCombatEntity.SendNetworkUpdateImmediate();
                }
            }
            ChatMessage(iplayer, "CompletedBuildingSkin", skinned);
        }

        private Dictionary<string, CachedSkin> GetCachedSkins(BasePlayer player)
        {
            IPlayer iplayer = player.IPlayer;
            int set = 1;
            Dictionary<string, CachedSkin> cachedskins = new Dictionary<string, CachedSkin>();
            if (_playerSelectedSet.TryGetValue(player.userID, out set))
            {
                if (set == 2)
                {
                    if (!_playerDefaultSkins2.TryGetValue(player.userID, out cachedskins))
                    {
                        _playerDefaultSkins2[player.userID] = new Dictionary<string, CachedSkin>();
                        ChatMessage(iplayer, "NoDefaultSkins");
                        return _playerDefaultSkins2[player.userID];
                    }
                    return cachedskins;
                }
                if (set == 3)
                {
                    if (!_playerDefaultSkins3.TryGetValue(player.userID, out cachedskins))
                    {
                        _playerDefaultSkins3[player.userID] = new Dictionary<string, CachedSkin>();
                        ChatMessage(iplayer, "NoDefaultSkins");
                        return _playerDefaultSkins3[player.userID];
                    }
                    return cachedskins;
                }
            }
            if (!_playerDefaultSkins.TryGetValue(player.userID, out cachedskins))
            {
                ChatMessage(iplayer, "NoDefaultSkins");
                _playerDefaultSkins[player.userID] = new Dictionary<string, CachedSkin>();
                return _playerDefaultSkins[player.userID];
            }
            return cachedskins;
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
            Dictionary<string, CachedSkin> cachedskins = GetCachedSkins(player);
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
                CoolDowns coolDowns;
                if (!_playercooldowns.TryGetValue(player.userID, out coolDowns))
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

                    //skip blacklisted skin
                    if (config.blacklistedskins.Contains(item.skin)) continue;

                    CachedSkin cachedSkin;
                    if (cachedskins.TryGetValue(item.info.shortname, out cachedSkin))
                    {
                        if (item.skin == cachedSkin.skinid) continue;
                        item.skin = cachedSkin.skinid;
                        var held = item.GetHeldEntity();
                        if (held != null)
                        {
                            held.skinID = cachedSkin.skinid;
                            held.SendNetworkUpdate();
                        }
                    }
                }
            }
            ChatMessage(iplayer, "CompletedAllSkin", skinned);
        }

        private void SBNextPageCMD(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player == null) return;
            if (!_viewingcon.Contains(player)) return;
            StorageContainer storagecontainer = player.inventory.loot.entitySource as StorageContainer;
            if (storagecontainer == null) return;
            BoxController boxController;
            if (!storagecontainer.TryGetComponent<BoxController>(out boxController))
                return;
            if (boxController._fillingbox || boxController._clearingbox)
                return;
            boxController.NextPage();
        }

        private void SBBackPageCMD(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player == null) return;
            if (!_viewingcon.Contains(player)) return;
            StorageContainer storagecontainer = player.inventory.loot.entitySource as StorageContainer;
            if (storagecontainer == null) return;
            BoxController boxController;
            if (!storagecontainer.TryGetComponent<BoxController>(out boxController))
                return;
            if (boxController._fillingbox || boxController._clearingbox)
                return;
            boxController.BackPage();
        }

        private void SearchCMD(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player == null) return;
            if (!_viewingcon.Contains(player)) return;
            StorageContainer storagecontainer = player.inventory.loot.entitySource as StorageContainer;
            if (storagecontainer == null) return;
            BoxController boxController;
            if (!storagecontainer.TryGetComponent<BoxController>(out boxController))
                return;
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

            if (!_viewingcon.Contains(player)) return;
            StorageContainer storagecontainer = player.inventory.loot.entitySource as StorageContainer;
            if (storagecontainer == null) return;
            BoxController boxController;
            if (!storagecontainer.TryGetComponent<BoxController>(out boxController))
                return;
            if (boxController._fillingbox || boxController._clearingbox)
                return;

            boxController.setSelect = setselect;
            boxController.SearchUpdate();
        }

        #endregion Commands

        #region Hooks

        //private object CanStackItem(Item item, Item targetItem) => (targetItem.parent?.entityOwner?._limitedNetworking ?? false) ? false : (object)null;

        private List<BasePlayer> _viewingcon = new List<BasePlayer>();
        private void OnLootEntityEnd(BasePlayer player, StorageContainer storageContainer)
        {
            if (!_viewingcon.Contains(player)) return;

            _viewingcon.Remove(player);

            CuiHelper.DestroyUi(player, "SkinPageUI");
            CuiHelper.DestroyUi(player, "SkinSearchUI");
            CuiHelper.DestroyUi(player, "SkinSetsSelectUI");

            if (_viewingcon.Count < 1)
                UnSubscribeFromHooks();

            BoxController boxController;
            if (storageContainer.TryGetComponent<BoxController>(out boxController))
            {
                timer.Once(0.2f, () => { UnityEngine.Object.Destroy(boxController); });
            }
        }

        private void OnItemCraftFinished(ItemCraftTask task, Item item, ItemCrafter itemCrafter)
        {
            if (task.skinID != 0)
                return;

            BasePlayer player = itemCrafter.owner;

            if (!HasPerm(player.UserIDString, permdefault))
                return;

            Dictionary<string, CachedSkin> cached = GetCachedSkins(player);
            if (cached.Count < 1)
                return;

            CachedSkin cachedSkin;
            if (!cached.TryGetValue(item.info.shortname, out cachedSkin))
                return;

            if (cachedSkin.redirect)
            {
                int amt = item.amount;
                NextTick(() =>
                {
                    DoRemove(item);
                    player.GiveItem(ItemManager.CreateByName(cachedSkin.redirectshortname, amt, 0), BaseEntity.GiveItemReason.Crafted);
                });
                return;
            }

            item.skin = cachedSkin.skinid;

            if (config.applySkinNames)
            {
                item.name = cachedSkin.displayName;
                item.info.displayName = cachedSkin.displayName;
            }

            var held = item.GetHeldEntity();

            if (held != null)
            {
                held.skinID = cachedSkin.skinid;
                held.SendNetworkUpdate();
            }
        }

        #endregion Hooks

        #region Methods
        private StorageContainer CreateStorageCon(BasePlayer player)
        {
            StorageContainer storage = GameManager.server.CreateEntity(StringPool.Get(4080262419), Vector3.zero) as StorageContainer;

            storage.syncPosition = false;
            storage.limitNetworking = true;
            storage.name = player.displayName;
            storage.enableSaving = false;
            storage.Spawn();
            storage.inventory.playerOwner = player;

            DestroyOnGroundMissing bouyancy;
            if (storage.TryGetComponent<DestroyOnGroundMissing>(out bouyancy))
            {
                UnityEngine.Object.Destroy(bouyancy);
            }
            GroundWatch ridgidbody;
            if (storage.TryGetComponent<GroundWatch>(out ridgidbody))
            {
                UnityEngine.Object.Destroy(ridgidbody);
            }

            storage.gameObject.AddComponent<BoxController>();

            return storage;
        }

        private void StartLooting(BasePlayer player, StorageContainer storage)
        {
            if (player == null || storage == null || !player.IsAlive()) return;

            _viewingcon.Add(player);

            if (_viewingcon.Count == 1)
                SubscribeToHooks();

            //storage.SendAsSnapshot(player.Connection);

            if (player.inventory.loot.IsLooting())
                player.EndLooting();

            player.inventory.loot.Clear();
            player.inventory.loot.AddContainer(storage.inventory);
            player.inventory.loot.entitySource = storage;
            player.inventory.loot.PositionChecks = false;
            player.inventory.loot.MarkDirty();
            player.inventory.loot.SendImmediate();
            player.ClientRPCPlayer<string>(null, player, "RPC_OpenLootPanel", "generic_resizable");
        }

        #endregion Methods

        #region Spraycan Controller

        void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (newItem == null)
            {
                if (oldItem != null)
                {
                    if (oldItem.info?.itemid == -596876839)
                    {
                        SpraycanController spraycanController;
                        if (player.TryGetComponent<SpraycanController>(out spraycanController))
                        {
                            UnityEngine.GameObject.Destroy(spraycanController);
                        }
                    }
                }

                return;
            }

            if (newItem.info?.itemid == -596876839)
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
                if (oldItem.info.itemid == -596876839)
                {
                    SpraycanController spraycanController;
                    if (player.TryGetComponent<SpraycanController>(out spraycanController))
                    {
                        UnityEngine.GameObject.Destroy(spraycanController);
                    }
                }

            }
        }

        private class SpraycanController : FacepunchBehaviour
        {
            private BasePlayer player;
            private SprayCan sprayCan;

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

                skinner.SkinItemCMD(player.IPlayer, "", null);
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

        #region Controller
        private class BoxController : FacepunchBehaviour
        {
            private StorageContainer storageContainer;
            private BasePlayer player;
            private Vector3 ogpos;
            private Item mainitem = null;
            private int mainitemamt = 1;
            private Item returnitem = null;
            private Item returnitemplayer = null;
            public bool _fillingbox = false;
            public bool _clearingbox = false;
            private bool _redirectskin = false;
            private List<Item> _redirectitems = new List<Item>();
            private ItemDefinition itemselected = null;
            public BaseCombatEntity maindeployable = null;
            public string searchtxt = string.Empty;
            public int setSelect = 1;
            public Dictionary<string, CachedSkin> setSkins = new Dictionary<string, CachedSkin>();

            private int page = 0;

            private void Awake()
            {
                storageContainer = GetComponent<StorageContainer>();
                player = storageContainer.inventory.playerOwner;
                ogpos = player.transform.position;

                setSkins = skinner.GetCachedSkins(player);

                //disable stacks
                storageContainer.inventory.maxStackSize = 1;
                storageContainer.inventory.onPreItemRemove += new Action<Item>(Preremove);
            }

            private void Preremove(Item item)
            {
                if (item == mainitem)
                {
                    if (item.amount > 0)
                        return;

                    if (mainitemamt < 2)
                        return;

                    List<Item> items = player.inventory.FindItemsByItemID(item.info.itemid);
                    foreach (var item1 in items)
                    {
                        if (item1.skin != item.skin) continue;
                        item1.amount += mainitemamt - 1;
                        if (item1.amount > item1.info.stackable)
                        {
                            Item split = item1.SplitItem(item.info.stackable);
                            GiveItem(split);
                        }
                        item1.MarkDirty();
                        break;
                    }
                    return;
                }

                if (item.amount == 0)
                {
                    List<Item> items = player.inventory.FindItemsByItemID(item.info.itemid);
                    foreach (var item1 in items)
                    {
                        if (item1.skin != item.skin) continue;
                        item1.amount -= 1;
                        if (item1.amount < 1)
                            DoRemove(item1);
                        else
                            item1.MarkDirty();
                        break;
                    }
                }
            }

            #region Skin Deployables

            public void SkinDeplyoables(BaseCombatEntity entity)
            {
                storageContainer.inventory.onItemAddedRemoved += new Action<Item, bool>(CheckforItemDply);
                storageContainer.inventory.SetFlag(ItemContainer.Flag.IsLocked, true);
                maindeployable = entity;

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


                //CuiHelper.DestroyUi(player, "SkinSearchUI");
                if (_clearingbox)
                    return;

                searchtxt = string.Empty;
                if (maindeployable == null)
                {
                    Remove(item, true);
                    player.EndLooting();
                    return;
                }

                if (item == returnitemplayer)
                {
                    returnitemplayer = null;
                    return;
                }

                if (maindeployable.skinID != item.skin)
                {
                    //force refresh client skins
                    maindeployable.skinID = item.skin;

                    //Spray Can Effects
                    if (skinner.config.sprayCanEffect)
                    {
                        SprayCan can = player.GetHeldEntity() as SprayCan;
                        if (can != null)
                            can.ClientRPC<int, ulong>(null, "Client_ReskinResult", 1, maindeployable.net.ID.Value);
                    }
                    if (maindeployable.skinID == 0uL || maindeployable.skinID < 100000)
                    {
                        SendNetworkUpdate(maindeployable);
                    }
                    else
                        maindeployable.SendNetworkUpdateImmediate();
                }

                storageContainer.inventory.SetFlag(ItemContainer.Flag.IsLocked, true);
                returnitem = item;
                GetDeployableSkins();
                skinner.NextTick(() =>
                {
                    item.Remove(0f);
                });

            }

            private void GetDeployableSkins()
            {
                ItemDefinition itemdef = maindeployable.pickup.itemTarget;
                //Get Skins List
                List<CachedSkin> cachedskins = skinner._skinsCache[itemdef.shortname];

                if (!string.IsNullOrEmpty(searchtxt))
                {
                    List<CachedSkin> cachedskins2 = new List<CachedSkin>();
                    foreach (var s in cachedskins)
                    {
                        if (s.displayName.ToLower().Contains(searchtxt.ToLower()) || s.skinid.ToString().Contains(searchtxt))
                        {
                            cachedskins2.Add(s);
                        }
                    }
                    if (cachedskins2.Count > 0)
                        cachedskins = cachedskins2;

                }

                if (page > (cachedskins.Count - 1) / (storageContainer.inventorySlots - 1))
                    page = (cachedskins.Count - 1) / (storageContainer.inventorySlots - 1);

                //Search Bar UI
                if (skinner.config.searchbar)
                {
                    CuiHelper.DestroyUi(player, "SkinSearchUI");
                    CuiHelper.AddUi(player, skinner.CreateSearchBarUI());
                }

                //Check for UI
                if (cachedskins.Count > storageContainer.inventorySlots)
                {
                    CuiHelper.DestroyUi(player, "SkinPageUI");
                    CuiHelper.AddUi(player, skinner.CreatePageUI(page + 1, (cachedskins.Count - 1) / storageContainer.inventorySlots + 1));
                }

                //Fill container
                _fillingbox = true;

                int i;
                for (i = 0; i < storageContainer.inventorySlots && i < cachedskins.Count - (storageContainer.inventorySlots) * page; i++)
                {
                    //for refresh only refill empty slots
                    if (storageContainer.inventory.GetSlot(i) != null) continue;
                    CachedSkin cachedSkin = cachedskins[i + ((storageContainer.inventorySlots) * page)];
                    if (cachedSkin.redirect)
                    {
                        cachedskins.Remove(cachedSkin);
                        i -= 1;
                        continue;
                    }
                    InsertItem(cachedSkin, itemdef, 1, i);
                }
                _fillingbox = false;
                storageContainer.inventory.SetFlag(ItemContainer.Flag.IsLocked, false);
                storageContainer.inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);
                storageContainer.inventory.MarkDirty();
                storageContainer.SendNetworkUpdateImmediate();
            }

            #endregion Skin Deployables

            #region Skin Items

            public void StartItemSkin()
            {
                //enable stacks
                storageContainer.inventory.maxStackSize = 0;

                storageContainer.inventory.onItemAddedRemoved += new Action<Item, bool>(CheckforItem);
            }

            private void CheckforItem(Item item, bool b)
            {
                //if item removed
                if (!b)
                {
                    if (_clearingbox || mainitem == null)
                        return;

                    searchtxt = string.Empty;
                    CuiHelper.DestroyUi(player, "SkinSearchUI");
                    CuiHelper.DestroyUi(player, "SkinPageUI");

                    if (item == mainitem)
                    {
                        item.amount = mainitemamt;
                        item.MarkDirty();
                        mainitem = null;
                        mainitemamt = 1;
                        ClearCon();
                        storageContainer.inventory.maxStackSize = 0;
                        return;
                    }
                    ItemRemoveCheck(item);
                    return;
                }

                //if item added
                storageContainer.inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);
                storageContainer.inventory.SetFlag(ItemContainer.Flag.IsLocked, true);

                if (_fillingbox)
                    return;

                //if (item.info.stackable > 1)
                //{
                storageContainer.inventory.maxStackSize = 1;
                //}

                if (item.amount > 1)
                {
                    mainitemamt = item.amount;
                    item.amount = 1;
                }
                else
                    mainitemamt = 1;
                mainitem = item;
                GetSkins();
            }

            private void GetSkins()
            {
                ItemDefinition itemdef = mainitem.info;
                _redirectskin = false;

                //Blacklisted Skin
                if (skinner.config.blacklistedskins.Contains(mainitem.skin))
                {
                    skinner.NextTick(() =>
                    {
                        GiveItem(mainitem);
                    });
                    storageContainer.inventory.SetFlag(ItemContainer.Flag.NoItemInput, false);
                    storageContainer.inventory.SetFlag(ItemContainer.Flag.IsLocked, false);
                    return;
                }

                //No Skins Found
                if (!skinner._skinsCache.ContainsKey(mainitem.info.shortname))
                {
                    //No Skins available
                    if (!skinner._redirectSkins.ContainsKey(mainitem.info.shortname))
                    {
                        skinner.NextTick(() =>
                        {
                            GiveItem(mainitem);
                        });
                        storageContainer.inventory.SetFlag(ItemContainer.Flag.NoItemInput, false);
                        storageContainer.inventory.SetFlag(ItemContainer.Flag.IsLocked, false);
                        return;
                    }
                    else
                    {
                        //Get Redirect Skin
                        itemdef = ItemManager.FindItemDefinition(skinner._redirectSkins[mainitem.info.shortname]);
                        _redirectskin = true;
                    }
                }

                //Get Skins List
                List<CachedSkin> cachedskins = skinner._skinsCache[itemdef.shortname];

                if (!string.IsNullOrEmpty(searchtxt))
                {
                    List<CachedSkin> cachedskins2 = new List<CachedSkin>();
                    foreach (var s in cachedskins)
                    {
                        if (s.displayName.ToLower().Contains(searchtxt.ToLower()) || s.skinid.ToString().Contains(searchtxt))
                        {
                            cachedskins2.Add(s);
                        }
                    }

                    if (cachedskins2.Count > 0)
                        cachedskins = cachedskins2;

                }

                if (page > (cachedskins.Count - 1) / (storageContainer.inventorySlots - 1))
                    page = (cachedskins.Count - 1) / (storageContainer.inventorySlots - 1);

                //Search Bar UI
                if (skinner.config.searchbar)
                {
                    CuiHelper.DestroyUi(player, "SkinSearchUI");
                    CuiHelper.AddUi(player, skinner.CreateSearchBarUI());
                }


                //Check for UI
                if (cachedskins.Count + 1 > storageContainer.inventorySlots)
                {
                    CuiHelper.DestroyUi(player, "SkinPageUI");
                    CuiHelper.AddUi(player, skinner.CreatePageUI(page + 1, (cachedskins.Count - 1) / (storageContainer.inventorySlots - 1) + 1));
                }

                //Fill container
                _fillingbox = true;
                int amount = 1;
                //if (itemdef.stackable > 1)
                amount = mainitem.amount;
                int i;
                for (i = 0; i < storageContainer.inventorySlots - 1 && i < cachedskins.Count - (storageContainer.inventorySlots - 1) * page; i++)
                {
                    CachedSkin cachedskin = cachedskins[i + ((storageContainer.inventorySlots - 1) * page)];
                    InsertItem(cachedskin, itemdef, amount);
                }

                _fillingbox = false;
                storageContainer.inventory.SetFlag(ItemContainer.Flag.IsLocked, false);

                storageContainer.inventory.MarkDirty();
                storageContainer.SendNetworkUpdateImmediate();
            }

            private void ItemRemoveCheck(Item item)
            {
                if (_redirectitems.Contains(item))
                {
                    item.maxCondition = mainitem.maxCondition;
                    item.condition = mainitem.condition;
                    item.amount = mainitemamt;
                    if (item.contents?.itemList != null)
                    {
                        foreach (var con in mainitem.contents.itemList)
                        {
                            var newCon = ItemManager.Create(con.info, con.amount, con.skin);
                            newCon.condition = con.condition;
                            newCon.maxCondition = con.maxCondition;
                            newCon.MoveToContainer(item.contents);
                            newCon.MarkDirty();
                        }
                        item.contents.MarkDirty();
                    }
                    item.contents?.SetFlag(ItemContainer.Flag.NoItemInput, false);
                    item.contents?.SetFlag(ItemContainer.Flag.IsLocked, false);

                    BaseEntity held = item.GetHeldEntity();
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
                        //held.SendNetworkUpdate();
                    }
                    item.MarkDirty();
                    Remove(mainitem);
                    return;
                }
                else
                {
                    BaseEntity held = mainitem.GetHeldEntity();
                    mainitem.skin = item.skin;
                    if (held != null)
                    {
                        held.skinID = item.skin;
                        held.SendNetworkUpdateImmediate();
                    }
                    if (item != mainitem)
                    {
                        if (skinner.config.applySkinNames)
                        {
                            mainitem.info.displayName = item.info.displayName;
                            mainitem.name = item.name;
                        }

                        Remove(item, true);
                        mainitem.amount = mainitemamt;
                        GiveItem(mainitem);
                    }
                }

                mainitemamt = 1;
                mainitem = null;
            }

            #endregion Skin Items

            #region Set Default Skins

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

                if (_clearingbox)
                    return;


                if (item == returnitemplayer)
                {
                    returnitemplayer = null;
                    return;
                }

                storageContainer.inventory.SetFlag(ItemContainer.Flag.IsLocked, true);

                if (_clearingbox || _fillingbox)
                    return;

                //this should block item swaps
                _fillingbox = true;
                for (int i = 0; i < storageContainer.inventorySlots; i++)
                {
                    //fill empty slots
                    if (storageContainer.inventory.GetSlot(i) != null) continue;
                    Item dummyitem = ItemManager.Create(item.info, 1, 0);
                    dummyitem.MoveToContainer(storageContainer.inventory, i, false, false, null, false);
                }
                _fillingbox = false;

                skinner.NextTick(() =>
                {
                    item.Remove(0f);

                    ItemDefinition itemdef = item.info;
                    if (itemselected == null)
                    {
                        page = 0;
                        string origionalskin;
                        if (skinner._redirectSkins.TryGetValue(itemdef.shortname, out origionalskin))
                        {
                            itemdef = ItemManager.FindItemDefinition(origionalskin);
                        }
                        itemselected = itemdef;
                    }

                    //Remove(item, true);
                    ClearCon();

                    CuiHelper.DestroyUi(player, "SkinPageUI");

                    //Get Skins List
                    List<CachedSkin> cachedskins = skinner._skinsCache[itemdef.shortname];

                    if (!string.IsNullOrEmpty(searchtxt))
                    {
                        List<CachedSkin> cachedskins2 = new List<CachedSkin>();
                        foreach (var s in cachedskins)
                        {
                            if (s.displayName.ToLower().Contains(searchtxt.ToLower()) || s.skinid.ToString().Contains(searchtxt))
                            {
                                cachedskins2.Add(s);
                            }
                        }

                        if (cachedskins2.Count > 0)
                            cachedskins = cachedskins2;

                    }

                    if (page > (cachedskins.Count - 1) / (storageContainer.inventorySlots - 1))
                        page = (cachedskins.Count - 1) / (storageContainer.inventorySlots - 1);

                    //Search Bar UI
                    if (skinner.config.searchbar)
                    {
                        CuiHelper.DestroyUi(player, "SkinSearchUI");
                        CuiHelper.AddUi(player, skinner.CreateSearchBarUI());
                    }
                    if (page > (cachedskins.Count - 1) / storageContainer.inventorySlots)
                        page = (cachedskins.Count - 1) / storageContainer.inventorySlots;

                    //Check for UI
                    if (cachedskins.Count + 1 > storageContainer.inventorySlots)
                    {
                        CuiHelper.AddUi(player, skinner.CreatePageUI(page + 1, (cachedskins.Count - 1) / storageContainer.inventorySlots + 1));
                    }
                    _fillingbox = true;
                    int i;
                    for (i = 0; i < storageContainer.inventorySlots && i < cachedskins.Count - (storageContainer.inventorySlots) * page; i++)
                    {
                        CachedSkin cachedskin = cachedskins[i + ((storageContainer.inventorySlots) * page)];
                        InsertItem(cachedskin, itemdef);
                    }
                    _fillingbox = false;

                    storageContainer.inventory.onItemAddedRemoved -= new Action<Item, bool>(CheckforItemSelect);
                    storageContainer.inventory.onItemAddedRemoved += new Action<Item, bool>(CheckforSkinSelect);
                    storageContainer.inventory.SetFlag(ItemContainer.Flag.IsLocked, false);
                    storageContainer.inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);
                    storageContainer.inventory.MarkDirty();
                    storageContainer.SendNetworkUpdateImmediate();
                });
            }

            private void CheckforSkinSelect(Item item, bool b)
            {
                if (item == null)
                    return;
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

                if (_clearingbox)
                    return;

                if (item == returnitemplayer)
                {
                    returnitemplayer = null;
                    return;
                }

                searchtxt = string.Empty;
                storageContainer.inventory.SetFlag(ItemContainer.Flag.IsLocked, true);

                //this should block item swaps
                _fillingbox = true;
                for (int i = 0; i < storageContainer.inventorySlots; i++)
                {
                    //fill empty slots
                    if (storageContainer.inventory.GetSlot(i) != null) continue;
                    Item dummyitem = ItemManager.Create(item.info, 1, 0);
                    dummyitem.MoveToContainer(storageContainer.inventory, i, false, false, null, false);
                }
                _fillingbox = false;

                skinner.NextTick(() =>
                {
                    item.Remove(0f);

                    if (_clearingbox || _fillingbox || item == null)
                        return;

                    bool flag1 = skinner._redirectSkins.ContainsKey(item.info.shortname);
                    if (item.skin == 0 && !flag1)
                    {
                        if (setSkins.ContainsKey(itemselected.shortname))
                        {
                            setSkins.Remove(itemselected.shortname);
                        }
                    }
                    else
                    {
                        List<CachedSkin> cachelist = new List<CachedSkin>();
                        CachedSkin cachedskin = new CachedSkin();
                        if (skinner._skinsCache.TryGetValue(itemselected.shortname, out cachelist))
                        {
                            if (!flag1)
                                cachedskin = cachelist.Find(x => x.skinid == item.skin);
                            else
                            {
                                cachedskin = cachelist.Find(x => x.redirectshortname == item.info.shortname);
                            }
                        }
                        //Use real short name here to avoid redirect skins
                        setSkins[itemselected.shortname] = cachedskin;
                    }

                    itemselected = null;

                    //Remove(item, true);

                    ClearCon();
                    page = 0;
                    GetDefaultSkins();
                });
            }

            public void GetDefaultSkins()
            {
                CuiHelper.DestroyUi(player, "SkinSetsSelectUI");
                CuiHelper.AddUi(player, skinner.CreateSetsUI(setSelect));

                storageContainer.inventory.SetFlag(ItemContainer.Flag.IsLocked, true);

                Dictionary<string, CachedSkin> defaultskins = new Dictionary<string, CachedSkin>();
                CachedSkin defaults;

                foreach (KeyValuePair<string, CachedSkin> item in skinner._defaultskins)
                {
                    if (setSkins.TryGetValue(item.Key, out defaults))
                    {
                        if (defaults != null)
                            defaultskins[item.Key] = defaults;
                        continue;
                    }
                    defaultskins[item.Key] = item.Value;
                }

                if (page > (defaultskins.Count - 1) / (storageContainer.inventorySlots))
                    page = (defaultskins.Count - 1) / (storageContainer.inventorySlots);


                if (defaultskins.Count > storageContainer.inventorySlots)
                {
                    CuiHelper.DestroyUi(player, "SkinPageUI");
                    CuiHelper.AddUi(player, skinner.CreatePageUI(page + 1, (skinner._skinsCache.Keys.Count - 1) / storageContainer.inventorySlots + 1));
                }

                int i;
                _fillingbox = true;
                for (i = 0; i < storageContainer.inventorySlots && i < defaultskins.Count - (storageContainer.inventorySlots) * page; i++)
                {
                    CachedSkin cachedskin = defaultskins.Values.ElementAt(i + ((storageContainer.inventorySlots) * page));
                    ItemDefinition itemdef;

                    if (cachedskin.redirect)
                    {
                        itemdef = ItemManager.FindItemDefinition(cachedskin.redirectshortname);
                    }
                    else
                    {
                        itemdef = ItemManager.FindItemDefinition(cachedskin.shortname);
                    }
                    InsertItem(cachedskin, itemdef);
                }

                _fillingbox = false;
                storageContainer.inventory.onItemAddedRemoved -= new Action<Item, bool>(CheckforSkinSelect);
                storageContainer.inventory.onItemAddedRemoved += new Action<Item, bool>(CheckforItemSelect);

                storageContainer.inventory.SetFlag(ItemContainer.Flag.IsLocked, false);
                storageContainer.inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);
                storageContainer.inventory.MarkDirty();
                storageContainer.SendNetworkUpdateImmediate();
            }

            #endregion  Set Default Skins

            #region UI
            public void NextPage()
            {
                page += 1;
                storageContainer.inventory.SetFlag(ItemContainer.Flag.IsLocked, true);
                if (maindeployable != null)
                {
                    ClearCon();
                    skinner.NextTick(() =>
                    {
                        GetDeployableSkins();
                    });
                    return;
                }

                if (mainitem == null)
                {
                    if (itemselected == null)
                    {
                        ClearCon();
                        storageContainer.inventory.onItemAddedRemoved -= new Action<Item, bool>(CheckforItemSelect);
                        GetDefaultSkins();
                        return;
                    }
                    Item dummy = ItemManager.Create(itemselected, 1);
                    storageContainer.inventory.SetFlag(ItemContainer.Flag.IsLocked, true);
                    storageContainer.inventory.onItemAddedRemoved -= new Action<Item, bool>(CheckforSkinSelect);
                    CheckforItemSelect(dummy, false);
                    return;
                }

                ClearCon(false, true);
                skinner.NextTick(() =>
                {
                    GetSkins();
                });
            }

            public void BackPage()
            {
                page -= 1;
                storageContainer.inventory.SetFlag(ItemContainer.Flag.IsLocked, true);
                if (page < 0)
                    page = 0;

                if (maindeployable != null)
                {
                    ClearCon();
                    GetDeployableSkins();
                    return;
                }

                if (mainitem == null)
                {
                    if (itemselected == null)
                    {
                        ClearCon();
                        storageContainer.inventory.onItemAddedRemoved -= new Action<Item, bool>(CheckforItemSelect);
                        GetDefaultSkins();
                        return;
                    }
                    Item dummy = ItemManager.Create(itemselected, 1);
                    storageContainer.inventory.SetFlag(ItemContainer.Flag.IsLocked, true);
                    storageContainer.inventory.onItemAddedRemoved -= new Action<Item, bool>(CheckforSkinSelect);
                    CheckforItemSelect(dummy, false);
                    return;
                }

                ClearCon(false, true);
                skinner.NextTick(() =>
                {
                    GetSkins();
                });
            }

            public void SearchUpdate()
            {
                CuiHelper.DestroyUi(player, "SkinSetsSelectUI");
                setSkins = skinner.GetCachedSkins(player);

                if (mainitem == null && maindeployable == null)
                    CuiHelper.AddUi(player, skinner.CreateSetsUI(setSelect));


                page = 0;
                storageContainer.inventory.SetFlag(ItemContainer.Flag.IsLocked, true);

                if (maindeployable != null)
                {
                    ClearCon();
                    GetDeployableSkins();
                    return;
                }

                if (mainitem == null)
                {
                    if (itemselected == null)
                    {
                        ClearCon();
                        storageContainer.inventory.onItemAddedRemoved -= new Action<Item, bool>(CheckforItemSelect);
                        GetDefaultSkins();
                        return;
                    }
                    Item dummy = ItemManager.Create(itemselected, 1);
                    storageContainer.inventory.SetFlag(ItemContainer.Flag.IsLocked, true);
                    storageContainer.inventory.onItemAddedRemoved -= new Action<Item, bool>(CheckforSkinSelect);
                    CheckforItemSelect(dummy, false);
                    return;
                }

                ClearCon(false, true);

                skinner.NextTick(() =>
                {
                    GetSkins();
                });
            }

            #endregion UI

            #region Helpers
            private void GiveItem(Item item)
            {
                if (item.MoveToContainer(player.inventory.containerMain, -1, false, false, null, false))
                {
                    return;
                }
                if (item.MoveToContainer(player.inventory.containerBelt, -1, false, false, null, false))
                {
                    return;
                }
                item.Drop(player.IsAlive() ? player.transform.position : ogpos, player.inventory.containerMain.dropVelocity, new Quaternion());
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
            private void ClearCon(bool nexttick = false, bool skipmainitem = false)
            {
                if (nexttick)
                {
                    skinner.NextTick(() => ClearCon());
                    return;
                }
                storageContainer.inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);
                storageContainer.inventory.SetFlag(ItemContainer.Flag.IsLocked, true);
                _clearingbox = true;
                foreach (var item in storageContainer.inventory.itemList)
                {
                    if (item == null) continue;
                    if (!skipmainitem || item != mainitem) item.Remove();
                }
                ItemManager.DoRemoves();
                storageContainer.inventory.MarkDirty();
                storageContainer.SendNetworkUpdateImmediate();
                _redirectitems.Clear();
                _clearingbox = false;
                storageContainer.inventory.SetFlag(ItemContainer.Flag.NoItemInput, false);
                storageContainer.inventory.SetFlag(ItemContainer.Flag.IsLocked, false);
            }

            private void InsertItem(CachedSkin cachedSkin, ItemDefinition itemDef, int amount = 1, int pos = -1)
            {
                ulong skinid = cachedSkin.skinid;
                ItemDefinition itemdef2 = itemDef;

                //Get redirect item def
                if (cachedSkin.redirect)
                    itemdef2 = ItemManager.FindItemDefinition(cachedSkin.redirectshortname);

                Item item = ItemManager.Create(itemdef2, amount, skinid);

                if (pos == -1)
                {
                    if (!item.MoveToContainer(storageContainer.inventory, -1, false))
                    {
                        Remove(item, true);
                        return;
                    }
                }
                else
                {
                    item.position = pos;
                    item.SetParent(storageContainer.inventory);
                }

                if (cachedSkin.redirect || _redirectskin)
                    _redirectitems.Add(item);
                else
                    item.name = cachedSkin.displayName;

                //Lock mod slots
                item.contents?.SetFlag(ItemContainer.Flag.NoItemInput, true);
                item.contents?.SetFlag(ItemContainer.Flag.IsLocked, true);

                //Update held skins
                BaseEntity held = item.GetHeldEntity();
                if (held != null)
                {
                    //held.skinID = skinid;
                    //Remove Bullets
                    BaseProjectile baseProjectile = held as BaseProjectile;
                    if (baseProjectile != null)
                    {
                        baseProjectile.canUnloadAmmo = false;
                    }
                    //held.SendNetworkUpdate();
                }
                item.MarkDirty();
            }

            //Refresh skins so they dont show as missing textures on client side for deployables
            private void SendNetworkUpdate(BaseEntity ent)
            {
                if (Net.sv.IsConnected())
                {
                    NetWrite netWrite = Net.sv.StartWrite();
                    netWrite.PacketID(Message.Type.EntityDestroy);
                    netWrite.EntityID(ent.net.ID);
                    netWrite.UInt8(0);
                    netWrite.Send(new SendInfo(player.net.group.subscribers.ToList()));
                }


                ent.OnNetworkGroupLeave(ent.net.group);
                ent.InvalidateNetworkCache();

                List<Connection> subscribers = ent.GetSubscribers();
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
                ent.gameObject.SendOnSendNetworkUpdate(ent as BaseEntity);
            }

            public void OnDestroy()
            {
                _clearingbox = true;
                if (mainitem != null)
                {
                    mainitem.amount = mainitemamt;
                    GiveItem(mainitem);
                }
                ClearCon();
                storageContainer.Kill();
                CuiHelper.DestroyUi(player, "SkinPageUI");
                CuiHelper.DestroyUi(player, "SkinSearchUI");
                CuiHelper.DestroyUi(player, "SkinSetsSelectUI");
                GameObject.Destroy(this);
            }
            #endregion Helpers
        }

        #endregion Controller

        #region GUI Panel
        // Cached UI
        public CuiElementContainer CreatePageUI(int pagecurr, int pagemax)
        {
            CuiElementContainer elements = new CuiElementContainer();
            string panel = elements.Add(new CuiPanel
            {
                Image = { Color = "0.5 0.5 0.5 0.3" },
                RectTransform = { AnchorMin = config.uiposition[0], AnchorMax = config.uiposition[1] }
            }, "Hud.Menu", "SkinPageUI");

            elements.Add(new CuiLabel
            {
                Text = { Text = $"{pagecurr} of {pagemax}", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 0.75" },
                RectTransform = { AnchorMin = "0.30 0.00", AnchorMax = "0.70 1.0" }
            }, panel);

            string cmdback = "sbBackPage";
            if (pagecurr == 1)
                cmdback = "";

            elements.Add(new CuiButton
            {
                Button = { Command = cmdback, Color = "0.5 0.5 0.5 0.0" },
                Text = { Text = "←", FontSize = 25, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 0.75" },
                RectTransform = { AnchorMin = "0.00 0.00", AnchorMax = "0.30 1.0" }
            }, panel);

            string cmdfwd = "sbNextPage";
            if (pagecurr == pagemax)
                cmdfwd = "";

            elements.Add(new CuiButton
            {
                Button = { Command = cmdfwd, Color = "0.5 0.5 0.5 0.0" },
                Text = { Text = "→", FontSize = 25, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 0.75" },
                RectTransform = { AnchorMin = "0.70 0.00", AnchorMax = "1.0 1.0" }
            }, panel);
            return elements;
        }

        public CuiElementContainer CreateSearchBarUI(string lastsearch = " Search Name or ID")
        {
            CuiElementContainer elements = new CuiElementContainer();
            string panel = elements.Add(new CuiPanel
            {
                Image = { Color = "0.5 0.5 0.5 0.5" },
                RectTransform = { AnchorMin = config.uisearchposition[0], AnchorMax = config.uisearchposition[1] }
            }, "Hud.Menu", "SkinSearchUI");
            elements.Add(new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = panel,
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Align = TextAnchor.MiddleLeft,
                        Color = "#FFFFFF",
                        Command = "searchCMD",
                        FontSize = 14,
                        IsPassword = false,
                        Text = lastsearch,
                        HudMenuInput = true
                    },
                    new CuiRectTransformComponent {AnchorMin = "0.00 0.00", AnchorMax = "1.00 1.00" }
                }
            });
            elements.Add(new CuiButton
            {
                Button = { Command = "searchCMD", Color = "0.9 0.1 0.1 1.0" },
                Text = { Text = "x", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 0.75" },
                RectTransform = { AnchorMin = "0.85 0.00", AnchorMax = "1.0 0.98" }
            }, panel);
            return elements;
        }

        public CuiElementContainer CreateSetsUI(int selectedset = 1)
        {
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


            CuiElementContainer elements = new CuiElementContainer();
            string panel = elements.Add(new CuiPanel
            {
                Image = { Color = "0.5 0.5 0.5 0.5" },
                RectTransform = { AnchorMin = config.uisetsposition[0], AnchorMax = config.uisetsposition[1] }
            }, "Hud.Menu", "SkinSetsSelectUI");
            elements.Add(new CuiButton
            {
                Button = { Command = "setSelectCMD 1", Color = color1 },
                Text = { Text = "Set 1", FontSize = 17, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 0.75" },
                RectTransform = { AnchorMin = "0.00 0.00", AnchorMax = "0.31 0.98" }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "setSelectCMD 2", Color = color2 },
                Text = { Text = "Set 2", FontSize = 17, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 0.75" },
                RectTransform = { AnchorMin = "0.34 0.00", AnchorMax = "0.65 0.98" }
            }, panel);
            elements.Add(new CuiButton
            {
                Button = { Command = "setSelectCMD 3", Color = color3 },
                Text = { Text = "Set 3", FontSize = 17, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 0.75" },
                RectTransform = { AnchorMin = "0.68 0.00", AnchorMax = "1.00 0.98" }
            }, panel);
            return elements;
        }
        #endregion GUI Panel

        #region Helpers
        private void SendNetworkUpdate(BaseEntity ent, BasePlayer player)
        {
            if (Net.sv.IsConnected())
            {
                NetWrite netWrite = Net.sv.StartWrite();
                netWrite.PacketID(Message.Type.EntityDestroy);
                netWrite.EntityID(ent.net.ID);
                netWrite.UInt8(0);
                netWrite.Send(new SendInfo(player.net.group.subscribers.ToList()));
            }

            ent.OnNetworkGroupLeave(ent.net.group);
            ent.InvalidateNetworkCache();

            List<Connection> subscribers = ent.GetSubscribers();
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
                SendNetworkUpdate(child, player);
            }
            ent.gameObject.SendOnSendNetworkUpdate(ent as BaseEntity);
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

        private void UnSubscribeFromHooks()
        {
            foreach (var hook in _registeredhooks)
                Unsubscribe(hook);
        }
        private void SubscribeToHooks()
        {
            foreach (var hook in _registeredhooks)
                Subscribe(hook);
        }
        private bool HasPerm(string id, string perm) => permission.UserHasPermission(id, perm);

        private string GetLang(string langKey, string playerId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(langKey, this, playerId), args);
        }
        private void ChatMessage(IPlayer player, string langKey, params object[] args)
        {
            if (player.IsConnected) player.Message(GetLang(langKey, player.Id, args));
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
            string vurl = "https://steamcommunity.com/sharedfiles/filedetails/?id={0}";
            for (int i = 0; i < _WorkshopSkinIDCollectionList.Count; i++)
            {
                var workshopid = _WorkshopSkinIDCollectionList[i];
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
                    yield return new WaitForSeconds(30f);
                    continue;
                }
                downloadHandler = www.downloadHandler.text;
                string[] htmlslines = downloadHandler.Split('\n');
                bool titlef = false;
                string skinname = "";
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
                    if (trimsplits[1] == "workshopTags")
                    {
                        skintype = trimmed.Split('>')[6].Split('<')[0];
                        if (skintype == "Skin" || skintype == "Version3")
                            skintype = trimmed.Split('>')[8].Split('<')[0];
                        if (skintype == "Skin" || skintype == "Version3")
                            skintype = trimmed.Split('>')[10].Split('<')[0];
                    }

                    if (trimsplits[3] == "workshopTags")
                    {
                        skintype = trimmed.Split('>')[4].Split('<')[0];
                        if (skintype == "Skin" || skintype == "Version3")
                            skintype = trimmed.Split('>')[6].Split('<')[0];
                        if (skintype == "Skin" || skintype == "Version3")
                            skintype = trimmed.Split('>')[8].Split('<')[0];
                    }

                    if (string.IsNullOrEmpty(skintype)) continue;

                    string shortname = string.Empty;
                    if (!WorkshopSkinNameConversion.TryGetValue(skintype, out shortname))
                    {
                        Puts($"Cannot find item definition for id: {workshopid} type:{skintype}");
                        break;
                    }

                    ulong uworkshopid;
                    if (ulong.TryParse(workshopid, out uworkshopid))
                    {
                        config.ImportedSkinList[uworkshopid] = new ImportedItem()
                        { itemDisplayname = skinname, itemShortname = shortname };
                    }
                    else
                    {
                        Puts("Failed to parse workshop ID" + workshopid);
                    }
                    break;
                }
                yield return new WaitForSeconds(0.001f);
            }
            getSteamWorkshopSkinData = null;
            _WorkshopSkinIDCollectionList.Clear();
            GetSkins();
        }

        private Dictionary<string, string> WorkshopSkinNameConversion = new Dictionary<string, string>
        {
            {"Acoustic Guitar","fun.guitar"},
            {"AK47","rifle.ak"},
            {"Armored Double Door", "door.double.hinged.toptier"},
            {"Armored Door","door.hinged.toptier"},
            {"Balaclava","mask.balaclava"},
            {"Bandana","mask.bandana"},
            {"Bearskin Rug", "rug.bear"},
            {"Beenie Hat","hat.beenie"},
            {"Bolt Rifle","rifle.bolt"},
            {"Bone Club","bone.club"},
            {"Bone Knife","knife.bone"},
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
            {"Rocket Launcher","rocket.launcher"},
            {"Rug", "rug"},
            {"Rug Bear Skin","rug.bear"},
            {"Salvaged Hammer","hammer.salvaged"},
            {"Salvaged Icepick","icepick.salvaged"},
            {"Sandbag Barricade","barricade.sandbags"},
            {"Satchel Charge","explosive.satchel"},
            {"Semi-Automatic Pistol","pistol.semiauto"},
            {"Semi-Automatic Rifle","rifle.semiauto"},
            {"Sheet Metal Door","door.hinged.metal"},
            {"Sheet Metal Double Door","door.double.hinged.metal"},
            {"Shorts","pants.shorts"},
            {"Sleeping Bag","sleepingbag"},
            {"Snow Jacket","jacket.snow"},
            {"Stone Hatchet","stonehatchet"},
            {"Stone Pick Axe","stone.pickaxe"},
            {"Sword","salvaged.sword"},
            {"Table", "table"},
            {"Tank Top","shirt.tanktop"},
            {"Thompson","smg.thompson"},
            {"TShirt","tshirt"},
            {"Vagabond Jacket","jacket"},
            {"Vending Machine","vending.machine"},
            {"Water Purifier","water.purifier"},
            {"Waterpipe Shotgun","shotgun.waterpipe"},
            {"Wood Storage Box","box.wooden"},
            {"Wooden Door","door.hinged.wood"},
            {"Wood Double Door", "door.double.hinged.wood" },
            {"Work Boots","shoes.boots"}
        };

        #endregion SteamWorkshop WebRequests

        #region Public Helpers
        public Dictionary<string, List<CachedSkin>> GetAllCachedSkins()
        {
            return _skinsCache;
        }

        public List<CachedSkin> GetSkinsItemList(string itemshortname)
        {
            List<CachedSkin> cachedSkins = new List<CachedSkin>();
            _skinsCache.TryGetValue(itemshortname, out cachedSkins);
            return cachedSkins;
        }
        #endregion Public Helpers
    }
}
/* Boosty - https://boosty.to/skulidropek 
Discord - https://discord.gg/k3hXsVua7Q 
Discord The Rust Bay - https://discord.gg/Zq3TVjxKWk  */