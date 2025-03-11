// Reference: 0Harmony
using Facepunch;
#if CARBON
using HarmonyLib;
#else
using Harmony;
#endif
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Assertions;
using static ConsoleSystem;

/* Suggestions
 * Add NPCs to drop list
 * Add permission options to items.
 * Add random perk from recycling.
 * Add command to toggle the button UI.
 */

/* 1.0.11
 * Updated the ipgive commands to take modifiers for each buff if specified. IE Butcher 1.2 would be a 120% buff given to the Butcher perk.
 * Added new command: ipgivewithskin <id> <skin> <optional: shortname> <optional: buffs/modifiers>
 */

namespace Oxide.Plugins
{
    [Info("Item Perks", "https://discord.gg/TrJ7jnS233", "1.0.11")]
    [Description("Perks that can be attached to items when they spawn")]
    class ItemPerks : RustPlugin
    {
        #region Config       

        private Configuration config;
        public class Configuration
        {
            [JsonProperty("Enhancement Settings")]
            public EnhancementSettings enhancementSettings = new EnhancementSettings();

            [JsonProperty("HUD Settings")]
            public PlayerSettings playerSettings = new PlayerSettings();

            [JsonProperty("Third Party Plugin Settings")]
            public ThirdpartyPluginSettings thirdpartyPluginSettings = new ThirdpartyPluginSettings();

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }


        public class ThirdpartyPluginSettings
        {
            [JsonProperty("ItemPerks Settings")]
            public ItemPerksSettings itemPerkSettings = new ItemPerksSettings();
            public class ItemPerksSettings
            {
                [JsonProperty("Enhancement Settings")]
                public bool allow_max_repair_on_enhanced_items = false;
            }

            [JsonProperty("RandomTrader Integration")]
            public RandomTraderAPI random_trader_api = new RandomTraderAPI();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    PrintToConsole("Configuration appears to be outdated; updating and saving");
                }
            }
            catch
            {
                PrintToConsole($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void SaveConfig()
        {
            PrintToConsole($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion

        #region Default Config

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();

            config.enhancementSettings.additional_perk_chances = DefaultAdditionalPerksChance;
            config.enhancementSettings.lootSettings.container = DefaultCrates;
            config.enhancementSettings.lootSettings.enhanceable_blacklist = DefaultBlackList;

            config.enhancementSettings.perk_config.thriftySettings.blacklist_shortnames.Add("gunpowder");
            config.enhancementSettings.perk_config.thriftySettings.blacklist_shortnames.Add("explosives");
            config.enhancementSettings.perk_config.thriftySettings.blacklist_shortnames.Add("sulfur");

            config.enhancementSettings.perk_config.fabricateSettings.blacklist_shortnames.Add("gunpowder");
            config.enhancementSettings.perk_config.fabricateSettings.blacklist_shortnames.Add("explosives");

            config.enhancementSettings.perk_config.componentLuckSettings.blacklist_shortnames = new List<string>() { "generic", "chassis", "glue", "bleach", "ducttape", "sticks", "vehicle.chassis", "vehicle.module", "vehicle.chassis.4mod", "vehicle.chassis.3mod", "vehicle.chassis.2mod", "electric.generator.small" };
            config.enhancementSettings.perk_config.electricalLuckSettings.blacklist_shortnames = new List<string>() { "electric.generator.small" };

            config.enhancementSettings.perk_config.woodcuttingLuckSettings.drop_rates = DefaultGatherLuck;
            config.enhancementSettings.perk_config.miningLuckSettings.drop_rates = DefaultGatherLuck;
            config.enhancementSettings.perk_config.skinningLuckSettings.drop_rates = DefaultGatherLuck;
            config.enhancementSettings.perk_config.fishingLuckSettings.drop_rates = DefaultGatherLuck;

            config.enhancementSettings.chance_modifier_settings.loot_chance_permissions = DefaultLootChancePermissions;
            config.enhancementSettings.chance_modifier_settings.recycle_chance_permissions = DefaultRecycleChancePermissions;
            config.enhancementSettings.chance_modifier_settings.craft_chance_permissions = DefaultCraftChancePermissions;

            config.enhancementSettings.enhancement_kit_settings.upgrade_settings.custom_item = new CustomCostItem() { shortname = "scrap", skin = 0 };
        }

        Dictionary<string, float> DefaultLootChancePermissions
        {
            get
            {
                return new Dictionary<string, float>()
                {
                    ["itemperks.lootchance.10"] = 0.1f,
                    ["itemperks.lootchance.20"] = 0.2f,
                    ["itemperks.lootchance.30"] = 0.3f,
                    ["itemperks.lootchance.40"] = 0.4f,
                    ["itemperks.lootchance.50"] = 0.5f
                };
            }
        }

        Dictionary<string, float> DefaultRecycleChancePermissions
        {
            get
            {
                return new Dictionary<string, float>()
                {
                    ["itemperks.recyclechance.10"] = 0.1f,
                    ["itemperks.recyclechance.20"] = 0.2f,
                    ["itemperks.recyclechance.30"] = 0.3f,
                    ["itemperks.recyclechance.40"] = 0.4f,
                    ["itemperks.recyclechance.50"] = 0.5f
                };
            }
        }

        Dictionary<string, float> DefaultCraftChancePermissions
        {
            get
            {
                return new Dictionary<string, float>()
                {
                    ["itemperks.craftchance.10"] = 0.1f,
                    ["itemperks.craftchance.20"] = 0.2f,
                    ["itemperks.craftchance.30"] = 0.3f,
                    ["itemperks.craftchance.40"] = 0.4f,
                    ["itemperks.craftchance.50"] = 0.5f
                };
            }
        }

        List<string> DefaultBlackList
        {
            get
            {
                return new List<string>()
                {
                    "tool.instant_camera",
                    "geiger.counter",
                    "grenade.f1",
                    "grenade.flashbang",
                    "grenade.molotov",
                    "explosive.satchel",
                    "grenade.smoke",
                    "explosive.timed"
                };
            }
        }

        Dictionary<string, float> DefaultCrates
        {
            get
            {
                return new Dictionary<string, float>()
                {
                    ["crate_normal_2"] = 3f,
                    ["crate_normal"] = 5f,
                    ["crate_elite"] = 15f,
                    ["crate_underwater_basic"] = 6f,
                    ["crate_underwater_advanced"] = 12f,
                    ["heli_crate"] = 10f,
                    ["bradley_crate"] = 10f,
                    ["codelockedhackablecrate"] = 10f,
                    ["codelockedhackablecrate_oilrig"] = 10f,
                    ["crate_tools"] = 1.5f,
                    ["loot-barrel-1"] = 1f,
                    ["loot_barrel_1"] = 1f,
                    ["loot-barrel-2"] = 2f,
                    ["loot_barrel_2"] = 2f
                };
            }
        }

        Dictionary<int, float> DefaultAdditionalPerksChance
        {
            get
            {
                return new Dictionary<int, float>()
                {
                    [2] = 25f,
                    [3] = 10f
                };
            }
        }

        Dictionary<Perk, PerkSettings> DefaultPerkSettings
        {
            get
            {
                return new Dictionary<Perk, PerkSettings>()
                {
                    [Perk.Academic] = new PerkSettings(true, 0.05f, 0.25f),
                    [Perk.Angler] = new PerkSettings(true, 0.05f, 0.5f),
                    [Perk.Attractive] = new PerkSettings(true, 0.15f, 0.25f),
                    [Perk.BeastBane] = new PerkSettings(true, 0.05f, 0.2f),
                    [Perk.BeastWard] = new PerkSettings(true, 0.05f, 0.2f),
                    [Perk.BlastMine] = new PerkSettings(true, 0.05f, 0.1f),
                    [Perk.Builder] = new PerkSettings(true, 0.02f, 0.1f),
                    [Perk.Butcher] = new PerkSettings(true, 0.05f, 0.2f),
                    [Perk.ComponentLuck] = new PerkSettings(true, 0.03f, 0.08f),
                    [Perk.Deforest] = new PerkSettings(true, 0.02f, 0.06f),
                    [Perk.Durable] = new PerkSettings(true, 0.05f, 0.1f),
                    [Perk.ElectronicsLuck] = new PerkSettings(true, 0.03f, 0.08f),
                    [Perk.Elemental] = new PerkSettings(true, 0.05f, 0.15f),
                    [Perk.Environmentalist] = new PerkSettings(true, 0.05f, 0.15f),
                    [Perk.Fabricate] = new PerkSettings(true, 0.04f, 0.1f),
                    [Perk.FallDamage] = new PerkSettings(true, 0.05f, 0.20f),
                    [Perk.FishingLuck] = new PerkSettings(true, 0.01f, 0.05f),
                    [Perk.FlakJacket] = new PerkSettings(true, 0.15f, 0.25f),
                    [Perk.Forager] = new PerkSettings(true, 0.05f, 0.15f),
                    [Perk.HealShare] = new PerkSettings(true, 0.03f, 0.10f),
                    [Perk.Horticulture] = new PerkSettings(true, 0.02f, 0.10f),
                    [Perk.IronStomach] = new PerkSettings(true, 0.5f, 1),
                    [Perk.Lead] = new PerkSettings(true, 0.05f, 0.2f),
                    [Perk.LineStrength] = new PerkSettings(true, 0.05f, 0.2f, 100, new List<string>() { "fishingrod.handmade" }),
                    [Perk.Lumberjack] = new PerkSettings(true, 0.05f, 0.2f),
                    [Perk.Manufacture] = new PerkSettings(true, 0.05f, 0.2f),
                    [Perk.MeleeWard] = new PerkSettings(true, 0.05f, 0.25f),
                    [Perk.MiningLuck] = new PerkSettings(true, 0.01f, 0.03f),
                    [Perk.Pharmaceutical] = new PerkSettings(true, 0.04f, 0.10f),
                    [Perk.Paramedic] = new PerkSettings(true, 0.05f, 0.15f),
                    [Perk.Prepper] = new PerkSettings(true, 0.05f, 0.15f),
                    [Perk.Prospector] = new PerkSettings(true, 0.05f, 0.2f),
                    [Perk.Regeneration] = new PerkSettings(true, 0.1f, 0.2f),
                    [Perk.Reinforced] = new PerkSettings(true, 0.05f, 0.2f),
                    [Perk.RockCycler] = new PerkSettings(true, 0.03f, 0.07f),
                    [Perk.Sated] = new PerkSettings(true, 0.1f, 0.5f),
                    [Perk.Scavenger] = new PerkSettings(true, 0.05f, 0.2f),
                    [Perk.ScientistBane] = new PerkSettings(true, 0.03f, 0.15f),
                    [Perk.ScientistWard] = new PerkSettings(true, 0.05f, 0.15f),
                    [Perk.SharkBane] = new PerkSettings(true, 0.1f, 0.25f),
                    [Perk.SharkWard] = new PerkSettings(true, 0.1f, 0.25f),
                    [Perk.SkinningLuck] = new PerkSettings(true, 0.01f, 0.05f),
                    [Perk.Smasher] = new PerkSettings(true, 0.5f, 1f),
                    [Perk.Smelter] = new PerkSettings(true, 0.05f, 0.12f),
                    [Perk.Tanner] = new PerkSettings(true, 0.02f, 0.06f),
                    [Perk.Thrifty] = new PerkSettings(true, 0.05f, 0.10f),
                    [Perk.TreePlanter] = new PerkSettings(true, 0.03f, 0.07f),
                    [Perk.UncannyDodge] = new PerkSettings(true, 0.01f, 0.03f),
                    [Perk.Vampiric] = new PerkSettings(true, 0.005f, 0.02f),
                    [Perk.WoodcuttingLuck] = new PerkSettings(true, 0.01f, 0.03f),
                    [Perk.BradleyDamage] = new PerkSettings(true, 0.03f, 0.05f),
                    [Perk.HeliDamage] = new PerkSettings(true, 0.03f, 0.05f),
                };
            }
        }

        Dictionary<string, DropInfo> DefaultGatherLuck
        {
            get
            {
                return new Dictionary<string, DropInfo>()
                {
                    ["fuse"] = new DropInfo(1, 2),
                    ["propanetank"] = new DropInfo(1, 5),
                    ["gears"] = new DropInfo(1, 3),
                    ["metalblade"] = new DropInfo(1, 5),
                    ["metalpipe"] = new DropInfo(1, 3),
                    ["metalspring"] = new DropInfo(1, 2),
                    ["riflebody"] = new DropInfo(1, 1),
                    ["roadsigns"] = new DropInfo(1, 3),
                    ["semibody"] = new DropInfo(1, 2),
                    ["sewingkit"] = new DropInfo(1, 3),
                    ["sheetmetal"] = new DropInfo(1, 2),
                    ["smgbody"] = new DropInfo(1, 2),
                    ["tarp"] = new DropInfo(1, 5),
                    ["techparts"] = new DropInfo(1, 2)
                };
            }
        }

        Dictionary<string, List<ulong>> DefaultItemSkins()
        {
            var result = new Dictionary<string, List<ulong>>();
            System.Random rnd = new System.Random();

            foreach (var skin in Rust.Workshop.Approved.All.Values)
            {
                if (EnhanceableItems.Contains(skin.Skinnable.ItemName))
                {
                    List<ulong> list;
                    if (!result.TryGetValue(skin.Skinnable.ItemName, out list)) result.Add(skin.Skinnable.ItemName, list = new List<ulong>());
                    list.Add(skin.WorkshopdId);
                }
            }

            // Picks 5 random skins from each result
            foreach (var entry in result)
            {
                if (entry.Value.Count > 5)
                {
                    List<ulong> skins = Pool.GetList<ulong>();
                    skins.AddRange(entry.Value.OrderBy(x => rnd.Next()).Take(5));
                    entry.Value.Clear();
                    entry.Value.AddRange(skins);
                    Pool.FreeList(ref skins);
                }
                else if (entry.Value.Count == 0) entry.Value.Add(0);
            }
            return result;
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            Dictionary<string, string> langDict = new Dictionary<string, string>()
            {
                ["UICLOSE"] = "CLOSE",
                ["UIPERK"] = "PERK",
                ["UIMOD"] = "MOD",
                ["UIRESET"] = "RESET",
                ["UITOTALBONUSES"] = "TOTAL BONUSES",
                ["hps"] = "hp/s",
                ["Notify_FirstFindLoot"] = "You have found an Enhanced Item!\nCheck your chat for more information.",
                ["Enhanced"] = "Enhanced",
                ["FoundItemMessage"] = "You found an <color=#ffb600>{0}</color>\n{1}",
                ["CraftedItemMessage"] = "You crafted a <color=#ffb600>{0}</color>\n{1}",
                ["PerksMessage"] = "<color=#ffff00>Perks:</color>",
                ["ModsString"] = "\n- <color=#ffb600>{0}</color>",
                ["FishLuckMessage"] = "As you catch the fish, you find {0} {1}.",
                ["some"] = "some",
                ["a"] = "a",
                ["ComponentsRefundedMessage"] = "Your components were refunded!",
                ["ItemDuplicatedMessage"] = "Your {0} was duplicated!",
                ["ItemRationedMessage"] = "You managed to ration your {0}.",
                ["FreeUpgradeMessage"] = "You received a free upgrade!",
                ["ResearchRefundedMessage"] = "Your research cost was refunded!",
                ["WoodcuttingLuckMessage"] = "You found {0} {1} while cutting down the tree.",
                ["MiningLuckMessage"] = "You found {0} {1} while mining the node.",
                ["SkinningLuckMessage"] = "You found {0} {1} while skinning the corpse.",
                ["DodgeMessage"] = "You manage to dodge out of the way of danger!",
                ["NoPerms"] = "You do not have permission to use this command.",
                ["MissingItem"] = "Item no longer exists in inventory",
                ["DisabledRegen"] = "Your regen has been disabled for <color=#ff8000>{0} seconds</color> after taking damage.",
                ["FailedTofindPlayer"] = "{0} is invalid, not connected or dead.",
                ["UIEnhancementButton"] = "ENHANCE ITEM",
                ["UIEnhancementChooseTitle"] = "CHOOSE AN ENHANCEMENT KIT TO USE",
                ["UIEnhancementChooseNoIKits"] = "<color=#ffff00>You do not have any enhancement kits in your inventory.</color>",
                ["UIEnhancementChooseKitTitle"] = "<color=#ffae00>{0}</color>",
                ["UIEnhancementItemSelectTitle"] = "CHOOSE AN ITEM TO ENHANCE",
                ["UIEnhancementItemSelectNoItems"] = "<color=#ffff00>You do not have any valid items in your inventory.</color>",
                ["UIEnhancementConfirmTitle"] = "CONFIRM YOUR ENHANCEMENT",
                ["UIEnhancementConfirmItemTitle"] = "<color=#ffec00>ITEM:</color> {0}",
                ["UIEnhancementConfirmPerkTitle"] = "<color=#ffec00>PERK:</color> {0}",
                ["UIEnhancementConfirmCostTitle"] = "<color=#ffec00>COST:</color> {0} {1}",
                ["UIEnhancementConfirmPerkConfirmation"] = "Enhancing this item will add an additional <color=#00ff00>{0}</color> enhancement with a random value. This cannot be reversed.",
                ["Eh_econ_NotEnough"] = "You do not have enough money to perform this enhancement.",
                ["Eh_econ_Error"] = "Something went wrong while withdrawing money for the enhancement.",
                ["Eh_SRP_NotEnough"] = "You do not have enough points to perform this enhancement.",
                ["Eh_SRP_Error"] = "Something went wrong while taking points for the enhancement.",
                ["Eh_Scrap_NotEnough"] = "You do not have enough scrap to perform this enhancement.",
                ["Eh_Custom_NotEnough"] = "You do not have enough {0} to perform this enhancement.",
                ["IPGive_Usage"] = "Usage: /ipgive <target> <Optional: shortname> <optional: perks>",
                ["IPWithSkinGive_Usage"] = "Usage: /ipgive <target> <skin> <Optional: shortname> <optional: perks>",
                ["ReceivedKit"] = "You received 1x <color=#4cff03>{0}</color>",
                ["GaveKitConsoleReply"] = "Gave 1x {0} to {1}.",
                ["KitConsoleUsageRevised"] = "Usage: ipgivekit <target> <optional: perk>",
                ["KitChatUsage"] = "Usage: /ipgivekit <perk>",
                ["PerkInvalid"] = "Invalid perk: {0}",
                ["PerkPlayerLimit"] = "\nPerk capped at: <color=#51ff00>{0}%</color>.",
                ["serverrewards"] = "RP",
                ["scrap"] = "Scrap",
                ["economics"] = "Dollars",
                ["custom"] = "Custom Item"
            };

            var perks = PerkDescriptions;

            foreach (var perk in perks)
                langDict.Add(perk.Key, perk.Value);

            perks.Clear();
            perks = null;

            foreach (var perk in DefaultPerkSettings)
            {
                var str = perk.Key.ToString();
                string value = string.Empty;
                bool firstChat = true;
                foreach (char c in str)
                {
                    if (!firstChat && char.IsUpper(c))
                        value += " ";
                    value += c;
                    firstChat = false;
                }
                langDict.Add("UI" + str, value);
            }

            lang.RegisterMessages(langDict, this);
        }

        Dictionary<string, string> PerkDescriptions
        {
            get
            {
                return new Dictionary<string, string>()
                {
                    ["Prospector"] = "This perk increases your mining yield when mining stone, metal and sulfur nodes.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Lumberjack"] = "This perk increases your woodcutting yield when chopping living or dead trees.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Butcher"] = "This perk increases the resources gained from skinning humans and animals.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Horticulture"] = "This perk increases the amount of resources gained when harvesting grown plans.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Forager"] = "This perk increases the amount of resources gained when picking up map generated collectibles.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Angler"] = "This perk increases the amount of fish you receive upon a successful catch.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["BeastBane"] = "This perk increases the damage dealt to animals.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["ScientistBane"] = "This perk increases the damage dealt to scientists.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["FlakJacket"] = "This perk reduces the damage received from explosions.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Elemental"] = "This perk reduces damage received from cold and heat sources.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Scavenger"] = "This perk provides you with a chance to find additional scrap from crates and barrels.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Hybrid"] = "This perk reduces fuel consumption when mounting boats and helicopters.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Manufacture"] = "This perk increases the speed of your crafting.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Durable"] = "This peark reduces the durability damage of all equipped items.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["BeastWard"] = "This perk reduces the damage received from animals.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["ScientistWard"] = "This perk reduces the damage received from scientists.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Equestrian"] = "This perk increases the speed of your mounted horse.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Builder"] = "This perk provides you with a chance for your building upgrades to be free.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Thrifty"] = "This perk provides you with a chance for your crafting components to be refunded upon a successful craft.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Fabricate"] = "This perk provides you with a chance to duplicate an item upon a successful craft.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Pharmaceutical"] = "This perk increases the amount of healing received from all sources.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["MeleeWard"] = "This perk reduces the damage received from melee weapons.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Sails"] = "Ths perk increases the speed of your boat.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Academic"] = "This perk provides you with a chance to receive a scrap refund when researching an item at the research bench.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["FallDamage"] = "This perk reduces the impact damage received from falling.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Lead"] = "This perk reduces the damage from radiation.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Gilled"] = "This perk allows you to breath underwater for a set amount of time.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Smasher"] = "This perk will provide you with a chance to instantly destroy barrels with any amount of damage.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Environmentalist"] = "This perk will increase the speed of recyclers that you activate.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Smelter"] = "This perk will increase the smelting speed of furnaces you activate.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Paramedic"] = "This perk provides players that you revive with additional health.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Prepper"] = "This perk provides you with a chance to not consume food when eating.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Regeneration"] = "This perk will passively regenerate you.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["SharkWard"] = "This perk will reduce the amount of damage received from sharks.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["SharkBane"] = "This perk will increase the amount of damage dealt to sharks.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Deforest"] = "This perk provides you with a chance to cut down nearby trees when successfully cutting a tree down.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["BlastMine"] = "This perk provides you with a chance to mine out nearby nodes when successfully mining out a node.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Tanner"] = "This perk provides you with a chance to skin nearby corpses when successfully skinning out a corpse.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Vampiric"] = "This perk will heal you for a percentage of the damage dealt to certain enemies.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Reinforced"] = "This perk will reduce the the amount of damage that your vehicles receive when mounted.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["ComponentLuck"] = "This perk will provide you with a chance to receive additional components when looting barrels and crates.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["ElectronicsLuck"] = "This perk will provide you with a chance to receive additional electronics when looting barrels and crates.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["UncannyDodge"] = "This perk provides you with a chance to dodge incoming damage, reducing it to 0.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["LineStrength"] = "This perk increases the tensile strength of your fishing line.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["HealShare"] = "This perk will share healing effects with nearby players.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Attractive"] = "This perk will provide you with a chance to automatically pick up components when destroying barrels.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["TreePlanter"] = "This perk will provide a chance for a tree to instantly regrow when cut down.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["RockCycler"] = "This perk will provide a chance for a node to instantly respawn when mined out.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["Sated"] = "This perk will increase the amount of calories and hydration you receive from food and water sources.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["IronStomach"] = "This perk provides you with a chance to negate negative effects when consuming food.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["WoodcuttingLuck"] = "This perk provides you with a chance to find a random item when you cut down a tree.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["MiningLuck"] = "This perk provides you with a chance to find a random item when you mine out a node.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["SkinningLuck"] = "This perk provides you with a chance to find a random item when you skin out a corpse.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["FishingLuck"] = "This perk provides you with a chance to find a random item when you catch a fish.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["BradleyDamage"] = "This perk perk increases the damage dealt to Bradley Tanks.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                    ["HeliDamage"] = "This perk perk increases the damage dealt to Patrol Helicopters.\nPerk rolls - Min: <color=#51ff00>{0}{2}</color> <color=#ffff00><></color> Max: <color=#51ff00>{1}{2}</color>",
                };
            }
        }

        #endregion

        #region Classes

        [PluginReference]
        private Plugin Cooking, UINotify, Economics, ServerRewards, RandomTrader;

        public class PlayerSettings
        {
            [JsonProperty("Command to open the ItemPerks menu")]
            public string command = "ip";

            [JsonProperty("Send the icon to access the ItemPerks menu?")]
            public bool send_menu_icon = true;

            [JsonProperty("Icon position")]
            public IconSettings iconSettings = new IconSettings(64, 0, 0);

            [JsonProperty("Steam workshop ID to use for the hud icon")]
            public ulong icon_id = 2907569326;

            [JsonProperty("Only show the inspector icon when the player has an enhanced piece equipped/held?")]
            public bool only_show_when_equipped = true;

            [JsonProperty("Show the UI displaying the combined bonuses for all worn/held items?")]
            public bool show_total_bonuses = true;

            [JsonProperty("Settings for UINotify")]
            public UINotifySettings uinotify_settings = new UINotifySettings();
        }

        public class UINotifySettings
        {
            [JsonProperty("Send player specific messages using the notify plugin")]
            public bool enabled = true;

            [JsonProperty("Message type")]
            public int messageType = 0;

            [JsonProperty("Send notification when an Enhanced item drops from a barrel or crate")]
            public bool notify_ItemDrops = true;

            [JsonProperty("Send notification when the player crafts an Enhanced item")]
            public bool notify_Crafted = true;

            [JsonProperty("Send notification when the FishingLuck perk procs")]
            public bool notify_FishingLuck = true;

            [JsonProperty("Send notification when the WoodcuttingLuck perk procs")]
            public bool notify_WoodcuttingLuck = true;

            [JsonProperty("Send notification when the MiningLuck perk procs")]
            public bool notify_MiningLuck = true;

            [JsonProperty("Send notification when the SkinningLuck perk procs")]
            public bool notify_SkinningLuck = true;

            [JsonProperty("Send notification when the Thrifty perk procs")]
            public bool notify_ComponentRefund = true;

            [JsonProperty("Send notification when the Fabricate perk procs")]
            public bool notify_ItemDuplication = true;

            [JsonProperty("Send notification when the Prepper perk procs")]
            public bool notify_Rationed = true;

            [JsonProperty("Send notification when the Builder perk procs")]
            public bool notify_FreeUpgrade = true;

            [JsonProperty("Send notification when the Academic perk procs")]
            public bool notify_ResearchRefund = true;

            [JsonProperty("Send notification when the UncannyDodge perk procs")]
            public bool notify_Dodge = true;
        }

        public class IconSettings
        {
            public float size;
            public float offset_x;
            public float offset_y;
            public string anchor_min = "0.5 0";
            public string anchor_max = "0.5 0";

            public IconSettings(float size, float offset_x, float offset_y, string anchor_min = "0.5 0", string anchor_max = "0.5 0")
            {
                this.size = size;
                this.offset_x = offset_x;
                this.offset_y = offset_y;
                this.anchor_min = anchor_min;
                this.anchor_max = anchor_max;
            }
        }

        public class EnhancementSettings
        {
            [JsonProperty("Loot settings")]
            public LootSettings lootSettings = new LootSettings();

            [JsonProperty("Chance for an item to receive additional perks after successfully rolling its first perk? [out of 100]")]
            public Dictionary<int, float> additional_perk_chances;

            [JsonProperty("Allow items to have the same perk more than once?")]
            public bool allow_duplicate_perks = true;

            [JsonProperty("Naming prefix to show that the item is enhanced. Leaving empty will not adjust the items name.")]
            public string item_name_prefix = "Enhanced";

            [JsonProperty("Prevent the maximum condition of the item from being reduced when repaired?")]
            public bool prevent_max_condition_loss = false;

            [JsonProperty("Perk settings")]
            public Dictionary<Perk, PerkSettings> perk_settings;

            [JsonProperty("Perk configuration")]
            public PerkConfigs perk_config = new PerkConfigs();

            [JsonProperty("List of skins for item types. If more than 1 skin is added, a random one will be selected when the item is created.")]
            public Dictionary<string, List<ulong>> item_skins = new Dictionary<string, List<ulong>>();

            [JsonProperty("Permission based modifiers")]
            public ChanceModifierSettings chance_modifier_settings = new ChanceModifierSettings();

            [JsonProperty("Enhancement kit settings")]
            public EnhancementKitSettings enhancement_kit_settings = new EnhancementKitSettings();
        }

        public class EnhancementKitSettings
        {
            [JsonProperty("Cap the amount of perks that an item can have attached to it?")]
            public bool perks_capped = true;

            public string displayName = "enhancement kit:";
            public string shortname = "blood";
            public ulong skin = 2920198584;

            [JsonProperty("Recycle Settings")]
            public EnhancementRecyclingSettings recycle_settings = new EnhancementRecyclingSettings();

            [JsonProperty("Enhancement Settings")]
            public EnhancementUpgradeSettings upgrade_settings = new EnhancementUpgradeSettings();
        }

        public class EnhancementRecyclingSettings
        {
            [JsonProperty("Chance when recycling an enhanced item (per enhancement) to give an enhancement kit [0 = off, 100.0 = 100%]")]
            public float perk_kit_chance = 5f;

            [JsonProperty("Effect to run over the recycler when a kit is successfully awarded")]
            public string success_effect = "assets/prefabs/misc/halloween/lootbag/effects/gold_open.prefab";

            [JsonProperty("Provide a random perk if successful (instead of the perk attached to the recycled item)?")]
            public bool random_perk = false;
        }

        public class EnhancementUpgradeSettings
        {
            [JsonProperty("Effect to run when the upgrade is successful")]
            public string enhance_effect = "assets/prefabs/misc/halloween/lootbag/effects/gold_open.prefab";

            [JsonProperty("Type of currency to use for additional upgrade costs [scrap, economics, serverrewards, custom]")]
            public string upgrade_cost_type = "scrap";

            [JsonProperty("Custom currency information")]
            public CustomCostItem custom_item = new CustomCostItem();

            [JsonProperty("Additional cost when enhancing an item via an enhancement kit")]
            public Dictionary<Perk, float> additional_upgrade_cost = new Dictionary<Perk, float>();
        }

        public class CustomCostItem
        {
            [JsonProperty("Currency shortname")]
            public string shortname;

            [JsonProperty("Currency skin ID")]
            public ulong skin;
        }

        public class LootSettings
        {
            // Key is crate type. Float is chance.
            [JsonProperty("Chance for a player to receive an enhanced item when looting a container [out of 100]")]
            public Dictionary<string, float> container;

            [JsonProperty("List of items that cannot be enhanced")]
            public List<string> enhanceable_blacklist = new List<string>();

            [JsonProperty("Use Rust's default loot profile? [true: Will only give items that the container could provide]")]
            public bool use_default_loot_profiles = true;

            [JsonProperty("Chance out of 100 for the item to be enhanced when crafting [0 = off]")]
            public float craft_chance = 1f;

            [JsonProperty("Notify the player in chat when they craft an enhanced item")]
            public bool notify_craft_chat = true;

            [JsonProperty("List of items that cannot be enhanced when crafting")]
            public List<string> craft_blacklist = new List<string>();

            [JsonProperty("Cooldown after successfully crafting an enhanced item, before they can craft another one [seconds]")]
            public float craft_success_cooldown = 0;
        }

        public class ChanceModifierSettings
        {
            [JsonProperty("Permission modifiers to increase the chance of obtaining enhanced loot from all sources [1.0 = 100% increase in chance]")]
            public Dictionary<string, float> loot_chance_permissions = new Dictionary<string, float>();

            [JsonProperty("Permission modifiers to increase the chance of obtaining enhancement kit when recycling [1.0 = 100% increase in chance]")]
            public Dictionary<string, float> recycle_chance_permissions = new Dictionary<string, float>();

            [JsonProperty("Permission modifiers to increase the chance of obtaining enhanced loot when crafting [1.0 = 100% increase in chance]")]
            public Dictionary<string, float> craft_chance_permissions = new Dictionary<string, float>();
        }

        public class PerkSettings
        {
            public bool enabled;
            public float min_mod;
            public float max_mod;
            public int perkWeight;
            public List<string> whitelist;
            public List<string> blacklist;
            [JsonProperty("Perk modifier cap")]
            public float perk_cap;

            public PerkSettings(bool enabled, float min_mod, float max_mod, int perkWeight = 100, List<string> whitelist = null, List<string> blacklist = null, float player_mod_limit = 0)
            {
                this.enabled = enabled;
                this.min_mod = min_mod;
                this.max_mod = max_mod;
                this.perkWeight = perkWeight;
                this.whitelist = whitelist;
                this.blacklist = blacklist;
                this.perk_cap = player_mod_limit;
            }
        }

        public class PerkConfigs
        {
            [JsonProperty("Settings for Deforest perk")]
            public DeforestSettings deforestSettings = new DeforestSettings();

            [JsonProperty("Settings for BlastMine perk")]
            public BlastMineSettings blastMineSettings = new BlastMineSettings();

            [JsonProperty("Settings for Tanner perk")]
            public TannerSettings tannerSettings = new TannerSettings();

            [JsonProperty("Settings for Vampiric perk")]
            public VampiricSettings vampiricSettings = new VampiricSettings();

            [JsonProperty("Maximum amount of scrap that the scavenger perk can award")]
            public int scavenger_max_amount = 3;

            [JsonProperty("Settings for Thrifty perk")]
            public ThriftySettings thriftySettings = new ThriftySettings();

            [JsonProperty("Settings for Fabricate perk")]
            public FabricateSettings fabricateSettings = new FabricateSettings();

            [JsonProperty("Settings for Prepper perk")]
            public PrepperSettings prepperSettings = new PrepperSettings();

            [JsonProperty("Settings for ComponentLuck perk")]
            public ComponentLuckSettings componentLuckSettings = new ComponentLuckSettings();

            [JsonProperty("Settings for ElectricalLuck perk")]
            public ElectricalLuckSettings electricalLuckSettings = new ElectricalLuckSettings();

            [JsonProperty("Settings for Attractive perk")]
            public AttractiveSettings attractiveSettings = new AttractiveSettings();

            [JsonProperty("Settings for HealShare perk")]
            public HealShareSettings healShareSettings = new HealShareSettings();

            [JsonProperty("Settings for Regeneration perk")]
            public RegenSettings regenerationSettings = new RegenSettings();

            [JsonProperty("Settings for WoodcuttingLuck perk")]
            public GatherLuckSettings woodcuttingLuckSettings = new GatherLuckSettings();

            [JsonProperty("Settings for MiningLuck perk")]
            public GatherLuckSettings miningLuckSettings = new GatherLuckSettings();

            [JsonProperty("Settings for SkinningLuck perk")]
            public GatherLuckSettings skinningLuckSettings = new GatherLuckSettings();

            [JsonProperty("Settings for FishingLuck perk")]
            public GatherLuckSettings fishingLuckSettings = new GatherLuckSettings();

            [JsonProperty("Settings for ScientistBane perk")]
            public ScientistBaneSettings ScientistBaneSettings = new ScientistBaneSettings();

            [JsonProperty("Settings for Durability perk")]
            public DurabilitySettings DurabilitySettings = new DurabilitySettings();
        }

        #region Perk configs        

        public class DurabilitySettings
        {
            [JsonProperty("List of items that the Durability perk will not work with")]
            public List<string> durability_blacklist = new List<string>();
        }

        public class DeforestSettings
        {
            [JsonProperty("The radius that the Deforest perk checks for trees to cut down")]
            public float deforest_max_radius = 5f;
        }

        public class BlastMineSettings
        {
            [JsonProperty("The radius that the BlastMine perk checks for nodes to mine out")]
            public float blastmine_max_radius = 30f;
        }

        public class TannerSettings
        {
            [JsonProperty("The radius that the Tanner perk checks for corpses to skin out")]
            public float tanner_max_radius = 30f;
        }

        public class VampiricSettings
        {
            [JsonProperty("Enemy types that the Vampiric ability will work on [scientist, player, animal]")]
            public List<string> vampiric_entities = new List<string>();
        }

        public class ThriftySettings
        {
            [JsonProperty("List of items that will not be refunded upon a successful Thrifty proc")]
            public List<string> blacklist_shortnames = new List<string>();
        }

        public class FabricateSettings
        {
            [JsonProperty("List of items that will not be duplicated upon a successful Fabricate proc")]
            public List<string> blacklist_shortnames = new List<string>();
        }

        public class PrepperSettings
        {
            [JsonProperty("List of items that will not be refunded when the Prepper perk procs")]
            public List<string> blacklist_shortnames = new List<string>();
        }

        public class ComponentLuckSettings
        {
            [JsonProperty("Minimum amount of components that will be given when the ComponentLuck perk procs")]
            public int min = 1;

            [JsonProperty("Maximum amount of components that will be given when the ComponentLuck perk procs")]
            public int max = 3;

            [JsonProperty("List of components that are excluded by the ComponentLuck perk")]
            public List<string> blacklist_shortnames = new List<string>();
        }

        public class ElectricalLuckSettings
        {
            [JsonProperty("Minimum amount of electronics that will be given when the ElectricalLuck perk procs")]
            public int min = 1;

            [JsonProperty("Maximum amount of electronics that will be given when the ElectricalLuck perk procs")]
            public int max = 2;

            [JsonProperty("List of electrical items that are excluded by the ElectricalLuck perk")]
            public List<string> blacklist_shortnames = new List<string>();
        }

        public class AttractiveSettings
        {
            [JsonProperty("Maximum distance that the Attractive perk will work [0 = no limit]")]
            public float max_dist = 0f;

            [JsonProperty("Only allow melee perks to trigger the Attractive perk?")]
            public bool meleeOnly = false;
        }

        public class HealShareSettings
        {
            [JsonProperty("Maximum distance that the HealShare perk will work")]
            public float distance = 10f;

            [JsonProperty("Only allow Healshare to work on members of the players team?")]
            public bool team_only = true;
        }

        public class RegenSettings
        {
            [JsonProperty("Delay after receiving damage before the regen buff continues to heal [set to 0 to ignore damage]")]
            public float damage_delay = 5f;
        }

        public class GatherLuckSettings
        {
            public Dictionary<string, DropInfo> drop_rates = new Dictionary<string, DropInfo>();
        }

        public class DropInfo
        {
            public int min_amount;
            public int max_amount;
            public ulong skin;
            public string display_name;

            public DropInfo(int min_amount, int max_amount, ulong skin = 0, string display_name = null)
            {
                this.min_amount = min_amount;
                this.max_amount = max_amount;
                this.skin = skin;
                this.display_name = display_name;
            }
        }

        public class ScientistBaneSettings
        {
            [JsonProperty("Only allow ScientistBane to work against vanilla scientist NPCs (excludes HumanNPC, scarecrows etc)?")]
            public bool scientist_only = false;
        }
        #endregion

        static Dictionary<ulong, PlayerPerks> Player_perks;

        public class PlayerPerks
        {
            public Dictionary<Perk, float> perk_modifiers = new Dictionary<Perk, float>();
        }

        static Dictionary<string, Perk> parsedEnums = new Dictionary<string, Perk>(StringComparer.InvariantCultureIgnoreCase);
        List<Perk> PerksList;
        Dictionary<string, ItemBlueprint> item_BPs;
        Dictionary<string, int> ItemIDs;
        List<string> EnhanceableItems;
        List<ItemDefinition> ElectricalItems;
        List<ItemDefinition> ComponentItems;

        #endregion

        #region enums

        const string perm_use = "itemperks.use";
        const string perm_loot = "itemperks.loot";
        const string perm_craft = "itemperks.craft";
        const string perm_admin = "itemperks.admin";
        const string perm_enhance = "itemperks.enhance";
        const string perm_recycle = "itemperks.recycle";
        const float DefaultLineStrength = 0.75f;

        // Make it so 1.0 = 100%

        public enum Perk
        {
            None,
            Prospector, // Mining yield ++
            Lumberjack, // Woodcutting yield ++
            Butcher, // Skinning yield ++
            Horticulture, // Farming yield +
            Forager, // Harvesting yield ++
            Angler, // Fishing yield ++
            BeastBane, // More damage to animals ++
            ScientistBane, // More damage to scientists ++
            FlakJacket, // Reduced damage from explosions  ++
            Elemental, // Reduced damage from elements (fire/cold)  ++
            Scavenger, // Chance to find additional scrap in crates/barrels.  ++
            //Hybrid, // Reduced fuel consumption
            Manufacture, // Crafting speed ++
            Durable, // Reduces durability loss ++
            BeastWard, // Reduce damage from animals  ++
            ScientistWard, // Reduced damage from scientists  ++
            //Equestrian, // Horse speed            
            Builder, // Chance to refund materials used to upgrade building blocks ++
            Thrifty, // Chance to refund crafting components ++
            Fabricate, // Chance to duplicate the crafted item ++
            Pharmaceutical, // Increased healing ++
            MeleeWard, // Reduced melee damage  ++
            //Sails, // increased boat speed
            Academic, // chance to refund research cost ++
            FallDamage, // Reduces fall damage  ++
            Lead, // Reduces radiation damage  ++
            //Gilled, // Breath underwater
            Smasher, // Chance to smash barrels and road signs instantly. Find a better name.  ++
            Environmentalist, // Recycler speed ++
            Smelter, // Smelt speed ++
            Paramedic, // Reviver - increase health of player you are reviving ++
            Prepper, // Rationer - chance to not consume food ++
            Regeneration, // Health regen ++
            SharkWard, // Shark resist  ++
            SharkBane, // Shark bane  ++
            // Untie speed reduction
            Deforest, // tree clear out (like WC ultimate) ++
            BlastMine, // node clear out (same as WC, but for nodes) ++
            Tanner, // skin clear out (same as WC, but for bodies) ++
            Vampiric, // Vampiric ++
            Reinforced, // Vehicle damage reduction ++
            // skin cook
            ComponentLuck, // Component luck ++
            ElectronicsLuck, // electrical luck ++
            UncannyDodge, // Chance to receive no damage from attack ++
            LineStrength, // Fishing rod strength ++
            HealShare, // Heals nearby team members when you heal ++
            Attractive, //Loot magnet ++
            WoodcuttingLuck, // random components from woodcutting on FinalHit ++
            MiningLuck, // random components from mining on FinalHit ++
            SkinningLuck, // random components from skinning on FinalHit ++
            FishingLuck, // random components from fishing on catch ++
            Sated, // More cal/hydration from food
            IronStomach, // Chance for no poison or cal/water reduction when eating
            // Vehicle repair costs reduction
            // Explosives refund chance perk
            // Melee damage
            // Smelting mined ore
            // Instant mining
            // Instant woodcutting
            // Instant skinning
            TreePlanter, // Tree regrowth when cut down ++
            RockCycler, // Node respawn when mined out ++
            BradleyDamage,
            HeliDamage,
        }

        #endregion

        #region Data

        PlayerEntity pcdData;
        private DynamicConfigFile PCDDATA;

        void SaveData()
        {
            PCDDATA.WriteObject(pcdData);
        }

        void LoadData()
        {
            try
            {
                pcdData = Interface.Oxide.DataFileSystem.ReadObject<PlayerEntity>(this.Name);
            }
            catch
            {
                Puts("Couldn't load player data, creating new Playerfile");
                pcdData = new PlayerEntity();
            }
        }

        class PlayerEntity
        {
            public Dictionary<ulong, PCDInfo> pEntity = new Dictionary<ulong, PCDInfo>();
        }

        class PCDInfo
        {
            public IconSettings iconSettings;

            public PCDInfo(IconSettings iconSettings)
            {
                this.iconSettings = iconSettings;
            }
        }

        PCDInfo GetPlayerData(BasePlayer player)
        {
            PCDInfo pi;
            if (pcdData.pEntity.TryGetValue(player.userID, out pi)) return pi;
            pi = new PCDInfo(new IconSettings(config.playerSettings.iconSettings.size, config.playerSettings.iconSettings.offset_x, config.playerSettings.iconSettings.offset_y));
            pcdData.pEntity.Add(player.userID, pi);
            return pi;
        }

        #endregion;

        #region Hooks

        void Init()
        {
            PCDDATA = Interface.Oxide.DataFileSystem.GetFile(this.Name);
            LoadData();
            permission.RegisterPermission(perm_use, this);
            permission.RegisterPermission(perm_loot, this);
            permission.RegisterPermission(perm_craft, this);
            permission.RegisterPermission(perm_admin, this);
            permission.RegisterPermission(perm_enhance, this);
            permission.RegisterPermission(perm_recycle, this);

            if (!config.thirdpartyPluginSettings.itemPerkSettings.allow_max_repair_on_enhanced_items) Unsubscribe(nameof(STOnItemRepairWithMaxRepair));

            foreach (var perm in config.enhancementSettings.chance_modifier_settings.loot_chance_permissions.Keys)
            {
                permission.RegisterPermission(perm.ToLower().StartsWith("itemperks.") ? perm : "itemperks." + perm, this);
            }

            foreach (var perm in config.enhancementSettings.chance_modifier_settings.recycle_chance_permissions.Keys)
            {
                permission.RegisterPermission(perm.ToLower().StartsWith("itemperks.") ? perm : "itemperks." + perm, this);
            }

            foreach (var perm in config.enhancementSettings.chance_modifier_settings.craft_chance_permissions.Keys)
            {
                permission.RegisterPermission(perm.ToLower().StartsWith("itemperks.") ? perm : "itemperks." + perm, this);
            }
        }

        void Unload()
        {
            _harmony.UnpatchAll(Name + "Patch");
            Pool.FreeList(ref PerksList);
            foreach (var oven in OvenWatchList)
            {
                oven.Key.StopCooking();
            }
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyRegen(player);
                DestroyUI(player);
                CuiHelper.DestroyUi(player, "InspectorButtonImg");
                CloseEnhancementMenu(player);
            }
            if (!string.IsNullOrEmpty(config.playerSettings.command))
            {
                cmd.RemoveChatCommand(config.playerSettings.command, this);
                cmd.RemoveConsoleCommand(config.playerSettings.command, this);
            }
            ClearLists();

            SaveData();
        }

        void OnServerInitialized(bool initial)
        {
            InstantiateDict();
            bool DoSave = false;
            if (config.enhancementSettings.lootSettings.container == null || config.enhancementSettings.lootSettings.container.Count == 0)
            {
                config.enhancementSettings.lootSettings.container = DefaultCrates;
                DoSave = true;
            }

            if (config.enhancementSettings.additional_perk_chances == null || config.enhancementSettings.additional_perk_chances.Count == 0)
            {
                config.enhancementSettings.additional_perk_chances = DefaultAdditionalPerksChance;
                DoSave = true;
            }

            if (config.enhancementSettings.perk_settings == null || config.enhancementSettings.perk_settings.Count == 0)
            {
                config.enhancementSettings.perk_settings = DefaultPerkSettings;
                DoSave = true;
            }
            else
            {
                foreach (var kvp in DefaultPerkSettings)
                {
                    if (!config.enhancementSettings.perk_settings.ContainsKey(kvp.Key))
                    {
                        config.enhancementSettings.perk_settings.Add(kvp.Key, kvp.Value);
                        Puts($"Added new perk: {kvp.Key}");
                        DoSave = true;
                    }
                }
            }
            if (config.enhancementSettings.perk_config.vampiricSettings.vampiric_entities.Count == 0)
            {
                config.enhancementSettings.perk_config.vampiricSettings.vampiric_entities.Add("animal");
                config.enhancementSettings.perk_config.vampiricSettings.vampiric_entities.Add("player");
                config.enhancementSettings.perk_config.vampiricSettings.vampiric_entities.Add("scientist");
                DoSave = true;
            }

            if (config.enhancementSettings.perk_config.woodcuttingLuckSettings.drop_rates.Count == 0)
            {
                config.enhancementSettings.perk_config.woodcuttingLuckSettings.drop_rates = DefaultGatherLuck;
                DoSave = true;
            }

            if (config.enhancementSettings.perk_config.miningLuckSettings.drop_rates.Count == 0)
            {
                config.enhancementSettings.perk_config.miningLuckSettings.drop_rates = DefaultGatherLuck;
                DoSave = true;
            }

            if (config.enhancementSettings.perk_config.skinningLuckSettings.drop_rates.Count == 0)
            {
                config.enhancementSettings.perk_config.skinningLuckSettings.drop_rates = DefaultGatherLuck;
                DoSave = true;
            }

            if (config.enhancementSettings.perk_config.fishingLuckSettings.drop_rates.Count == 0)
            {
                config.enhancementSettings.perk_config.fishingLuckSettings.drop_rates = DefaultGatherLuck;
                DoSave = true;
            }

            if (config.enhancementSettings.chance_modifier_settings.loot_chance_permissions.Count == 0)
            {
                config.enhancementSettings.chance_modifier_settings.loot_chance_permissions = DefaultLootChancePermissions;
                DoSave = true;
            }

            if (config.enhancementSettings.chance_modifier_settings.craft_chance_permissions.Count == 0)
            {
                config.enhancementSettings.chance_modifier_settings.craft_chance_permissions = DefaultCraftChancePermissions;
                DoSave = true;
            }

            if (config.enhancementSettings.chance_modifier_settings.recycle_chance_permissions.Count == 0)
            {
                config.enhancementSettings.chance_modifier_settings.recycle_chance_permissions = DefaultRecycleChancePermissions;
                DoSave = true;
            }

            parsedEnums = new Dictionary<string, Perk>(StringComparer.InvariantCultureIgnoreCase);
            PerksList = Pool.GetList<Perk>();
            item_BPs = new Dictionary<string, ItemBlueprint>();
            ItemIDs = new Dictionary<string, int>();
            EnhanceableItems = new List<string>();
            ElectricalItems = new List<ItemDefinition>();
            ComponentItems = new List<ItemDefinition>();
            Player_perks = new Dictionary<ulong, PlayerPerks>();

            List<Perk> perks = Pool.GetList<Perk>();

            perks.AddRange(Enum.GetValues(typeof(Perk)).Cast<Perk>().Intersect(config.enhancementSettings.perk_settings.Keys));

            foreach (var perk in config.enhancementSettings.perk_settings)
            {
                if (!perk.Value.enabled) continue;
                PerksList.Add(perk.Key);
                parsedEnums[perk.Key.ToString()] = perk.Key;
                if (perk.Value.perk_cap > 0) GlobalLimits.Add(perk.Key, perk.Value.perk_cap);
                if (!config.enhancementSettings.enhancement_kit_settings.upgrade_settings.additional_upgrade_cost.ContainsKey(perk.Key))
                {
                    config.enhancementSettings.enhancement_kit_settings.upgrade_settings.additional_upgrade_cost.Add(perk.Key, 0);
                    DoSave = true;
                }
            }
            Puts($"Loaded Perks:\n- {string.Join("\n- ", parsedEnums.Keys)}");
            Pool.FreeList(ref perks);

            SetupSubscriptions();

            if (Player_perks == null) Player_perks = new Dictionary<ulong, PlayerPerks>();
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (!permission.UserHasPermission(player.UserIDString, perm_use)) continue;
                GetPlayerPerks(player);
                if (!config.playerSettings.only_show_when_equipped)
                    SendInspectMenuButton(player);
            }
            GetEnhanceableItems();
            if (config.enhancementSettings.item_skins == null || config.enhancementSettings.item_skins.Count == 0)
            {
                config.enhancementSettings.item_skins = DefaultItemSkins();
                Puts($"Added skins for {config.enhancementSettings.item_skins.Count} item types.");
                DoSave = true;
            }

            if (config.enhancementSettings.enhancement_kit_settings.upgrade_settings.custom_item == null || string.IsNullOrEmpty(config.enhancementSettings.enhancement_kit_settings.upgrade_settings.custom_item.shortname))
            {
                config.enhancementSettings.enhancement_kit_settings.upgrade_settings.custom_item = new CustomCostItem() { shortname = "scrap", skin = 0 };
                DoSave = true;
            }

            if (DoSave) SaveConfig();         
            
            foreach (var kvp in config.enhancementSettings.additional_perk_chances)
            {
                if (kvp.Key > MaxPerks) MaxPerks = kvp.Key;
            }
            Puts($"Set max perks to {MaxPerks}");

            if (config.enhancementSettings.enhancement_kit_settings.recycle_settings.perk_kit_chance == 0) Unsubscribe(nameof(OnItemRecycle));

            if (!string.IsNullOrEmpty(config.playerSettings.command))
            {
                cmd.AddChatCommand(config.playerSettings.command, this, nameof(SendMenu));
                cmd.AddConsoleCommand(config.playerSettings.command, this, nameof(SendMenu));
            }

            Unsubscribe(nameof(STCanGainXP));
        }

        void InstantiateDict()
        {
            took_damage = new Dictionary<ulong, float>();
            RegenAmount = new Dictionary<BasePlayer, float>();
            if (parsedEnums == null) parsedEnums = new Dictionary<string, Perk>(StringComparer.InvariantCultureIgnoreCase);
        }

        void OnItemDropped(Item item, BaseEntity entity)
        {
            if (item == null || string.IsNullOrEmpty(item.text) || !HasPerks(item.text)) return;

            var player = item.GetOwnerPlayer();
            if (player == null || player.IsNpc || !player.userID.IsSteamId()) return;

            List<Perk> perks = Pool.GetList<Perk>();

            PlayerPerks pp;
            if (Player_perks.TryGetValue(player.userID, out pp) && pp.perk_modifiers != null) perks.AddRange(pp.perk_modifiers.Keys);

            var activeItem = player.GetActiveItem();
            if (activeItem != null && activeItem != item && !string.IsNullOrEmpty(activeItem.text) && HasPerks(activeItem.text))
            {
                var mods = GetMods(activeItem.text);
                perks.AddRange(mods.Keys);
                WipeDictionary(mods);
            }

            HandlePlayerSubscription(player, perks);
            Pool.FreeList(ref perks);
        }

        void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (player == null || player.IsNpc || !player.userID.IsSteamId()) return;
            if (newItem != null && newItem.text != null && HasPerks(newItem.text))
            {
                if (config.playerSettings.only_show_when_equipped)
                    SendInspectMenuButton(player);
                
                List<Perk> perks = Pool.GetList<Perk>();
                PlayerPerks pp;
                if (Player_perks.TryGetValue(player.userID, out pp) && pp != null && pp.perk_modifiers != null)
                    perks.AddRange(pp.perk_modifiers.Keys);

                var weaponPerks = GetMods(newItem.text).Keys;
                foreach (var perk in weaponPerks)
                    if (!perks.Contains(perk))
                        perks.Add(perk);

                HandlePlayerSubscription(player, perks);
                Pool.FreeList(ref perks);
            }

            else if ((oldItem != null && oldItem.text != null && HasPerks(oldItem.text)) || (newItem == null && oldItem == null))
            {
                List<Perk> perks = Pool.GetList<Perk>();
                PlayerPerks pp;
                if (Player_perks.TryGetValue(player.userID, out pp) && pp != null && pp.perk_modifiers != null) 
                    perks.AddRange(pp.perk_modifiers.Keys);

                if (perks.Count == 0 && config.playerSettings.only_show_when_equipped) 
                    CuiHelper.DestroyUi(player, "InspectorButtonImg");

                HandlePlayerSubscription(player, perks);
                Pool.FreeList(ref perks);
            }            
        }

        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (container == null || item == null) return;
            var player = container.GetOwnerPlayer();
            if (player == null || player.IsNpc || !player.userID.IsSteamId() || !permission.UserHasPermission(player.UserIDString, perm_use) || !player.IsConnected) return;
            if (!HasPerks(item.text)) return;
            if (player.inventory.containerWear == container)
                GetPlayerPerks(player);
        }

        void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {            
            var player = container.GetOwnerPlayer();
            if (player == null || player.IsNpc || !player.userID.IsSteamId() || !permission.UserHasPermission(player.UserIDString, perm_use) || !player.IsConnected) return;
            if (item == null || !HasPerks(item.text)) return;
            if (player.inventory.containerWear == container)
                GetPlayerPerks(player);
        }

        object OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            if (!oven.IsOn())
            {
                Dictionary<Perk, float> perks = GetCombinedMods(player);
                
                if (perks == null) return null;

                float value;
                if (perks.TryGetValue(Perk.Smelter, out value))
                {
                    if (!OvenWatchList.ContainsKey(oven)) OvenWatchList.Add(oven, value);
                    else OvenWatchList[oven] = value;
                }

                WipeDictionary(perks);
            }
            return null;
        }

        Dictionary<BaseOven, float> OvenWatchList = new Dictionary<BaseOven, float>();

        object OnOvenStart(BaseOven oven)
        {
            float value;
            if (!OvenWatchList.TryGetValue(oven, out value)) return null;            

            NextTick(() =>
            {
                if (oven.FindBurnable() != null)
                {
                    oven.inventory.temperature = 1000f;
                    oven.UpdateAttachmentTemperature();
                    float repeatingValue = Math.Max(0.5f - (0.5f * value), 0.001f);
                    oven.InvokeRepeating(oven.Cook, repeatingValue, repeatingValue);
                    oven.SetFlag(BaseEntity.Flags.On, b: true);
                }
            });

            return true;
        }

        object OnPlayerAddModifiers(BasePlayer player, Item item, ItemModConsumable consumable)
        {
            Dictionary<Perk, float> perks = GetCombinedMods(player);
            if (perks == null) return null;

            float value;
            if (perks.TryGetValue(Perk.Sated, out value))
            {
                var gain = consumable.GetIfType(MetabolismAttribute.Type.Calories);
                if (gain > 0) player.metabolism.calories.value += value * gain;
                gain = consumable.GetIfType(MetabolismAttribute.Type.Hydration);
                if (gain > 0) player.metabolism.hydration.value += value * gain;
            }

            if (perks.TryGetValue(Perk.IronStomach, out value) && RollSuccessful(value))
            {
                if (consumable.GetIfType(MetabolismAttribute.Type.Poison) > 0)
                    player.metabolism.poison.SetValue(player.metabolism.FindAttribute(MetabolismAttribute.Type.Poison).lastValue);

                if (consumable.GetIfType(MetabolismAttribute.Type.Hydration) < 0)
                    player.metabolism.hydration.SetValue(player.metabolism.FindAttribute(MetabolismAttribute.Type.Hydration).lastValue);
            }

            return null;
        }

        void OnFishCatch(Item item, BaseFishingRod rod, BasePlayer player)
        {
            float value = GetTotalPerkMod(Perk.FishingLuck, player);
            if (value <= 0) return;

            if (RollSuccessful(value))
            {
                var comp = CreateRandomComponent(player, Perk.FishingLuck);
                if (comp != null)
                {
                    string message = string.Format(lang.GetMessage("FishLuckMessage", this, player.UserIDString), comp.amount > 1 ? lang.GetMessage("some", this, player.UserIDString) : lang.GetMessage("a", this, player.UserIDString), comp.info.displayName.english);
                    if (config.playerSettings.uinotify_settings.notify_FishingLuck) SendUINotify(player, message);
                    PrintToChat(player, message);
                    player.GiveItem(comp);
                }
            }
        }

        void OnLoseCondition(Item item, ref float amount)
        {
            var player = item.GetOwnerPlayer();
            if (player == null || player.IsNpc || !player.userID.IsSteamId() || config.enhancementSettings.perk_config.DurabilitySettings.durability_blacklist.Contains(item.info.shortname)) return;

            float value = GetTotalPerkMod(Perk.Durable, player);
            float fix_value = amount * value;

            if (fix_value > 0)
            {
                if (item.condition - fix_value > item.maxCondition) NextTick(() =>
                {
                    if (item != null) item.condition += fix_value;
                });
                else item.condition += fix_value;
            }
        }

        object OnItemCraft(ItemCraftTask task, BasePlayer player, Item fromTempBlueprint)
        {
            float value = GetTotalPerkMod(Perk.Manufacture, player);
            if (value <= 0) return null;

            var craftingTime = task.blueprint.time;
            var reducedTime = Math.Max(craftingTime - (craftingTime * value), 0.01f);
            if (!task.blueprint.name.Contains("(Clone)"))
                task.blueprint = UnityEngine.Object.Instantiate(task.blueprint);
            task.blueprint.time = reducedTime;

            return null;
        }

        float GetModifiedTime(BasePlayer player, ItemCraftTask task)
        {
            var workbenchLevel = player.currentCraftLevel;

            if (workbenchLevel == 0) return task.blueprint.time;
            var diff = workbenchLevel - task.blueprint.workbenchLevelRequired;
            if (diff < 0.5) return task.blueprint.time;
            else if (diff < 1.5) return task.blueprint.time / 2;
            else return task.blueprint.time / 4;
        }

        void OnPlayerRevive(BasePlayer reviver, BasePlayer player)
        {
            if (reviver == null || player == null) return;
            float value = GetTotalPerkMod(Perk.Paramedic, reviver);
            if (value <= 0) return;

            BasePlayer revived_player = player;
            NextTick(() =>
            {
                if (revived_player == null) return;
                var healFor = 100 * value;
                Unsubscribe(nameof(OnPlayerHealthChange));
                player.Heal(healFor);
                Subscribe(nameof(OnPlayerHealthChange));
            });
        }

        float GetModifiedCraftChance(BasePlayer player, float chance)
        {
            var result = 0f;
            foreach (var kvp in config.enhancementSettings.chance_modifier_settings.craft_chance_permissions)
            {
                if (permission.UserHasPermission(player.UserIDString, kvp.Key) && result < kvp.Value) result = kvp.Value;
            }

            return chance + (result * chance);
        }

        Dictionary<BasePlayer, float> CraftCooldowns = new Dictionary<BasePlayer, float>();
        bool HasCraftCooldown(BasePlayer player)
        {
            if (config.enhancementSettings.lootSettings.craft_success_cooldown == 0) return false;
            if (!CraftCooldowns.ContainsKey(player))
            {
                CraftCooldowns.Add(player, Time.time + config.enhancementSettings.lootSettings.craft_success_cooldown);
                return false;
            }
            if (CraftCooldowns[player] > Time.time) return true;
            CraftCooldowns[player] = Time.time + config.enhancementSettings.lootSettings.craft_success_cooldown;
            return false;
        }

        void OnItemCraftFinished(ItemCraftTask task, Item item, ItemCrafter crafter)
        {
            if (crafter == null) return;
            var player = crafter.owner;
            if (player == null) return;
            if (task.blueprint == null)
            {
                return;
            }
            var chance = GetModifiedCraftChance(player, config.enhancementSettings.lootSettings.craft_chance);

            if (permission.UserHasPermission(player.UserIDString, perm_craft) && EnhanceableItems.Contains(item.info.shortname) && UnityEngine.Random.Range(0f, 100f) >= 100 - chance && !config.enhancementSettings.lootSettings.craft_blacklist.Contains(item.info.shortname) && !HasCraftCooldown(player))
            {
                ReplaceItem(player, item);
            }

            Dictionary<Perk, float> perks = GetCombinedMods(player);
            if (perks == null) return;

            float value;

            if (perks.TryGetValue(Perk.Thrifty, out value) && RollSuccessful(value))
            {
                var refunded = 0;
                ItemBlueprint bp;
                if (item_BPs.TryGetValue(item.info.shortname, out bp))
                {
                    foreach (var component in bp.ingredients)
                    {
                        if (config.enhancementSettings.perk_config.thriftySettings.blacklist_shortnames.Contains(component.itemDef.shortname)) continue;
                        var nitem = ItemManager.CreateByName(component.itemDef.shortname, Convert.ToInt32(component.amount));
                        if (nitem == null) continue;
                        player.GiveItem(nitem);
                        refunded++;
                    }
                    if (refunded > 0)
                    {
                        var message = lang.GetMessage("ComponentsRefundedMessage", this, player.UserIDString);
                        if (config.playerSettings.uinotify_settings.notify_ComponentRefund) SendUINotify(player, message);
                        PrintToChat(player, message);
                    }
                }
            }

            if (perks.TryGetValue(Perk.Fabricate, out value) && RollSuccessful(value) && !config.enhancementSettings.perk_config.fabricateSettings.blacklist_shortnames.Contains(item.info.shortname))
            {
                var ditem = ItemManager.CreateByName(item.info.shortname, item.amount, item.skin);
                if (ditem != null)
                {
                    ditem.name = item.name;
                    ditem.text = item.text;

                    var message = string.Format(lang.GetMessage("ItemDuplicatedMessage", this, player.UserIDString), item.info.displayName.english);
                    if (config.playerSettings.uinotify_settings.notify_ItemDuplication) SendUINotify(player, message);
                    PrintToChat(player, message);

                    player.GiveItem(ditem);
                }
            }

            if (task.amount < 1 && task.blueprint != null && task.blueprint.name.Contains("(Clone)"))
            {
                var behaviours = task.blueprint.GetComponents<MonoBehaviour>();
                foreach (var behaviour in behaviours)
                {
                    if (behaviour.name.Contains("(Clone)"))
                    {
                        UnityEngine.Object.Destroy(behaviour);
                    }
                        
                }
            }

            WipeDictionary(perks);
        }

        void ReplaceItem(BasePlayer player, Item item)
        {
            var newItem = CreateItem(item.info.shortname, item.skin);
            if (newItem == null) return;

            item.name = newItem.name;
            item.text = newItem.text;

            item.skin = newItem.skin;
            var heldEnt = item.GetHeldEntity();
            if (heldEnt != null) heldEnt.skinID = item.skin;

            newItem.Remove();

            NotifyPlayer(player, item, false);
        }

        object CanCastFishingRod(BasePlayer player, BaseFishingRod fishingRod, Item lure)
        {
            float value = GetTotalPerkMod(Perk.LineStrength, player);
            if (value == 0) return null;

            fishingRod.GlobalStrainSpeedMultiplier = DefaultLineStrength - (DefaultLineStrength * value);

            return null;
        }

        void OnFishingStopped(BaseFishingRod fishingRod, BaseFishingRod.FailReason reason)
        {
            if (fishingRod.skinID > 0) return;
            fishingRod.GlobalStrainSpeedMultiplier = DefaultLineStrength;
        }

        object OnItemUse(Item item, int amountToUse)
        {
            if (Cooking != null && Cooking.IsLoaded && Convert.ToBoolean(Cooking.Call("IsCookingMeal", item))) return null;
            var player = item.GetOwnerPlayer();
            if (player == null) return null;
            float value = GetTotalPerkMod(Perk.Prepper, player);
            if (item.info.category != ItemCategory.Food || !RollSuccessful(value) || config.enhancementSettings.perk_config.prepperSettings.blacklist_shortnames.Contains(item.info.shortname)) return null;

            var refunded_item = ItemManager.CreateByName(item.info.shortname, amountToUse, item.skin);
            if (item.name != null) refunded_item.name = item.name;
            player.GiveItem(refunded_item);

            var message = string.Format(lang.GetMessage("ItemRationedMessage", this, player.UserIDString), item.name ?? item.info.displayName.english);
            if (config.playerSettings.uinotify_settings.notify_Rationed) SendUINotify(player, message);
            PrintToChat(player, message);

            return null;
        }

        void OnMealConsumed(BasePlayer player, Item item, int buff_duration)
        {
            float value = GetTotalPerkMod(Perk.Prepper, player);
            if (!RollSuccessful(value)) return;

            var refunded_item = ItemManager.CreateByName(item.info.shortname, 1, item.skin);
            if (item.name != null) refunded_item.name = item.name;

            player.GiveItem(refunded_item);

            PrintToChat(player, string.Format(lang.GetMessage("ItemRationedMessage", this, player.UserIDString), item.name ?? item.info.displayName.english));
        }

        object OnPayForUpgrade(BasePlayer player, BuildingBlock block, ConstructionGrade gradeTarget)
        {
            if (player == null || player.IsNpc || !player.userID.IsSteamId()) return null;
            float value = GetTotalPerkMod(Perk.Builder, player);
            if (value > 0 && RollSuccessful(value))
            {
                var message = string.Format(lang.GetMessage("FreeUpgradeMessage", this, player.UserIDString));
                if (config.playerSettings.uinotify_settings.notify_FreeUpgrade) SendUINotify(player, message);
                PrintToChat(player, message);
                return 0;
            }
            return null;
        }

        object OnPlayerHealthChange(BasePlayer player, float oldValue, float newValue)
        {
            if (newValue <= oldValue) return null;
            
            Dictionary<Perk, float> perks = GetCombinedMods(player);
            if (perks == null) return null;
            float value;
            float totalHealed = newValue - oldValue;
            if (perks.TryGetValue(Perk.Pharmaceutical, out value))
            {                
                var additionalHealth = (newValue - oldValue) * value;
                totalHealed += additionalHealth;
                Unsubscribe(nameof(OnPlayerHealthChange));
                player.Heal(additionalHealth);
                Subscribe(nameof(OnPlayerHealthChange));
            }

            if (perks.TryGetValue(Perk.HealShare, out value))
            {
                var healthToShare = totalHealed * value;
                List<BasePlayer> teamMembers = Pool.GetList<BasePlayer>();
                teamMembers.AddRange(FindEntitiesOfType<BasePlayer>(player.transform.position, config.enhancementSettings.perk_config.healShareSettings.distance));
                Unsubscribe(nameof(OnPlayerHealthChange));

                foreach (var hit in teamMembers)
                {
                    if (hit == player || hit.IsNpc) continue;
                    if (!config.enhancementSettings.perk_config.healShareSettings.team_only) hit.Heal(healthToShare);
                    else if (hit.Team != null && player.Team != null && player.Team.teamID == hit.Team.teamID) hit.Heal(healthToShare);
                }

                Pool.FreeList(ref teamMembers);
                Subscribe(nameof(OnPlayerHealthChange));
            }

            WipeDictionary(perks);
            return null;
        }

        Dictionary<Recycler, BasePlayer> RecyclerPlayers = new Dictionary<Recycler, BasePlayer>();

        private void OnRecyclerToggle(Recycler recycler, BasePlayer player)
        {
            if (recycler.IsOn())
            {
                RecyclerPlayers.Remove(recycler);
                return;
            }

            if (player == null) return;

            if (config.enhancementSettings.enhancement_kit_settings.recycle_settings.perk_kit_chance > 0)
            {
                RecyclerPlayers[recycler] = player;
            }

            float value = GetTotalPerkMod(Perk.Environmentalist, player);
            if (value == 0) return;

            float modifiedSpeed = 5 - (5 * value);
            if (modifiedSpeed < 0.01) modifiedSpeed = 0.01f;

            recycler.CancelInvoke(nameof(recycler.RecycleThink));
            timer.Once(0.1f, () => recycler.InvokeRepeating(recycler.RecycleThink, modifiedSpeed - 0.005f, modifiedSpeed));
        }

        void OnItemRecycle(Item item, Recycler recycler)
        {
            if (string.IsNullOrEmpty(item.text) || !HasPerks(item.text)) return;
            var perks = GetPerkCount(item.text);
            if (perks == null) return;
            BasePlayer player;
            if (!RecyclerPlayers.TryGetValue(recycler, out player) || !permission.UserHasPermission(player.UserIDString, perm_recycle)) return;
            if (player == null || !player.IsConnected)
            {
                RecyclerPlayers.Remove(recycler);
                return;
            }
            float chance = GetModifiedRecyclerChanceTarget(player, config.enhancementSettings.enhancement_kit_settings.recycle_settings.perk_kit_chance);
            foreach (var kvp in perks)
            {                
                var roll = UnityEngine.Random.Range(0f, 100f);
                if (roll >= 100 - chance)
                {
                    var kit = CreateEnhancementKit(kvp.Key);
                    //LogToFile("ItemPerk_EnhancementKitsCreated", $"Created a kit using perk: {kvp.Key}. Kit name: {kit.name ?? "null"}", this);
                    if (!kit.MoveToContainer(recycler.inventory)) kit.DropAndTossUpwards(recycler.transform.position);
                    if (!string.IsNullOrEmpty(config.enhancementSettings.enhancement_kit_settings.recycle_settings.success_effect)) RunEffect(player, config.enhancementSettings.enhancement_kit_settings.recycle_settings.success_effect, recycler.transform.position);
                }
            }
            WipeDictionary(perks);
        }

        float GetModifiedRecyclerChanceTarget(BasePlayer player, float chance)
        {
            var result = 0f;
            foreach (var kvp in config.enhancementSettings.chance_modifier_settings.recycle_chance_permissions)
            {
                if (permission.UserHasPermission(player.UserIDString, kvp.Key) && result < kvp.Value) result = kvp.Value;
            }

            return chance + (result * chance);
        }

        object OnResearchCostDetermine(Item item)
        {
            ResearchTable researchTable = item.GetEntityOwner() as ResearchTable;
            if (researchTable == null) return null;
            var player = researchTable.user;
            if (player == null) return null;

            float value = GetTotalPerkMod(Perk.Academic, player);
            if (value == 0 || !RollSuccessful(value)) return null;

            PrintToChat(player, lang.GetMessage("ResearchRefundedMessage", this, player.UserIDString));
            return 0;
        }

        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null || player.IsNpc || !player.userID.IsSteamId()) return;
            ClearPerks(player);
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            ClearPerks(player, true);
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, perm_use)) GetPlayerPerks(player);
        }

        void ClearPerks(BasePlayer player, bool removeKey = false)
        {
            if (removeKey) Player_perks.Remove(player.userID);
            else
            {
                PlayerPerks perkData;
                if (Player_perks.TryGetValue(player.userID, out perkData))
                {
                    perkData.perk_modifiers.Clear();
                }
            }

            if (HasRegen(player)) DestroyRegen(player);

            List<Perk> perks = Pool.GetList<Perk>();
            HandlePlayerSubscription(player, perks);
            Pool.FreeList(ref perks);
        }

        void OnCollectiblePickup(CollectibleEntity entity, BasePlayer player)
        {
            if (player == null || entity == null || player.IsNpc || !player.userID.IsSteamId() || entity.itemList == null) return;
            float value = GetTotalPerkMod(Perk.Forager, player);
            if (value > 0)
            {
                foreach (var item in entity.itemList)
                {
                    if (item == null) continue;
                    item.amount += Convert.ToInt32(Math.Round(value * item.amount, 0, MidpointRounding.AwayFromZero));
                }
            }
        }

        void OnGrowableGathered(GrowableEntity plant, Item item, BasePlayer player)
        {
            if (player == null || plant == null || player.IsNpc || !player.userID.IsSteamId()) return;
            float value = GetTotalPerkMod(Perk.Horticulture, player);
            item.amount += Convert.ToInt32(Math.Round(item.amount * value, 0, MidpointRounding.AwayFromZero));
        }

        void CanCatchFish(BasePlayer player, BaseFishingRod fishingRod, Item fish)
        {
            if (player == null || player.IsNpc || !player.userID.IsSteamId()) return;

            float value = GetTotalPerkMod(Perk.Angler, player);
            if (value > 0)
            {
                int extraFish = Math.Max(Convert.ToInt32(Math.Round(value, 0, MidpointRounding.ToEven)), 0);
                var luck = value - extraFish;
                if (RollSuccessful(luck)) extraFish++;

                fish.amount += extraFish;
            }            
        }

        void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            HandleDispenser(dispenser, player, item, false);
        }

        void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            HandleDispenser(dispenser, player, item, true);
        }

        void HandleDispenser(ResourceDispenser dispenser, BasePlayer player, Item item, bool finalHit)
        {
            if (player == null || dispenser == null || item == null) return;
            Dictionary<Perk, float> perks = GetCombinedMods(player);
            if (perks == null) return;

            float value;
            if (dispenser.gatherType == ResourceDispenser.GatherType.Tree)
            {
                if (perks.TryGetValue(Perk.Lumberjack, out value)) HandleYield(item, value);
                if (finalHit && perks.TryGetValue(Perk.TreePlanter, out value) && RollSuccessful(value)) RespawnResource(dispenser);
                if (!DoingClearOut && finalHit && perks.TryGetValue(Perk.Deforest, out value) && RollSuccessful(value))
                {
                    var trees = FindEntitiesOfType<BaseEntity>(dispenser.baseEntity.transform.position, config.enhancementSettings.perk_config.deforestSettings.deforest_max_radius);
                    List<ResourceDispenser> dispensers = Pool.GetList<ResourceDispenser>();
                    foreach (var entity in trees)
                    {
                        if (entity == dispenser.baseEntity) continue;
                        if (entity.PrefabName.StartsWith("assets/bundled/prefabs/autospawn/resource/v3_") || entity.PrefabName.StartsWith("assets/bundled/prefabs/autospawn/resource/swamp"))
                        {
                            dispensers.Add(entity.GetComponent<ResourceDispenser>());
                        }
                    }
                    Pool.FreeList(ref trees);
                    DoingClearOut = true;
                    Subscribe(nameof(STCanGainXP));
                    foreach (var tree in dispensers)
                    {
                        if (tree == null) continue;
                        HandleDispenserClearOut(player, tree);
                    }
                    Unsubscribe(nameof(STCanGainXP));
                    Pool.FreeList(ref dispensers);
                    DoingClearOut = false;
                }
                if (finalHit && perks.TryGetValue(Perk.WoodcuttingLuck, out value) && RollSuccessful(value))
                {
                    var comp = CreateRandomComponent(player, Perk.WoodcuttingLuck);
                    if (comp != null)
                    {
                        var message = string.Format(lang.GetMessage("WoodcuttingLuckMessage", this, player.UserIDString), comp.amount > 1 ? lang.GetMessage("some", this, player.UserIDString) : lang.GetMessage("a", this, player.UserIDString), comp.info.displayName.english);
                        if (config.playerSettings.uinotify_settings.notify_WoodcuttingLuck) SendUINotify(player, message);
                        PrintToChat(player, message);
                        player.GiveItem(comp);                        
                    }
                        
                }
            }
            else if (dispenser.gatherType == ResourceDispenser.GatherType.Ore)
            {
                if (perks.TryGetValue(Perk.Prospector, out value)) HandleYield(item, value);
                if (finalHit && perks.TryGetValue(Perk.RockCycler, out value) && RollSuccessful(value)) RespawnResource(dispenser);
                if (!DoingClearOut && finalHit && perks.TryGetValue(Perk.BlastMine, out value) && RollSuccessful(value))
                {
                    var nodes = FindEntitiesOfType<BaseEntity>(dispenser.baseEntity.transform.position, config.enhancementSettings.perk_config.blastMineSettings.blastmine_max_radius);
                    List<ResourceDispenser> dispensers = Pool.GetList<ResourceDispenser>();
                    foreach (var entity in nodes)
                    {
                        if (entity == dispenser.baseEntity) continue;
                        if (entity.ShortPrefabName.Equals("stone-ore") || entity.ShortPrefabName.Equals("sulfur-ore") || entity.ShortPrefabName.Equals("metal-ore"))
                        {
                            dispensers.Add(entity.GetComponent<ResourceDispenser>());
                        }
                    }
                    Pool.FreeList(ref nodes);
                    DoingClearOut = true;
                    foreach (var node in dispensers)
                    {
                        if (node == null) continue;
                        HandleDispenserClearOut(player, node);
                    }
                    Pool.FreeList(ref dispensers);
                    DoingClearOut = false;
                }
                if (finalHit && perks.TryGetValue(Perk.MiningLuck, out value) && RollSuccessful(value))
                {
                    var comp = CreateRandomComponent(player, Perk.MiningLuck);
                    if (comp != null)
                    {
                        var message = string.Format(lang.GetMessage("MiningLuckMessage", this, player.UserIDString), comp.amount > 1 ? lang.GetMessage("some", this, player.UserIDString) : lang.GetMessage("a", this, player.UserIDString), comp.info.displayName.english);
                        if (config.playerSettings.uinotify_settings.notify_MiningLuck) SendUINotify(player, message);
                        PrintToChat(player, message);
                        player.GiveItem(comp);
                    }
                        
                }
            }
            else if (dispenser.gatherType == ResourceDispenser.GatherType.Flesh)
            {
                finalHit = true;
                foreach (var x in dispenser.containedItems)
                {
                    if (x.amount > 0)
                    {
                        finalHit = false;
                        break;
                    }
                }
                if (perks.TryGetValue(Perk.Butcher, out value)) HandleYield(item, value);
                if (!DoingClearOut && finalHit && perks.TryGetValue(Perk.Tanner, out value) && RollSuccessful(value))
                {
                    var nodes = FindEntitiesOfType<BaseCorpse>(dispenser.baseEntity.transform.position, config.enhancementSettings.perk_config.tannerSettings.tanner_max_radius);
                    List<ResourceDispenser> dispensers = Pool.GetList<ResourceDispenser>();
                    foreach (var entity in nodes)
                    {
                        if (entity == dispenser.baseEntity) continue;
                        dispensers.Add(entity.GetComponent<ResourceDispenser>());
                    }
                    Pool.FreeList(ref nodes);
                    DoingClearOut = true;
                    foreach (var node in dispensers)
                    {
                        if (node == null) continue;
                        HandleDispenserClearOut(player, node);
                    }
                    Pool.FreeList(ref dispensers);
                    DoingClearOut = false;
                }
                if (finalHit && perks.TryGetValue(Perk.SkinningLuck, out value) && RollSuccessful(value))
                {
                    var comp = CreateRandomComponent(player, Perk.SkinningLuck);
                    if (comp != null)
                    {
                        var message = string.Format(lang.GetMessage("SkinningLuckMessage", this, player.UserIDString), comp.amount > 1 ? lang.GetMessage("some", this, player.UserIDString) : lang.GetMessage("a", this, player.UserIDString), comp.info.displayName.english);
                        if (config.playerSettings.uinotify_settings.notify_SkinningLuck) SendUINotify(player, message);
                        PrintToChat(player, message);
                        player.GiveItem(comp);
                    }                        
                }
            }
            // Clears our dictionary from memory
            WipeDictionary(perks);
        }

        Item CreateRandomComponent(BasePlayer player, Perk perk)
        {
            Dictionary<string, DropInfo> dict = null;
            switch (perk)
            {
                case Perk.WoodcuttingLuck:
                    dict = config.enhancementSettings.perk_config.woodcuttingLuckSettings.drop_rates;
                    break;
                case Perk.MiningLuck:
                    dict = config.enhancementSettings.perk_config.miningLuckSettings.drop_rates;
                    break;
                case Perk.SkinningLuck:
                    dict = config.enhancementSettings.perk_config.skinningLuckSettings.drop_rates;
                    break;
                case Perk.FishingLuck:
                    dict = config.enhancementSettings.perk_config.fishingLuckSettings.drop_rates;
                    break;
            }

            if (dict == null) return null;

            List<KeyValuePair<string, DropInfo>> keys = Pool.GetList<KeyValuePair<string, DropInfo>>();
            keys.AddRange(dict);
            var chosen = keys.GetRandom();
            Pool.FreeList(ref keys);

            var result = ItemManager.CreateByName(chosen.Key, UnityEngine.Random.Range(chosen.Value.min_amount, chosen.Value.max_amount), chosen.Value.skin);
            if (result != null && !string.IsNullOrEmpty(chosen.Value.display_name)) result.name = chosen.Value.display_name;
            return result;
        }

        void RespawnResource(ResourceDispenser dispenser)
        {
            string prefab = dispenser.baseEntity.PrefabName;
            Vector3 pos = dispenser.baseEntity.transform.position;
            Quaternion rot = dispenser.baseEntity.transform.rotation;
            timer.Once(0.5f, () =>
            {
                var nodes = FindEntitiesOfType<ResourceEntity>(pos, 2);
                foreach (var node in nodes)
                {
                    if (node.PrefabName != prefab) continue;
                    if (InRange(node.transform.position, pos, 0.1f))
                    {
                        Pool.FreeList(ref nodes);
                        return;
                    }
                }
                Pool.FreeList(ref nodes);

                var entity = GameManager.server.CreateEntity(prefab, pos, rot);
                entity.Spawn();
            });
        }

        private static bool InRange(Vector3 a, Vector3 b, float distance)
        {
            return (a - b).sqrMagnitude <= distance * distance;
        }

        #region Stack management

        static bool IsEnhancementKit(Item item) => item.text != null && parsedEnums.ContainsKey(item.text);

        Item OnItemSplit(Item item, int amount)
        {
            if (item.text != null && parsedEnums.ContainsKey(item.text))
            {
                var newItem = ItemManager.CreateByItemID(item.info.itemid);

                item.amount -= amount;
                newItem.name = item.name;
                newItem.text = item.text;
                newItem.skin = item.skin;
                newItem.amount = amount;
                newItem.MarkDirty();

                item.MarkDirty();
                return newItem;
            }
            return null;
        }


        #endregion

        #region OnEntityTakeDamage

        void OnEntityTakeDamage(BaseAnimalNPC animal, HitInfo info)
        {
            if (animal == null || info == null || info.InitiatorPlayer == null || info.InitiatorPlayer.IsNpc || !info.InitiatorPlayer.userID.IsSteamId()) return;
            var perks = GetCombinedMods(info.InitiatorPlayer);
            if (perks == null) return;

            float value;
            if (perks.TryGetValue(Perk.BeastBane, out value))
            {
                info.damageTypes.ScaleAll(1 + value);
            }
                         
            if (config.enhancementSettings.perk_config.vampiricSettings.vampiric_entities.Contains("animal") && perks.TryGetValue(Perk.Vampiric, out value))
            {
                info.InitiatorPlayer.Heal(info.damageTypes.Total() * value);
            }

            WipeDictionary(perks);
        }

        void OnEntityTakeDamage(SimpleShark shark, HitInfo info)
        {
            if (shark == null || info == null || info.InitiatorPlayer == null || info.InitiatorPlayer.IsNpc || !info.InitiatorPlayer.userID.IsSteamId()) return;

            float value = GetTotalPerkMod(Perk.SharkBane, info.InitiatorPlayer);
            info.damageTypes.ScaleAll(1 + value);
        }

        void OnEntityTakeDamage(BasePlayer victim, HitInfo info)
        {
            if (info == null || victim == null) return;
            var attackerPerks = info.InitiatorPlayer != null && !info.InitiatorPlayer.IsNpc ? GetCombinedMods(info.InitiatorPlayer) : null;
            Dictionary<Perk, float> defenderPerks = null;
            if (!victim.IsNpc) defenderPerks = GetCombinedMods(victim);

            if (attackerPerks == null && defenderPerks == null) return;

            float value = 0;
            float modifier = 1;
            if (victim.IsNpc || !victim.UserIDString.IsSteamId())
            {
                if (attackerPerks != null)
                {
                    if ((!config.enhancementSettings.perk_config.ScientistBaneSettings.scientist_only || victim is ScientistNPC) && attackerPerks.TryGetValue(Perk.ScientistBane, out value) && value > 0) modifier += value;
                    if (config.enhancementSettings.perk_config.vampiricSettings.vampiric_entities.Contains("scientist") && attackerPerks.TryGetValue(Perk.Vampiric, out value)) info.InitiatorPlayer.Heal(info.damageTypes.Total() * value);
                }
                info.damageTypes.ScaleAll(modifier);
                return;
            }
            float AttackerHasVampiric = 0;
            // victim is a real player
            if (defenderPerks != null && defenderPerks.Count > 0)
            {
                if (defenderPerks.ContainsKey(Perk.Regeneration) && config.enhancementSettings.perk_config.regenerationSettings.damage_delay > 0)
                    AddRegenDelay(victim);
                var damageType = info.damageTypes.GetMajorityDamageType();
                switch (damageType)
                {
                    case Rust.DamageType.Cold:
                    case Rust.DamageType.ColdExposure:
                    case Rust.DamageType.Heat:
                        modifier -= HandlePerkDamage(victim, Perk.Elemental, defenderPerks);
                        if (modifier <= 0) victim.metabolism.temperature.SetValue(25);
                        info.damageTypes.ScaleAll(modifier);
                        return;

                    case Rust.DamageType.Radiation:
                        modifier -= HandlePerkDamage(victim, Perk.Lead, defenderPerks);
                        if (modifier <= 0)
                        {
                            victim.metabolism.radiation_level.SetValue(0);
                            victim.metabolism.radiation_poison.SetValue(0);
                        }
                        info.damageTypes.ScaleAll(modifier);
                        return;

                    case Rust.DamageType.Fall:
                        modifier -= HandlePerkDamage(victim, Perk.FallDamage, defenderPerks);
                        info.damageTypes.ScaleAll(modifier);
                        return;
                    case Rust.DamageType.AntiVehicle:
                    case Rust.DamageType.Explosion:
                        modifier -= HandlePerkDamage(victim, Perk.FlakJacket, defenderPerks);
                        break;
                    case Rust.DamageType.Blunt:
                        if (info.WeaponPrefab != null && IsBluntExplosive(info.WeaponPrefab.ShortPrefabName)) modifier -= HandlePerkDamage(victim, Perk.FlakJacket, defenderPerks);
                        break;

                    case Rust.DamageType.Stab:
                        if (info.WeaponPrefab != null && (info.WeaponPrefab.ShortPrefabName == "grenade.beancan.deployed" || info.WeaponPrefab.ShortPrefabName == "explosive.satchel.deployed")) modifier -= HandlePerkDamage(victim, Perk.FlakJacket, defenderPerks);
                        break;                    

                }
                var animalInitiator = info.Initiator as BaseAnimalNPC;
                if (animalInitiator != null)
                {
                    if (defenderPerks.TryGetValue(Perk.BeastWard, out value)) modifier -= value;
                }

                var sharkInitiator = info.Initiator as SimpleShark;
                if (sharkInitiator != null)
                {
                    if (defenderPerks.TryGetValue(Perk.SharkWard, out value)) modifier -= value;
                }

                var scientistInitiator = info.Initiator as ScientistNPC;
                if (scientistInitiator != null)
                {
                    if (defenderPerks.TryGetValue(Perk.ScientistWard, out value)) modifier -= value;
                }

                if (info.InitiatorPlayer != null)
                {
                    var heldEntity = info.InitiatorPlayer.GetHeldEntity();
                    if (defenderPerks.TryGetValue(Perk.MeleeWard, out value) && heldEntity != null && heldEntity is BaseMelee && (damageType == Rust.DamageType.Slash || damageType == Rust.DamageType.Stab || damageType == Rust.DamageType.Blunt)) modifier -= value;
                    if (!info.InitiatorPlayer.IsNpc && info.InitiatorPlayer.userID.IsSteamId())
                    {
                        // Being attacked by real player
                        if (config.enhancementSettings.perk_config.vampiricSettings.vampiric_entities.Contains("player") && attackerPerks != null && attackerPerks.TryGetValue(Perk.Vampiric, out value)) AttackerHasVampiric = value;
                    }
                }                
            }
            if (attackerPerks != null)
            {
                // Attacker is real
                // Add attacker perks in here
            }
            if (defenderPerks != null && defenderPerks.TryGetValue(Perk.UncannyDodge, out value) && RollSuccessful(value))
            {
                var message = lang.GetMessage("DodgeMessage", this, victim.UserIDString);
                if (config.playerSettings.uinotify_settings.notify_Dodge) SendUINotify(victim, message);
                PrintToChat(victim, message);
                modifier = 0;
            }
            if (modifier < 0) modifier = 0;
            if (modifier != 1)
            {                
                info.damageTypes.ScaleAll(modifier);
            }
            if (AttackerHasVampiric > 0)
            {
                info.InitiatorPlayer.Heal(info.damageTypes.Total() * AttackerHasVampiric);
            }
            WipeDictionary(attackerPerks);
            WipeDictionary(defenderPerks);
        }

        bool IsBluntExplosive(string shortname)
        {
            switch (shortname)
            {
                case "grenade.f1.deployed":
                case "40mm_grenade_he":
                case "grenade.flashbang.deployed":
                case "maincannonshell":
                case "rocket_heli_napalm":
                    return true;
                default: return false;
            }
        }

        void OnEntityTakeDamage(BaseVehicle vehicle, HitInfo info)
        {
            if (info == null || info.damageTypes == null || vehicle == null) return;
            var player = vehicle.GetDriver();
            if (player == null) return;

            var value = GetTotalPerkMod(Perk.Reinforced, player);
            if (value == 0) return;

            info.damageTypes.ScaleAll(Math.Max(1 - value, 0));                        
        }

        void OnEntityTakeDamage(LootContainer entity, HitInfo info)
        {
            if (entity == null || info == null || info.InitiatorPlayer == null || info.InitiatorPlayer.IsNpc || !info.InitiatorPlayer.userID.IsSteamId()) return;

            bool entityWillDie = info?.damageTypes?.Total() >= entity?.health;

            if (entityWillDie)
                HandleItemRoll(info.InitiatorPlayer, entity);

            var perks = GetCombinedMods(info.InitiatorPlayer);
            if (perks == null || perks.Count == 0) return;

            float value;
            if (perks.TryGetValue(Perk.Smasher, out value) && RollSuccessful(value))
            {
                info.damageTypes.ScaleAll(100f);
                entityWillDie = true;
            }

            if (entityWillDie)
            {
                if (perks.TryGetValue(Perk.Scavenger, out value) && RollSuccessful(value)) AddScrapToContainer(entity);
                if (perks.TryGetValue(Perk.ComponentLuck, out value) && RollSuccessful(value)) AddComponentsToContainer(entity);
                if (perks.TryGetValue(Perk.ElectronicsLuck, out value) && RollSuccessful(value)) AddElectronicsToContainer(entity);
            }

            WipeDictionary(perks);
        }

        void OnEntityTakeDamage(BradleyAPC apc, HitInfo info)
        {
            if (apc == null || info == null || info.InitiatorPlayer == null || info.InitiatorPlayer.IsNpc || !info.InitiatorPlayer.userID.IsSteamId()) return;

            float value = GetTotalPerkMod(Perk.BradleyDamage, info.InitiatorPlayer);
            info.damageTypes.ScaleAll(1 + value);
        }

        void OnEntityTakeDamage(PatrolHelicopter heli, HitInfo info)
        {
            if (heli == null || info == null || info.InitiatorPlayer == null || info.InitiatorPlayer.IsNpc || !info.InitiatorPlayer.userID.IsSteamId()) return;

            float value = GetTotalPerkMod(Perk.HeliDamage, info.InitiatorPlayer);
            info.damageTypes.ScaleAll(1 + value);
        }

        #endregion

        List<LootContainer> LootedContainers = new List<LootContainer>();

        void CanLootEntity(BasePlayer player, LootContainer container)
        {
            if (player == null || container == null || LootedContainers.Contains(container)) return;
            LootedContainers.Add(container);

            HandleItemRoll(player, container);

            var perks = GetCombinedMods(player);
            if (perks == null || perks.Count == 0) return;

            float value;
            if (perks.TryGetValue(Perk.Scavenger, out value) && RollSuccessful(value)) AddScrapToContainer(container);
            if (perks.TryGetValue(Perk.ComponentLuck, out value) && RollSuccessful(value)) AddComponentsToContainer(container);
            if (perks.TryGetValue(Perk.ElectronicsLuck, out value) && RollSuccessful(value)) AddElectronicsToContainer(container);

            WipeDictionary(perks);
        }

        List<BasePlayer> workbench_looters = new List<BasePlayer>();

        void OnLootEntity(BasePlayer player, Workbench entity)
        {
            if (!permission.UserHasPermission(player.UserIDString, perm_enhance)) return;
            if (!workbench_looters.Contains(player)) workbench_looters.Add(player);
            SendWorkbenchButton(player);
        }

        void OnLootEntityEnd(BasePlayer player, Workbench entity)
        {
            workbench_looters.Remove(player);
            CloseEnhancementMenu(player);
            CuiHelper.DestroyUi(player, "WorkbenchEnhancementPanel");
        }


        float GetModifiedLootChanceTarget(BasePlayer player, float chance)
        {
            var result = 0f;
            foreach (var kvp in config.enhancementSettings.chance_modifier_settings.loot_chance_permissions)
            {
                if (permission.UserHasPermission(player.UserIDString, kvp.Key) && result < kvp.Value) result = kvp.Value;
            }

            return chance + (result * chance);
        }

        void HandleItemRoll(BasePlayer player, LootContainer container)
        {
            if (!permission.UserHasPermission(player.UserIDString, perm_loot)) return;
            if (container == null) return;

            float target;
            if (!config.enhancementSettings.lootSettings.container.TryGetValue(container.ShortPrefabName, out target) || target == 0) return;

            var roll = UnityEngine.Random.Range(0f, 100f);
            if (roll < 100 - GetModifiedLootChanceTarget(player, target)) return;

            if (!config.enhancementSettings.lootSettings.use_default_loot_profiles)
            {
                RollRandomItem(player, container);
                return;
            }

            List<string> lootProfile;

            if (!LootTables.TryGetValue(container.ShortPrefabName, out lootProfile) || lootProfile == null)
            {
                lootProfile = Pool.GetList<string>();
                if (container.lootDefinition != null) lootProfile.AddRange(GetLootFromTable(container.lootDefinition));
                else
                {
                    if (container.LootSpawnSlots.Length > 0)
                    {
                        LootContainer.LootSpawnSlot[] lootSpawnSlots = container.LootSpawnSlots;
                        foreach (var slot in lootSpawnSlots)
                        {
                            lootProfile.AddRange(GetLootFromTable(slot.definition));
                        }
                    }
                }
                if (lootProfile.Count == 0)
                {
                    Pool.FreeList(ref lootProfile);
                    return;
                }
                AddTable(container.ShortPrefabName, lootProfile);
            }

            if (!LootTables.TryGetValue(container.ShortPrefabName, out lootProfile)) return;

            string randomShortname = lootProfile.GetRandom();
            var item = CreateItem(randomShortname);
            if (item != null) AddEnhancedItemToContainer(player, item, container);
        }

        void RollRandomItem(BasePlayer player, LootContainer container)
        {
            var item = CreateItem();
            if (item != null) AddEnhancedItemToContainer(player, item, container);
        }

        void AddEnhancedItemToContainer(BasePlayer player, Item item, LootContainer container)
        {
            container.inventory.capacity++;
            container.inventorySlots++;
            if (item.MoveToContainer(container.inventory)) NotifyPlayer(player, item);
            else item.Remove();
        }

        void AddTable(string prefab, List<string> profile)
        {
            if (!LootTables.ContainsKey(prefab))
            {
                List<string> list = new List<string>();
                foreach (var shortname in profile)
                {
                    if (EnhanceableItems.Contains(shortname))
                    {
                        list.Add(shortname);
                    }
                }
                LootTables.Add(prefab, list);

                Pool.FreeList(ref profile);
            }                
        }

        void NotifyPlayer(BasePlayer player, Item item, bool found = true)
        {
            if (config.playerSettings.uinotify_settings.enabled && UINotify != null && UINotify.IsLoaded && permission.UserHasPermission(player.UserIDString, "uinotify.see") && ((config.playerSettings.uinotify_settings.notify_Crafted && false) || (config.playerSettings.uinotify_settings.notify_ItemDrops && true)))
                UINotify.Call("SendNotify", player.userID, config.playerSettings.uinotify_settings.messageType, lang.GetMessage("Notify_FirstFindLoot", this, player.UserIDString));

            var perks = GetMods(item.text);
            string message = lang.GetMessage("PerksMessage", this, player.UserIDString);
            foreach (var perk in perks)
            {
                message += string.Format(lang.GetMessage("ModsString", this, player.UserIDString), lang.GetMessage("UI" + perk.Key.ToString(), this, player.UserIDString));
            }
            WipeDictionary(perks);

            if (found || config.enhancementSettings.lootSettings.notify_craft_chat) PrintToChat(player, string.Format(lang.GetMessage(found ? "FoundItemMessage" : "CraftedItemMessage", this, player.UserIDString), item.name ?? lang.GetMessage("Enhanced", this, player.UserIDString) + " " + item.info.displayName.english, message));
        }

        void SendUINotify(BasePlayer player, string message)
        {
            if (!config.playerSettings.uinotify_settings.enabled || UINotify == null || !UINotify.IsLoaded) return;

            UINotify.Call("SendNotify", player.userID, config.playerSettings.uinotify_settings.messageType, message);
        }

        Dictionary<string, List<string>> LootTables = new Dictionary<string, List<string>>();

        List<string> GetLootFromTable(LootSpawn profile)
        {
            if (profile == null) return null;
            var result = Pool.GetList<string>();
            if (profile.subSpawn != null && profile.subSpawn.Length > 0)
            {
                foreach (var cat in profile.subSpawn)
                {
                    var list = GetLootFromTable(cat.category);
                    if (list != null)
                    {
                        result.AddRange(list);
                        Pool.FreeList(ref list);
                    }
                }
                return result;
            }
            if (profile.items != null && profile.items.Length > 0)
            {                
                result.AddRange(profile.items.Select(x => x.itemDef.shortname));
            }
            return result;
        }        

        void OnEntityDeath(LootContainer entity, HitInfo info)
        {
            if (entity == null || info == null) return;

            var perks = GetCombinedMods(info.InitiatorPlayer);
            if (perks == null || perks.Count == 0) return;

            float value;
            if (perks.TryGetValue(Perk.Attractive, out value) && RollSuccessful(value)) HandleLootPickup(info.InitiatorPlayer, entity as LootContainer);

            WipeDictionary(perks);
        }

        Dictionary<BasePlayer, bool> LastMagnetSuccess = new Dictionary<BasePlayer, bool>();

        void HandleLootPickup(BasePlayer player, LootContainer entity)
        {
            if (config.enhancementSettings.perk_config.attractiveSettings.max_dist > 0 && Vector3.Distance(player.transform.position, entity.transform.position) > config.enhancementSettings.perk_config.attractiveSettings.max_dist)
            {
                LastMagnetSuccess[player] = false;
                return;
            }
            if (config.enhancementSettings.perk_config.attractiveSettings.meleeOnly)
            {
                var heldEntity = player.GetHeldEntity();
                if (heldEntity == null || !(heldEntity is BaseMelee))
                {
                    LastMagnetSuccess[player] = false;
                    return;
                }
            }

            if (!LastMagnetSuccess.ContainsKey(player)) LastMagnetSuccess.Add(player, false);

            List<Item> item_drops = Pool.GetList<Item>();
            item_drops.AddRange(entity.inventory.itemList);

            LastMagnetSuccess[player] = true;
            BasePlayer _player = player;
            NextTick(() => ServerMgr.Instance.StartCoroutine(DoAttractive(player, item_drops)));                                           
        }

        public IEnumerator DoAttractive(BasePlayer player, List<Item> items)
        {
            for (int i = 0; i < 3; i++)
            {
                if (player == null || player.IsDead())
                {
                    Pool.FreeList(ref items);
                    if (player != null) LastMagnetSuccess[player] = false;
                    yield break;
                }
                bool foundStray = false;
                foreach (var item in items)
                {
                    if (item == null) continue;
                    if (item.GetWorldEntity() != null)
                    {
                        if (i > 0 && player.inventory.containerMain.IsFull() && player.inventory.containerBelt.IsFull()) continue;
                        foundStray = true;
                        player.GiveItem(item);
                    }
                }
                if (!foundStray) yield break;
                yield return CoroutineEx.waitForEndOfFrame;
            }
            Pool.FreeList(ref items);
            LastMagnetSuccess[player] = false;
        }
        
        void OnBonusItemDropped(Item item, BasePlayer player)
        {
            bool result;
            if (LastMagnetSuccess.TryGetValue(player, out result) && result)
            {
                NextTick(() =>
                {
                    if (item.GetWorldEntity() != null)
                        player.GiveItem(item);
                });                
            }
        }        

        #endregion

        #region Parsing       

        // Use this if we only need to access 1 perk in a hook.
        float GetTotalPerkMod(Perk perk, BasePlayer player)
        {
            float result = 0;

            // Handle active item
            var heldItem = player.GetActiveItem();
            if (heldItem != null && heldItem.text != null) result += GetPerkMod(perk, heldItem.text);

            PlayerPerks perkData;
            float mod = 0;
            if (Player_perks.TryGetValue(player.userID, out perkData) && perkData.perk_modifiers.TryGetValue(perk, out mod)) result += mod;

            //Puts($"Total {perk} mod: {result}");
            return result;
        }

        Dictionary<Perk, float> GlobalLimits = new Dictionary<Perk, float>();
        
        // Use this if we need to access multiple perks in a single hook.
        Dictionary<Perk, float> GetCombinedMods(BasePlayer player)
        {
            if (player == null || player.IsNpc || !player.userID.IsSteamId()) return null;
            var activeItem = player.GetActiveItem();
            Dictionary<Perk, float> result = null;
            if (activeItem != null && !string.IsNullOrEmpty(activeItem.text))
            {
                var weaponMods = GetMods(activeItem.text);
                if (weaponMods != null && weaponMods.Count > 0)
                {
                    result = new Dictionary<Perk, float>();
                    foreach (var kvp in weaponMods)
                    {
                        if (!result.ContainsKey(kvp.Key)) result.Add(kvp.Key, kvp.Value);
                        else result[kvp.Key] += kvp.Value;
                    }
                    
                }
                WipeDictionary(weaponMods);
            }

            PlayerPerks perkData;
            float mod = 0;
            if (Player_perks.TryGetValue(player.userID, out perkData))
            {
                if (result == null) result = new Dictionary<Perk, float>();
                foreach (var kvp in perkData.perk_modifiers)
                {
                    if (!result.ContainsKey(kvp.Key)) result.Add(kvp.Key, kvp.Value);
                    else result[kvp.Key] += kvp.Value;
                }
            }

            // Global limits is used to cap perk modifiers based on config values.
            if (GlobalLimits.Count > 0 && result != null)
            {
                for (int i = 0; i < result.Count; i++)
                {
                    var p = result.ElementAt(i);
                    float globalLimit;
                    if (GlobalLimits.TryGetValue(p.Key, out globalLimit) && globalLimit < p.Value) result[p.Key] = globalLimit;
                }
            }           

            return result;
        }

        static bool HasPerks(string item)
        {
            if (string.IsNullOrWhiteSpace(item) || item == string.Empty) return false;
            var arr = item.Split('[', ']');
            return arr.Length > 1;
        }

        float GetPerkMod(Perk _perk, string item)
        {
            if (string.IsNullOrEmpty(item) || string.IsNullOrWhiteSpace(item)) return 0;
            var arr = item.Split('[', ']');

            if (arr == null || arr.Length <= 1) return 0;

            string perkStr = _perk.ToString();

            foreach (var entry in arr.Skip(1))
                if (!string.IsNullOrWhiteSpace(entry))
                {
                    var split = entry.Split(' ');
                    if (split.Length <= 1) continue;
                    if (split[0].Equals(perkStr, StringComparison.OrdinalIgnoreCase))
                        return Convert.ToSingle(split[1]);
                }

            return 0;
        }

        Dictionary<Perk, float> GetMods(string item)
        {
            if (string.IsNullOrEmpty(item) || string.IsNullOrWhiteSpace(item)) return null;
            var arr = item.Split('[', ']');

            if (arr == null || arr.Length <= 1) return null;

            Dictionary<Perk, float> result = new Dictionary<Perk, float>();

            foreach (var entry in arr.Skip(1))
            {
                var split = entry.Split(' ');
                if (split.Length <= 1) continue;

                Perk chosenPerk;
                if (parsedEnums.TryGetValue(split[0], out chosenPerk))
                {
                    if (!result.ContainsKey(chosenPerk)) result.Add(chosenPerk, Convert.ToSingle(split[1]));
                    else result[chosenPerk] += Convert.ToSingle(split[1]);
                }
            }

            return result;
        }

        Dictionary<Perk, int> GetPerkCount(string item)
        {
            if (string.IsNullOrEmpty(item) || string.IsNullOrWhiteSpace(item)) return null;
            var arr = item.Split('[', ']');

            if (arr == null || arr.Length <= 1) return null;

            Dictionary<Perk, int> result = new Dictionary<Perk, int>();

            foreach (var entry in arr.Skip(1))
            {
                var split = entry.Split(' ');
                if (split.Length <= 1) continue;

                Perk chosenPerk;
                if (parsedEnums.TryGetValue(split[0], out chosenPerk))
                {
                    if (!result.ContainsKey(chosenPerk)) result.Add(chosenPerk, 1);
                    else result[chosenPerk]++;
                }
            }

            return result;
        }

        void GetPlayerPerks(BasePlayer player)
        {
            if (player.IsDead() || !player.IsConnected) return;
            PlayerPerks perkData;
            if (!Player_perks.TryGetValue(player.userID, out perkData))
            {
                Player_perks.Add(player.userID, perkData = new PlayerPerks());
            }
            if (perkData.perk_modifiers == null) perkData.perk_modifiers = new Dictionary<Perk, float>();
            else perkData.perk_modifiers.Clear();
            bool hasRegen = false;
            foreach (var item in player.inventory.containerWear.itemList)
            {
                if (item.text == null) continue;
                var perks = GetMods(item.text);
                if (perks != null)
                {
                    foreach (var perk in perks)
                    {
                        if (!perkData.perk_modifiers.ContainsKey(perk.Key)) perkData.perk_modifiers.Add(perk.Key, perk.Value);
                        else perkData.perk_modifiers[perk.Key] += perk.Value;
                        if (perk.Key == Perk.Regeneration) hasRegen = true;

                        //// Cap the perk limit if applicable.
                        //PerkSettings settings;
                        //if (!config.enhancementSettings.perk_settings.TryGetValue(perk.Key, out settings)) continue;
                        //if (settings.player_mod_limit > 0 && perk.Value > settings.player_mod_limit) perkData.perk_modifiers[perk.Key] = settings.player_mod_limit;     
                    }
                    // Remove dictionary from memory
                    perks.Clear();
                    perks = null;                    
                }
            }

            if (hasRegen)
                UpdateRegen(player, perkData.perk_modifiers[Perk.Regeneration]);
            else if (HasRegen(player))
                DestroyRegen(player);

            var activeItem = player.GetActiveItem();
            if (config.playerSettings.only_show_when_equipped)
            {
                if (perkData.perk_modifiers.Count == 0)
                {
                    if (activeItem == null || string.IsNullOrEmpty(activeItem.text) || !HasPerks(activeItem.text))
                        CuiHelper.DestroyUi(player, "InspectorButtonImg");
                    else SendInspectMenuButton(player);
                }
                else SendInspectMenuButton(player);
            }
            List<Perk> subscriptionCheck = Pool.GetList<Perk>();
            subscriptionCheck.AddRange(perkData.perk_modifiers.Keys);
            if (activeItem != null && activeItem.text != null && HasPerks(activeItem.text))
            {
                var weaponPerks = GetMods(activeItem.text).Keys;
                foreach (var wperk in weaponPerks)
                {
                    if (!subscriptionCheck.Contains(wperk))
                        subscriptionCheck.Add(wperk);
                }
            }
                
            HandlePlayerSubscription(player, subscriptionCheck);
            Pool.FreeList(ref subscriptionCheck);
        }

        #endregion

        #region helpers

        void RunEffect(BasePlayer player, string effect, Vector3 pos)
        {
            EffectNetwork.Send(new Effect(effect, pos, pos), player.net.connection);
        }

        bool HasPerms(BasePlayer player, string perm)
        {
            if (!permission.UserHasPermission(player.UserIDString, perm))
            {
                PrintToChat(player, lang.GetMessage("NoPerms", this, player.UserIDString));
                return false;
            }
            return true;
        }        

        bool DoingClearOut = false;

        void AddScrapToContainer(LootContainer container)
        {
            var scrap = ItemManager.CreateByName("scrap", UnityEngine.Random.Range(1, Math.Max(config.enhancementSettings.perk_config.scavenger_max_amount, 1) + 1));
            container.inventory.capacity++;
            container.inventorySlots++;
            if (!scrap.MoveToContainer(container.inventory)) scrap.DropAndTossUpwards(container.transform.position, 1);
        }

        void AddComponentsToContainer(LootContainer container)
        {
            var def = ComponentItems.GetRandom();
            Item item = ItemManager.CreateByName(def.shortname, Math.Max(UnityEngine.Random.Range(config.enhancementSettings.perk_config.componentLuckSettings.min, config.enhancementSettings.perk_config.componentLuckSettings.max + 1), 1));
            container.inventory.capacity++;
            container.inventorySlots++;
            AddItemToContainer(item, container);
        }

        void AddElectronicsToContainer(LootContainer container)
        {
            var def = ElectricalItems.GetRandom();
            Item item = ItemManager.CreateByName(def.shortname, Math.Max(UnityEngine.Random.Range(config.enhancementSettings.perk_config.electricalLuckSettings.min, config.enhancementSettings.perk_config.electricalLuckSettings.max + 1), 1));
            container.inventory.capacity++;
            container.inventorySlots++;
            AddItemToContainer(item, container);
        }

        bool AddItemToContainer(Item item, LootContainer container)
        {
            if (item == null) return false;

            if (item.MoveToContainer(container.inventory)) return true;

            container.inventory.capacity++;
            container.inventorySlots++;
            if (item.MoveToContainer(container.inventory)) return true;

            item.Remove();
            return false;
        }

        float HandlePerkDamage(BasePlayer player, Perk perk, Dictionary<Perk, float> perks)
        {
            float mod;
            if (perks.TryGetValue(perk, out mod)) return mod;
            return 0;
        }

        void ClearLists()
        {
            ElectricalItems?.Clear();
            ElectricalItems = null;

            ComponentItems?.Clear();
            ComponentItems = null;

            LootedContainers?.Clear();
            LootedContainers = null;

            parsedEnums?.Clear();
            parsedEnums = null;

            item_BPs?.Clear();
            item_BPs = null;

            ItemIDs?.Clear();
            ItemIDs = null;

            EnhanceableItems?.Clear();
            EnhanceableItems = null;

            Player_perks?.Clear();
            Player_perks = null;

            OvenWatchList?.Clear();
            OvenWatchList = null;

            LastMagnetSuccess?.Clear();
            LastMagnetSuccess = null;

            RegenAmount?.Clear();
            RegenAmount = null;

            took_damage?.Clear();
            took_damage = null;

            IsSubscribed?.Clear();
            IsSubscribed = null;

            PlayersSubscribed?.Clear();
            PlayersSubscribed = null;

            foreach (var kvp in LootTables)
            {
                if (kvp.Value != null) kvp.Value.Clear();
            }

            LootTables?.Clear();
            LootTables = null;

            workbench_looters?.Clear();
            workbench_looters = null;

            RecyclerPlayers?.Clear();
            RecyclerPlayers = null;

            GlobalLimits?.Clear();
            GlobalLimits = null;

            CraftCooldowns?.Clear();
            CraftCooldowns = null;
        }

        void HandleDispenserClearOut(BasePlayer player, ResourceDispenser dispenser)
        {               
            foreach (var _item in dispenser.containedItems)
            {
                if (_item.amount < 1) continue;
                var item = ItemManager.CreateByItemID(_item.itemid, Convert.ToInt32(_item.amount));
                _item.amount = 0;
                HandleDispenser(dispenser, player, item, false);
                player.GiveItem(item);
            }
            var attackEntity = player.GetHeldEntity() as AttackEntity;
            if (attackEntity == null) return;
            dispenser.AssignFinishBonus(player, 1f, attackEntity);
            HitInfo hitInfo = new HitInfo(player, dispenser.baseEntity, Rust.DamageType.Generic, dispenser.baseEntity.MaxHealth(), dispenser.transform.position);
            hitInfo.gatherScale = 0f;
            hitInfo.PointStart = dispenser.transform.position;
            hitInfo.PointEnd = dispenser.transform.position;
            var heldEntity = player.GetHeldEntity();
            hitInfo.WeaponPrefab = heldEntity;
            hitInfo.Weapon = null;
            dispenser.baseEntity.OnAttacked(hitInfo);
        }

        void HandleYield(Item item, float mod)
        {
            if (mod == 0) return;
            item.amount += Convert.ToInt32(item.amount * mod);
        }

        bool RollSuccessful(float chance, float target = 1)
        {
            if (chance == 0) return false;
            if (chance > target) return true;
            float roll = UnityEngine.Random.Range(0f, 1f);
            return chance + roll > 1;
        }

        void WipeDictionary(Dictionary<Perk, float> dict)
        {
            dict?.Clear();
            dict = null;
        }

        void WipeDictionary(Dictionary<Perk, int> dict)
        {
            dict?.Clear();
            dict = null;
        }

        private static List<T> FindEntitiesOfType<T>(Vector3 a, float n, int m = -1) where T : BaseEntity
        {
            int hits = Physics.OverlapSphereNonAlloc(a, n, Vis.colBuffer, m, QueryTriggerInteraction.Collide);
            List<T> entities = Pool.GetList<T>();
            for (int i = 0; i < hits; i++)
            {
                var entity = Vis.colBuffer[i]?.ToBaseEntity();
                if (entity is T && !entities.Contains(entity)) entities.Add(entity as T);
                Vis.colBuffer[i] = null;
            }
            return entities;
        }

        void GetEnhanceableItems()
        {
            foreach (var itemDef in ItemManager.GetItemDefinitions())
            {
                //This handles the item defs for blueprints (crafting refund).
                if (itemDef.Blueprint != null && itemDef.Blueprint.userCraftable)
                {
                    if (!item_BPs.ContainsKey(itemDef.shortname)) item_BPs.Add(itemDef.shortname, itemDef.Blueprint);
                }

                //This handles enchantables.
                if (itemDef.category == ItemCategory.Attire && itemDef.isWearable && !itemDef.shortname.StartsWith("frankensteins.monster") && !EnhanceableItems.Contains(itemDef.shortname) && !config.enhancementSettings.lootSettings.enhanceable_blacklist.Contains(itemDef.shortname))
                {
                    ItemIDs.Add(itemDef.shortname, itemDef.itemid);
                    EnhanceableItems.Add(itemDef.shortname);
                }

                if ((itemDef.category == ItemCategory.Weapon || itemDef.category == ItemCategory.Tool) && itemDef.isHoldable && (itemDef.occupySlots == ItemSlot.None || itemDef.occupySlots == 0) && !EnhanceableItems.Contains(itemDef.shortname) && !config.enhancementSettings.lootSettings.enhanceable_blacklist.Contains(itemDef.shortname))
                {
                    ItemIDs.Add(itemDef.shortname, itemDef.itemid);
                    EnhanceableItems.Add(itemDef.shortname);
                }

                if (itemDef.category == ItemCategory.Electrical && !config.enhancementSettings.perk_config.electricalLuckSettings.blacklist_shortnames.Contains(itemDef.shortname)) ElectricalItems.Add(itemDef);
                else if (itemDef.category == ItemCategory.Component && !config.enhancementSettings.perk_config.componentLuckSettings.blacklist_shortnames.Contains(itemDef.shortname)) ComponentItems.Add(itemDef);
            }
            Puts($"Added {EnhanceableItems.Count} items to loot table.");
        }

        #endregion

        #region Item Creation

        [HookMethod("IPAPI_CreateItem")]
        public object IPAPI_CreateItem(string shortname = null, ulong skin = 0, Dictionary<string, float> api_perks = null)
        {
            List<KeyValuePair<Perk, float>> perks = Pool.GetList<KeyValuePair<Perk, float>>();
            if (!api_perks.IsNullOrEmpty())
            {
                foreach (var perk in api_perks)
                {
                    Perk _perk;
                    if (parsedEnums.TryGetValue(perk.Key, out _perk)) perks.Add(new KeyValuePair<Perk, float>(_perk, perk.Value));
                }
            }            
            var result = CreateItem(shortname, skin, perks);

            Pool.FreeList(ref perks);
            return result;
        }

        string GetShortnameForPerk(List<Perk> perks)
        {
            if (perks.IsNullOrEmpty()) return EnhanceableItems.GetRandom();

            string shortname = null;

            List<string> whitelistShortnames = Pool.GetList<string>();
            List<string> blacklistShortnames = Pool.GetList<string>();
            foreach (var perk in perks)
            {
                PerkSettings perkMods;
                if (config.enhancementSettings.perk_settings.TryGetValue(perk, out perkMods))
                {
                    if (perkMods.whitelist != null && perkMods.whitelist.Count > 0) whitelistShortnames.AddRange(perkMods.whitelist);
                    if (perkMods.blacklist != null && perkMods.blacklist.Count > 0)
                    {
                        blacklistShortnames.AddRange(perkMods.blacklist);
                        foreach (var l in blacklistShortnames)
                        {
                            if (whitelistShortnames.Contains(l))
                            {
                                whitelistShortnames.Remove(l);
                            }
                        }
                    }
                }
            }

            if (whitelistShortnames.Count > 0) shortname = whitelistShortnames.GetRandom();
            else if (blacklistShortnames.Count > 0)
            {
                List<string> validshortnames = Pool.GetList<string>();
                validshortnames.AddRange(EnhanceableItems.Where(x => !blacklistShortnames.Contains(x)));
                shortname = validshortnames.GetRandom();
                Pool.FreeList(ref validshortnames);
            }
            else shortname = EnhanceableItems.GetRandom();

            Pool.FreeList(ref whitelistShortnames);
            Pool.FreeList(ref blacklistShortnames);

            return shortname;
        }

        Item CreateItem(string shortname = null, ulong skin = 0, List<KeyValuePair<Perk, float>> perks = null)
        {
            if (shortname != null && !ItemManager.FindItemDefinition(shortname))
            {
                Puts($"Error: {shortname} is not a valid shortname. Picking a random item instead.");
                shortname = null;                
            }
            if (string.IsNullOrEmpty(shortname))
            {
                List<Perk> _perks = Pool.GetList<Perk>();
                if (!perks.IsNullOrEmpty())
                {
                    foreach (var kvp in perks)
                        _perks.Add(kvp.Key);
                }                
                shortname = GetShortnameForPerk(_perks);
                Pool.FreeList(ref _perks);
            }

            List<ulong> skins;
            if (skin == 0 && config.enhancementSettings.item_skins.TryGetValue(shortname, out skins) && skins != null && skins.Count > 0)
                skin = skins.GetRandom();

            Item item = ItemManager.CreateByName(shortname, 1, skin);
            if (item == null) return null;
            
            if (item.GetHeldEntity() != null)
                item.GetHeldEntity().skinID = skin;

            item.MarkDirty();

            bool handleListClear = perks == null;
            if (perks == null || perks.Count == 0)
            {
                if (perks == null) perks = Pool.GetList<KeyValuePair<Perk, float>>();
                int rolls = RollPerkSlots();
                for (int i = 0; i < rolls; i++)
                {
                    perks.Add(new KeyValuePair<Perk, float>(RollPerk(shortname, perks), 0));                   
                }
            }
            string name = string.Empty;
            foreach (var perk in perks)
            {
                PerkSettings perkMods;
                if (!config.enhancementSettings.perk_settings.TryGetValue(perk.Key, out perkMods)) continue;
                if (perk.Value > 0)
                {
                    name += $"[{perk.Key} {perk.Value}]";
                    continue;
                }
                double mod = Math.Round(UnityEngine.Random.Range(perkMods.min_mod, perkMods.max_mod), 3);
                name += $"[{perk.Key} {mod}]";
            }
            item.text = name;
            if (!string.IsNullOrEmpty(config.enhancementSettings.item_name_prefix))
                item.name = config.enhancementSettings.item_name_prefix + " " + item.info.displayName.english;
            if (handleListClear) Pool.FreeList(ref perks);           

            return item;
        }

        bool IsHoldableItem(string shortname)
        {
            var def = ItemManager.FindItemDefinition(shortname);
            if (def == null) return false;

            return def.isHoldable;
        }

        bool IsHoldablePerk(Perk perk)
        {
            switch (perk)
            {
                case Perk.Regeneration: return false;
                default: return true;
            }
        }

        Perk RollPerk(string shortname, List<KeyValuePair<Perk, float>> existing_perks)
        {
            int TotalPerkWeight = 0;
            List<KeyValuePair<Perk, PerkSettings>> perks = Pool.GetList<KeyValuePair<Perk, PerkSettings>>();
            List<Perk> exclusions = Pool.GetList<Perk>();
            FindExclusions(shortname, exclusions);
            foreach (var perk in config.enhancementSettings.perk_settings)
            {
                if (!perk.Value.enabled) continue;
                if (IsHoldableItem(shortname) && !IsHoldablePerk(perk.Key)) continue;
                if (exclusions.Contains(perk.Key))
                {
                    continue;
                }

                perks.Add(perk);
                TotalPerkWeight += perk.Value.perkWeight;
            }
            Pool.FreeList(ref exclusions);

            if (!config.enhancementSettings.allow_duplicate_perks)
            {
                List<KeyValuePair<Perk, PerkSettings>> duplicateRemove = Pool.GetList<KeyValuePair<Perk, PerkSettings>>();
                foreach (var ep in existing_perks)
                {
                    foreach (var p in perks)
                    {
                        if (ep.Key == p.Key)
                        {
                            if (!duplicateRemove.Contains(p)) duplicateRemove.Add(p);
                            break;
                        }
                    }
                }

                if (duplicateRemove.Count > 0)
                {
                    foreach (var kvp in duplicateRemove)
                    {
                        TotalPerkWeight -= kvp.Value.perkWeight;
                        perks.Remove(kvp);
                    }
                }

                Pool.FreeList(ref duplicateRemove);
            }

            if (perks.Count == 0)
            {
                Puts("Perk count was 0 so we are just picking a random perk.");
                Pool.FreeList(ref perks);
                return GetRandomPerkIgnoreCriteria(config.enhancementSettings.allow_duplicate_perks ? null : existing_perks);
            }

            int count = 0;
            int roll = UnityEngine.Random.Range(0, TotalPerkWeight + 1);

            Perk result = Perk.None;
            foreach (var perk in perks)
            {
                count += perk.Value.perkWeight;
                if (roll <= count)
                {
                    result = perk.Key;
                    break;
                }                
            }
            Pool.FreeList(ref perks);
            if (result != Perk.None) return result;
            result = GetRandomPerkIgnoreCriteria(existing_perks);
            return result;
        }

        List<Perk> FindExclusions(string shortname, List<Perk> exclusions)
        {
            foreach (var kvp in config.enhancementSettings.perk_settings)
            {
                if (kvp.Value.whitelist != null && kvp.Value.whitelist.Count > 0 && !kvp.Value.whitelist.Contains(shortname)) exclusions.Add(kvp.Key);
                else if (kvp.Value.blacklist != null && kvp.Value.blacklist.Contains(shortname)) exclusions.Add(kvp.Key);
            }

            return exclusions;
        }

        Perk GetRandomPerkIgnoreCriteria(List<KeyValuePair<Perk, float>> existing_perks)
        {
            List<string> forbiddenPerks = null;
            List<string> perkstrings = Pool.GetList<string>();
            if (existing_perks != null && existing_perks.Count > 0)
            {
                forbiddenPerks = Pool.GetList<string>();
                foreach (var kvp in existing_perks)
                {
                    var keyStr = kvp.Key.ToString();
                    if (!forbiddenPerks.Contains(keyStr)) forbiddenPerks.Add(keyStr);
                }
                perkstrings.AddRange(parsedEnums.Keys.Where(x => !forbiddenPerks.Contains(x)));
            }            
            else perkstrings.AddRange(parsedEnums.Keys);
            
            var result = parsedEnums[perkstrings.GetRandom()];
            Pool.FreeList(ref perkstrings);

            return result;
        }

        int RollPerkSlots()
        {
            int highest = 1;
            var roll = UnityEngine.Random.Range(0f, 100f);
            foreach (var entry in config.enhancementSettings.additional_perk_chances)
            {
                if (entry.Key > highest && entry.Value >= 100 - roll) highest = entry.Key;
            }

            return highest;
        }

        #endregion

        #region CUI

        /* Features
         * - Inspector (just equipped or all items?)
         * - Ability to build items via CUI (either admin use or purchasable)
         * - 
         * 
         */

        void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "backPanel");
            CuiHelper.DestroyUi(player, "ItemPerks_Slots");
            CuiHelper.DestroyUi(player, "ItemPerks_IndividualInspector");
            CuiHelper.DestroyUi(player, "PerkDetails");
            CuiHelper.DestroyUi(player, "ItemPerks_GlobalStats");
        }

        #region Backpanel

        void SendMenu(BasePlayer player)
        {
            SendBackPanel(player);
            SendPerkSlots(player);
            SendGlobalStats(player);
        }

        private void SendBackPanel(BasePlayer player)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0 0 0 0.99" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-0.353 -0.328", OffsetMax = "0.347 0.342" }
            }, "Overlay", "backPanel");

            container.Add(new CuiElement
            {
                Name = "Label_2913",
                Parent = "backPanel",
                Components = {
                    new CuiTextComponent { Text = lang.GetMessage("UICLOSE", this, player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-84 -207.6", OffsetMax = "-24 -182.6" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = "closeitemperkinspector" },
                Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-84 -207.6", OffsetMax = "-24 -182.6" }
            }, "backPanel", "closeButton");

            CuiHelper.DestroyUi(player, "backPanel");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("closeitemperkinspector")]
        void CloseUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            DestroyUI(player);
        }

        #endregion

        #region Perk slots

        private void SendPerkSlots(BasePlayer player, ulong selectedPiece = 0)
        {
            // We get the worn and active items that are enhanced and store them
            List<Item> items = Pool.GetList<Item>();
            foreach (var item in player.inventory.containerWear.itemList)
            {
                if (!string.IsNullOrEmpty(item.text) && HasPerks(item.text)) items.Add(item);
            }            
            var activeItem = player.GetActiveItem();
            if (activeItem != null && !string.IsNullOrEmpty(activeItem.text) && HasPerks(activeItem.text)) items.Add(activeItem);

            if (items.Count < 1)
            {
                Pool.FreeList(ref items);
                return;
            }
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-223.999 120.011", OffsetMax = "-176.001 168.009" }
            }, "Overlay", "ItemPerks_Slots");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.4716981 0.4716981 0.4716981 1" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = $"-24.003 {0 - (items.Count * 48)}", OffsetMax = "23.997 0" }
            }, "ItemPerks_Slots", "backpanel");            

            int count = 0;
            if (selectedPiece == 0)
            {                
                selectedPiece = items[0].uid.Value;
                SendInspector(player, items[0]);
            }
            foreach (var item in items)
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.1803922 0.1803922 0.1803922 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-22.003 {-22.003 - (count * 48)}", OffsetMax = $"21.997 {21.997 - (count * 48)}" }
                }, "ItemPerks_Slots", "slot");

                if (item.uid.Value == selectedPiece)
                {
                    container.Add(new CuiPanel
                    {
                        CursorEnabled = false,
                        Image = { Color = "0 0.542263 1 0.3960784" },
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-22 -22", OffsetMax = "22 22" }
                    }, "slot", "selectedPanel");
                }

                container.Add(new CuiElement
                {
                    Name = "img",
                    Parent = "slot",
                    Components = {
                    new CuiImageComponent { Color = "1 1 1 1", ItemId = item.info.itemid, SkinId = item.skin },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-22 -22", OffsetMax = "22 22" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "1 1 1 0", Command = $"sendinfopanel {item.uid.Value}" },
                    Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-22 -22", OffsetMax = "22 22" }
                }, "slot", "button");

                count++;
            }

            Pool.FreeList(ref items);

            CuiHelper.DestroyUi(player, "ItemPerks_Slots");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("sendinfopanel")]
        void SendInfoPanel(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length < 1) return;

            ulong uid = Convert.ToUInt64(arg.Args[0]);

            Item item = player.inventory.FindItemByUID(new ItemId(uid));
            if (item == null)
            {
                PrintToChat(player, lang.GetMessage("MissingItem", this, player.UserIDString));
                return;
            }

            SendPerkSlots(player, item.uid.Value);
            SendInspector(player, item);
        }

        #endregion

        #region Individual Inspector

        private void SendInspector(BasePlayer player, Item item, int perkIndex = 0)
        {
            var mods = GetMods(item.text);
            if (mods == null) return;

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.4745098 0.4745098 0.4745098 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-172.003 2.007", OffsetMax = "63.997 168.007" }
            }, "Overlay", "ItemPerks_IndividualInspector");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.1803922 0.1803922 0.1803922 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-116 -81", OffsetMax = "116 81" }
            }, "ItemPerks_IndividualInspector", "InnerPanel");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.3679245 0.3679245 0.3679245 0.4901961" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-114 55.005", OffsetMax = "114 79.005" }
            }, "InnerPanel", "TitleBackpanel");

            container.Add(new CuiElement
            {
                Name = "title",
                Parent = "InnerPanel",
                Components = {
                    new CuiTextComponent { Text = item.name?.ToUpper() ?? item.info.displayName.english.ToUpper(), Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-114 55.004", OffsetMax = "114 79.005" }
                }
            });

            if ((float)mods.Count / 4 > perkIndex + 1)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = "1 1 1 0", Command = $"individualinspectorpagechange {item.uid.Value} {perkIndex + 1}" },
                    Text = { Text = ">>", Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.UpperCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "79 -76", OffsetMax = "111 -60" }
                }, "InnerPanel", "nextbutton");
            } 
            
            if (perkIndex > 0)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = "1 1 1 0", Command = $"individualinspectorpagechange {item.uid.Value} {perkIndex - 1}" },
                    Text = { Text = "<<", Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.UpperCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-111 -76", OffsetMax = "-79 -60" }
                }, "InnerPanel", "backbutton");
            }

            container.Add(new CuiElement
            {
                Name = "perksTitle",
                Parent = "ItemPerks_IndividualInspector",
                Components = {
                    new CuiTextComponent { Text = lang.GetMessage("UIPERK", this, player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.UpperLeft, Color = "1 0.937262 0 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-100 33.01", OffsetMax = "0 53.01" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "modTitle",
                Parent = "ItemPerks_IndividualInspector",
                Components = {
                    new CuiTextComponent { Text = lang.GetMessage("UIMOD", this, player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.UpperLeft, Color = "1 0.937262 0 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "37.999 33.01", OffsetMax = "99.999 53.01" }
                }
            });

            int indexCount = 0;
            int displayCount = 0;
            int count = 0;
            foreach (var mod in mods)
            {
                count++;
                if (indexCount == perkIndex)
                {
                    container.Add(new CuiElement
                    {
                        Name = "perk",
                        Parent = "ItemPerks_IndividualInspector",
                        Components = {
                        new CuiTextComponent { Text = lang.GetMessage("UI" + mod.Key.ToString(), this, player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                        new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-100 {13.01 - (displayCount * 20)}", OffsetMax = $"0 {33.01 - (displayCount * 20)}" }
                    }
                    });

                    container.Add(new CuiButton
                    {
                        Button = { Color = "1 1 1 0", Command = $"showitmperkinfofor {mod.Key}" },
                        Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-50 -10", OffsetMax = "50 10" }
                    }, "perk", "infobutton");

                    string perkType = GetPerkTypeString(mod.Key.ToString());
                    container.Add(new CuiElement
                    {
                        Name = "mod",
                        Parent = "ItemPerks_IndividualInspector",
                        Components = {
                        new CuiTextComponent { Text = $"+{Math.Round(GetPerkValue(mod.Value, perkType), 2)}{lang.GetMessage(perkType, this, player.UserIDString)}", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.UpperLeft, Color = "0.03471455 0.7735849 0 1" },
                        new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"37.999 {13.01 - (displayCount * 20)}", OffsetMax = $"99.999 {33.01 - (displayCount * 20)}" }
                    }
                    });

                    displayCount++;
                    if (displayCount >= 4) break;
                }
                else if (count >= 4)
                {
                    indexCount++;
                    count = 0;
                }
                if (indexCount > perkIndex) break;
            }
           

            CuiHelper.DestroyUi(player, "ItemPerks_IndividualInspector");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("individualinspectorpagechange")]
        void SendNextPerkPage(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length == 0) return;

            ulong uid = Convert.ToUInt64(arg.Args[0]);
            var item = player.inventory.FindItemByUID(new ItemId(uid));
            if (item == null) return;

            int index = Convert.ToInt32(arg.Args[1]);
            SendInspector(player, item, index);
        }

        [ConsoleCommand("showitmperkinfofor")]
        void SendItemPerkInfoPanel(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length == 0) return;

            SendPerkInfo(player, arg.Args[0]);
        }

        #endregion

        #region Perk information page

        private void SendPerkInfo(BasePlayer player, string perk)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.4745098 0.4745098 0.4745098 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "68 68.01", OffsetMax = "268 168.01" }
            }, "Overlay", "PerkDetails");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.1803922 0.1803922 0.1803922 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-98 -48", OffsetMax = "98 48" }
            }, "PerkDetails", "innerpanel");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.3679245 0.3679245 0.3679245 0.4901961" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-96 22.005", OffsetMax = "96 46.005" }
            }, "PerkDetails", "TitleBackpanel");

            container.Add(new CuiElement
            {
                Name = "title",
                Parent = "PerkDetails",
                Components = {
                    new CuiTextComponent { Text = lang.GetMessage("UI" + perk, this, player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-96 22.005", OffsetMax = "96 46.005" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Label_9185",
                Parent = "PerkDetails",
                Components = {
                    new CuiTextComponent { Text = GetPerkDescription(player, perk), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-94 -43.99", OffsetMax = "94 20.01" }
                }
            });

            CuiHelper.DestroyUi(player, "PerkDetails");
            CuiHelper.AddUi(player, container);
        }

        string GetPerkDescription(BasePlayer player, string perk)
        {
            Perk parsedPerk;
            if (!parsedEnums.TryGetValue(perk, out parsedPerk)) return $"No perk information found for {perk}";
            PerkSettings ps;
            if (!config.enhancementSettings.perk_settings.TryGetValue(parsedPerk, out ps)) return lang.GetMessage(perk, this, player.UserIDString);
            string perkType = GetPerkTypeString(perk);
            return string.Format(lang.GetMessage(perk, this, player.UserIDString), GetPerkValue(ps.min_mod, perkType), GetPerkValue(ps.max_mod, perkType), lang.GetMessage(perkType, this, player.UserIDString)) + (ps.perk_cap > 0 ? string.Format(lang.GetMessage("PerkPlayerLimit", this, player.UserIDString), GetPerkValue(ps.perk_cap, perkType)) : null);
        }

        string GetPerkTypeString(string perk)
        {
            switch (perk)
            {
                case "Regeneration": return "hps";
                default: return "%";
            }
        }

        float GetPerkValue(float mod, string type)
        {
            switch (type)
            {
                case "hps": return mod;
                default: return mod * 100;
            }
        }

        #endregion

        #region Global stats page

        private void SendGlobalStats(BasePlayer player, int perkIndex = 0)
        {
            if (!config.playerSettings.show_total_bonuses) return;
            var mods = GetCombinedMods(player);
            if (mods == null) return;

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.4745098 0.4745098 0.4745098 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-172.003 -167.993", OffsetMax = "63.997 -1.993" }
            }, "Overlay", "ItemPerks_GlobalStats");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.1803922 0.1803922 0.1803922 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-116 -81", OffsetMax = "116 81" }
            }, "ItemPerks_GlobalStats", "InnerPanel");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.3679245 0.3679245 0.3679245 0.4901961" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-114 55.005", OffsetMax = "114 79.005" }
            }, "ItemPerks_GlobalStats", "TitleBackpanel");

            container.Add(new CuiElement
            {
                Name = "title",
                Parent = "ItemPerks_GlobalStats",
                Components = {
                    new CuiTextComponent { Text = lang.GetMessage("UITOTALBONUSES", this, player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-114 55.005", OffsetMax = "114 79.005" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "perksTitle",
                Parent = "ItemPerks_GlobalStats",
                Components = {
                    new CuiTextComponent { Text = lang.GetMessage("UIPERK", this, player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.UpperLeft, Color = "1 0.937262 0 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-99.999 33.01", OffsetMax = "0.001 53.01" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "modTitle",
                Parent = "ItemPerks_GlobalStats",
                Components = {
                    new CuiTextComponent { Text = lang.GetMessage("UIMOD", this, player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.UpperLeft, Color = "1 0.937262 0 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "38 33.01", OffsetMax = "100 53.01" }
                }
            });

            int indexCount = 0;
            int displayCount = 0;
            int count = 0;
            foreach (var mod in mods)
            {
                count++;
                if (indexCount == perkIndex)
                {
                    container.Add(new CuiElement
                    {
                        Name = "perk",
                        Parent = "ItemPerks_GlobalStats",
                        Components = {
                        new CuiTextComponent { Text = lang.GetMessage("UI" + mod.Key.ToString(), this, player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                        new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-99.999 {13.01 - (displayCount * 20)}", OffsetMax = $"0.001 {33.01 - (displayCount * 20)}" }
                    }
                    });

                    container.Add(new CuiButton
                    {
                        Button = { Color = "1 1 1 0", Command = $"showitmperkinfofor {mod.Key}" },
                        Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-50 -10", OffsetMax = "50 10" }
                    }, "perk", "infobutton");

                    string perkType = GetPerkTypeString(mod.Key.ToString());
                    container.Add(new CuiElement
                    {
                        Name = "mod",
                        Parent = "ItemPerks_GlobalStats",
                        Components = {
                        new CuiTextComponent { Text = $"+{Math.Round(GetPerkValue(mod.Value, perkType), 2)}{lang.GetMessage(perkType, this, player.UserIDString)}", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.UpperLeft, Color = "0.03471455 0.7735849 0 1" },
                        new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"38 {13.01 - (displayCount * 20)}", OffsetMax = $"100 {33.01 - (displayCount * 20)}" }
                    }
                    });

                    displayCount++;
                    if (displayCount >= 4) break;
                }
                else if (count >= 4)
                {
                    indexCount++;
                    count = 0;
                }
                if (indexCount > perkIndex) break;
            }            

            if ((float)mods.Count / 4 > perkIndex + 1)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = "1 1 1 0", Command = $"globalinspectorpagechange {perkIndex + 1}" },
                    Text = { Text = ">>", Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.UpperCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "79 -76", OffsetMax = "111 -60" }
                }, "ItemPerks_GlobalStats", "nextbutton");
            }

            if (perkIndex > 0)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = "1 1 1 0", Command = $"globalinspectorpagechange {perkIndex - 1}" },
                    Text = { Text = "<<", Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.UpperCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-111 -76", OffsetMax = "-79 -60" }
                }, "ItemPerks_GlobalStats", "backbutton");
            }                

            CuiHelper.DestroyUi(player, "ItemPerks_GlobalStats");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("globalinspectorpagechange")]
        void SendNextGlobalPage(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length == 0) return;

            int index = Convert.ToInt32(arg.Args[0]);
            SendGlobalStats(player, index);
        }

        #endregion

        #region Inspector Icon

        private void SendInspectMenuButton(BasePlayer player)
        {
            if (!config.playerSettings.send_menu_icon) return;
            if (player.IsNpc || !player.userID.IsSteamId()) return;
            if (!permission.UserHasPermission(player.UserIDString, perm_use)) return;
            if (player.IsDead()) return;
            var container = new CuiElementContainer();
            var pi = GetPlayerData(player);

            CuiImageComponent img = new CuiImageComponent { Color = "1 1 1 1", ItemId = 1751045826, SkinId = config.playerSettings.icon_id };
            container.Add(new CuiElement
            {
                Name = "InspectorButtonImg",
                Parent = "Overlay",
                Components = {
                    img,
                    new CuiRectTransformComponent { AnchorMin = config.playerSettings.iconSettings.anchor_min, AnchorMax = config.playerSettings.iconSettings.anchor_max, OffsetMin = $"{-235.4 + pi.iconSettings.offset_x - pi.iconSettings.size / 2} {36.7 + pi.iconSettings.offset_y - pi.iconSettings.size / 2}", OffsetMax = $"{-235.4 + pi.iconSettings.offset_x + pi.iconSettings.size / 2} {36.7 + pi.iconSettings.offset_y + pi.iconSettings.size / 2}" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = "senditemperksinspectormenu" },
                Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-{pi.iconSettings.size / 2} -{pi.iconSettings.size / 2}", OffsetMax = $"{pi.iconSettings.size / 2} {pi.iconSettings.size / 2}" }
            }, "InspectorButtonImg", "InspectButton");

            CuiHelper.DestroyUi(player, "InspectorButtonImg");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("senditemperksinspectormenu")]
        void IconPressed(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            if (!HasPerms(player, perm_use) && !HasPerms(player, perm_admin))
            {
                CuiHelper.DestroyUi(player, "InspectorButtonImg");
                DestroyUI(player);
                return;
            }

            SendMenu(player);
        }

        #endregion

        #region Item creation

        #region background

        private void WorkbenchBackground(BasePlayer player)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0 0 0 0.9992353" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-0.351 -0.332", OffsetMax = "0.349 0.338" }
            }, "Overlay", "workbenchbackground");

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = "ipclosetheenhancementmenu" },
                Text = { Text = lang.GetMessage("UICLOSE", this, player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-50 -206.8", OffsetMax = "50 -174.8" }
            }, "workbenchbackground", "closebutton");

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = "ipenhancementmenustartover" },
                Text = { Text = lang.GetMessage("UIRESET", this, player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 -206.8", OffsetMax = "-50 -174.8" }
            }, "workbenchbackground", "closebutton");

            CuiHelper.DestroyUi(player, "workbenchbackground");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("ipclosetheenhancementmenu")]
        void CloseEnhancementMenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            CloseEnhancementMenu(player);
        }
        
        [ConsoleCommand("ipenhancementmenustartover")]
        void ResetEnhancementMenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            CuiHelper.DestroyUi(player, "EnhancementMenu");
            CuiHelper.DestroyUi(player, "ItemSelectMenu");
            CuiHelper.DestroyUi(player, "UpgradeConfirmation");

            EnhancementMenu(player);
        }

        void CloseEnhancementMenu(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "workbenchbackground");
            CuiHelper.DestroyUi(player, "EnhancementMenu");
            CuiHelper.DestroyUi(player, "ItemSelectMenu");
            CuiHelper.DestroyUi(player, "UpgradeConfirmation");

            //Check if player is looting workbench and send menu button
            if (workbench_looters.Contains(player)) SendWorkbenchButton(player);            
        }

        #endregion

        #region Workbench Button

        private void SendWorkbenchButton(BasePlayer player)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.4313726 0.5411765 0.254902 1" },
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "420 232", OffsetMax = "572 257" }
            }, "Overlay", "WorkbenchEnhancementPanel");

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = $"ipsendenhancementmenu" },
                Text = { Text = lang.GetMessage("UIEnhancementButton", this, player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-55 -12.5", OffsetMax = "55 12.5" }
            }, "WorkbenchEnhancementPanel", "Button_5626");

            CuiHelper.DestroyUi(player, "WorkbenchEnhancementPanel");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("ipsendenhancementmenu")]
        void SendEnhancementMenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            CuiHelper.DestroyUi(player, "WorkbenchEnhancementPanel");
            WorkbenchBackground(player);
            EnhancementMenu(player);
        }

        #endregion

        #region Menu: Enhancement selection

        private void EnhancementMenu(BasePlayer player)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Name = "EnhancementMenu",
                Parent = "Overlay",
                Components = {
                    new CuiTextComponent { Text = lang.GetMessage("UIEnhancementChooseTitle", this, player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-250.004 180.8", OffsetMax = "249.996 230.8" }
                }
            });

            List<Item> items = Pool.GetList<Item>();          
            
            foreach (var item in player.inventory.AllItems())
            {
                if (item == null || string.IsNullOrEmpty(item.text)) continue;
                if (IsEnhancementKit(item.text)) items.Add(item);
            }

            if (items.Count == 0)
            {
                container.Add(new CuiElement
                {
                    Name = "NoKitsMessage",
                    Parent = "EnhancementMenu",
                    Components = {
                    new CuiTextComponent { Text = lang.GetMessage("UIEnhancementChooseNoIKits", this, player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-250 -200.2", OffsetMax = "250 -150.2" }
                }
                });                
            }
            else
            {
                var count = 0;
                var rows = 0;
                foreach (var item in items)
                {
                    container.Add(new CuiPanel
                    {
                        CursorEnabled = false,
                        Image = { Color = "0.2735849 0.2735849 0.2735849 1" },
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{-250 + (count * 180)} {-89.5 - (rows * 80)}", OffsetMax = $"{-110 + (count * 180)} {-49.5 - (rows * 80)}" }
                    }, "EnhancementMenu", "PerkPanel_");

                    container.Add(new CuiButton
                    {
                        Button = { Color = "1 1 1 0", Command = $"ipenhancementmenuselectperk {item.uid.Value}" },
                        Text = { Text = string.Format(lang.GetMessage("UIEnhancementChooseKitTitle", this, player.UserIDString), item.text), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-70.004 -20", OffsetMax = "69.996 20" }
                    }, "PerkPanel_", "button");

                    count++;
                    if (count >= 3)
                    {
                        count = 0;
                        rows++;
                    }
                }                
            }            

            Pool.FreeList(ref items);

            CuiHelper.DestroyUi(player, "EnhancementMenu");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("ipenhancementmenuselectperk")]
        void SelectEnhancement(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            ulong id = Convert.ToUInt64(arg.Args[0]);
            CuiHelper.DestroyUi(player, "EnhancementMenu");

            ItemSelectMenu(player, id);
        }

        #endregion

        #region Menu: Item selection

        private void ItemSelectMenu(BasePlayer player, ulong perk_uid)
        {
            var perkItem = player.inventory.FindItemByUID(new ItemId(perk_uid));
            if (perkItem == null) return;

            var perk = GetEnhancementKitPerk(perkItem.text);
            if (perk == Perk.None) return;

            var container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Name = "ItemSelectMenu",
                Parent = "Overlay",
                Components = {
                    new CuiTextComponent { Text = lang.GetMessage("UIEnhancementItemSelectTitle", this, player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-250.004 180.8", OffsetMax = "249.996 230.8" }
                }
            });

            List<Item> items = Pool.GetList<Item>();

            foreach (var item in player.inventory.AllItems())
            {
                if (CanBeEnhanced(item, perk)) items.Add(item);
            }

            if (items.Count == 0)
            {
                container.Add(new CuiElement
                {
                    Name = "NoItemsMessage",
                    Parent = "ItemSelectMenu",
                    Components = {
                    new CuiTextComponent { Text = lang.GetMessage("UIEnhancementItemSelectNoItems", this, player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-250 -200.2", OffsetMax = "250 -150.2" }
                }
                });
            }            
            else
            {
                var count = 0;
                var rows = 0;
                foreach (var item in items)
                {
                    container.Add(new CuiElement
                    {
                        Name = $"item_{rows}_{count}",
                        Parent = "ItemSelectMenu",
                        Components = {
                        new CuiImageComponent { Color = "1 1 1 1", ItemId = item.info.itemid, SkinId = item.skin },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{-249 + (50 * count)} {-86.7 - (rows * 50)}", OffsetMax = $"{-201+ (50 * count)} {-38.7 - (rows * 50)}" }
                    }
                    });

                    container.Add(new CuiButton
                    {
                        Button = { Color = "1 1 1 0", Command = $"ipenhancementmenuitemselected {perk_uid} {item.uid.Value}" },
                        Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -25", OffsetMax = "25 25" }
                    }, $"item_{rows}_{count}", "button");

                    count++;
                    if (count >= 10)
                    {
                        count = 0;
                        rows++;
                    }
                }
            }

            Pool.FreeList(ref items);

            CuiHelper.DestroyUi(player, "ItemSelectMenu");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("ipenhancementmenuitemselected")]
        void SelectItemToEnhance(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            var perkID = Convert.ToUInt64(arg.Args[0]);
            var itemID = Convert.ToUInt64(arg.Args[1]);

            CuiHelper.DestroyUi(player, "ItemSelectMenu");
            UpgradeConfirmation(player, perkID, itemID);
        }

        int MaxPerks = 1;

        bool CanBeEnhanced(Item item, Perk perk)
        {
            if (!EnhanceableItems.Contains(item.info.shortname) || config.enhancementSettings.lootSettings.enhanceable_blacklist.Contains(item.info.shortname)) return false;
            if (string.IsNullOrEmpty(item.text)) return true;

            var perks = GetPerkCount(item.text);
            if (perks == null) return false;
                        
            if (config.enhancementSettings.enhancement_kit_settings.perks_capped)
            {
                int perkCount = 0;
                foreach (var kvp in perks)
                {
                    perkCount += kvp.Value;
                }
                if (perkCount >= MaxPerks)
                {
                    WipeDictionary(perks);
                    return false;
                }
            }

            if (!config.enhancementSettings.allow_duplicate_perks && perks.ContainsKey(perk))
            {
                WipeDictionary(perks);
                return false;
            }

            WipeDictionary(perks);
            return true;
        }

        #endregion

        #region Menu: Confirm enhancement

        private void UpgradeConfirmation(BasePlayer player, ulong perkItemUID, ulong selectedItemUID)
        {
            var perkItem = player.inventory.FindItemByUID(new ItemId(perkItemUID));
            if (perkItem == null) return;

            var selectedItem = player.inventory.FindItemByUID(new ItemId(selectedItemUID));
            if (selectedItem == null) return;

            var perk = GetEnhancementKitPerk(perkItem.text);
            if (perk == Perk.None) return;

            var container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Name = "UpgradeConfirmation",
                Parent = "Overlay",
                Components = {
                    new CuiTextComponent { Text = lang.GetMessage("UIEnhancementConfirmTitle", this, player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-250.004 180.8", OffsetMax = "249.996 230.8" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "img",
                Parent = "UpgradeConfirmation",
                Components = {
                    new CuiImageComponent { Color = "1 1 1 1", ItemId = selectedItem.info.itemid, SkinId = selectedItem.skin },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-143 -124", OffsetMax = "-71 -52" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "item",
                Parent = "img",
                Components = {
                    new CuiTextComponent { Text = string.Format(lang.GetMessage("UIEnhancementConfirmItemTitle", this, player.UserIDString), selectedItem.name ?? selectedItem.info.displayName.english), Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "40.4 14", OffsetMax = "220.4 36" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "perk",
                Parent = "img",
                Components = {
                    new CuiTextComponent { Text = string.Format(lang.GetMessage("UIEnhancementConfirmPerkTitle", this, player.UserIDString), perk), Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "40.4 -8", OffsetMax = "220.4 14" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Cost",
                Parent = "img",
                Components = {
                    new CuiTextComponent { Text = string.Format(lang.GetMessage("UIEnhancementConfirmCostTitle", this, player.UserIDString), GetAdditionalEnhancementCost(perk), lang.GetMessage(config.enhancementSettings.enhancement_kit_settings.upgrade_settings.upgrade_cost_type, this, player.UserIDString)), Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "40.4 -30", OffsetMax = "220.4 -8" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Label_8345",
                Parent = "img",
                Components = {
                    new CuiTextComponent { Text = string.Format(lang.GetMessage("UIEnhancementConfirmPerkConfirmation", this, player.UserIDString), perk), Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-36 -128.701", OffsetMax = "251.658 -49.62" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Label_8345",
                Parent = "img",
                Components = {
                    new CuiTextComponent { Text = GetPerkDescription(player, perk.ToString()), Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-36 -208.701", OffsetMax = "251.658 -129.62" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = $"ipconfirmenhance {perkItemUID} {selectedItemUID}" },
                Text = { Text = "<color=#ffec00>CONFIRM</color>", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleLeft, Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "175 -327.7", OffsetMax = "275 -287.7" }
            }, "img", "button");

            CuiHelper.DestroyUi(player, "UpgradeConfirmation");
            CuiHelper.AddUi(player, container);
        }

        float GetAdditionalEnhancementCost(Perk perk)
        {
            float value;
            if (config.enhancementSettings.enhancement_kit_settings.upgrade_settings.additional_upgrade_cost.TryGetValue(perk, out value)) return value;
            return 0;
        }

        [ConsoleCommand("ipconfirmenhance")]
        void ConfirmEnhancement(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            var perkItem = player.inventory.FindItemByUID(new ItemId(Convert.ToUInt64(arg.Args[0])));
            var selectedItem = player.inventory.FindItemByUID(new ItemId(Convert.ToUInt64(arg.Args[1])));

            var perk = GetEnhancementKitPerk(perkItem.text);

            CloseEnhancementMenu(player);

            var reason = PassWhiteBlacklistCheck(selectedItem, perk);
            if (!string.IsNullOrEmpty(reason))
            {
                PrintToChat(player, reason);
                return;
            }

            float cost;
            if (config.enhancementSettings.enhancement_kit_settings.upgrade_settings.additional_upgrade_cost.TryGetValue(perk, out cost))
            {
                switch (config.enhancementSettings.enhancement_kit_settings.upgrade_settings.upgrade_cost_type)
                {
                    case "economics":
                        if (Economics != null && Economics.IsLoaded)
                        {
                            var balance = Convert.ToDouble(Economics.Call("Balance", player.UserIDString));
                            if (balance < cost)
                            {
                                PrintToChat(player, lang.GetMessage("Eh_econ_NotEnough", this, player.UserIDString));
                                return;
                            }                            
                            if (!Convert.ToBoolean(Economics.Call("Withdraw", player.UserIDString, Convert.ToDouble(cost))))
                            {
                                PrintToChat(player, lang.GetMessage("Eh_econ_Error", this, player.UserIDString));
                                return;
                            }
                        }
                        break;

                    case "serverrewards":
                        if (ServerRewards != null && ServerRewards.IsLoaded)
                        {
                            var balance = Convert.ToInt32(ServerRewards.Call("CheckPoints", player.userID));
                            if (balance < cost)
                            {
                                PrintToChat(player, lang.GetMessage("Eh_SRP_NotEnough", this, player.UserIDString));
                                return;
                            }
                            if (!Convert.ToBoolean(ServerRewards.Call("TakePoints", player.userID, Convert.ToInt32(cost))))
                            {
                                PrintToChat(player, lang.GetMessage("Eh_SRP_Error", this, player.UserIDString));
                                return;
                            }
                        }
                        break;
                    case "custom":
                        if (config.enhancementSettings.enhancement_kit_settings.upgrade_settings.custom_item == null || string.IsNullOrEmpty(config.enhancementSettings.enhancement_kit_settings.upgrade_settings.custom_item.shortname))
                        {
                            PrintToChat(player, "ERROR - Custom item has not been setup. Please contact Admin.");
                            return;
                        }

                        var customCost = Convert.ToInt32(cost);
                        var found = 0;
                        foreach (var item in player.inventory.AllItems())
                        {
                            if (item.info.shortname.Equals(config.enhancementSettings.enhancement_kit_settings.upgrade_settings.custom_item.shortname, StringComparison.InvariantCultureIgnoreCase) && item.skin == config.enhancementSettings.enhancement_kit_settings.upgrade_settings.custom_item.skin) found += item.amount;
                            if (found >= customCost) break;
                        }

                        if (found < customCost)
                        {
                            PrintToChat(player, string.Format(lang.GetMessage("Eh_Custom_NotEnough", this, player.UserIDString), lang.GetMessage("Custom", this, player.UserIDString)));
                            return;
                        }

                        found = 0;
                        foreach (var item in player.inventory.AllItems())
                        {
                            if (!item.info.shortname.Equals(config.enhancementSettings.enhancement_kit_settings.upgrade_settings.custom_item.shortname, StringComparison.InvariantCultureIgnoreCase) || item.skin != config.enhancementSettings.enhancement_kit_settings.upgrade_settings.custom_item.skin) continue;
                            if (item.amount <= customCost - found)
                            {
                                found += item.amount;
                                item.Remove();
                            }
                            else
                            {
                                item.UseItem(customCost - found);
                                found = customCost;
                            }
                            if (found >= cost) break;
                        }
                        break;

                    default:
                        var scrapCost = Convert.ToInt32(cost);
                        if (player.inventory.AllItems().Where(x => x.info.shortname == "scrap").Sum(x => x.amount) < scrapCost)
                        {
                            PrintToChat(player, lang.GetMessage("Eh_Scrap_NotEnough", this, player.UserIDString));
                            return;
                        } 
                        var count = 0;
                        
                        foreach (var scrap in player.inventory.AllItems())
                        {
                            if (count >= scrapCost) break;
                            if (scrap.info.shortname != "scrap") continue;
                            if (scrap.amount >= scrapCost - count)
                            {
                                scrap.UseItem(scrapCost - count);
                                count = scrapCost;
                                break;
                            }
                            else
                            {
                                count += scrap.amount;
                                scrap.UseItem(scrap.amount);
                            }
                        }                        
                        break;
                }
            }

            perkItem.UseItem(1);

            EnhanceExistingItem(player, selectedItem, perk);
        }        

        void EnhanceExistingItem(BasePlayer player, Item item, Perk perk)
        {
            PerkSettings ps;
            if (!config.enhancementSettings.perk_settings.TryGetValue(perk, out ps))
            {
                Puts($"Critical error: Failed to find the perk data for {perk} so we could not create the item for {player.displayName}");
                return;
            }
            bool shouldGive = false;
            Item itemToMod;
            if (item.amount > 1)
            {
                itemToMod = item.SplitItem(1);
                shouldGive = true;
            }
            else itemToMod = item;

            var mod = UnityEngine.Random.Range(ps.min_mod, ps.max_mod);

            if (itemToMod.text == null) itemToMod.text = $"[{perk} {mod}]";
            else itemToMod.text += $"[{perk} {mod}]";

            if (!string.IsNullOrEmpty(config.enhancementSettings.item_name_prefix) && string.IsNullOrEmpty(itemToMod.name)) itemToMod.name = $"{config.enhancementSettings.item_name_prefix} {itemToMod.info.displayName.english}";

            //if (!string.IsNullOrEmpty(item.text) && HasPerks(item.text)) item.text += $"[{perk} {mod}]";
            //else
            //{
            //    item.text = $"[{perk} {mod}]";
            //    if (!string.IsNullOrEmpty(config.enhancementSettings.item_name_prefix) && string.IsNullOrEmpty(item.name)) item.name = $"{config.enhancementSettings.item_name_prefix} {item.info.displayName.english}";
            //}
            itemToMod.MarkDirty();
            if (shouldGive) player.GiveItem(itemToMod);

            NotifyPlayer(player, itemToMod, false);
            RunEffect(player, config.enhancementSettings.enhancement_kit_settings.upgrade_settings.enhance_effect, player.transform.position);
        }

        string PassWhiteBlacklistCheck(Item item, Perk perk)
        {
            PerkSettings data;
            if (!config.enhancementSettings.perk_settings.TryGetValue(perk, out data)) return "Could not find perk data in the config.";
            if (!data.whitelist.IsNullOrEmpty() && !data.whitelist.Contains(item.info.shortname)) return $"Failed to enhance item: You can only apply this perk to the following items: {string.Join(", ", data.whitelist)}.";
            if (!data.blacklist.IsNullOrEmpty() && data.blacklist.Contains(item.info.shortname)) return $"Failed to enhance item: Item black listed.";
            return null;
        }

        #endregion

        #endregion

        #endregion

        #region Enhancement Kits

        static bool IsEnhancementKit(string text)
        {            
            return (parsedEnums.ContainsKey(text)) ;
        }

        Perk GetEnhancementKitPerk(string text)
        {
            Perk perk;
            if (parsedEnums.TryGetValue(text, out perk)) return perk;
            else return Perk.None;
        }

        Item CreateEnhancementKit(Perk perk)
        {
            var result = ItemManager.CreateByName(config.enhancementSettings.enhancement_kit_settings.shortname, 1, config.enhancementSettings.enhancement_kit_settings.skin);
            if (result == null) return null;

            if (config.enhancementSettings.enhancement_kit_settings.recycle_settings.random_perk)
            {
                var randomPerk = GetRandomPerk;
                result.text = randomPerk.ToString();
                result.name = config.enhancementSettings.enhancement_kit_settings.displayName + $" {randomPerk}";
            }
            else
            {
                result.text = perk.ToString();
                result.name = config.enhancementSettings.enhancement_kit_settings.displayName + $" {perk}";
            }
            
            return result;
        }

        #endregion

        #region Commands

        [ChatCommand("ipgive")]
        void GiveItemChatCommand(BasePlayer player, string cmd, string[] args)
        {
            GiveItemCMD(player, args);
        }

        [ConsoleCommand("ipgive")]
        void GiveItemConsoleCommand(ConsoleSystem.Arg arg)
        {
            GiveItemCMD(arg.Player(), arg.Args, true);
        }

        [ConsoleCommand("ipgivewithskin")]
        void GiveItemWithSkinConsoleCommand(ConsoleSystem.Arg arg)
        {
            GiveItemWithSkinCMD(arg.Player(), arg.Args, true);
        }

        [ChatCommand("ipgivekit")]
        void IPGivePerkKitChatCMD(BasePlayer player, string cmd, string[] args)
        {
            if (!HasPerms(player, perm_admin)) return;

            if (args == null || args.Length == 0)
            {
                PrintToChat(player, lang.GetMessage("KitChatUsage", this, player.UserIDString));
                return;
            }

            IPGiveKit(player, player, args[0], false);

        }

        // Target, Perk
        [ConsoleCommand("ipgivekit")]
        void IPGivePerkKitConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !HasPerms(player, perm_admin)) return;

            if (arg.Args == null || arg.Args.Length < 1)
            {
                arg.ReplyWith(lang.GetMessage("KitConsoleUsageRevised", this, player?.UserIDString ?? null));
                return;
            }

            var target = BasePlayer.FindAwakeOrSleeping(arg.Args[0]);
            if (target == null)
            {
                var players = FindPlayersWithName(arg.Args[0]);
                if (players.Count == 1) target = players[0];
                else if (players.Count > 1) arg.ReplyWith(string.Format(lang.GetMessage("TooManyNameMatches", this, player?.UserIDString ?? null), string.Join("\n- ", players.Select(x => x.displayName))));
                Pool.FreeList(ref players);
            }
            if (target == null)
            {
                arg.ReplyWith(string.Format(lang.GetMessage("FailedTofindPlayer", this, player?.UserIDString ?? null), arg.Args[0]));
                return;
            }

            IPGiveKit(player, target, arg.Args.Length > 1 ? arg.Args[1] : string.Empty, true);
        }

        List<BasePlayer> FindPlayersWithName(string name)
        {
            List<BasePlayer> players = Pool.GetList<BasePlayer>();
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.IsDead() || !player.displayName.Contains(name)) continue;
                players.Add(player);
            }
            return players;
        }

        void IPGiveKit(BasePlayer giver, BasePlayer receiver, string perkString, bool fromConsole)
        {
            Perk perk;
            if (!parsedEnums.TryGetValue(perkString, out perk))
            {
                perk = GetRandomPerk;
                //LogToFile("IPGiveKit_Log", $"Selecting random perk: {perk} for {receiver.displayName} [PerkString: {perkString}] [Console: {fromConsole}].", this, false, true);
            }
            var item = CreateEnhancementKit(perk);

            receiver.GiveItem(item);
            PrintToChat(receiver, string.Format(lang.GetMessage("ReceivedKit", this, receiver.UserIDString), item.name));
            if (giver != null)
            {
                if (giver == receiver) return;
                if (fromConsole) PrintToConsole(giver, string.Format(lang.GetMessage("GaveKitConsoleReply", this, giver.UserIDString), item.name, receiver.displayName));
                else PrintToChat(giver, string.Format(lang.GetMessage("GaveKitConsoleReply", this, giver.UserIDString), item.name, receiver.displayName));
            }
            else Puts(string.Format(lang.GetMessage("GaveKitConsoleReply", this), item.name, receiver.displayName));
        }

        [ConsoleCommand("ipupdateicon")]
        void ForceIconUpdateCMD(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, perm_admin)) return;
            foreach (var kvp in pcdData.pEntity)
            {
                kvp.Value.iconSettings.anchor_min = config.playerSettings.iconSettings.anchor_min;
                kvp.Value.iconSettings.anchor_max = config.playerSettings.iconSettings.anchor_max;
                kvp.Value.iconSettings.offset_x = config.playerSettings.iconSettings.offset_x;
                kvp.Value.iconSettings.offset_y = config.playerSettings.iconSettings.offset_y;
                kvp.Value.iconSettings.size = config.playerSettings.iconSettings.size;
            }

            foreach (var p in BasePlayer.activePlayerList)
            {
                GetPlayerPerks(p);
            }

            arg.ReplyWith("Updated icon positions.");
        }

        Perk GetRandomPerk
        {
            get
            {
                List<Perk> perks = Pool.GetList<Perk>();
                perks.AddRange(parsedEnums.Values);
                Perk result = perks.GetRandom();

                Pool.FreeList(ref perks);
                return result;
            }            
        }

        void GiveItemCMD(BasePlayer player, string[] args, bool console = false)
        {
            if (player != null && !HasPerms(player, perm_admin)) return;

            if (args == null || args.Length < 1)
            {
                if (player != null)
                {
                    if (console) PrintToConsole(player, lang.GetMessage("IPGive_Usage", this, player.UserIDString));
                    else PrintToChat(player, lang.GetMessage("IPGive_Usage", this, player.UserIDString));
                }                    
                else Puts(lang.GetMessage("IPGive_Usage", this));
                return;
            }

            var target = BasePlayer.Find(args[0]);
            if (target == null) target = FindPlayerByName(args[0]);

            if (target == null || !target.IsConnected || target.IsDead())
            {
                if (player != null) PrintToChat(player, string.Format(lang.GetMessage("FailedTofindPlayer", this, player.UserIDString), target?.displayName ?? args[0]));
                else Puts(string.Format(lang.GetMessage("FailedTofindPlayer", this), target?.displayName ?? args[0]));
                return;
            }

            List<KeyValuePair<Perk, float>> perks = null;
            string shortname = null;

            if (args.Length > 1)
            {
                if (parsedEnums.ContainsKey(args[1]) || args[1].Equals("random", StringComparison.OrdinalIgnoreCase)) perks = ParsePerks(args.Skip(1));
                else
                {
                    if (ItemManager.FindItemDefinition(args[1].ToLower())) shortname = args[1].ToLower();
                    perks = ParsePerks(args.Skip(2));
                }
            }

            GiveItemWithPerks(target, shortname, perks);

            if (perks != null) Pool.FreeList(ref perks);
        }

        void GiveItemWithSkinCMD(BasePlayer player, string[] args, bool console = false)
        {
            if (player != null && !HasPerms(player, perm_admin)) return;

            if (args == null || args.Length < 1)
            {
                if (player != null)
                {
                    if (console) PrintToConsole(player, lang.GetMessage("IPWithSkinGive_Usage", this, player.UserIDString));
                    else PrintToChat(player, lang.GetMessage("IPWithSkinGive_Usage", this, player.UserIDString));
                }                    
                else Puts(lang.GetMessage("IPWithSkinGive_Usage", this));
                return;
            }

            var target = BasePlayer.Find(args[0]);
            if (target == null) target = FindPlayerByName(args[0]);

            if (target == null || !target.IsConnected || target.IsDead())
            {
                if (player != null) PrintToChat(player, string.Format(lang.GetMessage("FailedTofindPlayer", this, player.UserIDString), target?.displayName ?? args[0]));
                else Puts(string.Format(lang.GetMessage("FailedTofindPlayer", this), target?.displayName ?? args[0]));
                return;
            }

            if (!args[1].IsNumeric())
            {
                if (player != null) PrintToChat(player, $"{args[1]} is not a valid skin.");
                return;
            }
            var skin = Convert.ToUInt64(args[1]);

            List<KeyValuePair<Perk, float>> perks = null;
            string shortname = null;

            if (args.Length > 2)
            {
                if (parsedEnums.ContainsKey(args[2]) || args[2].Equals("random", StringComparison.OrdinalIgnoreCase)) perks = ParsePerks(args.Skip(2));
                else
                {
                    if (ItemManager.FindItemDefinition(args[2].ToLower())) shortname = args[2].ToLower();
                    perks = ParsePerks(args.Skip(3));
                }
            }

            GiveItemWithPerks(target, shortname, perks, skin);

            if (perks != null) Pool.FreeList(ref perks);
        }

        private BasePlayer FindPlayerByName(string Playername)
        {
            var lowered = Playername.ToLower();
            var targetList = BasePlayer.allPlayerList.Where(x => x.displayName.ToLower().Contains(lowered)).OrderBy(x => x.displayName.Length);
            if (targetList.Count() == 1)
            {
                return targetList.First();
            }
            if (targetList.Count() > 1)
            {
                if (targetList.First().displayName.Equals(Playername, StringComparison.OrdinalIgnoreCase))
                {
                    return targetList.First();
                }
                return null;
            }
            if (targetList.Count() == 0)
            {
                return null;
            }
            return null;
        }

        List<KeyValuePair<Perk, float>> ParsePerks(IEnumerable<string> args)
        {
            List<KeyValuePair<Perk, float>> result = Pool.GetList<KeyValuePair<Perk, float>>();
            
            List<string> parsedArgs = Pool.GetList<string>();
            parsedArgs.AddRange(args);
            
            for (int i = 0; i < parsedArgs.Count; i++)
            {
                float floatValue;
                if (float.TryParse(parsedArgs[i], out floatValue))
                {
                    continue;
                }
                float value = i + 1 < parsedArgs.Count && float.TryParse(parsedArgs[i + 1], out floatValue) && floatValue > 0 ? floatValue : 0;

                if (parsedArgs[i].Equals("random", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(new KeyValuePair<Perk, float>(PerksList.GetRandom(), value));
                }
                Perk _perk;
                if (parsedEnums.TryGetValue(parsedArgs[i], out _perk)) result.Add(new KeyValuePair<Perk, float>(_perk, value));
            }
            Pool.FreeList(ref parsedArgs);
            return result;
        }

        void GiveItemWithPerks(BasePlayer target, string shortname, List<KeyValuePair<Perk, float>> perks, ulong skin = 0)
        {
            Item item = CreateItem(shortname, 0, perks);
            if (skin > 0)
            {
                item.skin = skin;
                var heldEntity = item.GetHeldEntity();
                if (heldEntity != null) heldEntity.skinID = skin;
                item.MarkDirty();
            }

            if (item != null)
            {
                target.GiveItem(item);
            }
        }

        #endregion

        #region Monobehaviours

        #region Health Regen

        private static Dictionary<BasePlayer, float> RegenAmount;
        public static Dictionary<ulong, float> took_damage;

        bool HasRegen(BasePlayer player)
        {
            return player.GetComponent<Regen>() != null;
        }

        void UpdateRegen(BasePlayer player, float value)
        {
            DestroyRegen(player);
            RegenAmount.Add(player, value);
            player.gameObject.AddComponent<Regen>();
        }

        static void DestroyRegen(BasePlayer player)
        {
            if (player == null) return;
            if (RegenAmount != null && RegenAmount.ContainsKey(player)) RegenAmount.Remove(player);
            var gameObject = player.GetComponent<Regen>();
            if (gameObject != null) GameObject.DestroyImmediate(gameObject);
        }        

        void AddRegenDelay(BasePlayer player)
        {
            if (config.enhancementSettings.perk_config.regenerationSettings.damage_delay <= 0) return;
            if (took_damage == null || !took_damage.ContainsKey(player.userID))
            {
                took_damage.Add(player.userID, Time.time + config.enhancementSettings.perk_config.regenerationSettings.damage_delay);
                PrintToChat(player, string.Format(lang.GetMessage("DisabledRegen", this, player.UserIDString), config.enhancementSettings.perk_config.regenerationSettings.damage_delay));
            }
            else
            {
                took_damage[player.userID] = Time.time + config.enhancementSettings.perk_config.regenerationSettings.damage_delay;
            }
        }

        public class Regen : MonoBehaviour
        {
            private BasePlayer player;
            private float regenDelay;
            private float _regenAmount;

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                regenDelay = Time.time + 1f;
                _regenAmount = RegenAmount[player];
            }

            public void FixedUpdate()
            {
                if (player == null) return;
                if (regenDelay < Time.time)
                {
                    if (took_damage.ContainsKey(player.userID))
                    {
                        if (took_damage[player.userID] > Time.time) return;
                        else
                        {
                            took_damage.Remove(player.userID);
                            if (!HasRegenPerk(player))
                            {
                                DestroyRegen(player);
                                return;
                            }
                        }                        
                    }                    
                    regenDelay = Time.time + 1f;
                    DoRegen();
                }
            }

            public void DoRegen()
            {
                if (player == null || !player.IsConnected || !player.IsAlive() || player.health == player.MaxHealth()) return;
                player.Heal(_regenAmount);
            }

            private void OnDestroy()
            {
                enabled = false;
                CancelInvoke();
            }
        }

        static bool HasRegenPerk(BasePlayer player)
        {
            PlayerPerks perks;
            if (Player_perks.TryGetValue(player.userID, out perks) && perks.perk_modifiers.ContainsKey(Perk.Regeneration)) return true;
            return false;
        }

        #endregion

        #endregion

        #region Subscriptions

        Dictionary<string, List<Perk>> Subscriptions = new Dictionary<string, List<Perk>>()
        {
            ["OnEntityDeath"] = new List<Perk>() { Perk.Attractive },
            ["OnOvenToggle"] = new List<Perk>() { Perk.Smelter },
            ["OnPlayerAddModifiers"] = new List<Perk>() { Perk.Sated, Perk.IronStomach },
            ["OnFishCatch"] = new List<Perk>() { Perk.FishingLuck },
            ["OnLoseCondition"] = new List<Perk>() { Perk.Durable },
            ["OnItemCraft"] = new List<Perk>() { Perk.Manufacture },
            ["OnPlayerRevive"] = new List<Perk>() { Perk.Paramedic },
            ["CanCastFishingRod"] = new List<Perk>() { Perk.LineStrength },
            ["OnItemUse"] = new List<Perk>() { Perk.Prepper },
            ["OnMealConsumed"] = new List<Perk>() { Perk.Prepper },
            ["OnPayForUpgrade"] = new List<Perk>() { Perk.Builder },
            ["OnPlayerHealthChange"] = new List<Perk>() { Perk.Pharmaceutical, Perk.HealShare },
            ["OnResearchCostDetermine"] = new List<Perk>() { Perk.Academic },
            ["OnCollectiblePickup"] = new List<Perk>() { Perk.Forager },
            ["OnGrowableGathered"] = new List<Perk>() { Perk.Horticulture },
            ["CanCatchFish"] = new List<Perk>() { Perk.Angler },
            ["OnDispenserGather"] = new List<Perk>() { Perk.Lumberjack, Perk.TreePlanter, Perk.Deforest, Perk.WoodcuttingLuck, Perk.Prospector, Perk.RockCycler, Perk.BlastMine, Perk.MiningLuck, Perk.Butcher, Perk.Tanner, Perk.SkinningLuck },
            ["OnDispenserBonus"] = new List<Perk>() { Perk.Lumberjack, Perk.TreePlanter, Perk.Deforest, Perk.WoodcuttingLuck, Perk.Prospector, Perk.RockCycler, Perk.BlastMine, Perk.MiningLuck, Perk.Butcher, Perk.Tanner, Perk.SkinningLuck },
        };

        Dictionary<string, bool> IsSubscribed = new Dictionary<string, bool>();
        void SetupSubscriptions()
        {
            foreach (var sub in Subscriptions)
            {
                if (!IsSubscribed.ContainsKey(sub.Key))
                    IsSubscribed.Add(sub.Key, false);

                DoUnSubscribe(sub.Key);
            }
        }

        void DoUnSubscribe(string hook)
        {
            //Puts($"Unsubbing: {hook}");
            Unsubscribe(hook);
            IsSubscribed[hook] = false;
        }

        void DoSubscribe(string hook)
        {
            //Puts($"Subbing: {hook}");
            Subscribe(hook);
            IsSubscribed[hook] = true;
        }

        Dictionary<Perk, List<ulong>> PlayersSubscribed = new Dictionary<Perk, List<ulong>>();

        void HandlePlayerSubscription(BasePlayer player, List<Perk> perks)
        {
            List<Perk> perks_to_remove = Pool.GetList<Perk>();
            foreach (var perk in perks)
            {
                if (!PlayersSubscribed.ContainsKey(perk))
                    PlayersSubscribed.Add(perk, new List<ulong>());
            }
            foreach (var perk in PlayersSubscribed)
            {
                // Adds and removes the player from perk subscriptions.
                if (!perks.Contains(perk.Key)) perk.Value.Remove(player.userID);
                else if (!perk.Value.Contains(player.userID)) perk.Value.Add(player.userID);

                if (perk.Value.Count == 0) perks_to_remove.Add(perk.Key);
            }
            // Removes perks from PlayersSubscribed that do not have any players listed.
            foreach (var perk in perks_to_remove)
                PlayersSubscribed.Remove(perk);
            Pool.FreeList(ref perks_to_remove);
            CheckSubscriptions();
        }

        void CheckSubscriptions()
        {
            foreach (var sub in Subscriptions)
            {
                bool shouldUnsub = true;
                foreach (var perk in sub.Value)
                {
                    List<ulong> subs;
                    if (PlayersSubscribed.TryGetValue(perk, out subs) && subs.Count > 0)
                    {
                        if (!IsSubscribed[sub.Key])
                            DoSubscribe(sub.Key);
                        shouldUnsub = false;
                        break;
                    }
                }
                if (shouldUnsub)
                    if (IsSubscribed[sub.Key])
                        DoUnSubscribe(sub.Key);
            }
        }

        #endregion

        #region API

        object STOnItemRepairWithMaxRepair(Item item)
        {
            if (item != null && !string.IsNullOrEmpty(item.text) && HasPerks(item.text)) return true;
            return null;
        }

        object STCanGainXP(BasePlayer player, BaseEntity sourceEntity)
        {
            return false;
        }

        Dictionary<ulong, KeyValuePair<string, string>> Skinbox_Item_Tracking = new Dictionary<ulong, KeyValuePair<string, string>>();

        string SB_CanReskinItem(BasePlayer player, Item item, ulong newSkinID)
        {
            if (Skinbox_Item_Tracking.ContainsKey(player.userID))
            {
                NextFrame(() =>
                {
                    item.text = Skinbox_Item_Tracking[player.userID].Key;
                    item.name = Skinbox_Item_Tracking[player.userID].Value;
                    player.SendNetworkUpdateImmediate();
                    Skinbox_Item_Tracking.Remove(player.userID);
                });
            }

            return null;
        }

        string SB_CanAcceptItem(BasePlayer player, Item item)
        {
            if (item.text != null && HasPerks(item.text))
            {
                if (!Skinbox_Item_Tracking.ContainsKey(player.userID)) Skinbox_Item_Tracking.Add(player.userID, new KeyValuePair<string, string>(item.text, item.name));
                else Skinbox_Item_Tracking[player.userID] = new KeyValuePair<string, string>(item.text, item.name);
            }

            return null;
        }

        #region RandomTrader

        public class RandomTraderAPI
        {
            [JsonProperty("CopyPaste file name to use")]
            public string copypaste_file_name = "ItemPerksStore";

            [JsonProperty("Shop name")]
            public string shop_name = "Item Perks Shop";

            [JsonProperty("Shop purchase limit")]
            public int shop_purchase_limit = 2;

            [JsonProperty("How many random items from the list will be picked?")]
            public int shop_display_amount = 8;

            [JsonProperty("Minimum quantity of an item that the shop will stock")]
            public int min_stock_quantity = 1;

            [JsonProperty("Maximum quantity of an item that the shop will stock")]
            public int max_stock_quantity = 2;

            [JsonProperty("Minimum cost of an item")]
            public int min_cost = 100;

            [JsonProperty("Maximum cost of an item")]
            public int max_cost = 1000;

            [JsonProperty("Skin the items?")]
            public bool skin_item = true;

            [JsonProperty("How many rolls of each enhancement should we add?")]
            public int quantity_of_enhancements = 10;
        }

        public class RTItem
        {
            public string shortname;
            public ulong skin;
            public int minQuantity;
            public int maxQuantity;
            public int minCost;
            public int maxCost;
            public string itemDisplayName;
            public string shopDisplayName;
            public string url;
            public string text;

            public RTItem(string shortname, ulong skin, int minQuantity, int maxQuantity, int minCost, int maxCost, string itemDisplayName, string shopDisplayName, string url, string text)
            {
                this.shortname = shortname;
                this.skin = skin;
                this.minQuantity = minQuantity;
                this.maxQuantity = maxQuantity;
                this.minCost = minCost;
                this.maxCost = maxCost;
                this.itemDisplayName = itemDisplayName;
                this.shopDisplayName = shopDisplayName;
                this.url = url;
                this.text = text;
            }
        }

        void AddItemPerks()
        {
            object[] generlSettingsOjb = new object[] { config.thirdpartyPluginSettings.random_trader_api.copypaste_file_name, config.thirdpartyPluginSettings.random_trader_api.shop_name, config.thirdpartyPluginSettings.random_trader_api.shop_purchase_limit, config.thirdpartyPluginSettings.random_trader_api.shop_display_amount };

            List<object[]> item_objects = Pool.GetList<object[]>();

            foreach (var kvp in config.enhancementSettings.perk_settings)
            {
                for (int i = 0; i < config.thirdpartyPluginSettings.random_trader_api.quantity_of_enhancements; i++)
                {
                    List<Perk> _perks = Pool.GetList<Perk>();
                    _perks.Add(kvp.Key);
                    var shortname = GetShortnameForPerk(_perks);
                    Pool.FreeList(ref _perks);

                    ItemDefinition def = ItemManager.FindItemDefinition(shortname);
                    if (def == null)
                    {
                        Puts($"Found invalid shortname {shortname} - skipping.");
                        continue;
                    }

                    List<ulong> skins;
                    ulong skin = config.thirdpartyPluginSettings.random_trader_api.skin_item && config.enhancementSettings.item_skins.TryGetValue(shortname, out skins) ? skins.GetRandom() : 0;

                    int minQuantity = config.thirdpartyPluginSettings.random_trader_api.min_stock_quantity;
                    int maxQuantity = config.thirdpartyPluginSettings.random_trader_api.max_stock_quantity;

                    int minCost = config.thirdpartyPluginSettings.random_trader_api.min_cost;
                    int maxCost = config.thirdpartyPluginSettings.random_trader_api.max_cost;

                    string itemDisplayName = !string.IsNullOrEmpty(config.enhancementSettings.item_name_prefix) ? config.enhancementSettings.item_name_prefix + " " + def.displayName.english : def.displayName.english;
                    string shopDisplayName = def.displayName.english + $" <color=#00ffb2>[{lang.GetMessage("UI"+kvp.Key.ToString(), this)}]</color>";
                    
                    double mod = Math.Round(UnityEngine.Random.Range(kvp.Value.min_mod, kvp.Value.max_mod), 3);
                    string text = $"[{kvp.Key} {mod}]";

                    item_objects.Add(new object[]
                    {
                        shortname,
                        skin,
                        minQuantity,
                        maxQuantity,
                        minCost,
                        maxCost,
                        itemDisplayName,
                        shopDisplayName,
                        new KeyValuePair<string, string>(),
                        text
                    });
                }
            }

            RandomTrader.Call("RTAddStore", this.Name, generlSettingsOjb, item_objects);
            Pool.FreeList(ref item_objects);
        }

        void RandomTraderReady(List<string> current_stores)
        {
            if (current_stores == null || !current_stores.Contains(this.Name)) AddItemPerks();
        }

        #endregion

        #endregion

        #region Harmony patch

#if CARBON
        private Harmony _harmony;
#else
        private HarmonyInstance _harmony;
#endif

        private void Loaded()
        {
#if CARBON
            _harmony = new Harmony(Name + "Patch");
#else
            _harmony = HarmonyInstance.Create(Name + "Patch");
#endif
            _harmony.Patch(AccessTools.Method(typeof(Item), "MaxStackable"), new HarmonyMethod(typeof(Item_MaxStackable_Patch), "Prefix"));
            _harmony.Patch(AccessTools.Method(typeof(Item), "SplitItem"), new HarmonyMethod(typeof(Item_SplitItem_Patch), "Prefix"));
            _harmony.Patch(AccessTools.Method(typeof(Item), "CanStack"), new HarmonyMethod(typeof(Item_CanStack_Patch), "Prefix"));
            _harmony.Patch(AccessTools.Method(typeof(DroppedItem), "OnDroppedOn"), new HarmonyMethod(typeof(CombineDroppedItem_Patch), "Prefix"));
            if (config.enhancementSettings.prevent_max_condition_loss) _harmony.Patch(AccessTools.Method(typeof(Item), "DoRepair"), new HarmonyMethod(typeof(Item_DoRepair_Patch), "Postfix"));
        }
        
        [HarmonyPatch(typeof(Item), "MaxStackable")]
        internal class Item_MaxStackable_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(Item __instance, ref int __result)
            {
                if (__instance == null) return true;

                if (string.IsNullOrEmpty(__instance.text) || string.IsNullOrWhiteSpace(__instance.text)) return true;
                var arr = __instance.text.Split('[', ']');

                if (arr == null || arr.Length <= 1) return true;

                foreach (var entry in arr.Skip(1))
                {
                    var split = entry.Split(' ');
                    if (split.Length <= 1) continue;

                    if (parsedEnums.ContainsKey(split[0]))
                    {
                        __result = 1;
                        return false;
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Item), "SplitItem", typeof(int))]
        internal class Item_SplitItem_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(Item __instance, int split_Amount,  ref Item __result)
            {
                if (__instance == null || !IsEnhancementKit(__instance)) return true;

                Assert.IsTrue(split_Amount > 0, "split_Amount <= 0");
                if (split_Amount <= 0)
                {
                    return true;
                }
                if (split_Amount >= __instance.amount)
                {
                    return true;
                }
                __instance.amount -= split_Amount;
                var splitItem = ItemManager.CreateByItemID(__instance.info.itemid, 1, 0ul);
                splitItem.amount = split_Amount;
                splitItem.skin = __instance.skin;
                splitItem.text = __instance.text;
                splitItem.name = __instance.name;
                __result = splitItem;

                return false;
            }
        }

        [HarmonyPatch(typeof(Item), "CanStack", typeof(Item), typeof(Item))]
        internal class Item_CanStack_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(Item __instance, Item item, ref bool __result)
            {
                if (__instance == null || item == null || (string.IsNullOrEmpty(item.text) && string.IsNullOrEmpty(__instance.text))) return true;

                if ((IsEnhancementKit(item) || IsEnhancementKit(__instance)) || (HasPerks(item.text) || HasPerks(__instance.text)))
                {
                    if (item.text != __instance.text) return false;
                }
                return true;                
            }
        }

        [HarmonyPatch(typeof(DroppedItem), "OnDroppedOn", typeof(DroppedItem), typeof(DroppedItem))]
        internal class CombineDroppedItem_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(DroppedItem __instance, DroppedItem di)
            {
                if (__instance == null || __instance.item == null || di == null || di.item == null) return true;
                if (string.IsNullOrEmpty(__instance.item.text) && string.IsNullOrEmpty(di.item.text)) return true;
                if ((IsEnhancementKit(di.item) || IsEnhancementKit(__instance.item)) || (HasPerks(di.item.text) || HasPerks(__instance.item.text)))
                {
                    if (di.item.text != __instance.item.text) return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Item), "DoRepair", typeof(Item), typeof(float))]
        internal class Item_DoRepair_Patch
        {
            [HarmonyPrefix]
            private static void Postfix(Item __instance, float maxLossFraction)
            {
                if (__instance == null || string.IsNullOrEmpty(__instance.text) || string.IsNullOrWhiteSpace(__instance.text) || !__instance.hasCondition) return;

                var arr = __instance.text.Split('[', ']');

                if (arr == null || arr.Length <= 1) return;

                foreach (var entry in arr.Skip(1))
                {
                    var split = entry.Split(' ');
                    if (split.Length <= 1) continue;

                    if (parsedEnums.ContainsKey(split[0]))
                    {
                        __instance.maxCondition = __instance.info.Blueprint.targetItem.condition.max;
                        __instance.condition = __instance.info.Blueprint.targetItem.condition.max;
                        return;
                    }
                }
            }
        }

        #endregion
    }
}
