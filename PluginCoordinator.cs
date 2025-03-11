using System;
using System.Collections.Generic;
using System.Linq;

namespace Carbon.Plugins
{
    [Info("Plugin Coordinator", "Assistant", "1.0.0")]
    [Description("Coordinates optimization tasks across multiple plugins")]
    internal sealed class PluginCoordinator : CarbonPlugin
    {
        private Configuration config = new();
        private readonly List<Action> optimizationTasks = new();

        #region Configuration
        private sealed class Configuration
        {
            public bool EnableSmartScheduling = true;
            public float InitialDelay = 30f;
            public float MinInterval = 15f;
            public float MaxInterval = 300f;
            public float CpuThresholdForAggressive = 70f;
            public int MaxConcurrentOptimizations = 2;
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
            if (!config.EnableSmartScheduling)
            {
                return;
            }

            RegisterOptimizationTasks();
            StartOptimizationScheduler();
            Logger.Log($"Coordinating {optimizationTasks.Count} optimization tasks");
        }
        #endregion Lifecycle

        #region Core Logic
        private void RegisterOptimizationTasks()
        {
            RegisterTask<EntityOptimizer>(p => p.ProcessEntityQueue());
            RegisterTask<MemoryOptimizer>(p => p.OptimizeMemory());
            RegisterTask<NetworkOptimizer>(p => p.OptimizeNetwork());
            RegisterTask<LodOptimizer>(p => p.OptimizeLODs());
            RegisterTask<PositionChecker>(p => p.CleanupLogMessages());
        }

        private void RegisterTask<T>(Action<T> action) where T : CarbonPlugin
        {
            Plugin plugin = plugins.Find<T>();
            if (plugin != null)
            {
                optimizationTasks.Add(() => action(plugin));
            }
        }

        private void StartOptimizationScheduler()
        {
            _ = timer.Once(config.InitialDelay, () => _ = timer.Repeat(CalculateDynamicInterval(), 0, RunOptimizationCycle));
        }

        private void RunOptimizationCycle()
        {
            try
            {
                float cpuUsage = GetCpuUsage();
                int maxTasks = cpuUsage > config.CpuThresholdForAggressive
                    ? 1
                    : config.MaxConcurrentOptimizations;

                ExecuteOptimizations(maxTasks);
            }
            catch (Exception ex)
            {
                Logger.Log($"Optimization cycle error: {ex.Message}");
            }
        }

        private void ExecuteOptimizations(int maxConcurrent)
        {
            List<Action> tasksToRun = optimizationTasks
                .OrderBy(_ => Guid.NewGuid())
                .Take(maxConcurrent)
                .ToList();

            foreach (Action? task in tasksToRun)
            {
                try
                {
                    task?.Invoke();
                }
                catch (Exception ex)
                {
                    Logger.Log($"Optimization task failed: {ex.Message}");
                }
            }
        }
        #endregion Core Logic

        #region Helpers
        private float CalculateDynamicInterval()
        {
            int playerCount = BasePlayer.activePlayerList.Count;
            float cpuUsage = GetCpuUsage();

            // Calculate interval based on load
            return Math.Clamp(
                config.MaxInterval - (playerCount * 2) - (cpuUsage / 2),
                config.MinInterval,
                config.MaxInterval
            );
        }

        private float GetCpuUsage()
        {
            Plugin optimizer = plugins.Find<MemoryOptimizer>();
            return optimizer?.GetCPUUsage() ?? 50f;
        }
        #endregion Helpers

        #region Commands
        [ChatCommand("optimize")]
        private void CmdOptimize(BasePlayer player, string command, string[] args)
        {
            if (player?.IsAdmin != true)
            {
                player?.ChatMessage("Admin permission required");
                return;
            }

            RunOptimizationCycle();
            player.ChatMessage($"Ran optimization cycle ({optimizationTasks.Count} tasks)");
        }
        #endregion Commands

        public override void IUnload()
        {
            optimizationTasks.Clear();
            base.IUnload();
        }
    }
}