using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;
using VLB;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("Remover Tool", "Reneb/Fuji/Arainrr/Tryhard", "4.3.43", ResourceId = 651)]
    [Description("Building and entity removal tool")]
    public class RemoverTool : RustPlugin
    {
        #region Fields

        [PluginReference]
        private readonly Plugin Friends, ServerRewards, Clans, Economics, ImageLibrary, BuildingOwners, RustTranslationAPI, NoEscape;

        private const string ECONOMICS_KEY = "economics";
        private const string SERVER_REWARDS_KEY = "serverrewards";

        private const string PERMISSION_ALL = "removertool.all";
        private const string PERMISSION_ADMIN = "removertool.admin";
        private const string PERMISSION_NORMAL = "removertool.normal";
        private const string PERMISSION_TARGET = "removertool.target";
        private const string PERMISSION_EXTERNAL = "removertool.external";
        private const string PERMISSION_OVERRIDE = "removertool.override";
        private const string PERMISSION_STRUCTURE = "removertool.structure";

        private const string PREFAB_ITEM_DROP = "assets/prefabs/misc/item drop/item_drop.prefab";

        private const int LAYER_ALL = 1 << 0 | 1 << 8 | 1 << 21;
        private const int LAYER_TARGET = ~(1 << 2 | 1 << 3 | 1 << 4 | 1 << 10 | 1 << 18 | 1 << 28 | 1 << 29);

        private static RemoverTool _instance;
        private static BUTTON _removeButton;
        private static RemoveMode _removeMode;

        private readonly object _false = false;
        private bool _configChanged;
        private bool _removeOverride;
        private Coroutine _removeAllCoroutine;
        private Coroutine _removeStructureCoroutine;
        private Coroutine _removeExternalCoroutine;
        private Coroutine _removePlayerEntityCoroutine;

        private StringBuilder _debugStringBuilder;
        private Hash<ulong, float> _entitySpawnedTimes;
        private readonly Hash<ulong, float> _cooldownTimes = new Hash<ulong, float>();

        private enum RemoveMode
        {
            None,
            NoHeld,
            MeleeHit,
            SpecificTool
        }

        private enum RemoveType
        {
            None,
            All,
            Admin,
            Normal,
            External,
            Structure
        }

        private enum PlayerEntityRemoveType
        {
            All,
            Cupboard,
            Building
        }

        #endregion Fields

        #region Oxide Hooks

        private void Init()
        {
            _instance = this;
            permission.RegisterPermission(PERMISSION_ALL, this);
            permission.RegisterPermission(PERMISSION_ADMIN, this);
            permission.RegisterPermission(PERMISSION_NORMAL, this);
            permission.RegisterPermission(PERMISSION_TARGET, this);
            permission.RegisterPermission(PERMISSION_OVERRIDE, this);
            permission.RegisterPermission(PERMISSION_EXTERNAL, this);
            permission.RegisterPermission(PERMISSION_STRUCTURE, this);

            Unsubscribe(nameof(OnHammerHit));
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnEntityKill));
            Unsubscribe(nameof(OnPlayerAttack));
            Unsubscribe(nameof(OnActiveItemChanged));
            Unsubscribe(nameof(OnRaidBlock));
            Unsubscribe(nameof(OnCombatBlock));

            foreach (var perm in _configData.permission.Keys)
            {
                if (!permission.PermissionExists(perm, this))
                {
                    permission.RegisterPermission(perm, this);
                }
            }
            cmd.AddChatCommand(_configData.chat.command, this, nameof(CmdRemove));
        }

        private void OnServerInitialized()
        {
            Initialize();
            UpdateConfig();
            LoadConfig();
            _removeMode = RemoveMode.None;
            if (_configData.removerMode.noHeldMode)
            {
                _removeMode = RemoveMode.NoHeld;
            }
            if (_configData.removerMode.meleeHitMode)
            {
                _removeMode = RemoveMode.MeleeHit;
            }
            if (_configData.removerMode.specificToolMode)
            {
                _removeMode = RemoveMode.SpecificTool;
            }
            if (_removeMode == RemoveMode.MeleeHit)
            {
                BaseMelee baseMelee;
                ItemDefinition itemDefinition;
                if (string.IsNullOrEmpty(_configData.removerMode.meleeHitItemShortname) ||
                        (itemDefinition = ItemManager.FindItemDefinition(_configData.removerMode.meleeHitItemShortname)) == null ||
                        (baseMelee = itemDefinition.GetComponent<ItemModEntity>()?.entityPrefab.Get()?.GetComponent<BaseMelee>()) == null)
                {
                    PrintError($"{_configData.removerMode.meleeHitItemShortname} is not an item shortname for a melee tool");
                    _removeMode = RemoveMode.None;
                }
                else
                {
                    Subscribe(baseMelee is Hammer ? nameof(OnHammerHit) : nameof(OnPlayerAttack));
                }
            }

            if (_configData.noEscape.useRaidBlocker)
            {
                Subscribe(nameof(OnRaidBlock));
            }
            if (_configData.noEscape.useCombatBlocker)
            {
                Subscribe(nameof(OnCombatBlock));
            }

            if (_configData.global.entityTimeLimit)
            {
                _entitySpawnedTimes = new Hash<ulong, float>();
                Subscribe(nameof(OnEntitySpawned));
                Subscribe(nameof(OnEntityKill));
            }
            if (_configData.global.logToFile)
            {
                _debugStringBuilder = new StringBuilder();
            }

            if (_removeMode == RemoveMode.MeleeHit && _configData.removerMode.meleeHitEnableInHand ||
                    _removeMode == RemoveMode.SpecificTool && _configData.removerMode.specificToolEnableInHand)
            {
                Subscribe(nameof(OnActiveItemChanged));
            }

            if (!Enum.TryParse(_configData.global.removeButton, true, out _removeButton) || !Enum.IsDefined(typeof(BUTTON), _removeButton))
            {
                PrintError($"{_configData.global.removeButton} is an invalid button. The remove button has been changed to 'FIRE_PRIMARY'.");
                _removeButton = BUTTON.FIRE_PRIMARY;
                _configData.global.removeButton = _removeButton.ToString();
                SaveConfig();
            }
            if (ImageLibrary != null)
            {
                foreach (var image in _configData.imageUrls)
                {
                    AddImageToLibrary(image.Value, image.Key);
                }
                if (_configData.ui.showCrosshair)
                {
                    AddImageToLibrary(_configData.ui.crosshairImageUrl, UINAME_CROSSHAIR);
                }
            }
        }

        private void Unload()
        {
            // if (_configChanged)
            // {
            //     SaveConfig();
            // }
            SaveDebug();
            if (_removeAllCoroutine != null)
            {
                ServerMgr.Instance.StopCoroutine(_removeAllCoroutine);
            }
            if (_removeStructureCoroutine != null)
            {
                ServerMgr.Instance.StopCoroutine(_removeStructureCoroutine);
            }
            if (_removeExternalCoroutine != null)
            {
                ServerMgr.Instance.StopCoroutine(_removeExternalCoroutine);
            }
            if (_removePlayerEntityCoroutine != null)
            {
                ServerMgr.Instance.StopCoroutine(_removePlayerEntityCoroutine);
            }
            foreach (var player in BasePlayer.activePlayerList)
            {
                player.GetComponent<ToolRemover>()?.DisableTool();
            }
            _configData = null;
            _instance = null;
        }

        private void OnServerSave()
        {
            if (_configChanged)
            {
                _configChanged = false;
                timer.Once(Random.Range(0f, 60f), SaveConfig);
            }
            if (_configData.global.logToFile)
            {
                timer.Once(Random.Range(0f, 60f), SaveDebug);
            }
            if (_configData.global.entityTimeLimit && _entitySpawnedTimes != null)
            {
                var currentTime = Time.realtimeSinceStartup;
                foreach (var entry in _entitySpawnedTimes.ToArray())
                {
                    if (currentTime - entry.Value > _configData.global.limitTime)
                    {
                        _entitySpawnedTimes.Remove(entry.Key);
                    }
                }
            }
        }

        private void OnEntitySpawned(BaseEntity entity)
        {
            if (entity == null || entity.net == null)
            {
                return;
            }
            // if (!CanEntityBeSaved(entity)) return;
            _entitySpawnedTimes[entity.net.ID.Value] = Time.realtimeSinceStartup;
        }

        private void OnEntityKill(BaseEntity entity)
        {
            if (entity == null || entity.net == null)
            {
                return;
            }
            _entitySpawnedTimes.Remove(entity.net.ID.Value);
        }

        private object OnPlayerAttack(BasePlayer player, HitInfo info)
        {
            return OnHammerHit(player, info);
        }

        private object OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (player == null || info.HitEntity == null)
            {
                return null;
            }
            var toolRemover = player.GetComponent<ToolRemover>();
            if (toolRemover == null)
            {
                return null;
            }
            if (!IsMeleeTool(player))
            {
                return null;
            }
            toolRemover.HitEntity = info.HitEntity;
            return _false;
        }

        private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (newItem == null)
            {
                return;
            }
            if (player == null || !player.userID.IsSteamId())
            {
                return;
            }
            if (IsToolRemover(player))
            {
                return;
            }
            if (_removeMode == RemoveMode.MeleeHit && IsMeleeTool(newItem))
            {
                ToggleRemove(player, RemoveType.Normal);
                return;
            }
            if (_removeMode == RemoveMode.SpecificTool && IsSpecificTool(newItem))
            {
                ToggleRemove(player, RemoveType.Normal);
            }
        }

        #endregion Oxide Hooks

        #region Initializing

        private readonly HashSet<Construction> _constructions = new HashSet<Construction>();
        private readonly Dictionary<string, int> _itemShortNameToItemId = new Dictionary<string, int>();
        private readonly Dictionary<string, string> _prefabNameToStructure = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _shortPrefabNameToDeployable = new Dictionary<string, string>();

        private void Initialize()
        {
            foreach (var itemDefinition in ItemManager.GetItemDefinitions())
            {
                if (!_itemShortNameToItemId.ContainsKey(itemDefinition.shortname))
                {
                    _itemShortNameToItemId.Add(itemDefinition.shortname, itemDefinition.itemid);
                }
                var deployablePrefab = itemDefinition.GetComponent<ItemModDeployable>()?.entityPrefab?.resourcePath;
                if (string.IsNullOrEmpty(deployablePrefab))
                {
                    continue;
                }
                var shortPrefabName = Utility.GetFileNameWithoutExtension(deployablePrefab);
                if (!string.IsNullOrEmpty(shortPrefabName) && !_shortPrefabNameToDeployable.ContainsKey(shortPrefabName))
                {
                    _shortPrefabNameToDeployable.Add(shortPrefabName, itemDefinition.shortname);
                }
            }
            foreach (var entry in PrefabAttribute.server.prefabs)
            {
                var construction = entry.Value.Find<Construction>().FirstOrDefault();
                if (construction != null && construction.deployable == null && !string.IsNullOrEmpty(construction.info.name.english))
                {
                    _constructions.Add(construction);
                    if (!_prefabNameToStructure.ContainsKey(construction.fullName))
                    {
                        _prefabNameToStructure.Add(construction.fullName, construction.info.name.english);
                    }
                }
            }
        }

        #endregion Initializing

        #region Methods

        private static string GetRemoveTypeName(RemoveType removeType)
        {
            return _configData.removeType[removeType].displayName;
        }

        private static void DropItemContainer(ItemContainer itemContainer, Vector3 position, Quaternion rotation)
        {
            itemContainer?.Drop(PREFAB_ITEM_DROP, position, rotation, 0);
        }

        private static bool IsExternalWall(StabilityEntity stabilityEntity)
        {
            return stabilityEntity.ShortPrefabName.Contains("external");
        }

        private static bool CanEntityBeDisplayed(BaseEntity entity, BasePlayer player)
        {
            var stash = entity as StashContainer;
            return stash == null || !stash.IsHidden() || stash.PlayerInRange(player);
        }

        private static bool CanEntityBeSaved(BaseEntity entity)
        {
            if (entity is BuildingBlock)
            {
                return true;
            }
            EntitySettings entitySettings;
            if (_configData.remove.entity.TryGetValue(entity.ShortPrefabName, out entitySettings) && entitySettings.enabled)
            {
                return true;
            }
            return false;
        }

        private static bool HasEntityEnabled(BaseEntity entity)
        {
            var buildingBlock = entity as BuildingBlock;
            if (buildingBlock != null)
            {
                bool valid;
                if (_configData.remove.validConstruction.TryGetValue(buildingBlock.grade, out valid) && valid)
                {
                    return true;
                }
            }
            EntitySettings entitySettings;
            if (_configData.remove.entity.TryGetValue(entity.ShortPrefabName, out entitySettings) && entitySettings.enabled)
            {
                return true;
            }
            return false;
        }

        private static bool IsRemovableEntity(BaseEntity entity)
        {
            if (_instance._shortPrefabNameToDeployable.ContainsKey(entity.ShortPrefabName)
                    || _instance._prefabNameToStructure.ContainsKey(entity.PrefabName)
                    || _configData.remove.entity.ContainsKey(entity.ShortPrefabName))
            {
                var baseCombatEntity = entity as BaseCombatEntity;
                if (baseCombatEntity != null)
                {
                    if (baseCombatEntity.IsDead())
                    {
                        return false;
                    }
                    if (baseCombatEntity.pickup.itemTarget != null)
                    {
                        return true;
                    }
                }
                return true;
            }
            return false;
        }

        private static string GetEntityImage(string name)
        {
            if (_instance.ImageLibrary == null)
            {
                return null;
            }
            if (_configData.imageUrls.ContainsKey(name))
            {
                return GetImageFromLibrary(name);
            }
            if (_instance._itemShortNameToItemId.ContainsKey(name))
            {
                return GetImageFromLibrary(name);
            }
            return null;
        }

        private static string GetItemImage(string shortname)
        {
            if (_instance.ImageLibrary == null)
            {
                return null;
            }
            switch (shortname.ToLower())
            {
                case ECONOMICS_KEY:
                    return GetImageFromLibrary(ECONOMICS_KEY);

                case SERVER_REWARDS_KEY:
                    return GetImageFromLibrary(SERVER_REWARDS_KEY);
            }
            return GetEntityImage(shortname);
        }

        private static void TryFindEntityName(BasePlayer player, BaseEntity entity, out string displayName, out string imageName)
        {
            var target = entity as BasePlayer;
            if (target != null)
            {
                imageName = target.userID.IsSteamId() ? target.UserIDString : target.ShortPrefabName;
                displayName = $"{target.displayName} ({target.ShortPrefabName})";
                return;
            }
            EntitySettings entitySettings;
            if (_configData.remove.entity.TryGetValue(entity.ShortPrefabName, out entitySettings))
            {
                imageName = entity.ShortPrefabName;
                displayName = _instance.GetDeployableDisplayName(player, entity.ShortPrefabName, entitySettings.displayName);
                return;
            }

            string structureName;
            if (_instance._prefabNameToStructure.TryGetValue(entity.PrefabName, out structureName))
            {
                BuildingBlocksSettings buildingBlockSettings;
                if (_configData.remove.buildingBlock.TryGetValue(structureName, out buildingBlockSettings))
                {
                    imageName = structureName;
                    displayName = _instance.GetConstructionDisplayName(player, entity.PrefabName, buildingBlockSettings.displayName);
                    return;
                }
            }

            imageName = entity.ShortPrefabName;
            displayName = entity.ShortPrefabName;
        }

        private static string GetDisplayNameByCurrencyName(string language, string currencyName, long skinId)
        {
            var itemDefinition = ItemManager.FindItemDefinition(currencyName);
            if (itemDefinition != null)
            {
                var translationKey = $"{itemDefinition.shortname}_{skinId}";
                var translationValue = GetCurrencyDisplayName(translationKey, itemDefinition.displayName.english, true);
                if (skinId <= 0 || string.IsNullOrEmpty(translationValue))
                {
                    var displayName = _instance.GetItemDisplayName(language, itemDefinition.shortname);
                    if (!string.IsNullOrEmpty(displayName))
                    {
                        return displayName;
                    }
                    return itemDefinition.displayName.english;
                }
                return GetCurrencyDisplayName(translationKey, itemDefinition.displayName.english);
            }
            return GetCurrencyDisplayName(currencyName, currencyName);
        }

        private static string GetCurrencyDisplayName(string currencyName, string defaultName = null, bool readOnly = false)
        {
            string displayName;
            if (_configData.remove.displayNames.TryGetValue(currencyName, out displayName))
            {
                return displayName;
            }
            if (!readOnly)
            {
                _configData.remove.displayNames.Add(currencyName, defaultName);
                _instance._configChanged = true;
            }
            return defaultName;
        }

        private static PermissionSettings GetPermissionSettings(BasePlayer player)
        {
            var priority = 0;
            PermissionSettings permissionSettings = null;
            foreach (var entry in _configData.permission)
            {
                if (entry.Value.priority >= priority && _instance.permission.UserHasPermission(player.UserIDString, entry.Key))
                {
                    priority = entry.Value.priority;
                    permissionSettings = entry.Value;
                }
            }
            return permissionSettings ?? new PermissionSettings();
        }

        private static Vector2 GetAnchor(string anchor)
        {
            var array = anchor.Split(' ');
            return new Vector2(float.Parse(array[0]), float.Parse(array[1]));
        }

        private static bool AddImageToLibrary(string url, string shortname, ulong skin = 0)
        {
            return (bool)_instance.ImageLibrary.Call("AddImage", url, shortname.ToLower(), skin);
        }

        private static string GetImageFromLibrary(string shortname, ulong skin = 0, bool returnUrl = false)
        {
            return string.IsNullOrEmpty(shortname) ? null : (string)_instance.ImageLibrary.Call("GetImage", shortname.ToLower(), skin, returnUrl);
        }

        #endregion Methods

        #region NoEscape

        private void OnRaidBlock(BasePlayer player)
        {
            if (_configData.noEscape.useRaidBlocker)
            {
                // Print(player, Lang("RaidBlocked", player.UserIDString));
                player.GetComponent<ToolRemover>()?.DisableTool(false);
            }
        }

        private void OnCombatBlock(BasePlayer player)
        {
            if (_configData.noEscape.useCombatBlocker)
            {
                // Print(player, Lang("CombatBlocked", player.UserIDString));
                player.GetComponent<ToolRemover>()?.DisableTool(false);
            }
        }

        private bool IsPlayerBlocked(BasePlayer player, out string reason)
        {
            if (NoEscape != null)
            {
                if (_configData.noEscape.useRaidBlocker && IsRaidBlocked(player.UserIDString))
                {
                    reason = Lang("RaidBlocked", player.UserIDString);
                    return true;
                }
                if (_configData.noEscape.useCombatBlocker && IsCombatBlocked(player.UserIDString))
                {
                    reason = Lang("CombatBlocked", player.UserIDString);
                    return true;
                }
            }

            reason = null;
            return false;
        }

        private bool IsRaidBlocked(string playerID)
        {
            return (bool)NoEscape.Call("IsRaidBlocked", playerID);
        }

        private bool IsCombatBlocked(string playerID)
        {
            return (bool)NoEscape.Call("IsCombatBlocked", playerID);
        }

        #endregion NoEscape

        #region UI

        private static class UI
        {
            public static CuiElementContainer CreateElementContainer(string parent, string panelName, string backgroundColor, string anchorMin, string anchorMax, string offsetMin = "", string offsetMax = "", bool cursor = false)
            {
                return new CuiElementContainer
                {
                    {
                        new CuiPanel
                        {
                            Image = { Color = backgroundColor },
                            RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax, OffsetMin = offsetMin, OffsetMax = offsetMax },
                            CursorEnabled = cursor
                        },
                        parent, panelName
                    }
                };
            }

            public static void CreatePanel(ref CuiElementContainer container, string panelName, string backgroundColor, string anchorMin, string anchorMax, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = backgroundColor },
                    RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax },
                    CursorEnabled = cursor
                }, panelName);
            }

            public static void CreateLabel(ref CuiElementContainer container, string panelName, string textColor, string text, int fontSize, string anchorMin, string anchorMax, TextAnchor align = TextAnchor.MiddleCenter, float fadeIn = 0f)
            {
                container.Add(new CuiLabel
                {
                    Text = { Color = textColor, FontSize = fontSize, Align = align, Text = text, FadeIn = fadeIn },
                    RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax }
                }, panelName);
            }

            public static void CreateImage(ref CuiElementContainer container, string panelName, string image, string anchorMin, string anchorMax, string color = "1 1 1 1")
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panelName,
                    Components =
                    {
                        new CuiRawImageComponent { Color = color, Png = image },
                        new CuiRectTransformComponent { AnchorMin = anchorMin, AnchorMax = anchorMax }
                    }
                });
            }

            public static void CreateImage(ref CuiElementContainer container, string panelName, int itemId, ulong skinId, string anchorMin, string anchorMax)
            {
                container.Add(new CuiPanel
                {
                    Image = { ItemId = itemId, SkinId = skinId },
                    RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax },
                    CursorEnabled = false
                }, panelName);
            }
        }

        [Flags]
        private enum UiEntry
        {
            None = 0,
            Entity = 1,
            Price = 1 << 1,
            Refund = 1 << 2,
            Auth = 1 << 3
        }

        private const string UINAME_MAIN = "RemoverToolUI_Main";
        private const string UINAME_TIMELEFT = "RemoverToolUI_TimeLeft";
        private const string UINAME_ENTITY = "RemoverToolUI_Entity";
        private const string UINAME_PRICE = "RemoverToolUI_Price";
        private const string UINAME_REFUND = "RemoverToolUI_Refund";
        private const string UINAME_AUTH = "RemoverToolUI_Auth";
        private const string UINAME_CROSSHAIR = "RemoverToolUI_Crosshair";

        private static void CreateCrosshairUI(BasePlayer player)
        {
            if (_instance.ImageLibrary == null)
            {
                return;
            }
            var image = GetImageFromLibrary(UINAME_CROSSHAIR);
            if (string.IsNullOrEmpty(image))
            {
                return;
            }
            var container = UI.CreateElementContainer("Hud", UINAME_CROSSHAIR, "0 0 0 0", _configData.ui.crosshairAnchorMin, _configData.ui.crosshairAnchorMax, _configData.ui.crosshairOffsetMin, _configData.ui.crosshairOffsetMax);
            UI.CreateImage(ref container, UINAME_CROSSHAIR, image, "0 0", "1 1", _configData.ui.crosshairColor);
            CuiHelper.DestroyUi(player, UINAME_CROSSHAIR);
            CuiHelper.AddUi(player, container);
        }

        private static void CreateMainUI(BasePlayer player, RemoveType removeType)
        {
            var container = UI.CreateElementContainer("Hud", UINAME_MAIN, _configData.ui.removerToolBackgroundColor, _configData.ui.removerToolAnchorMin, _configData.ui.removerToolAnchorMax, _configData.ui.removerToolOffsetMin, _configData.ui.removerToolOffsetMax);
            UI.CreatePanel(ref container, UINAME_MAIN, _configData.ui.removeBackgroundColor, _configData.ui.removeAnchorMin, _configData.ui.removeAnchorMax);
            UI.CreateLabel(ref container, UINAME_MAIN, _configData.ui.removeTextColor, _instance.Lang("RemoverToolType", player.UserIDString, GetRemoveTypeName(removeType)), _configData.ui.removeTextSize, _configData.ui.removeTextAnchorMin, _configData.ui.removeTextAnchorMax, TextAnchor.MiddleLeft);
            CuiHelper.DestroyUi(player, UINAME_MAIN);
            CuiHelper.AddUi(player, container);
        }

        private static void UpdateTimeLeftUI(BasePlayer player, RemoveType removeType, int timeLeft, int currentRemoved, int maxRemovable)
        {
            var container = UI.CreateElementContainer(UINAME_MAIN, UINAME_TIMELEFT, _configData.ui.timeLeftBackgroundColor, _configData.ui.timeLeftAnchorMin, _configData.ui.timeLeftAnchorMax);
            UI.CreateLabel(ref container, UINAME_TIMELEFT, _configData.ui.timeLeftTextColor, _instance.Lang("TimeLeft", player.UserIDString, timeLeft, removeType == RemoveType.Normal || removeType == RemoveType.Admin ? maxRemovable == 0 ? $"{currentRemoved} / {_instance.Lang("Unlimit", player.UserIDString)}" : $"{currentRemoved} / {maxRemovable}" : currentRemoved.ToString()), _configData.ui.timeLeftTextSize, _configData.ui.timeLeftTextAnchorMin, _configData.ui.timeLeftTextAnchorMax, TextAnchor.MiddleLeft);
            CuiHelper.DestroyUi(player, UINAME_TIMELEFT);
            CuiHelper.AddUi(player, container);
        }

        private static void UpdateEntityUI(BasePlayer player, BaseEntity targetEntity, RemovableEntityInfo? info)
        {
            var container = UI.CreateElementContainer(UINAME_MAIN, UINAME_ENTITY, _configData.ui.entityBackgroundColor, _configData.ui.entityAnchorMin, _configData.ui.entityAnchorMax);

            string displayName, imageName;
            TryFindEntityName(player, targetEntity, out displayName, out imageName);
            if (info.HasValue && !string.IsNullOrEmpty(info.Value.DisplayName.Value))
            {
                displayName = info.Value.DisplayName.Value;
            }
            UI.CreateLabel(ref container, UINAME_ENTITY, _configData.ui.entityTextColor, displayName, _configData.ui.entityTextSize, _configData.ui.entityTextAnchorMin, _configData.ui.entityTextAnchorMax, TextAnchor.MiddleLeft);
            if (_configData.ui.entityImageEnabled)
            {
                var imageAnchorMin = _configData.ui.entityImageAnchorMin;
                var imageAnchorMax = _configData.ui.entityImageAnchorMax;
                if (info.HasValue && !string.IsNullOrEmpty(info.Value.ImageId.Value))
                {
                    var image = info.Value.ImageId.Value;
                    if (!string.IsNullOrEmpty(image))
                    {
                        UI.CreateImage(ref container, UINAME_ENTITY, image, imageAnchorMin, imageAnchorMax);
                    }
                }
                else if (!string.IsNullOrEmpty(imageName))
                {
                    string shortname;
                    int itemId;
                    if (_instance._shortPrefabNameToDeployable.TryGetValue(imageName, out shortname) && _instance._itemShortNameToItemId.TryGetValue(shortname, out itemId))
                    {
                        UI.CreateImage(ref container, UINAME_ENTITY, itemId, targetEntity.skinID, imageAnchorMin, imageAnchorMax);
                    }
                    else
                    {
                        var image = GetEntityImage(imageName);
                        if (!string.IsNullOrEmpty(image))
                        {
                            UI.CreateImage(ref container, UINAME_ENTITY, image, imageAnchorMin, imageAnchorMax);
                        }
                    }
                }
            }
            CuiHelper.DestroyUi(player, UINAME_ENTITY);
            CuiHelper.AddUi(player, container);
        }

        private static void UpdatePriceUI(BasePlayer player, BaseEntity targetEntity, RemovableEntityInfo? info, bool usePrice)
        {
            Dictionary<string, CurrencyInfo> price = null;
            if (usePrice)
            {
                price = _instance.GetPrice(targetEntity, info);
            }
            var container = UI.CreateElementContainer(UINAME_MAIN, UINAME_PRICE, _configData.ui.priceBackgroundColor, _configData.ui.priceAnchorMin, _configData.ui.priceAnchorMax);
            UI.CreateLabel(ref container, UINAME_PRICE, _configData.ui.priceTextColor, _instance.Lang("Price", player.UserIDString), _configData.ui.priceTextSize, _configData.ui.priceTextAnchorMin, _configData.ui.priceTextAnchorMax, TextAnchor.MiddleLeft);
            if (price == null || price.Count == 0)
            {
                UI.CreateLabel(ref container, UINAME_PRICE, _configData.ui.price2TextColor, _instance.Lang("Free", player.UserIDString), _configData.ui.price2TextSize, _configData.ui.price2TextAnchorMin, _configData.ui.price2TextAnchorMax, TextAnchor.MiddleLeft);
            }
            else
            {
                var anchorMin = _configData.ui.Price2TextAnchorMin;
                var anchorMax = _configData.ui.Price2TextAnchorMax;
                var x = (anchorMax.y - anchorMin.y) / price.Count;
                var textSize = _configData.ui.price2TextSize - price.Count;
                var language = _instance.lang.GetLanguage(player.UserIDString);

                var i = 0;
                foreach (var entry in price)
                {
                    var externalItemInfo = info?.Price[entry.Key];
                    var displayText = !externalItemInfo.HasValue || string.IsNullOrEmpty(externalItemInfo.Value.DisplayName.Value)
                            ? $"{GetDisplayNameByCurrencyName(language, entry.Key, entry.Value.SkinId)}  <color=#00B5FF>x{entry.Value.Amount}</color>"
                            : $"{externalItemInfo.Value.DisplayName.Value} x{externalItemInfo.Value.Amount.Value}";

                    UI.CreateLabel(ref container, UINAME_PRICE, _configData.ui.price2TextColor, displayText, textSize, $"{anchorMin.x} {anchorMin.y + i * x}", $"{anchorMax.x} {anchorMin.y + (i + 1) * x}", TextAnchor.MiddleLeft);
                    if (_configData.ui.imageEnabled)
                    {
                        var imageAnchorMin = $"{anchorMax.x - _configData.ui.rightDistance - x * _configData.ui.imageScale} {anchorMin.y + i * x}";
                        var imageAnchorMax = $"{anchorMax.x - _configData.ui.rightDistance} {anchorMin.y + (i + 1) * x}";
                        if (externalItemInfo.HasValue && !string.IsNullOrEmpty(externalItemInfo.Value.ImageId.Value))
                        {
                            var image = externalItemInfo.Value.ImageId.Value;
                            if (!string.IsNullOrEmpty(image))
                            {
                                UI.CreateImage(ref container, UINAME_PRICE, image, imageAnchorMin, imageAnchorMax);
                            }
                        }
                        else
                        {
                            int itemId;
                            if (_instance._itemShortNameToItemId.TryGetValue(entry.Key, out itemId))
                            {
                                UI.CreateImage(ref container, UINAME_PRICE, itemId, entry.Value.SkinId >= 0 ? (ulong)entry.Value.SkinId : 0, imageAnchorMin, imageAnchorMax);
                            }
                            else
                            {
                                var image = GetItemImage(entry.Key);
                                if (!string.IsNullOrEmpty(image))
                                {
                                    UI.CreateImage(ref container, UINAME_PRICE, image, imageAnchorMin, imageAnchorMax);
                                }
                            }
                        }
                    }
                    i++;
                }
            }
            CuiHelper.DestroyUi(player, UINAME_PRICE);
            CuiHelper.AddUi(player, container);
        }

        private static void UpdateRefundUI(BasePlayer player, BaseEntity targetEntity, RemovableEntityInfo? info, bool useRefund)
        {
            Dictionary<string, CurrencyInfo> refund = null;
            if (useRefund)
            {
                refund = _instance.GetRefund(targetEntity, info);
            }
            var container = UI.CreateElementContainer(UINAME_MAIN, UINAME_REFUND, _configData.ui.refundBackgroundColor, _configData.ui.refundAnchorMin, _configData.ui.refundAnchorMax);
            UI.CreateLabel(ref container, UINAME_REFUND, _configData.ui.refundTextColor, _instance.Lang("Refund", player.UserIDString), _configData.ui.refundTextSize, _configData.ui.refundTextAnchorMin, _configData.ui.refundTextAnchorMax, TextAnchor.MiddleLeft);

            if (refund == null || refund.Count == 0)
            {
                UI.CreateLabel(ref container, UINAME_REFUND, _configData.ui.refund2TextColor, _instance.Lang("Nothing", player.UserIDString), _configData.ui.refund2TextSize, _configData.ui.refund2TextAnchorMin, _configData.ui.refund2TextAnchorMax, TextAnchor.MiddleLeft);
            }
            else
            {
                var anchorMin = _configData.ui.Refund2TextAnchorMin;
                var anchorMax = _configData.ui.Refund2TextAnchorMax;
                var x = (anchorMax.y - anchorMin.y) / refund.Count;
                var textSize = _configData.ui.refund2TextSize - refund.Count;
                var language = _instance.lang.GetLanguage(player.UserIDString);

                var i = 0;
                foreach (var entry in refund)
                {
                    var externalItemInfo = info?.Refund[entry.Key];
                    var displayText = !externalItemInfo.HasValue || string.IsNullOrEmpty(externalItemInfo.Value.DisplayName.Value)
                            ? $"{GetDisplayNameByCurrencyName(language, entry.Key, entry.Value.SkinId)}  <color=#00B5FF>x{entry.Value.Amount}</color>"
                            : $"{externalItemInfo.Value.DisplayName.Value} x{externalItemInfo.Value.Amount.Value}";

                    UI.CreateLabel(ref container, UINAME_REFUND, _configData.ui.refund2TextColor, displayText, textSize, $"{anchorMin.x} {anchorMin.y + i * x}", $"{anchorMax.x} {anchorMin.y + (i + 1) * x}", TextAnchor.MiddleLeft);
                    if (_configData.ui.imageEnabled)
                    {
                        var imageAnchorMin = $"{anchorMax.x - _configData.ui.rightDistance - x * _configData.ui.imageScale} {anchorMin.y + i * x}";
                        var imageAnchorMax = $"{anchorMax.x - _configData.ui.rightDistance} {anchorMin.y + (i + 1) * x}";
                        if (externalItemInfo.HasValue && !string.IsNullOrEmpty(externalItemInfo.Value.ImageId.Value))
                        {
                            var image = externalItemInfo.Value.ImageId.Value;
                            if (!string.IsNullOrEmpty(image))
                            {
                                UI.CreateImage(ref container, UINAME_REFUND, image, imageAnchorMin, imageAnchorMax);
                            }
                        }
                        else
                        {
                            int itemId;
                            if (_instance._itemShortNameToItemId.TryGetValue(entry.Key, out itemId))
                            {
                                UI.CreateImage(ref container, UINAME_REFUND, itemId, entry.Value.SkinId >= 0 ? (ulong)entry.Value.SkinId : 0, imageAnchorMin, imageAnchorMax);
                            }
                            else
                            {
                                var image = GetItemImage(entry.Key);
                                if (!string.IsNullOrEmpty(image))
                                {
                                    UI.CreateImage(ref container, UINAME_REFUND, image, imageAnchorMin, imageAnchorMax);
                                }
                            }
                        }
                    }
                    i++;
                }
            }
            CuiHelper.DestroyUi(player, UINAME_REFUND);
            CuiHelper.AddUi(player, container);
        }

        private static void UpdateAuthorizationUI(BasePlayer player, RemoveType removeType, BaseEntity targetEntity, RemovableEntityInfo? info, bool shouldPay)
        {
            string reason;
            var color = _instance.CanRemoveEntity(player, removeType, targetEntity, info, shouldPay, out reason) ? _configData.ui.allowedBackgroundColor : _configData.ui.refusedBackgroundColor;
            var container = UI.CreateElementContainer(UINAME_MAIN, UINAME_AUTH, color, _configData.ui.authorizationsAnchorMin, _configData.ui.authorizationsAnchorMax);
            UI.CreateLabel(ref container, UINAME_AUTH, _configData.ui.authorizationsTextColor, reason, _configData.ui.authorizationsTextSize, _configData.ui.authorizationsTextAnchorMin, _configData.ui.authorizationsTextAnchorMax, TextAnchor.MiddleLeft);
            CuiHelper.DestroyUi(player, UINAME_AUTH);
            CuiHelper.AddUi(player, container);
        }

        private static void DestroyAllUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UINAME_CROSSHAIR);
            CuiHelper.DestroyUi(player, UINAME_MAIN);
        }

        private static void DestroyUiEntry(BasePlayer player, UiEntry uiEntry)
        {
            switch (uiEntry)
            {
                case UiEntry.Entity:
                    CuiHelper.DestroyUi(player, UINAME_ENTITY);
                    return;

                case UiEntry.Price:
                    CuiHelper.DestroyUi(player, UINAME_PRICE);
                    return;

                case UiEntry.Refund:
                    CuiHelper.DestroyUi(player, UINAME_REFUND);
                    return;

                case UiEntry.Auth:
                    CuiHelper.DestroyUi(player, UINAME_AUTH);
                    return;
            }
        }

        #endregion UI

        #region ToolRemover Component

        #region Tool Helpers

        private static bool IsSpecificTool(BasePlayer player)
        {
            var heldItem = player.GetActiveItem();
            return IsSpecificTool(heldItem);
        }

        private static bool IsSpecificTool(Item heldItem)
        {
            if (heldItem != null && heldItem.info.shortname == _configData.removerMode.specificToolShortName)
            {
                if (_configData.removerMode.specificToolSkin < 0)
                {
                    return true;
                }
                return heldItem.skin == (ulong)_configData.removerMode.specificToolSkin;
            }
            return false;
        }

        private static bool IsMeleeTool(BasePlayer player)
        {
            var heldItem = player.GetActiveItem();
            return IsMeleeTool(heldItem);
        }

        private static bool IsMeleeTool(Item heldItem)
        {
            if (heldItem != null && heldItem.info.shortname == _configData.removerMode.meleeHitItemShortname)
            {
                if (_configData.removerMode.meleeHitModeSkin < 0)
                {
                    return true;
                }
                return heldItem.skin == (ulong)_configData.removerMode.meleeHitModeSkin;
            }
            return false;
        }

        #endregion Tool Helpers

        private class ToolRemover : FacepunchBehaviour
        {
            private const float MinInterval = 0.2f;

            public int CurrentRemoved { get; set; }
            public BaseEntity HitEntity { get; set; }
            public bool CanOverride { get; private set; }
            public BasePlayer Player { get; private set; }
            public RemoveType RemoveType { get; private set; }

            private bool _resetTime;
            private bool _shouldPay;
            private bool _shouldRefund;
            private int _removeTime;
            private int _maxRemovable;
            private float _distance;
            private float _removeInterval;

            private int _timeLeft;
            private float _lastRemove;
            private ItemId _currentItemId;
            private bool _disableInHand;

            private Item _lastHeldItem;
            private BaseEntity _targetEntity;
            private UiEntry _activeUiEntries;

            private void Awake()
            {
                Player = GetComponent<BasePlayer>();
                _currentItemId = Player.svActiveItemID;
                _disableInHand = _removeMode == RemoveMode.MeleeHit && _configData.removerMode.meleeHitDisableInHand
                        || _removeMode == RemoveMode.SpecificTool && _configData.removerMode.specificToolDisableInHand;
                if (_disableInHand)
                {
                    _lastHeldItem = Player.GetActiveItem();
                }
                if (_removeMode == RemoveMode.NoHeld)
                {
                    UnEquip();
                }
            }

            public void Init(RemoveType removeType, int removeTime, int maxRemovable, float distance, float removeInterval, bool shouldPay, bool shouldRefund, bool resetTime, bool canOverride)
            {
                RemoveType = removeType;
                CanOverride = canOverride;

                _distance = distance;
                _resetTime = resetTime;
                _removeTime = _timeLeft = removeTime;
                _removeInterval = Mathf.Max(MinInterval, removeInterval);
                if (RemoveType == RemoveType.Normal)
                {
                    _maxRemovable = maxRemovable;
                    _shouldPay = shouldPay && _configData.remove.priceEnabled;
                    _shouldRefund = shouldRefund && _configData.remove.refundEnabled;
                    _instance.PrintDebug($"{Player.displayName}({Player.userID}) have Enabled the remover tool.");
                    Interface.CallHook("OnRemoverToolActivated", Player);
                }
                else
                {
                    _maxRemovable = CurrentRemoved = 0;
                    _shouldPay = _shouldRefund = false;
                }

                DestroyAllUI(Player);
                if (_configData.ui.showCrosshair)
                {
                    CreateCrosshairUI(Player);
                }

                if (_configData.ui.enabled)
                {
                    CreateMainUI(Player, RemoveType);
                }

                CancelInvoke(RemoveUpdate);
                InvokeRepeating(RemoveUpdate, 0f, 1f);
            }

            private void RemoveUpdate()
            {
                if (_configData.ui.enabled)
                {
                    _targetEntity = GetTargetEntity();
                    UpdateTimeLeftUI(Player, RemoveType, _timeLeft, CurrentRemoved, _maxRemovable);

                    var info = RemoveType == RemoveType.Normal ? GetRemovableEntityInfo(_targetEntity, Player) : null;
                    var canShow = (info.HasValue || _targetEntity != null) && CanEntityBeDisplayed(_targetEntity, Player);
                    if (HandleUiEntry(UiEntry.Entity, canShow))
                    {
                        UpdateEntityUI(Player, _targetEntity, info);
                    }
                    if (RemoveType == RemoveType.Normal)
                    {
                        if (_configData.ui.authorizationEnabled)
                        {
                            if (HandleUiEntry(UiEntry.Auth, canShow))
                            {
                                UpdateAuthorizationUI(Player, RemoveType, _targetEntity, info, _shouldPay);
                            }
                        }
                        if (_configData.ui.priceEnabled || _configData.ui.refundEnabled)
                        {
                            canShow = canShow && (info.HasValue || HasEntityEnabled(_targetEntity));
                            if (_configData.ui.priceEnabled)
                            {
                                if (HandleUiEntry(UiEntry.Price, canShow))
                                {
                                    UpdatePriceUI(Player, _targetEntity, info, _shouldPay);
                                }
                            }
                            if (_configData.ui.refundEnabled)
                            {
                                if (HandleUiEntry(UiEntry.Refund, canShow))
                                {
                                    UpdateRefundUI(Player, _targetEntity, info, _shouldRefund);
                                }
                            }
                        }
                    }
                }

                if (_timeLeft-- <= 0)
                {
                    DisableTool();
                }
            }

            private BaseEntity GetTargetEntity()
            {
                BaseEntity target = null;
                List<RaycastHit> hitInfos = Pool.GetList<RaycastHit>();
                GamePhysics.TraceAll(Player.eyes.HeadRay(), 0f, hitInfos, _distance, LAYER_TARGET);
                foreach (var hitInfo in hitInfos)
                {
                    var hitEntity = hitInfo.GetEntity();
                    if (hitEntity != null)
                    {
                        if (target == null)
                        {
                            target = hitEntity;
                        }
                        else if (hitEntity.GetParentEntity() == target)
                        {
                            target = hitEntity;
                            break;
                        }
                    }
                }
                Pool.FreeUnmanaged(ref hitInfos);
                return target;
                // RaycastHit hitInfo;
                // if (Physics.Raycast(Player.eyes.HeadRay(), out hitInfo, _distance, LAYER_TARGET))
                // {
                //     return hitInfo.GetEntity();
                // }
                // return null;
            }

            private void Update()
            {
                if (Player == null || !Player.IsConnected || !Player.CanInteract())
                {
                    DisableTool();
                    return;
                }
                if (Player.svActiveItemID != _currentItemId)
                {
                    if (_disableInHand)
                    {
                        var heldItem = Player.GetActiveItem();
                        if (_removeMode == RemoveMode.MeleeHit && IsMeleeTool(_lastHeldItem) && !IsMeleeTool(heldItem) ||
                                _removeMode == RemoveMode.SpecificTool && IsSpecificTool(_lastHeldItem) && !IsSpecificTool(heldItem))
                        {
                            DisableTool();
                            return;
                        }
                        _lastHeldItem = heldItem;
                    }
                    if (_removeMode == RemoveMode.NoHeld)
                    {
                        if (Player.svActiveItemID.IsValid)
                        {
                            if (_configData.removerMode.noHeldDisableInHand)
                            {
                                DisableTool();
                                return;
                            }
                            UnEquip();
                        }
                    }
                    _currentItemId = Player.svActiveItemID;
                }
                if (Time.realtimeSinceStartup - _lastRemove >= _removeInterval)
                {
                    if (_removeMode == RemoveMode.MeleeHit)
                    {
                        if (HitEntity == null)
                        {
                            return;
                        }
                        _targetEntity = HitEntity;
                        HitEntity = null;
                    }
                    else
                    {
                        if (!Player.serverInput.IsDown(_removeButton))
                        {
                            return;
                        }
                        if (_removeMode == RemoveMode.SpecificTool && !IsSpecificTool(Player))
                        {
                            //rt.Print(player,rt.Lang("UsageOfRemove",player.UserIDString));
                            return;
                        }
                        _targetEntity = GetTargetEntity();
                    }
                    if (_instance.TryRemove(Player, _targetEntity, RemoveType, _shouldPay, _shouldRefund))
                    {
                        if (_resetTime)
                        {
                            _timeLeft = _removeTime;
                        }
                        if (RemoveType == RemoveType.Normal || RemoveType == RemoveType.Admin)
                        {
                            CurrentRemoved++;
                        }
                        if (_configData.global.startCooldownOnRemoved && RemoveType == RemoveType.Normal)
                        {
                            _instance._cooldownTimes[Player.userID] = Time.realtimeSinceStartup;
                        }
                    }
                    _lastRemove = Time.realtimeSinceStartup;
                }
                if (RemoveType == RemoveType.Normal && _maxRemovable > 0 && CurrentRemoved >= _maxRemovable)
                {
                    _instance.Print(Player, _instance.Lang("EntityLimit", Player.UserIDString, _maxRemovable));
                    DisableTool(false);
                }
                ;
            }

            private void UnEquip()
            {
                // Player.lastReceivedTick.activeItem = 0;
                var activeItem = Player.GetActiveItem();
                if (activeItem?.GetHeldEntity() is HeldEntity)
                {
                    var slot = activeItem.position;
                    activeItem.SetParent(null);
                    Player.Invoke(() =>
                    {
                        if (activeItem == null || !activeItem.IsValid())
                        {
                            return;
                        }
                        if (Player.inventory.containerBelt.GetSlot(slot) == null)
                        {
                            activeItem.position = slot;
                            activeItem.SetParent(Player.inventory.containerBelt);
                        }
                        else
                        {
                            Player.GiveItem(activeItem);
                        }
                    }, 0.2f);
                }
            }

            private bool HandleUiEntry(UiEntry uiEntry, bool canShow)
            {
                if (canShow)
                {
                    _activeUiEntries |= uiEntry;
                    return true;
                }

                if (_activeUiEntries.HasFlag(uiEntry))
                {
                    _activeUiEntries &= ~uiEntry;
                    DestroyUiEntry(Player, uiEntry);
                }
                return false;
            }

            public void DisableTool(bool showMessage = true)
            {
                if (showMessage)
                {
                    if (_instance != null && Player != null && Player.IsConnected)
                    {
                        if (_configData != null && _configData.chat.showMessageWhenEnabledOrDisabled)
                        {
                            _instance.Print(Player, _instance.Lang("ToolDisabled", Player.UserIDString));
                        }
                    }
                }

                if (RemoveType == RemoveType.Normal)
                {
                    if (_instance != null && Player != null)
                    {
                        _instance.PrintDebug($"{Player.displayName}({Player.userID}) have Disabled the remover tool.");
                    }
                    Interface.CallHook("OnRemoverToolDeactivated", Player);
                }
                DestroyAllUI(Player);
                Destroy(this);
            }

            private void OnDestroy()
            {
                if (_instance != null && RemoveType == RemoveType.Normal)
                {
                    if (_configData != null && !_configData.global.startCooldownOnRemoved)
                    {
                        _instance._cooldownTimes[Player.userID] = Time.realtimeSinceStartup;
                    }
                }
            }
        }

        #endregion ToolRemover Component

        #region TryRemove

        private bool TryRemove(BasePlayer player, BaseEntity targetEntity, RemoveType removeType, bool shouldPay, bool shouldRefund)
        {
            if (targetEntity == null)
            {
                Print(player, Lang("NotFoundOrFar", player.UserIDString));
                return false;
            }
            if (targetEntity.IsDestroyed)
            {
                Print(player, Lang("InvalidEntity", player.UserIDString));
                return false;
            }
            if (removeType != RemoveType.Normal)
            {
                var result = Interface.CallHook("CanAdminRemove", player, targetEntity, removeType.ToString());
                if (result != null)
                {
                    Print(player, result is string ? (string)result : Lang("BeBlocked", player.UserIDString));
                    return false;
                }
                switch (removeType)
                {
                    case RemoveType.Admin:
                        {
                            var target = targetEntity as BasePlayer;
                            if (target != null)
                            {
                                if (target.userID.IsSteamId() && target.IsConnected)
                                {
                                    target.Kick("From RemoverTool Plugin");
                                    return true;
                                }
                            }
                            DoRemove(targetEntity, _configData.removeType[RemoveType.Admin].gibs ? BaseNetworkable.DestroyMode.Gib : BaseNetworkable.DestroyMode.None);
                            return true;
                        }
                    case RemoveType.All:
                        {
                            if (_removeAllCoroutine != null)
                            {
                                Print(player, Lang("AlreadyRemoveAll", player.UserIDString));
                                return false;
                            }
                            _removeAllCoroutine = ServerMgr.Instance.StartCoroutine(RemoveAll(targetEntity, player));
                            Print(player, Lang("StartRemoveAll", player.UserIDString));
                            return true;
                        }
                    case RemoveType.External:
                        {
                            var stabilityEntity = targetEntity as StabilityEntity;
                            if (stabilityEntity == null || !IsExternalWall(stabilityEntity))
                            {
                                Print(player, Lang("NotExternalWall", player.UserIDString));
                                return false;
                            }
                            if (_removeExternalCoroutine != null)
                            {
                                Print(player, Lang("AlreadyRemoveExternal", player.UserIDString));
                                return false;
                            }
                            _removeExternalCoroutine = ServerMgr.Instance.StartCoroutine(RemoveExternal(stabilityEntity, player));
                            Print(player, Lang("StartRemoveExternal", player.UserIDString));
                            return true;
                        }
                    case RemoveType.Structure:
                        {
                            var decayEntity = targetEntity as DecayEntity;
                            if (decayEntity == null)
                            {
                                Print(player, Lang("NotStructure", player.UserIDString));
                                return false;
                            }
                            if (_removeStructureCoroutine != null)
                            {
                                Print(player, Lang("AlreadyRemoveStructure", player.UserIDString));
                                return false;
                            }
                            _removeStructureCoroutine = ServerMgr.Instance.StartCoroutine(RemoveStructure(decayEntity, player));
                            Print(player, Lang("StartRemoveStructure", player.UserIDString));
                            return true;
                        }
                }
            }

            var info = GetRemovableEntityInfo(targetEntity, player);

            string reason;
            if (!CanRemoveEntity(player, removeType, targetEntity, info, shouldPay, out reason))
            {
                Print(player, reason);
                return false;
            }

            DropContainerEntity(targetEntity);

            if (shouldPay)
            {
                var flag = TryPay(player, targetEntity, info);
                if (!flag)
                {
                    Print(player, Lang("CantPay", player.UserIDString));
                    return false;
                }
            }

            if (shouldRefund)
            {
                GiveRefund(player, targetEntity, info);
            }

            DoNormalRemove(player, targetEntity, _configData.removeType[RemoveType.Normal].gibs);
            return true;
        }

        private bool CanRemoveEntity(BasePlayer player, RemoveType removeType, BaseEntity targetEntity, RemovableEntityInfo? info, bool shouldPay, out string reason)
        {
            if (removeType != RemoveType.Normal)
            {
                reason = null;
                return true;
            }
            if (targetEntity == null || !CanEntityBeDisplayed(targetEntity, player))
            {
                reason = Lang("NotFoundOrFar", player.UserIDString);
                return false;
            }
            if (targetEntity.IsDestroyed)
            {
                reason = Lang("InvalidEntity", player.UserIDString);
                return false;
            }
            if (!info.HasValue)
            {
                if (!IsRemovableEntity(targetEntity))
                {
                    reason = Lang("InvalidEntity", player.UserIDString);
                    return false;
                }
                if (!HasEntityEnabled(targetEntity))
                {
                    reason = Lang("EntityDisabled", player.UserIDString);
                    return false;
                }
            }
            var result = Interface.CallHook("canRemove", player, targetEntity);
            if (result != null)
            {
                reason = result is string ? (string)result : Lang("BeBlocked", player.UserIDString);
                return false;
            }
            if (!_configData.damagedEntity.enabled && IsDamagedEntity(targetEntity))
            {
                reason = Lang("DamagedEntity", player.UserIDString);
                return false;
            }
            if (IsPlayerBlocked(player, out reason))
            {
                return false;
            }
            if (_configData.global.entityTimeLimit && IsEntityTimeLimit(targetEntity))
            {
                reason = Lang("EntityTimeLimit", player.UserIDString, _configData.global.limitTime);
                return false;
            }
            if (!_configData.container.removeNotEmptyStorage)
            {
                var storageContainer = targetEntity as StorageContainer;
                if (storageContainer != null && storageContainer.inventory?.itemList?.Count > 0)
                {
                    reason = Lang("StorageNotEmpty", player.UserIDString);
                    return false;
                }
            }
            if (!_configData.container.removeNotEmptyIoEntity)
            {
                var containerIOEntity = targetEntity as ContainerIOEntity;
                if (containerIOEntity != null && containerIOEntity.inventory?.itemList?.Count > 0)
                {
                    reason = Lang("StorageNotEmpty", player.UserIDString);
                    return false;
                }
            }
            if (shouldPay && !CanPay(player, targetEntity, info))
            {
                reason = Lang("NotEnoughCost", player.UserIDString);
                return false;
            }
            if (!HasAccess(player, targetEntity))
            {
                reason = Lang("NotRemoveAccess", player.UserIDString);
                return false;
            }
            reason = Lang("CanRemove", player.UserIDString);
            return true;
        }

        private bool HasAccess(BasePlayer player, BaseEntity targetEntity)
        {
            if (_configData.global.useBuildingOwners && BuildingOwners != null)
            {
                var buildingBlock = targetEntity as BuildingBlock;
                if (buildingBlock != null)
                {
                    var result = BuildingOwners?.Call("FindBlockData", buildingBlock) as string;
                    if (result != null)
                    {
                        var ownerID = ulong.Parse(result);
                        if (AreFriends(ownerID, player.userID))
                        {
                            return true;
                        }
                    }
                }
            }
            //var 1 = configData.globalS.excludeTwigs && (targetEntity as BuildingBlock)?.grade == BuildingGrade.Enum.Twigs;
            if (_configData.global.useEntityOwners)
            {
                if (AreFriends(targetEntity.OwnerID, player.userID))
                {
                    if (!_configData.global.useToolCupboards)
                    {
                        return true;
                    }
                    if (HasTotalAccess(player, targetEntity))
                    {
                        return true;
                    }
                }

                return false;
            }
            if (_configData.global.useToolCupboards)
            {
                if (HasTotalAccess(player, targetEntity))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasTotalAccess(BasePlayer player, BaseEntity targetEntity)
        {
            if (player.IsBuildingBlocked(targetEntity.WorldSpaceBounds()))
            {
                return false;
            }
            if (_configData.global.useBuildingLocks && !CanOpenAllLocks(player, targetEntity))
            {
                //reason = Lang("Can'tOpenAllLocks", player.UserIDString);
                return false;
            }
            return true;
        }

        private static bool CanOpenAllLocks(BasePlayer player, BaseEntity targetEntity)
        {
            var decayEntities = Pool.GetList<DecayEntity>();
            var building = targetEntity.GetBuildingPrivilege()?.GetBuilding() ?? (targetEntity as DecayEntity)?.GetBuilding();
            if (building != null)
            {
                decayEntities.AddRange(building.decayEntities);
            }
            /*else//An entity placed outside
            {
                Vis.Entities(targetEntity.transform.position, 9f, decayEntities, Layers.Mask.Construction | Layers.Mask.Deployed);
            }*/
            foreach (var decayEntity in decayEntities)
            {
                if ((decayEntity is Door || decayEntity is BoxStorage) && decayEntity.OwnerID.IsSteamId())
                {
                    var lockEntity = decayEntity.GetSlot(BaseEntity.Slot.Lock) as BaseLock;
                    if (lockEntity != null && !OnTryToOpen(player, lockEntity))
                    {
                        Pool.FreeList(ref decayEntities);
                        return false;
                    }
                }
            }
            Pool.FreeUnmanaged(ref decayEntities);
            return true;
        }

        private static bool OnTryToOpen(BasePlayer player, BaseLock baseLock)
        {
            var codeLock = baseLock as CodeLock;
            if (codeLock != null)
            {
                var obj = Interface.CallHook("CanUseLockedEntity", player, codeLock);
                if (obj is bool)
                {
                    return (bool)obj;
                }
                if (!codeLock.IsLocked())
                {
                    return true;
                }
                // Make no sound during the check
                if (codeLock.whitelistPlayers.Contains(player.userID) || codeLock.guestPlayers.Contains(player.userID))
                {
                    return true;
                }
                return false;
            }
            var keyLock = baseLock as KeyLock;
            if (keyLock != null)
            {
                return keyLock.OnTryToOpen(player);
            }

            return false;
        }

        private static bool IsDamagedEntity(BaseEntity entity)
        {
            var baseCombatEntity = entity as BaseCombatEntity;
            if (baseCombatEntity == null || !baseCombatEntity.repair.enabled)
            {
                return false;
            }
            if (_configData.damagedEntity.excludeBuildingBlocks && (baseCombatEntity is BuildingBlock || baseCombatEntity is SimpleBuildingBlock))
            {
                return false;
            }
            if (_configData.damagedEntity.excludeQuarries && !(baseCombatEntity is BuildingBlock))
            {
                // Quarries
                if (baseCombatEntity.repair.itemTarget == null || baseCombatEntity.repair.itemTarget.Blueprint == null)
                {
                    return false;
                }
            }

            if (baseCombatEntity.healthFraction * 100f >= _configData.damagedEntity.percentage)
            {
                return false;
            }
            return true;
        }

        private static bool IsEntityTimeLimit(BaseEntity entity)
        {
            if (entity.net == null)
            {
                return true;
            }
            float spawnedTime;
            if (_instance._entitySpawnedTimes.TryGetValue(entity.net.ID.Value, out spawnedTime))
            {
                return Time.realtimeSinceStartup - spawnedTime > _configData.global.limitTime;
            }
            return true;
        }

        private static void DropContainerEntity(BaseEntity targetEntity)
        {
            var itemContainerEntity = targetEntity as IItemContainerEntity;
            if (itemContainerEntity != null && itemContainerEntity.inventory?.itemList?.Count > 0)
            {
                bool dropContainer = false, dropItems = false;
                var storageContainer = targetEntity as StorageContainer;
                if (storageContainer != null)
                {
                    dropContainer = _configData.container.dropContainerStorage;
                    dropItems = _configData.container.dropItemsStorage;
                }
                else
                {
                    var containerIoEntity = targetEntity as ContainerIOEntity;
                    if (containerIoEntity != null)
                    {
                        dropContainer = _configData.container.dropContainerIoEntity;
                        dropItems = _configData.container.dropItemsIoEntity;
                    }
                }
                if (dropContainer || dropItems)
                {
                    if (Interface.CallHook("OnDropContainerEntity", targetEntity) == null)
                    {
                        if (dropContainer)
                        {
                            DropItemContainer(itemContainerEntity.inventory, itemContainerEntity.GetDropPosition(), itemContainerEntity.Transform.rotation);
                        }
                        else if (dropItems)
                        {
                            itemContainerEntity.DropItems();
                        }
                    }
                }
            }
        }

        #region AreFriends

        private bool AreFriends(ulong playerID, ulong friendID)
        {
            if (!playerID.IsSteamId())
            {
                return false;
            }
            if (playerID == friendID)
            {
                return true;
            }
            if (_configData.global.useTeams && SameTeam(playerID, friendID))
            {
                return true;
            }
            if (_configData.global.useFriends && HasFriend(playerID, friendID))
            {
                return true;
            }
            if (_configData.global.useClans && SameClan(playerID, friendID))
            {
                return true;
            }
            return false;
        }

        private static bool SameTeam(ulong playerID, ulong friendID)
        {
            if (!RelationshipManager.TeamsEnabled())
            {
                return false;
            }
            var playerTeam = RelationshipManager.ServerInstance.FindPlayersTeam(playerID);
            if (playerTeam == null)
            {
                return false;
            }
            var friendTeam = RelationshipManager.ServerInstance.FindPlayersTeam(friendID);
            if (friendTeam == null)
            {
                return false;
            }
            return playerTeam == friendTeam;
        }

        private bool HasFriend(ulong playerID, ulong friendID)
        {
            if (Friends == null)
            {
                return false;
            }
            return (bool)Friends.Call("HasFriend", playerID, friendID);
        }

        private bool SameClan(ulong playerID, ulong friendID)
        {
            if (Clans == null)
            {
                return false;
            }
            //Clans
            var isMember = Clans.Call("IsClanMember", playerID.ToString(), friendID.ToString());
            if (isMember != null)
            {
                return (bool)isMember;
            }
            //Rust:IO Clans
            var playerClan = Clans.Call("GetClanOf", playerID);
            if (playerClan == null)
            {
                return false;
            }
            var friendClan = Clans.Call("GetClanOf", friendID);
            if (friendClan == null)
            {
                return false;
            }
            return (string)playerClan == (string)friendClan;
        }

        #endregion AreFriends

        #endregion TryRemove

        #region Pay

        private Dictionary<string, CurrencyInfo> GetPrice(BaseEntity targetEntity, RemovableEntityInfo? info)
        {
            if (info.HasValue)
            {
                return info.Value.Price.ValueName2Currency;
            }
            var buildingBlock = targetEntity as BuildingBlock;
            if (buildingBlock != null)
            {
                var entityName = _prefabNameToStructure[buildingBlock.PrefabName];
                BuildingBlocksSettings buildingBlockSettings;
                if (_configData.remove.buildingBlock.TryGetValue(entityName, out buildingBlockSettings))
                {
                    BuildingGradeSettings buildingGradeSettings;
                    if (buildingBlockSettings.buildingGrade.TryGetValue(buildingBlock.grade, out buildingGradeSettings))
                    {
                        if (buildingGradeSettings.priceDict != null)
                        {
                            return buildingGradeSettings.priceDict;
                        }
                        if (buildingGradeSettings.pricePercentage > 0f)
                        {
                            var currentGrade = buildingBlock.currentGrade;
                            if (currentGrade != null)
                            {
                                var price = new Dictionary<string, CurrencyInfo>();
                                var costToBuild = buildingBlock.blockDefinition.GetGrade(buildingBlock.grade, buildingBlock.skinID).CostToBuild();
                                foreach (var itemAmount in costToBuild)
                                {
                                    var amount = Mathf.RoundToInt(itemAmount.amount * buildingGradeSettings.pricePercentage / 100);
                                    if (amount <= 0)
                                    {
                                        continue;
                                    }
                                    price.Add(itemAmount.itemDef.shortname, new CurrencyInfo(amount));
                                }

                                return price;
                            }
                        }
                        else if (buildingGradeSettings.pricePercentage < 0f)
                        {
                            var currentGrade = buildingBlock.currentGrade;
                            if (currentGrade != null)
                            {
                                return buildingBlock.blockDefinition.GetGrade(buildingBlock.grade, buildingBlock.skinID).CostToBuild().ToDictionary(x => x.itemDef.shortname, y => new CurrencyInfo(Mathf.RoundToInt(y.amount)));
                            }
                        }
                    }
                }
            }
            else
            {
                EntitySettings entitySettings;
                if (_configData.remove.entity.TryGetValue(targetEntity.ShortPrefabName, out entitySettings))
                {
                    return entitySettings.priceDict;
                }
            }
            return null;
        }

        private bool TryPay(BasePlayer player, BaseEntity targetEntity, RemovableEntityInfo? info)
        {
            var price = GetPrice(targetEntity, info);
            if (price == null || price.Count == 0)
            {
                return true;
            }
            var collect = Pool.GetList<Item>();
            try
            {
                foreach (var entry in price)
                {
                    if (entry.Value.Amount <= 0)
                    {
                        continue;
                    }
                    int itemId;
                    if (_itemShortNameToItemId.TryGetValue(entry.Key, out itemId))
                    {
                        var take = TakeInventory(player, itemId, entry.Value, collect);
                        player.Command("note.inv", itemId, -take);
                    }
                    else if (!CheckOrPay(targetEntity, player, entry.Key, entry.Value, false))
                    {
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                PrintError($"{player} couldn't pay to remove entity. Error: {e}");
                return false;
            }
            finally
            {
                foreach (var item in collect)
                {
                    item.Remove();
                }
                Pool.FreeUnmanaged(ref collect);
            }
            return true;
        }

        private bool CanPay(BasePlayer player, BaseEntity targetEntity, RemovableEntityInfo? info)
        {
            var price = GetPrice(targetEntity, info);
            if (price == null || price.Count == 0)
            {
                return true;
            }
            foreach (var entry in price)
            {
                if (entry.Value.Amount <= 0)
                {
                    continue;
                }
                int itemId;
                if (_itemShortNameToItemId.TryGetValue(entry.Key, out itemId))
                {
                    var amount = GetInventoryAmount(player, itemId, entry.Value);
                    if (amount < entry.Value.Amount)
                    {
                        return false;
                    }
                }
                else if (!CheckOrPay(targetEntity, player, entry.Key, entry.Value, true))
                {
                    return false;
                }
            }
            return true;
        }

        private bool CheckOrPay(BaseEntity targetEntity, BasePlayer player, string itemName, CurrencyInfo currencyInfo, bool check)
        {
            if (currencyInfo.Amount <= 0)
            {
                return true;
            }
            switch (itemName.ToLower())
            {
                case ECONOMICS_KEY:
                    if (Economics == null)
                    {
                        return false;
                    }
                    if (check)
                    {
                        var balance = Economics.Call("Balance", player.userID);
                        if (balance == null)
                        {
                            return false;
                        }
                        if ((double)balance < currencyInfo.Amount)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        var withdraw = Economics.Call("Withdraw", player.userID, (double)currencyInfo.Amount);
                        if (withdraw == null || !(bool)withdraw)
                        {
                            return false;
                        }
                    }
                    return true;

                case SERVER_REWARDS_KEY:
                    if (ServerRewards == null)
                    {
                        return false;
                    }
                    if (check)
                    {
                        var points = ServerRewards.Call("CheckPoints", player.userID);
                        if (points == null)
                        {
                            return false;
                        }

                        if ((int)points < currencyInfo.Amount)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        var takePoints = ServerRewards.Call("TakePoints", player.userID, currencyInfo.Amount);
                        if (takePoints == null || !(bool)takePoints)
                        {
                            return false;
                        }
                    }
                    return true;

                default:
                    {
                        var result = Interface.CallHook("OnRemovableEntityCheckOrPay", targetEntity, player, itemName, currencyInfo.Amount, currencyInfo.SkinId, check);
                        if (result is bool)
                        {
                            return (bool)result;
                        }
                    }

                    return true;
            }
        }

        private static int GetInventoryAmount(BasePlayer player, int itemId, CurrencyInfo currencyInfo)
        {
            var count = 0;
            if (itemId == 0)
            {
                return count;
            }
            var list = Pool.Get<List<Item>>();
            player.inventory.GetAllItems(list);
            foreach (var item in list)
            {
                if (item.info.itemid == itemId && !item.IsBusy() && (currencyInfo.SkinId < 0 || item.skin == (ulong)currencyInfo.SkinId))
                {
                    count += item.amount;
                }
            }
            Pool.FreeUnmanaged(ref list);
            return count;
        }

        private static int TakeInventory(BasePlayer player, int itemId, CurrencyInfo currencyInfo, List<Item> collect)
        {
            var take = 0;
            if (itemId == 0)
            {
                return take;
            }
            var amount = currencyInfo.Amount;
            var list = Pool.Get<List<Item>>();
            player.inventory.GetAllItems(list);
            foreach (var item in list)
            {
                if (item.info.itemid == itemId && !item.IsBusy() && (currencyInfo.SkinId < 0 || item.skin == (ulong)currencyInfo.SkinId))
                {
                    var need = amount - take;
                    if (need > 0)
                    {
                        if (item.amount > need)
                        {
                            item.MarkDirty();
                            item.amount -= need;
                            take += need;
                            var newItem = ItemManager.CreateByItemID(itemId, 1, item.skin);
                            newItem.amount = need;
                            newItem.CollectedForCrafting(player);
                            collect?.Add(newItem);
                            break;
                        }
                        if (item.amount <= need)
                        {
                            take += item.amount;
                            item.RemoveFromContainer();
                            collect?.Add(item);
                        }
                        if (take == amount)
                        {
                            break;
                        }
                    }
                }
            }
            Pool.FreeUnmanaged(ref list);
            return take;
        }

        #endregion Pay

        #region Refund

        private void GiveRefund(BasePlayer player, BaseEntity targetEntity, RemovableEntityInfo? info)
        {
            var refund = GetRefund(targetEntity, info);
            if (refund == null || refund.Count == 0)
            {
                return;
            }
            foreach (var entry in refund)
            {
                var itemName = entry.Key;
                var currencyInfo = entry.Value;
                if (currencyInfo.Amount <= 0)
                {
                    continue;
                }
                int itemId;
                string shortname;
                _shortPrefabNameToDeployable.TryGetValue(targetEntity.ShortPrefabName, out shortname);
                if (_itemShortNameToItemId.TryGetValue(itemName, out itemId))
                {
                    var isOriginalItem = itemName == shortname;
                    var isSpecifiedSkinId = currencyInfo.SkinId >= 0;
                    var item = ItemManager.CreateByItemID(itemId, currencyInfo.Amount, isSpecifiedSkinId ? (ulong)currencyInfo.SkinId : isOriginalItem ? targetEntity.skinID : 0);
                    if (!isSpecifiedSkinId && isOriginalItem && item.hasCondition && targetEntity is BaseCombatEntity)
                    {
                        item.condition = item.maxCondition * (targetEntity.Health() / targetEntity.MaxHealth());
                    }
                    player.GiveItem(item);
                }
                else
                {
                    var flag = false;
                    switch (itemName.ToLower())
                    {
                        case ECONOMICS_KEY:
                            {
                                if (Economics == null)
                                {
                                    continue;
                                }
                                var result = Economics.Call("Deposit", player.userID, (double)currencyInfo.Amount);
                                if (result != null)
                                {
                                    flag = true;
                                }
                                break;
                            }

                        case SERVER_REWARDS_KEY:
                            {
                                if (ServerRewards == null)
                                {
                                    continue;
                                }
                                var result = ServerRewards.Call("AddPoints", player.userID, currencyInfo.Amount);
                                if (result != null)
                                {
                                    flag = true;
                                }
                                break;
                            }

                        default:
                            {
                                var result = Interface.CallHook("OnRemovableEntityGiveRefund", targetEntity, player, itemName, currencyInfo.Amount, currencyInfo.SkinId);
                                if (result == null)
                                {
                                    flag = true;
                                }
                                break;
                            }
                    }

                    if (!flag)
                    {
                        PrintError($"{player} didn't receive refund maybe {itemName} doesn't seem to be a valid item name");
                    }
                }
            }
        }

        private Dictionary<string, CurrencyInfo> GetRefund(BaseEntity targetEntity, RemovableEntityInfo? info)
        {
            if (info.HasValue)
            {
                return info.Value.Refund.ValueName2Currency;
            }
            var buildingBlock = targetEntity.GetComponent<BuildingBlock>();
            if (buildingBlock != null)
            {
                var entityName = _prefabNameToStructure[buildingBlock.PrefabName];
                BuildingBlocksSettings buildingBlockSettings;
                if (_configData.remove.buildingBlock.TryGetValue(entityName, out buildingBlockSettings))
                {
                    BuildingGradeSettings buildingGradeSettings;
                    if (buildingBlockSettings.buildingGrade.TryGetValue(buildingBlock.grade, out buildingGradeSettings))
                    {
                        if (buildingGradeSettings.refundDict != null)
                        {
                            return buildingGradeSettings.refundDict;
                        }
                        if (buildingGradeSettings.refundPercentage > 0f)
                        {
                            var currentGrade = buildingBlock.currentGrade;
                            if (currentGrade != null)
                            {
                                var refund = new Dictionary<string, CurrencyInfo>();
                                var costToBuild = buildingBlock.blockDefinition.GetGrade(buildingBlock.grade, buildingBlock.skinID).CostToBuild();
                                foreach (var itemAmount in costToBuild)
                                {
                                    var amount = Mathf.RoundToInt(itemAmount.amount * buildingGradeSettings.refundPercentage / 100);
                                    if (amount <= 0)
                                    {
                                        continue;
                                    }
                                    refund.Add(itemAmount.itemDef.shortname, new CurrencyInfo(amount));
                                }
                                return refund;
                            }
                        }
                        else if (buildingGradeSettings.refundPercentage < 0f)
                        {
                            var currentGrade = buildingBlock.currentGrade;
                            if (currentGrade != null)
                            {
                                return buildingBlock.blockDefinition.GetGrade(buildingBlock.grade, buildingBlock.skinID).CostToBuild().ToDictionary(x => x.itemDef.shortname, y => new CurrencyInfo(Mathf.RoundToInt(y.amount)));
                            }
                        }
                    }
                }
            }
            else
            {
                EntitySettings entitySettings;
                if (_configData.remove.entity.TryGetValue(targetEntity.ShortPrefabName, out entitySettings))
                {
                    if (_configData.remove.refundSlot)
                    {
                        var slots = GetSlots(targetEntity);
                        if (slots.Any())
                        {
                            var refund = new Dictionary<string, CurrencyInfo>(entitySettings.refundDict);
                            foreach (var slotName in slots)
                            {
                                if (!refund.ContainsKey(slotName))
                                {
                                    refund.Add(slotName, new CurrencyInfo(0));
                                }
                                refund[slotName] = new CurrencyInfo(refund[slotName].Amount + 1, refund[slotName].SkinId);
                            }
                            return refund;
                        }
                    }
                    return entitySettings.refundDict;
                }
            }
            return null;
        }

        private IEnumerable<string> GetSlots(BaseEntity targetEntity)
        {
            foreach (BaseEntity.Slot slot in Enum.GetValues(typeof(BaseEntity.Slot)))
            {
                if (targetEntity.HasSlot(slot))
                {
                    var entity = targetEntity.GetSlot(slot);
                    if (entity != null)
                    {
                        string slotName;
                        if (_shortPrefabNameToDeployable.TryGetValue(entity.ShortPrefabName, out slotName))
                        {
                            yield return slotName;
                        }
                    }
                }
            }
        }

        #endregion Refund

        #region RemoveEntity

        private IEnumerator RemoveAll(BaseEntity sourceEntity, BasePlayer player)
        {
            var removeList = Pool.Get<HashSet<BaseEntity>>();
            yield return GetNearbyEntities(sourceEntity, removeList, LAYER_ALL);
            yield return ProcessContainers(removeList);
            yield return DelayRemove(removeList, player, RemoveType.All);
            removeList.Clear();
            Pool.FreeUnmanaged(ref removeList);
            _removeAllCoroutine = null;
        }

        private IEnumerator RemoveExternal(StabilityEntity sourceEntity, BasePlayer player)
        {
            var removeList = Pool.Get<HashSet<StabilityEntity>>();
            yield return GetNearbyEntities(sourceEntity, removeList, Layers.Mask.Construction, IsExternalWall);
            yield return DelayRemove(removeList, player, RemoveType.External);
            removeList.Clear();
            Pool.FreeUnmanaged(ref removeList);
            _removeExternalCoroutine = null;
        }

        private IEnumerator RemoveStructure(DecayEntity sourceEntity, BasePlayer player)
        {
            var removeList = Pool.Get<HashSet<BaseEntity>>();
            yield return ProcessBuilding(sourceEntity, removeList);
            yield return DelayRemove(removeList, player, RemoveType.Structure);
            removeList.Clear();
            Pool.FreeUnmanaged(ref removeList);
            _removeStructureCoroutine = null;
        }

        private IEnumerator RemovePlayerEntity(ConsoleSystem.Arg arg, ulong targetID, PlayerEntityRemoveType playerEntityRemoveType)
        {
            var current = 0;
            var removeList = Pool.Get<HashSet<BaseEntity>>();
            switch (playerEntityRemoveType)
            {
                case PlayerEntityRemoveType.All:
                case PlayerEntityRemoveType.Building:
                    var onlyBuilding = playerEntityRemoveType == PlayerEntityRemoveType.Building;
                    foreach (var serverEntity in BaseNetworkable.serverEntities)
                    {
                        if (++current % 500 == 0)
                        {
                            yield return CoroutineEx.waitForEndOfFrame;
                        }
                        var entity = serverEntity as BaseEntity;
                        if (entity == null || entity.OwnerID != targetID)
                        {
                            continue;
                        }
                        if (!onlyBuilding || entity is BuildingBlock)
                        {
                            removeList.Add(entity);
                        }
                    }
                    foreach (var player in BasePlayer.allPlayerList)
                    {
                        if (player.userID == targetID)
                        {
                            if (player.IsConnected)
                            {
                                player.Kick("From RemoverTool Plugin");
                            }
                            removeList.Add(player);
                            break;
                        }
                    }
                    break;

                case PlayerEntityRemoveType.Cupboard:
                    foreach (var serverEntity in BaseNetworkable.serverEntities)
                    {
                        if (++current % 500 == 0)
                        {
                            yield return CoroutineEx.waitForEndOfFrame;
                        }
                        var entity = serverEntity as BuildingPrivlidge;
                        if (entity == null || entity.OwnerID != targetID)
                        {
                            continue;
                        }
                        yield return ProcessBuilding(entity, removeList);
                    }
                    break;
            }
            var removed = removeList.Count(x => x != null && !x.IsDestroyed);
            yield return DelayRemove(removeList);
            removeList.Clear();
            Pool.FreeUnmanaged(ref removeList);
            Print(arg, $"You have successfully removed {removed} entities of player {targetID}.");
            _removePlayerEntityCoroutine = null;
        }

        private IEnumerator DelayRemove(IEnumerable<BaseEntity> entities, BasePlayer player = null, RemoveType removeType = RemoveType.None)
        {
            var removed = 0;
            var destroyMode = removeType == RemoveType.None ? BaseNetworkable.DestroyMode.None : _configData.removeType[removeType].gibs ? BaseNetworkable.DestroyMode.Gib : BaseNetworkable.DestroyMode.None;
            foreach (var entity in entities)
            {
                if (DoRemove(entity, destroyMode) && ++removed % _configData.global.removePerFrame == 0)
                {
                    yield return CoroutineEx.waitForEndOfFrame;
                }
            }

            if (removeType == RemoveType.None)
            {
                yield break;
            }
            var toolRemover = player?.GetComponent<ToolRemover>();
            if (toolRemover != null && toolRemover.RemoveType == removeType)
            {
                toolRemover.CurrentRemoved += removed;
            }
            if (player != null)
            {
                Print(player, Lang($"CompletedRemove{removeType}", player.UserIDString, removed));
            }
        }

        #region RemoveEntity Helpers

        private static IEnumerator GetNearbyEntities<T>(T sourceEntity, HashSet<T> removeList, int layers, Func<T, bool> filter = null) where T : BaseEntity
        {
            var current = 0;
            var checkFrom = Pool.Get<Queue<Vector3>>();
            var nearbyEntities = Pool.GetList<T>();
            removeList.Add(sourceEntity);
            checkFrom.Enqueue(sourceEntity.transform.position);
            while (checkFrom.Count > 0)
            {
                nearbyEntities.Clear();
                var position = checkFrom.Dequeue();
                Vis.Entities(position, 3f, nearbyEntities, layers);
                for (var i = 0; i < nearbyEntities.Count; i++)
                {
                    var entity = nearbyEntities[i];
                    if (filter != null && !filter(entity))
                    {
                        continue;
                    }
                    if (!removeList.Add(entity))
                    {
                        continue;
                    }
                    checkFrom.Enqueue(entity.transform.position);
                }
                if (++current % _configData.global.removePerFrame == 0)
                {
                    yield return CoroutineEx.waitForEndOfFrame;
                }
            }
            checkFrom.Clear();
            Pool.FreeUnmanaged(ref checkFrom);
            Pool.FreeUnmanaged(ref nearbyEntities);
        }

        private static IEnumerator ProcessContainers(HashSet<BaseEntity> removeList)
        {
            foreach (var entity in removeList)
            {
                ProcessContainer(entity);
            }

            ItemManager.DoRemoves();
            yield break;
        }

        private static IEnumerator ProcessBuilding(DecayEntity sourceEntity, HashSet<BaseEntity> removeList)
        {
            var building = sourceEntity.GetBuilding();
            if (building != null)
            {
                foreach (var entity in building.decayEntities)
                {
                    if (!removeList.Add(entity))
                    {
                        continue;
                    }
                    ProcessContainer(entity);
                }
            }
            else
            {
                removeList.Add(sourceEntity);
            }

            ItemManager.DoRemoves();
            yield break;
        }

        private static void ProcessContainer(BaseEntity entity)
        {
            var itemContainerEntity = entity as IItemContainerEntity;
            if (itemContainerEntity != null && itemContainerEntity.inventory?.itemList?.Count > 0)
            {
                if (_configData.global.noItemContainerDrop)
                {
                    itemContainerEntity.inventory.Clear();
                }
                else
                {
                    DropItemContainer(itemContainerEntity.inventory, itemContainerEntity.GetDropPosition(), itemContainerEntity.Transform.rotation);
                }
            }
        }

        private static bool DoRemove(BaseEntity entity, BaseNetworkable.DestroyMode destroyMode)
        {
            if (entity != null && !entity.IsDestroyed)
            {
                entity.Kill(destroyMode);
                return true;
            }
            return false;
        }

        private static void DoNormalRemove(BasePlayer player, BaseEntity entity, bool gibs = true)
        {
            if (entity != null && !entity.IsDestroyed)
            {
                _instance.PrintDebug($"{player.displayName}({player.userID}) has removed {entity.ShortPrefabName}({entity.OwnerID} | {entity.transform.position})", true);
                Interface.CallHook("OnNormalRemovedEntity", player, entity);
                entity.Kill(gibs ? BaseNetworkable.DestroyMode.Gib : BaseNetworkable.DestroyMode.None);
            }
        }

        #endregion RemoveEntity Helpers

        #endregion RemoveEntity

        #region API

        public struct CurrencyInfo
        {
            [JsonProperty(PropertyName = "amount")]
            public int Amount { get; set; }

            [JsonProperty(PropertyName = "skinId")]
            public long SkinId { get; set; }

            public CurrencyInfo(int amount, long skinId = -1)
            {
                Amount = amount;
                SkinId = skinId;
            }
        }

        private struct ValueCache<T>
        {
            private T _value;
            private bool _flag;
            private readonly string _key;
            private readonly Dictionary<string, object> _dictionary;

            public ValueCache(string key, Dictionary<string, object> dictionary, T defaultValue)
            {
                _flag = false;
                _key = key;
                _value = defaultValue;
                _dictionary = dictionary;
            }

            public T Value
            {
                get
                {
                    if (!_flag)
                    {
                        _flag = true;
                        object value;
                        if (_dictionary.TryGetValue(_key, out value))
                        {
                            try
                            {
                                _value = (T)value;
                            }
                            catch (Exception)
                            {
                                _instance.PrintError($"Incorrect type for {_key}( {typeof(T)})");
                            }
                        }
                    }

                    return _value;
                }
            }
        }

        private struct CurrencyCache
        {
            private bool _name2InfoFlag;
            private bool _name2AmountFlag;
            private Dictionary<string, CurrencyInfo> _valueName2Currency;
            private Dictionary<string, ExternalItemInfo> _valueName2Info;
            private ValueCache<Dictionary<string, object>> _dictionary;

            public CurrencyCache(string key, Dictionary<string, object> dictionary)
            {
                _name2InfoFlag = false;
                _name2AmountFlag = false;
                _valueName2Info = null;
                _valueName2Currency = null;
                _dictionary = new ValueCache<Dictionary<string, object>>(key, dictionary, default(Dictionary<string, object>));
            }

            public ExternalItemInfo? this[string key]
            {
                get
                {
                    if (ValueName2Info == null)
                    {
                        return null;
                    }

                    ExternalItemInfo externalItemInfo;
                    if (!ValueName2Info.TryGetValue(key, out externalItemInfo))
                    {
                        return null;
                    }
                    return externalItemInfo;
                }
            }

            public Dictionary<string, CurrencyInfo> ValueName2Currency
            {
                get
                {
                    if (!_name2AmountFlag)
                    {
                        _name2AmountFlag = true;
                        if (ValueName2Info == null)
                        {
                            return null;
                        }

                        _valueName2Currency = new Dictionary<string, CurrencyInfo>();
                        foreach (var entry in ValueName2Info)
                        {
                            _valueName2Currency.Add(entry.Key, new CurrencyInfo(entry.Value.Amount.Value, entry.Value.SkinId.Value));
                        }
                    }

                    return _valueName2Currency;
                }
            }

            private Dictionary<string, ExternalItemInfo> ValueName2Info
            {
                get
                {
                    if (!_name2InfoFlag)
                    {
                        _name2InfoFlag = true;
                        if (_dictionary.Value == null)
                        {
                            return null;
                        }

                        _valueName2Info = new Dictionary<string, ExternalItemInfo>();
                        foreach (var entry in _dictionary.Value)
                        {
                            _valueName2Info.Add(entry.Key, new ExternalItemInfo(entry.Value as Dictionary<string, object>));
                        }
                    }

                    return _valueName2Info;
                }
            }
        }

        private struct ExternalItemInfo
        {
            public ExternalItemInfo(Dictionary<string, object> dictionary)
            {
                Amount = new ValueCache<int>(nameof(Amount), dictionary, 0);
                SkinId = new ValueCache<long>(nameof(SkinId), dictionary, -1);
                ImageId = new ValueCache<string>(nameof(ImageId), dictionary, "");
                DisplayName = new ValueCache<string>(nameof(DisplayName), dictionary, "");
            }

            public ValueCache<int> Amount { get; }
            public ValueCache<long> SkinId { get; }
            public ValueCache<string> ImageId { get; }
            public ValueCache<string> DisplayName { get; }
        }

        private struct RemovableEntityInfo
        {
            public RemovableEntityInfo(Dictionary<string, object> dictionary)
            {
                ImageId = new ValueCache<string>(nameof(ImageId), dictionary, "");
                DisplayName = new ValueCache<string>(nameof(DisplayName), dictionary, "");
                Price = new CurrencyCache(nameof(Price), dictionary);
                Refund = new CurrencyCache(nameof(Refund), dictionary);
            }

            public ValueCache<string> ImageId { get; }

            public ValueCache<string> DisplayName { get; }

            public CurrencyCache Price { get; }

            public CurrencyCache Refund { get; }
        }

        private static RemovableEntityInfo? GetRemovableEntityInfo(BaseEntity entity, BasePlayer player)
        {
            if (entity == null)
            {
                return null;
            }
            var result = Interface.CallHook("OnRemovableEntityInfo", entity, player) as Dictionary<string, object>;
            if (result != null)
            {
                return new RemovableEntityInfo(result);
            }

            return null;
        }

        private bool IsToolRemover(BasePlayer player)
        {
            return player != null && player.GetComponent<ToolRemover>() != null;
        }

        private string GetPlayerRemoveType(BasePlayer player)
        {
            return player != null ? player.GetComponent<ToolRemover>()?.RemoveType.ToString() : null;
        }

        #endregion API

        #region Commands

        private void CmdRemove(BasePlayer player, string command, string[] args)
        {
            if (args == null || args.Length == 0)
            {
                var sourceRemover = player.GetComponent<ToolRemover>();
                if (sourceRemover != null)
                {
                    sourceRemover.DisableTool();
                    return;
                }
            }
            if (_removeOverride && !permission.UserHasPermission(player.UserIDString, PERMISSION_OVERRIDE))
            {
                Print(player, Lang("CurrentlyDisabled", player.UserIDString));
                return;
            }
            var removeType = RemoveType.Normal;
            var time = _configData.removeType[removeType].defaultTime;
            if (args != null && args.Length > 0)
            {
                switch (args[0].ToLower())
                {
                    case "n":
                    case "normal":
                        break;

                    case "a":
                    case "admin":
                        removeType = RemoveType.Admin;
                        time = _configData.removeType[removeType].defaultTime;
                        if (!permission.UserHasPermission(player.UserIDString, PERMISSION_ADMIN))
                        {
                            Print(player, Lang("NotAllowed", player.UserIDString, PERMISSION_ADMIN));
                            return;
                        }
                        break;

                    case "all":
                        removeType = RemoveType.All;
                        time = _configData.removeType[removeType].defaultTime;
                        if (!permission.UserHasPermission(player.UserIDString, PERMISSION_ALL))
                        {
                            Print(player, Lang("NotAllowed", player.UserIDString, PERMISSION_ALL));
                            return;
                        }
                        break;

                    case "s":
                    case "structure":
                        removeType = RemoveType.Structure;
                        time = _configData.removeType[removeType].defaultTime;
                        if (!permission.UserHasPermission(player.UserIDString, PERMISSION_STRUCTURE))
                        {
                            Print(player, Lang("NotAllowed", player.UserIDString, PERMISSION_STRUCTURE));
                            return;
                        }
                        break;

                    case "e":
                    case "external":
                        removeType = RemoveType.External;
                        time = _configData.removeType[removeType].defaultTime;
                        if (!permission.UserHasPermission(player.UserIDString, PERMISSION_EXTERNAL))
                        {
                            Print(player, Lang("NotAllowed", player.UserIDString, PERMISSION_EXTERNAL));
                            return;
                        }
                        break;

                    case "h":
                    case "help":
                        var stringBuilder = Pool.Get<StringBuilder>();
                        stringBuilder.Clear();
                        stringBuilder.AppendLine(Lang("Syntax", player.UserIDString, _configData.chat.command, GetRemoveTypeName(RemoveType.Normal)));
                        stringBuilder.AppendLine(Lang("Syntax1", player.UserIDString, _configData.chat.command, GetRemoveTypeName(RemoveType.Admin)));
                        stringBuilder.AppendLine(Lang("Syntax2", player.UserIDString, _configData.chat.command, GetRemoveTypeName(RemoveType.All)));
                        stringBuilder.AppendLine(Lang("Syntax3", player.UserIDString, _configData.chat.command, GetRemoveTypeName(RemoveType.Structure)));
                        stringBuilder.AppendLine(Lang("Syntax4", player.UserIDString, _configData.chat.command, GetRemoveTypeName(RemoveType.External)));
                        Print(player, stringBuilder.ToString());
                        stringBuilder.Clear();
                        Pool.FreeUnmanaged(ref stringBuilder);
                        return;

                    default:
                        if (int.TryParse(args[0], out time))
                        {
                            break;
                        }
                        Print(player, Lang("SyntaxError", player.UserIDString, _configData.chat.command));
                        return;
                }
            }
            if (args != null && args.Length > 1)
            {
                int.TryParse(args[1], out time);
            }
            ToggleRemove(player, removeType, time);
        }

        private bool ToggleRemove(BasePlayer player, RemoveType removeType, int time = 0)
        {
            if (removeType == RemoveType.Normal && !permission.UserHasPermission(player.UserIDString, PERMISSION_NORMAL))
            {
                Print(player, Lang("NotAllowed", player.UserIDString, PERMISSION_NORMAL));
                return false;
            }

            var maxRemovable = 0;
            bool pay = false, refund = false;
            var removeTypeS = _configData.removeType[removeType];
            var distance = removeTypeS.distance;
            var maxTime = removeTypeS.maxTime;
            var resetTime = removeTypeS.resetTime;
            var interval = _configData.global.removeInterval;
            if (removeType == RemoveType.Normal)
            {
                var permissionS = GetPermissionSettings(player);
                var cooldown = permissionS.cooldown;
                if (cooldown > 0 && !(_configData.global.cooldownExclude && player.IsAdmin))
                {
                    float lastUse;
                    if (_cooldownTimes.TryGetValue(player.userID, out lastUse))
                    {
                        var timeLeft = cooldown - (Time.realtimeSinceStartup - lastUse);
                        if (timeLeft > 0)
                        {
                            var timeRemaining = timeLeft > 300f ? TimeSpan.FromSeconds(timeLeft).ToShortString() : Mathf.CeilToInt(timeLeft).ToString();
                            Print(player, Lang("Cooldown", player.UserIDString, timeRemaining));
                            return false;
                        }
                    }
                }
                if (_removeMode == RemoveMode.MeleeHit && _configData.removerMode.meleeHitRequires)
                {
                    if (!IsMeleeTool(player))
                    {
                        var meleeToolDisplayName = GetDisplayNameByCurrencyName(lang.GetLanguage(player.UserIDString), _configData.removerMode.meleeHitItemShortname, _configData.removerMode.meleeHitModeSkin);
                        Print(player, Lang("MeleeToolNotHeld", player.UserIDString, meleeToolDisplayName));
                        return false;
                    }
                }
                if (_removeMode == RemoveMode.SpecificTool && _configData.removerMode.specificToolRequires)
                {
                    if (!IsSpecificTool(player))
                    {
                        var specificToolDisplayName = GetDisplayNameByCurrencyName(lang.GetLanguage(player.UserIDString), _configData.removerMode.specificToolShortName, _configData.removerMode.specificToolSkin);
                        Print(player, Lang("SpecificToolNotHeld", player.UserIDString, specificToolDisplayName));
                        return false;
                    }
                }

                interval = permissionS.removeInterval;
                resetTime = permissionS.resetTime;
                maxTime = permissionS.maxTime;
                maxRemovable = permissionS.maxRemovable;
                if (_configData.global.maxRemovableExclude && player.IsAdmin)
                {
                    maxRemovable = 0;
                }
                distance = permissionS.distance;
                pay = permissionS.pay;
                refund = permissionS.refund;
            }
            if (time == 0)
            {
                time = _configData.removeType[removeType].defaultTime;
            }
            if (time > maxTime)
            {
                time = maxTime;
            }
            var toolRemover = player.GetOrAddComponent<ToolRemover>();
            if (toolRemover.RemoveType == RemoveType.Normal)
            {
                if (!_configData.global.startCooldownOnRemoved)
                {
                    _cooldownTimes[player.userID] = Time.realtimeSinceStartup;
                }
            }
            toolRemover.Init(removeType, time, maxRemovable, distance, interval, pay, refund, resetTime, true);
            if (_configData.chat.showMessageWhenEnabledOrDisabled)
            {
                Print(player, Lang("ToolEnabled", player.UserIDString, time, maxRemovable == 0 ? Lang("Unlimit", player.UserIDString) : maxRemovable.ToString(), GetRemoveTypeName(removeType)));
            }
            return true;
        }

        [ConsoleCommand("remove.toggle")]
        private void CCmdRemoveToggle(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
            {
                Print(arg, "Syntax error!!! Please type the commands in the F1 console");
                return;
            }
            CmdRemove(player, null, arg.Args);
        }

        [ConsoleCommand("remove.target")]
        private void CCmdRemoveTarget(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length <= 1)
            {
                var stringBuilder = Pool.Get<StringBuilder>();
                stringBuilder.Clear();
                stringBuilder.AppendLine("Syntax error of target command");
                stringBuilder.AppendLine("remove.target <disable | d> <player (name or id)> - Disable remover tool for player");
                stringBuilder.AppendLine("remove.target <normal | n> <player (name or id)> [time (seconds)] [max removable objects (integer)] - Enable remover tool for player (Normal)");
                stringBuilder.AppendLine("remove.target <admin | a> <player (name or id)> [time (seconds)] - Enable remover tool for player (Admin)");
                stringBuilder.AppendLine("remove.target <all> <player (name or id)> [time (seconds)] - Enable remover tool for player (All)");
                stringBuilder.AppendLine("remove.target <structure | s> <player (name or id)> [time (seconds)] - Enable remover tool for player (Structure)");
                stringBuilder.AppendLine("remove.target <external | e> <player (name or id)> [time (seconds)] - Enable remover tool for player (External)");
                Print(arg, stringBuilder.ToString());
                stringBuilder.Clear();
                Pool.FreeUnmanaged(ref stringBuilder);
                return;
            }
            var player = arg.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, PERMISSION_TARGET))
            {
                Print(arg, Lang("NotAllowed", player.UserIDString, PERMISSION_TARGET));
                return;
            }
            var target = RustCore.FindPlayer(arg.Args[1]);
            if (target == null || !target.IsConnected)
            {
                Print(arg, target == null ? $"'{arg.Args[0]}' cannot be found." : $"'{target}' is offline.");
                return;
            }
            var removeType = RemoveType.Normal;
            switch (arg.Args[0].ToLower())
            {
                case "n":
                case "normal":
                    break;

                case "a":
                case "admin":
                    removeType = RemoveType.Admin;
                    break;

                case "all":
                    removeType = RemoveType.All;
                    break;

                case "s":
                case "structure":
                    removeType = RemoveType.Structure;
                    break;

                case "e":
                case "external":
                    removeType = RemoveType.External;
                    break;

                case "d":
                case "disable":
                    var toolRemover = target.GetComponent<ToolRemover>();
                    if (toolRemover != null)
                    {
                        toolRemover.DisableTool();
                        Print(arg, $"{target}'s remover tool is disabled");
                    }
                    else
                    {
                        Print(arg, $"{target} did not enable the remover tool");
                    }
                    return;

                default:
                    var stringBuilder = Pool.Get<StringBuilder>();
                    stringBuilder.Clear();
                    stringBuilder.AppendLine("Syntax error of target command");
                    stringBuilder.AppendLine("remove.target <disable | d> <player (name or id)> - Disable remover tool for player");
                    stringBuilder.AppendLine("remove.target <normal | n> <player (name or id)> [time (seconds)] [max removable objects (integer)] - Enable remover tool for player (Normal)");
                    stringBuilder.AppendLine("remove.target <admin | a> <player (name or id)> [time (seconds)] - Enable remover tool for player (Admin)");
                    stringBuilder.AppendLine("remove.target <all> <player (name or id)> [time (seconds)] - Enable remover tool for player (All)");
                    stringBuilder.AppendLine("remove.target <structure | s> <player (name or id)> [time (seconds)] - Enable remover tool for player (Structure)");
                    stringBuilder.AppendLine("remove.target <external | e> <player (name or id)> [time (seconds)] - Enable remover tool for player (External)");
                    Print(arg, stringBuilder.ToString());
                    stringBuilder.Clear();
                    Pool.FreeUnmanaged(ref stringBuilder);
                    return;
            }
            var maxRemovable = 0;
            var time = _configData.removeType[removeType].defaultTime;
            if (arg.Args.Length > 2)
            {
                int.TryParse(arg.Args[2], out time);
            }
            if (arg.Args.Length > 3 && removeType == RemoveType.Normal)
            {
                int.TryParse(arg.Args[3], out maxRemovable);
            }
            var permissionS = _configData.permission[PERMISSION_NORMAL];
            var targetRemover = target.GetOrAddComponent<ToolRemover>();
            targetRemover.Init(removeType, time, maxRemovable, _configData.removeType[removeType].distance, permissionS.removeInterval, permissionS.pay, permissionS.refund, permissionS.resetTime, false);
            Print(arg, Lang("TargetEnabled", player?.UserIDString, target, time, maxRemovable, GetRemoveTypeName(removeType)));
        }

        [ConsoleCommand("remove.building")]
        private void CCmdConstruction(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length <= 1 || !arg.IsAdmin)
            {
                Print(arg, "Syntax error, Please type 'remove.building <price / refund / priceP / refundP> <percentage>', e.g.'remove.building price 60'");
                return;
            }
            float value;
            switch (arg.Args[0].ToLower())
            {
                case "price":
                    if (!float.TryParse(arg.Args[1], out value))
                    {
                        value = 50f;
                    }
                    foreach (var construction in _constructions)
                    {
                        BuildingBlocksSettings buildingBlocksSettings;
                        if (_configData.remove.buildingBlock.TryGetValue(construction.info.name.english, out buildingBlocksSettings))
                        {
                            foreach (var entry in buildingBlocksSettings.buildingGrade)
                            {
                                var grade = construction.GetGrade(entry.Key, 0);
                                var costToBuild = grade.CostToBuild();
                                entry.Value.price = costToBuild.ToDictionary(x => x.itemDef.shortname, y => new CurrencyInfo(value <= 0 ? 0 : Mathf.RoundToInt(y.amount * value / 100)));
                            }
                        }
                    }
                    Print(arg, $"Successfully modified all building prices to {value}% of the initial cost.");
                    SaveConfig();
                    LoadConfig();
                    return;

                case "refund":
                    if (!float.TryParse(arg.Args[1], out value))
                    {
                        value = 40f;
                    }
                    foreach (var construction in _constructions)
                    {
                        BuildingBlocksSettings buildingBlocksSettings;
                        if (_configData.remove.buildingBlock.TryGetValue(construction.info.name.english, out buildingBlocksSettings))
                        {
                            foreach (var entry in buildingBlocksSettings.buildingGrade)
                            {
                                var grade = construction.GetGrade(entry.Key, 0);
                                var costToBuild = grade.CostToBuild();
                                entry.Value.refund = costToBuild.ToDictionary(x => x.itemDef.shortname, y => new CurrencyInfo(value <= 0 ? 0 : Mathf.RoundToInt(y.amount * value / 100)));
                            }
                        }
                    }
                    Print(arg, $"Successfully modified all building refunds to {value}% of the initial cost.");
                    SaveConfig();
                    LoadConfig();
                    return;

                case "pricep":
                    if (!float.TryParse(arg.Args[1], out value))
                    {
                        value = 40f;
                    }
                    foreach (var buildingBlockS in _configData.remove.buildingBlock.Values)
                    {
                        foreach (var data in buildingBlockS.buildingGrade.Values)
                        {
                            data.price = value <= 0 ? 0 : value;
                        }
                    }

                    Print(arg, $"Successfully modified all building prices to {value}% of the initial cost.");
                    SaveConfig();
                    LoadConfig();
                    return;

                case "refundp":
                    if (!float.TryParse(arg.Args[1], out value))
                    {
                        value = 50f;
                    }
                    foreach (var buildingBlockS in _configData.remove.buildingBlock.Values)
                    {
                        foreach (var data in buildingBlockS.buildingGrade.Values)
                        {
                            data.refund = value <= 0 ? 0 : value;
                        }
                    }

                    Print(arg, $"Successfully modified all building refunds to {value}% of the initial cost.");
                    SaveConfig();
                    LoadConfig();
                    return;

                default:
                    Print(arg, "Syntax error, Please type 'remove.building <price / refund / priceP / refundP> <percentage>', e.g.'remove.building price 60'");
                    return;
            }
        }

        [ConsoleCommand("remove.allow")]
        private void CCmdRemoveAllow(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length == 0)
            {
                Print(arg, "Syntax error, Please type 'remove.allow <true | false>'");
                return;
            }
            var player = arg.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, PERMISSION_OVERRIDE))
            {
                Print(arg, Lang("NotAllowed", player.UserIDString, PERMISSION_OVERRIDE));
                return;
            }
            switch (arg.Args[0].ToLower())
            {
                case "true":
                case "1":
                    _removeOverride = false;
                    Print(arg, "Remove is now allowed depending on your settings.");
                    return;

                case "false":
                case "0":
                    _removeOverride = true;
                    Print(arg, "Remove is now restricted for all players (exept admins)");
                    foreach (var p in BasePlayer.activePlayerList)
                    {
                        var toolRemover = p.GetComponent<ToolRemover>();
                        if (toolRemover == null)
                        {
                            continue;
                        }
                        if (toolRemover.RemoveType == RemoveType.Normal && toolRemover.CanOverride)
                        {
                            Print(toolRemover.Player, "The remover tool has been disabled by the admin");
                            toolRemover.DisableTool(false);
                        }
                    }
                    return;

                default:
                    Print(arg, "This is not a valid argument");
                    return;
            }
        }

        [ConsoleCommand("remove.playerentity")]
        private void CCmdRemoveEntity(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length <= 1 || !arg.IsAdmin)
            {
                var stringBuilder = Pool.Get<StringBuilder>();
                stringBuilder.Clear();
                stringBuilder.AppendLine("Syntax error of remove.playerentity command");
                stringBuilder.AppendLine("remove.playerentity <all | a> <player id> - Remove all entities of the player");
                stringBuilder.AppendLine("remove.playerentity <building | b> <player id> - Remove all buildings of the player");
                stringBuilder.AppendLine("remove.playerentity <cupboard | c> <player id> - Remove buildings of the player owned cupboard");
                Print(arg, stringBuilder.ToString());
                stringBuilder.Clear();
                Pool.FreeUnmanaged(ref stringBuilder);
                return;
            }
            if (_removePlayerEntityCoroutine != null)
            {
                Print(arg, "There is already a RemovePlayerEntity running, please wait.");
                return;
            }
            ulong targetID;
            if (!ulong.TryParse(arg.Args[1], out targetID) || !targetID.IsSteamId())
            {
                Print(arg, "Please enter the player's steamID.");
                return;
            }
            PlayerEntityRemoveType playerEntityRemoveType;
            switch (arg.Args[0].ToLower())
            {
                case "a":
                case "all":
                    playerEntityRemoveType = PlayerEntityRemoveType.All;
                    break;

                case "b":
                case "building":
                    playerEntityRemoveType = PlayerEntityRemoveType.Building;
                    break;

                case "c":
                case "cupboard":
                    playerEntityRemoveType = PlayerEntityRemoveType.Cupboard;
                    break;

                default:
                    Print(arg, "This is not a valid argument");
                    return;
            }
            _removePlayerEntityCoroutine = ServerMgr.Instance.StartCoroutine(RemovePlayerEntity(arg, targetID, playerEntityRemoveType));
            Print(arg, "Start running RemovePlayerEntity, please wait.");
        }

        #endregion Commands

        #region Debug

        private void PrintDebug(string message, bool warning = false)
        {
            if (_configData.global.debugEnabled)
            {
                if (warning)
                {
                    PrintWarning(message);
                }
                else
                {
                    Puts(message);
                }
            }
            if (_configData.global.logToFile)
            {
                _debugStringBuilder.AppendLine($"[{DateTime.Now.ToString(CultureInfo.InstalledUICulture)}] | {message}");
            }
        }

        private void SaveDebug()
        {
            if (!_configData.global.logToFile)
            {
                return;
            }
            var debugText = _debugStringBuilder.ToString().Trim();
            _debugStringBuilder.Clear();
            if (!string.IsNullOrEmpty(debugText))
            {
                LogToFile("debug", debugText, this);
            }
        }

        #endregion Debug

        #region RustTranslationAPI

        private string GetItemTranslationByShortName(string language, string itemShortName)
        {
            return (string)RustTranslationAPI.Call("GetItemTranslationByShortName", language, itemShortName);
        }

        private string GetConstructionTranslation(string language, string prefabName)
        {
            return (string)RustTranslationAPI.Call("GetConstructionTranslation", language, prefabName);
        }

        private string GetDeployableTranslation(string language, string deployable)
        {
            return (string)RustTranslationAPI.Call("GetDeployableTranslation", language, deployable);
        }

        private string GetItemDisplayName(string language, string itemShortName)
        {
            if (RustTranslationAPI != null)
            {
                return GetItemTranslationByShortName(language, itemShortName);
            }
            return null;
        }

        private string GetConstructionDisplayName(BasePlayer player, string shortPrefabName, string displayName)
        {
            if (RustTranslationAPI != null)
            {
                var displayName1 = GetConstructionTranslation(lang.GetLanguage(player.UserIDString), shortPrefabName);
                if (!string.IsNullOrEmpty(displayName1))
                {
                    return displayName1;
                }
            }
            return displayName;
        }

        private string GetDeployableDisplayName(BasePlayer player, string deployable, string displayName)
        {
            if (RustTranslationAPI != null)
            {
                var displayName1 = GetDeployableTranslation(lang.GetLanguage(player.UserIDString), deployable);
                if (!string.IsNullOrEmpty(displayName1))
                {
                    return displayName1;
                }
            }
            return displayName;
        }

        #endregion RustTranslationAPI

        #region ConfigurationFile

        private void UpdateConfig()
        {
            var buildingGrades = new[]
            {
                BuildingGrade.Enum.Twigs, BuildingGrade.Enum.Wood,
                BuildingGrade.Enum.Stone, BuildingGrade.Enum.Metal, BuildingGrade.Enum.TopTier
            };

            foreach (var value in buildingGrades)
            {
                if (!_configData.remove.validConstruction.ContainsKey(value))
                {
                    _configData.remove.validConstruction.Add(value, true);
                }
            }

            var newBuildingBlocks = new Dictionary<string, BuildingBlocksSettings>();
            foreach (var construction in _constructions)
            {
                BuildingBlocksSettings buildingBlocksSettings;
                if (!_configData.remove.buildingBlock.TryGetValue(construction.info.name.english, out buildingBlocksSettings))
                {
                    var buildingGrade = new Dictionary<BuildingGrade.Enum,
                            BuildingGradeSettings>();
                    foreach (var value in buildingGrades)
                    {
                        var grade = construction.GetGrade(value, 0);

                        if (grade == null)
                        {
                            continue;
                        }

                        var costToBuild = grade.CostToBuild();
                       
                        buildingGrade.Add(value, new BuildingGradeSettings
                        {
                            refund = costToBuild.ToDictionary(x => x.itemDef.shortname, y => new CurrencyInfo(Mathf.RoundToInt(y.amount * 0.4f))),
                            price = costToBuild.ToDictionary(x => x.itemDef.shortname, y => new CurrencyInfo(Mathf.RoundToInt(y.amount * 0.6f)))
                        });
                    }
                    buildingBlocksSettings = new BuildingBlocksSettings { displayName = construction.info.name.english, buildingGrade = buildingGrade };
                }
                newBuildingBlocks.Add(construction.info.name.english, buildingBlocksSettings);
            }
            _configData.remove.buildingBlock = newBuildingBlocks;
            foreach (var entry in _shortPrefabNameToDeployable)
            {
                EntitySettings entitySettings;
                if (!_configData.remove.entity.TryGetValue(entry.Key, out entitySettings))
                {
                    var itemDefinition = ItemManager.FindItemDefinition(entry.Value);
                    entitySettings = new EntitySettings
                    {
                        enabled = _configData.global.defaultEntity.removeAllowed && itemDefinition.category != ItemCategory.Food,
                        displayName = itemDefinition.displayName.english,
                        refund = new Dictionary<string, CurrencyInfo>
                        {
                            [entry.Value] = new CurrencyInfo(1)
                        },
                        price = new Dictionary<string, CurrencyInfo>()
                    };
                    _configData.remove.entity.Add(entry.Key, entitySettings);
                }
            }
            SaveConfig();
        }

        private static ConfigData _configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Settings")]
            public readonly GlobalSettings global = new GlobalSettings();

            [JsonProperty(PropertyName = "Container Settings")]
            public readonly ContainerSettings container = new ContainerSettings();

            [JsonProperty(PropertyName = "Remove Damaged Entities")]
            public readonly DamagedEntitySettings damagedEntity = new DamagedEntitySettings();

            [JsonProperty(PropertyName = "Chat Settings")]
            public readonly ChatSettings chat = new ChatSettings();

            [JsonProperty(PropertyName = "Permission Settings (Just for normal type)")]
            public readonly Dictionary<string, PermissionSettings> permission = new Dictionary<string, PermissionSettings>
            {
                [PERMISSION_NORMAL] = new PermissionSettings { priority = 0, distance = 3, cooldown = 60, maxTime = 300, maxRemovable = 50, removeInterval = 0.8f, pay = true, refund = true, resetTime = false }
            };

            [JsonProperty(PropertyName = "Remove Type Settings")]
            public readonly Dictionary<RemoveType, RemoveTypeSettings> removeType = new Dictionary<RemoveType, RemoveTypeSettings>
            {
                [RemoveType.Normal] = new RemoveTypeSettings { displayName = RemoveType.Normal.ToString(), distance = 3, gibs = true, defaultTime = 60, maxTime = 300, resetTime = false },
                [RemoveType.Structure] = new RemoveTypeSettings { displayName = RemoveType.Structure.ToString(), distance = 100, gibs = false, defaultTime = 300, maxTime = 600, resetTime = true },
                [RemoveType.All] = new RemoveTypeSettings { displayName = RemoveType.All.ToString(), distance = 50, gibs = false, defaultTime = 300, maxTime = 600, resetTime = true },
                [RemoveType.Admin] = new RemoveTypeSettings { displayName = RemoveType.Admin.ToString(), distance = 20, gibs = true, defaultTime = 300, maxTime = 600, resetTime = true },
                [RemoveType.External] = new RemoveTypeSettings { displayName = RemoveType.External.ToString(), distance = 20, gibs = true, defaultTime = 300, maxTime = 600, resetTime = true }
            };

            [JsonProperty(PropertyName = "Remove Mode Settings (Only one model works)")]
            public readonly RemoverModeSettings removerMode = new RemoverModeSettings();

            [JsonProperty(PropertyName = "NoEscape Settings")]
            public readonly NoEscapeSettings noEscape = new NoEscapeSettings();

            [JsonProperty(PropertyName = "Image Urls (Used to UI image)")]
            public readonly Dictionary<string, string> imageUrls = new Dictionary<string, string>
            {
                [ECONOMICS_KEY] = "https://i.imgur.com/znPwdcv.png",
                [SERVER_REWARDS_KEY] = "https://i.imgur.com/04rJsV3.png"
            };

            [JsonProperty(PropertyName = "GUI")]
            public readonly UiSettings ui = new UiSettings();

            [JsonProperty(PropertyName = "Remove Info (Refund & Price)")]
            public readonly RemoveSettings remove = new RemoveSettings();

            [JsonProperty(PropertyName = "Version")]
            public VersionNumber version;
        }

        public class GlobalSettings
        {
            [JsonProperty(PropertyName = "Enable Debug Mode")]
            public bool debugEnabled;

            [JsonProperty(PropertyName = "Log Debug To File")]
            public bool logToFile;

            [JsonProperty(PropertyName = "Use Teams")]
            public bool useTeams = false;

            [JsonProperty(PropertyName = "Use Clans")]
            public bool useClans = true;

            [JsonProperty(PropertyName = "Use Friends")]
            public bool useFriends = true;

            [JsonProperty(PropertyName = "Use Entity Owners")]
            public bool useEntityOwners = true;

            [JsonProperty(PropertyName = "Use Building Locks")]
            public bool useBuildingLocks = false;

            [JsonProperty(PropertyName = "Use Tool Cupboards (Strongly unrecommended)")]
            public bool useToolCupboards = false;

            [JsonProperty(PropertyName = "Use Building Owners (You will need BuildingOwners plugin)")]
            public bool useBuildingOwners = false;

            //[JsonProperty(PropertyName = "Exclude Twigs (Used for \"Use Tool Cupboards\" and \"Use Entity Owners\")")]
            //public bool excludeTwigs;

            [JsonProperty(PropertyName = "Remove Button")]
            public string removeButton = BUTTON.FIRE_PRIMARY.ToString();

            [JsonProperty(PropertyName = "Remove Interval (Min = 0.2)")]
            public float removeInterval = 0.5f;

            [JsonProperty(PropertyName = "Only start cooldown when an entity is removed")]
            public bool startCooldownOnRemoved;

            [JsonProperty(PropertyName = "RemoveType - All/Structure - Remove per frame")]
            public int removePerFrame = 15;

            [JsonProperty(PropertyName = "RemoveType - All/Structure - No item container dropped")]
            public bool noItemContainerDrop = true;

            [JsonProperty(PropertyName = "RemoveType - Normal - Max Removable Objects - Exclude admins")]
            public bool maxRemovableExclude = true;

            [JsonProperty(PropertyName = "RemoveType - Normal - Cooldown - Exclude admins")]
            public bool cooldownExclude = true;

            [JsonProperty(PropertyName = "RemoveType - Normal - Entity Spawned Time Limit - Enabled")]
            public bool entityTimeLimit = false;

            [JsonProperty(PropertyName = "RemoveType - Normal - Entity Spawned Time Limit - Cannot be removed when entity spawned time more than it")]
            public float limitTime = 300f;

            [JsonProperty(PropertyName = "Default Entity Settings (When automatically adding new entities to 'Other Entity Settings')")]
            public DefaultEntitySettings defaultEntity = new DefaultEntitySettings();

            public class DefaultEntitySettings
            {
                [JsonProperty(PropertyName = "Default Remove Allowed")]
                public bool removeAllowed = true;
            }
        }

        public class ContainerSettings
        {
            [JsonProperty(PropertyName = "Storage Container - Enable remove of not empty storages")]
            public bool removeNotEmptyStorage = true;

            [JsonProperty(PropertyName = "Storage Container - Drop items from container")]
            public bool dropItemsStorage = false;

            [JsonProperty(PropertyName = "Storage Container - Drop a item container from container")]
            public bool dropContainerStorage = true;

            [JsonProperty(PropertyName = "IOEntity Container - Enable remove of not empty storages")]
            public bool removeNotEmptyIoEntity = true;

            [JsonProperty(PropertyName = "IOEntity Container - Drop items from container")]
            public bool dropItemsIoEntity = false;

            [JsonProperty(PropertyName = "IOEntity Container - Drop a item container from container")]
            public bool dropContainerIoEntity = true;
        }

        public class DamagedEntitySettings
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool enabled = false;

            [JsonProperty(PropertyName = "Exclude Quarries")]
            public bool excludeQuarries = true;

            [JsonProperty(PropertyName = "Exclude Building Blocks")]
            public bool excludeBuildingBlocks = true;

            [JsonProperty(PropertyName = "Percentage (Can be removed when (health / max health * 100) is not less than it)")]
            public float percentage = 90f;
        }

        public class ChatSettings
        {
            [JsonProperty(PropertyName = "Chat Command")]
            public string command = "remove";

            [JsonProperty(PropertyName = "Chat Prefix")]
            public string prefix = "<color=#00FFFF>[RemoverTool]</color>: ";

            [JsonProperty(PropertyName = "Chat SteamID Icon")]
            public ulong steamIDIcon = 0;

            [JsonProperty(PropertyName = "Show Message When Enabled/Disabled")]
            public bool showMessageWhenEnabledOrDisabled = true;
        }

        public class PermissionSettings
        {
            [JsonProperty(PropertyName = "Priority")]
            public int priority;

            [JsonProperty(PropertyName = "Distance")]
            public float distance;

            [JsonProperty(PropertyName = "Cooldown")]
            public float cooldown;

            [JsonProperty(PropertyName = "Max Time")]
            public int maxTime;

            [JsonProperty(PropertyName = "Remove Interval (Min = 0.2)")]
            public float removeInterval;

            [JsonProperty(PropertyName = "Max Removable Objects (0 = Unlimited)")]
            public int maxRemovable;

            [JsonProperty(PropertyName = "Pay")]
            public bool pay;

            [JsonProperty(PropertyName = "Refund")]
            public bool refund;

            [JsonProperty(PropertyName = "Reset the time after removing an entity")]
            public bool resetTime;
        }

        public class RemoveTypeSettings
        {
            [JsonProperty(PropertyName = "Display Name")]
            public string displayName;

            [JsonProperty(PropertyName = "Distance")]
            public float distance;

            [JsonProperty(PropertyName = "Default Time")]
            public int defaultTime;

            [JsonProperty(PropertyName = "Max Time")]
            public int maxTime;

            [JsonProperty(PropertyName = "Gibs")]
            public bool gibs;

            [JsonProperty(PropertyName = "Reset the time after removing an entity")]
            public bool resetTime;
        }

        public class RemoverModeSettings
        {
            [JsonProperty(PropertyName = "No Held Item Mode")]
            public bool noHeldMode = true;

            [JsonProperty(PropertyName = "No Held Item Mode - Disable remover tool when you have any item in hand")]
            public bool noHeldDisableInHand = true;

            [JsonProperty(PropertyName = "Melee Tool Hit Mode")]
            public bool meleeHitMode;

            [JsonProperty(PropertyName = "Melee Tool Hit Mode - Item shortname")]
            public string meleeHitItemShortname = "hammer";

            [JsonProperty(PropertyName = "Melee Tool Hit Mode - Item skin (-1 = All skins)")]
            public long meleeHitModeSkin = -1;

            [JsonProperty(PropertyName = "Melee Tool Hit Mode - Auto enable remover tool when you hold a melee tool")]
            public bool meleeHitEnableInHand = false;

            [JsonProperty(PropertyName = "Melee Tool Hit Mode - Requires a melee tool in your hand when remover tool is enabled")]
            public bool meleeHitRequires;

            [JsonProperty(PropertyName = "Melee Tool Hit Mode - Disable remover tool when you are not holding a melee tool")]
            public bool meleeHitDisableInHand;

            [JsonProperty(PropertyName = "Specific Tool Mode")]
            public bool specificToolMode = false;

            [JsonProperty(PropertyName = "Specific Tool Mode - Item shortname")]
            public string specificToolShortName = "hammer";

            [JsonProperty(PropertyName = "Specific Tool Mode - Item skin (-1 = All skins)")]
            public long specificToolSkin = -1;

            [JsonProperty(PropertyName = "Specific Tool Mode - Auto enable remover tool when you hold a specific tool")]
            public bool specificToolEnableInHand = false;

            [JsonProperty(PropertyName = "Specific Tool Mode - Requires a specific tool in your hand when remover tool is enabled")]
            public bool specificToolRequires = false;

            [JsonProperty(PropertyName = "Specific Tool Mode - Disable remover tool when you are not holding a specific tool")]
            public bool specificToolDisableInHand = false;
        }

        public class NoEscapeSettings
        {
            [JsonProperty(PropertyName = "Use Raid Blocker")]
            public bool useRaidBlocker;

            [JsonProperty(PropertyName = "Use Combat Blocker")]
            public bool useCombatBlocker;
        }

        public class UiSettings
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool enabled = true;

            [JsonProperty(PropertyName = "Main Box - Min Anchor (in Rust Window)")]
            public string removerToolAnchorMin = "0 1";

            [JsonProperty(PropertyName = "Main Box - Max Anchor (in Rust Window)")]
            public string removerToolAnchorMax = "0 1";

            [JsonProperty(PropertyName = "Main Box - Min Offset (in Rust Window)")]
            public string removerToolOffsetMin = "30 -330";

            [JsonProperty(PropertyName = "Main Box - Max Offset (in Rust Window)")]
            public string removerToolOffsetMax = "470 -40";

            [JsonProperty(PropertyName = "Main Box - Background Color")]
            public string removerToolBackgroundColor = "0 0 0 0";

            [JsonProperty(PropertyName = "Remove Title - Box - Min Anchor (in Main Box)")]
            public string removeAnchorMin = "0 0.84";

            [JsonProperty(PropertyName = "Remove Title - Box - Max Anchor (in Main Box)")]
            public string removeAnchorMax = "0.996 1";

            [JsonProperty(PropertyName = "Remove Title - Box - Background Color")]
            public string removeBackgroundColor = "0.31 0.88 0.71 1";

            [JsonProperty(PropertyName = "Remove Title - Text - Min Anchor (in Main Box)")]
            public string removeTextAnchorMin = "0.05 0.84";

            [JsonProperty(PropertyName = "Remove Title - Text - Max Anchor (in Main Box)")]
            public string removeTextAnchorMax = "0.6 1";

            [JsonProperty(PropertyName = "Remove Title - Text - Text Color")]
            public string removeTextColor = "1 0.1 0.1 1";

            [JsonProperty(PropertyName = "Remove Title - Text - Text Size")]
            public int removeTextSize = 18;

            [JsonProperty(PropertyName = "Timeleft - Box - Min Anchor (in Main Box)")]
            public string timeLeftAnchorMin = "0.6 0.84";

            [JsonProperty(PropertyName = "Timeleft - Box - Max Anchor (in Main Box)")]
            public string timeLeftAnchorMax = "1 1";

            [JsonProperty(PropertyName = "Timeleft - Box - Background Color")]
            public string timeLeftBackgroundColor = "0 0 0 0";

            [JsonProperty(PropertyName = "Timeleft - Text - Min Anchor (in Timeleft Box)")]
            public string timeLeftTextAnchorMin = "0 0";

            [JsonProperty(PropertyName = "Timeleft - Text - Max Anchor (in Timeleft Box)")]
            public string timeLeftTextAnchorMax = "0.9 1";

            [JsonProperty(PropertyName = "Timeleft - Text - Text Color")]
            public string timeLeftTextColor = "0 0 0 0.9";

            [JsonProperty(PropertyName = "Timeleft - Text - Text Size")]
            public int timeLeftTextSize = 15;

            [JsonProperty(PropertyName = "Entity - Box - Min Anchor (in Main Box)")]
            public string entityAnchorMin = "0 0.68";

            [JsonProperty(PropertyName = "Entity - Box - Max Anchor (in Main Box)")]
            public string entityAnchorMax = "1 0.84";

            [JsonProperty(PropertyName = "Entity - Box - Background Color")]
            public string entityBackgroundColor = "0.82 0.58 0.30 1";

            [JsonProperty(PropertyName = "Entity - Text - Min Anchor (in Entity Box)")]
            public string entityTextAnchorMin = "0.05 0";

            [JsonProperty(PropertyName = "Entity - Text - Max Anchor (in Entity Box)")]
            public string entityTextAnchorMax = "1 1";

            [JsonProperty(PropertyName = "Entity - Text - Text Color")]
            public string entityTextColor = "1 1 1 1";

            [JsonProperty(PropertyName = "Entity - Text - Text Size")]
            public int entityTextSize = 16;

            [JsonProperty(PropertyName = "Entity - Image - Enabled")]
            public bool entityImageEnabled = true;

            [JsonProperty(PropertyName = "Entity - Image - Min Anchor (in Entity Box)")]
            public string entityImageAnchorMin = "0.795 0.01";

            [JsonProperty(PropertyName = "Entity - Image - Max Anchor (in Entity Box)")]
            public string entityImageAnchorMax = "0.9 0.99";

            [JsonProperty(PropertyName = "Authorization Check Enabled")]
            public bool authorizationEnabled = true;

            [JsonProperty(PropertyName = "Authorization Check - Box - Min Anchor (in Main Box)")]
            public string authorizationsAnchorMin = "0 0.6";

            [JsonProperty(PropertyName = "Authorization Check - Box - Max Anchor (in Main Box)")]
            public string authorizationsAnchorMax = "1 0.68";

            [JsonProperty(PropertyName = "Authorization Check - Box - Allowed Background Color")]
            public string allowedBackgroundColor = "0.22 0.78 0.27 1";

            [JsonProperty(PropertyName = "Authorization Check - Box - Refused Background Color")]
            public string refusedBackgroundColor = "0.78 0.22 0.27 1";

            [JsonProperty(PropertyName = "Authorization Check - Text - Min Anchor (in Authorization Check Box)")]
            public string authorizationsTextAnchorMin = "0.05 0";

            [JsonProperty(PropertyName = "Authorization Check - Text - Max Anchor (in Authorization Check Box)")]
            public string authorizationsTextAnchorMax = "1 1";

            [JsonProperty(PropertyName = "Authorization Check - Text - Text Color")]
            public string authorizationsTextColor = "1 1 1 0.9";

            [JsonProperty(PropertyName = "Authorization Check Box - Text - Text Size")]
            public int authorizationsTextSize = 14;

            [JsonProperty(PropertyName = "Price & Refund - Image Enabled")]
            public bool imageEnabled = true;

            [JsonProperty(PropertyName = "Price & Refund - Image Scale")]
            public float imageScale = 0.18f;

            [JsonProperty(PropertyName = "Price & Refund - Distance of image from right border")]
            public float rightDistance = 0.05f;

            [JsonProperty(PropertyName = "Price Enabled")]
            public bool priceEnabled = true;

            [JsonProperty(PropertyName = "Price - Box - Min Anchor (in Main Box)")]
            public string priceAnchorMin = "0 0.3";

            [JsonProperty(PropertyName = "Price - Box - Max Anchor (in Main Box)")]
            public string priceAnchorMax = "1 0.6";

            [JsonProperty(PropertyName = "Price - Box - Background Color")]
            public string priceBackgroundColor = "0 0 0 0.8";

            [JsonProperty(PropertyName = "Price - Text - Min Anchor (in Price Box)")]
            public string priceTextAnchorMin = "0.05 0";

            [JsonProperty(PropertyName = "Price - Text - Max Anchor (in Price Box)")]
            public string priceTextAnchorMax = "0.25 1";

            [JsonProperty(PropertyName = "Price - Text - Text Color")]
            public string priceTextColor = "1 1 1 0.9";

            [JsonProperty(PropertyName = "Price - Text - Text Size")]
            public int priceTextSize = 18;

            [JsonProperty(PropertyName = "Price - Text2 - Min Anchor (in Price Box)")]
            public string price2TextAnchorMin = "0.3 0";

            [JsonProperty(PropertyName = "Price - Text2 - Max Anchor (in Price Box)")]
            public string price2TextAnchorMax = "1 1";

            [JsonProperty(PropertyName = "Price - Text2 - Text Color")]
            public string price2TextColor = "1 1 1 0.9";

            [JsonProperty(PropertyName = "Price - Text2 - Text Size")]
            public int price2TextSize = 16;

            [JsonProperty(PropertyName = "Refund Enabled")]
            public bool refundEnabled = true;

            [JsonProperty(PropertyName = "Refund - Box - Min Anchor (in Main Box)")]
            public string refundAnchorMin = "0 0";

            [JsonProperty(PropertyName = "Refund - Box - Max Anchor (in Main Box)")]
            public string refundAnchorMax = "1 0.3";

            [JsonProperty(PropertyName = "Refund - Box - Background Color")]
            public string refundBackgroundColor = "0 0 0 0.8";

            [JsonProperty(PropertyName = "Refund - Text - Min Anchor (in Refund Box)")]
            public string refundTextAnchorMin = "0.05 0";

            [JsonProperty(PropertyName = "Refund - Text - Max Anchor (in Refund Box)")]
            public string refundTextAnchorMax = "0.25 1";

            [JsonProperty(PropertyName = "Refund - Text - Text Color")]
            public string refundTextColor = "1 1 1 0.9";

            [JsonProperty(PropertyName = "Refund - Text - Text Size")]
            public int refundTextSize = 18;

            [JsonProperty(PropertyName = "Refund - Text2 - Min Anchor (in Refund Box)")]
            public string refund2TextAnchorMin = "0.3 0";

            [JsonProperty(PropertyName = "Refund - Text2 - Max Anchor (in Refund Box)")]
            public string refund2TextAnchorMax = "1 1";

            [JsonProperty(PropertyName = "Refund - Text2 - Text Color")]
            public string refund2TextColor = "1 1 1 0.9";

            [JsonProperty(PropertyName = "Refund - Text2 - Text Size")]
            public int refund2TextSize = 16;

            [JsonProperty(PropertyName = "Crosshair - Enabled")]
            public bool showCrosshair = true;

            [JsonProperty(PropertyName = "Crosshair - Image Url")]
            public string crosshairImageUrl = "https://i.imgur.com/SqLCJaQ.png";

            [JsonProperty(PropertyName = "Crosshair - Box - Min Anchor (in Rust Window)")]
            public string crosshairAnchorMin = "0.5 0.5";

            [JsonProperty(PropertyName = "Crosshair - Box - Max Anchor (in Rust Window)")]
            public string crosshairAnchorMax = "0.5 0.5";

            [JsonProperty(PropertyName = "Crosshair - Box - Min Offset (in Rust Window)")]
            public string crosshairOffsetMin = "-15 -15";

            [JsonProperty(PropertyName = "Crosshair - Box - Max Offset (in Rust Window)")]
            public string crosshairOffsetMax = "15 15";

            [JsonProperty(PropertyName = "Crosshair - Box - Image Color")]
            public string crosshairColor = "1 0 0 1";

            [JsonIgnore]
            public Vector2 Price2TextAnchorMin, Price2TextAnchorMax, Refund2TextAnchorMin, Refund2TextAnchorMax;
        }

        public class RemoveSettings
        {
            [JsonProperty(PropertyName = "Price Enabled")]
            public bool priceEnabled = true;

            [JsonProperty(PropertyName = "Refund Enabled")]
            public bool refundEnabled = true;

            [JsonProperty(PropertyName = "Refund Items In Entity Slot")]
            public bool refundSlot = true;

            [JsonProperty(PropertyName = "Allowed Building Grade")]
            public Dictionary<BuildingGrade.Enum, bool> validConstruction = new Dictionary<BuildingGrade.Enum, bool>();

            [JsonProperty(PropertyName = "Display Names (Refund & Price)")]
            public readonly Dictionary<string, string> displayNames = new Dictionary<string, string>();

            [JsonProperty(PropertyName = "Building Blocks Settings")]
            public Dictionary<string, BuildingBlocksSettings> buildingBlock = new Dictionary<string, BuildingBlocksSettings>();

            [JsonProperty(PropertyName = "Other Entity Settings")]
            public Dictionary<string, EntitySettings> entity = new Dictionary<string, EntitySettings>();
        }

        public class BuildingBlocksSettings
        {
            [JsonProperty(PropertyName = "Display Name")]
            public string displayName;

            [JsonProperty(PropertyName = "Building Grade")]
            public Dictionary<BuildingGrade.Enum, BuildingGradeSettings> buildingGrade = new Dictionary<BuildingGrade.Enum, BuildingGradeSettings>();
        }

        public class BuildingGradeSettings
        {
            [JsonProperty(PropertyName = "Price")]
            public object price;

            [JsonProperty(PropertyName = "Refund")]
            public object refund;

            [JsonIgnore]
            public float pricePercentage = -1, refundPercentage = -1;

            [JsonIgnore]
            public Dictionary<string, CurrencyInfo> priceDict, refundDict;
        }

        public class EntitySettings
        {
            [JsonProperty(PropertyName = "Remove Allowed")]
            public bool enabled;

            [JsonProperty(PropertyName = "Display Name")]
            public string displayName = string.Empty;

            [JsonProperty(PropertyName = "Price")]
            public object price;

            [JsonProperty(PropertyName = "Refund")]
            public object refund;

            [JsonIgnore]
            public Dictionary<string, CurrencyInfo> priceDict, refundDict;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                PreprocessOldConfig();
                _configData = Config.ReadObject<ConfigData>();
                if (_configData == null)
                {
                    LoadDefaultConfig();
                }
                else
                {
                    UpdateConfigValues();
                }
            }
            catch (Exception ex)
            {
                PrintError($"The configuration file is corrupted. \n{ex}");

                LoadDefaultConfig();
            }
            SaveConfig();
            PreprocessConfigValues();
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            _configData = new ConfigData();
            _configData.version = Version;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_configData);
        }

        private void PreprocessConfigValues()
        {
            _configData.ui.Price2TextAnchorMin = GetAnchor(_configData.ui.price2TextAnchorMin);
            _configData.ui.Price2TextAnchorMax = GetAnchor(_configData.ui.price2TextAnchorMax);
            _configData.ui.Refund2TextAnchorMin = GetAnchor(_configData.ui.refund2TextAnchorMin);
            _configData.ui.Refund2TextAnchorMax = GetAnchor(_configData.ui.refund2TextAnchorMax);

            foreach (var entry in _configData.remove.buildingBlock)
            {
                foreach (var gradeEntry in entry.Value.buildingGrade)
                {
                    var price = gradeEntry.Value.price;
                    float pricePercentage;
                    if (float.TryParse(price.ToString(), out pricePercentage))
                    {
                        gradeEntry.Value.pricePercentage = pricePercentage;
                    }
                    else
                    {
                        var priceDic = price as Dictionary<string, CurrencyInfo>;
                        if (priceDic != null)
                        {
                            gradeEntry.Value.priceDict = priceDic;
                        }
                        else
                        {
                            try
                            {
                                gradeEntry.Value.priceDict = JsonConvert.DeserializeObject<Dictionary<string, CurrencyInfo>>(price.ToString());
                            }
                            catch (Exception e)
                            {
                                gradeEntry.Value.priceDict = null;
                                PrintError($"Wrong price format for '{gradeEntry.Key}' of '{entry.Key}' in 'Building Blocks Settings'. Error Message: {e.Message}");
                            }
                        }
                    }

                    var refund = gradeEntry.Value.refund;
                    float refundPercentage;
                    if (float.TryParse(refund.ToString(), out refundPercentage))
                    {
                        gradeEntry.Value.refundPercentage = refundPercentage;
                    }
                    else
                    {
                        var refundDic = refund as Dictionary<string, CurrencyInfo>;
                        if (refundDic != null)
                        {
                            gradeEntry.Value.refundDict = refundDic;
                        }
                        else
                        {
                            try
                            {
                                gradeEntry.Value.refundDict = JsonConvert.DeserializeObject<Dictionary<string, CurrencyInfo>>(refund.ToString());
                            }
                            catch (Exception e)
                            {
                                gradeEntry.Value.refundDict = null;
                                PrintError($"Wrong refund format for '{gradeEntry.Key}' of '{entry.Key}' in 'Building Blocks Settings'. Error Message: {e.Message}");
                            }
                        }
                    }
                }
            }
            foreach (var entry in _configData.remove.entity)
            {
                var price = entry.Value.price;
                var priceDic = price as Dictionary<string, CurrencyInfo>;
                if (priceDic != null)
                {
                    entry.Value.priceDict = priceDic;
                }
                else
                {
                    try
                    {
                        entry.Value.priceDict = JsonConvert.DeserializeObject<Dictionary<string, CurrencyInfo>>(price.ToString());
                    }
                    catch (Exception e)
                    {
                        entry.Value.priceDict = null;
                        PrintError($"Wrong price format for '{entry.Key}' of '{entry.Key}' in 'Other Entity Settings'. Error Message: {e.Message}");
                    }
                }

                var refund = entry.Value.refund;
                var refundDic = refund as Dictionary<string, CurrencyInfo>;
                if (refundDic != null)
                {
                    entry.Value.refundDict = refundDic;
                }
                else
                {
                    try
                    {
                        entry.Value.refundDict = JsonConvert.DeserializeObject<Dictionary<string, CurrencyInfo>>(refund.ToString());
                    }
                    catch (Exception e)
                    {
                        entry.Value.refundDict = null;
                        PrintError($"Wrong refund format for '{entry.Key}' of '{entry.Key}' in 'Other Entity Settings'. Error Message: {e.Message}");
                    }
                }
            }
        }

        private void UpdateConfigValues()
        {
            if (_configData.version < Version)
            {
                if (_configData.version <= default(VersionNumber))
                {
                    string prefix, prefixColor;
                    if (GetConfigValue(out prefix, "Chat Settings", "Chat Prefix") && GetConfigValue(out prefixColor, "Chat Settings", "Chat Prefix Color"))
                    {
                        _configData.chat.prefix = $"<color={prefixColor}>{prefix}</color>: ";
                    }

                    if (_configData.ui.removerToolAnchorMin == "0.1 0.55")
                    {
                        _configData.ui.removerToolAnchorMin = "0.04 0.55";
                    }

                    if (_configData.ui.removerToolAnchorMax == "0.4 0.95")
                    {
                        _configData.ui.removerToolAnchorMax = "0.37 0.95";
                    }
                }

                if (_configData.version <= new VersionNumber(4, 3, 22))
                {
                    bool enabled;
                    if (GetConfigValue(out enabled, "Remove Mode Settings (Only one model works)", "Hammer Hit Mode"))
                    {
                        _configData.removerMode.meleeHitMode = true;
                        _configData.removerMode.meleeHitItemShortname = "hammer";
                    }
                    if (GetConfigValue(out enabled, "Remove Mode Settings (Only one model works)", "Hammer Hit Mode - Requires a hammer in your hand when remover tool is enabled"))
                    {
                        _configData.removerMode.meleeHitRequires = true;
                    }
                    if (GetConfigValue(out enabled, "Remove Mode Settings (Only one model works)", "Hammer Hit Mode - Disable remover tool when you are not holding a hammer"))
                    {
                        _configData.removerMode.meleeHitDisableInHand = true;
                    }
                }

                if (_configData.version <= new VersionNumber(4, 3, 23))
                {
                    string value;
                    if (GetConfigValue(out value, "GUI", "Authorization Check - Box - Allowed Background"))
                    {
                        _configData.ui.allowedBackgroundColor = value == "0 1 0 0.8" ? "0.22 0.78 0.27 1" : value;
                    }
                    if (GetConfigValue(out value, "GUI", "Authorization Check - Box - Refused Background"))
                    {
                        _configData.ui.refusedBackgroundColor = value == "1 0 0 0.8" ? "0.78 0.22 0.27 1" : value;
                    }
                    if (_configData.ui.removeBackgroundColor == "0.42 0.88 0.88 1")
                    {
                        _configData.ui.removeBackgroundColor = "0.31 0.88 0.71 1";
                    }
                    if (_configData.ui.entityBackgroundColor == "0 0 0 0.8")
                    {
                        _configData.ui.entityBackgroundColor = "0.82 0.58 0.30 1";
                    }
                }
                _configData.version = Version;
            }
        }

        private bool GetConfigValue<T>(out T value, params string[] path)
        {
            var configValue = Config.Get(path);
            if (configValue != null)
            {
                if (configValue is T)
                {
                    value = (T)configValue;
                    return true;
                }
                try
                {
                    value = Config.ConvertValue<T>(configValue);
                    return true;
                }
                catch (Exception ex)
                {
                    PrintError($"GetConfigValue ERROR: path: {string.Join("\\", path)}\n{ex}");
                }
            }

            value = default(T);
            return false;
        }

        private void SetConfigValue(params object[] pathAndTrailingValue)
        {
            Config.Set(pathAndTrailingValue);
        }

        #region Preprocess Old Config

        private void PreprocessOldConfig()
        {
            var config = Config.ReadObject<JObject>();
            if (config == null)
            {
                return;
            }
            //Interface.Oxide.DataFileSystem.WriteObject(Name + "_old", jObject);
            VersionNumber oldVersion;
            if (GetConfigVersionPre(config, out oldVersion))
            {
                if (oldVersion < Version)
                {
                    //Fixed typos
                    if (oldVersion <= new VersionNumber(4, 3, 23))
                    {
                        foreach (RemoveType value in Enum.GetValues(typeof(RemoveType)))
                        {
                            if (value == RemoveType.None)
                            {
                                continue;
                            }
                            bool enabled;
                            if (GetConfigValuePre(config, out enabled, "Remove Type Settings", value.ToString(), "Reset the time after removing a entity"))
                            {
                                SetConfigValuePre(config, enabled, "Remove Type Settings", value.ToString(), "Reset the time after removing an entity");
                            }
                        }
                        Dictionary<string, object> values;
                        if (GetConfigValuePre(config, out values, "Permission Settings (Just for normal type)"))
                        {
                            foreach (var entry in values)
                            {
                                object value;
                                if (GetConfigValuePre(config, out value, "Permission Settings (Just for normal type)", entry.Key, "Reset the time after removing a entity"))
                                {
                                    SetConfigValuePre(config, value, "Permission Settings (Just for normal type)", entry.Key, "Reset the time after removing an entity");
                                }
                            }
                        }
                    }

                    if (oldVersion <= new VersionNumber(4, 3, 25))
                    {
                        bool enabled;
                        if (GetConfigValuePre(config, out enabled, "Remove Mode Settings (Only one model works)", "No Held Item Mode - Show Crosshair"))
                        {
                            SetConfigValuePre(config, enabled, "GUI", "Crosshair - Enabled");
                        }
                        object value;
                        if (GetConfigValuePre(config, out value, "Remove Mode Settings (Only one model works)", "No Held Item Mode - Crosshair Image Url"))
                        {
                            SetConfigValuePre(config, value, "GUI", "Crosshair - Image Url");
                        }
                        if (GetConfigValuePre(config, out value, "Remove Mode Settings (Only one model works)", "No Held Item Mode - Crosshair Box - Min Anchor (in Rust Window)"))
                        {
                            SetConfigValuePre(config, value, "GUI", "Crosshair - Box - Min Anchor (in Rust Window)");
                        }
                        if (GetConfigValuePre(config, out value, "Remove Mode Settings (Only one model works)", "No Held Item Mode - Crosshair Box - Max Anchor (in Rust Window)"))
                        {
                            SetConfigValuePre(config, value, "GUI", "Crosshair - Box - Max Anchor (in Rust Window)");
                        }
                        if (GetConfigValuePre(config, out value, "Remove Mode Settings (Only one model works)", "No Held Item Mode - Crosshair Box - Min Offset (in Rust Window)"))
                        {
                            SetConfigValuePre(config, value, "GUI", "Crosshair - Box - Min Offset (in Rust Window)");
                        }
                        if (GetConfigValuePre(config, out value, "Remove Mode Settings (Only one model works)", "No Held Item Mode - Crosshair Box - Max Offset (in Rust Window)"))
                        {
                            SetConfigValuePre(config, value, "GUI", "Crosshair - Box - Max Offset (in Rust Window)");
                        }
                        if (GetConfigValuePre(config, out value, "Remove Mode Settings (Only one model works)", "No Held Item Mode - Crosshair Box - Image Color"))
                        {
                            SetConfigValuePre(config, value, "GUI", "Crosshair - Box - Image Color");
                        }
                    }
                    if (oldVersion <= new VersionNumber(4, 3, 32))
                    {
                        try
                        {
                            var permissions = GetConfigValue(config, "Permission Settings (Just for normal type)");
                            if (permissions != null)
                            {
                                foreach (var perm in permissions)
                                {
                                    var maxRemovables = perm.Value.Value<int>("Max Removable Objects (0 = Unlimit)");
                                    perm.Value["Max Removable Objects (0 = Unlimited)"] = JToken.FromObject(maxRemovables);
                                }
                            }
                            var entities = GetConfigValue(config, "Remove Info (Refund & Price)", "Other Entity Settings");
                            if (entities != null)
                            {
                                foreach (var entity in entities)
                                {
                                    var price = entity.Value.Value<JObject>("Price");
                                    var priceDict = price?.ToObject<Dictionary<string, int>>();
                                    if (priceDict != null)
                                    {
                                        entity.Value["Price"] = JToken.FromObject(priceDict.ToDictionary(x => x.Key, y => new CurrencyInfo(y.Value)));
                                    }
                                    var refund = entity.Value.Value<JObject>("Refund");
                                    var refundDict = refund?.ToObject<Dictionary<string, int>>();
                                    if (refundDict != null)
                                    {
                                        entity.Value["Refund"] = JToken.FromObject(refundDict.ToDictionary(x => x.Key, y => new CurrencyInfo(y.Value)));
                                    }
                                }
                            }

                            var buildingBlocks = GetConfigValue(config, "Remove Info (Refund & Price)", "Building Blocks Settings");
                            if (buildingBlocks != null)
                            {
                                foreach (var item in buildingBlocks)
                                {
                                    var buildingGrades = item.Value.Value<JObject>("Building Grade");
                                    foreach (var buildingGrade in buildingGrades)
                                    {
                                        float percentage;
                                        var price = buildingGrade.Value.Value<object>("Price");
                                        if (price != null && !float.TryParse(price.ToString(), out percentage))
                                        {
                                            var target = buildingGrade.Value["Price"] as JObject;
                                            var dict = target?.ToObject<Dictionary<string, int>>();
                                            if (dict != null)
                                            {
                                                buildingGrade.Value["Price"] = JToken.FromObject(dict.ToDictionary(x => x.Key, y => new CurrencyInfo(y.Value)));
                                            }
                                        }
                                        var refund = buildingGrade.Value.Value<object>("Refund");
                                        if (refund != null && !float.TryParse(refund.ToString(), out percentage))
                                        {
                                            var target = buildingGrade.Value["Refund"] as JObject;
                                            var dict = target?.ToObject<Dictionary<string, int>>();
                                            if (dict != null)
                                            {
                                                buildingGrade.Value["Refund"] = JToken.FromObject(dict.ToDictionary(x => x.Key, y => new CurrencyInfo(y.Value)));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                    Config.WriteObject(config);
                    // Interface.Oxide.DataFileSystem.WriteObject(Name + "_new", jObject);
                }
            }
        }

        private JObject GetConfigValue(JObject config, params string[] path)
        {
            if (path.Length < 1)
            {
                throw new ArgumentException("path is empty");
            }

            try
            {
                JToken jToken;
                if (!config.TryGetValue(path[0], out jToken))
                {
                    return null;
                }

                for (var i = 1; i < path.Length; i++)
                {
                    var jObject = jToken as JObject;
                    if (jObject == null || !jObject.TryGetValue(path[i], out jToken))
                    {
                        return null;
                    }
                }
                return jToken as JObject;
            }
            catch (Exception ex)
            {
                PrintError($"GetConfigValue ERROR: path: {string.Join("\\", path)}\n{ex}");
            }
            return null;
        }

        private bool GetConfigValuePre<T>(JObject config, out T value, params string[] path)
        {
            if (path.Length < 1)
            {
                throw new ArgumentException("path is empty");
            }

            try
            {
                JToken jToken;
                if (!config.TryGetValue(path[0], out jToken))
                {
                    value = default(T);
                    return false;
                }

                for (var i = 1; i < path.Length; i++)
                {
                    var jObject = jToken.ToObject<JObject>();
                    if (jObject == null || !jObject.TryGetValue(path[i], out jToken))
                    {
                        value = default(T);
                        return false;
                    }
                }
                value = jToken.ToObject<T>();
                return true;
            }
            catch (Exception ex)
            {
                PrintError($"GetConfigValuePre ERROR: path: {string.Join("\\", path)}\n{ex}");
            }
            value = default(T);
            return false;
        }

        private void SetConfigValuePre(JObject config, object value, params string[] path)
        {
            if (path.Length < 1)
            {
                throw new ArgumentException("path is empty");
            }

            try
            {
                JToken jToken;
                if (!config.TryGetValue(path[0], out jToken))
                {
                    if (path.Length == 1)
                    {
                        jToken = JToken.FromObject(value);
                        config.Add(path[0], jToken);
                        return;
                    }
                    jToken = new JObject();
                    config.Add(path[0], jToken);
                }

                for (var i = 1; i < path.Length - 1; i++)
                {
                    var jObject = jToken as JObject;
                    if (jObject == null || !jObject.TryGetValue(path[i], out jToken))
                    {
                        jToken = new JObject();
                        jObject?.Add(path[i], jToken);
                    }
                }
                var targetToken = jToken as JObject;
                if (targetToken != null)
                {
                    targetToken[path[path.Length - 1]] = JToken.FromObject(value);
                }
                // (jToken as JObject)?.TryAdd(path[path.Length - 1], JToken.FromObject(value));
            }
            catch (Exception ex)
            {
                PrintError($"SetConfigValuePre ERROR: value: {value} path: {string.Join("\\", path)}\n{ex}");
            }
        }

        private bool GetConfigVersionPre(JObject config, out VersionNumber version)
        {
            try
            {
                JToken jToken;
                if (config.TryGetValue("Version", out jToken))
                {
                    version = jToken.ToObject<VersionNumber>();
                    return true;
                }
            }
            catch
            {
                // ignored
            }
            version = default(VersionNumber);
            return false;
        }

        #endregion Preprocess Old Config

        #endregion ConfigurationFile

        #region LanguageFile

        private void Print(BasePlayer player, string message)
        {
            Player.Message(player, message, _configData.chat.prefix, _configData.chat.steamIDIcon);
        }

        private void Print(ConsoleSystem.Arg arg, string message)
        {
            //SendReply(arg, message);
            var player = arg.Player();
            if (player == null)
            {
                Puts(message);
            }
            else
            {
                PrintToConsole(player, message);
            }
        }

        private string Lang(string key, string id = null, params object[] args)
        {
            try
            {
                return string.Format(lang.GetMessage(key, this, id), args);
            }
            catch (Exception)
            {
                PrintError($"Error in the language formatting of '{key}'. (userid: {id}. lang: {lang.GetLanguage(id)}. args: {string.Join(" ,", args)})");
                throw;
            }
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "You don't have '{0}' permission to use this command.",
                ["TargetDisabled"] = "{0}'s Remover Tool has been disabled.",
                ["TargetEnabled"] = "{0} is now using Remover Tool; Enabled for {1} seconds (Max Removable Objects: {2}, Remove Type: {3}).",
                ["ToolDisabled"] = "Remover Tool has been disabled.",
                ["ToolEnabled"] = "Remover Tool enabled for {0} seconds (Max Removable Objects: {1}, Remove Type: {2}).",
                ["Cooldown"] = "You need to wait {0} seconds before using Remover Tool again.",
                ["CurrentlyDisabled"] = "Remover Tool is currently disabled.",
                ["EntityLimit"] = "Entity limit reached, you have removed {0} entities, Remover Tool was automatically disabled.",
                ["MeleeToolNotHeld"] = "You need to be holding a {0} in order to use the Remover Tool",
                ["SpecificToolNotHeld"] = "You need to be holding a {0} in order to use the Remover Tool.",

                ["StartRemoveAll"] = "Start running RemoveAll, please wait.",
                ["StartRemoveStructure"] = "Start running RemoveStructure, please wait.",
                ["StartRemoveExternal"] = "Start running RemoveExternal, please wait.",
                ["AlreadyRemoveAll"] = "There is already a RemoveAll running, please wait.",
                ["AlreadyRemoveStructure"] = "There is already a RemoveStructure running, please wait.",
                ["AlreadyRemoveExternal"] = "There is already a RemoveExternal running, please wait.",
                ["CompletedRemoveAll"] = "You've successfully removed {0} entities using RemoveAll.",
                ["CompletedRemoveStructure"] = "You've successfully removed {0} entities using RemoveStructure.",
                ["CompletedRemoveExternal"] = "You've successfully removed {0} entities using RemoveExternal.",

                ["CanRemove"] = "You can remove this entity.",
                ["NotEnoughCost"] = "Can't remove: You don't have enough resources.",
                ["EntityDisabled"] = "Can't remove: Server has disabled the entity from being removed.",
                ["DamagedEntity"] = "Can't remove: Server has disabled damaged objects from being removed.",
                ["BeBlocked"] = "Can't remove: An external plugin blocked the usage.",
                ["InvalidEntity"] = "Can't remove: No valid entity targeted.",
                ["NotFoundOrFar"] = "Can't remove: The entity is not found or too far away.",
                ["StorageNotEmpty"] = "Can't remove: The entity storage is not empty.",
                ["RaidBlocked"] = "Can't remove: Raid blocked.",
                ["CombatBlocked"] = "Can't remove: Combat blocked.",
                ["NotRemoveAccess"] = "Can't remove: You don't have any rights to remove this.",
                ["NotStructure"] = "Can't remove: The entity is not a structure.",
                ["NotExternalWall"] = "Can't remove: The entity is not an external wall.",
                ["EntityTimeLimit"] = "Can't remove: The entity was built more than {0} seconds ago.",
                //["Can'tOpenAllLocks"] = "Can't remove: There is a lock in the building that you cannot open.",
                ["CantPay"] = "Can't remove: Paying system crashed! Contact an administrator with the time and date to help him understand what happened.",
                //["UsageOfRemove"] = "You have to hold a hammer in your hand and press the left mouse button.",

                ["Refund"] = "Refund:",
                ["Nothing"] = "Nothing",
                ["Price"] = "Price:",
                ["Free"] = "Free",
                ["TimeLeft"] = "Timeleft: {0}s\nRemoved: {1}",
                ["RemoverToolType"] = "Remover Tool ({0})",
                ["Unlimit"] = "∞",

                ["SyntaxError"] = "Syntax error, please type '<color=#ce422b>/{0} <help | h></color>' to view help",
                ["Syntax"] = "<color=#ce422b>/{0} [time (seconds)]</color> - Enable RemoverTool ({1})",
                ["Syntax1"] = "<color=#ce422b>/{0} <admin | a> [time (seconds)]</color> - Enable RemoverTool ({1})",
                ["Syntax2"] = "<color=#ce422b>/{0} <all> [time (seconds)]</color> - Enable RemoverTool ({1})",
                ["Syntax3"] = "<color=#ce422b>/{0} <structure | s> [time (seconds)]</color> - Enable RemoverTool ({1})",
                ["Syntax4"] = "<color=#ce422b>/{0} <external | e> [time (seconds)]</color> - Enable RemoverTool ({1})"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "您没有 '{0}' 权限来使用该命令",
                ["TargetDisabled"] = "'{0}' 的拆除工具已禁用",
                ["TargetEnabled"] = "'{0}' 的拆除工具已启用 {1} 秒 (可拆除数: {2}, 拆除模式: {3}).",
                ["ToolDisabled"] = "您的拆除工具已禁用",
                ["ToolEnabled"] = "您的拆除工具已启用 {0} 秒 (可拆除数: {1}, 拆除模式: {2}).",
                ["Cooldown"] = "您需要等待 {0} 秒才可以再次使用拆除工具",
                ["CurrentlyDisabled"] = "服务器当前已禁用了拆除工具",
                ["EntityLimit"] = "您已经拆除了 '{0}' 个实体，拆除工具已自动禁用",
                ["MeleeToolNotHeld"] = "您必须拿着{0}才可以使用拆除工具",
                ["SpecificToolNotHeld"] = "您必须拿着{0}才可以使用拆除工具",

                ["StartRemoveAll"] = "开始运行 '所有拆除'，请稍等片刻",
                ["StartRemoveStructure"] = "开始运行 '建筑拆除'，请稍等片刻",
                ["StartRemoveExternal"] = "开始运行 '外墙拆除'，请稍等片刻",
                ["AlreadyRemoveAll"] = "已经有一个 '所有拆除' 正在运行，请稍等片刻",
                ["AlreadyRemoveStructure"] = "已经有一个 '建筑拆除' 正在运行，请稍等片刻",
                ["AlreadyRemoveExternal"] = "已经有一个 '外墙拆除' 正在运行，请稍等片刻",
                ["CompletedRemoveAll"] = "您使用 '所有拆除' 成功拆除了 {0} 个实体",
                ["CompletedRemoveStructure"] = "您使用 '建筑拆除' 成功拆除了 {0} 个实体",
                ["CompletedRemoveExternal"] = "您使用 '外墙拆除' 成功拆除了 {0} 个实体",

                ["CanRemove"] = "您可以拆除该实体",
                ["NotEnoughCost"] = "无法拆除: 拆除所需资源不足",
                ["EntityDisabled"] = "无法拆除: 服务器已禁用拆除这种实体",
                ["DamagedEntity"] = "无法拆除: 服务器已禁用拆除已损坏的实体",
                ["BeBlocked"] = "无法拆除: 其他插件阻止您拆除该实体",
                ["InvalidEntity"] = "无法拆除: 无效的实体",
                ["NotFoundOrFar"] = "无法拆除: 没有找到实体或者距离太远",
                ["StorageNotEmpty"] = "无法拆除: 该实体内含有物品",
                ["RaidBlocked"] = "无法拆除: 拆除工具被突袭阻止了",
                ["CombatBlocked"] = "无法拆除: 拆除工具被战斗阻止了",
                ["NotRemoveAccess"] = "无法拆除: 您无权拆除该实体",
                ["NotStructure"] = "无法拆除: 该实体不是建筑物",
                ["NotExternalWall"] = "无法拆除: 该实体不是外高墙",
                ["EntityTimeLimit"] = "无法拆除: 该实体的存活时间大于 {0} 秒",
                //["Can'tOpenAllLocks"] = "无法拆除: 该建筑中有您无法打开的锁",
                ["CantPay"] = "无法拆除: 支付失败，请联系管理员，告诉他详情",

                ["Refund"] = "退还:",
                ["Nothing"] = "没有",
                ["Price"] = "价格:",
                ["Free"] = "免费",
                ["TimeLeft"] = "剩余时间: {0}s\n已拆除数: {1} ",
                ["RemoverToolType"] = "拆除工具 ({0})",
                ["Unlimit"] = "∞",

                ["SyntaxError"] = "语法错误，输入 '<color=#ce422b>/{0} <help | h></color>' 查看帮助",
                ["Syntax"] = "<color=#ce422b>/{0} [time (seconds)]</color> - 启用拆除工具 ({1})",
                ["Syntax1"] = "<color=#ce422b>/{0} <admin | a> [time (seconds)]</color> - 启用拆除工具 ({1})",
                ["Syntax2"] = "<color=#ce422b>/{0} <all> [time (seconds)]</color> - 启用拆除工具 ({1})",
                ["Syntax3"] = "<color=#ce422b>/{0} <structure | s> [time (seconds)]</color> - 启用拆除工具 ({1})",
                ["Syntax4"] = "<color=#ce422b>/{0} <external | e> [time (seconds)]</color> - 启用拆除工具 ({1})"
            }, this, "zh-CN");
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "У вас нет разрешения '{0}' чтобы использовать эту команду.",
                ["TargetDisabled"] = "{0}'s Remover Tool отключен.",
                ["TargetEnabled"] = "{0} теперь использует Remover Tool; Включено на {1} секунд (Макс. объектов для удаления: {2}, Тип удаления: {3}).",
                ["ToolDisabled"] = "Remover Tool отключен.",
                ["ToolEnabled"] = "Remover Tool включен на {0} секунд (Макс. объектов для удаления: {1}, Тип удаления: {2}).",
                ["Cooldown"] = "Необходимо подождать {0} секунд, прежде чем использовать Remover Tool снова.",
                ["CurrentlyDisabled"] = "Remover Tool в данный момент отключен.",
                ["EntityLimit"] = "Достигнут предел, удалено {0} объектов, Remover Tool автоматически отключен.",
                ["MeleeToolNotHeld"] = "Вы должны держать {0}, чтобы использовать инструмент для удаления.",
                ["SpecificToolNotHeld"] = "Вы должны держать {0}, чтобы использовать инструмент для удаления.",

                ["StartRemoveAll"] = "Запускается RemoveAll, пожалуйста, подождите.",
                ["StartRemoveStructure"] = "Запускается RemoveStructure, пожалуйста, подождите.",
                ["StartRemoveExternal"] = "Запускается RemoveExternal, пожалуйста, подождите.",
                ["AlreadyRemoveAll"] = "RemoveAll уже выполняется, пожалуйста, подождите.",
                ["AlreadyRemoveStructure"] = "RemoveStructure уже выполняется, пожалуйста, подождите.",
                ["AlreadyRemoveExternal"] = "RemoveExternal уже выполняется, пожалуйста, подождите.",
                ["CompletedRemoveAll"] = "Вы успешно удалили {0} объектов используя RemoveAll.",
                ["CompletedRemoveStructure"] = "Вы успешно удалили {0} объектов используя RemoveStructure.",
                ["CompletedRemoveExternal"] = "Вы успешно удалили {0} объектов используя RemoveExternal.",

                ["CanRemove"] = "Вы можете удалить этот объект.",
                ["NotEnoughCost"] = "Нельзя удалить: У вас не достаточно ресурсов.",
                ["EntityDisabled"] = "Нельзя удалить: Сервер отключил возможность удаления этого объекта.",
                ["DamagedEntity"] = "Нельзя удалить: Сервер отключил возможность удалять повреждённые объекты.",
                ["BeBlocked"] = "Нельзя удалить: Внешний plugin блокирует использование.",
                ["InvalidEntity"] = "Нельзя удалить: Неверный объект.",
                ["NotFoundOrFar"] = "Нельзя удалить: Объект не найден, либо слишком далеко.",
                ["StorageNotEmpty"] = "Нельзя удалить: Хранилище объекта не пусто.",
                ["RaidBlocked"] = "Нельзя удалить: Рейды остановки.",
                ["CombatBlocked"] = "Нельзя удалить: боевые остановки",
                ["NotRemoveAccess"] = "Нельзя удалить: У вас нет прав удалять это.",
                ["NotStructure"] = "Нельзя удалить: Объект не конструкция.",
                ["NotExternalWall"] = "Нельзя удалить: Объект не внешняя стена.",
                ["EntityTimeLimit"] = "Нельзя удалить: Объект был построен более {0} секунд назад.",
                //["Can'tOpenAllLocks"] = "Нельзя удалить: в здании есть замок, который вы не можете открыть",
                ["CantPay"] = "Нельзя удалить: Система оплаты дала сбой! Свяжитесь с админом указав дату и время, чтобы помочь ему понять что случилось.",

                ["Refund"] = "Возврат:",
                ["Nothing"] = "Ничего",
                ["Price"] = "Цена:",
                ["Free"] = "Бесплатно",
                ["TimeLeft"] = "Осталось времени: {0}s\nУдалено: {1}",
                ["RemoverToolType"] = "Remover Tool ({0})",
                ["Unlimit"] = "∞",

                ["SyntaxError"] = "Синтаксическая ошибка! Пожалуйста, введите '<color=#ce422b>/{0} <help | h></color>' для отображения помощи",
                ["Syntax"] = "<color=#ce422b>/{0} [время (секунд)]</color> - Включить RemoverTool ({1})",
                ["Syntax1"] = "<color=#ce422b>/{0} <admin | a> [время (секунд)]</color> - Включить RemoverTool ({1})",
                ["Syntax2"] = "<color=#ce422b>/{0} <all> [время (секунд)]</color> - Включить RemoverTool ({1})",
                ["Syntax3"] = "<color=#ce422b>/{0} <structure | s> [время (секунд)]</color> - Включить RemoverTool ({1})",
                ["Syntax4"] = "<color=#ce422b>/{0} <external | e> [время (секунд)]</color> - Включить RemoverTool ({1})"
            }, this, "ru");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "No tienes permiso '{0}' para usar este comando.",
                ["TargetDisabled"] = "La herramienta de eliminación de {0} ha sido desactivada.",
                ["TargetEnabled"] = "{0} ahora está usando la herramienta de eliminación; habilitada durante {1} segundos (Máximo de objetos removibles: {2}, Tipo de eliminación: {3}).",
                ["ToolDisabled"] = "La herramienta de eliminación ha sido desactivada.",
                ["ToolEnabled"] = "Herramienta de eliminación habilitada durante {0} segundos (Máximo de objetos removibles: {1}, Tipo de eliminación: {2}).",
                ["Cooldown"] = "Debes esperar {0} segundos antes de usar la herramienta de eliminación nuevamente.",
                ["CurrentlyDisabled"] = "La herramienta de eliminación está actualmente desactivada.",
                ["EntityLimit"] = "Límite de entidades alcanzado, has eliminado {0} entidades, la herramienta de eliminación se ha desactivado automáticamente.",
                ["MeleeToolNotHeld"] = "Debes estar sosteniendo un(a) {0} para usar la herramienta de eliminación",
                ["SpecificToolNotHeld"] = "Debes estar sosteniendo un(a) {0} para usar la herramienta de eliminación.",

                ["StartRemoveAll"] = "Iniciando eliminación total, por favor espera.",
                ["StartRemoveStructure"] = "Iniciando eliminación de estructura, por favor espera.",
                ["StartRemoveExternal"] = "Iniciando eliminación de externos, por favor espera.",
                ["AlreadyRemoveAll"] = "Ya hay una eliminación total en curso, por favor espera.",
                ["AlreadyRemoveStructure"] = "Ya hay una eliminación de estructura en curso, por favor espera.",
                ["AlreadyRemoveExternal"] = "Ya hay una eliminación de externos en curso, por favor espera.",
                ["CompletedRemoveAll"] = "Has eliminado exitosamente {0} entidades utilizando la eliminación total.",
                ["CompletedRemoveStructure"] = "Has eliminado exitosamente {0} entidades utilizando la eliminación de estructura.",
                ["CompletedRemoveExternal"] = "Has eliminado exitosamente {0} entidades utilizando la eliminación de externos.",

                ["CanRemove"] = "Puedes eliminar esta entidad.",
                ["NotEnoughCost"] = "No se puede eliminar: no tienes suficientes recursos.",
                ["EntityDisabled"] = "No se puede eliminar: el servidor ha desactivado la eliminación de esta entidad.",
                ["DamagedEntity"] = "No se puede eliminar: el servidor ha desactivado la eliminación de objetos dañados.",
                ["BeBlocked"] = "No se puede eliminar: un complemento externo bloqueó su uso.",
                ["InvalidEntity"] = "No se puede eliminar: no se ha seleccionado una entidad válida.",
                ["NotFoundOrFar"] = "No se puede eliminar: la entidad no se encuentra o está demasiado lejos.",
                ["StorageNotEmpty"] = "No se puede eliminar: el almacenamiento de la entidad no está vacío.",
                ["RaidBlocked"] = "No se puede eliminar: bloqueado durante un asalto.",
                ["CombatBlocked"] = "No se puede eliminar: bloqueado durante el combate.",
                ["NotRemoveAccess"] = "No se puede eliminar: no tienes permisos para eliminar esto.",
                ["NotStructure"] = "No se puede eliminar: la entidad no es una estructura.",
                ["NotExternalWall"] = "No se puede eliminar: la entidad no es una pared externa.",
                ["EntityTimeLimit"] = "No se puede eliminar: la entidad fue construida hace más de {0} segundos.",
                //["Can'tOpenAllLocks"] = "No se puede eliminar: hay una cerradura en el edificio que no puedes abrir.",
                ["CantPay"] = "No se puede eliminar: ¡el sistema de pago ha fallado! Contacta a un administrador con la fecha y hora para ayudarlo a entender lo que sucedió.",
                //["UsageOfRemove"] = "Debes sostener un martillo en tu mano y presionar el botón izquierdo del mouse.",

                ["Refund"] = "Reembolso:",
                ["Nothing"] = "Nada",
                ["Price"] = "Precio:",
                ["Free"] = "Gratis",
                ["TimeLeft"] = "Tiempo restante: {0}s\nEliminado: {1}",
                ["RemoverToolType"] = "Herramienta de eliminación ({0})",
                ["Unlimit"] = "∞",

                ["SyntaxError"] = "Error de sintaxis, por favor escribe '<color=#ce422b>/{0} <ayuda | h></color>' para ver la ayuda",
                ["Syntax"] = "<color=#ce422b>/{0} [tiempo (segundos)]</color> - Habilitar la herramienta de eliminación ({1})",
                ["Syntax1"] = "<color=#ce422b>/{0} <admin | a> [tiempo (segundos)]</color> - Habilitar la herramienta de eliminación ({1})",
                ["Syntax2"] = "<color=#ce422b>/{0} <todos> [tiempo (segundos)]</color> - Habilitar la herramienta de eliminación ({1})",
                ["Syntax3"] = "<color=#ce422b>/{0} <estructura | s> [tiempo (segundos)]</color> - Habilitar la herramienta de eliminación ({1})",
                ["Syntax4"] = "<color=#ce422b>/{0} <externo | e> [tiempo (segundos)]</color> - Habilitar la herramienta de eliminación ({1})"
            }, this, "es");
        }

        #endregion LanguageFile
    }
}