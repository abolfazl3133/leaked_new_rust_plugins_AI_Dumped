using System.Collections.Generic;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Auto Diving Gear", "sdapro", "1.0.6")]
    public class AutoDivingGear : RustPlugin
    {
        private List<string> divingGear = new List<string>
        {
            "diving.tank",
            "diving.mask",
            "diving.fins",
            "diving.wetsuit"
        };
        
        private Dictionary<ulong, List<Item>> playerHiddenArmor = new Dictionary<ulong, List<Item>>();
        private Dictionary<ulong, bool> playerHasDivingGear = new Dictionary<ulong, bool>();

        private void OnTick()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected) continue;

                if (IsSwimming(player))
                {
                    if (!playerHasDivingGear.ContainsKey(player.userID) || !playerHasDivingGear[player.userID])
                    {
                        StoreArmorInHiddenSlots(player);
                        EquipDivingGear(player);
                        player.inventory.containerWear.SetLocked(true);
                        playerHasDivingGear[player.userID] = true;
                    }
                }
                else
                {
                    if (playerHasDivingGear.ContainsKey(player.userID) && playerHasDivingGear[player.userID])
                    {
                        RemoveDivingGear(player);
                        RestoreArmorFromHiddenSlots(player);
                        player.inventory.containerWear.SetLocked(false);
                        playerHasDivingGear[player.userID] = false;
                    }
                }
            }
        }

        private void StoreArmorInHiddenSlots(BasePlayer player)
        {
            if (!playerHiddenArmor.ContainsKey(player.userID))
                playerHiddenArmor[player.userID] = new List<Item>();

            foreach (var item in player.inventory.containerWear.itemList.ToArray())
            {
                playerHiddenArmor[player.userID].Add(item);  
                player.inventory.containerWear.Remove(item);  
            }
        }

        private void EquipDivingGear(BasePlayer player)
        {
            foreach (string itemShortname in divingGear)
            {
                if (!IsItemEquipped(player, itemShortname))
                {
                    Item item = ItemManager.CreateByName(itemShortname, 1);
                    if (item != null)
                    {
                        player.inventory.GiveItem(item, player.inventory.containerWear);
                    }
                }
            }
        }

        private void RemoveDivingGear(BasePlayer player)
        {
            foreach (string itemShortname in divingGear)
            {
                var item = player.inventory.containerWear.itemList.Find(i => i.info.shortname == itemShortname);
                if (item != null)
                {
                    item.RemoveFromContainer();
                    item.Remove();
                }
            }

            player.inventory.containerWear.MarkDirty();
            player.inventory.SendUpdatedInventory(PlayerInventory.Type.Wear, player.inventory.containerWear);
        }

        private void RestoreArmorFromHiddenSlots(BasePlayer player)
        {
            if (playerHiddenArmor.ContainsKey(player.userID))
            {
                foreach (var item in playerHiddenArmor[player.userID].ToArray())
                {
                    if (item != null && item.MoveToContainer(player.inventory.containerWear))
                    {
                        playerHiddenArmor[player.userID].Remove(item);
                    }
                }
                player.inventory.containerWear.MarkDirty();
                player.inventory.SendUpdatedInventory(PlayerInventory.Type.Wear, player.inventory.containerWear);

            }
        }

        private bool IsSwimming(BasePlayer player)
        {
            return player.modelState.waterLevel > 0.5f; 
        }

        private bool IsItemEquipped(BasePlayer player, string itemShortname)
        {
            return player.inventory.containerWear.itemList.Exists(i => i.info.shortname == itemShortname);
        }
    }
}
