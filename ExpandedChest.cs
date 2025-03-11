using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ExpandedChest", "By Bantik", "1.0.0")]
    [Description("Расширяет ёмкость хранения сундуков.")]
    public class ExpandedChest : RustPlugin
    {
        private const int DefaultSlots = 30; // Стандартное количество слотов для ящиков 
        private const int ExpandedSlots = 54; // Количество слотов, равное гробу

        private const string PermissionUse = "expandedchest.use";

        
        private void Init()
        {
            permission.RegisterPermission(PermissionUse, this);
        }

        
        private void OnServerInitialized()
        {
            Puts("Плагин ExpandedChest инициализирован. Расширяем ёмкость сундуков...");

            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var storage = entity as StorageContainer;
                if (storage != null && IsChest(storage)) 
                {
                    ExpandChest(storage); 
                }
            }
        }

        
        private void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            if (gameObject == null) return;

            var player = planner?.GetOwnerPlayer();
            if (player != null && !permission.UserHasPermission(player.UserIDString, PermissionUse))
            {
                Puts($"Игрок {player.displayName} не имеет прав на использование ExpandedChest.");
                return;
            }

            var storage = gameObject.GetComponent<StorageContainer>();
            if (storage != null && IsChest(storage)) 
            {
                ExpandChest(storage); 
            }
        }

        
        private bool IsChest(StorageContainer storage)
        {
            
            return storage != null && (storage.ShortPrefabName.Contains("box") || storage.ShortPrefabName.Contains("coffin"));
        }

       
        private void ExpandChest(StorageContainer storage)
        {
            if (storage.inventorySlots == ExpandedSlots) return;

            storage.inventorySlots = ExpandedSlots;
            var oldInventory = storage.inventory;
            var items = oldInventory?.itemList ?? new List<Item>();

            
            storage.inventory = new ItemContainer
            {
                capacity = ExpandedSlots,
                entityOwner = storage,
                allowedContents = ItemContainer.ContentsType.Generic,
                maxStackSize = storage.maxStackSize
            };
            storage.inventory.ServerInitialize(null, ExpandedSlots);
            storage.inventory.entityOwner = storage;
            storage.inventory.GiveUID();
            storage.SendNetworkUpdateImmediate();

            foreach (var item in items)
            {
                item.MoveToContainer(storage.inventory, -1, true);
            }

            Puts($"Сундук расширен до {ExpandedSlots} слотов.");
        }
    }
}
