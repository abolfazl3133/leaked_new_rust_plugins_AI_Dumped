using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;
using Rust;
using Oxide.Game.Rust.Cui;
using System;
using UnityEngine.AI;

namespace Oxide.Plugins
{
    [Info("Chaos Mode", "YourName", "1.0.0")]
    [Description("Creates extreme chaos and monstrous behaviors in the game")]
    public class ChaosPlugin : RustPlugin
    {
        // Add effect constants at the top of the class
        private const string EXPLOSION_EFFECT = "assets/prefabs/weapons/f1 grenade/effects/f1grenade_explosion.prefab";
        private const string TELEPORT_EFFECT = "assets/bundled/prefabs/fx/gestures/wave.prefab";
        private const string SMOKE_EFFECT = "assets/bundled/prefabs/fx/smoke_signal_green.prefab";
        private const string LIGHTNING_EFFECT = "assets/prefabs/npc/ch47/effects/rocket-explosion.prefab";
        
        private Dictionary<ulong, float> playerJumpPowers = new Dictionary<ulong, float>();
        private Dictionary<ulong, float> playerSizes = new Dictionary<ulong, float>();
        private Dictionary<ulong, PlayerModifiers> playerModifiers = new Dictionary<ulong, PlayerModifiers>();
        private Dictionary<BaseNpc, NpcChaosData> npcChaos = new Dictionary<BaseNpc, NpcChaosData>();
        private Timer chaosTimer;
        private List<ActiveEvent> activeEvents = new List<ActiveEvent>();
        private List<BaseNpc> chaosNPCs = new List<BaseNpc>();
        private Dictionary<BasePlayer, Timer> activeTeleportTimers = new Dictionary<BasePlayer, Timer>();
        private Dictionary<NetworkableId, float> lastRadioDropTime = new Dictionary<NetworkableId, float>();
        private const float RADIO_DROP_COOLDOWN = 300f; // 5 minute cooldown

        // UI Elements
        private const string UIMain = "ChaosUI";
        private const string UIPanel = UIMain + ".Panel";
        private const string UIEvent = UIPanel + ".Event";
        private const string UITimer = UIPanel + ".Timer";
        private const string UIEventList = UIPanel + ".EventList";

        #region Plugin Priority and Permissions
        private const string PermAdmin = "chaos.admin";
        private const string PermUse = "chaos.use";
        private const int PluginPriority = 0; // Highest priority
        #endregion

        private class ActiveEvent
        {
            public string Name { get; set; }
            public float EndTime { get; set; }
        }

        private class PlayerModifiers
        {
            public float HealthMultiplier = 1f;
            public float DamageMultiplier = 1f;
            public float SpeedMultiplier = 1f;
            public float FallDamageReduction = 0f;
            public bool IsGodMode = false;
            public bool NoClip = false;
            public float GatherMultiplier = 1f;
        }

        private class NpcChaosData
        {
            public bool IsMutated;
            public float SizeMultiplier;
            public float SpeedMultiplier;
            public float DamageMultiplier;
            public bool IsExplosive;
            public bool IsTeleporter;
            public bool IsResourceDropper;
            public string CustomBehavior;
        }

        // Configuration
        private Configuration config;
        public class Configuration
        {
            // Event Settings
            public float ChaosInterval = 45f; // Changed to 45 seconds for more frequent chaos
            public bool EnableRandomSizes = true;
            public bool EnableJumpModification = true;
            public bool EnableResourceMultiplier = true;
            public bool EnableCombatChaos = true;
            public bool EnableEventChaos = true;
            public int MaxSimultaneousEvents = 5; // Increased simultaneous events

            // Player Modification Settings
            public float MinPlayerSize = 0.3f; // Even smaller possible
            public float MaxPlayerSize = 8f; // Even larger possible
            public float MinJumpPower = 2f; // Increased minimum jump
            public float MaxJumpPower = 10f; // Increased maximum jump
            public float SuperJumpChance = 0.2f; // More frequent super jumps
            public float DamageReflectChance = 0.25f; // More frequent damage reflection
            public float ResourceMultiplierChance = 0.4f; // More frequent resource bonuses

            // Balance Settings
            public float BaseHealthMultiplier = 3f; // More base health
            public float MaxHealthMultiplier = 8f; // Much more max health
            public float BaseDamageMultiplier = 2f; // More base damage
            public float MaxDamageMultiplier = 5f; // Much more max damage
            public float BaseSpeedMultiplier = 1.5f; // Faster base speed
            public float MaxSpeedMultiplier = 4f; // Much faster max speed
            public float BaseFallDamageReduction = 0.9f; // Almost no fall damage by default
            public float MaxFallDamageReduction = 1f; // Complete fall damage immunity possible
            public float BaseGatherMultiplier = 5f; // Much more resource gathering
            public float MaxGatherMultiplier = 20f; // Extreme resource gathering

            // Event Chances - More balanced distribution for variety
            public float WeirdEffectChance = 0.4f;
            public float RaidDefenseChance = 0.55f;
            public float LootExplosionChance = 0.7f;
            public float RandomTeleportChance = 0.8f;
            public float WeatherChangeChance = 0.9f;
            public float AnimalPartyChance = 0.95f;
            public float ResourceRainChance = 1f;
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
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        void Init()
        {
            // Register permissions
            permission.RegisterPermission(PermAdmin, this);
            permission.RegisterPermission(PermUse, this);

            // Start the chaos timer
            if (chaosTimer != null)
            {
                chaosTimer.Destroy();
            }
            
            chaosTimer = timer.Every(config.ChaosInterval, () =>
            {
                TriggerRandomChaosEvent();
            });

            // Add UI update timer
            timer.Every(1f, () =>
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player != null)
                    {
                        RefreshPlayerUI(player);
                    }
                }
            });

            Puts("Chaos Mode initialized! Prepare for weirdness...");

            // Create UI for all active players
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player != null)
                {
                    RefreshPlayerUI(player);
                }
            }

            // Override server settings
            SetServerOverrides();

            // Add cleanup timer
            timer.Every(1800f, () => // 30 minutes
            {
                CleanupChaosEntities();
            });
        }

        private void SetServerOverrides()
        {
            try
            {
                // Override server settings for better chaos experience
                ConsoleSystem.Run(ConsoleSystem.Option.Server, "pve", "0");
                ConsoleSystem.Run(ConsoleSystem.Option.Server, "antihack.noclip_protection", "0");
                ConsoleSystem.Run(ConsoleSystem.Option.Server, "antihack.maxdesync", "1");
                ConsoleSystem.Run(ConsoleSystem.Option.Server, "antihack.maxviolation", "1000");
                ConsoleSystem.Run(ConsoleSystem.Option.Server, "server.radiation", "0");
                ConsoleSystem.Run(ConsoleSystem.Option.Server, "server.respawnresetrange", "0");
                ConsoleSystem.Run(ConsoleSystem.Option.Server, "server.itemdespawn", "7200");
                ConsoleSystem.Run(ConsoleSystem.Option.Server, "server.corpsedespawn", "600");
                
                // Broadcast server settings change
                foreach (var player in BasePlayer.activePlayerList)
                {
                    Player.Message(player, "Chaos Mode has taken control of the server!", "Chaos");
                }

                Puts("Server settings overridden for Chaos Mode");
            }
            catch (Exception ex)
            {
                Puts($"Error setting server overrides: {ex.Message}");
            }
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new configuration file");
            config = new Configuration
            {
                // Event Settings
                ChaosInterval = 45f, // Changed to 45 seconds for more frequent chaos
                EnableRandomSizes = true,
                EnableJumpModification = true,
                EnableResourceMultiplier = true,
                EnableCombatChaos = true,
                EnableEventChaos = true,
                MaxSimultaneousEvents = 5,

                // Player Modification Settings
                MinPlayerSize = 0.3f,
                MaxPlayerSize = 8f,
                MinJumpPower = 2f,
                MaxJumpPower = 10f,
                SuperJumpChance = 0.2f,
                DamageReflectChance = 0.25f,
                ResourceMultiplierChance = 0.4f,

                // Balance Settings
                BaseHealthMultiplier = 3f,
                MaxHealthMultiplier = 8f,
                BaseDamageMultiplier = 2f,
                MaxDamageMultiplier = 5f,
                BaseSpeedMultiplier = 1.5f,
                MaxSpeedMultiplier = 4f,
                BaseFallDamageReduction = 0.9f,
                MaxFallDamageReduction = 1f,
                BaseGatherMultiplier = 5f,
                MaxGatherMultiplier = 20f,

                // Event Chances
                WeirdEffectChance = 0.4f,
                RaidDefenseChance = 0.55f,
                LootExplosionChance = 0.7f,
                RandomTeleportChance = 0.8f,
                WeatherChangeChance = 0.9f,
                AnimalPartyChance = 0.95f,
                ResourceRainChance = 1f
            };
        }

        #region Command Handling
        [ChatCommand("chaos")]
        void ChaosCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermAdmin))
            {
                Player.Message(player, "You don't have permission to use this command!", "Chaos");
                return;
            }

            if (args.Length == 0)
            {
                SendHelpText(player);
                return;
            }

            Puts($"Chaos command received: {string.Join(" ", args)}"); // Debug log

            switch (args[0].ToLower())
            {
                case "event":
                    int eventCount = 1;
                    if (args.Length > 1 && int.TryParse(args[1], out int count))
                    {
                        eventCount = Mathf.Clamp(count, 1, 5);
                    }
                    
                    Puts($"Triggering {eventCount} random chaos events..."); // Debug log
                    for (int i = 0; i < eventCount; i++)
                    {
                        TriggerRandomChaosEvent(true); // Pass true to indicate manual trigger
                    }
                    Player.Message(player, $"Triggered {eventCount} random chaos event{(eventCount > 1 ? "s" : "")}!", "Chaos");
                    RefreshPlayerUI(player); // Update UI immediately
                    break;

                case "size":
                    if (args.Length < 3)
                    {
                        Player.Message(player, "Usage: /chaos size <playerName> <size>", "Chaos");
                        return;
                    }
                    var target = BasePlayer.Find(args[1]);
                    if (target == null)
                    {
                        Player.Message(player, "Player not found!", "Chaos");
                        return;
                    }
                    if (float.TryParse(args[2], out float size))
                    {
                        ModifyPlayerSize(target, size);
                        Player.Message(player, $"Changed {target.displayName}'s size to {size}x", "Chaos");
                    }
                    break;

                case "health":
                    if (args.Length < 3)
                    {
                        Player.Message(player, "Usage: /chaos health <playerName> <multiplier>", "Chaos");
                        return;
                    }
                    var healthTarget = BasePlayer.Find(args[1]);
                    if (healthTarget == null)
                    {
                        Player.Message(player, "Player not found!", "Chaos");
                        return;
                    }
                    if (float.TryParse(args[2], out float healthMult))
                    {
                        SetPlayerHealthMultiplier(healthTarget, healthMult);
                        Player.Message(player, $"Changed {healthTarget.displayName}'s health multiplier to {healthMult}x", "Chaos");
                    }
                    break;

                case "speed":
                    if (args.Length < 3)
                    {
                        Player.Message(player, "Usage: /chaos speed <playerName> <multiplier>", "Chaos");
                        return;
                    }
                    var speedTarget = BasePlayer.Find(args[1]);
                    if (speedTarget == null)
                    {
                        Player.Message(player, "Player not found!", "Chaos");
                        return;
                    }
                    if (float.TryParse(args[2], out float speedMult))
                    {
                        SetPlayerSpeedMultiplier(speedTarget, speedMult);
                        Player.Message(player, $"Changed {speedTarget.displayName}'s speed multiplier to {speedMult}x", "Chaos");
                    }
                    break;

                case "god":
                    if (args.Length < 2)
                    {
                        Player.Message(player, "Usage: /chaos god <playerName>", "Chaos");
                        return;
                    }
                    var godTarget = BasePlayer.Find(args[1]);
                    if (godTarget == null)
                    {
                        Player.Message(player, "Player not found!", "Chaos");
                        return;
                    }
                    ToggleGodMode(godTarget);
                    Player.Message(player, $"Toggled god mode for {godTarget.displayName}", "Chaos");
                    break;

                case "reset":
                    if (args.Length < 2)
                    {
                        ResetAllPlayers();
                        Player.Message(player, "Reset all players to default state", "Chaos");
                    }
                    else
                    {
                        var resetTarget = BasePlayer.Find(args[1]);
                        if (resetTarget == null)
                        {
                            Player.Message(player, "Player not found!", "Chaos");
                            return;
                        }
                        ResetPlayer(resetTarget);
                        Player.Message(player, $"Reset {resetTarget.displayName} to default state", "Chaos");
                    }
                    break;

                case "gather":
                    if (args.Length < 3)
                    {
                        Player.Message(player, "Usage: /chaos gather <playerName> <multiplier>", "Chaos");
                        return;
                    }
                    var gatherTarget = BasePlayer.Find(args[1]);
                    if (gatherTarget == null)
                    {
                        Player.Message(player, "Player not found!", "Chaos");
                        return;
                    }
                    if (float.TryParse(args[2], out float gatherMult))
                    {
                        SetGatherMultiplier(gatherTarget, gatherMult);
                        Player.Message(player, $"Changed {gatherTarget.displayName}'s gather rate to {gatherMult}x", "Chaos");
                    }
                    break;

                default:
                    SendHelpText(player);
                    break;
            }
        }

        void SendHelpText(BasePlayer player)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Chaos Mode Commands:");
            sb.AppendLine("/chaos event - Trigger a random chaos event");
            sb.AppendLine("/chaos size <player> <size> - Change player size");
            sb.AppendLine("/chaos health <player> <multiplier> - Set health multiplier");
            sb.AppendLine("/chaos speed <player> <multiplier> - Set speed multiplier");
            sb.AppendLine("/chaos god <player> - Toggle god mode");
            sb.AppendLine("/chaos gather <player> <multiplier> - Set gather multiplier");
            sb.AppendLine("/chaos reset [player] - Reset all or specific player");
            Player.Message(player, sb.ToString(), "Chaos");
        }
        #endregion

        #region Player Modifications
        private void SetPlayerHealthMultiplier(BasePlayer player, float multiplier)
        {
            if (!playerModifiers.ContainsKey(player.userID))
                playerModifiers[player.userID] = new PlayerModifiers();

            playerModifiers[player.userID].HealthMultiplier = Mathf.Clamp(multiplier, 1f, config.MaxHealthMultiplier);
            UpdatePlayerHealth(player);
        }

        private void SetPlayerSpeedMultiplier(BasePlayer player, float multiplier)
        {
            if (!playerModifiers.ContainsKey(player.userID))
                playerModifiers[player.userID] = new PlayerModifiers();

            playerModifiers[player.userID].SpeedMultiplier = Mathf.Clamp(multiplier, 0.5f, config.MaxSpeedMultiplier);
        }

        private void SetGatherMultiplier(BasePlayer player, float multiplier)
        {
            if (!playerModifiers.ContainsKey(player.userID))
                playerModifiers[player.userID] = new PlayerModifiers();

            playerModifiers[player.userID].GatherMultiplier = Mathf.Clamp(multiplier, 1f, config.MaxGatherMultiplier);
        }

        private void ToggleGodMode(BasePlayer player)
        {
            if (!playerModifiers.ContainsKey(player.userID))
                playerModifiers[player.userID] = new PlayerModifiers();

            playerModifiers[player.userID].IsGodMode = !playerModifiers[player.userID].IsGodMode;
            Player.Message(player, playerModifiers[player.userID].IsGodMode ? "God mode enabled!" : "God mode disabled!", "Chaos");
        }

        private void UpdatePlayerHealth(BasePlayer player)
        {
            if (!playerModifiers.ContainsKey(player.userID)) return;

            float maxHealth = 100f * playerModifiers[player.userID].HealthMultiplier;
            player.health = maxHealth;
            player.MaxHealth();
        }

        private void ResetPlayer(BasePlayer player)
        {
            ModifyPlayerSize(player, 1f);
            playerJumpPowers.Remove(player.userID);
            playerModifiers.Remove(player.userID);
            player.health = 100f;
            player.MaxHealth();
        }

        private void ResetAllPlayers()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                ResetPlayer(player);
            }
        }
        #endregion

        #region UI System
        private void RefreshPlayerUI(BasePlayer player)
        {
            if (player == null) return;

            // Destroy any existing UI
            CuiHelper.DestroyUi(player, UIMain);

            var ui = new CuiElementContainer();

            // Background gradient panel
            ui.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.95", Material = "assets/content/ui/uibackgroundblur.mat" },
                RectTransform = { AnchorMin = "0.8 0.5", AnchorMax = "0.99 0.95" },
                CursorEnabled = false
            }, "Hud", UIMain);

            // Top border
            ui.Add(new CuiPanel
            {
                Image = { Color = "1 0.5 0 1" },
                RectTransform = { AnchorMin = "0 0.98", AnchorMax = "1 1" }
            }, UIMain);

            // Bottom border
            ui.Add(new CuiPanel
            {
                Image = { Color = "1 0.5 0 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.02" }
            }, UIMain);

            // Update title based on active events
            string titleText = activeEvents.Count > 0 ? "CHAOS UNLEASHED!" : "CHAOS MODE";
            string titleColor = activeEvents.Count > 0 ? "1 0.3 0 1" : "1 0.6 0 1";

            // Title text
            ui.Add(new CuiElement
            {
                Parent = UIMain,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = titleText,
                        FontSize = 20,
                        Font = "robotocondensed-bold.ttf",
                        Align = TextAnchor.MiddleCenter,
                        Color = titleColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0.85",
                        AnchorMax = "1 1"
                    }
                }
            });

            // Update timer
            float timeLeft = config.ChaosInterval - (Time.time % config.ChaosInterval);
            string timerText = $"Next event: {(int)(timeLeft / 60)}:{(int)(timeLeft % 60):D2}";
            string timerColor = timeLeft < 10 ? "1 0 0 1" : timeLeft < 30 ? "1 1 0 1" : "0.7 1 0.7 1";

            // Timer text
            ui.Add(new CuiElement
            {
                Parent = UIMain,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = timerText,
                        FontSize = 16,
                        Font = "robotocondensed-regular.ttf",
                        Align = TextAnchor.MiddleCenter,
                        Color = timerColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0.75",
                        AnchorMax = "1 0.85"
                    }
                }
            });

            // Event list container
            ui.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.8" },
                RectTransform = { AnchorMin = "0 0.1", AnchorMax = "1 0.75" }
            }, UIMain, UIEventList);

            // Event list header
            ui.Add(new CuiElement
            {
                Parent = UIEventList,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "ACTIVE EVENTS",
                        FontSize = 14,
                        Font = "robotocondensed-bold.ttf",
                        Align = TextAnchor.UpperCenter,
                        Color = "1 0.8 0 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0.9",
                        AnchorMax = "1 1"
                    }
                }
            });

            // Display active events
            float currentY = 0.85f;
            float entryHeight = 0.1f;

            // Sort events by end time and take up to MaxSimultaneousEvents
            var sortedEvents = activeEvents
                .OrderByDescending(e => e.EndTime - Time.time)
                .Take(config.MaxSimultaneousEvents)
                .ToList();

            foreach (var evt in sortedEvents)
            {
                float remaining = evt.EndTime - Time.time;
                if (remaining <= 0) continue;

                float progress = remaining / 30f;
                string eventColor = GetEventColor(evt.Name);

                // Event text with smaller font
                string eventText = $"• {evt.Name} ({(int)remaining}s)";
                ui.Add(new CuiElement
                {
                    Parent = UIEventList,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = eventText,
                            FontSize = 12, // Reduced font size
                            Font = "robotocondensed-regular.ttf",
                            Align = TextAnchor.MiddleLeft,
                            Color = eventColor
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"0.05 {currentY - entryHeight}",
                            AnchorMax = $"0.95 {currentY}"
                        }
                    }
                });

                // Progress bar background
                ui.Add(new CuiPanel
                {
                    Image = { Color = "0.2 0.2 0.2 1" },
                    RectTransform = { 
                        AnchorMin = $"0.05 {currentY - entryHeight + 0.03}", // Adjusted spacing
                        AnchorMax = $"0.95 {currentY - entryHeight + 0.05}"  // Adjusted spacing
                    }
                }, UIEventList);

                // Progress bar fill
                ui.Add(new CuiPanel
                {
                    Image = { Color = eventColor },
                    RectTransform = { 
                        AnchorMin = $"0.05 {currentY - entryHeight + 0.03}", // Adjusted spacing
                        AnchorMax = $"{0.05 + (0.9 * progress)} {currentY - entryHeight + 0.05}" // Adjusted spacing
                    }
                }, UIEventList);

                currentY -= entryHeight;
            }

            // Apply the UI changes
            CuiHelper.AddUi(player, ui);
        }

        private string GetEventColor(string eventName)
        {
            switch (eventName)
            {
                case "GIANT MODE": return "1 0.5 0.5 1";
                case "SUPER SPEED": return "0.5 1 0.5 1";
                case "BOUNCY CASTLE": return "0.5 0.5 1 1";
                case "GRAVITY SHIFT": return "1 0.5 0.5 1";
                case "EXPLOSIVE TOUCH": return "1 1 0.5 1";
                case "RESOURCE AURA": return "0.5 1 0.5 1";
                case "CHAOS STORM": return "1 0.5 1 1";
                case "RANDOM TELEPORTS": return "0.7 0.3 1 1";
                case "WEATHER: RAIN": return "0.5 0.5 1 1";
                case "WEATHER: FOG": return "0.5 0.5 0.5 1";
                case "WEATHER: STORM": return "0.5 0.5 0.5 1";
                case "WEATHER: CLEAR": return "1 1 1 1";
                case "ANIMAL PARTY": return "0.3 1 0.7 1";
                case "RESOURCE RAIN": return "0.5 1 1 1";
                case "MUTANT HORDE": return "1 0.5 0.5 1";
                case "MIND CONTROL": return "1 0.5 1 1";
                case "MASS CHAOS": return "1 0.3 0.3 1";
                default: return "1 1 1 1";
            }
        }
        #endregion

        #region Hooks
        void OnPlayerConnected(BasePlayer player)
        {
            if (!playerModifiers.ContainsKey(player.userID))
                playerModifiers[player.userID] = new PlayerModifiers();
            
            if (config.EnableRandomSizes)
            {
                playerSizes[player.userID] = Random.Range(config.MinPlayerSize, config.MaxPlayerSize);
                ModifyPlayerSize(player, playerSizes[player.userID]);
            }

            if (config.EnableJumpModification)
            {
                playerJumpPowers[player.userID] = Random.Range(config.MinJumpPower, config.MaxJumpPower);
            }

            // Apply base modifiers
            playerModifiers[player.userID].HealthMultiplier = config.BaseHealthMultiplier;
            playerModifiers[player.userID].SpeedMultiplier = config.BaseSpeedMultiplier;
            playerModifiers[player.userID].FallDamageReduction = config.BaseFallDamageReduction;
            playerModifiers[player.userID].GatherMultiplier = config.BaseGatherMultiplier;

            UpdatePlayerHealth(player);
            
            string welcomeMessage = $"Welcome to CHAOS MODE! ";
            if (config.EnableJumpModification)
                welcomeMessage += $"Jump power: {playerJumpPowers[player.userID]}x ";
            if (config.EnableRandomSizes)
                welcomeMessage += $"Size: {playerSizes[player.userID]}x";
            
            Player.Message(player, welcomeMessage, "Chaos");

            // Create UI for new player
            RefreshPlayerUI(player);
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            playerJumpPowers.Remove(player.userID);
            playerSizes.Remove(player.userID);
            playerModifiers.Remove(player.userID);
            CuiHelper.DestroyUi(player, UIMain);
        }

        object OnPlayerJump(BasePlayer player)
        {
            if (!config.EnableJumpModification || !playerJumpPowers.ContainsKey(player.userID))
                return null;

            var rigidbody = player.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                float jumpForce = playerJumpPowers[player.userID] * 5f;
                
                if (Random.Range(0f, 1f) < config.SuperJumpChance)
                {
                    jumpForce *= 5f;
                    Player.Message(player, "SUPER JUMP!", "Chaos");
                }

                rigidbody.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            }
            return null;
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;

            // Handle player damage
            if (entity is BasePlayer player)
            {
                if (!playerModifiers.ContainsKey(player.userID))
                    return null;

                var mods = playerModifiers[player.userID];

                // God mode check
                if (mods.IsGodMode)
                    return true; // Block all damage

                // Fall damage reduction
                if (info.damageTypes.Has(DamageType.Fall))
                {
                    info.damageTypes.Scale(DamageType.Fall, 1f - mods.FallDamageReduction);
                }

                // Damage multiplier for PvP
                if (info.Initiator is BasePlayer)
                {
                    info.damageTypes.Scale(DamageType.Generic, mods.DamageMultiplier);
                }
            }

            // Handle damage reflection
            if (config.EnableCombatChaos && entity is BasePlayer && info?.Initiator is BasePlayer)
            {
                if (Random.Range(0f, 1f) < config.DamageReflectChance)
                {
                    BasePlayer attacker = info.Initiator.ToPlayer();
                    attacker.Hurt(info.damageTypes.Total());
                    Player.Message(attacker, "Your attack was reflected!", "Chaos");
                }
            }

            return null;
        }

        void OnGatherItem(Item item, BaseEntity entity)
        {
            if (!config.EnableResourceMultiplier) return;

            var player = entity.ToPlayer();
            if (player == null || !playerModifiers.ContainsKey(player.userID)) return;

            float multiplier = playerModifiers[player.userID].GatherMultiplier;
            if (Random.Range(0f, 1f) < config.ResourceMultiplierChance)
            {
                multiplier *= Random.Range(2, 10);
                Player.Message(player, $"Lucky! Resources multiplied by {multiplier}x!", "Chaos");
            }

            item.amount = Mathf.RoundToInt(item.amount * multiplier);
        }

        private void ModifyPlayerSize(BasePlayer player, float scale)
        {
            if (player?.transform != null)
            {
                Puts($"Modifying size for {player.displayName} to {scale}x"); // Debug log
                try
                {
                    player.transform.localScale = new Vector3(scale, scale, scale);
                    player.SendNetworkUpdate();
                    player.ClientRPCPlayer(null, player, "ForcePositionTo", player.transform.position);
                }
                catch (Exception ex)
                {
                    Puts($"Error modifying player size: {ex.Message}");
                }
            }
        }
        #endregion

        #region Events
        private void TriggerRandomChaosEvent(bool isManualTrigger = false)
        {
            Puts("TriggerRandomChaosEvent called"); // Debug log
            if (!config.EnableEventChaos && !isManualTrigger)
            {
                Puts("Event chaos is disabled!");
                return;
            }

            var players = BasePlayer.activePlayerList.ToList();
            if (players.Count == 0)
            {
                Puts("No active players found!");
                return;
            }

            float eventRoll = Random.Range(0f, 1f);
            Puts($"Event roll: {eventRoll}"); // Debug log
            
            if (eventRoll < 0.15f) // Increased chance for weird effects
            {
                Puts("Triggering weird effect");
                TriggerWeirdEffect(players);
            }
            else if (eventRoll < 0.3f) // Increased chance for raid defense
            {
                Puts("Triggering raid defense");
                TriggerRaidDefense(players);
            }
            else if (eventRoll < 0.45f) // Increased chance for loot explosion
            {
                Puts("Triggering loot explosion");
                TriggerLootExplosion(players);
            }
            else if (eventRoll < 0.6f) // Increased chance for random teleport
            {
                Puts("Triggering random teleport");
                TriggerRandomTeleport(players);
            }
            else if (eventRoll < 0.7f)
            {
                Puts("Triggering weather change");
                TriggerWeatherChange();
            }
            else if (eventRoll < 0.8f)
            {
                Puts("Triggering animal party");
                TriggerAnimalParty(players);
            }
            else if (eventRoll < 0.85f)
            {
                Puts("Triggering resource rain");
                TriggerResourceRain(players);
            }
            else if (eventRoll < 0.9f)
            {
                Puts("Triggering chaos NPC event");
                TriggerChaosNPCEvent();
            }
            else if (eventRoll < 0.95f)
            {
                Puts("Triggering mind control event");
                TriggerMindControlEvent();
            }
            else
            {
                Puts("Triggering mass chaos event");
                // Trigger multiple events at once for maximum chaos
                TriggerWeirdEffect(players);
                TriggerLootExplosion(players);
                TriggerChaosNPCEvent();
                TriggerResourceRain(players);
                UpdateEventUI("MASS CHAOS", 300f);
                Server.Broadcast("CHAOS EVENT: MASS CHAOS - EVERYTHING IS HAPPENING!");
            }
        }

        private void TriggerWeirdEffect(List<BasePlayer> players)
        {
            int effectType = Random.Range(0, 8); // Reduced number of effects
            string eventName = "";
            float duration = 180f;

            switch (effectType)
            {
                case 0:
                    eventName = "GIANT MODE";
                    foreach (var player in players)
                    {
                        float newSize = Random.Range(2f, 4f);
                        ModifyPlayerSize(player, newSize);
                        playerSizes[player.userID] = newSize;
                        Player.Message(player, $"You've grown to {newSize}x size!", "Chaos");
                    }
                    break;

                case 1:
                    eventName = "SUPER SPEED";
                    foreach (var player in players)
                    {
                        if (!playerModifiers.ContainsKey(player.userID))
                            playerModifiers[player.userID] = new PlayerModifiers();
                        playerModifiers[player.userID].SpeedMultiplier = Random.Range(2f, 4f);
                        Player.Message(player, "GOTTA GO FAST!", "Chaos");
                    }
                    break;

                case 2:
                    eventName = "BOUNCY CASTLE";
                    foreach (var player in players)
                    {
                        if (!playerModifiers.ContainsKey(player.userID))
                            playerModifiers[player.userID] = new PlayerModifiers();
                        playerJumpPowers[player.userID] = Random.Range(5f, 8f);
                        Player.Message(player, "BOUNCE! BOUNCE! BOUNCE!", "Chaos");
                    }
                    break;

                case 3:
                    eventName = "GRAVITY SHIFT";
                    foreach (var player in players)
                    {
                        var rigidbody = player.GetComponent<Rigidbody>();
                        if (rigidbody != null)
                        {
                            rigidbody.mass = Random.Range(0.1f, 0.5f);
                            Player.Message(player, "Gravity? What gravity?", "Chaos");
                        }
                    }
                    break;

                case 4:
                    eventName = "EXPLOSIVE TOUCH";
                    foreach (var player in players)
                    {
                        timer.Every(1f, () => {
                            if (player != null && Time.time < Time.time + duration)
                            {
                                Effect.server.Run("assets/bundled/prefabs/fx/explosions/explosion_satchel.prefab", player.transform.position);
                                foreach (var collider in Physics.OverlapSphere(player.transform.position, 3f))
                                {
                                    var target = collider.GetComponentInParent<BasePlayer>();
                                    if (target != null && target != player)
                                    {
                                        target.Hurt(25f);
                                    }
                                }
                            }
                        });
                    }
                    break;

                case 5:
                    eventName = "RESOURCE AURA";
                    foreach (var player in players)
                    {
                        timer.Every(2f, () => {
                            if (player != null && Time.time < Time.time + duration)
                            {
                                DropRandomResources(player.transform.position);
                            }
                        });
                    }
                    break;

                case 6:
                    eventName = "CHAOS STORM";
                    foreach (var player in players)
                    {
                        timer.Every(1f, () => {
                            if (player != null && Time.time < Time.time + duration)
                            {
                                if (Random.value < 0.3f)
                                {
                                    Vector3 strikePos = player.transform.position + new Vector3(Random.Range(-10f, 10f), 0, Random.Range(-10f, 10f));
                                    Effect.server.Run(LIGHTNING_EFFECT, strikePos);
                                    foreach (var collider in Physics.OverlapSphere(strikePos, 3f))
                                    {
                                        var target = collider.GetComponentInParent<BasePlayer>();
                                        if (target != null)
                                        {
                                            target.Hurt(15f);
                                        }
                                    }
                                }
                            }
                        });
                    }
                    break;

                case 7:
                    eventName = "RANDOM TELEPORTS";
                    foreach (var player in players)
                    {
                        timer.Every(3f, () => {
                            if (player != null && Time.time < Time.time + duration)
                            {
                                Vector3 randomPos = player.transform.position + new Vector3(
                                    Random.Range(-20f, 20f),
                                    0f,
                                    Random.Range(-20f, 20f)
                                );
                                
                                // Validate ground position
                                var ray = new Ray(randomPos + new Vector3(0, 50f, 0), Vector3.down);
                                RaycastHit hit;
                                if (Physics.Raycast(ray, out hit, 100f, LayerMask.GetMask("Terrain", "World")))
                                {
                                    // Check if position is safe (not in water, not too steep)
                                    bool isSafe = true;
                                    float slope = Vector3.Angle(hit.normal, Vector3.up);
                                    
                                    if (slope > 40f) // Too steep
                                        isSafe = false;
                                    
                                    if (WaterLevel.GetWaterDepth(hit.point, true, false, null) > 0.5f) // In water
                                        isSafe = false;

                                    if (isSafe)
                                    {
                                        randomPos.y = hit.point.y + 1f;
                                        player.Teleport(randomPos);
                                        Effect.server.Run("assets/bundled/prefabs/fx/teleport.prefab", randomPos);
                                    }
                                }
                            }
                        });
                    }
                    break;
            }

            UpdateEventUI(eventName, duration);
            Server.Broadcast($"CHAOS EVENT: {eventName}!");
        }

        private void TriggerRaidDefense(List<BasePlayer> players)
        {
            foreach (var player in players)
            {
                int attempts = 0;
                int maxAttempts = 5;
                int spawnedTurrets = 0;
                int maxTurrets = Random.Range(1, 3);

                while (spawnedTurrets < maxTurrets && attempts < maxAttempts)
                {
                    attempts++;
                    
                    // Get random position around player
                    float angle = Random.Range(0f, 360f);
                    float distance = Random.Range(10f, 20f);
                    Vector3 offset = new Vector3(
                        Mathf.Cos(angle * Mathf.Deg2Rad) * distance,
                        0f,
                        Mathf.Sin(angle * Mathf.Deg2Rad) * distance
                    );
                    
                    Vector3 spawnPos = player.transform.position + offset;
                    
                    // Find valid ground position
                    var ray = new Ray(spawnPos + new Vector3(0, 10f, 0), Vector3.down);
                    RaycastHit hit;
                    if (Physics.Raycast(ray, out hit, 20f, LayerMask.GetMask("Terrain", "World", "Construction")))
                    {
                        spawnPos = hit.point + new Vector3(0, 0.2f, 0);
                        
                        // Check ground slope
                        if (Vector3.Angle(hit.normal, Vector3.up) < 40f)
                        {
                            var turret = GameManager.server.CreateEntity("assets/prefabs/npc/autoturret/autoturret_deployed.prefab", spawnPos) as AutoTurret;
                            if (turret != null)
                            {
                                turret.Spawn();
                                
                                // Make turret functional without power
                                turret.SetFlag(BaseEntity.Flags.Reserved8, true); // Makes turret deployable
                                turret.SetFlag(BaseEntity.Flags.Reserved7, true); // Powered flag
                                turret.SetFlag(BaseEntity.Flags.On, true); // Turn it on
                                turret.InitializeHealth(9999f, 9999f); // Make turret nearly indestructible
                                turret.pickup.enabled = false; // Prevent pickup
                                
                                // Add weapon and ammo
                                Item weapon = ItemManager.CreateByName("rifle.ak", 1);
                                if (weapon != null)
                                {
                                    weapon.condition = 9999f; // Make weapon unbreakable
                                    weapon.maxCondition = 9999f;
                                    turret.inventory.Insert(weapon);
                                }

                                // Add multiple stacks of ammo
                                for (int i = 0; i < 3; i++)
                                {
                                    Item ammo = ItemManager.CreateByName("ammo.rifle", 9999);
                                    if (ammo != null)
                                    {
                                        turret.inventory.Insert(ammo);
                                    }
                                }
                                
                                // Configure turret settings
                                turret.SetPeacekeepermode(false); // Attack all
                                turret.authorizedPlayers.Add(new ProtoBuf.PlayerNameID { 
                                    userid = player.userID, 
                                    username = player.displayName 
                                });
                                
                                // Force turret to be powered and ready
                                turret.UpdateHasPower(100, 1);
                                turret.SetIsOnline(true);
                                
                                // Add to cleanup list
                                chaosEntities.Add(new ChaosEntity { 
                                    Entity = turret, 
                                    SpawnTime = Time.time 
                                });
                                
                                spawnedTurrets++;
                                
                                // Add visual effect for turret spawn
                                Effect.server.Run(SMOKE_EFFECT, spawnPos);
                            }
                        }
                    }
                }
            }
            
            UpdateEventUI("RAID DEFENSE", 45f);
            Server.Broadcast("CHAOS EVENT: Indestructible Auto-turret Defense System!");
        }

        private void TriggerLootExplosion(List<BasePlayer> players)
        {
            int maxDropsPerPlayer = 2; // Reduced from 3-8 to just 2
            float cleanupDelay = 300f; // 5 minutes

            foreach (var player in players)
            {
                for (int i = 0; i < maxDropsPerPlayer; i++)
                {
                    Vector3 spawnPos = player.transform.position + new Vector3(
                        Random.Range(-10f, 10f),
                        20f,
                        Random.Range(-10f, 10f)
                    );
                    
                    // Find valid ground position
                    var ray = new Ray(spawnPos + new Vector3(0, 5f, 0), Vector3.down);
                    RaycastHit hit;
                    if (Physics.Raycast(ray, out hit, 15f, LayerMask.GetMask("Terrain", "World")))
                    {
                        spawnPos.y = hit.point.y + 0.2f;
                        var drop = GameManager.server.CreateEntity("assets/prefabs/misc/supply drop/supply_drop.prefab", spawnPos) as SupplyDrop;
                        if (drop != null)
                        {
                            drop.Spawn();
                            
                            // Add to cleanup list
                            chaosEntities.Add(new ChaosEntity { 
                                Entity = drop, 
                                SpawnTime = Time.time 
                            });

                            // Add smoke effect
                            Effect.server.Run(SMOKE_EFFECT, spawnPos);
                        }
                    }
                }
            }

            // Schedule cleanup of uncollected drops
            timer.Once(cleanupDelay, () =>
            {
                var drops = chaosEntities
                    .Where(ce => ce.Entity is SupplyDrop && Time.time - ce.SpawnTime >= cleanupDelay)
                    .ToList();

                foreach (var drop in drops)
                {
                    if (drop.Entity != null && !drop.Entity.IsDestroyed)
                    {
                        Effect.server.Run(SMOKE_EFFECT, drop.Entity.transform.position);
                        drop.Entity.Kill();
                    }
                    chaosEntities.Remove(drop);
                }
            });

            UpdateEventUI("LOOT EXPLOSION", 60f);
            Server.Broadcast("CHAOS EVENT: Limited Supply Drop Event! Drops will disappear in 5 minutes!");
        }

        private void TriggerRandomTeleport(List<BasePlayer> players)
        {
            foreach (var player in players)
            {
                // Cancel any existing teleport timer for this player
                if (activeTeleportTimers.ContainsKey(player))
                {
                    activeTeleportTimers[player]?.Destroy();
                    activeTeleportTimers.Remove(player);
                }

                // Create new teleport timer
                var timer = this.timer.Every(5f, () => { // Reduced frequency to every 5 seconds
                    if (player == null || !player.IsConnected) return;

                    // Get current map bounds
                    float mapSize = TerrainMeta.Size.x;
                    Vector3 mapCenter = TerrainMeta.Position + new Vector3(mapSize / 2, 0, mapSize / 2);

                    // Calculate random position within reasonable bounds
                    float maxDistance = 30f; // Reduced teleport distance
                    Vector3 randomOffset = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(10f, maxDistance);
                    randomOffset.y = 0; // Zero out vertical component initially

                    Vector3 targetPos = player.transform.position + randomOffset;

                    // Ensure position is within map bounds
                    targetPos.x = Mathf.Clamp(targetPos.x, 0, mapSize);
                    targetPos.z = Mathf.Clamp(targetPos.z, 0, mapSize);

                    // Validate ground position
                    RaycastHit hit;
                    if (Physics.Raycast(targetPos + new Vector3(0, 50f, 0), Vector3.down, out hit, 100f, LayerMask.GetMask("Terrain", "World")))
                    {
                        // Check if position is safe (not in water, not too steep)
                        bool isSafe = true;
                        float slope = Vector3.Angle(hit.normal, Vector3.up);
                        
                        if (slope > 40f) // Too steep
                            isSafe = false;
                        
                        if (WaterLevel.GetWaterDepth(hit.point, true, false, null) > 0.5f) // In water
                            isSafe = false;

                        if (isSafe)
                        {
                            targetPos.y = hit.point.y + 1f;
                            player.Teleport(targetPos);
                            Effect.server.Run("assets/bundled/prefabs/fx/teleport.prefab", targetPos);
                        }
                    }
                });

                activeTeleportTimers[player] = timer;
            }

            // Set shorter duration for teleport event
            UpdateEventUI("RANDOM TELEPORT", 15f);
            Server.Broadcast("CHAOS EVENT: Random Teleport Party!");

            // Cleanup timers after event ends
            this.timer.Once(15f, () =>
            {
                foreach (var kvp in activeTeleportTimers.ToList())
                {
                    kvp.Value?.Destroy();
                    activeTeleportTimers.Remove(kvp.Key);
                }
            });
        }

        private void TriggerWeatherChange()
        {
            string[] weathers = { "rain", "fog", "storm", "clear" };
            string randomWeather = weathers[Random.Range(0, weathers.Length)];
            ConVar.Weather.rain = randomWeather == "rain" ? 1 : 0;
            ConVar.Weather.fog = randomWeather == "fog" ? 1 : 0;
            UpdateEventUI($"WEATHER: {randomWeather.ToUpper()}", 120f);
            Server.Broadcast($"CHAOS EVENT: Weather changed to {randomWeather}!");
        }

        private void TriggerAnimalParty(List<BasePlayer> players)
        {
            foreach (var player in players)
            {
                for (int i = 0; i < Random.Range(2, 5); i++)
                {
                    Vector3 spawnPos = player.transform.position + new Vector3(
                        Random.Range(-20f, 20f),
                        0f,
                        Random.Range(-20f, 20f)
                    );
                    
                    string[] animals = { 
                        "assets/rust.ai/agents/chicken/chicken.prefab",
                        "assets/rust.ai/agents/boar/boar.prefab",
                        "assets/rust.ai/agents/stag/stag.prefab",
                        "assets/rust.ai/agents/wolf/wolf.prefab",
                        "assets/rust.ai/agents/bear/bear.prefab",
                        "assets/rust.ai/agents/horse/horse.prefab"
                    };
                    string randomAnimal = animals[Random.Range(0, animals.Length)];
                    
                    GameManager.server.CreateEntity(randomAnimal, spawnPos)?.Spawn();
                }
            }
            UpdateEventUI("ANIMAL PARTY", 30f);
            Server.Broadcast("CHAOS EVENT: Animal Party!");
        }

        private void TriggerResourceRain(List<BasePlayer> players)
        {
            foreach (var player in players)
            {
                for (int i = 0; i < 10; i++)
                {
                    Vector3 randomPos = player.transform.position + new Vector3(
                        Random.Range(-10f, 10f),
                        15f,
                        Random.Range(-10f, 10f)
                    );
                    Item randomItem = ItemManager.CreateByName(GetRandomResource(), Random.Range(50, 200));
                    randomItem?.Drop(randomPos, Vector3.zero);
                }
            }
            UpdateEventUI("RESOURCE RAIN", 30f);
            Server.Broadcast("CHAOS EVENT: It's raining resources!");
        }

        private string GetRandomResource()
        {
            string[] resources = new[] {
                "wood", "stones", "metal.ore", "metal.refined",
                "sulfur.ore", "sulfur", "charcoal", "cloth",
                "leather", "metal.fragments", "gunpowder",
                "scrap", "crude.oil", "lowgradefuel",
                "explosive.timed", "explosive.satchel",
                "ammo.rifle", "ammo.pistol", "weapon.mod.holosight"
            };
            return resources[Random.Range(0, resources.Length)];
        }
        #endregion

        private void UpdateEventUI(string eventName, float duration)
        {
            // Remove expired events first
            activeEvents.RemoveAll(e => Time.time >= e.EndTime);

            // Check for existing event and update its duration if found
            var existingEvent = activeEvents.FirstOrDefault(e => e.Name == eventName);
            if (existingEvent != null)
            {
                // Extend the duration from current end time
                existingEvent.EndTime = Mathf.Max(existingEvent.EndTime, Time.time + duration);
            }
            else
            {
                // Add new event
                var newEvent = new ActiveEvent
                {
                    Name = eventName,
                    EndTime = Time.time + duration
                };
                activeEvents.Add(newEvent);
            }

            // Limit number of simultaneous events
            while (activeEvents.Count > config.MaxSimultaneousEvents)
            {
                activeEvents.RemoveAt(0);
            }

            // Force UI update for all players
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player != null)
                {
                    RefreshPlayerUI(player);
                }
            }
        }

        private void DropRandomResources(Vector3 position)
        {
            // Find valid ground position
            var ray = new Ray(position + new Vector3(0, 5f, 0), Vector3.down);
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 10f, LayerMask.GetMask("Terrain", "World")))
                return;

            position = hit.point + new Vector3(0, 0.2f, 0);

            string[] resources = {
                "wood", "stones", "metal.ore", "metal.refined",
                "sulfur.ore", "sulfur", "charcoal", "cloth",
                "leather", "metal.fragments", "gunpowder",
                "scrap"
            };

            for (int i = 0; i < Random.Range(1, 3); i++)
            {
                string resource = resources[Random.Range(0, resources.Length)];
                Item item = ItemManager.CreateByName(resource, Random.Range(10, 50));
                if (item != null)
                {
                    item.Drop(
                        position + new Vector3(Random.Range(-1f, 1f), 0.5f, Random.Range(-1f, 1f)),
                        Vector3.up
                    );
                }
            }
        }

        private void TriggerChaosNPCEvent()
        {
            string eventName = "MUTANT HORDE";
            float duration = 300f;

            // Spawn mutated NPCs near each player
            foreach (var player in BasePlayer.activePlayerList)
            {
                int npcCount = Random.Range(1, 3);
                int attempts = 0;
                int maxAttempts = 10;
                int spawnedNPCs = 0;

                while (spawnedNPCs < npcCount && attempts < maxAttempts)
                {
                    attempts++;
                    
                    // Get random position around player with increased height check
                    float angle = Random.Range(0f, 360f);
                    float distance = Random.Range(15f, 25f);
                    Vector3 offset = new Vector3(
                        Mathf.Cos(angle * Mathf.Deg2Rad) * distance,
                        30f, // Increased height for better ground detection
                        Mathf.Sin(angle * Mathf.Deg2Rad) * distance
                    );
                    
                    Vector3 spawnPos = player.transform.position + offset;
                    
                    // Find valid ground position with multiple checks
                    RaycastHit groundHit;
                    bool validPosition = false;
                    
                    // Check in a larger radius for valid ground
                    for (int i = 0; i < 8; i++) // Increased number of position checks
                    {
                        Vector3 checkPos = spawnPos + new Vector3(Random.Range(-5f, 5f), 0, Random.Range(-5f, 5f));
                        if (Physics.Raycast(checkPos, Vector3.down, out groundHit, 50f, LayerMask.GetMask("Terrain", "World", "Construction")))
                        {
                            // Verify ground is suitable
                            float slope = Vector3.Angle(groundHit.normal, Vector3.up);
                            float heightAboveTerrain = groundHit.point.y - TerrainMeta.HeightMap.GetHeight(groundHit.point);
                            
                            if (slope < 40f && heightAboveTerrain > -1f && heightAboveTerrain < 5f)
                            {
                                spawnPos = groundHit.point + Vector3.up * 1f;
                                validPosition = true;
                                break;
                            }
                        }
                    }

                    if (validPosition)
                    {
                        string[] npcPrefabs = {
                            "assets/rust.ai/agents/bear/bear.prefab",
                            "assets/rust.ai/agents/wolf/wolf.prefab",
                            "assets/prefabs/npc/scientist/scientist.prefab"
                        };

                        var npc = GameManager.server.CreateEntity(
                            npcPrefabs[Random.Range(0, npcPrefabs.Length)],
                            spawnPos
                        ) as BaseNpc;

                        if (npc != null)
                        {
                            npc.Spawn();
                            
                            // Apply modifications to all NPCs
                            var baseCombat = npc as BaseCombatEntity;
                            if (baseCombat != null)
                            {
                                baseCombat.startHealth *= Random.Range(2f, 4f);
                                baseCombat.health = baseCombat.startHealth;
                            }
                            
                            var nav = npc.GetComponent<BaseNavigator>();
                            if (nav != null)
                            {
                                nav.Speed = Mathf.Max(nav.Speed * Random.Range(1.5f, 3f), 1f);
                                nav.CanUseNavMesh = false; // Disable NavMesh requirement
                            }
                            
                            spawnedNPCs++;
                            Effect.server.Run(EXPLOSION_EFFECT, spawnPos); // Use explosion effect instead of smoke
                        }
                    }
                }
            }

            UpdateEventUI(eventName, duration);
            Server.Broadcast($"CHAOS EVENT: {eventName} - Mutated creatures are hunting you!");
        }

        private void TriggerMindControlEvent()
        {
            string eventName = "MIND CONTROL";
            float duration = 60f;

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (Random.value < 0.5f)
                {
                    // Random teleportation with longer intervals
                    timer.Every(5f, () => { // Increased from 1f to 5f
                        if (player != null && Time.time < Time.time + duration)
                        {
                            if (Random.value < 0.3f) // Only 30% chance to trigger movement
                            {
                                // Get current map bounds for safer teleportation
                                float mapSize = TerrainMeta.Size.x;
                                Vector3 randomDir = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
                                Vector3 targetPos = player.transform.position + (randomDir * Random.Range(10f, 30f)); // Increased distance range

                                // Ensure position is within map bounds
                                targetPos.x = Mathf.Clamp(targetPos.x, 0, mapSize);
                                targetPos.z = Mathf.Clamp(targetPos.z, 0, mapSize);

                                // Validate ground position
                                RaycastHit hit;
                                if (Physics.Raycast(targetPos + new Vector3(0, 50f, 0), Vector3.down, out hit, 100f, LayerMask.GetMask("Terrain", "World")))
                                {
                                    // Check if position is safe
                                    float slope = Vector3.Angle(hit.normal, Vector3.up);
                                    if (slope < 40f && WaterLevel.GetWaterDepth(hit.point, true, false, null) < 0.5f)
                                    {
                                        targetPos.y = hit.point.y + 1f;
                                        player.Teleport(targetPos);
                                        Effect.server.Run(TELEPORT_EFFECT, targetPos);
                                    }
                                }
                            }
                        }
                    });
                }
            }

            UpdateEventUI(eventName, duration);
            Server.Broadcast($"CHAOS EVENT: {eventName} - Your mind is being controlled from afar!");
        }

        void OnEntitySpawned(BaseEntity entity)
        {
            if (entity is BaseNpc npc && Random.value < 0.3f)
            {
                MutateNPC(npc);
            }
        }

        void Unload()
        {
            // Reset all player sizes and modifiers
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player != null)
                {
                    ResetPlayer(player);
                    CuiHelper.DestroyUi(player, UIMain);
                }
            }

            // Clean up NPCs
            foreach (var npc in chaosNPCs.ToList())
            {
                if (npc != null && !npc.IsDestroyed)
                {
                    npc.transform.localScale = Vector3.one;
                }
            }

            chaosNPCs.Clear();
            npcChaos.Clear();
            chaosTimer?.Destroy();

            // Clean up all remaining chaos entities
            foreach (var chaosEntity in chaosEntities.ToList())
            {
                if (chaosEntity.Entity != null && !chaosEntity.Entity.IsDestroyed)
                {
                    chaosEntity.Entity.Kill();
                }
            }
            chaosEntities.Clear();

            // Clean up teleport timers
            foreach (var timer in activeTeleportTimers.Values)
            {
                timer?.Destroy();
            }
            activeTeleportTimers.Clear();
        }

        #region NPC Modifications
        private void MutateNPC(BaseNpc npc)
        {
            if (!npcChaos.ContainsKey(npc))
            {
                npcChaos[npc] = new NpcChaosData
                {
                    IsMutated = true,
                    SizeMultiplier = Random.Range(2f, 4f),
                    SpeedMultiplier = Random.Range(1.5f, 3f),
                    DamageMultiplier = Random.Range(1.5f, 3f),
                    IsExplosive = Random.value < 0.2f,
                    IsTeleporter = Random.value < 0.2f,
                    IsResourceDropper = Random.value < 0.2f
                };
            }

            var chaosData = npcChaos[npc];
            npc.transform.localScale = Vector3.one * chaosData.SizeMultiplier;
            
            var baseCombatEntity = npc as BaseCombatEntity;
            if (baseCombatEntity != null)
            {
                baseCombatEntity.startHealth *= chaosData.DamageMultiplier;
                baseCombatEntity.health *= chaosData.DamageMultiplier;
            }

            var baseNavigator = npc.GetComponent<BaseNavigator>();
            if (baseNavigator != null)
            {
                baseNavigator.Speed = Mathf.Max(baseNavigator.Speed * chaosData.SpeedMultiplier, 1f);
                baseNavigator.CanUseNavMesh = false; // Disable NavMesh requirement
            }

            chaosNPCs.Add(npc);

            timer.Every(3f, () =>
            {
                if (npc == null || npc.IsDestroyed)
                    return;

                if (chaosData.IsTeleporter && Random.value < 0.1f)
                {
                    BasePlayer nearestPlayer = null;
                    float nearestDistance = float.MaxValue;
                    foreach (var player in BasePlayer.activePlayerList)
                    {
                        float distance = Vector3.Distance(npc.transform.position, player.transform.position);
                        if (distance < nearestDistance && distance > 10f)
                        {
                            nearestDistance = distance;
                            nearestPlayer = player;
                        }
                    }

                    if (nearestPlayer != null)
                    {
                        Vector3 targetPos = nearestPlayer.transform.position + new Vector3(
                            Random.Range(-15f, 15f),
                            20f,
                            Random.Range(-15f, 15f)
                        );

                        // Find valid ground position
                        RaycastHit groundHit;
                        if (Physics.Raycast(targetPos, Vector3.down, out groundHit, 30f, LayerMask.GetMask("Terrain", "World")))
                        {
                            if (Vector3.Angle(groundHit.normal, Vector3.up) < 40f)
                            {
                                npc.transform.position = groundHit.point + Vector3.up * 0.5f;
                                Effect.server.Run(TELEPORT_EFFECT, groundHit.point);
                            }
                        }
                    }
                }

                if (chaosData.IsExplosive && Random.value < 0.05f)
                {
                    Effect.server.Run(EXPLOSION_EFFECT, npc.transform.position);
                    foreach (var collider in Physics.OverlapSphere(npc.transform.position, 5f))
                    {
                        var player = collider.GetComponentInParent<BasePlayer>();
                        if (player != null)
                        {
                            player.Hurt(50f * chaosData.DamageMultiplier);
                        }
                    }
                }
            });
        }
        #endregion

        #region NPC Hooks
        object OnNpcTarget(BaseNpc npc, BaseEntity target)
        {
            if (!npcChaos.ContainsKey(npc)) return null;
            var chaosData = npcChaos[npc];

            // Random chance to change target
            if (Random.value < 0.2f)
            {
                var players = BasePlayer.activePlayerList;
                if (players.Count > 0)
                {
                    var randomPlayer = players[Random.Range(0, players.Count)];
                    return randomPlayer;
                }
            }

            // Random chance to ignore target
            if (Random.value < 0.1f)
            {
                return false;
            }

            return null;
        }

        void OnNpcDestinationSet(BaseEntity entity)
        {
            var npc = entity as BaseNpc;
            if (npc == null || !npcChaos.ContainsKey(npc)) return;

            var chaosData = npcChaos[npc];

            // Random chance to teleport to nearest player
            if (chaosData.IsTeleporter && Random.value < 0.3f)
            {
                BasePlayer nearestPlayer = null;
                float nearestDistance = float.MaxValue;
                foreach (var player in BasePlayer.activePlayerList)
                {
                    float distance = Vector3.Distance(npc.transform.position, player.transform.position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestPlayer = player;
                    }
                }

                if (nearestPlayer != null)
                {
                    Vector3 targetPos = nearestPlayer.transform.position + new Vector3(
                        Random.Range(-10f, 10f),
                        Random.Range(0f, 5f),
                        Random.Range(-10f, 10f)
                    );
                    npc.transform.position = targetPos;
                    Effect.server.Run("assets/bundled/prefabs/fx/gestures/wave.prefab", npc.transform.position);
                }
            }

            // Random chance to create chaos at current position
            if (Random.value < 0.2f)
            {
                Vector3 pos = npc.transform.position;
                timer.Once(1f, () =>
                {
                    if (npc == null || npc.IsDestroyed) return;
                    
                    // Create explosion effect
                    Effect.server.Run("assets/bundled/prefabs/fx/explosions/explosion_satchel.prefab", pos);
                    
                    // Spawn random items
                    for (int i = 0; i < Random.Range(2, 6); i++)
                    {
                        Item randomItem = ItemManager.CreateByName(GetRandomResource(), Random.Range(25, 100));
                        if (randomItem != null)
                        {
                            randomItem.Drop(pos + new Vector3(Random.Range(-3f, 3f), 1f, Random.Range(-3f, 3f)), Vector3.up * 3f);
                        }
                    }
                });
            }
        }

        void OnNpcAlert(ScientistNPC scientist)
        {
            if (scientist == null) return;

            // Random chance to trigger chaos event
            if (Random.value < 0.3f)
            {
                // Call for reinforcements
                int npcCount = Random.Range(1, 4);
                for (int i = 0; i < npcCount; i++)
                {
                    Vector3 spawnPos = scientist.transform.position + new Vector3(
                        Random.Range(-10f, 10f),
                        0f,
                        Random.Range(-10f, 10f)
                    );

                    var newNpc = GameManager.server.CreateEntity(
                        "assets/prefabs/npc/scientist/scientist.prefab",
                        spawnPos
                    ) as BaseNpc;

                    if (newNpc != null)
                    {
                        newNpc.Spawn();
                        MutateNPC(newNpc);
                    }
                }

                // Create smoke screen
                Effect.server.Run("assets/bundled/prefabs/fx/smoke_signal_green.prefab", scientist.transform.position);
            }
        }

        void OnNpcConversationStart(NPCTalking npc, BasePlayer player, ConversationData conversation)
        {
            if (Random.value < 0.2f)
            {
                // Trigger random chaos event when talking to NPCs
                TriggerRandomChaosEvent();
                Player.Message(player, "The NPC's words trigger something chaotic!", "Chaos");
            }
        }

        void OnNpcRadioChatter(ScientistNPC scientist)
        {
            if (scientist == null) return;

            // Check cooldown
            if (!lastRadioDropTime.ContainsKey(scientist.net.ID))
            {
                lastRadioDropTime[scientist.net.ID] = 0f;
            }

            if (Time.time - lastRadioDropTime[scientist.net.ID] < RADIO_DROP_COOLDOWN)
            {
                return; // Still on cooldown
            }

            if (Random.value < 0.05f) // Reduced from 0.2 to 0.05 (5% chance)
            {
                Vector3 dropPos = scientist.transform.position + new Vector3(
                    Random.Range(-50f, 50f),
                    50f,
                    Random.Range(-50f, 50f)
                );

                var drop = GameManager.server.CreateEntity("assets/prefabs/misc/supply drop/supply_drop.prefab", dropPos) as SupplyDrop;
                if (drop != null)
                {
                    drop.Spawn();
                    lastRadioDropTime[scientist.net.ID] = Time.time;
                    
                    // Add to cleanup list
                    chaosEntities.Add(new ChaosEntity { 
                        Entity = drop, 
                        SpawnTime = Time.time 
                    });

                    // Add smoke effect
                    Effect.server.Run(SMOKE_EFFECT, dropPos);
                    Server.Broadcast("A scientist's radio chatter has called in a supply drop!");
                }
            }
        }
        #endregion

        // Add cleanup system
        private class ChaosEntity
        {
            public BaseEntity Entity;
            public float SpawnTime;
        }

        private List<ChaosEntity> chaosEntities = new List<ChaosEntity>();

        private void CleanupChaosEntities()
        {
            float currentTime = Time.time;
            var entitiesToRemove = chaosEntities.Where(ce => currentTime - ce.SpawnTime >= 1800f).ToList();
            
            foreach (var chaosEntity in entitiesToRemove)
            {
                if (chaosEntity.Entity != null && !chaosEntity.Entity.IsDestroyed)
                {
                    chaosEntity.Entity.Kill();
                }
                chaosEntities.Remove(chaosEntity);
            }
            
            Puts($"Cleaned up {entitiesToRemove.Count} chaos entities");
        }
    }
} 