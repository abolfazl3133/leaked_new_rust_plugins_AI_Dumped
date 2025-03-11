using Oxide.Core;
using Oxide.Game.Rust;
using UnityEngine;
using System;
using System.Timers;

namespace Oxide.Plugins
{
    [Info("RussianRoulettePlugin", "Scrooge", "1.0.0")]
    [Description("Русская рулетка: каждые X минут взрывает случайного игрока")] 
    public class RussianRoulettePlugin : RustPlugin
    {
        private Timer rouletteTimer;
        private float explosionInterval;

        private void LoadConfig()
        {
            explosionInterval = Config.GetFloat("ExplosionInterval", 5f) * 60f; // Получение интервала из конфигурации в секундах
        }

        private void OnServerInitialized()
        {
            LoadConfig();
            StartRouletteTimer();
        }

        private void StartRouletteTimer()
        {
            rouletteTimer = new Timer(explosionInterval * 1000); // Перевод в миллисекунды
            rouletteTimer.Elapsed += OnRouletteTimerElapsed;
            rouletteTimer.Start();
        }

        private void OnRouletteTimerElapsed(object sender, ElapsedEventArgs e)
        {
            var players = BasePlayer.activePlayerList;
            if (players.Count > 0)
            {
                var randomPlayer = players[UnityEngine.Random.Range(0, players.Count)];
                // Создание эффекта взрыва
                Effect.server.Run("assets/prefabs/explosives/explosive.timed.prefab", randomPlayer.transform.position, Vector3.up);
                // Уничтожение игрока
                randomPlayer.Die();
            }
        }

        private void Unload()
        {
            rouletteTimer?.Stop();
            rouletteTimer?.Dispose();
        }
    }
}
