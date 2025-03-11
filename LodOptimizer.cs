using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Carbon.Plugins
{
    [Info("LOD Optimizer", "Assistant", "1.0.0")]
    [Description("Optimizes Level of Detail settings for better performance")]
    internal sealed class LodOptimizer : CarbonPlugin
    {
        private Configuration config = new();
        private readonly HashSet<LODGroup> optimizedLODGroups = new();

        #region Configuration
        private sealed class Configuration
        {
            public bool EnableLODOptimization = true;
            public float BaseLODDistance = 150f;
            public float PlayerDensityMultiplier = 0.8f;
            public float MinLODDistance = 50f;
            public float MaxLODDistance = 300f;
            public float OptimizationInterval = 120f;
            public List<string> ExcludedPrefabs = new()
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
            if (config.EnableLODOptimization)
            {
                Logger.Log("LOD optimization started");
                _ = timer.Every(config.OptimizationInterval, OptimizeLODs);
            }
        }
        #endregion Lifecycle

        #region Core Logic
        private void OptimizeLODs()
        {
            try
            {
                if (!config.EnableLODOptimization)
                {
                    return;
                }

                int playerCount = BasePlayer.activePlayerList.Count;
                float densityFactor = Mathf.Clamp(
                    1 - (playerCount * config.PlayerDensityMultiplier / 100f),
                    0.2f,
                    1f
                );

                float targetDistance = Math.Clamp(
                    config.BaseLODDistance * densityFactor,
                    config.MinLODDistance,
                    config.MaxLODDistance
                );

                OptimizeAllLODGroups(targetDistance);
                Logger.Log($"Optimized LODs for {optimizedLODGroups.Count} objects");
            }
            catch (Exception ex)
            {
                Logger.Log($"LOD optimization error: {ex.Message}");
            }
        }

        private void OptimizeAllLODGroups(float targetDistance)
        {
            List<LODGroup> groups = UnityEngine.Object.FindObjectsOfType<LODGroup>()
                .Where(ShouldOptimizeLODGroup)
                .ToList();

            foreach (LODGroup? group in groups)
            {
                OptimizeLODGroup(group, targetDistance);
            }

            Plugin logger = plugins.Find<SmartLogger>();
            logger?.CallHook("OnOptimizationPerformed", Name, groups.Count);
        }

        private bool ShouldOptimizeLODGroup(LODGroup lodGroup)
        {
            if (lodGroup == null || optimizedLODGroups.Contains(lodGroup))
            {
                return false;
            }

            BaseEntity parentEntity = lodGroup.GetComponentInParent<BaseEntity>();
            return parentEntity == null ||
                   !config.ExcludedPrefabs.Exists(prefab =>
                       parentEntity.PrefabName?.StartsWith(prefab, StringComparison.OrdinalIgnoreCase) == true);
        }

        private void OptimizeLODGroup(LODGroup lodGroup, float targetDistance)
        {
            try
            {
                LOD[] lods = lodGroup.GetLODs();
                if (lods.Length < 2)
                {
                    return;
                }

                // Adjust LOD distances based on target
                for (int i = 0; i < lods.Length; i++)
                {
                    float distance = targetDistance * (1 - (i / (float)lods.Length));
                    lods[i].screenRelativeTransitionHeight =
                        CalculateTransitionHeight(distance, lodGroup.size);
                }

                lodGroup.SetLODs(lods);
                lodGroup.RecalculateBounds();
                _ = optimizedLODGroups.Add(lodGroup);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error optimizing LOD group: {ex.Message}");
            }
        }

        private float CalculateTransitionHeight(float distance, float lodGroupSize)
        {
            // Simplified calculation based on group size and target distance
            return Mathf.Clamp(
                distance / (lodGroupSize * 10f) * 0.1f,
                0.01f,
                0.5f
            );
        }
        #endregion Core Logic

        #region Commands
        [ChatCommand("lodoptimize")]
        private void CmdLODOptimize(BasePlayer player, string command, string[] args)
        {
            if (player?.IsAdmin != true)
            {
                player?.ChatMessage("Admin permission required");
                return;
            }

            OptimizeLODs();
            player.ChatMessage($"LODs optimized. Current distance: {config.BaseLODDistance}m");
        }
        #endregion Commands

        public override void IUnload()
        {
            optimizedLODGroups.Clear();
            base.IUnload();
        }
    }
}