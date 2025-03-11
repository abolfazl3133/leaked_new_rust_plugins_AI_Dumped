using System;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using Rust;

namespace Oxide.Plugins
{
    [Info("AutoHeals", "sdapro", "1.0.0")]
    class AutoHeals : RustPlugin
    {
        private Dictionary<ulong, HealthData> playerData = new Dictionary<ulong, HealthData>();

        class HealthData
        {
            public float lastHealth;
            public float lastHealTime;
            public float lastHealTick;

            public HealthData(float health, float time, float tick)
            {
                lastHealth = health;
                lastHealTime = time;
                lastHealTick = tick;
            }
        }
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BasePlayer)
            {
                var player = entity.ToPlayer();

                if (player == null || info == null)
                {
                    return;
                }

                if (info.damageTypes.GetMajorityDamageType() != DamageType.Suicide)
                {
                    UpdateHealthData(player);
                }
            }
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (player == null)
            {
                return;
            }

            if (!playerData.ContainsKey(player.userID))
            {
                playerData[player.userID] = new HealthData(player.health, Time.realtimeSinceStartup, 0f);
            }
        }

        void OnPlayerWound(BasePlayer player)
        {
            if (player == null)
            {
                return;
            }

            if (playerData.ContainsKey(player.userID))
            {
                playerData.Remove(player.userID);
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null)
            {
                return;
            }

            if (playerData.ContainsKey(player.userID))
            {
                playerData.Remove(player.userID);
            }
        }

        void UpdateHealthData(BasePlayer player)
        {
            if (player == null)
            {
                return;
            }

            if (!playerData.ContainsKey(player.userID))
            {
                playerData[player.userID] = new HealthData(player.health, Time.realtimeSinceStartup, 0f);
            }
            else
            {
                playerData[player.userID].lastHealth = player.health;
                playerData[player.userID].lastHealTime = Time.realtimeSinceStartup;
            }
        }

        void OnTick()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
				
                if (player == null || player.IsDead() || player.IsSleeping() || !player.IsConnected)
                {
                    continue;
                }

                if (playerData.ContainsKey(player.userID))
                {
                    var timeSinceLastDamage = Time.realtimeSinceStartup - playerData[player.userID].lastHealTime;
                    if (timeSinceLastDamage >= 1.3f && player.health < 100f)
                    {
                        if (playerData[player.userID].lastHealTick + 0.2f <= Time.realtimeSinceStartup)
                        {
                            player.health += 2f;
                            playerData[player.userID].lastHealth = player.health;
                            playerData[player.userID].lastHealTick = Time.realtimeSinceStartup;
                        }
                    }
                }
                else
                {
                    playerData[player.userID] = new HealthData(player.health, Time.realtimeSinceStartup, 0f);
                }
            }
        }
    }
}