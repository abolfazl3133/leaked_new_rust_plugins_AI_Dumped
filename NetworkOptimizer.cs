using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Carbon.Plugins
{
    [Info("Network Optimizer", "Assistant", "1.0.0")]
    [Description("Optimizes network traffic and entity updates")]
    internal sealed class NetworkOptimizer : CarbonPlugin
    {
        private Configuration config = new();
        private readonly HashSet<BaseNetworkable> optimizedEntities = new();

        #region Configuration
        private sealed class Configuration
        {
            public bool EnableNetworkOptimization = true;
            public int MaxUpdatesPerSecond = 15;
            public int MaxEntitiesPerUpdate = 50;
            public float OptimizationInterval = 30f;
            public List<string> CriticalNetworkPrefabs = new()
            {
                "assets/prefabs/player/player.prefab",
                "assets/prefabs/npc/",
                "assets/prefabs/vehicle/"
            };
        }
        #endregion Configuration

        #region Lifecycle
        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                config ??= new Configuration();
            }
            catch
            {
                Logger.Log("Error loading config, using defaults");
                config = new Configuration();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private void OnServerInitialized(bool initial)
        {
            if (config.EnableNetworkOptimization)
            {
                Logger.Log("Network optimization started");
                _ = timer.Every(config.OptimizationInterval, OptimizeNetwork);
            }
        }
        #endregion Lifecycle

        #region Core Logic
        private void OptimizeNetwork()
        {
            try
            {
                if (!config.EnableNetworkOptimization)
                {
                    return;
                }

                // Get CPU usage from MemoryOptimizer if available
                float cpuUsage = GetCpuUsage();
                int activePlayers = GetActivePlayerCount();

                AdjustNetworkRates(cpuUsage, activePlayers);
                ThrottleEntityUpdates();
            }
            catch (Exception ex)
            {
                Logger.Log($"Network optimization error: {ex.Message}");
            }
        }

        private void AdjustNetworkRates(float cpuUsage, int activePlayers)
        {
            // Replace Mathf.Clamp with System.Math.Clamp
            int targetRate = Math.Clamp(
                config.MaxUpdatesPerSecond - (int)(cpuUsage / 10),
                5,
                config.MaxUpdatesPerSecond
            );

            _ = ConsoleSystem.Run(
                ConsoleSystem.Option.Server,
                "net.rate",
                targetRate.ToString(CultureInfo.InvariantCulture)
            );
        }

        private void ThrottleEntityUpdates()
        {
            List<BaseNetworkable> entitiesToOptimize = BaseNetworkable.serverEntities
                .Where(ShouldOptimizeEntity)
                .Take(config.MaxEntitiesPerUpdate)
                .ToList();

            foreach (BaseNetworkable? entity in entitiesToOptimize)
            {
                if (entity?.IsDestroyed != false)
                {
                    continue;
                }

                _ = optimizedEntities.Add(entity);
                entity.UpdateNetworkGroup();
            }

            plugins.Find<SmartLogger>()?.CallHook("OnOptimizationPerformed", Name, entitiesToOptimize.Count);
        }
        #endregion Core Logic

        #region Helpers
        private bool ShouldOptimizeEntity(BaseNetworkable entity)
        {
            if (entity == null || optimizedEntities.Contains(entity))
            {
                return false;
            }

            // Check if entity is critical
            return !config.CriticalNetworkPrefabs.Any(prefab =>
                entity.PrefabName?.StartsWith(prefab, StringComparison.OrdinalIgnoreCase) == true);
        }

        private float GetCpuUsage()
        {
            Plugin optimizer = plugins.Find<MemoryOptimizer>();
            return optimizer?.GetCPUUsage() ?? 50f;
        }

        private int GetActivePlayerCount()
        {
            Plugin tracker = plugins.Find<PlayerTracker>();
            return BasePlayer.activePlayerList.Count(p =>
                p != null && tracker?.IsPlayerActive(p.userID));
        }
        #endregion Helpers

        #region Commands
        [ChatCommand("netoptimize")]
        private void CmdNetOptimize(BasePlayer player, string command, string[] args)
        {
            if (player?.IsAdmin != true)
            {
                player?.ChatMessage("Admin permission required");
                return;
            }

            OptimizeNetwork();
            player.ChatMessage($"Network optimized. Rates set to {config.MaxUpdatesPerSecond}/s");
        }
        #endregion Commands

        public override void IUnload()
        {
            optimizedEntities.Clear();
            base.IUnload();
        }
    }
}