using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Wolf AI Fix", "BlackWolf", "1.0.0")]
    class WolfAIFix : RustPlugin
    {
        private Timer cleanupTimer;
        private const float CHECK_INTERVAL = 40f; // Check every 40 seconds
        private const float MIN_WOLF_DISTANCE = 50f;
        private Dictionary<ulong, float> wolfLastStateChange = new Dictionary<ulong, float>();
        private Dictionary<ulong, int> wolfRecursionCount = new Dictionary<ulong, int>();
        private readonly HashSet<string> wolfTypes = new HashSet<string> { "wolf", "wolf2" };
        private Dictionary<string, int> removedWolfCounts = new Dictionary<string, int>();
        private float lastLogTime;
        private const float LOG_INTERVAL = 300f; // Log summary every 5 minutes
        
        void OnServerInitialized()
        {
            // Start monitoring wolves
            StartMonitoring();
            lastLogTime = Time.realtimeSinceStartup;
            removedWolfCounts["wolf"] = 0;
            removedWolfCounts["wolf2"] = 0;
        }
        
        void Unload()
        {
            if (cleanupTimer != null)
            {
                cleanupTimer.Destroy();
                cleanupTimer = null;
            }
        }
        
        private void StartMonitoring()
        {
            // Destroy existing timer if it exists
            if (cleanupTimer != null)
            {
                cleanupTimer.Destroy();
                cleanupTimer = null;
            }
            
            // Create new timer for frequent checks
            cleanupTimer = timer.Every(CHECK_INTERVAL, CheckWolves);
            PrintWarning("Wolf AI monitoring started - checking every 40 seconds");
        }
        
        private bool IsWolf(BaseEntity entity)
        {
            if (entity == null) return false;
            var entityName = entity.ShortPrefabName?.ToLower();
            return entityName != null && wolfTypes.Contains(entityName);
        }
        
        private void LogRemoval(string wolfType)
        {
            removedWolfCounts[wolfType]++;
            
            // Check if it's time to log a summary
            var currentTime = Time.realtimeSinceStartup;
            if (currentTime - lastLogTime >= LOG_INTERVAL)
            {
                if (removedWolfCounts["wolf"] > 0 || removedWolfCounts["wolf2"] > 0)
                {
                    PrintWarning($"Last 5 minutes: Removed {removedWolfCounts["wolf"]} wolves and {removedWolfCounts["wolf2"]} wolf2s");
                }
                
                // Reset counters and timer
                removedWolfCounts["wolf"] = 0;
                removedWolfCounts["wolf2"] = 0;
                lastLogTime = currentTime;
            }
        }
        
        object OnEntitySpawned(BaseEntity entity)
        {
            if (!IsWolf(entity)) return null;
            
            // Check for nearby wolves on spawn
            var nearbyWolves = UnityEngine.Physics.OverlapSphere(entity.transform.position, MIN_WOLF_DISTANCE)
                .Select(c => c.GetComponentInParent<BaseEntity>())
                .Where(e => e != null && e != entity && IsWolf(e))
                .Count();
                
            if (nearbyWolves > 0)
            {
                entity.Kill();
                LogRemoval(entity.ShortPrefabName.ToLower());
                return false;
            }
            
            return null;
        }
        
        private void CheckWolves()
        {
            try
            {
                var wolves = BaseNetworkable.serverEntities
                    .Where(e => e != null && !e.IsDestroyed && IsWolf(e as BaseEntity))
                    .Cast<BaseEntity>()
                    .ToList();
                
                var problematicWolves = new HashSet<BaseEntity>();
                
                foreach (var wolf in wolves)
                {
                    if (wolf == null || wolf.IsDestroyed) continue;
                    
                    bool shouldRemove = false;
                    ulong wolfId = wolf.net.ID.Value;
                    
                    // Check for recursion issues
                    if (wolfRecursionCount.ContainsKey(wolfId))
                    {
                        int count = wolfRecursionCount[wolfId];
                        if (count > 3) // If wolf has had more than 3 recursion issues
                        {
                            shouldRemove = true;
                        }
                    }
                    
                    // Check for nearby wolves
                    if (!shouldRemove)
                    {
                        var nearbyWolves = UnityEngine.Physics.OverlapSphere(wolf.transform.position, MIN_WOLF_DISTANCE)
                            .Select(c => c.GetComponentInParent<BaseEntity>())
                            .Where(e => e != null && e != wolf && IsWolf(e))
                            .Count();
                            
                        if (nearbyWolves > 0)
                        {
                            shouldRemove = true;
                        }
                    }
                    
                    if (shouldRemove)
                    {
                        problematicWolves.Add(wolf);
                    }
                }
                
                // Remove problematic wolves
                foreach (var wolf in problematicWolves)
                {
                    if (wolf != null && !wolf.IsDestroyed)
                    {
                        wolf.Kill();
                        LogRemoval(wolf.ShortPrefabName.ToLower());
                    }
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error checking wolves: {ex.Message}");
            }
        }
        
        object OnNpcDestinationSet(BaseEntity entity)
        {
            if (!IsWolf(entity)) 
                return null;
                
            var wolfId = entity.net.ID.Value;
            var currentTime = Time.realtimeSinceStartup;
            
            // Track state changes
            if (wolfLastStateChange.ContainsKey(wolfId))
            {
                float lastChange = wolfLastStateChange[wolfId];
                if (currentTime - lastChange < 0.5f) // If state changed too quickly
                {
                    // Increment recursion counter
                    if (!wolfRecursionCount.ContainsKey(wolfId))
                        wolfRecursionCount[wolfId] = 0;
                    wolfRecursionCount[wolfId]++;
                }
            }
            
            wolfLastStateChange[wolfId] = currentTime;
            return null;
        }
    }
} 