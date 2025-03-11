using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("RandomSpawns", "k1lly0u", "0.3.7")]
    class RandomSpawns : RustPlugin
    {
        #region Fields
        [PluginReference]
        private Plugin ZoneManager;

        private readonly Hash<TerrainBiome.Enum, List<Vector3>> _spawnpoints = new Hash<TerrainBiome.Enum, List<Vector3>>();


        private bool IsDisabled { get; set; } = false;

        private const int VIS_RAYCAST_LAYERS = 1 << 8 | 1 << 17 | 1 << 21;

        private const int POINT_RAYCAST_LAYERS = 1 << 4 | 1 << 8 | 1 << 10 | 1 << 15 | 1 << 16 | 1 << 21 | 1 << 23 | 1 << 27 | 1 << 28 | 1 << 29;

        private int BLOCKED_TOPOLOGY = 0;
        //private const int BLOCKED_TOPOLOGY = (int)(TerrainTopology.Enum.Cliff | TerrainTopology.Enum.Cliffside | TerrainTopology.Enum.Lake | TerrainTopology.Enum.Ocean | TerrainTopology.Enum.Monument | TerrainTopology.Enum.Offshore | TerrainTopology.Enum.River | TerrainTopology.Enum.Swamp | TerrainTopology.Enum.Rail | TerrainTopology.Enum.Railside);
        #endregion

        #region Oxide Hooks     
        private void Loaded()
        {
            for (int i = 0; i < configData.Spawn.BlockedTopologies.Length; i++)
            {
                TerrainTopology.Enum value;

                if (Enum.TryParse(configData.Spawn.BlockedTopologies[i], out value))                
                    BLOCKED_TOPOLOGY |= (int)value;                
                else Debug.Log($"[RandomSpawns] Failed to parse topology type {configData.Spawn.BlockedTopologies[i]}");
            }
        }

        private void OnServerInitialized() => GenerateSpawnpoints();
        
        private object OnPlayerRespawn(BasePlayer player)
        {
            if (IsDisabled)
                return null;

            object targetpos = GetSpawnPoint(false);
            if (targetpos == null)
                return null;
            
            return new BasePlayer.SpawnPoint
            {
                pos = (Vector3)targetpos,
                rot = Quaternion.identity
            };
        }
        #endregion

        #region Point Generation
        
        private void GenerateSpawnpoints()
        {
            float halfSize = (float)World.Size * 0.5f;

            for (int i = 0; i < configData.Generation.Attempts; i++)
            {
                Vector2 random = (UnityEngine.Random.insideUnitCircle * 0.95f) * halfSize;

                Vector3 position = new Vector3(random.x, 500f, random.y);

                if (ContainsTopologyAtPoint(BLOCKED_TOPOLOGY, position))
                    continue;

                float heightAtPoint;
                if (!IsPointOnTerrain(position, out heightAtPoint))
                    continue;

                position.y = heightAtPoint;

                if (IsPositionInZone(position))
                    continue;

                TerrainBiome.Enum majorityBiome = (TerrainBiome.Enum)TerrainMeta.BiomeMap.GetBiomeMaxType(position);

                List<Vector3> list;
                if (!_spawnpoints.TryGetValue(majorityBiome, out list))
                    _spawnpoints[majorityBiome] = list = new List<Vector3>();

                list.Add(position);
            }

            Debug.Log($"[RandomSpawns] - Generated {_spawnpoints.Sum(x => x.Value.Count)} spawn points");
        }

        private object GetSpawnPoint(bool ignorePlayerRestriction)
        {
            TerrainBiome.Enum biome = _spawnpoints.Keys.ElementAt(UnityEngine.Random.Range(0, _spawnpoints.Count));

            if (!ignorePlayerRestriction)
            {
                List<TerrainBiome.Enum> availableTypes = Facepunch.Pool.Get<List<TerrainBiome.Enum>>();

                int onlinePlayers = BasePlayer.activePlayerList.Count;

                foreach (KeyValuePair<TerrainBiome.Enum, ConfigData.SpawnOptions.BiomeOptions> kvp in configData.Spawn.Biomes)
                {
                    if (kvp.Value.Enabled && onlinePlayers >= kvp.Value.Players)
                        availableTypes.Add(kvp.Key);
                }

                biome = availableTypes.GetRandom();

                Facepunch.Pool.FreeUnmanaged(ref availableTypes);
            }

            return GetSpawnPoint(biome, ignorePlayerRestriction);
        }

        private object GetSpawnPoint(TerrainBiome.Enum biome, bool ignorePlayerRestriction)
        {
            if (!_spawnpoints.ContainsKey(biome))            
                return null;
            
            List<Vector3> spawnpoints = _spawnpoints[biome];
            Vector3 position = spawnpoints.GetRandom();
            if (position == Vector3.zero)           
                return null;
            
            List<BaseEntity> list = Facepunch.Pool.Get<List<BaseEntity>>();
            Vis.Entities(position, configData.Generation.BuildingDistance, list, VIS_RAYCAST_LAYERS);
            int count = list.Count;
            Facepunch.Pool.FreeUnmanaged(ref list);

            if (count > 0)
            {
                spawnpoints.Remove(position);
                if (spawnpoints.Count < 1)
                {
                    PrintWarning("All current spawnpoints have been overrun by buildings and such. Regenerating spawnpoints!");
                    GenerateSpawnpoints();                    
                }
                return GetSpawnPoint(ignorePlayerRestriction);
            }

            return position;
        }
        #endregion

        #region Helpers
        private bool ContainsTopologyAtPoint(int mask, Vector3 position) => (TerrainMeta.TopologyMap.GetTopology(position) & mask) != 0;

        private bool IsPointOnTerrain(Vector3 position, out float heightAtPoint)
        {
            RaycastHit raycastHit;
            if (Physics.Raycast(position, Vector3.down, out raycastHit, 500f, POINT_RAYCAST_LAYERS))
            {
                if (raycastHit.collider is TerrainCollider)
                {
                    heightAtPoint = raycastHit.point.y;
                    return true;
                }
            }
            heightAtPoint = 500f;
            return false;
        }

        private TerrainBiome.Enum ParseType(string type)
        {
            try
            {
                return (TerrainBiome.Enum)System.Enum.Parse(typeof(TerrainBiome.Enum), type, true);
            }
            catch
            {
                return TerrainBiome.Enum.Temperate;
            }
        }
        #endregion

        #region Zone Manager
        private bool IsPositionInZone(Vector3 position)
        {
            if (!ZoneManager || configData.Spawn.BlockedZones.Length == 0)
                return false;

            for (int i = 0; i < configData.Spawn.BlockedZones.Length; i++)
            {
                object success = ZoneManager.Call("IsPositionInZone", configData.Spawn.BlockedZones[i], position);
                if (success != null && success is bool)
                    return (bool)success;
            }
            return false;
        }
        #endregion

        #region API
        [HookMethod("GetSpawnPointAtBiome")]
        public object GetSpawnPointAtBiome(string biomeTypeStr)
        {
            TerrainBiome.Enum biomeType = ParseType(biomeTypeStr);
            return GetSpawnPoint(biomeType, true);
        }

        [HookMethod("DisableSpawnSystem")]
        public void DisableSpawnSystem() => IsDisabled = true;

        [HookMethod("GetSpawnPoint")]
        public object GetSpawnPoint() => GetSpawnPoint(true);
        #endregion

        #region Commands
        [ChatCommand("showspawns")]
        private void cmdShowSpawns(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
                return;

            SendReply(player, "Total spawn count: " + _spawnpoints.Sum(x => x.Value.Count));

            foreach (KeyValuePair<TerrainBiome.Enum, List<Vector3>> kvp in _spawnpoints)
            {
                Color color = kvp.Key == TerrainBiome.Enum.Arctic ? Color.blue : kvp.Key == TerrainBiome.Enum.Arid ? Color.red : kvp.Key == TerrainBiome.Enum.Temperate ? Color.green : Color.white;

                foreach (Vector3 position in kvp.Value)
                {
                    player.SendConsoleCommand("ddraw.sphere", 30f, color, position, 1f);
                    player.SendConsoleCommand("ddraw.line", 30f, color, position, position + (Vector3.up * 200f));
                }
            }
        }
        #endregion

        #region Config        
        private ConfigData configData;
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Generation Options")]
            public GenerationOptions Generation { get; set; }

            [JsonProperty(PropertyName = "Spawn Options")]
            public SpawnOptions Spawn { get; set; }

            public class GenerationOptions
            {
                [JsonProperty(PropertyName = "Generation attempts")]
                public int Attempts { get; set; }

                [JsonProperty(PropertyName = "Maximum slope (degrees)")]
                public float MaxSlope { get; set; }

                [JsonProperty(PropertyName = "Distance from buildings (metres)")]
                public float BuildingDistance { get; set; }
            }

            public class SpawnOptions
            {
                [JsonProperty(PropertyName = "Biome Options")]
                public Dictionary<TerrainBiome.Enum, BiomeOptions> Biomes { get; set; }

                [JsonProperty(PropertyName = "Disable spawn points in these zones (zone IDs)")]
                public string[] BlockedZones { get; set; }

                [JsonProperty(PropertyName = "Disable spawn points in these topologies")]
                public string[] BlockedTopologies { get; set; }

                public class BiomeOptions
                {
                    [JsonProperty(PropertyName = "Enable spawn points to be generated in this biome")]
                    public bool Enabled { get; set; }

                    [JsonProperty(PropertyName = "Minimum required online players before spawns from this biome will be selected")]
                    public int Players { get; set; }
                }
            }

            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                Generation = new ConfigData.GenerationOptions
                {
                    Attempts = 3000,
                    MaxSlope = 45f,
                    BuildingDistance = 15f
                },
                Spawn = new ConfigData.SpawnOptions
                {
                    Biomes = new Dictionary<TerrainBiome.Enum, ConfigData.SpawnOptions.BiomeOptions>
                    {
                        [TerrainBiome.Enum.Arctic] = new ConfigData.SpawnOptions.BiomeOptions { Enabled = true, Players = 30 },
                        [TerrainBiome.Enum.Tundra] = new ConfigData.SpawnOptions.BiomeOptions { Enabled = true, Players = 20 },
                        [TerrainBiome.Enum.Arid] = new ConfigData.SpawnOptions.BiomeOptions { Enabled = true, Players = 10 },
                        [TerrainBiome.Enum.Temperate] = new ConfigData.SpawnOptions.BiomeOptions { Enabled = true, Players = 1 }
                    },
                    BlockedZones = new string[0],
                    BlockedTopologies = new string[] { "Cliff", "Cliffside", "Lake", "Ocean", "Monument", "Offshore", "River", "Swamp", "Rail" },
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new Oxide.Core.VersionNumber(0, 3, 0))
                configData = GetBaseConfig();

            if (configData.Version < new Core.VersionNumber(0, 3, 5))
                configData.Spawn.BlockedTopologies = baseConfig.Spawn.BlockedTopologies;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

       
    }
}
