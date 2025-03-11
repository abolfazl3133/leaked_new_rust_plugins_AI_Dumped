using System;
using System.Collections.Generic;

namespace Carbon.Plugins
{
    [Info("Player Tracker", "Assistant", "1.0.0")]
    [Description("Tracks player activity for optimization purposes")]
    internal sealed class PlayerTracker : CarbonPlugin
    {
        private readonly Dictionary<ulong, DateTime> _playerActivity = new();

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null)
            {
                return;
            }

            _playerActivity[player.userID] = DateTime.UtcNow;
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player == null)
            {
                return;
            }

            _ = _playerActivity.Remove(player.userID);
        }

        public void UpdateActivity(ulong userId)
        {
            if (userId == 0)
            {
                return;
            }

            _playerActivity[userId] = DateTime.UtcNow;
        }

        internal bool IsPlayerActive(ulong userId)
        {
            return _playerActivity.TryGetValue(userId, out DateTime lastActive) &&
                   (DateTime.UtcNow - lastActive).TotalMinutes < 30;
        }
    }
}