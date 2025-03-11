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
using Facepunch;

namespace Oxide.Plugins
{
    [Info("LoadoutController", "Amino", "2.0.7")]
    [Description("An advanced loadout system for rust")]
    public class LoadoutController : RustPlugin
    {
        [PluginReference] Plugin WelcomeController, ImageLibrary;
        #region Config
        public class Configuration
        {
            public List<string> LoadoutCommands { get; set; } = new List<string>();
            [JsonProperty("X Image")]
            public string XEmoji { get; set; }
            [JsonProperty("Check Image")]
            public string CheckEmoji { get; set; }
            [JsonProperty("! Image")]
            public string ExclamationEmoji { get; set; }
            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    LoadoutCommands = new List<string> { "loadout", "loadouts", "lo" },
                    XEmoji = "https://i.ibb.co/WsbrbR6/cross-mark-emoji-2048x2048-tkn63gln.png",
                    CheckEmoji = "https://i.ibb.co/m4Lvcj9/check-mark-2714-fe0f.png",
                    ExclamationEmoji = "https://i.ibb.co/wsbH6Fy/8044-heavy-exclamation-mark-symbol-1.png"
                };
            }
        }

        private static Configuration _config;
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) LoadDefaultConfig();
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

        #region Constructors
        private bool usingWC = false;
        #endregion

        #region Data
        public List<LoadoutOption> _loadoutOptions = new List<LoadoutOption>();
        public Dictionary<ulong, PlayerLoadoutOption> _playerLoadoutOptions = new Dictionary<ulong, PlayerLoadoutOption>();

        public class SavedItem
        {
            public string Category { get; set; }
            public int Position { get; set; }
            public string Shortname { get; set; }
            public int ItemID { get; set; }
            public ulong SkinID { get; set; }
            public int Amount { get; set; }
            public List<int> Mods { get; set; }
            public string AmmoType { get; set; }
            public int AmmoAmount { get; set; }
        }

        public class LoadoutOption
        {
            public int Priority { get; set; } = 1;
            public string Permission { get; set; } = "";
            [JsonProperty(PropertyName = "Allowed to save loadouts")]
            public bool SaveLoadouts { get; set; } = true;
            public int MaxSaves { get; set; } = 10;
            [JsonProperty(PropertyName = "Max items per category")]
            public Dictionary<string, int> MaxItemsCat = new Dictionary<string, int>();
            [JsonProperty(PropertyName = "Max Items Per Shortname")]
            public Dictionary<string, int> MaxItemsSN = new Dictionary<string, int>();
            [JsonProperty(PropertyName = "Blocked Skins IDs")]
            public List<ulong> BlockedSkins = new List<ulong>();
            public Dictionary<string, bool> AllowedLoadedAmmoTypes = new Dictionary<string, bool>();
            public List<SavedItem> Loadout = new List<SavedItem>();
        }

        public class PlayerLoadoutOption
        {
            public string ActiveLoadout { get; set; }
            public string NewSaveName { get; set; }
            public List<PlayerLoadoutSaved> Loadouts = new List<PlayerLoadoutSaved>();
        }

        public class PlayerLoadoutSaved
        {
            public string SavedPerm { get; set; }
            public string SaveName { get; set; }
            public List<SavedItem> Items = new List<SavedItem>();
        }
        #endregion

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Title"] = "LOADOUTS",
                ["InvalidSavePerms"] = "You are not allowed to save loadouts!",
                ["MaxLoadoutLimit"] = "You have reached the max loadout limit!",
                ["NoLoadoutName"] = "You have not set a save name for the loadout",
                ["LoadoutNameAlreadySet"] = "You already have a loadout with this name",
                ["LoadoutNameTooLong"] = "Your loadout name is too long",
                ["AdminSettings"] = "ADMIN SETTINGS",
                ["AdminDefaultSave"] = "ADMIN DEFAULT SAVE",
                ["AdminFollowRestrictions"] = "FOLLOW LOADOUT RESTRICTIONS",
                ["AdminLoadoutRestrictions"] = "IGNORE LOADOUT RESTRICTIONS",
                ["SaveNewLoadout"] = "SAVE NEW LOADOUT",
                ["SaveName"] = "SAVE NAME",
                ["SaveLoadout"] = "SAVE LOADOUT",
                ["PresetOption"] = "PRESET OPTION",
                ["InputName"] = "INPUT NAME",
                ["OverrideLoadout"] = "OVERRIDE LOADOUT",
                ["SelectPersonalLoadout"] = "SELECT PERSONAL LOADOUT",
                ["NoPersonalLoadout"] = "NO PERSONAL LOADOUT",
                ["NoDefaultLoadout"] = "NO DEFAULT LOADOUT",
                ["Inventory"] = "INVENTORY",
                ["CouldNotFindOverride"] = "Could not find the requested loadout to override...",
                ["BackButton"] = "BACK"
            }, this, "en");
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        #endregion

        #region Hooks
        void OnServerInitialized(bool initial)
        {
            if(ImageLibrary == null || !ImageLibrary.IsLoaded)
            {
                Puts("Cannot start plugin because Image Library is not found!");
                return;
            }

            var isUsingWC = Interface.Call("IsUsingPlugin", "LoadoutController");
            if (isUsingWC != null && (isUsingWC is bool))
            {
                usingWC = (bool)isUsingWC;
            }
            else usingWC = false;

            RegisterImages();
            LoadLoadoutOptions();
            RegisterPermissions();
            RegisterCommands();
        }

        void OnWCRequestedUIPanel(BasePlayer player, string panelName, string neededPlugin)
        {
            if (!neededPlugin.Equals("LoadoutController", StringComparison.OrdinalIgnoreCase)) return;
            usingWC = true;
            OpenLoadoutMenu(player, null, null);
        }

        void OnPluginLoaded(Plugin plugin)
        {
            if (plugin.Name == "WelcomeController")
            {
                var isUsingWC = Interface.Call("IsUsingPlugin", "LoadoutController");
                if (isUsingWC != null && (isUsingWC is bool))
                {
                    usingWC = (bool)isUsingWC;
                }
                else usingWC = false;
            }
        }

        void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin.Name == "WelcomeController")
            {
                usingWC = false;
                RegisterCommands();
            }
        }

        private void Unload()
        {
            if (!Interface.Oxide.IsShuttingDown)
            {
                SaveLoadoutOptions();
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(player, "LCMainPanel");
                    CuiHelper.DestroyUi(player, "LCOverlayPanel");
                    SavePlayerLoadouts(player);
                }
            }

            _config = null;
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            var playerGroup = CheckPlayerPermission(player);
            var playerLoadout = GetOrLoadPlayerLoadout(player);

            var loadout = new List<SavedItem>();
            if (playerGroup != null) loadout = playerGroup.Loadout;
            if (playerLoadout.ActiveLoadout != null)
            {
                var tempLoadout = playerLoadout.Loadouts.FirstOrDefault(x => x.SaveName == playerLoadout.ActiveLoadout);
                if (tempLoadout != null) loadout = tempLoadout.Items;
            }

            int i = 0;
            if (loadout.Count > 0)
            {
                var itemsToAdd = new Dictionary<string, Item>();

                List<Item> itemsToRemove = Pool.Get<List<Item>>();
                player.inventory.GetAllItems(itemsToRemove);

                foreach (var item in loadout)
                {
                    var theItem = ItemManager.CreateByItemID(item.ItemID, item.Amount, item.SkinID);

                    theItem.position = item.Position;
                    if (theItem.info.category.ToString() == "Weapon")
                    {
                        if (item.Mods != null)
                            foreach (var attachment in item.Mods)
                                theItem.contents.AddItem(ItemManager.FindItemDefinition(attachment), 1);

                        if (item.AmmoType != null)
                        {
                            var weapon = theItem.GetHeldEntity() as BaseProjectile;
                            weapon.primaryMagazine.ammoType = ItemManager.FindItemDefinition(item.AmmoType);
                            weapon.primaryMagazine.contents = item.AmmoAmount;
                        }
                    }

                    itemsToAdd.Add($"{item.Category}_{i}", theItem);
                    i++;
                }

                player.inventory.Strip();
                
                foreach (var item in itemsToAdd)
                {
                    if (item.Key.Contains("attire")) item.Value.MoveToContainer(player.inventory.containerWear, item.Value.position);
                    else if (item.Key.Contains("main")) item.Value.MoveToContainer(player.inventory.containerMain, item.Value.position);
                    else if (item.Key.Contains("hotbar")) item.Value.MoveToContainer(player.inventory.containerBelt, item.Value.position);
                }
                player.inventory.containerWear.MarkDirty();
                player.inventory.containerMain.MarkDirty();
                player.inventory.containerBelt.MarkDirty();

                itemsToRemove.Clear();
                Pool.FreeUnsafe(ref itemsToRemove);
            }
        }

        void OnServerSave()
        {
            SaveLoadoutOptions();
            foreach (BasePlayer player in BasePlayer.activePlayerList) SavePlayerLoadouts(player); 
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null || !player.userID.Get().IsSteamId()) return;
            SavePlayerLoadouts(player);

            _playerLoadoutOptions.Remove(player.userID);
        }

        void OnPlayerConnected(BasePlayer player)
        {
            GetOrLoadPlayerLoadout(player);
        }
        #endregion

        #region Commands
        [ConsoleCommand("lc_main")]
        private void CmdOverrideLoadout(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (arg == null || player == null) return;

            var HasPerms = CheckPlayerPermission(player);
            var playerLoadout = GetOrLoadPlayerLoadout(player);
            switch (arg.Args[0])
            {
                case "close":
                    CuiHelper.DestroyUi(player, "LCMainPanel");
                    break;
                case "adminclose":
                    CuiHelper.DestroyUi(player, "LCAdminSelection");
                    break;
                case "loadoutspage":
                    UICreateSelectionPanel(player, "LCOverlayPanel", playerLoadout, int.Parse(arg.Args[1]));
                    break;
                case "adminpage":
                    UICreateAdminLoadoutPanel(player, int.Parse(arg.Args[1]));
                    break;
                case "adminselect":
                    UICreateAdminSelection(player, arg.Args[1]);
                    break; 
                case "overridepage":
                    UICreateOverrideLoadoutPanel(player, playerLoadout, int.Parse(arg.Args[1]));
                    break;
                case "followrestrictions":
                    SaveNewLoadout(player, arg.Args[1], HasPerms, playerLoadout, false, true);
                    CuiHelper.DestroyUi(player, "LCAdminSelection");
                    break;
                case "ignorerestrictions":
                    IgnoreLoadoutOptions(player, arg.Args[1]);
                    CuiHelper.DestroyUi(player, "LCAdminSelection");
                    break;
                case "deleteloadout":
                    if(playerLoadout.ActiveLoadout == string.Join(" ", arg.Args.Skip(1))) playerLoadout.ActiveLoadout = null;
                    playerLoadout.Loadouts.RemoveAll(x => x.SaveName == string.Join(" ", arg.Args.Skip(1)));

                    UICreatePersonalLoadoutPanel(player, "LCOverlayPanel", playerLoadout);
                    UICreateSelectionPanel(player, "LCOverlayPanel", playerLoadout);
                    break;
                case "selectloadout":
                    playerLoadout.ActiveLoadout = string.Join(" ", arg.Args.Skip(1));
                    UICreatePersonalLoadoutPanel(player, "LCOverlayPanel", playerLoadout);
                    UICreateSelectionPanel(player, "LCOverlayPanel", playerLoadout);
                    break;
                case "back":
                    CuiHelper.DestroyUi(player, "LCSavePanel");
                    UICreateDefaultLoadoutPanel(player, "LCOverlayPanel", HasPerms);
                    UICreatePersonalLoadoutPanel(player, "LCOverlayPanel", playerLoadout);
                    UICreateSelectionPanel(player, "LCOverlayPanel", playerLoadout);
                    break;
                case "saveloadout":
                    CuiHelper.DestroyUi(player, "LCLoadoutSelectionPanel");
                    CuiHelper.DestroyUi(player, "LCLoadoutPersonalDisplay");
                    CuiHelper.DestroyUi(player, "LCLoadoutDefaultDisplay");
                    OpenSaveMenu(player);
                    break;
                case "override":
                    var newSaveName = string.Join(" ", arg.Args.Skip(1));
                    SaveNewLoadout(player, newSaveName, HasPerms, playerLoadout, true);

                    CuiHelper.DestroyUi(player, "LCSavePanel");
                    UICreateDefaultLoadoutPanel(player, "LCOverlayPanel", HasPerms);
                    UICreatePersonalLoadoutPanel(player, "LCOverlayPanel", playerLoadout);
                    UICreateSelectionPanel(player, "LCOverlayPanel", playerLoadout);
                    break;
                case "savename":
                    newSaveName = string.Join(" ", arg.Args.Skip(1));

                    if (string.IsNullOrEmpty(newSaveName)) playerLoadout.NewSaveName = null;
                    else playerLoadout.NewSaveName = string.Join(" ", arg.Args.Skip(1));
                    UICreateSaveLoadoutPanel(player, playerLoadout);
                    break; 
                case "saveloadoutnew":
                    newSaveName = playerLoadout.NewSaveName;
                    if (arg.Args.Length > 1) newSaveName = string.Join(" ", arg.Args.Skip(1));

                    if(!HasPerms.SaveLoadouts)
                    {
                        UICreateSavePopup(player, Lang("InvalidSavePerms", player.UserIDString));
                        return;
                    }
                    if(HasPerms.MaxSaves <= playerLoadout.Loadouts.Count)
                    {
                        UICreateSavePopup(player, Lang("MaxLoadoutLimit", player.UserIDString));
                        return;
                    }
                    else if (string.IsNullOrEmpty(newSaveName))
                    {
                        UICreateSavePopup(player, Lang("NoLoadoutName", player.UserIDString));
                        return;
                    }
                    else if (playerLoadout.Loadouts.Any(x => x.SaveName == newSaveName))
                    {
                        UICreateSavePopup(player, Lang("LoadoutNameAlreadySet", player.UserIDString));
                        return;
                    }
                    else if (newSaveName.Length > 15)
                    {
                        UICreateSavePopup(player, Lang("LoadoutNameTooLong", player.UserIDString));
                        return;
                    }
                    else SaveNewLoadout(player, newSaveName, HasPerms, playerLoadout, false); 

                    playerLoadout.ActiveLoadout = newSaveName;

                    CuiHelper.DestroyUi(player, "LCSavePanel");
                    UICreateDefaultLoadoutPanel(player, "LCOverlayPanel", HasPerms);
                    UICreatePersonalLoadoutPanel(player, "LCOverlayPanel", playerLoadout);
                    UICreateSelectionPanel(player, "LCOverlayPanel", playerLoadout);
                    break;
            }
        }
        #endregion

        #region UI
        private void OpenSaveMenu(BasePlayer player)
        {
            var container = new CuiElementContainer();
            var playerLoadout = GetOrLoadPlayerLoadout(player);
            var savePanel = CreatePanel(ref container, ".507 0", "1 .858", "0 0 0 0", "LCOverlayPanel", "LCSavePanel");

            CreatePanel(ref container, "0 .7", "1 1", "0 0 0 .5", savePanel, "LCSaveOverride");
            CreatePanel(ref container, "0 .39", "1 .688", "0 0 0 .5", savePanel, "LCSaveNew");
            CreatePanel(ref container, "0 .07", "1 .378", "0 0 0 .5", savePanel, "LCSaveAdmin");

            CreateButton(ref container, "0 0", "1 .056", "0 0 0 .5", "1 1 1 1", Lang("BackButton", player.UserIDString), 20, "lc_main back", savePanel);

            CuiHelper.DestroyUi(player, "LCSavePanel");
            CuiHelper.AddUi(player, container);

            UICreateOverrideLoadoutPanel(player, playerLoadout);
            UICreateSaveLoadoutPanel(player, playerLoadout);
            if (permission.UserHasPermission(player.UserIDString, "loadoutcontroller.admin")) UICreateAdminLoadoutPanel(player);
        }

        private void UICreateAdminLoadoutPanel(BasePlayer player, int page = 0)
        {
            var container = new CuiElementContainer();

            var panel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "LCSaveAdmin", "LCSaveAdminOverlay");
            CreateLabel(ref container, "0 .8", "1 1", "0 0 0 0", "1 1 1 1", Lang("AdminSettings", player.UserIDString), 25, TextAnchor.MiddleCenter, panel);

            var maxPage = _loadoutOptions.Count / 6;
            if (page < 0) page = maxPage;
            if (page > maxPage) page = 0;

            if (_loadoutOptions.Count > 6)
            {
                CreateButton(ref container, ".01 .05", ".06 .75", "0 0 0 .4", "1 1 1 1", "<", 15, $"lc_main adminpage {page - 1}", panel);
                CreateButton(ref container, ".935 .05", ".985 .75", "0 0 0 .4", "1 1 1 1", ">", 15, $"lc_main adminpage {page + 1}", panel);
            }

            int i = 0;
            int row = 0;
            foreach (var loadout in _loadoutOptions.Skip(page * 6).Take(6))
            {
                if (i == 3)
                {
                    row++;
                    i = 0;
                }

                CreateButton(ref container, $"{.07 + (i * .29)} {(row == 1 ? .05 : .425)}", $"{.345 + (i * .29)} {(row == 1 ? .375 : .75)}", "0 0 0 .4", "1 1 1 1", loadout.Permission.Split('.')[1].ToUpper(), 20, $"lc_main adminselect {loadout.Permission}", panel);
                i++;
            }

            CuiHelper.DestroyUi(player, "LCSaveAdminOverlay");
            CuiHelper.AddUi(player, container);
        }

        private void UICreateAdminSelection(BasePlayer player, string permissionName)
        {
            var container = new CuiElementContainer();

            var panel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 .5", "LCMainPanel", "LCAdminSelection", true);
            var mainPanel = CreatePanel(ref container, ".35 .35", ".65 .65", "0 0 0 .6", panel, "LCAdminSelectionPanel");

            CreateLabel(ref container, "0 .85", "1 1", "0 0 0 0", "1 1 1 1", Lang("AdminDefaultSave", player.UserIDString), 20, TextAnchor.MiddleCenter, mainPanel);
            CreateButton(ref container, ".93 .85", "1 1", "0 0 0 0", "1 1 1 1", "X", 20, "lc_main adminclose", mainPanel);

            CreateButton(ref container, ".03 .45", ".97 .80", ".31 .57 .95 .6", "1 1 1 1", Lang("AdminFollowRestrictions", player.UserIDString), 20, $"lc_main followrestrictions {permissionName}", mainPanel);
            CreateButton(ref container, ".03 .05", ".97 .4", ".36 .96 .26 .6", "1 1 1 1", Lang("AdminLoadoutRestrictions", player.UserIDString), 20, $"lc_main ignorerestrictions {permissionName}", mainPanel);

            CuiHelper.DestroyUi(player, "LCAdminSelection");
            CuiHelper.AddUi(player, container);
        }

        private void UICreateSaveLoadoutPanel(BasePlayer player, PlayerLoadoutOption playerLoadouts)
        {
            System.Random rnd = new System.Random();
            var container = new CuiElementContainer();

            var randomNumber = rnd.Next(1000);

            var panel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "LCSaveNew", "LCSaveNewOverlay");
            CreateLabel(ref container, "0 .8", "1 1", "0 0 0 0", "1 1 1 1", Lang("SaveNewLoadout", player.UserIDString), 25, TextAnchor.MiddleCenter, panel);

            CreateLabel(ref container, ".02 .67", ".49 .8", "0 0 0 0", "1 1 1 1", Lang("SaveName", player.UserIDString), 15, TextAnchor.MiddleCenter, panel);
            CreateInput(ref container, ".02 .24", ".49 .65", "lc_main savename", "0 0 0 .4", "1 1 1 .8", String.IsNullOrEmpty(playerLoadouts.NewSaveName) ? Lang("InputName", player.UserIDString) : playerLoadouts.NewSaveName, 20, TextAnchor.MiddleCenter, panel);
            CreateButton(ref container, ".02 .05", ".488 .21", ".2 .8 .17 .6", "1 1 1 1", Lang("SaveLoadout", player.UserIDString), 15, "lc_main saveloadoutnew", panel);

            CreateLabel(ref container, ".51 .67", ".98 .8", "0 0 0 0", "1 1 1 1", Lang("PresetOption", player.UserIDString), 15, TextAnchor.MiddleCenter, panel);
            CreateButton(ref container, ".51 .05", ".98 .65", ".2 .8 .17 .6", "1 1 1 1", $"Loadout {randomNumber}", 25, $"lc_main saveloadoutnew Loadout {randomNumber}", panel);

            CuiHelper.DestroyUi(player, "LCSaveNewOverlay");
            CuiHelper.AddUi(player, container);
        }

        private void UICreateOverrideLoadoutPanel(BasePlayer player, PlayerLoadoutOption playerLoadouts, int page = 0)
        {
            var container = new CuiElementContainer();

            var panel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "LCSaveOverride", "LCSaveOverrideOverlay");
            CreateLabel(ref container, "0 .8", "1 1", "0 0 0 0", "1 1 1 1", Lang("OverrideLoadout", player.UserIDString), 25, TextAnchor.MiddleCenter, panel);

            var maxPage = playerLoadouts.Loadouts.Count / 6;
            if (page < 0) page = maxPage;
            if (page > maxPage) page = 0;

            if (playerLoadouts.Loadouts.Count > 6)
            {
                CreateButton(ref container, ".01 .05", ".06 .75", "0 0 0 .4", "1 1 1 1", "<", 15, $"lc_main overridepage {page - 1}", panel);
                CreateButton(ref container, ".935 .05", ".985 .75", "0 0 0 .4", "1 1 1 1", ">", 15, $"lc_main overridepage {page + 1}", panel);
            }

            int i = 0;
            int row = 0;
            foreach (var loadout in playerLoadouts.Loadouts.Skip(page * 6).Take(6))
            {
                if (i == 3)
                {
                    row++;
                    i = 0;
                }

                CreateButton(ref container, $"{.07 + (i * .29)} {(row == 1 ? .05 : .425)}", $"{.345 + (i * .29)} {(row == 1 ? .375 : .75)}", "0 0 0 .4", "1 1 1 1", loadout.SaveName, 20, $"lc_main override {loadout.SaveName}", panel);
                i++;
            }

            CuiHelper.DestroyUi(player, "LCSaveOverrideOverlay");
            CuiHelper.AddUi(player, container);
        }

        void OpenLoadoutMenu(BasePlayer player, string command, string[] args)
        {
            var container = new CuiElementContainer();
            var HasPerms = CheckPlayerPermission(player);
            var playerLoadout = GetOrLoadPlayerLoadout(player);
            var panel = "WCSourcePanel";

            if(!usingWC)
            {
                panel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 .4", "Overlay", "LCMainPanel", true, true);
                CreatePanel(ref container, "0 0", "1 1", "1 1 1 .3", panel);
            }

            var mainPanel = CreatePanel(ref container, usingWC ? "0 .13" : ".1 .1", usingWC ? "1 1" : ".9 .9", "0 0 0 0", panel, "LCOverlayPanel");
            var label = CreateLabel(ref container, "0 .87", "1 1", "0 0 0 .5", "1 1 1 1", Lang("Title", player.UserIDString), usingWC ? 50 : 60, TextAnchor.MiddleCenter, mainPanel);
            if(!usingWC) CreateButton(ref container, ".9 0", "1 1", "0 0 0 0", "1 1 1 1", "X", 50, "lc_main close", label);

            CreatePanel(ref container, "0 0", ".5 .86", "0 0 0 .5", mainPanel, "LCLoadoutInventory");
            CreatePanel(ref container, ".507 0", "1 .48", "0 0 0 .5", mainPanel, "LCLoadoutSelectionPanel");

            CuiHelper.DestroyUi(player, "LCMainPanel");
            CuiHelper.AddUi(player, container);

            UICreateInventoryPanel(player, "LCLoadoutInventory", HasPerms);
            UICreateDefaultLoadoutPanel(player, "LCOverlayPanel", HasPerms);
            UICreatePersonalLoadoutPanel(player, "LCOverlayPanel", playerLoadout);
            UICreateSelectionPanel(player, "LCOverlayPanel", playerLoadout);
        }

        private void UICreateSelectionPanel(BasePlayer player, string lcSelectionPanel, PlayerLoadoutOption playerLoadouts, int page = 0)
        {
            var container = new CuiElementContainer();
            var loadoutSelectionPanel = CreatePanel(ref container, ".507 0", "1 .48", "0 0 0 .5", lcSelectionPanel, "LCLoadoutSelectionPanel");

            CreateLabel(ref container, "0 .87", "1 1", "0 0 0 0", "1 1 1 1", Lang("SelectPersonalLoadout", player.UserIDString), 25, TextAnchor.MiddleCenter, loadoutSelectionPanel);

            var maxPage = playerLoadouts.Loadouts.Count / 6;
            if (page < 0) page = maxPage;
            if (page > maxPage) page = 0;

            if (playerLoadouts.Loadouts.Count > 6)
            {
                CreateButton(ref container, ".02 .056", ".06 .87", "0 0 0 .4", "1 1 1 1", "<", 15, $"lc_main loadoutspage {page - 1}", loadoutSelectionPanel);
                CreateButton(ref container, ".94 .056", ".98 .87", "0 0 0 .4", "1 1 1 1", ">", 15, $"lc_main loadoutspage {page + 1}", loadoutSelectionPanel);
            }

            int i = 0;
            var topHeight = .87;
            foreach (var loadout in playerLoadouts.Loadouts.Skip(6 * page).Take(6))
            {
                if (i == 2)
                {
                    topHeight -= .282;
                    i = 0;
                }
                var pnl = CreatePanel(ref container, $"{.08 + (i * .43)} {topHeight - .25}", $"{.49 + (i * .43)} {topHeight}", loadout.SaveName == playerLoadouts.ActiveLoadout ? ".15 .4 .78 .4" : "0 0 0 .4", loadoutSelectionPanel);
                CreateLabel(ref container, "0 0", "1 1", "0 0 0 0", "1 1 1 1", loadout.SaveName, 20, TextAnchor.MiddleCenter, pnl);
                CreateButton(ref container, "0 0", "1 1", "0 0 0 0", "1 1 1 1", "", 15, $"lc_main selectloadout {loadout.SaveName}", pnl);
                CreateButton(ref container, ".88 .63", ".97 .92", "0 0 0 0", "1 1 1 1", "X", 15, $"lc_main deleteloadout {loadout.SaveName}", pnl);
                i++;
            }

            CuiHelper.DestroyUi(player, "LCLoadoutSelectionPanel");
            CuiHelper.AddUi(player, container);
        }

        private void UICreatePersonalLoadoutPanel(BasePlayer player, string overlayPanel, PlayerLoadoutOption playerLoadouts)
        {
            var container = new CuiElementContainer();
            var loadoutName = string.Empty;
            var personalLoadoutPanel = CreatePanel(ref container, ".507 .49", "1 .67", "0 0 0 .5", overlayPanel, "LCLoadoutPersonalDisplay");

            if (playerLoadouts.ActiveLoadout == null)
            {
                loadoutName = Lang("NoPersonalLoadout", player.UserIDString);
                CreateLabel(ref container, "0 .7", "1 1", "0 0 0 0", "1 1 1 1", loadoutName, 25, TextAnchor.MiddleCenter, personalLoadoutPanel);
                CuiHelper.DestroyUi(player, "LCLoadoutPersonalDisplay");
                CuiHelper.AddUi(player, container);
                return;
            }
            else loadoutName = playerLoadouts.ActiveLoadout.ToUpper();

            CreateLabel(ref container, "0 .7", "1 1", "0 0 0 0", "1 1 1 1", loadoutName, 25, TextAnchor.MiddleCenter, personalLoadoutPanel);

            var loadout = playerLoadouts.Loadouts.FirstOrDefault(x => x.SaveName == playerLoadouts.ActiveLoadout);
            if (loadout != null)
            {
                var i = 0;
                foreach (var item in loadout.Items.Take(9))
                {
                    CreateItemPanel(ref container, $"{.04 + (i * .09)} .1", $"{.16 + (i * .09)} .7", 0f, "0 0 0 0", item.ItemID, personalLoadoutPanel, skinId: item.SkinID);
                    i++;
                }

                if (loadout.Items.Count > 9) CreateLabel(ref container, ".85 .1", ".95 .7", "0 0 0 0", "1 1 1 1", $"+{loadout.Items.Count - 9}", 25, TextAnchor.MiddleCenter, personalLoadoutPanel);

            }

            CuiHelper.DestroyUi(player, "LCLoadoutPersonalDisplay");
            CuiHelper.AddUi(player, container);
        }

        private void UICreateDefaultLoadoutPanel(BasePlayer player, string overlayPanel, LoadoutOption loadout)
        {
            var container = new CuiElementContainer();
            var loadoutName = string.Empty;

            var defaultLoadoutPanel =  CreatePanel(ref container, ".507 .68", "1 .86", "0 0 0 .5", overlayPanel, "LCLoadoutDefaultDisplay");

            if (loadout == null)
            {
                loadoutName = Lang("NoDefaultLoadout", player.UserIDString);
                CreateLabel(ref container, "0 .7", "1 1", "0 0 0 0", "1 1 1 1", loadoutName, 25, TextAnchor.MiddleCenter, defaultLoadoutPanel);
                CuiHelper.DestroyUi(player, "LCLoadoutDefaultDisplay");
                CuiHelper.AddUi(player, container);
                return;
            }
            else loadoutName = loadout.Permission.Split('.')[1].ToUpper();

            CreateLabel(ref container, "0 .7", "1 1", "0 0 0 0", "1 1 1 1", loadoutName, 25, TextAnchor.MiddleCenter, defaultLoadoutPanel);

            var i = 0;
            foreach (var item in loadout.Loadout.Take(9))
            {
                CreateItemPanel(ref container, $"{.04 + (i * .09)} .1", $"{.16 + (i * .09)} .7", 0f, "0 0 0 0", item.ItemID, defaultLoadoutPanel, skinId: item.SkinID);
                i++;
            }

            if (loadout.Loadout.Count > 9) CreateLabel(ref container, ".85 .1", ".95 .7", "0 0 0 0", "1 1 1 1", $"+{loadout.Loadout.Count - 9}", 25, TextAnchor.MiddleCenter, defaultLoadoutPanel);

            CuiHelper.DestroyUi(player, "LCLoadoutDefaultDisplay");
            CuiHelper.AddUi(player, container);
        }

        private void UICreateInventoryPanel(BasePlayer player, string parentPanel, LoadoutOption loadout)
        {
            var container = new CuiElementContainer();
            var savedItems = new Dictionary<int, int>();
            var savedCategories = new Dictionary<string, int>();
            var successfullySaved = new List<SavedItem>();

            var row = 0;
            var ind = 0;
            for (int i = 0; i < 24; i++)
            {
                if (ind == 6)
                {
                    row++;
                    ind = 0;
                }

                var possibleItem = player.inventory.containerMain.itemList.FirstOrDefault(x => x.position == i);
                var itemPanel = CreatePanel(ref container, $"{.13 + (ind * .125)} {.8 - (row * .13)}", $"{.245 + (ind * .125)} {.92 - (row * .13)}", "0 0 0 .4", parentPanel);
                if (possibleItem != null) HandlePossibleItem(ref container, itemPanel, possibleItem, loadout, ref savedItems, ref savedCategories, player, ref successfullySaved);
                ind++;
            }

            for (int i = 0; i < 8; i++)
            {
                var possibleItem = player.inventory.containerWear.itemList.FirstOrDefault(x => x.position == i);
                var itemPanel = CreatePanel(ref container, $"{.07 + ((i != 7 ? i : i - 1) * .125)} {(i != 7 ? .28 : .15)}", $"{.185 + ((i != 7 ? i : i - 1) * .125)} {(i != 7 ? .4 : .27)}", "0 0 0 .4", parentPanel);
                if (possibleItem != null) HandlePossibleItem(ref container, itemPanel, possibleItem, loadout, ref savedItems, ref savedCategories, player, ref successfullySaved);
            }

            for (int i = 0; i < 6; i++)
            {
                var possibleItem = player.inventory.containerBelt.itemList.FirstOrDefault(x => x.position == i);
                var itemPanel = CreatePanel(ref container, $"{.07 + (i * .125)} .15", $"{.185 + (i * .125)} .27", "0 0 0 .4", parentPanel);
                if (possibleItem != null) HandlePossibleItem(ref container, itemPanel, possibleItem, loadout, ref savedItems, ref savedCategories, player, ref successfullySaved);
            }

            CreateButton(ref container, ".07 .05", ".934 .14", ".2 .8 .17 .7", "1 1 1 1", Lang("SaveLoadout", player.UserIDString), 35, "lc_main saveloadout", parentPanel);
            CreateLabel(ref container, ".07 .92", ".934 .99", "0 0 0 0", "1 1 1 1", Lang("Inventory", player.UserIDString), 25, TextAnchor.MiddleCenter, parentPanel);

            CuiHelper.DestroyUi(player, "LCLoadoutInvDisplay");
            CuiHelper.AddUi(player, container);
        }

        void UICreateSavePopup(BasePlayer player, string errorMsg)
        {
            var container = new CuiElementContainer();
            var panel = CreatePanel(ref container, "0 1.01", "1 1.07", "0 0 0 .5", "LCOverlayPanel", "LCPopup");
            CreatePanel(ref container, "0 0", ".005 .96", ".78 .24 .15 1", panel);
            CreateLabel(ref container, "0 0", "1 1", "0 0 0 0", "1 1 1 1", errorMsg, 25, TextAnchor.MiddleCenter, panel);
            CuiHelper.DestroyUi(player, "LCPopup");
            CuiHelper.AddUi(player, container);
            timer.Once(2, () => CuiHelper.DestroyUi(player, "LCPopup"));
        }
        #endregion

        #region Methods
        private void IgnoreLoadoutOptions(BasePlayer player, string permission)
        {
            var savedItems = new List<SavedItem>();
            foreach (var item in player.inventory.containerWear.itemList) HandleItemIgnore(ref savedItems, item, "attire");
            foreach (var item in player.inventory.containerBelt.itemList) HandleItemIgnore(ref savedItems, item, "hotbar");
            foreach (var item in player.inventory.containerMain.itemList.Take(24)) HandleItemIgnore(ref savedItems, item, "main");

            var defaultLoadout = _loadoutOptions.FirstOrDefault(x => x.Permission == permission);
            if (defaultLoadout != null) defaultLoadout.Loadout = savedItems;
        }

        private void HandleItemIgnore(ref List<SavedItem> savedItems, Item item, string inventoryPart)
        {
            List<int> ItemMods = new List<int>();
            if (item.info.category.ToString() == "Weapon")
            {
                if (item.contents != null)
                    foreach (var mod in item.contents.itemList)
                        if (mod.info.itemid != 0)
                            ItemMods.Add(mod.info.itemid);

                BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                if (weapon != null && weapon.primaryMagazine != null) savedItems.Add(new SavedItem { ItemID = item.info.itemid, Amount = item.amount, Position = item.position, Category = inventoryPart, Shortname = item.info.shortname, SkinID = item.skin, Mods = ItemMods, AmmoType = weapon.primaryMagazine.ammoType.shortname, AmmoAmount = weapon.primaryMagazine.contents });
                else savedItems.Add(new SavedItem { ItemID = item.info.itemid, Amount = item.amount, Position = item.position, Category = inventoryPart, Shortname = item.info.shortname, SkinID = item.skin, Mods = ItemMods });
            }
            else savedItems.Add(new SavedItem { ItemID = item.info.itemid, Amount = item.amount, Position = item.position, Category = inventoryPart, Shortname = item.info.shortname, SkinID = item.skin, Mods = null });
        }

        private void SaveNewLoadout(BasePlayer player, string saveName, LoadoutOption loadoutPerms, PlayerLoadoutOption playerLoadouts, bool isOverride = false, bool isAdminSet = false)
        {
            var tempContainer = new CuiElementContainer();
            var savedItems = new Dictionary<int, int>();
            var savedCategories = new Dictionary<string, int>();
            var successfullySaved = new List<SavedItem>();

            foreach (var item in player.inventory.containerWear.itemList) HandlePossibleItem(ref tempContainer, null, item, loadoutPerms, ref savedItems, ref savedCategories, player, ref successfullySaved, "attire"); 
            foreach (var item in player.inventory.containerBelt.itemList) HandlePossibleItem(ref tempContainer, null, item, loadoutPerms, ref savedItems, ref savedCategories, player, ref successfullySaved, "hotbar"); 
            foreach (var item in player.inventory.containerMain.itemList.Take(24)) HandlePossibleItem(ref tempContainer, null, item, loadoutPerms, ref savedItems, ref savedCategories, player, ref successfullySaved, "main");

            if (!isAdminSet)
            {
                if (isOverride)
                {
                    var plyrLoadout = playerLoadouts.Loadouts.FirstOrDefault(x => x.SaveName == saveName);
                    if (plyrLoadout == null) UICreateSavePopup(player, Lang("CouldNotFindOverride", player.UserIDString));
                    else plyrLoadout.Items = successfullySaved;
                }
                else playerLoadouts.Loadouts.Add(new PlayerLoadoutSaved { SavedPerm = loadoutPerms.Permission, SaveName = saveName, Items = successfullySaved });

                playerLoadouts.NewSaveName = null;
            } else
            {
                var defaultLoadout = _loadoutOptions.FirstOrDefault(x => x.Permission == saveName);
                if (defaultLoadout != null) defaultLoadout.Loadout = successfullySaved;
            }

        }

        void HandlePossibleItem(ref CuiElementContainer container, string itemPanel, Item item, LoadoutOption loadout, ref Dictionary<int, int> savedItems, ref Dictionary<string, int> savedCategories, BasePlayer player, ref List<SavedItem> listSavedItems, string inventoryPart = null)
        {
            var category = item.info.category.ToString();
            List<int> ItemMods = new List<int>();

            if (itemPanel != null)
            {
                CreateItemPanel(ref container, "0 0", "1 1", 0.1f, "0 0 0 0", item.info.itemid, itemPanel, skinId: item.skin);
                CreateLabel(ref container, ".06 0", "1 .3", "0 0 0 0", "1 1 1 1", $"x{item.amount}", 13, TextAnchor.MiddleLeft, itemPanel);
            }

            if (loadout == null)
            {
                if (itemPanel != null) CreateLabel(ref container, itemPanel, 3);
                else UICreateSavePopup(player, "No loadout group found");
                return;
            }

            if (loadout.BlockedSkins != null && loadout.BlockedSkins.Contains(item.skin))
            {
                if(itemPanel != null) CreateLabel(ref container, itemPanel, 3);
                return;
            }

            int itemAmount;
            int categoryAmount;

            bool hasCategory = loadout.MaxItemsCat.TryGetValue(category, out int categoryMax);
            bool hasItem = loadout.MaxItemsSN.TryGetValue(item.info.shortname, out int itemMax);

            if(!hasCategory || !hasItem)
            {
                if (itemPanel != null) CreateLabel(ref container, itemPanel, 3);
                return;
            }

            if (!savedItems.TryGetValue(item.info.itemid, out itemAmount))
            {
                savedItems.Add(item.info.itemid, 0);
                itemAmount = savedItems[item.info.itemid];
            }

            if (!savedCategories.TryGetValue(category, out categoryAmount))
            {
                savedCategories.Add(category, 0);
                categoryAmount = savedCategories[category];
            }

            var amountLeftItem = itemMax - itemAmount;
            var amountLeftCat = categoryMax - categoryAmount;

            if (itemAmount >= itemMax || categoryAmount >= categoryMax || amountLeftItem == 0 || amountLeftCat == 0)
            {
                if (itemPanel != null) CreateLabel(ref container, itemPanel, 3);
                return;
            }

            if (amountLeftItem >= 0 && amountLeftCat >= 0)
            {
                int goOn = amountLeftItem > amountLeftCat ? amountLeftCat : amountLeftItem;
                int saveAmount;

                if (item.amount > goOn)
                {
                    if (itemPanel != null) CreateLabel(ref container, itemPanel, 2, item.amount - goOn);
                    saveAmount = goOn;
                }
                else
                {
                    if (itemPanel != null) CreateLabel(ref container, itemPanel, 1);
                    saveAmount = item.amount;
                }

                if (item.info.category.ToString() == "Weapon")
                {
                    if (item.contents != null)
                        foreach (var mod in item.contents.itemList)
                            if (mod.info.itemid != 0)
                                ItemMods.Add(mod.info.itemid);

                    string ammoType = null;
                    BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                    if (weapon != null && weapon.primaryMagazine != null)
                    {
                        if (loadout.AllowedLoadedAmmoTypes.ContainsKey(weapon.primaryMagazine.ammoType.shortname) && loadout.AllowedLoadedAmmoTypes[weapon.primaryMagazine.ammoType.shortname]) ammoType = weapon.primaryMagazine.ammoType.shortname;
                        listSavedItems.Add(new SavedItem { ItemID = item.info.itemid, Amount = saveAmount, Position = item.position, Category = inventoryPart, Shortname = item.info.shortname, SkinID = item.skin, Mods = ItemMods, AmmoType = ammoType, AmmoAmount = weapon.primaryMagazine.contents });
                    }
                    else listSavedItems.Add(new SavedItem { ItemID = item.info.itemid, Amount = saveAmount, Position = item.position, Category = inventoryPart, Shortname = item.info.shortname, SkinID = item.skin, Mods = ItemMods });
                } else listSavedItems.Add(new SavedItem { ItemID = item.info.itemid, Amount = saveAmount, Position = item.position, Category = inventoryPart, Shortname = item.info.shortname, SkinID = item.skin, Mods = null });

                if (savedCategories[category] + saveAmount >= categoryMax) savedCategories[category] = categoryMax;
                else savedCategories[category] += saveAmount;

                if (savedItems[item.info.itemid] + saveAmount >= itemMax) savedItems[item.info.itemid] = itemMax;
                else savedItems[item.info.itemid] += saveAmount;
            }
        }

        void CreateLabel(ref CuiElementContainer container, string itemPanel, int canAdd, int lostAmount = 0)
        {
            switch (canAdd)
            {
                case 1:
                    CreateImagePanel(ref container, ".05 .65", ".35 .95", ImageLibrary?.Call<string>("GetImage", "CheckMark", 0UL), itemPanel);
                    break;
                case 2:
                    CreateImagePanel(ref container, ".05 .65", ".35 .95", ImageLibrary?.Call<string>("GetImage", "ExclamationMark", 0UL), itemPanel);
                    CreateLabel(ref container, ".4 .65", "1 .95", "0 0 0 0", "1 1 1 1", $"- x{lostAmount}", 12, TextAnchor.MiddleLeft, itemPanel);
                    break;
                case 3:
                    CreateImagePanel(ref container, ".05 .65", ".35 .95", ImageLibrary?.Call<string>("GetImage", "XMark", 0UL), itemPanel);
                    break;
            }
        }

        private LoadoutOption CheckPlayerPermission(BasePlayer player)
        {
            LoadoutOption Loadout = null;
            foreach (var loadout in _loadoutOptions.OrderByDescending(x => x.Priority))
                if (permission.UserHasPermission(player.UserIDString, loadout.Permission))
                {
                    Loadout = loadout;
                    break;
                }

            return Loadout;
        }

        private void RegisterImages()
        {
            Dictionary<string, string> imageList = new Dictionary<string, string>
            {
                { "CheckMark", _config.CheckEmoji },
                { "ExclamationMark", _config.ExclamationEmoji },
                { "XMark", _config.XEmoji },
            };

            ImageLibrary?.Call("ImportImageList", "LoadoutController", imageList, 0UL, true);
        }

        private void RegisterCommands()
        {
            if(!usingWC) foreach (var command in _config.LoadoutCommands) cmd.AddChatCommand(command, this, OpenLoadoutMenu);
        }

        private void RegisterPermissions()
        {
            foreach (var loadout in _loadoutOptions) if (!permission.PermissionExists(loadout.Permission, this)) permission.RegisterPermission(loadout.Permission, this); 
            if (!permission.PermissionExists("loadoutcontroller.admin", this)) permission.RegisterPermission("loadoutcontroller.admin", this);
        }

        private void SaveLoadoutOptions() => Interface.GetMod().DataFileSystem.WriteObject($"LoadoutController{Path.DirectorySeparatorChar}LoadoutOptions", _loadoutOptions);

        private void LoadLoadoutOptions()
        {
            var itemOptions = Interface.GetMod().DataFileSystem.ReadObject<List<LoadoutOption>>($"LoadoutController{Path.DirectorySeparatorChar}LoadoutOptions");
            if (itemOptions == null || itemOptions.Count == 0)
            {
                var categoryList = new Dictionary<string, int>();
                var itemList = new Dictionary<string, int>();
                var populateCats = new List<string> { "Weapon", "Ammunition", "Food", "Tool", "Attire" };
                var ammoTypes = new Dictionary<string, bool>();
                var amount = 0;
                foreach (ItemDefinition itemDefinition in ItemManager.itemList)
                {
                    if (!categoryList.ContainsKey(itemDefinition.category.ToString())) categoryList.Add(itemDefinition.category.ToString(), 100);
                    if (populateCats.Contains(itemDefinition.category.ToString()) && !itemList.ContainsKey(itemDefinition.shortname))
                    {
                        if (itemDefinition.category.ToString() == "Weapon") amount = 2;
                        if (itemDefinition.category.ToString() == "Ammunition") amount = 100;
                        if (itemDefinition.category.ToString() == "Food") amount = 20;
                        if (itemDefinition.category.ToString() == "Tool") amount = 2;
                        if (itemDefinition.category.ToString() == "Attire") amount = 1;
                        itemList.Add(itemDefinition.shortname, amount);
                    }
                    if (itemDefinition.category.ToString() == "Ammunition" && !ammoTypes.ContainsKey(itemDefinition.shortname))
                    {
                        ammoTypes.Add(itemDefinition.shortname, true);
                    }
                }
                var loadout = new List<SavedItem>
                {
                    new SavedItem{ItemID = -1211166256, SkinID = 0,Shortname = "ammo.rifle", Position = 0, Category = "main", Amount = 1 },
                    new SavedItem{ItemID = -194953424, SkinID = 0, Shortname = "metal.facemask", Position = 0, Category = "attire", Amount = 1 },
                    new SavedItem{ItemID = 1751045826, SkinID = 0, Shortname = "hoodie", Position = 2, Category = "attire", Amount = 1 },
                    new SavedItem{ItemID = 1110385766, SkinID = 0, Shortname = "metal.plate.torso", Position = 1, Category = "attire", Amount = 1 },
                    new SavedItem{ItemID = 1850456855, SkinID = 0, Shortname = "roadsign.kilt", Position = 3, Category = "attire", Amount = 1 },
                    new SavedItem{ItemID = -1108136649, SkinID = 0, Shortname = "tactical.gloves", Position = 4, Category = "attire", Amount = 1 },
                    new SavedItem{ItemID = 237239288, SkinID = 0, Shortname = "pants", Position = 5, Category = "attire", Amount = 1 },
                    new SavedItem{ItemID = -1549739227, SkinID = 0, Shortname = "shoes.boots", Position = 6, Category = "attire", Amount = 1 },
                    new SavedItem{ItemID = 1545779598, SkinID = 0, Shortname = "rifle.ak", Position = 1, Category = "hotbar", Amount = 1 },
                    new SavedItem{ItemID = 1079279582, SkinID = 0, Shortname = "syringe.medical", Position = 2, Category = "hotbar", Amount = 1 },
                    new SavedItem{ItemID = 1079279582, SkinID = 0, Shortname = "syringe.medical", Position = 3, Category = "hotbar", Amount = 1 },
                    new SavedItem{ItemID = 1079279582, SkinID = 0, Shortname = "syringe.medical", Position = 4, Category = "hotbar", Amount = 1 },
                    new SavedItem{ItemID = 1079279582, SkinID = 0, Shortname = "syringe.medical", Position = 5, Category = "hotbar", Amount = 1 }
                };
                _loadoutOptions.Add(new LoadoutOption { Permission = "loadoutcontroller.default", Priority = 1, SaveLoadouts = true, Loadout = loadout, MaxItemsCat = categoryList, MaxItemsSN = itemList, AllowedLoadedAmmoTypes = ammoTypes });
            }
            else
            {
                _loadoutOptions = itemOptions;
            }
            SaveLoadoutOptions();
        }

        private PlayerLoadoutOption GetOrLoadPlayerLoadout(BasePlayer player)
        {
            var playerLoadout = CheckPlayerPermission(player);

            var loadoutOptions = new PlayerLoadoutOption();
            if (!_playerLoadoutOptions.ContainsKey(player.userID)) _playerLoadoutOptions[player.userID] = Interface.GetMod().DataFileSystem.ReadObject<PlayerLoadoutOption>($"LoadoutController{Path.DirectorySeparatorChar}PlayerLoadouts{Path.DirectorySeparatorChar}{player.userID}");
            
            loadoutOptions = _playerLoadoutOptions[player.userID];

            if (playerLoadout == null)
            {
                loadoutOptions.Loadouts = new List<PlayerLoadoutSaved>();
                return loadoutOptions;
            }

            loadoutOptions.Loadouts.RemoveAll(x => x.SavedPerm != playerLoadout.Permission);
            if (loadoutOptions.Loadouts.Count > playerLoadout.MaxSaves) loadoutOptions.Loadouts.RemoveRange(playerLoadout.MaxSaves, loadoutOptions.Loadouts.Count - 1);

            return loadoutOptions;
        }

        private void SavePlayerLoadouts(BasePlayer player)
        {
            if (!_playerLoadoutOptions.ContainsKey(player.userID)) return;
            Interface.GetMod().DataFileSystem.WriteObject($"LoadoutController{Path.DirectorySeparatorChar}PlayerLoadouts{Path.DirectorySeparatorChar}{player.userID}", _playerLoadoutOptions[player.userID]);
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

        private static void CreateImagePanel(ref CuiElementContainer container, string anchorMin, string anchorMax, string panelImage, string parent = "Overlay", string panelName = null)
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
            CreateImagePanel(ref container, "0 0", "1 1", panelImage, panel);

            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = $"{buttonCommand}" }
            }, panel);
        }

        private static string CreateButton(ref CuiElementContainer container, string anchorMin, string anchorMax, string buttonColor, string textColor, string buttonText, int fontSize, string buttonCommand, string parent = "Overlay",TextAnchor labelAnchor = TextAnchor.MiddleCenter)
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
