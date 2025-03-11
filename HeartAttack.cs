using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("HeartAttack", "Scrooge", "1.0.0")]
    [Description("Система инфаркта для игры Rust.")]
    public class HeartAttack : RustPlugin
    {
        private Dictionary<ulong, float> playerHealth = new Dictionary<ulong, float>();
        private const float HeartAttackThreshold = 20f; // Уровень здоровья, при котором может произойти инфаркт
        private const float HeartAttackDamage = 30f; // Урон от инфаркта

        void OnPlayerInit(BasePlayer player)
        {
            // Инициализация здоровья игрока
            playerHealth[player.userID] = player.health;
        }

        void OnPlayerHealthChanged(BasePlayer player, float oldHealth, float newHealth)
        {
            if (!playerHealth.ContainsKey(player.userID))
                playerHealth[player.userID] = oldHealth;

            // Обновляем здоровье игрока
            playerHealth[player.userID] = newHealth;

            // Проверяем, можно ли вызвать инфаркт
            CheckForHeartAttack(player);
        }

        private void CheckForHeartAttack(BasePlayer player)
        {
            if (playerHealth[player.userID] <= HeartAttackThreshold)
            {
                // Игрок страдает инфарктом
                player.health -= HeartAttackDamage;

                // Сообщаем игроку об инфаркте
                PrintToChat(player, "Вы испытали инфаркт и потеряли здоровье!");
                
                // Вызываем смерть, если здоровье стало меньше или равно нулю
                if (player.health <= 0)
                {
                    player.Die(null); // Игрок умирает
                }
            }
        }

        private void OnPlayerDeath(BasePlayer player)
        {
            if (playerHealth.ContainsKey(player.userID))
                playerHealth.Remove(player.userID);
        }

        // Очистка при выгрузке плагина
        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (playerHealth.ContainsKey(player.userID))
                    playerHealth.Remove(player.userID);
            }
        }
    }
}