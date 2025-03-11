using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using Oxide.Core.Libraries;
using Time = UnityEngine.Time;

namespace Oxide.Plugins
{
    [Info("Raid Block", "BlackWolf", "1.0.0")]
    public class RaidBlock : RustPlugin
    {
        #region Fields
        private const string PermissionAdmin = "raidblock.admin";
        private const float RAID_BLOCK_TIME = 120f; // 2 minutes in seconds
        private Dictionary<ulong, Timer> raidBlockTimers = new();
        private Dictionary<ulong, string> activeCui = new();
        private Dictionary<ulong, float> blockStartTimes = new();
        
        // Configuration
        private Configuration config;

        // Lists of items that trigger raid block
        private readonly HashSet<string> raidItems = new HashSet<string>
        {
            "explosive.timed", // C4
            "ammo.rocket.basic", // Regular Rocket
            "ammo.rocket.fire", // Incendiary Rocket
            "ammo.rocket.hv", // High Velocity Rocket
            "ammo.rocket.smoke", // Smoke Rocket
            "grenade.f1", // F1 Grenade
            "grenade.beancan", // Beancan Grenade
            "grenade.satchel", // Satchel Charge
            "ammo.rifle.explosive", // Explosive Ammo
            "surveycharge", // Survey Charge
            "explosive.underwater", // Underwater Explosive
            "ammo.shotgun", // Shotgun Ammo
            "ammo.handmade.shell", // Handmade Shells
            "ammo.rifle", // Rifle Ammo
            "ammo.pistol" // Pistol Ammo
        };

        private readonly HashSet<string> raidTools = new HashSet<string>
        {
            "hammer.salvaged",
            "axe.salvaged",
            "pickaxe",
            "jackhammer",
            "explosive.timed.underwater",
            "chainsaw",
            "hammer",
            "icepick.salvaged",
            "axe",
            "mace",
            "machete",
            "salvaged.sword",
            "longsword",
            "spear.stone",
            "spear.wooden"
        };
        #endregion

        #region Configuration
        private class Configuration
        {
            [JsonProperty("Enable sound effects")]
            public bool EnableSoundEffects = true;

            [JsonProperty("Block message color (hex)")]
            public string MessageColor = "#FF4444";

            [JsonProperty("Timer color (hex)")]
            public string TimerColor = "#FFFFFF";

            [JsonProperty("Show block message to nearby players")]
            public bool ShowToNearbyPlayers = true;

            [JsonProperty("Nearby players notification range")]
            public float NotificationRange = 30f;

            [JsonProperty("Block combat between raid blocked players")]
            public bool BlockPvPBetweenBlocked = true;

            public static Configuration DefaultConfig()
            {
                return new Configuration();
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintError("Configuration file is corrupt! Loading default config...");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = Configuration.DefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        #endregion

        #region Plugin Lifecycle
        private void Init()
        {
            // Register permissions
            permission.RegisterPermission(PermissionAdmin, this);
            
            // Register commands
            cmd.AddChatCommand("raidblock", this, CmdRaidBlock);
            
            // Clean up any existing data
            raidBlockTimers.Clear();
            activeCui.Clear();
        }

        private void OnServerInitialized()
        {
            // Load configuration
            LoadConfig();
        }

        private void Unload()
        {
            // Clean up timers and CUI
            foreach (var timer in raidBlockTimers.Values)
                timer?.Destroy();
            
            foreach (var player in BasePlayer.activePlayerList)
                DestroyUI(player);
        }
        #endregion

        #region Combat Events
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            try
            {
                // Check if it's building damage
                bool isRaidableEntity = entity is BuildingBlock || 
                                      entity is Door || 
                                      entity is AutoTurret || 
                                      entity is StorageContainer ||
                                      entity is BuildingPrivlidge ||
                                      entity is BaseOven ||
                                      entity is LootContainer;
                
                if (!isRaidableEntity) return;
                
                // Get the attacker
                BasePlayer attacker = info?.InitiatorPlayer;
                if (attacker == null) return;

                // Check damage type
                bool isRaidDamage = false;

                // Check weapon/tool
                Item weapon = attacker.GetActiveItem();
                if (weapon != null)
                {
                    isRaidDamage = raidItems.Contains(weapon.info.shortname) ||
                                  raidTools.Contains(weapon.info.shortname);

                    // If using a tool on a building, it's raid damage
                    if (!isRaidDamage && entity is BuildingBlock && raidTools.Contains(weapon.info.shortname))
                    {
                        isRaidDamage = true;
                    }
                }

                // Check damage types
                if (!isRaidDamage && info.damageTypes != null)
                {
                    isRaidDamage = info.damageTypes.Has(Rust.DamageType.Explosion) ||
                                  info.damageTypes.Has(Rust.DamageType.AntiVehicle) ||
                                  info.damageTypes.Has(Rust.DamageType.Bullet) ||
                                  info.damageTypes.Has(Rust.DamageType.Blunt) ||
                                  info.damageTypes.Has(Rust.DamageType.Slash) ||
                                  info.damageTypes.Has(Rust.DamageType.Stab);
                }

                if (!isRaidDamage) return;

                ApplyRaidBlockToPlayer(attacker);
            }
            catch (Exception ex)
            {
                PrintError($"Error in OnEntityTakeDamage: {ex.Message}");
            }
        }

        // Handle explosive ammo and rockets
        private void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod, ProtoBuf.ProjectileShoot projectileShoot)
        {
            try
            {
                if (player == null || projectile == null) return;

                var heldItem = player.GetActiveItem();
                if (heldItem?.info == null) return;

                // Check if the weapon uses raid ammo
                var ammoType = heldItem.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine?.ammoType;
                if (ammoType == null) return;

                if (raidItems.Contains(ammoType.shortname))
                {
                    ApplyRaidBlockToPlayer(player);
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error in OnWeaponFired: {ex.Message}");
            }
        }

        // Handle thrown explosives (Satchels, Beancans, etc.)
        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity, ThrownWeapon weapon)
        {
            try
            {
                if (player == null || entity == null) return;

                string itemName = entity.ShortPrefabName;
                if (raidItems.Contains(itemName))
                {
                    ApplyRaidBlockToPlayer(player);
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error in OnExplosiveThrown: {ex.Message}");
            }
        }

        // Handle C4 placement
        private void OnExplosiveDropped(BasePlayer player, BaseEntity entity, ThrownWeapon weapon)
        {
            try
            {
                if (player == null || entity == null) return;

                if (entity is TimedExplosive)
                {
                    ApplyRaidBlockToPlayer(player);
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error in OnExplosiveDropped: {ex.Message}");
            }
        }

        // Helper method to apply raid block
        private void ApplyRaidBlockToPlayer(BasePlayer player)
        {
            // Check if player is already raid blocked
            if (IsRaidBlocked(player.userID))
            {
                // Refresh the timer
                RefreshRaidBlock(player);
                return;
            }

            // Apply raid block
            ApplyRaidBlock(player);

            // Notify nearby players if enabled
            if (config.ShowToNearbyPlayers)
                NotifyNearbyPlayers(player);

            // Play sound if enabled
            if (config.EnableSoundEffects)
                player.Command("note.raid", 1, 1, 0);
        }

        private object OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            try
            {
                if (!config.BlockPvPBetweenBlocked) return null;

                // Check if both players are raid blocked
                if (info?.HitEntity is BasePlayer victim)
                {
                    if (IsRaidBlocked(attacker.userID) && IsRaidBlocked(victim.userID))
                    {
                        SendReply(attacker, "You cannot PvP while raid blocked!");
                        return true; // Cancel the attack
                    }
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error in OnPlayerAttack: {ex.Message}");
            }
            return null;
        }
        #endregion

        #region Helper Methods
        private void ApplyRaidBlock(BasePlayer player)
        {
            try
            {
                ulong userId = player.userID;

                // Initialize dictionaries if needed
                if (!raidBlockTimers.ContainsKey(userId))
                    raidBlockTimers[userId] = null;
                if (!blockStartTimes.ContainsKey(userId))
                    blockStartTimes[userId] = 0f;

                // Destroy existing timer if any
                raidBlockTimers[userId]?.Destroy();

                // Store start time
                blockStartTimes[userId] = Time.realtimeSinceStartup;

                // Create new timer
                raidBlockTimers[userId] = timer.Once(RAID_BLOCK_TIME, () => RemoveRaidBlock(player));

                // Show UI
                CreateUI(player, RAID_BLOCK_TIME);

                // Notify player
                SendReply(player, $"You are now raid blocked for {RAID_BLOCK_TIME} seconds!");
            }
            catch (Exception ex)
            {
                PrintError($"Error in ApplyRaidBlock: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }

        private void RefreshRaidBlock(BasePlayer player)
        {
            try
            {
                ulong userId = player.userID;

                // Initialize dictionaries if needed
                if (!raidBlockTimers.ContainsKey(userId))
                    raidBlockTimers[userId] = null;
                if (!blockStartTimes.ContainsKey(userId))
                    blockStartTimes[userId] = 0f;

                // Reset timer
                raidBlockTimers[userId]?.Destroy();
                blockStartTimes[userId] = Time.realtimeSinceStartup;
                raidBlockTimers[userId] = timer.Once(RAID_BLOCK_TIME, () => RemoveRaidBlock(player));

                // Update UI
                CreateUI(player, RAID_BLOCK_TIME);
            }
            catch (Exception ex)
            {
                PrintError($"Error in RefreshRaidBlock: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }

        private void RemoveRaidBlock(BasePlayer player)
        {
            try
            {
                ulong userId = player.userID;

                // Clean up
                if (raidBlockTimers.ContainsKey(userId))
                {
                    raidBlockTimers[userId]?.Destroy();
                    raidBlockTimers.Remove(userId);
                }
                
                if (blockStartTimes.ContainsKey(userId))
                    blockStartTimes.Remove(userId);

                DestroyUI(player);

                // Notify player
                if (player != null && player.IsConnected)
                {
                    SendReply(player, "Your raid block has expired!");

                    // Play sound if enabled
                    if (config.EnableSoundEffects)
                        player.Command("note.raid", 1, 0.5f, 0);
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error in RemoveRaidBlock: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }

        private bool IsRaidBlocked(ulong userId)
        {
            return raidBlockTimers.ContainsKey(userId) && raidBlockTimers[userId] != null;
        }

        private float GetRemainingBlockTime(ulong userId)
        {
            if (!blockStartTimes.ContainsKey(userId)) return 0f;
            float elapsed = Time.realtimeSinceStartup - blockStartTimes[userId];
            return Math.Max(0f, RAID_BLOCK_TIME - elapsed);
        }

        private void NotifyNearbyPlayers(BasePlayer attacker)
        {
            try
            {
                var nearbyPlayers = new List<BasePlayer>();
                var entities = new List<BaseEntity>();
                Vis.Entities(attacker.transform.position, config.NotificationRange, entities);
                
                foreach (var entity in entities)
                {
                    if (entity is BasePlayer player && player != attacker && player.IsConnected)
                        nearbyPlayers.Add(player);
                }

                foreach (var player in nearbyPlayers)
                    SendReply(player, $"{attacker.displayName} has started raiding nearby!");
            }
            catch (Exception ex)
            {
                PrintError($"Error in NotifyNearbyPlayers: {ex.Message}");
            }
        }
        #endregion

        #region UI Methods
        private void CreateUI(BasePlayer player, float duration)
        {
            DestroyUI(player);

            var elements = new CuiElementContainer();

            // Main panel - centered at top
            elements.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.35 0.85", AnchorMax = "0.65 0.95" },
                CursorEnabled = false
            }, "Overlay", "RaidBlock_UI");

            // Background panel with gradient
            elements.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.9", Material = "assets/content/ui/uibackgroundblur-ingame.mat" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, "RaidBlock_UI", "RaidBlock_BG");

            // Red warning bar at top
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.8 0 0 0.8" },
                RectTransform = { AnchorMin = "0 0.8", AnchorMax = "1 1" }
            }, "RaidBlock_UI");

            // Warning icon (⚠)
            elements.Add(new CuiLabel
            {
                Text = 
                {
                    Text = "⚠",
                    FontSize = 20,
                    Font = "robotocondensed-bold.ttf",
                    Color = "1 1 1 1",
                    Align = TextAnchor.MiddleCenter
                },
                RectTransform = { AnchorMin = "0 0.8", AnchorMax = "0.2 1" }
            }, "RaidBlock_UI");

            // Raid block text
            elements.Add(new CuiLabel
            {
                Text = 
                {
                    Text = "RAID BLOCKED",
                    FontSize = 24,
                    Font = "robotocondensed-bold.ttf",
                    Color = "1 0 0 1",
                    Align = TextAnchor.MiddleCenter
                },
                RectTransform = { AnchorMin = "0.2 0.8", AnchorMax = "0.8 1" }
            }, "RaidBlock_UI");

            // Timer text with glow
            elements.Add(new CuiLabel
            {
                Text = 
                {
                    Text = $"{duration:F0}s",
                    FontSize = 32,
                    Font = "robotocondensed-bold.ttf",
                    Color = "1 1 1 1",
                    Align = TextAnchor.MiddleCenter
                },
                RectTransform = { AnchorMin = "0 0.2", AnchorMax = "1 0.8" }
            }, "RaidBlock_UI", "RaidBlock_Timer");

            // Bottom text
            elements.Add(new CuiLabel
            {
                Text = 
                {
                    Text = "NO PVP ALLOWED",
                    FontSize = 16,
                    Font = "robotocondensed-regular.ttf",
                    Color = "1 0.3 0.3 1",
                    Align = TextAnchor.MiddleCenter
                },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.2" }
            }, "RaidBlock_UI");

            // Store and send UI
            var json = CuiHelper.ToJson(elements);
            activeCui[player.userID] = json;
            CuiHelper.AddUi(player, json);

            // Start timer updates
            timer.Every(1f, () =>
            {
                if (!player.IsConnected || !IsRaidBlocked(player.userID))
                {
                    DestroyUI(player);
                    return;
                }

                float remaining = GetRemainingBlockTime(player.userID);
                UpdateTimerText(player, remaining);
            });
        }

        private void UpdateTimerText(BasePlayer player, float remaining)
        {
            var elements = new CuiElementContainer();
            elements.Add(new CuiLabel
            {
                Text = 
                {
                    Text = $"{remaining:F0}s",
                    FontSize = 32,
                    Font = "robotocondensed-bold.ttf",
                    Color = "1 1 1 1",
                    Align = TextAnchor.MiddleCenter
                },
                RectTransform = { AnchorMin = "0 0.2", AnchorMax = "1 0.8" }
            }, "RaidBlock_UI", "RaidBlock_Timer");

            CuiHelper.DestroyUi(player, "RaidBlock_Timer");
            CuiHelper.AddUi(player, CuiHelper.ToJson(elements));
        }

        private void DestroyUI(BasePlayer player)
        {
            if (activeCui.ContainsKey(player.userID))
            {
                CuiHelper.DestroyUi(player, "RaidBlock_UI");
                activeCui.Remove(player.userID);
            }
        }
        #endregion

        #region Commands
        private void CmdRaidBlock(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionAdmin))
            {
                SendReply(player, "You don't have permission to use this command!");
                return;
            }

            if (args.Length == 0)
            {
                SendReply(player, "Usage: /raidblock <check/clear> [player name]");
                return;
            }

            switch (args[0].ToLower())
            {
                case "check":
                    if (args.Length < 2)
                    {
                        SendReply(player, "Usage: /raidblock check <player name>");
                        return;
                    }
                    var targetPlayer = BasePlayer.Find(args[1]);
                    if (targetPlayer == null)
                    {
                        SendReply(player, "Player not found!");
                        return;
                    }
                    SendReply(player, IsRaidBlocked(targetPlayer.userID) 
                        ? $"{targetPlayer.displayName} is raid blocked!" 
                        : $"{targetPlayer.displayName} is not raid blocked.");
                    break;

                case "clear":
                    if (args.Length < 2)
                    {
                        SendReply(player, "Usage: /raidblock clear <player name>");
                        return;
                    }
                    var clearPlayer = BasePlayer.Find(args[1]);
                    if (clearPlayer == null)
                    {
                        SendReply(player, "Player not found!");
                        return;
                    }
                    if (IsRaidBlocked(clearPlayer.userID))
                    {
                        RemoveRaidBlock(clearPlayer);
                        SendReply(player, $"Removed raid block from {clearPlayer.displayName}");
                    }
                    else
                    {
                        SendReply(player, $"{clearPlayer.displayName} is not raid blocked!");
                    }
                    break;

                default:
                    SendReply(player, "Usage: /raidblock <check/clear> [player name]");
                    break;
            }
        }
        #endregion
    }
} 