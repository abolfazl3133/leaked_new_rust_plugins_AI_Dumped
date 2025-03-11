using System;
using System.Collections.Generic;
using System.Globalization;

namespace Carbon.Plugins
{
    [Info("Position Checker", "Assistant", "1.0.0")]
    [Description("Checks for and fixes entities with invalid positions")]
    internal sealed class PositionChecker : CarbonPlugin
    {
        private Configuration config = new();
        private readonly HashSet<string> recentLogMessages = new();
        private DateTime lastLogCleanup = DateTime.MinValue;

        private sealed class Configuration
        {
            public float InvalidPositionThreshold = -500f;
            public bool SuppressInvalidPositionMessages;
        }

        protected override void LoadDefaultConfig()
        {
            Logger.Log("Creating new configuration file");
            config = new Configuration();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                Logger.Log("Configuration file is corrupt, creating new one");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private void OnServerInitialized(bool initial)
        {
            Logger.Log("Position Checker initialized");
            _ = timer.Every(300f, CleanupLogMessages);
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity is BaseEntity baseEntity)
            {
                CheckAndHandleInvalidPosition(baseEntity);
            }
        }

        private void CheckAndHandleInvalidPosition(BaseEntity entity)
        {
            if (entity?.IsDestroyed != false)
            {
                return;
            }

            // Skip animal entities
            if (entity is BaseNpc)
            {
                return;
            }

            if (entity.transform.position.y < config.InvalidPositionThreshold)
            {
                string entityId = entity.net?.ID.ToString()
                    ?? entity.GetInstanceID().ToString(CultureInfo.InvariantCulture);

                if (!IsRecentLogMessage(entityId) && !config.SuppressInvalidPositionMessages)
                {
                    Logger.Log($"Entity {entity.ShortPrefabName} at invalid position Y={entity.transform.position.y}, destroying");
                    _ = recentLogMessages.Add(entityId);
                }

                NextFrame(() =>
                {
                    if (entity?.IsDestroyed == false)
                    {
                        entity.Kill();
                    }
                });
            }
        }

        private bool IsRecentLogMessage(string message)
        {
            if ((DateTime.UtcNow - lastLogCleanup).TotalMinutes > 5)
            {
                recentLogMessages.Clear();
                lastLogCleanup = DateTime.UtcNow;
            }
            return recentLogMessages.Contains(message);
        }

        private void CleanupLogMessages()
        {
            recentLogMessages.Clear();
            lastLogCleanup = DateTime.UtcNow;
        }

        [ChatCommand("checkpositions")]
        private void CmdCheckPositions(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                player.ChatMessage("Only admins can use this command.");
                return;
            }

            int count = 0;
            foreach (BaseNetworkable entity in BaseNetworkable.serverEntities)
            {
                if (entity is BaseEntity baseEntity)
                {
                    CheckAndHandleInvalidPosition(baseEntity);
                    count++;
                }
            }

            player.ChatMessage($"Checked positions of {count} entities");
        }

        [ChatCommand("togglepositionmessages")]
        private void CmdTogglePositionMessages(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                player.ChatMessage("Only admins can use this command.");
                return;
            }

            config.SuppressInvalidPositionMessages = !config.SuppressInvalidPositionMessages;
            SaveConfig();

            player.ChatMessage(
                $"Invalid position messages: {(config.SuppressInvalidPositionMessages ? "Suppressed" : "Enabled")}"
            );
        }
    }
}