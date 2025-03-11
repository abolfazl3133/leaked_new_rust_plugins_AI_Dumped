
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

namespace Oxide.Plugins
{
    [Info("ShootInAir", "sdapro", "0.0.0")]
    public class ShootInAir : RustPlugin
    {
        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || !player.IsConnected || input == null || player.modelState.onground) return;
            
            var weapon = player.GetHeldEntity() as BaseProjectile;
            if (weapon == null || !weapon.IsValid() || weapon.IsBusy()) return;
            
            if (IsRocketLauncher(weapon))
            {
                weapon.SetFlag(BaseEntity.Flags.Busy, true);
                weapon.SendNetworkUpdate();
                return;
            }
            
            if (weapon.ServerIsReloading() || weapon.IsReloading())
            {
                player.modelState.flags = (int)ModelState.Flag.OnGround;
                player.SendModelState();
                return;
            }
            
            if (input.IsDown(BUTTON.FIRE_PRIMARY) && weapon.CanAttack())
            {
                player.modelState.flags = (int)(ModelState.Flag.OnGround | ModelState.Flag.Aiming);
                player.modelState.ducking = player.modelState.waterLevel = 0f;
                
                if (weapon.recoil != null)
                {
                    weapon.recoil.recoilYawMin = weapon.recoil.recoilYawMax = 
                    weapon.recoil.recoilPitchMin = weapon.recoil.recoilPitchMax = 0;
                }
                
                weapon.SendNetworkUpdate();
                player.SendModelState();
            }
        }
		
        private bool IsRocketLauncher(BaseProjectile weapon){string weaponName = weapon.ShortPrefabName;return weaponName == "rocket.launcher" || weaponName == "rocket.launcher.dragon" || weaponName == "multiplegrenadelauncher";}

        object OnPlayerViolation(BasePlayer player, AntiHackType type, float amount)
        {
            return !player.modelState.onground ? false : null;
        }

        object OnWeaponFired(BaseProjectile weapon, BasePlayer player)
        {
            if (!player.modelState.onground)
            {
                if (IsRocketLauncher(weapon))
                {
                    return true;
                }
                
                if (weapon.recoil != null && !weapon.IsBusy() && weapon.CanAttack())
                {
                    weapon.recoil.recoilYawMin = weapon.recoil.recoilYawMax = 
                    weapon.recoil.recoilPitchMin = weapon.recoil.recoilPitchMax = 0;
                }
                return true;
            }
            return null;
        }
        private void SendMessage(BasePlayer player, string message)
        {
            if (player != null && player.IsConnected)
            {
                player.ChatMessage(message);
            }
        }
    }
}