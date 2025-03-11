using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;
using System.Globalization;
using Oxide.Core.Configuration;
using Oxide.Ext.Chaos;
using Oxide.Ext.Chaos.Collections;
using Oxide.Ext.Chaos.Data;
using Oxide.Ext.Chaos.UIFramework;
using UnityEngine.UI;

using Chaos = Oxide.Ext.Chaos;
using Color = Oxide.Ext.Chaos.UIFramework.Color;
using Axis = Oxide.Ext.Chaos.UIFramework.Axis;
using Debug = UnityEngine.Debug;
using GridLayoutGroup = Oxide.Ext.Chaos.UIFramework.GridLayoutGroup;
using HorizontalLayoutGroup = Oxide.Ext.Chaos.UIFramework.HorizontalLayoutGroup;
using VerticalLayoutGroup = Oxide.Ext.Chaos.UIFramework.VerticalLayoutGroup;

namespace Oxide.Plugins
{
    [Info("StacksExtended", "k1lly0u", "2.0.14")]
    [Description("An advanced stack size controller")]
    class StacksExtended : ChaosPlugin
    {
        #region Fields
        [PluginReference] private Plugin FurnaceSplitter;

        private Datafile<OrderedHash<string, StackLimit>> m_StackLimits;
        private Datafile<OrderedHash<string, StackLimit>> m_PlayerLimits;
        private Datafile<OrderedHash<string, StorageLimit>> m_StorageLimits;
        private VipLimitsDataFile m_VIPLimits;

        private readonly Hash<string, int> m_DefaultItemStackSizes = new Hash<string, int>();
        private readonly Hash<string, int> m_DefaultStorageStackSizes = new Hash<string, int>();
        private readonly Hash<string, int> m_PrefabNameToItemID = new Hash<string, int>();
        private readonly Hash<string, string> m_ShortPrefabNameToPrefabName = new Hash<string, string>();

        [Chaos.Permission]
        private const string ADMIN_PERMISSION = "stacksextended.admin";
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            m_StackLimits = new Datafile<OrderedHash<string, StackLimit>>("StacksExtended/stack_limits");
            m_PlayerLimits = new Datafile<OrderedHash<string, StackLimit>>("StacksExtended/player_overrides");
            m_StorageLimits = new Datafile<OrderedHash<string, StorageLimit>>("StacksExtended/storage_limits");
            m_VIPLimits = new VipLimitsDataFile("StacksExtended/vip_limits");
            
            m_CallbackHandler = new CommandCallbackHandler(this);
            
            foreach (string perm in m_VIPLimits.Data.Keys)
            {
                if (!permission.PermissionExists(perm, this))
                    permission.RegisterPermission(perm, this);
            }
        }

        private void OnServerInitialized()
        {
            InitializeUI();
            
            CheckUpdateConfiguration();

            if (Configuration.Player.InventoryStackLimit > 0)
                Unsubscribe(nameof(OnGiveSoldItem));
            
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
            
            foreach (BasePlayer player in BasePlayer.sleepingPlayerList)
                OnPlayerConnected(player);
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (m_UIUsers.ContainsKey(player.userID))
            {
                m_UIUsers.Remove(player.userID);
                CuiHelper.DestroyUi(player, STACKS_UI);
                CuiHelper.DestroyUi(player, POPUP_UI);
            }
        }
        
        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                SetPlayerStackSize(player, true);
                OnPlayerDisconnected(player);
            }

            foreach (BasePlayer player in BasePlayer.sleepingPlayerList)
                SetPlayerStackSize(player, true);

            ResetContainerStackSizes();
            ResetItemStackSizes();
        }
        #endregion
       
        #region Localization
        protected override void PopulatePhrases()
        {
            m_Messages = new Dictionary<string, string>()
            {
                ["Button.Create"] = "Create",
                ["Button.Cancel"] = "Cancel",
                ["Button.Exit"] = "Exit",
                ["Button.Item"] = "Items",
                ["Button.Storage"] = "Containers",
                ["Button.PlayerOverrides"] = "Player Overrides",
                ["Button.VIPItems"] = "VIP Items",
                ["Button.VIPStorage"] = "VIP Containers",
                ["Button.ItemOverrides"] = "Item Overrides ({0} active)",
                ["Button.AddItemOverride"] = "Add Item Override",
                ["Button.AddStorageOverride"] = "Add Container Override",
                ["Button.AddCustomPermission"] = "Add VIP Permission",

                ["Label.StackSize"] = "Stack Size",
                ["Label.StackMultiplier"] = "Stack Multiplier",
                ["Label.DefaultStackSize"] = "Default stack size : {0}",
                ["Label.AddItemOverride"] = "Add item override",
                ["Label.AddStorageOverride"] = "Add container override",
                ["Label.CreateVIPPermission"] = "Create VIP Permission",
                ["Label.SelectPermission"] = "Select a permission to continue",
                ["Label.VIPPriority"] = "Priority",
                ["Label.VIPItems"] = "Editing item overrides for permission {0}",
                ["Label.VIPStorage"] = "Editing container overrides for permission {0}",
                ["Label.PlayerInventoryOverrides"] = "Player inventory stack overrides",
                ["Label.MaxStackSize"] = "Max Stack Size",
                ["Label.StorageHint"] = "Max stack size of 0 means no container based limit",
                ["Label.ContainerItemOverrides"] = "Item overrides for container {0}",
                ["Label.ContainerItemOverrides.Permission"] = "Item overrides for container {0} with permission {1}",
                
                ["Error.EnterPermission"] = "You must enter a permission in the input field",
                ["Error.Error.PermissionExists"] = "That permission already exists"
            };
        }

        #endregion
        
        #region Item Management
        private object CanStackItem(Item item, Item otherItem)
        {
            if (item.parent != null && item.parent.entityOwner is LootContainer)
                return null;
            
            if (Configuration.Exclude.IsExcluded(item))
                return null;

            bool canStack = otherItem != item && item.info.stackable > 1 && otherItem.info.stackable > 1 && 
                            otherItem.info.itemid == item.info.itemid && (!item.hasCondition || Mathf.Approximately(item.condition, item.maxCondition)) &&
                            (!otherItem.hasCondition ||  Mathf.Approximately(otherItem.condition, otherItem.maxCondition)) && 
                            item.IsValid() && (!item.IsBlueprint() || item.blueprintTarget == otherItem.blueprintTarget);

            if (!canStack)
                return false;
            
            if (item.GetHeldEntity() is BaseLiquidVessel)
            {
                if (!Configuration.Options.EnableLiquidContainerStacks) 
                    return false;
                
                if (item.contents != null && otherItem.contents != null && (item.contents.itemList.Count != otherItem.contents.itemList.Count ||
                     item.contents.itemList.Count > 0 && otherItem.contents.itemList.Count > 0 && item.contents.itemList[0].amount != otherItem.contents.itemList[0].amount)) 
                    return false;
            }

            if (Configuration.Options.BlockModdedWeaponStacks || Configuration.Options.BlockUnequalAmmoWeaponStacks)
            {
                BaseProjectile targetProjectile = item.GetHeldEntity() as BaseProjectile;
                BaseProjectile sourceProjectile = otherItem.GetHeldEntity() as BaseProjectile;

                if (!Configuration.Options.EnableProjectileWeaponStacks && (targetProjectile != null || sourceProjectile != null))
                    return false;

                if (Configuration.Options.BlockModdedWeaponStacks && ((targetProjectile != null && item.contents?.itemList?.Count > 0) || (sourceProjectile != null && otherItem.contents?.itemList?.Count > 0)))
                    return false;

                if (Configuration.Options.BlockUnequalAmmoWeaponStacks && sourceProjectile != null && targetProjectile != null)
                {
                    if (targetProjectile.primaryMagazine.contents != sourceProjectile.primaryMagazine.contents || targetProjectile.primaryMagazine.ammoType != sourceProjectile.primaryMagazine.ammoType)
                        return false;
                }
            }

            if (Configuration.Options.BeltAntiToolWeaponStack && item.parent.HasFlag(ItemContainer.Flag.Belt) && item.info == otherItem.info)
            { 
                BaseEntity itemEntity = item.GetHeldEntity();
                BaseEntity otherEntity = otherItem.GetHeldEntity();

                if (!itemEntity)
                {
                    ItemModEntity itemModEntity = item.info.GetComponent<ItemModEntity>();
                    itemEntity = itemModEntity ? itemModEntity.entityPrefab.GetEntity() : null;
                }

                if (!otherEntity)
                {
                    ItemModEntity otherModEntity = otherItem.info.GetComponent<ItemModEntity>();
                    otherEntity = otherModEntity ? otherModEntity.entityPrefab.GetEntity() : null;
                }

                if (itemEntity && otherEntity)
                {
                    if (itemEntity is AttackEntity && !(itemEntity is ThrownWeapon) && !(otherEntity is MedicalTool))
                        return false;
                }
            }

            if (item.skin != 0UL || otherItem.skin != 0UL)
            {
                if (item.skin != otherItem.skin && Configuration.Options.BlockDifferentSkinStacks) 
                    return false;

                if (item.hasCondition && otherItem.hasCondition)
                {
                    if (!Mathf.Approximately(otherItem.maxCondition, item.maxCondition) || !Mathf.Approximately(otherItem.condition, item.condition))
                        return false;
                }

                return true;
            }
            return null;
        }

        private object CanMoveItem(Item item, PlayerInventory playerInventory, ItemContainerId targetContainerID, int targetSlot, int amount, ItemMoveModifier itemMoveModifier)
        {
            if (item == null || playerInventory == null)
                return null;

            if (item.parent?.entityOwner)
            {
                if (item.parent.entityOwner.GetComponent("Oxide.Plugins.SkinBox/LootHandler") != null)
                    return null;
            }

            if (Configuration.Exclude.IsExcluded(item))
                return null;
            
            if (targetContainerID == default(ItemContainerId))
            {
                BaseEntity entityOwner = item.GetEntityOwner();
                if (playerInventory.loot.containers.Count > 0)
                    entityOwner = entityOwner == playerInventory.baseEntity ? playerInventory.loot.entitySource : playerInventory.baseEntity;

                IIdealSlotEntity idealSlotEntity = entityOwner as IIdealSlotEntity;
                if (idealSlotEntity != null)
                    targetContainerID = idealSlotEntity.GetIdealContainer(playerInventory.baseEntity, item, itemMoveModifier);
                
                if (targetContainerID == default(ItemContainerId) && entityOwner is StorageContainer)
                    targetContainerID = (entityOwner as StorageContainer).inventory.uid;
            }
            
            ItemContainer itemContainer = playerInventory.FindContainer(targetContainerID);
            if (itemContainer == null)
                return null;
            
            if (IsUsingFurnaceSplitter(playerInventory, item))
                return null;

            if (item.parent != null)
            {
                if (item.parent.IsLocked() || itemContainer.IsLocked() || itemContainer.PlayerItemInputBlocked() || !CanMoveItemsFrom(playerInventory, item.parent?.entityOwner, item))
                    return null;
                
                if (itemContainer != item.parent)
                {
                    BaseEntity heldEntity = item.GetHeldEntity();
                    if (itemContainer.HasFlag(ItemContainer.Flag.Belt) && item.amount > 1 && heldEntity is AttackEntity && !(heldEntity is ThrownWeapon) && !(heldEntity is MedicalTool))
                    {
                        if (item.amount > 1 && playerInventory.containerBelt.SlotTaken(item, targetSlot))
                            return false;

                        if (playerInventory.containerBelt.SlotTaken(item, targetSlot) && playerInventory.containerBelt.GetSlot(targetSlot).info == item.info)
                            return null;

                        Item splitItem = item.SplitItem(1);
                        if (splitItem != null && !splitItem.MoveToContainer(playerInventory.containerBelt, targetSlot, false))
                        {
                            if (!splitItem.MoveToContainer(playerInventory.containerBelt, -1, false))
                                playerInventory.GiveItem(splitItem, null);
                        }

                        playerInventory.ServerUpdate(0f);
                        return false;
                    }

                    if (itemContainer.SlotTaken(item, targetSlot))
                    {
                        Item slot = itemContainer.GetSlot(targetSlot);
                        if (slot != null)
                        {
                            if (slot.info == item.info && !slot.CanStack(item))
                                return null;

                            heldEntity = slot.GetHeldEntity();

                            if (slot.amount > 1 && heldEntity is AttackEntity && !(heldEntity is ThrownWeapon) && !(heldEntity is MedicalTool))
                                return false;
                        }
                    }
                }

                if (targetSlot != -1 && itemContainer.entityOwner != item.parent.entityOwner)
                {
                    Item otherItem = itemContainer.GetSlot(targetSlot);
                    if (otherItem != null && otherItem.info.itemid != item.info.itemid)
                    {
                        if (item.parent.CanAcceptItem(otherItem, -1) == ItemContainer.CanAcceptResult.CanAccept)
                        {
                            int storageLimit = GetMaxStackable(otherItem, item.parent);
                            if (storageLimit > 0)
                            {
                                int splitAmount = Mathf.FloorToInt((float) otherItem.amount / (float) storageLimit) - 1;

                                //Debug.Log($"item {item.info.shortname} | other {otherItem.info.shortname} | storagel {storageLimit} | splitam {splitAmount} | canaccept {item.parent.CanAcceptItem(otherItem, -1)}");
                                for (int i = 0; i < splitAmount; i++)
                                {
                                    Item splitItem;
                                    if (item.parent.itemList.Count >= item.parent.capacity)
                                    {
                                        splitItem = otherItem.SplitItem(storageLimit * (splitAmount - i));
                                        splitItem.Drop(itemContainer.dropPosition, itemContainer.dropVelocity);
                                        break;
                                    }

                                    splitItem = otherItem.SplitItem(storageLimit);
                                    if (!splitItem.MoveToContainer(item.parent))
                                        splitItem.Drop(itemContainer.dropPosition, itemContainer.dropVelocity);
                                }
                            }
                        }
                    }
                }
            }

            if (amount <= 0)
                amount = item.amount;
		    
            amount = Mathf.Clamp(amount, 1, GetMaxStackable(item, itemContainer));
            
            if (playerInventory.baseEntity.GetActiveItem() == item)
                playerInventory.baseEntity.UpdateActiveItem(default(ItemId));
            
            if (amount > 0 && item.amount > amount)
			{
				int split_Amount = amount;
				if (itemContainer.maxStackSize > 0)
                    split_Amount = Mathf.Min(amount, itemContainer.maxStackSize);

                if (split_Amount > 0)
                {
                    Item splitItem = item.SplitItem(split_Amount);
                    if (!splitItem.MoveToContainer(itemContainer, targetSlot, true, false, playerInventory.baseEntity, true))
                    {
                        item.amount += splitItem.amount;
                        splitItem.Remove(0f);
                    }
                }

                ItemManager.DoRemoves();
				playerInventory.ServerUpdate(0f);
                return false;
			}

            if (!item.MoveToContainer(itemContainer, targetSlot, true, false, playerInventory.baseEntity, true))
                return null;
            
		    ItemManager.DoRemoves();
            playerInventory.ServerUpdate(0f);
            return false;
        }
        
        private object OnItemAction(Item item, string action)
        {
            if (item == null)
                return null;
            
            if (Configuration.Exclude.IsExcluded(item))
                return null;

            if (item.GetHeldEntity() is BaseProjectile && item.amount > 1 && action == "unload_ammo")
                return false;

            return null;
        }

        private object OnItemSplit(Item item, int splitAmount)
        {
            if (item == null || Configuration.Exclude.IsExcluded(item))
                return null;

            BaseEntity heldEntity = item.GetHeldEntity();
            if (heldEntity)
            {
                if (heldEntity is BaseLiquidVessel)
                {
                    Item splitItem = SplitItem(item, splitAmount);
                    
                    if (item.contents != null && item.contents.itemList.Count > 0 && splitItem.contents != null && splitItem.contents.itemList.Count > 0)
                        splitItem.contents.itemList[0].amount = item.contents.itemList[0].amount;
                    
                    return splitItem;
                }

                if (heldEntity is BaseProjectile)
                {
                    Item splitItem = SplitItem(item, splitAmount);

                    BaseProjectile splitBaseProjectile = splitItem.GetHeldEntity() as BaseProjectile;
                    splitBaseProjectile.primaryMagazine.contents = (heldEntity as BaseProjectile).primaryMagazine.contents;
                    splitBaseProjectile.SendNetworkUpdateImmediate(false);

                    return splitItem;
                }

                if (item.skin != 0UL)
                    return SplitItem(item, splitAmount);
            }
            else if (item.skin != 0UL)
                return SplitItem(item, splitAmount);
                
            return null;
        }

        private object OnMaxStackable(Item item)
        {
            if (Configuration.Exclude.IsExcluded(item))
                return null;
            
            if (item.parent == null || (int) item.parent.flags == 3)
                return null;

            if (item.parent.entityOwner is LootContainer)
                return null;
            
            return GetMaxStackable(item, item.parent);
        }

        private int GetMaxStackable(Item item, ItemContainer container)
        {
            int maxStackable = 0;

            if ((int)container.flags == 1 || (int)container.flags == 5)
            {
                maxStackable = Configuration.Player.InventoryStackLimit;
                
                if (m_PlayerLimits.Data.TryGetValue(item.info.shortname, out StackLimit stackLimit))
                    maxStackable = stackLimit.GetStackSize();
            }
            else 
            {
                if (container.entityOwner)
                {
                    if (container.entityOwner is LootContainer)
                        goto SKIP_BASIC;
                    
                    StorageLimit storageLimit;

                    if (container.entityOwner.OwnerID != 0UL)
                    {
                        foreach (KeyValuePair<string, VIPLimits> kvp in m_VIPLimits.Data)
                        {
                            if (container.entityOwner.OwnerID.HasPermission(kvp.Key) && kvp.Value.StorageOverrides.TryGetValue(container.entityOwner.PrefabName, out storageLimit))
                            {
                                maxStackable = storageLimit.GetMaxStackable(item);
                                goto SKIP_BASIC;
                            }
                        }
                    }

                    if (m_StorageLimits.Data.TryGetValue(container.entityOwner.PrefabName, out storageLimit))
                        maxStackable = storageLimit.GetMaxStackable(item);
                    else maxStackable = container.maxStackSize;
                }
            }
            
            SKIP_BASIC:

            return maxStackable > 0 ? maxStackable : item.info.stackable;
        }
        
        private bool CanMoveItemsFrom(PlayerInventory playerInventory, BaseEntity baseEntity, Item item)
        {
            StorageContainer storageContainer = baseEntity as StorageContainer;
            
            return !storageContainer || storageContainer.CanMoveFrom(playerInventory.baseEntity, item);
        }
        
        private Item SplitItem(Item item, int splitAmount)
        {
            Item splitItem = ItemManager.CreateByItemID(item.info.itemid, 1, item.skin);
            if (splitItem == null) 
                return null;
            
            item.amount -= splitAmount;
            item.MarkDirty();
            splitItem.amount = splitAmount;
                
            splitItem.OnVirginSpawn();
                    
            if (item.IsBlueprint()) 
                splitItem.blueprintTarget = item.blueprintTarget;
                    
            if (item.hasCondition) 
                splitItem.condition = item.condition;
                    
            splitItem.MarkDirty();
            return splitItem;
        }
        #endregion
        
        #region Vending Management
        private object OnGiveSoldItem(VendingMachine vendingMachine, Item item, BasePlayer player)
        {
            if (Configuration.Player.InventoryStackLimit > 0 && item.amount > Configuration.Player.InventoryStackLimit)
            {
                int amountRemaining = item.amount;
                
                while(amountRemaining > 0)
                {
                    int amount = Mathf.Min(amountRemaining, Configuration.Player.InventoryStackLimit);
                    amountRemaining -= amount;
                    player.GiveItem(ItemManager.CreateByItemID(item.info.itemid, amount, item.skin), BaseEntity.GiveItemReason.PickedUp);
                }

                item.Remove(0f);
                return true;
            }            

            return null;
        }
        #endregion
        
        #region Container Management
        private void OnEntityBuilt(Planner planner, GameObject obj)
        {
            if (!planner || !obj)
                return;

            BaseEntity baseEntity = obj.GetComponent<BaseEntity>();
            if (!baseEntity || baseEntity.OwnerID == 0UL)
                return;
            
            if (baseEntity is MiningQuarry)
            {
                OnQuarryBuilt(baseEntity as MiningQuarry);
                return;
            }

            if (!(baseEntity is StorageContainer)) 
                return;
            
            BasePlayer player = planner.GetOwnerPlayer();
            if (!player) 
                return;
            
            UpdateStorageContainerStackSize(baseEntity as StorageContainer);
        }

        private void OnLootEntity(BasePlayer player, StorageContainer storageContainer)
        {
            if (storageContainer.OwnerID == player.userID)
                UpdateStorageContainerStackSize(storageContainer);
        }

        private void OnQuarryBuilt(MiningQuarry miningQuarry)
        {
            StorageContainer hopperContainer = miningQuarry.hopperPrefab.instance as StorageContainer;
            if (hopperContainer)
                UpdateStorageContainerStackSize(hopperContainer);
            
            StorageContainer fuelContainer = miningQuarry.fuelStoragePrefab.instance as StorageContainer;
            if (hopperContainer)
                UpdateStorageContainerStackSize(fuelContainer);
        }
        
        private void UpdateStorageContainerStackSize(StorageContainer storageContainer)
        {
            StorageLimit storageLimit;

            foreach (KeyValuePair<string, VIPLimits> kvp in m_VIPLimits.Data)
            {
                if (storageContainer.OwnerID.HasPermission(kvp.Key) && kvp.Value.StorageOverrides.TryGetValue(storageContainer.PrefabName, out storageLimit))
                    goto SKIP_BASIC;
            }

            m_StorageLimits.Data.TryGetValue(storageContainer.PrefabName, out storageLimit);
            
            SKIP_BASIC:
            
            if (storageLimit != null && storageContainer.inventory.maxStackSize != storageLimit.MaxStackSize)
            {
                storageContainer.inventory.maxStackSize = storageLimit.MaxStackSize;
                storageContainer.maxStackSize = storageLimit.MaxStackSize;
                storageContainer.SendNetworkUpdate();
            }
        }
        #endregion
        
        #region Player Management

        private void OnPlayerRespawned(BasePlayer player) => SetPlayerStackSize(player);

        private void OnPlayerConnected(BasePlayer player)
        {
            SetPlayerStackSize(player);
            //CheckUserPermissions(player.UserIDString);
        }

        private void SetPlayerStackSize(BasePlayer player, bool unload = false)
        {
            if (!player || !player.inventory)
                return;

            if (unload)
            {
                player.inventory.containerWear.maxStackSize = 0;
                player.inventory.containerMain.maxStackSize = 0;
                player.inventory.containerBelt.maxStackSize = 0;
            }
            else
            {
                player.inventory.containerWear.maxStackSize = 1;
                player.inventory.containerMain.maxStackSize = Configuration.Player.InventoryStackLimit;
                player.inventory.containerBelt.maxStackSize = Configuration.Player.InventoryStackLimit;
            }

            player.inventory.SendSnapshot();
        }
        #endregion

        #region Functions
        private readonly string[] m_IgnoreItems = new string[] { "ammo.snowballgun", "blueprintbase", "rhib", "spraycandecal", "vehicle.chassis", "vehicle.module", "water", "water.salt" };
        
        private void CheckUpdateConfiguration()
        {
            foreach (ItemDefinition itemDefinition in ItemManager.itemList)
            {
                if (m_IgnoreItems.Contains(itemDefinition.shortname))
                    continue;
                
                m_DefaultItemStackSizes[itemDefinition.shortname] = itemDefinition.stackable;

                ItemModDeployable itemModDeployable = itemDefinition.GetComponent<ItemModDeployable>();
                if (itemModDeployable)
                {
                    m_PrefabNameToItemID[itemModDeployable.entityPrefab.resourcePath] = itemDefinition.itemid;

                    StorageContainer storageContainer = itemModDeployable.entityPrefab.GetEntity() as StorageContainer;
                    if (storageContainer)
                    {
                        m_DefaultStorageStackSizes[storageContainer.PrefabName] = storageContainer.maxStackSize;
                        m_ShortPrefabNameToPrefabName[storageContainer.ShortPrefabName.Replace(".deployed", "").Replace("_deployed", "")] = storageContainer.PrefabName;

                        StorageLimit storageLimit;
                        if (!m_StorageLimits.Data.TryGetValue(storageContainer.PrefabName, out storageLimit))
                        {
                            storageLimit = m_StorageLimits.Data[storageContainer.PrefabName] = new StorageLimit
                            {
                                MaxStackSize = storageContainer.maxStackSize,
                                ItemOverrides = new OrderedHash<string, StackLimit>()
                            };
                        }

                        storageLimit.NiceName = PrefabNameToNiceName(storageContainer.PrefabName);
                    }
                }

                StackLimit stackLimit;
                if (!m_StackLimits.Data.TryGetValue(itemDefinition.shortname, out stackLimit))
                    m_StackLimits.Data.Add(itemDefinition.shortname, stackLimit = new StackLimit(itemDefinition.stackable));

                stackLimit.ItemDefinition = itemDefinition;
                itemDefinition.stackable = stackLimit.GetStackSize();
            }

            StorageContainer[] resources = UnityEngine.Resources.FindObjectsOfTypeAll<StorageContainer>();

            for (int i = 0; i < resources.Length; i++)
            {
                StorageContainer storageContainer = resources[i];

                if (storageContainer && !(storageContainer is LootContainer))
                {
                    if (string.IsNullOrEmpty(storageContainer.PrefabName) || storageContainer.ShortPrefabName.EndsWith("_static"))
                        continue;

                    if (storageContainer is NPCVendingMachine || storageContainer.inventorySlots == 0 || m_StorageIgnoreList.Contains(storageContainer.PrefabName))
                        continue;

                    m_DefaultStorageStackSizes[storageContainer.PrefabName] = storageContainer.maxStackSize;
                    m_ShortPrefabNameToPrefabName[storageContainer.ShortPrefabName.Replace(".deployed", "").Replace("_deployed", "")] = storageContainer.PrefabName;
                    
                    StorageLimit storageLimit;
                    if (!m_StorageLimits.Data.TryGetValue(storageContainer.PrefabName, out storageLimit))
                    {
                        storageLimit = m_StorageLimits.Data[storageContainer.PrefabName] = new StorageLimit
                        {
                            MaxStackSize = storageContainer.maxStackSize,
                            ItemOverrides = new OrderedHash<string, StackLimit>()
                        };
                    }

                    storageLimit.NiceName = PrefabNameToNiceName(storageContainer.PrefabName);
                }
            }
            
            m_StackLimits.Save();
            m_StorageLimits.Save();
        }

        private bool IsUsingFurnaceSplitter(PlayerInventory playerInventory, Item item)
        {
            if (FurnaceSplitter == null || !playerInventory.loot.IsLooting() || !(playerInventory.loot.entitySource is BaseOven)) 
                return false;
            
            BasePlayer player = playerInventory.baseEntity;
            if (player)
            {
                object isEnabled = FurnaceSplitter.CallHook("GetEnabled", player);
                if (isEnabled is bool) 
                    return (bool) isEnabled;
            }

            List<BasePlayer> looters = FurnaceSplitter.Call<List<BasePlayer>>("GetLooters", playerInventory.loot.entitySource as BaseOven);
            
            if (looters != null && (looters.Contains(player) || playerInventory.loot.entitySource == item.GetRootContainer()?.entityOwner))
                return true;
            
            return false;
        }
        
        private void ResetContainerStackSizes()
        {
            foreach (BaseNetworkable baseNetworkable in BaseNetworkable.serverEntities)
            {
                StorageContainer storageContainer = baseNetworkable as StorageContainer;
                int maxStackSize;
                if (storageContainer && m_DefaultStorageStackSizes.TryGetValue(storageContainer.PrefabName, out maxStackSize))
                    storageContainer.maxStackSize = maxStackSize;
            }
        }

        private void ResetItemStackSizes()
        {
            foreach (ItemDefinition itemDefinition in ItemManager.itemList)
            {
                int stackable;
                if (m_DefaultItemStackSizes.TryGetValue(itemDefinition.shortname, out stackable))
                    itemDefinition.stackable = stackable;
            }
        }
        #endregion
        
        #region Prefab Nice Names

        private readonly string[] _replaceStrings = new string[]
        {
            "assets/content/vehicles/",
            "assets/content/structures/",
            "assets/content/",
            "assets/bundled/prefabs/static/",
            "assets/prefabs/building/wall.frame.shopfront/",
            "assets/prefabs/gamemodes/objects/",
            "assets/prefabs/misc/halloween/",
            "assets/prefabs/misc/chinesenewyear/sky_lantern/skylantern.",
            "assets/prefabs/misc/summer_dlc/",
            "assets/prefabs/misc/xmas/",
            "assets/prefabs/misc/",
            "assets/bundled/prefabs/",
            "assets/prefabs/deployable/",
            "assets/prefabs/npc/",
            "assets/prefabs/voiceaudio/",
            "assets/scenes/prefabs/",
            "trophy skulls/skins/",
            "cursed_cauldron/",
            "skull_fire_pit/",
            "subents/",
            "tool cupboard/",
            "woodenbox/",
            "repair bench/",
            "bigwheel/",
            "slotmachine/",
            "research table/",
            "photoframe/",
            "twitch/",
            "cassetterecorder/",
            "xmastree/",
            "flame turret/",
            "single shot trap/",
            "tuna can wall lamp/",
            "survivalfishtrap/",
            "reclaim/",
            "coffin/",
            "prefabs/",
            "marketplace/",
            "small stash/",
            "bbq/",
            "trains/",
            "composter/",
            "card table/",
            "jack o lantern/",
            "large wood storage/",
            "mailbox/",
            "mixingtable/",
            "planters/",
            "oil refinery/",
            "locker/",
            "hitch & trough/",
            "campfire/",
            "dropbox/",
            "furnace large/",
            "furnace/",
            "fridge/",
            ".prefab",
            ".deployed",
            ".entity",
            "snowmobiles/",
            "stockings/",
            "submarine/",
            "trainyard/",
            "vendingmachine/",
            "tier 1 workbench/",
            "tier 2 workbench/",
            "tier 3 workbench/",
            "caboose/blackjackmachine/",
            "boats/",
            "casino/",
            "chinesenewyear/chineselantern/",
            "frankensteintable/",
            "excavator/",
            "fireplace/",
            "locomotive/",
            "mlrs/",
            "modularcar/",
            "playerioents/",
            "wagons/",
            "workcart/",
            "wall.frame.",
            "scrap heli carrier/",
            "rowboat/",
            "rhib/",
            "furnace.large/",
            "hot air balloon/",
            "lantern/",
            "carvablepumpkin/",
            "trophy skulls/",
            "hobobarrel/"
        };
        
        private string PrefabNameToNiceName(string prefabName)
        {
            for (int i = 0; i < _replaceStrings.Length; i++)
                prefabName = prefabName.Replace(_replaceStrings[i], "");
            
            prefabName = prefabName.Replace(".", " ")
                                   .Replace("_", " ");

            string[] strs = prefabName.Split('/');
            for (int i = 0; i < strs.Length; i++)
            {
                string[] strs2 = strs[i].Split(' ');

                for (int j = 0; j < strs2.Length; j++)
                    strs2[j] = UppercaseFirstLetter(strs2[j]);
                
                strs[i] = string.Join(" ", strs2);
            }

            return string.Join(" | ", strs);
        }

        private string UppercaseFirstLetter(string str) => Char.ToUpper(str[0]) + str.Substring(1);

        #endregion
        
        #region Images
        private readonly Dictionary<string, string> m_PrefabIconUrls = new Dictionary<string, string>
        {
            ["assets/content/vehicles/modularcar/subents/modular_car_1mod_storage.prefab"] = "https://www.rustedit.io/images/imagelibrary/vehicle.1mod.storage.png",
            ["assets/content/vehicles/modularcar/subents/modular_car_v8_engine_storage.prefab"] = "https://www.rustedit.io/images/imagelibrary/vehicle.1mod.engine.png",
            ["assets/content/vehicles/modularcar/subents/modular_car_fuel_storage.prefab"] = "https://www.rustedit.io/images/stacksextended/modular-fuel-storage.png",
            ["assets/content/vehicles/modularcar/subents/modular_car_camper_storage.prefab"] = "https://www.rustedit.io/images/imagelibrary/vehicle.2mod.camper.png",
            ["assets/prefabs/deployable/bbq/bbq.campermodule.prefab"] = "https://www.rustedit.io/images/imagelibrary/vehicle.2mod.camper.png",
            ["assets/prefabs/deployable/locker/locker.campermodule.prefab"] = "https://www.rustedit.io/images/imagelibrary/vehicle.2mod.camper.png",
            ["assets/content/vehicles/modularcar/subents/modular_car_i4_engine_storage.prefab"] = "https://www.rustedit.io/images/imagelibrary/vehicle.1mod.cockpit.with.engine.png",
            ["assets/content/vehicles/workcart/subents/workcart_fuel_storage.prefab"] = "https://www.rustedit.io/images/stacksextended/workcart.png",
            ["assets/content/vehicles/locomotive/subents/locomotive_fuel_storage.prefab"] = "https://www.rustedit.io/images/stacksextended/locomotive.png",
            ["assets/content/vehicles/boats/rowboat/subents/fuel_storage.prefab"] = "https://www.rustedit.io/images/stacksextended/rowboat.png",
            ["assets/prefabs/deployable/oil jack/fuelstorage.prefab"] = "https://www.rustedit.io/images/stacksextended/pumpjack.png",
            ["assets/prefabs/deployable/oil jack/crudeoutput.prefab"] = "https://www.rustedit.io/images/stacksextended/pumpjack.png",
            ["assets/prefabs/deployable/quarry/fuelstorage.prefab"] = "https://www.rustedit.io/images/stacksextended/quarry.png",
            ["assets/prefabs/deployable/quarry/hopperoutput.prefab"] = "https://www.rustedit.io/images/stacksextended/quarry.png",
            ["assets/prefabs/deployable/hot air balloon/subents/hab_storage.prefab"] = "https://www.rustedit.io/images/stacksextended/hab.png",
            ["assets/content/vehicles/boats/rhib/subents/fuel_storage.prefab"] = "https://www.rustedit.io/images/stacksextended/rhib.png",
            ["assets/content/vehicles/boats/rowboat/subents/rowboat_storage.prefab"] = "https://www.rustedit.io/images/stacksextended/rowboat.png",
            ["assets/content/vehicles/boats/rhib/subents/rhib_storage.prefab"] = "https://www.rustedit.io/images/stacksextended/rhib.png",
            ["assets/content/vehicles/snowmobiles/subents/snowmobileitemstorage.prefab"] = "https://www.rustedit.io/images/stacksextended/snow-mobile.png",
            ["assets/content/vehicles/snowmobiles/subents/snowmobilefuelstorage.prefab"] = "https://www.rustedit.io/images/stacksextended/snow-mobile.png",
            ["assets/content/vehicles/mlrs/subents/mlrs_rocket_storage.prefab"] = "https://www.rustedit.io/images/stacksextended/mlrs.png",
            ["assets/content/vehicles/mlrs/subents/mlrs_dashboard_storage.prefab"] = "https://www.rustedit.io/images/stacksextended/mlrs.png",
            ["assets/content/vehicles/train/subents/wagon_storage_lootwagon.prefab"] = "https://www.rustedit.io/images/stacksextended/loot-wagon.png",
            ["assets/content/vehicles/train/subents/wagon_storage_fuel.prefab"] = "https://www.rustedit.io/images/stacksextended/fuel-wagon.png",
            ["assets/scenes/prefabs/trainyard/subents/coaling_tower_ore_storage.entity.prefab"] = "https://www.rustedit.io/images/stacksextended/coaling-fuel-storage.png",
            ["assets/scenes/prefabs/trainyard/subents/coaling_tower_fuel_storage.entity.prefab"] = "https://www.rustedit.io/images/stacksextended/coaling-ore-storage.png",
            ["assets/bundled/prefabs/static/bbq.static.prefab"] = "https://www.rustedit.io/images/imagelibrary/bbq.png",
            ["assets/bundled/prefabs/static/workbench1.static.prefab"] = "https://www.rustedit.io/images/imagelibrary/workbench1.png",
            ["assets/prefabs/misc/marketplace/marketterminal.prefab"] = "https://www.rustedit.io/images/stacksextended/marketplace.png",
            ["assets/bundled/prefabs/static/wall.frame.shopfront.metal.static.prefab"] = "https://www.rustedit.io/images/imagelibrary/wall.frame.shopfront.metal.png",
            ["assets/prefabs/deployable/card table/subents/cardtableplayerstorage.prefab"] = "https://www.rustedit.io/images/stacksextended/card-table.png",
            ["assets/prefabs/deployable/card table/subents/cardtablepotstorage.prefab"] = "https://www.rustedit.io/images/stacksextended/card-table.png",
            ["assets/prefabs/misc/casino/bigwheel/bigwheelbettingterminal.prefab"] = "https://www.rustedit.io/images/stacksextended/betting-terminal.png",
            ["assets/prefabs/misc/casino/slotmachine/slotmachinestorage.prefab"] = "https://www.rustedit.io/images/stacksextended/slot-machine.png",
            ["assets/bundled/prefabs/static/bbq.static_hidden.prefab"] = "https://www.rustedit.io/images/imagelibrary/bbq.png",
            ["assets/bundled/prefabs/static/workbench2.static.prefab"] = "https://www.rustedit.io/images/imagelibrary/workbench2.png",
            ["assets/prefabs/voiceaudio/cassetterecorder/cassetterecorder.deployed.prefab"] = "https://www.rustedit.io/images/stacksextended/cassette-recorder.png",
            ["assets/prefabs/gamemodes/objects/reclaim/reclaimterminal.prefab"] = "https://www.rustedit.io/images/stacksextended/reclaim-terminal.png",
            ["assets/prefabs/gamemodes/objects/reclaim/reclaimbackpack.prefab"] = "https://www.rustedit.io/images/stacksextended/reclaim-terminal.png",
            ["assets/content/vehicles/submarine/subents/submarineitemstorage.prefab"] = "https://www.rustedit.io/images/stacksextended/submarine.png",
            ["assets/content/vehicles/submarine/subents/submarinetorpedostorage.prefab"] = "https://www.rustedit.io/images/stacksextended/submarine.png",
            ["assets/content/vehicles/submarine/subents/submarinefuelstorage.prefab"] = "https://www.rustedit.io/images/stacksextended/submarine.png",
            ["assets/content/vehicles/scrap heli carrier/subents/fuel_storage_scrapheli.prefab"] = "https://www.rustedit.io/images/stacksextended/scrap-heli.png",
            ["assets/content/vehicles/minicopter/subents/fuel_storage.prefab"] = "https://www.rustedit.io/images/stacksextended/minicopter.png",
            ["assets/content/structures/excavator/prefabs/excavator_output_pile.prefab"] = "https://www.rustedit.io/images/stacksextended/excavator.png",
            ["assets/content/structures/excavator/prefabs/engine.prefab"] = "https://www.rustedit.io/images/stacksextended/excavator.png",
        };

        private readonly string[] m_StorageIgnoreList = new string[]
        {
            "assets/bundled/prefabs/modding/events/twitch/twitch_dropbox.deployed.prefab",
            "assets/content/vehicles/train/subents/wagon_storage.prefab",
            "assets/content/vehicles/modularcar/subents/modular_car_1mod_trade.prefab"
        };

        private void RegisterImages()
        {
            if (ImageLibrary.IsLoaded)
            {
                ImageLibrary.AddImage("https://chaoscode.io/oxide/Images/magnifyingglass.png", "adminmenu.search", 0UL, () =>
                {
                    m_MagnifyImage = ImageLibrary.GetImage("adminmenu.search", 0UL);
                });
                ImageLibrary.ImportImageList("StacksExtended", m_PrefabIconUrls, 0UL, false, null);
            }
        }

        private string GetImage(string name, ulong skinId = 0UL) => ImageLibrary.IsLoaded ? ImageLibrary.GetImage(name, skinId) : string.Empty;
        #endregion

        #region UI Creation
        private const string STACKS_UI = "stacksextended.ui";
        private const string POPUP_UI = "stacksextended.popup.ui";

        private readonly Hash<ulong, UIUser> m_UIUsers = new Hash<ulong, UIUser>();

        private string[] m_CharacterFilter = new string[] { "~", "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z" };
        private ItemCategory[] m_ItemCategoryTypes = new ItemCategory[] {ItemCategory.Weapon, ItemCategory.Construction, ItemCategory.Items, ItemCategory.Resources, ItemCategory.Attire, ItemCategory.Tool, ItemCategory.Medical, ItemCategory.Food, ItemCategory.Ammunition, ItemCategory.Traps, ItemCategory.Misc, ItemCategory.Component, ItemCategory.Electrical, ItemCategory.Fun};

        private UICategory[] m_UICategories;
        private Hash<ItemCategory, List<ItemDefinition>> m_ItemDefinitionsPerCategory;

        private string m_MagnifyImage;
        
        
        public enum UICategory { Item, Storage, PlayerOverrides, VIPStorage }

        public class UIUser
        { 
            public BasePlayer Player;
            
            public UICategory Category = UICategory.Item;
            public ItemCategory ItemCategory = ItemCategory.Weapon;

            public StorageLimit ContainerItemOverride = null;
            
            public string Permission = string.Empty;
            
            public string SearchFilter = string.Empty;
            public string CharacterFilter = "~";
            
            public int Page = 0;

            public UIUser(BasePlayer player)
            {
                Player = player;
            }
            
            public void Reset()
            {
                ContainerItemOverride = null;
                SearchFilter = string.Empty;
                CharacterFilter = "~";
                Page = 0;
                Permission = string.Empty;
                ItemCategory = ItemCategory.Weapon;
            }
        }

        private CommandCallbackHandler m_CallbackHandler;
        
        #region Styles
        private Style m_PanelStyle = new Style
        {
            ImageColor = new Color(1f, 1f, 1f, 0.1647059f),
            Sprite = Sprites.Background_Rounded,
            ImageType = Image.Type.Tiled,
        };

        private Style m_ButtonStyle = new Style
        {
            ImageColor = new Color(0.1647059f, 0.1803922f, 0.1921569f, 1f),
            Sprite = Sprites.Background_Rounded,
            ImageType = Image.Type.Tiled,
            Alignment = TextAnchor.MiddleCenter,
        };
        
        private Style m_DisabledButtonStyle = new Style
        {
            ImageColor = new Color(0.1647059f, 0.1803922f, 0.1921569f, 0.8f),
            Sprite = Sprites.Background_Rounded,
            ImageType = Image.Type.Tiled,
            FontColor = new Color(1f, 1f, 1f, 0.2f),
            Alignment = TextAnchor.MiddleCenter
        };

        private Style m_BackgroundStyle = new Style
        {
            ImageColor = new Color(0.08235294f, 0.08235294f, 0.08235294f, 0.9490196f),
            Sprite = Sprites.Background_Rounded,
            Material = Materials.BackgroundBlur,
            ImageType = Image.Type.Tiled,
        };
        
        private OutlineComponent m_OutlineGreen = new OutlineComponent(new Color(0.7695657f, 1f, 0f, 1f));
        private OutlineComponent m_OutlineRed = new OutlineComponent(new Color(0.8078431f, 0.2588235f, 0.1686275f, 1f));
        private OutlineComponent m_OutlineWhite = new OutlineComponent(new Color(0.1647059f, 0.1803922f, 0.1921569f, 1f));
        #endregion
        
        #region Layout Groups

        private HorizontalLayoutGroup m_CategoryLayout = new HorizontalLayoutGroup()
        {
            Area = new Area(-535f, -15f, 535f, 15f),
            Spacing = new Spacing(5f, 0f),
            Corner = Corner.Centered,
            FixedSize = new Vector2(120, 20),
            FixedCount = new Vector2Int(4, 1),
        };

        private VerticalLayoutGroup m_SearchFilterLayout = new VerticalLayoutGroup()
        {
            Area = new Area(-10f, -257.5f, 10f, 257.5f),
            Spacing = new Spacing(0f, 2f),
            Corner = Corner.Centered,
            FixedSize = new Vector2(16, 16),
            FixedCount = new Vector2Int(1, 27),
        };
        
        private readonly GridLayoutGroup m_PermissionGridLayout = new GridLayoutGroup(5, 15, Axis.Vertical)
        {
            Area = new Area(-522.5f, -257.5f, 522.5f, 257.5f),
            Spacing = new Spacing(5f, 5f),
            Padding = new Padding(5f, 5f, 5f, 5f),
            Corner = Corner.TopLeft,
        };

        private readonly GridLayoutGroup m_ItemGridLayout = new GridLayoutGroup(4, 6, Axis.Horizontal)
        {
            Area = new Area(-522.5f, -257.5f, 522.5f, 257.5f),
            Spacing = new Spacing(5f, 5f),
            Padding = new Padding(5f, 5f, 5f, 5f),
            Corner = Corner.TopLeft,
        };
        
        private readonly HorizontalLayoutGroup m_SubMenuLayout = new HorizontalLayoutGroup()
        {
            Area = new Area(-535f, -12.5f, 535f, 12.5f),
            Spacing = new Spacing(5f, 5f),
            Corner = Corner.Centered,
            FixedSize = new Vector2(71.5f, 20),
            FixedCount = new Vector2Int(14, 1),
        };
        #endregion
        
        private void InitializeUI()
        {
            m_CallbackHandler = new CommandCallbackHandler(this);

            m_UICategories = (UICategory[]) Enum.GetValues(typeof(UICategory));
            
            m_ItemDefinitionsPerCategory = new Hash<ItemCategory, List<ItemDefinition>>();
            
            foreach (ItemDefinition itemDefinition in ItemManager.itemList)
            {
                if (string.IsNullOrEmpty(itemDefinition.displayName.english) || itemDefinition.hidden)
                    continue;
                
                List<ItemDefinition> list;
                if (!m_ItemDefinitionsPerCategory.TryGetValue(itemDefinition.category, out list))
                    list = m_ItemDefinitionsPerCategory[itemDefinition.category] = new List<ItemDefinition>();
			    
                list.Add(itemDefinition);
            }

            foreach (KeyValuePair<ItemCategory, List<ItemDefinition>> kvp in m_ItemDefinitionsPerCategory)
                kvp.Value.Sort(((a, b) => a.displayName.english.CompareTo(b.displayName.english)));
            
            RegisterImages();
        }

        private void OpenStacksUI(BasePlayer player)
        {
            UIUser uiUser;
            if (!m_UIUsers.TryGetValue(player.userID, out uiUser))
                uiUser = m_UIUsers[player.userID] = new UIUser(player);

            BaseContainer root = ImageContainer.Create(STACKS_UI, Layer.Overall, Anchor.Center, new Offset(-540f, -310f, 540f, 310f))
                .WithStyle(m_BackgroundStyle)
                .NeedsCursor()
                .NeedsKeyboard()
                .DestroyExisting()
                .WithChildren(parent =>
                {
                    CreateTitleBar(uiUser, parent);
                    CreateSearchFilterBar(uiUser, parent);

                    switch (uiUser.Category)
                    {
                        case UICategory.Item:
                            CreateSubMenuBar(uiUser, parent, m_ItemCategoryTypes, CreateSubmenuCategory);
                            
                            CreateItemGridLayout(uiUser, parent, "", m_StackLimits.Data.Keys, CreateItemEntry);
                            break;

                        case UICategory.Storage:
                            CreateSubMenuBar(uiUser, parent, Array.Empty<string>(), null);

                            if (uiUser.ContainerItemOverride != null)
                                CreateItemGridLayout(uiUser, parent, FormatString("Label.ContainerItemOverrides", uiUser.Player, uiUser.ContainerItemOverride.NiceName), uiUser.ContainerItemOverride.ItemOverrides.Keys, CreateItemEntry);
                            else CreateStorageGridLayout(uiUser, parent, GetString("Label.StorageHint", uiUser.Player), m_StorageLimits.Data.Keys, CreateStorageEntry);
                            break;

                        case UICategory.PlayerOverrides:
                            CreateSubMenuBar(uiUser, parent, Array.Empty<string>(), null);
                            
                            CreateItemGridLayout(uiUser, parent, GetString("Label.PlayerInventoryOverrides", uiUser.Player), m_PlayerLimits.Data.Keys, CreateItemEntry);
                            break;

                        case UICategory.VIPStorage:
                            CreateSubMenuBar(uiUser, parent, Array.Empty<string>(), null);
                            
                            if (string.IsNullOrEmpty(uiUser.Permission))
                                CreatePermissionLayout(uiUser, parent);
                            else
                            {
                                if (uiUser.ContainerItemOverride != null)
                                    CreateItemGridLayout(uiUser, parent, FormatString("Label.ContainerItemOverrides.Permission", uiUser.Player, uiUser.ContainerItemOverride.NiceName, uiUser.Permission), uiUser.ContainerItemOverride.ItemOverrides.Keys, CreateItemEntry);
                                else
                                {
                                    VIPLimits vipLimits = m_VIPLimits.Data[uiUser.Permission];
                                    BaseContainer.Create(parent, Anchor.TopStretch, new Offset(5f, -65f, -5f, -40f))
                                        .WithChildren(subMenu =>
                                        {
                                            TextContainer.Create(subMenu, Anchor.CenterLeft, new Offset(10f, -10f, 55f, 10f))
                                                .WithText(GetString("Label.VIPPriority", uiUser.Player.UserIDString))
                                                .WithAlignment(TextAnchor.MiddleLeft);

                                            ImageContainer.Create(subMenu, Anchor.CenterLeft, new Offset(60f, -10f, 100f, 10f))
                                                .WithStyle(m_ButtonStyle)
                                                .WithChildren(searchInput =>
                                                {
                                                    InputFieldContainer.Create(searchInput, Anchor.FullStretch, new Offset(5f, 0f, -5f, 0f))
                                                        .WithText(vipLimits.Priority.ToString())
                                                        .WithAlignment(TextAnchor.MiddleCenter)
                                                        .WithCallback(m_CallbackHandler, arg =>
                                                            {
                                                                int value = arg.GetInt(1);
                                                                if (value == vipLimits.Priority)
                                                                    return;

                                                                vipLimits.Priority = value;
                                                                m_VIPLimits.Save();

                                                                OpenStacksUI(uiUser.Player);
                                                            }, $"{uiUser.Player.UserIDString}.vip.priority");
                                                });
                                        });
                                    
                                    CreateStorageGridLayout(uiUser, parent, FormatString("Label.VIPStorage", player, uiUser.Permission), vipLimits.StorageOverrides.Keys, CreateStorageEntry);
                                }
                            }

                            break;
                    }
                });

            ChaosUI.Show(player, root);
        }

        #region Bars
        private void CreateTitleBar(UIUser uiUser, BaseContainer parent)
        {
            ImageContainer.Create(parent, Anchor.TopStretch, new Offset(5f, -35f, -5f, -5f))
                .WithStyle(m_PanelStyle)
                .WithChildren(titlebar =>
                {
                    TextContainer.Create(titlebar, Anchor.FullStretch, new Offset(10f, 0f, 0f, 0f))
                        .WithSize(18)
                        .WithText($"{Title} v{Version}")
                        .WithAlignment(TextAnchor.MiddleLeft)
                        .WithOutline(m_OutlineWhite);

                    // Header Buttons
                    BaseContainer.Create(titlebar, Anchor.FullStretch, Offset.zero)
                        .WithLayoutGroup(m_CategoryLayout, m_UICategories, 0, (int i, UICategory t, BaseContainer buttons, Anchor anchor, Offset offset) =>
                        {
                            BaseContainer button = ImageContainer.Create(buttons, anchor, offset)
                                .WithStyle(m_ButtonStyle)
                                .WithChildren(items =>
                                {
                                    TextContainer.Create(items, Anchor.FullStretch, Offset.zero)
                                        .WithText(GetString($"Button.{t}", uiUser.Player.UserIDString))
                                        .WithAlignment(TextAnchor.MiddleCenter);

                                    ButtonContainer.Create(items, Anchor.FullStretch, Offset.zero)
                                        .WithColor(Color.Clear)
                                        .WithCallback(m_CallbackHandler, arg =>
                                        {
                                            uiUser.Reset();
                                            uiUser.Category = t;
                                            OpenStacksUI(uiUser.Player);
                                        }, $"{uiUser.Player.UserIDString}.category.{t}");

                                });

                            if (uiUser.Category == t)
                                button.WithOutline(m_OutlineGreen);
                        });

                    // Exit Button
                    ImageContainer.Create(titlebar, Anchor.CenterRight, new Offset(-55f, -10f, -5f, 10f))
                        .WithStyle(m_ButtonStyle)
                        .WithOutline(m_OutlineRed)
                        .WithChildren(exit =>
                        {
                            TextContainer.Create(exit, Anchor.FullStretch, Offset.zero)
                                .WithText(GetString("Button.Exit", uiUser.Player.UserIDString))
                                .WithAlignment(TextAnchor.MiddleCenter);

                            ButtonContainer.Create(exit, Anchor.FullStretch, Offset.zero)
                                .WithColor(Color.Clear)
                                .WithCallback(m_CallbackHandler, arg =>
                                {
                                    ChaosUI.Destroy(uiUser.Player, STACKS_UI);
                                    ChaosUI.Destroy(uiUser.Player, POPUP_UI);
                                    m_UIUsers.Remove(uiUser.Player.userID);
                                }, $"{uiUser.Player.UserIDString}.exit");
                        });

                });
        }

        private BaseContainer CreateHeaderBar(UIUser uiUser, BaseContainer parent, string label, bool pageUp, bool pageDown)
        {
            return ImageContainer.Create(parent, Anchor.TopStretch, new Offset(5f, -95f, -5f, -70f))
                .WithStyle(m_PanelStyle)
			    .WithChildren(header =>
			    {
				    ImageContainer.Create(header, Anchor.CenterLeft, new Offset(5f, -10f, 35f, 10f))
                        .WithStyle(pageDown ? m_ButtonStyle : m_DisabledButtonStyle)
					    .WithChildren(backButton =>
					    {
						    TextContainer.Create(backButton, Anchor.FullStretch, Offset.zero)
							    .WithText("<<<")
                                .WithStyle(pageDown ? m_ButtonStyle : m_DisabledButtonStyle);

                            if (pageDown)
                            {
                                ButtonContainer.Create(backButton, Anchor.FullStretch, Offset.zero)
                                    .WithColor(Color.Clear)
                                    .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        uiUser.Page--;
                                        OpenStacksUI(uiUser.Player);
                                    }, $"{uiUser.Player.UserIDString}.back");
                            }
                        });

				    ImageContainer.Create(header, Anchor.CenterRight, new Offset(-35f, -10f, -5f, 10f))
                        .WithStyle(pageUp ? m_ButtonStyle : m_DisabledButtonStyle)
					    .WithChildren(nextButton =>
					    {
						    TextContainer.Create(nextButton, Anchor.FullStretch, Offset.zero)
							    .WithText(">>>")
                                .WithStyle(pageUp ? m_ButtonStyle : m_DisabledButtonStyle);

                            if (pageUp)
                            {
                                ButtonContainer.Create(nextButton, Anchor.FullStretch, Offset.zero)
                                    .WithColor(Color.Clear)
                                    .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        uiUser.Page++;
                                        OpenStacksUI(uiUser.Player);
                                    }, $"{uiUser.Player.UserIDString}.next");
                            }
                        });

				    ImageContainer.Create(header, Anchor.CenterRight, new Offset(-240f, -10f, -40f, 10f))
					    .WithStyle(m_ButtonStyle)
					    .WithChildren(searchInput =>
					    {
						    InputFieldContainer.Create(searchInput, Anchor.FullStretch, new Offset(5f, 0f, -5f, 0f))
                                .WithText(uiUser.SearchFilter)
							    .WithAlignment(TextAnchor.MiddleLeft)
                                .WithCallback(m_CallbackHandler, arg =>
                                {
                                    uiUser.SearchFilter = arg.Args.Length > 1 ? string.Join(" ", arg.Args.Skip(1)) : string.Empty;
                                    uiUser.Page = 0;
                                    OpenStacksUI(uiUser.Player);
                                }, $"{uiUser.Player.UserIDString}.searchinput");

					    });

                    if (!string.IsNullOrEmpty(m_MagnifyImage))
                    {
                        RawImageContainer.Create(header, Anchor.Center, new Offset(275f, -10f, 295f, 10f))
                            .WithPNG(m_MagnifyImage);
                    }

				    TextContainer.Create(header, Anchor.Center, new Offset(-200f, -12.5f, 200f, 12.5f))
					    .WithText(label)
					    .WithAlignment(TextAnchor.MiddleCenter);

			    });
        }

        private void CreateSubMenuBar<T>(UIUser uiUser, BaseContainer parent, IEnumerable<T> collection, Action<UIUser, T, BaseContainer, Anchor, Offset> createAction)
        {
            ImageContainer.Create(parent, Anchor.TopStretch, new Offset(5f, -65f, -5f, -40f))
                .WithStyle(m_PanelStyle)
                .WithLayoutGroup(m_SubMenuLayout, collection, 0, (int i, T t, BaseContainer subMenu, Anchor anchor, Offset offset) => createAction(uiUser, t, subMenu, anchor, offset));
        }

        private void CreateSubmenuCategory(UIUser uiUser, ItemCategory t, BaseContainer parent, Anchor anchor, Offset offset)
        {
            BaseContainer baseContainer = ImageContainer.Create(parent, anchor, offset)
                .WithStyle(m_ButtonStyle)
                .WithChildren(commands =>
                {
                    TextContainer.Create(commands, Anchor.FullStretch, Offset.zero)
                        .WithSize(12)
                        .WithText(t.ToString())
                        .WithAlignment(TextAnchor.MiddleCenter);

                    ButtonContainer.Create(commands, Anchor.FullStretch, Offset.zero)
                        .WithColor(Color.Clear)
                        .WithCallback(m_CallbackHandler, arg =>
                        {
                            uiUser.Reset();
                            uiUser.ItemCategory = t;
                            OpenStacksUI(uiUser.Player);
                        }, $"{uiUser.Player.UserIDString}.itemcategory.{t}");

                });

            if (uiUser.ItemCategory == t)
                baseContainer.WithOutline(m_OutlineGreen);
        }

        private void CreateSearchFilterBar(UIUser uiUser, BaseContainer parent)
        {
            ImageContainer.Create(parent, Anchor.LeftStretch, new Offset(5f, 5f, 25f, -100f))
                .WithStyle(m_PanelStyle)
                .WithLayoutGroup(m_SearchFilterLayout, m_CharacterFilter, 0, (int i, string t, BaseContainer filterList, Anchor anchor, Offset offset) =>
                {
                    BaseContainer filterButton = ImageContainer.Create(filterList, anchor, offset)
                        .WithStyle(m_ButtonStyle)
                        .WithChildren(characterTemplate =>
                        {
                            TextContainer.Create(characterTemplate, Anchor.FullStretch, Offset.zero)
                                .WithSize(12)
                                .WithText(t)
                                .WithAlignment(TextAnchor.MiddleCenter);

                            if (t != uiUser.CharacterFilter)
                            {
                                ButtonContainer.Create(characterTemplate, Anchor.FullStretch, Offset.zero)
                                    .WithColor(Color.Clear)
                                    .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        uiUser.CharacterFilter = t;
                                        uiUser.Page = 0;
                                        OpenStacksUI(uiUser.Player);
                                    }, $"{uiUser.Player.UserIDString}.filter.{i}");
                            }
                        });

                    if (t == uiUser.CharacterFilter)
                        filterButton.WithOutline(m_OutlineGreen);
                });
        }
        #endregion
        
        #region Grids
        private void CreateItemGridLayout(UIUser uiUser, BaseContainer parent, string label, IEnumerable<string> keys, Action<UIUser, ItemDefinition, BaseContainer, Anchor, Offset> createElement)
        {
            List<ItemDefinition> dst = Facepunch.Pool.GetList<ItemDefinition>();

            if (uiUser.CharacterFilter != m_CharacterFilter[0] || !string.IsNullOrEmpty(uiUser.SearchFilter))
            {
                FilterList(ItemManager.itemList, dst, uiUser,
                    ((s, pair) => StartsWithValidator(s, pair.displayName.english)),
                    (s, pair) => ContainsValidator(s, pair.displayName.english));
            }
            else dst.AddRange(uiUser.Category != UICategory.Item ? ItemManager.itemList : m_ItemDefinitionsPerCategory[uiUser.ItemCategory]);

            for (int i = dst.Count - 1; i >= 0; i--)
            {
                if (!keys.Contains(dst[i].shortname) || Configuration.Exclude.IsExcluded(dst[i].shortname))
                    dst.RemoveAt(i);
            }
            
            dst.Sort((a, b) => a.displayName.english.CompareTo(b.displayName.english));
            
            BaseContainer header = CreateHeaderBar(uiUser, parent, label, m_ItemGridLayout.HasNextPage(uiUser.Page, dst.Count), uiUser.Page > 0);

            if (uiUser.Category >= UICategory.PlayerOverrides || uiUser.ContainerItemOverride != null)
            {
                ImageContainer.Create(header, Anchor.CenterLeft, new Offset(40f, -10f, 180f, 10f))
                    .WithStyle(m_ButtonStyle)
                    .WithChildren(addButton =>
                    {
                        TextContainer.Create(addButton, Anchor.FullStretch, Offset.zero)
                            .WithText(GetString("Button.AddItemOverride", uiUser.Player))
                            .WithAlignment(TextAnchor.MiddleCenter);

                        ButtonContainer.Create(addButton, Anchor.FullStretch, Offset.zero)
                            .WithColor(Color.Clear)
                            .WithCallback(m_CallbackHandler, arg =>
                            {
                                CreateItemOverrideSelector(uiUser, (definition =>
                                {
                                    StackLimit stackLimit = new StackLimit
                                    {
                                        StackMultiplier = 1,
                                        MaxStackSize = definition.stackable
                                    };

                                    if (uiUser.ContainerItemOverride != null)
                                        uiUser.ContainerItemOverride.ItemOverrides[definition.shortname] = stackLimit;
                                    else m_PlayerLimits.Data[definition.shortname] = stackLimit;
                                        
                                    if (uiUser.Category == UICategory.PlayerOverrides)
                                        m_PlayerLimits.Save();
                                    else if (uiUser.Category == UICategory.Storage)
                                        m_StorageLimits.Save();
                                    else if (uiUser.Category == UICategory.VIPStorage)
                                        m_VIPLimits.Save();

                                    OpenStacksUI(uiUser.Player);
                                }));
                            }, $"{uiUser.Player.UserIDString}.addoverride");
                    });
            }

            ImageContainer.Create(parent, Anchor.FullStretch, new Offset(30f, 5f, -5f, -100f))
			.WithStyle(m_PanelStyle)
			.WithLayoutGroup(m_ItemGridLayout, dst, uiUser.Page, (int i, ItemDefinition t, BaseContainer layout, Anchor anchor, Offset offset) => createElement(uiUser, t, layout, anchor, offset));
            
            Facepunch.Pool.FreeList(ref dst);
        }
        
        private void CreateStorageGridLayout(UIUser uiUser, BaseContainer parent, string label, IEnumerable<string> keys, Action<UIUser, string, BaseContainer, Anchor, Offset> createElement)
        {
            List<string> dst = Facepunch.Pool.GetList<string>();

            if (uiUser.CharacterFilter != m_CharacterFilter[0] || !string.IsNullOrEmpty(uiUser.SearchFilter))
            {
                FilterList(keys, dst, uiUser, 
                    ((s, pair) => StartsWithValidator(s, pair)), 
                    (s, pair) => ContainsValidator(s, pair));
            }
            else dst.AddRange(keys);
            
            dst.Sort((a, b) => m_StorageLimits.Data[a].NiceName.CompareTo(m_StorageLimits.Data[b].NiceName));
            
            BaseContainer header = CreateHeaderBar(uiUser, parent, label, m_ItemGridLayout.HasNextPage(uiUser.Page, dst.Count), uiUser.Page > 0);

            if (uiUser.Category >= UICategory.VIPStorage)
            {
                ImageContainer.Create(header, Anchor.CenterLeft, new Offset(40f, -10f, 190f, 10f))
                    .WithStyle(m_ButtonStyle)
                    .WithChildren(addButton =>
                    {
                        TextContainer.Create(addButton, Anchor.FullStretch, Offset.zero)
                            .WithText(GetString("Button.AddStorageOverride", uiUser.Player))
                            .WithAlignment(TextAnchor.MiddleCenter);

                        ButtonContainer.Create(addButton, Anchor.FullStretch, Offset.zero)
                            .WithColor(Color.Clear)
                            .WithCallback(m_CallbackHandler, arg =>
                            {
                                CreateStorageOverrideSelector(uiUser, (prefab =>
                                {
                                    m_VIPLimits.Data[uiUser.Permission].StorageOverrides[prefab] = new StorageLimit 
                                    { 
                                        MaxStackSize = m_DefaultStorageStackSizes[prefab], 
                                        ItemOverrides = new OrderedHash<string, StackLimit>(),
                                        NiceName = PrefabNameToNiceName(prefab) 
                                    };
                                    m_VIPLimits.Save();
                                    OpenStacksUI(uiUser.Player);
                                }));
                            }, $"{uiUser.Player.UserIDString}.addoverride");
                    });
            }

            ImageContainer.Create(parent, Anchor.FullStretch, new Offset(30f, 5f, -5f, -100f))
			.WithStyle(m_PanelStyle)
			.WithLayoutGroup(m_ItemGridLayout, dst, uiUser.Page, (int i, string t, BaseContainer layout, Anchor anchor, Offset offset) => createElement(uiUser, t, layout, anchor, offset));
            
            Facepunch.Pool.FreeList(ref dst);
        }
        
        private void CreatePermissionLayout(UIUser uiUser, BaseContainer parent)
        {
            List<string> dst = Facepunch.Pool.GetList<string>();

            if (uiUser.CharacterFilter != m_CharacterFilter[0] || !string.IsNullOrEmpty(uiUser.SearchFilter))
            {
                FilterList(m_VIPLimits.Data.Keys, dst, uiUser, 
                    ((s, pair) => StartsWithValidator(s, pair)), 
                    (s, pair) => ContainsValidator(s, pair));
            }
            else dst.AddRange(m_VIPLimits.Data.Keys);
            
            BaseContainer header = CreateHeaderBar(uiUser, parent, GetString("Label.SelectPermission", uiUser.Player), m_ItemGridLayout.HasNextPage(uiUser.Page, dst.Count), uiUser.Page > 0);
            
            ImageContainer.Create(header, Anchor.CenterLeft, new Offset(40f, -10f, 180f, 10f))
                .WithStyle(m_ButtonStyle)
                .WithChildren(addButton =>
                {
                    TextContainer.Create(addButton, Anchor.FullStretch, Offset.zero)
                        .WithText(GetString("Button.AddCustomPermission", uiUser.Player))
                        .WithAlignment(TextAnchor.MiddleCenter);

                    ButtonContainer.Create(addButton, Anchor.FullStretch, Offset.zero)
                        .WithColor(Color.Clear)
                        .WithCallback(m_CallbackHandler, arg =>
                        {
                            CreateCustomPermissionCreator(uiUser);
                        }, $"{uiUser.Player.UserIDString}.addpermission");
                });

            ImageContainer.Create(parent, Anchor.FullStretch, new Offset(30f, 5f, -5f, -100f))
                .WithStyle(m_PanelStyle)
                .WithLayoutGroup(m_PermissionGridLayout, dst, uiUser.Page, (int i, string t, BaseContainer permissionLayout, Anchor anchor, Offset offset) =>
                {
                    ImageContainer.Create(permissionLayout, anchor, offset)
                        .WithStyle(m_ButtonStyle)
                        .WithChildren(permissionTemplate =>
                        {
                            TextContainer.Create(permissionTemplate, Anchor.FullStretch, Offset.zero)
                                .WithSize(12)
                                .WithText(t)
                                .WithAlignment(TextAnchor.MiddleCenter);

                            ButtonContainer.Create(permissionTemplate, Anchor.FullStretch, Offset.zero)
                                .WithColor(Color.Clear)
                                .WithCallback(m_CallbackHandler, arg =>
                                {
                                    uiUser.Permission = t;
                                    OpenStacksUI(uiUser.Player);
                                }, $"{uiUser.Player.UserIDString}.permission.{t}");
                        });

                });
            
            Facepunch.Pool.FreeList(ref dst);
        }
        #endregion

        #region Filtering
        private void FilterList<T>(IEnumerable<T> src, List<T> dst, UIUser uiUser, Func<string, T, bool> startsWith, Func<string, T, bool> contains)
        {
            bool useCharacterFilter = !string.IsNullOrEmpty(uiUser.CharacterFilter) && uiUser.CharacterFilter != m_CharacterFilter[0];
            bool useSearchFilter = !string.IsNullOrEmpty(uiUser.SearchFilter);
				                
            if (!useCharacterFilter && !useSearchFilter)
                dst.AddRange(src);
            else
            {
                foreach (T t in src)
                {
                    if (useSearchFilter && useCharacterFilter)
                    {
                        if (startsWith(uiUser.CharacterFilter, t) && contains(uiUser.SearchFilter, t))
                            dst.Add(t);

                        continue;
                    }

                    if (useCharacterFilter)
                    {
                        if (startsWith(uiUser.CharacterFilter, t))
                            dst.Add(t);
				                
                        continue;
                    }
						                
                    if (useSearchFilter && contains(uiUser.SearchFilter, t))
                        dst.Add(t);
                }
            }
        }

        private bool StartsWithValidator(string character, string phrase) => phrase.StartsWith(character, StringComparison.OrdinalIgnoreCase);
                
        private bool ContainsValidator(string character, string phrase) => phrase.Contains(character, CompareOptions.OrdinalIgnoreCase);
        #endregion
        
        #region Grid Entries
        private void CreateItemEntry(UIUser uiUser, ItemDefinition t, BaseContainer layout, Anchor anchor, Offset offset)
        {
            StackLimit stackLimit = uiUser.Category == UICategory.Item ? m_StackLimits.Data[t.shortname] :
                                    uiUser.Category == UICategory.PlayerOverrides ? m_PlayerLimits.Data[t.shortname] : 
                                    uiUser.ContainerItemOverride != null ? uiUser.ContainerItemOverride.ItemOverrides[t.shortname] : null;
            
            if (stackLimit == null)
                return;
            
            ImageContainer.Create(layout, anchor, offset)
                .WithStyle(m_PanelStyle)
                .WithChildren(item =>
                {
                    ImageContainer.Create(item, Anchor.CenterLeft, new Offset(5.5f, -32f, 69.5f, 32f))
                        .WithIcon(t.itemid);

                    TextContainer.Create(item, Anchor.TopStretch, new Offset(74f, -20f, 0f, 0f))
                        .WithSize(12)
                        .WithText(t.displayName.english)
                        .WithAlignment(TextAnchor.MiddleLeft);

                    TextContainer.Create(item, Anchor.TopStretch, new Offset(74f, -40f, 0f, -20f))
                        .WithSize(12)
                        .WithText(GetString("Label.StackSize", uiUser.Player))
                        .WithAlignment(TextAnchor.MiddleLeft)
                        .WithChildren(stackSize =>
                        {
                            ImageContainer.Create(stackSize, Anchor.FullStretch, new Offset(100f, 1f, -5f, -1f))
                                .WithStyle(m_ButtonStyle)
                                .WithChildren(input =>
                                {
                                    InputFieldContainer.Create(input, Anchor.FullStretch, Offset.zero)
                                        .WithSize(12)
                                        .WithText(stackLimit.MaxStackSize.ToString())
                                        .WithAlignment(TextAnchor.MiddleCenter)
                                        .WithCallback(m_CallbackHandler, arg =>
                                        {
                                            int maxStackSize = arg.GetInt(1);
                                            if (maxStackSize == stackLimit.MaxStackSize)
                                                return;
                                            
                                            stackLimit.MaxStackSize = maxStackSize;

                                            switch (uiUser.Category)
                                            {
                                                case UICategory.Item:
                                                    stackLimit.ItemDefinition.stackable = stackLimit.GetStackSize();
                                                    m_StackLimits.Save();
                                                    break;
                                                case UICategory.Storage:
                                                    m_StorageLimits.Save();
                                                    break;
                                                case UICategory.PlayerOverrides:
                                                    m_PlayerLimits.Save();
                                                    break;
                                                case UICategory.VIPStorage:
                                                    m_VIPLimits.Save();
                                                    break;
                                            }
                                           
                                            OpenStacksUI(uiUser.Player);
                                        }, $"{uiUser.Player.UserIDString}.{t.shortname}.stacklimit");

                                });

                        });

                    TextContainer.Create(item, Anchor.TopStretch, new Offset(74f, -60f, 0f, -40f))
                        .WithSize(12)
                        .WithText(GetString("Label.StackMultiplier", uiUser.Player))
                        .WithAlignment(TextAnchor.MiddleLeft)
                        .WithChildren(stackMultiplier =>
                        {
                            ImageContainer.Create(stackMultiplier, Anchor.FullStretch, new Offset(100f, 1f, -5f, -1f))
                                .WithStyle(m_ButtonStyle)
                                .WithChildren(input =>
                                {
                                    InputFieldContainer.Create(input, Anchor.FullStretch, Offset.zero)
                                        .WithSize(12)
                                        .WithText(stackLimit.StackMultiplier.ToString("n2"))
                                        .WithAlignment(TextAnchor.MiddleCenter)
                                        .WithCallback(m_CallbackHandler, arg =>
                                        {
                                            float stackMultiplier = arg.GetFloat(1, stackLimit.StackMultiplier);
                                            if (stackMultiplier == stackLimit.StackMultiplier)
                                                return;
                                            
                                            stackLimit.StackMultiplier = stackMultiplier;
                                            
                                            switch (uiUser.Category)
                                            {
                                                case UICategory.Item:
                                                    stackLimit.ItemDefinition.stackable = stackLimit.GetStackSize();
                                                    m_StackLimits.Save();
                                                    break;
                                                case UICategory.Storage:
                                                    m_StorageLimits.Save();
                                                    break;
                                                case UICategory.PlayerOverrides:
                                                    m_PlayerLimits.Save();
                                                    break;
                                                case UICategory.VIPStorage:
                                                    m_VIPLimits.Save();
                                                    break;
                                            }
                                            
                                            OpenStacksUI(uiUser.Player);
                                        }, $"{uiUser.Player.UserIDString}.{t.shortname}.stackmultiplier");
                                });

                        });

                    TextContainer.Create(item, Anchor.TopStretch, new Offset(74f, -80f, 0, -60f))
                        .WithSize(12)
                        .WithText(FormatString("Label.DefaultStackSize", uiUser.Player, m_DefaultItemStackSizes[t.shortname]))
                        .WithAlignment(TextAnchor.MiddleLeft);

                    if (uiUser.ContainerItemOverride != null || uiUser.Category == UICategory.PlayerOverrides)
                    {
                        ImageContainer.Create(item, Anchor.TopLeft, new Offset(5f, -25f, 25f, -5f))
                            .WithStyle(m_ButtonStyle)
                            .WithOutline(m_OutlineRed)
                            .WithChildren(remove =>
                            {
                                TextContainer.Create(remove, Anchor.FullStretch, Offset.zero)
                                    .WithSize(18)
                                    .WithText("<b>×</b>")
                                    .WithAlignment(TextAnchor.MiddleCenter)
                                    .WithWrapMode(VerticalWrapMode.Overflow);

                                ButtonContainer.Create(remove, Anchor.FullStretch, Offset.zero)
                                    .WithColor(Color.Clear)
                                    .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        if (uiUser.Category == UICategory.PlayerOverrides)
                                            m_PlayerLimits.Data.Remove(t.shortname);
                                        else uiUser.ContainerItemOverride.ItemOverrides.Remove(t.shortname);

                                        switch (uiUser.Category)
                                        {
                                            case UICategory.Item:
                                                stackLimit.ItemDefinition.stackable = stackLimit.GetStackSize();
                                                m_StackLimits.Save();
                                                break;
                                            case UICategory.Storage:
                                                m_StorageLimits.Save();
                                                break;
                                            case UICategory.PlayerOverrides:
                                                m_PlayerLimits.Save();
                                                break;
                                            case UICategory.VIPStorage:
                                                m_VIPLimits.Save();
                                                break;
                                        }

                                        OpenStacksUI(uiUser.Player);
                                    }, $"{uiUser.Player.UserIDString}.remove.{t.shortname}");

                            });
                    }
                });
        }

        private void CreateStorageEntry(UIUser uiUser, string t, BaseContainer layout, Anchor anchor, Offset offset)
        {
            StorageLimit storageLimit = uiUser.Category == UICategory.VIPStorage ? m_VIPLimits.Data[uiUser.Permission].StorageOverrides[t] : m_StorageLimits.Data[t];

            ImageContainer.Create(layout, anchor, offset)
                .WithStyle(m_PanelStyle)
                .WithChildren(item =>
                {
                    int itemId;

                    if (m_PrefabNameToItemID.TryGetValue(t, out itemId))
                        ImageContainer.Create(item, Anchor.CenterLeft, new Offset(5.5f, -32f, 69.5f, 32f))
                            .WithIcon(itemId);
                    else if (m_PrefabIconUrls.ContainsKey(t))
                        RawImageContainer.Create(item, Anchor.CenterLeft, new Offset(5.5f, -32f, 69.5f, 32f))
                            .WithPNG(GetImage(t));

                    TextContainer.Create(item, Anchor.TopStretch, new Offset(74f, -20f, 0f, 0f))
                        .WithSize(12)
                        .WithText(storageLimit.NiceName)
                        .WithAlignment(TextAnchor.MiddleLeft);

                    TextContainer.Create(item, Anchor.TopStretch, new Offset(74f, -40f, 0f, -20f))
                        .WithSize(12)
                        .WithText(GetString("Label.MaxStackSize", uiUser.Player))
                        .WithAlignment(TextAnchor.MiddleLeft)
                        .WithChildren(stackSize =>
                        {
                            ImageContainer.Create(stackSize, Anchor.FullStretch, new Offset(100f, 1f, -5f, -1f))
                                .WithStyle(m_ButtonStyle)
                                .WithChildren(input =>
                                {
                                    InputFieldContainer.Create(input, Anchor.FullStretch, Offset.zero)
                                        .WithSize(12)
                                        .WithText(storageLimit.MaxStackSize.ToString())
                                        .WithAlignment(TextAnchor.MiddleCenter)
                                        .WithCallback(m_CallbackHandler, arg =>
                                        {
                                            int maxStackSize = arg.GetInt(1);
                                            if (maxStackSize == storageLimit.MaxStackSize)
                                                return;
                                            
                                            storageLimit.MaxStackSize = maxStackSize;

                                            if (uiUser.Category == UICategory.VIPStorage)
                                                m_VIPLimits.Save();
                                            else m_StorageLimits.Save();

                                            OpenStacksUI(uiUser.Player);
                                        }, $"{uiUser.Player.UserIDString}.maxstack.{t}");
                                });
                        });

                    TextContainer.Create(item, Anchor.TopStretch, new Offset(74f, -60f, 0f, -40f))
                        .WithSize(12)
                        .WithText(GetString("Label.StackMultiplier", uiUser.Player))
                        .WithAlignment(TextAnchor.MiddleLeft)
                        .WithChildren(stackMultiplier =>
                        {
                            ImageContainer.Create(stackMultiplier, Anchor.FullStretch, new Offset(100f, 1f, -5f, -1f))
                                .WithStyle(m_ButtonStyle)
                                .WithChildren(input =>
                                {
                                    InputFieldContainer.Create(input, Anchor.FullStretch, Offset.zero)
                                        .WithSize(12)
                                        .WithText(storageLimit.StackMultiplier.ToString("n2"))
                                        .WithAlignment(TextAnchor.MiddleCenter)
                                        .WithCallback(m_CallbackHandler, arg =>
                                        {
                                            float stackMultiplier = arg.GetFloat(1, storageLimit.StackMultiplier);
                                            if (stackMultiplier == storageLimit.StackMultiplier)
                                                return;
                                            
                                            storageLimit.StackMultiplier = stackMultiplier;

                                            m_StorageLimits.Save();

                                            OpenStacksUI(uiUser.Player);
                                        }, $"{uiUser.Player.UserIDString}.stackmultiplier.{t}");
                                });
                        });

                    ImageContainer.Create(item, Anchor.BottomStretch, new Offset(74f, 3f, -5f, 19f))
                        .WithStyle(m_ButtonStyle)
                        .WithChildren(itemOverrides =>
                        {
                            TextContainer.Create(itemOverrides, Anchor.FullStretch, Offset.zero)
                                .WithSize(12)
                                .WithText(FormatString("Button.ItemOverrides", uiUser.Player, storageLimit.ItemOverrides.Count))
                                .WithAlignment(TextAnchor.MiddleCenter);

                            ButtonContainer.Create(itemOverrides, Anchor.FullStretch, Offset.zero)
                                .WithColor(Color.Clear)
                                .WithCallback(m_CallbackHandler, arg =>
                                {
                                    uiUser.ContainerItemOverride = storageLimit;
                                    OpenStacksUI(uiUser.Player);
                                }, $"{uiUser.Player.UserIDString}.itemoverride.{t}");

                        });

                    if (uiUser.Category == UICategory.VIPStorage)
                    {
                        ImageContainer.Create(item, Anchor.TopLeft, new Offset(5f, -25f, 25f, -5f))
                            .WithStyle(m_ButtonStyle)
                            .WithOutline(m_OutlineRed)
                            .WithChildren(remove =>
                            {
                                TextContainer.Create(remove, Anchor.FullStretch, Offset.zero)
                                    .WithSize(18)
                                    .WithText("<b>×</b>")
                                    .WithAlignment(TextAnchor.MiddleCenter)
                                    .WithWrapMode(VerticalWrapMode.Overflow);

                                ButtonContainer.Create(remove, Anchor.FullStretch, Offset.zero)
                                    .WithColor(Color.Clear)
                                    .WithCallback(m_CallbackHandler, arg =>
                                    {
                                        m_VIPLimits.Data[uiUser.Permission].StorageOverrides.Remove(t);
                                        m_VIPLimits.Save();
                                        OpenStacksUI(uiUser.Player);
                                    }, $"{uiUser.Player.UserIDString}.remove.{t}");

                            });
                    }
                });
        }

        #endregion

        #region Override Selectors

        private HorizontalLayoutGroup m_ItemCategoryLayout = new HorizontalLayoutGroup()
        {
            Area = new Area(-635f, -12.5f, 635f, 12.5f),
            Spacing = new Spacing(5f, 5f),
            Padding = new Padding(0f, 0f, 0f, 0f),
            Corner = Corner.Centered,
            FixedSize = new Vector2(85, 20),
            FixedCount = new Vector2Int(14, 0),
        };

        private GridLayoutGroup m_ItemOverrideLayout = new GridLayoutGroup(12, 6, Axis.Horizontal)
        {
            Area = new Area(-635f, -322.5f, 635f, 322.5f),
            Spacing = new Spacing(5f, 5f),
            Padding = new Padding(5f, 5f, 5f, 5f),
            Corner = Corner.TopLeft,
            FixedCount = new Vector2Int(12, 6),
        };

        private GridLayoutGroup m_StorageOverrideLayout = new GridLayoutGroup(12, 6, Axis.Horizontal)
        {
            Area = new Area(-635f, -337.5f, 635f, 337.5f),
            Spacing = new Spacing(5f, 5f),
            Padding = new Padding(5f, 5f, 5f, 5f),
            Corner = Corner.TopLeft,
            FixedCount = new Vector2Int(12, 6),
        };
        
        private void CreateItemOverrideSelector(UIUser uiUser, Action<ItemDefinition> onSelectItem, ItemCategory itemCategory = ItemCategory.Weapon, int page = 0, string search = "")
        {
            List<ItemDefinition> dst = Facepunch.Pool.GetList<ItemDefinition>();

            if (!string.IsNullOrEmpty(search))
            {
                List<ItemDefinition> src = Facepunch.Pool.GetList<ItemDefinition>();

                src.AddRange(ItemManager.itemList);
			        
                FilterList(src, dst, uiUser, 
                    (s, itemDefinition) => StartsWithValidator(s, itemDefinition.displayName.english), 
                    (s, itemDefinition) => ContainsValidator(s, itemDefinition.displayName.english));
			        
                Facepunch.Pool.FreeList(ref src);
            }
            else dst.AddRange(m_ItemDefinitionsPerCategory[itemCategory]);
                
            BaseContainer root = ImageContainer.Create(STACKS_UI, Layer.Overall, Anchor.FullStretch, Offset.zero)
                .WithStyle(m_BackgroundStyle)
                .NeedsCursor()
                .NeedsKeyboard()
                .DestroyExisting()
                .WithChildren(addItemOverride =>
                {
                    ImageContainer.Create(addItemOverride, Anchor.TopStretch, new Offset(5f, -35f, -5f, -5f))
                        .WithStyle(m_PanelStyle)
                        .WithChildren(header =>
                        {
                            TextContainer.Create(header, Anchor.FullStretch, Offset.zero)
                                .WithText(GetString("Label.AddItemOverride", uiUser.Player))
                                .WithAlignment(TextAnchor.MiddleCenter);

                            ImageContainer.Create(header, Anchor.CenterLeft, new Offset(40f, -10f, 100f, 10f))
                                .WithStyle(m_ButtonStyle)
                                .WithChildren(backButton =>
                                {
                                    TextContainer.Create(backButton, Anchor.FullStretch, Offset.zero)
                                        .WithText(GetString("Button.Cancel", uiUser.Player))
                                        .WithAlignment(TextAnchor.MiddleCenter);

                                    ButtonContainer.Create(backButton, Anchor.FullStretch, Offset.zero)
                                        .WithColor(Color.Clear)
                                        .WithCallback(m_CallbackHandler, arg =>
                                        {
                                            OpenStacksUI(uiUser.Player);
                                        }, $"{uiUser.Player.UserIDString}.cancel");
                                })
                                .WithOutline(m_OutlineRed);
                            
                            ImageContainer.Create(header, Anchor.CenterLeft, new Offset(5f, -10f, 35f, 10f))
                                .WithStyle(page > 0 ? m_ButtonStyle : m_DisabledButtonStyle)
                                .WithChildren(backButton =>
                                {
                                    TextContainer.Create(backButton, Anchor.FullStretch, Offset.zero)
                                        .WithText("<<<")
                                        .WithAlignment(TextAnchor.MiddleCenter);

                                    if (page > 0)
                                    {
                                        ButtonContainer.Create(backButton, Anchor.FullStretch, Offset.zero)
                                            .WithColor(Color.Clear)
                                            .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                CreateItemOverrideSelector(uiUser, onSelectItem, itemCategory, page - 1, search);
                                            }, $"{uiUser.Player.UserIDString}.back");
                                    }
                                });

                            ImageContainer.Create(header, Anchor.CenterRight, new Offset(-35f, -10f, -5f, 10f))
                                .WithStyle(m_ItemOverrideLayout.HasNextPage(page, dst.Count) ? m_ButtonStyle : m_DisabledButtonStyle)
                                .WithChildren(nextButton =>
                                {
                                    TextContainer.Create(nextButton, Anchor.FullStretch, Offset.zero)
                                        .WithText(">>>")
                                        .WithAlignment(TextAnchor.MiddleCenter);

                                    if (m_ItemOverrideLayout.HasNextPage(page, dst.Count))
                                    {
                                        ButtonContainer.Create(nextButton, Anchor.FullStretch, Offset.zero)
                                            .WithColor(Color.Clear)
                                            .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                CreateItemOverrideSelector(uiUser, onSelectItem, itemCategory, page + 1, search);
                                            }, $"{uiUser.Player.UserIDString}.next");
                                    }

                                });
                            
                            ImageContainer.Create(header, Anchor.CenterRight, new Offset(-240f, -10f, -40f, 10f))
                                .WithStyle(m_ButtonStyle)
                                .WithChildren(searchInput =>
                                {
                                    InputFieldContainer.Create(searchInput, Anchor.FullStretch, new Offset(5f, 0f, -5f, 0f))
                                        .WithText(search)
                                        .WithAlignment(TextAnchor.MiddleLeft)
                                        .WithCallback(m_CallbackHandler, arg =>
                                        {
                                            CreateItemOverrideSelector(uiUser, onSelectItem, itemCategory, page, arg.Args.Length > 1 ? string.Join(" ", arg.Args.Skip(1)) : string.Empty);
                                        }, $"{uiUser.Player.UserIDString}.searchinput");
                                });

                            if (!string.IsNullOrEmpty(m_MagnifyImage))
                            {
                                RawImageContainer.Create(header, Anchor.CenterRight, new Offset(-265f, -10f, -245f, 10f))
                                    .WithPNG(m_MagnifyImage);
                            }
                        });

                    ImageContainer.Create(addItemOverride, Anchor.TopStretch, new Offset(5f, -65f, -5f, -40f))
                        .WithStyle(m_PanelStyle)
                        .WithLayoutGroup(m_ItemCategoryLayout, m_ItemCategoryTypes, 0, (int i, ItemCategory t, BaseContainer subMenu, Anchor anchor, Offset offset) =>
                        {
                            BaseContainer button = ImageContainer.Create(subMenu, anchor, offset)
                                .WithStyle(m_ButtonStyle)
                                .WithChildren(commands =>
                                {
                                    TextContainer.Create(commands, Anchor.FullStretch, Offset.zero)
                                        .WithSize(13)
                                        .WithText(t.ToString())
                                        .WithAlignment(TextAnchor.MiddleCenter);

                                    ButtonContainer.Create(commands, Anchor.FullStretch, Offset.zero)
                                        .WithColor(Color.Clear)
                                        .WithCallback(m_CallbackHandler, arg =>
                                        {
                                            CreateItemOverrideSelector(uiUser, onSelectItem, t, page, search);
                                        }, $"{uiUser.Player.UserIDString}.category.{i}");

                                });

                            if (t == itemCategory)
                                button.WithOutline(m_OutlineGreen);
                        });

                    ImageContainer.Create(addItemOverride, Anchor.FullStretch, new Offset(5f, 5f, -5f, -70f))
                        .WithStyle(m_PanelStyle)
                        .WithLayoutGroup(m_ItemOverrideLayout, dst, page, (int i, ItemDefinition t, BaseContainer layout, Anchor anchor, Offset offset) =>
                        {
                            ImageContainer.Create(layout, anchor, offset)
                                .WithStyle(m_PanelStyle)
                                .WithChildren(template =>
                                {
                                    ImageContainer.Create(template, Anchor.TopCenter, new Offset(-32f, -69f, 32f, -5f))
                                        .WithIcon(t.itemid);

                                    TextContainer.Create(template, Anchor.BottomStretch, new Offset(5f, 5f, -5f, 31f))
                                        .WithSize(10)
                                        .WithText(t.displayName.english)
                                        .WithAlignment(TextAnchor.MiddleCenter);

                                    ButtonContainer.Create(template, Anchor.FullStretch, Offset.zero)
                                        .WithColor(Color.Clear)
                                        .WithCallback(m_CallbackHandler, arg =>
                                        {
                                            onSelectItem(t);
                                            OpenStacksUI(uiUser.Player);
                                        }, $"{uiUser.Player.UserIDString}.itemoverride.{i}");
                                });
                        });
                });
            
            Facepunch.Pool.FreeList(ref dst);
            
            ChaosUI.Show(uiUser.Player, root);
        }

        private void CreateStorageOverrideSelector(UIUser uiUser, Action<string> onSelectAction, int page = 0, string search = "")
        {
            List<string> dst = Facepunch.Pool.GetList<string>();

            if (!string.IsNullOrEmpty(search))
            {
                List<string> src = Facepunch.Pool.GetList<string>();

                src.AddRange(m_StorageLimits.Data.Keys);
			        
                FilterList(src, dst, uiUser, 
                    (s, prefab) => StartsWithValidator(s, m_StorageLimits.Data[prefab].NiceName), 
                    (s, prefab) => ContainsValidator(s, m_StorageLimits.Data[prefab].NiceName));
			        
                Facepunch.Pool.FreeList(ref src);
            }
            else dst.AddRange(m_StorageLimits.Data.Keys);
                
            BaseContainer root = ImageContainer.Create(STACKS_UI, Layer.Overall, Anchor.FullStretch, Offset.zero)
                .WithStyle(m_BackgroundStyle)
                .NeedsCursor()
                .NeedsKeyboard()
                .DestroyExisting()
                .WithChildren(addStorageOverride =>
                {
                    ImageContainer.Create(addStorageOverride, Anchor.TopStretch, new Offset(5f, -35f, -5f, -5f))
                        .WithStyle(m_PanelStyle)
                        .WithChildren(header =>
                        {
                            TextContainer.Create(header, Anchor.FullStretch, Offset.zero)
                                .WithText(GetString("Label.AddStorageOverride", uiUser.Player))
                                .WithAlignment(TextAnchor.MiddleCenter);

                            ImageContainer.Create(header, Anchor.CenterLeft, new Offset(40f, -10f, 100f, 10f))
                                .WithStyle(m_ButtonStyle)
                                .WithChildren(backButton =>
                                {
                                    TextContainer.Create(backButton, Anchor.FullStretch, Offset.zero)
                                        .WithText(GetString("Button.Cancel", uiUser.Player))
                                        .WithAlignment(TextAnchor.MiddleCenter);

                                    ButtonContainer.Create(backButton, Anchor.FullStretch, Offset.zero)
                                            .WithColor(Color.Clear)
                                            .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                OpenStacksUI(uiUser.Player);
                                            }, $"{uiUser.Player.UserIDString}.cancel");
                                })
                                .WithOutline(m_OutlineRed);
                            
                            ImageContainer.Create(header, Anchor.CenterLeft, new Offset(5f, -10f, 35f, 10f))
                                .WithStyle(page > 0 ? m_ButtonStyle : m_DisabledButtonStyle)
                                .WithChildren(backButton =>
                                {
                                    TextContainer.Create(backButton, Anchor.FullStretch, Offset.zero)
                                        .WithText("<<<")
                                        .WithAlignment(TextAnchor.MiddleCenter);

                                    if (page > 0)
                                    {
                                        ButtonContainer.Create(backButton, Anchor.FullStretch, Offset.zero)
                                            .WithColor(Color.Clear)
                                            .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                CreateStorageOverrideSelector(uiUser, onSelectAction, page - 1, search);
                                            }, $"{uiUser.Player.UserIDString}.back");
                                    }
                                });

                            ImageContainer.Create(header, Anchor.CenterRight, new Offset(-35f, -10f, -5f, 10f))
                                .WithStyle(m_ItemOverrideLayout.HasNextPage(page, dst.Count) ? m_ButtonStyle : m_DisabledButtonStyle)
                                .WithChildren(nextButton =>
                                {
                                    TextContainer.Create(nextButton, Anchor.FullStretch, Offset.zero)
                                        .WithText(">>>")
                                        .WithAlignment(TextAnchor.MiddleCenter);

                                    if (m_ItemOverrideLayout.HasNextPage(page, dst.Count))
                                    {
                                        ButtonContainer.Create(nextButton, Anchor.FullStretch, Offset.zero)
                                            .WithColor(Color.Clear)
                                            .WithCallback(m_CallbackHandler, arg =>
                                            {
                                                CreateStorageOverrideSelector(uiUser, onSelectAction, page + 1, search);
                                            }, $"{uiUser.Player.UserIDString}.next");
                                    }

                                });
                            
                            ImageContainer.Create(header, Anchor.CenterRight, new Offset(-240f, -10f, -40f, 10f))
                                .WithStyle(m_ButtonStyle)
                                .WithChildren(searchInput =>
                                {
                                    InputFieldContainer.Create(searchInput, Anchor.FullStretch, new Offset(5f, 0f, -5f, 0f))
                                        .WithText(search)
                                        .WithAlignment(TextAnchor.MiddleLeft)
                                        .WithCallback(m_CallbackHandler, arg =>
                                        {
                                            CreateStorageOverrideSelector(uiUser, onSelectAction, page, arg.Args.Length > 1 ? string.Join(" ", arg.Args.Skip(1)) : string.Empty);
                                        }, $"{uiUser.Player.UserIDString}.searchinput");
                                });

                            if (!string.IsNullOrEmpty(m_MagnifyImage))
                            {
                                RawImageContainer.Create(header, Anchor.CenterRight, new Offset(-265f, -10f, -245f, 10f))
                                    .WithPNG(m_MagnifyImage);
                            }
                        });

                    ImageContainer.Create(addStorageOverride, Anchor.FullStretch, new Offset(5f, 5f, -5f, -40f))
                        .WithStyle(m_PanelStyle)
                        .WithLayoutGroup(m_StorageOverrideLayout, dst, page, (int i, string t, BaseContainer layout, Anchor anchor, Offset offset) =>
                        {
                            ImageContainer.Create(layout, anchor, offset)
                                .WithStyle(m_PanelStyle)
                                .WithChildren(template =>
                                {
                                    int itemId;
                                    if (m_PrefabNameToItemID.TryGetValue(t, out itemId))
                                        ImageContainer.Create(template, Anchor.TopCenter, new Offset(-32f, -69f, 32f, -5f))
                                            .WithIcon(itemId);
                                    else if (m_PrefabIconUrls.ContainsKey(t))
                                        RawImageContainer.Create(template, Anchor.TopCenter, new Offset(-32f, -69f, 32f, -5f))
                                            .WithPNG(GetImage(t));
                                    
                                    TextContainer.Create(template, Anchor.BottomStretch, new Offset(5f, 5f, -5f, 31f))
                                        .WithSize(10)
                                        .WithText(m_StorageLimits.Data[t].NiceName)
                                        .WithAlignment(TextAnchor.MiddleCenter);

                                    ButtonContainer.Create(template, Anchor.FullStretch, Offset.zero)
                                        .WithColor(Color.Clear)
                                        .WithCallback(m_CallbackHandler, arg =>
                                        {
                                            onSelectAction(t);
                                        }, $"{uiUser.Player.UserIDString}.container.{i}");
                                });
                        });
                });
            
            Facepunch.Pool.FreeList(ref dst);
            
            ChaosUI.Show(uiUser.Player, root);
        }

        private void CreateCustomPermissionCreator(UIUser uiUser, string inputText = "")
        {
            BaseContainer root = ImageContainer.Create(STACKS_UI, Layer.Overall, Anchor.FullStretch, Offset.zero)
                .WithStyle(m_BackgroundStyle)
                .NeedsCursor()
                .NeedsKeyboard()
                .DestroyExisting()
                .WithChildren(createPermissionPopup =>
                {
                    ImageContainer.Create(createPermissionPopup, Anchor.Center, new Offset(-175f, 32.5f, 175f, 52.5f))
                        .WithStyle(m_PanelStyle)
                        .WithChildren(header =>
                        {
                            TextContainer.Create(header, Anchor.FullStretch, Offset.zero)
                                .WithText(GetString("Label.CreateVIPPermission", uiUser.Player))
                                .WithAlignment(TextAnchor.MiddleCenter);
                        });

                    ImageContainer.Create(createPermissionPopup, Anchor.Center, new Offset(-175f, -27.5f, 175f, 27.5f))
                        .WithStyle(m_PanelStyle)
                        .WithChildren(titleBar =>
                        {
                            ImageContainer.Create(titleBar, Anchor.BottomLeft, new Offset(5f, 5f, 95f, 25f))
                                .WithStyle(m_ButtonStyle)
                                .WithOutline(m_OutlineGreen)
                                .WithChildren(confirm =>
                                {
                                    TextContainer.Create(confirm, Anchor.FullStretch, Offset.zero)
                                        .WithText(GetString("Button.Create", uiUser.Player))
                                        .WithAlignment(TextAnchor.MiddleCenter);

                                    ButtonContainer.Create(confirm, Anchor.FullStretch, Offset.zero)
                                        .WithColor(Color.Clear)
                                        .WithCallback(m_CallbackHandler, arg =>
                                        {
                                            if (string.IsNullOrEmpty(inputText))
                                            {
                                                CreatePopupMessage(uiUser, GetString("Error.EnterPermission", uiUser.Player));
                                                return;
                                            }

                                            if (!inputText.StartsWith("stacksextended."))
                                                inputText = $"stacksextended.{inputText}";
                                            
                                            if (m_VIPLimits.Data.ContainsKey(inputText))
                                            {
                                                CreatePopupMessage(uiUser, GetString("Error.PermissionExists", uiUser.Player));
                                                return;
                                            }

                                            m_VIPLimits.Data[inputText] = new VIPLimits();
                                            m_VIPLimits.Save();

                                            permission.RegisterPermission(inputText, this);
                                            
                                            ChaosUI.Destroy(uiUser.Player, POPUP_UI);
                                            OpenStacksUI(uiUser.Player);
                                        }, $"{uiUser.Player.UserIDString}.createpermission");

                                });

                            ImageContainer.Create(titleBar, Anchor.BottomRight, new Offset(-95f, 5f, -5f, 25f))
                                .WithStyle(m_ButtonStyle)
                                .WithOutline(m_OutlineRed)
                                .WithChildren(cancel =>
                                {
                                    TextContainer.Create(cancel, Anchor.FullStretch, Offset.zero)
                                        .WithText(GetString("Button.Cancel", uiUser.Player))
                                        .WithAlignment(TextAnchor.MiddleCenter);

                                    ButtonContainer.Create(cancel, Anchor.FullStretch, Offset.zero)
                                        .WithColor(Color.Clear)
                                        .WithCallback(m_CallbackHandler, arg => OpenStacksUI(uiUser.Player), $"{uiUser.Player.UserIDString}.cancelpermission");
                                });

                            TextContainer.Create(titleBar, Anchor.BottomStretch, new Offset(5f, 30f, -145f, 50f))
                                .WithText("stacksextended.")
                                .WithAlignment(TextAnchor.MiddleLeft);
                            
                            ImageContainer.Create(titleBar, Anchor.BottomStretch, new Offset(97f, 30f, -5f, 50f))
                                .WithStyle(m_ButtonStyle)
                                .WithChildren(input =>
                                {
                                    InputFieldContainer.Create(input, Anchor.FullStretch, new Offset(5f, 0f, -5f, 0f))
                                        .WithText(inputText)
                                        .WithAlignment(TextAnchor.MiddleLeft)
                                        .WithCallback(m_CallbackHandler, arg =>
                                        {
                                            string inputStr = arg.Args.Length > 1 ? string.Join(" ", arg.Args.Skip(1)).Replace(" ", "") : string.Empty;
                                            
                                            CreateCustomPermissionCreator(uiUser, inputStr);
                                        }, $"{uiUser.Player.UserIDString}.inputpermission");
                                });
                        });

                });


            ChaosUI.Show(uiUser.Player, root);
        }

        #endregion
        
        #region Popup Message

        private Hash<ulong, Timer> m_PopupTimers = new Hash<ulong, Timer>();

        private void CreatePopupMessage(UIUser uiUser, string message)
        {
            BaseContainer baseContainer = ImageContainer.Create(POPUP_UI, Layer.Overall, Anchor.Center, new Offset(-540f, -345f, 540f, -315f))
                .WithColor(Color.Clear)
                .WithChildren(popup =>
                {
                    ImageContainer.Create(popup, Anchor.FullStretch, new Offset(5f, 5f, -5f, -5f))
                        .WithStyle(m_PanelStyle)
                        .WithChildren(titleBar =>
                        {
                            TextContainer.Create(titleBar, Anchor.FullStretch, Offset.zero)
                                .WithText(message)
                                .WithAlignment(TextAnchor.MiddleCenter);

                        });
                })
                .DestroyExisting();
			
            ChaosUI.Show(uiUser.Player, baseContainer);

            Timer t;
            if (m_PopupTimers.TryGetValue(uiUser.Player.userID, out t))
                t?.Destroy();

            m_PopupTimers[uiUser.Player.userID] = timer.Once(5f, () => ChaosUI.Destroy(uiUser.Player, POPUP_UI));
        }
        #endregion
        #endregion

        #region Chat Commands

        [ChatCommand("stacks")]
        private void CmdStacks(BasePlayer player, string command, string[] args)
        {
            if (!player.HasPermission(ADMIN_PERMISSION))
            {
                player.LocalizedMessage(this, "Error.NoPermission");
                return;
            }

            OpenStacksUI(player);
        }
        #endregion
        
        #region Console Commands
        [ConsoleCommand("se.stackcategory")]
        private void ccmdStackCategory(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                SendReply(arg, "This command can only be used in rcon");
                return;
            }
            
            if (arg.Args == null || arg.Args.Length != 3)
            {
                SendReply(arg, "Invalid syntax. se.stackcategory <category> <stacklimit> <stackmultiplier>\nex. se.stackcategory Weapon 10 1.0");
                return;
            }

            int stackAmount;
            float stackMultiplier;

            if (!int.TryParse(arg.GetString(1), out stackAmount) || !float.TryParse(arg.GetString(2), out stackMultiplier))
            {
                SendReply(arg, "Invalid stack amount or stack multiplier entered");
                return;
            }

            ItemCategory itemCategory = ItemCategory.Weapon;

            bool foundCategory = false;
            foreach (ItemCategory itemCategoryType in m_ItemCategoryTypes)
            {
                if (itemCategoryType.ToString().Equals(arg.GetString(0), StringComparison.OrdinalIgnoreCase))
                {
                    itemCategory = itemCategoryType;
                    foundCategory = true;
                    break;
                }
            }

            if (!foundCategory)
            {
                SendReply(arg, $"Invalid category entered. Available categories are {m_ItemCategoryTypes.ToSentence()}");
                return;
            }

            foreach (ItemDefinition itemDefinition in m_ItemDefinitionsPerCategory[itemCategory])
            {
                StackLimit stackLimit;
                if (m_StackLimits.Data.TryGetValue(itemDefinition.shortname, out stackLimit))
                {
                    stackLimit.MaxStackSize = stackAmount;
                    stackLimit.StackMultiplier = stackMultiplier;

                    itemDefinition.stackable = stackLimit.GetStackSize();
                }
            }

            m_StackLimits.Save();
            
            SendReply(arg, $"All items in the {itemCategory} category have now been set to a max stack size of {stackAmount} with a stack multiplier of {stackMultiplier}");
        }
        
        [ConsoleCommand("se.stackcategorylimit")]
        private void ccmdStackCategoryLimit(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                SendReply(arg, "This command can only be used in rcon");
                return;
            }
            
            if (arg.Args == null || arg.Args.Length != 2)
            {
                SendReply(arg, "Invalid syntax. se.stackcategorylimit <category> <stacklimit>\nex. se.stackcategory Weapon 10");
                return;
            }

            int stackAmount;

            if (!int.TryParse(arg.GetString(1), out stackAmount))
            {
                SendReply(arg, "Invalid stack amount entered");
                return;
            }

            ItemCategory itemCategory = ItemCategory.Weapon;

            bool foundCategory = false;
            foreach (ItemCategory itemCategoryType in m_ItemCategoryTypes)
            {
                if (itemCategoryType.ToString().Equals(arg.GetString(0), StringComparison.OrdinalIgnoreCase))
                {
                    itemCategory = itemCategoryType;
                    foundCategory = true;
                    break;
                }
            }

            if (!foundCategory)
            {
                SendReply(arg, $"Invalid category entered. Available categories are {m_ItemCategoryTypes.ToSentence()}");
                return;
            }

            foreach (ItemDefinition itemDefinition in m_ItemDefinitionsPerCategory[itemCategory])
            {
                StackLimit stackLimit;
                if (m_StackLimits.Data.TryGetValue(itemDefinition.shortname, out stackLimit))
                {
                    stackLimit.MaxStackSize = stackAmount;

                    itemDefinition.stackable = stackLimit.GetStackSize();
                }
            }

            m_StackLimits.Save();
            
            SendReply(arg, $"All items in the {itemCategory} category have now been set to a max stack size of {stackAmount}");
        }
        
        [ConsoleCommand("se.stackcategorymultiplier")]
        private void ccmdStackCategoryMultiplier(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                SendReply(arg, "This command can only be used in rcon");
                return;
            }
            
            if (arg.Args == null || arg.Args.Length != 2)
            {
                SendReply(arg, "Invalid syntax. se.stackcategory <category> <stackmultiplier>\nex. se.stackcategory Weapon 1.0");
                return;
            }

            float stackMultiplier;

            if (!float.TryParse(arg.GetString(1), out stackMultiplier))
            {
                SendReply(arg, "Invalid stack multiplier entered");
                return;
            }

            ItemCategory itemCategory = ItemCategory.Weapon;

            bool foundCategory = false;
            foreach (ItemCategory itemCategoryType in m_ItemCategoryTypes)
            {
                if (itemCategoryType.ToString().Equals(arg.GetString(0), StringComparison.OrdinalIgnoreCase))
                {
                    itemCategory = itemCategoryType;
                    foundCategory = true;
                    break;
                }
            }

            if (!foundCategory)
            {
                SendReply(arg, $"Invalid category entered. Available categories are {m_ItemCategoryTypes.ToSentence()}");
                return;
            }

            foreach (ItemDefinition itemDefinition in m_ItemDefinitionsPerCategory[itemCategory])
            {
                StackLimit stackLimit;
                if (m_StackLimits.Data.TryGetValue(itemDefinition.shortname, out stackLimit))
                {
                    stackLimit.StackMultiplier = stackMultiplier;

                    itemDefinition.stackable = stackLimit.GetStackSize();
                }
            }

            m_StackLimits.Save();
            
            SendReply(arg, $"All items in the {itemCategory} category have now been set to a stack multiplier of {stackMultiplier}");
        }

        [ConsoleCommand("se.stackitem")]
        private void ccmdStackItem(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                SendReply(arg, "This command can only be used in rcon");
                return;
            }
            
            if (arg.Args == null || arg.Args.Length != 3)
            {
                SendReply(arg, "Invalid syntax. se.stackitem <shortname> <stacklimit> <stackmultiplier>\nex. se.stackcategory wood 2000 1.0");
                return;
            }

            int stackAmount;
            float stackMultiplier;

            if (!int.TryParse(arg.GetString(1), out stackAmount) || !float.TryParse(arg.GetString(2), out stackMultiplier))
            {
                SendReply(arg, "Invalid stack amount or stack multiplier entered");
                return;
            }
            
            ItemDefinition itemDefinition = ItemManager.FindItemDefinition(arg.GetString(0).ToLower());
            if (itemDefinition == null)
            {
                SendReply(arg, $"Failed to find a Item Definition with the shortname {arg.GetString(0)}");
                return;
            }

            StackLimit stackLimit;
            if (m_StackLimits.Data.TryGetValue(itemDefinition.shortname, out stackLimit))
            {
                stackLimit.MaxStackSize = stackAmount;
                stackLimit.StackMultiplier = stackMultiplier;
                m_StackLimits.Save();

                itemDefinition.stackable = stackLimit.GetStackSize();
                
                SendReply(arg, $"You have set the item {itemDefinition.shortname} to a max stack size of {stackAmount} with a stack multiplier of {stackMultiplier}");
            }
            else SendReply(arg, "The chosen item definition is not covered with stack manipulation");
        }
        #endregion
        
        #region Config
        private ConfigData Configuration;
        
        private class ConfigData : BaseConfigData
        {
            [JsonProperty("Stack Options")]
            public StackOptions Options { get; set; }
            
            [JsonProperty("Player Inventory Options")]
            public PlayerOptions Player { get; set; }
            
            [JsonProperty("Exclude Options")]
            public ExcludeOptions Exclude { get; set; }
            
            public class StackOptions
            {
                [JsonProperty("Enable stacking of projectile weapons")]
                public bool EnableProjectileWeaponStacks { get; set; }
                
                [JsonProperty("Prevent weapon stacking in player belt container")]
                public bool BeltAntiToolWeaponStack { get; set; }

                [JsonProperty("Prevent stacking weapons that have attachments")]
                public bool BlockModdedWeaponStacks { get; set; }
                
                [JsonProperty("Prevent stacking projectile weapons with ammunition in the clip")]
                public bool BlockUnequalAmmoWeaponStacks { get; set; }
                
                [JsonProperty("Enable stacking of liquid containers")]
                public bool EnableLiquidContainerStacks { get; set; }
                
                [JsonProperty("Prevent stacking skinned items that have different skins")]
                public bool BlockDifferentSkinStacks { get; set; }
            }

            public class PlayerOptions
            {
                [JsonProperty("The maximum size of any stack in a players inventory (0 is Rust default)")]
                public int InventoryStackLimit { get; set; }
            }

            public class ExcludeOptions
            {
                [JsonProperty("Items to be excluded from stack changes")]
                public HashSet<string> ExcludedItems { get; set; }
                
                [JsonProperty("Skins to be excluded from stack changes")]
                public HashSet<ulong> ExcludedSkins { get; set; }

                public bool IsExcluded(Item item) => ExcludedSkins.Contains(item.skin) || ExcludedItems.Contains(item.info.shortname);

                public bool IsExcluded(string shortname) => ExcludedItems.Contains(shortname);
            }
        }     
        
        protected override void OnConfigurationUpdated(VersionNumber oldVersion)
        {
            ConfigData baseConfigData = GenerateDefaultConfiguration<ConfigData>();
            
            Configuration = ConfigurationData as ConfigData;

            if (oldVersion < new VersionNumber(2, 0, 0))
                Configuration = baseConfigData;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            
            Configuration = ConfigurationData as ConfigData;
        }

        protected override ConfigurationFile OnLoadConfig(ref ConfigurationFile configurationFile) => configurationFile = new ConfigurationFile<ConfigData>(Config);

        protected override T GenerateDefaultConfiguration<T>()
        {
            return new ConfigData
            {
                Options = new ConfigData.StackOptions
                {
                    BeltAntiToolWeaponStack = true,
                    BlockDifferentSkinStacks = true,
                    BlockModdedWeaponStacks = true,
                    EnableLiquidContainerStacks = false,
                    EnableProjectileWeaponStacks = false,
                    BlockUnequalAmmoWeaponStacks = true
                },
                Player = new ConfigData.PlayerOptions
                {
                    InventoryStackLimit = 0,
                },
                Exclude = new ConfigData.ExcludeOptions
                {
                    ExcludedItems = new HashSet<string>
                    {
                        "water",
                        "water.salt",
                        "blood",
                        "blueprintbase",
                        "coal",
                        "flare",
                        "generator.wind.scrap",
                        "battery.small",
                        "building.planner",
                        "door.key",
                        "map",
                        "note",
                        "hat.candle",
                        "hat.miner"
                    },
                    ExcludedSkins = new HashSet<ulong>()
                }
            } as T;
        }
        
        #region v1.x.x Config Restoration

        [ConsoleCommand("se.loadoldconfig")]
        private void ccmdLoadOldConfig(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                SendReply(arg, "This command can only be run via rcon");
                return;
            }
            
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile($"StacksExtended/{Name}"))
            {
                SendReply(arg, "Copy your old StacksExtended config file to your /oxide/data/StacksExtended folder to continue");
                return;
            }
            
            TryLoadv1Config(arg);
        }
        
        private void TryLoadv1Config(ConsoleSystem.Arg arg)
        {
            try
            {
                DynamicConfigFile configFile = Interface.Oxide.DataFileSystem.GetFile($"StacksExtended/{Name}");
                if (configFile.Exists(null))
                {
                    configFile.Load(null);
                    
                    Dictionary<string, object> containerStacks = (Dictionary<string, object>) GetConfigValue(configFile, "Storages", "Stack");
                    if (containerStacks != null && containerStacks.Count > 0)
                    {
                        int count = 0;
                        foreach (KeyValuePair<string, object> kvp in containerStacks)
                        {
                            string prefabName;
                            if (m_ShortPrefabNameToPrefabName.TryGetValue(kvp.Key, out prefabName))
                            {
                                StorageLimit storageLimit;
                                if (m_StorageLimits.Data.TryGetValue(prefabName, out storageLimit))
                                {
                                    storageLimit.MaxStackSize = (int)kvp.Value;
                                    count++;
                                }
                            }
                        }

                        SendReply(arg, $"Copied {count} container stack limits");
                        m_StorageLimits.Save();
                    }

                    Dictionary<string, object> containerVIP = (Dictionary<string, object>) GetConfigValue(configFile, "Storages", "VIP");
                    if (containerVIP != null && containerVIP.Count > 0)
                    {
                        int count = 0;
                        foreach (KeyValuePair<string, object> kvp in containerVIP)
                        {
                            Dictionary<string, object> fields = (Dictionary<string, object>) kvp.Value;
                            if (fields == null || !(bool) fields["Enabled"])
                                continue;

                            string prefabName;
                            if (m_ShortPrefabNameToPrefabName.TryGetValue(kvp.Key, out prefabName))
                            {
                                Dictionary<string, object> perms = (Dictionary<string, object>) fields["Permissions"];
                                foreach (KeyValuePair<string, object> permKvp in perms)
                                {
                                    if ((int)permKvp.Value == 0)
                                        continue;
                                    
                                    string perm = kvp.Key;
                                    if (!perm.StartsWith($"stacksextended.", StringComparison.OrdinalIgnoreCase))
                                        perm = $"stacksextended.{perm}";

                                    VIPLimits vipLimit;
                                    if (!m_VIPLimits.Data.TryGetValue(perm, out vipLimit))
                                        vipLimit = m_VIPLimits.Data[perm] = new VIPLimits();

                                    StorageLimit storageLimit;
                                    if (!vipLimit.StorageOverrides.TryGetValue(prefabName, out storageLimit))
                                        storageLimit = vipLimit.StorageOverrides[prefabName] = new StorageLimit {NiceName = PrefabNameToNiceName(prefabName)};

                                    storageLimit.MaxStackSize = (int)permKvp.Value;

                                    count++;
                                }
                            }
                        }

                        SendReply(arg, $"Copied {count} VIP container stack limits and associated permission");
                        m_VIPLimits.Save();
                    }

                    Dictionary<string, object> stackLimits = (Dictionary<string, object>)configFile.Get("StackLimits");
                    if (stackLimits != null && stackLimits.Count > 0)
                    {
                        foreach (var kvp in stackLimits)
                        {
                            StackLimit stackLimit;
                            if (m_StackLimits.Data.TryGetValue(kvp.Key, out stackLimit))
                            {
                                stackLimit.MaxStackSize = (int)kvp.Value;
                            }
                        }
                        
                        m_StackLimits.Save();
                        SendReply(arg, $"Copied {stackLimits.Count} item stack limits");
                    }
                    
                    Dictionary<string, object> containerItemOverrides = (Dictionary<string, object>)GetConfigValue(configFile, "StackOverrides", "Storages");
                    if (containerItemOverrides != null && containerItemOverrides.Count > 0)
                    {
                        int count = 0, itemCount = 0;
                        foreach (KeyValuePair<string, object> kvp in containerItemOverrides)
                        {
                            string prefabName;
                            if (m_ShortPrefabNameToPrefabName.TryGetValue(kvp.Key, out prefabName))
                            {
                                Dictionary<string, int> itemLimits = (Dictionary<string, int>)kvp.Value;
                                if (itemLimits != null && itemLimits.Count > 0)
                                {
                                    StorageLimit storageLimit;
                                    if (m_StorageLimits.Data.TryGetValue(prefabName, out storageLimit))
                                    {
                                        foreach (KeyValuePair<string, int> itemKvp in itemLimits)
                                        {
                                            storageLimit.ItemOverrides[itemKvp.Key] = new StackLimit(itemKvp.Value);
                                            itemCount++;
                                        }

                                        count++;
                                    }
                                }
                            }
                        }
                        
                        m_StorageLimits.Save();
                        SendReply(arg, $"Copied {count} storage containers with a total of {itemCount} item overrides");
                    }
                    
                    Dictionary<string, object> playerInventoryItemOverrides = (Dictionary<string, object>)GetConfigValue(configFile, "StackOverrides", "PlayerInventory");
                    if (playerInventoryItemOverrides != null && playerInventoryItemOverrides.Count > 0)
                    {
                        foreach (KeyValuePair<string, object> kvp in playerInventoryItemOverrides)
                        {
                            m_PlayerLimits.Data[kvp.Key] = new StackLimit((int)kvp.Value);
                        }
                        
                        m_PlayerLimits.Save();
                        SendReply(arg, $"Copied {playerInventoryItemOverrides.Count} player inventory item overrides");
                    }
                    
                    List<object> itemStackExcludes = (List<object>) GetConfigValue(configFile, "Settings", "ExcludedItems");
                    if (itemStackExcludes != null && itemStackExcludes.Count > 0)
                    {
                        foreach (string shortname in itemStackExcludes)
                            Configuration.Exclude.ExcludedItems.Add(shortname);

                        SendReply(arg, $"Copied {itemStackExcludes.Count} item excludes");
                    }

                    List<object> skinStackExcludes = (List<object>) GetConfigValue(configFile, "Settings", "ExcludedSkins");
                    if (skinStackExcludes != null && skinStackExcludes.Count > 0)
                    {
                        foreach (object skin in skinStackExcludes)
                            Configuration.Exclude.ExcludedSkins.Add(Convert.ToUInt64(skin));

                        SendReply(arg, $"Copied {skinStackExcludes.Count} skin excludes");
                    }

                    object playerInventoryStacklimit = GetConfigValue(configFile, "Settings", "playerInventoryStacklimit");
                    if (playerInventoryStacklimit is int)
                        Configuration.Player.InventoryStackLimit = (int) playerInventoryStacklimit;

                    object blockDifferentSkinStacks = GetConfigValue(configFile, "ExtraFeatures", "blockDifferentSkinStacks");
                    if (blockDifferentSkinStacks is bool)
                        Configuration.Options.BlockDifferentSkinStacks = (bool) blockDifferentSkinStacks;
                    
                    object beltAntiToolWeaponStack = GetConfigValue(configFile, "ExtraFeatures", "beltAntiToolWeaponStack");
                    if (beltAntiToolWeaponStack is bool)
                        Configuration.Options.BeltAntiToolWeaponStack = (bool) beltAntiToolWeaponStack;
                    
                    object blockModdedWeaponStacks = GetConfigValue(configFile, "ExtraFeatures", "blockModdedWeaponStacks");
                    if (blockModdedWeaponStacks is bool)
                        Configuration.Options.BlockModdedWeaponStacks = (bool) blockModdedWeaponStacks;
                    
                    object enableProjectileWeaponStacks = GetConfigValue(configFile, "ExtraFeatures", "enableProjectileWeaponStacks");
                    if (enableProjectileWeaponStacks is bool)
                        Configuration.Options.EnableProjectileWeaponStacks = (bool) enableProjectileWeaponStacks;
                    
                    object blockUnequalAmmoWeaponStacks = GetConfigValue(configFile, "ExtraFeatures", "blockUnequalAmmoWeaponStacks");
                    if (blockUnequalAmmoWeaponStacks is bool)
                        Configuration.Options.BlockUnequalAmmoWeaponStacks = (bool) blockUnequalAmmoWeaponStacks;
                    
                    object enableLiquidContainerStacks = GetConfigValue(configFile, "ExtraFeatures", "enableLiquidContainerStacks");
                    if (enableLiquidContainerStacks is bool)
                        Configuration.Options.EnableLiquidContainerStacks = (bool) enableLiquidContainerStacks;

                    SaveConfiguration();
                    
                    SendReply(arg, $"Configuration updated with additional config values");
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"{ex.Message}\n{ex.StackTrace}");
                SendReply(arg, "Failed to parse old configuration file");
            }
        }
        
        private object GetConfigValue(DynamicConfigFile configFile, string menu, string datavalue)
        {
            Dictionary<string, object> data = configFile[menu] as Dictionary<string, object>;
            if (data == null)
            {
                return null;
            }

            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                return null;
            }

            return value;
        }
        #endregion
        #endregion
        
        #region Data
        public class VIPLimits
        {
            public int Priority { get; set; }
            public OrderedHash<string, StorageLimit> StorageOverrides { get; set; } = new OrderedHash<string, StorageLimit>();
        }
        
        public class StorageLimit
        {
            public string NiceName { get; set; }

            public float StackMultiplier { get; set; } = 1f;
            
            public int MaxStackSize { get; set; }

            public OrderedHash<string, StackLimit> ItemOverrides { get; set; } = new OrderedHash<string, StackLimit>();

            public int GetMaxStackable(Item item)
            {
                StackLimit stackLimit;
                if (ItemOverrides.TryGetValue(item.info.shortname, out stackLimit))
                    return stackLimit.GetStackSize();

                if (!Mathf.Approximately(StackMultiplier, 1f))
                    return Mathf.CeilToInt((float)item.info.stackable * StackMultiplier);
                
                return MaxStackSize;
            }
        }

        public class StackLimit
        {
            public int MaxStackSize { get; set; }
            
            public float StackMultiplier { get; set; }
            
            [JsonIgnore]
            public ItemDefinition ItemDefinition { get; set; }
            
            public StackLimit(){}

            public StackLimit(int maxStackSize)
            {
                MaxStackSize = maxStackSize;
                StackMultiplier = 1f;
            }

            public int GetStackSize() => Mathf.RoundToInt(MaxStackSize * StackMultiplier);
        }

        public class VipLimitsDataFile : Datafile<Hash<string, VIPLimits>>
        {
            public VipLimitsDataFile(string name, params JsonConverter[] converters) : base(name, converters)
            {
            }

            public override void Save()
            {
                KeyValuePair<string, VIPLimits>[] ordered = Data.OrderByDescending(x => x.Value.Priority).ToArray();
                
                Data.Clear();

                for (var i = 0; i < ordered.Length; i++)
                {
                    KeyValuePair<string, VIPLimits> kvp = ordered[i];
                    Data[kvp.Key] = kvp.Value;
                }

                base.Save();
            }
        }
        #endregion
    }
}
