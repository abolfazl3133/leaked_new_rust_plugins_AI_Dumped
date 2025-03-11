using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Prevent Flyhack Kick", "sdapro", "1.1.1")]

    public class PreventFlyhackKick : RustPlugin
    {

        private void OnPlayerTick(BasePlayer player)
        {
            if (player == null )
                return;

            if (IsPlayerInFlight(player))
            {
                player.PauseFlyHackDetection(5f);
                player.PauseSpeedHackDetection(5f);
            }
        }

        private bool IsPlayerInFlight(BasePlayer player)
        {
            float groundDistance = player.transform.position.y - TerrainMeta.HeightMap.GetHeight(player.transform.position);
            return groundDistance > 1.5f || player.GetParentEntity() != null;
        }
    }
}
