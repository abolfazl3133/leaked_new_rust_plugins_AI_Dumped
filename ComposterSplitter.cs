using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("ComposterSplitter", "Malmo", "1.0.1")]
    [Description("A plugin that splits the items in a composter into multiple stacks for best efficiency")]
    class ComposterSplitter : RustPlugin
    {

        #region Switches

        private bool _busy = false;

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            permission.RegisterPermission("compostersplitter.use", this);
        }

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (_busy) return;
            if (item == null || container == null) return;
            if (container.GetEntityOwner() is Composter)
            {
                if (item.info.GetComponent<ItemModCompostable>() == null) return;

                TrySplitComposterItems(null, null, container.GetEntityOwner() as Composter);
            }
        }

        private void OnItemStacked(Item destinationItem, Item sourceItem, ItemContainer container)
        {
            if (_busy) return;
            if (container == null) return;
            if (container.GetEntityOwner() is Composter)
            {
                if (sourceItem.info.GetComponent<ItemModCompostable>() == null) return;

                TrySplitComposterItems(null, null, container.GetEntityOwner() as Composter);
            }
        }

        private object OnComposterUpdate(Composter composter)
        {
            if (_busy) return null;
            if (composter == null) return null;

            if (CanFitMoreFertilizer(composter))
            {
                TrySplitComposterItems(null, null, composter);
                return null;
            }

            return false;
        }

        private object CanMoveItem(Item item, PlayerInventory inventory, ItemContainerId targetContainerId, int targetSlotIndex, int splitAmount)
        {
            if (item == null || inventory == null)
                return null;

            if (targetSlotIndex != -1)
                return null;

            BasePlayer player = inventory.GetComponent<BasePlayer>();
            if (player == null)
                return null;

            Composter composter = inventory.loot.entitySource as Composter;
            if (composter == null)
                return null;

            ItemContainer targetContainer = inventory.FindContainer(targetContainerId);
            if (targetContainer != null && !(targetContainer?.entityOwner is Composter))
                return null;

            ItemContainer container = composter.inventory;
            ItemContainer originalContainer = item.GetRootContainer();
            if (container == null || originalContainer == null || originalContainer?.entityOwner is Composter)
                return null;

            return TrySplitComposterItems(player, item, composter);
        }

        #endregion

        #region Helper Methods

        private bool CanFitMoreFertilizer(Composter composter)
        {
            if (composter == null) return false;

            var inventory = composter.inventory;
            if (inventory == null) return false;
            var slot = composter.inventorySlots;

            while (slot > 0)
            {
                var item = inventory.GetSlot(slot);

                if (item == null)
                {
                    return true;
                }

                if (composter.ItemIsFertilizer(item) && item.amount < item.MaxStackable())
                {
                    return true;
                }

                slot--;
            }

            return false;
        }

        #endregion

        #region Spliting Methods

        private object TrySplitComposterItems(BasePlayer player, Item item, Composter composter)
        {
            if (_busy) return null;

            var ownerId = player?.UserIDString;
            if (ownerId == null)
            {
                ownerId = composter.OwnerID.ToString();
            }

            if (ownerId == null)
            {
                return null;
            }

            if (!permission.UserHasPermission(ownerId, "compostersplitter.use"))
            {
                return null;
            }

            try
            {
                return SplitComposterItems(item, composter);
            }
            catch
            {
                return null;
            }
            finally
            {
                _busy = false;
            }
        }

        private object SplitComposterItems(Item addedItem, Composter composter)
        {
            if (_busy)
            {
                return null;
            }

            _busy = true;

            var inventory = composter.inventory;
            var inventorySlotSize = composter.inventorySlots;
            if (inventory == null) return null;

            if (addedItem != null && composter.ItemIsFertilizer(addedItem))
            {
                return null;
            }

            var itemList = new Dictionary<int, float>();
            if (inventory.itemList.Count == 0 && addedItem == null) return null;

            var itemWeights = new Dictionary<int, float>();
            var itemStackSizes = new Dictionary<int, int>();
            var itemAmounts = new Dictionary<int, int>();

            var removeItems = Pool.GetList<Item>();
            var fertilizerItems = 0;

            try
            {
                for (var i = 0; i < inventory.itemList.Count + (addedItem == null ? 0 : 1); i++)
                {
                    var item = inventory.itemList.Count == i ? addedItem : inventory.itemList[i];
                    var compostable = item.info.GetComponent<ItemModCompostable>();
                    if (compostable == null)
                    {
                        if (composter.ItemIsFertilizer(item))
                        {
                            fertilizerItems++;
                        }

                        continue;
                    }

                    if (inventory.itemList.Count > i) removeItems.Add(item);

                    var itemId = item.info.itemid;
                    var addedWeight = item.amount * compostable.TotalFertilizerProduced;

                    if (!itemList.ContainsKey(itemId))
                    {
                        itemList.Add(itemId, addedWeight);
                        itemWeights.Add(itemId, compostable.TotalFertilizerProduced);
                        itemStackSizes.Add(itemId, item.MaxStackable());
                        itemAmounts.Add(itemId, item.amount);
                    }
                    else
                    {
                        itemList[itemId] += addedWeight;
                        itemAmounts[itemId] += item.amount;
                    }
                }

                var slots = composter.inventorySlots;

                if (itemList.Count >= slots) return null;

                var sortedItemWeights = itemList.OrderByDescending(key => key.Value).ToDictionary(pair => pair.Key, pair => pair.Value);
                var maxSlots = inventorySlotSize - (fertilizerItems > 0 ? fertilizerItems : 1);
                var newItemStacks = new Dictionary<int, int>();

                var totalWeight = sortedItemWeights.Sum(item => item.Value);
                var weightPerSlot = totalWeight / maxSlots;

                var itemProportions = sortedItemWeights.ToDictionary(
                    item => item.Key,
                    item => item.Value / totalWeight
                );

                var itemSlots = itemProportions.ToDictionary(
                    item => item.Key,
                    item => (int)Math.Ceiling(item.Value / weightPerSlot)
                );

                var allocatedSlots = 0;

                for (var i = 0; i < sortedItemWeights.Count; i++)
                {
                    var item = sortedItemWeights.ElementAt(i);
                    var itemId = item.Key;
                    var itemWeight = item.Value;
                    var proportion = itemProportions[itemId];
                    var slotsRemaining = maxSlots - allocatedSlots - (sortedItemWeights.Count - i - 1);

                    var itemStacks = Math.Min(
                                            (int)Math.Ceiling(proportion * maxSlots),
                                            Math.Min(
                                                Math.Min(itemAmounts[itemId], slotsRemaining),
                                                maxSlots - allocatedSlots
                                            )
                                        );

                    newItemStacks.Add(itemId, itemStacks);

                    allocatedSlots += itemStacks;
                    if (allocatedSlots >= maxSlots)
                    {
                        break;
                    }
                }

                var added = 0;

                if (addedItem != null)
                {
                    var stacks = newItemStacks[addedItem.info.itemid];
                    var amount = itemAmounts[addedItem.info.itemid];
                    var stackSize = addedItem.info.stackable;

                    var maxFits = stacks * stackSize;
                    if (maxFits < amount)
                    {
                        var excess = amount - maxFits;
                        if (excess > 0)
                        {
                            var original = addedItem.amount;
                            addedItem.amount = excess;
                            added = original - excess;

                            itemAmounts[addedItem.info.itemid] = maxFits;
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        added += addedItem.amount;
                        addedItem?.Remove();
                    }
                }
                else
                {
                    foreach (var kv in newItemStacks)
                    {
                        var itemId = kv.Key;
                        var stacks = kv.Value;
                        var amount = itemAmounts[itemId];
                        var stackSize = itemStackSizes[itemId];

                        var maxFits = stacks * stackSize;
                        if (maxFits < amount)
                        {
                            return null;
                        }
                    }
                }

                foreach (var item in removeItems)
                {
                    item.RemoveFromContainer();
                    item.Remove();
                }

                addedItem?.MarkDirty();

                var slotIndex = 0;
                foreach (var kv in newItemStacks)
                {
                    var stacks = kv.Value;
                    var itemId = kv.Key;
                    var totalAmount = itemAmounts[itemId];

                    while (stacks > 0)
                    {
                        var amount = (int)Math.Round((float)totalAmount / stacks, 0, MidpointRounding.AwayFromZero);
                        var newItem = ItemManager.CreateByItemID(itemId, amount);
                        newItem.MoveToContainer(inventory, -1, false, false, null, false);
                        stacks--;
                        totalAmount -= amount;
                        slotIndex++;
                    }
                }

                inventory.MarkDirty();
                ItemManager.DoRemoves();

                return true;
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
                return null;
            }
            finally
            {
                Pool.FreeList(ref removeItems);
            }
        }

        #endregion
    }
}
