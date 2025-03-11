using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("No Effects", "Spike ft Darwick", "1.0.0")]
    [Description("Grants immunity to cold, heat, wet, or bleeding effects based on permissions")]
    public class NoEffects : CovalencePlugin
    {
        // Define permissions
        private const string PermissionCold = "noeffects.cold";
        private const string PermissionHeat = "noeffects.heat";
        private const string PermissionWet = "noeffects.wet";
        private const string PermissionBleed = "noeffects.bleed";

        // Initialize plugin
        private void Init()
        {
            // Register permissions
            permission.RegisterPermission(PermissionCold, this);
            permission.RegisterPermission(PermissionHeat, this);
            permission.RegisterPermission(PermissionWet, this);
            permission.RegisterPermission(PermissionBleed, this);

            // Subscribe to necessary events
            Subscribe(nameof(OnPlayerMetabolismUpdate));
            Subscribe(nameof(OnPlayerHurt));
            Subscribe(nameof(OnEntityTakeDamage));
        }

        // Handle player hurt event to prevent bleeding
        private void OnPlayerHurt(BasePlayer player, HitInfo info)
        {
            if (permission.UserHasPermission(player.UserIDString, PermissionBleed))
            {
                player.metabolism.bleeding.value = 0;
                player.InvokeRepeating(nameof(ClearBleeding), 0f, 1f);
            }
        }

        // Prevent bleeding effect from happening
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            var player = entity.ToPlayer();
            if (player != null && permission.UserHasPermission(player.UserIDString, PermissionBleed))
            {
                player.metabolism.bleeding.value = 0;
                player.InvokeRepeating(nameof(ClearBleeding), 0f, 1f);
            }
        }

        // Clear bleeding repeatedly
        private void ClearBleeding(BasePlayer player)
        {
            if (player != null && permission.UserHasPermission(player.UserIDString, PermissionBleed))
            {
                player.metabolism.bleeding.value = 0;
            }
            else
            {
                player.CancelInvoke(nameof(ClearBleeding));
            }
        }

        // Handle player metabolism update to prevent cold, heat, and wet effects
        private void OnPlayerMetabolismUpdate(BasePlayer player, PlayerMetabolism metabolism)
        {
            if (permission.UserHasPermission(player.UserIDString, PermissionCold))
            {
                metabolism.temperature.min = Mathf.Max(metabolism.temperature.min, 0);
            }

            if (permission.UserHasPermission(player.UserIDString, PermissionHeat))
            {
                metabolism.temperature.max = Mathf.Min(metabolism.temperature.max, 0);
            }

            if (permission.UserHasPermission(player.UserIDString, PermissionWet))
            {
                metabolism.wetness.value = 0;
                player.InvokeRepeating(nameof(ClearWetness), 0f, 0.1f);
            }
        }

        // Clear wetness repeatedly
        private void ClearWetness(BasePlayer player)
        {
            if (player != null && permission.UserHasPermission(player.UserIDString, PermissionWet))
            {
                player.metabolism.wetness.value = 0;
            }
            else
            {
                player.CancelInvoke(nameof(ClearWetness));
            }
        }
    }
}
