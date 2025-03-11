using System.Collections.Generic;
using UnityEngine;
namespace Oxide.Plugins
{
    [Info("SwimBoost", "sdapro", "1.3.0")]
    public class SwimBoost: RustPlugin
    {
        private
        const float SwimBoostForce = 10.0 f;
        private
        const float Cooldown = 0.1 f;
        private Dictionary < ulong, float > lastBoostTime = new Dictionary < ulong, float > ();
        private HashSet < ulong > activeBoosters = new HashSet < ulong > ();
        private float originalFlyhackHorizontalForgiveness;
        private float originalFlyhackHorizontalInertia;
        private float originalFlyhackVerticalForgiveness;
        private float originalFlyhackVerticalInertia;
        private float originalSpeedhackForgiveness;
        private float originalSpeedhackForgivenessInertia;
        private void OnServerInitialized()
        {
            originalFlyhackHorizontalForgiveness = ConVar.AntiHack.flyhack_forgiveness_horizontal;
            originalFlyhackHorizontalInertia = ConVar.AntiHack.flyhack_forgiveness_horizontal_inertia;
            originalFlyhackVerticalForgiveness = ConVar.AntiHack.flyhack_forgiveness_vertical;
            originalFlyhackVerticalInertia = ConVar.AntiHack.flyhack_forgiveness_vertical_inertia;
            originalSpeedhackForgiveness = ConVar.AntiHack.speedhack_forgiveness;
            originalSpeedhackForgivenessInertia = ConVar.AntiHack.speedhack_forgiveness_inertia;
            ConVar.AntiHack.flyhack_forgiveness_horizontal = 200 f;
            ConVar.AntiHack.flyhack_forgiveness_horizontal_inertia = 500 f;
            ConVar.AntiHack.flyhack_forgiveness_vertical = 200 f;
            ConVar.AntiHack.flyhack_forgiveness_vertical_inertia = 500 f;
            ConVar.AntiHack.speedhack_forgiveness = 200 f;
            ConVar.AntiHack.speedhack_forgiveness_inertia = 500 f;
            Puts("SwimBoost initialized and AntiHack tolerances adjusted.");
        }
        private void Unload()
        {
            ConVar.AntiHack.flyhack_forgiveness_horizontal = originalFlyhackHorizontalForgiveness;
            ConVar.AntiHack.flyhack_forgiveness_horizontal_inertia = originalFlyhackHorizontalInertia;
            ConVar.AntiHack.flyhack_forgiveness_vertical = originalFlyhackVerticalForgiveness;
            ConVar.AntiHack.flyhack_forgiveness_vertical_inertia = originalFlyhackVerticalInertia;
            ConVar.AntiHack.speedhack_forgiveness = originalSpeedhackForgiveness;
            ConVar.AntiHack.speedhack_forgiveness_inertia = originalSpeedhackForgivenessInertia;
            Puts("SwimBoost unloaded and AntiHack tolerances restored.");
        }
        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || input == null) return;
            if (!player.IsSwimming())
            {
                if (activeBoosters.Contains(player.userID))
                {
                    activeBoosters.Remove(player.userID);
                }
                StopSwimmingBoost(player);
                return;
            }
            Vector3 swimDirection = GetSwimDirection(player, input);
            if (swimDirection != Vector3.zero)
            {
                if (input.IsDown(BUTTON.SPRINT))
                {
                    if (!activeBoosters.Contains(player.userID))
                    {
                        activeBoosters.Add(player.userID);
                    }
                    if (lastBoostTime.TryGetValue(player.userID, out float lastTime) && Time.time - lastTime < Cooldown) return;
                    BoostSwimming(player, swimDirection);
                    lastBoostTime[player.userID] = Time.time;
                }
                else
                {
                    if (activeBoosters.Contains(player.userID))
                    {
                        activeBoosters.Remove(player.userID);
                    }
                    StopSwimmingBoost(player);
                }
            }
            else
            {
                StopSwimmingBoost(player);
            }
        }
        private Vector3 GetSwimDirection(BasePlayer player, InputState input)
        {
            Vector3 direction = Vector3.zero;
            if (input.IsDown(BUTTON.FORWARD)) direction += player.eyes.BodyForward();
            if (input.IsDown(BUTTON.BACKWARD)) direction -= player.eyes.BodyForward();
            if (input.IsDown(BUTTON.RIGHT)) direction += player.eyes.BodyRight();
            if (input.IsDown(BUTTON.LEFT)) direction -= player.eyes.BodyRight();
            return direction.normalized;
        }
        private void BoostSwimming(BasePlayer player, Vector3 direction)
        {
            if (player == null || player.IsSleeping()) return;
            player.PauseSpeedHackDetection(1 f);
            Vector3 force = direction * SwimBoostForce;
            player.ApplyInheritedVelocity(force);
            player.SendNetworkUpdateImmediate();
        }
        private void StopSwimmingBoost(BasePlayer player)
        {
            if (player == null) return;
            player.ApplyInheritedVelocity(Vector3.zero);
            player.SendNetworkUpdateImmediate();
        }
    }
}