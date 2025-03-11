using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
using Rust;
using System.Globalization;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("PookEvent", "https://discord.gg/TrJ7jnS233", "1.3.5")]
    [Description("Unleash chaos and reap rewards with Pookie and it's Decoy!")]
    class PookEvent : RustPlugin
    {
        #region Declarations
        private const float GroundOffset = 0.1f;
        private BaseEntity pookie;
        private BaseEntity pookieDecoy;
        private BaseEntity decoyLan;
        private BaseEntity pookieLan;
        private Timer eventTimer;
        private bool eventActive = false;
        List<string> gridList = new List<string>();
        List<Vector3> decoyPositions = new List<Vector3>(); // Create a list to store decoy positions
        private readonly List<Collider> colliders = new List<Collider>();
        private MapMarkerGenericRadius pookieMarker { get; set; }
        private Dictionary<string, MapMarkerGenericRadius> pookieMarkers = new Dictionary<string, MapMarkerGenericRadius>();
        private List<NetworkableId> pookieEntName = new List<NetworkableId>();
        Vector3 newPositionDecoy = Vector3.zero;
        string decoyGrid = "";
        string spawnGrid = "";
        string pookGrids = "";
        string gridOne = "";
        string gridTwo = "";

        private const string worlItem_Prefab = "assets/prefabs/misc/burlap sack/generic_world.prefab";
        private static PookEvent Instance;

        [PluginReference]
        private Plugin Economics;

        [PluginReference]
        private Plugin ServerRewards;

        [PluginReference]
        private Plugin EntityScaleManager;

        #endregion

        #region Config
        private Configuration config;

        private class Configuration
        {
            [JsonProperty("Enable Special Event? (Holiday themed Pookie)")]
            public bool specialEvent { get; set; }

            [JsonProperty("Event Interval (in minutes)")]
            public int IntervalMinutes { get; set; }

            [JsonProperty("Event Length (in minutes)")]
            public int EventLength { get; set; }

            [JsonProperty("Minimum players online for event to start.")]
            public int minPlayers { get; set; }

            [JsonProperty("How many decoys should spawn?")]
            public int decoyCount { get; set; }

            [JsonProperty("Item to spawn next to Pookies")]
            public string pookieObject { get; set; }

            [JsonProperty("Broadcast grids they spawn in?")]
            public bool BroadcastGrid { get; set; }

            [JsonProperty("Show map markers when they spawn?")]
            public bool pookieMapMarkers { get; set; }

            [JsonProperty("Color of Map Marker")]
            public string MapMarkerColor { get; set; }

            [JsonProperty("Map Marker Radius")]
            public float MapMarkerSize { get; set; }

            [JsonProperty("Name color for decoy killer.")]
            public string DecoyKiller { get; set; }

            [JsonProperty("Name color for pookie killer.")]
            public string PookieKiller { get; set; }

            [JsonProperty("Enable custom HP for Pookie and Decoy?")]
            public bool customHeatlh { get; set; }

            [JsonProperty("Custom Pookie HP Amount")]
            public int pookieHP { get; set; }

            [JsonProperty("Custom Decoy HP Amount")]
            public int decoyHP { get; set; }

            [JsonProperty("Enable Negative Effects?")]
            public bool EnableNegativeEffects { get; set; }

            [JsonProperty("Enable Negative Effect Commands?")]
            public bool EnableNegativeEffectCommand { get; set; }

            [JsonProperty("Give Reward Item AND Currency?")]
            public bool enableDualReward { get; set; }

            [JsonProperty("Use ServerRewards?")]
            public bool srvrRewards { get; set; }

            [JsonProperty("ServerRewards Amount to give per kill.")]
            public int srAmount { get; set; }

            [JsonProperty("Use Economics?")]
            public bool ecoUse { get; set; }

            [JsonProperty("Economics Amount to give per kill.")]
            public int ecoAmount { get; set; }

            [JsonProperty("Reward Items (Item Shortname, Quantity)")]
            public List<Rewards> RewardItems { get; set; }

            [JsonProperty("Negative Effects (Executes on player for killing Decoy)")]
            public List<NegativeEffect> NegativeEffects { get; set; }

            [JsonProperty("Negative Effect Commands (When using commands, put %steamid% where the player ID would be used in the command)")]
            public List<NegativeEffectCommands> NegativeEffectCommands { get; set; }
        }

        private class NegativeEffect
        {
            [JsonProperty("Effect Name")]
            public string EffectName { get; set; }

            [JsonProperty("Effect Description")]
            public string EffectDesc { get; set; }

            [JsonProperty("Enabled")]
            public bool EffectEnabled { get; set; }

            [JsonProperty("Amount")]
            public int effectAmt { get; set; }
        }

        private class NegativeEffectCommands
        {
            [JsonProperty("Command")]
            public string effectCommand { get; set; }

            [JsonProperty("Command Chat Message")]
            public string effectCommandMsg { get; set; }

            [JsonProperty("Enabled")]
            public bool CommandEffectEnabled { get; set; }
        }

        private class Rewards
        {
            [JsonProperty("Item Shortname")]
            public string itemShortname { get; set; }

            [JsonProperty("Item Skin")]
            public ulong itemSkin { get; set; }

            [JsonProperty("Item Name")]
            public string itemName { get; set; }

            [JsonProperty("Minimum Quantity")]
            public int minQty { get; set; }

            [JsonProperty("Maximum Quantity")]
            public int maxQty { get; set; }

        }

        protected override void LoadDefaultConfig()
        {
            var defaultConfig = new Configuration
            {
                specialEvent = false,
                IntervalMinutes = 60,
                EventLength = 20,
                minPlayers = 1,
                decoyCount = 1,
                pookieObject = "assets/prefabs/deployable/lantern/lantern.deployed.prefab",
                BroadcastGrid = true,
                pookieMapMarkers = false,
                MapMarkerColor = "#00FF00",
                MapMarkerSize = 0.25f,
                PookieKiller = "green",
                DecoyKiller = "orange",
                customHeatlh = false,
                pookieHP = 100,
                decoyHP = 100,
                EnableNegativeEffects = true,
                EnableNegativeEffectCommand = false,
                enableDualReward = false,
                srvrRewards = false,
                srAmount = 0,
                ecoUse = false,
                ecoAmount = 0,
                RewardItems = new List<Rewards>
                {
                    new Rewards {itemShortname = "ammo.rifle", itemSkin = 0, itemName = "", minQty = 1, maxQty = 5},
                    new Rewards {itemShortname = "apple", itemSkin = 0, itemName = "", minQty = 2, maxQty = 3},
                    new Rewards {itemShortname = "bandage", itemSkin = 0, itemName = "", minQty = 7, maxQty = 11},
                },
                NegativeEffects = new List<NegativeEffect>
                {
                    new NegativeEffect {EffectName = "Health",EffectDesc = "Subtracts amount from players health. (If players health is less than amount, they will die!)",EffectEnabled = true,effectAmt = 25},
                    new NegativeEffect {EffectName = "Dehydration",EffectDesc = "Sets players hydration to the value given.",EffectEnabled = true,effectAmt = 25},
                    new NegativeEffect {EffectName = "Hunger",EffectDesc = "Sets players hunger to the value given.",EffectEnabled = true,effectAmt = 25},
                    new NegativeEffect {EffectName = "Radiation",EffectDesc = "Amount of radiation poisoning you want to give the player.",EffectEnabled = true,effectAmt = 25},
                    new NegativeEffect {EffectName = "Bleeding",EffectDesc = "Bleeding amount you want applied to the player.",EffectEnabled = true,effectAmt = 25},
                    new NegativeEffect {EffectName = "Spawn Bears",EffectDesc = "Number of bears you want spawned around the player.",EffectEnabled = true,effectAmt = 1},
                    new NegativeEffect {EffectName = "Spawn Wolves",EffectDesc = "Number of wolves you want spawned around the player.",EffectEnabled = true,effectAmt = 1},
                    new NegativeEffect {EffectName = "Spawn Boar",EffectDesc = "Number of boar you want spawned around the player.",EffectEnabled = true,effectAmt = 1},
                    new NegativeEffect {EffectName = "Drop Inventory",EffectDesc = "Drops the players inventory on the ground.(1 = Hot Bar, 2 = Armor, 3 = Main Inventory, 4 = All)",EffectEnabled = true,effectAmt = 1},
                    new NegativeEffect {EffectName = "Spikes",EffectDesc = "Spawn a ring of spike traps around a player for x secs.(e.g. Amount = 10 is 10 seconds.)",EffectEnabled = true,effectAmt = 10},
                },
                NegativeEffectCommands = new List<NegativeEffectCommands>
                {
                    new NegativeEffectCommands {effectCommand = "killplayer %steamid%", effectCommandMsg = "Message to send to player", CommandEffectEnabled = false },
                    new NegativeEffectCommands {effectCommand = "killplayer %steamid%", effectCommandMsg = "Message to send to player", CommandEffectEnabled = false },
                    new NegativeEffectCommands {effectCommand = "killplayer %steamid%", effectCommandMsg = "Message to send to player", CommandEffectEnabled = false },
                }
            };

            Config.WriteObject(defaultConfig, true);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();
            SaveConfig();

            if (config == null)
            {
                Puts("Configuration file not found, generating default configuration...");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        #endregion

        #region Admin Commands/Cleanup
        [ChatCommand("spawnpookie")]
        private void SpawnPookieCommand(BasePlayer player, string command, string[] args)
        {
            if (eventActive)
            {
                gridList.Clear();
                decoyPositions.Clear();
                decoyGrid = "";
                RemoveEntitiesByNames(pookieEntName);
            }
            if (!player.IsAdmin)
                return;
            SpawnPookieEvent();

        }

        private void KillMapMarkerByName()
        {
            MapMarkerGenericRadius[] markers = UnityEngine.Object.FindObjectsOfType<MapMarkerGenericRadius>();

            foreach (var marker in markers)
            {
                marker.Kill();
            }
        }

        [ChatCommand("clearpookie")]
        private void RemoveEntitiesCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
                return;

            RemoveEntitiesByNames(pookieEntName);
            eventActive = false;
            EndEvent();
        }

        [ChatCommand("pg")]
        private void locatePookie(BasePlayer player, string command, string[] args)
        {
            if (!eventActive)
            {
                player.ChatMessage("Event not active!");
                return;
            }
            if (config.BroadcastGrid)
            {
                if (config.decoyCount > 1)
                {
                    player.ChatMessage($"Grids are: {pookGrids}");
                }
                else
                {
                    player.ChatMessage($"Grids are: {gridOne},{gridTwo}");
                }
            }
        }

        private void RemoveEntitiesByNames(List<NetworkableId> entityNames)
        {
            List<BaseEntity> entitiesToRemove = new List<BaseEntity>();

            foreach (var entity in BaseNetworkable.serverEntities)
            {
                BaseEntity baseEntity = entity as BaseEntity;
                if (baseEntity != null)
                {
                    foreach (var name in entityNames)
                    {
                        if (baseEntity.name.Equals(name))
                        {
                            entitiesToRemove.Add(baseEntity);
                            break;
                        }
                    }
                }
            }

            foreach (var entity in entitiesToRemove)
            {
                entity.Kill();
            }
            Puts("Entities removed successfully.");
        }


        #endregion

        #region Hooks
        void Init()
        {
            Instance = this;
            LoadConfig();
            timer.Every(config.IntervalMinutes * 60, SpawnPookieEvent);
            Puts($"The first event will start in {config.IntervalMinutes} minutes!");
            Subscribe(nameof(OnEntityDeath));
            Subscribe(nameof(Unload));
        }

        void Unload()
        {
            if (eventActive) { EndEvent(); }
            Instance = null;
            Unsubscribe(nameof(OnEntityDeath));
            Unsubscribe(nameof(Unload));
            foreach (var pookieMarker in pookieMarkers.Values)
            {
                if (pookieMarker != null && !pookieMarker.IsDestroyed)
                {
                    pookieMarker.Kill();
                }
            }

            pookieMarkers.Clear();
        }
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity.name == "pumpkinhead" || entity.name == "Pookie Lantern")
            { return true; }
            return null;
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (hitInfo == null || hitInfo.InitiatorPlayer == null)
                return;

            if (pookieEntName.Count > 0)
            {
                if (entity.name == "Pookie Bear")
                {
                    PrintToChat(GetMessage("pookiekiller", hitInfo.InitiatorPlayer.displayName, config.PookieKiller));
                    pookieEntName.Remove(entity.net.ID);
                    FindAndKillDecoys();
                    Effect.server.Run("assets/prefabs/misc/halloween/lootbag/effects/gold_open.prefab", entity, 0, new Vector3(0, 1.5f, 0), new Vector3());
                    if (eventTimer != null && eventTimer.Destroyed == false)
                    {
                        eventTimer.Destroy();
                    }

                    pookie = null;
                    EndEvent();
                    var pookID = entity.net.ID;

                    MapMarkerGenericRadius pookieMarker;
                    if (pookieMarkers.TryGetValue(pookID.ToString(), out pookieMarker))
                    {
                        pookieMarker.Kill();
                        pookieMarkers.Remove(pookID.ToString());
                    }

                    if (config.RewardItems != null && config.RewardItems.Count > 0)
                    {
                        ulong skin = 0;
                        string name = "";
                        int randomIndex = Random.Range(0, config.RewardItems.Count);
                        var rewardItem = config.RewardItems[randomIndex];
                        int quantity = Random.Range(rewardItem.minQty, rewardItem.maxQty + 1);
                        var item = FindItemDefinition(rewardItem.itemShortname);

                        if (rewardItem.itemSkin != 0)
                        {
                            skin = rewardItem.itemSkin;
                        }
                        if (rewardItem.itemName != "")
                        {
                            name = rewardItem.itemName;
                        }
                        GivePlayerItem(hitInfo.InitiatorPlayer, rewardItem.itemShortname, quantity, name, skin);
                    }

                }
                else if (entity.name.ToLower().Contains("pookie decoy"))
                {
                    pookieEntName.Remove(entity.net.ID);
                    PrintToChat(GetMessage("decoykiller", hitInfo.InitiatorPlayer.displayName, config.DecoyKiller));
                    Effect.server.Run("assets/prefabs/misc/halloween/pumpkin_bucket/effects/eggexplosion.prefab", entity, 0, new Vector3(0, 1.5f, 0), new Vector3());
                    if (decoyLan != null)
                    {
                        decoyLan.Kill();
                        decoyLan = null;
                    }
                    var pookID = entity.net.ID;

                    // Check if the pookID exists in the dictionary and retrieve the associated map marker
                    MapMarkerGenericRadius pookieMarker;
                    if (pookieMarkers.TryGetValue(pookID.ToString(), out pookieMarker))
                    {
                        // Remove the map marker
                        pookieMarker.Kill();
                        pookieMarkers.Remove(pookID.ToString()); // Remove the reference from the dictionary
                    }


                    if (!config.EnableNegativeEffects && config.EnableNegativeEffectCommand && config.NegativeEffectCommands != null && config.NegativeEffectCommands.Count > 0)
                    {
                        List<NegativeEffectCommands> enabledCommands = config.NegativeEffectCommands.Where(command => command.CommandEffectEnabled).ToList();
                        if (enabledCommands.Count > 0)
                        {
                            int randomIndex = Random.Range(0, enabledCommands.Count);
                            NegativeEffectCommands nEffect = enabledCommands[randomIndex];
                            ExecuteNegativeCommand(hitInfo.InitiatorPlayer, nEffect, nEffect.effectCommandMsg);
                            return;
                        }
                    }

                    if (!config.EnableNegativeEffectCommand && config.EnableNegativeEffects && config.NegativeEffects != null && config.NegativeEffects.Count > 0)
                    {
                        List<NegativeEffect> enabledEffects = config.NegativeEffects.Where(effect => effect.EffectEnabled).ToList();
                        if (enabledEffects.Count > 0)
                        {
                            int randomIndex = Random.Range(0, enabledEffects.Count);
                            NegativeEffect effect = enabledEffects[randomIndex];

                            ApplyNegativeEffect(hitInfo.InitiatorPlayer, effect);
                            return;
                        }
                    }


                    if (config.EnableNegativeEffects && config.NegativeEffects != null && config.NegativeEffects.Count > 0 &&
                    config.EnableNegativeEffectCommand && config.NegativeEffectCommands != null && config.NegativeEffectCommands.Count > 0)
                    {
                        List<NegativeEffect> enabledEffects = config.NegativeEffects.Where(effect => effect.EffectEnabled).ToList();
                        List<NegativeEffectCommands> enabledCommands = config.NegativeEffectCommands.Where(command => command.CommandEffectEnabled).ToList();

                        bool applyEffect = Random.Range(0, 2) == 0; // Randomly choose between 0 and 1

                        if ((applyEffect && enabledEffects.Count > 0) || (!applyEffect && enabledCommands.Count > 0))
                        {
                            if (applyEffect)
                            {
                                int randomIndex = Random.Range(0, enabledEffects.Count);
                                NegativeEffect effect = enabledEffects[randomIndex];
                                ApplyNegativeEffect(hitInfo.InitiatorPlayer, effect);
                                return;
                            }
                            else
                            {
                                int randomIndex = Random.Range(0, enabledCommands.Count);
                                NegativeEffectCommands nEffect = enabledCommands[randomIndex];
                                ExecuteNegativeCommand(hitInfo.InitiatorPlayer, nEffect, nEffect.effectCommandMsg);
                                return;
                            }
                        }
                    }


                }
            }
        }

        private void FindAndKillDecoys()
        {
            foreach (NetworkableId decoyID in pookieEntName.ToList())
            {
                NetworkableId netID = decoyID;

                var networkable = BaseNetworkable.serverEntities.Find(netID);
                var entity = networkable as BaseEntity;
                if (entity != null)
                {
                    entity.Kill();
                }
            }
        }

        private object CanPickupEntity(BasePlayer player, BaseEntity entity)
        {
            foreach (NetworkableId decoyID in pookieEntName.ToList())
            {
                if (entity.net.ID == decoyID || entity.name == "Pookie Lantern")
                {
                    player.ChatMessage("You can't pick that up!");
                    return false;
                }
            }

            return null;
        }
        #endregion

        #region MapMarkers

        private void CreatePookieMarker(Vector3 location, string pookID)
        {

            pookieMarker = GameManager.server.CreateEntity(StringPool.Get(2849728229), location) as MapMarkerGenericRadius;

            if (pookieMarker != null)
            {
                pookieMarker.alpha = 0.75f;
                pookieMarker.name = pookID;
                pookieMarker.color1 = MarkerColor();
                pookieMarker.color2 = MarkerColor();
                pookieMarker.radius = Mathf.Min(2.5f, config.MapMarkerSize);
                pookieMarker.Spawn();
                pookieMarker.SendUpdate();

                pookieMarkers[pookID] = pookieMarker;
            }

        }

        private Color MarkerColor()
        {
            Color color;
            return TryParseHtmlString(config.MapMarkerColor, out color) ? color : Color.green;
        }

        private bool TryParseHtmlString(string value, out Color color)
        {
            return ColorUtility.TryParseHtmlString(value.StartsWith("#") ? value : $"#{value}", out color);
        }

        #endregion

        #region Event Handlers
        private void SpawnPookieEvent()
        {
            foreach (var pookieMarker in pookieMarkers.Values)
            {
                if (pookieMarker != null && !pookieMarker.IsDestroyed)
                {
                    pookieMarker.Kill();
                }
            }

            pookieMarkers.Clear();

            if (BasePlayer.activePlayerList.Count < config.minPlayers)
            {
                PrintToChat($"There must be at least {config.minPlayers} players online for the event to start.");
                return;
            }

            eventActive = true;
            pookGrids = "";
            gridOne = "";
            gridTwo = "";
            decoyGrid = "";
            gridList.Clear();
            decoyPositions.Clear();
            RemoveEntitiesByNames(pookieEntName);
            float spawnThreshold = 180f;

            Vector3 bLoc = BanditLoc();
            Vector3 oLoc = GetOutpostLoc();
            if (eventTimer != null) { EndEvent(); }
            Vector3 spawnPosition = GetRandomSpawnPosition();
            Vector3 spawnPositionDecoy = GetRandomSpawnPosition();

            // Check if the spawn positions are outside the safe zone
            if (AreVectorsClose(bLoc, spawnPosition, spawnThreshold) || AreVectorsClose(oLoc, spawnPosition, spawnThreshold) || AreVectorsClose(bLoc, spawnPositionDecoy, spawnThreshold) || AreVectorsClose(oLoc, spawnPositionDecoy, spawnThreshold))
            {
                Puts("Pookie cannot spawn in a safe zone. Trying again...");
                SpawnPookieEvent(); // Try again until a valid spawn position is found
                return;
            }
            SpawnPookieBear(spawnPosition);
            if (config.decoyCount > 1)
            {
                for (int i = 0; i < config.decoyCount; i++)
                {
                    Vector3 newPositionDecoy = GetRandomSpawnPosition();
                    while (AreVectorsClose(bLoc, newPositionDecoy, spawnThreshold) || AreVectorsClose(oLoc, newPositionDecoy, spawnThreshold))
                    {
                        // If the new position is too close to bLoc or oLoc, generate a new random position
                        newPositionDecoy = GetRandomSpawnPosition();
                    }
                    SpawnPookieBearDecoy(newPositionDecoy, i);

                    //Add new position to list and then write a for each to get coordinates and append a string.
                    decoyPositions.Add(newPositionDecoy);
                }
            }
            else
            {
                SpawnPookieBearDecoy(spawnPositionDecoy, 0);
            }
            if (config.BroadcastGrid)
            {
                if (config.decoyCount > 1)
                {
                    foreach (Vector3 position in decoyPositions)
                    {
                        decoyGrid = decoyGrid + PhoneController.PositionToGridCoord(position) + ", ";
                    }
                    string spawnGrid = PhoneController.PositionToGridCoord(spawnPosition);
                    pookGrids = decoyGrid + spawnGrid;
                    PrintToChat(GetMessage("spawnedgridmulti", pookGrids));

                    decoyPositions.Clear(); // Clear the decoy list for the next event
                }
                else if (config.decoyCount == 1)
                {
                    string spawnGrid = PhoneController.PositionToGridCoord(spawnPosition);
                    string decoyGrid = PhoneController.PositionToGridCoord(spawnPositionDecoy);
                    if (spawnGrid == "N19") { spawnGrid = "N-19"; }
                    if (decoyGrid == "N19") { decoyGrid = "N-19"; }

                    //Put grid locations in a list and randomly select them. This prevents knowing which grid location will always be the pookie and not the decoy.
                    gridList.Add(spawnGrid);
                    gridList.Add(decoyGrid);
                    int randomIndex = UnityEngine.Random.Range(0, gridList.Count);
                    gridOne = gridList[randomIndex];
                    gridList.RemoveAt(randomIndex);
                    gridTwo = gridList[0];

                    PrintToChat(GetMessage("spawnedgrid", gridOne, gridTwo));
                    gridList.Clear(); // Clear the list for the next event
                }
            }
            else { PrintToChat(GetMessage("spawned")); }
            eventTimer = timer.Once(config.EventLength * 60, EndEvent);
        }

        private bool AreVectorsClose(Vector3 a, Vector3 b, float distanceThreshold)
        {
            float distance = Vector3.Distance(a, b);
            if (distance <= distanceThreshold)
            {
                return true;
            }
            return false;
        }

        private Vector3 GetOutpostLoc()
        {
            var monuments = UnityEngine.Object.FindObjectsOfType<MonumentInfo>();

            foreach (var monument in monuments)
            {
                if (monument.displayPhrase.english == "Outpost")
                {
                    return monument.transform.position + monument.transform.rotation * new Vector3(-30.62f, 1.87f, 20.95f);
                }
            }
            return Vector3.zero;
        }

        private Vector3 BanditLoc()
        {
            var monuments = UnityEngine.Object.FindObjectsOfType<MonumentInfo>();

            foreach (var monument in monuments)
            {
                if (monument.displayPhrase.english == "Bandit Camp")
                {
                    return monument.transform.position + monument.transform.rotation * new Vector3(-30.62f, 1.87f, 20.95f);
                }
            }
            return Vector3.zero;
        }

        void ClearItemList()
        {
            gridList.Clear();
            decoyPositions.Clear();
            decoyGrid = "";
            pookieEntName.Clear();
        }

        private void EndEvent()
        {
            FindAndKillDecoys();
            eventActive = false;
            if (pookie != null && !pookie.IsDestroyed)
            {
                pookie.Kill();
                pookie = null;
                PrintToChat(GetMessage("notfound"));
                ClearItemList();
            }
            else
            { pookie = null; }
            if (pookieDecoy != null && !pookieDecoy.IsDestroyed)
            {
                pookieDecoy.Kill();
                pookieDecoy = null;

                ClearItemList();
            }

            if (decoyLan != null && !decoyLan.IsDestroyed)
            {
                decoyLan.Kill();
                decoyLan = null;

                ClearItemList();
            }

            if (pookieLan != null && !pookieLan.IsDestroyed)
            {
                pookieLan.Kill();
                pookieLan = null;

                ClearItemList();
            }
            foreach (var pookieMarker in pookieMarkers.Values)
            {
                if (pookieMarker != null && !pookieMarker.IsDestroyed)
                {
                    pookieMarker.Kill();
                }
            }

            // Clear the pookieMarkers dictionary after killing the map markers
            pookieMarkers.Clear();
            eventTimer = null;

            ClearItemList();


        }

        private Vector3 GetRandomSpawnPosition()
        {
            float mapSize = TerrainMeta.Size.x / 2f;
            Vector3 randomPosition = new Vector3(Random.Range(-mapSize, mapSize), 0f, Random.Range(-mapSize, mapSize));
            float y = TerrainMeta.HeightMap.GetHeight(randomPosition);
            Vector3 spawnPosition = new Vector3(randomPosition.x, y + GroundOffset, randomPosition.z);

            if (!TestPos(spawnPosition) || IsNearToolCupboard(spawnPosition) || !IsPositionValid(spawnPosition))
            {
                return GetRandomSpawnPosition();
            }

            return spawnPosition;
        }

        private string GetGridLocation(Vector3 position)
        {
            int gridX = Mathf.FloorToInt(position.x / 146.3f);
            int gridZ = Mathf.FloorToInt(position.z / 146.3f);
            char gridLetter = (char)(gridX + 'A');
            string gridLocation = $"{gridLetter}{gridZ + 1}";
            return gridLocation;
        }

        private bool IsNearToolCupboard(Vector3 position)
        {
            Collider[] colliders = Physics.OverlapSphere(position, 10f, LayerMask.GetMask("Construction"));
            foreach (Collider collider in colliders)
            {
                BuildingPrivlidge privilege = collider.GetComponentInParent<BuildingPrivlidge>();
                if (privilege != null)
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsPositionValid(Vector3 position)
        {
            float terrainHeight = TerrainMeta.HeightMap.GetHeight(position);
            if (position.y - GroundOffset < terrainHeight)
            {
                return false;
            }

            Collider[] colliders = Physics.OverlapSphere(position, 1f, LayerMask.GetMask("Construction", "Deployed"));
            if (colliders.Length > 0)
            {
                return false;
            }

            if (position.y - GroundOffset <= WaterSystem.OceanLevel)
            {
                return false;
            }

            NavMeshHit hit;
            if (!NavMesh.SamplePosition(position, out hit, 1f, NavMesh.AllAreas))
            {
                return false;
            }

            RaycastHit rockHit;
            if (Physics.Raycast(position, Vector3.down, out rockHit, 10f, LayerMask.GetMask("Terrain", "World")))
            {
                string rock = rockHit.collider.sharedMaterial.name;
                if (rockHit.collider.sharedMaterial.name.Contains("Rock") || rockHit.collider.sharedMaterial.name.ToLower().Contains("mountain") || rockHit.collider.sharedMaterial.name.ToLower().Contains("Cliff"))
                {
                    return false;
                }
                if (rock == "Rock")
                {
                    return false;
                }
            }

            return true;
        }

        #region New Pos Spawns

        private bool TestPos(Vector3 randomPos)
        {
            RaycastHit hitInfo;
            if (!Physics.Raycast(randomPos + Vector3.up * 300f, Vector3.down, out hitInfo, 400f, Layers.Solid) ||
                hitInfo.GetEntity() != null)
            {
                return false;
            }

            randomPos.y = hitInfo.point.y;

            var topology = TerrainMeta.TopologyMap.GetTopology(randomPos);
            if (topology == TerrainTopology.CLIFF || topology == TerrainTopology.CLIFFSIDE || topology == TerrainTopology.MONUMENT)
            {
                return false;
            }

            if (AntiHack.TestInsideTerrain(randomPos))
            {
                return false;
            }

            if (!ValidBounds.Test(randomPos))
            {
                return false;
            }
            return TestPosAgain(randomPos);
        }

        private bool TestPosAgain(Vector3 spawnPos)
        {
            if (WaterLevel.Test(spawnPos, true, true))
            {
                return false;
            }

            colliders.Clear();
            Vis.Colliders(spawnPos, 3f, colliders);
            foreach (var collider in colliders)
            {
                switch (collider.gameObject.layer)
                {
                    case (int)Layer.Prevent_Building:
                        return false;

                    case (int)Layer.Vehicle_Large:
                    case (int)Layer.Vehicle_World:
                    case (int)Layer.Vehicle_Detailed:
                        return false;
                }

                if (collider.name.Contains("zonemanager", CompareOptions.IgnoreCase))
                {
                    return false;
                }

                if (collider.name.Contains("radiation", CompareOptions.IgnoreCase))
                {
                    return false;
                }

                if (collider.name.Contains("fireball", CompareOptions.IgnoreCase) ||
                    collider.name.Contains("iceberg", CompareOptions.IgnoreCase) ||
                    collider.name.Contains("ice_sheet", CompareOptions.IgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        #endregion
        private static TerrainTopology.Enum GetPosTopology(Vector3 position) => (TerrainTopology.Enum)TerrainMeta.TopologyMap.GetTopology(position);

        private void SpawnPookieBear(Vector3 position)
        {
            Vector3 newPos = new Vector3(0, 0.01f, 0);
            if (config.specialEvent && EntityScaleManager != null)
            {
                position = position + newPos;
            }

            Quaternion rotation = Quaternion.Euler(0, 90, 0);

            BaseEntity pookiePrefab = GameManager.server.CreateEntity("assets/prefabs/misc/xmas/pookie/pookie_deployed.prefab", position, rotation);

            if (pookiePrefab != null)
            {
                BaseCombatEntity entity = pookiePrefab as BaseCombatEntity;
                entity.Spawn();
                if (config.customHeatlh)
                {
                    entity.InitializeHealth(config.pookieHP, config.pookieHP);
                }
                if (config.pookieMapMarkers)
                {
                    CreatePookieMarker(position, entity.net.ID.ToString());
                }
                entity.name = "Pookie Bear";
                pookieEntName.Add(entity.net.ID);
                pookie = entity;
                AddMarkerToPook(entity);
                if (config.specialEvent)
                {
                    if (EntityScaleManager == null)
                    {
                        Puts("You need to download Entity Scale Manager by WhiteThunder for the event to work!");
                        Puts("https://umod.org/plugins/entity-scale-manager");
                    }
                    else
                    {
                        AddEventToPook(entity);
                    }
                }
            }
        }

        private void SpawnPookieBearDecoy(Vector3 position, int decNum)
        {
            Vector3 newPos = new Vector3(0, 0.01f, 0);
            if (config.specialEvent && EntityScaleManager != null)
            {
                position = position + newPos;
            }

            Quaternion rotation = Quaternion.Euler(0, 90, 0);
            BaseEntity pookiePrefab = GameManager.server.CreateEntity("assets/prefabs/misc/xmas/pookie/pookie_deployed.prefab", position, rotation);
            if (pookiePrefab != null)
            {
                BaseCombatEntity entity = pookiePrefab as BaseCombatEntity;
                entity.Spawn();
                if (config.customHeatlh)
                {
                    entity.InitializeHealth(config.decoyHP, config.decoyHP);
                }
                if (config.pookieMapMarkers)
                {
                    CreatePookieMarker(position, entity.net.ID.ToString());
                }
                entity.name = "Pookie Decoy" + decNum.ToString();
                pookieEntName.Add(entity.net.ID);
                pookieDecoy = entity;
                AddMarkerToPook(entity);
                if (config.specialEvent)
                {
                    if (EntityScaleManager == null)
                    {
                        Puts("You need to download Entity Scale Manager by WhiteThunder for the event to work!");
                        Puts("https://umod.org/plugins/entity-scale-manager");
                    }
                    else
                    {
                        AddEventToPook(entity);
                    }
                }
            }
        }

        void AddMarkerToPook(BaseEntity marker)
        {
            BaseEntity lantern = GameManager.server.CreateEntity(config.pookieObject, new Vector3(-1f, 0, 0));
            lantern.SetFlag(BaseEntity.Flags.On, true);
            lantern.name = "Pookie Lantern";
            lantern.SetParent(marker);
            lantern.Spawn();
        }

        void AddEventToPook(BaseEntity marker)
        {
            BaseEntity lantern = GameManager.server.CreateEntity("assets/prefabs/misc/xmas/sled/sled.deployed.prefab", new Vector3(0f, -0.10f, 0f));
            lantern.SetFlag(BaseEntity.Flags.On, true);
            lantern.name = "pumpkinhead";
            lantern.SetParent(marker);
            lantern.SetFlag(BaseEntity.Flags.InUse, true);
            lantern.SetFlag(BaseEntity.Flags.Busy, true, true);
            lantern.Spawn();
            EntityScaleManager.Call("API_ScaleEntity", lantern, 0.35f);
            var rigidBody = lantern.GetComponent<Rigidbody>();
            if (rigidBody != null)
                rigidBody.isKinematic = true;

            SpawnLights(lantern);
        }
        void SpawnLights(BaseEntity marker)
        {
            Quaternion newRot = Quaternion.Euler(90, 0, 90);
            Quaternion newRotTwo = Quaternion.Euler(-90, 0, -90);
            BaseEntity lightOne = GameManager.server.CreateEntity("assets/prefabs/misc/xmas/christmas_lights/xmas.lightstring.deployed.prefab", new Vector3(0.40f, 0.55f, -0.22f), newRot);
            BaseEntity lightTwo = GameManager.server.CreateEntity("assets/prefabs/misc/xmas/christmas_lights/xmas.lightstring.deployed.prefab", new Vector3(-0.40f, 0.55f, -0.22f), newRotTwo);

            lightOne.SetFlag(BaseEntity.Flags.On, true);
            lightTwo.SetFlag(BaseEntity.Flags.On, true);
            lightOne.name = "pumpkinhead";
            lightTwo.name = "pumpkinhead";
            lightOne.SetParent(marker);
            lightTwo.SetParent(marker);
            lightOne.Spawn();
            lightTwo.Spawn();
           // CreateWorldObject(-1622660759, "pumpkinhead", marker, false);
            EntityScaleManager.Call("API_ScaleEntity", lightOne, 0.35f);
            EntityScaleManager.Call("API_ScaleEntity", lightTwo, 0.35f);
        }

        //private DroppedItem CreatePresentItem(string name, BaseEntity parent, Vector3 localPosition, Vector3 localRotation, bool canPickup = true)
        //{
        //    DroppedItem presentItem = CreatePresentItem(name, parent.transform.position);

        //    presentItem.SetParent(parent);
        //    presentItem.transform.localPosition = localPosition;
        //    presentItem.transform.rotation = Quaternion.Euler(localRotation);

        //    return presentItem;
        //}

        //private DroppedItem CreatePresentItem(string name, Vector3 position, bool canPickup = true)
        //{
        //    BaseEntity presentEntity = CreateWorldObject(-1622660759, name, position, canPickup);

        //    Rigidbody rb = presentEntity.GetComponent<Rigidbody>();
        //    rb.isKinematic = true;

        //    UnityEngine.Object.Destroy(presentEntity.GetComponent<EntityCollisionMessage>());
        //    UnityEngine.Object.Destroy(presentEntity.GetComponent<PhysicsEffects>());

        //    return presentEntity as DroppedItem;
        //}

        //private BaseEntity CreateWorldObject(int itemId, string name, BaseEntity marker, bool canPickup)
        //{
        //    Item item = ItemManager.CreateByItemID(itemId);

        //    if (!string.IsNullOrEmpty(name))
        //        item.name = name;

        //    BaseEntity worldEntity = GameManager.server.CreateEntity(worlItem_Prefab, new Vector3(-0.40f, 0.55f, -0.22f));

        //    WorldItem worldItem = worldEntity as WorldItem;
        //    if (worldItem != null)
        //        worldItem.InitializeItem(item);

        //    worldEntity.Invoke(() =>
        //    {
        //        (worldEntity as DroppedItem).CancelInvoke((worldEntity as DroppedItem).IdleDestroy);
        //    }, 1f);


        //    worldItem.enableSaving = false;
        //    worldItem.allowPickup = true;

        //    worldItem.SetParent(marker);

        //    worldEntity.Spawn();

        //    item.SetWorldEntity(worldEntity);

        //    return worldEntity;
        //}

        private ItemDefinition FindItemDefinition(string shortname)
        {
            foreach (var itemDefinition in ItemManager.itemList)
            {
                if (itemDefinition.shortname == shortname)
                    return itemDefinition;
            }
            return null;
        }

        #endregion

        #region Rewards/Punishments
        private void GivePlayerItem(BasePlayer player, string itemName, int quantity, string name, ulong skin)
        {
            Item item = null;
            if (!config.enableDualReward)
            {
                if (config.srvrRewards)
                {
                    //Use ServerRewards
                    ServerRewards?.Call("AddPoints", player.userID, config.srAmount);
                    player.ChatMessage($"You have received {config.srAmount} RP for killing pookie!");
                    return;
                }
                if (config.ecoUse)
                {
                    //Use Economics
                    Economics?.Call("Deposit", player.userID, (double)config.ecoAmount);
                    player.ChatMessage($"{config.ecoAmount} has been added to your balance for killing pookie!");
                    return;
                }

                if (skin != 0)
                {
                    item = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(itemName).itemid, quantity, skin);
                }
                else
                {
                    item = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(itemName).itemid, quantity);
                }

                if (name != "")
                {
                    item.name = name;
                }

                if (item != null)
                {
                    if (!player.inventory.GiveItem(item, player.inventory.containerMain))
                    {
                        float dropHeight = 5f; // You can adjust this value as needed to set the drop height.
                        Vector3 dropPosition = player.transform.position + Vector3.up * dropHeight;
                        item.Drop(dropPosition, player.transform.forward * 2);
                        player.ChatMessage($"Your inventory was full so {quantity}x {item.info.displayName.translated} were dropped on the ground!!");

                    }
                    else
                    {
                        player.ChatMessage($"You have received {quantity}x {item.info.displayName.translated} for finding Pookie!");
                    }

                }
            }
            else
            {
                if (config.srvrRewards)
                {
                    //Use ServerRewards
                    ServerRewards?.Call("AddPoints", player.userID, config.srAmount);
                    player.ChatMessage($"You have received {config.srAmount} RP for killing pookie!");
                }
                if (config.ecoUse)
                {
                    //Use Economics
                    Economics?.Call("Deposit", player.userID, (double)config.ecoAmount);
                    player.ChatMessage($"{config.ecoAmount} has been added to your balance for killing pookie!");
                }

                if (skin != 0)
                {
                    item = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(itemName).itemid, quantity, skin);
                }
                else
                {
                    item = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(itemName).itemid, quantity);
                }

                if (name != "")
                {
                    item.name = name;
                    item.info.displayName = name;
                }

                if (item != null)
                {
                    if (!player.inventory.GiveItem(item, player.inventory.containerMain))
                    {
                        float dropHeight = 5f; // You can adjust this value as needed to set the drop height.
                        Vector3 dropPosition = player.transform.position + Vector3.up * dropHeight;
                        item.Drop(dropPosition, player.transform.forward * 2);
                        player.ChatMessage($"Your inventory was full so {quantity}x {item.info.displayName.translated} were dropped on the ground!!");

                    }
                    else
                    {
                        player.ChatMessage($"You have received {quantity}x {item.info.displayName.translated} for finding Pookie!");
                    }

                }
            }
        }

        private void ApplyNegativeEffect(BasePlayer player, NegativeEffect negativeEffect)
        {
            int effectAmount = negativeEffect.effectAmt;


            if (negativeEffect.EffectName == "Health")
            {
                player.ChatMessage(GetMessage("health"));
                player.health -= effectAmount;
                if (player.health <= 0f)
                {
                    player.Die();
                }
            }
            else if (negativeEffect.EffectName == "Dehydration")
            {
                player.ChatMessage(GetMessage("dehydration"));
                player.metabolism.hydration.value = effectAmount;
            }
            else if (negativeEffect.EffectName == "Hunger")
            {
                player.ChatMessage(GetMessage("hunger"));
                player.metabolism.calories.value = effectAmount;
            }
            else if (negativeEffect.EffectName == "Radiation")
            {
                player.ChatMessage(GetMessage("radiation"));
                player.metabolism.radiation_poison.value = effectAmount;
            }
            else if (negativeEffect.EffectName == "Bleeding")
            {
                player.ChatMessage(GetMessage("bleeding"));
                player.metabolism.bleeding.value = effectAmount;
            }
            else if (negativeEffect.EffectName == "Drop Inventory")
            {
                if (negativeEffect.effectAmt == 1)
                {
                    player.ChatMessage(GetMessage("invdrop1"));
                    DropPlayerHotbarItems(player);
                }
                else if (negativeEffect.effectAmt == 2)
                {
                    player.ChatMessage(GetMessage("invdrop2"));
                    DropPlayerArmorItems(player);
                }
                else if (negativeEffect.effectAmt == 3)
                {
                    player.ChatMessage(GetMessage("invdrop3"));
                    DropPlayerMainInventoryItems(player);
                }
                else if (negativeEffect.effectAmt == 4)
                {
                    player.ChatMessage(GetMessage("invdrop4"));
                    DropPlayerInventoryItems(player);
                }
                else
                {
                    Puts("Invalid setting for Drop Inventory effect. Please pick a value 1-4");
                }
            }
            else if (negativeEffect.EffectName == "Spawn Bears")
            {
                player.ChatMessage(GetMessage("bear"));
                string spawnAnimal = "bear";
                for (int i = 0; i < effectAmount; i++)
                {
                    Vector3 spawnPosition = player.transform.position + player.transform.forward * 3f;
                    SpawnAnimal(spawnAnimal, spawnPosition);
                }
            }
            else if (negativeEffect.EffectName == "Spawn Wolves")
            {
                player.ChatMessage(GetMessage("wolf"));
                string spawnAnimal = "wolf";
                for (int i = 0; i < effectAmount; i++)
                {
                    Vector3 spawnPosition = player.transform.position + player.transform.forward * 3f;
                    SpawnAnimal(spawnAnimal, spawnPosition);
                }
            }
            else if (negativeEffect.EffectName == "Spawn Boar")
            {
                player.ChatMessage(GetMessage("boar"));
                string spawnAnimal = "boar";
                for (int i = 0; i < effectAmount; i++)
                {
                    Vector3 spawnPosition = player.transform.position + player.transform.forward * 3f;
                    SpawnAnimal(spawnAnimal, spawnPosition);
                }
            }
            else if (negativeEffect.EffectName == "Spikes")
            {
                player.ChatMessage(GetMessage("spike"));
                SpawnSpikesAroundPlayer(player, effectAmount);
            }
            else
            {
                Puts($"Unknown negative effect: {negativeEffect.EffectName}");
            }
        }

        private void ExecuteNegativeCommand(BasePlayer player, NegativeEffectCommands nEffect, string effectMsg)
        {
            if (nEffect != null)
            {
                string commandText = nEffect.effectCommand;

                if (!string.IsNullOrEmpty(commandText))
                {
                    // Replace %steamid% with player's Steam ID
                    string fullCommand = commandText.Replace("%steamid%", player.UserIDString);

                    // Execute the modified command
                    ConsoleSystem.Run(ConsoleSystem.Option.Server, fullCommand);
                    PrintToChat(player, effectMsg);
                }
            }
        }

        private void SpawnAnimal(string animalSpawn, Vector3 pos)
        {
            BaseEntity animal = null;
            if (animalSpawn == "bear")
            {
                animal = GameManager.server.CreateEntity("assets/rust.ai/agents/bear/bear.prefab", pos, new Quaternion(), true);
            }
            else if (animalSpawn == "wolf")
            {
                animal = GameManager.server.CreateEntity("assets/rust.ai/agents/wolf/wolf.prefab", pos, new Quaternion(), true);

            }
            else
            {
                animal = GameManager.server.CreateEntity("assets/rust.ai/agents/boar/boar.prefab", pos, new Quaternion(), true);
            }
            animal.Spawn();
        }

        private void SpawnSpikesAroundPlayer(BasePlayer player, float time)
        {
            int count = 6;
            float radius = 3f;
            float angleIncrement = 2 * Mathf.PI / count;

            for (int i = 0; i < count; i++)
            {
                float angle = i * angleIncrement;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 spawnPosition = player.transform.position + direction * radius;

                BaseEntity entity = GameManager.server.CreateEntity("assets/bundled/prefabs/static/spikes_static.prefab", spawnPosition);
                if (entity != null)
                {
                    entity.Spawn();

                    timer.Once(time, () =>
                    {
                        if (entity != null && !entity.IsDestroyed)
                            entity.Kill();
                    });
                }
            }
        }

        private void DropPlayerHotbarItems(BasePlayer player)
        {
            foreach (var item in player.inventory.containerBelt.itemList.ToList())
            {
                item.Drop(player.transform.position + player.transform.forward * -2f + Vector3.up * 0.5f, player.transform.forward);
            }
        }

        private void DropPlayerArmorItems(BasePlayer player)
        {
            foreach (var item in player.inventory.containerWear.itemList.ToList())
            {
                item.Drop(player.transform.position + player.transform.forward * -2f + Vector3.up * 0.5f, player.transform.forward);
            }
        }

        private void DropPlayerMainInventoryItems(BasePlayer player)
        {
            foreach (var item in player.inventory.containerMain.itemList.ToList())
            {
                item.Drop(player.transform.position + player.transform.forward * -2f + Vector3.up * 0.5f, player.transform.forward);
            }
        }

        private void DropPlayerInventoryItems(BasePlayer player)
        {
            DropPlayerHotbarItems(player);
            DropPlayerArmorItems(player);
            DropPlayerMainInventoryItems(player);
        }
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["spawned"] = "Two Pookie Bears have randomly spawned on the map!",
                ["spawnedgrid"] = "Two Pookie Bears have randomly spawned in grids {0} and {1}!",
                ["spawnedgridmulti"] = "Multiple Pookie Bears have randomly spawned near grids {0}!",
                ["notfound"] = "Pookie wasn't found in time! Don't worry, it will be back!",
                ["pookiekiller"] = "<color={1}>{0}</color> killed the pookie! The event has ended.",
                ["decoykiller"] = "<color={1}>{0}</color> killed the decoy!",
                ["health"] = "You look a little <color=red>Hurt</color>!",
                ["dehydration"] = "You look a little <color=blue>Thirsty</color>!",
                ["hunger"] = "You look a little <color=orange>Hungry</color>!",
                ["radiation"] = "I think your hazzy has a <color=green>Leak</color>!",
                ["bleeding"] = "You might need a <color=red>Bandage</color>!",
                ["invdrop1"] = "Ok, <color=yellow>Butterfingers</color>!",
                ["invdrop2"] = "Put your <color=yellow>clothes</color> back on!",
                ["invdrop3"] = "I think your pockets have a <color=yellow>hole</color> in them!",
                ["invdrop4"] = "You dropped something! Or <color=red>EVERYTHING</color>...",
                ["bear"] = "This is <color=red>BEARy</color> funny!",
                ["wolf"] = "Stop <color=red>howling</color>! Oh, that wasn't you?",
                ["boar"] = "Are you <color=red>BOARd</color>?",
                ["spike"] = "Watch your <color=red>step</color>!",
            }, this);
        }

        string GetMessage(string langKey) => lang.GetMessage(langKey, this);

        string GetMessage(string langKey, params object[] args) => (args.Length == 0) ? GetMessage(langKey) : string.Format(GetMessage(langKey), args);
        #endregion
    }
}
