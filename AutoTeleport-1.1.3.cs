using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Auto Teleport", "https://discord.gg/TrJ7jnS233", "1.1.3")]
    [Description("https://rustworkshop.space/resources/auto-teleport.78")]
    public class AutoTeleport : RustPlugin
    {
        private
        const string permUse = "autoteleport.use";
        private
        const string elemMain = "autoteleport.main";
        private static UISettings ui => config.uiSettings;
        private void Init()
        {
            permission.RegisterPermission(permUse, this);
            LoadData();
            cmd.AddConsoleCommand("autoteleport", this, "cmdControlConsole");
            foreach (var command in config.commands)
            {
                cmd.AddChatCommand(command, this, cmdControlChat);
            }
        }
        private void OnTeleportRequested(BasePlayer receiver, BasePlayer caller)
        {
            if (permission.UserHasPermission(receiver.UserIDString, permUse) == false)
            {
                return;
            }
            CheckTeleport(receiver, caller);
        }
        private void Unload()
        {
            SaveData();
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, elemMain);
            }
        }
        private void cmdControlConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || permission.UserHasPermission(player.UserIDString, permUse) == false)
            {
                return;
            }
            var data = Data.Get(player.userID);
            var type = arg.Args?.Length > 0 ? arg.Args[0] : "null";
            switch (type)
            {
                case "c":
                case "clan":
                case "clans":
                    data.autoClans = !data.autoClans;
                    break;
                case "f":
                case "friend":
                case "friends":
                    data.autoFriends = !data.autoFriends;
                    break;
                case "t":
                case "team":
                case "teams":
                    data.autoTeam = !data.autoTeam;
                    break;
            }
            OpenUI(player);
        }
        private void cmdControlChat(BasePlayer player, string command, string[] args)
        {
            OpenUI(player);
        }
        private void CheckTeleport(BasePlayer receiver, BasePlayer caller)
        {
            var data = Data.Get(receiver.userID);
            if (data.autoTeam && InSameTeam(receiver, caller))
            {
                AcceptTeleport(receiver);
                return;
            }
            if (data.autoFriends && IsFriends(receiver, caller))
            {
                AcceptTeleport(receiver);
                return;
            }
            if (data.autoClans && InSameClan(receiver, caller))
            {
                AcceptTeleport(receiver);
                return;
            }
        }
        private static void AcceptTeleport(BasePlayer player)
        {
            player.SendConsoleCommand("chat.say /tpa");
        }
        private void OpenUI(BasePlayer player, DataEntry data = null)
        {
            if (data == null)
            {
                data = Data.Get(player.userID);
            }
            var container = new CuiElementContainer {
                new CuiElement {
                    Name = elemMain, Components = {
                        new CuiImageComponent {
                            Color = ui.mainColor, Material = "assets/content/ui/namefontmaterial.mat",
                        },
                        new CuiRectTransformComponent {
                            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-{ui.mainSizeX} -{ui.mainSizeY}", OffsetMax = $"{ui.mainSizeX} {ui.mainSizeY}"
                        },
                        new CuiNeedsCursorComponent()
                    }
                }, new CuiElement {
                    Parent = elemMain, Components = {
                        new CuiTextComponent {
                            Text = ui.textMain, Color = ui.textColor, FontSize = ui.mainFont, Align = TextAnchor.UpperCenter,
                        },
                        new CuiRectTransformComponent {
                            AnchorMin = "0.2 0.05", AnchorMax = "0.8 0.95",
                        }
                    }
                }
            };
            var buttonClan = CreateButton("0.2", ui.textClan, "clans", data.autoClans, config.enabledClan == false);
            var buttonFriends = CreateButton("0.4", ui.textFriends, "friends", data.autoFriends, config.enabledFriends == false);
            var buttonTeam = CreateButton("0.6", ui.textTeam, "team", data.autoTeam, config.enabledTeam == false);
            container.AddRange(buttonClan);
            container.AddRange(buttonFriends);
            container.AddRange(buttonTeam);
            container.Add(new CuiButton
            {
                Text = {
                    Text = "X",
                    Color = "1 1 1 1",
                    Align = TextAnchor.MiddleCenter,
                    FontSize = ui.fontClose,
                },
                Button = {
                    Close = elemMain,
                    Color = "1 1 1 0"
                },
                RectTransform = {
                    AnchorMin = "1 1",
                    AnchorMax = "1 1",
                    OffsetMin = $"-{ui.sizeClose} -{ui.sizeClose}",
                    OffsetMax = "0 0",
                }
            }, elemMain);
            CuiHelper.DestroyUi(player, elemMain);
            CuiHelper.AddUi(player, container);
        }
        private CuiElementContainer CreateButton(string anchorY, string textLabel, string commandArg, bool buttonActive, bool buttonDisabled)
        {
            var container = new CuiElementContainer();
            var textButton = buttonActive ? ui.textOn : ui.textOff;
            var buttonColor = buttonActive ? ui.onColor : ui.offColor;
            if (buttonDisabled == true)
            {
                textButton = config.uiSettings.textDeactivated;
                buttonColor = config.uiSettings.deactivatedColor;
            }
            container.Add(new CuiElement
            {
                Parent = elemMain,
                Components = {
                    new CuiTextComponent {
                        Text = textLabel, Color = ui.textColor, Align = TextAnchor.MiddleCenter, FontSize = ui.textFont
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = $"0.45 {anchorY}", AnchorMax = $"0.45 {anchorY}", OffsetMin = $"-{ui.buttonSizeX * 2} -{ui.buttonSizeY}", OffsetMax = $"0 {ui.buttonSizeY}"
                    },
                }
            });
            container.Add(new CuiButton
            {
                Text = {
                    Text = textButton,
                    Align = TextAnchor.MiddleCenter,
                    Color = ui.buttonTextColor,
                    FontSize = ui.buttonFont
                },
                Button = {
                    Command = "autoteleport " + commandArg,
                    Color = buttonColor,
                },
                RectTransform = {
                    AnchorMin = $"0.55 {anchorY}",
                    AnchorMax = $"0.55 {anchorY}",
                    OffsetMin = $"0 -{ui.buttonSizeY}",
                    OffsetMax = $"{ui.buttonSizeX * 2} {ui.buttonSizeY}"
                }
            }, elemMain);
            return container;
        }
        private static ConfigData config = new ConfigData();
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Command")]
            public string[] commands = {
                "atp",
                "autoteleport",
                "autotp",
            };
            [JsonProperty(PropertyName = "Friends button enabled")] public bool enabledFriends = true;
            [JsonProperty(PropertyName = "Default value for friends")] public bool defaultFriends = true;
            [JsonProperty(PropertyName = "Clan button enabled")] public bool enabledClan = true;
            [JsonProperty(PropertyName = "Default value for clan")] public bool defaultClan = true;
            [JsonProperty(PropertyName = "Team button enabled")] public bool enabledTeam = true;
            [JsonProperty(PropertyName = "Default value for team")] public bool defaultTeam = true;
            [JsonProperty(PropertyName = "UI Settings")] public UISettings uiSettings = new UISettings();
        }
        private class UISettings
        {
            [JsonProperty(PropertyName = "[Text] On")] public string textOn = "ON";
            [JsonProperty(PropertyName = "[Text] Off")] public string textOff = "OFF";
            [JsonProperty(PropertyName = "[Text] Clan")] public string textClan = "Clan";
            [JsonProperty(PropertyName = "[Text] Team")] public string textTeam = "Team";
            [JsonProperty(PropertyName = "[Text] Deactivated")] public string textDeactivated = "INACTIVE";
            [JsonProperty(PropertyName = "[Text] Friends")] public string textFriends = "Friends";
            [JsonProperty(PropertyName = "[Text] Mail text")] public string textMain = "Auto Teleports";
            [JsonProperty(PropertyName = "[Color] Mail panel")] public string mainColor = "0.2 0.2 0.2 0.7";
            [JsonProperty(PropertyName = "[Color] Label text")] public string textColor = "1 1 1 1";
            [JsonProperty(PropertyName = "[Color] On/Off Button text")] public string buttonTextColor = "1 1 1 1";
            [JsonProperty(PropertyName = "[Color] Button On")] public string onColor = "0.5 0.75 0 0.8";
            [JsonProperty(PropertyName = "[Color] Button Off")] public string offColor = "0.7 0.2 0.2 0.8";
            [JsonProperty(PropertyName = "[Color] Button deactivate")] public string deactivatedColor = "0.9 0.9 0.9 0.8";
            [JsonProperty(PropertyName = "[Size] Mail panel X")] public int mainSizeX = 250;
            [JsonProperty(PropertyName = "[Size] Mail panel Y")] public int mainSizeY = 115;
            [JsonProperty(PropertyName = "[Size] On/Off Button X")] public int buttonSizeX = 100;
            [JsonProperty(PropertyName = "[Size] On/Off Button Y")] public int buttonSizeY = 20;
            [JsonProperty(PropertyName = "[Size] Close button")] public int sizeClose = 40;
            [JsonProperty(PropertyName = "[Font] Mail text")] public int mainFont = 25;
            [JsonProperty(PropertyName = "[Font] Label text")] public int textFont = 30;
            [JsonProperty(PropertyName = "[Font] On/Off Button text")] public int buttonFont = 30;
            [JsonProperty(PropertyName = "[Font] Close button text")] public int fontClose = 25;
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                timer.Every(10f, () =>
                {
                    PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                });
                LoadDefaultConfig();
                return;
            }
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        private
        const string filename = "data";
        private bool corruptedData;
        private Timer saveTimer;
        private class DataEntry
        {
            public bool autoClans = config.defaultClan;
            public bool autoTeam = config.defaultTeam;
            public bool autoFriends = config.defaultFriends;
        }
        private static PluginData Data = new PluginData();
        private class PluginData
        {
            [JsonIgnore] public Dictionary<string, DataEntry> cache = new Dictionary<string, DataEntry>();
            public Dictionary<string, DataEntry> info = new Dictionary<string, DataEntry>();
            public DataEntry Get(object param)
            {
                var key = param?.ToString();
                if (key == null)
                {
                    return null;
                }
                var value = (DataEntry)null;
                if (cache.TryGetValue(key, out value) == true)
                {
                    return value;
                }
                if (info.TryGetValue(key, out value) == false)
                {
                    value = new DataEntry();
                    info.Add(key, value);
                }
                cache.Add(key, value);
                return value;
            }
        }
        private void LoadData(string keyName = filename)
        {
            if (saveTimer == null)
            {
                saveTimer = timer.Every(Core.Random.Range(500, 700), () => SaveData());
            }
            try
            {
                Data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>($"{Name}/{keyName}");
            }
            catch (Exception e)
            {
                corruptedData = true;
                Data = new PluginData();
                timer.Every(30f, () =>
                {
                    PrintError($"!!! CRITICAL DATA ERROR !!!\n * Data was not loaded!\n * Data auto-save was disabled!\n * Error: {e.Message}");
                });
                LogToFile("errors", $"\n\nError: {e.Message}\n\nTrace: {e.StackTrace}\n\n", this);
            }
        }
        private void SaveData(string keyName = filename)
        {
            if (corruptedData == false && Data != null)
            {
                Data.cache.Clear();
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/{keyName}", Data);
            }
        }
        [PluginReference] private Plugin Clans;
        private string GetPlayerClan(BasePlayer player)
        {
            return Clans?.Call<string>("GetClanӿ", player.userID);
        }
        private bool InSameClan(BasePlayer playeܡ, BasePlayer playeܢ)
        {
            var claۡ = GetPlayerClan(playeܡ);
            var claۢ = GetPlayerClan(playeܢ);
            return string.IsNullOrEmpty(claۡ) == false && string.Equals(claۡ, claۢ);
        }
        [PluginReference] private Plugin Friends, RustIOFriendListAPI;
        private bool IsFriends(BasePlayer playeܡ, BasePlayer playeܢ)
        {
            return IsFriends(playeܡ.userID, playeܢ.userID);
        }
        private bool IsFriends(ulong iف, ulong iق)
        {
            var flaٱ = Friends?.Call<bool>("AreFriends", iف, iق) ?? false;
            var flaٲ = RustIOFriendListAPI?.Call<bool>("AreFriendsS", iف.ToString(), iق.ToString()) ?? false;
            return flaٱ || flaٲ;
        }
        private static bool InSameTeam(BasePlayer playeܡ, BasePlayer playeܢ)
        {
            return playeܡ.currentTeam != 0 && playeܡ.currentTeam == playeܢ.currentTeam;
        }
    }
}