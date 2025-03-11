using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Instant Loot", "by bantik aka gpt koder", "1.0.0")]
    [Description("Мгновенный взлом и автоматический сбор добычи")]
    class InstantLoot : RustPlugin
    {
        private const string UsePermission = "instantloot.use";
        private const string AutoLootPermission = "instantloot.autoloot";

        #region Oxide Hooks

        private void Init()
        {
            // Регистрация разрешений
            permission.RegisterPermission(UsePermission, this);
            permission.RegisterPermission(AutoLootPermission, this);
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null || info.InitiatorPlayer == null)
                return;

            BasePlayer player = info.InitiatorPlayer;

            // Проверяем, есть ли у игрока разрешение
            if (!permission.UserHasPermission(player.UserIDString, UsePermission))
                return;

            // Проверяем, является ли объект бочкой или ящиком
            if (entity is LootContainer)
            {
                LootContainer container = entity as LootContainer;
                
                // Мгновенно уничтожаем контейнер
                container.health = 0f;

                // Если у игрока есть разрешение на автолут
                if (permission.UserHasPermission(player.UserIDString, AutoLootPermission))
                {
                    // Ждем следующий кадр перед сбором лута
                    NextTick(() => 
                    {
                        if (container != null && !container.IsDestroyed)
                        {
                            CollectLoot(container, player);
                            container.Kill();
                        }
                    });
                }
                else
                {
                    container.Die();
                }
            }
        }

        #endregion

        #region Helper Methods

        private void CollectLoot(LootContainer container, BasePlayer player)
        {
            if (container?.inventory == null || player?.inventory == null)
                return;

            int collectedItems = 0;
            string itemsList = "";

            // Перемещаем все предметы из контейнера в инвентарь игрока
            foreach (Item item in container.inventory.itemList.ToArray())
            {
                if (item == null)
                    continue;

                // Пытаемся добавить в основной инвентарь
                if (item.MoveToContainer(player.inventory.containerMain))
                {
                    collectedItems++;
                    itemsList += $"\n- {item.amount}x {item.info.displayName.english}";
                    continue;
                }

                // Если не получилось в основной, пробуем в пояс
                if (item.MoveToContainer(player.inventory.containerBelt))
                {
                    collectedItems++;
                    itemsList += $"\n- {item.amount}x {item.info.displayName.english}";
                    continue;
                }

                // Если никуда не получилось положить, выбрасываем на землю
                Vector3 dropPosition = player.transform.position + (player.transform.forward * 1f);
                item.Drop(dropPosition, Vector3.zero, Quaternion.identity);
                SendReply(player, $"Инвентарь полон! {item.amount}x {item.info.displayName.english} выброшено на землю.");
            }

            // Принудительно обновляем инвентарь
            player.inventory.containerMain.MarkDirty();
            player.inventory.containerBelt.MarkDirty();
            player.SendNetworkUpdate();

            // Отправляем сообщение о собранных предметах
            if (collectedItems > 0)
            {
                SendReply(player, $"Собрано {collectedItems} предметов:{itemsList}");
            }
        }

        #endregion
    }
} 