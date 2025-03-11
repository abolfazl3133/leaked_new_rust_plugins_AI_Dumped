using System;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("StaminaControl", "sdapro", "1.3.4")]
    [Description("Manage stamina for sprinting players using thirst.")]
    class StaminaControl : RustPlugin
    {
        private const string StaminaPermission = "staminacontrol.use";
        private const float ThirstCostPerSecond = 15f; // Снижение жажды в секунду при беге
        private const float ThirstRecoveryRate = 15f; // Восстановление жажды в секунду
        private const float MinHydration = -200f; // Минимальное значение жажды
        private const float MaxHydration = 200f; // Максимальное значение жажды
        private const float JumpThirstCost = 50f; // Жажда за прыжок
        private const float RecoveryDelay = 4f; // Задержка восстановления жажды после остановки

        private Dictionary<ulong, StaminaData> playerData = new Dictionary<ulong, StaminaData>();

        class StaminaData
        {
            public bool isSprinting;
            public bool wasOnGround;
            public float lastUpdateTime;
            public float lastJumpTime;
            public float lastStopTime;

            public StaminaData()
            {
                isSprinting = false;
                wasOnGround = true;
                lastUpdateTime = Time.realtimeSinceStartup;
                lastJumpTime = 0f;
                lastStopTime = 0f;
            }
        }

        private void Init()
        {
            permission.RegisterPermission(StaminaPermission, this);
        }

        private void OnPlayerTick(BasePlayer player)
        {
            if (player == null || player.IsDead() || !player.IsConnected)
                return;

            if (!permission.UserHasPermission(player.UserIDString, StaminaPermission))
                return;

            if (!playerData.ContainsKey(player.userID))
            {
                playerData[player.userID] = new StaminaData();
            }

            var data = playerData[player.userID];
            bool isCurrentlySprinting = player.modelState.sprinting;
            bool isOnGround = player.IsOnGround();
            float currentTime = Time.realtimeSinceStartup;
            float deltaTime = currentTime - data.lastUpdateTime;

            if (!isOnGround && data.wasOnGround && currentTime - data.lastJumpTime > 0.1f)
            {
                player.metabolism.hydration.value -= JumpThirstCost;
                data.lastJumpTime = currentTime;

                if (player.metabolism.hydration.value < MinHydration)
                {
                    player.metabolism.hydration.value = MinHydration;
                }
            }

            if (isCurrentlySprinting)
            {
                player.metabolism.hydration.value -= ThirstCostPerSecond * deltaTime;

                if (player.metabolism.hydration.value < MinHydration)
                {
                    player.metabolism.hydration.value = MinHydration;
                }
                data.lastStopTime = currentTime;
            }
            else
            {
                if (currentTime - data.lastStopTime >= RecoveryDelay)
                {
                    player.metabolism.hydration.value += ThirstRecoveryRate * deltaTime;

                    if (player.metabolism.hydration.value > MaxHydration)
                    {
                        player.metabolism.hydration.value = MaxHydration;
                    }
                }
            }
            data.wasOnGround = isOnGround;
            data.isSprinting = isCurrentlySprinting;
            data.lastUpdateTime = currentTime;
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (playerData.ContainsKey(player.userID))
            {
                playerData.Remove(player.userID);
            }
        }
    }
}
