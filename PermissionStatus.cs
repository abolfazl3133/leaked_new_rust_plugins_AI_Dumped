
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

#if CARBON
using Carbon.Core;
#endif

namespace Oxide.Plugins
{
    [Info("Permission Status", "0xF", "1.4.4")]
    public class PermissionStatus : RustPlugin
    {
        #region Plugin References
        [PluginReference] private Plugin ImageLibrary, SimpleStatus, TimedPermissions, IQPermissions;
        #endregion

        #region Permissions
        const string PERMISSION_HIDE = "permissionstatus.hide";
        #endregion

        #region Default Icon
        const string DefaultIconId = "TimedPermissionsInStatusbar_DefaultIcon";
        const string DefaultIconUrl = "https://i.imgur.com/nMeXKPp.png";
        #endregion

        #region Magic
        private abstract class StatusDependencies
        {
            public abstract bool Condition(BasePlayer player, string key, bool isGroup);
            public abstract string Value(BasePlayer player, string key, bool isGroup, string format = @"d\d\ hh\h\ mm\m");
        }

        private class __TimedPermissions : StatusDependencies
        {
            public __TimedPermissions(Plugin plugin)
            {
                References.GetReferences(plugin);
            }


            public static class References
            {
                public static Plugin Plugin { get; set; }
                public static Type TimedPermissionsType { get; set; }
                public static Type[] NestedTypes_TimedPermissions { get; set; }
                public static Type PlayerInformationType { get; set; }
                public static MethodInfo PlayerInformation_Get { get; set; }
                public static PropertyInfo PlayerInformation_Groups { get; set; }
                public static PropertyInfo PlayerInformation_Permissions { get; set; }
                public static Type ExpiringAccessValueType { get; set; }
                public static PropertyInfo ExpiringAccessValue_Value { get; set; }
                public static PropertyInfo ExpiringAccessValue_ExpireDate { get; set; }
                public static PropertyInfo ReadOnlyCollection_Count { get; set; }
                public static MethodInfo ElementAt { get; set; }
                public static bool GetReferences(Plugin plugin)
                {
                    Plugin = plugin;
                    if (Plugin == null)
                        return false;
                    TimedPermissionsType = Plugin?.GetType();
                    NestedTypes_TimedPermissions = TimedPermissionsType?.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Instance);
                    PlayerInformationType = NestedTypes_TimedPermissions?.First(t => t.Name == "PlayerInformation");
                    PlayerInformation_Get = PlayerInformationType?.GetMethod("Get");
                    PlayerInformation_Groups = PlayerInformationType?.GetProperty("Groups");
                    PlayerInformation_Permissions = PlayerInformationType?.GetProperty("Permissions");
                    ExpiringAccessValueType = NestedTypes_TimedPermissions?.First(t => t.Name == "ExpiringAccessValue");
                    ExpiringAccessValue_Value = ExpiringAccessValueType?.GetProperty("Value");
                    ExpiringAccessValue_ExpireDate = ExpiringAccessValueType?.GetProperty("ExpireDate");
                    ReadOnlyCollection_Count = typeof(System.Collections.ObjectModel.ReadOnlyCollection<>)
                                  .MakeGenericType(ExpiringAccessValueType)
                                  .GetProperty("Count");
                    ElementAt = typeof(Enumerable).GetMethod("ElementAt").MakeGenericMethod(ExpiringAccessValueType);
                    return true;
                }
            }

            public override bool Condition(BasePlayer player, string key, bool isGroup)
            {
                var pinfo = References.PlayerInformation_Get.Invoke(null, new object[] { player.UserIDString });
                if (pinfo == null) return false;
                var collection = (isGroup ? References.PlayerInformation_Groups.GetValue(pinfo) : References.PlayerInformation_Permissions.GetValue(pinfo));
                if (collection == null) return false;
                int countGroups = (int)References.ReadOnlyCollection_Count.GetValue(collection);
                object ExpiringAccessValue = null;
                for (int i = 0; i < countGroups; i++)
                {
                    var eav = References.ElementAt.Invoke(null, new object[] { collection, i });
                    var namePerm = (string)References.ExpiringAccessValue_Value.GetValue(eav);
                    if (namePerm.Equals(key))
                        ExpiringAccessValue = eav;
                }
                return ExpiringAccessValue != null;
            }
            public override string Value(BasePlayer player, string key, bool isGroup, string format = @"d\d\ hh\h\ mm\m")
            {
                var pinfo = __TimedPermissions.References.PlayerInformation_Get.Invoke(null, new object[] { player.UserIDString });
                if (pinfo == null) return string.Empty;
                var collection = (isGroup ? __TimedPermissions.References.PlayerInformation_Groups.GetValue(pinfo) : __TimedPermissions.References.PlayerInformation_Permissions.GetValue(pinfo));
                if (collection == null) return string.Empty;
                int countGroups = (int)__TimedPermissions.References.ReadOnlyCollection_Count.GetValue(collection);
                object ExpiringAccessValue = null;
                for (int i = 0; i < countGroups; i++)
                {
                    var eav = __TimedPermissions.References.ElementAt.Invoke(null, new object[] { collection, i });
                    var namePerm = (string)__TimedPermissions.References.ExpiringAccessValue_Value.GetValue(eav);
                    if (namePerm.Equals(key))
                        ExpiringAccessValue = eav;
                }
                if (ExpiringAccessValue == null) return string.Empty;
                var ExpireDate = (DateTime)__TimedPermissions.References.ExpiringAccessValue_ExpireDate.GetValue(ExpiringAccessValue);
                return (ExpireDate - DateTime.UtcNow).ToString(format);
            }
        }
        private class __IQPermissions : StatusDependencies
        {
            private Plugin Plugin { get; set; }

            public __IQPermissions(Plugin plugin)
            {
                Plugin = plugin;
                References.GetReferences(plugin);
            }

            public static class References
            {
                public static Plugin Plugin { get; set; }
                public static Type IQPermissionsType { get; set; }
                public static MethodInfo GetGroups { get; set; }
                public static MethodInfo GetPermissions { get; set; }

                public static bool GetReferences(Plugin plugin)
                {
                    Plugin = plugin;
                    if (Plugin == null)
                        return false;
                    IQPermissionsType = Plugin?.GetType();
                    GetGroups = IQPermissionsType.GetMethod("GetGroups", BindingFlags.Public | BindingFlags.Instance);
                    GetPermissions = IQPermissionsType.GetMethod("GetPermissions", BindingFlags.Public | BindingFlags.Instance);
                    return true;
                }
            }

            public override bool Condition(BasePlayer player, string key, bool isGroup)
            {
                Dictionary<String, DateTime> dictionary = (Dictionary<String, DateTime>)(isGroup ? References.GetGroups : References.GetPermissions).Invoke(Plugin, new object[] { player.userID });
                return dictionary.ContainsKey(key);
            }
            public override string Value(BasePlayer player, string key, bool isGroup, string format = @"d\d\ hh\h\ mm\m")
            {
                Dictionary<String, DateTime> dictionary = (Dictionary<String, DateTime>)(isGroup ? References.GetGroups : References.GetPermissions).Invoke(Plugin, new object[] { player.userID }); ;
                DateTime dateTime;
                if (!dictionary.TryGetValue(key, out dateTime)) return string.Empty;
                return (DateTime.Now - dateTime).ToString(format);
            }
        }

        #endregion

        #region Fields
        static StatusDependencies Dependencies;
        #endregion

        #region Methods
        private bool BasicConditionForPlayer(BasePlayer player, Status status)
        {
            if (permission.UserHasPermission(player.UserIDString, PERMISSION_HIDE))
                return false;

            if (status.StatusSettings.InCupboardArea && !player.IsBuildingAuthed())
                return false;

            return true;
        }

        void SetStatus(ulong userId, string statusId, int duration = int.MaxValue)
        {
            SimpleStatus.Call("SetStatus", userId, statusId, duration);
        }

        void SetTitle(ulong userId, string statusId, string title = null)
        {
            SimpleStatus.Call("SetStatusTitle", userId, statusId, title);
        }

        void SetText(ulong userId, string statusId, string text = null)
        {
            SimpleStatus.Call("SetStatusText", userId, statusId, text);
        }

        bool IsStatusDisplayed(ulong userId, string statusId)
        {
            return (SimpleStatus.Call<int>("GetDuration", userId, statusId) == 0 ? false : true);
        }

        string GetStatusText(BasePlayer player, Status status)
        {
            if (status.StatusSettings.SelectedMode == Status.Settings.Mode.Static)
                return GetMessage($"{status.Key}:StaticText", player.UserIDString);

            bool hasTimedPerm = Dependencies.Condition(player, status.Key, status.StatusSettings.IsGroup);
            bool hasUsualPerm = status.StatusSettings.IsGroup ? permission.UserHasGroup(player.UserIDString, status.Key) : permission.UserHasPermission(player.UserIDString, status.Key);

            if (!hasTimedPerm && !hasUsualPerm)
                return string.Empty;

            return (hasUsualPerm && !hasTimedPerm ? GetMessage(LangKeys.Unlimited, player.UserIDString) : Dependencies.Value(player, status.Key, status.StatusSettings.IsGroup, status.StatusSettings.TimeFormat));
        }

        bool IsComplyingConditions(BasePlayer player, Status status)
        {
            bool result = false;
            var basic = BasicConditionForPlayer(player, status);
            if (basic == false)
                return result;

            bool hasTimedPerm = Dependencies.Condition(player, status.Key, status.StatusSettings.IsGroup);
            bool hasUsualPerm = status.StatusSettings.IsGroup ? permission.UserHasGroup(player.UserIDString, status.Key) : permission.UserHasPermission(player.UserIDString, status.Key);

            switch (status.StatusSettings.SelectedMode)
            {
                case Status.Settings.Mode.Time:
                    result = hasTimedPerm || hasUsualPerm;
                    break;
                case Status.Settings.Mode.Static:
                    result = hasUsualPerm;
                    break;
            }
            return basic && result;
        }

        void UpdateStatus(Status status, BasePlayer player)
        {
            bool IsDisplayed = IsStatusDisplayed(player.userID, status.Key);
            int duration = IsComplyingConditions(player, status) ? int.MaxValue : 0;
            string statusText = null;
            if (duration > 0)
            {
                statusText = GetStatusText(player, status);
                if (string.IsNullOrEmpty(statusText) || (status.StatusSettings.HideUnlimited && statusText == GetMessage(LangKeys.Unlimited, player.UserIDString)))
                    duration = 0;
            }
            if (IsDisplayed != duration > 0)
                SetStatus(player.userID, status.Key, duration);
            if (duration > 0)
            {
                SetTitle(player.userID, status.Key, GetMessage(status.Key, player.UserIDString));
                SetText(player.userID, status.Key, statusText);
            }
        }

        void FullUpdate(BasePlayer player)
        {
            foreach (var status in Statuses)
                UpdateStatus(status, player);
        }

        void PeriodicUpdateStatus(Status status)
        {
            foreach (var player in BasePlayer.activePlayerList)
                UpdateStatus(status, player);
        }

        void FindAndUpdatePlayer(string userID)
        {
            BasePlayer player = BasePlayer.Find(userID);
            if (player == null) return;
            FullUpdate(player);
        }

        public void CheckBuildings()
        {
            foreach (Status status in Statuses)
            {
                if (!status.StatusSettings.InCupboardArea)
                    continue;

                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    if (IsStatusDisplayed(player.userID, status.Key) != player.IsBuildingAuthed())
                        UpdateStatus(status, player);
                }
            }
        }

        private void SendMessage(BasePlayer player, string format, params object[] args) => SendReply(player, $"<color=#6fcbd9>[{this.Title}]</color>\n> " + format, args);

        #endregion

        #region Commands
        [ChatCommand("ps.toggle")]
        private void pstoggle(BasePlayer player)
        {
            UserData userData = permission.GetUserData(player.UserIDString);
            if (permission.GroupsHavePermission(userData.Groups, PERMISSION_HIDE))
            {
                player.ChatMessage("The permission statuses have been hidden by the group, ignore.");
                return;
            }

            if (userData.Perms.Contains(PERMISSION_HIDE, StringComparer.OrdinalIgnoreCase))
            {
                userData.Perms.Remove(PERMISSION_HIDE);
                FullUpdate(player);
                SendMessage(player, GetMessage(LangKeys.ShowMessage, player.UserIDString));
            }
            else
            {
                userData.Perms.Add(PERMISSION_HIDE);
                FullUpdate(player);
                SendMessage(player, GetMessage(LangKeys.HideMessage, player.UserIDString));
            }
        }
        #endregion

        #region Hooks

        void Init()
        {
            permission.RegisterPermission(PERMISSION_HIDE, this);
        }

        void OnServerInitialized()
        {
            if (ImageLibrary == null || SimpleStatus == null)
            {
                PrintError("\nYou do not have one or more required plugins installed!\n" +
                    "ImageLibrary: https://umod.org/plugins/image-library \n" +
                    "SimpleStatus: https://codefling.com/plugins/simple-status");
                NextTick(() => Interface.Oxide.UnloadPlugin(this.Name));
                return;
            }

            LoadConfig();
            SaveConfig();

#if CARBON
            bool timedpermLoaded = Carbon.Community.Runtime.Plugins.Plugins.Exists(pl => pl.Name == nameof(TimedPermissions)); ;
            bool iqpermLoaded = Carbon.Community.Runtime.Plugins.Plugins.Exists(pl => pl.Name == nameof(IQPermissions));
#else
            bool timedpermLoaded = CSharpPluginLoader.Instance.LoadedPlugins.ContainsKey(nameof(TimedPermissions));
            bool iqpermLoaded = CSharpPluginLoader.Instance.LoadedPlugins.ContainsKey(nameof(IQPermissions));
#endif

            if (!timedpermLoaded && !iqpermLoaded)
            {
                PrintError("\nAt least one of the plugins for temporary permissions is needed!\n" +
                    "Timed Permissions: https://umod.org/plugins/timed-permissions \n" +
                    "IQPermissions");
                NextTick(() => Interface.Oxide.UnloadPlugin(this.Name));
                return;
            }
            else if (timedpermLoaded && iqpermLoaded)
            {
                PrintError("Leave only one plugin for temporary permissions!");
                return;
            }
            if (timedpermLoaded)
                Dependencies = new __TimedPermissions(TimedPermissions);
            else
                Dependencies = new __IQPermissions(IQPermissions);

            if (TimedPermissions != null)
            {
                if (!__TimedPermissions.References.GetReferences(TimedPermissions))
                {
                    PrintError("References.GetReferences call failed!");
                    return;
                }
            }

            Dictionary<string, string> iconsToLoad = new Dictionary<string, string>()
            {
                { DefaultIconUrl, DefaultIconUrl }
            };

            foreach (var status in Statuses)
            {
                var key = status.Key;
                var settings = status.StatusSettings;

                if (settings.IconUrl != "default" && !iconsToLoad.ContainsKey(settings.IconUrl))
                    iconsToLoad.Add(settings.IconUrl, settings.IconUrl);

                SimpleStatus.Call("CreateStatus", this, key, settings.BackgroundColor, key, settings.TitleColor, string.Empty, settings.TextColor, settings.IconUrl == "default" ? DefaultIconId : settings.IconUrl, settings.IconColor);
            }


            ImageLibrary.Call("ImportImageList", new object[] { this.Name, iconsToLoad, 0UL, false, new Action(OnImageBatchCompleted) });
        }



        void OnImageBatchCompleted()
        {
            foreach (var player in BasePlayer.activePlayerList)
                FullUpdate(player);

            foreach (Status status in Statuses)
                if (status.StatusSettings.SelectedMode == Status.Settings.Mode.Time)
                    timer.Every(status.StatusSettings.UpdateInterval, () => PeriodicUpdateStatus(status));

            timer.Every(3f, CheckBuildings);
        }


        void OnPlayerConnected(BasePlayer player) => FullUpdate(player);
        void OnUserPermissionGranted(string id, string permName) => FindAndUpdatePlayer(id);
        void OnUserPermissionRevoked(string id, string permName) => FindAndUpdatePlayer(id);
        void OnUserGroupAdded(string id, string groupName) => FindAndUpdatePlayer(id);
        void OnUserGroupRemoved(string id, string groupName) => FindAndUpdatePlayer(id);
        #endregion

        #region Config
        public class Status
        {
            public class Settings
            {
                public enum Mode
                {
                    Time,
                    Static
                }

                [JsonProperty(PropertyName = "true - Group | false - Permission")]
                public bool IsGroup { get; set; } = true;
                [JsonProperty(PropertyName = "Display Mode (0 - Time, 1 - Static)")]
                public Mode SelectedMode { get; set; } = 0;
                [JsonProperty(PropertyName = "[Time display mode] Update Interval (in seconds)")]
                public float UpdateInterval { get; set; } = 60;
                [JsonProperty(PropertyName = "Only show in authorized cupboard area")]
                public bool InCupboardArea { get; set; } = false;
                [JsonProperty(PropertyName = "Hide if unlimited")]
                public bool HideUnlimited { get; set; } = false;
                [JsonProperty(PropertyName = "Time Format")]
                public string TimeFormat { get; set; } = @"d\d\ hh\h\ mm\m";
                [JsonProperty(PropertyName = "Icon Url")]
                public string IconUrl { get; set; } = "default";
                [JsonProperty(PropertyName = "Icon Color")]
                public string IconColor { get; set; } = "0.22 0.63 0.90 0.9";
                [JsonProperty(PropertyName = "Background Color")]
                public string BackgroundColor { get; set; } = "0.16 0.44 0.63 0.85";
                [JsonProperty(PropertyName = "Title Color")]
                public string TitleColor { get; set; } = "1 1 1 1";
                [JsonProperty(PropertyName = "Text Color")]
                public string TextColor { get; set; } = "1 1 1 1";
            }
            public string Key { get; set; }
            public Settings StatusSettings { get; set; }
        }


        private static Status[] Statuses { get; set; }

        private Dictionary<string, Status.Settings> GetDefaultConfig()
        {
            return new Dictionary<string, Status.Settings>()
            {
                ["ExampleGroupOrPerm"] = new Status.Settings(),
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                var config = Config.ReadObject<Dictionary<string, Status.Settings>>();
                if (config == null)
                    LoadDefaultConfig();
                else
                    Statuses = config.Select(pair => new Status { Key = pair.Key, StatusSettings = pair.Value }).ToArray();

            }
            catch (Exception ex)
            {
                if (ex is JsonSerializationException || ex is NullReferenceException || ex is JsonReaderException)
                {
                    Puts($"Exception: {ex}");
                    LoadDefaultConfig();
                    return;
                }
                throw;
            }
        }
        protected override void LoadDefaultConfig()
        {
            Puts("Configuration file missing or corrupt, creating default config file.");
            Statuses = GetDefaultConfig().Select(pair => new Status { Key = pair.Key, StatusSettings = pair.Value }).ToArray();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(Statuses.ToDictionary(x => x.Key, x => x.StatusSettings));
        }
        #endregion

        #region Lang

        public class LangKeys
        {
            public const string HideMessage = nameof(HideMessage);
            public const string ShowMessage = nameof(ShowMessage);
            public const string Unlimited = nameof(Unlimited);
        }

        protected override void LoadDefaultMessages()
        {
            var basic = new Dictionary<string, string>()
            {
                [LangKeys.HideMessage] = "You have hidden the statuses of timed permissions.",
                [LangKeys.ShowMessage] = "You have enabled the display of timed permissions statuses.",
                [LangKeys.Unlimited] = "Unlimited",
            };
            var keys = Statuses.ToDictionary(x => x.Key, x => x.Key);
            var texts = Statuses.Where(status => status.StatusSettings.SelectedMode == Status.Settings.Mode.Static).Select(p => p.Key).ToDictionary(x => $"{x}:StaticText", x => "");
            lang.RegisterMessages(basic.Concat(keys).Concat(texts).ToDictionary(x => x.Key, x => x.Value), this, "en");
        }

        private string GetMessage(string langKey, string userID) => lang.GetMessage(langKey, this, userID);
        private string GetMessage(string langKey, string userID, params object[] args) => (args.Length == 0) ? GetMessage(langKey, userID) : string.Format(GetMessage(langKey, userID), args);
        #endregion Lang
    }
}