using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Powerless Electronics", "WhiteThunder", "1.2.5")]
    [Description("Allows electrical entities to generate their own power when not plugged in.")]
    internal class PowerlessElectronics : CovalencePlugin
    {
        #region Fields

        private const string PermissionAll = "powerlesselectronics.all";
        private const string PermissionEntityFormat = "powerlesselectronics.{0}";

        private Configuration _config;

        #endregion

        #region Hooks

        private void Init()
        {
            Unsubscribe(nameof(OnEntitySpawned));
        }

        private void OnServerInitialized()
        {
            // Don't overwrite the config if invalid since the user will lose their config!
            if (!_config.UsingDefaults)
            {
                var addedPrefabs = _config.AddMissingPrefabs();
                if (addedPrefabs != null)
                {
                    LogWarning($"Discovered and added {addedPrefabs.Count} electrical entity types to Configuration.\n - {string.Join("\n - ", addedPrefabs)}");
                    SaveConfig();
                }
            }

            _config.GeneratePermissionNames();

            // Register permissions only after discovering prefabs.
            permission.RegisterPermission(PermissionAll, this);
            foreach (var entry in _config.Entities)
            {
                permission.RegisterPermission(entry.Value.PermissionName, this);
            }

            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var ioEntity = entity as IOEntity;
                if (ioEntity != null)
                {
                    ProcessIOEntity(ioEntity, delay: false);
                }
            }

            Subscribe(nameof(OnEntitySpawned));
        }

        private void OnEntitySpawned(IOEntity ioEntity)
        {
            ProcessIOEntity(ioEntity, delay: true);
        }

        private void OnIORefCleared(IOEntity.IORef ioRef, IOEntity ioEntity)
        {
            ProcessIOEntity(ioEntity, delay: true);
        }

        #endregion

        #region Helper Methods

        private static bool InputUpdateWasBlocked(IOEntity ioEntity, int inputSlot, int amount)
        {
            return Interface.CallHook("OnPowerlessInputUpdate", inputSlot, ioEntity, amount) is false;
        }

        private static bool IsHybridIOEntity(IOEntity ioEntity)
        {
            return ioEntity is ElectricFurnaceIO or ElevatorIOEntity or MicrophoneStandIOEntity
                   || (ioEntity is SimpleLight && ioEntity.GetParentEntity() is WeaponRack);
        }

        private static BaseEntity GetOwnerEntity(IOEntity ioEntity)
        {
            var parent = ioEntity.GetParentEntity();
            if ((object)parent == null)
                return ioEntity;

            return IsHybridIOEntity(ioEntity) ? parent : ioEntity;
        }

        private static bool ShouldIgnoreEntity(IOEntity ioEntity)
        {
            // Parented entities are assumed to be controlled by other plugins that can manage power themselves
            // Exception being entities that are parented in vanilla
            if (ioEntity.HasParent()
                && !IsHybridIOEntity(ioEntity)
                && !(ioEntity is IndustrialCrafter)
                && !(ioEntity is StorageMonitor)
                && !(ioEntity is DoorManipulator))
                return true;

            // Turrets and sam sites with switches on them are assumed to be controlled by other plugins
            if (ioEntity is AutoTurret or SamSite && GetChildEntity<ElectricSwitch>(ioEntity) != null)
                return true;

            return false;
        }

        private static void MaybeProvidePower(IOEntity ioEntity, EntityConfig entityConfig)
        {
            if (ShouldIgnoreEntity(ioEntity))
                return;

            foreach (var inputSlot in entityConfig.InputSlots)
            {
                var powerAmount = entityConfig.GetPowerForSlot(inputSlot);

                // Don't update power if specified to be 0 to avoid conflicts with other plugins
                if (powerAmount > 0 && !HasConnectedInput(ioEntity, inputSlot))
                {
                    TryProvidePower(ioEntity, inputSlot, powerAmount);
                }
            }
        }

        private static T GetChildEntity<T>(BaseEntity entity) where T : BaseEntity
        {
            foreach (var child in entity.children)
            {
                var childOfType = child as T;
                if (childOfType != null)
                    return childOfType;
            }

            return null;
        }

        private static bool HasConnectedInput(IOEntity ioEntity, int inputSlot)
        {
            return inputSlot < ioEntity.inputs.Length
                && ioEntity.inputs[inputSlot].connectedTo.Get() != null;
        }

        private static void TryProvidePower(IOEntity ioEntity, int inputSlot, int powerAmount)
        {
            if (ioEntity.inputs.Length > inputSlot && !InputUpdateWasBlocked(ioEntity, inputSlot, powerAmount))
            {
                ioEntity.UpdateFromInput(powerAmount, inputSlot);
            }
        }

        private void ProcessIOEntity(IOEntity ioEntity, bool delay)
        {
            if (ioEntity == null)
                return;

            var entityConfig = GetEntityConfig(ioEntity);

            // Entity not supported
            if (entityConfig is not { Enabled: true })
                return;

            if (!EntityOwnerHasPermission(ioEntity, entityConfig))
                return;

            if (delay)
            {
                var ioEntity2 = ioEntity;
                var entityConfig2 = entityConfig;

                NextTick(() =>
                {
                    if (ioEntity2 == null)
                        return;

                    MaybeProvidePower(ioEntity2, entityConfig2);
                });
            }
            else
            {
                MaybeProvidePower(ioEntity, entityConfig);
            }
        }

        private bool EntityOwnerHasPermission(IOEntity ioEntity, EntityConfig entityConfig)
        {
            if (!entityConfig.RequirePermission)
                return true;

            var ownerEntity = GetOwnerEntity(ioEntity);
            if (ownerEntity.OwnerID == 0)
                return false;

            var ownerIdString = ownerEntity.OwnerID.ToString();
            return permission.UserHasPermission(ownerIdString, PermissionAll)
                || permission.UserHasPermission(ownerIdString, entityConfig.PermissionName);
        }

        #endregion

        #region Configuration

        private EntityConfig GetEntityConfig(IOEntity ioEntity)
        {
            return _config.Entities.TryGetValue(ioEntity.ShortPrefabName, out var entityConfig) ? entityConfig : null;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class Configuration : BaseConfiguration
        {
            private static readonly string[] IgnoredEntities =
            {
                // Has inputs to move the lift but does not consume power (elevatorioentity is the right one).
                "elevator",

                // Has inputs to toggle on/off but does not consume power.
                "small_fuel_generator.deployed",

                // Has audio input only
                "connectedspeaker.deployed",
                "soundlight.deployed",

                // Static entity
                "caboose_xorswitch",

                // Has no power input
                "fogmachine",
                "spookyspeaker",
                "snowmachine",
                "strobelight",
            };

            private static bool HasElectricalInput(IOEntity ioEntity)
            {
                foreach (var input in ioEntity.inputs)
                {
                    if (input.type == IOEntity.IOType.Electric)
                        return true;
                }

                return false;
            }

            [JsonProperty("Entities")]
            public Dictionary<string, EntityConfig> Entities = new()
            {
                ["andswitch.entity"] = new EntityConfig
                {
                    InputSlots = new[] { 0, 1 },
                    PowerAmounts = new[] { 0, 0 },
                },

                ["electrical.combiner.deployed"] = new EntityConfig
                {
                    InputSlots = new[] { 0, 1 },
                    PowerAmounts = new[] { 0, 0 },
                },

                // Has no pickup entity.
                ["electrical.modularcarlift.deployed"] = new EntityConfig(),

                // Has no pickup entity.
                ["elevatorioentity"] = new EntityConfig(),

                ["fluidswitch"] = new EntityConfig
                {
                    InputSlots = new[] { 2 },
                },

                ["industrialconveyor.deployed"] = new EntityConfig
                {
                    InputSlots = new[] { 1 },
                },

                ["industrialcrafter.deployed"] = new EntityConfig
                {
                    InputSlots = new[] { 1 },
                },

                // Has no pickup entity.
                ["microphonestandio.entity"] = new EntityConfig(),
                ["electricfurnace.io"] = new EntityConfig(),

                ["orswitch.entity"] = new EntityConfig
                {
                    InputSlots = new[] { 0, 1 },
                    PowerAmounts = new[] { 0, 0 },
                },

                ["poweredwaterpurifier.deployed"] = new EntityConfig
                {
                    InputSlots = new[] { 1 },
                },

                ["xorswitch.entity"] = new EntityConfig
                {
                    InputSlots = new[] { 0, 1 },
                    PowerAmounts = new[] { 0, 0 },
                },
            };

            public List<string> AddMissingPrefabs()
            {
                var addedPrefabs = new List<string>();

                foreach (var prefab in GameManifest.Current.entities)
                {
                    var ioEntity = GameManager.server.FindPrefab(prefab.ToLower())?.GetComponent<IOEntity>();
                    if (ioEntity == null || string.IsNullOrEmpty(ioEntity.ShortPrefabName))
                        continue;

                    if (Entities.TryGetValue(ioEntity.ShortPrefabName, out _))
                        continue;

                    if (!HasElectricalInput(ioEntity)
                        || ioEntity.pickup.itemTarget == null
                        || ioEntity.ShortPrefabName.ToLower().Contains("static")
                        || IgnoredEntities.Contains(ioEntity.ShortPrefabName.ToLower()))
                        continue;

                    addedPrefabs.Add(ioEntity.ShortPrefabName);
                }

                if (addedPrefabs.Count == 0)
                    return null;

                foreach (var shortPrefabName in addedPrefabs)
                {
                    Entities[shortPrefabName] = new EntityConfig();
                }

                SortEntities();

                addedPrefabs.Sort();
                return addedPrefabs;
            }

            public void GeneratePermissionNames()
            {
                foreach (var entry in Entities)
                {
                    // Make the permission name less redundant
                    entry.Value.PermissionName = string.Format(PermissionEntityFormat, entry.Key)
                        .Replace("electric.", string.Empty)
                        .Replace("electrical.", string.Empty)
                        .Replace(".deployed", string.Empty)
                        .Replace("_deployed", string.Empty)
                        .Replace(".entity", string.Empty);
                }
            }

            private void SortEntities()
            {
                var shortPrefabNames = Entities.Keys.ToList();
                shortPrefabNames.Sort();

                var newEntities = new Dictionary<string, EntityConfig>();
                foreach (var shortName in shortPrefabNames)
                {
                    newEntities[shortName] = Entities[shortName];
                }

                Entities = newEntities;
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        internal class EntityConfig
        {
            private static readonly int[] StandardInputSlot = { 0 };

            [JsonProperty("RequirePermission")]
            public bool RequirePermission = false;

            // Hidden from config when it's using the default value
            [JsonProperty("InputSlots")]
            public int[] InputSlots = StandardInputSlot;

            public bool ShouldSerializeInputSlots() =>
                !InputSlots.SequenceEqual(StandardInputSlot);

            // Hidden from config when the plural form is used
            [JsonProperty("PowerAmount")]
            public int PowerAmount = 0;

            public bool ShouldSerializePowerAmount() =>
                PowerAmounts == null;

            // Hidden from config when null
            [JsonProperty("PowerAmounts", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public int[] PowerAmounts;

            public string PermissionName;

            public bool Enabled
            {
                get
                {
                    foreach (var slot in InputSlots)
                    {
                        if (GetPowerForSlot(slot) > 0)
                            return true;
                    }

                    return false;
                }
            }

            public int GetPowerForSlot(int slotNumber)
            {
                var index = Array.IndexOf(InputSlots, slotNumber);

                // We can't power an input slot that we don't know about
                if (index == -1)
                    return 0;

                // Allow plural array form to take precedence if present
                if (PowerAmounts == null)
                    return PowerAmount;

                // InputSlots and PowerAmounts are expected to be parallel arrays
                return index < PowerAmounts.Length ? PowerAmounts[index] : 0;
            }
        }

        private Configuration GetDefaultConfig() => new();

        #region Configuration Helpers

        [JsonObject(MemberSerialization.OptIn)]
        private class BaseConfiguration
        {
            public bool UsingDefaults;

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>()
                                    .ToDictionary(prop => prop.Name,
                                                  prop => ToObject(prop.Value));

                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue)token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(BaseConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            var changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                if (currentRaw.TryGetValue(key, out var currentRawValue))
                {
                    var currentDictValue = currentRawValue as Dictionary<string, object>;
                    if (currentWithDefaults[key] is Dictionary<string, object> defaultDictValue)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
                        {
                            changed = true;
                        }
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }

        protected override void LoadDefaultConfig() => _config = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_config))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                _config.UsingDefaults = true;
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Log($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_config, true);
        }

        #endregion

        #endregion
    }
}
