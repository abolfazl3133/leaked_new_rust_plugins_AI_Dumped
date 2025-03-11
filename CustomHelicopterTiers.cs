using ConVar;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Physics = UnityEngine.Physics;
using Pool = Facepunch.Pool;
using Random = UnityEngine.Random;
using Time = UnityEngine.Time;
using Network;
using Facepunch;
using Oxide.Game.Rust.Libraries;

namespace Oxide.Plugins
{
    [Info("Custom Helicopter Tiers", "Dana", "2.5.1")]
    [Description("Create and customize an unlimited number of patrol helicopter levels with different difficulties.")]
    class CustomHelicopterTiers : RustPlugin
    {
        #region References

        [PluginReference] private Plugin ServerRewards;
        [PluginReference] private Plugin Economics;
        [PluginReference] private Plugin TruePVE;

        #endregion References

        #region Config Auto-Update

        private bool AutoUpdateConfig(DynamicConfigFile configFile)
        {
            PrintWarning("Checking for auto-update config...");
            if (configFile == null)
            {
                PrintWarning("Invalid config file! auto-update skipped.");
                return false;
            }

            var config = configFile["Config"] as Dictionary<string, object>;
            var latestVersion = new PluginConfig().Config.Version;
            if (config == null)
            {
                PrintWarning("Config is either new or not supported. auto-update skipped. loading default config...");
                ApplyDefaultConfig();
                return false;
            }

            object configVersion = -1;
            config.TryGetValue("Config Version", out configVersion);
            if (configVersion == null)
            {
                //PrintWarning("Couldn't detect config version. consider adding \"Config Version\": 0 to your config files. auto-update skipped");
                //return false;
                configVersion = 1;
            }

            var oldVersion = -1;
            if (!int.TryParse(configVersion.ToString(), out oldVersion) || oldVersion == -1)
            {
                PrintWarning(
                    "Couldn't read config version. make sure that config version has a value and is an integer. auto-update skipped");
                return false;
            }

            /*if (latestVersion > 0 && oldVersion == 0)
            {
                PrintWarning($"Auto-Updating config version {oldVersion} to {latestVersion} started...");
                var helicopterTiers = config["Helicopter - Tiers"] as Dictionary<string, object>;
                if (helicopterTiers == null)
                {
                    PrintWarning("Config format is not supported. auto-update skipped.");
                    return false;
                }
                config["Config Version"] = 1;
                helicopterTiers["Military"] = helicopterTiers["Regular"];
                helicopterTiers["Elite"] = helicopterTiers["Regular"];
                config["Helicopter - Tiers"] = helicopterTiers;
                var spawnLocations = config["Spawn Locations"] as Dictionary<string, object>;
                if (spawnLocations != null)
                {
                    var itemsToRemove = new List<string>();
                    foreach (var location in spawnLocations)
                    {
                        var locationValue = location.Value as Dictionary<string, object>;
                        if (locationValue != null)
                        {
                            var position = locationValue["Position"] as Dictionary<string, object>;
                            if (position != null)
                            {
                                var x = -1f;
                                float.TryParse(position["x"].ToString(), out x);
                                var y = -1f;
                                float.TryParse(position["y"].ToString(), out y);
                                var z = -1f;
                                float.TryParse(position["z"].ToString(), out z);
                                if (x == 0 && y == 0 && z == 0)
                                {
                                    itemsToRemove.Add(location.Key);
                                }
                            }
                        }
                    }
                    foreach (var itemToRemove in itemsToRemove)
                    {
                        spawnLocations.Remove(itemToRemove);
                    }
                }
                configFile["Spawn Locations"] = spawnLocations;
                configFile.WriteObject(new { Config = config });
                configFile.Save();
                PrintWarning("Auto-Update config finished.");
                if (latestVersion == 1)
                    return true;
                oldVersion = 1;
            }*/
            PrintWarning("No config updates found.");
            return false;
        }

        #endregion Config Auto-Update

        #region Permissions

        private const string CustomHelicopterTiersAdmin = "customhelicoptertiers.admin";

        public static string GetTierSpawnPermissionName(string tierName) =>
            "customhelicoptertiers.call." + tierName.ToLower();

        public static string GetLimitPermissionName(string limitName) =>
            "customhelicoptertiers.limits." + limitName.ToLower();

        public static string AllTierSpawnPermissionName = GetTierSpawnPermissionName("all");

        #endregion Permissions

        #region Fields

        private static CustomHelicopterTiers _instance;
        public const string MainFolderName = "CHT";
        private PluginConfig _pluginConfig = new PluginConfig();
        private const int BlueprintId = -996920608;
        private static readonly string PATROL_HELICOPTER_PREFAB = "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab";
        private static readonly float HelicopterItemsRadius = 25f; // 10f

        private Dictionary<string, PlayerCommandTracker> CommandTracker = new Dictionary<string, PlayerCommandTracker>(StringComparer.InvariantCultureIgnoreCase);

        private Dictionary<PatrolHelicopter, int> strafeCount = new Dictionary<PatrolHelicopter, int>();

        private Dictionary<PatrolHelicopter, HelicopterHitInfo> heliHitTracker = new Dictionary<PatrolHelicopter, HelicopterHitInfo>();

        private Dictionary<ulong, TempBoxData> _tempBoxes = new Dictionary<ulong, TempBoxData>();

        private HashSet<HelicopterDebris> _debris = new HashSet<HelicopterDebris>();
        private Dictionary<BasePlayer, HashSet<FireBall>> _fireBalls = new Dictionary<BasePlayer, HashSet<FireBall>>();

        private Dictionary<string, HashSet<PatrolHelicopter>> SpawnedHelicopters { get; set; } = new Dictionary<string, HashSet<PatrolHelicopter>>(StringComparer.InvariantCultureIgnoreCase);

        private const int LAYER_RAGDOLL = Layers.Mask.Ragdoll;
        private HashSet<LockedByEntCrate> _crates = new HashSet<LockedByEntCrate>();

        private const string CRATE_PREFAB = "assets/prefabs/npc/patrol helicopter/heli_crate.prefab";
        private const string SMALL_FIREBALL_PREFAB = "assets/bundled/prefabs/oilfireballsmall.prefab";

        private const string BLUEPRINT_TEMPLATE = "blueprintbase";

        #endregion Fields

        #region Hooks

        void Loaded()
        {
            if (!_pluginConfig.Config.IsEnabled)
                return;
            LoadTrackers();
            if (CommandTracker == null)
                CommandTracker =
                    new Dictionary<string, PlayerCommandTracker>(StringComparer.InvariantCultureIgnoreCase);
        }

        void OnServerInitialized()
        {
            if (!_pluginConfig.Config.IsEnabled)
                return;

            SpawnedHelicopters = new Dictionary<string, HashSet<PatrolHelicopter>>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity == null)
                    continue;

                PatrolHelicopter helicopter = entity as PatrolHelicopter;
                HelicopterDebris debris = entity as HelicopterDebris;

                if (helicopter != null)
                    AddToHelicopterCollection(helicopter);

                if (debris != null)
                    _debris.Add(debris);
            }

            if (_pluginConfig.Config.GlobalHelicopterConfig.DisableRustDefaultHelicopter)
                DisableDefaultSpawning();

            ConVar.PatrolHelicopter.bulletAccuracy = _pluginConfig.Config.GlobalHelicopterConfig.DamageTurretsBulletAccuracy;
            ConVar.PatrolHelicopter.lifetimeMinutes = _pluginConfig.Config.GlobalHelicopterConfig.SpawnMaximumHelicopterLifeTime;

            GetMonumentsPositions();
            InitializeHelicopters();
        }

        private void Init()
        {
            _instance = this;
            if (!_pluginConfig.Config.IsEnabled)
                return;
            var tiers = _pluginConfig.Config.Tiers;
            if (tiers == null)
            {
                Puts(Lang(PluginMessages.NoTiersFound));
            }
            else
            {
                foreach (var tier in tiers)
                {
                    if (tier.Value == null)
                    {
                        continue;
                    }

                    var spawnPermName = GetTierSpawnPermissionName(tier.Key);

                    if (!permission.PermissionExists(spawnPermName, this))
                        permission.RegisterPermission(spawnPermName, this);

                    if (tier.Value.CallCommand.CommandLimits != null)
                        foreach (var limit in tier.Value.CallCommand.CommandLimits)
                        {
                            var permissionName = GetLimitPermissionName(limit.Key);
                            if (!permission.PermissionExists(permissionName))
                                permission.RegisterPermission(permissionName, this);
                        }
                }

                if (!permission.PermissionExists(AllTierSpawnPermissionName))
                    permission.RegisterPermission(AllTierSpawnPermissionName, this);
            }

            if (!permission.PermissionExists(CustomHelicopterTiersAdmin))
                permission.RegisterPermission(CustomHelicopterTiersAdmin, this);

            if (!plugins.Exists(nameof(Economics)))
                PrintWarning("No Economics plugin found");

            if (!plugins.Exists(nameof(ServerRewards)))
                PrintWarning("No ServerRewards plugin found");

            var allPermissions = permission.GetPermissions();
            var oldKillPermissions = allPermissions.Where(x => x.StartsWith("customhelicoptertiers.kill."));
            var oldSpawnPermissions = "customhelicoptertiers.spawn";
            var oldEditPermissions = "customhelicoptertiers.edit";
            foreach (var basePlayer in BasePlayer.allPlayerList)
            {
                var shouldGrantAdminPermission = false;
                if (permission.UserHasPermission(basePlayer.UserIDString, oldSpawnPermissions))
                {
                    shouldGrantAdminPermission = true;
                    permission.RevokeUserPermission(basePlayer.UserIDString, oldSpawnPermissions);
                }

                if (permission.UserHasPermission(basePlayer.UserIDString, oldEditPermissions))
                {
                    shouldGrantAdminPermission = true;
                    permission.RevokeUserPermission(basePlayer.UserIDString, oldEditPermissions);
                }

                foreach (var oldKillPermission in oldKillPermissions)
                {
                    if (permission.UserHasPermission(basePlayer.UserIDString, oldKillPermission))
                    {
                        shouldGrantAdminPermission = true;
                        permission.RevokeUserPermission(basePlayer.UserIDString, oldKillPermission);
                    }
                }

                if (shouldGrantAdminPermission)
                {
                    permission.GrantUserPermission(basePlayer.UserIDString, CustomHelicopterTiersAdmin, this);
                }
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            if (!AutoUpdateConfig(Config) && _pluginConfig?.Config == null)
            {
                ApplyDefaultConfig();
                SaveConfig();
            }

            _pluginConfig = Config.ReadObject<PluginConfig>();
            SaveConfig();
            Config.Settings.Converters = new JsonConverter[] { new StringEnumConverter() };
            PrintWarning("Config Loaded");
        }

        protected override void SaveConfig() => Config.WriteObject(_pluginConfig, true);

        private void ApplyDefaultConfig()
        {
            _pluginConfig.Config = new HelicopterTierConfig
            {
                IsEnabled = true,
                UseEconomics = false,
                UseServerRewards = true,
                GlobalHelicopterConfig = new GlobalHelicopterConfig
                {
                    DamageTurretsBulletAccuracy = 2,
                    DisableRustDefaultHelicopter = true,
                    SpawnMaximumHelicopterLifeTime = 15,
                    MaximumActivePatrolHelicopters = 3
                },
                SpawnLocations = new Dictionary<string, SpawnLocationData>(),
                MonumentSpawnLocations = new Dictionary<string, SpawnLocationData>(),
                Tiers = GetDefaultTiers()
            };
        }

        protected override void LoadDefaultConfig()
        {
            ApplyDefaultConfig();
            PrintWarning("Loading Default Config");
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [PluginMessages.KillSingleRewardMessage] =
                    "<color=#CCEE33>{0}</color> has been rewarded <color=#00FF00>{1} {2}</color>",
                [PluginMessages.KillDoubleRewardMessage] =
                    "<color=#CCEE33>{0}</color> has been rewarded <color=#00FF00>{1} {2}</color> and <color=#00FF00>{3} {4}</color>",
                [PluginMessages.ServerRewardsName] = "RP",
                [PluginMessages.EconomicsName] = "Money",
                [PluginMessages.KillStats] =
                    "It took {0} and total {1} damage to destroy {2} and <color=#CCEE33>{3}</color> with top rotor accuracy of <color=#00FF00>{4}%</color>!",
                [PluginMessages.NoTiersFound] = "No Tiers Found",
                [PluginMessages.TierNotFound] = "Tier Not Found",
                [PluginMessages.TierNameExist] = "This tier name is currently exist",
                [PluginMessages.PlayerNotFound] = "Player Not Found",
                [PluginMessages.NoCustomLootsFound] =
                    "Unable to populate heli crate with custom loot items because no items were specified in config",
                [PluginMessages.NoItemDefinition] =
                    "Failed to use custom helicopter crate loot item \"{0}\" because no such item definition exists",
                [PluginMessages.IsNotResearchable] =
                    "Failed create helicopter crate loot item \"{0}\" as a blueprint because it is not researchable",
                [PluginMessages.ItemNotFound] =
                    "Failed to create heli crate loot item \"{0}\" because no such item exists",
                [PluginMessages.HelicopterCrateItems] = "--- Helicopter Crate Items ---",
                [PluginMessages.NoLootPermission] = "You don't have permission to loot this container.",
                [PluginMessages.PluginDisabled] = "This plugin is disabled.",
                [PluginMessages.TierAdded] = "New tier Added.",
                [PluginMessages.TierRemoved] = "Tier removed.",
                [PluginMessages.TierNameRequired] = "You must add a tier name as an argument.",
                [PluginMessages.TierNoCallPermission] = "You don't have permission to call this tier.",
                [PluginMessages.TierNoCallPermissionAdmin] = "You don't have {0} permission to call this tier.",
                [PluginMessages.HelicopterTierDisabled] = "That helicopter tier is disabled.",
                [PluginMessages.HelicopterCalledChatMessage] = "Helicopter tier {0} called for {1}",
                [PluginMessages.HelicopterCalledConsoleMessage] = "{0} called helicopter tier {1} for {2}",
                [PluginMessages.HelicopterSpawned] = "Helicopter tier {0} spawned for {1}",
                [PluginMessages.InvalidArgumentForKillCommand] = "You must add a tier name, or \"all\" as an argument.",
                [PluginMessages.TierNoKillPermission] = "You don't have permission to destroy this tier.",
                [PluginMessages.TierNoKillPermissionAdmin] = "You don't have {0} permission to destroy this tier.",
                [PluginMessages.NoHelicoptersToDestroy] = "No helicopters to destroy.",
                [PluginMessages.HelicoptersDestroyedChatMessage] = "You destroyed {0} tier {1} helicopter(s).",
                [PluginMessages.HelicoptersDestroyedConsoleMessage] = "{0} destroyed {1} tier {2} helicopter(s).",
                [PluginMessages.HelicopterListTitle] = "Helicopters Tiers:",
                //[PluginMessages.HelicopterListItem] = "Tier: {0}, Enabled: {1}, AutoSpawn: {2}, RandomSpawn: {3}, Current Active: {4}",
                [PluginMessages.HelicopterListNoData] = "Tier: {0}, No Data To Display",
                [PluginMessages.Days] = "Day(s)",
                [PluginMessages.Hours] = "Hour(s)",
                [PluginMessages.Minutes] = "Minute(s)",
                [PluginMessages.Seconds] = "Second(s)",
                [PluginMessages.PatrolHelicopterInfo] = "<color=#EE0000>{0}</color> Patrol Helicopter",
                [PluginMessages.NoTiersFoundToSpawn] = "No auto or random spawn helicopters since there are no tiers",
                [PluginMessages.NoEnabledTiersFoundToSpawn] =
                    "No auto or random spawn helicopters since all tiers are disabled",
                [PluginMessages.NoAutoSpawnTiersFound] = "No auto spawn helicopters found to spawn",
                [PluginMessages.NoRandomSpawnTiersFound] = "No random spawn helicopters found to spawn",
                [PluginMessages.CooldownMessage] = "You can use this again in {0}.",
                [PluginMessages.DailyLimitMessage] = "You have reached your daily limit for this helicopter ({0}).",
                [PluginMessages.DefaultHelicopterSpawningDisabled] = "Default helicopter spawning disabled",
                [PluginMessages.WrongCommand] =
                    "Wrong Command, Use <color=#5af>{0}</color> to get available commands list",
                [PluginMessages.NoSpawnPermission] = "You don't have permission to use this command",
                [PluginMessages.NoEditPermission] = "You don't have permission to use this command",
                [PluginMessages.InvalidCoordinates] = "Invalid Coordinates",
                [PluginMessages.SpawnNameRequired] = "You must add a spawn name as an argument.",
                [PluginMessages.SpawnPositionSet] = "Spawn position set",
                [PluginMessages.SpawnPositionDeleted] = "Spawn position deleted",
                [PluginMessages.SpawnPositionNotFound] = "Spawn Position Not Found",
                [PluginMessages.HelicopterLimitReached] = "Unable to spawn tier {0} helicopter, because maximum limit reached.",
                [PluginMessages.GlobalActiveHelicoptersLimitReached] = "Cannot spawn patrol helicopter of tier {0} because the maximum limit of active helicopters has been reached.",
                [PluginMessages.InvalidBoxData] = "Invalid box data",
                [PluginMessages.LootBoxUpdated] = "Loot box table updated for helicopter tier {0}",
                [PluginMessages.LootBoxCooldown] = "You should wait {0} for opening this crate",
                [PluginMessages.ManualSpawnBroadcastMessage] =
                    "An aggressive {0} helicopter started flying & patrolling {1}!",
                [PluginMessages.CustomSpawnBroadcastMessage] =
                    "An aggressive {0} helicopter started flying & patrolling around monuments!",
                [PluginMessages.RandomSpawnBroadcastMessage] =
                    "An aggressive {0} helicopter started flying & patrolling!",
                [PluginMessages.Reservation] = "{0} has been reserved to {1}"
            }, this);
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            var name = entity.ShortPrefabName;
            var baseHelicopter = entity as PatrolHelicopter;
            if (baseHelicopter != null)
            {
                var tieredComponent = baseHelicopter.GetComponent<TieredPatrolHelicopterComponent>();
                if (tieredComponent == null)
                    return;

                var baseHelicopterAi = baseHelicopter.GetComponent<PatrolHelicopterAI>();
                if (baseHelicopterAi == null)
                    return;

                Helicopter helicopter;
                if (!_pluginConfig.Config.Tiers.TryGetValue(tieredComponent.Tier, out helicopter) || helicopter == null)
                {
                    Puts(Lang(PluginMessages.TierNotFound));
                    return;
                }

                baseHelicopter.bulletDamage = helicopter.Damage.DamageTurretsBulletDamage;
                baseHelicopter.bulletSpeed = helicopter.Damage.DamageTurretsBulletSpeed;
                if (baseHelicopterAi.leftGun != null && baseHelicopterAi.rightGun != null)
                {
                    baseHelicopterAi.leftGun.fireRate = baseHelicopterAi.rightGun.fireRate = helicopter.Damage.DamageTurretsFireRate;
                    baseHelicopterAi.leftGun.burstLength = baseHelicopterAi.rightGun.burstLength = helicopter.Damage.DamageTurretsDurationOfBurst;
                    baseHelicopterAi.leftGun.timeBetweenBursts = baseHelicopterAi.rightGun.timeBetweenBursts = helicopter.Damage.DamageTurretsIntervalBetweenBursts;
                    baseHelicopterAi.leftGun.maxTargetRange = baseHelicopterAi.rightGun.maxTargetRange = helicopter.Damage.DamageTurretsBulletRange;
                }

                if (Math.Abs(baseHelicopter.startHealth - helicopter.Health.HealthBodyHealth) > 0.0001f)
                {
                    baseHelicopter.startHealth = helicopter.Health.HealthBodyHealth;
                    baseHelicopter.InitializeHealth(helicopter.Health.HealthBodyHealth, helicopter.Health.HealthBodyHealth);
                }

                if (helicopter.Speed.MiscHelicopterStartupLength > 0.0f && Math.Abs(helicopter.Speed.MiscHelicopterStartupSpeed - helicopter.Speed.MiscHelicopterSpeed) > 0.0001f)
                {
                    baseHelicopterAi.maxSpeed = helicopter.Speed.MiscHelicopterStartupSpeed;
                    baseHelicopterAi.Invoke(() =>
                    {
                        if (baseHelicopterAi == null || baseHelicopterAi.gameObject == null || baseHelicopter == null || baseHelicopter.IsDestroyed || baseHelicopter.gameObject == null ||
                            baseHelicopter.IsDead())
                            return;

                        baseHelicopterAi.maxSpeed = helicopter.Speed.MiscHelicopterSpeed;
                    }, helicopter.Speed.MiscHelicopterStartupLength);
                }

                baseHelicopter.weakspots[0].health = helicopter.Health.HealthMainRotorHealth;
                baseHelicopter.weakspots[1].health = helicopter.Health.HealthTailRotorHealth;
                baseHelicopter.maxCratesToSpawn = helicopter.Loot.MaximumCratesToSpawn;
                baseHelicopterAi.timeBetweenRockets = Mathf.Clamp(helicopter.Damage.DamageRocketsTimeBetweenEachRocket, 0.1f, 1f);
                baseHelicopterAi.numRocketsLeft = Mathf.Clamp(helicopter.Damage.DamageRocketsMaxLaunchedRockets, 0, 48);
            }
            else if (entity is HelicopterDebris)
            {
                var gib = entity as HelicopterDebris;
                var helis = Facepunch.Pool.GetList<PatrolHelicopter>();
                var (tieredComponent, tieredConfig) = GetTierSettingsFromVicinity(entity, helis, true);
                if (tieredComponent != null && tieredConfig != null)
                {
                    if (!tieredConfig.Debris.SpawnAfterCrash || tieredConfig.Debris.HarvestableHitPoints <= 0)
                        gib.Kill();
                    else
                    {
                        if (Math.Abs(tieredConfig.Debris.HarvestableHitPoints - 500f) > 0.0001f)
                        {
                            gib.InitializeHealth(tieredConfig.Debris.HarvestableHitPoints, tieredConfig.Debris.HarvestableHitPoints);
                            gib.massReductionScalar = 5 / (tieredConfig.Debris.HarvestableHitPoints / 500);
                            gib.SendNetworkUpdate();
                        }

                        if (Math.Abs(tieredConfig.Debris.CoolingPeriodSeconds - 480f) > 0.0001f)
                        {
                            gib.tooHotUntil = Time.realtimeSinceStartup + tieredConfig.Debris.CoolingPeriodSeconds;
                            gib.SendNetworkUpdate();
                        }
                        _debris.Add(gib);
                    }
                }

                Facepunch.Pool.FreeList(ref helis);
            }
            else if (entity is FireBall)
            {
                var fireball = entity as FireBall;
                var helis = Facepunch.Pool.GetList<PatrolHelicopter>();
                var (tieredComponent, tieredConfig) = GetTierSettingsFromVicinity(entity, helis, true);
                if (tieredComponent != null && tieredConfig != null)
                {
                    if (fireball != null && (fireball.PrefabName.Contains("oilfireballsmall")))
                    {
                        if (tieredComponent.Invoker != null)
                        {
                            if (!_fireBalls.ContainsKey(tieredComponent.Invoker))
                                _fireBalls[tieredComponent.Invoker] = new HashSet<FireBall>();

                            _fireBalls[tieredComponent.Invoker].Add(fireball);
                        }
                    }
                }

                Facepunch.Pool.FreeList(ref helis);
            }
            else if (name.Contains("rocket_heli"))
            {
                var explosion = entity as TimedExplosive;
                if (explosion == null || explosion.IsDestroyed) return;
                var helis = Facepunch.Pool.GetList<PatrolHelicopter>();
                var heliComponent = entity.GetComponent<HelicopterRocket>();
                var strafeHeli = heliComponent != null ? heliComponent.Helicopter : GetHelicopterFromVicinity(entity, helis, false, p => p != null && !p.IsDestroyed && p.gameObject != null && (p?.GetComponent<PatrolHelicopterAI>()?._currentState ?? PatrolHelicopterAI.aiState.IDLE) == PatrolHelicopterAI.aiState.STRAFE);
                if (strafeHeli == null)
                {
                    //explosion.Kill();
                    Facepunch.Pool.FreeList(ref helis);
                    return;
                }

                explosion.creatorEntity = strafeHeli;
                var tieredComponent = strafeHeli.GetComponent<TieredPatrolHelicopterComponent>();
                if (tieredComponent == null)
                {
                    Facepunch.Pool.FreeList(ref helis);
                    return;
                }

                Helicopter helicopter;
                if (!_pluginConfig.Config.Tiers.TryGetValue(tieredComponent.Tier, out helicopter) || helicopter == null)
                {
                    Facepunch.Pool.FreeList(ref helis);
                    return;
                }

                var heliAI = strafeHeli.GetComponent<PatrolHelicopterAI>();
                var ownerID = (entity as BaseEntity).OwnerID;
                if (helicopter.Damage.DamageRocketsMaxLaunchedRockets < 1)
                    explosion.Kill();
                else
                {
                    if (helicopter.Damage.DamageRocketsMaxLaunchedRockets > 12 && ownerID == 0)
                    {
                        if (strafeHeli.IsDestroyed)
                        {
                            Facepunch.Pool.FreeList(ref helis);
                            return;
                        }

                        var curCount = 0;
                        if (!strafeCount.TryGetValue(strafeHeli, out curCount))
                            curCount = strafeCount[strafeHeli] = 1;
                        else
                            curCount = strafeCount[strafeHeli] += 1;
                        if (curCount >= 12)
                        {
                            if (heliAI == null)
                            {
                                Facepunch.Pool.FreeList(ref helis);
                                return;
                            }

                            var actCount = 0;
                            Action fireAct = null;
                            fireAct = new Action(() =>
                            {
                                if (actCount >= helicopter.Damage.DamageRocketsMaxLaunchedRockets - 12)
                                {
                                    InvokeHandler.CancelInvoke(heliAI, fireAct);
                                    return;
                                }

                                actCount++;
                                FireRocket(strafeHeli, heliAI);
                            });
                            InvokeHandler.InvokeRepeating(heliAI, fireAct, helicopter.Damage.DamageRocketsTimeBetweenEachRocket, helicopter.Damage.DamageRocketsTimeBetweenEachRocket);
                            strafeCount[strafeHeli] = 0;
                        }
                    }
                    else if (helicopter.Damage.DamageRocketsMaxLaunchedRockets < 12 && heliAI.ClipRocketsLeft() > helicopter.Damage.DamageRocketsMaxLaunchedRockets)
                    {
                        explosion.Kill();
                        Facepunch.Pool.FreeList(ref helis);
                        return;
                    }

                    var dmgTypes = explosion != null ? explosion.damageTypes : null;
                    explosion.explosionRadius = helicopter.Damage.DamageRocketsExplosionRadius;
                    if (dmgTypes != null && dmgTypes.Count > 0)
                    {
                        for (var i = 0; i < dmgTypes.Count; i++)
                        {
                            var dmg = dmgTypes[i];
                            if (dmg.type == Rust.DamageType.Blunt) dmg.amount = helicopter.Damage.DamageRocketsBluntDamage;
                            if (dmg.type == Rust.DamageType.Explosion) dmg.amount = helicopter.Damage.DamageRocketsExplosionDamage;
                        }
                    }
                }

                Facepunch.Pool.FreeList(ref helis);
            }
        }

        private void OnEntityKill(PatrolHelicopter patrolHelicopter)
        {
            if (patrolHelicopter == null)
                return;

            TieredPatrolHelicopterComponent tieredComponent = patrolHelicopter.GetComponent<TieredPatrolHelicopterComponent>();
            if (tieredComponent == null)
                return;

            PatrolHelicopterAI patrolHelicopterAi = patrolHelicopter.GetComponent<PatrolHelicopterAI>();
            if (patrolHelicopterAi == null)
                return;

            Helicopter patrolHelicopterConfig;
            if (!_pluginConfig.Config.Tiers.TryGetValue(tieredComponent.Tier, out patrolHelicopterConfig) || patrolHelicopterConfig == null)
                return;

            HashSet<PatrolHelicopter> spawnedPatrolHelicopters;
            if (SpawnedHelicopters.TryGetValue(tieredComponent.Tier, out spawnedPatrolHelicopters))
                spawnedPatrolHelicopters.Remove(patrolHelicopter);

            InitializeHelicopters(tieredComponent.Tier, patrolHelicopterConfig);

            // Verify the patrol helicopter was actually taken down, rather than retired or forcibly killed.
            if (!patrolHelicopterAi.isDead)
                return;

            CalculateStats(patrolHelicopter);
            CaptureAfterExplosionCrates(patrolHelicopter.transform.position);
            SpawnAndAttachFireBallsToCrates(patrolHelicopterConfig.Loot);
            SetupAndPopulateCratesLoot(tieredComponent, patrolHelicopterConfig.Loot);
        }

        #region Loot Crates Management

        private void SetupAndPopulateCratesLoot(TieredPatrolHelicopterComponent tieredComponent, LootConfig lootConfig)
        {
            foreach (LockedByEntCrate crate in _crates)
            {
                TieredCrateComponent tieredCrate = crate.gameObject.AddComponent<TieredCrateComponent>();

                tieredCrate.Invoker = tieredComponent.Invoker;
                tieredCrate.AdminBypass = lootConfig.AlwaysLootableByAdmins;
                tieredCrate.OnlyInvokerCanLoot = tieredComponent.Invoker != null && lootConfig.CanBeOpenedOnlyBySummoner;
                tieredCrate.InvokerTeamCanLoot = tieredComponent.Invoker != null && lootConfig.CanBeOpenedBySummonerTeam;

                if (lootConfig.DelayBeforeCratesBecomeLootableMinutes <= 0)
                    UnlockCrate(crate);
                else
                    tieredCrate.TimeToUnlock = DateTime.UtcNow.AddMinutes(lootConfig.DelayBeforeCratesBecomeLootableMinutes);

                if (!lootConfig.LootEnableCustomLootTable || lootConfig.CustomLootTable == null)
                    continue;

                List<ItemInfo> lootTable = lootConfig.CustomLootTable;

                int lootSpawnSlots = Random.Range(lootConfig.MinimumLootSpawnSlots, lootConfig.MaximumLootSpawnSlots + 1);
                if (lootSpawnSlots > lootTable.Count)
                    lootSpawnSlots = lootTable.Count;

                // Ensure that it doesn't exceed 12
                lootSpawnSlots = Mathf.Min(lootSpawnSlots, 12);
                crate.inventory.capacity = lootSpawnSlots;

                crate.inventory.Clear();
                var lootCrate = crate as LootContainer;
                if (lootCrate == null)
                    continue;

                lootCrate.inventory.Clear();
                var items = lootCrate.inventory.itemList.ToList();
                for (var i = 0; i < items.Count; i++)
                    items[i].DoRemove();

                // Keep track of items that have already been spawned in this crate.
                HashSet<string> spawnedItems = new HashSet<string>();

                for (int i = 0; i < lootSpawnSlots; i++)
                {
                    Rarity chosenRarity = ChooseRarity(lootConfig.RarityWeights);

                    List<ItemInfo> itemsOfChosenRarity = lootConfig.CustomLootTable.Where(ii => ii.Rarity == chosenRarity && !spawnedItems.Contains(ii.Shortname)).ToList();

                    if (itemsOfChosenRarity.Count == 0)
                        continue;

                    ItemInfo itemInfo = itemsOfChosenRarity[Random.Range(0, itemsOfChosenRarity.Count)];
                    spawnedItems.Add(itemInfo.Shortname);

                    Item item;
                    ItemDefinition itemDefinition = itemInfo.GetItemDefinition();

                    var amount = Random.Range(itemInfo.MinimumSpawnAmount, itemInfo.MaximumSpawnAmount + 1);
                    if (itemInfo.SpawnAsBlueprint)
                        item = ItemManager.CreateByItemID(BlueprintId, amount, itemInfo.SkinId);
                    else
                        item = ItemManager.CreateByName(itemInfo.Shortname, amount, itemInfo.SkinId);

                    if (item == null)
                    {
                        Puts(Lang(PluginMessages.ItemNotFound, null, itemInfo.Shortname));
                        continue;
                    }

                    if (itemInfo.SpawnAsBlueprint)
                        item.blueprintTarget = itemDefinition.itemid;
                    item.MoveToContainer(crate.inventory);
                }
            }
        }

        private Rarity ChooseRarity(Dictionary<Rarity, int> weights)
        {
            int totalWeight = weights.Sum(x => x.Value);
            int randomValue = Random.Range(0, totalWeight);
            int currentWeight = 0;

            foreach (var rarity in weights.Keys)
            {
                currentWeight += weights[rarity];
                if (randomValue < currentWeight)
                    return rarity;
            }

            return Rarity.Common; // Default fallback
        }

        private void CaptureAfterExplosionCrates(Vector3 crashSitePosition)
        {
            _crates.Clear();
            List<LockedByEntCrate> nearbyCrates = Pool.GetList<LockedByEntCrate>();
            Vis.Entities(crashSitePosition, 30f, nearbyCrates, LAYER_RAGDOLL, QueryTriggerInteraction.Ignore);

            if (nearbyCrates.Count != 0)
            {
                foreach (BaseEntity capturedEntity in nearbyCrates)
                {
                    LockedByEntCrate crate = capturedEntity as LockedByEntCrate;
                    if (crate != null && crate.PrefabName == CRATE_PREFAB)
                    {
                        _crates.Add(crate);

                        FireBall fireBall = FindChildByType<FireBall>(crate);
                        if (fireBall != null)
                            fireBall.Extinguish();
                    }
                }
            }

            Pool.FreeList(ref nearbyCrates);
        }
        
        private void SpawnAndAttachFireBallsToCrates(LootConfig lootConfig)
        {
            if (!lootConfig.BurnCrates)
                return;

            foreach (LockedByEntCrate crate in _crates)
            {
                FireBall fireBall = GameManager.server.CreateEntity(SMALL_FIREBALL_PREFAB, crate.transform.position, Quaternion.identity, false) as FireBall;
                if (!fireBall)
                    continue;

                fireBall.SetParent(crate, true, true);
                fireBall.SetActive(true);
                fireBall.Spawn();
                fireBall.GetComponent<Rigidbody>().isKinematic = true;
                fireBall.GetComponent<Collider>().enabled = false;
                crate.SendMessage("SetLockingEnt", fireBall.gameObject, SendMessageOptions.DontRequireReceiver);
                SetupFireBall(fireBall, lootConfig.FireBall);
            }
        }
        
        private void SetupFireBall(FireBall fireBall, FireBallConfig fireBallconfig)
        {
            fireBall.tickRate = fireBallconfig.DamageOccurrenceFrequencySeconds;
            fireBall.lifeTimeMin = fireBallconfig.MinimumLifetimeSeconds;
            fireBall.lifeTimeMax = fireBallconfig.MaximumLifetimeSeconds;
            fireBall.radius = fireBallconfig.BurnDamageReach;
            fireBall.damagePerSecond = fireBallconfig.BaseBurnDamageAmount;
            fireBall.waterToExtinguish = fireBallconfig.WaterAmountToExtinguish;
            fireBall.generation = (fireBallconfig.SpreadProbabilityPercentage == 0) ? 9f : (1f - (fireBallconfig.SpreadProbabilityPercentage / 100f)) / 0.1f;
            fireBall.Think();

            float lifeTime = Random.Range(fireBall.lifeTimeMax, fireBall.lifeTimeMin);
            fireBall.Invoke(() => fireBall.Extinguish(), lifeTime);

            float spreadDelay = lifeTime * (fireBallconfig.ElapsedLifetimePercentageToBeginSpreading / 100f);
            fireBall.Invoke(() => fireBall.TryToSpread(), spreadDelay);
        }

        private static T FindChildByType<T>(BaseEntity parentEntity, string prefabName = null) where T : BaseEntity
        {
            T result = null;
            foreach (BaseEntity childEntity in parentEntity.children)
            {
                T specificChild = childEntity as T;
                if (specificChild != null)
                {
                    if (prefabName == null || childEntity.PrefabName == prefabName)
                    {
                        result = specificChild;
                        break;
                    }
                }
            }

            return result;
        }

        #endregion Loot Crates Management

        private void OnEntityKill(HelicopterDebris debris)
        {
            if (debris != null)
                _debris.Remove(debris);
        }

        private void OnEntityKill(FireBall fireball)
        {
            if (fireball != null && (fireball.PrefabName.Contains("oilfireballsmall")))
                foreach (var pair in _fireBalls)
                {
                    if (pair.Value.Contains(fireball))
                    {
                        pair.Value.Remove(fireball);
                        break;
                    }
                }
        }

        private object OnEntityTakeDamage(BaseEntity entity, HitInfo hitInfo)
        {
            if (entity == null || hitInfo == null)
                return null;

            if (hitInfo.Initiator == null || entity is BasePlayer)
                return null;

            if (hitInfo.Initiator is PatrolHelicopter)
            {
                TieredPatrolHelicopterComponent tieredComponent = hitInfo.Initiator.GetComponent<TieredPatrolHelicopterComponent>();
                if (tieredComponent == null)
                    return null;

                if (tieredComponent.EnablePlayerVersusEnvironment && tieredComponent.Invoker != null)
                {
                    if (entity.OwnerID == tieredComponent.Invoker.userID)
                        return null;

                    if (tieredComponent.Invoker.Team != null && tieredComponent.Invoker.Team.members.Contains(entity.OwnerID))
                        return null;

                    hitInfo.damageTypes.Clear();
                    return true;
                }
            }
            else if (hitInfo.Initiator is FireBall)
            {
                FireBall fireBall = hitInfo.Initiator as FireBall;
                if (fireBall != null)
                {
                    bool ownerMatch = false;
                    foreach (var pair in _fireBalls)
                    {
                        if (pair.Key != null && pair.Value.Contains(fireBall))
                        {
                            if (pair.Key.userID == entity.OwnerID || (pair.Key.Team != null && pair.Key.Team.members.Contains(entity.OwnerID)))
                            {
                                ownerMatch = true;
                                break;
                            }
                        }
                    }

                    if (!ownerMatch)
                    {
                        hitInfo.damageTypes.Clear();
                        return true;
                    }
                }
            }

            return null;
        }

        private void OnFireBallSpread(FireBall motherFireBall, BaseEntity childFireBall)
        {
            if (motherFireBall == null || childFireBall == null)
                return;

            foreach (var fireBallSet in _fireBalls.Values)
            {
                if (fireBallSet.Contains(motherFireBall))
                {
                    fireBallSet.Add(childFireBall as FireBall);
                    break;
                }
            }
        }

        object OnEntityTakeDamage(PatrolHelicopter helicopter, HitInfo hitInfo)
        {
            var hitter = hitInfo?.Initiator;
            if (hitter == null)
                return null;

            var tieredComponent = helicopter.GetComponent<TieredPatrolHelicopterComponent>();
            if (tieredComponent != null)
            {
                if (hitter is BasePlayer)
                {
                    var playerInitiator = hitter.ToPlayer();
                    if (tieredComponent.Invoker != null && tieredComponent.LockToInvoker)
                    {
                        if (tieredComponent.Invoker.userID == playerInitiator.userID)
                        {
                            RegisterHeliHitInfo(helicopter, playerInitiator, hitInfo);
                        }
                        else if (tieredComponent.IncludeTeammates && tieredComponent.Invoker.currentTeam > 0 &&
                                 tieredComponent.Invoker.currentTeam == playerInitiator.currentTeam)
                        {
                            RegisterHeliHitInfo(helicopter, playerInitiator, hitInfo);
                        }
                        else
                        {
                            hitInfo.damageTypes.Clear();
                            return true;
                        }
                    }
                    else if (tieredComponent.Invoker == null)
                    {
                        Helicopter tierhelicopter;
                        if (_pluginConfig.Config.Tiers.TryGetValue(tieredComponent.Tier, out tierhelicopter) &&
                            tierhelicopter != null && tierhelicopter.PlayerVsPlayer.LockToAttacker)
                        {
                            tieredComponent.Invoker = playerInitiator;
                            Server.Broadcast(Lang(PluginMessages.Reservation, null, tieredComponent.Tier,
                                playerInitiator.displayName));
                        }

                        RegisterHeliHitInfo(helicopter, playerInitiator, hitInfo);
                    }
                }
            }

            return null;
        }

        private object OnHelicopterStrafeEnter(PatrolHelicopterAI helicopter, Vector3 strafePosition)
        {
            var tieredComponent = helicopter.GetComponent<TieredPatrolHelicopterComponent>();
            if (tieredComponent != null && tieredComponent.Invoker != null && tieredComponent.LockToInvoker)
            {
                if (Vector3.Distance(strafePosition, tieredComponent.Invoker.ServerPosition) < 50)
                {
                    return null;
                }

                if (tieredComponent.IncludeTeammates && tieredComponent.Invoker.currentTeam > 0)
                {
                    foreach (var teamMemberId in tieredComponent.Invoker.Team.members)
                    {
                        var teamMember = BasePlayer.FindByID(teamMemberId);
                        if (teamMember != null && Vector3.Distance(strafePosition, teamMember.ServerPosition) < 50)
                        {
                            return null;
                        }
                    }
                }

                return false;
            }

            return null;
        }

        private object CanHelicopterTarget(PatrolHelicopterAI helicopter, BasePlayer player)
        {
            var tieredComponent = helicopter.GetComponent<TieredPatrolHelicopterComponent>();
            if (tieredComponent != null && tieredComponent.Invoker != null && tieredComponent.LockToInvoker)
            {
                if (tieredComponent.Invoker.userID == player.userID)
                {
                    return null;
                }

                if (tieredComponent.IncludeTeammates && tieredComponent.Invoker.currentTeam > 0 &&
                    tieredComponent.Invoker.currentTeam == player.currentTeam)
                {
                    return null;
                }

                return false;
            }

            return null;
        }

        private object CanHelicopterStrafeTarget(PatrolHelicopterAI heli, BasePlayer target) =>
            CanHelicopterTarget(heli, target);

        private object OnHelicopterTarget(HelicopterTurret turret, BasePlayer player)
        {
            var tieredComponent = turret._heliAI.GetComponent<TieredPatrolHelicopterComponent>();
            if (tieredComponent != null && tieredComponent.Invoker != null && tieredComponent.LockToInvoker)
            {
                if (tieredComponent.Invoker.userID == player.userID)
                {
                    return null;
                }

                if (tieredComponent.IncludeTeammates && tieredComponent.Invoker.currentTeam > 0 &&
                    tieredComponent.Invoker.currentTeam == player.currentTeam)
                {
                    return null;
                }

                return false;
            }

            return null;
        }

        private object CanLootEntity(BasePlayer player, LockedByEntCrate crate)
        {
            if (crate.ShortPrefabName != "heli_crate")
                return null;

            var crateInfo = crate.GetComponent<TieredCrateComponent>();
            if (crateInfo == null)
                return null;

            if (crateInfo.AdminBypass && player.IsAdmin)
                return null;

            if (crateInfo.OnlyInvokerCanLoot && crateInfo.Invoker != player)
            {
                if (!crateInfo.InvokerTeamCanLoot || crateInfo.Invoker.currentTeam == 0 ||
                    crateInfo.Invoker.currentTeam != player.currentTeam)
                {
                    SendReply(player, Lang(PluginMessages.NoLootPermission, player.UserIDString));
                    return true;
                }
            }

            if (!crateInfo.TimeToUnlock.HasValue)
            {
                return null;
            }

            if (!WaterLevel.Test(crate.ServerPosition, false, false, crate) && crateInfo.TimeToUnlock.Value > DateTime.UtcNow)
            {
                player.ChatMessage(Lang(PluginMessages.LootBoxCooldown, player.UserIDString,
                    Humanize(crateInfo.TimeToUnlock.Value.Subtract(DateTime.UtcNow))));
                return true;
            }

            return null;
        }

        private void OnPlayerLootEnd(PlayerLoot inventory)
        {
            var player = inventory.GetComponent<BasePlayer>();
            TempBoxData boxData;
            if (inventory.entitySource != null && _tempBoxes.TryGetValue(player.userID, out boxData) &&
                boxData != null && inventory.entitySource is StorageContainer)
            {
                StoreTempBoxData(player, inventory.entitySource as StorageContainer, boxData.Tier);
            }
        }

        object OnHelicopterRetire(PatrolHelicopterAI entity)
        {
            if (heliHitTracker != null && heliHitTracker.ContainsKey(entity.helicopterBase))
            {
                heliHitTracker.Remove(entity.helicopterBase);
            }

            return null;
        }

        private void Unload()
        {
            foreach (var spawnedHelicopter in SpawnedHelicopters)
            {
                var destroyedCount = DestroySpawnedTier(spawnedHelicopter.Key, spawnedHelicopter.Value.ToList());
                SendEchoConsole(null, Lang(PluginMessages.HelicoptersDestroyedConsoleMessage, null, string.Empty, destroyedCount, spawnedHelicopter.Key));
            }

            _fireBalls.Clear();
            SpawnedHelicopters.Clear();
            _crates.Clear();
            _instance = null;
        }

        #endregion Hooks

        #region Commands

        // heli.help
        [ChatCommand(Commands.Help)]
        void HelicopterHelpChatCommand(BasePlayer player, string cmd, string[] args)
        {
            SendReply(player, GetCommands(player));
        }

        // heli.call <Tier>
        // heli.call <Tier> here
        [ChatCommand(Commands.Call)]
        void CallHelicopterTier(BasePlayer player, string cmd, string[] args)
        {
            if (!_pluginConfig.Config.IsEnabled)
            {
                SendReply(player, Lang(PluginMessages.PluginDisabled, player.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                SendReply(player, Lang(PluginMessages.TierNameRequired, player.UserIDString));
                return;
            }

            var tierSelected = args[0];
            if (!_pluginConfig.Config.Tiers.ContainsKey(tierSelected))
            {
                SendReply(player, Lang(PluginMessages.TierNotFound, player.UserIDString));
                return;
            }

            var userPermissions = permission.GetUserPermissions(player.UserIDString);
            var permissionName = GetTierSpawnPermissionName(tierSelected);
            if (!userPermissions.Contains(permissionName) &&
                !userPermissions.Contains(AllTierSpawnPermissionName) &&
                !userPermissions.Contains(CustomHelicopterTiersAdmin))
            {
                SendReply(player,
                    player.IsAdmin
                        ? Lang(PluginMessages.TierNoCallPermissionAdmin, player.UserIDString, permissionName)
                        : Lang(PluginMessages.TierNoCallPermission, player.UserIDString));
                return;
            }

            var helicopter = _pluginConfig.Config.Tiers[tierSelected];
            if (!helicopter.Enabled)
            {
                SendReply(player, Lang(PluginMessages.HelicopterTierDisabled, player.UserIDString));
                return;
            }

            if (!PlayerCanSpawnTier(player, tierSelected, helicopter))
                return;
            var location = Vector3.zero;
            if (args.Length > 1 && args[1] == "here")
            {
                location = player.transform.position;
            }
            else
            {
                if (helicopter.Spawn.SpawnCustomEnabled)
                {
                    List<string> validSpawns = GetValidSpawnPointsIncludingMonuments(helicopter);
                    var spawnLocationName = validSpawns?.GetRandom();
                    if (!string.IsNullOrWhiteSpace(spawnLocationName))
                    {
                        location = GetSpawnPointLocationIncludingMonuments(spawnLocationName);
                    }
                }
            }

            CallHelicopterToPlayer(tierSelected, location, player, player);
        }

        // heli.kill <Tier>
        // heli.kill all
        [ChatCommand(Commands.Kill)]
        void KillHeli(BasePlayer player, string cmd, string[] args)
        {
            if (!_pluginConfig.Config.IsEnabled)
            {
                SendReply(player, Lang(PluginMessages.PluginDisabled, player.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                SendReply(player, Lang(PluginMessages.InvalidArgumentForKillCommand, player.UserIDString));
                return;
            }

            var tierSelected = args[0].ToLower();
            if (tierSelected != "all" && !_pluginConfig.Config.Tiers.ContainsKey(tierSelected))
            {
                SendReply(player, Lang(PluginMessages.TierNotFound, player.UserIDString));
                return;
            }

            var userHasAllAdminPermission =
                permission.UserHasPermission(player.UserIDString, CustomHelicopterTiersAdmin);
            if (!userHasAllAdminPermission)
            {
                SendReply(player,
                    player.IsAdmin
                        ? Lang(PluginMessages.TierNoKillPermissionAdmin, player.UserIDString,
                            CustomHelicopterTiersAdmin)
                        : Lang(PluginMessages.TierNoKillPermission, player.UserIDString));
                return;
            }

            if (SpawnedHelicopters.Count == 0)
            {
                SendReply(player, Lang(PluginMessages.NoHelicoptersToDestroy, player.UserIDString));
                return;
            }

            foreach (var spawnedHelicopter in SpawnedHelicopters)
            {
                if (tierSelected != "all" && !tierSelected.Equals(spawnedHelicopter.Key.ToLower()))
                {
                    continue;
                }

                var destroyedCount = DestroySpawnedTier(spawnedHelicopter.Key, spawnedHelicopter.Value.ToList());
                SendReply(player,
                    Lang(PluginMessages.HelicoptersDestroyedChatMessage, player.UserIDString, destroyedCount,
                        spawnedHelicopter.Key));
                Puts(Lang(PluginMessages.HelicoptersDestroyedConsoleMessage, player.UserIDString, player.displayName,
                    destroyedCount, spawnedHelicopter.Key));
            }
        }

        // heli.spawn set <SpawnPointName> <here|Coordinates>
        // heli.spawn delete <SpawnPointName>
        // heli.spawn show
        [ChatCommand(Commands.Spawn)]
        void SpawnHelicopterChatCommand(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, CustomHelicopterTiersAdmin))
            {
                SendReply(player, Lang(PluginMessages.NoSpawnPermission, player.UserIDString));
                return;
            }

            if (!_pluginConfig.Config.IsEnabled)
            {
                SendReply(player, Lang(PluginMessages.PluginDisabled, player.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                SendReply(player, Lang(PluginMessages.WrongCommand, player.UserIDString, Commands.Help));
                return;
            }

            switch (args[0])
            {
                case "set":
                    {
                        if (args.Length < 2)
                        {
                            SendReply(player, Lang(PluginMessages.SpawnNameRequired, player.UserIDString));
                            return;
                        }

                        var spawnLocation = player.ServerPosition;
                        if (args.Length > 2)
                        {
                            if (args[2].Equals("here", StringComparison.OrdinalIgnoreCase))
                            {
                                spawnLocation = player.ServerPosition;
                            }
                            else
                            {
                                var xString = "";
                                var yString = "";
                                var zString = "";
                                if (args.Length == 3)
                                {
                                    var coordinates = args[2].Split(',');
                                    if (coordinates.Length != 3)
                                    {
                                        SendReply(player, Lang(PluginMessages.InvalidCoordinates, player.UserIDString));
                                        return;
                                    }

                                    xString = coordinates[0];
                                    yString = coordinates[1];
                                    zString = coordinates[2];
                                }
                                else if (args.Length != 5)
                                {
                                    SendReply(player, Lang(PluginMessages.InvalidCoordinates, player.UserIDString));
                                    return;
                                }
                                else
                                {
                                    xString = args[2];
                                    yString = args[3];
                                    zString = args[4];
                                }

                                float x, y, z;
                                if (!float.TryParse(xString, out x))
                                {
                                    SendReply(player, Lang(PluginMessages.InvalidCoordinates, player.UserIDString));
                                    return;
                                }

                                if (!float.TryParse(yString, out y))
                                {
                                    SendReply(player, Lang(PluginMessages.InvalidCoordinates, player.UserIDString));
                                    return;
                                }

                                if (!float.TryParse(zString, out z))
                                {
                                    SendReply(player, Lang(PluginMessages.InvalidCoordinates, player.UserIDString));
                                    return;
                                }

                                spawnLocation = new Vector3(x, y, z);
                            }
                        }

                        if (_pluginConfig.Config.SpawnLocations == null)
                            _pluginConfig.Config.SpawnLocations =
                                new Dictionary<string, SpawnLocationData>(StringComparer.InvariantCultureIgnoreCase);

                        _pluginConfig.Config.SpawnLocations[args[1]] = new SpawnLocationData { Position = spawnLocation };
                        ConfigSave();
                        player.SendConsoleCommand("ddraw.text", 30, Color.green, spawnLocation + new Vector3(0, 1.5f, 0),
                            $"<size=40>{args[1]}</size>");
                        player.SendConsoleCommand("ddraw.sphere", 30, Color.green, spawnLocation, 1f);
                        SendReply(player, Lang(PluginMessages.SpawnPositionSet, player.UserIDString));
                        break;
                    }
                case "show": //show <duration> or //show
                    {
                        var time = 30;
                        if (args.Length == 2)
                        {
                            var timeString = args[1];
                            if (!string.IsNullOrWhiteSpace(timeString))
                            {
                                if (!int.TryParse(timeString, out time))
                                    time = 30;
                            }
                        }

                        foreach (var spawnPointInfo in _pluginConfig.Config.SpawnLocations)
                        {
                            player.SendConsoleCommand("ddraw.text", time, Color.green,
                                spawnPointInfo.Value?.Position + new Vector3(0, 1.5f, 0),
                                $"<size=40>{spawnPointInfo.Key}</size>");
                            player.SendConsoleCommand("ddraw.sphere", time, Color.green, spawnPointInfo.Value, 1f);
                        }

                        foreach (var spawnPointInfo in _pluginConfig.Config.MonumentSpawnLocations)
                        {
                            player.SendConsoleCommand("ddraw.text", time, Color.green,
                                spawnPointInfo.Value?.Position + new Vector3(0, 1.5f, 0),
                                $"<size=40>{spawnPointInfo.Key}</size>");
                            player.SendConsoleCommand("ddraw.sphere", time, Color.green, spawnPointInfo.Value, 1f);
                        }

                        break;
                    }
                case "delete":
                    {
                        if (args.Length < 2)
                        {
                            SendReply(player, Lang(PluginMessages.SpawnNameRequired, player.UserIDString));
                            return;
                        }

                        if (_pluginConfig.Config.SpawnLocations == null &&
                            _pluginConfig.Config.MonumentSpawnLocations == null)
                        {
                            SendReply(player, Lang(PluginMessages.SpawnPositionNotFound, player.UserIDString));
                            return;
                        }

                        if (_pluginConfig.Config.SpawnLocations != null &&
                            (_pluginConfig.Config.SpawnLocations.Remove(args[1]) ||
                             _pluginConfig.Config.MonumentSpawnLocations.Remove(args[1])))
                        {
                            SendReply(player, Lang(PluginMessages.SpawnPositionDeleted, player.UserIDString));
                            ConfigSave();
                        }
                        else
                            SendReply(player, Lang(PluginMessages.SpawnPositionNotFound, player.UserIDString));

                        break;
                    }
                default:
                    SendReply(player, Lang(PluginMessages.WrongCommand, player.UserIDString, Commands.Help));
                    break;
            }
        }

        // heli.loot set <Tier>
        [ChatCommand(Commands.Loot)]
        void HelicopterLootBoxChatCommand(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, CustomHelicopterTiersAdmin))
            {
                SendReply(player, Lang(PluginMessages.NoEditPermission, player.UserIDString));
                return;
            }

            if (!_pluginConfig.Config.IsEnabled)
            {
                SendReply(player, Lang(PluginMessages.PluginDisabled, player.UserIDString));
                return;
            }

            if (args.Length <= 1 || !args[0].Equals("set", StringComparison.OrdinalIgnoreCase))
            {
                SendReply(player, Lang(PluginMessages.WrongCommand, player.UserIDString, Commands.Help));
                return;
            }

            var tierSelected = args[1].ToLower();
            if (!_pluginConfig.Config.Tiers.ContainsKey(tierSelected))
            {
                SendReply(player, Lang(PluginMessages.TierNotFound, player.UserIDString));
                return;
            }

            CreateTempBox(player, tierSelected);
        }

        // heli.add <Tier>
        [ChatCommand(Commands.Add)]
        void AddHelicopterChatCommand(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, CustomHelicopterTiersAdmin))
            {
                SendReply(player, Lang(PluginMessages.NoSpawnPermission, player.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                SendReply(player, Lang(PluginMessages.WrongCommand, player.UserIDString, Commands.Help));
                return;
            }

            var tier = args[0];
            if (_pluginConfig.Config.Tiers.ContainsKey(tier))
            {
                SendReply(player, Lang(PluginMessages.TierNameExist, player.UserIDString));
                return;
            }

            _pluginConfig.Config.Tiers[tier] = GetDefaultHelicopter();
            ConfigSave();
            SendReply(player, Lang(PluginMessages.TierAdded, player.UserIDString));
        }

        // heli.delete <Tier>
        [ChatCommand(Commands.Delete)]
        void DeleteHelicopterChatCommand(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, CustomHelicopterTiersAdmin))
            {
                SendReply(player, Lang(PluginMessages.NoSpawnPermission, player.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                SendReply(player, Lang(PluginMessages.WrongCommand, player.UserIDString, Commands.Help));
                return;
            }

            var tier = args[0];
            if (!_pluginConfig.Config.Tiers.ContainsKey(tier))
            {
                SendReply(player, Lang(PluginMessages.TierNotFound, player.UserIDString));
                return;
            }

            if (_pluginConfig.Config.Tiers.Remove(tier))
            {
                ConfigSave();
                SendReply(player, Lang(PluginMessages.TierRemoved, player.UserIDString));
            }
        }

        // heli.list
        [ChatCommand(Commands.List)]
        void ListHelicopterChatCommand(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, CustomHelicopterTiersAdmin))
            {
                SendReply(player, Lang(PluginMessages.NoSpawnPermission, player.UserIDString));
                return;
            }

            if (_pluginConfig.Config.Tiers.Count == 0)
            {
                SendEchoConsole(player, Lang(PluginMessages.NoTiersFound, player != null ? player.UserIDString : null));
            }

            var sb = new StringBuilder();
            sb.AppendLine(Lang(PluginMessages.HelicopterListTitle, player != null ? player.UserIDString : null));
            foreach (var tier in _pluginConfig.Config.Tiers)
            {
                sb.AppendLine(tier.Key);
            }

            SendReply(player, sb.ToString());
        }


        // heli.call <Tier>
        // heli.call <Tier> <SteamID>
        [ConsoleCommand(Commands.Call)]
        void CallHelicopter(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
                return;
            var inGameConsoleCaller = conArgs.Player();
            var playerIdIsFirstArg = false;
            BasePlayer targetPlayer = null;
            if (inGameConsoleCaller == null)
            {
                targetPlayer = conArgs.GetPlayer(0);
                if (targetPlayer != null)
                {
                    playerIdIsFirstArg = true;
                }
                else
                {
                    targetPlayer = conArgs.GetPlayer(1);
                }
            }

            if (!_pluginConfig.Config.IsEnabled)
            {
                if (inGameConsoleCaller != null)
                    SendReply(inGameConsoleCaller,
                        Lang(PluginMessages.PluginDisabled, inGameConsoleCaller.UserIDString));
                else
                    SendEchoConsole(inGameConsoleCaller, Lang(PluginMessages.PluginDisabled));
                return;
            }

            if (conArgs.Args == null || conArgs.Args.Length == 0)
            {
                if (inGameConsoleCaller != null)
                    SendReply(inGameConsoleCaller,
                        Lang(PluginMessages.TierNameRequired, inGameConsoleCaller.UserIDString));
                else
                    SendEchoConsole(inGameConsoleCaller, Lang(PluginMessages.TierNameRequired));
                return;
            }

            var tierSelected = conArgs.GetString(playerIdIsFirstArg ? 1 : 0);
            if (!_pluginConfig.Config.Tiers.ContainsKey(tierSelected))
            {
                if (inGameConsoleCaller != null)
                    SendReply(inGameConsoleCaller, Lang(PluginMessages.TierNotFound, inGameConsoleCaller.UserIDString));
                else
                    SendEchoConsole(inGameConsoleCaller, Lang(PluginMessages.TierNotFound));
                return;
            }

            if (inGameConsoleCaller != null)
            {
                var userPermissions = permission.GetUserPermissions(inGameConsoleCaller.UserIDString);
                var permissionName = GetTierSpawnPermissionName(tierSelected);
                if (!userPermissions.Contains(permissionName) && !userPermissions.Contains(AllTierSpawnPermissionName))
                {
                    SendReply(inGameConsoleCaller,
                        inGameConsoleCaller.IsAdmin
                            ? Lang(PluginMessages.TierNoCallPermissionAdmin, inGameConsoleCaller.UserIDString,
                                permissionName)
                            : Lang(PluginMessages.TierNoCallPermission, inGameConsoleCaller.UserIDString));
                    return;
                }
            }

            var helicopter = _pluginConfig.Config.Tiers[tierSelected];
            if (!helicopter.Enabled)
            {
                if (inGameConsoleCaller != null)
                    SendReply(inGameConsoleCaller,
                        Lang(PluginMessages.HelicopterTierDisabled, inGameConsoleCaller.UserIDString));
                else
                    SendEchoConsole(inGameConsoleCaller, Lang(PluginMessages.HelicopterTierDisabled));
                return;
            }

            if (inGameConsoleCaller != null && !PlayerCanSpawnTier(inGameConsoleCaller, tierSelected, helicopter))
                return;

            var position = Vector3.zero;

            if (targetPlayer == null)
            {
                var playerId = conArgs.GetString(1);
                if (!string.IsNullOrWhiteSpace(playerId))
                {
                    if (playerId.IsSteamId())
                    {
                        targetPlayer = BasePlayer.Find(playerId);
                        if (targetPlayer == null)
                        {
                            Puts(Lang(PluginMessages.PlayerNotFound));
                            return;
                        }
                    }
                    else
                    {
                        Puts(Lang(PluginMessages.PlayerNotFound));
                        return;
                    }
                }
            }

            if (targetPlayer != null)
            {
                position = targetPlayer.ServerPosition;
            }

            if (position == Vector3.zero)
            {
                if (helicopter.Spawn.SpawnCustomEnabled)
                {
                    var validSpawns = GetValidSpawnPointsIncludingMonuments(helicopter);
                    var spawnLocationName = validSpawns?.GetRandom();
                    if (!string.IsNullOrWhiteSpace(spawnLocationName))
                    {
                        position = GetSpawnPointLocationIncludingMonuments(spawnLocationName);
                    }
                }
            }

            CallHelicopterToPlayer(tierSelected, position, inGameConsoleCaller, targetPlayer);
        }

        // heli.kill <Tier>
        // heli.kill all
        [ConsoleCommand(Commands.Kill)]
        void KillHelicopter(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
                return;

            var player = conArgs.Player();
            if (!_pluginConfig.Config.IsEnabled)
            {
                if (player != null)
                    SendReply(player, Lang(PluginMessages.PluginDisabled, player.UserIDString));
                else
                    SendEchoConsole(player, Lang(PluginMessages.PluginDisabled));
                return;
            }

            if (conArgs.Args == null || conArgs.Args.Length == 0)
            {
                if (player != null)
                    SendReply(player, Lang(PluginMessages.InvalidArgumentForKillCommand, player.UserIDString));
                else
                    SendEchoConsole(player, Lang(PluginMessages.InvalidArgumentForKillCommand));
                return;
            }

            var tierSelected = conArgs.Args[0].ToLower();
            if (tierSelected != "all" && !_pluginConfig.Config.Tiers.ContainsKey(tierSelected))
            {
                if (player != null)
                    SendReply(player, Lang(PluginMessages.TierNotFound, player.UserIDString));
                else
                    SendEchoConsole(player, Lang(PluginMessages.TierNotFound));
                return;
            }

            if (player != null && !permission.UserHasPermission(player.UserIDString, CustomHelicopterTiersAdmin))
            {
                SendReply(player,
                    player.IsAdmin
                        ? Lang(PluginMessages.TierNoKillPermissionAdmin, player.UserIDString,
                            CustomHelicopterTiersAdmin)
                        : Lang(PluginMessages.TierNoKillPermission, player.UserIDString));
                return;
            }

            if (SpawnedHelicopters.Count == 0)
            {
                if (player != null)
                    SendReply(player, Lang(PluginMessages.NoHelicoptersToDestroy, player.UserIDString));
                else
                    SendEchoConsole(player, Lang(PluginMessages.NoHelicoptersToDestroy));
                return;
            }

            foreach (var spawnedHelicopter in SpawnedHelicopters)
            {
                if (tierSelected != "all" && !tierSelected.Equals(spawnedHelicopter.Key.ToLower()))
                {
                    continue;
                }

                var destroyedCount = DestroySpawnedTier(spawnedHelicopter.Key, spawnedHelicopter.Value.ToList());
                if (player != null)
                {
                    SendReply(player,
                        Lang(PluginMessages.HelicoptersDestroyedChatMessage, player.UserIDString, destroyedCount,
                            spawnedHelicopter.Key));
                    Puts(Lang(PluginMessages.HelicoptersDestroyedConsoleMessage, null, player.displayName,
                        destroyedCount, spawnedHelicopter.Key));
                }
                else
                {
                    SendEchoConsole(player,
                        Lang(PluginMessages.HelicoptersDestroyedConsoleMessage, null, string.Empty, destroyedCount,
                            spawnedHelicopter.Key));
                }
            }
        }

        //heli.report
        [ConsoleCommand(Commands.Report)]
        void ListHelicopters(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
                return;

            var player = conArgs.Player();
            if (player != null && !player.IsAdmin)
                return;

            if (_pluginConfig.Config.Tiers.Count == 0)
            {
                SendEchoConsole(player, Lang(PluginMessages.NoTiersFound, player != null ? player.UserIDString : null));
            }

            var sb = new StringBuilder();
            var spacing = "        ";
            sb.AppendLine();
            sb.AppendLine("General Report");
            sb.AppendLine($"{spacing}Config Version: {_pluginConfig.Config.Version}");

            int totalHelicopters = SpawnedHelicopters.Values.Sum(hashSet => hashSet?.Count ?? 0);
            sb.AppendLine($"{spacing}Active Helicopters: {totalHelicopters}");
            sb.AppendLine(
                $"{spacing}Vanilla Helicopter: {!_pluginConfig.Config.GlobalHelicopterConfig.DisableRustDefaultHelicopter}");
            sb.AppendLine($"{spacing}Custom Spawn Locations: {_pluginConfig.Config.SpawnLocations.Count}");
            sb.AppendLine($"{spacing}Monuments Spawn Locations: {_pluginConfig.Config.MonumentSpawnLocations.Count}");
            foreach (var tier in _pluginConfig.Config.Tiers)
            {
                sb.AppendLine($"{tier.Key}");
                if (tier.Value == null)
                {
                    sb.AppendLine(Lang(PluginMessages.HelicopterListNoData, player != null ? player.UserIDString : null,
                        tier.Key));
                    continue;
                }

                var helicopterConfig = tier.Value;
                HashSet<PatrolHelicopter> helicopters;
                SpawnedHelicopters.TryGetValue(tier.Key, out helicopters);
                sb.AppendLine($"{spacing}Enabled: {helicopterConfig.Enabled}");
                sb.AppendLine(
                    $"{spacing}Current Active: {helicopters?.Count ?? 0} / {helicopterConfig.SpawnMaxActiveHelicopters}");
                sb.AppendLine($"{spacing}Spawn Chance: {helicopterConfig.Spawn.SpawnChance:N0}");
                sb.AppendLine($"{spacing}Custom Spawn: {helicopterConfig.Spawn.SpawnCustomEnabled}");
                sb.AppendLine($"{spacing}Randomized Spawn: {helicopterConfig.Spawn.SpawnRandomizedEnabled}");
                sb.AppendLine(
                    $"{spacing}Spawn Time: Min {helicopterConfig.Spawn.SpawnMinimumTime}, Max {helicopterConfig.Spawn.SpawnMaximumTime}");
                sb.AppendLine(
                    $"{spacing}Health: B {helicopterConfig.Health.HealthBodyHealth}, MR {helicopterConfig.Health.HealthMainRotorHealth}, TR {helicopterConfig.Health.HealthTailRotorHealth}");
                sb.AppendLine($"{spacing}Custom Loot: {helicopterConfig.Loot.LootEnableCustomLootTable}");
            }

            SendEchoConsole(player, sb.ToString());
        }

        #endregion Commands

        #region Functions

        private string GetCommands(BasePlayer player)
        {
            var commands = new StringBuilder(Environment.NewLine);
            commands.AppendLine("<color=#eb9534>Available Chat Commands:</color>");
            commands.AppendLine(
                $"<color=#5af>{Commands.Call} <Tier></color> Spawns the helicopter from a random location");
            commands.AppendLine(
                $"<color=#5af>{Commands.Call} <Tier> here</color> Calls the helicopter to your location");
            if (permission.UserHasPermission(player.UserIDString, CustomHelicopterTiersAdmin))
            {
                commands.AppendLine($"<color=#5af>{Commands.Kill} <Tier></color> Kills the helicopter");
                commands.AppendLine($"<color=#5af>{Commands.Kill} all</color> Kills all active helicopters on the map");
                commands.AppendLine(
                    $"<color=#5af>{Commands.Loot} set <Tier></color> Sets the loot table of the helicopter");
                commands.AppendLine(
                    $"<color=#5af>{Commands.Spawn} set <Spawn Point Name> <Coordinates></color> Sets a custom spawn point based on the coordinates");
                commands.AppendLine(
                    $"<color=#5af>{Commands.Spawn} set <Spawn Point Name> here</color> Sets a custom spawn point at your location");
                commands.AppendLine(
                    $"<color=#5af>{Commands.Spawn} delete <Spawn Point Name></color> Deletes a custom spawn point");
                commands.AppendLine(
                    $"<color=#5af>{Commands.Spawn} show</color> Shows all created custom spawn points for 30 seconds");
                commands.AppendLine(
                    $"<color=#5af>{Commands.Spawn} show <duration in seconds></color> Shows all created custom spawn points for x seconds");
                commands.AppendLine($"<color=#5af>{Commands.Delete} <Tier></color> Deletes a helicopter");
                commands.AppendLine($"<color=#5af>{Commands.Add} <Tier></color> Adds a helicopter");
                commands.AppendLine($"<color=#5af>{Commands.List}</color> Lists all created helicopters");

                commands.AppendLine();
                commands.AppendLine("<color=#eb9534>Available Console Commands:</color>");
                commands.AppendLine(
                    $"<color=#5af>{Commands.Call} <Tier></color> Spawns the helicopter from a random location");
                commands.AppendLine(
                    $"<color=#5af>{Commands.Call} <Tier> <Steam ID></color> Calls the helicopter to the player's location");
                commands.AppendLine($"<color=#5af>{Commands.Kill} <Tier></color> Kills the helicopter");
                commands.AppendLine($"<color=#5af>{Commands.Kill} all</color> Kills all active helicopters on the map");
                commands.AppendLine($"<color=#5af>{Commands.Report}</color> Prints a report of the helicopters");
            }

            return commands.ToString();
        }

        public Helicopter GetDefaultHelicopter()
        {
            return new Helicopter
            {
                Enabled = true,
                SpawnMaxActiveHelicopters = 1,
                PlayerVsPlayer = new PlayerVersusPlayer
                {
                    LockToAttacker = false,
                    InvokerHasPriority = true,
                    LockToInvoker = true,
                },
                Damage = new Damage
                {
                    DamageRocketsBluntDamage = 175f,
                    DamageRocketsExplosionDamage = 100f,
                    DamageRocketsExplosionRadius = 6f,
                    DamageRocketsMaxLaunchedRockets = 12,
                    DamageRocketsTimeBetweenEachRocket = 0.2f,
                    DamageTurretsBulletDamage = 20f,
                    DamageTurretsBulletRange = 300f,
                    DamageTurretsBulletSpeed = 250,
                    DamageTurretsIntervalBetweenBursts = 3f,
                    DamageTurretsDurationOfBurst = 3f,
                    DamageTurretsFireRate = 0.125f,
                },
                Health = new Health
                {
                    HealthBodyHealth = 10000,
                    HealthMainRotorHealth = 750,
                    HealthTailRotorHealth = 375
                },
                Speed = new Speed
                {
                    MiscHelicopterSpeed = 25,
                    MiscHelicopterStartupLength = 0,
                    MiscHelicopterStartupSpeed = 25
                },
                Debris = new DebrisConfig
                {
                    SpawnAfterCrash = true,
                    HarvestableHitPoints = 500f,
                    CoolingPeriodSeconds = 480f,
                },
                Loot = new LootConfig
                {
                    CanBeOpenedOnlyBySummoner = false,
                    CanBeOpenedBySummonerTeam = false,
                    AlwaysLootableByAdmins = true,
                    MaximumCratesToSpawn = 4,
                    BurnCrates = true,
                    FireBall = new FireBallConfig
                    {
                        MinimumLifetimeSeconds = 20f,
                        MaximumLifetimeSeconds = 40f,
                        SpreadProbabilityPercentage = 50,
                        ElapsedLifetimePercentageToBeginSpreading = 50,
                        BurnDamageReach = 0.5f,
                        BaseBurnDamageAmount = 2f,
                        DamageOccurrenceFrequencySeconds = 0.5f,
                        WaterAmountToExtinguish = 200,
                    },
                    DelayBeforeCratesBecomeLootableMinutes = 10,
                    MinimumLootSpawnSlots = 2,
                    MaximumLootSpawnSlots = 3,
                    LootEnableCustomLootTable = true,
                    RarityWeights = new Dictionary<Rarity, int>()
                    {
                        { Rarity.Common, 70 },
                        { Rarity.Uncommon, 30 },
                        { Rarity.Rare, 3 },
                        { Rarity.VeryRare, 1 }
                    },
                    CustomLootTable = new List<ItemInfo>
                    {
                        new ItemInfo
                        {
                            Shortname = "ammo.rocket.basic",
                            MinimumSpawnAmount = 3,
                            MaximumSpawnAmount = 3,
                            SpawnAsBlueprint = false,
                            SkinId = 0,
                            Rarity = Rarity.Common
                        },
                        new ItemInfo
                        {
                            Shortname = "ammo.rocket.fire",
                            MinimumSpawnAmount = 3,
                            MaximumSpawnAmount = 5,
                            SpawnAsBlueprint = false,
                            SkinId = 0,
                            Rarity = Rarity.Common
                        },
                        new ItemInfo
                        {
                            Shortname = "ammo.rocket.hv",
                            MinimumSpawnAmount = 2,
                            MaximumSpawnAmount = 3,
                            SpawnAsBlueprint = false,
                            SkinId = 0,
                            Rarity = Rarity.Common
                        },
                        new ItemInfo
                        {
                            Shortname = "ammo.rifle",
                            MinimumSpawnAmount = 120,
                            MaximumSpawnAmount = 120,
                            SpawnAsBlueprint = false,
                            SkinId = 0,
                            Rarity = Rarity.Common
                        },
                        new ItemInfo
                        {
                            Shortname = "ammo.rifle.hv",
                            MinimumSpawnAmount = 40,
                            MaximumSpawnAmount = 40,
                            SpawnAsBlueprint = false,
                            SkinId = 0,
                            Rarity = Rarity.Common
                        },
                        new ItemInfo
                        {
                            Shortname = "ammo.rifle.incendiary",
                            MinimumSpawnAmount = 60,
                            MaximumSpawnAmount = 60,
                            SpawnAsBlueprint = false,
                            SkinId = 0,
                            Rarity = Rarity.Common
                        },
                        new ItemInfo
                        {
                            Shortname = "explosive.timed",
                            MinimumSpawnAmount = 1,
                            MaximumSpawnAmount = 2,
                            SpawnAsBlueprint = false,
                            SkinId = 0,
                            Rarity = Rarity.Uncommon
                        },
                        new ItemInfo
                        {
                            Shortname = "ammo.rifle.explosive",
                            MinimumSpawnAmount = 30,
                            MaximumSpawnAmount = 30,
                            SpawnAsBlueprint = false,
                            SkinId = 0,
                            Rarity = Rarity.Uncommon
                        },
                        new ItemInfo
                        {
                            Shortname = "metal.plate.torso",
                            MinimumSpawnAmount = 1,
                            MaximumSpawnAmount = 1,
                            SpawnAsBlueprint = false,
                            SkinId = 0,
                            Rarity = Rarity.Rare
                        },
                        new ItemInfo
                        {
                            Shortname = "shotgun.spas12",
                            MinimumSpawnAmount = 1,
                            MaximumSpawnAmount = 1,
                            SpawnAsBlueprint = false,
                            SkinId = 0,
                            Rarity = Rarity.Rare
                        },
                        new ItemInfo
                        {
                            Shortname = "lmg.m249",
                            MinimumSpawnAmount = 1,
                            MaximumSpawnAmount = 1,
                            SpawnAsBlueprint = false,
                            SkinId = 0,
                            Rarity = Rarity.VeryRare
                        },
                        new ItemInfo
                        {
                            Shortname = "door.double.hinged.toptier",
                            MinimumSpawnAmount = 1,
                            MaximumSpawnAmount = 1,
                            SpawnAsBlueprint = false,
                            SkinId = 0,
                            Rarity = Rarity.VeryRare
                        }
                    }
                },
                Spawn = new Spawn
                {
                    SpawnRandomizedEnabled = true,
                    SpawnCustomEnabled = false,
                    SpawnCustomLocationNames = new List<string> { "Airfield" },
                    SpawnMinimumTime = 60,
                    SpawnMaximumTime = 120
                },
                KillRewards = new KillReward
                {
                    KillRewardPoints = 1000,
                    KillEconomics = 1000
                },
                CallCommand = new CallCommand
                {
                    DefaultCooldown = 0,
                    DefaultDailyLimit = 0,
                    CommandLimits = new Dictionary<string, CommandLimit>(StringComparer.InvariantCultureIgnoreCase)
                    {
                        {"VIP", new CommandLimit {Cooldown = 60, DailyLimit = 3}}
                    }
                }
            };
        }

        public Dictionary<string, Helicopter> GetDefaultTiers()
        {
            return new Dictionary<string, Helicopter>(StringComparer.InvariantCultureIgnoreCase)
            {
                {"Regular", GetDefaultHelicopter()},
                {"Military", GetDefaultHelicopter()},
                {"Elite", GetDefaultHelicopter()}
            };
        }

        private Vector3 GetSpawnPointLocationIncludingMonuments(string spawnLocationName)
        {
            if (_pluginConfig.Config.SpawnLocations.ContainsKey(spawnLocationName))
                return _pluginConfig.Config.SpawnLocations[spawnLocationName].Position;
            else if (_pluginConfig.Config.MonumentSpawnLocations.ContainsKey(spawnLocationName))
                return _pluginConfig.Config.MonumentSpawnLocations[spawnLocationName].Position;
            return Vector3.zero;
        }

        private List<string> GetValidSpawnPointsIncludingMonuments(Helicopter helicopter)
        {
            var customSpawns = helicopter.Spawn.SpawnCustomLocationNames?.Where(x =>
                (_pluginConfig.Config.SpawnLocations != null && _pluginConfig.Config.SpawnLocations.ContainsKey(x))
                || _pluginConfig.Config.MonumentSpawnLocations != null &&
                _pluginConfig.Config.MonumentSpawnLocations.ContainsKey(x)).ToList();
            return customSpawns;
        }

        private void GetMonumentsPositions()
        {
            if (TerrainMeta.Path == null || TerrainMeta.Path.Monuments == null)
                return;
            var monuments = TerrainMeta.Path.Monuments.Where(x => x.shouldDisplayOnMap).ToList();
            if (monuments.Any())
            {
                _pluginConfig.Config.MonumentSpawnLocations =
                    new Dictionary<string, SpawnLocationData>(StringComparer.InvariantCultureIgnoreCase);
                foreach (var monumentInfo in monuments)
                {
                    var name = monumentInfo.displayPhrase.english.Replace("\n", string.Empty);
                    var tempName = name;
                    var counter = 1;
                    while (_pluginConfig.Config.MonumentSpawnLocations.ContainsKey(tempName))
                    {
                        tempName = $"{name}_{counter}";
                        counter++;
                    }

                    _pluginConfig.Config.MonumentSpawnLocations.Add(tempName, new SpawnLocationData
                    {
                        Position = monumentInfo.transform.position,
                    });
                }

                ConfigSave();
            }
        }

        private void SendEchoConsole(BasePlayer player, string msg)
        {
            if (player != null && player.net?.connection != null && Network.Net.sv.IsConnected())
            {
                NetWrite netWrite = Network.Net.sv.StartWrite();
                netWrite.PacketID(Network.Message.Type.ConsoleMessage);
                netWrite.String(msg);
                netWrite.Send(new SendInfo(player.net.connection));
            }
            else
            {
                Puts(msg);
            }
        }

        private string Humanize(TimeSpan timeSpan, string playerId = null)
        {
            var dayString = Lang(PluginMessages.Days, playerId);
            var hourString = Lang(PluginMessages.Hours, playerId);
            var minuteString = Lang(PluginMessages.Minutes, playerId);
            var secondString = Lang(PluginMessages.Seconds, playerId);
            var days = timeSpan.TotalDays >= 1 ? $"{(int)timeSpan.TotalDays} {dayString} " : string.Empty;
            var hours = timeSpan.TotalHours >= 24 ? $"{(int)timeSpan.Hours} {hourString} " :
                timeSpan.TotalHours >= 1 ? $"{(int)timeSpan.TotalHours} {hourString} " : string.Empty;
            var minutes = timeSpan.TotalMinutes >= 60 ? $"{timeSpan.Minutes} {minuteString} " :
                timeSpan.TotalMinutes >= 1 ? $"{(int)timeSpan.TotalMinutes} {minuteString} " : string.Empty;
            var seconds = timeSpan.TotalSeconds >= 60 ? $"{timeSpan.Seconds} {secondString}" :
                timeSpan.TotalSeconds >= 1 ? $"{(int)timeSpan.TotalSeconds} {secondString}" : string.Empty;
            return $"{days}{hours}{minutes}{seconds}";
        }

        private void CalculateStats(PatrolHelicopter baseHelicopter)
        {
            if (heliHitTracker != null && heliHitTracker.ContainsKey(baseHelicopter))
            {
                var heliHitInfo = heliHitTracker[baseHelicopter];
                heliHitInfo.LastHitAt = DateTime.Now;

                heliHitTracker.Remove(baseHelicopter);

                if (heliHitInfo.Hitters.Count == 0)
                    return;

                // Helicopter info
                var tieredComponent = baseHelicopter.GetComponent<TieredPatrolHelicopterComponent>();
                if (tieredComponent == null)
                    return;

                Helicopter helicopter;
                if (!_pluginConfig.Config.Tiers.TryGetValue(tieredComponent.Tier, out helicopter) || helicopter == null)
                {
                    return;
                }

                var heliInfo = Lang(PluginMessages.PatrolHelicopterInfo, null, tieredComponent.Tier);

                // Calculate rewards
                var totalDamage = heliHitInfo.TotalDamage;

                var topAccurateHitter = heliHitInfo.Hitters.First().Key;
                var topAccurateHitterHitInfo = heliHitInfo.Hitters.First().Value;
                foreach (var hitter in heliHitInfo.Hitters.Keys.ToList())
                {
                    // Accuracy calculations
                    heliHitInfo.Hitters[hitter].Accuracy = (100f * (float)heliHitInfo.Hitters[hitter].WeakSpotHits /
                                                            (float)heliHitInfo.Hitters[hitter].TotalHits);
                    if (topAccurateHitterHitInfo.Accuracy < heliHitInfo.Hitters[hitter].Accuracy)
                        topAccurateHitter = hitter;

                    int rewardPoint = 0, economic = 0;
                    if (_pluginConfig.Config.UseServerRewards && ServerRewards != null)
                    {
                        rewardPoint = Convert.ToInt32(helicopter.KillRewards.KillRewardPoints *
                                                      (heliHitInfo.Hitters[hitter].Damage / totalDamage));
                        ServerRewards.Call("AddPoints", hitter.userID, rewardPoint);
                    }

                    if (_pluginConfig.Config.UseEconomics && Economics != null)
                    {
                        economic = Convert.ToInt32(helicopter.KillRewards.KillEconomics *
                                                   (heliHitInfo.Hitters[hitter].Damage / totalDamage));
                        Economics.Call("Deposit", hitter.UserIDString, (double)economic);
                    }

                    var localizeEconomics = Lang(PluginMessages.EconomicsName);
                    var localizeServerReward = Lang(PluginMessages.ServerRewardsName);
                    if (rewardPoint > 0 && economic > 0)
                    {
                        Server.Broadcast(Lang(PluginMessages.KillDoubleRewardMessage, null, hitter.displayName,
                            rewardPoint, localizeServerReward, economic, localizeEconomics));
                    }
                    else if (rewardPoint > 0)
                    {
                        Server.Broadcast(Lang(PluginMessages.KillSingleRewardMessage, null, hitter.displayName,
                            rewardPoint, localizeServerReward));
                    }
                    else if (economic > 0)
                    {
                        Server.Broadcast(Lang(PluginMessages.KillSingleRewardMessage, null, hitter.displayName,
                            economic, localizeEconomics));
                    }
                }

                // Server stats
                var timeTaken = heliHitInfo.LastHitAt - heliHitInfo.FirstHitAt;
                var accuracy = topAccurateHitterHitInfo.Accuracy < 0
                    ? string.Empty
                    : topAccurateHitterHitInfo.Accuracy.ToString("F0");

                //"It took {0} and total {1} damage to destroy {2} and <color=#CCEE33>{3}</color> with top rotor accuracy of <color=#00FF00>{4}%</color>!"
                Server.Broadcast(Lang(PluginMessages.KillStats, null, Humanize(timeTaken), totalDamage.ToString("F0"),
                    heliInfo, topAccurateHitter.displayName, accuracy));
            }
        }

        private void RegisterHeliHitInfo(PatrolHelicopter heli, BasePlayer hitter, HitInfo hitInfo)
        {
            var damage = hitInfo.damageTypes?.Total() ?? 0f;

            // Get or create tracking data
            HelicopterHitInfo helicopterHitInfo;
            if (!heliHitTracker.ContainsKey(heli))
            {
                helicopterHitInfo = new HelicopterHitInfo
                {
                    FirstHitAt = DateTime.Now,
                    LastWeakSpotHealth = heli.weakspots.Sum(x => x.maxHealth)
                };
                heliHitTracker.Add(heli, helicopterHitInfo);
            }
            else
            {
                helicopterHitInfo = heliHitTracker[heli];
            }

            var isWeakspotHit = false;
            var weakspotHealth = heli.weakspots.Sum(x => x.health);
            if (helicopterHitInfo.LastWeakSpotHealth > weakspotHealth)
            {
                helicopterHitInfo.LastWeakSpotHealth = weakspotHealth;
                isWeakspotHit = true;
            }

            // Update tracking data
            HitterHitInfo hitterHitInfo;
            if (!helicopterHitInfo.Hitters.ContainsKey(hitter))
            {
                hitterHitInfo = new HitterHitInfo();
                helicopterHitInfo.Hitters[hitter] = hitterHitInfo;
            }
            else
            {
                hitterHitInfo = helicopterHitInfo.Hitters[hitter];
            }

            if (hitInfo.hasDamage)
            {
                helicopterHitInfo.TotalDamage += damage;
                hitterHitInfo.Damage += damage;
            }

            if (isWeakspotHit)
            {
                hitterHitInfo.WeakSpotHits += 1;
            }

            hitterHitInfo.TotalHits += 1;
        }

        void InitializeHelicopters(string tier = null, Helicopter helicopter = null)
        {
            if (!string.IsNullOrEmpty(tier) && helicopter != null)
            {
                var next = Random.Range(helicopter.Spawn.SpawnMinimumTime, helicopter.Spawn.SpawnMaximumTime);
                if (helicopter.Spawn.SpawnCustomEnabled)
                {
                    var validSpawns = GetValidSpawnPointsIncludingMonuments(helicopter);
                    var spawnLocationName = validSpawns?.GetRandom();
                    timer.Once(next * 60, () =>
                    {
                        var spawnVector = Vector3.zero;
                        if (string.IsNullOrWhiteSpace(spawnLocationName))
                        {
                            SpawnHelicopterByChance(tier, spawnVector);
                        }
                        else
                        {
                            spawnVector = GetSpawnPointLocationIncludingMonuments(spawnLocationName);
                            SpawnHelicopterByChance(tier, spawnVector);
                        }
                    });
                }
                else if (helicopter.Spawn.SpawnRandomizedEnabled)
                {
                    timer.Once(next * 60, () => { SpawnHelicopterByChance(tier); });
                }

                return;
            }

            if (_pluginConfig.Config.Tiers == null || _pluginConfig.Config.Tiers.Count == 0)
            {
                Puts(Lang(PluginMessages.NoTiersFoundToSpawn));
                return;
            }

            if (!_pluginConfig.Config.Tiers.Any(x => x.Value != null &&
                                                     (x.Value.Spawn.SpawnRandomizedEnabled ||
                                                      x.Value.Spawn.SpawnCustomEnabled)))
            {
                Puts(Lang(PluginMessages.NoEnabledTiersFoundToSpawn));
                return;
            }

            var autoSpawns = _pluginConfig.Config.Tiers.Where(x => x.Value != null &&
                                                                   x.Value.Spawn.SpawnCustomEnabled &&
                                                                   x.Value.Spawn.SpawnCustomLocationNames != null &&
                                                                   x.Value.Spawn.SpawnCustomLocationNames.Any())
                .ToList();
            if (autoSpawns.Count == 0)
            {
                Puts(Lang(PluginMessages.NoAutoSpawnTiersFound));
            }

            foreach (var autoSpawn in autoSpawns)
            {
                if (autoSpawn.Value == null)
                    continue;

                var next = UnityEngine.Random.Range(autoSpawn.Value.Spawn.SpawnMinimumTime,
                    autoSpawn.Value.Spawn.SpawnMaximumTime);
                timer.Once(next * 60, (Action)(() =>
                {
                    var spawnVector = Vector3.zero;
                    List<string> validSpawns = GetValidSpawnLocationsForAutoSpawn(autoSpawn);
                    var spawnLocationName = validSpawns?.GetRandom();
                    if (string.IsNullOrWhiteSpace(spawnLocationName))
                    {
                        SpawnHelicopterByChance(autoSpawn.Key, spawnVector);
                    }
                    else
                    {
                        spawnVector = GetSpawnPointLocationIncludingMonuments(spawnLocationName);
                        SpawnHelicopterByChance(autoSpawn.Key, spawnVector);
                    }
                }));
            }

            var randomHelicopters = _pluginConfig.Config.Tiers.Where(x => x.Value != null &&
                                                                          !x.Value.Spawn.SpawnCustomEnabled &&
                                                                          x.Value.Spawn.SpawnRandomizedEnabled)
                .ToList();
            if (randomHelicopters.Count == 0)
            {
                Puts(Lang(PluginMessages.NoRandomSpawnTiersFound));
            }

            foreach (var randomHelicopter in randomHelicopters)
            {
                var next = Random.Range(randomHelicopter.Value.Spawn.SpawnMinimumTime,
                    randomHelicopter.Value.Spawn.SpawnMaximumTime);
                timer.Once(next * 60, () => { SpawnHelicopterByChance(randomHelicopter.Key); });
            }
        }

        private List<string> GetValidSpawnLocationsForAutoSpawn(KeyValuePair<string, Helicopter> autoSpawn)
        {
            if (autoSpawn.Value.Spawn.SpawnCustomLocationNames == null)
                return new List<string>();
            return autoSpawn.Value.Spawn.SpawnCustomLocationNames.Where(x =>
                (_pluginConfig.Config.SpawnLocations != null && _pluginConfig.Config.SpawnLocations.ContainsKey(x))
                || _pluginConfig.Config.MonumentSpawnLocations != null &&
                _pluginConfig.Config.MonumentSpawnLocations.ContainsKey(x)).ToList();
        }

        (TieredPatrolHelicopterComponent, Helicopter) GetTierSettingsFromVicinity(BaseNetworkable entity, List<PatrolHelicopter> helis, bool onlyDestroyed = true)
        {
            Vis.Entities(entity.transform.position, HelicopterItemsRadius, helis);

            foreach (var heli in helis)
            {
                if (heli.IsDestroyed && !onlyDestroyed)
                    continue;

                var tieredComponent = heli.GetComponent<TieredPatrolHelicopterComponent>();
                if (tieredComponent == null)
                    continue;

                Helicopter helicopter;
                _pluginConfig.Config.Tiers.TryGetValue(tieredComponent.Tier, out helicopter);

                return (tieredComponent, helicopter);
            }

            return (null, null);
        }

        PatrolHelicopter GetHelicopterFromVicinity(BaseNetworkable entity, List<PatrolHelicopter> helis,
            bool onlyDestroyed = true,
            Func<PatrolHelicopter, bool> predicate = null) // If onlyDestroyed is false, then only alive will be returned
        {
            Vis.Entities(entity.transform.position, HelicopterItemsRadius, helis);
            foreach (var heli in helis)
            {
                if (heli.IsDestroyed && !onlyDestroyed) continue;
                if (predicate == null || predicate(heli)) return heli;
            }

            return null;
        }

        //nearly exact code used by Rust to fire helicopter rockets
        private void FireRocket(PatrolHelicopter strafeHeli, PatrolHelicopterAI heliAI)
        {
            if (heliAI == null || !heliAI.IsAlive())
                return;
            var num1 = 4f;
            var strafeTarget = heliAI.strafe_target_position;
            if (strafeTarget == Vector3.zero)
                return;
            var vector3 = heliAI.transform.position + heliAI.transform.forward * 1f;
            var direction = (strafeTarget - vector3).normalized;
            if (num1 > 0.0)
                direction = Quaternion.Euler(UnityEngine.Random.Range((float)(-(double)num1 * 0.5), num1 * 0.5f),
                    UnityEngine.Random.Range((float)(-(double)num1 * 0.5), num1 * 0.5f),
                    UnityEngine.Random.Range((float)(-(double)num1 * 0.5), num1 * 0.5f)) * direction;
            var flag = heliAI.leftTubeFiredLast;
            heliAI.leftTubeFiredLast = !flag;
            Effect.server.Run(heliAI.helicopterBase.rocket_fire_effect.resourcePath, heliAI.helicopterBase,
                StringPool.Get(!flag ? "rocket_tube_right" : "rocket_tube_left"), Vector3.zero, Vector3.forward,
                (Network.Connection)null, true);
            var entity = GameManager.server.CreateEntity(!heliAI.CanUseNapalm() ? heliAI.rocketProjectile.resourcePath : heliAI.rocketProjectile_Napalm.resourcePath, vector3, new Quaternion(), true);
            if (entity == null)
                return;
            var projectile = entity.GetComponent<ServerProjectile>();
            entity.SendMessage("InitializeVelocity", (direction * projectile.speed));
            entity.OwnerID = 1337; //assign ownerID so it doesn't infinitely loop on OnEntitySpawned
            var component = entity.gameObject.AddComponent<HelicopterRocket>();
            component.Initialize(strafeHeli);
            entity.Spawn();
        }

        private void UnlockCrate(LockedByEntCrate crate)
        {
            if (crate == null)
                return;

            FireBall fireBall = FindChildByType<FireBall>(crate);
            if (fireBall != null)
                fireBall.Extinguish();

            crate.SetLocked(false);
            crate.lockingEnt = null;
        }

        private void AddToHelicopterCollection(PatrolHelicopter helicopter, string tier = null)
        {
            if (string.IsNullOrWhiteSpace(tier))
            {
                tier = GetTier(helicopter);
                if (string.IsNullOrWhiteSpace(tier))
                    return;
            }

            HashSet<PatrolHelicopter> helicopterList;
            if (SpawnedHelicopters.TryGetValue(tier, out helicopterList) && helicopterList != null)
            {
                helicopterList.Add(helicopter);
            }
            else
            {
                SpawnedHelicopters[tier] = new HashSet<PatrolHelicopter> { helicopter };
            }
        }

        private string GetTier(PatrolHelicopter helicopter)
        {
            var tierHelicopter = helicopter.GetComponents<MonoBehaviour>()
                .FirstOrDefault(x => x.GetType().Name.Equals(nameof(TieredPatrolHelicopterComponent)));
            if (tierHelicopter == null)
            {
                return string.Empty;
            }

            return tierHelicopter.GetType().GetProperty(nameof(TieredPatrolHelicopterComponent.Tier))?.GetValue(tierHelicopter)
                .ToString();
        }

        private int DestroySpawnedTier(string tier, List<PatrolHelicopter> spawnedHelicopters)
        {
            var destroyedCount = 0;
            for (var i = spawnedHelicopters.Count - 1; i >= 0; i--)
            {
                var baseHelicopter = spawnedHelicopters[i];

                //baseHelicopter.Hurt(baseHelicopter.health);
                baseHelicopter.Kill();
                destroyedCount++;
            }
            return destroyedCount;
        }

        private void SpawnHelicopterByChance(string tier, Vector3 coordinates = new Vector3())
        {
            Helicopter targetHelicopter;
            if (!_pluginConfig.Config.Tiers.TryGetValue(tier, out targetHelicopter) || targetHelicopter == null)
            {
                Puts(Lang(PluginMessages.TierNotFound));
                return;
            }

            var randomChanceValue = Random.Range(0f, 100f);
            if (targetHelicopter.Spawn.SpawnChance > 0 && targetHelicopter.Spawn.SpawnChance >= randomChanceValue)
                SpawnHelicopter(tier, targetHelicopter, coordinates);
        }

        private void CallHelicopterToPlayer(string tier, Vector3 coordinates = new Vector3(), BasePlayer invoker = null, BasePlayer target = null)
        {
            Helicopter targetHelicopter;
            if (!_pluginConfig.Config.Tiers.TryGetValue(tier, out targetHelicopter) || targetHelicopter == null)
            {
                if (invoker != null)
                {
                    SendReply(invoker, Lang(PluginMessages.TierNotFound, invoker.UserIDString));
                }
                else
                {
                    Puts(Lang(PluginMessages.TierNotFound));
                }

                return;
            }

            SpawnHelicopter(tier, targetHelicopter, coordinates, invoker, target);
        }

        private void SpawnHelicopter(string tier, Helicopter targetHelicopter, Vector3 coordinates = new Vector3(), BasePlayer invoker = null, BasePlayer target = null)
        {
            HashSet<PatrolHelicopter> helicopters;

            if (SpawnedHelicopters.TryGetValue(tier, out helicopters) && helicopters != null)
            {
                if (targetHelicopter.SpawnMaxActiveHelicopters >= 0 && targetHelicopter.SpawnMaxActiveHelicopters <= helicopters.Count)
                {
                    if (invoker != null)
                    {
                        SendReply(invoker, Lang(PluginMessages.HelicopterLimitReached, invoker.UserIDString, tier));
                    }
                    else
                    {
                        Puts(Lang(PluginMessages.HelicopterLimitReached, null, tier));
                    }

                    return;
                }
            }

            int totalActiveHelicopters = 0;
            foreach (var helicopterList in SpawnedHelicopters.Values)
            {
                if (helicopterList != null)
                    totalActiveHelicopters += helicopterList.Count;
            }

            if (_pluginConfig.Config.GlobalHelicopterConfig.MaximumActivePatrolHelicopters >= 0 && _pluginConfig.Config.GlobalHelicopterConfig.MaximumActivePatrolHelicopters <= totalActiveHelicopters)
            {
                if (invoker != null)
                    SendReply(invoker, Lang(PluginMessages.GlobalActiveHelicoptersLimitReached, invoker.UserIDString, tier));
                else
                    Puts(Lang(PluginMessages.GlobalActiveHelicoptersLimitReached, null, tier));

                return;
            }

            var baseHelicopter = GameManager.server.CreateEntity(PATROL_HELICOPTER_PREFAB) as PatrolHelicopter;

            if (baseHelicopter == null)
                return;
            var ai = baseHelicopter.GetComponent<PatrolHelicopterAI>();
            if (ai == null)
                return;

            if (coordinates != Vector3.zero)
                ai.SetInitialDestination(coordinates + new Vector3(0f, 10f, 0f));

            var component = baseHelicopter.gameObject.AddComponent<TieredPatrolHelicopterComponent>();
            if (component == null)
                return;
            component.Initialize(tier, target, targetHelicopter);
            baseHelicopter.Spawn();
            AddToHelicopterCollection(baseHelicopter, tier);
            if (invoker != null)
            {
                if (targetHelicopter.ChatBroadcast.BroadcastManualSpawn)
                {
                    Server.Broadcast(Lang(PluginMessages.ManualSpawnBroadcastMessage, null, tier, invoker.displayName));
                }

                SendReply(invoker, Lang(PluginMessages.HelicopterCalledChatMessage, invoker.UserIDString, tier, ai.destination));
                Puts(Lang(PluginMessages.HelicopterCalledConsoleMessage, null, invoker.displayName, tier, ai.destination));
            }
            else
            {
                if (coordinates == Vector3.zero)
                {
                    if (targetHelicopter.ChatBroadcast.BroadcastRandomSpawn)
                    {
                        Server.Broadcast(Lang(PluginMessages.RandomSpawnBroadcastMessage, null, tier));
                    }
                }
                else
                {
                    if (targetHelicopter.ChatBroadcast.BroadcastCustomSpawn)
                    {
                        Server.Broadcast(Lang(PluginMessages.CustomSpawnBroadcastMessage, null, tier));
                    }
                }

                Puts(Lang(PluginMessages.HelicopterSpawned, null, tier, ai.destination));
            }
        }

        private bool PlayerCanSpawnTier(BasePlayer player, string tierSelected, Helicopter helicopter)
        {
            var cooldownMinutes = helicopter.CallCommand.DefaultCooldown;
            var dailyLimit = helicopter.CallCommand.DefaultDailyLimit;
            var tiers = helicopter.CallCommand.CommandLimits;
            var userLimits = permission.GetUserPermissions(player.UserIDString)
                .Where(x => x.StartsWith("customhelicoptertiers.limits."))
                .Select(x => x.Replace("customhelicoptertiers.limits.", string.Empty)).ToList();
            if (userLimits.Any())
            {
                var userCommandLimits = tiers.Where(x => userLimits.Contains(x.Key.ToLower()))
                    .Select(x => x.Value).ToList();
                if (userCommandLimits.Any())
                {
                    cooldownMinutes = userCommandLimits.Min(x => x.Cooldown);
                    dailyLimit = userCommandLimits.Max(x => x.DailyLimit);
                }
            }

            if (Math.Abs(cooldownMinutes) < 0.0001f && dailyLimit == 0) // No limits are set, OK
            {
                return true;
            }

            PlayerCommandTracker tiersTracked = null;
            TierUseTracker tierUsed = null;
            if (CommandTracker != null &&
                !CommandTracker.TryGetValue(player.UserIDString,
                    out tiersTracked)) // No tracker exists for this player, create it
            {
                var newTracker = new PlayerCommandTracker();
                newTracker.AddTier(tierSelected);
                CommandTracker[player.UserIDString] = newTracker;
                SaveTrackers();
                return true;
            }

            if (tiersTracked != null && !tiersTracked.Tiers.TryGetValue(tierSelected, out tierUsed))
            {
                tiersTracked.AddTier(tierSelected);
                SaveTrackers();
                return true;
            }

            if (tierUsed != null && !tierUsed.LastUsed.HasValue)
            {
                tierUsed.LogUse();
                SaveTrackers();
                return true;
            }

            var useCount = tierUsed?.UseCount ?? 0;
            var lastUsedDate = tierUsed?.LastUsed.Value ?? DateTime.UtcNow;
            var minutesSinceLastUsed = DateTime.UtcNow.Subtract(lastUsedDate).TotalMinutes;
            if (minutesSinceLastUsed < cooldownMinutes)
            {
                var waitTime = TimeSpan.FromMinutes(cooldownMinutes - minutesSinceLastUsed);
                SendReply(player,
                    Lang(PluginMessages.CooldownMessage, player.UserIDString, Humanize(waitTime, player.UserIDString)));
                return false;
            }

            if (dailyLimit > 0 && useCount >= dailyLimit)
            {
                if (minutesSinceLastUsed < 1440)
                {
                    SendReply(player,
                        Lang(PluginMessages.DailyLimitMessage, player.UserIDString,
                            dailyLimit)); // 1440 = minutes per day
                    return false;
                }
                else
                {
                    tierUsed?.Reset(); // Reset cooldown - it's a new day
                    SaveTrackers();
                    return true;
                }
            }
            else
            {
                tierUsed?.LogUse();
                SaveTrackers();
                return true;
            }
        }

        private void DisableDefaultSpawning()
        {
            var eventPrefabs = UnityEngine.Object.FindObjectsOfType<TriggeredEventPrefab>();
            var heliEvent = eventPrefabs?.FirstOrDefault(p =>
                p != null && p.targetPrefab != null && p.targetPrefab.resourcePath.Contains("heli"));
            if (heliEvent != null)
            {
                UnityEngine.Object.Destroy(heliEvent);
                Puts(Lang(PluginMessages.DefaultHelicopterSpawningDisabled));
            }
        }

        private void CreateTempBox(BasePlayer player, string tier)
        {
            TempBoxData tempBoxData;
            if (_tempBoxes.TryGetValue(player.userID, out tempBoxData) && tempBoxData != null &&
                tempBoxData.Box != null)
            {
                tempBoxData.Box.Kill();
                tempBoxData.Box.KillMessage();
                _tempBoxes.Remove(player.userID);
            }

            Helicopter helicopter;
            if (!_pluginConfig.Config.Tiers.TryGetValue(tier, out helicopter) || helicopter == null)
            {
                SendReply(player, Lang(PluginMessages.TierNotFound, player.UserIDString));
                return;
            }

            var pos = GetGroundPosition(player.transform.position + (player.eyes.BodyForward() * 2));
            var box = GameManager.server.CreateEntity(
                "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab", pos);
            var storage = box as StorageContainer;
            if (storage == null)
            {
                box.Kill();
                box.KillMessage();
                SendReply(player, Lang(PluginMessages.InvalidBoxData, player.UserIDString));
                return;
            }

            box.enableSaving = false;
            box.SendMessage("SetDeployedBy", player, SendMessageOptions.DontRequireReceiver);
            _tempBoxes.Add(player.userID, new TempBoxData { Box = box, Tier = tier });
            box.Spawn();

            storage.inventory.Clear();
            if (helicopter.Loot.CustomLootTable != null)
                foreach (var itemInfo in helicopter.Loot.CustomLootTable)
                {
                    Item item;
                    ItemDefinition itemDefinition = itemInfo.GetItemDefinition();

                    var amount = itemInfo.MaximumSpawnAmount;
                    if (itemInfo.SpawnAsBlueprint)
                        item = ItemManager.CreateByItemID(BlueprintId, amount, itemInfo.SkinId);
                    else
                        item = ItemManager.CreateByName(itemInfo.Shortname, amount, itemInfo.SkinId);

                    if (item == null)
                    {
                        Puts(Lang(PluginMessages.ItemNotFound, null, itemInfo.Shortname));
                        continue;
                    }

                    if (itemInfo.SpawnAsBlueprint)
                        item.blueprintTarget = itemDefinition.itemid;

                    item.MoveToContainer(storage.inventory);
                }
        }

        private void StoreTempBoxData(BasePlayer player, StorageContainer container, string tier)
        {
            Helicopter helicopter;
            if (!_pluginConfig.Config.Tiers.TryGetValue(tier, out helicopter) || helicopter == null)
            {
                SendReply(player, Lang(PluginMessages.TierNotFound, player.UserIDString));
                return;
            }

            var itemList = Pool.GetList<Item>();
            itemList = container.inventory.itemList;

            var targetTable = new List<ItemInfo>();
            foreach (var item in itemList)
            {
                var itemId = item.info.itemid == BlueprintId ? item.blueprintTarget : item.info.itemid;
                var duplicateItem = targetTable.FirstOrDefault(x => x.GetItemDefinition().itemid == itemId && x.SkinId == item.skin);
                if (duplicateItem != null)
                {
                    duplicateItem.MaximumSpawnAmount += item.amount;
                }
                else
                {
                    var existingItem =
                        helicopter.Loot.CustomLootTable.FirstOrDefault(x =>
                            x.GetItemDefinition().itemid == itemId && x.SkinId == item.skin);
                    var minAmount = 1;
                    if (existingItem != null)
                    {
                        minAmount = existingItem.MinimumSpawnAmount;
                        if (minAmount > existingItem.MaximumSpawnAmount)
                        {
                            minAmount = existingItem.MaximumSpawnAmount;
                        }
                    }

                    targetTable.Add(new ItemInfo
                    {
                        Shortname = item.info.shortname,
                        SpawnAsBlueprint = item.info.itemid == BlueprintId,
                        MinimumSpawnAmount = minAmount,
                        MaximumSpawnAmount = item.amount,
                        SkinId = item.skin,
                        Rarity = Rarity.Common
                    });
                }
            }

            helicopter.Loot.CustomLootTable = new List<ItemInfo>(targetTable);
            Pool.FreeList(ref itemList);
            ConfigSave();
            container.inventory.Clear();
            container.Kill();
            container.KillMessage();
            _tempBoxes.Remove(player.userID);
            SendReply(player, Lang(PluginMessages.LootBoxUpdated, player.UserIDString, tier));
        }

        private bool CheckHeliInitiator(HitInfo hitInfo)
        {
            if (hitInfo.Initiator is PatrolHelicopter || (hitInfo.Initiator != null && (hitInfo.Initiator.ShortPrefabName.Equals("oilfireballsmall") || hitInfo.Initiator.ShortPrefabName.Equals("napalm"))))
            {
                return true;
            }
            else if (hitInfo.WeaponPrefab != null && (hitInfo.WeaponPrefab.ShortPrefabName.Equals("rocket_heli") || hitInfo.WeaponPrefab.ShortPrefabName.Equals("rocket_heli_napalm")))
            {
                return true;
            }
            return false;
        }

        private Vector3 GetGroundPosition(Vector3 sourcePos)
        {
            RaycastHit hitInfo;

            if (Physics.Raycast(sourcePos, Vector3.down, out hitInfo, Mathf.Infinity,
                    LayerMask.GetMask("Terrain", "World", "Construction")))
            {
                sourcePos.y = hitInfo.point.y;
            }

            sourcePos.y = Mathf.Max(sourcePos.y, TerrainMeta.HeightMap.GetHeight(sourcePos));
            return sourcePos;
        }

        public string Lang(string key, string id = null, params object[] args) =>
            string.Format(lang.GetMessage(key, this, id), args);

        private void ConfigSave()
        {
            //var newConfig = new DynamicConfigFile(_configPath);
            //newConfig.WriteObject(_pluginConfig, true);
            Config.WriteObject(_pluginConfig);
        }

        void SaveTrackers() => Interface.Oxide.DataFileSystem.WriteObject("CustomHelicopterTiers", CommandTracker);

        void LoadTrackers()
        {
            CommandTracker =
                Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, PlayerCommandTracker>>(
                    "CustomHelicopterTiers");
        }

        #endregion Methods

        #region Classes

        #region Commands Class

        public static class Commands
        {
            /// <summary>
            /// heli.help
            /// </summary>
            public const string Help = "heli.help";

            /// <summary>
            /// heli.call <Tier>
            /// heli.call <Tier> here
            /// heli.call <Tier> <SteamID>
            /// </summary>
            public const string Call = "heli.call";

            /// <summary>
            /// heli.kill <Tier>
            /// heli.kill all
            /// </summary>
            public const string Kill = "heli.kill";

            /// <summary>
            /// heli.loot set <Tier>
            /// </summary>
            public const string Loot = "heli.loot";

            /// <summary>
            /// heli.spawn set <SpawnPointName> <here|Coordinates>
            /// heli.spawn delete <SpawnPointName>
            /// heli.spawn show
            /// </summary>
            public const string Spawn = "heli.spawn";

            /// <summary>
            /// heli.delete <Tier>
            /// </summary>
            public const string Delete = "heli.delete";

            /// <summary>
            /// heli.add <Tier>
            /// </summary>
            public const string Add = "heli.add";

            /// <summary>
            /// heli.list
            /// </summary>
            public const string List = "heli.list";

            /// <summary>
            /// heli.report
            /// </summary>
            public const string Report = "heli.report";
        }

        #endregion

        #region Configuration

        private class PluginConfig
        {
            public HelicopterTierConfig Config { get; set; } = new HelicopterTierConfig();
        }

        private class HelicopterTierConfig
        {
            [JsonProperty(PropertyName = "Config Version", Order = 1)]
            public int Version { get; set; } = 1;

            [JsonProperty(PropertyName = "Plugin - Enabled", Order = 2)]
            public bool IsEnabled { get; set; } = true;

            [JsonProperty(PropertyName = "Economics - Enabled", Order = 3)]
            public bool UseEconomics { get; set; }

            [JsonProperty(PropertyName = "ServerRewards - Enabled", Order = 4)]
            public bool UseServerRewards { get; set; }

            [JsonProperty(PropertyName = "Global Helicopter Config", Order = 5)]
            public GlobalHelicopterConfig GlobalHelicopterConfig { get; set; } = new GlobalHelicopterConfig();

            [JsonProperty(PropertyName = "Debug Config", Order = 6)]
            public DebugConfig DebugConfig { get; set; } = new DebugConfig();

            [JsonProperty(PropertyName = "Tiers", Order = 7)]
            public Dictionary<string, Helicopter> Tiers { get; set; } = new Dictionary<string, Helicopter>(StringComparer.InvariantCultureIgnoreCase);

            [JsonProperty(PropertyName = "Custom Spawn Locations", Order = 8)]
            public Dictionary<string, SpawnLocationData> SpawnLocations { get; set; } = new Dictionary<string, SpawnLocationData>(StringComparer.InvariantCultureIgnoreCase);

            [JsonProperty(PropertyName = "Monuments Spawn Locations", Order = 9)]
            public Dictionary<string, SpawnLocationData> MonumentSpawnLocations { get; set; } = new Dictionary<string, SpawnLocationData>(StringComparer.InvariantCultureIgnoreCase);
        }

        public class GlobalHelicopterConfig
        {
            [JsonProperty(PropertyName = "Turrets Bullet Accuracy")]
            public float DamageTurretsBulletAccuracy { get; set; }

            [JsonProperty(PropertyName = "Disable Rust Default Helicopter")]
            public bool DisableRustDefaultHelicopter { get; set; }

            [JsonProperty(PropertyName = "Maximum Helicopter Life Time In Minutes")]
            public float SpawnMaximumHelicopterLifeTime { get; set; }

            [JsonProperty("Maximum Active Patrol Helicopters")]
            public int MaximumActivePatrolHelicopters { get; set; }
        }

        public class DebugConfig
        {
            [JsonProperty(PropertyName = "Debug Mode")]
            public bool IsEnabled { get; set; }
        }

        public class SpawnLocationData
        {
            [JsonProperty(PropertyName = "Position")]
            public Vector3 Position { get; set; }
        }

        public class CommandLimit
        {
            [JsonProperty(PropertyName = "Cooldown Minutes")]
            public float Cooldown { get; set; }

            [JsonProperty(PropertyName = "Daily Limit")]
            public int DailyLimit { get; set; }
        }

        public class Helicopter
        {
            [JsonProperty(PropertyName = "Helicopter - Enabled", Order = 1)]
            public bool Enabled { get; set; }

            [JsonProperty(PropertyName = "Max Active Helicopters", Order = 2)]
            public int SpawnMaxActiveHelicopters { get; set; }

            [JsonProperty(PropertyName = "PVP", Order = 3)]
            public PlayerVersusPlayer PlayerVsPlayer { get; set; } = new PlayerVersusPlayer();

            [JsonProperty(PropertyName = "PVE", Order = 4)]
            public PlayerVersusEnvironment PlayerVsEnvironment { get; set; } = new PlayerVersusEnvironment();

            [JsonProperty(PropertyName = "Damage", Order = 5)]
            public Damage Damage { get; set; } = new Damage();

            [JsonProperty(PropertyName = "Health", Order = 6)]
            public Health Health { get; set; } = new Health();

            [JsonProperty(PropertyName = "Speed", Order = 7)]
            public Speed Speed { get; set; } = new Speed();

            [JsonProperty(PropertyName = "Chat Broadcast", Order = 8)]
            public ChatBroadcast ChatBroadcast { get; set; } = new ChatBroadcast();

            [JsonProperty(PropertyName = "Debris", Order = 9)]
            public DebrisConfig Debris { get; set; } = new DebrisConfig();

            [JsonProperty(PropertyName = "Loot", Order = 10)]
            public LootConfig Loot { get; set; } = new LootConfig();

            [JsonProperty(PropertyName = "Spawn", Order = 11)]
            public Spawn Spawn { get; set; } = new Spawn();

            [JsonProperty(PropertyName = "Kill Rewards", Order = 12)]
            public KillReward KillRewards { get; set; } = new KillReward();

            [JsonProperty(PropertyName = "Call Command", Order = 13)]
            public CallCommand CallCommand { get; set; } = new CallCommand();
        }

        public class PlayerVersusPlayer
        {
            [JsonProperty(PropertyName = "Lock To The Invoker", Order = 1)]
            public bool LockToInvoker { get; set; }

            [JsonProperty(PropertyName = "Team Included On Invoker Lock", Order = 2)]
            public bool IncludeInvokerTeammates { get; set; }

            [JsonProperty(PropertyName = "Invoker Has Priority In The Team", Order = 3)]
            public bool InvokerHasPriority { get; set; }

            [JsonProperty(PropertyName = "Lock To First Attacker", Order = 5)]
            public bool LockToAttacker { get; set; }
        }

        public class PlayerVersusEnvironment
        {
            [JsonProperty(PropertyName = "Prevent Damage To Other Players Properties")]
            public bool EnablePlayerVersusEnvironment { get; set; }
        }

        public class Damage
        {
            [JsonProperty(PropertyName = "Rockets - Blunt Damage", Order = 1)]
            public float DamageRocketsBluntDamage { get; set; }

            [JsonProperty(PropertyName = "Rockets - Explosion Damage", Order = 2)]
            public float DamageRocketsExplosionDamage { get; set; }

            [JsonProperty(PropertyName = "Rockets - Explosion Radius", Order = 3)]
            public float DamageRocketsExplosionRadius { get; set; }

            [JsonProperty(PropertyName = "Rockets - Max Launched Rockets", Order = 4)]
            public int DamageRocketsMaxLaunchedRockets { get; set; }

            [JsonProperty(PropertyName = "Rockets - Time Between Each Rocket In Seconds", Order = 5)]
            public float DamageRocketsTimeBetweenEachRocket { get; set; }

            [JsonProperty(PropertyName = "Turrets - Bullet Damage", Order = 6)]
            public float DamageTurretsBulletDamage { get; set; }

            [JsonProperty(PropertyName = "Turrets - Max Bullet Range", Order = 7)]
            public float DamageTurretsBulletRange { get; set; }

            [JsonProperty(PropertyName = "Turrets - Bullet Speed", Order = 8)]
            public int DamageTurretsBulletSpeed { get; set; }

            [JsonProperty(PropertyName = "Turrets - Interval Between Bursts In Seconds", Order = 9)]
            public float DamageTurretsIntervalBetweenBursts { get; set; }

            [JsonProperty(PropertyName = "Turrets - Duration of Burst In Seconds", Order = 10)]
            public float DamageTurretsDurationOfBurst { get; set; }

            [JsonProperty(PropertyName = "Turrets - Fire Rate In Seconds", Order = 11)]
            public float DamageTurretsFireRate { get; set; }
        }

        public class Health
        {
            [JsonProperty(PropertyName = "Body", Order = 1)]
            public float HealthBodyHealth { get; set; }

            [JsonProperty(PropertyName = "Main Rotor", Order = 2)]
            public float HealthMainRotorHealth { get; set; }

            [JsonProperty(PropertyName = "Tail Rotor", Order = 3)]
            public float HealthTailRotorHealth { get; set; }
        }

        public class Speed
        {
            [JsonProperty(PropertyName = "Maximum Helicopter Speed", Order = 1)]
            public float MiscHelicopterSpeed { get; set; }

            [JsonProperty(PropertyName = "Helicopter Startup Length In Seconds", Order = 2)]
            public float MiscHelicopterStartupLength { get; set; }

            [JsonProperty(PropertyName = "Initial Helicopter Startup Speed", Order = 3)]
            public float MiscHelicopterStartupSpeed { get; set; }
        }

        public class ChatBroadcast
        {
            [JsonProperty(PropertyName = "Manual Spawn", Order = 1)]
            public bool BroadcastManualSpawn { get; set; }

            [JsonProperty(PropertyName = "Custom Spawn", Order = 2)]
            public bool BroadcastCustomSpawn { get; set; }

            [JsonProperty(PropertyName = "Random Spawn", Order = 3)]
            public bool BroadcastRandomSpawn { get; set; }
        }

        public class DebrisConfig
        {
            [JsonProperty("Spawn After Crash")]
            public bool SpawnAfterCrash { get; set; }

            [JsonProperty("Harvestable Hit Points")]
            public float HarvestableHitPoints { get; set; }

            [JsonProperty("Cooling Period Seconds")]
            public float CoolingPeriodSeconds { get; set; }
        }

        public class LootConfig
        {
            [JsonProperty("Maximum Crates To Spawn")]
            public int MaximumCratesToSpawn { get; set; }

            [JsonProperty("Burn Crates")]
            public bool BurnCrates { get; set; }

            [JsonProperty("Fire Ball")]
            public FireBallConfig FireBall { get; set; }

            [JsonProperty("Delay Before Crates Become Lootable Minutes")]
            public float DelayBeforeCratesBecomeLootableMinutes { get; set; }

            [JsonProperty("Always Lootable By Admins")]
            public bool AlwaysLootableByAdmins { get; set; }

            [JsonProperty("Can Be Opened Only By Summoner")]
            public bool CanBeOpenedOnlyBySummoner { get; set; }

            [JsonProperty("Can Be Opened By Summoner Team")]
            public bool CanBeOpenedBySummonerTeam { get; set; }

            [JsonProperty("Use Custom Loot Table")]
            public bool LootEnableCustomLootTable { get; set; }

            [JsonProperty("Minimum Loot Spawn Slots")]
            public int MinimumLootSpawnSlots { get; set; }

            [JsonProperty("Maximum Loot Spawn Slots")]
            public int MaximumLootSpawnSlots { get; set; }

            [JsonProperty("Rarity Weights")]
            public Dictionary<Rarity, int> RarityWeights { get; set; }

            [JsonProperty("Custom Loot Table")]
            public List<ItemInfo> CustomLootTable { get; set; } = new List<ItemInfo>();
        }

        public enum Rarity
        {
            Common,
            Uncommon,
            Rare,
            VeryRare
        }

        public class FireBallConfig
        {
            [JsonProperty("Minimum Lifetime Seconds")]
            public float MinimumLifetimeSeconds { get; set; }

            [JsonProperty("Maximum Lifetime Seconds")]
            public float MaximumLifetimeSeconds { get; set; }

            [JsonProperty("Spread Probability Percentage")]
            public int SpreadProbabilityPercentage { get; set; }

            [JsonProperty("Elapsed Lifetime Percentage To Begin Spreading")]
            public int ElapsedLifetimePercentageToBeginSpreading { get; set; }

            [JsonProperty("Burn Damage Reach")]
            public float BurnDamageReach { get; set; }

            [JsonProperty("Base Burn Damage Amount")]
            public float BaseBurnDamageAmount { get; set; }

            [JsonProperty("Damage Occurrence Frequency Seconds")]
            public float DamageOccurrenceFrequencySeconds { get; set; }

            [JsonProperty("Water Amount To Extinguish")]
            public int WaterAmountToExtinguish { get; set; }
        }

        public class Spawn
        {
            [JsonProperty(PropertyName = "Spawn Chance (Default = 100)", Order = 1)]
            public float SpawnChance { get; set; } = 100f;

            [JsonProperty(PropertyName = "Minimum Spawn Time In Minutes", Order = 2)]
            public float SpawnMinimumTime { get; set; }

            [JsonProperty(PropertyName = "Maximum Spawn Time In Minutes", Order = 3)]
            public float SpawnMaximumTime { get; set; }

            [JsonProperty(PropertyName = "Randomized Spawn - Enabled (Vanilla)", Order = 4)]
            public bool SpawnRandomizedEnabled { get; set; }

            [JsonProperty(PropertyName = "Custom Spawn - Enabled", Order = 5)]
            public bool SpawnCustomEnabled { get; set; }

            [JsonProperty(PropertyName = "Custom Spawn - Locations", Order = 6)]
            public List<string> SpawnCustomLocationNames { get; set; } = new List<string>();
        }

        public class KillReward
        {
            [JsonProperty(PropertyName = "Reward Points", Order = 1)]
            public int KillRewardPoints { get; set; }

            [JsonProperty(PropertyName = "Economics", Order = 2)]
            public int KillEconomics { get; set; }
        }

        public class CallCommand
        {
            [JsonProperty(PropertyName = "Default Cooldown In Minutes")]
            public float DefaultCooldown { get; set; }

            [JsonProperty(PropertyName = "Default Daily Limit")]
            public int DefaultDailyLimit { get; set; }

            [JsonProperty(PropertyName = "Custom Limits")]
            public Dictionary<string, CommandLimit> CommandLimits { get; set; } = new Dictionary<string, CommandLimit>(StringComparer.InvariantCultureIgnoreCase);
        }

        public class ItemInfo
        {
            [JsonProperty(PropertyName = "Shortname", Order = 2)]
            public string Shortname { get; set; }

            [JsonProperty(PropertyName = "Skin Id", Order = 3)]
            public ulong SkinId { get; set; }

            [JsonProperty(PropertyName = "Minimum Spawn Amount", Order = 4)]
            public int MinimumSpawnAmount { get; set; }

            [JsonProperty(PropertyName = "Maximum Spawn Amount", Order = 5)]
            public int MaximumSpawnAmount { get; set; }

            [JsonProperty(PropertyName = "Spawn As Blueprint", Order = 6)]
            public bool SpawnAsBlueprint { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            [JsonProperty(PropertyName = "Rarity", Order = 7)]
            public Rarity Rarity { get; set; }

            [JsonIgnore]
            private ItemDefinition itemDefinition;

            [JsonIgnore]
            private bool isValidated;

            public ItemDefinition GetItemDefinition()
            {
                if (!isValidated)
                {
                    ItemDefinition foundItemDefinition = ItemManager.FindItemDefinition(Shortname);
                    if (foundItemDefinition != null)
                        itemDefinition = foundItemDefinition;
                    else
                        return null;

                    isValidated = true;
                }

                return itemDefinition;
            }
        }

        #endregion

        private class TempBoxData
        {
            public string Tier { get; set; }
            public BaseEntity Box { get; set; }
        }

        private class TierUseTracker
        {
            public DateTime? LastUsed;
            public int UseCount;

            public TierUseTracker()
            {
                LogUse();
            }

            public void Reset()
            {
                LastUsed = null;
                UseCount = 0;
            }

            public void LogUse()
            {
                LastUsed = DateTime.UtcNow;
                UseCount += 1;
            }
        }

        private class PlayerCommandTracker
        {
            public Dictionary<string, TierUseTracker> Tiers =
                new Dictionary<string, TierUseTracker>(StringComparer.InvariantCultureIgnoreCase);

            public void AddTier(string id)
            {
                Tiers.Add(id, new TierUseTracker());
            }
        }

        private class TieredPatrolHelicopterComponent : FacepunchBehaviour
        {
            private float _debugTimer = 0.5f;
            private readonly StringBuilder _debugString = new StringBuilder();

            private bool _patrolling;
            private float _trackerTimer = 2;

            public PatrolHelicopterAI _ai;

            public PatrolHelicopter PatrolHelicopter { get; set; }
            public string Tier { get; set; }
            public BasePlayer Invoker { get; set; }
            public bool LockToInvoker { get; set; }
            public bool IncludeTeammates { get; set; }
            public bool InvokerHasPriority { get; set; }
            public bool EnablePlayerVersusEnvironment { get; set; }


            private void Awake()
            {
                PatrolHelicopter = GetComponent<PatrolHelicopter>();
                _ai = GetComponent<PatrolHelicopterAI>();
                _patrolling = false;
                enabled = false;
            }

            public void Initialize(string tier, BasePlayer player, Helicopter helicopter)
            {
                Tier = tier;
                Invoker = player;
                LockToInvoker = helicopter.PlayerVsPlayer.LockToInvoker;
                IncludeTeammates = helicopter.PlayerVsPlayer.IncludeInvokerTeammates;
                InvokerHasPriority = helicopter.PlayerVsPlayer.InvokerHasPriority;
                EnablePlayerVersusEnvironment = helicopter.PlayerVsEnvironment.EnablePlayerVersusEnvironment;
                enabled = true;
            }

            public void DoDestroy()
            {
                PatrolHelicopter.DieInstantly();
                Destroy(this);
            }

            private void Update()
            {
                if ((_instance?._pluginConfig?.Config.DebugConfig.IsEnabled ?? false) &&
                    (_debugTimer -= Time.deltaTime) <= 0)
                {
                    _debugTimer = 0.5f;
                    _debugString.Clear();
                    _debugString.AppendLine($"Tier: {Tier}");
                    _debugString.AppendLine($"Health: {PatrolHelicopter.health}");
                    _debugString.AppendLine($"Invoker: {(Invoker == null ? "Invoker is null" : Invoker.displayName)}");
                    if (_ai != null)
                    {
                        var firstTarget = _ai._targetList.Any() ? _ai._targetList[0]?.ply : null;
                        if (firstTarget != null)
                        {
                            var distance = Vector2.Distance(
                                new Vector2(firstTarget.ServerPosition.x, firstTarget.ServerPosition.z),
                                new Vector2(gameObject.transform.position.x, gameObject.transform.position.z));
                            _debugString.AppendLine($"Distance to target: {distance}");
                        }

                        _debugString.AppendLine($"AI State: {_ai._currentState}");
                        _debugString.AppendLine(
                            $"Targets: {string.Join(",", _ai._targetList.Select(x => x.ply != null ? x.ply.displayName : "Not a player"))}");
                    }

                    ConsoleNetwork.BroadcastToAllClients("ddraw.text", 0.5f, Color.green, gameObject.transform.position,
                        $"<size=20>{_debugString}</size>");
                }

                if (_ai == null || _ai._currentState == PatrolHelicopterAI.aiState.DEATH)
                    return;

                if (Invoker == null)
                {
                    if (!_patrolling &&
                        Vector2.Distance(new Vector2(_ai.transform.position.x, _ai.transform.position.z),
                            new Vector2(_ai.destination.x, _ai.destination.z)) < 2d &&
                        (_ai._currentState == PatrolHelicopterAI.aiState.IDLE ||
                         _ai._currentState == PatrolHelicopterAI.aiState.MOVE))
                    {
                        _patrolling = true;
                        _ai.State_Patrol_Enter();
                    }

                    return;
                }

                if (!LockToInvoker)
                {
                    return;
                }

                var targets = new List<ulong>();
                if ((_trackerTimer -= Time.deltaTime) <= 0 && !Invoker.IsDead() && !Invoker.IsSleeping() &&
                    _ai._currentState != PatrolHelicopterAI.aiState.STRAFE)
                {
                    _trackerTimer = 2;
                    if (!_ai.PlayerVisible(Invoker) && Vector2.Distance(
                            new Vector2(Invoker.ServerPosition.x, Invoker.ServerPosition.z),
                            new Vector2(_ai.destination.x, _ai.destination.z)) > 100)
                    {
                        _ai.ExitCurrentState();
                        _ai.State_Move_Enter(Invoker.ServerPosition);
                    }
                    else if (_ai._currentState == PatrolHelicopterAI.aiState.MOVE)
                    {
                        _ai.ExitCurrentState();
                    }
                }

                if (IncludeTeammates && Invoker.currentTeam > 0 &&
                    (!InvokerHasPriority || Invoker.IsDead() || Invoker.IsSleeping() || !_ai.PlayerVisible(Invoker)))
                {
                    targets.AddRange(Invoker.Team.members);
                }

                UpdateTargetList(targets);
            }

            private void UpdateTargetList(IEnumerable<ulong> players)
            {
                var invoker = _ai._targetList.FirstOrDefault(x => x.ply != null && x.ply.userID == Invoker.userID);
                if (invoker == null)
                {
                    invoker = new PatrolHelicopterAI.targetinfo(Invoker, Invoker);
                }

                var otherTargets = new List<PatrolHelicopterAI.targetinfo>();
                foreach (var teammateUserId in players)
                {
                    var teammate = _ai._targetList.FirstOrDefault(x => x.ply != null && x.ply.userID == teammateUserId);
                    if (teammate == null)
                    {
                        var teamMember = BasePlayer.FindByID(teammateUserId);
                        if (teamMember != null)
                            teammate = new PatrolHelicopterAI.targetinfo(teamMember, teamMember);
                    }

                    otherTargets.Add(teammate);
                }

                _ai._targetList.Clear();
                _ai._targetList.Add(invoker);
                _ai._targetList.AddRange(otherTargets);
            }
        }

        private class TieredCrateComponent : FacepunchBehaviour
        {
            public BasePlayer Invoker { get; set; }
            public bool AdminBypass { get; set; }
            public bool OnlyInvokerCanLoot { get; set; }
            public bool InvokerTeamCanLoot { get; set; }
            public DateTime? TimeToUnlock { get; set; }
        }

        private class HelicopterRocket : FacepunchBehaviour
        {
            public PatrolHelicopter Helicopter { get; set; }

            public void Initialize(PatrolHelicopter helicopter)
            {
                Helicopter = helicopter;
            }
        }

        private class HitterHitInfo
        {
            public float Damage { get; set; } = 0;
            public int WeakSpotHits { get; set; } = 0;
            public int TotalHits { get; set; } = 0;
            public float Accuracy { get; set; } = 0;
        }

        private class HelicopterHitInfo
        {
            public DateTime FirstHitAt { get; set; }
            public DateTime LastHitAt { get; set; }
            public float TotalDamage { get; set; } = 0;
            public float LastWeakSpotHealth { get; set; } = 0;
            public Dictionary<BasePlayer, HitterHitInfo> Hitters { get; set; }

            public HelicopterHitInfo()
            {
                Hitters = new Dictionary<BasePlayer, HitterHitInfo>();
            }
        }

        private static class PluginMessages
        {
            public const string KillSingleRewardMessage = "Kill Single Reward Message";
            public const string KillDoubleRewardMessage = "Kill Double Reward Message";
            public const string ServerRewardsName = "ServerRewards Plugin Short Name";
            public const string EconomicsName = "Economics Plugin Short Name";
            public const string KillStats = "Kill Stats";
            public const string NoTiersFound = "No Tiers Found";
            public const string TierNameExist = "Tier Name Exist";
            public const string TierNotFound = "Tier Not Found";
            public const string PlayerNotFound = "Player Not Found";
            public const string NoCustomLootsFound = "No Custom Loots Found";
            public const string NoItemDefinition = "No Item Definition";
            public const string IsNotResearchable = "Is Not Researchable";
            public const string ItemNotFound = "Item Not Found";
            public const string HelicopterCrateItems = "Helicopter Crate Items";
            public const string NoLootPermission = "No Loot Permission";
            public const string PluginDisabled = "Plugin Disabled";
            public const string TierAdded = "Tier Added";
            public const string TierRemoved = "Tier Removed";
            public const string TierNameRequired = "Tier Name Required";
            public const string TierNoCallPermission = "Tier No Call Permission";
            public const string TierNoCallPermissionAdmin = "Tier No Call Permission (ForAdmins)";
            public const string HelicopterTierDisabled = "Helicopter Tier Disabled";
            public const string HelicopterCalledChatMessage = "Helicopter Called Chat Message";
            public const string HelicopterCalledConsoleMessage = "Helicopter Called Console Message";
            public const string HelicopterSpawned = "Helicopter Spawned";
            public const string InvalidArgumentForKillCommand = "Invalid Argument For Kill Command";
            public const string TierNoKillPermission = "Tier No Kill Permission";
            public const string TierNoKillPermissionAdmin = "Tier No Kill Permission (ForAdmins)";
            public const string NoHelicoptersToDestroy = "No Helicopters To Destroy";
            public const string HelicoptersDestroyedChatMessage = "Helicopters Destroyed Chat Message";
            public const string HelicoptersDestroyedConsoleMessage = "Helicopters Destroyed Console Message";

            public const string HelicopterListTitle = "Helicopter List Title";

            public const string HelicopterListNoData = "Helicopter List No Data";
            public const string Days = "Day(s)";
            public const string Hours = "Hour(s)";
            public const string Minutes = "Minute(s)";
            public const string Seconds = "Second(s)";
            public const string PatrolHelicopterInfo = "Patrol Helicopter Info";
            public const string NoTiersFoundToSpawn = "No Tiers Found To Spawn";
            public const string NoEnabledTiersFoundToSpawn = "No Enabled Tiers Found To Spawn";
            public const string NoAutoSpawnTiersFound = "No Auto Spawn Tiers Found";
            public const string NoRandomSpawnTiersFound = "No Random Spawn Tiers Found";
            public const string CooldownMessage = "Cooldown Message";
            public const string DailyLimitMessage = "Daily Limit Message";
            public const string DefaultHelicopterSpawningDisabled = "Default Helicopter Spawning Disabled";
            public const string WrongCommand = "Wrong Command";
            public const string InvalidCoordinates = "Invalid Coordinates";
            public const string SpawnNameRequired = "Spawn Name Required";
            public const string NoSpawnPermission = "No Spawn Command Permission";
            public const string NoEditPermission = "No Edit Command Permission";
            public const string SpawnPositionSet = "Spawn Position Set";
            public const string SpawnPositionDeleted = "Spawn Position Deleted";
            public const string SpawnPositionNotFound = "Spawn Position Not Found";
            public const string HelicopterLimitReached = "Helicopter Limit Reached";
            public const string GlobalActiveHelicoptersLimitReached = "Global Helicopter Limit Reached";
            public const string InvalidBoxData = "Invalid Box Data";
            public const string LootBoxUpdated = "Loot Box Updated";
            public const string LootBoxCooldown = "Loot Box Cooldown";
            public const string ManualSpawnBroadcastMessage = "Manual Spawn Broadcast Message";
            public const string CustomSpawnBroadcastMessage = "Custom Spawn Broadcast Message";
            public const string RandomSpawnBroadcastMessage = "Random Spawn Broadcast Message";
            public const string Reservation = "Reserve Message";
        }

        private static class DrawUtil
        {
            public static void Box(BasePlayer player, float duration, Color color, Vector3 originPosition, float radius)
            {
                player.SendConsoleCommand("ddraw.box", duration, color, originPosition, radius);
            }

            public static void Sphere(BasePlayer player, float duration, Color color, Vector3 originPosition, float radius)
            {
                player.SendConsoleCommand("ddraw.sphere", duration, color, originPosition, radius);
            }

            public static void Line(BasePlayer player, float duration, Color color, Vector3 originPosition, Vector3 targetPosition)
            {
                player.SendConsoleCommand("ddraw.line", duration, color, originPosition, targetPosition);
            }

            public static void Arrow(BasePlayer player, float duration, Color color, Vector3 originPosition, Vector3 targetPosition, float headSize)
            {
                player.SendConsoleCommand("ddraw.arrow", duration, color, originPosition, targetPosition, headSize);
            }

            public static void Text(BasePlayer player, float duration, Color color, Vector3 originPosition, string text)
            {
                player.SendConsoleCommand("ddraw.text", duration, color, originPosition, text);
            }
        }


        #endregion Classes
    }
}