using System;
using System.Collections.Generic;
using Carbon.Core;
using UnityEngine;

namespace Carbon.Plugins
{
    [Info("AntiFleeSpam", "RustGPT", "2.2.2")]
    public class AntiFleeSpam : CarbonPlugin
    {
        private Dictionary<BaseNpc, bool> frozenWolves = new Dictionary<BaseNpc, bool>();
        private Dictionary<BaseNpc, Vector3> lastPositions = new Dictionary<BaseNpc, Vector3>();
        private Dictionary<BaseNpc, int> stuckCount = new Dictionary<BaseNpc, int>();
        private Dictionary<BaseNpc, int> recursionCount = new Dictionary<BaseNpc, int>();

        private const float CheckInterval = 15f; // Check wolves every 15 seconds
        private const float CleanupInterval = 45f; // Remove stuck animals every 45 seconds
        private const int MaxStuckCount = 3; // Number of checks before considering an animal stuck
        private const int MaxRecursionCount = 5; // Max recursion count before stopping

        private void OnServerInitialized(bool initial)
        {
            timer.Every(CheckInterval, CheckNearbyPlayers);
            timer.Every(CleanupInterval, RemoveStuckAnimals);
            timer.Every(120f, FixInvalidCorpsePositions); // Clean up corpses every 2 minutes
        }

        /// <summary>
        /// Freezes wolves unless a player is nearby.
        /// </summary>
        private void CheckNearbyPlayers()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is BaseNpc npc && npc.ShortPrefabName.Contains("wolf"))
                {
                    HandleFSMTransitions(npc);
                    
                    bool hasNearbyPlayer = false;
                    
                    foreach (var player in BasePlayer.activePlayerList)
                    {
                        if (player == null || player.IsSleeping()) continue;

                        float distance = Vector3.Distance(npc.transform.position, player.transform.position);
                        if (distance <= 40f) // Wolves wake up at 40m
                        {
                            hasNearbyPlayer = true;
                            break;
                        }
                    }

                    if (hasNearbyPlayer)
                    {
                        if (frozenWolves.ContainsKey(npc) && frozenWolves[npc])
                        {
                            npc.SetFact(BaseNpc.Facts.Speed, 1, true); // Unfreeze wolf
                            frozenWolves[npc] = false;
                        }
                    }
                    else
                    {
                        if (!frozenWolves.ContainsKey(npc) || !frozenWolves[npc])
                        {
                            npc.SetFact(BaseNpc.Facts.Speed, 0, true); // Freeze wolf
                            frozenWolves[npc] = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handles FSM transitions to prevent endless recursion.
        /// </summary>
        private void HandleFSMTransitions(BaseNpc npc)
        {
            if (!recursionCount.ContainsKey(npc))
            {
                recursionCount[npc] = 0;
            }

            if (npc.currentState != BaseNpc.FSMState.Flee && npc.currentState != BaseNpc.FSMState.Roam)
            {
                recursionCount[npc] = 0;
                return;
            }

            recursionCount[npc]++;
            if (recursionCount[npc] > MaxRecursionCount)
            {
                npc.SetFact(BaseNpc.Facts.Speed, 0, true);
                frozenWolves[npc] = true;
                recursionCount[npc] = 0;
                Puts($"Prevented endless recursion on {npc.ShortPrefabName}[{npc.net.ID}]");
                
                npc.SetCurrentState(BaseNpc.FSMState.Sleep);
            }
        }

        /// <summary>
        /// Removes stuck animals quietly (no spam).
        /// </summary>
        private void RemoveStuckAnimals()
        {
            List<BaseNpc> toRemove = new List<BaseNpc>();

            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is BaseNpc ai)
                {
                    if (!ai.ShortPrefabName.Contains("wolf") &&
                        !ai.ShortPrefabName.Contains("bear") &&
                        !ai.ShortPrefabName.Contains("boar") &&
                        !ai.ShortPrefabName.Contains("stag") &&
                        !ai.ShortPrefabName.Contains("chicken") &&
                        !ai.ShortPrefabName.Contains("horse"))
                        continue;

                    if (!lastPositions.ContainsKey(ai))
                    {
                        lastPositions[ai] = ai.transform.position;
                        stuckCount[ai] = 0;
                        continue;
                    }

                    if (lastPositions[ai] == ai.transform.position)
                    {
                        stuckCount[ai]++;
                    }
                    else
                    {
                        stuckCount[ai] = 0;
                    }

                    lastPositions[ai] = ai.transform.position;

                    if (stuckCount[ai] >= MaxStuckCount)
                    {
                        toRemove.Add(ai);
                    }
                }
            }

            foreach (var npc in toRemove)
            {
                npc.Kill();
                lastPositions.Remove(npc);
                stuckCount.Remove(npc);
            }
        }

        /// <summary>
        /// Quietly removes glitched corpses every 2 minutes.
        /// </summary>
        private void FixInvalidCorpsePositions()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is BaseCorpse corpse)
                {
                    if (corpse.transform.position.y < -100) // Corpses below -100 Y are deleted
                    {
                        corpse.Kill();
                    }
                }
            }
        }

        private void Unload()
        {
            foreach (var npc in frozenWolves.Keys)
            {
                if (npc != null && !npc.IsDestroyed)
                {
                    npc.SetFact(BaseNpc.Facts.Speed, 1, true); // Unfreeze all wolves when unloading
                }
            }
            frozenWolves.Clear();
            lastPositions.Clear();
            stuckCount.Clear();
            recursionCount.Clear();
        }
    }
}
