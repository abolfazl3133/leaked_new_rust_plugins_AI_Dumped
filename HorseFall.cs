
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

using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("HorseFall", "sdapro", "0.6.0")]
    public class HorseFall : RustPlugin
    {
        private Dictionary<ulong, float> fallEndTimes = new Dictionary<ulong, float>();
        private Dictionary<ulong, BaseRagdoll> activeRagdolls = new Dictionary<ulong, BaseRagdoll>();
        private Dictionary<ulong, ExplosionData> explosionDataMap = new Dictionary<ulong, ExplosionData>();

        private class ExplosionData
        {
    public Vector3 Direction { get; set; }
    public float Force { get; set; }
        }

        void Init()
        {
            cmd.AddChatCommand("fall", this, nameof(CmdFall));
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;

            var player = entity as BasePlayer;
    if (player == null || player.IsDead()) return null;

    if (info.damageTypes.GetMajorityDamageType() == Rust.DamageType.Explosion || 
        info.damageTypes.Has(Rust.DamageType.Explosion))
        {
        Vector3 explosionDirection;
    if (info.WeaponPrefab != null)
        {
            explosionDirection = (player.transform.position - info.HitPositionWorld).normalized;
        }
    else
        {
            explosionDirection = (info.PointEnd - info.PointStart).normalized;
        }

    float force = Mathf.Min(info.damageTypes.Total() * 0.3f, 15f);

        explosionDataMap[player.userID] = new ExplosionData 
        { 
            Direction = explosionDirection, 
            Force = force 
        };

        CreateRagdoll(player);
    }

    return null;
}



        void CreateRagdoll(BasePlayer player)
        {
           if (player == null || player.IsDead()) return; 

    if (activeRagdolls.ContainsKey(player.userID))
    {
        var oldRagdoll = activeRagdolls[player.userID];
        if (oldRagdoll != null && !oldRagdoll.IsDestroyed)
        {
            oldRagdoll.Kill();
        }
        activeRagdolls.Remove(player.userID);
    }
            fallEndTimes[player.userID] = Time.time + 2f;

            BaseRagdoll ragdoll = GameManager.server.CreateEntity("assets/prefabs/player/player_temp_ragdoll.prefab") as BaseRagdoll;
            if (ragdoll != null)
            {
                ragdoll.transform.position = player.transform.position;
                ragdoll.transform.rotation = player.transform.rotation;

                var ragdollComponent = ragdoll.GetComponent<Ragdoll>();
                if (ragdollComponent != null)
                {
                    ragdollComponent.simOnServer = true;
                }

                ragdoll.InitFromPlayer(player, Vector3.up * 5f, true, false, false, null);
                ragdoll.Spawn();

                activeRagdolls[player.userID] = ragdoll;
                player.SetPlayerFlag(BasePlayer.PlayerFlags.Ragdolling, true);
                ragdoll.MountPlayer(player);

                timer.Once(0.0f, () =>
                {
                    if (ragdoll != null && !ragdoll.IsDestroyed)
                    {
                        var mainRigidbody = ragdoll.GetComponent<Rigidbody>();
                        if (mainRigidbody != null)
                        {
                            mainRigidbody.useGravity = false;
                            mainRigidbody.AddForce(Vector3.up * 200f, ForceMode.Impulse);
                        }
                    }
                });

                timer.Once(0.1f, () =>
                {
                    if (ragdoll != null && !ragdoll.IsDestroyed && explosionDataMap.ContainsKey(player.userID))
                    {
                        var explosionData = explosionDataMap[player.userID];
                        var mainRigidbody = ragdoll.GetComponent<Rigidbody>();
                        
                        if (mainRigidbody != null)
                        {
                            mainRigidbody.useGravity = true;
                            mainRigidbody.AddForce(explosionData.Direction * explosionData.Force * 20f, ForceMode.Impulse);
                            mainRigidbody.AddTorque(UnityEngine.Random.insideUnitSphere * 20f, ForceMode.Impulse);
                        }
                        
                        explosionDataMap.Remove(player.userID);
                    }
                });

                Effect.server.Run("assets/bundled/prefabs/fx/player/beartrap_scream.prefab", player.transform.position);
            }
        }

        object CanRagdollDismount(BaseRagdoll ragdoll, BasePlayer player)
        {
            if (fallEndTimes.ContainsKey(player.userID) && Time.time < fallEndTimes[player.userID])
            {
                return false;
            }
            return null;
        }

        object OnPlayerRespawn(BasePlayer player)
        {
            if (fallEndTimes.ContainsKey(player.userID) && Time.time < fallEndTimes[player.userID])
            {
                return true;
            }
            return null;
        }

        void CmdFall(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            CreateRagdoll(player);
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            if (activeRagdolls.ContainsKey(player.userID))
            {
                var ragdoll = activeRagdolls[player.userID];
                if (ragdoll != null && !ragdoll.IsDestroyed)
                {
                    ragdoll.Kill();
                }
                activeRagdolls.Remove(player.userID);
                fallEndTimes.Remove(player.userID);
            }
            explosionDataMap.Remove(player.userID);
        }

        void Unload()
        {
            foreach (var kvp in activeRagdolls)
            {
                var ragdoll = kvp.Value;
                if (ragdoll != null && !ragdoll.IsDestroyed)
                {
                    var player = BasePlayer.FindByID(kvp.Key);
                    if (player != null)
                    {
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.Ragdolling, false);
                        ragdoll.DismountPlayer(player);
                    }
                    ragdoll.Kill();
                }
            }
            
            activeRagdolls.Clear();
            fallEndTimes.Clear();
            explosionDataMap.Clear();
        }
    }
}