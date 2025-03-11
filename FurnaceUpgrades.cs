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
* 
*/
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("FurnaceUpgrades", "ifte", "2.5.3")]
    [Description("Upgrade your furnaces and improve smelting!")]
    internal class FurnaceUpgrades : RustPlugin
    {
        /*------------------------------------
         *
         *          FurnaceUpgrades by Ifte
         *            Original Author: Yun
         *    Support: https://discord.gg/cCWadjcapr
         *    Fiverr: https://www.fiverr.com/s/e2pkYY
         *            Eror404 not found
         *
         -------------------------------------*/

        /* Version: 2.5.3
         * - Fixed Console Spam from StackModifer(Codefling + Umod)
         * - Added On new wipe its going to reset existing furnaces data
        */

        /* Version: 2.5.2
         * - Fixed for old update.
        */

        /* Version: 2.5.1
         * - Fixed oven running after water gunned
        */

        /* Version: 2.5.0
         * - Fixed item not spliting on ovens
         * - Fixed less fuel needed on higher smelting items
         * - Adjusted Fuel Speed Multiplier and Smelting speed for vanilla
         * - Fixed Furnace timings for vanilla
         * - Fixed Notification
         * - Fixed Stacks issues (Solution on Description)
        */

        #region Fields

        [PluginReference]
        private Plugin Economics, ServerRewards, Toastify, Notify, ImageLibrary, StackModifier;

        private static FurnaceUpgrades _instance;
        private OvenController _controller;

        private const string USE_PERMISSION = "FurnaceUpgrades.use";
        private const string AUTO_FUEL_PERMISSION = "FurnaceUpgrades.autofuel";
        private const string AUTO_SPLIT_PERMISSION = "FurnaceUpgrades.autosplit";
        private readonly bool doDebug = false;

        private readonly string[] _conflictPlugins = new string[] {
            "StacksExtended",
            "StackModifier",
            "StackSizeController"
        };

        private readonly Dictionary<string, string> _ovensPrefab = new Dictionary<string, string>()
        {
            { "furnace", "assets/prefabs/deployable/furnace/furnace.prefab" },
            { "furnace.large", "assets/prefabs/deployable/furnace.large/furnace.large.prefab" },
            { "electric.furnace", "assets/prefabs/deployable/playerioents/electricfurnace/electricfurnace.deployed.prefab" },
            { "legacyfurnace", "assets/prefabs/deployable/legacyfurnace/legacy_furnace.prefab" },
            { "campfire", "assets/prefabs/deployable/campfire/campfire.prefab" },
            { "bbq", "assets/prefabs/deployable/bbq/bbq.deployed.prefab" },
            { "small.oil.refinery", "assets/prefabs/deployable/oil refinery/refinery_small_deployed.prefab" }
        };

        private readonly Dictionary<string, string> _images = new Dictionary<string, string>() {
            { "legacyfurnace_image", "https://i.ibb.co/tLxgDP0/legacyfurnace-300x300.png" }
        };

        private readonly Hash<ulong, BaseOven> _openModals = new Hash<ulong, BaseOven>();

        private Coroutine _setupOvensCoroutine;

        private class OvenSlot
        {
            public Item Item;
            public int? Position;
            public int Index;
            public int DeltaAmount;
        }

        private class OvenInfo
        {
            public float ETA;
            public float FuelNeeded;
        }

        private class OvenStatus
        {
            public float SmeltingSpeed;
            public float FuelSpeed;
            public float ResourceOutput;
            public float CharcoalMultiplier;
        }

        private class UpgradeResult
        {
            public UpgradeResultType Type;
            public int Cost;

            public enum UpgradeResultType
            {
                NotEnoughMoney,
                Success,
                MaxLevel,
            }

            public UpgradeResult(UpgradeResultType type, int cost)
            {
                Type = type;
                Cost = cost;
            }
        }

        #region Enums

        private enum FeatureType
        {
            SmeltingSpeed,
            FuelSpeed,
            ResourceOutput,
            CharcoalMultiplier
        }

        private enum CurrencyType
        {
            Item,
            Economics,
            ServerRewards
        }

        private enum NotificationPlugin
        {
            None,
            Toastify,
            Notify
        }

        #endregion

        #endregion

        #region Data

        private static Dictionary<ulong, FurnaceData> _furnacesData;

        private enum FurnaceDataType
        {
            Furnace,
            Item
        }

        private class FurnaceData
        {
            public FurnaceDataType type;
            public bool useAutoFuel = true;
            public bool useAutoSplit = true;
            public Levels levels = new Levels();

            public class Levels
            {
                public int smeltingSpeed;
                public int fuelSpeed;
                public int resourceOutput;
                public int charcoalMultiplier;
            }
        }

        private void LoadData()
        {
            try
            {
                _furnacesData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, FurnaceData>>(Name);
            }
            catch
            {
                _furnacesData = new Dictionary<ulong, FurnaceData>();
            }

            if (_furnacesData == null)
                _furnacesData = new Dictionary<ulong, FurnaceData>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, _furnacesData);
        }

        #endregion

        #region Configuration

        private static Configuration _config;

        private class UpgradeSettings
        {
            [JsonProperty("Smelting speed")]
            public Upgrade[] SmeltingSpeed { get; set; }

            [JsonProperty("Fuel speed")]
            public Upgrade[] FuelSpeed { get; set; }

            [JsonProperty("Resource output multiplier")]
            public Upgrade[] ResourceOutput { get; set; }

            [JsonProperty("Charcoal multiplier")]
            public Upgrade[] CharcoalMultiplier { get; set; }

            public class Upgrade
            {
                public int Cost { get; set; }
                public float Multiplier { get; set; }

                public Upgrade(int cost, float multiplier)
                {
                    Cost = cost;
                    Multiplier = multiplier;
                }
            }
        }

        private class DefaultSettings
        {
            [JsonProperty("Smelting speed")]
            public float SmeltingSpeed { get; set; }

            [JsonProperty("Fuel speed")]
            public float FuelSpeed { get; set; }

            [JsonProperty("Resource output multiplier")]
            public float ResourceOutput { get; set; }

            [JsonProperty("Charcoal multiplier")]
            public float CharcoalMultiplier { get; set; }
        }

        private class FeaturesSettings
        {
            [JsonProperty("Smelting speed multiplier")]
            public bool SmeltingSpeed { get; set; }

            [JsonProperty("Fuel speed")]
            public bool FuelSpeed { get; set; }

            [JsonProperty("Resource output multiplier")]
            public bool ResourceOutput { get; set; }

            [JsonProperty("Charcoal multiplier")]
            public bool CharcoalMultiplier { get; set; }

            [JsonProperty("Auto split cookables")]
            public bool AutoSplit { get; set; }

            [JsonProperty("Auto add fuel")]
            public bool AutoFuel { get; set; }
        }

        private class UISettings
        {
            [JsonProperty("Disable status panel")]
            public bool DisableStatusPanel { get; set; }

            [JsonProperty("Upgrade button")]
            public BasicSettings UpgradeButton { get; set; }

            [JsonProperty("Upgrade button on electric furnaces")]
            public BasicSettings UpgradeButtonElectricFurnace { get; set; }

            [JsonProperty("Status panel")]
            public BasicSettings StatusPanel { get; set; }

            public class BasicSettings
            {
                public string Anchor { get; set; }
                public string Offset { get; set; }

                [JsonProperty("Background color")]
                public string BackgroundColor { get; set; }

                [JsonProperty("Font size")]
                public int FontSize { get; set; }
            }
        }

        private class VisualSettings
        {
            [JsonProperty("Use chat messages")]
            public bool UseChatMessages { get; set; }

            [JsonProperty("Notification plugin (0 - None | 1 - Toastify | 2 - Notify)")]
            public NotificationPlugin NotificationPlugin { get; set; }

            [JsonProperty("Toastify notification ID")]
            public string ToastifyNotificationId { get; set; }

            [JsonProperty("Notify notification type")]
            public int NotifyNotificationType { get; set; }

            [JsonProperty("Upgrade effect (empty = disabled)")]
            public string UpgradeEffect { get; set; }
        }

        private class CurrencySettings
        {
            [JsonProperty("Currency type (0 - Scrap | 1 - Economics | 2 - Server Rewards)")]
            public CurrencyType Type { get; set; }

            [JsonProperty("Currency item (if the currency type is '0')")]
            public CurrencyItem Item { get; set; }

            public class CurrencyItem
            {
                [JsonProperty("Short name")]
                public string ShortName { get; set; }

                [JsonProperty("Skin ID")]
                public ulong SkinID { get; set; }
            }
        }

        private class GeneralSettings
        {
            [JsonProperty("Only the owner can upgrade the furnace")]
            public bool OwnerOnly { get; set; }

            [JsonProperty("Only teammates can upgrade the furnace")]
            public bool OnlyTeamMates { get; set; }

            [JsonProperty("Keep attributes when removing")]
            public bool KeepAttributes { get; set; }

        }

        private class Configuration
        {
            [JsonProperty("General Settings")]
            public GeneralSettings General { get; set; }

            [JsonProperty("Currency Settings")]
            public CurrencySettings Currency { get; set; }

            [JsonProperty("Visual Settings")]
            public VisualSettings Visual { get; set; }

            [JsonProperty("UI Settings")]
            public UISettings UI { get; set; }

            [JsonProperty("Features Settings")]
            public FeaturesSettings Features { get; set; }

            [JsonProperty("Default Settings")]
            public DefaultSettings Defaults { get; set; }

            [JsonProperty("Upgrades Settings", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, UpgradeSettings> Upgrades { get; set; }

            public VersionNumber Version { get; set; }
        }

        private Configuration GetBaseConfig()
        {
            UpgradeSettings upgradeSettings = new UpgradeSettings()
            {
                SmeltingSpeed = new UpgradeSettings.Upgrade[]
                {
                    new UpgradeSettings.Upgrade(100, 2f),
                    new UpgradeSettings.Upgrade(200, 3f),
                },
                CharcoalMultiplier = new UpgradeSettings.Upgrade[]
                {
                    new UpgradeSettings.Upgrade(100, 2f)
                },
                FuelSpeed = new UpgradeSettings.Upgrade[]
                {
                    new UpgradeSettings.Upgrade(100, 2f)
                },
                ResourceOutput = new UpgradeSettings.Upgrade[]
                {
                    new UpgradeSettings.Upgrade(100, 2f)
                }
            };

            return new Configuration()
            {
                General = new GeneralSettings()
                {
                    OwnerOnly = false,
                    OnlyTeamMates = true,
                    KeepAttributes = true
                },
                Currency = new CurrencySettings()
                {
                    Type = CurrencyType.Item,
                    Item = new CurrencySettings.CurrencyItem()
                    {
                        ShortName = "scrap",
                        SkinID = 0ul
                    }
                },
                Visual = new VisualSettings()
                {
                    UseChatMessages = true,
                    NotificationPlugin = NotificationPlugin.Toastify,
                    ToastifyNotificationId = "success",
                    NotifyNotificationType = 0,
                    UpgradeEffect = "assets/bundled/prefabs/fx/build/promote_stone.prefab"
                },
                UI = new UISettings()
                {
                    DisableStatusPanel = false,
                    UpgradeButton = new UISettings.BasicSettings()
                    {
                        Anchor = "0.5 0 0.5 0",
                        Offset = "400 109.5 480 141.5",
                        BackgroundColor = UIBuilder.Colors.GREEN,
                        FontSize = 13
                    },
                    UpgradeButtonElectricFurnace = new UISettings.BasicSettings()
                    {
                        Anchor = "0.5 0 0.5 0",
                        Offset = "492 354.5 572 386.5",
                        BackgroundColor = UIBuilder.Colors.GREEN,
                        FontSize = 13
                    },
                    StatusPanel = new UISettings.BasicSettings()
                    {
                        Anchor = "0.5 0 0.5 0",
                        Offset = "193 17 420 103",
                        BackgroundColor = "0.969 0.922 0.882 0.035",
                        FontSize = 12
                    }
                },
                Features = new FeaturesSettings()
                {
                    SmeltingSpeed = true,
                    FuelSpeed = true,
                    CharcoalMultiplier = true,
                    ResourceOutput = true,
                    AutoFuel = true,
                    AutoSplit = true,
                },
                Defaults = new DefaultSettings()
                {
                    FuelSpeed = 1f,
                    CharcoalMultiplier = 1f,
                    ResourceOutput = 1f,
                    SmeltingSpeed = 1f
                },
                Upgrades = _ovensPrefab.ToDictionary((x) => x.Key, (x) => upgradeSettings),
                Version = Version
            };
        }

        #region Overrides

        protected override void LoadDefaultConfig()
            => _config = GetBaseConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();

            if (_config.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(_config, true);
        }

        protected override void SaveConfig()
            => Config.WriteObject(_config, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Your config file is outdated! Updating config values...");

            if (_config.Version < new VersionNumber(2, 0, 3))
            {
                Configuration baseConfig = GetBaseConfig();
                _config.General.OnlyTeamMates = baseConfig.General.OnlyTeamMates;
            }

            if (_config.Version < new VersionNumber(2, 0, 40))
            {
                Configuration baseconfig = GetBaseConfig();
                _config.UI.UpgradeButtonElectricFurnace = baseconfig.UI.UpgradeButtonElectricFurnace;
            }

            _config.Version = Version;
            PrintWarning("Config updated!");
        }

        #endregion

        #endregion

        #region Localization

        private void SendNotification(BasePlayer player, string message, params object[] args)
        {
            if (_config.Visual.NotificationPlugin == NotificationPlugin.None)
                return;

            if (_config.Visual.NotificationPlugin == NotificationPlugin.Toastify)
            {
                if (!Toastify)
                    return;

                Toastify.CallHook("SendToast", player, _config.Visual.ToastifyNotificationId, "UPGRADES", GetMessage(player, message, args), 3f);
            }
            else if (Notify)
            {
                Notify.CallHook("SendNotify", player.UserIDString, _config.Visual.NotifyNotificationType, GetMessage(player, message, args));
            }
        }

        private void SendMessage(BasePlayer player, string message, params object[] args)
        {
            if (_config.Visual.UseChatMessages)
                player.ChatMessage($"{GetMessage(player, "Chat.Prefix")}: {GetMessage(player, message, args)}");

            SendNotification(player, message, args);
        }

        private string GetMessage(BasePlayer player, string message, params object[] args)
            => string.Format(lang.GetMessage(message, this, player.UserIDString), args);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "Chat.Prefix", "<color=red>[FurnaceUpgrades]</color>" },

                { "Error.NoPermission", "You don't have permission to update this oven" },
                { "Error.NoPermission.AutoFuel", "You don't have permission to toggle the auto fuel." },
                { "Error.NoPermission.AutoSplit", "You don't have permission to toggle the auto split." },
                { "Error.NotEnoughMoney", "You don't have enough money to upgrade this attribute" },
                { "Error.OwnerOnly", "Only the owner can update the oven attributes." },
                { "Error.OwnerOnly.CanRemove", "Only the owner of a upgraded furnace can remove it" },
                { "Error.OnlyTeamMates", "Only the furnace owner's teammates can upgrade this furnace." },
                { "Error.OnlyTeamMates.CanRemove", "Only the furnace owner's teammates can remove this furnace" },

                { "Notification.SmeltingSpeedUpgraded", "The smelting speed has been upgraded for <color=red>-${0}</color>" },
                { "Notification.FuelSpeedUpgraded", "The fuel speed has been upgraded for <color=red>-${0}</color>" },
                { "Notification.ResourceOutputUpgraded", "The resource output multiplier has been upgraded for <color=red>-${0}</color>" },
                { "Notification.CharcoalMultiplierUpgraded", "The charcoal multiplier has been upgraded for <color=red>-${0}</color>" },

                { "UI.UpgradeButton", "UPGRADE" },
                { "UI.StatusTitle", "<b>STATUS:</b>" },
                { "UI.SmeltingSpeedStatus", "Smelting speed: <color=orange>{0}x</color>" },
                { "UI.FuelSpeedStatus", "Fuel speed: <color=orange>{0}x</color>" },
                { "UI.ResourceOutputStatus", "Resource output: <color=orange>{0}x</color>" },
                { "UI.CharcoalMultiplierStatus", "Charcoal multiplier: <color=orange>{0}x</color>" },

                { "UI.OptionsLabel", "<b>OPTIONS:</b>" },
                { "UI.NoOptionAvailable", "No option available =/" },
                { "UI.AutoFuelOptionLabel", "Auto add fuel" },
                { "UI.AutoSplitOptionLabel", "Auto split added ores" },

                { "UI.StatusTemplate", "<b>{0}x » <color=orange>{1}x</color></b>" },
                { "UI.StatusMaxLevel", "<b>MAX: <color=orange>{0}x</color></b>" },
                { "UI.UpgradeTemplate", "UPGRADE FOR ${0}" },
                { "UI.MaxLevelUpgrade", "✔ MAX LEVEL REACHED" },
                { "UI.SmeltingSpeed.Title", "<b>SMELTING SPEED</b>" },
                { "UI.SmeltingSpeed.Description", "Increases the speed of the ore smelting process" },
                { "UI.FuelSpeed.Title", "<b>FUEL SPEED</b>" },
                { "UI.FuelSpeed.Description", "Increases the speed at which fuel is burned" },
                { "UI.ResourceOutput.Title", "<b>RESOURCE OUTPUT</b>" },
                { "UI.ResourceOutput.Description", "Increases the resource production by tick" },
                { "UI.CharcoalMultiplier.Title", "<b>CHARCOAL MULTIPLIER</b>" },
                { "UI.CharcoalMultiplier.Description", "Increases the charcoal production by tick" }
            }, this);
        }

        #endregion

        #region Initialization

        private void Init()
        {
            Unsubscribe(nameof(CanMoveItem));
            Unsubscribe(nameof(CanStackItem));
            Unsubscribe(nameof(CanCombineDroppedItem));
            Unsubscribe(nameof(OnEntityBuilt));
        }

        private void OnServerInitialized()
        {
            _instance = this;
            _controller = new OvenController();
            LoadData();

            CheckConflictPlugins();

            if (_config.Features.AutoFuel || _config.Features.AutoSplit)
            {
                Subscribe(nameof(CanMoveItem));
            }

            if (_config.General.KeepAttributes)
            {
                Subscribe(nameof(OnEntityBuilt));

                if (!StackModifier || !StackModifier.IsLoaded)
                {
                    Subscribe(nameof(CanStackItem));
                    Subscribe(nameof(CanCombineDroppedItem));
                }
                else
                {
                    PrintWarning("If the StackModifier plugin oven stack is greater than (1), upgrades will be lost, FurnaceUpgrades does not work correctly when changing the Furnace Stack using StackModifier!");
                }
            }

            permission.RegisterPermission(USE_PERMISSION, this);
            permission.RegisterPermission(AUTO_FUEL_PERMISSION, this);
            permission.RegisterPermission(AUTO_SPLIT_PERMISSION, this);

            if (ImageLibrary)
            {
                ImageLibrary.CallHook("ImportImageList", Title, _images, 0ul, true);

                var imageList = _ovensPrefab.Keys.Where((x) => x != "legacyfurnace").Select((x) => new KeyValuePair<string, ulong>(x, 0ul)).ToList();
                ImageLibrary.CallHook("LoadImageList", Title, imageList);
            }

            _setupOvensCoroutine = ServerMgr.Instance.StartCoroutine(_controller.SetupOvens());
        }

        private void OnNewSave(string FileName)
        {
            Puts("New save detected, resetting data...");
            _furnacesData.Clear();
            SaveData();
        }

        #endregion

        #region Plugin loads check

        private void OnPluginLoaded(Plugin plugin)
        {
            if (_conflictPlugins.Contains(plugin.Name))
                CheckConflictPlugins();
        }

        private void OnPluginUnloaded(Plugin plugin)
            => OnPluginLoaded(plugin);

        private bool CheckConflictPlugins()
        {
            string? conflictPlugin = _conflictPlugins.FirstOrDefault(x => plugins.Exists(x));
            if (!string.IsNullOrEmpty(conflictPlugin))
            {
                Unsubscribe(nameof(CanStackItem));
                Unsubscribe(nameof(CanCombineDroppedItem));
                PrintWarning($"The CanStackItem, CanCombineDroppedItem hook's was unsubscribed due to conflicts with the '{conflictPlugin}' plugin");
                return true;
            }
            else
            {
                if (_config.General.KeepAttributes)
                {
                    Subscribe(nameof(CanStackItem));
                    Subscribe(nameof(CanCombineDroppedItem));
                }
            }

            return false;
        }
        
        #endregion

        #region Hooks

        private void Unload()
        {
            if (!_setupOvensCoroutine.IsUnityNull())
            {
                ServerMgr.Instance.StopCoroutine(_setupOvensCoroutine);
                _setupOvensCoroutine = null;
            }

            foreach (var player in BasePlayer.activePlayerList)
                DestroyUI(player);

            _controller.CleanupOvens();

            _furnacesData = null;
            _config = null;
            _instance = null;
        }

        private void OnEntitySpawned(BaseOven oven)
        {
            if (!oven.OwnerID.IsSteamId())
                return;

            if (!_ovensPrefab.ContainsValue(oven.PrefabName))
                return;

            _controller.InstallComponent(oven);
        }

        private object CanMoveItem(Item item, PlayerInventory playerLoot, ItemContainerId targetContainerId, int targetSlot, int amount)
        {
            if (item == null || !playerLoot)
                return null;

            if (!playerLoot.TryGetComponent<BasePlayer>(out var player))
                return null;

            BaseOven oven = playerLoot.loot.entitySource as BaseOven;
            if (!oven)
                return null;

            ItemContainer targetContainer = playerLoot.FindContainer(targetContainerId);
            if (targetContainer != null && !(targetContainer?.entityOwner is BaseOven))
                return null;

            ItemContainer container = oven.inventory;
            ItemContainer originalContainer = item.GetRootContainer();
            if (container == null || originalContainer == null || container == originalContainer || originalContainer?.entityOwner is BaseOven)
                return null;

            BaseOven.MinMax? allowedSlots = oven.GetAllowedSlots(item);
            if (allowedSlots is null)
                return null;

            for (int i = allowedSlots.Value.Min; i <= allowedSlots.Value.Max; i++)
            {
                Item slot = oven.inventory.GetSlot(i);
                if (slot != null && slot.info.shortname != item.info.shortname)
                    return null;
            }

            if (!item.info.TryGetComponent<ItemModCookable>(out var cookable) || oven.IsOutputItem(item))
                return null;

            if (cookable.lowTemp > oven.cookingTemperature || cookable.highTemp < oven.cookingTemperature)
                return null;

            if (!_furnacesData.TryGetValue(oven.net.ID.Value, out var furnaceData))
                furnaceData = new FurnaceData();

            // Move the splitted items
            if (_config.Features.AutoSplit && furnaceData.useAutoSplit)
                MoveSplitItem(item, oven, amount);

            // Auto add fuel
            if (oven.allowByproductCreation && _config.Features.AutoFuel && furnaceData.useAutoFuel)
                NextTick(() => AutoAddFuel(playerLoot, oven));

            return (_config.Features.AutoSplit && furnaceData.useAutoSplit) ? true : null;
        }

        #region Open/Close UI Hooks

        private void OnLootEntity(BasePlayer player, BaseOven oven)
        {
            if (player.IsInTutorial || !oven.OwnerID.IsSteamId())
                return;

            if (_config.General.OwnerOnly && oven.OwnerID != player.userID)
                return;

            if (_config.General.OnlyTeamMates && oven.OwnerID != player.userID && !IsTeamMate(player, oven.OwnerID))
                return;

            if (!_ovensPrefab.ContainsValue(oven.PrefabName))
                return;

            if (!oven.HasComponent<OvenComponent>())
                return;

            ShowUpgradeButton(player, oven);
            ShowOvenStatus(player, oven);
        }

        private void OnLootEntityEnd(BasePlayer player, BaseOven oven)
        {
            DestroyUI(player);
        }

        #endregion

        #region Oven Hooks

        private object OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            if (!oven.TryGetComponent<OvenComponent>(out var component))
                return null;

            if (oven.IsOn())
                component.StopCooking();
            else
            {
                oven.StopCooking();
                component.StartCooking();
            }

            return false;
        }

        private object OnOvenStart(BaseOven oven)
        {
            if (!oven.TryGetComponent<OvenComponent>(out var component))
                return null;

            component.StartCooking();
            return false;
        }

        #endregion

        #region Stack Fixes

        private object CanStackItem(Item source, Item target)
        {
            if (source == null || target == null)
                return null;

            if (_furnacesData.ContainsKey(source.uid.Value) || _furnacesData.ContainsKey(target.uid.Value))
                return false;

            return null;
        }

        private object CanCombineDroppedItem(DroppedItem item, DroppedItem targetItem)
            => CanStackItem(item?.item, targetItem?.item);

        #endregion

        private void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            BasePlayer player = planner.GetOwnerPlayer();
            if (!player)
                return;

            Item item = player.GetActiveItem();
            if (item == null || !_ovensPrefab.ContainsKey(item.info.shortname))
                return;

            if (!gameObject.TryGetComponent<BaseOven>(out var oven))
                return;

            if (!_furnacesData.TryGetValue(item.uid.Value, out var itemData))
                return;

            _furnacesData.Remove(item.uid.Value);
            _furnacesData[oven.net.ID.Value] = itemData;
            itemData.type = FurnaceDataType.Furnace;
            SaveData();

            NextTick(() =>
            {
                if (oven.TryGetComponent<OvenComponent>(out var component))
                    component.UpdateProperties();

                oven.inventory.Clear();
                oven.inventory.MarkDirty();
            });
        }

        private bool? CanPickupEntity(BasePlayer player, BaseOven oven)
        {
            if (!oven.HasComponent<OvenComponent>())
                return null;

            if (!IsVanillaFurnace(oven) && _config.General.KeepAttributes)
            {
                if (_config.General.OwnerOnly && oven.OwnerID != player.userID)
                {
                    SendMessage(player, "Error.OwnerOnly.CanRemove");
                    return false;
                }

                if (_config.General.OnlyTeamMates && oven.OwnerID != player.userID && !IsTeamMate(player, oven.OwnerID))
                {
                    SendMessage(player, "Error.OnlyTeamMates.CanRemove");
                    return false;
                }
            }

            if (TryGiveOvenWithAttributes(player, oven))
            {
                oven.Kill();
                return false;
            }

            return null;
        }

        #region RemoverTool plugin

        private object canRemove(BasePlayer player, BaseOven oven)
        {
            if (!oven.HasComponent<OvenComponent>() || IsVanillaFurnace(oven))
                return null;

            if (_config.General.KeepAttributes)
            {
                if (_config.General.OwnerOnly && oven.OwnerID != player.userID)
                    return GetMessage(player, "Error.OwnerOnly.CanRemove");

                if (_config.General.OnlyTeamMates && oven.OwnerID != player.userID && !IsTeamMate(player, oven.OwnerID))
                    return GetMessage(player, "Error.OnlyTeamMates.CanRemove");
            }

            return null;
        }

        private object OnRemovableEntityInfo(BaseEntity entity, BasePlayer player)
        {
            if (!_config.General.KeepAttributes)
                return null;

            if (!(entity is BaseOven oven) || !oven.HasComponent<OvenComponent>())
                return null;

            if (!_furnacesData.TryGetValue(oven.net.ID.Value, out var furnaceData))
                return null;

            string itemName = _ovensPrefab.First((x) => x.Value == entity.PrefabName).Key;
            ItemDefinition itemDef = ItemManager.FindItemDefinition(itemName);
            if (!itemDef)
                return null;

            string displayName = $"{itemDef.displayName.english} - UPGRADED";
            return new Dictionary<string, object>()
            {
                { "DisplayName", displayName },
                {
                    "Refund", new Dictionary<string, object>
                    {
                        {
                            "furnaceupgrades.furnace", new Dictionary<string, object>
                            {
                                { "Amount", 1 },
                                { "DisplayName", displayName }
                            }
                        }
                    }
                }
            };
        }

        private object OnRemovableEntityGiveRefund(BaseEntity entity, BasePlayer player, string itemName)
        {
            if (!(entity is BaseOven oven) || !oven.HasComponent<OvenComponent>())
                return null;

            if (itemName != "furnaceupgrades.furnace")
                return null;

            if (!TryGiveOvenWithAttributes(player, oven))
                return false;

            return null;
        }

        #endregion

        #region BuildTools Plugin

        private object CanBuildToolsGiveRefund(BasePlayer player, BaseEntity entity)
        {
            if (!(entity is BaseOven oven) || !oven.HasComponent<OvenComponent>())
                return null;

            if (TryGiveOvenWithAttributes(player, oven))
                return false;

            return null;
        }

        #endregion

        #endregion

        #region Functions

        private void MoveSplitItem(Item item, BaseOven oven, int splitAmount)
        {
            ItemContainer container = oven.inventory;
            int numOreSlots = oven.inputSlots;
            int totalMoved = 0;
            int itemAmount = item.amount > splitAmount ? splitAmount : item.amount;
            int totalAmount = Math.Min(itemAmount + container.itemList.Where(slotItem => slotItem.info == item.info).Take(numOreSlots).Sum(slotItem => slotItem.amount), Math.Abs(item.info.stackable * numOreSlots));

            if (numOreSlots <= 0)
            {
                return;
            }

            int totalStackSize = Math.Min(totalAmount / numOreSlots, item.info.stackable);
            int remaining = totalAmount - totalAmount / numOreSlots * numOreSlots;

            List<int> addedSlots = Pool.Get<List<int>>();

            List<OvenSlot> ovenSlots = Pool.Get<List<OvenSlot>>();

            for (int i = 0; i < numOreSlots; ++i)
            {
                Item existingItem;
                int slot = FindMatchingSlotIndex(oven, container, out existingItem, item.info, addedSlots);

                if (slot == -1)
                {
                    return;
                }

                addedSlots.Add(slot);

                OvenSlot ovenSlot = new OvenSlot
                {
                    Position = existingItem?.position,
                    Index = slot,
                    Item = existingItem
                };

                int currentAmount = existingItem?.amount ?? 0;
                int missingAmount = totalStackSize - currentAmount + (i < remaining ? 1 : 0);
                ovenSlot.DeltaAmount = missingAmount;

                if (currentAmount + missingAmount <= 0)
                    continue;

                ovenSlots.Add(ovenSlot);
            }

            Pool.FreeUnmanaged(ref addedSlots);

            foreach (OvenSlot slot in ovenSlots)
            {
                if (slot.Item == null)
                {
                    Item newItem = ItemManager.Create(item.info, slot.DeltaAmount, item.skin);
                    slot.Item = newItem;
                    newItem.MoveToContainer(container, slot.Position ?? slot.Index);
                }
                else
                {
                    slot.Item.amount += slot.DeltaAmount;
                }

                totalMoved += slot.DeltaAmount;
            }

            container.MarkDirty();

            Pool.FreeUnmanaged(ref ovenSlots);

            if (totalMoved >= item.amount)
            {
                item.Remove();
                item.GetRootContainer()?.MarkDirty();
                return;
            }
            else
            {
                item.amount -= totalMoved;
                item.GetRootContainer()?.MarkDirty();
                return;
            }
        }

        private void AutoAddFuel(PlayerInventory playerInventory, BaseOven oven)
        {
            OvenInfo ovenInfo = GetOvenInfo(oven);

            int neededFuel = (int)Math.Ceiling(ovenInfo.FuelNeeded) - oven.inventory.GetAmount(oven.fuelType.itemid, false);
            var playerFuel = playerInventory.FindItemsByItemID(oven.fuelType.itemid);

            if (neededFuel <= 0 || playerFuel.Count <= 0)
                return;

            int fuelSlotIndex = 0;
            foreach (var item in playerFuel)
            {
                Item existingFuel = oven.inventory.GetSlot(fuelSlotIndex);
                if (existingFuel != null && existingFuel.amount >= existingFuel.info.stackable)
                {
                    if (fuelSlotIndex < oven.fuelSlots)
                        fuelSlotIndex++;
                    else break;
                }

                Item largestFuelStack = oven.inventory.itemList
                    .Where(x => x.info == oven.fuelType)
                    .OrderByDescending(x => item.amount)
                    .FirstOrDefault();

                int toTake = Math.Min(neededFuel, (oven.fuelType.stackable * oven.fuelSlots) - (largestFuelStack?.amount ?? 0));
                if (toTake <= 0)
                    break;

                if (toTake > item.amount)
                    toTake = item.amount;

                neededFuel -= toTake;

                int currentFuelAmount = oven.inventory.GetAmount(oven.fuelType.itemid, false);
                if (currentFuelAmount >= oven.fuelType.stackable * oven.fuelSlots)
                    break;

                if (toTake >= item.amount)
                {
                    item.MoveToContainer(oven.inventory, fuelSlotIndex);
                }
                else
                {
                    Item splitItem = item.SplitItem(toTake);
                    if (!splitItem.MoveToContainer(oven.inventory, fuelSlotIndex))
                        break;
                }

                if (neededFuel <= 0)
                    break;
            }
        }

        private UpgradeResult TryUpgradeAttribute(BasePlayer player, BaseOven oven, FeatureType type)
        {
            UpgradeSettings.Upgrade nextUpgrade = GetNextUpgrade(oven, type);
            if (nextUpgrade == null)
                return new UpgradeResult(UpgradeResult.UpgradeResultType.MaxLevel, 0);

            if (!TryTakeMoney(player, nextUpgrade.Cost))
                return new UpgradeResult(UpgradeResult.UpgradeResultType.NotEnoughMoney, nextUpgrade.Cost);

            if (!_furnacesData.TryGetValue(oven.net.ID.Value, out var furnaceData))
                furnaceData = new FurnaceData();

            switch (type)
            {
                case FeatureType.SmeltingSpeed:
                    furnaceData.levels.smeltingSpeed++;
                    break;

                case FeatureType.FuelSpeed:
                    furnaceData.levels.fuelSpeed++;
                    break;

                case FeatureType.ResourceOutput:
                    furnaceData.levels.resourceOutput++;
                    break;

                case FeatureType.CharcoalMultiplier:
                    furnaceData.levels.charcoalMultiplier++;
                    break;
            }

            _furnacesData[oven.net.ID.Value] = furnaceData;
            SaveData();
            return new UpgradeResult(UpgradeResult.UpgradeResultType.Success, nextUpgrade.Cost);
        }

        private bool TryGiveOvenWithAttributes(BasePlayer player, BaseOven oven)
        {
            string ovenName = _ovensPrefab.FirstOrDefault((x) => x.Value == oven.PrefabName).Key;
            if (string.IsNullOrEmpty(ovenName))
                return false;

            if (!_furnacesData.TryGetValue(oven.net.ID.Value, out var furnaceData))
                return false;

            if (!_config.General.KeepAttributes)
            {
                _furnacesData.Remove(oven.net.ID.Value);
                SaveData();
                return false;
            }

            Item item = ItemManager.CreateByName(ovenName, 1, oven.skinID);
            if (item == null)
                return false;

            item.name = $"{item.info.displayName.english} - UPGRADED";

            _furnacesData.Remove(oven.net.ID.Value);
            _furnacesData[item.uid.Value] = furnaceData;
            furnaceData.type = FurnaceDataType.Item;
            SaveData();

            if (!item.MoveToContainer(player.inventory.containerBelt, -1, false)
                && !item.MoveToContainer(player.inventory.containerMain, -1, false))
                item.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity);

            return true;
        }

        #endregion

        #region Helpers

        private BaseOven GetOvenById(NetworkableId ovenId)
        {
            return BaseNetworkable.serverEntities.Find(ovenId) as BaseOven;
        }

        private bool TryTakeMoney(BasePlayer player, int amount)
        {
            switch (_config.Currency.Type)
            {
                case CurrencyType.Economics:
                    return Economics.Call<bool>("Withdraw", player.UserIDString, (double)amount);

                case CurrencyType.ServerRewards:
                    {
                        int balance = ServerRewards.Call<int>("CheckPoints", player.userID);
                        if (balance < amount)
                            return false;

                        object result = ServerRewards.CallHook("TakePoints", player.userID, amount);
                        return result is bool ? (bool)result : false;
                    }

                default:
                    {
                        int acc = 0;
                        List<Item> toTake = Pool.Get<List<Item>>();

                        foreach (var item in player.inventory.containerMain.itemList)
                        {
                            if (item.info.shortname == _config.Currency.Item.ShortName
                                && item.skin == _config.Currency.Item.SkinID)
                            {
                                acc += item.amount;
                                toTake.Add(item);

                                if (acc >= amount)
                                    break;
                            }
                        }

                        // Process containerMain if still required
                        if (acc < amount)
                        {
                            foreach (var item in player.inventory.containerBelt.itemList)
                            {
                                if (item.info.shortname == _config.Currency.Item.ShortName
                                    && item.skin == _config.Currency.Item.SkinID)
                                {
                                    acc += item.amount;
                                    toTake.Add(item);

                                    if (acc >= amount)
                                        break;
                                }
                            }
                        }

                        if (acc < amount)
                        {
                            Pool.FreeUnmanaged(ref toTake);
                            return false;
                        }

                        int remaining = amount;
                        foreach (var item in toTake)
                        {
                            if (item.amount > remaining)
                            {
                                item.UseItem(remaining);
                                remaining = 0;
                            }
                            else
                            {
                                remaining -= item.amount;
                                item.Remove();
                            }

                            item.MarkDirty();
                            if (remaining < 1)
                                break;
                        }

                        Pool.FreeUnmanaged(ref toTake);
                        return true;
                    }
            }
        }

        private OvenStatus GetOvenStatus(BaseOven oven)
        {
            return new OvenStatus()
            {
                SmeltingSpeed = GetCurrentUpgrade(oven, FeatureType.SmeltingSpeed)?.Multiplier ?? _config.Defaults.SmeltingSpeed,
                FuelSpeed = GetCurrentUpgrade(oven, FeatureType.FuelSpeed)?.Multiplier ?? _config.Defaults.FuelSpeed,
                ResourceOutput = GetCurrentUpgrade(oven, FeatureType.ResourceOutput)?.Multiplier ?? _config.Defaults.ResourceOutput,
                CharcoalMultiplier = GetCurrentUpgrade(oven, FeatureType.CharcoalMultiplier)?.Multiplier ?? _config.Defaults.CharcoalMultiplier
            };
        }

        private UpgradeSettings.Upgrade GetCurrentUpgrade(BaseOven oven, FeatureType type)
        {
            string ovenName = _ovensPrefab.FirstOrDefault((x) => x.Value == oven.PrefabName).Key;

            _furnacesData.TryGetValue(oven.net.ID.Value, out var furnaceData);
            int currentLevel = type switch
            {
                FeatureType.SmeltingSpeed => furnaceData?.levels.smeltingSpeed ?? 0,
                FeatureType.FuelSpeed => furnaceData?.levels.fuelSpeed ?? 0,
                FeatureType.ResourceOutput => furnaceData?.levels.resourceOutput ?? 0,
                _ => furnaceData?.levels.charcoalMultiplier ?? 0,
            };

            UpgradeSettings settings = _config.Upgrades[ovenName];
            UpgradeSettings.Upgrade[] upgrades = type switch
            {
                FeatureType.SmeltingSpeed => settings.SmeltingSpeed,
                FeatureType.FuelSpeed => settings.FuelSpeed,
                FeatureType.ResourceOutput => settings.ResourceOutput,
                _ => settings.CharcoalMultiplier,
            };

            return (currentLevel > 0 && upgrades.Length >= currentLevel)
                ? upgrades[currentLevel - 1]
                : currentLevel > 0
                    ? upgrades.LastOrDefault()
                    : null;
        }

        private UpgradeSettings.Upgrade GetNextUpgrade(BaseOven oven, FeatureType type)
        {
            string ovenName = _ovensPrefab.FirstOrDefault((x) => x.Value == oven.PrefabName).Key;

            _furnacesData.TryGetValue(oven.net.ID.Value, out var furnaceData);
            int currentLevel = type switch
            {
                FeatureType.SmeltingSpeed => furnaceData?.levels.smeltingSpeed ?? 0,
                FeatureType.FuelSpeed => furnaceData?.levels.fuelSpeed ?? 0,
                FeatureType.ResourceOutput => furnaceData?.levels.resourceOutput ?? 0,
                _ => furnaceData?.levels.charcoalMultiplier ?? 0,
            };

            UpgradeSettings settings = _config.Upgrades[ovenName];
            UpgradeSettings.Upgrade[] upgrades = type switch
            {
                FeatureType.SmeltingSpeed => settings.SmeltingSpeed,
                FeatureType.FuelSpeed => settings.FuelSpeed,
                FeatureType.ResourceOutput => settings.ResourceOutput,
                _ => settings.CharcoalMultiplier,
            };

            return upgrades.Count() > currentLevel
                ? upgrades[currentLevel]
                : null;
        }

        #region Splitter & Auto Fuel Helpers

        private OvenInfo GetOvenInfo(BaseOven oven)
        {
            OvenComponent component = oven.GetComponent<OvenComponent>();

            float ETA = GetSmeltDuration(oven) / oven.smeltSpeed;
            float fuelNeeded = GetFuelNeeded(oven, ETA);

            return new OvenInfo()
            {
                ETA = ETA,
                FuelNeeded = fuelNeeded
            };
        }
        private static void Debug(string msg)
        {
            _instance.Puts(msg);
        }
        private float GetFuelNeeded(BaseOven oven, float ETA)
        {
            OvenComponent component = oven.GetComponent<OvenComponent>();
            float fuelSpeedMultiplier = component ? component.Properties.FuelSpeed : 1f;
            float fuel = oven.fuelType.GetComponent<ItemModBurnable>().fuelAmount;

            return (float)Math.Ceiling(ETA * (oven.cookingTemperature / 200.0f) / fuel * fuelSpeedMultiplier);
        }

        private float GetSmeltDuration(BaseOven oven)
        {
            float ETA = 0f;

            int inputEndSlot = oven._inputSlotIndex + oven.inputSlots;
            for (int i = oven._inputSlotIndex; i < inputEndSlot; i++)
            {
                Item item = oven.inventory.GetSlot(i);
                if (item == null)
                    continue;

                if (!item.info.TryGetComponent<ItemModCookable>(out var cookable))
                    continue;

                ETA += cookable.cookTime * item.amount;
            }
            return ETA;
        }

        private int FindMatchingSlotIndex(BaseOven oven, ItemContainer container, out Item existingItem, ItemDefinition itemType, List<int> indexBlacklist)
        {
            existingItem = null;

            int firstIndex = -1;
            int inputSlotsMin = oven._inputSlotIndex;
            int inputSlotsMax = oven._inputSlotIndex + oven.inputSlots;
            Dictionary<int, Item> existingItems = new Dictionary<int, Item>();

            for (int i = inputSlotsMin; i < inputSlotsMax; ++i)
            {
                if (indexBlacklist.Contains(i))
                    continue;

                Item itemSlot = container.GetSlot(i);
                if (itemSlot == null || (itemType != null && itemSlot.info == itemType))
                {
                    if (itemSlot != null)
                        existingItems.Add(i, itemSlot);

                    if (firstIndex == -1)
                    {
                        existingItem = itemSlot;
                        firstIndex = i;
                    }
                }
            }

            if (existingItems.Count <= 0 && firstIndex != -1)
            {
                return firstIndex;
            }
            else if (existingItems.Count > 0)
            {
                var largestStackItem = existingItems.OrderByDescending(x => x.Value.amount).First();
                existingItem = largestStackItem.Value;
                return existingItem.position;
            }

            existingItem = null;
            return -1;
        }

        #endregion

        private bool IsVanillaFurnace(BaseOven oven)
        {
            if (!_furnacesData.TryGetValue(oven.net.ID.Value, out var furnaceData))
                return true;

            return furnaceData.levels.smeltingSpeed == 0
                && furnaceData.levels.fuelSpeed == 0
                && furnaceData.levels.charcoalMultiplier == 0
                && furnaceData.levels.resourceOutput == 0;
        }

        private bool IsTeamMate(BasePlayer player, ulong targetId)
            => player.Team != null && player.Team.members.Contains(targetId);

        private bool HasPermission(BasePlayer player, string permissionName)
        {
            return player.IsAdmin || permission.UserHasPermission(player.UserIDString, permissionName);
        }

        #endregion

        #region Components

        private class OvenController
        {
            private readonly HashSet<OvenComponent> _components = new HashSet<OvenComponent>();

            #region Management

            public void Register(OvenComponent component)
            {
                _components.Add(component);
            }

            public void Unregister(OvenComponent component)
            {
                _components.Remove(component);
            }

            #endregion

            #region Handlers

            public IEnumerator SetupOvens()
            {
                IEnumerable<BaseOven> ovens = BaseNetworkable.serverEntities.OfType<BaseOven>();
                foreach (var oven in ovens)
                {
                    if (!oven.OwnerID.IsSteamId())
                        continue;

                    if (!_instance._ovensPrefab.ContainsValue(oven.PrefabName))
                        continue;

                    OvenComponent component = InstallComponent(oven);
                    yield return null;

                    if (oven.IsOn())
                    {
                        oven.StopCooking();
                        component.StartCooking();
                    }

                    yield return null;
                }
            }

            public OvenComponent InstallComponent(BaseOven oven)
            {
                OvenComponent component = oven.gameObject.AddComponent<OvenComponent>();
                Register(component);

                component.Initialize(this);
                return component;
            }

            public void CleanupOvens()
            {
                foreach (var component in _components)
                {
                    BaseOven oven = component.GetComponent<BaseOven>();
                    if (oven.IsOn())
                    {
                        component.StopCooking();
                        oven.StartCooking();
                    }

                    UnityEngine.Object.Destroy(component);
                }
            }

            #endregion
        }

        private class OvenComponent : FacepunchBehaviour
        {
            private BaseOven _oven;
            private OvenController _controller;

            public OvenProperties Properties { get; set; }

            private readonly Hash<ItemId, float> _cachedSmelts = new Hash<ItemId, float>();

            #region Unity Callbacks

            private void Awake()
            {
                _oven = GetComponent<BaseOven>();
            }

            private void OnDestroy()
            {
                _controller.Unregister(this);
            }

            #endregion

            #region Initialization

            public void Initialize(OvenController controller)
            {
                _controller = controller;
                UpdateProperties();
            }

            #endregion

            #region Public Methods

            public void StartCooking()
            {
                if (_oven.FindBurnable() != null || _oven.CanRunWithNoFuel)
                {
                    _oven.inventory.temperature = _oven.cookingTemperature;
                    _oven.UpdateAttachmentTemperature();
                    InvokeRepeating(Cook, 0.5f, 0.5f);
                    _oven.SetFlag(BaseEntity.Flags.On, true);
                    Interface.CallHook("OnOvenStarted", _oven);
                }
            }

            public void StopCooking()
            {
                CancelInvoke(Cook);
                _oven.StopCooking();
            }

            public void UpdateProperties()
            {
                if (Properties == null)
                    Properties = new OvenProperties();

                Properties.SmeltingSpeed = _config.Features.SmeltingSpeed
                    ? (_instance.GetCurrentUpgrade(_oven, FeatureType.SmeltingSpeed)?.Multiplier ?? _config.Defaults.SmeltingSpeed)
                    : _config.Defaults.SmeltingSpeed;

                Properties.FuelSpeed = _config.Features.FuelSpeed
                    ? (_instance.GetCurrentUpgrade(_oven, FeatureType.FuelSpeed)?.Multiplier ?? _config.Defaults.FuelSpeed)
                    : _config.Defaults.FuelSpeed;

                Properties.ResourceOutput = _config.Features.ResourceOutput
                    ? (_instance.GetCurrentUpgrade(_oven, FeatureType.ResourceOutput)?.Multiplier ?? _config.Defaults.ResourceOutput)
                    : _config.Defaults.ResourceOutput;

                Properties.CharcoalMultiplier = _config.Features.CharcoalMultiplier
                    ? (_instance.GetCurrentUpgrade(_oven, FeatureType.CharcoalMultiplier)?.Multiplier ?? _config.Defaults.CharcoalMultiplier)
                    : _config.Defaults.CharcoalMultiplier;
            }

            #endregion

            #region Private Methods

            private void Cook()
            {
                Item fuel = _oven.FindBurnable();
                if (Interface.CallHook("OnOvenCook", _oven, fuel) != null)
                    return;

                if (fuel == null && !_oven.CanRunWithNoFuel)
                {
                    StopCooking();
                    return;
                }

                if (_oven.inventory.temperature < _oven.cookingTemperature)
                {
                    StopCooking();
                    return;
                }

                foreach (Item item in _oven.inventory.itemList)
                {
                    if (IsCookable(item) && !item.HasFlag(global::Item.Flag.Cooking))
                    {
                        item.SetFlag(global::Item.Flag.Cooking, true);
                        item.MarkDirty();
                    }
                }

                foreach (var itemId in _cachedSmelts.Keys.ToArray())
                {
                    if (!_oven.inventory.itemList.Any((x) => x.uid == itemId))
                        _cachedSmelts.Remove(itemId);
                }

                SmeltItems();

                BaseEntity slot = _oven.GetSlot(BaseEntity.Slot.FireMod);
                if (slot) slot.SendMessage("Cook", 0.5f, UnityEngine.SendMessageOptions.DontRequireReceiver);

                if (fuel != null)
                {
                    ItemModBurnable burnable = fuel.info.GetComponent<ItemModBurnable>();
                    fuel.fuel -= 0.5f * (_oven.cookingTemperature / 200f) * Properties.SmeltingSpeed * Properties.FuelSpeed;

                    if (!fuel.HasFlag(global::Item.Flag.Cooking))
                    {
                        fuel.SetFlag(global::Item.Flag.Cooking, true);
                        fuel.MarkDirty();
                    }

                    if (fuel.fuel <= 0f)
                        ConsumeFuel(fuel, burnable);
                }

                Interface.CallHook("OnOvenCooked", _oven, fuel, slot);
            }

            private void SmeltItems()
            {
                Item[] itemList = _oven.inventory.itemList.Where(IsCookable).ToArray();

                foreach (var item in itemList)
                {
                    if (!item.info.TryGetComponent<ItemModCookable>(out var cookable))
                    {
                        continue;
                    }

                    if (!cookable.CanBeCookedByAtTemperature(item.temperature) || item.cookTimeLeft < 0)
                    {
                        if (cookable.setCookingFlag && item.HasFlag(global::Item.Flag.Cooking))
                        {
                            item.SetFlag(global::Item.Flag.Cooking, false);
                            item.MarkDirty();
                        }

                        continue;
                    }

                    if (cookable.setCookingFlag && !item.HasFlag(global::Item.Flag.Cooking))
                    {
                        item.SetFlag(global::Item.Flag.Cooking, true);
                        item.MarkDirty();
                    }

                    int itemSlots = itemList.Where((x) => x.info == item.info).Count();
                    item.cookTimeLeft -= 0.5f * _oven.GetSmeltingSpeed() / itemSlots * Properties.SmeltingSpeed;

                    if (item.cookTimeLeft > 0)
                    {
                        item.MarkDirty();
                        continue;
                    }

                    float nextElapsed = item.cookTimeLeft * -1f;
                    item.cookTimeLeft = cookable.cookTime - nextElapsed % cookable.cookTime;

                    int amountToConsume = Mathf.Min(item.amount, 1 + Mathf.FloorToInt(nextElapsed / cookable.cookTime));

                    if (item.amount > amountToConsume)
                    {
                        item.amount -= amountToConsume;
                        item.MarkDirty();
                    }
                    else
                    {
                        item.Remove();
                    }

                    if (!cookable.becomeOnCooked)
                        continue;

                    int producedAmount = GetAmountWithBonus(item.uid, cookable.amountOfBecome * amountToConsume, Properties.ResourceOutput);

                    Item itemProduced = ItemManager.Create(cookable.becomeOnCooked, producedAmount);
                    if (itemProduced != null && !itemProduced.MoveToContainer(item.parent))
                    {
                        StopCooking();
                        itemProduced.Drop(item.parent.dropPosition, item.parent.dropVelocity);
                    }
                }
            }

            private void ConsumeFuel(Item fuel, ItemModBurnable burnable)
            {
                if (Interface.CallHook("OnFuelConsume", _oven, fuel, burnable) != null)
                    return;

                float nextElapsed = fuel.fuel * -1f;
                int amountToConsume = Mathf.Min(fuel.amount, 1 + Mathf.FloorToInt(nextElapsed / burnable.fuelAmount));

                if (_oven.allowByproductCreation && burnable.byproductItem != null && UnityEngine.Random.Range(0f, 1f) > burnable.byproductChance)
                {
                    int producedAmount = GetAmountWithBonus(fuel.uid, burnable.byproductAmount * amountToConsume, Properties.CharcoalMultiplier);
                    Item item = ItemManager.Create(burnable.byproductItem, producedAmount);
                    if (!item.MoveToContainer(_oven.inventory))
                    {
                        StopCooking();
                        item.Drop(_oven.inventory.dropPosition, _oven.inventory.dropVelocity);
                    }
                }

                if (fuel.amount <= amountToConsume)
                {
                    fuel.Remove();
                    return;
                }

                fuel.UseItem(amountToConsume);
                fuel.fuel = burnable.fuelAmount - nextElapsed % burnable.fuelAmount;
                fuel.MarkDirty();

                Interface.CallHook("OnFuelConsumed", _oven, fuel, burnable);
            }

            #endregion

            #region Helpers

            private bool IsCookable(Item item)
            {
                return item.position >= _oven._inputSlotIndex
                    && item.position < _oven._inputSlotIndex + _oven.inputSlots;
            }

            private int GetAmountWithBonus(ItemId itemId, int currentAmount, float multiplier)
            {
                int amount = currentAmount * Mathf.FloorToInt(multiplier);
                float fraction = (multiplier % 1) * currentAmount;

                float accumulated = fraction;
                if (_cachedSmelts.ContainsKey(itemId))
                    accumulated += _cachedSmelts[itemId];

                int bonus = Mathf.FloorToInt(accumulated);
                amount += bonus;

                if ((accumulated % 1) > 0)
                    _cachedSmelts[itemId] = accumulated % 1;
                else _cachedSmelts.Remove(itemId);

                return amount;
            }

            #endregion
        }

        private class OvenProperties
        {
            public float SmeltingSpeed { get; set; }
            public float FuelSpeed { get; set; }
            public float ResourceOutput { get; set; }
            public float CharcoalMultiplier { get; set; }
        }

        #endregion

        #region UI

        private const string UI_UPGRADE_BUTTON = "FurnaceUpgrades.UpgradeButton";
        private const string UI_OVEN_STATUS = "FurnaceUpgrades.OvenStatus";
        private const string UI_OVERLAY = "FurnaceUpgrades.Overlay";
        private const string UI_LEFT_PANEL = "FurnaceUpgrades.LeftPanel";

        private void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UI_UPGRADE_BUTTON);
            CuiHelper.DestroyUi(player, UI_OVEN_STATUS);
            CloseModal(player);
        }

        #region Upgrade Button

        private void ShowUpgradeButton(BasePlayer player, BaseOven oven)
        {
            string name = _ovensPrefab.First((x) => x.Value == oven.PrefabName).Key;
            var uiSettings = name == "electric.furnace"
                ? _config.UI.UpgradeButtonElectricFurnace
                : _config.UI.UpgradeButton;

            UIBuilder ui = new UIBuilder();
            ui.AddButton(
                "Hud.Menu",
                $"furnaceupgrades.ui show {oven.net.ID.Value}",
                GetMessage(player, "UI.UpgradeButton"),
                uiSettings.BackgroundColor,
                uiSettings.FontSize,
                uiSettings.Anchor,
                uiSettings.Offset,
                UI_UPGRADE_BUTTON
            );

            ui.Add(player);
        }

        #endregion

        #region Oven Status

        private void ShowOvenStatus(BasePlayer player, BaseOven oven)
        {
            if (_config.UI.DisableStatusPanel)
                return;

            if (!oven.TryGetComponent<OvenComponent>(out var component))
                return;

            UIBuilder ui = new UIBuilder();
            ui.AddPanel(
                "Hud.Menu",
                _config.UI.StatusPanel.BackgroundColor,
                _config.UI.StatusPanel.Anchor,
                _config.UI.StatusPanel.Offset,
                null,
                null,
                UI_OVEN_STATUS
            );

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(GetMessage(player, "UI.StatusTitle"));

            if (_config.Features.SmeltingSpeed)
                stringBuilder.AppendLine(GetMessage(player, "UI.SmeltingSpeedStatus", component.Properties.SmeltingSpeed));
            if (_config.Features.FuelSpeed && oven.fuelSlots > 0)
                stringBuilder.AppendLine(GetMessage(player, "UI.FuelSpeedStatus", component.Properties.FuelSpeed));
            if (_config.Features.ResourceOutput)
                stringBuilder.AppendLine(GetMessage(player, "UI.ResourceOutputStatus", component.Properties.ResourceOutput));
            if (_config.Features.CharcoalMultiplier && oven.fuelSlots > 0)
                stringBuilder.AppendLine(GetMessage(player, "UI.CharcoalMultiplierStatus", component.Properties.CharcoalMultiplier));

            stringBuilder.Replace(Environment.NewLine, "\n");
            ui.AddText(UI_OVEN_STATUS, stringBuilder.ToString()[..^1], "1 1 1 1", _config.UI.StatusPanel.FontSize, "0 0 1 1", "6 6 -6 -6", TextAnchor.UpperLeft);
            ui.Add(player);
        }

        #endregion

        #region Modal

        private void CloseModal(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UI_OVERLAY);
            _openModals.Remove(player.userID);
        }

        private void ShowModal(BasePlayer player, BaseOven oven)
        {
            if (!oven.TryGetComponent<OvenComponent>(out var component))
                return;

            string ovenName = _ovensPrefab.FirstOrDefault((x) => x.Value == oven.PrefabName).Key;
            if (!_furnacesData.TryGetValue(oven.net.ID.Value, out var furnaceData))
                furnaceData = new FurnaceData();

            UIBuilder ui = new UIBuilder();
            // Draw the main panel
            ui.AddPanel("OverlayNonScaled", "0.1 0.1 0.14 0.25", "0 0 1 1", "0 0 0 0", string.Empty, null, UI_OVERLAY)
                .AddPanel(UI_OVERLAY, "0.1 0.1 0.14 1", "0 0 1 1", "0 0 0 0", null, "assets/content/ui/ui.background.transparent.radial.psd")
                .AllowControls(UI_OVERLAY, false, true)
                .AddImage(UI_OVERLAY, "assets/icons/close.png", UIBuilder.Colors.RED, "1 1 1 1", "-48 -48 -24 -24")
                .AddButton(UI_OVERLAY, "furnaceupgrades.ui close", "", "0 0 0 0", 20, "1 1 1 1", "-48 -48 -24 -24");

            // Draw attribute boxes
            int featureIndex = 0;
            if (_config.Features.SmeltingSpeed)
                RenderUpgradeAttributeBox(
                    player,
                    ref ui,
                    featureIndex++,
                    GetMessage(player, "UI.SmeltingSpeed.Title"),
                    GetMessage(player, "UI.SmeltingSpeed.Description"),
                    component.Properties.SmeltingSpeed,
                    GetNextUpgrade(oven, FeatureType.SmeltingSpeed),
                    $"furnaceupgrades.ui upgrade {FeatureType.SmeltingSpeed}"
                );

            if (_config.Features.ResourceOutput)
                RenderUpgradeAttributeBox(
                    player,
                    ref ui,
                    featureIndex++,
                    GetMessage(player, "UI.ResourceOutput.Title"),
                    GetMessage(player, "UI.ResourceOutput.Description"),
                    component.Properties.ResourceOutput,
                    GetNextUpgrade(oven, FeatureType.ResourceOutput),
                    $"furnaceupgrades.ui upgrade {FeatureType.ResourceOutput}"
                );

            if (_config.Features.FuelSpeed && oven.fuelSlots > 0)
                RenderUpgradeAttributeBox(
                    player,
                    ref ui,
                    featureIndex++,
                    GetMessage(player, "UI.FuelSpeed.Title"),
                    GetMessage(player, "UI.FuelSpeed.Description"),
                    component.Properties.FuelSpeed,
                    GetNextUpgrade(oven, FeatureType.FuelSpeed),
                    $"furnaceupgrades.ui upgrade {FeatureType.FuelSpeed}"
                );

            if (_config.Features.CharcoalMultiplier && oven.fuelSlots > 0)
                RenderUpgradeAttributeBox(
                    player,
                    ref ui,
                    featureIndex++,
                    GetMessage(player, "UI.CharcoalMultiplier.Title"),
                    GetMessage(player, "UI.CharcoalMultiplier.Description"),
                    component.Properties.CharcoalMultiplier,
                    GetNextUpgrade(oven, FeatureType.CharcoalMultiplier),
                    $"furnaceupgrades.ui upgrade {FeatureType.CharcoalMultiplier}"
                );

            // Middle vertical line
            ui.AddPanel(UI_OVERLAY, "1 1 1 .01", "0.5 0.5 0.5 0.5", "-0.5 -218 0.5 218", null);

            // Draw furnace
            string imageName = _images.ContainsKey($"{ovenName}_image") ? $"{ovenName}_image" : ovenName;
            string ovenImage = ImageLibrary?.Call<string>("GetImage", imageName, 0ul, true);

            ui.AddPanel(UI_OVERLAY, "0 0 0 0", "0.5 0.5 0.5 0.5", "-360 -218 -12 218", string.Empty, null, UI_LEFT_PANEL)
                .AddImage(UI_LEFT_PANEL, ovenImage, null, "0.5 0.5 0.5 0.5", "-128 -48 128 208");

            int optionIndex = 0;
            if (_config.Features.AutoFuel && HasPermission(player, AUTO_FUEL_PERMISSION) && oven.fuelSlots > 0)
                RenderOptionBox(ref ui, optionIndex++, GetMessage(player, "UI.AutoFuelOptionLabel"), furnaceData.useAutoFuel, "furnaceupgrades.ui toggle_option 0");
            if (_config.Features.AutoSplit && HasPermission(player, AUTO_SPLIT_PERMISSION))
                RenderOptionBox(ref ui, optionIndex++, GetMessage(player, "UI.AutoSplitOptionLabel"), furnaceData.useAutoSplit, "furnaceupgrades.ui toggle_option 1");

            if (optionIndex < 1)
                ui.AddPanel(UI_LEFT_PANEL, "0 0 0 0.65", "0 0 1 0", "0 0 0 32", string.Empty, "assets/content/ui/ui.background.transparent.linearltr.tga")
                    .AddText(UI_LEFT_PANEL, GetMessage(player, "UI.NoOptionAvailable"), "1 1 1 .55", 12, "0 0 1 0", "12 0 0 32", TextAnchor.MiddleLeft);

            float posY = (32f * Mathf.Max(optionIndex, 1));
            ui.AddText(UI_LEFT_PANEL, GetMessage(player, "UI.OptionsLabel"), "1 1 1 1", 26, "0 0 1 0", $"0 {posY} 0 {(posY + 32f)}");

            ui.Add(player);
        }

        private void RenderUpgradeAttributeBox(BasePlayer player, ref UIBuilder ui, int index, string title, string description, float status, UpgradeSettings.Upgrade nextUpgrade, string command = null)
        {
            float y = 218 - (100 * (index + 1) + index * 12);
            string name = CuiHelper.GetGuid();

            // The box with contents
            ui.AddPanel(UI_OVERLAY, "0 0 0 0.25", "0.5 0.5 0.5 0.5", $"12 {y} 360 {(y + 100)}", null, null, name)
                .AddText(name, title, "1 1 1 1", 18, "0 1 1 1", "10 -32 -102 -8")
                .AddText(name, description, "1 1 1 0.5", 12, "0 1 1 1", "10 -70 -10 -34", TextAnchor.UpperLeft);

            if (nextUpgrade != null)
                ui.AddButton(name, command, GetMessage(player, "UI.UpgradeTemplate", nextUpgrade.Cost), UIBuilder.Colors.GREEN, 14, "0 0 1 0", "0 0 0 28");
            else
                ui.AddButton(name, "", GetMessage(player, "UI.MaxLevelUpgrade"), UIBuilder.Colors.GRAY, 14, "0 0 1 0", "0 0 0 28");

            // The status indicator
            string statusText = nextUpgrade != null
                ? GetMessage(player, "UI.StatusTemplate", status, nextUpgrade.Multiplier)
                : GetMessage(player, "UI.StatusMaxLevel", status);
            ui.AddPanel(name, "0 0 0 0.5", "1 1 1 1", "-94 -28 -8 -8")
                .AddText(name, statusText, "1 1 1 1", 12, "1 1 1 1", "-94 -28 -8 -8", TextAnchor.MiddleCenter);
        }

        private void RenderOptionBox(ref UIBuilder ui, int index, string label, bool isChecked, string command = null)
        {
            float y = (28f + 4f) * index;
            string name = CuiHelper.GetGuid();

            ui.AddPanel(UI_LEFT_PANEL, "0 0 0 0.65", "0 0 1 0", $"0 {y} 0 {(y + 28f)}", string.Empty, "assets/content/ui/ui.background.transparent.linearltr.tga", name)
                .AddButton(name, command, isChecked ? "✔" : "", isChecked ? UIBuilder.Colors.GREEN : UIBuilder.Colors.GRAY, 14, "0 0 0 1", "4 4 24 -4")
                .AddText(name, label, "1 1 1 1", 12, "0 0 1 1", "32 0 0 0");
        }

        #endregion

        private class UIBuilder
        {
            public CuiElementContainer Container { get; private set; }

            public static class Colors
            {
                public const string GREEN = "0.45 0.63 0.19 1";
                public const string GRAY = "0.45 0.45 0.45 1";
                public const string RED = "0.75 0.27 0.27 1";
            }

            public UIBuilder()
            {
                Container = new CuiElementContainer();
            }

            public void Add(BasePlayer player)
            {
                CuiHelper.AddUi(player, Container);
            }

            public UIBuilder AddPanel(string parent, string color, string anchor, string offset, string material = null, string sprite = null, string name = null)
            {
                name ??= CuiHelper.GetGuid();
                Container.Add(new CuiElement()
                {
                    Name = name,
                    Parent = parent,
                    DestroyUi = name,
                    Components = {
                        ParseToRect(anchor, offset),
                        new CuiImageComponent()
                        {
                            Color = color,
                            Material = material == null ? "assets/content/ui/uibackgroundblur-ingamemenu.mat" : null,
                            Sprite = sprite
                        }
                    }
                });

                return this;
            }

            public UIBuilder AddImage(string parent, string image, string color, string anchor, string offset, string name = null)
            {
                name ??= CuiHelper.GetGuid();
                CuiElement element = new CuiElement()
                {
                    Name = name,
                    Parent = parent,
                    DestroyUi = name,
                    Components = {
                        ParseToRect(anchor, offset)
                    }
                };

                if (image.StartsWith("assets/"))
                {
                    element.Components.Add(new CuiImageComponent()
                    {
                        Color = color,
                        Sprite = image
                    });
                }
                else
                {
                    bool isImgId = image.All(char.IsNumber);
                    element.Components.Add(new CuiRawImageComponent()
                    {
                        Color = color,
                        Url = isImgId ? null : image,
                        Png = isImgId ? image : null
                    });
                }

                Container.Add(element);
                return this;
            }

            public UIBuilder AddText(string parent, string text, string color, int fontSize, string anchor, string offset, TextAnchor textAnchor = TextAnchor.MiddleLeft, string name = null)
            {
                name ??= CuiHelper.GetGuid();
                Container.Add(new CuiElement()
                {
                    Name = name,
                    Parent = parent,
                    DestroyUi = name,
                    Components = {
                        ParseToRect(anchor, offset),
                        new CuiTextComponent()
                        {
                            Text = text,
                            Color = color,
                            Align = textAnchor,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = fontSize
                        }
                    }
                });

                return this;
            }

            public UIBuilder AddButton(string parent, string command, string text, string color, int fontSize, string anchor, string offset, string name = null)
            {
                name ??= CuiHelper.GetGuid();
                CuiButton button = new CuiButton()
                {
                    Text = {
                        Align = TextAnchor.MiddleCenter,
                        Text = text,
                        FontSize = fontSize
                    },
                    Button = {
                        Command = command,
                        Color = color,
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    }
                };

                CuiRectTransformComponent rect = ParseToRect(anchor, offset);
                button.RectTransform.AnchorMin = rect.AnchorMin;
                button.RectTransform.AnchorMax = rect.AnchorMax;
                button.RectTransform.OffsetMin = rect.OffsetMin;
                button.RectTransform.OffsetMax = rect.OffsetMax;

                Container.Add(button, parent, name, name);
                return this;
            }

            public UIBuilder AllowControls(string parent, bool keyboard = true, bool cursor = true)
            {
                CuiElement element = new CuiElement();
                element.Parent = parent;

                if (keyboard)
                    element.Components.Add(new CuiNeedsKeyboardComponent());
                if (cursor)
                    element.Components.Add(new CuiNeedsCursorComponent());

                Container.Add(element);
                return this;
            }

            private CuiRectTransformComponent ParseToRect(string anchor, string offset)
            {
                string[] anchors = anchor.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
                string[] offsets = offset.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);

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

        #region Commands

        [ConsoleCommand("furnaceupgrades.ui")]
        private void ConsoleCommandUI(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg?.Player();
            if (!player || !arg.HasArgs())
                return;

            switch (arg.Args[0].ToLower())
            {
                case "show":
                    {
                        if (!arg.HasArgs(2) || !ulong.TryParse(arg.Args[1], out ulong ovenId))
                            return;

                        BaseOven oven = GetOvenById(new NetworkableId(ovenId));
                        if (!oven || !oven.HasComponent<OvenComponent>())
                            return;

                        _openModals[player.userID] = oven;
                        ShowModal(player, oven);
                        return;
                    }

                case "close":
                    {
                        CloseModal(player);
                        return;
                    }

                case "toggle_option":
                    {
                        if (!_openModals.ContainsKey(player.userID))
                            return;

                        if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out int type))
                            return;

                        if (!HasPermission(player, USE_PERMISSION))
                        {
                            SendMessage(player, "Error.NoPermission");
                            return;
                        }

                        if (type == 0 && !HasPermission(player, AUTO_FUEL_PERMISSION))
                        {
                            SendMessage(player, "Error.NoPermission.AutoFuel");
                            return;
                        }
                        else if (type == 1 && !HasPermission(player, AUTO_SPLIT_PERMISSION))
                        {
                            SendMessage(player, "Error.NoPermission.AutoSplit");
                            return;
                        }

                        BaseOven oven = _openModals[player.userID];
                        if (_config.General.OwnerOnly && oven.OwnerID != player.userID)
                        {
                            SendMessage(player, "Error.OwnerOnly");
                            return;
                        }

                        if (_config.General.OnlyTeamMates && player.userID != oven.OwnerID && !IsTeamMate(player, oven.OwnerID))
                        {
                            SendMessage(player, "Error.OnlyTeamMates");
                            return;
                        }

                        if (!_furnacesData.TryGetValue(oven.net.ID.Value, out var furnaceData))
                            furnaceData = new FurnaceData();

                        if (type == 0)
                            furnaceData.useAutoFuel = !furnaceData.useAutoFuel;
                        else if (type == 1)
                            furnaceData.useAutoSplit = !furnaceData.useAutoSplit;
                        else
                            return;

                        _furnacesData[oven.net.ID.Value] = furnaceData;
                        SaveData();

                        ShowModal(player, oven);
                        return;
                    }

                case "upgrade":
                    {
                        if (!_openModals.ContainsKey(player.userID))
                            return;

                        if (!arg.HasArgs(2) || !Enum.TryParse<FeatureType>(arg.Args[1], out var upgradeType))
                            return;

                        if (!HasPermission(player, USE_PERMISSION))
                        {
                            SendMessage(player, "Error.NoPermission");
                            return;
                        }

                        BaseOven oven = _openModals[player.userID];
                        if (_config.General.OwnerOnly && oven.OwnerID != player.userID)
                        {
                            SendMessage(player, "Error.OwnerOnly");
                            return;
                        }

                        if (_config.General.OnlyTeamMates && oven.OwnerID != player.userID && !IsTeamMate(player, oven.OwnerID))
                        {
                            SendMessage(player, "Error.OnlyTeamMates");
                            return;
                        }

                        string ovenName = _ovensPrefab.FirstOrDefault((x) => x.Value == oven.PrefabName).Key;
                        if (!_config.Upgrades.ContainsKey(ovenName) || !oven.TryGetComponent<OvenComponent>(out var component))
                            return;

                        UpgradeResult result = TryUpgradeAttribute(player, oven, upgradeType);
                        if (result.Type == UpgradeResult.UpgradeResultType.MaxLevel)
                            return;

                        if (result.Type == UpgradeResult.UpgradeResultType.NotEnoughMoney)
                        {
                            SendMessage(player, "Error.NotEnoughMoney");
                            return;
                        }

                        if (!string.IsNullOrEmpty(_config.Visual.UpgradeEffect))
                            Effect.server.Run(
                                _config.Visual.UpgradeEffect,
                                oven.transform.position,
                                Vector3.zero,
                                null,
                                true
                            );

                        component.UpdateProperties();
                        if (oven.IsOn())
                            component.StartCooking();

                        string messageKey = upgradeType switch
                        {
                            FeatureType.SmeltingSpeed => "Notification.SmeltingSpeedUpgraded",
                            FeatureType.FuelSpeed => "Notification.FuelSpeedUpgraded",
                            FeatureType.ResourceOutput => "Notification.ResourceOutputUpgraded",
                            _ => "Notification.CharcoalMultiplierUpgraded"
                        };

                        SendMessage(player, messageKey, result.Cost);

                        ShowOvenStatus(player, oven);
                        ShowModal(player, oven);
                        return;
                    }
            }
        }

        #endregion

    }
}