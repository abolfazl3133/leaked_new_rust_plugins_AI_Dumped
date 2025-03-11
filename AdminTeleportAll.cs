using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AdminTeleportAll", "sdapro", "1.0.1")]
    public class AdminTeleportAll : RustPlugin
    {
        [ChatCommand("tpall")]
        private void TeleportAllCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) { player.ChatMessage("У вас нет прав для выполнения этой команды."); return;}
            if (player.IsDead())
            {
                player.ChatMessage("Вы должны быть живы, чтобы использовать эту команду.");
                return;
            }
            Vector3 adminPosition = player.transform.position;

            foreach (var targetPlayer in BasePlayer.activePlayerList.ToList())
            {
                if (targetPlayer == player)
                {
                    continue;
                }

                targetPlayer.Teleport(adminPosition);
                targetPlayer.SendConsoleCommand("effect.run", "assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab", adminPosition);
                targetPlayer.ChatMessage("Вы были телепортированы к администратору!");
            }
            player.ChatMessage("Все активные игроки были телепортированы к вам.");
        }
    }
}
