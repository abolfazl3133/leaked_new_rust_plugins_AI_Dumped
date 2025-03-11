using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Carbon.Plugins
{
    [Info("SmartFPS", "Assistant", "1.1.0")]
    [Description("Comprehensive performance optimization suite")]
    internal sealed class SmartFps : CarbonPlugin
    {
        #region Configuration
        private Configuration config = new();

        private sealed class Configuration
        {
            /// <summary>
            /// Entity Optimization
            /// </summary>
            public bool EnableEntityOptimization = true;
            public float EntityCleanupRadius = 200f;
            public List<string> CriticalPrefabs = new()
            {
                "assets/prefabs/deployable/quarry/",
                "assets/prefabs/deployable/furnace",
                "assets/prefabs/building/",
                "assets/prefabs/deployable/oil jack/",
                "assets/prefabs/npc/",
                "assets/prefabs/player/player.prefab"
            };

            /// <summary>
            /// Network Optimization
            /// </summary>
            public bool EnableNetworkOptimization = true;
            public int MaxUpdatesPerSecond = 15;
            public float NetworkOptimizationInterval = 30f;
            public List<string> CriticalNetworkPrefabs = new()
            {
                "assets/prefabs/player/player.prefab",
                "assets/prefabs/npc/",
                "assets/prefabs/vehicle/"
            };

            /// <summary>
            /// LOD Optimization
            /// </summary>
            public bool EnableLODOptimization = true;
            public float BaseLODDistance = 150f;
            public float LODPlayerDensityMultiplier = 0.8f;
            public float MinLODDistance = 50f;
            public float MaxLODDistance = 300f;

            /// <summary>
            /// Memory Optimization
            /// </summary>
            public bool EnableMemoryOptimization = true;
        }
        #endregion Configuration

        #region State Trackers
        private readonly HashSet<BaseEntity> optimizedEntities = new();
        private readonly Queue<BaseEntity> entityQueue = new();
        private readonly HashSet<LODGroup> optimizedLODs = new();
        private readonly Dictionary<ulong, DateTime> playerActivity = new();
        private readonly Dictionary<string, int> _optimizationStats = new();
        private DateTime _lastReport = DateTime.UtcNow;
        #endregion State Trackers

        #region Lifecycle
        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
        }

        protected override void LoadConfig()
        {
            config = Config.ReadObject<Configuration>() ?? new();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private void OnServerInitialized(bool initial)
        {
            InitializeOptimizers();
            Logger.Log("SmartFPS initialized with all optimization modules");
        }
        #endregion Lifecycle

        #region Core Initialization
        private void InitializeOptimizers()
        {
            if (config.EnableEntityOptimization)
            {
                _ = timer.Every(300f, ProcessEntities);
            }

            if (config.EnableNetworkOptimization)
            {
                _ = timer.Every(config.NetworkOptimizationInterval, OptimizeNetwork);
            }

            if (config.EnableLODOptimization)
            {
                _ = timer.Every(120f, OptimizeLODs);
            }

            if (config.EnableMemoryOptimization)
            {
                _ = timer.Every(900f, OptimizeMemory);
            }

            Logger.Log("SmartFPS initialized with safe optimization modules");
        }
        #endregion Core Initialization

        #region Entity Optimization
        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity is BaseEntity baseEntity && !IsCriticalEntity(baseEntity))
            {
                entityQueue.Enqueue(baseEntity);
                CheckAndHandleInvalidPosition(baseEntity);
            }
        }

        private void ProcessEntities()
        {
            if (!config.EnableEntityOptimization)
            {
                return;
            }

            int processed = 0;
            for (int i = 0; i < 100 && entityQueue.Count > 0; i++)
            {
                if (OptimizeEntity(entityQueue.Dequeue()))
                {
                    processed++;
                }
            }

            if (processed > 0)
            {
                Logger.Log($"Optimized {processed} entities. Remaining: {entityQueue.Count}");
            }
        }

        private bool OptimizeEntity(BaseEntity entity)
        {
            try
            {
                if (entity?.IsDestroyed != false || optimizedEntities.Contains(entity))
                {
                    return false;
                }

                _ = optimizedEntities.Add(entity);

                if ((entity is IOEntity || entity is StorageContainer) && IsFarFromPlayers(entity))
                {
                    entity.SetFlag(BaseEntity.Flags.Disabled, true);
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Entity error: {ex.Message}");
                return false;
            }
        }

        private void CheckAndHandleInvalidPosition(BaseEntity entity)
        {
            const float INVALID_Y = -500f;
            if (entity?.transform.position.y < INVALID_Y)
            {
                NextFrame(() => entity?.Kill());
                Logger.Log($"Destroyed invalid entity at Y={entity.transform.position.y}");
            }
        }
        #endregion Entity Optimization

        #region Network Optimization
        private void OptimizeNetwork()
        {
            if (!config.EnableNetworkOptimization)
            {
                return;
            }

            try
            {
                AdjustNetworkRate();
                ThrottleEntityUpdates();
                TrackOptimization("Network", 1);
            }
            catch (Exception ex)
            {
                Logger.Log($"Network error: {ex.Message}");
            }
        }

        private void AdjustNetworkRate()
        {
            try
            {
                // Only use commands confirmed to work
                _ = ConsoleSystem.Run(ConsoleSystem.Option.Server, "server.tickrate", "30");

                // Skip commands that don't exist
                // ConsoleSystem.Run(ConsoleSystem.Option.Server, "culling.entitymaxdist", "500");
                // ConsoleSystem.Run(ConsoleSystem.Option.Server, "vis.lerp", "0");

                // Add alternative optimization through direct Unity settings
                QualitySettings.shadowDistance = 100f;
                QualitySettings.lodBias = 0.8f;

                TrackOptimization("Network", 1);
                Logger.Log("Network settings optimized with server.tickrate");
            }
            catch (Exception ex)
            {
                Logger.Log($"Network adjustment error: {ex.Message}");
            }
        }

        private void ThrottleEntityUpdates()
        {
            List<BaseNetworkable> entities = BaseNetworkable.serverEntities
                .Where(ShouldThrottleEntity)
                .Take(50)
                .ToList();

            foreach (BaseNetworkable? entity in entities)
            {
                entity.UpdateNetworkGroup();
            }
        }
        #endregion Network Optimization

        #region LOD Optimization
        private void OptimizeLODs()
        {
            if (!config.EnableLODOptimization)
            {
                return;
            }

            try
            {
                float targetDistance = CalculateLODDistance();
                OptimizeLODGroups(targetDistance);
            }
            catch (Exception ex)
            {
                Logger.Log($"LOD error: {ex.Message}");
            }
        }

        private float CalculateLODDistance()
        {
            int playerCount = BasePlayer.activePlayerList.Count;
            float densityFactor = Mathf.Clamp(
                1 - (playerCount * config.LODPlayerDensityMultiplier / 100f),
                0.2f,
                1f
            );

            return Math.Clamp(
                config.BaseLODDistance * densityFactor,
                config.MinLODDistance,
                config.MaxLODDistance
            );
        }

        private void OptimizeLODGroups(float targetDistance)
        {
            List<LODGroup> groups = UnityEngine.Object.FindObjectsOfType<LODGroup>()
                .Where(ShouldOptimizeLOD)
                .ToList();

            foreach (LODGroup? group in groups)
            {
                AdjustLODGroup(group, targetDistance);
            }
        }

        private void AdjustLODGroup(LODGroup lodGroup, float targetDistance)
        {
            try
            {
                if (lodGroup == null)
                {
                    return;
                }

                LOD[] lods = lodGroup.GetLODs();
                if (lods.Length < 2)
                {
                    return;
                }

                for (int i = 0; i < lods.Length; i++)
                {
                    float distance = targetDistance * (1 - (i / (float)lods.Length));
                    lods[i].screenRelativeTransitionHeight = CalculateLODTransition(distance, lodGroup.size);
                }

                lodGroup.SetLODs(lods);
                lodGroup.RecalculateBounds();
                _ = optimizedLODs.Add(lodGroup);
            }
            catch (Exception ex)
            {
                Logger.Log($"LOD adjustment error: {ex.Message}");
            }
        }

        private float CalculateLODTransition(float distance, float size)
        {
            return Mathf.Clamp(
                distance / (size * 10f) * 0.1f,
                0.01f,
                0.5f
            );
        }
        #endregion LOD Optimization

        #region Memory Optimization
        private void OptimizeMemory()
        {
            if (!config.EnableMemoryOptimization)
            {
                return;
            }

            try
            {
                // Don't force GC - let it happen naturally
                // GC.Collect();

                // Use gentle asset unloading
                _ = Resources.UnloadUnusedAssets();

                // Log memory usage
                long memoryUsed = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024;
                Logger.Log($"Memory optimized. Current usage: {memoryUsed}MB");

                TrackOptimization("Memory", 1);
            }
            catch (Exception ex)
            {
                Logger.Log($"Memory optimization error: {ex.Message}");
            }
        }
        #endregion Memory Optimization

        #region Shared Helpers
        private bool IsCriticalEntity(BaseEntity entity)
        {
            return entity != null && config.CriticalPrefabs.Any(p =>
                entity.PrefabName.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsFarFromPlayers(BaseEntity entity)
        {
            return BasePlayer.activePlayerList.All(player =>
                Vector3.Distance(player.transform.position, entity.transform.position)
                >= config.EntityCleanupRadius);
        }

        private bool ShouldThrottleEntity(BaseNetworkable entity)
        {
            return entity != null &&
            !config.CriticalNetworkPrefabs.Any(p =>
                entity.PrefabName?.StartsWith(p, StringComparison.OrdinalIgnoreCase) == true);
        }

        private bool ShouldOptimizeLOD(LODGroup lod)
        {
            BaseEntity parent = lod.GetComponentInParent<BaseEntity>();
            return parent == null || !config.CriticalPrefabs.Any(p =>
                parent.PrefabName?.StartsWith(p, StringComparison.OrdinalIgnoreCase) == true);
        }

        private float GetCpuUsage()
        {
            return 50f; // Implement actual CPU monitoring logic
        }
        #endregion Shared Helpers

        #region Player Tracking
        private void OnPlayerConnected(BasePlayer player)
        {
            playerActivity[player.userID] = DateTime.UtcNow;
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            _ = playerActivity.Remove(player.userID);
        }

        [ChatCommand("fpsoptimize")]
        private void OptimizationCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                return;
            }

            ProcessEntities();
            OptimizeNetwork();
            OptimizeLODs();
            player.ChatMessage("Full optimization cycle executed");
        }
        #endregion Player Tracking

        #region Physics Optimization
        [ChatCommand("physics")]
        private void PhysicsCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                return;
            }

            _ = ConsoleSystem.Run(ConsoleSystem.Option.Server, "physics.sleepthreshold", "0.5");
            _ = ConsoleSystem.Run(ConsoleSystem.Option.Server, "physics.substeps", "3");
            player.ChatMessage("Physics optimized!");
        }
        #endregion Physics Optimization

        #region Performance Tracking
        private void TrackOptimization(string module, int count)
        {
            _optimizationStats[module] = _optimizationStats.TryGetValue(module, out int current)
                ? current + count
                : count;

            if ((DateTime.UtcNow - _lastReport).TotalMinutes >= 10)
            {
                List<string> report = new() { "=== Performance Report ===" };
                foreach (KeyValuePair<string, int> entry in _optimizationStats)
                {
                    report.Add($"{entry.Key}: {entry.Value} optimizations");
                }
                Logger.Log(string.Join("\n", report));

                _optimizationStats.Clear();
                _lastReport = DateTime.UtcNow;
            }
        }
        #endregion Performance Tracking

        private void OptimizeGraphics()
        {
            try
            {
                // Server-safe graphics commands only
                _ = ConsoleSystem.Run(ConsoleSystem.Option.Server, "server.tickrate", "30");

                // Log optimization without changing settings
                TrackOptimization("Server", 1);
                Logger.Log("Server settings optimized");
            }
            catch (Exception ex)
            {
                Logger.Log($"Server optimization error: {ex.Message}");
            }
        }
    }
}