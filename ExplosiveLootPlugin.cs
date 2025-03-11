using Oxide.Core;
using Oxide.Game.Rust;
using UnityEngine;
using System;

namespace Oxide.Plugins
{
    [Info("ExplosiveLootPlugin", "Scrooge", "1.0.0")]
    [Description("Шанс взрыва при лутании ящика или ломании бочки")] 
    public class ExplosiveLootPlugin : RustPlugin
    {
        private float explosionChance;

        private void LoadConfig()
        {
            explosionChance = Config.GetFloat("ExplosionChance", 5f); // Получение шанса взрыва из конфигурации
        }

        private void OnServerInitialized()
        {
            LoadConfig();
        }

        private void OnLootEntity(BasePlayer player, LootableEntity entity)
        {
            // Проверка на ящик или бочку
            if (entity is StorageContainer || entity is Barrel)
            {
                if (UnityEngine.Random.Range(0f, 100f) < explosionChance)
                {
                    // Создание эффекта взрыва
                    Effect.server.Run("assets/prefabs/explosives/explosive.timed.prefab", player.transform.position, Vector3.up);
                    // Уничтожение игрока
                    player.Die();
                }
            }
        }
    }
}
