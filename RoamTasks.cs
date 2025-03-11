using Newtonsoft.Json;
using Oxide.Core;
using Rust;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using Network;
using System.Runtime.Serialization;

namespace Oxide.Plugins
{
    [Info("RoamTasks", "Ridamees", "1.1.5")]
    [Description("RoamTasks - random periodic tasks for rewards")]
    public class RoamTasks : RustPlugin
    {
        [PluginReference]
		private Plugin Economics;
		[PluginReference]
		private Plugin ServerRewards;

        private float uiBarFadeInDuration = 0.2f;
        private float uiBarFadeOutDuration = 0.2f;
        private float GameTipDuration = 4.3f;
        
        private class GeneralConfig
        {
            [JsonProperty("Log Task Completion")]
            public bool LogTasks { get; set; } = false;
            [JsonProperty("Automatically Add Latest Tasks")]
            public bool AddLatestTasks { get; set; } = true;
            [JsonProperty("Only Command Start Tasks")]
            public bool onlyCommandStartTask { get; set; } = false;
            [JsonProperty("Next Task Minimum(seconds)")]
            public int NextRoamMin { get; set; } = 600;
            [JsonProperty("Next Task Maximum(seconds)")]
            public int NextRoamMax { get; set; } = 1800;
            [JsonProperty("Start Sound")]
            public bool StartSound { get; set; } = true;
            [JsonProperty("Start Sound Vibration")]
            public bool StartSoundVibration { get; set; } = true;
            [JsonProperty("GameTip Notifications")]
            public bool GameTipEnabled { get; set; } = true;
            [JsonProperty("GameTip Reward Message")]
            public string gameTipMessage { get; set; } = "<color=#8cc83c>You received: {rewardDisplayName}</color>";
            [JsonProperty("GameTip Start Message")]
            public string gameTipStartMessage { get; set; } = "Roam Available";
            [JsonProperty("Chat Notifications")]
            public bool ChatEnabled { get; set; } = true;
            [JsonProperty("Chat Announce Task Complete")]
            public bool ChatTaskCompleteAnnounce { get; set; } = true;
            [JsonProperty("Chat Announce Task Complete - Message ")]
            public string ChatTaskCompleteAnnounceMessage { get; set; } = "{config.General.scientistName}Participant <color=#55aaff>{playerName}</color> has completed the roam.{RankReward}";
            [JsonProperty("Chat Announce Task Complete - Max Players")]
            public int ChatTaskCompleteAnnounceMaxPlayers { get; set; } = 3;
            [JsonProperty("Chat Player Limit Messages")]
            public string[] MaxCompleteMessage { get; set; } = new string[]
            {
                "\nLimited to {selectedMaxComplete} participants!",
                "\nMaximum {selectedMaxComplete} participants allowed!",
                "\nOnly {selectedMaxComplete} participants allowed!",
                "\nMaximum {selectedMaxComplete} participants!"
            };
            [JsonProperty("Chat Icon | 0 = Disabled")]
            public ulong ChatIcon { get; set; } = 76561199350559937;
            [JsonProperty("Chat Name")]
            public string scientistName { get; set; } = "<color=#aaff55>Scientist</color>: ";
            [JsonProperty("Chat Reward Message")]
            public string chatMessage { get; set; } = "{scientistName}You received {rewardDisplayName}.";
            [JsonProperty("Chat Next Roam Notification")]
            public bool NextRoamMessageEnabled { get; set; } = true;
            [JsonProperty("Chat Next Roam Message")]
            public string NextRoamMessage { get; set; } = "{scientistName}Next roam in {NextRoamTime}.";
            [JsonProperty("{RankReward} Text ")]
            public string RankRewardMessageAddon { get; set; } = "\n+ {playerRank}{playerRankSuffix} participant bonus.";
            [JsonProperty("Chat Start Messages")]
            public string[] scientistMessages { get; set; } = new string[]
            {
                "Attention. Roam available.",
                "Alert. Roam initiated.",
                "Attention. Roam authorized.",
                "Warning. Roam initiated. Proceed with caution.",
                "Attention. Roam accessible.",
                "Alert. Roam authorized."
            };
            [JsonProperty("UI Location X")]
            public float UIx { get; set; } = 0.5f;
            [JsonProperty("UI Location Y")]
            public float UIy { get; set; } = 0f; 
            [JsonProperty("Position Bonus Rewards")]
            public bool PositionBonusRewards { get; set; } = true;
        }

        private PluginConfig config;
        private class PluginConfig
        {
            [JsonProperty("GeneralConfig")]
            public GeneralConfig General { get; set; } = new GeneralConfig();
            [JsonProperty("Position Bonus Rewards List")]
            public Dictionary<int, RewardConfig> RankBonusRewards { get; set; } = new Dictionary<int, RewardConfig>
            {
                { 1, new RewardConfig
                    {
                        RewardEnabled = true,
                        RewardType = new List<int> { 1 },
                        RewardItems = new List<RewardItem>
                        {
                            new RewardItem { ItemType = "xmas.present.large", ItemAmount = 1 }
                        }
                    }
                },
                { 2, new RewardConfig
                    {
                        RewardEnabled = true,
                        RewardType = new List<int> { 1 },
                        RewardItems = new List<RewardItem>
                        {
                            new RewardItem { ItemType = "xmas.present.medium", ItemAmount = 1 }
                        }
                    }
                },
                { 3, new RewardConfig
                    {
                        RewardEnabled = true,
                        RewardType = new List<int> { 1 },
                        RewardItems = new List<RewardItem>
                        {
                            new RewardItem { ItemType = "xmas.present.small", ItemAmount = 3 }
                        }
                    }
                }
            };
            [JsonProperty("RewardPool Configuration - Tiers and Rewards: Each Tier has a List of Rewards. Each Reward has multiple Types. One reward from set Tier's Rewards List is randomly selected based on configured Chance. (Add as many Tiers/Rewards as needed)")]
            public Dictionary<string, RewardPoolConfig> RewardPools { get; set; } = new Dictionary<string, RewardPoolConfig>
            {
                {
                    "Tier1", new RewardPoolConfig
                    {
                        RewardConfigs = new List<RewardConfigOption>
                        {
                            new RewardConfigOption { RewardConfig = new RewardConfig { RewardEnabled = true, RewardType = new List<int> { 1 }, RewardItems = new List<RewardItem> { new RewardItem { ItemType = "pistol.semiauto", ItemAmount = 1, ItemSkinID = 830255284 }, new RewardItem { ItemType = "ammo.pistol", ItemAmount = 32 }, new RewardItem { ItemType = "bandage", ItemAmount = 3 } } },  Chance = 0.5f },
                            new RewardConfigOption { RewardConfig = new RewardConfig { RewardEnabled = true, RewardType = new List<int> { 1 }, RewardItems = new List<RewardItem> { new RewardItem { ItemType = "pistol.revolver", ItemAmount = 1 }, new RewardItem { ItemType = "ammo.pistol", ItemAmount = 64 }, new RewardItem { ItemType = "cloth", ItemAmount = 50 } } }, Chance = 0.35f },
                            new RewardConfigOption { RewardConfig = new RewardConfig { RewardEnabled = true, RewardType = new List<int> { 1 }, RewardItems = new List<RewardItem> { new RewardItem { ItemType = "explosive.satchel", ItemAmount = 2 }, new RewardItem { ItemType = "grenade.f1", ItemAmount = 2 }, new RewardItem { ItemType = "metal.fragments", ItemAmount = 100 } } }, Chance = 0.15f }
                        }
                    }
                },
                {
                    "Tier2", new RewardPoolConfig
                    {
                        RewardConfigs = new List<RewardConfigOption>
                        {
                            new RewardConfigOption { RewardConfig = new RewardConfig { RewardEnabled = true, RewardType = new List<int> { 1 }, RewardItems = new List<RewardItem> { new RewardItem { ItemType = "rifle.semiauto", ItemAmount = 1 }, new RewardItem { ItemType = "ammo.rifle", ItemAmount = 64 }, new RewardItem { ItemType = "syringe.medical", ItemAmount = 5 } } }, Chance = 0.5f },
                            new RewardConfigOption { RewardConfig = new RewardConfig { RewardEnabled = true, RewardType = new List<int> { 1 }, RewardItems = new List<RewardItem> { new RewardItem { ItemType = "smg.2", ItemAmount = 1 }, new RewardItem { ItemType = "ammo.pistol", ItemAmount = 64 }, new RewardItem { ItemType = "lowgradefuel", ItemAmount = 30 } } }, Chance = 0.35f },
                            new RewardConfigOption { RewardConfig = new RewardConfig { RewardEnabled = true, RewardType = new List<int> { 1 }, RewardItems = new List<RewardItem> { new RewardItem { ItemType = "ammo.rifle.explosive", ItemAmount = 64 }, new RewardItem { ItemType = "hq.metal.ore", ItemAmount = 50 } } }, Chance = 0.15f }
                        }
                    }
                },
                {
                    "Tier3", new RewardPoolConfig
                    {
                        RewardConfigs = new List<RewardConfigOption>
                        {
                            new RewardConfigOption { RewardConfig = new RewardConfig { RewardEnabled = true, RewardType = new List<int> { 1 }, RewardItems = new List<RewardItem> { new RewardItem { ItemType = "rifle.lr300", ItemAmount = 1 }, new RewardItem { ItemType = "ammo.rifle", ItemAmount = 128 }, new RewardItem { ItemType = "largemedkit", ItemAmount = 3 } } }, Chance = 0.5f },
                            new RewardConfigOption { RewardConfig = new RewardConfig { RewardEnabled = true, RewardType = new List<int> { 1 }, RewardItems = new List<RewardItem> { new RewardItem { ItemType = "rifle.bolt", ItemAmount = 1 }, new RewardItem { ItemType = "ammo.rifle.hv", ItemAmount = 64 }, new RewardItem { ItemType = "hq.metal.ore", ItemAmount = 100 } } }, Chance = 0.35f },
                            new RewardConfigOption { RewardConfig = new RewardConfig { RewardEnabled = true, RewardType = new List<int> { 1 }, RewardItems = new List<RewardItem> { new RewardItem { ItemType = "ammo.rifle.explosive", ItemAmount = 128 }, new RewardItem { ItemType = "explosive.timed", ItemAmount = 1 } } }, Chance = 0.15f }
                        }
                    }
                }
            };
            [JsonProperty("Tasks Configuration: Tasks are categorized by Hooks. First Task = TaskID 1, increments per task. MaxComplete 0 = no players complete limit | (Tip) - useful console commands 'RoamTask List' & 'RoamTask Start (Hook) (TaskID)' ")]
            public Dictionary<string, List<TaskConfig>> HookTasks { get; set; } = new Dictionary<string, List<TaskConfig>>
            {
                {
                    "OnEntityDeath", new List<TaskConfig>
                    {
                        new TaskConfig { Name = "Destroy Barrels", Enabled = true, MaxComplete = 12, RoamDuration = 600, RequiredAmount = 5, RewardPool = "Tier1", ShortName = new List<string> { "loot-barrel-1", "loot-barrel-2", "loot_barrel_1", "loot_barrel_2", "oil_barrel" } },
                        new TaskConfig { Name = "Destroy Barrels", Enabled = true, MaxComplete = 6, RoamDuration = 800, RequiredAmount = 15, RewardPool = "Tier2", ShortName = new List<string> { "loot-barrel-1", "loot-barrel-2", "loot_barrel_1", "loot_barrel_2", "oil_barrel" } },
                        new TaskConfig { Name = "Destroy Barrels", Enabled = true, MaxComplete = 3, RoamDuration = 1200, RequiredAmount = 30, RewardPool = "Tier3", ShortName = new List<string> { "loot-barrel-1", "loot-barrel-2", "loot_barrel_1", "loot_barrel_2", "oil_barrel" } },
                        new TaskConfig { Name = "Destroy Roadsigns", Enabled = true, MaxComplete = 12, RoamDuration = 600, RequiredAmount = 4, RewardPool = "Tier1", ShortName = new List<string> { "roadsign1", "roadsign2", "roadsign3", "roadsign4", "roadsign5", "roadsign6", "roadsign7", "roadsign8", "roadsign9" } },
                        new TaskConfig { Name = "Destroy Roadsigns", Enabled = true, MaxComplete = 6, RoamDuration = 800, RequiredAmount = 9, RewardPool = "Tier2", ShortName = new List<string> { "roadsign1", "roadsign2", "roadsign3", "roadsign4", "roadsign5", "roadsign6", "roadsign7", "roadsign8", "roadsign9" } },
                        new TaskConfig { Name = "Hunt for Boars", Enabled = true, MaxComplete = 5, RoamDuration = 600, RequiredAmount = 3, RewardPool = "Tier1", ShortName = new List<string> { "boar" } },
                        new TaskConfig { Name = "Hunt for Bears", Enabled = true, MaxComplete = 5, RoamDuration = 600, RequiredAmount = 3, RewardPool = "Tier1", ShortName = new List<string> { "bear", "polarbear" } },
                        new TaskConfig { Name = "Hunt for Deer", Enabled = true, MaxComplete = 5, RoamDuration = 600, RequiredAmount = 3, RewardPool = "Tier1", ShortName = new List<string> { "stag" } },
                        new TaskConfig { Name = "Hunt for Wolves", Enabled = true, MaxComplete = 5, RoamDuration = 600, RequiredAmount = 3, RewardPool = "Tier1", ShortName = new List<string> { "wolf" } },
                        new TaskConfig { Name = "Kill Scientists", Enabled = true, MaxComplete = 3, RoamDuration = 800, RequiredAmount = 3, RewardPool = "Tier2", ShortName = new List<string> { "scientistnpc_cargo", "scientistnpc_cargo_turret_any", "scientistnpc_cargo_turret_lr300", "scientistnpc_excavator", "scientistnpc_heavy", "scientistnpc_junkpile_pistol", "scientistnpc_oilrig", "scientistnpc_patrol", "scientistnpc_roam", "scientistnpc_roam_nvg_variant", "scientistnpc_full_any", "scientistnpc_full_lr300", "scientistnpc_full_mp5", "scientistnpc_full_pistol", "scientistnpc_full_shotgun", "scientistnpc_arena" } }
                    }
                },
                {
                    "OnEntityTakeDamage", new List<TaskConfig>
                    {
                        new TaskConfig { Name = "Damage Patrol Heli", Enabled = false, RoamDuration = 1200, RequiredAmount = 1000, RewardPool = "Tier3", ShortName = new List<string> { "patrolhelicopter" } },
                        new TaskConfig { Name = "Damage Bradley APC", Enabled = false, RoamDuration = 1200, RequiredAmount = 1000, RewardPool = "Tier3", ShortName = new List<string> { "bradleyapc" } },
                        new TaskConfig { Name = "Damage Tugboats", Enabled = false, RoamDuration = 1200, RequiredAmount = 300, RewardPool = "Tier3", ShortName = new List<string> { "tugboat" } }
                    }
                },
                {
                    "OnDispenserGather", new List<TaskConfig>
                    {
                        new TaskConfig { Name = "Gather Wood", Enabled = true, MaxComplete = 12, RoamDuration = 600, RequiredAmount = 1000, RewardPool = "Tier1", ShortName = new List<string> { "wood" } },
                        new TaskConfig { Name = "Gather Stone", Enabled = true, MaxComplete = 12, RoamDuration = 600, RequiredAmount = 1000, RewardPool = "Tier1", ShortName = new List<string> { "stones" } },
                        new TaskConfig { Name = "Gather Sulfur Ore", Enabled = true, MaxComplete = 12, RoamDuration = 600, RequiredAmount = 1000, RewardPool = "Tier1", ShortName = new List<string> { "sulfur.ore" } },
                        new TaskConfig { Name = "Gather Metal Ore", Enabled = true, MaxComplete = 12, RoamDuration = 600, RequiredAmount = 1000, RewardPool = "Tier1", ShortName = new List<string> { "metal.ore" } },
                        new TaskConfig { Name = "Gather Wood", Enabled = true, MaxComplete = 6, RoamDuration = 800, RequiredAmount = 4500, RewardPool = "Tier2", ShortName = new List<string> { "wood" } },
                        new TaskConfig { Name = "Gather Stone", Enabled = true, MaxComplete = 6, RoamDuration = 800, RequiredAmount = 4500, RewardPool = "Tier2", ShortName = new List<string> { "stones" } },
                        new TaskConfig { Name = "Gather Sulfur Ore", Enabled = true, MaxComplete = 6, RoamDuration = 800, RequiredAmount = 4000, RewardPool = "Tier2", ShortName = new List<string> { "sulfur.ore" } },
                        new TaskConfig { Name = "Gather Metal Ore", Enabled = true, MaxComplete = 6, RoamDuration = 800, RequiredAmount = 4000, RewardPool = "Tier2", ShortName = new List<string> { "metal.ore" } },
                        new TaskConfig { Name = "Gather Wood", Enabled = true, MaxComplete = 3, RoamDuration = 1200, RequiredAmount = 10000, RewardPool = "Tier3", ShortName = new List<string> { "wood" } },
                        new TaskConfig { Name = "Gather Stone", Enabled = true, MaxComplete = 3, RoamDuration = 1200, RequiredAmount = 10000, RewardPool = "Tier3", ShortName = new List<string> { "stones" } },
                        new TaskConfig { Name = "Gather Sulfur Ore", Enabled = true, MaxComplete = 3, RoamDuration = 1200, RequiredAmount = 10000, RewardPool = "Tier3", ShortName = new List<string> { "sulfur.ore" } },
                        new TaskConfig { Name = "Gather Metal Ore", Enabled = true, MaxComplete = 3, RoamDuration = 1200, RequiredAmount = 10000, RewardPool = "Tier3", ShortName = new List<string> { "metal.ore" } }
                    }
                },
                {
                    "OnCollectiblePickup", new List<TaskConfig>
                    {
                        new TaskConfig { Name = "Pickup Wood", Enabled = true, MaxComplete = 5, RoamDuration = 600, RequiredAmount = 400, RewardPool = "Tier1", ShortName = new List<string> { "wood" } },
                        new TaskConfig { Name = "Pickup Stone", Enabled = true, MaxComplete = 5, RoamDuration = 600, RequiredAmount = 400, RewardPool = "Tier1", ShortName = new List<string> { "stones" } },
                        new TaskConfig { Name = "Pickup Metal Ore", Enabled = true, MaxComplete = 5, RoamDuration = 600, RequiredAmount = 400, RewardPool = "Tier1", ShortName = new List<string> { "metal.ore" } },
                        new TaskConfig { Name = "Pickup Sulfur Ore", Enabled = true, MaxComplete = 5, RoamDuration = 600, RequiredAmount = 400, RewardPool = "Tier1", ShortName = new List<string> { "sulfur.ore" } },
                        new TaskConfig { Name = "Pickup Hemp", Enabled = true, MaxComplete = 5, RoamDuration = 600, RequiredAmount = 50, RewardPool = "Tier1", ShortName = new List<string> { "cloth" } },
                        new TaskConfig { Name = "Pickup Mushrooms", Enabled = true, MaxComplete = 5, RoamDuration = 600, RequiredAmount = 5, RewardPool = "Tier1", ShortName = new List<string> { "mushroom" } },
                        new TaskConfig { Name = "Pickup Berries", Enabled = true, MaxComplete = 5, RoamDuration = 600, RequiredAmount = 3, RewardPool = "Tier1", ShortName = new List<string> { "black.berry", "blue.berry", "green.berry", "red.berry", "white.berry", "yellow.berry" } }
                    }
                },
                {
                    "OnLootEntity", new List<TaskConfig>
                    {
                        new TaskConfig { Name = "Open Normal Crates", Enabled = true, MaxComplete = 8, RoamDuration = 600, RequiredAmount = 5, RewardPool = "Tier1", ShortName = new List<string> { "crate_normal_2", "crate_normal_2_food", "crate_normal_2_medical", "crate_normal_2", "crate_tools" } },
                        new TaskConfig { Name = "Open Foodboxes", Enabled = true, MaxComplete = 6, RoamDuration = 600, RequiredAmount = 3, RewardPool = "Tier1", ShortName = new List<string> { "foodbox" } },
                        new TaskConfig { Name = "Open Military Crates", Enabled = true, MaxComplete = 5, RoamDuration = 800, RequiredAmount = 2, RewardPool = "Tier2", ShortName = new List<string> { "crate_normal" } },
                        new TaskConfig { Name = "Open Military Crates", Enabled = true, MaxComplete = 3, RoamDuration = 1200, RequiredAmount = 4, RewardPool = "Tier3", ShortName = new List<string> { "crate_normal" } },
                        new TaskConfig { Name = "Open Elite Crates", Enabled = true, RoamDuration = 1200, RequiredAmount = 2, RewardPool = "Tier3", ShortName = new List<string> { "crate_elite" } },
                        new TaskConfig { Name = "Open Supply Drops", Enabled = false, RoamDuration = 1200, RequiredAmount = 1, RewardPool = "Tier3", ShortName = new List<string> { "supply_drop" } },
                        new TaskConfig { Name = "Open Locked Crates", Enabled = false, RoamDuration = 1200, RequiredAmount = 1, RewardPool = "Tier3", ShortName = new List<string> { "codelockedhackablecrate" } }
                    }
                },
                {
                    "OnFishCatch", new List<TaskConfig>
                    {
                        new TaskConfig { Name = "Catch Fish", Enabled = true, RoamDuration = 800, RequiredAmount = 5, RewardPool = "Tier2", ShortName = new List<string> { "fish.anchovy", "fish.catfish", "fish.herring", "fish.minnows", "fish.orangeroughy", "fish.salmon", "fish.sardine", "fish.smallshark", "fish.troutsmall", "fish.yellowperch" } }
                    }
                },
                {
                    "OnItemCraftFinished", new List<TaskConfig>
                    {
                        new TaskConfig { Name = "Craft Eokas", Enabled = false, RoamDuration = 600, RequiredAmount = 5, RewardPool = "Tier1", ShortName = new List<string> { "pistol.eoka" } }
                    }
                },
                {
                    "OnCardSwipe", new List<TaskConfig>
                    {
                        new TaskConfig { Name = "Swipe Green Keycards", Enabled = true, RoamDuration = 600, RequiredAmount = 1, RewardPool = "Tier1", ShortName = new List<string> { "keycard_green" } },
                        new TaskConfig { Name = "Swipe Green Keycards", Enabled = true, MaxComplete = 6, RoamDuration = 800, RequiredAmount = 3, RewardPool = "Tier2", ShortName = new List<string> { "keycard_green" } },
                        new TaskConfig { Name = "Swipe Blue Keycards", Enabled = true, MaxComplete = 6, RoamDuration = 800, RequiredAmount = 1, RewardPool = "Tier2", ShortName = new List<string> { "keycard_blue" } },
                        new TaskConfig { Name = "Swipe Blue Keycards", Enabled = true, MaxComplete = 4, RoamDuration = 1200, RequiredAmount = 3, RewardPool = "Tier3", ShortName = new List<string> { "keycard_blue" } },
                        new TaskConfig { Name = "Swipe Red Keycards", Enabled = true, MaxComplete = 4, RoamDuration = 1200, RequiredAmount = 1, RewardPool = "Tier3", ShortName = new List<string> { "keycard_red" } }
                    }
                },
                {
                    "OnEntityMounted", new List<TaskConfig>
                    {
                        new TaskConfig { Name = "Drive Modular Cars", Enabled = true, MaxComplete = 6, RoamDuration = 800, RequiredAmount = 3000, RewardPool = "Tier2", ShortName = new List<string> { "modularcardriverseat" } },
                        new TaskConfig { Name = "Fly Minicopters", Enabled = true, MaxComplete = 6, RoamDuration = 800, RequiredAmount = 3000, RewardPool = "Tier2", ShortName = new List<string> { "miniheliseat" } },
                        new TaskConfig { Name = "Fly Scrapheli", Enabled = true, MaxComplete = 4, RoamDuration = 1200, RequiredAmount = 5000, RewardPool = "Tier3", ShortName = new List<string> { "transporthelipilot" } },
                        new TaskConfig { Name = "Drive Submarines", Enabled = true, MaxComplete = 4, RoamDuration = 1200, RequiredAmount = 3200, RewardPool = "Tier3", ShortName = new List<string> { "submarinesolodriverstanding", "submarineduodriverseat" } },
                        new TaskConfig { Name = "Sail Tugboats", Enabled = true, MaxComplete = 4, RoamDuration = 1200, RequiredAmount = 3200, RewardPool = "Tier3", ShortName = new List<string> { "tugboatdriver" } },
                        new TaskConfig { Name = "Sail Normal Boats", Enabled = true, MaxComplete = 6, RoamDuration = 800, RequiredAmount = 2600, RewardPool = "Tier2", ShortName = new List<string> { "smallboatdriver", "rhibdriver" } },
                        new TaskConfig { Name = "Sail Kayaks", Enabled = true, MaxComplete = 12, RoamDuration = 600, RequiredAmount = 800, RewardPool = "Tier1", ShortName = new List<string> { "kayakseat" } },
                        new TaskConfig { Name = "Drive Trains", Enabled = true, MaxComplete = 6, RoamDuration = 800, RequiredAmount = 3200, RewardPool = "Tier2", ShortName = new List<string> { "workcartdriver" } },
                        new TaskConfig { Name = "Ride Horses", Enabled = true, RoamDuration = 600, RequiredAmount = 1200, RewardPool = "Tier1", ShortName = new List<string> { "saddletest" } },
                        new TaskConfig { Name = "Use Ziplines", Enabled = true, RoamDuration = 600, RequiredAmount = 600, RewardPool = "Tier1", ShortName = new List<string> { "ziplinemountable" } },
                        new TaskConfig { Name = "Fly Attackheli", Enabled = true, MaxComplete = 6, RoamDuration = 800, RequiredAmount = 5000, RewardPool = "Tier3", ShortName = new List<string> { "attackhelidriver" } },
                        new TaskConfig { Name = "Use Parachutes", Enabled = true, MaxComplete = 6, RoamDuration = 1200, RequiredAmount = 1500, RewardPool = "Tier3", ShortName = new List<string> { "parachuteseat" } },
                        new TaskConfig { Name = "Ride Pedal Bikes", Enabled = true, RoamDuration = 600, RequiredAmount = 1200, RewardPool = "Tier1", ShortName = new List<string> { "bikedriverseat" } },
                        new TaskConfig { Name = "Ride Motor Bikes", Enabled = true, RoamDuration = 800, RequiredAmount = 3000, RewardPool = "Tier2", ShortName = new List<string> { "motorbikedriverseat" } }
                    }
                },
                {
                    "OnBigWheelWin", new List<TaskConfig>
                    {
                        new TaskConfig { Name = "Win Gambling Wheel", Enabled = true, RoamDuration = 600, RequiredAmount = 50, RewardPool = "Tier1", ShortName = new List<string> { "scrap" } },
                        new TaskConfig { Name = "Win Gambling Wheel", Enabled = true, RoamDuration = 800, RequiredAmount = 150, RewardPool = "Tier2", ShortName = new List<string> { "scrap" } },
                        new TaskConfig { Name = "Win Gambling Wheel", Enabled = true, RoamDuration = 1200, RequiredAmount = 250, RewardPool = "Tier3", ShortName = new List<string> { "scrap" } }
                    }
                }
            };
        }
        
        private void AddLatestTasks()
        {
            
            if (!TaskExists("bikedriverseat"))
            {
                var taskToAdd = new TaskConfig
                {
                    Name = "Ride Pedal Bikes", Enabled = true, RoamDuration = 600, RequiredAmount = 1200, RewardPool = "Tier1", ShortName = new List<string> { "bikedriverseat" }
                };
                config.HookTasks["OnEntityMounted"].Add(taskToAdd);
                Puts($"Added new Task - {taskToAdd.Name}");
            }
            
            if (!TaskExists("motorbikedriverseat"))
            {
                var taskToAdd = new TaskConfig
                {
                    Name = "Ride Motor Bikes", Enabled = true, RoamDuration = 800, RequiredAmount = 3000, RewardPool = "Tier2", ShortName = new List<string> { "motorbikedriverseat" }
                };
                config.HookTasks["OnEntityMounted"].Add(taskToAdd);
                Puts($"Added new Task - {taskToAdd.Name}");
            }
        }

        private bool TaskExists(string shortName)
        {
            
            return config.HookTasks.ContainsKey("OnEntityMounted") && config.HookTasks["OnEntityMounted"].Any(task => task.ShortName.Contains(shortName));
        }

        private class RewardConfig
        {
            [JsonProperty("Enabled")]
            public bool RewardEnabled { get; set; } = false;
            [JsonProperty("Types - ( Items = 1, Command = 2, Eco = 3, SR = 4 ) - Usage - 1, 2, 3, 4")]
            public List<int> RewardType { get; set; } = new List<int>();
            [JsonProperty("Items")]
            public List<RewardItem> RewardItems { get; set; } = new List<RewardItem>();
            [JsonProperty("Command")]
            public string RewardCommand { get; set; } = "oxide.usergroup add {player.id} vip";
            [JsonProperty("Command Custom Name")]
            public string CommandRewardCustomName { get; set; } = "VIP";
            [JsonProperty("Command Reward Msg")]
            public string CommandRewardFormat { get; set; } = "<color=#ffbf00>{customName}</color>"; 
            [JsonProperty("Economics Amount (Plugin)")]
            public double EconomicsRewardAmount { get; set; } = 420.0;
            [JsonProperty("Economics Custom Name (Plugin)")]
            public string EconomicsRewardCustomName { get; set; } = "$";
            [JsonProperty("Economics Reward Msg")]
            public string EconomicsRewardFormat { get; set; } = "<color=#3e9c35>{customName}</color>{amount}"; 
            [JsonProperty("ServerRewards Amount (Plugin)")]
            public int ServerRewardsAmount { get; set; } = 420;
            [JsonProperty("ServerRewards Custom Name (Plugin)")]
            public string ServerRewardsRewardCustomName { get; set; } = "RP";
            [JsonProperty("ServerRewards Reward Msg")]
            public string ServerRewardsRewardFormat { get; set; } = "{amount}<color=#cd4632>{customName}</color>";

        }

        private class RewardItem
        {
            [JsonProperty("Item Type")]
            public string ItemType { get; set; } = "scrap";
            [JsonProperty("Item Amount")]
            public int ItemAmount { get; set; } = 42;
            [JsonProperty("Item Custom Name")]
            public string ItemCustomName { get; set; } = "";
            [JsonProperty("Item Skin ID")]
            public ulong ItemSkinID { get; set; } = 0;
        }

        private class RewardPoolConfig
        {
            [JsonProperty(" -- Rewards List -- ")]
            public List<RewardConfigOption> RewardConfigs { get; set; } = new List<RewardConfigOption>();
        }

        private class RewardConfigOption
        {
            [JsonProperty(" -- Reward")]
            public RewardConfig RewardConfig { get; set; }
            [JsonProperty(" -- Reward Select Chance % (0.0 - 1.0)")]
            public float Chance { get; set; }        
        }
    
        private int taskIdCounter = 1;
        TaskConfig selectedTaskConfig = null; 
        private class TaskConfig
        {
            public TaskConfig() { }
            [JsonIgnore]
            public string TaskID { get; set; }
            public string Name { get; set; }
            public bool Enabled { get; set; }
            public int MaxComplete { get; set; }
            public int RoamDuration { get; set; }
            public string RewardPool { get; set; }
            public int RequiredAmount { get; set; }
            public List<string> ShortName { get; set; }
        }
        
        protected override void LoadDefaultConfig()
        {
            config = Config.ReadObject<PluginConfig>() ?? new PluginConfig();
            if (config.RewardPools == null)
            {
                config.RewardPools = new Dictionary<string, RewardPoolConfig>();
            }
            if (config.HookTasks == null)
            {
                config.HookTasks = new Dictionary<string, List<TaskConfig>>();
            }
            if (config.General.AddLatestTasks == true)
            {
                AddLatestTasks();
            }
            Config.WriteObject(config, true);
        }

        private void LoadConfigVariables()
        {
            LoadDefaultConfig();
        }

        private void Init()
        {
            UnsubscribeAll();
            LoadConfigVariables();
            LoadSteamAvatarUserID();
            if (config != null && config.HookTasks != null)
            {
                foreach (var hookTasks in config.HookTasks.Values)
                {
                    foreach (var task in hookTasks)
                    {
                        task.TaskID = (taskIdCounter++).ToString();
                    }
                }
            }
            ScheduleNextRoam();
        }

        private void LoadSteamAvatarUserID()
        {
            object steamAvatarUserIDObj = Config.Get<object>("Steam Avatar User ID", null);
            {
                steamAvatarUserID = config.General.ChatIcon;
            }
        }   

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!player.IsSleeping() && !player.IsDead() && isUiBarVisible && !playersWithCompletedTask.Contains(player))
            {
                CreateUiBar(player); 
            }
        }

        private void OnPlayerSleep(BasePlayer player)
        {
            
            if (player == null || player.IsSleeping())
                return;

            if (isUiBarVisible)
            {
                DestroyUiElements(player);
                DestroyUiElementsTemp(player);
            }
        }

        private void OnPlayerRespawn(BasePlayer player)
        {
            if (!player.IsDead() && isUiBarVisible && !playersWithCompletedTask.Contains(player))
            {
                CreateUiBar(player); 
            }
        }

        private void OnPlayerDeath(BasePlayer player)
        {
            if (isUiBarVisible)
            {
                DestroyUiElements(player);
                DestroyUiElementsTemp(player);
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (isUiBarVisible && !playersWithCompletedTask.Contains(player) && !player.IsSleeping())
            {
                CreateUiBar(player);
                UpdateTimerLabel(player);
                UpdatePointsUI(player);
            }
        }

        private Timer roamTimer;
        private Timer firstMessageTimer;
        private Timer secondMessageTimer;
        private Timer thirdMessageTimer;
        private void ScheduleNextRoam()
        {
            if (!config.General.onlyCommandStartTask)
            {
                int nextRoamInterval = UnityEngine.Random.Range(config.General.NextRoamMin, config.General.NextRoamMax);
                roamTimer = timer.Once(nextRoamInterval, () => StartRoam(null, null)); 
                if (config.General != null && config.General.ChatEnabled == true && config.General.NextRoamMessageEnabled == true)
                {
                    firstMessageTimer?.Destroy();
                    secondMessageTimer?.Destroy();
                    thirdMessageTimer?.Destroy();
                    int firstMessageTime = (int)(nextRoamInterval * 0);
                    int secondMessageTime = (int)(nextRoamInterval * 0.5);
                    int thirdMessageTime = (int)(nextRoamInterval * 0.9);

                    firstMessageTimer = timer.Once(firstMessageTime, () =>
                    {
                        Subscribe("OnSendCommand");
                        SendRoamMessage(nextRoamInterval, firstMessageTime);
                        Unsubscribe("OnSendCommand");
                    });

                    secondMessageTimer = timer.Once(secondMessageTime, () =>
                    {
                        Subscribe("OnSendCommand");
                        SendRoamMessage(nextRoamInterval, secondMessageTime);
                        Unsubscribe("OnSendCommand");
                    });

                    thirdMessageTimer = timer.Once(thirdMessageTime, () =>
                    {
                        Subscribe("OnSendCommand");
                        SendRoamMessage(nextRoamInterval, thirdMessageTime);
                        Unsubscribe("OnSendCommand");
                    });
                }
            }
        }

       private void SendRoamMessage(int nextRoamInterval, int timeOffset)
        {
            if (config.General != null && config.General.ChatEnabled == true && config.General.NextRoamMessageEnabled == true)
            {
                int timeDifference = nextRoamInterval - timeOffset;
                TimeSpan timeSpan = TimeSpan.FromSeconds(timeDifference);
                string formattedTime = "";
                if (timeSpan.TotalHours >= 1)
                {
                    formattedTime = $"{(int)timeSpan.TotalHours} hour{(timeSpan.TotalHours >= 2 ? "s" : "")} ";
                }
                if (timeSpan.TotalMinutes >= 1)
                {
                    formattedTime += $"{(int)timeSpan.TotalMinutes % 60} minute{(timeSpan.TotalMinutes >= 2 ? "s" : "")}";
                }
                if (timeSpan.TotalSeconds >= 1 && timeSpan.TotalMinutes < 1)
                {
                    formattedTime += $"{(int)timeSpan.TotalSeconds} second{(timeSpan.TotalSeconds >= 2 ? "s" : "")}";
                }
                string NextRoamMessage = config.General.NextRoamMessage
                    .Replace("{NextRoamTime}", formattedTime) 
                    .Replace("{scientistName}", config.General.scientistName);
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    player.ChatMessage(NextRoamMessage);
                }
            }
        }

        [ChatCommand("roamtask")]
        private void ChatRoamTask(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin)
            {
                SendReply(player, "To use RoamTask commands, please use the console command: RoamTask");
            }
            else
            {
                SendReply(player, "You don't have permission to use this command.");
            }
        }

        [ChatCommand("roamtasks")]
        private void ChatRoamTasks(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin)
            {
                SendReply(player, "To use RoamTask commands, please use the console command: RoamTask");
            }
            else
            {
                SendReply(player, "You don't have permission to use this command.");
            }
        }

        [ConsoleCommand("RoamTask")]
        private void RoamTaskConsoleCmdShort(ConsoleSystem.Arg arg)
        {
            RoamTaskConsoleCmd(arg);
        }
        [ConsoleCommand("RoamTasks")]
        private void RoamTaskConsoleCmdFull(ConsoleSystem.Arg arg)
        {
            RoamTaskConsoleCmd(arg);
        }

        private void RoamTaskConsoleCmd(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                SendReply(arg, "You don't have permission to use this command.");
                return;
            }
            if (arg.Args == null || arg.Args.Length < 1)
            {
                SendReply(arg, "Commands - Usage: RoamTask <command> [options]\n" +
                            "  Start (Hook) (TaskID) - Start random Task - Optionally, specify the Hook and TaskID.\n" +
                            "  Stop - Stop active RoamTask.\n" +
                            "  List - List all available Tasks with their Hooks and TaskIDs.\n" +
                            "  Stats - Display statistics such as most completed tasks.\n" +
                            "  StatsClear - Clear all recorded RoamTask statistics.\n" +
                            "  Rewards - List all reward tiers and their reward options.");
                return;
            }
            string subCommand = arg.Args[0].ToLower();
            switch (subCommand)
            {
                case "start":
            if (!isUiBarVisible)
            {
                string specifiedHook = null;
                string specifiedTask = null;
                if (arg.Args.Length >= 2)
                {
                    specifiedHook = arg.Args[1]; 
                }
                if (arg.Args.Length >= 3)
                {
                    specifiedTask = arg.Args[2];
                }
                if (string.IsNullOrEmpty(specifiedHook) || config.HookTasks.ContainsKey(specifiedHook))
                {
                    if (roamTimer != null)
                    {
                        roamTimer.Destroy();
                        roamTimer = null;
                    }

                    if (!string.IsNullOrEmpty(specifiedTask) && !IsValidTaskID(specifiedHook, specifiedTask))
                    {
                        SendReply(arg, "Invalid TaskID specified. Task could not be started. #Console command to see TaskIDs - 'RoamTask List'");
                        return;
                    }

                    StartRoam(specifiedHook, specifiedTask);
                    SendReply(arg, "Task Started.");
                }
                else
                {
                    SendReply(arg, "Invalid Hook specified. Task could not be started. #Hooks are case sensitive.");
                }
            }
            else
            {
                SendReply(arg, "Task Already Active.");
            }
            break;
                case "stop":
                    if (isUiBarVisible)
                    {
                        StopRoam();
                        SendReply(arg, "Task Stopped.");
                    }
                    else
                    {
                        SendReply(arg, "Task Not Active.");
                    }
                    break;
                case "list":
                    RoamTaskListConsoleCmd(arg);
                    break;
                case "stats":
                    RoamTaskStatsConsoleCmd(arg);
                    break;
                case "statsclear":
                    RoamTaskStatsClearConsoleCmd(arg);
                    break;
                case "rewards":
                    RoamTaskRewardsConsoleCmd(arg);
                    break;
                default:
                    SendReply(arg, "Invalid Command.\n" +
                            "Commands - Usage: RoamTask <command> [options]\n" +
                            "  Start (Hook) (TaskID) - Start random RoamTask - Optionally, specify the Hook and TaskID.\n" +
                            "  Stop - Stop active RoamTask.\n" +
                            "  List - List all available RoamTasks with their hooks and TaskIDs.\n" +
                            "  Stats - Display statistics such as most completed tasks.\n" +
                            "  StatsClear - Clear all recorded RoamTask statistics.\n" +
                            "  Rewards - List all reward tiers and their reward options.");
                    break;
            }
        }
        private bool IsValidTaskID(string hook, string taskID)
        {
            if (config.HookTasks.TryGetValue(hook, out List<TaskConfig> availableTasks))
            {
                return availableTasks.Any(task => task.TaskID == taskID);
            }
            return false;
        }

        private void RoamTaskRewardsConsoleCmd(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                SendReply(arg, "You don't have permission to use this command.");
                return;
            }
            var sb = new StringBuilder();
            sb.AppendLine("Roam Task Rewards:");
            if (config != null && config.RewardPools != null)
            {
                foreach (var tier in config.RewardPools)
                {
                    sb.AppendLine($"\n Tier: {tier.Key}");
                    foreach (var rewardOption in tier.Value.RewardConfigs)
                    {
                        sb.AppendLine("   - Reward: ");
                        foreach (var rewardItem in rewardOption.RewardConfig.RewardItems)
                        {
                            sb.AppendLine($"       {rewardItem.ItemType}: {rewardItem.ItemAmount}");
                        }
                        sb.AppendLine($"     -- Reward Chance: {rewardOption.Chance}");
                    }
                }
            }
            SendReply(arg, sb.ToString());
        }

        private void RoamStartConsoleCmd(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                SendReply(arg, "You don't have permission to use this command.");
                return;
            }
            if (arg.Args == null || arg.Args.Length < 1)
            {
                if (!isUiBarVisible)
                {
                    StartRoam(); 
                }
                else
                {
                    PrintWarning("Task already active.");
                }
            }
            else
            {
                string specifiedHook = arg.Args[0];
                string specifiedTask = null;
                if (arg.Args.Length >= 2)
                {
                    specifiedTask = arg.Args[1];
                }
                if (!isUiBarVisible)
                {
                    StartRoam(specifiedHook, specifiedTask); 
                }
                else
                {
                    PrintWarning("Task already active.");
                }
            }
        }

        private void RoamTaskListConsoleCmd(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                SendReply(arg, "You don't have permission to use this command.");
                return;
            }
            var sb = new StringBuilder();
            sb.AppendLine("Roam Task List:");
            if (config != null && config.HookTasks != null)
            {
                foreach (var hookTasks in config.HookTasks)
                {
                    sb.AppendLine($"\n- Category(Hook): {hookTasks.Key}");
                    foreach (var task in hookTasks.Value)
                    {
                        sb.AppendLine($"  - TaskID: {task.TaskID}, {task.Name}, Enabled: {task.Enabled}, {(task.MaxComplete > 0 ? $"MaxComplete: {task.MaxComplete}, " : "")}Duration: {task.RoamDuration}, RewardPool: {task.RewardPool}, RequiredAmount: {task.RequiredAmount}");
                    }
                }
            }
            SendReply(arg, sb.ToString());
        }

        private string ReadTaskCompletionLog()
        {
            string logFileName = "RoamTasks_Log.txt";
            string logFilePath = Path.Combine(Interface.Oxide.LogDirectory, logFileName);
            if (File.Exists(logFilePath))
            {
                string logContents = File.ReadAllText(logFilePath);
                if (string.IsNullOrEmpty(logContents))
                {
                    return "Task completion log is empty.";
                }
                else
                {
                    return $"Contents of task completion log ({logFilePath}):\n{logContents}";
                }
            }
            else
            {
                return "Task completion log file not found.";
            }
        }

        private void RoamTaskStatsConsoleCmd(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                SendReply(arg, "You don't have permission to use this command.");
                return;
            }
            string logContents = ReadTaskCompletionLog();
            SendReply(arg, logContents);
        }

        private void ClearTaskCompletionLog()
        {
            string logFileName = "RoamTasks_Log.txt";
            string logFilePath = Path.Combine(Interface.Oxide.LogDirectory, logFileName);
            if (File.Exists(logFilePath))
            {
                File.WriteAllText(logFilePath, string.Empty);
            }
        }

        private void RoamTaskStatsClearConsoleCmd(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                SendReply(arg, "You don't have permission to use this command.");
                return;
            }
            ClearTaskCompletionLog();
            SendReply(arg, "Task completion log cleared.");
        }

        private void RoamStopConsoleCmd(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                SendReply(arg, "You don't have permission to use this command.");
                return;
            }
            if (isUiBarVisible)
            {
                StopRoam();
            }
            else
            {
                PrintWarning("No Task Active.");
            }
        }

        private string selectedHook;
        private void SelectRandomHook(string forceHook = null)
        {
            if (!string.IsNullOrEmpty(forceHook))
            {
                selectedHook = forceHook;
                return;
            }
            List<string> availableHooks = new List<string>
            {
                "OnEntityDeath",
                "OnEntityTakeDamage",
                "OnDispenserGather",
                "OnCollectiblePickup",
                "OnLootEntity",
                "OnFishCatch",
                "OnItemCraftFinished",
                "OnCardSwipe",
                "OnEntityMounted",
                "OnBigWheelWin"
            };
            List<string> enabledHooks = new List<string>();
            Dictionary<string, int> enabledTaskCounts = new Dictionary<string, int>();
            foreach (var hook in availableHooks) 
            {
                List<TaskConfig> availableTasks = new List<TaskConfig>();
                if (config.HookTasks.TryGetValue(hook, out availableTasks))
                {
                    var enabledTasks = availableTasks.Count(task => task.Enabled);
                    if (enabledTasks > 0)
                    {
                        enabledHooks.Add(hook);
                        enabledTaskCounts[hook] = enabledTasks;
                    }
                }
            }
            
            if (enabledHooks.Count > 0)
            {
                Shuffle(enabledHooks); 
                int totalTaskCount = enabledTaskCounts.Values.Sum(); 
                List<float> probabilities = enabledHooks.Select(hook => (float)enabledTaskCounts[hook] / totalTaskCount).ToList(); 
                int selectedIndex = WeightedRandomIndex(probabilities); 
                selectedHook = enabledHooks[selectedIndex];
            }
            else
            {
                PrintWarning("No enabled tasks found for any hooks. Task canceled.");
                StopRoam();
            }
        }

        private int WeightedRandomIndex(List<float> probabilities) 
        {
            float randomValue = UnityEngine.Random.value;
            float cumulativeProbability = 0f;
            for (int i = 0; i < probabilities.Count; i++)
            {
                cumulativeProbability += probabilities[i];
                if (randomValue <= cumulativeProbability)
                {
                    return i;
                }
            }
            return probabilities.Count - 1;
        }

        private int selectedMaxComplete = 0;
        private string selectedTask;
        private string selectedTaskName = string.Empty;
        private void SelectRandomTask(string specifiedTask = null)
        {
            List<TaskConfig> availableTasks = new List<TaskConfig>();
            availableTasks.Clear();
            if (config.HookTasks.TryGetValue(selectedHook, out availableTasks))
            {
                if (!string.IsNullOrEmpty(specifiedTask)) 
                {
                    var task = availableTasks.FirstOrDefault(t => t.TaskID == specifiedTask);
                    if (task != null)
                    {
                        selectedTask = task.TaskID;
                        selectedTaskName = task.Name; 
                        selectedMaxComplete = task.MaxComplete;
                        float roamDuration = task.RoamDuration; 
                        roamEndTime = DateTime.UtcNow.AddSeconds(roamDuration); 
                        return;
                    }
                    else
                    {
                        PrintWarning($"Specified task '{specifiedTask}' not found. Task canceled.");
                        StopRoam();
                        return; 
                    }
                }
                var enabledTasks = availableTasks.Where(task => task.Enabled).ToList(); 
                if (enabledTasks.Count > 0)
                {
                    Shuffle(enabledTasks); 
                    var selectedTaskConfig = enabledTasks[0]; 
                    selectedTask = selectedTaskConfig.TaskID;
                    selectedTaskName = selectedTaskConfig.Name; 
                    selectedMaxComplete = selectedTaskConfig.MaxComplete; 
                    float roamDuration = selectedTaskConfig.RoamDuration; 
                    roamEndTime = DateTime.UtcNow.AddSeconds(roamDuration); 
                }

                else
                {
                    PrintWarning("No enabled tasks found for the selected Hook. Task Cancelled.");
                    StopRoam();
                    return; 
                }
            }
            else
            {
                PrintWarning("Selected Hook not found. Hooks are case sensitive. Task Cancelled.");
                StopRoam();
                return; 
            }
        }

        private void Shuffle<T>(List<T> list)
        {
            int n = list.Count;
            for (int i = n - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        private void SubscribeHooks(string hook)
        {
            switch (hook)
            {
                case "OnEntityDeath":
                    Subscribe("OnEntityDeath");
                    break;
                case "OnEntityTakeDamage":
                    Subscribe("OnEntityTakeDamage");
                    break;
                case "OnDispenserGather":
                    Subscribe("OnDispenserGather");
                    Subscribe("OnDispenserBonus");
                    break;
                case "OnCollectiblePickup":
                    Subscribe("OnCollectiblePickup");
                    break;
                case "OnLootEntity":
                    Subscribe("OnLootEntity");
                    break;
                case "OnFishCatch":
                    Subscribe("OnFishCatch");
                    break;
                case "OnItemCraftFinished":
                    Subscribe("OnItemCraftFinished");
                    break;
                case "OnCardSwipe":
                    Subscribe("OnCardSwipe");
                    break;
                case "OnEntityMounted":
                    Subscribe("OnEntityMounted");
                    Subscribe("OnEntityDismounted");
                    break;
                case "OnBigWheelWin":
                    Subscribe("OnBigWheelWin");
                    break;
                default:
                    PrintWarning($"Not valid hook {hook}");
                    break;
            }
        }
        
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo)
        {
            var player = hitInfo?.Initiator as BasePlayer;
            if (player == null || entity == null || hitInfo == null || playersWithCompletedTask.Contains(player))
                return;
            if (selectedHook == "OnEntityDeath") 
            {
                List<string> selectedTaskHookShortNames = GetSelectedTaskHookShortName().Split(',').ToList();
                if (selectedTaskHookShortNames.Contains(entity.ShortPrefabName.ToLower()))
                {
                    IncrementPlayerPoints(player, 1);
                    int requiredAmount = GetSelectedTaskRequiredAmount();
                    if (GetPlayerPoints(player) >= requiredAmount) 
                    {
                        CompleteTask(player);
                    }
                }
            }
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            BasePlayer player = info.InitiatorPlayer;
            if (player == null || entity == null || info == null || info.InitiatorPlayer == null || playersWithCompletedTask.Contains(player))
                return;
            if (selectedHook == "OnEntityTakeDamage") 
            {
                List<string> selectedTaskHookShortNames = GetSelectedTaskHookShortName().Split(',').ToList();
                if (selectedTaskHookShortNames.Contains(entity.ShortPrefabName.ToLower()))
                {
                    float damageAmount = info.damageTypes.Total();
                    int pointsEarned = Mathf.FloorToInt(damageAmount); 
                    IncrementPlayerPoints(player, pointsEarned);
                    UpdateTimerLabel(player);
                    int requiredAmount = GetSelectedTaskRequiredAmount();
                    if (GetPlayerPoints(player) >= requiredAmount) 
                    {
                        playersWithCompletedTask.Add(player);
                        CompleteTask(player);
                    }
                }
            }
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            BasePlayer player = entity.ToPlayer();
            if (player == null || dispenser == null || entity == null || item == null || playersWithCompletedTask.Contains(player))
                return;
            if (selectedHook == "OnDispenserGather") 
            {
                List<string> selectedTaskHookShortNames = GetSelectedTaskHookShortName().Split(',').ToList();
                string itemShortname = item.info.shortname.ToLower();
                if (selectedTaskHookShortNames.Contains(itemShortname))
                {
                    IncrementPlayerPoints(player, item.amount);
                    int requiredAmount = GetSelectedTaskRequiredAmount();
                    if (GetPlayerPoints(player) >= requiredAmount) 
                    {
                        CompleteTask(player);
                    }
                }
            }
        }

        private void OnDispenserBonus(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            BasePlayer player = entity.ToPlayer();
            if (player == null || dispenser == null || entity == null || item == null || playersWithCompletedTask.Contains(player))
                return;
            if (selectedHook == "OnDispenserGather") 
            {
                List<string> selectedTaskHookShortNames = GetSelectedTaskHookShortName().Split(',').ToList();
                string itemShortname = item.info.shortname.ToLower();
                if (selectedTaskHookShortNames.Contains(itemShortname))
                {
                    IncrementPlayerPoints(player, item.amount);
                    int requiredAmount = GetSelectedTaskRequiredAmount();
                    if (GetPlayerPoints(player) >= requiredAmount) 
                    {
                        CompleteTask(player);
                    }
                }
            }
        }

        private void OnCollectiblePickup(CollectibleEntity entity, BasePlayer player)
        {
            if (player == null || entity == null || playersWithCompletedTask.Contains(player))
                return;
            List<string> selectedTaskHookShortNames = GetSelectedTaskHookShortName().Split(',').Select(name => name.ToLower()).ToList();
            
            HashSet<string> validShortNames = new HashSet<string>(selectedTaskHookShortNames);
            
            bool validItemPickedUp = entity.itemList.Any(item => validShortNames.Contains(item.itemDef.shortname.ToLower()));
            if (validItemPickedUp)
            {
                IncrementPlayerPoints(player, (int)entity.itemList.Where(item => validShortNames.Contains(item.itemDef.shortname.ToLower())).Sum(item => item.amount));
                int requiredAmount = GetSelectedTaskRequiredAmount();

                if (GetPlayerPoints(player) >= requiredAmount)
                {
                    CompleteTask(player);
                }
            }
        }

        private HashSet<string> openedLootBoxes = new HashSet<string>();
        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (player == null || entity == null || playersWithCompletedTask.Contains(player))
                return;
            if (selectedHook == "OnLootEntity") 
            {
                List<string> selectedTaskHookShortNames = GetSelectedTaskHookShortName().Split(',').ToList();
                string entityShortName = entity.ShortPrefabName.ToLower();
                if (selectedTaskHookShortNames.Contains(entityShortName))
                {
                    string lootBoxId = entity.transform.position.ToString();
                    if (openedLootBoxes.Contains(lootBoxId))
                        return;
                    openedLootBoxes.Add(lootBoxId);
                    IncrementPlayerPoints(player, 1);
                    int requiredAmount = GetSelectedTaskRequiredAmount();
                    if (GetPlayerPoints(player) >= requiredAmount) 
                    {
                        CompleteTask(player);
                    }
                }
            }
        }

        private void OnFishCatch(Item item, BaseFishingRod rod, BasePlayer player)
        {
            if (player == null || item == null || rod == null || playersWithCompletedTask.Contains(player))
                return;

            if (selectedHook == "OnFishCatch")
            {
                List<string> selectedTaskHookShortNames = GetSelectedTaskHookShortName().Split(',').ToList();
                string itemShortname = item.info.shortname.ToLower();
                if (selectedTaskHookShortNames.Contains(itemShortname))
                {
                    IncrementPlayerPoints(player, item.amount);
                    int requiredAmount = GetSelectedTaskRequiredAmount();
                    if (GetPlayerPoints(player) >= requiredAmount) 
                    {
                        CompleteTask(player);
                    }
                }
            }
        }

        private void OnItemCraftFinished(ItemCraftTask task, Item item, ItemCrafter crafter)
        {
            if (crafter == null || crafter.owner == null)
            {
                Puts("OnItemCraftFinished: Crafter or owner is null. Instant crafting not supported for crafting tasks, if enabled.");
                return;
            }
            BasePlayer player = crafter.owner;
            if (playersWithCompletedTask.Contains(player))
            {
                return;
            }
            if (selectedHook == "OnItemCraftFinished")
            {
                List<string> selectedTaskHookShortNames = GetSelectedTaskHookShortName().Split(',').ToList();
                string itemShortname = item.info.shortname.ToLower();
                if (selectedTaskHookShortNames.Contains(itemShortname))
                {
                    IncrementPlayerPoints(player, 1);
                    int requiredAmount = GetSelectedTaskRequiredAmount();
                    if (GetPlayerPoints(player) >= requiredAmount) 
                    {
                        CompleteTask(player);
                    }
                }
            }
        }

        private void OnCardSwipe(CardReader cardReader, Keycard card, BasePlayer player)
        {
            if (player == null || cardReader == null || card == null || playersWithCompletedTask.Contains(player))
                return;
            if (selectedHook == "OnCardSwipe") 
            {
                string cardReaderInfo = $"Card Reader Info: Prefab: {cardReader.PrefabName}, ShortName: {cardReader.ShortPrefabName}, Position: {cardReader.transform.position}"; 
                string selectedCardShortName = GetSelectedTaskHookShortName();
                string swipedCardShortName = card.GetEntity().GetItem().info.shortname;
                int cardReaderAccessLevel = cardReader.accessLevel; 
                if (selectedCardShortName == swipedCardShortName) 
                {
                    if ((selectedCardShortName == "keycard_green" && cardReaderAccessLevel == 1) ||
                        (selectedCardShortName == "keycard_blue" && cardReaderAccessLevel == 2) ||
                        (selectedCardShortName == "keycard_red" && cardReaderAccessLevel == 3))
                    {
                        IncrementPlayerPoints(player, 1);
                        int requiredAmount = GetSelectedTaskRequiredAmount();
                        if (GetPlayerPoints(player) >= requiredAmount) 
                        {
                            CompleteTask(player);
                        }
                    }
                }
            }
        }

        private object OnBigWheelWin(BigWheelGame bigWheel, Item item, BigWheelBettingTerminal terminal, int multiplier)
        {
            if (terminal == null || item == null || bigWheel == null)
                return null; 
            BasePlayer player = terminal.lastPlayer; 
            if (player == null || playersWithCompletedTask.Contains(player))
                return null; 
            int requiredAmount = GetSelectedTaskRequiredAmount(); 
            int winnings = item.amount * multiplier;
            string wonItemShortName = item.info.shortname.ToLower(); 
            string selectedItemShortName = GetSelectedTaskHookShortName();
            if (wonItemShortName == selectedItemShortName)
            {
                IncrementPlayerPoints(player, winnings);
                if (GetPlayerPoints(player) >= requiredAmount) 
                {
                    CompleteTask(player);
                }
            }
            return null;
        }

        private Dictionary<BasePlayer, Vector3> playerLastPositions = new Dictionary<BasePlayer, Vector3>();
        private Timer distanceUpdateTimer;
        private float updateInterval = 2.333f;
        private void OnEntityMounted(BaseMountable entity, BasePlayer player)
        {
            if (player == null || entity == null || playersWithCompletedTask.Contains(player))
                return;

            if (selectedHook == "OnEntityMounted") 
            {
                List<string> selectedTaskHookShortNames = GetSelectedTaskHookShortName().Split(',').ToList();
                string entityShortName = entity.ShortPrefabName.ToLower();

                if (selectedTaskHookShortNames.Contains(entityShortName))
                {
                    playerLastPositions[player] = player.transform.position;
                }
            }
        }

        private void OnEntityDismounted(BaseMountable entity, BasePlayer player)
        {
            if (player == null || entity == null || playersWithCompletedTask.Contains(player))
                return;
            if (selectedHook == "OnEntityMounted") 
            {
                List<string> selectedTaskHookShortNames = GetSelectedTaskHookShortName().Split(',').ToList();
                string entityShortName = entity.ShortPrefabName.ToLower();
                if (selectedTaskHookShortNames.Contains(entityShortName))
                {
                    Vector3 lastPosition;
                    if (playerLastPositions.TryGetValue(player, out lastPosition))
                    {
                        float remainingDistanceDriven = Vector3.Distance(player.transform.position, lastPosition);
                        if (remainingDistanceDriven > 2f)
                        {
                            int remainingPointsEarned = Mathf.FloorToInt(remainingDistanceDriven);
                            IncrementPlayerPoints(player, remainingPointsEarned);
                        }
                    }
                    playerLastPositions.Remove(player);
                }
            }
        }

        private void UpdateMountedPlayerDistances(BasePlayer player)
        {
            if (player == null || playersWithCompletedTask.Contains(player))
                return;
            if (selectedHook != "OnEntityMounted") 
            {
                return;
            }
            List<string> selectedTaskHookShortNames = GetSelectedTaskHookShortName().Split(',').ToList();
            if (!player.isMounted)
            {
                return;
            }
            var mountedEntity = player.GetMounted();
            string entityShortName = mountedEntity.ShortPrefabName.ToLower();
            if (!selectedTaskHookShortNames.Contains(entityShortName)) 
            {
                return;
            }
            Vector3 currentPosition = player.transform.position; 
            Vector3 lastPosition;
            if (playerLastPositions.TryGetValue(player, out lastPosition))
            {
                float distanceDriven = Vector3.Distance(currentPosition, lastPosition); 

                if (distanceDriven > 0.001f) 
                {
                    int pointsEarned = Mathf.FloorToInt(distanceDriven); 
                    IncrementPlayerPoints(player, pointsEarned); 
                }
            }
            playerLastPositions[player] = currentPosition; 
            int requiredAmount = GetSelectedTaskRequiredAmount(); 
            if (GetPlayerPoints(player) >= requiredAmount)
            {
                CompleteTask(player);
            }
        }

        private Dictionary<BasePlayer, int> playerPoints = new Dictionary<BasePlayer, int>();
        private int GetPlayerPoints(BasePlayer player)
        {
            int points;
            if (playerPoints.TryGetValue(player, out points))
            {
                return points;
            }
            return 0; 
        }
        
        private void IncrementPlayerPoints(BasePlayer player, int amount)
        {
            if (!playerPoints.ContainsKey(player))
            {
                playerPoints[player] = 0; 
            }
            playerPoints[player] += amount; 
            UpdatePointsUI(player);
        }

        private int GetSelectedTaskRequiredAmount()
        {
            List<TaskConfig> availableTasks = new List<TaskConfig>();
            if (config.HookTasks.TryGetValue(selectedHook, out availableTasks))
            {
                var selectedTaskConfig = availableTasks.Find(task => task.TaskID == selectedTask);
                if (selectedTaskConfig != null)
                {
                    return selectedTaskConfig.RequiredAmount; 
                }
            }
            return 0;
        }

        private void GiveRewardToPlayer(BasePlayer player, RewardConfig rewardConfig)
        {
            foreach (int rewardType in rewardConfig.RewardType)
            {
                switch (rewardType)
                {
                    case 1: 
                        try
                        {
                            GiveItemReward(player, rewardConfig);
                        }
                        catch (Exception ex)
                        {
                            PrintWarning($"Error giving item reward to player '{player.displayName}'. Reward Type: '{rewardType}'.");
                        }
                        break;
                    case 2: 
                        try
                        {
                            ExecuteCommandReward(player, rewardConfig);
                        }
                        catch (Exception ex)
                        {
                            PrintWarning($"Error executing command reward for player '{player.displayName}'. Reward Type: '{rewardType}'.");
                        }
                        break;
                    case 3: 
                        try
                        {
                            GiveEconomicsReward(player, rewardConfig);
                        }
                        catch (Exception ex)
                        {
                            PrintWarning($"Error giving Economics reward to player '{player.displayName}'. Reward Type: '{rewardType}'.");
                        }
                        break;
                    case 4: 
                        try
                        {
                            GiveServerRewardsReward(player, rewardConfig);
                        }
                        catch (Exception ex)
                        {
                            PrintWarning($"Error giving ServerRewards reward to player '{player.displayName}'. Reward Type: '{rewardType}'.");
                        }
                        break;
                    default:
                        PrintWarning($"Unknown reward type '{rewardType}' for player '{player.displayName}'.");
                        break;
                }
            }
            
            string rewardDisplayName;
            try
            {
                rewardDisplayName = GetRewardDisplayName(rewardConfig);
            }
            catch (Exception ex)
            {
                rewardDisplayName = "Good Luck";
            }
            if (config.General != null && config.General.GameTipEnabled)
            {
                string gameTipMessage = config.General.gameTipMessage.Replace("{rewardDisplayName}", rewardDisplayName);
                SendGameTipWithQueue(player, gameTipMessage, 0);
            }
            if (config.General != null && config.General.ChatEnabled)
            {
                string chatMessage = config.General.chatMessage.Replace("{rewardDisplayName}", rewardDisplayName)
                    .Replace("{scientistName}", config.General.scientistName);
                player.ChatMessage(chatMessage);
            }
        }

        private bool isSendingGameTips = false;
        private Queue<GameTipInfo> gameTipQueue = new Queue<GameTipInfo>();
        private Timer gameTipTimer;

        private void SendGameTipWithQueue(BasePlayer player, string message, float delay)
        {
            gameTipQueue.Enqueue(new GameTipInfo(player, message, delay));

            if (!isSendingGameTips)
            {
                isSendingGameTips = true;
                HandleGameTipQueue(); 
                gameTipTimer = timer.Repeat(4.4f, 0, () =>
                {
                    HandleGameTipQueue();
                });
            }
        }

        private void HandleGameTipQueue()
        {
            if (gameTipQueue.Count > 0)
            {
                var gameTipInfo = gameTipQueue.Dequeue();
                gameTipInfo.Player.SendConsoleCommand("gametip.showgametip", gameTipInfo.Message, gameTipInfo.Player.UserIDString);
                timer.Once(GameTipDuration, () =>
                {
                    gameTipInfo.Player.SendConsoleCommand("gametip.hidegametip", "", gameTipInfo.Player.UserIDString);
                });
            }
            else
            {
                isSendingGameTips = false;
                gameTipTimer?.Destroy(); 
            }
        }

        public class GameTipInfo
        {
            public BasePlayer Player { get; }
            public string Message { get; }
            public float Delay { get; }

            public GameTipInfo(BasePlayer player, string message, float delay)
            {
                Player = player;
                Message = message;
                Delay = delay;
            }
        }

        private string GetRewardDisplayName(RewardConfig rewardConfig)
        {
            List<string> rewardDisplayNames = new List<string>();
            foreach (int rewardType in rewardConfig.RewardType)
            {
                switch (rewardType)
                {
                    case 1: 
                        foreach (RewardItem rewardItem in rewardConfig.RewardItems)
                        {
                            ItemDefinition itemDefinition = ItemManager.FindItemDefinition(rewardItem.ItemType);
                            string itemDisplayName = string.IsNullOrEmpty(rewardItem.ItemCustomName) ? itemDefinition.displayName.english : rewardItem.ItemCustomName;
                            rewardDisplayNames.Add($"{rewardItem.ItemAmount} {itemDisplayName}");
                        }
                        break;
                    case 2: 
                        var commandFormat = rewardConfig.CommandRewardFormat;
                        if (commandFormat.Contains("{customName}"))
                        {
                            var commandFormattedReward = commandFormat.Replace("{customName}", rewardConfig.CommandRewardCustomName.ToString());
                            rewardDisplayNames.Add(commandFormattedReward);
                        }
                        else
                        {
                            Console.WriteLine("Warning: Problem with reward message formatting, make sure {amount} and {customName} are correctly in config.");
                            
                        }
                        break;

                    case 3: 
                        var economicsFormat = rewardConfig.EconomicsRewardFormat;
                        if (economicsFormat.Contains("{amount}") && economicsFormat.Contains("{customName}"))
                        {
                            var economicsFormattedReward = economicsFormat.Replace("{amount}", rewardConfig.EconomicsRewardAmount.ToString())
                                                                        .Replace("{customName}", rewardConfig.EconomicsRewardCustomName);
                            rewardDisplayNames.Add(economicsFormattedReward);
                        }
                        else
                        {
                            Console.WriteLine("Warning: Problem with reward message formatting, make sure {amount} and {customName} are correctly in config.");
                            
                        }
                        break;

                    case 4: 
                        var serverRewardsFormat = rewardConfig.ServerRewardsRewardFormat;
                        if (serverRewardsFormat.Contains("{amount}") && serverRewardsFormat.Contains("{customName}"))
                        {
                            var serverRewardsFormattedReward = serverRewardsFormat.Replace("{amount}", rewardConfig.ServerRewardsAmount.ToString())
                                                                                .Replace("{customName}", rewardConfig.ServerRewardsRewardCustomName);
                            rewardDisplayNames.Add(serverRewardsFormattedReward);
                        }
                        else
                        {
                            Console.WriteLine("Warning: Problem with reward message formatting, make sure {amount} and {customName} are correctly in config.");
                            
                        }
                        break;
                }
            }
            return string.Join(", ", rewardDisplayNames);
        }

        private void GiveItemReward(BasePlayer player, RewardConfig reward)
        {
            foreach (var rewardItem in reward.RewardItems)
            {
                ItemDefinition itemDefinition = ItemManager.FindItemDefinition(rewardItem.ItemType);
                Item item = ItemManager.CreateByItemID(itemDefinition.itemid, rewardItem.ItemAmount, rewardItem.ItemSkinID);

                
                if (!string.IsNullOrEmpty(rewardItem.ItemCustomName))
                {
                    item.name = rewardItem.ItemCustomName;
                }

                
                if (IsInventoryFull(player))
                {
                    
                    ItemContainer backpack = player.inventory.GetContainer(PlayerInventory.Type.BackpackContents);
                    if (backpack != null)
                    {
                        
                        if (!IsContainerFull(backpack))
                        {
                            
                            backpack.AddItem(item.info, item.amount, item.skin);
                            
                        }
                        else
                        {
                            
                            item.Drop(player.eyes.position, player.eyes.BodyForward() * 2f);
                            
                        }
                    }
                    else
                    {
                        
                        item.Drop(player.eyes.position, player.eyes.BodyForward() * 2f);
                        
                    }
                }
                else
                {
                    
                    player.inventory.GiveItem(item);
                    
                }
            }
        }

        private bool IsInventoryFull(BasePlayer player)
        {
            return IsContainerFull(player.inventory.containerMain) && IsContainerFull(player.inventory.containerBelt);
        }

        private bool IsContainerFull(ItemContainer container)
        {
            return container.itemList.Count >= container.capacity;
        }

        private void ExecuteCommandReward(BasePlayer player, RewardConfig reward)
        {
            string command = reward.RewardCommand.Replace("{player.id}", player.UserIDString).Replace("{player.name}", player.displayName);
            player.SendConsoleCommand("chat.add", player.UserIDString, $"Executing command: {command}");
            ConsoleSystem.Run(ConsoleSystem.Option.Server, command);
            string rewardDisplayName = "Command Reward";
            string rewardDescription = "Command Reward";
        }

        private void GiveEconomicsReward(BasePlayer player, RewardConfig reward)
        {
            if (Economics != null)
            {
                Economics.Call("Deposit", player.UserIDString, reward.EconomicsRewardAmount);
                string rewardDisplayName = $"{reward.EconomicsRewardAmount} Economics Reward";
                string rewardDescription = "Economics Reward";
            }
            else
            {
                PrintWarning("Economics plugin not found. Make sure the plugin is installed and loaded.");
            }
        }

        private void GiveItem(BasePlayer player, string itemShortName, int amount, ulong skinId = 0)
        {
            ItemDefinition itemDefinition = ItemManager.FindItemDefinition(itemShortName);
            if (itemDefinition != null)
            {
                player.GiveItem(ItemManager.Create(itemDefinition, amount, skinId));
            }
            else
            {
                PrintWarning($"Unable to give item '{itemShortName}' to player '{player.displayName}', item not found.");
            }
        }

        private void GiveServerRewardsReward(BasePlayer player, RewardConfig reward)
        {
            if (ServerRewards != null)
            {
                ServerRewards.Call("AddPoints", player.UserIDString, reward.ServerRewardsAmount);
                string rewardDisplayName = $"{reward.ServerRewardsAmount} ServerRewards Points";
                string rewardDescription = "ServerRewards Points";
            }
            else
            {
                PrintWarning("ServerRewards plugin not found. Make sure the plugin is installed and loaded.");
            }
        }

        private HashSet<BasePlayer> playersWithCompletedTask = new HashSet<BasePlayer>();
        private CuiElementContainer temporaryuiBarContainer;
        private string uiBarPanelNameTemp = "RoamTasksUIBarTC";
        private string temporarysecondColorPanelName = "RoamTasksUIBarLineTC";
        string taskLabelNameTemp = CuiHelper.GetGuid();
        private void CompleteTask(BasePlayer player)
        {
            if (player.IsNpc)
                return;

            playersWithCompletedTask.Add(player);
            List<TaskConfig> availableTasks = new List<TaskConfig>();
            if (config.HookTasks.TryGetValue(selectedHook, out availableTasks))
            {
                selectedTaskConfig = availableTasks.Find(task => task.TaskID == selectedTask);
                if (selectedTaskConfig != null)
                {
                    var rewardPoolConfig = config.RewardPools[selectedTaskConfig.RewardPool];
                    if (rewardPoolConfig != null && rewardPoolConfig.RewardConfigs.Count > 0)
                    {
                        var rewardConfigOption = GetRandomRewardConfigOption(rewardPoolConfig.RewardConfigs);
                        if (rewardConfigOption != null && rewardConfigOption.RewardConfig.RewardEnabled)
                        {
                            GiveRewardToPlayer(player, rewardConfigOption.RewardConfig);
                            string soundPath = "assets/prefabs/misc/casino/slotmachine/effects/payout.prefab";
                            Effect.server.Run(soundPath, player, 0, Vector3.zero, Vector3.forward);
                        }
                    }
                }
            }
            DestroyUiElements(player);
            var temporaryUiBarContainer = new CuiElementContainer();
            var temporaryUiBarPanel = temporaryUiBarContainer.Add(new CuiPanel
            {
                FadeOut = uiBarFadeOutDuration, 
                Image = { Color = "0 0 0 0"},
                RectTransform =
                {
                    AnchorMin = $"{config.General.UIx} {config.General.UIy}",
                    AnchorMax = $"{config.General.UIx} {config.General.UIy}",
                    OffsetMin = $"{-190f} {82.5f}",
                    OffsetMax = $"{170f} {117.5f}"
                },
                CursorEnabled = false
            }, "Hud", uiBarPanelNameTemp);
            temporaryUiBarContainer.Add(new CuiElement
            {
                Name = taskLabelNameTemp,
                Parent = uiBarPanelNameTemp,
                FadeOut = uiBarFadeOutDuration,
                Components =
                {
                    new CuiTextComponent { Color = "192 192 192 0.8", Text = "Roam Complete", FontSize = 17, Align = TextAnchor.MiddleCenter, FadeIn = uiBarFadeInDuration },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.4", Distance = "0.4 0.4", UseGraphicAlpha = true }
                }
            });
            temporaryUiBarContainer.Add(new CuiElement
            {
                Name = temporarysecondColorPanelName,
                Parent = uiBarPanelNameTemp,
                FadeOut = uiBarFadeOutDuration,
                Components =
                {
                    new CuiImageComponent { Color = "0.438 0.572 0.182 0.95", Material = "assets/icons/greyout.mat", FadeIn = uiBarFadeInDuration },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 0.09" },
                }
            });
            
            DestroyUiElementsTemp(player);
            
            if (selectedMaxComplete > 0)
            {
                UpdateCompletionLabelForAllPlayers();
                if (playersWithCompletedTask.Count >= selectedMaxComplete)
                {
                    timer.Once(0.1f, () => 
                    {
                        StopRoam();
                    });
                }
            }
            int playerRank = playersWithCompletedTask.Count;
            if (config.General.PositionBonusRewards && config.RankBonusRewards.ContainsKey(playerRank))
            {
                RewardConfig rankBonusReward = config.RankBonusRewards[playerRank];
                if (rankBonusReward.RewardEnabled)
                {
                    GiveRewardToPlayer(player, rankBonusReward); 
                }
            }
            if (config.General != null && config.General.ChatEnabled && config.General.ChatTaskCompleteAnnounce)
            {
                string playerName = player.displayName.Length > 30 ? player.displayName.Substring(0, 30) : player.displayName;
                string RankReward = "";
                if (config.General.PositionBonusRewards && config.RankBonusRewards.ContainsKey(playerRank) && config.RankBonusRewards[playerRank].RewardEnabled)
                {
                    RankReward = config.General.RankRewardMessageAddon;
                }
                int maxPlayersToAnnounce = config.General.ChatTaskCompleteAnnounceMaxPlayers;
                string rankSuffix = GetRankSuffix(playerRank); 
                if (playerRank >= 1 && playerRank <= maxPlayersToAnnounce)
                {
                    string chatMessage = config.General.ChatTaskCompleteAnnounceMessage
                        .Replace("{config.General.scientistName}", config.General.scientistName)
                        .Replace("{playerName}", playerName)
                        .Replace("{RankReward}", RankReward)
                        .Replace("{playerRank}", $"{playerRank}") 
                        .Replace("{playerRankSuffix}", $"{rankSuffix}"); 

                    foreach (var basePlayer in BasePlayer.activePlayerList)
                    {
                        basePlayer.ChatMessage(chatMessage);
                    }
                }
                if (playerRank < 1)
                {
                    PrintWarning($"Player {playerName} does not have a valid rank.");
                }
            }
        }

        private string GetRankSuffix(int rank)
        {
            int lastDigit = rank % 10;
            switch (lastDigit)
            {
                case 1:
                    return "st";
                case 2:
                    return "nd";
                case 3:
                    return "rd";
                default:
                    return "th";
            }
        }

        private RewardConfigOption GetRandomRewardConfigOption(List<RewardConfigOption> rewardConfigOptions)
        {
            List<RewardConfigOption> enabledOptions = new List<RewardConfigOption>(); 
            enabledOptions.AddRange(rewardConfigOptions.Where(option => option.RewardConfig.RewardEnabled)); 
            if (enabledOptions.Count == 1)
            {
                return enabledOptions[0]; 
            }
            float totalChance = enabledOptions.Sum(option => option.Chance);
            float randomValue = UnityEngine.Random.Range(0f, totalChance);
            float cumulativeChance = 0f;
            foreach (var option in enabledOptions)
            {
                cumulativeChance += option.Chance;
                if (randomValue <= cumulativeChance)
                {
                    return option;
                }
            }
            return enabledOptions.LastOrDefault(); 
        }

        private string GetSelectedTaskHookShortName()
        {
            if (string.IsNullOrEmpty(selectedHook))
            {
                return string.Empty;
            }
            List<TaskConfig> availableTasks = new List<TaskConfig>();
            if (config.HookTasks.TryGetValue(selectedHook, out availableTasks))
            {
                var selectedTaskConfig = availableTasks.Find(task => task.TaskID == selectedTask);
                if (selectedTaskConfig != null)
                {
                    return string.Join(",", selectedTaskConfig.ShortName); 
                }
            }
            return string.Empty;
        }

        private Timer timerUpdateUI;
        private Timer roamDurationTimer;
        private float roamStartTime;
        private void StartRoam(string specifiedHook = null, string specifiedTask = null)
        {
            isUiBarVisible = true;
            playersWithCompletedTask.Clear();
            
            
            
            
            
            SelectRandomHook(specifiedHook); 
            if (string.IsNullOrEmpty(selectedHook))
            {
                PrintWarning("No Hook found. Task canceled.");
                StopRoam();
                return;
            }
            SelectRandomTask(specifiedTask); 
            if (string.IsNullOrEmpty(selectedTask))
            {
                PrintWarning("No task found. Task canceled.");
                StopRoam();
                return;
            }
            float roamDuration = 0;
            if (!string.IsNullOrEmpty(selectedTask))
            {
                List<TaskConfig> availableTasks = new List<TaskConfig>();
                if (config.HookTasks.TryGetValue(selectedHook, out availableTasks))
                {
                    var selectedTaskConfig = availableTasks.FirstOrDefault(task => task.TaskID == selectedTask);
                    if (selectedTaskConfig != null)
                    {
                        roamDuration = selectedTaskConfig.RoamDuration;
                    }
                }
            }
            Subscribe("OnSendCommand");
            if (roamDuration > 0) 
            {
                SubscribeHooks(selectedHook);
                foreach (var basePlayer in BasePlayer.activePlayerList)
                {
                    if (config.General != null && config.General.GameTipEnabled)
                    {
                        basePlayer.SendConsoleCommand("gametip.showtoast", 4, config.General.gameTipStartMessage);
                    }
                    if (config.General != null && config.General.ChatEnabled)
                    {
                        string[] scientistMessages = config.General.scientistMessages;
                        string selectedScientistMessage = scientistMessages[UnityEngine.Random.Range(0, scientistMessages.Length)];
                        string[] maxCompleteMessages = config.General.MaxCompleteMessage;
                        string selectedMaxCompleteMessage = maxCompleteMessages[UnityEngine.Random.Range(0, maxCompleteMessages.Length)];
                        string maxCompleteMessage = "";
                        if (selectedMaxComplete > 0)
                        {
                            maxCompleteMessage = selectedMaxCompleteMessage.Replace("{selectedMaxComplete}", selectedMaxComplete.ToString());
                        }
                        string formattedMessage = $"{config.General.scientistName}{selectedScientistMessage}{maxCompleteMessage}";
                        basePlayer.ChatMessage(formattedMessage);
                    }
                    if (selectedHook == "OnEntityMounted") 
                    {
                        List<string> selectedTaskHookShortNames = GetSelectedTaskHookShortName().Split(',').ToList();
                        if (distanceUpdateTimer == null)
                        {
                            distanceUpdateTimer = timer.Every(updateInterval, () =>
                            {
                                foreach (var player in BasePlayer.activePlayerList)
                                {
                                    if (player.isMounted)
                                    {
                                        var mountedEntity = player.GetMounted();
                                        string entityShortName = mountedEntity.ShortPrefabName.ToLower();
                                        if (selectedTaskHookShortNames.Contains(entityShortName)) 
                                        {
                                            UpdateMountedPlayerDistances(player);
                                            if (!playerLastPositions.ContainsKey(player))
                                            {
                                                playerLastPositions.Add(player, player.transform.position);
                                            }
                                        }
                                    }
                                }
                            });
                        }
                    }
                    
                }
                foreach (var currentPlayer in BasePlayer.activePlayerList)
                {
                    if (isUiBarVisible && !currentPlayer.IsSleeping())
                        CreateUiBar(currentPlayer);
                    if (config.General != null && config.General.StartSound)
                    {
                        string soundPath2 = "assets/prefabs/tools/pager/effects/beep.prefab"; 
                        Effect.server.Run(soundPath2, currentPlayer, 0, Vector3.zero, Vector3.forward);
                    }
                    if (config.General != null && config.General.StartSoundVibration)
                    {
                        string soundPath = "assets/prefabs/tools/pager/effects/vibrate.prefab"; 
                        Effect.server.Run(soundPath, currentPlayer, 0, Vector3.zero, Vector3.forward);
                    }
                }
                roamStartTime = Time.realtimeSinceStartup; 

                timerUpdateUI = timer.Repeat(15f, 0, () =>
                {
                    foreach (var player in BasePlayer.activePlayerList.Except(playersWithCompletedTask))
                    {
                        if (isUiBarVisible)
                            UpdateTimerLabel(player);

                        
                        float elapsedTime = Time.realtimeSinceStartup - roamStartTime;
                        float remainingTime = roamDuration - elapsedTime;

                        
                        if (remainingTime < 60f)
                        {
                            if (timerUpdateUI != null)
                                timerUpdateUI.Destroy();

                            timerUpdateUI = timer.Repeat(1f, 0, () =>
                            {
                                foreach (var p in BasePlayer.activePlayerList.Except(playersWithCompletedTask))
                                {
                                    if (isUiBarVisible)
                                        UpdateTimerLabel(p);
                                }
                            });
                        }
                    }
                });
                roamDurationTimer = timer.Once((int)(roamDuration), StopRoam); 
                playerPoints.Clear();
            }
            else
            {
                SelectRandomTask(); 
                StartRoam(); 
            }
            Subscribe("OnPlayerSleepEnded");
            Subscribe("OnPlayerSleep");
            Subscribe("OnPlayerConnected");
            Subscribe("OnPlayerDeath");
            Subscribe("OnPlayerRespawn");
        }

        private ulong steamAvatarUserID;
        private void ApplySteamAvatarUserID(string command, object[] args)
        {
            if (args == null || steamAvatarUserID == 0)
                return;
            ulong providedID;
            if (ulong.TryParse(args[1].ToString(), out providedID) && providedID == 0)
                args[1] = steamAvatarUserID;
        }

         private object OnSendCommand(Network.Connection cn, string strCommand, object[] args)
        {
            
            
            string GetArgumentAsString(int index)
            {
                if (args != null && index >= 0 && index < args.Length)
                {
                    return args[index]?.ToString()?.Trim();
                }
                return null;
            }

            
            string userName = GetArgumentAsString(2);

            
            if (userName != null && config != null && config.General != null)
            {
                if (config.General.scientistName != null)
                {
                    if (userName.Contains(config.General.scientistName))
                    {
                        
                        ApplySteamAvatarUserID(strCommand, args);
                    }
                }
            }

            
            return null;
        }

        private Dictionary<string, int> taskCompletionCounts = new Dictionary<string, int>();
        private void LogCompletedTask(string selectedHook, string selectedTask)
        {
            List<TaskConfig> availableTasks = new List<TaskConfig>();
            if (config.HookTasks.TryGetValue(selectedHook, out availableTasks))
            {
                var selectedTaskConfig = availableTasks.Find(task => task.TaskID == selectedTask);
                if (selectedTaskConfig != null)
                {
                    string taskName = selectedTaskConfig.Name;
                    string taskID = selectedTaskConfig.TaskID;
                    string taskIdentifier = $"{selectedHook} {taskID}, ({taskName})";
                    int completionCount;
                    if (taskCompletionCounts.TryGetValue(taskIdentifier, out completionCount))
                    {
                        taskCompletionCounts[taskIdentifier] = completionCount + 1;
                    }
                    else
                    {
                        taskCompletionCounts.Add(taskIdentifier, 1);
                    }
                    playersWithCompletedTask.Clear();
                    string logFileName = "RoamTasks_Log.txt";
                    string logFilePath = Path.Combine(Interface.Oxide.LogDirectory, logFileName); 
                    var lines = new List<string>();
                    if (File.Exists(logFilePath))
                    {
                        lines.AddRange(File.ReadAllLines(logFilePath));
                    }
                    bool taskFound = false;
                    for (int i = 0; i < lines.Count; i++)
                    {
                        string line = lines[i];
                        if (line.StartsWith($"Task: {taskIdentifier}, Total Completions:"))
                        {
                            int startIndex = line.LastIndexOf(':') + 1;
                            int currentCompletions = int.Parse(line.Substring(startIndex).Trim());
                            int newCompletions = currentCompletions + taskCompletionCounts[taskIdentifier]; 
                            lines[i] = $"Task: {taskIdentifier}, Total Completions: {newCompletions}";
                            taskFound = true;
                            break;
                        }
                    }
                    if (!taskFound)
                    {
                        int totalCompletions = taskCompletionCounts[taskIdentifier];
                        string line = $"Task: {taskIdentifier}, Total Completions: {totalCompletions}";
                        lines.Add(line);
                    }
                    File.WriteAllLines(logFilePath, lines);
                }
            }
        }

        private DateTime roamEndTime;
        private void StopRoam()
        {
            if (playersWithCompletedTask.Count > 0 && config.General.LogTasks) 
            {
                LogCompletedTask(selectedHook, selectedTask);
            }
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player != null)
                {
                    DestroyUiElements(player);
                    DestroyUiElementsTemp(player);
                }
            }
            if (distanceUpdateTimer != null)
            {
                distanceUpdateTimer.Destroy();
                distanceUpdateTimer = null;
            }
            playersWithCompletedTask.Clear();
            taskCompletionCounts.Clear();
            timerUpdateUI?.Destroy();
            timerUpdateUI = null;
            roamDurationTimer?.Destroy();
            roamDurationTimer = null;
            roamEndTime = DateTime.UtcNow.AddSeconds(0);
            isUiBarVisible = false;
            playerPoints.Clear();
            playerLastPositions.Clear();
            ScheduleNextRoam();
            UnsubscribeAll();
        }

        private void DestroyRoamTimers()
        {
            firstMessageTimer?.Destroy();
            secondMessageTimer?.Destroy();
            thirdMessageTimer?.Destroy();
            roamTimer?.Destroy();
            roamTimer = null;
            timerUpdateUI?.Destroy();
            timerUpdateUI = null;
            roamDurationTimer?.Destroy();
            roamDurationTimer = null;
            gameTipTimer?.Destroy();
        }

        private bool isUiBarVisible;
        private CuiElementContainer uiBarContainer;
        private string uiBarPanelName = "RoamTasksUIBar";
        string uiBackgroundImageName = CuiHelper.GetGuid();
        string uiSecondColorImageName = CuiHelper.GetGuid();
        string backgroundIconName = CuiHelper.GetGuid();
        string stopwatchIconName = CuiHelper.GetGuid();
        string taskLabelName = CuiHelper.GetGuid();  
        private void CreateUiBar(BasePlayer player)
        {
            if (player.IsNpc)
                return;

            if (uiBarContainer != null)
                CuiHelper.DestroyUi(player, uiBarPanelName);
            uiBarContainer = new CuiElementContainer();
            var uiBarPanel = uiBarContainer.Add(new CuiPanel
            {
                FadeOut = uiBarFadeOutDuration, 
                Image = { Color = "0 0 0 0"},
                RectTransform =
                {
                    AnchorMin = $"{config.General.UIx} {config.General.UIy}",
                    AnchorMax = $"{config.General.UIx} {config.General.UIy}",
                    OffsetMin = $"{-190f} {82.5f}",
                    OffsetMax = $"{170f} {117.5f}"
                },
                CursorEnabled = false
            }, "Hud", uiBarPanelName);
            uiBarContainer.Add(new CuiElement
            {
                Name = uiBackgroundImageName,
                Parent = uiBarPanelName,
                FadeOut = uiBarFadeOutDuration,
                Components =
                {
                    new CuiImageComponent { Color = "0.969 0.922 0.882 0.0", Material = "assets/icons/greyout.mat", FadeIn = uiBarFadeInDuration },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                }
            });
            uiBarContainer.Add(new CuiElement
            {
                Name = uiSecondColorImageName,
                Parent = uiBarPanelName,
                FadeOut = uiBarFadeOutDuration,
                Components =
                {
                    new CuiImageComponent { Color = "0.438 0.572 0.182 0.95", Material = "assets/icons/greyout.mat", FadeIn = uiBarFadeInDuration },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 0.09" },
                }
            });
            uiBarContainer.Add(new CuiElement 
            {
                Name = backgroundIconName,
                Parent = uiBarPanelName,
                FadeOut = 0f,
                Components =
                {
                    new CuiImageComponent { Sprite = "assets/icons/shadow.png", Color = "35 35 35 0.17", FadeIn = uiBarFadeInDuration },
                    new CuiRectTransformComponent { AnchorMin = "0.01 0", AnchorMax = "0.99 0.1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.6", Distance = "0.4 0.4", UseGraphicAlpha = true }
                }
            });
            uiBarContainer.Add(new CuiElement 
            {
                Name = stopwatchIconName,
                Parent = uiBarPanelName,
                FadeOut = 0f,
                Components =
                {
                    new CuiImageComponent { Sprite = "assets/icons/stopwatch.png", Color = "192 192 192 0.8", FadeIn = uiBarFadeInDuration },
                    new CuiRectTransformComponent { AnchorMin = "0.1 0.2", AnchorMax = "0.16 0.8" },
                    new CuiOutlineComponent { Color = "0 0 0 0.6", Distance = "0.4 0.4", UseGraphicAlpha = true }
                }
            });
            uiBarContainer.Add(new CuiElement
            {
                Name = taskLabelName, 
                Parent = uiBarPanelName,
                FadeOut = uiBarFadeOutDuration,
                Components =
                {
                    new CuiTextComponent { Color = "192 192 192 0.8", Text = $"{selectedTaskName}", FadeIn = uiBarFadeInDuration, FontSize = 17, Align = TextAnchor.MiddleCenter },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.4", Distance = "0.4 0.4", UseGraphicAlpha = true }
                }
            });
            CreateTimerLabel(player);
            CreatePointsLabel(player);
            if (selectedMaxComplete > 0)
            {
                CreateCompletionLabel(player);
            }
            CuiHelper.AddUi(player, uiBarContainer);
        }

        private string completionLabelName = "RoamTasksCompletionLabel";
        private string maxCompleteIconName = "MaxCompleteIcon";
        private void CreateCompletionLabel(BasePlayer player)
        {
            if (uiBarContainer != null)
            {
                int completedCount = playersWithCompletedTask.Count;
                var completionText = $"{completedCount}/{selectedMaxComplete}";
                var completionLabel = new CuiElement
                {
                    Name = completionLabelName,
                    Parent = uiBarPanelName,
                    Components =
                    {
                        new CuiTextComponent { Text = completionText, Color = "192 192 192 0.8", FontSize = 11, Align = TextAnchor.MiddleCenter },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0.1 0.57" },
                        new CuiOutlineComponent { Color = "0 0 0 0.4", Distance = "0.4 0.4", UseGraphicAlpha = true }
                    }
                };
                var maxCompleteIconName = "MaxCompleteIcon"; 
                var maxCompleteIcon = new CuiElement
                {
                    Name = maxCompleteIconName,
                    Parent = uiBarPanelName,
                    FadeOut = 0f,
                    Components =
                    {
                        new CuiImageComponent { Sprite = "assets/icons/clan.png", Color = "192 192 192 0.8", FadeIn = uiBarFadeInDuration },
                        new CuiRectTransformComponent { AnchorMin = "0.02 0.3", AnchorMax = "0.08 1" },
                        new CuiOutlineComponent { Color = "0 0 0 0.6", Distance = "0.4 0.4", UseGraphicAlpha = true }
                    }
                };
                uiBarContainer.Add(completionLabel);
                uiBarContainer.Add(maxCompleteIcon); 
            }
        }

        private void UpdateCompletionLabel(BasePlayer player)
        {
            if (uiBarContainer != null)
            {
                int completedCount = playersWithCompletedTask.Count;
                var completionText = $"{completedCount}/{selectedMaxComplete}";
                
                var completionLabel = uiBarContainer.FirstOrDefault(e => e.Name == completionLabelName);
                if (completionLabel != null)
                {
                    var textComponent = completionLabel.Components.Find(c => c is CuiTextComponent) as CuiTextComponent;
                    if (textComponent != null)
                    {
                        textComponent.Text = completionText;
                        CuiHelper.DestroyUi(player, completionLabelName);
                        CuiHelper.AddUi(player, new CuiElementContainer { completionLabel });
                    }
                }
            }
        }

        private void UpdateCompletionLabelForAllPlayers()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (!playersWithCompletedTask.Contains(player))
                {
                    UpdateCompletionLabel(player);
                }
            }
        }

        private string GetPointsText(int points)
        {
            List<TaskConfig> availableTasks = new List<TaskConfig>();
            if (config.HookTasks.TryGetValue(selectedHook, out availableTasks))
            {
                var selectedTaskConfig = availableTasks.Find(task => task.TaskID == selectedTask);
                if (selectedTaskConfig != null)
                {
                    int requiredAmount = selectedTaskConfig.RequiredAmount;
                    if (selectedHook == "OnEntityMounted") 
                    {
                        string pointsText = $"{points}m";
                        string requiredAmountText = $"{requiredAmount}m";
                        if (points >= 100)
                            pointsText = $"{(points / 1000.0):N1}km";
                        if (requiredAmount >= 100)
                            requiredAmountText = $"{(requiredAmount / 1000.0):N1}km";
                        return $"{pointsText}/{requiredAmountText}";
                    }
                    else
                    {
                        return $"{points}/{requiredAmount}";
                    }
                }
            }
            return $"{points}/0";
        }

        private string GetTimerText(TimeSpan timeRemaining)
        {
            if (timeRemaining < TimeSpan.Zero)
                timeRemaining = TimeSpan.Zero; 
            string formattedTime = string.Empty;
            if (timeRemaining.TotalHours >= 1)
            {
                double hours = Math.Round(timeRemaining.TotalHours, 1);
                formattedTime = $"{hours}h";
            }
            else if (timeRemaining.TotalMinutes >= 1)
            {
                int minutes = (int)timeRemaining.TotalMinutes;
                formattedTime = $"{minutes}m";
            }
            else
            {
                int seconds = (int)timeRemaining.TotalSeconds;
                formattedTime = $"{seconds}s";
            }
            return formattedTime;
        }

        private string timerLabelName = "RoamTasksTimerLabel";
        private void CreateTimerLabel(BasePlayer player)
        {
            if (uiBarContainer != null)
            {
                TimeSpan timeRemaining = roamEndTime - DateTime.UtcNow;
                if (timeRemaining < TimeSpan.Zero)
                    timeRemaining = TimeSpan.Zero;
                string formattedTime = GetTimerText(timeRemaining);
                uiBarContainer.Add(new CuiElement
                {
                    Name = timerLabelName,
                    Parent = uiBarPanelName,
                    FadeOut = 0f,
                    Components =
                    {
                        new CuiTextComponent { Text = formattedTime, Color = "192 192 192 0.8", FontSize = 15, Align = TextAnchor.MiddleLeft, FadeIn = 0f },
                        new CuiRectTransformComponent { AnchorMin = "0.17 0", AnchorMax = "1.17 1" },
                        new CuiOutlineComponent { Color = "0 0 0 0.4", Distance = "0.4 0.4", UseGraphicAlpha = true }
                    }
                });
            }
        }

        private string pointsLabelName = "PointsLabel";
        private void CreatePointsLabel(BasePlayer player)
        {
            if (uiBarContainer != null)
            {
                int points;
                if (!playerPoints.TryGetValue(player, out points))
                {
                    points = 0; 
                }
                uiBarContainer.Add(new CuiElement
                {
                    Name = pointsLabelName,
                    Parent = uiBarPanelName,
                    FadeOut = 0f,
                    Components =
                    {
                        new CuiTextComponent { Text = GetPointsText(points), Color = "192 192 192 0.8", FontSize = 15, Align = TextAnchor.MiddleCenter, FadeIn = 0f },
                        new CuiRectTransformComponent { AnchorMin = "0.35 0", AnchorMax = "1.35 1" },
                        new CuiOutlineComponent { Color = "0 0 0 0.4", Distance = "0.4 0.4", UseGraphicAlpha = true }
                    }
                });
            }
        }

        private void UpdateTimerLabel(BasePlayer currentPlayer)
        {
            if (uiBarContainer != null)
            {
                TimeSpan timeRemaining = roamEndTime - DateTime.UtcNow;
                if (timeRemaining < TimeSpan.Zero)
                    timeRemaining = TimeSpan.Zero;
                var label = uiBarContainer.FirstOrDefault(e => e.Name == timerLabelName);
                if (label != null)
                {
                    var textComponent = label.Components.Find(c => c is CuiTextComponent) as CuiTextComponent;
                    if (textComponent != null)
                    {
                        textComponent.Text = GetTimerText(timeRemaining);
                        if (isUiBarVisible && !currentPlayer.IsSleeping() && !currentPlayer.IsDead())
                        {
                            CuiHelper.DestroyUi(currentPlayer, timerLabelName);
                            CuiHelper.AddUi(currentPlayer, new CuiElementContainer { label });
                        }
                    }
                }
            }
        }

        private void UpdatePointsUI(BasePlayer player)
        {
            if (uiBarContainer != null)
            {
                string pointsLabelName = "PointsLabel"; 
                var pointsLabel = uiBarContainer.FirstOrDefault(e => e.Name == pointsLabelName) as CuiElement;
                if (pointsLabel != null)
                {
                    int points;
                    var textComponent = pointsLabel.Components.Find(c => c is CuiTextComponent) as CuiTextComponent;
                    if (playerPoints.TryGetValue(player, out points))
                    {
                        textComponent.Text = GetPointsText(points);
                        CuiHelper.DestroyUi(player, pointsLabelName); 
                        CuiHelper.AddUi(player, new CuiElementContainer { pointsLabel }); 
                    }
                }
            }
        }

        private void DestroyUiElements(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, backgroundIconName);
            CuiHelper.DestroyUi(player, stopwatchIconName);
            CuiHelper.DestroyUi(player, taskLabelName);
            CuiHelper.DestroyUi(player, pointsLabelName);
            CuiHelper.DestroyUi(player, uiBackgroundImageName);
            CuiHelper.DestroyUi(player, uiSecondColorImageName);
            CuiHelper.DestroyUi(player, timerLabelName);
            CuiHelper.DestroyUi(player, completionLabelName);
            CuiHelper.DestroyUi(player, maxCompleteIconName);
            CuiHelper.DestroyUi(player, uiBarPanelName);
        }

        private void DestroyUiElementsTemp(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, taskLabelNameTemp);
            CuiHelper.DestroyUi(player, temporarysecondColorPanelName);
            CuiHelper.DestroyUi(player, uiBarPanelNameTemp);
        }

        private void DestroyUiBar(BasePlayer player)
        {
            if (uiBarContainer == null)
                return;
            foreach (var element in uiBarContainer)
            {
                DestroyUiElements(player);
                DestroyUiElementsTemp(player);
            }
            uiBarContainer = null;
            temporaryuiBarContainer = null;
        }

        private void UnsubscribeAll()
        {
            Unsubscribe("OnEntityDeath");
            Unsubscribe("OnEntityTakeDamage");
            Unsubscribe("OnDispenserGather");
            Unsubscribe("OnDispenserBonus");
            Unsubscribe("OnCollectiblePickup");
            Unsubscribe("OnLootEntity");
            Unsubscribe("OnFishCatch");
            Unsubscribe("OnItemCraftFinished");
            Unsubscribe("OnCardSwipe");
            Unsubscribe("OnEntityMounted");
            Unsubscribe("OnBigWheelWin");
            Unsubscribe("OnEntityDismounted");
            Unsubscribe("OnSendCommand");
            Unsubscribe("OnPlayerSleepEnded");
            Unsubscribe("OnPlayerSleep");
            Unsubscribe("OnPlayerConnected");
            Unsubscribe("OnPlayerDeath");
            Unsubscribe("OnPlayerRespawn");
        }

        private void Unload()
        {
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                isUiBarVisible = false;
                DestroyRoamTimers();
                DestroyUiBar(basePlayer);
                playerPoints.Clear();
                basePlayer.SendConsoleCommand("gametip.hidegametip", "", basePlayer.UserIDString);
            }
            taskCompletionCounts.Clear();
            playersWithCompletedTask.Clear();
            distanceUpdateTimer?.Destroy();
            openedLootBoxes.Clear();
            playerLastPositions.Clear();
            UnsubscribeAll();
        }
    }
}