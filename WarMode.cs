
using static Oxide.Plugins.WarMode;
using System.IO;
using Oxide.Core.Plugins;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using Oxide.Plugins.WarModeMethods;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System;
using Oxide.Game.Rust.Cui;
using System.Data;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("WarMode", "mr01sam", "1.3.1")]
    [Description("Flag players as PvP or PvE.")]
    public partial class WarMode : CovalencePlugin
    {
        [PluginReference]
        private readonly Plugin SimpleStatus, AdvancedStatus, ZoneManager, AbandonedBases, RaidableBases, WarModeAdminPanel, Toastify, BetterChat, NoEscape;

        internal const string PermissionFlag = "warmode.flag";
        internal const string PermissionAdmin = "warmode.admin";
        internal const string PermissionBypass = "warmode.bypass";

        public static bool DEBUGGING => INSTANCE.config.Settings.ShowDebugMessagesInConsole;
        public static void Debug(string message, Func<bool> condition = null)
        {
            if (DEBUGGING && (condition?.Invoke() ?? true)) { INSTANCE.Puts($"[DEBUG]: {message}"); }
        }

        public static void DebugAction(string action, BaseEntity initiator, BaseEntity target, HitInfo info = null)
        {
            if (!(initiator is BasePlayer)) { return; }
            var basePlayer = initiator as BasePlayer;
            Debug($"{basePlayer.displayName} {action} {initiator.GetTargetType()}({initiator.GetMode().Name()}) -> {target.GetTargetType()}({target.GetMode().Name()})");
        }

        public static WarMode INSTANCE;

        void Init()
        {
            INSTANCE = this;
            UnsubscribeAll(
                nameof(OnEntityTakeDamage),
                nameof(CanLootPlayer),
                nameof(CanLootEntity),
                nameof(OnItemPickup),
                nameof(OnLootEntity),
                nameof(OnPlayerSleepEnded),
                nameof(OnPlayerDisconnected),
                nameof(OnUserConnected),
                nameof(OnPlayerInput),
                nameof(OnUserGroupAdded),
                nameof(OnTeamInvite),
                nameof(OnTeamUpdate),
                nameof(CanBeTargeted),
                nameof(OnSamSiteTarget),
                nameof(OnTrapTrigger),
                nameof(OnRackedWeaponMount),
                nameof(OnRackedWeaponUnload),
                nameof(OnRackedWeaponLoad),
                nameof(OnRackedWeaponSwap),
                nameof(OnRackedWeaponTake),
                nameof(OnGrowableGather),
                nameof(CanTakeCutting),
                nameof(CanAdministerVending),
                nameof(CanMountEntity),
                nameof(CanRenameBed)

            );
        }

        private List<string> _subscriptions = new List<string>();

        public void UnsubscribeAll(params string[] methods)
        {
            _subscriptions.AddRange(methods);
            foreach(var method in _subscriptions) { Unsubscribe(method); }
        }

        public void SubscribeAll()
        {
            foreach (var method in _subscriptions)
            {
                switch(method)
                {
                    //case nameof(CanMountEntity):
                    //    if (CONFIG.Settings.AllowVehicleModeOwnership) { Subscribe(method); }
                    //    break;
                    default:
                        Subscribe(method);
                        break;
                }
            }
        }

        void OnServerInitialized()
        {
            RevealConfigOptions();

            LoadData();
            ValidateModes();

            // Add Default Modes
            InitModesFromConfig();

            // Localization
            LoadDefaultMessages();

            // Permissions
            permission.RegisterPermission(PermissionFlag, this);
            permission.RegisterPermission(PermissionBypass, this);

            // Commands
            if (!string.IsNullOrWhiteSpace(config.Flagging.ChatCommand))
            {
                AddCovalenceCommand(config.Flagging.ChatCommand, nameof(CmdFlagPvp), PermissionFlag);
            }

            // SimpleStatus
            if (SimpleStatus.IsLoaded())
            {
                foreach(var kvp in config.SimpleStatus.ModeStatusBars)
                {
                    var statusBar = kvp.Value;
                    if (statusBar.Show)
                    {
                        SimpleStatus.CallHook("CreateStatus",
                        this,
                        $"warmode.{kvp.Key}",
                        statusBar.BackgroundColor,
                        $"ss title {kvp.Key}",
                        statusBar.TitleColor,
                        $"ss text {kvp.Key}",
                        statusBar.TextColor,
                        statusBar.Image,
                        statusBar.ImageColor);
                    }
                }
                foreach (var basePlayer in BasePlayer.activePlayerList)
                {
                    UpdateStatusBar(basePlayer.UserIDString);
                }
            }

            // BetterChat
            if (BetterChat.IsLoaded())
            {
                BetterChat_AddTitles();
            }

            // Subscribe All
            SubscribeAll();

            // Init Players
            foreach (var player in covalence.Players.All)
            {
                OnUserConnected(player);
            }
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                OnPlayerSleepEnded(basePlayer);
            }
            
        }

        void Unload()
        {
            SaveData();

            Marker.ClearAllBehaviors();

            // Unbsubscribe
            Unsubscribe(nameof(OnPlayerInput));
            Unsubscribe(nameof(OnEntityTakeDamage));
            Unsubscribe(nameof(CanLootPlayer));
            Unsubscribe(nameof(CanLootEntity));
            

            if (SimpleStatus.IsLoaded())
            {
                foreach(var basePlayer in BasePlayer.activePlayerList)
                {
                    SimpleStatus.CallHook("SetStatus", basePlayer.UserIDString, "warmode.pve", 0);
                    SimpleStatus.CallHook("SetStatus", basePlayer.UserIDString, "warmode.pvp", 0);
                }
            }
        }

        void OnNewSave(string filename)
        {
            Data = new PluginData();
        }

        private Dictionary<ulong, ulong> cachedPrivs = new Dictionary<ulong, ulong>();

        public static BaseNetworkable FindEntity(ulong entityId)
        {
            return BaseNetworkable.serverEntities.Find(new NetworkableId(entityId));
        }

        public BuildingPrivlidge GetPriv(BaseEntity entity)
        {
            if (cachedPrivs.ContainsKey(entity.net.ID.Value))
            {
                var eid = cachedPrivs[entity.net.ID.Value];
                return FindEntity(eid) as BuildingPrivlidge;
            }
            var priv = entity.GetBuildingPrivilege();
            if (priv == null) { return null; }
            cachedPrivs[entity.net.ID.Value] = priv.net.ID.Value;
            return priv;
        }

        string json = null;
        string GetMarkerJson()
        {
            if (json != null) { return json; }
            var container = new CuiElementContainer();
            var size = config.Marker.Size;
            var up = 0;
            container.Add(new CuiElement
            {
                Name = "warmode.marker",
                Parent = "Hud",
                FadeOut = 0.1f,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = $"{-size} {-size+up}",
                        OffsetMax = $"{size} {size+up}"
                    },
                    new CuiImageComponent
                    {
                        FadeIn = 0.1f,
                        Sprite = config.Marker.Image,
                        Color = config.Marker.Color,
                    }
                }
            });
            json = container.ToJson();
            return json;
        }

        private static T FindObject<T>(BasePlayer basePlayer, float distance) where T : BaseEntity
        {
            Ray ray = new Ray(basePlayer.eyes.position, basePlayer.eyes.HeadForward());
            RaycastHit hit;
            return !Physics.Raycast(ray, out hit, distance) ? null : hit.GetEntity() as T;
        }

        // key is compound key of userid|messageid
        private Dictionary<string, float> NotifyDelays = new Dictionary<string, float>();
        public void Notify(Plugin plugin, BasePlayer basePlayer, string loc, bool positive, float cooldown, params object[] args)
        {
            var userid = basePlayer.UserIDString;
            var key = CompoundKey(userid, loc);
            if (cooldown > 0 && NotifyDelays.GetValueOrDefault(key) > UnityEngine.Time.realtimeSinceStartup) { return; }
            var localizedText = Lang(loc, userid, args);
            var overrule = (bool?) CallHook("WarMode_NotificationSent", userid, loc, localizedText);
            if (overrule != null) { return; }
            // Chat
            if (config.Notifications.Chat)
            {
                var chatText = localizedText;
                if (config.Chat.ShowPluginPrefix)
                {
                    chatText = "<color=#aaee32>[WarMode]</color> " + localizedText;
                }
                ConsoleNetwork.SendClientCommand(basePlayer.Connection, "chat.add", 2, config.Chat.SteamIdForIcon, chatText);
                if (!positive && !string.IsNullOrWhiteSpace(config.Chat.SfxFail))
                {
                    EffectNetwork.Send(new Effect(config.Chat.SfxFail, basePlayer, 0, Vector3.zero, Vector3.forward), basePlayer.net.connection);
                }
                else if (positive && !string.IsNullOrWhiteSpace(config.Chat.SfxSuccess))
                {
                    EffectNetwork.Send(new Effect(config.Chat.SfxSuccess, basePlayer, 0, Vector3.zero, Vector3.forward), basePlayer.net.connection);
                }
            }
            // Toastify
            if (config.Notifications.Toastify && Toastify.IsLoaded())
            {
                Toastify?.CallHook("SendToast", basePlayer, positive ? config.Toastify.PositiveToastID : config.Toastify.NegativeToastID, null, localizedText, config.Toastify.Duration);
            }
            if (cooldown > 0)
            {
                NotifyDelays[key] = UnityEngine.Time.realtimeSinceStartup + cooldown;
            }
        }

        private HashSet<ulong> teamStartedSync = new HashSet<ulong>();
        public void UpdateStatusBar(string userid)
        {
            if (!SimpleStatus.IsLoaded()) { return; }
            // SimpleStatus
            var modeName = userid.GetMode()?.Name;
            foreach (var mode in Modes.Values)
            {
                if (mode.Name == modeName) { continue; }
                SimpleStatus.CallHook("SetStatus", userid, $"warmode.{mode.Name}", 0);
            }
            if (AdvancedStatus?.IsLoaded ?? false)
            {
                timer.In(1f, () =>
                {
                    SimpleStatus.CallHook("SetStatus", userid, $"warmode.{modeName}", int.MaxValue);
                });
            }
            else
            {
                SimpleStatus.CallHook("SetStatus", userid, $"warmode.{modeName}", int.MaxValue);
            }
        }
        private static string CompoundKey(object obj1, object obj2) => $"{obj1}|{obj2}";
    }
}

namespace Oxide.Plugins
{
    public partial class WarMode
    {
        // The following are hooks that other plugins can subscribe to

        /*
        
        // Called when a player's mode has been updated or config changes have ocurred that may affect the mode.
        private void WarMode_PlayerModeUpdated(string userid, string modeId)
        {

        }

        */
    }
}

namespace Oxide.Plugins
{
    public partial class WarMode
    {
        public void BetterChat_AddTitles()
        {
            BetterChat?.CallHook("API_RegisterThirdPartyTitle", this, new Func<IPlayer, string>(GetBetterChatModeTitle));
        }

        public string GetBetterChatModeTitle(IPlayer player)
        {
            if (player == null) { return null; }
            var mode = player.GetMode();
            if (mode == null) { return null; }
            if (!config.BetterChat.Modes.ContainsKey(mode.Name()) || !config.BetterChat.Modes[mode.Name()].ShowModeTitleInChat) { return null; }
            return string.Format(config.BetterChat.Modes[mode.Name()].ModeTitleFormat, mode.Title(player.Id, colored: false));
        }
    }
}

namespace Oxide.Plugins
{
    public partial class WarMode
    {
        /* This file is intentionally blank to use as a template */
    }
}

namespace Oxide.Plugins
{
    public partial class WarMode
    {
        [Command("warmode"), Permission(PermissionAdmin)]
        private void CmdWarmode(IPlayer player, string command, string[] args)
        {
            var commands = new[]
            {
                new
                {
                    Enabled = WarModeAdminPanel?.IsLoaded ?? false,
                    Commands = new string[]  { "warmode.config", "wmc" },
                    Args = new string[] { },
                    Description = "Opens a UI panel that allows you to edit modes in game."
                },
                new
                {
                    Enabled = true,
                    Commands = new string[]  { "warmode.setplayer" },
                    Args = new string[] { "player", "mode" },
                    Description = "Sets the mode for the given player."
                },
                new
                {
                    Enabled = true,
                    Commands = new string[]  { "warmode.setall" },
                    Args = new string[] { "mode" },
                    Description = "Sets the mode for all players on the server."
                },
                new
                {
                    Enabled = true,
                    Commands = new string[]  { "warmode.mode" },
                    Args = new string[] { "player?" },
                    Description = "Display the mode for the specified player name OR your own mode if left blank."
                },
                new
                {
                    Enabled = true,
                    Commands = new string[]  { "warmode.tc" },
                    Args = new string[] { },
                    Description = "Display the mode for the base you are standing in. Will display nothing if not within a base."
                }
            };

            var str = "";
            str += Size("[ " + Color("WarMode Admin Commands", "#55e076") + " ]", 16) + "\n";
            foreach(var cmd in commands.Where(x => x.Enabled))
            {
                foreach(var cmd2 in cmd.Commands)
                {
                    str += $"\n/{Color(cmd2, "#f7ce83")} {(string.Join(" ", cmd.Args.Select(x => Color("<" + x + ">", "#83f7f5"))))}";
                }
                str += "\n" + Size(cmd.Description, 10) + "\n";
            }
            player.Message(Size(str, 13));
        }

        [Command("warmode.setplayer"), Permission(PermissionAdmin)]
        private void CmdSetPlayer(IPlayer player, string command, string[] args)
        {
            // warmode.setplayer <player> <pve|pvp>
            try
            {
                var userIdOrString = args[0];
                var mode = args[1].ToLower();
                var target = covalence.Players.FindPlayer(userIdOrString);
                if (target == null)
                {
                    player.Message($"No player found with ID or name matching '{userIdOrString}'");
                    return;
                }
                if (GetModeGroupByTitle(mode) == null)
                {
                    player.Message($"No mode named '{mode}' exists, valid modes are {Modes.Select(x => x.Value.Name).ToSentence()}");
                    return;
                }
                SetModeByName(target.Id, mode);
                player.Message($"Set MODE={mode} for {target.Name}");
            } catch
            {
                player.Message("Usage: warmode.setplayer <player> <pve|pvp>");
            }
        }

        [Command("warmode.setall"), Permission(PermissionAdmin)]
        private void CmdSetAll(IPlayer player, string command, string[] args)
        {
            // warmode.setall <pve|pvp>
            try
            {
                var i = 0;
                var mode = args[0].ToLower();
                if (GetModeGroupByTitle(mode) == null)
                {
                    player.Message($"No mode named '{mode}' exists, valid modes are {Modes.Select(x => x.Value.Name).ToSentence()}'");
                    return;
                }
                foreach (var basePlayer in covalence.Players.All)
                {
                    SetModeByName(basePlayer.Id, mode);
                    i++;
                }
                player.Message($"Set MODE={mode} for {i} players");
            }
            catch
            {
                player.Message("Usage: warmode.setall <pve|pvp>");
            }
        }

        [Command("warmode.getmode"), Permission(PermissionAdmin)]
        private void CmdGetMode(IPlayer player, string command, string[] args)
        {
            // warmode.getmode
            try
            {
                BasePlayer basePlayer = player.Object as BasePlayer;
                Ray ray = new Ray(basePlayer.eyes.position, basePlayer.eyes.HeadForward());
                BaseEntity target = FindObject(ray, 4f);
                if (target == null)
                {
                    player.Message("You must be looking at an entity");
                    return;
                } 
                ModeConfig mode = null;
                if (target is BasePlayer targetBasePlayer)
                {
                    mode = targetBasePlayer.GetMode();
                }
                else if (target is BaseVehicle targetVehicle)
                {
                    mode = targetVehicle.GetMode();
                }
                var message = $"Mode of that {target.GetType()} is {mode?.Name.ToUpper() ?? "NONE"}";
                var privInfo = target.GetPrivInfo();
                if (!privInfo.NotExists)
                {
                    message += $" but is within base with a mode of {privInfo.GetMode()?.Name.ToUpper() ?? "NONE"}";
                }
                player.Message(message);
            }
            catch
            {
                player.Message("Usage: warmode.getmode");
            }
        }
    }
}

namespace Oxide.Plugins
{
    public partial class WarMode : CovalencePlugin
    {
        public static Configuration CONFIG => INSTANCE.config;
        public Configuration config;
        public class Configuration
        {
            [ConfigDescription("")]
            public SettingsConfig Settings = new SettingsConfig();

            [ConfigDescription("")]
            public FlaggingConfig Flagging = new FlaggingConfig();

            [ConfigDescription("")]
            public MarkerConfig Marker = new MarkerConfig();

            [ConfigDescription("")]
            public NotificationsConfig Notifications = new NotificationsConfig();

            [ConfigDescription("")]
            public ChatConfig Chat = new ChatConfig();

            [ConfigDescription("")]
            public TeamsConfig Teams = new TeamsConfig();

            // ADD PLUGIN INTEGRATION

            [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            [ConfigDescription("")]
            public SimpleStatusConfig SimpleStatus = null;

            [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            [ConfigDescription("")]
            public ZoneManagerConfig ZoneManager = null;

            [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            [ConfigDescription("")]
            public RaidableBasesConfig RaidableBases = null;

            [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            [ConfigDescription("")]
            public BetterChatConfig BetterChat = null;

            [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            [ConfigDescription("")]
            public NoEscapeConfig NoEscape = null;

            [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            [ConfigDescription("")]
            public ToastifyConfig Toastify = null;

            [ConfigDescription("")]
            public ModeConfig[] Modes = new ModeConfig[]
{
                new ModeConfig
                {
                    Priority = 1,
                    Name = "pvp",
                    DisplayName = "PVP",
                    Group = "warmodepvp",
                    ColorHex = "#e6573a",
                    CanAttackTypes = TargetTypesForCategory.Attacking.ToDictionary(ttype => ttype, value =>
                    {
                        return new string[] { "pvp", "npc" };
                    }),
                    CanLootTypes = TargetTypesForCategory.Looting.ToDictionary(ttype => ttype, value =>
                    {
                        return new string[] { "pvp", "npc" };
                    }),
                    CanTargetTypes = TargetTypesForCategory.Targeting.ToDictionary(ttype => ttype, value =>
                    {
                        return new string[] { "pvp", "npc" };
                    }),
                    CanMountTypes = TargetTypesForCategory.Mounting.ToDictionary(ttype => ttype, value =>
                    {
                        return new string[] { "pvp", "npc" };
                    }),
                    ShowMarkerWhenAimedAt = false
                },
                new ModeConfig
                {
                    Priority = 2,
                    Name = "pve",
                    DisplayName = "PVE",
                    Group = "warmodepve",
                    ColorHex = "#8bda00",
                    CanAttackTypes = TargetTypesForCategory.Attacking.ToDictionary(ttype => ttype, value =>
                    {
                        return new string[] { "npc" };
                    }),
                    CanLootTypes = TargetTypesForCategory.Looting.ToDictionary(ttype => ttype, value =>
                    {
                        return new string[] { "npc" };
                    }),
                    CanTargetTypes = TargetTypesForCategory.Targeting.ToDictionary(ttype => ttype, value =>
                    {
                        return new string[] { "npc" };
                    }),
                    CanMountTypes = TargetTypesForCategory.Mounting.ToDictionary(ttype => ttype, value =>
                    {
                        return new string[] { "npc" };
                    }),
                    ShowMarkerWhenAimedAt = true
                },
                new ModeConfig
                {
                    Name = "npc",
                    DisplayName = "NPC",
                    ColorHex = "#47a3e3",
                    CanAttackTypes = TargetTypesForCategory.Attacking.ToDictionary(ttype => ttype, value =>
                    {
                        return new string[] { "pve", "pvp", "npc" };
                    }),
                    CanLootTypes = TargetTypesForCategory.Looting.ToDictionary(ttype => ttype, value =>
                    {
                        return new string[] { "pve", "pvp", "npc" };
                    }),
                    CanTargetTypes = TargetTypesForCategory.Targeting.ToDictionary(ttype => ttype, value =>
                    {
                        return new string[] { "pve", "pvp", "npc" };
                    }),
                    CanMountTypes = TargetTypesForCategory.Mounting.ToDictionary(ttype => ttype, value =>
                    {
                        return new string[] { "pve", "pvp", "npc" };
                    }),
                    ShowMarkerWhenAimedAt = false
                }
};

            public Core.VersionNumber Version { get; set; } = new Core.VersionNumber(0, 0, 0);
        }

        #region Simple Status
        public class SimpleStatusConfig
        {
            [ConfigDescription("")]
            public Dictionary<string, StatusDetailsConfig> ModeStatusBars;
        }

        public class StatusDetailsConfig
        {
            public bool Show;
            public string BackgroundColor;
            [ConfigDescription("Icon that will display, currently only Sprites are supported")]
            public string Image;
            public string ImageColor;
            public string TitleColor;
            public string TextColor;
        }
        #endregion

        #region BetterChat
        public class BetterChatConfig
        {
            [ConfigDescription("If true, then the current mode of the player will appear as a title in the better chat window.")]
            public bool ShowModePrefixInChat = true;

            [ConfigDescription("")]
            public Dictionary<string, BetterChatModeConfig> Modes;
        }

        public class BetterChatModeConfig
        {

            [ConfigDescription("If true, then this mode will appear as a title in chat if a player belongs to it.")]
            public bool ShowModeTitleInChat = true;

            [ConfigDescription("The string format of how the mode title will appear before the player's name. The {0} will be replaced with the mode display name. Emojis are also supported.")]
            public string ModeTitleFormat = "[{0}]";
        }
        #endregion

        public class TeamsConfig
        {
            [ConfigDescription("If true, then when a player joins a team, the modes of all the members will be synced to match the shared mode with the LOWEST priority.")]
            public bool SyncModeWithTeamMembers = true;
        }

        public class ChatConfig
        {
            [ConfigDescription("If true, then [WarMode] will appear in front of chat messages.")]
            public bool ShowPluginPrefix = true;

            [ConfigDescription("Steam id for the icon that will show for the chat messages.")]
            public ulong SteamIdForIcon = 0;

            [ConfigDescription("Sfx that is played when a negative chat message is shown. Other effects can be found <a href=\"https://github.com/OrangeWulf/Rust-Docs/blob/master/Extended/Effects.mdt\">here</a>")]
            public string SfxFail = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab";

            [ConfigDescription("Sfx that is played when a positive chat message is shown. Other effects can be found <a href=\"https://github.com/OrangeWulf/Rust-Docs/blob/master/Extended/Effects.mdt\">here</a>")]
            public string SfxSuccess = "assets/prefabs/locks/keypad/effects/lock.code.updated.prefab";
        }

        public class MarkerConfig
        {
            [ConfigDescription("If false, then the marker icons that are shown when a player aims their weapon at another player will be disabled for all modes.")]
            public bool Show = true;

            [ConfigDescription("Distance that the marker will start appearing.")]
            public float Distance = 100f;

            [ConfigDescription("The time in seconds that the marker will refresh, lower values will cause it to update more frequently, but may impact server performance.")]
            public float UpdateInterval = 0.5f;

            [ConfigDescription("The sprite asset path for the indicator icon. Other valid sprites can be found <a href=\"https://github.com/OrangeWulf/Rust-Docs/blob/master/Extended/UI.md\">here</a>")]
            public string Image = "assets/icons/peace.png";
            public float Size = 16;
            public string Color = "0 1 0 0.5";
        }

        public class FlaggingConfig
        {
            [ConfigDescription("If true, then the user must be in a safe zone to use the flag command.")]
            public bool RequiresSafeZone = true;
            [ConfigDescription("If true, then the user must not be marked as hostile to use the flag command.")]
            public bool RequiresNoHostile = true;
            [ConfigDescription("If true, then the use must be a leader of their team to use the flag command. If they are not in a team, this restriction will be ignored.")]
            public bool RequiresTeamLeader = false;
            public int CooldownSeconds = 60;
            public string ChatCommand = "flag";
            [ConfigDescription("These are the modes that are available for players to choose from when using the flag command. They can either specify the mode they want to switch to, or it will rotate through them if no mode is specified.")]
            public string[] ModeOptions = new string[]
            {
                "pve",
                "pvp"
            };
        }

        public class NotificationsConfig
        {
            public bool Chat = true;
            public bool Toastify = false;
        }

        public class ModeConfig
        {
            public static ModeConfig New()
            {
                var newMode = new ModeConfig();

                // EDIT HERE

                newMode.CanAttackTypes = TargetTypesForCategory.Attacking.ToDictionary(ttype => ttype, value => Array.Empty<string>());
                newMode.CanLootTypes = TargetTypesForCategory.Looting.ToDictionary(ttype => ttype, value => Array.Empty<string>());
                newMode.CanTargetTypes = TargetTypesForCategory.Targeting.ToDictionary(ttype => ttype, value => Array.Empty<string>());
                newMode.CanMountTypes = TargetTypesForCategory.Mounting.ToDictionary(ttype => ttype, value => Array.Empty<string>());
                newMode.CanEnterTypes = TargetTypesForCategory.Entering.ToDictionary(ttype => ttype, value => Array.Empty<string>());

                newMode.Name = "mymode";
                newMode.DisplayName = "My Mode";
                newMode.ShowMarkerWhenAimedAt = false;
                newMode.Group = "warmodemymode";

                return newMode;
            }

            // WarMode
            [ConfigDescription("When multiple modes are considered, the mode with the LOWEST priority will be applied. For example, in the case where a team has a mixture of ModeA and ModeB, the mode with the LOWEST priority will be used.")]
            [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public int? Priority = null;

            [ConfigDescription("The oxide permission group associated with this mode. Leave as NULL if this mode is intended only for NPCs. If specified, you MUST provided a Priority as well.")]
            [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public string Group = null;

            [ConfigDescription("Unique name of the mode. This is NOT the display name.")]
            public string Name = "";

            [ConfigDescription("The name of the mode that is displayed to players. This is not a localized value.")]
            public string DisplayName = null;

            [ConfigDescription("The color associated with this mode, it will appear this color when mentioned in messages or the UI.")]
            public string ColorHex = null;

            [JsonIgnore]
            public string ColorRgb
            {
                get
                {
                    var color = UnityEngine.Color.white;
                    ColorUtility.TryParseHtmlString(ColorHex, out color);
                    return color.ToColorString();
                }
            }

            [ConfigDescription("If true then a marker icon will be displayed on the reticle when another player aims their weapon at this player.")]
            public bool ShowMarkerWhenAimedAt = true;

            // LEGACY CONFIG - This needs to stay to convert old configs
            [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public string[] CanRaid = null;
            [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public string[] CanAttack = null;
            [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public string[] CanLoot = null;
            [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public string[] CanTargetWithTraps = null;
            [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public string[] CanEnterOwnedMonuments = null;
            public bool CanEnterOwnedMonumentMode(ModeConfig otherMode) => otherMode == null ? true : CanEnterOwnedMonuments.Contains(otherMode.Name);

            // ATTACKING
            public Dictionary<TargetType, string[]> CanAttackTypes = TargetTypesForCategory.Attacking.ToDictionary(ttype => ttype, value => Array.Empty<string>());
            public bool CanAttackMode(ModeConfig otherMode, TargetType ttype) => otherMode == null ? true : CanAttackTypes.GetValueOrDefault(ttype, Array.Empty<string>()).Contains(otherMode.Name);

            // LOOTING
            public Dictionary<TargetType, string[]> CanLootTypes = TargetTypesForCategory.Looting.ToDictionary(ttype => ttype, value => Array.Empty<string>());
            public bool CanLootMode(ModeConfig otherMode, TargetType ttype) => otherMode == null ? true : CanLootTypes.GetValueOrDefault(ttype, Array.Empty<string>()).Contains(otherMode.Name);

            // TARGETING
            public Dictionary<TargetType, string[]> CanTargetTypes = TargetTypesForCategory.Targeting.ToDictionary(ttype => ttype, value => Array.Empty<string>());
            public bool CanTargetMode(ModeConfig otherMode, TargetType ttype) => otherMode == null ? true : CanTargetTypes.GetValueOrDefault(ttype, Array.Empty<string>()).Contains(otherMode.Name);

            // MOUNTING
            public Dictionary<TargetType, string[]> CanMountTypes = TargetTypesForCategory.Mounting.ToDictionary(ttype => ttype, value => Array.Empty<string>());
            public bool CanMountMode(ModeConfig otherMode, TargetType ttype) => otherMode == null ? true : CanMountTypes.GetValueOrDefault(ttype, Array.Empty<string>()).Contains(otherMode.Name);

            // MONUMENT OWNER
            public Dictionary<TargetType, string[]> CanEnterTypes = new Dictionary<TargetType, string[]>();


            // EDIT HERE

            // RaidableBases
            [Newtonsoft.Json.JsonIgnore]
            public bool CanEnterPvpRaidableBases => CONFIG.RaidableBases?.CanEnterPvpRaidableBases.Contains(Name) ?? false;
            [Newtonsoft.Json.JsonIgnore]
            public bool CanEnterPveRaidableBases => CONFIG.RaidableBases?.CanEnterPveRaidableBases.Contains(Name) ?? false;
        }

        // These are for use in the AdminPanel creator, they do not get saved
        public class AdminPanelConfig
        {
            public string DisplayName { get; set; } = null;
        }

        public class ZoneManagerConfig
        {
            [ConfigDescription("If using ZoneManager you can specify zones that will temporarily force players into a certain mode when entered. They key must be in the format of <property><operator><value> where property can be 'name' or 'id', operator can be '=' or '~' and value would be the zone id or zone name respectively. The '=' operator will do an exact match, the '~' will do a partial match.\n\nFor example 'id=abc123'='pve' will make any zone with the exact id of 'abc123' a pve zone. While 'name~123'='pvp' will make any zone that contains '123' in their name a pvp zone.")]
            public Dictionary<string, string> ForceModeInZone = new Dictionary<string, string>();
        }

        public class RaidableBasesConfig
        {
            [ConfigDescription("This needs to match the protection radius property in the Raidable Bases config.")]
            public float ProtectionRadius { get; set; } = 50f;

            [ConfigDescription("A list of modes that can enter raidable bases marked as PVP.")]
            public string[] CanEnterPvpRaidableBases = new string[]
            {
                "pvp"
            };

            [ConfigDescription("A list of modes that can enter raidable bases marked as PVE.")]
            public string[] CanEnterPveRaidableBases = new string[]
            {
                "pve",
                "pvp"
            };
        }

        public class ToastifyConfig
        {

            [ConfigDescription("Toast ID of the toast used for positive messages. Should align with your Toastify config values.")]
            public string PositiveToastID = "success";

            [ConfigDescription("Toast ID of the toast used for negative messages. Should align with your Toastify config values.")]
            public string NegativeToastID = "error";

            [ConfigDescription("Duration the toasts will last for in seconds.")]
            public float Duration = 4f;
        }

        public class SettingsConfig
        {
            [ConfigDescription("New players will spawn with this mode assigned to them.")]
            public string InitialPlayerMode = "pvp";

            [ConfigDescription("This is the mode that NPC players will be assigned. These NPCs are usually spawned by other plugins. Vanilla scientists are NOT counted as NPCs.")]
            public string NpcMode = "npc";

            [ConfigDescription("If true, then modes will respect ownership of vehicles from plugins like VehicleLicense. Once enabled, claimed horses and vehicles will now have their own rules that differ from unclaimed ones.")]
            public bool AllowVehicleModeOwnership = false;

            [ConfigDescription("If true, then twig building blocks can be attacked regardless of mode settings.")]
            public bool AlwaysAllowTwigDamage = true;

            [ConfigDescription("If true, then messages will appear in the server console when things are interacted with. This will spam your console, so only turn on if you are debugging issues.")]
            public bool ShowDebugMessagesInConsole = false;
        }

        public class NoEscapeConfig
        {

            [ConfigDescription("If true, then a player cannot use the flag command if they are combat blocked.")]
            public bool PreventFlaggingWhileCombatBlocked = true;

            [ConfigDescription("If true, then a player cannot use the flag command if they are raid blocked.")]
            public bool PreventFlaggingWhileRaidBlocked = true;

            [ConfigDescription("If true, then a player cannot use the flag command if they are escape blocked.")]
            public bool PreventFlaggingWhileEscapeBlocked = true;
        }

        private void UpdateConfig()
        {
            // EDIT HERE
            var configUpdateStr = "[CONFIG UPDATE] Updating to Version {0}";

            var defaultConfig = new Configuration();

            // Update to 1.3.x
            if (config.Version.Minor < 3)
            {
                PrintWarning(string.Format(configUpdateStr, "1.3.0"));
                foreach(var mode in config.Modes)
                {
                    if (mode.CanRaid != null)
                    {
                        mode.CanAttackTypes = new Dictionary<TargetType, string[]>();
                        foreach(var ttype in TargetTypesForCategory.Attacking)
                        {
                            mode.CanAttackTypes[ttype] = mode.CanRaid;
                        }
                        mode.CanRaid = null;
                    }
                    if (mode.CanAttack != null)
                    {
                        if (mode.CanAttackTypes == null)
                        {
                            mode.CanAttackTypes = new Dictionary<TargetType, string[]>();
                        }
                        mode.CanAttackTypes[TargetType.players] = mode.CanAttack;
                        mode.CanAttack = null;
                    }
                    if (mode.CanTargetWithTraps != null)
                    {
                        mode.CanTargetTypes = new Dictionary<TargetType, string[]>();
                        foreach (var ttype in TargetTypesForCategory.Targeting)
                        {
                            mode.CanTargetTypes[ttype] = mode.CanTargetWithTraps;
                        }
                        mode.CanTargetWithTraps = null;
                    }
                    if (mode.CanLoot != null)
                    {
                        mode.CanLootTypes = new Dictionary<TargetType, string[]>();
                        foreach (var ttype in TargetTypesForCategory.Looting)
                        {
                            mode.CanLootTypes[ttype] = mode.CanLoot;
                        }
                        mode.CanLoot = null;
                    }
                    if (mode.CanEnterOwnedMonuments != null)
                    {
                        mode.CanEnterTypes = new Dictionary<TargetType, string[]>();
                        mode.CanEnterTypes[TargetType.claimedmonuments] = mode.CanEnterOwnedMonuments;
                        mode.CanEnterOwnedMonuments = null;
                    }
                    // Setup new config properties
                    var matchingMode = defaultConfig.Modes.FirstOrDefault(x => x.Name == mode.Name);
                    if (mode.ColorHex == null)
                    {
                        mode.ColorHex = matchingMode?.ColorHex ?? ColorUtility.ToHtmlStringRGBA(UnityEngine.Color.white);
                    }
                    if (mode.DisplayName == null)
                    {
                        mode.DisplayName = matchingMode?.DisplayName ?? mode.Name.ToUpper();
                    }
                }
            }
            // EDIT HERE
            // Init Missing Ttypes
            foreach(var mode in config.Modes)
            {
                foreach (var ttype in TargetTypesForCategory.Attacking)
                {
                    if (!mode.CanAttackTypes.ContainsKey(ttype))
                    {
                        mode.CanAttackTypes[ttype] = new string[0];
                    }
                }
                foreach (var ttype in TargetTypesForCategory.Looting)
                {
                    if (!mode.CanLootTypes.ContainsKey(ttype))
                    {
                        mode.CanLootTypes[ttype] = new string[0];
                    }
                }
                foreach (var ttype in TargetTypesForCategory.Targeting)
                {
                    if (!mode.CanTargetTypes.ContainsKey(ttype))
                    {
                        mode.CanTargetTypes[ttype] = new string[0];
                    }
                }
                foreach (var ttype in TargetTypesForCategory.Mounting)
                {
                    if (!mode.CanMountTypes.ContainsKey(ttype))
                    {
                        mode.CanMountTypes[ttype] = new string[0];
                    }
                }
                foreach (var ttype in TargetTypesForCategory.Entering)
                {
                    if (!mode.CanEnterTypes.ContainsKey(ttype))
                    {
                        mode.CanEnterTypes[ttype] = new string[0];
                    }
                }
            }
            
            config.Version = this.Version;
        }


        private void RevealConfigOptions()
        {
            var hasRevealed = false;
            // ADD PLUGIN INTEGRATION

            // SimpleStatus
            if (SimpleStatus.IsLoaded() && config.SimpleStatus == null)
            {
                config.SimpleStatus = new SimpleStatusConfig()
                {
                    ModeStatusBars = new Dictionary<string, StatusDetailsConfig>
                    {
                        ["pve"] = new StatusDetailsConfig
                        {
                            Show = true,
                            BackgroundColor = "0.385 0.478 0.228 1",
                            Image = "assets/icons/peace.png",
                            ImageColor = "0.545 0.855 0 1",
                            TitleColor = "0.99 0.99 0.99 1",
                            TextColor = "0.855 0.855 0.855 1"

                        },
                        ["pvp"] = new StatusDetailsConfig
                        {
                            Show = true,
                            BackgroundColor = "0.77255 0.23922 0.15686 1",
                            Image = "assets/icons/warning.png",
                            ImageColor = "1 0.82353 0.44706 1",
                            TitleColor = "1 0.82353 0.44706 1",
                            TextColor = "1 0.82353 0.44706 1"
                        }
                    }
                };
                hasRevealed = true;
            }
            // ZoneManager
            if (ZoneManager.IsLoaded() && config.ZoneManager == null)
            {
                config.ZoneManager = new ZoneManagerConfig()
                {
                    ForceModeInZone = new Dictionary<string, string>
                    {
                        ["id=examplezoneid"] = "pvp",
                        ["name~examplezonename"] = "pve"
                    }
                };
                hasRevealed = true;
            }
            // RaidableBases
            if (RaidableBases.IsLoaded() && config.RaidableBases == null)
            {
                config.RaidableBases = new RaidableBasesConfig();
                hasRevealed = true;
            }
            // BetterChat
            if (BetterChat.IsLoaded() && config.BetterChat == null)
            {
                config.BetterChat = new BetterChatConfig()
                {
                    Modes = new Dictionary<string, BetterChatModeConfig>
                    {
                        ["pvp"] = new BetterChatModeConfig
                        {
                            ShowModeTitleInChat = true,
                            ModeTitleFormat = "[{0}]"
                        },
                        ["pve"] = new BetterChatModeConfig
                        {
                            ShowModeTitleInChat = true,
                            ModeTitleFormat = "[{0}]"
                        }
                    }
                };
                hasRevealed = true;
            }
            // Toastify
            if (Toastify.IsLoaded() && config.Toastify == null)
            {
                config.Toastify = new ToastifyConfig();
                hasRevealed = true;
            }
            // NoEscape
            if (NoEscape.IsLoaded() && config.NoEscape == null)
            {
                config.NoEscape = new NoEscapeConfig();
                hasRevealed = true;
            }

            if (hasRevealed)
            {
                SaveConfig();
                timer.In(1.5f, () =>
                {
                    PrintWarning("IMPORTANT - New configuration options are available!");
                });
            }
        }

        public static VersionNumber VersionZero = new VersionNumber(0, 0, 0);

        protected override void LoadConfig()
        {
            base.LoadConfig();
            // UNCOMMENT TO DEBUG
            //LoadDefaultConfig();
            //PrintWarning("DEFAULT CONFIG IS LOADED");
            //return;
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
            if (Version == VersionZero)
            {
                config.Version = Version;
            }
            UpdateConfig();
            SaveConfig();
        }
        public void SaveConfigExternal(Configuration config)
        {
            Puts("Config file saved");
            this.config = config;
            SaveConfig();
            LoadConfig();
            InitModesFromConfig();
            foreach(var player in covalence.Players.All)
            {
                if (player == null) { continue; }
                Interface.CallHook("WarMode_PlayerModeUpdated", player.Id, player.GetMode().Name());
            }
        }
        protected override void SaveConfig() => Config.WriteObject(config);
        protected override void LoadDefaultConfig() => config = new Configuration();
    }
}

namespace Oxide.Plugins
{
    public partial class WarMode
    {
        // For generating a file with config descriptions, not for gameplay use
        [Command("warmode.printconfig")]
        private void PrintConfigReadme(IPlayer player, string command, string[] args)
        {
            if (!player.IsServer) { return; }
            ConfigPrinter.PrintDescriptions<Configuration>(INSTANCE);
            Puts("Printed config readme");
        }

        #region Generic
        [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct | System.AttributeTargets.Property | System.AttributeTargets.Field)]
        public class ConfigDescriptionAttribute : System.Attribute
        {
            public readonly string Text;

            public ConfigDescriptionAttribute(string text)
            {
                Text = text;
            }
        }

        public static class ConfigPrinter
        {
            private struct DescriptionInfo
            {
                public string Label;
                public string Value;
                public int Depth;
            }

            private static string GenerateHtml(List<DescriptionInfo> descriptions)
            {
                StringBuilder sb = new StringBuilder();
                GenerateHtmlRecursive(descriptions, sb, 0, 0);
                return sb.ToString();
            }

            private static int GenerateHtmlRecursive(List<DescriptionInfo> descriptions, StringBuilder sb, int index, int currentDepth)
            {
                sb.AppendLine("<ul>");

                while (index < descriptions.Count && descriptions[index].Depth == currentDepth)
                {
                    sb.Append("<li>");
                    if (string.IsNullOrWhiteSpace(descriptions[index].Value))
                    {
                        sb.Append($"<b>{descriptions[index].Label}</b>:");
                    }
                    else
                    {
                        sb.Append($"<b>{descriptions[index].Label}</b> - {descriptions[index].Value}");
                    }

                    int nextIndex = index + 1;

                    if (nextIndex < descriptions.Count && descriptions[nextIndex].Depth > currentDepth)
                    {
                        index = GenerateHtmlRecursive(descriptions, sb, nextIndex, currentDepth + 1);
                    }
                    else
                    {
                        index++;
                    }

                    sb.AppendLine("</li>");
                }

                sb.AppendLine("</ul>");
                return index;
            }

            public static void PrintDescriptions<T>(CovalencePlugin plugin)
            {
                var descriptions = CollectConfigDescriptions<Configuration>();
                var path = Interface.Oxide.DataDirectory + $"/{plugin.Name}/docs/";
                Directory.CreateDirectory(path);
                var data = GenerateHtml(descriptions);
                File.WriteAllText(path + "configreadme.html", data);
            }

            private static List<DescriptionInfo> CollectConfigDescriptions<T>()
            {
                List<DescriptionInfo> descriptions = new List<DescriptionInfo>();
                CollectDescriptionsRecursive(typeof(T), descriptions, 0);
                return descriptions;
            }

            private static void CollectDescriptionsRecursive(Type type, List<DescriptionInfo> descriptions, int depth)
            {
                if (type.IsArray)
                {
                    type = type.GetElementType();
                }
                // Handle properties
                System.Reflection.PropertyInfo[] properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                foreach (var property in properties)
                {
                    var attribute = property.GetCustomAttributes(false).FirstOrDefault(x => x is ConfigDescriptionAttribute) as ConfigDescriptionAttribute;
                    ProcessMember(property, property.PropertyType, attribute, property.GetValue, descriptions, depth);
                }

                // Handle fields
                System.Reflection.FieldInfo[] fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                foreach (System.Reflection.FieldInfo field in fields)
                {
                    var attribute = field.GetCustomAttributes(false).FirstOrDefault(x => x is ConfigDescriptionAttribute) as ConfigDescriptionAttribute;
                    ProcessMember(field, field.FieldType, attribute, field.GetValue, descriptions, depth);
                }
            }

            private static void ProcessMember(System.Reflection.MemberInfo member, Type memberType, ConfigDescriptionAttribute attribute, Func<object, object> getValue, List<DescriptionInfo> descriptions, int depth)
            {
                if (attribute != null)
                {
                    descriptions.Add(new DescriptionInfo
                    {
                        Label = member.Name,
                        Value = attribute.Text,
                        Depth = depth
                    });
                }

                var nextDepth = depth + 1;
                if (memberType.IsClass && memberType != typeof(string))
                {
                    if (memberType.IsGenericType && memberType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                    {
                        // Get the type of the values in the dictionary
                        Type valueType = memberType.GetGenericArguments()[1];
                        CollectDescriptionsRecursive(valueType, descriptions, nextDepth);
                    }
                    else
                    {
                        CollectDescriptionsRecursive(memberType, descriptions, nextDepth);
                    }
                }
            }
        }
        #endregion
    }
}

namespace Oxide.Plugins
{
    public partial class WarMode
    {
        public PluginData Data = new PluginData();
        public class PluginData
        {
            public Dictionary<string, long> PlayerFlaggingCooldowns = new Dictionary<string, long>();
        }

        private void LoadData()
        {
            try
            {
                Data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>($"{Name}/Data");
            }
            catch (Exception)
            {
                Data = new PluginData();
            }
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Data", Data);
        }
    }
}

namespace Oxide.Plugins.WarModeMethods
{
    public static partial class ExtensionMethods
    {
        public static IEnumerable<BasePlayer> Riders(this BaseVehicle baseVehicle)
        {
            return baseVehicle.mountPoints.Select(x => x.mountable?.GetMounted()).Where(x => x != null);
        }

        public static string ToColorString(this UnityEngine.Color color)
        {
            return $"{color.r} {color.g} {color.b} {color.a}";
        }

        public static void Notify(this BasePlayer basePlayer, string messageId, bool positive, float cooldown, params object[] args) => WarMode.INSTANCE.Notify(WarMode.INSTANCE, basePlayer, messageId, positive, cooldown, args);

        public static void Notify(this BasePlayer basePlayer, Plugin plugin, string messageId, bool positive, float cooldown, params object[] args) => WarMode.INSTANCE.Notify(plugin, basePlayer, messageId, positive, cooldown, args);

        public static void Notify(this BasePlayer basePlayer, string messageId, bool positive, params object[] args) => basePlayer.Notify(messageId, positive, 6f, args);

        public static bool IsAbandonedBase(this BaseEntity entity) => WarMode.IsAbandonedBase(entity); 

        public static bool IsNpcBase(this BuildingPrivlidge priv) => priv.OwnerID == 0;

        public static bool IsFireDamage(this HitInfo info) => info != null && info.damageTypes.GetMajorityDamageType() == Rust.DamageType.Heat;

        public static IEnumerable<string> Residents(this BuildingPrivlidge priv) => WarMode.Residents(priv);

        // ADD TARGET TYPE
        public static WarMode.ModeConfig GetMode(this BaseEntity entity)
        {
            if (entity == null) { return null; }
            var parent = entity.GetParentEntity();
            if (parent != null && parent is BaseVehicle)
            {
                entity = parent;
            }
            var ttype = entity.GetTargetType();
            switch (ttype)
            {
                case TargetType.players:
                    var basePlayer = entity as BasePlayer;
                    return basePlayer.UserIDString.GetMode();
                case TargetType.claimedhorses:
                case TargetType.claimedvehicles:
                case TargetType.vehicles:
                case TargetType.horses:
                case TargetType.tugboats:
                    var baseVehicle = entity as BaseVehicle;
                    var riders = baseVehicle.Riders();
                    if (!riders.Any())
                    {
                        if (baseVehicle.HasParent())
                        {
                            if (parent is BaseVehicle parentVehicle && parent.OwnerID != 0)
                            {
                                return parentVehicle.GetMode();
                            }
                        }
                        if (baseVehicle.OwnerID == 0)
                        {
                            break;
                        }
                        return baseVehicle.OwnerID.GetMode();
                    }
                    return riders.Where(x => x.IsRealPlayer()).Select(x => x.GetTrueMode()).OrderBy(x => x.Priority).FirstOrDefault();
                case TargetType.droppedbackpacks:
                    var droppedItem = entity as DroppedItem;
                    return droppedItem.DroppedBy.GetMode();
                case TargetType.stashes:
                    return entity.OwnerID.GetMode();
            }
            var privInfo = entity.GetPrivInfo();
            if (!privInfo.NotExists)
            {
                return privInfo.GetMode();
            }
            //Debug($"NO MODE LOGIC SETUP FOR TYPE = {entity.GetType()}");
            return null;
        }
        public static WarMode.ModeConfig GetTrueMode(this string userid) => WarMode.INSTANCE.GetMode(userid, false);
        public static WarMode.ModeConfig GetTrueMode(this BasePlayer basePlayer) => basePlayer?.UserIDString.GetTrueMode();
        public static WarMode.ModeConfig GetMode(this string userid) => WarMode.INSTANCE.GetMode(userid, true);
        public static WarMode.ModeConfig GetMode(this ulong userid) => userid.ToString().GetMode();
        public static WarMode.ModeConfig GetMode(this IPlayer player) => player.Id.GetMode();

        public static string Title(this WarMode.ModeConfig mode, object userid, bool colored = true)
        {
            if (mode == null) { return "None"; }
            var title = string.IsNullOrWhiteSpace(mode.DisplayName) ? mode.Name : mode.DisplayName;
            return string.IsNullOrWhiteSpace(mode.ColorHex) ? title : colored ? Color(title, mode.ColorHex) : title;
        }

        public static string Name(this WarMode.ModeConfig mode)
        {
            if (mode == null) { return "NONE"; }
            return mode.Name;
        }

        public static WarMode.ModeConfig GetMode(this BuildingPrivlidge priv)
        {
            if (priv.OwnerID == 0)
            {
                return WarMode.INSTANCE.NpcMode;
            }
            var residents = priv.Residents();
            if (!residents.Any())
            {
                return null;
            } 
            return residents.Select(x => x.GetTrueMode()).OrderBy(x => x.Priority).FirstOrDefault();
        }

        public static void PreventAllDamage(this HitInfo info)
        {
            info.damageTypes.ScaleAll(0);
        }

        public static void PreventAttackLog(this BaseCombatEntity entity)
        {
            var lastAttackTime = entity.lastAttackedTime;
            var lastAttacker = entity.lastAttacker;
            WarMode.DoNextTick(() =>
            {
                if (entity == null) { return; }
                entity.lastAttackedTime = lastAttackTime;
                entity.lastAttacker = lastAttacker;
            });
        }

        public static bool IsRealPlayer(this ulong userid) => userid > 76561197960265728L;

        public static bool IsRealPlayer(this BasePlayer basePlayer) => (!(basePlayer is NPCPlayer)) && (basePlayer?.userID.Get().IsRealPlayer() ?? false);

        public static bool IsRealPlayer(this string userid) => ulong.Parse(userid) > 76561197960265728L;

        public static bool IsHoldingWeapon(this BasePlayer basePlayer) => basePlayer.IsHoldingEntity<BaseProjectile>();

        public static bool IsADS(this BasePlayer basePlayer) => WarMode.INSTANCE.PlayersAiming.Contains(basePlayer.UserIDString);

        public static bool HasTeam(this BasePlayer basePlayer) => basePlayer.currentTeam != 0;

        public static bool HasBypass(this BasePlayer basePlayer) => basePlayer == null ? false : WarMode.HasBypass(basePlayer.UserIDString);

        public static bool IsLeader(this RelationshipManager.PlayerTeam team, ulong userid) => team.teamLeader == userid;

        public static bool TeamIncludes(this BasePlayer basePlayer, string userid) => basePlayer.HasTeam() && basePlayer.Team.members.Any(x => x.ToString() == userid);

        public static IEnumerable<BasePlayer> TeamMembersActive(this BasePlayer basePlayer) => basePlayer.Team?.members.Select(x => WarMode.FindBasePlayer(x)).Where(x => x != null) ?? new BasePlayer[0];

        public static IEnumerable<IPlayer> TeamMembers(this BasePlayer basePlayer) => basePlayer.Team?.members.Select(x => WarMode.FindIPlayer(x.ToString())).Where(x => x != null) ?? new IPlayer[0];

        public static bool InOrSleepingInSafeZone(this BasePlayer basePlayer) => basePlayer.InSafeZone() || (basePlayer.IsSleeping() && (TerrainMeta.Path.FindMonumentWithBoundsOverlap(basePlayer.transform.position)?.IsSafeZone ?? false));

        public static bool HasDrawnMarker(this BasePlayer basePlayer) => WarMode.INSTANCE.HasDrawnMarker.Contains(basePlayer.UserIDString);

        public static WarMode.PrivInfo GetPrivInfo(this BaseEntity baseEntity)
        {
            if (baseEntity == null) { return WarMode.PrivInfo.NotFound; }
            var parent = baseEntity.GetParentEntity();
            // LegacyShelter
            if (baseEntity is LegacyShelter) { return new WarMode.PrivInfo(baseEntity); }
            if (parent is LegacyShelter) { return new WarMode.PrivInfo(parent); }
            // Tugboat
            if (baseEntity is Tugboat) { return new WarMode.PrivInfo(baseEntity); }
            // ToolCupboard
            var priv = baseEntity.GetBuildingPrivilege();
            return new WarMode.PrivInfo(priv);
        }

        public static VehiclePrivilege VehiclePriv(this Tugboat target)
        {
            VehiclePrivilege vpriv = null;
            foreach (BaseEntity child in target.children)
            {
                vpriv = child as VehiclePrivilege;
                if (vpriv != null) { break; }
            }
            return vpriv;
        }

        public static V GetValueOrNew<K, V>(this Dictionary<K, V> dict, K key) where V : new()
        {
            var value = dict.GetValueOrDefault(key);
            if (value != null) { return value; }
            dict[key] = new V();
            return dict[key];
        }

        public static int IndexOf<T>(this IEnumerable<T> collection, T value)
        {
            int i = 0;
            foreach(var v in collection)
            {
                if (v.Equals(value)) { return i; }
                i++;
            }
            return -1;
        }

        public static T NextAfter<T>(this IEnumerable<T> collection, T value)
        {
            if (collection.Count() == 0) { return value; }
            var indexOf = collection.IndexOf(value);
            if (indexOf == -1) { return collection.ElementAt(0); }
            var nextIndex = indexOf += 1;
            if (nextIndex >= collection.Count())
            {
                nextIndex = 0;
            }
            return collection.ElementAt(nextIndex);
        }

        public static bool IsSubsetOf<T>(this IEnumerable<T> coll1, IEnumerable<T> coll2)
        {
            bool isSubset = !coll1.Except(coll2).Any();
            return isSubset;
        }

        public static BaseVehicle VehicleParent(this BaseEntity entity)
        {
            if (entity == null) { return null; }
            if (entity is BaseVehicle && (!entity.HasParent() || !(entity.GetParentEntity() is BaseVehicle))) { return (BaseVehicle) entity; }
            if (entity.HasParent())
            {
                return entity.GetParentEntity().VehicleParent();
            }
            return null;
        }

        public static string ColorHexToRgb(this string hexcolor)
        {
            var color = UnityEngine.Color.white;
            ColorUtility.TryParseHtmlString(hexcolor, out color);
            return color.ToColorString();
        }

        public static string ColorRgbToHex(this string rgb)
        {
            var split = rgb.Split(' ').Select(x => float.Parse(x)).ToArray();
            var color = new UnityEngine.Color(split[0], split[1], split[2], split[3]);
            return "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        public static bool IsLoaded(this Plugin plugin)
        {
            return plugin?.IsLoaded ?? false;
        }
    }
}

namespace Oxide.Plugins
{
    public partial class WarMode
    {
        private bool IsFlagOffCooldown(string userid)
        {
            var targetTime = Data.PlayerFlaggingCooldowns.GetValueOrDefault(userid);
            return targetTime == 0 || targetTime <= DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private void CmdFlagPvp(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null) { return; }
            string mode = null;
            if (args.Length > 0)
            {
                mode = args[0].ToLower();
                if (!(config.Flagging.ModeOptions.Contains(mode) && Modes.ContainsKey(mode)))
                {
                    basePlayer.Notify($"command invalid mode", positive: false, cooldown: 0, config.Flagging.ModeOptions.ToSentence());
                    return;
                }
            }
            var overrule = (bool?)CallHook("WarMode_CanUseFlagCommand", basePlayer.UserIDString) ?? true;
            // Requires Team Leader
            if (config.Flagging.RequiresTeamLeader && basePlayer.HasTeam() && !basePlayer.Team.IsLeader(basePlayer.userID))
            {
                basePlayer.Notify($"notify flag team leader", positive: false, cooldown: 0);
                return;
            }
            // Requires No Hostile
            if (config.Flagging.RequiresNoHostile 
                && config.Teams.SyncModeWithTeamMembers 
                && basePlayer.HasTeam()
                && !basePlayer.TeamMembersActive().All(x => x.GetHostileDuration() <= 0f))
            {
                basePlayer.Notify($"notify flag team no hostile", positive: false, cooldown: 0);
                return;
            }
            else if (config.Flagging.RequiresNoHostile && basePlayer.GetHostileDuration() > 0f)
            {
                basePlayer.Notify($"notify flag no hostile", positive: false, cooldown: 0);
                return;
            }
            // Requires Safe Zone
            if (config.Flagging.RequiresSafeZone 
                && config.Teams.SyncModeWithTeamMembers 
                && basePlayer.HasTeam() 
                && !basePlayer.TeamMembersActive().All(x => x.InOrSleepingInSafeZone()))
            {
                basePlayer.Notify($"notify flag safe zone team", positive: false, cooldown: 0);
                return;
            }
            else if (config.Flagging.RequiresSafeZone && !basePlayer.InSafeZone())
            {
                basePlayer.Notify($"notify flag safe zone", positive: false, cooldown: 0);
                return;
            }
            // On Cooldown
            if (config.Flagging.CooldownSeconds > 0 && !IsFlagOffCooldown(basePlayer.UserIDString))
            {
                var seconds = Data.PlayerFlaggingCooldowns.GetValueOrDefault(basePlayer.UserIDString) - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (seconds < 60)
                {
                    basePlayer.Notify($"notify flag cooldown seconds", positive: false, cooldown: 0, (int)Math.Floor((float)seconds));
                }
                else if (seconds < 3600)
                {
                    basePlayer.Notify($"notify flag cooldown minutes", positive: false, cooldown: 0, (int)(seconds / 60f));
                }
                else
                {
                    basePlayer.Notify($"notify flag cooldown hours", positive: false, cooldown: 0, (int)(seconds / 3600f));
                }
                return;
            }
            // NoEscape Blocked
            if (NoEscape.IsLoaded()  
                && config.Teams.SyncModeWithTeamMembers 
                && basePlayer.HasTeam() 
                && !basePlayer.TeamMembersActive().All(x => NoEscapeAllowsFlagging(x.UserIDString)))
            {
                basePlayer.Notify($"notify flag no escape team", positive: false, cooldown: 0);
                return;
            }
            else if (NoEscape.IsLoaded() && !NoEscapeAllowsFlagging(basePlayer.UserIDString))
            {
                basePlayer.Notify("ne flag blocked", false);
                return;
            }
            if (!overrule)
            {
                return;
            }


            // Set flag
            if (config.Flagging.CooldownSeconds > 0)
            {
                Data.PlayerFlaggingCooldowns[basePlayer.UserIDString] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + config.Flagging.CooldownSeconds;
            }

            if (Interface.CallHook("WarMode_OnFlagCommand", basePlayer.UserIDString, mode) == null)
            {
                if (mode == null)
                {
                    var currentMode = basePlayer.GetTrueMode();
                    mode = config.Flagging.ModeOptions.NextAfter(currentMode.Name);
                }
                SetModeByName(basePlayer.UserIDString, mode);
                UpdateStatusBar(basePlayer.UserIDString);
            }
        }
    }
}

namespace Oxide.Plugins
{
    public partial class WarMode
    {
        public static Core.Libraries.Lang LANG => INSTANCE.lang;

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["mode npc"] = null,
                ["mode pve"] = null,
                ["mode pvp"] = null,

                /* ADD TARGET TYPE */
                /* EDIT HERE */
                ["cannot attack players"] = "Your mode does not have permission to attack {0} players.",
                ["cannot attack horses"] = "Your mode does not have permission to attack horses within bases owned by {0} players.",
                ["cannot attack vehicles"] = "Your mode does not have permission to attack vehicles within bases owned by {0} players.",
                ["cannot attack buildings"] = "Your mode does not have permission to attack buildings within bases owned by {0} players.",
                ["cannot attack traps"] = "Your mode does not have permission to attack traps within bases owned by {0} players.",
                ["cannot attack containers"] = "Your mode does not have permission to attack containers within bases owned by {0} players.",
                ["cannot attack tugboats"] = "Your mode does not have permission to attack tugboats owned by {0} players.",
                ["cannot attack legacyshelters"] = "Your mode does not have permission to attack legacy shelters owned by {0} players.",
                ["cannot attack growables"] = "Your mode does not have permission to attack growables within bases owned by {0} players.",
                ["cannot attack turrets"] = "Your mode does not have permission to attack turrets within bases owned by {0} players.",
                ["cannot attack samsites"] = "Your mode does not have permission to attack SAM sites within bases owned by {0} players.",
                ["cannot attack worldloot"] = "Your mode does not have permission to attack loot containers within bases owned by {0} players.",
                ["cannot attack claimedvehicles"] = "Your mode does not have permission to attack claimed vehicles owned by {0} players.",
                ["cannot attack claimedhorses"] = "Your mode does not have permission to attack claimed horses owned by {0} players.",
                ["cannot attack stashes"] = "Your mode does not have permission to attack stashes owned by {0} players.",
                ["cannot attack sleepingbags"] = "Your mode does not have permission to attack sleeping bags owned by {0} players.",

                ["cannot loot players"] = "Your mode does not have permission to loot {0} players.",
                ["cannot loot vehicles"] = "Your mode does not have permission to loot vehicles within bases owned by {0} players.",
                ["cannot loot traps"] = "Your mode does not have permission to loot traps within bases owned by {0} players.",
                ["cannot loot containers"] = "Your mode does not have permission to loot containers within bases owned by {0} players.",
                ["cannot loot growables"] = "Your mode does not have permission to loot growables within bases owned by {0} players.",
                ["cannot loot turrets"] = "Your mode does not have permission to loot turrets within bases owned by {0} players.",
                ["cannot loot samsites"] = "Your mode does not have permission to loot SAM sites within bases owned by {0} players.",
                ["cannot loot worldloot"] = "Your mode does not have permission to loot loot containers within bases owned by {0} players.",
                ["cannot loot claimedvehicles"] = "Your mode does not have permission to loot claimed vehicles owned by {0} players.",
                ["cannot loot claimedhorses"] = "Your mode does not have permission to loot claimed horses owned by {0} players.",
                ["cannot loot droppedbackpacks"] = "Your mode does not have permission to loot dropped backpacks owned by {0} players.",
                ["cannot loot stashes"] = "Your mode does not have permission to loot stashes owned by {0} players.",
                ["cannot loot sleepingbags"] = "Your mode does not have permission to loot sleeping bags owned by {0} players.",

                ["cannot mount horses"] = "Your mode does not have permission to mount horses within bases owned by {0} players.",
                ["cannot mount vehicles"] = "Your mode does not have permission to mount vehicles within bases owned by {0} players.",
                ["cannot mount tugboats"] = "Your mode does not have permission to mount tugboats owned by {0} players.",
                ["cannot mount claimedvehicles"] = "Your mode does not have permission to mount claimed vehicles owned by {0} players.",
                ["cannot mount claimedhorses"] = "Your mode does not have permission to mount claimed horses owned by {0} players.",

                ["team sync warning"] = "If you accept this team invite your mode will be set to {0} based on the members of the team.",
                ["team sync updated"] = "A team member has changed your team's mode to {0}.",
                ["your mode updated"] = "Your mode is now {0}.",
                ["command invalid mode"] = "Invalid mode, valid modes are {0}",
                ["ss title pvp"] = "PVP",
                ["ss title pve"] = "PVE",
                ["ss text pvp"] = "",
                ["ss text pve"] = "",
                ["rb cannot enter"] = "Your mode does not have permission to enter that raidable base.",
                ["mo owned"] = "This monument is already owned by another player and your mode does not have permission to enter.",
                ["zm forced mode"] = "You have entered a {0} only zone!",
                ["notify flag cooldown seconds"] = "You cannot use this command for another {0} second(s).",
                ["notify flag cooldown minutes"] = "You cannot use this command for another {0} minute(s).",
                ["notify flag cooldown hours"] = "You cannot use this command for another {0} hour(s).",
                ["notify flag safe zone"] = "You must be in a safe zone use that command.",
                ["notify flag safe zone team"] = "All team members must be in a safe zone to use that command.",
                ["notify flag no escape team"] = "You cannot use that command while one or more team members have an active block.",
                ["notify flag no hostile"] = "You cannot use that command while marked as hostile.",
                ["notify flag team no hostile"] = "You cannot use that command while one or more team members are marked as hostile.",
                ["notify flag team leader"] = "Only the leader of your team can use that command.",
                ["button okay"] = "okay",
            }, this);
        }

        private string Lang(string key, BasePlayer basePlayer, params object[] args) => Lang(this, key, basePlayer, args);
        private string Lang(string key, string id, params object[] args) => Lang(this, key, id, args);
        public static string Lang(Plugin plugin, string key, string id, params object[] args) => string.Format(INSTANCE.lang.GetMessage(key, plugin, id), args);
        private string Lang(Plugin plugin, string key, BasePlayer basePlayer, params object[] args) => string.Format(lang.GetMessage(key, plugin, basePlayer?.UserIDString), args);
    }
}

namespace Oxide.Plugins
{
    public partial class WarMode
    {
        public static class Marker
        {
            public static void AddBehavior(BasePlayer basePlayer)
            {
                if (basePlayer == null || !basePlayer.IsConnected || !basePlayer.IsRealPlayer()) { return; }
                if (!INSTANCE._behaviours.ContainsKey(basePlayer.UserIDString))
                {
                    var obj = basePlayer.gameObject.AddComponent<MarkerBehavior>();
                    obj.TickId = UnityEngine.Time.realtimeSinceStartup;
                    INSTANCE._behaviours.Add(basePlayer.UserIDString, obj);
                }
            }
            public static void RemoveBehavior(string userid)
            {
                if (userid == null) { return; }
                var obj = INSTANCE._behaviours.GetValueOrDefault(userid);
                if (obj == null) { return; }
                UnityEngine.Object.Destroy(obj);
                INSTANCE._behaviours.Remove(userid);
            }

            public static void ClearAllBehaviors()
            {
                foreach (var b in INSTANCE._behaviours.Values.ToArray())
                {
                    if (b == null || b.basePlayer == null) { continue; }
                    RemoveBehavior(b.basePlayer.UserIDString);
                    CuiHelper.DestroyUi(b.basePlayer, "warmode.marker");
                }
                INSTANCE._behaviours.Clear();
            }
        }
        

        private Dictionary<string, MarkerBehavior> _behaviours = new Dictionary<string, MarkerBehavior>();
        public class MarkerBehavior : MonoBehaviour
        {
            public float TickId { get; set; } = 0;
            public BasePlayer basePlayer { get; private set; }
            private void Awake()
            {
                basePlayer = GetComponent<BasePlayer>();
                StartWorking();
            }
            private void OnDestroy() => StopWorking();

            private void StartWorking() => InvokeRepeating(nameof(OnTick), CONFIG.Marker.UpdateInterval, CONFIG.Marker.UpdateInterval);

            private void StopWorking() => CancelInvoke(nameof(OnTick));

            private void OnTick()
            {
                if (basePlayer == null) { return; }
                if (basePlayer.IsADS() && !basePlayer.HasDrawnMarker())
                {
                    var target = FindObject<BasePlayer>(basePlayer, INSTANCE.config.Marker.Distance);
                    var mode = target?.GetMode();
                    if (target != null && target.IsRealPlayer() && (mode?.ShowMarkerWhenAimedAt ?? false))
                    {
                        CuiHelper.AddUi(basePlayer, INSTANCE.GetMarkerJson());
                        INSTANCE.HasDrawnMarker.Add(basePlayer.UserIDString);
                    }
                }
                else if (!basePlayer.IsADS() && basePlayer.HasDrawnMarker())
                {
                    CuiHelper.DestroyUi(basePlayer, "warmode.marker");
                    INSTANCE.HasDrawnMarker.Remove(basePlayer.UserIDString);
                }
                else if (basePlayer.HasDrawnMarker())
                {
                    var target = FindObject<BasePlayer>(basePlayer, INSTANCE.config.Marker.Distance);
                    var mode = target?.GetMode();
                    if (target == null || !(mode?.ShowMarkerWhenAimedAt ?? false))
                    {
                        CuiHelper.DestroyUi(basePlayer, "warmode.marker");
                        INSTANCE.HasDrawnMarker.Remove(basePlayer.UserIDString);
                    }
                }
            }
        }
    }
}

namespace Oxide.Plugins
{
    public partial class WarMode
    {
        [Command("warmode.mode"), Permission(PermissionAdmin)]
        private void CmdMode(IPlayer player, string command, string[] args)
        {
            var iplayer = player;
            if (iplayer == null) { return; }
            if (args.Length > 0)
            {
                var name = args[0];
                iplayer = FindIPlayerByName(name);
            }
            if (iplayer == null) { return; }
            player.Message($"PLAYER: {iplayer.Name}\nCURRENT MODE: {GetMode(iplayer.Id, true).Name}\nTRUE MODE: {GetMode(iplayer.Id, false).Name}\nGROUPS: {permission.GetUserGroups(iplayer.Id).ToSentence()}");
        }

        [Command("warmode.tc"), Permission(PermissionAdmin)]
        private void CmdModeTc(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null) { return; }
            var priv = basePlayer.GetBuildingPrivilege();
            if (priv == null) { return; }
            var tcmode = priv.GetMode();
            player.Message($"TC MODE: {tcmode?.Name}\nAUTHED: {priv.Residents().Select(x => ($"{FindIPlayer(x)?.Name} ({x.GetTrueMode()?.Name})")).ToSentence()}");
        }


        private static BaseEntity FindObject(Ray ray, float distance)
        {
            RaycastHit hit;
            return !Physics.Raycast(ray, out hit, distance) ? null : hit.GetEntity();
        }

        // Key=Mode.Name
        public Dictionary<string, ModeConfig> Modes = new Dictionary<string, ModeConfig>();

        private ModeConfig _npcMode = null;
        public ModeConfig NpcMode
        {
            get
            {
                if (_npcMode == null)
                {
                    _npcMode = Modes.Values.First(x => x.Name == config.Settings.NpcMode);
                }
                return _npcMode;
            }
        }

        public void InitModesFromConfig()
        {
            _npcMode = null;
            AddModes(config.Modes);
        }

        private bool modesAreValid = false;
        private const string INVALID_MODE = "[MODE CONFIG ERROR]: {0}";
        public void ValidateModes()
        {
            var foundIssues = 0;
            var validModes = new List<ModeConfig>();
            // Check validity of values
            int idx = -1;
            foreach(var mode in config.Modes)
            {
                idx++;
                // Can't be blank
                if (string.IsNullOrWhiteSpace(mode.Name))
                {
                    PrintError(string.Format(INVALID_MODE, $"Mode at index={idx} has an invalid Name. The Name value cannot be empty."));
                    foundIssues++;
                    continue;
                }
                // Can't contain spaces
                if (mode.Name.Split(' ').Count() > 1)
                {
                    PrintError(string.Format(INVALID_MODE, $"Mode at index={idx} has an invalid Name of '{mode.Name}'. The Name value cannot contain spaces."));
                    foundIssues++;
                    continue;
                }
                // Must be lowercase
                if (!mode.Name.IsLower())
                {
                    PrintError(string.Format(INVALID_MODE, $"Mode at index={idx} has an invalid Name of '{mode.Name}'. The Name value must be all lowercase."));
                    foundIssues++;
                    continue;
                }
                // Duplicate name
                if (validModes.Any(x => x.Name == mode.Name))
                {
                    PrintError(string.Format(INVALID_MODE, $"Mode at index={idx} has an invalid Name of '{mode.Name}'. A mode with this name is already defined at index={config.Modes.ToList().FindIndex(x => x.Name == mode.Name)}."));
                    foundIssues++;
                    continue;
                }
                // Duplicate priority
                if (validModes.Any(x => x.Priority == mode.Priority))
                {
                    PrintError(string.Format(INVALID_MODE, $"Mode at index={idx} has an invalid Priority of '{mode.Name}'. Two modes cannot have the same priority value."));
                    foundIssues++;
                    continue;
                }
                // Group and priority must be defined, or not at all
                if (string.IsNullOrWhiteSpace(mode.Group) != (mode.Priority == null))
                {
                    PrintError(string.Format(INVALID_MODE, $"Mode at index={idx} has an invalid Priority and Group value. A Priority must be defined if a Group is defined."));
                    foundIssues++;
                    continue;
                }
                validModes.Add(mode);
            }
            // Validate NpcMOde
            if (!validModes.Any(x => x.Name == config.Settings.NpcMode))
            {
                PrintError(string.Format(INVALID_MODE, $"The mode '{config.Settings.NpcMode}' you provided for Settings.NpcMode is invalid. This must be a valid mode."));
                foundIssues++;
            }
            // Validate InitialPlayerMode
            if (!validModes.Any(x => x.Name == config.Settings.InitialPlayerMode))
            {
                PrintError(string.Format(INVALID_MODE, $"The mode '{config.Settings.InitialPlayerMode}' you provided for Settings.InitialPlayerMode is invalid. This must be a valid mode."));
                foundIssues++;
            }
            // Validate Flagging ModeOptions
            if(!config.Flagging.ModeOptions.IsSubsetOf(validModes.Select(x => x.Name)))
            {
                PrintError(string.Format(INVALID_MODE, $"The modes you provided for Flagging.ModeOptions contains an invalid mode. Valid modes that you have defined are '{validModes.Select(x => x.Name).ToSentence()}'"));
                foundIssues++;
            }
            modesAreValid = foundIssues == 0;
        }

        public void AddModes(params ModeConfig[] newModeConfigs)
        {
            foreach(var newModeConfig in newModeConfigs)
            {
                Modes[newModeConfig.Name] = newModeConfig;
                if (!string.IsNullOrWhiteSpace(newModeConfig.Group))
                {
                    if (!permission.GroupExists(newModeConfig.Group))
                    {
                        Puts($"Created group '{newModeConfig.Group}' for {newModeConfig.Name}");
                        permission.CreateGroup(newModeConfig.Group, newModeConfig.Name, 0);
                    }
                }
            }
            _cachedPlayerModes.Clear();
        }


        Dictionary<string, string> _cachedPlayerModes = new Dictionary<string, string>();
        public ModeConfig GetMode(string userid, bool allowOverride)
        {
            if (userid == null || !userid.IsRealPlayer()) { return null; }
            if (!_cachedPlayerModes.ContainsKey(userid))
            {
                _cachedPlayerModes[userid] = config.Settings.InitialPlayerMode;
                foreach (var mode in Modes.Values)
                {
                    if (permission.UserHasGroup(userid, mode.Group))
                    {
                        _cachedPlayerModes[userid] = mode.Name;
                        break;
                    }
                }
            }
            if (!allowOverride)
            {
                return Modes[_cachedPlayerModes[userid]];
            }
            // Overrides
            var returnMode = Modes[_cachedPlayerModes[userid]];
            var basePlayer = FindBasePlayer(userid);
            if (basePlayer != null)
            {
                var forcedZoneOverride = ForcedPlayerModeFromZone(basePlayer);
                if (forcedZoneOverride != null)
                {
                    returnMode = forcedZoneOverride;
                }
            }
            return returnMode;
        }

        public void SetModeByName(string userid, string modeName)
        {
            SetModeByGroup(userid, Modes[modeName].Group);
        }

        public void SetModeByGroup(string userid, string modeGroup)
        {
            if (modeGroup == null) { return; }
            _cachedPlayerModes.Remove(userid);
            // Make sure they are in the proper group for their mode
            permission.AddUserGroup(userid, modeGroup);
            // Remove them from other mode groups
            foreach (var mode in Modes.Values)
            {
                if (mode.Group == modeGroup) { continue; }
                permission.RemoveUserGroup(userid, mode.Group);
            }
        }

        public string GetModeGroupByTitle(string modeTitle)
        {
            return Modes[modeTitle].Group;
        }
    }
}

namespace Oxide.Plugins
{
    public partial class WarMode
    {
        public bool NoEscapeAllowsFlagging(string userIdString)
        {
            if (!NoEscape.IsLoaded()) { return true; }
            return (!(bool)NoEscape.Call("IsEscapeBlocked", userIdString) || !config.NoEscape.PreventFlaggingWhileEscapeBlocked)
                && (!(bool)NoEscape.Call("IsRaidBlocked", userIdString) || !config.NoEscape.PreventFlaggingWhileRaidBlocked)
                && (!(bool)NoEscape.Call("IsCombatBlocked", userIdString) || !config.NoEscape.PreventFlaggingWhileCombatBlocked);
        }
    }
}

namespace Oxide.Plugins
{
    public partial class WarMode
    {
        #region ATTACKING
        void OnEntityTakeDamage(BaseCombatEntity target, HitInfo info)
        {
            var _override = (bool?)Interface.CallHook("WarMode_OverrideOnEntityTakeDamage", target, info);
            if (_override != null)
            {
                if (!_override.Value) { info.PreventAllDamage(); }
                return;
            }
            try
            {
                if (target == null || info == null) { return; }
                var initiator = info.Initiator;
                if (initiator == null) { return; }
                var ttype = target.GetTargetType();
                if (ttype == TargetType.unknown) { return; }

                var initiatorMode = initiator.GetMode();
                var targetMode = target.GetMode();

                DebugAction($"ATTACKING", initiator, target, info);

                // WORLD LOOT (LOOT BARRELS)
                if (ttype == TargetType.worldloot)
                {
                    // PLAYER -> WORLD LOOT
                    if (initiator is BasePlayer attackerPlayer)
                    {
                        return; // Attacking loot piles is always allowed
                    }
                }
                // NPCS
                else if (ttype == TargetType.players && target is NPCPlayer targetNpc)
                {
                    targetMode = NpcMode;
                    // PLAYER -> NPC
                    if (initiator is BasePlayer attackerPlayer)
                    {
                        if (!attackerPlayer.GetMode()?.CanAttackMode(targetMode, ttype) ?? false)
                        {
                            attackerPlayer.Notify($"cannot attack {ttype}", false, targetMode.Title(info.InitiatorPlayer?.UserIDString));
                            info.PreventAllDamage();
                            return;
                        }
                    }
                }
                // PLAYERS
                else if (ttype == TargetType.players && target is BasePlayer targetPlayer && targetPlayer.IsRealPlayer())
                {
                    // NPC -> PLAYER
                    if (initiator is NPCPlayer attackerNpc)
                    {
                        if (!NpcMode.CanAttackMode(targetMode, ttype))
                        {
                            info.PreventAllDamage();
                            return;
                        }
                    }
                    // PLAYER -> PLAYER
                    else if (initiator is BasePlayer attackerPlayer)
                    {
                        if (attackerPlayer.HasBypass()) { return; } // Bypass
                        if (attackerPlayer.UserIDString == targetPlayer.UserIDString) { return; } // self damage is allowed
                        if (!attackerPlayer.GetMode()?.CanAttackMode(targetPlayer.GetMode(), ttype) ?? false)
                        {
                            attackerPlayer.Notify($"cannot attack {ttype}", false, targetMode.Title(info.InitiatorPlayer?.UserIDString));
                            info.PreventAllDamage();
                            return;
                        }
                    }
                    // FIRE -> PLAYER
                    else if (initiator is FireBall fireball)
                    {
                        HandleFireDamage(target, info, requireBuildingPriv: false);
                        return;
                    }
                }
                // CLAIMED HORSES
                else if (ttype == TargetType.claimedhorses && target is RidableHorse claimedHorse)
                {
                    // PLAYER -> CLAIMED HORSE
                    if (initiator is BasePlayer attackerPlayer)
                    {
                        if (attackerPlayer.HasBypass()) { return; } // Bypass
                        if (attackerPlayer.OwnerID == claimedHorse.OwnerID) { return; } // Owner, then allowed
                        if (!attackerPlayer.GetMode()?.CanAttackMode(targetMode, ttype) ?? false)
                        {
                            info.InitiatorPlayer?.Notify($"cannot attack {ttype}", false, targetMode.Title(info.InitiatorPlayer?.UserIDString));
                            info.PreventAllDamage();
                            target.PreventAttackLog();
                            return;
                        }
                    }
                    // FIRE -> CLAIMED HORSE
                    else if (initiator is FireBall fireball)
                    {
                        HandleFireDamage(target, info, requireBuildingPriv: false);
                        return;
                    }
                }
                // CLAIMED VEHICLES
                else if (ttype == TargetType.claimedvehicles && target is BaseVehicle claimedVehicle)
                {
                    // PLAYER -> CLAIMED VEHICLE
                    if (initiator is BasePlayer attackerPlayer)
                    {
                        if (attackerPlayer.HasBypass()) { return; } // Bypass
                        if (attackerPlayer.OwnerID == claimedVehicle.OwnerID) { return; } // Owner, then allowed
                        if (!attackerPlayer.GetMode()?.CanAttackMode(targetMode, ttype) ?? false)
                        {
                            info.InitiatorPlayer?.Notify($"cannot attack {ttype}", false, targetMode.Title(info.InitiatorPlayer?.UserIDString));
                            info.PreventAllDamage();
                            target.PreventAttackLog();
                            return;
                        }
                    }
                    // FIRE -> CLAIMED VEHICLE
                    else if (initiator is FireBall fireball)
                    {
                        HandleFireDamage(target, info, requireBuildingPriv: false);
                        return;
                    }
                }

                // ADD TARGET TYPE

                // EVERYTHING ELSE
                else if (TargetTypesForCategory.Attacking.Contains(ttype) 
                    && ttype != TargetType.players 
                    && ttype != TargetType.claimedhorses
                    && ttype != TargetType.claimedvehicles
                // ADD TARGET TYPE
                )
                {
                    // PLAYER -> X
                    if (initiator is BasePlayer attackerPlayer)
                    {
                        if (attackerPlayer.HasBypass()) { return; } // Bypass
                        var privInfo = target.GetPrivInfo();
                        if (privInfo.NotExists) { return; } // No priv, no mode, allowed
                        if (privInfo.IsAuthed(attackerPlayer)) { return; } // If authed, then allowed
                        if (config.Settings.AlwaysAllowTwigDamage && target is BuildingBlock buildingBlock)
                        {
                            if (buildingBlock.grade == BuildingGrade.Enum.Twigs) { return; }; // Twig damage is allowed
                        }
                        targetMode = privInfo.GetMode();
                        if (!attackerPlayer.GetMode()?.CanAttackMode(targetMode, ttype) ?? false)
                        {
                            info.InitiatorPlayer?.Notify($"cannot attack {ttype}", false, targetMode.Title(info.InitiatorPlayer?.UserIDString));
                            info.PreventAllDamage();
                            target.PreventAttackLog();
                            return;
                        }
                    }
                    // FIRE -> X
                    else if (initiator is FireBall fireball)
                    {
                        HandleFireDamage(target, info);
                        return;
                    }
                }
            } catch(Exception e)
            {
                PrintError($"EXCEPTION: {e.GetType()}\ntarget={target?.GetType().ToString() ?? "null"}, attacker={info?.Initiator?.GetType().ToString() ?? "null"}");
            }
        }

        private void HandleFireDamage(BaseCombatEntity target, HitInfo info, bool requireBuildingPriv = true)
        {
            if (requireBuildingPriv)
            {
                var priv = target.GetBuildingPrivilege();
                if (priv == null) { return; } // No priv, then no restriction
                                              // Unfortunately, we cant tell who owns the fireball, so we have to block all damage from it
            }
            info.PreventAllDamage();
            target.PreventAttackLog();
            return;
        }

        #endregion

        #region LOOTING
        bool CanLootPlayer(BasePlayer target, BasePlayer looter)
        {
            var result = CanLootPlayerEntity(looter, target, target.UserIDString, 1);
            return result == null ? true : ((bool?) result).Value;
        }

        object CanLootEntity(BasePlayer looter, DroppedItemContainer container) => CanLootPlayerEntity(looter, null, (container?.playerSteamID ?? 0).ToString(), 2, container: container);
        object CanLootEntity(BasePlayer looter, LootableCorpse corpse) => CanLootPlayerEntity(looter, null, (corpse?.playerSteamID ?? 0).ToString(), 3, container: corpse);

        object OnItemPickup(Item item, BasePlayer looter)
        {
            if (looter == null || item == null) { return null; }
            if (item.IsBackpack())
            {
                var droppedItem = item.GetWorldEntity() as DroppedItem;
                if (droppedItem == null) { return null; }
                if (!CanPickupDroppedBackpack(droppedItem, looter))
                {
                    return false;
                }
            }
            return null;
        }

        void OnLootEntity(BasePlayer looter, DroppedItem droppedItem)
        {
            if (looter.HasBypass()) { return; }
            if (looter == null || droppedItem == null) { return; }
            // Backpacks
            if (droppedItem.item?.IsBackpack() ?? false)
            {
                if (!CanPickupDroppedBackpack(droppedItem, looter))
                {
                    NextTick(() =>
                    {
                        looter?.EndLooting();
                    });
                }
            }
        }

        private bool CanPickupDroppedBackpack(DroppedItem droppedItem, BasePlayer looter)
        {
            if (looter.HasBypass()) { return true; }
            var item = droppedItem.item;
            if (item == null) { return true; }
            if (item.contents == null) { return true; } // Its not a real backpack, allow it
            var droppedBy = droppedItem.DroppedBy.ToString();
            if (looter.UserIDString == droppedBy) { return true; } // Its yours then its allowed
            if (looter.TeamIncludes(droppedBy)) { return true; } // Team members, then its allowed
            if (!looter.GetMode().CanLootMode(droppedBy.GetMode(), TargetType.droppedbackpacks))
            {
                looter.Notify("cannot loot droppedbackpacks", positive: false, droppedItem.GetMode().Title(looter.UserIDString));
                return false;
            }
            return true;
        }

        public object CanLootPlayerEntity(BasePlayer looter, BasePlayer target, string targetUserid, int from, BaseCombatEntity container = null)
        {
            DebugAction($"LOOTING", looter, target);
            if (looter.HasBypass()) { return null; }
            if (looter == null || !targetUserid.IsRealPlayer()) { return null; }
            if (looter.UserIDString == targetUserid) { return null; } // Can always loot your own corpse
            if (looter.TeamIncludes(targetUserid)) { return null; } // You can loot your own teammates corpse
            // Corspse is in forced Zone
            var targetMode = target == null ? targetUserid.GetMode() : target.GetMode();
            if (IsUsingZoneManager && container != null)
            {
                var zoneids = GetEntityZoneIDs(container);
                var forcedMode = GetForcedModeForZones(zoneids);
                if (forcedMode != null)
                {
                    if (!looter.GetMode().CanLootMode(forcedMode, TargetType.players))
                    {
                        looter.Notify("cannot loot players", positive: false, forcedMode.Title(looter.UserIDString));
                        return false;
                    }
                    return null;
                }
            }
            if (!looter.GetMode().CanLootMode(targetMode, TargetType.players))
            {
                looter.Notify("cannot loot players", positive: false, targetMode.Title(looter.UserIDString));
                return false;
            }
            return null;
        }

        object CanLootEntity(BasePlayer looter, StorageContainer container) => CanLootHelper(looter, container);

        object OnRackedWeaponMount(Item item, BasePlayer basePlayer, WeaponRack instance) => CanLootHelper(basePlayer, instance);

        object OnRackedWeaponUnload(Item slot, BasePlayer basePlayer, WeaponRack instance) => CanLootHelper(basePlayer, instance);

        object OnRackedWeaponLoad(Item slot, ItemDefinition itemDefinition, BasePlayer basePlayer, WeaponRack instance) => CanLootHelper(basePlayer, instance);

        object OnRackedWeaponSwap(Item item, WeaponRackSlot forItemDef, BasePlayer basePlayer, WeaponRack instance) => CanLootHelper(basePlayer, instance);

        object OnRackedWeaponTake(Item slot, BasePlayer looter, WeaponRack instance) => CanLootHelper(looter, instance);

        object OnGrowableGather(GrowableEntity instance, BasePlayer looter, bool eat) => CanLootHelper(looter, instance);

        object CanTakeCutting(BasePlayer looter, GrowableEntity entity) => CanLootHelper(looter, entity);

        object CanAdministerVending(BasePlayer basePlayer, VendingMachine instance) => CanLootHelper(basePlayer, instance, skipVendingCheck: true);

        object CanRenameBed(BasePlayer basePlayer, SleepingBag bed, string bedName) => CanLootHelper(basePlayer, bed);

        private object CanLootHelper(BasePlayer looter, BaseEntity target, bool skipVendingCheck = false)
        {
            var _override = (bool?)Interface.CallHook("WarMode_OverrideOnLootContainer", looter, target, skipVendingCheck);
            if (_override != null) { return _override.Value ? null : _override; }
            if (looter.HasBypass()) { return null; }
            var ttype = target.GetTargetType();
            if (!TargetTypesForCategory.Looting.Contains(ttype)) { return null; } // Not a tracked looting ttype, its allowed
            DebugAction($"LOOTING", looter, target);
            // Vending Machine Shop
            if (!skipVendingCheck && target is VendingMachine vendingMachine)
            {
                if (!vendingMachine.CanPlayerAdmin(looter))
                {
                    return null; // Players can always use the vending side
                }
            }
            // Claimed Vehicle or Horse or Tugboat
            if (ttype == TargetType.claimedhorses || ttype == TargetType.claimedvehicles)
            {
                var baseVehicle = target.VehicleParent();
                if (baseVehicle == null || baseVehicle?.OwnerID == 0) { return null; }
                if (looter.userID == baseVehicle.OwnerID) { return null; } // Is owner, looting allowed
                if (looter.TeamIncludes(baseVehicle.OwnerID.ToString())) { return null; } // Team members, then its allowed
                if (!looter.GetMode()?.CanLootMode(baseVehicle.GetMode(), ttype) ?? false)
                {
                    looter.Notify("cannot loot vehicle", positive: false, target.GetMode().Title(looter.UserIDString));
                    return false;
                }
            }
            // Small Stash
            if (ttype == TargetType.stashes)
            {
                
                var stache = target as StashContainer;
                if (stache == null || stache.OwnerID == 0) { return null; }
                if (looter.userID == stache.OwnerID) { return null;} // Can loot your own stash
                if (looter.TeamIncludes(stache.OwnerID.ToString())) { return null; } // Team members, then its allowed
                if (!looter.GetMode()?.CanLootMode(stache.GetMode(), ttype) ?? false)
                {
                    looter.Notify($"cannot loot {ttype}", positive: false, target.GetMode().Title(looter.UserIDString));
                    return false;
                }
            }
            // Everything else
            else
            {
                if (looter.HasBypass()) { return null; } // Bypass
                var privInfo = target.GetPrivInfo();
                if (privInfo.NotExists) { return null; } // No priv, no mode, allowed
                if (privInfo.IsAuthed(looter)) { return null; } // If authed, then allowed
                if (!looter.GetMode()?.CanLootMode(privInfo.GetMode(), ttype) ?? false)
                {
                    looter.Notify($"cannot loot {ttype}", positive: false, target.GetMode().Title(looter.UserIDString));
                    return false;
                }
            }

            return null;
        }
        #endregion

        #region TRAP TARGETING
        object CanBeTargeted(BasePlayer targetPlayer, MonoBehaviour behaviour)
        {
            if (targetPlayer == null || behaviour == null) { return null; }
            if (targetPlayer is NPCPlayer) { return null; } // TODO
            var turret = behaviour.GetComponent<BaseEntity>();
            if (turret == null) { return null; }
            var _override = (bool?)Interface.CallHook("WarMode_OverrideCanTarget", targetPlayer, turret);
            if (_override != null) { return _override.Value ? null : _override; }
            var priv = turret?.GetBuildingPrivilege();
            bool isNpcOwnedTurret;
            if (priv != null)
            {
                isNpcOwnedTurret = priv.IsNpcBase();
            }
            else
            {
                isNpcOwnedTurret = turret.OwnerID == 0;
            }
            // NPC TURRET -> PLAYER
            if (isNpcOwnedTurret)
            {
                if (!NpcMode.CanTargetMode(targetPlayer.GetMode(), TargetType.turrets))
                {
                    return false;
                }
            }
            // PLAYER TURRET -> PLAYER
            else
            {
                ModeConfig turretMode = priv?.GetMode() ?? (turret.OwnerID != 0 ? turret.OwnerID.GetMode() : null);
                if (turretMode != null && !turretMode.CanTargetMode(targetPlayer.GetMode(), TargetType.turrets))
                {
                    return false;
                }
            }
            return null;
        }

        object OnSamSiteTarget(SamSite samSite, BaseCombatEntity target)
        {
            if (samSite == null || target == null) { return null; }
            var _override = (bool?)Interface.CallHook("WarMode_OverrideCanTarget", target, samSite);
            if (_override != null) { return _override.Value ? null : _override; }
            var priv = samSite?.GetBuildingPrivilege();
            bool isNpcOwnedSamSite;
            if (priv != null)
            {
                isNpcOwnedSamSite = priv.IsNpcBase();
            }
            else
            {
                isNpcOwnedSamSite = samSite.OwnerID == 0;
            }
            // NPC OWNED SAM SITE
            if (isNpcOwnedSamSite)
            {
                // NPC SAMSITE -> VEHICLE
                if (target is BaseVehicle targetVehicle)
                {
                    if (!NpcMode.CanTargetMode(targetVehicle.GetMode(), TargetType.samsites))
                    {
                        return false;
                    }
                }
            }
            // PLAYER OWNED SAM SITE
            else
            {
                // PLAYER SAMSITE -> VEHICLE
                if (target is BaseVehicle targetVehicle)
                {
                    ModeConfig samSiteMode = priv?.GetMode() ?? (samSite.OwnerID != 0 ? samSite.OwnerID.GetMode() : null);
                    if (samSiteMode != null && !samSiteMode.CanTargetMode(targetVehicle.GetMode(), TargetType.samsites))
                    {
                        return false;
                    }
                }
            }
            return null;
        }

        object OnTrapTrigger(BaseTrap trap, GameObject go)
        {
            if (trap == null || go == null) { return null; }
            var target = go.ToBaseEntity() as BaseCombatEntity;
            if (target == null) { return null; }
            var _override = (bool?)Interface.CallHook("WarMode_OverrideCanTarget", target, trap);
            if (_override != null) { return _override.Value ? null : _override; }
            var priv = trap?.GetBuildingPrivilege();
            bool isNpcOwnedTrap;
            if (priv != null)
            {
                isNpcOwnedTrap = priv.IsNpcBase();
            }
            else
            {
                isNpcOwnedTrap = trap.OwnerID == 0;
            }
            // NPC OWNED TRAP
            if (isNpcOwnedTrap)
            {
                // NPC TRAP -> PLAYER
                if (target is BasePlayer targetPlayer)
                {
                    if (!NpcMode.CanTargetMode(targetPlayer.GetMode(), targetPlayer.GetTargetType()))
                    {
                        return false;
                    }
                }
            }
            // PLAYER OWNED TRAP
            else
            {
                // PLAYER TRAP -> PLAYER
                if (target is BasePlayer targetPlayer)
                {
                    ModeConfig trapMode = priv?.GetMode() ?? (trap.OwnerID != 0 ? trap.OwnerID.GetMode() : null);
                    if (trapMode != null && !trapMode.CanTargetMode(targetPlayer.GetMode(), targetPlayer.GetTargetType()))
                    {
                        return false;
                    }
                }
            }
            return null;
        }
        #endregion

        #region MOUNTING
        object CanMountEntity(BasePlayer mounter, BaseMountable target)
        {
            var _override = (bool?)Interface.CallHook("WarMode_OverrideCanMountEntity", mounter, target);
            if (_override != null) { return _override.Value ? null : _override; }
            if (mounter == null || target == null) { return null; }
            if (mounter.HasBypass()) { return null; }
            var baseVehicle = target.VehicleParent();
            var ttype = target.GetTargetType();
            if (!TargetTypesForCategory.Mounting.Contains(ttype)) { return null; }
            DebugAction($"MOUNTING", mounter, target);
            // Claimed Vehicle or Horse or Tugboat
            if (ttype == TargetType.claimedhorses || ttype == TargetType.claimedvehicles)
            {
                
                if (mounter.userID == baseVehicle.OwnerID) { return null; } // Is owner
                if (mounter.TeamIncludes(baseVehicle.OwnerID.ToString())) { return null; } // Team members, then its allowed
                if (!mounter.GetMode()?.CanMountMode(baseVehicle.GetMode(), ttype) ?? false)
                {
                    mounter.Notify($"cannot mount {ttype}", positive: false, target.GetMode().Title(mounter.UserIDString));
                    return false;
                }
            }
            // Unclaimed Vehicle or Horse or Tugboat
            else
            {
                var privInfo = target.GetPrivInfo();
                if (privInfo.NotExists) { return null; } // No priv, no mode, allowed
                if (privInfo.IsAuthed(mounter)) { return null; } // If authed, then allowed
                if (!mounter.GetMode()?.CanMountMode(privInfo.GetMode(), ttype) ?? false)
                {
                    mounter.Notify($"cannot mount {ttype}", positive: false, target.GetMode().Title(mounter.UserIDString));
                    return false;
                }
            }

            return null;
        }
        #endregion

        #region MARKER
        void OnPlayerSleepEnded(BasePlayer basePlayer)
        {
            if (basePlayer == null) { return; }
            if (config.Marker.Show)
            {
                Marker.AddBehavior(basePlayer);
            }
            UpdateStatusBar(basePlayer.UserIDString);
        }

        void OnPlayerDisconnected(BasePlayer basePlayer)
        {
            if (basePlayer == null) { return; }
            Marker.RemoveBehavior(basePlayer.UserIDString);
        }

        void OnUserConnected(IPlayer player)
        {
            if (player == null) { return; }
            var mode = GetMode(player.Id, false);
            if (mode == null) { return; }
            SetModeByGroup(player.Id, mode.Group);
        }

        public HashSet<string> PlayersAiming = new HashSet<string>();
        public HashSet<string> HasDrawnMarker = new HashSet<string>();
        void OnPlayerInput(BasePlayer basePlayer, InputState input)
        {
            if (basePlayer == null) { return; }
            if (basePlayer.IsHoldingWeapon() && input.IsDown(BUTTON.FIRE_SECONDARY))
            {
                PlayersAiming.Add(basePlayer.UserIDString);
            }
            else if (PlayersAiming.Contains(basePlayer.UserIDString))
            {
                PlayersAiming.Remove(basePlayer.UserIDString);
                CuiHelper.DestroyUi(basePlayer, "warmode.marker");
            }
        }
        #endregion

        #region MODES
        void OnUserGroupAdded(string id, string groupName)
        {
            if (id == null || groupName == null || !Modes.Values.Any(x => x.Group == groupName)) { return; }
            SetModeByGroup(id, groupName);
            var basePlayer = BasePlayer.FindAwakeOrSleeping(id);
            if (config.Teams.SyncModeWithTeamMembers && basePlayer != null && basePlayer.HasTeam())
            {
                if (!teamStartedSync.Contains(basePlayer.currentTeam))
                {
                    teamStartedSync.Add(basePlayer.currentTeam);
                    SyncTeamModeToFlaggedValue(basePlayer);
                    teamStartedSync.Remove(basePlayer.currentTeam);
                }
            }
            NextTick(() =>
            {
                var mode = Modes.Values.FirstOrDefault(x => x.Group == groupName);
                Interface.CallHook("WarMode_PlayerModeUpdated", id, mode.Name());
                if (basePlayer == null)
                {
                    basePlayer = BasePlayer.FindAwakeOrSleeping(id);
                }
                if (basePlayer != null)
                {
                    basePlayer.Notify($"your mode updated", positive: true, cooldown: 0.1f, mode.Title(basePlayer.UserIDString));
                    UpdateStatusBar(id);
                }
            }); 
        }

        void OnTeamInvite(BasePlayer inviter, BasePlayer target)
        {
            if (config.Teams.SyncModeWithTeamMembers)
            {
                var projectedMode = GetProjectedMode(inviter.TeamMembers().Concat(new IPlayer[] { target.IPlayer }));
                if (projectedMode.Name != target.GetMode().Name)
                {
                    target.Notify("team sync warning", positive: false, projectedMode.Title(target.UserIDString));
                }
            }
        }

        ModeConfig GetProjectedMode(IEnumerable<IPlayer> projectedTeam)
        {
            return projectedTeam.Select(x => x.GetMode()).OrderBy(x => x.Priority).First();
        }

        void OnTeamUpdate(ulong currentTeam, ulong newTeam, BasePlayer basePlayer)
        {
            NextTick(() =>
            {
                if (basePlayer != null && basePlayer.HasTeam() && config.Teams.SyncModeWithTeamMembers)
                {
                    SyncTeamMode(basePlayer);
                }
            });
        }
        #endregion
    }
}

namespace Oxide.Plugins
{
    public partial class WarMode
    {
        void OnPlayerEnteredRaidableBase(BasePlayer basePlayer, Vector3 raidPos, bool allowPVP, int mode)
        {
            if (basePlayer == null) return;
            var pMode = basePlayer.GetMode();
            var kick = false;
            if (allowPVP && !pMode.CanEnterPvpRaidableBases)
            {
                kick = true;
            }
            else if (!allowPVP && !pMode.CanEnterPveRaidableBases)
            {
                kick = true;
            }
            if (kick)
            {
                basePlayer.Notify("rb cannot enter", positive: false, cooldown: 0);
                // Kick the player
                var vehicle = basePlayer.GetMounted()?.VehicleParent();
                if (vehicle != null)
                {
                    // If its a vehicle, turn it around
                    EjectVehicle(vehicle);
                }
                else
                {
                    // TP the player outside the bounds
                    EjectPlayer(basePlayer, raidPos, config.RaidableBases.ProtectionRadius);
                }
            }
        }
    }
}

namespace Oxide.Plugins
{
    public partial class WarMode
    {
        // ADD TARGET TYPE
        [Flags]
        [JsonConverter(typeof(StringEnumConverter))]
        public enum TargetType
        {
            unknown,
            players,
            horses,
            vehicles,
            buildings,
            traps,
            containers,
            tugboats,
            legacyshelters,
            growables,
            turrets,
            samsites,
            worldloot,
            claimedvehicles,
            claimedhorses,
            claimedmonuments,
            droppedbackpacks,
            stashes,
            sleepingbags
        }

        // EDIT HERE
        // ADD TARGET TYPE
        public static class TargetTypesForCategory
        {
            public static readonly TargetType[] Attacking = new TargetType[]
            {
                TargetType.players,
                TargetType.horses,
                TargetType.vehicles,
                TargetType.buildings,
                TargetType.traps,
                TargetType.containers,
                TargetType.tugboats,
                TargetType.legacyshelters,
                TargetType.turrets,
                TargetType.samsites,
                TargetType.claimedvehicles,
                TargetType.claimedhorses,
                TargetType.stashes,
                TargetType.sleepingbags
            };


            public static readonly TargetType[] Looting = new TargetType[]
            {
                TargetType.players,
                TargetType.vehicles,
                TargetType.traps,
                TargetType.containers,
                TargetType.growables,
                TargetType.turrets,
                TargetType.samsites,
                TargetType.claimedvehicles,
                TargetType.claimedhorses,
                TargetType.droppedbackpacks,
                TargetType.stashes,
                TargetType.sleepingbags
            };

            public static readonly TargetType[] Targeting = new TargetType[]
            {
                TargetType.traps,
                TargetType.turrets,
                TargetType.samsites
            };

            public static readonly TargetType[] Mounting = new TargetType[]
            {
                TargetType.vehicles,
                TargetType.horses,
                TargetType.tugboats,
                TargetType.claimedvehicles,
                TargetType.claimedhorses
            };

            public static readonly TargetType[] Entering = new TargetType[]
            {
                TargetType.claimedmonuments
            };

            public static bool IsLoaded(TargetType type, Configuration confg = null)
            {
                if (confg == null)
                {
                    confg = INSTANCE.config;
                }
                if (type == TargetType.claimedhorses || type == TargetType.claimedvehicles)
                {
                    return confg.Settings.AllowVehicleModeOwnership;
                }
                return true;
            }
        }
    }
}

namespace Oxide.Plugins.WarModeMethods
{
    public static partial class ExtensionMethods
    {
        public static WarMode.TargetType GetTargetType(this BaseEntity entity)
        {
            // ADD TARGET TYPE
            if (entity == null) { return WarMode.TargetType.unknown; }
            var parent = entity.GetParentEntity();
            if (entity is BasePlayer)
            {
                return WarMode.TargetType.players;
            }
            if (entity is SleepingBag)
            {
                return WarMode.TargetType.sleepingbags;
            }
            if (entity is LootContainer)
            {
                return WarMode.TargetType.worldloot;
            }
            if (entity is Tugboat || parent is Tugboat)
            {
                return WarMode.TargetType.tugboats;
            }
            if (entity is LegacyShelter)
            {
                return WarMode.TargetType.legacyshelters;
            }
            if (WarMode.CONFIG.Settings.AllowVehicleModeOwnership && ((entity is RidableHorse && entity.OwnerID != 0) || (parent is RidableHorse && parent.OwnerID != 0)))
            {
                return WarMode.TargetType.claimedhorses;
            }
            if (entity is RidableHorse || parent is RidableHorse)
            {
                return WarMode.TargetType.horses;
            }
            if (WarMode.CONFIG.Settings.AllowVehicleModeOwnership && ((entity is BaseVehicle && entity.OwnerID != 0) || (parent is BaseVehicle && parent.OwnerID != 0)))
            {
                return WarMode.TargetType.claimedvehicles;
            }
            if (entity is BaseVehicle || parent is BaseVehicle)
            {
                return WarMode.TargetType.vehicles;
            }
            if (entity is BuildingBlock || entity is StabilityEntity)
            {
                return WarMode.TargetType.buildings;
            }
            if (entity is AutoTurret || entity is FlameTurret)
            {
                return WarMode.TargetType.turrets;
            }
            if (entity is SamSite)
            {
                return WarMode.TargetType.samsites;
            }
            if (entity is BaseTrap || entity is Barricade || entity is GunTrap)
            {
                return WarMode.TargetType.traps;
            }
            if (entity is StashContainer)
            {
                return WarMode.TargetType.stashes;
            }
            if (entity is DroppedItem droppedItem && (droppedItem.item?.IsBackpack() ?? false))
            {
                return parent?.GetTargetType() ?? WarMode.TargetType.droppedbackpacks;
            }
            if (entity is StorageContainer)
            {
                return parent?.GetTargetType() ?? WarMode.TargetType.containers;
            }
            if (entity is GrowableEntity)
            {
                return WarMode.TargetType.growables;
            }
            return WarMode.TargetType.unknown;
        }
    }
}

namespace Oxide.Plugins
{
    public partial class WarMode
    {
        public void SyncTeamMode(BasePlayer anyTeamMember)
        {
            if (anyTeamMember == null) { return; }
            if (anyTeamMember.HasTeam())
            {
                var members = anyTeamMember.TeamMembers();
                var newMode = GetProjectedMode(members);
                foreach (var member in members)
                {
                    if (member.GetMode().Name() != newMode.Name)
                    {
                        var target = BasePlayer.Find(member.Id);
                        target?.Notify("team sync updated", positive: true, cooldown: 0, newMode.Title(target.UserIDString));
                        SetModeByGroup(member.Id, newMode.Group);
                    }
                }
            }
        }

        public void SyncTeamModeToFlaggedValue(BasePlayer basedOn)
        {
            if (basedOn == null) { return; }
            var newMode = basedOn.GetMode();
            if (newMode == null) { return; }
            if (basedOn.HasTeam())
            {
                foreach (var member in basedOn.TeamMembers())
                {
                    if (member == null) { continue; }
                    if (member.GetMode().Name() != newMode.Name)
                    {
                        var target = BasePlayer.Find(member.Id);
                        target?.Notify("team sync updated", positive: true, cooldown: 0, newMode.Title(target.UserIDString));
                        SetModeByGroup(member.Id, newMode.Group);
                    }
                }
            }
        }
    }
}

namespace Oxide.Plugins
{
    public partial class WarMode
    {
        public static string Color(string text, string hexColor)
        {
            return $"<color={hexColor}>{text}</color>";
        }

        public static string Size(string text, int size)
        {
            return $"<size={size}>{text}</size>";
        }

        public static BasePlayer FindBasePlayer(string userIdOrName)
        {
            return BasePlayer.FindAwakeOrSleeping(userIdOrName);
        }

        public static BasePlayer FindBasePlayer(ulong userid)
        {
            return BasePlayer.FindAwakeOrSleepingByID(userid);
        }

        public static IPlayer FindIPlayer(string userid)
        {
            return INSTANCE.covalence.Players.FindPlayerById(userid);
        }

        public static IPlayer FindIPlayerByName(string name)
        {
            return INSTANCE.covalence.Players.FindPlayer(name);
        }

        public HashSet<ulong> AbandonedEntities = new HashSet<ulong>();

        public static bool IsAbandonedBase(BaseEntity entity)
        {
            if (entity == null) { return false; }
            if (INSTANCE.AbandonedEntities.Contains(entity.net.ID.Value)) { return true; }
            else if (INSTANCE.AbandonedBases?.Call<bool>("isAbandoned", entity) ?? false)
            {
                INSTANCE.AbandonedEntities.Add(entity.net.ID.Value);
                return true;
            }
            return false;
        }

        public static IEnumerable<string> Residents(BuildingPrivlidge priv)
        {
            return priv.authorizedPlayers.Select(x => x.userid.ToString());
        }

        public static bool HasBypass(string userid)
        {
            return INSTANCE.permission.UserHasPermission(userid, PermissionBypass);
        }

        public static void DoNextTick(Action action) => INSTANCE.NextTick(action);

        public static void EjectVehicle(BaseVehicle vehicle)
        {
            vehicle.transform.rotation = Quaternion.Euler(vehicle.transform.eulerAngles.x, vehicle.transform.eulerAngles.y - 180f, vehicle.transform.eulerAngles.z);
            vehicle.rigidBody.velocity *= -2f;
        }

        public static void EjectPlayer(BasePlayer basePlayer, Vector3 position, float radius)
        {
            Vector3 Pposition = GetEjectLocation(basePlayer.transform.position, 10f, position, radius);
            basePlayer.Teleport(Pposition);
        }

        public static Vector3 GetEjectLocation(Vector3 a, float distance, Vector3 target, float radius) // Credit to RaidableBases
        {
            const int targetMask2 = 10551313;
            var position = ((a.XZ3D() - target.XZ3D()).normalized * (radius + distance)) + target; // Credits ZoneManager
            float y = TerrainMeta.HighestPoint.y + 250f;

            if (Physics.Raycast(position + new Vector3(0f, y, 0f), Vector3.down, out var hit, Mathf.Infinity, targetMask2, QueryTriggerInteraction.Ignore))
            {
                position.y = hit.point.y + 0.75f;
            }
            else position.y = Mathf.Max(TerrainMeta.HeightMap.GetHeight(position), TerrainMeta.WaterMap.GetHeight(position)) + 0.75f;

            return position;
        }

        public struct PrivInfo
        {
            public static PrivInfo NotFound = new PrivInfo(null);
            public BaseEntity BaseEntity { get; private set; }
            public readonly ulong OwnerID => BaseEntity.OwnerID;
            public readonly string OwnerIDString => BaseEntity.OwnerID.ToString();
            public readonly ulong ID => BaseEntity.net.ID.Value;
            public readonly bool NotExists => BaseEntity == null;

            public PrivInfo(BaseEntity priv)
            {
                BaseEntity = priv;
            }

            public IEnumerable<string> Residents()
            {
                if (BaseEntity is BuildingPrivlidge priv)
                {
                    return WarMode.Residents(priv);
                }
                if (BaseEntity is LegacyShelter ls)
                {
                    return new string[] { ls.OwnerID.ToString() };
                }
                if (BaseEntity is Tugboat tb)
                {
                    return tb.VehiclePriv().authorizedPlayers.Select(x => x.userid.ToString());
                }
                return Enumerable.Empty<string>();
            }

            public ModeConfig GetMode()
            {
                if (BaseEntity is BuildingPrivlidge priv)
                {
                    if (priv.OwnerID == 0)
                    {
                        return WarMode.INSTANCE.NpcMode;
                    }
                    var residents = Residents();
                    if (!residents.Any())
                    {
                        return null;
                    }
                    return residents.Select(x => x.GetTrueMode()).OrderBy(x => x.Priority).FirstOrDefault();
                }
                if (BaseEntity is LegacyShelter ls)
                {
                    return OwnerIDString.GetTrueMode();
                }
                if (BaseEntity is Tugboat tb)
                {
                    return tb.VehiclePriv().authorizedPlayers.Select(x => x.userid.ToString().GetTrueMode()).OrderBy(x => x.Priority).FirstOrDefault();
                }
                return null;
            }

            public bool IsAuthed(BasePlayer basePlayer)
            {
                if (BaseEntity is BuildingPrivlidge priv)
                {
                    return priv.IsAuthed(basePlayer);
                }
                if (BaseEntity is LegacyShelter ls)
                {
                    return ls.OwnerID == basePlayer.userID;
                }
                if (BaseEntity is Tugboat tb)
                {
                    return tb.IsAuthed(basePlayer);
                }
                return false;
            }

            public bool IsAbandonedBase()
            {
                return WarMode.IsAbandonedBase(BaseEntity);
            }

            public bool IsNpcBase()
            {
                return OwnerID == 0;
            }
        }
    }
}

namespace Oxide.Plugins
{
    public partial class WarMode
    {
        private bool IsUsingZoneManager => (ZoneManager?.IsLoaded ?? false) && (config.ZoneManager.ForceModeInZone?.Any() ?? false);

        private ModeConfig ForcedPlayerModeFromZone(BasePlayer basePlayer)
        {
            if (!IsUsingZoneManager) { return null; }
            string[] zoneids = null;
            if (basePlayer.IsNpc) { return null; }
            try
            {
                var result = ZoneManager?.Call("GetPlayerZoneIDs", basePlayer);
                if (result != null)
                {
                    zoneids = (string[])result;
                }
            } catch (Exception) { return null; }
            if (zoneids == null) { return null; }
            var forcedMode = GetForcedModeForZones(zoneids);
            return forcedMode;
        }

        private void OnEnterZone(string zoneid, BasePlayer basePlayer)
        {
            if (!IsUsingZoneManager) { return; }
            var forcedMode = GetForcedModeForZone(zoneid);
            if (forcedMode == null) { return; }
            var mode = basePlayer.GetMode();
            basePlayer.Notify("zm forced mode", positive: true, cooldown: 0, mode.Title(basePlayer.UserIDString));
            UpdateStatusBar(basePlayer.UserIDString);
            Interface.CallHook("WarMode_PlayerModeUpdated", basePlayer.UserIDString, mode.Name());
        }
        private void OnExitZone(string zoneid, BasePlayer basePlayer)
        {
            if (!IsUsingZoneManager) { return; }
            var forcedMode = GetForcedModeForZone(zoneid);
            if (forcedMode == null) { return; }
            UpdateStatusBar(basePlayer.UserIDString);
            var mode = basePlayer.GetMode();
            Interface.CallHook("WarMode_PlayerModeUpdated", basePlayer.UserIDString, mode.Name());
        }

        private string[] GetEntityZoneIDs(BaseEntity entity)
        {
            if (!IsUsingZoneManager) { return Array.Empty<string>(); }
            try
            {
                var result = ZoneManager?.Call("GetEntityZoneIDs", entity);
                if (result != null)
                {
                    return (string[])result;
                }
            }
            catch (Exception) { }
            return Array.Empty<string>();
        }

        private string GetZoneName(string zoneid)
        {
            if (!IsUsingZoneManager) { return null; }
            try
            {
                var result = ZoneManager?.Call("GetZoneName", zoneid);
                if (result != null)
                {
                    return result.ToString();
                }
            }
            catch (Exception) { }
            return null;
        }

        public struct ZoneManagerModeQuery
        {
            public string Property { get; set; }
            public string Operator { get; set; }
            public string Value { get; set; }
            public string Mode { get; set; }

            public bool ValueMatches(string nameOrId)
            {
                if (Operator == "=")
                {
                    return Value.ToLower().Equals(nameOrId.ToLower());
                }
                else if (Operator == "~")
                {
                    return nameOrId.ToLower().Contains(Value.ToLower());
                }
                return false;
            }
        }

        public ZoneManagerModeQuery[] _zoneManagerQueries = null;
        public ZoneManagerModeQuery[] ZoneManagerQueries
        {
            get
            {
                if (_zoneManagerQueries == null)
                {
                    var queries = new List<ZoneManagerModeQuery>();
                    foreach(var query in config.ZoneManager.ForceModeInZone)
                    {
                        var newquery = new ZoneManagerModeQuery();
                        if (query.Key.Contains("="))
                        {
                            var split = query.Key.Split("=");
                            newquery.Property = split[0].ToLower().Trim();
                            newquery.Operator = "=";
                            newquery.Value = split[1].ToLower().Trim();
                            newquery.Mode = query.Value.Trim();
                        }
                        else if (query.Key.Contains("~"))
                        {
                            var split = query.Key.Split("~");
                            newquery.Property = split[0].ToLower().Trim();
                            newquery.Operator = "~";
                            newquery.Value = split[1].ToLower().Trim();
                            newquery.Mode = query.Value.Trim();
                        }
                        else
                        {
                            newquery.Property = "id";
                            newquery.Operator = "=";
                            newquery.Value = query.Key.ToLower().Trim();
                            newquery.Mode = query.Value.Trim();
                        }
                        if (newquery.Property != "id" && newquery.Property != "name") { continue; }
                        queries.Add(newquery);
                    }
                    _zoneManagerQueries = queries.ToArray();
                }
                return _zoneManagerQueries;
            }
        }

        private ModeConfig GetForcedModeForZone(params string[] zoneids)
        {
            return GetForcedModeForZones(zoneids);
        }

        private ModeConfig GetForcedModeForZones(IEnumerable<string> zoneids)
        {
            if (!IsUsingZoneManager) { return null; }
            try
            {
                foreach(var zoneid in zoneids)
                {
                    foreach(var query in ZoneManagerQueries)
                    {
                        var key = zoneid;
                        if (query.Property == "name")
                        {
                            key = GetZoneName(zoneid);
                        }
                        if (query.ValueMatches(key))
                        {
                            var mode = INSTANCE.Modes.GetValueOrDefault(query.Mode);
                            if (mode == null) { continue; }
                            return mode;
                        }
                    }
                }
            } catch (Exception) { }
            return null;
        }
    }
}
