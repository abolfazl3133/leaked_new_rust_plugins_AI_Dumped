// Плагин для выпадения конского навоза каждые 5 минут
// Версия: 1.0.0

using System;
using System.Timers;
using Oxide.Core;
using Oxide.Game.Rust;

namespace Oxide.Plugins
{
    [Info("PoopPlugin", "Scrooge", "1.5.0")]
    [Description("Выпадение конского навоза каждые 5 минут")]
    public class PoopPlugin : RustPlugin
    {
        private Timer poopTimer;
        private int dropInterval;

        private void LoadConfig()
        {
            dropInterval = Config.GetInt("DropInterval", 300); // Получение интервала из конфигурации
            poopTimer = new Timer(dropInterval * 1000); // Перевод в миллисекунды
            poopTimer.Elapsed += OnPoopTimerElapsed;
            poopTimer.Start();
        }

        private void OnPoopTimerElapsed(object sender, ElapsedEventArgs e)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DropPoop(player);
            }
        }

        private void DropPoop(BasePlayer player)
        {
            // Создание и выпадение "какашки"
            var poopPrefab = PrefabAttribute.server.Find<Prefab>("assets/prefabs/props/poop/horse_poop.prefab"); // Путь к конскому навозу
            if (poopPrefab != null)
            {
                var poopEntity = GameManager.server.CreateEntity(poopPrefab.resourcePath, player.transform.position, Quaternion.identity);
                if (poopEntity != null)
                {
                    poopEntity.Spawn();
                }
            }
        }

        private void OnServerInitialized()
        {
            LoadConfig();
        }
    }
}
