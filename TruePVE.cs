using Facepunch;
using Facepunch.Math;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Plugins.TruePVEExtensionMethods;
using Rust;
using Rust.Ai.Gen2;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("TruePVE", "nivex", "2.2.4")]
    [Description("Improvement of the default Rust PVE behavior")]
    // Thanks to the original author, ignignokt84.
    internal class TruePVE : RustPlugin
    {
        #region Variables
        private static TruePVE Instance;
        // config/data container
        private Configuration config = new();

        [PluginReference]
        Plugin ZoneManager, LiteZones, Clans, Friends, AbandonedBases, RaidableBases;

        public string usageString;
        private enum Command { def, sched, trace, usage, enable, sleepers };

        [Flags]
        public enum RuleFlags : ulong
        {
            None = 0,
            AdminsHurtSleepers = 1uL << 1,
            AnimalsIgnoreSleepers = 1uL << 2,
            AuthorizedDamage = 1uL << 3,
            AuthorizedDamageRequiresOwnership = 1uL << 4,
            CupboardOwnership = 1uL << 5,
            FriendlyFire = 1uL << 6,
            HeliDamageLocked = 1uL << 7,
            HumanNPCDamage = 1uL << 8,
            LockedBoxesImmortal = 1uL << 9,
            LockedDoorsImmortal = 1uL << 10,
            NoPlayerDamageToCar = 1uL << 11,
            NoPlayerDamageToMini = 1uL << 12,
            NoPlayerDamageToScrap = 1uL << 13,
            NoHeliDamage = 1uL << 14,
            NoHeliDamagePlayer = 1uL << 15,
            NoHeliDamageQuarry = 1uL << 16,
            NoHeliDamageRidableHorses = 1uL << 17,
            NoHeliDamageSleepers = 1uL << 18,
            NoMLRSDamage = 1uL << 19,
            NpcsCanHurtAnything = 1uL << 20,
            PlayerSamSitesIgnorePlayers = 1uL << 21,
            ProtectedSleepers = 1uL << 22,
            TrapsIgnorePlayers = 1uL << 23,
            TrapsIgnoreScientist = 1uL << 24,
            TurretsIgnorePlayers = 1uL << 25,
            TurretsIgnoreScientist = 1uL << 26,
            TwigDamage = 1uL << 27,
            TwigDamageRequiresOwnership = 1uL << 28,
            VehiclesTakeCollisionDamageWithoutDriver = 1uL << 29,
            SamSitesIgnoreMLRS = 1uL << 30,
            SelfDamage = 1uL << 31,
            StaticSamSitesIgnorePlayers = 1uL << 32,
            StaticTurretsIgnorePlayers = 1uL << 33,
            SafeZoneTurretsIgnorePlayers = 1uL << 34,
            SuicideBlocked = 1uL << 35,
            NoHeliDamageBuildings = 1uL << 36,
            WoodenDamage = 1uL << 37,
            WoodenDamageRequiresOwnership = 1uL << 38,
            AuthorizedDamageCheckPrivilege = 1uL << 39,
            ExcludeTugboatFromImmortalFlags = 1uL << 40,
        }

        public static RuleFlags GetRuleFlag(string value)
        {
            return Enum.TryParse(value, out RuleFlags flag) ? flag : RuleFlags.None;
        }

        private Timer scheduleUpdateTimer;                              // timer to check for schedule updates
        private bool shareRedirectEnabled;                              // undocumented. UAYOR.
        private RuleSet dudRuleSet;                                     // dud ruleset when no locations are shared
        private RuleSet currentRuleSet;                                 // current ruleset
        private string currentBroadcastMessage;                         // current broadcast message
        private bool useZones;                                          // internal useZones flag
        private const string Any = "any";                               // constant "any" string for rules
        private const string AllZones = "allzones";                     // constant "allzones" string for mappings
        private const string PermCanMap = "truepve.canmap";             // permission for mapping command
        private bool animalsIgnoreSleepers;                             // toggle flag to protect sleepers
        private bool trace = false;                                     // trace flag
        private const string traceFile = "ruletrace";                   // tracefile name
        private const float traceTimeout = 300f;                        // auto-disable trace after 300s (5m)
        private Timer traceTimer;                                       // trace timeout timer
        private bool tpveEnabled = true;                                // toggle flag for damage handling
        private List<DamageType> damageTypes = new()
        {
            DamageType.Arrow,
            DamageType.Blunt,
            DamageType.Bullet,
            DamageType.Explosion,
            DamageType.Cold,
            DamageType.Heat,
            DamageType.Generic,
            DamageType.Slash,
            DamageType.Stab,
        };
        #endregion

        #region Loading/Unloading

        private void Unload()
        {
            Instance = null;
            scheduleUpdateTimer?.Destroy();
            SaveData();
        }

        private void OnPluginLoaded(Plugin plugin)
        {
            if (plugin.Name == "ZoneManager")
                ZoneManager = plugin;
            if (plugin.Name == "LiteZones")
                LiteZones = plugin;
            if (ZoneManager != null || LiteZones != null)
                useZones = config == null || config.options.useZones;
        }

        private void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin.Name == "ZoneManager")
                ZoneManager = null;
            if (plugin.Name == "LiteZones")
                LiteZones = null;
            if (ZoneManager == null && LiteZones == null)
                useZones = false;
            traceTimer?.Destroy();
        }

        private void Init()
        {
            Unsubscribe(nameof(OnEntityMarkHostile));
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnEntityEnter));
            Unsubscribe(nameof(OnTurretTarget));
            Unsubscribe(nameof(OnEntityTakeDamage));
            Unsubscribe(nameof(OnPlayerConnected));
            Unsubscribe(nameof(OnSamSiteTarget));
            Unsubscribe(nameof(OnTrapTrigger));
            Unsubscribe(nameof(OnNpcTarget));
            Unsubscribe(nameof(OnMlrsFire));
            Instance = this;
            // register console commands automagically
            foreach (Command command in Enum.GetValues(typeof(Command)))
            {
                AddCovalenceCommand($"tpve.{command}", nameof(CommandDelegator));
            }
            // register chat commands
            AddCovalenceCommand("tpve_prod", nameof(CommandDelegator));
            AddCovalenceCommand("tpve_enable", nameof(CommandDelegator));
            AddCovalenceCommand("tpve", nameof(CommandDelegator));
            permission.RegisterPermission(PermCanMap, this);
            // build usage string for console (without sizing)
            usageString = WrapColor("orange", GetMessage("Header_Usage")) + $" - {Version}{Environment.NewLine}" +
                          WrapColor("cyan", $"tpve.{Command.def}") + $" - {GetMessage("Cmd_Usage_def")}{Environment.NewLine}" +
                          WrapColor("cyan", $"tpve.{Command.trace}") + $" - {GetMessage("Cmd_Usage_trace")}{Environment.NewLine}" +
                          WrapColor("cyan", $"tpve.{Command.sched} [enable|disable]") + $" - {GetMessage("Cmd_Usage_sched")}{Environment.NewLine}" +
                          WrapColor("cyan", $"/tpve_prod") + $" - {GetMessage("Cmd_Usage_prod")}{Environment.NewLine}" +
                          WrapColor("cyan", $"/tpve map") + $" - {GetMessage("Cmd_Usage_map")}";
            LoadData();
        }

        private void OnServerInitialized(bool isStartup)
        {
            // check for server pve setting
            if (ConVar.Server.pve) WarnPve();
            // load configuration
            config.Init();
            currentRuleSet = config.GetDefaultRuleSet();
            dudRuleSet = config.GetDudRuleSet();
            if (currentRuleSet == null)
                PrintWarning(GetMessage("Warning_NoRuleSet"), config.defaultRuleSet);
            useZones = config.options.useZones && (LiteZones != null || ZoneManager != null);
            if (useZones && config.mappings.Count == 1 && config.mappings.FirstOrDefault().Key.Equals(config.defaultRuleSet))
                useZones = false;
            if (config.schedule.enabled)
                TimerLoop(true);
            if (config.ruleSets.Exists(ruleSet => ruleSet.HasFlag(RuleFlags.AnimalsIgnoreSleepers))) Subscribe(nameof(OnNpcTarget));
            if (currentRuleSet == null) return;
            if (config.ruleSets.Exists(ruleSet => (ruleSet._flags & (RuleFlags.SafeZoneTurretsIgnorePlayers | RuleFlags.StaticTurretsIgnorePlayers | RuleFlags.TrapsIgnorePlayers | RuleFlags.TrapsIgnoreScientist | RuleFlags.TurretsIgnorePlayers | RuleFlags.TurretsIgnoreScientist)) != 0))
            {
                Subscribe(nameof(OnEntityEnter));
                Subscribe(nameof(OnTurretTarget));
            }
            if (config.ruleSets.Exists(ruleSet => (ruleSet._flags & (RuleFlags.SamSitesIgnoreMLRS | RuleFlags.PlayerSamSitesIgnorePlayers | RuleFlags.StaticSamSitesIgnorePlayers)) != 0))
            {
                Subscribe(nameof(OnSamSiteTarget));
            }
            if (config.ruleSets.Exists(ruleSet => (ruleSet._flags & (RuleFlags.TrapsIgnorePlayers | RuleFlags.TrapsIgnoreScientist)) != 0))
            {
                Subscribe(nameof(OnTrapTrigger));
            }
            if (config.schedule.enabled && config.schedule.broadcast && !string.IsNullOrEmpty(currentBroadcastMessage))
            {
                Subscribe(nameof(OnPlayerConnected));
            }
            if (config.options.disableBaseOvenSplash)
            {
                BaseNetworkable.serverEntities.OfType<BaseOven>().ForEach(oven => oven.disabledBySplash = false);
            }
            if (config.options.disableHostility)
            {
                Subscribe(nameof(OnEntityMarkHostile));
            }
            Subscribe(nameof(OnEntitySpawned));
            Subscribe(nameof(OnEntityTakeDamage));
            Subscribe(nameof(OnMlrsFire));
        }
        #endregion

        #region Data

        private class StoredData
        {
            public Dictionary<ulong, int> LastSeen = new();
            public string LastRunTime { get; set; } = DateTime.MinValue.ToString();
        }

        private StoredData data = new();

        private void LoadData()
        {
            try { data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name); } catch (Exception ex) { Puts(ex.ToString()); }
            data ??= new();
            data.LastSeen ??= new();
            if (data.LastRunTime != DateTime.MinValue.ToString() && DateTime.TryParse(data.LastRunTime, out var lastDate) && DateTime.Now.Subtract(lastDate).TotalHours >= 24)
            {
                data = new();
                data.LastRunTime = DateTime.Now.ToString();
                Puts("Last seen data wiped due to plugin not being loaded for {0} day(s).", DateTime.Now.Subtract(lastDate).Days);
            }
            if (config.AllowKillingSleepersHoursOffline <= 0f)
            {
                if (data.LastSeen.Count > 0)
                {
                    data.LastSeen.Clear();
                    SaveData();
                }
                return;
            }
            timer.Every(60f, UpdateLastSeen);
            UpdateLastSeen();
        }

        private void SaveData()
        {
            data.LastRunTime = DateTime.Now.ToString();
            Interface.Oxide.DataFileSystem.WriteObject(Name, data);
        }

        public void UpdateLastSeen()
        {
            bool changed = false;
            foreach (var sleeper in BasePlayer.sleepingPlayerList)
            {
                if (!sleeper || !sleeper.userID.IsSteamId())
                {
                    continue;
                }
                if (!data.LastSeen.ContainsKey(sleeper.userID))
                {
                    data.LastSeen[sleeper.userID] = Epoch.Current;
                    changed = true;
                }
            }
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (data.LastSeen.Remove(player.userID))
                {
                    changed = true;
                }
            }
            if (changed)
            {
                SaveData();
            }
        }

        public bool CanKillOfflinePlayer(BasePlayer player)
        {
            if (config.AllowKillingSleepersHoursOffline <= 0f)
            {
                return false;
            }
            if (player.IsConnected || !player.IsSleeping())
            {
                data.LastSeen.Remove(player.userID);
                return false;
            }
            if (!data.LastSeen.TryGetValue(player.userID, out var lastSeen))
            {
                return false;
            }
            return Epoch.Current - lastSeen > config.AllowKillingSleepersHoursOffline * 3600f;
        }

        #endregion Data

        #region Command Handling
        // delegation method for commands
        private void CommandDelegator(IPlayer user, string command, string[] args)
        {
            // return if user doesn't have access to run console command
            if (!user.IsServer && !(user.Object as BasePlayer).IsAdmin) return;

            if (args.Length > 0 && args[0] == "map" && user.HasPermission(PermCanMap))
            {
                CommandMap(user, command, args);
                return;
            }

            if (args.Contains("pvp"))
            {
                if (!currentRuleSet.rules.Remove("players cannot hurt players"))
                {
                    currentRuleSet.rules.Add("players cannot hurt players");
                }

                Puts("PVP toggled {0}", currentRuleSet.rules.Contains("players cannot hurt players") ? "on" : "off");
                SaveConfig();
                return;
            }

            if (command == "tpve_prod")
            {
                HandleProd(user);
                return;
            }

            if (command == "tpve_enable")
            {
                Message(user, "Enable", tpveEnabled = !tpveEnabled);
                return;
            }

            if (command == "tpve" && args.Length != 0) command = args[0];
            else command = command.Replace("tpve.", string.Empty);

            if (!Enum.TryParse(command, out TruePVE.Command @enum))
            {
                user.Reply($"Invalid argument: {command}");
                return;
            }

            switch (@enum)
            {
                case Command.sleepers:
                    HandleSleepers(user);
                    return;
                case Command.def:
                    HandleDef(user);
                    return;
                case Command.sched:
                    HandleScheduleSet(user, args);
                    return;
                case Command.trace:
                    if (!IsTraceEnabled(user))
                    {
                        return;
                    }
                    if (user.IsServer)
                    {
                        traceDistance = 0f;
                    }
                    else traceDistance = config.options.MaxTraceDistance;
                    trace = !trace;
                    if (!trace)
                    {
                        tracePlayer = null;
                        traceEntity = null;
                    }
                    else tracePlayer = user.Object as BasePlayer;
                    Message(user, "Notify_TraceToggle", new object[] { trace ? "on" : "off" });
                    if (trace)
                    {
                        traceTimer = timer.In(traceTimeout, () => trace = false);
                    }
                    else traceTimer?.Destroy();
                    return;
                case Command.enable:
                    Message(user, "Enable", tpveEnabled = !tpveEnabled);
                    return;
                case Command.usage:
                default:
                    ShowUsage(user);
                    return;
            }
        }

        private bool IsTraceEnabled(IPlayer user)
        {
            if (config.options.PlayerConsole || config.options.ServerConsole)
            {
                return true;
            }
            Message(user, "`Trace To Player Console` or `Trace To Server Console` must be enabled in the config!");
            return false;
        }

        private void HandleSleepers(IPlayer user)
        {
            if (animalsIgnoreSleepers)
            {
                animalsIgnoreSleepers = false;
                if (!config.ruleSets.Exists(ruleSet => ruleSet.HasFlag(RuleFlags.AnimalsIgnoreSleepers))) Unsubscribe(nameof(OnNpcTarget));
                user.Reply("Sleepers are no longer protected from animals.");
            }
            else
            {
                animalsIgnoreSleepers = true;
                Subscribe(nameof(OnNpcTarget));
                user.Reply("Sleepers are now protected from animals.");
            }
        }

        // handle setting defaults
        private void HandleDef(IPlayer user)
        {
            config.options = new();
            Message(user, "Notify_DefConfigLoad");
            LoadDefaultData();
            Message(user, "Notify_DefDataLoad");
            SaveConfig();
        }

        // handle prod command (raycast to determine what player is looking at)
        private void HandleProd(IPlayer user)
        {
            var player = user.Object as BasePlayer;
            if (!player || !player.IsAdmin)
            {
                Message(user, "Error_NoPermission");
                return;
            }

            if (!GetRaycastTarget(player, out var entity))
            {
                SendReply(player, WrapSize(12, WrapColor("red", GetMessage("Error_NoEntityFound", player.UserIDString))));
                return;
            }

            Message(player, "Notify_ProdResult", new object[] { entity.GetType(), entity.ShortPrefabName });
        }

        private void CommandMap(IPlayer user, string command, string[] args)
        {
            // assume args[0] is the command (beyond /tpve)
            if (args.Length > 0) command = args[0];

            // shift arguments
            args = args.Length > 1 ? args.Skip(1) : new string[0];

            if (command != "map")
            {
                Message(user, "Error_InvalidCommand");
            }
            else if (args.Length == 0)
            {
                Message(user, "Error_InvalidParamForCmd", command);
            }
            else
            {
                string from = args[0]; // mapping name
                string to = args.Length == 2 ? args[1] : null; // target ruleSet/exclude, otherwise delete mapping
                if (to != null)
                {
                    if (to != "exclude" && !config.ruleSets.Exists(r => r.name == to))
                    {
                        // target ruleset must exist, or be "exclude"
                        Message(user, "Error_InvalidMapping", from, to);
                        return;
                    }
                    if (config.HasMapping(from))
                    {
                        string old = config.mappings[from];
                        Message(user, "Notify_MappingUpdated", from, old, to); // update existing mapping
                    }
                    else Message(user, "Notify_MappingCreated", from, to); // add new mapping
                    config.mappings[from] = to;
                    SaveConfig();
                }
                else
                {
                    if (config.HasMapping(from))
                    {
                        Message(user, "Notify_MappingDeleted", from, config.mappings[from]);
                        config.mappings.Remove(from); // remove mapping
                        SaveConfig();
                    }
                    else Message(user, "Error_NoMappingToDelete", from);
                }
            }
        }

        // handles schedule enable/disable
        private void HandleScheduleSet(IPlayer user, string[] args)
        {
            if (args.Length == 0)
            {
                Message(user, "Error_InvalidParamForCmd");
                return;
            }
            if (!config.schedule.valid)
            {
                Message(user, "Notify_InvalidSchedule");
            }
            else if (args[0] == "enable")
            {
                if (config.schedule.enabled) return;
                config.schedule.enabled = true;
                TimerLoop();
                Message(user, "Notify_SchedSetEnabled");
            }
            else if (args[0] == "disable")
            {
                if (!config.schedule.enabled) return;
                config.schedule.enabled = false;
                if (scheduleUpdateTimer != null)
                    scheduleUpdateTimer.Destroy();
                Message(user, "Notify_SchedSetDisabled");
            }
            else
            {
                Message(user, "Error_InvalidParameter", args[0]);
            }
        }
        #endregion

        #region Configuration/Data

        // load config
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
                CheckData();
                SaveConfig();
            }
            catch (Exception ex)
            {
                canSaveConfig = false;
                Puts("{0}", ex);
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration
            {
                configVersion = Version.ToString(),
                options = new ConfigurationOptions()
            };
            LoadDefaultData();
        }

        private bool canSaveConfig = true;

        // save data
        protected override void SaveConfig()
        {
            if (canSaveConfig)
            {
                Config.WriteObject(config);
            }
        }

        // check rulesets and groups
        private void CheckData()
        {
            if (config.ruleSets.IsNullOrEmpty() || config.groups.IsNullOrEmpty())
            {
                LoadDefaultData();
            }
            if (config.schedule == null)
            {
                config.schedule = new Schedule();
            }
            // check config version, update version to current version
            if (config.configVersion != Version.ToString())
            {
                config.configVersion = Version.ToString();
            }
            CheckMappings();
        }

        // rebuild mappings
        private bool CheckMappings()
        {
            bool dirty = false;
            foreach (RuleSet rs in config.ruleSets)
            {
                if (!config.mappings.ContainsValue(rs.name))
                {
                    config.mappings[rs.name] = rs.name;
                    dirty = true;
                }
            }
            return dirty;
        }

        // load default data to mappings, rulesets, and groups

        private bool LoadDefaultData()
        {
            config.mappings.Clear();
            config.ruleSets.Clear();
            config.groups.Clear();
            config.schedule = new();
            config.defaultRuleSet = "default";

            // build groups first
            config.groups.Add(new("barricades")
            {
                members = "door_barricade_a, door_barricade_a_large, door_barricade_b, door_barricade_dbl_a, door_barricade_dbl_a_large, door_barricade_dbl_b, door_barricade_dbl_b_large, gingerbread_barricades_house, gingerbread_barricades_snowman, gingerbread_barricades_tree, wooden_crate_gingerbread, icewall, GraveYardFence",
                exclusions = "barricade.concrete, barricade.sandbags, barricade.stone"
            });

            config.groups.Add(new("barricades2")
            {
                members = "spikes_static, barricade.metal, barricade.wood, barricade.woodwire, spikes.floor",
            });

            config.groups.Add(new("dispensers")
            {
                members = "BaseCorpse, HelicopterDebris, PlayerCorpse, NPCPlayerCorpse, HorseCorpse, SkyLantern, Pinata"
            });

            config.groups.Add(new("fire")
            {
                members = "FireBall, FlameExplosive, FlameThrower, BaseOven, FlameTurret, rocket_heli_napalm, napalm, oilfireball2"
            });

            config.groups.Add(new("guards")
            {
                members = "bandit_guard, scientistpeacekeeper, sentry.scientist.static"
            });

            config.groups.Add(new("heli")
            {
                members = "PatrolHelicopter"
            });

            config.groups.Add(new("highwalls")
            {
                members = "SimpleBuildingBlock, wall.external.high.ice, gates.external.high.stone, gates.external.high.wood"
            });

            config.groups.Add(new("ridablehorses")
            {
                members = "RidableHorse"
            });

            config.groups.Add(new("cars")
            {
                members = "BasicCar, ModularCar, BaseModularVehicle, BaseVehicleModule, VehicleModuleEngine, VehicleModuleSeating, VehicleModuleStorage, VehicleModuleTaxi, ModularCarSeat, Bike"
            });

            config.groups.Add(new("mini")
            {
                members = "minicopter.entity"
            });

            config.groups.Add(new("scrapheli")
            {
                members = "ScrapTransportHelicopter"
            });

            config.groups.Add(new("ch47")
            {
                members = "ch47.entity"
            });

            config.groups.Add(new("npcs")
            {
                members = "ch47scientists.entity, BradleyAPC, CustomScientistNpc, ScarecrowNPC, HumanNPC, NPCPlayer, ScientistNPC, TunnelDweller, SimpleShark, UnderwaterDweller, ZombieNPC"
            });

            config.groups.Add(new("players")
            {
                members = "BasePlayer, FrankensteinPet"
            });

            config.groups.Add(new("resources")
            {
                members = "ResourceEntity, TreeEntity, OreResourceEntity, LootContainer",
                exclusions = "hobobarrel.deployed"
            });

            config.groups.Add(new("snowmobiles")
            {
                members = "snowmobile, tomahasnowmobile"
            });

            config.groups.Add(new("traps")
            {
                members = "AutoTurret, BearTrap, FlameTurret, Landmine, GunTrap, ReactiveTarget, TeslaCoil, spikes.floor"
            });

            config.groups.Add(new("junkyard")
            {
                members = "magnetcrane.entity, carshredder.entity"
            });

            config.groups.Add(new("tugboats")
            {
                members = "Tugboat"
            });

            config.groups.Add(new("heliturrets")
            {
                members = "turret_attackheli"
            });

            // create default ruleset
            RuleSet defaultRuleSet = new(config.defaultRuleSet)
            {
                _flags = RuleFlags.HumanNPCDamage | RuleFlags.LockedBoxesImmortal | RuleFlags.LockedDoorsImmortal | RuleFlags.PlayerSamSitesIgnorePlayers | RuleFlags.TrapsIgnorePlayers | RuleFlags.TurretsIgnorePlayers,
                flags = "HumanNPCDamage, LockedBoxesImmortal, LockedDoorsImmortal, PlayerSamSitesIgnorePlayers, TrapsIgnorePlayers, TurretsIgnorePlayers"
            };

            // create rules and add to ruleset
            defaultRuleSet.AddRule("anything can hurt dispensers");
            defaultRuleSet.AddRule("anything can hurt resources");
            defaultRuleSet.AddRule("anything can hurt barricades");
            defaultRuleSet.AddRule("anything can hurt traps");
            defaultRuleSet.AddRule("anything can hurt heli");
            defaultRuleSet.AddRule("anything can hurt npcs");
            defaultRuleSet.AddRule("anything can hurt players");
            defaultRuleSet.AddRule("nothing can hurt ch47");
            defaultRuleSet.AddRule("nothing can hurt cars");
            defaultRuleSet.AddRule("nothing can hurt mini");
            defaultRuleSet.AddRule("nothing can hurt snowmobiles");
            //defaultRuleSet.AddRule("nothing can hurt guards");
            defaultRuleSet.AddRule("nothing can hurt ridablehorses");
            defaultRuleSet.AddRule("cars cannot hurt anything");
            defaultRuleSet.AddRule("mini cannot hurt anything");
            defaultRuleSet.AddRule("ch47 cannot hurt anything");
            defaultRuleSet.AddRule("scrapheli cannot hurt anything");
            defaultRuleSet.AddRule("players cannot hurt players");
            defaultRuleSet.AddRule("players cannot hurt traps");
            defaultRuleSet.AddRule("guards cannot hurt players");
            defaultRuleSet.AddRule("fire cannot hurt players");
            defaultRuleSet.AddRule("traps cannot hurt players");
            defaultRuleSet.AddRule("highwalls cannot hurt players");
            defaultRuleSet.AddRule("barricades cannot hurt players");
            defaultRuleSet.AddRule("barricades2 cannot hurt players");
            defaultRuleSet.AddRule("mini cannot hurt mini");
            defaultRuleSet.AddRule("npcs can hurt players");
            defaultRuleSet.AddRule("junkyard cannot hurt anything");
            defaultRuleSet.AddRule("junkyard can hurt cars");
            defaultRuleSet.AddRule("players cannot hurt tugboats");
            defaultRuleSet.AddRule("heliturrets cannot hurt players");

            config.ruleSets.Add(defaultRuleSet); // add ruleset to rulesets list

            config.mappings[config.defaultRuleSet] = config.defaultRuleSet; // create mapping for ruleset

            return true;
        }

        private bool ResetRules(string key)
        {
            if (string.IsNullOrEmpty(key) || config == null)
            {
                return false;
            }

            string old = config.defaultRuleSet;

            config.defaultRuleSet = key;
            currentRuleSet = config.GetDefaultRuleSet();

            if (currentRuleSet == null)
            {
                config.defaultRuleSet = old;
                currentRuleSet = config.GetDefaultRuleSet();
                return false;
            }

            return true;
        }
        #endregion

        #region Trace
        private StringBuilder _tsb = new();
        private BaseEntity traceEntity;
        private BasePlayer tracePlayer;
        private float traceDistance;

        private void Trace(string message, int indentation = 0)
        {
            if (!traceEntity || traceEntity.IsDestroyed)
            {
                return;
            }

            bool playerInRange = tracePlayer != null && !tracePlayer.IsDestroyed && tracePlayer.Distance(traceEntity) <= traceDistance;

            if ((config.options.PlayerConsole && playerInRange) || (config.options.ServerConsole && (traceDistance == 0 || playerInRange)))
            {
                _tsb.AppendLine(string.Empty.PadLeft(indentation, ' ') + message);
            }
        }

        private void LogTrace()
        {
            var text = _tsb.ToString();
            traceEntity = null;
            _tsb.Length = 0;
            try
            {
                if (!string.IsNullOrEmpty(text))
                {
                    if (config.options.ServerConsole)
                    {
                        Puts(text);
                    }
                    if (config.options.PlayerConsole && tracePlayer.IsOnline())
                    {
                        tracePlayer.ConsoleMessage(text);
                    }
                    if (config.options.LogToFile)
                    {
                        LogToFile(traceFile, text, this);
                    }
                }
            }
            catch (IOException)
            {
                timer.Once(1f, () => LogToFile(traceFile, text, this));
            }
        }

        #endregion Trace

        #region Hooks/Handler Procedures
        private void OnPlayerConnected(BasePlayer player)
        {
            if (config.schedule.broadcast && !string.IsNullOrEmpty(currentBroadcastMessage))
            {
                SendReply(player, GetMessage("Prefix") + currentBroadcastMessage);
            }
        }

        private string CurrentRuleSetName() => currentRuleSet?.name;

        private bool IsEnabled() => tpveEnabled;

        private class PlayerExclusion : Pool.IPooled
        {
            public Plugin plugin;
            public float time;
            public bool IsExpired => Time.time > time;
            public void EnterPool()
            {
                plugin = null;
                time = 0f;
            }
            public void LeavePool()
            {
                plugin = null;
                time = 0f;
            }
        }

        private Dictionary<ulong, List<PlayerExclusion>> playerDelayExclusions = new();

        private void ExcludePlayer(ulong userid, float maxDelayLength, Plugin plugin)
        {
            if (plugin == null)
            {
                return;
            }
            if (!playerDelayExclusions.TryGetValue(userid, out var exclusions))
            {
                playerDelayExclusions[userid] = exclusions = Pool.Get<List<PlayerExclusion>>();
            }
            var exclusion = exclusions.Find(x => x.plugin == plugin);
            if (maxDelayLength <= 0f)
            {
                if (exclusion != null)
                {
                    exclusions.Remove(exclusion);
                    exclusion.plugin = null;
                    exclusion.time = 0f;
                    Pool.Free(ref exclusion);
                }
                if (exclusions.Count == 0)
                {
                    playerDelayExclusions.Remove(userid);
                    Pool.FreeUnmanaged(ref exclusions);
                }
            }
            else
            {
                if (exclusion == null)
                {
                    exclusion = Pool.Get<PlayerExclusion>();
                    exclusion.plugin = plugin;
                    exclusions.Add(exclusion);
                }
                exclusion.time = Time.time + maxDelayLength;
            }
        }

        private bool HasDelayExclusion(ulong userid)
        {
            if (playerDelayExclusions.TryGetValue(userid, out var exclusions))
            {
                for (int i = 0; i < exclusions.Count; i++)
                {
                    var exclusion = exclusions[i];
                    if (!exclusion.IsExpired)
                    {
                        return true;
                    }
                    exclusions.RemoveAt(i);
                    exclusion.plugin = null;
                    exclusion.time = 0f;
                    Pool.Free(ref exclusion);
                    i--;
                }
                if (exclusions.Count == 0)
                {
                    playerDelayExclusions.Remove(userid);
                    Pool.Free(ref exclusions);
                }
            }
            return false;
        }

        // handle damage - if another mod must override TruePVE damages or take priority,
        // set handleDamage to false and reference HandleDamage from the other mod(s)
        private object OnEntityTakeDamage(ResourceEntity entity, HitInfo hitInfo)
        {
            // if default global is not enabled, return true (allow all damage)
            if (!IsEnabled() || hitInfo == null || currentRuleSet == null || currentRuleSet.IsEmpty() || !currentRuleSet.enabled)
            {
                return null;
            }

            // get entity and initiator locations (zones)
            List<string> entityLocations = GetLocationKeys(entity);
            List<string> initiatorLocations = GetLocationKeys(hitInfo.Initiator);
            // check for exclusion zones (zones with no rules mapped)
            if (CheckExclusion(entityLocations, initiatorLocations, trace))
            {
                if (trace) Trace("Exclusion found; allow and return", 1);
                return null;
            }

            if (trace) Trace("No exclusion found - looking up RuleSet...", 1);
            // process location rules
            RuleSet ruleSet = GetRuleSet(entityLocations, initiatorLocations);

            return EvaluateRules(entity, hitInfo, ruleSet) ? (object)null : true;
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity.IsNull() || entity.IsDestroyed || hitInfo == null || AllowKillingSleepers(entity, hitInfo))
            {
                return null;
            }

            if (Interface.CallHook("CanEntityTakeDamage", new object[] { entity, hitInfo }) is bool val)
            {
                if (val)
                {
                    return null;
                }

                CancelHit(hitInfo);
                return true;
            }

            if (!IsEnabled() || !config.options.handleDamage)
            {
                return null;
            }

            var damageAmount = hitInfo.damageTypes.Total();

            if (damageAmount <= 0f)
            {
                return null;
            }

            var victim = entity as BasePlayer;

            if (config.scrap)
            {
                if (hitInfo.Initiator is ScrapTransportHelicopter && victim)
                {
                    hitInfo.damageTypes.Clear();
                    return null;
                }

                if (hitInfo.Initiator is BasePlayer driver && (hitInfo.WeaponPrefab is ScrapTransportHelicopter || driver.GetMountedVehicle() is ScrapTransportHelicopter))
                {
                    hitInfo.damageTypes.Clear();
                    return null;
                }
            }

            var damageType = hitInfo.damageTypes.GetMajorityDamageType();

            if (damageType == DamageType.Decay || damageType == DamageType.Fall || damageType == DamageType.Radiation)
            {
                return null;
            }

            if (config.igniter && entity is Igniter && entity.OwnerID != 0)
            {
                hitInfo.damageTypes.Clear();
                return null;
            }

            var weapon = hitInfo.Initiator ?? hitInfo.WeaponPrefab ?? hitInfo.Weapon;

            if ((damageType == DamageType.Cold || damageType == DamageType.Heat) && IsMetabolismDamage(victim, damageType, damageAmount))
            {
                if (trace) Trace($"Initiator is {damageType} metabolism damage; {((damageType == DamageType.Cold ? config.options.Cold : config.options.Heat) ? "allow and return" : "block and return")}", 1);
                if ((damageType == DamageType.Cold && !config.options.Cold) || (damageType == DamageType.Heat && !config.options.Heat)) hitInfo.damageTypes.Clear();
                if (trace) LogTrace();
                return null;
            }

            if (!AllowDamage(weapon, victim, entity, hitInfo, damageType, damageAmount))
            {
                if (trace) LogTrace();
                CancelHit(hitInfo);
                return true;
            }

            if (trace) LogTrace();
            return null;
        }

        private bool IsTrap(BaseEntity entity) => entity is BaseTrap || entity is HBHFSensor || entity is GunTrap;

        private bool IsTurret(BaseEntity entity) => entity is FlameTurret || entity is AutoTurret;

        private bool ContainsTopology(TerrainTopology.Enum mask, Vector3 position, float radius) => (TerrainMeta.TopologyMap.GetTopology(position, radius) & (int)mask) != 0;

        private bool CanPlayerTriggerTurretOrTrap(BasePlayer victim, BaseEntity entity, BaseEntity weapon)
        {
            if (weapon != null && weapon.OwnerID == 0uL && victim != null && victim.userID.IsSteamId() && (config.PlayersTriggerTraps && IsTrap(weapon) || config.PlayersTriggerTurrets && IsTurret(weapon)))
            {
                return ContainsTopology(TerrainTopology.Enum.Monument, weapon.transform.position, 5f);
            }
            return false;
        }

        private bool CanPlayerHurtTurretOrTrap(BasePlayer victim, BaseEntity entity, BaseEntity weapon)
        {
            if (entity.OwnerID == 0uL && weapon is BasePlayer attacker && attacker.userID.IsSteamId() && (config.PlayersHurtTraps && IsTrap(entity) || config.PlayersHurtTurrets && IsTurret(entity)))
            {
                return ContainsTopology(TerrainTopology.Enum.Monument, weapon.transform.position, 5f);
            }
            return false;
        }

        private void CancelHit(HitInfo hitInfo)
        {
            hitInfo.damageTypes = new();
            hitInfo.DidHit = false;
            hitInfo.DoHitEffects = false;
        }

        private bool AllowKillingSleepers(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity.ShortPrefabName == "player" && (config.AllowKillingSleepersAlly || config.AllowKillingSleepers || config.AllowKillingSleepersAuthorization))
            {
                var victim = entity.ToPlayer();
                if (!victim.IsSleeping())
                {
                    return false;
                }
                if (config.AllowKillingSleepersAuthorization && hitInfo.Initiator is BasePlayer attacker && AllowAuthorizationDamage(victim, attacker))
                {
                    return true;
                }
                if (config.AllowKillingSleepersAlly && hitInfo.Initiator is BasePlayer attacker2)
                {
                    return IsAlly(victim.userID, attacker2.userID);
                }
                return config.AllowKillingSleepers;
            }
            return false;
        }

        private bool IsAuthed(DecayEntity entity, BasePlayer attacker)
        {
            if (entity is LegacyShelter || entity is LegacyShelterDoor)
            {
                return entity.GetEntityBuildingPrivilege() is EntityPrivilege entityPriv && entityPriv.AnyAuthed() && entityPriv.IsAuthed(attacker);
            }
            return entity.GetBuilding() is BuildingManager.Building building && building.GetDominatingBuildingPrivilege() is BuildingPrivlidge priv && priv.AnyAuthed() && priv.IsAuthed(attacker);
        }

        public bool IsAuthed(Tugboat tugboat, BasePlayer attacker)
        {
            return !tugboat.children.IsNullOrEmpty() && tugboat.children.Exists(child => child is VehiclePrivilege vehiclePrivilege && vehiclePrivilege.AnyAuthed() && vehiclePrivilege.IsAuthed(attacker));
        }

        public bool IsAuthed(BaseHelicopter heli, BasePlayer attacker)
        {
            return attacker.GetBuildingPrivilege(heli.WorldSpaceBounds()) is BuildingPrivlidge priv && priv.AnyAuthed() && priv.IsAuthed(attacker);
        }

        private bool AllowAuthorizationDamage(BasePlayer victim, BasePlayer attacker)
        {
            if (!attacker.userID.IsSteamId())
            {
                return false;
            }
            if (victim.GetParentEntity() is Tugboat tugboat && tugboat.IsAuthed(attacker))
            {
                return true;
            }
            if (victim.GetBuildingPrivilege() is BuildingPrivlidge priv && priv.IsAuthed(attacker))
            {
                return true;
            }
            return false;
        }

        // determines if an entity is "allowed" to take damage
        private bool AllowDamage(BaseEntity weapon, BasePlayer victim, BaseCombatEntity entity, HitInfo hitInfo, DamageType damageType, float damageAmount)
        {
            if (trace)
            {
                traceEntity = entity;
                _tsb.Length = 0;
            }

            // if default global is not enabled or entity is npc, allow all damage
            if (currentRuleSet == null || currentRuleSet.IsEmpty() || !currentRuleSet.enabled)
            {
                return true;
            }

            if (entity is BaseNpc || entity is BaseNPC2)
            {
                if (trace) Trace("Target is animal; allow and return", 1);
                return true;
            }

            // allow damage to door barricades and covers 
            if (entity.PrefabName.Contains("trainbarricade") || entity is Barricade && (entity.ShortPrefabName.Contains("door_barricade") || entity.ShortPrefabName.Contains("cover")))
            {
                if (trace) Trace($"Target is {entity.ShortPrefabName}; allow and return", 1);
                return true;
            }

            // if entity is a barrel, trash can, or giftbox, allow damage (exclude water and hobo barrels)
            if (!entity.ShortPrefabName.Equals("waterbarrel") && entity.prefabID != 1748062128 && ((entity.ShortPrefabName.Contains("barrel") && entity is LootContainer) || entity.ShortPrefabName.Equals("loot_trash") || entity.ShortPrefabName.Equals("giftbox_loot")))
            {
                if (trace) Trace("Target is barrel; allow and return", 1);
                return true;
            }

            TrySetInitiator(hitInfo);

            if (trace)
            {
                // Sometimes the initiator is not the attacker (turrets)
                Trace("======================" + Environment.NewLine +
                  "==  STARTING TRACE  ==" + Environment.NewLine +
                  "==  " + DateTime.Now.ToString("HH:mm:ss.fffff") + "  ==" + Environment.NewLine +
                  "======================");
                Trace($"From: {weapon?.GetType().Name ?? "Unknown_Weapon"}, {weapon?.ShortPrefabName ?? "Unknown_Prefab"}", 1);
                Trace($"To: {entity.GetType().Name}, {entity.ShortPrefabName}", 1);
            }

            // get entity and initiator locations (zones)
            List<string> entityLocations = GetLocationKeys(entity);
            List<string> initiatorLocations = GetLocationKeys(weapon);
            // check for exclusion zones (zones with no rules mapped)
            if (CheckExclusion(entityLocations, initiatorLocations, trace))
            {
                if (trace) Trace("Exclusion found; allow and return", 1);
                return true;
            }

            if ((config.PlayersHurtTraps || config.PlayersHurtTurrets) && CanPlayerHurtTurretOrTrap(victim, entity, weapon))
            {
                if (trace) Trace($"Initiator is player; Target is turret or trap in monument topology; allow and return", 1);
                return true;
            }

            if ((config.PlayersTriggerTraps || config.PlayersTriggerTurrets) && CanPlayerTriggerTurretOrTrap(victim, entity, weapon))
            {
                if (trace) Trace($"Initiator is turret or trap in monument topology; Target is player; allow and return", 1);
                return true;
            }

            if (trace) Trace("No exclusion found - looking up RuleSet...", 1);

            // process location rules
            RuleSet ruleSet = GetRuleSet(entityLocations, initiatorLocations);

            if (trace) Trace($"Using RuleSet \"{ruleSet.name}\"", 1);

            var attacker = hitInfo.Initiator as BasePlayer;
            var isAttacker = !attacker.IsKilled();
            var isVictim = !victim.IsKilled();

            if (isVictim)
            {
                if (isAttacker)
                {
                    if (CanKillOfflinePlayer(victim))
                    {
                        if (trace) Trace($"Initiator ({attacker}) and target ({victim} exceeds Allow Killing Sleepers offline time); allow and return", 1);
                        return true;
                    }

                    bool atkCondition = HasDelayExclusion(attacker.userID) ||
                                        (config.options.Aboveworld < 5000f && attacker.transform.position.y >= config.options.Aboveworld) ||
                                        (config.options.Underworld > -500f && attacker.transform.position.y <= config.options.Underworld) ||
                                        (!initiatorLocations.IsNullOrEmpty() && initiatorLocations.Exists(loc => config.mappings.TryGetValue(loc, out var mapping) && mapping.Equals("exclude")));

                    if (atkCondition)
                    {
                        bool vicCondition = HasDelayExclusion(victim.userID) ||
                                            (config.options.Aboveworld < 5000f && victim.transform.position.y >= config.options.Aboveworld) ||
                                            (config.options.Underworld > -500f && victim.transform.position.y <= config.options.Underworld) ||
                                            (!entityLocations.IsNullOrEmpty() && entityLocations.Exists(loc => config.mappings.TryGetValue(loc, out var mapping) && mapping.Equals("exclude")));

                        if (vicCondition)
                        {
                            if (trace) Trace($"Initiator ({attacker}) and target ({victim}) meet exclusion conditions; allow and return", 1);
                            return true;
                        }
                    }
                }

                if (config.options.UnderworldOther > -500f && (!isAttacker || !attacker.userID.IsSteamId()) && victim.transform.position.y <= config.options.UnderworldOther)
                {
                    if (trace) Trace($"Initiator is {weapon} under world; Target is player; allow and return", 1);
                    return true;
                }

                if (config.options.AboveworldOther < 5000f && (!isAttacker || !attacker.userID.IsSteamId()) && victim.transform.position.y >= config.options.AboveworldOther)
                {
                    if (trace) Trace($"Initiator is {weapon} above world; Target is player; allow and return", 1);
                    return true;
                }
            }

            if (entity is PatrolHelicopter)
            {
                if (weapon is BasePlayer || weapon is PatrolHelicopter)
                {
                    bool isBlocked = !EvaluateRules(entity, weapon, ruleSet, false);
                    if (trace) Trace($"Target is PatrolHelicopter; Initiator is player; {(isBlocked ? "block and return" : "allow and return")}", 1);
                    return !isBlocked;
                }
                if (trace) Trace($"Target is PatrolHelicopter; Initiator is {weapon?.GetType()?.Name}; allow and return", 1);
                return true;
            }

            if (weapon?.ShortPrefabName == "maincannonshell" || weapon is BradleyAPC)
            {
                if (trace) Trace("Initiator is BradleyAPC; evaluating RuleSet rules...", 1);
                return EvaluateRules(entity, weapon, ruleSet);
            }

            if (ruleSet.HasFlag(RuleFlags.VehiclesTakeCollisionDamageWithoutDriver) && entity is BaseVehicle vehicle && weapon == vehicle && vehicle.GetDriver() == null)
            {
                if (trace) Trace($"Vehicle collision: No driver; allow and return", 1);
                return true;
            }

            // check heli and turret
            var heli = CheckHeliInitiator(ruleSet, hitInfo);

            if (config.Firework && entity is BaseFirework)
            {
                if (trace) Trace($"Target is firework; {(heli is not bool ? "allow and return" : "block and return")}", 1);
                return heli is not bool;
            }

            if (heli is bool val1)
            {
                if (CheckImmortalFlag(entity, hitInfo, ruleSet) is bool val2)
                {
                    return val2;
                }
                return HandleHelicopter(ruleSet, entity, weapon, val1);
            }

            if (hitInfo.WeaponPrefab is MLRSRocket && ruleSet.HasFlag(RuleFlags.NoMLRSDamage))
            {
                if (trace) Trace("Initiator is MLRS rocket with NoMLRSDamage set; block and return", 1);
                return false;
            }

            // after heli check, return true if initiator is null
            if (hitInfo.Initiator.IsRealNull())
            {
                if ((damageType == DamageType.Slash || damageType == DamageType.Stab || damageType == DamageType.Cold) && (entity.lastAttacker is not BasePlayer lastAttacker || !lastAttacker.userID.IsSteamId() || lastAttacker == entity))
                {
                    if (trace) Trace("Initiator is hurt trigger; allow and return", 1);
                    return true;
                }
                if (damageTypes.Exists(x => hitInfo.damageTypes.Get(x) > 0f))
                {
                    bool tut = IsTutorialNetworkGroup(entity);
                    if (trace) Trace($"Initiator empty for player damage; {(tut ? "allow and return (Tutorial Zone)" : "block and return")} (Damage Type: {damageType}, Damage Amount: {damageAmount})", 1);
                    return tut;
                }
                if (weapon is MLRSRocket)
                {
                    if (trace) Trace($"Initiator empty for MLRS Rocket; block and return", 1);
                    return false;
                }
                if (trace) Trace($"Initiator empty; allow and return {damageType} {damageAmount}", 1);
                return true;
            }

            if (CheckImmortalFlag(entity, hitInfo, ruleSet) is bool val3)
            {
                return val3;
            }

            if (hitInfo.Initiator is SamSite ss && (entity is BasePlayer || entity is BaseMountable))
            {
                if (CheckExclusion(ss))
                {
                    if (trace) Trace($"Initiator is samsite, and target is player; exclusion found; allow and return", 1);
                    return true;
                }

                bool isAllowed = ss.staticRespawn ? !ruleSet.HasFlag(RuleFlags.StaticSamSitesIgnorePlayers) : !ruleSet.HasFlag(RuleFlags.PlayerSamSitesIgnorePlayers);
                if (trace) Trace($"Initiator is samsite, and target is player; {(isAllowed ? "flag not set; allow and return" : "flag set; block and return")}", 1);
                return isAllowed;
            }

            if (isAttacker && !attacker.userID.IsSteamId() || hitInfo.Initiator is BaseNpc || hitInfo.Initiator is BaseNPC2)
            {
                if (isVictim && ruleSet.HasFlag(RuleFlags.ProtectedSleepers) && entity.ToPlayer().IsSleeping())
                {
                    if (trace) Trace("Target is sleeping player, with ProtectedSleepers flag set; block and return", 1);
                    return false;
                }

                if (ruleSet.HasFlag(RuleFlags.NpcsCanHurtAnything))
                {
                    if (trace) Trace("Initiator is NPC; flag set; allow damage and return", 1);
                    return true;
                }
            }

            bool selfDamageFlag = ruleSet.HasFlag(RuleFlags.SelfDamage);

            if (isVictim)
            {
                if (hitInfo.Initiator is AutoTurret && hitInfo.Initiator.OwnerID == 0 && victim.userID.IsSteamId())
                {
                    if (hitInfo.Initiator is NPCAutoTurret)
                    {
                        if (trace) Trace($"Initiator is npc turret; Target is player; {(!ruleSet.HasFlag(RuleFlags.SafeZoneTurretsIgnorePlayers) ? "allow and return" : "block and return")}", 1);
                        return !ruleSet.HasFlag(RuleFlags.SafeZoneTurretsIgnorePlayers);
                    }
                    if (trace) Trace($"Initiator is static turret; Target is player; {(!ruleSet.HasFlag(RuleFlags.StaticTurretsIgnorePlayers) ? "allow and return" : "block and return")}", 1);
                    return !ruleSet.HasFlag(RuleFlags.StaticTurretsIgnorePlayers);
                }

                if (entity == attacker && damageType == DamageType.Bullet && damageAmount < 0f)
                {
                    if (trace) Trace($"Negative damage; allow and return", 1);
                    return true;
                }

                // handle suicide
                if (damageType == DamageType.Suicide && victim.userID.IsSteamId())
                {
                    bool isBlocked = ruleSet.HasFlag(RuleFlags.SuicideBlocked);
                    if (trace) Trace($"DamageType is suicide; blocked? {(isBlocked ? "true; block and return" : "false; allow and return")}", 1);
                    if (isBlocked) Message(victim, "Error_NoSuicide");
                    return !isBlocked;
                }

                // allow players to hurt themselves
                if (selfDamageFlag && victim.userID.IsSteamId() && hitInfo.Initiator == entity)
                {
                    if (trace) Trace($"SelfDamage flag; player inflicted damage to self; allow and return", 1);
                    return true;
                }
            }

            if (isAttacker)
            {
                if (attacker.GetMounted() is BaseMountable mounted && !EvaluateRules(entity, mounted, ruleSet, false))
                {
                    if (trace) Trace($"Player is mounted; evaluation? block and return", 1);
                    return false;
                }

                if (entity is BuildingBlock block)
                {
                    if (hitInfo.Initiator is Minicopter)
                    {
                        if (trace) Trace("Initiator is minicopter, target is building; evaluate and return", 1);
                        return EvaluateRules(entity, hitInfo, ruleSet);
                    }

                    if (ruleSet.HasFlag(RuleFlags.TwigDamage) && block.grade == BuildingGrade.Enum.Twigs)
                    {
                        bool isAllowed = !ruleSet.HasFlag(RuleFlags.TwigDamageRequiresOwnership) || IsAlly(entity.OwnerID, attacker.userID) || IsAuthed(block, attacker);
                        if (trace) Trace($"Initiator is player and target is twig block, with TwigDamage flag set; {(isAllowed ? "allow" : "block")} and return", 1);
                        TwigOutputHandler(entity, damageType, damageAmount, attacker, block, selfDamageFlag);
                        return isAllowed;
                    }

                    if (ruleSet.HasFlag(RuleFlags.WoodenDamage) && block.grade == BuildingGrade.Enum.Wood)
                    {
                        bool isAllowed = !ruleSet.HasFlag(RuleFlags.WoodenDamageRequiresOwnership) || IsAlly(entity.OwnerID, attacker.userID) || IsAuthed(block, attacker);
                        if (trace) Trace($"Initiator is player and target is wood block, with WoodenDamage flag set; {(isAllowed ? "allow" : "block")} and return", 1);
                        return isAllowed;
                    }
                }

                if (ruleSet.HasFlag(RuleFlags.NoPlayerDamageToMini) && entity is Minicopter)
                {
                    if (trace) Trace("Initiator is player and target is Minicopter, with NoPlayerDamageToMini flag set; block and return", 1);
                    return false;
                }

                if (ruleSet.HasFlag(RuleFlags.NoPlayerDamageToScrap) && entity is ScrapTransportHelicopter)
                {
                    if (trace) Trace("Initiator is player and target is ScrapTransportHelicopter, with NoPlayerDamageToScrap flag set; block and return", 1);
                    return false;
                }

                if (ruleSet.HasFlag(RuleFlags.NoPlayerDamageToCar) && entity.name.Contains("modularcar"))
                {
                    if (trace) Trace("Initiator is player and target is ModularCar, with NoPlayerDamageToCar flag set; block and return", 1);
                    return false;
                }

                if (entity.OwnerID == 0 && entity is AdvancedChristmasLights)
                {
                    if (trace) Trace($"Entity is christmas lights; block and return", 1);
                    return false;
                }

                if (entity is GrowableEntity)
                {
                    bool isAllowed = entity.GetParentEntity() is not PlanterBox planter || !planter.OwnerID.IsSteamId() || IsAlly(planter.OwnerID, attacker.userID);
                    if (trace) Trace($"Entity is growable entity; {(isAllowed ? "allow ally" : "block non-ally")} and return", 1);
                    return isAllowed;
                }

                if (config.SleepingBags && entity is SleepingBag)
                {
                    if (trace) Trace("Initiator is player and target is sleeping bag; allow and return", 1);
                    return true;
                }

                if (config.Campfires && entity.name.Contains("campfire"))
                {
                    if (trace) Trace("Initiator is player and target is campfire; allow and return", 1);
                    return true;
                }

                if (config.Ladders && entity is BaseLadder)
                {
                    if (trace) Trace("Initiator is player and target is ladder; allow and return", 1);
                    return true;
                }

                if (isVictim)
                {
                    if (ruleSet.HasFlag(RuleFlags.FriendlyFire) && victim.userID.IsSteamId() && victim.userID != attacker.userID && IsAlly(attacker.userID, victim.userID))
                    {
                        if (trace) Trace("Initiator and target are allied players, with FriendlyFire flag set; allow and return", 1);
                        return true;
                    }

                    // allow sleeper damage by admins if configured
                    if (ruleSet.HasFlag(RuleFlags.AdminsHurtSleepers) && attacker.IsAdmin && victim.IsSleeping())
                    {
                        if (trace) Trace("Initiator is admin player and target is sleeping player, with AdminsHurtSleepers flag set; allow and return", 1);
                        return true;
                    }

                    // allow Human NPC damage if configured
                    if (ruleSet.HasFlag(RuleFlags.HumanNPCDamage) && IsHumanNPC(attacker, victim))
                    {
                        if (trace) Trace("Initiator or target is HumanNPC, with HumanNPCDamage flag set; allow and return", 1);
                        return true;
                    }
                }
                else if (ruleSet.HasFlag(RuleFlags.AuthorizedDamage) && !isVictim && !entity.IsNpc && attacker.userID.IsSteamId())
                { // ignore checks if authorized damage enabled (except for players and npcs)
                    if (ruleSet.HasFlag(RuleFlags.AuthorizedDamageCheckPrivilege))
                    {
                        if (entity is DecayEntity decayEntity && IsAuthed(decayEntity, attacker))
                        {
                            if (trace) Trace("Initiator is player with building priv over target; allow and return", 1);
                            return true;
                        }
                        if (entity is BaseHelicopter playerHelicopter && !(entity is PatrolHelicopter) && IsAuthed(playerHelicopter, attacker))
                        {
                            if (trace) Trace("Initiator is player with heli priv over target; allow and return", 1);
                            return true;
                        }
                        if (entity is Tugboat tugboat && IsAuthed(tugboat, attacker))
                        {
                            if (trace) Trace("Initiator is player with tugboat priv over target; allow and return", 1);
                            return true;
                        }
                        if (entity.HasParent() && entity.GetParentEntity() is Tugboat tugboat2 && IsAuthed(tugboat2, attacker))
                        {
                            if (trace) Trace("Initiator is player with tugboat priv over target; allow and return", 1);
                            return true;
                        }
                    }

                    if (ruleSet.HasFlag(RuleFlags.AuthorizedDamageRequiresOwnership) && !IsAlly(entity.OwnerID, attacker.userID) && CanAuthorize(entity, attacker, ruleSet))
                    {
                        if (trace) Trace("Initiator is player who does not own the target; block and return", 1);
                        return false;
                    }

                    bool cupboardOwnership = ruleSet.HasFlag(RuleFlags.CupboardOwnership);

                    if (CheckAuthorized(entity, attacker, ruleSet, cupboardOwnership))
                    {
                        if (entity is SamSite || entity.name.Contains("modular") || entity is BaseMountable)
                        {
                            if (trace) Trace($"Target is {entity.GetType().Name}; evaluate and return", 1);
                            return EvaluateRules(entity, hitInfo, ruleSet);
                        }
                        if (trace) Trace("Initiator is player with authorization over target; allow and return", 1);
                        return true;
                    }

                    if (cupboardOwnership)
                    {
                        if (trace) Trace("Initiator is player without authorization over target; block and return", 1);
                        return false;
                    }
                }
            }

            if (trace) Trace("No match in pre-checks; evaluating RuleSet rules...", 1);
            return EvaluateRules(entity, hitInfo, ruleSet);
        }

        private void TwigOutputHandler(BaseCombatEntity entity, DamageType damageType, float damageAmount, BasePlayer attacker, BuildingBlock block, bool selfDamageFlag)
        {
            if ((config.options.Twig.Log || config.options.Twig.Notify || config.options.Twig.ReflectDamageMultiplier > 0f) && attacker.userID.IsSteamId() && !IsAlly(entity.OwnerID, attacker.userID))
            {
                if (config.options.Twig.Log)
                {
                    string ownerDisplayName = BasePlayer.FindAwakeOrSleepingByID(entity.OwnerID) is BasePlayer owner ? owner.displayName : "Unknown Owner";
                    Puts($"Twig Damage: Attacker - {attacker.displayName} ({attacker.userID}) | Twig Owner: {ownerDisplayName} ({entity.OwnerID}) at Location: {block.transform.position} | Damage Amount: {damageAmount}");
                }

                if (config.options.Twig.Notify)
                {
                    SendReply(attacker, GetMessage("Twig", attacker.UserIDString));
                }

                if (config.options.Twig.ReflectDamageMultiplier > 0f)
                {
                    float reflectedDamage = damageAmount * config.options.Twig.ReflectDamageMultiplier;

                    if (!selfDamageFlag)
                    {
                        damageType = DamageType.Radiation;
                    }

                    attacker.Hurt(new HitInfo(attacker, attacker, damageType, reflectedDamage)
                    {
                        UseProtection = config.options.Twig.ReflectDamageProtection
                    });

                    if (config.options.Twig.Log)
                    {
                        Puts($"Debug: Attacker {attacker.displayName} ({attacker.userID}) was hurt for {reflectedDamage} damage. New Health: {attacker.health}");
                    }
                }
            }
        }

        private bool IsMetabolismDamage(BasePlayer victim, DamageType type, float amount)
        {
            if (victim.IsRealNull() || !victim.userID.IsSteamId()) return false;
            var delta = ConVar.Player.serverTickInterval;
            var temperature = victim.metabolism.temperature;
            if (type == DamageType.Cold && temperature.value < -20f) return amount <= Mathf.InverseLerp(1f, -50f, temperature.value) * delta * 1f;
            if (type == DamageType.Cold && temperature.value < -10f) return amount <= Mathf.InverseLerp(1f, -50f, temperature.value) * delta * 0.3f;
            if (type == DamageType.Cold && temperature.value < 1f) return amount <= Mathf.InverseLerp(1f, -50f, temperature.value) * delta * 0.1f;
            if (type == DamageType.Heat && temperature.value > 60f) return amount <= Mathf.InverseLerp(60f, 200f, temperature.value) * delta * 5f;
            return false;
        }

        private bool IsTutorialNetworkGroup(BaseCombatEntity entity)
        {
            if (entity.net == null || entity.net.group == null) return false;
            return TutorialIsland.IsTutorialNetworkGroup(entity.net.group.ID);
        }

        private object CheckImmortalFlag(BaseCombatEntity entity, HitInfo hitInfo, RuleSet ruleSet)
        {
            // Check storage containers and doors for locks for player entity only
            if (ruleSet.HasFlag(RuleFlags.LockedBoxesImmortal) && entity is StorageContainer && !(entity is LootContainer) || ruleSet.HasFlag(RuleFlags.LockedDoorsImmortal) && entity is Door)
            {
                if (ruleSet.HasFlag(RuleFlags.ExcludeTugboatFromImmortalFlags) && entity.GetParentEntity() is Tugboat)
                {
                    if (trace) Trace($"Player Door/StorageContainer detected with immortal flag on tugboat with ImmortalExcludesTugboats flag; allow and return", 1);
                    return true;
                }
                object hurt = CheckLock(ruleSet, entity, hitInfo); // check for lock
                if (trace) Trace($"Player Door/StorageContainer detected with immortal flag; lock check results: {(hurt == null ? "null (no lock or unlocked); continue checks" : (bool)hurt ? "allow and return" : "block and return")}", 1);
                if (hurt is bool val) return val;
            }
            return null;
        }

        private void TrySetInitiator(HitInfo hitInfo)
        {
            void SetInitiator(BaseEntity weapon)
            {
                if (!(weapon == null) && !(hitInfo.Initiator is BasePlayer))
                {
                    if (weapon.creatorEntity is BasePlayer)
                    {
                        hitInfo.Initiator = weapon.creatorEntity;
                    }
                    else if (weapon.GetParentEntity() is BasePlayer attacker1)
                    {
                        hitInfo.Initiator = attacker1;
                    }
                    else if (weapon is BasePlayer attacker2)
                    {
                        hitInfo.Initiator = attacker2;
                    }
                }
            }

            SetInitiator(hitInfo.WeaponPrefab);
            SetInitiator(hitInfo.Weapon);
        }

        private bool HandleHelicopter(RuleSet ruleSet, BaseCombatEntity entity, BaseEntity weapon, bool allow)
        {
            if (entity is BasePlayer victim)
            {
                if (ruleSet.HasFlag(RuleFlags.NoHeliDamageSleepers))
                {
                    if (trace) Trace($"Initiator is heli, and target is player; flag check results: {(victim.IsSleeping() ? "victim is sleeping; block and return" : "victim is not sleeping; continue checks")}", 1);
                    if (victim.IsSleeping()) return false;
                }

                if (trace) Trace($"Initiator is heli, and target is player; flag check results: {(ruleSet.HasFlag(RuleFlags.NoHeliDamagePlayer) ? "flag set; block and return" : "flag not set; allow and return")}", 1);
                return !ruleSet.HasFlag(RuleFlags.NoHeliDamagePlayer);
            }
            if (entity is MiningQuarry)
            {
                if (trace) Trace($"Initiator is heli, and target is quarry; flag check results: {(ruleSet.HasFlag(RuleFlags.NoHeliDamageQuarry) ? "flag set; block and return" : "flag not set; allow and return")}", 1);
                return !ruleSet.HasFlag(RuleFlags.NoHeliDamageQuarry);
            }
            if (entity is RidableHorse)
            {
                if (trace) Trace($"Initiator is heli, and target is ridablehorse; flag check results: {(ruleSet.HasFlag(RuleFlags.NoHeliDamageRidableHorses) ? "flag set; block and return" : "flag not set; allow and return")}", 1);
                return !ruleSet.HasFlag(RuleFlags.NoHeliDamageRidableHorses);
            }
            if (ruleSet.HasFlag(RuleFlags.NoHeliDamageBuildings) && IsPlayerEntity(entity))
            {
                if (!entity.HasParent() && entity is DecayEntity decayEntity && !HasBuildingPrivilege(decayEntity))
                {
                    if (trace) Trace($"Initiator is heli, {entity.ShortPrefabName} is not within TC; allow and return", 1);
                    return true;
                }
                if (trace) Trace($"Initiator is heli, {entity.ShortPrefabName} is within TC; block and return", 1);
                return false;
            }
            if (trace) Trace($"Initiator is heli, target is {entity.ShortPrefabName}; {(allow ? "allow and return" : "block and return")}", 1);
            return allow;
        }

        private bool HasBuildingPrivilege(DecayEntity decayEntity)
        {
            var building = decayEntity.GetBuilding();
            if (building == null) return false;
            return building.GetDominatingBuildingPrivilege();
        }

        public bool IsAlly(ulong playerId, ulong targetId)
        {
            if (playerId == targetId)
            {
                return true;
            }

            if (RelationshipManager.ServerInstance.playerToTeam.TryGetValue(playerId, out var team) && team.members.Contains(targetId))
            {
                return true;
            }

            if (Clans != null && Convert.ToBoolean(Clans?.Call("IsClanMember", playerId.ToString(), targetId.ToString())))
            {
                return true;
            }

            if (Friends != null && Convert.ToBoolean(Friends?.Call("AreFriends", playerId.ToString(), targetId.ToString())))
            {
                return true;
            }

            return false;
        }

        private bool CanAuthorize(BaseEntity entity, BasePlayer attacker, RuleSet ruleSet)
        {
            if (entity is BaseVehicle && !EvaluateRules(entity, attacker, ruleSet, false))
            {
                return false;
            }

            if (entity.OwnerID == 0)
            {
                return entity is Minicopter;
            }

            return IsPlayerEntity(entity);
        }

        private HashSet<string> _deployables = new HashSet<string>();

        private bool IsPlayerEntity(BaseEntity entity)
        {
            if (_deployables.Count == 0)
            {
                foreach (var def in ItemManager.GetItemDefinitions())
                {
                    if (def.TryGetComponent<ItemModDeployable>(out var imd))
                    {
                        _deployables.Add(imd.entityPrefab.resourcePath);
                    }
                }
            }
            return entity.PrefabName.Contains("building") || entity.PrefabName.Contains("modular") || entity is BaseMountable || entity is LegacyShelter || entity is LegacyShelterDoor || _deployables.Contains(entity.PrefabName);
        }

        // process rules to determine whether to allow damage
        private bool EvaluateRules(BaseEntity entity, BaseEntity attacker, RuleSet ruleSet, bool returnDefaultValue = true)
        {
            List<string> e0Groups = config.ResolveEntityGroups(attacker);
            List<string> e1Groups = config.ResolveEntityGroups(entity);

            if (trace)
            {
                Trace($"Initiator EntityGroup matches: {(e0Groups.IsNullOrEmpty() ? "none" : string.Join(", ", e0Groups.ToArray()))}", 2);
                Trace($"Target EntityGroup matches: {(e1Groups.IsNullOrEmpty() ? "none" : string.Join(", ", e1Groups.ToArray()))}", 2);
            }

            return ruleSet.Evaluate(e0Groups, e1Groups, attacker, returnDefaultValue);
        }

        private bool EvaluateRules(BaseEntity entity, HitInfo hitInfo, RuleSet ruleSet)
        {
            return EvaluateRules(entity, hitInfo.Initiator ?? hitInfo.WeaponPrefab, ruleSet);
        }

        // checks an entity to see if it has a lock
        private object CheckLock(RuleSet ruleSet, BaseEntity entity, HitInfo hitInfo)
        {
            var slot = entity.GetSlot(BaseEntity.Slot.Lock); // check for lock

            if (slot.IsNull() || !slot.IsLocked())
            {
                return null; // no lock or unlocked, continue checks
            }

            // if HeliDamageLocked flag is false or NoHeliDamage flag, all damage is cancelled from immortal flag
            if (!ruleSet.HasFlag(RuleFlags.HeliDamageLocked) || ruleSet.HasFlag(RuleFlags.NoHeliDamage))
            {
                return false;
            }

            return CheckHeliInitiator(ruleSet, hitInfo) is bool val ? val : (object)null; // cancel damage except from heli
        }

        private object CheckHeliInitiator(RuleSet ruleSet, HitInfo hitInfo)
        {
            // Check for heli initiator
            if (hitInfo.Initiator is PatrolHelicopter || (hitInfo.Initiator != null && (hitInfo.Initiator.ShortPrefabName.Equals("oilfireballsmall") || hitInfo.Initiator.ShortPrefabName.Equals("napalm"))))
            {
                return !ruleSet.HasFlag(RuleFlags.NoHeliDamage);
            }
            else if (hitInfo.WeaponPrefab != null && (hitInfo.WeaponPrefab.ShortPrefabName.Equals("rocket_heli") || hitInfo.WeaponPrefab.ShortPrefabName.Equals("rocket_heli_napalm")))
            {
                return !ruleSet.HasFlag(RuleFlags.NoHeliDamage);
            }
            return null;
        }

        // checks if the player is authorized to damage the entity
        private bool CheckAuthorized(BaseEntity entity, BasePlayer player, RuleSet ruleSet, bool cupboardOwnership)
        {
            if (!cupboardOwnership)
            {
                return entity.OwnerID == 0 && !entity.InSafeZone() || IsAlly(entity.OwnerID, player.userID); // allow damage to entities that the player owns or is an ally of
            }

            // treat entities outside of cupboard range as unowned, and entities inside cupboard range require authorization
            if (entity is LegacyShelter || entity is LegacyShelterDoor)
            {
                var entityPriv = entity.GetEntityBuildingPrivilege();

                return entityPriv == null || entityPriv.AnyAuthed() && entityPriv.IsAuthed(player);
            }

            BuildingPrivlidge priv = null;
            if (entity is DecayEntity decayEntity)
            {
                BuildingManager.Building building = decayEntity.GetBuilding();
                if (building != null)
                {
                    priv = building.GetDominatingBuildingPrivilege();
                }
            }
            priv ??= player.GetBuildingPrivilege(entity.WorldSpaceBounds());

            return priv == null || priv.AnyAuthed() && priv.IsAuthed(player);
        }

        private bool IsFunTurret(AutoTurret turret)
        {
            return turret.GetAttachedWeapon() is BaseProjectile projectile && projectile.GetItem() is Item weapon && weapon.info.shortname.StartsWith("fun.");
        }

        private object OnSamSiteTarget(SamSite ss, BaseEntity entity)
        {
            if (Interface.CallHook("CanEntityBeTargeted", new object[] { entity, ss }) is bool val)
            {
                if (val)
                {
                    if (trace) Trace($"CanEntityBeTargeted allowed {entity.ShortPrefabName} to be targetted by SamSite", 1);
                    return null;
                }

                if (trace) Trace($"CanEntityBeTargeted blocked {entity.ShortPrefabName} from being targetted by SamSite", 1);
                ss.CancelInvoke(ss.WeaponTick);
                return true;
            }

            RuleSet ruleSet = GetRuleSet(entity, ss);

            if (ruleSet == null)
            {
                if (trace) Trace($"OnSamSiteTarget allowed {entity.ShortPrefabName} to be targetted; no ruleset found.", 1);
                return null;
            }

            if (entity is MLRSRocket)
            {
                if (ruleSet.HasFlag(RuleFlags.SamSitesIgnoreMLRS)) return SamSiteHelper(ss, entity);
                return null;
            }

            if (ss.staticRespawn && ruleSet.HasFlag(RuleFlags.StaticSamSitesIgnorePlayers)) return SamSiteHelper(ss, entity);
            if (!ss.staticRespawn && ruleSet.HasFlag(RuleFlags.PlayerSamSitesIgnorePlayers)) return SamSiteHelper(ss, entity);

            return null;
        }

        private object OnMlrsFire(MLRS mlrs, BasePlayer player)
        {
            if (mlrs == null || player == null)
            {
                return true;
            }

            if (Interface.CallHook("CanMlrsTargetLocation", new object[] { mlrs, player }) is bool val)
            {
                if (val)
                {
                    if (trace) Trace($"CanMlrsTargetLocation allowed {mlrs.TrueHitPos} to be targetted by {player.displayName}", 1);
                    return null;
                }

                if (trace) Trace($"CanMlrsTargetLocation blocked {mlrs.TrueHitPos} from being targetted by {player.displayName}", 1);
                return true;
            }

            RuleSet ruleSet = GetRuleSet(player, mlrs);

            if (ruleSet == null)
            {
                if (trace) Trace($"CanMlrsTargetLocation allowed {mlrs.TrueHitPos} to be targetted by {player.displayName}; no ruleset found.", 1);
                return null;
            }

            return ruleSet.HasFlag(RuleFlags.NoMLRSDamage) ? true : (object)null;
        }

        private object OnEntityMarkHostile(BasePlayer player, float duration) => true;

        private void OnEntitySpawned(BaseOven oven)
        {
            if (config.options.disableBaseOvenSplash)
            {
                oven.disabledBySplash = false;
            }
        }

        private void OnEntitySpawned(MLRSRocket rocket)
        {
            if (rocket.IsNull()) return;
            List<MLRS> systems = new();
            Vis.Entities<MLRS>(rocket.transform.position, 15f, systems, -1);
            if (systems.Count == 0 || CheckIsEventTerritory(systems[0].TrueHitPos)) return;
            if (systems[0].rocketOwnerRef.Get(true) is not BasePlayer owner) return;
            rocket.creatorEntity = owner;
            rocket.OwnerID = owner.userID;
        }

        private bool CheckIsEventTerritory(Vector3 position)
        {
            if (AbandonedBases.CanCall() && Convert.ToBoolean(AbandonedBases?.Call("EventTerritory", position))) return true;
            if (RaidableBases.CanCall() && Convert.ToBoolean(RaidableBases?.Call("EventTerritory", position))) return true;
            return false;
        }

        private object SamSiteHelper(SamSite ss, BaseEntity entity)
        {
            var entityLocations = GetLocationKeys(entity);
            var initiatorLocations = GetLocationKeys(ss);

            // check for exclusion zones (zones with no rules mapped)
            if (CheckExclusion(entityLocations, initiatorLocations, false))
            {
                if (trace) Trace($"OnSamSiteTarget allowed {entity.ShortPrefabName} to be targetted; exclusion of zone found.", 1);
                return null;
            }

            // check for exclusions in entity groups
            if (CheckExclusion(ss))
            {
                if (trace) Trace($"OnSamSiteTarget allowed {entity.ShortPrefabName} to be targetted; exclusion found in entity group.", 1);
                return null;
            }

            if (trace && entity is BasePlayer) Trace($"SamSitesIgnorePlayers blocked {entity.ShortPrefabName} from being targetted.", 1);
            else if (trace && entity is MLRSRocket) Trace($"SamSitesIgnoreMLRS blocked {entity.ShortPrefabName} from being targetted.", 1);
            ss.CancelInvoke(ss.WeaponTick);
            return true;
        }

        // check if entity can be targeted
        private object OnEntityEnter(TargetTrigger trigger, BasePlayer target)
        {
            if (trigger == null || target == null)
            {
                return null;
            }

            var entity = trigger.GetComponentInParent<BaseEntity>();

            return OnEntityEnterInternal(entity, target);

        }

        private object OnEntityEnterInternal(BaseEntity entity, BasePlayer target)
        {
            if (entity == null || target == null)
            {
                return null;
            }

            if (Interface.CallHook("CanEntityBeTargeted", new object[] { target, entity }) is bool val)
            {
                return val ? (object)null : true;
            }

            if (config.PlayersTriggerTurrets && entity.OwnerID == 0uL && IsTurret(entity) && !entity.HasParent())
            {
                return null;
            }

            RuleSet ruleSet = GetRuleSet(target, entity);

            if (ruleSet == null)
            {
                return null;
            }

            var isAutoTurret = entity is AutoTurret;
            var isStatic = !entity.OwnerID.IsSteamId();
            var isSafeZone = entity is NPCAutoTurret && entity.OwnerID == 0;

            if (target.IsNpc || !target.userID.IsSteamId())
            {
                if (isAutoTurret)
                {
                    return ruleSet.HasFlag(RuleFlags.TurretsIgnoreScientist) && entity.OwnerID.IsSteamId() ? true : (object)null;
                }
                else
                {
                    return ruleSet.HasFlag(RuleFlags.TrapsIgnoreScientist) ? true : (object)null;
                }
            }
            else if (isSafeZone)
            {
                return ruleSet.HasFlag(RuleFlags.SafeZoneTurretsIgnorePlayers) ? true : (object)null;
            }
            else if (isAutoTurret && ruleSet.HasFlag(isStatic ? RuleFlags.StaticTurretsIgnorePlayers : RuleFlags.TurretsIgnorePlayers) || !isAutoTurret && ruleSet.HasFlag(RuleFlags.TrapsIgnorePlayers))
            {
                if (isAutoTurret && IsFunTurret(entity as AutoTurret))
                {
                    return null;
                }

                var entityLocations = GetLocationKeys(target);
                var initiatorLocations = GetLocationKeys(entity);

                // check for exclusion zones (zones with no rules mapped)
                if (CheckExclusion(entityLocations, initiatorLocations, trace))
                {
                    return null;
                }

                // check for exclusions in entity group
                if (CheckExclusion(target, entity) || CheckExclusion(entity))
                {
                    return null;
                }

                return true;
            }

            return null;
        }

        private object OnTurretTarget(AutoTurret turret, BasePlayer target)
        {
            return OnEntityEnterInternal(turret, target);
        }

        // ignore players stepping on traps if configured
        private object OnTrapTrigger(BaseTrap trap, GameObject go)
        {
            var player = go.GetComponent<BasePlayer>();

            if (player.IsNull() || trap.IsNull())
            {
                return null;
            }

            if (Interface.CallHook("CanEntityTrapTrigger", new object[] { trap, player }) is bool val)
            {
                return val ? (object)null : true;
            }

            var entityLocations = GetLocationKeys(player);
            var initiatorLocations = GetLocationKeys(trap);
            RuleSet ruleSet = GetRuleSet(player, trap);

            if (ruleSet == null)
            {
                return null;
            }

            if ((player.IsNpc || !player.userID.IsSteamId()) && ruleSet.HasFlag(RuleFlags.TrapsIgnoreScientist))
            {
                return true;
            }
            else if (player.userID.IsSteamId() && ruleSet.HasFlag(RuleFlags.TrapsIgnorePlayers))
            {
                // check for exclusion zones (zones with no rules mapped)
                if (CheckExclusion(entityLocations, initiatorLocations, false))
                {
                    return null;
                }

                if (CheckExclusion(trap))
                {
                    return null;
                }

                if (config.PlayersTriggerTraps && trap.OwnerID == 0uL && !trap.HasParent())
                {
                    return null;
                }

                return true;
            }

            return null;
        }

        private object OnNpcTarget(BaseNpc npc, BasePlayer target) => OnNpcTargetInternal(npc, target);
        
        private object OnNpcTarget(BaseNPC2 npc, BasePlayer target) => OnNpcTargetInternal(npc, target);
        
        private object OnNpcTargetInternal(BaseEntity npc, BasePlayer target)
        {
            if (!target.IsValid() || !target.userID.IsSteamId() || !target.IsSleeping())
            {
                return null;
            }

            RuleSet ruleSet = GetRuleSet(target, npc);

            if (ruleSet == null || !animalsIgnoreSleepers && !ruleSet.HasFlag(RuleFlags.AnimalsIgnoreSleepers))
            {
                return null;
            }

            var entityLocations = GetLocationKeys(target);
            var initiatorLocations = GetLocationKeys(npc);

            // check for exclusion zones (zones with no rules mapped)
            if (CheckExclusion(entityLocations, initiatorLocations, false))
            {
                return null;
            }

            return true;
        }

        // Check for exclusions in entity groups (attacker)
        private bool CheckExclusion(BaseEntity attacker)
        {
            string attackerName = attacker.GetType().Name;

            return config.groups.Exists(group => group.IsExclusion(attacker.ShortPrefabName) || group.IsExclusion(attackerName));
        }

        // Check for exclusions in entity groups (target, attacker)
        private bool CheckExclusion(BaseEntity target, BaseEntity attacker)
        {
            string targetName = target.GetType().Name;

            if (!config.groups.Exists(group => group.IsMember(target.ShortPrefabName) || group.IsExclusion(targetName)))
            {
                return false;
            }

            string attackerName = attacker.GetType().Name;

            return config.groups.Exists(group => group.IsExclusion(attacker.ShortPrefabName) || group.IsExclusion(attackerName));
        }

        private RuleSet GetRuleSet(List<string> vicLocations, List<string> atkLocations)
        {
            RuleSet ruleSet = currentRuleSet;

            if (config.PVEZones)
            {
                if (atkLocations == null) atkLocations = vicLocations; // Allow TruePVE to be used on PVP servers that want to add *PVE* zones via Zone Manager (just do this inside of Zone Manager instead...)
            }

            if (!vicLocations.IsNullOrEmpty() && !atkLocations.IsNullOrEmpty())
            {
                if (trace) Trace($"Beginning RuleSet lookup for [{(vicLocations.Count == 0 ? "empty" : string.Join(", ", vicLocations.ToArray()))}] and [{(atkLocations.Count == 0 ? "empty" : string.Join(", ", atkLocations.ToArray()))}]", 2);

                var locations = GetSharedLocations(vicLocations, atkLocations);

                if (trace) Trace($"Shared locations: {(locations.Count == 0 ? "none" : string.Join(", ", locations.ToArray()))}", 3);

                if (locations?.Count > 0)
                {
                    var names = locations.Select(s => config.mappings[s]).ToList();
                    var sets = config.ruleSets.Where(r => names.Contains(r.name)).ToList();

                    if (trace) Trace($"Found {names.Count} location names, with {sets.Count} mapped RuleSets", 3);

                    if (sets.Count == 0 && config.mappings.ContainsKey(AllZones) && config.ruleSets.Exists(r => r.name == config.mappings[AllZones]))
                    {
                        sets.Add(config.ruleSets.FirstOrDefault(r => r.name == config.mappings[AllZones]));
                        if (trace) Trace($"Found allzones mapped RuleSet", 3);
                    }

                    if (sets.Count > 1)
                    {
                        if (trace) Trace($"WARNING: Found multiple RuleSets: {string.Join(", ", sets.Select(s => s.name))}", 3);
                        PrintWarning(string.Join(", ", sets.Select(s => s.name)));
                    }

                    ruleSet = sets.FirstOrDefault();
                    if (trace) Trace($"Found RuleSet: {ruleSet?.name ?? "null"}", 3);
                }
            }
            else if (shareRedirectEnabled && atkLocations?.Count == 0 && vicLocations?.Count > 0)
            {
                return dudRuleSet;
            }

            if (ruleSet == null)
            {
                ruleSet = currentRuleSet;
                if (trace) Trace($"No RuleSet found; assigned current global RuleSet: {ruleSet?.name ?? "null"}", 3);
            }

            return ruleSet;
        }

        private RuleSet GetRuleSet(BaseEntity e0, BaseEntity e1)
        {
            return GetRuleSet(GetLocationKeys(e0), GetLocationKeys(e1));
        }

        // get locations shared between the two passed location lists
        private List<string> GetSharedLocations(List<string> e0Locations, List<string> e1Locations)
        {
            //return System.Linq.Enumerable.Intersect(e0Locations, e1Locations).Where(s => config.HasMapping(s)).ToList();
            return e0Locations.Intersect(e1Locations).Where(s => config.HasMapping(s)).ToList();
        }

        // Check exclusion for given entity locations
        private bool CheckExclusion(List<string> e0Locations, List<string> e1Locations, bool trace)
        {
            if (e0Locations == null || e1Locations == null)
            {
                if (trace) Trace("No shared locations (empty location) - no exclusions", 3);
                return false;
            }
            if (trace) Trace($"Checking exclusions between [{(e0Locations.Count == 0 ? "empty" : string.Join(", ", e0Locations.ToArray()))}] and [{(e1Locations.Count == 0 ? "empty" : string.Join(", ", e1Locations.ToArray()))}]", 2);
            List<string> locations = GetSharedLocations(e0Locations, e1Locations);
            if (trace) Trace($"Shared locations: {(locations.Count == 0 ? "none" : string.Join(", ", locations.ToArray()))}", 3);
            if (!locations.IsNullOrEmpty())
            {
                foreach (string loc in locations)
                {
                    if (config.HasEmptyMapping(loc))
                    {
                        if (trace) Trace($"Found exclusion mapping for location: {loc}", 3);
                        return true;
                    }
                }
            }
            if (trace) Trace("No shared locations, or no matching exclusion mapping - no exclusions", 3);
            return false;
        }

        // add or update a mapping
        private bool AddOrUpdateMapping(string key, string ruleset)
        {
            if (string.IsNullOrEmpty(key) || config == null || ruleset == null || (ruleset != "exclude" && !config.ruleSets.Exists(r => r.name == ruleset)))
                return false;

            config.mappings[key] = ruleset;
            SaveConfig();

            return true;
        }

        // remove a mapping
        private bool RemoveMapping(string key)
        {
            if (config.mappings.Remove(key))
            {
                SaveConfig();
                return true;
            }
            return false;
        }
        #endregion

        #region Messaging
        private void Message(BasePlayer player, string key, params object[] args) => SendReply(player, BuildMessage(player, key, args));

        private void Message(IPlayer user, string key, params object[] args) => user.Reply(RemoveFormatting(BuildMessage(user.Object as BasePlayer, key, args)));

        // build message string
        private string BuildMessage(BasePlayer player, string key, params object[] args)
        {
            string message = GetMessage(key, player?.UserIDString);
            if (args.Length > 0) message = string.Format(message, args);
            string type = key.Split('_')[0];
            if (player != null)
            {
                string size = GetMessage("Format_" + type + "Size");
                string color = GetMessage("Format_" + type + "Color");
                return WrapSize(size, WrapColor(color, message));
            }
            else
            {
                string color = GetMessage("Format_" + type + "Color");
                return WrapColor(color, message);
            }
        }

        // prints the value of an Option
        private void PrintValue(ConsoleSystem.Arg arg, string text, bool value)
        {
            SendReply(arg, WrapSize(GetMessage("Format_NotifySize"), WrapColor(GetMessage("Format_NotifyColor"), text + ": ") + value));
        }

        // wrap string in <size> tag, handles parsing size string to integer
        private string WrapSize(string size, string input)
        {
            return int.TryParse(size, out var i) ? WrapSize(i, input) : input;
        }

        // wrap a string in a <size> tag with the passed size
        private string WrapSize(int size, string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            return "<size=" + size + ">" + input + "</size>";
        }

        // wrap a string in a <color> tag with the passed color
        private string WrapColor(string color, string input)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(color))
                return input;
            return "<color=" + color + ">" + input + "</color>";
        }

        // show usage information
        private void ShowUsage(IPlayer user) => user.Reply(RemoveFormatting(usageString));

        public string RemoveFormatting(string source) => source.Contains(">") ? Regex.Replace(source, "<.*?>", string.Empty) : source;

        // warn that the server is set to PVE mode
        private void WarnPve() => PrintWarning(GetMessage("Warning_PveMode"));
        #endregion

        #region Helper Procedures

        // is player a HumanNPC
        private bool IsHumanNPC(BasePlayer attacker, BasePlayer victim)
        {
            return attacker.IsNpc || !attacker.userID.IsSteamId() || victim.IsNpc || !victim.userID.IsSteamId();
        }

        // get location keys from ZoneManager (zone IDs) or LiteZones (zone names)
        private List<string> GetLocationKeys(BaseEntity entity)
        {
            if (!useZones || !entity) return null;
            List<string> locations = new List<string>();
            string zname;
            if (ZoneManager.CanCall())
            {
                List<string> zmloc = new List<string>();
                if (entity is BasePlayer player)
                {
                    // BasePlayer fix from chadomat
                    string[] zmlocply = (string[])ZoneManager.Call("GetPlayerZoneIDs", new object[] { player });
                    if (!zmlocply.IsNullOrEmpty()) zmloc.AddRange(zmlocply);
                }
                else if (entity.IsValid())
                {
                    string[] zmlocent = (string[])ZoneManager.Call("GetEntityZoneIDs", new object[] { entity });
                    if (!zmlocent.IsNullOrEmpty()) zmloc.AddRange(zmlocent);
                }
                if (zmloc != null && zmloc.Count > 0)
                {
                    // Add names into list of ID numbers
                    foreach (string s in zmloc)
                    {
                        locations.Add(s);
                        zname = (string)ZoneManager.Call("GetZoneName", s);
                        if (zname != null) locations.Add(zname);
                    }
                }
            }
            if (LiteZones.CanCall())
            {
                List<string> lzloc = (List<string>)LiteZones?.Call("GetEntityZones", new object[] { entity });
                if (lzloc != null && lzloc.Count > 0)
                {
                    locations.AddRange(lzloc);
                }
            }
            return locations;
        }
        private List<string> _foundMessages = new List<string>();

        // handle raycast from player (for prodding)
        private bool GetRaycastTarget(BasePlayer player, out BaseEntity closestEntity)
        {
            if (Physics.Raycast(player.eyes.HeadRay(), out var hit, 10f) && hit.GetEntity() is BaseEntity hitEntity)
            {
                closestEntity = hitEntity;
                return closestEntity != null;
            }
            closestEntity = null;
            return false;
        }

        // loop to update current ruleset
        private void TimerLoop(bool firstRun = false)
        {
            config.schedule.ClockUpdate(out var ruleSetName, out currentBroadcastMessage);
            if (currentRuleSet.name != ruleSetName || firstRun)
            {
                currentRuleSet = config.ruleSets.FirstOrDefault(r => r.name == ruleSetName);
                currentRuleSet ??= new(ruleSetName); // create empty ruleset to hold name
                if (config.schedule.broadcast && !string.IsNullOrEmpty(currentBroadcastMessage))
                {
                    Server.Broadcast(currentBroadcastMessage, GetMessage("Prefix"));
                    Puts(RemoveFormatting(GetMessage("Prefix") + " Schedule Broadcast: " + currentBroadcastMessage));
                }
            }

            if (config.schedule.enabled)
                scheduleUpdateTimer = timer.Once(config.schedule.useRealtime ? 30f : 3f, () => TimerLoop());
        }

        #endregion

        #region Subclasses
        // configuration and data storage container

        private class TwigDamageOptions
        {
            [JsonProperty(PropertyName = "Log Offenses")]
            public bool Log { get; set; }

            [JsonProperty(PropertyName = "Notify Offenders")]
            public bool Notify { get; set; }

            [JsonProperty(PropertyName = "Reflect Damage Multiplier")]
            public float ReflectDamageMultiplier { get; set; }

            [JsonProperty(PropertyName = "Multiplier Allows Armor Protection")]
            public bool ReflectDamageProtection { get; set; } = true;
        }

        private class ConfigurationOptions
        {
            [JsonProperty(PropertyName = "TwigDamage (FLAG)")]
            public TwigDamageOptions Twig { get; set; } = new();

            [JsonProperty(PropertyName = "handleDamage")] // (true) enable TruePVE damage handling hooks
            public bool handleDamage { get; set; } = true;

            [JsonProperty(PropertyName = "useZones")] // (true) use ZoneManager/LiteZones for zone-specific damage behavior (requires modification of ZoneManager.cs)
            public bool useZones { get; set; } = true;

            [JsonProperty(PropertyName = "Trace To Player Console")]
            public bool PlayerConsole { get; set; }

            [JsonProperty(PropertyName = "Trace To Server Console")]
            public bool ServerConsole { get; set; } = true;

            [JsonProperty(PropertyName = "Log Trace To File")]
            public bool LogToFile { get; set; } = true;

            [JsonProperty(PropertyName = "Maximum Distance From Player To Trace")]
            public float MaxTraceDistance { get; set; } = 50f;

            [JsonProperty(PropertyName = "Prevent Water From Extinguishing BaseOven")]
            public bool disableBaseOvenSplash { get; set; }

            [JsonProperty(PropertyName = "Prevent Players From Being Marked Hostile")]
            public bool disableHostility { get; set; }

            [JsonProperty(PropertyName = "Allow PVP Below Height")]
            public float Underworld { get; set; } = -500f;

            [JsonProperty(PropertyName = "Allow PVP Above Height")]
            public float Aboveworld { get; set; } = 5000f;

            [JsonProperty(PropertyName = "Allow Other Damage Below Height")]
            public float UnderworldOther { get; set; } = -500f;

            [JsonProperty(PropertyName = "Allow Other Damage Above Height")]
            public float AboveworldOther { get; set; } = 5000f;

            [JsonProperty(PropertyName = "Allow Cold Metabolism Damage")]
            public bool Cold { get; set; }

            [JsonProperty(PropertyName = "Allow Heat Metabolism Damage")]
            public bool Heat { get; set; }
        }

        private class Configuration
        {
            [JsonProperty(PropertyName = "Config Version")]
            public string configVersion = null;
            [JsonProperty(PropertyName = "Default RuleSet")]
            public string defaultRuleSet = "default";
            [JsonProperty(PropertyName = "Configuration Options")]
            public ConfigurationOptions options = new();
            [JsonProperty(PropertyName = "Mappings")]
            public Dictionary<string, string> mappings = new();
            [JsonProperty(PropertyName = "Schedule")]
            public Schedule schedule = new();
            [JsonProperty(PropertyName = "RuleSets")]
            public List<RuleSet> ruleSets = new();
            [JsonProperty(PropertyName = "Entity Groups")]
            public List<EntityGroup> groups = new();
            [JsonProperty(PropertyName = "Allow Killing Sleepers")]
            public bool AllowKillingSleepers;
            [JsonProperty(PropertyName = "Allow Killing Sleepers (Ally Only)")]
            public bool AllowKillingSleepersAlly;
            [JsonProperty(PropertyName = "Allow Killing Sleepers (Authorization Only)")]
            public bool AllowKillingSleepersAuthorization;
            [JsonProperty(PropertyName = "Allow Killing Sleepers (After X Hours Offline)")]
            public float AllowKillingSleepersHoursOffline;
            [JsonProperty(PropertyName = "Ignore Firework Damage")]
            public bool Firework = true;
            [JsonProperty(PropertyName = "Ignore Campfire Damage")]
            public bool Campfires;
            [JsonProperty(PropertyName = "Ignore Ladder Damage")]
            public bool Ladders;
            [JsonProperty(PropertyName = "Ignore Sleeping Bag Damage")]
            public bool SleepingBags;
            [JsonProperty(PropertyName = "Players Can Trigger Traps In Monument Topology")]
            public bool PlayersTriggerTraps = true;
            [JsonProperty(PropertyName = "Players Can Hurt Traps In Monument Topology")]
            public bool PlayersHurtTraps;
            [JsonProperty(PropertyName = "Players Can Trigger Turrets In Monument Topology")]
            public bool PlayersTriggerTurrets = true;
            [JsonProperty(PropertyName = "Players Can Hurt Turrets In Monument Topology")]
            public bool PlayersHurtTurrets;
            [JsonProperty(PropertyName = "Block Scrap Heli Damage")]
            public bool scrap = true;
            [JsonProperty(PropertyName = "Block Igniter Damage")]
            public bool igniter;
            [JsonProperty(PropertyName = "Experimental ZoneManager support for PVE zones")]
            public bool PVEZones;
            Dictionary<NetworkableId, List<string>> groupCache = new();

            public void Init()
            {
                schedule.Init();
                foreach (RuleSet rs in ruleSets)
                    rs.Build();
                ruleSets.Remove(null);
            }

            public List<string> ResolveEntityGroups(BaseEntity entity)
            {
                if (!entity.IsNull())
                {
                    if (!entity.net.IsNull())
                    {
                        if (!groupCache.TryGetValue(entity.net.ID, out var groupList))
                        {
                            groupCache[entity.net.ID] = groupList = groups.Where(g => g.Contains(entity)).Select(g => g.name).ToList();
                        }
                        return groupList;
                    }

                    return groups.Where(g => g.Contains(entity)).Select(g => g.name).ToList();
                }

                return null;
            }

            public bool HasMapping(string key)
            {
                return mappings.ContainsKey(key) || mappings.ContainsKey(AllZones);
            }

            public bool HasEmptyMapping(string key)
            {
                if (mappings.ContainsKey(AllZones) && mappings[AllZones].Equals("exclude")) return true; // exclude all zones
                if (!mappings.ContainsKey(key)) return false;
                if (mappings[key].Equals("exclude")) return true;
                RuleSet r = ruleSets.FirstOrDefault(rs => rs.name.Equals(mappings[key]));
                return r == null || r.IsEmpty();
            }

            public RuleSet GetDefaultRuleSet()
            {
                try
                {
                    return ruleSets.Single(r => r.name == defaultRuleSet);
                }
                catch (Exception)
                {
                    Interface.Oxide.LogWarning($"Warning - duplicate ruleset found for default RuleSet: '{defaultRuleSet}'");
                    return ruleSets.FirstOrDefault(r => r.name == defaultRuleSet);
                }
            }

            public RuleSet GetDudRuleSet()
            {
                return new("override")
                {
                    _flags = RuleFlags.None,
                    defaultAllowDamage = false,
                    enabled = true
                };
            }
        }

        private class RuleSet
        {
            public string name;
            public bool enabled = true;
            public bool defaultAllowDamage = false;
            public string flags = string.Empty;
            [JsonIgnore]
            public RuleFlags _flags = RuleFlags.None;
            [JsonIgnore]
            public bool Changed;

            public HashSet<string> rules = new();
            HashSet<Rule> parsedRules = new();

            public RuleSet() { }
            public RuleSet(string name) { this.name = name; }

            // evaluate the passed lists of entity groups against rules
            public bool Evaluate(List<string> eg1, List<string> eg2, BaseEntity attacker, bool returnDefaultValue = true)
            {
                if (Instance.trace) Instance.Trace("Evaluating Rules...", 3);
                if (parsedRules.IsNullOrEmpty())
                {
                    if (Instance.trace) Instance.Trace($"No rules found; returning default value: {defaultAllowDamage}", 4);
                    return defaultAllowDamage;
                }
                bool? res;
                if (Instance.trace) Instance.Trace("Checking direct initiator->target rules...", 4);
                // check all direct links
                bool resValue = defaultAllowDamage;
                bool resFound = false;

                if (eg1 != null && eg1.Count > 0 && eg2 != null && eg2.Count > 0)
                {
                    foreach (string s1 in eg1)
                    {
                        foreach (string s2 in eg2)
                        {
                            if ((res = Evaluate(s1, s2)).HasValue)
                            {
                                resValue = res.Value;
                                resFound = true;
                                break;
                            }
                        }
                    }
                }

                if (!resFound && eg1 != null && eg1.Count > 0)
                {
                    if (Instance.trace) Instance.Trace("No direct match rules found; continuing...", 4);

                    foreach (string s1 in eg1)
                    {// check group -> any
                        if ((res = Evaluate(s1, Any)).HasValue)
                        {
                            resValue = res.Value;
                            resFound = true;
                            break;
                        }
                    }
                }

                if (!resFound && eg2 != null && eg2.Count > 0)
                {
                    if (Instance.trace) Instance.Trace("No matching initiator->any rules found; continuing...", 4);

                    foreach (string s2 in eg2)
                    {// check any -> group
                        if ((res = Evaluate(Any, s2)).HasValue)
                        {
                            resValue = res.Value;
                            resFound = true;
                            break;
                        }
                    }
                }

                if (resFound)
                {
                    /*if (attacker.IsValid() && Instance.data.groups.Any(group => group.IsExclusion(attacker.GetType().Name) || group.IsExclusion(attacker.ShortPrefabName)))
                    {
                        if (Instance.trace) Instance.Trace($"Exclusion found; allow damage? {!resValue}", 6);
                        return !resValue;
                    }*/

                    return resValue;
                }

                if (returnDefaultValue)
                {
                    if (Instance.trace) Instance.Trace($"No matching any->target rules found; returning default value: {defaultAllowDamage}", 4);
                    return defaultAllowDamage;
                }

                return true;
            }

            // evaluate two entity groups against rules
            public bool? Evaluate(string eg1, string eg2)
            {
                if (eg1 == null || eg2 == null || parsedRules.IsNullOrEmpty()) return null;
                if (Instance.trace) Instance.Trace($"Evaluating \"{eg1}->{eg2}\"...", 5);
                Rule rule = parsedRules.FirstOrDefault(r => r.valid && r.key.Equals(eg1 + "->" + eg2));
                if (rule != null)
                {
                    if (Instance.trace) Instance.Trace($"Match found; allow damage? {rule.hurt}", 6);
                    return rule.hurt;
                }
                if (Instance.trace) Instance.Trace($"No match found", 6);
                return null;
            }

            // build rule strings to rules
            public void Build()
            {
                foreach (string ruleText in rules)
                    parsedRules.Add(new(ruleText));
                parsedRules.Remove(null);
                ValidateRules();
                if (flags.Length == 0)
                {
                    _flags |= RuleFlags.None;
                    return;
                }
                foreach (string _value in flags.Split(','))
                {
                    string value = _value.Trim();
                    RuleFlags flag = GetRuleFlag(value);
                    if (flag == RuleFlags.None)
                    {
                        if (value == "SamSitesIgnorePlayers")
                        {
                            ConvertSamSiteFlag();
                        }
                        else if (value == "TrapsIgnoreScientists")
                        {
                            ConvertTrapsIgnoreScientists();
                        }
                        else if (value == "TurretsIgnoreScientists")
                        {
                            ConvertTurretsIgnoreScientists();
                        }
                        else
                        {
                            Instance.Puts("WARNING - invalid flag: '{0}' (does this flag still exist?)", value);
                        }
                    }
                    else if (!HasFlag(flag))
                    {
                        _flags |= flag;
                    }
                }
                if (Changed)
                {
                    Instance.SaveConfig();
                    Changed = false;
                }
            }

            private void ConvertSamSiteFlag()
            {
                flags = flags.Replace("SamSitesIgnorePlayers", "PlayerSamSitesIgnorePlayers, StaticSamSitesIgnorePlayers");
                if (!HasFlag(RuleFlags.PlayerSamSitesIgnorePlayers))
                {
                    _flags |= RuleFlags.PlayerSamSitesIgnorePlayers;
                }
                if (!HasFlag(RuleFlags.StaticSamSitesIgnorePlayers))
                {
                    _flags |= RuleFlags.StaticSamSitesIgnorePlayers;
                }
                Changed = true;
            }

            private void ConvertTrapsIgnoreScientists()
            {
                flags = flags.Replace("TrapsIgnoreScientists", "TrapsIgnoreScientist");
                if (!HasFlag(RuleFlags.TrapsIgnoreScientist))
                {
                    _flags |= RuleFlags.TrapsIgnoreScientist;
                }
                Changed = true;
            }

            private void ConvertTurretsIgnoreScientists()
            {
                flags = flags.Replace("TurretsIgnoreScientists", "TurretsIgnoreScientist");
                if (!HasFlag(RuleFlags.TurretsIgnoreScientist))
                {
                    _flags |= RuleFlags.TurretsIgnoreScientist;
                }
                Changed = true;
            }

            public void ValidateRules()
            {
                foreach (Rule rule in parsedRules)
                    if (!rule.valid)
                        Interface.Oxide.LogWarning($"Warning - invalid rule: {rule.ruleText}");
            }

            // add a rule
            public void AddRule(string ruleText)
            {
                rules.Add(ruleText);
                parsedRules.Add(new(ruleText));
            }

            public bool HasAnyFlag(RuleFlags flags) => (_flags | flags) != RuleFlags.None;
            public bool HasFlag(RuleFlags flag) => (_flags & flag) == flag;
            public bool IsEmpty() => rules.IsNullOrEmpty() && _flags == RuleFlags.None;
        }

        private class Rule
        {
            public string ruleText;
            [JsonIgnore]
            public string key;
            [JsonIgnore]
            public bool hurt;
            [JsonIgnore]
            public bool valid;

            public Rule() { }
            public Rule(string ruleText)
            {
                this.ruleText = ruleText;
                valid = RuleTranslator.Translate(this);
            }

            public override int GetHashCode() { return key.GetHashCode(); }

            public override bool Equals(object obj)
            {
                if (obj == null) return false;
                if (obj == this) return true;
                if (obj is Rule)
                    return key.Equals((obj as Rule).key);
                return false;
            }
        }

        // helper class to translate rule text to rules
        private class RuleTranslator
        {
            static readonly Regex regex = new(@"\s+");
            static readonly List<string> synonyms = new() { "anything", "nothing", "all", "any", "none", "everything" };
            public static bool Translate(Rule rule)
            {
                if (string.IsNullOrEmpty(rule.ruleText)) return false;
                string str = rule.ruleText;
                string[] splitStr = regex.Split(str);
                // first and last words should be ruleset names
                string rs0 = splitStr[0];
                string rs1 = splitStr[splitStr.Length - 1];
                string[] mid = splitStr.Skip(1).Take(splitStr.Length - 2).ToArray();
                if (mid == null || mid.Length == 0) return false;

                bool canHurt = true;
                foreach (string s in mid)
                    if (s.Equals("cannot") || s.Equals("can't"))
                        canHurt = false;

                // rs0 and rs1 shouldn't ever be "nothing" simultaneously
                if (rs0.Equals("nothing") || rs1.Equals("nothing") || rs0.Equals("none") || rs1.Equals("none")) canHurt = !canHurt;

                if (synonyms.Contains(rs0)) rs0 = Any;
                if (synonyms.Contains(rs1)) rs1 = Any;

                rule.key = rs0 + "->" + rs1;
                rule.hurt = canHurt;
                return true;
            }
        }

        // container for mapping entities
        private class EntityGroup
        {
            private List<string> memberList { get; set; } = new();
            private List<string> exclusionList { get; set; } = new();
            public string name { get; set; }

            public string members
            {
                get
                {
                    if (memberList.Count == 0) return string.Empty;
                    return string.Join(", ", memberList.ToArray());
                }
                set
                {
                    if (string.IsNullOrEmpty(value)) return;
                    memberList = value.Split(',').Select(s => s.Trim()).ToList();
                }
            }

            public string exclusions
            {
                get
                {
                    if (exclusionList.Count == 0) return string.Empty;
                    return string.Join(", ", exclusionList.ToArray());
                }
                set
                {
                    if (string.IsNullOrEmpty(value)) return;
                    exclusionList = value.Split(',').Select(s => s.Trim()).ToList();
                }
            }

            public EntityGroup()
            {

            }

            public EntityGroup(string name)
            {
                this.name = name;
            }

            public bool IsMember(string value)
            {
                foreach (var member in memberList)
                {
                    if (member.Equals(value, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }

            public bool IsExclusion(string value)
            {
                foreach (var exclusion in exclusionList)
                {
                    if (exclusion.Equals(value, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }

            public bool Contains(BaseEntity entity)
            {
                if (entity.IsNull()) return false;
                return (memberList.Contains(entity.GetType().Name) || memberList.Contains(entity.ShortPrefabName)) && !(exclusionList.Contains(entity.GetType().Name) || exclusionList.Contains(entity.ShortPrefabName));
            }
        }

        // scheduler
        private class Schedule
        {
            public bool enabled;
            public bool useRealtime;
            public bool broadcast;
            public List<string> entries = new();
            List<ScheduleEntry> parsedEntries = new();
            [JsonIgnore]
            public bool valid;

            public void Init()
            {
                foreach (string str in entries)
                    parsedEntries.Add(new(str));
                // schedule not valid if entries are empty, there are less than 2 entries, or there are less than 2 rulesets defined
                if (parsedEntries.IsNullOrEmpty() || parsedEntries.Sum(e => e.valid ? 1 : 0) < 2 || parsedEntries.Select(e => e.ruleSet).Distinct().Count < 2)
                    enabled = false;
                else
                    valid = true;
            }

            // returns delta between current time and next schedule entry
            public void ClockUpdate(out string ruleSetName, out string message)
            {
                TimeSpan time = useRealtime ? new TimeSpan((int)DateTime.Now.DayOfWeek, 0, 0, 0).Add(DateTime.Now.TimeOfDay) : TOD_Sky.Instance.Cycle.DateTime.TimeOfDay;
                try
                {
                    ScheduleEntry se = null;
                    // get the most recent schedule entry
                    if (parsedEntries.Where(t => !t.isDaily).Count > 0)
                        se = parsedEntries.FirstOrDefault(e => e.time == parsedEntries.Where(t => t.valid && t.time <= time && ((useRealtime && !t.isDaily) || !useRealtime)).Max(t => t.time));
                    // if realtime, check for daily
                    if (useRealtime)
                    {
                        ScheduleEntry daily = null;
                        try
                        {
                            daily = parsedEntries.FirstOrDefault(e => e.time == parsedEntries.Where(t => t.valid && t.time <= DateTime.Now.TimeOfDay && t.isDaily).Max(t => t.time));
                        }
                        catch (Exception)
                        { // no daily entries
                        }
                        if (daily != null && se == null)
                            se = daily;
                        if (daily != null && daily.time.Add(new((int)DateTime.Now.DayOfWeek, 0, 0, 0)) > se.time)
                            se = daily;
                    }
                    ruleSetName = se.ruleSet;
                    message = se.message;
                }
                catch (Exception)
                {
                    ScheduleEntry se = null;
                    // if time is earlier than all schedule entries, use max time
                    if (parsedEntries.Where(t => !t.isDaily).Count > 0)
                        se = parsedEntries.FirstOrDefault(e => e.time == parsedEntries.Where(t => t.valid && ((useRealtime && !t.isDaily) || !useRealtime)).Max(t => t.time));
                    if (useRealtime)
                    {
                        ScheduleEntry daily = null;
                        try
                        {
                            daily = parsedEntries.FirstOrDefault(e => e.time == parsedEntries.Where(t => t.valid && t.isDaily).Max(t => t.time));
                        }
                        catch (Exception)
                        { // no daily entries
                        }
                        if (daily != null && se == null)
                            se = daily;
                        if (daily != null && daily.time.Add(new((int)DateTime.Now.DayOfWeek, 0, 0, 0)) > se.time)
                            se = daily;
                    }
                    ruleSetName = se?.ruleSet;
                    message = se?.message;
                }
            }
        }

        // helper class to translate schedule text to schedule entries
        private class ScheduleTranslator
        {
            static readonly Regex regex = new(@"\s+");
            public static bool Translate(ScheduleEntry entry)
            {
                if (string.IsNullOrEmpty(entry.scheduleText)) return false;
                string str = entry.scheduleText;
                string[] splitStr = regex.Split(str, 3); // split into 3 parts
                // first word should be a timespan
                string ts = splitStr[0];
                // second word should be a ruleset name
                string rs = splitStr[1];
                // remaining should be message
                string message = splitStr.Length > 2 ? splitStr[2] : null;

                try
                {
                    if (ts.StartsWith("*."))
                    {
                        entry.isDaily = true;
                        ts = ts.Substring(2);
                    }
                    entry.time = TimeSpan.Parse(ts);
                    entry.ruleSet = rs;
                    entry.message = message;
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        private class ScheduleEntry
        {
            public string ruleSet;
            public string message;
            public string scheduleText;
            public bool valid;
            public TimeSpan time { get; set; }
            [JsonIgnore]
            public bool isDaily = false;

            public ScheduleEntry() { }
            public ScheduleEntry(string scheduleText)
            {
                this.scheduleText = scheduleText;
                valid = ScheduleTranslator.Translate(this);
            }
        }
        #endregion

        #region Lang
        // load default messages to Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new()
            {
                {"Prefix", "<color=#FFA500>[ TruePVE ]</color>" },
                {"Enable", "TruePVE enable set to {0}" },
                {"Twig", "<color=#ff0000>WARNING:</color> It is against server rules to destroy other players' items. Actions logged for admin review." },

                {"Header_Usage", "---- TruePVE usage ----"},
                {"Cmd_Usage_def", "Loads default configuration and data"},
                {"Cmd_Usage_sched", "Enable or disable the schedule" },
                {"Cmd_Usage_prod", "Show the prefab name and type of the entity being looked at"},
                {"Cmd_Usage_map", "Create/remove a mapping entry" },
                {"Cmd_Usage_trace", "Toggle tracing on/off" },

                {"Warning_PveMode", "ConVar server.pve is TRUE!  TruePVE is designed for PVP mode, and may cause unexpected behavior in PVE mode."},
                {"Warning_NoRuleSet", "No RuleSet found for \"{0}\"" },
                {"Warning_DuplicateRuleSet", "Multiple RuleSets found for \"{0}\"" },

                {"Error_InvalidCommand", "Invalid command" },
                {"Error_InvalidParameter", "Invalid parameter: {0}"},
                {"Error_InvalidParamForCmd", "Invalid parameters for command \"{0}\""},
                {"Error_InvalidMapping", "Invalid mapping: {0} => {1}; Target must be a valid RuleSet or \"exclude\"" },
                {"Error_NoMappingToDelete", "Cannot delete mapping: \"{0}\" does not exist" },
                {"Error_NoPermission", "Cannot execute command: No permission"},
                {"Error_NoSuicide", "You are not allowed to commit suicide"},
                {"Error_NoEntityFound", "No entity found"},

                {"Notify_AvailOptions", "Available Options: {0}"},
                {"Notify_DefConfigLoad", "Loaded default configuration"},
                {"Notify_DefDataLoad", "Loaded default mapping data"},
                {"Notify_ProdResult", "Prod results: type={0}, prefab={1}"},
                {"Notify_SchedSetEnabled", "Schedule enabled" },
                {"Notify_SchedSetDisabled", "Schedule disabled" },
                {"Notify_InvalidSchedule", "Schedule is not valid" },
                {"Notify_MappingCreated", "Mapping created for \"{0}\" => \"{1}\"" },
                {"Notify_MappingUpdated", "Mapping for \"{0}\" changed from \"{1}\" to \"{2}\"" },
                {"Notify_MappingDeleted", "Mapping for \"{0}\" => \"{1}\" deleted" },
                {"Notify_TraceToggle", "Trace mode toggled {0}" },

                {"Format_EnableColor", "#00FFFF"}, // cyan
                {"Format_EnableSize", "12"},
                {"Format_NotifyColor", "#00FFFF"}, // cyan
                {"Format_NotifySize", "12"},
                {"Format_HeaderColor", "#FFA500"}, // orange
                {"Format_HeaderSize", "14"},
                {"Format_ErrorColor", "#FF0000"}, // red
                {"Format_ErrorSize", "12"},
            }, this);
        }

        // get message from Lang
        private string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);
        #endregion
    }
}

namespace Oxide.Plugins.TruePVEExtensionMethods
{
    public static class ExtensionMethods
    {
        public static List<T> Distinct<T>(this IEnumerable<T> a) { var b = new List<T>(); using (var c = a.GetEnumerator()) { while (c.MoveNext()) { if (!b.Contains(c.Current)) { b.Add(c.Current); } } } return b; }
        public static bool Exists<T>(this IEnumerable<T> a, Func<T, bool> b = null) { using (var c = a.GetEnumerator()) { while (c.MoveNext()) { if (b == null || b(c.Current)) { return true; } } } return false; }
        public static IEnumerable<T> Intersect<T>(this IEnumerable<T> a, IEnumerable<T> b) { var d = new List<T>(); foreach (T item in b) { d.Add(item); } foreach (T e in a) { if (d.Remove(e)) { yield return e; } } }
        public static T FirstOrDefault<T>(this IEnumerable<T> a, Func<T, bool> b = null) { using (var c = a.GetEnumerator()) { while (c.MoveNext()) { if (b == null || b(c.Current)) { return c.Current; } } } return default; }
        public static T Single<T>(this IEnumerable<T> a, Func<T, bool> b) { var d = new List<T>(); using (var c = a.GetEnumerator()) { while (c.MoveNext()) { if (b(c.Current)) { d.Add(c.Current); } } } if (d.Count > 1) throw new InvalidOperationException("single"); return d[0]; }
        public static IEnumerable<V> Select<T, V>(this IEnumerable<T> a, Func<T, V> b) { var c = new List<V>(); if (a == null) return c; using (var d = a.GetEnumerator()) { while (d.MoveNext()) { c.Add(b(d.Current)); } } return c; }
        public static string[] Skip(this string[] a, int b) { if (a.Length == 0) { return Array.Empty<string>(); } string[] c = new string[a.Length - b]; int n = 0; for (int i = 0; i < a.Length; i++) { if (i < b) continue; c[n] = a[i]; n++; } return c; }
        public static List<T> Take<T>(this IList<T> a, int b) { var c = new List<T>(); for (int i = 0; i < a.Count; i++) { if (c.Count == b) { break; } c.Add(a[i]); } return c; }
        public static List<T> ToList<T>(this IEnumerable<T> a) { var b = new List<T>(); using (var c = a.GetEnumerator()) { while (c.MoveNext()) { b.Add(c.Current); } } return b; }
        public static List<T> Where<T>(this IEnumerable<T> a, Func<T, bool> b) { var c = new List<T>(); using (var d = a.GetEnumerator()) { while (d.MoveNext()) { if (b(d.Current)) { c.Add(d.Current); } } } return c; }
        public static List<T> OfType<T>(this IEnumerable<BaseNetworkable> a) where T : BaseEntity { var b = new List<T>(); using (var c = a.GetEnumerator()) { while (c.MoveNext()) { if (c.Current is T) { b.Add(c.Current as T); } } } return b; }
        public static R Max<T, R>(this IList<T> a, Func<T, R> b) { R c = default(R); Comparer<R> @default = Comparer<R>.Default; for (int i = 0; i < a.Count; i++) { var d = b(a[i]); if (@default.Compare(d, c) > 0) { c = d; } } return c; }
        public static int Sum<T>(this IList<T> a, Func<T, int> b) { int c = 0; for (int i = 0; i < a.Count; i++) { var d = b(a[i]); if (!float.IsNaN(d)) { c += d; } } return c; }
        public static bool IsKilled(this BaseNetworkable a) { try { return (object)a == null || a.IsDestroyed || a.transform == null; } catch { return true; } }
        public static bool IsOnline(this BasePlayer a) { return (object)a != null && (object)a.net != null && (object)a.net.connection != null; }
        public static bool IsNull<T>(this T a) where T : class { return (object)a == null; }
        public static bool CanCall(this Plugin a) { return a != null && a.IsLoaded; }
    }
}