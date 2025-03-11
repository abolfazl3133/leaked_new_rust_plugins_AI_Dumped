
using static Oxide.Plugins.InjuriesAndDiseases;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Core;
using Newtonsoft.Json.Converters;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Plugins.InjuriesAndDiseasesExtensionMethods;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("InjuriesAndDiseases", "https://discord.gg/TrJ7jnS233", "1.3.1")]
    [Description("Players can sustain injuries and become infected with diseases.")]
    public partial class InjuriesAndDiseases : CovalencePlugin
    {
        // Changelog
        /*
            ### 1.3.0
            - Dropped support for CustomStatusFramework in favor of SimpleStatus
            - Large parts of the code of been rewritten to optimize performance
            - Rabies, Z13Virus, and Tapeworm now by default have 'unlimited' duration, this can be changed in the config
            - Statuses should now correctly be removed upon death, even when the player is disconnected
            - Fixed many bugs and inconsistencies with statuses
            - Several API methods now use player user ids instead of base player
            - Added config option 'Infliction Damage Action' this can specify what kind of attack needs to be made to be inflicted. This value can be any, melee, or ranged. Z13Virus and Rabies will by default be set to melee.
            - By default Z13Virus can now be inflicted from melee attacks of scientistnpc_heavy entities. This is the entity type that the plugin Zombie Horde uses, so these zombies should infect you now. The heavy scientists from Oil Rig won't because they use guns and not melee.
            - Images can now be asset paths in addition to image libary pngs. Asset paths must start with "assets/", by default the undiagnosed image will use an asset path.
            - Removed Damage Overlay from config - this is being replaced with a hardcoded sprite
            - Moved image hosting from Imgur to ibb.co, hopefully this will not throttle downloads and it won't be necessary for people to host the images themselves. Only time will tell though
            ### 1.3.1
            - Fixed duplicate messages appearing when being inflicting more than once
            - Update Z13Virus config to by default have the "zombie" value. This value can be used to denote entities that are spawned by the ZombieHorde plugin.
            - Added option for support with the BotReSpawn plugin. Use the value "botrespawn" under Infliction Entities to have it so botrespawn npcs will inflict the condition.
            - Fixed issue where "ghost statuses" would sometimes be present on server restart
         */
        [PluginReference]
        private Plugin CustomStatusFramework;
        [PluginReference]
        private Plugin ImageLibrary;
        [PluginReference]
        private Plugin SimpleStatus;
        [PluginReference]
        private Plugin BotReSpawn;

        public static InjuriesAndDiseases PLUGIN;

        private bool PluginLoaded = true;

        private bool Debugging = false;

        private bool ShowConfigWarnings = true;

        private const string PermissionDoctor = "injuriesanddiseases.doctor";
        private const string PermissionAdmin = "injuriesanddiseases.admin";

        void OnServerInitialized()
        {
            PLUGIN = this;
            DependencyCheck();
            if (!PluginLoaded) { return; }
            LoadAll();
            LoadImages();
            Statuses.CreateStatuses();
            RemoveCustomConditions();
            PremadeStatusConditions.InitDefaultStatusConditions();
            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
            Debug("WARNING - Debugging is enabled", true);
        }

        void Unload()
        {
            SaveAll();
            foreach (var pair in Behaviours)
            {
                UnityEngine.Object.Destroy(pair.Value);
            }
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                OnPlayerDisconnected(basePlayer);
            }
            Data = null;
        }

        void SaveAll()
        {
            SaveDataFile("conditions", Data);
        }

        void LoadAll()
        {
            Data = LoadDataFile<SaveData>("conditions") ?? new SaveData();
        }

        private void LoadImages()
        {
            if (ImageLibrary != null && ImageLibrary.IsLoaded)
            {
                AddImages(new Dictionary<string, string>
                {
                    ["undiagnosed"] = config.Images.Undiagnosed,
                    ["doctor"] = config.Images.Doctor
                });
                AddImages(PremadeStatusConditions.ALL.ToDictionary(x => x.Icon, x => x.Config.Icon));
            }
        }

        private void DependencyCheck()
        {
            var required = "The required dependency {0} is not installed, {1} will not work properly without it.";
            timer.In(1f, () => // delay incase plugin has been reloaded
            {
                if (CustomStatusFramework?.IsLoaded ?? false)
                {
                    PrintError("Custom Status Framework is no longer supported with this plugin as of v1.3.0. Please install the new required dependency Simple Status instead.");
                    PluginLoaded = false;
                    return;
                }
                if (!SimpleStatus?.IsLoaded ?? true)
                {
                    PrintError(String.Format(required, "Simple Status", Name));
                    PluginLoaded = false;
                    return;
                }
                if (!ImageLibrary?.IsLoaded ?? true)
                {
                    PrintError(String.Format(required, "ImageLibrary", Name));
                    PluginLoaded = false;
                    return;
                }
            });
        }

        private void RemoveCustomConditions()
        {
            PremadeStatusConditions.ALL.RemoveAll(x => x.IsCustom);
            PremadeStatusConditions.UpdateAllById();
        }
    }
}

namespace Oxide.Plugins
{
    partial class InjuriesAndDiseases : CovalencePlugin
    {
        /*
         * Returns a list of all enabled conditions.
         */
        private List<string> GetConditions()
        {
            return PremadeStatusConditions.ALL.Where(x => x.IsEnabled).Select(x => x.Id).ToList();
        }

        /*
         * Returns a list of conditions a player is inflicted with.
         */
        private List<string> GetPlayerConditions(ulong userId)
        {
            return Helper.GetConditions(userId).Select(x => x.StatusConditionId).ToList();
        }

        /*
         * Returns true if the player has the specified condition.
         */
        private bool HasCondition(ulong userId, string conditionNameId)
        {
            return Helper.HasCondition(userId, conditionNameId);
        }

        /*
         * Inflicts the player with the specified condition.
         */
        private void SetCondition(ulong userId, string conditionNameId, bool revealed)
        {
            Helper.SetCondition(userId, conditionNameId, revealed);
        }

        /*
         * Removes the condition for the player.
         */
        private void RemoveCondition(ulong userId, string conditionNameId, bool cured)
        {
            Helper.RemoveCondition(userId, conditionNameId, cured);
        }

        /*
         * Removes all conditions for the player.
         */
        private void RemoveAllConditions(ulong userId, bool cured)
        {
            Helper.RemoveAllConditions(userId, cured);
        }

        /*
         * Reveals the condition to the player if it is not already revealed.
         */
        private void RevealCondition(ulong userId, string conditionNameId)
        {
            Helper.RevealCondition(userId, conditionNameId);
        }

        /*
         * Create a custom condition.
         */
        private void CreateCondition(Plugin plugin, string conditionNameId, string imageLibraryIconName, int minIntervalSeconds, int maxIntervalSeconds, int minDurationSeconds, int maxDurationSeconds, bool showDuration, bool showIndicator, Action<BasePlayer> beginEffect = null, Action<BasePlayer> intervalEffect = null)
        {
            PremadeStatusConditions.ALL.RemoveAll(x => x.Id == conditionNameId);
            PremadeStatusConditions.ALL.Add(new StatusCondition(conditionNameId, beginEffect, intervalEffect)
            {
                SourcePlugin = plugin,
                CustomIcon = imageLibraryIconName,
                MinIntervalSeconds = minIntervalSeconds,
                MaxIntervalSeconds = maxIntervalSeconds,
                MinDurationSeconds = minDurationSeconds,
                MaxDurationSeconds = maxDurationSeconds,
                IsCustom = true,
                Config = new StatusConditionConfig
                {
                    ShowDuration = showDuration,
                    ShowIndicator = showIndicator
                }
            });
            Puts($"Loaded custom status condition {conditionNameId} by {plugin.Author}");
            PremadeStatusConditions.UpdateAllById();
        }
    }
}

namespace Oxide.Plugins
{
    public partial class InjuriesAndDiseases
    {
        public static class RustColor
        {
            public static string Blue = "0.08627 0.25490 0.38431 1";
            public static string LightBlue = "0.25490 0.61176 0.86275 1";
            //public static string Red = "0.68627 0.21569 0.14118 1";
            public static string Red = "0.77255 0.23922 0.15686 1";
            public static string Maroon = "0.46667 0.22745 0.18431 1";
            public static string LightMaroon = "1.00000 0.32549 0.21961 1";
            public static string DarkMaroon = "0.47059 0.14902 0.00000 1";
            public static string LightRed = "0.91373 0.77647 0.75686 1";
            //public static string Green = "0.25490 0.30980 0.14510 1";
            public static string Green = "0.35490 0.40980 0.24510 1";
            public static string LightGreen = "0.76078 0.94510 0.41176 1";
            public static string Gray = "0.45490 0.43529 0.40784 1";
            public static string LightGray = "0.69804 0.66667 0.63529 1";
            public static string Orange = "1.00000 0.53333 0.18039 1";
            public static string LightOrange = "1.00000 0.82353 0.44706 1";
            public static string White = "0.87451 0.83529 0.80000 1";
            public static string LightWhite = "0.97647 0.97647 0.97647 1";
            public static string Lime = "0.64706 1.00000 0.00000 1";
            public static string LightLime = "0.69804 0.83137 0.46667 1";
            public static string DarkGray = "0.08627 0.08627 0.08627 1";
            public static string DarkBrown = "0.15686 0.15686 0.12549 1";
            public static string LightBrown = "0.54509 0.51372 0.4705 1";
            public static string Black = "0 0 0 1";
        }

        public static class StatusColor
        {
            public static string Background = RustColor.Red;
            public static string Text = RustColor.LightRed;
            public static string Icon = RustColor.DarkMaroon;
        }

        public static class DoctorColor
        {
            public static string Background = RustColor.Blue;
            public static string Text = RustColor.LightBlue;
            public static string Icon = RustColor.LightBlue;
        }
    }
}

namespace Oxide.Plugins
{
    partial class InjuriesAndDiseases : CovalencePlugin
    {
        void Usage(IPlayer player, string command, params string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null) { return; }
            var newArgs = new List<string>() { command, String.Join(" ", args) };
            Message(basePlayer, "usage", newArgs.ToArray());
        }

        [Command("diagnose"), Permission(PermissionDoctor)] // diagnose
        void CmdDiagnose(IPlayer player, string command, string[] args)
        {
            var doctor = player.Object as BasePlayer;
            if (doctor == null) { return; }
            var target = GetObjectRaycast<BasePlayer>(doctor, 4f);
            if (target == null)
            {
                Message(doctor, "look at player");
                return;
            }
            var psc = Helper.GetConditions(target.userID).FirstOrDefault(x => !x.Revealed);
            if (psc == null)
            {
                Message(doctor, "healthy diagnosis", target.displayName);
                return;
            }
            Helper.RevealCondition(target.userID, psc.StatusConditionId);
            Message(doctor, $"{psc.StatusConditionId} diagnosis", target.displayName);
        }

        [Command("inflict"), Permission(PermissionAdmin)] // inflict <player> <condition> <revealed?>
        void CmdAdminInflict(IPlayer player, string command, string[] args)
        {
            try
            {
                var playername = args[0];
                var condition = args[1].ToLower();
                var revealed = !config.RequireDiagnosis;
                if (args.Length > 2)
                {
                    revealed = bool.Parse(args[2]);
                }
                var userId = FindPlayer(playername).UserId();
                Helper.SetCondition(userId, condition.ToLower(), revealed);
                player.Reply(Lang("success", player.Id));
            }
            catch (Exception e)
            {
                Usage(player, command, "<player>", "<condition>", "<revealed?>");
            }
        }

        [Command("reveal"), Permission(PermissionAdmin)] // reveal <player> <condition>
        void CmdAdminReveal(IPlayer player, string command, string[] args)
        {
            try
            {
                var playername = args[0];
                var condition = args[1].ToLower();
                var userId = FindPlayer(playername).UserId();
                Helper.RevealCondition(userId, condition);
                player.Reply(Lang("success", player.Id));
            }
            catch (Exception e)
            {
                Usage(player, command, "<player>", "<condition>");
            }
        }

        [Command("cure"), Permission(PermissionAdmin)] // cure <player> <condition?>
        void CmdAdminCure(IPlayer player, string command, string[] args)
        {
            try
            {
                var playername = args[0];
                if (args.Length == 1)
                {
                    Helper.RemoveAllConditions(player.UserId(), true);
                }
                else
                {
                    var condition = args[1].ToLower();
                    Helper.RemoveCondition(player.UserId(), condition, true);
                }
                player.Reply(Lang("success", player.Id));
            }
            catch (Exception e)
            {
                Usage(player, command, "<player>", "<condition?>");
            }
        }

        [Command("conditions"), Permission(PermissionAdmin)] // conditions <player>
        void CmdAdminConditions(IPlayer player, string command, string[] args)
        {
            try
            {
                var userId = FindPlayer(args[0]).UserId();
                var pscs = Helper.GetConditions(userId).Select(x => $"{x.StatusConditionId}{(x.HasDuration ? $"({x.DurationTime})" : string.Empty)}").ToArray();
                player.Reply(Lang("success", player.Id));
                player.Reply(string.Join(",", (object[])pscs));
            }
            catch (Exception e)
            {
                Usage(player, command, "<player>");
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class InjuriesAndDiseases : CovalencePlugin
    {
        private Dictionary<ulong, ConditionBehaviour> Behaviours = new Dictionary<ulong, ConditionBehaviour>();

        public class ConditionBehaviour : MonoBehaviour
        {
            #region Private
            private BasePlayer basePlayer;
            public ulong UserId => basePlayer.userID;

            private void Awake()
            {
                basePlayer = GetComponent<BasePlayer>();
            }
            private void OnDestroy()
            {
                // Any cleanup
            }

            private void StartWorking()
            {
                InvokeRepeating(nameof(ConditionTick), 1f, 1f);
            }

            private void StopWorking()
            {
                CancelInvoke(nameof(ConditionTick));
                CancelInvoke(nameof(BrokenLegTick));
                CancelInvoke(nameof(NoSprintTick));
            }

            private void BrokenLegTick()
            {
                if (!Data[UserId].ContainsKey(PremadeStatusConditions.BrokenLeg.Id))
                {
                    CancelInvoke(nameof(BrokenLegTick));
                    return;
                }
                if (basePlayer.isMounted) { return; }
                var input = basePlayer.serverInput;
                if (input == null || !(input.IsDown(BUTTON.FORWARD) || input.IsDown(BUTTON.BACKWARD) || input.IsDown(BUTTON.LEFT) || input.IsDown(BUTTON.RIGHT) || input.IsDown(BUTTON.JUMP))) { return; }
                var config = PremadeStatusConditions.BrokenLeg.Config;
                PlayEffect(basePlayer, Prefabs.EFFECT_STAGGER, true);
                PlayEffect(basePlayer, Prefabs.EFFECT_SCREAM, true);
                ShowOverlayEffect(basePlayer, id:PremadeStatusConditions.BrokenLeg.Id, color:"1 0 0 0.5", imageLibraryNameOrAssetPath: "assets/icons/circle_gradient.png", material: "", duration:0.5f, fadeIn:0, fadeOut:1f, transform: new Game.Rust.Cui.CuiRectTransformComponent
                {
                    AnchorMin = "-0.5 -0.5",
                    AnchorMax = "1.5 0.5"
                });
                basePlayer.metabolism.bleeding.Add(0.3f);
                basePlayer.Hurt(2 * config.DamageScale, Rust.DamageType.Bleeding);
            }

            private void NoSprintTick()
            {
                if (!Data[UserId].ContainsKey(PremadeStatusConditions.BrokenLeg.Id))
                {
                    CancelInvoke(nameof(NoSprintTick));
                    return;
                }
                basePlayer.SetPlayerFlag(BasePlayer.PlayerFlags.NoSprint, true);
            }

            private void ConditionTick()
            {
                foreach(var psc in Data[UserId].Values.ToArray())
                {
                    if (psc.HasDuration)
                    {
                        psc.DurationTime -= 1;
                        if (psc.IsExpired)
                        {
                            Helper.RemoveCondition(UserId, psc.StatusConditionId, true);
                            continue;
                        }
                    }
                    if (psc.HasInterval && (!psc.NextIntervalTime.HasValue || psc.NextIntervalTime.Value < Time.realtimeSinceStartup))
                    {
                        psc.StatusCondition.DoIntervalEffect(basePlayer);
                        Interface.Call("OnConditionEffect", basePlayer, psc.StatusConditionId, psc);
                        psc.NextIntervalTime = (long)Time.realtimeSinceStartup + psc.StatusCondition.GetRandomIntervalSeconds();
                    }
                }
            }

            private bool HasAnyConditions => Data[UserId].Count > 0;
            #endregion

            #region Public
            public void Resume(PlayerStatusCondition psc)
            {
                if (psc.StatusConditionId == PremadeStatusConditions.BrokenLeg.Id)
                {
                    if (!IsInvoking(nameof(BrokenLegTick)))
                    {
                        InvokeRepeating(nameof(BrokenLegTick), 2f, 2f);
                    }
                    if (!IsInvoking(nameof(NoSprintTick)))
                    {
                        InvokeRepeating(nameof(NoSprintTick), 0.1f, 0.1f);
                    }
                }
                if (IsInvoking(nameof(ConditionTick))) { return; }
                StartWorking();
            }
            public void StopIfNoConditions()
            {
                if (!HasAnyConditions)
                {
                    StopWorking();
                }
            }
            #endregion
        }
    }
}

namespace Oxide.Plugins
{
    partial class InjuriesAndDiseases : CovalencePlugin
    {
        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Death Removes Conditions")]
            public bool DeathRemovesConditions = true;

            [JsonProperty(PropertyName = "Pause on Disconnect")]
            public bool PauseOnDisconnect = true;

            [JsonProperty(PropertyName = "Require Diagnosis")]
            public bool RequireDiagnosis = false;

            [JsonProperty(PropertyName = "Show Doctor Indicator")]
            public bool ShowDoctorIndicator = true;

            [JsonProperty(PropertyName = "Messages Enabled")]
            public bool MessagesEnabled = true;

            [JsonProperty(PropertyName = "Message Icon ID")]
            public long MessagesIconID = 0;

            [JsonProperty(PropertyName = "Images")]
            public ImagesConfig Images = new ImagesConfig();

            [JsonProperty(PropertyName = "Status Conditions")]
            public Dictionary<string, StatusConditionConfig> StatusConditions = PremadeStatusConditions.DefaultConfigs.ToDictionary(x => x.Key, x => x.Value.Copy());

            [JsonProperty(PropertyName = "Version")]
            public VersionNumber Version { get; set; } = new VersionNumber(0, 0, 0);
        }

        public class ImagesConfig
        {
            public string Undiagnosed = "assets/icons/info.png";
            public string Doctor = "https://i.ibb.co/mCdGMP0/shkpDE2.png";
        }

        #region Config Classes
        public enum InflictionDamageAction
        {
            any, melee, ranged
        }
        public class StatusConditionConfig
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled { get; set; }

            [JsonProperty(PropertyName = "Likeliness", NullValueHandling = NullValueHandling.Ignore)]
            public float? Chance { get; set; } = null;

            [JsonProperty(PropertyName = "Icon")]
            public string Icon { get; set; }

            [JsonProperty(PropertyName = "From Legshots", NullValueHandling = NullValueHandling.Ignore)]
            public bool? FromLegShots { get; set; } = null;

            [JsonProperty(PropertyName = "From Falling", NullValueHandling = NullValueHandling.Ignore)]
            public bool? FromFalling { get; set; } = null;

            [JsonProperty(PropertyName = "Damage Scale")]
            public float DamageScale { get; set; } = 1f;

            [JsonProperty(PropertyName = "Show Duration")]
            public bool ShowDuration { get; set; } = true;

            [JsonProperty(PropertyName = "Show Indicator")]
            public bool ShowIndicator { get; set; } = true;

            [JsonProperty(PropertyName = "Cure Items")]
            public Dictionary<string, float> CureItems { get; set; } = new Dictionary<string, float>();

            [JsonProperty(PropertyName = "Interval Min Seconds", NullValueHandling = NullValueHandling.Ignore)]
            public int? MinIntervalSeconds { get; set; } = null;

            [JsonProperty(PropertyName = "Interval Max Seconds", NullValueHandling = NullValueHandling.Ignore)]
            public int? MaxIntervalSeconds { get; set; } = null;

            [JsonProperty(PropertyName = "Duration Min Seconds")]
            public int? MinDurationSeconds { get; set; }

            [JsonProperty(PropertyName = "Duration Max Seconds")]
            public int? MaxDurationSeconds { get; set; }

            [JsonProperty(PropertyName = "Move Items to Zombie", NullValueHandling = NullValueHandling.Ignore)]
            public bool? MoveItemsToZombie { get; set; } = null;

            [JsonProperty(PropertyName = "Reanimation Seconds", NullValueHandling = NullValueHandling.Ignore)]
            public int? ReanimationSeconds { get; set; } = null;

            [JsonProperty(PropertyName = "Infliction Entities", NullValueHandling = NullValueHandling.Ignore)]
            public Dictionary<string, float> InflictionEntities { get; set; } = new Dictionary<string, float>();

            [JsonConverter(typeof(StringEnumConverter))]
            [JsonProperty(PropertyName = "Infliction Damage Action", NullValueHandling = NullValueHandling.Ignore)]
            public InflictionDamageAction? InflictionDamageAction { get; set; } = null;

            [JsonProperty(PropertyName = "Infliction Items", NullValueHandling = NullValueHandling.Ignore)]
            public Dictionary<string, float> InflictionItems { get; set; } = new Dictionary<string, float>();

            [JsonIgnore]
            public Action<BasePlayer> BeginEffect { get; set; } = (basePlayer) => { };

            [JsonIgnore]
            public Action<BasePlayer> IntervalEffect { get; set; } = (basePlayer) => { };

            public StatusConditionConfig Copy()
            {
                return new StatusConditionConfig
                {
                    Enabled = this.Enabled,
                    Chance = this.Chance,
                    DamageScale = this.DamageScale,
                    MinDurationSeconds = this.MinDurationSeconds,
                    MaxDurationSeconds = this.MaxDurationSeconds,
                    ShowDuration = this.ShowDuration,
                    ShowIndicator = this.ShowIndicator,
                    CureItems = this.CureItems,
                    FromFalling = this.FromFalling,
                    FromLegShots = this.FromLegShots,
                    Icon = this.Icon,
                    MinIntervalSeconds = this.MinIntervalSeconds,
                    MaxIntervalSeconds = this.MaxIntervalSeconds,
                    MoveItemsToZombie = this.MoveItemsToZombie,
                    ReanimationSeconds = this.ReanimationSeconds,
                    InflictionEntities = this.InflictionEntities,
                    InflictionItems = this.InflictionItems,
                    InflictionDamageAction = this.InflictionDamageAction
                };
            }
        }
        #endregion

        protected override void LoadConfig()
        {
            base.LoadConfig();
            var recommended = $"It is recommended to backup your current oxide/config/{Name}.json and oxide/lang/en/{Name}.json files and remove them to generate fresh ones.";
            var usingDefault = "Overriding configuration with default values to avoid errors.";

            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) { throw new Exception(); }
                else if (config.Version.Major != Version.Major || config.Version.Minor != Version.Minor) throw new NotSupportedException();
                SaveConfig();
            }
            catch (NotSupportedException)
            {
                if (ShowConfigWarnings)
                {
                    PrintError($"Your configuration file is out of date. Your configuration file is for v{config.Version.Major}.{config.Version.Minor}.{config.Version.Patch} but the plugin is on v{Version.Major}.{Version.Minor}.{Version.Patch}. {recommended}");
                    PrintWarning(usingDefault);
                }
                LoadDefaultConfig();
            }
            catch (Exception)
            {
                if (ShowConfigWarnings)
                {
                    PrintError($"Your configuration file contains an error. {recommended}");
                    PrintWarning(usingDefault);
                }
                LoadDefaultConfig();
            }
            ShowConfigWarnings = true;
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            config.Version = new VersionNumber(Version.Major, Version.Minor, Version.Patch);
        }
    }
}

namespace Oxide.Plugins.InjuriesAndDiseasesExtensionMethods
{
    public static class ExtensionMethods
    {
        public static ulong UserId(this IPlayer player)
        {
            return ulong.Parse(player.Id);
        }

        public static V GetValueOrNew<K, V>(this Dictionary<K, V> dict, K key) where V : new()
        {
            var value = dict.GetValueOrDefault(key);
            if (value != null) { return value; }
            dict[key] = new V();
            return dict[key];
        }

        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (T element in source)
                action(element);
        }

        public static bool IsMelee(this HitInfo hitInfo) => hitInfo.Weapon == null || hitInfo.Weapon?.GetItem()?.GetHeldEntity() is BaseMelee;

        public static bool IsAssetPath(this string str) => str.StartsWith("assets/");
    }
}

namespace Oxide.Plugins
{
    public partial class InjuriesAndDiseases
    {
        public static class Helper
        {
            public static void ResumeCondition(PlayerStatusCondition psc)
            {
                Data.GetValueOrNew(psc.UserId)[psc.StatusConditionId] = psc;
                if (psc.Revealed)
                {
                    Statuses.RemoveStatus(psc.UserId, Undiagnosed(psc.StatusConditionId));
                    Statuses.SetStatus(psc.UserId, psc.StatusConditionId, psc.DurationTime);
                }
                else
                {
                    Statuses.RemoveStatus(psc.UserId, psc.StatusConditionId);
                    Statuses.SetStatus(psc.UserId, Undiagnosed(psc.StatusConditionId), psc.DurationTime);
                }
                var behavior = PLUGIN.Behaviours.GetValueOrDefault(psc.UserId); if (behavior == null) { return; }
                behavior.Resume(psc);
            }

            public static void SetCondition(ulong userId, string statusConditionId, bool revealed)
            {
                var statusCondition = PremadeStatusConditions.ALL_BY_ID.GetValueOrDefault(statusConditionId);
                if (statusCondition == null) { return; }
                var alreadyInflicted = Helper.HasCondition(userId, statusConditionId);
                var psc = new PlayerStatusCondition(userId, statusCondition, revealed);
                Data.GetValueOrNew(userId)[statusConditionId] = psc;
                if (psc.Revealed)
                {
                    Statuses.RemoveStatus(psc.UserId, Undiagnosed(statusConditionId));
                    Statuses.SetStatus(psc.UserId, statusConditionId, psc.DurationTime);
                    if (!alreadyInflicted)
                    {
                        Message(psc.UserId, statusCondition.LangReveal);
                    }
                }
                else
                {
                    Statuses.RemoveStatus(psc.UserId, statusConditionId);
                    Statuses.SetStatus(psc.UserId, Undiagnosed(statusConditionId), psc.DurationTime);
                }
                var behavior = PLUGIN.Behaviours.GetValueOrDefault(userId); if (behavior == null) { return; }
                behavior.Resume(psc);
            }

            public static void RemoveCondition(ulong userId, string statusConditionId, bool cured)
            {
                var pscs = Data.GetValueOrDefault(userId);
                if (pscs == null) { return; }
                var psc = pscs.GetValueOrDefault(statusConditionId);
                if (psc == null) { return; }
                var revealed = psc.Revealed;
                pscs.Remove(statusConditionId);
                Statuses.RemoveStatus(userId, statusConditionId);
                Statuses.RemoveStatus(userId, Undiagnosed(statusConditionId));
                var behavior = PLUGIN.Behaviours.GetValueOrDefault(userId); if (behavior == null) { return; }
                behavior.StopIfNoConditions();
                var statusCondition = PremadeStatusConditions.ALL_BY_ID.GetValueOrDefault(statusConditionId);
                if (statusCondition != null && cured && revealed)
                {
                    Message(userId, statusCondition.LangCured);
                }
            }

            public static void RemoveAllConditions(ulong userId, bool cured)
            {
                Data.GetValueOrDefault(userId).Keys.ToArray().ForEach(x => RemoveCondition(userId, x, cured));
                var behavior = PLUGIN.Behaviours.GetValueOrDefault(userId); if (behavior == null) { return; }
                behavior.StopIfNoConditions();
            }

            public static bool IsRevealed(ulong userId, string statusConditionId) => Data[userId].GetValueOrDefault(statusConditionId)?.Revealed ?? false;

            public static bool HasCondition(ulong userId, string statusConditionId) => Data[userId].ContainsKey(statusConditionId);

            public static bool IsValidPlayer(BasePlayer basePlayer) => basePlayer != null && !basePlayer.IsNpc && !basePlayer.IsBot;

            public static PlayerStatusCondition GetCondition(ulong userId, string statusConditionId) => Data[userId].GetValueOrDefault(statusConditionId);

            public static List<PlayerStatusCondition> GetConditions(ulong userId) => Data[userId].Values.ToList();

            public static void RevealCondition(ulong userId, string statusConditionId)
            {
                var psc = Data[userId].GetValueOrDefault(statusConditionId);
                if (psc == null || psc.Revealed) { return; }
                psc.Revealed = true;
                Statuses.RemoveStatus(userId, Undiagnosed(statusConditionId));
                Statuses.SetStatus(userId, statusConditionId, psc.DurationTime);
                var statusCondition = PremadeStatusConditions.ALL_BY_ID.GetValueOrDefault(statusConditionId);
                if (statusCondition == null) { return; }
                Message(userId, statusCondition.LangReveal);
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class InjuriesAndDiseases : CovalencePlugin
    {
        private bool IsDoctor(BasePlayer basePlayer)
        {
            return permission.UserHasPermission(basePlayer.UserIDString, PermissionDoctor);
        }

        private bool InflictByChance(BasePlayer basePlayer, StatusCondition statusCondition, int rolls = 1)
        {
            if (!Helper.IsValidPlayer(basePlayer)) { return false; }
            if (Helper.HasCondition(basePlayer.userID, statusCondition.Id)) { return false; }
            for(int i = 0; i < rolls; i++)
            {
                var roll = UnityEngine.Random.Range(0f, 1f);
                if (config.StatusConditions[statusCondition.Id].Chance >= roll)
                {
                    var revealed = IsDoctor(basePlayer) || !config.RequireDiagnosis;
                    Helper.SetCondition(basePlayer.userID, statusCondition.Id, revealed);
                    return true;
                }
            }
            return false;
        }

        private bool IsInflictionEntity(StatusCondition condition, BaseEntity entity)
        {
            return condition.Config.InflictionEntities.ContainsKey(entity.ShortPrefabName) || (IsZombieHordeEntity(entity) && condition.Config.InflictionEntities.Any(x => x.Key.ToLower() == "zombie")) || (IsBotRespawnEntity(entity.net.ID.Value) && condition.Config.InflictionEntities.Any(x => x.Key.ToLower() == "botrespawn"));
        }

        private float GetInflictionEntityChanceFromConfig(StatusCondition condition, string key) => condition.Config.InflictionEntities?.GetValueOrDefault(key) ?? 0f;

        private float GetInflictionEntityChance(StatusCondition condition, BaseEntity entity)
        {
            var chance = GetInflictionEntityChanceFromConfig(condition, entity.ShortPrefabName);
            return chance != 0f ? chance : IsZombieHordeEntity(entity) ? GetInflictionEntityChanceFromConfig(condition, "zombie") : IsBotRespawnEntity(entity.net.ID.Value) ? GetInflictionEntityChanceFromConfig(condition, "botrespawn") : 0f;
        }

        private bool InflictByEntity(BasePlayer basePlayer, BaseEntity entity, bool isMelee)
        {
            if (!Helper.IsValidPlayer(basePlayer)) { return false; }
            if (basePlayer == null || entity == null) { return false; }
            foreach (var condition in PremadeStatusConditions.ALL.Where(x => x != null && x.IsEnabled && x.Config.InflictionEntities != null && IsInflictionEntity(x, entity)))
            {
                if (condition.MatchesInflictionAction(isMelee) && RollSucceeds(GetInflictionEntityChance(condition, entity)))
                {
                    var revealed = IsDoctor(basePlayer) || !config.RequireDiagnosis;
                    Helper.SetCondition(basePlayer.userID, condition.Id, revealed);
                    return true;
                }
            }
            return false;
        }

        private bool InflictByItem(BasePlayer basePlayer, Item item)
        {
            if (!Helper.IsValidPlayer(basePlayer)) { return false; }
            foreach (var condition in PremadeStatusConditions.ALL.Where(x => x != null && x.IsEnabled && x.Config.InflictionItems != null))
            {
                var chance = GetItemValueByShortNameWithSkin(condition.Config.InflictionItems, item);
                if (chance.HasValue && RollSucceeds(chance.Value))
                {
                    var revealed = IsDoctor(basePlayer) || !config.RequireDiagnosis;
                    Helper.SetCondition(basePlayer.userID, condition.Id, revealed);
                    return true;
                }
            }
            return false;
        }

        private void CureByItem(BasePlayer basePlayer, Item item)
        {
            if (!Helper.IsValidPlayer(basePlayer)) { return; }
            foreach (var psc in Helper.GetConditions(basePlayer.userID))
            {
                var cureItems = psc.StatusCondition.Config.CureItems;
                var chance = GetItemValueByShortNameWithSkin(cureItems, item);
                if (chance.HasValue && RollSucceeds(chance.Value))
                {
                    Helper.RemoveCondition(basePlayer.userID, psc.StatusConditionId, true);
                }
            }
        }

        private float? GetItemValueByShortNameWithSkin(Dictionary<string, float> dict, Item item)
        {
            if (dict == null || item == null)
            {
                return null;
            }
            float value;
            if (dict.TryGetValue($"{item.info.shortname}#{item.skin}", out value) || dict.TryGetValue(item.info.shortname, out value))
            {
                return value;
            }
            return null;
        }
    }
}

namespace Oxide.Plugins
{
    partial class InjuriesAndDiseases : CovalencePlugin
    {
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["usage"] = "Usage: /{0} {1}",
                ["success"] = "Command was successful.",
                ["doctor"] = "DOCTOR",
                ["motto"] = "Do No Harm",
                ["undiagnosed"] = "UNDIAGNOSED",
                ["see a doctor"] = "Visit Doctor",
                ["look at player"] = "You must be looking at a player to diagnose them.",
                ["concussion"] = "CONCUSSION",
                ["concussion reveal"] = "You are suffering from head trauma.",
                ["concussion diagnosis"] = "{0} seems to be suffering from head trauma. This will require some time to heal.",
                ["concussion cured"] = "Your are no longer inflicted with head trauma.",
                ["brokenleg"] = "BROKEN LEG",
                ["brokenleg reveal"] = "You have a leg fracture and it is difficult to move.",
                ["brokenleg diagnosis"] = "{0} has a severe leg fracture. This will require some time to heal.",
                ["brokenleg cured"] = "You are able to put weight on your leg once again.",
                ["foodpoisoning"] = "FOOD POISONING",
                ["foodpoisoning reveal"] = "You have a bad case of food poisoning.",
                ["foodpoisoning diagnosis"] = "{0} has a bad case of food poisoning. This can be remedied by drinking tea.",
                ["foodpoisoning cured"] = "Your stomach has settled.",
                ["rabies"] = "RABIES",
                ["rabies reveal"] = "You have contracted rabies.",
                ["rabies diagnosis"] = "{0} is showing symptoms of rabies. It may be a good idea to put them out of their misery.",
                ["rabies cured"] = "Miraculously, you have been cured of rabies!",
                ["tapeworm"] = "TAPEWORM",
                ["tapeworm reveal"] = "You have picked up a parasite.",
                ["tapeworm diagnosis"] = "{0} has a parasitic infection. Antibiotics can be taken to fight off the parasite.",
                ["tapeworm cured"] = "Your body has managed to fight off the parasite.",
                ["z13virus"] = "UNKNOWN",
                ["z13virus reveal"] = "You have become exposed to a mysterious pathogen.",
                ["z13virus diagnosis"] = "{0} is showing symptoms of rabies. It may be a good idea to put them out of their misery.",
                ["z13virus cured"] = "Your strange symptoms have subsided, did the cure really work?",
                ["healthy diagnosis"] = "{0} does not seem to be showing any symptoms of any unknown issues."
            }, this);
        }

        private static string Undiagnosed(string statusConditionId) => $"{statusConditionId} undiagnosed";
        private static string Lang(string key, string userIdString, params object[] args) => string.Format(PLUGIN.lang.GetMessage(key, PLUGIN, userIdString), args);
        private static string Lang(string key, BasePlayer basePlayer, params object[] args) => Lang(key, basePlayer.UserIDString, args);
        private static string Lang(Plugin plugin, string key, BasePlayer basePlayer, params object[] args) => string.Format(PLUGIN.lang.GetMessage(key, plugin, basePlayer == null ? null : basePlayer?.UserIDString), args);
    }
}

namespace Oxide.Plugins
{
    public partial class InjuriesAndDiseases
    {
        void OnServerSave()
        {
            SaveAll();
        }

        private void OnPlayerConnected(BasePlayer basePlayer)
        {
            if (!Helper.IsValidPlayer(basePlayer)) { return; }
            var obj = basePlayer.gameObject.AddComponent<ConditionBehaviour>();
            Behaviours[basePlayer.userID] = obj;
            if (IsDoctor(basePlayer))
            {
                Statuses.SetStatus(basePlayer.userID, "doctor");
            }
            else
            {
                Statuses.RemoveStatus(basePlayer.userID, "doctor");
            }
            foreach (var status in PremadeStatusConditions.ALL)
            {
                Statuses.RemoveStatus(basePlayer.userID, status.Id);
            }
            foreach (var psc in Data[basePlayer.userID].Values.ToArray())
            {
                Helper.ResumeCondition(psc);
            }
        }

        private void OnPlayerDisconnected(BasePlayer basePlayer)
        {
            var obj = Behaviours.GetValueOrDefault(basePlayer.userID);
            UnityEngine.Object.Destroy(obj);
        }

        Dictionary<ulong, ZombieLootInfo> EntityIdToItemList = new Dictionary<ulong, ZombieLootInfo>();
        HashSet<ulong> PlayerIdsToSpawnZombieAt = new HashSet<ulong>();
        void OnPlayerDeath(BasePlayer basePlayer, HitInfo info)
        {
            if (basePlayer == null) { Debug($"Dead player was null"); return; }
            Debug($"{basePlayer.displayName} has died Remove Conditions={config.DeathRemovesConditions}");
            if (Helper.HasCondition(basePlayer.userID, PremadeStatusConditions.Z13Virus.Id))
            {
                PlayerIdsToSpawnZombieAt.Add(basePlayer.userID);
            }
            if (config.DeathRemovesConditions)
            {
                Debug($"Removing conditions for {basePlayer.displayName} because they died");
                Helper.RemoveAllConditions(basePlayer.userID, false);
            }
        }

        void OnPlayerCorpseSpawned(BasePlayer basePlayer, PlayerCorpse corpse)
        {
            if (!Helper.IsValidPlayer(basePlayer) || corpse == null) { return; }
            if (!PlayerIdsToSpawnZombieAt.Contains(basePlayer.userID)) { return; }
            timer.In(PremadeStatusConditions.Z13Virus.Config.ReanimationSeconds.Value, () =>
            {
                if (!PlayerIdsToSpawnZombieAt.Contains(basePlayer.userID)) { return; }
                PlayerIdsToSpawnZombieAt.Remove(basePlayer.userID);
                var pos = corpse.transform.position;
                BasePlayer entity = (BasePlayer)GameManager.server.CreateEntity(Prefabs.ZOMBIE_PREFAB, pos);
                entity.Spawn();
                EntityIdToItemList[entity.userID] = new ZombieLootInfo(basePlayer);
                if (PremadeStatusConditions.Z13Virus.Config.MoveItemsToZombie.Value)
                {
                    foreach (var item in corpse.containers[0].itemList)
                    {
                        Item itemCopy = ItemManager.CreateByItemID(item.info.itemid, item.amount, item.skin);
                        itemCopy.text = item.text;
                        itemCopy.maxCondition = item.maxCondition;
                        itemCopy.condition = item.condition;
                        EntityIdToItemList[entity.userID].Items.Add(itemCopy);
                    }
                    corpse.containers[0].Clear();
                }
                corpse.Die();
            });
        }

        void OnLootEntity(BasePlayer basePlayer, NPCPlayerCorpse entity)
        {
            if (!Helper.IsValidPlayer(basePlayer) || entity == null || !EntityIdToItemList.ContainsKey(entity.playerSteamID)) { return; }
            var info = EntityIdToItemList[entity.playerSteamID];
            entity.containers[0].Clear();
            entity.playerName = info.PlayerDisplayName;
            if (PremadeStatusConditions.Z13Virus.Config.MoveItemsToZombie.Value)
            {
                foreach (var item in info.Items)
                {
                    entity.containers[0].Insert(item);
                }
            }
            EntityIdToItemList.Remove(entity.playerSteamID);
        }

        void OnItemAction(Item item, string action, BasePlayer basePlayer)
        {
            if (item == null || action == null || basePlayer == null) { return; }
            var oldCalories = basePlayer.metabolism.calories.value;
            var oldHydration = basePlayer.metabolism.hydration.value;
            if (item.info.category == ItemCategory.Food && action == "consume")
            {
                var conditions = Behaviours.GetValueOrDefault(basePlayer.userID); if (conditions == null) { return; }
                if (Helper.HasCondition(basePlayer.userID, PremadeStatusConditions.Tapeworm.Id))
                {
                    NextTick(() =>
                    {
                        if (basePlayer != null)
                        {
                            var config = PremadeStatusConditions.Tapeworm.Config;
                            var newCalories = basePlayer.metabolism.calories.value;
                            var newHydration = basePlayer.metabolism.hydration.value;
                            if (newCalories > oldCalories)
                            {
                                var calTake = ((newCalories - oldCalories) / 2) * config.DamageScale;
                                basePlayer.metabolism.ApplyChange(MetabolismAttribute.Type.Calories, -calTake, 1);
                            }
                            if (newHydration > oldHydration)
                            {
                                var hydrationTake = ((newHydration - oldHydration) / 2) * config.DamageScale;
                                basePlayer.metabolism.ApplyChange(MetabolismAttribute.Type.Hydration, -hydrationTake, 1);
                            }
                        }
                    });
                }
            }
        }

        void OnHealingItemUse(MedicalTool tool, BasePlayer basePlayer)
        {
            if (tool == null || basePlayer == null) { return; }
            var item = tool.GetItem();
            if (item == null) { return; }
            InflictByItem(basePlayer, item);
            CureByItem(basePlayer, item);
        }

        void OnItemUse(Item item, int amountToUse)
        {
            if (item == null)
            {
                return;
            }
            var basePlayer = item.GetOwnerPlayer();
            if (basePlayer == null)
            {
                if (item.parentItem != null)
                {
                    basePlayer = item.parentItem.GetOwnerPlayer();
                    if (basePlayer != null)
                    {
                        InflictByItem(basePlayer, item.parentItem);
                        CureByItem(basePlayer, item.parentItem);
                    }
                }
                return;
            }
            InflictByItem(basePlayer, item);
            CureByItem(basePlayer, item);
        }

        void OnEntityTakeDamage(BasePlayer basePlayer, HitInfo hitInfo)
        {
            if (!Helper.IsValidPlayer(basePlayer) || hitInfo == null) { return; }
            var totalDamage = hitInfo.damageTypes.Total();
            var beforeHp = basePlayer.health;
            NextTick(() =>
            {
                if (basePlayer == null || basePlayer.IsDead()) { return; }
                var healthLost = beforeHp - basePlayer.health;
                var tookDamage = totalDamage > 0 && healthLost > 0;
                if (!tookDamage)
                {
                    Debug($"Damage blocked, skipping");
                    return;
                }
                if (PremadeStatusConditions.BrokenLeg.IsEnabled && PremadeStatusConditions.BrokenLeg.Config.FromFalling.Value && hitInfo.damageTypes.Get(Rust.DamageType.Fall) > 30)
                {
                    var rolls = (int)Math.Ceiling((totalDamage - 10) / 5);
                    Debug($"Broken Leg from Falling Damage={totalDamage} Rolls={rolls} Chance={PremadeStatusConditions.BrokenLeg.Config.Chance.Value} Prob={ProbAtleastOne(rolls, PremadeStatusConditions.BrokenLeg.Config.Chance.Value)}");
                    InflictByChance(basePlayer, PremadeStatusConditions.BrokenLeg, rolls);
                }
                else if (PremadeStatusConditions.BrokenLeg.IsEnabled && PremadeStatusConditions.BrokenLeg.Config.FromLegShots.Value && hitInfo.boneArea == HitArea.Leg && totalDamage > 30)
                {
                    var rolls = (int)Math.Ceiling((totalDamage - 30) / 5);
                    Debug($"Broken Leg from Leg Shot Damage={totalDamage} Rolls={rolls} Chance={PremadeStatusConditions.BrokenLeg.Config.Chance.Value} Prob={ProbAtleastOne(rolls, PremadeStatusConditions.BrokenLeg.Config.Chance.Value)}");
                    if (InflictByChance(basePlayer, PremadeStatusConditions.BrokenLeg, rolls))
                    {
                        PlayEffect(basePlayer, Prefabs.EFFECT_LEG_BREAK, false);
                    }
                }
                else if (PremadeStatusConditions.Concussion.IsEnabled && hitInfo.isHeadshot && totalDamage > 30)
                {
                    var rolls = (int)Math.Ceiling((totalDamage - 30) / 5);
                    Debug($"Concussion Damage={totalDamage} Rolls={rolls} Chance={PremadeStatusConditions.Concussion.Config.Chance.Value} Prob={ProbAtleastOne(rolls, PremadeStatusConditions.Concussion.Config.Chance.Value)}");
                    InflictByChance(basePlayer, PremadeStatusConditions.Concussion, rolls);
                }
                else if (hitInfo?.Initiator != null)
                {
                    InflictByEntity(basePlayer, hitInfo?.Initiator, hitInfo?.IsMelee() ?? false);
                }
            });
        }

        void OnUserPermissionGranted(string id, string perm)
        {
            if (perm != PermissionDoctor) { return; }
            if (permission.UserHasPermission(id, perm)) { Statuses.SetStatus(ulong.Parse(id), "doctor"); }
        }

        void OnUserPermissionRevoked(string id, string perm)
        {
            if (perm != PermissionDoctor) { return; }
            if (!permission.UserHasPermission(id, perm)) { Statuses.RemoveStatus(ulong.Parse(id), "doctor"); }
        }

        void OnGroupPermissionGranted(string name, string perm)
        {
            if (perm != PermissionDoctor) { return; }
            foreach (var userId in permission.GetUsersInGroup(name).Select(x => x.Split(" ")[0]))
            {
                var player = FindPlayer(userId); if (player == null) { continue; }
                OnUserPermissionGranted(player.Id, perm);
            }
        }

        void OnGroupPermissionRevoked(string name, string perm)
        {
            if (perm != PermissionDoctor) { return; }
            foreach (var userId in permission.GetUsersInGroup(name).Select(x => x.Split(" ")[0]))
            {
                var player = FindPlayer(userId); if (player == null) { continue; }
                OnUserPermissionRevoked(player.Id, perm);
            }
        }
    }
}

namespace Oxide.Plugins
{
    public partial class InjuriesAndDiseases
    {
        public static SaveData Data = new SaveData();

        public class SaveData : Dictionary<ulong, PlayerStatusConditionList>
        {
            new public PlayerStatusConditionList this[ulong key]
            {
                get
                {
                    if (!base.ContainsKey(key))
                    {
                        base[key] = new PlayerStatusConditionList();
                    }
                    return base[key];
                }
                set
                {
                    base[key] = value;
                }
            }
        }

        public class PlayerStatusConditionList : Dictionary<string, PlayerStatusCondition>
        {
            new public PlayerStatusCondition this[string key]
            {
                get
                {
                    if (!base.ContainsKey(key))
                    {
                        return null;
                    }
                    return base[key];
                }
                set
                {
                    base[key] = value;
                }
            }
        }

        public class PlayerStatusCondition
        {
            public ulong UserId;
            public string StatusConditionId;
            public int? DurationTime;
            public bool Revealed;

            [JsonIgnore]
            public bool HasDuration => DurationTime.HasValue;

            [JsonIgnore]
            public bool HasInterval => StatusCondition.HasInterval;

            [JsonIgnore]
            public bool IsExpired => DurationTime <= 0;

            [JsonIgnore]
            public float? NextIntervalTime = null;

            [JsonIgnore]
            private StatusCondition _statusCondition = null;
            [JsonIgnore]
            public StatusCondition StatusCondition
            {
                get
                {
                    if (_statusCondition == null)
                    {
                        _statusCondition = PremadeStatusConditions.ALL_BY_ID.GetValueOrDefault(StatusConditionId);
                    }
                    return _statusCondition;
                }
            }

            public PlayerStatusCondition() { }

            public PlayerStatusCondition(ulong userId, StatusCondition statusCondition, bool revealed)
            {
                UserId = userId;
                StatusConditionId = statusCondition.Id;
                DurationTime = statusCondition.GetRandomDurationSeconds();
                Revealed = revealed;
            }
        }
    }

}

namespace Oxide.Plugins
{
    partial class InjuriesAndDiseases : CovalencePlugin
    {
        public static class Prefabs
        {
            /* ENTITIES */
            public static readonly string ZOMBIE_PREFAB = "assets/prefabs/npc/scarecrow/scarecrow.prefab";
            public static readonly string ZOMBIE_CORPSE = "assets/prefabs/npc/murderer/murderer_corpse.prefab";

            /* EFFECTS */
            public static readonly string EFFECT_DROWN = "assets/bundled/prefabs/fx/player/drown.prefab";
            public static readonly string EFFECT_SCREAM = "assets/bundled/prefabs/fx/player/gutshot_scream.prefab";
            public static readonly string EFFECT_STAGGER = "assets/prefabs/tools/jackhammer/effects/strike_screenshake.prefab";
            public static readonly string EFFECT_BEAR_BREATHE = "assets/prefabs/npc/bear/sound/breathe.prefab";
            public static readonly string EFFECT_SCREAM_LOUD = "assets/bundled/prefabs/fx/player/beartrap_scream.prefab";
            public static readonly string EFFECT_LEG_BREAK = "assets/bundled/prefabs/fx/player/fall-damage.prefab";
            public static readonly string EFFECT_SCREEN_SHAKE = "assets/bundled/prefabs/fx/elevator_arrive.prefab";
            public static readonly string EFFECT_FLINCH_DOWN = "assets/bundled/prefabs/fx/screen_land.prefab";
            public static readonly string EFFECT_ZOOM_IN_OUT = "assets/bundled/prefabs/fx/takedamage_generic.prefab";
            public static readonly string EFFECT_TAKE_HIT = "assets/bundled/prefabs/fx/takedamage_hit_new.prefab";
            public static readonly string EFFECT_VOMIT = "assets/bundled/prefabs/fx/gestures/drink_vomit.prefab";
            public static readonly string EFFECT_SPLASH = "assets/bundled/prefabs/fx/water/midair_splash.prefab";

            /* MATERIALS */
            public static readonly string MAT_BLUR = "assets/content/ui/uibackgroundblur-ingamemenu.mat";
            public static readonly string MAT_POISONED = "assets/content/ui/overlay_poisoned.png";
        }
    }
}

namespace Oxide.Plugins
{
    partial class InjuriesAndDiseases : CovalencePlugin
    {
        public static class PremadeStatusConditions
        {
            public static readonly StatusCondition Concussion = new StatusCondition("concussion");
            public static readonly StatusCondition FoodPoisoning = new StatusCondition("foodpoisoning");
            public static readonly StatusCondition Z13Virus = new StatusCondition("z13virus");
            public static readonly StatusCondition Rabies = new StatusCondition("rabies");
            public static readonly StatusCondition BrokenLeg = new StatusCondition("brokenleg");
            public static readonly StatusCondition Tapeworm = new StatusCondition("tapeworm");

            public static readonly Dictionary<string, StatusConditionConfig> DefaultConfigs = new Dictionary<string, StatusConditionConfig>()
            {
                [Concussion.Id] = new StatusConditionConfig
                {
                    Enabled = true,
                    Icon = "https://i.ibb.co/sscH1Wz/concussion.png",
                    Chance = 0.05f,
                    MinIntervalSeconds = 18,
                    MaxIntervalSeconds = 20,
                    MinDurationSeconds = 360,
                    MaxDurationSeconds = 400,
                    IntervalEffect = (basePlayer) =>
                    {
                        var roll = UnityEngine.Random.Range(0f, 1f);
                        if (roll > 0.4f)
                        {
                            ShowOverlayEffect(basePlayer, Concussion.Id, RustColor.Black, "", Prefabs.MAT_BLUR, 0.5f, 1f, 1f);
                            PLUGIN.timer.In(1.75f, () =>
                            {
                                ShowOverlayEffect(basePlayer, Concussion.Id, RustColor.Black, "", Prefabs.MAT_BLUR, 1f, 1f, 1f);
                            });
                            PLUGIN.timer.In(3.75f, () =>
                            {
                                ShowOverlayEffect(basePlayer, Concussion.Id, RustColor.Black, "", Prefabs.MAT_BLUR, 1.25f, 1f, 4f);
                            });
                        }
                        else
                        {
                            ShowOverlayEffect(basePlayer, Concussion.Id, RustColor.Black, "", Prefabs.MAT_BLUR, 0.75f, 1f, 4f);
                        }
                    }
                },
                [FoodPoisoning.Id] = new StatusConditionConfig
                {
                    Enabled = true,
                    Icon = "https://i.ibb.co/xDh6q1z/UplSFWD.png",
                    MinIntervalSeconds = 10,
                    MaxIntervalSeconds = 12,
                    MinDurationSeconds = 200,
                    MaxDurationSeconds = 240,
                    CureItems = new Dictionary<string, float>
                    {
                        ["healingtea"] = 0.5f,
                        ["healingtea.advanced"] = 0.75f,
                        ["healingtea.pure"] = 1f
                    },
                    InflictionItems = new Dictionary<string, float>
                    {
                        ["chicken.spoiled"] = 0.75f,
                        ["humanmeat.spoiled"] = 0.75f,
                        ["wolfmeat.spoiled"] = 0.75f,
                        ["jar.pickle"] = 0.25f,
                        ["apple.spoiled"] = 0.1f
                    },
                    IntervalEffect = (basePlayer) =>
                    {
                        var config = FoodPoisoning.Config;
                        basePlayer.SetPlayerFlag(BasePlayer.PlayerFlags.NoSprint, true);
                        PlayEffect(basePlayer, Prefabs.EFFECT_VOMIT, false);
                        PlayEffect(basePlayer, Prefabs.EFFECT_SPLASH, false);
                        PlayEffect(basePlayer, Prefabs.EFFECT_FLINCH_DOWN, true);
                        ShowOverlayEffect(basePlayer, FoodPoisoning.Id, "0.3 0.6 0.3 0.9", Prefabs.MAT_POISONED, "", 0.5f, 0f, 1f);
                        basePlayer.metabolism.ApplyChange(MetabolismAttribute.Type.Calories, -40 * config.DamageScale, 1);
                        basePlayer.metabolism.ApplyChange(MetabolismAttribute.Type.Hydration, -40 * config.DamageScale, 1);
                        basePlayer.Hurt(1 * FoodPoisoning.Config.DamageScale, Rust.DamageType.Poison);
                    }
                },
                [Z13Virus.Id] = new StatusConditionConfig
                {
                    Enabled = true,
                    Icon = "https://i.ibb.co/bPhYqN2/hIEhCUR.png",
                    ShowDuration = false,
                    MinIntervalSeconds = 18,
                    MaxIntervalSeconds = 20,
                    MinDurationSeconds = null,
                    MaxDurationSeconds = null,
                    MoveItemsToZombie = true,
                    ReanimationSeconds = 10,
                    InflictionDamageAction = InflictionDamageAction.melee,
                    IntervalEffect = (basePlayer) =>
                    {
                        var config = Z13Virus.Config;
                        PlayEffect(basePlayer, Prefabs.EFFECT_BEAR_BREATHE, false);
                        PlayEffect(basePlayer, Prefabs.EFFECT_ZOOM_IN_OUT, true);
                        ShowOverlayEffect(basePlayer, Z13Virus.Id, "0.7 0 0 0.7", "", "", 0, 0, 1f);
                        basePlayer.Hurt(4 * config.DamageScale, Rust.DamageType.Poison);
                    },
                    InflictionEntities = new Dictionary<string, float>
                    {
                        ["scarecrow"] = 0.5f,
                        ["zombie"] = 0.5f // category used by ZombieHordes
                    }
                },
                [Rabies.Id] = new StatusConditionConfig
                {
                    Enabled = true,
                    Icon = "https://i.ibb.co/NTvggmn/38Wk5EV.png",
                    ShowDuration = false,
                    MinIntervalSeconds = 18,
                    MaxIntervalSeconds = 20,
                    MinDurationSeconds = null,
                    MaxDurationSeconds = null,
                    InflictionDamageAction = InflictionDamageAction.melee,
                    IntervalEffect = (basePlayer) =>
                    {
                        var config = Rabies.Config;
                        basePlayer.Hurt(3 * config.DamageScale, Rust.DamageType.Poison);
                        PlayEffect(basePlayer, Prefabs.EFFECT_DROWN, false);
                        PlayEffect(basePlayer, Prefabs.EFFECT_ZOOM_IN_OUT, true);
                        ShowOverlayEffect(basePlayer, Rabies.Id, "0.7 0 0 0.5", "", "", 0, 0, 1f);
                    },
                    InflictionEntities = new Dictionary<string, float>
                    {
                        ["wolf"] = 0.05f,
                        ["boar"] = 0.03f,
                        ["bear"] = 0.03f,
                        ["polarbear"] = 0.03f
                    }
                },
                [BrokenLeg.Id] = new StatusConditionConfig
                {
                    Enabled = true,
                    Icon = "https://i.ibb.co/gmZ6Ffy/S11eeWk.png",
                    Chance = 0.05f,
                    FromFalling = true,
                    FromLegShots = true,
                    MinDurationSeconds = 300,
                    MaxDurationSeconds = 360
                },
                [Tapeworm.Id] = new StatusConditionConfig
                {
                    Enabled = true,
                    Icon = "https://i.ibb.co/KzBX22p/KTCquA7.png",
                    ShowDuration = false,
                    MinDurationSeconds = null,
                    MaxDurationSeconds = null,
                    CureItems = new Dictionary<string, float>
                    {
                        ["antiradpills"] = 1f
                    },
                    InflictionItems = new Dictionary<string, float>
                    {
                        ["meat.boar"] = 0.5f,
                        ["chicken.raw"] = 0.5f,
                        ["bearmeat"] = 0.5f,
                        ["humanmeat.raw"] = 0.3f,
                        ["deermeat.raw"] = 0.3f,
                        ["horsemeat.raw"] = 0.3f,
                        ["wolfmeat.raw"] = 0.3f,
                        ["fish.raw"] = 0.1f
                    }
                },
            };

            public static readonly List<StatusCondition> ALL = new List<StatusCondition>()
            {
                Concussion,
                FoodPoisoning,
                Z13Virus,
                Rabies,
                BrokenLeg,
                Tapeworm
            };

            public static Dictionary<string, StatusCondition> ALL_BY_ID = ALL.ToDictionary(x => x.Id, x => x);

            public static void InitDefaultStatusConditions()
            {
                foreach (var statusCondition in ALL)
                {
                    statusCondition.LoadConfigValues();
                }
            }

            public static void UpdateAllById()
            {
                ALL_BY_ID = ALL.ToDictionary(x => x.Id, x => x);
            }
        }
    }
}

namespace Oxide.Plugins
{
    public partial class InjuriesAndDiseases : CovalencePlugin
    {
        public static class Statuses
        {
            public static void CreateStatuses()
            {
                PLUGIN.SimpleStatus.CallHook("CreateStatus", PLUGIN, "doctor", DoctorColor.Background, "doctor", DoctorColor.Text, "motto", DoctorColor.Text, ImageNameOrAssetPath("doctor"), DoctorColor.Icon);
                foreach (var status in PremadeStatusConditions.ALL)
                {
                    PLUGIN.SimpleStatus.CallHook("CreateStatus", PLUGIN, $"{status.Id} undiagnosed", StatusColor.Background, "undiagnosed", StatusColor.Text, "see a doctor", StatusColor.Text, ImageNameOrAssetPath("undiagnosed"), StatusColor.Icon);
                    PLUGIN.SimpleStatus.CallHook("CreateStatus", PLUGIN, status.Id, StatusColor.Background, status.Id, StatusColor.Text, null, StatusColor.Text, ImageNameOrAssetPath(status.Icon), StatusColor.Icon);
                }
            }

            public static void SetStatus(ulong userId, string statusId, long? duration = null)
            {
                if (duration == null)
                {
                    PLUGIN.SimpleStatus.CallHook("SetStatus", userId, statusId);
                }
                else
                {
                    PLUGIN.SimpleStatus.CallHook("SetStatus", userId, statusId, (int)duration);
                }
            }

            public static void RemoveStatus(ulong userId, string statusId)
            {
                PLUGIN.SimpleStatus.CallHook("SetStatus", userId, statusId, 0);
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class InjuriesAndDiseases : CovalencePlugin
    {
        public class StatusCondition
        {
            public string Id;
            public int? MinDurationSeconds;
            public int? MaxDurationSeconds;
            public int? MinIntervalSeconds = null;
            public int? MaxIntervalSeconds = null;
            public Action<BasePlayer> BeginEffect;
            public Action<BasePlayer> IntervalEffect;
            public string CustomIcon = null;
            private StatusConditionConfig _config = null;
            public bool IsCustom = false;
            public InflictionDamageAction? InflictionAction = null;

            [JsonIgnore]
            public Plugin SourcePlugin { get; set; } = PLUGIN;

            [JsonIgnore]
            public string Icon
            {
                get
                {
                    return CustomIcon ?? $"iad.icon.{Id}";
                }
            }

            [JsonIgnore]
            public StatusConditionConfig Config
            {
                get
                {
                    if (_config == null)
                    {
                        StatusConditionConfig config;
                        if (PLUGIN.config.StatusConditions.TryGetValue(Id, out config))
                        {
                            _config = config;
                        }
                    }
                    return _config;
                }
                set
                {
                    _config = value;
                }
            }

            [JsonIgnore]
            public bool IsEnabled
            {
                get
                {
                    return Config.Enabled;
                }
            }

            [JsonIgnore]
            public bool HasInterval
            {
                get
                {
                    return IsCustom ? (MinIntervalSeconds.HasValue && MaxIntervalSeconds.HasValue) : (Config.MinIntervalSeconds.HasValue && Config.MaxIntervalSeconds.HasValue);
                }
            }

            public string LangName => Id;

            public string LangReveal => $"{Id} reveal";

            public string LangDiagnosis => $"{Id} diagnosis";

            public string LangCured => $"{Id} cured";

            public int? GetRandomDurationSeconds()
            {
                if (!MinDurationSeconds.HasValue || !MaxDurationSeconds.HasValue) { return null; }
                return UnityEngine.Random.Range(MinDurationSeconds.Value, MaxDurationSeconds.Value);
            }

            public int? GetRandomIntervalSeconds()
            {
                return HasInterval ? (int?)UnityEngine.Random.Range(MinIntervalSeconds.Value, MaxIntervalSeconds.Value) : null;
            }

            public void DoIntervalEffect(BasePlayer basePlayer)
            {
                if (IntervalEffect != null)
                {
                    IntervalEffect.Invoke(basePlayer);
                }
            }

            public bool MatchesInflictionAction(bool isMelee)
            {
                return InflictionAction == null || InflictionAction.Value == InflictionDamageAction.any || (isMelee && InflictionAction.Value == InflictionDamageAction.melee) || (!isMelee && InflictionAction.Value == InflictionDamageAction.ranged);
            }

            public StatusCondition(string id, Action<BasePlayer> beginEffect = null, Action<BasePlayer> intervalEffect = null)
            {
                Id = id;
                BeginEffect = beginEffect;
                IntervalEffect = intervalEffect;
            }

            public void LoadConfigValues()
            {
                if (!PLUGIN.config.StatusConditions.ContainsKey(Id))
                {
                    return;
                }
                var Config = PLUGIN.config.StatusConditions[Id];
                if (Config.MinIntervalSeconds.HasValue && Config.MaxIntervalSeconds.HasValue)
                {
                    MinIntervalSeconds = Config.MinIntervalSeconds.Value;
                    MaxIntervalSeconds = Config.MaxIntervalSeconds.Value;
                }
                MinDurationSeconds = Config.MinDurationSeconds;
                MaxDurationSeconds = Config.MaxDurationSeconds;
                this.Config = Config;
                var defaultConfig = PremadeStatusConditions.DefaultConfigs[Id];
                BeginEffect = defaultConfig.BeginEffect;
                IntervalEffect = defaultConfig.IntervalEffect;
                InflictionAction = Config.InflictionDamageAction;
            }

            public override string ToString()
            {
                return Id;
            }
        }
    }

}

namespace Oxide.Plugins
{
    public partial class InjuriesAndDiseases
    {
        public string CUI_OVERLAY = "iad.overlay";

        public readonly static Dictionary<string, string> AssetPaths = new Dictionary<string, string>();

        public static void AddImages(Dictionary<string, string> idAndUrls, Action callback = null)
        {
            var toAdd = new Dictionary<string, string>();
            foreach(var iau in idAndUrls)
            {
                if (iau.Value.IsAssetPath())
                {
                    AssetPaths[iau.Key] = iau.Value;
                }
                else
                {
                    toAdd[iau.Key] = iau.Value;
                }
            }
            PLUGIN.ImageLibrary.Call("ImportImageList", PLUGIN.Name, toAdd, 0UL, true, callback);
        }

        public static string ImageNameOrAssetPath(string name)
        {
            if (AssetPaths.ContainsKey(name)) { return AssetPaths[name]; }
            return name;
        }

        public static string GetImage(string id)
        {
            return PLUGIN.ImageLibrary?.Call<string>("GetImage", id);
        }

        private T GetObjectRaycast<T>(BasePlayer basePlayer, float distance) where T : BaseEntity
        {
            Ray ray = new Ray(basePlayer.eyes.position, basePlayer.eyes.HeadForward());
            RaycastHit hit;
            var entity = !Physics.Raycast(ray, out hit, distance) ? null : hit.GetEntity();
            if (entity == null || !(entity is T))
            {
                return null;
            }
            return (T)entity;
        }

        public static bool IsBotRespawnEntity(ulong entityId) => (PLUGIN?.BotReSpawn?.IsLoaded ?? false) && (bool)PLUGIN.BotReSpawn.CallHook("IsBotReSpawn", entityId);

        public static bool IsZombieHordeEntity(BaseEntity entity) => entity.Categorize() == "Zombie";

        public static void PlayEffect(BasePlayer player, string effectString, bool local)
        {
            var effect = new Effect(effectString, player, 0, Vector3.zero, Vector3.forward);
            if (local)
                EffectNetwork.Send(effect, player.net.connection);
            else
                EffectNetwork.Send(effect);
        }

        public static void ShowOverlayEffect(BasePlayer basePlayer, string id, string color, string imageLibraryNameOrAssetPath, string material, float duration, float fadeIn, float fadeOut, CuiRectTransformComponent transform = null)
        {
            if (basePlayer == null) { return; }
            CuiElementContainer container = new CuiElementContainer();
            var image = new CuiImageComponent
            {
                Color = color,
                FadeIn = fadeIn
            };
            if (transform == null) { transform = new CuiRectTransformComponent(); }
            if (imageLibraryNameOrAssetPath.IsAssetPath()) { image.Sprite = imageLibraryNameOrAssetPath; }
            else { image.Png = GetImage(imageLibraryNameOrAssetPath); }
            if (!string.IsNullOrWhiteSpace(material)) { image.Material = material; }
            var element = new CuiElement
            {
                Parent = "Hud",
                Name = id,
                FadeOut = fadeOut,
                Components = { image, transform }
            };
            container.Add(element);
            CuiHelper.DestroyUi(basePlayer, id);
            CuiHelper.AddUi(basePlayer, container);
            PLUGIN.timer.In(duration, () =>
            {
                if (basePlayer == null)
                {
                    return;
                }
                CuiHelper.DestroyUi(basePlayer, id);
            });
        }

        public static void Message(BasePlayer basePlayer, string lang, params object[] args)
        {
            if (PLUGIN.config.MessagesEnabled)
            {
                ConsoleNetwork.SendClientCommand(basePlayer.Connection, "chat.add", 2, PLUGIN.config.MessagesIconID, Lang(lang, basePlayer, args));
            }
        }

        public static void Message(ulong userId, string lang, params object[] args)
        {
            var target = FindPlayer(userId);
            if (target == null) { return; }
            var basePlayer = target.Object as BasePlayer;
            if (basePlayer == null) { return; }
            Message(basePlayer, lang, args);
        }

        public static IPlayer FindPlayer(string userNameOrId)
        {
            return PLUGIN.covalence.Players.FindPlayer(userNameOrId);
        }

        public static IPlayer FindPlayer(ulong userId)
        {
            return PLUGIN.covalence.Players.FindPlayerById(userId.ToString());
        }

        public static List<IPlayer> FindPlayers(IEnumerable<ulong> userIds)
        {
            return userIds.Select(x => FindPlayer(x)).Where(x => x != null).ToList();
        }

        public T LoadDataFile<T>(string fileName)
        {
            try
            {
                return Interface.Oxide.DataFileSystem.ReadObject<T>($"{Name}/{fileName}");
            }
            catch (Exception e)
            {
                throw e;
                return default(T);
            }
        }

        public void SaveDataFile<T>(string fileName, T data)
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/{fileName}", data);
        }

        public class NullSafeDictionary<K, V> : Dictionary<K, V> where V : new()
        {
            new public V this[K key]
            {
                get
                {
                    try
                    {
                        return base[key];
                    }
                    catch
                    {
                        base[key] = new V();
                        return base[key];
                    }
                }
                set
                {
                    base[key] = value;
                }
            }
        }

        public void Debug(string message, bool warning = false)
        {
            if (!Debugging) { return; }
            if (warning)
            {
                PrintWarning(message);
            }
            else
            {
                Puts(message);
            }
        }

        public double ProbAtleastOne(int rolls, float chance)
        {
            var pnone = Math.Pow((1f-chance), rolls);
            return 1f - pnone;
        }

        public bool RollSucceeds(float chance)
        {
            if (chance <= 0) { return false; }
            var roll = UnityEngine.Random.Range(0f, 1f);
            return chance >= roll;
        }
    }
}

namespace Oxide.Plugins
{
    public partial class InjuriesAndDiseases
    {
        public class ZombieLootInfo
        {
            public ZombieLootInfo(BasePlayer basePlayer)
            {
                PlayerDisplayName = basePlayer?.displayName;
                Items = new List<Item>();
            }
            public string PlayerDisplayName { get; set; }
            public List<Item> Items { get; set; }
        }
    }
}
