using Facepunch;
using Facepunch.Utility;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Oxide.Plugins;
using HarmonyLib;


/* Look at adding NPC Talking. When a player interacts with it, it goes to sleep and they loot it, so it can be dressed.
 * 
 */

/* 1.0.25 changes
 * Updated for July's surprise patch.
 */

namespace Oxide.Plugins
{
    [Info("DeployableNature", "imthenewguy", "1.0.26")]
    [Description("Makes nature deployable.")]
    class DeployableNature : RustPlugin
    {
        #region Config

        private Configuration config;
        public class Configuration
        {
            [JsonProperty("Maximum number of rocks that a player can deploy [0 = no limit]")]
            public int max_rocks = 0;

            [JsonProperty("Maximum number of trees that a player can deploy [0 = no limit]")]
            public int max_trees = 0;

            [JsonProperty("Maximum number of bushes that a player can deploy [0 = no limit]")]
            public int max_bushes = 0;

            [JsonProperty("Maximum number of animals that a player can deploy [0 = no limit]")]
            public int max_animals = 3;

            [JsonProperty("Maximum number of furniture that a player can deploy [0 = no limit]")]
            public int max_furniture = 0;

            [JsonProperty("Mining node drop rate [%] - [0 = off]")]
            public int drop_rate_from_node = 1;

            [JsonProperty("Tree drop rate [%] - [0 = off]")]
            public int drop_rate_from_tree = 1;

            [JsonProperty("Animal drop rate [%] - [0 = off]")]
            public int drop_rate_from_animal = 1;

            [JsonProperty("Roll the chance on each hit [false - will only roll once per tree/node/corpse]")]
            public bool roll_chance_on_each_hit = true;

            [JsonProperty("Animals only drop from their corpse type [bear only drops from harvesting bear corpses]?")]
            public bool animal_specific_drops = true;

            [JsonProperty("Prevent wild animals from targeting and killing deployed animals?")]
            public bool prevent_animal_targeting = true;

            [JsonProperty("Collectable plant drop rate [%] - [0 = off]")]
            public int drop_rate_from_collectables = 5;

            [JsonProperty("Chance for loot to be added to a minecart when it spawns [%] - [0 = off]")]
            public int drop_rate_from_minecart = 10;

            [JsonProperty("Allow players to hold sprint while placing a rock to have it embed into the ground?")]
            public bool shift_place = true;

            [JsonProperty("How much deeper should the rock sink when shift-placing?")]
            public float depth_modifier = 0.5f;

            [JsonProperty("Currency to use [SCRAP, ECONOMICS, SR, CUSTOM]")]
            public string currency = "SCRAP";

            [JsonProperty("Custom currency details")]
            public CustomCurrency custom_currency = new CustomCurrency();

            [JsonProperty("Enable players to buy prfabs from the market?")]
            public bool market_enabled = true;

            [JsonProperty("Nature market command")]
            public string[] market_cmds = new string[] { };

            [JsonProperty("How often should the chat message post telling players how to access the deployable nature market? (seconds) 0 = off")]
            public float chat_delay = 600f;

            [JsonProperty("Prevent deployable items from being recycled")]
            public bool prevent_recycling = true;

            [JsonProperty("Display a chat message when a player pulls out a hammer for the first time, reminding them that they can remove deployables")]
            public bool notify_player_with_hammer = false;

            [JsonProperty("Notify how to remove items after first deploy")]
            public bool notify_after_first_Deploy = true;

            [JsonProperty("Allow players to use their middle mouse button to remove an item with a hammer (they can still use chat command regardless if they have perms)")]
            public bool use_input_command = true;

            [JsonProperty("Automatically add new types to the config?")]
            public bool auto_update = false;

            [JsonProperty("Respawn delay when an entity is collected that isnt supposed to be")]
            public float collectible_respawn_delay = 1f;

            [JsonProperty("Kill all deployed nature when the associated cupboard is destroyed?")]
            public bool cupboard_kill = false;

            [JsonProperty("Allow team mates to pickup their members items")]
            public bool team_pickup = true;

            [JsonProperty("Allow TC authed players to pickup items")]
            public bool auth_pickup = true;

            [JsonProperty("Allow DeployableNature to control stack sizes? [Set to false if using a stacks plugin]")]
            public bool manage_stacks = true;

            [JsonProperty("Respawn entities that did not die from being hit")]
            public bool force_respawn_entities = true;

            [JsonProperty("Names of HumanNPCs that will open the market")]
            public List<string> market_npcs = new List<string>();

            [JsonProperty("IDs of HumanNPCs that will open the market")]
            public List<ulong> market_npc_ids = new List<ulong>();

            [JsonProperty("Prevent damage done to entities deployed by an admin, by players who are not admin?")]
            public bool prevent_damage_to_admin_deployed_entities = false;

            [JsonProperty("Kill all DN entities when the associated TC is destroyed?")]
            public bool kill_on_tc_destroy = false;

            [JsonProperty("Permissions that will receive a discount on the store cost when purchasing [1.0 is full price]. Prefix with deployablenature.")]
            public Dictionary<string, float> discount_permissions = new Dictionary<string, float>();

            [JsonProperty("Prefab information", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<ulong, ItemInfo> Prefabs = new Dictionary<ulong, ItemInfo>();

            [JsonProperty("Tree prefabs", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<TreeType, TreeInfo> trees = new Dictionary<TreeType, TreeInfo>();

            [JsonProperty("Bush prefabs", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<BushType, BushInfo> bushes = new Dictionary<BushType, BushInfo>();

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration()
            {
                Prefabs = DefaultPrefabs,
                trees = DefaultTrees,
                bushes = DefaultBushes,
                custom_currency = new CustomCurrency()
                {
                    name = "cash",
                    skinID = 2661030582,
                    shortname = "blood"
                },
                discount_permissions = new Dictionary<string, float>()
                {
                    ["deployablenature.vip"] = 1.0f
                }
            };
            config.market_cmds = new string[] { "naturemarket", "nm" };
        }

        private Dictionary<ulong, ItemInfo> DefaultPrefabs
        {
            get
            {
                return new Dictionary<ulong, ItemInfo>
                {
                    [2609145017] = new ItemInfo(true, "medium clutter rock", "electric.teslacoil", "assets/bundled/prefabs/modding/admin/admin_rock_clutter_medium_d.prefab", 10, true, true, false, 3, 20, PrefabType.Rock, true, 10),
                    [2668227876] = new ItemInfo(true, "small quarry rock", "electric.teslacoil", "assets/bundled/prefabs/modding/admin/admin_rock_quarry_small_a.prefab", 15, true, true, false, 3, 20, PrefabType.Rock, true, 10),
                    [2668228341] = new ItemInfo(true, "small rock formation", "electric.teslacoil", "assets/bundled/prefabs/modding/admin/admin_rock_formation_small_c.prefab", 15, true, true, false, 3, 20, PrefabType.Rock, true, 10),
                    [2668228500] = new ItemInfo(true, "medium rock formation", "electric.teslacoil", "assets/bundled/prefabs/modding/admin/admin_rock_formation_medium_a.prefab", 15, true, true, false, 3, 20, PrefabType.Rock, true, 10),
                    [2668228817] = new ItemInfo(true, "arid medium cliff", "electric.teslacoil", "assets/bundled/prefabs/modding/admin/admin_cliff_medium_arc_arid_small.prefab", 15, true, true, false, 3, 20, PrefabType.Rock, true, 10),
                    [2668228981] = new ItemInfo(true, "large cliff", "electric.teslacoil", "assets/bundled/prefabs/modding/admin/admin_cliff_low_arc.prefab", 15, true, true, false, 3, 20, PrefabType.Rock, true, 10),

                    [2806644689] = new ItemInfo(true, "polar bear", "telephone", "assets/rust.ai/agents/bear/polarbear.prefab", 15, true, true, false, 1, 20, PrefabType.Animal, true, 10),
                    [2806645092] = new ItemInfo(true, "wolf", "telephone", "assets/rust.ai/agents/wolf/wolf.prefab", 15, true, true, false, 1, 20, PrefabType.Animal, true, 10),
                    [2806645210] = new ItemInfo(true, "stag", "telephone", "assets/rust.ai/agents/stag/stag.prefab", 15, true, true, false, 1, 20, PrefabType.Animal, true, 10),
                    [2806645341] = new ItemInfo(true, "chicken", "telephone", "assets/rust.ai/agents/chicken/chicken.prefab", 15, true, true, false, 1, 20, PrefabType.Animal, true, 10),
                    [2806645547] = new ItemInfo(true, "bear", "telephone", "assets/rust.ai/agents/bear/bear.prefab", 15, true, true, false, 1, 20, PrefabType.Animal, true, 10),
                    [2806645796] = new ItemInfo(true, "boar", "telephone", "assets/rust.ai/agents/boar/boar.prefab", 15, true, true, false, 1, 20, PrefabType.Animal, true, 10),
                    [3216966288] = new ItemInfo(true, "horse", "telephone", "assets/rust.ai/agents/horse/horse.prefab", 15, true, true, false, 1, 20, PrefabType.Animal, true, 10),

                    [2668843731] = new ItemInfo(true, "oak tree", "electric.teslacoil", TreeType.Oak, 15, true, true, false, 3, 20, PrefabType.Tree, true, 10),
                    [2668843336] = new ItemInfo(true, "beech tree", "electric.teslacoil", TreeType.Beech, 15, true, true, false, 3, 20, PrefabType.Tree, true, 10),
                    [2668843556] = new ItemInfo(true, "birch tree", "electric.teslacoil", TreeType.Birch, 15, true, true, false, 3, 20, PrefabType.Tree, true, 10),
                    [2668843841] = new ItemInfo(true, "palm tree", "electric.teslacoil", TreeType.Palm, 15, true, true, false, 3, 20, PrefabType.Tree, true, 10),
                    [2668843981] = new ItemInfo(true, "pine tree", "electric.teslacoil", TreeType.Pine, 15, true, true, false, 3, 20, PrefabType.Tree, true, 10),
                    [2668844123] = new ItemInfo(true, "swamp tree", "electric.teslacoil", TreeType.Swamp, 15, true, true, false, 3, 20, PrefabType.Tree, true, 10),
                    [2830312612] = new ItemInfo(true, "cactus", "electric.teslacoil", TreeType.Cacti, 5, true, true, false, 3, 20, PrefabType.Tree, true, 10),

                    [2668860584] = new ItemInfo(true, "creosote bush", "electric.teslacoil", BushType.Creosote, 0, true, true, false, 3, 20, PrefabType.Bush, true, 10),
                    [2668861030] = new ItemInfo(true, "snow willow bush", "electric.teslacoil", BushType.Willow_snow, 0, true, true, false, 3, 20, PrefabType.Bush, true, 10),
                    [2668861281] = new ItemInfo(true, "willow bush", "electric.teslacoil", BushType.Willow, 0, true, true, false, 3, 20, PrefabType.Bush, true, 10),
                    [2668861630] = new ItemInfo(true, "spice bush", "electric.teslacoil", BushType.Spice, 0, true, true, false, 3, 20, PrefabType.Bush, true, 10),
                    [2668861850] = new ItemInfo(true, "snow spice bush", "electric.teslacoil", BushType.Spice_snow, 0, true, true, false, 3, 20, PrefabType.Bush, true, 10),
                    [3027624096] = new ItemInfo(true, "ocotillo bush", "electric.teslacoil", BushType.Ocotillo, 0, true, true, false, 3, 20, PrefabType.Bush, true, 10),

                    [2668807382] = new ItemInfo(true, "decorative corn", "electric.teslacoil", "assets/bundled/prefabs/autospawn/collectable/corn/corn-collectable.prefab", 0, true, true, false, 3, 20, PrefabType.Bush, true, 10),
                    [2668906806] = new ItemInfo(true, "decorative pumpkin", "electric.teslacoil", "assets/bundled/prefabs/autospawn/collectable/pumpkin/pumpkin-collectable.prefab", 0, true, true, false, 3, 20, PrefabType.Bush, true, 10),
                    [2668914014] = new ItemInfo(true, "decorative potato", "electric.teslacoil", "assets/bundled/prefabs/autospawn/collectable/potato/potato-collectable.prefab", 0, true, true, false, 3, 20, PrefabType.Bush, true, 10),
                    [2668913894] = new ItemInfo(true, "decorative berries", "electric.teslacoil", BushType.Berries, 0, true, true, false, 3, 20, PrefabType.Bush, true, 10),
                    [2668914545] = new ItemInfo(true, "dect' mushroom", "electric.teslacoil", BushType.Mushrooms, 0, true, true, false, 3, 20, PrefabType.Bush, true, 10),
                    [2726178501] = new ItemInfo(true, "dect' hemp plant", "electric.teslacoil", "assets/bundled/prefabs/autospawn/collectable/hemp/hemp-collectable.prefab", 0, true, true, false, 3, 20, PrefabType.Bush, true, 10),
                    [3055146089] = new ItemInfo(true, "halloween wood", "electric.teslacoil", "assets/bundled/prefabs/autospawn/collectable/stone/halloween/halloween-wood-collectable.prefab", 0, true, true, false, 5, 20, PrefabType.Bush, true, 10),
                    [3055146304] = new ItemInfo(true, "halloween metal", "electric.teslacoil", "assets/bundled/prefabs/autospawn/collectable/stone/halloween/halloween-metal-collectable.prefab", 0, true, true, false, 5, 20, PrefabType.Bush, true, 10),
                    [3055146795] = new ItemInfo(true, "halloween stone", "electric.teslacoil", "assets/bundled/prefabs/autospawn/collectable/stone/halloween/halloween-stone-collectable.prefab", 0, true, true, false, 5, 20, PrefabType.Bush, true, 10),
                    [3055147361] = new ItemInfo(true, "halloween sulfur", "electric.teslacoil", "assets/bundled/prefabs/autospawn/collectable/stone/halloween/halloween-sulfur-collectible.prefab", 0, true, true, false, 5, 20, PrefabType.Bush, true, 10),

                    [3027617264] = new ItemInfo(true, "elevator door", "barricade.concrete", "assets/bundled/prefabs/static/door.hinged.elevator_door.prefab", 20, true, true, false, 3, 10, PrefabType.Furniture, true, 10, "deployablenature.elevator"),
                    [3027621036] = new ItemInfo(true, "desk - blue", "box.wooden.large", "assets/bundled/prefabs/static/desk_a.static.prefab", 20, true, true, false, 3, 10, PrefabType.Furniture, true, 10, "deployablenature.desk"),
                    [3027620925] = new ItemInfo(true, "desk - red", "box.wooden.large", "assets/bundled/prefabs/static/desk_b.static.prefab", 20, true, true, false, 3, 10, PrefabType.Furniture, true, 10, "deployablenature.desk"),
                    [3027620794] = new ItemInfo(true, "desk - yellow", "box.wooden.large", "assets/bundled/prefabs/static/desk_c.static.prefab", 20, true, true, false, 3, 10, PrefabType.Furniture, true, 10, "deployablenature.desk"),
                    [3027620190] = new ItemInfo(true, "desk - grey", "box.wooden.large", "assets/bundled/prefabs/static/desk_d.static.prefab", 20, true, true, false, 3, 10, PrefabType.Furniture, true, 10, "deployablenature.desk"),
                    [3027621575] = new ItemInfo(true, "control chair", "chair", "assets/bundled/prefabs/static/controlchair.static.prefab", 20, true, true, false, 3, 10, PrefabType.Furniture, true, 10, "deployablenature.chair"),
                    [3027621861] = new ItemInfo(true, "office chair", "chair", "assets/bundled/prefabs/static/chair_a.static.prefab", 20, true, true, false, 3, 10, PrefabType.Furniture, true, 10, "deployablenature.chair"),
                    [3027622087] = new ItemInfo(true, "dining chair", "chair", "assets/bundled/prefabs/static/chair_b.static.prefab", 20, true, true, false, 3, 10, PrefabType.Furniture, true, 10, "deployablenature.chair"),
                    [3027622226] = new ItemInfo(true, "farm chair", "chair", "assets/bundled/prefabs/static/chair_c.static.prefab", 20, true, true, false, 3, 10, PrefabType.Furniture, true, 10, "deployablenature.chair"),
                    [3223954199] = new ItemInfo(true, "cantina chair", "chair", "assets/bundled/prefabs/static/cantina_chair.static.prefab", 20, true, true, false, 3, 10, PrefabType.Furniture, true, 10, "deployablenature.chair"),
                    [3027622334] = new ItemInfo(true, "toilet - chain", "chair", "assets/bundled/prefabs/static/toilet_a.static.prefab", 20, true, true, false, 3, 10, PrefabType.Furniture, true, 10, "deployablenature.toilet"),
                    [3027622773] = new ItemInfo(true, "toilet - normal", "chair", "assets/bundled/prefabs/static/toilet_b.static.prefab", 20, true, true, false, 3, 10, PrefabType.Furniture, true, 10, "deployablenature.toilet"),
                    [3027622962] = new ItemInfo(true, "door table", "box.wooden.large", "assets/bundled/prefabs/static/tabledoor_a.static.prefab", 20, true, true, false, 3, 10, PrefabType.Furniture, true, 10, "deployablenature.table"),
                    [3027623860] = new ItemInfo(true, "door poker table", "box.wooden.large", "assets/bundled/prefabs/static/tabledoor_b.static.prefab", 20, true, true, false, 3, 10, PrefabType.Furniture, true, 10, "deployablenature.table"),
                    [3223953302] = new ItemInfo(true, "lab table 1", "box.wooden.large", "assets/bundled/prefabs/static/lab_table1.static.prefab", 20, true, true, false, 3, 10, PrefabType.Furniture, true, 10, "deployablenature.table"),
                    [3223953450] = new ItemInfo(true, "lab table 2", "box.wooden.large", "assets/bundled/prefabs/static/lab_table2.static.prefab", 20, true, true, false, 3, 10, PrefabType.Furniture, true, 10, "deployablenature.table"),
                    [3223953558] = new ItemInfo(true, "lab table 3", "box.wooden.large", "assets/bundled/prefabs/static/lab_table3.static.prefab", 20, true, true, false, 3, 10, PrefabType.Furniture, true, 10, "deployablenature.table"),

                    [3105560624] = new ItemInfo(true, "snow covered tree", "electric.teslacoil", TreeType.Snow, 5, true, true, false, 3, 20, PrefabType.Tree, true, 10),
                    [3105561422] = new ItemInfo(true, "gingerbread house", "barricade.concrete", "assets/content/props/gingerbread_barricades/gingerbread_barricades_house.prefab", 20, true, true, false, 3, 10, PrefabType.Furniture, true, 10, "deployablenature.xmas"),
                    [3105562074] = new ItemInfo(true, "gingerbread snowman", "barricade.concrete", "assets/content/props/gingerbread_barricades/gingerbread_barricades_snowman.prefab", 20, true, true, false, 3, 10, PrefabType.Furniture, true, 10, "deployablenature.xmas"),
                    [3105562389] = new ItemInfo(true, "gingerbread tree", "barricade.concrete", "assets/content/props/gingerbread_barricades/gingerbread_barricades_tree.prefab", 20, true, true, false, 3, 10, PrefabType.Furniture, true, 10, "deployablenature.xmas"),
                    [3105562678] = new ItemInfo(true, "gingerbread crate", "barricade.concrete", "assets/content/props/wooden_crates/wooden_crate_gingerbread.prefab", 20, true, true, false, 3, 10, PrefabType.Furniture, true, 10, "deployablenature.xmas"),                    
                
                };
            }
        }

        private Dictionary<TreeType, TreeInfo> DefaultTrees
        {
            get
            {
                return new Dictionary<TreeType, TreeInfo>
                {
                    [TreeType.Beech] = new TreeInfo(new List<string>()
                    {
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_field/american_beech_d.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_field/american_beech_e.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/american_beech_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/american_beech_a_dead.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/american_beech_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/american_beech_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest_pine/american_beech_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest_pine/american_beech_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest_pine/american_beech_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forestside/american_beech_d.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forestside/american_beech_e.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forestside/american_beech_e_dead.prefab"
                    }),
                    [TreeType.Birch] = new TreeInfo(new List<string>()
                    {
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_beachside/birch_small_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_beachside/birch_tiny_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_field/birch_small_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_field/birch_tiny_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/birch_big_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/birch_large_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/birch_medium_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest_pine/birch_big_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest_pine/birch_large_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forestside/birch_medium_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forestside/birch_small_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forestside/birch_tiny_temp.prefab"
                    }),
                    [TreeType.Oak] = new TreeInfo(new List<string>()
                    {
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_field/oak_e.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_field/oak_f.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_field_large/oak_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_field_large/oak_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_field_large/oak_d.prefab"
                    }),
                    [TreeType.Palm] = new TreeInfo(new List<string>()
                    {
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_beachside/palm_tree_short_a_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_beachside/palm_tree_short_b_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_beachside/palm_tree_short_c_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_beachside/palm_tree_small_a_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_beachside/palm_tree_small_b_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_beachside/palm_tree_small_c_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_field/palm_tree_short_a_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_field/palm_tree_short_b_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_field/palm_tree_short_c_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_field/palm_tree_small_b_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_field/palm_tree_small_c_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_forest/palm_tree_med_a_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_forest/palm_tree_short_a_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_forest/palm_tree_short_b_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_forest/palm_tree_short_c_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_forest/palm_tree_tall_a_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_forest/palm_tree_tall_b_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_forestside/palm_tree_short_a_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_forestside/palm_tree_short_b_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_forestside/palm_tree_short_c_entity.prefab"
                    }),
                    [TreeType.Pine] = new TreeInfo(new List<string>()
                    {
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_field/pine_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_field/pine_d.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest_pine/pine_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forest_pine/pine_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forestside_pine/pine_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_forestside_pine/pine_d.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_field/pine_dead_d.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_field/pine_dead_e.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_field/pine_dead_f.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_field_pines/pine_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_field_pines/pine_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_field_pines/pine_d.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_forest/pine_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_tundra_forest/pine_c.prefab"
                    }),
                    [TreeType.Swamp] = new TreeInfo(new List<string>()
                    {
                        "assets/bundled/prefabs/autospawn/resource/swamp-trees/swamp_tree_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/swamp-trees/swamp_tree_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/swamp-trees/swamp_tree_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/swamp-trees/swamp_tree_d.prefab",
                        "assets/bundled/prefabs/autospawn/resource/swamp-trees/swamp_tree_e.prefab",
                        "assets/bundled/prefabs/autospawn/resource/swamp-trees/swamp_tree_f.prefab"
                    }),
                    [TreeType.Cacti] = new TreeInfo(new List<string>()
                    {
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_cactus/cactus-1.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_cactus/cactus-2.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_cactus/cactus-3.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_cactus/cactus-4.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_cactus/cactus-5.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_cactus/cactus-6.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arid_cactus/cactus-7.prefab"
                    }),
                    [TreeType.Snow] = new TreeInfo(new List<string>()
                    {
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest/pine_a_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forestside/pine_b snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest/pine_c_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forestside/pine_d_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest_saplings/pine_sapling_a_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest_saplings/pine_sapling_b_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest_saplings/pine_sapling_c_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest_saplings/pine_sapling_d_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forestside/pine_sapling_d_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest_saplings/pine_sapling_e_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest/douglas_fir_a_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest/douglas_fir_b_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest/douglas_fir_c_snow.prefab"
                    })
                };
            }
        }

        private Dictionary<BushType, BushInfo> DefaultBushes
        {
            get
            {
                return new Dictionary<BushType, BushInfo>
                {
                    [BushType.Creosote] = new BushInfo(new List<string>()
                    {
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arid_desert/creosote_bush_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arid_desert/creosote_bush_d.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arid_dry/creosote_bush_dry_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arid_dry/creosote_bush_dry_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arid_grass/creosote_bush_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arid_grass/creosote_bush_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arid_grass/creosote_bush_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arid_grass/creosote_bush_d.prefab"
                    }),
                    [BushType.Spice] = new BushInfo(new List<string>()
                    {
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_temp/bush_spicebush_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_temp/bush_spicebush_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_temp/bush_spicebush_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_temp/bush_spicebush_d.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_tundra/bush_spicebush_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_tundra/bush_spicebush_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_tundra/bush_spicebush_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_tundra/bush_spicebush_d.prefab"
                    }),
                    [BushType.Spice_snow] = new BushInfo(new List<string>()
                    {
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arctic_forest/bush_spicebush_a_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arctic_forest/bush_spicebush_c_snow.prefab"
                    }),
                    [BushType.Willow] = new BushInfo(new List<string>()
                    {
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_temp/bush_willow_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_temp/bush_willow_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_temp/bush_willow_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_temp/bush_willow_d.prefab"
                    }),
                    [BushType.Willow_snow] = new BushInfo(new List<string>()
                    {
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arctic/bush_willow_snow_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arctic/bush_willow_snow_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arctic/bush_willow_snow_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arctic/bush_willow_snow_d.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arctic/bush_willow_snow_small_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arctic/bush_willow_snow_small_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arctic_forest/bush_willow_snow_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arctic_forest/bush_willow_snow_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arctic_forest/bush_willow_snow_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arctic_forest/bush_willow_snow_d.prefab"
                    }),
                    [BushType.Berries] = new BushInfo(new List<string>()
                    {
                        "assets/bundled/prefabs/autospawn/collectable/berry-black/berry-black-collectable.prefab",
                        "assets/bundled/prefabs/autospawn/collectable/berry-blue/berry-blue-collectable.prefab",
                        "assets/bundled/prefabs/autospawn/collectable/berry-green/berry-green-collectable.prefab",
                        "assets/bundled/prefabs/autospawn/collectable/berry-red/berry-red-collectable.prefab",
                        "assets/bundled/prefabs/autospawn/collectable/berry-white/berry-white-collectable.prefab",
                        "assets/bundled/prefabs/autospawn/collectable/berry-yellow/berry-yellow-collectable.prefab"
                    }),
                    [BushType.Mushrooms] = new BushInfo(new List<string>()
                    {
                        "assets/bundled/prefabs/autospawn/collectable/mushrooms/mushroom-cluster-5.prefab",
                        "assets/bundled/prefabs/autospawn/collectable/mushrooms/mushroom-cluster-6.prefab"
                    }),
                    [BushType.Ocotillo] = new BushInfo(new List<string>()
                    {
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arid_ocotillo/ocotillo_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arid_ocotillo/ocotillo_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arid_ocotillo/ocotillo_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arid_ocotillo/ocotillo_d.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arid_ocotillo/ocotillo_dry_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arid_ocotillo/ocotillo_dry_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arid_ocotillo/ocotillo_dry_c.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_bushes_arid_ocotillo/ocotillo_dry_d.prefab",
                    }),
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    PrintToConsole("Configuration appears to be outdated; updating and saving");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            PrintToConsole($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion

        #region Data

        PlayerEntity pcdData;
        private DynamicConfigFile PCDDATA;
        public static DeployableNature Instance;

        void Init()
        {
            Instance = this;
            PCDDATA = Interface.Oxide.DataFileSystem.GetFile(this.Name);
            permission.RegisterPermission("deployablenature.admin", this);
            permission.RegisterPermission("deployablenature.ignore.restrictions", this);
            permission.RegisterPermission("deployablenature.market.chat", this);
            permission.RegisterPermission("deployablenature.gather", this);
            permission.RegisterPermission("deployablenature.free", this);
            permission.RegisterPermission("deployablenature.use", this);

            //if (!config.cupboard_kill) Unsubscribe("OnEntityDeath");

            var updated = false;

			if (config.market_cmds == null || config.market_cmds.Length == 0)
			{
				config.market_cmds = new string[] { "naturemarket", "nm" };
			}

            if (config.auto_update)
            {
                if (UpdateBushes()) updated = true;
                if (UpdateTrees()) updated = true;
                if (UpdateItems()) updated = true;
            }

            foreach (var kvp in config.Prefabs)
            {
                if (string.IsNullOrEmpty(kvp.Value.permission_required)) continue;
                if (!kvp.Value.permission_required.StartsWith("deployablenature."))
                {
                    kvp.Value.permission_required = "deployablenature." + kvp.Value.permission_required;
                    updated = true;
                }
                if (!permission.PermissionExists(kvp.Value.permission_required, this)) permission.RegisterPermission(kvp.Value.permission_required, this);
            }

            if (updated) SaveConfig();

            if (!config.prevent_animal_targeting) Unsubscribe(nameof(OnNpcTarget));

            if (config.drop_rate_from_node == 0 && config.drop_rate_from_animal == 0 && config.drop_rate_from_collectables == 0)
            {
                Unsubscribe(nameof(OnDispenserBonus));
                Unsubscribe(nameof(OnDispenserGather));
            }
            else if (config.roll_chance_on_each_hit) Unsubscribe(nameof(OnDispenserBonus));
        }

        bool UpdateBushes()
        {
            var result = false;
            foreach (var def in DefaultBushes)
            {
                if (!config.bushes.ContainsKey(def.Key))
                {
                    config.bushes.Add(def.Key, def.Value);
                    Puts($"Added new bush type: {def.Key}");
                    result = true;
                }
                else
                {
                    foreach (var path in def.Value.prefabs)
                    {
                        if (!config.bushes[def.Key].prefabs.Contains(path))
                        {
                            config.bushes[def.Key].prefabs.Add(path);
                            Puts($"Added new bush to {def.Key}: {path}");
                            result = true;
                        }
                    }
                }
            }
            return result;
        }

        bool UpdateTrees()
        {
            var result = false;
            foreach (var def in DefaultTrees)
            {
                if (!config.trees.ContainsKey(def.Key))
                {
                    config.trees.Add(def.Key, def.Value);
                    Puts($"Added new tree type: {def.Key}");
                    result = true;
                }
                else
                {
                    foreach (var path in def.Value.prefabs)
                    {
                        if (!config.trees[def.Key].prefabs.Contains(path))
                        {
                            config.trees[def.Key].prefabs.Add(path);
                            Puts($"Added new tree to {def.Key}: {path}");
                            result = true;
                        }
                    }
                }
            }
            return result;
        }

        bool UpdateItems()
        {
            var result = false;
            foreach (var def in DefaultPrefabs)
            {
                if (!config.Prefabs.ContainsKey(def.Key))
                {
                    config.Prefabs.Add(def.Key, def.Value);
                    result = true;
                    Puts($"Added new item: {def.Value.displayName}[{def.Key}]");
                }
            }
            return result;
        }

        void Unload()
        {
            foreach (var _command in config.market_cmds)
                cmd.RemoveChatCommand(_command, this);
            SaveData();
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "NatureMarket");
                CuiHelper.DestroyUi(player, "MenuBackPanel");
            }

#if CARBON
            _harmony.UnpatchAll(Name + "Patch");
#endif
        }

#if CARBON
        private Harmony _harmony;
#endif

        void Loaded()
        {
            LoadData();
#if CARBON
            _harmony = new Harmony(Name + "Patch");
            _harmony.PatchAll();
#endif
        }

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
            public Dictionary<ulong, PrefabInfo> rEntity = new Dictionary<ulong, PrefabInfo>();
            public bool purgeEnabled = false;
        }

        public class CustomCurrency
        {
            public string name = null;
            public ulong skinID = 0;
            public string shortname = null;
        }

        class PCDInfo
        {
            public int deployed_rocks;
            public int deployed_trees;
            public int deployed_bushes;
            public int deployed_animals;
            public int deployed_furniture;
        }

        class PrefabInfo
        {
            public bool AuthDamageOnly;
            public int hits_taken;
            public int max_hits;
            public ulong skinID;
            public bool prevent_gather;
            public PrefabType type;
            public ulong ownerID;
            public Vector3 loc;
            public Vector3 rot;
            public string path;
            public ulong tool_cupboard_id;
            public bool admin_deployed = false;
            public bool deployedOnTugboat = false;
        }


        public class ItemInfo
        {
            public bool enabled;
            public string displayName;
            public string item_shortname;
            public string prefab_path;
            public int prefab_durability;
            public bool TCAuthRequired;
            public bool canBePickedUp;
            public bool authDamageOnly;
            public int max_spawn_quantity;
            public int max_stack_size;
            public PrefabType prefab_type;
            public bool prevent_gather;
            public TreeType treeType = TreeType.None;
            public BushType bushType = BushType.None;
            public double market_price;
            public string permission_required;

            [JsonConstructor]
            public ItemInfo() { }
            public ItemInfo(bool enabled, string displayName, string item_shortname, string prefab_path, int prefab_durability, bool TCAuthRequired, bool canBePickedUp, bool authDamageOnly, int max_spawn_quantity, int max_stack_size, PrefabType prefab_type, bool prevent_gather, float market_price, string permission_required = null)
            {
                this.enabled = enabled;
                this.displayName = displayName;
                this.item_shortname = item_shortname;
                this.prefab_path = prefab_path;
                this.prefab_durability = prefab_durability;
                this.TCAuthRequired = TCAuthRequired;
                this.canBePickedUp = canBePickedUp;
                this.authDamageOnly = authDamageOnly;
                this.max_spawn_quantity = max_spawn_quantity;
                this.max_stack_size = max_stack_size;
                this.prefab_type = prefab_type;
                this.prevent_gather = prevent_gather;
                this.market_price = market_price;
                this.permission_required = permission_required;
            }

            public ItemInfo(bool enabled, string displayName, string item_shortname, TreeType treeType, int prefab_durability, bool TCAuthRequired, bool canBePickedUp, bool authDamageOnly, int max_spawn_quantity, int max_stack_size, PrefabType prefab_type, bool prevent_gather, float market_price, string permission_required = null)
            {
                this.enabled = enabled;
                this.displayName = displayName;
                this.item_shortname = item_shortname;
                this.treeType = treeType;
                this.prefab_durability = prefab_durability;
                this.TCAuthRequired = TCAuthRequired;
                this.canBePickedUp = canBePickedUp;
                this.authDamageOnly = authDamageOnly;
                this.max_spawn_quantity = max_spawn_quantity;
                this.max_stack_size = max_stack_size;
                this.prefab_type = prefab_type;
                this.prevent_gather = prevent_gather;
                this.market_price = market_price;
                this.permission_required = permission_required;
            }

            public ItemInfo(bool enabled, string displayName, string item_shortname, BushType bushType, int prefab_durability, bool TCAuthRequired, bool canBePickedUp, bool authDamageOnly, int max_spawn_quantity, int max_stack_size, PrefabType prefab_type, bool prevent_gather, float market_price, string permission_required = null)
            {
                this.enabled = enabled;
                this.displayName = displayName;
                this.item_shortname = item_shortname;
                this.bushType = bushType;
                this.prefab_durability = prefab_durability;
                this.TCAuthRequired = TCAuthRequired;
                this.canBePickedUp = canBePickedUp;
                this.authDamageOnly = authDamageOnly;
                this.max_spawn_quantity = max_spawn_quantity;
                this.max_stack_size = max_stack_size;
                this.prefab_type = prefab_type;
                this.prevent_gather = prevent_gather;
                this.market_price = market_price;
                this.permission_required = permission_required;
            }
        }

        public class TreeInfo
        {
            public List<string> prefabs = new List<string>();
            public TreeInfo(List<string> prefabs)
            {
                this.prefabs = prefabs;
            }
        }

        public class BushInfo
        {
            public List<string> prefabs = new List<string>();
            public BushInfo(List<string> prefabs)
            {
                this.prefabs = prefabs;
            }
        }

        public enum PrefabType
        {
            Rock,
            Tree,
            Bush,
            Animal,
            Furniture
        }

        public enum TreeType
        {
            None,
            Palm,
            Oak,
            Swamp,
            Birch,
            Beech,
            Pine,
            Cacti,
            Snow
        }

        public enum BushType
        {
            None,
            Willow,
            Willow_snow,
            Spice,
            Spice_snow,
            Creosote,
            Berries,
            Mushrooms,
            Ocotillo
        }

        public string Prefix;

        public Dictionary<ulong, ItemInfo> loot_table_rocks = new Dictionary<ulong, ItemInfo>();
        public Dictionary<ulong, ItemInfo> loot_table_animals = new Dictionary<ulong, ItemInfo>();
        public Dictionary<ulong, ItemInfo> loot_table_trees = new Dictionary<ulong, ItemInfo>();
        public Dictionary<ulong, ItemInfo> loot_table_bushes = new Dictionary<ulong, ItemInfo>();
        public Dictionary<ulong, ItemInfo> loot_table_furniture = new Dictionary<ulong, ItemInfo>();
        public Dictionary<TreeType, List<string>> trees = new Dictionary<TreeType, List<string>>();

#endregion;

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Prefix"] = "<color=#DFF008>[DN]</color>",
                ["WarnMarketCooldown"] = "{0} Please wait a second before buying another item.",
                ["NoPickup"] = "{0} You cannot collect this entity as it is decorative.",
                ["NoGather"] = "{0} You cannot gather from this entity as it is decorative.",
                ["PlacementLimit"] = "{0} You have reached your placement limit for this item type.",
                ["BuildAuthReq"] = "{0} You must have building auth in order to deploy this item.",
                ["NP"] = "{0} You cannot pick this entity up.",
                ["WP"] = "{0} Please wait a second before picking up another entity.",
                ["GiveItem"] = "{0} You Received {1}x {2}.",
                ["NotifyConsoleInvalid"] = "{0} Invalid skin: {1}. A list of valid keys has been printed to console.",
                ["NotifyOfConsole"] = "{0} Usage: /giveprefab <skin id> <optional: quantity>\nA list of valid keys has been printed to console.",
                ["NotifyInConsole"] = "{0}",
                ["NEC"] = "{0} You do not have enough {1} to purchase {2}x {3}.",
                ["broadcastmsg"] = "{0} You can type <color=#51ff00>/{1}</color> to access the Nature Market and purchase deployable nature items.",
                ["itemDisabled"] = "{0} This item has been disabled.",
                ["dnkillentities"] = "Removed all deployable nature entities",
                ["dnkillentitiesdata"] = "Removed all deployable nature entities and cleared data.",
                ["NoUsePerms"] = "You do not have permission to use Deployable Nature items.",
                ["PickupNotify"] = "You can collect your deployable nature items by looking at them with your <color=#ff8300>{0}</color> active, and pressing your <color=#ff8300>middle mouse button</color>, or by using the <color=#ff8300>/dnpickup</color> command.\nYou can also adjust the deployment height of the entity by holding down <color=#ff8300>shift</color> while deploying.",
                ["PickupNotifyNoInput"] = "You can collect your deployable nature items by looking at them while using the <color=#ff8300>/dnpickup</color> command.\nYou can also adjust the deployment height of the entity by holding down <color=#ff8300>shift</color> while deploying.",
                ["dnkillentitiesplayer"] = "Deleted {0} entities for {1}.",
                ["PurgeEnabled"] = "Purge enabled: {0}.\nYou can enable/disable with dnpurge true/false.",
                ["PurgeEnabledAnnouncement"] = "Purge is enabled and all deployable nature items have been deleted.",
                ["PurgeDisabledAnnouncement"] = "Purge is no longer enabled. You can now deploy nature entities."
            }, this);
        }

        #endregion

        #region Hooks

        List<ulong> shown = new List<ulong>();

        void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (newItem != null && (newItem.info.shortname == "hammer" || newItem.info.shortname == "toolgun") && permission.UserHasPermission(player.UserIDString, "deployablenature.use") && !shown.Contains(player.userID))
            {
                PrintToChat(player, config.use_input_command ? string.Format(lang.GetMessage("PickupNotify", this, player.UserIDString), newItem.info.displayName.english) : lang.GetMessage("PickupNotifyNoInput", this, player.UserIDString));
                shown.Add(player.userID);
            }
        }

        object CanNpcEat(BaseNpc npc, BaseEntity target)
        {
            if (npc == null || target == null) return null;
            if (IsDeployableNature(target)) return false;
            return null;
        }

        object OnItemRecycle(Item item, Recycler recycler)
        {
            if (config.Prefabs.ContainsKey(item.skin)) return false;
            return null;
        }        

        object OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (dispenser == null) return null;

            if (config.roll_chance_on_each_hit) return HandleDispenserGather(dispenser, player, item);

            if (dispenser.gatherType != ResourceDispenser.GatherType.Flesh) return null;

            bool isFinalHit = true;
            foreach (var i in dispenser.containedItems)
            {
                if (i.amount > 0)
                {
                    isFinalHit = false;
                    break;
                }
            }

            if (isFinalHit) return HandleDispenserGather(dispenser, player, item);

            return null;
        }

        object OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            return HandleDispenserGather(dispenser, player, item);
        }

        object HandleDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (dispenser == null || player == null) return null;
            object result = null;
            PrefabInfo prefabData;
            if (dispenser.baseEntity != null && pcdData.rEntity.TryGetValue(dispenser.baseEntity.net.ID.Value, out prefabData) && prefabData.prevent_gather)
            {
                PrintToChat(player, string.Format(lang.GetMessage("NoGather", this, player.UserIDString), Prefix));
                result = false;
            }

            if (!permission.UserHasPermission(player.UserIDString, "deployablenature.gather")) return result;
            if (dispenser.gatherType == ResourceDispenser.GatherType.Ore && result == null && config.drop_rate_from_node > 0)
            {
                var roll = UnityEngine.Random.Range(1, 101);
                if (roll <= config.drop_rate_from_node)
                {
                    var randomRock = loot_table_rocks.ToList().GetRandom();
                    GiveItem(player, randomRock.Key, UnityEngine.Random.Range(1, randomRock.Value.max_spawn_quantity + 1));
                }
            }
            if (dispenser.gatherType == ResourceDispenser.GatherType.Tree && result == null && config.drop_rate_from_tree > 0)
            {
                var roll = UnityEngine.Random.Range(1, 101);
                if (roll <= config.drop_rate_from_tree)
                {
                    var randomTree = loot_table_trees.ToList().GetRandom();
                    GiveItem(player, randomTree.Key, UnityEngine.Random.Range(1, randomTree.Value.max_spawn_quantity + 1));
                }
            }

            if (dispenser.gatherType == ResourceDispenser.GatherType.Flesh && result == null && config.drop_rate_from_animal > 0)
            {
                var roll = UnityEngine.Random.Range(1, 101);
                if (roll <= config.drop_rate_from_animal)
                {
                    if (!config.animal_specific_drops)
                    {
                        var randomAnimal = loot_table_animals.ToList().GetRandom();
                        GiveItem(player, randomAnimal.Key, UnityEngine.Random.Range(1, randomAnimal.Value.max_spawn_quantity + 1));
                    }
                    else
                    {
                        var prefab = GetAnimalPrefab(dispenser.baseEntity?.ShortPrefabName);
                        if (!string.IsNullOrEmpty(prefab))
                        {
                            foreach (var kvp in loot_table_animals)
                            {
                                if (kvp.Value.prefab_path == prefab)
                                {
                                    GiveItem(player, kvp.Key, UnityEngine.Random.Range(1, kvp.Value.max_spawn_quantity + 1));
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            return result;
        }

        string GetAnimalPrefab(string corpse_shortname)
        {
            if (!string.IsNullOrEmpty(corpse_shortname)) return null;
            switch (corpse_shortname)
            {
                case "bear.corpse": return "assets/rust.ai/agents/bear/polarbear.prefab";
                case "polarbear.corpse": return "assets/rust.ai/agents/bear/polarbear.prefab";
                case "chicken.corpse": return "assets/rust.ai/agents/chicken/chicken.prefab";
                case "stag.corpse": return "assets/rust.ai/agents/stag/stag.prefab";
                case "boar.corpse": return "assets/rust.ai/agents/boar/boar.prefab";
                case "wolf.corpse": return "assets/rust.ai/agents/wolf/wolf.prefab";
                default: return null;
            }
        }        

        class CreationInfo
        {
            public string PrefabName;
            public Vector3 pos;
            public Quaternion rot;
            public ulong skinID;
            public ulong ownerID;
            public CreationInfo(string PrefabName, Vector3 pos, Quaternion rot, ulong skinID, ulong ownerID)
            {
                this.PrefabName = PrefabName;
                this.pos = pos;
                this.rot = rot;
                this.skinID = skinID;
                this.ownerID = ownerID;
            }
        }

        //object OnCollectiblePickup(CollectibleEntity collectible, BaseRidableAnimal horse, bool eat)
        //{
        //    if (collectible == null || collectible.itemList == null || collectible.itemList.Length == 0) return null;
        //    PrefabInfo prefabData;
        //    if (pcdData.rEntity.TryGetValue(collectible.net.ID.Value, out prefabData) && prefabData.prevent_gather)
        //    {
        //        return true;
        //    }
        //    return null;
        //}

        //object OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player, bool eat)
        //{
        //    if (collectible == null || collectible.itemList == null || collectible.itemList.Length == 0 || player == null || player.IsNpc) return null;

        //    PrefabInfo prefabData;
        //    if (pcdData.rEntity.TryGetValue(collectible.net.ID.Value, out prefabData) && prefabData.prevent_gather && collectible.itemList != null)
        //    {
        //        PrintToChat(player, string.Format(lang.GetMessage("NoPickup", this, player.UserIDString), Prefix));
        //        return true;
        //    }
        //    if (!permission.UserHasPermission(player.UserIDString, "deployablenature.gather")) return null;
        //    var roll = UnityEngine.Random.Range(1, 101);
        //    if (roll <= config.drop_rate_from_collectables && config.drop_rate_from_collectables > 0)
        //    {
        //        List<KeyValuePair<ulong, ItemInfo>> temp_list = Pool.GetList<KeyValuePair<ulong, ItemInfo>>();
        //        temp_list.AddRange(loot_table_bushes);
        //        var randomPlant = temp_list.GetRandom();
        //        GiveItem(player, randomPlant.Key, UnityEngine.Random.Range(1, randomPlant.Value.max_spawn_quantity + 1));
        //        Pool.FreeList(ref temp_list);
        //    }
        //    return null;
        //}

        void OnLootSpawn(LootContainer container)
        {
            if (container == null || container.GetEntity() == null) return;
            try
            {
                if (container.GetEntity().ShortPrefabName == "minecart" && container.inventory.itemList.Count < 12)
                {
                    var roll = UnityEngine.Random.Range(1, 101);
                    if (roll <= config.drop_rate_from_minecart)
                    {
                        if (container.inventory.itemList.Count >= container.inventorySlots && container.inventorySlots < 12) container.inventorySlots += 1;
                        var randomRock = loot_table_rocks.ToList().GetRandom();
                        var item = ItemManager.CreateByName(randomRock.Value.item_shortname, UnityEngine.Random.Range(1, randomRock.Value.max_spawn_quantity + 1), randomRock.Key);
                        item.name = randomRock.Value.displayName;
                        NextTick(() =>
                        {
                            if (!item.MoveToContainer(container.inventory)) item.Remove();
                        });
                    }
                }
            }
            catch { }
        }

        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            CuiHelper.DestroyUi(player, "NatureMarket");
            CuiHelper.DestroyUi(player, "MenuBackPanel");
        }


        object OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (attacker == null || info == null || info.HitEntity == null) return null;
            string error = string.Empty;
            try
            {
                error = "Error 1";
                PrefabInfo prefabData;
                error = $"Error 1.5 - {info.HitEntity.net == null}";
                ulong id = info.HitEntity.net.ID.Value;
                error = "Error 2";
                if (id == 0) return null;

                error = $"Error 3 - {pcdData.rEntity == null}";
                if (pcdData.rEntity.TryGetValue(id, out prefabData) && prefabData.prevent_gather)
                {
                    return false;
                }
            }
            catch
            {
                Puts($"OnPlayerAttack ERROR: {error}");
            }
            return null;
        }

        object OnMaxStackable(Item item)
        {
            ItemInfo i;
            if (config.Prefabs.TryGetValue(item.skin, out i) && i.max_stack_size > 0)
                return i.max_stack_size;
            return null;
        }

        object CanCombineDroppedItem(DroppedItem item, DroppedItem targetItem)
        {
            if (item == null || targetItem == null) return null;
            if ((config.Prefabs.ContainsKey(item.item.skin) || config.Prefabs.ContainsKey(targetItem.item.skin)) && item.item.skin != targetItem.item.skin) return true;
            return null;
        }

        void OnServerInitialized(bool initial)
        {
            Prefix = lang.GetMessage("Prefix", this);

            if (config.currency.ToUpper() == "SR" && ServerRewards == null)
            {
                Puts($"{config.currency.TitleCase()} has been set as the currency, but is not loaded. Please load the {config.currency.TitleCase()} plugin and try again or set the currency to something else.");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }

            if (config.currency.ToUpper() == "ECONOMICS" && Economics == null)
            {
                Puts($"{config.currency.TitleCase()} has been set as the currency, but is not loaded. Please load the {config.currency.TitleCase()} plugin and try again or set the currency to something else.");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }

            if (!config.manage_stacks)
            {
                Unsubscribe(nameof(OnMaxStackable));
            }

            if (!config.notify_player_with_hammer || pcdData.purgeEnabled) Unsubscribe("OnActiveItemChanged");
            if (!config.use_input_command || pcdData.purgeEnabled) Unsubscribe("OnPlayerInput");

            CheckForNewItems();

            if (!config.prevent_recycling) Unsubscribe("OnItemRecycle");

            if (config.currency.ToUpper() == "CUSTOM" && config.custom_currency == null)
            {
                config.custom_currency = new CustomCurrency() { name = "scrap", shortname = "scrap", skinID = 0 };
                SaveConfig();
            }

            foreach (var _command in config.market_cmds)
                cmd.AddChatCommand(_command, this, nameof(NatureMarketCMD));

            if (config.chat_delay > 0)
            {
                timer.Every(config.chat_delay, () =>
                {
                    foreach (var player in BasePlayer.activePlayerList)
                    {
                        if (permission.UserHasPermission(player.UserIDString, "deployablenature.market.chat") || permission.UserHasPermission(player.UserIDString, "deployablenature.admin")) PrintToChat(player, string.Format(lang.GetMessage("broadcastmsg", this, player.UserIDString), Prefix, config.market_cmds.ElementAt(0)));
                    }
                });
            }            

            if (config.drop_rate_from_minecart == 0) Unsubscribe("OnLootSpawn");

            var entities = BaseNetworkable.serverEntities.Where(x => pcdData.rEntity.ContainsKey(x.net.ID.Value)).ToList();
            var entities_id = entities.Select(x => x.net.ID.Value).ToList();

            foreach (var ent in pcdData.rEntity.ToList())
            {
                try
                {
                    if (!entities_id.Contains(ent.Key))
                    {
                        if (ent.Value.ownerID == 0 || ent.Value.loc == Vector3.zero || ent.Value.rot == Vector3.zero || ent.Value.path == null || ent.Value.deployedOnTugboat)
                        {
                            pcdData.rEntity.Remove(ent.Key);
                        }
                        else
                        {
                            ItemInfo info;
                            if (config.Prefabs.TryGetValue(ent.Value.skinID, out info))
                            {
                                CreateEntity(ent.Value.path, ent.Value.loc, Quaternion.Euler(ent.Value.rot), ent.Value.skinID, ent.Value.ownerID, info.prefab_durability, info.authDamageOnly, info.prevent_gather, ent.Value.admin_deployed);
                            }
                            pcdData.rEntity.Remove(ent.Key);
                        }
                    }
                    else
                    {
                        var entity = entities.Where(x => ent.Key == x.net.ID.Value).FirstOrDefault() as BaseEntity;
                        if (entity == null) continue;
                        if (ent.Value.ownerID == 0) ent.Value.ownerID = entity.OwnerID;
                        if (ent.Value.loc == Vector3.zero) ent.Value.loc = entity.transform.position;
                        if (ent.Value.rot == Vector3.zero) ent.Value.rot = entity.transform.rotation.eulerAngles;
                        if (ent.Value.path == null) ent.Value.path = entity.PrefabName;
                    }
                }
                catch { }
            }
            

            foreach (KeyValuePair<ulong, PrefabInfo> kvp in pcdData.rEntity)
            {
                ItemInfo itemData;
                if (config.Prefabs.TryGetValue(kvp.Value.skinID, out itemData))
                {
                    kvp.Value.max_hits = itemData.prefab_durability;
                    kvp.Value.AuthDamageOnly = itemData.authDamageOnly;
                    kvp.Value.prevent_gather = itemData.prevent_gather;
                    kvp.Value.type = itemData.prefab_type;
                }                
            }

            var doSave = false;
            foreach (KeyValuePair<ulong, ItemInfo> kvp in config.Prefabs)
            {
                if (!kvp.Value.enabled) continue;
                for (int i = 0; i < 99; i++)
                {
                    if (!market_pages.ContainsKey(i))
                    {
                        market_pages.Add(i, new List<ulong> { kvp.Key });
                        break;
                    }
                    else if (market_pages[i].Count < 6)
                    {
                        market_pages[i].Add(kvp.Key);
                        break;
                    }
                }
            }

            if (doSave) SaveConfig();

            // Cleans the data file of removed entities.
            List<ulong> found = new List<ulong>();
            Dictionary<ulong, PCDInfo> ent_count = new Dictionary<ulong, PCDInfo>();
            foreach (KeyValuePair<ulong, PCDInfo> kvp in pcdData.pEntity)
            {
                ent_count.Add(kvp.Key, new PCDInfo());
            }

            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (pcdData.rEntity.ContainsKey(entity.net.ID.Value))
                {
                    found.Add(entity.net.ID.Value);
                    var ent = entity as BaseEntity;
                    if (ent == null || ent.OwnerID == 0) continue;
                    PCDInfo playerData;
                    if (!ent_count.TryGetValue(ent.OwnerID, out playerData)) ent_count.Add(ent.OwnerID, playerData = new PCDInfo());
                    switch (pcdData.rEntity[entity.net.ID.Value].type)
                    {
                        case PrefabType.Bush:
                            playerData.deployed_bushes++;
                            break;
                        case PrefabType.Rock:
                            playerData.deployed_rocks++;
                            break;
                        case PrefabType.Tree:
                            playerData.deployed_trees++;
                            break;
                        case PrefabType.Animal:
                            playerData.deployed_animals++;
                            break;
                        case PrefabType.Furniture:
                            playerData.deployed_furniture++;
                            break;
                    }
                }
            }

            foreach (var key in pcdData.rEntity.Keys.Except(found).ToList())
            {
                pcdData.rEntity.Remove(key);
            }

            if (ent_count != null)
            {
                foreach (var kvp in ent_count)
                {
                    if (!pcdData.pEntity.ContainsKey(kvp.Key)) pcdData.pEntity.Add(kvp.Key, new PCDInfo());
                    pcdData.pEntity[kvp.Key] = kvp.Value;
                }
            }           


            foreach (KeyValuePair<ulong, ItemInfo> kvp in config.Prefabs)
            {
                if (kvp.Value.enabled)
                {
                    if (kvp.Value.prefab_type == PrefabType.Bush && !loot_table_bushes.ContainsKey(kvp.Key)) loot_table_bushes.Add(kvp.Key, kvp.Value);
                    if (kvp.Value.prefab_type == PrefabType.Tree && !loot_table_trees.ContainsKey(kvp.Key)) loot_table_trees.Add(kvp.Key, kvp.Value);
                    if (kvp.Value.prefab_type == PrefabType.Rock && !loot_table_rocks.ContainsKey(kvp.Key)) loot_table_rocks.Add(kvp.Key, kvp.Value);
                    if (kvp.Value.prefab_type == PrefabType.Animal && !loot_table_animals.ContainsKey(kvp.Key)) loot_table_animals.Add(kvp.Key, kvp.Value);
                    if (kvp.Value.prefab_type == PrefabType.Furniture && !loot_table_furniture.ContainsKey(kvp.Key)) loot_table_furniture.Add(kvp.Key, kvp.Value);
                }                               
            }      
            
            foreach (var perm in config.discount_permissions.Keys)
            {
                if (!permission.PermissionExists(perm, this))
                {
                    permission.RegisterPermission(perm, this);
                    Puts($"Registered permission: {perm}");
                }
            }

            if (pcdData.purgeEnabled)
            {
                PrintToChat(lang.GetMessage("PurgeEnabledAnnouncement", this));
                KillAllEntities(false);
            }
        }

        void OnMeleeAttack(BasePlayer player, HitInfo info)
        {
            if (player != null && !player.IsNpc && info != null && info.HitEntity != null)
            {
                var entity = info.HitEntity;
                if (!IsDestroyableRock(entity)) return;
                PrefabInfo ri;
                if (!pcdData.rEntity.TryGetValue(entity.net.ID.Value, out ri)) pcdData.rEntity.Add(entity.net.ID.Value, ri = new PrefabInfo());
                if (ri.AuthDamageOnly && !player.IsBuildingAuthed() && entity.GetBuildingPrivilege() != null) return;
                if (ri.admin_deployed && !player.IsAdmin) return;
                ri.hits_taken++;
                if (ri.max_hits > 0 && ri.hits_taken >= ri.max_hits)
                {
                    //Break rock
                    PCDInfo pi;
                    if (pcdData.pEntity.TryGetValue(entity.net.ID.Value, out pi))
                    {
                        if (ri.type == PrefabType.Rock && pi.deployed_rocks > 0) pi.deployed_rocks--;
                        if (ri.type == PrefabType.Animal && pi.deployed_animals > 0) pi.deployed_animals--;
                        if (ri.type == PrefabType.Tree && pi.deployed_trees > 0) pi.deployed_trees--;
                        if (ri.type == PrefabType.Bush && pi.deployed_bushes > 0) pi.deployed_bushes--;
                        if (ri.type == PrefabType.Furniture && pi.deployed_furniture > 0) pi.deployed_furniture--;
                    }
                    ulong id = entity.net.ID.Value;
                    entity.Invoke(entity.KillMessage, 0.01f);
                    timer.Once(0.05f, () => pcdData.rEntity.Remove(id));
                }
            }            
        }        

        void OnNewSave(string filename)
        {
            Puts("New map wipe - clearing data.");
            pcdData.pEntity.Clear();
            pcdData.rEntity.Clear();
            pcdData.purgeEnabled = false;
            SaveData();
        }

        public BaseEntity CreateEntity(string prefab, Vector3 pos, Quaternion rot, ulong skin, ulong ownerID, int max_hits, bool authDamageOnly, bool prevent_gather, bool isAdmin, bool createEntry = true)
        {
            var result = GameManager.server.CreateEntity(prefab, pos, rot, true);
            result.skinID = skin;
            result.OwnerID = ownerID;
            result.Spawn();            
            if (!pcdData.rEntity.ContainsKey(result.net.ID.Value) && createEntry)
            {
                pcdData.rEntity.Add(result.net.ID.Value, new PrefabInfo()
                {
                    max_hits = max_hits,
                    AuthDamageOnly = authDamageOnly,
                    skinID = result.skinID,
                    prevent_gather = prevent_gather,
                    loc = pos,
                    rot = rot.eulerAngles,
                    ownerID = ownerID,
                    path = result.PrefabName,
                    tool_cupboard_id = result.GetBuildingPrivilege()?.net.ID.Value ?? 0,
                    admin_deployed = isAdmin
                });
            }
            if (result is BaseAnimalNPC)
            {
                var animal = result as BaseAnimalNPC;
                animal.brain = null;
                var rotation = rot;
                NextTick(() =>
                {
                    animal.transform.rotation = rotation;
                    animal.SendNetworkUpdateImmediate();
                });
            }
            return result;
        }

        object OnNpcTarget(BaseEntity npc, BaseEntity entity)
        {
            if (pcdData.rEntity.ContainsKey(entity.net.ID.Value)) return true;
            return null;
        }

        void OnEntityKill(BuildingPrivlidge tc)
        {
            if (!config.kill_on_tc_destroy) return;
            var entities = FindEntitiesOfType<BaseEntity>(tc.transform.position, 100);
            List<ulong> authed = Pool.GetList<ulong>();
            foreach (var player in tc.authorizedPlayers)
                authed.Add(player.userid);
            foreach (var entity in entities)
            {
                if (entity == null) continue;
                if (entity.GetBuildingPrivilege() != tc) continue;
                if (!IsDeployableNature(entity)) continue;
                if (!authed.Contains(entity.OwnerID)) continue;
                if (!pcdData.rEntity.TryGetValue(entity.net.ID.Value, out var entityData)) continue;
                if (!pcdData.pEntity.TryGetValue(entity.OwnerID, out var data))
                {
                    pcdData.rEntity.Remove(entity.net.ID.Value);
                    entity.Invoke(entity.KillMessage, 1);
                    continue;
                }
                switch (entityData.type)
                {
                    case PrefabType.Animal: data.deployed_animals--; break;
                    case PrefabType.Furniture: data.deployed_furniture--; break;
                    case PrefabType.Tree: data.deployed_trees--; break;
                    case PrefabType.Rock: data.deployed_rocks--; break;
                    case PrefabType.Bush: data.deployed_bushes--; break;
                }
                pcdData.rEntity.Remove(entity.net.ID.Value);
                entity.Invoke(entity.KillMessage, 1);
            }
            Pool.FreeList(ref entities);
            Pool.FreeList(ref authed);
        }

        void OnEntityKill(BaseEntity dnEntity)
        {
            if (IsClearing || dnEntity == null || dnEntity.net == null) return;

            if (!config.force_respawn_entities) return;
            if (dnEntity == null || !IsDeployableNature(dnEntity)) return;
            //force_respawn_entities
            var data = pcdData.rEntity[dnEntity.net.ID.Value];
            if (data.max_hits - data.hits_taken <= 0) return;

            var newEntity = CreateEntity(dnEntity.PrefabName, dnEntity.transform.position, dnEntity.transform.rotation, data.skinID, dnEntity.OwnerID, data.max_hits, data.AuthDamageOnly, data.prevent_gather, false, false);
            pcdData.rEntity.Add(newEntity.net.ID.Value, new PrefabInfo()
            {
                max_hits = data.max_hits,
                AuthDamageOnly = data.AuthDamageOnly,
                skinID = data.skinID,
                prevent_gather = data.prevent_gather,
                loc = dnEntity.transform.position,
                rot = dnEntity.transform.rotation.eulerAngles,
                ownerID = data.ownerID,
                path = newEntity.PrefabName,
                tool_cupboard_id = newEntity.GetBuildingPrivilege()?.net.ID.Value ?? 0,
                admin_deployed = false,
                hits_taken = data.hits_taken
            });
            pcdData.rEntity.Remove(dnEntity.net.ID.Value);

            var collectibleEntity = newEntity as CollectibleEntity;
            if (collectibleEntity != null)
            {
                var collider = collectibleEntity.GetComponent<Collider>();
                if (collider != null) UnityEngine.Object.Destroy(collider);
            }

            var parent = dnEntity.GetParentEntity();
            if (parent == null || parent.ShortPrefabName != "tugboat") return;

            newEntity.SetParent(parent, true, true);
            pcdData.rEntity[newEntity.net.ID.Value].deployedOnTugboat = true;
        }

        object OnEntityTakeDamage(BaseAnimalNPC entity, HitInfo info)
        {            
            PrefabInfo ri;
            if (entity.skinID > 0 && pcdData.rEntity.TryGetValue(entity.net.ID.Value, out ri))
            {
                info?.damageTypes?.ScaleAll(0);
                var player = info?.InitiatorPlayer;
                if (player == null || player.IsNpc || !player.userID.IsSteamId()) return null;
                if (ri.AuthDamageOnly && !player.IsBuildingAuthed() && entity.GetBuildingPrivilege() != null) return null;
                ri.hits_taken++;
                if (ri.max_hits > 0 && ri.hits_taken >= ri.max_hits)
                {
                    PCDInfo pi;
                    if (pcdData.pEntity.TryGetValue(entity.net.ID.Value, out pi))
                    {
                        if (ri.type == PrefabType.Rock && pi.deployed_rocks > 0) pi.deployed_rocks--;
                        if (ri.type == PrefabType.Animal && pi.deployed_animals > 0) pi.deployed_animals--;
                        if (ri.type == PrefabType.Tree && pi.deployed_trees > 0) pi.deployed_trees--;
                        if (ri.type == PrefabType.Bush && pi.deployed_bushes > 0) pi.deployed_bushes--;
                        if (ri.type == PrefabType.Furniture && pi.deployed_furniture > 0) pi.deployed_furniture--;
                    }
                    ulong id = entity.net.ID.Value;
                    entity.Invoke(entity.KillMessage, 0.01f);
                    timer.Once(0.05f, () => pcdData.rEntity.Remove(id));
                }
            }
            return null;
        }

        void OnEntityDeath(Tugboat entity, HitInfo info)
        {
            List<ulong> remove = Pool.GetList<ulong>();
            foreach (var child in entity.children)
            {
                if (child == null) continue;
                if (IsDeployableNature(child))
                {
                    remove.Add(child.net.ID.Value);
                    child.Invoke(child.KillMessage, 0.05f);
                }
            }

            foreach (var id in remove)
            {
                pcdData.rEntity.Remove(id);
            }

            Pool.FreeList(ref remove);
        }

        void OnEntityDeath(BuildingPrivlidge cupboard, HitInfo info)
        {
            if (!config.cupboard_kill) return;
            if (cupboard == null) return;
            Dictionary<ulong, PrefabInfo> delete = new Dictionary<ulong, PrefabInfo>();
            foreach (KeyValuePair<ulong, PrefabInfo> kvp in pcdData.rEntity.Where(x => x.Value.tool_cupboard_id == cupboard.net.ID.Value).Select(y => y))
            {
                if (delete.ContainsKey(kvp.Key)) continue;
                delete.Add(kvp.Key, kvp.Value);
            }

            if (delete.Count == 0) return;

            List<BaseNetworkable> entities = Pool.GetList<BaseNetworkable>();
            entities.AddRange(BaseNetworkable.serverEntities.Where(x => delete.ContainsKey(x.net.ID.Value)));

            if (entities == null || entities.Count == 0)
            {
                Pool.FreeList(ref entities);
                return;
            }
                
            PCDInfo pi;
            ItemInfo itemData;
            foreach (var entry in delete)
            {
                if (!pcdData.pEntity.TryGetValue(entry.Value.ownerID, out pi)) continue;
                if (!config.Prefabs.TryGetValue(entry.Value.skinID, out itemData)) continue;
                switch (itemData.prefab_type)
                {
                    case PrefabType.Bush:
                        pi.deployed_bushes--;
                        break;
                    case PrefabType.Tree:
                        pi.deployed_trees--;
                        break;
                    case PrefabType.Rock:
                        pi.deployed_rocks--;
                        break;
                    case PrefabType.Animal:
                        pi.deployed_animals--;
                        break;
                    case PrefabType.Furniture:
                        pi.deployed_furniture--;
                        break;
                }
                pcdData.rEntity.Remove(entry.Key);
            }

            foreach (var entity in entities)
            {
                // May need to test for Issues with killing entities in a list.
                entity.KillMessage();
            }
            Pool.FreeList(ref entities);
        }
        
        void OnEntityBuilt(Planner plan, GameObject go)
        {
            var entity = go?.ToBaseEntity();
            if (entity == null) return;
            var player = plan?.GetOwnerPlayer();
            if (player == null) return;
            
            ItemInfo itemData;
            if (!config.Prefabs.TryGetValue(entity.skinID, out itemData)) return;
            PCDInfo pi;
            if (!pcdData.pEntity.TryGetValue(player.userID, out pi)) pcdData.pEntity.Add(player.userID, pi = new PCDInfo());

            var pos = entity.transform.position;
            
            if (config.shift_place && player.serverInput.IsDown(BUTTON.SPRINT))
            {
                pos.y -= config.depth_modifier;
            }

            var prefabPath = itemData.prefab_path;
            if (prefabPath == null)
            {
                if (itemData.treeType != TreeType.None)
                {
                    TreeInfo treeData;
                    if (config.trees.TryGetValue(itemData.treeType, out treeData)) prefabPath = treeData.prefabs.GetRandom();
                    else prefabPath = "assets/bundled/prefabs/autospawn/resource/v3_temp_field/pine_b.prefab";
                }
                else if (itemData.bushType != BushType.None)
                {
                    BushInfo bushData;
                    if (config.bushes.TryGetValue(itemData.bushType, out bushData)) prefabPath = bushData.prefabs.GetRandom();
                    else prefabPath = "assets/bundled/prefabs/autospawn/clutter/v3_temp_bushes/bush_willow_a.prefab";
                }
            }

            var dnEntity = CreateEntity(prefabPath, pos, entity.transform.rotation, entity.skinID, player.userID, itemData.prefab_durability, itemData.authDamageOnly, itemData.prevent_gather, config.prevent_damage_to_admin_deployed_entities && player.IsAdmin);
            
            switch (itemData.prefab_type)
            {
                case PrefabType.Bush:
                    pi.deployed_bushes++;
                    break;
                case PrefabType.Tree:
                    pi.deployed_trees++;
                    break;
                case PrefabType.Rock:
                    pi.deployed_rocks++;
                    break;
                case PrefabType.Animal:
                    pi.deployed_animals++;
                    break;
                case PrefabType.Furniture:
                    pi.deployed_furniture++;
                    break;
            }
            var collectibleEntity = dnEntity as CollectibleEntity;
            if (collectibleEntity != null)
            {
                var collider = collectibleEntity.GetComponent<Collider>();
                if (collider != null) UnityEngine.Object.Destroy(collider);
                
            }

            if (!shown.Contains(player.userID))
            {
                PrintToChat(player, config.use_input_command ? string.Format(lang.GetMessage("PickupNotify", this, player.UserIDString), "Hammer") : lang.GetMessage("PickupNotifyNoInput", this, player.UserIDString));
                shown.Add(player.userID);
            }

            NextTick(() =>
            {
                entity.Invoke(entity.KillMessage, 0.01f);
                var parent = entity.GetParentEntity();
                if (parent == null || parent.ShortPrefabName != "tugboat") return;

                dnEntity.SetParent(parent, true, true);
                pcdData.rEntity[dnEntity.net.ID.Value].deployedOnTugboat = true;
            });
        }

        object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            var player = planner?.GetOwnerPlayer();
            if (player == null) return null;
            if (permission.UserHasPermission(player.UserIDString, "deployablenature.ignore.restrictions")) return null;
            var item = planner?.GetItem();
            ItemInfo itemData;
            if (item == null || !config.Prefabs.TryGetValue(item.skin, out itemData)) return null;
            if (!string.IsNullOrEmpty(itemData.permission_required) && !permission.UserHasPermission(player.UserIDString, itemData.permission_required))
            {
                PrintToChat(player, "You do not have the permission to use this item.");
                return false;
            }
            if (pcdData.purgeEnabled)
            {
                PrintToChat(player, "You cannot deploy nature entities while purge is enabled.");
                return false;
            }
            if (!permission.UserHasPermission(player.UserIDString, "deployablenature.use"))
            {
                PrintToChat(player, lang.GetMessage("NoUsePerms", this, player.UserIDString));
                return false;
            }
            PCDInfo pi;
            if (!pcdData.pEntity.TryGetValue(player.userID, out pi)) pcdData.pEntity.Add(player.userID, pi = new PCDInfo());
            if (!itemData.enabled)
            {
                PrintToChat(player, string.Format(lang.GetMessage("itemDisabled", this, player.UserIDString), Prefix));
                return false;
            }
            if ((config.max_trees > 0 && itemData.prefab_type == PrefabType.Tree && pi.deployed_trees >= config.max_trees) || (config.max_rocks > 0 && itemData.prefab_type == PrefabType.Rock && pi.deployed_rocks >= config.max_rocks) || (config.max_bushes > 0 && itemData.prefab_type == PrefabType.Bush && pi.deployed_bushes >= config.max_bushes) || (config.max_animals > 0 && itemData.prefab_type == PrefabType.Animal && pi.deployed_animals >= config.max_animals) || (config.max_furniture > 0 && itemData.prefab_type == PrefabType.Furniture && pi.deployed_furniture >= config.max_furniture))
            {
                PrintToChat(player, string.Format(lang.GetMessage("PlacementLimit", this, player.UserIDString), Prefix));
                return false;
            }
            if (itemData.TCAuthRequired && !player.IsBuildingAuthed())
            {
                PrintToChat(player, string.Format(lang.GetMessage("BuildAuthReq", this, player.UserIDString), Prefix));
                return false;
            }            
            return null;
        }

        List<ulong> pickup_cooldown = new List<ulong>();        

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!input.WasJustReleased(BUTTON.FIRE_THIRD) || player == null) return;
            if (player.GetActiveItem() == null) return;
            var tool = player.GetActiveItem();
            if (tool == null || (!tool.info.shortname.Equals("toolgun") && !tool.info.shortname.Equals("hammer"))) return;
            PickupEntity(player);
        }

        void OnServerSave()
        {
            SaveData();
        }

        #endregion

        #region Helpers

        void CheckForNewItems()
        {
            try
            {
                var newItems = false;
                var missing_items = DefaultPrefabs.Where(x => !config.Prefabs.ContainsKey(x.Key)).ToList();
                if (missing_items != null && missing_items.Count > 0)
                {
                    newItems = true;
                    foreach (var item in missing_items)
                    {
                        config.Prefabs.Add(item.Key, item.Value);
                        Puts($"Added new item - {item.Value.displayName} [{item.Key}]");
                    }
                    if (newItems) SaveConfig();
                }
            }
            catch { }
        }

        float GetModifier(string id)
        {
            if (permission.UserHasPermission(id, "deployablenature.free")) return 0f;
            var lowest = 1.0f;
            foreach (var perm in config.discount_permissions)
            {
                if (permission.UserHasPermission(id, perm.Key) && perm.Value < lowest) lowest = perm.Value;
            }
            return lowest;
        }

        bool isTeam(BasePlayer player, ulong owner)
        {
            if (player.Team == null || player.Team.members == null) return false;
            return player.Team.members.Contains(owner);
        }

        void PickupEntity(BasePlayer player)
        {
            var target = GetTargetEntity(player);
            if (target == null) return;

            if (!pcdData.rEntity.ContainsKey(target.net.ID.Value))
            {
                if (!(target is Tugboat)) return;
                target = FindClosestDNEntity(target as Tugboat, player.transform.position);
                if (target == null || target is Tugboat) return;
            }            

            ItemInfo itemData;
            if ((target.OwnerID == player.userID || (config.team_pickup && isTeam(player, target.OwnerID)) || permission.UserHasPermission(player.UserIDString, "deployablenature.admin") || (config.auth_pickup && player.IsBuildingAuthed())) && config.Prefabs.TryGetValue(target.skinID, out itemData))
            {
                if (!itemData.canBePickedUp)
                {
                    PrintToChat(player, string.Format(lang.GetMessage("NP", this, player.UserIDString), Prefix));
                    return;
                }
                if (pickup_cooldown.Contains(player.userID))
                {
                    PrintToChat(player, string.Format(lang.GetMessage("WP", this, player.UserIDString), Prefix));
                    return;
                }
                ulong playerID = player.userID;
                pickup_cooldown.Add(playerID);
                timer.Once(0.5f, () => pickup_cooldown.Remove(playerID));
                PCDInfo pi;
                if (pcdData.pEntity.TryGetValue(target.OwnerID, out pi))
                {
                    switch (itemData.prefab_type)
                    {
                        case PrefabType.Bush:
                            pi.deployed_bushes--;
                            break;
                        case PrefabType.Tree:
                            pi.deployed_trees--;
                            break;
                        case PrefabType.Rock:
                            pi.deployed_rocks--;
                            break;
                        case PrefabType.Animal:
                            pi.deployed_animals--;
                            break;
                        case PrefabType.Furniture:
                            pi.deployed_furniture--;
                            break;
                    }
                }
                pcdData.rEntity.Remove(target.net.ID.Value);
                GiveItem(player, target.skinID);
                target.Invoke(target.KillMessage, 0.01f);
            }
        }

        BaseEntity FindClosestDNEntity(Tugboat boat, Vector3 pos)
        {
            if (boat.children.IsNullOrEmpty()) return null;
            float closest = -1;
            BaseEntity result = null;
            foreach (var entity in boat.children)
            {
                if (entity is Tugboat) continue;
                if (!IsDeployableNature(entity)) continue;
                if (result == null)
                {
                    closest = Vector3.Distance(entity.transform.position, pos);
                    result = entity;
                    continue;
                }
                var dist = Vector3.Distance(entity.transform.position, pos);
                if (dist < closest)
                {
                    closest = dist;
                    result = entity;
                }
            }

            if (closest > 2) return null;

            return result;
        }

        private static List<T> FindEntitiesOfType<T>(Vector3 a, float n, int m = -1) where T : BaseEntity
        {
            int hits = Physics.OverlapSphereNonAlloc(a, n, Vis.colBuffer, m, QueryTriggerInteraction.Collide);
            List<T> entities = Pool.GetList<T>();
            for (int i = 0; i < hits; i++)
            {
                var entity = Vis.colBuffer[i]?.ToBaseEntity() as T;
                if (entity != null && !entities.Contains(entity)) entities.Add(entity);
                Vis.colBuffer[i] = null;
            }
            return entities;
        }

        private const int LAYER_TARGET = ~(1 << 2 | 1 << 3 | 1 << 4 | 1 << 10 | 1 << 18 | 1 << 28 | 1 << 29);
        private BaseEntity GetTargetEntity(BasePlayer player, float dist = 5f)
        {
            RaycastHit raycastHit;
            bool flag = Physics.Raycast(player.eyes.HeadRay(), out raycastHit, 100f, -1);
            var targetEntity = flag ? raycastHit.GetEntity() : null;
            return targetEntity;
        }

        bool IsDestroyableRock(BaseEntity entity)
        {
            return config.Prefabs.ContainsKey(entity.skinID) && entity.OwnerID > 0;
        }

        void GiveItem(BasePlayer player, ulong skin, int quantity = 1)
        {
            ItemInfo itemData;
            if (!config.Prefabs.TryGetValue(skin, out itemData)) return;
            var item = ItemManager.CreateByName(itemData.item_shortname, quantity, skin);
            item.name = itemData.displayName;
			List<Item> playerItems = new List<Item>();
			playerItems.AddRange(player.inventory.containerMain.itemList);
			playerItems.AddRange(player.inventory.containerBelt.itemList);
			playerItems.AddRange(player.inventory.containerWear.itemList);

			foreach (var _item in playerItems)
			{
				if (_item.skin == skin && _item.amount < itemData.max_stack_size)
				{
					if (!item.MoveToContainer(_item.GetRootContainer(), _item.position, true, true))
						item.Remove();

					PrintToChat(player, string.Format(lang.GetMessage("GiveItem", this, player.UserIDString), Prefix, quantity, itemData.displayName));
					return;
				}
			}
       
            player.GiveItem(item);
            PrintToChat(player, string.Format(lang.GetMessage("GiveItem", this, player.UserIDString), Prefix, quantity, itemData.displayName));
        }

        #endregion

        #region ChatCommands

        void NatureMarketCMD(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, "deployablenature.market.chat") && !permission.UserHasPermission(player.UserIDString, "deployablenature.admin")) return;
            MenuBackPanel(player);
            SendNatureMenu(player);
        }

        [ChatCommand("dnpickup")]
        void DNPickupCMD(BasePlayer player)
        {
            PickupEntity(player);
        }

        BasePlayer FindPlayer(string searchTerm, BasePlayer SearchingPlayer = null)
        {
            if (string.IsNullOrEmpty(searchTerm)) return null;
            searchTerm = searchTerm.ToLower();
            List<BasePlayer> foundPLayers = Pool.GetList<BasePlayer>();
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.UserIDString == searchTerm)
                {
                    Pool.FreeList(ref foundPLayers);
                    return player;
                }
                if (player.displayName.ToLower().Contains(searchTerm)) foundPLayers.Add(player);
            }
            if (foundPLayers.Count == 0)
            {
                if (SearchingPlayer != null)
                {
                    PrintToChat(SearchingPlayer, $"No player was found that matched: {searchTerm}");
                }
                else Puts($"No player was found that matched: {searchTerm}");
                Pool.FreeList(ref foundPLayers);
                return null;
            }
            if (foundPLayers.Count > 1)
            {
                if (SearchingPlayer != null)
                {
                    PrintToChat(SearchingPlayer, $"More than one player found: {String.Join(",", foundPLayers.Select(x => x.displayName))}");
                }
                else Puts($"More than one player found: {String.Join(",", foundPLayers.Select(x => x.displayName))}");
                Pool.FreeList(ref foundPLayers);
                return null;
            }
            var result = foundPLayers[0];
            Pool.FreeList(ref foundPLayers);
            return result;
        } 

        [ConsoleCommand("giveprefab")]
        void ConsoleGivePrefab(ConsoleSystem.Arg arg)
        {            
            var player = arg.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, "deployablenature.admin")) return;
            
            if (arg.Args == null || arg.Args.Length < 2)
            {
                if (player != null) PrintToConsole(player, "Invalid parameters: giveprefab <player ID/name> <skin ID> <optional: quantity>");
                else Puts("Invalid parameters: giveprefab <player ID/name> <skin ID> <optional: quantity>");
                return;
            }
            BasePlayer foundPlayer = FindPlayer(arg.Args[0], player);           

            if (foundPlayer == null || foundPlayer.IsDead() || !foundPlayer.IsConnected)
            {
                if (player != null) PrintToConsole(player, $"No player matched: {arg.Args[0]} was found, or they are dead.");
                else Puts($"No player matched: {arg.Args[0]} was found, or they are dead.");
                return;
            }
            if (!arg.Args[1].IsNumeric())
            {
                if (player != null) PrintToConsole(player, $"{arg.Args[1]} is not a valid skin ID.\n{GetValidSkins()}");
                else Puts($"{arg.Args[1]} is not a valid skin ID.\n{GetValidSkinsNoCol()}");
                return;
            }
            var skinID = Convert.ToUInt64(arg.Args[1]);
            if (!config.Prefabs.ContainsKey(skinID))
            {
                if (player != null) PrintToConsole(player, $"{arg.Args[1]} is not a valid skin ID.\n{GetValidSkins()}");
                else Puts($"{arg.Args[1]} is not a valid skin ID.\n{GetValidSkinsNoCol()}");
                return;
            }
            var quantity = 1;
            if (arg.Args.Length == 3 && arg.Args[2].IsNumeric()) quantity = Convert.ToInt32(arg.Args[2]);
            if (player != null) PrintToConsole(player, $"Gave {foundPlayer.displayName} {quantity}x {config.Prefabs[skinID].displayName}");
            else Puts($"Gave {foundPlayer.displayName} {quantity}x {config.Prefabs[skinID].displayName}");
            GiveItem(foundPlayer, skinID, quantity);
        }

        string GetValidSkins()
        {
            StringBuilder sb = new StringBuilder("Valid Skins:\n");
            foreach (KeyValuePair<ulong, ItemInfo> kvp in config.Prefabs)
            {
                sb.AppendFormat("\n- Key: <color=#ffff00>{0}(</color><color=#ffaa00>{1}</color><color=#ffff00>)</color>", kvp.Key, kvp.Value.displayName);
            }
            return sb.ToString();
        }

        string GetValidSkinsNoCol()
        {
            StringBuilder sb = new StringBuilder("Valid Skins:\n");
            foreach (KeyValuePair<ulong, ItemInfo> kvp in config.Prefabs)
            {
                sb.AppendFormat("\n- Key: {0}({1})", kvp.Key, kvp.Value.displayName);
            }
            return sb.ToString();
        }

        [ChatCommand("giveprefab")]
        void GivePrefab(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "deployablenature.admin")) return;
            if (args.Length == 0 || args.Length > 2)
            {
                
                PrintToChat(player, string.Format(lang.GetMessage("NotifyOfConsole", this), Prefix));
                PrintToConsole(player, string.Format(lang.GetMessage("NotifyInConsole", this), GetValidSkins()));
                return;
            }
            var quantity = 1;
            var skin = Convert.ToUInt64(args[0]);
            if (!config.Prefabs.ContainsKey(skin))
            {
                PrintToChat(player, string.Format(lang.GetMessage("NotifyConsoleInvalid", this), Prefix, GetValidSkins()));
                PrintToConsole(player, string.Format(lang.GetMessage("NotifyInConsole", this), GetValidSkins()));
                return;
            }
            if (args.Length == 2 && args[1].IsNumeric()) quantity = Convert.ToInt32(args[1]);
            GiveItem(player, skin, quantity);
        }

        #endregion

        #region Console commands

        [ConsoleCommand("dnpurge")]
        void PurgeCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, "deployablenature.admin")) return;

            if (arg.Args == null || arg.Args.Length == 0 || (!arg.Args[0].Equals("true", StringComparison.OrdinalIgnoreCase) && !arg.Args[0].Equals("false", StringComparison.OrdinalIgnoreCase)))
            {
                arg.ReplyWith(string.Format(lang.GetMessage("PurgeEnabled", this, player != null? player.UserIDString : null), pcdData.purgeEnabled));
                return;
            }
            if (arg.Args[0].Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                pcdData.purgeEnabled = true;                
                KillAllEntities(false);
                PrintToChat(lang.GetMessage("PurgeEnabledAnnouncement", this));
                
            }
            else
            {
                pcdData.purgeEnabled = false;
                PrintToChat(lang.GetMessage("PurgeDisabledAnnouncement", this));
                if (config.notify_player_with_hammer) Subscribe("OnActiveItemChanged");
                if (config.use_input_command) Subscribe("OnPlayerInput");
            }
            SaveData();
        }

        [ConsoleCommand("dnkillentities")]
        void KillEntities(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, "deployablenature.admin")) return;

            if (arg.Args != null && arg.Args.Length > 0 && arg.Args[0].Equals("true", StringComparison.OrdinalIgnoreCase)) KillAllEntities(true);
            else KillAllEntities(false);
            arg.ReplyWith(pcdData.rEntity.Count > 0 ? lang.GetMessage("dnkillentities", this) : lang.GetMessage("dnkillentitiesdata", this));
        }

        private static bool InRange(Vector3 a, Vector3 b, float distance)
        {
            return (a - b).sqrMagnitude <= distance * distance;
        }

        bool IsClearing;
        void KillAllEntities(bool deleteFromData)
        {
            IsClearing = true;
            List<Vector3> measure = Pool.GetList<Vector3>();
            foreach (var entity in pcdData.rEntity)
                if (!measure.Contains(entity.Value.loc)) 
                    measure.Add(entity.Value.loc);

            List<BaseNetworkable> entities = Pool.GetList<BaseNetworkable>();
            foreach (var entity in BaseNetworkable.serverEntities.OfType<BaseEntity>())
            {
                if (measure.Contains(entity.transform.position)) entities.Add(entity);
            }

            Puts($"Found {entities.Count} entities to delete.");
            foreach (var entity in entities)
            {
                entity.Kill();
            }

            if (deleteFromData)
            {
                pcdData.rEntity.Clear();
                pcdData.pEntity.Clear();
                SaveData();
            }

            Pool.FreeList(ref entities);
            Pool.FreeList(ref measure);

            IsClearing = false;
        }

        [ConsoleCommand("dnkillentitiesforplayer")]
        void KillEntitiesForPlayer(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, "deployablenature.admin")) return;

            if (arg.Args == null || arg.Args.Length == 0)
            {
                arg.ReplyWith("Usage: /dnkillentitiesforplayer <player name/ID>");
                return;
            }
            var name = string.Join(" ", arg.Args);

            var target = FindPlayer(name, player);
            if (target == null)
            {
                arg.ReplyWith($"Could not find player matching: {name}");
                return;
            }

            var entityList = BaseNetworkable.serverEntities.Where(x => pcdData.rEntity.ContainsKey(x.net.ID.Value) && pcdData.rEntity[x.net.ID.Value].ownerID == target.userID).ToList();

            Puts($"Deleting {entityList.Count} entities.");
            foreach (var entity in entityList.ToList())
            {
                pcdData.rEntity.Remove(entity.net.ID.Value);
                entity.KillMessage();
            }
            pcdData.pEntity.Remove(target.userID);            
            arg.ReplyWith(string.Format(lang.GetMessage("dnkillentitiesplayer", this), entityList?.Count, target.displayName));
        }

        #endregion

        #region API Stuff

        void OnAbandonedBaseStarted(Vector3 center, bool AllowPVP, List<BasePlayer> intruders, List<BaseEntity> entities)
        {
            List<BaseEntity> deployedNature = Pool.GetList<BaseEntity>();
            foreach (var entity in entities)
            {
                if (entity == null || entity.IsDestroyed) continue;
                if (IsDeployableNature(entity)) deployedNature.Add(entity);
            }

            foreach (var entity in deployedNature)
            {
                PrefabInfo prefabData;
                if (!pcdData.rEntity.TryGetValue(entity.net.ID.Value, out prefabData)) continue;

                PCDInfo playerData;
                if (pcdData.pEntity.TryGetValue(entity.OwnerID, out playerData))
                {
                    switch (prefabData.type)
                    {
                        case PrefabType.Rock:
                            if (playerData.deployed_rocks > 0) playerData.deployed_rocks--;
                            break;

                        case PrefabType.Animal:
                            if (playerData.deployed_animals > 0) playerData.deployed_animals--;
                            break;

                        case PrefabType.Tree:
                            if (playerData.deployed_trees > 0) playerData.deployed_trees--;
                            break;

                        case PrefabType.Bush:
                            if (playerData.deployed_bushes > 0) playerData.deployed_bushes--;
                            break;

                        case PrefabType.Furniture:
                            if (playerData.deployed_furniture > 0) playerData.deployed_furniture--;
                            break;
                    }
                }
                pcdData.rEntity.Remove(entity.net.ID.Value);
                entity.Kill();
            }

            Pool.FreeList(ref deployedNature);
        }

        object OnGatherRewardsGiveCredit(BasePlayer player, CollectibleEntity collectible, float amount)
        {
            if (IsDeployableNature(collectible)) return true;
            return null;
        }

        void OnUseNPC(BasePlayer npc, BasePlayer player)
        {
            if (config.market_npcs.Contains(npc.displayName) || config.market_npc_ids.Contains(npc.userID))
            {
                MenuBackPanel(player);
                SendNatureMenu(player);
            }
        }

        object CanGatherIngredient(BasePlayer player, ulong source)
        {
            if (pcdData.rEntity.ContainsKey(source)) return false;
            return null;
        }

        object CanGatherRune(BasePlayer player, ulong source)
        {
            if (pcdData.rEntity.ContainsKey(source)) return false;
            return null;
        }

        object CanGainXP(BasePlayer player, BaseEntity source)
        {
            if (pcdData.rEntity.ContainsKey(source.net.ID.Value)) return false;
            return null;
        }

        void OnZLevelDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item, int prevAmount, int newAmount)
        {
            if (pcdData.rEntity.ContainsKey(dispenser.baseEntity.net.ID.Value)) item.amount = prevAmount;
        }

        void OnZLevelCollectiblePickup(Item item, BasePlayer player, CollectibleEntity collectible, int prevAmount, int newAmount)
        {
            PrefabInfo pd;
            if (pcdData.rEntity.TryGetValue(collectible.net.ID.Value, out pd))
            {
                if (pd.prevent_gather) item.amount = prevAmount;
            }
        }

        object STCanGainXP(BasePlayer player, BaseEntity source)
        {
            try
            {
                if (source == null || source.net == null) return null;
                PrefabInfo pi;
                if (pcdData.rEntity.TryGetValue(source.net.ID.Value, out pi) && pi != null && pi.prevent_gather) return false;
            }
            catch { }
            return null;
        }

        [HookMethod("IsDeployableNature")]
        public bool IsDeployableNature(BaseEntity entity)
        {
            if (entity == null) return false;
            if (!pcdData.rEntity.TryGetValue(entity.net.ID.Value, out var pi) || !pi.prevent_gather) return false;
            return true;
        }

        object STCanReceiveYield(BasePlayer player, BaseEntity source) => STCanGainXP(player, source);

        #endregion

        #region Nature Market

        Dictionary<int, List<ulong>> market_pages = new Dictionary<int, List<ulong>>();

        private void MenuBackPanel(BasePlayer player)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0 0 0 0.99" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
            }, "Overlay", "MenuBackPanel");

            CuiHelper.DestroyUi(player, "MenuBackPanel");
            CuiHelper.AddUi(player, container);
        }

        void SendNatureMenu(BasePlayer player, int page_number = 0)
        {
            if (market_pages.Count == 0) return;
            var keys = market_pages[page_number];

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
            }, "Overlay", "NatureMarket");

            container.Add(new CuiElement
            {
                Name = "NatureMarket_Title",
                Parent = "NatureMarket",
                Components = {
                    new CuiTextComponent { Text = "Nature Shop", Font = "robotocondensed-bold.ttf", FontSize = 32, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-122.996 115.986", OffsetMax = "122.996 181.014" }
                }
            });

            ItemInfo itemData;

            var modifier = GetModifier(player.UserIDString);

            if (keys.Count > 0)
            {
                itemData = config.Prefabs[keys[0]];
                bool hasPerm = string.IsNullOrEmpty(itemData.permission_required) ? true : permission.UserHasPermission(player.UserIDString, itemData.permission_required) ? true : false;
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.1333333 0.1333333 0.1333333 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-284.319 -2", OffsetMax = "-216.319 66" }
                }, "NatureMarket", "NatureMarket_img_panel_back_1");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.282353 0.282353 0.282353 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32 -32", OffsetMax = "32 32" }
                }, "NatureMarket_img_panel_back_1", "NatureMarket_img_panel_front_1");

                container.Add(new CuiElement
                {
                    Name = "NatureMarket_img_1",
                    Parent = "NatureMarket_img_panel_back_1",
                    Components = {
                    new CuiImageComponent { Color = "1 1 1 1", ItemId = 1751045826, SkinId = keys[0] },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32 -32", OffsetMax = "32 32" }
                }
                });

                if (!hasPerm)
                {
                    container.Add(new CuiElement
                    {
                        Name = "NoPerms",
                        Parent = "NatureMarket_img_1",
                        Components = {
                        new CuiTextComponent { Text = "<color=#ff0000>Missing\nPermission</color>", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                        new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32 -32", OffsetMax = "32 32" }
                    }
                    });
                }                

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.1320755 0.1320755 0.1320755 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "34 -34", OffsetMax = "184 -6" }
                }, "NatureMarket_img_panel_back_1", "NatureMarket_button_panel_back_1");

                container.Add(new CuiElement
                {
                    Name = "NatureMarket_item_title_1",
                    Parent = "NatureMarket_img_panel_back_1",
                    Components = {
                    new CuiTextComponent { Text = itemData.displayName.TitleCase(), Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "40.84 -6", OffsetMax = "184 32" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "0.2830189 0.2830189 0.2830189 1", Command = hasPerm ? $"naturemarketbuyitem 1 {keys[0]}" : "" },
                    Text = { Text = "1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "34 -32", OffsetMax = "82 -8" }
                }, "NatureMarket_img_panel_back_1", "NatureMarket_buy_1_button_1");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.2830189 0.2830189 0.2830189 1", Command = hasPerm ? $"naturemarketbuyitem 10 {keys[0]}" : "" },
                    Text = { Text = "10", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "84 -32", OffsetMax = "132 -8" }
                }, "NatureMarket_img_panel_back_1", "NatureMarket_buy_2_button_1");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.2830189 0.2830189 0.2830189 1", Command = hasPerm ? $"naturemarketbuyitem 20 {keys[0]}" : "" },
                    Text = { Text = "20", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "134 -32", OffsetMax = "182 -8" }
                }, "NatureMarket_img_panel_back_1", "NatureMarket_buy_3_button_1");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.1333333 0.1333333 0.1333333 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "184 -34", OffsetMax = "234 -6" }
                }, "NatureMarket_img_panel_back_1", "NatureMarket_price_panel_1");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.6320754 0.4442004 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -12", OffsetMax = "23 12" }
                }, "NatureMarket_price_panel_1", "NatureMarket_price_panel_front_1");

                container.Add(new CuiElement
                {
                    Name = "NatureMarket_price_1",
                    Parent = "NatureMarket_price_panel_1",
                    Components = {
                    new CuiTextComponent { Text = $"${Math.Round(itemData.market_price * modifier, 1)}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -12", OffsetMax = "23 12" }
                }
                });
            }

            if (keys.Count > 1)
            {
                itemData = config.Prefabs[keys[1]];
                bool hasPerm = string.IsNullOrEmpty(itemData.permission_required) ? true : permission.UserHasPermission(player.UserIDString, itemData.permission_required) ? true : false;
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.1333333 0.1333333 0.1333333 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "66.381 0", OffsetMax = "134.381 68" }
                }, "NatureMarket", "NatureMarket_img_panel_back_2");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.282353 0.282353 0.282353 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32 -32", OffsetMax = "32 32" }
                }, "NatureMarket_img_panel_back_2", "NatureMarket_img_panel_front_2");

                container.Add(new CuiElement
                {
                    Name = "NatureMarket_img_2",
                    Parent = "NatureMarket_img_panel_back_2",
                    Components = {
                    new CuiImageComponent { Color = "1 1 1 1", ItemId = 1751045826, SkinId = keys[1] },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32 -32", OffsetMax = "32 32" }
                }
                });

                if (!hasPerm)
                {
                    container.Add(new CuiElement
                    {
                        Name = "NoPerms",
                        Parent = "NatureMarket_img_2",
                        Components = {
                        new CuiTextComponent { Text = "<color=#ff0000>Missing\nPermission</color>", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                        new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32 -32", OffsetMax = "32 32" }
                    }
                    });
                }

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.1320755 0.1320755 0.1320755 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "34 -34", OffsetMax = "184 -6" }
                }, "NatureMarket_img_panel_back_2", "NatureMarket_button_panel_back_2");

                container.Add(new CuiElement
                {
                    Name = "NatureMarket_item_title_2",
                    Parent = "NatureMarket_img_panel_back_2",
                    Components = {
                    new CuiTextComponent { Text = itemData.displayName.TitleCase(), Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "40.84 -6", OffsetMax = "184 32" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "0.2830189 0.2830189 0.2830189 1", Command = hasPerm ? $"naturemarketbuyitem 1 {keys[1]}" : "" },
                    Text = { Text = "1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "34 -32", OffsetMax = "82 -8" }
                }, "NatureMarket_img_panel_back_2", "NatureMarket_buy_1_button_2");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.2830189 0.2830189 0.2830189 1", Command = hasPerm ? $"naturemarketbuyitem 10 {keys[1]}" : "" },
                    Text = { Text = "10", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "84 -32", OffsetMax = "132 -8" }
                }, "NatureMarket_img_panel_back_2", "NatureMarket_buy_2_button_2");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.2830189 0.2830189 0.2830189 1", Command = hasPerm ? $"naturemarketbuyitem 20 {keys[1]}" : "" },
                    Text = { Text = "20", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "134 -32", OffsetMax = "182 -8" }
                }, "NatureMarket_img_panel_back_2", "NatureMarket_buy_3_button_2");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.1333333 0.1333333 0.1333333 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "184 -34", OffsetMax = "234 -6" }
                }, "NatureMarket_img_panel_back_2", "NatureMarket_price_panel_2");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.6320754 0.4442004 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -12", OffsetMax = "23 12" }
                }, "NatureMarket_price_panel_2", "NatureMarket_price_panel_front_2");

                container.Add(new CuiElement
                {
                    Name = "NatureMarket_price_2",
                    Parent = "NatureMarket_price_panel_2",
                    Components = {
                    new CuiTextComponent { Text = $"${Math.Round(itemData.market_price * modifier, 1)}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -12", OffsetMax = "23 12" }
                }
                });
            }

            if (keys.Count > 2)
            {
                itemData = config.Prefabs[keys[2]];
                bool hasPerm = string.IsNullOrEmpty(itemData.permission_required) ? true : permission.UserHasPermission(player.UserIDString, itemData.permission_required) ? true : false;
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.1333333 0.1333333 0.1333333 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-284.319 -90", OffsetMax = "-216.319 -22" }
                }, "NatureMarket", "NatureMarket_img_panel_back_3");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.282353 0.282353 0.282353 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32 -32", OffsetMax = "32 32" }
                }, "NatureMarket_img_panel_back_3", "NatureMarket_img_panel_front_3");

                container.Add(new CuiElement
                {
                    Name = "NatureMarket_img_3",
                    Parent = "NatureMarket_img_panel_back_3",
                    Components = {
                    new CuiImageComponent { Color = "1 1 1 1", ItemId = 1751045826, SkinId = keys[2] },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32 -32", OffsetMax = "32 32" }
                }
                });

                if (!hasPerm)
                {
                    container.Add(new CuiElement
                    {
                        Name = "NoPerms",
                        Parent = "NatureMarket_img_3",
                        Components = {
                        new CuiTextComponent { Text = "<color=#ff0000>Missing\nPermission</color>", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                        new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32 -32", OffsetMax = "32 32" }
                    }
                    });
                }

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.1320755 0.1320755 0.1320755 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "34 -34", OffsetMax = "184 -6" }
                }, "NatureMarket_img_panel_back_3", "NatureMarket_button_panel_back_3");

                container.Add(new CuiElement
                {
                    Name = "NatureMarket_item_title_3",
                    Parent = "NatureMarket_img_panel_back_3",
                    Components = {
                    new CuiTextComponent { Text = itemData.displayName.TitleCase(), Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "40.84 -6", OffsetMax = "184 32" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "0.2830189 0.2830189 0.2830189 1", Command = hasPerm ? $"naturemarketbuyitem 1 {keys[2]}" : "" },
                    Text = { Text = "1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "34 -32", OffsetMax = "82 -8" }
                }, "NatureMarket_img_panel_back_3", "NatureMarket_buy_1_button_3");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.2830189 0.2830189 0.2830189 1", Command = hasPerm ? $"naturemarketbuyitem 10 {keys[2]}" : "" },
                    Text = { Text = "10", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "84 -32", OffsetMax = "132 -8" }
                }, "NatureMarket_img_panel_back_3", "NatureMarket_buy_2_button_3");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.2830189 0.2830189 0.2830189 1", Command = hasPerm ? $"naturemarketbuyitem 20 {keys[2]}" : "" },
                    Text = { Text = "20", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "134 -32", OffsetMax = "182 -8" }
                }, "NatureMarket_img_panel_back_3", "NatureMarket_buy_3_button_3");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.1333333 0.1333333 0.1333333 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "184 -34", OffsetMax = "234 -6" }
                }, "NatureMarket_img_panel_back_3", "NatureMarket_price_panel_3");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.6320754 0.4442004 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -12", OffsetMax = "23 12" }
                }, "NatureMarket_price_panel_3", "NatureMarket_price_panel_front_3");

                container.Add(new CuiElement
                {
                    Name = "NatureMarket_price_3",
                    Parent = "NatureMarket_price_panel_3",
                    Components = {
                    new CuiTextComponent { Text = $"${Math.Round(itemData.market_price * modifier, 1)}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -12", OffsetMax = "23 12" }
                }
                });
            }

            if (keys.Count > 3)
            {
                itemData = config.Prefabs[keys[3]];
                bool hasPerm = string.IsNullOrEmpty(itemData.permission_required) ? true : permission.UserHasPermission(player.UserIDString, itemData.permission_required) ? true : false;
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.1333333 0.1333333 0.1333333 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "66.381 -90", OffsetMax = "134.381 -22" }
                }, "NatureMarket", "NatureMarket_img_panel_back_4");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.282353 0.282353 0.282353 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32 -32", OffsetMax = "32 32" }
                }, "NatureMarket_img_panel_back_4", "NatureMarket_img_panel_front_4");

                container.Add(new CuiElement
                {
                    Name = "NatureMarket_img_4",
                    Parent = "NatureMarket_img_panel_back_4",
                    Components = {
                    new CuiImageComponent { Color = "1 1 1 1", ItemId = 1751045826, SkinId = keys[3] },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32 -32", OffsetMax = "32 32" }
                }
                });
                if (!hasPerm)
                {
                    container.Add(new CuiElement
                    {
                        Name = "NoPerms",
                        Parent = "NatureMarket_img_4",
                        Components = {
                        new CuiTextComponent { Text = "<color=#ff0000>Missing\nPermission</color>", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                        new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32 -32", OffsetMax = "32 32" }
                    }
                    });
                }

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.1320755 0.1320755 0.1320755 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "34 -34", OffsetMax = "184 -6" }
                }, "NatureMarket_img_panel_back_4", "NatureMarket_button_panel_back_4");

                container.Add(new CuiElement
                {
                    Name = "NatureMarket_item_title_4",
                    Parent = "NatureMarket_img_panel_back_4",
                    Components = {
                    new CuiTextComponent { Text = itemData.displayName.TitleCase(), Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "40.84 -6", OffsetMax = "184 32" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "0.2830189 0.2830189 0.2830189 1", Command = hasPerm ? $"naturemarketbuyitem 1 {keys[3]}" : "" },
                    Text = { Text = "1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "34 -32", OffsetMax = "82 -8" }
                }, "NatureMarket_img_panel_back_4", "NatureMarket_buy_1_button_4");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.2830189 0.2830189 0.2830189 1", Command = hasPerm ? $"naturemarketbuyitem 10 {keys[3]}" : "" },
                    Text = { Text = "10", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "84 -32", OffsetMax = "132 -8" }
                }, "NatureMarket_img_panel_back_4", "NatureMarket_buy_2_button_4");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.2830189 0.2830189 0.2830189 1", Command = hasPerm ? $"naturemarketbuyitem 20 {keys[3]}" : "" },
                    Text = { Text = "20", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "134 -32", OffsetMax = "182 -8" }
                }, "NatureMarket_img_panel_back_4", "NatureMarket_buy_3_button_4");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.1333333 0.1333333 0.1333333 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "184 -34", OffsetMax = "234 -6" }
                }, "NatureMarket_img_panel_back_4", "NatureMarket_price_panel_4");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.6320754 0.4442004 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -12", OffsetMax = "23 12" }
                }, "NatureMarket_price_panel_4", "NatureMarket_price_panel_front_4");

                container.Add(new CuiElement
                {
                    Name = "NatureMarket_price_4",
                    Parent = "NatureMarket_price_panel_4",
                    Components = {
                    new CuiTextComponent { Text = $"${Math.Round(itemData.market_price * modifier, 1)}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -12", OffsetMax = "23 12" }
                }
                });
            }

            if (keys.Count > 4)
            {
                itemData = config.Prefabs[keys[4]];
                bool hasPerm = string.IsNullOrEmpty(itemData.permission_required) ? true : permission.UserHasPermission(player.UserIDString, itemData.permission_required) ? true : false;
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.1333333 0.1333333 0.1333333 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-284.319 -178", OffsetMax = "-216.319 -110" }
                }, "NatureMarket", "NatureMarket_img_panel_back_5");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.282353 0.282353 0.282353 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32 -32", OffsetMax = "32 32" }
                }, "NatureMarket_img_panel_back_5", "NatureMarket_img_panel_front_5");

                container.Add(new CuiElement
                {
                    Name = "NatureMarket_img_5",
                    Parent = "NatureMarket_img_panel_back_5",
                    Components = {
                    new CuiImageComponent { Color = "1 1 1 1", ItemId = 1751045826, SkinId = keys[4] },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32 -32", OffsetMax = "32 32" }
                }
                });
                if (!hasPerm)
                {
                    container.Add(new CuiElement
                    {
                        Name = "NoPerms",
                        Parent = "NatureMarket_img_5",
                        Components = {
                        new CuiTextComponent { Text = "<color=#ff0000>Missing\nPermission</color>", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                        new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32 -32", OffsetMax = "32 32" }
                    }
                    });
                }

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.1320755 0.1320755 0.1320755 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "34 -34", OffsetMax = "184 -6" }
                }, "NatureMarket_img_panel_back_5", "NatureMarket_button_panel_back_5");

                container.Add(new CuiElement
                {
                    Name = "NatureMarket_item_title_5",
                    Parent = "NatureMarket_img_panel_back_5",
                    Components = {
                    new CuiTextComponent { Text = itemData.displayName.TitleCase(), Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "40.84 -6", OffsetMax = "184 32" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "0.2830189 0.2830189 0.2830189 1", Command = hasPerm ? $"naturemarketbuyitem 1 {keys[4]}" : "" },
                    Text = { Text = "1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "34 -32", OffsetMax = "82 -8" }
                }, "NatureMarket_img_panel_back_5", "NatureMarket_buy_1_button_5");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.2830189 0.2830189 0.2830189 1", Command = hasPerm ? $"naturemarketbuyitem 10 {keys[4]}" : "" },
                    Text = { Text = "10", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "84 -32", OffsetMax = "132 -8" }
                }, "NatureMarket_img_panel_back_5", "NatureMarket_buy_2_button_5");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.2830189 0.2830189 0.2830189 1", Command = hasPerm ? $"naturemarketbuyitem 20 {keys[4]}" : "" },
                    Text = { Text = "20", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "134 -32", OffsetMax = "182 -8" }
                }, "NatureMarket_img_panel_back_5", "NatureMarket_buy_3_button_5");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.1333333 0.1333333 0.1333333 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "184 -34", OffsetMax = "234 -6" }
                }, "NatureMarket_img_panel_back_5", "NatureMarket_price_panel_5");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.6320754 0.4442004 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -12", OffsetMax = "23 12" }
                }, "NatureMarket_price_panel_5", "NatureMarket_price_panel_front_5");

                container.Add(new CuiElement
                {
                    Name = "NatureMarket_price_5",
                    Parent = "NatureMarket_price_panel_5",
                    Components = {
                    new CuiTextComponent { Text = $"${Math.Round(itemData.market_price * modifier, 1)}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -12", OffsetMax = "23 12" }
                }
                });
            }

            if (keys.Count > 5)
            {
                itemData = config.Prefabs[keys[5]];
                bool hasPerm = string.IsNullOrEmpty(itemData.permission_required) ? true : permission.UserHasPermission(player.UserIDString, itemData.permission_required) ? true : false;
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.1333333 0.1333333 0.1333333 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "66.381 -178", OffsetMax = "134.381 -110" }
                }, "NatureMarket", "NatureMarket_img_panel_back_6");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.282353 0.282353 0.282353 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32 -32", OffsetMax = "32 32" }
                }, "NatureMarket_img_panel_back_6", "NatureMarket_img_panel_front_6");

                container.Add(new CuiElement
                {
                    Name = "NatureMarket_img_6",
                    Parent = "NatureMarket_img_panel_back_6",
                    Components = {
                    new CuiImageComponent { Color = "1 1 1 1", ItemId = 1751045826, SkinId = keys[5] },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32 -32", OffsetMax = "32 32" }
                }
                });

                if (!hasPerm)
                {
                    container.Add(new CuiElement
                    {
                        Name = "NoPerms",
                        Parent = "NatureMarket_img_6",
                        Components = {
                        new CuiTextComponent { Text = "<color=#ff0000>Missing\nPermission</color>", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                        new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32 -32", OffsetMax = "32 32" }
                    }
                    });
                }

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.1320755 0.1320755 0.1320755 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "34 -34", OffsetMax = "184 -6" }
                }, "NatureMarket_img_panel_back_6", "NatureMarket_button_panel_back_6");

                container.Add(new CuiElement
                {
                    Name = "NatureMarket_item_title_6",
                    Parent = "NatureMarket_img_panel_back_6",
                    Components = {
                    new CuiTextComponent { Text = itemData.displayName.TitleCase(), Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "40.84 -6", OffsetMax = "184 32" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "0.2830189 0.2830189 0.2830189 1", Command = hasPerm ? $"naturemarketbuyitem 1 {keys[5]}" : "" },
                    Text = { Text = "1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "34 -32", OffsetMax = "82 -8" }
                }, "NatureMarket_img_panel_back_6", "NatureMarket_buy_1_button_6");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.2830189 0.2830189 0.2830189 1", Command = hasPerm ? $"naturemarketbuyitem 10 {keys[5]}" : "" },
                    Text = { Text = "10", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "84 -32", OffsetMax = "132 -8" }
                }, "NatureMarket_img_panel_back_6", "NatureMarket_buy_2_button_6");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.2830189 0.2830189 0.2830189 1", Command = hasPerm ? $"naturemarketbuyitem 20 {keys[5]}" : "" },
                    Text = { Text = "20", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "134 -32", OffsetMax = "182 -8" }
                }, "NatureMarket_img_panel_back_6", "NatureMarket_buy_3_button_6");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.1333333 0.1333333 0.1333333 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "184 -34", OffsetMax = "234 -6" }
                }, "NatureMarket_img_panel_back_6", "NatureMarket_price_panel_6");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.6320754 0.4442004 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -12", OffsetMax = "23 12" }
                }, "NatureMarket_price_panel_6", "NatureMarket_price_panel_front_6");

                container.Add(new CuiElement
                {
                    Name = "NatureMarket_price_6",
                    Parent = "NatureMarket_price_panel_6",
                    Components = {
                    new CuiTextComponent { Text = $"${Math.Round(itemData.market_price * modifier, 1)}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -12", OffsetMax = "23 12" }
                }
                });
            }               
            
            if (market_pages.ContainsKey(page_number + 1))
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.1333333 0.1333333 0.1333333 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "66.381 -218.8", OffsetMax = "114.381 -194.8" }
                }, "NatureMarket", "NatureMarket_next_panel");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.282353 0.282353 0.282353 1", Command = $"naturemarketpage {page_number + 1}" },
                    Text = { Text = "NEXT", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-22 -10", OffsetMax = "22 10" }
                }, "NatureMarket_next_panel", "NatureMarket_next_button");                
            }

            if (market_pages.ContainsKey(page_number - 1))
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.1333333 0.1333333 0.1333333 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-115.2 -218.8", OffsetMax = "-67.2 -194.8" }
                }, "NatureMarket", "NatureMarket_back_panel");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.282353 0.282353 0.282353 1", Command = $"naturemarketpage {page_number - 1}" },
                    Text = { Text = "BACK", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-22 -10", OffsetMax = "22 10" }
                }, "NatureMarket_back_panel", "NatureMarket_back_button");
            }

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.1333333 0.1333333 0.1333333 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-24 -218.8", OffsetMax = "24 -194.8" }
            }, "NatureMarket", "NatureMarket_close_panel");

            container.Add(new CuiButton
            {
                Button = { Color = "0.282353 0.282353 0.282353 1", Command = $"naturemarketclose" },
                Text = { Text = "CLOSE", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-22 -10", OffsetMax = "22 10" }
            }, "NatureMarket_close_panel", "NatureMarket_close_button");

            CuiHelper.DestroyUi(player, "NatureMarket");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("naturemarketclose")]
        void CloseNatureMarket(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            CuiHelper.DestroyUi(player, "NatureMarket");
            CuiHelper.DestroyUi(player, "MenuBackPanel");
        }

        [ConsoleCommand("naturemarketpage")]
        void SendMarketPage(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            SendNatureMenu(player, Convert.ToInt32(arg.Args[0]));
        }

        [ConsoleCommand("naturemarketbuyitem")]
        void BuyMarketItem(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            if (market_cooldown.TryGetValue(player.userID, out markettimerData))
            {
                if (!markettimerData.warned)
                {
                    PrintToChat(player, string.Format(lang.GetMessage("WarnMarketCooldown", this, player.UserIDString), Prefix));
                    markettimerData.warned = true;
                }
                return;
            }
            
            var quantity = Convert.ToInt32(arg.Args[0]);
            var key = Convert.ToUInt64(arg.Args[1]);
            ItemInfo itemData;
            if (!config.Prefabs.TryGetValue(key, out itemData)) return;

            var cost = itemData.market_price;
            var modifier = GetModifier(player.UserIDString);

            cost = cost * modifier;

            if (!permission.UserHasPermission(player.UserIDString, "deployablenature.free") && modifier > 0)
            {
                switch (config.currency.ToUpper())
                {                  
                    case "ECONOMICS":
                        var playerBalance = Convert.ToDouble(Economics?.Call("Balance", player.userID.Get()));
                        if (playerBalance < Math.Round(cost * quantity, 1))
                        {
                            PrintToChat(player, string.Format(lang.GetMessage("NEC", this), Prefix, config.currency.TitleCase(), quantity, itemData.displayName));
                            return;
                        }
                        if (!Convert.ToBoolean(Economics?.Call("Withdraw", player.userID.Get(), Math.Round(cost * quantity, 1)))) return;
                        break;

                    case "SR":
                        var totalCost = Convert.ToInt32(cost) * quantity;
                        var balance = Convert.ToInt32(ServerRewards?.Call("CheckPoints", player.userID.Get()));
                        if (balance < totalCost)
                        {
                            PrintToChat(player, string.Format(lang.GetMessage("NEC", this), Prefix, config.currency.TitleCase(), quantity, itemData.displayName));
                            return;
                        }
                        if (!Convert.ToBoolean(ServerRewards?.Call("TakePoints", player.userID.Get(), totalCost))) return;
                        break;

                    case "CUSTOM":
                        if (!PaidWithItem(player, quantity, Convert.ToInt32(cost), config.custom_currency.shortname, config.custom_currency.skinID))
                        {
                            PrintToChat(player, string.Format(lang.GetMessage("NEC", this), Prefix, config.custom_currency.name.TitleCase(), quantity, itemData.displayName));
                            return;
                        }
                        break;
                    default:
                        if (!PaidWithItem(player, quantity, Convert.ToInt32(cost), "scrap"))
                        {
                            PrintToChat(player, string.Format(lang.GetMessage("NEC", this), Prefix, config.currency.TitleCase(), quantity, itemData.displayName));
                            return;
                        }
                        break;
                }
            }            
            AddCooldown(player);
            GiveItem(player, key, quantity);
        }

        [PluginReference]
        private Plugin Economics, ServerRewards, VendingUI;

		bool PaidWithItem(BasePlayer player, int quantity, int cost, string shortname, ulong skin = 0)
		{
			var found = 0;
			var totalCost = quantity * cost;

			// Collect items from all inventory containers (main, belt, and wear)
			List<Item> playerItems = new List<Item>();
			playerItems.AddRange(player.inventory.containerMain.itemList);
			playerItems.AddRange(player.inventory.containerBelt.itemList);
			playerItems.AddRange(player.inventory.containerWear.itemList);

			// First loop: Check if player has enough of the required item
			foreach (var item in playerItems)
			{
				if (item.info.shortname == shortname && item.skin == skin) found += item.amount;
				if (found >= totalCost) break;
			}

			// If not enough items found, return false
			if (found < totalCost) return false;

			found = 0;

			// Second loop: Deduct the required amount of items
			foreach (var item in playerItems)
			{
				if (item.info.shortname == shortname && item.skin == skin)
				{
					if (found >= totalCost) break;

					if (item.amount > totalCost - found)
					{
						item.UseItem(totalCost - found);  // Use the remaining required amount
						break;
					}
					else
					{
						found += item.amount;  // Add the current item's amount to the found total
						item.UseItem(item.amount);  // Use the entire item
					}
				}
			}

			return true;  // If enough items were found and deducted, return true
		}


        Dictionary<ulong, MarketTimerInfo> market_cooldown = new Dictionary<ulong, MarketTimerInfo>();

        MarketTimerInfo markettimerData;

        void AddCooldown(BasePlayer player)
        {
            if (market_cooldown.TryGetValue(player.userID, out markettimerData))
            {
                if (markettimerData._timer != null && !markettimerData._timer.Destroyed) markettimerData._timer.Destroy();
                market_cooldown.Remove(player.userID);
            }
            ulong id = player.userID;
            market_cooldown.Add(id, new MarketTimerInfo()
            {
                _timer = timer.Once(1f, () =>
                {
                    if (market_cooldown.TryGetValue(id, out markettimerData))
                    {
                        if (markettimerData._timer != null && !markettimerData._timer.Destroyed) markettimerData._timer.Destroy();
                        market_cooldown.Remove(id);
                    }
                })
            });

        }

        public class MarketTimerInfo
        {
            public Timer _timer;
            public bool warned;
        }

        #endregion

        #region Harmony

#if CARBON
#else
        [AutoPatch]
#endif
        [HarmonyPatch(typeof(CollectibleEntity), "DoPickup")]
        internal class CollectibleEntity_DoPickup_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(CollectibleEntity __instance, BasePlayer reciever, bool eat)
            {
                if (__instance == null) return true;


                if (__instance == null || __instance.itemList == null || __instance.itemList.Length == 0) return true;
                PrefabInfo prefabData;
                if (Instance.pcdData.rEntity.TryGetValue(__instance.net.ID.Value, out prefabData) && prefabData.prevent_gather)
                {
                    return false;
                }

                return true;
            }
        }

#if CARBON
#else
        [AutoPatch]
#endif
        [HarmonyPatch(typeof(ResourceDispenser), "GiveResourceFromItem")]
        internal class ResourceDispenser_GiveResourceFromItem_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(ResourceDispenser __instance, ItemAmount itemAmt, float gatherDamage, float destroyFraction, AttackEntity attackWeapon)
            {
                if (__instance == null || __instance.baseEntity == null || __instance.baseEntity.net == null) return true;

                if (Instance.pcdData.rEntity.TryGetValue(__instance.baseEntity.net.ID.Value, out var prefabData) && prefabData.prevent_gather) return false;

                return true;
            }
        }

        #endregion
    }
}
