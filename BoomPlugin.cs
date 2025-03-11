using Oxide.Core;
using Oxide.Game.Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BoomPlugin", "Scrooge", "1.0.0")]
    [Description("Взрыв игрока по команде /boom")] 
    public class BoomPlugin : RustPlugin
    {
        [ChatCommand("boom")]
        private void BoomCommand(BasePlayer player, string command, string[] args)
        {
            // Создание эффекта взрыва
            Effect.server.Run("assets/prefabs/explosives/explosive.timed.prefab", player.transform.position, Vector3.up);
            // Уничтожение игрока
            player.Die();
        }
    }
}
