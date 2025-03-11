using Oxide.Core;
using Oxide.Game.Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ChairPlugin", "Scrooge", "1.0.0")]
    [Description("Спавнит стул для сидения по команде /sit")] 
    public class ChairPlugin : RustPlugin
    {
        [ChatCommand("sit")]
        private void SitCommand(BasePlayer player, string command, string[] args)
        {
            // Путь к префабу стула
            var chairPrefab = "assets/prefabs/props/furniture/chair/chair.prefab";

            // Создание стула на позиции игрока
            var chairEntity = GameManager.server.CreateEntity(chairPrefab, player.transform.position, Quaternion.identity);
            if (chairEntity != null)
            {
                chairEntity.Spawn();
                player.SetPlayerFlag(BasePlayer.PlayerFlags.Sitting, true);
            }
        }
    }
}
