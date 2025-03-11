// #define DEBUG_POOLING
// #define DEBUG_GATHER

// #define FEATURE_BELT_ACTIVATION
// #define FEATURE_EXTRA_POCKETS_CONFIG

// #define CARBON

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;
using static BaseEntity;
using static PlayerInventory;
using Time = UnityEngine.Time;

namespace Oxide.Plugins
{
    [Info("Bag of Holding", "https://discord.gg/TrJ7jnS233", "1.8.3")]
    [Description("Adds portable bags for carrying loot.")]
    internal class BagOfHolding : CovalencePlugin
    {
        #region Fields

        [PluginReference]
        private Plugin Backpacks, Economics, ItemRetriever, Kits, Loottable, ServerRewards, SkillTree, StackModifier;

        private Configuration _config;

        private const string PermissionListBags = "bagofholding.listbags";
        private const string PermissionListContainers = "bagofholding.listcontainers";
        private const string PermissionGiveBag = "bagofholding.givebag";
        private const string PermissionGiveKitBag = "bagofholding.givekitbag";
        private const string PermissionConfig = "bagofholding.config";
        private const string PermissionStats = "bagofholding.stats";
        private const string PermissionManageLoot = "bagofholding.manageloot";

        private const int BagItemId = 479292118;
        private const int SaddleBagItemId = 1400460850;
        private const int ParachuteItemId = 602628465;
        private const int SmallBackpackItemId = 2068884361;
        private const int LargeBackpackItemId = -907422733;
        private const int VisualBackpackItemId = LargeBackpackItemId;
        private const string GenericResizableLootPanelName = "generic_resizable";
        private const Item.Flag FlagHasItems = Item.Flag.Cooking;
        private const Item.Flag FlagGather = Item.Flag.OnFire;
        private const Item.Flag FlagGatherExisting = (Item.Flag)(1 << 20);
        private const Item.Flag UnsearchableItemFlag = (Item.Flag)(1 << 24);
        private const ItemDefinition.Flag SearchableItemDefinitionFlag = (ItemDefinition.Flag)(1 << 24);

        private const string PrefabPlayerCorpse = "assets/prefabs/player/player_corpse.prefab";
        private const string PrefabStorageBox = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";

        private static readonly object True = true;
        private static readonly object False = false;
        private static readonly object CannotAccept = ItemContainer.CanAcceptResult.CannotAccept;

        private UIBuilder _containerUIBuilder = new(15000);

        private BaseEntity _sharedLootEntity;
        private LootableCorpse _sharedCorpseEntity;
        private ProtectionProperties _immortalProtection;

        private BackpacksAdapter _backpacksAdapter;
        private SkillTreeAdapter _skillTreeAdapter;
        private ItemRetrieverAdapter _itemRetrieverAdapter;
        private LimitManager _limitManager;
        private BagManager _bagManager;
        private UIUpdateManager _uiUpdateManager;
        private ItemModBagOfHolding _bagItemMod;
        private ItemModWearableBag _wearableBagItemMod;
        private ItemModContainerDummy _containerDummyMod;
        private ApiInstance _api;
        private PaymentProviderResolver _paymentProviderResolver;
        private CustomItemManager _customItemManager;
        private BeltUIRenderer _beltUIRenderer;

        #if FEATURE_BELT_ACTIVATION
        private BeltTracker _beltTracker;
        #endif

        private ItemDefinition _bagItemDefinition;
        private bool _preInitialized;
        private bool _initialized;
        private bool _subscribedToOnItemSplit;

        private Item[] _lockerItemBuffer = new Item[Locker.setSize];

        private string[] _delayedHooks;
        private string[] _conditionalHooks;

        private object[] _objectArray1 = new object[1];
        private object[] _objectArray2 = new object[2];

        private string[] _networkingHooks =
        {
            nameof(OnEntitySaved),
            nameof(OnInventoryNetworkUpdate),
        };

        private readonly ProtoBuf.Item _visualBackpackItemData = new()
        {
            ShouldPool = false,
            itemid = VisualBackpackItemId,
            amount = 1,
        };

        public BagOfHolding()
        {
            _backpacksAdapter = new BackpacksAdapter(this);
            _skillTreeAdapter = new SkillTreeAdapter(this);
            _itemRetrieverAdapter = new ItemRetrieverAdapter(this);

            _uiUpdateManager = new UIUpdateManager(this);
            _paymentProviderResolver = new PaymentProviderResolver(this);
            _beltUIRenderer = new BeltUIRenderer(this);

            #if FEATURE_BELT_ACTIVATION
            _beltTracker = new BeltTracker(this);
            #endif

            _delayedHooks = new[]
            {
                nameof(OnPlayerLootEnd),
                nameof(OnItemAction),
                nameof(CanAcceptItem),
                nameof(CanMoveItem),
                nameof(OnLockerSwap),
            };

            _conditionalHooks = new[]
            {
                #if FEATURE_BELT_ACTIVATION
                nameof(OnActiveItemChanged),
                #endif

                nameof(CanWearItem),
                nameof(CanLockerAcceptItem),

                nameof(OnPlayerSleepEnded),

                nameof(CanStackItem),
                nameof(CanCombineDroppedItem),
                nameof(OnDroppedItemCombined),
                nameof(OnItemSplit),

                nameof(OnItemPickup),

                nameof(CanBeRecycled),
                nameof(CanRecycle),
                nameof(OnItemRecycle),

                nameof(OnMaxStackable)
            }.Concat(_networkingHooks).ToArray();
        }

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(PermissionListBags, this);
            permission.RegisterPermission(PermissionListContainers, this);
            permission.RegisterPermission(PermissionGiveBag, this);
            permission.RegisterPermission(PermissionGiveKitBag, this);
            permission.RegisterPermission(PermissionConfig, this);
            permission.RegisterPermission(PermissionStats, this);
            permission.RegisterPermission(PermissionManageLoot, this);

            CustomPool.Reset<BagInfo>(512);
            CustomPool.Reset<List<BagInfo>>(512);
            CustomPool.Reset<DisposableList<BagInfo>>(8);
            CustomPool.Reset<ContainerSupervisor>(512);
            CustomPool.Reset<List<Item>>(8);
            CustomPool.Reset<ItemContainer>(8);

            _limitManager = new LimitManager(this, _config);
            _bagManager = new BagManager(this, _config, _limitManager);
            _api = new ApiInstance(this);
            _customItemManager = new CustomItemManager(_config);

            _config.Init(this);

            foreach (var hookName in _delayedHooks.Concat(_conditionalHooks))
            {
                Unsubscribe(hookName);
            }

            if (!_config.LootConfig.Enabled)
            {
                // OnLootSpawn is intentionally not included in the list of hooks that are always subscribed,
                // so that it is subscribed during server boot.
                Unsubscribe(nameof(OnLootSpawn));
            }
        }

        private void OnServerInitialize()
        {
            _preInitialized = true;
            _config.OnServerInitialize();
        }

        private void OnServerInitialized()
        {
            if (!_preInitialized)
            {
                OnServerInitialize();
            }

            _config.OnServerInitialized(this);

            _bagItemDefinition = ItemManager.FindItemDefinition(BagItemId);
            if (_bagItemDefinition == null)
            {
                LogError($"Unable to initialize plugin because item {BagItemId} was not found.");
                return;
            }

            _bagItemDefinition.flags |= SearchableItemDefinitionFlag;

            var oldBagItemMod = _bagItemDefinition.itemMods.FirstOrDefault(itemMod => itemMod.GetType().Name == nameof(ItemModBagOfHolding));
            if (oldBagItemMod != null)
            {
                oldBagItemMod.GetType().GetMethod(nameof(ItemModBagOfHolding.Unload))?.Invoke(oldBagItemMod, null);
            }

            _immortalProtection = ScriptableObject.CreateInstance<ProtectionProperties>();
            _immortalProtection.name = "BagOfHoldingProtection";
            _immortalProtection.Add(1);

            _sharedLootEntity = CreateLootEntity(_immortalProtection);
            if (_sharedLootEntity == null || _sharedLootEntity.IsDestroyed)
            {
                LogError($"Failed to create the shared lootable entity. This plugin will not work correctly. There is likely a plugin conflict.");
                return;
            }

            _sharedCorpseEntity = CreateCorpseEntity(_immortalProtection);
            if (_sharedCorpseEntity == null || _sharedCorpseEntity.IsDestroyed)
            {
                LogError($"Failed to create the shared lootable corpse entity. This plugin will not work correctly. There is likely a plugin conflict.");
                return;
            }

            if (_config.Wearable.Enabled)
            {
                _wearableBagItemMod = ItemModWearableBag.AddToItemDefinition(_bagItemDefinition);
            }

            _bagItemMod = ItemModBagOfHolding.AddToItemDefinition(_bagItemDefinition, this);
            _containerDummyMod = ItemModContainerDummy.AddToItemDefinition(_bagItemDefinition);

            // Setup container dependencies before discovering items. Most important for backpacks.
            _skillTreeAdapter.HandleLoadedChanged();
            _backpacksAdapter.HandleLoadedChanged();
            _itemRetrieverAdapter.HandleLoadedChanged();

            // Enable networking hooks before sending network updates.
            if (_config.VisualBackpack.Enabled)
            {
                foreach (var hookName in _networkingHooks)
                {
                    Subscribe(hookName);
                }
            }

            // Best effort attempt to update all bags.
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var ridableAnimal = entity as BaseRidableAnimal;
                if ((object)ridableAnimal != null)
                {
                    DiscoverBags(ridableAnimal.storageInventory);
                    DiscoverBags(ridableAnimal.equipmentInventory);
                    continue;
                }

                if (entity is IItemContainerEntity containerEntity)
                {
                    DiscoverBags(containerEntity.inventory);

                    var shopFront = containerEntity as ShopFront;
                    if ((object)shopFront != null)
                    {
                        DiscoverBags(shopFront.customerInventory);
                    }

                    continue;
                }

                var droppedItemContainer = entity as DroppedItemContainer;
                if ((object)droppedItemContainer != null)
                {
                    DiscoverBags(droppedItemContainer.inventory);
                    continue;
                }

                var worldItem = entity as WorldItem;
                if ((object)worldItem != null)
                {
                    DiscoverBags(worldItem.item);

                    var droppedItem = entity as DroppedItem;
                    if ((object)droppedItem != null && droppedItem.item != null && IsSkinnedBag(droppedItem.item))
                    {
                        // Reset removal time. Most important for server restarts.
                        _bagItemMod.HandleBagMovedToWorld(droppedItem.item, delayRemovalTimeReset: true);
                    }
                    continue;
                }

                var lootableCorpse = entity as LootableCorpse;
                if ((object)lootableCorpse != null)
                {
                    DiscoverBags(lootableCorpse.containers);
                    continue;
                }

                var basePlayer = entity as BasePlayer;
                if (basePlayer != null)
                {
                    var inventory = basePlayer.inventory;
                    if (inventory == null)
                        continue;

                    DiscoverBags(basePlayer.inventory.containerMain);
                    DiscoverBags(basePlayer.inventory.containerBelt);
                    DiscoverBags(basePlayer.inventory.containerWear);

                    if (_config.VisualBackpack.Enabled && ShouldDisplayVisualBackpack(basePlayer))
                    {
                        inventory.SendUpdatedInventory(PlayerInventory.Type.Wear, inventory.containerWear, bSendInventoryToEveryone: true);
                    }

                    continue;
                }
            }

            foreach (var hookName in _delayedHooks)
            {
                Subscribe(hookName);
            }

            if (_config.Wearable.Enabled)
            {
                Subscribe(nameof(CanWearItem));
                Subscribe(nameof(CanLockerAcceptItem));
            }

            if (_config.DroppedBagSettings.EnableOpeningDroppedBags
                || (_config.Wearable.Enabled && _config.DroppedBagSettings.AutoEquipDroppedBags))
            {
                Subscribe(nameof(OnItemPickup));
            }

            if (_config.ContainsRecyclableBags)
            {
                Subscribe(nameof(CanBeRecycled));
                Subscribe(nameof(CanRecycle));
                Subscribe(nameof(OnItemRecycle));
            }

            #if FEATURE_BELT_ACTIVATION
            if (_config.BeltActivation.Enabled)
            {
                Subscribe(nameof(OnActiveItemChanged));
            }
            #endif

            if (_config.UISettings.BeltButton.Enabled)
            {
                Subscribe(nameof(OnPlayerSleepEnded));
            }

            if (_config.ContainsStackProfileItemOverrides)
            {
                Subscribe(nameof(OnMaxStackable));
            }

            RegisterCustomItemsWithLootTable();

            _initialized = true;

            // Give stack plugins one frame to update stack sizes.
            NextTick(() =>
            {
                // Always subscribe to CanStackItem, since StackModifier only handles it in player inventories.
                Subscribe(nameof(CanStackItem));

                RefreshStackHooks();

                if (_bagItemDefinition.stackable > 1)
                {
                    TestOnItemSplit();
                }
            });
        }

        private void Unload()
        {
            foreach (var hookName in _networkingHooks)
            {
                Unsubscribe(hookName);
            }

            if (_config.VisualBackpack.Enabled || _config.Wearable.Enabled)
            {
                foreach (var entity in BaseNetworkable.serverEntities)
                {
                    if (_config.VisualBackpack.Enabled)
                    {
                        var basePlayer = entity as BasePlayer;
                        if ((object)basePlayer != null)
                        {
                            var inventory = basePlayer.inventory;
                            if (inventory == null)
                                continue;

                            if (ShouldDisplayVisualBackpack(basePlayer))
                            {
                                inventory.SendUpdatedInventory(PlayerInventory.Type.Wear, inventory.containerWear, bSendInventoryToEveryone: true);
                            }

                            continue;
                        }
                    }
                }
            }

            _wearableBagItemMod?.Destroy();
            _containerDummyMod.Destroy();
            _customItemManager.Unload();
            _bagManager.Unload();
            _limitManager.Unload();

            CustomPool.Reset<BagInfo>(0);
            CustomPool.Reset<List<BagInfo>>(0);
            CustomPool.Reset<DisposableList<BagInfo>>(0);
            CustomPool.Reset<ContainerSupervisor>(0);
            CustomPool.Reset<List<Item>>(0);
            CustomPool.Reset<ItemContainer>(0);
        }

        private void OnPluginLoaded(Plugin plugin)
        {
            if (!_initialized)
                return;

            switch (plugin.Name)
            {
                case nameof(Backpacks):
                    _backpacksAdapter.HandleLoadedChanged();
                    return;
                case nameof(ItemRetriever):
                    _itemRetrieverAdapter.HandleLoadedChanged();
                    return;
                case nameof(Loottable):
                    RefreshStackHooks();
                    RegisterCustomItemsWithLootTable();
                    return;
                case nameof(SkillTree):
                    _skillTreeAdapter.HandleLoadedChanged();
                    return;
                case nameof(StackModifier):
                    RefreshStackHooks();
                    return;
            }
        }

        private void OnPluginUnloaded(Plugin plugin)
        {
            if (!_initialized)
                return;

            switch (plugin.Name)
            {
                case nameof(Backpacks):
                    _backpacksAdapter.HandleLoadedChanged();
                    return;
                case nameof(ItemRetriever):
                    _itemRetrieverAdapter.HandleLoadedChanged();
                    return;
                case nameof(Loottable):
                    RefreshStackHooks();
                    return;
                case nameof(SkillTree):
                    _skillTreeAdapter.HandleLoadedChanged();
                    return;
                case nameof(StackModifier):
                    RefreshStackHooks();
                    return;
            }
        }

        // Subscribed only while wearable bags are enabled.
        private object CanWearItem(PlayerInventory playerInventory, Item item, int targetSlot)
        {
            if (item.info.itemid != BagItemId)
                return null;

            // Disallow wearing vanilla loot bags, necessary because of the ItemModWearable component.
            if (!_bagManager.IsBag(item))
                return False;

            if (targetSlot == Locker.backpackSlotIndex)
            {
                if (_config.Wearable.AllowInBackpackSlot)
                    return True;
            }
            else if (_config.Wearable.AllowInNonBackpackSlot)
            {
                return True;
            }

            // Must return false instead of null, due to the existence of the ItemModWearable component.
            return False;
        }

        // Subscribed only while wearable bags are enabled.
        private object CanLockerAcceptItem(Locker locker, Item item, int targetSlot)
        {
            if (!_bagManager.IsBag(item))
                return null;

            if (locker.IsBackpackSlot(targetSlot))
                return _config.Wearable.AllowInBackpackSlot ? True : null;

            if (locker.GetRowType(targetSlot) == Locker.RowType.Clothing)
                return _config.Wearable.AllowInNonBackpackSlot ? True : null;

            return null;
        }

        // Always subscribed to avoid bag limit issues when switching non-empty bags.
        // Also subscribed to allow items to be swapped in clothing slots.
        private object OnLockerSwap(Locker locker, int outfitIndex, BasePlayer player)
        {
            if (locker.IsEquipping())
                return null;

            var startIndex = outfitIndex * 14;
            var wantsToTransferBag = false;

            if (_config.Wearable.Enabled)
            {
                // Override locker swap if any bag is being worn.
                for (var i = 0; i < Locker.attireSize; i++)
                {
                    var lockerItem = locker.inventory.GetSlot(startIndex + i);
                    if (lockerItem != null && _bagManager.IsBag(lockerItem))
                    {
                        wantsToTransferBag = true;
                        break;
                    }

                    var playerItem = player.inventory.containerWear.GetSlot(i);
                    if (playerItem != null && _bagManager.IsBag(playerItem))
                    {
                        wantsToTransferBag = true;
                        break;
                    }
                }
            }

            if (!wantsToTransferBag)
            {
                // Override locker swap if any non-empty bag is being transferred.
                for (var i = 0; i < Locker.beltSize; i++)
                {
                    var lockerItem = locker.inventory.GetSlot(startIndex + i);
                    if (lockerItem != null && _bagManager.IsNonEmptyBag(lockerItem))
                    {
                        wantsToTransferBag = true;
                        break;
                    }

                    var playerItem = player.inventory.containerBelt.GetSlot(i);
                    if (playerItem != null && _bagManager.IsNonEmptyBag(playerItem))
                    {
                        wantsToTransferBag = true;
                        break;
                    }
                }
            }

            if (!wantsToTransferBag)
                return null;

            var didSwapAnything = false;

            // Move attire and belt items to limbo to avoid bag limit issues.
            for (var i = 0; i < Locker.attireSize; i++)
            {
                var playerItem = player.inventory.containerWear.GetSlot(i);
                if (playerItem == null)
                    continue;

                playerItem.RemoveFromContainer();
                _lockerItemBuffer[i] = playerItem;
            }

            for (var i = 0; i < Locker.beltSize; i++)
            {
                var playerItem = player.inventory.containerBelt.GetSlot(i);
                if (playerItem == null)
                    continue;

                playerItem.RemoveFromContainer();
                _lockerItemBuffer[i + Locker.attireSize] = playerItem;
            }

            // Move items from locker to player inventory.
            for (var i = 0; i < Locker.attireSize; i++)
            {
                var lockerItemIndex = startIndex + i;
                var lockerItem = locker.inventory.GetSlot(lockerItemIndex);
                if (lockerItem == null)
                    continue;

                didSwapAnything = true;
                if (lockerItem.info.category != ItemCategory.Attire && !_bagManager.IsBag(lockerItem) || !lockerItem.MoveToContainer(player.inventory.containerWear, i))
                {
                    lockerItem.Drop(locker.GetDropPosition(), locker.GetDropVelocity());
                }
            }

            for (var i = 0; i < Locker.beltSize; i++)
            {
                var lockerItemIndex = startIndex + Locker.attireSize + i;
                var lockerItem = locker.inventory.GetSlot(lockerItemIndex);
                if (lockerItem == null)
                    continue;

                didSwapAnything = true;
                if (!lockerItem.MoveToContainer(player.inventory.containerBelt, i))
                {
                    lockerItem.Drop(locker.GetDropPosition(), locker.GetDropVelocity());
                }
            }

            // Move items from limbo to locker.
            for (var i = 0; i < Locker.attireSize; i++)
            {
                var lockerItemIndex = startIndex + i;
                var playerItem = _lockerItemBuffer[i];
                if (playerItem == null)
                    continue;

                didSwapAnything = true;
                if (playerItem.info.category != ItemCategory.Attire && !_bagManager.IsBag(playerItem) || !playerItem.MoveToContainer(locker.inventory, lockerItemIndex))
                {
                    playerItem.Drop(locker.GetDropPosition(), locker.GetDropVelocity());
                }

                _lockerItemBuffer[i] = null;
            }

            for (var i = 0; i < Locker.beltSize; i++)
            {
                var bufferIndex = Locker.attireSize + i;
                var lockerItem = _lockerItemBuffer[bufferIndex];
                if (lockerItem == null)
                    continue;

                var lockerItemIndex = startIndex + Locker.attireSize + i;

                didSwapAnything = true;
                if (!lockerItem.MoveToContainer(locker.inventory, lockerItemIndex))
                {
                    lockerItem.Drop(locker.GetDropPosition(), locker.GetDropVelocity());
                }

                _lockerItemBuffer[bufferIndex] = null;
            }

            if (didSwapAnything)
            {
                Effect.server.Run(locker.equipSound.resourcePath, player, StringPool.Get("spine3"), Vector3.zero, Vector3.zero);
                locker.SetFlag(Locker.LockerFlags.IsEquipping, true);
                locker.Invoke(locker.ClearEquipping, 1.5f);
            }

            return False;
        }

        // Subscribed only while the visual backpack is enabled.
        private void OnEntitySaved(BasePlayer player, BaseNetworkable.SaveInfo saveInfo)
        {
            if (!ShouldDisplayVisualBackpack(player))
                return;

            AddVisualBackpackItemToContainer(saveInfo.msg.basePlayer.inventory.invWear);
        }

        // Subscribed only while the visual backpack is enabled.
        private void OnInventoryNetworkUpdate(PlayerInventory playerInventory, ItemContainer container,
            ProtoBuf.UpdateItemContainer payload, PlayerInventory.Type inventoryType)
        {
            if (inventoryType != PlayerInventory.Type.Wear
                || !ShouldDisplayVisualBackpack(playerInventory.baseEntity))
                return;

            AddVisualBackpackItemToContainer(payload.container[0]);
        }

        // Possible improvement: Dynamically subscribe only while a player is looting a bag.
        // Possible improvement: Attach components with `PlayerStoppedLooting(BasePlayer)` on-demand and keep them until plugin unload.
        private void OnPlayerLootEnd(PlayerLoot playerLoot)
        {
            var player = playerLoot.baseEntity;
            if (player == null)
                return;

            if (!IsLootingBag(player, out var bagInfo))
                return;

            _bagManager.HandlePlayerStoppedLooting(player, bagInfo);
        }

        #if FEATURE_BELT_ACTIVATION
        // Subscribed only while belt activation is enabled.
        private void OnActiveItemChanged(BasePlayer player, Item previousItem, Item nextItem)
        {
            if (!IsRealPlayer(player))
                return;

            if (nextItem != null)
            {
                var nextBagInfo = _bagManager.EnsureBagInfoIfEligible(nextItem);
                if (nextBagInfo != null)
                {
                    if (nextItem == previousItem)
                    {
                        StopLooting(player);
                    }
                    else
                    {
                        if (player.inventory.loot.IsLooting())
                        {
                            AttemptOpenBag(player, nextBagInfo);
                        }
                        else
                        {
                            // Player might be scrolling through their items, so don't open immediately.
                            _beltTracker.AddPlayer(player, nextBagInfo);
                        }

                        // var originalPosition = nextItem.position;

                        // timer.Once(0.1f, () =>
                        // {
                        //     var activeItem = player.GetActiveItem();
                        //     if (nextItem == activeItem && activeItem.position == originalPosition)
                        //     {
                        //         AttemptOpenBag(player, nextBagInfo);
                        //     }
                        // });
                    }
                }
                return;
            }

            if (previousItem == null)
                return;

            var previousBagInfo = _bagManager.GetBagInfo(previousItem);
            if (previousBagInfo == null)
                return;

            _beltTracker.RemovePlayer(player);

            var lootContainer = player.inventory.loot.containers.FirstOrDefault();
            if (lootContainer == null || lootContainer != previousItem.contents)
                return;

            // The following code must be in a block to ensure the delegate doesn't get created for code paths that don't reach this.
            {
                var originalContainer = previousItem.parent;
                var originalPosition = previousItem.position;
                var originalAmount = previousItem.amount;

                NextTick(() =>
                {
                    if (originalContainer != previousItem.parent
                        || originalPosition != previousItem.position
                        || originalAmount != previousItem.amount)
                        return;

                    ClosePlayerInventory(player);
                });
            }
        }
        #endif

        // Subscribed only while the belt button is enabled.
        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (ShouldDisplayBeltUI(player))
            {
                _beltUIRenderer.CreateUIIfMissing(player);
            }
        }

        // Always subscribed due to vanilla issues that need to be mitigated, most importantly for dropping locked bags.
        private object OnItemAction(Item item, string action, BasePlayer player)
        {
            switch (action)
            {
                // Unlock the bag before it's dropped since otherwise vanilla won't allow the action.
                case "drop":
                {
                    if (!item.IsLocked())
                        return null;

                    var bagInfo = _bagManager.GetBagInfo(item);
                    if (bagInfo == null)
                        return null;

                    // Set the item unlocked so it can be dropped.
                    bagInfo.SetLocked(false);

                    if (bagInfo.Item.amount > 1)
                    {
                        var parentSupervisor = bagInfo.ParentSupervisor;
                        if (parentSupervisor != null)
                        {
                            NextTick(() => parentSupervisor.RefreshBags());
                        }
                    }

                    return null;
                }

                // Fix issue where wrapping a gift can delete the contents.
                case "wrap":
                {
                    if (item.amount != 1)
                        return null;

                    if (!ItemUtils.HasItemMod(item.info, out ItemModWrap itemModWrap))
                        return null;

                    if (!IsInsideBag(item, out var parentContainer))
                        return null;

                    if (!CanPlayerMoveItem(player, item))
                        return null;

                    var childItem = item.contents.GetSlot(0);
                    if (childItem == null)
                        return null;

                    var position = item.position;
                    item.RemoveFromContainer();

                    var wrappedItem = ItemManager.Create(itemModWrap.wrappedDefinition);
                    if (!childItem.MoveToContainer(wrappedItem.contents))
                    {
                        wrappedItem.Remove();
                        player.GiveItem(childItem);
                        return null;
                    }

                    if (!wrappedItem.MoveToContainer(parentContainer, position))
                    {
                        player.GiveItem(wrappedItem);
                    }

                    item.Remove();
                    if (itemModWrap.successEffect.isValid)
                    {
                        Effect.server.Run(itemModWrap.successEffect.resourcePath, player.eyes.position);
                    }

                    return False;
                }

                // Fix issue where unwrapping a gift can delete the contents.
                case "open":
                {
                    if (item.amount != 1)
                        return null;

                    if (!ItemUtils.HasItemMod(item.info, out ItemModOpenWrapped itemModOpenWrapped))
                        return null;

                    if (!IsInsideBag(item, out var parentContainer))
                        return null;

                    if (!CanPlayerMoveItem(player, item))
                        return null;

                    var childItem = item.contents.GetSlot(0);
                    if (childItem == null)
                        return null;

                    var position = item.position;
                    item.RemoveFromContainer();

                    if (!childItem.MoveToContainer(parentContainer, position))
                    {
                        player.GiveItem(childItem);
                    }

                    item.Remove();
                    if (itemModOpenWrapped.successEffect.isValid)
                    {
                        Effect.server.Run(itemModOpenWrapped.successEffect.resourcePath, player.eyes.position);
                    }

                    return False;
                }

                default:
                    return null;
            }
        }

        // Always subscribed to prevent containers from accepting bags when bag limit is reached.
        private object CanAcceptItem(ItemContainer container, Item item, int targetPosition)
        {
            // Allow bags to move around in the same inventory.
            if (item.parent == container)
                return null;

            if (!EnforceBagLimitsInContainer(container))
                return null;

            var bagInfo = _bagManager.EnsureBagInfoIfEligible(item);
            if (bagInfo == null || !bagInfo.HasItems)
                return null;

            var supervisor = _limitManager.GetSupervisor(container);
            if (supervisor != null)
            {
                return supervisor.CanAddBag(bagInfo, container)
                    ? null
                    : CannotAccept;
            }

            // If there is no supervisor, assume the container does not have any bags yet.
            var limitProfile = _limitManager.GetLimitProfile(container);
            if (limitProfile.MaxTotalBags == 0
                || limitProfile.GetMaxBagsOfCategory(bagInfo.BagProfile.CategoryNumber) == 0)
                return CannotAccept;

            return null;
        }

        // Subscribed only while multiple bag profiles share a skin ID, and while no suitable stack plugin is loaded.
        private Item OnItemSplit(Item sourceItem, int amount)
        {
            var bagInfo = _bagManager.EnsureBagInfoIfEligible(sourceItem);
            if (bagInfo == null)
                return null;

            sourceItem.amount -= amount;
            sourceItem.MarkDirty();
            return CreateBag(bagInfo.BagProfile, amount);
        }

        // Subscribed only while stack protection is enabled.
        private object CanStackItem(Item hostItem, Item movedItem)
        {
            if (hostItem.info.stackable <= 1)
                return null;

            var bagInfo = _bagManager.EnsureBagInfoIfEligible(hostItem);
            if (bagInfo == null)
                return null;

            var otherBagInfo = _bagManager.EnsureBagInfoIfEligible(movedItem);
            if (otherBagInfo == null)
                return null;

            if (bagInfo.BagProfile != otherBagInfo.BagProfile)
                return False;

            if (ItemUtils.HasChildren(movedItem))
                return False;

            return null;
        }

        // Subscribed only while no suitable stack plugin is loaded.
        private object CanCombineDroppedItem(DroppedItem itemEntity, DroppedItem otherItemEntity)
        {
            var bagInfo = _bagManager.EnsureBagInfoIfEligible(itemEntity.item);
            if (bagInfo == null)
                return null;

            var otherBagInfo = _bagManager.EnsureBagInfoIfEligible(otherItemEntity.item);
            if (otherBagInfo == null)
                return null;

            if (bagInfo.BagProfile != otherBagInfo.BagProfile)
                return False;

            if (ItemUtils.HasChildren(itemEntity.item) || ItemUtils.HasChildren(otherItemEntity.item))
                return False;

            return null;
        }

        // Always subscribed to mitigate an issue where quick-looting an item from a bag in your inventory would move it back into the bag.
        // Possible improvement: Dynamically subscribe only while a player is looting a bag in their inventory, and while stack improvements is enabled.
        private object CanMoveItem(Item item, PlayerInventory playerInventory, ItemContainerId targetContainerId, int targetSlot, int amount)
        {
            if (targetContainerId.Value == 0
                && targetSlot == -1
                && IsInsideBag(item, out var parentContainer)
                && parentContainer.playerOwner == playerInventory.baseEntity)
            {
                // Player is quick moving an item from a bag in their inventory into their inventory.
                // This needs to be overriden since vanilla logic would move the item back into the bag.
                if (!playerInventory.GiveItem(item))
                {
                    playerInventory.baseEntity.ChatMessage("GiveItem failed!");
                }

                return False;
            }

            // When attempting to move a bag into another bag, try to stack it if the destination bag is closed.
            if (_config.EnableStackImprovements)
            {
                var bagInfo = _bagManager.GetBagInfo(item);
                if (bagInfo == null)
                    return null;

                // If the bag contains items, only allow moving a partial stack from it onto another bag.
                if (bagInfo.HasItems && (item.amount == 1 || amount >= item.amount))
                    return null;

                // Target container must have a parent item (must be a bag).
                var targetContainer = playerInventory.FindContainer(targetContainerId);
                if (targetContainer?.parent == null)
                    return null;

                // Target container must be a bag of the same profile as the bag being moved.
                var destinationBagInfo = _bagManager.GetBagInfo(targetContainer.parent);
                if (destinationBagInfo == null || destinationBagInfo.BagProfile != bagInfo.BagProfile)
                    return null;

                // Don't stack if looting the destination bag (move the bag into it instead).
                if (playerInventory.loot.containers.FirstOrDefault() == targetContainer)
                    return null;

                // Don't allow stacking onto a bag item that doesn't accept items (e.g., that bag is in a loot container).
                if (targetContainer.PlayerItemInputBlocked())
                    return null;

                // Vanilla checks to prohibit taking items in various situations (e.g., from a locked container).
                if (item.parent.entityOwner is ICanMoveFrom canMoveFrom
                    && !canMoveFrom.CanMoveFrom(playerInventory.baseEntity, item))
                    return null;

                // If amount is negative, the player is trying to move the whole stack.
                if (amount <= 0)
                {
                    amount = item.amount;
                }

                amount = Mathf.Clamp(amount, 1, destinationBagInfo.Item.MaxStackable() - destinationBagInfo.Item.amount);
                if (amount <= 0)
                    return null;

                destinationBagInfo.Item.amount += amount;
                destinationBagInfo.Item.MarkDirty();

                item.amount -= amount;
                if (item.amount > 0)
                {
                    item.MarkDirty();
                }
                else
                {
                    item.RemoveFromContainer();
                    item.Remove();
                }

                return False;
            }

            return null;
        }

        // Subscribed only while looting or equipping dropped bags is enabled.
        private object OnItemPickup(Item item, BasePlayer player)
        {
            var bagInfo = _bagManager.EnsureBagInfoIfEligible(item);
            if (bagInfo == null)
                return null;

            if (CanAcceptItem(player.inventory.containerMain, item, -1) == null)
                return null;

            if (_config.Wearable.Enabled
                && _config.WearableBagLimits.Enabled
                && _config.DroppedBagSettings.AutoEquipDroppedBags
                && item.MoveToContainer(player.inventory.containerWear))
                return False;

            if (!_config.DroppedBagSettings.EnableOpeningDroppedBags)
                return null;

            var droppedItem = item.GetWorldEntity() as DroppedItem;
            if (droppedItem == null || droppedItem.IsDestroyed)
                return null;

            // Reset the removal time to reduce the likelihood of the bag item being removed while it's being looted.
            ResetRemovalTime(droppedItem);
            AttemptOpenBag(player, bagInfo);
            return False;
        }

        // Subscribed only while loot spawns are enabled.
        private void OnLootSpawn(LootContainer lootContainer)
        {
            if (lootContainer.OwnerID != 0 || lootContainer.skinID != 0)
                return;

            if (!_config.LootConfig.SpawnChanceByPrefabId.TryGetValue(lootContainer.prefabID, out var spawnChanceByProfile))
                return;

            ScheduleLootSpawn(lootContainer, spawnChanceByProfile);
        }

        // Subscribed only while a bag profile has recycling enabled.
        private object CanBeRecycled(Item item, Recycler recycler)
        {
            if (item == null)
                return null;

            var bagInfo = _bagManager.GetBagInfo(item);
            if (bagInfo == null)
                return null;

            return ObjectCache.Get(bagInfo.BagProfile.CanBeRecycled && !bagInfo.HasItems);
        }

        // Subscribed only while a bag profile has recycling enabled.
        private object CanRecycle(Recycler recycler, Item item)
        {
            return CanBeRecycled(item, recycler);
        }

        // Subscribed only while a bag profile has recycling enabled.
        private object OnItemRecycle(Item item, Recycler recycler)
        {
            var recycleInfo = _bagManager.GetBagInfo(item)?.BagProfile.RecycleInfo;
            if (recycleInfo == null || !recycleInfo.Enabled)
                return null;

            if (recycleInfo.Ingredients == null)
                return null;

            var recycleAmount = Mathf.CeilToInt(Mathf.Min(item.amount, item.info.stackable * 0.1f));

            item.UseItem(recycleAmount);

            foreach (var ingredientInfo in recycleInfo.Ingredients)
            {
                if (ingredientInfo.Amount <= 0 || ingredientInfo.ItemDefinition == null)
                    continue;

                var ingredientItem = ItemManager.Create(ingredientInfo.ItemDefinition, ingredientInfo.Amount * recycleAmount, ingredientInfo.SkinId);
                if (ingredientItem == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(ingredientInfo.DisplayName))
                {
                    ingredientItem.name = ingredientInfo.DisplayName;
                }

                recycler.MoveItemToOutput(ingredientItem);
            }

            return False;
        }

        // Subscribed only while no suitable stack plugin is loaded.
        private void OnDroppedItemCombined(DroppedItem droppedItem)
        {
            var item = droppedItem.item;
            if (item == null)
                return;

            var bagInfo = _bagManager.GetBagInfo(item);
            if (bagInfo == null)
                return;

            // Delay in case another plugin (such as DespawnConfig) is generically resetting despawn time.
            ResetRemovalTime(droppedItem, delayed: true);
        }

        // Only subscribed while at least one bag profile exists that uses a Stack profile that has item-specific or skin-specific overrides.
        private object OnMaxStackable(Item item)
        {
            var stackSizeOverride = GetParentBag(item)?.BagProfile?.StackProfile?.GetStackSizeOverride(item);
            if (stackSizeOverride == null)
                return null;

            var stackSizeInt = (int)stackSizeOverride;
            if (stackSizeInt >= item.info.stackable)
            {
                // Don't allow increasing max stack size, since that creates some complications.
                return null;
            }

            if (item.parent != null && item.parent.maxStackSize != 0 && stackSizeInt >= item.parent.maxStackSize)
            {
                // Don't allow increasing max stack size, since that creates some complications.
                return null;
            }

            return stackSizeOverride;
        }

        private void OnUserPermissionGranted(string userIdString, string perm)
        {
            if (perm.StartsWith(PlayerLimitProfile.LimitPrefix, StringComparison.OrdinalIgnoreCase))
            {
                _limitManager.RefreshPlayerLimitProfile(userIdString);
            }
            else if (perm.StartsWith(WearableLimitProfile.LimitPrefix, StringComparison.OrdinalIgnoreCase))
            {
                _limitManager.RefreshWearableLimitProfile(userIdString);
            }
            else if (perm.StartsWith(BackpackLimitProfile.LimitPrefix, StringComparison.OrdinalIgnoreCase))
            {
                _limitManager.RefreshBackpackLimitProfile(userIdString);
            }
            else if (perm.StartsWith(ContainerLimitProfile.LimitPrefix, StringComparison.OrdinalIgnoreCase))
            {
                _limitManager.RefreshContainerLimitProfileForPlayer(userIdString);
            }
        }

        private void OnUserPermissionRevoked(string userIdString, string perm)
        {
            OnUserPermissionGranted(userIdString, perm);
        }

        private void OnGroupPermissionGranted(string groupName, string perm)
        {
            if (perm.StartsWith(PlayerLimitProfile.LimitPrefix, StringComparison.OrdinalIgnoreCase))
            {
                _limitManager.RefreshPlayerLimitProfilesForGroup(groupName);
            }
            else if (perm.StartsWith(WearableLimitProfile.LimitPrefix, StringComparison.OrdinalIgnoreCase))
            {
                _limitManager.RefreshWearableLimitProfilesForGroup(groupName);
            }
            else if (perm.StartsWith(BackpackLimitProfile.LimitPrefix, StringComparison.OrdinalIgnoreCase))
            {
                _limitManager.RefreshBackpackLimitProfilesForGroup(groupName);
            }
            else if (perm.StartsWith(ContainerLimitProfile.LimitPrefix, StringComparison.OrdinalIgnoreCase))
            {
                _limitManager.RefreshContainerLimitProfiles();
            }
        }

        private void OnGroupPermissionRevoked(string groupName, string perm)
        {
            OnGroupPermissionGranted(groupName, perm);
        }

        private void OnUserGroupAdded(string userIdString, string groupName)
        {
            _limitManager.RefreshPlayerLimitProfile(userIdString);
            if (_config.WearableBagLimits.Enabled)
            {
                _limitManager.RefreshWearableLimitProfile(userIdString);
            }
            _limitManager.RefreshBackpackLimitProfile(userIdString);
            _limitManager.RefreshContainerLimitProfileForPlayer(userIdString);
        }

        private void OnUserGroupRemoved(string userIdString, string groupName)
        {
            OnUserGroupAdded(userIdString, groupName);
        }

        #endregion

        #region API

        private class ApiInstance
        {
            public Dictionary<string, object> _apiWrapper { get; }

            private BagOfHolding _plugin;
            private Configuration _config => _plugin._config;
            private BagManager _bagManager => _plugin._bagManager;
            private LimitManager _limitManager => _plugin._limitManager;

            public ApiInstance(BagOfHolding plugin)
            {
                _plugin = plugin;

                _apiWrapper = new Dictionary<string, object>
                {
                    [nameof(IsBag)] = new Func<Item, bool>(IsBag),
                    [nameof(IsLootingBag)] = new Func<BasePlayer, bool>(IsLootingBag),
                    [nameof(GetBagProfileName)] = new Func<Item, string>(GetBagProfileName),
                    [nameof(OpenBag)] = new Func<BasePlayer, Item, bool>(OpenBag),
                    [nameof(CreateBag)] = new Func<string, int, Item>(CreateBag),
                    [nameof(GiveBag)] = new Func<BasePlayer, string, int, Item>(GiveBag),
                    [nameof(CreateKitBag)] = new Func<string, string, BasePlayer, Item>(CreateKitBag),
                    [nameof(GiveKitBag)] = new Func<BasePlayer, string, string, Item>(GiveKitBag),
                    [nameof(ChangeBagProfile)] = new Func<Item, string, BasePlayer, bool>(ChangeBagProfile),
                    [nameof(UpgradeBag)] = new Func<Item, BasePlayer, bool>(UpgradeBag),
                    [nameof(FindUnlockedBag)] = new Func<ItemContainer, string, int, int, Item>(FindUnlockedBag),
                    [nameof(DiscoverBags)] = new Action<ItemContainer>(DiscoverBags),
                    [nameof(CreateLimitProfile)] = new Func<string, object>(CreateLimitProfile),
                    [nameof(SetLimitProfile)] = new Func<ItemContainer, object, bool>(SetLimitProfile),
                    [nameof(RemoveLimitProfile)] = new Action<ItemContainer>(RemoveLimitProfile),
                };
            }

            public bool IsBag(Item item)
            {
                return _bagManager.EnsureBagInfoIfEligible(item) != null;
            }

            public bool IsLootingBag(BasePlayer player)
            {
                return _plugin.IsLootingBag(player);
            }

            public string GetBagProfileName(Item item)
            {
                return _bagManager.EnsureBagInfoIfEligible(item)?.BagProfile.Name;
            }

            public bool OpenBag(BasePlayer player, Item item)
            {
                if (!VerifyValidBag(nameof(API_OpenBag), item, out var bagInfo))
                    return false;

                return _plugin.AttemptOpenBag(player, bagInfo);
            }

            public Item CreateBag(string profileName, int amount = 1)
            {
                if (amount <= 0)
                    return null;

                if (!VerifyValidBagProfile(nameof(API_CreateBag), profileName, out var bagProfile))
                    return null;

                return _plugin.CreateBag(bagProfile, amount);
            }

            public Item GiveBag(BasePlayer player, string profileName, int amount = 1)
            {
                if (amount <= 0)
                    return null;

                if (!VerifyValidBagProfile(nameof(API_GiveBag), profileName, out var bagProfile))
                    return null;

                var bagItem = _plugin.CreateBag(bagProfile, amount);
                if (bagItem == null)
                    return null;

                player.GiveItem(bagItem);
                return bagItem;
            }

            public Item CreateKitBag(string profileName, string kitName, BasePlayer initiator = null)
            {
                if (!VerifyValidBagProfile(nameof(API_CreateKitBag), profileName, out var bagProfile))
                    return null;

                return _plugin.CreateKitBag(bagProfile, kitName, initiator);
            }

            public Item GiveKitBag(BasePlayer player, string profileName, string kitName)
            {
                if (!VerifyValidBagProfile(nameof(API_GiveBag), profileName, out var bagProfile))
                    return null;

                if (!VerifyValidKit(nameof(API_GiveBag), kitName))
                    return null;

                var bagItem = _plugin.CreateKitBag(bagProfile, kitName, player);
                if (bagItem == null)
                    return null;

                player.GiveItem(bagItem);
                return bagItem;
            }

            public bool ChangeBagProfile(Item item, string profileName, BasePlayer initiator = null)
            {
                if (!VerifyValidBag(nameof(API_ChangeBagProfile), item, out var bagInfo))
                    return false;

                if (!VerifyValidBagProfile(nameof(API_ChangeBagProfile), profileName, out var bagProfile))
                    return false;

                bagInfo.ChangeProfile(_plugin, bagProfile, initiator);
                return true;
            }

            public bool UpgradeBag(Item item, BasePlayer initiator = null)
            {
                if (!VerifyValidBag(nameof(API_UpgradeBag), item, out var bagInfo))
                    return false;

                if (bagInfo.TryUpgrade(_plugin, initiator))
                    return true;

                LogError($"[{nameof(API_UpgradeBag)}] Bag profile \"{bagInfo.BagProfile.Name}\" has no valid upgrade target.");
                return false;
            }

            public Item FindUnlockedBag(ItemContainer container, string profileOrDisplayName = null, int start = 0, int end = -1)
            {
                return _plugin.FindUnlockedBag(container, profileOrDisplayName, start, end)?.Item;
            }

            public void DiscoverBags(ItemContainer container)
            {
                _plugin.DiscoverBags(container);
            }

            public object CreateLimitProfile(string limitSpec)
            {
                var limitProfile = JsonConvert.DeserializeObject<CustomLimitProfile>(limitSpec);
                limitProfile.Init(_plugin, _plugin._config);
                return limitProfile;
            }

            public bool SetLimitProfile(ItemContainer container, object limitProfile)
            {
                var typedLimitedProfile = limitProfile as CustomLimitProfile;
                if (typedLimitedProfile == null)
                {
                    LogError(limitProfile.GetType().Name == nameof(CustomLimitProfile)
                        ? $"[{nameof(API_SetLimitProfile)}] It appears that {nameof(BagOfHolding)} has recompiled since the caller of this API created that limit profile. Please create a new limit profile by calling API_CreateLimitProfile."
                        : $"[{nameof(API_SetLimitProfile)}] Limit profile type is not correct. Please make sure to pass this an object that was created by calling API_CreateLimitProfile.");

                    return false;
                }

                if (container == null)
                {
                    LogError($"[{nameof(API_SetLimitProfile)}] ItemContainer is null. Limit profile cannot be set.");
                    return false;
                }

                _limitManager.SetCustomLimitProfile(container, typedLimitedProfile);
                return true;
            }

            public void RemoveLimitProfile(ItemContainer container)
            {
                if (container == null)
                {
                    LogError($"[{nameof(API_RemoveLimitProfile)}] ItemContainer is null. Limit profile cannot be removed.");
                    return;
                }

                _limitManager.RemoveCustomLimitProfile(container);
            }

            private bool VerifyValidBag(string errorPrefix, Item item, out BagInfo bagInfo)
            {
                bagInfo = _bagManager.EnsureBagInfoIfEligible(item);
                if (bagInfo != null)
                    return true;

                LogError($"[{errorPrefix}] Invalid bag: {item.info.shortname} / {item.skin}");
                return false;
            }

            private bool VerifyValidBagProfile(string errorPrefix, string profileName, out BagProfile bagProfile)
            {
                bagProfile = _config.GetBagProfileByName(profileName);
                if (bagProfile != null)
                    return true;

                LogError($"[{errorPrefix}] Invalid bag profile: \"{profileName}\"");
                return false;
            }

            private bool VerifyValidKit(string errorPrefix, string kitName)
            {
                if (_plugin.KitExists(kitName))
                    return true;

                LogError($"[{errorPrefix}] Invalid kit name: \"{kitName}\"");
                return false;
            }
        }

        [HookMethod(nameof(API_GetApi))]
        public Dictionary<string, object> API_GetApi()
        {
            return _api._apiWrapper;
        }

        [HookMethod(nameof(API_IsBag))]
        public object API_IsBag(Item item)
        {
            return ObjectCache.Get(_api.IsBag(item));
        }

        [HookMethod(nameof(API_IsLootingBag))]
        public object API_IsLootingBag(BasePlayer player)
        {
            return ObjectCache.Get(_api.IsLootingBag(player));
        }

        [HookMethod(nameof(API_GetBagProfileName))]
        public string API_GetBagProfileName(Item item)
        {
            return _api.GetBagProfileName(item);
        }

        [HookMethod(nameof(API_OpenBag))]
        public object API_OpenBag(BasePlayer player, Item item)
        {
            return ObjectCache.Get(_api.OpenBag(player, item));
        }

        [HookMethod(nameof(API_CreateBag))]
        public Item API_CreateBag(string profileName, int amount = 1)
        {
            return _api.CreateBag(profileName, amount);
        }

        [HookMethod(nameof(API_GiveBag))]
        public Item API_GiveBag(BasePlayer player, string profileName, int amount = 1)
        {
            return _api.GiveBag(player, profileName, amount);
        }

        [HookMethod(nameof(API_CreateKitBag))]
        public Item API_CreateKitBag(string profileName, string kitName, BasePlayer initiator = null)
        {
            return _api.CreateKitBag(profileName, kitName, initiator);
        }

        [HookMethod(nameof(API_GiveKitBag))]
        public Item API_GiveKitBag(BasePlayer player, string profileName, string kitName)
        {
            return _api.GiveKitBag(player, profileName, kitName);
        }

        [HookMethod(nameof(API_ChangeBagProfile))]
        public object API_ChangeBagProfile(Item item, string profileName, BasePlayer initiator = null)
        {
            return ObjectCache.Get(_api.ChangeBagProfile(item, profileName, initiator));
        }

        [HookMethod(nameof(API_UpgradeBag))]
        public object API_UpgradeBag(Item item, BasePlayer initiator = null)
        {
            return ObjectCache.Get(_api.UpgradeBag(item, initiator));
        }

        [HookMethod(nameof(API_FindUnlockedBag))]
        public Item API_FindUnlockedBag(ItemContainer container, string profileOrDisplayName = null, int start = 0, int end = -1)
        {
            return _api.FindUnlockedBag(container, profileOrDisplayName, start, end);
        }

        [HookMethod(nameof(API_DiscoverBags))]
        public void API_DiscoverBags(ItemContainer container)
        {
            _api.DiscoverBags(container);
        }

        [HookMethod(nameof(API_CreateLimitProfile))]
        public object API_CreateLimitProfile(string limitSpec)
        {
            return _api.CreateLimitProfile(limitSpec);
        }

        [HookMethod(nameof(API_SetLimitProfile))]
        public object API_SetLimitProfile(ItemContainer container, object limitProfile)
        {
            return ObjectCache.Get(_api.SetLimitProfile(container, limitProfile));
        }

        [HookMethod(nameof(API_RemoveLimitProfile))]
        public void API_RemoveLimitProfile(ItemContainer container)
        {
            _api.RemoveLimitProfile(container);
        }

        #endregion

        #region Exposed Hooks

        private static class ExposedHooks
        {
            public static object OnBagOpen(Item item, BasePlayer player)
            {
                return Interface.CallHook("OnBagOpen", item, player);
            }

            public static void OnBagOpened(Item item, BasePlayer player)
            {
                Interface.CallHook("OnBagOpened", item, player);
            }

            public static void OnBagClosed(Item item, BasePlayer player)
            {
                Interface.CallHook("OnBagClosed", item, player);
            }

            public static object OnBagUpgrade(Item item, string newProfileName, BasePlayer initiator)
            {
                return Interface.CallHook("OnBagUpgrade", item, newProfileName, initiator);
            }

            public static void OnBagUpgraded(Item item, string newProfileName, BasePlayer initiator)
            {
                Interface.CallHook("OnBagUpgraded", item, newProfileName, initiator);
            }

            public static void OnBagProfileChanged(Item item, string newProfileName)
            {
                Interface.CallHook("OnBagProfileChanged", item, newProfileName);
            }
        }

        #endregion

        #region Dependencies

        private class BackpacksAdapter
        {
            private class BackpacksApi
            {
                public static BackpacksApi Parse(Dictionary<string, object> dict)
                {
                    var backpacksApi = new BackpacksApi();

                    GetOption(dict, nameof(GetBackpackOwnerId), out backpacksApi.GetBackpackOwnerId);

                    return backpacksApi;
                }

                public Func<ItemContainer, ulong> GetBackpackOwnerId;

                private static void GetOption<T>(Dictionary<string, object> dict, string key, out T result)
                {
                    result = dict.TryGetValue(key, out var objectValue) && objectValue is T valueOfType
                        ? valueOfType
                        : default;
                }
            }

            public bool SupportsPages => _api != null;

            private BagOfHolding _plugin;
            private BackpacksApi _api;
            private Plugin Backpacks => _plugin.Backpacks;

            public BackpacksAdapter(BagOfHolding plugin)
            {
                _plugin = plugin;
            }

            public void HandleLoadedChanged()
            {
                _api = Backpacks?.Call("API_GetApi") is Dictionary<string, object> apiDict
                    ? BackpacksApi.Parse(apiDict)
                    : null;
            }

            public ulong GetBackpackOwnerId(ItemContainer container)
            {
                if (Backpacks == null)
                    return 0;

                // Optimization: Skip calling Backpacks if the container is not BoxStorage.
                var boxStorage = container.entityOwner as BoxStorage;
                if ((object)boxStorage == null)
                    return 0;

                if (_api?.GetBackpackOwnerId != null)
                    return _api.GetBackpackOwnerId(container);

                var result = Backpacks.Call($"API_GetBackpackOwnerId", container);
                if (result is ulong ownerId)
                    return ownerId;

                return 0;
            }
        }

        private class ItemRetrieverAdapter
        {
            private class ItemRetrieverApi
            {
                public static ItemRetrieverApi Parse(Dictionary<string, object> dict)
                {
                    var backpacksApi = new ItemRetrieverApi();

                    GetOption(dict, nameof(SumPlayerItems), out backpacksApi.SumPlayerItems);
                    GetOption(dict, nameof(TakePlayerItems), out backpacksApi.TakePlayerItems);

                    return backpacksApi;
                }

                public Func<BasePlayer, Dictionary<string, object>, int> SumPlayerItems;
                public Func<BasePlayer, Dictionary<string, object>, int, List<Item>, int> TakePlayerItems;

                private static void GetOption<T>(Dictionary<string, object> dict, string key, out T result)
                {
                    result = dict.TryGetValue(key, out var objectValue) && objectValue is T valueOfType
                        ? valueOfType
                        : default;
                }
            }

            private BagOfHolding _plugin;
            private ItemRetrieverApi _api;
            private Dictionary<string, object> _itemRetrieverQuery = new();

            public ItemRetrieverAdapter(BagOfHolding plugin)
            {
                _plugin = plugin;
            }

            public void HandleLoadedChanged()
            {
                _api = _plugin.ItemRetriever?.Call("API_GetApi") is Dictionary<string, object> apiDict
                    ? ItemRetrieverApi.Parse(apiDict)
                    : null;
            }

            public int? SumPlayerItems(BasePlayer player, ref ItemQuery itemQuery)
            {
                return _api?.SumPlayerItems(player, SetupItemRetrieverQuery(ref itemQuery));
            }

            public int? TakePlayerItems(BasePlayer player, ref ItemQuery itemQuery, int amount, List<Item> collect = null)
            {
                return _api?.TakePlayerItems(player, SetupItemRetrieverQuery(ref itemQuery), amount, collect);
            }

            private Dictionary<string, object> SetupItemRetrieverQuery(ref ItemQuery itemQuery)
            {
                _itemRetrieverQuery.Clear();
                _itemRetrieverQuery["RequireEmpty"] = True;

                if (itemQuery.ItemId != 0)
                    _itemRetrieverQuery["ItemId"] = ObjectCache.Get(itemQuery.ItemId);

                if (itemQuery.SkinId.HasValue)
                    _itemRetrieverQuery["SkinId"] = ObjectCache.Get(itemQuery.SkinId.Value);

                return _itemRetrieverQuery;
            }
        }

        private class SkillTreeAdapter
        {
            private BagOfHolding _plugin;
            private Func<ulong, string> _extraPocketsOwnerIdProvider;

            public SkillTreeAdapter(BagOfHolding plugin)
            {
                _plugin = plugin;
            }

            public void HandleLoadedChanged()
            {
                _extraPocketsOwnerIdProvider = _plugin.SkillTree?.Call("GetExtraPocketsOwnerIdProvider") as Func<ulong, string>;
            }

            public string GetExtraPocketsOwnerIdString(ItemContainer container)
            {
                return _extraPocketsOwnerIdProvider?.Invoke(container.uid.Value);
            }
        }

        private bool KitExists(string kitName)
        {
            return Kits?.Call("IsKit", kitName) is true;
        }

        private IEnumerable<Item> CreateKitItems(string kitName)
        {
            return Kits?.Call("CreateKitItems", kitName) as IEnumerable<Item>;
        }

        #endregion

        #region Commands

        private BagInfo FindUnlockedBagInParents(BagInfo bagInfo, string profileName, bool forward, out ItemContainer parentContainer)
        {
            parentContainer = bagInfo.Item.parent;
            if (parentContainer == null)
                return null;

            var parentItem = parentContainer.parent;
            if (parentItem != null)
            {
                var parentBagInfo = _bagManager.GetBagInfo(parentItem);
                if (parentBagInfo == null)
                {
                    // Bag is inside an unrecognized parent item.
                    return null;
                }

                var piblingBagInfo = FindUnlockedBag(parentContainer, profileName, forward, bagInfo);
                if (piblingBagInfo != null)
                    return piblingBagInfo;

                // If going backward and no pibling bag found, try parent bag.
                if (!forward && parentBagInfo.BagProfile.MatchesNameOrDisplayName(profileName))
                    return parentBagInfo;

                return FindUnlockedBagInParents(parentBagInfo, profileName, forward, out parentContainer);
            }

            var playerOwner = GetOwnerPlayer(parentContainer);
            if ((object)playerOwner != null)
            {
                var containerList = CustomPool.GetList<ItemContainer>();
                AddPlayerContainers(containerList, playerOwner);
                var foundBag = FindUnlockedBagInContainers(containerList, profileName, forward, currentBag: bagInfo);
                CustomPool.FreeList(ref containerList);
                return foundBag;
            }

            var lootableCorpse = parentContainer.entityOwner as LootableCorpse;
            if ((object)lootableCorpse != null && lootableCorpse.containers.Contains(parentContainer))
                return FindUnlockedBagInContainers(lootableCorpse.containers, profileName, forward, currentBag: bagInfo);

            return FindUnlockedBag(parentContainer, profileName, forward, bagInfo);
        }

        private void CommandAdvanceShared(IPlayer player, string[] args,
            bool addPlayerContainers = true,
            bool addLootContainers = true,
            bool forward = true,
            bool wrapAround = true)
        {
            if (!VerifyIsPlayer(player, out var basePlayer))
                return;

            var profileName = args.FirstOrDefault();
            if (!IsValidArg(profileName))
            {
                profileName = null;
            }

            if (IsLootingContainer(basePlayer, out var containerInfo) && containerInfo.BagInfo != null)
            {
                if (forward)
                {
                    // Find first bag while traversing downward.
                    var childBagInfo = FindUnlockedBag(containerInfo.BagInfo.Item.contents, profileName, forward: true);
                    if (childBagInfo != null)
                    {
                        AttemptOpenBag(basePlayer, childBagInfo);
                        return;
                    }
                }

                // Next, try to find a sibling bag after the current bag.
                var parentContainer = containerInfo.Container.parent?.parent;
                if (parentContainer == null)
                    return;

                var nextBag = FindUnlockedBagInParents(containerInfo.BagInfo, profileName, forward, out parentContainer);
                if (nextBag != null)
                {
                    AttemptOpenBag(basePlayer, nextBag);
                    return;
                }

                if (!wrapAround)
                {
                    ClosePlayerInventory(basePlayer);
                    return;
                }

                var playerOwner = GetOwnerPlayer(parentContainer);
                if ((object)playerOwner != null)
                {
                    nextBag = FindUnlockedBagAfterBag(playerOwner, profileName, forward);
                    if (nextBag != null && nextBag != containerInfo.BagInfo)
                    {
                        AttemptOpenBag(basePlayer, nextBag);
                    }
                    return;
                }

                if (parentContainer.entityOwner is IItemContainerEntity containerEntity)
                {
                    nextBag = FindUnlockedBag(containerEntity.inventory, profileName, forward);
                    if (nextBag != null)
                    {
                        AttemptOpenBag(basePlayer, nextBag);
                    }
                    return;
                }

                var lootableCorpse = parentContainer.entityOwner as LootableCorpse;
                if ((object)lootableCorpse != null)
                {
                    nextBag = FindUnlockedBagInContainers(lootableCorpse.containers, profileName, forward);
                    if (nextBag != null)
                    {
                        AttemptOpenBag(basePlayer, nextBag);
                    }
                    return;
                }

                var droppedItemContainer = parentContainer.entityOwner as DroppedItemContainer;
                if (droppedItemContainer != null)
                {
                    nextBag = FindUnlockedBag(droppedItemContainer.inventory, profileName, forward);
                    if (nextBag != null)
                    {
                        AttemptOpenBag(basePlayer, nextBag);
                    }
                    return;
                }
            }
            else
            {
                // Force a delay if not currently looting and not triggered via key bind.
                var shouldDelay = !IsKeyBindArg(args.LastOrDefault());

                if (!OpenFirstBag(
                    basePlayer,
                    profileName,
                    addPlayerContainers: addPlayerContainers,
                    addLootContainers: addLootContainers,
                    forward: forward,
                    shouldDelay: shouldDelay
                ))
                {
                    ChatMessage(basePlayer, LangEntry.ErrorNoBags);
                }
            }
        }

        [Command("bag.open")]
        private void CommandBagOpen(IPlayer player, string cmd, string[] args)
        {
            CommandAdvanceShared(player, args, wrapAround: false);
        }

        [Command("bag.next")]
        private void CommandBagNext(IPlayer player, string cmd, string[] args)
        {
            CommandAdvanceShared(player, args);
        }

        [Command("bag.prev", "bag.previous")]
        private void CommandBagPrevious(IPlayer player, string cmd, string[] args)
        {
            CommandAdvanceShared(player, args, forward: false);
        }

        [Command("bag.parent")]
        private void CommandBagParent(IPlayer player, string cmd, string[] args)
        {
            if (!VerifyIsPlayer(player, out var basePlayer))
                return;

            // Must be looting a bag.
            if (!IsLootingContainer(basePlayer, out var containerInfo) || containerInfo.BagInfo == null)
                return;

            // Bag must have a valid parent.
            var parentContainer = containerInfo.Container.parent?.parent;
            if (parentContainer == null)
                return;

            // Only the Backpacks plugin can open a backpack.
            // If Backpacks returns 0, the container is not a backpack.
            var backpackOwnerId = _backpacksAdapter.GetBackpackOwnerId(parentContainer);
            if (backpackOwnerId != 0)
            {
                basePlayer.SendConsoleCommand(basePlayer.userID == backpackOwnerId
                    ? "backpack.open"
                    : $"viewbackpack {backpackOwnerId.ToString()}");

                return;
            }

            // Only the Skill Tree plugin can open Extra Pockets pouches.
            // If Skill Tree returns null, the container is not an Extra Pockets pouch.
            var extraPocketsOwnerIdString = _skillTreeAdapter.GetExtraPocketsOwnerIdString(parentContainer);
            if (extraPocketsOwnerIdString != null)
            {
                if (parentContainer.entityOwner != null)
                {
                    StartLootingEntity(basePlayer, parentContainer.entityOwner, doPositionChecks: false);
                }
                return;
            }

            var parentContainerInfo = ContainerInfo.FromContainer(parentContainer, _bagManager);
            if (parentContainerInfo.BagInfo != null)
            {
                AttemptOpenBag(basePlayer, parentContainerInfo.BagInfo);
                return;
            }

            var shouldDelay = ShouldDelayOpening(
                LootPanelInfo.FromBagInfo(containerInfo.BagInfo),
                LootPanelInfo.FromContainer(parentContainer, _bagManager)
            );
            StartLootingEntity(basePlayer, parentContainerInfo.Entity, shouldDelay);
        }

        [Command("bag.select")]
        private void CommandBagSelect(IPlayer player, string cmd, string[] args)
        {
            if (!VerifyIsPlayer(player, out var basePlayer))
                return;

            if (args.Length == 0 || !ulong.TryParse(args[0], out var itemUID))
                return;

            if (!IsLootingContainer(basePlayer, out var containerInfo) || containerInfo.BagInfo == null)
                return;

            var parentContainer = containerInfo.Container.parent?.parent;
            if (parentContainer == null)
                return;

            var supervisor = _limitManager.GetSupervisor(parentContainer);
            var bagInfo = supervisor?.FindBag(itemUID);
            if (bagInfo == null)
                return;

            if (bagInfo == containerInfo.BagInfo)
                return;

            AttemptOpenBag(basePlayer, bagInfo);
        }

        [Command("bagofholding.spawnloot", "boh.spawnloot")]
        private void CommandPopulateLoot(IPlayer player, string cmd, string[] args)
        {
            if (!VerifyHasPermission(player, PermissionStats))
                return;

            var itemCount = 0;

            foreach (var networkable in BaseNetworkable.serverEntities)
            {
                if (networkable is not LootContainer lootContainer
                    || lootContainer == null
                    || lootContainer.IsDestroyed
                    || lootContainer.OwnerID != 0
                    || lootContainer.skinID != 0)
                    continue;

                if (!_config.LootConfig.SpawnChanceByPrefabId.TryGetValue(lootContainer.prefabID, out var spawnChanceByProfile))
                    continue;

                itemCount += HandleLootSpawn(lootContainer, spawnChanceByProfile);
            }

            ReplyToPlayer(player, LangEntry.LootSpawnSuccess, itemCount.ToString());
        }

        [Command("bagofholding.clearloot", "boh.clearloot")]
        private void CommandClearLootContainers(IPlayer player, string cmd, string[] args)
        {
            if (!VerifyHasPermission(player, PermissionManageLoot))
                return;

            var bagItemList = CustomPool.GetList<Item>();

            foreach (var networkable in BaseNetworkable.serverEntities)
            {
                if (networkable is not LootContainer lootContainer
                    || lootContainer == null
                    || lootContainer.IsDestroyed
                    || IsPlayerDeployedLootContainer(lootContainer))
                    continue;

                var supervisor = _limitManager.GetSupervisor(lootContainer.inventory);
                if (supervisor == null)
                    continue;

                foreach (var bagInfo in supervisor.AllBags)
                {
                    if (bagInfo.HasItems)
                        continue;

                    bagItemList.Add(bagInfo.Item);
                }
            }

            foreach (var item in bagItemList)
            {
                item.RemoveFromContainer();
                item.Remove();
            }

            var itemCount = bagItemList.Count;
            CustomPool.FreeList(ref bagItemList);
            ReplyToPlayer(player, LangEntry.LootClearSuccess, itemCount.ToString());
        }

        [Command("bagofholding.stats", "boh.stats")]
        private void CommandStats(IPlayer player, string cmd, string[] args)
        {
            if (!VerifyHasPermission(player, PermissionStats))
                return;

            player.Reply(_bagManager.GetBagStats(this, player, BagQuery.FromArgs(this, args)));
        }

        [Command("bagofholding.listbags", "boh.listbags")]
        private void CommandListBags(IPlayer player, string cmd, string[] args)
        {
            if (!VerifyHasPermission(player, PermissionListBags))
                return;

            var bagList = _config.BagProfiles;

            var filterArg = args.FirstOrDefault();
            if (IsValidArg(filterArg))
            {
                bagList = bagList.Where(bagProfile => bagProfile.MatchesNameOrDisplayName(filterArg)).ToArray();

                if (bagList.Length == 0)
                {
                    ReplyToPlayer(player, LangEntry.ErrorListBagsNotFound, filterArg);
                    return;
                }
            }

            // Force the output to display in console.
            player.LastCommand = CommandType.Console;

            var textTable = new TextTable();

            // Header names are the same as in the config.
            textTable.AddColumns("Name", "Skin ID", "Capacity", "Display name", "Category name", "Contents ruleset", "Upgrades to", "Upgrade cost");

            foreach (var bagProfile in bagList)
            {
                var upgradeTarget = "-";
                var upgradeCost = "-";

                if (bagProfile.UpgradeTarget?.IsValid ?? false)
                {
                    var paymentProvider = _paymentProviderResolver.Resolve(bagProfile.UpgradeTarget.Cost);
                    if (paymentProvider.IsAvailable)
                    {
                        upgradeTarget = $"{bagProfile.UpgradeTarget.TargetProfile.Name}";
                        upgradeCost = $"{bagProfile.UpgradeTarget.Cost.Amount.ToString()} {GetCurrencyName(player.Id, paymentProvider)}";
                    }
                }

                textTable.AddRow(
                    bagProfile.Name,
                    bagProfile.SkinId.ToString(),
                    bagProfile.Capacity.ToString(),
                    bagProfile.DisplayName,
                    bagProfile.CategoryName,
                    bagProfile.ContentsRuleset?.Name ?? "-",
                    upgradeTarget,
                    upgradeCost
                );
            }

            player.Reply(textTable.ToString());
        }

        [Command("bagofholding.listcontainers", "boh.listcontainers")]
        private void CommandListContainers(IPlayer player, string cmd, string[] args)
        {
            if (!VerifyHasPermission(player, PermissionListContainers))
                return;

            var containerPrefabs = new List<string>();

            foreach (var assetPath in GameManifest.Current.entities)
            {
                var baseEntity = GameManager.server.FindPrefab(assetPath)?.GetComponent<BaseEntity>();
                if (baseEntity == null)
                    continue;

                if (baseEntity is IItemContainerEntity || baseEntity is LootableCorpse || baseEntity is DroppedItemContainer)
                {
                    containerPrefabs.Add(baseEntity.PrefabName);
                }
            }

            containerPrefabs.Sort();
            player.Reply(string.Join("\n", containerPrefabs));
        }

        [Command("bagofholding.givebag", "boh.givebag")]
        private void CommandGiveBag(IPlayer player, string cmd, string[] args)
        {
            if (!VerifyHasPermission(player, PermissionGiveBag))
                return;

            if (args.Length < 2)
            {
                ReplyToPlayer(player, LangEntry.GiveBagSyntax, cmd);
                return;
            }

            if (!VerifyTargetPlayer(player, args[0], out var targetPlayer)
                || !VerifyBagProfileName(player, args[1], out var bagProfile))
                return;

            var amount = 1;
            if (args.Length >= 3 && !int.TryParse(args[2], out amount))
            {
                ReplyToPlayer(player, LangEntry.GiveBagSyntax);
                return;
            }

            var bagItem = CreateBag(bagProfile, amount);
            if (bagItem == null)
                return;

            targetPlayer.GiveItem(bagItem);
            ReplyToPlayer(player, LangEntry.GiveBagSuccess, amount, bagProfile.DisplayName, targetPlayer.displayName);
        }

        [Command("bagofholding.givekitbag", "boh.givekitbag")]
        private void CommandGiveKitBag(IPlayer player, string cmd, string[] args)
        {
            if (!VerifyHasPermission(player, PermissionGiveKitBag))
                return;

            if (args.Length < 3)
            {
                ReplyToPlayer(player, LangEntry.GiveKitBagSyntax, cmd);
                return;
            }

            var kitName = args[2];

            if (!VerifyTargetPlayer(player, args[0], out var targetPlayer)
                || !VerifyBagProfileName(player, args[1], out var bagProfile))
                return;

            if (!KitExists(kitName))
            {
                ReplyToPlayer(player, LangEntry.ErrorKitNotFound, kitName);
                return;
            }

            var bagItem = CreateKitBag(bagProfile, kitName, targetPlayer);
            if (bagItem == null)
                return;

            targetPlayer.GiveItem(bagItem);
            ReplyToPlayer(player, LangEntry.GiveKitBagSuccess, bagProfile.DisplayName, targetPlayer.displayName, kitName);
        }

        [Command("bagofholding.setskins", "boh.setskins")]
        private void CommandSetSkins(IPlayer player, string cmd, string[] args)
        {
            if (!VerifyHasPermission(player, PermissionConfig))
                return;

            if (args.Length < 2)
            {
                ReplyToPlayer(player, LangEntry.SetSkinsSyntax, cmd);
                return;
            }

            var categoryName = args[0];

            var matchingBagProfiles = _config.BagProfiles
                .Where(bagProfile => bagProfile.CategoryNumber != -1 && StringUtils.Equals(bagProfile.CategoryName, categoryName))
                .ToArray();

            if (matchingBagProfiles.Length == 0)
            {
                ReplyToPlayer(player, LangEntry.ErrorBagCategoryNotFound, categoryName);
                return;
            }

            var skinIdArgList = args.Skip(1).ToArray();

            if (matchingBagProfiles.Length != skinIdArgList.Length)
            {
                ReplyToPlayer(player, LangEntry.SetSkinsCountMismatch, matchingBagProfiles.Length.ToString(), categoryName, skinIdArgList.Length.ToString());
                return;
            }

            var skinIdList = new List<ulong>();

            foreach (var skinIdArg in skinIdArgList)
            {
                if (!ulong.TryParse(skinIdArg, out var skinId))
                {
                    ReplyToPlayer(player, LangEntry.ErrorInvalidSkinId, skinIdArg);
                    return;
                }

                skinIdList.Add(skinId);
            }

            for (var i = 0; i < matchingBagProfiles.Length; i++)
            {
                matchingBagProfiles[i].SkinId = skinIdList[i];
            }

            _config.IndexBagBySkins();
            RefreshStackHooks();

            foreach (var bagProfile in matchingBagProfiles)
            {
                _bagManager.UpdateSkinId(bagProfile);
            }

            SaveConfig();
            ReplyToPlayer(player, LangEntry.SetSkinsSuccess, categoryName);
            RegisterCustomItemsWithLootTable();
        }

        [Command("bag.ui.upgrade")]
        private void CommandUIUpgrade(IPlayer player, string cmd, string[] args)
        {
            if (!VerifyIsPlayer(player, out var basePlayer))
                return;

            if (!IsLootingBag(basePlayer, out var bagInfo))
                return;

            if (!CanPlayerUpgradeBag(basePlayer, bagInfo))
            {
                ReplyToPlayer(player, LangEntry.ErrorNoPermission);
                return;
            }

            if (StringUtils.Equals(args.FirstOrDefault(), "confirm"))
            {
                if (bagInfo.Item.amount > 1)
                    return;

                var upgradeTarget = bagInfo.BagProfile.UpgradeTarget;
                var costInfo = upgradeTarget?.Cost;
                if (upgradeTarget?.TargetProfile == null || costInfo == null)
                    return;

                var paymentProvider = _paymentProviderResolver.Resolve(costInfo, bagInfo.Item);
                if (!paymentProvider.IsAvailable)
                    return;

                var costAmount = costInfo.Amount;
                var costAmountOverride = _config.GetUpgradeCostOverride(basePlayer.UserIDString, bagInfo.BagProfile);
                if (costAmountOverride >= 0)
                {
                    costAmount = costAmountOverride;
                }

                if (paymentProvider.GetBalance(basePlayer) < costAmount)
                    return;

                if (!bagInfo.TryUpgrade(this, basePlayer))
                    return;

                paymentProvider.TakeBalance(basePlayer, costAmount);
            }
            else
            {
                bagInfo.ToggleUpgradeViewer(this, basePlayer);
            }
        }

        [Command("bag.ui.togglegather")]
        private void CommandUIToggleGather(IPlayer player, string cmd, string[] args)
        {
            if (!_config.GatherMode.Enabled)
                return;

            if (!VerifyIsPlayer(player, out var basePlayer))
                return;

            if (!IsLootingBag(basePlayer, out var bagInfo))
                return;

            if (!bagInfo.CanGather)
                return;

            var gatherMode = bagInfo.GatherMode;
            if (gatherMode == GatherMode.None)
            {
                gatherMode = GatherMode.All;
            }
            else if (gatherMode == GatherMode.All)
            {
                gatherMode = GatherMode.Existing;
            }
            else
            {
                gatherMode = GatherMode.None;
            }

            bagInfo.SetGatherMode(gatherMode);
            bagInfo.RefreshUI(this);
        }

        #endregion

        #region Helper Classes & Structs

        private static class StringUtils
        {
            public static bool Equals(string a, string b) =>
                string.Compare(a, b, StringComparison.OrdinalIgnoreCase) == 0;

            public static bool Contains(string haystack, string needle) =>
                haystack.Contains(needle, CompareOptions.IgnoreCase);
        }

        private struct ContainerInfo
        {
            public static ContainerInfo FromContainer(ItemContainer container, BagManager bagManager)
            {
                var info = new ContainerInfo
                {
                    Container = container,
                    Entity = container.entityOwner ?? GetOwnerPlayer(container),
                };

                var parentItem = container.parent;
                if (parentItem != null)
                {
                    info.BagInfo = bagManager.EnsureBagInfoIfEligible(parentItem);
                }

                return info;
            }

            public ItemContainer Container { get; private set; }
            public BaseEntity Entity { get; private set; }
            public BagInfo BagInfo { get; private set; }
        }

        private struct LootPanelInfo
        {
            public static LootPanelInfo FromContainer(ItemContainer container, BagManager bagManager)
            {
                var parentItem = container.parent;
                if (parentItem != null)
                {
                    var bagInfo = bagManager.GetBagInfo(parentItem);
                    if (bagInfo != null)
                        return FromBagInfo(bagInfo);
                }

                var storageContainer = container.entityOwner as StorageContainer;
                if ((object)storageContainer != null)
                {
                    return new LootPanelInfo
                    {
                        Capacity = storageContainer.inventory?.capacity ?? 0,
                        PanelName = storageContainer.panelName ?? GenericResizableLootPanelName,
                    };
                }

                var containerIOEntity = container.entityOwner as ContainerIOEntity;
                if ((object)containerIOEntity != null)
                {
                    return new LootPanelInfo
                    {
                        Capacity = containerIOEntity.inventory?.capacity ?? 0,
                        PanelName = containerIOEntity.lootPanelName ?? GenericResizableLootPanelName,
                    };
                }

                var lootableCorpse = container.entityOwner as LootableCorpse;
                if ((object)lootableCorpse != null)
                {
                    return new LootPanelInfo
                    {
                        Capacity = lootableCorpse.containers.FirstOrDefault()?.capacity ?? 0,
                        PanelName = lootableCorpse.lootPanelName ?? GenericResizableLootPanelName,
                    };
                }

                return new LootPanelInfo
                {
                    Capacity = 0,
                    PanelName = GenericResizableLootPanelName,
                };
            }

            public static LootPanelInfo FromBagProfile(BagProfile bagProfile)
            {
                return new LootPanelInfo
                {
                    Capacity = bagProfile.Capacity,
                    PanelName = GenericResizableLootPanelName,
                };
            }

            public static LootPanelInfo FromBagInfo(BagInfo bagInfo)
            {
                return new LootPanelInfo
                {
                    Capacity = bagInfo.Item.contents.capacity,
                    PanelName = GenericResizableLootPanelName,
                };
            }

            public int Capacity;
            public string PanelName;
        }

        private static class ObjectCache
        {
            private static readonly object True = true;
            private static readonly object False = false;

            private static class StaticObjectCache<T>
            {
                private static readonly Dictionary<T, object> _cacheByValue = new();

                public static object Get(T value)
                {
                    if (!_cacheByValue.TryGetValue(value, out var cachedObject))
                    {
                        cachedObject = value;
                        _cacheByValue[value] = cachedObject;
                    }
                    return cachedObject;
                }
            }

            public static object Get<T>(T value)
            {
                return StaticObjectCache<T>.Get(value);
            }

            public static object Get(bool value)
            {
                return value ? True : False;
            }
        }

        private interface IStringCache
        {
            string Get<T>(T value);
            string Get<T>(T value, Func<T, string> createString);
            string Get(bool value);
        }

        private sealed class StringCache : IStringCache
        {
            public static readonly StringCache Instance = new();

            private static class StaticStringCache<T>
            {
                private static readonly Dictionary<T, string> _cacheByValue = new();

                public static string Get(T value)
                {
                    if (!_cacheByValue.TryGetValue(value, out var str))
                    {
                        str = value.ToString();
                        _cacheByValue[value] = str;
                    }

                    return str;
                }
            }

            private static class StaticStringCacheWithFactory<T>
            {
                private static readonly Dictionary<Func<T, string>, Dictionary<T, string>> _cacheByDelegate = new();

                public static string Get(T value, Func<T, string> createString)
                {
                    if (!_cacheByDelegate.TryGetValue(createString, out var cache))
                    {
                        cache = new Dictionary<T, string>();
                        _cacheByDelegate[createString] = cache;
                    }

                    if (!cache.TryGetValue(value, out var str))
                    {
                        str = createString(value);
                        cache[value] = str;
                    }

                    return str;
                }
            }

            private StringCache() {}

            public string Get<T>(T value)
            {
                return StaticStringCache<T>.Get(value);
            }

            public string Get(bool value)
            {
                return value ? "true" : "false";
            }

            public string Get<T>(T value, Func<T, string> createString)
            {
                return StaticStringCacheWithFactory<T>.Get(value, createString);
            }
        }

        private static class ItemUtils
        {
            public static bool HasChildren(Item item)
            {
                return item.contents?.itemList?.Count > 0;
            }

            public static bool HasMatchingItem(List<Item> itemList, ref ItemQuery itemQuery)
            {
                for (var i = 0; i < itemList.Count; i++)
                {
                    if (itemQuery.GetUsableAmount(itemList[i]) > 0)
                        return true;
                }

                return false;
            }

            public static int SumItems(List<Item> itemList, ref ItemQuery itemQuery)
            {
                var sum = 0;

                for (var i = 0; i < itemList.Count; i++)
                {
                    var item = itemList[i];
                    sum += itemQuery.GetUsableAmount(item);

                    if (HasSearchableContainer(item, out var childItems))
                    {
                        sum += SumItems(childItems, ref itemQuery);
                    }
                }

                return sum;
            }

            public static int SumPlayerItems(BasePlayer player, ref ItemQuery itemQuery)
            {
                return SumItems(player.inventory.containerMain.itemList, ref itemQuery)
                    + SumItems(player.inventory.containerBelt.itemList, ref itemQuery);
            }

            public static int TakeItems(List<Item> itemList, ref ItemQuery itemQuery, int amount, List<Item> collect)
            {
                var totalAmountTaken = 0;

                for (var i = itemList.Count - 1; i >= 0; i--)
                {
                    var item = itemList[i];
                    var amountToTake = amount - totalAmountTaken;
                    if (amountToTake <= 0)
                        break;

                    var usableAmount = itemQuery.GetUsableAmount(item);
                    if (usableAmount > 0)
                    {
                        amountToTake = Math.Min(usableAmount, amountToTake);

                        TakeItemAmount(item, amountToTake, collect);
                        totalAmountTaken += amountToTake;
                    }

                    amountToTake = amount - totalAmountTaken;

                    if (amountToTake > 0 && HasSearchableContainer(item, out var childItemList))
                    {
                        totalAmountTaken += TakeItems(childItemList, ref itemQuery, amountToTake, collect);
                    }
                }

                return totalAmountTaken;
            }

            public static int TakePlayerItems(BasePlayer player, ref ItemQuery itemQuery, int amount, List<Item> collect = null)
            {
                var amountTaken = TakeItems(player.inventory.containerMain.itemList, ref itemQuery, amount, collect);
                if (amountTaken >= amount)
                    return amountTaken;

                amountTaken += TakeItems(player.inventory.containerBelt.itemList, ref itemQuery, amount - amountTaken, collect);

                return amountTaken;
            }

            public static bool HasItemMod<T>(ItemDefinition itemDefinition, out T matchingItemMod) where T : ItemMod
            {
                foreach (var itemMod in itemDefinition.itemMods)
                {
                    matchingItemMod = itemMod as T;
                    if ((object)matchingItemMod != null)
                        return true;
                }

                matchingItemMod = null;
                return false;
            }

            public static bool HasItemMod<T>(ItemDefinition itemDefinition) where T : ItemMod
            {
                return HasItemMod(itemDefinition, out T _);
            }

            private static bool HasSearchableContainer(ItemDefinition itemDefinition)
            {
                // Don't consider vanilla containers searchable (i.e., don't take low grade out of a miner's hat).
                return !HasItemMod<ItemModContainer>(itemDefinition);
            }

            private static bool HasSearchableContainer(Item item, out List<Item> itemList)
            {
                itemList = item.contents?.itemList;
                return itemList?.Count > 0 && !item.HasFlag(UnsearchableItemFlag) && HasSearchableContainer(item.info);
            }

            private static void TakeItemAmount(Item item, int amount, List<Item> collect)
            {
                if (amount >= item.amount)
                {
                    item.RemoveFromContainer();
                    if (collect != null)
                    {
                        collect.Add(item);
                    }
                    else
                    {
                        item.Remove();
                    }
                }
                else
                {
                    if (collect != null)
                    {
                        collect.Add(item.SplitItem(amount));
                    }
                    else
                    {
                        item.amount -= amount;
                        item.MarkDirty();
                    }
                }
            }
        }

        #endregion

        #region Pooling

        private static class CustomPool
        {
            public interface IPooled
            {
                void EnterPool();
                void LeavePool();
            }

            private static class StaticPool<T> where T : class, new()
            {
                public static readonly PoolCollection<T> Collection = new();
            }

            private class PoolCollection<T> where T : class, new()
            {
                public const int DefaultPoolSize = 512;

                private T[] _buffer;
                public int ItemsCreated { get; private set; }
                public int ItemsInStack { get; private set; }
                public int ItemsInUse { get; private set; }
                public int ItemsSpilled { get; private set; }
                public int ItemsTaken { get; private set; }

                public PoolCollection()
                {
                    Reset(DefaultPoolSize);
                }

                public void Reset(int size = 0)
                {
                    _buffer = size == 0 ? Array.Empty<T>() : new T[size];

                    ItemsCreated = 0;
                    ItemsInStack = 0;
                    ItemsInUse = 0;
                    ItemsSpilled = 0;
                    ItemsTaken = 0;
                }

                public void Add(T obj)
                {
                    (obj as IPooled)?.EnterPool();

                    ItemsInUse--;

                    if (ItemsInStack >= _buffer.Length)
                    {
                        ItemsSpilled++;
                        return;
                    }

                    _buffer[ItemsInStack] = obj;
                    ItemsInStack++;
                }

                public T Take()
                {
                    if (ItemsInStack > 0)
                    {
                        ItemsInStack--;
                        ItemsInUse++;
                        var obj = _buffer[ItemsInStack];
                        _buffer[ItemsInStack] = null;
                        (obj as IPooled)?.LeavePool();
                        ItemsTaken++;
                        return obj;
                    }

                    ItemsCreated++;
                    ItemsInUse++;
                    return new T();
                }
            }

            public static void Reset<T>(int size) where T : class, new()
            {
                StaticPool<T>.Collection.Reset(size);
            }

            public static string GetStats<T>() where T : class, new()
            {
                var pool = StaticPool<T>.Collection;
                return $"{typeof(T).Name} | {pool.ItemsInUse.ToString()} used of {pool.ItemsCreated.ToString()} created | {pool.ItemsTaken.ToString()} taken";
            }

            public static T Get<T>() where T : class, new()
            {
                return StaticPool<T>.Collection.Take();
            }

            public static List<T> GetList<T>()
            {
                return Get<List<T>>();
            }

            public static void Free<T>(ref T obj) where T : class, new()
            {
                FreeInternal(ref obj);
            }

            public static void FreeList<T>(ref List<T> list) where T : class
            {
                list.Clear();
                FreeInternal(ref list);
            }

            private static void FreeInternal<T>(ref T obj) where T : class, new()
            {
                StaticPool<T>.Collection.Add(obj);
                obj = null;
            }
        }

        private class DisposableList<T> : List<T>, IDisposable
        {
            public static DisposableList<T> Get()
            {
                return CustomPool.Get<DisposableList<T>>();
            }

            public void Dispose()
            {
                Clear();
                var self = this;
                CustomPool.Free(ref self);
            }
        }

        #endregion

        #region Helper Methods - Static

        private static class VanillaMethods
        {
            // Exact copy of vanilla PlayerInventory::CanMoveItemsFrom.
            public static bool CanMoveItemsFrom(PlayerInventory inventory, BaseEntity entity, Item item)
            {
                if (entity is ICanMoveFrom canMoveFrom
                    && !canMoveFrom.CanMoveFrom(inventory.baseEntity, item))
                    return false;

                if ((bool)BaseGameMode.GetActiveGameMode(serverside: true))
                    return BaseGameMode.GetActiveGameMode(serverside: true).CanMoveItemsFrom(inventory, entity, item);

                return true;
            }
        }

        public static void LogInfo(string message) => Interface.Oxide.LogInfo($"[Bag Of Holding] {message}");
        public static void LogError(string message) => Interface.Oxide.LogError($"[Bag Of Holding] {message}");
        public static void LogWarning(string message) => Interface.Oxide.LogWarning($"[Bag Of Holding] {message}");

        #if !CARBON
        private static IList<Plugin> GetHookSubscribers(PluginManager pluginManager, string hookName)
        {
            var hookSubscriptionsField = typeof(PluginManager).GetField("hookSubscriptions", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (hookSubscriptionsField == null)
                return null;

            var hookSubscriptions = hookSubscriptionsField.GetValue(pluginManager) as IDictionary<string, IList<Plugin>>;
            if (hookSubscriptions == null)
                return null;

            return hookSubscriptions.TryGetValue(hookName, out var pluginList)
                ? pluginList
                : null;
        }
        #endif

        private static void SendEffect(BasePlayer player, string effectPrefab)
        {
            if (string.IsNullOrWhiteSpace(effectPrefab))
                return;

            var effect = new Effect(effectPrefab, player, 0, Vector3.zero, Vector3.forward);
            EffectNetwork.Send(effect, player.net.connection);
        }

        private static void ClosePlayerInventory(BasePlayer player)
        {
            player.ClientRPCPlayer(null, player, "OnRespawnInformation");
        }

        private static ItemContainer GetRootContainer(Item item)
        {
            var container = item.parent;
            if (container == null)
                return null;

            while (container.parent?.parent != null && container.parent != item)
            {
                container = container.parent.parent;
            }

            return container;
        }

        private static T[] ParseEnumList<T>(string[] list, string errorFormat) where T : struct
        {
            var valueList = new List<T>(list?.Length ?? 0);

            if (list != null)
            {
                foreach (var itemName in list)
                {
                    if (Enum.TryParse(itemName, ignoreCase: true, result: out T result))
                    {
                        valueList.Add(result);
                    }
                    else
                    {
                        LogError(string.Format(errorFormat, itemName));
                    }
                }
            }

            return valueList.ToArray();
        }

        private static void AddToDictKey<T>(Dictionary<T, int> dict, T key, int amount = 1)
        {
            if (!dict.TryGetValue(key, out var currentAmount))
            {
                currentAmount = 0;
            }

            dict[key] = currentAmount + amount;
        }

        private static bool UserHasPermission(Permission permission, UserData userData, string perm)
        {
            return userData.Perms.Contains(perm)
                || permission.GroupsHavePermission(userData.Groups, perm);
        }

        private static bool IsRealPlayer(BasePlayer player)
        {
            return !player.IsNpc && player.userID.IsSteamId();
        }

        private static bool IsRealPlayerInventory(ItemContainer container, out BasePlayer player)
        {
            player = GetOwnerPlayer(container);
            return (object)player != null && IsRealPlayer(player);
        }

        private static bool TrySetContainerFlag(ItemContainer container, ItemContainer.Flag flag, bool value)
        {
            if (container.HasFlag(flag) == value)
                return false;

            container.SetFlag(flag, value);
            return true;
        }

        private static bool TrySetItemFlag(Item item, Item.Flag flag, bool value)
        {
            if (item.HasFlag(flag) == value)
                return false;

            item.SetFlag(flag, value);
            return true;
        }

        private static bool IsSkinnedBag(Item item)
        {
            return item.skin != 0
                && item.info.itemid == BagItemId;
        }

        private static void StopLooting(BasePlayer player)
        {
            if (player.inventory.loot.IsLooting())
            {
                player.inventory.loot.Clear();
                player.inventory.loot.SendImmediate();
            }
        }

        private static bool IsKeyBindArg(string arg)
        {
            return arg == "True";
        }

        private static bool IsValidArg(string arg)
        {
            return !string.IsNullOrWhiteSpace(arg) && !IsKeyBindArg(arg);
        }

        private static bool CanPlayerMoveItem(BasePlayer player, Item item)
        {
            if (item.IsLocked())
                return false;

            if (!VanillaMethods.CanMoveItemsFrom(player.inventory, GetRootContainer(item).entityOwner, item))
                return false;

            return true;
        }

        private static void AddPlayerContainers(List<ItemContainer> containerList, BasePlayer player)
        {
            containerList.Add(player.inventory.containerWear);
            containerList.Add(player.inventory.containerMain);
            containerList.Add(player.inventory.containerBelt);
        }

        private static BaseEntity CreateLootEntity(ProtectionProperties protectionProperties)
        {
            var lootEntity = GameManager.server.CreateEntity(PrefabStorageBox, new Vector3(0, -500, 0)) as BoxStorage;
            if (lootEntity == null)
                return null;

            UnityEngine.Object.DestroyImmediate(lootEntity.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.DestroyImmediate(lootEntity.GetComponent<GroundWatch>());

            foreach (var collider in lootEntity.GetComponentsInChildren<Collider>())
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            lootEntity.baseProtection = protectionProperties;
            lootEntity.CancelInvoke(lootEntity.DecayTick);
            lootEntity.SetFlag(Flags.Busy, true);
            lootEntity.SetFlag(Flags.Disabled, true);
            lootEntity.SetFlag(Flags.Locked, true);
            lootEntity.EnableGlobalBroadcast(true);
            lootEntity.EnableSaving(false);
            lootEntity.Spawn();

            return lootEntity;
        }

        private static LootableCorpse CreateCorpseEntity(ProtectionProperties protectionProperties)
        {
            var corpseEntity = GameManager.server.CreateEntity(PrefabPlayerCorpse, new Vector3(0, -500, 0)) as LootableCorpse;
            if (corpseEntity == null)
                return null;

            corpseEntity.baseProtection = protectionProperties;
            corpseEntity.syncPosition  = false;
            corpseEntity.containers = Array.Empty<ItemContainer>();
            corpseEntity.SetFlag(Flags.Busy, true);
            corpseEntity.SetFlag(Flags.Disabled, true);
            corpseEntity.EnableGlobalBroadcast(true);
            corpseEntity.EnableSaving(false);
            corpseEntity.Spawn();
            corpseEntity.CancelInvoke(corpseEntity.RemoveCorpse);

            UnityEngine.Object.DestroyImmediate(corpseEntity.GetComponent<Rigidbody>());
            UnityEngine.Object.DestroyImmediate(corpseEntity.GetComponent<Buoyancy>());

            foreach (var collider in corpseEntity.GetComponentsInChildren<Collider>())
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            return corpseEntity;
        }

        private static void StartLootingCorpse(BasePlayer player, LootableCorpse corpse, BasePlayer targetPlayer = null)
        {
            if (player.CanInteract()
                && corpse.CanLoot()
                && corpse.containers != null
                && Interface.CallHook("CanLootEntity", player, corpse) == null
                && player.inventory.loot.StartLootingEntity(corpse))
            {
                corpse.SetFlag(Flags.Open, true);
                if ((object)targetPlayer != null)
                {
                    player.inventory.loot.AddContainer(targetPlayer.inventory.containerMain);
                    player.inventory.loot.AddContainer(targetPlayer.inventory.containerWear);
                    player.inventory.loot.AddContainer(targetPlayer.inventory.containerBelt);
                    player.inventory.loot.PositionChecks = false;
                }
                else
                {
                    foreach (var container in corpse.containers)
                    {
                        player.inventory.loot.AddContainer(container);
                    }
                }
                player.inventory.loot.SendImmediate();
                corpse.ClientRPCPlayer(null, player, "RPC_ClientLootCorpse");
                corpse.SendNetworkUpdate();
            }
        }

        private static void StartLootingDroppedItemContainer(BasePlayer player, DroppedItemContainer containerEntity)
        {
            if (containerEntity.inventory != null
                && player.CanInteract()
                && Interface.CallHook("CanLootEntity", player, containerEntity) == null
                && player.inventory.loot.StartLootingEntity(containerEntity))
            {
                containerEntity.SetFlag(Flags.Open, true);
				player.inventory.loot.AddContainer(containerEntity.inventory);
				player.inventory.loot.SendImmediate();
				player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", containerEntity.lootPanelName);
				containerEntity.SendNetworkUpdate();
            }
        }

        // Multiple plugins assign playerOwner to a container being looted (for no apparent reason), so make sure it's really a player container.
        private static bool IsPlayerContainer(ItemContainer container, BasePlayer player)
        {
            return player.inventory.containerMain == container
                || player.inventory.containerBelt == container
                || player.inventory.containerWear == container;
        }

        private static BasePlayer GetOwnerPlayer(ItemContainer container)
        {
            var playerOwner = container.playerOwner;
            if ((object)playerOwner == null || !IsPlayerContainer(container, playerOwner))
                return null;

            return playerOwner;
        }

        private static BasePlayer GetOwnerPlayer(Item item)
        {
            var container = item.parent;
            if (container == null)
                return null;

            return GetOwnerPlayer(container);
        }

        private static BasePlayer FindPlayer(string userIdString)
        {
            if (!ulong.TryParse(userIdString, out var userId))
                return null;

            return BasePlayer.FindByID(userId) ?? BasePlayer.FindSleeping(userId);
        }

        private static int GetHighestUsedSlot(ProtoBuf.ItemContainer containerData)
        {
            var highestUsedSlot = -1;

            for (var i = 0; i < containerData.contents.Count; i++)
            {
                var item = containerData.contents[i];
                if (item.slot > highestUsedSlot)
                {
                    highestUsedSlot = item.slot;
                }
            }

            return highestUsedSlot;
        }

        #endregion

        #region Helper Methods - Instance

        private bool VerifyHasPermission(IPlayer player, string perm)
        {
            if (player.IsServer
                || player.HasPermission(perm))
                return true;

            ReplyToPlayer(player, LangEntry.ErrorNoPermission);
            return false;
        }

        private bool VerifyIsPlayer(IPlayer player, out BasePlayer basePlayer)
        {
            if (player.IsServer)
            {
                basePlayer = null;
                return false;
            }

            basePlayer = player.Object as BasePlayer;
            return true;
        }

        private bool VerifyTargetPlayer(IPlayer player, string playerNameOrId, out BasePlayer targetPlayer)
        {
            targetPlayer = BasePlayer.Find(playerNameOrId);
            if (targetPlayer != null)
                return true;

            ReplyToPlayer(player, LangEntry.ErrorPlayerNotFound, playerNameOrId);
            return false;
        }

        private bool VerifyBagProfileName(IPlayer player, string bagProfileOrDisplayName, out BagProfile bagProfile)
        {
            bagProfile = _config.GetBagProfileByDisplayName(bagProfileOrDisplayName)
                ?? _config.GetBagProfileByName(bagProfileOrDisplayName);

            if (bagProfile != null)
                return true;

            ReplyToPlayer(player, LangEntry.ErrorBagProfileNotFound, bagProfileOrDisplayName);
            return false;
        }

        private bool IsInsideBag(Item item, out ItemContainer parentContainer)
        {
            parentContainer = item.parent;
            var parentItem = parentContainer?.parent;
            if (parentItem == null)
                return false;

            return _bagManager.EnsureBagInfoIfEligible(parentItem) != null;
        }

        private bool EnforceBagLimitsInContainer(ItemContainer container)
        {
            // Always enforce bag limits inside bags.
            if (container.parent != null && _bagManager.EnsureBagInfoIfEligible(container.parent) != null)
                return true;

            if (container.PlayerItemInputBlocked())
                return false;

            // Loot containers spawn loot prior to setting the NoItemInput flag, so we have to check for this case.
            // TODO: What if a plugin is using a LootContainer for a custom storage box.
            if (container.entityOwner is LootContainer lootContainer && !IsPlayerDeployedLootContainer(lootContainer))
                return false;

            if (container.entityOwner is BaseCorpse)
                return false;

            return true;
        }

        private bool ShouldSubscribeStackHooks()
        {
            return _config.EnableStackProtection
               && _bagItemDefinition.stackable > 1
               && StackModifier == null
               && Loottable == null;
        }

        private void RefreshStackHooks()
        {
            if (ShouldSubscribeStackHooks())
            {
                Subscribe(nameof(CanCombineDroppedItem));
                Subscribe(nameof(OnDroppedItemCombined));

                if (_config.ContainsDuplicateSkins)
                {
                    Subscribe(nameof(OnItemSplit));
                    _subscribedToOnItemSplit = true;
                }
            }
            else
            {
                Unsubscribe(nameof(CanCombineDroppedItem));
                Unsubscribe(nameof(OnDroppedItemCombined));
                Unsubscribe(nameof(OnItemSplit));
                _subscribedToOnItemSplit = false;
            }
        }

        private void RegisterCustomItemsWithLootTable()
        {
            if (Loottable == null)
                return;

            Loottable.Call("ClearCustomItems", this);

            var registeredSkinIds = new HashSet<ulong>();

            foreach (var bagProfile in _config.BagProfiles)
            {
                // Skip kit bags.
                if (!bagProfile.AllowsPlayerItemInput)
                    continue;

                // Skip bags that upgrade to kit bags (i.e., unlocked kit bags).
                var upgradeTarget = bagProfile.UpgradeTarget?.TargetProfile;
                if (upgradeTarget is { AllowsPlayerItemInput: false })
                    continue;

                // Don't register multiple bags with the same skin, since Loottable doesn't support that currently.
                if (registeredSkinIds.Contains(bagProfile.SkinId))
                    continue;

                Loottable.Call("AddCustomItem", this, BagItemId, bagProfile.SkinId, bagProfile.DisplayName);
                registeredSkinIds.Add(bagProfile.SkinId);
            }
        }

        private void TestOnItemSplit()
        {
            #if !CARBON
            var otherSubscribers = GetHookSubscribers(plugins.PluginManager, nameof(OnItemSplit))?
                .Where(subscriber => subscriber.Name != nameof(BagOfHolding))
                .ToArray();

            if (otherSubscribers?.Length == 0)
            {
                // No other subscribers of `OnItemSplit`, so nothing to test.
                return;
            }

            // Prefer a bag profile which has a display name, since some plugins only handle `OnItemSplit` if the item
            // has a display name.
            var bagProfile = _config.BagProfiles.FirstOrDefault(profile => !string.IsNullOrWhiteSpace(profile.DisplayName))
                ?? _config.BagProfiles.FirstOrDefault();

            if (bagProfile == null)
            {
                // No bag profiles, so nothing to test.
                return;
            }

            // Don't create an item larger than the max stack size, since other plugins might have special logic in
            // `OnItemSplit` to reduce the original item to max stack size which could throw off the test.
            var initialStackAmount = _bagItemDefinition.stackable;

            var bagItem = CreateBag(bagProfile, initialStackAmount);
            if (bagItem == null)
                return;

            var onItemSplitHandlers = new List<Plugin>();

            // Copy the list of subscribers, since some plugins might dynamically unsubscribe when called.
            foreach (var subscriber in otherSubscribers.ToArray())
            {
                if (bagItem.removeTime > 0)
                {
                    // Bag item appears to be is scheduled for removal (for unknown reasons), so replace it.
                    bagItem = CreateBag(bagProfile, initialStackAmount);
                }
                else
                {
                    bagItem.amount = initialStackAmount;
                }

                var splitResult = subscriber.Call(nameof(OnItemSplit), bagItem, 1) as Item;
                if (splitResult == null)
                {
                    // Great, that plugin is not handling `OnItemSplit` for bags.
                    continue;
                }

                splitResult.Remove();
                onItemSplitHandlers.Add(subscriber);
            }

            bagItem.Remove();

            var totalHandlers = onItemSplitHandlers.Count + (_subscribedToOnItemSplit ? 1 : 0);
            if (totalHandlers <= 1)
                return;

            // More than one plugin is subscribed to `OnItemSplit`, so log an error.
            var errorMessageSentences = new List<string> { "Plugin conflict detected!" };

            var otherSubscriberNames = otherSubscribers
                .Select(subscriber => subscriber.Name);

            if (_subscribedToOnItemSplit && totalHandlers == 2)
            {
                var otherPluginName = otherSubscriberNames.First();
                errorMessageSentences.Add($"Both \"{nameof(BagOfHolding)}\" and the \"{otherPluginName}\" plugin are handling the \"{nameof(OnItemSplit)}\" hook for bags.");
                errorMessageSentences.Add($"\nHere are several different ways to prevent this conflict.");
                errorMessageSentences.Add($"\n- You: Ensure every bag profile has a unique skin ID.");
                errorMessageSentences.Add($"\n- You: Prevent bags from stacking by setting the max stack size of the \"{_bagItemDefinition.shortname}\" item to 1.");
                errorMessageSentences.Add($"\n- Developer of {otherPluginName}: Update plugin to be less aggressive when handling the \"{nameof(OnItemSplit)}\" hook, if {otherPluginName} is *not* a general purpose stack plugin.");
                errorMessageSentences.Add($"\n- Developer of {nameof(BagOfHolding)}: Update plugin to unsubscribe from the \"{nameof(OnItemSplit)}\" hook while {otherPluginName} is loaded, if {otherPluginName} *is* a general purpose stack plugin.");
            }
            else
            {
                errorMessageSentences.Add($"{onItemSplitHandlers.Count} other plugins are handling the \"{nameof(OnItemSplit)}\" hook for bags: [ {string.Join(", ", otherSubscriberNames)} ].");
                errorMessageSentences.Add($"At least {onItemSplitHandlers.Count - 1} of them must be updated to be less aggressive to prevent this conflict.");

                if (_subscribedToOnItemSplit)
                {
                    errorMessageSentences.Add($"{nameof(BagOfHolding)} is also handling that hook and might need to be updated for compatibility.");
                }

                errorMessageSentences.Add($"To prevent this conflict for now, you can simply prevent bags from stacking by setting the max stack size of the \"{_bagItemDefinition.shortname}\" item to 1.");
            }

            LogError(string.Join(" ", errorMessageSentences));
            #endif
        }

        private bool CanPlayerUpgradeBag(BasePlayer player, BagInfo bagInfo)
        {
            var upgradeTarget = bagInfo.BagProfile.UpgradeTarget;
            if (upgradeTarget == null || upgradeTarget.TargetProfile == null)
                return false;

            var costInfo = upgradeTarget.Cost;
            if (costInfo == null)
                return false;

            var paymentProvider = _paymentProviderResolver.Resolve(costInfo, bagInfo.Item);
            if (!paymentProvider.IsAvailable)
                return false;

            return permission.UserHasPermission(player.UserIDString, upgradeTarget.Permission);
        }

        private void DiscoverBags(Item item)
        {
            if (item == null)
                return;

            var bagInfo = _bagManager.EnsureBagInfoIfEligible(item);
            if (bagInfo == null)
                return;

            _bagItemMod.OnParentChanged(item);
            DiscoverBags(bagInfo.Item.contents);
        }

        private void DiscoverBags(ItemContainer container)
        {
            if (container == null || container.itemList == null)
                return;

            foreach (var childItem in container.itemList)
            {
                DiscoverBags(childItem);
            }
        }

        private void DiscoverBags(IEnumerable<ItemContainer> containerList)
        {
            if (containerList == null)
                return;

            foreach (var container in containerList)
            {
                DiscoverBags(container);
            }
        }

        private BagInfo GetParentBag(Item item)
        {
            var parentItem = item.parentItem;
            if (parentItem == null)
                return null;

            return _bagManager.EnsureBagInfoIfEligible(parentItem);
        }

        private BagInfo FindUnlockedBag(ItemContainer container, string profileName, int start = 0, int end = -1)
        {
            // Optimization: Don't bother searching if we know the container doesn't have any bags.
            var supervisor = _limitManager.GetSupervisor(container);
            if (supervisor == null)
                return null;

            if (end == -1)
            {
                end = container.capacity - 1;
            }

            start = Mathf.Clamp(start, 0, container.capacity - 1);
            end = Mathf.Clamp(end, 0, container.capacity - 1);

            var forward = start <= end;
            var increment = forward ? 1 : -1;

            for (var i = start; (forward) ? (i <= end) : (i >= end); i += increment)
            {
                var item = container.GetSlot(i);
                if (item == null)
                    continue;

                if (item.IsLocked())
                    continue;

                var bagInfo = _bagManager.EnsureBagInfoIfEligible(item);
                if (bagInfo == null)
                    continue;

                if (forward)
                {
                    // Find first bag while traversing downward.
                    if (bagInfo.BagProfile.MatchesNameOrDisplayName(profileName))
                        return bagInfo;

                    var childBagInfo = FindUnlockedBag(bagInfo.Item.contents, profileName, start: 0);
                    if (childBagInfo != null)
                        return childBagInfo;
                }
                else
                {
                    // Find last bag while traversing downward.
                    var childBagInfo = FindUnlockedBag(bagInfo.Item.contents, profileName, start: bagInfo.Item.contents.capacity - 1, end: 0);
                    if (childBagInfo != null)
                        return childBagInfo;

                    if (bagInfo.BagProfile.MatchesNameOrDisplayName(profileName))
                        return bagInfo;
                }
            }

            return null;
        }

        private BagInfo FindUnlockedBag(ItemContainer container, string profileName, bool forward, BagInfo afterBag = null)
        {
            var start = forward
                ? afterBag?.Item.position + 1 ?? 0
                : afterBag?.Item.position - 1 ?? container.capacity - 1;

            var end = forward
                ? container.capacity - 1
                : 0;

            return FindUnlockedBag(container, profileName, start, end);
        }

        private BagInfo FindUnlockedBagInContainers(IList<ItemContainer> containerList, string profileName, bool forward, bool wrapAround = false, BagInfo currentBag = null)
        {
            var start = forward ? 0 : containerList.Count - 1;
            var end = forward ? containerList.Count - 1 : 0;
            var increment = forward ? 1 : -1;

            var startContainer = currentBag?.Item.parent;
            if (startContainer != null)
            {
                var containerIndex = containerList.IndexOf(startContainer);
                if (containerIndex >= 0)
                {
                    var startingAtEndOfContainer = forward
                        ? currentBag.Item.position >= currentBag.Item.parent.capacity - 1
                        : currentBag.Item.position <= 0;

                    // When the starting item is the last in a container, try to start at the next container.
                    if (startingAtEndOfContainer)
                    {
                        currentBag = null;

                        // When the starting container is the last container, wrap around if possible.
                        if (containerIndex == end)
                        {
                            // When wrap around is enabled, while starting at the last item of the last container,
                            // we'll simply start at the beginning (no assignment needed).
                            if (!wrapAround)
                                return null;
                        }
                        else
                        {
                            start = containerIndex + increment;
                        }
                    }
                    else
                    {
                        start = containerIndex;
                    }
                }
            }

            for (var i = start; (forward) ? (i <= end) : (i >= end); i += increment)
            {
                var container = containerList[i];
                var bagInfo = FindUnlockedBag(container, profileName, forward, currentBag);
                if (bagInfo != null)
                    return bagInfo;

                currentBag = null;
            }

            return null;
        }

        private BagInfo FindUnlockedBagAfterBag(BasePlayer player, string profileName, bool forward, bool wrapAround = false, BagInfo currentBag = null)
        {
            var containerList = CustomPool.GetList<ItemContainer>();
            AddPlayerContainers(containerList, player);
            var nextBag = FindUnlockedBagInContainers(containerList, profileName, forward, wrapAround, currentBag);
            CustomPool.FreeList(ref containerList);
            return nextBag;
        }

        private bool OpenFirstBag(BasePlayer player, string profileName, bool addPlayerContainers, bool addLootContainers, bool forward, bool shouldDelay = false)
        {
            var containerList = CustomPool.GetList<ItemContainer>();
            if (addPlayerContainers)
            {
                AddPlayerContainers(containerList, player);
            }

            if (addLootContainers)
            {
                foreach (var container in player.inventory.loot.containers)
                {
                    containerList.Add(container);
                }
            }

            var bagInfo = FindUnlockedBagInContainers(containerList, profileName, forward);
            CustomPool.FreeList(ref containerList);

            if (bagInfo == null)
                return false;

            AttemptOpenBag(player, bagInfo, shouldDelay: shouldDelay);
            return true;
        }

        private Item CreateBag(BagProfile bagProfile, int amount = 1)
        {
            var item = ItemManager.CreateByItemID(BagItemId, amount, bagProfile.SkinId);
            if (item == null)
                return null;

            _bagManager.EnsureBagInfoIfEligible(item).ChangeProfile(this, bagProfile);
            return item;
        }

        private Item CreateKitBag(BagProfile bagProfile, string kitName, BasePlayer initiator = null)
        {
            var kitItems = CreateKitItems(kitName);
            if (kitItems == null)
                return null;

            var bagItem = CreateBag(bagProfile);
            if (bagItem == null)
                return null;

            foreach (var item in kitItems)
            {
                if (item.MoveToContainer(bagItem.contents))
                    continue;

                if ((object)initiator != null)
                {
                    initiator.GiveItem(item);
                }

                item.Remove();
            }

            return bagItem;
        }

        private int SumPlayerItems(BasePlayer player, ref ItemQuery itemQuery)
        {
            return _itemRetrieverAdapter.SumPlayerItems(player, ref itemQuery)
                   ?? ItemUtils.SumPlayerItems(player, ref itemQuery);
        }

        private int TakePlayerItems(BasePlayer player, ref ItemQuery itemQuery, int amount)
        {
            return _itemRetrieverAdapter.TakePlayerItems(player, ref itemQuery, amount)
                   ?? ItemUtils.TakePlayerItems(player, ref itemQuery, amount);
        }

        private struct ItemQuery
        {
            public static ItemQuery FromItem(Item item)
            {
                return new ItemQuery
                {
                    ItemId = item.info.itemid,
                    SkinId = item.skin,
                };
            }

            public int ItemId;
            public ulong? SkinId;
            public Item IgnoreItem;

            public int GetUsableAmount(Item item)
            {
                if (IgnoreItem != null && item == IgnoreItem)
                    return 0;

                if (item.info.itemid != ItemId)
                    return 0;

                if (SkinId.HasValue && SkinId != item.skin)
                    return 0;

                return item.contents?.itemList?.Count > 0
                    ? Math.Max(0, item.amount - 1)
                    : item.amount;
            }
        }

        private bool IsLootingContainer(BasePlayer player, out ContainerInfo info)
        {
            info = default(ContainerInfo);

            var container = player.inventory.loot.containers.FirstOrDefault();
            if (container == null)
                return false;

            info = ContainerInfo.FromContainer(container, _bagManager);
            return true;
        }

        private bool IsLootingBag(BasePlayer player, out BagInfo bagInfo)
        {
            bagInfo = null;

            if (!IsLootingContainer(player, out var containerInfo))
                return false;

            bagInfo = containerInfo.BagInfo;
            return bagInfo != null;
        }

        private bool IsLootingBag(BasePlayer player)
        {
            BagInfo bagInfo;
            return IsLootingBag(player, out bagInfo);
        }

        private void StartLootingEntity(BasePlayer player, BaseEntity targetEntity, bool delay = false, bool doPositionChecks = true)
        {
            if (player == targetEntity)
                return;

            StopLooting(player);

            if (delay)
            {
                // Copy these variables so that the closure object only gets created if this block runs.
                var player2 = player;
                var targetEntity2 = targetEntity;

                timer.Once(0.1f, () => StartLootingEntity(player2, targetEntity2));
                return;
            }

            if (targetEntity == null || targetEntity.IsDestroyed)
                return;

            if (targetEntity is IItemContainerEntity containerEntity)
            {
                containerEntity.PlayerOpenLoot(player, doPositionChecks: doPositionChecks);
                return;
            }

            var playerEntity = targetEntity as BasePlayer;
            if ((object)playerEntity != null)
            {
                if (_sharedCorpseEntity == null || _sharedCorpseEntity.IsDestroyed)
                {
                    LogWarning("Shared lootable corpse entity is missing. Attempting to recreate it.");
                    _sharedCorpseEntity = CreateCorpseEntity(_immortalProtection);

                    if (_sharedCorpseEntity == null && _sharedCorpseEntity.IsDestroyed)
                    {
                        LogError($"Failed to create shared lootable corpse entity. Unable to loot entity: {targetEntity.PrefabName}");
                        return;
                    }
                }

                StartLootingCorpse(player, _sharedCorpseEntity, playerEntity);
                // StartLootingPlayer(player, playerEntity);
                return;
            }

            var corpseEntity = targetEntity as LootableCorpse;
            if (corpseEntity != null)
            {
                StartLootingCorpse(player, corpseEntity);
                return;
            }

            var droppedItemContainer = targetEntity as DroppedItemContainer;
            if ((object)droppedItemContainer != null)
            {
                StartLootingDroppedItemContainer(player, droppedItemContainer);
                return;
            }
        }

        private void OpenBag(BasePlayer player, Item item, BagInfo bagInfo, BaseEntity entitySource, bool delay = false)
        {
            StopLooting(player);

            if (delay)
            {
                // Copy these variables so that the closure object only gets created if this block runs.
                var player2 = player;
                var item2 = item;
                var bagInfo2 = bagInfo;
                var entitySource2 = entitySource;

                timer.Once(0.1f, () => OpenBag(player2, item2, bagInfo2, entitySource2));
                return;
            }

            if (entitySource == null || entitySource.IsDestroyed)
                return;

            if (entitySource is BasePlayer)
            {
                if (_sharedLootEntity == null || _sharedLootEntity.IsDestroyed)
                {
                    LogWarning("Shared lootable entity is missing. Attempting to recreate it.");
                    _sharedLootEntity = CreateLootEntity(_immortalProtection);

                    if (_sharedLootEntity == null || _sharedLootEntity.IsDestroyed)
                    {
                        LogError("Failed to create shared lootable entity. Unable to open bag.");
                        return;
                    }
                }

                entitySource = _sharedLootEntity;
            }

            // When opening a bag inside a container entity, set that entity to Open.
            // This ensures that other players cannot loot the container while the original looter is looking inside a child bag.
            var storageContainer = entitySource as StorageContainer;
            if ((object)storageContainer != null && storageContainer.onlyOneUser)
            {
                storageContainer.SetFlag(Flags.Open, true);
            }

            var containerIOEntity = entitySource as ContainerIOEntity;
            if ((object)containerIOEntity != null && containerIOEntity.onlyOneUser)
            {
                containerIOEntity.SetFlag(Flags.Open, true);
            }

            bagInfo.AddLooter(this, player);
            Interface.CallHook("OnLootEntity", player, entitySource);
            SendEffect(player, _config.Effects.OpenEffect);

            player.inventory.loot.entitySource = entitySource;
            if (entitySource == _sharedLootEntity)
            {
                player.inventory.loot.PositionChecks = false;
            }

            player.inventory.loot.AddContainer(item.contents);
            player.inventory.loot.SendImmediate();
            player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", GenericResizableLootPanelName);
            ExposedHooks.OnBagOpened(item, player);
        }

        private bool ShouldDelayOpening(LootPanelInfo currentInfo, LootPanelInfo nextInfo)
        {
            return nextInfo.Capacity > currentInfo.Capacity
                && currentInfo.PanelName == GenericResizableLootPanelName
                && nextInfo.PanelName == GenericResizableLootPanelName;
        }

        private bool AttemptOpenBag(BasePlayer player, BagInfo bagInfo, BagProfile previousBagProfile = null, bool shouldDelay = false)
        {
            var item = bagInfo.Item;
            if (item.amount <= 0)
            {
                LogError($"Player {player.UserIDString} failed to open {bagInfo.BagProfile?.Name} bag because its stack size is {item.amount}. This should not happen.");
                return false;
            }

            BaseEntity entityOwner = null;

            var rootContainer = GetRootContainer(item);
            if (rootContainer == null)
            {
                entityOwner = item.GetWorldEntity();
                if ((object)entityOwner == null)
                {
                    LogError($"Player {player.UserIDString} failed to open {bagInfo.BagProfile?.Name} bag because it has no root container or world entity.");
                    return false;
                }
            }

            if (item.IsLocked())
                return false;

            if ((object)entityOwner == null)
            {
                if (rootContainer.entityOwner is ShopFront or NPCVendingMachine or MarketTerminal or ReclaimBackpack or ReclaimTerminal)
                    return false;

                entityOwner = rootContainer.entityOwner
                    ?? GetOwnerPlayer(rootContainer)
                    ?? rootContainer.parent?.GetWorldEntity();

                if (entityOwner == null)
                    return false;
            }

            if (!shouldDelay && player.inventory.loot.IsLooting())
            {
                if (player.inventory.loot.containers.Count == 1)
                {
                    var currentContainer = player.inventory.loot.containers.FirstOrDefault();
                    if (currentContainer == item.contents)
                    {
                        player.inventory.loot.Clear();
                        player.inventory.loot.SendImmediate();

                        if (previousBagProfile == null)
                            return false;
                    }

                    shouldDelay = previousBagProfile != null
                        ? ShouldDelayOpening(
                            LootPanelInfo.FromBagProfile(previousBagProfile),
                            LootPanelInfo.FromBagProfile(bagInfo.BagProfile)
                        )
                        : ShouldDelayOpening(
                            LootPanelInfo.FromContainer(currentContainer, _bagManager),
                            LootPanelInfo.FromBagInfo(bagInfo)
                        );
                }
            }

            var hookResult = ExposedHooks.OnBagOpen(item, player);
            if (hookResult is false)
                return false;

            OpenBag(player, item, bagInfo, entityOwner, shouldDelay);
            return true;
        }

        private static int GetLootContainerMaxCapacity(LootContainer lootContainer)
        {
            return lootContainer.panelName switch
            {
                "genericsmall" => 12,
                "generic" => 36,
                "generic_resizable" => 48,
                _ => lootContainer.inventorySlots,
            };
        }

        private static bool IsPlayerDeployedLootContainer(LootContainer lootContainer)
        {
            return lootContainer is Stocking;
        }

        private int HandleLootSpawn(LootContainer lootContainer, Dictionary<BagProfile, float> spawnChanceByProfile)
        {
            var inventory = lootContainer.inventory;
            var maxCapacity = GetLootContainerMaxCapacity(lootContainer);
            if (inventory.itemList.Count >= maxCapacity)
                return 0;

            var amountAdded = 0;

            foreach (var (bagProfile, percentChance) in spawnChanceByProfile)
            {
                if (percentChance > 0 && UnityEngine.Random.Range(0, 100) <= percentChance)
                {
                    var item = CreateBag(bagProfile);
                    if (item == null)
                        continue;

                    if (inventory.itemList.Count == inventory.capacity)
                    {
                        lootContainer.inventory.capacity++;
                    }

                    if (!item.MoveToContainer(lootContainer.inventory))
                    {
                        item.Remove();
                        break;
                    }

                    amountAdded++;

                    if (inventory.itemList.Count >= maxCapacity)
                        break;
                }
            }

            return amountAdded;
        }

        private void ScheduleLootSpawn(LootContainer lootContainer, Dictionary<BagProfile, float> spawnChanceByProfile)
        {
            NextTick(() =>
            {
                if (lootContainer == null || lootContainer.IsDestroyed)
                    return;

                HandleLootSpawn(lootContainer, spawnChanceByProfile);
            });
        }

        private void ResetRemovalTime(DroppedItem droppedItem, bool delayed = false)
        {
            if (delayed)
            {
                NextTick(() =>
                {
                    if (droppedItem == null || droppedItem.IsDestroyed)
                        return;

                    ResetRemovalTime(droppedItem);
                });
            }
            else
            {
                droppedItem.Invoke(droppedItem.IdleDestroy, _bagManager.CalculateDespawnTime(droppedItem.item));
            }
        }

        private bool ShouldDisplayVisualBackpack(BasePlayer player)
        {
            if (!IsRealPlayer(player))
                return false;

            var playerInventory = player.inventory;
            foreach (var item in playerInventory.containerWear.itemList)
            {
                if (item.info.itemid is SmallBackpackItemId or LargeBackpackItemId or ParachuteItemId)
                    return false;
            }

            var supervisor = _limitManager.GetSupervisor(playerInventory.containerWear);
            if (supervisor == null)
                return false;

            foreach (var bagInfo in supervisor.AllBags)
            {
                var bagParent = bagInfo.Item.parent;
                if (bagParent == playerInventory.containerWear)
                {
                    var position = bagInfo.Item.position;
                    if (position == Locker.backpackSlotIndex && _config.VisualBackpack.DisplayWhileInBackpackSlot)
                        return true;

                    if (position != Locker.backpackSlotIndex && _config.VisualBackpack.DisplayWhileInNonBackpackSlot)
                        return true;
                }
            }

            return false;
        }

        private void AddVisualBackpackItemToContainer(ProtoBuf.ItemContainer containerData)
        {
            var firstAvailableInvisibleSlot = Math.Max(containerData.slots, GetHighestUsedSlot(containerData) + 1);
            _visualBackpackItemData.slot = firstAvailableInvisibleSlot;
            containerData.contents.Add(_visualBackpackItemData);
            containerData.slots = firstAvailableInvisibleSlot + 1;
        }

        private bool ShouldDisplayBeltUI(BasePlayer player)
        {
            var beltUISettings = _config.UISettings.BeltButton;
            var playerInventory = player.inventory;

            if (beltUISettings.OnlyShowWhileBagsAreWorn)
            {
                // Get the supervisor associated with the wearable container.
                // This is not necessarily the same as the supervisor for the main container.
                var supervisor = _limitManager.GetSupervisor(playerInventory.containerWear);
                if (supervisor == null)
                    return false;

                // Verify at least one bag is worn, since multiple player containers may share a supervisor.
                foreach (var bagInfo in supervisor.AllBags)
                {
                    if (bagInfo.Item.parent == playerInventory.containerWear)
                        return true;
                }

                return false;
            }

            if (_config.WearableBagLimits.Enabled
                && _limitManager.GetSupervisor(playerInventory.containerWear)?.AllBags.Count > 0)
                return true;

            return _limitManager.GetSupervisor(playerInventory.containerMain)?.AllBags?.Count > 0;
        }

        #endregion

        #region Payment Providers

        private interface IPaymentProvider
        {
            bool IsAvailable { get; }
            int GetBalance(BasePlayer player);
            bool TakeBalance(BasePlayer player, int amount);
        }

        private class ItemsPaymentProvider : IPaymentProvider
        {
            public ItemDefinition ItemDefinition;
            public ulong SkinId;
            public Item IgnoreItem;

            private BagOfHolding _plugin;

            public bool IsAvailable => ItemDefinition != null;

            public ItemsPaymentProvider(BagOfHolding plugin)
            {
                _plugin = plugin;
            }

            public int GetBalance(BasePlayer player)
            {
                var itemQuery = new ItemQuery
                {
                    ItemId = ItemDefinition.itemid,
                    IgnoreItem = IgnoreItem,
                };

                if (SkinId != 0)
                {
                    itemQuery.SkinId = SkinId;
                }

                return _plugin.SumPlayerItems(player, ref itemQuery);
            }

            public bool TakeBalance(BasePlayer player, int amount)
            {
                if (amount <= 0)
                    return true;

                var itemQuery = new ItemQuery
                {
                    ItemId = ItemDefinition.itemid,
                    IgnoreItem = IgnoreItem,
                };

                if (SkinId != 0)
                {
                    itemQuery.SkinId = SkinId;
                }

                _plugin.TakePlayerItems(player, ref itemQuery, amount);
                return true;
            }
        }

        private class EconomicsPaymentProvider : IPaymentProvider
        {
            private BagOfHolding _plugin;
            private Plugin _ownerPlugin => _plugin.Economics;

            public EconomicsPaymentProvider(BagOfHolding plugin)
            {
                _plugin = plugin;
            }

            public bool IsAvailable => _ownerPlugin != null;

            public int GetBalance(BasePlayer player)
            {
                _plugin._objectArray1[0] = ObjectCache.Get(player.userID);
                return Convert.ToInt32(_ownerPlugin.Call("Balance", _plugin._objectArray1));
            }

            public bool TakeBalance(BasePlayer player, int amount)
            {
                _plugin._objectArray2[0] = ObjectCache.Get(player.userID);
                _plugin._objectArray2[1] = ObjectCache.Get(Convert.ToDouble(amount));
                var result = _ownerPlugin.Call("Withdraw", _plugin._objectArray2);
                return result is true;
            }
        }

        private class ServerRewardsPaymentProvider : IPaymentProvider
        {
            private BagOfHolding _plugin;
            private Plugin _ownerPlugin => _plugin.ServerRewards;

            public ServerRewardsPaymentProvider(BagOfHolding plugin)
            {
                _plugin = plugin;
            }

            public bool IsAvailable => _ownerPlugin != null;

            public int GetBalance(BasePlayer player)
            {
                _plugin._objectArray1[0] = ObjectCache.Get(player.userID);
                return Convert.ToInt32(_ownerPlugin.Call("CheckPoints", _plugin._objectArray1));
            }

            public bool TakeBalance(BasePlayer player, int amount)
            {
                _plugin._objectArray2[0] = ObjectCache.Get(player.userID);
                _plugin._objectArray2[1] = ObjectCache.Get(amount);
                var result = _ownerPlugin.Call("TakePoints", _plugin._objectArray2);
                return result is true;
            }
        }

        private interface IPaymentInfo
        {
            bool UseEconomics { get; }
            bool UseServerRewards { get; }
            ItemDefinition ItemDefinition { get; }
            ulong SkinId { get; }
        }

        private class PaymentProviderResolver
        {
            private readonly ItemsPaymentProvider _itemsPaymentProvider;
            private readonly EconomicsPaymentProvider _economicsPaymentProvider;
            private readonly ServerRewardsPaymentProvider _serverRewardsPaymentProvider;

            public PaymentProviderResolver(BagOfHolding plugin)
            {
                _itemsPaymentProvider = new ItemsPaymentProvider(plugin);
                _economicsPaymentProvider = new EconomicsPaymentProvider(plugin);
                _serverRewardsPaymentProvider = new ServerRewardsPaymentProvider(plugin);
            }

            public IPaymentProvider Resolve(IPaymentInfo paymentInfo, Item ignoreItem = null)
            {
                if (paymentInfo.UseEconomics && _economicsPaymentProvider.IsAvailable)
                    return _economicsPaymentProvider;

                if (paymentInfo.UseServerRewards && _serverRewardsPaymentProvider.IsAvailable)
                    return _serverRewardsPaymentProvider;

                _itemsPaymentProvider.ItemDefinition = paymentInfo.ItemDefinition;
                _itemsPaymentProvider.SkinId = paymentInfo.SkinId;
                _itemsPaymentProvider.IgnoreItem = ignoreItem;
                return _itemsPaymentProvider;
            }
        }

        #endregion

        #region Custom Item Manager

        private class ItemModRedirect : ItemMod
        {
            public static void AddToItemDefinition(ItemDefinitionRedirect hostDefinition, ItemDefinition targetDefinition, ulong targetSkinId)
            {
                var itemMod = hostDefinition.gameObject.AddComponent<ItemModRedirect>();
                itemMod._targetDefinition = targetDefinition;
                itemMod._targetSkinId = targetSkinId;
            }

            private ItemDefinition _targetDefinition;
            private ulong _targetSkinId;

            public override void OnItemCreated(Item item)
            {
                if (item.info == _targetDefinition)
                    throw new InvalidOperationException($"Item {item.info.shortname} cannot redirect to itself.");

                item.info = _targetDefinition;
                item.skin = _targetSkinId;
                item.OnItemCreated();
            }
        }

        private class ItemDefinitionRedirect : ItemDefinition
        {
            public static ItemDefinitionRedirect Create(int itemId, string shortName, ItemDefinition targetDefinition, ulong targetSkinId)
            {
                var gameObject = new GameObject();
                var itemDefinition = gameObject.AddComponent<ItemDefinitionRedirect>();

                itemDefinition.itemid = itemId;
                itemDefinition.shortname = shortName;
                itemDefinition.category = ItemCategory.Misc;

                ItemManager.itemDictionary[itemId] = itemDefinition;
                ItemManager.itemDictionaryByName[shortName] = itemDefinition;
                ItemManager.itemList.Add(itemDefinition);

                ItemModRedirect.AddToItemDefinition(itemDefinition, targetDefinition, targetSkinId);
                itemDefinition.itemMods = itemDefinition.GetComponents<ItemMod>();

                return itemDefinition;
            }

            private void OnDestroy()
            {
                ItemManager.itemDictionary.Remove(itemid);
                ItemManager.itemDictionaryByName.Remove(shortname);
                ItemManager.itemList.Remove(this);
            }
        }

        private class CustomItemManager
        {
            private static int GenerateRandomItemId(int tries)
            {
                for (var i = 0; i < tries; i++)
                {
                    var itemId = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                    if (itemId != 0 && !ItemManager.itemDictionary.ContainsKey(itemId))
                        return itemId;
                }

                return 0;
            }

            private List<ItemDefinitionRedirect> _customItemDefinitions = new();

            public CustomItemManager(Configuration config)
            {
                if (!config.EnableItemShortNameRedirects)
                    return;

                var targetDefinition = ItemManager.FindItemDefinition(BagItemId);

                foreach (var bagProfile in config.BagProfiles)
                {
                    var shortName = $"{nameof(BagOfHolding)}.{bagProfile.Name}".ToLower();
                    if (ItemManager.FindItemDefinition(shortName) != null)
                        continue;

                    var tries = 10;
                    var itemId = GenerateRandomItemId(tries);
                    if (itemId == 0)
                    {
                        LogError($"Unable to generate random item id for {shortName} after {tries} tries.");
                        continue;
                    }

                    var customItemDefinition = ItemDefinitionRedirect.Create(itemId, shortName, targetDefinition, bagProfile.SkinId);
                    _customItemDefinitions.Add(customItemDefinition);
                }
            }

            public void Unload()
            {
                for (var i = _customItemDefinitions.Count - 1; i >= 0; i--)
                {
                    var customItemDefinition = _customItemDefinitions[i];
                    if (customItemDefinition != null)
                    {
                        UnityEngine.Object.DestroyImmediate(customItemDefinition.gameObject);
                    }
                }
            }
        }

        #endregion

        #region Dropped Container Watcher

        private class DroppedContainerWatcher : FacepunchBehaviour
        {
            public static DroppedContainerWatcher AddToEntity(BagManager bagManager, DroppedItemContainer droppedItemContainer)
            {
                var component = droppedItemContainer.gameObject.GetComponent<DroppedContainerWatcher>();
                if (component == null)
                {
                    component = droppedItemContainer.gameObject.AddComponent<DroppedContainerWatcher>();
                }

                component._bagManager = bagManager;
                component._droppedItemContainer = droppedItemContainer;
                component.ResetRemovalTime();

                return component;
            }

            private BagManager _bagManager;
            private DroppedItemContainer _droppedItemContainer;

            private void ResetRemovalTime()
            {
                var despawnTime = _bagManager.CalculateDespawnTime(_droppedItemContainer.inventory);
                _droppedItemContainer.Invoke(_droppedItemContainer.RemoveMe, despawnTime);
            }

            private void PlayerStoppedLooting(BasePlayer player)
            {
                ResetRemovalTime();
            }
        }

        #endregion

        #region Limit Management

        [JsonObject(MemberSerialization.OptIn)]
        private class LimitProfile
        {
            [JsonProperty("Permission suffix", DefaultValueHandling = DefaultValueHandling.Ignore, Order = -3)]
            public string PermissionSuffix;

            [JsonProperty("Max total bags", Order = -2)]
            public int MaxTotalBags;

            [JsonProperty("Max bags by category name")]
            public Dictionary<string, int> MaxBagsByCategoryName = new();

            [JsonIgnore]
            public string Permission { get; private set; }

            [JsonIgnore]
            protected virtual string PermissionPrefix { get; }

            [JsonIgnore]
            public Dictionary<int, int> MaxBagsByCategory = new();

            public void Init(BagOfHolding plugin, Configuration config, string permissionInfix = null)
            {
                if (!string.IsNullOrWhiteSpace(PermissionSuffix))
                {
                    var permissionParts = new List<string>
                    {
                        nameof(BagOfHolding),
                        "limit",
                        PermissionPrefix,
                    };

                    if (!string.IsNullOrWhiteSpace(permissionInfix))
                    {
                        permissionParts.Add(permissionInfix);
                    }

                    permissionParts.Add(PermissionSuffix);
                    Permission = string.Join(".", permissionParts).ToLower();

                    plugin.permission.RegisterPermission(Permission, plugin);
                }

                foreach (var entry in MaxBagsByCategoryName)
                {
                    var categoryName = entry.Key;
                    if (string.IsNullOrWhiteSpace(categoryName))
                        continue;

                    var categoryNumber = config.UniqueBagCategories.IndexOf(categoryName);
                    if (categoryNumber == -1)
                    {
                        LogError($"Invalid bag category: \"{categoryName}\"");
                        continue;
                    }

                    MaxBagsByCategory[categoryNumber] = entry.Value;
                }
            }

            public int GetMaxBagsOfCategory(int categoryNumber)
            {
                if (categoryNumber == -1)
                    return -1;

                return MaxBagsByCategory.TryGetValue(categoryNumber, out var limit)
                    ? limit
                    : -1;
            }
        }

        private class CustomLimitProfile : LimitProfile {}

        private class ContainerSupervisor : CustomPool.IPooled
        {
            private struct ContainerWrapper
            {
                public ItemContainer ItemContainer;
                public LimitProfile LimitProfile;

                public ContainerWrapper(ItemContainer container, LimitProfile limitProfile)
                {
                    ItemContainer = container;
                    LimitProfile = limitProfile;
                }

                public bool SetLimitProfile(LimitProfile limitProfile)
                {
                    if (LimitProfile == limitProfile)
                        return false;

                    LimitProfile = limitProfile;
                    return true;
                }
            }

            private List<ContainerWrapper> _containers = new(3);
            public List<BagInfo> AllBags { get; } = new();

            private LimitManager _limitManager;
            private Dictionary<int, List<BagInfo>> _bagsByCategory = new();
            private Action<Item, bool> _handleItemAddedRemoved;
            private int _pauseGatherModeUntilFrame;
            private bool _isGathering;
            private bool _retainSupervisorWhenAllBagsRemoved;
            private DroppedContainerWatcher _droppedContainerWatcher;
            private readonly Comparison<BagInfo> _compareBagsByPosition;

            private BagOfHolding _plugin => _limitManager.Plugin;
            private Configuration _config => _plugin._config;

            public ContainerSupervisor()
            {
                _compareBagsByPosition = CompareBagsByPosition;
            }

            public void Setup(LimitManager limitManager, bool retainSupervisorWhenAllBagsRemoved = false)
            {
                _limitManager = limitManager;
                _retainSupervisorWhenAllBagsRemoved = retainSupervisorWhenAllBagsRemoved;
                PauseGatherModeForOneFrame();
            }

            public void EnterPool()
            {
                #if DEBUG_POOLING
                LogWarning($"EnterPool | {CustomPool.GetStats<ContainerSupervisor>()}");
                #endif

                StopGathering();

                // Remove known references to the supervisor.
                foreach (var bagInfo in AllBags)
                {
                    bagInfo.ParentSupervisor = null;
                }

                foreach (var containerWrapper in _containers)
                {
                    _limitManager.UnregisterContainer(containerWrapper.ItemContainer);
                }

                _containers.Clear();
                AllBags.Clear();
                _limitManager = null;
                _bagsByCategory.Clear();
                _retainSupervisorWhenAllBagsRemoved = false;

                if (_droppedContainerWatcher != null)
                {
                    UnityEngine.Object.Destroy(_droppedContainerWatcher);
                }

                _droppedContainerWatcher = null;

                // Note: _handleItemAddedRemoved is intentionally not reset, to preserve the delegate closure for performance.
            }

            public void LeavePool()
            {
                #if DEBUG_POOLING
                LogWarning($"LeavePool | {CustomPool.GetStats<ContainerSupervisor>()}");
                #endif
            }

            public void AddContainer(ItemContainer container, LimitProfile limitProfile = null)
            {
                if (limitProfile == null)
                {
                    limitProfile = _containers.FirstOrDefault().LimitProfile;

                    if (limitProfile == null)
                    {
                        LogError("Unable to add container without a limit profile");
                        return;
                    }
                }

                _containers.Add(new ContainerWrapper(container, limitProfile));

                if (_isGathering)
                {
                    container.onItemAddedRemoved += _handleItemAddedRemoved;
                }

                if (_containers.Count == 1)
                {
                    // The first time a bag item is added to a dropped item container, reset its removal time.
                    var droppedItemContainer = container.entityOwner as DroppedItemContainer;
                    if ((object)droppedItemContainer != null)
                    {
                        var droppedItemContainer2 = droppedItemContainer;

                        // Delay by 1 second to override Despawn Config.
                        _plugin.timer.Once(1, () =>
                        {
                            if (droppedItemContainer2 == null || droppedItemContainer2.IsDestroyed)
                                return;

                            _droppedContainerWatcher = DroppedContainerWatcher.AddToEntity(_plugin._bagManager, droppedItemContainer2);
                        });
                    }
                }
            }

            public void PruneContainers()
            {
                for (var i = _containers.Count - 1; i >= 0; i--)
                {
                    var containerWrapper = _containers[i];
                    var container = containerWrapper.ItemContainer;
                    if (container.uid.Value != 0)
                        continue;

                    _containers.RemoveAt(i);
                    _limitManager.UnregisterContainer(container);
                }
            }

            public ItemContainer GetFirstContainerWithLimitProfile<T>() where T : LimitProfile
            {
                foreach (var containerWrapper in _containers)
                {
                    if (containerWrapper.LimitProfile is T)
                        return containerWrapper.ItemContainer;
                }

                return null;
            }

            public void ChangeLimitProfile<T>(LimitProfile limitProfile) where T : LimitProfile
            {
                var changed = false;

                foreach (var containerWrapper in _containers)
                {
                    if (containerWrapper.LimitProfile is T)
                    {
                        changed |= containerWrapper.SetLimitProfile(limitProfile);
                    }
                }

                if (changed)
                {
                    RefreshBags(updateIfChanged: true);
                }
            }

            public BagInfo FindBag(ulong itemUID)
            {
                foreach (var bagInfo in AllBags)
                {
                    if (bagInfo.Item.uid.Value == itemUID)
                        return bagInfo;
                }

                return null;
            }

            public void RegisterBag(BagInfo bagInfo, bool refreshBags = true)
            {
                if (AllBags.Contains(bagInfo))
                    return;

                AllBags.Add(bagInfo);
                MaybeShowBeltUI();

                EnsureBagsOfCategoryIfEligible(bagInfo)?.Add(bagInfo);

                if (refreshBags)
                {
                    RefreshBags();
                }

                if (bagInfo.IsGathering)
                {
                    HandleBagGatherToggled(enabled: true);
                }
            }

            public void UnregisterBag(BagInfo bagInfo)
            {
                AllBags.Remove(bagInfo);
                MaybeHideBeltUI();

                GetBagsOfCategory(bagInfo.BagProfile.CategoryNumber)?.Remove(bagInfo);

                if (AllBags.Count == 0)
                {
                    if (!_retainSupervisorWhenAllBagsRemoved)
                    {
                        _limitManager.RemoveSupervisor(this);
                    }

                    return;
                }

                RefreshBags(updateIfChanged: true);

                if (bagInfo.IsGathering)
                {
                    HandleBagGatherToggled(enabled: false);
                }
            }

            public void HandleBagCategoryChanged(BagInfo bagInfo, BagProfile previousBagProfile)
            {
                GetBagsOfCategory(previousBagProfile.CategoryNumber)?.Remove(bagInfo);
                EnsureBagsOfCategoryIfEligible(bagInfo)?.Add(bagInfo);
                RefreshBags(updateIfChanged: true);
            }

            public bool CanChangeBagCategory(BagInfo bagInfo, int categoryNumber)
            {
                if (categoryNumber == -1)
                    return true;

                return CanAddBagOfProfile(GetLimitProfile(bagInfo.Item.parent), categoryNumber);
            }

            public bool CanAddBag(BagInfo bagInfo, ItemContainer container)
            {
                var limitProfile = GetLimitProfile(container);
                if (limitProfile.MaxTotalBags == 0)
                    return false;

                // Allow moving a bag between multiple containers with the same limit profile.
                var bagParent = bagInfo.Item.parent;
                if (bagParent != null
                    && GetLimitProfile(bagParent) == limitProfile)
                    return true;

                // Don't allow the bag if the total bag limit is currently reached.
                if (limitProfile.MaxTotalBags > 0
                    && CountUsedBags(AllBags, limitProfile) >= limitProfile.MaxTotalBags)
                    return false;

                // Allow the bag as long as there is no category-specific limit reached.
                return CanAddBagOfProfile(limitProfile, bagInfo.BagProfile.CategoryNumber);
            }

            public void RefreshBagUIs()
            {
                foreach (var bagInfo in AllBags)
                {
                    bagInfo.RefreshUI(_plugin);
                }
            }

            public void RefreshBags(bool updateIfChanged = false)
            {
                if (AllBags.Count == 0)
                    return;

                foreach (var containerWrapper in _containers)
                {
                    var limitProfile = containerWrapper.LimitProfile;
                    var container = containerWrapper.ItemContainer;

                    var totalBags = CountUsedBags(AllBags, limitProfile);
                    var shouldLockAllBags = limitProfile.MaxTotalBags >= 0 && totalBags >= limitProfile.MaxTotalBags;

                    var changed = false;

                    // First, process bags that have category limits.
                    foreach (var entry in _bagsByCategory)
                    {
                        var categoryNumber = entry.Key;
                        var bagList = entry.Value;

                        var shouldLock = shouldLockAllBags;
                        if (!shouldLock)
                        {
                            var categoryLimit = limitProfile.GetMaxBagsOfCategory(categoryNumber);
                            if (categoryLimit != -1 && CountUsedBags(bagList, limitProfile) >= categoryLimit)
                            {
                                shouldLock = true;
                            }
                        }

                        foreach (var bagInfo in bagList)
                        {
                            if (bagInfo.Item.parent != container
                                || bagInfo.HasItems)
                                continue;

                            changed |= bagInfo.SetLocked(shouldLock);
                        }
                    }

                    // Next, process bags that have no category limits.
                    foreach (var bagInfo in AllBags)
                    {
                        if (bagInfo.Item.parent != container
                            || bagInfo.HasItems)
                            continue;

                        if (limitProfile.GetMaxBagsOfCategory(bagInfo.BagProfile.CategoryNumber) != -1)
                            continue;

                        changed |= bagInfo.SetLocked(shouldLockAllBags);
                    }

                    if (changed)
                    {
                        BasePlayer player;
                        if (updateIfChanged
                            || _containers.Count > 1 && IsRealPlayerInventory(containerWrapper.ItemContainer, out player))
                        {
                            containerWrapper.ItemContainer.MarkDirty();
                        }
                    }
                }

                _plugin._uiUpdateManager.QueueSupervisor(this);
            }

            public void HandleBagChildAddedRemoved()
            {
                _plugin._uiUpdateManager.QueueSupervisor(this);
            }

            public void MaybeShowBeltUI()
            {
                if (!_plugin.IsLoaded
                    || !_config.UISettings.BeltButton.Enabled)
                    return;

                var ownerPlayer = GetRealOwnerPlayer();
                if ((object)ownerPlayer == null)
                    return;

                if (!_plugin.ShouldDisplayBeltUI(ownerPlayer))
                    return;

                _plugin._beltUIRenderer.CreateUIIfMissing(ownerPlayer);
            }

            public void MaybeHideBeltUI()
            {
                if (!_config.UISettings.BeltButton.Enabled)
                    return;

                var ownerPlayer = GetRealOwnerPlayer();
                if ((object)ownerPlayer == null)
                    return;

                if (_plugin.ShouldDisplayBeltUI(ownerPlayer))
                    return;

                _plugin._beltUIRenderer.DestroyUIIfPresent(ownerPlayer);
            }

            public void HandleBagGatherToggled(bool enabled)
            {
                var numGathering = CountGatheringBags();

                if (enabled)
                {
                    if (numGathering == 1)
                    {
                        StartGathering();
                    }
                }
                else if (numGathering == 0)
                {
                    StopGathering();
                }
            }

            public void PauseGatherModeForOneFrame()
            {
                _pauseGatherModeUntilFrame = Time.frameCount + 1;
            }

            private int CompareBagsByPosition(BagInfo a, BagInfo b)
            {
                if (a.Item.parent != b.Item.parent)
                    return GetContainerIndex(a)
                        .CompareTo(GetContainerIndex(b));

                return a.Item.position.CompareTo(b.Item.position);
            }

            public void SortBagsByDisplayPosition(List<BagInfo> bagList)
            {
                bagList.Sort(_compareBagsByPosition);
            }

            private int GetContainerIndex(BagInfo bagInfo)
            {
                var container = bagInfo.Item.parent;

                for (var i = 0; i < _containers.Count; i++)
                {
                    var containerWrapper = _containers[i];
                    if (containerWrapper.ItemContainer == container)
                        return i;
                }

                return -1;
            }

            private bool IsGatherModePaused()
            {
                if (_pauseGatherModeUntilFrame != 0)
                {
                    if (_pauseGatherModeUntilFrame > Time.frameCount)
                        return true;

                    _pauseGatherModeUntilFrame = 0;
                }

                foreach (var bagInfo in AllBags)
                {
                    if (bagInfo.Item.contents == null)
                        continue;

                    var childSupervisor = _limitManager.GetSupervisor(bagInfo.Item.contents);
                    if (childSupervisor == null)
                        continue;

                    if (childSupervisor.IsGatherModePaused())
                        return true;
                }

                return false;
            }

            private bool CanBagGatherFromContainer(BagInfo bagInfo, ItemContainer container, bool allowCrossContainerGather)
            {
                return bagInfo.Parent == container || allowCrossContainerGather;
            }

            private bool HasMatchingItem(BagInfo bagInfo, Item item, ref ItemQuery itemQuery)
            {
                var bagContainer = bagInfo.Item.contents;
                if (bagContainer == null)
                    return false;

                if (ItemUtils.HasMatchingItem(bagContainer.itemList, ref itemQuery))
                    return true;

                var supervisor = _limitManager.GetSupervisor(bagContainer);
                if (supervisor == null)
                    return false;

                // Recursively search child bags to see if any of them could gather the item.
                foreach (var childBagInfo in supervisor.AllBags)
                {
                    if (childBagInfo.GatherMode == GatherMode.None || !childBagInfo.CanGather)
                        continue;

                    var childBagContainer = childBagInfo.Item.contents;
                    if (childBagContainer == null)
                        continue;

                    if (childBagContainer.canAcceptItem != null && !childBagContainer.canAcceptItem(item, -1))
                        continue;

                    if (HasMatchingItem(childBagInfo, item, ref itemQuery))
                        return true;
                }

                return false;
            }

            private bool TryMoveItemToBag(BagInfo bagInfo, Item item, ref ItemQuery itemQuery, bool requireMatchingItem)
            {
                var bagContainer = bagInfo.Item.contents;
                if (bagContainer == null)
                    return false;

                // Optimization: Verify the bag filter permits the item, in order to skip the bag early, since the
                // item.MoveToContainer method is significantly slower.
                if (bagContainer.canAcceptItem != null && !bagContainer.canAcceptItem(item, -1))
                    return false;

                if (requireMatchingItem && !HasMatchingItem(bagInfo, item, ref itemQuery))
                    return false;

                return item.MoveToContainer(bagContainer);
            }

            private bool TryGatherItem(Item item, ref ItemQuery itemQuery, bool allowCrossContainerGather)
            {
                var anyBagsWithGatherAll = false;

                // Use a copy of all bags, in case the item being gathered is a bag.
                using var allBags = DisposableList<BagInfo>.Get();
                allBags.AddRange(AllBags);

                foreach (var bagInfo in allBags)
                {
                    var gatherMode = bagInfo.GatherMode;
                    if (gatherMode == GatherMode.None
                        || !bagInfo.CanGather
                        || !CanBagGatherFromContainer(bagInfo, item.parent, allowCrossContainerGather))
                        continue;

                    if (gatherMode == GatherMode.All)
                    {
                        anyBagsWithGatherAll = true;
                        continue;
                    }

                    if (TryMoveItemToBag(bagInfo, item, ref itemQuery, requireMatchingItem: true))
                        return true;
                }

                if (anyBagsWithGatherAll)
                {
                    foreach (var bagInfo in allBags)
                    {
                        if (bagInfo.GatherMode != GatherMode.All
                            || !bagInfo.CanGather
                            || !CanBagGatherFromContainer(bagInfo, item.parent, allowCrossContainerGather))
                            continue;

                        if (TryMoveItemToBag(bagInfo, item, ref itemQuery, requireMatchingItem: true))
                            return true;
                    }

                    foreach (var bagInfo in allBags)
                    {
                        if (bagInfo.GatherMode != GatherMode.All
                            || !bagInfo.CanGather
                            || !CanBagGatherFromContainer(bagInfo, item.parent, allowCrossContainerGather))
                            continue;

                        if (TryMoveItemToBag(bagInfo, item, ref itemQuery, requireMatchingItem: false))
                            return true;
                    }
                }

                return false;
            }

            private void StartGathering()
            {
                if (_isGathering)
                    return;

                #if DEBUG_GATHER
                LogWarning("StartGathering");
                #endif

                _handleItemAddedRemoved ??= (item, wasAdded) =>
                {
                    if (!wasAdded)
                    {
                        PauseGatherModeForOneFrame();
                        return;
                    }

                    if (IsGatherModePaused())
                        return;

                    var parent = item.parent;
                    if (parent.IsLocked()
                        || parent.PlayerItemInputBlocked()
                        || parent.entityOwner is BaseCorpse)
                        return;

                    var parentRecycler = parent?.entityOwner as Recycler;
                    if ((object)parentRecycler != null
                        && parentRecycler.inventory == parent
                        && item.position < 6)
                        return;

                    var itemQuery = ItemQuery.FromItem(item);

                    var isPlayerInventory = IsRealPlayerInventory(parent, out var player);
                    if (isPlayerInventory)
                    {
                        // Don't gather from the player inventory while the player is looting a network-limited container.
                        var lootingContainer = player.inventory.loot.containers.FirstOrDefault();
                        if (lootingContainer?.entityOwner?.limitNetworking == true)
                            return;

                        // Never gather from the player wearable container.
                        if (parent == player.inventory.containerWear)
                            return;

                        // Don't gather from the player inventory while the player is looting a bag.
                        if (_plugin.IsLootingBag(player))
                            return;

                        itemQuery.IgnoreItem = item;
                        if (ItemUtils.HasMatchingItem(player.inventory.containerMain.itemList, ref itemQuery)
                            || ItemUtils.HasMatchingItem(player.inventory.containerBelt.itemList, ref itemQuery))
                            return;
                    }
                    else
                    {
                        itemQuery.IgnoreItem = item;
                        if (ItemUtils.HasMatchingItem(parent.itemList, ref itemQuery))
                            return;
                    }

                    AllBags.Sort(_compareBagsByPosition);

                    var originalPauseGatherModeUntilFrame = _pauseGatherModeUntilFrame;
                    if (TryGatherItem(item, ref itemQuery, allowCrossContainerGather: isPlayerInventory)
                        && originalPauseGatherModeUntilFrame != _pauseGatherModeUntilFrame)
                    {
                        // Don't pause gather mode due to gathering an item.
                        _pauseGatherModeUntilFrame = 0;
                    }
                };

                foreach (var containerWrapper in _containers)
                {
                    containerWrapper.ItemContainer.onItemAddedRemoved += _handleItemAddedRemoved;
                }

                _isGathering = true;
            }

            private void StopGathering()
            {
                if (!_isGathering)
                    return;

                #if DEBUG_GATHER
                LogWarning("StopGathering");
                #endif

                foreach (var containerWrapper in _containers)
                {
                    containerWrapper.ItemContainer.onItemAddedRemoved -= _handleItemAddedRemoved;
                }

                _isGathering = false;
                _pauseGatherModeUntilFrame = 0;
            }

            private List<BagInfo> GetBagsOfCategory(int categoryNumber)
            {
                if (categoryNumber == -1)
                    return null;

                return _bagsByCategory.TryGetValue(categoryNumber, out var bagList)
                    ? bagList
                    : null;
            }

            private List<BagInfo> EnsureBagsOfCategoryIfEligible(BagInfo bagInfo)
            {
                // Don't bother creating lookups if the specific category has no restrictions, or if the category is not defined.
                if (!HasCategorySpecificLimits(bagInfo))
                    return null;

                var categoryNumber = bagInfo.BagProfile.CategoryNumber;

                var bagList = GetBagsOfCategory(categoryNumber);
                if (bagList == null)
                {
                    bagList = new List<BagInfo>();
                    _bagsByCategory[categoryNumber] = bagList;
                }

                return bagList;
            }

            private bool HasCategorySpecificLimits(BagInfo bagInfo)
            {
                var bagParent = bagInfo.Item.parent;
                if (bagParent == null)
                    return false;

                var limitProfile = GetLimitProfile(bagParent);
                if (limitProfile == null)
                    return false;

                return limitProfile.GetMaxBagsOfCategory(bagInfo.BagProfile.CategoryNumber) != -1;
            }

            private int CountUsedBags(List<BagInfo> bagList, LimitProfile limitProfile)
            {
                var count = 0;
                foreach (var bagInfo in bagList)
                {
                    if (!bagInfo.HasItems)
                        continue;

                    if (GetLimitProfile(bagInfo.Item.parent) != limitProfile)
                        continue;

                    count++;
                }

                return count;
            }

            private bool CanAddBagOfProfile(LimitProfile limitProfile, int categoryNumber)
            {
                var maxBagsOfCategory = limitProfile.GetMaxBagsOfCategory(categoryNumber);
                if (maxBagsOfCategory < 0)
                    return true;

                if (maxBagsOfCategory == 0)
                    return false;

                return CountUsedBagsOfCategory(limitProfile, categoryNumber) < maxBagsOfCategory;
            }

            private int CountUsedBagsOfCategory(LimitProfile limitProfile, int categoryNumber)
            {
                var bagList = GetBagsOfCategory(categoryNumber);
                if (bagList == null)
                    return 0;

                return CountUsedBags(bagList, limitProfile);
            }

            private LimitProfile GetLimitProfile(ItemContainer container)
            {
                foreach (var containerWrapper in _containers)
                {
                    if (containerWrapper.ItemContainer == container)
                        return containerWrapper.LimitProfile;
                }

                return _containers.First().LimitProfile;
            }

            private int CountGatheringBags()
            {
                var count = 0;
                foreach (var bagInfo in AllBags)
                {
                    if (bagInfo.IsGathering)
                    {
                        count++;
                    }
                }

                return count;
            }

            private BasePlayer GetRealOwnerPlayer()
            {
                if (_containers.Count == 0)
                    return null;

                var player = GetOwnerPlayer(_containers.First().ItemContainer);
                if ((object)player == null || !player.userID.IsSteamId() || !player.IsConnected)
                    return null;

                return player;
            }
        }

        private class BackpackComponent : FacepunchBehaviour
        {
            public static BackpackComponent AddToContainer(LimitManager limitManager, ContainerSupervisor supervisor, StorageContainer containerEntity, ulong backpackOwnerId)
            {
                var component = containerEntity.gameObject.AddComponent<BackpackComponent>();
                component.Supervisor = supervisor;
                component.BackpackOwnerId = backpackOwnerId;
                component._limitManager = limitManager;
                return component;
            }

            public ContainerSupervisor Supervisor { get; private set; }
            public ulong BackpackOwnerId { get; private set; }
            private LimitManager _limitManager;

            public void DestroyImmediate()
            {
                DestroyImmediate(this);
            }

            private void OnDestroy()
            {
                _limitManager.RemoveBackpackSupervisor(this);
            }
        }

        private class LimitManager
        {
            public BagOfHolding Plugin;
            private Configuration _config;

            private Dictionary<ItemContainer, ContainerSupervisor> _containerToSupervisor = new();
            private Dictionary<ItemContainer, CustomLimitProfile> _containerToCustomLimitProfile = new();
            private Dictionary<ulong, BackpackComponent> _backpackComponents = new();

            public LimitManager(BagOfHolding plugin, Configuration config)
            {
                Plugin = plugin;
                _config = config;
            }

            public void Unload()
            {
                foreach (var supervisor in _backpackComponents.Values.ToArray())
                {
                    supervisor.DestroyImmediate();
                }
            }

            public LimitProfile GetLimitProfile(ItemContainer container)
            {
                // Custom limit profiles always take priority.
                if (_containerToCustomLimitProfile.TryGetValue(container, out var customLimitProfile))
                    return customLimitProfile;

                // Check if it's a bag next, since entityOwner and playerOwner may be set on bag containers to allow networking of associated entities.
                var parentItem = container.parent;
                if (parentItem != null)
                {
                    var bagChildLimitProfile = Plugin._bagManager.GetBagInfo(parentItem)?.BagProfile.ContentsRuleset?.LimitProfile;
                    if (bagChildLimitProfile != null)
                        return bagChildLimitProfile;
                }

                // Backpacks must be checked before storage entity limits since backpacks use storage entities.
                // If Backpacks returns 0, the container is not a backpack.
                var backpackOwnerId = Plugin._backpacksAdapter.GetBackpackOwnerId(container);
                if (backpackOwnerId != 0)
                    return _config.BackpackBagLimits.GetPlayerLimitProfile(StringCache.Instance.Get(backpackOwnerId));

                #if FEATURE_EXTRA_POCKETS_CONFIG
                // Skill Tree must be checked before storage entity limits since the Extra Pockets pouches use storage entities.
                // If Skill Tree returns null, the container is not an Extra Pockets pouch.
                var extraPocketsOwnerIdString = Plugin._skillTreeAdapter.GetExtraPocketsOwnerIdString(container);
                if (extraPocketsOwnerIdString != null)
                    return _config.ExtraPocketsBagLimits.GetPlayerLimitProfile(extraPocketsOwnerIdString);
                #endif

                var entityOwner = container.entityOwner;
                if ((object)entityOwner != null)
                {
                    var ownerId = entityOwner.OwnerID;

                    if (_config.ContainerBagLimitsByPrefabId.TryGetValue(entityOwner.prefabID, out var containerPrefabBagLimits))
                    {
                        return ownerId != 0
                            ? containerPrefabBagLimits.GetPlayerLimitProfile(StringCache.Instance.Get(ownerId))
                            : containerPrefabBagLimits.DefaultLimitProfile;
                    }

                    return ownerId != 0
                        ? _config.ContainerBagLimits.GetPlayerLimitProfile(StringCache.Instance.Get(ownerId))
                        : _config.ContainerBagLimits.DefaultLimitProfile;
                }

                if (IsRealPlayerInventory(container, out var player))
                {
                    return _config.WearableBagLimits.Enabled && container == player.inventory.containerWear
                        ? _config.WearableBagLimits.GetPlayerLimitProfile(player.UserIDString)
                        : _config.PlayerBagLimits.GetPlayerLimitProfile(player.UserIDString);
                }

                return _config.ContainerBagLimits.DefaultLimitProfile;
            }

            public ContainerSupervisor GetSupervisor(ItemContainer container)
            {
                return _containerToSupervisor.TryGetValue(container, out var supervisor)
                    ? supervisor
                    : GetBackpackSupervisor(container);
            }

            public void HandleBagAddedToContainer(ItemContainer container, BagInfo bagInfo)
            {
                bagInfo.ParentSupervisor?.UnregisterBag(bagInfo);

                var supervisor = GetSupervisor(container) ?? CreateSupervisor(container);
                supervisor.RegisterBag(bagInfo);
                bagInfo.ParentSupervisor = supervisor;
            }

            public void HandleBagRemovedFromContainer(ItemContainer container, BagInfo bagInfo)
            {
                GetSupervisor(container)?.UnregisterBag(bagInfo);
                bagInfo.ParentSupervisor = null;
            }

            public void RemoveSupervisor(ContainerSupervisor supervisor)
            {
                CustomPool.Free(ref supervisor);
            }

            public void SetCustomLimitProfile(ItemContainer container, CustomLimitProfile limitProfile)
            {
                _containerToCustomLimitProfile[container] = limitProfile;

                var supervisor = GetSupervisor(container);
                if (supervisor != null)
                {
                    RemoveSupervisor(supervisor);
                }

                Plugin.DiscoverBags(container);
                container.MarkDirty();
            }

            public void RemoveCustomLimitProfile(ItemContainer container)
            {
                CustomLimitProfile customLimitProfile;
                if (!_containerToCustomLimitProfile.TryGetValue(container, out customLimitProfile))
                    return;

                _containerToCustomLimitProfile.Remove(container);

                var supervisor = GetSupervisor(container);
                if (supervisor != null)
                {
                    RemoveSupervisor(supervisor);
                }

                Plugin.DiscoverBags(container);
                container.MarkDirty();
            }

            public void RefreshLimitProfile<T>(ItemContainer container) where T : LimitProfile
            {
                // Don't reset custom limits. This is intended to be used when player permissions change.
                if (_containerToCustomLimitProfile.ContainsKey(container))
                    return;

                GetSupervisor(container)?.ChangeLimitProfile<T>(GetLimitProfile(container));
            }

            public void RemoveBackpackSupervisor(BackpackComponent component)
            {
                _backpackComponents.Remove(component.BackpackOwnerId);
                RemoveSupervisor(component.Supervisor);
            }

            public void RefreshPlayerLimitProfile(string userIdString)
            {
                var basePlayer = FindPlayer(userIdString);
                if (basePlayer == null)
                    return;

                RefreshLimitProfile<PlayerLimitProfile>(basePlayer.inventory.containerMain);
            }

            public void RefreshWearableLimitProfile(string userIdString)
            {
                var basePlayer = FindPlayer(userIdString);
                if (basePlayer == null)
                    return;

                RefreshLimitProfile<WearableLimitProfile>(basePlayer.inventory.containerWear);
            }

            public void RefreshPlayerLimitProfilesForGroup(string groupName)
            {
                foreach (var supervisor in _containerToSupervisor.Values)
                {
                    var container = supervisor.GetFirstContainerWithLimitProfile<PlayerLimitProfile>();
                    if (container == null)
                        continue;

                    var playerOwner = container?.playerOwner;
                    if ((object)playerOwner == null || !Plugin.permission.UserHasGroup(playerOwner.UserIDString, groupName))
                        continue;

                    RefreshLimitProfile<PlayerLimitProfile>(container);
                }
            }

            public void RefreshWearableLimitProfilesForGroup(string groupName)
            {
                foreach (var supervisor in _containerToSupervisor.Values)
                {
                    var container = supervisor.GetFirstContainerWithLimitProfile<WearableLimitProfile>();
                    if (container == null)
                        continue;

                    var playerOwner = container?.playerOwner;
                    if ((object)playerOwner == null || !Plugin.permission.UserHasGroup(playerOwner.UserIDString, groupName))
                        continue;

                    RefreshLimitProfile<WearableLimitProfile>(container);
                }
            }

            public void RefreshBackpackLimitProfile(string userIdString)
            {
                var basePlayer = FindPlayer(userIdString);
                if (basePlayer == null)
                    return;

                if (!_backpackComponents.TryGetValue(basePlayer.userID, out var component))
                    return;

                var container = component.Supervisor.GetFirstContainerWithLimitProfile<BackpackLimitProfile>();
                if (container == null)
                    return;

                RefreshLimitProfile<BackpackLimitProfile>(container);
            }

            public void RefreshBackpackLimitProfilesForGroup(string groupName)
            {
                foreach (var component in _backpackComponents.Values)
                {
                    if (!Plugin.permission.UserHasGroup(StringCache.Instance.Get(component.BackpackOwnerId), groupName))
                        continue;

                    var container = component.Supervisor.GetFirstContainerWithLimitProfile<BackpackLimitProfile>();
                    if (container == null)
                        continue;

                    RefreshLimitProfile<BackpackLimitProfile>(container);
                }
            }

            public void RefreshContainerLimitProfileForPlayer(string userIdString)
            {
                if (!ulong.TryParse(userIdString, out var userId))
                    return;

                foreach (var supervisor in _containerToSupervisor.Values)
                {
                    var container = supervisor.GetFirstContainerWithLimitProfile<ContainerLimitProfile>();
                    var entityOwner = container?.entityOwner;
                    if ((object)entityOwner == null || entityOwner.OwnerID != userId)
                        continue;

                    RefreshLimitProfile<ContainerLimitProfile>(container);
                }
            }

            public void RefreshContainerLimitProfiles()
            {
                foreach (var supervisor in _containerToSupervisor.Values)
                {
                    var container = supervisor.GetFirstContainerWithLimitProfile<ContainerLimitProfile>();
                    var entityOwner = container?.entityOwner;
                    if ((object)entityOwner == null || entityOwner.OwnerID == 0)
                        continue;

                    RefreshLimitProfile<ContainerLimitProfile>(container);
                }
            }

            private void RegisterContainerSupervisor(ItemContainer container, ContainerSupervisor supervisor, LimitProfile limitProfile = null)
            {
                _containerToSupervisor[container] = supervisor;

                if (limitProfile is CustomLimitProfile customLimitProfile)
                {
                    _containerToCustomLimitProfile[container] = customLimitProfile;
                }
            }

            public void UnregisterContainer(ItemContainer container)
            {
                _containerToSupervisor.Remove(container);
                _containerToCustomLimitProfile.Remove(container);
            }

            private bool IsBackpackAndSupportsPages(ItemContainer container, out ulong backpackOwnerId)
            {
                if (!Plugin._backpacksAdapter.SupportsPages)
                {
                    backpackOwnerId = 0;
                    return false;
                }

                backpackOwnerId = Plugin._backpacksAdapter.GetBackpackOwnerId(container);
                return backpackOwnerId != 0;
            }

            private ContainerSupervisor GetBackpackSupervisor(ItemContainer container)
            {
                if (!IsBackpackAndSupportsPages(container, out var backpackOwnerId))
                    return null;

                if (!_backpackComponents.TryGetValue(backpackOwnerId, out var component))
                    return null;

                var supervisor = component.Supervisor;

                // Prune killed containers (Backpacks may kill pages when shrinking).
                supervisor.PruneContainers();

                // Add new container. If gather mode is enabled, this will add the OnItemAddedRemoved delegate.
                supervisor.AddContainer(container);
                RegisterContainerSupervisor(container, supervisor);

                return supervisor;
            }

            private ContainerSupervisor CreateSupervisor(ItemContainer container)
            {
                var supervisor = CustomPool.Get<ContainerSupervisor>();
                var playerOwner = GetOwnerPlayer(container);

                LimitProfile limitProfile;

                if ((object)playerOwner != null)
                {
                    supervisor.Setup(this);

                    var playerInventory = playerOwner.inventory;
                    limitProfile = GetLimitProfile(playerInventory.containerMain);

                    if (_config.WearableBagLimits.Enabled)
                    {
                        var wearableLimitProfile = GetLimitProfile(playerInventory.containerWear);
                        supervisor.AddContainer(playerInventory.containerWear, wearableLimitProfile);
                        RegisterContainerSupervisor(playerInventory.containerWear, supervisor, wearableLimitProfile);
                    }
                    else
                    {
                        supervisor.AddContainer(playerInventory.containerWear, limitProfile);
                        RegisterContainerSupervisor(playerInventory.containerWear, supervisor, limitProfile);
                    }

                    supervisor.AddContainer(playerInventory.containerMain, limitProfile);
                    RegisterContainerSupervisor(playerInventory.containerMain, supervisor, limitProfile);

                    supervisor.AddContainer(playerInventory.containerBelt, limitProfile);
                    RegisterContainerSupervisor(playerInventory.containerBelt, supervisor, limitProfile);
                }
                else
                {
                    limitProfile = GetLimitProfile(container);

                    var isBackpackContainer = false;

                    if (IsBackpackAndSupportsPages(container, out var backpackOwnerId))
                    {
                        var containerEntity = container.entityOwner as StorageContainer;
                        if (containerEntity != null)
                        {
                            isBackpackContainer = true;
                            _backpackComponents[backpackOwnerId] = BackpackComponent.AddToContainer(this, supervisor, containerEntity, backpackOwnerId);
                        }
                    }

                    supervisor.Setup(this, retainSupervisorWhenAllBagsRemoved: isBackpackContainer);
                    supervisor.AddContainer(container, limitProfile);
                    RegisterContainerSupervisor(container, supervisor, limitProfile);
                }

                return supervisor;
            }
        }

        #endregion

        #region Belt Update Manager

        #if FEATURE_BELT_ACTIVATION
        private class BeltTracker
        {
            private BagOfHolding _plugin;
            private Dictionary<ulong, Timer> _playerTimers = new Dictionary<ulong, Timer>();

            private Configuration _config => _plugin._config;

            public BeltTracker(BagOfHolding plugin)
            {
                _plugin = plugin;
            }

            public void AddPlayer(BasePlayer player, BagInfo bagInfo)
            {
                var originalPosition = bagInfo.Item.position;

                _playerTimers[player.userID] = _plugin.timer.Once(_config.BeltActivation.DelaySeconds, () =>
                {
                    var activeItem = player.GetActiveItem();
                    if (activeItem != null
                        && activeItem == bagInfo.Item
                        && activeItem.position == originalPosition)
                    {
                        _plugin.AttemptOpenBag(player, bagInfo);
                    }

                    _playerTimers.Remove(player.userID);
                });
            }

            public void RemovePlayer(BasePlayer player)
            {
                Timer timer;
                if (_playerTimers.TryGetValue(player.userID, out timer))
                {
                    timer.Destroy();
                    _playerTimers.Remove(player.userID);
                }
            }
        }
        #endif

        #endregion

        #region UI Builder

        private class UIBuilder
        {
            private StringBuilder _sb;
            private const char Delimiter = ',';

            private bool _needsDelimiter;

            public UIBuilder(int capacity)
            {
                _sb = new StringBuilder(capacity);
            }

            public void StartObject()
            {
                AddDelimiterIfNeeded();

                _sb.Append('{');
                _needsDelimiter = false;
            }

            public void EndObject()
            {
                _sb.Append('}');
                _needsDelimiter = true;
            }

            public void StartArray()
            {
                _sb.Append('[');
                _needsDelimiter = false;
            }

            public void EndArray()
            {
                _sb.Append(']');
                _needsDelimiter = true;
            }

            public UIBuilder Start()
            {
                _sb.Clear();
                StartArray();
                return this;
            }

            public string Finalize()
            {
                EndArray();
                var result = _sb.ToString();
                _sb.Clear();
                return result;
            }

            public void AddLiteral(string key, object value)
            {
                if (value == null)
                    return;

                AddKey(key);
                _sb.Append(value);
                _needsDelimiter = true;
            }

            public void AddString(string key, string value)
            {
                if (value == null)
                    return;

                AddKey(key);
                _sb.Append('"');
                _sb.Append(value);
                _sb.Append('"');
                _needsDelimiter = true;
            }

            public void AddKey(string key)
            {
                AddDelimiterIfNeeded();

                _sb.Append('"');
                _sb.Append(key);
                _sb.Append('"');
                _sb.Append(':');
            }

            public UIBuilder WithElement(UIElement element)
            {
                element.AddToBuilder(this);
                return this;
            }

            private void AddDelimiter()
            {
                _sb.Append(Delimiter);
            }

            private void AddDelimiterIfNeeded()
            {
                if (_needsDelimiter)
                {
                    AddDelimiter();
                }
            }
        }

        private interface IUIComponent
        {
            void AddToBuilder(UIBuilder builder);
        }

        private class UIButtonComponent : IUIComponent
        {
            private const string Type = "UnityEngine.UI.Button";

            private const string DefaultCommand = null;
            private const string DefaultClose = null;
            private const string DefaultSprite = "Assets/Content/UI/UI.Background.Tile.psd";
            private const string DefaultMaterial = "Assets/Icons/IconMaterial.mat";
            private const string DefaultColor = "1 1 1 1";
            private const Image.Type DefaultImageType = Image.Type.Simple;
            private const float DefaultFadeIn = 0;

            private static UIButtonComponent _instance = new();
            private static bool _inUse;

            public static UIButtonComponent With(
                string command = DefaultCommand,
                string close = DefaultClose,
                string sprite = DefaultSprite,
                string material = DefaultMaterial,
                string color = DefaultColor,
                Image.Type imageType = DefaultImageType,
                float fadeIn = DefaultFadeIn)
            {
                if (_inUse)
                {
                    throw new InvalidOperationException("UIButtonComponent instance is in-use.");
                }

                _inUse = true;

                _instance._command = command;
                _instance._close = close;
                _instance._sprite = sprite;
                _instance._material = material;
                _instance._color = color;
                _instance._imageType = imageType;
                _instance._fadeIn = fadeIn;

                return _instance;
            }

            private string _command;
            private string _close;
            private string _sprite;
            private string _material;
            private string _color;
            private Image.Type _imageType;
            private float _fadeIn;

            public void AddToBuilder(UIBuilder builder)
            {
                builder.StartObject();
                builder.AddString("type", Type);
                if (_command != DefaultCommand)
                {
                    builder.AddString("command", _command);
                }
                if (_close != DefaultClose)
                {
                    builder.AddString("close", _close);
                }
                if (_sprite != DefaultSprite)
                {
                    builder.AddString("sprite", _sprite);
                }
                if (_material != DefaultMaterial)
                {
                    builder.AddString("material", _material);
                }
                if (_color != DefaultColor)
                {
                    builder.AddString("color", _color);
                }
                if (_imageType != DefaultImageType)
                {
                    builder.AddString("imagetype", _imageType.ToString());
                }
                if (_fadeIn != DefaultFadeIn)
                {
                    builder.AddLiteral("fadeIn", _fadeIn);
                }
                builder.EndObject();

                _inUse = false;
            }
        }

        private class UIImageComponent : IUIComponent
        {
            private const string Type = "UnityEngine.UI.Image";

            private const string DefaultSprite = "Assets/Content/UI/UI.Background.Tile.psd";
            private const string DefaultMaterial = "Assets/Icons/IconMaterial.mat";
            private const string DefaultColor = "1 1 1 1";
            private const Image.Type DefaultImageType = Image.Type.Simple;
            private const string DefaultPng = null;
            private const int DefaultItemId = 0;
            private const ulong DefaultSkinId = 0;
            private const float DefaultFadeIn = 0;

            private static UIImageComponent _instance = new();
            private static bool _inUse;

            public static UIImageComponent With(
                string sprite = DefaultSprite,
                string material = DefaultMaterial,
                string color = DefaultColor,
                Image.Type imageType = DefaultImageType,
                string png = DefaultPng,
                int itemId = DefaultItemId,
                ulong skinId = DefaultSkinId,
                float fadeIn = DefaultFadeIn)
            {
                if (_inUse)
                {
                    throw new InvalidOperationException("UIImageComponent instance is in-use.");
                }

                _inUse = true;

                _instance._sprite = sprite;
                _instance._material = material;
                _instance._color = color;
                _instance._imageType = imageType;
                _instance._png = png;
                _instance._itemId = itemId;
                _instance._skinId = skinId;
                _instance._fadeIn = fadeIn;

                return _instance;
            }

            private string _sprite;
            private string _material;
            private string _color;
            private Image.Type _imageType;
            private string _png;
            private int _itemId;
            private ulong _skinId;
            private float _fadeIn;

            public void AddToBuilder(UIBuilder builder)
            {
                builder.StartObject();
                builder.AddString("type", Type);
                if (_sprite != DefaultSprite)
                {
                    builder.AddString("sprite", _sprite);
                }
                if (_material != DefaultMaterial)
                {
                    builder.AddString("material", _material);
                }
                if (_color != DefaultColor)
                {
                    builder.AddString("color", _color);
                }
                if (_imageType != DefaultImageType)
                {
                    builder.AddString("imagetype", _imageType.ToString());
                }
                if (_png != DefaultPng)
                {
                    builder.AddString("png", _png);
                }
                if (_itemId != DefaultItemId)
                {
                    builder.AddLiteral("itemid", _itemId);
                }
                if (_skinId != DefaultSkinId)
                {
                    builder.AddLiteral("skinid", _skinId);
                }
                if (_fadeIn != DefaultFadeIn)
                {
                    builder.AddLiteral("fadeIn", _fadeIn);
                }
                builder.EndObject();

                _inUse = false;
            }
        }

        private class UIRawImageComponent : IUIComponent
        {
            private const string Type = "UnityEngine.UI.RawImage";

            private const string DefaultSprite = "Assets/Icons/rust.png";
            private const string DefaultColor = "1 1 1 1";
            private const string DefaultMaterial = null;
            private const string DefaultUrl = null;
            private const string DefaultPng = null;
            private const float DefaultFadeIn = 0;

            private static UIRawImageComponent _instance = new();
            private static bool _inUse;

            public static UIRawImageComponent With(
                string sprite = DefaultSprite,
                string color = DefaultColor,
                string material = DefaultMaterial,
                string url = DefaultUrl,
                string png = DefaultPng,
                float fadeIn = DefaultFadeIn)
            {
                if (_inUse)
                {
                    throw new InvalidOperationException("UIRawImageComponent instance is in-use.");
                }

                _inUse = true;

                _instance._sprite = sprite;
                _instance._color = color;
                _instance._material = material;
                _instance._url = url;
                _instance._png = png;
                _instance._fadeIn = fadeIn;

                return _instance;
            }

            private string _sprite;
            private string _color;
            private string _material;
            private string _url;
            private string _png;
            private float _fadeIn;

            public void AddToBuilder(UIBuilder builder)
            {
                builder.StartObject();
                builder.AddString("type", Type);
                if (_sprite != DefaultSprite)
                {
                    builder.AddString("sprite", _sprite);
                }
                if (_color != DefaultColor)
                {
                    builder.AddString("color", _color);
                }
                if (_material != DefaultMaterial)
                {
                    builder.AddString("material", _material);
                }
                if (_url != DefaultUrl)
                {
                    builder.AddString("url", _url);
                }
                if (_png != DefaultPng)
                {
                    builder.AddString("png", _png);
                }
                if (_fadeIn != DefaultFadeIn)
                {
                    builder.AddLiteral("fadeIn", _fadeIn);
                }
                builder.EndObject();

                _inUse = false;
            }
        }

        private class UIRectTransformComponent : IUIComponent
        {
            private const string Type = "RectTransform";

            private const string DefaultAnchorMin = "0.0 0.0";
            private const string DefaultAnchorMax = "1.0 1.0";
            private const string DefaultOffsetMin = "0.0 0.0";
            private const string DefaultOffsetMax = "1.0 1.0";

            private static UIRectTransformComponent _instance = new();
            private static bool _inUse;

            public static UIRectTransformComponent With(
                string anchorMin = DefaultAnchorMin,
                string anchorMax = DefaultAnchorMax,
                string offsetMin = DefaultOffsetMin,
                string offsetMax = DefaultOffsetMax)
            {
                if (_inUse)
                {
                    throw new InvalidOperationException("UIRectTransformComponent instance is in-use.");
                }

                _inUse = true;

                _instance._anchorMin = anchorMin;
                _instance._anchorMax = anchorMax;
                _instance._offsetMin = offsetMin;
                _instance._offsetMax = offsetMax;

                return _instance;
            }

            private string _anchorMin;
            private string _anchorMax;
            private string _offsetMin;
            private string _offsetMax;

            public void AddToBuilder(
                UIBuilder builder)
            {
                builder.StartObject();
                builder.AddString("type", Type);
                if (_anchorMin != DefaultAnchorMin)
                {
                    builder.AddString("anchormin", _anchorMin);
                }
                if (_anchorMax != DefaultAnchorMax)
                {
                    builder.AddString("anchormax", _anchorMax);
                }
                if (_offsetMin != DefaultOffsetMin)
                {
                    builder.AddString("offsetmin", _offsetMin);
                }
                if (_offsetMax != DefaultOffsetMax)
                {
                    builder.AddString("offsetmax", _offsetMax);
                }
                builder.EndObject();

                _inUse = false;
            }
        }

        private class UITextComponent : IUIComponent
        {
            private static UITextComponent _instance = new();
            private static bool _inUse;

            public static UITextComponent With(
                string text = DefaultText,
                int fontSize = DefaultFontSize,
                string font = DefaultFont,
                TextAnchor textAlign = DefaultTextAlign,
                string color = DefaultColor,
                VerticalWrapMode verticalWrapMode = DefaultVerticalWrapMode,
                float fadeIn = DefaultFadeIn)
            {
                if (_inUse)
                {
                    throw new InvalidOperationException("UITextComponent instance is in-use.");
                }

                _inUse = true;

                _instance._text = text;
                _instance._fontSize = fontSize;
                _instance._font = font;
                _instance._textAlign = textAlign;
                _instance._color = color;
                _instance._verticalWrapMode = verticalWrapMode;
                _instance._fadeIn = fadeIn;

                return _instance;
            }

            private const string Type = "UnityEngine.UI.Text";

            private const string DefaultText = "Text";
            private const int DefaultFontSize = 14;
            private const string DefaultFont = "RobotoCondensed-Bold.ttf";
            private const TextAnchor DefaultTextAlign = TextAnchor.UpperLeft;
            private const string DefaultColor = "1 1 1 1";
            private const VerticalWrapMode DefaultVerticalWrapMode = VerticalWrapMode.Truncate;
            private const float DefaultFadeIn = 0;

            private string _text;
            private int _fontSize;
            private string _font;
            private TextAnchor _textAlign;
            private string _color;
            private VerticalWrapMode _verticalWrapMode;
            private float _fadeIn;

            public void AddToBuilder(UIBuilder builder)
            {
                builder.StartObject();
                builder.AddString("type", Type);
                if (_text != DefaultText)
                {
                    builder.AddString("text", _text);
                }
                if (_fontSize != DefaultFontSize)
                {
                    builder.AddLiteral("fontSize", _fontSize);
                }
                if (_font != DefaultFont)
                {
                    builder.AddString("font", _font);
                }
                if (_textAlign != DefaultTextAlign)
                {
                    builder.AddString("align", _textAlign.ToString());
                }
                if (_color != DefaultColor)
                {
                    builder.AddString("color", _color);
                }
                if (_verticalWrapMode != DefaultVerticalWrapMode)
                {
                    builder.AddString("verticalOverflow", _verticalWrapMode.ToString());
                }
                if (_fadeIn != DefaultFadeIn)
                {
                    builder.AddLiteral("fadeIn", _fadeIn);
                }
                builder.EndObject();

                _inUse = false;
            }
        }

        private class UIElement
        {
            private static UIElement _instance = new();
            private static bool _inUse;

            public static UIElement With(string name = null, string parent = "Overlay", string destroyName = null, float fadeOut = 0)
            {
                if (_inUse)
                {
                    throw new InvalidOperationException("UIElement instance is in-use.");
                }

                _inUse = true;
                _instance._components.Clear();

                _instance._name = name;
                _instance._parentName = parent;
                _instance._destroyName = destroyName;
                _instance._fadeOut = fadeOut;
                return _instance;
            }

            private string _name;
            private string _parentName;
            private string _destroyName;
            private float _fadeOut;
            private List<IUIComponent> _components = new(3);

            private UIElement() {}

            public UIElement WithComponent(IUIComponent component)
            {
                _components.Add(component);
                return this;
            }

            public void AddToBuilder(UIBuilder builder)
            {
                builder.StartObject();
                builder.AddString("name", _name);
                builder.AddString("parent", _parentName);
                builder.AddString("destroyUi", _destroyName);
                builder.AddKey("components");
                builder.StartArray();
                foreach (var component in _components)
                {
                    component.AddToBuilder(builder);
                }
                builder.EndArray();
                builder.EndObject();

                _components.Clear();
                _inUse = false;
            }
        }

        #endregion

        #region UI Update Manager

        // This allows throttling UI updates when multiple items are added to or removed from a container in quick succession.
        // Since this is a global update manager, one player's activity can delay processing of another player's activity.
        private class UIUpdateManager
        {
            private const float FrequencySeconds = 1f;

            private BagOfHolding _plugin;
            private HashSet<ContainerSupervisor> _queuedSupervisors = new();
            private Queue<ContainerSupervisor> _queue = new();
            private Action _processQueue;
            private Timer _timer;

            public UIUpdateManager(BagOfHolding plugin)
            {
                _plugin = plugin;
                _processQueue = ProcessQueue;
            }

            public void QueueSupervisor(ContainerSupervisor supervisor)
            {
                if (!_queuedSupervisors.Add(supervisor))
                    return;

                _queue.Enqueue(supervisor);

                if (_timer == null || _timer.Destroyed)
                {
                    _timer = _plugin.timer.Every(FrequencySeconds, _processQueue);
                }
            }

            private void ProcessQueue()
            {
                if (_queue.TryDequeue(out var supervisor))
                {
                    _queuedSupervisors.Remove(supervisor);
                    supervisor.RefreshBagUIs();
                }

                if (_queue.Count == 0)
                {
                    _timer?.Destroy();
                    _timer = null;
                }
            }
        }

        #endregion

        #region UI

        private static class UIConstants
        {
            public const string GreenButtonColor = "0.451 0.553 0.271 1";
            public const string GreenButtonTextColor = "0.659 0.918 0.2 1";

            public const string HeaderBackgroundColor = "0.4 0.4 0.4 1";

            // 247f / 255f, 47f / 51f, 0.882352948f, 3f / 85f
            public const string ItemBackgroundColor = "0.969 0.922 0.882 0.035";

            // 31f / 255f, 107f / 255f, 32f / 51f, 40f / 51f
            public const string ItemBackgroundColorSelected = "0.122 0.42 0.627 0.784";

            public const float PanelWidth = 380f;
            public const float HeaderHeight = 23;
            public const float ItemSpacing = 4;
            public const float ItemBoxSize = 58;

            public const float ItemSpaceFraction = (ItemBoxSize + ItemSpacing) / ItemBoxSize;
            public const float ContainerPadding = 6;

            public const string TexturedBackgroundSprite = "assets/content/ui/ui.background.tiletex.psd";

            public const string AnchorMin = "0.5 0";
            public const string AnchorMax = "0.5 0";
        }

        private static void AddItemBackground(UIBuilder builder, string parentName, float offsetX, float offsetY, float size, bool isActive = false)
        {
            builder.WithElement(
                UIElement.With(parent: parentName)
                    .WithComponent(UIRawImageComponent.With(
                        sprite: UIConstants.TexturedBackgroundSprite,
                        color: isActive ? UIConstants.ItemBackgroundColorSelected : UIConstants.ItemBackgroundColor
                    ))
                    .WithComponent(UIRectTransformComponent.With(
                        anchorMin: "0 0",
                        anchorMax: "0 0",
                        offsetMin: $"{offsetX} {offsetY}",
                        offsetMax: $"{offsetX + size} {offsetY + size}"
                    ))
            );
        }

        private static void AddItemIcon(UIBuilder builder, string parentName, int itemId, ulong skinId, float offsetX, float offsetY, float size, bool isActive = false, bool isGathering = false, float fullFraction = 0)
        {
            AddItemBackground(builder, parentName, offsetX, offsetY, size, isActive);

            var imagePadding = size * (UIConstants.ItemSpaceFraction - 1);

            if (fullFraction > 0)
            {
                builder.WithElement(
                    UIElement.With(parent: parentName)
                        .WithComponent(UIRawImageComponent.With(
                            sprite: UIConstants.TexturedBackgroundSprite,
                            color: "0.792 0.459 0.251 1"
                        ))
                        .WithComponent(UIRectTransformComponent.With(
                            anchorMin: "0 0",
                            anchorMax: "0 0",
                            offsetMin: $"{offsetX} {offsetY}",
                            offsetMax: $"{offsetX + imagePadding} {offsetY + fullFraction * size}"
                        ))
                );
            }

            // Image
            builder.WithElement(
                UIElement.With(parent: parentName)
                    .WithComponent(UIImageComponent.With(
                        color: "1 1 1 1",
                        itemId: itemId,
                        skinId: skinId
                    ))
                    .WithComponent(UIRectTransformComponent.With(
                        anchorMin: "0 0",
                        anchorMax: "0 0",
                        offsetMin: $"{offsetX + imagePadding} {offsetY + imagePadding}",
                        offsetMax: $"{offsetX + size - imagePadding} {offsetY + size - imagePadding}"
                    ))
            );

            if (isGathering || fullFraction > 0)
            {
                var cornerIconSize = size / 1.75f;
                var cornerIconOffsetX = 3;
                var cornerIconOffsetY = -1;

                builder.WithElement(
                    UIElement.With(parent: parentName)
                        .WithComponent(UIImageComponent.With(
                            color: isGathering ? "1 0.5 0.25 1" : "0.863 0.863 0.863 1",
                            sprite: "assets/icons/isonfire.png",
                            material: "assets/icons/greyout.mat"
                        ))
                        .WithComponent(UIRectTransformComponent.With(
                            anchorMin: "0 0",
                            anchorMax: "0 0",
                            offsetMin: $"{offsetX + cornerIconOffsetX + imagePadding + size - cornerIconSize} {offsetY + cornerIconOffsetY + imagePadding + size - cornerIconSize}",
                            offsetMax: $"{offsetX + cornerIconOffsetX + size - imagePadding} {offsetY + cornerIconOffsetY + size - imagePadding}"
                        ))
                );
            }
        }

        private static void AddBagIcon(UIBuilder builder, string parentName, BagInfo bagInfo, float offsetX, float offsetY, float iconSize, string command, bool isActive = false, bool isGathering = false, bool showCapacity = false)
        {
            var fullFraction = showCapacity
                ? bagInfo.Item.contents.itemList.Count / (float)bagInfo.Item.contents.capacity
                : 0;

            AddItemIcon(builder, parentName, SaddleBagItemId, bagInfo.BagProfile.SkinId, offsetX, offsetY, iconSize, isActive, isGathering, fullFraction);

            builder.WithElement(
                UIElement.With(parent: parentName)
                    .WithComponent(UIButtonComponent.With(
                        command: command,
                        color: "1 1 1 0"
                    ))
                    .WithComponent(UIRectTransformComponent.With(
                        anchorMin: "0 0",
                        anchorMax: "0 0",
                        offsetMin: $"{offsetX} {offsetY}",
                        offsetMax: $"{offsetX + iconSize} {offsetY + iconSize}"
                    ))
            );
        }

        private class ContainerUIRenderer
        {
            public const string UIName = "BagOfHolding.ContainerUI";
            public const string HeaderUIName = "BagOfHolding.ContainerUI.Header";

            public static string RenderContainerUI(BagOfHolding plugin, BasePlayer player, BagInfo bagInfo, bool showUpgradeInfo = false)
            {
                var builder = plugin._containerUIBuilder;
                var uiSettings = plugin._config.UISettings.LootInterface;

                var showBagSelector = uiSettings.BagSelector.Enabled
                    && !showUpgradeInfo;

                var showBackButton = uiSettings.EnableBackButton
                    && bagInfo.Parent != null
                    && !IsPlayerContainer(bagInfo.Parent, player);

                var showUpgradeButton = plugin.CanPlayerUpgradeBag(player, bagInfo);
                var showGatherButton = plugin._config.GatherMode.Enabled && bagInfo.CanGather;
                var showHeaderBar = showBackButton || showUpgradeButton || showGatherButton;

                if (!showHeaderBar && !showBagSelector)
                    return null;

                var numRows = 1 + (bagInfo.Item.contents.capacity - 1) / 6;

                var offsetX = 192.5f;
                var offsetY = 113.5f + numRows * (UIConstants.ItemBoxSize + UIConstants.ItemSpacing);

                builder.Start();

                builder.WithElement(
                    UIElement.With(name: UIName, parent: "Hud.Menu", destroyName: UIName)
                        .WithComponent(UIImageComponent.With())
                        .WithComponent(UIRectTransformComponent.With(
                            anchorMin: UIConstants.AnchorMin,
                            anchorMax: UIConstants.AnchorMax,
                            offsetMin: $"{offsetX} {offsetY}",
                            offsetMax: $"{offsetX} {offsetY}"
                        ))
                );

                if (showHeaderBar)
                {
                    builder.WithElement(
                        UIElement.With(name: HeaderUIName, parent: UIName)
                            .WithComponent(UIRawImageComponent.With(
                                color: UIConstants.HeaderBackgroundColor,
                                sprite: UIConstants.TexturedBackgroundSprite
                            ))
                            .WithComponent(UIRectTransformComponent.With(
                                anchorMin: UIConstants.AnchorMin,
                                anchorMax: UIConstants.AnchorMax,
                                offsetMin: "0 0",
                                offsetMax: $"{UIConstants.PanelWidth} {UIConstants.HeaderHeight}"
                            ))
                    );
                }

                var buttonOffsetX = 0;

                if (showBackButton)
                {
                    AddButton(builder, UIConstants.GreenButtonColor, UIConstants.GreenButtonTextColor, plugin.GetMessage(player.UserIDString, LangEntry.UIBack), "bag.parent", 0, 75);
                    buttonOffsetX += 80;
                }

                if (showUpgradeButton)
                {
                    AddButton(builder, "0.25 0.5 0.75 1", "0.75 0.85 1 1", plugin.GetMessage(player.UserIDString, LangEntry.UIUpgrade), "bag.ui.upgrade", buttonOffsetX, 75);
                    buttonOffsetX += 80;
                }

                if (showGatherButton)
                {
                    var gatherMode = bagInfo.GatherMode;
                    var gatherLangKey = gatherMode == GatherMode.All
                        ? LangEntry.UIGatherAll
                        : gatherMode == GatherMode.Existing
                            ? LangEntry.UIGatherExisting
                            : LangEntry.UIGatherOff;
                    var message = plugin.GetMessage(player.UserIDString, gatherLangKey);
                    AddButton(builder, "0.788 0.459 0.243 1", "0.984 0.816 0.714 1", message, "bag.ui.togglegather", buttonOffsetX, 100);
                }

                if (bagInfo.Parent != null)
                {
                    var supervisor = plugin._limitManager.GetSupervisor(bagInfo.Parent);
                    if (showBagSelector && supervisor != null)
                    {
                        AddBagSelector(plugin, builder, supervisor, bagInfo);
                    }
                }

                if (showUpgradeInfo)
                {
                    AddUpgradeInfo(plugin, builder, player, bagInfo);
                }

                return builder.Finalize();
            }

            private static void AddButton(UIBuilder builder, string color, string textColor, string text, string command, int offsetX, int width)
            {
                var uiName = CuiHelper.GetGuid();

                builder.WithElement(
                    UIElement.With(name: uiName, parent: UIName)
                        .WithComponent(UIButtonComponent.With(
                            color: color,
                            command: command
                        ))
                        .WithComponent(UIRectTransformComponent.With(
                            anchorMin: "0 0",
                            anchorMax: "0 0",
                            offsetMin: $"{offsetX} 0",
                            offsetMax: $"{offsetX + width} {UIConstants.HeaderHeight}"
                        ))
                );

                builder.WithElement(
                    UIElement.With(parent: uiName)
                        .WithComponent(UITextComponent.With(
                            text: text,
                            color: textColor,
                            textAlign: TextAnchor.MiddleCenter,
                            fontSize: 12
                        ))
                );
            }

            private static void AddBagSelector(BagOfHolding plugin, UIBuilder builder, ContainerSupervisor supervisor, BagInfo selectedBagInfo)
            {
                var bagSelectorSettings = plugin._config.UISettings.LootInterface.BagSelector;

                var minBagSize = bagSelectorSettings.MinBagSize;
                var maxBagSize = bagSelectorSettings.MaxBagSize;

                var maxBagSpace = bagSelectorSettings.MaxWidth;
                var bagList = CustomPool.GetList<BagInfo>();

                foreach (var bagInfo in supervisor.AllBags)
                {
                    if (bagInfo.Item.IsLocked())
                        continue;

                    bagList.Add(bagInfo);
                }

                supervisor.SortBagsByDisplayPosition(bagList);

                var maxBagsToShow = Mathf.FloorToInt(maxBagSpace / minBagSize / UIConstants.ItemSpaceFraction);
                var bagsToShow = Math.Min(bagList.Count, maxBagsToShow);

                var iconSize = Mathf.Clamp(maxBagSpace / UIConstants.ItemSpaceFraction / bagsToShow, minBagSize, maxBagSize);
                var adjustedIconSize = iconSize * UIConstants.ItemSpaceFraction;

                var bagIconOffsetY = 23 + 6;

                for (var i = 0; i < bagsToShow; i++)
                {
                    var bagInfo = bagList[i];
                    var isActive = bagInfo == selectedBagInfo;
                    var offsetX = UIConstants.PanelWidth - UIConstants.ContainerPadding + UIConstants.ItemSpacing - (bagsToShow - i) * adjustedIconSize;
                    AddBagIcon(builder, UIName, bagInfo, offsetX, bagIconOffsetY, iconSize, $"bag.select {bagInfo.Item.uid}", isActive: isActive, isGathering: bagInfo.IsGathering, showCapacity: true);
                }

                CustomPool.FreeList(ref bagList);
            }

            private static void AddUpgradeInfo(BagOfHolding plugin, UIBuilder builder, BasePlayer player, BagInfo bagInfo)
            {
                var offsetY = 23 + 6;

                var padding = 6f;
                var buttonHeight = 23;
                var contentHeight = 150;

                var width = UIConstants.PanelWidth;
                var contentWidth = width - padding * 2;

                var height = contentHeight + padding * 2;
                var buttonFontSize = 14;

                var dialogName = $"{UIName}.UpgradeDialog";

                var upgradeTarget = bagInfo.BagProfile.UpgradeTarget;
                var targetProfile = upgradeTarget.TargetProfile;

                builder.WithElement(
                    UIElement.With(name: dialogName, parent: UIName)
                        .WithComponent(UIImageComponent.With(
                            color: "0 0 0 0"
                        ))
                        .WithComponent(UIRectTransformComponent.With(
                            anchorMin: "0 0",
                            anchorMax: "0 0",
                            offsetMin: $"0 {offsetY}",
                            offsetMax: $"{width} {offsetY + height}"
                        ))
                );

                var dialogContentName = $"{dialogName}.Content";

                builder.WithElement(
                    UIElement.With(name: dialogContentName, parent: dialogName)
                        .WithComponent(UIRectTransformComponent.With(
                            anchorMin: "0 0",
                            anchorMax: "0 0",
                            offsetMin: $"{padding} {padding}",
                            offsetMax: $"{width - padding} {height - padding}"
                        ))
                );

                var costInfo = upgradeTarget.Cost;

                var leftIconOffsetX = 6 + UIConstants.ItemBoxSize + UIConstants.ItemSpacing;
                var leftIconOffsetY = 6 + 1 * UIConstants.ItemBoxSize;
                AddItemIcon(builder, UIName, SaddleBagItemId, bagInfo.BagProfile.SkinId, leftIconOffsetX, leftIconOffsetY, UIConstants.ItemBoxSize);

                var rightIconOffsetX = 6 + 4 * (UIConstants.ItemBoxSize + UIConstants.ItemSpacing);
                var rightIconOffsetY = 6 + 1 * UIConstants.ItemBoxSize;
                AddItemIcon(builder, UIName, SaddleBagItemId, targetProfile.SkinId, rightIconOffsetX, rightIconOffsetY, UIConstants.ItemBoxSize);

                // Left capacity
                builder.WithElement(
                    UIElement.With(parent: UIName)
                        .WithComponent(UITextComponent.With(
                            text: plugin.GetMessage(player.UserIDString, LangEntry.UISlotAmount, bagInfo.BagProfile.Capacity),
                            textAlign: TextAnchor.LowerCenter,
                            fontSize: 12,
                            color: "0.85 0.85 0.85 1"
                        ))
                        .WithComponent(UIRectTransformComponent.With(
                            anchorMin: "0 0",
                            anchorMax: "0 0",
                            offsetMin: $"{leftIconOffsetX + 2} {leftIconOffsetY + 1}",
                            offsetMax: $"{leftIconOffsetX + UIConstants.ItemBoxSize - 3} {leftIconOffsetY + UIConstants.ItemBoxSize}"
                        ))
                );

                // Right capacity
                builder.WithElement(
                    UIElement.With(parent: UIName)
                        .WithComponent(UITextComponent.With(
                            text: plugin.GetMessage(player.UserIDString, LangEntry.UISlotAmount, targetProfile.Capacity),
                            textAlign: TextAnchor.LowerCenter,
                            fontSize: 12,
                            color: "0.85 0.85 0.85 1"
                        ))
                        .WithComponent(UIRectTransformComponent.With(
                            anchorMin: "0 0",
                            anchorMax: "0 0",
                            offsetMin: $"{rightIconOffsetX + 2} {rightIconOffsetY + 1}",
                            offsetMax: $"{rightIconOffsetX + UIConstants.ItemBoxSize - 3} {rightIconOffsetY + UIConstants.ItemBoxSize}"
                        ))
                );

                // Left name
                builder.WithElement(
                    UIElement.With(parent: UIName)
                        .WithComponent(UITextComponent.With(
                            text: bagInfo.BagProfile.DisplayName,
                            textAlign: TextAnchor.LowerCenter,
                            color: "0.85 0.85 0.85 1"
                        ))
                        .WithComponent(UIRectTransformComponent.With(
                            anchorMin: "0 0",
                            anchorMax: "0 0",
                            offsetMin: $"{6 + 29} {rightIconOffsetY + UIConstants.ItemBoxSize + 4}",
                            offsetMax: $"{6 + 4 * 2 + 2 * UIConstants.ItemBoxSize + 29} {rightIconOffsetY + UIConstants.ItemBoxSize + 30 + 24}"
                        ))
                );

                // Right name
                builder.WithElement(
                    UIElement.With(parent: UIName)
                        .WithComponent(UITextComponent.With(
                            text: targetProfile.DisplayName,
                            textAlign: TextAnchor.LowerCenter,
                            color: "0.85 0.85 0.85 1"
                        ))
                        .WithComponent(UIRectTransformComponent.With(
                            anchorMin: "0 0",
                            anchorMax: "0 0",
                            offsetMin: $"{6 + 4 * 3 + 3 * UIConstants.ItemBoxSize + 29} {leftIconOffsetY + UIConstants.ItemBoxSize + 4}",
                            offsetMax: $"{6 + 4 * 5 + 5 * UIConstants.ItemBoxSize + 29} {leftIconOffsetY + UIConstants.ItemBoxSize + 30 + 24}"
                        ))
                );

                // +
                builder.WithElement(
                    UIElement.With(parent: UIName)
                        .WithComponent(UITextComponent.With(
                            text: "+",
                            textAlign: TextAnchor.MiddleCenter,
                            fontSize: 30,
                            color: "0.85 0.85 0.85 1"
                        ))
                        .WithComponent(UIRectTransformComponent.With(
                            anchorMin: "0 0",
                            anchorMax: "0 0",
                            offsetMin: $"{6 + 4 * 2 + UIConstants.ItemBoxSize * 2} {6 + UIConstants.ItemBoxSize}",
                            offsetMax: $"{6 + 4 * 2 + UIConstants.ItemBoxSize * 2 + 29} {6 + 2 * UIConstants.ItemBoxSize}"
                        ))
                );

                // =
                builder.WithElement(
                    UIElement.With(parent: UIName)
                        .WithComponent(UITextComponent.With(
                            text: "=",
                            textAlign: TextAnchor.MiddleCenter,
                            fontSize: 30,
                            color: "0.85 0.85 0.85 1"
                        ))
                        .WithComponent(UIRectTransformComponent.With(
                            anchorMin: "0 0",
                            anchorMax: "0 0",
                            offsetMin: $"{6 + 4 * 3 + UIConstants.ItemBoxSize * 3 + 29} {6 + UIConstants.ItemBoxSize}",
                            offsetMax: $"{6 + 4 * 3 + UIConstants.ItemBoxSize * 4} {6 + 2 * UIConstants.ItemBoxSize}"
                        ))
                );

                // Currency
                var paymentProvider = plugin._paymentProviderResolver.Resolve(costInfo, bagInfo.Item);
                var balance = paymentProvider.GetBalance(player);

                var costAmount = costInfo.Amount;
                var costAmountOverride = plugin._config.GetUpgradeCostOverride(player.UserIDString, bagInfo.BagProfile);
                if (costAmountOverride >= 0)
                {
                    costAmount = costAmountOverride;
                }

                var canAfford = balance >= costAmount;

                var allowedByLimits = bagInfo.BagProfile.UpgradesToSameCategory
                    || bagInfo.ParentSupervisor?.CanChangeBagCategory(bagInfo, targetProfile.CategoryNumber) != false;

                var allowedByStackSize = bagInfo.Item.amount == 1;
                var canUpgrade = allowedByLimits && allowedByStackSize && canAfford;

                var upgradeText = !allowedByLimits
                    ? plugin.GetMessage(player.UserIDString, LangEntry.UIUpgradeErrorLimit)
                    : !allowedByStackSize
                    ? plugin.GetMessage(player.UserIDString, LangEntry.UIUpgradeErrorStacked)
                    : !canAfford
                    ? plugin.GetMessage(player.UserIDString, LangEntry.UIUpgradeErrorCantAfford)
                    : costAmount > 0
                    ? plugin.GetMessage(player.UserIDString, LangEntry.UIUpgradePurchase)
                    : plugin.GetMessage(player.UserIDString, LangEntry.UIUpgradeFree);

                var currencyIconSize = 58f;
                var currencyOffsetX = 6 + 4 * 2 + UIConstants.ItemBoxSize * 3 - currencyIconSize / 2;
                var currencyOffsetY = 6 + UIConstants.ItemBoxSize;

                if (paymentProvider is ItemsPaymentProvider)
                {
                    AddItemIcon(builder, UIName, costInfo.ItemId, costInfo.SkinId, currencyOffsetX, currencyOffsetY, currencyIconSize);

                    builder.WithElement(
                        UIElement.With(parent: UIName)
                            .WithComponent(UITextComponent.With(
                                text: $"x{costAmount}",
                                textAlign: TextAnchor.LowerRight,
                                fontSize: 12,
                                color: "0.85 0.85 0.85 1"
                            ))
                            .WithComponent(UIRectTransformComponent.With(
                                anchorMin: "0 0",
                                anchorMax: "0 0",
                                offsetMin: $"{currencyOffsetX} {currencyOffsetY + 1}",
                                offsetMax: $"{currencyOffsetX + currencyIconSize - 3} {currencyOffsetY + currencyIconSize}"
                            ))
                    );
                }
                else
                {
                    var currencyName = plugin.GetCurrencyName(player.UserIDString, paymentProvider);

                    AddItemBackground(builder, UIName, currencyOffsetX, currencyOffsetY, currencyIconSize);

                    builder.WithElement(
                        UIElement.With(parent: UIName)
                            .WithComponent(UITextComponent.With(
                                text: $"{costAmount} {currencyName}\n({balance})",
                                textAlign: TextAnchor.MiddleCenter,
                                fontSize: 12,
                                color: "0.85 0.85 0.85 1"
                            ))
                            .WithComponent(UIRectTransformComponent.With(
                                anchorMin: "0 0",
                                anchorMax: "0 0",
                                offsetMin: $"{currencyOffsetX} {currencyOffsetY + 1}",
                                offsetMax: $"{currencyOffsetX + currencyIconSize - 3} {currencyOffsetY + currencyIconSize}"
                            ))
                    );
                }

                var buttonWidth = 200;
                var buttonName = CuiHelper.GetGuid();

                builder.WithElement(
                    UIElement.With(name: buttonName, parent: UIName)
                        .WithComponent(UIButtonComponent.With(
                            color: canUpgrade ? UIConstants.GreenButtonColor : "0.694 0.227 0.157",
                            command: "bag.ui.upgrade confirm"
                        ))
                        .WithComponent(UIRectTransformComponent.With(
                            anchorMin: "0 0",
                            anchorMax: "0 0",
                            offsetMin: $"{(contentWidth - buttonWidth) / 2 + 6} {UIConstants.HeaderHeight + 8}",
                            offsetMax: $"{(contentWidth - buttonWidth) / 2 + 6 + buttonWidth} {UIConstants.HeaderHeight + 8 + buttonHeight}"
                        ))
                );

                builder.WithElement(
                    UIElement.With(parent: buttonName)
                        .WithComponent(UITextComponent.With(
                            color: canUpgrade ? UIConstants.GreenButtonTextColor : "0.855 0.733 0.710",
                            text: upgradeText,
                            textAlign: TextAnchor.MiddleCenter,
                            fontSize: buttonFontSize
                        ))
                );
            }
        }

        private class BeltUIRenderer
        {
            private const string UIName = "BagOfHolding.BeltUI";

            private BagOfHolding _plugin;
            private string _cachedJson;
            private HashSet<ulong> _playerIdsWithVisibleUI = new();

            private UIBuilder _builder = new(3000);

            private Configuration _config => _plugin._config;

            public BeltUIRenderer(BagOfHolding plugin)
            {
                _plugin = plugin;
            }

            public void CreateUIIfMissing(BasePlayer player)
            {
                if (!_playerIdsWithVisibleUI.Add(player.userID))
                    return;

                _cachedJson ??= RenderBeltUI();

                CuiHelper.AddUi(player, _cachedJson);
            }

            public void DestroyUIIfPresent(BasePlayer player)
            {
                if (!_playerIdsWithVisibleUI.Remove(player.userID))
                    return;

                CuiHelper.DestroyUi(player, UIName);
            }

            private string RenderBeltUI()
            {
                var beltUISettings = _config.UISettings.BeltButton;

                var offsetX = beltUISettings.OffsetX;
                var offsetY = 18;
                var iconSize = 60;

                _builder.Start();
                _builder.WithElement(
                    UIElement.With(name: UIName, parent: "Hud.Menu", destroyName: UIName)
                        .WithComponent(UIImageComponent.With())
                        .WithComponent(UIRectTransformComponent.With(
                            anchorMin: UIConstants.AnchorMin,
                            anchorMax: UIConstants.AnchorMax
                        ))
                );

                AddItemIcon(_builder, UIName, SaddleBagItemId, beltUISettings.SkinId, offsetX, offsetY, iconSize);

                _builder.WithElement(
                    UIElement.With(name: "BagOfHolding.BeltUI.Button", parent: UIName)
                        .WithComponent(UIButtonComponent.With(
                            command: "bag.open",
                            color: "1 1 1 0"
                        ))
                        .WithComponent(UIRectTransformComponent.With(
                            anchorMin: "0 0",
                            anchorMax: "0 0",
                            offsetMin: $"{offsetX} {offsetY}",
                            offsetMax: $"{offsetX + iconSize} {offsetY + iconSize}"
                        ))
                );

                return _builder.Finalize();
            }
        }

        #endregion

        #region Bag Manager

        private enum GatherMode
        {
            None,
            All,
            Existing
        }

        private class BagInfo : CustomPool.IPooled
        {
            public Item Item { get; private set; }
            public BagProfile BagProfile { get; private set; }
            public BasePlayer AncestorPlayer;
            public ItemContainer Parent;
            public ContainerSupervisor ParentSupervisor;
            public bool HasEverBeenInAContainer;
            public bool HasEverContainedAnItem;

            public bool HasItems => Item.contents?.itemList.Count > 0;
            public bool IsGathering => Item.HasFlag(FlagGather);

            public GatherMode GatherMode
            {
                get
                {
                    if (!Item.HasFlag(FlagGather))
                        return GatherMode.None;

                    return Item.HasFlag(FlagGatherExisting) ? GatherMode.Existing : GatherMode.All;
                }
            }

            public bool CanGather
            {
                get
                {
                    if (Item.parent == null)
                        return false;

                    if (Item.IsLocked())
                        return false;

                    var contents = Item.contents;
                    if (contents == null)
                        return false;

                    return !contents.IsLocked() && !contents.PlayerItemInputBlocked();
                }
            }

            private List<BasePlayer> _looters = new(1);
            private List<BasePlayer> _upgradeViewers = new(1);
            private List<BasePlayer> _uiViewers = new(1);
            private bool _hasItemEverBeenRemovedFromBag;

            public void Setup(Item bagItem, BagProfile bagProfile, BagOfHolding plugin)
            {
                Item = bagItem;
                BagProfile = bagProfile;
                Parent = bagItem.parent;

                var rootContainer = GetRootContainer(bagItem);
                AncestorPlayer = rootContainer != null ? GetOwnerPlayer(rootContainer) : null;

                bagItem.name = bagProfile.DisplayName;

                CreateOrUpdateContainer();

                bagItem.contents.onItemAddedRemoved = (childItem, wasAdded) =>
                {
                    if (wasAdded && ItemUtils.HasItemMod<ItemModEntity>(childItem.info))
                    {
                        // Copy these variables so that the closure object only gets created if this block runs.
                        // Note: The instance variables don't need to be copied for this optimization.
                        var childItem2 = childItem;

                        plugin.NextTick(() =>
                        {
                            // Verify the item is still in the bag.
                            if (childItem2.parent != Item.contents)
                                return;

                            var heldEntity = childItem2.GetHeldEntity();
                            if (heldEntity != null)
                            {
                                // Work around a bug in `ItemModEntity.OnParentChanged(Item)` which causes the item to
                                // be moved to map origin. Clients near map origin will briefly be aware of the entity.
                                heldEntity.limitNetworking = true;
                                heldEntity.SetFlag(Flags.Disabled, true);
                            }
                        });
                    }

                    if (wasAdded)
                    {
                        if (!HasEverContainedAnItem)
                        {
                            MaybeUpdateBagProfile(plugin);
                            HasEverContainedAnItem = true;
                        }
                    }
                    else
                    {
                        ParentSupervisor?.PauseGatherModeForOneFrame();
                    }

                    if (!BagProfile.AllowsPlayerItemInput)
                    {
                        // Kit bags do not allow player input, so we need to handle these cases:
                        // 1. When a player adds an item (via swapping), remove that item.
                        // 2. When the bag becomes empty, remove the bag.
                        if (wasAdded)
                        {
                            if (_hasItemEverBeenRemovedFromBag)
                            {
                                // Since items have been removed from the bag previously, we assume the item was added
                                // by a player (not by a plugin). The only way a player can do that is if they swapped
                                // the item with one in their inventory, which is allowed by vanilla. We don't want to
                                // allow the player to use that trick since it would allow them to use kit bags for
                                // storage, so try to move the new item to the parent container or drop it.
                                if (bagItem.parent == null || !childItem.MoveToContainer(bagItem.parent))
                                {
                                    TryDropItemFromBag(childItem);
                                }
                            }
                        }
                        else
                        {
                            _hasItemEverBeenRemovedFromBag = true;

                            if (bagItem.contents.itemList.Count == 0)
                            {
                                // The bag is now empty, so remove it.
                                // Delay since the player might be swapping the item out for another.
                                plugin.NextTick(() =>
                                {
                                    // Verify the bag is still empty, since the player might have swapped an item.
                                    if (Item == null || Item.contents?.itemList.Count > 0)
                                        return;

                                    Item.RemoveFromContainer();
                                    Item.Remove();
                                });
                                return;
                            }
                        }
                    }

                    if (TrySetItemFlag(bagItem, FlagHasItems, bagItem.contents.itemList.Count > 0))
                    {
                        ParentSupervisor?.RefreshBags();
                    }
                    else
                    {
                        // Only called if RefreshBags is not called, since RefreshBags calls this too.
                        ParentSupervisor?.HandleBagChildAddedRemoved();
                    }

                    // Don't bother altering stack size of kit bags since that should be addressed by the kit creator.
                    if (wasAdded && BagProfile.AllowsPlayerItemInput)
                    {
                        var stackSizeOverride = BagProfile.StackProfile?.GetStackSizeOverride(childItem);
                        if (stackSizeOverride != null)
                        {
                            var maxStackSize = (int)stackSizeOverride;
                            while (childItem.amount > maxStackSize)
                            {
                                var amountToSplit = Math.Min(maxStackSize, childItem.amount - maxStackSize);
                                var splitItem = childItem.SplitItem(amountToSplit);
                                if (splitItem == null)
                                    break;

                                if (!splitItem.MoveToContainer(bagItem.contents, allowStack: false)
                                    && !TryDropItemFromBag(splitItem))
                                {
                                    // Cannot drop, so add back to original item.
                                    childItem.amount += splitItem.amount;
                                    childItem.Remove();
                                    break;
                                }
                            }
                        }
                    }
                };

                if (BagProfile.ContentsRuleset != null)
                {
                    bagItem.contents.canAcceptItem = (childItem, targetPosition) =>
                    {
                        // Don't allow items with ItemModEntity to be added to a dropped bag item, since that will parent the entity.
                        if (Item.parent == null && (object)Item.GetWorldEntity() != null && ItemUtils.HasItemMod<ItemModEntity>(childItem.info))
                            return false;

                        return BagProfile.ContentsRuleset.AllowsItem(childItem);
                    };
                }

                if (IsGathering)
                {
                    if (plugin._config.GatherMode.Enabled && CanGather)
                    {
                        ParentSupervisor?.HandleBagGatherToggled(enabled: true);
                    }
                    else
                    {
                        TrySetItemFlag(Item, FlagGather, false);
                    }
                }
            }

            public void EnterPool()
            {
                #if DEBUG_POOLING
                LogWarning($"EnterPool | {CustomPool.GetStats<BagInfo>()}");
                #endif

                Item = null;
                BagProfile = null;
                AncestorPlayer = null;
                Parent = null;
                ParentSupervisor = null;
                HasEverBeenInAContainer = false;
                HasEverContainedAnItem = false;
                _looters.Clear();
                _upgradeViewers.Clear();
                _uiViewers.Clear();
                _hasItemEverBeenRemovedFromBag = false;
            }

            public void LeavePool()
            {
                #if DEBUG_POOLING
                LogWarning($"LeavePool | {CustomPool.GetStats<BagInfo>()}");
                #endif
            }

            public void Unload()
            {
                ForceCloseAllLooters();
                ParentSupervisor?.UnregisterBag(this);
                ParentSupervisor = null;
            }

            public void ChangeProfile(BagOfHolding plugin, BagProfile bagProfile, BasePlayer initiator = null)
            {
                if (bagProfile == BagProfile)
                    return;

                var previousBagProfile = BagProfile;

                BagProfile = bagProfile;
                CreateOrUpdateContainer(initiator);
                Item.name = bagProfile.DisplayName;
                Item.skin = bagProfile.SkinId;
                Item.MarkDirty();

                if (IsGathering && !CanGather)
                {
                    SetGatherMode(GatherMode.None);
                }

                ExposedHooks.OnBagProfileChanged(Item, bagProfile.Name);

                ParentSupervisor?.HandleBagCategoryChanged(this, previousBagProfile);

                plugin._limitManager.RefreshLimitProfile<LimitProfile>(Item.contents);

                for (var i = _looters.Count - 1; i >= 0; i--)
                {
                    var looter = _looters[i];
                    plugin.AttemptOpenBag(looter, this, previousBagProfile);
                }
            }

            public bool TryUpgrade(BagOfHolding plugin, BasePlayer initiator = null)
            {
                var targetProfile = BagProfile.UpgradeTarget?.TargetProfile;
                if (targetProfile == null)
                    return false;

                if (!BagProfile.UpgradesToSameCategory
                    && ParentSupervisor?.CanChangeBagCategory(this, targetProfile.CategoryNumber) == false)
                    return false;

                var hookResult = ExposedHooks.OnBagUpgrade(Item, targetProfile.Name, initiator);
                if (hookResult is false)
                    return false;

                ChangeProfile(plugin, targetProfile, initiator);
                if ((object)initiator != null)
                {
                    SendEffect(initiator, plugin._config.Effects.UpgradeEffect);
                }

                ExposedHooks.OnBagUpgraded(Item, targetProfile.Name, initiator);
                return true;
            }

            public void MaybeUpdateBagProfile(BagOfHolding plugin)
            {
                if (Item.contents.capacity == BagProfile.Capacity
                    && StringUtils.Equals(Item.name, BagProfile.DisplayName))
                    return;

                // Scenario A:
                //   1. Another plugin created the bag item with a skin.
                //   2. Bag of Holding detected the item being created, then created the container.
                //   3. The other plugin adjusted the display name or capacity based on memory.
                //   4. The item was added to a container for the first time, or Bag of Holding reloaded.
                //
                // Scenario B:
                //   1. Display name or capacity was reconfigured.
                //   2. Bag of Holding reloaded.

                var idealBagProfile = plugin._config.DetermineBagProfile(Item);
                if (idealBagProfile == null)
                {
                    var bagProfile = BagProfile;
                    LogError($"A bag item has changed after creation and is no longer recognized!\nSkin: {bagProfile.SkinId} -> {Item.skin}, Capacity: {bagProfile.Capacity} -> {Item.contents.capacity}, Display name: {bagProfile.DisplayName} -> {Item.name}.");
                    return;
                }

                if (idealBagProfile == BagProfile)
                {
                    // The capacity is not a perfect match, so try to adjust it.
                    HandleOverflowingItems();
                    return;
                }

                // There is a more suitable bag profile, so change the capacity.
                ChangeProfile(plugin, idealBagProfile);
            }

            public void ForceCloseAllLooters()
            {
                for (var i = _looters.Count - 1; i >= 0; i--)
                {
                    var looter = _looters[i];
                    DestroyUI(looter);
                    _looters.RemoveAt(i);
                    looter.inventory.loot.Clear();
                }
            }

            public bool SetLocked(bool value)
            {
                if (value)
                {
                    ForceCloseAllLooters();
                }

                var changed = false;

                if (Item.contents != null)
                {
                    changed |= TrySetItemFlag(Item, Item.Flag.IsLocked, value);
                }
                else
                {
                    LogError($"Bag {BagProfile.Name} is missing a container while {(value ? "locking" : "unlocking")} it.");
                }

                return changed | TrySetItemFlag(Item, Item.Flag.IsLocked, value);
            }

            public void SetGatherMode(GatherMode gatherMode)
            {
                var originalGatherMode = GatherMode;
                if (gatherMode == originalGatherMode)
                    return;

                if (ParentSupervisor == null)
                    return;

                var changed = false;

                switch (gatherMode)
                {
                    case GatherMode.None:
                        changed |= TrySetItemFlag(Item, FlagGather, false);
                        changed |= TrySetItemFlag(Item, FlagGatherExisting, false);
                        break;
                    case GatherMode.All:
                        changed |= TrySetItemFlag(Item, FlagGather, true);
                        changed |= TrySetItemFlag(Item, FlagGatherExisting, false);
                        break;
                    case GatherMode.Existing:
                        changed |= TrySetItemFlag(Item, FlagGather, true);
                        changed |= TrySetItemFlag(Item, FlagGatherExisting, true);
                        break;
                }

                if (changed)
                {
                    // Marking the item dirty is primarily for networking, to ensure that the flame icon is toggled on
                    // the client, but it's also done in case the parent container is a Backpack, to ensure the flag
                    // change gets saved.
                    Item.MarkDirty();
                }

                // If gather mode is or was None, then it was toggled.
                // This check allows skipping the case where gather mode is switched between All and Existing.
                if (originalGatherMode == GatherMode.None || gatherMode == GatherMode.None)
                {
                    ParentSupervisor.HandleBagGatherToggled(gatherMode != GatherMode.None);
                }
            }

            public void AddLooter(BagOfHolding plugin, BasePlayer player)
            {
                if (!plugin.IsLoaded)
                    return;

                if (_looters.Contains(player))
                    return;

                _looters.Add(player);

                CreateUI(plugin, player);
            }

            public void RemoveLooter(BasePlayer player)
            {
                if (!_looters.Contains(player))
                    return;

                _looters.Remove(player);
                _upgradeViewers.Remove(player);

                DestroyUI(player);
            }

            public void RefreshUI(BagOfHolding plugin)
            {
                foreach (var player in _looters)
                {
                    DestroyUI(player);
                    CreateUI(plugin, player);
                }
            }

            public void ToggleUpgradeViewer(BagOfHolding plugin, BasePlayer player)
            {
                if (_upgradeViewers.Remove(player))
                {
                    DestroyUI(player);
                    CreateUI(plugin, player);
                }
                else
                {
                    DestroyUI(player);
                    _upgradeViewers.Add(player);
                    CreateUI(plugin, player);
                }
            }

            public void CreateOrUpdateContainer(BasePlayer initiator = null)
            {
                var hadContainer = Item.contents != null;

                if (hadContainer)
                {
                    // If updating the capacity would hide existing items, that will be fixed in HandleOverflowingItems.
                    Item.contents.capacity = BagProfile.Capacity;
                }
                else
                {
                    Item.contents = new ItemContainer();
                    Item.contents.ServerInitialize(Item, BagProfile.Capacity);
                    Item.contents.GiveUID();
                    Item.contents.parent = Item;
                }

                var shouldBlockItemInput = (Item.parent?.PlayerItemInputBlocked() ?? false)
                    || !BagProfile.AllowsPlayerItemInput;

                TrySetContainerFlag(Item.contents, ItemContainer.Flag.NoItemInput, shouldBlockItemInput);

                Item.contents.maxStackSize = BagProfile.StackProfile?.GlobalMaxStackSize ?? 0;

                if (hadContainer)
                {
                    HandleOverflowingItems(initiator);
                }
            }

            public void HandleOverflowingItems(BasePlayer initiator = null)
            {
                var container = Item.contents;
                var capacity = container.capacity;

                List<Item> overflowingItems = null;

                for (var i = container.itemList.Count - 1; i >= 0; i--)
                {
                    var item = container.itemList[i];
                    if (item.position >= capacity)
                    {
                        item.RemoveFromContainer();
                        overflowingItems ??= CustomPool.GetList<Item>();
                        overflowingItems.Add(item);
                    }
                }

                if (overflowingItems != null)
                {
                    for (var i = overflowingItems.Count - 1; i >= 0; i--)
                    {
                        var item = overflowingItems[i];

                        // Try to move the item to the bag.
                        if (item.MoveToContainer(container))
                        {
                            overflowingItems.RemoveAt(i);
                            continue;
                        }

                        if ((object)initiator == null)
                            break;

                        initiator.GiveItem(item);
                        overflowingItems.RemoveAt(i);
                    }

                    if (overflowingItems.Count > 0)
                    {
                        var capacityBeforeExpansion = container.capacity;
                        container.capacity += overflowingItems.Count;

                        for (var i = 0; i < overflowingItems.Count; i++)
                        {
                            var item = overflowingItems[i];
                            item.position = capacityBeforeExpansion + i;
                            item.SetParent(container);
                        }
                    }

                    CustomPool.FreeList(ref overflowingItems);
                }
            }

            private bool TryDropItemFromBag(Item item)
            {
                var rootContainer = GetRootContainer(Item);

                var worldEntity = rootContainer.parent?.GetWorldEntity();
                if ((object)worldEntity != null)
                {
                    item.Drop(worldEntity.GetDropPosition(), worldEntity.GetDropVelocity());
                    return true;
                }

                var playerOwner = GetOwnerPlayer(rootContainer) ?? GetOwnerPlayer(Item);
                if ((object)playerOwner != null)
                {
                    if (!playerOwner.inventory.GiveItem(item))
                    {
                        item.Drop(playerOwner.GetDropPosition(), playerOwner.GetDropVelocity());
                    }
                    return true;
                }

                var entityOwner = rootContainer.entityOwner ?? item.parent?.entityOwner;
                if ((object)entityOwner != null)
                {
                    item.Drop(entityOwner.GetDropPosition(), entityOwner.GetDropVelocity());
                    return true;
                }

                return false;
            }

            private void CreateUI(BagOfHolding plugin, BasePlayer player)
            {
                if (_uiViewers.Contains(player))
                    return;

                var showUpgradeInfo = _upgradeViewers.Contains(player);

                var uiJson = ContainerUIRenderer.RenderContainerUI(plugin, player, this, showUpgradeInfo);
                if (uiJson == null)
                    return;

                _uiViewers.Add(player);
                CuiHelper.AddUi(player, uiJson);
            }

            private void DestroyUI(BasePlayer player)
            {
                if (!_uiViewers.Remove(player))
                    return;

                CuiHelper.DestroyUi(player, ContainerUIRenderer.UIName);
            }
        }

        private class BagQuery
        {
            private static readonly Regex ArgPattern = new(@"([^!=<>]+)(!?={1,2}|<=?|>=?)([^=]+)", RegexOptions.Compiled);

            public enum BagLocation
            {
                PlayerInventory,
                LootContainer,
                Backpack,
                StorageContainer,
                Corpse,
                DroppedItemContainer,
                DroppedItem,
                Animal,
                Limbo,
                Other,
            }

            public static BagLocation GetBagLocation(BagOfHolding plugin, BagInfo bagInfo)
            {
                var item = bagInfo.Item;

                var rootContainer = GetRootContainer(item);
                if (rootContainer == null)
                    return item.GetWorldEntity() != null
                        ? BagLocation.DroppedItem
                        : BagLocation.Limbo;

                if (rootContainer.parent != null)
                    return rootContainer.parent.GetWorldEntity() != null
                        ? BagLocation.DroppedItem
                        : BagLocation.Limbo;

                var playerOwner = GetOwnerPlayer(rootContainer);
                if ((object)playerOwner != null)
                    return BagLocation.PlayerInventory;

                var entityOwner = rootContainer.entityOwner;
                if (entityOwner == null)
                    return BagLocation.Other;

                return entityOwner switch
                {
                    LootableCorpse => BagLocation.Corpse,
                    BaseRidableAnimal => BagLocation.Animal,
                    DroppedItemContainer => BagLocation.DroppedItemContainer,
                    LootContainer lootContainer when !IsPlayerDeployedLootContainer(lootContainer) => BagLocation.LootContainer,
                    StorageContainer when plugin._backpacksAdapter.GetBackpackOwnerId(rootContainer) != 0 => BagLocation.Backpack,
                    _ => BagLocation.StorageContainer,
                };
            }

            public static BagQuery FromArgs(BagOfHolding plugin, string[] args)
            {
                var bagQuery = new BagQuery();

                foreach (var arg in args)
                {
                    var match = ArgPattern.Match(arg);
                    if (!match.Success)
                        throw new ArgumentException("Invalid query parameter");

                    var argName = match.Groups[1].Value;
                    var argOperator = match.Groups[2].Value;
                    var argValues = match.Groups[3].Value.Split(',', StringSplitOptions.RemoveEmptyEntries);

                    if (StringUtils.Equals(argName, "locked"))
                    {
                        if (!bool.TryParse(argValues[0], out var locked))
                            throw new ArgumentException($"Invalid boolean value for parameter 'locked': {argValues[0]}");

                        bagQuery.Locked = locked;
                        continue;
                    }

                    if (StringUtils.Equals(argName, "capacity"))
                    {
                        if (!float.TryParse(argValues[0], out var capacityValue))
                            throw new ArgumentException($"Invalid value for parameter 'capacity': {argValues[0]}");

                        bagQuery.CapacityCondition = new Condition<float>(ResolveComparer<float>(argOperator), capacityValue);
                    }

                    if (StringUtils.Equals(argName, "occupiedslots"))
                    {
                        if (!float.TryParse(argValues[0], out var capacityValue))
                            throw new ArgumentException($"Invalid value for parameter 'occupiedslots': {argValues[0]}");

                        bagQuery.OccupiedSlotsCondition = new Condition<float>(ResolveComparer<float>(argOperator), capacityValue);
                    }

                    if (StringUtils.Equals(argName, "fullpercent"))
                    {
                        if (!float.TryParse(argValues[0], out var capacityValue))
                            throw new ArgumentException($"Invalid value for parameter 'fullpercent': {argValues[0]}");

                        bagQuery.FullPercentCondition = new Condition<float>(ResolveComparer<float>(argOperator), capacityValue);
                        continue;
                    }

                    if (StringUtils.Equals(argName, "profile"))
                    {
                        foreach (var profileName in argValues)
                        {
                            var bagProfile = plugin._config.GetBagProfileByName(profileName);
                            if (bagProfile == null)
                                throw new ArgumentException($"Invalid value for 'profile': {profileName}");

                            bagQuery.ProfileNames ??= new List<string>();
                            bagQuery.ProfileNames.Add(bagProfile.Name);
                        }

                        continue;
                    }

                    if (StringUtils.Equals(argName, "skin"))
                    {
                        foreach (var argValue in argValues)
                        {
                            if (!ulong.TryParse(argValue, out var skinId))
                                throw new ArgumentException($"Invalid value for 'skin': {argValue}");

                            bagQuery.SkinIds ??= new List<ulong>();
                            bagQuery.SkinIds.Add(skinId);
                        }
                    }

                    if (StringUtils.Equals(argName, "category"))
                    {
                        foreach (var argValue in argValues)
                        {
                            if (!plugin._config.UniqueBagCategories.Contains(argValue, StringComparer.InvariantCultureIgnoreCase))
                                throw new ArgumentException($"Invalid value for 'category': {argValue}");

                            bagQuery.CategoryNames ??= new List<string>();
                            bagQuery.CategoryNames.Add(argValue);
                        }
                    }

                    if (StringUtils.Equals(argName, "location"))
                    {
                        foreach (var argValue in argValues)
                        {
                            if (!Enum.TryParse(argValue, true, out BagLocation bagLocation))
                                throw new ArgumentException($"Invalid value for 'location': {argValue}");

                            bagQuery.Locations ??= new List<BagLocation>();
                            bagQuery.Locations.Add(bagLocation);
                        }
                    }

                    if (StringUtils.Equals(argName, "gathermode"))
                    {
                        foreach (var argValue in argValues)
                        {
                            if (!Enum.TryParse(argValue, true, out GatherMode gatherMode))
                                throw new ArgumentException($"Invalid value for parameter 'gathermode': {argValue}");

                            bagQuery.GatherModes ??= new List<GatherMode>();
                            bagQuery.GatherModes.Add(gatherMode);
                        }

                        continue;
                    }
                }

                return bagQuery;
            }

            private static Func<T, T, bool> ResolveComparer<T>(string @operator) where T : IComparable
            {
                return @operator switch
                {
                    "=" or "==" => (a, b) => Comparer<T>.Default.Compare(a, b) == 0,
                    "!=" => (a, b) => Comparer<T>.Default.Compare(a, b) != 0,
                    ">" => (a, b) => Comparer<T>.Default.Compare(a, b) > 0,
                    ">=" => (a, b) => Comparer<T>.Default.Compare(a, b) >= 0,
                    "<" => (a, b) => Comparer<T>.Default.Compare(a, b) < 0,
                    "<=" => (a, b) => Comparer<T>.Default.Compare(a, b) <= 0,
                    _ => throw new ArgumentException($"Unrecognized operator: {@operator}"),
                };
            }

            private bool? Locked;
            private Condition<float>? CapacityCondition;
            private Condition<float>? OccupiedSlotsCondition;
            private Condition<float>? FullPercentCondition;
            private List<string> ProfileNames;
            private List<ulong> SkinIds;
            private List<string> CategoryNames;
            private List<BagLocation> Locations;
            private List<GatherMode> GatherModes;

            private readonly struct Condition<T>
            {
                private readonly Func<T, T, bool> Comparer;
                private readonly T Amount;

                public Condition(Func<T, T, bool> comparer, T amount)
                {
                    Comparer = comparer;
                    Amount = amount;
                }

                public bool Passes(T value)
                {
                    return Comparer.Invoke(value, Amount);
                }
            }

            private static float GetCapacity(BagInfo bagInfo) => bagInfo.BagProfile.Capacity;
            private static float GetOccupiedSlots(BagInfo bagInfo) => bagInfo.Item.contents?.itemList?.Count ?? 0;
            private static float GetFullPercent(BagInfo bagInfo) => GetOccupiedSlots(bagInfo) / GetCapacity(bagInfo) * 100;

            public bool Matches(BagOfHolding plugin, BagInfo bagInfo)
            {
                if (Locked.HasValue && Locked.Value != bagInfo.Item.IsLocked())
                    return false;

                if (CapacityCondition?.Passes(GetCapacity(bagInfo)) == false)
                    return false;

                if (OccupiedSlotsCondition?.Passes(GetOccupiedSlots(bagInfo)) == false)
                    return false;

                if (FullPercentCondition?.Passes(GetFullPercent(bagInfo)) == false)
                    return false;

                if (ProfileNames != null)
                {
                    var matches = false;

                    foreach (var profileName in ProfileNames)
                    {
                        if (bagInfo.BagProfile.MatchesNameOrDisplayName(profileName))
                        {
                            matches = true;
                            break;
                        }
                    }

                    if (!matches)
                        return false;
                }

                if (SkinIds != null)
                {
                    var matches = false;

                    foreach (var skinId in SkinIds)
                    {
                        if (bagInfo.Item.skin == skinId)
                        {
                            matches = true;
                            break;
                        }
                    }

                    if (!matches)
                        return false;
                }

                if (CategoryNames != null)
                {
                    var matches = false;

                    foreach (var categoryName in CategoryNames)
                    {
                        if (StringUtils.Equals(bagInfo.BagProfile.CategoryName, categoryName))
                        {
                            matches = true;
                            break;
                        }
                    }

                    if (!matches)
                        return false;
                }

                if (Locations != null)
                {
                    var matches = false;
                    var bagLocation = GetBagLocation(plugin, bagInfo);

                    foreach (var location in Locations)
                    {
                        if (location == bagLocation)
                        {
                            matches = true;
                            break;
                        }
                    }

                    if (!matches)
                        return false;
                }

                if (GatherModes != null)
                {
                    var matches = false;

                    foreach (var gatherMode in GatherModes)
                    {
                        if (gatherMode == bagInfo.GatherMode)
                        {
                            matches = true;
                            break;
                        }
                    }

                    if (!matches)
                        return false;
                }

                return true;
            }
        }

        private class BagManager
        {
            private BagOfHolding _plugin;
            private Configuration _config;
            private LimitManager _limitManager;
            private Dictionary<Item, BagInfo> _bagToInfoMap = new();

            public BagManager(BagOfHolding plugin, Configuration config, LimitManager limitManager)
            {
                _plugin = plugin;
                _config = config;
                _limitManager = limitManager;
            }

            public void Unload()
            {
                foreach (var bagInfo in _bagToInfoMap.Values)
                {
                    bagInfo.Unload();
                }
            }

            public BagInfo GetBagInfo(Item item)
            {
                return _bagToInfoMap.TryGetValue(item, out var bagInfo)
                    ? bagInfo
                    : null;
            }

            public bool IsBag(Item item)
            {
                return GetBagInfo(item) != null;
            }

            public bool IsNonEmptyBag(Item item)
            {
                return GetBagInfo(item)?.HasItems ?? false;
            }

            public BagInfo EnsureBagInfoIfEligible(Item item)
            {
                if (item.amount <= 0)
                    return null;

                if (!IsSkinnedBag(item))
                    return null;

                var bagInfo = GetBagInfo(item);
                if (bagInfo != null)
                {
                    if (item.contents == null)
                    {
                        LogError($"Bag {item.uid} ({bagInfo.BagProfile.Name}) is missing its inventory. Remove time: {item.removeTime}.");
                    }
                    return bagInfo;
                }

                var bagProfile = _config.DetermineBagProfile(item);
                if (bagProfile == null)
                    return null;

                bagInfo = CustomPool.Get<BagInfo>();
                bagInfo.Setup(item, bagProfile, _plugin);
                _bagToInfoMap[item] = bagInfo;

                if (item.parent != null)
                {
                    _limitManager.HandleBagAddedToContainer(item.parent, bagInfo);
                }

                return bagInfo;
            }

            public void HandleBagRemoved(BagInfo bagInfo, Item item)
            {
                _bagToInfoMap.Remove(item);
                bagInfo.ForceCloseAllLooters();

                if (bagInfo.Parent != null)
                {
                    _limitManager.HandleBagRemovedFromContainer(bagInfo.Parent, bagInfo);
                }

                CustomPool.Free(ref bagInfo);
            }

            public void HandlePlayerStoppedLooting(BasePlayer player, BagInfo bagInfo)
            {
                bagInfo.RemoveLooter(player);
                ExposedHooks.OnBagClosed(bagInfo.Item, player);
            }

            public void UpdateSkinId(BagProfile bagProfile)
            {
                foreach (var bagInfo in _bagToInfoMap.Values)
                {
                    if (bagInfo.BagProfile != bagProfile)
                        continue;

                    bagInfo.Item.skin = bagProfile.SkinId;
                    bagInfo.Item.MarkDirty();
                }
            }

            public float CalculateDespawnTime(ItemContainer container)
            {
                float despawnTime = ConVar.Server.itemdespawn_quick;

                foreach (var item in container.itemList)
                {
                    despawnTime = Math.Max(despawnTime, CalculateDespawnTime(item));
                }

                return despawnTime;
            }

            public float CalculateDespawnTime(Item item)
            {
                if (item.contents == null)
                    return item.GetDespawnDuration();

                var bagInfo = EnsureBagInfoIfEligible(item);
                if (bagInfo == null)
                    return item.GetDespawnDuration();

                return Math.Max(_config.DroppedBagSettings.MinimumDespawnTime, CalculateDespawnTime(item.contents));
            }

            public string GetBagStats(BagOfHolding plugin, IPlayer player, BagQuery bagQuery)
            {
                var totalBags = 0;
                var totalEmptyBags = 0;
                var totalUsedBags = 0;
                var totalCapacity = 0;
                var totalAvailableCapacity = 0;
                var totalOccupiedCapacity = 0;
                var totalBagsByProfile = new Dictionary<BagProfile, int>();
                var totalBagsByCategory = new Dictionary<int, int>();
                var totalBagsByLocationType = new Dictionary<BagQuery.BagLocation, int>();

                foreach (var bagInfo in _bagToInfoMap.Values)
                {
                    if (!bagQuery.Matches(plugin, bagInfo))
                        continue;

                    var bagItem = bagInfo.Item;
                    var amount = bagItem.amount;
                    totalBags += amount;

                    var capacity = bagItem.contents?.capacity ?? 0;
                    totalCapacity += capacity;

                    var occupiedCapacity = bagItem.contents?.itemList.Count ?? 0;
                    totalOccupiedCapacity += occupiedCapacity;
                    totalAvailableCapacity += capacity - occupiedCapacity;

                    totalEmptyBags += amount - 1;

                    if (bagInfo.HasItems)
                    {
                        // Only increment by one, since stacked bags are inherently empty.
                        totalUsedBags++;
                    }
                    else
                    {
                        totalEmptyBags++;
                    }

                    AddToDictKey(totalBagsByProfile, bagInfo.BagProfile, amount);
                    AddToDictKey(totalBagsByCategory, bagInfo.BagProfile.CategoryNumber, amount);
                    AddToDictKey(totalBagsByLocationType, BagQuery.GetBagLocation(plugin, bagInfo), amount);
                }

                var reportLines = new List<string>
                {
                    plugin.GetMessage(player.Id, LangEntry.StatsTotalBags, totalBags, totalUsedBags, totalEmptyBags),
                    plugin.GetMessage(player.Id, LangEntry.StatsTotalCapacity, totalCapacity, totalOccupiedCapacity, totalAvailableCapacity),
                };

                if (totalBagsByProfile.Count > 0)
                {
                    reportLines.Add(plugin.GetMessage(player.Id, LangEntry.StatsTotalBagsByProfile));
                    foreach (var (bagProfile, count) in totalBagsByProfile
                                 .OrderBy(entry => Array.IndexOf(plugin._config.BagProfiles, entry.Key)))
                    {
                        reportLines.Add($" - {bagProfile.Name}: {count}");
                    }
                }

                if (totalBagsByCategory.Count > 0)
                {
                    reportLines.Add(plugin.GetMessage(player.Id, LangEntry.StatsTotalBagsByCategory));
                    foreach (var (categoryNumber, count) in totalBagsByCategory
                                 .OrderBy(entry => entry.Key))
                    {
                        var categoryName = plugin._config.UniqueBagCategories[categoryNumber];
                        reportLines.Add($" - {categoryName}: {count}");
                    }
                }

                if (totalBagsByLocationType.Count > 0)
                {
                    reportLines.Add(plugin.GetMessage(player.Id, LangEntry.StatsTotalBagsByLocationType));
                    foreach (var (locationType, count) in totalBagsByLocationType
                                 .OrderBy(entry => entry.Key))
                    {
                        reportLines.Add($" - {Enum.GetName(typeof(BagQuery.BagLocation), locationType)}: {count}");
                    }
                }

                return string.Join("\n", reportLines);
            }
        }

        #endregion

        #region Bag Item Mod

        private static class ItemDefinitionUtils
        {
            public static void RegisterItemMod(ItemDefinition itemDefinition, ItemMod itemMod)
            {
                if (itemDefinition.itemMods.Contains(itemMod))
                    return;

                var originalLength = itemDefinition.itemMods.Length;
                Array.Resize(ref itemDefinition.itemMods, originalLength + 1);
                itemDefinition.itemMods[originalLength] = itemMod;
            }

            public static void UnregisterItemMod(ItemDefinition itemDefinition, ItemMod itemMod)
            {
                if (!itemDefinition.itemMods.Contains(itemMod))
                    return;

                itemDefinition.itemMods = itemDefinition.itemMods.Where(mod => mod != itemMod).ToArray();
            }
        }

        // This isn't strictly necessary to allow the bag to be worn, since the CanWearItem hook can handle that.
        // This is necessary to prevent NRE in item.MoveToContainer.
        private class ItemModWearableBag : ItemModWearable
        {
            private static readonly PropertyInfo ItemModWearableProperty = typeof(ItemDefinition).GetProperty("ItemModWearable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            private static readonly ItemDefinition VisualBackpackItemDefinition = ItemManager.FindItemDefinition(VisualBackpackItemId);

            public static ItemModWearableBag AddToItemDefinition(ItemDefinition itemDefinition)
            {
                var itemMod = itemDefinition.gameObject.AddComponent<ItemModWearableBag>();
                itemMod._hostItemDefinition = itemDefinition;
                itemMod.entityPrefab.guid = VisualBackpackItemDefinition.ItemModWearable.entityPrefab.guid;
                ItemDefinitionUtils.RegisterItemMod(itemDefinition, itemMod);

                if (ItemModWearableProperty != null)
                {
                    ItemModWearableProperty.SetValue(itemDefinition, itemMod);
                }

                return itemMod;
            }

            private ItemDefinition _hostItemDefinition;

            public void Destroy()
            {
                if (ItemModWearableProperty != null)
                {
                    ItemModWearableProperty.SetValue(_hostItemDefinition, null);
                }

                ItemDefinitionUtils.UnregisterItemMod(_hostItemDefinition, this);
                DestroyImmediate(this);
            }
        }

        // Extend from ItemModContainer to fix NREs in DroppedItem.UpdateItemMass since Rust backpacks update.
        private class ItemModContainerDummy : ItemModContainer
        {
            public static ItemModContainerDummy AddToItemDefinition(ItemDefinition itemDefinition)
            {
                var itemMod = itemDefinition.gameObject.AddComponent<ItemModContainerDummy>();
                itemMod._hostItemDefinition = itemDefinition;
                ItemDefinitionUtils.RegisterItemMod(itemDefinition, itemMod);
                return itemMod;
            }

            private ItemDefinition _hostItemDefinition;

            public void Destroy()
            {
                ItemDefinitionUtils.UnregisterItemMod(_hostItemDefinition, this);
                DestroyImmediate(this);
            }

            public override void OnItemCreated(Item item) {}
        }

        // This class is designed to be copied and pasted into multiple plugins that aim to replace the `ItemModUnwrap`
        // instance of a shared `ItemDefinition` instance.
        private abstract class BaseItemModUnwrapReplacer : ItemMod
        {
            private ItemDefinition _hostItemDefinition;
            private ItemModUnwrap _originalItemMod;

            public override void ModInit()
            {
                _hostItemDefinition = GetComponent<ItemDefinition>();
                _originalItemMod = GetComponent<ItemModUnwrap>();

                ItemDefinitionUtils.UnregisterItemMod(_hostItemDefinition, _originalItemMod);
                ItemDefinitionUtils.RegisterItemMod(_hostItemDefinition, this);
            }

            public virtual void Unload()
            {
                ItemDefinitionUtils.UnregisterItemMod(_hostItemDefinition, this);
                ItemDefinitionUtils.RegisterItemMod(_hostItemDefinition, _originalItemMod);

                DestroyImmediate(this);

                // Notify other item mods that the original item mod has been added back.
                // One of them will remove the `ItemModUnwrap` instance from the `ItemDefinition` instance.
                for (var i = _hostItemDefinition.itemMods.Length - 1; i >= 0; i--)
                {
                    _hostItemDefinition.itemMods[i].ModInit();
                }

                HandleUnload();
            }

            // Subclasses should override this to first run their custom checks/behavior, then call this.
            public sealed override void ServerCommand(Item item, string command, BasePlayer player)
            {
                if (command != "unwrap")
                    return;

                if (HandleUnwrap(item, player))
                    return;

                // Give other plugins the opportunity to block the vanilla behavior.
                // This technique is possible because vanilla doesn't call `ItemMod.Passes(Item)` for `ItemModUnwrap`.
                // The assumption is that other plugins will also have an `ItemMod` which implements `Passes(Item)`.
                foreach (var itemMod in item.info.itemMods)
                {
                    if (itemMod == this)
                        continue;

                    if (!itemMod.Passes(item))
                        return;
                }

                _originalItemMod.ServerCommand(item, command, player);
            }

            // Subclasses should return `false` for items specific to the plugin.
            public abstract override bool Passes(Item item);

            // Subclasses should return `true` when handling the event.
            public abstract bool HandleUnwrap(Item item, BasePlayer player);

            // Warning: Do not rename this because it is used for future instances of the plugin.
            // The intent is to allow this item mod to remain after the plugin has unloaded, to allow existing items to
            // function while the plugin is temporarily unloaded, so they don't revert to vanilla unwrap behavior.
            // The expectation is that when the plugin reloads, it will call this method via reflection (in case the
            // plugin recompiled) to clean up the old item mod so there are no double calls.
            public abstract void HandleUnload();
        }

        private class ItemModBagOfHolding : BaseItemModUnwrapReplacer
        {
            public static ItemModBagOfHolding AddToItemDefinition(ItemDefinition hostItemDefinition, BagOfHolding plugin)
            {
                if (hostItemDefinition == null)
                    throw new ArgumentNullException(nameof(hostItemDefinition));

                var itemModBagOfHolding = hostItemDefinition.gameObject.AddComponent<ItemModBagOfHolding>();
                itemModBagOfHolding.ModInit();

                itemModBagOfHolding._plugin = plugin;
                itemModBagOfHolding._bagManager = plugin._bagManager;
                itemModBagOfHolding._limitManager = plugin._limitManager;

                return itemModBagOfHolding;
            }

            private BagOfHolding _plugin;
            private BagManager _bagManager;
            private LimitManager _limitManager;

            private Configuration _config => _plugin._config;

            public override void HandleUnload()
            {
                if (_plugin._sharedLootEntity != null && !_plugin._sharedLootEntity.IsDestroyed)
                {
                    _plugin._sharedLootEntity.Kill();
                }

                if (_plugin._sharedCorpseEntity != null && !_plugin._sharedCorpseEntity.IsDestroyed)
                {
                    _plugin._sharedCorpseEntity.Kill();
                }

                if (_plugin._immortalProtection != null)
                {
                    Destroy(_plugin._immortalProtection);
                }
            }

            public override void OnItemCreated(Item item)
            {
                _bagManager.EnsureBagInfoIfEligible(item);
            }

            public override void OnRemove(Item item)
            {
                // Performance micro-optimization: Verify it's a skinned bag before doing hash lookup.
                if (!IsSkinnedBag(item))
                    return;

                var bagInfo = _bagManager.GetBagInfo(item);
                if (bagInfo == null)
                    return;

                _bagManager.HandleBagRemoved(bagInfo, item);
            }

            public override bool Passes(Item item)
            {
                // Return `false` for bag items to instruct other plugins to not run vanilla unwrap behavior.
                return _bagManager.EnsureBagInfoIfEligible(item) == null;
            }

            public override bool HandleUnwrap(Item item, BasePlayer player)
            {
                var bagInfo = _bagManager.EnsureBagInfoIfEligible(item);
                if (bagInfo != null)
                {
                    _plugin.AttemptOpenBag(player, bagInfo);
                    return true;
                }

                return false;
            }

            public override void OnMovedToWorld(Item item)
            {
                HandleBagMovedToWorld(item);
            }

            public override void OnParentChanged(Item item)
            {
                if (item.uid.Value == 0)
                {
                    // This is possible if `item.DoRemove()` is called while the item is inside a container, since that
                    // will first set `item.uid` to 0, then call `item.RemoveFromContainer()` which calls
                    // `item.SetParent(null)`. If this happens, it can be ignored because `OnRemove()` would already
                    // have been called via `item.Remove()`.
                    return;
                }

                // Registering a bag using `EnsureBagInfoIfEligible(Item)` instead of simply `GetBagInfo(Item)` in
                // `OnParentChanged(Item)` is necessary because it's possible to miss the item in `OnItemCreated(Item)`
                // when the item is initially created without a skin. Creating the item without a skin can happen
                // multiple ways, including via the vanilla `give` command, and via vanilla splitting logic.
                var bagInfo = _bagManager.EnsureBagInfoIfEligible(item);
                if (bagInfo == null)
                    return;

                if (item.parent != null)
                {
                    if (!bagInfo.HasEverBeenInAContainer)
                    {
                        bagInfo.MaybeUpdateBagProfile(_plugin);
                        bagInfo.HasEverBeenInAContainer = true;
                    }

                    _limitManager.HandleBagAddedToContainer(item.parent, bagInfo);
                }
                else if (bagInfo.Parent != null)
                {
                    _limitManager.HandleBagRemovedFromContainer(bagInfo.Parent, bagInfo);
                }

                HandleBagAncestorChanged(item, bagInfo, GetRootContainer(item));
                bagInfo.Parent = item.parent;
            }

            public void HandleBagMovedToWorld(Item item, bool delayRemovalTimeReset = false)
            {
                var bagInfo = _bagManager.EnsureBagInfoIfEligible(item);
                if (bagInfo == null)
                    return;

                var droppedItem = item.GetWorldEntity() as DroppedItem;
                if ((object)droppedItem == null || droppedItem.IsDestroyed)
                    return;

                bagInfo.SetGatherMode(GatherMode.None);
                _plugin.ResetRemovalTime(droppedItem, delayed: delayRemovalTimeReset);
                MaybeAddBuoyancy(droppedItem);
            }

            private bool HandleBagAncestorChanged(Item item, BagInfo bagInfo, ItemContainer rootContainer)
            {
                var changed = false;

                var shouldBlockItemInput = (item.parent?.PlayerItemInputBlocked() ?? false)
                    || !bagInfo.BagProfile.AllowsPlayerItemInput;

                if (TrySetContainerFlag(item.contents, ItemContainer.Flag.NoItemInput, shouldBlockItemInput))
                {
                    changed = true;
                }

                var previousAncestorPlayer = bagInfo.AncestorPlayer;

                if (rootContainer != null)
                {
                    if ((object)rootContainer.playerOwner != null)
                    {
                        item.contents.entityOwner = null;
                        item.contents.playerOwner = rootContainer.playerOwner;
                        bagInfo.AncestorPlayer = rootContainer.playerOwner;
                    }
                    else if ((object)rootContainer.entityOwner != null)
                    {
                        item.contents.entityOwner = rootContainer.entityOwner;
                        item.contents.playerOwner = null;
                        bagInfo.AncestorPlayer = null;
                    }
                }
                else
                {
                    // Don't set the entityOwner to the WorldEntity, since that would cause associated entities to be killed when the bag is picked up.
                    item.contents.entityOwner = null;
                    item.contents.playerOwner = null;
                    bagInfo.AncestorPlayer = null;
                }

                if ((object)previousAncestorPlayer == null && (object)bagInfo.AncestorPlayer == null)
                {
                    // Not moved to or from a player, so close looters, if any, just in case.
                    bagInfo.ForceCloseAllLooters();
                }
                else if ((object)bagInfo.AncestorPlayer == null)
                {
                    // Moved from a player.
                    var originalItem = item;

                    // Copy these variables so that the closure object only gets created if this block runs.
                    var bagInfo2 = bagInfo;
                    var previousAncestorPlayer2 = previousAncestorPlayer;

                    // The bag is possibly moving between separate containers in a player's inventory.
                    _plugin.NextTick(() =>
                    {
                        // Handle the case where the bag is removed (bagInfo is freed to pool, possibly reused by another bag).
                        if (bagInfo2.Item != originalItem)
                            return;

                        var currentRootContainer = GetRootContainer(originalItem);
                        var nextAncestorPlayer = currentRootContainer != null ? GetOwnerPlayer(currentRootContainer) : null;
                        if (nextAncestorPlayer == previousAncestorPlayer2)
                            return;

                        bagInfo2.ForceCloseAllLooters();
                    });
                }

                if (item.contents?.itemList != null)
                {
                    foreach (var childItem in item.contents.itemList)
                    {
                        if (childItem.contents != null)
                        {
                            var childBagInfo = _bagManager.EnsureBagInfoIfEligible(childItem);
                            if (childBagInfo != null)
                            {
                                if (HandleBagAncestorChanged(childItem, childBagInfo, rootContainer))
                                {
                                    changed = true;
                                }
                                continue;
                            }
                        }

                        // Update associated entities of children, so they parent to the new entityOwner/ownerPlayer.
                        // This fixes issues like sign content not being viewable.
                        foreach (var itemMod in childItem.info.itemMods)
                        {
                            // Skip ItemModEntity since that would parent entities to the world item which will display visually.
                            // That would not only be odd visually, it would cause the entities to be deleted when the world item is picked up.
                            if (itemMod is ItemModEntity)
                                continue;

                            itemMod.OnParentChanged(childItem);
                        }
                    }
                }

                return changed;
            }

            private BuoyancyPoint AddBuoyancyPoint(Rigidbody rigidBody, Buoyancy buoyancy, Vector3 localOffset)
            {
                var childObject = buoyancy.gameObject.CreateChild();
                childObject.name = "BuoyancyPoint";
                childObject.transform.localPosition = rigidBody.centerOfMass + localOffset;

                var buoyancyPoint = childObject.AddComponent<BuoyancyPoint>();
                buoyancyPoint.buoyancyForce = rigidBody.mass * (0f - Physics.gravity.y);
				buoyancyPoint.buoyancyForce *= 1.32f;
				buoyancyPoint.size = 0.2f;

                return buoyancyPoint;
            }

            private void MaybeAddBuoyancy(DroppedItem droppedItem)
            {
                if (!_config.DroppedBagSettings.EnableBuoyancy)
                    return;

                var rigidBody = droppedItem.GetComponent<Rigidbody>();
                if (rigidBody == null)
                    return;

                var buoyancy = droppedItem.GetComponent<Buoyancy>();
                if (buoyancy != null)
                    return;

                buoyancy = droppedItem.gameObject.AddComponent<Buoyancy>();
                buoyancy.rigidBody = rigidBody;
                buoyancy.buoyancyScale = 1f;
                buoyancy.useUnderwaterDrag = true;
                buoyancy.underwaterDrag = 2f;

                buoyancy.points = new[]
                {
                    AddBuoyancyPoint(rigidBody, buoyancy, new Vector3(0f, -0.35f, 0f)),
                };

                buoyancy.SavePointData();
            }
        }

        #endregion

        #region Configuration

        [JsonObject(MemberSerialization.OptIn)]
        private class PlayerLimitProfile : LimitProfile
        {
            public const string LimitPrefix = "bagofholding.limit.player";

            [JsonIgnore]
            protected override string PermissionPrefix => "player";
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class WearableLimitProfile : LimitProfile
        {
            public const string LimitPrefix = "bagofholding.limit.wearable";

            [JsonIgnore]
            protected override string PermissionPrefix => "wearable";
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class ContainerLimitProfile : LimitProfile
        {
            public const string LimitPrefix = "bagofholding.limit.container";

            [JsonIgnore]
            protected override string PermissionPrefix => "container";
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class BackpackLimitProfile : LimitProfile
        {
            public const string LimitPrefix = "bagofholding.limit.backpack";

            [JsonIgnore]
            protected override string PermissionPrefix => "backpack";
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class ExtraPocketsLimitProfile : LimitProfile
        {
            [JsonIgnore]
            protected override string PermissionPrefix => "extrapockets";
        }

        [JsonObject(MemberSerialization.OptIn)]
        private abstract class PermissionLimitSet<T> where T : LimitProfile
        {
            public abstract T DefaultLimitProfile { get; set; }

            public abstract T[] LimitProfilesByPermission { get; set; }

            [JsonIgnore]
            private Permission _permission;

            public void Init(BagOfHolding plugin, Configuration config, string permissionInfix = null)
            {
                _permission = plugin.permission;

                DefaultLimitProfile?.Init(plugin, config);

                foreach (var limitProfile in LimitProfilesByPermission)
                {
                    limitProfile.Init(plugin, config, permissionInfix);
                }
            }

            public T GetPlayerLimitProfile(string playerId)
            {
                var userData = _permission.GetUserData(playerId);

                for (var i = LimitProfilesByPermission.Length - 1; i >= 0; i--)
                {
                    var profile = LimitProfilesByPermission[i];
                    if (profile.Permission != null && UserHasPermission(_permission, userData, profile.Permission))
                        return profile;
                }

                return DefaultLimitProfile;
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class PlayerBagLimits : PermissionLimitSet<PlayerLimitProfile>
        {
            [JsonProperty("Default limits")]
            public override PlayerLimitProfile DefaultLimitProfile { get; set; } = new()
            {
                MaxTotalBags = 3,
            };

            [JsonProperty("Bag limits by permission")]
            public override PlayerLimitProfile[] LimitProfilesByPermission { get; set; } =
            {
                new()
                {
                    PermissionSuffix = "unlimited",
                    MaxTotalBags = -1,
                },
            };
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class WearableBagLimits : PermissionLimitSet<WearableLimitProfile>
        {
            [JsonProperty("Enabled")]
            public bool Enabled;

            [JsonProperty("Default limits")]
            public override WearableLimitProfile DefaultLimitProfile { get; set; } = new()
            {
                MaxTotalBags = 3,
            };

            [JsonProperty("Bag limits by permission")]
            public override WearableLimitProfile[] LimitProfilesByPermission { get; set; } =
            {
                new WearableLimitProfile
                {
                    PermissionSuffix = "unlimited",
                    MaxTotalBags = -1,
                },
            };
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class BackpackBagLimits : PermissionLimitSet<BackpackLimitProfile>
        {
            [JsonProperty("Default limits")]
            public override BackpackLimitProfile DefaultLimitProfile { get; set; } = new();

            [JsonProperty("Bag limits by permission")]
            public override BackpackLimitProfile[] LimitProfilesByPermission { get; set; } =
            {
                new()
                {
                    PermissionSuffix = "unlimited",
                    MaxTotalBags = -1,
                },
            };
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class ExtraPocketsBagLimits : PermissionLimitSet<ExtraPocketsLimitProfile>
        {
            [JsonProperty("Default limits")]
            public override ExtraPocketsLimitProfile DefaultLimitProfile { get; set; } = new();

            [JsonProperty("Bag limits by permission")]
            public override ExtraPocketsLimitProfile[] LimitProfilesByPermission { get; set; } =
            {
                new()
                {
                    PermissionSuffix = "unlimited",
                    MaxTotalBags = -1,
                },
            };
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class ContainerBagLimits : PermissionLimitSet<ContainerLimitProfile>
        {
            [JsonProperty("Default limits")]
            public override ContainerLimitProfile DefaultLimitProfile { get; set; } = new();

            [JsonProperty("Bag limits by permission")]
            public override ContainerLimitProfile[] LimitProfilesByPermission { get; set; } =
            {
                new()
                {
                    PermissionSuffix = "unlimited",
                    MaxTotalBags = -1,
                },
            };
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class ContainerPrefabBagLimits : PermissionLimitSet<ContainerLimitProfile>
        {
            [JsonProperty("Default limits")]
            public override ContainerLimitProfile DefaultLimitProfile { get; set; } = new();

            [JsonProperty("Bag limits by permission")]
            public override ContainerLimitProfile[] LimitProfilesByPermission { get; set; } = Array.Empty<ContainerLimitProfile>();
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class BagContentsRuleset
        {
            [JsonProperty("Name")]
            public string Name;

            [JsonProperty("Allowed item categories")]
            public string[] AllowedItemCategoryNames = Array.Empty<string>();

            [JsonProperty("Disallowed item categories")]
            public string[] DisallowedItemCategoryNames = Array.Empty<string>();

            [JsonProperty("Allowed item short names")]
            public string[] AllowedItemShortNames = Array.Empty<string>();

            [JsonProperty("Disallowed item short names")]
            public string[] DisallowedItemShortNames = Array.Empty<string>();

            [JsonProperty("Allowed skin IDs")]
            public HashSet<ulong> AllowedSkinIds = new();

            [JsonProperty("Disallowed skin IDs")]
            public HashSet<ulong> DisallowedSkinIds = new();

            [JsonProperty("Bag limits")]
            public LimitProfile LimitProfile = new();

            [JsonIgnore]
            private ItemCategory[] AllowedItemCategories;

            [JsonIgnore]
            private ItemCategory[] DisallowedItemCategories;

            [JsonIgnore]
            private HashSet<int> _allowedItemIds = new();

            [JsonIgnore]
            private HashSet<int> _disallowedItemIds = new();

            public void Init(BagOfHolding plugin, Configuration config)
            {
                LimitProfile.Init(plugin, config);

                var errorFormat = $"Invalid item category defined for \"{Name}\" ruleset: \"{{0}}\"";
                AllowedItemCategories = ParseEnumList<ItemCategory>(AllowedItemCategoryNames, errorFormat);
                DisallowedItemCategories = ParseEnumList<ItemCategory>(DisallowedItemCategoryNames, errorFormat);

                foreach (var itemShortName in AllowedItemShortNames)
                {
                    var itemDefinition = ItemManager.FindItemDefinition(itemShortName);
                    if (itemDefinition != null)
                    {
                        _allowedItemIds.Add(itemDefinition.itemid);
                    }
                    else
                    {
                        LogWarning($"Invalid item short name in config: {itemShortName}");
                    }
                }

                foreach (var itemShortName in DisallowedItemShortNames)
                {
                    var itemDefinition = ItemManager.FindItemDefinition(itemShortName);
                    if (itemDefinition != null)
                    {
                        _disallowedItemIds.Add(itemDefinition.itemid);
                    }
                    else
                    {
                        LogWarning($"Invalid item short name in config: {itemShortName}");
                    }
                }
            }

            public bool AllowsItem(Item item)
            {
                if (DisallowedSkinIds.Contains(item.skin))
                    return false;

                if (AllowedSkinIds.Contains(item.skin))
                    return true;

                if (_disallowedItemIds.Contains(item.info.itemid))
                    return false;

                if (_allowedItemIds.Contains(item.info.itemid))
                    return true;

                if (DisallowedItemCategories.Contains(item.info.category))
                    return false;

                if (AllowedItemCategories.Contains(item.info.category))
                    return true;

                return AllowedItemCategories.Contains(ItemCategory.All);
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class ItemInfo
        {
            [JsonProperty("Item short name", Order = 0)]
            public string ShortName = "scrap";

            [JsonProperty("Item skin ID", Order = 1)]
            public ulong SkinId { get; set; }

            [JsonProperty("Amount", Order = 2)]
            public int Amount;

            [JsonIgnore]
            public ItemDefinition ItemDefinition { get; private set; }

            [JsonIgnore]
            public int ItemId => ItemDefinition?.itemid ?? 0;

            public void Init()
            {
                if (!string.IsNullOrWhiteSpace(ShortName))
                {
                    ItemDefinition = ItemManager.FindItemDefinition(ShortName);
                    if (ItemDefinition == null)
                    {
                        LogWarning($"Invalid item short name in config: {ShortName}");
                    }
                }
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class CostInfo : ItemInfo, IPaymentInfo
        {
            [JsonProperty("Use Economics", Order = 3)]
            public bool UseEconomics { get; set; }

            [JsonProperty("Use Server Rewards", Order = 4)]
            public bool UseServerRewards { get; set; }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private abstract class BasePermissionProfile
        {
            [JsonIgnore]
            public string Permission { get; private set; }

            protected void Init(BagOfHolding plugin, string permissionSuffix)
            {
                if (!string.IsNullOrWhiteSpace(permissionSuffix))
                {
                    Permission = $"{nameof(BagOfHolding)}.{permissionSuffix}".ToLower();
                    plugin.permission.RegisterPermission(Permission, plugin);
                }
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class BagUpgradeTarget : BasePermissionProfile
        {
            [JsonProperty("To")]
            public string TargetName;

            [JsonProperty("Cost")]
            public CostInfo Cost = new()
            {
                Amount = 100,
                ShortName = "scrap",
            };

            [JsonIgnore]
            public BagProfile TargetProfile;

            [JsonIgnore]
            public bool IsValid => Cost != null && TargetProfile != null;

            public void Init(BagOfHolding plugin, Configuration config, BagProfile bagProfile)
            {
                if (string.IsNullOrWhiteSpace(TargetName))
                    return;

                TargetProfile = config.GetBagProfileByName(TargetName);
                if (TargetProfile == null)
                    return;

                base.Init(plugin, $"upgrade.{bagProfile.Name}");
                Cost.Init();
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class RecycleIngredient : ItemInfo
        {
            [JsonProperty("Display name")]
            private string DeprecatedDisplayName { set => DisplayName = value; }

            [JsonProperty("Item display name", Order = 5)]
            public string DisplayName = "";
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class RecycleInfo
        {
            [JsonProperty("Enabled")]
            public bool Enabled;

            [JsonProperty("Ingredients")]
            public RecycleIngredient[] Ingredients = Array.Empty<RecycleIngredient>();
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class BagProfile
        {
            public const string NameField = "Name";
            public const string DisplayNameField = "Display name";

            [JsonProperty(NameField)]
            public string Name;

            [JsonProperty("Skin ID")]
            public ulong SkinId;

            [JsonProperty("Capacity")]
            public int Capacity;

            [JsonProperty(DisplayNameField)]
            public string DisplayName;

            [JsonProperty("Category name")]
            public string CategoryName;

            [JsonProperty("Contents ruleset")]
            public string ContentsRulesetName;

            [JsonProperty("Stack profile", DefaultValueHandling = DefaultValueHandling.Ignore)]
            [DefaultValue("")]
            public string StackProfileName = "";

            [JsonProperty("Allow player item input", DefaultValueHandling = DefaultValueHandling.Ignore)]
            [DefaultValue(true)]
            public bool AllowsPlayerItemInput = true;

            [JsonProperty("Upgrade", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public BagUpgradeTarget UpgradeTarget;

            [JsonProperty("Recyclable")]
            public RecycleInfo RecycleInfo = new();

            [JsonIgnore]
            public BagContentsRuleset ContentsRuleset { get; private set; }

            [JsonIgnore]
            public StackProfile StackProfile { get; private set; }

            [JsonIgnore]
            public int CategoryNumber { get; private set; } = -1;

            [JsonIgnore]
            public bool UpgradesToSameCategory =>
                CategoryNumber == (UpgradeTarget?.TargetProfile?.CategoryNumber ?? -1);

            [JsonIgnore]
            public bool CanBeRecycled
            {
                get
                {
                    if (RecycleInfo == null || !RecycleInfo.Enabled)
                        return false;

                    return RecycleInfo.Ingredients?.Length > 0;
                }
            }

            public void Init(BagOfHolding plugin, Configuration config)
            {
                UpgradeTarget?.Init(plugin, config, this);

                ContentsRuleset = config.FindRuleset(ContentsRulesetName);
                if (ContentsRuleset == null)
                {
                    LogError($"Bag profile \"{Name}\" refers to a contents ruleset named \"{ContentsRulesetName}\" which does not exist. Did you type the name correctly? A contents ruleset must first be defined in the config before it can be referenced in a bag profile.");
                }

                if (!string.IsNullOrWhiteSpace(StackProfileName))
                {
                    StackProfile = config.FindStackProfile(StackProfileName);
                    if (StackProfile == null)
                    {
                        LogError($"Bag profile \"{Name}\" refers to a stack profile named \"{StackProfileName}\" which does not exist. Did you type the name correctly? A stack profile must first be defined in the config before it can be referenced in a bag profile.");
                    }
                }

                if (!string.IsNullOrWhiteSpace(CategoryName))
                {
                    CategoryNumber = config.UniqueBagCategories.IndexOf(CategoryName);
                    if (CategoryNumber == -1)
                    {
                        CategoryNumber = config.UniqueBagCategories.Count;
                        config.UniqueBagCategories.Add(CategoryName);
                    }
                }

                if (RecycleInfo is { Enabled: true, Ingredients: { } })
                {
                    foreach (var ingredientInfo in RecycleInfo.Ingredients)
                    {
                        ingredientInfo.Init();
                    }
                }
            }

            public bool MatchesNameOrDisplayName(string profileName)
            {
                return profileName == null
                    || StringUtils.Contains(Name, profileName)
                    || StringUtils.Contains(DisplayName, profileName);
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class BeltActivation
        {
            [JsonProperty("Enabled")]
            public bool Enabled = true;

            [JsonProperty("Open delay (seconds)")]
            public float DelaySeconds = 0.2f;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class DroppedBagSettings
        {
            [JsonProperty("Enable dropped bag buoyancy")]
            public bool EnableBuoyancy = true;

            [JsonProperty("Enable opening dropped bags while at bag limit")]
            public bool EnableOpeningDroppedBags = true;

            [JsonProperty("Auto equip dropped bags while at bag limit")]
            public bool AutoEquipDroppedBags = true;

            [JsonProperty("Minimum despawn time (seconds)")]
            public int MinimumDespawnTime = 1800;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class BeltUISettings
        {
            [JsonProperty("Enabled")]
            public bool Enabled = true;

            [JsonProperty("Only show while bags are worn")]
            public bool OnlyShowWhileBagsAreWorn;

            [JsonProperty("OffsetX")]
            public float OffsetX = 185;

            [JsonProperty("Display skin ID")]
            public ulong SkinId = 2824115881;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class BagSelectorUISettings
        {
            [JsonProperty("Enabled")]
            public bool Enabled = true;

            [JsonProperty("Max width")]
            public float MaxWidth = 310;

            [JsonProperty("Min bag size")]
            public float MinBagSize = 29;

            [JsonProperty("Max bag size")]
            public float MaxBagSize = 58;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class BagUISettings
        {
            [JsonProperty("Enable back button")]
            public bool EnableBackButton = true;

            [JsonProperty("Bag selector")]
            public BagSelectorUISettings BagSelector = new();
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class UISettings
        {
            [JsonProperty("Belt button")]
            public BeltUISettings BeltButton = new();

            [JsonProperty("Loot interface")]
            public BagUISettings LootInterface = new();
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class EffectsConfig
        {
            [JsonProperty("Open effect")]
            public string OpenEffect = "assets/prefabs/deployable/small stash/effects/small-stash-deploy.prefab";

            [JsonProperty("Upgrade effect")]
            public string UpgradeEffect = "assets/prefabs/misc/halloween/lootbag/effects/silver_open.prefab";
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class LootConfig
        {
            [JsonProperty("Enabled")]
            public bool Enabled;

            [JsonProperty("Bag spawn chance percent by prefab")]
            public Dictionary<string, Dictionary<string, float>> SpawnChanceByPrefabName = new();

            [JsonIgnore]
            public Dictionary<uint, Dictionary<BagProfile, float>> SpawnChanceByPrefabId = new();

            public void OnServerInitialize(Configuration config)
            {
                foreach (var (prefabPath, spawnChanceByBagProfileName) in SpawnChanceByPrefabName)
                {
                    var prefabId = StringPool.Get(prefabPath);
                    if (prefabId == 0)
                    {
                        LogError($"Invalid loot container prefab in configuration: {prefabPath}");
                        continue;
                    }

                    var spawnChanceByBagProfile = new Dictionary<BagProfile, float>();

                    foreach (var (profileName, percentChance) in spawnChanceByBagProfileName)
                    {
                        var bagProfile = config.GetBagProfileByName(profileName);
                        if (bagProfile == null)
                        {
                            LogError($"Invalid bag profile: \"{profileName}\"");
                            continue;
                        }

                        spawnChanceByBagProfile[bagProfile] = percentChance;
                    }

                    if (spawnChanceByBagProfile.Count > 0)
                    {
                        SpawnChanceByPrefabId[prefabId] = spawnChanceByBagProfile;
                    }
                }
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class UpgradeCostOverride : BasePermissionProfile
        {
            [JsonProperty("Permission suffix")]
            public string PermissionSuffix;

            [JsonProperty("Upgrade cost by bag profile")]
            public Dictionary<string, int> UpgradeCostByProfileName = new();

            [JsonIgnore]
            private Dictionary<BagProfile, int> UpgradeCostByProfile = new();

            public void Init(BagOfHolding plugin, Configuration config)
            {
                foreach (var (profileName, cost) in UpgradeCostByProfileName)
                {
                    var bagProfile = config.GetBagProfileByName(profileName);
                    if (bagProfile == null)
                    {
                        LogError($"Invalid bag profile: \"{profileName}\"");
                        continue;
                    }

                    UpgradeCostByProfile[bagProfile] = cost;
                }

                if (UpgradeCostByProfile.Count != 0)
                {
                    base.Init(plugin, $"upgradecost.{PermissionSuffix}");
                }
            }

            public int GetOverrideForProfile(BagProfile bagProfile)
            {
                return UpgradeCostByProfile.TryGetValue(bagProfile, out var amount)
                    ? amount
                    : -1;
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class StackProfile
        {
            [JsonProperty("Name")]
            public string Name;

            [JsonProperty("Global max stack size")]
            public int GlobalMaxStackSize;

            [JsonProperty("Stack size by item short name")]
            private Dictionary<string, int> StackSizeByItemShortName = new();

            [JsonProperty("Stack size by skin ID")]
            private Dictionary<ulong, int> StackSizeBySkinId = new();

            [JsonIgnore]
            private Dictionary<int, object> StackSizeObjectsByItemId = new();

            [JsonIgnore]
            private Dictionary<ulong, object> StackSizeObjectsBySkinId = new();

            [JsonIgnore]
            public bool ContainsItemOverrides { get; private set; }

            public void Init()
            {
                foreach (var entry in StackSizeByItemShortName)
                {
                    var itemDefinition = ItemManager.FindItemDefinition(entry.Key);
                    if (itemDefinition == null)
                    {
                        LogWarning($"Invalid item short name in config: {entry.Key}");
                        continue;
                    }

                    StackSizeObjectsByItemId[itemDefinition.itemid] = entry.Value;
                    ContainsItemOverrides = true;
                }

                foreach (var entry in StackSizeBySkinId)
                {
                    StackSizeObjectsBySkinId[entry.Key] = entry.Value;
                }

                if (StackSizeBySkinId.Count > 0)
                {
                    ContainsItemOverrides = true;
                }
            }

            public object GetStackSizeOverride(Item item)
            {
                if (item.skin != 0 && StackSizeObjectsBySkinId.TryGetValue(item.skin, out var stackSize))
                    return stackSize;

                if (StackSizeObjectsByItemId.TryGetValue(item.info.itemid, out stackSize))
                    return stackSize;

                return null;
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class GatherModeSettings
        {
            [JsonProperty("Enabled")]
            public bool Enabled;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class WearableSettings
        {
            public const string AllowInBackpackSlotField = "Allow wearing bags in the backpack slot";
            public const string AllowInNonBackpackSlotField = "Allow wearing bags in non-backpack slots";

            [JsonProperty(AllowInBackpackSlotField)]
            public bool AllowInBackpackSlot;

            [JsonProperty(AllowInNonBackpackSlotField)]
            public bool AllowInNonBackpackSlot;

            public bool Enabled => AllowInBackpackSlot || AllowInNonBackpackSlot;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class VisualBackpackSettings
        {
            public const string DisplayWhileInBackpackSlotField = "Display while a bag is worn in the backpack slot";
            public const string DisplayWhileInNonBackpackSlotField = "Display while a bag is worn in a non-backpack slot";

            [JsonProperty(DisplayWhileInBackpackSlotField)]
            public bool DisplayWhileInBackpackSlot;

            [JsonProperty(DisplayWhileInNonBackpackSlotField)]
            public bool DisplayWhileInNonBackpackSlot;

            public bool Enabled => DisplayWhileInBackpackSlot || DisplayWhileInNonBackpackSlot;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class Configuration : BaseConfiguration
        {
            [JsonProperty("Wearable bags")]
            public WearableSettings Wearable = new();

            [JsonProperty("Visual backpack")]
            public VisualBackpackSettings VisualBackpack = new();

            [JsonProperty("Gather mode")]
            public GatherModeSettings GatherMode = new();

            [JsonProperty("Enable gather mode")]
            private bool DeprecatedEnableGatherMode { set => GatherMode.Enabled = value; }

            [JsonProperty("Enable stack protection", DefaultValueHandling = DefaultValueHandling.Ignore)]
            [DefaultValue(true)]
            public bool EnableStackProtection = true;

            [JsonProperty("Enable stack improvements")]
            public bool EnableStackImprovements = true;

            [JsonProperty("Enable item short name redirects (experimental)", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public bool EnableItemShortNameRedirects = false;

            #if FEATURE_BELT_ACTIVATION
            [JsonProperty("Belt activation")]
            public BeltActivation BeltActivation = new();
            #endif

            [JsonProperty("Dropped bag item settings")]
            public DroppedBagSettings DroppedBagSettings = new();

            [JsonProperty("UI settings")]
            public UISettings UISettings = new();

            [JsonProperty("Effects")]
            public EffectsConfig Effects = new();

            [JsonProperty("Loot spawns", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public LootConfig LootConfig = new()
            {
                Enabled = false,
                SpawnChanceByPrefabName = new Dictionary<string, Dictionary<string, float>>
                {
                    ["assets/bundled/prefabs/radtown/crate_normal_2.prefab"] = new()
                    {
                        ["weapons_tools.xxsmall"] = 0.1f,
                        ["armor_clothing.xxsmall"] = 0.1f,
                        ["items_construction.xxsmall"] = 0.1f,
                        ["resources_components.xxsmall"] = 0.1f,
                        ["generic.xxsmall"] = 0.1f,
                    },
                    ["assets/bundled/prefabs/radtown/crate_normal.prefab"] = new()
                    {
                        ["weapons_tools.small"] = 0.2f,
                        ["armor_clothing.small"] = 0.2f,
                        ["items_construction.small"] = 0.2f,
                        ["resources_components.small"] = 0.2f,
                        ["generic.small"] = 0.2f,
                    },
                    ["assets/bundled/prefabs/radtown/foodbox.prefab"] = new()
                    {
                        ["food_medical.xxsmall"] = 1.0f,
                    },
                    ["assets/bundled/prefabs/radtown/crate_normal_2_food.prefab"] = new()
                    {
                        ["food_medical.xxsmall"] = 1.0f,
                    },
                    ["assets/bundled/prefabs/radtown/crate_normal_2_medical.prefab"] = new()
                    {
                        ["food_medical.xxsmall"] = 1.0f,
                    },
                    ["assets/bundled/prefabs/radtown/crate_elite.prefab"] = new()
                    {
                        ["bagofholding"] = 1f,
                    },
                    ["assets/prefabs/npc/m2bradley/bradley_crate.prefab"] = new()
                    {
                        ["bagofholding"] = 1f,
                    },
                    ["assets/prefabs/npc/patrol helicopter/heli_crate.prefab"] = new()
                    {
                        ["bagofholding"] = 1f,
                    },
                },
            };

            [JsonProperty("Player bag limits")]
            public PlayerBagLimits PlayerBagLimits = new();

            [JsonProperty("Player wearable bag limits")]
            public WearableBagLimits WearableBagLimits = new();

            [JsonProperty("Backpack bag limits")]
            public BackpackBagLimits BackpackBagLimits = new();

            #if FEATURE_EXTRA_POCKETS_CONFIG
            [JsonProperty("Extra Pockets bag limits")]
            public ExtraPocketsBagLimits ExtraPocketsBagLimits = new();
            #endif

            [JsonProperty("Default container bag limits")]
            public ContainerBagLimits ContainerBagLimits = new();

            [JsonProperty("Container bag limits")]
            private ContainerBagLimits DeprecatedContainerBagLimits { set => ContainerBagLimits = value; }

            [JsonProperty("Container bag limits by prefab")]
            public Dictionary<string, ContainerPrefabBagLimits> ContainerBagLimitsByPrefabName = new();

            [JsonProperty("Upgrade cost overrides by permission")]
            public UpgradeCostOverride[] UpgradeCostOverrides = Array.Empty<UpgradeCostOverride>();

            public bool ShouldSerializeUpgradeCostOverrides() => UpgradeCostOverrides?.Length > 0;

            [JsonProperty("Bag content rulesets")]
            public BagContentsRuleset[] BagContentRulesets =
            {
                new()
                {
                    Name = "unrestricted",
                    AllowedItemCategoryNames = new[]
                    {
                        ItemCategory.All.ToString(),
                    },
                    LimitProfile = new LimitProfile
                    {
                        MaxTotalBags = -1,
                    },
                },
                new()
                {
                    Name = "bagofholding",
                    AllowedItemCategoryNames = new[]
                    {
                        ItemCategory.All.ToString(),
                    },
                    LimitProfile = new LimitProfile
                    {
                        MaxTotalBags = 6,
                        MaxBagsByCategoryName = new Dictionary<string, int>
                        {
                            ["bagofholding"] = 0,
                        },
                    },
                },
                new()
                {
                    Name = "generic",
                    AllowedItemCategoryNames = new[]
                    {
                        ItemCategory.All.ToString(),
                    },
                },
                new()
                {
                    Name = "armor_clothing",
                    AllowedItemCategoryNames = new[]
                    {
                        ItemCategory.Attire.ToString(),
                    },
                },
                new()
                {
                    Name = "food_medical",
                    AllowedItemCategoryNames = new[]
                    {
                        ItemCategory.Food.ToString(),
                        ItemCategory.Medical.ToString(),
                    },
                    AllowedItemShortNames = new[]
                    {
                        "botabag",
                        "easter.bronzeegg",
                        "easter.goldegg",
                        "easter.paintedeggs",
                        "easter.silveregg",
                        "halloween.candy",
                        "halloween.lootbag.large",
                        "halloween.lootbag.medium",
                        "halloween.lootbag.small",
                        "xmas.present.large",
                        "xmas.present.medium",
                        "xmas.present.small",
                    },
                },
                new()
                {
                    Name = "items_construction",
                    AllowedItemCategoryNames = new[]
                    {
                        ItemCategory.Construction.ToString(),
                        ItemCategory.Electrical.ToString(),
                        ItemCategory.Items.ToString(),
                        ItemCategory.Traps.ToString(),
                    },
                    AllowedItemShortNames = new[]
                    {
                        "abovegroundpool",
                        "beachchair",
                        "beachparasol",
                        "beachtable",
                        "beachtowel",
                        "boogieboard",
                        "boombox",
                        "cctv.camera",
                        "coffin.storage",
                        "connected.speaker",
                        "cursedcauldron",
                        "discoball",
                        "discofloor.largetiles",
                        "discofloor",
                        "drumkit",
                        "firework.boomer.blue",
                        "firework.boomer.champagne",
                        "firework.boomer.green",
                        "firework.boomer.orange",
                        "firework.boomer.pattern",
                        "firework.boomer.red",
                        "firework.boomer.violet",
                        "firework.romancandle.blue",
                        "firework.romancandle.green",
                        "firework.romancandle.red",
                        "firework.romancandle.violet",
                        "firework.volcano.red",
                        "firework.volcano.violet",
                        "firework.volcano",
                        "fogmachine",
                        "giantcandycanedecor",
                        "giantlollipops",
                        "gravestone",
                        "innertube.horse",
                        "innertube.unicorn",
                        "innertube",
                        "largecandles",
                        "laserlight",
                        "microphonestand",
                        "newyeargong",
                        "piano",
                        "rustige_egg_a",
                        "rustige_egg_b",
                        "rustige_egg_c",
                        "rustige_egg_d",
                        "rustige_egg_e",
                        "skull.trophy.jar",
                        "skull.trophy.jar2",
                        "skull.trophy.table",
                        "skull.trophy",
                        "skullspikes.candles",
                        "skullspikes.pumpkin",
                        "skullspikes",
                        "skylantern.skylantern.green",
                        "skylantern.skylantern.orange",
                        "skylantern.skylantern.purple",
                        "skylantern.skylantern.red",
                        "skylantern",
                        "sled.xmas",
                        "sled",
                        "smallcandles",
                        "snowmachine",
                        "soundlight",
                        "spiderweb",
                        "spookyspeaker",
                        "strobelight",
                        "telephone",
                        "wall.graveyard.fence",
                        "woodcross",
                        "xmas.decoration.baubels",
                        "xmas.decoration.candycanes",
                        "xmas.decoration.gingerbreadmen",
                        "xmas.decoration.lights",
                        "xmas.decoration.pinecone",
                        "xmas.decoration.star",
                        "xmas.decoration.tinsel",
                        "xylophone",
                    },
                },
                new()
                {
                    Name = "resources_components",
                    AllowedItemCategoryNames = new[]
                    {
                        ItemCategory.Component.ToString(),
                        ItemCategory.Resources.ToString(),
                    },
                },
                new()
                {
                    Name = "weapons_tools",
                    AllowedItemCategoryNames = new[]
                    {
                        ItemCategory.Ammunition.ToString(),
                        ItemCategory.Medical.ToString(),
                        ItemCategory.Tool.ToString(),
                        ItemCategory.Weapon.ToString(),
                    },
                    AllowedItemShortNames = new[]
                    {
                        "easterbasket",
                        "fun.bass",
                        "fun.boomboxportable",
                        "fun.casetterecorder",
                        "fun.cowbell",
                        "fun.flute",
                        "fun.guitar",
                        "fun.jerrycanguitar",
                        "fun.tambourine",
                        "fun.trumpet",
                        "fun.tuba",
                        "hosetool",
                        "keycard_blue",
                        "keycard_green",
                        "keycard_red",
                        "megaphone",
                        "mobilephone",
                        "pumpkinbasket",
                        "rf_pager",
                        "sickle",
                        "wiretool",
                    },
                },
            };

            [JsonProperty("Stack profiles")]
            public StackProfile[] StackProfiles = Array.Empty<StackProfile>();

            public bool ShouldSerializeStackProfiles() => StackProfiles?.Length > 0;

            [JsonProperty("Bag profiles")]
            public BagProfile[] BagProfiles =
            {
                // Bag of Holding
                new()
                {
                    Name = "bagofholding",
                    SkinId = 2824136143,
                    Capacity = 48,
                    DisplayName = "Bag of Holding",
                    CategoryName = "bagofholding",
                    ContentsRulesetName = "bagofholding",
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 1500 } },
                    },
                },

                // Kit Bags
                new()
                {
                    Name = "kitbag.red.unlocked",
                    SkinId = 2830974120,
                    Capacity = 42,
                    DisplayName = "Kit Bag (Editable)",
                    CategoryName = "kitbag",
                    ContentsRulesetName = "unrestricted",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "kitbag.red.locked",
                        Cost = new CostInfo { Amount = 0 },
                    },
                },
                new()
                {
                    Name = "kitbag.red.locked",
                    SkinId = 2830952942,
                    Capacity = 42,
                    DisplayName = "Kit Bag",
                    CategoryName = "kitbag",
                    ContentsRulesetName = "unrestricted",
                    AllowsPlayerItemInput = false,
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "kitbag.red.unlocked",
                        Cost = new CostInfo { Amount = 0 },
                    },
                },

                new()
                {
                    Name = "kitbag.orange.unlocked",
                    SkinId = 2830974662,
                    Capacity = 42,
                    DisplayName = "Kit Bag (Editable)",
                    CategoryName = "kitbag",
                    ContentsRulesetName = "unrestricted",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "kitbag.orange.locked",
                        Cost = new CostInfo { Amount = 0 },
                    },
                },
                new()
                {
                    Name = "kitbag.orange.locked",
                    SkinId = 2830963984,
                    Capacity = 42,
                    DisplayName = "Kit Bag",
                    CategoryName = "kitbag",
                    ContentsRulesetName = "unrestricted",
                    AllowsPlayerItemInput = false,
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "kitbag.orange.unlocked",
                        Cost = new CostInfo { Amount = 0 },
                    },
                },

                new()
                {
                    Name = "kitbag.yellow.unlocked",
                    SkinId = 2830975150,
                    Capacity = 42,
                    DisplayName = "Kit Bag (Editable)",
                    CategoryName = "kitbag",
                    ContentsRulesetName = "unrestricted",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "kitbag.yellow.locked",
                        Cost = new CostInfo { Amount = 0 },
                    },
                },
                new()
                {
                    Name = "kitbag.yellow.locked",
                    SkinId = 2830964495,
                    Capacity = 42,
                    DisplayName = "Kit Bag",
                    CategoryName = "kitbag",
                    ContentsRulesetName = "unrestricted",
                    AllowsPlayerItemInput = false,
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "kitbag.yellow.unlocked",
                        Cost = new CostInfo { Amount = 0 },
                    },
                },

                new()
                {
                    Name = "kitbag.green.unlocked",
                    SkinId = 2830975690,
                    Capacity = 42,
                    DisplayName = "Kit Bag (Editable)",
                    CategoryName = "kitbag",
                    ContentsRulesetName = "unrestricted",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "kitbag.green.locked",
                        Cost = new CostInfo { Amount = 0 },
                    },
                },
                new()
                {
                    Name = "kitbag.green.locked",
                    SkinId = 2830965006,
                    Capacity = 42,
                    DisplayName = "Kit Bag",
                    CategoryName = "kitbag",
                    ContentsRulesetName = "unrestricted",
                    AllowsPlayerItemInput = false,
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "kitbag.green.unlocked",
                        Cost = new CostInfo { Amount = 0 },
                    },
                },

                new()
                {
                    Name = "kitbag.cyan.unlocked",
                    SkinId = 2830976173,
                    Capacity = 42,
                    DisplayName = "Kit Bag (Editable)",
                    CategoryName = "kitbag",
                    ContentsRulesetName = "unrestricted",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "kitbag.cyan.locked",
                        Cost = new CostInfo { Amount = 0 },
                    },
                },
                new()
                {
                    Name = "kitbag.cyan.locked",
                    SkinId = 2830965451,
                    Capacity = 42,
                    DisplayName = "Kit Bag",
                    CategoryName = "kitbag",
                    ContentsRulesetName = "unrestricted",
                    AllowsPlayerItemInput = false,
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "kitbag.cyan.unlocked",
                        Cost = new CostInfo { Amount = 0 },
                    },
                },

                new()
                {
                    Name = "kitbag.blue.unlocked",
                    SkinId = 2830976481,
                    Capacity = 42,
                    DisplayName = "Kit Bag (Editable)",
                    CategoryName = "kitbag",
                    ContentsRulesetName = "unrestricted",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "kitbag.blue.locked",
                        Cost = new CostInfo { Amount = 0 },
                    },
                },
                new()
                {
                    Name = "kitbag.blue.locked",
                    SkinId = 2830965789,
                    Capacity = 42,
                    DisplayName = "Kit Bag",
                    CategoryName = "kitbag",
                    ContentsRulesetName = "unrestricted",
                    AllowsPlayerItemInput = false,
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "kitbag.blue.unlocked",
                        Cost = new CostInfo { Amount = 0 },
                    },
                },

                // Generic
                new()
                {
                    Name = "generic.xxsmall",
                    SkinId = 2824110403,
                    Capacity = 6,
                    DisplayName = "XXSmall Bag",
                    CategoryName = "generic",
                    ContentsRulesetName = "generic",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "generic.xsmall",
                        Cost = new CostInfo { Amount = 100 }
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 100 } },
                    },
                },
                new()
                {
                    Name = "generic.xsmall",
                    SkinId = 2824113497,
                    Capacity = 12,
                    DisplayName = "XSmall Bag",
                    CategoryName = "generic",
                    ContentsRulesetName = "generic",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "generic.small",
                        Cost = new CostInfo { Amount = 200 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 200 } },
                    },
                },
                new()
                {
                    Name = "generic.small",
                    SkinId = 2824115881,
                    Capacity = 18,
                    DisplayName = "Small Bag",
                    CategoryName = "generic",
                    ContentsRulesetName = "generic",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "generic.medium",
                        Cost = new CostInfo { Amount = 300 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 300 } },
                    },
                },
                new()
                {
                    Name = "generic.medium",
                    SkinId = 2824117824,
                    Capacity = 24,
                    DisplayName = "Medium Bag",
                    CategoryName = "generic",
                    ContentsRulesetName = "generic",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "generic.large",
                        Cost = new CostInfo { Amount = 400 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 400 } },
                    },
                },
                new()
                {
                    Name = "generic.large",
                    SkinId = 2824119889,
                    Capacity = 30,
                    DisplayName = "Large Bag",
                    CategoryName = "generic",
                    ContentsRulesetName = "generic",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "generic.xlarge",
                        Cost = new CostInfo { Amount = 500 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 500 } },
                    },
                },
                new()
                {
                    Name = "generic.xlarge",
                    SkinId = 2824121905,
                    Capacity = 36,
                    DisplayName = "XLarge Bag",
                    CategoryName = "generic",
                    ContentsRulesetName = "generic",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "generic.xxlarge",
                        Cost = new CostInfo { Amount = 600 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 600 } },
                    },
                },
                new()
                {
                    Name = "generic.xxlarge",
                    SkinId = 2824123811,
                    Capacity = 42,
                    DisplayName = "XXLarge Bag",
                    CategoryName = "generic",
                    ContentsRulesetName = "generic",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "bagofholding",
                        Cost = new CostInfo { Amount = 3000 }
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 750 } },
                    },
                },

                // Armor & Clothing - Light Blue
                new()
                {
                    Name = "armor_clothing.xxsmall",
                    SkinId = 2824111863,
                    Capacity = 6,
                    DisplayName = "XXSmall Armor & Clothing Bag",
                    CategoryName = "armor_clothing",
                    ContentsRulesetName = "armor_clothing",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "armor_clothing.xsmall",
                        Cost = new CostInfo { Amount = 100 }
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 100 } },
                    },
                },
                new()
                {
                    Name = "armor_clothing.xsmall",
                    SkinId = 2824114542,
                    Capacity = 12,
                    DisplayName = "XSmall Armor & Clothing Bag",
                    CategoryName = "armor_clothing",
                    ContentsRulesetName = "armor_clothing",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "armor_clothing.small",
                        Cost = new CostInfo { Amount = 200 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 200 } },
                    },
                },
                new()
                {
                    Name = "armor_clothing.small",
                    SkinId = 2824116781,
                    Capacity = 18,
                    DisplayName = "Small Armor & Clothing Bag",
                    CategoryName = "armor_clothing",
                    ContentsRulesetName = "armor_clothing",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "armor_clothing.medium",
                        Cost = new CostInfo { Amount = 300 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 300 } },
                    },
                },
                new()
                {
                    Name = "armor_clothing.medium",
                    SkinId = 2824118678,
                    Capacity = 24,
                    DisplayName = "Medium Armor & Clothing Bag",
                    CategoryName = "armor_clothing",
                    ContentsRulesetName = "armor_clothing",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "armor_clothing.large",
                        Cost = new CostInfo { Amount = 400 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 400 } },
                    },
                },
                new()
                {
                    Name = "armor_clothing.large",
                    SkinId = 2824120619,
                    Capacity = 30,
                    DisplayName = "Large Armor & Clothing Bag",
                    CategoryName = "armor_clothing",
                    ContentsRulesetName = "armor_clothing",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "armor_clothing.xlarge",
                        Cost = new CostInfo { Amount = 500 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 500 } },
                    },
                },
                new()
                {
                    Name = "armor_clothing.xlarge",
                    SkinId = 2824122747,
                    Capacity = 36,
                    DisplayName = "XLarge Armor & Clothing Bag",
                    CategoryName = "armor_clothing",
                    ContentsRulesetName = "armor_clothing",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "armor_clothing.xxlarge",
                        Cost = new CostInfo { Amount = 600 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 600 } },
                    },
                },
                new()
                {
                    Name = "armor_clothing.xxlarge",
                    SkinId = 2824124690,
                    Capacity = 42,
                    DisplayName = "XXLarge Armor & Clothing Bag",
                    CategoryName = "armor_clothing",
                    ContentsRulesetName = "armor_clothing",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "generic.xxlarge",
                        Cost = new CostInfo { Amount = 900 }
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 900 } },
                    },
                },

                // Food & Medical - Green
                new()
                {
                    Name = "food_medical.xxsmall",
                    SkinId = 2824111462,
                    Capacity = 6,
                    DisplayName = "XXSmall Food & Medical Bag",
                    CategoryName = "food_medical",
                    ContentsRulesetName = "food_medical",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "food_medical.xsmall",
                        Cost = new CostInfo { Amount = 100 }
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 100 } },
                    },
                },
                new()
                {
                    Name = "food_medical.xsmall",
                    SkinId = 2824114220,
                    Capacity = 12,
                    DisplayName = "XSmall Food & Medical Bag",
                    CategoryName = "food_medical",
                    ContentsRulesetName = "food_medical",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "food_medical.small",
                        Cost = new CostInfo { Amount = 200 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 200 } },
                    },
                },
                new()
                {
                    Name = "food_medical.small",
                    SkinId = 2824116431,
                    Capacity = 18,
                    DisplayName = "Small Food & Medical Bag",
                    CategoryName = "food_medical",
                    ContentsRulesetName = "food_medical",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "food_medical.medium",
                        Cost = new CostInfo { Amount = 300 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 300 } },
                    },
                },
                new()
                {
                    Name = "food_medical.medium",
                    SkinId = 2824118433,
                    Capacity = 24,
                    DisplayName = "Medium Food & Medical Bag",
                    CategoryName = "food_medical",
                    ContentsRulesetName = "food_medical",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "food_medical.large",
                        Cost = new CostInfo { Amount = 400 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 400 } },
                    },
                },
                new()
                {
                    Name = "food_medical.large",
                    SkinId = 2824120423,
                    Capacity = 30,
                    DisplayName = "Large Food & Medical Bag",
                    CategoryName = "food_medical",
                    ContentsRulesetName = "food_medical",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "food_medical.xlarge",
                        Cost = new CostInfo { Amount = 500 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 500 } },
                    },
                },
                new()
                {
                    Name = "food_medical.xlarge",
                    SkinId = 2824122439,
                    Capacity = 36,
                    DisplayName = "XLarge Food & Medical Bag",
                    CategoryName = "food_medical",
                    ContentsRulesetName = "food_medical",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "food_medical.xxlarge",
                        Cost = new CostInfo { Amount = 600 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 600 } },
                    },
                },
                new()
                {
                    Name = "food_medical.xxlarge",
                    SkinId = 2824124459,
                    Capacity = 42,
                    DisplayName = "XXLarge Food & Medical Bag",
                    CategoryName = "food_medical",
                    ContentsRulesetName = "food_medical",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "generic.xxlarge",
                        Cost = new CostInfo { Amount = 900 }
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 900 } },
                    },
                },

                // Items & Construction - Yellow
                new()
                {
                    Name = "items_construction.xxsmall",
                    SkinId = 2824113019,
                    Capacity = 6,
                    DisplayName = "XXSmall Items & Construction Bag",
                    CategoryName = "items_construction",
                    ContentsRulesetName = "items_construction",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "items_construction.xsmall",
                        Cost = new CostInfo { Amount = 100 }
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 100 } },
                    },
                },
                new()
                {
                    Name = "items_construction.xsmall",
                    SkinId = 2824115483,
                    Capacity = 12,
                    DisplayName = "XSmall Items & Construction Bag",
                    CategoryName = "items_construction",
                    ContentsRulesetName = "items_construction",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "items_construction.small",
                        Cost = new CostInfo { Amount = 200 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 200 } },
                    },
                },
                new()
                {
                    Name = "items_construction.small",
                    SkinId = 2824117546,
                    Capacity = 18,
                    DisplayName = "Small Items & Construction Bag",
                    CategoryName = "items_construction",
                    ContentsRulesetName = "items_construction",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "items_construction.medium",
                        Cost = new CostInfo { Amount = 300 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 300 } },
                    },
                },
                new()
                {
                    Name = "items_construction.medium",
                    SkinId = 2824119338,
                    Capacity = 24,
                    DisplayName = "Medium Items & Construction Bag",
                    CategoryName = "items_construction",
                    ContentsRulesetName = "items_construction",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "items_construction.large",
                        Cost = new CostInfo { Amount = 400 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 400 } },
                    },
                },
                new()
                {
                    Name = "items_construction.large",
                    SkinId = 2824121607,
                    Capacity = 30,
                    DisplayName = "Large Items & Construction Bag",
                    CategoryName = "items_construction",
                    ContentsRulesetName = "items_construction",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "items_construction.xlarge",
                        Cost = new CostInfo { Amount = 500 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 500 } },
                    },
                },
                new()
                {
                    Name = "items_construction.xlarge",
                    SkinId = 2824123520,
                    Capacity = 36,
                    DisplayName = "XLarge Items & Construction Bag",
                    CategoryName = "items_construction",
                    ContentsRulesetName = "items_construction",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "items_construction.xxlarge",
                        Cost = new CostInfo { Amount = 600 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 600 } },
                    },
                },
                new()
                {
                    Name = "items_construction.xxlarge",
                    SkinId = 2824125425,
                    Capacity = 42,
                    DisplayName = "XXLarge Items & Construction Bag",
                    CategoryName = "items_construction",
                    ContentsRulesetName = "items_construction",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "generic.xxlarge",
                        Cost = new CostInfo { Amount = 900 }
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 900 } },
                    },
                },

                // Resources & Components - Orange
                new()
                {
                    Name = "resources_components.xxsmall",
                    SkinId = 2824112139,
                    Capacity = 6,
                    DisplayName = "XXSmall Resources & Components Bag",
                    CategoryName = "resources_components",
                    ContentsRulesetName = "resources_components",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "resources_components.xsmall",
                        Cost = new CostInfo { Amount = 100 }
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 100 } },
                    },
                },
                new()
                {
                    Name = "resources_components.xsmall",
                    SkinId = 2824114807,
                    Capacity = 12,
                    DisplayName = "XSmall Resources & Components Bag",
                    CategoryName = "resources_components",
                    ContentsRulesetName = "resources_components",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "resources_components.small",
                        Cost = new CostInfo { Amount = 200 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 200 } },
                    },
                },
                new()
                {
                    Name = "resources_components.small",
                    SkinId = 2824117061,
                    Capacity = 18,
                    DisplayName = "Small Resources & Components Bag",
                    CategoryName = "resources_components",
                    ContentsRulesetName = "resources_components",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "resources_components.medium",
                        Cost = new CostInfo { Amount = 300 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 300 } },
                    },
                },
                new()
                {
                    Name = "resources_components.medium",
                    SkinId = 2824118878,
                    Capacity = 24,
                    DisplayName = "Medium Resources & Components Bag",
                    CategoryName = "resources_components",
                    ContentsRulesetName = "resources_components",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "resources_components.large",
                        Cost = new CostInfo { Amount = 400 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 400 } },
                    },
                },
                new()
                {
                    Name = "resources_components.large",
                    SkinId = 2824121055,
                    Capacity = 30,
                    DisplayName = "Large Resources & Components Bag",
                    CategoryName = "resources_components",
                    ContentsRulesetName = "resources_components",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "resources_components.xlarge",
                        Cost = new CostInfo { Amount = 500 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 500 } },
                    },
                },
                new()
                {
                    Name = "resources_components.xlarge",
                    SkinId = 2824123001,
                    Capacity = 36,
                    DisplayName = "XLarge Resources & Components Bag",
                    CategoryName = "resources_components",
                    ContentsRulesetName = "resources_components",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "resources_components.xxlarge",
                        Cost = new CostInfo { Amount = 600 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 600 } },
                    },
                },
                new()
                {
                    Name = "resources_components.xxlarge",
                    SkinId = 2824124960,
                    Capacity = 42,
                    DisplayName = "XXLarge Resources & Components Bag",
                    CategoryName = "resources_components",
                    ContentsRulesetName = "resources_components",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "generic.xxlarge",
                        Cost = new CostInfo { Amount = 900 }
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 900 } },
                    },
                },

                // Weapons & Tools - Dark Blue
                new()
                {
                    Name = "weapons_tools.xxsmall",
                    SkinId = 2824110902,
                    Capacity = 6,
                    DisplayName = "XXSmall Weapons & Tools Bag",
                    CategoryName = "weapons_tools",
                    ContentsRulesetName = "weapons_tools",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "weapons_tools.xsmall",
                        Cost = new CostInfo { Amount = 100 }
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 100 } },
                    },
                },
                new()
                {
                    Name = "weapons_tools.xsmall",
                    SkinId = 2824113791,
                    Capacity = 12,
                    DisplayName = "XSmall Weapons & Tools Bag",
                    CategoryName = "weapons_tools",
                    ContentsRulesetName = "weapons_tools",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "weapons_tools.small",
                        Cost = new CostInfo { Amount = 200 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 200 } },
                    },
                },
                new()
                {
                    Name = "weapons_tools.small",
                    SkinId = 2824116147,
                    Capacity = 18,
                    DisplayName = "Small Weapons & Tools Bag",
                    CategoryName = "weapons_tools",
                    ContentsRulesetName = "weapons_tools",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "weapons_tools.medium",
                        Cost = new CostInfo { Amount = 300 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 300 } },
                    },
                },
                new()
                {
                    Name = "weapons_tools.medium",
                    SkinId = 2824118115,
                    Capacity = 24,
                    DisplayName = "Medium Weapons & Tools Bag",
                    CategoryName = "weapons_tools",
                    ContentsRulesetName = "weapons_tools",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "weapons_tools.large",
                        Cost = new CostInfo { Amount = 400 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 400 } },
                    },
                },
                new()
                {
                    Name = "weapons_tools.large",
                    SkinId = 2824120210,
                    Capacity = 30,
                    DisplayName = "Large Weapons & Tools Bag",
                    CategoryName = "weapons_tools",
                    ContentsRulesetName = "weapons_tools",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "weapons_tools.xlarge",
                        Cost = new CostInfo { Amount = 500 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 500 } },
                    },
                },
                new()
                {
                    Name = "weapons_tools.xlarge",
                    SkinId = 2824122197,
                    Capacity = 36,
                    DisplayName = "XLarge Weapons & Tools Bag",
                    CategoryName = "weapons_tools",
                    ContentsRulesetName = "weapons_tools",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "weapons_tools.xxlarge",
                        Cost = new CostInfo { Amount = 600 },
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 600 } },
                    },
                },
                new()
                {
                    Name = "weapons_tools.xxlarge",
                    SkinId = 2824124162,
                    Capacity = 42,
                    DisplayName = "XXLarge Weapons & Tools Bag",
                    CategoryName = "weapons_tools",
                    ContentsRulesetName = "weapons_tools",
                    UpgradeTarget = new BagUpgradeTarget
                    {
                        TargetName = "generic.xxlarge",
                        Cost = new CostInfo { Amount = 900 }
                    },
                    RecycleInfo = new RecycleInfo
                    {
                        Enabled = false,
                        Ingredients = new[] { new RecycleIngredient { Amount = 900 } },
                    },
                },
            };

            [JsonIgnore]
            private Permission _permission;

            [JsonIgnore]
            public Dictionary<uint, ContainerPrefabBagLimits> ContainerBagLimitsByPrefabId = new();

            [JsonIgnore]
            private Dictionary<ulong, List<BagProfile>> _bagProfileListsBySkinId = new();

            [JsonIgnore]
            private Dictionary<string, BagProfile> _bagProfilesByName = new(StringComparer.InvariantCultureIgnoreCase);

            [JsonIgnore]
            private Dictionary<string, BagProfile> _bagProfilesByDisplayName = new(StringComparer.OrdinalIgnoreCase);

            [JsonIgnore]
            public List<string> UniqueBagCategories = new();

            [JsonIgnore]
            public bool ContainsDuplicateSkins;

            [JsonIgnore]
            public bool ContainsRecyclableBags;

            [JsonIgnore]
            public bool ContainsStackProfileItemOverrides;

            public void Init(BagOfHolding plugin)
            {
                _permission = plugin.permission;

                if (VisualBackpack.DisplayWhileInBackpackSlot && !Wearable.AllowInBackpackSlot)
                {
                    LogWarning($"Potential misconfiguration detected: \"{VisualBackpackSettings.DisplayWhileInBackpackSlotField}\" is true, and \"{WearableSettings.AllowInBackpackSlotField}\" is false. Enabling the visual backpack while a bag is worn in the backpack slot has no effect if you do not allow bags to be worn in the backpack slot. Both of these options should be set to the same value.");
                }

                if (VisualBackpack.DisplayWhileInNonBackpackSlot && !Wearable.AllowInNonBackpackSlot)
                {
                    LogWarning($"Potential misconfiguration detected: \"{VisualBackpackSettings.DisplayWhileInNonBackpackSlotField}\" is true, and \"{WearableSettings.AllowInNonBackpackSlotField}\" is false. Enabling the visual backpack while a bag is worn in a non-backpack slot has no effect if you do not allow bags to be worn in non-backpack slots. Both of these options should be set to the same value.");
                }

                foreach (var bagProfile in BagProfiles)
                {
                    if (string.IsNullOrWhiteSpace(bagProfile.Name))
                    {
                        LogError("Bag profile found with an empty name. Please ensure all bag profiles have a non-empty name.");
                    }
                    else if (!_bagProfilesByName.TryAdd(bagProfile.Name, bagProfile))
                    {
                        LogError($"Multiple bag profiles found with \"{BagProfile.NameField}\": \"{bagProfile.Name}\". Bag profile \"{BagProfile.NameField}\" must be unique. Please rename all duplicates.");
                    }

                    if (string.IsNullOrWhiteSpace(bagProfile.DisplayName))
                    {
                        LogError($"Bag profile \"{bagProfile.Name}\" has an empty \"Display name\". Please configure \"Display name\".");
                    }
                    else
                    {
                        // No error handling on this case, since duplicate display names are tolerated.
                        _bagProfilesByDisplayName.TryAdd(bagProfile.DisplayName, bagProfile);
                    }

                    IndexBagProfile(bagProfile);
                }

                // Cache and sort bag profiles by skin ID.
                foreach (var entry in _bagProfileListsBySkinId.ToArray())
                {
                    _bagProfileListsBySkinId[entry.Key] = entry.Value.OrderBy(profile => profile.Capacity).ToList();
                }

                // Initialize cost overrides.
                foreach (var costOverride in UpgradeCostOverrides)
                {
                    costOverride.Init(plugin, this);
                }

                // Initialize stack profiles.
                foreach (var stackProfile in StackProfiles)
                {
                    stackProfile.Init();
                }

                // Initialize bag profiles (upgrade targets, contents rulesets, categories).
                foreach (var bagProfile in BagProfiles)
                {
                    bagProfile.Init(plugin, this);

                    if (bagProfile.CanBeRecycled)
                    {
                        ContainsRecyclableBags = true;
                    }

                    if (bagProfile.StackProfile?.ContainsItemOverrides ?? false)
                    {
                        ContainsStackProfileItemOverrides = true;
                    }
                }

                // Initialize content rulesets (limit profiles, validate item categories/names).
                // Must be initialized after bag profiles since they will refer to bag profiles.
                foreach (var contentRuleset in BagContentRulesets)
                {
                    contentRuleset.Init(plugin, this);
                }

                // Initialize limit profiles (prefab limits will be fully initialized in OnServedInitialized).
                // Must be initialized after bag profiles since they will refer to bag profiles.
                PlayerBagLimits.Init(plugin, this);
                if (WearableBagLimits.Enabled)
                {
                    WearableBagLimits.Init(plugin, this);
                }

                BackpackBagLimits.Init(plugin, this);
                #if FEATURE_EXTRA_POCKETS_CONFIG
                ExtraPocketsBagLimits.Init(plugin, this);
                #endif
                ContainerBagLimits.Init(plugin, this);
            }

            public void OnServerInitialize()
            {
                LootConfig.OnServerInitialize(this);
            }

            public void OnServerInitialized(BagOfHolding plugin)
            {
                foreach (var entry in ContainerBagLimitsByPrefabName)
                {
                    var prefabPath = entry.Key;
                    var entityTemplate = GameManager.server.FindPrefab(prefabPath)?.GetComponent<BaseEntity>();
                    if (entityTemplate == null)
                    {
                        LogError($"Invalid container prefab in configuration: {prefabPath}");
                        continue;
                    }

                    var containerPrefabLimits = entry.Value;
                    ContainerBagLimitsByPrefabId[entityTemplate.prefabID] = containerPrefabLimits;
                    containerPrefabLimits.Init(plugin, this, entityTemplate.ShortPrefabName);
                }
            }

            public void IndexBagBySkins()
            {
                ContainsDuplicateSkins = false;
                _bagProfileListsBySkinId.Clear();

                foreach (var bagProfile in BagProfiles)
                {
                    IndexBagProfile(bagProfile);
                }
            }

            public BagProfile DetermineBagProfile(Item item)
            {
                if (!IsSkinnedBag(item))
                    return null;

                if (!_bagProfileListsBySkinId.TryGetValue(item.skin, out var bagProfileList))
                    return null;

                if (bagProfileList.Count == 1)
                    return bagProfileList[0];

                var bagProfile = EstimateBagProfileByDisplayName(bagProfileList, item, out var matchCount);

                // Since duplicate display names are tolerated, only consider a bag profile a match if it's unique.
                if (bagProfile != null && matchCount == 1)
                    return bagProfile;

                if (item.contents == null)
                    return bagProfileList[0];

                return EstimateBagProfileBySkinAndCapacity(bagProfileList, item);
            }

            public BagProfile GetBagProfileByName(string name)
            {
                return _bagProfilesByName.TryGetValue(name, out var bagProfile)
                    ? bagProfile
                    : null;
            }

            public BagProfile GetBagProfileByDisplayName(string displayName)
            {
                return _bagProfilesByDisplayName.TryGetValue(displayName, out var bagProfile)
                    ? bagProfile
                    : null;
            }

            public BagContentsRuleset FindRuleset(string name)
            {
                foreach (var ruleset in BagContentRulesets)
                {
                    if (StringUtils.Equals(ruleset.Name, name))
                        return ruleset;
                }

                return null;
            }

            public StackProfile FindStackProfile(string name)
            {
                foreach (var stackProfile in StackProfiles)
                {
                    if (StringUtils.Equals(stackProfile.Name, name))
                        return stackProfile;
                }

                return null;
            }

            public int GetUpgradeCostOverride(string playerId, BagProfile bagProfile)
            {
                return GetUpgradeCostOverride(playerId)?.GetOverrideForProfile(bagProfile) ?? -1;
            }

            private UpgradeCostOverride GetUpgradeCostOverride(string playerId)
            {
                var userData = _permission.GetUserData(playerId);

                for (var i = UpgradeCostOverrides.Length - 1; i >= 0; i--)
                {
                    var costOverride = UpgradeCostOverrides[i];
                    if (costOverride.Permission != null && UserHasPermission(_permission, userData, costOverride.Permission))
                        return costOverride;
                }

                return null;
            }

            private BagProfile EstimateBagProfileByDisplayName(List<BagProfile> bagProfileList, Item item, out int matchCount)
            {
                matchCount = 0;

                if (string.IsNullOrWhiteSpace(item.name))
                    return null;

                BagProfile matchingBagProfile = null;

                foreach (var bagProfile in bagProfileList)
                {
                    if (string.IsNullOrWhiteSpace(bagProfile.DisplayName))
                        continue;

                    if (!StringUtils.Equals(bagProfile.DisplayName, item.name))
                        continue;

                    // Return the first match, but keep counting for matches.
                    matchingBagProfile ??= bagProfile;

                    matchCount++;
                }

                return matchingBagProfile;
            }

            private BagProfile EstimateBagProfileBySkinAndCapacity(List<BagProfile> bagProfileList, Item item)
            {
                var capacity = item.contents.capacity;
                BagProfile previousBagProfile = null;

                // Assumption: The bag profiles are already sorted ascending by capacity (ensured elsewhere by plugin logic).
                foreach (var bagProfile in bagProfileList)
                {
                    // If the capacity is a perfect match, no need to check other profiles.
                    if (bagProfile.Capacity == capacity)
                        return bagProfile;

                    // If the profile has higher capacity than the container, prefer the lower profile (if there is one).
                    if (bagProfile.Capacity > capacity)
                        return previousBagProfile ?? bagProfile;

                    // Current capacity is larger than the profile, so keep searching for a larger profile.
                    previousBagProfile = bagProfile;
                }

                return previousBagProfile;
            }

            private void IndexBagProfile(BagProfile bagProfile)
            {
                if (!_bagProfileListsBySkinId.TryGetValue(bagProfile.SkinId, out var bagProfileListWithSkinId))
                {
                    bagProfileListWithSkinId = new List<BagProfile>();
                    _bagProfileListsBySkinId[bagProfile.SkinId] = bagProfileListWithSkinId;
                }
                else if (bagProfileListWithSkinId.Any(bp => bp.Capacity == bagProfile.Capacity))
                {
                    LogError($"Multiple bag profiles have skin ID {bagProfile.SkinId} and capacity {bagProfile.Capacity}. The plugin won't be able to tell which profile a bag like this belongs to.");
                }

                bagProfileListWithSkinId.Add(bagProfile);

                if (bagProfileListWithSkinId.Count > 1)
                {
                    ContainsDuplicateSkins = true;
                }
            }
        }

        private Configuration GetDefaultConfig() => new();

        #region Configuration Helpers

        [JsonObject(MemberSerialization.OptIn)]
        private class BaseConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>()
                                    .ToDictionary(prop => prop.Name,
                                                  prop => ToObject(prop.Value));

                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue)token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(BaseConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigSection(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigSection(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            var changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                if (currentRaw.TryGetValue(key, out var currentRawValue))
                {
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (currentWithDefaults[key] is Dictionary<string, object> defaultDictValue)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = defaultDictValue;
                            changed = true;
                        }
                        else if (MaybeUpdateConfigSection(defaultDictValue, currentDictValue))
                        {
                            changed = true;
                        }
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }

        protected override void LoadDefaultConfig() => _config = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_config))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch (Exception e)
            {
                LogError(e.Message);
                LoadDefaultConfig();
                throw new InvalidOperationException("Configuration file is invalid JSON.");
            }
        }

        protected override void SaveConfig()
        {
            Log($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_config, true);
        }

        #endregion

        #endregion

        #region Localization

        private class LangEntry
        {
            public static readonly List<LangEntry> AllLangEntries = new();

            public static readonly LangEntry CurrencyNameEconomics = new("CurrencyName.Economics", "Coins");
            public static readonly LangEntry CurrencyNameServerRewards = new("CurrencyName.ServerRewards", "RP");

            public static readonly LangEntry ErrorNoPermission = new("Error.NoPermission", "You don't have permission to do that.");
            public static readonly LangEntry ErrorBagProfileNotFound = new("Error.BagProfileNotFound", "Error: Bag profile <color=#fe0>{0}</color> not found. Use <color=#fe0>boh.listbags <filter></color> to search available bag names.");
            public static readonly LangEntry ErrorBagCategoryNotFound = new("Error.BagCategoryNotFound", "Error: Bag category <color=#fe0>{0}</color> not found.");
            public static readonly LangEntry ErrorPlayerNotFound = new("Error.PlayerNotFound", "Error: Player <color=#fe0>{0}</color> not found.");
            public static readonly LangEntry ErrorNoBags = new("Error.NoBags", "Error: You don't have any bags.");
            public static readonly LangEntry ErrorListBagsNotFound = new("Error.ListBagsNotFound", "Error: No bag profiles found matching query <color=#fe0>{0}</color>.");
            public static readonly LangEntry ErrorInvalidSkinId = new("Error.InvalidSkinId", "Error: <color=#fe0>{0}</color> is not a valid skin ID.");

            public static readonly LangEntry GiveBagSyntax = new("GiveBag.Syntax", "Syntax: <color=#fe0>{0} <player> <profile> <amount></color>");
            public static readonly LangEntry GiveBagSuccess = new("GiveBag.Success", "Gave <color=#fe0>{0} {1}</color> to <color=#fe0>{2}</color>.");

            public static readonly LangEntry ErrorKitNotFound = new("Error.KitNotFound", "Error: Kit <color=#fe0>{0}</color> not found.");
            public static readonly LangEntry GiveKitBagSyntax = new("GiveKitBag.Syntax", "Syntax: <color=#fe0>{0} <player> <profile> <kit></color>");
            public static readonly LangEntry GiveKitBagSuccess = new("GiveKitBag.Success", "Gave <color=#fe0>{0}</color> to <color=#fe0>{1}</color> with kit <color=#fe0>{2}</color>.");

            public static readonly LangEntry SetSkinsSyntax = new("SetSkins.Syntax", "Syntax: <color=#fe0>{0} <category> <skin1> <skin2> ...</color>");
            public static readonly LangEntry SetSkinsCountMismatch = new("SetSkins.CountMismatch2", "Error: There are <color=#fe0>{0}</color> bag profiles with category <color=#fe0>{1}</color>, but you have only provided <color=#fe0>{2}</color> skins.");
            public static readonly LangEntry SetSkinsSuccess = new("SetSkins.Success", "Successfully updated skins of category <color=#fe0>{0}</color>.");

            public static readonly LangEntry UIBack = new("UI.Back", "↑ Back");
            public static readonly LangEntry UIUpgrade = new("UI.Upgrade", "Upgrade");
            public static readonly LangEntry UIGatherAll = new("UI.Gather.All", "Gather: All");
            public static readonly LangEntry UIGatherExisting = new("UI.Gather.Existing", "Gather: Existing");
            public static readonly LangEntry UIGatherOff = new("UI.Gather.Off", "Gather: Off");
            public static readonly LangEntry UISlotAmount = new("UI.SlotAmount", "{0} slots");
            public static readonly LangEntry UIUpgradeErrorLimit = new("UI.Upgrade.Error.Limit", "Can't upgrade: Reached bag limit");
            public static readonly LangEntry UIUpgradeErrorStacked = new("UI.Upgrade.Error.Stacked", "Can't upgrade: Bag is stacked");
            public static readonly LangEntry UIUpgradeErrorCantAfford = new("UI.Upgrade.Error.CantAfford", "Can't afford upgrade");
            public static readonly LangEntry UIUpgradePurchase = new("UI.Upgrade.Purchase", "Purchase upgrade");
            public static readonly LangEntry UIUpgradeFree = new("UI.Upgrade.Free", "Confirm upgrade");

            public static readonly LangEntry LootSpawnSuccess = new("SpawnLoot.Success", "{0} bags have been spawned in loot containers.");
            public static readonly LangEntry LootClearSuccess = new("ClearLoot.Success", "All {0} bags have been removed from loot containers.");

            public static readonly LangEntry StatsTotalBags = new("Stats.TotalBags", "Total bags: {0} ({1} used, {2} empty)");
            public static readonly LangEntry StatsTotalCapacity = new("Stats.TotalCapacity", "Total capacity: {0} ({1} used, {2} available)");
            public static readonly LangEntry StatsTotalBagsByProfile = new("Stats.TotalBagsByProfile", "Total bags by profile:");
            public static readonly LangEntry StatsTotalBagsByCategory = new("Stats.TotalBagsByCategory", "Total bags by category:");
            public static readonly LangEntry StatsTotalBagsByLocationType = new("Stats.TotalBagsByLocationType", "Total bags by location type:");

            public string Name;
            public string English;

            public LangEntry(string name, string english)
            {
                Name = name;
                English = english;

                AllLangEntries.Add(this);
            }
        }

        private string GetCurrencyName(string playerId, IPaymentProvider paymentProvider)
        {
            return paymentProvider switch
            {
                ItemsPaymentProvider itemsPaymentProvider => itemsPaymentProvider.ItemDefinition.displayName.english,
                EconomicsPaymentProvider => GetMessage(playerId, LangEntry.CurrencyNameEconomics),
                ServerRewardsPaymentProvider => GetMessage(playerId, LangEntry.CurrencyNameServerRewards),
                _ => "?",
            };
        }

        private string GetMessage(string playerId, LangEntry langEntry) =>
            lang.GetMessage(langEntry.Name, this, playerId);

        private string GetMessage(string playerId, LangEntry langEntry, object arg1) =>
            string.Format(GetMessage(playerId, langEntry), arg1);

        private string GetMessage(string playerId, LangEntry langEntry, object arg1, object arg2) =>
            string.Format(GetMessage(playerId, langEntry), arg1, arg2);

        private string GetMessage(string playerId, LangEntry langEntry, object arg1, object arg2, string arg3) =>
            string.Format(GetMessage(playerId, langEntry), arg1, arg2, arg3);

        private string GetMessage(string playerId, LangEntry langEntry, params object[] args) =>
            string.Format(GetMessage(playerId, langEntry), args);


        private void ReplyToPlayer(IPlayer player, LangEntry langEntry) =>
            player.Reply(GetMessage(player.Id, langEntry));

        private void ReplyToPlayer(IPlayer player, LangEntry langEntry, object arg1) =>
            player.Reply(GetMessage(player.Id, langEntry, arg1));

        private void ReplyToPlayer(IPlayer player, LangEntry langEntry, object arg1, object arg2) =>
            player.Reply(GetMessage(player.Id, langEntry, arg1, arg2));

        private void ReplyToPlayer(IPlayer player, LangEntry langEntry, object arg1, object arg2, object arg3) =>
            player.Reply(GetMessage(player.Id, langEntry, arg1, arg2, arg3));

        private void ReplyToPlayer(IPlayer player, LangEntry langEntry, params object[] args) =>
            player.Reply(GetMessage(player.Id, langEntry, args));


        private void ChatMessage(BasePlayer player, LangEntry langEntry) =>
            player.ChatMessage(GetMessage(player.UserIDString, langEntry));

        private void ChatMessage(BasePlayer player, LangEntry langEntry, object arg1) =>
            player.ChatMessage(GetMessage(player.UserIDString, langEntry, arg1));

        private void ChatMessage(BasePlayer player, LangEntry langEntry, object arg1, object arg2) =>
            player.ChatMessage(GetMessage(player.UserIDString, langEntry, arg1, arg2));

        private void ChatMessage(BasePlayer player, LangEntry langEntry, object arg1, object arg2, object arg3) =>
            player.ChatMessage(GetMessage(player.UserIDString, langEntry, arg1, arg2, arg3));

        private void ChatMessage(BasePlayer player, LangEntry langEntry, params object[] args) =>
            player.ChatMessage(GetMessage(player.UserIDString, langEntry, args));


        protected override void LoadDefaultMessages()
        {
            var englishLangKeys = new Dictionary<string, string>();

            foreach (var langEntry in LangEntry.AllLangEntries)
            {
                englishLangKeys[langEntry.Name] = langEntry.English;
            }

            lang.RegisterMessages(englishLangKeys, this, "en");
        }

        #endregion
    }
}
