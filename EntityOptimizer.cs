using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Carbon.Plugins
{
    [Info("Entity Optimizer", "Assistant", "1.0.0")]
    [Description("Optimizes entities to improve server performance")]
    internal sealed class EntityOptimizer : CarbonPlugin
    {
        private Configuration config = new();
        private readonly HashSet<BaseEntity> optimizedEntities = new();
        private readonly Queue<BaseEntity> entityOptimizationQueue = new();

        private sealed class Configuration
        {
            public bool EnableEntityOptimization = true;
            public float CleanupRadius = 200f;
            public List<string> CriticalPrefabs = new()
            {
                "assets/prefabs/deployable/quarry/",
                "assets/prefabs/deployable/furnace",
                "assets/prefabs/building/",
                "assets/prefabs/deployable/oil jack/",
                "assets/prefabs/npc/",
                "assets/prefabs/player/player.prefab"
            };
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
            Logger.Log("Entity Optimizer initialized");
            _ = timer.Every(60f, ProcessEntityQueue);
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity == null)
            {
                return;
            }

            if (entity is BaseEntity baseEntity && !IsCriticalEntity(baseEntity))
            {
                // Add to optimization queue
                entityOptimizationQueue.Enqueue(baseEntity);
            }
        }

        internal void ProcessEntityQueue()
        {
            if (!config.EnableEntityOptimization)
            {
                return;
            }

            int optimizedCount = 0;
            int processCount = Math.Min(entityOptimizationQueue.Count, 100);

            for (int i = 0; i < processCount; i++)
            {
                if (entityOptimizationQueue.Count == 0)
                {
                    break;
                }

                BaseEntity entity = entityOptimizationQueue.Dequeue();
                if (OptimizeEntity(entity))
                {
                    optimizedCount++;
                }
            }

            if (optimizedCount > 0)
            {
                Plugin logger = plugins.Find<SmartLogger>();
                logger?.CallHook("OnOptimizationPerformed", Name, optimizedCount);
                Logger.Log($"Optimized {optimizedCount} entities. Queue size: {entityOptimizationQueue.Count}");
            }
        }

        private bool OptimizeEntity(BaseEntity entity)
        {
            try
            {
                if (entity?.IsDestroyed != false)
                {
                    return false;
                }

                // Skip if already optimized
                if (optimizedEntities.Contains(entity))
                {
                    return false;
                }

                // Add to optimized set
                _ = optimizedEntities.Add(entity);

                // Disable thinking for certain entities when far from players
                if ((entity is IOEntity || entity is StorageContainer) && IsEntityFarFromPlayers(entity))
                {
                    entity.SetFlag(BaseEntity.Flags.Disabled, true);
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error optimizing entity {entity.ShortPrefabName}: {ex.Message}");
                return false;
            }
        }

        private bool IsCriticalEntity(BaseEntity entity)
        {
            return entity == null
|| config.CriticalPrefabs.Any(p =>
                entity.PrefabName.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsEntityFarFromPlayers(BaseEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                float distance = Vector3.Distance(player.transform.position, entity.transform.position);
                if (distance < config.CleanupRadius)
                {
                    return false;
                }
            }

            return true;
        }

        [ChatCommand("entityoptimize")]
        private void CmdEntityOptimize(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                player.ChatMessage("Only admins can use this command.");
                return;
            }

            ProcessEntityQueue();
            player.ChatMessage("Entity optimization started!");
        }

        public override void IUnload()
        {
            // Clean up
            optimizedEntities.Clear();
            entityOptimizationQueue.Clear();
            base.IUnload();
        }
    }
}