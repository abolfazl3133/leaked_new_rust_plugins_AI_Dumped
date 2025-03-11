using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using ProtoBuf;

namespace Oxide.Plugins
{
    [Info("Better Teams", "BlackWolf", "1.0.0")]
    [Description("Enhances team gameplay with HUD, auto-auth, skins, markers, and team voice chat")]
    public class BetterTeams : RustPlugin
    {
        [PluginReference] private Plugin? ImageLibrary;

        private ConfigData configData = new();
        private Dictionary<ulong, TeamData> teamData = new();
        private Dictionary<ulong, bool> teamVoiceEnabled = new();
        private Dictionary<ulong, bool> teamSkinsEnabled = new();
        private Dictionary<ulong, bool> teamMarkersEnabled = new();

        private const string PERMISSION_HUD = "betterteams.hud";
        private const string PERMISSION_VOICE = "betterteams.voice";
        private const string PERMISSION_SKINS = "betterteams.skins";
        private const string PERMISSION_MARKER = "betterteams.marker";

        private const string UI_PANEL_MAIN = "BetterTeamsUI.Main";
        private const string UI_PANEL_HUD = "BetterTeamsUI.HUD";

        void Init()
        {
            permission.RegisterPermission(PERMISSION_HUD, this);
            permission.RegisterPermission(PERMISSION_VOICE, this);
            permission.RegisterPermission(PERMISSION_SKINS, this);
            permission.RegisterPermission(PERMISSION_MARKER, this);

            cmd.AddChatCommand(configData.TeamSettingCommand, this, nameof(CmdBetterTeam));
            
            // Register console commands for UI
            cmd.AddConsoleCommand("betterteams.close", this, nameof(CmdCloseUI));
            cmd.AddConsoleCommand("betterteams.menu", this, nameof(CmdOpenMenu));
            cmd.AddConsoleCommand("betterteams.toggle", this, nameof(CmdToggleSetting));
            LoadDefaultMessages();
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyAllUI(player);
            }
            SaveData();
        }

        void OnServerInitialized(bool initial)
        {
            if (!ImageLibrary)
            {
                PrintError("ImageLibrary is not loaded! Plugin will not function correctly.");
                return;
            }

            LoadData();
            InitializeTeams();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["UI_NO_TEAM"] = "You must be in a team to use this command!",
                ["UI_BETTERTEAMS"] = "Better Teams Settings",
                ["UI_BETTERTEAMSDESCRIPTION"] = "Configure your team settings and features",
                ["UI_SETTINGSNAME"] = "Team Settings",
                ["UI_H_INFO_TEXT"] = "Click to open team settings",
                ["UI_S_ENABLE_TEAM_VOICE"] = "Team Voice Chat",
                ["UI_S_ENABLE_TEAM_VOICE_DES"] = "Enable voice chat with team members only",
                ["UI_S_ENABLE_TEAM_SKINS"] = "Team Skins",
                ["UI_S_ENABLE_TEAM_SKIN_DES"] = "Enable custom skins for team items",
                ["UI_S_ENABLE_MARKERS"] = "Team Markers",
                ["UI_S_ENABLE_MARKERS_DES"] = "Enable team position markers",
                ["UI_S_MARKER_BUTTON_DES"] = "Press ALT + Right Click to place a marker",
                ["UI_S_YES"] = "ON",
                ["UI_S_NO"] = "OFF",
                ["UI_HELP"] = "Available commands:\n/bt - Open team settings menu",
                ["UI_HELP_ADMIN"] = "/bt reload - Reload plugin configuration"
            }, this);
        }

        private void InitializeTeams()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null) continue;
                if (player.currentTeam != 0)
                {
                    var team = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
                    if (team != null)
                    {
                        OnTeamCreated(player, team);
                    }
                }
            }
        }

        private void DestroyAllUI(BasePlayer player)
        {
            if (player == null) return;
            CuiHelper.DestroyUi(player, UI_PANEL_MAIN);
            CuiHelper.DestroyUi(player, UI_PANEL_HUD);
        }

        void OnTeamCreated(BasePlayer player, RelationshipManager.PlayerTeam team)
        {
            if (team == null || player == null) return;

            if (!teamData.ContainsKey(team.teamID))
            {
                teamData[team.teamID] = new TeamData
                {
                    TeamId = team.teamID,
                    LeaderId = team.teamLeader,
                    Members = new Dictionary<ulong, TeamMemberData>(),
                    TeamSkins = new Dictionary<string, ulong>(),
                    AutoAuth = new Dictionary<ulong, AutoAuthSettings>()
                };
                SaveData();
            }

            if (configData.EnabledFunctions.EnableTeamHud)
            {
                CreateTeamHUD(player);
            }
        }

        void OnTeamAcceptInvite(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            if (team == null || player == null) return;

            if (!teamData.ContainsKey(team.teamID))
            {
                OnTeamCreated(player, team);
            }

            var memberData = new TeamMemberData
            {
                Name = player.displayName ?? "Unknown",
                IsOnline = true,
                Health = player.health,
                GridPosition = GetGridPosition(player.transform.position)
            };

            teamData[team.teamID].Members[player.userID] = memberData;
            SaveData();

            if (configData.EnabledFunctions.EnableTeamHud)
            {
                CreateTeamHUD(player);
            }
        }

        void OnTeamLeave(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            if (team == null || player == null) return;

            if (teamData.ContainsKey(team.teamID))
            {
                teamData[team.teamID].Members.Remove(player.userID);
                if (teamData[team.teamID].Members.Count == 0)
                {
                    teamData.Remove(team.teamID);
                }
                SaveData();
            }

            DestroyAllUI(player);
        }

        private void CreateTeamHUD(BasePlayer player)
        {
            if (player == null) return;
            if (!configData.EnabledFunctions.EnableTeamHud) return;
            if (configData.Permissions.RequireHudPermission && !permission.UserHasPermission(player.UserIDString, PERMISSION_HUD)) return;

            var container = new CuiElementContainer();

            // Main HUD Panel
            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.5" },
                RectTransform = {
                    AnchorMin = $"{configData.HudSettings.LeftOffset / 100f} {1f - (configData.HudSettings.TopOffset / 100f)}",
                    AnchorMax = $"{(configData.HudSettings.LeftOffset + 200) / 100f} {1f - ((configData.HudSettings.TopOffset - 100) / 100f)}"
                },
                CursorEnabled = false
            }, "Hud", UI_PANEL_HUD);

            // Team Members
            if (teamData.TryGetValue(player.currentTeam, out var team))
            {
                float yOffset = 0f;
                foreach (var member in team.Members.Values)
                {
                    AddMemberToHUD(container, UI_PANEL_HUD, member, yOffset);
                    yOffset += configData.HudSettings.LinesMargin;
                }
            }

            // Menu Button
            container.Add(new CuiButton
            {
                Button = { Command = "betterteams.menu", Color = "0.7 0.7 0.7 0.8" },
                RectTransform = {
                    AnchorMin = "0 0",
                    AnchorMax = "1 0.1"
                },
                Text = { Text = GetMsg("UI_H_INFO_TEXT", player), FontSize = 12, Align = TextAnchor.MiddleCenter }
            }, UI_PANEL_HUD);

            CuiHelper.DestroyUi(player, UI_PANEL_HUD);
            CuiHelper.AddUi(player, container);
        }

        private void AddMemberToHUD(CuiElementContainer container, string parent, TeamMemberData member, float yOffset)
        {
            // Member Name
            container.Add(new CuiLabel
            {
                Text = { Text = member.Name, FontSize = 14, Align = TextAnchor.MiddleLeft },
                RectTransform = {
                    AnchorMin = $"0.05 {1f - ((yOffset + 20) / 100f)}",
                    AnchorMax = $"0.7 {1f - (yOffset / 100f)}"
                }
            }, parent);

            // Health Bar
            container.Add(new CuiPanel
            {
                Image = { Color = GetHealthColor(member.Health) },
                RectTransform = {
                    AnchorMin = $"0.75 {1f - ((yOffset + 18) / 100f)}",
                    AnchorMax = $"0.95 {1f - ((yOffset + 2) / 100f)}"
                }
            }, parent);

            // Grid Position
            container.Add(new CuiLabel
            {
                Text = { Text = member.GridPosition, FontSize = 12, Align = TextAnchor.MiddleRight },
                RectTransform = {
                    AnchorMin = $"0.8 {1f - ((yOffset + 20) / 100f)}",
                    AnchorMax = $"1 {1f - (yOffset / 100f)}"
                }
            }, parent);
        }

        private static string GetHealthColor(float health)
        {
            if (health >= 80) return "0 1 0 1"; // Green
            if (health >= 50) return "1 1 0 1"; // Yellow
            return "1 0 0 1"; // Red
        }

        private static string GetGridPosition(Vector3 position)
        {
            var x = position.x;
            var z = position.z;
            return $"{(char)('A' + Mathf.FloorToInt((x + 1000) / 146.3f))}{Mathf.FloorToInt((z + 1000) / 146.3f)}";
        }

        [ChatCommand("bt")]
        private void CmdBetterTeam(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;

            if (args.Length > 0)
            {
                switch (args[0].ToLowerInvariant())
                {
                    case "help":
                        string helpText = GetMsg("UI_HELP", player);
                        if (player.IsAdmin)
                            helpText += "\n" + GetMsg("UI_HELP_ADMIN", player);
                        SendReply(player, helpText);
                        return;

                    case "reload":
                        if (!player.IsAdmin)
                        {
                            SendReply(player, "You don't have permission to use this command.");
                            return;
                        }
                        LoadConfig();
                        LoadData();
                        SendReply(player, "Configuration reloaded!");
                        return;

                    case "close":
                        DestroyAllUI(player);
                        return;
                }
            }

            if (player.currentTeam == 0)
            {
                SendReply(player, GetMsg("UI_NO_TEAM", player));
                return;
            }

            OpenTeamMenu(player);
        }

        private void CmdCloseUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            CuiHelper.DestroyUi(player, UI_PANEL_MAIN + ".bg");
            CuiHelper.DestroyUi(player, UI_PANEL_MAIN);
        }

        private void CmdOpenMenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            if (player.currentTeam == 0)
            {
                SendReply(player, GetMsg("UI_NO_TEAM", player));
                return;
            }
            OpenTeamMenu(player);
        }

        private void CmdToggleSetting(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            var args = arg.Args;
            if (args == null || args.Length < 2) return;
            
            var buttonId = args[0];
            var value = args[1].ToLowerInvariant() == "true";

            if (teamData.TryGetValue(player.currentTeam, out var team))
            {
                switch (buttonId)
                {
                    case "voiceToggle":
                        teamVoiceEnabled[player.userID] = value;
                        break;
                    case "skinsToggle":
                        teamSkinsEnabled[player.userID] = value;
                        if (!value)
                        {
                            team.TeamSkins.Clear();
                            SaveData();
                        }
                        break;
                    case "markersToggle":
                        teamMarkersEnabled[player.userID] = value;
                        if (!value)
                        {
                            foreach (var member in team.Members.Keys)
                            {
                                var memberPlayer = BasePlayer.FindByID(member);
                                if (memberPlayer != null)
                                {
                                    CuiHelper.DestroyUi(memberPlayer, "TeamMarker");
                                }
                            }
                        }
                        break;
                }
            }

            // Refresh the UI
            OpenTeamMenu(player);
        }

        private void OpenTeamMenu(BasePlayer player)
        {
            if (player == null) return;

            // First destroy any existing UI
            CuiHelper.DestroyUi(player, UI_PANEL_MAIN + ".bg");
            CuiHelper.DestroyUi(player, UI_PANEL_MAIN);

            var container = new CuiElementContainer();
            
            // Background Panel for Click-Away
            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.5" },
                RectTransform = { 
                    AnchorMin = "0 0", 
                    AnchorMax = "1 1" 
                },
                CursorEnabled = true
            }, "Overlay", UI_PANEL_MAIN + ".bg");

            // Add click handler to background
            container.Add(new CuiButton
            {
                Button = { 
                    Color = "0 0 0 0", 
                    Command = "betterteams.close", 
                    Close = UI_PANEL_MAIN + ".bg"
                },
                RectTransform = { 
                    AnchorMin = "0 0", 
                    AnchorMax = "1 1" 
                },
                Text = { Text = "" }
            }, UI_PANEL_MAIN + ".bg");

            // Main Menu Panel
            var panel = container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.9" },
                RectTransform = {
                    AnchorMin = "0.2 0.2",
                    AnchorMax = "0.8 0.8"
                },
                CursorEnabled = true
            }, UI_PANEL_MAIN + ".bg", UI_PANEL_MAIN);

            // Close Button
            container.Add(new CuiButton
            {
                Button = { 
                    Color = "0.7 0.2 0.2 0.9", 
                    Command = "betterteams.close",
                    Close = UI_PANEL_MAIN + ".bg"
                },
                RectTransform = { 
                    AnchorMin = "0.95 0.95", 
                    AnchorMax = "0.99 0.99" 
                },
                Text = { 
                    Text = "✕", 
                    FontSize = 14, 
                    Align = TextAnchor.MiddleCenter 
                }
            }, panel);

            // Title
            container.Add(new CuiLabel
            {
                Text = { Text = GetMsg("UI_BETTERTEAMS", player), FontSize = 24, Align = TextAnchor.MiddleCenter },
                RectTransform = {
                    AnchorMin = "0 0.9",
                    AnchorMax = "1 0.98"
                }
            }, panel);

            // Description
            container.Add(new CuiLabel
            {
                Text = { Text = GetMsg("UI_BETTERTEAMSDESCRIPTION", player), FontSize = 14, Align = TextAnchor.MiddleLeft },
                RectTransform = {
                    AnchorMin = "0.05 0.82",
                    AnchorMax = "0.95 0.9"
                }
            }, panel);

            // Settings Section
            AddSettingsSection(container, panel, player);

            CuiHelper.DestroyUi(player, UI_PANEL_MAIN);
            CuiHelper.AddUi(player, container);
        }

        private void AddSettingsSection(CuiElementContainer container, string panel, BasePlayer player)
        {
            // Settings Title
            container.Add(new CuiLabel
            {
                Text = { Text = GetMsg("UI_SETTINGSNAME", player), FontSize = 18, Align = TextAnchor.MiddleLeft },
                RectTransform = {
                    AnchorMin = "0.05 0.75",
                    AnchorMax = "0.95 0.8"
                }
            }, panel);

            float yPos = 0.7f;

            // Voice Chat Toggle
            if (configData.EnabledFunctions.EnableTeamVoice)
            {
                AddSettingToggle(container, panel, player, 
                    "UI_S_ENABLE_TEAM_VOICE", 
                    "UI_S_ENABLE_TEAM_VOICE_DES",
                    yPos,
                    () => teamVoiceEnabled.GetValueOrDefault(player.userID),
                    value => teamVoiceEnabled[player.userID] = value);
                yPos -= 0.1f;
            }

            // Team Skins Toggle
            if (configData.EnabledFunctions.EnableTeamSkins && permission.UserHasPermission(player.UserIDString, PERMISSION_SKINS))
            {
                AddSettingToggle(container, panel, player,
                    "UI_S_ENABLE_TEAM_SKINS",
                    "UI_S_ENABLE_TEAM_SKIN_DES",
                    yPos,
                    () => teamSkinsEnabled.GetValueOrDefault(player.userID),
                    value => 
                    {
                        teamSkinsEnabled[player.userID] = value;
                        if (teamData.TryGetValue(player.currentTeam, out var team))
                        {
                            if (!value)
                            {
                                team.TeamSkins.Clear();
                                SaveData();
                            }
                        }
                    });
                yPos -= 0.1f;
            }

            // Team Markers Toggle
            if (configData.EnabledFunctions.EnableEasyTeamMarkers && permission.UserHasPermission(player.UserIDString, PERMISSION_MARKER))
            {
                AddSettingToggle(container, panel, player,
                    "UI_S_ENABLE_MARKERS",
                    "UI_S_ENABLE_MARKERS_DES",
                    yPos,
                    () => teamMarkersEnabled.GetValueOrDefault(player.userID),
                    value => 
                    {
                        teamMarkersEnabled[player.userID] = value;
                        if (teamData.TryGetValue(player.currentTeam, out var team))
                        {
                            if (!value)
                            {
                                // Clear existing markers
                                foreach (var member in team.Members.Keys)
                                {
                                    var memberPlayer = BasePlayer.FindByID(member);
                                    if (memberPlayer != null)
                                    {
                                        CuiHelper.DestroyUi(memberPlayer, "TeamMarker");
                                    }
                                }
                            }
                        }
                    });
                yPos -= 0.1f;

                // Marker Key Bind Info
                container.Add(new CuiLabel
                {
                    Text = { Text = GetMsg("UI_S_MARKER_BUTTON_DES", player), FontSize = 12, Align = TextAnchor.MiddleLeft },
                    RectTransform = {
                        AnchorMin = $"0.1 {yPos - 0.05}",
                        AnchorMax = $"0.9 {yPos}"
                    }
                }, panel);
            }
        }

        private void AddSettingToggle(CuiElementContainer container, string panel, BasePlayer player, 
            string titleKey, string descKey, float yPos, Func<bool> getState, Action<bool> setState)
        {
            if (player == null) return;

            var isEnabled = getState();
            setState?.Invoke(isEnabled);

            string toggleId = titleKey.ToUpperInvariant() switch
            {
                "UI_S_ENABLE_TEAM_VOICE" => "voiceToggle",
                "UI_S_ENABLE_TEAM_SKINS" => "skinsToggle",
                "UI_S_ENABLE_MARKERS" => "markersToggle",
                _ => CuiHelper.GetGuid()
            };

            // Title
            container.Add(new CuiLabel
            {
                Text = { Text = GetMsg(titleKey, player), FontSize = 14, Align = TextAnchor.MiddleLeft },
                RectTransform = {
                    AnchorMin = $"0.1 {yPos}",
                    AnchorMax = $"0.9 {yPos + 0.03}"
                }
            }, panel);

            // Description
            container.Add(new CuiLabel
            {
                Text = { Text = GetMsg(descKey, player), FontSize = 12, Align = TextAnchor.MiddleLeft },
                RectTransform = {
                    AnchorMin = $"0.1 {yPos - 0.03}",
                    AnchorMax = $"0.9 {yPos}"
                }
            }, panel);

            // ON Button
            container.Add(new CuiButton
            {
                Button = { 
                    Color = isEnabled ? "0.7 0.7 0.7 1" : "0.4 0.4 0.4 1", 
                    Command = $"betterteams.toggle {toggleId} true" 
                },
                RectTransform = {
                    AnchorMin = $"0.75 {yPos}",
                    AnchorMax = $"0.82 {yPos + 0.03}"
                },
                Text = { Text = GetMsg("UI_S_YES", player), FontSize = 12, Align = TextAnchor.MiddleCenter }
            }, panel);

            // OFF Button
            container.Add(new CuiButton
            {
                Button = { 
                    Color = !isEnabled ? "0.7 0.7 0.7 1" : "0.4 0.4 0.4 1", 
                    Command = $"betterteams.toggle {toggleId} false" 
                },
                RectTransform = {
                    AnchorMin = $"0.85 {yPos}",
                    AnchorMax = $"0.92 {yPos + 0.03}"
                },
                Text = { Text = GetMsg("UI_S_NO", player), FontSize = 12, Align = TextAnchor.MiddleCenter }
            }, panel);
        }

        private string GetMsg(string key, BasePlayer? player) =>
            lang.GetMessage(key, this, player?.UserIDString);

        #region Data Classes
        private class TeamData
        {
            public ulong TeamId { get; set; }
            public ulong LeaderId { get; set; }
            public Dictionary<ulong, TeamMemberData> Members { get; set; } = new();
            public Dictionary<string, ulong> TeamSkins { get; set; } = new();
            public Dictionary<ulong, AutoAuthSettings> AutoAuth { get; set; } = new();
        }

        private class TeamMemberData
        {
            public string Name { get; set; } = string.Empty;
            public bool IsOnline { get; set; }
            public bool IsWounded { get; set; }
            public bool IsSleeping { get; set; }
            public float Health { get; set; }
            public string GridPosition { get; set; } = string.Empty;
        }

        private class AutoAuthSettings
        {
            public bool TC { get; set; } = true;
            public bool CodeLocks { get; set; } = true;
            public bool AutoTurrets { get; set; } = true;
            public bool SAMSite { get; set; } = true;
        }

        private class ConfigData
        {
            [JsonProperty(nameof(TeamSettingCommand))]
            public string TeamSettingCommand { get; set; } = "bt";

            [JsonProperty(nameof(EnabledFunctions))]
            public EnabledFunctions EnabledFunctions { get; set; } = new();

            [JsonProperty(nameof(Permissions))]
            public PermissionSettings Permissions { get; set; } = new();

            [JsonProperty(nameof(HudSettings))]
            public HudSettings HudSettings { get; set; } = new();
        }

        private class EnabledFunctions
        {
            [JsonProperty("Enable Team Hud")]
            public bool EnableTeamHud { get; set; } = true;

            [JsonProperty("Enable global team voice chat")]
            public bool EnableTeamVoice { get; set; } = true;

            [JsonProperty("Enable team skins")]
            public bool EnableTeamSkins { get; set; } = true;

            [JsonProperty("Enable easy team markers")]
            public bool EnableEasyTeamMarkers { get; set; } = true;
        }

        private class PermissionSettings
        {
            [JsonProperty("Need permission for Team Hud?")]
            public bool RequireHudPermission { get; set; } = true;

            [JsonProperty("Need permission for Team Voice?")]
            public bool RequireVoicePermission { get; set; } = true;

            [JsonProperty("Need permission for Team Skins?")]
            public bool RequireSkinsPermission { get; set; } = true;

            [JsonProperty("Need permission for Team marker?")]
            public bool RequireMarkerPermission { get; set; } = true;
        }

        private class HudSettings
        {
            [JsonProperty("UI Scale")]
            public float UIScale { get; set; } = 1.0f;

            [JsonProperty("Left Offset")]
            public float LeftOffset { get; set; } = 5f;

            [JsonProperty("Top Offset")]
            public float TopOffset { get; set; } = 200f;

            [JsonProperty("Lines margin")]
            public float LinesMargin { get; set; } = 20f;

            [JsonProperty("Max players per line")]
            public int MaxPlayersPerLine { get; set; } = 8;
        }
        #endregion

        #region Data Management
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                configData = Config.ReadObject<ConfigData>();
                if (configData == null) LoadDefaultConfig();
            }
            catch (JsonException ex)
            {
                PrintError($"Configuration file is corrupt! Loading default config... Error: {ex.Message}");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => 
            configData = new ConfigData();

        protected override void SaveConfig() => 
            Config.WriteObject(configData);

        private void LoadData() =>
            teamData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, TeamData>>("BetterTeams/TeamData") 
                ?? new Dictionary<ulong, TeamData>();

        private void SaveData() =>
            Interface.Oxide.DataFileSystem.WriteObject("BetterTeams/TeamData", teamData);
        #endregion

        void OnItemCraft(ItemCraftTask task, BasePlayer player)
        {
            if (player == null || !teamSkinsEnabled.GetValueOrDefault(player.userID)) return;
            
            if (teamData.TryGetValue(player.currentTeam, out var team) && team.TeamSkins.ContainsKey(task.blueprint.targetItem.shortname))
            {
                task.skinID = (int)team.TeamSkins[task.blueprint.targetItem.shortname];
            }
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || !teamMarkersEnabled.GetValueOrDefault(player.userID)) return;
            
            // Check for ALT + Right Click
            if (input.IsDown(BUTTON.FIRE_SECONDARY) && input.IsDown(BUTTON.SPRINT))
            {
                // Place marker at player's position
                if (teamData.TryGetValue(player.currentTeam, out var team))
                {
                    foreach (var member in team.Members.Keys)
                    {
                        var memberPlayer = BasePlayer.FindByID(member);
                        if (memberPlayer != null)
                        {
                            // Create marker UI for team member
                            var container = new CuiElementContainer();
                            container.Add(new CuiPanel
                            {
                                Image = { Color = "1 1 1 0.8" },
                                RectTransform = {
                                    AnchorMin = "0.495 0.495",
                                    AnchorMax = "0.505 0.505"
                                }
                            }, "Overlay", "TeamMarker");

                            CuiHelper.DestroyUi(memberPlayer, "TeamMarker");
                            CuiHelper.AddUi(memberPlayer, container);
                        }
                    }
                }
            }
        }
    }
} 
