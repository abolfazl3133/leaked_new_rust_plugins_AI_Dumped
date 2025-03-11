

//  ######  
//  #     # 
//  #     # 
//  ######  
//  #   #   
//  #    #  
//  #     # 
//
//  #     # 
//  #     # 
//  #     # 
//  #     # 
//  #     # 
//  #     # 
//   #####  
//
//   #####  
//  #     # 
//  #       
//   #####  
//        # 
//  #     # 
//   #####  
//
//  #######
//     #    
//     #    
//     #    
//     #    
//     #    
//     #   
// 
//   ####  #####    ##   #####  ######   ####  
//  #      #    #  #  #  #    # #    #  #    # 
//   ####  #    # #    # #    # #    #  #    # 
//       # #    # ###### #####  ######  #    # 
//  #    # #    # #    # #      #   #   #    #  Creator
//   ####  #####  #    # #      #    #   #### 

using UnityEngine;
using Oxide.Core;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("ShootInAir", "sdapro", "0.5.2")]
    public class ShootInAir : RustPlugin
    {
        private const string PERMISSION_USE = "shootinair.use";
        private Dictionary<ulong, float> lastShotTime = new Dictionary<ulong, float>();
        private readonly Dictionary<string, float> weaponDelays = new Dictionary<string, float>
        {
            {"ak47u.entity", 0.1f},
            {"lr300.entity", 0.08f},
            {"bolt_rifle.entity", 2.0f},
            {"l96.entity", 2.0f},
            {"ak47u_ice.entity", 0.1f},
            {"ak47u_diver.entity", 0.1f},
            {"m39.entity", 0.2f},
            {"mp5.entity", 0.08f},
            {"nailgun.entity", 0.2f},
            {"m249.entity", 0.09f}
        };
        private readonly HashSet<string> ignoredWeapons = new HashSet<string>
        {
            "rocket_launcher",
            "dragon",
            "multiple_grenade_launcher",
            "mgl"
        };
        void OnServerInitialized()
        {
            permission.RegisterPermission(PERMISSION_USE, this);
        }

        private float GetWeaponDelay(string weaponName)
        {
            weaponName = weaponName.ToLower();
            if (weaponDelays.ContainsKey(weaponName))
                return weaponDelays[weaponName];
            return 0.15f;
        }

        private bool IsRocketLauncher(BaseProjectile weapon)
        {
            if (weapon == null) return false;
            
            string weaponName = weapon.ShortPrefabName.ToLower();
            return ignoredWeapons.Any(w => weaponName.Contains(w));
        }

        object OnPlayerViolation(BasePlayer player, AntiHackType type, float amount)
        {
            if (type == AntiHackType.ProjectileHack && permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                return false;
            }
            return null;
        }

        object OnWeaponFired(BaseProjectile projectile, BasePlayer player)
        {
            if (projectile == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) return null;

            bool isRocket = IsRocketLauncher(projectile);
            
            if (isRocket)
            {
                return true;
            }
            return null;
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || !player.IsConnected || input == null) return;
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) return;
            
            var weapon = player.GetHeldEntity() as BaseProjectile;
            if (weapon == null || !weapon.IsValid() || weapon.IsBusy()) return;

            if (IsRocketLauncher(weapon))
            {
                return;
            }

            float currentTime = Time.realtimeSinceStartup;
            if (lastShotTime.ContainsKey(player.userID))
            {
                float weaponDelay = GetWeaponDelay(weapon.ShortPrefabName);
                float timeSinceLastShot = currentTime - lastShotTime[player.userID];
                if (timeSinceLastShot < weaponDelay)
                {
                    return;
                }
            }
            
            weapon.SetFlag(BaseEntity.Flags.Busy | BaseEntity.Flags.OnFire, false);
            
            if (input.IsDown(BUTTON.FIRE_PRIMARY))
            {
                Effect.server.Run(weapon.attackFX.resourcePath, weapon);
                weapon.SignalBroadcast(BaseEntity.Signal.Attack, string.Empty);
                
                if (weapon.primaryMagazine != null && weapon.primaryMagazine.ammoType != null)
                {
                    var mod = weapon.primaryMagazine.ammoType.GetComponent<ItemModProjectile>();
                    if (mod != null)
                    {
                        weapon.repeatDelay = 0f;
                        weapon.primaryMagazine.contents = 1;
                        weapon.ServerUse();
                        lastShotTime[player.userID] = currentTime;
                    }
                }
            }
        }

        object OnProjectileUpdate(BaseProjectile projectile)
        {
            if (projectile == null) return null;
            
            var player = projectile.GetComponent<BasePlayer>();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) return null;

            if (IsRocketLauncher(projectile))
            {
                return true;
            }

            projectile.SetFlag(BaseEntity.Flags.Busy | BaseEntity.Flags.OnFire, false);
            return null;
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            if (player != null && lastShotTime.ContainsKey(player.userID))
            {
                lastShotTime.Remove(player.userID);
            }
        }
    }
}