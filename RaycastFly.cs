using System;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("RaycastFly", "sdapro", "1.5.0")]
    public class RaycastFly : RustPlugin
    {
        private const float FlyForce = 3000f;
        private const float RayDistance = 10000f;
        private const float Cooldown = 0.1f;
        private const float MarkerDuration = 1f;

        private Dictionary<ulong, float> lastFlyTime = new Dictionary<ulong, float>();
        private Dictionary<ulong, Timer> playerTimers = new Dictionary<ulong, Timer>();

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
			if (!player.IsAdmin)
            {
                return;
            }
            if (player == null || input == null) return;
            if (lastFlyTime.TryGetValue(player.userID, out float lastTime) && Time.time - lastTime < Cooldown)
            {
                return;
            }

            if (input.IsDown(BUTTON.FIRE_THIRD))
            {
                FlyInDirection(player);
                lastFlyTime[player.userID] = Time.time;
            }

            if (!input.IsDown(BUTTON.FIRE_THIRD))
            {
                StopFlying(player);
            }
        }

        private void FlyInDirection(BasePlayer player)
        {
            if (player == null) return;

            Ray ray = player.eyes.HeadRay();
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, RayDistance))
            {
                Vector3 direction = ray.direction;
                Vector3 force = direction * (FlyForce / 100f);
                ApplyFlyForce(player, force);
                CreateLineEffect(player, hit.point);
            }
        }

        private void ApplyFlyForce(BasePlayer player, Vector3 force)
        {
            if (player.IsSleeping()) return;
            player.ApplyInheritedVelocity(force);
            player.SendNetworkUpdateImmediate();
            if (playerTimers.ContainsKey(player.userID))
            {
                playerTimers[player.userID].Destroy();
            }
            playerTimers[player.userID] = timer.Once(MarkerDuration, () => StopFlying(player));
        }

        private void StopFlying(BasePlayer player)
        {
            if (player == null) return;
            player.ApplyInheritedVelocity(Vector3.zero);
            player.SendNetworkUpdateImmediate();
        }

        private void CreateLineEffect(BasePlayer player, Vector3 targetPosition)
        {
            if (player == null) return;

            Vector3 startPosition = player.transform.position + new Vector3(0, 0.5f, 0);
            int segments = 10;
            Vector3 step = (targetPosition - startPosition) / segments;

            for (int i = 0; i <= segments; i++)
            {
                Vector3 effectPosition = startPosition + step * i;
                Effect.server.Run("assets/bundled/prefabs/fx/impacts/jump-land/barefoot/snow/jump-land-snow.prefab", effectPosition, Vector3.zero);
            }
        }
    }
}
