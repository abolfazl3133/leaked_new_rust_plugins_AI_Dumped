using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using UnityEngine;
using System.Globalization;
using System.Threading.Tasks;

/*
 *              _           _    _______                _       __      ______  
 *     /\      | |         (_)  |__   __|              | |      \ \    / /___ \ 
 *    /  \   __| |_ __ ___  _ _ __ | | ___   __ _  __ _| | ___   \ \  / /  __) |
 *   / /\ \ / _` | '_ ` _ \| | '_ \| |/ _ \ / _` |/ _` | |/ _ \   \ \/ /  |__ < 
 *  / ____ \ (_| | | | | | | | | | | | (_) | (_| | (_| | |  __/    \  /   ___) |
 * /_/    \_\__,_|_| |_| |_|_|_| |_|_|\___/ \__, |\__, |_|\___|     \/   |____/ 
 *                                           __/ | __/ |                        
 *                                          |___/ |___/                                            
*/

/* LICENCE
 *  SOFTWARE LICENCE AGREEMENT OF AdminToggle
 *      The following Software License Agreement constitutes a legally binding agreement between You, (the "Buyer") and https://steamcommunity.com/id/InfinityNet/ (the "Vendor").
 *      This Software License Agreement by https://steamcommunity.com/id/InfinityNet/ (the "Vendor") grants the right to use AdminToggle (the "Software") respecting the license limitations and restrictions below.
 *      By acquiring a copy of AdminToggle (the "Software"), You, the original licensee is legally bound to the license (the "License") Disclaimers, Limitations and Restrictions. Failing to comply with any of the terms of this Software license Agreement (the "License"), the license ("License") shall be terminated. On termination of the license ("License"), You are legally obliged to stop all use of AdminToggle (the "Software") immediately.
 *      Please read this Software License Agreement (the "License") thoroughly before acquiring a copy of AdminToggle (the "Software").
 *
 *  GRANT OF LICENCE
 *      This agreement (the "License") by https://steamcommunity.com/id/InfinityNet/ (the "Vendor") grants the Licensee exclusive, non-transferable, personal-use of the license (the "License") to use this AdminToggle (the "Software").
 *
 *  LICENSE DISCLAIMERS, LIMITATIONS AND RESTRICTIONS
 *      AdminToggle (the "Software") is provided "AS IS" from https://steamcommunity.com/id/InfinityNet/ (the "Vendor") without any warranties of any kind.
 *      AdminToggle (the "Software") may not be modified, changed, adapted, reverse-engineered, de-compiled, Reproduced, Distributed, Resold, Published or shared without the authority of the https://steamcommunity.com/id/InfinityNet/ (the "Vendor")
 *      AdminToggle (the "Software") license (the "License") is restricted to a maximum of 5 installations per license (the "License"). The Licensee may only install AdminToggle (the "Software") on computer installations owned or leased by the Licensee.
*/

/*VERSION 3.1.16
 * Fixed FPF
        player.inventory.AllItems() -> player.inventory.GetAllItems()
        player.MountObject() -> player.SetMounted()
 */
/*VERSION 3.1.15
 * Imported XLIB the dll file (So its no longer required)
 * Broke HttpClient ??? Error while compiling: AdminToggle.cs(3663,23): error CS0584: Internal compiler error: Assembly 'System.Net.Http, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' is a missing assembly and does not support the requested operation.
 */
/*VERSION 3.1.14
 * DLL recompiled
 */
/*VERSION 3.1.13
 * DLL recompiled
 */
/*VERSION 3.1.12
 * DLL recompiled
 */
/*VERSION 3.1.11
 * DLL recompiled
 */
/*VERSION 3.1.10
 * Fixed CUI Error
 */
/*VERSION 3.1.9
 * DLL recompiled
 */
/*VERSION 3.1.8
 * Minor changes
 * DLL recompiled
 */
/*VERSION 3.1.7
 * Rust broke XLIB.PLAYER.Location.Utility.UnMountChair & MountChair
 */
/*VERSION 3.1.6
 * Updated OnActiveItemChanged, CanEquipItem to not remove weapons when switching to player
 * Add system to check if player is switching between admin & player LevelHelper.IsSwitching
 * Fixed incorrect third party plugin loading
 * Fixed functionally of RevertOnTrigger
 * Fixed OnUserPermissionRevoked HardUnload
 * Changed XLIB Inventory management to save item names (DLL Updated)
 */
/*VERSION 3.1.5
 * Minor changes
 * DLL recompiled
 */
/* VERSION 3.1.4
 * Added option to lock admin outfit
 * Added option to customize all interface elements
 * Fixed hook that could spam create items
 * Improved minor changes & Third-Party Plugins updates
 * Improved oxide group toggling simplified
 */
/* VERSION 3.1.3
 * Removed unnecessary hooks
 */
/* VERSION 3.1.2
 * Added detection of permission revoked while admin mode active then gets untoggled
 * Added many new limited actions (Oxide Hooks) & Improved
 * Added option to specify whenever a plugin should be On or Off
 * Added option to specify auth level for modes onAdmin
 * Changed option to toggle multiple onAdmin, onPlayer
 * Improved Blocking of commands & Plugins
 * Improved Third-Party Plugins Hooks
 * Improved HandleCommands method 
 */
/* VERSION 3.0.1
 *  Added support for config manager plugin addon
*/
/* VERSION 3.0.0
 *  Release - Complete rewrite
*/

namespace Oxide.Plugins
{
    [Info("AdminToggle", "InfinityNet/Xray", "3.1.16")]
    [Description("Allows admins with permission to toggle between player & admin mode")]
    public class AdminToggle : RustPlugin
    {
        private const DebugType DEBUG = DebugType.Disabled;
        private const string constPluginName = "admintoggle";
        private const string SignatureColor = "#faaf19";
        private const string CooldownDelay = "1"; /*Toggle Cooldown Secs*/
        private const int FileLineLength = 4; /*eg 1000 = 4*/
        private const string PanicCommand = "at.fix";
        private static AdminToggle Instance { get; set; }
        [PluginReference] private readonly Core.Plugins.Plugin Vanish, AdminRadar, Backpacks, Godmode;

        #region Localization 
        private class LangMessages
        {
            internal const string StopFlying = "%Please% Stop flying while toggling as it might cause a server violation";
            internal const string PleaseWait = "%Please% Wait %" + CooldownDelay + "% second to allow the server to update";
            internal const string NoPerm = "%You% Do not have permission to use %{mode.permission}% mode";
            internal const string PermRevoked = "Permission to use %{mode.permission}% mode has been %revoked%";
            internal const string NoPermGenetic = "%You% Are not assigned to %any% admin modes!";
            internal const string NoPermTargetMode = "%You% Do not have permission to use %{mode.permission}% mode";
            internal const string NoReason = "%You% Are missing a reason to toggle %{mode.permission}% mode";
            internal const string NoBuilding = "%Placing% buildings, deployables is %obstructed%";
            internal const string NoCrafting = "%Crafting% of items is %obstructed%";
            internal const string NoDropItems = "%You% Are obstructed from %dropping% items";
            internal const string RestrictedMode = "%{mode.permission}% Mode is restricted to specfic ids & You're not included";
            internal const string RestrictedModeFailed = "%{mode.permission}% Mode is restricted to specfic ids & The restricted ids list couldn't be specified";
            internal const string ToggleDowned = "%You% Cannot toggle modes while being downed!";
            internal const string ToggleCrawling = "%You% Cannot toggle modes while crawling!";
            internal const string ToggleDead = "%You% Cannot toggle modes while being dead!";
            internal const string ToggleModeNotFound = "Couldn't find any mode by %{arg}%";
            internal const string NoDamageStructures = "%You% Are obstructed from dealing damage to structures!";
            internal const string NoHurtPlayers = "%You% Are obstructed from hurting players!";
            internal const string NoLootPlayers = "%You% Are obstructed from looting players!";
            internal const string NoLootEntities = "%You% Are obstructed from looting or interacting with entities!";
            internal const string NoUseWeapons = "%You% Are obstructed from interacting any weapons!";
            internal const string ViolationBannable = "%You% Triggered a bannable violation: %{violation.name}%";
            internal const string ViolationRegular = "%You% Triggered a violation: %{violation.name}%";
            internal const string RestrictedCommand = "%You% Are obstructed from using the command: %{command.name}%";
            internal const string NoPluginUsage = "%You% Are obstructed from using the plugin: %{plugin.name}%";
            internal const string PluginNoInstalled = "%{plugin.name}% Isn't installed";
            internal const string PlayerNotFound = "Couldn't find any player by %{arg}%";
            internal const string TooManyArgs = "%You% May only have %{arg}% Identifiers";
            internal const string InvalidArgs = "%You% Must provide a %identifier%";
        }
        #endregion Localization

        #region Configuration
        private static ConfigData _configuration { get; set; } = new ConfigData();
        public class ConfigData
        {
            public Modes[] MODES { get; set; } = { new Modes() };
            public class Modes
            {
                [JsonProperty(PropertyName = "Permission")]
                public string Permission { get; set; } = "master";

                [JsonProperty(PropertyName = "Priority")]
                public int Priority { get; set; } = 999;

                [JsonProperty(PropertyName = "Master Level CAREFUL! (Enabling this does the following #Overrides priority, mode & permission system! #Allows to set/get modes for yourself or others! #Overrides limitations by current or lower modes")]
                public bool IsMaster { get; set; } = false;

                [JsonProperty(PropertyName = "Toggle Commands")]
                public string[] Commands { get; set; } = { "admin", "mode" };

                [JsonProperty(PropertyName = "Restrict Mode To Specfic SteamIds (Leave blank to disable)")]
                public object[] SpecficIds { get; set; } = { };

                [JsonProperty(PropertyName = "Settings")]
                public Settings SETTINGS { get; set; } = new Settings();
                public class Settings
                {
                    [JsonProperty(PropertyName = "On Admin")]
                    public OnAdminData OnAdmin { get; set; } = new OnAdminData();
                    public class OnAdminData
                    {
                        [JsonProperty(PropertyName = "Require Reason")]
                        public bool RequireReason { get; set; } = false;

                        [JsonProperty(PropertyName = "Autorun Commands Use Forward Slash '/' For Chat-Commands & Leave It Blank For Console-Commands")]
                        public string[] AutoRunCommands { get; set; } = new string[] { };

                        [JsonProperty(PropertyName = "Toggle Groups To Grant (Leave blank to disable)")]
                        public string[] ToggleGroups { get; set; } = new string[] { };

                        [JsonProperty(PropertyName = "Specified Auth Level (1 = moderators, 2 = owners) Must either be 1 or 2 cannot be below or above")]
                        public int AuthLevel { get; set; } = 2;

                        [JsonProperty(PropertyName = "Keep Separate Inventories")]
                        public bool SeparateInventories { get; set; } = true;

                        [JsonProperty(PropertyName = "Teleport Back Upon Exiting")]
                        public bool TeleportBack { get; set; } = false;

                        [JsonProperty(PropertyName = "Revert On Disconnect, Restart, Reload")]
                        public bool RevertOnTrigger { get; set; } = false;

                        [JsonProperty(PropertyName = "Ignore Server Violations (Bans, Kicks Etc) (Recommended to keep true)")]
                        public bool IgnoreViolations { get; set; } = true;

                        [JsonProperty(PropertyName = "Blocked Commands")]
                        public string[] BlockedCommands { get; set; } = new string[] { };

                        [JsonProperty(PropertyName = "Name Prefix Changes your name to a set prefix (Leave blank to disable)")]
                        public string Name { get; set; } = "";

                        [JsonProperty(PropertyName = "Admin Outfit")]
                        public AdminOutfitData AdminOutfit { get; set; } = new AdminOutfitData();
                        public class AdminOutfitData
                        {
                            [JsonProperty(PropertyName = "Enabled")]
                            public bool Enabled { get; set; } = false;

                            [JsonProperty(PropertyName = "Lock Outfit")]
                            public bool Locked { get; set; } = false;

                            [JsonProperty(PropertyName = "Settings")]
                            public AdminOutfitSettingsData Settings { get; set; } = new AdminOutfitSettingsData();
                            public class AdminOutfitSettingsData
                            {
                                [JsonProperty("(Shortnam::SkinID)")]
                                public string[] Outfit { get; set; } = new string[]
                                {
                                    "hoodie::1234567890",
                                    "pants::1234567890",
                                    "shoes.boots::1234567890"
                                };
                            }
                        }

                        [JsonProperty(PropertyName = "Notifications")]
                        public NotificationData Notification { get; set; } = new NotificationData();
                        public class NotificationData
                        {
                            [JsonProperty(PropertyName = "Global Chat")]
                            public NotificationGlobalChatData GlobalChat { get; set; } = new NotificationGlobalChatData();
                            public class NotificationGlobalChatData
                            {
                                [JsonProperty(PropertyName = "Enabled")]
                                public bool Enabled { get; set; } = false;
                                [JsonProperty(PropertyName = "Settings")]
                                public GlobalChatSettingsData Settings { get; set; } = new GlobalChatSettingsData();
                                public class GlobalChatSettingsData
                                {
                                    [JsonProperty(PropertyName = "Chat Icon (SteamID64)")]
                                    public ulong ChatIcon { get; set; } = 0;

                                    [JsonProperty(PropertyName = "Text Message Special Color (HEX) Example %message%")]
                                    public string MessageSpecialColor { get; set; } = SignatureColor;

                                    [JsonProperty(PropertyName = "Text Message")]
                                    public string Message { get; set; } = "%{player.name}% Activated %{mode.permission}% Mode";
                                }
                            }

                            [JsonProperty(PropertyName = "Self Chat")]
                            public NotificationSelfChatData SelfChat { get; set; } = new NotificationSelfChatData();
                            public class NotificationSelfChatData
                            {
                                [JsonProperty(PropertyName = "Enabled")]
                                public bool Enabled { get; set; } = true;
                                [JsonProperty(PropertyName = "Settings")]
                                public SelfChatSettingsData Settings { get; set; } = new SelfChatSettingsData();
                                public class SelfChatSettingsData
                                {
                                    [JsonProperty(PropertyName = "Chat Icon (SteamID64)")]
                                    public ulong ChatIcon { get; set; } = 0;

                                    [JsonProperty(PropertyName = "Text Message Special Color (HEX) Example %message%")]
                                    public string MessageSpecialColor { get; set; } = SignatureColor;

                                    [JsonProperty(PropertyName = "Text Message")]
                                    public string Message { get; set; } = "%You% Activated %{mode.permission}% Mode";
                                }
                            }

                            [JsonProperty(PropertyName = "Self Popup")]
                            public NotificationSelfPopupData SelfPopup { get; set; } = new NotificationSelfPopupData();
                            public class NotificationSelfPopupData
                            {
                                [JsonProperty(PropertyName = "Enabled")]
                                public bool Enabled { get; set; } = false;
                                [JsonProperty(PropertyName = "Settings")]
                                public SelfPopupSettingsData Settings { get; set; } = new SelfPopupSettingsData();
                                public class SelfPopupSettingsData
                                {
                                    [JsonProperty(PropertyName = "Text Message Special Color (HEX) Example %message%")]
                                    public string MessageSpecialColor { get; set; } = SignatureColor;

                                    [JsonProperty(PropertyName = "Text Message")]
                                    public string Message { get; set; } = "%You% Activated %{mode.permission}% Mode";
                                }
                            }

                            [JsonProperty(PropertyName = "Self Sound")]
                            public NotificationSelfSoundData SelfSound { get; set; } = new NotificationSelfSoundData();
                            public class NotificationSelfSoundData
                            {
                                [JsonProperty(PropertyName = "Enabled")]
                                public bool Enabled { get; set; } = false;
                                [JsonProperty(PropertyName = "Settings")]
                                public SelfSoundSettingsData Settings { get; set; } = new SelfSoundSettingsData();
                                public class SelfSoundSettingsData
                                {
                                    [JsonProperty(PropertyName = "Sound (Prefab)")]
                                    public string Sound { get; set; } = "assets/prefabs/misc/easter/painted eggs/effects/eggpickup.prefab";
                                }
                            }

                            [JsonProperty(PropertyName = "Discord")]
                            public NotificationDiscordData Discord { get; set; } = new NotificationDiscordData();
                            public class NotificationDiscordData
                            {
                                [JsonProperty(PropertyName = "Enabled")]
                                public bool Enabled { get; set; } = false;
                                [JsonProperty(PropertyName = "Settings")]
                                public DiscordSettingsData Settings { get; set; } = new DiscordSettingsData();
                                public class DiscordSettingsData
                                {
                                    [JsonProperty(PropertyName = "Webhook")]
                                    public string Webhook { get; set; } = "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks";

                                    [JsonProperty(PropertyName = "Embed Color")]
                                    public long WebhookColor { get; set; } = 3315400;
                                }
                            }
                        }

                        [JsonProperty(PropertyName = "Interface")]
                        public InterfaceData Interface { get; set; } = new InterfaceData();
                        public class InterfaceData
                        {
                            [JsonProperty(PropertyName = "Button")]
                            public ButtonData Button { get; set; } = new ButtonData();
                            public class ButtonData
                            {
                                [JsonProperty(PropertyName = "Enabled")]
                                public bool Enabled { get; set; } = false;

                                [JsonProperty(PropertyName = "Settings")]
                                public ButtonSettingsData Settings { get; set; } = new ButtonSettingsData();
                                public class ButtonSettingsData
                                {
                                    [JsonProperty(PropertyName = "Opacity (0.0 to 1.0)")]
                                    public double Opacity { get; set; } = 0.64;

                                    [JsonProperty(PropertyName = "Active Color (HEX)")]
                                    public string ActiveColor { get; set; } = "#008000";

                                    [JsonProperty(PropertyName = "Inactive Color (HEX)")]
                                    public string InactiveColor { get; set; } = "#800000";

                                    [JsonProperty(PropertyName = "Active Text")]
                                    public string ActiveText { get; set; } = "Activated";

                                    [JsonProperty(PropertyName = "Inactive Text")]
                                    public string InactiveText { get; set; } = "Disabled";

                                    [JsonProperty(PropertyName = "Active Text Color (HEX)")]
                                    public string ActiveTextColor { get; set; } = "#ffffff";

                                    [JsonProperty(PropertyName = "Inactive Text Color (HEX)")]
                                    public string InactiveTextColor { get; set; } = "#ffffff";
                                    
                                    [JsonProperty(PropertyName = "Design (Advanced)")]
                                    public ButtonDesignData Design { get; set; } = new ButtonDesignData();
                                    public class ButtonDesignData
                                    {
                                        [JsonProperty(PropertyName = "Anchor")]
                                        public ButtonDesignAnchorData Anchor { get; set; } = new ButtonDesignAnchorData();
                                        public class ButtonDesignAnchorData
                                        {
                                            [JsonProperty(PropertyName = "Min Width (0.0 - 1.0)")]
                                            public double Min_Width { get; set; } = 0.5;

                                            [JsonProperty(PropertyName = "Min Height (0.0 - 1.0)")]
                                            public double Min_Height { get; set; } = 0.0;

                                            [JsonProperty(PropertyName = "Max Width (0.0 - 1.0)")]
                                            public double Max_Width { get; set; } = 0.5;

                                            [JsonProperty(PropertyName = "Max Height (0.0 - 1.0)")]
                                            public double Max_Height { get; set; } = 0.0;
                                        
                                        }
                                        
                                        [JsonProperty(PropertyName = "Offset")]
                                        public ButtonDesignOffsetData Offset { get; set; } = new ButtonDesignOffsetData();
                                        public class ButtonDesignOffsetData
                                        {
                                            [JsonProperty(PropertyName = "Min Width (pixels)")]
                                            public int Min_Width { get; set; } = 0;

                                            [JsonProperty(PropertyName = "Min Height (pixels)")]
                                            public int Min_Height { get; set; } = 0;

                                            [JsonProperty(PropertyName = "Max Width (pixels)")]
                                            public int Max_Width { get; set; } = 60;

                                            [JsonProperty(PropertyName = "Max Height (pixels)")]
                                            public int Max_Height { get; set; } = 60;

                                            [JsonProperty(PropertyName = "Offset Points (Relative To Offset)")]
                                            public ButtonDesignOffsetPointsData Points { get; set; } = new ButtonDesignOffsetPointsData();
                                            public class ButtonDesignOffsetPointsData
                                            {
                                                [JsonProperty(PropertyName = "Top (pixels)")]
                                                public int Top { get; set; } = 0;

                                                [JsonProperty(PropertyName = "Bottom (pixels)")]
                                                public int Bottom { get; set; } = 18;

                                                [JsonProperty(PropertyName = "Left (pixels)")]
                                                public int Left { get; set; } = 0;

                                                [JsonProperty(PropertyName = "Right (pixels)")]
                                                public int Right { get; set; } = 263;
                                            }
                                        }
                                        
                                    }
                                }
                            }

                            [JsonProperty(PropertyName = "Panel")]
                            public PanelData Panel { get; set; } = new PanelData();
                            public class PanelData
                            {
                                [JsonProperty(PropertyName = "Enabled")]
                                public bool Enabled { get; set; } = false;

                                [JsonProperty(PropertyName = "Settings")]
                                public PanelSettingsData Settings { get; set; } = new PanelSettingsData();
                                public class PanelSettingsData
                                {
                                    [JsonProperty(PropertyName = "Text")]
                                    public string ActiveText { get; set; } = "A D M I N   M O D E      A C T I V A T E D";

                                    [JsonProperty(PropertyName = "Text Color (HEX)")]
                                    public string ActiveTextColor { get; set; } = "#ffffff";

                                    [JsonProperty(PropertyName = "Pulse Duration ")]
                                    public float PulseSpeed { get; set; } = 1.0f;

                                    [JsonProperty(PropertyName = "Design (Advanced)")]
                                    public PanelDesignData Design { get; set; } = new PanelDesignData();
                                    public class PanelDesignData
                                    {
                                        [JsonProperty(PropertyName = "Anchor")]
                                        public PanelDesignAnchorData Anchor { get; set; } = new PanelDesignAnchorData();
                                        public class PanelDesignAnchorData
                                        {
                                            [JsonProperty(PropertyName = "Min Width (0.0 - 1.0)")]
                                            public double Min_Width { get; set; } = 0.5;

                                            [JsonProperty(PropertyName = "Min Height (0.0 - 1.0)")]
                                            public double Min_Height { get; set; } = 0.0;

                                            [JsonProperty(PropertyName = "Max Width (0.0 - 1.0)")]
                                            public double Max_Width { get; set; } = 0.5;

                                            [JsonProperty(PropertyName = "Max Height (0.0 - 1.0)")]
                                            public double Max_Height { get; set; } = 0.0;
                                        }

                                        [JsonProperty(PropertyName = "Offset")]
                                        public PanelDesignOffsetData Offset { get; set; } = new PanelDesignOffsetData();
                                        public class PanelDesignOffsetData
                                        {
                                            [JsonProperty(PropertyName = "Min Width (pixels)")]
                                            public int Min_Width { get; set; } = 0;

                                            [JsonProperty(PropertyName = "Min Height (pixels)")]
                                            public int Min_Height { get; set; } = 0;

                                            [JsonProperty(PropertyName = "Max Width (pixels)")]
                                            public int Max_Width { get; set; } = 380;

                                            [JsonProperty(PropertyName = "Max Height (pixels)")]
                                            public int Max_Height { get; set; } = 20;

                                            [JsonProperty(PropertyName = "Offset Points (Relative To Offset)")]
                                            public PanelDesignOffsetPointsData Points { get; set; } = new PanelDesignOffsetPointsData();
                                            public class PanelDesignOffsetPointsData
                                            {
                                                [JsonProperty(PropertyName = "Top (pixels)")]
                                                public int Top { get; set; } = 0;

                                                [JsonProperty(PropertyName = "Bottom (pixels)")]
                                                public int Bottom { get; set; } = 82;

                                                [JsonProperty(PropertyName = "Left (pixels)")]
                                                public int Left { get; set; } = 0;

                                                [JsonProperty(PropertyName = "Right (pixels)")]
                                                public int Right { get; set; } = 200;
                                            }
                                        }
                                    }
                                }
                            }

                            [JsonProperty(PropertyName = "Menu")]
                            public MenuData Menu { get; set; } = new MenuData();
                            public class MenuData
                            {
                                [JsonProperty(PropertyName = "Enabled")]
                                public bool Enabled { get; set; } = false;

                                [JsonProperty(PropertyName = "Settings")]
                                public MenuSettingsData Settings { get; set; } = new MenuSettingsData();
                                public class MenuSettingsData
                                {
                                    [JsonProperty(PropertyName = "Opacity (0.0 to 1.0)")]
                                    public double Opacity { get; set; } = 0.64;

                                    [JsonProperty(PropertyName = "Not Found Color (HEX)")]
                                    public string NotFoundColor { get; set; } = "#000000";

                                    [JsonProperty(PropertyName = "Active Color (HEX)")]
                                    public string ActiveColor { get; set; } = "#008000";

                                    [JsonProperty(PropertyName = "Inactive Color (HEX)")]
                                    public string InactiveColor { get; set; } = "#800000";

                                    [JsonProperty(PropertyName = "Text Color (HEX)")]
                                    public string ActiveTextColor { get; set; } = "#ffffff";

                                    [JsonProperty(PropertyName = "Design (Advanced)")]
                                    public MenuDesignData Design { get; set; } = new MenuDesignData();
                                    public class MenuDesignData
                                    {
                                        [JsonProperty(PropertyName = "Anchor")]
                                        public MenuDesignAnchorData Anchor { get; set; } = new MenuDesignAnchorData();
                                        public class MenuDesignAnchorData
                                        {
                                            [JsonProperty(PropertyName = "Min Width (0.0 - 1.0)")]
                                            public double Min_Width { get; set; } = 1.0;

                                            [JsonProperty(PropertyName = "Min Height (0.0 - 1.0)")]
                                            public double Min_Height { get; set; } = 0.0;

                                            [JsonProperty(PropertyName = "Max Width (0.0 - 1.0)")]
                                            public double Max_Width { get; set; } = 1.0;

                                            [JsonProperty(PropertyName = "Max Height (0.0 - 1.0)")]
                                            public double Max_Height { get; set; } = 0.0;
                                        }

                                        [JsonProperty(PropertyName = "Offset")]
                                        public MenuDesignOffsetData Offset { get; set; } = new MenuDesignOffsetData();
                                        public class MenuDesignOffsetData
                                        {
                                            [JsonProperty(PropertyName = "Min Width (pixels)")]
                                            public int Min_Width { get; set; } = 0;

                                            [JsonProperty(PropertyName = "Min Height (pixels)")]
                                            public int Min_Height { get; set; } = 0;

                                            [JsonProperty(PropertyName = "Max Width (pixels)")]
                                            public int Max_Width { get; set; } = 180;

                                            [JsonProperty(PropertyName = "Max Height (pixels)")]
                                            public int Max_Height { get; set; } = 81;

                                            [JsonProperty(PropertyName = "Offset Points (Relative To Offset)")]
                                            public MenuDesignOffsetPointsData Points { get; set; } = new MenuDesignOffsetPointsData();
                                            public class MenuDesignOffsetPointsData
                                            {
                                                [JsonProperty(PropertyName = "Top (pixels)")]
                                                public int Top { get; set; } = 0;

                                                [JsonProperty(PropertyName = "Bottom (pixels)")]
                                                public int Bottom { get; set; } = 16;

                                                [JsonProperty(PropertyName = "Left (pixels)")]
                                                public int Left { get; set; } = 0;

                                                [JsonProperty(PropertyName = "Right (pixels)")]
                                                public int Right { get; set; } = 392;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        [JsonProperty(PropertyName = "Actions")]
                        public ActionsData Actions { get; set; } = new ActionsData();
                        public class ActionsData
                        {
                            [JsonProperty(PropertyName = "Allow All Actions (Overrides Specfic Actions)")]
                            public bool AllowAll { get; set; } = false;

                            [JsonProperty(PropertyName = "Allow Specfic Actions")]
                            public AllowSpecficActionsData Specfic { get; set; } = new AllowSpecficActionsData();
                            public class AllowSpecficActionsData
                            {
                                [JsonProperty(PropertyName = "Can Build")]
                                public bool CanBuild { get; set; } = false;

                                [JsonProperty(PropertyName = "Can Craft")]
                                public bool CanCraft { get; set; } = false;

                                [JsonProperty(PropertyName = "Can Loot Players")]
                                public bool CanLootPlayer { get; set; } = false;

                                [JsonProperty(PropertyName = "Can Loot Entities")]
                                public bool CanLootEntity { get; set; } = false;

                                [JsonProperty(PropertyName = "Can Damage Structures")]
                                public bool CanDamageStructures { get; set; } = false;

                                [JsonProperty(PropertyName = "Can Interact With Items In Weapons Category")]
                                public bool CanUseWeapons { get; set; } = false;

                                [JsonProperty(PropertyName = "Can Hurt Players")]
                                public bool CanHurtPlayers { get; set; } = false;

                                [JsonProperty(PropertyName = "Can Drop Items")]
                                public bool CanDropItems { get; set; } = false;
                            }
                        }

                        [JsonProperty(PropertyName = "Third-Party Plugins")]
                        public PluginsData Plugins { get; set; } = new PluginsData();
                        public class PluginsData
                        {
                            [JsonProperty(PropertyName = "Autorun Plugins")]
                            public PluginsDataAutorun Autorun { get; set; } = new PluginsDataAutorun();
                            public class PluginsDataAutorun
                            {
                                [JsonProperty(PropertyName = "Autorun All")]
                                public bool All { get; set; } = false;

                                [JsonProperty(PropertyName = "Autorun Specfic")]
                                public PluginsDataAutorunSpecfic Specfic { get; set; } = new PluginsDataAutorunSpecfic();
                                public class PluginsDataAutorunSpecfic
                                {
                                    [JsonProperty(PropertyName = "AdminRadar")]
                                    public bool AdminRadar { get; set; } = false;

                                    [JsonProperty(PropertyName = "Vanish")]
                                    public bool Vanish { get; set; } = false;

                                    [JsonProperty(PropertyName = "Godmode")]
                                    public bool Godmode { get; set; } = false;
                                }
                            }

                            [JsonProperty(PropertyName = "Blocked Plugins")]
                            public PluginsDataBlocked Blocked { get; set; } = new PluginsDataBlocked();
                            public class PluginsDataBlocked
                            {
                                [JsonProperty(PropertyName = "Block All")]
                                public bool All { get; set; } = false;

                                [JsonProperty(PropertyName = "Block Specfic")]
                                public PluginsDataBlockedSpecfic Specfic { get; set; } = new PluginsDataBlockedSpecfic();
                                public class PluginsDataBlockedSpecfic
                                {
                                    [JsonProperty(PropertyName = "Backpacks")]
                                    public bool Backpacks { get; set; } = false;

                                    [JsonProperty(PropertyName = "AdminRadar")]
                                    public bool AdminRadar { get; set; } = false;

                                    [JsonProperty(PropertyName = "Vanish")]
                                    public bool Vanish { get; set; } = false;

                                    [JsonProperty(PropertyName = "Godmode")]
                                    public bool Godmode { get; set; } = false;
                                }
                            }
                        }
                    }

                    /*========================================*/

                    [JsonProperty(PropertyName = "On Player")]
                    public OnPlayerData OnPlayer { get; set; } = new OnPlayerData();
                    public class OnPlayerData
                    {
                        [JsonProperty(PropertyName = "Autorun Commands Use Forward Slash '/' For Chat-Commands & Leave It Blank For Console-Commands")]
                        public string[] AutoRunCommands { get; set; } = { };

                        [JsonProperty(PropertyName = "Toggle Groups To Revoke (Leave blank to disable)")]
                        public string[] ToggleGroups { get; set; } = new string[] { };

                        [JsonProperty(PropertyName = "Notifications")]
                        public NotificationData Notification { get; set; } = new NotificationData();
                        public class NotificationData
                        {
                            [JsonProperty(PropertyName = "Global Chat")]
                            public NotificationGlobalChatData GlobalChat { get; set; } = new NotificationGlobalChatData();
                            public class NotificationGlobalChatData
                            {
                                [JsonProperty(PropertyName = "Enabled")]
                                public bool Enabled { get; set; } = false;
                                [JsonProperty(PropertyName = "Settings")]
                                public GlobalChatSettingsData Settings { get; set; } = new GlobalChatSettingsData();
                                public class GlobalChatSettingsData
                                {
                                    [JsonProperty(PropertyName = "Chat Icon (SteamID64)")]
                                    public ulong ChatIcon { get; set; } = 0;

                                    [JsonProperty(PropertyName = "Text Message Special Color (HEX) Example %message%")]
                                    public string MessageSpecialColor { get; set; } = SignatureColor;

                                    [JsonProperty(PropertyName = "Text Message")]
                                    public string Message { get; set; } = "%{player.name}% Returned To %Player% Mode";

                                }
                            }

                            [JsonProperty(PropertyName = "Self Chat")]
                            public NotificationSelfChatData SelfChat { get; set; } = new NotificationSelfChatData();
                            public class NotificationSelfChatData
                            {
                                [JsonProperty(PropertyName = "Enabled")]
                                public bool Enabled { get; set; } = true;
                                [JsonProperty(PropertyName = "Settings")]
                                public SelfChatSettingsData Settings { get; set; } = new SelfChatSettingsData();
                                public class SelfChatSettingsData
                                {
                                    [JsonProperty(PropertyName = "Chat Icon (SteamID64)")]
                                    public ulong ChatIcon { get; set; } = 0;

                                    [JsonProperty(PropertyName = "Text Message Special Color (HEX) Example %message%")]
                                    public string MessageSpecialColor { get; set; } = SignatureColor;

                                    [JsonProperty(PropertyName = "Text Message")]
                                    public string Message { get; set; } = "%You% Returned To %Player% Mode";
                                }
                            }

                            [JsonProperty(PropertyName = "Self Popup")]
                            public NotificationSelfPopupData SelfPopup { get; set; } = new NotificationSelfPopupData();
                            public class NotificationSelfPopupData
                            {
                                [JsonProperty(PropertyName = "Enabled")]
                                public bool Enabled { get; set; } = false;
                                [JsonProperty(PropertyName = "Settings")]
                                public SelfPopupSettingsData Settings { get; set; } = new SelfPopupSettingsData();
                                public class SelfPopupSettingsData
                                {
                                    [JsonProperty(PropertyName = "Text Message Special Color (HEX) Example %message%")]
                                    public string MessageSpecialColor { get; set; } = SignatureColor;

                                    [JsonProperty(PropertyName = "Text Message")]
                                    public string Message { get; set; } = "%You% Returned To %Player% Mode";
                                }
                            }

                            [JsonProperty(PropertyName = "Self Sound")]
                            public NotificationSelfSoundData SelfSound { get; set; } = new NotificationSelfSoundData();
                            public class NotificationSelfSoundData
                            {
                                [JsonProperty(PropertyName = "Enabled")]
                                public bool Enabled { get; set; } = false;
                                [JsonProperty(PropertyName = "Settings")]
                                public SelfSoundSettingsData Settings { get; set; } = new SelfSoundSettingsData();
                                public class SelfSoundSettingsData
                                {
                                    [JsonProperty(PropertyName = "Sound (Prefab)")]
                                    public string Sound { get; set; } = "assets/prefabs/misc/easter/painted eggs/effects/eggpickup.prefab";
                                }
                            }

                            [JsonProperty(PropertyName = "Discord")]
                            public NotificationDiscordData Discord { get; set; } = new NotificationDiscordData();
                            public class NotificationDiscordData
                            {
                                [JsonProperty(PropertyName = "Enabled")]
                                public bool Enabled { get; set; } = false;
                                [JsonProperty(PropertyName = "Settings")]
                                public DiscordSettingsData Settings { get; set; } = new DiscordSettingsData();
                                public class DiscordSettingsData
                                {
                                    [JsonProperty(PropertyName = "Webhook")]
                                    public string Webhook { get; set; } = "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks";

                                    [JsonProperty(PropertyName = "Embed Color")]
                                    public long WebhookColor { get; set; } = 3315400;
                                }
                            }
                        }

                        [JsonProperty(PropertyName = "Blocked Commands")]
                        public string[] BlockedCommands { get; set; } = new string[]
                        {
                            "god",
                            "vanish",
                            "freeze",
                            "viewinv",
                            "inspect",
                            "padmin",
                            "playeradministration.show",
                            "spectate",
                        };


                        [JsonProperty(PropertyName = "Third-Party Plugins")]
                        public PluginsData Plugins { get; set; } = new PluginsData();
                        public class PluginsData
                        {
                            [JsonProperty(PropertyName = "Blocked Plugins")]
                            public PluginsDataBlocked Blocked { get; set; } = new PluginsDataBlocked();
                            public class PluginsDataBlocked
                            {
                                [JsonProperty(PropertyName = "Block All")]
                                public bool All { get; set; } = true;

                                [JsonProperty(PropertyName = "Block Specfic")]
                                public PluginsDataBlockedSpecfic Specfic { get; set; } = new PluginsDataBlockedSpecfic();
                                public class PluginsDataBlockedSpecfic
                                {
                                    [JsonProperty(PropertyName = "AdminRadar")]
                                    public bool AdminRadar { get; set; } = false;

                                    [JsonProperty(PropertyName = "Vanish")]
                                    public bool Vanish { get; set; } = false;

                                    [JsonProperty(PropertyName = "Godmode")]
                                    public bool Godmode { get; set; } = false;
                                }
                            }
                        }
                    }
                }
            }
        }
        #endregion Configuration
        #region Configuration Init
        protected override void LoadConfig()
        {
            base.LoadConfig();

            DebugLog(DebugType.Method);

            try
            {
                _configuration = Config.ReadObject<ConfigData>();
                if (_configuration == null) { LoadDefaultConfig(); }
            }
            catch (Exception ex)
            {
                PrintError($"Configuration is corrupted. Resetting to default values\n{ex}");
                LoadDefaultConfig();
            }
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            DebugLog(DebugType.Method);

            PrintWarning("Creating a new configuration file");
            _configuration = new ConfigData();
        }
        protected override void SaveConfig()
        {
            DebugLog(DebugType.Method);

            Config.WriteObject(_configuration);
        }
        #endregion Configuration Init

        #region Init Hooks
        private void Loaded()
        {
            DebugLog(DebugType.Method);

            Instance = this;
            Initialization.Load.Permission();
            Initialization.Load.Commands();

            var admins = LevelHelper.GetAdmins();
            if (admins != null)
            {
                foreach (var admin in admins)
                {
                    var Mode = LevelHelper.Get(admin);
                    if (Mode != null)
                    {
                        if (Mode.SETTINGS.OnAdmin.Interface.Button.Enabled && !Mode.SETTINGS.OnAdmin.RequireReason)
                        {
                            UI.Draw.Button(admin);
                        }
                        if (LevelHelper.IsAdmin(admin) && Mode.SETTINGS.OnAdmin.Interface.Panel.Enabled)
                        {
                            UI.Draw.PulsePanel.Start(admin);
                        }
                        if (LevelHelper.IsAdmin(admin) && Mode.SETTINGS.OnAdmin.Interface.Menu.Enabled)
                        {
                            UI.Draw.NavMenu(admin);
                        }
                    }
                }
            }
        }
        private void Unload()
        {
            DebugLog(DebugType.Method);

            var admins = LevelHelper.GetAdmins();
            if (admins != null)
            {
                foreach (var admin in admins)
                {
                    var Mode = LevelHelper.Get(admin);
                    if(Mode.SETTINGS.OnAdmin.RevertOnTrigger) { CoreLevel.HardUnload(admin, true, true); }
                }
            }

            try { CoreLevel.AuthKeys.Clear(); } catch { }
            try { CoreLevel.ForcedMode.Clear(); } catch { }
            try { UI.Draw.PulsePanel.PanelTimers.Clear(); } catch { }
            try { UICommands.Clear(); } catch { }
            Initialization.Unload.Commands();
        }
        #endregion Init Hooks
        #region Initialization Helpers
        private class Initialization
        {
            internal class Load
            {
                internal static void Permission()
                {
                    Instance?.DebugLog(DebugType.Method);

                    var permissions = LevelHelper.Permission.GetAll();
                    if (permissions != null)
                    {
                        foreach (var permission in permissions)
                        {
                            Instance?.permission?.RegisterPermission($"{Instance.Name}.{permission}", Instance);
                        }
                    }
                }
                internal static void Commands()
                {
                    Instance?.DebugLog(DebugType.Method);

                    var chatCommands = LevelHelper.Commands.GetAll();
                    if (chatCommands != null)
                    {
                        foreach (var chatCommand in chatCommands)
                        {
                            Instance?.cmd?.AddChatCommand(chatCommand, Instance, CoreLevel.Toggle);
                        }
                    }
                }
            }
            internal class Unload
            {
                internal static void Commands()
                {
                    Instance?.DebugLog(DebugType.Method);

                    var chatCommands = LevelHelper.Commands.GetAll();
                    if (chatCommands != null)
                    {
                        foreach (var chatCommand in chatCommands)
                        {
                            Instance?.cmd?.RemoveChatCommand(chatCommand, Instance);
                        }
                    }
                }
            }
        }
        #endregion Initialization Helpers

        #region Level Functions
        private class CoreLevel
        {
            //Keep track of times toggled & lastToggle time
            internal static Dictionary<ulong, ToggleCounter> AuthKeys = new Dictionary<ulong, ToggleCounter>();
            internal class ToggleCounter
            {
                internal int Admin { get; set; }
                internal int Player { get; set; }
                internal DateTime LastToggle { get; set; }
            }

            //Forced mode override
            internal static Dictionary<ulong, ConfigData.Modes> ForcedMode = new Dictionary<ulong, ConfigData.Modes>();

            internal static void HardUnload(BasePlayer player, bool CheckIsAdmin = false, bool OnUnload = false, ConfigData.Modes OnRevokeMode = null)
            {
                Instance?.DebugLog(DebugType.Method);

                if (player != null && LevelHelper.IsAdmin(player))
                {
                    if (CheckIsAdmin)
                    {
                        var Mode = LevelHelper.Get(player);
                        if (Mode == null) { return; }
                    }

                    CoreLevel.Set(player, null, null, XLIB.PLAYER.AuthLevel.LevelType.Player, null, null, OnUnload, OnRevokeMode);
                    UI.Destroy(player);

                    /*Credit: UIChamp*/
                    ServerMgr.Instance.Invoke(() =>
                    {
                        XLIB.PLAYER.Location.Storage.RemoveKey(player.userID, $"{Instance.Name}.location");
                        XLIB.PLAYER.Inventory.Storage.RemoveKey(player.userID, $"{Instance.Name}.inventory");
                        XLIB.STEAM.WebData.RemoveKey(player.userID);
                    }, 5.0f);


                    try { if (CoreLevel.AuthKeys.ContainsKey(player.userID)) { CoreLevel.AuthKeys.Remove(player.userID); } } catch { }
                    try { if (CoreLevel.ForcedMode.ContainsKey(player.userID)) { CoreLevel.ForcedMode.Remove(player.userID); } } catch { }
                    try { if (UI.Draw.PulsePanel.PanelTimers.ContainsKey(player.userID)) { UI.Draw.PulsePanel.PanelTimers.Remove(player.userID); } } catch { }
                    try { if (UICommands.ContainsKey(player.userID)) { UICommands.Remove(player.userID); } } catch { }
                }
            }

            internal static void Set(BasePlayer player, string command, string reason = null, XLIB.PLAYER.AuthLevel.LevelType authLevel = XLIB.PLAYER.AuthLevel.LevelType.Admin, ConfigData.Modes targetMode = null, BasePlayer targetPlayer = null, bool QuickUnload = false, ConfigData.Modes OnRevokeMode = null)
            {
                bool IsRevoke = (OnRevokeMode != null);

                XLIB.PLAYER.Location.Utility.UnMountChair(player);

                Instance?.DebugLog(DebugType.Method);

                //Set Mode for Target
                if (!IsRevoke && targetMode != null)
                {
                    if (!ForcedMode.ContainsKey(player.userID)) { ForcedMode.Add(player.userID, targetMode); }
                    ForcedMode[player.userID] = targetMode;
                }



                var Mode = LevelHelper.Get(player);
                var TrueMode = LevelHelper.Get(player, true);
                if (!IsRevoke && Mode == null)
                {
                    string data = FormatData(new FormattedData
                    {
                        TargetMode = Mode,
                    }, LangMessages.NoPerm);
                    string message = ColorSpecialText(data, SignatureColor);
                    XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                    return;
                }



                
                if (!IsRevoke && !QuickUnload)
                {
                    //Get Mode by commands
                    var commandMode = _configuration.MODES.Where(x => x.Commands.Contains(command)).OrderByDescending(i => i.Priority).FirstOrDefault();

                    if (commandMode != null && !LevelHelper.GetModes(player).Any(x => x.Priority == commandMode.Priority))
                    {
                        string data = FormatData(new FormattedData
                        {
                            TargetMode = commandMode,
                        }, LangMessages.NoPermTargetMode);
                        string message = ColorSpecialText(data, SignatureColor);
                        XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                        return;
                    }

                    if (commandMode != null && !LevelHelper.GetModes(player).Any(x => x.Commands == commandMode.Commands) && TrueMode.IsMaster)
                    {
                        Mode = commandMode;
                    }

                    //Toggle Lower Mode
                    if (commandMode != null && (Mode.Commands != commandMode.Commands) && commandMode.Priority < TrueMode.Priority)
                    {
                        Mode = commandMode;
                    }
                }
 


                if (!IsRevoke && !QuickUnload)
                {
                    if (!AuthKeys.ContainsKey(player.userID)) { AuthKeys.Add(player.userID, new ToggleCounter()); }
                    //Cooldown
                    if (DateTime.Now < AuthKeys[player.userID].LastToggle.AddSeconds(Convert.ToDouble(CooldownDelay)))
                    {
                        string data = FormatData(new FormattedData
                        {
                        }, LangMessages.PleaseWait);
                        string message = ColorSpecialText(data, SignatureColor);
                        XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                        return;
                    }
                    AuthKeys[player.userID].LastToggle = DateTime.Now;
                }

                if (!IsRevoke && !QuickUnload)
                {
                    //Toggle Fixer
                    if (AuthKeys[player.userID].Admin > 1 || AuthKeys[player.userID].Player > 1)
                    {
                        XLIB.PLAYER.AuthLevel.Set(player, XLIB.PLAYER.AuthLevel.LevelType.Player);
                        CoreLevel.AuthKeys[player.userID].Admin++; CoreLevel.AuthKeys[player.userID].Admin = 0;
                        CoreLevel.AuthKeys[player.userID].Admin++; CoreLevel.AuthKeys[player.userID].Player = 0;
                        return;
                    }
                    if ((int)authLevel >= (int)XLIB.PLAYER.AuthLevel.LevelType.Moderator)
                    { CoreLevel.AuthKeys[player.userID].Player++; CoreLevel.AuthKeys[player.userID].Admin = 0; }

                    if (authLevel == (int)XLIB.PLAYER.AuthLevel.LevelType.Player)
                    { CoreLevel.AuthKeys[player.userID].Admin++; CoreLevel.AuthKeys[player.userID].Player = 0; }
                }

                #region On Toggle
                /*OnAdmin Minuim 1 -> Moderator*/
                if ((int)authLevel >= (int)XLIB.PLAYER.AuthLevel.LevelType.Moderator)
                {
                    player.PauseFlyHackDetection(float.MaxValue);

                    if (!LevelHelper.IsReady(player)) { return; }


                    //Reason
                    if (Mode.SETTINGS.OnAdmin.RequireReason && string.IsNullOrEmpty(reason))
                    {
                        string data = FormatData(new FormattedData
                            {
                                TargetMode = Mode,
                            }, LangMessages.NoReason);
                        string message = ColorSpecialText(data, SignatureColor);
                        XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                        return;
                    }

                    //Restricted Ids
                    if (Mode.SpecficIds.Length > 0)
                    {
                        string[] stringIds = Array.ConvertAll(Mode.SpecficIds, i => i.ToString());
                        if (stringIds.All(x => x.All(Char.IsDigit)))
                        {
                            ulong[] ulongsIds = Array.ConvertAll(stringIds, i => Convert.ToUInt64(i));
                            if (!ulongsIds.Contains(player.userID))
                            {
                                string data = FormatData(new FormattedData
                                    {
                                        TargetMode = Mode,
                                    }, LangMessages.RestrictedMode);
                                string message = ColorSpecialText(data, SignatureColor);
                                XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                                return;
                            }
                        }
                        else
                        {
                            string data = FormatData(new FormattedData
                            {
                                TargetMode = Mode,
                            }, LangMessages.RestrictedModeFailed);
                            string message = ColorSpecialText(data, SignatureColor);
                            XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                            return;
                        }
                    }

                    /*Switching State*/
                    LevelHelper.SetSwitching(player.userID, XLIB.PLAYER.AuthLevel.LevelType.Admin);

                    XLIB.PLAYER.Location.Utility.MountChair(player, true);

                    XLIB.PLAYER.AuthLevel.Set(player, XLIB.PLAYER.AuthLevel.Get(Mode.SETTINGS.OnAdmin.AuthLevel));

                    //Toggle Group
                    if (Mode.SETTINGS.OnAdmin.ToggleGroups != null && Mode.SETTINGS.OnAdmin.ToggleGroups.Length > 0)
                    {
                        foreach (var group in Mode.SETTINGS.OnAdmin.ToggleGroups)
                        {
                            Instance.permission.AddUserGroup(player.UserIDString, group);
                        }
                    }

                    //Separate Inventories
                    if (Mode.SETTINGS.OnAdmin.SeparateInventories) { XLIB.PLAYER.Inventory.Storage.Save(player, $"{Instance.Name}.inventory", true); }

                    //Teleport
                    if (Mode.SETTINGS.OnAdmin.TeleportBack) { XLIB.PLAYER.Location.Storage.Set(player, $"{Instance.Name}.location"); }

                    //Notifications
                    if (Mode.SETTINGS.OnAdmin.Notification.GlobalChat.Enabled)
                    {
                        string data = FormatData(new FormattedData
                        {
                            TargetMode = Mode,
                            Player = player
                        }, Mode.SETTINGS.OnAdmin.Notification.GlobalChat.Settings.Message);
                        string message = ColorSpecialText(data, Mode.SETTINGS.OnAdmin.Notification.GlobalChat.Settings.MessageSpecialColor);
                        XLIB.NOTIFICATION.Server.Global.Send(FormatMessage(message), Mode.SETTINGS.OnAdmin.Notification.GlobalChat.Settings.ChatIcon);
                    }
                    if (Mode.SETTINGS.OnAdmin.Notification.SelfChat.Enabled)
                    {
                        string data = FormatData(new FormattedData
                        {
                            TargetMode = Mode,
                        }, Mode.SETTINGS.OnAdmin.Notification.SelfChat.Settings.Message);
                        string message = ColorSpecialText(data, Mode.SETTINGS.OnAdmin.Notification.SelfChat.Settings.MessageSpecialColor);
                        XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message), Mode.SETTINGS.OnAdmin.Notification.SelfChat.Settings.ChatIcon);
                    }
                    if (Mode.SETTINGS.OnAdmin.Notification.SelfPopup.Enabled)
                    {
                        string data = FormatData(new FormattedData
                        {
                            TargetMode = Mode,
                        }, Mode.SETTINGS.OnAdmin.Notification.SelfPopup.Settings.Message);
                        string message = ColorSpecialText(data, Mode.SETTINGS.OnAdmin.Notification.SelfPopup.Settings.MessageSpecialColor);
                        XLIB.NOTIFICATION.Server.Self.SendPopup(player, XLIB.DATA.String.TextEncodeing(16, "#ffffff", $"{message} "), 4000);
                    }
                    if (Mode.SETTINGS.OnAdmin.Notification.SelfSound.Enabled)
                    {
                        XLIB.NOTIFICATION.Server.Self.SendEffect(player, Mode.SETTINGS.OnAdmin.Notification.SelfSound.Settings.Sound);
                    }

                    //Backpack
                    Instance.LockBackpack(player);

                    //Admin Outfit
                    AdminOutfit.Set(player);

                    //UI
                    if (Mode.SETTINGS.OnAdmin.Interface.Button.Enabled && !Mode.SETTINGS.OnAdmin.RequireReason)
                    {
                        UI.Draw.Button(player);
                    }
                    if (Mode.SETTINGS.OnAdmin.Interface.Panel.Enabled)
                    {
                        UI.Draw.PulsePanel.Start(player);
                    }
                    if (Mode.SETTINGS.OnAdmin.Interface.Menu.Enabled)
                    {
                        UI.Draw.NavMenu(player);
                    }

                    /*Credit: UIChamp*/
                    ServerMgr.Instance.Invoke(() =>
                    {
                        //AutoRun Commands
                        RunCommands(player, Mode.SETTINGS.OnAdmin.AutoRunCommands);

                        //Toggle Plugins
                        TogglePlugin(player, Instance.Vanish, PluginState.On);
                        TogglePlugin(player, Instance.AdminRadar, PluginState.On);
                        TogglePlugin(player, Instance.Godmode, PluginState.On);

                        XLIB.PLAYER.Location.Utility.UnMountChair(player);

                        AuthKeys[player.userID].LastToggle = DateTime.Now;

                        RenamePlayer(player, Mode.SETTINGS.OnAdmin.Name);

                        /*Switching State*/
                        LevelHelper.SetSwitching(player.userID, XLIB.PLAYER.AuthLevel.LevelType.Admin, true);

                        /*Call Custom Hook*/
                        Interface.CallHook(API_onAdmin, player);
                    }, 0.1f);
                }


                /*OnPlayer*/
                if (authLevel == XLIB.PLAYER.AuthLevel.LevelType.Player)
                {
                    player.PauseFlyHackDetection(float.MaxValue);

                    //Check For (Flying Dead Etc)
                    LevelHelper.IsReady(player, true);

                    /*Switching State*/
                    LevelHelper.SetSwitching(player.userID, XLIB.PLAYER.AuthLevel.LevelType.Player);

                    var SetMode = IsRevoke ? OnRevokeMode : Mode;

                    //Teleport
                    var TeleportKey = XLIB.PLAYER.Location.Storage.GetKey(player.userID, $"{Instance.Name}.location");
                    if (!IsRevoke && Mode.SETTINGS.OnAdmin.TeleportBack && TeleportKey != Vector3.zero)
                    {
                        XLIB.PLAYER.Location.Storage.Teleport(player, $"{Instance.Name}.location", true);
                    }
                    else { XLIB.PLAYER.Location.Utility.Teleport(player, new Vector3(player.transform.position.x, TerrainMeta.HeightMap.GetHeight(player.transform.position), player.transform.position.z)); }


                    /*Credit: UIChamp*/
                    ServerMgr.Instance.Invoke(() =>
                    {
                        //Teleport again #Hotfix if player flying & teleport in air
                        if (!IsRevoke && Mode.SETTINGS.OnAdmin.TeleportBack && TeleportKey != Vector3.zero)
                        {
                            XLIB.PLAYER.Location.Storage.Teleport(player, $"{Instance.Name}.location", true);
                        }
                        else { XLIB.PLAYER.Location.Utility.Teleport(player, new Vector3(player.transform.position.x, TerrainMeta.HeightMap.GetHeight(player.transform.position), player.transform.position.z)); }
                        XLIB.PLAYER.Location.Utility.MountChair(player, true);
                    }, 0.01f);



                    //AutoRun Commands
                    RunCommands(player, SetMode.SETTINGS.OnPlayer.AutoRunCommands);

                    //AutoRun Plugins
                    TogglePlugin(player, Instance.Vanish, PluginState.Off, IsRevoke);
                    TogglePlugin(player, Instance.AdminRadar, PluginState.Off, IsRevoke);
                    TogglePlugin(player, Instance.Godmode, PluginState.Off, IsRevoke);

                    /*Credit: UIChamp*/
                    ServerMgr.Instance.Invoke(() =>
                    {
                        
                        if (!QuickUnload && !IsRevoke) { XLIB.PLAYER.Location.Utility.MountChair(player, true); }

                        if (!IsRevoke && !QuickUnload && !LevelHelper.IsReady(player))
                        {
                            
                            XLIB.PLAYER.Location.Utility.UnMountChair(player);

                            string data = FormatData(new FormattedData
                            {
                            }, LangMessages.StopFlying);
                            string message = ColorSpecialText(data, SignatureColor);
                            XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                            return;
                        }

                        //Toggle Group
                        if (!IsRevoke && Mode.SETTINGS.OnPlayer.ToggleGroups != null && Mode.SETTINGS.OnPlayer.ToggleGroups.Length > 0)
                        {
                            foreach (var group in Mode.SETTINGS.OnPlayer.ToggleGroups)
                            {
                                Instance.permission.RemoveUserGroup(player.UserIDString, group);
                            }
                        }

                        /*Revert Outfit*/
                        if (SetMode.SETTINGS.OnAdmin.AdminOutfit.Locked)
                        {
                            XLIB.PLAYER.Inventory.Utility.DestroyItems(player.inventory.containerWear.itemList.ToArray());
                            XLIB.PLAYER.Inventory.Utility.UnlockContainer(player.inventory.containerWear);
                        }

                        //Separate Inventories
                        if (SetMode.SETTINGS.OnAdmin.SeparateInventories) { XLIB.PLAYER.Inventory.Storage.Restore(player, $"{Instance.Name}.inventory", true, true); }

                        //Backpack
                        Instance.UnLockBackpack(player);

                        XLIB.PLAYER.AuthLevel.Set(player, XLIB.PLAYER.AuthLevel.LevelType.Player);
                        //UI
                        if (!IsRevoke && Mode.SETTINGS.OnAdmin.Interface.Button.Enabled && !Mode.SETTINGS.OnAdmin.RequireReason)
                        {
                            UI.Draw.Button(player);
                        }
                        if (SetMode.SETTINGS.OnAdmin.Interface.Panel.Enabled)
                        {
                            UI.Draw.PulsePanel.Stop(player);
                            UI.Destroy(player, UI.UIElement.Panel);
                        }
                        if (SetMode.SETTINGS.OnAdmin.Interface.Menu.Enabled)
                        {
                            UI.Destroy(player, UI.UIElement.Menu);
                        }

                        if (!IsRevoke && !QuickUnload)
                        {
                            //Notifications
                            if (Mode.SETTINGS.OnPlayer.Notification.GlobalChat.Enabled)
                            {
                                string data = FormatData(new FormattedData
                                {
                                    Player = player,
                                }, Mode.SETTINGS.OnPlayer.Notification.GlobalChat.Settings.Message);
                                string message = ColorSpecialText(data, Mode.SETTINGS.OnPlayer.Notification.GlobalChat.Settings.MessageSpecialColor);
                                XLIB.NOTIFICATION.Server.Global.Send(FormatMessage(message), Mode.SETTINGS.OnPlayer.Notification.GlobalChat.Settings.ChatIcon);
                            }
                            if (Mode.SETTINGS.OnPlayer.Notification.SelfChat.Enabled)
                            {
                                string data = FormatData(new FormattedData
                                {
                                }, Mode.SETTINGS.OnPlayer.Notification.SelfChat.Settings.Message);
                                string message = ColorSpecialText(data, Mode.SETTINGS.OnPlayer.Notification.SelfChat.Settings.MessageSpecialColor);
                                XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message), Mode.SETTINGS.OnPlayer.Notification.SelfChat.Settings.ChatIcon);
                            }
                            if (Mode.SETTINGS.OnPlayer.Notification.SelfPopup.Enabled)
                            {
                                string data = FormatData(new FormattedData
                                {
                                }, Mode.SETTINGS.OnPlayer.Notification.SelfPopup.Settings.Message);
                                string message = ColorSpecialText(data, Mode.SETTINGS.OnPlayer.Notification.SelfPopup.Settings.MessageSpecialColor);
                                XLIB.NOTIFICATION.Server.Self.SendPopup(player, XLIB.DATA.String.TextEncodeing(16, "#ffffff", $"{message} "), 4000);
                            }
                            if (Mode.SETTINGS.OnPlayer.Notification.SelfSound.Enabled)
                            {
                                XLIB.NOTIFICATION.Server.Self.SendEffect(player, Mode.SETTINGS.OnPlayer.Notification.SelfSound.Settings.Sound);
                            }
                        }

                        
                        XLIB.PLAYER.Location.Utility.UnMountChair(player);


                        if (!IsRevoke && !QuickUnload && AuthKeys.ContainsKey(player.userID))
                        {
                            AuthKeys[player.userID].LastToggle = DateTime.Now;
                        }

                        RenamePlayer(player, null, true);

                        /*Switching State*/
                        LevelHelper.SetSwitching(player.userID, XLIB.PLAYER.AuthLevel.LevelType.Player, true);

                        /*Call Custom Hook*/
                        Interface.CallHook(API_onPlayer, player);
                    }, 0.5f);
                }

                SendDiscordEmbed(authLevel, reason, player, targetPlayer);
                #endregion On Toggle
            }

            internal static void Toggle(BasePlayer player, string command, string[] args)
            {
                Instance?.DebugLog(DebugType.Method);



                var TrueMode = LevelHelper.Get(player, true);
                //Has NO admin mode at all
                if (TrueMode == null)
                {
                    string data = FormatData(new FormattedData
                    {

                    }, LangMessages.NoPermGenetic);
                    string message = ColorSpecialText(data, SignatureColor);
                    XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                    return;
                }

         
                /*Toggle Mode For Target*/
                if (TrueMode.IsMaster && args != null && args.Length >= 1)
                {
                    BasePlayer target = BasePlayer.Find(args?[0]);
                    if (target == null)
                    {
                        string data = FormatData(new FormattedData
                        {
                            String = args?[0]
                        }, LangMessages.PlayerNotFound);
                        string message = ColorSpecialText(data, SignatureColor);
                        XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                        return;
                    }
                    if (args.Length < 2)
                    {
        
                        string data = FormatData(new FormattedData
                        {
                        }, LangMessages.InvalidArgs);
                        string message = ColorSpecialText(data, SignatureColor);
                        XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                        return;
                    }

                    string targetPermission = Regex.IsMatch(args[1], @"^[a-zA-Z]+$") ? args[1] : null;
                    int targetPriority = args[1].All(char.IsDigit) ? Convert.ToInt32(args[1]) : -1;
                    if (!(targetPriority > -1) && string.IsNullOrEmpty(targetPermission))
                    {
                        string data = FormatData(new FormattedData
                        {
                        }, LangMessages.InvalidArgs);
                        string message = ColorSpecialText(data, SignatureColor);
                        XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                        return;
                    }
                    if (targetPriority > -1 && !string.IsNullOrEmpty(targetPermission))
                    {
                        string data = FormatData(new FormattedData
                        {
                            String = "1"
                        }, LangMessages.TooManyArgs);
                        string message = ColorSpecialText(data, SignatureColor);
                        return;
                    }
                    if (targetPriority > -1 || !string.IsNullOrEmpty(targetPermission))
                    {
                        var targetMode =
                            !string.IsNullOrEmpty(targetPermission) ?
                            _configuration.MODES.Where(x => x.Permission == targetPermission).FirstOrDefault() :
                            targetPriority > -1 ? _configuration.MODES.Where(x => x.Priority == targetPriority).FirstOrDefault() : null;
                        if (targetMode == null)
                        {
                            string data = FormatData(new FormattedData
                            {
                                String = !string.IsNullOrEmpty(targetPermission) ? $"<color=#ffffff>Permission:</color> {targetPermission}" : targetPriority > -1 ? $"<color=#ffffff>Priority:</color> {targetPriority}" : ""
                            }, LangMessages.ToggleModeNotFound);
                            string message = ColorSpecialText(data, SignatureColor);
                            XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                            return;
                        }


                        switch (LevelHelper.IsAdmin(player))
                        {
                            case true: Set(target, command, string.Join(" ", args.Skip(2)), XLIB.PLAYER.AuthLevel.LevelType.Player, null, target); break;
                            case false: Set(target, command, string.Join(" ", args.Skip(2)), XLIB.PLAYER.AuthLevel.Get(targetMode.SETTINGS.OnAdmin.AuthLevel), targetMode, target); break;
                        }
                    }
                }
                else /*Toggle Mode For SELF*/
                {
                    string reason = (args != null && args.Length > 0) ? string.Join(" ", args) : string.Empty;
                    switch (LevelHelper.IsAdmin(player))
                    {
                        case true:
                            Set(player, command, null, XLIB.PLAYER.AuthLevel.LevelType.Player);
                            if (ForcedMode.ContainsKey(player.userID)) { ForcedMode.Remove(player.userID); }
                            break;
                        case false: Set(player, command, reason); break;
                    }
                }
            }
        }
        private class LevelHelper
        {
            /// <returns>
            /// <list type="table">Mode <see cref="CoreLevel.ForcedMode"/> IF NOT <paramref name="GetTrueLevel"/></list> 
            /// <list type="table">Mode <see cref="GetModes(BasePlayer)"/> BY HIGHEST <see cref="ConfigData.Modes.Priority"/></list> 
            /// </returns>
            internal static ConfigData.Modes Get(BasePlayer player, bool GetTrueLevel = false)
            {
                Instance?.DebugLog(DebugType.Method);

                if (!GetTrueLevel && CoreLevel.ForcedMode.ContainsKey(player.userID))
                {
                    return CoreLevel.ForcedMode[player.userID];
                }
                else
                {
                    return GetModes(player).OrderByDescending(i => i?.Priority ?? null).FirstOrDefault();
                }
            }

            /// <returns>
            /// <list type="table">ALL <see cref="ConfigData.Modes"/> ASSOCIATED WITH <paramref name="player"/></list>
            /// </returns>
            internal static ConfigData.Modes[] GetModes(BasePlayer player)
            {
                Instance?.DebugLog(DebugType.Method);

                ConfigData.Modes[] modes = _configuration.MODES.Where(x => Instance.permission.UserHasPermission(player.UserIDString, $"{Instance.Name}.{x.Permission}")).ToArray();
                return modes;
            }

            /// <summary>
            /// <list type="table">PREPARES <paramref name="player"/> TOGGLE</list>
            /// </summary>
            /// <returns>TRUE IF READY</returns>
            internal static bool IsReady(BasePlayer player, bool Fix = false)
            {
                Instance?.DebugLog(DebugType.Method);
                var Mode = LevelHelper.Get(player);
                if (Mode == null) { return false; }
                if (player.isMounted && Fix) { return false; }
                if (player.IsFlying && Fix)
                {
                    player.Command("noclip");
                    return false;
                }           
                if (player.HasPlayerFlag(BasePlayer.PlayerFlags.Wounded))
                {
                    string data = FormatData(new FormattedData
                    {
                    }, LangMessages.ToggleDowned);
                    string message = ColorSpecialText(data, SignatureColor);
                    XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                    return false;
                }
                if (player.IsCrawling())
                {
                    string data = FormatData(new FormattedData
                    {
                    }, LangMessages.ToggleCrawling);
                    string message = ColorSpecialText(data, SignatureColor);
                    XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                    return false;
                }
                if (player.IsDead())
                {
                    string data = FormatData(new FormattedData
                    {
                    }, LangMessages.ToggleDead);
                    string message = ColorSpecialText(data, SignatureColor);
                    XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                    return false;
                }
                return true;
            }

            /// <returns>
            /// <list type="table">IF <paramref name="playerID"/> > 0 CHECK IF <see cref="GetAdmins"/> CONTAINS BY <paramref name="playerID"/></list>
            /// <list type="table">IF <paramref name="player"/> NOT NULL AND <paramref name="CheckPerm"/> TRUE CHECKS IF <see cref="GetAdmins"/> CONTAINS <paramref name="player"/> AND IS <see cref="BasePlayer.Connection.authLevel"/> >= 1</list>
            /// <list type="table">IF <paramref name="CheckPerm"/> FALSE CHECKS IF <see cref="BasePlayer.Connection.authLevel"/> >= 1</list>
            /// </returns>
            internal static bool IsAdmin(BasePlayer player = null, ulong playerID = 0, bool CheckPerm = false)
            {
                Instance?.DebugLog(DebugType.Method);

                /*Credit: UIChamp*/
                if ((playerID > 0 && playerID.IsSteamId()) && GetAdmins().Any(x => x != null && x.userID == playerID)) { return true; }

                if (player != null)
                {
                    if (CheckPerm) { return ((player?.Connection?.authLevel >= 1) && GetAdmins().Contains(player)); }
                    else { return (player?.Connection?.authLevel >= 1); }
                }
                return false;
            }

            /// <returns>
            /// ALL <see cref="BasePlayer"/> WITH PERMISSION BY <see cref="ConfigData"/>
            /// </returns>
            internal static BasePlayer[] GetAdmins()
            {
                Instance?.DebugLog(DebugType.Method);

                var perms = _configuration?.MODES?.Select(x => x?.Permission);
                var players = BasePlayer.activePlayerList;
                if (players == null || perms == null) { return null; }
                var list = players.Where(x => x != null && perms.Any(i => i != null && Instance.permission.UserHasPermission(x.UserIDString, $"{Instance.Name}.{i}")));
                return list?.ToArray();
            }

            /// <summary>
            /// Used to check if player is between auth levels (switching)
            /// </summary>
            private static Dictionary<ulong, XLIB.PLAYER.AuthLevel.LevelType> switchingUsers = new Dictionary<ulong, XLIB.PLAYER.AuthLevel.LevelType>();
            internal static void SetSwitching(ulong player, XLIB.PLAYER.AuthLevel.LevelType type, bool remove = false)
            {
                if(remove && switchingUsers.ContainsKey(player)) { switchingUsers.Remove(player); return; }
                if (!switchingUsers.ContainsKey(player)) { switchingUsers.Add(player, type); }
                else { switchingUsers[player] = type; }
            }
            internal static bool IsSwitching(ulong player, out XLIB.PLAYER.AuthLevel.LevelType type)
            {
                if(switchingUsers.ContainsKey(player)) 
                {
                    type = switchingUsers[player];
                    return true; 
                }
                else { type = XLIB.PLAYER.AuthLevel.LevelType.Player; }
                return false;
            }

            internal class Commands
            {
                /// <returns>
                /// <list type="table">ALL COMMANDS <see cref="ConfigData.Modes.Commands"/></list>
                /// </returns>
                internal static string[] GetAll()
                {
                    Instance?.DebugLog(DebugType.Method);

                    return _configuration.MODES.SelectMany(x => x.Commands).ToArray();
                }
            }
            internal class Permission
            {
                /// <returns>
                /// <list type="table">ALL PERMISSIONS <see cref="ConfigData.Modes.Permission"/></list>
                /// </returns>
                internal static string[] GetAll()
                {
                    Instance?.DebugLog(DebugType.Method);

                    return _configuration.MODES.Select(x => x.Permission).ToArray();
                }
            }
        }

        /// <summary>
        /// Resets All Data For Player (Hard Reset)
        /// </summary>
        [ConsoleCommand(PanicCommand)]
        private void PanicCommandHandler(ConsoleSystem.Arg arg)
        {
            DebugLog(DebugType.Method);
            if (arg?.Player() != null) { CoreLevel.HardUnload(arg?.Player(), true, true); }
        }
        #endregion Level Functions

        #region Oxide Hooks
        
        private void OnUserPermissionRevoked(string id, string permName)
        {
            DebugLog(DebugType.Method);

            BasePlayer player;
            BasePlayer.TryFindByID(Convert.ToUInt64(id), out player);
            var permissions = LevelHelper.Permission.GetAll();
            string perm = permName.ToLower().Replace($"{constPluginName}", "").Replace(".", "");
            if (player != null && permissions.Contains(perm))
            {
                //Get Mode by permissions
                var permMode = _configuration.MODES.Where(x => x.Permission == perm).OrderByDescending(i => i.Priority).FirstOrDefault();

                CoreLevel.HardUnload(player, false, false, permMode);

                string data = FormatData(new FormattedData
                {
                    TargetMode = permMode,
                }, LangMessages.PermRevoked);
                string message = ColorSpecialText(data, SignatureColor);
                XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
            }
        }


        private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            DebugLog(DebugType.Method);

            var player = planner?.GetOwnerPlayer();
            if (player != null && LevelHelper.IsAdmin(player))
            {
                var Mode = LevelHelper.Get(player);
                if (Mode == null) { return null; }
                if (!Mode.SETTINGS.OnAdmin.Actions.AllowAll && !Mode.SETTINGS.OnAdmin.Actions.Specfic.CanBuild)
                {
                    string data = FormatData(new FormattedData
                    {
                    }, LangMessages.NoBuilding);
                    string message = ColorSpecialText(data, SignatureColor);
                    XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                    return false;
                }
            }
            return null;
        }
        private object CanCraft(ItemCrafter itemCrafter, ItemBlueprint bp, int amount)
        {
            DebugLog(DebugType.Method);

            var player = itemCrafter?.GetComponent<BasePlayer>();
            if (player != null && LevelHelper.IsAdmin(player))
            {
                var Mode = LevelHelper.Get(player);
                if (Mode == null) { return null; }
                if (!Mode.SETTINGS.OnAdmin.Actions.AllowAll && !Mode.SETTINGS.OnAdmin.Actions.Specfic.CanCraft)
                {
                    string data = FormatData(new FormattedData
                    {
                    }, LangMessages.NoCrafting);
                    string message = ColorSpecialText(data, SignatureColor);
                    XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                    return false;
                }
            }
            return null;
        }
        private object CanLootPlayer(BasePlayer target, BasePlayer looter)
        {
            DebugLog(DebugType.Method);

            if (looter != null && target != null && looter != target && LevelHelper.IsAdmin(looter))
            {
                var Mode = LevelHelper.Get(looter);
                if (Mode == null) { return null; }
                if (!Mode.SETTINGS.OnAdmin.Actions.AllowAll && !Mode.SETTINGS.OnAdmin.Actions.Specfic.CanLootPlayer)
                {
                    string data = FormatData(new FormattedData
                    {
                    }, LangMessages.NoLootPlayers);
                    string message = ColorSpecialText(data, SignatureColor);
                    XLIB.NOTIFICATION.Server.Self.SendMessage(looter, FormatMessage(message));
                    return false;
                }
            }
            return null;
        }
        private object CanLootEntity(BasePlayer player)
        {
            DebugLog(DebugType.Method);

            if (player != null && LevelHelper.IsAdmin(player))
            {
                var Mode = LevelHelper.Get(player);
                if (Mode == null) { return null; }
                if (!Mode.SETTINGS.OnAdmin.Actions.AllowAll && !Mode.SETTINGS.OnAdmin.Actions.Specfic.CanLootEntity)
                {
                    NextTick(player.EndLooting);
                    string data = FormatData(new FormattedData
                    {
                    }, LangMessages.NoLootEntities);
                    string message = ColorSpecialText(data, SignatureColor);
                    XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                    return false;
                }
            }
            return null;
        }
        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            var attacker = hitInfo?.InitiatorPlayer;
            if (attacker != null && LevelHelper.IsAdmin(attacker))
            {
                DebugLog(DebugType.Method);

                var Mode = LevelHelper.Get(attacker);
                if (Mode == null) { return null; }
                if (entity is BuildingBlock || entity.name.Contains("deploy") || entity.name.Contains("building"))
                {
                    if (!Mode.SETTINGS.OnAdmin.Actions.AllowAll && !Mode.SETTINGS.OnAdmin.Actions.Specfic.CanDamageStructures)
                    {
                        hitInfo.damageTypes.Clear();
                        hitInfo.damageTypes.ScaleAll(0);
                        hitInfo.DidHit = false;
                        hitInfo.DoHitEffects = false;

  
                        string data = FormatData(new FormattedData
                        {
                        }, LangMessages.NoDamageStructures);
                        string message = ColorSpecialText(data, SignatureColor);
                        XLIB.NOTIFICATION.Server.Self.SendMessage(attacker, FormatMessage(message));
                        

                        return false;
                    }
                }
            }
            return null;
        }
        private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            DebugLog(DebugType.Method);

            var Mode = LevelHelper.Get(player);
            if (Mode == null) { return; }

            XLIB.PLAYER.AuthLevel.LevelType switchingLevel;
            var IsSwitching = LevelHelper.IsSwitching(player.userID, out switchingLevel);

            if (player != null && newItem != null && !IsSwitching && switchingLevel != XLIB.PLAYER.AuthLevel.LevelType.Player && LevelHelper.IsAdmin(player))
            {
                if (newItem?.info?.category == ItemCategory.Weapon && (!Mode.SETTINGS.OnAdmin.Actions.AllowAll && !Mode.SETTINGS.OnAdmin.Actions.Specfic.CanUseWeapons))
                {
                    string data = FormatData(new FormattedData
                    {
                    }, LangMessages.NoUseWeapons);
                    string message = ColorSpecialText(data, SignatureColor);
                    XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                    newItem?.Remove();
                }
            }
        }
        private object CanEquipItem(PlayerInventory inventory, Item item, int targetPos)
        {
            DebugLog(DebugType.Method);

            var player = inventory?.GetComponent<BasePlayer>();

            XLIB.PLAYER.AuthLevel.LevelType switchingLevel;
            var IsSwitching = LevelHelper.IsSwitching(player.userID, out switchingLevel);

            if (player != null && item != null && !IsSwitching && switchingLevel != XLIB.PLAYER.AuthLevel.LevelType.Player && LevelHelper.IsAdmin(player))
            {
                var Mode = LevelHelper.Get(player);
                if (Mode == null) { return null; }

                if (item?.info?.category == ItemCategory.Weapon && !Mode.SETTINGS.OnAdmin.Actions.AllowAll && !Mode.SETTINGS.OnAdmin.Actions.Specfic.CanUseWeapons)
                {
                    string data = FormatData(new FormattedData
                    {
                    }, LangMessages.NoUseWeapons);
                    string message = ColorSpecialText(data, SignatureColor);
                    XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                    item?.Remove();
                    return false;
                }
            }
            return null;
        }
        private object OnEntityTakeDamage(BasePlayer target, HitInfo hitInfo)
        {
            var attacker = hitInfo?.InitiatorPlayer;
            if (attacker != null && LevelHelper.IsAdmin(attacker))
            {
                DebugLog(DebugType.Method);

                var Mode = LevelHelper.Get(attacker);
                if (Mode == null) { return null; }


                if (!Mode.SETTINGS.OnAdmin.Actions.AllowAll && !Mode.SETTINGS.OnAdmin.Actions.Specfic.CanHurtPlayers)
                {
                    hitInfo.damageTypes.Clear();
                    hitInfo.damageTypes.ScaleAll(0);
                    hitInfo.DidHit = false;
                    hitInfo.DoHitEffects = false;

       
                    string data = FormatData(new FormattedData
                    {
                    }, LangMessages.NoHurtPlayers);
                    string message = ColorSpecialText(data, SignatureColor);
                    XLIB.NOTIFICATION.Server.Self.SendMessage(attacker, FormatMessage(message));
                    return false;
                }
            }
            return null;
        }
        private void OnItemDropped(Item item)
        {
            DebugLog(DebugType.Method);

            if (item == null) { return; }
            var player = item?.GetOwnerPlayer();


            if (player != null && LevelHelper.IsAdmin(player))
            {
                var Mode = LevelHelper.Get(player);
                if (Mode == null) { return; }

                if (!Mode.SETTINGS.OnAdmin.Actions.AllowAll && !Mode.SETTINGS.OnAdmin.Actions.Specfic.CanDropItems)
                {
                    string data = FormatData(new FormattedData
                    {
                    }, LangMessages.NoDropItems);
                    string message = ColorSpecialText(data, SignatureColor);
                    XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                    item?.Remove();
                }
            }
        }


        private object OnPlayerCommand(BasePlayer player, string command, string[] args)
        {
            DebugLog(DebugType.Method);

            if (!HandleCommands(player, command))
            {
   
                string data = FormatData(new FormattedData
                {
                    Command = command
                }, LangMessages.RestrictedCommand);
                string message = ColorSpecialText(data, SignatureColor);
                XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                
                return false;
            }
            return null;
        }
        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg?.Player() == null) { return null; }

            DebugLog(DebugType.Method);

            if (!HandleCommands(arg?.Player(), arg.cmd.Name))
            {

                var Mode = LevelHelper.Get(arg.Player());
                if (Mode != null)
                {
                    string data = FormatData(new FormattedData
                    {
                        Command = arg.cmd.Name
                    }, LangMessages.RestrictedCommand);
                    string message = ColorSpecialText(data, SignatureColor);
                    XLIB.NOTIFICATION.Server.Self.SendMessage(arg?.Player(), FormatMessage(message));
                }
                return false;
            };

            return null;
        }


        private void OnPlayerConnected(BasePlayer player)
        {
            DebugLog(DebugType.Method);

            var Mode = LevelHelper.Get(player);
            if (Mode == null) { return; }
            
            XLIB.PLAYER.Location.Utility.UnMountChair(player);
            if (LevelHelper.IsAdmin(player))
            {
                if (Mode.SETTINGS.OnAdmin.Interface.Button.Enabled && !Mode.SETTINGS.OnAdmin.RequireReason)
                {
                    UI.Draw.Button(player);
                }
                if (Mode.SETTINGS.OnAdmin.Interface.Panel.Enabled)
                {
                    UI.Draw.PulsePanel.Start(player);
                }
                if (Mode.SETTINGS.OnAdmin.Interface.Menu.Enabled)
                {
                    UI.Draw.NavMenu(player);
                }
            }
        }
        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            DebugLog(DebugType.Method);

            var Mode = LevelHelper.Get(player);
            if (Mode == null) { return; }
            if (LevelHelper.IsAdmin(player) && Mode.SETTINGS.OnAdmin.RevertOnTrigger && LevelHelper.IsReady(player))
            {
                CoreLevel.Set(player, null, null, XLIB.PLAYER.AuthLevel.LevelType.Player, null, null, true);
            }
        }



        private void OnPlayerBanned(string name, ulong id, string address, string reason) => HandleBan(id, reason);
        /*Handle Bans for universal Plugins*/
        private void OnUserBanned(string name, ulong id, string address, string reason) => HandleBan(id, reason);


        private object OnPlayerViolation(BasePlayer player, AntiHackType type, float amount)
        {
            DebugLog(DebugType.Method);

            var Mode = LevelHelper.Get(player);
            if (Mode != null && LevelHelper.IsAdmin(player) && Mode.SETTINGS.OnAdmin.IgnoreViolations)
            {
                string data = FormatData(new FormattedData
                {
                    Violation = type
                }, LangMessages.ViolationRegular);
                string message = ColorSpecialText(data, SignatureColor);
                XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                return false;
            }
            return null;
        }
        #endregion Oxide Hooks

        #region Third-Party Plugins
        private void OnVanishReappear(BasePlayer player)
        {
            DebugLog(DebugType.Method);

            if (!PluginInstalled(Vanish)) { return; }
            var Mode = LevelHelper.Get(player);
            if (Mode == null) { return; }
            if (!UICommands.ContainsKey(player.userID)) { UICommands.Add(player.userID, new UICommandsData()); }
            UICommands[player.userID].IsVanish = false;
            UI.Draw.UpdateNavMenu(player);
        }
        private void OnVanishDisappear(BasePlayer player)
        {
            DebugLog(DebugType.Method);


            if (!PluginInstalled(Vanish)) { return; }
            var Mode = LevelHelper.Get(player);
            if (Mode == null) { return; }
            if (!UICommands.ContainsKey(player.userID)) { UICommands.Add(player.userID, new UICommandsData()); }
            if (LevelHelper.IsAdmin(player))
            {
                if (Mode.IsMaster) { return; }
                if ((Mode.SETTINGS.OnAdmin.Plugins.Blocked.All || Mode.SETTINGS.OnAdmin.Plugins.Blocked.Specfic.Vanish))
                {
                    player.Command("vanish");
            
                    string data = FormatData(new FormattedData
                    {
                        Plugin = "Vanish"
                    }, LangMessages.NoPluginUsage);
                    string message = ColorSpecialText(data, SignatureColor);
                    XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                    
                }
                else /*Allowed*/
                {
                    UICommands[player.userID].IsVanish = true;
                    UI.Draw.UpdateNavMenu(player);
                }
            }
            else
            {
                if ((Mode.SETTINGS.OnPlayer.Plugins.Blocked.All || Mode.SETTINGS.OnPlayer.Plugins.Blocked.Specfic.Vanish))
                {
                    player.Command("vanish");
          
                    string data = FormatData(new FormattedData
                    {
                        Plugin = "Vanish"
                    }, LangMessages.NoPluginUsage);
                    string message = ColorSpecialText(data, SignatureColor);
                    XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));

                    UICommands[player.userID].IsVanish = false;
                    UI.Draw.UpdateNavMenu(player);
                }
                else /*Allowed*/
                {
                    UICommands[player.userID].IsVanish = true;
                    UI.Draw.UpdateNavMenu(player);
                }
            }
        }


        private void OnRadarDeactivated(BasePlayer player)
        {
            DebugLog(DebugType.Method);

            if (!PluginInstalled(AdminRadar)) { return; }
            var Mode = LevelHelper.Get(player);
            if (Mode == null) { return; }
            if (!UICommands.ContainsKey(player.userID)) { UICommands.Add(player.userID, new UICommandsData()); }
            UICommands[player.userID].IsRadar = false;
            UI.Draw.UpdateNavMenu(player);
        }
        private void OnRadarActivated(BasePlayer player)
        {
            DebugLog(DebugType.Method);

            if (!PluginInstalled(AdminRadar)) { return; }
            if (!UICommands.ContainsKey(player.userID)) { UICommands.Add(player.userID, new UICommandsData()); }
            var Mode = LevelHelper.Get(player);
            if (Mode == null) { return; }
            bool IsActive = AdminRadar.Call<bool>("IsRadar", player.UserIDString);
            if (LevelHelper.IsAdmin(player))
            {
                if (Mode.IsMaster) { return; }
                if (IsActive && (Mode.SETTINGS.OnAdmin.Plugins.Blocked.All || Mode.SETTINGS.OnAdmin.Plugins.Blocked.Specfic.AdminRadar))
                {
                    player.Command("radar");
             
                    string data = FormatData(new FormattedData
                    {
                        Plugin = "AdminRadar"
                    }, LangMessages.NoPluginUsage);
                    string message = ColorSpecialText(data, SignatureColor);
                    XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));

                    UICommands[player.userID].IsRadar = false;
                    UI.Draw.UpdateNavMenu(player);
                }
                else if (IsActive) /*Allowed*/
                {
                    UICommands[player.userID].IsRadar = true;
                    UI.Draw.UpdateNavMenu(player);
                }
            }
            else
            {
                if (IsActive && (Mode.SETTINGS.OnPlayer.Plugins.Blocked.All || Mode.SETTINGS.OnPlayer.Plugins.Blocked.Specfic.AdminRadar))
                {
                    player.Command("radar");
                
                    string data = FormatData(new FormattedData
                    {
                        Plugin = "AdminRadar"
                    }, LangMessages.NoPluginUsage);
                    string message = ColorSpecialText(data, SignatureColor);
                    XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));

                    UICommands[player.userID].IsRadar = false;
                    UI.Draw.UpdateNavMenu(player);
                }
                else if (IsActive) /*Allowed*/
                {
                    UICommands[player.userID].IsRadar = true;
                    UI.Draw.UpdateNavMenu(player);
                }
            }
        }

        private void OnGodmodeToggled(string playerId, bool enabled)
        {
            DebugLog(DebugType.Method);

            BasePlayer player;
            BasePlayer.TryFindByID(Convert.ToUInt64(playerId), out player);
            if (player == null) { return; }

            if (!PluginInstalled(Godmode)) { return; }
            if (!UICommands.ContainsKey(player.userID)) { UICommands.Add(player.userID, new UICommandsData()); }
            var Mode = LevelHelper.Get(player);
            if (Mode == null) { return; }
            bool IsActive = Godmode.Call<bool>("IsGod", player.UserIDString);
            if (LevelHelper.IsAdmin(player))
            {
                if (Mode.IsMaster) { return; }
                if (IsActive && (Mode.SETTINGS.OnAdmin.Plugins.Blocked.All || Mode.SETTINGS.OnAdmin.Plugins.Blocked.Specfic.Godmode))
                {
                    player.Command("god");

                    string data = FormatData(new FormattedData
                    {
                        Plugin = "Godmode"
                    }, LangMessages.NoPluginUsage);
                    string message = ColorSpecialText(data, SignatureColor);
                    XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                    
                }
                if (!enabled)
                {
                    UICommands[player.userID].IsGodmode = false;
                    UI.Draw.UpdateNavMenu(player);
                }
                if (enabled)
                {
                    UICommands[player.userID].IsGodmode = true;
                    UI.Draw.UpdateNavMenu(player);
                }
            }
            else
            {
                if (IsActive && (Mode.SETTINGS.OnPlayer.Plugins.Blocked.All || Mode.SETTINGS.OnPlayer.Plugins.Blocked.Specfic.Godmode))
                {
                    player.Command("god");
              
                    string data = FormatData(new FormattedData
                    {
                        Plugin = "Godmode"
                    }, LangMessages.NoPluginUsage);
                    string message = ColorSpecialText(data, SignatureColor);
                    XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                    
                }
                if (!enabled)
                {
                    UICommands[player.userID].IsGodmode = false;
                    UI.Draw.UpdateNavMenu(player);
                }
                if (enabled)
                {
                    UICommands[player.userID].IsGodmode = true;
                    UI.Draw.UpdateNavMenu(player);
                }
            }
        }

        private void LockBackpack(BasePlayer player)
        {
            DebugLog(DebugType.Method);

            if (Backpacks == null) { return; }
            var Mode = LevelHelper.Get(player);
            if (Mode == null) { return; }
            if ((Mode.SETTINGS.OnAdmin.Plugins.Blocked.All || Mode.SETTINGS.OnAdmin.Plugins.Blocked.Specfic.Backpacks))
            {
                var Backpack = Backpacks?.Call<ItemContainer>("API_GetBackpackContainer", player.userID);
                if (Backpack != null)
                {
                    XLIB.PLAYER.Inventory.Utility.LockContainer(Backpack);
                }
            }
        }
        private void UnLockBackpack(BasePlayer player)
        {
            DebugLog(DebugType.Method);

            if (Backpacks == null) { return; }
            var Mode = LevelHelper.Get(player);
            if (Mode == null) { return; }
            if ((Mode.SETTINGS.OnAdmin.Plugins.Blocked.All || Mode.SETTINGS.OnAdmin.Plugins.Blocked.Specfic.Backpacks))
            {
                var Backpack = Backpacks?.Call<ItemContainer>("API_GetBackpackContainer", player.userID);
                if (Backpack != null)
                {
                    XLIB.PLAYER.Inventory.Utility.UnlockContainer(Backpack);
                }
            }
        }
        #endregion Third-Party Plugins

        #region AdminOutfit
        private class AdminOutfit
        {
            internal static void Set(BasePlayer player)
            {
                Instance?.DebugLog(DebugType.Method);

                var Mode = LevelHelper.Get(player);
                if (Mode != null && Mode.SETTINGS.OnAdmin.AdminOutfit.Enabled)
                {
                    XLIB.PLAYER.Inventory.Utility.DestroyItems(player.inventory.containerWear.itemList.ToArray());
                    int length = Mode.SETTINGS.OnAdmin.AdminOutfit.Settings.Outfit.Length < player.inventory.containerWear.capacity ? Mode.SETTINGS.OnAdmin.AdminOutfit.Settings.Outfit.Length : player.inventory.containerWear.capacity;
                    for (int i = 0; i < length; i++)
                    {
                        var item = Mode.SETTINGS.OnAdmin.AdminOutfit.Settings?.Outfit[i];
                        var Identifier = item.Split(new string[] { "::" }, StringSplitOptions.None);
                        ulong value = Identifier[1].All(char.IsDigit) ? Convert.ToUInt64(Identifier[1]) : 0;
                        var itemResult = XLIB.PLAYER.Inventory.Utility.CreateItem(Identifier[0], 1, value);
                        XLIB.PLAYER.Inventory.Utility.GiveItem(player, itemResult, player.inventory.containerWear, i);
                    }
                    if (Mode.SETTINGS.OnAdmin.AdminOutfit.Locked) { XLIB.PLAYER.Inventory.Utility.LockContainer(player.inventory.containerWear); }
                }
            }
        }
        #endregion AdminOutfit 

        #region UI
        private class UI
        {
            internal enum UIElement
            {
                Button,
                Panel,
                Menu,
                ALL
            }
            internal static void Destroy(BasePlayer player, UIElement Specfic = UIElement.ALL)
            {
                Instance?.DebugLog(DebugType.Method);

                if (Specfic == UIElement.ALL)
                {
                    CuiHelper.DestroyUi(player, $"{Instance.Name}.UI.ButtonPanel");
                    CuiHelper.DestroyUi(player, $"{Instance.Name}.UI.Panel");
                    CuiHelper.DestroyUi(player, $"{Instance.Name}.UI.Menu");
                    return;
                }

                switch (Specfic)
                {
                    case UIElement.Button:
                        CuiHelper.DestroyUi(player, $"{Instance.Name}.UI.ButtonPanel");
                        break;

                    case UIElement.Panel:
                        CuiHelper.DestroyUi(player, $"{Instance.Name}.UI.Panel");
                        CuiHelper.DestroyUi(player, $"{Instance.Name}.UI.Panel.Text");
                        break;

                    case UIElement.Menu:
                        CuiHelper.DestroyUi(player, $"{Instance.Name}.UI.Menu");
                        break;
                }
            }
            private static void Add(BasePlayer player, CuiElementContainer Container)
            {
                Instance?.DebugLog(DebugType.Method);

                CuiHelper.AddUi(player, Container);
            }

            internal partial class Draw
            {
                internal static void Button(BasePlayer player)
                {
                    Instance?.DebugLog(DebugType.Method);

                    var Mode = LevelHelper.Get(player);
                    if (Mode != null && Mode.SETTINGS.OnAdmin.Interface.Button.Enabled)
                    {
                        Destroy(player, UIElement.Button);
                        var Anchor = new XLIB.UI.UIPoint.Anchor
                        {
                            Min_Width = Mode.SETTINGS.OnAdmin.Interface.Button.Settings.Design.Anchor.Min_Width,
                            Min_Height = Mode.SETTINGS.OnAdmin.Interface.Button.Settings.Design.Anchor.Min_Height,
                            Max_Width = Mode.SETTINGS.OnAdmin.Interface.Button.Settings.Design.Anchor.Max_Width,
                            Max_Height = Mode.SETTINGS.OnAdmin.Interface.Button.Settings.Design.Anchor.Max_Height
                        };
                        var Offset = new XLIB.UI.UIPoint.Offset
                        {
                            Min_Width = Mode.SETTINGS.OnAdmin.Interface.Button.Settings.Design.Offset.Min_Width,
                            Min_Height = Mode.SETTINGS.OnAdmin.Interface.Button.Settings.Design.Offset.Min_Height,
                            Max_Width = Mode.SETTINGS.OnAdmin.Interface.Button.Settings.Design.Offset.Max_Width,
                            Max_Height = Mode.SETTINGS.OnAdmin.Interface.Button.Settings.Design.Offset.Max_Height,
                            Top = Mode.SETTINGS.OnAdmin.Interface.Button.Settings.Design.Offset.Points.Top,
                            Bottom = Mode.SETTINGS.OnAdmin.Interface.Button.Settings.Design.Offset.Points.Bottom,
                            Left = Mode.SETTINGS.OnAdmin.Interface.Button.Settings.Design.Offset.Points.Left,
                            Right = Mode.SETTINGS.OnAdmin.Interface.Button.Settings.Design.Offset.Points.Right,
                        };
                        var Color = new XLIB.UI.UIColor
                        {
                            color = "#000000",
                            alpha = 0.0
                        };

                        var Container = XLIB.UI.Create.Container(Anchor, Offset, Color, new XLIB.UI.UIProperties(), XLIB.UI.UILayer.Overlay, $"{Instance.Name}.UI.ButtonPanel");

                        XLIB.UI.Create.Button(ref Container,
                            new XLIB.UI.UIPoint.Anchor
                            {
                                Min_Height = 0.0,
                                Min_Width = 0.0,
                                Max_Height = 1.0,
                                Max_Width = 1.0
                            },
                            new XLIB.UI.UIColor
                            {
                                color = LevelHelper.IsAdmin(player) ? Mode.SETTINGS.OnAdmin.Interface.Button.Settings.ActiveColor : Mode.SETTINGS.OnAdmin.Interface.Button.Settings.InactiveColor,
                                alpha = Mode.SETTINGS.OnAdmin.Interface.Button.Settings.Opacity
                            },
                            new XLIB.UI.UIProperties
                            {
                                Material = "assets/content/ui/uibackgroundblur.mat"
                            }, $"{Instance.Name}.UI.ButtonPanel", $"{Instance.Name}.UI.ButtonPanel.Button", $"{Instance.Name.ToLower()}.ui.commandhandler togglemode",
                            new XLIB.UI.UIColor
                            {
                                color = LevelHelper.IsAdmin(player) ? Mode.SETTINGS.OnAdmin.Interface.Button.Settings.ActiveTextColor : Mode.SETTINGS.OnAdmin.Interface.Button.Settings.InactiveTextColor,
                            },
                            TextAnchor.MiddleCenter, LevelHelper.IsAdmin(player) ? Mode.SETTINGS.OnAdmin.Interface.Button.Settings.ActiveText : Mode.SETTINGS.OnAdmin.Interface.Button.Settings.InactiveText, 14);
                        Add(player, Container);
                    }
                }

                internal class PulsePanel
                {
                    internal static Dictionary<ulong, TimerData> PanelTimers = new Dictionary<ulong, TimerData>();
                    internal class TimerData
                    {
                        internal Timer PulseTimer { get; set; }
                        internal bool Running { get; set; } = false;
                        internal bool IsDrawn { get; set; } = false;
                    }
                    internal static void Start(BasePlayer player)
                    {
                        Instance?.DebugLog(DebugType.Method);

                        var Mode = LevelHelper.Get(player);
                        if (Mode != null && Mode.SETTINGS.OnAdmin.Interface.Panel.Enabled)
                        {
                            if (!PanelTimers.ContainsKey(player.userID)) { PanelTimers.Add(player.userID, new TimerData()); }
                            Destroy(player, UIElement.Panel);
                            PanelTimers[player.userID].Running = true;
                            PanelTimers[player.userID].IsDrawn = true;
                            Draw(player); PulseLoop(player);
                        }

                    }
                    private static void PulseLoop(BasePlayer player)
                    {
                        Instance?.DebugLog(DebugType.Method);

                        var mode = LevelHelper.Get(player);
                        if (mode != null && mode.SETTINGS.OnAdmin.Interface.Panel.Enabled)
                        {
                            if (!PanelTimers.ContainsKey(player.userID)) { return; }

                            try { PanelTimers[player.userID].PulseTimer.Destroy(); } catch { }
                            if (PanelTimers[player.userID].Running)
                            {
                                PanelTimers[player.userID].PulseTimer = Instance.timer.Every(mode.SETTINGS.OnAdmin.Interface.Panel.Settings.PulseSpeed, () =>
                                {
                                    switch (PanelTimers[player.userID].IsDrawn)
                                    {
                                        case true:
                                            Destroy(player, UIElement.Panel);
                                            PanelTimers[player.userID].IsDrawn = false;
                                            break;

                                        case false:
                                            Draw(player);
                                            PanelTimers[player.userID].IsDrawn = true;
                                            break;
                                    }
                                });
                            }
                            else { Stop(player); }
                        }
                    }
                    internal static void Stop(BasePlayer player)
                    {
                        Instance?.DebugLog(DebugType.Method);

                        var mode = LevelHelper.Get(player);
                        if (mode != null && mode.SETTINGS.OnAdmin.Interface.Panel.Enabled)
                        {
                            if (PanelTimers.ContainsKey(player.userID))
                            {
                                PanelTimers[player.userID].Running = false;
                                PanelTimers[player.userID].PulseTimer.Destroy();
                                PanelTimers.Remove(player.userID);
                                Destroy(player, UIElement.Panel);
                            }
                        }
                    }

                    private static void Draw(BasePlayer player)
                    {
                        Instance?.DebugLog(DebugType.Method);

                        var Mode = LevelHelper.Get(player);
                        if (Mode != null && Mode.SETTINGS.OnAdmin.Interface.Panel.Enabled)
                        {
                            Destroy(player, UIElement.Panel);

                            var Anchor = new XLIB.UI.UIPoint.Anchor
                            {
                                Min_Width = Mode.SETTINGS.OnAdmin.Interface.Panel.Settings.Design.Anchor.Min_Width,
                                Min_Height = Mode.SETTINGS.OnAdmin.Interface.Panel.Settings.Design.Anchor.Min_Height,
                                Max_Width = Mode.SETTINGS.OnAdmin.Interface.Panel.Settings.Design.Anchor.Max_Width,
                                Max_Height = Mode.SETTINGS.OnAdmin.Interface.Panel.Settings.Design.Anchor.Max_Height
                            };
                            var Offset = new XLIB.UI.UIPoint.Offset
                            {
                                Min_Width = Mode.SETTINGS.OnAdmin.Interface.Panel.Settings.Design.Offset.Min_Width,
                                Min_Height = Mode.SETTINGS.OnAdmin.Interface.Panel.Settings.Design.Offset.Min_Height,
                                Max_Width = Mode.SETTINGS.OnAdmin.Interface.Panel.Settings.Design.Offset.Max_Width,
                                Max_Height = Mode.SETTINGS.OnAdmin.Interface.Panel.Settings.Design.Offset.Max_Height,
                                Top = Mode.SETTINGS.OnAdmin.Interface.Panel.Settings.Design.Offset.Points.Top,
                                Bottom = Mode.SETTINGS.OnAdmin.Interface.Panel.Settings.Design.Offset.Points.Bottom,
                                Left = Mode.SETTINGS.OnAdmin.Interface.Panel.Settings.Design.Offset.Points.Left,
                                Right = Mode.SETTINGS.OnAdmin.Interface.Panel.Settings.Design.Offset.Points.Right,
                            };
                            var Properties = new XLIB.UI.UIProperties
                            {
                                Material = "assets/content/ui/uibackgroundblur.mat",
                                Fade = 0.5f
                            };
                            var Color = new XLIB.UI.UIColor
                            {
                                color = "#e8e8e8",
                                alpha = 0.032
                            };

                            var Container = XLIB.UI.Create.Container(Anchor, Offset, Color, Properties, XLIB.UI.UILayer.Hud, $"{Instance.Name}.UI.Panel");
                            XLIB.UI.Create.Label(ref Container,
                                new XLIB.UI.UIPoint.Anchor { Min_Height = 0.0, Min_Width = 0.0, Max_Height = 1.0, Max_Width = 1.0 },
                                new XLIB.UI.UIProperties { Fade = 0.5f },
                                $"{Instance.Name}.UI.Panel",
                                $"{Instance.Name}.UI.Panel.Text",
                                new XLIB.UI.UIColor { color = Mode.SETTINGS.OnAdmin.Interface.Panel.Settings.ActiveTextColor }, TextAnchor.MiddleCenter, Mode.SETTINGS.OnAdmin.Interface.Panel.Settings.ActiveText, 14
                            );

                            Add(player, Container);
                        }
                    }
                }


                internal static void UpdateNavMenu(BasePlayer player)
                {
                    var Mode = LevelHelper.Get(player);
                    if (Mode == null) { return; }
                    if (!LevelHelper.IsAdmin(player)) { return; }
                    if (!Mode.SETTINGS.OnAdmin.Interface.Menu.Enabled) { return; }
                    if (!UICommands.ContainsKey(player.userID)) { UICommands.Add(player.userID, new UICommandsData()); }
                    NavMenu(player);
                }
                internal static void NavMenu(BasePlayer player)
                {
                    Instance?.DebugLog(DebugType.Method);

                    if (!UICommands.ContainsKey(player.userID)) { UICommands.Add(player.userID, new UICommandsData()); }

                    Destroy(player, UIElement.Menu);
                    var Mode = LevelHelper.Get(player);
                    if (Mode != null && Mode.SETTINGS.OnAdmin.Interface.Menu.Enabled)
                    {
                        var Anchor = new XLIB.UI.UIPoint.Anchor
                        {
                            Min_Width = Mode.SETTINGS.OnAdmin.Interface.Menu.Settings.Design.Anchor.Min_Width,
                            Min_Height = Mode.SETTINGS.OnAdmin.Interface.Menu.Settings.Design.Anchor.Min_Height,
                            Max_Width = Mode.SETTINGS.OnAdmin.Interface.Menu.Settings.Design.Anchor.Max_Width,
                            Max_Height = Mode.SETTINGS.OnAdmin.Interface.Menu.Settings.Design.Anchor.Max_Height
                        };
                        var Offset = new XLIB.UI.UIPoint.Offset
                        {
                            Min_Width = Mode.SETTINGS.OnAdmin.Interface.Menu.Settings.Design.Offset.Min_Width,
                            Min_Height = Mode.SETTINGS.OnAdmin.Interface.Menu.Settings.Design.Offset.Min_Height,
                            Max_Width = Mode.SETTINGS.OnAdmin.Interface.Menu.Settings.Design.Offset.Max_Width,
                            Max_Height = Mode.SETTINGS.OnAdmin.Interface.Menu.Settings.Design.Offset.Max_Height,
                            Top = Mode.SETTINGS.OnAdmin.Interface.Menu.Settings.Design.Offset.Points.Top,
                            Bottom = Mode.SETTINGS.OnAdmin.Interface.Menu.Settings.Design.Offset.Points.Bottom,
                            Left = Mode.SETTINGS.OnAdmin.Interface.Menu.Settings.Design.Offset.Points.Left,
                            Right = Mode.SETTINGS.OnAdmin.Interface.Menu.Settings.Design.Offset.Points.Right,
                        };
                        var Properties = new XLIB.UI.UIProperties
                        {
                            Material = "assets/content/ui/uibackgroundblur.mat",
                        };
                        var Color = new XLIB.UI.UIColor
                        {
                            color = "#000000",
                            alpha = 0.0
                        };

                        var ActiveColor = Mode.SETTINGS.OnAdmin.Interface.Menu.Settings.ActiveColor;
                        var InactiveColor = Mode.SETTINGS.OnAdmin.Interface.Menu.Settings.InactiveColor;
                        var NotFoundColor = Mode.SETTINGS.OnAdmin.Interface.Menu.Settings.NotFoundColor;

                        /*Root*/
                        var Container = XLIB.UI.Create.Container(Anchor, Offset, Color, Properties, XLIB.UI.UILayer.Overlay, $"{Instance.Name}.UI.Menu");

                        const double TopHeight = 0.25;
                        const double SplitterHeight = 0.032;
                        XLIB.UI.Create.Panel(ref Container, new XLIB.UI.UIPoint.Anchor
                        {
                            Min_Height = (1.0 - TopHeight),
                            Max_Height = 1.0,
                            Max_Width = 1.0
                        }, new XLIB.UI.UIColor { color = "#000000", alpha = 0.0 }, new XLIB.UI.UIProperties { },
                        $"{Instance.Name}.UI.Menu", $"{Instance.Name}.UI.Menu.TopPanel");

                        /*Noclip*/
                        XLIB.UI.Create.Button(ref Container,
                            new XLIB.UI.UIPoint.Anchor { Min_Width = 0.0, Max_Width = 0.5, Max_Height = 1.0 },
                            new XLIB.UI.UIColor { color = UICommands[player.userID].IsFlying ? ActiveColor : InactiveColor, alpha = Mode.SETTINGS.OnAdmin.Interface.Menu.Settings.Opacity, },
                            Properties,
                            $"{Instance.Name}.UI.Menu.TopPanel", $"{Instance.Name}.UI.Menu.TopPanel.Button.Noclip", $"{Instance.Name.ToLower()}.ui.commandhandler noclip", new XLIB.UI.UIColor { color = Mode.SETTINGS.OnAdmin.Interface.Menu.Settings.ActiveTextColor }, TextAnchor.MiddleCenter, "Noclip", 14);

                        /*Weather*/
                        XLIB.UI.Create.Button(ref Container,
                            new XLIB.UI.UIPoint.Anchor { Min_Width = 0.5, Max_Width = 1.0, Max_Height = 1.0 },
                            new XLIB.UI.UIColor { color = UICommands[player.userID].IsWeather ? ActiveColor : InactiveColor, alpha = Mode.SETTINGS.OnAdmin.Interface.Menu.Settings.Opacity, },
                            Properties,
                            $"{Instance.Name}.UI.Menu.TopPanel", $"{Instance.Name}.UI.Menu.TopPanel.Button.Weather", $"{Instance.Name.ToLower()}.ui.commandhandler weather", new XLIB.UI.UIColor { color = Mode.SETTINGS.OnAdmin.Interface.Menu.Settings.ActiveTextColor }, TextAnchor.MiddleCenter, "Weather", 14);

                        /*Splitter Above Commands Below Plugins*/
                        XLIB.UI.Create.Panel(ref Container, new XLIB.UI.UIPoint.Anchor
                        {
                            Min_Height = (1.0 - (TopHeight + SplitterHeight)),
                            Max_Height = (1.0 - TopHeight),
                            Max_Width = 1.0
                        }, new XLIB.UI.UIColor { color = SignatureColor, alpha = Mode.SETTINGS.OnAdmin.Interface.Menu.Settings.Opacity },
                        Properties,
                        $"{Instance.Name}.UI.Menu", $"{Instance.Name}.UI.Menu.PanelSplitter");


                        /*Plugin Container*/
                        XLIB.UI.Create.Panel(ref Container, new XLIB.UI.UIPoint.Anchor
                        {
                            Max_Height = (1.0 - (TopHeight + SplitterHeight)),
                            Max_Width = 1.0
                        }, new XLIB.UI.UIColor { color = "#000000", alpha = 0.0 }, new XLIB.UI.UIProperties { },
                        $"{Instance.Name}.UI.Menu", $"{Instance.Name}.UI.Menu.BottomPanel");

                        /*AdminRadar*/
                        XLIB.UI.Create.Button(ref Container,
                            new XLIB.UI.UIPoint.Anchor { Max_Width = 1.0, Min_Height = 0.6666, Max_Height = 1.0 },
                            new XLIB.UI.UIColor { color = !PluginInstalled(Instance.AdminRadar) ? NotFoundColor : UICommands[player.userID].IsRadar ? ActiveColor : InactiveColor, alpha = Mode.SETTINGS.OnAdmin.Interface.Menu.Settings.Opacity, },
                            Properties,
                            $"{Instance.Name}.UI.Menu.BottomPanel", $"{Instance.Name}.UI.Menu.BottomPanel.Button.AdminRadar", $"{Instance.Name.ToLower()}.ui.commandhandler adminradar", new XLIB.UI.UIColor { color = Mode.SETTINGS.OnAdmin.Interface.Menu.Settings.ActiveTextColor }, TextAnchor.MiddleCenter, "Admin Radar", 14);

                        /*Godmode*/
                        XLIB.UI.Create.Button(ref Container,
                            new XLIB.UI.UIPoint.Anchor { Max_Width = 1.0, Min_Height = 0.3333, Max_Height = 0.6666 },
                            new XLIB.UI.UIColor { color = !PluginInstalled(Instance.Godmode) ? NotFoundColor : UICommands[player.userID].IsGodmode ? ActiveColor : InactiveColor, alpha = Mode.SETTINGS.OnAdmin.Interface.Menu.Settings.Opacity },
                            Properties,
                            $"{Instance.Name}.UI.Menu.BottomPanel", $"{Instance.Name}.UI.Menu.TopPanel.Button.Godmode", $"{Instance.Name.ToLower()}.ui.commandhandler godmode", new XLIB.UI.UIColor { color = Mode.SETTINGS.OnAdmin.Interface.Menu.Settings.ActiveTextColor }, TextAnchor.MiddleCenter, "Godmode", 14);

                        /*Vanish*/
                        XLIB.UI.Create.Button(ref Container,
                            new XLIB.UI.UIPoint.Anchor { Max_Width = 1.0, Min_Height = 0.0, Max_Height = 0.3333 },
                            new XLIB.UI.UIColor { color = !PluginInstalled(Instance.Vanish) ? NotFoundColor : UICommands[player.userID].IsVanish ? ActiveColor : InactiveColor, alpha = Mode.SETTINGS.OnAdmin.Interface.Menu.Settings.Opacity, },
                            Properties,
                            $"{Instance.Name}.UI.Menu.BottomPanel", $"{Instance.Name}.UI.Menu.BottomPanel.Button.Vanish", $"{Instance.Name.ToLower()}.ui.commandhandler vanish", new XLIB.UI.UIColor { color = Mode.SETTINGS.OnAdmin.Interface.Menu.Settings.ActiveTextColor }, TextAnchor.MiddleCenter, "Vanish", 14);

                        Add(player, Container);
                    }
                }
            }
        }
        #endregion UI
        #region UI Commands
        internal static Dictionary<ulong, UICommandsData> UICommands = new Dictionary<ulong, UICommandsData>();
        internal class UICommandsData
        {
            internal bool IsFlying { get; set; } = false;
            internal bool IsGodmode { get; set; } = false;
            internal bool IsWeather { get; set; } = false;
            internal bool IsRadar { get; set; } = false;
            internal bool IsVanish { get; set; } = false;
        }

        //Handles UI Commands NavMenu & Button Etc
        [ConsoleCommand(constPluginName + ".ui.commandhandler")]
        private void HandleUICommands(ConsoleSystem.Arg arg)
        {
            DebugLog(DebugType.Method);

            string Identifier = arg.HasArgs(1) ? arg.Args[0] : null;
            var player = arg?.Player();
            var Mode = LevelHelper.Get(player);
            if (Identifier == null || player == null || Mode == null) { return; }
            if (!UICommands.ContainsKey(player.userID)) { UICommands.Add(player.userID, new UICommandsData()); }

            switch (Identifier)
            {
                case "togglemode": {
                        CoreLevel.Toggle(player, arg.cmd.Name, null);
                    } break;

                case "noclip": {
                        if (player.IsFlying) { UICommands[player.userID].IsFlying = false; }
                        if (!player.IsFlying) { UICommands[player.userID].IsFlying = true; }
                        RunCommands(player, new string[] { "noclip" });
                        UI.Draw.NavMenu(player);
                    } break;

                case "weather": {
                        if (UICommands[player.userID].IsWeather)
                        {
                            RunCommands(player, new string[] { "admintime -1", "adminclouds -1", "adminfog -1", "adminrain -1" });
                            UICommands[player.userID].IsWeather = false;
                        }
                        else
                        {
                            RunCommands(player, new string[] { "admintime 12", "adminclouds 0", "adminfog 0", "adminrain 0" });
                            UICommands[player.userID].IsWeather = true;
                        }
                        UI.Draw.NavMenu(player);
                    } break;

                case "adminradar": {
                        if (PluginInstalled(AdminRadar))
                        {
                            if (!Mode.SETTINGS.OnAdmin.Plugins.Blocked.All && !Mode.SETTINGS.OnAdmin.Plugins.Blocked.Specfic.AdminRadar)
                            {
                                bool isRadar = AdminRadar.Call<bool>("IsRadar", player.UserIDString);
                                if (isRadar)
                                {
                                    RunCommands(player, new string[] { "/radar" });
                                    UICommands[player.userID].IsRadar = false;
                                }
                                else
                                {
                                    RunCommands(player, new string[] { "/radar" });
                                    UICommands[player.userID].IsRadar = true;
                                }
                                UI.Draw.NavMenu(player);
                            }
                            else
                            {
                                string data = FormatData(new FormattedData
                                {
                                    Plugin = "AdminRadar"
                                }, LangMessages.NoPluginUsage);
                                string message = ColorSpecialText(data, SignatureColor);
                                XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                            }
                        }
                        else
                        {
                            string data = FormatData(new FormattedData
                            {
                                Plugin = "AdminRadar"
                            }, LangMessages.PluginNoInstalled);
                            string message = ColorSpecialText(data, SignatureColor);
                            XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                        }
                    } break;

                case "vanish": {
                        if (PluginInstalled(Vanish))
                        {
                            if (LevelHelper.IsAdmin(player))
                            {
                                if (!Mode.SETTINGS.OnAdmin.Plugins.Blocked.All && !Mode.SETTINGS.OnAdmin.Plugins.Blocked.Specfic.Vanish)
                                {
                                    bool Invisible = Vanish.Call<bool>("IsInvisible", player);
                                    if (Invisible)
                                    {
                                        player.Command("vanish");
                                        UICommands[player.userID].IsVanish = false;
                                    }
                                    if (!Invisible)
                                    {
                                        player.Command("vanish");
                                        UICommands[player.userID].IsVanish = true;
                                    }
                                    UI.Draw.NavMenu(player);
                                }
                                else
                                {
                                    string data = FormatData(new FormattedData
                                    {
                                        Plugin = "Vanish"
                                    }, LangMessages.NoPluginUsage);
                                    string message = ColorSpecialText(data, SignatureColor);
                                    XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                                }
                            }
                            else
                            {
                                if (!Mode.SETTINGS.OnPlayer.Plugins.Blocked.All && !Mode.SETTINGS.OnPlayer.Plugins.Blocked.Specfic.Vanish)
                                {
                                    bool Invisible = Vanish.Call<bool>("IsInvisible", player);
                                    if (Invisible)
                                    {
                                        player.Command("vanish");
                                        UICommands[player.userID].IsVanish = false;
                                    }
                                    if (!Invisible)
                                    {
                                        player.Command("vanish");
                                        UICommands[player.userID].IsVanish = true;
                                    }
                                    UI.Draw.NavMenu(player);
                                }
                                else
                                {
                                    string data = FormatData(new FormattedData
                                    {
                                        Plugin = "Vanish"
                                    }, LangMessages.NoPluginUsage);
                                    string message = ColorSpecialText(data, SignatureColor);
                                    XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                                }
                            }
                        }
                        else
                        {
                            string data = FormatData(new FormattedData
                            {
                                Plugin = "Vanish"
                            }, LangMessages.PluginNoInstalled);
                            string message = ColorSpecialText(data, SignatureColor);
                            XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                        }
                    } break;

                case "godmode": {
                        if (PluginInstalled(Godmode))
                        {
                            if (!Mode.SETTINGS.OnAdmin.Plugins.Blocked.All && !Mode.SETTINGS.OnAdmin.Plugins.Blocked.Specfic.Godmode)
                            {
                                bool isGod = Godmode.Call<bool>("IsGod", player.UserIDString);
                                if (isGod)
                                {
                                    RunCommands(player, new string[] { "/god" });
                                    UICommands[player.userID].IsGodmode = false;
                                }
                                else
                                {
                                    RunCommands(player, new string[] { "/god" });
                                    UICommands[player.userID].IsGodmode = true;
                                }
                                UI.Draw.NavMenu(player);
                            }
                            else
                            {
                                string data = FormatData(new FormattedData
                                {
                                    Plugin = "GodMode"
                                }, LangMessages.NoPluginUsage);
                                string message = ColorSpecialText(data, SignatureColor);
                                XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                            }
                        }
                        else
                        {
                            string data = FormatData(new FormattedData
                            {
                                Plugin = "GodMode"
                            }, LangMessages.PluginNoInstalled);
                            string message = ColorSpecialText(data, SignatureColor);
                            XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                        }
                    } break;
            }
        }
        #endregion UI Commands

        #region Helpers
        /*Handle Ban & Kicks Etc*/
        private void HandleBan(ulong id, string reason)
        {
            DebugLog(DebugType.Method);

            var player = BasePlayer.allPlayerList.Where(x => x.userID == id).FirstOrDefault();
            var Mode = LevelHelper.Get(player);
            if (Mode != null && Mode.SETTINGS.OnAdmin.IgnoreViolations)
            {
                XLIB.PLAYER.AuthLevel.Set(player, XLIB.PLAYER.AuthLevel.LevelType.Admin);

                string data = FormatData(new FormattedData
                {
                    Command = reason
                }, LangMessages.ViolationBannable);
                string message = ColorSpecialText(data, SignatureColor);
                XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));



                XLIB.PLAYER.AuthLevel.LevelType switchingLevel;
                var IsSwitching = LevelHelper.IsSwitching(player.userID, out switchingLevel);

                /*Credit: UIChamp*/
                ServerMgr.Instance.Invoke(() =>
                {
                    if (!IsSwitching)
                    {
                        CoreLevel.Set(player, null, null, XLIB.PLAYER.AuthLevel.LevelType.Player, null, null, true);
                    }
                }, 0.1f);
            }     
        }


        private enum DebugType
        {
            Method,
            Action,
            All,
            Disabled
        }
        private void DebugLog(DebugType type, [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string caller = null)
        {
            if (DEBUG == DebugType.Disabled) { return; }

            string message = $"[{type}] | {DateTime.Now} | {lineNumber.ToString().PadRight(FileLineLength)} | {caller}";

            if ((DEBUG == DebugType.All || DEBUG == DebugType.Method) && type == DebugType.Method) { PrintWarning(message); }
            if ((DEBUG == DebugType.All || DEBUG == DebugType.Action) && type == DebugType.Action) { Puts(message); }

            LogToFile("LOG", message, this, false);
        }

        /// <returns>
        /// TRUE IF NOT <paramref name="command"/> IS BLOCKED BY <see cref="LevelHelper.Get"/>
        /// </returns>
        private static bool HandleCommands(BasePlayer player, string command)
        {
            Instance?.DebugLog(DebugType.Method);

            if (player != null && player.userID.IsSteamId())
            {
                var Mode = LevelHelper.Get(player);
                if (Mode != null)
                {
                    //Overrides if command is Mode command 
                    if (LevelHelper.GetModes(player).Any(mode => mode.Commands.Any(cmd => cmd == command))) { return true; }

                    if (LevelHelper.IsAdmin(player))
                    {
                        if (Mode.IsMaster) { return true; }

                        bool IsBlocked = Mode.SETTINGS.OnAdmin.BlockedCommands.Any(x => !string.IsNullOrEmpty(x) && command.Contains(x));
                        if (Mode.SETTINGS.OnAdmin.BlockedCommands.Length > 0)
                        {
                            if (IsBlocked) { return false; }
                            if (command.Contains("vanish") && (Mode.SETTINGS.OnAdmin.Plugins.Blocked.All || Mode.SETTINGS.OnAdmin.Plugins.Blocked.Specfic.Vanish)) { return false; }
                            if (command.Contains("radar") && (Mode.SETTINGS.OnAdmin.Plugins.Blocked.All || Mode.SETTINGS.OnAdmin.Plugins.Blocked.Specfic.AdminRadar)) { return false; }
                            if (command.Contains("god") && (Mode.SETTINGS.OnAdmin.Plugins.Blocked.All || Mode.SETTINGS.OnAdmin.Plugins.Blocked.Specfic.Godmode)) { return false; }
                            if (command.Contains("backpack") && (Mode.SETTINGS.OnAdmin.Plugins.Blocked.All || Mode.SETTINGS.OnAdmin.Plugins.Blocked.Specfic.Backpacks)) { return false; }
                        }
                    }
                    else
                    {

                        bool IsBlocked = Mode.SETTINGS.OnPlayer.BlockedCommands.Any(x => !string.IsNullOrEmpty(x) && command.Contains(x)); if (Mode.SETTINGS.OnPlayer.BlockedCommands.Length > 0)
                        {
                            if (IsBlocked) { return false; }
                            if (command.Contains("vanish") && (Mode.SETTINGS.OnPlayer.Plugins.Blocked.All || Mode.SETTINGS.OnPlayer.Plugins.Blocked.Specfic.Vanish)) { return false; }
                            if (command.Contains("radar") && (Mode.SETTINGS.OnPlayer.Plugins.Blocked.All || Mode.SETTINGS.OnPlayer.Plugins.Blocked.Specfic.AdminRadar)) { return false; }
                            if (command.Contains("god") && (Mode.SETTINGS.OnPlayer.Plugins.Blocked.All || Mode.SETTINGS.OnPlayer.Plugins.Blocked.Specfic.Godmode)) { return false; }
                        }
                    }
                }
            }
            return true;
        }

        private static bool PluginInstalled(Core.Plugins.Plugin plugin)
        {
            Instance?.DebugLog(DebugType.Method);

            return (plugin != null);
        }

        //Sends Discord Embed if enabled by Mode
        private static void SendDiscordEmbed(XLIB.PLAYER.AuthLevel.LevelType authLevel, string reason, BasePlayer player, BasePlayer target = null)
        {
            Instance?.DebugLog(DebugType.Method);

            var Mode = LevelHelper.Get(player);
            if (Mode == null) { return; }

            string playerTarget = (player.userID == (target?.userID ?? 0)) ? $"{target?.displayName} (Forced)" : $"{player?.displayName} (Self)";
            var Reason = string.IsNullOrEmpty(reason) ? "None" : reason;
            long EmbedColor =
                LevelHelper.IsAdmin(player) ? Mode.SETTINGS.OnAdmin.Notification.Discord.Settings.WebhookColor :
                Mode.SETTINGS.OnPlayer.Notification.Discord.Settings.WebhookColor;
            XLIB.STEAM.WebData.SetProfile(player.userID);
            var profile = XLIB.STEAM.WebData.GetProfile(player.userID);
            string body = "{\"content\":null,\"embeds\":[{\"color\":" + EmbedColor + ",\"fields\":[{\"name\":\"**Toggle Information** <:AdminToggle:987073714514432030>\",\"value\":\">>> Type: **" + authLevel + "**\\nReason: **" + Reason + "**\\nPermission: **" + Mode.Permission + "**\\nPriority: **" + Mode.Priority + "**\\nTarget: **" + playerTarget + "**\"}],\"author\":{\"name\":\"" + player.displayName + " (" + player.UserIDString + ")\",\"url\":\"https://steamcommunity.com/profiles/" + player.UserIDString + "\",\"icon_url\":\"" + profile?.Picture + "\"},\"footer\":{\"text\":\"Server: " + ConVar.Server.hostname + "\"}}],\"username\":\"AdminToggle V3\",\"attachments\":[]}";

            if (LevelHelper.IsAdmin(player) && Mode.SETTINGS.OnAdmin.Notification.Discord.Enabled)
            {
                XLIB.NOTIFICATION.Discord.SendEmbed(Mode.SETTINGS.OnAdmin.Notification.Discord.Settings.Webhook, body);
            }
            else if (Mode.SETTINGS.OnPlayer.Notification.Discord.Enabled)
            {
                XLIB.NOTIFICATION.Discord.SendEmbed(Mode.SETTINGS.OnPlayer.Notification.Discord.Settings.Webhook, body);
            }
        }

        private enum PluginState
        {
            On,
            Off
        }
        //Toggles Plugin (On IF Autorun admin & Not blocked)
        private static void TogglePlugin(BasePlayer player, Core.Plugins.Plugin plugin, PluginState state, bool OnRevoke = false)
        {
            Instance?.DebugLog(DebugType.Method);

            if (!UICommands.ContainsKey(player.userID)) { UICommands.Add(player.userID, new UICommandsData()); }

            var Mode = !OnRevoke ? LevelHelper.Get(player) : null;

            if (PluginInstalled(plugin) && Mode != null)
            {
                string PluginName = plugin.Name.ToLower();

                bool aAutoRun = Mode.SETTINGS.OnAdmin.Plugins.Autorun.All; /*Admin AutoRun*/
                bool aBlock = Mode.SETTINGS.OnAdmin.Plugins.Blocked.All; /*Admin Blocked*/

                bool pBlock = Mode.SETTINGS.OnPlayer.Plugins.Blocked.All; /*Player Blocked*/

                switch (state)
                {
                    case PluginState.On: {
                            if (LevelHelper.IsAdmin(player))
                            {
                                if (Mode.IsMaster)
                                {
                                    if (PluginName == "vanish" && (aAutoRun || Mode.SETTINGS.OnAdmin.Plugins.Autorun.Specfic.Vanish))
                                    { plugin.Call("Disappear", player); UICommands[player.userID].IsVanish = true; }

                                    if (PluginName == "adminradar" && (aAutoRun || Mode.SETTINGS.OnAdmin.Plugins.Autorun.Specfic.AdminRadar) && !plugin.Call<bool>("IsRadar", player.UserIDString))
                                    { player.Command("radar"); UICommands[player.userID].IsRadar = true; }

                                    if (PluginName == "godmode" && (aAutoRun || Mode.SETTINGS.OnAdmin.Plugins.Autorun.Specfic.Godmode) && !plugin.Call<bool>("IsGod", player.UserIDString))
                                    { player.Command("god"); UICommands[player.userID].IsGodmode = true; }
                                }
                                else
                                {
                                    if (PluginName == "vanish" && (!aBlock && !Mode.SETTINGS.OnAdmin.Plugins.Blocked.Specfic.Vanish) && (aAutoRun || Mode.SETTINGS.OnAdmin.Plugins.Autorun.Specfic.Vanish))
                                    { plugin.Call("Disappear", player); UICommands[player.userID].IsVanish = true; }

                                    if (PluginName == "adminradar" && (!aBlock && !Mode.SETTINGS.OnAdmin.Plugins.Blocked.Specfic.AdminRadar) && (aAutoRun || Mode.SETTINGS.OnAdmin.Plugins.Autorun.Specfic.AdminRadar) && !plugin.Call<bool>("IsRadar", player.UserIDString))
                                    { player.Command("radar"); UICommands[player.userID].IsRadar = true; }

                                    if (PluginName == "godmode" && (!aBlock && !Mode.SETTINGS.OnAdmin.Plugins.Blocked.Specfic.Godmode) && (aAutoRun || Mode.SETTINGS.OnAdmin.Plugins.Autorun.Specfic.Godmode) && !plugin.Call<bool>("IsGod", player.UserIDString))
                                    { player.Command("god"); UICommands[player.userID].IsGodmode = true; }
                                }
                            }
                            else
                            {
                                if (PluginName == "vanish" && (!pBlock && !Mode.SETTINGS.OnPlayer.Plugins.Blocked.Specfic.Vanish))
                                { plugin.Call("Disappear", player); UICommands[player.userID].IsVanish = true; }

                                if (PluginName == "adminradar" && (!pBlock && !Mode.SETTINGS.OnPlayer.Plugins.Blocked.Specfic.AdminRadar) && !plugin.Call<bool>("IsRadar", player.UserIDString))
                                { player.Command("radar"); UICommands[player.userID].IsRadar = true; }

                                if (PluginName == "godmode" && (!pBlock && !Mode.SETTINGS.OnPlayer.Plugins.Blocked.Specfic.Godmode) && !plugin.Call<bool>("IsGod", player.UserIDString))
                                { player.Command("god"); UICommands[player.userID].IsGodmode = true; }
                            }
                        } break;
                    case PluginState.Off: {
                            if (PluginName == "vanish") { plugin.Call("Reappear", player); UICommands[player.userID].IsVanish = false; }
                            if (PluginName == "adminradar" && plugin.Call<bool>("IsRadar", player.UserIDString)) { player.Command("radar"); UICommands[player.userID].IsRadar = false; }
                            if (PluginName == "godmode" && plugin.Call<bool>("IsGod", player.UserIDString)) { player.Command("god"); UICommands[player.userID].IsGodmode = false; }
                        } break;
                }
            }
        }

        //Runs Chat-Commands Or Console-Commands If not blocked by Mode
        private static void RunCommands(BasePlayer player, string[] commands)
        {
            Instance?.DebugLog(DebugType.Method);

            var Mode = LevelHelper.Get(player);
            if (Mode == null) { return; }


            if (LevelHelper.IsAdmin(player))
            {
                if (Mode.IsMaster)
                {
                    /*Master Ignore Blocked Commands*/
                    foreach (var command in commands)
                    {
                        if (command.StartsWith("/")) { player.SendConsoleCommand($"chat.say {command}"); }
                        else { player.SendConsoleCommand(command); }
                    }
                }
                else
                {
                    /*Follow Admin Blocked Commands*/
                    foreach (var command in commands)
                    {
                        if (Mode.SETTINGS.OnAdmin.BlockedCommands.Any(x => x == command))
                        {
                            string data = FormatData(new FormattedData
                            {
                                Command = command
                            }, LangMessages.RestrictedCommand);
                            string message = ColorSpecialText(data, SignatureColor);
                            XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                            
                            continue;
                        }

                        if (command.StartsWith("/")) { player.SendConsoleCommand($"chat.say {command}"); }
                        else { player.SendConsoleCommand(command); }
                    }
                }
            }
            else
            {
                /*Follow Player Blocked Commands*/
                foreach (var command in commands)
                {
                    if (Mode.SETTINGS.OnPlayer.BlockedCommands.Any(x => x == command))
                    {

                        string data = FormatData(new FormattedData
                        {
                            Command = command
                        }, LangMessages.RestrictedCommand);
                        string message = ColorSpecialText(data, SignatureColor);
                        XLIB.NOTIFICATION.Server.Self.SendMessage(player, FormatMessage(message));
                        
                        continue;
                    }

                    if (command.StartsWith("/")) { player.SendConsoleCommand($"chat.say {command}"); }
                    else { player.SendConsoleCommand(command); }
                }
            }
        }

        //Colors text regex pattern %between%
        private static string ColorSpecialText(string message, string HexColor)
        {
            Instance?.DebugLog(DebugType.Method);

            Regex specialTextPattern = new Regex("%(.*?)|($\\b*?)%");
            string[] arrayMessage = new string[message.Split(' ').Length];
            for (int i = 0; i < message.Split(' ').Length; i++)
            {
                var word = message.Split(' ')[i];
                string arrayWord = "";
                if (specialTextPattern.IsMatch(word)) { arrayWord = $"<color=#{HexColor.Replace("#", "")}>{word.Replace("%", "")}</color>"; }
                else { arrayWord = word; }
                arrayMessage[i] = arrayWord;
            }
            return string.Join(" ", arrayMessage);
        }
        private static string FormatMessage(string message)
        {
            Instance?.DebugLog(DebugType.Method);

            return $"{XLIB.DATA.String.TextEncodeing(16, "#ffffff", "[")}" +
                $"{XLIB.DATA.String.TextEncodeing(16, SignatureColor, Instance.Name, true)}" +
                $"{XLIB.DATA.String.TextEncodeing(16, "#ffffff", "]")}" +
                $" {XLIB.DATA.String.TextEncodeing(16, "#ffffff", message)}";
        }
        private static string FormatData(FormattedData Data, string Input)
        {
            Instance?.DebugLog(DebugType.Method);

            StringBuilder sb = new StringBuilder(Input);

            try { sb.Replace("{player.name}", Data.Player.displayName); } catch { }
            try { sb.Replace("{player.id}", Data.Player.UserIDString); } catch { }
            try { sb.Replace("{mode.permission}", Data.TargetMode.Permission); } catch { }
            try { sb.Replace("{mode.priority}", Data.TargetMode.Priority.ToString()); } catch { }
            try { sb.Replace("{server.name}", ConVar.Server.hostname); } catch { }
            try { sb.Replace("{violation.name}", Data.Violation.ToString()); } catch { }
            try { sb.Replace("{command.name}", Data.Command); } catch { }
            try { sb.Replace("{plugin.name}", Data.Plugin); } catch { }
            try { sb.Replace("{arg}", Data.String); } catch { }

            return sb.ToString();
        }
        private class FormattedData
        {
            internal ConfigData.Modes TargetMode { get; set; } = null;
            internal BasePlayer Player { get; set; } = null;
            internal AntiHackType Violation { get; set; } = AntiHackType.None;
            internal string Command { get; set; } = "";
            internal string String { get; set; } = "";
            internal string Plugin { get; set; } = "";
        }

        private static Dictionary<ulong, string> NamePrefix = new Dictionary<ulong, string>();
        private static void RenamePlayer(BasePlayer player, string name, bool Revert = false)
        {
            if (!NamePrefix.ContainsKey(player.userID)) { NamePrefix.Add(player.userID, player.displayName); }
            if(Revert && NamePrefix.ContainsKey(player.userID)) 
            { 
                player.IPlayer.Rename(NamePrefix[player.userID]);
                return;
            }
            player.IPlayer.Rename(name);
        }
        #endregion Helpers

        #region API Hooks Fields
        private const string API_onAdmin = constPluginName + "_onAdmin";
        private const string API_onPlayer = constPluginName + "_onPlayer";
        #endregion API Hooks Fields
        #region API Public Helpers
        /// <returns>
        /// <list type="table">Retures TRUE IF <paramref name="id"/> IS ASSIGNED ANY <see cref="ConfigData.Modes"/></list>
        /// </returns>
        public bool IsAdmin(ulong id)
        {
            DebugLog(DebugType.Method);
            
            return LevelHelper.IsAdmin(null, id);
        }

        /// <returns>
        /// <list type="table"><see cref="ConfigData.Modes.Permission"/> (<see cref="string"/>)</list> 
        /// <list type="table"><see cref="ConfigData.Modes.Priority"/> (<see cref="int"/>)</list> 
        /// <list type="table"><see cref="ConfigData.Modes.IsMaster"/> (<see cref="bool"/>)</list> 
        /// </returns>
        public object[] GetMode(BasePlayer player, bool TrueMode = false)
        {
            DebugLog(DebugType.Method);

            var Mode = LevelHelper.Get(player, TrueMode);
            if (Mode == null) { return null; }
            return new object[] { Mode.Permission, Mode.Priority, Mode.IsMaster };
        }
        #endregion API Public Helpers
    }
}
/*Imported XLIB (NO more dll file)*/
namespace XLIB
{
    public class DATA
    {
        public class String
        {
            public static string TextEncodeing(double TextSize, string HexColor, string text, bool bold = false)
            {
                var s = TextSize <= 0 ? 1 : TextSize;
                var c = HexColor.StartsWith("#") ? HexColor : $"#{HexColor}";
                if (bold) { return $"<size={s}><color={c}><b>{text}</b></color></size>"; }
                else { return $"<size={s}><color={c}>{text}</color></size>"; }
            }
        }
    }
    public class NOTIFICATION
    {
        public class Server
        {
            public class Global
            {
                public static void Send(string Message, ulong IconID = 0) => new Oxide.Game.Rust.Libraries.Server().Broadcast(Message, IconID);
            }
            public class Self
            {
                private static Dictionary<ulong, bool> ActivePopups = new Dictionary<ulong, bool>();
                public static async void SendPopup(BasePlayer player, string Message, int DurationMS)
                {
                    if (!ActivePopups.ContainsKey(player.userID)) { ActivePopups.Add(player.userID, false); }
                    if (!ActivePopups[player.userID] == true)
                    {
                        player.SendConsoleCommand($"gametip.showgametip \"{Message}\"");
                        ActivePopups[player.userID] = true;

                        await Task.Delay(DurationMS);
                        player.SendConsoleCommand("gametip.hidegametip");
                        ActivePopups[player.userID] = false;
                    }
                }
                public static void SendMessage(BasePlayer player, string Message, ulong IconID = 0) => player.SendConsoleCommand("chat.add", 0, IconID, Message);
                public static void SendEffect(BasePlayer player, string soundPrefab)
                {
                    var effect = new Effect(soundPrefab, player, 0, Vector3.zero, Vector3.forward);
                    EffectNetwork.Send(effect, player.net.connection);
                    effect.Clear();
                }
            }
        }
        public class Discord
        {
            //BROKEN
            //Error while compiling: AdminToggle.cs(3663,23): error CS0584: Internal compiler error: Assembly 'System.Net.Http, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' is a missing assembly and does not support the requested operation.
            public static async Task SendEmbed(string webHook, string body)
            {
                /*
                using (HttpClient client = new HttpClient())
                {
                    var stringContent = new StringContent(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(body)), Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(webHook, stringContent);
                };
                */
            }
        }
    }
    public class PLAYER
    {
        public class Movement
        {
            public class Utility
            {
                public static void Lock(BasePlayer player, bool FreezeKeys = true, bool FreezeMouse = false)
                {
                    var container = new CuiElementContainer();
                    var props = new UI.UIProperties
                    {
                        EnableCursor = FreezeMouse,
                        EnableKeyboard = FreezeKeys,
                    };
                    XLIB.UI.Create.Panel(ref container, new UI.UIPoint.Anchor(), new UI.UIColor(), props, "Under", "freeze.movement.element");
                    CuiHelper.AddUi(player, container);
                }
                public static void Unlock(BasePlayer player) => CuiHelper.DestroyUi(player, "freeze.movement.element");
            }
        }
        public class Location
        {
            public class Utility
            {
                public static void Teleport(BasePlayer player, Vector3 location) => player.Teleport(location);

                private static Dictionary<ulong, FrozenData> FrozenPlayers = new Dictionary<ulong, FrozenData>();
                private class FrozenData
                {
                    public bool IsMounted { get; set; } = false;
                    public BaseEntity Entity { get; set; } = null;
                }

                public static bool IsFrozen(BasePlayer player)
                {
                    if (!FrozenPlayers.ContainsKey(player.userID)) { return false; }
                    return FrozenPlayers[player.userID].IsMounted;
                }

                public static void MountChair(BasePlayer player, bool LockMovement = false)
                {
                    UnMountChair(player);
                    if (LockMovement) { Movement.Utility.Lock(player, true, true); }
                    if (!FrozenPlayers.ContainsKey(player.userID)) { FrozenPlayers.Add(player.userID, new FrozenData()); }

                    const string prefab = "assets/bundled/prefabs/static/chair.invisible.static.prefab";
                    const float PlayerHeight = 0.19f;

                    Vector3 pos = new Vector3(player.transform.position.x, (player.transform.position.y + PlayerHeight), player.transform.position.z);
                    Quaternion rot = Quaternion.Euler(0, player.GetNetworkRotation().eulerAngles.y, 0);
                    Quaternion lookRot = new Quaternion(0, 100, 0, 0);

                    player.transform.SetPositionAndRotation(player.transform.position, lookRot);

                    BaseEntity MountOjb = GameManager.server.CreateEntity(prefab, pos, rot);
                    MountOjb.Spawn();
                    FrozenPlayers[player.userID].Entity = MountOjb;

                    player.SetMounted((BaseMountable)MountOjb);
                    FrozenPlayers[player.userID].IsMounted = true;
                }
                public static void UnMountChair(BasePlayer player)
                {
                    if (!FrozenPlayers.ContainsKey(player.userID)) { return; }

                    Movement.Utility.Unlock(player);
                    FrozenPlayers[player.userID].Entity.Kill();
                    FrozenPlayers[player.userID].IsMounted = false;
                    player.DismountObject(); player.EnsureDismounted();
                    FrozenPlayers.Remove(player.userID);
                }
            }
            public class Storage
            {
                /// <summary>
                /// <list type="table"><see cref="ulong"/> Is Key</list>
                /// <list type="table"><see cref="string"/> Is Value Identifier</list>
                /// <list type="table"><see cref="ItemData[]"/> Is Value Data</list>
                /// </summary>
                private static Dictionary<ulong, Dictionary<string, Vector3>> PlayerLocations = new Dictionary<ulong, Dictionary<string, Vector3>>();
                private static bool ContainsKeyValue(ulong KEY1, string KEY2, bool Create = false)
                {
                    if (Create)
                    {
                        if (!PlayerLocations.ContainsKey(KEY1)) { PlayerLocations.Add(KEY1, new Dictionary<string, Vector3>()); }
                        if (PlayerLocations.ContainsKey(KEY1) && !PlayerLocations[KEY1].ContainsKey(KEY2))
                        {
                            PlayerLocations[KEY1].Add(KEY2, Vector3.zero);
                            return false;
                        }
                    }
                    if (!PlayerLocations.ContainsKey(KEY1)) { return false; }
                    if (PlayerLocations.ContainsKey(KEY1) && !PlayerLocations[KEY1].ContainsKey(KEY2)) { return false; }
                    return true;
                }
                public static Vector3 GetKey(ulong playerKey, string locationKey)
                {
                    if (!PlayerLocations.ContainsKey(playerKey)) { return Vector3.zero; }
                    if (PlayerLocations.ContainsKey(playerKey) && !PlayerLocations[playerKey].ContainsKey(locationKey)) { return Vector3.zero; }
                    return PlayerLocations[playerKey][locationKey];
                }

                public static void RemoveKey(ulong SteamID, string LocationKey)
                {
                    if (ContainsKeyValue(SteamID, LocationKey))
                    {
                        PlayerLocations[SteamID].Remove(LocationKey);
                    }
                }
                public static void Set(BasePlayer player, string LocationKey)
                {
                    ContainsKeyValue(player.userID, LocationKey, true);
                    PlayerLocations[player.userID][LocationKey] = player.transform.localPosition;
                }
                public static void Teleport(BasePlayer player, string LocationKey, bool RemoveKeyAfterUse = false)
                {
                    if (ContainsKeyValue(player.userID, LocationKey))
                    {
                        player.Teleport(PlayerLocations[player.userID][LocationKey]);
                        if (RemoveKeyAfterUse) { RemoveKey(player.userID, LocationKey); }
                    }
                }
            }
        }
        public class Inventory
        {
            public class Utility
            {
                public static Item CreateItem(string ShortName, int Quantity = 1, ulong SkinID = 0) => ItemManager.CreateByName(ShortName, Quantity, SkinID);
                public static void GiveItem(BasePlayer player, Item item, ItemContainer container, int Position = 0)
                {
                    if (item != null)
                        player.inventory.GiveItem(item, container); item.MoveToContainer(container, Position);
                }
                public static void DestroyItems(Item[] items)
                {
                    foreach (var item in items) { item.Remove(); }
                }
                public static void Lock(BasePlayer player)
                {
                    LockContainer(player.inventory.containerBelt);
                    LockContainer(player.inventory.containerMain);
                    LockContainer(player.inventory.containerWear);
                }
                public static void Unlock(BasePlayer player)
                {
                    UnlockContainer(player.inventory.containerBelt);
                    UnlockContainer(player.inventory.containerMain);
                    UnlockContainer(player.inventory.containerWear);
                }
                public static void LockContainer(ItemContainer container) => container.SetLocked(true);
                public static void UnlockContainer(ItemContainer container) => container.SetLocked(false);
            }

            public class Storage
            {
                /// <summary>
                /// <list type="table"><see cref="ulong"/> Is Key</list>
                /// <list type="table"><see cref="string"/> Is Value Identifier</list>
                /// <list type="table"><see cref="ItemData[]"/> Is Value Data</list>
                /// </summary>
                private static Dictionary<ulong, Dictionary<string, ItemData[]>> PlayerInventories = new Dictionary<ulong, Dictionary<string, ItemData[]>>();

                private static bool ContainsKeyValue(ulong KEY1, string KEY2, bool Create = false)
                {
                    if (Create)
                    {
                        if (!PlayerInventories.ContainsKey(KEY1)) { PlayerInventories.Add(KEY1, new Dictionary<string, ItemData[]>()); }
                        if (PlayerInventories.ContainsKey(KEY1) && !PlayerInventories[KEY1].ContainsKey(KEY2))
                        {
                            PlayerInventories[KEY1].Add(KEY2, null);
                            return false;
                        }
                    }
                    if (!PlayerInventories.ContainsKey(KEY1)) { return false; }
                    if (PlayerInventories.ContainsKey(KEY1) && !PlayerInventories[KEY1].ContainsKey(KEY2)) { return false; }
                    return true;
                }

                public class ItemData
                {
                    public ItemContainer Container;
                    public string displayName;
                    public int ID;
                    public bool IsBlueprint;
                    public int Position;
                    public int Quantity;
                    public int BlueprintTarget;
                    public float Condition;
                    public float MaxCondition;
                    public ulong Skin;
                    public int AmmoType;
                    public int Ammo;
                    public int FlameFuel;
                    public string Text;
                    public float Fuel;
                    public ItemData[] Contents;
                }
                public static Item ToItem(ItemData i)
                {
                    Item item = ItemManager.CreateByItemID(i.ID, i.Quantity, i.Skin);
                    item.name = i.displayName == null ? item.name /*Default*/ : i.displayName /*Changed Name*/;
                    item.text = !string.IsNullOrEmpty(i.Text) ? i.Text : "";

                    var magazine = (item?.GetHeldEntity() as BaseProjectile)?.primaryMagazine ?? null;
                    var flameThrower = (item?.GetHeldEntity() as FlameThrower) ?? null;

                    if (i.IsBlueprint)
                    {
                        item.blueprintTarget = i.BlueprintTarget;
                        return item;
                    }

                    /*Speical Cases*/
                    if (magazine != null)
                    {
                        magazine.contents = i.Ammo;
                        magazine.ammoType = ItemManager.FindItemDefinition(i.AmmoType);
                    }
                    if (flameThrower != null)
                    {
                        flameThrower.ammo = i.FlameFuel;
                    }
                    item.position = i.Position;
                    item.condition = i.Condition;
                    item.maxCondition = i.MaxCondition;
                    item.fuel = i.Fuel;

                    if (i.Contents != null && i.Contents.Length > 0)
                    {
                        foreach (var ci in i.Contents) { ToItem(ci).MoveToContainer(item.contents); }
                    }
                    return item;
                }
                public static ItemData FromItem(Item item) => new ItemData
                {
                    ID = item.info.itemid,
                    displayName = item.name != ItemManager.FindItemDefinition(item.info.itemid).name ? item.name /*Name Changed*/ : null /*Default*/,
                    Container = item.GetRootContainer(),
                    Position = item.position,
                    Ammo = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.contents ?? 0,
                    AmmoType = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.ammoType.itemid ?? 0,
                    FlameFuel = (item.GetHeldEntity() as FlameThrower)?.ammo ?? 0,
                    Quantity = item.amount,
                    Condition = item.condition,
                    MaxCondition = item.maxCondition,
                    Fuel = item.fuel,
                    BlueprintTarget = item.blueprintTarget,
                    IsBlueprint = item.IsBlueprint(),
                    Skin = item.skin,
                    Text = item.text,
                    Contents = item.contents?.itemList?.Select(FromItem).ToArray(),
                };

                public static void Save(BasePlayer player, string InventoryKey, bool ClearInventoryAfterSave = false)
                {
                    ContainsKeyValue(player.userID, InventoryKey, true);
                    List<Item> Items = new List<Item>();
                    player.inventory.GetAllItems(Items);
                    PlayerInventories[player.userID][InventoryKey] = Items.Where(x => x != null).Select(i => FromItem(i)).ToArray();
                    if (ClearInventoryAfterSave) { player.inventory.Strip(); }
                }
                public static void Restore(BasePlayer player, string InventoryKey, bool RemoveKeyAfterUse = false, bool ClearInventoryBeforeRestore = false)
                {
                    if (ContainsKeyValue(player.userID, InventoryKey))
                    {
                        if (ClearInventoryBeforeRestore) { player.inventory.Strip(); }
                        foreach (var i in PlayerInventories[player.userID][InventoryKey])
                        {
                            var item = ToItem(i);
                            item.MoveToContainer(i.Container, i.Position);
                        }
                        if (RemoveKeyAfterUse) { PlayerInventories[player.userID].Remove(InventoryKey); }
                    }
                }
                public static void RemoveKey(ulong SteamID, string LocationKey)
                {
                    if (ContainsKeyValue(SteamID, LocationKey)) { PlayerInventories[SteamID].Remove(LocationKey); }
                }
            }
        }
        public class AuthLevel
        {
            public static LevelType Get(int authLevel) =>
                (authLevel == 2 ? LevelType.Admin : authLevel == 1 ? LevelType.Moderator : LevelType.Admin);


            public enum LevelType
            {
                Admin = 2,
                Moderator = 1,
                Player = 0,
            }
            public static void Set(BasePlayer player, LevelType type)
            {
                switch (type)
                {
                    case LevelType.Admin:
                        ServerUsers.Set(player.userID, ServerUsers.UserGroup.Owner, player.displayName, "");
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                        player.Connection.authLevel = 2;
                        ServerUsers.Save();
                        break;

                    case LevelType.Moderator:
                        ServerUsers.Set(player.userID, ServerUsers.UserGroup.Moderator, player.displayName, "");
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                        player.Connection.authLevel = 1;
                        ServerUsers.Save();
                        break;

                    case LevelType.Player:
                        ServerUsers.Set(player.userID, ServerUsers.UserGroup.None, player.displayName, "");
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                        player.Connection.authLevel = 0;
                        ServerUsers.Save();
                        break;
                }
            }
        }
    }
    public class STEAM
    {
        public class WebData
        {
            private static Dictionary<ulong, ProfileData> Profiles = new Dictionary<ulong, ProfileData>();
            public class ProfileData
            {
                public string Name { get; set; } = "";
                public string Picture { get; set; } = "";
            }

            public static void RemoveKey(ulong SteamID)
            {
                if (Profiles.ContainsKey(SteamID))
                {
                    Profiles.Remove(SteamID);
                }
            }


            public static ProfileData GetProfile(ulong SteamID)
            {
                if (Profiles.ContainsKey(SteamID))
                {
                    return Profiles[SteamID];
                }
                return null;
            }

            public static async Task SetProfile(ulong SteamID)
            {

                //BROKEN
                //Error while compiling: AdminToggle.cs(3663,23): error CS0584: Internal compiler error: Assembly 'System.Net.Http, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' is a missing assembly and does not support the requested operation.
                /*
                using (HttpClient client = new HttpClient())
                {
                    var URL = $"https://steamcommunity.com/profiles/{SteamID}/?xml=1";
                    HttpResponseMessage response = await client.GetAsync(URL);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string result = await response.Content.ReadAsStringAsync();

                        if (!Profiles.ContainsKey(SteamID)) { Profiles.Add(SteamID, new ProfileData()); }
                        Profiles[SteamID].Name = GetName(result);
                        Profiles[SteamID].Picture = GetPicture(result);
                    }
                };
                */
            }


            private static string GetName(string data) => Get(data, "steamID");
            private static string GetPicture(string data) => Get(data, "avatarFull");
            private static string Get(string data, string tag)
            {
                var startTag = "<" + tag + ">";
                int startIndex = data.IndexOf(startTag) + startTag.Length;
                int endIndex = data.IndexOf("</" + tag + ">", startIndex);
                return data.Substring(startIndex, endIndex - startIndex).Replace("<![CDATA[", "").Replace("]]>", "");
            }
        }
    }
    public class UI
    {
        #region Helpers
        public class UIColor
        {
            /// <summary>
            /// Hex Color
            /// </summary>
            public string color { get; set; } = "#ffffff";
            /// <summary>
            /// Value in (%) Max 1.0
            /// </summary>
            public double alpha { get; set; } = 1.0;


            /// <summary>
            /// Converts <see cref="UIColor.color"/> and <see cref="UIColor.alpha"/> to <see cref="string"/> example "1 0 0 1" Red 100 % alpha
            /// </summary>
            /// <returns></returns>
            internal static string HexColor(UIColor color)
            {
                var c = color.color.Replace("#", "");
                var a = color.alpha < 0.0 ? 0.0 : color.alpha > 1.0 ? 1.0 : color.alpha;
                int red = int.Parse(c.Substring(0, 2), NumberStyles.HexNumber);
                int green = int.Parse(c.Substring(2, 2), NumberStyles.HexNumber);
                int blue = int.Parse(c.Substring(4, 2), NumberStyles.HexNumber);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {a}";
            }
        }
        public struct UIPoint
        {
            /*
             * AnchorMin "MinWidth (%), MinHeight (%)"
             * AnchorMax "MaxWidth (%), MaxHeight (%)"
             * 
             * OffsetMin "MinWidth (Pixel), MinHeight (Pixel)"
             * OffsetMax "MaxWidth (Pixel), MaxHeight (Pixel)"
            */
            public struct Anchor
            {
                /// <summary>
                /// Value in (%) Max 1.0
                /// </summary>
                public double Min_Width, Min_Height, Max_Width, Max_Height, Left, Right, Bottom, Top;
                public static string Min(UIPoint.Anchor point)
                {
                    double Width =
                        (point.Min_Width + point.Left - point.Right) > 1.0 ? 1.0 :
                        (point.Min_Width + point.Left - point.Right) < 0.0 ? 0.0 :
                        (point.Min_Width + point.Left - point.Right);

                    double Height =
                        (point.Min_Height + point.Bottom - point.Top) > 1.0 ? 1.0 :
                        (point.Min_Height + point.Bottom - point.Top) < 0.0 ? 0.0 :
                        (point.Min_Height + point.Bottom - point.Top);
                    return $"{Width} {Height}";
                }
                public static string Max(UIPoint.Anchor point)
                {
                    double Width =
                        (point.Max_Width + point.Left - point.Right) > 1.0 ? 1.0 :
                        (point.Max_Width + point.Left - point.Right) < 0.0 ? 0.0 :
                        (point.Max_Width + point.Left - point.Right);

                    double Height =
                        (point.Max_Height + point.Bottom - point.Top) > 1.0 ? 1.0 :
                        (point.Max_Height + point.Bottom - point.Top) < 0.0 ? 0.0 :
                        (point.Max_Height + point.Bottom - point.Top);
                    return $"{Width} {Height}";
                }
            }
            public struct Offset
            {
                /// <summary>
                /// Value in (Pixel)
                /// </summary>
                public double Min_Width, Min_Height, Max_Width, Max_Height, Left, Right, Bottom, Top;
                public static string Min(UIPoint.Offset point)
                {
                    double Width = (point.Min_Width + point.Left - point.Right);
                    double Height = (point.Min_Height + point.Bottom - point.Top);
                    return $"{Width} {Height}";
                }
                public static string Max(UIPoint.Offset point)
                {
                    double Width = (point.Max_Width + point.Left - point.Right);
                    double Height = (point.Max_Height + point.Bottom - point.Top);
                    return $"{Width} {Height}";
                }
            }
        }
        #endregion Helpers
        #region Properties
        public enum UILayer
        {
            Overall,
            Overlay,
            Hud,
            Hud_Menu,
            Under,
        }
        public struct UIProperties
        {
            public float Fade;
            public string Material, Sprite;
            public bool EnableCursor, EnableKeyboard;

        }
        #endregion Properties

        public static class Create
        {
            public static CuiElementContainer Container(UIPoint.Anchor Anchor, UIPoint.Offset Offset, UIColor Color, UIProperties Properties, UILayer Layer, string ElementName)
            {
                var container = new CuiElementContainer()
                {
                    {
                        new CuiPanel()
                        {
                            RectTransform =
                            {
                                AnchorMin = $"{UIPoint.Anchor.Min(Anchor)}",
                                AnchorMax = $"{UIPoint.Anchor.Max(Anchor)}",
                                OffsetMin = $"{UIPoint.Offset.Min(Offset)}",
                                OffsetMax = $"{UIPoint.Offset.Max(Offset)}",
                            },
                            Image =
                            {
                                Color = UIColor.HexColor(Color),
                                FadeIn = Properties.Fade,
                                Material = Properties.Material,
                                Sprite = Properties.Sprite
                            },
                            CursorEnabled = Properties.EnableCursor,
                            KeyboardEnabled = Properties.EnableKeyboard,
                            FadeOut = Properties.Fade,
                        }, new CuiElement().Parent = Layer.ToString().Replace("_","."),ElementName
                    }
                };
                return container;
            }
            public static void Button(ref CuiElementContainer _container, UIPoint.Anchor Anchor, UIColor Color, UIProperties Properties, string ParentElement, string ElementName, string Command, UIColor TextColor, TextAnchor textAnchor = TextAnchor.MiddleCenter, string Text = "Click Me", int TextSize = 14)
            {
                _container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = $"{UIPoint.Anchor.Min(Anchor)}",
                        AnchorMax = $"{UIPoint.Anchor.Max(Anchor)}",
                        OffsetMin = $"0 0", OffsetMax = $"0 0",
                    },
                    Button =
                    {
                        Color = UIColor.HexColor(Color),
                        Command = Command,
                        Material = Properties.Material,
                        Sprite = Properties.Sprite,
                        FadeIn = Properties.Fade,
                    },
                    Text =
                    {
                        Align = textAnchor,
                        Text = Text,
                        Color = UIColor.HexColor(TextColor),
                        FadeIn = Properties.Fade,
                        FontSize = TextSize,
                    },
                    FadeOut = Properties.Fade,
                }, ParentElement, ElementName);
            }
            public static void Label(ref CuiElementContainer _container, UIPoint.Anchor Anchor, UIProperties Properties, string ParentElement, string ElementName, UIColor TextColor, TextAnchor textAnchor = TextAnchor.MiddleCenter, string Text = "Epic Text", int TextSize = 14)
            {
                _container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = $"{UIPoint.Anchor.Min(Anchor)}",
                        AnchorMax = $"{UIPoint.Anchor.Max(Anchor)}",
                        OffsetMin = $"0 0", OffsetMax = $"0 0",
                    },
                    FadeOut = Properties.Fade,
                    Text =
                    {
                        Align = textAnchor,
                        Color = UIColor.HexColor(TextColor),
                        FadeIn = Properties.Fade,
                        FontSize = TextSize,
                        Text = Text,
                    },
                }, ParentElement, ElementName);
            }
            public static void Panel(ref CuiElementContainer _container, UIPoint.Anchor Anchor, UIColor Color, UIProperties Properties, string ParentElement, string ElementName)
            {
                _container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = $"{UIPoint.Anchor.Min(Anchor)}",
                        AnchorMax = $"{UIPoint.Anchor.Max(Anchor)}",
                        OffsetMin = $"0 0", OffsetMax = $"0 0",
                    },
                    Image =
                    {
                        Color = UIColor.HexColor(Color),
                        FadeIn = Properties.Fade,
                        Material = Properties.Material,
                        Sprite = Properties.Sprite,
                    },
                    CursorEnabled = Properties.EnableCursor,
                    KeyboardEnabled = Properties.EnableKeyboard,
                    FadeOut = Properties.Fade,
                }, ParentElement, ElementName);
            }
            public static void Image(ref CuiElementContainer _container, UIPoint.Anchor Anchor, UIProperties Properties, string ParentElement, string ElementName, string Url)
            {
                _container.Add(new CuiElement
                {
                    Name = ElementName,
                    Parent = ParentElement,
                    FadeOut = Properties.Fade,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Url = Url,
                            FadeIn = Properties.Fade,
                            Material = Properties.Material,
                            Sprite = Properties.Sprite,
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"{UIPoint.Anchor.Min(Anchor)}",
                            AnchorMax = $"{UIPoint.Anchor.Max(Anchor)}",
                            OffsetMin = $"0 0", OffsetMax = $"0 0",
                        }
                    }
                });
            }
            public static void InputField(ref CuiElementContainer _container, UIPoint.Anchor PanelAnchor, UIPoint.Anchor InputFieldAnchor, UIColor Color, UIProperties Properties, UIColor TextColor, string ParentElement, string ElementName, string PanelElementName, string Command, string Text = "Epic Text", int TextLimit = 64, int TextSize = 14, bool IsPassword = false, bool NeedsKeyboard = false, bool ReadOnly = false, UnityEngine.UI.InputField.LineType lineType = UnityEngine.UI.InputField.LineType.SingleLine, TextAnchor textAnchor = TextAnchor.MiddleCenter)
            {
                Panel(ref _container, PanelAnchor, Color, Properties, ParentElement, PanelElementName);
                _container.Add(new CuiElement
                {
                    FadeOut = Properties.Fade,
                    Name = ElementName,
                    Parent = PanelElementName,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Align = textAnchor,
                            Color = UIColor.HexColor(TextColor),
                            Command = Command,
                            Text = Text,
                            CharsLimit = TextLimit,
                            FontSize = TextSize,
                            IsPassword = IsPassword,
                            LineType = lineType,
                            NeedsKeyboard = NeedsKeyboard,
                            ReadOnly = ReadOnly,
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"{UIPoint.Anchor.Min(InputFieldAnchor)}",
                            AnchorMax = $"{UIPoint.Anchor.Max(InputFieldAnchor)}",
                            OffsetMin = $"0 0", OffsetMax = $"0 0",
                        }
                    },
                });
            }
        }
    }
}