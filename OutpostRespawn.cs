using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("Outpost Respawn", "RubMyBricks", "1.3.1")]
    [Description("Allows players to respawn at Outpost or Bandit during death.")]
    public class OutpostRespawn : RustPlugin
    {
        #region Variables

        private Configuration configuration;

        private MonumentInfo outpostMonument;
        private MonumentInfo banditCampMonument;

        private Dictionary<ulong, Dictionary<string, float>> playerCooldowns
            = new Dictionary<ulong, Dictionary<string, float>>();

        private Dictionary<ulong, Dictionary<string, float>> playerUIUnlocks
            = new Dictionary<ulong, Dictionary<string, float>>();

        private readonly Dictionary<(ulong, string), SleepingBag> invisibleBags
            = new Dictionary<(ulong, string), SleepingBag>();

        private readonly Dictionary<SleepingBag, string> bagLocations
            = new Dictionary<SleepingBag, string>();

        private readonly Dictionary<ulong, Dictionary<string, (bool singleButton, bool leftSide)>> playerUIState
            = new Dictionary<ulong, Dictionary<string, (bool, bool)>>();

        private readonly List<Vector3> outpostO = new List<Vector3>
        {
            new Vector3(6.7235f, 0.0444f, 41.9094f),
            new Vector3(15.6741f, 0.0443f, -42.1317f),
            new Vector3(-24.2365f, 0.2488f, -9.2070f),
            new Vector3(-23.2494f, 0.0537f, -36.2637f),
        };
        private readonly List<Vector3> outpostR = new List<Vector3>
        {
            new Vector3(0.7998979f, 182.3819f, 0.0f),
            new Vector3(1.6999058f, 271.2569f, 0.0f),
            new Vector3(1.8124722f, 88.7819f, 0.0f),
            new Vector3(2.3749706f, 54.6948f, 0.0f),
        };

        private readonly List<Vector3> banditO = new List<Vector3>
        {
            new Vector3(63.3794f, 2.0065f, -46.4379f),
            new Vector3(53.4470f, 2.4929f, 63.9125f),
            new Vector3(-5.3252f, 2.9515f, -76.1143f),
            new Vector3(52.6162f, 2.4362f, 63.1438f),
        };
        private readonly List<Vector3> banditR = new List<Vector3>
        {
            new Vector3(0.4624849f, 314.457458f, 0.0f),
            new Vector3(3.9499995f, 230.532516f, 0.0f),
            new Vector3(1.2500077f, 345.5074f,  0.0f),
            new Vector3(1.1249817f, 233.10228f, 0.0f),
        };

        private const float OFFSET_FACTOR = 0.01f;

        #endregion

        #region Configuration

        public class Configuration
        {
            [JsonProperty("Options")]
            public OptionsConfig Options { get; set; } = new OptionsConfig();

            [JsonProperty("Outpost")]
            public OutpostConfig Outpost { get; set; } = new OutpostConfig();

            [JsonProperty("Bandit Camp")]
            public BanditCampConfig BanditCamp { get; set; } = new BanditCampConfig();
        }

        public class OptionsConfig
        {
            [JsonProperty("Enable Sleeping Bags (Disables UI)")]
            public bool EnableSleepingBagsDisablesUI { get; set; } = false;

            [JsonProperty("Enable Permissions")]
            public bool EnablePermissions { get; set; } = true;

            [JsonProperty("Enable Outpost")]
            public bool EnableOutpost { get; set; } = true;

            [JsonProperty("Enable Bandit")]
            public bool EnableBandit { get; set; } = true;

            [JsonProperty("Enable Cooldown")]
            public bool EnableCooldown { get; set; } = true;

            [JsonProperty("Enable Auto Wake Up")]
            public bool EnableAutoWakeUp { get; set; } = true;
        }

        public class OutpostConfig
        {
            [JsonProperty("Use Permission")]
            public bool UsePermission { get; set; } = true;

            [JsonProperty("Cooldown")]
            public float Cooldown { get; set; } = 300f;

            [JsonProperty("Unlock Timer")]
            public float UnlockTimer { get; set; } = 0.0f;

            [JsonProperty("Remove Hostility")]
            public bool RemoveHostile { get; set; } = true;

            [JsonProperty("Use Auto Wake Up")]
            public bool UseAutoWakeUp { get; set; } = true;

            [JsonProperty("Outpost UI Settings")]
            public OutpostUISettings OutpostUISettings { get; set; } = new OutpostUISettings();

            [JsonProperty("Outpost Sleeping Bag Settings")]
            public OutpostSleepingBagSettings OutpostSleepingBagSettings { get; set; } = new OutpostSleepingBagSettings();
        }

        public class OutpostUISettings
        {
            [JsonProperty("Button Title Text")]
            public string ButtonTitleText { get; set; } = "Outpost";

            [JsonProperty("Button Respawn Text")]
            public string ButtonRespawnText { get; set; } = "RESPAWN »";

            [JsonProperty("Button Cooldown Text")]
            public string ButtonCooldownText { get; set; } = "COOLDOWN!";

            [JsonProperty("Button Color")]
            public string ButtonColor { get; set; } = "0.34 0.39 0.21 0.8";

            [JsonProperty("Button UI Image URL")]
            public string ButtonUIImageURL { get; set; } = "https://i.imgur.com/nqFeINB.png";

            [JsonProperty("Button UI Size")]
            public float ButtonUISize { get; set; } = 1.0f;

            [JsonProperty("Button Horizontal Offset")]
            public float ButtonHorizontalOffset { get; set; } = 0.0f;

            [JsonProperty("Button Vertical Offset")]
            public float ButtonVerticalOffset { get; set; } = 0.0f;
        }

        public class OutpostSleepingBagSettings
        {
            [JsonProperty("Sleeping Bag Name")]
            public string SleepingBagName { get; set; } = "Outpost";

            [JsonProperty("Bag Location Cycle")]
            public bool BagLocationCycle { get; set; } = false;

            [JsonProperty("Respawn At Sleeping Bag")]
            public bool RespawnAtBag { get; set; } = false;
        }

        public class BanditCampConfig
        {
            [JsonProperty("Use Permission")]
            public bool UsePermission { get; set; } = true;

            [JsonProperty("Cooldown")]
            public float Cooldown { get; set; } = 300f;

            [JsonProperty("Unlock Timer")]
            public float UnlockTimer { get; set; } = 0.0f;

            [JsonProperty("Remove Hostility")]
            public bool RemoveHostile { get; set; } = true;

            [JsonProperty("Use Auto Wake Up")]
            public bool UseAutoWakeUp { get; set; } = true;

            [JsonProperty("Bandit Camp UI Settings")]
            public BanditCampUISettings BanditCampUISettings { get; set; } = new BanditCampUISettings();

            [JsonProperty("Bandit Camp Sleeping Bag Settings")]
            public BanditCampSleepingBagSettings BanditCampSleepingBagSettings { get; set; } = new BanditCampSleepingBagSettings();
        }

        public class BanditCampUISettings
        {
            [JsonProperty("Button Title Text")]
            public string ButtonTitleText { get; set; } = "Bandit Camp";

            [JsonProperty("Button Respawn Text")]
            public string ButtonRespawnText { get; set; } = "RESPAWN »";

            [JsonProperty("Button Cooldown Text")]
            public string ButtonCooldownText { get; set; } = "COOLDOWN!";

            [JsonProperty("Button Color")]
            public string ButtonColor { get; set; } = "0.34 0.39 0.21 0.8";

            [JsonProperty("Button UI Image URL")]
            public string ButtonUIImageURL { get; set; } = "https://i.imgur.com/nqFeINB.png";

            [JsonProperty("Button UI Size")]
            public float ButtonUISize { get; set; } = 1.0f;

            [JsonProperty("Button Horizontal Offset")]
            public float ButtonHorizontalOffset { get; set; } = 0.0f;

            [JsonProperty("Button Vertical Offset")]
            public float ButtonVerticalOffset { get; set; } = 0.0f;
        }

        public class BanditCampSleepingBagSettings
        {
            [JsonProperty("Sleeping Bag Name")]
            public string SleepingBagName { get; set; } = "Bandit Camp";

            [JsonProperty("Bag Location Cycle")]
            public bool BagLocationCycle { get; set; } = false;

            [JsonProperty("Respawn At Sleeping Bag")]
            public bool RespawnAtBag { get; set; } = false;
        }

        protected override void LoadDefaultConfig()
        {
            configuration = new Configuration
            {
                Options = new OptionsConfig
                {
                    EnableSleepingBagsDisablesUI = false,
                    EnablePermissions = true,
                    EnableOutpost = true,
                    EnableBandit = true,
                    EnableCooldown = true,
                    EnableAutoWakeUp = true
                },
                Outpost = new OutpostConfig
                {
                    UsePermission = true,
                    Cooldown = 300.0f,
                    UnlockTimer = 0.0f,
                    RemoveHostile = true,
                    UseAutoWakeUp = true,
                    OutpostUISettings = new OutpostUISettings
                    {
                        ButtonTitleText = "Outpost",
                        ButtonRespawnText = "RESPAWN »",
                        ButtonCooldownText = "COOLDOWN!",
                        ButtonColor = "0.34 0.39 0.21 0.8",
                        ButtonUIImageURL = "https://i.imgur.com/nqFeINB.png",
                        ButtonUISize = 1.0f,
                        ButtonHorizontalOffset = 0.0f,
                        ButtonVerticalOffset = 0.0f
                    },
                    OutpostSleepingBagSettings = new OutpostSleepingBagSettings
                    {
                        SleepingBagName = "Outpost",
                        BagLocationCycle = false,
                        RespawnAtBag = false
                    }
                },
                BanditCamp = new BanditCampConfig
                {
                    UsePermission = true,
                    Cooldown = 300.0f,
                    UnlockTimer = 0.0f,
                    RemoveHostile = true,
                    UseAutoWakeUp = true,
                    BanditCampUISettings = new BanditCampUISettings
                    {
                        ButtonTitleText = "Bandit Camp",
                        ButtonRespawnText = "RESPAWN »",
                        ButtonCooldownText = "COOLDOWN!",
                        ButtonColor = "0.34 0.39 0.21 0.8",
                        ButtonUIImageURL = "https://i.imgur.com/nqFeINB.png",
                        ButtonUISize = 1.0f,
                        ButtonHorizontalOffset = 0.0f,
                        ButtonVerticalOffset = 0.0f
                    },
                    BanditCampSleepingBagSettings = new BanditCampSleepingBagSettings
                    {
                        SleepingBagName = "Bandit Camp",
                        BagLocationCycle = false,
                        RespawnAtBag = false
                    }
                }
            };
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                configuration = Config.ReadObject<Configuration>();
                if (configuration == null)
                    throw new Exception("Config was null after reading.");
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(configuration);

        #endregion

        #region Initialization

        private void OnServerInitialized()
        {
            LoadConfig();
            RegisterPermissions();

            AttemptMonumentDetection();

            if (configuration.Options.EnableSleepingBagsDisablesUI)
            {
                foreach (var p in BasePlayer.activePlayerList)
                {
                    RemoveOldBagsForPlayer(p);
                    CreateOrRelocateBagsForPlayer(p);
                }
            }
            else
            {
                foreach (var p in BasePlayer.activePlayerList)
                {
                    StartUILocksForPlayer(p);

                    if (p.IsDead())
                    {
                        timer.Once(2f, () =>
                        {
                            ShowMonumentUI(p);
                        });
                    }
                }
            }
        }

        private void AttemptMonumentDetection()
        {
            if (configuration.Options.EnableOutpost)
            {
                outpostMonument = FindMonumentByName("outpost");
                if (outpostMonument == null)
                {
                    PrintWarning("Outpost has not been found and has been disabled.");
                    configuration.Options.EnableOutpost = false;
                }
            }
            if (configuration.Options.EnableBandit)
            {
                banditCampMonument = FindMonumentByName("bandit camp");
                if (banditCampMonument == null)
                {
                    PrintWarning("Banditcamp has not been found and has been disabled.");
                    configuration.Options.EnableBandit = false;
                }
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (configuration.Options.EnableSleepingBagsDisablesUI)
            {
                CreateOrRelocateBagsForPlayer(player);
            }
            else
            {
                StartUILocksForPlayer(player);

                if (player.IsDead())
                {
                    timer.Once(2f, () =>
                    {
                        if (player.IsDead())
                            ShowMonumentUI(player);
                    });
                }
            }
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (configuration.Options.EnableSleepingBagsDisablesUI)
            {
                RandomizeBagsForPlayer(player);
            }
            else
            {
                timer.Once(4f, () =>
                {
                    if (player.IsDead())
                        ShowMonumentUI(player);
                });
            }
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            RemoveRespawnButtonUI(player);

            if (configuration.Options.EnableSleepingBagsDisablesUI)
            {
                var usedBag = FindRecentlyUsedBag(player);
                if (usedBag != null && bagLocations.ContainsKey(usedBag))
                {
                    string locationKey = bagLocations[usedBag];

                    bool respawnAtBag = locationKey.Equals("Outpost", StringComparison.OrdinalIgnoreCase)
                        ? configuration.Outpost.OutpostSleepingBagSettings.RespawnAtBag
                        : configuration.BanditCamp.BanditCampSleepingBagSettings.RespawnAtBag;

                    if (!respawnAtBag)
                    {
                        TeleportToRandomLocation(player, locationKey);
                    }

                    ApplyRespawnSettings(player, locationKey);
                }
            }

            NextTick(() =>
            {
                if (player != null && player.IsConnected && !player.IsSleeping())
                {
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, false);
                    player.UpdateNetworkGroup();
                    player.SendNetworkUpdateImmediate();
                    player.ClearEntityQueue();
                    player.SendFullSnapshot();
                }
            });
        }

        private SleepingBag FindRecentlyUsedBag(BasePlayer player, float maxDistance = 3f)
        {
            SleepingBag bestBag = null;
            float bestDist = float.MaxValue;
            Vector3 pPos = player.transform.position;
            foreach (var kvp in invisibleBags)
            {
                var (userID, bagLocationKey) = kvp.Key;
                var bagEntity = kvp.Value;
                if (userID != player.userID || bagEntity == null || bagEntity.IsDestroyed)
                    continue;
                float dist = Vector3.Distance(pPos, bagEntity.transform.position);
                if (dist < bestDist && dist <= maxDistance)
                {
                    bestDist = dist;
                    bestBag = bagEntity;
                }
            }
            return bestBag;
        }

        private void RegisterPermissions()
        {
            if (!configuration.Options.EnablePermissions)
                return;
            if (!permission.PermissionExists("outpostrespawn.outpost"))
                permission.RegisterPermission("outpostrespawn.outpost", this);
            if (!permission.PermissionExists("outpostrespawn.bandit"))
                permission.RegisterPermission("outpostrespawn.bandit", this);
        }

        #endregion

        #region Sleeping Bags

        private void CreateOrRelocateBagsForPlayer(BasePlayer player)
        {
            if (configuration.Options.EnableOutpost && outpostMonument != null)
            {
                if (configuration.Options.EnablePermissions && configuration.Outpost.UsePermission)
                {
                    if (permission.UserHasPermission(player.UserIDString, "outpostrespawn.outpost"))
                        CreateOrRelocateBag(player, "Outpost");
                }
                else
                {
                    CreateOrRelocateBag(player, "Outpost");
                }
            }
            if (configuration.Options.EnableBandit && banditCampMonument != null)
            {
                if (configuration.Options.EnablePermissions && configuration.BanditCamp.UsePermission)
                {
                    if (permission.UserHasPermission(player.UserIDString, "outpostrespawn.bandit"))
                        CreateOrRelocateBag(player, "BanditCamp");
                }
                else
                {
                    CreateOrRelocateBag(player, "BanditCamp");
                }
            }
        }

        private void RandomizeBagsForPlayer(BasePlayer player)
        {
            foreach (var locationKey in new[] { "Outpost", "BanditCamp" })
            {
                var tupleKey = (player.userID, locationKey);
                if (!invisibleBags.ContainsKey(tupleKey)) continue;

                bool bagLocationRotation = locationKey.Equals("Outpost", StringComparison.OrdinalIgnoreCase)
                    ? configuration.Outpost.OutpostSleepingBagSettings.BagLocationCycle
                    : configuration.BanditCamp.BanditCampSleepingBagSettings.BagLocationCycle;

                bool allowOutpost = (locationKey == "Outpost" && configuration.Options.EnableOutpost);
                bool allowBandit = (locationKey == "BanditCamp" && configuration.Options.EnableBandit);

                if ((allowOutpost || allowBandit) && bagLocationRotation)
                {
                    RelocateBag(invisibleBags[tupleKey], locationKey);
                }
            }
        }

        private void CreateOrRelocateBag(BasePlayer player, string locationKey)
        {
            var tupleKey = (player.userID, locationKey);
            if (!invisibleBags.ContainsKey(tupleKey))
            {
                var newBag = CreateNewBag(player, locationKey);
                if (newBag != null)
                {
                    invisibleBags[tupleKey] = newBag;
                }
            }
            else
            {
                RelocateBag(invisibleBags[tupleKey], locationKey);
            }
        }

        private SleepingBag CreateNewBag(BasePlayer player, string locationKey)
        {
            var data = GetMonumentSetup(locationKey);
            if (data == null) return null;
            MonumentInfo monumentRef = data.Item1;
            List<Vector3> spawnList = data.Item2;
            List<Vector3> rotList = data.Item3;
            if (monumentRef == null || spawnList.Count == 0)
                return null;

            int idx = UnityEngine.Random.Range(0, spawnList.Count);
            Vector3 localPos = spawnList[idx];
            Quaternion localRot = Quaternion.Euler(rotList[idx]);
            Vector3 spawnPos = monumentRef.transform.TransformPoint(localPos);
            spawnPos = GetGroundPosition(spawnPos);
            Quaternion spawnRot = monumentRef.transform.rotation * localRot;

            const string bagPrefab = "assets/prefabs/deployable/sleeping bag/sleepingbag_leather_deployed.prefab";
            var bag = GameManager.server.CreateEntity(bagPrefab, spawnPos, spawnRot) as SleepingBag;
            if (bag == null)
                return null;

            bag.RespawnType = ProtoBuf.RespawnInformation.SpawnOptions.RespawnType.Bed;
            bag.deployerUserID = player.userID;
            bag.OwnerID = player.userID;

            if (locationKey.Equals("Outpost", StringComparison.OrdinalIgnoreCase))
            {
                var outCfg = configuration.Outpost;
                bag.niceName = outCfg.OutpostSleepingBagSettings.SleepingBagName;
                if (!configuration.Options.EnableCooldown)
                {
                    bag.unlockTime = Time.realtimeSinceStartup + 0f;
                    bag.secondsBetweenReuses = 0f;
                }
                else
                {
                    bag.unlockTime = Time.realtimeSinceStartup + outCfg.UnlockTimer;
                    bag.secondsBetweenReuses = outCfg.Cooldown;
                }
            }
            else
            {
                var banditCfg = configuration.BanditCamp;
                bag.niceName = banditCfg.BanditCampSleepingBagSettings.SleepingBagName;
                if (!configuration.Options.EnableCooldown)
                {
                    bag.unlockTime = Time.realtimeSinceStartup + 0f;
                    bag.secondsBetweenReuses = 0f;
                }
                else
                {
                    bag.unlockTime = Time.realtimeSinceStartup + banditCfg.UnlockTimer;
                    bag.secondsBetweenReuses = banditCfg.Cooldown;
                }
            }

            bag.Spawn();
            bag.limitNetworking = true;
            bag.EnableSaving(false);
            bag.SendNetworkUpdateImmediate();

            foreach (var collider in bag.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            if (!SleepingBag.sleepingBags.Contains(bag))
            {
                SleepingBag.sleepingBags.Add(bag);
            }

            bagLocations[bag] = locationKey;

            NextTick(() =>
            {
                if (player != null && player.IsConnected)
                {
                    player.SendRespawnOptions();
                }
            });
            return bag;
        }

        private void RelocateBag(SleepingBag bag, string locationKey)
        {
            if (bag == null || bag.net == null) return;
            var data = GetMonumentSetup(locationKey);
            if (data == null) return;
            MonumentInfo monumentRef = data.Item1;
            List<Vector3> spawnList = data.Item2;
            List<Vector3> rotList = data.Item3;
            if (monumentRef == null || spawnList.Count == 0)
                return;

            int idx = UnityEngine.Random.Range(0, spawnList.Count);
            Vector3 localPos = spawnList[idx];
            Quaternion localRot = Quaternion.Euler(rotList[idx]);
            Vector3 spawnPos = monumentRef.transform.TransformPoint(localPos);
            spawnPos = GetGroundPosition(spawnPos);
            Quaternion spawnRot = monumentRef.transform.rotation * localRot;

            bag.transform.position = spawnPos;
            bag.transform.rotation = spawnRot;
        }

        private void RemoveOldBagsForPlayer(BasePlayer player)
        {
            foreach (var locKey in new[] { "Outpost", "BanditCamp" })
            {
                var tupleKey = (player.userID, locKey);
                if (!invisibleBags.ContainsKey(tupleKey)) continue;
                var existingBag = invisibleBags[tupleKey];
                if (existingBag != null && !existingBag.IsDestroyed)
                {
                    if (SleepingBag.sleepingBags.Contains(existingBag))
                        SleepingBag.sleepingBags.Remove(existingBag);
                    if (bagLocations.ContainsKey(existingBag))
                        bagLocations.Remove(existingBag);
                    existingBag.Kill();
                }
                invisibleBags.Remove(tupleKey);
            }
        }

        private Tuple<MonumentInfo, List<Vector3>, List<Vector3>> GetMonumentSetup(string locationKey)
        {
            if (locationKey.Equals("Outpost", StringComparison.OrdinalIgnoreCase))
            {
                return new Tuple<MonumentInfo, List<Vector3>, List<Vector3>>(outpostMonument, outpostO, outpostR);
            }
            else if (locationKey.Equals("BanditCamp", StringComparison.OrdinalIgnoreCase))
            {
                return new Tuple<MonumentInfo, List<Vector3>, List<Vector3>>(banditCampMonument, banditO, banditR);
            }
            return null;
        }

        #endregion

        #region UI Management

        private void HandleRespawn(BasePlayer player, string locationKey)
        {
            var data = GetMonumentSetup(locationKey);
            if (data == null)
            {
                player.ChatMessage($"No in-game Monument found for {locationKey}. Cannot respawn there.");
                return;
            }
            MonumentInfo monumentRef = data.Item1;
            List<Vector3> spawnList = data.Item2;
            List<Vector3> rotList = data.Item3;
            if (monumentRef == null || spawnList.Count == 0) return;

            int idx = UnityEngine.Random.Range(0, spawnList.Count);
            Vector3 localPos = spawnList[idx];
            Quaternion localRot = Quaternion.Euler(rotList[idx]);
            Vector3 globalPos = monumentRef.transform.TransformPoint(localPos);
            globalPos = GetGroundPosition(globalPos);
            Quaternion globalRot = monumentRef.transform.rotation * localRot;

            if (!configuration.Options.EnableSleepingBagsDisablesUI)
            {
                player.RespawnAt(globalPos, globalRot);
            }
            else
            {
                player.RespawnAt(globalPos, Quaternion.identity);
            }
            NextTick(() => ApplyRespawnSettings(player, locationKey));
        }

        private void ApplyRespawnSettings(BasePlayer player, string locationKey)
        {
            bool removeHostile;
            bool autoWakeUp;
            if (locationKey.Equals("Outpost", StringComparison.OrdinalIgnoreCase))
            {
                removeHostile = configuration.Outpost.RemoveHostile;
                autoWakeUp    = configuration.Outpost.UseAutoWakeUp;
            }
            else
            {
                removeHostile = configuration.BanditCamp.RemoveHostile;
                autoWakeUp    = configuration.BanditCamp.UseAutoWakeUp;
            }
            if (configuration.Options.EnableAutoWakeUp && autoWakeUp)
            {
                timer.Once(3f, () => player.EndSleeping());
            }
            if (removeHostile && player.IsHostile())
            {
                player.State.unHostileTimestamp = 0.0f;
                player.DirtyPlayerState();
            }
        }

        private void ShowMonumentUI(BasePlayer player)
        {
            if (configuration.Options.EnableSleepingBagsDisablesUI) return;
            if (!player.IsDead())
            {
                RemoveRespawnButtonUI(player);
                return;
            }

            var validChoices = new List<string>();
            if (configuration.Options.EnableOutpost && outpostMonument != null)
            {
                if (configuration.Options.EnablePermissions && configuration.Outpost.UsePermission)
                {
                    if (permission.UserHasPermission(player.UserIDString, "outpostrespawn.outpost"))
                    {
                        validChoices.Add("Outpost");
                    }
                }
                else
                {
                    validChoices.Add("Outpost");
                }
            }
            if (configuration.Options.EnableBandit && banditCampMonument != null)
            {
                if (configuration.Options.EnablePermissions && configuration.BanditCamp.UsePermission)
                {
                    if (permission.UserHasPermission(player.UserIDString, "outpostrespawn.bandit"))
                    {
                        validChoices.Add("BanditCamp");
                    }
                }
                else
                {
                    validChoices.Add("BanditCamp");
                }
            }
            if (validChoices.Count == 0) return;

            RemoveRespawnButtonUI(player);

            if (validChoices.Count == 1)
            {
                AddRespawnButtonUI(player, validChoices[0], singleButton: true, leftSide: true);
            }
            else
            {
                AddRespawnButtonUI(player, validChoices[0], singleButton: false, leftSide: true);
                AddRespawnButtonUI(player, validChoices[1], singleButton: false, leftSide: false);
            }
        }

        private void AdjustAnchorsForScale(ref string anchorMin, ref string anchorMax, float scale)
        {
            var minParts = anchorMin.Split(' ');
            var maxParts = anchorMax.Split(' ');
            float minX = float.Parse(minParts[0], CultureInfo.InvariantCulture);
            float minY = float.Parse(minParts[1], CultureInfo.InvariantCulture);
            float maxX = float.Parse(maxParts[0], CultureInfo.InvariantCulture);
            float maxY = float.Parse(maxParts[1], CultureInfo.InvariantCulture);

            float centerX = (minX + maxX) * 0.5f;
            float centerY = (minY + maxY) * 0.5f;
            float sizeX = maxX - minX;
            float sizeY = maxY - minY;

            sizeX *= scale;
            sizeY *= scale;

            float halfX = sizeX / 2f;
            float halfY = sizeY / 2f;
            anchorMin = $"{(centerX - halfX).ToString("F4", CultureInfo.InvariantCulture)} {(centerY - halfY).ToString("F4", CultureInfo.InvariantCulture)}";
            anchorMax = $"{(centerX + halfX).ToString("F4", CultureInfo.InvariantCulture)} {(centerY + halfY).ToString("F4", CultureInfo.InvariantCulture)}";
        }

        private void AdjustImageAnchorsRelative(
            ref string imageAnchorMin, ref string imageAnchorMax,
            string origButtonMin, string origButtonMax,
            string newButtonMin, string newButtonMax)
        {
            var obMinParts = origButtonMin.Split(' ');
            var obMaxParts = origButtonMax.Split(' ');
            float obMinX = float.Parse(obMinParts[0], CultureInfo.InvariantCulture);
            float obMinY = float.Parse(obMinParts[1], CultureInfo.InvariantCulture);
            float obMaxX = float.Parse(obMaxParts[0], CultureInfo.InvariantCulture);
            float obMaxY = float.Parse(obMaxParts[1], CultureInfo.InvariantCulture);

            var oiMinParts = imageAnchorMin.Split(' ');
            var oiMaxParts = imageAnchorMax.Split(' ');
            float oiMinX = float.Parse(oiMinParts[0], CultureInfo.InvariantCulture);
            float oiMinY = float.Parse(oiMinParts[1], CultureInfo.InvariantCulture);
            float oiMaxX = float.Parse(oiMaxParts[0], CultureInfo.InvariantCulture);
            float oiMaxY = float.Parse(oiMaxParts[1], CultureInfo.InvariantCulture);

            var nbMinParts = newButtonMin.Split(' ');
            var nbMaxParts = newButtonMax.Split(' ');
            float nbMinX = float.Parse(nbMinParts[0], CultureInfo.InvariantCulture);
            float nbMinY = float.Parse(nbMinParts[1], CultureInfo.InvariantCulture);
            float nbMaxX = float.Parse(nbMaxParts[0], CultureInfo.InvariantCulture);
            float nbMaxY = float.Parse(nbMaxParts[1], CultureInfo.InvariantCulture);

            float obSizeX = obMaxX - obMinX;
            float obSizeY = obMaxY - obMinY;
            float nbSizeX = nbMaxX - nbMinX;
            float nbSizeY = nbMaxY - nbMinY;

            float fracMinX = (oiMinX - obMinX) / obSizeX;
            float fracMinY = (oiMinY - obMinY) / obSizeY;
            float fracMaxX = (oiMaxX - obMinX) / obSizeX;
            float fracMaxY = (oiMaxY - obMinY) / obSizeY;

            float newIMinX = nbMinX + fracMinX * nbSizeX;
            float newIMinY = nbMinY + fracMinY * nbSizeY;
            float newIMaxX = nbMinX + fracMaxX * nbSizeX;
            float newIMaxY = nbMinY + fracMaxY * nbSizeY;

            imageAnchorMin = $"{newIMinX.ToString("F4", CultureInfo.InvariantCulture)} {newIMinY.ToString("F4", CultureInfo.InvariantCulture)}";
            imageAnchorMax = $"{newIMaxX.ToString("F4", CultureInfo.InvariantCulture)} {newIMaxY.ToString("F4", CultureInfo.InvariantCulture)}";
        }

        private void ApplyOffsets(ref string anchorMin, ref string anchorMax, float offsetX, float offsetY)
        {
            float xAnchorOffset = offsetX * OFFSET_FACTOR;
            float yAnchorOffset = offsetY * OFFSET_FACTOR;

            var minParts = anchorMin.Split(' ');
            var maxParts = anchorMax.Split(' ');

            float minX = float.Parse(minParts[0], CultureInfo.InvariantCulture);
            float minY = float.Parse(minParts[1], CultureInfo.InvariantCulture);
            float maxX = float.Parse(maxParts[0], CultureInfo.InvariantCulture);
            float maxY = float.Parse(maxParts[1], CultureInfo.InvariantCulture);

            minX += xAnchorOffset;
            maxX += xAnchorOffset;
            minY += yAnchorOffset;
            maxY += yAnchorOffset;

            anchorMin = $"{minX.ToString("F4", CultureInfo.InvariantCulture)} {minY.ToString("F4", CultureInfo.InvariantCulture)}";
            anchorMax = $"{maxX.ToString("F4", CultureInfo.InvariantCulture)} {maxY.ToString("F4", CultureInfo.InvariantCulture)}";
        }

        private void AddRespawnButtonUI(BasePlayer player, string locationKey, bool singleButton, bool leftSide)
        {
            if (!player.IsDead()) return;

            bool isUILocked = IsUILockActive(player.userID, locationKey);

            bool isOnCooldown = configuration.Options.EnableCooldown && IsCooldownActive(player.userID, locationKey);

            string buttonName = $"monumentrespawnbutton_{locationKey}_{(leftSide ? "left" : "right")}";
            string imageName  = $"monumentrespawnguiimg_{locationKey}_{(leftSide ? "left" : "right")}";
            var container = new CuiElementContainer();

            string command = (isOnCooldown || isUILocked)
                ? ""
                : $"monumentrespawnui2_respawn {player.userID} {locationKey}";

            string closeString = (isOnCooldown || isUILocked) ? "" : buttonName;

            string truncatedName, buttonColor, buttonRespawnText, buttonCooldownText, buttonUIImage;
            float buttonUISize, horizontalOffset, verticalOffset;

            if (locationKey.Equals("Outpost", StringComparison.OrdinalIgnoreCase))
            {
                truncatedName     = configuration.Outpost.OutpostUISettings.ButtonTitleText;
                buttonColor       = configuration.Outpost.OutpostUISettings.ButtonColor;
                buttonRespawnText = configuration.Outpost.OutpostUISettings.ButtonRespawnText;
                buttonCooldownText= configuration.Outpost.OutpostUISettings.ButtonCooldownText;
                buttonUIImage     = configuration.Outpost.OutpostUISettings.ButtonUIImageURL;
                buttonUISize      = configuration.Outpost.OutpostUISettings.ButtonUISize;
                horizontalOffset  = configuration.Outpost.OutpostUISettings.ButtonHorizontalOffset;
                verticalOffset    = configuration.Outpost.OutpostUISettings.ButtonVerticalOffset;
            }
            else
            {
                truncatedName     = configuration.BanditCamp.BanditCampUISettings.ButtonTitleText;
                buttonColor       = configuration.BanditCamp.BanditCampUISettings.ButtonColor;
                buttonRespawnText = configuration.BanditCamp.BanditCampUISettings.ButtonRespawnText;
                buttonCooldownText= configuration.BanditCamp.BanditCampUISettings.ButtonCooldownText;
                buttonUIImage     = configuration.BanditCamp.BanditCampUISettings.ButtonUIImageURL;
                buttonUISize      = configuration.BanditCamp.BanditCampUISettings.ButtonUISize;
                horizontalOffset  = configuration.BanditCamp.BanditCampUISettings.ButtonHorizontalOffset;
                verticalOffset    = configuration.BanditCamp.BanditCampUISettings.ButtonVerticalOffset;
            }

            string origButtonAnchorMin, origButtonAnchorMax;
            string origImageAnchorMin, origImageAnchorMax;
            if (singleButton)
            {
                origButtonAnchorMin = "0.40 0.16";
                origButtonAnchorMax = "0.59 0.26";
                origImageAnchorMin  = "0.41 0.17";
                origImageAnchorMax  = "0.46 0.25";
            }
            else if (leftSide)
            {
                origButtonAnchorMin = "0.25 0.16";
                origButtonAnchorMax = "0.44 0.26";
                origImageAnchorMin  = "0.26 0.17";
                origImageAnchorMax  = "0.31 0.25";
            }
            else
            {
                origButtonAnchorMin = "0.56 0.16";
                origButtonAnchorMax = "0.75 0.26";
                origImageAnchorMin  = "0.57 0.17";
                origImageAnchorMax  = "0.62 0.25";
            }

            string newButtonAnchorMin = origButtonAnchorMin;
            string newButtonAnchorMax = origButtonAnchorMax;
            string newImageAnchorMin  = origImageAnchorMin;
            string newImageAnchorMax  = origImageAnchorMax;

            AdjustAnchorsForScale(ref newButtonAnchorMin, ref newButtonAnchorMax, buttonUISize);
            AdjustImageAnchorsRelative(ref newImageAnchorMin, ref newImageAnchorMax,
                origButtonAnchorMin, origButtonAnchorMax,
                newButtonAnchorMin, newButtonAnchorMax);

            ApplyOffsets(ref newButtonAnchorMin, ref newButtonAnchorMax, horizontalOffset, verticalOffset);
            ApplyOffsets(ref newImageAnchorMin,  ref newImageAnchorMax,  horizontalOffset, verticalOffset);

            int baseFontSize = 20;
            float scaleFactor = buttonUISize;
            int scaledFontSize     = Mathf.RoundToInt(baseFontSize * scaleFactor);
            int scaledHeadlineSize = Mathf.RoundToInt(24 * scaleFactor);

            string colorString = buttonColor;
            string textString;
            if (isUILocked)
            {
                colorString = "0.8 0.34 0.34 0.8";
                textString =
                    $"<size={scaledHeadlineSize}>                 {truncatedName.ToUpper()} \n" +
                    $"                 <color=#FF6347>{buttonCooldownText}</color></size>";
            }
            else if (isOnCooldown)
            {
                colorString = "0.8 0.34 0.34 0.8";
                float remainingTime = GetRemainingCooldown(player.userID, locationKey);
                textString =
                    $"<size={scaledHeadlineSize}>                 {truncatedName.ToUpper()} \n" +
                    $"                 <color=#FF6347>{buttonCooldownText} ({remainingTime:F1}s)</color></size>";
            }
            else
            {
                textString =
                    $"<size={scaledHeadlineSize}>                 {truncatedName.ToUpper()} \n" +
                    $"                 <color=#92BC49>{buttonRespawnText}</color></size>";
            }

            container.Add(new CuiButton
            {
                Button = { Command = command, Color = colorString, Close = closeString },
                RectTransform = { AnchorMin = newButtonAnchorMin, AnchorMax = newButtonAnchorMax },
                Text = { Text = textString, FontSize = scaledFontSize, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, "Overlay", buttonName);

            container.Add(new CuiElement
            {
                Name = imageName,
                Parent = "Overlay",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Color = "1 1 1 1",
                        Url   = buttonUIImage,
                        Sprite= "assets/content/textures/generic/fulltransparent.tga"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = newImageAnchorMin,
                        AnchorMax = newImageAnchorMax
                    }
                }
            });

            if (!playerUIState.ContainsKey(player.userID))
                playerUIState[player.userID] = new Dictionary<string, (bool, bool)>();
            playerUIState[player.userID][locationKey] = (singleButton, leftSide);

            CuiHelper.AddUi(player, container);
        }

        private void RemoveSingleButtonUI(BasePlayer player, string locationKey)
        {
            foreach (var side in new[] { "left", "right" })
            {
                string buttonName = $"monumentrespawnbutton_{locationKey}_{side}";
                string imageName  = $"monumentrespawnguiimg_{locationKey}_{side}";
                CuiHelper.DestroyUi(player, buttonName);
                CuiHelper.DestroyUi(player, imageName);
            }
        }

        private void RefreshSingleButtonUI(BasePlayer player, string locationKey)
        {
            if (!player.IsDead()) return;
            if (!playerUIState.TryGetValue(player.userID, out var locationDict))
                return;
            if (!locationDict.TryGetValue(locationKey, out var info))
                return;
            RemoveSingleButtonUI(player, locationKey);
            AddRespawnButtonUI(player, locationKey, info.singleButton, info.leftSide);
        }

        private void RemoveRespawnButtonUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "monumentrespawnbutton");
            CuiHelper.DestroyUi(player, "monumentrespawnguiimg");
            CuiHelper.DestroyUi(player, "monumentrespawncooldownoverlay");
            CuiHelper.DestroyUi(player, "cooldownUI");

            foreach (var locationKey in new[] { "Outpost", "BanditCamp" })
            {
                foreach (var side in new[] { "left", "right" })
                {
                    var buttonName   = $"monumentrespawnbutton_{locationKey}_{side}";
                    var imageName    = $"monumentrespawnguiimg_{locationKey}_{side}";
                    var overlayName  = $"monumentrespawncooldownoverlay_{locationKey}_{side}";
                    var panelName    = $"cooldownUI_{overlayName}";

                    CuiHelper.DestroyUi(player, buttonName);
                    CuiHelper.DestroyUi(player, imageName);
                    CuiHelper.DestroyUi(player, overlayName);
                    CuiHelper.DestroyUi(player, panelName);
                }
            }
        }

        [ConsoleCommand("monumentrespawnui2_respawn")]
        private void CmdRespawn(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length < 2) return;
            var p = BasePlayer.FindByID(Convert.ToUInt64(arg.Args[0]));
            if (p == null || !p.IsDead()) return;

            string locationKey = arg.Args[1];

            if (IsUILockActive(p.userID, locationKey))
            {
                p.ChatMessage("You must wait for the UI unlock timer to finish (no time displayed).");
                return;
            }
            if (configuration.Options.EnableCooldown && IsCooldownActive(p.userID, locationKey))
            {
                if (playerCooldowns.TryGetValue(p.userID, out var locationCooldowns) &&
                    locationCooldowns.TryGetValue(locationKey, out float expiryTime))
                {
                    float remaining = expiryTime - Time.time;
                    p.ChatMessage($"You are still on cooldown for {locationKey} ({remaining:F1}s).");
                }
                else
                {
                    p.ChatMessage($"You are still on cooldown for {locationKey}.");
                }
                return;
            }

            HandleRespawn(p, locationKey);

            if (locationKey.Equals("Outpost", StringComparison.OrdinalIgnoreCase))
            {
                if (configuration.Outpost.Cooldown > 0f && configuration.Options.EnableCooldown)
                {
                    StartCooldown(p.userID, locationKey, configuration.Outpost.Cooldown, p);
                }
            }
            else
            {
                if (configuration.BanditCamp.Cooldown > 0f && configuration.Options.EnableCooldown)
                {
                    StartCooldown(p.userID, locationKey, configuration.BanditCamp.Cooldown, p);
                }
            }

            NextTick(() => RemoveRespawnButtonUI(p));
        }

        #endregion

        #region Cooldown

        private bool IsCooldownActive(ulong playerID, string locationKey)
        {
            if (!configuration.Options.EnableCooldown) return false;
            if (!playerCooldowns.ContainsKey(playerID)) return false;
            if (!playerCooldowns[playerID].ContainsKey(locationKey)) return false;
            float expiryTime = playerCooldowns[playerID][locationKey];
            float currentTime = Time.time;
            return currentTime < expiryTime;
        }

        private float GetRemainingCooldown(ulong playerID, string locationKey)
        {
            if (!playerCooldowns.ContainsKey(playerID) ||
                !playerCooldowns[playerID].ContainsKey(locationKey))
                return 0f;

            return Math.Max(0f, playerCooldowns[playerID][locationKey] - Time.time);
        }

        private void StartCooldown(ulong playerID, string locationKey, float duration, BasePlayer player = null)
        {
            if (!configuration.Options.EnableCooldown) return;

            if (!playerCooldowns.ContainsKey(playerID))
                playerCooldowns[playerID] = new Dictionary<string, float>();

            float exactExpiryTime = Time.time + duration;
            playerCooldowns[playerID][locationKey] = exactExpiryTime;

            if (player != null)
            {
                timer.Once(duration, () =>
                {
                    if (player != null && player.IsConnected)
                    {
                        if (playerCooldowns.ContainsKey(playerID) &&
                            playerCooldowns[playerID].ContainsKey(locationKey) &&
                            Math.Abs(playerCooldowns[playerID][locationKey] - exactExpiryTime) < 0.001f)
                        {
                            playerCooldowns[playerID].Remove(locationKey);
                            if (player.IsDead())
                            {
                                RefreshSingleButtonUI(player, locationKey);
                            }
                        }
                    }
                });
            }
        }

        #endregion

        #region Unlock Timer

        private bool IsUILockActive(ulong playerID, string locationKey)
        {
            if (!playerUIUnlocks.ContainsKey(playerID)) return false;
            if (!playerUIUnlocks[playerID].ContainsKey(locationKey)) return false;

            float unlockTime = playerUIUnlocks[playerID][locationKey];
            return Time.time < unlockTime;
        }

        private float GetUILockRemainingTime(ulong playerID, string locationKey)
        {
            if (!playerUIUnlocks.ContainsKey(playerID)) return 0f;
            if (!playerUIUnlocks[playerID].ContainsKey(locationKey)) return 0f;

            float unlockTime = playerUIUnlocks[playerID][locationKey];
            return Math.Max(0f, unlockTime - Time.time);
        }

        private void StartUILock(ulong playerID, string locationKey, float duration, BasePlayer player = null)
        {
            if (duration <= 0f) return;

            if (!playerUIUnlocks.ContainsKey(playerID))
                playerUIUnlocks[playerID] = new Dictionary<string, float>();

            float exactUnlockTime = Time.time + duration;
            playerUIUnlocks[playerID][locationKey] = exactUnlockTime;

            if (player != null)
            {
                timer.Once(duration, () =>
                {
                    if (player != null && player.IsConnected && player.IsDead())
                    {
                        if (playerUIUnlocks.ContainsKey(playerID) &&
                            playerUIUnlocks[playerID].ContainsKey(locationKey) &&
                            Math.Abs(playerUIUnlocks[playerID][locationKey] - exactUnlockTime) < 0.001f)
                        {
                            playerUIUnlocks[playerID].Remove(locationKey);
                            ShowMonumentUI(player);
                        }
                    }
                });
            }
        }

        private void StartUILocksForPlayer(BasePlayer player)
        {
            if (player == null) return;

            bool canOutpost = configuration.Options.EnableOutpost && outpostMonument != null;
            bool canBandit  = configuration.Options.EnableBandit && banditCampMonument != null;

            if (canOutpost)
            {
                bool hasOutpostPerm = (!configuration.Options.EnablePermissions || !configuration.Outpost.UsePermission)
                                      || permission.UserHasPermission(player.UserIDString, "outpostrespawn.outpost");
                if (hasOutpostPerm)
                {
                    StartUILock(player.userID, "Outpost", configuration.Outpost.UnlockTimer, player);
                }
            }

            if (canBandit)
            {
                bool hasBanditPerm = (!configuration.Options.EnablePermissions || !configuration.BanditCamp.UsePermission)
                                     || permission.UserHasPermission(player.UserIDString, "outpostrespawn.bandit");
                if (hasBanditPerm)
                {
                    StartUILock(player.userID, "BanditCamp", configuration.BanditCamp.UnlockTimer, player);
                }
            }
        }

        #endregion

        #region Helper Methods

        private object CanNetworkTo(BaseNetworkable entity, BasePlayer target)
        {
            if (entity is SleepingBag bag && invisibleBags.Values.Contains(bag))
            {
                return false;
            }
            return null;
        }

        private MonumentInfo FindMonumentByName(string searchTerm)
        {
            foreach (var monument in TerrainMeta.Path.Monuments)
            {
                string monName = monument?.displayPhrase?.english;
                if (string.IsNullOrEmpty(monName))
                    monName = monument.name;
                if (monName == null) continue;
                if (monName.ToLower().Contains(searchTerm.ToLower()))
                    return monument;
            }
            return null;
        }

        private Vector3 GetGroundPosition(Vector3 originalPos)
        {
            var startPos = originalPos + Vector3.up * 500f;
            RaycastHit hit;
            var layerMask = LayerMask.GetMask("Terrain", "World", "Deployed");
            if (Physics.Raycast(startPos, Vector3.down, out hit, 1000f, layerMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }
            return originalPos;
        }

        private void TeleportToRandomLocation(BasePlayer player, string locationKey)
        {
            var data = GetMonumentSetup(locationKey);
            if (data == null) return;
            MonumentInfo monumentRef = data.Item1;
            List<Vector3> spawnList = data.Item2;
            List<Vector3> rotList = data.Item3;
            if (monumentRef == null || spawnList.Count == 0)
                return;

            int idx = UnityEngine.Random.Range(0, spawnList.Count);
            Vector3 localPos = spawnList[idx];
            Quaternion localRot = Quaternion.Euler(rotList[idx]);
            Vector3 globalPos = monumentRef.transform.TransformPoint(localPos);
            globalPos = GetGroundPosition(globalPos);
            Quaternion globalRot = monumentRef.transform.rotation * localRot;

            player.RespawnAt(globalPos, globalRot);
        }

        #endregion

        #region Cleanup

        private void Unload()
        {
            foreach (var p in BasePlayer.activePlayerList)
            {
                if (p.IsConnected)
                {
                    RemoveRespawnButtonUI(p);
                }
            }

            playerCooldowns.Clear();
            playerUIState.Clear();
            playerUIUnlocks.Clear();

            foreach (var kvp in invisibleBags.ToList())
            {
                var bag = kvp.Value;
                if (bag != null && !bag.IsDestroyed)
                {
                    bag.Kill();
                }
            }
            invisibleBags.Clear();

            foreach (var bag in bagLocations.Keys.ToList())
            {
                if (bag != null && !bag.IsDestroyed)
                {
                    bag.Kill();
                }
            }
            bagLocations.Clear();
        }

        #endregion
    }
}