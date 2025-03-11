

using System.Collections.Generic;
using System.Linq;
using System;
using Newtonsoft.Json.Converters;
using UnityEngine;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using System.Globalization;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core;
using Oxide.Plugins.SimpleStatusExtensionMethods;

namespace Oxide.Plugins
{
    [Info("SimpleStatus", "mr01sam", "1.2.2")]
    [Description("Allows plugins to add custom status displays for the UI.")]
    partial class SimpleStatus : CovalencePlugin
    {
        /* // Changelog
[v1.2.0]
-SimpleStatus now works together with AdvancedStatus. You can now have both installed on your server and they wont overlap with each other. Might be a few bugs with this integration, just report them if you see them.
-Added progress bars, which mimic the style of the health/thirst/hunger/vehicle durability bars
-Added new hook for OnSimpleStatusReady that is called after the plugin has finished loading and is called when a plugin that uses SimpleStatus is loaded, ideal for initializing statuses
-Added a rank property that can determine the priority of listing custom statuses
-Updated some API endpoints, old ones still exist but the new ones are recommended to use
-Fixed issue where statuses wouldnt account for horse stamina
-Updated rendering so it should be quicker to redraw
[v1.2.1]
-Fixed issue where statuses would be invisible after dying
-Fixed issue where holding a planner would mess up the statuses
[v1.2.2]
-Fixed issue where the SetColor api wouldnt work
-Fixed issue where statuses would appear at the wrong index if the color was changed
-Fixed issue where the title/text would not be translated if changed
         */

        public static SimpleStatus PLUGIN;

        [PluginReference]
        private readonly Plugin ImageLibrary, CustomStatusFramework, AdvancedStatus;

        private readonly bool Debugging = false; // If you are a developer, you can enable this to get console logs.
        private readonly int[] DebugCodes = new int[] { }; // Add codes here for specific debug statements you want to see, empty array will show all.

        #region Init
        private void Init()
        {
            PLUGIN = this;
        }

        private void OnServerInitialized()
        {
            if (!ImageLibrary?.IsLoaded ?? true)
            {
                PrintError("ImageLibary is REQUIRED for this plugin to work properly. Please load it onto your server and reload this plugin.");
                return;
            }
            if (CustomStatusFramework?.IsLoaded ?? false)
            {
                PrintError("You have both Simple Status and Custom Status Framework installed. These plugins do the same thing and will conflict with each other. Please unload Custom Status Framework and reload this plugin.");
                return;
            }
            AddCovalenceCommand(config.ToggleStatusCommand, nameof(CmdToggleStatus));
            SubscribeAll(
                nameof(OnPlayerConnected),
                nameof(OnPlayerDisconnected),
                nameof(CanPickupEntity),
                nameof(OnItemPickup),
                nameof(OnStructureUpgrade),
                nameof(OnStructureRepair),
                nameof(OnCupboardAuthorize),
                nameof(OnCupboardDeauthorize),
                nameof(OnDispenserGather),
                nameof(OnCollectiblePickup)
            );
            LoadData();
            if (UsingAdvancedStatus)
            {
                Puts("AdvancedStatus will be used in place of SimpleStatus for your plugins, you still need both though!");
            }
            Interface.CallHook("OnSimpleStatusReady");
            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
        }

        private void Unload()
        {
            UnsubscribeAll();
            SaveData();
            foreach (var pair in Behaviours)
            {
                foreach (var status in Data.Statuses.Keys)
                {
                    pair.Value.RemoveStatus(status);
                }
                UnityEngine.Object.Destroy(pair.Value);
            }
        }

        private void OnPluginLoaded(Plugin plugin)
        {
            NextTick(() =>
            {
                plugin?.CallHook("OnSimpleStatusReady");
            });
        }

        private void OnPluginUnloaded(Plugin plugin)
        {
            var name = plugin?.Name;
            if (name == Name) { return; }
            if (Data.Statuses.Values.Any(x => x.PluginName == name))
            {
                RemovePluginData(name);
            }
        }
        #endregion

        #region Subscribing
        public List<string> SubscribedHooks = new List<string>();
        private void SubscribeAll(params string[] hooks)
        {
            foreach (var hook in hooks)
            {
                SubscribedHooks.Add(hook);
                Subscribe(hook);
            }
        }
        private void UnsubscribeAll()
        {
            foreach (var hook in SubscribedHooks)
            {
                Unsubscribe(hook);
            }
        }
        #endregion

        #region Oxide Hooks
        private void OnPlayerConnected(BasePlayer basePlayer)
        {
            if (UsingAdvancedStatus)
            {
                return;
            }
            var obj = basePlayer.gameObject.AddComponent<StatusBehaviour>();
            Behaviours.Add(basePlayer.UserIDString, obj);
            Debug($"Connecting {basePlayer.displayName} has {(Data.Player.ContainsKey(basePlayer.UserIDString) ? Data.Player.GetValueOrDefault(basePlayer.UserIDString)?.Count : 0)} statuses: {Data.Player.GetValueOrDefault(basePlayer.UserIDString)?.Keys.ToSentence()}");
            NextFrame(() =>
            {
                Debug($"Resuming statuses for {basePlayer.displayName}..");
                if (Data.Player.ContainsKey(basePlayer.UserIDString))
                {
                    foreach (var data in Data.Player[basePlayer.UserIDString])
                    {
                        var statusName = data.Key;
                        Debug($"Resuming {data.Key} {data.Value.Duration} {data.Value.Title} {data.Value.Text} {data.Value.EndTime.HasValue} {data.Value.DurationUntilEndTime}");
                        obj.SetStatus(data.Key, data.Value.Duration, !data.Value.EndTime.HasValue, true);
                    }
                }
                if (config.WarnPlayersThatStatusIsHidden && Data.PlayersHiding.Contains(basePlayer.UserIDString))
                {
                    Message(basePlayer, Lang(PLUGIN, "warning", basePlayer.UserIDString, config.ToggleStatusCommand));
                }
            });
        }

        private void OnPlayerDisconnected(BasePlayer basePlayer)
        {
            var obj = Behaviours.GetValueOrDefault(basePlayer.UserIDString);
            if (obj == null) { return; }
            UnityEngine.Object.Destroy(obj);
            Behaviours.Remove(basePlayer.UserIDString);
            Debug($"Disconnecting {basePlayer.displayName} has {(Data.Player.ContainsKey(basePlayer.UserIDString) ? Data.Player[basePlayer.UserIDString].Count : 0)} statuses: {Data.Player.GetValueOrDefault(basePlayer.UserIDString)?.Keys.ToSentence()}");
        }

        private void CanPickupEntity(BasePlayer basePlayer, BaseEntity entity)
        {
            if (basePlayer == null || entity == null) { return; }
            var name = entity?.name;
            NextTick(() =>
            {
                if (basePlayer != null && name != null)
                {
                    Behaviours.GetValueOrDefault(basePlayer.UserIDString)?.itemStatuses.Inc(name);
                }
            });
        }

        private void OnItemPickup(Item item, BasePlayer basePlayer)
        {
            if (basePlayer == null || item == null) { return; }
            Behaviours.GetValueOrDefault(basePlayer.UserIDString)?.itemStatuses.Inc(item.info.shortname);
        }

        private void OnStructureUpgrade(BaseCombatEntity entity, BasePlayer basePlayer, BuildingGrade.Enum grade)
        {
            if (basePlayer == null) { return; }
            Behaviours.GetValueOrDefault(basePlayer.UserIDString)?.itemStatuses.Inc($"removed {grade}");
        }

        private void OnStructureRepair(BuildingBlock entity, BasePlayer basePlayer)
        {
            NextTick(() =>
            {
                if (entity != null && basePlayer != null)
                {
                    Behaviours.GetValueOrDefault(basePlayer.UserIDString)?.itemStatuses.Inc($"removed {entity.grade}");
                }
            });
        }

        private void OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer basePlayer)
        {
            NextTick(() =>
            {
                var b = Behaviours.GetValueOrDefault(basePlayer?.UserIDString);
                if (b == null) { return; }
                b.ForceCheckModifiers();
                b.ForceUpdateUI();
            });
        }

        private void OnCupboardDeauthorize(BuildingPrivlidge privilege, BasePlayer basePlayer)
        {
            NextTick(() =>
            {
                var b = Behaviours.GetValueOrDefault(basePlayer?.UserIDString);
                if (b == null) { return; }
                b.ForceCheckModifiers();
                b.ForceUpdateUI();
            });
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BasePlayer basePlayer, Item item)
        {
            if (basePlayer == null || item == null) { return; }
            Behaviours.GetValueOrDefault(basePlayer.UserIDString)?.itemStatuses.Inc(item.info.shortname);
        }

        private void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer basePlayer)
        {
            if (basePlayer == null) { return; }
            collectible.itemList.ForEach(item =>
            {
                Behaviours.GetValueOrDefault(basePlayer.UserIDString)?.itemStatuses.Inc(item.itemDef.shortname);
            });
        }
        #endregion

        #region Status Info
        protected class SavedData
        {
            public Dictionary<string, StatusInfo> Statuses = new Dictionary<string, StatusInfo>();
            public HashSet<string> PlayersHiding = new HashSet<string>();
            public Dictionary<string, Dictionary<string, PlayerStatusInfo>> Player = new Dictionary<string, Dictionary<string, PlayerStatusInfo>>();
        }

        private static string GetImageData(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }
            else if (path.IsAssetPath())
            {
                return path;
            }
            else if (path.IsItemId())
            {
                return path.AsItemId();
            }
            else if (path.IsRawImage())
            {
                return PLUGIN.ImageLibrary?.Call<string>("GetImage", path.AsRawImage()) ?? string.Empty;
            }
            return PLUGIN.ImageLibrary?.Call<string>("GetImage", path) ?? string.Empty;
        }

        [JsonConverter(typeof(StringEnumConverter))]
        protected enum StatusType
        {
            titletext,
            progress
        }

        protected class PlayerStatusInfo
        {
            public int Duration;
            public string Color = null;
            public string Title = null;
            public string TitleColor = null;
            public string Text = null;
            public string TextColor = null;
            public string ImagePathOrUrl = null;
            public string IconColor = null;
            public string Progress = null;
            public string ProgressColor = null;
            public string UserId = "0";
            public StatusType StatusType = StatusType.titletext;
            [JsonIgnore]
            public string ImageData => GetImageData(ImagePathOrUrl);
            public DateTime? EndTime = null;
            [JsonIgnore]
            public bool IsPastEndTime => EndTime.HasValue && EndTime.Value < DateTime.Now;
            [JsonIgnore]
            public int DurationUntilEndTime => !EndTime.HasValue ? Duration : (int)Math.Floor((EndTime.Value.Subtract(DateTime.Now)).TotalSeconds);
            [JsonIgnore]
            public bool Drawn = false;
        }

        private SavedData _data = new SavedData();

        protected static SavedData Data => PLUGIN._data;

        private void RemovePluginData(string pluginName)
        {
            Debug($"Removing {pluginName} because its no longer loaded");
            var statuses = Data.Statuses.Where(x => x.Value.PluginName == pluginName).Select(x => x.Key).ToArray();
            foreach (var status in statuses)
            {
                Data.Statuses.Remove(status);
            }
            var userIdsToUpdate = new HashSet<string>();
            foreach (var playerData in Data.Player.ToArray())
            {
                var userId = playerData.Key;
                foreach (var key in playerData.Value.Keys.ToArray())
                {
                    if (statuses.Contains(key)) { Data.Player[userId].Remove(key); }
                }
                userIdsToUpdate.Add(userId);
            }
            foreach (var userId in userIdsToUpdate)
            {
                var behavior = Behaviours.GetValueOrDefault(userId); if (behavior == null) { continue; }
                behavior.rowsNeedUpdate = true;
                var basePlayer = BasePlayer.Find(userId);
                if (basePlayer != null)
                {
                    foreach(var status in statuses)
                    {
                        CuiHelper.DestroyUi(basePlayer, string.Format(UI_Status_ID, status));
                    }
                }
            }
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/data", Data);
        }

        private void LoadData()
        {
            Debug("Load data called");
            var data = Interface.Oxide.DataFileSystem.ReadObject<SavedData>($"{Name}/data") ?? new SavedData();
            if (data.Statuses == null)
            {
                data.Statuses = new Dictionary<string, StatusInfo>();
            }
            if (data.Player == null)
            {
                data.Player = new Dictionary<string, Dictionary<string, PlayerStatusInfo>>();
            }
            _data.Player = data.Player;
            if (data.Statuses == null)
            {
                data.Statuses = new Dictionary<string, StatusInfo>();
            }
            foreach (var status in data.Statuses.ToArray())
            {
                if (!status.Value.Plugin?.IsLoaded ?? true)
                {
                    RemovePluginData(status.Value.PluginName);
                }
                else if (!_data.Statuses.ContainsKey(status.Key))
                {
                    Debug("Assigned new status from load");
                    _data.Statuses[status.Key] = status.Value;
                }
            }
        }


        private static void Debug(string message)
        {
            if (PLUGIN == null || !PLUGIN.Debugging) { return; }
            var code = message.Split(" ").First().GetHashCode();
            if (PLUGIN.DebugCodes != null && PLUGIN.DebugCodes.Length > 0 && !PLUGIN.DebugCodes.Contains(code)) { return; }
            PLUGIN?.Puts($"DEBUG({code}): {message}");
        }

        protected class StatusInfo
        {
            [JsonIgnore]
            public Plugin Plugin => Interface.uMod.RootPluginManager.GetPlugin(PluginName);
            [JsonIgnore]
            public bool PluginIsLoaded => Plugin?.IsLoaded ?? false;
            public string PluginName;
            public string Id;
            public string Color;
            public string Title;
            public string TitleColor;
            public string Text = null;
            public string TextColor;
            public string ImageLibraryNameOrAssetPath;
            public string Progress = null;
            public string ProgressColor = "0 0 0 0.9";
            public int Rank = 0;
            public StatusType Type = StatusType.titletext;
            [JsonProperty("ImageLibraryIconId")]
            private string ImageLibraryIconId // old version
            {
                set { ImageLibraryNameOrAssetPath = value; }
            }
            public string IconColor;
            [JsonIgnore]
            public bool IsAssetImage => !string.IsNullOrEmpty(ImageLibraryNameOrAssetPath) && ImageLibraryNameOrAssetPath.IsAssetPath();
            [JsonIgnore]
            public bool HasImage => !string.IsNullOrEmpty(ImageLibraryNameOrAssetPath);
            [JsonIgnore]
            public string ImageData => GetImageData(ImageLibraryNameOrAssetPath);
        }
        #endregion

        #region Utility
        private static void Message(BasePlayer basePlayer, string message)
        {
            var icon = PLUGIN.config.ChatMessageSteamId;
            ConsoleNetwork.SendClientCommand(basePlayer.Connection, "chat.add", 2, icon, message);
        }

        public static string Json(CuiElement element)
        {
            return JsonConvert.SerializeObject(element, Formatting.None, new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.Ignore
            });
        }

        #endregion
    }
}

namespace Oxide.Plugins
{
    partial class SimpleStatus : CovalencePlugin
    {
        [HookMethod(nameof(CreateStatus))]
        private void CreateStatus(Plugin plugin, string statusId, string backgroundColor = "1 1 1 1", string title = "Text", string titleColor = "1 1 1 1", string text = null, string textColor = "1 1 1 1", string imageLibraryNameOrAssetPath = null, string imageColor = "1 1 1 1")
        {
            CreateStatus(plugin, statusId, new Dictionary<string, object>
            {
                ["color"] = backgroundColor,
                ["title"] = title,
                ["titleColor"] = titleColor,
                ["text"] = text,
                ["textColor"] = textColor,
                ["icon"] = imageLibraryNameOrAssetPath,
                ["iconColor"] = imageColor
            });
        }

        [HookMethod(nameof(CreateStatus))]
        private void CreateStatus(Plugin plugin, string statusId, Dictionary<string, object> properties)
        {
            Debug("CreateStatus called");
            Data.Statuses[statusId] = new StatusInfo
            {
                PluginName = plugin.Name,
                Id = statusId,
                Color = properties.GetProperty("color", string.Empty),
                Title = properties.GetProperty("title", string.Empty),
                TitleColor = properties.GetProperty("titleColor", string.Empty),
                Text = properties.GetProperty("text", string.Empty),
                TextColor = properties.GetProperty("textColor", string.Empty),
                ImageLibraryNameOrAssetPath = properties.GetProperty("icon", string.Empty),
                IconColor = properties.GetProperty("iconColor", string.Empty),
                Progress = properties.GetPropertyFloatOrString("progress"),
                ProgressColor = properties.GetProperty("progressColor", string.Empty),
                Rank = properties.GetProperty("rank", 0)
            };
        }

        [HookMethod(nameof(SetStatus))]
        private void SetStatus(ulong userId, string statusId, int duration = int.MaxValue, bool pauseOffline = true) => SetStatus(userId.ToString(), statusId, duration, pauseOffline);
        private void SetStatus(EncryptedValue<ulong> userId, string statusId, int duration = int.MaxValue, bool pauseOffline = true) => SetStatus(userId.Get().ToString(), statusId, duration, pauseOffline);
        private void SetStatus(string userId, string statusId, int duration = int.MaxValue, bool pauseOffline = true)
        {
            Debug($"SetStatus called {userId} {statusId} {duration} {pauseOffline}");
            #region AdvancedStatus
            if (UsingAdvancedStatus)
            {
                var createdStatus = Data.Statuses.GetValueOrDefault(statusId);
                if (createdStatus == null) { return; }
                var data = Data.Player.GetValueOrNew(userId).GetValueOrDefault(statusId);
                var userIdParsed = ulong.Parse(userId);
                if (duration == 0)
                {
                    AS_DeleteBar(userIdParsed, createdStatus);
                }
                else if ((bool)(AdvancedStatus?.Call("BarExists", userIdParsed, statusId, Name) ?? false))
                {
                    AS_SetDuration(userIdParsed, createdStatus, duration);
                }
                else
                {
                    AS_CreateBar(userIdParsed, createdStatus, data, duration);
                }
                return;
            }
            #endregion
            if (IsStatusIdInvalid(statusId)) { return; }

            var b = Behaviours.GetValueOrDefault(userId);
            if (b == null)
            {
                // save status for later if player is offline
                SetStatusForOfflinePlayer(userId, statusId, duration, pauseOffline);
                return;
            }
            if (duration > 0)
            {
                b.SetStatus(statusId, duration, pauseOffline);
            }
            else
            {
                b.RemoveStatus(statusId);
            }
        }

        [HookMethod(nameof(SetStatusColor))]
        private void SetStatusColor(ulong userId, string statusId, string color = null) => SetStatusColor(userId.ToString(), statusId, color);
        private void SetStatusColor(EncryptedValue<ulong> userId, string statusId, string color = null) => SetStatusColor(userId.Get().ToString(), statusId, color);
        private void SetStatusColor(string userId, string statusId, string color = null)
        {
            Debug("SetStatusColor called");
            if (IsStatusIdInvalid(statusId)) { return; }
            var b = Behaviours.GetValueOrDefault(userId); if (b == null) { return; }
            b.SetProperties(statusId, new Dictionary<string, object>
            {
                ["color"] = color ?? RESET_KEYWORD
            }, true);
        }

        [HookMethod(nameof(SetStatusTitle))]
        private void SetStatusTitle(ulong userId, string statusId, string title = null) => SetStatusTitle(userId.ToString(), statusId.ToString(), title);
        private void SetStatusTitle(EncryptedValue<ulong> userId, string statusId, string title = null) => SetStatusTitle(userId.Get().ToString(), statusId, title);
        private void SetStatusTitle(string userId, string statusId, string title = null)
        {
            Debug("SetStatusTitle called");
            if (IsStatusIdInvalid(statusId)) { return; }
            var b = Behaviours.GetValueOrDefault(userId); if (b == null) { return; }
            b.SetProperties(statusId, new Dictionary<string, object>
            {
                ["title"] = title ?? RESET_KEYWORD
            }, true);
        }

        [HookMethod(nameof(SetStatusTitleColor))]
        private void SetStatusTitleColor(ulong userId, string statusId, string color = null) => SetStatusTitleColor(userId.ToString(), statusId, color);
        private void SetStatusTitleColor(EncryptedValue<ulong> userId, string statusId, string color = null) => SetStatusColor(userId.Get().ToString(), statusId, color);
        private void SetStatusTitleColor(string userId, string statusId, string color = null)
        {
            Debug("SetStatusTitleColor called");
            if (IsStatusIdInvalid(statusId)) { return; }
            var b = Behaviours.GetValueOrDefault(userId); if (b == null) { return; }
            b.SetProperties(statusId, new Dictionary<string, object>
            {
                ["titleColor"] = color ?? RESET_KEYWORD
            }, true);
        }

        [HookMethod(nameof(SetStatusText))]
        private void SetStatusText(ulong userId, string statusId, string text = null) => SetStatusText(userId.ToString(), statusId, text);
        private void SetStatusText(EncryptedValue<ulong> userId, string statusId, string text = null) => SetStatusText(userId.Get().ToString(), statusId, text);
        private void SetStatusText(string userId, string statusId, string text = null)
        {
            Debug("SetStatusText called");
            if (IsStatusIdInvalid(statusId)) { return; }
            var b = Behaviours.GetValueOrDefault(userId); if (b == null) { return; }
            b.SetProperties(statusId, new Dictionary<string, object>
            {
                ["text"] = text ?? RESET_KEYWORD
            }, true);
        }

        [HookMethod(nameof(SetStatusTextColor))]
        private void SetStatusTextColor(ulong userId, string statusId, string color = null) => SetStatusText(userId.ToString(), statusId, color);
        private void SetStatusTextColor(EncryptedValue<ulong> userId, string statusId, string color = null) => SetStatusText(userId.Get().ToString(), statusId, color);
        private void SetStatusTextColor(string userId, string statusId, string color = null)
        {
            Debug("SetStatusTextColor called");
            if (IsStatusIdInvalid(statusId)) { return; }
            var b = Behaviours.GetValueOrDefault(userId); if (b == null) { return; }
            b.SetProperties(statusId, new Dictionary<string, object>
            {
                ["textColor"] = color ?? RESET_KEYWORD
            }, true);
        }

        [HookMethod(nameof(SetStatusIcon))]
        private void SetStatusIcon(ulong userId, string statusId, string imageLibraryNameOrAssetPath = null) => SetStatusIcon(userId.ToString(), statusId, imageLibraryNameOrAssetPath);
        private void SetStatusIcon(EncryptedValue<ulong> userId, string statusId, string imageLibraryNameOrAssetPath = null) => SetStatusIcon(userId.Get().ToString(), statusId, imageLibraryNameOrAssetPath);
        private void SetStatusIcon(string userId, string statusId, string imageLibraryNameOrAssetPath = null)
        {
            Debug("SetStatusIcon called");
            if (IsStatusIdInvalid(statusId)) { return; }
            var b = Behaviours.GetValueOrDefault(userId); if (b == null) { return; }
            b.SetProperties(statusId, new Dictionary<string, object>
            {
                ["icon"] = imageLibraryNameOrAssetPath ?? RESET_KEYWORD
            }, true);
        }

        [HookMethod(nameof(SetStatusIconColor))]
        private void SetStatusIconColor(ulong userId, string statusId, string color = null) => SetStatusIcon(userId.ToString(), statusId, color);
        private void SetStatusIconColor(EncryptedValue<ulong> userId, string statusId, string color = null) => SetStatusIcon(userId.Get().ToString(), statusId, color);
        private void SetStatusIconColor(string userId, string statusId, string color = null)
        {
            Debug("SetStatusIconColor called");
            if (IsStatusIdInvalid(statusId)) { return; }
            var b = Behaviours.GetValueOrDefault(userId); if (b == null) { return; }
            b.SetProperties(statusId, new Dictionary<string, object>
            {
                ["iconColor"] = color ?? RESET_KEYWORD
            }, true);
        }

        [HookMethod(nameof(SetStatusProperty))]
        private void SetStatusProperty(ulong userId, string statusId, Dictionary<string, object> properties) => SetStatusProperty(userId.ToString(), statusId, properties);
        private void SetStatusProperty(EncryptedValue<ulong> userId, string statusId, Dictionary<string, object> properties) => SetStatusProperty(userId.Get().ToString(), statusId, properties);
        private void SetStatusProperty(string userId, string statusId, Dictionary<string, object> properties)
        {
            Debug("SetStatusProperty called");

            if (UsingAdvancedStatus)
            {
                var status = Data.Statuses.GetValueOrDefault(statusId);
                if (status == null) { PrintWarning($"status {statusId} not found"); return; }
                var data = Data.Player.GetValueOrNew(userId).GetValueOrDefault(statusId);
                AS_UpdateBar(ulong.Parse(userId), status, data, properties);
                return;
            }

            if (IsStatusIdInvalid(statusId)) { return; }
            var b = Behaviours.GetValueOrDefault(userId); if (b == null) { return; }
            b.SetProperties(statusId, properties, !UsingAdvancedStatus);
        }

        [HookMethod(nameof(GetDuration))]
        private int GetDuration(ulong userId, string statusId) => GetDuration(userId.ToString(), statusId);
        private int GetDuration(EncryptedValue<ulong> userId, string statusId) => GetDuration(userId.Get().ToString(), statusId);
        private int GetDuration(string userId, string statusId)
        {
            Debug("GetDuration called");
            if (IsStatusIdInvalid(statusId)) { return 0; }
            if (!Data.Player.ContainsKey(userId) || !Data.Player[userId].ContainsKey(statusId)) { return 0; }
            return Data.Player[userId][statusId].Duration;
        }

        internal bool IsStatusIdInvalid(string statusId)
        {
            if (Data?.Statuses?.ContainsKey(statusId) ?? true) { return false; }
            if (Debugging) { PrintError($"There is no status with the id of '{statusId}'"); }
            return true;
        }

        /* Subscribable Hooks */

        /*
         * # Called when a status is initially set for a player.
         * void OnStatusSet(ulong userId, string statusId, int duration)
         * 
         * 
         * # Called when a status is removed for a player. (When the duration reaches 0).
         * void OnStatusEnd(ulong userId, string statusId, int duration)
         * 
         * 
         * # Called when a status property is updated.
         * # The 'property' parameter can be: 'title', 'titleColor', 'text', 'textColor', 'icon', 'iconColor', 'color'
         * void OnStatusUpdate(ulong userId, string statusId, string property, string value);
         */
    }
}

namespace Oxide.Plugins
{
    partial class SimpleStatus : CovalencePlugin
    {
        private void CmdToggleStatus(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null) { return; }
            if (!Data.PlayersHiding.Contains(basePlayer.UserIDString))
            {
                Data.PlayersHiding.Add(basePlayer.UserIDString);
                CuiHelper.DestroyUi(basePlayer, UI_Base_ID);
                Message(basePlayer, Lang(PLUGIN, "hiding", basePlayer.UserIDString));
            }
            else
            {
                Data.PlayersHiding.Remove(basePlayer.UserIDString);
                var b = Behaviours.GetValueOrDefault(basePlayer.UserIDString); if (b == null) { return; }
                b.InitStatusUI();
                b.rowsNeedUpdate = true;
                Message(basePlayer, Lang(PLUGIN, "showing", basePlayer.UserIDString));
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class SimpleStatus : CovalencePlugin
    {
        private Configuration config;

        private partial class Configuration
        {
            public ulong ChatMessageSteamId = 0;
            public string ToggleStatusCommand = "ts";
            public bool WarnPlayersThatStatusIsHidden = true;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        protected override void LoadDefaultConfig() => config = new Configuration();
    }
}

namespace Oxide.Plugins.SimpleStatusExtensionMethods
{
    public static class ExtensionMethods
    {
        public static void RemoveAll<TKey, TValue>(this Dictionary<TKey, TValue> dict, Func<KeyValuePair<TKey, TValue>, bool> condition)
        {
            foreach (var cur in dict.Where(condition).ToList())
            {
                dict.Remove(cur.Key);
            }
        }

        public static void Inc<TKey>(this Dictionary<TKey, float> dict, TKey key)
        {
            dict[key] = Time.realtimeSinceStartup + 3.6f;
        }

        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (T element in source) action(element);
        }

        public static bool IsEmpty(this string text) => string.IsNullOrEmpty(text);
        public static bool IsBlank(this string text) => string.IsNullOrWhiteSpace(text);

        public static bool IsAssetPath(this string path) => !path.IsBlank() && path.StartsWith("assets/");

        public static bool IsItemId(this string path) => !path.IsBlank() && path.StartsWith("itemid:");

        public static bool IsRawImage(this string path) => !path.IsBlank() && path.StartsWith("raw:");

        public static string AsRawImage(this string path) => !IsRawImage(path) ? "" : $"{path.Substring(4)}";

        public static string AsItemId(this string path) => !IsItemId(path) ? "" : path.Substring(7);
        public static T GetProperty<T>(this Dictionary<string, object> dict, string key, T defaultValue) => (T)dict.GetValueOrDefault(key, defaultValue);

        public static string GetPropertyFloatOrString(this Dictionary<string, object> dict, string key)
        {
            try
            {
                var floatVal = (float)dict.GetValueOrDefault(key);
                return floatVal.ToString();
            } catch
            {
                return dict.GetValueOrDefault(key, string.Empty).ToString();
            }
        }

        public static void CopyFrom<V>(this Dictionary<string, V> dict, Dictionary<string, V> other, string key1, string key2, Func<V, V> clean = null)
        {
            if (other.ContainsKey(key2))
            {
                dict[key1] = clean == null ? other[key2] : clean.Invoke(other[key2]);
            }
        }

        public static V GetValueOrNew<K, V>(this Dictionary<K, V> dict, K key) where V : new()
        {
            var value = dict.GetValueOrDefault(key);
            if (value != null) { return value; }
            dict[key] = new V();
            return dict[key];
        }

        public static string FormatDuration(this int duration)
        {
            var ts = TimeSpan.FromSeconds(duration);
            return ts.TotalDays >= 1 ? $"{Math.Floor(ts.TotalDays):0}d {ts.Hours}h {ts.Minutes}m" :
                ts.TotalHours >= 1 ? $"{Math.Floor(ts.TotalHours):0}h {ts.Minutes}m" :
                ts.TotalMinutes >= 1 ? $"{Math.Floor(ts.TotalMinutes):0}m" :
                $"{ts.TotalSeconds:0}";
        }

        public static string CheckReset(this string value, string defaultVal)
        {
            if (value == SimpleStatus.RESET_KEYWORD) { return defaultVal; }
            return value;
        }

        public static T GetProperty<T>(this Dictionary<string, object> dict, string key) => (T)dict.GetValueOrDefault(key, null);
    }
}

namespace Oxide.Plugins
{
    partial class SimpleStatus : CovalencePlugin
    {
        #region Advanced Status Integration
        public bool UsingAdvancedStatus => AdvancedStatus?.IsLoaded ?? false;

        protected Dictionary<string, StatusInfo> AS_CreatedStatuses = new Dictionary<string, StatusInfo>();

        protected void AS_DeleteBar(ulong userId, StatusInfo status)
        {
            AdvancedStatus?.Call("DeleteBar", userId, status.Id, status.PluginName);
        }

        protected void AS_SetDuration(ulong userId, StatusInfo status, int duration)
        {
            if (duration == 0)
            {
                AS_DeleteBar(userId, status);
                return;
            }
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "Id", status.Id },
                { "Plugin", status.Plugin }
            };
            if (duration != int.MaxValue)
            {
                parameters["TimeStamp"] = Network.TimeEx.currentTimestamp + duration;
                parameters["TimeStampStart"] = Network.TimeEx.currentTimestamp;
                parameters["TimeStampDestroy"] = Network.TimeEx.currentTimestamp + duration;
            }
            AdvancedStatus?.Call("UpdateContent", userId, parameters);
        }

        protected void AS_CreateBar(ulong userId, StatusInfo status, PlayerStatusInfo data, int duration)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                ["Id"] = status.Id,
                ["BarType"] = (duration != int.MaxValue && status.Text == null && data?.Text == null) ? "TimeCounter" : "Default",
                ["Plugin"] = status.PluginName,
                ["Main_Color"] = RgbaToHex(Opts(data?.Color, status.Color, null, (x) => x == null)),
                ["Main_Transparency"] = RgbaToAlpha(Opts(data?.Color, status.Color, null, (x) => x == null)),
                ["Text"] = Lang(status.Plugin, Opts(data?.Title, status.Title, string.Empty, (x) => x == null), userId),
                ["Text_Color"] = RgbaToHex(Opts(data?.TitleColor, status.TitleColor, null, (x) => x == null)),
                ["SubText"] = Lang(status.Plugin, Opts(data?.Text, status.Text, null, (x) => x == null), userId),
                ["SubText_Color"] = RgbaToHex(Opts(data?.TextColor, status.TextColor, null, (x) => x == null)),
                ["Progress"] = Opts(data?.Progress, status.Progress, null, (x) => x == null),
                ["Progress_Color"] = RgbaToHex(Opts(data?.ProgressColor, status.ProgressColor, null, (x) => x == null)),
                ["Text_Offset_Horizontal"] = Opts(data?.Progress, status.Progress, null, (x) => x.IsBlank()) != null ? 6 : 0
            };
            if (duration != int.MaxValue)
            {
                parameters["TimeStamp"] = Network.TimeEx.currentTimestamp + duration;
                parameters["TimeStampStart"] = Network.TimeEx.currentTimestamp;
                parameters["TimeStampDestroy"] = Network.TimeEx.currentTimestamp + duration;
            }
            if (!status.ImageLibraryNameOrAssetPath.IsBlank())
            {
                parameters["Image_Color"] = RgbaToHex(Opts(data?.IconColor, status.IconColor, null, (x) => x == null));
                parameters["Image_Transparency"] = RgbaToAlpha(Opts(data?.IconColor, status.IconColor, null, (x) => x == null));
                if (status.ImageLibraryNameOrAssetPath?.IsAssetPath() ?? false)
                {
                    parameters["Image_Sprite"] = status.ImageLibraryNameOrAssetPath;
                }
                else
                {
                    parameters["Image"] = status.ImageLibraryNameOrAssetPath;
                }
            }
            AdvancedStatus?.Call("CreateBar", userId, parameters);
        }

        protected void AS_UpdateBar(ulong userId, StatusInfo status, PlayerStatusInfo data, Dictionary<string, object> ssprops)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                ["Id"] = status.Id,
                ["Plugin"] = status.PluginName,
            };
            parameters.CopyFrom(ssprops, "Main_Color", "color", (x) => RgbaToHex(x.ToString()));
            parameters.CopyFrom(ssprops, "Text", "title", (x) => Lang(status.Plugin, x.ToString(), userId));
            parameters.CopyFrom(ssprops, "Text_Color", "titleColor", (x) => RgbaToHex(x.ToString()));
            parameters.CopyFrom(ssprops, "Progress", "progress", (x) => (float)x);
            parameters.CopyFrom(ssprops, "ProgressColor", "progressColor", (x) => RgbaToHex(x.ToString()));
            AdvancedStatus?.Call("UpdateContent", userId, parameters);
        }

        public static T Opts<T>(T one, T two, T three, Func<T, bool> isnull)
        {
            return !isnull.Invoke(one) ? one : !isnull.Invoke(two) ? two : three;
        }

        public static string RgbaToHex(string rgba)
        {
            if (rgba.IsBlank())
            {
                return string.Empty;
            }
            var components = rgba.Split(' ');
            int r = (int)(float.Parse(components[0], CultureInfo.InvariantCulture) * 255);
            int g = (int)(float.Parse(components[1], CultureInfo.InvariantCulture) * 255);
            int b = (int)(float.Parse(components[2], CultureInfo.InvariantCulture) * 255);
            int a = (int)(float.Parse(components[3], CultureInfo.InvariantCulture) * 255);
            return $"#{r:X2}{g:X2}{b:X2}{a:X2}";
        }

        public static float RgbaToAlpha(string rgba)
        {
            if (rgba.IsBlank())
            {
                return 1f;
            }
            return float.Parse(rgba.Split(' ')[3]);
        }
        #endregion
    }
}

namespace Oxide.Plugins
{
    partial class SimpleStatus : CovalencePlugin
    {
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["showing"] = "Showing statuses.",
                ["hiding"] = "Hiding statuses.",
                ["warning"] = "You have statuses hidden. Use the /{0} command to show them again."
            }, this);
        }

        private string Lang(Plugin plugin, string key, string userId, params object[] args) => key.IsBlank() ? string.Empty : string.Format(lang.GetMessage(key, plugin, userId), args);

        private string Lang(Plugin plugin, string key, ulong userId, params object[] args) => key.IsBlank() ? string.Empty : string.Format(lang.GetMessage(key, plugin, userId.ToString()), args);
    }
}

namespace Oxide.Plugins
{
    partial class SimpleStatus : CovalencePlugin
    {
        private void SetStatusForOfflinePlayer(string userId, string statusId, int duration, bool pauseOffline)
        {
            if (duration <= 0)
            {
                var data = Data.Player.GetValueOrDefault(userId);
                if (data == null) { return; }
                Debug($"Remove status {statusId} for offline player {userId}");
                data.Remove(statusId);
                if (data.Count <= 0)
                {
                    Data.Player.Remove(userId);
                }
                return;
            }
            if (!Data.Player.ContainsKey(userId))
            {
                Data.Player[userId] = new Dictionary<string, PlayerStatusInfo>();
            }
            Debug($"Set status {statusId} for offline player {userId} duration {duration}");
            Data.Player[userId][statusId] = new PlayerStatusInfo() { Duration = duration, EndTime = pauseOffline ? null : (DateTime?)DateTime.Now.AddSeconds(duration) };
        }

        private List<DrawElement> drawing = new List<DrawElement>();

        private Dictionary<string, StatusBehaviour> Behaviours = new Dictionary<string, StatusBehaviour>();
        public int BehaviorCount = 0;
        public class StatusBehaviour : MonoBehaviour
        {
            private BasePlayer basePlayer;
            private int smallModifiersCount;
            private int bigModifiersCount;
            private int previousModifiersCount = 0;
            public bool rowsNeedUpdate = false;
            public bool privForceUpdate = false;
            public bool forceCheckMods = false;
            public Dictionary<string, float> itemStatuses = new Dictionary<string, float>();
            private float nextBigStatusUpdate;
            public int ModifiersCount => smallModifiersCount + bigModifiersCount;
            public string[] ActiveStatusIds => !Data.Player.ContainsKey(UserId) ? new string[] { } : Data.Player[UserId].Keys.ToArray();
            public string UserId => basePlayer.UserIDString;
            private bool inBuildingPriv = false;
            private ulong lastPrivId = 0;

            #region Private

            private void Awake()
            {
                basePlayer = GetComponent<BasePlayer>();
                PLUGIN.BehaviorCount++;
            }
            private void OnDestroy()
            {
                CuiHelper.DestroyUi(basePlayer, UI_Base_ID);
                PLUGIN.BehaviorCount--;
            }

            private void StartWorking()
            {
                InitStatusUI();
                RepeatCheckModifiers();
                RepeatUpdateUI();
                nextBigStatusUpdate = 0;

                InvokeRepeating(nameof(RepeatCheckDurations), 1f, 1f);
                InvokeRepeating(nameof(RepeatUpdateUI), 0.2f, 0.2f);
                InvokeRepeating(nameof(RepeatCheckModifiers), 0.2f, 0.2f);
            }

            private void StopWorking()
            {
                CancelInvoke(nameof(RepeatCheckDurations));
                CancelInvoke(nameof(RepeatUpdateUI));
                CancelInvoke(nameof(RepeatCheckModifiers));
                CuiHelper.DestroyUi(basePlayer, UI_Base_ID);
            }

            public void InitStatusUI()
            {
                if (Data.PlayersHiding.Contains(UserId)) { return; }
                CuiHelper.DestroyUi(basePlayer, UI_Base_ID);
                CuiHelper.AddUi(basePlayer, PLUGIN.UI_Base());
            }

            private void RepeatCheckDurations()
            {
                if (basePlayer.IsSleeping())
                {
                    rowsNeedUpdate = true;
                    return;
                }
                foreach (var statusId in ActiveStatusIds)
                {
                    var status = Data.Statuses.GetValueOrDefault(statusId); if (status == null) { continue; }
                    var data = Data.Player.GetValueOrDefault(UserId)?.GetValueOrDefault(statusId); if (data == null) { continue; }
                    var duration = data.Duration;
                    if (duration >= int.MaxValue) { continue; } // No need to update duration
                    data.Duration -= 1;
                    if (data.Duration <= 0 || data.IsPastEndTime)
                    {
                        if (data.IsPastEndTime)
                        {
                            Debug($"Current: {DateTime.Now.ToLongTimeString()} EndTime: {data.EndTime.Value.ToLongTimeString()}");
                        }
                        RemoveStatus(statusId);
                    }
                    else if (status.Text == null && data.Text == null && !Data.PlayersHiding.Contains(basePlayer.UserIDString))
                    {
                        // Update duration text
                        PLUGIN.drawing.Clear();
                        PLUGIN.drawing.Add(PLUGIN.Element_Text(status, data, update: true));
                        PLUGIN.Draw(basePlayer, PLUGIN.drawing);
                    }
                }
            }

            public void ForceCheckModifiers()
            {
                forceCheckMods = true;
                RepeatCheckModifiers();
            }

            public void ForceUpdateUI()
            {
                RepeatUpdateUI();
            }

            private void RepeatUpdateUI()
            {
                if (!rowsNeedUpdate && !privForceUpdate) { return; }
                if (Data.PlayersHiding.Contains(basePlayer.UserIDString))
                {
                    rowsNeedUpdate = false;
                    privForceUpdate = false;
                    return;
                }
                if (basePlayer.IsSleeping() && basePlayer.IsDead())
                {
                    foreach(var statusId in ActiveStatusIds)
                    {
                        var data = Data.Player.GetValueOrDefault(UserId)?.GetValueOrDefault(statusId); if (data == null) { continue; }
                        data.Drawn = false;
                        CuiHelper.DestroyUi(basePlayer, string.Format(UI_Status_ID, statusId));
                    }
                    return;
                }
                Debug($"RepeatUpdateUI {ActiveStatusIds.Length} RowUpdate={rowsNeedUpdate} PrivUpdate={privForceUpdate}");
                var index = ModifiersCount;
                PLUGIN.drawing.Clear();
                var orderedStatuses = ActiveStatusIds.Select(statusId => Data.Statuses.GetValueOrDefault(statusId)).Where(x => x != null).OrderBy(x => x.Rank);
                foreach (var status in orderedStatuses)
                {
                    //var status = Data.Statuses.GetValueOrDefault(statusId); if (status == null) { continue; }
                    if (!status.PluginIsLoaded)
                    {
                        RemoveStatus(status.Id);
                        continue;
                    }
                    var data = Data.Player.GetValueOrDefault(UserId)?.GetValueOrDefault(status.Id); if (data == null) { continue; }

                    Debug($"STATUS={status.Id} DRAWN={data.Drawn}");
                    if (!data.Drawn)
                    {
                        PLUGIN.drawing.Add(PLUGIN.Element_Background(status, data, index, update: false));
                        PLUGIN.drawing.Add(PLUGIN.Element_Icon(status, data, update: false));
                        PLUGIN.drawing.Add(PLUGIN.Element_Progress(status, data, update: false));
                        PLUGIN.drawing.Add(PLUGIN.Element_Title(status, data, update: false));
                        PLUGIN.drawing.Add(PLUGIN.Element_Text(status, data, update: false));
                        data.Drawn = true;
                    }
                    else
                    {
                        PLUGIN.drawing.Add(PLUGIN.Element_Background(status, data, index, update: true));
                    }
                    index++;
                }
                PLUGIN.Draw(basePlayer, PLUGIN.drawing);
                rowsNeedUpdate = false;
                privForceUpdate = false;
                if (ActiveStatusIds.Length <= 0)
                {
                    StopWorking();
                }
            }


            private readonly string[] ShowBuldingBlockedItems = new string[]
            {
                "hammer",
                "building.planner",
                "toolgun"
            };

            private void RepeatCheckModifiers()
            {
                smallModifiersCount = 0;

                if (basePlayer.metabolism.bleeding.value >= 1)
                {
                    smallModifiersCount++; // bleeding
                }
                if (basePlayer.metabolism.temperature.value < 5)
                {
                    smallModifiersCount++; // toocold
                }
                if (basePlayer.metabolism.temperature.value > 40)
                {
                    smallModifiersCount++; // toohot
                }
                if (basePlayer.currentComfort > 0)
                {
                    smallModifiersCount++; // comfort
                }
                if (basePlayer.metabolism.calories.value < 40)
                {
                    smallModifiersCount++; // starving
                }
                if (basePlayer.metabolism.hydration.value < 35)
                {
                    smallModifiersCount++; // dehydrated
                }
                if (basePlayer.metabolism.radiation_poison.value > 0)
                {
                    smallModifiersCount++; // radiation
                }
                if (basePlayer.metabolism.wetness.value >= 0.02)
                {
                    smallModifiersCount++; // wet
                }
                if (basePlayer.metabolism.oxygen.value < 1f)
                {
                    smallModifiersCount++; // drowning
                }
                if (basePlayer.currentCraftLevel > 0)
                {
                    smallModifiersCount++; // workbench
                }
                if (basePlayer.inventory.crafting.queue.Count > 0)
                {
                    smallModifiersCount++; // crafting
                }
                if (basePlayer.modifiers.ActiveModifierCoount > 0)
                {
                    smallModifiersCount++; // modifiers
                }
                if (basePlayer.HasPlayerFlag(BasePlayer.PlayerFlags.SafeZone))
                {
                    smallModifiersCount++; // safezone
                }
                if (basePlayer.isMounted)
                {
                    var vehicle = basePlayer.GetMountedVehicle();
                    if (vehicle?.shouldShowHudHealth ?? false)
                    {
                        smallModifiersCount++; // vehicle durability
                    }
                    if (vehicle is RidableHorse)
                    {
                        smallModifiersCount++; // horse health
                    }
                }

                // Other stats
                if (forceCheckMods || nextBigStatusUpdate < Time.realtimeSinceStartup)
                {
                    var stillInBuildingPriv = false;

                    bigModifiersCount = 0;
                    var priv = basePlayer.GetBuildingPrivilege();
                    if (priv != null && priv.IsAuthed(basePlayer))
                    {
                        bigModifiersCount++; // buildpriv authed
                        bigModifiersCount++; // upkeep
                        inBuildingPriv = true;
                        stillInBuildingPriv = true;
                    }
                    else if (priv != null && !priv.IsAuthed(basePlayer) && ShowBuldingBlockedItems.Contains(basePlayer.GetActiveItem()?.info.shortname))
                    {
                        bigModifiersCount++; // buildpriv not authed
                        inBuildingPriv = true;
                        stillInBuildingPriv = true;
                    }
                    if (inBuildingPriv && !stillInBuildingPriv) // Raid Protection relies on this
                    {
                        Interface.CallHook("OnStatusEnd", UserId, "simplestatus.buildingpriv", 0);
                        inBuildingPriv = false;
                    }
                    if ((priv == null && lastPrivId != 0) || (priv != null && lastPrivId != priv.net.ID.Value))
                    {
                        lastPrivId = priv == null ? 0 : priv.net.ID.Value;
                        privForceUpdate = true;
                    }
                    nextBigStatusUpdate = Time.realtimeSinceStartup + 2;
                }
                itemStatuses.RemoveAll(x => x.Value < Time.realtimeSinceStartup);
                smallModifiersCount += itemStatuses.Count;
                if (ModifiersCount != previousModifiersCount)
                {
                    previousModifiersCount = ModifiersCount;
                    rowsNeedUpdate = true;
                }
                forceCheckMods = false;
            }

            private string FormatDuration(int duration)
            {
                var ts = TimeSpan.FromSeconds(duration);
                return ts.TotalDays >= 1 ? $"{Math.Floor(ts.TotalDays):0}d {ts.Hours}h {ts.Minutes}m" :
                    ts.TotalHours >= 1 ? $"{Math.Floor(ts.TotalHours):0}h {ts.Minutes}m" :
                    ts.TotalMinutes >= 1 ? $"{Math.Floor(ts.TotalMinutes):0}m" :
                    $"{ts.TotalSeconds:0}";
            }

            #endregion

            #region Public

            public void SetStatus(string statusId, int duration, bool pauseOffline, bool resuming = false)
            {
                if (!Data.Player.ContainsKey(UserId))
                {
                    Data.Player[UserId] = new Dictionary<string, PlayerStatusInfo>();
                }
                var data = Data.Player[UserId].GetValueOrDefault(statusId);
                if (data == null)
                {
                    Debug($"Set status new PauseOffline={pauseOffline}");
                    Data.Player[UserId][statusId] = new PlayerStatusInfo() { Duration = duration, EndTime = pauseOffline ? null : (DateTime?)DateTime.Now.AddSeconds(duration) };
                    Debug($"End time is {Data.Player[UserId][statusId].EndTime?.ToShortTimeString()}");
                    Interface.CallHook("OnStatusSet", UserId, statusId, duration);
                    rowsNeedUpdate = true;
                    if (!IsInvoking(nameof(RepeatUpdateUI))) { StartWorking(); }
                }
                else if (data != null && !data.IsPastEndTime)
                {
                    Debug($"Set status existing end time is {data.EndTime?.ToShortTimeString()}");
                    if (resuming)
                    {
                        data.Duration = data.DurationUntilEndTime;
                    }
                    else
                    {
                        data.Duration = duration;
                        data.EndTime = pauseOffline ? null : (DateTime?)DateTime.Now.AddSeconds(duration);
                    }
                    Interface.CallHook("OnStatusSet", basePlayer, statusId, duration);
                    rowsNeedUpdate = true;
                    if (!IsInvoking(nameof(RepeatUpdateUI))) { StartWorking(); }
                }
                else if (data != null && data.IsPastEndTime)
                {
                    Debug($"Cancelling {statusId} past end time");
                    RemoveStatus(statusId);
                }
                Debug($"Player {basePlayer.displayName} has {(Data.Player.ContainsKey(basePlayer.UserIDString) ? Data.Player[basePlayer.UserIDString].Count : 0)} statuses");
            }

            public void RemoveStatus(string statusId)
            {
                Debug($"Remove status invoked");
                if (Data?.Player.ContainsKey(UserId) ?? false)
                {
                    Debug($"Removing {statusId}");
                    var status = Data.Player[UserId].GetValueOrDefault(statusId); if (status == null) { return; }
                    Data.Player[UserId].Remove(statusId);
                    CuiHelper.DestroyUi(basePlayer, string.Format(UI_Status_ID, statusId));
                    rowsNeedUpdate = true;
                    Interface.CallHook("OnStatusEnd", UserId, statusId, status.Duration);
                    if (Data.Player.GetValueOrDefault(UserId)?.Count <= 0)
                    {
                        Debug($"All statuses removed");
                        Data.Player.Remove(UserId);
                        StopWorking();
                    }
                }
            }

            //public void SetStatusColor(string statusId, string color = null)
            //{
            //    if (Data.Statuses.ContainsKey(statusId) && Data.Player.ContainsKey(UserId) && Data.Player[UserId].ContainsKey(statusId))
            //    {
            //        var needsUpdate = false;
            //        var data = Data.Player[UserId][statusId];
            //        var status = Data.Statuses[statusId];
            //        if (color == RESET_KEYWORD) { color = status.Color; }
            //        if (color != null && data.Color != color)
            //        {
            //            needsUpdate = true;
            //            data.Color = color;
            //            Interface.CallHook("OnStatusUpdate", UserId, statusId, "color", color);
            //        }
            //        if (needsUpdate)
            //        {
            //            // Update
            //            if (Data.PlayersHiding.Contains(basePlayer.UserIDString)) { return; }
            //            rowsNeedUpdate = true;
            //        }
            //    }
            //}

            //public void SetStatusTitle(string statusId, string title = null, string color = null)
            //{
            //    if (Data.Statuses.ContainsKey(statusId) && Data.Player.ContainsKey(UserId) && Data.Player[UserId].ContainsKey(statusId))
            //    {
            //        var needsUpdate = false;
            //        var data = Data.Player[UserId][statusId];
            //        var status = Data.Statuses[statusId];
            //        var updateTitle = data.Title ?? status.Title;
            //        var updateTitleColor = data.TitleColor ?? status.TitleColor;
            //        if (title == RESET_KEYWORD) { title = status.Title; }
            //        if (color == RESET_KEYWORD) { color = status.TitleColor; }
            //        if (title != null && data.Title != title)
            //        {
            //            needsUpdate = true;
            //            data.Title = title;
            //            updateTitle = title;
            //            Interface.CallHook("OnStatusUpdate", UserId, statusId, "title", title);
            //        }
            //        if (color != null && data.TitleColor != color)
            //        {
            //            needsUpdate = true;
            //            data.TitleColor = color;
            //            updateTitleColor = color;
            //            Interface.CallHook("OnStatusUpdate", UserId, statusId, "titleColor", color);
            //        }
            //        if (needsUpdate)
            //        {
            //            // Update
            //            if (Data.PlayersHiding.Contains(basePlayer.UserIDString)) { return; }
            //            var titleLocalized = string.IsNullOrWhiteSpace(updateTitle) ? updateTitle : PLUGIN.Lang(status.Plugin, updateTitle, UserId);
            //            CuiHelper.DestroyUi(basePlayer, string.Format(UI_Title_ID, statusId));
            //            CuiHelper.AddUi(basePlayer, UI_StatusTitle(Data.Statuses[statusId], titleLocalized, updateTitleColor));
            //        }
            //    }
            //}

            //public void SetStatusText(string statusId, string text = null, string color = null)
            //{
            //    if (Data.Statuses.ContainsKey(statusId) && Data.Player.ContainsKey(UserId) && Data.Player[UserId].ContainsKey(statusId))
            //    {
            //        var needsUpdate = false;
            //        var ignoreText = text == null;
            //        var data = Data.Player[UserId][statusId];
            //        var status = Data.Statuses[statusId];
            //        var updateText = data.Text ?? status.Text;
            //        var updateTextColor = data.TextColor ?? status.TextColor;
            //        if (text == RESET_KEYWORD) { text = status.Text; }
            //        if (color == RESET_KEYWORD) { color = status.TextColor; }
            //        if (!ignoreText && data.Text != text)
            //        {
            //            needsUpdate = true;
            //            data.Text = text;
            //            updateText = text;
            //            Interface.CallHook("OnStatusUpdate", UserId, statusId, "text", text);
            //        }
            //        if (color != null && data.TextColor != color)
            //        {
            //            needsUpdate = true;
            //            data.TextColor = color;
            //            updateTextColor = color;
            //            Interface.CallHook("OnStatusUpdate", UserId, statusId, "textColor", color);
            //        }
            //        if (needsUpdate)
            //        {
            //            // Update
            //            if (Data.PlayersHiding.Contains(basePlayer.UserIDString)) { return; }
            //            if (updateText == null && data.Duration != int.MaxValue)
            //            {
            //                updateText = FormatDuration(data.Duration);
            //            }
            //            else
            //            {
            //                updateText = string.IsNullOrWhiteSpace(updateText) ? updateText : PLUGIN.Lang(status.Plugin, updateText, UserId);
            //            }
            //            CuiHelper.DestroyUi(basePlayer, string.Format(UI_Text_ID, statusId));
            //            CuiHelper.AddUi(basePlayer, UI_StatusText(Data.Statuses[statusId], updateText, updateTextColor));
            //        }
            //    }
            //}

            //public void SetStatusIcon(string statusId, string imageLibraryNameOrAssetPath = null, string color = null)
            //{
            //    if (Data.Statuses.ContainsKey(statusId) && Data.Player.ContainsKey(UserId) && Data.Player[UserId].ContainsKey(statusId))
            //    {
            //        var needsUpdate = false;
            //        var data = Data.Player[UserId][statusId];
            //        var status = Data.Statuses[statusId];
            //        var updateIcon = data.ImagePathOrUrl ?? status.ImageLibraryNameOrAssetPath;
            //        var updateIconColor = data.IconColor ?? status.IconColor;
            //        if (imageLibraryNameOrAssetPath == RESET_KEYWORD) { imageLibraryNameOrAssetPath = status.ImageLibraryNameOrAssetPath; }
            //        if (color == RESET_KEYWORD) { color = status.IconColor; }
            //        if (imageLibraryNameOrAssetPath != null && data.ImagePathOrUrl != imageLibraryNameOrAssetPath)
            //        {
            //            needsUpdate = true;
            //            data.ImagePathOrUrl = imageLibraryNameOrAssetPath;
            //            updateIcon = imageLibraryNameOrAssetPath;
            //            Interface.CallHook("OnStatusUpdate", UserId, statusId, "icon", imageLibraryNameOrAssetPath);
            //        }
            //        if (color != null && data.IconColor != color)
            //        {
            //            needsUpdate = true;
            //            data.IconColor = color;
            //            updateIconColor = color;
            //            Interface.CallHook("OnStatusUpdate", UserId, statusId, "iconColor", color);
            //        }
            //        if (needsUpdate)
            //        {
            //            // Update
            //            if (Data.PlayersHiding.Contains(basePlayer.UserIDString)) { return; }
            //            CuiHelper.DestroyUi(basePlayer, string.Format(UI_Icon_ID, statusId));
            //            var imageData = data.ImagePathOrUrl != null ? data.ImageData : status.ImageData;
            //            if (!string.IsNullOrWhiteSpace(updateIcon) && !string.IsNullOrWhiteSpace(imageData))
            //            {
            //                CuiHelper.AddUi(basePlayer, UI_StatusIcon(Data.Statuses[statusId], data.ImagePathOrUrl ?? status.ImageLibraryNameOrAssetPath, imageData, updateIconColor));
            //            }
            //        }
            //    }
            //}

            //public void SetStatusProgress(string statusId, string progress = null, string progressColor = null)
            //{
            //    if (Data.Statuses.ContainsKey(statusId) && Data.Player.ContainsKey(UserId) && Data.Player[UserId].ContainsKey(statusId))
            //    {
            //        var needsUpdate = false;
            //        var data = Data.Player[UserId][statusId];
            //        var status = Data.Statuses[statusId];
            //        var updateProgress = data.Progress ?? status.Progress;
            //        var updateProgressColor = data.ProgressColor ?? status.ProgressColor;
            //        if (progress == RESET_KEYWORD) { progress = status.Progress; }
            //        if (progressColor == RESET_KEYWORD) { progressColor = status.ProgressColor; }
            //        if (progress != null && data.Progress != progress)
            //        {
            //            needsUpdate = true;
            //            data.Progress = progress;
            //            updateProgress = progress;
            //            Interface.CallHook("OnStatusUpdate", UserId, statusId, "progress", progress);
            //        }
            //        if (progressColor != null && data.ProgressColor != progressColor)
            //        {
            //            needsUpdate = true;
            //            data.ProgressColor = progressColor;
            //            updateProgressColor = progressColor;
            //            Interface.CallHook("OnStatusUpdate", UserId, statusId, "progressColor", progressColor);
            //        }
            //        if (needsUpdate)
            //        {
            //            // Update
            //            if (Data.PlayersHiding.Contains(basePlayer.UserIDString)) { return; }
            //            //float progressValue;
            //            //var parsed = float.TryParse(progress, out progressValue);
            //            //CuiHelper.DestroyUi(basePlayer, string.Format(UI_Progress_ID, statusId));
            //            //if (parsed)
            //            //{
            //            //    CuiHelper.AddUi(basePlayer, UI_StatusProgress(Data.Statuses[statusId], progressValue, updateProgressColor));
            //            //}
            //            rowsNeedUpdate = true;
            //        }
            //    }
            //}

            public void SetProperties(string statusId, Dictionary<string, object> properties, bool draw)
            {
                if (Data.Statuses.ContainsKey(statusId) && Data.Player.ContainsKey(UserId) && Data.Player[UserId].ContainsKey(statusId))
                {
                    var data = Data.Player[UserId][statusId];
                    var status = Data.Statuses[statusId];
                    var updateStatus = false;
                    var updateTitle = false;
                    var updateText = false;
                    var updateIcon = false;
                    var updateProgress = false;
                    foreach(var kvp in properties.Keys)
                    {
                        switch(kvp)
                        {
                            case "color":
                                data.Color = properties.GetProperty<string>("color").CheckReset(status.Color);
                                updateStatus = true;
                                break;
                            case "title":
                                data.Title = properties.GetProperty<string>("title").CheckReset(status.Title);
                                updateTitle = true;
                                break;
                            case "titleColor":
                                data.TitleColor = properties.GetProperty<string>("titleColor").CheckReset(status.TitleColor);
                                updateTitle = true;
                                break;
                            case "text":
                                data.Text = properties.GetProperty<string>("text").CheckReset(status.Text);
                                updateText = true;
                                break;
                            case "textColor":
                                data.TextColor = properties.GetProperty<string>("textColor").CheckReset(status.TextColor);
                                updateText = true;
                                break;
                            case "icon":
                                data.ImagePathOrUrl = properties.GetProperty<string>("icon").CheckReset(status.ImageLibraryNameOrAssetPath);
                                updateIcon = true;
                                break;
                            case "iconColor":
                                data.IconColor = properties.GetProperty<string>("iconColor").CheckReset(status.IconColor);
                                updateIcon = true;
                                break;
                            case "progress":
                                try
                                {
                                    data.Progress = properties.GetProperty("progress", 0f).ToString();
                                } catch
                                {
                                    data.Progress = properties.GetProperty<string>("progress");
                                }
                                updateProgress = true;
                                break;
                            case "progressColor":
                                data.ProgressColor = properties.GetProperty<string>("progressColor");
                                updateProgress = true;
                                break;
                            default:
                                break;
                        }
                    }
                    if (!draw) { return; }
                    PLUGIN.drawing.Clear();
                    if (updateStatus)
                    {
                        var idx = -1;
                        for (int i = 0; i < ActiveStatusIds.Length; i++)
                        {
                            if (ActiveStatusIds[i] == statusId)
                            {
                                idx = i + ModifiersCount;
                                break;
                            }
                        }
                        PLUGIN.drawing.Add(PLUGIN.Element_Background(status, data, idx == -1 ? ModifiersCount : idx, idx != -1));
                    }
                    if (updateTitle)
                    {
                        PLUGIN.drawing.Add(PLUGIN.Element_Title(status, data, update: true));
                    }
                    if (updateText)
                    {
                        PLUGIN.drawing.Add(PLUGIN.Element_Text(status, data, update: true));
                    }
                    if (updateIcon)
                    {
                        PLUGIN.drawing.Add(PLUGIN.Element_Icon(status, data, update: true));
                    }
                    if (updateProgress)
                    {
                        PLUGIN.drawing.Add(PLUGIN.Element_Progress(status, data, update: true));
                    }
                    if (PLUGIN.drawing.Any())
                    {
                        PLUGIN.Draw(basePlayer, PLUGIN.drawing);
                    }
                }
            }
            #endregion
        }
    }
}

namespace Oxide.Plugins
{
    partial class SimpleStatus : CovalencePlugin
    {
        protected static readonly string UI_Base_ID = "ss";
        protected static readonly string UI_Status_ID = "ss.{0}";
        protected static readonly string UI_Content_ID = "ss.{0}.content";
        protected static readonly string UI_Title_ID = "ss.{0}.title";
        protected static readonly string UI_Icon_ID = "ss.{0}.icon";
        protected static readonly string UI_Text_ID = "ss.{0}.text";
        protected static readonly string UI_ProgressBG_ID = "ss.{0}.progressbg";
        protected static readonly string UI_Progress_ID = "ss.{0}.progress";
        public static readonly string RESET_KEYWORD = "$reset$";

        protected static class UI
        {
            public static int EntryH = 26;
            public static int EntryGap = 2;
            public static int Padding = 8;
            public static int ImageSize = 16;
            public static int ImageMargin = 5;
        }

        protected struct DrawElement
        {
            public string Id;
            public string Json;
            public bool Update;
        }

        private string cachedUiBase = null;
        protected string UI_Base()
        {
            if (cachedUiBase == null)
            {
                var container = new CuiElementContainer();
                var offX = -16;
                var offY = 100;
                var w = 192;
                var eh = 26;
                var eg = 2;
                var numEntries = 12;
                var h = (eh + eg) * numEntries - offY;
                container.Add(new CuiElement
                {
                    Name = UI_Base_ID,
                    Parent = "Under",
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "1 0",
                            AnchorMax = "1 0",
                            OffsetMin = $"{offX-w} {offY}",
                            OffsetMax = $"{offX} {offY+h}"
                        }
                    }
                });
                cachedUiBase = container.ToJson();
            }
            return cachedUiBase;
        }

        private readonly Dictionary<string, object> UI_Properties = new Dictionary<string, object>
        {
            ["top"] = null,
            ["bottom"] = null,
            ["backgroundColor"] = null,
            ["updateBackground"] = false,
            ["title"] = null,
            ["titleColor"] = null,
            ["updateTitle"] = false,
            ["text"] = null,
            ["textColor"] = null,
            ["updateText"] = false,
            ["progress"] = null,
            ["progressColor"] = null,
            ["updateProgress"] = false,
        };

        private List<string> elements = new List<string>();

        #region Background
        private string cachedElementBackground = null;
        protected DrawElement Element_Background(StatusInfo status, PlayerStatusInfo data, int index, bool update = false)
        {
            if (cachedElementBackground == null)
            {
                elements.Clear();
                elements.Add(Json(new CuiElement
                {
                    Parent = UI_Base_ID,
                    Name = "$id$",
                    Update = true,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "$color$",
                            Sprite = "assets/content/ui/ui.background.tile.psd",
                            Material = "assets/content/ui/namefontmaterial.mat"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 0",
                            OffsetMin = "0 $bottom$",
                            OffsetMax = "0 $top$"
                        }
                    }
                }));
                cachedElementBackground = string.Join(",", elements);
            }
            var uiStatusId = string.Format(UI_Status_ID, status.Id);
            var bottom = index * (UI.EntryGap + UI.EntryH);
            var top = bottom + UI.EntryH;
            return new DrawElement
            {
                Id = uiStatusId,
                Update = update,
                Json = cachedElementBackground
                .Replace("$bottom$", bottom.ToString())
                .Replace("$top$", top.ToString())
                .Replace("$color$", data.Color ?? status.Color)
                .Replace("$id$", uiStatusId)
                .Replace("\"update\":true", "\"update\":" + (update ? "true" : "false"))
            };
        }
        #endregion

        #region Progress
        private string cachedElementProgress = null;
        protected DrawElement Element_Progress(StatusInfo status, PlayerStatusInfo data, bool update = false)
        {
            if (cachedElementProgress == null)
            {
                elements.Clear();
                elements.Add(Json(new CuiElement
                {
                    Parent = "$parent$",
                    Name = "$id2$",
                    Update = true,
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = "24 3.5",
                            OffsetMax = "-4 -3.5"
                        }
                    }
                }));
                elements.Add(Json(new CuiElement
                {
                    Parent = "$id2$",
                    Name = "$id$",
                    Update = true,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "$progressColor$",
                            Sprite = "assets/content/ui/ui.background.tile.psd",
                            Material = "assets/content/ui/namefontmaterial.mat"
                        },
                        new CuiRectTransformComponent
                        {
                            OffsetMin = "0 0",
                            OffsetMax = "0 0",
                            AnchorMin = "0 0",
                            AnchorMax = "$progress$ 1"
                        }
                    }
                }));
                cachedElementProgress = string.Join(",", elements);
            }
            var parent = string.Format(UI_Status_ID, status.Id);
            var id = string.Format(UI_Progress_ID, status.Id);
            var idbg = string.Format(UI_ProgressBG_ID, status.Id);
            var progress = data.Progress.IsBlank() ? status.Progress : data.Progress;
            return new DrawElement
            {
                Id = id,
                Update = update,
                Json = cachedElementProgress
                .Replace("$progress$", progress.IsBlank() ? "0" : progress)
                .Replace("$progressColor$", data.ProgressColor.IsBlank() ? status.ProgressColor : data.ProgressColor)
                .Replace("$parent$", parent)
                .Replace("$id2$", idbg)
                .Replace("$id$", id)
                .Replace("\"update\":true", "\"update\":" + (update ? "true" : "false"))
            };
        }
        #endregion

        #region Title
        private string cachedElementTitle = null;
        protected DrawElement Element_Title(StatusInfo status, PlayerStatusInfo data, bool update = false)
        {
            if (cachedElementTitle == null)
            {
                elements.Clear();
                elements.Add(Json(new CuiElement
                {
                    Parent = "$parent$",
                    Name = "$id$",
                    Update = true,
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 1",
                            OffsetMin = "$oxmin$ 0",
                            OffsetMax = $"{192-24} 0"
                        },
                        new CuiTextComponent
                        {
                            Color = "$titleColor$",
                            Text = "$title$",
                            FontSize = 12,
                            Align = TextAnchor.MiddleLeft
                        }
                    }
                }));
                cachedElementTitle = string.Join(",", elements);
            }
            var parent = string.Format(UI_Progress_ID, status.Id);
            var id = string.Format(UI_Title_ID, status.Id);
            var hasProgress = (!status.Progress.IsBlank() || !data.Progress.IsBlank());
            var oxmin = hasProgress ? 7.5f : 0f;
            var titleSize = hasProgress ? 14 : 12;
            return new DrawElement
            {
                Id = id,
                Update = update,
                Json = cachedElementTitle
                .Replace("$oxmin$", oxmin.ToString())
                .Replace("$title$", data.Title.IsEmpty() ? Lang(status.Plugin, status.Title, data.UserId) : Lang(status.Plugin, data.Title, data.UserId))
                .Replace("$titleColor$", data.TitleColor.IsEmpty() ? status.TitleColor : data.TitleColor)
                .Replace("$parent$", parent)
                .Replace("$id$", id)
                .Replace("\"fontSize\":12", "\"fontSize\":" + (titleSize))
                .Replace("\"update\":true", "\"update\":" + (update ? "true" : "false"))
            };
        }
        #endregion

        #region Text
        private string cachedElementText = null;
        protected DrawElement Element_Text(StatusInfo status, PlayerStatusInfo data, bool update = false)
        {
            if (cachedElementText == null)
            {
                elements.Clear();
                elements.Add(Json(new CuiElement
                {
                    Parent = "$parent$",
                    Name = "$id$",
                    Update = true,
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 1",
                            OffsetMin = $"{0} 0",
                            OffsetMax = $"{192-24-12} 0"
                        },
                        new CuiTextComponent
                        {
                            Color = "$textColor$",
                            Text = "$text$",
                            FontSize = 12,
                            Align = TextAnchor.MiddleRight
                        }
                    }
                }));
                cachedElementText = string.Join(",", elements);
            }
            var parent = string.Format(UI_Progress_ID, status.Id);
            var id = string.Format(UI_Text_ID, status.Id);
            var text = (data.Text == null && status.Text == null && data.Duration != int.MaxValue) ? data.Duration.FormatDuration() :
                (data.Text.IsEmpty() ? Lang(status.Plugin, status.Text, data.UserId) : Lang(status.Plugin, data.Text, data.UserId));
            return new DrawElement
            {
                Id = id,
                Update = update,
                Json = cachedElementText
                .Replace("$text$", text)
                .Replace("$textColor$", data.TextColor.IsEmpty() ? status.TextColor : data.TextColor)
                .Replace("$parent$", parent)
                .Replace("$id$", id)
                .Replace("\"fontSize\":12", "\"fontSize\":" + 12)
                .Replace("\"update\":true", "\"update\":" + (update ? "true" : "false"))
            };
        }
        #endregion

        #region Icon
        private string cachedElementIcon = null;
        protected DrawElement Element_Icon(StatusInfo status, PlayerStatusInfo data, bool update = false)
        {
            if (cachedElementIcon == null)
            {
                elements.Clear();
                elements.Add(Json(new CuiElement
                {
                    Parent = "$parent$",
                    Name = "$id$",
                    Update = true,
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0.5",
                            AnchorMax = "0 0.5",
                            OffsetMin = $"{UI.Padding-4} {-UI.ImageSize/2}",
                            OffsetMax = $"{UI.Padding+UI.ImageSize-4} {UI.ImageSize/2}"
                        },
                        new CuiImageComponent
                        {
                            Color = "$iconColor$",
                            Sprite = "$imageData$"
                        }
                    }
                }));
                cachedElementIcon = string.Join(",", elements);
            }
            var parent = string.Format(UI_Status_ID, status.Id);
            var id = string.Format(UI_Icon_ID, status.Id);
            var imageUrlOrAssetPath = data.ImagePathOrUrl ?? status.ImageLibraryNameOrAssetPath;
            var isSprite = imageUrlOrAssetPath.IsAssetPath();
            var isItem = imageUrlOrAssetPath.IsItemId();
            var spriteOrPng = isSprite ? "\"sprite\":" : isItem ? "\"itemid\":" : "\"png\":";
            var imageType = imageUrlOrAssetPath.IsRawImage() ? "\"UnityEngine.UI.RawImage\"" : "\"UnityEngine.UI.Image\"";
            return new DrawElement
            {
                Id = id,
                Update = update,
                Json = cachedElementIcon
                .Replace("\"sprite\":", spriteOrPng)
                .Replace("\"png\":", spriteOrPng)
                .Replace("\"UnityEngine.UI.Image\"", imageType)
                .Replace("\"UnityEngine.UI.RawImage\"", imageType)
                .Replace("\"itemid\":", spriteOrPng)
                .Replace("$imageData$", GetImageData(imageUrlOrAssetPath))
                .Replace("$iconColor$", data.IconColor ?? status.IconColor)
                .Replace("$parent$", parent)
                .Replace("$id$", id)
                .Replace("\"fontSize\":12", "\"fontSize\":" + 12)
                .Replace("\"update\":true", "\"update\":" + (update ? "true" : "false"))
            };
        }
        #endregion

        private void Draw(BasePlayer basePlayer, IEnumerable<DrawElement> elements)
        {
            var json = "[" + string.Join(",", elements.Select(x => x.Json)) + "]";
            //Puts(json);
            CuiHelper.AddUi(basePlayer, json);
        }
    }
}
