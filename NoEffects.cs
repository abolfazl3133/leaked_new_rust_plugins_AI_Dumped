using Oxide.Core.Plugins;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("NoEffects", "Spike ft Darwick", "1.1.0")]
    [Description("Removes specific debuffs based on player permissions.")]

    class NoEffects : RustPlugin
    {
        private const string PermissionCold = "noeffects.cold";
        private const string PermissionHeat = "noeffects.heat";
        private const string PermissionBleeding = "noeffects.bleeding";
        private const string PermissionRadiation = "noeffects.radiation";
        private const string PermissionWet = "noeffects.wet";

        void Init()
        {
            permission.RegisterPermission(PermissionCold, this);
            permission.RegisterPermission(PermissionHeat, this);
            permission.RegisterPermission(PermissionBleeding, this);
            permission.RegisterPermission(PermissionRadiation, this);
            permission.RegisterPermission(PermissionWet, this);
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || !(entity is BasePlayer) || info == null) return;

            BasePlayer player = entity as BasePlayer;

            // Process cold effects
            if ((info.damageTypes.Has(Rust.DamageType.Cold) || info.damageTypes.Has(Rust.DamageType.ColdExposure)) 
                && permission.UserHasPermission(player.UserIDString, PermissionCold))
            {
                player.metabolism.temperature.SetValue(20f); // Neutralize cold temperature
                info.damageTypes.ScaleAll(0f); // Remove damage
                return;
            }

            // Process heat effects
            if (info.damageTypes.Has(Rust.DamageType.Heat) 
                && permission.UserHasPermission(player.UserIDString, PermissionHeat))
            {
                info.damageTypes.ScaleAll(0f); // Remove damage
                return;
            }

            // Process radiation effects
            if ((info.damageTypes.Has(Rust.DamageType.Radiation) || info.damageTypes.Has(Rust.DamageType.RadiationExposure)) 
                && permission.UserHasPermission(player.UserIDString, PermissionRadiation))
            {
                player.metabolism.radiation_level.SetValue(0f);
                player.metabolism.radiation_poison.SetValue(0f);
                info.damageTypes.ScaleAll(0f); // Remove damage
                return;
            }

            // Process bleeding effects
            if (info.damageTypes.Has(Rust.DamageType.Bleeding) 
                && permission.UserHasPermission(player.UserIDString, PermissionBleeding))
            {
                player.metabolism.bleeding.SetValue(0f); // Clear bleeding
                info.damageTypes.ScaleAll(0f); // Remove damage
                return;
            }
        }

        void OnPlayerTick(BasePlayer player)
        {
            if (player == null || !player.IsConnected) return;

            // Process wet effects
            if (permission.UserHasPermission(player.UserIDString, PermissionWet))
            {
                player.metabolism.wetness.SetValue(0f); // Clear wetness
            }
        }
    }
}
