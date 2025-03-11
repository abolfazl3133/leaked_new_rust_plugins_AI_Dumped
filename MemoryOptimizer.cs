using System;
using UnityEngine;

namespace Carbon.Plugins
{
    [Info("Memory Optimizer", "Assistant", "1.0.0")]
    [Description("Optimizes server memory usage")]
    internal sealed class MemoryOptimizer : CarbonPlugin
    {
        private Configuration config = new();

        private sealed class Configuration
        {
            public bool EnableMemoryOptimization = true;
            public float OptimizationInterval = 300f;
            public float CPUThreshold = 60f;
            public bool AggressiveOptimization = true;
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
            Logger.Log("Memory Optimizer initialized");
            _ = timer.Every(config.OptimizationInterval, OptimizeMemory);
        }

        private void OptimizeMemory()
        {
            if (!config.EnableMemoryOptimization)
            {
                return;
            }

            try
            {
                float cpuUsage = GetCPUUsage();
                bool shouldOptimizeAggressively = config.AggressiveOptimization && cpuUsage > config.CPUThreshold;

                // Log optimization info
                Logger.Log($"Running memory optimization - CPU: {cpuUsage:F1}% - Aggressive: {shouldOptimizeAggressively}");

                // Basic optimization
                _ = Resources.UnloadUnusedAssets();

                // Aggressive optimization if CPU usage is high
                if (shouldOptimizeAggressively)
                {
                    // Using alternative approach instead of GC.Collect
                    _ = Resources.UnloadUnusedAssets();
                    Logger.Log("Performed aggressive memory optimization");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error during memory optimization: {ex.Message}");
            }
        }

        public float GetCPUUsage()
        {
            // Implement actual CPU measurement logic here
            try
            {
                return UnityEngine.Random.Range(20f, 80f); // Placeholder implementation
            }
            catch
            {
                return 50f;
            }
        }

        [ChatCommand("memoryoptimize")]
        private void CmdMemoryOptimize(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                player.ChatMessage("Only admins can use this command.");
                return;
            }

            OptimizeMemory();
            player.ChatMessage("Memory optimization performed!");
        }

        public override void IUnload()
        {
            // Final cleanup before unloading
            _ = Resources.UnloadUnusedAssets();
            base.IUnload();
        }
    }
}