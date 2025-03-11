

using Oxide.Plugins.RaidProtectionExtensionMethods;
using System.Collections.Generic;
using ConVar;
using System.Linq;
using System.Data.SqlTypes;
using Oxide.Game.Rust.Cui;
using System.ComponentModel;
using UnityEngine;
using Oxide.Core.Plugins;
using System.Text;
using Oxide.Core.Libraries.Covalence;
using Oxide.Plugins;
using System;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("RaidProtection", "mr01sam", "3.4.11")]
    [Description("Provides raid protection to bases at the cost of a resource.")]
    partial class RaidProtection : CovalencePlugin
    {
        public static RaidProtection PLUGIN = null;

        [PluginReference]
        private readonly Plugin ImageLibrary;

        [PluginReference]
        private readonly Plugin Economics;

        [PluginReference]
        private readonly Plugin ServerRewards;

        [PluginReference]
        private readonly Plugin CustomStatusFramework;

        [PluginReference]
        private readonly Plugin Notify;

        [PluginReference]
        private readonly Plugin ZoneManager;

        [PluginReference]
        private readonly Plugin Clans;

        [PluginReference]
        private readonly Plugin AbandonedBases;

        [PluginReference]
        private readonly Plugin SkillTree;

        [PluginReference]
        private readonly Plugin SimpleStatus;

        private bool PluginLoaded = true;

        private bool ShowConfigWarnings = true;

        public const string PermissionAdmin = "raidprotection.admin";
        public const string PermissionIgnore = "raidprotection.ignore";
        public readonly string PermissionLevel = "raidprotection.level.";

        private bool AllowRaidProtectedTugboats = false; // this will be set by protection level settings

        private void OnServerInitialized()
        {
            PLUGIN = this;
            DependencyCheck();
            if (!PluginLoaded) { return; }
            if (!permission.PermissionExists(PermissionAdmin, this))
            {
                permission.RegisterPermission(PermissionAdmin, this);
            }
            if (!permission.PermissionExists(PermissionIgnore, this))
            {
                permission.RegisterPermission(PermissionIgnore, this);
            }
            AddCovalenceCommand(config.Commands.Admin, nameof(CmdController));
            AddCovalenceCommand(config.Commands.Levels, nameof(UILevelsShow));
            AddCovalenceCommand(config.Commands.Protection, nameof(TcInfoShowPlayer));
            InitProtectionLevels();
            LoadImages();
            LoadProtectedCupboards();
            FindNewCupboards();
            InitIntegrations();
            SubscribePlugins();
            if (config.EnableLogging)
            {
                PrintWarning("WARNING: You have logging ENABLED, this will generate large amount of logs for every tool cupboard on your server. It is recommended to have this enabled only for debugging purposes.");
            }
            timer.In(5f, () =>
            {
                BalanceLedger.LoadAll();
            });
            if (SimpleStatusHelper.IsLoaded())
            {
                SimpleStatusHelper.CreateStatuses();
                foreach (var player in BasePlayer.activePlayerList)
                {
                    OnPlayerConnected(player);
                }
            }
        }

        private void UnsubscribePlugins()
        {
            Unsubscribe(nameof(OnServerSave));
            Unsubscribe(nameof(OnPluginLoaded));
            Unsubscribe(nameof(OnPluginUnloaded));
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnEntityBuilt));
            Unsubscribe(nameof(OnEntityKill));
            Unsubscribe(nameof(OnEntityDeath));
            Unsubscribe(nameof(OnCupboardAuthorize));
            Unsubscribe(nameof(OnCupboardClearList));
            Unsubscribe(nameof(OnCupboardDeauthorize));
            Unsubscribe(nameof(OnEntityTakeDamage));
            Unsubscribe(nameof(OnLootEntity));
            Unsubscribe(nameof(OnLootEntityEnd));
            Unsubscribe(nameof(OnLootNetworkUpdate));
            Unsubscribe(nameof(OnUserConnected));
            Unsubscribe(nameof(OnUserDisconnected));
            Unsubscribe(nameof(OnUserPermissionGranted));
            Unsubscribe(nameof(OnUserPermissionRevoked));
            Unsubscribe(nameof(OnGroupPermissionGranted));
            Unsubscribe(nameof(OnGroupPermissionRevoked));
            Unsubscribe(nameof(OnActiveItemChanged));
            Unsubscribe(nameof(OnPlayerDeath));
            Unsubscribe(nameof(OnItemDropped));
        }

        private void SubscribePlugins()
        {
            Subscribe(nameof(OnServerSave));
            Subscribe(nameof(OnPluginLoaded));
            Subscribe(nameof(OnPluginUnloaded));
            Subscribe(nameof(OnEntitySpawned));
            Subscribe(nameof(OnEntityBuilt));
            Subscribe(nameof(OnEntityKill));
            Subscribe(nameof(OnEntityDeath));
            Subscribe(nameof(OnCupboardAuthorize));
            Subscribe(nameof(OnCupboardClearList));
            Subscribe(nameof(OnCupboardDeauthorize));
            Subscribe(nameof(OnEntityTakeDamage));
            Subscribe(nameof(OnLootEntity));
            Subscribe(nameof(OnLootEntityEnd));
            Subscribe(nameof(OnLootNetworkUpdate));
            Subscribe(nameof(OnUserConnected));
            Subscribe(nameof(OnUserDisconnected));
            Subscribe(nameof(OnUserPermissionGranted));
            Subscribe(nameof(OnUserPermissionRevoked));
            Subscribe(nameof(OnGroupPermissionGranted));
            Subscribe(nameof(OnGroupPermissionRevoked));
            Subscribe(nameof(OnActiveItemChanged));
            Subscribe(nameof(OnPlayerDeath));
            Subscribe(nameof(OnItemDropped));
        }

        private void Unload()
        {
            if (PLUGIN != null)
            {
                UnloadIntegrations();
            }
            SaveProtectedCupboards();
            if (config.EnableLedger)
            {
                BalanceLedger.SaveAll();
            }
            UnsubscribePlugins();
            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerDisconnected(player);
            }
            RemoveAndDestroyAllBehaviors();
        }

        void OnServerSave()
        {
            SaveProtectedCupboards();
            if (config.EnableLedger)
            {
                BalanceLedger.SaveAll();
            }
        }

        void OnServerShutdown()
        {
            SaveProtectedCupboards();
            if (config.EnableLedger)
            {
                BalanceLedger.SaveAll();
            }
        }

        void OnPluginLoaded(Plugin name)
        {
            if (name?.Name == this.Name) { return; }
            ShowConfigWarnings = false;
            LoadConfig();
            DependencyCheck();
            InitIntegrations();
        }

        void OnPluginUnloaded(Plugin name)
        {
            if (name?.Name == this.Name) { return; }
            DependencyCheck();
        }

        private void DependencyCheck()
        {
            timer.In(1f, () => // delay incase plugin has been reloaded
            {
                if (!ImageLibrary?.IsLoaded ?? true)
                {
                    PrintError($"The required dependency ImageLibary is not installed, {Name} will not work properly without it.");
                    PluginLoaded = false;
                    return;
                }
                var format = "Integration with '{0}' is set to 'true' in your configuration file, however this plugin is not installed on this server. This option will be temporarily set to 'false' to avoid issues. Once this plugin is installed, you will no longer see this warning and your configuration settings will be followed.";
                if (config.Integration.ServerRewards && (!ServerRewards?.IsLoaded ?? true))
                {
                    PrintWarning(String.Format(format, "ServerRewards"));
                    config.Integration.ServerRewards = false;
                }
                if (config.Integration.Economics && (!Economics?.IsLoaded ?? true))
                {
                    PrintWarning(String.Format(format, "Economics"));
                    config.Integration.Economics = false;
                }
                if (config.Integration.SimpleStatus && (!SimpleStatus?.IsLoaded ?? true))
                {
                    PrintWarning(String.Format(format, "Simple Status"));
                    config.Integration.SimpleStatus = false;
                }
                if (config.Integration.CustomStatusFramework && (!CustomStatusFramework?.IsLoaded ?? true))
                {
                    PrintWarning(String.Format(format, "Custom Status Framework"));
                    config.Integration.CustomStatusFramework = false;
                }
                if (config.Integration.Clans && (!Clans?.IsLoaded ?? true))
                {
                    PrintWarning(String.Format(format, "Clans"));
                    config.Integration.Clans = false;
                }
                if (config.Integration.AbandonedBases && (!AbandonedBases?.IsLoaded ?? true))
                {
                    PrintWarning(String.Format(format, "Abandoned Bases"));
                    config.Integration.AbandonedBases = false;
                }
                if (config.Integration.SkillTree && (!SkillTree?.IsLoaded ?? true))
                {
                    PrintWarning(String.Format(format, "Skill Tree"));
                    config.Integration.SkillTree = false;
                }
                if (config.Integration.Economics && config.Integration.ServerRewards)
                {
                    PrintWarning("Integration is set to 'true' for both 'Economics' and 'ServerRewards' in your configuration file. Only one of these plugins can be used for currency, please set only one of these to 'true' to avoid issues. Both will be disabled until this is resolved.");
                    config.Integration.ServerRewards = false;
                    config.Integration.Economics = false;
                }
            });
        }

        private void InitIntegrations()
        {
            try
            {
                if (config.Integration.CustomStatusFramework)
                {
                    CustomStatusFrameworkHelper.CreateProtectionStatus();
                }
            } catch (Exception) { }
        }

        private void UnloadIntegrations()
        {
            if (config.Integration.CustomStatusFramework)
            {
                CustomStatusFrameworkHelper.DeleteProtectionStatus();
            }
        }

        private void InitProtectionLevels()
        {
            try
            {
                foreach(var level in config.Protection.ProtectionLevels)
                {
                    if (!AllowRaidProtectedTugboats && level.AllowTugboatProtection)
                    {
                        AllowRaidProtectedTugboats = true;
                    }
                    var permString = PermissionLevel + level.Rank;
                    if (!permission.PermissionExists(permString, this))
                    {
                        permission.RegisterPermission(permString, this);
                    }
                    if (config.EnableConsoleMessages)
                    {
                        Puts($"Registered permission {permString}");
                    }
                }
            }
            catch
            {
                PrintError("Failed to load protection levels from config file, make sure they are properly formatted.");
            }
        }

        private void LoadImages()
        {
            ImageLibrary.Call<bool>("AddImage", config.Images.StatusProtected, $"status.protected", 0UL);
            ImageLibrary.Call<bool>("AddImage", config.Images.StatusUnprotected, $"status.unprotected", 0UL);
            ImageLibrary.Call<bool>("AddImage", config.Images.StatusInfo, $"status.info", 0UL);
            ImageLibrary.Call<bool>("AddImage", config.Images.StatusToggle, $"status.toggle", 0UL);
            ImageLibrary.Call<bool>("AddImage", config.Images.StatusRefresh, $"status.refresh", 0UL);
            ImageLibrary.Call<bool>("AddImage", config.Images.InfoOwners, $"rp.owners", 0UL);
            ImageLibrary.Call<bool>("AddImage", config.Images.InfoCosts, $"rp.costs", 0UL);
            ImageLibrary.Call<bool>("AddImage", config.Images.InfoCheck, $"rp.check", 0UL);
            ImageLibrary.Call<bool>("AddImage", config.Images.InfoCross, $"rp.cross", 0UL);
            ImageLibrary.Call<bool>("AddImage", "https://www.rustedit.io/images/imagelibrary/scrap.png", $"rp.scrap", 0UL);
        }

        private void FindNewCupboards()
        {
            NextTick(() =>
            {
                var privs = BaseNetworkable.FindObjectsOfType<BuildingPrivlidge>().Where(x => x != null && x.net != null && !IsNpcBase(x) && !ProtectedCupboardManager.ProtectedCupboardExists(x.net.ID.Value)).ToList();
                var count = 0;
                if (privs.Count > 0)
                {
                    foreach (var priv in privs)
                    {
                        try
                        {
                            if (priv != null)
                            {
                                if (AddProtectedCupboard(priv))
                                {
                                    count++;
                                }
                            }
                        }
                        catch (Exception) { }
                    }
                    if (config.EnableConsoleMessages)
                    {
                        Puts($"Loaded {count} new cupboards");
                    }
                }
            });
        }

        private void LoadProtectedCupboards()
        {
            NextTick(() =>
            {
                var existing = new Dictionary<string, ProtectedEntity>();
                try
                {
                    existing = LoadDataFile<Dictionary<string, ProtectedEntity>>("ProtectedCupboards");
                    if (existing != null)
                    {
                        int count = 0;
                        int skipped = 0;
                        foreach (var kvp in existing)
                        {
                            try
                            {
                                var priv = kvp.Value.BaseEntity;
                                var tc = kvp.Value;
                                if (priv != null && (!tc.IsTugboat || AllowRaidProtectedTugboats))
                                {
                                    if (AddProtectedCupboard(priv, tc))
                                    {
                                        count++;
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                skipped++;
                            }
                        }
                        if (count > 0 && config.EnableConsoleMessages)
                        {
                            Puts($"Loaded {count} existing cupboards");
                        }
                    }
                }
                catch (Exception)
                {
                    PrintError("Failed to load protected cupboards");
                }
            });
        }

        private void SaveProtectedCupboards()
        {
            try
            {
                var allCupboards = ProtectedCupboardManager.ProtectedCupboards.Where(x => x.Value?.BaseEntity != null && !x.Value.IsNpcBase).ToDictionary(t => t.Key, t => t.Value);
                SaveDataFile("ProtectedCupboards", allCupboards);
                if (config.EnableConsoleMessages)
                {
                    Puts($"Saved {allCupboards.Count} cupboards");
                }
            }
            catch
            {
                PrintError("Failed to save protected cupboards");
            }
        }
    }
}

namespace Oxide.Plugins
{
	partial class RaidProtection
	{
        
        /*
         * Returns 0 if entity unprotected and 100 if entity fully protected
         */
        private float GetProtectionPercent(BaseEntity entity)
        {
            if (entity == null) { return 0; }
            var priv = entity.GetBuildingPrivilege();
            if (priv == null) { return 0; }
            var tc = ProtectedCupboardManager.GetByID(priv.net.ID.Value);
            if (tc == null) { return 0; }
            return IsProtectedEntity(entity, tc) ? tc.CurrentProtectionPercent : 0;
        }

        /*
         * Returns the protection level rank of the player
         */
        private int GetProtectionLevel(BasePlayer basePlayer)
        {
            if (basePlayer == null) { return ProtectionLevel.NONE.Rank; }
            return ProtectionLevel.GetProtectionLevelOfPlayer(basePlayer.UserId()).Rank;
        }

        /*
         * Returns a list of the owners of the structure associated with the given entity
         */
        private List<IPlayer> GetOwners(BaseEntity entity)
        {
            var retVal = new List<IPlayer>();
            if (entity == null) { return retVal; }
            var priv = entity.GetBuildingPrivilege();
            if (priv == null) { return retVal; }
            var tc = ProtectedCupboardManager.GetByID(priv.net.ID.Value);
            if (tc == null) { return retVal; }
            return tc.Owners;
        }

        /*
         * Returns the player who is the founder of the structure associated with the given entity. Can be null.
         */
        private IPlayer GetFounder(BaseEntity entity)
        {
            IPlayer retVal = null;
            if (entity == null) { return retVal; }
            var priv = entity.GetBuildingPrivilege();
            if (priv == null) { return retVal; }
            var tc = ProtectedCupboardManager.GetByID(priv.net.ID.Value);
            if (tc == null) { return retVal; }
            return tc.Founder;
        }

        /*
         * Returns the protection balance of the structure associated with the given entity
         */
        private float GetProtectionBalance(BaseEntity entity)
        {
            var retVal = 0;
            if (entity == null) { return retVal; }
            var priv = entity.GetBuildingPrivilege();
            if (priv == null) { return retVal; }
            var tc = ProtectedCupboardManager.GetByID(priv.net.ID.Value);
            if (tc == null) { return retVal; }
            return tc.Balance;
        }

        /*
         * Returns the hours of protection remaining of the structure associated with the given entity
         */
        private float GetProtectionHours(BaseEntity entity)
        {
            var retVal = 0;
            if (entity == null) { return retVal; }
            var priv = entity.GetBuildingPrivilege();
            if (priv == null) { return retVal; }
            var tc = ProtectedCupboardManager.GetByID(priv.net.ID.Value);
            if (tc == null) { return retVal; }
            return tc.HoursOfProtection;
        }

        /* API Hooks */
        /*  
         *  OnProtectionInit(BaseEntity entity, string status)
         *  
         *  OnProtectionStarted(BaseEntity entity)
         *  
         *  OnProtectionStopped(BaseEntity entity, string reason)
         */
    }
}

namespace Oxide.Plugins
{
    partial class RaidProtection
    {
        private readonly string[] CHANGELOG = new string[]
        {
            "## 3.4.0",
            "Implemented config option 'Founder Limit' which will limit the number of tool cupboards that a player can be the founder of. If a player reaches their limit, they can not receive protection on new toolcupboards and previous tool cupboards will need to be destroyed first.",
            "Fixed issue where players who were dead and offline sometimes would lose protection.",
            "Updated GetOwners and GetFounder api. Now returns IPlayer instead of BasePlayer.",
            "Attack helicopter missiles and napalm can now correctly damage structures if protection for Attack Heli is off.",
            "You will no longer need to delete your config file after each major update unless otherwise specified.",
            "Fixed issue where /pro command wouldnt work on tugboat bases",
            "Added support for Simple Status, which has better performance and is recommended to use instead of Custom Status Framework",
            "## 3.4.1",
            "Fixed null reference error with attack heli",
            "## 3.4.2",
            "Update for the release of Simple Status",
            "Added two new API hooks, OnProtectionStarted and OnProtectionStopped",
            "Fixed bug where commands wouldnt show player display names",
            "Added config options for Notify to change the notify type",
            "## 3.4.3",
            "Fixed issue with Simple Status not loading correctly on server restart. Be sure to also download the latest version of Simple Status",
            "Fixed null reference spam issue with Raidable Bases",
            "Suppressed some null reference error with the OnSleepEnded hook",
            "## 3.4.4",
            "Fixed issue with online/offline timers",
            "Fixed issue with the new retro TC skin erasing data",
            "## 3.4.5",
            "Fixed macro click hacking issue with balances",
            "## 3.4.6",
            "Added config option to move tabs horizontally in addition to the existing vertical",
            "Rehosted images, default config updated with new URLs",
            "## 3.4.7",
            "Fixed issue with macro spam exploit hack",
            "Fixed issue where some Traps, like turrets, would be counted as Electrical instead of Traps if you're using Carbon",
            "Fixed problem with offline timers if your online protection percent was above 0%",
            "## 3.4.8",
            "Fixed issue with userID error with Rust 7/6/2024 update. Reported on tab switch.",
            "## 3.4.9",
            "Fixed rare issue where cupboard balances would be reset.",
            "Removed extra console log 'Timer finished'"
        };
    }
}

namespace Oxide.Plugins
{
    partial class RaidProtection
    {
        public static class CollectionManager
        {
            public static readonly int COLLECTION_INTERVAL = 5;

            public static Dictionary<ulong, Timer> _activeTimers = new Dictionary<ulong, Timer>();

            public static void RunCollectionLoop(ProtectedEntity tc)
            {
                if (tc == null)
                {
                    return;
                }
                var entityID = tc?.ID;
                try
                {
                    var timer = PLUGIN.timer.In(COLLECTION_INTERVAL, () =>
                    {
                        PLUGIN.CollectProtectionCost(tc);
                    });
                    if (_activeTimers.ContainsKey(tc.ID))
                    {
                        _activeTimers[tc.ID].Destroy();
                    }
                    else
                    {
                        tc.NumTimesCollected = 0; // start new loop
                    }
                    _activeTimers[tc.ID] = timer;
                } catch(Exception ex)
                {
                    PLUGIN.PrintWarning($"Failed to run timer for TC.EntityID={entityID} IsNull={tc == null}");
                }
            }

            public static void StopCollectionLoop(ProtectedEntity tc)
            {
                if (_activeTimers.ContainsKey(tc.ID))
                {
                    var timer = _activeTimers[tc.ID];
                    timer.Destroy();
                    _activeTimers.Remove(tc.ID);
                }
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class RaidProtection : CovalencePlugin
    {
        public class CommandInfo
        {
            public string Command { get; set; }
            public CommandArgument[] Arguments { get; set; } = new CommandArgument[0];
            public string Description { get; set; }
            public string Method { get; set; }
            public int Rank = 999;
            public bool SkipOptional = false;
            public string Permission
            {
                get
                {
                    return Permissions == null ? null : Permissions.FirstOrDefault();
                }
                set
                {
                    Permissions = new string[1] { value };
                }
            }
            public string[] Permissions { get; set; } = new string[0];
            public bool AdminOnly
            {
                get
                {
                    return Permissions.Any(x => x.Contains("admin"));
                }
            }
            public int CommandWordCount
            {
                get
                {
                    return Command.Split(' ').Length;
                }
            }

            public int RequiredArgCount { 
                get
                {
                    return Arguments.Where(x => !x.Optional).Count();
                } 
            }

            public int TotalArgCount
            {
                get
                {
                    return Arguments.Length;
                }
            }

            public string ArgString
            {
                get
                {
                    return $"{string.Join(" ", Arguments.Select(x => x.ToString()))}";
                }
            }

            public void Execute(IPlayer player, string command, string[] args)
            {
                PLUGIN.Call(Method, player, command, args);
            }

            public ValidationResponse Validate(params string[] args) => Validate(false, args);

            public ValidationResponse Validate(bool isServer, params string[] args)
            {
                if (args.Length < RequiredArgCount || args.Length > TotalArgCount)
                {
                    return new ValidationResponse(ValidationStatusCode.INVALID_LENGTH, RequiredArgCount, TotalArgCount);
                }
                var ArgsToCheck = Arguments;
                if (SkipOptional && !isServer && args.Length == RequiredArgCount)
                {
                    ArgsToCheck = Arguments.Where(x => !x.Optional).ToArray();
                }
                int i = 0;
                foreach(var arg in args)
                {
                    var Argument = ArgsToCheck[i];
                    var resp = Argument.Validate(arg);
                    if (!resp.IsValid)
                    {
                        switch(resp.StatusCode)
                        {
                            case ValidationStatusCode.INVALID_VALUE:
                            case ValidationStatusCode.PLAYER_NOT_FOUND:
                                resp.SetData(arg);
                                break;
                        }
                        return resp;
                    }
                    i++;
                }
                return new ValidationResponse();
            }
        }

        public class CommandArgument
        {
            public static readonly CommandArgument PLAYER_NAME = new CommandArgument
            {
                Parameter = "player",
                Validate = (value) =>
                {
                    return BasePlayer.FindAwakeOrSleeping(value) == null ? new ValidationResponse(ValidationStatusCode.PLAYER_NOT_FOUND) : new ValidationResponse(ValidationStatusCode.SUCCESS);
                }
            };

            public string Parameter { get; set; }
            public bool Optional { get; set; } = false;
            public string[] AllowedValues
            {
                set
                {
                    Validate = (given) =>
                    {
                        var expected = value;
                        return expected.Any(x => x.ToLower() == given.ToLower()) ? new ValidationResponse() : new ValidationResponse(ValidationStatusCode.VALUE_NOT_ALLOWED, given, expected);
                    };
                }
            }
            public Func<string, ValidationResponse> Validate { get; set; } = ((value) => { return new ValidationResponse(); });

            public override string ToString()
            {
                return $"<{Parameter}{(Optional ? "?" : string.Empty)}>";
            }
        }

        public class ValidationResponse
        {
            public bool IsValid
            {
                get
                {
                    return StatusCode == ValidationStatusCode.SUCCESS;
                }
            }
            public ValidationStatusCode StatusCode { get; }
            public object[] Data { get; private set; } = new object[0];

            public ValidationResponse()
            {
                StatusCode = ValidationStatusCode.SUCCESS;
            }

            public ValidationResponse(ValidationStatusCode statusCode)
            {
                StatusCode = statusCode;
            }

            public ValidationResponse(ValidationStatusCode statusCode, params object[] data)
            {
                StatusCode = statusCode;
                Data = data;
            }

            public void SetData(params object[] data)
            {
                Data = data;
            }
        }

        public enum ValidationStatusCode
        {
            SUCCESS,
            INVALID_LENGTH,
            INVALID_VALUE,
            PLAYER_NOT_FOUND,
            VALUE_NOT_ALLOWED
        }
    }
}

namespace Oxide.Plugins
{
    partial class RaidProtection
    {
        #region Premade Arguments
        private static readonly CommandArgument REQUIRED_TC_ID_ARGUMENT = new CommandArgument
        {
            Parameter = "tc id",
            Validate = (given) =>
            {
                ulong id = 0;
                if (!ulong.TryParse(given, out id))
                {
                    return new ValidationResponse(ValidationStatusCode.INVALID_VALUE, given);
                }
                var tc = ProtectedCupboardManager.GetByID(id);
                return tc == null ? new ValidationResponse(ValidationStatusCode.INVALID_VALUE, given) : new ValidationResponse();
            }
        };
        private static readonly CommandArgument OPTIONAL_TC_ID_ARGUMENT = new CommandArgument
        {
            Parameter = "tc id",
            Optional = true,
            Validate = (given) =>
            {
                ulong id = 0;
                if (!ulong.TryParse(given, out id))
                {
                    return new ValidationResponse(ValidationStatusCode.INVALID_VALUE, given);
                }
                var tc = ProtectedCupboardManager.GetByID(id);
                return tc == null ? new ValidationResponse(ValidationStatusCode.INVALID_VALUE, given) : new ValidationResponse();
            }
        };
        private static readonly CommandArgument REQUIRED_PLAYER = new CommandArgument
        {
            Parameter = "player",
            Validate = (given) =>
            {
                var found = BasePlayer.FindAwakeOrSleeping(given);
                return found == null ? new ValidationResponse(ValidationStatusCode.PLAYER_NOT_FOUND, given) : new ValidationResponse();
            }
        };
        private static readonly CommandArgument OPTIONAL_PLAYER = new CommandArgument
        {
            Parameter = "player",
            Optional = true,
            Validate = (given) =>
            {
                var found = BasePlayer.FindAwakeOrSleeping(given);
                return found == null ? new ValidationResponse(ValidationStatusCode.PLAYER_NOT_FOUND, given) : new ValidationResponse();
            }
        };
        private static readonly CommandArgument LEDGER_GUID = new CommandArgument
        {
            Parameter = "ledger guid",
            Validate = (given) =>
            {
                Guid guid;
                return !Guid.TryParse(given, out guid) ? new ValidationResponse(ValidationStatusCode.INVALID_VALUE, given) : new ValidationResponse();
            }
        };
        private static readonly CommandArgument BALANCE_AMOUNT = new CommandArgument
        {
            Parameter = "amount",
            Validate = (given) =>
            {
                int amount = 0;
                return !int.TryParse(given, out amount) || amount < 0 ? new ValidationResponse(ValidationStatusCode.INVALID_VALUE, given) : new ValidationResponse();
            }
        };
        private static readonly CommandArgument TRUEFALSE = new CommandArgument
        {
            Parameter = "true/false",
            Validate = (given) =>
            {
                bool boolean;
                return !bool.TryParse(given, out boolean) ? new ValidationResponse(ValidationStatusCode.INVALID_VALUE, given) : new ValidationResponse();
            }
        };
        private static readonly CommandArgument DATETIME = new CommandArgument
        {
            Parameter = "timestamp",
            Validate = (given) =>
            {
                DateTime date;
                return !DateTime.TryParse(given, out date) ? new ValidationResponse(ValidationStatusCode.INVALID_VALUE, given) : new ValidationResponse();
            }
        };
        #endregion

        #region Commands

        public static readonly List<CommandInfo> Commands = new List<CommandInfo>()
        {
            new CommandInfo()
            {
                Command = "help",
                Method = "CmdHelp",
                Description = "Opens the plugin help menu.",
                Permission = PermissionAdmin,
                Rank = 1
            },
            new CommandInfo()
            {
                Command = "id",
                Method = "CmdTcId",
                Description = "Returns the id of the tool cupboard at your location or at the specified player.",
                Permission = PermissionAdmin,
                Arguments = new CommandArgument[]
                {
                    OPTIONAL_PLAYER
                }
            },
            new CommandInfo()
            {
                Command = "tp",
                Method = "CmdTcTp",
                Description = "Teleports you or the specified player to the tool cupboard.",
                Permission = PermissionAdmin,
                Arguments = new CommandArgument[]
                {
                    REQUIRED_TC_ID_ARGUMENT,
                    OPTIONAL_PLAYER
                }
            },
            new CommandInfo()
            {
                Command = "offline",
                Method = "CmdTcOffline",
                Description = "Forces a tool cupboard to be considered offline. (For debugging)",
                Permission = PermissionAdmin,
                SkipOptional = true,
                Arguments = new CommandArgument[]
                {
                    OPTIONAL_TC_ID_ARGUMENT,
                    TRUEFALSE
                }
            },
            new CommandInfo()
            {
                Command = "owners",
                Method = "CmdTcOwners",
                Description = "Returns a list of the owners of the tool cupboard.",
                Permission = PermissionAdmin,
                Arguments = new CommandArgument[]
                {
                    OPTIONAL_TC_ID_ARGUMENT
                }
            },
            new CommandInfo()
            {
                Command = "owners add",
                Method = "CmdTcOwnersAdd",
                Description = "Adds the player as an owner of the tool cupboard.",
                Permission = PermissionAdmin,
                SkipOptional = true,
                Arguments = new CommandArgument[]
                {
                    OPTIONAL_TC_ID_ARGUMENT,
                    REQUIRED_PLAYER
                }
            },
            new CommandInfo()
            {
                Command = "owners remove",
                Method = "CmdTcOwnersRemove",
                Description = "Removes an owner from the tool cupboard.",
                Permission = PermissionAdmin,
                SkipOptional = true,
                Arguments = new CommandArgument[]
                {
                    OPTIONAL_TC_ID_ARGUMENT,
                    REQUIRED_PLAYER
                }
            },
            new CommandInfo()
            {
                Command = "founder",
                Method = "CmdTcFounder",
                Description = "Returns the name of the founder of the tool cupboard.",
                Permission = PermissionAdmin,
                SkipOptional = true,
                Arguments = new CommandArgument[]
                {
                    OPTIONAL_TC_ID_ARGUMENT
                }
            },
            new CommandInfo()
            {
                Command = "founder remove",
                Method = "CmdTcFounderRemove",
                Description = "Removes the founder of the tool cupboard, it will have no founder and no protection level.",
                Permission = PermissionAdmin,
                SkipOptional = true,
                Arguments = new CommandArgument[]
                {
                    OPTIONAL_TC_ID_ARGUMENT
                }
            },
            new CommandInfo()
            {
                Command = "founder set",
                Method = "CmdTcFounderSet",
                Description = "Sets the player as a founder of the tool cupboard, it will inherit their protection level.",
                Permission = PermissionAdmin,
                SkipOptional = true,
                Arguments = new CommandArgument[]
                {
                    OPTIONAL_TC_ID_ARGUMENT,
                    REQUIRED_PLAYER
                }
            },
            new CommandInfo()
            {
                Command = "protection",
                Method = "CmdTcProtection",
                Description = "Returns the protection details of the tool cupboard.",
                Permission = PermissionAdmin,
                SkipOptional = true,
                Arguments = new CommandArgument[]
                {
                    OPTIONAL_TC_ID_ARGUMENT
                }
            },
            new CommandInfo()
            {
                Command = "cost",
                Method = "CmdTcCost",
                Description = "Returns the cost details of the tool cupboard.",
                Permission = PermissionAdmin,
                SkipOptional = true,
                Arguments = new CommandArgument[]
                {
                    OPTIONAL_TC_ID_ARGUMENT
                }
            },
            new CommandInfo()
            {
                Command = "balance",
                Method = "CmdTcBalance",
                Description = "Returns the current balance of the tool cupboard.",
                Permission = PermissionAdmin,
                SkipOptional = true,
                Arguments = new CommandArgument[]
                {
                    OPTIONAL_TC_ID_ARGUMENT
                }
            },
            new CommandInfo()
            {
                Command = "balance set",
                Method = "CmdTcBalanceSet",
                Description = "Sets the tool cupboard protection balance to the amount.",
                Permission = PermissionAdmin,
                SkipOptional = true,
                Arguments = new CommandArgument[]
                {
                    OPTIONAL_TC_ID_ARGUMENT,
                    BALANCE_AMOUNT
                }
            },
            new CommandInfo()
            {
                Command = "balance add",
                Method = "CmdTcBalanceAdd",
                Description = "Adds the amount to the tool cupboard protection balance.",
                Permission = PermissionAdmin,
                SkipOptional = true,
                Arguments = new CommandArgument[]
                {
                    OPTIONAL_TC_ID_ARGUMENT,
                    BALANCE_AMOUNT
                }
            },
            new CommandInfo()
            {
                Command = "balance remove",
                Method = "CmdTcBalanceRemove",
                Description = "Removes the amount from the tool cupboard protection balance.",
                Permission = PermissionAdmin,
                SkipOptional = true,
                Arguments = new CommandArgument[]
                {
                    OPTIONAL_TC_ID_ARGUMENT,
                    BALANCE_AMOUNT
                }
            },
            new CommandInfo()
            {
                Command = "status",
                Method = "CmdTcStatus",
                Description = "Returns the protection status and reason of the tool cupboard.",
                Permission = PermissionAdmin,
                SkipOptional = true,
                Arguments = new CommandArgument[]
                {
                    OPTIONAL_TC_ID_ARGUMENT
                }
            },
            new CommandInfo()
            {
                Command = "restore",
                Method = "CmdTcRestore",
                Description = "Returns the protection balance of the tool cupboard to the specified ledger snapshot guid.",
                Permission = PermissionAdmin,
                SkipOptional = true,
                Arguments = new CommandArgument[]
                {
                    OPTIONAL_TC_ID_ARGUMENT,
                    LEDGER_GUID
                }
            },
            new CommandInfo()
            {
                Command = "ledger save",
                Method = "CmdTcLedgerSave",
                Description = "Saves all ledgers for all tool cupboards instead of waiting until the next server save.",
                Permission = PermissionAdmin
            },
            new CommandInfo()
            {
                Command = "ledger clear",
                Method = "CmdTcLedgerClear",
                Description = "Clears all ledgers for all tool cupboards. The current balance will not be changed but you will no longer have a ledger history.",
                Permission = PermissionAdmin
            },
            new CommandInfo()
            {
                Command = "ledger rollback",
                Method = "CmdTcLedgerRollback",
                Description = "Restores all tool cupboards to the ledger entry to the latest entry before a timestamp given as month/day/year hour:min:second.",
                Permission = PermissionAdmin,
                Arguments = new CommandArgument[]
                {
                    DATETIME
                }
            },
            new CommandInfo()
            {
                Command = "changelog",
                Method = "CmdTcChangelog",
                Description = "Displays a list of changes in the latest patch.",
                Permission = PermissionAdmin
            },
        };
        #endregion

        #region Helpers
        private BasePlayer GetPlayerOrTarget(IPlayer player, string command, string[] args, int index = 0, int length = 1)
        {
            BasePlayer caller = player.Object as BasePlayer;
            if (player.IsServer && args.Length < length)
            {
                Message(caller, "command player not found", "<not given>");
                return null;
            }
            BasePlayer basePlayer = caller;
            if (args.Length >= length)
            {
                basePlayer = BasePlayer.FindAwakeOrSleeping(args[index]);
            }
            return basePlayer;
        }

        private BaseEntity GetEntityFromId(string id)
        {
            return ProtectedCupboardManager.GetByID(ulong.Parse(id)).BaseEntity;
        }

        private ProtectedEntity GetTcFromId(string id)
        {
            return ProtectedCupboardManager.GetByID(ulong.Parse(id));
        }

        private ProtectedEntity GetTcFromIdOrPlayer(IPlayer player, string command, string[] args, int indexOfTcId = 0, int totalExpectedLengthOfArgs = 1)
        {
            BasePlayer caller = player.Object as BasePlayer;
            if (player.IsServer && args.Length < totalExpectedLengthOfArgs)
            {
                Message(caller, "command invalid value", args[0]);
                return null;
            }
            else if (args.Length < totalExpectedLengthOfArgs)
            {
                var priv = GetProtectedEntity(caller);
                if (priv == null) { return null; }
                return ProtectedCupboardManager.GetByID(priv.net.ID.Value);
            }
            var id = ulong.Parse(args[indexOfTcId]);
            return ProtectedCupboardManager.GetByID(id);
        }
        #endregion

        #region Controller
        private void CmdController(IPlayer player, string command, string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    args = new string[] { "help" };
                }
                CommandInfo commandInfo = null;
                if (args.Length >= 2)
                {
                    commandInfo = Commands.OrderByDescending(x => x.CommandWordCount).FirstOrDefault(x => x.Command == string.Join(" ", new[] { args[0], args[1] }));
                }
                if (commandInfo == null)
                {
                    commandInfo = Commands.FirstOrDefault(x => x.Command == args[0]);
                }
                if (commandInfo == null)
                {
                    CmdHelp(player, command, args);
                    return;
                }
                // Permission
                if (!player.IsServer)
                {
                    foreach (var perm in commandInfo.Permissions)
                    {
                        if (!permission.UserHasPermission(player.Id, perm))
                        {
                            Message(player.Object as BasePlayer, "command no permission", perm);
                            return;
                        }
                    }
                }
                args = args.Skip(commandInfo.CommandWordCount).ToArray();
                // Validation
                var resp = commandInfo.Validate(player.IsServer, args);
                if (resp.IsValid)
                {
                    if (player.IsServer && args.Length < commandInfo.TotalArgCount)
                    {
                        Message(player.Object as BasePlayer, "command all args required");
                        Message(player.Object as BasePlayer, "command usage", config.Commands.Admin, commandInfo.Command, commandInfo.ArgString);
                        return;
                    }
                    commandInfo.Execute(player, command, args);
                }
                else
                {
                    var message = Lang("command usage", player.Object as BasePlayer, config.Commands.Admin, commandInfo.Command, commandInfo.ArgString);
                    switch (resp.StatusCode)
                    {
                        case ValidationStatusCode.PLAYER_NOT_FOUND:
                            message = Lang("command player not found", player.Id, resp.Data);
                            break;
                        case ValidationStatusCode.INVALID_VALUE:
                        case ValidationStatusCode.VALUE_NOT_ALLOWED:
                            message = Lang("command invalid value", player.Id, resp.Data);
                            break;
                    }
                    MessageNonLocalized(player.Object as BasePlayer, message);
                }
            }
            catch (Exception)
            {
                Message(player.Object as BasePlayer, "command error");
            }
        }
        #endregion

        #region Methods
        private void CmdHelp(IPlayer player, string command, string[] args)
        {
            if (player.IsServer)
            {
                Puts("\n" + string.Join("\n", Commands.Select(x => $"{config.Commands.Admin} {x.Command} {x.ArgString}".PadRight(60) + x.Description)));
            }
            else
            {
                ShowHelp(player.Object as BasePlayer);
            }
        }

        private void CmdTcId(IPlayer player, string command, string[] args)
        {
            var basePlayer = GetPlayerOrTarget(player, command, args);
            if (basePlayer == null) { return; }
            var priv = GetProtectedEntity(basePlayer);
            if (priv == null)
            {
                Message(player.Object as BasePlayer, "command no tc", basePlayer.displayName);
            }
            else
            {
                Message(player.Object as BasePlayer, "command id", priv.net.ID);
            }
        }

        private void CmdTcTp(IPlayer player, string command, string[] args)
        {
            var priv = GetEntityFromId(args[0]);
            var basePlayer = GetPlayerOrTarget(player, command, args, 1, 2);
            basePlayer.MovePosition(priv.transform.position);
            Message(player.Object as BasePlayer, "command success", priv.net.ID);
        }

        private void CmdTcOffline(IPlayer player, string command, string[] args)
        {
            var tc = GetTcFromIdOrPlayer(player, command, args, 0, 2);
            if (tc == null) { Message(player.Object as BasePlayer, "command no tc found"); return; }
            tc.ForceOffline = bool.Parse(args.Last());
            UpdateOnlineOwners(tc);
            Message(player.Object as BasePlayer, "command success", tc.ID, string.Join("\n", tc.Owners.Select(x => x.Name)));
            Message(player.Object as BasePlayer, "command status", tc.ID, tc.Status, tc.ReasonAndTimeRemaining);
        }

        private void CmdTcOwners(IPlayer player, string command, string[] args)
        {
            var tc = GetTcFromIdOrPlayer(player, command, args, 0, 1);
            if (tc == null) { Message(player.Object as BasePlayer, "command no tc found"); return; }
            Message(player.Object as BasePlayer, "command owners", tc.ID, string.Join("\n", tc.Owners.Select(x => x.Name)));
        }

        private void CmdTcOwnersAdd(IPlayer player, string command, string[] args)
        {
            var tc = GetTcFromIdOrPlayer(player, command, args, 0, 2);
            if (tc == null) { Message(player.Object as BasePlayer, "command no tc found"); return; }
            var basePlayer = BasePlayer.FindAwakeOrSleeping(args.Last());
            if (!tc.OwnerUserIds.Contains(basePlayer.UserId()))
            {
                tc.OwnerUserIds.Add(basePlayer.UserId());
                UpdateOwners(tc);
            }
            Message(player.Object as BasePlayer, "command success");
            Message(player.Object as BasePlayer, "command owners", tc.ID, tc.Owners.Select(x => x.Name).ToSentence());
        }

        private void CmdTcOwnersRemove(IPlayer player, string command, string[] args)
        {
            var tc = GetTcFromIdOrPlayer(player, command, args, 0, 2);
            if (tc == null) { Message(player.Object as BasePlayer, "command no tc found"); return; }
            var basePlayer = BasePlayer.FindAwakeOrSleeping(args.Last());
            if (tc.IsAuthed(basePlayer))
            {
                Message(player.Object as BasePlayer, "command cannot remove auth");
                return;
            }
            if (tc.OwnerUserIds.Contains(basePlayer.UserId()))
            {
                tc.OwnerUserIds.Remove(basePlayer.UserId());
                UpdateOwners(tc);
            }
            Message(player.Object as BasePlayer, "command success");
            Message(player.Object as BasePlayer, "command owners", tc.ID, tc.Owners.Select(x => x.Name).ToSentence());
        }

        private void CmdTcFounder(IPlayer player, string command, string[] args)
        {
            var tc = GetTcFromIdOrPlayer(player, command, args);
            if (tc == null) { Message(player.Object as BasePlayer, "command no tc found"); return; }
            Message(player.Object as BasePlayer, "command founder", tc.ID, tc.FounderDisplayName, tc.HighestProtectionLevel.Rank);
        }

        private void CmdTcFounderSet(IPlayer player, string command, string[] args)
        {
            var tc = GetTcFromIdOrPlayer(player, command, args, 0, 2);
            if (tc == null) { Message(player.Object as BasePlayer, "command no tc found"); return; }
            var basePlayer = BasePlayer.FindAwakeOrSleeping(args.Last());
            tc.SetEntityOwner(basePlayer.UserId());
            UpdateOwners(tc);
            Message(player.Object as BasePlayer, "command success");
            Message(player.Object as BasePlayer, "command founder", tc.ID, tc.FounderDisplayName, tc.HighestProtectionLevel.Rank);
        }

        private void CmdTcFounderRemove(IPlayer player, string command, string[] args)
        {
            var tc = GetTcFromIdOrPlayer(player, command, args);
            if (tc == null) { Message(player.Object as BasePlayer, "command no tc found"); return; }
            tc.SetEntityOwner(null);
            UpdateOwners(tc);
            Message(player.Object as BasePlayer, "command success");
            Message(player.Object as BasePlayer, "command founder", tc.ID, tc.FounderDisplayName, tc.HighestProtectionLevel.Rank);
        }

        private void CmdTcProtection(IPlayer player, string command, string[] args)
        {
            var tc = GetTcFromIdOrPlayer(player, command, args);
            if (tc == null) { Message(player.Object as BasePlayer, "command no tc found"); return; }
            Message(player.Object as BasePlayer, "command protection", tc.ID, tc.CurrentProtectionPercent, tc.OnlineProtectionPercent, tc.OfflineProtectionPercent, tc.HighestProtectionLevel.Rank);
        }

        private void CmdTcCost(IPlayer player, string command, string[] args)
        {
            var tc = GetTcFromIdOrPlayer(player, command, args);
            if (tc == null) { Message(player.Object as BasePlayer, "command no tc found"); return; }
            Message(player.Object as BasePlayer, "command cost", tc.ID, tc.BaseCostPerHour, tc.OwnerCostPerHour, tc.Owners.Count, tc.BuildingCostPerHour, tc.FoundationCount, FormatCurrency(player.Id, tc.TotalProtectionCostPerHour));
        }

        private void CmdTcBalance(IPlayer player, string command, string[] args)
        {
            var tc = GetTcFromIdOrPlayer(player, command, args);
            if (tc == null) { Message(player.Object as BasePlayer, "command no tc found"); return; }
            Message(player.Object as BasePlayer, "command balance", tc.ID, FormatCurrency(player.Id, tc.Balance));
        }

        private void CmdTcBalanceSet(IPlayer player, string command, string[] args)
        {
            var tc = GetTcFromIdOrPlayer(player, command, args, 0, 2);
            if (tc == null) { Message(player.Object as BasePlayer, "command no tc found"); return; }
            var amt = float.Parse(args.Last());
            if (tc.Balance <= 0 && tc.Enabled && amt > 0)
            {
                tc.SkipNextProtectionStartDelay = true;
            }
            ClearProtectionBalance(tc, false);
            UpdateProtectionBalance(tc, float.Parse(args.Last()), BalanceLedgerReason.Command);
            Message(player.Object as BasePlayer, "command success");
            Message(player.Object as BasePlayer, "command balance", tc.ID, FormatCurrency(player.Id, tc.Balance));
        }

        private void CmdTcBalanceAdd(IPlayer player, string command, string[] args)
        {
            var tc = GetTcFromIdOrPlayer(player, command, args, 0, 2);
            if (tc == null) { Message(player.Object as BasePlayer, "command no tc found"); return; }
            var amt = float.Parse(args.Last());
            if (tc.Balance <= 0 && tc.Enabled && amt > 0)
            {
                tc.SkipNextProtectionStartDelay = true;
            }
            UpdateProtectionBalance(tc, amt, BalanceLedgerReason.Command);
            Message(player.Object as BasePlayer, "command success");
            Message(player.Object as BasePlayer, "command balance", tc.ID, FormatCurrency(player.Id, tc.Balance));
        }

        private void CmdTcBalanceRemove(IPlayer player, string command, string[] args)
        {
            var tc = GetTcFromIdOrPlayer(player, command, args, 0, 2);
            if (tc == null) { Message(player.Object as BasePlayer, "command no tc found"); return; }
            UpdateProtectionBalance(tc, -float.Parse(args.Last()), BalanceLedgerReason.Command);
            Message(player.Object as BasePlayer, "command success");
            Message(player.Object as BasePlayer, "command balance", tc.ID, FormatCurrency(player.Id, tc.Balance));
        }
        private void CmdTcRestore(IPlayer player, string command, string[] args)
        {
            var tc = GetTcFromIdOrPlayer(player, command, args, 0, 1);
            if (tc == null) { Message(player.Object as BasePlayer, "command no tc found"); return; }
            if (BalanceLedger.Restore(tc, args.Last()))
            {
                Message(player.Object as BasePlayer, "command success");
                Message(player.Object as BasePlayer, "command balance", tc.ID, FormatCurrency(player.Id, tc.Balance));
            }
            else
            {
                Message(player.Object as BasePlayer, "command cannot restore", args.Last());
            }
        }

        private void CmdTcLedgerSave(IPlayer player, string command, string[] args)
        {
            BalanceLedger.SaveAll();
            Message(player.Object as BasePlayer, "command success");
        }

        private void CmdTcLedgerClear(IPlayer player, string command, string[] args)
        {
            BalanceLedger.ClearAll();
            BalanceLedger.SaveAll();
            Message(player.Object as BasePlayer, "command success");
        }

        private void CmdTcLedgerRollback(IPlayer player, string command, string[] args)
        {
            var dateTime = DateTime.Parse(args.Last());
            var successes = 0;
            foreach(var tc in ProtectedCupboardManager.ProtectedCupboards.Values)
            {
                if (BalanceLedger.Restore(tc, dateTime))
                {
                    successes++;
                }
            }
            Message(player.Object as BasePlayer, "command success");
            Message(player.Object as BasePlayer, "command rollback", dateTime.ToString(), successes);
        }

        private void CmdTcStatus(IPlayer player, string command, string[] args)
        {
            var tc = GetTcFromIdOrPlayer(player, command, args);
            if (tc == null) { Message(player.Object as BasePlayer, "command no tc found"); return; }
            Message(player.Object as BasePlayer, "command status", tc.ID, tc.Status, tc.ReasonAndTimeRemaining);
        }

        private void CmdTcChangelog(IPlayer player, string command, string[] args)
        {
            Message(player.Object as BasePlayer, "command changelog", PLUGIN.Version.ToString(), $"\n-{string.Join("\n-", CHANGELOG)}");
        }
        #endregion
    }
}

namespace Oxide.Plugins
{
    partial class RaidProtection
    {
		private Configuration config;

		private class Configuration
        {
			[JsonProperty(PropertyName = "Enable Logging")]
			public bool EnableLogging = false;

			[JsonProperty(PropertyName = "Enable Ledger")]
			public bool EnableLedger = true;

			[JsonProperty(PropertyName = "Enable Console Messages")]
			public bool EnableConsoleMessages = true;

			[JsonProperty(PropertyName = "Chat Message Icon ID")]
			public ulong ChatMessageIconId = 0;

			[JsonProperty(PropertyName = "Protection Tabs Offset", NullValueHandling = NullValueHandling.Ignore)]
            public int? ProtectionTabsOffset = 0; // replaced by new property

            [JsonProperty(PropertyName = "Commands")]
			public CommandsConfig Commands = new CommandsConfig();

			[JsonProperty(PropertyName = "Protection Settings")]
			public ProtectionConfig Protection = new ProtectionConfig();

			[JsonProperty(PropertyName = "Image Settings")]
			public ImagesConfig Images = new ImagesConfig();

			[JsonProperty(PropertyName = "Indicator Settings")]
			public IndicatorConfig Indicator = new IndicatorConfig();

			[JsonProperty(PropertyName = "Custom Status Framework Settings")]
			public CustomStatusFrameworkConfig CustomStatusFramework = new CustomStatusFrameworkConfig();

			[JsonProperty(PropertyName = "Simple Status Settings")]
			public SimpleStatusConfig SimpleStatus = new SimpleStatusConfig();

			[JsonProperty(PropertyName = "Notify Settings")]
			public NotifyConfig Notify = new NotifyConfig();

			[JsonProperty(PropertyName = "Plugin Integration")]
			public IntegrationConfig Integration = new IntegrationConfig();

			[JsonProperty(PropertyName = "Version")]
			public VersionNumber Version { get; set; } = new VersionNumber(0, 0, 0);

			public class ProtectionConfig
			{
				[JsonProperty(PropertyName = "Protected entities")]
				public ProtectedEntitiesConfig ProtectedEntities = new ProtectedEntitiesConfig();

				[JsonProperty(PropertyName = "Protected from")]
				public ProtectedFromConfig ProtectedFrom = new ProtectedFromConfig();

				[JsonProperty(PropertyName = "Protection levels")]
				public ProtectionLevel[] ProtectionLevels = new ProtectionLevel[]
				{
					new ProtectionLevel {
						Rank = 1,
						OnlineProtectionPercent = 100f,
						OfflineProtectionPercent = 100f,
						CostPerDamageProtected = 0f,
						HourlyBaseCost = 10,
					},
				};
				[JsonProperty(PropertyName = "Admin owners removed when deauthorized")]
				public bool RemoveAdminOwners { get; set; } = true;
				[JsonProperty(PropertyName = "Award remaining balance when cupboard destroyed")]
				public bool AwardRemainingBalance { get; set; } = true;

				[JsonProperty(PropertyName = "Allow max deposit")]
				public bool AllowMaxDeposit { get; set; } = true;
				[JsonProperty(PropertyName = "Allow balance withdraw")]
				public bool AllowBalanceWithdraw { get; set; } = true;

				[JsonProperty(PropertyName = "Allow protection pause")]
				public bool AllowProtectionPause { get; set; } = true;

				[JsonProperty(PropertyName = "Panel Tabs")]
				public PanelTabsConfig PanelTabs { get; set; } = new PanelTabsConfig();

                [JsonProperty(PropertyName = "Currency item (if not using ServerRewards or Economics)")]
				public string CurrencyItem { get; set; } = "scrap";
				[JsonProperty(PropertyName = "Protect twig")]
				public bool ProtectTwig { get; set; } = true;
			}

			public class CommandsConfig
			{
				[JsonProperty(PropertyName = "Admin")]
				public string Admin { get; set; } = "tc";

				[JsonProperty(PropertyName = "Protection")]
				public string Protection { get; set; } = "pro";

				[JsonProperty(PropertyName = "Levels")]
				public string Levels { get; set; } = "lev";
			}

			public class ProtectedFromConfig
			{
				[JsonProperty(PropertyName = "Authorized Players")]
				public bool AuthorizedPlayers { get; set; } = false;

				[JsonProperty(PropertyName = "Unauthorized Players")]
				public bool UnauthorizedPlayers { get; set; } = true;

				[JsonProperty(PropertyName = "Attack Heli")]
				public bool AttackHeli { get; set; } = true;

				[JsonProperty(PropertyName = "NPCs")]
				public bool NPCs { get; set; } = true;
			}

			public class ProtectedEntitiesConfig
            {
				[JsonProperty(PropertyName = "Buildings")]
				public bool Buildings { get; set; } = true;

				[JsonProperty(PropertyName = "Deployables")]
				public bool Containers { get; set; } = true;
				[JsonProperty(PropertyName = "Traps")]
				public bool Traps { get; set; } = true;

				[JsonProperty(PropertyName = "Loot Nodes")]
				public bool LootNodes { get; set; } = false;

				[JsonProperty(PropertyName = "Authed Players")]
				public bool AuthedPlayers { get; set; } = false;

				[JsonProperty(PropertyName = "Unauthed Players")]
				public bool UnauthedPlayers { get; set; } = false;

				[JsonProperty(PropertyName = "NPCs")]
				public bool NPCs { get; set; } = false;

				[JsonProperty(PropertyName = "Animals")]
				public bool Animals { get; set; } = false;

				[JsonProperty(PropertyName = "Vehicles")]
				public bool Vehicles { get; set; } = false;

				[JsonProperty(PropertyName = "Horses")]
				public bool Horses { get; set; } = false;

				[JsonProperty(PropertyName = "Electrical")]
				public bool Electrical { get; set; } = true;
			}

			public class IntegrationConfig
			{
				[JsonProperty(PropertyName = "Economics")]
				public bool Economics { get; set; } = false;
				[JsonProperty(PropertyName = "Server Rewards")]
				public bool ServerRewards { get; set; } = false;
				[JsonProperty(PropertyName = "Custom Status Framework")]
				public bool CustomStatusFramework { get; set; } = false;
				[JsonProperty(PropertyName = "Simple Status")]
				public bool SimpleStatus { get; set; } = false;
				[JsonProperty(PropertyName = "Notify")]
				public bool Notify { get; set; } = false;
				[JsonProperty(PropertyName = "Clans")]
				public bool Clans { get; set; } = false;
				[JsonProperty(PropertyName = "Abandoned Bases")]
				public bool AbandonedBases { get; set; } = false;
				[JsonProperty(PropertyName = "Skill Tree")]
				public bool SkillTree { get; set; } = false;
			}

			public class NotifyConfig
			{
				[JsonProperty(PropertyName = "Protected Type")]
				public int ProtectedType = 0;

				[JsonProperty(PropertyName = "Unprotected Type")]
				public int UnprotectedType = 0;
			}

			public class PanelTabsConfig
			{
                [JsonProperty(PropertyName = "Offset X")]
                public int OffsetX = 0;

				[JsonProperty(PropertyName = "Offset Y")]
				public int OffsetY = 0;

				[JsonProperty(PropertyName = "Tab Width")]
				public int TabWidth = 200;
            }

			public class CustomStatusFrameworkConfig
			{
				[JsonProperty(PropertyName = "Popup Attack Indicator")]
				public bool ShowAttackedIndicator { get; set; } = true;
				[JsonProperty(PropertyName = "Persistent Status For Owners")]
				public bool ShowStatusForOwners { get; set; } = true;
				[JsonProperty(PropertyName = "Persistent Status For Non Owners")]
				public bool ShowStatusForNonOwners { get; set; } = false;
				[JsonProperty(PropertyName = "Popup Status When Hammer Equipped")]
				public bool ShowWhenHammerEquipped { get; set; } = true;
			}

			public class SimpleStatusConfig
			{
				[JsonProperty(PropertyName = "Text Value")]
				public string TextValue { get; set; } = "duration"; // duration, percent, none
				[JsonProperty(PropertyName = "Always Show For Owners")]
				public bool AlwaysShowForOwners { get; set; } = false;
				[JsonProperty(PropertyName = "Always Show For Non Owners")]
				public bool AlwaysShowForNonOwners { get; set; } = false;
				[JsonProperty(PropertyName = "Show When Holding Hammer")]
				public bool ShowWhenHoldingHammer { get; set; } = true;
				[JsonProperty(PropertyName = "Show When Attacking")]
				public bool ShowWhenAttacking { get; set; } = true;
				[JsonProperty(PropertyName = "Show Balance Bleed When Attacking")]
				public bool ShowBalanceBleedWhenAttacking { get; set; } = true;
			}

			public class IndicatorConfig
            {
				public bool Enabled = true;
				public bool ShowBalanceDeducted = true;
				public int FontSize = 18;
				public string AnchorMin = "0.94 0.9";
				public string AnchorMax = "0.94 0.9";
				public string OffsetMin = "0 0";
				public string OffsetMax = "64 64";
			}

			public class ImagesConfig
            {
				[JsonProperty(PropertyName = "Status Protected")]
				public string StatusProtected { get; set; } = "https://i.ibb.co/f4qRcGk/protected.png";
				[JsonProperty(PropertyName = "Status Unprotected")]
				public string StatusUnprotected { get; set; } = "https://i.ibb.co/8KHF4Bz/unprotected.png";
				[JsonProperty(PropertyName = "Status Info")]
				public string StatusInfo { get; set; } = "https://i.ibb.co/f9KSv7G/settings.png";
				[JsonProperty(PropertyName = "Status Toggle")]
				public string StatusToggle { get; set; } = "https://i.ibb.co/FmBxdDf/toggle.png";
				[JsonProperty(PropertyName = "Status Refresh")]
				public string StatusRefresh { get; set; } = "https://i.ibb.co/r3w1NPV/refresh.png";
				[JsonProperty(PropertyName = "Info Owners")]
				public string InfoOwners { get; set; } = "https://i.ibb.co/mChVXHJ/key.png";
				[JsonProperty(PropertyName = "Info Costs")]
				public string InfoCosts { get; set; } = "https://i.ibb.co/Nrd48VW/money.png";
				[JsonProperty(PropertyName = "Info Check")]
				public string InfoCheck { get; set; } = "https://i.ibb.co/XbThxdm/check.png";
				[JsonProperty(PropertyName = "Info Cross")]
				public string InfoCross { get; set; } = "https://i.ibb.co/6r2mx5t/cross.png";
			}
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			var recommended = $"It is recommended to backup your current oxide/config/{Name}.json and oxide/lang/en/{Name}.json files and remove them to generate fresh ones.";
			var usingDefault = "Overriding configuration with default values to avoid errors.";
			var containsError = "Your configuration file contains an error.";

			try
			{
				config = Config.ReadObject<Configuration>();
				if (config == null) { throw new Exception(); }
				//else if (config.Version.Major != Version.Major || config.Version.Minor != Version.Minor) throw new NotSupportedException();
				else if (config.Version.Major != Version.Major || config.Version.Minor != Version.Minor)
                {
					PrintWarning($"CONFIG UPDATE: Updating config from v{config.Version.Major}.{config.Version.Minor}.{config.Version.Patch} to v{Version.Major}.{Version.Minor}.{Version.Patch}. It is recommended that you verify that all configuration settings for this plugin are still correct.");
					config.Version = Version;
				}
				else if (config.Protection.ProtectionLevels.Any(x => x.Rank <= 0)) { throw new InvalidProtectionLevelException(); }
				else if (config.Protection.ProtectionLevels.Select(x => x.Rank).Count() != config.Protection.ProtectionLevels.Select(x => x.Rank).Distinct().Count()) { throw new DuplicateProtectionLevelException(); }
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
			catch(InvalidProtectionLevelException)
            {
				if (ShowConfigWarnings)
                {
					PrintError($"{containsError} Protection level ranks must be 1 or greater.");
					PrintWarning(usingDefault);
				}
				LoadDefaultConfig();
			}
			catch (DuplicateProtectionLevelException)
			{
				if (ShowConfigWarnings)
				{
					PrintError($"{containsError} Duplicate protection level ranks are not allowed.");
					PrintWarning(usingDefault);
				}
				LoadDefaultConfig();
			}
			catch (Exception)
			{
				if (ShowConfigWarnings)
                {
					PrintError($"{containsError} {recommended}");
					PrintWarning(usingDefault);
				}
				LoadDefaultConfig();
			}
			// Carryover renamed properties
			bool madeChanges = false;
			if (config.ProtectionTabsOffset != null)
			{
				if (config.ProtectionTabsOffset != 0)
				{
                    config.Protection.PanelTabs.OffsetY = config.ProtectionTabsOffset.Value;
                }
				config.ProtectionTabsOffset = null;
				madeChanges = true;
			}
			if (config.ProtectionTabsOffset == 0)
			{
				config.ProtectionTabsOffset = null;
				madeChanges = true;
			}
			if (madeChanges)
			{
                SaveConfig();
            }
			ShowConfigWarnings = true;
		}

		protected override void SaveConfig() => Config.WriteObject(config);

		protected override void LoadDefaultConfig()
		{
			config = new Configuration();
			config.Version = new VersionNumber(Version.Major, Version.Minor, Version.Patch);
		}

		class InvalidProtectionLevelException : Exception { }

		class DuplicateProtectionLevelException : Exception { }
	}
}

namespace Oxide.Plugins
{
    partial class RaidProtection
    {
        #region Protected Cupboard
        /// <summary>
        /// Initializes a new protected cupboard
        /// </summary>
        private bool AddProtectedCupboard(BaseEntity priv, ProtectedEntity existing = null)
        {
            if (IsNull(priv) || IsNpcBase(priv)) return false;
            var tc = ProtectedCupboardManager.InitProtectedCupboard(priv);
            var success = tc != null;
            if (success && existing != null)
            {
                LogAction(tc.EntityID, "Loaded existing tool cupboard");
                tc.ProtectionLevelRank = existing.ProtectionLevelRank;
                tc.OwnerUserIds = existing.OwnerUserIds;
                tc.Balance = existing.Balance;
                tc.Enabled = existing.Enabled;
                tc.DisableProtectionFromExceedingFounderLimit = existing.DisableProtectionFromExceedingFounderLimit;
                if (tc.Status == ProtectionStatus.Pending)
                {
                    tc.Status = ProtectionStatus.Protected;
                }
            }
            else if (success && tc != null && existing == null)
            {
                LogAction(tc.EntityID, "TC initially added");
            }
            if (tc == null) { return false; }
            // Audit the new tc
            tc.ClearCache(founderOwnersAndProtectionLevel: true, priv: true, building: true, costs: true);
            tc.AuditOwners();
            tc.AuditFounder();
            tc.AuditProtectionLevel();
            BalanceLedger.Update(tc, BalanceLedgerReason.Initial);
            if (success)
            {
                Oxide.Core.Interface.Call("OnProtectionInit", tc.BaseEntity, tc.Status.ToString());
                StartOrStopProtection(tc, () =>
                {
                    if (existing != null)
                    {
                        LogAction(tc.EntityID, $"Initial status is {tc.Status} with reason {tc.Reason} and balance {tc.Balance}");
                    }
                    tc.SkipNextProtectionStartDelay = false;
                });
            }
            return success;
        }

        /// <summary>
        /// Removes an existing protected cupboard
        /// </summary>
        private void RemoveProtectedCupboard(ProtectedEntity tc)
        {
            if (IsNull(tc)) return;
            StopProtection(tc);
            foreach (var bp in tc.CurrentlyViewingInfoPanel.ToList())
            {
                CloseProtectedCupboard(tc, bp);
            }
            NextTick(() =>
            {
                ProtectedCupboardManager.RemoveProtectedCupboard(tc.EntityID.Value);
            });
        }
        #endregion

        #region Protection
        private void StartOrStopProtection(ProtectedEntity tc, Action callback = null)
        {
            if (IsNull(tc)) return;
            var valid = tc.HasFounder && tc.HasRemainingProtection && tc.Enabled && ((tc.HasOnlineProtection && tc.HasOwnersOnline) || (tc.HasOfflineProtection && !tc.HasOwnersOnline));
            if (!tc.IsProtected && valid || (tc.IsProtected && !tc.HasPendingProtectionTimer && tc._protectionTimerReason == StatusReason.PendingOfflineProtection))
            {
                StartProtection(tc, callback);
            }
            else if (tc.IsProtected && !valid)
            {
                StopProtection(tc, callback);
            }
            else
            {
                callback?.Invoke();
            }
        }
        /// <summary>
        /// Starts protection for a protected cupboard
        /// </summary>
        private void StartProtection(ProtectedEntity tc, Action callback = null)
        {
            if (IsNull(tc)) return;
            LogAction(tc.EntityID, $"Starting protection in {tc.RemainingProtectionDelaySeconds}s {(tc.SkipNextProtectionStartDelay ? "(Skipped)" : string.Empty)}");
            tc.StartProtectionDelayTimer(tc.RemainingProtectionDelaySeconds, () =>
            {
                if (IsNull(tc)) return;
                var success = tc.HighestProtectionLevel != ProtectionLevel.NONE && tc.HasRemainingProtection;
                tc.StopProtectionDelayTimer();
                tc.SkipNextProtectionStartDelay = false;
                if (success)
                {
                    UpdateProtectionStatus(tc, ProtectionStatus.Protected);
                    StartCollectionLoop(tc);
                }
                callback?.Invoke();
            });
            //if (!tc.HasRemainingProtectionDelay)
            //{
            //    action.Invoke();
            //}
            //else if (tc.HasRemainingProtectionDelay && !tc.)
            //if (tc.HasPendingProtectionTimer)
            //{
            //    return; // Don't start it twice
            //}
            //tc.StopProtectionDelayTimer();
            //tc.PendingProtectionTimer = timer.In(tc.RemainingProtectionDelaySeconds, () =>
            //{
            //    if (IsNull(tc)) return;
            //    var success = tc.HighestProtectionLevel != ProtectionLevel.NONE && tc.HasRemainingProtection;
            //    tc.PendingProtectionTimer = null;
            //    tc.SkipNextProtectionStartDelay = false;
            //    if (success)
            //    {
            //        UpdateProtectionStatus(tc, ProtectionStatus.Protected);
            //        StartCollectionLoop(tc);
            //    }
            //    callback?.Invoke();
            //});
        }

        /// <summary>
        /// Stops protection for a protected cupboard
        /// </summary>
        private void StopProtection(ProtectedEntity tc, Action callback = null)
        {
            if (IsNull(tc)) return;
            LogAction(tc.EntityID, "Stopping protection");
            StopCollectionLoop(tc);
            UpdateProtectionStatus(tc, ProtectionStatus.Unprotected);
            var success = true;
            callback?.Invoke();
        }
        #endregion

        #region Collection Loop
        /// <summary>
        /// Called to collect the due amount from a protected cupboards balance
        /// </summary>
        protected void CollectProtectionCost(ProtectedEntity tc)
        {
            if (IsNull(tc)) return;
            if (!tc.IsProtected)
            {
                LogAction(tc.EntityID, $"Stopping collection since it is no longer protected");
                StopCollectionLoop(tc);
                return;
            }
            var cost = tc.TotalCostPerInterval(CollectionManager.COLLECTION_INTERVAL);
            bool success = tc.Balance >= cost;
            if (success)
            {
                UpdateProtectionBalance(tc, -tc.TotalCostPerInterval(CollectionManager.COLLECTION_INTERVAL), BalanceLedgerReason.DoNotRecord);
            }
            tc.NumTimesCollected++;
            if (success)
            {
                ContinueCollectionLoop(tc);
            }
            else
            {
                ClearProtectionBalance(tc);
            }
        }

        /// <summary>
        /// Starts the loop to continuously collect the due protection amount
        /// </summary>
        private void StartCollectionLoop(ProtectedEntity tc)
        {
            if (IsNull(tc)) return;
            LogAction(tc.EntityID, $"Starting collection loop balance is {tc.Balance} cost per interval is {tc.TotalCostPerInterval(CollectionManager.COLLECTION_INTERVAL)}");
            BalanceLedger.Update(tc, BalanceLedgerReason.CollectionStarted);
            CollectionManager.RunCollectionLoop(tc);
        }

        /// <summary>
        /// Continues an already started collection loop
        /// </summary>
        private void ContinueCollectionLoop(ProtectedEntity tc)
        {
            if (IsNull(tc)) return;
            CollectionManager.RunCollectionLoop(tc);
        }

        /// <summary>
        /// Stops an active collection loop
        /// </summary>
        private void StopCollectionLoop(ProtectedEntity tc)
        {
            if (IsNull(tc)) return;
            LogAction(tc.EntityID, $"Stopped collection loop that ran {tc.NumTimesCollected} times the balance is {tc.Balance}");
            if (tc.NumTimesCollected > 0)
            {
                BalanceLedger.Update(tc, BalanceLedgerReason.CollectionStopped);
            }
            tc.NumTimesCollected = 0;
            CollectionManager.StopCollectionLoop(tc);
        }
        #endregion

        #region Owners
        private void AdminOwnerDeauthorized(ProtectedEntity tc, BasePlayer basePlayer)
        {
            if (IsNull(tc) || IsNull(basePlayer)) return;
            Message(basePlayer, "message deauthorize admin");
            if (tc.FounderUserId == basePlayer.UserId())
            {
                tc.SetEntityOwner(null);
            }
            tc.OwnerUserIds.Remove(basePlayer.UserId());
            tc.ClearCache(founderOwnersAndProtectionLevel: true, costs: true);
            tc.SkipNextProtectionStartDelay = true;
        }

        private void OwnerAuthorized(ProtectedEntity tc, BasePlayer basePlayer)
        {
            if (IsNull(tc) || IsNull(basePlayer)) return;
            var isNew = !tc.OwnerUserIds.Contains(basePlayer.UserId());
            if (isNew)
            {
                tc.OwnerUserIds.Add(basePlayer.UserId());
            }
            UpdateOwners(tc);
            if (IsAdmin(basePlayer.IPlayer) && config.Protection.RemoveAdminOwners)
            {
                Message(basePlayer, "message authorize admin");
            }
            LogAction(tc.EntityID, $"Player {basePlayer.displayName} has authorized on the TC");
        }

        private void UpdateOwners(ProtectedEntity tc, bool autostart = true)
        {
            if (IsNull(tc)) return;
            tc.SimpleStatusNeedsUpdate = true;
            tc.ClearCache(founderOwnersAndProtectionLevel: true, priv: true, building: true, costs: true);
            tc.AuditOwners();
            tc.AuditFounder();
            tc.AuditProtectionLevel();
            LogAction(tc.EntityID, $"Owners have been updated to {tc.Owners.Select(x => x.Name).ToSentence()} the Founder is {tc.FounderDisplayName}");
            UpdateOnlineOwners(tc, false);
        }

        private void UpdateOnlineOwners(ProtectedEntity tc, bool audit = true)
        {
            if (IsNull(tc)) return;
            tc.AuditOwners();
            tc.AuditFounder();
            tc.AuditProtectionLevel();
            tc.ClearCache(founderOwnersAndProtectionLevel: true);
            if (!tc.OnlineOwners.Any())
            {
                LogAction(tc.EntityID, $"All owners are now offline");
            }
            else
            {
                LogAction(tc.EntityID, $"Online owners ({tc.OnlineOwners.Count}/{tc.Owners.Count}) are {tc.OnlineOwners.Select(x => x.Name).ToSentence()}");
            }
            //StopPendingProtectionTimer(tc);
            // Start online protection
            if (tc.HasOwnersOnline && tc.HasOnlineProtection && tc.HasRemainingProtection && !tc.IsProtected)
            {
                StartOrStopProtection(tc);
            }
            // Start offline protection
            else if (!tc.HasOwnersOnline && tc.HasOfflineProtection && tc.HasRemainingProtection && !tc.IsProtected)
            {
                tc.SetProtectionStartDelayTime(tc.HighestProtectionLevel.OfflineProtectionDelay, StatusReason.OfflineOnly);
                StartOrStopProtection(tc);
            }
            // Start offline protection while there is current protection (of a different percent)
            else if (!tc.HasOwnersOnline && tc.HasOfflineProtection && tc.HasRemainingProtection && tc.IsProtected && tc.OnlineProtectionPercent != tc.OfflineProtectionPercent && tc.HighestProtectionLevel.OfflineProtectionDelay > 0)
            {
                tc.SetProtectionStartDelayTime(tc.HighestProtectionLevel.OfflineProtectionDelay, StatusReason.PendingOfflineProtection);
                StartOrStopProtection(tc);
            }
            // Stop online protection
            else if (tc.HasOwnersOnline && !tc.HasOnlineProtection && tc.HasRemainingProtection && tc.IsProtected)
            {
                StartOrStopProtection(tc);
            }
            // Stop offline protection
            else if (!tc.HasOwnersOnline && !tc.HasOfflineProtection && tc.HasRemainingProtection && tc.IsProtected)
            {
                StartOrStopProtection(tc);
            }
        }

        private void StopPendingProtectionTimer(ProtectedEntity tc)
        {
            if (tc.HasPendingProtectionTimer)
            {
                LogAction(tc.EntityID, "Stopping pending protection timer");
                tc.StopProtectionDelayTimer();
            }
        }
        #endregion

        #region Protection Balance
        private void ClearProtectionBalance(ProtectedEntity tc, bool stopIfCleared = true, BasePlayer basePlayer = null, BalanceLedgerReason reason = BalanceLedgerReason.Withdraw)
        {
            if (IsNull(tc)) return;
            var cleared = tc.Balance > 0;
            tc.Balance = 0;
            tc.SimpleStatusNeedsUpdate = true;
            BalanceLedger.Update(tc, reason);
            if (!stopIfCleared)
            {
                return;
            }
            if (cleared)
            {
                var byPlayer = basePlayer == null ? "by command" : $"by {basePlayer.displayName}";
                LogAction(tc.EntityID, $"Balance was cleared {byPlayer}");
                StopPendingProtectionTimer(tc);
            }
            StartOrStopProtection(tc);
        }

        private void UpdateProtectionBalance(ProtectedEntity tc, float amount, BalanceLedgerReason reason, BasePlayer basePlayer = null)
        {
            if (IsNull(tc)) return;
            var beforeBalance = tc.Balance;
            if (amount != 0)
            {
                tc.Balance += amount;
                BalanceLedger.Update(tc, reason);
            }
            if (reason == BalanceLedgerReason.Added)
            {
                tc.SimpleStatusNeedsUpdate = true;
            }
            if (reason == BalanceLedgerReason.Withdraw || reason == BalanceLedgerReason.Added || reason == BalanceLedgerReason.Other || reason == BalanceLedgerReason.Command)
            {
                var byPlayer = basePlayer == null ? "by command" : $"by {basePlayer.displayName}";
                LogAction(tc.EntityID, $"Balance updated by {amount} from {beforeBalance} to {tc.Balance} with reason {reason} {byPlayer}");
            }
            StartOrStopProtection(tc);
        }
        #endregion

        #region Protection Status
        private void UpdateProtectionStatus(ProtectedEntity tc, ProtectionStatus status)
        {
            if (IsNull(tc)) return;
            var previousStatus = tc.Status;
            if (status == ProtectionStatus.Protected)
            {
                if (!tc.HasFounder)
                {
                    // No founder
                    tc.Status = ProtectionStatus.Unprotected;
                }
                else if (!tc.HasOnlineProtection && !tc.HasOfflineProtection)
                {
                    // Has no protection at all
                    tc.Status = ProtectionStatus.Unprotected;
                }
                else if (tc.HasOnlineProtection && !tc.HasOfflineProtection && tc.HasOwnersOnline)
                {
                    // Has only Online protection
                    tc.Status = ProtectionStatus.Protected;
                }
                else if (!tc.HasOnlineProtection && tc.HasOfflineProtection && !tc.HasOwnersOnline)
                {
                    // Has only Offline protection
                    tc.Status = ProtectionStatus.Protected;
                }
                else if(tc.HasOnlineProtection && tc.HasOfflineProtection)
                {
                    // Has both Online/Offline protection
                    tc.Status = ProtectionStatus.Protected;
                }
                else
                {
                    tc.Status = ProtectionStatus.Unprotected;
                }
            }
            else
            {
                tc.Status = status;
            }
            if (tc.Status == ProtectionStatus.Protected && previousStatus != ProtectionStatus.Protected)
            {
                Oxide.Core.Interface.Call("OnProtectionStarted", tc.BaseEntity);
            }
            else if (tc.Status == ProtectionStatus.Unprotected && previousStatus != ProtectionStatus.Unprotected)
            {
                Oxide.Core.Interface.Call("OnProtectionStopped", tc.BaseEntity, tc.Reason.ToString());
            }
            LogAction(tc.EntityID, $"Protection status updated from {previousStatus} to {tc.Status}");
            foreach (var viewer in tc.CurrentlyViewingInfoPanel)
            {
                ShowProtectionStatusOverlay(viewer, tc);
            }
        }
        #endregion

        #region Structure Damage
        private object DamageProtectedStructure(ProtectedEntity tc, BaseCombatEntity entity, HitInfo info)
        {
            if (IsNull(tc) || IsNull(entity) || IsNull(info) || info.damageTypes.Has(Rust.DamageType.Decay) || info.damageTypes.Has(Rust.DamageType.Decay) || AbandonedBasesHelper.IsAbandoned(entity)) return null;
            if (!ZoneManagerHelper.AllowDestruction(entity, tc)) { return null; }
            var attacker = info.Initiator;
            var attackerPlayer = info.InitiatorPlayer;
            if (!config.Protection.ProtectedFrom.AuthorizedPlayers && attackerPlayer != null && tc.IsAuthed(attackerPlayer))
            {
                return null;
            }
            else if (!config.Protection.ProtectedFrom.NPCs && attackerPlayer != null && attackerPlayer.IsBot)
            {
                return null;
            }
            else if (!config.Protection.ProtectedFrom.UnauthorizedPlayers && attackerPlayer != null && !attackerPlayer.IsBot)
            {
                return null;
            }
            else if (!config.Protection.ProtectedFrom.AttackHeli && attacker != null && attacker.name == "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab")
            {
                return null;
            }
            else if (!config.Protection.ProtectedFrom.AttackHeli && info != null && info.WeaponPrefab != null && (info.WeaponPrefab.name == "rocket_heli" || info.WeaponPrefab.name == "rocket_heli_napalm"))
            {
                return null;
            }
            else if (!config.Protection.ProtectedFrom.NPCs && attacker != null && attacker.IsNpc)
            {
                return null;
            }
            var totalDamage = info.damageTypes.Total();
            var fireDamage = info.damageTypes.Get(Rust.DamageType.Heat);
            var shockDamage = info.damageTypes.Get(Rust.DamageType.ElectricShock);
            float damageTaken;
            float damageProtected;
            bool providedProtection = false;
            var majoritytype = info.damageTypes.GetMajorityDamageType();
            var protectionPercent = tc.CurrentProtectionPercent;
            var beforeBalance = tc.Balance;
            object returnVal;
            if (protectionPercent >= 100f)
            {
                returnVal = true; // completely protected
                damageTaken = 0f;
                damageProtected = totalDamage;
                providedProtection = true;
                //if (attackerPlayer != null)
                //{
                //    NotifyProtectionStatusChanged(attackerPlayer, tc);
                //}
            }
            else if (protectionPercent <= 0f)
            {
                returnVal = null; // completely unprotected
                damageTaken = totalDamage;
                damageProtected = 0f;
            }
            else
            {
                // some protection
                var factor = 1f - (protectionPercent / 100f);
                info.damageTypes.ScaleAll(factor);
                returnVal = null;
                damageTaken = totalDamage * factor;
                damageProtected = totalDamage - damageTaken;
                providedProtection = true;
                
            }
            // charge for damage taken
            if (providedProtection && tc.HasCostPerDamageProtected)
            {
                damageProtected -= fireDamage;
                damageProtected -= shockDamage; // dont charge for these
                var cost = tc.CostPerDamageProtected * damageProtected;
                UpdateProtectionBalance(tc, -cost, BalanceLedgerReason.DoNotRecord);
                if (attackerPlayer != null)
                {
                    if (SimpleStatusHelper.IsLoaded())
                    {
                        if (!SimpleStatusHelper.SimpleStatusAccumulatedCost.ContainsKey(attackerPlayer.UserIDString))
                        {
                            SimpleStatusHelper.SimpleStatusAccumulatedCost[attackerPlayer.UserIDString] = 0;
                        }
                        SimpleStatusHelper.SimpleStatusAccumulatedCost[attackerPlayer.UserIDString] += cost;
                        SimpleStatusHelper.SetProtectionStatus(attackerPlayer, tc, popup: true);
                        SimpleStatusHelper.SetBalanceStatus(attackerPlayer, tc);
                    }
                    NotifyProtectionStatusChanged(attackerPlayer, tc, false, cost);
                }
            }
            else if (providedProtection)
            {
                if (attackerPlayer != null)
                {
                    if (SimpleStatusHelper.IsLoaded())
                    {
                        SimpleStatusHelper.SetProtectionStatus(attackerPlayer, tc, popup: true);
                    }
                    NotifyProtectionStatusChanged(attackerPlayer, tc, false);
                }
            }
            OnAfterProtectedStructureDamaged(tc, entity, attackerPlayer, beforeBalance, totalDamage, damageTaken, providedProtection, majoritytype);
            return returnVal;
        }

        private void OnAfterProtectedStructureDamaged(ProtectedEntity tc, BaseCombatEntity entity, BaseEntity attacker, float beforeBalance, float totalDamage, float takenDamage, bool providedProtection, Rust.DamageType majorityDamageType)
        {
            if (IsNull(tc)) return;
            if (config.EnableLogging && (totalDamage > 0 || attacker != null))
            {
                var balanceText = tc.Balance < beforeBalance ? $". Balance was {beforeBalance} and is now {tc.Balance}" : String.Empty;
                var damageTypeText = majorityDamageType.ToString();
                LogAction(tc.EntityID, $"Attacked by {GetEntityDisplayName(attacker)} for {takenDamage}/{totalDamage} {damageTypeText} damage{balanceText}");
            }
            if (!providedProtection)
            {
                if (tc.HighestProtectionLevel.DamageResetsTimerWhenOwnerIsOffline)
                {
                    tc.SetProtectionStartDelayTime(tc.HighestProtectionLevel.ProtectedDelayAfterTakingDamage, StatusReason.RecentlyTookDamage);
                }
                else if (tc.HasOwnersOnline)
                {
                    tc.SetProtectionStartDelayTime(tc.HighestProtectionLevel.ProtectedDelayAfterTakingDamage, StatusReason.RecentlyTookDamage);
                }
            }
            if (beforeBalance > 0 && tc.Balance <= 0)
            {
                StopProtection(tc, () =>
                {
                    timer.In(0.5f, () =>
                    {
                        if (attacker != null && tc != null && attacker is BasePlayer && !attacker.IsNpc)
                        {
                            NotifyProtectionStatusChanged((BasePlayer)attacker, tc, true);
                        }
                    });
                });
            }
        }

        private void DestroyProtectedCupboard(ProtectedEntity tc, BaseEntity attacker)
        {
            if (IsNull(tc) || IsNull(attacker)) return;
            var balance = tc.Balance;
            RemoveProtectedCupboard(tc);
            var awardBalance = config.Protection.AwardRemainingBalance && balance > 0;
            LogAction(tc.EntityID, $"Destroyed by {GetEntityDisplayName(attacker)}. Awarded {(awardBalance ? balance : 0)}");
            if (awardBalance && attacker is BasePlayer && !attacker.IsNpc)
            {
                GiveBalanceResource((BasePlayer)attacker, balance);
                Message((BasePlayer)attacker, "message awarded", FormatCurrency(((BasePlayer)attacker).UserIDString, (float)balance, true));
            }
        }
        #endregion

        #region Structure Updates
        private void UpdateProtectedStructure(ProtectedEntity tc)
        {
            if (IsNull(tc)) return;
            tc.ClearCache(building: true, costs: true);
        }
        #endregion

        #region Viewing Cupboard
        private void UpdateProtectedCupboardInventory(ProtectedEntity tc, BasePlayer basePlayer)
        {
            if (IsNull(tc)) return;
            if (tc.IsViewingProtectionPanel(basePlayer))
            {
                ShowProtectionStatusOverlay(basePlayer, tc);
            }
        }
        private void OpenProtectedCupboard(ProtectedEntity tc, BasePlayer basePlayer, bool showTabs = true)
        {
            if (IsNull(tc)) return;
            if (showTabs)
            {
                ShowProtectionStatusOverlayTabs(basePlayer, tc);
            }
            else
            {
                ShowProtectionStatusOverlay(basePlayer, tc);
            }
            if (!tc.CurrentlyViewing.Contains(basePlayer))
            {
                tc.CurrentlyViewing.Add(basePlayer);
            }
        }

        private void CloseProtectedCupboard(ProtectedEntity tc, BasePlayer basePlayer)
        {
            if (IsNull(tc)) return;
            CloseInfoPanel(basePlayer);
            CloseProtectionStatusOverlay(basePlayer);
            CloseProtectionStatusOverlayTabs(basePlayer);
            if (tc.CurrentlyViewing.Contains(basePlayer))
            {
                tc.CurrentlyViewing.Remove(basePlayer);
            }
        }
        #endregion

        private bool IsNull(object obj)
        {
            if (obj == null)
            {
                return true;
            }
            return false;
        }

        private void NotifyProtectionStatusChanged(BasePlayer basePlayer, ProtectedEntity tc, bool ignoreCooldown = false, double deducted = 0)
        {
            tc.SimpleStatusNeedsUpdate = true;
            ShowIndicator(basePlayer, tc, ignoreCooldown, deducted);
            if (config.Integration.Notify)
            {
                NotifyHelper.ShowProtectionStatus(basePlayer, tc);
            }
            if (config.Integration.CustomStatusFramework && config.CustomStatusFramework.ShowAttackedIndicator)
            {
                CustomStatusFrameworkHelper.ShowAttackedProtectionStatus(basePlayer, tc);
                CustomStatusFrameworkHelper.ShowProtectionBalanceDeducted(basePlayer, tc);
            }
            SetIndicatorCooldownOrClose(basePlayer, tc);
        }
    }
}

namespace Oxide.Plugins
{
    partial class RaidProtection
    {
        private void LogAction(NetworkableId entityId, string message)
        {
            if (config.EnableLogging)
            {
                var now = DateTime.Now;
                var msg = $"{now,-30}{entityId, -20}{message}";
                var fileName = $"tc_{entityId}";
                LogToFile(fileName, msg, this, true);
            }
        }
    }
}

namespace Oxide.Plugins.RaidProtectionExtensionMethods
{
    public static class ExtensionMethods
    {
        public static ulong UserId(this IPlayer player)
        {
            return ulong.Parse(player.Id);
        }

        public static ulong UserId(this BasePlayer basePlayer)
        {
            return basePlayer.userID.Get();
        }

        public static bool IsOnline(this IPlayer player)
        {
            var basePlayer = BasePlayer.FindByID(player.UserId());
            return player.IsConnected || (basePlayer != null && basePlayer.IsConnected);
        }

        public static V GetValueOrNew<K, V>(this Dictionary<K, V> dict, K key) where V : new()
        {
            var value = dict.GetValueOrDefault(key);
            if (value != null) { return value; }
            dict[key] = new V();
            return dict[key];
        }
    }
}

namespace Oxide.Plugins
{
    partial class RaidProtection
    {
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["title"] = "raid protection",
                ["tc id"] = "ID: {0}",
                ["message awarded"] = "You have been awarded the remaining protection balance of {0}.",
                ["command changelog"] = "<color=yellow>New changes in Raid Protecion v{0}:</color>{1}",
                ["command help title"] = "Raid Protection Admin Commands",
                ["command success"] = "The command was successful.",
                ["command error"] = "There was an error with the command.",
                ["command no tc"] = "No tool cupboard was found near {0}.",
                ["command no tc found"] = "No tool cupboard was found.",
                ["command all args required"] = "All arguments are required when using a command from the server console.",
                ["command usage"] = "Usage: /{0} {1} {2}",
                ["command player not found"] = "The player {0} could not be found.",
                ["command invalid value"] = "Invalid value {0} was given.",
                ["command cannot restore"] = "Failed to restore balance to snapshot with guid '{0}'. Verify that the given guid is correct and manually set the balance if needed.",
                ["command no permission"] = "The permission '{0}' is required to use this command.",
                ["command cannot remove auth"] = "Authorized owners cannot be removed. They must first deauthorize from the tool cupboard.",
                ["command id"] = "The tool cupboard id is {0}",
                ["command owners"] = "Owners of tool cupboard {0}\n{1}",
                ["command founder"] = "Founder of tool cupboard {0} is {1} and inherits their protection level rank {2}",
                ["command protection"] = "Protection for tool cupboard {0}\nCurrent {1}%\nOnline {2}%\nOffline {3}%\nLevel {4}",
                ["command cost"] = "The hourly costs for tool cupboard {0}\nBase {1}\nPer Owner {2} ({3})\nPer Floor {4} ({5})\nTotal {6}",
                ["command balance"] = "Balance for tool cupboard {0} is {1}",
                ["command rollback"] = "Restored balances back to date {0} for {1} tool cupboards.",
                ["command status"] = "Protection status for tool cupboard {0}\nStatus {1}\nReason {2}",
                ["message authorize admin"] = "<color=yellow>IMPORTANT:</color> You have authorized on this tool cupboard as an admin. You will be counted as an owner and affect online/offline protection until you deauthorize yourself.",
                ["message deauthorize admin"] = "<color=yellow>IMPORTANT:</color> As an admin, you have been removed as an owner from this tool cupboard. The online/offline protection will no longer be affected.",
                ["balance"] = "balance",
                ["cost"] = "cost",
                ["max time"] = "max time",
                ["note maxed"] = "Max protection time reached",
                ["note paused"] = "Toggle to resume protection",
                ["note damaged"] = "Protection starts in {0} seconds",
                ["status protected"] = "Protected",
                ["status unprotected"] = "Unprotected",
                ["status pending"] = "Pending",
                ["reason paused"] = "protection paused, resume to continue protection",
                ["reason offline only"] = "Protected by {0}% for {1} when owners are offline",
                ["reason online only"] = "Protected by {0}% for {1} when owners are online",
                ["reason recently damaged"] = "took damage, protection delayed",
                ["reason insufficient item"] = "add {0} more {1} to tool cupboard",
                ["reason insufficient balance"] = "purchase at least 1 hour of protection time",
                ["reason no permission"] = "no protection permission",
                ["reason no founder"] = "no founder",
                ["reason tugboat auth"] = "authorize yourself on the tugboat",
                ["reason pending"] = "protection is delayed",
                ["reason pending offline only"] = "offline protection starts in {0} seconds",
                ["reason founder limit"] = "Tool cupboard founder limit reached",
                ["ui info text"] = "Your base can be protected from damage at the cost of resources. Purchase protection time to ensure your base cannot be raided. Clear to refund balance.",
                ["ui cost per hour"] = "Cost per hour",
                ["ui damage cost"] = "Protected damage will reduce your balance!",
                ["ui protected"] = "Protected by {0}% for {1}",
                ["ui button activate"] = "Activate",
                ["ui button 1h"] = "+1 hour",
                ["ui button 24h"] = "+24 hour",
                ["ui button max"] = "+Max",
                ["ui button clear"] = "Clear",
                ["ui button info"] = "Info",
                ["ui button pause"] = "Pause",
                ["ui button resume"] = "Resume",
                ["ui max time reached"] = "Max Time Reached",
                ["ui tab upkeep"] = "Upkeep",
                ["ui tab protection"] = "Protection",
                ["ui your balance"] = "You have {0}",
                ["info need authorization"] = "You must be authorized on this tool cupboard or and admin to view it's protection info.",
                ["info owners count"] = "Owners {0}",
                ["info protection"] = "Protection {0}",
                ["info balance"] = "Balance {0}",
                ["info owners persistent"] = "Owners are permanent once authorized and are not removed when deauthorized.",
                ["info owners"] = "Owners",
                ["info online"] = "Online",
                ["info authorized"] = "Authorized",
                ["info others"] = "+{0} Others",
                ["info founder"] = "(founder)",
                ["info clan"] = "(clan)",
                ["info protection rank"] = "Protection Rank {0} (from founder)",
                ["info protection current"] = "Current",
                ["info protection online"] = "Online",
                ["info protection offline"] = "Offline",
                ["info max protection time"] = "Max Protection Time",
                ["info no limit"] = "No Limit",
                ["info hours"] = "{0} Hours",
                ["info cost when protected"] = "Cost When Protected",
                ["info protection time delay"] = "Protection Time Delay",
                ["info delay seconds"] = "{0} sec",
                ["info starts when owners offline"] = "Starts when all owners are offline",
                ["info protected entities"] = "Protected Entities",
                ["info protected animals"] = "Animals",
                ["info protected buildings"] = "Buildings",
                ["info protected deployables"] = "Deployables",
                ["info protected traps"] = "Traps",
                ["info protected electrical"] = "Electrical",
                ["info protected horses"] = "Horses",
                ["info protected loot nodes"] = "Loot Nodes",
                ["info protected npcs"] = "NPCs",
                ["info protected unauthed players"] = "Unauthed Players",
                ["info protected authed players"] = "Authed Players",
                ["info protected vehicles"] = "Vehicles",
                ["info protected attack heli"] = "Attack Heli",
                ["info protected"] = "Protected",
                ["info from"] = "From",
                ["info total hourly cost"] = "Total Hourly Cost",
                ["info balance label"] = "Balance",
                ["info balance info"] = "Your protection balance must stay above zero (unless the cost is free) in order to maintain raid protection. If the balance falls below zero, you will be unprotected.",
                ["info hourly base cost"] = "Hourly Base Cost",
                ["info hourly base cost note"] = "From Rank {0} Protection",
                ["info hourly base cost info"] = "Fixed cost charged per hour, based on your protection level.",
                ["info hourly foundation cost"] = "Hourly Floor Cost",
                ["info hourly foundation cost note"] = "{0} Floors x {1}",
                ["info hourly foundation cost info"] = "Charged every hour for each floor in your base. Building larger structures will cost make protection cost more.",
                ["info hourly owner cost"] = "Hourly Owner Cost",
                ["info hourly owner cost note"] = "{0} Owners x {1}",
                ["info hourly owner cost info"] = "The amount charged for each owner of this structure. Having a larger team will make protection cost more.",
                ["indicator balance reduced amount"] = "-{0} Balance",
                ["levels protection"] = "Protection",
                ["levels costs"] = "Costs",
                ["levels online"] = "Online",
                ["levels offline"] = "Offline",
                ["levels max hours"] = "Max Hours",
                ["levels base cost"] = "Base",
                ["levels foundation cost"] = "Per Floor",
                ["levels owner cost"] = "Per Owner",
                ["levels damage cost"] = "Per Damage",
                ["levels delays"] = "Delays",
                ["levels after offline"] = "After Offline",
                ["levels after damaged"] = "After Damaged",
                ["levels protection level"] = "Your protection level is Rank {0}",
                ["levels description"] = "Tool cupboards that you place will inherit your protection level.",
                ["levels no limit"] = "No Limit",
                ["notify protected"] = "Protected {0}%",
                ["notify unprotected"] = "Unprotected",
                ["customstatusframework raid protected"] = "Raid Protected",
                ["customstatusframework protected"] = "Protected",
                ["customstatusframework balance"] = "Balance",
                ["economics currency format"] = "{0:C}",
                ["server rewards currency format"] = "{0} RP",
                ["founder limit warning"] = "You are founder of {0}/{1} tool cupboards. When you reach your limit you can no longer receive raid protection until previous tool cupboards have been destroyed.",
                ["founder limit reached"] = "You have reached the limit of {0} tool cupboards that can receive raid protection and this tool cupboard will not be protected. Destroy previously placed tool cupboards and activate to receive raid protection.",
                ["simplestatus owner"] = "RAID PROTECTION",
                ["simplestatus nonowner"] = "RAID PROTECTED",
                ["simplestatus balance"] = "BALANCE",
                ["simplestatus offline only"] = "Offline Only",
                ["simplestatus damage delayed"] = "Damage Delayed"
            }, this);
        }

        private string Lang(string key, params object[] args) => string.Format(lang.GetMessage(key, this), args);
        private string Lang(string key, string id, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private string Lang(string key, BasePlayer basePlayer, params object[] args) => string.Format(lang.GetMessage(key, this, basePlayer?.UserIDString), args);
    }
}

namespace Oxide.Plugins
{
    partial class RaidProtection
    {
        public class ProtectedCupboard : ProtectedEntity
        {
            public ProtectedCupboard() { }

            public ProtectedCupboard(BuildingPrivlidge priv)
            {
                this.EntityID = priv.net.ID;
                ClearCache(priv: true, building: true, costs: true);
            }

            private BuildingPrivlidge _buildingPrivlidge = null;


            [JsonIgnore]
            public override List<ulong> AuthedPlayerUserIds
            {
                get
                {
                    return BuildingPrivlidge == null ? new List<ulong>() : BuildingPrivlidge.authorizedPlayers.Select(x => x.userid).ToList();
                }
            }

            [JsonIgnore]
            public override int? FoundationCount
            {
                get
                {
                    if (_foundationCount == null)
                    {
                        if (BuildingPrivlidge == null || BuildingPrivlidge.GetBuilding() == null)
                        {
                            return 0;
                        }
                        _foundationCount = BuildingPrivlidge.GetBuilding().buildingBlocks.Where(x =>
                        {
                            var shortPrefabName = x.ShortPrefabName;
                            return shortPrefabName.StartsWith("foundation") || shortPrefabName.StartsWith("floor");
                        }).Count();
                    }
                    return _foundationCount ?? 0;
                }
            }

            [JsonIgnore]
            public override bool IsNpcBase
            {
                get
                {
                    return PLUGIN.IsNpcBase(BuildingPrivlidge);
                }
            }

            [JsonIgnore]
            public override List<Item> InventoryItemList
            {
                get
                {
                    return BuildingPrivlidge?.inventory.itemList ?? new List<Item>();
                }
            }

            [JsonIgnore]
            public BuildingPrivlidge BuildingPrivlidge
            {
                get
                {
                    if (_buildingPrivlidge == null)
                    {
                        _buildingPrivlidge = (BuildingPrivlidge)BaseEntity;
                    }
                    return _buildingPrivlidge;
                }
            }

            public override bool IsAuthed(BasePlayer basePlayer)
            {
                return BuildingPrivlidge?.IsAuthed(basePlayer) ?? false;
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class RaidProtection
    {
        public Dictionary<ulong, ProtectedEntity> _protectedCupboards = new Dictionary<ulong, ProtectedEntity>();
        
        public static class ProtectedCupboardManager
        {
            public static Dictionary<ulong, ProtectedEntity> ProtectedCupboards => PLUGIN._protectedCupboards;
            public static ProtectedEntity InitProtectedCupboard(BaseEntity entity)
            {
                if (entity == null || entity.net == null || entity.net.ID == null || ProtectedCupboards == null || PLUGIN == null || PLUGIN.IsNpcBase(entity)) { return null; }
                if (ProtectedCupboards.ContainsKey(entity.net.ID.Value))
                {
                    return ProtectedCupboards[entity.net.ID.Value];
                }
                ProtectedEntity tc = null;
                if (entity is BuildingPrivlidge)
                {
                    tc = new ProtectedCupboard(entity as BuildingPrivlidge);
                }
                else if (entity is Tugboat)
                {
                    tc = new ProtectedTugboat(entity as Tugboat);
                }
                if (tc == null) { return null; }
                ProtectedCupboards.Add(entity.net.ID.Value, tc);
                return tc;
            }
            public static bool RemoveProtectedCupboard(ulong entityID)
            {
                return ProtectedCupboards.Remove(entityID);
            }
            public static bool ProtectedCupboardExists(ulong entityID)
            {
                //return _protectedCupboards.ContainsKey(entityID);
                return ProtectedCupboards.Keys.Any(x => x == entityID);
            }
            public static ProtectedEntity Transfer(ProtectedEntity oldTc, BaseEntity newEntity)
            {
                oldTc.SetBaseEntity(newEntity);
                ProtectedCupboards[newEntity.net.ID.Value] = oldTc;
                var newTc = ProtectedCupboards[newEntity.net.ID.Value];
                PLUGIN.AddProtectedCupboard(newTc.BaseEntity, newTc);
                return newTc;
            }
            public static ProtectedEntity GetByID(ulong entityID)
            {
                ProtectedEntity tc = null;
                ProtectedCupboards.TryGetValue(entityID, out tc);
                return tc;
            }
            public static List<ProtectedEntity> GetCupboardsForOwner(ulong userId)
            {
                return ProtectedCupboards.Values.Where(x => x.OwnerUserIds.Contains(userId)).ToList();
            }
            public static List<ProtectedEntity> GetCupboardsForFounder(ulong userId)
            {
                return ProtectedCupboards.Values.Where(x => x.HasFounder && x.FounderUserId == userId).ToList();
            }
            public static int GetCupboardFounderCount(ulong userId)
            {
                return ProtectedCupboards.Values.Where(x => x.HasFounder && x.FounderUserId == userId && !x.DisableProtectionFromExceedingFounderLimit).Count();
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class RaidProtection
    {
        public class ProtectedEntity
        {
            [JsonIgnore]
            public BaseEntity BaseEntity
            {
                get
                {
                    if (_baseEntity == null)
                    {
                        _baseEntity = (BaseEntity)BaseNetworkable.serverEntities.Find(EntityID);
                    }
                    return _baseEntity;
                }
            }
            public NetworkableId EntityID { get; set; }
            public bool Enabled { get; set; } = true;

            [JsonIgnore]
            public DateTime? LastStatusUIClickTime { get; set; }

            [JsonIgnore]
            public bool CanClickStatusUI
            {
                get
                {
                    if (LastStatusUIClickTime == null)
                    {
                        return true;
                    }
                    return DateTime.Now >= LastStatusUIClickTime.Value.AddSeconds(0.5);
                }
            }


            public float Balance
            {
                get
                {
                    return _balance;
                }
                set
                {
                    _balance = Math.Min(MaxBalance ?? float.MaxValue, Math.Max(0, value));
                }
            }

            public ProtectionStatus Status { get; set; } = ProtectionStatus.Unprotected;

            public bool IsPending => !IsProtected && Reason == StatusReason.OfflineOnly || Reason == StatusReason.RecentlyTookDamage || Reason == StatusReason.Pending;

            public StatusReason Reason
            {
                get
                {
                    if (HasFounder && DisableProtectionFromExceedingFounderLimit)
                    {
                        return StatusReason.ExceedingFounderLimit;
                    }
                    if (!HasFounder)
                    {
                        return StatusReason.NoFounder;
                    }
                    else if (HighestProtectionLevel == ProtectionLevel.NONE)
                    {
                        return StatusReason.NoPermission;
                    }
                    else if (!HasRemainingProtection && !HasCurrencyItemAmount)
                    {
                        return StatusReason.InsufficientItem;
                    }
                    else if (!HasRemainingProtection)
                    {
                        return StatusReason.InsufficientBalance;
                    }
                    else if (!Enabled)
                    {
                        return StatusReason.Paused;
                    }
                    else if (HasPendingProtectionTimer)
                    {
                        return _protectionTimerReason ?? StatusReason.Pending;
                    }
                    else if (!HasOnlineProtection && HasOfflineProtection && HasOwnersOnline)
                    {
                        return StatusReason.OfflineOnly;
                    }
                    else if (HasOnlineProtection && !HasOfflineProtection && !HasOwnersOnline)
                    {
                        return StatusReason.OnlineOnly;
                    }
                    return StatusReason.NoReason;
                }
            }

            public string ReasonAndTimeRemaining
            {
                get
                {
                    var remaining = RemainingProtectionDelaySeconds;
                    if (remaining > 0)
                    {
                        return $"{Reason} ({remaining}s)";
                    }
                    return Reason.ToString();
                }
            }

            public int ProtectionLevelRank { get; set; }

            public bool DisableProtectionFromExceedingFounderLimit { get; set; } = false;

            #region Founder
            public ulong FounderUserId
            {
                get
                {
                    return _founderUserId;
                }
                set
                {
                    _founderUserId = value;
                    _founder = null;
                }
            }

            private BaseEntity _baseEntity = null;
            private float? _ownerCost = null;
            private float? _buildingCost = null;
            private float? _totalProtectionCost = null;
            protected int? _foundationCount = null;
            private float _balance = 0f;
            private float? _totalCostPerInterval = null;
            private ulong _founderUserId = 0;
            private IPlayer _founder = null;
            private List<IPlayer> _owners = null;
            private List<IPlayer> _onlineOwners = null;
            private ProtectionLevel _highestProtectionLevel = null;
            private Timer _pendingProtectionTimer = null;
            private DateTime? _timeWhenProtectionMayResume = null;
            public StatusReason? _protectionTimerReason = null;

            [JsonIgnore]
            public bool IsCupboard => BaseEntity != null && BaseEntity is BuildingPrivlidge;

            [JsonIgnore]
            public bool IsTugboat => BaseEntity != null && BaseEntity is Tugboat;

            [JsonIgnore]
            public ulong ID => EntityID.Value;


            [JsonIgnore]
            public bool ForceOffline { get; set; } = false;

            [JsonIgnore]
            public IPlayer Founder
            {
                get
                {
                    if (_founder == null && FounderUserId != null)
                    {
                        _founder = FindPlayer(FounderUserId);
                    }
                    return _founder;
                }
            }

            [JsonIgnore]
            public bool HasFounder
            {
                get
                {
                    return FounderUserId != 0;
                }
            }

            [JsonIgnore]
            public string FounderDisplayName
            {
                get
                {
                    if (HasFounder)
                    {
                        if (Founder == null || string.IsNullOrEmpty(Founder.Name))
                        {
                            return FounderUserId.ToString();
                        }
                        else
                        {
                            return Founder.Name;
                        }
                    }
                    return "None";
                }
            }
            #endregion

            #region Owners
            public HashSet<ulong> OwnerUserIds { get; set; } = new HashSet<ulong>();

            [JsonIgnore]
            public List<IPlayer> AuthedPlayers => FindPlayers(AuthedPlayerUserIds);

            [JsonIgnore]
            public virtual List<ulong> AuthedPlayerUserIds => new List<ulong>();

            [JsonIgnore]
            public List<IPlayer> Owners
            {
                get
                {
                    if (_owners == null)
                    {
                        _owners = FindPlayers(OwnerUserIds);
                    }
                    return _owners;
                }
            }

            [JsonIgnore]
            public bool HasOwners => OwnerUserIds != null && OwnerUserIds.Any();

            [JsonIgnore]
            public List<IPlayer> OnlineOwners
            {
                get
                {
                    if (ForceOffline)
                    {
                        return new List<IPlayer>();
                    }
                    if (_onlineOwners == null)
                    {
                        _onlineOwners = Owners.Where(x => x != null && x.IsOnline()).ToList();
                    }
                    return _onlineOwners;
                }
            }

            #endregion

            [JsonIgnore]
            public ProtectionLevel HighestProtectionLevel
            {
                get
                {
                    if (_highestProtectionLevel == null)
                    {
                        _highestProtectionLevel = ProtectionLevel.GetByRankNullable(ProtectionLevelRank);
                    }
                    return _highestProtectionLevel;
                }

            }

            [JsonIgnore]
            public float? MaxBalance
            {
                get
                {
                    return !HasProtectionTimeLimit ? null : MaxProtectionTimeHours * TotalProtectionCostPerHour;
                }
            }

            [JsonIgnore]
            public ulong NumTimesCollected { get; set; } = 0;

            [JsonIgnore]
            public bool HasFreeProtection
            {
                get
                {
                    return TotalProtectionCostPerHour <= 0;
                }
            }

            [JsonIgnore]
            public float? AllowedBalanceRemaining
            {
                get
                {
                    return !HasProtectionTimeLimit ? null : MaxBalance - Balance;
                }
            }

            [JsonIgnore]
            public bool HasOnlineProtection
            {
                get
                {
                    return HighestProtectionLevel.HasOnlineProtection;
                }
            }

            [JsonIgnore]
            public bool IsAtMaxBalance
            {
                get
                {
                    if (!HasProtectionTimeLimit) { return false; }
                    return Balance >= MaxBalance;
                }
            }

            [JsonIgnore]
            public bool HasOfflineProtection
            {
                get
                {
                    return HighestProtectionLevel.HasOfflineProtection;
                }
            }

            [JsonIgnore]
            public bool HasOwnersOnline
            {
                get
                {
                    return OnlineOwners.Count > 0;
                }
            }

            [JsonIgnore]
            public bool HasProtectionTimeLimit
            {
                get
                {
                    return HighestProtectionLevel.HasProtectionTimeLimit;
                }
            }

            [JsonIgnore]
            public int? MaxProtectionTimeHours
            {
                get
                {
                    return HighestProtectionLevel.MaxProtectionTimeHours;
                }
            }

            [JsonIgnore]
            public float HoursOfProtection
            {
                get
                {
                    return TotalProtectionCostPerHour <= 0 ? float.PositiveInfinity : HasProtectionTimeLimit ? Math.Min(MaxProtectionTimeHours ?? 0, Balance / TotalProtectionCostPerHour ?? 0) : Balance / TotalProtectionCostPerHour ?? 0;
                }
            }

            [JsonIgnore]
            public bool HasPendingProtectionTimer => _pendingProtectionTimer != null;

            [JsonIgnore]
            public float CostPerDamageProtected
            {
                get
                {
                    return HighestProtectionLevel.CostPerDamageProtected;
                }
            }

            [JsonIgnore]
            public bool HasCostPerDamageProtected
            {
                get
                {
                    return CostPerDamageProtected > 0f;
                }
            }

            [JsonIgnore]
            public float OfflineProtectionPercent
            {
                get
                {
                    return HighestProtectionLevel.OfflineProtectionPercent;
                }
            }

            [JsonIgnore]
            public float OnlineProtectionPercent
            {
                get
                {
                    return HighestProtectionLevel.OnlineProtectionPercent;
                }
            }

            [JsonIgnore]
            public float CurrentProtectionPercent
            {
                get
                {
                    if (Status != ProtectionStatus.Protected)
                    {
                        return 0f;
                    }
                    if (HasOwnersOnline)
                    {
                        return OnlineProtectionPercent;
                    }
                    else
                    {
                        if (HasPendingProtectionTimer && _protectionTimerReason == StatusReason.PendingOfflineProtection)
                        {
                            return OnlineProtectionPercent;
                        }
                        else
                        {
                            return OfflineProtectionPercent;
                        }
                    }
                }
            }

            [JsonIgnore]
            public float? TotalProtectionCostPerHour
            {
                get
                {
                    if (_totalProtectionCost == null)
                    {
                        _totalProtectionCost = BaseCostPerHour + BuildingCostPerHour + OwnerCostPerHour;
                    }
                    return _totalProtectionCost ?? 0;
                }
            }

            [JsonIgnore]
            public float? OwnerCostPerHour
            {
                get
                {
                    if (_ownerCost == null)
                    {
                        _ownerCost = Owners.Count * HighestProtectionLevel.HourlyCostPerOwner;
                    }
                    return _ownerCost ?? 0;
                }
            }

            [JsonIgnore]
            public float? BuildingCostPerHour
            {
                get
                {
                    if (_buildingCost == null)
                    {
                        _buildingCost = FoundationCount * HighestProtectionLevel.HourlyCostPerFloor;
                    }
                    return _buildingCost ?? 0;
                }
            }

            [JsonIgnore]
            public virtual int? FoundationCount
            {
                get
                {
                    return 0;
                }
            }

            [JsonIgnore]
            public float BaseCostPerHour
            {
                get
                {
                    return HighestProtectionLevel.HourlyBaseCost;
                }
            }

            [JsonIgnore]
            public HashSet<BasePlayer> CurrentlyViewing { get; } = new HashSet<BasePlayer>();

            [JsonIgnore]
            public List<BasePlayer> CurrentlyViewingInfoPanel
            {
                get
                {
                    return CurrentlyViewing.Where(x => x != null && PlayersViewingOverlay.Contains(x.userID)).ToList();
                }
            }

            [JsonIgnore]
            public virtual List<Item> InventoryItemList => new List<Item>();

            [JsonIgnore]
            public int CurrencyItemCount
            {
                get
                {
                    if (BaseEntity == null) { return 0; }
                    var count = 0;
                    foreach (var item in InventoryItemList)
                    {
                        if (item.info.shortname.ToLower() == PLUGIN.config.Protection.CurrencyItem.ToLower())
                        {
                            count += item.amount;
                        }
                    }
                    return count;
                }
            }

            [JsonIgnore]
            public int CurrencyItemRemainingAmountRequired
            {
                get
                {
                    return (int)Math.Ceiling(TotalProtectionCostPerHour.Value) - CurrencyItemCount;
                }
            }

            [JsonIgnore]
            public bool HasCurrencyItemAmount
            {
                get
                {
                    return (PLUGIN.config.Integration.Economics || PLUGIN.config.Integration.ServerRewards) ? true : PLUGIN.HasBalanceResourceAmount(this, null, TotalProtectionCostPerHour ?? 0);
                }
            }

            [JsonIgnore]
            public bool HasRemainingProtection
            {
                get
                {
                    if (DisableProtectionFromExceedingFounderLimit) { return false; }
                    return (HoursOfProtection > 0f && Balance > 0) || (TotalProtectionCostPerHour.HasValue && TotalProtectionCostPerHour <= 0);
                }
            }

            [JsonIgnore]
            public virtual bool IsNpcBase => false;

            [JsonIgnore]
            public bool IsProtected
            {
                get
                {
                    return Status == ProtectionStatus.Protected;
                }
            }

            [JsonIgnore]
            public bool SkipNextProtectionStartDelay { get; set; } = true;

            [JsonIgnore]
            public bool SimpleStatusNeedsUpdate { get; set; } = true;

            public bool IsViewingProtectionPanel(BasePlayer basePlayer)
            {
                return PlayersViewingOverlay.Contains(basePlayer.UserId()) && CurrentlyViewing.Contains(basePlayer);
            }

            public float TotalCostPerInterval(int intervalTimeSeconds)
            {
                if (_totalCostPerInterval == null || _totalProtectionCost == null)
                {
                    _totalCostPerInterval = TotalProtectionCostPerHour / 60f * (intervalTimeSeconds / 60f);
                }
                return _totalCostPerInterval ?? 0;
            }

            public void SetEntityOwner(ulong? userId)
            {
                if (userId.HasValue)
                {
                    BaseEntity.OwnerID = userId.Value;
                    FounderUserId = userId.Value;
                }
                else
                {
                    BaseEntity.OwnerID = 0;
                    FounderUserId = 0;
                }
            }

            public void ClearCache(bool founderOwnersAndProtectionLevel = false, bool priv = false, bool building = false, bool costs = false)
            {
                if (founderOwnersAndProtectionLevel)
                {
                    _founder = null;
                    _owners = null;
                    _onlineOwners = null;
                    _highestProtectionLevel = null;
                }
                if (priv)
                {
                    _baseEntity = null;
                }
                if (building)
                {
                    _foundationCount = null;
                }
                if (costs)
                {
                    _ownerCost = null;
                    _buildingCost = null;
                    _totalProtectionCost = null;
                }
            }

            public void AuditFounder()
            {
                // Try to get founder from EntityOnwer
                if (FounderUserId == 0 && BaseEntity != null && BaseEntity.OwnerID != 0)
                {
                    FounderUserId = BaseEntity.OwnerID;
                }
                // Try to get founder from owner list
                if (FounderUserId == 0 && HasOwners)
                {
                    FounderUserId = OwnerUserIds.First();
                }
                // Set initial tugboat to first authed
                if (IsTugboat && !HasFounder && AuthedPlayerUserIds.Any())
                {
                    SetEntityOwner(AuthedPlayerUserIds.First());
                }
            }

            public void AuditProtectionLevel()
            {
                // Try to get protection level from founder
                if (HasFounder)
                {
                    if (PLUGIN.IsIgnored(FounderUserId.ToString()))
                    {
                        ProtectionLevelRank = 0;
                        return;
                    }
                    var rank = ProtectionLevel.GetProtectionLevelOfPlayer(FounderUserId).Rank;
                    if (rank != 0)
                    {
                        ProtectionLevelRank = rank;
                    }
                }
                else
                {
                    ProtectionLevelRank = 0;
                }
            }

            public void AuditOwners()
            {
                // Make sure it has authorized players
                try
                {
                    if (BaseEntity != null)
                    {
                        foreach (var userId in AuthedPlayerUserIds)
                        {
                            if (!OwnerUserIds.Contains(userId))
                            {
                                OwnerUserIds.Add(userId);
                            }
                        }
                    }
                    // Make sure it has founder
                    if (HasFounder && !OwnerUserIds.Contains(FounderUserId))
                    {
                        OwnerUserIds.Add(FounderUserId);
                    }
                    // Make sure it has clan members
                    if (PLUGIN.config.Integration.Clans && HasFounder)
                    {
                        var memberIds = ClansHelper.GetClanMembers(FounderUserId);
                        foreach (var memberId in memberIds)
                        {
                            if (!OwnerUserIds.Contains(memberId))
                            {
                                OwnerUserIds.Add(memberId);
                            }
                        }
                    }
                    // Remove ignored users
                    foreach (var userId in OwnerUserIds.ToArray())
                    {
                        if (PLUGIN.permission.UserHasPermission(userId.ToString(), PermissionIgnore))
                        {
                            OwnerUserIds.Remove(userId);
                        }
                    }
                }
                catch (Exception) { }
            }

            [JsonIgnore]
            public int RemainingProtectionDelaySeconds
            {
                get
                {
                    if (SkipNextProtectionStartDelay)
                    {
                        return 0;
                    }
                    if (_timeWhenProtectionMayResume.HasValue)
                    {
                        var remaining = (int)Math.Floor((_timeWhenProtectionMayResume.Value - DateTime.Now).TotalSeconds);
                        if (remaining <= 0)
                        {
                            _timeWhenProtectionMayResume = null;
                            _protectionTimerReason = null;
                            return 0;
                        }
                        return remaining;
                    }
                    return 0;
                }
            }

            [JsonIgnore]
            public bool HasRemainingProtectionDelay => RemainingProtectionDelaySeconds > 0;

            public void SetProtectionStartDelayTime(int secondsToDelay, StatusReason reason)
            {
                if (secondsToDelay <= 0 || secondsToDelay <= RemainingProtectionDelaySeconds) { return; }
                _timeWhenProtectionMayResume = DateTime.Now.AddSeconds(secondsToDelay);
                _protectionTimerReason = reason;
                if (HasPendingProtectionTimer)
                {
                    // update the timer
                    var callback = _pendingProtectionTimer.Callback;
                    StopProtectionDelayTimer();
                    StartProtectionDelayTimer(RemainingProtectionDelaySeconds, callback);
                    SimpleStatusNeedsUpdate = true;
                }
            }

            public void StartProtectionDelayTimer(int secondsToDelay, Action action)
            {
                if (_pendingProtectionTimer != null && secondsToDelay <= RemainingProtectionDelaySeconds) { return; }
                StopProtectionDelayTimer();
                if (secondsToDelay <= 0)
                {
                    action.Invoke();
                    return;
                }
                _pendingProtectionTimer = PLUGIN.timer.In(secondsToDelay, action);
            }

            public void StopProtectionDelayTimer()
            {
                if (_pendingProtectionTimer != null && !_pendingProtectionTimer.Destroyed)
                {
                    _pendingProtectionTimer.Destroy();
                }
                _pendingProtectionTimer = null;
                SimpleStatusNeedsUpdate = true;
                //_lastProtectionTimerStartTime = null;
            }

            public virtual bool IsAuthed(BasePlayer basePlayer) => false;

            public bool IsOwner(BasePlayer basePlayer) => OwnerUserIds.Contains(basePlayer.UserId());

            public void SetBaseEntity(BaseEntity entity)
            {
                _baseEntity = entity;
                EntityID = entity.net.ID;
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class RaidProtection
    {
        private NullSafeDictionary<ulong, bool> CachedTugboatEntities = new NullSafeDictionary<ulong, bool>();

        Tugboat GetTugboat(BasePlayer basePlayer)
        {
            if (basePlayer == null || !AllowRaidProtectedTugboats) { return null; }
            return GetNearbyEntities<Tugboat>(basePlayer.transform.position, 0.005f).FirstOrDefault();
        }

        public BaseEntity GetTugboatParent(BaseCombatEntity entity)
        {
            if (entity == null || !AllowRaidProtectedTugboats || entity.net == null || entity.net.ID == null) { return null; }
            var entityId = entity.net.ID.Value;
            if (!CachedTugboatEntities.ContainsKey(entityId))
            {
                if (entity is Tugboat)
                {
                    CachedTugboatEntities[entityId] = true;
                    return entity;
                }
                var parent = entity.GetParentEntity();
                if (parent == null)
                {
                    CachedTugboatEntities[entityId] = false;
                    return null;
                }
                else if (parent is Tugboat)
                {
                    CachedTugboatEntities[entityId] = true;
                    return parent;
                }
            }
            return CachedTugboatEntities[entityId] ? entity.GetParentEntity() : null;
        }

        public class ProtectedTugboat : ProtectedEntity
        {
            public ProtectedTugboat() { }

            public ProtectedTugboat(Tugboat priv)
            {
                this.EntityID = priv.net.ID;
                ClearCache(priv: true, building: true, costs: true);
            }

            private Tugboat _tugBoat = null;
            private VehiclePrivilege _vehiclePrivilege = null;

            [JsonIgnore]
            public BasePlayer Driver => Tugboat?.GetDriver();

            [JsonIgnore]
            public bool HasDriver => Tugboat.HasDriver();

            [JsonIgnore]
            public Tugboat Tugboat
            {
                get
                {
                    if (_tugBoat == null)
                    {
                        _tugBoat = (Tugboat)BaseEntity;
                    }
                    return _tugBoat;
                }
            }

            [JsonIgnore]
            public VehiclePrivilege VehiclePrivilege
            {
                get
                {
                    if (_vehiclePrivilege == null)
                    {
                        foreach (BaseEntity child in Tugboat.children)
                        {
                            VehiclePrivilege vehiclePrivilege = child as VehiclePrivilege;
                            if (!(vehiclePrivilege == null))
                            {
                                _vehiclePrivilege = vehiclePrivilege;
                                break;
                            }
                        }
                    }
                    return _vehiclePrivilege;
                }
            }

            [JsonIgnore]
            public override List<ulong> AuthedPlayerUserIds
            {
                get
                {
                    return VehiclePrivilege == null ? new List<ulong>() : VehiclePrivilege.authorizedPlayers.Select(x => x.userid).ToList();
                }
            }

            [JsonIgnore]
            public override List<Item> InventoryItemList
            {
                get
                {
                    return Enumerable.Concat(Driver.inventory.containerMain?.itemList ?? Enumerable.Empty<Item>(), Enumerable.Concat(Driver.inventory.containerBelt?.itemList ?? Enumerable.Empty<Item>(), Driver.inventory.containerWear?.itemList ?? Enumerable.Empty<Item>())).ToList() ?? new List<Item>();
                }
            }

            public override bool IsAuthed(BasePlayer basePlayer)
            {
                return VehiclePrivilege?.IsAuthed(basePlayer) ?? false;
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class RaidProtection
    {
        public class ProtectionLevel
        {
            public static ProtectionLevel NONE { get; set; } = new ProtectionLevel() { OnlineProtectionPercent = 0, OfflineProtectionPercent = 0};
            [JsonProperty(PropertyName = "Rank")]
            public int Rank { get; set; } = 0;

            [JsonProperty(PropertyName = "Online protection percentage (0-100)")]
            public float OnlineProtectionPercent { get; set; } = 0;

            [JsonProperty(PropertyName = "Offline protection percentage (0-100)")]
            public float OfflineProtectionPercent { get; set; } = 0;

            [JsonProperty(PropertyName = "Hourly cost per authorized player")]
            public float HourlyCostPerOwner { get; set; } = 0;

            [JsonProperty(PropertyName = "Hourly cost per floor")]
            public float HourlyCostPerFloor { get; set; } = 0;

            [JsonProperty(PropertyName = "Hourly base cost")]
            public float HourlyBaseCost { get; set; } = 0;

            [JsonProperty(PropertyName = "Cost per damage protected")]
            public float CostPerDamageProtected { get; set; } = 0f;

            [JsonProperty(PropertyName = "Max protection time (hours)")]
            public int? MaxProtectionTimeHours { get; set; } = null;

            [JsonProperty(PropertyName = "Delay for offline protection (seconds)")]
            public int OfflineProtectionDelay { get; set; } = 5;

            [JsonProperty(PropertyName = "Delay after taking damage (seconds)")]
            public int ProtectedDelayAfterTakingDamage { get; set; } = 10;

            [JsonProperty(PropertyName = "Damage resets timer when owner is offline")]
            public bool DamageResetsTimerWhenOwnerIsOffline { get; set; } = true;

            [JsonProperty(PropertyName = "Allow tugboat protection")]
            public bool AllowTugboatProtection { get; set; } = true;

            [JsonProperty(PropertyName = "Founder Limit")]
            public int? FounderLimit { get; set; } = null;

            [JsonIgnore]
            public bool HasFounderLimit{ get { return FounderLimit != null && FounderLimit > 0; } }

            [JsonIgnore]
            public bool HasOnlineProtection { get { return OnlineProtectionPercent > 0f; } }

            [JsonIgnore]
            public bool HasOfflineProtection { get { return OfflineProtectionPercent > 0f; } }

            [JsonIgnore]
            public bool HasProtectionTimeLimit { get { return MaxProtectionTimeHours != null; } }

            public static ProtectionLevel GetByRankNullable(int rank)
            {
                if (rank <= 0)
                {
                    return ProtectionLevel.NONE;
                }
                var pl = PLUGIN.config.Protection.ProtectionLevels.FirstOrDefault(x => x.Rank == rank);
                if (pl == null)
                {
                    pl = PLUGIN.config.Protection.ProtectionLevels.OrderByDescending(x => x.Rank).FirstOrDefault();
                }
                return pl;
            }

            public static ProtectionLevel GetProtectionLevelOfPlayer(ulong userId)
            {
                try
                {
                    var levels = PLUGIN.config.Protection.ProtectionLevels.OrderByDescending(x => x.Rank);
                    foreach (var pl in levels)
                    {
                        var i = pl.Rank;
                        if (PLUGIN.permission.UserHasPermission(userId.ToString(), PLUGIN.PermissionLevel + i) || PLUGIN.permission.GetUserGroups(userId.ToString()).Any(groupid => PLUGIN.permission.GroupHasPermission(groupid, PLUGIN.PermissionLevel + i)))
                        {
                            return pl;
                        }
                    }
                    return NONE;
                } catch (NullReferenceException) { return NONE; }
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class RaidProtection
    {
        public enum ProtectionStatus
        {
            Unprotected,
            Pending,
            Protected
        }

        public static string ProtectionStatusToString(ProtectedEntity tc, BasePlayer basePlayer, ProtectionStatus status)
        {
            switch (status)
            {
                case ProtectionStatus.Protected:
                    return PLUGIN.Capitalize(PLUGIN.Lang("status protected", basePlayer));
                case ProtectionStatus.Pending:
                    return PLUGIN.Capitalize(PLUGIN.Lang("status pending", basePlayer));
                case ProtectionStatus.Unprotected:
                    return PLUGIN.Capitalize(PLUGIN.Lang("status unprotected", basePlayer));
                default:
                    return PLUGIN.Capitalize(PLUGIN.Lang("status unprotected", basePlayer));
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class RaidProtection
    {
        struct LastKilled : INullable
        {
            public Vector3 Position;
            public ulong ID;
            public ProtectedEntity tc;

            public bool IsNull => Position == null;
        }

        LastKilled lastKilled;

        void OnEntitySpawned(BuildingPrivlidge priv)
        {
            NextTick(() =>
            {
                if (priv != null)
                {
                    if (!lastKilled.IsNull && lastKilled.Position.Equals(priv.transform.position))
                    {
                        // Logic to transfer cupboard
                        var oldTc = lastKilled.tc;
                        timer.In(1f, () =>
                        {
                            ProtectedCupboardManager.Transfer(lastKilled.tc, priv);
                        });
                    }
                    else
                    {
                        AddProtectedCupboard(priv);
                        var tc = ProtectedCupboardManager.InitProtectedCupboard(priv);
                        if (tc == null) { return; }
                        var basePlayer = tc.Founder;
                        if (basePlayer != null)
                        {
                            var pl = ProtectionLevel.GetProtectionLevelOfPlayer(basePlayer.UserId());
                            if (pl != null && pl.HasFounderLimit)
                            {
                                var count = ProtectedCupboardManager.GetCupboardFounderCount(basePlayer.UserId());
                                if (count > pl.FounderLimit)
                                {
                                    tc.DisableProtectionFromExceedingFounderLimit = true;
                                    StopProtection(tc);
                                    Message(basePlayer, "founder limit reached", pl.FounderLimit);
                                }
                                else
                                {
                                    Message(basePlayer, "founder limit warning", count, pl.FounderLimit);
                                }
                            }
                        }
                    }
                }
            });
        }

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (plan == null || go?.name == "assets/prefabs/deployable/tool cupboard/cupboard.tool.deployed.prefab") return;
            BuildingPrivlidge priv = plan.GetBuildingPrivilege();
            if (priv != null)
            {
                var tc = ProtectedCupboardManager.InitProtectedCupboard(priv);
                if (tc != null)
                {
                    UpdateProtectedStructure(tc);
                }
            }
        }

        void OnEntityKill(BuildingBlock block)
        {
            BuildingPrivlidge priv = block.GetBuildingPrivilege();
            if (priv != null)
            {
                NextTick(() =>
                {
                    if (priv != null)
                    {
                        var tc = ProtectedCupboardManager.InitProtectedCupboard(priv);
                        if (tc != null)
                        {
                            UpdateProtectedStructure(tc);
                        }
                    }
                });
            }
        }

        void OnEntityKill(BuildingPrivlidge priv)
        {
            if (priv != null)
            {
                lastKilled = new LastKilled
                {
                    Position = priv.transform.position,
                    ID = priv.net.ID.Value,
                    tc = ProtectedCupboardManager.GetByID(priv.net.ID.Value)
                };
                if (ProtectedCupboardManager.ProtectedCupboardExists(priv.net.ID.Value))
                {
                    var tc = ProtectedCupboardManager.InitProtectedCupboard(priv);
                    RemoveProtectedCupboard(tc);
                }
            }
        }

        void OnEntityDeath(BuildingBlock block, HitInfo hitInfo) => OnEntityKill(block);

        void OnEntityDeath(BuildingPrivlidge priv, HitInfo hitInfo)
        {
            if (priv != null && hitInfo != null)
            {
                if (ProtectedCupboardManager.ProtectedCupboardExists(priv.net.ID.Value))
                {
                    var tc = ProtectedCupboardManager.InitProtectedCupboard(priv);
                    DestroyProtectedCupboard(tc, hitInfo.Initiator);
                }
            }
        }


        void OnCupboardAuthorize(VehiclePrivilege priv, BasePlayer basePlayer)
        {
            var parent = priv.GetParentEntity() as Tugboat;
            if (parent == null) { return; }
            OnAuthorizeHelper(parent, basePlayer);
        }

        void OnCupboardAuthorize(BuildingPrivlidge priv, BasePlayer basePlayer)
        {
            OnAuthorizeHelper(priv, basePlayer);
        }

        private void OnAuthorizeHelper(BaseEntity priv, BasePlayer basePlayer)
        {
            NextTick(() =>
            {
                if (priv != null)
                {
                    var tc = ProtectedCupboardManager.InitProtectedCupboard(priv);
                    OwnerAuthorized(tc, basePlayer);
                    UpdateOwners(tc);
                }
            });
        }

        void OnCupboardClearList(VehiclePrivilege priv, BasePlayer basePlayer)
        {
            var parent = priv.GetParentEntity() as Tugboat;
            if (parent == null) { return; }
            OnCupboardClearListHelper(parent, basePlayer);
        }

        void OnCupboardClearList(BuildingPrivlidge priv, BasePlayer basePlayer)
        {
            OnCupboardClearListHelper(priv, basePlayer);
        }

        private void OnCupboardClearListHelper(BaseEntity priv, BasePlayer basePlayer)
        {
            NextTick(() =>
            {
                if (priv != null)
                {
                    var tc = ProtectedCupboardManager.InitProtectedCupboard(priv);
                    if (config.Protection.RemoveAdminOwners)
                    {
                        foreach (var adminPlayer in tc.AuthedPlayers.Where(x => PLUGIN.IsAdmin(x)))
                        {
                            AdminOwnerDeauthorized(tc, basePlayer);
                        }
                    }
                    UpdateOwners(tc);
                }
            });
        }

        void OnCupboardDeauthorize(VehiclePrivilege priv, BasePlayer basePlayer)
        {
            var parent = priv.GetParentEntity() as Tugboat;
            if (parent == null) { return; }
            OnCupboardDeauthorizeHelper(parent, basePlayer);
        }

        void OnCupboardDeauthorize(BuildingPrivlidge priv, BasePlayer basePlayer)
        {
            OnCupboardDeauthorizeHelper(priv, basePlayer);
        }

        private void OnCupboardDeauthorizeHelper(BaseEntity priv, BasePlayer basePlayer)
        {
            NextTick(() =>
            {
                if (priv != null)
                {
                    var tc = ProtectedCupboardManager.InitProtectedCupboard(priv);
                    if (IsAdmin(basePlayer.IPlayer) && config.Protection.RemoveAdminOwners)
                    {
                        AdminOwnerDeauthorized(tc, basePlayer);
                    }
                    UpdateOwners(tc);
                }
            });
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is AnimatedBuildingBlock || entity is SimpleBuildingBlock || entity is BuildingBlock)
            {
                return config.Protection.ProtectedEntities.Buildings ? OnEntityTakeDamageHelper(entity, info) : null;
            }
            else if (entity is LootContainer)
            {
                return config.Protection.ProtectedEntities.LootNodes ? OnEntityTakeDamageHelper(entity, info) : null;
            }
            else if (entity is BaseTrap || entity is Barricade || entity is GunTrap || entity is FlameTurret || entity is TeslaCoil || entity is SamSite || entity is AutoTurret)
            {
                return config.Protection.ProtectedEntities.Traps ? OnEntityTakeDamageHelper(entity, info) : null;
            }
            else if (entity is Horse)
            {
                return config.Protection.ProtectedEntities.Horses ? OnEntityTakeDamageHelper(entity, info) : null;
            }
            else if (entity is BaseVehicle)
            {
                return config.Protection.ProtectedEntities.Vehicles ? OnEntityTakeDamageHelper(entity, info) : null;
            }
            else if (entity is BaseAnimalNPC)
            {
                return config.Protection.ProtectedEntities.Animals ? OnEntityTakeDamageHelper(entity, info) : null;
            }
            else if (entity is NPCPlayer)
            {
                return config.Protection.ProtectedEntities.NPCs ? OnEntityTakeDamageHelper(entity, info) : null;
            }
            else if (entity is BasePlayer && !entity.IsNpc)
            {
                return (config.Protection.ProtectedEntities.AuthedPlayers || config.Protection.ProtectedEntities.UnauthedPlayers) ? OnEntityTakeDamagePlayerHelper(entity as BasePlayer, info) : null;
            }
            else if (entity is IOEntity)
            {
                return config.Protection.ProtectedEntities.Electrical ? OnEntityTakeDamageHelper(entity, info) : null;
            }
            else if (entity is DecayEntity || entity is StorageContainer)
            {
                return config.Protection.ProtectedEntities.Containers ? OnEntityTakeDamageHelper(entity, info) : null;
            }
            return null;
        }

        void OnLootEntity(BasePlayer basePlayer, StorageContainer entity)
        {
            if (entity == null || basePlayer == null || !AllowRaidProtectedTugboats) { return; }
            var tug = entity.GetParentEntity() as Tugboat;
            if (tug == null || entity.GetPanelName() != "fuelsmall" || basePlayer == null || !(tug is Tugboat)) { return; }
            var tc = ProtectedCupboardManager.InitProtectedCupboard(tug);
            if (tc == null) { return; }
            OpenProtectedCupboard(tc, basePlayer, false);
        }

        void OnLootEntity(BasePlayer basePlayer, BuildingPrivlidge priv)
        {
            if (basePlayer != null && priv != null)
            {
                var tc = ProtectedCupboardManager.InitProtectedCupboard(priv);
                if (tc != null)
                {
                    OpenProtectedCupboard(tc, basePlayer);
                }
            }
        }

        void OnLootEntityEnd(BasePlayer basePlayer, StorageContainer entity)
        {
            if (entity == null || basePlayer == null || !AllowRaidProtectedTugboats) { return; }
            var tug = entity.GetParentEntity() as Tugboat;
            if (tug == null || basePlayer == null || !(tug is Tugboat)) { return; }
            var tc = ProtectedCupboardManager.InitProtectedCupboard(tug);
            if (tc == null) { return; }
            CloseProtectedCupboard(tc, basePlayer);
        }

        void OnLootEntityEnd(BasePlayer basePlayer, BuildingPrivlidge priv)
        {
            if (basePlayer != null)
            {
                var tc = ProtectedCupboardManager.InitProtectedCupboard(priv);
                if (tc != null)
                {
                    CloseProtectedCupboard(tc, basePlayer);
                }
            }
        }


        object OnLootNetworkUpdate(PlayerLoot loot)
        {
            if (loot.entitySource != null && loot.entitySource is BuildingPrivlidge)
            {
                var tc = ProtectedCupboardManager.InitProtectedCupboard((BuildingPrivlidge)loot.entitySource);
                if (tc != null)
                {
                    BasePlayer basePlayer = loot._baseEntity;
                    if (basePlayer != null)
                    {
                        UpdateProtectedCupboardInventory(tc, basePlayer);
                        //OpenProtectedCupboard(tc, basePlayer);
                    }
                }
            }
            return null;
        }

        void OnItemDropped(Item item, DroppedItem droppedItem)
        {
            if (item == null || droppedItem == null) { return; }
            var basePlayer = item.GetOwnerPlayer();
            if (basePlayer == null || !basePlayer.isMounted) { return; }
            var tug = basePlayer.GetMountedVehicle() as Tugboat;
            if (tug == null) { return; }
            var tc = ProtectedCupboardManager.InitProtectedCupboard(tug);
            if (tc == null || !tc.IsViewingProtectionPanel(basePlayer)) { return; }
            NextTick(() =>
            {
                UpdateProtectedCupboardInventory(tc, basePlayer);
            });
            
        }

        private void OnPlayerConnected(BasePlayer basePlayer)
        {
            if (!SimpleStatusHelper.IsLoaded()) { return; }
            AddBehavior(basePlayer);
        }

        private void OnPlayerDisconnected(BasePlayer basePlayer)
        {
            if (!SimpleStatusHelper.IsLoaded() || basePlayer == null) { return; }
            RemoveAndDestroyBehavior(basePlayer.UserId());
        }

        void OnPlayerSleepEnded(BasePlayer basePlayer)
        {
            if (!SimpleStatusHelper.IsLoaded()) { return; }
            AddBehavior(basePlayer);
            var priv = GetProtectedEntity(basePlayer);
            if (priv == null) { return; }
            var tc = ProtectedCupboardManager.InitProtectedCupboard(priv);
            if (tc == null) { return; }
            SimpleStatusHelper.SetProtectionStatus(basePlayer, tc);
        }

        void OnUserConnected(IPlayer player)
        {
            NextTick(() =>
            {
                if (player != null)
                {
                    var cupboards = ProtectedCupboardManager.GetCupboardsForOwner(ulong.Parse(player.Id));
                    foreach (var tc in cupboards)
                    {
                        UpdateOnlineOwners(tc);
                    }
                }
            });
        }

        void OnUserDisconnected(IPlayer player)
        {
            NextTick(() =>
            {
                if (player != null)
                {
                    var cupboards = ProtectedCupboardManager.GetCupboardsForOwner(ulong.Parse(player.Id));
                    foreach (var tc in cupboards)
                    {
                        UpdateOnlineOwners(tc);
                    }
                }
            });
        }

        void OnUserPermissionGranted(string id, string permName)
        {
            if (permName.StartsWith(PermissionLevel) || permName.StartsWith(PermissionIgnore))
            {
                UpdatedPermission(id);
            }
        }

        void OnUserPermissionRevoked(string id, string permName)
        {
            if (permName.StartsWith(PermissionLevel) || permName.StartsWith(PermissionIgnore))
            {
                UpdatedPermission(id);
            }
        }

        void OnGroupPermissionGranted(string name, string permName)
        {
            if (permName.StartsWith(PermissionLevel) || permName.StartsWith(PermissionIgnore))
            {
                var users = players.All.Where(p => permission.UserHasGroup(p.Id, name)).Select(x => x.Id).ToList();
                foreach (var userIdString in users)
                {
                    UpdatedPermission(userIdString);
                }
            }
        }

        void OnGroupPermissionRevoked(string name, string permName)
        {
            if (permName.StartsWith(PermissionLevel) || permName.StartsWith(PermissionIgnore))
            {
                permission.GetUsersInGroup(name);
                var users = players.All.Where(p => permission.UserHasGroup(p.Id, name)).Select(x => x.Id).ToList();
                foreach (var userIdString in users)
                {
                    UpdatedPermission(userIdString);
                }
            }
        }

        void OnPlayerDeath(BasePlayer basePlayer, HitInfo info)
        {
            if (basePlayer != null && config.Integration.CustomStatusFramework && config.CustomStatusFramework.ShowWhenHammerEquipped)
            {
                CustomStatusFrameworkHelper.DestroyProtectionStatusPopup(basePlayer);
            }
        }

        void OnActiveItemChanged(BasePlayer basePlayer, Item oldItem, Item newItem)
        {
            if (!((config.Integration.CustomStatusFramework && config.CustomStatusFramework.ShowWhenHammerEquipped) || (config.Integration.SimpleStatus && config.SimpleStatus.ShowWhenHoldingHammer))) { return; }
            if (basePlayer != null && newItem != null && newItem.info?.shortname == "hammer")
            {
                var priv = GetProtectedEntity(basePlayer);
                if (priv != null)
                {
                    var tc = ProtectedCupboardManager.GetByID(priv.net.ID.Value);
                    if (tc != null)
                    {
                        if (SimpleStatusHelper.IsLoaded())
                        {
                            SimpleStatusHelper.SetProtectionStatus(basePlayer, tc, hammer: true);
                        }
                        if (tc.IsOwner(basePlayer))
                        {
                            CustomStatusFrameworkHelper.ShowOwnerPopupProtectionStatus(basePlayer, tc);
                        }
                        else
                        {
                            CustomStatusFrameworkHelper.ShowNonOwnerPopupProtectionStatus(basePlayer, tc);
                        }
                    }
                }
            }
            else if (basePlayer != null && newItem?.info.shortname != "hammer" && oldItem?.info.shortname == "hammer")
            {
                CustomStatusFrameworkHelper.DestroyProtectionStatusPopup(basePlayer);
                if (SimpleStatusHelper.IsLoaded())
                {
                    SimpleStatusHelper.ClearProtectionStatus(basePlayer);
                }
            }
        }

        #region Helper Methods
        private object OnEntityTakeDamagePlayerHelper(BasePlayer entity, HitInfo info)
        {
            if (entity == null || info == null) { return null; }
            var priv = GetProtectedEntity(entity);
            if (priv == null) { return null; }
            var tc = ProtectedCupboardManager.InitProtectedCupboard(priv);
            if (tc == null) { return null; }
            var authed = tc.IsAuthed(entity);
            if ((authed && config.Protection.ProtectedEntities.AuthedPlayers) || (!authed && config.Protection.ProtectedEntities.UnauthedPlayers))
            {
                return DamageProtectedStructure(tc, entity, info);
            }
            return null;
        }

        private object OnEntityTakeDamageHelper(BaseCombatEntity entity, HitInfo info)
        {
            if (entity != null && info != null)
            {
                if (entity is BuildingBlock)
                {
                    var buildingBlock = entity as BuildingBlock;
                    if (buildingBlock.grade == BuildingGrade.Enum.Twigs && !config.Protection.ProtectTwig)
                    {
                        return null;
                    }
                }
                var priv = GetProtectedEntity(entity);
                if (priv != null)
                {
                    var tc = ProtectedCupboardManager.InitProtectedCupboard(priv);
                    return DamageProtectedStructure(tc, entity, info);
                }
            }
            return null;
        }

        private void UpdatedPermission(string id)
        {
            NextTick(() =>
            {
                if (!string.IsNullOrEmpty(id))
                {
                    var cupboards = ProtectedCupboardManager.GetCupboardsForFounder(ulong.Parse(id));
                    foreach (var tc in cupboards)
                    {
                        UpdateOwners(tc);
                    }
                }
            });
        }

        private bool IsProtectedEntity(BaseEntity entity, ProtectedEntity tc)
        {
            if (entity == null || tc == null)
            {
                return false;
            }
            else if (entity is AnimatedBuildingBlock || entity is BuildingBlock || entity is SimpleBuildingBlock)
            {
                return config.Protection.ProtectedEntities.Buildings;
            }
            else if (entity is BaseTrap || entity is Barricade || entity is GunTrap || entity is FlameTurret || entity is TeslaCoil || entity is SamSite || entity is AutoTurret) 
            {
                return config.Protection.ProtectedEntities.LootNodes;
            }
            else if (entity is LootContainer)
            {
                return config.Protection.ProtectedEntities.LootNodes;
            }
            else if (entity is DecayEntity || entity is StorageContainer)
            {
                return config.Protection.ProtectedEntities.Containers;
            }
            else if (entity is Horse)
            {
                return config.Protection.ProtectedEntities.Horses;
            }
            else if (entity is BaseVehicle)
            {
                return config.Protection.ProtectedEntities.Vehicles;
            }
            else if (entity is BaseNpc || entity is BaseAnimalNPC)
            {
                return config.Protection.ProtectedEntities.NPCs;
            }
            else if (entity is BasePlayer)
            {
                return tc.AuthedPlayerUserIds
                    .Any(x => x == ((BasePlayer)entity).userID)
                    ? config.Protection.ProtectedEntities.AuthedPlayers : config.Protection.ProtectedEntities.UnauthedPlayers;
            }
            return false;
        }
        #endregion

        #region Clans Hooks
        private void OnClanMemberJoined(string userID, List<string> memberUserIDs)
        {
            if (config.Integration.Clans)
            {
                ClanUpdated(memberUserIDs);
            }
        }

        private void OnClanGone(string userID, List<string> memberUserIDs)
        {
            if (config.Integration.Clans)
            {
                ClanUpdated(memberUserIDs);
            }
        }

        private void OnClanDisbanded(List<string> memberUserIDs)
        {
            if (config.Integration.Clans)
            {
                ClanUpdated(memberUserIDs);
            }
        }

        private void ClanUpdated(List<string> memberUserIDs)
        {
            foreach(var userId in memberUserIDs)
            {
                var tcs = ProtectedCupboardManager.GetCupboardsForFounder(ulong.Parse(userId));
                foreach(var tc in tcs)
                {
                    UpdateOwners(tc);
                }
            }
        }
        #endregion

        #region SkillTree Hooks
        private object STOnLockpickAttempt(BasePlayer basePlayer, BaseLock baseLock)
        {
            if (!config.Integration.SkillTree) { return null; }
            if (basePlayer == null || baseLock == null) { return null; }
            var priv = baseLock.GetBuildingPrivilege();
            if (priv == null) { return null; }
            var tc = ProtectedCupboardManager.GetByID(priv.net.ID.Value);
            if (tc == null) { return null; }
            if (tc.IsProtected) { return true; }
            return null;
        }
        #endregion
    }
}

namespace Oxide.Plugins
{
    partial class RaidProtection
    {
        public enum StatusReason
        {
            NoReason,
            Paused,
            OfflineOnly,
            PendingOfflineProtection,
            OnlineOnly,
            RecentlyTookDamage,
            InsufficientItem,
            InsufficientBalance,
            NoPermission,
            NoFounder,
            PendingOfflineOnly,
            Pending,
            ExceedingFounderLimit
        }

        public static string StatusReasonToString(ProtectedEntity tc, BasePlayer basePlayer, StatusReason reason)
        {
            switch (reason)
            {
                case StatusReason.Paused:
                    return PLUGIN.Capitalize(PLUGIN.Lang("reason paused", basePlayer));
                case StatusReason.OfflineOnly:
                    return PLUGIN.Capitalize(PLUGIN.Lang("reason offline only", basePlayer, tc.OfflineProtectionPercent, PLUGIN.FormatTimeShort(tc.HoursOfProtection, false)));
                case StatusReason.OnlineOnly:
                    return PLUGIN.Capitalize(PLUGIN.Lang("reason online only", basePlayer, tc.OnlineProtectionPercent, PLUGIN.FormatTimeShort(tc.HoursOfProtection, false)));
                case StatusReason.RecentlyTookDamage:
                    return PLUGIN.Capitalize(PLUGIN.Lang("reason recently damaged", basePlayer));
                case StatusReason.InsufficientItem:
                   return PLUGIN.Capitalize(PLUGIN.Lang("reason insufficient item", basePlayer, tc.CurrencyItemRemainingAmountRequired, PLUGIN.config.Protection.CurrencyItem));
                case StatusReason.InsufficientBalance:
                    return PLUGIN.Capitalize(PLUGIN.Lang("reason insufficient balance", basePlayer));
                case StatusReason.NoPermission:
                    return PLUGIN.Capitalize(PLUGIN.Lang("reason no permission", basePlayer));
                case StatusReason.NoFounder:
                    return tc.IsTugboat ? PLUGIN.Capitalize(PLUGIN.Lang("reason tugboat auth", basePlayer)) : PLUGIN.Capitalize(PLUGIN.Lang("reason no founder", basePlayer));
                case StatusReason.PendingOfflineOnly:
                    return PLUGIN.Capitalize(PLUGIN.Lang("reason pending offline only", basePlayer, tc.RemainingProtectionDelaySeconds));
                case StatusReason.Pending:
                    return PLUGIN.Capitalize(PLUGIN.Lang("reason pending", basePlayer));
                case StatusReason.ExceedingFounderLimit:
                    return PLUGIN.Capitalize(PLUGIN.Lang("reason founder limit", basePlayer));
                default:
                    return "No protection";
            }
        }
    }
}

namespace Oxide.Plugins
{
	partial class RaidProtection
	{
		private static string ColorToHex(string color)
		{
			var split = color.Split(' ');
			var r = (int)Math.Round(float.Parse(split[0]) * 255f);
			var g = (int)Math.Round(float.Parse(split[1]) * 255f);
			var b = (int)Math.Round(float.Parse(split[2]) * 255f);
			return "#" + r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
		}
		private string Opacity(string color, float opacity)
        {
			var split = color.Split(' ');
			return $"{split[0]} {split[1]} {split[2]} {opacity}";
        }

		public static IPlayer FindPlayer(ulong userId)
        {
			return PLUGIN.covalence.Players.FindPlayerById(userId.ToString());
        }

		public static List<IPlayer> FindPlayers(IEnumerable<ulong> userIds)
		{
			return userIds.Select(x => FindPlayer(x)).Where(x => x != null).ToList();
		}

		private bool GiveBalanceResource(BasePlayer basePlayer, double amount)
		{
			if (IsNull(basePlayer)) { return false; }
			var userId = basePlayer.UserId();
			if (config.Integration.Economics)
			{
				Economics?.Call("Deposit", userId, amount);
				return true;
			}
			else if (config.Integration.ServerRewards)
			{
				ServerRewards?.Call("AddPoints", userId, (int)Math.Ceiling(amount));
				return true;
			}
			else
			{
				var item = ItemManager.Create(CurrencyItemDef, (int)Math.Floor(amount));
				if (IsNull(item)) return false;
				basePlayer.GiveItem(item);
				return true;
			}
		}

		private bool ConsumeBalanceResource(ProtectedEntity tc, BasePlayer basePlayer, double amount)
		{
			var entity = tc.BaseEntity;
			if (IsNull(basePlayer)) { return false; }
			if (entity != null)
            {
				var userId = basePlayer.UserId();
				if (config.Integration.Economics)
                {
					Economics?.Call("Withdraw", userId, amount);
					return true;
				}
				else if (config.Integration.ServerRewards)
                {
					ServerRewards?.Call("TakePoints", userId, (int)Math.Ceiling(amount));
					return true;
				}
				else
                {
					var due = (int)Math.Ceiling(amount);
					foreach (var item in tc.InventoryItemList.ToArray())
					{
						if (item.info.shortname.ToLower() == config.Protection.CurrencyItem.ToLower())
						{
							if (item.amount <= due)
							{
								due -= item.amount;
								item.RemoveFromContainer();
							}
							else
							{
								item.UseItem(due);
								due -= item.amount;
							}
						}
						if (due <= 0)
                        {
							return true;
						}
					}
					return true;
				}
			}
			return false;
		}

		private ItemDefinition _currencyItemDef = null;
		private ItemDefinition CurrencyItemDef
        {
			get
            {
				if (_currencyItemDef == null)
                {
					_currencyItemDef = ItemManager.FindItemDefinition(config.Protection.CurrencyItem);
                }
				return _currencyItemDef;
            }
        }

		private string FormatTimeShort(float hours, bool seconds = true)
		{
			if (hours >= float.MaxValue)
            {
				return $"forever";
            }
			var time = TimeSpan.FromHours(hours);
			return $"{time.Days}d {time.Hours}h {time.Minutes}m{(seconds ? ($" {time.Seconds}s") : String.Empty)}";
		}

		private string FormatCurrency(string userIdString, float? amount, bool round = false)
		{
			if (amount == null)
            {
				amount = 0;
            }
			if (config.Integration.Economics)
			{
				if (round)
				{
					amount = (float?)Math.Round(amount.Value, 2);
				}
				return Lang("economics currency format", userIdString, amount);
			}
			if (config.Integration.ServerRewards)
			{
				if (round)
                {
					amount = (float?) Math.Floor(amount.Value);
                }
				return Lang("server rewards currency format", userIdString, amount);
			}
			if (round)
			{
				amount = (float?)Math.Floor(amount.Value);
			}
			return $"{amount} {CurrencyItemDef.displayName.translated}";
		}

		private double GetBalanceResourceAmount(ProtectedEntity tc, BasePlayer basePlayer)
		{
			var entity = tc.BaseEntity;
			if (entity != null)
			{
				if (config.Integration.Economics)
				{
					var userId = basePlayer.UserId();
					var bal = (double)Economics?.Call("Balance", userId);
					return bal;
				}
				else if (config.Integration.ServerRewards)
				{
					var userId = basePlayer.UserId();
					int? points = (int?)ServerRewards?.Call("CheckPoints", userId);
					return points ?? 0;
				}
				else
				{
					var count = 0;
					foreach (var item in tc.InventoryItemList)
					{
						if (item.info.shortname.ToLower() == config.Protection.CurrencyItem.ToLower())
						{
							count += item.amount;
						}
					}
					return count;
				}
			}
			return 0;
		}

		private bool HasBalanceResourceAmount(ProtectedEntity tc, BasePlayer basePlayer, double amount)
        {
			var entity = tc.BaseEntity;
			if (entity != null)
            {
				if (config.Integration.Economics)
				{
					if (IsNull(basePlayer)) { return false; }
					var userId = basePlayer.UserId();
					var bal = (double)Economics?.Call("Balance", userId, amount);
					var due = amount;
					return bal >= due;
				}
				else if (config.Integration.ServerRewards)
				{
					if (IsNull(basePlayer)) { return false; }
					var userId = basePlayer.UserId();
					int? points = (int?)ServerRewards?.Call("CheckPoints", userId);
					var due = (int)Math.Ceiling(amount);
					return points != null && points >= due;
				}
				else
				{
					var due = (int)Math.Ceiling(amount);
					var count = 0;
					foreach (var item in tc.InventoryItemList)
					{
						if (item.info.shortname.ToLower() == config.Protection.CurrencyItem.ToLower())
						{
							count += item.amount;
							if (count >= due)
                            {
								return true;
                            }
						}
					}
				}
			}
			return false;
		}

		private string GetEntityDisplayName(BaseEntity baseEntity)
        {
			if (baseEntity == null)
            {
				return "None";
            }
			if (baseEntity is BasePlayer && !baseEntity.IsNpc)
            {
				return ((BasePlayer)baseEntity).displayName;
            }
			return baseEntity.name;
        }

		private string ID(string parent, string id, bool guid = false)
        {
			return $"{parent}.{id}{(guid ? Guid.NewGuid().ToString() : string.Empty)}";
        }

		private string Capitalize(string str)
        {
			if (str == null)
				return null;
			if (str.Length > 1)
				return char.ToUpper(str[0]) + str.Substring(1);
			return str.ToUpper();
		}

		private void MessageNonLocalized(BasePlayer basePlayer, string message)
		{
			var icon = config.ChatMessageIconId;
			if (basePlayer == null)
			{
				Puts(message);
			}
			else
            {
				ConsoleNetwork.SendClientCommand(basePlayer.Connection, "chat.add", 2, icon, message);
			}
		}

		private void Message(IPlayer iPlayer, string lang, params object[] args) => Message(iPlayer.Object as BasePlayer, lang, args);

		private void Message(BasePlayer basePlayer, string lang, params object[] args)
		{
			if (basePlayer == null)
            {
				Puts(Lang(lang, args));
            }
			else
            {
				MessageNonLocalized(basePlayer, Lang(lang, basePlayer, args));
			}
		}

		private T LoadDataFile<T>(string fileName)
		{
			try
			{
				return Interface.Oxide.DataFileSystem.ReadObject<T>($"{Name}/{fileName}");
			}
			catch (Exception ex)
			{
				PrintError(ex.ToString());
				return default(T);
			}
		}

        private void SaveDataFile<T>(string fileName, T data)
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/{fileName}", data);
        }

		private bool IsAdmin(IPlayer player)
        {
			if (player == null) { return false; }
			return permission.UserHasPermission(player.Id, PermissionAdmin);
        }

		private bool IsIgnored(string userIdString)
        {
			return permission.UserHasPermission(userIdString, PermissionIgnore);
		}

		private bool IsNpcBase(BaseEntity entity)
		{
			if (!(entity is BuildingPrivlidge)) { return false; }
			if (entity == null) { return false; }
			var priv = (BuildingPrivlidge)entity;
			BuildingManager.Building building = priv.GetBuilding();
			if (building != null && building.buildingBlocks != null)
			{
				if (building.buildingBlocks.Count > 0)
				{
					if (building.buildingBlocks.First().OwnerID == 0)
					{
						return true;
					}
				}
			}
			return false;
		}

		List<T> GetNearbyEntities<T>(Vector3 position, float radius) where T : BaseEntity
		{
			List<T> entities = new List<T>();
			Vis.Entities(position, radius, entities);
			return entities;
		}

		public BaseEntity GetProtectedEntity(BasePlayer basePlayer)
        {
			var priv = basePlayer.GetBuildingPrivilege();
			if (priv != null) { return priv; }
			return GetTugboat(basePlayer);
        }

		public BaseEntity GetProtectedEntity(BaseCombatEntity baseCombatEntity)
		{
			var priv = baseCombatEntity.GetBuildingPrivilege();
			if (priv != null) { return priv; }
			return GetTugboatParent(baseCombatEntity);
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
	}
}

namespace Oxide.Plugins
{
    partial class RaidProtection
    {
        public static class CustomStatusFrameworkHelper
        {
            public static void CreateProtectionStatus()
            {
                PLUGIN.CustomStatusFramework?.Call("DeleteStatus", "protectionstatus");
                PLUGIN.CustomStatusFramework?.Call("DeleteStatus", "protectionstatus.offline");
                PLUGIN.CustomStatusFramework?.Call("DeleteStatus", "protectionstatus.nonowner");
                if (PLUGIN.config.CustomStatusFramework.ShowStatusForOwners)
                {
                    PLUGIN.timer.In(1f, () =>
                    {
                        // Online Protection
                        Func<BasePlayer, bool> conditionOnline = (basePlayer) =>
                        {
                            if (basePlayer == null) { return false; }
                            var priv = PLUGIN.GetProtectedEntity(basePlayer);
                            if (priv == null) { return false; }
                            var tc = ProtectedCupboardManager.InitProtectedCupboard(priv);
                            if (tc == null) { return false; }
                            return tc.IsProtected && tc.IsOwner(basePlayer);
                        };
                        Func<BasePlayer, string> value = (basePlayer) =>
                        {
                            if (basePlayer == null) { return ""; }
                            var priv = PLUGIN.GetProtectedEntity(basePlayer);
                            if (priv == null) { return ""; }
                            var tc = ProtectedCupboardManager.InitProtectedCupboard(priv);
                            if (tc == null) { return ""; }
                            return tc.HasFreeProtection ? String.Empty : $"{(int)Math.Ceiling(tc.HoursOfProtection)}h";
                        };
                        PLUGIN.CustomStatusFramework?.Call("CreateDynamicStatus",
                            "protectionstatus",
                            RustColor.Green,
                            PLUGIN.Lang("customstatusframework raid protected"),
                            RustColor.LightWhite,
                            RustColor.LightLime,
                            "status.protected",
                            RustColor.Lime,
                            conditionOnline, value);
                        // Offline Protection
                        Func<BasePlayer, bool> conditionOffline = (basePlayer) =>
                        {
                            if (basePlayer == null) { return false; }
                            var priv = PLUGIN.GetProtectedEntity(basePlayer);
                            if (priv == null) { return false; }
                            var tc = ProtectedCupboardManager.InitProtectedCupboard(priv);
                            if (tc == null) { return false; }
                            return tc.IsPending && tc.IsOwner(basePlayer);
                        };
                        PLUGIN.CustomStatusFramework?.Call("CreateStatus",
                            "protectionstatus.offline",
                            RustColor.Red,
                            PLUGIN.Lang("customstatusframework raid protected"),
                            RustColor.LightOrange,
                            "Pending",
                            RustColor.LightOrange,
                            "status.protected",
                            RustColor.LightOrange,
                            conditionOffline);
                    });
                }
                if (PLUGIN.config.CustomStatusFramework.ShowStatusForNonOwners)
                {
                    PLUGIN.timer.In(1f, () =>
                    {
                        // Non-Owner Status
                        Func<BasePlayer, bool> conditionNonOwner = (basePlayer) =>
                        {
                            if (basePlayer == null) { return false; }
                            var priv = PLUGIN.GetProtectedEntity(basePlayer);
                            if (priv == null) { return false; }
                            var tc = ProtectedCupboardManager.InitProtectedCupboard(priv);
                            if (tc == null) { return false; }
                            return tc.IsProtected && !tc.IsOwner(basePlayer);
                        };
                        PLUGIN.CustomStatusFramework?.Call("CreateStatus",
                            "protectionstatus.nonowner",
                            RustColor.Maroon,
                            PLUGIN.Lang("customstatusframework protected"),
                            RustColor.LightMaroon,
                            $"",
                            RustColor.LightMaroon,
                            "status.protected",
                            RustColor.LightMaroon,
                            conditionNonOwner);
                        // Non-Owner Value
                        Func<BasePlayer, string> value = (basePlayer) =>
                        {
                            if (basePlayer == null) { return ""; }
                            var priv = PLUGIN.GetProtectedEntity(basePlayer);
                            if (priv == null) { return ""; }
                            var tc = ProtectedCupboardManager.InitProtectedCupboard(priv);
                            if (tc == null) { return ""; }
                            return $"{(int)Math.Ceiling(tc.CurrentProtectionPercent)}%";
                        };
                        PLUGIN.CustomStatusFramework?.Call("CreateDynamicStatus",
                            "protectionstatus.nonowner",
                            RustColor.Maroon,
                            PLUGIN.Lang("customstatusframework protected"),
                            RustColor.LightMaroon,
                            RustColor.LightMaroon,
                            "status.protected",
                            RustColor.LightMaroon,
                            conditionNonOwner, value);
                    });
                }
            }

            public static void DestroyProtectionStatusPopup(BasePlayer basePlayer)
            {
                if (basePlayer == null) { return; }
                PLUGIN.CustomStatusFramework?.Call("ClearStatus", basePlayer, "protectionstatushammerpopup");
            }

            public static void ShowOwnerPopupProtectionStatus(BasePlayer basePlayer, ProtectedEntity tc)
            {
                if (basePlayer == null || PLUGIN.config.CustomStatusFramework.ShowStatusForOwners) return;
                PLUGIN.CustomStatusFramework?.Call("UpdateStatus",
                     basePlayer,
                     "protectionstatushammerpopup",
                     RustColor.Green,
                     PLUGIN.Lang("customstatusframework raid protected", basePlayer),
                     RustColor.LightWhite,
                     $"{tc.CurrentProtectionPercent}%",
                     RustColor.LightLime,
                     "status.protected",
                     RustColor.Lime);
            }


            public static void ShowNonOwnerPopupProtectionStatus(BasePlayer basePlayer, ProtectedEntity tc)
            {
                if (basePlayer == null || PLUGIN.config.CustomStatusFramework.ShowStatusForNonOwners) return;
                PLUGIN.CustomStatusFramework?.Call("UpdateStatus",
                    basePlayer,
                    "protectionstatushammerpopup",
                    RustColor.Maroon,
                    PLUGIN.Lang("customstatusframework protected", basePlayer),
                    RustColor.LightMaroon,
                    $"{tc.CurrentProtectionPercent}%",
                    RustColor.LightMaroon,
                    "status.protected",
                    RustColor.LightMaroon);
            }

            public static void ShowAttackedProtectionStatus(BasePlayer basePlayer, ProtectedEntity tc)
            {
                if (basePlayer == null || PLUGIN.config.CustomStatusFramework.ShowStatusForNonOwners) return;
                if (PLUGIN.IsOffIndicatorCooldown(basePlayer))
                {
                    PLUGIN.CustomStatusFramework?.Call("UpdateStatus",
                        basePlayer,
                        "protectionstatuspopup",
                        RustColor.Maroon,
                        PLUGIN.Lang("customstatusframework protected", basePlayer),
                        RustColor.LightMaroon,
                        $"{tc.CurrentProtectionPercent}%",
                        RustColor.LightMaroon,
                        "status.protected",
                        RustColor.LightMaroon);
                }
            }

            public static void ShowProtectionBalanceDeducted(BasePlayer basePlayer, ProtectedEntity tc)
            {
                if (basePlayer == null) return;
                if (PLUGIN.config.Indicator.ShowBalanceDeducted)
                {
                    double amount = 0;
                    if (!PLUGIN.AccumulatedCost.TryGetValue(basePlayer.UserIDString, out amount)) { return; }
                    var statusid = "protectionstatusdeductedpopup";
                    var amountRounded = String.Format("{0:0.00}", amount);
                    PLUGIN.CustomStatusFramework?.Call("UpdateStatus",
                        basePlayer,
                        statusid,
                        RustColor.Maroon,
                        PLUGIN.Lang("customstatusframework balance", basePlayer),
                        RustColor.LightMaroon,
                        $"-{amountRounded}",
                        RustColor.LightMaroon,
                        "status.protected",
                        RustColor.LightMaroon);
                }
            }

            public static void ClearProtectionStatus(BasePlayer basePlayer)
            {
                if (basePlayer == null) return;
                PLUGIN.CustomStatusFramework?.Call("ClearStatus", basePlayer, "protectionstatuspopup");
                //PLUGIN.CustomStatusFramework?.Call("ClearStatus", basePlayer, "protectionstatushammerpopup");
                PLUGIN.CustomStatusFramework?.Call("ClearStatus", basePlayer, "protectionstatusdeductedpopup");
            }

            public static void ClearAllProtectionStatuses()
            {
                foreach (var basePlayer in BasePlayer.activePlayerList.Where(x => x != null))
                {
                    ClearProtectionStatus(basePlayer);
                    DestroyProtectionStatusPopup(basePlayer);
                }
            }

            public static void DeleteProtectionStatus()
            {
                ClearAllProtectionStatuses();
                PLUGIN.CustomStatusFramework?.Call("DeleteStatus", "protectionstatus");
                PLUGIN.CustomStatusFramework?.Call("DeleteStatus", "protectionstatus.offline");
            }
        }

        public static class NotifyHelper
        {
            public static void ShowProtectionStatus(BasePlayer basePlayer, ProtectedEntity tc)
            {
                if (basePlayer == null || !PLUGIN.IsOffIndicatorCooldown(basePlayer)) return;
                PLUGIN.NextTick(() =>
                {
                    if (basePlayer == null || tc == null) return;
                    if (tc.Status == ProtectionStatus.Protected)
                    {
                        PLUGIN.Notify?.Call("SendNotify", basePlayer, PLUGIN.config.Notify.ProtectedType, PLUGIN.Lang("notify protected", basePlayer, tc.CurrentProtectionPercent));
                    }
                    else
                    {
                        PLUGIN.Notify?.Call("SendNotify", basePlayer, PLUGIN.config.Notify.UnprotectedType, PLUGIN.Lang("notify unprotected", basePlayer));
                    }
                });
            }
        }

        public static class ZoneManagerHelper
        {
            public static bool AllowDestruction(BaseCombatEntity entityAttacked, ProtectedEntity tc)
            {
                if ((entityAttacked != null && tc != null) && (PLUGIN.ZoneManager?.IsLoaded ?? false))
                {
                    var zones = PLUGIN.ZoneManager.Call<string[]>("GetEntityZoneIDs", entityAttacked);
                    foreach(var zone in zones)
                    {
                        if (PLUGIN.ZoneManager.Call<bool>("HasFlag", zone, "undestr"))
                        {
                            return false;
                        }
                    }
                }
                return true;
            }
        }

        public static class ClansHelper
        {
            public static List<ulong> GetClanMembers(ulong userId)
            {
                try
                {
                    if (!PLUGIN.config.Integration.Clans || (!PLUGIN.Clans?.IsLoaded ?? true)) { return new List<ulong>(); }
                    var memberIds = PLUGIN.Clans?.Call<List<string>>("GetClanMembers", userId.ToString());
                    if (memberIds == null) { return new List<ulong>(); }
                    var playerList = new List<BasePlayer>();
                    return memberIds.Select(x => ulong.Parse(x)).ToList();
                }
                catch (Exception)
                {
                    return new List<ulong>();
                }
            }
        }

        public HashSet<ulong> AbandonedEntities = new HashSet<ulong>();

        public static class AbandonedBasesHelper
        {
            public static bool IsAbandoned(BaseEntity entity)
            {
                try
                {
                    if (!PLUGIN.config.Integration.AbandonedBases || entity == null || (!PLUGIN.AbandonedBases?.IsLoaded ?? true))
                    {
                        return false;
                    }
                    if (PLUGIN.AbandonedEntities.Contains(entity.net.ID.Value))
                    {
                        return true;
                    }
                    else
                    {
                        if (PLUGIN.AbandonedBases?.Call<bool>("IsAbandoned", entity) ?? false)
                        {
                            PLUGIN.AbandonedEntities.Add(entity.net.ID.Value);
                            return true;
                        }
                    }
                    return false;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class RaidProtection
    {
        private bool DebuggingBehaviors = false;

        private void OnStatusEnd(ulong userId, string statusId, int duration)
        {
            if (statusId == SimpleStatusHelper.ID_BALANCE)
            {
                SimpleStatusHelper.SimpleStatusAccumulatedCost.Remove(userId.ToString());
            }
            else if (statusId == "simplestatus.buildingpriv")
            {
                SimpleStatusHelper.ClearProtectionStatus(userId);
            }
        }

        public static class SimpleStatusHelper
        {
            public static Dictionary<string, double> SimpleStatusAccumulatedCost = new Dictionary<string, double>();

            public static readonly string ID_STATUS = "raidprotection.status";
            public static readonly string ID_VISITOR = "raidprotection.visitor";
            public static readonly string ID_BALANCE = "raidprotection.balance";

            public static bool IsLoaded()
            {
                try
                {
                    return (PLUGIN.SimpleStatus?.IsLoaded ?? false) && PLUGIN.config.Integration.SimpleStatus;
                } catch (Exception) { return false; }
            }

            public static void CreateStatuses()
            {
                PLUGIN.SimpleStatus.CallHook("CreateStatus", PLUGIN, ID_STATUS, RustColor.Green, "simplestatus owner", RustColor.LightWhite, null, RustColor.LightLime, "status.protected", RustColor.Lime);
                PLUGIN.SimpleStatus.CallHook("CreateStatus", PLUGIN, ID_VISITOR, RustColor.Maroon, "simplestatus nonowner", RustColor.LightMaroon, null, RustColor.LightMaroon, "status.unprotected", RustColor.LightMaroon);
                PLUGIN.SimpleStatus.CallHook("CreateStatus", PLUGIN, ID_BALANCE, RustColor.Red, "simplestatus balance", RustColor.LightOrange, null, RustColor.LightOrange, "rp.costs", RustColor.LightOrange);
            }

            public static void SetBalanceStatus(BasePlayer basePlayer, ProtectedEntity tc)
            {
                if (basePlayer == null || tc == null || tc.HasFreeProtection) return;
                var amount = SimpleStatusAccumulatedCost.GetValueOrDefault(basePlayer.UserIDString);
                if (amount <= 0) return;
                if (tc.Balance > 0)
                {
                    var amountRounded = String.Format("-{0:0.00}", amount);
                    PLUGIN.SimpleStatus.CallHook("SetStatus", basePlayer.UserId(), ID_BALANCE, 4);
                    PLUGIN.SimpleStatus.CallHook("SetStatusText", basePlayer.UserId(), ID_BALANCE, amountRounded);
                }
                else if (amount > 0)
                {
                    PLUGIN.SimpleStatus.CallHook("SetStatus", basePlayer.UserId(), ID_BALANCE, 0);
                    ClearProtectionStatus(basePlayer);
                }
            }

            private static void SetProtectionStatusTextPreference(BasePlayer basePlayer, ProtectedEntity tc, bool popup = false)
            {
                switch(PLUGIN.config.SimpleStatus.TextValue.ToLower())
                {
                    case "none":
                        PLUGIN.SimpleStatus.CallHook("SetStatusText", basePlayer.UserId(), ID_STATUS, string.Empty);
                        break;
                    case "percent":
                        PLUGIN.SimpleStatus.CallHook("SetStatusText", basePlayer.UserId(), ID_STATUS, $"{tc.CurrentProtectionPercent}%");
                        break;
                    case "duration":
                    default:
                        if (popup)
                        {
                            PLUGIN.SimpleStatus.CallHook("SetStatusText", basePlayer.UserId(), ID_STATUS, string.Empty);
                        }
                        if (tc.HasFreeProtection)
                        {
                            PLUGIN.SimpleStatus.CallHook("SetStatusText", basePlayer.UserId(), ID_STATUS, "∞");
                        }
                        else
                        {
                            PLUGIN.SimpleStatus.CallHook("SetStatusText", basePlayer.UserId(), ID_STATUS);
                        }
                        break;
                }
            }

            public static bool SetProtectionStatus(BasePlayer basePlayer, ProtectedEntity tc, bool hammer = false, bool popup = false)
            {
                bool showForOwners = popup || hammer || PLUGIN.config.SimpleStatus.AlwaysShowForOwners;
                bool showForNonOwners = popup || hammer || PLUGIN.config.SimpleStatus.AlwaysShowForNonOwners;
                int poptime = popup ? 4 : int.MaxValue;
                if (showForOwners && tc != null && tc.IsAuthed(basePlayer) && tc.IsProtected)
                {
                    if (tc.HasFreeProtection)
                    {
                        PLUGIN.SimpleStatus.CallHook("SetStatus", basePlayer.UserId(), ID_STATUS, poptime);
                        SetProtectionStatusTextPreference(basePlayer, tc, popup);
                        return true;
                    }
                    else if (!popup)
                    {
                        var duration = (int)Math.Floor(tc.HoursOfProtection * 3600);
                        PLUGIN.SimpleStatus.CallHook("SetStatus", basePlayer.UserId(), ID_STATUS, popup ? poptime : duration);
                        SetProtectionStatusTextPreference(basePlayer, tc, popup);
                        return duration > 0;
                    }
                    else // Owner attacked the tc
                    {
                        PLUGIN.SimpleStatus.CallHook("SetStatus", basePlayer.UserId(), ID_VISITOR, poptime);
                        PLUGIN.SimpleStatus.CallHook("SetStatusText", basePlayer.UserId(), ID_VISITOR, $"{tc.CurrentProtectionPercent}%");
                        return true;
                    }
                }
                else if (showForOwners && tc != null && tc.IsAuthed(basePlayer) && tc.IsPending && tc.Reason == StatusReason.OfflineOnly)
                {
                    PLUGIN.SimpleStatus.CallHook("SetStatus", basePlayer.UserId(), ID_VISITOR, 0);
                    PLUGIN.SimpleStatus.CallHook("SetStatus", basePlayer.UserId(), ID_STATUS, poptime);
                    PLUGIN.SimpleStatus.CallHook("SetStatusText", basePlayer.UserId(), ID_STATUS, PLUGIN.Lang("simplestatus offline only", basePlayer));
                    return true;
                }
                else if (showForOwners && tc != null && tc.IsAuthed(basePlayer) && tc.IsPending && tc.Reason == StatusReason.RecentlyTookDamage)
                {
                    PLUGIN.SimpleStatus.CallHook("SetStatus", basePlayer.UserId(), ID_VISITOR, 0);
                    PLUGIN.SimpleStatus.CallHook("SetStatus", basePlayer.UserId(), ID_STATUS, poptime);
                    PLUGIN.SimpleStatus.CallHook("SetStatusText", basePlayer.UserId(), ID_STATUS, PLUGIN.Lang("simplestatus damage delayed", basePlayer));
                    return true;
                }
                else if (showForNonOwners && tc != null && !tc.IsAuthed(basePlayer) && tc.IsProtected)
                {
                    PLUGIN.SimpleStatus.CallHook("SetStatus", basePlayer.UserId(), ID_VISITOR, poptime);
                    PLUGIN.SimpleStatus.CallHook("SetStatusText", basePlayer.UserId(), ID_VISITOR, $"{tc.CurrentProtectionPercent}%");
                    return true;
                }
                return ClearProtectionStatus(basePlayer);
            }

            public static bool ClearProtectionStatus(ulong userId)
            {
                PLUGIN.SimpleStatus?.CallHook("SetStatus", userId, ID_STATUS, 0);
                PLUGIN.SimpleStatus?.CallHook("SetStatus", userId, ID_VISITOR, 0);
                return false;
            }

            public static bool ClearProtectionStatus(BasePlayer basePlayer)
            {
                if (basePlayer == null) { return false; }
                return ClearProtectionStatus(basePlayer.UserId());
            }
        }
    }

    partial class RaidProtection : CovalencePlugin
    {
        public void AddBehavior(BasePlayer basePlayer)
        {
            if (basePlayer == null || !basePlayer.IsConnected) { return; }
            if (DebuggingBehaviors)
            {
                PLUGIN.Puts($"Adding behavior for {basePlayer.UserId()}");
            }
            if (PLUGIN.SimpleStatus?.IsLoaded ?? false)
            {
                SimpleStatusHelper.ClearProtectionStatus(basePlayer);
            }
            if (!_behaviours.ContainsKey(basePlayer.UserId()))
            {
                var obj = basePlayer.gameObject.AddComponent<ProtectionStatusBehavior>();
                _behaviours.Add(basePlayer.UserId(), obj);
                obj.ForceUpdate = true;
            }
        }
        private void RemoveAndDestroyBehavior(ulong userId)
        {
            if (DebuggingBehaviors)
            {
                PLUGIN.Puts($"Removing behavior for {userId}");
            }
            if (PLUGIN.SimpleStatus?.IsLoaded ?? false)
            {
                SimpleStatusHelper.ClearProtectionStatus(userId);
            }
            var obj = _behaviours.GetValueOrDefault(userId);
            if (obj == null) { return; }
            UnityEngine.Object.Destroy(obj);
            _behaviours.Remove(userId);
        }
        public void RemoveAndDestroyAllBehaviors()
        {
            var bcount = BehaviorCount;
            foreach (var b in _behaviours.Values)
            {
                if (b == null) { continue; }
                RemoveAndDestroyBehavior(b.UserId);
            }
            _behaviours.Clear();
            if (DebuggingBehaviors)
            {
                PLUGIN.Puts($"There were {bcount} behaviors and now there are {BehaviorCount}");
            }
        }
        public int BehaviorCount = 0;
        private Dictionary<ulong, ProtectionStatusBehavior> _behaviours = new Dictionary<ulong, ProtectionStatusBehavior>();
        public class ProtectionStatusBehavior : MonoBehaviour
        {
            private BasePlayer basePlayer;

            public ulong UserId => basePlayer?.userID ?? 0;

            public bool ForceUpdate = true;

            public bool CurrentlyShowingStatus = false;

            public bool InBuildingPriv => BuildingPrivlidge != null;

            public BuildingPrivlidge BuildingPrivlidge { get; private set; }
            private ulong lastPrivId = 0;

            #region Private

            private void Awake()
            {
                PLUGIN.BehaviorCount++;
                basePlayer = GetComponent<BasePlayer>();
                if (PLUGIN.DebuggingBehaviors)
                {
                    PLUGIN.Puts($"Behavior Count: {PLUGIN.BehaviorCount}");
                }
                StartWorking();
            }
            private void OnDestroy()
            {
                PLUGIN.BehaviorCount--;
                StopWorking();
                if (PLUGIN.DebuggingBehaviors) 
                {
                    PLUGIN.Puts($"Behavior Count: {PLUGIN.BehaviorCount}");
                }
            }

            private void StartWorking()
            {
                InvokeRepeating(nameof(RepeatCheckForProtection), 2, 2);
            }

            private void StopWorking()
            {
                CancelInvoke(nameof(RepeatCheckForProtection));
            }

            private void RepeatCheckForProtection()
            {
                try
                {
                    var wasInBuildingPrivBefore = InBuildingPriv;
                    var privAtCurrentLocation = basePlayer.GetBuildingPrivilege();
                    BuildingPrivlidge = privAtCurrentLocation;
                    var privStateChanged = wasInBuildingPrivBefore != InBuildingPriv || lastPrivId != (privAtCurrentLocation?.net.ID.Value ?? 0);
                    lastPrivId = (privAtCurrentLocation?.net.ID.Value ?? 0);
                    if (privAtCurrentLocation == null && CurrentlyShowingStatus)
                    {
                        CurrentlyShowingStatus = SimpleStatusHelper.ClearProtectionStatus(basePlayer);
                        return;
                    }
                    else if (privAtCurrentLocation == null && !CurrentlyShowingStatus)
                    {
                        return;
                    }
                    var tc = ProtectedCupboardManager.InitProtectedCupboard(privAtCurrentLocation);
                    if (tc == null && CurrentlyShowingStatus)
                    {
                        CurrentlyShowingStatus = SimpleStatusHelper.ClearProtectionStatus(basePlayer);
                        return;
                    }
                    if (tc != null && privStateChanged || tc.SimpleStatusNeedsUpdate)
                    {
                        tc.SimpleStatusNeedsUpdate = false;
                        CurrentlyShowingStatus = SimpleStatusHelper.SetProtectionStatus(basePlayer, tc);
                        return;
                    }
                } catch (NullReferenceException)
                {
                    CurrentlyShowingStatus = SimpleStatusHelper.ClearProtectionStatus(basePlayer);
                    return;
                }
            }

            #endregion
        }
    }
}

namespace Oxide.Plugins
{
    partial class RaidProtection
    {
        public enum BalanceLedgerReason
        {
            Initial, CollectionStarted, CollectionStopped, DamageCost, Withdraw, Added, Other, Command, Restored, ServerSave, DoNotRecord
        }

        public static class BalanceLedger
        {
            private static NullSafeDictionary<ulong, List<string>> LedgerLogs = new NullSafeDictionary<ulong, List<string>>();

            public static void Update(ProtectedEntity tc, BalanceLedgerReason reason = BalanceLedgerReason.Other)
            {
                try
                {
                    if (tc == null || !PLUGIN.config.EnableLedger || reason == BalanceLedgerReason.DoNotRecord) { return; }
                    LedgerLogs[tc.ID].Add($"{DateTime.Now},{Guid.NewGuid()},{tc.Balance},{reason}");
                } catch (Exception) { return; }
            }

            public static bool Restore(ProtectedEntity tc, string guid)
            {
                try
                {
                    float? snapshot = null;
                    foreach(var entry in LedgerLogs[tc.ID])
                    {
                        var split = entry.Split(',');
                        if (split[1] == guid)
                        {
                            snapshot = float.Parse(split[2]);
                            break;
                        }
                    }
                    if (snapshot.HasValue)
                    {
                        PLUGIN.ClearProtectionBalance(tc, false, null, BalanceLedgerReason.DoNotRecord);
                        PLUGIN.UpdateProtectionBalance(tc, snapshot.Value, BalanceLedgerReason.Restored);
                        SaveAll();
                        return true;
                    }
                    return false;
                } catch(Exception)
                {
                    return false;
                }
            }

            public static bool Restore(ProtectedEntity tc, DateTime targetDateTime)
            {
                try
                {
                    float snapshot = 0;
                    foreach (var entry in LedgerLogs[tc.ID])
                    {
                        var split = entry.Split(',');
                        var dateOfEntry = DateTime.Parse(split[0]);
                        if (dateOfEntry > targetDateTime)
                        {
                            break;
                        }
                        snapshot = float.Parse(split[2]);
                    }
                    PLUGIN.ClearProtectionBalance(tc, false, null, BalanceLedgerReason.DoNotRecord);
                    PLUGIN.UpdateProtectionBalance(tc, snapshot, BalanceLedgerReason.Restored);
                    SaveAll();
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            public static void ClearAll()
            {
                foreach(var key in LedgerLogs.Keys)
                {
                    LedgerLogs[key].Clear();
                }
            }

            public static void LoadAll()
            {
                foreach (var kvp in ProtectedCupboardManager.ProtectedCupboards)
                {
                    try
                    {
                        LedgerLogs[kvp.Key] = PLUGIN.LoadDataFile<List<string>>($"Ledgers/{kvp.Key}");
                    }
                    catch (Exception) { continue; }
                }

            }

            public static void SaveAll()
            {
                try
                {
                    foreach (var kvp in LedgerLogs)
                    {
                        var tc = ProtectedCupboardManager.GetByID(kvp.Key);
                        if (tc != null && !tc.IsNpcBase)
                        {
                            PLUGIN.SaveDataFile($"Ledgers/{kvp.Key}", kvp.Value);
                        }
                    }
                }
                catch(Exception)
                {
                    PLUGIN.PrintError("Failed to save ledger balances");
                }
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class RaidProtection
    {
        public static class RustColor
        {
            public static string Blue = "0.08627 0.25490 0.38431 1";
            public static string LightBlue = "0.25490 0.61176 0.86275 1";
            //public static string Red = "0.68627 0.21569 0.14118 1";
            public static string Red = "0.77255 0.23922 0.15686 1";
            public static string Maroon = "0.46667 0.22745 0.18431 1";
            public static string LightMaroon = "1.00000 0.32549 0.21961 1";
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
        }
    }
}

namespace Oxide.Plugins
{
    partial class RaidProtection
    {
        private static readonly string TRANSPARENT = "0 0 0 0";

        private static readonly string BOTTOM_LEFT = "0 0";

        private static readonly string BOTTOM_CENTER = "0.5 0";

        private static readonly string BOTTOM_RIGHT = "1 0";

        private static readonly string TOP_LEFT = "0 1";

        private static readonly string TOP_CENTER = "0.5 1";

        private static readonly string TOP_RIGHT = "1 1";

        private static readonly string MIDDLE_LEFT = "0 0.5";

        private static readonly string MIDDLE_CENTER = "0.5 0.5";

        private static readonly string MIDDLE_RIGHT = "1 0.5";

        public string Offset(float x, float y)
        {
            return $"{x} {y}";
        }
        public string Offset(int x, int y)
        {
            return $"{x} {y}";
        }

        public string Anchor(float x, float y)
        {
            return $"{x} {y}";
        }

        public string GetImage(string imgId)
        {
            return ImageLibrary?.Call<string>("GetImage", imgId);
        }
    }
}

namespace Oxide.Plugins
{
    partial class RaidProtection
    {
        [Command("tc.help")]
        private void CmdHelpShow(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer != null)
            {
                ShowLevels(basePlayer);
            }
        }

        [Command("tc.help.close")]
        private void CmdHelpClose(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer != null)
            {
                CloseHelp(basePlayer);
            }
        }

        private readonly static string HELP_ID = "rp.help";

        private void ShowHelp(BasePlayer basePlayer)
        {
            if (basePlayer == null) { return; }
            var styles = new InfoPanelStyles();
            var x = 150;
            var y = 50;
            var w = 700;
            var h = 550;
            var fontSize = 10;
            var container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Name = HELP_ID,
                Parent = "Overlay",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = styles.BackgroundColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = TOP_CENTER,
                        AnchorMax = TOP_CENTER,
                        OffsetMin = Offset(-w/2+x, -y-h),
                        OffsetMax = Offset(w/2+x, -y)
                    }
                }
            });
            var padded = ID(HELP_ID, "padded");
            container.Add(new CuiElement
            {
                Name = padded,
                Parent = HELP_ID,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = TRANSPARENT
                    },
                    new CuiRectTransformComponent
                    {
                        OffsetMin = Offset(styles.ContentPad, styles.ContentPad),
                        OffsetMax = Offset(-styles.ContentPad, -styles.ContentPad)
                    }
                }
            });
            /* Title */
            container.Add(new CuiElement
            {
                Parent = padded,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = Lang("command help title", basePlayer),
                        Align = UnityEngine.TextAnchor.UpperCenter,
                        Color = styles.TextColor,
                        FontSize = 16
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = TOP_LEFT,
                        AnchorMax = TOP_RIGHT,
                        OffsetMin = Offset(100, -styles.HeaderHeight),
                        OffsetMax = Offset(-100, 0)
                    }
                }
            });
            /* Close Button */
            var closebtn = ID(padded, "close");
            container.Add(new CuiElement
            {
                Parent = padded,
                Name = closebtn,
                Components =
                {
                    new CuiImageComponent
                    {
                        Png = ImageLibrary?.Call<string>("GetImage", "rp.cross"),
                        Color = styles.TextColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = TOP_RIGHT,
                        AnchorMax = TOP_RIGHT,
                        OffsetMin = Offset(-styles.HeaderHeight+styles.HeaderImgPad*2, -styles.HeaderHeight+styles.HeaderImgPad*2),
                        OffsetMax = Offset(0, 0)
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = closebtn,
                Components =
                {
                    new CuiButtonComponent
                    {
                        Color = TRANSPARENT,
                        Command = "tc.help.close"
                    }
                }
            });
            var cmdh = 20;
            var gap = 200;
            var top = styles.HeaderHeight + styles.ContentPad;
            foreach(var command in Commands)
            {
                /* Command */
                container.Add(new CuiElement
                {
                    Parent = padded,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"/{config.Commands.Admin} {command.Command} <color={ColorToHex(styles.SubtextColor)}>{command.ArgString}</color>",
                            Align = UnityEngine.TextAnchor.MiddleLeft,
                            Color = styles.TextColor,
                            FontSize = fontSize
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = TOP_LEFT,
                            AnchorMax = TOP_LEFT,
                            OffsetMin = Offset(0, -top-cmdh),
                            OffsetMax = Offset(gap, -top)
                        }
                    }
                });
                /* Description */
                container.Add(new CuiElement
                {
                    Parent = padded,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"{command.Description}",
                            Align = UnityEngine.TextAnchor.MiddleLeft,
                            Color = styles.TextColor,
                            FontSize = fontSize-1
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = TOP_LEFT,
                            AnchorMax = TOP_RIGHT,
                            OffsetMin = Offset(gap, -top-cmdh),
                            OffsetMax = Offset(0, -top)
                        }
                    }
                });
                top += cmdh;
            }
            var hints = new string[]
            {
                "Use /tc id to get the <tc id> parameter value.",
                "A '?' represents an optional parameter.",
                "You can interact with this menu when the chat or inventory is open."
            };
            var bottom = 0;
            foreach(var hint in hints)
            {
                /* Hint */
                container.Add(new CuiElement
                {
                    Parent = padded,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"HINT - {hint}",
                            Align = UnityEngine.TextAnchor.MiddleLeft,
                            Color = styles.SubtextColor,
                            FontSize = fontSize
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = BOTTOM_LEFT,
                            AnchorMax = BOTTOM_RIGHT,
                            OffsetMin = Offset(0, bottom),
                            OffsetMax = Offset(0, bottom + cmdh)
                        }
                    }
                });
                bottom += cmdh;
            }
            CloseHelp(basePlayer);
            CuiHelper.AddUi(basePlayer, container);
        }
        private void CloseHelp(BasePlayer basePlayer)
        {
            CuiHelper.DestroyUi(basePlayer, HELP_ID);
        }
    }
}

namespace Oxide.Plugins
{
    partial class RaidProtection
    {
        private static readonly float INDICATOR_LIFETIME_SECONDS = 2f;

        [Command("rp.indicator")]
        private void UIIndicatorShow(IPlayer player, string command, string[] args)
        {
            var result = Security.ValidateTokenArgs(args);
            if (!result.Success) { return; } else { args = result.Args; }
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer != null)
            {
                var priv = basePlayer.GetBuildingPrivilege();
                if (priv != null)
                {
                    var tc = ProtectedCupboardManager.InitProtectedCupboard(priv);
                    if (tc != null)
                    {
                        ShowIndicator(basePlayer, tc);
                    }
                }
            }
        }

        [Command("rp.indicator.close")]
        private void UIIndicatorClose(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer != null)
            {
                var priv = basePlayer.GetBuildingPrivilege();
                if (priv != null)
                {
                    var tc = ProtectedCupboardManager.InitProtectedCupboard(priv);
                    if (tc != null)
                    {
                        CloseIndicator(basePlayer);
                    }
                }
            }
        }

        private Dictionary<string, DateTime> IndicatorCooldowns = new Dictionary<string, DateTime>();
        public Dictionary<string, double> AccumulatedCost = new Dictionary<string, double>();

        private readonly static string INDICATOR_ID = "rp.indicator";

        private void SetIndicatorCooldown(BasePlayer basePlayer)
        {
            var until = DateTime.Now.AddSeconds(INDICATOR_LIFETIME_SECONDS);
            IndicatorCooldowns[basePlayer.UserIDString] = until;
        }

        private double UpdateIndicatorCost(BasePlayer basePlayer, double amount)
        {
            if (!AccumulatedCost.ContainsKey(basePlayer.UserIDString))
            {
                AccumulatedCost[basePlayer.UserIDString] = 0;
            }
            AccumulatedCost[basePlayer.UserIDString] += amount;
            return AccumulatedCost[basePlayer.UserIDString];
        }

        private bool IsOffIndicatorCooldown(BasePlayer basePlayer)
        {
            if (!IndicatorCooldowns.ContainsKey(basePlayer.UserIDString))
            {
                return true;
            }
            var now = DateTime.Now.Ticks;
            var target = IndicatorCooldowns[basePlayer.UserIDString].Ticks;
            if (now >= target)
            {
                AccumulatedCost.Remove(basePlayer.UserIDString);
                return true;
            }
            return false;
        }

        private void ShowIndicatorCost(BasePlayer basePlayer, ProtectedEntity tc, double amount)
        {
            if (tc.IsProtected && config.Indicator.Enabled && config.Indicator.ShowBalanceDeducted && tc.HasCostPerDamageProtected && amount > 0)
            {
                var container = new CuiElementContainer();
                var offset = 4;
                var panelw = 70;
                var panelh = 20;
                var costpanel = ID(INDICATOR_ID, "cost");
                var bgcolor = RustColor.Red;
                container.Add(new CuiElement
                {
                    Name = costpanel,
                    Parent = INDICATOR_ID,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = bgcolor
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"{0.5} {0}",
                            AnchorMax = $"{0.5} {0}",
                            OffsetMin = $"{-panelw/2} {-offset-panelh}",
                            OffsetMax = $"{panelw/2} {-offset}"
                        }
                    }
                });
                var txtcolor = RustColor.LightOrange;
                var deductedRounded = String.Format("{0:0.00}", amount);
                container.Add(new CuiElement
                {
                    Parent = costpanel,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = Lang("indicator balance reduced amount", basePlayer, deductedRounded),
                            Color = txtcolor,
                            FontSize = 8,
                            Align = UnityEngine.TextAnchor.MiddleCenter
                        }
                    }
                });
                CuiHelper.DestroyUi(basePlayer, costpanel);
                CuiHelper.AddUi(basePlayer, container);
            }

        }

        private void ShowIndicator(BasePlayer basePlayer, ProtectedEntity tc, bool ignoreCooldown = false, double amountDeducted = 0)
        {
            if (IsNull(basePlayer)) { return; }
            if (IsNull(tc))
            {
                CloseIndicator(basePlayer);
                return;
            }
            // Show Indicator if off cooldown
            if (config.Indicator.Enabled && (IsOffIndicatorCooldown(basePlayer) || ignoreCooldown))
            {
                var fontSize = config.Indicator.FontSize;
                var container = new CuiElementContainer();
                container.Add(new CuiElement
                {
                    Name = INDICATOR_ID,
                    Parent = "Hud",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = TRANSPARENT
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = config.Indicator.AnchorMin,
                            AnchorMax = config.Indicator.AnchorMax,
                            OffsetMin = config.Indicator.OffsetMin,
                            OffsetMax = config.Indicator.OffsetMax
                        }
                    }
                });
                var img = $"{INDICATOR_ID}.img";
                var green = RustColor.LightGreen;
                var red = RustColor.Red;
                var active = tc.IsProtected;
                container.Add(new CuiElement
                {
                    Name = img,
                    Parent = INDICATOR_ID,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Png = active ? ImageLibrary?.Call<string>("GetImage", "status.protected") : ImageLibrary?.Call<string>("GetImage", "status.unprotected"),
                            Color = active ? green : red
                        }
                    }
                });
                if (active)
                {
                    container.Add(new CuiElement
                    {
                        Parent = img,
                        Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"{tc.CurrentProtectionPercent}%",
                            FontSize = fontSize,
                            Align = UnityEngine.TextAnchor.MiddleCenter,
                            Color = RustColor.Green
                        }
                    }
                    });
                }
                CloseIndicator(basePlayer);
                CuiHelper.AddUi(basePlayer, container);
                CloseIndicatorIfOffCooldown(basePlayer);
            }
            else if (!config.Indicator.Enabled && config.Integration.CustomStatusFramework && (IsOffIndicatorCooldown(basePlayer) || ignoreCooldown))
            {
                CloseIndicatorIfOffCooldown(basePlayer);
            }
            // Update damage balance if enabled
            if (tc.IsProtected && (config.Indicator.Enabled || config.Integration.CustomStatusFramework) && config.Indicator.ShowBalanceDeducted && amountDeducted > 0)
            { 
                var amount = UpdateIndicatorCost(basePlayer, amountDeducted);
                ShowIndicatorCost(basePlayer, tc, amount);
            }
        }

        private void SetIndicatorCooldownOrClose(BasePlayer basePlayer, ProtectedEntity tc)
        {
            if (basePlayer == null || tc == null) { return; }
            if (tc.IsProtected)
            {
                SetIndicatorCooldown(basePlayer);
            }
            else
            {
                timer.In(INDICATOR_LIFETIME_SECONDS * 2, () =>
                {
                    CloseIndicator(basePlayer);
                });
            }
        }

        private void CloseIndicatorIfOffCooldown(BasePlayer basePlayer, int depth = 0)
        {
            timer.In(INDICATOR_LIFETIME_SECONDS+0.1f, () =>
            {
                if (basePlayer == null || !IndicatorCooldowns.ContainsKey(basePlayer.UserIDString)) { return; }
                if (depth >= 10)
                {
                    CloseIndicator(basePlayer);
                    IndicatorCooldowns.Remove(basePlayer.UserIDString);
                }
                if (IsOffIndicatorCooldown(basePlayer))
                {
                    CloseIndicator(basePlayer);
                    IndicatorCooldowns.Remove(basePlayer.UserIDString);
                }
                else
                {
                    var newDepth = depth += 1;
                    CloseIndicatorIfOffCooldown(basePlayer, newDepth);
                }
            });
        }

        private void CloseIndicator(BasePlayer basePlayer)
        {
            CustomStatusFrameworkHelper.ClearProtectionStatus(basePlayer);
            CuiHelper.DestroyUi(basePlayer, INDICATOR_ID);
        }
    }
}

namespace Oxide.Plugins
{
    partial class RaidProtection
    {
        private readonly string INFO_PANEL_ID = "rp.infopanel";
        private readonly string INFO_PANEL_CONTENT_ID = "rp.infopanel.content";
        private readonly string INFO_PANEL_SHADOW_ID = "rp.infopanel.shadow";

        private void TcInfoShowPlayer(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            if (IsNull(basePlayer)) return;
            var priv = GetProtectedEntity(basePlayer);
            if (IsNull(priv)) return;
            var tc = ProtectedCupboardManager.InitProtectedCupboard(priv);
            if (IsNull(tc)) return;
            if (tc.IsAuthed(basePlayer) || IsAdmin(basePlayer.IPlayer))
            {
                ShowInfoPanel(basePlayer, tc);
            }
            else
            {
                Message(basePlayer, "info need authorization");
            }
        }

        [Command("tc.info.open")]
        private void TcInfoOpen(IPlayer player, string command, string[] args)
        {
            var result = Security.ValidateTokenArgs(args);
            if (!result.Success) { return; } else { args = result.Args; }
            var basePlayer = player.Object as BasePlayer;
            if (IsNull(basePlayer)) return;
            var privid = ulong.Parse(args[0]);
            var tc = ProtectedCupboardManager.GetByID(privid);
            if (IsNull(tc)) return;
            var page = 0;
            int.TryParse(args[1], out page);
            ShowInfoPanel(basePlayer, tc, page);
        }

        [Command("tc.info.show")]
        private void TcInfoShow(IPlayer player, string command, string[] args)
        {
            var result = Security.ValidateTokenArgs(args);
            if (!result.Success) { return; } else { args = result.Args; }
            var basePlayer = player.Object as BasePlayer;
            if (IsNull(basePlayer)) return;
            var priv = basePlayer.GetBuildingPrivilege();
            if (IsNull(priv)) return;
            var tc = ProtectedCupboardManager.InitProtectedCupboard(priv);
            if (IsNull(tc)) return;
            ShowInfoPanel(basePlayer, tc, 0);
        }

        [Command("tc.info.close")]
        private void TcInfoClose(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            if (IsNull(basePlayer)) return;
            CloseInfoPanel(basePlayer);
        }

        private class InfoPanelStyles
        {
            public string ShadowColor = "0 0 0 0.9";
            public string BackgroundColor = RustColor.DarkGray;
            public string HeaderColor = RustColor.DarkBrown ;
            public string BoxColor = RustColor.DarkBrown;
            public string TextColor = "0.54509 0.51372 0.4705 1";
            public string SubtextColor = "0.54509 0.51372 0.5705 1";
            public float PanelSize = 0.5f;
            public int HeaderHeight = 30;
            public int HeaderImgPad = 8;
            public int HeaderImgGap = 2;
            public int ContentPad = 8;
            public string HeaderImgColor = "1 1 1 1";
            public float OVTitleH = 0.2f;
            public float OVTitleW = 0.6f;
            public float OVPanelP = 0.1f;
            public float OVPanelG = 0.05f;
            public int OVPanelContentP = 20;
            public int OVPanelImgS = 80;
            public float OVPanelFadeIn = 0.5f;
            public string TabSelectedColor = "0.54509 0.51372 0.4705 1";
            public string TabUnselectedColor = "0.54509 0.51372 0.4705 0.5";
            public float UPanelTitleW = 0.3f;
            public float UPanelTitleH = 0.1f;
        }

        private void ShowInfoPanel(BasePlayer basePlayer, ProtectedEntity tc, int page = 0)
        {
            var container = new CuiElementContainer();

            #region Styles
            var styles = new InfoPanelStyles();
            #endregion

            /* Shadow */
            container.Add(new CuiElement
            {
                Parent = "Overlay",
                Name = INFO_PANEL_SHADOW_ID,
                Components =
                {
                    new CuiButtonComponent
                    {
                        Color = styles.ShadowColor,
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                        Command = "tc.info.close"
                    }
                }
            });

            /* Panel */
            container.Add(new CuiElement
            {
                Parent = "Overlay",
                Name = INFO_PANEL_ID,
                Components =
                {
                    new CuiNeedsCursorComponent(),
                    new CuiImageComponent
                    {
                        Color = styles.BackgroundColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{0.5-(styles.PanelSize/2)} {0.5-(styles.PanelSize/2)}",
                        AnchorMax = $"{0.5+(styles.PanelSize/2)} {0.5+(styles.PanelSize/2)}"
                    }
                }
            });

            /* Header */
            var headerId = $"{INFO_PANEL_ID}.header";
            container.Add(new CuiElement
            {
                Parent = INFO_PANEL_ID,
                Name = headerId,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = styles.HeaderColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = TOP_LEFT,
                        AnchorMax = TOP_RIGHT,
                        OffsetMin = $"{0} {-styles.HeaderHeight}",
                        OffsetMax = $"{0} {0}"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = headerId,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = Lang("title", basePlayer).TitleCase(),
                        Color = styles.TextColor,
                        Align = UnityEngine.TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{0.3} {0}",
                        AnchorMax = $"{0.7} {1}"
                    }
                }
            });

            /* Header Tabs */
            #region Header Tabs
            var tabs = new[]
            {
                new 
                {
                    Icon = "status.info",
                    Command = $"tc.info.open {Security.Token} {tc.ID} 0",
                    Selected = page == 0
                },
                new
                {
                    Icon = "rp.owners",
                    Command = $"tc.info.open {Security.Token} {tc.ID} 1",
                    Selected = page == 1
                },
                new
                {
                    Icon = "status.protected",
                    Command = $"tc.info.open {Security.Token} {tc.ID} 2",
                    Selected = page == 2
                },
                new
                {
                    Icon = "rp.costs",
                    Command = $"tc.info.open {Security.Token} {tc.ID} 3",
                    Selected = page == 3
                }
            };

            var s = 0;
            foreach(var tab in tabs)
            {
                var tid = ID(headerId, "btn", true);
                container.Add(new CuiElement
                {
                    Parent = headerId,
                    Name = tid,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Png = ImageLibrary?.Call<string>("GetImage", tab.Icon),
                            Color = tab.Selected ? styles.TabSelectedColor : styles.TabUnselectedColor
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = BOTTOM_LEFT,
                            AnchorMax = TOP_LEFT,
                            OffsetMin = $"{s+styles.HeaderImgPad} {styles.HeaderImgPad}",
                            OffsetMax = $"{s-styles.HeaderImgPad+styles.HeaderHeight} {-styles.HeaderImgPad}"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = tid,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Color = TRANSPARENT,
                            Command = tab.Selected ? string.Empty : tab.Command
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = BOTTOM_LEFT,
                            AnchorMax = TOP_RIGHT
                        }
                    }
                });
                s += styles.HeaderHeight + styles.HeaderImgGap;
            }
            #endregion

            #region Option Tabs
            var options = new[]
            {
                new
                {
                    Icon = "rp.cross",
                    Command = "tc.info.close"
                }
            };
            s = 0;
            foreach (var option in options)
            {
                var tid = ID(headerId, "btn", true);
                container.Add(new CuiElement
                {
                    Parent = headerId,
                    Name = tid,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Png = ImageLibrary?.Call<string>("GetImage", option.Icon),
                            Color = styles.TextColor
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = BOTTOM_RIGHT,
                            AnchorMax = TOP_RIGHT,
                            OffsetMin = $"{-s-styles.HeaderHeight+styles.HeaderImgPad} {styles.HeaderImgPad}",
                            OffsetMax = $"{-s-styles.HeaderImgPad} {-styles.HeaderImgPad}"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = tid,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Color = TRANSPARENT,
                            Command = option.Command
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = BOTTOM_LEFT,
                            AnchorMax = TOP_RIGHT
                        }
                    }
                });
                s += styles.HeaderHeight + styles.HeaderImgGap;
            }
            #endregion

            /* Panel Content */
            container.Add(new CuiElement
            {
                Parent = INFO_PANEL_ID,
                Name = INFO_PANEL_CONTENT_ID,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = TRANSPARENT
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = BOTTOM_LEFT,
                        AnchorMax = TOP_RIGHT,
                        OffsetMin = $"{styles.ContentPad} {styles.ContentPad}",
                        OffsetMax = $"{-styles.ContentPad} {-styles.HeaderHeight-styles.ContentPad}"
                    }
                }
            });
            if (page == 0)
            {
                container = CreateOverviewPage(container, styles, basePlayer, tc);
            }
            if (page == 1)
            {
                container = CreateOwnersPage(container, styles, basePlayer, tc);
            }
            if (page == 2)
            {
                container = CreateProtectionPage(container, styles, basePlayer, tc);
            }
            if (page == 3)
            {
                container = CreateCostsPage(container, styles, basePlayer, tc);
            }
            CloseInfoPanel(basePlayer);
            CuiHelper.AddUi(basePlayer, container);
        }

        private CuiElementContainer CreateOverviewPage(CuiElementContainer container, InfoPanelStyles styles, BasePlayer basePlayer, ProtectedEntity tc)
        {
            /* Status Text */
            var pid = ID(INFO_PANEL_CONTENT_ID, "title");
            var isProtected = tc.Status == ProtectionStatus.Protected;
            var ptextcolor = isProtected ? RustColor.LightGreen : RustColor.LightRed;
            var pbgcolor = isProtected ? RustColor.Green : RustColor.Red;
            container.Add(new CuiElement
            {
                Parent = INFO_PANEL_CONTENT_ID,
                Name = pid,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = pbgcolor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{0.5f-styles.OVTitleW/2} {1-styles.OVTitleH}",
                        AnchorMax = $"{0.5f+styles.OVTitleW/2} {1}"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = pid,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = ProtectionStatusToString(tc, basePlayer, tc.Status),
                        Color = ptextcolor,
                        Align = UnityEngine.TextAnchor.UpperCenter,
                        FontSize = 18
                    },
                    new CuiRectTransformComponent
                    {
                        OffsetMin = Offset(0, styles.ContentPad),
                        OffsetMax = Offset(0, -styles.ContentPad)
                    }
                }
            });
            var rcolor = isProtected ? RustColor.LightGreen : RustColor.LightOrange;
            var reason = isProtected ? Lang("ui protected", basePlayer, tc.CurrentProtectionPercent, FormatTimeShort(tc.HoursOfProtection)) : StatusReasonToString(tc, basePlayer, tc.Reason);
            container.Add(new CuiElement
            {
                Parent = pid,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = reason,
                        Color = rcolor,
                        Align = UnityEngine.TextAnchor.LowerCenter
                    },
                    new CuiRectTransformComponent
                    {
                        OffsetMin = Offset(0, styles.ContentPad),
                        OffsetMax = Offset(0, -styles.ContentPad)
                    }
                }
            });
            var panels = new[]
            {
                new
                {
                    Text = Lang("info owners count", basePlayer, tc.Owners.Count),
                    Icon = "rp.owners",
                    Command = $"tc.info.open {Security.Token} {tc.ID} 1",
                    Color = "0 0 0 1"
                },
                new
                {
                    Text = Lang("info protection", basePlayer, tc.CurrentProtectionPercent),
                    Icon = isProtected ? "status.protected" : "status.unprotected",
                    Command = $"tc.info.open {Security.Token} {tc.ID} 2",
                    Color = "0 1 0 1"
                },
                new
                {
                    Text = Lang("info balance", basePlayer, FormatCurrency(basePlayer.UserIDString, (float)Math.Round(tc.Balance, 1))),
                    Icon = "rp.costs",
                    Command = $"tc.info.open {Security.Token} {tc.ID} 3",
                    Color = "0 0 1 1"
                }
            };
            #region Panels
            var panelsId = ID(INFO_PANEL_CONTENT_ID, "panels");
            container.Add(new CuiElement
            {
                Parent = INFO_PANEL_CONTENT_ID,
                Name = panelsId,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = TRANSPARENT
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{styles.OVPanelP} {styles.OVPanelP}",
                        AnchorMax = $"{1f-styles.OVPanelP} {1f-styles.OVTitleH-styles.OVPanelP}",
                        OffsetMin = $"{0} {styles.ContentPad*2}",
                        OffsetMax = $"{0} {0}"
                    }
                }
            });
            var panelW = (1f - ((panels.Length-1)*styles.OVPanelG)) / panels.Length;
            var s = 0f;
            foreach(var panel in panels)
            {
                var ppid = ID(panelsId, "panel", true);
                container.Add(new CuiElement
                {
                    Parent = panelsId,
                    Name = ppid,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Command = panel.Command,
                            FadeIn = styles.OVPanelFadeIn,
                            Color = styles.BoxColor
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"{s} {0}",
                            AnchorMax = $"{s+panelW} {1}"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = ppid,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            FadeIn = styles.OVPanelFadeIn,
                            Color = styles.TextColor,
                            Png = ImageLibrary?.Call<string>("GetImage", panel.Icon),
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = TOP_CENTER,
                            AnchorMax = TOP_CENTER,
                            OffsetMin = $"{-styles.OVPanelImgS/2} {-styles.OVPanelContentP-styles.OVPanelImgS}",
                            OffsetMax = $"{styles.OVPanelImgS/2} {-styles.OVPanelContentP}"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = ppid,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            FadeIn = styles.OVPanelFadeIn,
                            Text = panel.Text,
                            Align = UnityEngine.TextAnchor.MiddleCenter,
                            Color = styles.TextColor
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = BOTTOM_LEFT,
                            AnchorMax = TOP_RIGHT,
                            OffsetMin = $"{styles.OVPanelContentP} {styles.OVPanelContentP}",
                            OffsetMax = $"{-styles.OVPanelContentP} {-styles.OVPanelContentP-styles.OVPanelImgS}"
                        }
                    }
                });
                s += panelW + styles.OVPanelG;
            }
            #endregion
            return container;
        }

        private CuiElementContainer CreateOwnersPage(CuiElementContainer container, InfoPanelStyles styles, BasePlayer basePlayer, ProtectedEntity tc)
        {
            /* Bottom Bar */
            var bbarid = ID(INFO_PANEL_CONTENT_ID, "bar");
            container.Add(new CuiElement
            {
                Parent = INFO_PANEL_CONTENT_ID,
                Name = bbarid,
                Components =
                    {
                        new CuiImageComponent
                        {
                            FadeIn = styles.OVPanelFadeIn,
                            Color = RustColor.Red
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"{0.1} {0}",
                            AnchorMax = $"{0.9} {styles.UPanelTitleH}"
                        }
                    }
            });
            container.Add(new CuiElement
            {
                Parent = bbarid,
                Components =
                {
                    new CuiTextComponent
                    {
                        FadeIn = styles.OVPanelFadeIn,
                        Text = Lang("info owners persistent", basePlayer),
                        FontSize = 12,
                        Color = RustColor.LightRed,
                        Align = UnityEngine.TextAnchor.MiddleCenter
                    }
                }
            });
            var panels = new[]
            {
                new
                {
                    Title = Lang("info owners", basePlayer),
                    Values = tc.Owners
                    .Where(x => x != null)
                    .OrderBy(x => tc.FounderUserId == x.UserId())
                    .OrderBy(x => x.IsOnline())
                    .Select(x => new {
                        Text = x.Name,
                        Founder = tc.FounderUserId == x.UserId()
                    }).ToList()
                },
                new
                {
                    Title = Lang("info online", basePlayer),
                    Values = tc.OnlineOwners
                    .Where(x => x != null)
                    .OrderBy(x => tc.FounderUserId == x.UserId())
                    .Select(x => new {
                        Text = x.Name,
                        Founder = tc.FounderUserId == x.UserId()
                    }).ToList()
                },
                new
                {
                    Title = Lang("info authorized", basePlayer),
                    Values = tc.AuthedPlayers
                    .Where(x => x != null && !IsIgnored(x.Id))
                    .OrderBy(x => tc.FounderUserId == x.UserId())
                    .OrderBy(x => x.IsOnline())
                    .Select(x => new {
                        Text = x.Name,
                        Founder = tc.FounderUserId == x.UserId()
                    }).ToList()
                }
            };
            var panelW = 1f / panels.Length;
            var s = 0f;
            var idx = 0;
            foreach(var panel in panels)
            {
                bool isLast = idx == panels.Length - 1;
                var p = isLast ? 2 * styles.ContentPad : styles.ContentPad;
                /* Owner Title */
                var pid = ID(INFO_PANEL_CONTENT_ID, "title", true);
                container.Add(new CuiElement
                {
                    Parent = INFO_PANEL_CONTENT_ID,
                    Name = pid,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            FadeIn = styles.OVPanelFadeIn,
                            Color = styles.BoxColor
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"{s} {1-styles.UPanelTitleH}",
                            AnchorMax = $"{s+panelW} {1}",
                            OffsetMin = $"{0} {0}",
                            OffsetMax = $"{-p} {0}"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = pid,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            FadeIn = styles.OVPanelFadeIn,
                            Text = panel.Title,
                            Color = styles.TextColor,
                            Align = UnityEngine.TextAnchor.MiddleCenter
                        }
                    }
                });
                /* Owner Box */
                pid = ID(pid, "owners", true);
                container.Add(new CuiElement
                {
                    Parent = INFO_PANEL_CONTENT_ID,
                    Name = pid,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            FadeIn = styles.OVPanelFadeIn,
                            Color = styles.BoxColor
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"{s} {styles.UPanelTitleH}",
                            AnchorMax = $"{s+panelW} {1-styles.UPanelTitleH}",
                            OffsetMin = $"{0} {styles.ContentPad}",
                            OffsetMax = $"{-p} {-styles.ContentPad}"
                        }
                    }
                });
                p = styles.ContentPad;
                s += panelW;
                idx++;
                /* Owner Entries */
                var t = 0f;
                var h = 0.1f;
                var values = panel.Values;
                var moreThan10 = values.Count > 10;
                var j = 0;
                foreach(var owner in values)
                {
                    var lastEntry = j == values.Count - 1;
                    var entryId = ID(pid, "entry", true);
                    var ownerText = (moreThan10 && j >= 9) ? Lang("info others", basePlayer, values.Count - 9) : $"{owner.Text}";
                    if (owner.Founder)
                    {
                        ownerText += $" {Lang("info founder", basePlayer)}";
                    }
                    container.Add(new CuiElement
                    {
                        Parent = pid,
                        Name = entryId,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                FadeIn = styles.OVPanelFadeIn,
                                Text =  ownerText,
                                Color = styles.SubtextColor,
                                Align = UnityEngine.TextAnchor.MiddleLeft
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"{0} {1-t-h}",
                                AnchorMax = $"{1} {1-t}",
                                OffsetMin = $"{styles.ContentPad} {0}",
                                OffsetMax = $"{-styles.ContentPad} {0}"
                            }
                        }
                    });
                    if (moreThan10 && j >= 9)
                    {
                        break;
                    }
                    t += h;
                    j++;
                }
            }
            return container;
        }

        private CuiElementContainer CreateProtectionPage(CuiElementContainer container, InfoPanelStyles styles, BasePlayer basePlayer, ProtectedEntity tc)
        {
            #region Left
            var leftId = ID(INFO_PANEL_CONTENT_ID, "left");
            container.Add(new CuiElement
            {
                Parent = INFO_PANEL_CONTENT_ID,
                Name = leftId,
                Components =
                {
                    new CuiImageComponent
                    {
                        FadeIn = styles.OVPanelFadeIn,
                        Color = TRANSPARENT
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{0} {0}",
                        AnchorMax = $"{0.5} {1}",
                        OffsetMin = $"{0} {0}",
                        OffsetMax = $"{-styles.ContentPad} {0}"
                    }
                }
            });
            /* Title */
            var titleLId = ID(leftId, "title");
            container.Add(new CuiElement
            {
                Parent = leftId,
                Name = titleLId,
                Components =
                {
                    new CuiImageComponent
                    {
                        FadeIn = styles.OVPanelFadeIn,
                        Color = styles.BoxColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{0} {1-styles.UPanelTitleH}",
                        AnchorMax = $"{1} {1}"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = titleLId,
                Components =
                {
                    new CuiTextComponent
                    {
                        FadeIn = styles.OVPanelFadeIn,
                        Text = Lang("info protection rank", basePlayer, tc.HighestProtectionLevel.Rank),
                        Align = UnityEngine.TextAnchor.MiddleCenter,
                        Color = styles.TextColor
                    }
                }
            });
            var boxes = new[]
            {
                new
                {
                    Title = "",
                    Text = "",
                    Note = "",
                    FontSize = 24,
                    UseSubbox = true,
                    Subboxes = new []
                    {
                        new
                        {
                            Title = Lang("info protection current", basePlayer),
                            Text = $"{tc.CurrentProtectionPercent}%"
                        },
                        new
                        {
                            Title = Lang("info protection online", basePlayer),
                            Text = $"{tc.OnlineProtectionPercent}%"
                        },
                        new
                        {
                            Title = Lang("info protection offline", basePlayer),
                            Text = $"{tc.OfflineProtectionPercent}%"
                        }
                    }
                },
                new
                {
                    Title = "",
                    Text = "",
                    Note = "",
                    FontSize = 18,
                    UseSubbox = true,
                    Subboxes = new []
                    {
                        new
                        {
                            Title = Lang("info max protection time", basePlayer),
                            Text = (!tc.HasProtectionTimeLimit || tc.TotalProtectionCostPerHour <= 0) ? Lang("info no limit", basePlayer) : Lang("info hours", basePlayer, tc.MaxProtectionTimeHours),
                        },
                        new
                        {
                            Title = Lang("info cost when protected", basePlayer),
                            Text = $"{FormatCurrency(basePlayer.UserIDString, tc.CostPerDamageProtected)}"
                        }
                    }
                },
                new
                {
                    Title = Lang("info protection time delay", basePlayer),
                    Text = Lang("info delay seconds", basePlayer, tc.HighestProtectionLevel.OfflineProtectionDelay),
                    Note = Lang("info starts when owners offline", basePlayer),
                    FontSize = 18,
                    UseSubbox = false,
                    Subboxes = new[] { new { Title = "", Text = "" } }
                }
            };
            var t = 1f-styles.UPanelTitleH;
            var boxH = 0.3f;
            foreach(var box in boxes)
            {
                var boxId = ID(leftId, "box", true);
                container.Add(new CuiElement
                {
                    Parent = leftId,
                    Name = boxId,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            FadeIn = styles.OVPanelFadeIn,
                            Color = styles.BoxColor
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"{0} {t-boxH}",
                            AnchorMax = $"{1} {t}",
                            OffsetMin = $"{styles.ContentPad} {0}",
                            OffsetMax = $"{-styles.ContentPad} {-styles.ContentPad}"
                        }
                    }
                });
                if (box.UseSubbox)
                {
                    var sbs = 0f;
                    var sbw = 1f / (box.Subboxes.Length);
                    foreach(var sb in box.Subboxes)
                    {
                        var sbid = ID(boxId, "sb", true);
                        container.Add(new CuiElement
                        {
                            Parent = boxId,
                            Name = sbid,
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    FadeIn = styles.OVPanelFadeIn,
                                    Color = TRANSPARENT
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = $"{sbs} {0}",
                                    AnchorMax = $"{sbs+sbw} {1}",
                                    OffsetMin = $"{styles.ContentPad} {styles.ContentPad}",
                                    OffsetMax = $"{-styles.ContentPad} {-styles.ContentPad}"
                                }
                            }
                        });
                        container.Add(new CuiElement
                        {
                            Parent = sbid,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    FadeIn = styles.OVPanelFadeIn,
                                    Text = sb.Title,
                                    Color = styles.TextColor,
                                    Align = UnityEngine.TextAnchor.UpperCenter
                                }
                            }
                        });
                        container.Add(new CuiElement
                        {
                            Parent = sbid,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    FadeIn = styles.OVPanelFadeIn,
                                    Text = sb.Text,
                                    FontSize = box.FontSize,
                                    Color = styles.TextColor,
                                    Align = UnityEngine.TextAnchor.MiddleCenter
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = $"{0} {0}",
                                    AnchorMax = $"{1} {0.8}",
                                }
                            }
                        });
                        sbs += sbw;
                    }
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Parent = boxId,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                FadeIn = styles.OVPanelFadeIn,
                                Text = box.Title,
                                Color = styles.TextColor,
                                Align = UnityEngine.TextAnchor.UpperCenter
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                                OffsetMin = $"{styles.ContentPad} {styles.ContentPad}",
                                OffsetMax = $"{-styles.ContentPad} {-styles.ContentPad}"
                            }
                        }
                    });
                    container.Add(new CuiElement
                    {
                        Parent = boxId,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                FadeIn = styles.OVPanelFadeIn,
                                Text = box.Text,
                                FontSize = box.FontSize,
                                Color = styles.TextColor,
                                Align = UnityEngine.TextAnchor.MiddleCenter
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"{0} {0}",
                                AnchorMax = $"{1} {0.8}",
                                OffsetMin = $"{0} {(box.Note != "" ? styles.ContentPad : 0)}",
                                OffsetMax = $"{0} {0}"
                            }
                        }
                    });
                    container.Add(new CuiElement
                    {
                        Parent = boxId,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                FadeIn = styles.OVPanelFadeIn,
                                Text = box.Note,
                                FontSize = 10,
                                Color = styles.SubtextColor,
                                Align = UnityEngine.TextAnchor.LowerCenter
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"{0} {0}",
                                AnchorMax = $"{1} {0.8}",
                                OffsetMin = $"{styles.ContentPad} {styles.ContentPad}",
                                OffsetMax = $"{-styles.ContentPad} {-styles.ContentPad}"
                            }
                        }
                    });
                }
                t -= boxH;
            }
            #endregion
            #region Right
            var rightId = ID(INFO_PANEL_CONTENT_ID, "right");
            container.Add(new CuiElement
            {
                Parent = INFO_PANEL_CONTENT_ID,
                Name = rightId,
                Components =
                {
                    new CuiImageComponent
                    {
                        FadeIn = styles.OVPanelFadeIn,
                        Color = TRANSPARENT
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{0.5} {0}",
                        AnchorMax = $"{1} {1}",
                        OffsetMin = $"{styles.ContentPad} {0}",
                        OffsetMax = $"{0} {0}"
                    }
                }
            });
            var titleRId = ID(rightId, "title");
            container.Add(new CuiElement
            {
                Parent = rightId,
                Name = titleRId,
                Components =
                {
                    new CuiImageComponent
                    {
                        FadeIn = styles.OVPanelFadeIn,
                        Color = styles.BoxColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{0} {1-styles.UPanelTitleH}",
                        AnchorMax = $"{1} {1}"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = titleRId,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = Lang("info protected entities", basePlayer),
                        FadeIn = styles.OVPanelFadeIn,
                        Align = UnityEngine.TextAnchor.MiddleCenter,
                        Color = styles.TextColor
                    }
                }
            });
            var protectedEntities = new[]
            {
                new
                {
                    Text = Lang("info protected animals", basePlayer),
                    IsProtected = config.Protection.ProtectedEntities.Animals
                },
                new
                {
                    Text = Lang("info protected buildings", basePlayer),
                    IsProtected = config.Protection.ProtectedEntities.Buildings
                },
                new
                {
                    Text = Lang("info protected deployables", basePlayer),
                    IsProtected = config.Protection.ProtectedEntities.Containers
                },
                new
                {
                    Text = Lang("info protected traps", basePlayer),
                    IsProtected = config.Protection.ProtectedEntities.Traps
                },
                new
                {
                    Text = Lang("info protected electrical", basePlayer),
                    IsProtected = config.Protection.ProtectedEntities.Electrical
                },
                new
                {
                    Text = Lang("info protected horses", basePlayer),
                    IsProtected = config.Protection.ProtectedEntities.Horses
                },
                new
                {
                    Text = Lang("info protected loot nodes", basePlayer),
                    IsProtected = config.Protection.ProtectedEntities.LootNodes
                },
                new
                {
                    Text = Lang("info protected npcs", basePlayer),
                    IsProtected = config.Protection.ProtectedEntities.NPCs
                },
                new
                {
                    Text = Lang("info protected authed players", basePlayer),
                    IsProtected = config.Protection.ProtectedEntities.AuthedPlayers
                },
                new
                {
                    Text = Lang("info protected unauthed players", basePlayer),
                    IsProtected = config.Protection.ProtectedEntities.UnauthedPlayers
                },
                new
                {
                    Text = Lang("info protected vehicles", basePlayer),
                    IsProtected = config.Protection.ProtectedEntities.Vehicles
                },
            };
            var protectedFrom = new[]
{
                new
                {
                    Text = Lang("info protected authed players", basePlayer),
                    IsProtected = config.Protection.ProtectedFrom.AuthorizedPlayers
                },
                new
                {
                    Text = Lang("info protected unauthed players", basePlayer),
                    IsProtected = config.Protection.ProtectedFrom.UnauthorizedPlayers
                },
                new
                {
                    Text = Lang("info protected attack heli", basePlayer),
                    IsProtected = config.Protection.ProtectedFrom.AttackHeli
                }
            };
            var columns = new[]
            {
                new
                {
                    Title = Lang("info protected", basePlayer),
                    Data = protectedEntities
                },
                new
                {
                    Title = Lang("info from", basePlayer),
                    Data = protectedFrom
                }
            };
            var left = 0f;
            var colw = 1f / columns.Length;
            var subtitleH = 20;
            foreach(var col in columns)
            {
                var ttlId = ID(rightId, "title", true);
                var entId = ID(rightId, "entities", true);
                container.Add(new CuiElement
                {
                    Parent = rightId,
                    Name = ttlId,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            FadeIn = styles.OVPanelFadeIn,
                            Color = styles.BoxColor
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"{left} {1-styles.UPanelTitleH}",
                            AnchorMax = $"{left+colw} {1-styles.UPanelTitleH}",
                            OffsetMin = $"{styles.ContentPad} {-styles.ContentPad-subtitleH}",
                            OffsetMax = $"{-styles.ContentPad} {-styles.ContentPad}"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = ttlId,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            FadeIn = styles.OVPanelFadeIn,
                            Color = styles.TextColor,
                            Align = UnityEngine.TextAnchor.MiddleCenter,
                            Text = col.Title
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = rightId,
                    Name = entId,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            FadeIn = styles.OVPanelFadeIn,
                            Color = styles.BoxColor
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"{left} {0}",
                            AnchorMax = $"{left+colw} {1-styles.UPanelTitleH}",
                            OffsetMin = $"{styles.ContentPad} {0}",
                            OffsetMax = $"{-styles.ContentPad} {-styles.ContentPad-subtitleH-styles.ContentPad}"
                        }
                    }
                });
                var entryH = 0.085f;
                var entryT = 1f;
                foreach (var entry in col.Data)
                {
                    var entryId = ID(entId, "entry", true);
                    var checkColor = entry.IsProtected ? "0 0.6 0 1" : "1 0 0 1";
                    var checkIcon = entry.IsProtected ? "rp.check" : "rp.cross";
                    var iconSz = 11;
                    container.Add(new CuiElement
                    {
                        Parent = entId,
                        Name = entryId,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                FadeIn = styles.OVPanelFadeIn,
                                Color = styles.BoxColor
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"{0} {entryT-entryH}",
                                AnchorMax = $"{1} {entryT}",
                                OffsetMin = $"{styles.ContentPad} {0}",
                                OffsetMax = $"{-styles.ContentPad} {-styles.ContentPad}"
                            }
                        }
                    });
                    container.Add(new CuiElement
                    {
                        Parent = entryId,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                FadeIn = styles.OVPanelFadeIn,
                                Png = ImageLibrary?.Call<string>("GetImage", checkIcon),
                                Color = checkColor
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = MIDDLE_LEFT,
                                AnchorMax = MIDDLE_LEFT,
                                OffsetMin = $"{styles.ContentPad + -iconSz/2} {-iconSz/2}",
                                OffsetMax = $"{styles.ContentPad + iconSz/2} {iconSz/2}"
                            }
                        }
                    });
                    container.Add(new CuiElement
                    {
                        Parent = entryId,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                FadeIn = styles.OVPanelFadeIn,
                                Text = entry.Text,
                                FontSize = 11,
                                Color = styles.TextColor,
                                Align = UnityEngine.TextAnchor.MiddleRight
                            },
                            new CuiRectTransformComponent
                            {
                                OffsetMin = $"{styles.ContentPad} {0}",
                                OffsetMax = $"{-styles.ContentPad} {0}"
                            }
                        }
                    });
                    entryT -= entryH;
                }

                left += colw;
            }
            #endregion
            return container;
        }

        private CuiElementContainer CreateCostsPage(CuiElementContainer container, InfoPanelStyles styles, BasePlayer basePlayer, ProtectedEntity tc)
        {
            // Balance
            var balanceid = ID(INFO_PANEL_CONTENT_ID, "balance");
            var balanceH = styles.UPanelTitleH * 2;
            container.Add(new CuiElement
            {
                Parent = INFO_PANEL_CONTENT_ID,
                Name = balanceid,
                Components =
                {
                    new CuiImageComponent
                    {
                        FadeIn = styles.OVPanelFadeIn,
                        Color = styles.BoxColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{0} {1f-balanceH}",
                        AnchorMax = $"{0.3} {1f}",
                        OffsetMin = $"{0} {0}",
                        OffsetMax = $"{-styles.ContentPad} {0}"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = balanceid,
                Components =
                {
                    new CuiTextComponent
                    {
                        FadeIn = styles.OVPanelFadeIn,
                        Text = Lang("info balance label", basePlayer),
                        FontSize = 12,
                        Align = UnityEngine.TextAnchor.UpperCenter,
                        Color = styles.TextColor
                    },
                    new CuiRectTransformComponent
                    {
                        OffsetMin = $"{styles.ContentPad} {styles.ContentPad}",
                        OffsetMax = $"{-styles.ContentPad} {-styles.ContentPad}"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = balanceid,
                Components =
                {
                    new CuiTextComponent
                    {
                        FadeIn = styles.OVPanelFadeIn,
                        Text = FormatCurrency(basePlayer.UserIDString, (float)Math.Round(tc.Balance, 1)),
                        Align = UnityEngine.TextAnchor.LowerCenter,
                        Color = styles.TextColor,
                        FontSize = 22
                    },
                    new CuiRectTransformComponent
                    {
                        OffsetMin = $"{styles.ContentPad} {styles.ContentPad}",
                        OffsetMax = $"{-styles.ContentPad} {-styles.ContentPad}"
                    }
                }
            });
            // Balance Info
            var balanceinfoid = ID(INFO_PANEL_CONTENT_ID, "balanceinfo");
            container.Add(new CuiElement
            {
                Parent = INFO_PANEL_CONTENT_ID,
                Name = balanceinfoid,
                Components =
                {
                    new CuiImageComponent
                    {
                        FadeIn = styles.OVPanelFadeIn,
                        Color = styles.BoxColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{0} {0}",
                        AnchorMax = $"{0.3} {1f-balanceH}",
                        OffsetMin = $"{styles.ContentPad} {styles.ContentPad}",
                        OffsetMax = $"{-styles.ContentPad*2} {-styles.ContentPad}"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = balanceinfoid,
                Components =
                {
                    new CuiTextComponent
                    {
                        FadeIn = styles.OVPanelFadeIn,
                        Text = Lang("info balance info", basePlayer),
                        Align = UnityEngine.TextAnchor.UpperLeft,
                        Color = styles.SubtextColor,
                        FontSize = 10
                    },
                    new CuiRectTransformComponent
                    {
                        OffsetMin = $"{styles.ContentPad} {styles.ContentPad}",
                        OffsetMax = $"{-styles.ContentPad} {-styles.ContentPad}"
                    }
                }
            });
            // Title
            var titleId = ID(INFO_PANEL_CONTENT_ID, "title");
            var titleH = styles.UPanelTitleH * 2;
            container.Add(new CuiElement
            {
                Parent = INFO_PANEL_CONTENT_ID,
                Name = titleId,
                Components =
                {
                    new CuiImageComponent
                    {
                        FadeIn = styles.OVPanelFadeIn,
                        Color = styles.BoxColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{0.3} {1f-titleH}",
                        AnchorMax = $"{1f} {1f}",
                        OffsetMin = $"{styles.ContentPad} {0}",
                        OffsetMax = $"{0} {0}"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = titleId,
                Components =
                {
                    new CuiTextComponent
                    {
                        FadeIn = styles.OVPanelFadeIn,
                        Text = Lang("info total hourly cost", basePlayer),
                        FontSize = 12,
                        Align = UnityEngine.TextAnchor.UpperCenter,
                        Color = styles.TextColor
                    },
                    new CuiRectTransformComponent
                    {
                        OffsetMin = $"{styles.ContentPad} {styles.ContentPad}",
                        OffsetMax = $"{-styles.ContentPad} {-styles.ContentPad}"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = titleId,
                Components =
                {
                    new CuiTextComponent
                    {
                        FadeIn = styles.OVPanelFadeIn,
                        Text = $"{FormatCurrency(basePlayer.UserIDString, tc.TotalProtectionCostPerHour)}",
                        Align = UnityEngine.TextAnchor.LowerCenter,
                        Color = styles.TextColor,
                        FontSize = 22
                    },
                    new CuiRectTransformComponent
                    {
                        OffsetMin = $"{styles.ContentPad} {styles.ContentPad}",
                        OffsetMax = $"{-styles.ContentPad} {-styles.ContentPad}"
                    }
                }
            });
            var costs = new[]
            {
                new
                {
                    Text = Lang("info hourly base cost", basePlayer),
                    Note = Lang("info hourly base cost note", basePlayer, tc.HighestProtectionLevel.Rank),
                    Info = Lang("info hourly base cost info", basePlayer),
                    Value = $"{FormatCurrency(basePlayer.UserIDString, tc.BaseCostPerHour)}"
                },
                new
                {
                    Text = Lang("info hourly foundation cost", basePlayer),
                    Note = Lang("info hourly foundation cost note", basePlayer, tc.FoundationCount, FormatCurrency(basePlayer.UserIDString, tc.HighestProtectionLevel.HourlyCostPerFloor)),
                    Info = Lang("info hourly foundation cost info", basePlayer),
                    Value = $"{FormatCurrency(basePlayer.UserIDString, tc.BuildingCostPerHour)}"
                },
                new
                {
                    Text = Lang("info hourly owner cost", basePlayer),
                    Note = Lang("info hourly owner cost note", basePlayer, tc.Owners.Count, FormatCurrency(basePlayer.UserIDString, tc.HighestProtectionLevel.HourlyCostPerOwner)),
                    Info = Lang("info hourly owner cost info", basePlayer),
                    Value = $"{FormatCurrency(basePlayer.UserIDString, tc.OwnerCostPerHour)}"
                }
            };
            var panelH = (1f - titleH) / costs.Length;
            var t = 1f-titleH;
            var left = 0.3f;
            var panelW = (1f - left) / 2;
            foreach (var cost in costs)
            {
                var eid = ID(INFO_PANEL_CONTENT_ID, "entry", true);
                var eidright = ID(INFO_PANEL_CONTENT_ID, "entryright", true);
                // left
                container.Add(new CuiElement
                {
                    Parent = INFO_PANEL_CONTENT_ID,
                    Name = eid,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            FadeIn = styles.OVPanelFadeIn,
                            Color = styles.BoxColor
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"{left} {t-panelH}",
                            AnchorMax = $"{left+panelW} {t}",
                            OffsetMin = $"{styles.ContentPad*2} {styles.ContentPad}",
                            OffsetMax = $"{-styles.ContentPad/2} {-styles.ContentPad}"
                        }
                    }
                });
                // right
                container.Add(new CuiElement
                {
                    Parent = INFO_PANEL_CONTENT_ID,
                    Name = eidright,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            FadeIn = styles.OVPanelFadeIn,
                            Color = styles.BoxColor
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"{1f-panelW} {t-panelH}",
                            AnchorMax = $"{1f} {t}",
                            OffsetMin = $"{styles.ContentPad/2} {styles.ContentPad}",
                            OffsetMax = $"{-styles.ContentPad} {-styles.ContentPad}"
                        }
                    }
                });
                // left stuff
                container.Add(new CuiElement
                {
                    Parent = eid,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            FadeIn = styles.OVPanelFadeIn,
                            Text = cost.Text,
                            Align = UnityEngine.TextAnchor.UpperCenter,
                            Color = styles.TextColor,
                            FontSize = 10
                        },
                        new CuiRectTransformComponent
                        {
                            OffsetMin = $"{styles.ContentPad} {styles.ContentPad}",
                            OffsetMax = $"{-styles.ContentPad} {-styles.ContentPad}"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = eid,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            FadeIn = styles.OVPanelFadeIn,
                            Text = cost.Note,
                            Align = UnityEngine.TextAnchor.MiddleCenter,
                            Color = styles.SubtextColor,
                            FontSize = 10
                        },
                        new CuiRectTransformComponent
                        {
                            OffsetMin = $"{styles.ContentPad} {styles.ContentPad*2}",
                            OffsetMax = $"{-styles.ContentPad} {-styles.ContentPad}"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = eid,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            FadeIn = styles.OVPanelFadeIn,
                            Text = cost.Value,
                            Align = UnityEngine.TextAnchor.LowerCenter,
                            Color = styles.TextColor,
                            FontSize = 18
                        },
                        new CuiRectTransformComponent
                        {
                            OffsetMin = $"{styles.ContentPad} {styles.ContentPad}",
                            OffsetMax = $"{-styles.ContentPad} {-styles.ContentPad}"
                        }
                    }
                });
                // right stuff
                container.Add(new CuiElement
                {
                    Parent = eidright,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            FadeIn = styles.OVPanelFadeIn,
                            Text = cost.Info,
                            Align = UnityEngine.TextAnchor.UpperCenter,
                            Color = styles.SubtextColor,
                            FontSize = 10
                        },
                        new CuiRectTransformComponent
                        {
                            OffsetMin = $"{styles.ContentPad} {styles.ContentPad}",
                            OffsetMax = $"{-styles.ContentPad} {-styles.ContentPad}"
                        }
                    }
                });
                t -= panelH;
            }
            return container;
        }

        private void CloseInfoPanel(BasePlayer basePlayer)
        {
            CuiHelper.DestroyUi(basePlayer, INFO_PANEL_SHADOW_ID);
            CuiHelper.DestroyUi(basePlayer, INFO_PANEL_ID);
        }
    }
}

namespace Oxide.Plugins
{
    partial class RaidProtection
    {
        private void UILevelsShow(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer != null)
            {
                ShowLevels(basePlayer);
            }
        }

        [Command("lev.close")]
        private void UILevelsClose(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer != null)
            {
                CloseLevels(basePlayer);
            }
        }

        private readonly static string LEVELS_ID = "rp.levels";
        private readonly static string LEVELS_SHADOW_ID = "rp.levels.shadow";

        private void ShowLevels(BasePlayer basePlayer)
        {
            if (basePlayer == null) { return; }
            var styles = new InfoPanelStyles();
            var pl = ProtectionLevel.GetProtectionLevelOfPlayer(basePlayer.UserId());
            var w = 600;
            var h = 400;
            var container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Name = LEVELS_SHADOW_ID,
                Parent = "Overlay",
                Components =
                {
                    new CuiButtonComponent
                    {
                        Color = styles.ShadowColor,
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                        Command = "lev.close"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Name = LEVELS_ID,
                Parent = "Overlay",
                Components =
                {
                    new CuiNeedsCursorComponent{},
                    new CuiImageComponent
                    {
                        Color = RustColor.DarkGray
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = $"{-w/2} {-h/2}",
                        OffsetMax = $"{w/2} {h/2}"
                    }
                }
            });
            var titleh = 30;
            var title = ID(LEVELS_ID, "title");
            container.Add(new CuiElement
            {
                Name = title,
                Parent = LEVELS_ID,
                Components =
                {
                    new CuiImageComponent
                    {
                        FadeIn = styles.OVPanelFadeIn,
                        Color = RustColor.DarkBrown
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"{0} {-titleh}",
                        OffsetMax = $"{0} {0}"
                    }
                }
            });
            var content = ID(LEVELS_ID, "content");
            container.Add(new CuiElement
            {
                Name = content,
                Parent = LEVELS_ID,
                Components =
                {
                    new CuiImageComponent
                    {
                        FadeIn = styles.OVPanelFadeIn,
                        Color = TRANSPARENT
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{styles.ContentPad} {styles.ContentPad}",
                        OffsetMax = $"{-styles.ContentPad} {-titleh-styles.ContentPad}"
                    }
                }
            });
            /* Title */
            container.Add(new CuiElement
            {
                Parent = title,
                Components =
                {
                    new CuiTextComponent
                    {
                        FadeIn = styles.OVPanelFadeIn,
                        Text = Lang("title", basePlayer).TitleCase(),
                        Color = RustColor.LightBrown,
                        Align = UnityEngine.TextAnchor.MiddleCenter
                    }
                }
            });
            /* Close Button */
            var closebtn = ID(title, "close");
            container.Add(new CuiElement
            {
                Parent = title,
                Name = closebtn,
                Components =
                {
                    new CuiImageComponent
                    {
                        FadeIn = styles.OVPanelFadeIn,
                        Png = ImageLibrary?.Call<string>("GetImage", "rp.cross"),
                        Color = styles.TextColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = BOTTOM_RIGHT,
                        AnchorMax = TOP_RIGHT,
                        OffsetMin = $"{-styles.HeaderHeight+styles.HeaderImgPad} {styles.HeaderImgPad}",
                        OffsetMax = $"{-styles.HeaderImgPad} {-styles.HeaderImgPad}"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = closebtn,
                Components =
                {
                    new CuiButtonComponent
                    {
                        FadeIn = styles.OVPanelFadeIn,
                        Color = TRANSPARENT,
                        Command = "lev.close"
                    }
                }
            });
            /* Rank Text */
            container.Add(new CuiElement
            {
                Parent = content,
                Components =
                {
                    new CuiTextComponent
                    {
                        FadeIn = styles.OVPanelFadeIn,
                        Text = Lang("levels protection level", basePlayer, pl.Rank),
                        Color = RustColor.LightBrown,
                        FontSize = 18,
                        Align = UnityEngine.TextAnchor.UpperCenter
                    },
                    new CuiRectTransformComponent
                    {
                        OffsetMin = $"{0} {0}",
                        OffsetMax = $"{0} {-10}"
                    }
                }
            });
            /* Description */
            container.Add(new CuiElement
            {
                Parent = content,
                Components =
                {
                    new CuiTextComponent
                    {
                        FadeIn = styles.OVPanelFadeIn,
                        Text = Lang("levels description", basePlayer),
                        Color = RustColor.LightBrown,
                        FontSize = 12,
                        Align = UnityEngine.TextAnchor.UpperCenter
                    },
                    new CuiRectTransformComponent
                    {
                        OffsetMin = $"{0} {0}",
                        OffsetMax = $"{0} {-40}"
                    }
                }
            });
            /* Grid */
            var gridtop = 60;
            var grid = ID(LEVELS_ID, "grid");
            container.Add(new CuiElement
            {
                Name = grid,
                Parent = content,
                Components =
                {
                    new CuiImageComponent
                    {
                        FadeIn = styles.OVPanelFadeIn,
                        Color = TRANSPARENT
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{0} {0}",
                        OffsetMax = $"{0} {-gridtop}"
                    }
                }
            });

            var panels = new[]
            {
                new
                {
                    Title = Lang("levels protection", basePlayer),
                    Values = new []
                    {
                        new
                        {
                            Label = Lang("levels online", basePlayer),
                            Value = $"{pl.OnlineProtectionPercent}%"
                        },
                        new
                        {
                            Label = Lang("levels offline", basePlayer),
                            Value = $"{pl.OfflineProtectionPercent}%"
                        },
                        new
                        {
                            Label = Lang("levels max hours", basePlayer),
                            Value = pl.MaxProtectionTimeHours?.ToString() ?? Lang("levels no limit", basePlayer)
                        }
                    }
                },
                new
                {
                    Title = Lang("levels delays", basePlayer),
                    Values = new []
                    {
                        new
                        {
                            Label = Lang("levels after offline", basePlayer),
                            Value = $"{pl.OfflineProtectionDelay}s"
                        },
                        new
                        {
                            Label = Lang("levels after damaged", basePlayer),
                            Value = $"{pl.ProtectedDelayAfterTakingDamage}s"
                        }
                    }
                },
                new
                {
                    Title = Lang("levels costs", basePlayer),
                    Values = new []
                    {
                        new
                        {
                            Label = Lang("levels base cost", basePlayer),
                            Value = FormatCurrency(basePlayer.UserIDString, pl.HourlyBaseCost)
                        },
                        new
                        {
                            Label = Lang("levels foundation cost", basePlayer),
                            Value = FormatCurrency(basePlayer.UserIDString, pl.HourlyCostPerFloor)
                        },
                        new
                        {
                            Label = Lang("levels owner cost", basePlayer),
                            Value = FormatCurrency(basePlayer.UserIDString, pl.HourlyCostPerOwner)
                        },
                        new
                        {
                            Label = Lang("levels damage cost", basePlayer),
                            Value = FormatCurrency(basePlayer.UserIDString, pl.CostPerDamageProtected)
                        }
                    }
                }
            };
            var panelw = 1f / panels.Length;
            var panelleft = 0f;
            foreach (var panel in panels)
            {
                var panelid = ID(grid, "panel", true);
                /* Panel Base */
                container.Add(new CuiElement
                {
                    Name = panelid,
                    Parent = grid,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            FadeIn = styles.OVPanelFadeIn,
                            Color = TRANSPARENT
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = Anchor(panelleft, 0),
                            AnchorMax = Anchor(panelleft+panelw, 1),
                            OffsetMin = Offset(styles.ContentPad, styles.ContentPad),
                            OffsetMax = Offset(-styles.ContentPad, -styles.ContentPad)
                        }
                    }
                });
                /* Panel Title Base */
                var panelbase = ID(panelid, "base");
                container.Add(new CuiElement
                {
                    Name = panelbase,
                    Parent = panelid,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            FadeIn = styles.OVPanelFadeIn,
                            Color = RustColor.DarkBrown
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = Anchor(0, 0.8f),
                            AnchorMax = Anchor(1, 1f),
                            OffsetMin = Offset(styles.ContentPad, styles.ContentPad),
                            OffsetMax = Offset(-styles.ContentPad, -styles.ContentPad)
                        }
                    }
                });
                /* Panel Title */
                container.Add(new CuiElement
                {
                    Parent = panelbase,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            FadeIn = styles.OVPanelFadeIn,
                            Color = RustColor.LightBrown,
                            Text = panel.Title,
                            FontSize = 18,
                            Align = UnityEngine.TextAnchor.MiddleCenter
                        }
                    }
                });
                /* Subpanels */
                var subpanelt = 0.8f;
                //var subpanelh = subpanelt / panel.Values.Length;
                var subpanelh = 0.15f;
                var subpanelpad = styles.ContentPad / 2;
                foreach (var value in panel.Values)
                {
                    var subpanel = ID(panelid, "subpanel", true);
                    /* Box */
                    container.Add(new CuiElement
                    {
                        Name = subpanel,
                        Parent = panelid,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                FadeIn = styles.OVPanelFadeIn,
                                Color = RustColor.DarkBrown
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = Anchor(0, subpanelt-subpanelh),
                                AnchorMax = Anchor(1, subpanelt),
                                OffsetMin = Offset(styles.ContentPad, subpanelpad),
                                OffsetMax = Offset(-styles.ContentPad, -subpanelpad)
                            }
                        }
                    });
                    /* Label */
                    container.Add(new CuiElement
                    {
                        Parent = subpanel,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                FadeIn = styles.OVPanelFadeIn,
                                Color = RustColor.LightBrown,
                                Text = value.Label,
                                FontSize = 12,
                                Align = UnityEngine.TextAnchor.MiddleLeft
                            },
                            new CuiRectTransformComponent
                            {
                                OffsetMin = Offset(styles.ContentPad, 0),
                                OffsetMax = Offset(-styles.ContentPad, 0)
                            }
                        }
                    });
                    /* Value */
                    container.Add(new CuiElement
                    {
                        Parent = subpanel,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                FadeIn = styles.OVPanelFadeIn,
                                Color = RustColor.LightBrown,
                                Text = value.Value,
                                FontSize = 12,
                                Align = UnityEngine.TextAnchor.MiddleRight
                            },
                            new CuiRectTransformComponent
                            {
                                OffsetMin = Offset(styles.ContentPad, 0),
                                OffsetMax = Offset(-styles.ContentPad, 0)
                            }
                        }
                    });
                    subpanelt -= subpanelh;
                }
                panelleft += panelw;
            }
            CloseLevels(basePlayer);
            CuiHelper.AddUi(basePlayer, container);
        }
        private void CloseLevels(BasePlayer basePlayer)
        {
            CuiHelper.DestroyUi(basePlayer, LEVELS_SHADOW_ID);
            CuiHelper.DestroyUi(basePlayer, LEVELS_ID);
        }
    }
}

namespace Oxide.Plugins
{
    partial class RaidProtection
    {
        private static class Security
        {
            public static Guid Token { get; private set; }

            public static void GenerateToken()
            {
                Token = Guid.NewGuid();
            }

            public static TokenResponse ValidateTokenArgs(string[] args)
            {
                var token = args[0];
                bool success = token == Token.ToString();
                if (!success)
                {
                    PLUGIN.PrintWarning("Attempted to call a secure command without a valid token");
                }
                return new TokenResponse
                {
                    Success = success,
                    Args = args.Skip(1).ToArray()
                };
            }

            public class TokenResponse
            {
                public bool Success;
                public string[] Args;
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class RaidProtection
    {
        [Command("tc.ui.activate")]
        private void TcActivate(IPlayer player, string command, string[] args)
        {
            var result = Security.ValidateTokenArgs(args);
            if (!result.Success) { return; } else { args = result.Args; }
            var basePlayer = player.Object as BasePlayer;
            var tc = ProtectedCupboardManager.GetByID(ulong.Parse(args[0]));
            if (tc == null) { return; }
            if (tc.DisableProtectionFromExceedingFounderLimit && tc.HighestProtectionLevel.HasFounderLimit && ProtectedCupboardManager.GetCupboardFounderCount(basePlayer.UserId()) < tc.HighestProtectionLevel.FounderLimit)
            {
                tc.DisableProtectionFromExceedingFounderLimit = false;
                if (tc.HasFreeProtection)
                {
                    StartProtection(tc, () =>
                    {
                        ShowProtectionStatusOverlay(basePlayer, tc);
                    });
                }
            }
        }

        [Command("tc.ui.withdraw")]
        private void TcWithdraw(IPlayer player, string command, string[] args)
        {
            var result = Security.ValidateTokenArgs(args);
            if (!result.Success) { return; } else { args = result.Args; }
            var basePlayer = player.Object as BasePlayer;
            if (IsNull(basePlayer)) return;
            var tc = ProtectedCupboardManager.GetByID(ulong.Parse(args[0]));
            if (IsNull(tc) || !tc.CanClickStatusUI) { return; }
            tc.LastStatusUIClickTime = DateTime.Now;
            var bal = tc.Balance;
            if (bal <= 0) return;
            GiveBalanceResource(basePlayer, bal);
            ClearProtectionBalance(tc, true, basePlayer);
            ShowProtectionStatusOverlay(basePlayer, tc);
        }

        [Command("tc.ui.balance")]
        private void TcBalance(IPlayer player, string command, string[] args)
        {
            var result = Security.ValidateTokenArgs(args);
            if (!result.Success) { return; } else { args = result.Args; }
            var basePlayer = player.Object as BasePlayer;
            float amt = float.Parse(args[0]);
            var tc = ProtectedCupboardManager.GetByID(ulong.Parse(args[1]));
            var isMaxWithdraw = bool.Parse(args[2]);
            if (IsNull(tc) || !tc.CanClickStatusUI) { return; }
            tc.LastStatusUIClickTime = DateTime.Now.AddSeconds(isMaxWithdraw ? 0.5f : 0);
            if (tc.HasProtectionTimeLimit)
            {
                amt = Math.Min(amt, tc.AllowedBalanceRemaining.Value);
            }
            if (ConsumeBalanceResource(tc, basePlayer, amt))
            {
                UpdateProtectionBalance(tc, amt, BalanceLedgerReason.Added, basePlayer);
                StartOrStopProtection(tc, () =>
                {
                    if (tc.IsViewingProtectionPanel(basePlayer))
                    {
                        ShowProtectionStatusOverlay(basePlayer, tc);
                    }
                });
            }
        }

        [Command("tc.tab")]
        private void TcTab(IPlayer player, string command, string[] args)
        {
            var result = Security.ValidateTokenArgs(args);
            if (!result.Success) { return; } else { args = result.Args; }
            var basePlayer = player.Object as BasePlayer;
            if (IsNull(basePlayer)) return;
            var tc = ProtectedCupboardManager.GetByID(ulong.Parse(args[0]));
            if (IsNull(tc)) return;
            var tab = int.Parse(args[1]);
            if (tab == 0)
            {
                CloseProtectionStatusOverlay(basePlayer);
                ShowProtectionStatusOverlayTabs(basePlayer, tc, 0);
            }
            if (tab == 1)
            {
                ShowProtectionStatusOverlay(basePlayer, tc);
                ShowProtectionStatusOverlayTabs(basePlayer, tc, 1);
            }
        }

        [Command("tc.toggle")]
        private void TcToggle(IPlayer player, string command, string[] args)
        {
            var result = Security.ValidateTokenArgs(args);
            if (!result.Success) { return; } else { args = result.Args; }
            var basePlayer = player.Object as BasePlayer;
            var priv = basePlayer.GetBuildingPrivilege();
            var tc = ProtectedCupboardManager.GetByID(ulong.Parse(args[0]));
            if (tc == null) { return; }
            if (tc.Enabled)
            {
                tc.Enabled = false;
                StopProtection(tc);
                if (tc.IsViewingProtectionPanel(basePlayer))
                {
                    ShowProtectionStatusOverlay(basePlayer, tc);
                }
            }
            else
            {
                Action callback = () =>
                {
                    NextTick(() =>
                    {
                        if (basePlayer != null && tc != null && tc.IsViewingProtectionPanel(basePlayer))
                        {
                            ShowProtectionStatusOverlay(basePlayer, tc);
                        }
                    });

                };
                tc.Enabled = true;
                StartOrStopProtection(tc, callback);
            }
        }

        [Command("rp.info.show")]
        private void UIInfoShow(IPlayer player, string command, string[] args)
        {
            var result = Security.ValidateTokenArgs(args);
            if (!result.Success) { return; } else { args = result.Args; }
            var basePlayer = player.Object as BasePlayer;
            var netId = ulong.Parse(args[0]);
            var tc = ProtectedCupboardManager.GetByID(netId);
            if (tc != null)
            {
                //ShowProtectionStatusInfo(basePlayer, tc);
            }
        }

        [Command("rp.info.close")]
        private void UIInfoClose(IPlayer player, string command, string[] args)
        {
            var result = Security.ValidateTokenArgs(args);
            if (!result.Success) { return; } else { args = result.Args; }
            var basePlayer = player.Object as BasePlayer;
            CloseProtectionStatusOverlay(basePlayer);
            CloseProtectionStatusOverlayTabs(basePlayer);
        }

        [Command("rp.ui.refresh")]
        private void UIRefresh(IPlayer player, string command, string[] args)
        {
            var result = Security.ValidateTokenArgs(args);
            if (!result.Success) { return; } else { args = result.Args; }
            var basePlayer = player.Object as BasePlayer;
            var netId = ulong.Parse(args[0]);
            var tc = ProtectedCupboardManager.GetByID(netId);
            if (tc != null)
            {
                if (tc.IsViewingProtectionPanel(basePlayer))
                {
                    ShowProtectionStatusOverlay(basePlayer, tc);
                }
            }
        }

        private static readonly string PROTECTION_STATUS_ID = "rp.status";
        private static readonly string PROTECTION_INFO_SHADOW_ID = "rp.info.shadow";
        private static readonly string PROTECTION_INFO_ID = "rp.info";
        private static readonly string PROTECTION_OVERLAY_ID = "rp.overlay";
        private static readonly string PROTECTION_OVERLAY_TABS_ID = "rp.overlay.tabs";
        private static readonly string PROTECTION_OVERLAY_BACKGROUND_ID = "rp.overlay.background";

        public class Styles
        {
            public string BackgroundColor = "0.31765 0.30588 0.27451 1";
            public string BackgroundColorShaded = "0.29412 0.27843 0.25490 1";
            public int TitleHeight = 18;
            public int MiddleHeight = 64;
            public int BottomHeight = 160;
            public int GapHeight = 5;
            public int TitleFontSize = 13;
            public int TitlePad = 8;
            public int InfoFontSize = 13;
            public int InfoPad = 7;
            public string InfoFontColor = "0.9 0.9 0.9 1";
            public int InfoIconSize = 60;
            public int BottomFontSize = 14;
            public int BottomPad = 7;
            public string WhiteColor = "0.87451 0.83529 0.80000 1";
            public string BlueColor = "0.08627 0.25490 0.38431 1";
            public string LightBlueColor = "0.25490 0.61176 0.86275 1";
            public string GrayColor = "0.45490 0.43529 0.40784 1";
            public string LightGrayColor = "0.69804 0.66667 0.63529 1";
            public string OrangeColor = "1.00000 0.53333 0.18039 1";
            public string RedColor = "0.52549 0.19608 0.14118 1";
            public string GreenColor = "0.25490 0.30980 0.14510 1";
            public string LightGreenColor = "0.76078 0.94510 0.41176 1";
            public string LightRedColor = "0.91373 0.77647 0.75686 1";
            public float FadeIn = 0f;
        }

        private CuiElementContainer CreateProtectionStatusOverlayBackground(CuiElementContainer container, BasePlayer basePlayer)
        {
            var styles = new Styles();
            var x = 192;
            var w = 381;
            var y = 454;
            var fadein = 0f;
            /* Base */
            container.Add(new CuiElement
            {
                Parent = "Hud",
                Name = PROTECTION_OVERLAY_BACKGROUND_ID,
                Components =
                {
                    new CuiImageComponent
                    {
                        FadeIn = fadein,
                        Color = TRANSPARENT
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{0.5} {0}",
                        AnchorMax = $"{0.5} {0}",
                        OffsetMin = Offset(x, 1+y),
                        OffsetMax = Offset(x+w, 1+y)
                    }
                }
            });
            /* Full */
            container.Add(new CuiElement
            {
                Parent = PROTECTION_OVERLAY_BACKGROUND_ID,
                Components =
                {
                    new CuiImageComponent
                    {
                        FadeIn = styles.FadeIn,
                        Color = styles.BackgroundColorShaded,
                        Material = "assets/icons/greyout.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = TOP_LEFT,
                        AnchorMax = TOP_RIGHT,
                        OffsetMin = Offset(-2, -100),
                        OffsetMax = Offset(2.5f, 165),
                    }
                }
            });
            return container;
        }

        private void ShowProtectionStatusOverlayTabs(BasePlayer basePlayer, ProtectedEntity tc, int page = 0)
        {
            CuiElementContainer container = new CuiElementContainer();
            container = CreateProtectionStatusOverlayBackground(container, basePlayer);
            /* Base */
            var styles = new Styles();
            var x = 285 + config.Protection.PanelTabs.OffsetX;
            var w = config.Protection.PanelTabs.TabWidth;
            var y = 620 + config.Protection.PanelTabs.OffsetY;
            container.Add(new CuiElement
            {
                Parent = "Overlay",
                Name = PROTECTION_OVERLAY_TABS_ID,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = TRANSPARENT  
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{0.5} {0}",
                        AnchorMax = $"{0.5} {0}",
                        OffsetMin = Offset(x, y+styles.GapHeight),
                        OffsetMax = Offset(x+w, y+styles.GapHeight+30)
                    }
                }
            });
            /* Tabs */
            var tabs = new[]
            {
                new
                {
                    Selected = page == 0,
                    Command = $"tc.tab {Security.Token} {tc.ID} {0}",
                    Text = Lang("ui tab upkeep", basePlayer)
                },
                new
                {
                    Selected = page == 1,
                    Command = $"tc.tab {Security.Token} {tc.ID} {1}",
                    Text = Lang("ui tab protection", basePlayer)
                }
            };
            var left = 0f;
            var tabw = 1f / tabs.Length;
            var gap = 4;
            foreach(var tab in tabs)
            {
                var tabid = ID(PROTECTION_OVERLAY_TABS_ID, "tab", true);
                container.Add(new CuiElement
                {
                    Parent = PROTECTION_OVERLAY_TABS_ID,
                    Name = tabid,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Color = tab.Selected ? styles.BlueColor : styles.BackgroundColor,
                            Command = tab.Command
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"{left} {0}",
                            AnchorMax = $"{left+tabw} {1}",
                            OffsetMin = Offset(gap, 0),
                            OffsetMax = Offset(-gap, 0),
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = tabid,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = tab.Text,
                            Color = tab.Selected ? styles.LightBlueColor : styles.LightGrayColor,
                            Align = UnityEngine.TextAnchor.MiddleCenter
                        }
                    }
                });
                left += tabw;
            }

            CloseProtectionStatusOverlayTabs(basePlayer);
            CuiHelper.AddUi(basePlayer, container);
        }

        private void CloseProtectionStatusOverlayTabs(BasePlayer basePlayer)
        {
            CuiHelper.DestroyUi(basePlayer, PROTECTION_OVERLAY_TABS_ID);
            CuiHelper.DestroyUi(basePlayer, PROTECTION_OVERLAY_BACKGROUND_ID);
        }

        public static HashSet<ulong> PlayersViewingOverlay = new HashSet<ulong>();

        private void ShowProtectionStatusOverlay(BasePlayer basePlayer, ProtectedEntity tc)
        {
            CuiElementContainer container = new CuiElementContainer();

            var styles = new Styles();
            var x = 192;
            var w = 381;
            var y = 614;
            var h = 252;
            var lsoffset = 0.4f;
            var rsoffset = -0.7f;
            /* Base */
            container.Add(new CuiElement
            {
                Parent = "Overlay",
                Name = PROTECTION_OVERLAY_ID,
                Components =
                {
                    new CuiImageComponent
                    {
                        FadeIn = styles.FadeIn,
                        Color = TRANSPARENT
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{0.5} {0}",
                        AnchorMax = $"{0.5} {0}",
                        OffsetMin = Offset(x, 1+y-h),
                        OffsetMax = Offset(x+w, 1+y)
                    }
                }
            });
            /* Top */
            var topid = ID(PROTECTION_OVERLAY_ID, "top");
            container.Add(new CuiElement
            {
                Parent = PROTECTION_OVERLAY_ID,
                Name = topid,
                Components =
                {
                    new CuiImageComponent
                    {
                        FadeIn = styles.FadeIn,
                        Color = styles.BackgroundColorShaded,
                        Material = "assets/icons/greyout.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = TOP_LEFT,
                        AnchorMax = TOP_RIGHT,
                        OffsetMin = Offset(lsoffset, -styles.TitleHeight-1),
                        OffsetMax = Offset(rsoffset, 1),
                    }
                }
            });
            /* Top - Title */
            container.Add(new CuiElement
            {
                Parent = topid,
                Components =
                {
                    new CuiTextComponent
                    {
                        FadeIn = styles.FadeIn,
                        Text = Lang("title", basePlayer).ToUpper(),
                        FontSize = styles.TitleFontSize,
                        Color = styles.WhiteColor,
                        Align = UnityEngine.TextAnchor.MiddleLeft
                    },
                    new CuiRectTransformComponent
                    {
                        OffsetMin = Offset(styles.TitlePad, 0),
                        OffsetMax = Offset(-styles.TitlePad, 0),
                    }
                }
            });
            if (IsAdmin(basePlayer.IPlayer))
            {
                /* Top - Info */
                container.Add(new CuiElement
                {
                    Parent = topid,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            FadeIn = styles.FadeIn,
                            Text = Lang("tc id", basePlayer, tc.ID),
                            FontSize = 12,
                            Color = "1 1 1 0.2",
                            Align = UnityEngine.TextAnchor.MiddleRight
                        },
                        new CuiRectTransformComponent
                        {
                            OffsetMin = Offset(styles.TitlePad, 0),
                            OffsetMax = Offset(-styles.TitlePad, 0),
                        }
                    }
                });
            }
            /* Middle */
            var middleid = ID(PROTECTION_OVERLAY_ID, "middle");
            container.Add(new CuiElement
            {
                Parent = PROTECTION_OVERLAY_ID,
                Name = middleid,
                Components =
                {
                    new CuiImageComponent
                    {
                        FadeIn = styles.FadeIn,
                        Color = styles.BackgroundColorShaded,
                        Material = "assets/icons/greyout.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = TOP_LEFT,
                        AnchorMax = TOP_RIGHT,
                        OffsetMin = Offset(lsoffset, -styles.TitleHeight-styles.GapHeight-styles.MiddleHeight),
                        OffsetMax = Offset(rsoffset, -styles.TitleHeight-styles.GapHeight-1),
                    }
                }
            });
            /* Middle - Icon */
            var imgp = 12;
            var xoff = 6;
            container.Add(new CuiElement
            {
                Parent = middleid,
                Components =
                {
                    new CuiImageComponent
                    {
                        FadeIn = styles.FadeIn,
                        Png = GetImage("status.protected"),
                        Color = styles.GrayColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = MIDDLE_LEFT,
                        AnchorMax = MIDDLE_LEFT,
                        OffsetMin = Offset(styles.InfoPad+xoff, -styles.InfoIconSize/2+imgp),
                        OffsetMax = Offset(styles.InfoPad+styles.InfoIconSize-(2*imgp)+xoff, styles.InfoIconSize/2-imgp),
                    }
                }
            });
            /* Middle - Text */
            container.Add(new CuiElement
            {
                Parent = middleid,
                Components =
                {
                    new CuiTextComponent
                    {
                        FadeIn = styles.FadeIn,
                        Text = Lang("ui info text", basePlayer),
                        Font = "RobotoCondensed-Regular.ttf",
                        FontSize = styles.InfoFontSize,
                        Color = styles.LightGrayColor,
                        Align = UnityEngine.TextAnchor.UpperLeft
                    },
                    new CuiRectTransformComponent
                    {
                        OffsetMin = Offset(styles.InfoPad+styles.InfoIconSize, styles.InfoPad),
                        OffsetMax = Offset(-styles.InfoPad-4, -styles.InfoPad),
                    }
                }
            });
            /* Bottom */
            var bottomid = ID(PROTECTION_OVERLAY_ID, "bottom");
            container.Add(new CuiElement
            {
                Parent = PROTECTION_OVERLAY_ID,
                Name = bottomid,
                Components =
                {
                    new CuiImageComponent
                    {
                        FadeIn = styles.FadeIn,
                        Color = Opacity(styles.BackgroundColorShaded, 1f),
                        Material = "assets/icons/greyout.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = BOTTOM_LEFT,
                        AnchorMax = BOTTOM_RIGHT,
                        OffsetMin = Offset(lsoffset, -1),
                        OffsetMax = Offset(rsoffset, styles.BottomHeight),
                    }
                }
            });
            /* Bottom - Cost Title */
            container.Add(new CuiElement
            {
                Parent = bottomid,
                Components =
                {
                    new CuiTextComponent
                    {
                        FadeIn = styles.FadeIn,
                        Text = Lang("ui cost per hour", basePlayer),
                        FontSize = styles.BottomFontSize,
                        Color = styles.OrangeColor,
                        Align = UnityEngine.TextAnchor.UpperCenter
                    },
                    new CuiRectTransformComponent
                    {
                        OffsetMin = Offset(styles.BottomPad-1, styles.BottomPad),
                        OffsetMax = Offset(-styles.BottomPad, -styles.BottomPad+1),
                    }
                }
            });
            /* Bottom - Max Time */
            if (tc.IsAtMaxBalance)
            {
                container.Add(new CuiElement
                {
                    Parent = bottomid,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            FadeIn = styles.FadeIn,
                            Text = Lang("ui max time reached", basePlayer),
                            FontSize = 10,
                            Color = RustColor.LightGreen,
                            Align = UnityEngine.TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            OffsetMin = Offset(-20, -60),
                            OffsetMax = Offset(20, 0),
                        }
                    }
                });
            }
            /* Bottom - Damage Cost Text */
            if (tc.IsProtected && tc.CostPerDamageProtected > 0)
            {
                var msgoffset = -20;
                container.Add(new CuiElement
                {
                    Parent = bottomid,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            FadeIn = styles.FadeIn,
                            Text = Lang("ui damage cost", basePlayer),
                            FontSize = 9,
                            Color = RustColor.LightOrange,
                            Align = UnityEngine.TextAnchor.UpperCenter
                        },
                        new CuiRectTransformComponent
                        {
                            OffsetMin = Offset(styles.BottomPad-2, styles.BottomPad+3+msgoffset),
                            OffsetMax = Offset(-styles.BottomPad, -styles.BottomPad+msgoffset),
                        }
                    }
                });
            }            
            /* Bottom - Balance Text */
            if (config.Integration.Economics || config.Integration.ServerRewards)
            {
                var msgoffset = -10;
                container.Add(new CuiElement
                {
                    Parent = bottomid,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            FadeIn = styles.FadeIn,
                            Text = Lang("ui your balance", basePlayer, FormatCurrency(basePlayer.UserIDString, (float)GetBalanceResourceAmount(tc, basePlayer))),
                            FontSize = 10,
                            Color = RustColor.LightGray,
                            Align = UnityEngine.TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            OffsetMin = Offset(styles.BottomPad-2, styles.BottomPad+3+msgoffset),
                            OffsetMax = Offset(-styles.BottomPad, -styles.BottomPad+msgoffset),
                        }
                    }
                });
            }
            /* Bottom - Protected Title */
            var text = tc.IsProtected ? Lang("ui protected", basePlayer, tc.CurrentProtectionPercent, FormatTimeShort(tc.HoursOfProtection)) : StatusReasonToString(tc, basePlayer, tc.Reason);
            container.Add(new CuiElement
            {
                Parent = bottomid,
                Components =
                {
                    new CuiTextComponent
                    {
                        FadeIn = styles.FadeIn,
                        Text = text,
                        FontSize = styles.BottomFontSize,
                        Color = tc.IsProtected ? styles.LightGreenColor : styles.OrangeColor,
                        Align = UnityEngine.TextAnchor.LowerCenter
                    },
                    new CuiRectTransformComponent
                    {
                        OffsetMin = Offset(styles.BottomPad-2, styles.BottomPad+3),
                        OffsetMax = Offset(-styles.BottomPad, -styles.BottomPad),
                    }
                }
            });
            /* Bottom - Cost Text */
            if (config.Integration.ServerRewards || config.Integration.Economics)
            {
                container.Add(new CuiElement
                {
                    Parent = bottomid,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            FadeIn = styles.FadeIn,
                            Text = $"{FormatCurrency(basePlayer.UserIDString, tc.TotalProtectionCostPerHour)}",
                            FontSize = 16,
                            Align = UnityEngine.TextAnchor.LowerCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = MIDDLE_CENTER,
                            AnchorMax = MIDDLE_CENTER,
                            OffsetMin = Offset(-40, 0),
                            OffsetMax = Offset(40, 28),
                        }
                    }
                });
            }
            else
            {
                var currentItemToLower = config.Protection.CurrencyItem.ToLower();
                container.Add(new CuiElement
                {
                    Parent = bottomid,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            FadeIn = styles.FadeIn,
                            Png = ImageLibrary?.Call<string>("GetImage", (currentItemToLower == "scrap" ? "rp.scrap" : currentItemToLower))
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = MIDDLE_CENTER,
                            AnchorMax = MIDDLE_CENTER,
                            OffsetMin = Offset(-14-1, -14+3),
                            OffsetMax = Offset(14-1, 14+3),
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = bottomid,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            FadeIn = styles.FadeIn,
                            Text = $"x{tc.TotalProtectionCostPerHour}",
                            FontSize = 12,
                            Color = styles.LightGrayColor,
                            Align = UnityEngine.TextAnchor.LowerCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = MIDDLE_CENTER,
                            AnchorMax = MIDDLE_CENTER,
                            OffsetMin = Offset(-84, -22),
                            OffsetMax = Offset(100, -20+40),
                        }
                    }
                });
            }
            /* Bottom - Buttons */
            bool allowMaxDeposit = config.Protection.AllowMaxDeposit;
            var maxHours = 0;
            if (allowMaxDeposit)
            {
                var amountInPossesion = GetBalanceResourceAmount(tc, basePlayer);
                maxHours = tc.TotalProtectionCostPerHour <= 0 ? 0 : (int)Math.Floor(amountInPossesion / tc.TotalProtectionCostPerHour.Value);
            }
            var rightButtons = new[]
            {
                new
                {
                    Text = Lang("ui button activate", basePlayer),
                    Command = $"tc.ui.activate {Security.Token} {tc.ID}",
                    Visible = tc.DisableProtectionFromExceedingFounderLimit && tc.HighestProtectionLevel.HasFounderLimit && ProtectedCupboardManager.GetCupboardFounderCount(basePlayer.UserId()) < tc.HighestProtectionLevel.FounderLimit,
                    Enabled = true
                },
                new
                {
                    Text = Lang("ui button 1h", basePlayer),
                    Command = $"tc.ui.balance {Security.Token} {tc.TotalProtectionCostPerHour} {tc.ID} {false}",
                    Visible = !tc.HasFreeProtection && !tc.DisableProtectionFromExceedingFounderLimit,
                    Enabled = !tc.HasFreeProtection && HasBalanceResourceAmount(tc, basePlayer, tc.TotalProtectionCostPerHour.Value) && !tc.IsAtMaxBalance
                },
                new
                {
                    Text = Lang("ui button 24h", basePlayer),
                    Command = $"tc.ui.balance {Security.Token} {tc.TotalProtectionCostPerHour*24} {tc.ID} {false}",
                    Visible = !tc.HasFreeProtection && !tc.DisableProtectionFromExceedingFounderLimit,
                    Enabled = !tc.HasFreeProtection && HasBalanceResourceAmount(tc, basePlayer, tc.TotalProtectionCostPerHour.Value*24) && !tc.IsAtMaxBalance
                },
                new
                {
                    Text = Lang("ui button max", basePlayer),
                    Command = $"tc.ui.balance {Security.Token} {tc.TotalProtectionCostPerHour*maxHours} {tc.ID} {true}",
                    Visible = !tc.HasFreeProtection && allowMaxDeposit && !tc.DisableProtectionFromExceedingFounderLimit,
                    Enabled = !tc.HasFreeProtection && HasBalanceResourceAmount(tc, basePlayer, tc.TotalProtectionCostPerHour.Value) && !tc.IsAtMaxBalance
                }
            };
            var leftButtons = new[]
            {
                new
                {
                    Text = Lang("ui button info", basePlayer),
                    Command = $"tc.info.open {Security.Token} {tc.ID} 0",
                    Color = styles.GrayColor,
                    TextColor = styles.WhiteColor,
                    Visible = true,
                    Enabled = true
                },
                new
                {
                    Text = tc.Enabled ? Lang("ui button pause", basePlayer) : Lang("ui button resume", basePlayer),
                    Command = $"tc.toggle {Security.Token} {tc.ID}",
                    Color = tc.Enabled ? styles.GrayColor : styles.RedColor,
                    TextColor = tc.Enabled ? styles.WhiteColor : styles.LightRedColor,
                    Visible = config.Protection.AllowProtectionPause,
                    Enabled = true
                },
                new
                {
                    Text = Lang("ui button clear", basePlayer),
                    Command = $"tc.ui.withdraw {Security.Token} {tc.ID}",
                    Color = styles.GrayColor,
                    TextColor = styles.WhiteColor,
                    Visible = config.Protection.AllowBalanceWithdraw,
                    Enabled = tc.Balance > 0
                }
            };

            var topoffset = 0;
            var btnh = 20;
            var btnw = 60;
            var btngap = 6;
            foreach(var button in rightButtons)
            {
                if (!button.Visible) { continue; }
                var bid = ID(bottomid, "btn", true);
                container.Add(new CuiElement
                {
                    Parent = bottomid,
                    Name = bid,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            FadeIn = styles.FadeIn,
                            Color = button.Enabled ? styles.GrayColor : Opacity(styles.GrayColor, 0.6f),
                            Command = button.Enabled ? button.Command : String.Empty
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = TOP_RIGHT,
                            AnchorMax = TOP_RIGHT,
                            OffsetMin = Offset(-btnw-styles.BottomPad, topoffset-btnh-styles.BottomPad),
                            OffsetMax = Offset(-styles.BottomPad, topoffset-styles.BottomPad),
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = bid,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            FadeIn = styles.FadeIn,
                            Text = button.Text,
                            FontSize = 12,
                            Color = button.Enabled ? styles.WhiteColor : Opacity(styles.WhiteColor, 0.6f),
                            Align = UnityEngine.TextAnchor.MiddleCenter
                        }
                    }
                });
                topoffset -= btnh + btngap;
            }
            topoffset = 0;
            foreach (var button in leftButtons)
            {
                if (!button.Visible) { continue; }
                var bid = ID(bottomid, "btn", true);
                container.Add(new CuiElement
                {
                    Parent = bottomid,
                    Name = bid,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            FadeIn = styles.FadeIn,
                            Color = button.Enabled ? button.Color : Opacity(button.Color, 0.6f),
                            Command = button.Enabled ? button.Command : String.Empty
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = TOP_LEFT,
                            AnchorMax = TOP_LEFT,
                            OffsetMin = Offset(styles.BottomPad, topoffset-btnh-styles.BottomPad),
                            OffsetMax = Offset(styles.BottomPad+btnw, topoffset-styles.BottomPad),
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = bid,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            FadeIn = styles.FadeIn,
                            Text = button.Text,
                            FontSize = 12,
                            Color = button.Enabled ? button.TextColor : Opacity(button.TextColor, 0.6f),
                            Align = UnityEngine.TextAnchor.MiddleCenter
                        }
                    }
                });
                topoffset -= btnh + btngap;
            }

            CloseProtectionStatusOverlay(basePlayer);
            CuiHelper.AddUi(basePlayer, container);
            PlayersViewingOverlay.Add(basePlayer.UserId());
        }

        private void CloseProtectionStatusOverlay(BasePlayer basePlayer)
        {
            CuiHelper.DestroyUi(basePlayer, PROTECTION_OVERLAY_ID);
            PlayersViewingOverlay.Remove(basePlayer.UserId());
        }

        private class ProtectedCupboardStatusInfo
        {
            public string Status;
            public string Reason;
            public string Protection;
            public string MaxProtectionTime;
            public string Balance;
            public string ProtectionTime;
            public string Costs;
            public string CostPerDamageTaken;
            public string Owners;
            public string ProtectionLevel;
        }
    }
}

/* Boosty - https://boosty.to/skulidropek 
Discord - https://discord.gg/k3hXsVua7Q 
Discord The Rust Bay - https://discord.gg/Zq3TVjxKWk  */