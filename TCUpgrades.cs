/*
*  <----- End-User License Agreement ----->
*  Copyright © 2024 Iftebinjan
*  Devoloper: Iftebinjan (Contact: https://discord.gg/HFaGs8YwsH)
*  
*  You may not copy, modify, merge, publish, distribute, sublicense, or sell copies of This Software without the Developer’s consent
*  
*  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO,
*  THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS
*  BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE
*  GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
*  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace Oxide.Plugins
{
    [Info("TCUpgrades", "ifte", "1.0.4")]
    [Description("Tool Cupboard Upgrade!")]
    public class TCUpgrades : RustPlugin
    {
        /*------------------------------------
         *
         *           TCUpgrades by Ifte
         *      Support: https://discord.gg/cCWadjcapr
         *      Fiverr: https://www.fiverr.com/s/e2pkYY
         *            Eror404 not found
         *
         ------------------------------------*/

        /* Version: 1.0.4
         * - Fixed an issue with Auth Remove BTN
         * - Fixed config issue
         * = Fixed on server wiped clear data
        */

        /* Version: 1.0.3
         * - Added new config options
         * - Added default skin options
         * - Added wallpaper placeableable by both side
         * - Added Random color for metal skin
         * - Fixed Carbon UI bugs (Showing black bars on screen)
         * - Fixed some other bugs
        */

        /* Version: 1.0.2
         * - Updated lang files
         * - Updated for the latest rust release.
        */

        #region Variables

        [PluginReference]
        private Plugin ImageLibrary, Toastify, Notify, NoEscape;

        private Configuration config;

        private const string PermAuthList = $"TCUpgrades.authlist";
        private const string PermUpgrades = $"TCUpgrades.upgrade";
        private const string PermRepair = $"TCUpgrades.repair";
        private const string PermSkinBase = $"TCUpgrades.skin";
        private const string PermWallpaper = $"TCUpgrades.wallpaper";

        private const string PermUpgradesNoCost = $"TCUpgrades.upgrade.nocost";
        private const string PermRepairNoCost = $"TCUpgrades.repair.nocost";
        private const string PermWallpaperNoCost = $"TCUpgrades.wallpaper.nocost";

        private const string PermDefault = $"TCUpgrades.default";
        private const string PermWood = $"TCUpgrades.wood";
        private const string PermStone = $"TCUpgrades.stone";
        private const string PermMetal = $"TCUpgrades.metal";
        private const string PermHQM = $"TCUpgrades.hqm";
        private const string PermDeploy = $"TCUpgrades.deploy";

        private const string ClickFX = "assets/bundled/prefabs/fx/notice/loot.drag.grab.fx.prefab";

        private enum NotificationPluginType
        {
            None,
            Toastify,
            Notify
        };

        private readonly Dictionary<ulong, string> SkinBase = new Dictionary<ulong, string>()
        {   { 999, "tclevels.icons.wood.normal"},
            { 10232, "tclevels.icons.wood.frontier" },
            { 2, "tclevels.icons.wood.ginger" },
            { 1000, "tclevels.icons.stone.normal"},
            { 10223, "tclevels.icons.stone.brick" },
            { 10225, "tclevels.icons.stone.brutalist" },
            { 10220, "tclevels.icons.stone.adobe" },
            { 1001, "tclevels.icons.metal.normal"},
            { 10221, "tclevels.icons.metal.container" },
        };

        private readonly Dictionary<uint, string> MetalColors = new()
        {
            [9999] = "tclevels.icons.random",
            [1] = "0.38 0.56 0.74 1.0",
            [2] = "0.45 0.71 0.34 1.0",
            [3] = "0.57 0.29 0.83 1.0",
            [4] = "0.42 0.17 0.11 1.0",
            [5] = "0.82 0.46 0.13 1.0",
            [6] = "0.87 0.87 0.87 1.0",
            [7] = "0.20 0.20 0.18 1.0",
            [8] = "0.40 0.33 0.27 1.0",
            [9] = "0.20 0.22 0.34 1.0",
            [10] = "0.24 0.35 0.20 1.0",
            [11] = "0.73 0.30 0.18 1.0",
            [12] = "0.78 0.53 0.39 1.0",
            [13] = "0.84 0.66 0.22 1.0",
            [14] = "0.34 0.33 0.31 1.0",
            [15] = "0.21 0.34 0.37 1.0",
            [16] = "0.66 0.61 0.56 1.0"
        };

        private readonly Dictionary<BuildingGrade.Enum, HashSet<ulong>> BuildingSkins = new Dictionary<BuildingGrade.Enum, HashSet<ulong>>()
        {
            { BuildingGrade.Enum.Wood, new HashSet<ulong>{ 999, 2, 10232 } },
            { BuildingGrade.Enum.Stone, new HashSet<ulong>{ 1000, 10223, 10225, 10220 } },
            { BuildingGrade.Enum.Metal, new HashSet<ulong>{ 1001, 10221 } },
        };

        private readonly Dictionary<ulong, int> WallpaperSkins = new Dictionary<ulong, int>()
        {
            { 0, -1501434104 },
            { 10246, -1501434104 },
            { 10243, -1501434104 },
            { 10242, -1501434104 },
            { 10266, -1501434104 },
            { 10263, -1501434104 },
            { 10262, -1501434104 },
            { 10261, -1501434104 },
            { 10260, -1501434104 },
            { 10259, -1501434104 },
            { 10258, -1501434104 },
            { 10257, -1501434104 },
            { 10256, -1501434104 },
            { 10255, -1501434104 },
            { 10254, -1501434104 },
            { 10253, -1501434104 },
            { 10252, -1501434104 },
            { 10251, -1501434104 },
            { 10250, -1501434104 },
            { 10249, -1501434104 },
            { 10248, -1501434104 },
            { 10247, -1501434104 },
            { 10245, -1501434104 },
            { 10244, -1501434104 },
            { 10267, -1501434104 },
            { 10268, -1501434104 },
        };

        private readonly Dictionary<string, string> imageList = new Dictionary<string, string>()
        {
            { "tclevels.icons.x", "https://i.ibb.co/K0cD7b7/x.png" },
            { "tclevels.icons.lock", "https://i.ibb.co/ZzkjrmQ/image.png" },
            { "tclevels.icons.info", "https://i.postimg.cc/qqwJym7X/info.png" },
            { "tclevels.icons.twig", "https://i.postimg.cc/kXgNJ00n/sticks.png" },
            { "tclevels.icons.wood", "https://i.postimg.cc/vm67dkB2/wood.png" },
            { "tclevels.icons.stone", "https://i.postimg.cc/DwNQHn2b/stones.png" },
            { "tclevels.icons.metal", "https://i.postimg.cc/Hkn46VCg/metal-fragments.png" },
            { "tclevels.icons.hqm", "https://i.postimg.cc/HsB90bRz/metal-refined.png" },
            { "tclevels.icons.deoply", "https://i.postimg.cc/HWcbkS67/cupboard-tool.png" },
            { "tclevels.icons.wood.frontier", "https://i.postimg.cc/ZKHDDc0w/frontier.png" },
            { "tclevels.icons.wood.ginger", "https://i.postimg.cc/X74z8hNZ/gingerbread.png" },
            { "tclevels.icons.stone.brick", "https://i.postimg.cc/rsLY7ZSP/brick.png" },
            { "tclevels.icons.stone.brutalist", "https://i.postimg.cc/tJtmbdkC/brutalist.png" },
            { "tclevels.icons.stone.adobe", "https://i.postimg.cc/9XGkyV25/adobe.png" },
            { "tclevels.icons.metal.container", "https://i.postimg.cc/gJhTSb6h/container.png" },
            { "tclevels.icons.wood.normal", "https://i.postimg.cc/xCnfkzXv/wood-1.png" },
            { "tclevels.icons.stone.normal", "https://i.postimg.cc/6Qd650p6/stone.png" },
            { "tclevels.icons.metal.normal", "https://i.postimg.cc/MGkKS4xr/metal.png" },
            { "tclevels.icons.random", "https://i.postimg.cc/43kBx8vB/dRFs4.png" }
        };


        #endregion

        #region Config

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();

            if (config.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(config, true);
        }

        protected override void SaveConfig()
            => Config.WriteObject(config, true);

        protected override void LoadDefaultConfig()
            => config = GetBaseConfig();

        private void UpdateConfigValues()
        {
            PrintWarning("Your config file is outdated! Updating config values...");

            config.Version = Version;
            PrintWarning("Config updated!");
        }

        private class Configuration
        {
            [JsonProperty(PropertyName = "General Options")]
            public GeneralOptions General { get; set; }

            [JsonProperty(PropertyName = "Features")]
            public FeaturesOptions Features { get; set; }

            [JsonProperty(PropertyName = "Notifications")]
            public Notifications notifications { get; set; }

            public VersionNumber Version { get; set; }
        }

        private class GeneralOptions
        {
            [JsonProperty("Only owner can use features of TC")]
            public bool onlyOwner { get; set; }

            [JsonProperty("Support for NoEscape plugin")]
            public bool supportNoEscape { get; set; }

            [JsonProperty("Support for CombatBlock plugin")] ///
            public bool supportCombatBlock { get; set; }

            [JsonProperty("Use Automatic Upgrades on build")] ///
            public bool autoUponBuild { get; set; }
        }

        private class Options
        {
            [JsonProperty("Enable Use")]
            public bool enableUse { get; set; }

            [JsonProperty("Default use frequency time in sec")]
            public float defaultTime { get; set; }

            [JsonProperty("Permissions use frequency time in sec", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, float> permissions { get; set; }

        }

        private class UIOptions
        {
            [JsonProperty("UI Buttons Default offset")]
            public string btnOffsets { get; set; }
        }

        private class Notifications
        {
            [JsonProperty("Enable chat notifications")]
            public bool EenableChatNotify { get; set; }

            [JsonProperty("Chat notify prefix")]
            public string Prefix { get; set; }

            [JsonProperty("Chat notify avatarID")]
            public ulong Avatarid { get; set; }

            [JsonProperty("Enable GameTip notify")]
            public bool EenableGameTipnotify { get; set; }

            [JsonProperty("Notifications plugin (0 = disabled | 1 = Toastify | 2 = Notify)")]
            public NotificationPluginType NotificationPlugin { get; set; }

            [JsonProperty("Sync your notifications ID with below keywords")]
            public NotificationsType NotificationTypes { get; set; }

        }

        private class FeaturesOptions
        {
            [JsonProperty("Enable Auth List")]
            public AuthListOptions auttOptions { get; set; }

            [JsonProperty("Enable auto upgrade base")]
            public Options EnableAutoUpgradeBase { get; set; }

            [JsonProperty("Enable base skin upgrade")]
            public Options EnableBaseSkinUpgrade { get; set; }

            [JsonProperty("Enable base wallpaper upgrade")]
            public Options EnableBaseWallpaperUpgrade { get; set; }

            [JsonProperty("Enable base repair upgrade")]
            public Options EnableBaseRepairUpgrade { get; set; }

        }

        private class AuthListOptions
        {
            [JsonProperty(PropertyName = "Enable Auth List")]
            public bool enable { get; set; }

            [JsonProperty("Only owner can see the Remove BTN?")]
            public bool onlyOwnerRemoveBTN { get; set; }
        }

        private class NotificationsType
        {
            public string Error { get; set; }
            public string Success { get; set; }
            public string Info { get; set; }
        }

        private Configuration GetBaseConfig()
        {
            return new Configuration()
            {
                General = new GeneralOptions()
                {
                    onlyOwner = false,
                    supportNoEscape = true,
                    supportCombatBlock = false,
                    autoUponBuild = true
                },
                Features = new FeaturesOptions()
                {
                    auttOptions = new AuthListOptions()
                    {
                        enable = true,
                        onlyOwnerRemoveBTN = false
                    },
                    EnableAutoUpgradeBase = new Options()
                    {
                        enableUse = true,
                        defaultTime = 1.5f,
                        permissions = new Dictionary<string, float>()
                        {
                            { "tcupgrades.vip", 1.0f },
                            { "tcupgrades.mvp", 5 },
                        }
                    },
                    EnableBaseSkinUpgrade = new Options()
                    {
                        enableUse = true,
                        defaultTime = 1.5f,
                        permissions = new Dictionary<string, float>()
                        {
                            { "tcupgrades.vip", 1.0f },
                            { "tcupgrades.mvp", 5 },
                        }
                    },
                    EnableBaseWallpaperUpgrade = new Options()
                    {
                        enableUse = true,
                        defaultTime = 1.5f,
                        permissions = new Dictionary<string, float>()
                        {
                            { "tcupgrades.vip", 1.0f },
                            { "tcupgrades.mvp", 5 },
                        }
                    },
                    EnableBaseRepairUpgrade = new Options()
                    {
                        enableUse = true,
                        defaultTime = 1.5f,
                        permissions = new Dictionary<string, float>()
                        {
                            { "tcupgrades.vip", 1.0f },
                            { "tcupgrades.mvp", 5 },
                        }
                    },
                },
                notifications = new Notifications()
                {
                    EenableChatNotify = true,
                    Prefix = "<color=red>TC UPGRADES:</color>",
                    Avatarid = 0,
                    EenableGameTipnotify = true,
                    NotificationPlugin = NotificationPluginType.None,
                    NotificationTypes = new NotificationsType()
                    {
                        Error = "error",
                        Success = "success",
                        Info = "info"
                    }

                },
                Version = Version
            };
        }

        #endregion

        #region Data

        private Hash<ulong, PlayerState> playersState = new Hash<ulong, PlayerState>();
        private StoredData data = new StoredData();

        private class PlayerState
        {
            [JsonIgnore] public ulong cupboardId;
            [JsonIgnore] public bool UIon;
            [JsonIgnore] public Coroutine upgradeCoroutine;
            [JsonIgnore] public Coroutine repairCoroutine;
            [JsonIgnore] public Coroutine skinCoroutine;
            [JsonIgnore] public Coroutine wallpaperCoroutine;
            public ulong Wood = 0;
            public ulong Stone = 0;
            public ulong Metal = 0;
            public ulong HQM = 0;
            public uint MetalColor = 1;
            public bool ShowEffect = true;
        }

        private class StoredData
        {
            public Dictionary<ulong, PlayerState> playerData = new();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/{Name}", data);
        }

        private void LoadData()
        {
            data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>($"{Name}/{Name}");
            if (data == null)
            {
                data = new StoredData();
            }

            foreach (var d in data.playerData)
            {
                playersState.Add(d.Key, d.Value);
            }
        }

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "AlreadyUpgrading", "There is already an upgrade processing, please wait for it to finish." },
                { "AlreadyRepairing", "There is already an repair processing, please wait for it to finish." },
                { "AlreadySkining", "There is already an base skining processing, please wait for it to finish." },
                { "AlreadyWallpaper", "There is already an wallpaper processing, please wait for it to finish." },
                { "NoResources", "Not enough resources found in tool cupboard to upgrade" },
                { "UpgradeComplete", "Materials upgrade completed" },
                { "RepairComplete", "Materials repair completed" },
                { "SkiningComplete", "Base skining completed" },
                { "WallpaperComplete", "Base wallpaper completed" },
                { "ProgressStatus", "Successfully updated {0} out of {1} items." },
                { "RaidBlocked", "You can't use this while you have Raid Block." },
                { "CombatBlocked", "You can't use this while you have Combat Block." },
                { "StopCoro", "The process has been successfully stopped." },
                { "NoPerm", "You don't have permission to use this." },
                { "AUTHBTN", "AUTHS" },
                { "UPGRADEBTN", "UPGRADE" },
                { "REPAIRBTN", "REPAIR" },
                { "SKINBTN", "SKIN" },
                { "WALLPAPERBTN", "WALLPAPER" },
                { "EFFECTSON", "EFFECTS ON" },
                { "EFFECTSOFF", "EFFECTS OFF" },
            }, this);
        }

        #endregion

        #region Commands

        [ConsoleCommand("tcupgrades.cmd")]
        private void ConsoleCommandThings(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg?.Player();
            if (player == null)
            {
                return;
            }

            string command = arg.Args[0];

            var tc = player.GetBuildingPrivilege();

            SoundEffect(player, ClickFX);

            switch (command)
            {
                case "cancelProcess":
                    {
                        DestroyUI(player);
                        StopPlayerCoroutine(player);
                        SendMessage(player, "StopCoro");
                        SendNotification(player, "error", "StopCoro");

                        NextTick(() => { OnLootEntity(player, tc); });
                        return;
                    }
                case "authList":
                    {
                        DestroySide(player);
                        AuthList(player);
                        return;
                    }
                case "changeEffects":
                    {
                        CuiHelper.DestroyUi(player, "EffectBTN");

                        if (!playersState.TryGetValue(player.userID, out PlayerState value))
                        {
                            value = new PlayerState();
                            playersState.Add(player.userID, value);
                        }
                        value.ShowEffect = !value.ShowEffect;

                        EffectBTN(player);
                        return;
                    }
                case "close_authlist":
                    {
                        CuiHelper.DestroyUi(player, "AuthList");
                        return;
                    }
                case "close_UpgradeMat":
                    {
                        CuiHelper.DestroyUi(player, "UpgradeMaterial");
                        CuiHelper.DestroyUi(player, "EffectBTN");
                        return;
                    }
                case "close_RepairBase":
                    {
                        CuiHelper.DestroyUi(player, "RepairBase");
                        CuiHelper.DestroyUi(player, "EffectBTN");
                        return;
                    }
                case "close_SkinBase":
                    {
                        CuiHelper.DestroyUi(player, "SkinBase");
                        CuiHelper.DestroyUi(player, "EffectBTN");
                        return;
                    }
                case "close_Wallpapers":
                    {
                        CuiHelper.DestroyUi(player, "WallpaperCg");
                        CuiHelper.DestroyUi(player, "ShowWallpaper");
                        CuiHelper.DestroyUi(player, "EffectBTN");
                        return;
                    }
                case "close_WallpaperShow":
                    {
                        CuiHelper.DestroyUi(player, "WallpaperCg");
                        CuiHelper.DestroyUi(player, "ShowWallpaper");
                        CuiHelper.DestroyUi(player, "EffectBTN");
                        return;
                    }
                case "removeFromAuth":
                    {
                        ulong id;
                        ulong playerID;

                        if (!ulong.TryParse(arg.Args[1], out id))
                        {
                            Puts("Failed to parse 'id' from arguments.");
                            return;
                        }

                        if (!ulong.TryParse(arg.Args[2], out playerID))
                        {
                            return;
                        }

                        if (tc == null)
                        {
                            return;
                        }

                        if (!tc.IsAuthed(player))
                        {
                            return;
                        }

                        tc.authorizedPlayers.RemoveWhere(x => x.userid == playerID);
                        tc.SendNetworkUpdate();

                        AuthList(player);
                        return;
                    }
                case "addToPromote":
                    {
                        ulong id;
                        ulong playerID;

                        if (!ulong.TryParse(arg.Args[1], out id))
                        {
                            return;
                        }

                        if (!ulong.TryParse(arg.Args[2], out playerID))
                        {
                            return;
                        }

                        return;
                    }
                case "upgradeMaterials":
                    {
                        DestroySide(player);
                        UpgradeMaterial(player);
                        EffectBTN(player);
                        return;
                    }
                case "selectToUpgrade":
                    {
                        if (!playersState.TryGetValue(player.userID, out PlayerState value))
                        {
                            value = new PlayerState();
                            playersState.Add(player.userID, value);
                        }

                        string material = arg.Args[1];
                        DestroySide(player);
                        UpgradeMaterial(player, material);
                        EffectBTN(player);
                        return;
                    }
                case "upgradeTo":
                    {
                        if (IsBlocked(player)) return;

                        if (!playersState.TryGetValue(player.userID, out PlayerState value))
                        {
                            value = new PlayerState();
                            playersState.Add(player.userID, value);
                        }
                        string materialFrom = arg.Args[1];
                        string materialTo = arg.Args[2];

                        if (playersState[player.userID].upgradeCoroutine != null)
                        {
                            DestroySide(player);
                            SendMessage(player, "AlreadyUpgrading");
                            SendNotification(player, "error", "AlreadyUpgrading");
                            return;
                        }

                        if (!ValidPermission(player, materialTo))
                        {
                            SendMessage(player, "NoPerm");
                            SendNotification(player, "error", "NoPerm");
                            return;
                        }

                        DestroySide(player);
                        playersState[player.userID].upgradeCoroutine = ServerMgr.Instance.StartCoroutine(UpgradeMaterialTC(player, tc, materialFrom, materialTo));
                        NextTick(() => { CreateUpgradeButton(player, tc.net.ID.Value); });
                        return;
                    }
                case "repairBase":
                    {
                        DestroySide(player);
                        RepairBaseMaterial(player);
                        EffectBTN(player);
                        return;
                    }
                case "repairBaseMat":
                    {
                        if (IsBlocked(player)) return;

                        if (!playersState.TryGetValue(player.userID, out PlayerState value))
                        {
                            value = new PlayerState();
                            playersState.Add(player.userID, value);
                        }
                        if (playersState[player.userID].repairCoroutine != null)
                        {
                            DestroySide(player);
                            SendMessage(player, "AlreadyRepairing");
                            SendNotification(player, "error", "AlreadyRepairing");
                            return;
                        }
                        string material = arg.Args[1];

                        if (!ValidPermission(player, material))
                        {
                            SendMessage(player, "NoPerm");
                            SendNotification(player, "error", "NoPerm");
                            return;
                        }

                        DestroySide(player);

                        if (material == "deploy")
                        {
                            playersState[player.userID].repairCoroutine = ServerMgr.Instance.StartCoroutine(RepairMaterialTCDeploy(player, tc, material));
                        }
                        else
                        {
                            playersState[player.userID].repairCoroutine = ServerMgr.Instance.StartCoroutine(RepairMaterialTC(player, tc, material));
                        }
                        NextTick(() => { CreateUpgradeButton(player, tc.net.ID.Value); });
                        return;
                    }
                case "skinBaseSelect":
                    {
                        DestroySide(player);
                        SkinBaseUpgrade(player);
                        EffectBTN(player);
                        return;
                    }
                case "skinBaseStart":
                    {
                        if (IsBlocked(player)) return;

                        if (!playersState.TryGetValue(player.userID, out PlayerState value))
                        {
                            value = new PlayerState();
                            playersState.Add(player.userID, value);
                        }
                        if (playersState[player.userID].skinCoroutine != null)
                        {
                            DestroySide(player);
                            SendMessage(player, "AlreadySkining");
                            SendNotification(player, "error", "AlreadySkining");
                            return;
                        }

                        DestroySide(player);

                        if (ulong.TryParse(arg.Args[1], out var skinID))
                        {
                            var skinGrade = GetBuildingGradeFromSkin(skinID);
                            if (skinGrade == BuildingGrade.Enum.Metal && (GetNatID(skinID) != 0))
                            {
                                DestroySide(player);
                                SkinBaseUpgrade(player, true);
                                EffectBTN(player);
                            }
                            else
                            {
                                playersState[player.userID].skinCoroutine = ServerMgr.Instance.StartCoroutine(SkiningBaseTC(player, tc, skinID));
                            }
                        }
                        NextTick(() => { CreateUpgradeButton(player, tc.net.ID.Value); });
                        return;
                    }
                case "skinBaseSetColor":
                    {
                        if (IsBlocked(player)) return;
                        bool random = false;
                        DestroySide(player);
                        uint color = Convert.ToUInt32(arg.Args[1]);
                        ulong SkinID = ulong.Parse(arg.Args[2]);
                        if (color == 9999)
                        {
                            random = true;
                            color = 1;
                        }
                        SetPlayerSkinID(player, BuildingGrade.Enum.Metal, SkinID, color);

                        if (!playersState.TryGetValue(player.userID, out PlayerState value))
                        {
                            value = new PlayerState();
                            playersState.Add(player.userID, value);
                        }
                        if (playersState[player.userID].skinCoroutine != null)
                        {
                            DestroySide(player);
                            SendMessage(player, "AlreadySkining");
                            SendNotification(player, "error", "AlreadySkining");
                            return;
                        }
                        DestroySide(player);
                        playersState[player.userID].skinCoroutine = ServerMgr.Instance.StartCoroutine(SkiningBaseTC(player, tc, SkinID, random));
                        NextTick(() => { CreateUpgradeButton(player, tc.net.ID.Value); });
                        return;
                    }
                case "showWallpapersView":
                    {
                        DestroySide(player);
                        ChangeWallpaper(player);
                        EffectBTN(player);
                        return;
                    }
                case "viewWallpaper":
                    {
                        DestroySide(player);
                        string material = arg.Args[1];
                        ShowWallpapers(player, material);
                        EffectBTN(player);
                        return;
                    }
                case "changeWallpaper":
                    {
                        if (IsBlocked(player)) return;

                        if (!playersState.TryGetValue(player.userID, out PlayerState value))
                        {
                            value = new PlayerState();
                            playersState.Add(player.userID, value);
                        }
                        if (playersState[player.userID].wallpaperCoroutine != null)
                        {
                            DestroySide(player);
                            SendMessage(player, "AlreadyWallpaper");
                            SendNotification(player, "error", "AlreadyWallpaper");
                            return;
                        }

                        ulong wallpaperID = ulong.Parse(arg.Args[1]);
                        string material = arg.Args[2];

                        if (!ValidPermission(player, material))
                        {
                            SendMessage(player, "NoPerm");
                            SendNotification(player, "error", "NoPerm");
                            return;
                        }

                        DestroySide(player);
                        playersState[player.userID].wallpaperCoroutine = ServerMgr.Instance.StartCoroutine(WallpaperSkinProgress(player, tc, wallpaperID, material));
                        NextTick(() => { CreateUpgradeButton(player, tc.net.ID.Value); });
                        return;
                    }
            }

        }

        #endregion

        #region Init

        private void OnServerInitialized()
        {
            if (ImageLibrary == null)
            {
                Interface.Oxide.LogError("Missing ImageLibrary plugin; Download it at: https://umod.org/plugins/image-library");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }

            LoadData();

            RegisterImages();
            RegisterStaticPerms();
            RegisterCustomPerms();
        }

        private void Unload()
        {
            SaveData();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                DestroyUI(player);
            }
        }

        private void OnServerSave()
        {
            SaveData();
        }

        private void OnNewSave(string filename)
        {
            if (data != null && data.playerData != null)
            {
                data.playerData.Clear();
                SaveData();
            }
        }

        #endregion

        #region IEnumerators

        private IEnumerator WallpaperSkinProgress(BasePlayer player, BuildingPrivlidge tc, ulong wallpaperID, string material)
        {
            var Grade = BuildingGrade.Enum.Twigs;

            if (material == "wood") Grade = BuildingGrade.Enum.Wood;
            if (material == "stone") Grade = BuildingGrade.Enum.Stone;
            if (material == "metal") Grade = BuildingGrade.Enum.Metal;
            if (material == "hqm") Grade = BuildingGrade.Enum.TopTier;

            var buildings = tc.GetBuilding().buildingBlocks;
            yield return CoroutineEx.waitForSeconds(0.15f);

            var freq = GetPlayerFrequency(player, config.Features.EnableBaseWallpaperUpgrade);
            int count = 0;
            for (var i = 0; i < buildings.Count; i++)
            {
                var block = buildings[i];

                if (block == null || !block.IsValid() || block.IsDestroyed || block.transform == null)
                {
                    continue;
                }

                if (block.SecondsSinceAttacked < 30f)
                {
                    continue;
                }

                if (Grade != block.grade) continue;
                //Puts(block.ShortPrefabName.ToString());
                if (!block.ShortPrefabName.Contains("wall")) continue;
                if (block.HasWallpaper() && block.wallpaperID == wallpaperID) continue;

                if (!HasPermission(player, PermWallpaperNoCost) && !HasResoucesWallpaper(player, tc))
                {
                    SendMessage(player, "NoResources");
                    SendNotification(player, "error", "NoResources");
                    playersState[player.userID].wallpaperCoroutine = null;
                    CheckForUIs(player);
                    yield break;
                }
                if (!HasPermission(player, PermWallpaperNoCost))
                {
                    TakeFromInv(tc.inventory.itemList, "cloth", 10);
                }

                block.RemoveWallpaper(0);
                block.SetWallpaper(wallpaperID);
                if (CheckWallpaper(block))
                {
                    block.RemoveWallpaper(1);
                    block.SetWallpaper(wallpaperID, 1);
                }
                block.SendNetworkUpdate();
                count++;

                if (playersState[player.userID].ShowEffect)
                {
                    Effect.server.Run("assets/prefabs/deployable/repair bench/effects/skinchange_spraypaint.prefab", block.transform.position);
                    Effect.server.Run("assets/prefabs/deployable/research table/effects/research-success.prefab", block.transform.position);
                }

                yield return CoroutineEx.waitForSeconds(freq);
            }

            yield return CoroutineEx.waitForSeconds(0.15f);

            SendMessage(player, "WallpaperComplete");
            SendNotification(player, "success", "WallpaperComplete");
            SendMessage(player, "ProgressStatus", count, buildings.Count.ToString());
            playersState[player.userID].wallpaperCoroutine = null;
            CheckForUIs(player);
            yield return null;
        }

        private bool CheckWallpaper(BuildingBlock block)
        {
            Construction construction = WallpaperPlanner.Settings?.GetConstruction(block);
            if (!(construction == null) && !SocketMod_Inside.IsOutside(block.transform.position + construction.deployOffset.localPosition + block.transform.right * 0.2f, block.transform))
            {
                return true;
            }
            return false;
        }

        private ulong GetNatID(ulong id)
        {
            if (id == 999 || id == 1000 || id == 1001)
                return 0;
            return id;
        }

        private IEnumerator SkiningBaseTC(BasePlayer player, BuildingPrivlidge tc, ulong id, bool random = false)
        {
            var buildings = tc.GetBuilding().buildingBlocks;

            yield return CoroutineEx.waitForSeconds(0.15f);

            var freq = GetPlayerFrequency(player, config.Features.EnableBaseSkinUpgrade);

            int count = 0;
            for (var i = 0; i < buildings.Count; i++)
            {
                var block = buildings[i];

                if (block == null || !block.IsValid() || block.IsDestroyed || block.transform == null)
                {
                    continue;
                }

                if (block.SecondsSinceAttacked < 30f)
                {
                    continue;
                }

                if (!BuildingSkins.TryGetValue(block.grade, out var gradeSkins))
                {
                    continue;
                }

                var skinGrade = GetBuildingGradeFromSkin(id);

                if (block.grade != skinGrade)
                {
                    //Puts("Block grade (" + block.grade + ") does not match skin grade (" + skinGrade + ")");
                    continue;
                }

                /*if (GetNatID(id) == block.skinID)
                {
                    Puts("Block already has skinID: " + block.skinID);
                    continue;
                }*/

                block.skinID = GetNatID(id);
                block.UpdateSkin();

                // Set custom color if it's a metal block
                if (block.skinID != 0 && block.grade.ToString() == "Metal")
                {
                    //Puts("block: " + block.grade.ToString() + " skin: " + block.skinID.ToString() + " id: " + id.ToString() + " bC: " + block.customColour.ToString() + " PC: " + GetPlayerMetalColorID(player, BuildingGrade.Enum.Metal));
                    block.SetCustomColour(GetPlayerMetalColorID(player, BuildingGrade.Enum.Metal, random));
                }

                // Save the player's skin ID
                //Puts("block: " + block.grade.ToString() + " skin: " + block.skinID.ToString() + " id: " + id.ToString() + " bC: " + block.customColour.ToString() + " PC: " + GetPlayerMetalColorID(player, BuildingGrade.Enum.Metal));
                SetPlayerSkinID(player, block.grade, id, block.customColour);

                block.SendNetworkUpdateImmediate();

                if (playersState[player.userID].ShowEffect)
                {
                    Effect.server.Run("assets/prefabs/deployable/repair bench/effects/skinchange_spraypaint.prefab", block.transform.position);
                    Effect.server.Run("assets/prefabs/deployable/research table/effects/research-success.prefab", block.transform.position);
                }

                count++;
                yield return CoroutineEx.waitForSeconds(freq);
            }

            yield return CoroutineEx.waitForSeconds(0.15f);

            SendMessage(player, "SkiningComplete");
            SendNotification(player, "success", "SkiningComplete");
            SendMessage(player, "ProgressStatus", count, buildings.Count.ToString());
            playersState[player.userID].skinCoroutine = null;
            CheckForUIs(player);

            yield return null;
        }

        private IEnumerator RepairMaterialTC(BasePlayer player, BuildingPrivlidge tc, string material)
        {
            var RepairGrade = BuildingGrade.Enum.Wood;

            if (material == "wood") RepairGrade = BuildingGrade.Enum.Wood;
            if (material == "stone") RepairGrade = BuildingGrade.Enum.Stone;
            if (material == "metal") RepairGrade = BuildingGrade.Enum.Metal;
            if (material == "hqm") RepairGrade = BuildingGrade.Enum.TopTier;

            var buildings = tc.GetBuilding().buildingBlocks;

            yield return CoroutineEx.waitForSeconds(0.15f);

            var freq = GetPlayerFrequency(player, config.Features.EnableBaseRepairUpgrade);
            int count = 0;
            for (var i = 0; i < buildings.Count; i++)
            {
                var entity = buildings[i];
                if (entity == null || !entity.IsValid() || entity.IsDestroyed || entity.transform == null || !entity.repair.enabled || entity.grade != RepairGrade || entity.health == entity.MaxHealth()) continue;

                var missingHealth = entity.MaxHealth() - entity.health;
                var healthPercentage = missingHealth / entity.MaxHealth();
                if (healthPercentage <= 0 || missingHealth <= 0) continue;

                if (!HasPermission(player, PermRepairNoCost) && !HasResoucesRepair(player, tc, entity, healthPercentage))
                {
                    SendMessage(player, "NoResources");
                    SendNotification(player, "error", "NoResources");
                    playersState[player.userID].repairCoroutine = null;
                    CheckForUIs(player);
                    yield break;
                }

                if (!HasPermission(player, PermRepairNoCost))
                {
                    var list = entity.RepairCost(healthPercentage);
                    for (var index = 0; index < list.Count; index++)
                    {
                        var check = list[index];
                        TakeFromInv(tc.inventory.itemList, check.itemDef.shortname, (int)check.amount);
                    }
                }

                entity.health += missingHealth;
                entity.SendNetworkUpdate();
                if (playersState[player.userID].ShowEffect)
                {
                    if (entity.health < entity.MaxHealth())
                    {
                        entity.OnRepair();
                    }
                    else
                    {
                        entity.OnRepairFinished();
                    }
                }
                count++;
                yield return CoroutineEx.waitForSeconds(freq);
            }

            yield return CoroutineEx.waitForSeconds(0.15f);
            SendMessage(player, "RepairComplete");
            SendNotification(player, "success", "RepairComplete");
            SendMessage(player, "ProgressStatus", count, buildings.Count.ToString());
            playersState[player.userID].repairCoroutine = null;
            CheckForUIs(player);

            yield return null;
        }

        private IEnumerator RepairMaterialTCDeploy(BasePlayer player, BuildingPrivlidge tc, string material)
        {
            if (material != "deploy") yield break;

            var buildings = tc.GetBuilding().decayEntities;

            yield return CoroutineEx.waitForSeconds(0.15f);

            var freq = GetPlayerFrequency(player, config.Features.EnableBaseRepairUpgrade);
            int count = 0;
            for (var i = 0; i < buildings.Count; i++)
            {
                var entity = buildings[i];
                if (entity == null || !entity.IsValid() || entity.IsDestroyed || entity.transform == null || !entity.repair.enabled || entity.health == entity.MaxHealth()) continue;

                //Puts(entity.PrefabName);
                if (IsBuildingBlock(entity)) continue;

                var missingHealth = entity.MaxHealth() - entity.health;
                var healthPercentage = missingHealth / entity.MaxHealth();
                if (healthPercentage <= 0 || missingHealth <= 0) continue;

                if (!HasPermission(player, PermRepairNoCost) && !HasResoucesRepair(player, tc, entity, healthPercentage))
                {
                    SendMessage(player, "NoResources");
                    SendNotification(player, "error", "NoResources");
                    playersState[player.userID].repairCoroutine = null;
                    CheckForUIs(player);
                    yield break;
                }

                if (!HasPermission(player, PermRepairNoCost))
                {
                    var list = entity.RepairCost(healthPercentage);
                    for (var index = 0; index < list.Count; index++)
                    {
                        var check = list[index];
                        TakeFromInv(tc.inventory.itemList, check.itemDef.shortname, (int)check.amount);
                    }
                }

                entity.health += missingHealth;
                entity.SendNetworkUpdate();
                if (playersState[player.userID].ShowEffect)
                {
                    if (entity.health < entity.MaxHealth())
                    {
                        entity.OnRepair();
                    }
                    else
                    {
                        entity.OnRepairFinished();
                    }
                }
                count++;
                yield return CoroutineEx.waitForSeconds(freq);
            }

            yield return CoroutineEx.waitForSeconds(0.15f);
            SendMessage(player, "RepairComplete");
            SendNotification(player, "success", "RepairComplete");
            SendMessage(player, "ProgressStatus", count, buildings.Count.ToString());
            playersState[player.userID].repairCoroutine = null;
            CheckForUIs(player);

            yield return null;
        }

        private IEnumerator UpgradeMaterialTC(BasePlayer player, BuildingPrivlidge tc, string fromMat, string toMat)
        {
            var FromGrade = BuildingGrade.Enum.Wood;

            if (fromMat == "twig") FromGrade = BuildingGrade.Enum.Twigs;
            if (fromMat == "wood") FromGrade = BuildingGrade.Enum.Wood;
            if (fromMat == "stone") FromGrade = BuildingGrade.Enum.Stone;
            if (fromMat == "metal") FromGrade = BuildingGrade.Enum.Metal;
            if (fromMat == "hqm") FromGrade = BuildingGrade.Enum.TopTier;

            var ToGrade = BuildingGrade.Enum.Wood;

            if (toMat == "wood") ToGrade = BuildingGrade.Enum.Wood;
            if (toMat == "stone") ToGrade = BuildingGrade.Enum.Stone;
            if (toMat == "metal") ToGrade = BuildingGrade.Enum.Metal;
            if (toMat == "hqm") ToGrade = BuildingGrade.Enum.TopTier;

            var buildings = tc.GetBuilding().buildingBlocks;

            yield return CoroutineEx.waitForSeconds(0.15f);

            var freq = GetPlayerFrequency(player, config.Features.EnableAutoUpgradeBase);
            int count = 0;
            for (var i = 0; i < buildings.Count; i++)
            {
                var block = buildings[i];

                if (block == null || !block.IsValid() || block.IsDestroyed || block.transform == null) continue;

                if (tc == null)
                {
                    yield break;
                }

                if (block.SecondsSinceAttacked < 30f)
                {
                    continue;
                }

                if (block.grade != FromGrade)
                {
                    continue;
                }

                if (!HasPermission(player, PermUpgradesNoCost) && !HasResouces(player, tc, block, ToGrade))
                {
                    SendMessage(player, "NoResources");
                    SendNotification(player, "error", "NoResources");
                    playersState[player.userID].upgradeCoroutine = null;
                    CheckForUIs(player);
                    yield break;
                }

                if (!HasPermission(player, PermUpgradesNoCost))
                {
                    var list = block.blockDefinition.GetGrade(ToGrade, 0).CostToBuild();
                    for (var index = 0; index < list.Count; index++)
                    {
                        var check = list[index];
                        TakeFromInv(tc.inventory.itemList, check.itemDef.shortname, (int)check.amount);
                    }
                }

                var skinId = block.skinID;

                if (playersState[player.userID].ShowEffect)
                {
                    try
                    {
                        var effect = "assets/bundled/prefabs/fx/build/promote_toptier.prefab";
                        if (ToGrade == BuildingGrade.Enum.Wood)
                        {
                            effect = "assets/bundled/prefabs/fx/build/frame_place.prefab";
                            block.ClientRPC<int, ulong>(null, "DoUpgradeEffect", (int)BuildingGrade.Enum.Wood, skinId);
                        }
                        else if (ToGrade == BuildingGrade.Enum.Stone)
                        {
                            effect = "assets/bundled/prefabs/fx/build/promote_stone.prefab";
                            block.ClientRPC<int, ulong>(null, "DoUpgradeEffect", (int)BuildingGrade.Enum.Stone, skinId);
                        }
                        else if (ToGrade == BuildingGrade.Enum.Metal)
                        {
                            effect = "assets/bundled/prefabs/fx/build/promote_metal.prefab";
                            block.ClientRPC<int, ulong>(null, "DoUpgradeEffect", (int)BuildingGrade.Enum.Metal, skinId);
                        }
                        else
                        {
                            block.ClientRPC<int, ulong>(null, "DoUpgradeEffect", (int)BuildingGrade.Enum.TopTier, skinId);
                        }

                        Effect.server.Run(effect, block.transform.position);
                    }
                    catch { }
                }

                block.skinID = GetPlayerSkinID(player, ToGrade);
                block.SetGrade(ToGrade);
                block.UpdateSkin();
                block.SetHealthToMax();
                if (block.skinID != 0 && block.grade.ToString() == "Metal")
                {
                    block.SetCustomColour(GetPlayerMetalColorID(player, BuildingGrade.Enum.Metal));
                }

                block.SendNetworkUpdateImmediate();
                count++;
                yield return CoroutineEx.waitForSeconds(freq);
            }

            yield return CoroutineEx.waitForSeconds(0.15f);
            SendMessage(player, "UpgradeComplete");
            SendNotification(player, "success", "UpgradeComplete");
            SendMessage(player, "ProgressStatus", count, buildings.Count.ToString());
            playersState[player.userID].upgradeCoroutine = null;
            CheckForUIs(player);

            yield return null;
        }

        #endregion

        #region Methods

        private static string AdjustUiOffsets(string offsets, string direction, int n)
        {
            string[] parts = offsets.Split(' ');

            if (parts.Length != 4)
                return offsets; // Return the original if input is not in expected format

            float minX = Convert.ToSingle(parts[0]);
            float minY = Convert.ToSingle(parts[1]);
            float maxX = Convert.ToSingle(parts[2]);
            float maxY = Convert.ToSingle(parts[3]);

            switch (direction.ToLower())
            {
                case "left":
                    minX -= n;
                    maxX -= n;
                    break;
                case "right":
                    minX += n;
                    maxX += n;
                    break;
                case "up":
                    minY += n;
                    maxY += n;
                    break;
                case "down":
                    minY -= n;
                    maxY -= n;
                    break;
            }

            return $"{minX} {minY} {maxX} {maxY}";
        }

        private bool ValidPermission(BasePlayer player, string entity)
        {
            if (HasPermission(player, PermDefault))
                return true;

            if (entity == "wood" && HasPermission(player, PermWood))
            {
                return true;
            }
            else if (entity == "stone" && HasPermission(player, PermStone))
            {
                return true;
            }
            else if (entity == "metal" && HasPermission(player, PermMetal))
            {
                return true;
            }
            else if (entity == "hqm" && HasPermission(player, PermHQM))
            {
                return true;
            }
            else if (entity == "deploy" && HasPermission(player, PermHQM))
            {
                return true;
            }
            return false;
        }

        private void StopPlayerCoroutine(BasePlayer player)
        {
            if (playersState.TryGetValue(player.userID, out PlayerState state))
            {
                Coroutine[] coroutines = { state.upgradeCoroutine, state.repairCoroutine, state.skinCoroutine, state.wallpaperCoroutine };

                foreach (Coroutine coroutine in coroutines)
                {
                    if (coroutine != null)
                    {
                        ServerMgr.Instance.StopCoroutine(coroutine);
                    }
                }

                state.upgradeCoroutine = null;
                state.repairCoroutine = null;
                state.skinCoroutine = null;
                state.wallpaperCoroutine = null;
            }
        }


        private void CheckForUIs(BasePlayer player)
        {
            if (player == null && !player.IsConnected) return;

            if (playersState.TryGetValue(player.userID, out PlayerState value))
            {
                if (value.UIon && (player.GetBuildingPrivilege() != null))
                {
                    OnLootEntity(player, player.GetBuildingPrivilege());
                }
            }
        }

        private static void SoundEffect(BasePlayer player, string effect = null)
        {
            EffectNetwork.Send(new Effect(effect, player, 0, new Vector3(), new Vector3()), player.Connection);
        }

        string GetPropertyName(string type)
        {
            string propertyName = type switch
            {
                "error" => nameof(NotificationsType.Error),
                "info" => nameof(NotificationsType.Info),
                "success" => nameof(NotificationsType.Success),
                _ => nameof(NotificationsType.Info)
            };

            var propertyInfo = typeof(NotificationsType).GetProperty(propertyName);
            return propertyInfo != null ? propertyInfo.Name : null;
        }

        private bool IsBlocked(BasePlayer player)
        {
            if (NoEscape != null)
            {
                if (NoEscape.Call<bool>("IsRaidBlocked", player.UserIDString) && config.General.supportNoEscape)
                {
                    SendMessage(player, "RaidBlocked");
                    SendNotification(player, "error", "RaidBlocked");
                    return true;
                }
                else if (NoEscape.Call<bool>("IsCombatBlocked", player.UserIDString) && config.General.supportCombatBlock)
                {
                    SendMessage(player, "CombatBlocked");
                    SendNotification(player, "error", "CombatBlocked");
                    return true;
                }
            }
            return false;
        }

        private bool IsBuildingBlock(DecayEntity entity)
        {
            if (entity is BuildingBlock)
            {
                return true;
            }
            return false;
        }

        private float GetPlayerFrequency(BasePlayer player, Options options)
        {
            float feq = options.defaultTime;

            foreach (var item in options.permissions)
            {
                if (HasPermission(player, item.Key))
                    feq = Math.Min(feq, item.Value);
            }

            return feq;
        }

        private void RegisterStaticPerms()
        {
            RegisterPerm(PermAuthList);
            RegisterPerm(PermUpgrades);
            RegisterPerm(PermRepair);
            RegisterPerm(PermSkinBase);
            RegisterPerm(PermWallpaper);
            RegisterPerm(PermUpgradesNoCost);
            RegisterPerm(PermRepairNoCost);
            RegisterPerm(PermWallpaperNoCost);
            //
            RegisterPerm(PermDefault);
            RegisterPerm(PermWood);
            RegisterPerm(PermStone);
            RegisterPerm(PermMetal);
            RegisterPerm(PermHQM);
            RegisterPerm(PermDeploy);
        }

        private void RegisterCustomPerms()
        {
            foreach (var perm in config.Features.EnableAutoUpgradeBase.permissions)
            {
                RegisterPerm(perm.Key);
            }
            foreach (var perm in config.Features.EnableBaseRepairUpgrade.permissions)
            {
                RegisterPerm(perm.Key);
            }
            foreach (var perm in config.Features.EnableBaseSkinUpgrade.permissions)
            {
                RegisterPerm(perm.Key);
            }
            foreach (var perm in config.Features.EnableBaseWallpaperUpgrade.permissions)
            {
                RegisterPerm(perm.Key);
            }
        }

        private bool HasPermission(BasePlayer player, string perm)
        {
            if (permission.UserHasPermission(player.UserIDString, perm))
                return true;
            return false;
        }

        private void RegisterPerm(string perm)
        {
            if (!permission.PermissionExists(perm))
                permission.RegisterPermission(perm, this);
        }

        public BuildingGrade.Enum? GetBuildingGradeFromSkin(ulong skinID)
        {
            foreach (var entry in BuildingSkins)
            {
                if (entry.Value.Contains(skinID))
                {
                    return entry.Key;
                }
            }

            return null;
        }

        private bool HasResouces(BasePlayer player, BuildingPrivlidge cup, BuildingBlock block, BuildingGrade.Enum grade)
        {
            var list = block.blockDefinition.GetGrade(grade, 0).CostToBuild();
            for (var index = 0; index < list.Count; index++)
            {
                ItemAmount itemAmount = list[index];
                if (cup.inventory.GetAmount(itemAmount.itemid, false) < (double)itemAmount.amount) return false;
            }
            return true;
        }

        private bool HasResoucesWallpaper(BasePlayer player, BuildingPrivlidge cup)
        {
            Item item = ItemManager.CreateByPartialName("cloth");
            if (cup.inventory.GetAmount(item.info.itemid, false) < 10)
                return false;
            return true;
        }

        private bool HasResoucesRepair(BasePlayer player, BuildingPrivlidge cup, BaseCombatEntity entity, float healthMissing)
        {
            var list = entity.RepairCost(healthMissing);
            for (var index = 0; index < list.Count; index++)
            {
                ItemAmount itemAmount = list[index];
                if (cup.inventory.GetAmount(itemAmount.itemid, false) < (double)itemAmount.amount) return false;
            }
            return true;
        }

        private static void TakeFromInv(IEnumerable<Item> itemList, string name, int takeitems)
        {
            if (takeitems == 0) return;
            var list = Pool.Get<List<Item>>();
            var num1 = 0;
            foreach (var obj in itemList)
            {
                if (obj.info.shortname != name) continue;
                var num2 = takeitems - num1;
                if (num2 <= 0) continue;
                if (obj.amount > num2)
                {
                    obj.MarkDirty();
                    obj.amount -= num2;
                    break;
                }
                if (obj.amount <= num2)
                {
                    num1 += obj.amount;
                    list.Add(obj);
                }
                if (num1 == takeitems) break;
            }

            foreach (var obj in list)
                obj.Remove();
            Pool.FreeUnmanaged(ref list);
        }

        private ulong GetPlayerSkinID(BasePlayer player, BuildingGrade.Enum grade)
        {
            if (!data.playerData.TryGetValue(player.userID, out var playerState))
            {
                playerState = new PlayerState();
                data.playerData[player.userID] = playerState;
                return 0;
            }

            switch (grade)
            {
                case BuildingGrade.Enum.Wood:
                    return playerState.Wood;

                case BuildingGrade.Enum.Stone:
                    return playerState.Stone;

                case BuildingGrade.Enum.Metal:
                    return playerState.Metal;

                case BuildingGrade.Enum.TopTier:
                    return playerState.HQM;

                default:
                    return 0;
            }
        }

        private uint GetPlayerMetalColorID(BasePlayer player, BuildingGrade.Enum grade, bool random = false)
        {
            if (!data.playerData.TryGetValue(player.userID, out var playerState))
            {
                playerState = new PlayerState();
                data.playerData[player.userID] = playerState;
                return 0;
            }

            if (grade == BuildingGrade.Enum.Metal)
            {
                return random ? GetRandomMetalColorKey() : playerState.MetalColor;
            }
            return 0;
        }

        public uint GetRandomMetalColorKey()
        {
            var keys = MetalColors.Keys.Where(key => key != 9999).ToList();

            // Generate a random index
            System.Random random = new System.Random();
            int randomIndex = random.Next(keys.Count);

            // Return the randomly selected key
            return keys[randomIndex];
        }

        private void SetPlayerSkinID(BasePlayer player, BuildingGrade.Enum grade, ulong skinID, uint MetalColor = 0)
        {
            if (!data.playerData.TryGetValue(player.userID, out var playerState))
            {
                playerState = new PlayerState();
                data.playerData[player.userID] = playerState;
            }

            switch (grade)
            {
                case BuildingGrade.Enum.Wood:
                    playerState.Wood = skinID;
                    break;

                case BuildingGrade.Enum.Stone:
                    playerState.Stone = skinID;
                    break;

                case BuildingGrade.Enum.Metal:
                    playerState.Metal = skinID;
                    playerState.MetalColor = MetalColor;
                    break;

                case BuildingGrade.Enum.TopTier:
                    playerState.HQM = skinID;
                    break;

                default:
                    break;
            }
        }

        private void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, $"{Name}.AuthListButton");
            CuiHelper.DestroyUi(player, $"{Name}.AutoUpgradeBaseButton");
            CuiHelper.DestroyUi(player, $"{Name}.BaseRepairUpgradeButton");
            CuiHelper.DestroyUi(player, $"{Name}.BaseSkinUpgradeButton");
            CuiHelper.DestroyUi(player, $"{Name}.BaseWallpaperUpgradeButton");
            CuiHelper.DestroyUi(player, $"{Name}.CancelBTN");
            CuiHelper.DestroyUi(player, "AuthList");
            CuiHelper.DestroyUi(player, "UpgradeMaterial");
            CuiHelper.DestroyUi(player, "RepairBase");
            CuiHelper.DestroyUi(player, "SkinBase");
            CuiHelper.DestroyUi(player, "WallpaperCg");
            CuiHelper.DestroyUi(player, "ShowWallpaper");
            CuiHelper.DestroyUi(player, "EffectBTN");
        }

        private void DestroySide(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "AuthList");
            CuiHelper.DestroyUi(player, "UpgradeMaterial");
            CuiHelper.DestroyUi(player, "RepairBase");
            CuiHelper.DestroyUi(player, "SkinBase");
            CuiHelper.DestroyUi(player, "WallpaperCg");
            CuiHelper.DestroyUi(player, "ShowWallpaper");
            CuiHelper.DestroyUi(player, "EffectBTN");
        }

        private string FindPlayerName(ulong id)
        {
            return covalence.Players.FindPlayerById(id.ToString()).Name;
        }

        #endregion

        #region UIs

        private void CreateUpgradeButton(BasePlayer player, ulong cupboardId, BaseEntity entity = null)
        {
            //check for onlyOwner
            if (config.General.onlyOwner && entity != null && entity.OwnerID != player.userID)
                return;

            if (!playersState.TryGetValue(player.userID, out PlayerState state))
            {
                state = new PlayerState();
                playersState[player.userID] = state;
            }

            UIBuilder ui = new UIBuilder();
            float xOffset = 0f;
            float yOffset = 0f;
            int buttonCount = 0;

            //if any coroutine is running
            bool isAnyCoroutineRunning = state.upgradeCoroutine != null
                                          || state.repairCoroutine != null
                                          || state.skinCoroutine != null
                                          || state.wallpaperCoroutine != null;

            void AddButton(string color, string message, string command, string name)
            {
                if (buttonCount >= 3)
                {
                    xOffset = 0f;
                    yOffset -= 22f;
                    buttonCount = 0;
                }

                string offset = $"{486 - xOffset} {618 - yOffset} {572 - xOffset} {638 - yOffset}";

                string finalColor = color;
                string finalCommand = command;

                ui.AddButton(
                    "Hud.Menu",
                    finalColor,
                    message,
                    finalCommand,
                    ".5 0 .5 0",
                    offset,
                    12,
                    name,
                    0.2f
                );

                xOffset += 88f;
                buttonCount++;
            }

            if (isAnyCoroutineRunning)
            {
                DestroyUI(player);
                AddButton(UIBuilder.Colors.Red, "CANCEL", "tcupgrades.cmd cancelProcess", $"{Name}.CancelBTN");
                ui.Add(player);
                return;
            }

            if (config.Features.auttOptions.enable && HasPermission(player, PermAuthList))
            {
                AddButton(UIBuilder.Colors.DefaultRust, Msg(player, "AUTHBTN"), $"tcupgrades.cmd authList", $"{Name}.AuthListButton");
            }
            if (config.Features.EnableAutoUpgradeBase.enableUse && HasPermission(player, PermUpgrades))
            {
                AddButton(UIBuilder.Colors.DefaultRust, Msg(player, "UPGRADEBTN"), $"tcupgrades.cmd upgradeMaterials", $"{Name}.AutoUpgradeBaseButton");
            }
            if (config.Features.EnableBaseRepairUpgrade.enableUse && HasPermission(player, PermRepair))
            {
                AddButton(UIBuilder.Colors.DefaultRust, Msg(player, "REPAIRBTN"), $"tcupgrades.cmd repairBase", $"{Name}.BaseRepairUpgradeButton");
            }
            if (config.Features.EnableBaseSkinUpgrade.enableUse && HasPermission(player, PermSkinBase))
            {
                AddButton(UIBuilder.Colors.DefaultRust, Msg(player, "SKINBTN"), $"tcupgrades.cmd skinBaseSelect", $"{Name}.BaseSkinUpgradeButton");
            }
            if (config.Features.EnableBaseWallpaperUpgrade.enableUse && HasPermission(player, PermWallpaper))
            {
                AddButton(UIBuilder.Colors.DefaultRust, Msg(player, "WALLPAPERBTN"), $"tcupgrades.cmd showWallpapersView", $"{Name}.BaseWallpaperUpgradeButton");
            }

            ui.Add(player);
        }

        private void AuthList(BasePlayer player)
        {
            BuildingPrivlidge privlidge = player.GetNearestBuildingPrivledge();

            if (privlidge == null) return;

            var container = new CuiElementContainer();

            #region Top
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.969 0.922 0.882 0.035", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-113.095 360", OffsetMax = "188.693 616.546" }
            }, "Hud.Menu", "AuthList");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.1294118 0.1372549 0.1098039 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150.895 98.938", OffsetMax = "150.895 128.942" }
            }, "AuthList", "Top");

            container.Add(new CuiElement
            {
                Name = "Image_1931",
                Parent = "Top",
                Components = {
                    new CuiRawImageComponent { Color = "0.5137255 0.5294118 0.5450981 1", Png = GetImage("tclevels.icons.info") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-142.5 -11", OffsetMax = "-120.5 11" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Label_4892",
                Parent = "Top",
                Components = {
                    new CuiTextComponent { Text = "PLAYERS AUTHORIZED TO THIS TOOL CUPBOARD!", Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "0.909804 0.8705883 0.8352942 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-117.224 -15.002", OffsetMax = "117.822 14.517" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.7764707 0.2470588 0.1568628 1", Command = "tcupgrades.cmd close_authlist" },
                Text = { Text = "✘", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "126.3 -10", OffsetMax = "147.3 11" }
            }, "Top", "Button_5678");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-148.343 -125.287", OffsetMax = "147.83 96.386" }
            }, "AuthList", "MainList");

            container.Add(new CuiElement
            {
                Parent = "MainList",
                Name = "ScrollView",
                Components =
                {
                    new CuiImageComponent { Color = "0 0 0 0" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                    new CuiScrollViewComponent
                    {
                        Vertical = true,
                        MovementType = ScrollRect.MovementType.Clamped,
                        Elasticity = 0.3f,
                        Inertia= true,
                        DecelerationRate = 0.135f,
                        ScrollSensitivity = 6.0f, // Adjust sensitivity for more control
                        ContentTransform = new CuiRectTransformComponent {
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = "0 -300",
                            OffsetMax = "0 0"
                        },
                        VerticalScrollbar = new CuiScrollbar { Invert = true, Size = 0f, HandleColor = "0 0 0 0", HighlightColor = "0 0 0 0", TrackColor = "0 0 0 0", PressedColor = "0 0 0 0" },
                        HorizontalScrollbar = new CuiScrollbar { Invert = true, Size = 0f, HandleColor = "0 0 0 0", HighlightColor = "0 0 0 0", TrackColor = "0 0 0 0", PressedColor = "0 0 0 0" },
                    },
                }
            });

            #endregion

            BuildingManager.Building building = privlidge.GetBuilding();

            if (building != null)
            {
                BuildingPrivlidge pr = building.GetDominatingBuildingPrivilege();

                int panelIndex = 0;
                float panelHeight = 30f;
                float gap = 2f;
                float totalHeight = (panelHeight + gap) * pr.authorizedPlayers.Count;

                foreach (ulong authorized in pr.authorizedPlayers.Select(x => x.userid).ToArray())
                {
                    float offsetY = -panelIndex * (panelHeight + gap);

                    container.Add(new CuiPanel
                    {
                        Image = { Color = "0.969 0.922 0.882 0.035", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                        RectTransform = {
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = $"0 {offsetY - panelHeight}",
                            OffsetMax = $"0 {offsetY}"
                        }
                    }, "ScrollView", $"Panel_{authorized}");

                    container.Add(new CuiElement
                    {
                        Name = "Label_7263",
                        Parent = $"Panel_{authorized}",
                        Components = {
                        new CuiTextComponent { Text = $"{FindPlayerName(authorized)}\n<color=#FFD700>{authorized.ToString()}</color>", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-142.86 -13.656", OffsetMax = "99.866 13.656" }
                        }
                    });

                    if (config.Features.auttOptions.onlyOwnerRemoveBTN)
                    {
                        if (pr.OwnerID == player.userID)
                        {
                            container.Add(new CuiButton
                            {
                                Button = { Color = "0.7764707 0.2470588 0.1568628 1", Command = $"tcupgrades.cmd removeFromAuth {pr.net.ID.Value} {authorized}" },
                                Text = { Text = "REMOVE", Font = "robotocondensed-regular.ttf", FontSize = 9, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "89.313 -11.27", OffsetMax = "128.087 10.794" }
                            }, $"Panel_{authorized}", "RemoveBTN");
                        }                      
                    }
                    else
                    {
                        container.Add(new CuiButton
                        {
                            Button = { Color = "0.7764707 0.2470588 0.1568628 1", Command = $"tcupgrades.cmd removeFromAuth {pr.net.ID.Value} {authorized}" },
                            Text = { Text = "REMOVE", Font = "robotocondensed-regular.ttf", FontSize = 9, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "89.313 -11.27", OffsetMax = "128.087 10.794" }
                        }, $"Panel_{authorized}", "RemoveBTN");
                    }

                    /*
                    container.Add(new CuiButton
                    {
                        Button = { Color = "0.3921569 0.4784314 0.2352941 1", Command = $"tcupgrades.cmd addToPromote {pr.net.ID.Value} {authorized}" },
                        Text = { Text = "PROMOTE", Font = "robotocondensed-regular.ttf", FontSize = 9, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "65.524 -11.032", OffsetMax = "107.088 11.032" }
                    }, $"Panel_{authorized}", "PromoteBTN");*/

                    panelIndex++;
                }
            }

            CuiHelper.DestroyUi(player, "AuthList");
            CuiHelper.AddUi(player, container);
        }

        private void UpgradeMaterial(BasePlayer player, string materialFrom = null)
        {
            var container = new CuiElementContainer();

            #region Top
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.969 0.922 0.882 0.035", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-113.095 360", OffsetMax = "188.693 616.546" }
            }, "Hud.Menu", "UpgradeMaterial");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.1294118 0.1372549 0.1098039 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150.895 98.938", OffsetMax = "150.895 128.942" }
            }, "UpgradeMaterial", "Top");

            container.Add(new CuiElement
            {
                Name = "Image_1931",
                Parent = "Top",
                Components = {
                    new CuiRawImageComponent { Color = "0.5137255 0.5294118 0.5450981 1", Png = GetImage("tclevels.icons.info") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-142.5 -11", OffsetMax = "-120.5 11" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Label_4892",
                Parent = "Top",
                Components = {
                    new CuiTextComponent { Text = "UPGRADE BASE MATERIAL", Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "0.909804 0.8705883 0.8352942 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-117.224 -15.002", OffsetMax = "117.822 14.517" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.7764707 0.2470588 0.1568628 1", Command = "tcupgrades.cmd close_UpgradeMat" },
                Text = { Text = "✘", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "130.3 -7.634", OffsetMax = "146.3 8.634" } //AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "126.3 -10", OffsetMax = "147.3 11"
            }, "Top", "Button_5678");

            #endregion

            #region FromMat

            container.Add(new CuiElement
            {
                Name = "FromMatText",
                Parent = "UpgradeMaterial",
                Components = {
                    new CuiTextComponent { Text = "SELECT MATERIAL TO UPGRADE", Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "0.909804 0.8705883 0.8352942 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-148.343 67.314", OffsetMax = "147.827 89.538" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-148.343 -10.313", OffsetMax = "147.83 67.313" }
            }, "UpgradeMaterial", "FromMat");

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0.1686275" },
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "1.587 -67", OffsetMax = "58.587 -10" }
            }, "FromMat", "Material");

            container.Add(new CuiElement
            {
                Name = "Image_5612",
                Parent = "Material",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("tclevels.icons.twig") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -25", OffsetMax = "25 25" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0.1686275" },
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "60.587 -67", OffsetMax = "117.587 -10" }
            }, "FromMat", "Material (1)");

            container.Add(new CuiElement
            {
                Name = "Image_5612",
                Parent = "Material (1)",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("tclevels.icons.wood") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -25", OffsetMax = "25 25" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0.1686275" },
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "119.587 -67", OffsetMax = "176.587 -10" }
            }, "FromMat", "Material (2)");

            container.Add(new CuiElement
            {
                Name = "Image_5612",
                Parent = "Material (2)",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("tclevels.icons.stone") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -25", OffsetMax = "25 25" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0.1686275" },
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "178.587 -67", OffsetMax = "235.587 -10" }
            }, "FromMat", "Material (3)");

            container.Add(new CuiElement
            {
                Name = "Image_5612",
                Parent = "Material (3)",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("tclevels.icons.metal") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -25", OffsetMax = "25 25" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0.1686275" },
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "237.586 -67", OffsetMax = "294.586 -10" }
            }, "FromMat", "Material (4)");

            container.Add(new CuiElement
            {
                Name = "Image_5612",
                Parent = "Material (4)",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("tclevels.icons.hqm") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -25", OffsetMax = "25 25" }
                }
            });

            // Add button to Material panel
            container.Add(new CuiButton
            {
                Button = { Command = "tcupgrades.cmd selectToUpgrade twig", Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Text = { Text = "", FontSize = 0 }
            }, "Material", "Button_Material");

            // Add button to Material (1) panel
            container.Add(new CuiButton
            {
                Button = { Command = "tcupgrades.cmd selectToUpgrade wood", Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Text = { Text = "", FontSize = 0 }
            }, "Material (1)", "Button_Material_1");

            // Add button to Material (2) panel
            container.Add(new CuiButton
            {
                Button = { Command = "tcupgrades.cmd selectToUpgrade stone", Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Text = { Text = "", FontSize = 0 }
            }, "Material (2)", "Button_Material_2");

            // Add button to Material (3) panel
            container.Add(new CuiButton
            {
                Button = { Command = "tcupgrades.cmd selectToUpgrade metal", Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Text = { Text = "", FontSize = 0 }
            }, "Material (3)", "Button_Material_3");

            // Add button to Material (4) panel
            container.Add(new CuiButton
            {
                Button = { Command = "tcupgrades.cmd selectToUpgrade hqm", Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Text = { Text = "", FontSize = 0 }
            }, "Material (4)", "Button_Material_4");

            #endregion

            #region To Mat

            if (!string.IsNullOrEmpty(materialFrom))
            {
                container.Add(new CuiElement
                {
                    Name = "ToMatText",
                    Parent = "UpgradeMaterial",
                    Components = {
                    new CuiTextComponent { Text = "UPGRADED MATERIAL", Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "0.909804 0.8705883 0.8352942 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-148.343 -40.612", OffsetMax = "147.827 -18.388" }
                }
                });

                container.Add(new CuiPanel
                {
                    Image = { Color = "1 1 1 0" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-148.087 -123.114", OffsetMax = "148.087 -45.487" }
                }, "UpgradeMaterial", "ToMat");

                if (materialFrom == "twig")
                {
                    container.Add(new CuiPanel
                    {
                        Image = { Color = "1 1 1 0.1686275" },
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "5.087 -75", OffsetMax = "75.087 -5" }
                    }, "ToMat", "MaterialSelect");

                    container.Add(new CuiElement
                    {
                        Name = "Image_5612",
                        Parent = "MaterialSelect",
                        Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("tclevels.icons.wood") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -25", OffsetMax = "25 25" }
                }
                    });

                    container.Add(new CuiPanel
                    {
                        Image = { Color = "1 1 1 0.1686275" },
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "77.087 -75", OffsetMax = "147.087 -5" }
                    }, "ToMat", "Material (1)Select");

                    container.Add(new CuiElement
                    {
                        Name = "Image_5612",
                        Parent = "Material (1)Select",
                        Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("tclevels.icons.stone") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -25", OffsetMax = "25 25" }
                }
                    });

                    container.Add(new CuiPanel
                    {
                        Image = { Color = "1 1 1 0.1686275" },
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "149.087 -75", OffsetMax = "219.087 -5" }
                    }, "ToMat", "Material (2)Select");

                    container.Add(new CuiElement
                    {
                        Name = "Image_5612",
                        Parent = "Material (2)Select",
                        Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("tclevels.icons.metal") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -25", OffsetMax = "25 25" }
                }
                    });

                    container.Add(new CuiPanel
                    {
                        Image = { Color = "1 1 1 0.1686275" },
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "221.086 -75", OffsetMax = "291.086 -5" }
                    }, "ToMat", "Material (3)Select");

                    container.Add(new CuiElement
                    {
                        Name = "Image_5612",
                        Parent = "Material (3)Select",
                        Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("tclevels.icons.hqm") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -25", OffsetMax = "25 25" }
                }
                    });

                    container.Add(new CuiButton
                    {
                        Button = { Command = $"tcupgrades.cmd upgradeTo {materialFrom} wood", Color = "0 0 0 0" },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                        Text = { Text = "", FontSize = 0 }
                    }, "MaterialSelect", "Button_MaterialSelect");

                    // Add button to Material (1)Select panel
                    container.Add(new CuiButton
                    {
                        Button = { Command = $"tcupgrades.cmd upgradeTo {materialFrom} stone", Color = "0 0 0 0" },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                        Text = { Text = "", FontSize = 0 }
                    }, "Material (1)Select", "Button_MaterialSelect_1");

                    // Add button to Material (2)Select panel
                    container.Add(new CuiButton
                    {
                        Button = { Command = $"tcupgrades.cmd upgradeTo {materialFrom} metal", Color = "0 0 0 0" },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                        Text = { Text = "", FontSize = 0 }
                    }, "Material (2)Select", "Button_MaterialSelect_2");

                    // Add button to Material (3)Select panel
                    container.Add(new CuiButton
                    {
                        Button = { Command = $"tcupgrades.cmd upgradeTo {materialFrom} hqm", Color = "0 0 0 0" },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                        Text = { Text = "", FontSize = 0 }
                    }, "Material (3)Select", "Button_MaterialSelect_3");
                }
                else if (materialFrom == "wood")
                {
                    container.Add(new CuiPanel
                    {
                        Image = { Color = "1 1 1 0.1686275" },
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "77.087 -75", OffsetMax = "147.087 -5" }
                    }, "ToMat", "Material (1)Select");

                    container.Add(new CuiElement
                    {
                        Name = "Image_5612",
                        Parent = "Material (1)Select",
                        Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("tclevels.icons.stone") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -25", OffsetMax = "25 25" }
                }
                    });

                    container.Add(new CuiPanel
                    {
                        Image = { Color = "1 1 1 0.1686275" },
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "149.087 -75", OffsetMax = "219.087 -5" }
                    }, "ToMat", "Material (2)Select");

                    container.Add(new CuiElement
                    {
                        Name = "Image_5612",
                        Parent = "Material (2)Select",
                        Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("tclevels.icons.metal") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -25", OffsetMax = "25 25" }
                }
                    });

                    container.Add(new CuiPanel
                    {
                        Image = { Color = "1 1 1 0.1686275" },
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "221.086 -75", OffsetMax = "291.086 -5" }
                    }, "ToMat", "Material (3)Select");

                    container.Add(new CuiElement
                    {
                        Name = "Image_5612",
                        Parent = "Material (3)Select",
                        Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("tclevels.icons.hqm") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -25", OffsetMax = "25 25" }
                }
                    });

                    // Add button to Material (1)Select panel
                    container.Add(new CuiButton
                    {
                        Button = { Command = $"tcupgrades.cmd upgradeTo {materialFrom} stone", Color = "0 0 0 0" },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                        Text = { Text = "", FontSize = 0 }
                    }, "Material (1)Select", "Button_MaterialSelect_1");

                    // Add button to Material (2)Select panel
                    container.Add(new CuiButton
                    {
                        Button = { Command = $"tcupgrades.cmd upgradeTo {materialFrom} metal", Color = "0 0 0 0" },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                        Text = { Text = "", FontSize = 0 }
                    }, "Material (2)Select", "Button_MaterialSelect_2");

                    // Add button to Material (3)Select panel
                    container.Add(new CuiButton
                    {
                        Button = { Command = $"tcupgrades.cmd upgradeTo {materialFrom} hqm", Color = "0 0 0 0" },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                        Text = { Text = "", FontSize = 0 }
                    }, "Material (3)Select", "Button_MaterialSelect_3");
                }
                else if (materialFrom == "stone")
                {
                    container.Add(new CuiPanel
                    {
                        Image = { Color = "1 1 1 0.1686275" },
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "149.087 -75", OffsetMax = "219.087 -5" }
                    }, "ToMat", "Material (2)Select");

                    container.Add(new CuiElement
                    {
                        Name = "Image_5612",
                        Parent = "Material (2)Select",
                        Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("tclevels.icons.metal") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -25", OffsetMax = "25 25" }
                }
                    });

                    container.Add(new CuiPanel
                    {
                        Image = { Color = "1 1 1 0.1686275" },
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "221.086 -75", OffsetMax = "291.086 -5" }
                    }, "ToMat", "Material (3)Select");

                    container.Add(new CuiElement
                    {
                        Name = "Image_5612",
                        Parent = "Material (3)Select",
                        Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("tclevels.icons.hqm") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -25", OffsetMax = "25 25" }
                }
                    });

                    // Add button to Material (2)Select panel
                    container.Add(new CuiButton
                    {
                        Button = { Command = $"tcupgrades.cmd upgradeTo {materialFrom} metal", Color = "0 0 0 0" },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                        Text = { Text = "", FontSize = 0 }
                    }, "Material (2)Select", "Button_MaterialSelect_2");

                    // Add button to Material (3)Select panel
                    container.Add(new CuiButton
                    {
                        Button = { Command = $"tcupgrades.cmd upgradeTo {materialFrom} hqm", Color = "0 0 0 0" },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                        Text = { Text = "", FontSize = 0 }
                    }, "Material (3)Select", "Button_MaterialSelect_3");
                }
                else if (materialFrom == "metal")
                {
                    container.Add(new CuiPanel
                    {
                        Image = { Color = "1 1 1 0.1686275" },
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "221.086 -75", OffsetMax = "291.086 -5" }
                    }, "ToMat", "Material (3)Select");

                    container.Add(new CuiElement
                    {
                        Name = "Image_5612",
                        Parent = "Material (3)Select",
                        Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("tclevels.icons.hqm") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -25", OffsetMax = "25 25" }
                }
                    });


                    // Add button to Material (3)Select panel
                    container.Add(new CuiButton
                    {
                        Button = { Command = $"tcupgrades.cmd upgradeTo {materialFrom} hqm", Color = "0 0 0 0" },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                        Text = { Text = "", FontSize = 0 }
                    }, "Material (3)Select", "Button_MaterialSelect_3");
                }
                else if (materialFrom == "hqm")
                {
                }

            }

            #endregion

            CuiHelper.DestroyUi(player, "UpgradeMaterial");
            CuiHelper.AddUi(player, container);
        }

        private void RepairBaseMaterial(BasePlayer player)
        {
            var container = new CuiElementContainer();

            #region TOP

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.969 0.922 0.882 0.035", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-113.095 360", OffsetMax = "188.693 616.546" }
            }, "Hud.Menu", "RepairBase");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.1294118 0.1372549 0.1098039 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150.895 98.938", OffsetMax = "150.895 128.942" }
            }, "RepairBase", "Top");

            container.Add(new CuiElement
            {
                Name = "Image_1931",
                Parent = "Top",
                Components = {
                    new CuiRawImageComponent { Color = "0.5137255 0.5294118 0.5450981 1", Png = GetImage("tclevels.icons.info") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-142.5 -11", OffsetMax = "-120.5 11" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Label_4892",
                Parent = "Top",
                Components = {
                    new CuiTextComponent { Text = "REPAIR BASE", Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "0.909804 0.8705883 0.8352942 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-117.224 -15.002", OffsetMax = "117.822 14.517" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.7764707 0.2470588 0.1568628 1", Command = "tcupgrades.cmd close_RepairBase" },
                Text = { Text = "✘", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "130.3 -7.634", OffsetMax = "146.3 8.634" }
            }, "Top", "Button_5678");

            #endregion

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-148.343 -125.287", OffsetMax = "147.83 96.386" }
            }, "RepairBase", "Main");

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0.1686275" },
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "5.572 -39.429", OffsetMax = "290.601 -5" }
            }, "Main", "RepairWood");

            container.Add(new CuiElement
            {
                Name = "Image_5547",
                Parent = "RepairWood",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("tclevels.icons.wood") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-136.8 -13.5", OffsetMax = "-109.8 13.5" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Label_4089",
                Parent = "RepairWood",
                Components = {
                    new CuiTextComponent { Text = "REPAIR WOOD", Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-101.357 -13.5", OffsetMax = "134.491 13.5" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0.1686275" },
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "5.572 -76.857", OffsetMax = "290.601 -42.429" }
            }, "Main", "RepairStone");

            container.Add(new CuiElement
            {
                Name = "Image_5547",
                Parent = "RepairStone",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("tclevels.icons.stone") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-136.8 -13.5", OffsetMax = "-109.8 13.5" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Label_4089",
                Parent = "RepairStone",
                Components = {
                    new CuiTextComponent { Text = "REPAIR STONE", Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-101.357 -13.5", OffsetMax = "134.491 13.5" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0.1686275" },
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "5.572 -114.285", OffsetMax = "290.601 -79.857" }
            }, "Main", "RepairMetal");

            container.Add(new CuiElement
            {
                Name = "Image_5547",
                Parent = "RepairMetal",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("tclevels.icons.metal") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-136.8 -13.5", OffsetMax = "-109.8 13.5" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Label_4089",
                Parent = "RepairMetal",
                Components = {
                    new CuiTextComponent { Text = "REPAIR METAL", Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-101.357 -13.5", OffsetMax = "134.491 13.5" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0.1686275" },
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "5.572 -151.714", OffsetMax = "290.601 -117.286" }
            }, "Main", "RepairHQM");

            container.Add(new CuiElement
            {
                Name = "Image_5547",
                Parent = "RepairHQM",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("tclevels.icons.hqm") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-136.8 -13.5", OffsetMax = "-109.8 13.5" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Label_4089",
                Parent = "RepairHQM",
                Components = {
                    new CuiTextComponent { Text = "REPAIR HQM", Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-101.357 -13.5", OffsetMax = "134.491 13.5" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0.1686275" },
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "5.572 -189.143", OffsetMax = "290.601 -154.714" }
            }, "Main", "RepairDeploy");

            container.Add(new CuiElement
            {
                Name = "Image_5547",
                Parent = "RepairDeploy",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("tclevels.icons.deoply") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-136.8 -13.5", OffsetMax = "-109.8 13.5" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Label_4089",
                Parent = "RepairDeploy",
                Components = {
                    new CuiTextComponent { Text = "REPAIR DEPLOYABLES", Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-101.357 -13.5", OffsetMax = "134.491 13.5" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Command = "tcupgrades.cmd repairBaseMat wood", Color = "0.2 0.5 0.2 0" }, // Command for repairing wood
                Text = { Text = "", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
            }, "RepairWood", "Button_RepairWood");

            container.Add(new CuiButton
            {
                Button = { Command = "tcupgrades.cmd repairBaseMat stone", Color = "0.4 0.4 0.4 0" }, // Command for repairing stone
                Text = { Text = "", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
            }, "RepairStone", "Button_RepairStone");

            container.Add(new CuiButton
            {
                Button = { Command = "tcupgrades.cmd repairBaseMat metal", Color = "0.6 0.6 0.6 0" }, // Command for repairing metal
                Text = { Text = "", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
            }, "RepairMetal", "Button_RepairMetal");

            container.Add(new CuiButton
            {
                Button = { Command = "tcupgrades.cmd repairBaseMat hqm", Color = "0.7 0.7 0.7 0" }, // Command for repairing HQM
                Text = { Text = "", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
            }, "RepairHQM", "Button_RepairHQM");

            container.Add(new CuiButton
            {
                Button = { Command = "tcupgrades.cmd repairBaseMat deploy", Color = "0.9 0.9 0.9 0" }, // Command for repairing deployables
                Text = { Text = "", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
            }, "RepairDeploy", "Button_RepairDeploy");

            CuiHelper.DestroyUi(player, "RepairBase");
            CuiHelper.AddUi(player, container);
        }

        private void SkinBaseUpgrade(BasePlayer player, bool ShowColors = false)
        {
            #region Topp

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.969 0.922 0.882 0.035", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-113.095 360", OffsetMax = "188.693 616.546" }
            }, "Hud.Menu", "SkinBase");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.1294118 0.1372549 0.1098039 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150.895 98.938", OffsetMax = "150.895 128.942" }
            }, "SkinBase", "Top");

            container.Add(new CuiElement
            {
                Name = "Image_1931",
                Parent = "Top",
                Components = {
                    new CuiRawImageComponent { Color = "0.5137255 0.5294118 0.5450981 1", Png = GetImage("tclevels.icons.info") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-142.5 -11", OffsetMax = "-120.5 11" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Label_4892",
                Parent = "Top",
                Components = {
                    new CuiTextComponent { Text = "CHANGE BUILDING SKIN", Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "0.909804 0.8705883 0.8352942 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-117.224 -15.002", OffsetMax = "117.822 14.517" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.7764707 0.2470588 0.1568628 1", Command = "tcupgrades.cmd close_SkinBase" },
                Text = { Text = "✘", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "130.3 -7.634", OffsetMax = "146.3 8.634" }
            }, "Top", "Button_5678");

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-148.343 -124.141", OffsetMax = "147.83 98.937" }
            }, "SkinBase", "FromMat");

            #endregion

            container.Add(new CuiElement
            {
                Parent = "FromMat",
                Name = "ScrollView",
                Components =
                {
                    new CuiImageComponent { Color = "0 0 0 0" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                    new CuiScrollViewComponent
                    {
                        Vertical = true,
                        MovementType = ScrollRect.MovementType.Clamped,
                        Elasticity = 0.3f,
                        Inertia= true,
                        DecelerationRate = 0.135f,
                        ScrollSensitivity = 6f,
                        ContentTransform = new CuiRectTransformComponent {
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = "0 -500",
                            OffsetMax = "0 0"
                        },
                        VerticalScrollbar = new CuiScrollbar { Invert = true, Size = 0f, HandleColor = "0 0 0 0", HighlightColor = "0 0 0 0", TrackColor = "0 0 0 0", PressedColor = "0 0 0 0" },
                        HorizontalScrollbar = new CuiScrollbar { Invert = true, Size = 0f, HandleColor = "0 0 0 0", HighlightColor = "0 0 0 0", TrackColor = "0 0 0 0", PressedColor = "0 0 0 0" },
                    }
                }
            });

            int index = 0;
            int row = 0;
            int col = 0;

            if (ShowColors)
            {
                foreach (var color in MetalColors)
                {
                    if (color.Key == 9999)
                    {
                        container.Add(new CuiElement
                        {
                            Name = $"Material{index}",
                            Parent = "ScrollView",
                            Components = {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("tclevels.icons.random") },
                                new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{5.087 + (row * 72)} {-75 - (col * 72)}", OffsetMax = $"{75.087 + (row * 72)} {-5 - (col * 72)}" }
                            }
                        });
                    }
                    else
                    {
                        container.Add(new CuiPanel
                        {
                            Image = { Color = color.Value },
                            RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{5.087 + (row * 72)} {-75 - (col * 72)}", OffsetMax = $"{75.087 + (row * 72)} {-5 - (col * 72)}" }
                        }, "ScrollView", $"Material{index}");
                    }
                    

                    container.Add(new CuiButton
                    {
                        Button = { Command = $"tcupgrades.cmd skinBaseSetColor {color.Key} 10221", Color = "0.9 0.9 0.9 0" },
                        Text = { Text = "", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                    }, $"Material{index}", "Button_Skin");

                    row++;
                    index++;
                    if (row >= 4)
                    {
                        row = 0;
                        col++;
                    }
                }
            }
            else
            {
                foreach (var skin in SkinBase)
                {
                    container.Add(new CuiPanel
                    {
                        Image = { Color = "1 1 1 0.1686275" },
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{5.087 + (row * 72)} {-75 - (col * 72)}", OffsetMax = $"{75.087 + (row * 72)} {-5 - (col * 72)}" }
                    }, "FromMat", $"Material{index}");

                    container.Add(new CuiElement
                    {
                        Name = "Image_5612",
                        Parent = $"Material{index}",
                        Components = {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage(skin.Value) },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -25", OffsetMax = "25 25" }
                    }
                    });

                    container.Add(new CuiButton
                    {
                        Button = { Command = $"tcupgrades.cmd skinBaseStart {skin.Key}", Color = "0.9 0.9 0.9 0" },
                        Text = { Text = "", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                    }, $"Material{index}", "Button_Skin");

                    row++;
                    index++;
                    if (row >= 4)
                    {
                        row = 0;
                        col++;
                    }
                }
            }

            CuiHelper.DestroyUi(player, "SkinBase");
            CuiHelper.AddUi(player, container);
        }

        private void ChangeWallpaper(BasePlayer player)
        {
            var container = new CuiElementContainer();

            #region Top
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.969 0.922 0.882 0.035", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-113.095 360", OffsetMax = "188.693 616.546" }
            }, "Hud.Menu", "WallpaperCg");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.1294118 0.1372549 0.1098039 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150.895 98.938", OffsetMax = "150.895 128.942" }
            }, "WallpaperCg", "Top");

            container.Add(new CuiElement
            {
                Name = "Image_1931",
                Parent = "Top",
                Components = {
                    new CuiRawImageComponent { Color = "0.5137255 0.5294118 0.5450981 1", Png = GetImage("tclevels.icons.info") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-142.5 -11", OffsetMax = "-120.5 11" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Label_4892",
                Parent = "Top",
                Components = {
                    new CuiTextComponent { Text = "CHANGE WALLPAPER", Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "0.909804 0.8705883 0.8352942 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-117.224 -15.002", OffsetMax = "117.822 14.517" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.7764707 0.2470588 0.1568628 1", Command = "tcupgrades.cmd close_Wallpapers" },
                Text = { Text = "✘", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "130.3 -7.634", OffsetMax = "146.3 8.634" }
            }, "Top", "Button_5678");

            #endregion

            #region FromMat

            container.Add(new CuiElement
            {
                Name = "FromMatText",
                Parent = "WallpaperCg",
                Components = {
                    new CuiTextComponent { Text = "SELECT MATERIAL", Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "0.909804 0.8705883 0.8352942 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-148.343 67.314", OffsetMax = "147.827 89.538" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-148.343 -10.313", OffsetMax = "147.83 67.313" }
            }, "WallpaperCg", "FromMat");

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0.1686275" },
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "1.587 -67", OffsetMax = "58.587 -10" }
            }, "FromMat", "Material");

            container.Add(new CuiElement
            {
                Name = "Image_5612",
                Parent = "Material",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("tclevels.icons.twig") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -25", OffsetMax = "25 25" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0.1686275" },
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "60.587 -67", OffsetMax = "117.587 -10" }
            }, "FromMat", "Material (1)");

            container.Add(new CuiElement
            {
                Name = "Image_5612",
                Parent = "Material (1)",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("tclevels.icons.wood") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -25", OffsetMax = "25 25" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0.1686275" },
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "119.587 -67", OffsetMax = "176.587 -10" }
            }, "FromMat", "Material (2)");

            container.Add(new CuiElement
            {
                Name = "Image_5612",
                Parent = "Material (2)",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("tclevels.icons.stone") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -25", OffsetMax = "25 25" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0.1686275" },
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "178.587 -67", OffsetMax = "235.587 -10" }
            }, "FromMat", "Material (3)");

            container.Add(new CuiElement
            {
                Name = "Image_5612",
                Parent = "Material (3)",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("tclevels.icons.metal") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -25", OffsetMax = "25 25" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0.1686275" },
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "237.586 -67", OffsetMax = "294.586 -10" }
            }, "FromMat", "Material (4)");

            container.Add(new CuiElement
            {
                Name = "Image_5612",
                Parent = "Material (4)",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage("tclevels.icons.hqm") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -25", OffsetMax = "25 25" }
                }
            });

            // Add button to Material panel
            container.Add(new CuiButton
            {
                Button = { Command = "tcupgrades.cmd viewWallpaper twig", Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Text = { Text = "", FontSize = 0 }
            }, "Material", "Button_Material");

            // Add button to Material (1) panel
            container.Add(new CuiButton
            {
                Button = { Command = "tcupgrades.cmd viewWallpaper wood", Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Text = { Text = "", FontSize = 0 }
            }, "Material (1)", "Button_Material_1");

            // Add button to Material (2) panel
            container.Add(new CuiButton
            {
                Button = { Command = "tcupgrades.cmd viewWallpaper stone", Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Text = { Text = "", FontSize = 0 }
            }, "Material (2)", "Button_Material_2");

            // Add button to Material (3) panel
            container.Add(new CuiButton
            {
                Button = { Command = "tcupgrades.cmd viewWallpaper metal", Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Text = { Text = "", FontSize = 0 }
            }, "Material (3)", "Button_Material_3");

            // Add button to Material (4) panel
            container.Add(new CuiButton
            {
                Button = { Command = "tcupgrades.cmd viewWallpaper hqm", Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Text = { Text = "", FontSize = 0 }
            }, "Material (4)", "Button_Material_4");

            #endregion

            CuiHelper.DestroyUi(player, "WallpaperCg");
            CuiHelper.AddUi(player, container);
        }

        private void ShowWallpapers(BasePlayer player, string Material)
        {
            #region Topp

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.969 0.922 0.882 0.035", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-113.095 360", OffsetMax = "188.693 616.546" }
            }, "Hud.Menu", "ShowWallpaper");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.1294118 0.1372549 0.1098039 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150.895 98.938", OffsetMax = "150.895 128.942" }
            }, "ShowWallpaper", "Top");

            container.Add(new CuiElement
            {
                Name = "Image_1931",
                Parent = "Top",
                Components = {
                    new CuiRawImageComponent { Color = "0.5137255 0.5294118 0.5450981 1", Png = GetImage("tclevels.icons.info") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-142.5 -11", OffsetMax = "-120.5 11" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Label_4892",
                Parent = "Top",
                Components = {
                    new CuiTextComponent { Text = "SELECT WALLPAPER", Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "0.909804 0.8705883 0.8352942 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-117.224 -15.002", OffsetMax = "117.822 14.517" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.7764707 0.2470588 0.1568628 1", Command = "tcupgrades.cmd close_WallpaperShow" },
                Text = { Text = "✘", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "130.3 -7.634", OffsetMax = "146.3 8.634" }
            }, "Top", "Button_5678");

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-148.343 -124.141", OffsetMax = "147.83 98.937" }
            }, "ShowWallpaper", "FromMat");

            #endregion

            container.Add(new CuiElement
            {
                Parent = "FromMat",
                Name = "ScrollView",
                Components =
                {
                    new CuiImageComponent { Color = "0 0 0 0" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                    new CuiScrollViewComponent
                    {
                        Vertical = true,
                        MovementType = ScrollRect.MovementType.Clamped,
                        Elasticity = 0.3f,
                        Inertia= true,
                        DecelerationRate = 0.135f,
                        ScrollSensitivity = 6f,
                        ContentTransform = new CuiRectTransformComponent {
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = "0 -500",
                            OffsetMax = "0 0"
                        },
                        VerticalScrollbar = new CuiScrollbar { Invert = true, Size = 0f, HandleColor = "0 0 0 0", HighlightColor = "0 0 0 0", TrackColor = "0 0 0 0", PressedColor = "0 0 0 0" },
                        HorizontalScrollbar = new CuiScrollbar { Invert = true, Size = 0f, HandleColor = "0 0 0 0", HighlightColor = "0 0 0 0", TrackColor = "0 0 0 0", PressedColor = "0 0 0 0" },
                    }
                }
            });

            int index = 0;
            int row = 0;
            int col = 0;

            foreach (var item in WallpaperSkins)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = "0.969 0.922 0.882 0.035" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{5.087 + (row * 72)} {-75 - (col * 72)}", OffsetMax = $"{75.087 + (row * 72)} {-5 - (col * 72)}" }
                }, "ScrollView", $"Material{index}");

                container.Add(new CuiElement
                {
                    Name = "Image_5612",
                    Parent = $"Material{index}",
                    Components = {
                        new CuiImageComponent { Color = "1 1 1 1", SkinId = item.Key, ItemId = item.Value },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -25", OffsetMax = "25 25" }
                    }
                });

                container.Add(new CuiButton
                {
                    Button = { Command = $"tcupgrades.cmd changeWallpaper {item.Key} {Material}", Color = "0.9 0.9 0.9 0" },
                    Text = { Text = "", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                }, $"Material{index}", "Button_Skin");

                row++;
                index++;
                if (row >= 4)
                {
                    row = 0;
                    col++;
                }
            }

            CuiHelper.DestroyUi(player, "ShowWallpaper");
            CuiHelper.AddUi(player, container);
        }

        private void EffectBTN(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiButton
            {
                Button = { Color = playersState[player.userID].ShowEffect ? "0.3921569 0.482353 0.2352941 1" : "0.7764707 0.2470588 0.1568628 1", Command = "tcupgrades.cmd changeEffects" },
                Text = { Text = "", Font = "robotocondensed-regular.ttf", FontSize = 8, Align = TextAnchor.MiddleCenter, Color = "0.9098039 0.8705882 0.8352941 1" },
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "115.904 594.579", OffsetMax = "164.737 610.847" }
            }, "Hud.Menu", "EffectBTN");

            container.Add(new CuiElement
            {
                Name = "Label_4892",
                Parent = "EffectBTN",
                Components = {
                    new CuiTextComponent { Text = playersState[player.userID].ShowEffect ? Msg(player, "EFFECTSON"): Msg(player, "EFFECTSOFF"), Font = "robotocondensed-regular.ttf", FontSize = 8, Align = TextAnchor.MiddleCenter, Color = "0.909804 0.8705883 0.8352942 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                }
            });

            CuiHelper.DestroyUi(player, "EffectBTN");
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Hooks

        private object OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade, ulong skin)
        {
            if (config.General.autoUponBuild)
            {
                if (block.SecondsSinceAttacked < 30f)
                {
                    block.OnRepairFailed(player, string.Format("Unable to repair: Recently damaged. Repairable in: {0:N0}s.", 30f - block.SecondsSinceAttacked));
                    return false;
                }

                if (skin != 0 && player.blueprints.steamInventory.HasItem((int)skin)) return null;
                var skinID = GetNatID(GetPlayerSkinID(player, grade));

                if (block.skinID != 0 && grade.ToString() == "Metal") block.SetCustomColour(GetPlayerMetalColorID(player, BuildingGrade.Enum.Metal));
                if (block.skinID == skinID && block.grade == grade) return false;
                
                NextTick(() =>
                {
                    if (block == null || block.IsDestroyed) return;
                    block.skinID = skinID;
                    block.ChangeGradeAndSkin(block.grade, skinID, false, true);
                    block.ClientRPC(null, "DoUpgradeEffect", (int)block.grade, skinID);
                    if (block.skinID != 0 && grade.ToString() == "Metal") block.SetCustomColour(GetPlayerMetalColorID(player, BuildingGrade.Enum.Metal));
                });
            }

            return null;
        }

        private void OnStructureGradeUpdated(BuildingBlock block, BasePlayer player, BuildingGrade.Enum oldGrade, BuildingGrade.Enum newGrade)
        {
            OnStructureUpgrade(block, player, newGrade, block.skinID);
        }

        private void OnLootEntity(BasePlayer player, BuildingPrivlidge entity)
        {
            DestroyUI(player);
            CreateUpgradeButton(player, entity.net.ID.Value, entity);
            if (!playersState.TryGetValue(player.userID, out PlayerState value))
            {
                value = new PlayerState();
                playersState.Add(player.userID, value);
            }
            value.UIon = true;
        }

        private void OnLootEntityEnd(BasePlayer player, BuildingPrivlidge entity)
        {
            DestroyUI(player);
            if (!playersState.TryGetValue(player.userID, out PlayerState value))
            {
                value = new PlayerState();
                playersState.Add(player.userID, value);
            }
            value.UIon = false;
        }

        #endregion

        #region Helpers

        private void SendMessage(BasePlayer player, string message, params object[] args)
        {
            if (config.notifications.EenableChatNotify)
            {
                Player.Message(player, Msg(player, message, args), config.notifications.Prefix, config.notifications.Avatarid);
            }

            if (config.notifications.EenableGameTipnotify)
            {
                ShowGameTip(player, message, args);
            }
        }

        private void ShowGameTip(BasePlayer player, string message, params object[] args)
        {
            Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player.GetBuildingPrivilege().transform.position);
            player.SendConsoleCommand("gametip.showgametip", Msg(player, message, args));
            timer.Once(5, () => player.SendConsoleCommand("gametip.hidegametip"));
        }

        private void SendNotification(BasePlayer player, string type, string message, params object[] args)
        {
            if (type == "error")
            {
                DoEffect(player.transform.position, "assets/prefabs/weapons/toolgun/effects/repairerror.prefab");
            }

            string notificationId = type switch
            {
                "error" => config.notifications.NotificationTypes.Error,
                "info" => config.notifications.NotificationTypes.Info,
                "success" => config.notifications.NotificationTypes.Success,
                _ => config.notifications.NotificationTypes.Info
            };

            if (config.notifications.NotificationPlugin == NotificationPluginType.Toastify && Toastify != null)
            {
                Toastify.CallHook("SendToast", player, notificationId, GetPropertyName(type), Msg(player, message, args), 3f);
            }
            else if (config.notifications.NotificationPlugin == NotificationPluginType.Notify && Notify != null)
            {
                if (int.TryParse(notificationId, out int notificationType))
                {
                    Notify.CallHook("SendNotify", player, notificationType, Msg(player, message, args));
                }
            }
        }

        private string Msg(BasePlayer player, string key, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, player.UserIDString), args);
        }

        private void DoEffect(Vector3 position, string effect)
        {
            if (!string.IsNullOrEmpty(effect))
            {
                Effect.server.Run(effect, position, Vector3.zero, null, true);
            }
        }

        private void RegisterImages()
        {
            ImageLibrary.CallHook("ImportImageList", Name, imageList, 0ul, true);
        }

        private string GetImage(string name)
        {
            return ImageLibrary.Call<string>("GetImage", name, 0ul, true);
        }

        private class UIBuilder
        {
            public CuiElementContainer container;

            public class Colors
            {
                public const string Green = "0.4 0.49 0.24 1";
                public const string Red = "0.81 0.26 0.17 1";
                public const string Text = "0.96 0.92 0.88 1";
                public const string Grey = "0.39 0.39 0.39 1";
                public const string DefaultRust = "0.969 0.922 0.882 0.035";
            }

            public UIBuilder()
            {
                container = new CuiElementContainer();
            }

            public void Add(BasePlayer player)
            {
                CuiHelper.AddUi(player, container);
            }

            public void AddPanel(string parent, string color, string anchor, string offset, string material = "assets/content/ui/uibackgroundblur-ingamemenu.mat", string name = null)
            {
                name ??= CuiHelper.GetGuid();
                CuiElement element = new CuiElement()
                {
                    Name = name,
                    Parent = parent,
                    DestroyUi = name,
                    Components = {
                        ParseDimensions(anchor, offset),
                        new CuiImageComponent()
                        {
                            Color = color,
                            Material = material
                        }
                    }
                };

                container.Add(element);
            }

            public void AddText(string parent, string text, string color, string anchor, string offset, int fontSize = 12, TextAnchor align = TextAnchor.MiddleLeft, string name = null)
            {
                name ??= CuiHelper.GetGuid();
                CuiElement element = new CuiElement()
                {
                    Name = name,
                    Parent = parent,
                    DestroyUi = name,
                    Components = {
                        ParseDimensions(anchor, offset),
                        new CuiTextComponent()
                        {
                            Color = string.IsNullOrEmpty(color) ? Colors.Text : color,
                            Text = text,
                            Align = align,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = fontSize
                        }
                    }
                };

                container.Add(element);
            }

            public void AddImage(string parent, string url, string anchor, string offset, int itemId = 0)
            {
                string name = CuiHelper.GetGuid();
                bool isPng = string.IsNullOrEmpty(url) ? false : url.All(char.IsDigit);

                CuiElement element = new CuiElement();
                element.Parent = parent;
                element.Name = name;
                element.DestroyUi = name;
                element.Components.Add(ParseDimensions(anchor, offset));

                if (itemId == 0)
                {
                    element.Components.Add(new CuiRawImageComponent()
                    {
                        Png = isPng ? url : null,
                        Url = isPng ? null : url
                    });
                }
                else
                {
                    element.Components.Add(new CuiImageComponent()
                    {
                        ItemId = itemId
                    });
                }

                container.Add(element);
            }

            public void AddButton(string parent, string color, string text, string command, string anchor, string offset, int fontSize = 12, string name = null, float fadeIn = 0f)
            {
                name ??= CuiHelper.GetGuid();
                CuiRectTransformComponent rect = ParseDimensions(anchor, offset);
                CuiButton element = new CuiButton()
                {
                    RectTransform = {
                        AnchorMin = rect.AnchorMin,
                        AnchorMax = rect.AnchorMax,
                        OffsetMin = rect.OffsetMin,
                        OffsetMax = rect.OffsetMax
                    },
                    Button = {
                        FadeIn = fadeIn,
                        Close = command.StartsWith(":close:") ? command.Substring(7) : null,
                        Command = command.StartsWith(":close:") ? null : command,
                        Color = color,
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    },
                    Text = {
                        FadeIn = fadeIn,
                        Text = text,
                        Align = TextAnchor.MiddleCenter,
                        FontSize = fontSize
                    }
                };

                container.Add(element, parent, name, name);
            }

            private CuiRectTransformComponent ParseDimensions(string anchor, string offset)
            {
                string[] anchors = anchor.Split(' ');
                string[] offsets = offset.Split(' ');

                return new CuiRectTransformComponent()
                {
                    AnchorMin = $"{anchors[0]} {anchors[1]}",
                    AnchorMax = $"{anchors[2]} {anchors[3]}",
                    OffsetMin = $"{offsets[0]} {offsets[1]}",
                    OffsetMax = $"{offsets[2]} {offsets[3]}"
                };
            }
        }

        #endregion


    }
}
