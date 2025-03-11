using Oxide.Core;
using Oxide.Game.Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ChairPlugin", "Scrooge", "1.5.0")]
    [Description("Спавнит стул для сидения по команде /sit")] 
    public class ChairPlugin : RustPlugin
    {
        [ChatCommand("sit")]
        private void SitCommand(BasePlayer player, string command, string[] args)
        {
            // Проверка на разрешение для дивана
            string permission = "chairplugin.spawn.sofa";
            string chairPrefab = "assets/prefabs/props/furniture/chair/chair.prefab";
            string sofaPrefab = "assets/prefabs/props/furniture/sofa/sofa.prefab";

            // Если у игрока есть разрешение, спавним диван
            if (player.HasPermission(permission))
            {
                var sofaEntity = GameManager.server.CreateEntity(sofaPrefab, player.transform.position, Quaternion.identity);
                if (sofaEntity != null)
                {
                    sofaEntity.Spawn();
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.Sitting, true);
                }
                return;
            }

            // Если у игрока нет разрешения, спавним стул
            var chairEntity = GameManager.server.CreateEntity(chairPrefab, player.transform.position, Quaternion.identity);
            if (chairEntity != null)
            {
                chairEntity.Spawn();
                player.SetPlayerFlag(BasePlayer.PlayerFlags.Sitting, true);
            }
        }
    }
}
