using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Text;

using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Core;
using UnityEngine;

using Building = BuildingManager.Building;
using Pool = Facepunch.Pool;

namespace Oxide.Plugins
{
    [Info("Limit Entities", "MON@H", "2.1.9")]
    [Description("Limiting the number of entities a player can build")]
    public class LimitEntities : RustPlugin
    {
        #region Class Fields

        [PluginReference] private readonly Plugin RustTranslationAPI;

        private const string PermissionAdmin = "limitentities.admin";
        private const string PermissionImmunity = "limitentities.immunity";

        private static readonly Static _static = new();

        private readonly Cache _cache = new();

        public enum LogLevel
        {
            Off = 0,
            Error = 1,
            Warning = 2,
            Info = 3,
            Debug = 4,
        }

        public class BuildingEntities
        {
            public uint BuildingID { get; set; }
            public readonly Hash<uint, int> EntitiesCount = new();
            public readonly HashSet<ulong> EntitiesIds = new();

            public void AddEntity(BaseEntity entity)
            {
                EntitiesIds.Add(entity.net.ID.Value);
                uint prefabID = _static.Instance.GetPrefabID(entity.prefabID);
                EntitiesCount[prefabID]++;
            }

            public void AddRange(uint prefabID, int count)
            {
                EntitiesCount[prefabID] += count;
            }

            public void RemoveEntity(BaseEntity entity)
            {
                EntitiesIds.Remove(entity.net.ID.Value);
                uint prefabID = _static.Instance.GetPrefabID(entity.prefabID);
                EntitiesCount[prefabID]--;
            }

            public BuildingEntities(uint buildingID)
            {
                BuildingID = buildingID;
            }
        }

        public class Cache
        {
            public readonly Hash<string, Hash<uint, string>> DisplayNames = new();
            public readonly Hash<uint, BuildingEntities> Buildings = new();
            public readonly Hash<ulong, PlayerData> PlayerData = new();
            public readonly List<BuildingBlock> Blocks = new();
            public readonly Permissions Permissions = new();
            public readonly Prefabs Prefabs = new();
            public readonly StringBuilders StringBuilders = new();
        }

        public class PlayerData
        {
            public readonly string PlayerIdString;
            public PermissionEntry Perms;
            public bool HasImmunity { get; set; }
            public readonly PlayerEntities Entities = new();

            public PlayerData(ulong playerId)
            {
                PlayerIdString = playerId.ToString();
                UpdatePerms();
            }

            public void UpdatePerms()
            {
                HasImmunity = _static.Instance.permission.UserHasPermission(PlayerIdString, PermissionImmunity);
                Perms = _static.Instance.GetPlayerPermissions(this);
            }

            public void AddEntity(uint prefabID)
            {
                Entities.AddEntity(prefabID);
            }

            public void RemoveEntity(uint prefabID)
            {
                Entities.RemoveEntity(prefabID);
            }

            public bool CanBuild()
            {
                return Perms == null || HasImmunity || Perms.LimitsGlobal.LimitTotal != 0;
            }

            public bool IsGlobalLimit()
            {
                if (Perms == null)
                {
                    return false;
                }

                LimitsEntry limit = Perms.LimitsGlobal;
                return Entities.TotalCount >= limit.LimitTotal;
            }

            public bool IsGlobalLimit(uint prefabId)
            {
                if (Perms == null)
                {
                    return false;
                }

                Hash<uint, int> limitEntities = Perms.LimitsGlobal.LimitEntitiesCache;
                if (!limitEntities.ContainsKey(prefabId))
                {
                    return false;
                }

                return Entities.PlayersEntities[prefabId] >= limitEntities[prefabId];
            }

            public bool IsBuildingLimit(BuildingEntities entities)
            {
                if (Perms == null)
                {
                    return false;
                }

                LimitsEntry limit = Perms.LimitsBuilding;
                return entities.EntitiesIds.Count >= limit.LimitTotal;
            }

            public bool IsBuildingLimit(BuildingEntities entities, uint prefabId)
            {
                if (Perms == null)
                {
                    return false;
                }

                Hash<uint, int> limitEntities = Perms.LimitsBuilding.LimitEntitiesCache;
                if (!limitEntities.ContainsKey(prefabId))
                {
                    return false;
                }

                return entities.EntitiesCount[prefabId] >= limitEntities[prefabId];
            }

            public float GetGlobalPercentage()
            {
                if (Perms == null)
                {
                    return 0;
                }

                LimitsEntry limit = Perms.LimitsGlobal;
                return (float)Entities.TotalCount / limit.LimitTotal * 100;
            }

            public float GetGlobalPercentage(uint prefabId)
            {
                if (Perms == null)
                {
                    return 0;
                }

                Hash<uint, int> limitEntities = Perms.LimitsGlobal.LimitEntitiesCache;
                if (!limitEntities.ContainsKey(prefabId))
                {
                    return 0;
                }

                return (float)Entities.PlayersEntities[prefabId] / limitEntities[prefabId] * 100;
            }

            public float GetBuildingPercentage(BuildingEntities entities)
            {
                if (Perms == null)
                {
                    return 0;
                }

                LimitsEntry limit = Perms.LimitsBuilding;
                return (float)entities.EntitiesIds.Count / limit.LimitTotal * 100;
            }

            public float GetBuildingPercentage(BuildingEntities entities, uint prefabId)
            {
                if (Perms == null)
                {
                    return 0;
                }

                Hash<uint, int> limitEntities = Perms.LimitsBuilding.LimitEntitiesCache;
                if (!limitEntities.ContainsKey(prefabId))
                {
                    return 0;
                }

                return (float)entities.EntitiesCount[prefabId] / limitEntities[prefabId] * 100;
            }
        }

        public class PlayerEntities
        {
            public int TotalCount;
            public readonly Hash<uint, int> PlayersEntities = new();

            public void AddEntity(uint prefabID)
            {
                TotalCount++;
                PlayersEntities[prefabID]++;
            }

            public void AddRange(uint prefabID, int count)
            {
                TotalCount += count;
                PlayersEntities[prefabID] += count;
            }

            public void RemoveEntity(uint prefabID)
            {
                TotalCount--;
                int count = PlayersEntities[prefabID];
                if (count < 1)
                {
                    PlayersEntities.Remove(prefabID);
                    return;
                }
                PlayersEntities[prefabID] = count - 1;
            }
        }

        public class Prefabs
        {
            public readonly Hash<uint, string> ShortNames = new();
            public readonly Hash<uint, uint> Groups = new();
            public readonly HashSet<uint> BuildingBlocks = new();
            public readonly HashSet<uint> Tracked = new();
        }

        public class Permissions
        {
            public PermissionEntry[] Descending { get; set; }
            public string[] Registered { get; set; }
        }

        public class Static
        {
            public LimitEntities Instance { get; set; }
            public readonly object True = true;
            public readonly object False = false;
            public readonly Regex Tags = new("<color=.+?>|</color>|<size=.+?>|</size>|<i>|</i>|<b>|</b>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        }

        public class StringBuilders
        {
            public readonly StringBuilder LimitsBuilding = new();
            public readonly StringBuilder LimitsGlobal = new();
        }

        #endregion Class Fields

        #region Initialization

        private void Init() => HooksUnsubscribe();

        private void OnServerInitialized()
        {
            _static.Instance = this;
            RegisterPermissions();
            AddCommands();
            CacheGroupIds();
            CachePermissions();
            DataLoad();
            CachePrefabIds();
            CacheEntities();
            RegisterMessages();

            foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
            {
                GetPlayerData(activePlayer.userID);
            }

            HooksSubscribe();
        }

        private void Unload()
        {
            DataSave();
            _static.Instance = null;
        }

        private void OnNewSave() => DataClear();

        #endregion Initialization

        #region Configuration

        private PluginConfig _pluginConfig;

        public class PluginConfig
        {
            [JsonConverter(typeof(StringEnumConverter))]
            [DefaultValue(LogLevel.Off)]
            [JsonProperty(PropertyName = "Log Level (Debug, Info, Warning, Error, Off)", Order = 4)]
            public LogLevel LoggingLevel { get; set; }

            [JsonProperty(PropertyName = "Enable GameTip notifications")]
            public bool GameTipNotificationsEnabled { get; set; }

            [JsonProperty(PropertyName = "Enable notifications in chat")]
            public bool ChatNotificationsEnabled { get; set; }

            [JsonProperty(PropertyName = "Chat steamID icon")]
            public ulong SteamIDIcon { get; set; }

            [JsonProperty(PropertyName = "Commands list")]
            public string[] Commands { get; set; }

            [JsonProperty(PropertyName = "Warn when more than %")]
            [DefaultValue(80f)]
            public float WarnPercent { get; set; }

            [JsonProperty(PropertyName = "Excluded list")]
            public string[] Excluded { get; set; }

            [JsonProperty(PropertyName = "Entity Groups")]
            public List<EntityGroup> EntityGroups { get; set; }

            [JsonProperty(PropertyName = "Permissions")]
            public PermissionEntry[] Permissions { get; set; }
        }

        public class LimitsEntry
        {
            [JsonProperty(PropertyName = "Limit Total")]
            public int LimitTotal { get; set; }

            [JsonProperty(PropertyName = "Limits Entities")]
            public SortedDictionary<string, int> LimitsEntities { get; set; }

            [JsonIgnore]
            public readonly Hash<uint, int> LimitEntitiesCache = new();
        }

        public class PermissionEntry
        {
            [JsonProperty(PropertyName = "Permission")]
            public string Permission { get; set; }

            [JsonProperty(PropertyName = "Priority")]
            public int Priority { get; set; }

            [JsonProperty(PropertyName = "Limits Global")]
            public LimitsEntry LimitsGlobal { get; set; }

            [JsonProperty(PropertyName = "Limits Building")]
            public LimitsEntry LimitsBuilding { get; set; }

            [JsonProperty(PropertyName = "Prevent excessive merging of buildings")]
            public bool MergingCheck { get; set; }
        }

        public class EntityGroup
        {
            [JsonProperty(PropertyName = "Group name")]
            public string Name { get; set; }

            [JsonIgnore]
            public uint ID { get; set; }

            [JsonProperty(PropertyName = "Group Entities list")]
            public List<string> ListEntities { get; set; }

            [JsonIgnore]
            public readonly List<uint> ListEntitiesCache = new();
        }

        protected override void LoadDefaultConfig() => PrintWarning("Loading Default Config");

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = AdditionalConfig(Config.ReadObject<PluginConfig>());
            Config.WriteObject(_pluginConfig);
        }

        public PluginConfig AdditionalConfig(PluginConfig config)
        {
            config.Commands ??= new[] { "limits", "limit" };
            config.Excluded ??= new[] { "assets/prefabs/building/ladder.wall.wood/ladder.wooden.wall.prefab" };
            config.EntityGroups ??= new()
            {
                new()
                {
                    Name = "Foundations",
                    ListEntities = new()
                    {
                        "assets/prefabs/building core/foundation.triangle/foundation.triangle.prefab",
                        "assets/prefabs/building core/foundation/foundation.prefab",
                    }
                },
                new()
                {
                    Name = "Furnace",
                    ListEntities = new()
                    {
                        "assets/prefabs/deployable/furnace/furnace.prefab",
                        "assets/prefabs/deployable/legacyfurnace/legacy_furnace.prefab",
                        "assets/prefabs/deployable/playerioents/electricfurnace/electricfurnace.deployed.prefab",
                    }
                },
                new()
                {
                    Name = "PlanterBoxes",
                    ListEntities = new()
                    {
                        "assets/prefabs/deployable/planters/planter.large.deployed.prefab",
                        "assets/prefabs/deployable/planters/planter.small.deployed.prefab",
                        "assets/prefabs/misc/decor_dlc/bath tub planter/bathtub.planter.deployed.prefab",
                        "assets/prefabs/misc/decor_dlc/minecart planter/minecart.planter.deployed.prefab",
                        "assets/prefabs/misc/decor_dlc/rail road planter/railroadplanter.deployed.prefab",
                    }
                },
                new()
                {
                    Name = "Quarries",
                    ListEntities = new()
                    {
                        "assets/prefabs/deployable/oil jack/mining.pumpjack.prefab",
                        "assets/prefabs/deployable/quarry/mining_quarry.prefab",
                    }
                },
                new()
                {
                    Name = "Roof",
                    ListEntities = new()
                    {
                        "assets/prefabs/building core/roof.triangle/roof.triangle.prefab",
                        "assets/prefabs/building core/roof/roof.prefab",
                    }
                },
                new()
                {
                    Name = "TC",
                    ListEntities = new()
                    {
                        "assets/prefabs/deployable/tool cupboard/cupboard.tool.deployed.prefab",
                        "assets/prefabs/deployable/tool cupboard/retro/cupboard.tool.retro.deployed.prefab",
                    }
                },
            };
            config.Permissions ??= new PermissionEntry[]
            {
                new() {
                    Permission = nameof(LimitEntities).ToLower() + ".default",
                    Priority = 10,
                    LimitsGlobal = new()
                    {
                        LimitTotal = 2000,
                        LimitsEntities = new()
                        {
                            {"assets/content/props/strobe light/strobelight.prefab", 10},
                            {"assets/prefabs/deployable/campfire/campfire.prefab", 20},
                            {"assets/prefabs/deployable/ceiling light/ceilinglight.deployed.prefab", 50},
                            {"assets/prefabs/deployable/furnace.large/furnace.large.prefab", 5},
                            {"assets/prefabs/deployable/oil refinery/refinery_small_deployed.prefab", 5},
                            {"assets/prefabs/deployable/search light/searchlight.deployed.prefab", 10},
                            {"assets/prefabs/deployable/windmill/electric.windmill.small.prefab", 10},
                            {"assets/prefabs/npc/sam_site_turret/sam_site_turret_deployed.prefab", 0},
                            {"Foundations", 300},
                            {"Furnace", 10},
                            {"PlanterBoxes", 50},
                            {"Quarries", 2},
                            {"Roof", 200},
                            {"TC", 10},
                        }
                    },
                    LimitsBuilding = new()
                    {
                        LimitTotal = 1000,
                        LimitsEntities = new()
                        {
                            {"assets/content/props/strobe light/strobelight.prefab", 5},
                            {"assets/prefabs/deployable/search light/searchlight.deployed.prefab", 5},
                            {"assets/prefabs/deployable/windmill/electric.windmill.small.prefab", 5},
                        }
                    }
                },
                new() {
                    Permission = nameof(LimitEntities).ToLower() + ".vip",
                    Priority = 20,
                    LimitsGlobal = new()
                    {
                        LimitTotal = 5000,
                        LimitsEntities = new()
                        {
                            {"Foundations", 500},
                            {"Roof", 400},
                        },
                    },
                    LimitsBuilding = new()
                    {
                        LimitTotal = 2000,
                        LimitsEntities = new()
                        {
                            {"assets/content/props/strobe light/strobelight.prefab", 15},
                            {"assets/prefabs/deployable/search light/searchlight.deployed.prefab", 15},
                            {"assets/prefabs/deployable/windmill/electric.windmill.small.prefab", 15},
                        }
                    }
                },
                new() {
                    Permission = nameof(LimitEntities).ToLower() + ".elite",
                    Priority = 30,
                    LimitsGlobal = new()
                    {
                        LimitTotal = 10000,
                        LimitsEntities = new()
                        {
                            {"Foundations", 2000},
                            {"Roof", 1000},
                        },
                    },
                    LimitsBuilding = new()
                    {
                        LimitTotal = 5000,
                        LimitsEntities = new()
                        {
                            {"assets/content/props/strobe light/strobelight.prefab", 20},
                            {"assets/prefabs/deployable/search light/searchlight.deployed.prefab", 20},
                            {"assets/prefabs/deployable/windmill/electric.windmill.small.prefab", 20},
                        }
                    }
                },
            };
            return config;
        }

        #endregion Configuration

        #region Stored Data

        private StoredData _storedData;

        private class StoredData
        {
            public readonly Hash<uint, ulong> BuildingsOwners = new();
        }

        public void DataLoad()
        {
            try
            {
                _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            catch
            {
                DataClear();
                DataLoad();
            }

            if (_storedData.BuildingsOwners.Count > 0)
            {
                ListDictionary<uint, Building> serverBuildings = BuildingManager.server.buildingDictionary;

                List<uint> buildingsToRemove = Pool.Get<List<uint>>();
                foreach (uint buildingId in _storedData.BuildingsOwners.Keys)
                {
                    if (!serverBuildings.Contains(buildingId))
                    {
                        buildingsToRemove.Add(buildingId);
                    }
                }

                for (int index = 0; index < buildingsToRemove.Count; index++)
                {
                    uint buildingId = buildingsToRemove[index];
                    _storedData.BuildingsOwners.Remove(buildingId);
                }

                Pool.FreeUnsafe(ref buildingsToRemove);

                Log($"{_storedData.BuildingsOwners.Count} buildings with owners loaded from data file", LogLevel.Debug);
                return;
            }

            Log("Collecting all buildings owners on the server", LogLevel.Debug);

            for (int i = 0; i < BuildingManager.server.buildingDictionary.Values.Count; i++)
            {
                Building building = BuildingManager.server.buildingDictionary.Values[i];
                if (!building.HasDecayEntities() || _storedData.BuildingsOwners.ContainsKey(building.ID))
                {
                    continue;
                }

                ulong netID = uint.MaxValue;
                ulong ownerId = 0;
                for (int index = 0; index < building.decayEntities.Count; index++)
                {
                    DecayEntity decayEntity = building.decayEntities[index];
                    if (decayEntity.IsValid() && decayEntity.OwnerID.IsSteamId() && decayEntity.net.ID.Value < netID)
                    {
                        netID = decayEntity.net.ID.Value;
                        ownerId = decayEntity.OwnerID;
                    }
                }
                if (ownerId != 0)
                {
                    Log($"Adding missing owner ID: {ownerId} for building ID: {building.ID}", LogLevel.Debug);
                    _storedData.BuildingsOwners[building.ID] = ownerId;
                }
            }

            if (_storedData.BuildingsOwners.Count > 0)
            {
                Log($"{_storedData.BuildingsOwners.Count} buildings with owners added to data file", LogLevel.Debug);
                DataSave();
                return;
            }

            Log("No buildings with owners found on the server!", LogLevel.Warning);
        }

        public void DataClear()
        {
            PrintWarning("Creating a new data file");
            _storedData = new();
            DataSave();
        }

        public void DataSave()
        {
            if (_storedData != null)
            {
                Interface.Oxide.DataFileSystem.WriteObject(Name, _storedData);
            }
        }

        #endregion Stored Data

        #region Localization

        public string Lang(string key, string userIDString = null, params object[] args)
        {
            try
            {
                return string.Format(lang.GetMessage(key, this, userIDString), args);
            }
            catch (Exception ex)
            {
                PrintError($"Lang Key '{key}' threw exception:\n{ex}");
                throw;
            }
        }

        private static class LangKeys
        {
            public static class Error
            {
                private const string Base = nameof(Error) + ".";
                public const string EntityIsNotAllowed = Base + nameof(EntityIsNotAllowed);
                public const string NoPermission = Base + nameof(NoPermission);
                public const string PlayerNotFound = Base + nameof(PlayerNotFound);
                public static class LimitBuilding
                {
                    private const string Base = Error.Base + nameof(LimitBuilding) + ".";
                    public const string EntityMergeBlocked = Base + nameof(EntityMergeBlocked);
                    public const string EntityReached = Base + nameof(EntityReached);
                    public const string MergeBlocked = Base + nameof(MergeBlocked);
                    public const string Reached = Base + nameof(Reached);
                }
                public static class LimitGlobal
                {
                    private const string Base = Error.Base + nameof(LimitGlobal) + ".";
                    public const string EntityReached = Base + nameof(EntityReached);
                    public const string Reached = Base + nameof(Reached);
                }
            }

            public static class Format
            {
                private const string Base = nameof(Format) + ".";
                public const string Prefix = Base + nameof(Prefix);
            }

            public static class Info
            {
                private const string Base = nameof(Info) + ".";
                public const string Help = Base + nameof(Help);
                public const string LimitBuilding = Base + nameof(LimitBuilding);
                public const string LimitBuildingEntity = Base + nameof(LimitBuildingEntity);
                public const string LimitGlobal = Base + nameof(LimitGlobal);
                public const string LimitGlobalEntity = Base + nameof(LimitGlobalEntity);
                public const string Limits = Base + nameof(Limits);
                public const string TotalAmount = Base + nameof(TotalAmount);
                public const string Unlimited = Base + nameof(Unlimited);
            }
        }

        private readonly Dictionary<string, string> _langEN = new()
        {
            [LangKeys.Error.EntityIsNotAllowed] = "You are not allowed to build <color=#FFA500>{0}</color>",
            [LangKeys.Error.PlayerNotFound] = "Player <color=#FFA500>{0}</color> not found!",
            [LangKeys.Error.LimitBuilding.EntityMergeBlocked] = "You can't merge these buildings because the limit of <color=#FFA500>{1}</color> will be exceeded by <color=#FFA500>{0}</color>",
            [LangKeys.Error.LimitBuilding.EntityReached] = "You have reached the limit <color=#FFA500>{0}</color> of <color=#FFA500>{1}</color> <color=#FFA500>{2}</color> in this building",
            [LangKeys.Error.LimitBuilding.MergeBlocked] = "You can't merge these buildings because the limit of entities will be exceeded by <color=#FFA500>{0}</color>",
            [LangKeys.Error.LimitBuilding.Reached] = "You have reached the limit <color=#FFA500>{0}</color> of <color=#FFA500>{1}</color> entities in this building",
            [LangKeys.Error.LimitGlobal.EntityReached] = "You have reached the limit <color=#FFA500>{0}</color> of <color=#FFA500>{1}</color> <color=#FFA500>{2}</color>",
            [LangKeys.Error.LimitGlobal.Reached] = "You have reached the global limit <color=#FFA500>{0}</color> of <color=#FFA500>{1}</color> entities",
            [LangKeys.Error.NoPermission] = "You do not have permission to use this command!",
            [LangKeys.Format.Prefix] = "<color=#00FF00>[Limit Entities]</color>: ",
            [LangKeys.Info.Help] = "Get current limits: <color=#FFFF00>/{0}</color>",
            [LangKeys.Info.LimitBuilding] = "You have built <color=#FFA500>{0}</color> of <color=#FFA500>{1}</color> entities in this building",
            [LangKeys.Info.LimitBuildingEntity] = "You have built <color=#FFA500>{0}</color> of <color=#FFA500>{1}</color> <color=#FFA500>{2}</color> in this building",
            [LangKeys.Info.LimitGlobal] = "You have built <color=#FFA500>{0}</color> of <color=#FFA500>{1}</color> entities",
            [LangKeys.Info.LimitGlobalEntity] = "You have built <color=#FFA500>{0}</color> of <color=#FFA500>{1}</color> <color=#FFA500>{2}</color>",
            [LangKeys.Info.Limits] = "\nYour global limits are:\n<color=#FFA500>{0}</color>\nYour limits per building are:\n<color=#FFA500>{1}</color>",
            [LangKeys.Info.TotalAmount] = "Total amount: <color=#FFA500>{0}</color>",
            [LangKeys.Info.Unlimited] = "Your ability to build is unlimited",

            ["Foundations"] = "Foundations",
            ["Furnace"] = "Furnace",
            ["PlanterBoxes"] = "PlanterBoxes",
            ["Quarries"] = "Quarries",
            ["Roof"] = "Roof",
            ["TC"] = "TC",
        };

        private readonly Dictionary<string, string> _langRU = new()
        {
            [LangKeys.Error.EntityIsNotAllowed] = "Вам не разрешено строить <color=#FFA500>{0}</color>",
            [LangKeys.Error.PlayerNotFound] = "Игрок <color=#FFA500>{0}</color> не найден!",
            [LangKeys.Error.LimitBuilding.EntityMergeBlocked] = "Вы не можете объединить эти здания, поскольку лимит <color=#FFA500>{1}</color> будет превышен на <color=#FFA500>{0}</color>",
            [LangKeys.Error.LimitBuilding.EntityReached] = "Вы достигли лимита <color=#FFA500>{0}</color> из <color=#FFA500>{1}</color> для <color=#FFA500>{2}</color> в этом здании",
            [LangKeys.Error.LimitBuilding.MergeBlocked] = "Вы не можете объединить эти здания, поскольку лимит объектов будет превышен на <color=#FFA500>{0}</color>",
            [LangKeys.Error.LimitBuilding.Reached] = "Вы достигли лимита <color=#FFA500>{0}</color> из <color=#FFA500>{1}</color> объектов в этом здании",
            [LangKeys.Error.LimitGlobal.EntityReached] = "Вы достигли лимита <color=#FFA500>{0}</color> из <color=#FFA500>{1}</color> для <color=#FFA500>{2}</color>",
            [LangKeys.Error.LimitGlobal.Reached] = "Вы достигли глобального лимита <color=#FFA500>{0}</color> из <color=#FFA500>{1}</color> объектов",
            [LangKeys.Error.NoPermission] = "У вас нет разрешения на использование этой команды!",
            [LangKeys.Format.Prefix] = "<color=#00FF00>[Лимит построек]</color>: ",
            [LangKeys.Info.Help] = "Отобразить текущие лимиты: <color=#FFFF00>/{0}</color>",
            [LangKeys.Info.LimitBuilding] = "Вы построили <color=#FFA500>{0}</color> из <color=#FFA500>{1}</color> объектов в этом здании",
            [LangKeys.Info.LimitBuildingEntity] = "Вы построили <color=#FFA500>{0}</color> из <color=#FFA500>{1}</color> <color=#FFA500>{2}</color> в этом здании",
            [LangKeys.Info.LimitGlobal] = "Вы построили <color=#FFA500>{0}</color> из <color=#FFA500>{1}</color> объектов",
            [LangKeys.Info.LimitGlobalEntity] = "Вы построили <color=#FFA500>{0}</color> из <color=#FFA500>{1}</color> <color=#FFA500>{2}</color>",
            [LangKeys.Info.Limits] = "\nВаши глобальные лимиты:\n<color=#FFA500>{0}</color>\nВаши лимиты для постройки:\n<color=#FFA500>{1}</color>",
            [LangKeys.Info.TotalAmount] = "Общее количество: <color=#FFA500>{0}</color>",
            [LangKeys.Info.Unlimited] = "Ваши возможности строительства не ограничены",

            ["Foundations"] = "Фундамент",
            ["Furnace"] = "Печь",
            ["PlanterBoxes"] = "Плантация",
            ["Quarries"] = "Карьер",
            ["Roof"] = "Крыша",
            ["TC"] = "Шкаф с инструментами",
        };

        private readonly Dictionary<string, string> _langUK = new()
        {
            [LangKeys.Error.EntityIsNotAllowed] = "Вам не дозволено будувати <color=#FFA500>{0}</color>",
            [LangKeys.Error.PlayerNotFound] = "Гравеця <color=#FFA500>{0}</color> не знайдено!",
            [LangKeys.Error.LimitBuilding.EntityMergeBlocked] = "Ви не можете об'єднати ці будівлі, оскільки ліміт <color=#FFA500>{1}</color> буде перевищено на <color=#FFA500>{0}</color>",
            [LangKeys.Error.LimitBuilding.EntityReached] = "Ви досягли ліміту <color=#FFA500>{0}</color> із <color=#FFA500>{1}</color> для <color=#FFA500>{2}</color> у цій будівлі",
            [LangKeys.Error.LimitBuilding.MergeBlocked] = "Ви не можете об'єднати ці будівлі, оскільки ліміт об'єктів буде перевищено на <color=#FFA500>{0}</color>",
            [LangKeys.Error.LimitBuilding.Reached] = "Ви досягли ліміту <color=#FFA500>{0}</color> із <color=#FFA500>{1}</color> об'єктів в цьому будинку",
            [LangKeys.Error.LimitGlobal.EntityReached] = "Ви досягли ліміту <color=#FFA500>{0}</color> із <color=#FFA500>{1}</color> для <color=#FFA500>{2}</color>",
            [LangKeys.Error.LimitGlobal.Reached] = "Ви досягли глобального ліміту <color=#FFA500>{0}</color> із <color=#FFA500>{1}</color> об'єктів",
            [LangKeys.Error.NoPermission] = "У вас немає дозволу використовувати цю команду!",
            [LangKeys.Format.Prefix] = "<color=#00FF00>[Ліміт об'єктів]</color>: ",
            [LangKeys.Info.Help] = "Відобразити поточні ліміти: <color=#FFFF00>/{0}</color>",
            [LangKeys.Info.LimitBuilding] = "Ви побудували <color=#FFA500>{0}</color> із <color=#FFA500>{1}</color> об'єктів в цьому будинку",
            [LangKeys.Info.LimitBuildingEntity] = "Ви побудували <color=#FFA500>{0}</color> із <color=#FFA500>{1}</color> <color=#FFA500>{2}</color> в цьому будинку",
            [LangKeys.Info.LimitGlobal] = "Ви побудували <color=#FFA500>{0}</color> із <color=#FFA500>{1}</color> об'єктів",
            [LangKeys.Info.LimitGlobalEntity] = "Ви побудували <color=#FFA500>{0}</color> із <color=#FFA500>{1}</color> <color=#FFA500>{2}</color>",
            [LangKeys.Info.Limits] = "\nВаші глобальні ліміти:\n<color=#FFA500>{0}</color>\nВаші ліміти для будівництва:\n<color=#FFA500>{1}</color>",
            [LangKeys.Info.TotalAmount] = "Загальна кількість: <color=#FFA500>{0}</color>",
            [LangKeys.Info.Unlimited] = "Ваша можливість будувати необмежена",

            ["Foundations"] = "Фундамент",
            ["Furnace"] = "Піч",
            ["PlanterBoxes"] = "Плантація",
            ["Quarries"] = "Кар'єр",
            ["Roof"] = "Дах",
            ["TC"] = "Шафа з інструментами",
        };

        public void RegisterMessages()
        {
            lang.RegisterMessages(_langEN, this);
            lang.RegisterMessages(_langRU, this, "ru");
            lang.RegisterMessages(_langUK, this, "uk");
        }

        #endregion Localization

        #region Commands

        private void CmdLimitEntities(BasePlayer player, string cmd, string[] args)
        {
            if (!player.IsValid() || !player.userID.IsSteamId())
            {
                return;
            }

            BasePlayer target = player;

            if (args != null && args.Length > 0)
            {
                string strNameOrIDOrIP = args[0];

                target = BasePlayer.FindAwakeOrSleeping(strNameOrIDOrIP);

                if (target == null)
                {
                    PlayerSendMessage(player, Lang(LangKeys.Error.PlayerNotFound, player.UserIDString, strNameOrIDOrIP));
                    return;
                }
            }

            PlayerSendMessage(player, GetPlayerLimitString(player, target));
        }

        [ConsoleCommand("limitentities.list")]
        private void CmdLimitEntitiesList(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (player.IsValid() && !IsPlayerAdmin(player))
            {
                return;
            }

            StringBuilder sb = new();

            sb.AppendLine();
            sb.Append("All tracked entities list start");
            sb.AppendLine();

            foreach (uint trackedPrefabID in _cache.Prefabs.Tracked)
            {
                sb.AppendLine();
                sb.Append("PrefabID: ");
                sb.Append(trackedPrefabID);
                sb.AppendLine();
                sb.Append("PrefabShortName: ");
                sb.Append(_cache.Prefabs.ShortNames[trackedPrefabID]);
                sb.AppendLine();
                sb.Append("Prefab: ");
                sb.Append(StringPool.Get(trackedPrefabID));
                sb.AppendLine();
            }

            sb.AppendLine();
            sb.Append("All tracked entities list finish");
            sb.AppendLine();
            Log(sb.ToString(), LogLevel.Off);
            Puts($"Successfully listed {_cache.Prefabs.Tracked.Count} entities in the log file. You can find the log at: {Interface.Oxide.LogDirectory}");
        }

        #endregion Commands

        #region Oxide Hooks

        private object CanBuild(Planner planner, Construction entity, Construction.Target target) => HandleCanBuild(planner?.GetOwnerPlayer(), entity, target);

        private object OnPoweredLightsPointAdd(PoweredLightsDeployer deployer, BasePlayer player, Vector3 vector, Vector3 vector2) => HandlePoweredLightsAddPoint(deployer, player);

        private void OnBuildingMerge(ServerBuildingManager manager, Building to, Building from)
        {
            uint oldId = from.ID;
            uint newId = to.ID;

            NextTick(() =>
            {
                HandleBuildingСhange(oldId, newId, false);
            });
        }

        private void OnBuildingSplit(Building building, uint newId)
        {
            uint oldId = building.ID;
            NextTick(() => { HandleBuildingСhange(oldId, newId, true); });
        }

        private void OnPlayerConnected(BasePlayer player) => GetPlayerData(player.userID);

        private void OnEntitySpawned(BaseEntity entity)
        {
            if (!IsValidEntity(entity))
            {
                return;
            }

            Vector3 position = entity.transform.position;
            ulong ownerID = entity.OwnerID;
            uint prefabID = GetPrefabID(entity.prefabID);
            uint buildingID = AddBuildingEntity(entity);

            if (buildingID > 0 && !_storedData.BuildingsOwners.ContainsKey(buildingID))
            {
                _storedData.BuildingsOwners[buildingID] = ownerID;
                Log($"{position} {ownerID} Added new building {buildingID}", LogLevel.Debug);
            }

            PlayerData playerData = GetPlayerData(ownerID);
            playerData.AddEntity(GetPrefabID(entity.prefabID));

            if (_pluginConfig.LoggingLevel >= LogLevel.Debug)
            {
                PlayerEntities entities = playerData.Entities;
                Log($"{position} {ownerID} Increased player entity {GetShortName(prefabID)} count {entities.PlayersEntities[prefabID]}", LogLevel.Debug);
                Log($"{position} {ownerID} Increased player total entities count {entities.TotalCount}", LogLevel.Debug);
            }

            if ((_pluginConfig.ChatNotificationsEnabled || _pluginConfig.GameTipNotificationsEnabled)
                && _pluginConfig.WarnPercent > 0
                && !playerData.HasImmunity)
            {
                HandleEntityNotification(BasePlayer.FindByID(entity.OwnerID), playerData, _cache.Buildings[buildingID], prefabID);
            }
        }

        private void OnEntityKill(BaseEntity entity)
        {
            if (!IsValidEntity(entity))
            {
                return;
            }

            uint prefabID = GetPrefabID(entity.prefabID);
            ulong ownerID = entity.OwnerID;
            uint buildingID = RemoveBuildingEntity(entity);
            Vector3 position = entity.transform.position;

            if (entity is BuildingBlock)
            {
                if (BuildingManager.server.buildingDictionary.TryGetValue(buildingID, out Building building)
                && building.decayEntities.Count == 1
                && building.decayEntities.Contains(entity as DecayEntity))
                {
                    _storedData.BuildingsOwners.Remove(buildingID);
                    Log($"{position} {ownerID} Removed building {buildingID}", LogLevel.Debug);
                }
            }

            PlayerData playerData = GetPlayerData(ownerID);
            PlayerEntities entities = playerData.Entities;
            playerData.RemoveEntity(GetPrefabID(entity.prefabID));

            if (_pluginConfig.LoggingLevel >= LogLevel.Debug)
            {
                Log($"{position} {ownerID} Reduced player entity {GetShortName(prefabID)} count {entities.PlayersEntities[prefabID]}", LogLevel.Debug);
                Log($"{position} {ownerID} Reduced player total entities count {entities.TotalCount}", LogLevel.Debug);
            }
        }

        #endregion Oxide Hooks

        #region Permissions Hooks

        private void OnGroupCreated(string groupName) => OnGroupDeleted(groupName);

        private void OnGroupDeleted(string groupName)
        {
            foreach (string perm in _cache.Permissions.Registered)
            {
                if (permission.GroupHasPermission(groupName, perm))
                {
                    HandleGroup(groupName);
                    break;
                }
            }
        }

        private void OnGroupPermissionGranted(string groupName, string perm) => OnGroupPermissionRevoked(groupName, perm);

        private void OnGroupPermissionRevoked(string groupName, string perm)
        {
            if (!_cache.Permissions.Registered.Contains(perm))
            {
                return;
            }

            HandleGroup(groupName);
        }

        private void OnUserGroupAdded(string userIDString, string groupName) => OnUserGroupRemoved(userIDString, groupName);

        private void OnUserGroupRemoved(string userIDString, string groupName)
        {
            foreach (string perm in _cache.Permissions.Registered)
            {
                if (permission.GroupHasPermission(groupName, perm))
                {
                    HandleUserByString(userIDString);
                    break;
                }
            }
        }

        private void OnUserPermissionGranted(string userIDString, string perm) => OnUserPermissionRevoked(userIDString, perm);

        private void OnUserPermissionRevoked(string userIDString, string perm)
        {
            if (!_cache.Permissions.Registered.Contains(perm))
            {
                return;
            }

            HandleUserByString(userIDString);
        }

        #endregion Permissions Hooks

        #region Permissions Methods

        public void HandleGroup(string groupName)
        {
            foreach (string userIDString in permission.GetUsersInGroup(groupName))
            {
                HandleUserByString(userIDString.Substring(0, 17));
            }
        }

        public void HandleUserByString(string userIDString)
        {
            if (!ulong.TryParse(userIDString, out ulong userID))
            {
                return;
            }

            _cache.PlayerData[userID]?.UpdatePerms();
        }

        #endregion Permissions Methods

        #region Core Methods

        public void CacheGroupIds()
        {
            Log($"Cache creation for entities groups started", LogLevel.Debug);

            uint ID = StringPool.closest + 10000;

            foreach (EntityGroup entityGroup in _pluginConfig.EntityGroups)
            {
                foreach (string prefab in entityGroup.ListEntities)
                {
                    uint prefabID = StringPool.Get(prefab);
                    if (prefabID > 0)
                    {
                        entityGroup.ListEntitiesCache.Add(prefabID);
                    }
                }

                if (entityGroup.ListEntitiesCache.Count == 0)
                {
                    Log($"You have 0 valid entities in {entityGroup.Name} group in your config file! To get a list of all supported prefabs use console command limitentities.list", LogLevel.Error);
                    continue;
                }

                do
                {
                    ID++;
                } while (StringPool.toString.ContainsKey(ID));

                entityGroup.ID = ID;

                foreach (uint prefabID in entityGroup.ListEntitiesCache)
                {
                    _cache.Prefabs.Groups[prefabID] = ID;
                }

                _cache.Prefabs.ShortNames[ID] = entityGroup.Name;

                if (!_langEN.ContainsKey(entityGroup.Name))
                {
                    _langEN[entityGroup.Name] = entityGroup.Name;
                }
                if (!_langRU.ContainsKey(entityGroup.Name))
                {
                    _langRU[entityGroup.Name] = entityGroup.Name;
                }
                if (!_langUK.ContainsKey(entityGroup.Name))
                {
                    _langUK[entityGroup.Name] = entityGroup.Name;
                }
            }

            Log($"Cache creation for entities groups finished", LogLevel.Debug);
        }

        public void CachePermissions()
        {
            Dictionary<string, uint> groupStringPool = new();

            foreach (EntityGroup entityGroup in _pluginConfig.EntityGroups)
            {
                if (entityGroup.ID == 0)
                {
                    continue;
                }

                groupStringPool[entityGroup.Name] = entityGroup.ID;
            }

            foreach (PermissionEntry entry in _pluginConfig.Permissions)
            {
                foreach (KeyValuePair<string, int> entity in entry.LimitsBuilding.LimitsEntities)
                {
                    if (!groupStringPool.TryGetValue(entity.Key, out uint prefabID))
                    {
                        prefabID = StringPool.Get(entity.Key);
                    }
                    entry.LimitsBuilding.LimitEntitiesCache[prefabID] = entity.Value;
                }

                foreach (KeyValuePair<string, int> entity in entry.LimitsGlobal.LimitsEntities)
                {
                    if (!groupStringPool.TryGetValue(entity.Key, out uint prefabID))
                    {
                        prefabID = StringPool.Get(entity.Key);
                    }
                    entry.LimitsGlobal.LimitEntitiesCache[prefabID] = entity.Value;
                }
            }
        }

        public void CachePrefabIds()
        {
            Log("Cache creation started for deployables to be tracked", LogLevel.Debug);

            uint prefabID;

            for (int index = 0; index < ItemManager.itemList.Count; index++)
            {
                ItemDefinition def = ItemManager.itemList[index];
                BaseEntity entity = def.GetComponent<ItemModDeployable>()?.entityPrefab.Get().GetComponent<BaseEntity>();
                entity ??= def.GetComponent<ItemModEntity>()?.entityPrefab.Get().GetComponent<PoweredLightsDeployer>()?.poweredLightsPrefab.GetEntity();

                if (!entity
                || entity is GrowableEntity
                || _pluginConfig.Excluded.Contains(entity.PrefabName))
                {
                    continue;
                }

                _cache.Prefabs.Tracked.Add(entity.prefabID);

                if (!_cache.Prefabs.ShortNames.ContainsKey(entity.prefabID))
                {
                    _cache.Prefabs.ShortNames[entity.prefabID] = entity.ShortPrefabName;
                }
            }

            BaseEntity[] planner = ItemManager.FindItemDefinition("building.planner").GetComponent<ItemModEntity>().entityPrefab.GetEntity().GetComponent<Planner>()?.buildableList;
            foreach (BaseEntity entity in planner)
            {
                _cache.Prefabs.Tracked.Add(entity.prefabID);

                if (!_cache.Prefabs.ShortNames.ContainsKey(entity.prefabID))
                {
                    _cache.Prefabs.ShortNames[entity.prefabID] = entity.ShortPrefabName;
                }
                if (entity is BuildingBlock)
                {
                    _cache.Prefabs.BuildingBlocks.Add(entity.prefabID);
                }
            }

            Log($"Cache created for {_cache.Prefabs.Tracked.Count} deployables to be tracked", LogLevel.Debug);

            Log("All entities in config file check start", LogLevel.Debug);

            List<string> groupNames = Pool.Get<List<string>>();
            List<string> groupPrefabs = Pool.Get<List<string>>();
            List<string> prefabWrong = Pool.Get<List<string>>();

            foreach (EntityGroup entityGroup in _pluginConfig.EntityGroups)
            {
                if (entityGroup.ID == 0)
                {
                    continue;
                }

                if (groupNames.Contains(entityGroup.Name))
                {
                    Log($"Group names must be unique! Skipped: '{entityGroup.Name}'", LogLevel.Debug);
                    continue;
                }

                groupNames.Add(entityGroup.Name);
                groupPrefabs.AddRange(entityGroup.ListEntities);
            }

            foreach (PermissionEntry entry in _pluginConfig.Permissions)
            {
                foreach (string entityPrefab in entry.LimitsBuilding.LimitsEntities.Keys)
                {
                    if (groupPrefabs.Contains(entityPrefab))
                    {
                        Log($"You can't use the same prefabs in groups and individually. Choose one and remove it from the other '{entityPrefab}'", LogLevel.Error);
                        continue;
                    }

                    if (groupNames.Contains(entityPrefab))
                    {
                        continue;
                    }

                    prefabID = StringPool.Get(entityPrefab);

                    if (prefabID == 0 || !_cache.Prefabs.Tracked.Contains(prefabID))
                    {
                        Log($"prefabWrong {entityPrefab} ({prefabID} )", LogLevel.Debug);
                        prefabWrong.Add(entityPrefab);
                    }
                }

                foreach (string entityPrefab in entry.LimitsGlobal.LimitsEntities.Keys)
                {
                    if (groupPrefabs.Contains(entityPrefab))
                    {
                        Log($"You can't use the same prefabs in groups and individually. Choose one and remove it from the other '{entityPrefab}'", LogLevel.Error);
                        continue;
                    }

                    if (groupNames.Contains(entityPrefab))
                    {
                        continue;
                    }

                    prefabID = StringPool.Get(entityPrefab);

                    if (prefabID == 0 || !_cache.Prefabs.Tracked.Contains(prefabID))
                    {
                        prefabWrong.Add(entityPrefab);
                    }
                }
            }

            if (prefabWrong.Count > 0)
            {
                Log($"You have {prefabWrong.Count} untracked prefabs in your config file! To get a list of all supported prefabs use console command limitentities.list\n{string.Join("\n", prefabWrong)}", LogLevel.Error);
            }

            Pool.FreeUnmanaged(ref groupNames);
            Pool.FreeUnmanaged(ref groupPrefabs);
            Pool.FreeUnmanaged(ref prefabWrong);
            Log($"All entities in config file check finish", LogLevel.Debug);
        }

        public void CacheEntities()
        {
            Log("Cache creation started for all players entities on server.", LogLevel.Debug);

            int i = 0;
            int count = BaseNetworkable.serverEntities.Count;
            foreach (BaseEntity entity in BaseEntity.saveList)
            {
                if (++i == 1 || i % 10000 == 0 || i == count)
                {
                    Log($"{i} / {count}", LogLevel.Debug);
                }

                if (!IsValidEntity(entity))
                {
                    continue;
                }

                ulong ownerID = entity.OwnerID;
                AddBuildingEntity(entity);

                GetPlayerData(ownerID).AddEntity(GetPrefabID(entity.prefabID));
            }

            Log($"Cache created for {_cache.PlayerData.Count} players", LogLevel.Debug);
        }

        public uint GetPrefabID(uint prefabID)
        {
            uint id = _cache.Prefabs.Groups[prefabID];
            if (id == 0)
            {
                id = prefabID;
            }
            return id;
        }

        public PlayerData GetPlayerData(ulong playerId)
        {
            PlayerData playerData = _cache.PlayerData[playerId];
            if (playerData == null)
            {
                playerData = new(playerId);
                _cache.PlayerData[playerId] = playerData;
            }

            return playerData;
        }

        public PermissionEntry GetPlayerPermissions(PlayerData player)
        {
            for (int index = 0; index < _cache.Permissions.Descending.Length; index++)
            {
                PermissionEntry entry = _cache.Permissions.Descending[index];
                if (permission.UserHasPermission(player.PlayerIdString, entry.Permission))
                {
                    return entry;
                }
            }

            return null;
        }

        public BuildingEntities GetBuildingData(uint buildingID)
        {
            BuildingEntities buildingEntities = _cache.Buildings[buildingID];
            if (buildingEntities == null)
            {
                buildingEntities = new(buildingID);
                _cache.Buildings[buildingID] = buildingEntities;
            }

            return buildingEntities;
        }

        public bool IsMergeBlocked(Construction component, Construction.Target placement, BasePlayer player, PlayerData playerData, BuildingEntities buildingEntities)
        {
            GameObject gameObject = GameManager.server.CreatePrefab(component.fullName, Vector3.zero, Quaternion.identity, false);
            component.UpdatePlacement(gameObject.transform, component, ref placement);
            BaseEntity baseEntity = gameObject.ToBaseEntity();
            OBB oBB = baseEntity.WorldSpaceBounds();

            if (!baseEntity.IsValid())
            {
                GameManager.Destroy(gameObject);
            }
            else
            {
                baseEntity.Kill(BaseNetworkable.DestroyMode.None);
            }

            bool mergeBlocked = false;
            List<uint> processedBuildings = Pool.Get<List<uint>>();
            processedBuildings.Add(buildingEntities.BuildingID);
            List<BuildingBlock> adjoiningBlocks = Pool.Get<List<BuildingBlock>>();
            Vis.Entities(oBB.position, oBB.extents.magnitude + 1f, adjoiningBlocks, -1, QueryTriggerInteraction.Collide);

            if (adjoiningBlocks.Count > 0)
            {
                Hash<uint, int> limitEntitiesCache = playerData.Perms.LimitsBuilding.LimitEntitiesCache;

                int allowedBuildingTotal = playerData.Perms.LimitsBuilding.LimitTotal - buildingEntities.EntitiesIds.Count;
                Hash<uint, int> allowedBuildingEntities = new();

                foreach (BuildingBlock adjoiningBlock in adjoiningBlocks)
                {
                    if (processedBuildings.Contains(adjoiningBlock.buildingID)
                    || _cache.Buildings[adjoiningBlock.buildingID] is not BuildingEntities adjoiningBuilding)
                    {
                        continue;
                    }


                    foreach (KeyValuePair<uint, int> adjoiningEntity in adjoiningBuilding.EntitiesCount)
                    {
                        allowedBuildingTotal -= adjoiningEntity.Value;
                        if (allowedBuildingTotal < 0)
                        {
                            Log($"{oBB.position} {playerData.PlayerIdString} prevented from merge building {buildingEntities.BuildingID} Limit Building Total", LogLevel.Debug);
                            HandleNotification(player, Lang(LangKeys.Error.LimitBuilding.MergeBlocked, playerData.PlayerIdString, allowedBuildingTotal * -1), true);
                            mergeBlocked = true;
                            break;
                        }

                        if (!limitEntitiesCache.TryGetValue(adjoiningEntity.Key, out int limitEntity))
                        {// Entity is not limited
                            continue;
                        }

                        if (!allowedBuildingEntities.ContainsKey(adjoiningEntity.Key))
                        {
                            allowedBuildingEntities[adjoiningEntity.Key] = limitEntity - buildingEntities.EntitiesCount[adjoiningEntity.Key];
                        }

                        int count = allowedBuildingEntities[adjoiningEntity.Key] -= adjoiningEntity.Value;
                        if (count < 0)
                        {
                            Log($"{oBB.position} {playerData.PlayerIdString} prevented from building merge block entity {GetShortName(adjoiningEntity.Key)} in building {buildingEntities.BuildingID}", LogLevel.Debug);
                            HandleNotification(player, Lang(LangKeys.Error.LimitBuilding.EntityMergeBlocked, playerData.PlayerIdString, count * -1, GetItemDisplayName(adjoiningEntity.Key, playerData.PlayerIdString)), true);
                            mergeBlocked = true;
                            break;
                        }
                    }
                    if (mergeBlocked)
                    {
                        break;
                    }
                    processedBuildings.Add(adjoiningBlock.buildingID);
                }
            }
            Pool.FreeUnmanaged(ref adjoiningBlocks);
            Pool.FreeUnmanaged(ref processedBuildings);

            return mergeBlocked;
        }

        public bool IsValidEntity(BaseEntity entity) => entity.IsValid() && entity.OwnerID.IsSteamId() && _cache.Prefabs.Tracked.Contains(entity.prefabID);

        public object HandleCanBuild(BasePlayer player, Construction component, Construction.Target placement)
        {
            if (!player.IsValid()
            || !player.userID.IsSteamId()
            || !_cache.Prefabs.Tracked.Contains(component.prefabID))
            {
                return null;
            }

            PlayerData playerData = GetPlayerData(player.userID);
            if (playerData.Perms == null || playerData.HasImmunity)
            {
                return null;
            }

            PlayerEntities entities = playerData.Entities;

            Vector3 position = placement.entity.IsValid() && placement.socket ? placement.GetWorldPosition() : placement.position;
            uint prefabID = GetPrefabID(component.prefabID);
            if (!playerData.CanBuild())
            {
                Log($"{position} {playerData.PlayerIdString} prevented from building entity {GetShortName(prefabID)} cannot build", LogLevel.Debug);
                HandleNotification(player, Lang(LangKeys.Error.EntityIsNotAllowed, playerData.PlayerIdString, GetItemDisplayName(prefabID, playerData.PlayerIdString)), true);
                return _static.False;
            }

            if (playerData.IsGlobalLimit())
            {
                Log($"{position} {playerData.PlayerIdString} prevented from building entity {GetShortName(prefabID)} global limit", LogLevel.Debug);
                HandleNotification(player, Lang(LangKeys.Error.LimitGlobal.Reached, playerData.PlayerIdString, playerData.Entities.TotalCount, playerData.Perms.LimitsGlobal.LimitTotal), true);
                return _static.False;
            }

            if (playerData.IsGlobalLimit(prefabID))
            {
                Log($"{position} {playerData.PlayerIdString} prevented from building entity {GetShortName(prefabID)} global entity limit", LogLevel.Debug);
                HandleNotification(player, Lang(LangKeys.Error.LimitGlobal.EntityReached, playerData.PlayerIdString, entities.PlayersEntities[prefabID], playerData.Perms.LimitsGlobal.LimitEntitiesCache[prefabID], GetItemDisplayName(prefabID, playerData.PlayerIdString)), true);
                return _static.False;
            }

            uint buildingID = GetBuildingID(placement);
            BuildingEntities building = _cache.Buildings[buildingID];
            if (building != null)
            {
                if (playerData.IsBuildingLimit(building))
                {
                    Log($"{position} {playerData.PlayerIdString} prevented from building entity {GetShortName(prefabID)} in building {buildingID}", LogLevel.Debug);
                    HandleNotification(player, Lang(LangKeys.Error.LimitBuilding.Reached, playerData.PlayerIdString, building.EntitiesIds.Count, playerData.Perms.LimitsBuilding.LimitTotal), true);
                    return _static.False;
                }

                if (playerData.IsBuildingLimit(building, prefabID))
                {
                    Log($"{position} {playerData.PlayerIdString} prevented from building entity {GetShortName(prefabID)} in building {buildingID}", LogLevel.Debug);
                    HandleNotification(player, Lang(LangKeys.Error.LimitBuilding.EntityReached, playerData.PlayerIdString, building.EntitiesCount[prefabID], playerData.Perms.LimitsBuilding.LimitEntitiesCache[prefabID], GetItemDisplayName(prefabID, playerData.PlayerIdString)), true);
                    return _static.False;
                }

                if (playerData.Perms.MergingCheck
                && _cache.Prefabs.BuildingBlocks.Contains(component.prefabID)
                && IsMergeBlocked(component, placement, player, playerData, building))
                {
                    return _static.False;
                }
            }

            return null;
        }

        public object HandlePoweredLightsAddPoint(PoweredLightsDeployer deployer, BasePlayer player)
        {
            if (deployer.active == null)
            {
                BaseEntity baseEntity = deployer.poweredLightsPrefab.GetEntity();

                if (!_cache.Prefabs.Tracked.Contains(baseEntity.prefabID))
                {
                    return null;
                }

                PlayerData playerData = GetPlayerData(player.userID);
                if (playerData.Perms == null || playerData.HasImmunity)
                {
                    return null;
                }

                PlayerEntities entities = playerData.Entities;

                Vector3 position = baseEntity.transform.position;
                uint prefabID = GetPrefabID(baseEntity.prefabID);
                if (!playerData.CanBuild())
                {
                    Log($"{position} {playerData.PlayerIdString} prevented from building entity {GetShortName(prefabID)} cannot build", LogLevel.Debug);
                    HandleNotification(player, Lang(LangKeys.Error.EntityIsNotAllowed, playerData.PlayerIdString, GetItemDisplayName(prefabID, playerData.PlayerIdString)), true);
                    return _static.False;
                }

                if (playerData.IsGlobalLimit())
                {
                    Log($"{position} {playerData.PlayerIdString} prevented from building entity {GetShortName(prefabID)} global limit", LogLevel.Debug);
                    HandleNotification(player, Lang(LangKeys.Error.LimitGlobal.Reached, playerData.PlayerIdString, playerData.Entities.TotalCount, playerData.Perms.LimitsGlobal.LimitTotal), true);
                    return _static.False;
                }

                if (playerData.IsGlobalLimit(prefabID))
                {
                    Log($"{position} {playerData.PlayerIdString} prevented from building entity {GetShortName(prefabID)} global entity limit", LogLevel.Debug);
                    HandleNotification(player, Lang(LangKeys.Error.LimitGlobal.EntityReached, playerData.PlayerIdString, entities.PlayersEntities[prefabID], playerData.Perms.LimitsGlobal.LimitEntitiesCache[prefabID], GetItemDisplayName(prefabID, playerData.PlayerIdString)), true);
                    return _static.False;
                }

                uint buildingID = GetBuildingID(baseEntity);
                BuildingEntities building = _cache.Buildings[buildingID];
                if (building != null)
                {
                    if (playerData.IsBuildingLimit(building))
                    {
                        Log($"{position} {playerData.PlayerIdString} prevented from building entity {GetShortName(prefabID)} in building {buildingID}", LogLevel.Debug);
                        HandleNotification(player, Lang(LangKeys.Error.LimitBuilding.Reached, playerData.PlayerIdString, building.EntitiesIds.Count, playerData.Perms.LimitsBuilding.LimitTotal), true);
                        return _static.False;
                    }

                    if (playerData.IsBuildingLimit(building, prefabID))
                    {
                        Log($"{position} {playerData.PlayerIdString} prevented from building entity {GetShortName(prefabID)} in building {buildingID}", LogLevel.Debug);
                        HandleNotification(player, Lang(LangKeys.Error.LimitBuilding.EntityReached, playerData.PlayerIdString, building.EntitiesCount[prefabID], playerData.Perms.LimitsBuilding.LimitEntitiesCache[prefabID], GetItemDisplayName(prefabID, playerData.PlayerIdString)), true);
                        return _static.False;
                    }
                }
            }
            else if (!deployer.active.OwnerID.IsSteamId())
            {
                deployer.active.OwnerID = player.userID;
                deployer.active.SendNetworkUpdate();
                OnEntitySpawned(deployer.active);
            }

            return null;
        }

        public void HandleBuildingСhange(uint oldId, uint newId, bool split)
        {
            ulong ownerId = _storedData.BuildingsOwners[oldId];
            if (!ownerId.IsSteamId())
            {
                return;
            }
            if (!BuildingManager.server.buildingDictionary.ContainsKey(newId))
            {
                return;
            }
            _storedData.BuildingsOwners[newId] = ownerId;

            if (_pluginConfig.LoggingLevel >= LogLevel.Debug)
            {
                string mode = split ? "split" : "merge";
                Log($"{BuildingManager.server.buildingDictionary[newId].buildingBlocks[0].ServerPosition} Building {mode}. Saved the owner {ownerId} of old building {oldId} to new building {newId}", LogLevel.Debug);
            }

            BuildingEntities entitiesOld = _cache.Buildings[oldId];
            if (entitiesOld == null)
            {
                return;
            }

            uint currentBuildingId;
            List<BaseEntity> movedEntites = Pool.Get<List<BaseEntity>>();
            foreach (ulong id in entitiesOld.EntitiesIds)
            {
                BaseEntity entity = BaseNetworkable.serverEntities.Find(new(id)) as BaseEntity;
                if (!entity.IsValid())
                {
                    continue;
                }

                currentBuildingId = GetBuildingID(entity);
                if (currentBuildingId == oldId || currentBuildingId == 0)
                {
                    continue;
                }

                movedEntites.Add(entity);
            }

            foreach (BaseEntity entity in movedEntites)
            {
                entitiesOld.RemoveEntity(entity);
                if (_pluginConfig.LoggingLevel >= LogLevel.Debug)
                {
                    Vector3 position = entity.transform.position;
                    uint prefabID = GetPrefabID(entity.prefabID);
                    Log($"{position} {ownerId} Reduced building {oldId} entities {GetShortName(prefabID)} count {entitiesOld.EntitiesCount[prefabID]}", LogLevel.Debug);
                    Log($"{position} {ownerId} Reduced building {oldId} total entities count {entitiesOld.EntitiesIds.Count}", LogLevel.Debug);
                }

                AddBuildingEntity(entity);
            }

            Pool.FreeUnmanaged(ref movedEntites);
        }

        public uint AddBuildingEntity(BaseEntity entity)
        {
            uint buildingId = GetBuildingID(entity);
            if (buildingId == 0)
            {
                Log($"Failed to get building for {entity} {entity.GetType().Name} {entity.transform.position}", LogLevel.Debug);
                return 0;
            }

            BuildingEntities buildingEntities = GetBuildingData(buildingId);
            buildingEntities.AddEntity(entity);

            if (_pluginConfig.LoggingLevel >= LogLevel.Debug)
            {
                Vector3 position = entity.transform.position;
                uint prefabID = GetPrefabID(entity.prefabID);
                Log($"{position} {entity.OwnerID} Increased building {buildingId} entity {GetShortName(prefabID)} count {buildingEntities.EntitiesCount[prefabID]}", LogLevel.Debug);
                Log($"{position} {entity.OwnerID} Increased building {buildingId} total entities count {buildingEntities.EntitiesIds.Count}", LogLevel.Debug);
            }

            return buildingId;
        }

        public uint RemoveBuildingEntity(BaseEntity entity)
        {
            uint buildingId = GetBuildingID(entity);
            if (buildingId == 0)
            {
                return 0;
            }

            BuildingEntities buildingEntities = _cache.Buildings[buildingId];
            if (buildingEntities == null)
            {
                return buildingId;
            }

            buildingEntities.RemoveEntity(entity);

            if (_pluginConfig.LoggingLevel >= LogLevel.Debug)
            {
                Vector3 position = entity.transform.position;
                uint prefabID = GetPrefabID(entity.prefabID);
                Log($"{position} {entity.OwnerID} Reduced building {buildingId} entities {GetShortName(prefabID)} count {buildingEntities.EntitiesCount[prefabID]}", LogLevel.Debug);
                Log($"{position} {entity.OwnerID} Reduced building {buildingId} total entities count {buildingEntities.EntitiesIds.Count}", LogLevel.Debug);
            }

            return buildingId;
        }

        public uint GetBuildingID(Construction.Target target)
        {
            BaseEntity entity = target.entity;
            if (entity.IsValid())
            {
                if (entity is DecayEntity decayEntity)
                {
                    return decayEntity.buildingID;
                }

                return GetBuildingID(target.socket ? target.GetWorldPosition() : target.position);
            }

            return GetBuildingID(target.position);
        }

        public uint GetBuildingID(BaseEntity entity)
        {
            if (entity is DecayEntity decayEntity)
            {
                return decayEntity.buildingID;
            }

            return GetBuildingID(entity.transform.position);
        }

        public uint GetBuildingID(Vector3 position)
        {
            _cache.Blocks.Clear();
            GamePhysics.OverlapSphere(position, 16f, _cache.Blocks, Rust.Layers.Construction);

            if (_cache.Blocks.Count == 0)
            {
                return 0;
            }

            BuildingBlock oldestBlock = _cache.Blocks[0];
            uint buildingId = oldestBlock.buildingID;
            for (int index = 1; index < _cache.Blocks.Count; index++)
            {
                BuildingBlock block = _cache.Blocks[index];
                if (block.IsOlderThan(oldestBlock))
                {
                    oldestBlock = block;
                    buildingId = block.buildingID;
                }
            }

            return buildingId;
        }

        public void HandleEntityNotification(BasePlayer player, PlayerData playerData, BuildingEntities building, uint prefabID)
        {
            if (!player.IsValid() || player.IsDead() || !player.IsConnected || _pluginConfig.WarnPercent <= 0)
            {
                return;
            }

            if (building != null && playerData.GetBuildingPercentage(building, prefabID) >= _pluginConfig.WarnPercent)
            {
                HandleNotification(player, Lang(LangKeys.Info.LimitBuildingEntity, playerData.PlayerIdString, building.EntitiesCount[prefabID], playerData.Perms.LimitsBuilding.LimitEntitiesCache[prefabID], GetItemDisplayName(prefabID, playerData.PlayerIdString)));
            }
            else if (building != null && playerData.GetBuildingPercentage(building) >= _pluginConfig.WarnPercent)
            {
                HandleNotification(player, Lang(LangKeys.Info.LimitBuilding, playerData.PlayerIdString, building.EntitiesIds.Count, playerData.Perms.LimitsBuilding.LimitTotal));
            }
            else if (playerData.GetGlobalPercentage(prefabID) >= _pluginConfig.WarnPercent)
            {
                HandleNotification(player, Lang(LangKeys.Info.LimitGlobalEntity, playerData.PlayerIdString, playerData.Entities.PlayersEntities[prefabID], playerData.Perms.LimitsGlobal.LimitEntitiesCache[prefabID], GetItemDisplayName(prefabID, playerData.PlayerIdString)));
            }
            else if (playerData.GetGlobalPercentage() >= _pluginConfig.WarnPercent)
            {
                HandleNotification(player, Lang(LangKeys.Info.LimitGlobal, playerData.PlayerIdString, playerData.Entities.TotalCount, playerData.Perms.LimitsGlobal.LimitTotal));
            }
        }

        public string GetPlayerLimitString(BasePlayer player, BasePlayer target)
        {
            if (_cache.PlayerData[target.userID] is not PlayerData playerData
            || playerData.Perms == null || playerData.HasImmunity)
            {
                return Lang(LangKeys.Info.Unlimited, player.UserIDString);
            }

            PlayerEntities entities = playerData.Entities;
            PermissionEntry perms = playerData.Perms;

            _cache.StringBuilders.LimitsGlobal.Clear();
            _cache.StringBuilders.LimitsGlobal.AppendLine(Lang(LangKeys.Info.TotalAmount, player.UserIDString, $"{entities.TotalCount} / {perms.LimitsGlobal.LimitTotal}"));
            foreach (KeyValuePair<uint, int> limitEntry in perms.LimitsGlobal.LimitEntitiesCache)
            {
                uint prefabID = GetPrefabID(limitEntry.Key);
                _cache.StringBuilders.LimitsGlobal.AppendLine();
                _cache.StringBuilders.LimitsGlobal.Append(GetItemDisplayName(prefabID, player.UserIDString));
                _cache.StringBuilders.LimitsGlobal.Append("  ");
                _cache.StringBuilders.LimitsGlobal.Append(entities?.PlayersEntities[prefabID]);
                _cache.StringBuilders.LimitsGlobal.Append(" / ");
                _cache.StringBuilders.LimitsGlobal.Append(limitEntry.Value);
            }

            _cache.StringBuilders.LimitsGlobal.AppendLine();

            _cache.StringBuilders.LimitsBuilding.Clear();
            _cache.StringBuilders.LimitsBuilding.AppendLine(Lang(LangKeys.Info.TotalAmount, player.UserIDString, perms.LimitsBuilding.LimitTotal));
            foreach (KeyValuePair<uint, int> limitEntry in perms.LimitsBuilding.LimitEntitiesCache)
            {
                _cache.StringBuilders.LimitsBuilding.AppendLine();
                _cache.StringBuilders.LimitsBuilding.Append(GetItemDisplayName(GetPrefabID(limitEntry.Key), player.UserIDString));
                _cache.StringBuilders.LimitsBuilding.Append("  ");
                _cache.StringBuilders.LimitsBuilding.Append(limitEntry.Value);
            }

            return Lang(LangKeys.Info.Limits, player.UserIDString, _cache.StringBuilders.LimitsGlobal.ToString(), _cache.StringBuilders.LimitsBuilding.ToString());
        }

        #endregion Core Methods

        #region API Methods

        private ulong API_GetBuildingOwner(uint buildingID) => _storedData.BuildingsOwners[buildingID];

        private ulong API_GetBuildingOwner(BaseEntity entity) => _storedData.BuildingsOwners[GetBuildingID(entity)];

        private ulong API_GetBuildingOwner(Vector3 position) => _storedData.BuildingsOwners[GetBuildingID(position)];

        private void API_RemoveBuildingOwner(List<BaseEntity> entitiesList)
        {
            uint buildingID;
            foreach (BaseEntity entity in entitiesList)
            {
                if (!entity.OwnerID.IsSteamId())
                {
                    continue;
                }

                PlayerData playerData = GetPlayerData(entity.OwnerID);
                if (playerData.Perms == null || playerData.HasImmunity)
                {
                    continue;
                }

                playerData.RemoveEntity(GetPrefabID(entity.prefabID));

                buildingID = GetBuildingID(entity);
                if (buildingID > 0)
                {
                    _cache.Buildings.Remove(buildingID);
                    _storedData.BuildingsOwners.Remove(buildingID);
                    DataSave();
                }
                entity.OwnerID = 0;
                Log($"API_RemoveBuildingOwner: Owner of {entity} was set to 0", LogLevel.Debug);
            }
        }

        private HashSet<ulong> API_ChangeBuildingOwner(List<BaseEntity> entitiesList, ulong newOwner)
        {
            if (!newOwner.IsSteamId())
            {
                return null;
            }

            HashSet<ulong> limitedEntitiesIds = new();

            PlayerData playerData = GetPlayerData(newOwner);
            if (playerData.Perms == null || playerData.HasImmunity)
            {
                return limitedEntitiesIds;
            }

            Hash<uint, Hash<uint, int>> newEntities = new();
            Hash<uint, HashSet<ulong>> newEntitiesIds = new();
            Hash<uint, int> newBuildingTotal = new();
            int newTotal = playerData.Perms.LimitsBuilding.LimitTotal - playerData.Entities.TotalCount;

            Hash<uint, int> limitEntities = playerData.Perms.LimitsGlobal.LimitEntitiesCache;
            Hash<uint, int> limitBuildingEntities = playerData.Perms.LimitsBuilding.LimitEntitiesCache;

            foreach (BaseEntity entity in entitiesList)
            {
                if (!entity.IsValid())
                {
                    Log($"API_ChangeBuildingOwner: {newOwner} prevented from ChangeBuildingOwner invalid entity {entity}", LogLevel.Error);
                    return null;
                }

                uint buildingID = GetBuildingID(entity);
                uint prefabID = GetPrefabID(entity.prefabID);
                ulong networkableId = entity.net.ID.Value;
                if (!newBuildingTotal.ContainsKey(buildingID))
                {
                    newBuildingTotal[buildingID] = playerData.Perms.LimitsBuilding.LimitTotal;
                }

                if (++newTotal + playerData.Entities.TotalCount > playerData.Perms.LimitsGlobal.LimitTotal)
                {
                    if (entity is BuildingBlock)
                    {
                        Log($"API_ChangeBuildingOwner: {newOwner} prevented from ChangeBuildingOwner {buildingID} Limit Global Total", LogLevel.Debug);
                        return null;
                    }

                    newTotal--;
                    limitedEntitiesIds.Add(networkableId);
                }

                Hash<uint, int> newBuildingEntities = newEntities[buildingID];
                if (newBuildingEntities == null)
                {
                    newBuildingEntities = new();
                    newEntities[buildingID] = newBuildingEntities;
                }
                int count = newBuildingEntities[prefabID]++;

                if (limitEntities.ContainsKey(prefabID) && count > limitEntities[prefabID])
                {
                    if (entity is BuildingBlock)
                    {
                        Log($"API_ChangeBuildingOwner: {newOwner} prevented from ChangeBuildingOwner {buildingID} Limit Global entity {entity}", LogLevel.Debug);
                        return null;
                    }

                    newBuildingEntities[prefabID]--;
                    limitedEntitiesIds.Add(networkableId);
                }
                else if (buildingID > 0 && ++newBuildingTotal[buildingID] > playerData.Perms.LimitsBuilding.LimitTotal)
                {
                    if (entity is BuildingBlock)
                    {
                        Log($"API_ChangeBuildingOwner: {newOwner} prevented from ChangeBuildingOwner {buildingID} Limit Building Total", LogLevel.Debug);
                        return null;
                    }

                    newBuildingTotal[buildingID]--;
                    limitedEntitiesIds.Add(networkableId);
                }
                else if (buildingID > 0 && limitBuildingEntities.ContainsKey(prefabID) && count > limitBuildingEntities[prefabID])
                {
                    if (entity is BuildingBlock)
                    {
                        Log($"API_ChangeBuildingOwner: {newOwner} prevented from ChangeBuildingOwner {buildingID} Limit Building entity {entity}", LogLevel.Debug);
                        return null;
                    }

                    newBuildingEntities[prefabID]--;
                    limitedEntitiesIds.Add(networkableId);
                }
                if (!limitedEntitiesIds.Contains(networkableId))
                {
                    newEntitiesIds[buildingID].Add(networkableId);
                }
            }

            foreach (KeyValuePair<uint, Hash<uint, int>> building in newEntities)
            {
                if (building.Key > 0)
                {
                    BuildingEntities buildingEntities = GetBuildingData(building.Key);
                    buildingEntities.EntitiesIds.UnionWith(newEntitiesIds[building.Key]);
                    foreach (KeyValuePair<uint, int> entity in building.Value)
                    {
                        buildingEntities.AddRange(entity.Key, entity.Value);
                        Log($"API_ChangeBuildingOwner: added {GetShortName(entity.Key)} x {entity.Value} to building {building.Key}", LogLevel.Debug);
                    }

                    _storedData.BuildingsOwners[building.Key] = newOwner;
                    DataSave();
                    Log($"API_ChangeBuildingOwner: owner changed to {newOwner} for building {building.Key}", LogLevel.Debug);
                }

                foreach (KeyValuePair<uint, int> entity in building.Value)
                {
                    playerData.Entities.AddRange(entity.Key, entity.Value);
                    Log($"API_ChangeBuildingOwner: Increased player entity {GetShortName(entity.Key)} count for {entity.Value}", LogLevel.Debug);
                }
            }

            return limitedEntitiesIds;
        }

        #endregion API Methods

        #region Helpers

        public void HooksUnsubscribe()
        {
            Unsubscribe(nameof(CanBuild));
            Unsubscribe(nameof(OnBuildingMerge));
            Unsubscribe(nameof(OnBuildingSplit));
            Unsubscribe(nameof(OnEntityKill));
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnGroupCreated));
            Unsubscribe(nameof(OnGroupDeleted));
            Unsubscribe(nameof(OnGroupPermissionGranted));
            Unsubscribe(nameof(OnGroupPermissionRevoked));
            Unsubscribe(nameof(OnPlayerConnected));
            Unsubscribe(nameof(OnPoweredLightsPointAdd));
            Unsubscribe(nameof(OnUserGroupAdded));
            Unsubscribe(nameof(OnUserGroupRemoved));
            Unsubscribe(nameof(OnUserPermissionGranted));
            Unsubscribe(nameof(OnUserPermissionRevoked));
        }

        public void HooksSubscribe()
        {
            Subscribe(nameof(CanBuild));
            Subscribe(nameof(OnBuildingMerge));
            Subscribe(nameof(OnBuildingSplit));
            Subscribe(nameof(OnEntityKill));
            Subscribe(nameof(OnEntitySpawned));
            Subscribe(nameof(OnGroupCreated));
            Subscribe(nameof(OnGroupDeleted));
            Subscribe(nameof(OnGroupPermissionGranted));
            Subscribe(nameof(OnGroupPermissionRevoked));
            Subscribe(nameof(OnPlayerConnected));
            Subscribe(nameof(OnPoweredLightsPointAdd));
            Subscribe(nameof(OnUserGroupAdded));
            Subscribe(nameof(OnUserGroupRemoved));
            Subscribe(nameof(OnUserPermissionGranted));
            Subscribe(nameof(OnUserPermissionRevoked));
        }

        public void RegisterPermissions()
        {
            permission.RegisterPermission(PermissionAdmin, this);
            permission.RegisterPermission(PermissionImmunity, this);

            List<PermissionEntry> entries = Pool.Get<List<PermissionEntry>>();
            List<string> perms = Pool.Get<List<string>>();
            perms.Add(PermissionImmunity);

            foreach (PermissionEntry entry in _pluginConfig.Permissions)
            {
                if (string.IsNullOrWhiteSpace(entry.Permission))
                {
                    Log("You have empty 'Permission' in config file! Skipped.", LogLevel.Error);
                    continue;
                }
                if (!permission.PermissionExists(entry.Permission))
                {
                    permission.RegisterPermission(entry.Permission, this);
                }
                if (!perms.Contains(entry.Permission))
                {
                    perms.Add(entry.Permission);
                }
                entries.Add(entry);
            }

            entries.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            _cache.Permissions.Descending = entries.ToArray();

            perms.Sort();
            _cache.Permissions.Registered = perms.ToArray();

            Pool.FreeUnmanaged(ref entries);
            Pool.FreeUnmanaged(ref perms);
        }

        public void AddCommands()
        {
            foreach (string command in _pluginConfig.Commands)
            {
                cmd.AddChatCommand(command, this, nameof(CmdLimitEntities));
            }
        }

        public bool IsPluginLoaded(Plugin plugin) => plugin != null && plugin.IsLoaded;

        public bool IsPlayerAdmin(BasePlayer player) => player.IsAdmin || permission.UserHasPermission(player.UserIDString, PermissionAdmin);

        public void HandleNotification(BasePlayer player, string message, bool isWarning = false)
        {
            Log($"{player.displayName} {StripRustTags(message)}", isWarning ? LogLevel.Warning : LogLevel.Info);

            if (_pluginConfig.ChatNotificationsEnabled)
            {
                PlayerSendMessage(player, message + "\n\n" + Lang(LangKeys.Info.Help, player.UserIDString, _pluginConfig.Commands[0]));
            }

            if (_pluginConfig.GameTipNotificationsEnabled)
            {
                PlayerSendGameTip(player, message, isWarning);
            }
        }

        public void Log(string text, LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Error:
                    PrintError(text);
                    break;
                case LogLevel.Warning:
                    PrintWarning(text);
                    break;
                case LogLevel.Off:
                    LogToFile("log", $"\n{DateTime.Now:HH:mm:ss} {text}\n", this);
                    return;
            }

            if ((int)_pluginConfig.LoggingLevel >= (int)level)
            {
                LogToFile("log", $"{DateTime.Now:HH:mm:ss} {text}", this);
            }
        }

        public void PlayerSendMessage(BasePlayer player, string message) => player.SendConsoleCommand("chat.add", 2, _pluginConfig.SteamIDIcon, $"{Lang(LangKeys.Format.Prefix, player.UserIDString)}{message}");

        public void PlayerSendGameTip(BasePlayer player, string message, bool isWarning = false) => player.SendConsoleCommand("showtoast", isWarning ? (int)GameTip.Styles.Red_Normal : (int)GameTip.Styles.Blue_Long, message, false);

        public string GetShortName(uint prefabId)
        {
            string name = _cache.Prefabs.ShortNames[prefabId];
            if (string.IsNullOrWhiteSpace(name))
            {
                name = StringPool.Get(prefabId);
                if (string.IsNullOrWhiteSpace(name))
                {
                    Log($"The string for {prefabId} was not found in StringPool", LogLevel.Warning);
                    return string.Empty;
                }
                int startIndex = name.LastIndexOf("/");
                name = name.Substring(startIndex + 1, name.Length - startIndex - 8);
                _cache.Prefabs.ShortNames[prefabId] = name;
            }

            return name;
        }

        public string GetItemDisplayName(uint prefabID, string userIDString)
        {
            string language = lang.GetLanguage(userIDString);
            /*
            if (string.IsNullOrWhiteSpace(language))
            {
                language = lang.GetServerLanguage();
            }
            */
            Hash<uint, string> displayNames = _cache.DisplayNames[language];
            if (displayNames == null)
            {
                displayNames = new();
                _cache.DisplayNames[language] = displayNames;
            }

            prefabID = GetPrefabID(prefabID);

            string itemDisplayName = displayNames[prefabID];

            if (string.IsNullOrWhiteSpace(itemDisplayName))
            {
                string itemShortName = GetShortName(prefabID);

                if (_cache.Prefabs.Groups.Values.Contains(prefabID))
                {
                    itemDisplayName = Lang(itemShortName, userIDString);
                    displayNames[prefabID] = itemDisplayName;
                    return itemDisplayName;
                }

                if (string.IsNullOrWhiteSpace(itemShortName) || !IsPluginLoaded(RustTranslationAPI))
                {
                    return itemShortName;
                }

                itemDisplayName = RustTranslationAPI.Call<string>("GetItemTranslationByShortName", language, itemShortName);

                if (string.IsNullOrWhiteSpace(itemDisplayName))
                {
                    itemDisplayName = RustTranslationAPI.Call<string>("GetDeployableTranslation", language, itemShortName);

                    if (string.IsNullOrWhiteSpace(itemDisplayName))
                    {
                        itemDisplayName = RustTranslationAPI.Call<string>("GetConstructionTranslation", language, itemShortName);

                        if (string.IsNullOrWhiteSpace(itemDisplayName))
                        {
                            itemDisplayName = RustTranslationAPI.Call<string>("GetHoldableTranslation", language, itemShortName);

                            if (string.IsNullOrWhiteSpace(itemDisplayName))
                            {
                                Log($"There is no translation for shortname {itemShortName} found!", LogLevel.Warning);
                                itemDisplayName = itemShortName;
                            }
                        }
                    }
                }

                displayNames[prefabID] = itemDisplayName;
            }

            return itemDisplayName;
        }

        public string StripRustTags(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return _static.Tags.Replace(text, string.Empty);
        }

        #endregion Helpers
    }
}