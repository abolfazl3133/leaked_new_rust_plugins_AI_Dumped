using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Globalization;
using Newtonsoft.Json.Converters;
using UnityEngine.UIElements;

/* Ideas
 * - Add a scoreboard
 */

/* Update 1.0.5
 * Added support for discord messages when an event starts.
 * Added support for TreeHit, NodeHit and SkinHit. These will provide bonus xp on all interactions with the nodes.
 */

namespace Oxide.Plugins
{
	[Info("SkillTreeXPEvent", "imthenewguy", "1.0.5")]
	[Description("Creates random events that temporarily boost the xp gain of a randomly selected skill.")]
	class SkillTreeXPEvent : RustPlugin
	{
        #region Config       

        private Configuration config;
        public class Configuration
        {
            [JsonProperty("Time between events [seconds]")]
            public float time_between_events = 1800;

            [JsonProperty("Time between hud updates [seconds]")]
            public int hud_update_time = 3;

            [JsonProperty("Minimum amount of players online before the event will start?")]
            public int minimum_player_count = 1;

            [JsonProperty("Event settings")]
            public Dictionary<Source, XPSources> sources = new Dictionary<Source, XPSources>();

            [JsonProperty("Notification settings")]
            public NotificationSettings notificationSettings  = new NotificationSettings();
            public class NotificationSettings
            {
                [JsonProperty("Hud hint notification settings")]
                public HintNotificationSettings hintNotificationSettings = new HintNotificationSettings();
                public class HintNotificationSettings
                {
                    [JsonProperty("Send event notifications via hud hint?")]
                    public bool sendHint = true;

                    [JsonProperty("Hud hint duration [seconds]")]
                    public float hintLength = 10f;

                    [JsonProperty("Hex colour code for the hud hint")]
                    public string hintCol = "62d507";
                }

                [JsonProperty("UINotify settings")]
                public UINotifySettings uiNotifySettings = new UINotifySettings();
                public class UINotifySettings
                {
                    [JsonProperty("Send event notifications via UINotify?")]
                    public bool enabled = true;

                    [JsonProperty("Notification type")]
                    public int notifyType = 0;
                }

                [JsonProperty("Chat notification settings")]
                public ChatNotifySettings chatNotifySettings = new ChatNotifySettings();
                public class ChatNotifySettings
                {
                    [JsonProperty("Send event notifications via chat message?")]
                    public bool enabled = true;

                    [JsonProperty("Hex colour code of the message [null or empty is vanilla]")]
                    public string hexColour = "62d507";
                }
            }

            [JsonProperty("UI anchor settings")]
            public IconAnchorSettings iconAnchorSettings = new IconAnchorSettings();
            public class IconAnchorSettings
            {
                public string anchor_min = "1 1";
                public string anchor_max = "1 1";
                public string offset_min = "-164.865 -52";
                public string offset_max = "-7.135 -8";
            }

            [JsonProperty("UI color settings")]
            public UIColourSettings uiColourSettings = new UIColourSettings();
            public class UIColourSettings
            {
                public string mainPanelCol = "0.245283 0.245283 0.245283 0.8";
                public string backPanelCol = "0.2075472 0.2075472 0.2075472 1";
            }

            [JsonProperty("Settings for Discord plugin")]
            public DiscordSettings discordSettings = new DiscordSettings();
            public class DiscordSettings
            {
                [JsonProperty("Webhook URL")]
                public string webhook;

                [JsonProperty("Send a discord notification when an event starts?")]
                public bool send_on_event = true;
            }

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }   

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            config.sources = DefaultSources;
        }

        Dictionary<Source, XPSources> DefaultSources
        {
            get
            {
                return new Dictionary<Source, XPSources>()
                {
                    [Source.NodeHitFinal] = new XPSources(3099594829, 0.80f, 1.2f, 300, 1200),
                    [Source.TreeHitFinal] = new XPSources(2668843336, 0.80f, 1.2f, 300, 1200),
                    [Source.SkinHitFinal] = new XPSources(2806645341, 0.80f, 1.2f, 300, 1200),
                    [Source.CollectWildEntities] = new XPSources(3099597204, 0.80f, 1.2f, 300, 1200),
                    [Source.CollectWildBerries] = new XPSources(0, 0.8f, 1.2f, 300, 1200, 50, 1272194103),
                    [Source.CollectWildHemp] = new XPSources(0, 0.8f, 1.2f, 300, 1200, 50, -858312878),
                    [Source.CollectWildPumpkin] = new XPSources(0, 0.8f, 1.2f, 300, 1200, 50, -567909622),
                    [Source.CollectWildPotato] = new XPSources(0, 0.8f, 1.2f, 300, 1200, 50, -2086926071),
                    [Source.CollectWildCorn] = new XPSources(0, 0.8f, 1.2f, 300, 1200, 50, 1367190888),
                    [Source.CollectMushrooms] = new XPSources(2668914545, 0.8f, 1.2f, 300, 1200, 50),
                    [Source.CollectOreNodes] = new XPSources(3099600356, 0.8f, 1.2f, 300, 1200, 50),
                    [Source.CollectGrownEntities] = new XPSources(2668906806, 0.8f, 1.2f, 300, 1200, 25),
                    [Source.CollectGrownBerries] = new XPSources(0, 0.8f, 1.2f, 300, 1200, 10, 1272194103),
                    [Source.CollectGrownHemp] = new XPSources(0, 0.8f, 1.2f, 300, 1200, 10, -858312878),
                    [Source.CollectGrownCorn] = new XPSources(0, 0.8f, 1.2f, 300, 1200, 10, 1367190888),
                    [Source.CollectGrownPotato] = new XPSources(0, 0.8f, 1.2f, 300, 1200, 10, -2086926071),
                    [Source.CollectGrownPumpkin] = new XPSources(0, 0.8f, 1.2f, 300, 1200, 10, -567909622),
                    [Source.SkinWolfFinal] = new XPSources(2806645092, 0.8f, 1.2f, 300, 1200, 50),
                    [Source.SkinBearFinal] = new XPSources(2806645547, 0.8f, 1.2f, 300, 1200, 50),
                    [Source.SkinChickenFinal] = new XPSources(2806645341, 0.8f, 1.2f, 300, 1200, 50),
                    [Source.SkinBoarFinal] = new XPSources(2806645796, 0.8f, 1.2f, 300, 1200, 50),
                    [Source.SkinStagFinal] = new XPSources(2806645210, 0.8f, 1.2f, 300, 1200, 50),
                    [Source.SkinPolarBearFinal] = new XPSources(2806644689, 0.8f, 1.2f, 300, 1200, 50),
                    [Source.CatchAnyFish] = new XPSources(3099603674, 0.80f, 1.2f, 300, 1200),
                    [Source.KillScientist] = new XPSources(3099607227, 0.80f, 1.2f, 300, 1200),
                    [Source.KillTunnelDweller] = new XPSources(3099613770, 0.80f, 1.2f, 300, 1200),
                    [Source.KillUnderwaterDweller] = new XPSources(3099620500, 0.8f, 1.5f, 300, 1200),
                    [Source.KillAnyAnimal] = new XPSources(2806645341, 0.80f, 1.2f, 300, 1200),
                    [Source.KillBear] = new XPSources(2806645547, 0.8f, 1.5f, 300, 1200, 50),
                    [Source.KillStag] = new XPSources(2806645210, 0.8f, 1.5f, 300, 1200, 50),
                    [Source.KillBoar] = new XPSources(2806645796, 0.8f, 1.5f, 300, 1200, 50),
                    [Source.KillChicken] = new XPSources(2806645341, 0.8f, 1.5f, 300, 1200, 50),
                    [Source.KillWolf] = new XPSources(2806645092, 0.8f, 1.5f, 300, 1200, 50),
                    [Source.KillPolarBear] = new XPSources(2806644689, 0.8f, 1.5f, 300, 1200, 50),
                    [Source.LootCrate] = new XPSources(3099621428, 0.80f, 1.2f, 300, 1200),
                    [Source.BreakBarrel] = new XPSources(3099626605, 0.80f, 1.2f, 300, 1200),
                    [Source.BreakRoadSign] = new XPSources(3099628039, 0.80f, 1.2f, 300, 1200),
                    [Source.SwipeCard] = new XPSources(3099628631, 0.80f, 1.2f, 300, 1200),
                    [Source.All] = new XPSources(3193783597, 0.80f, 1.2f, 300, 1200, 0),
                };
            }
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

                SaveConfig();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
                Interface.Oxide.UnloadPlugin(Name);
            }
        }

        protected override void SaveConfig()
        {
            PrintToConsole($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["UIMultiplier"] = "XP Bonus: <color=#45d507>+{0}%</color>",
                ["UITime"] = "Time Left: <color=#d5c907>{0}</color>",

                ["NodeHitFinal"] = "Mining Out Nodes",
                ["TreeHitFinal"] = "Chopping Down Trees",
                ["SkinHitFinal"] = "Fully Skinning Animals",

                ["NodeHit"] = "Mining Nodes",
                ["TreeHit"] = "Chopping Trees",
                ["SkinHit"] = "Skinning Animals",

                ["CollectWildEntities"] = "All Wild Collectables",
                ["CollectWildBerries"] = "Wild Berries",
                ["CollectWildHemp"] = "Wild Hemp",
                ["CollectWildPumpkin"] = "Wild Pumpkins",
                ["CollectWildPotato"] = "Wild Potatoes",
                ["CollectWildCorn"] = "Wild Corn",
                ["CollectMushrooms"] = "Wild Mushrooms",
                ["CollectOreNodes"] = "Collectable nodes",

                ["CollectGrownEntities"] = "All Growables",
                ["CollectGrownBerries"] = "Growable Berries",
                ["CollectGrownHemp"] = "Growable Hemp",
                ["CollectGrownCorn"] = "Growable Corn",
                ["CollectGrownPotato"] = "Growable Potatoes",
                ["CollectGrownPumpkin"] = "Growable Pumpkins",

                ["SkinWolfFinal"] = "Skin Wolves",
                ["SkinBearFinal"] = "Skin Bears",
                ["SkinChickenFinal"] = "Skin Chickens",
                ["SkinBoarFinal"] = "Skin Boars",
                ["SkinStagFinal"] = "Skin Stags",
                ["SkinPolarBearFinal"] = "Skin Polar Bears",

                ["CatchAnyFish"] = "Catch Fish",

                ["KillScientist"] = "Kill Scientists",
                ["KillTunnelDweller"] = "Kill Tunnel Dwellers",
                ["KillUnderwaterDweller"] = "Kill Underwater Dwellers",
                ["KillAnyAnimal"] = "Kill Any Animal",
                ["KillBear"] = "Kill Bears",
                ["KillStag"] = "Kill Stags",
                ["KillBoar"] = "Kill Boars",
                ["KillChicken"] = "Kill Chickens",
                ["KillWolf"] = "Kill Wolves",
                ["KillPolarBear"] = "Kill Polar Bears",

                ["LootCrate"] = "Loot Crates",
                ["BreakBarrel"] = "Break Barrels",
                ["BreakRoadSign"] = "Break Roadsigns",

                ["SwipeCard"] = "Swipe Cards",
                ["All"] = "All Sources",
                ["EventStartedNotification"] = "A new XP event has started!",
                ["EventEndedNotification"] = "The XP event has ended.",
                ["DiscordMessageEventStarted"] = ":globe_with_meridians: **A new XP event has started!** \\n>>> Duration: {0}\\nResource: {1}\\nBonus: +{2}%",
            }, this);
        }

        #endregion

        #region Classes & Enums

        public class XPSources
        {
            [JsonProperty("Chance for this source to be selected [weight system][0=off]")]
            public int source_weight;

            [JsonProperty("Minimum multiplier to roll")]
            public float multiplier_min;

            [JsonProperty("Maximum multiplier to roll")]
            public float multiplier_max;

            [JsonProperty("Minimum duration for the event to run [seconds]")]
            public int duration_min;

            [JsonProperty("Maximum duration for the event to run [seconds]")]
            public int duration_max;

            [JsonProperty("Skin ID that appears in the popup")]
            public ulong icon;

            [JsonProperty("Item id")]
            public int itemid;

            [JsonProperty("Commands fired on start")]
            public List<string> commands_on_start = new List<string>();

            [JsonProperty("Commands fired on end")]
            public List<string> commands_on_end = new List<string>();

            public XPSources(ulong icon, float multiplier_min, float multiplier_max, int duration_min, int duration_max, int source_weight = 100, int itemid = 1751045826)
            {
                this.icon = icon;
                this.multiplier_min = multiplier_min;
                this.multiplier_max = multiplier_max;
                this.duration_min = duration_min;
                this.duration_max = duration_max;
                this.source_weight = source_weight;
                this.itemid = itemid;
            }
        }

        public class ActiveMultiplierInfo
        {
            public Timer _timer;
            public float multiplier;
            public Source source;
            public float max_duration;
            public int itemid;
            public ulong icon;

            public ActiveMultiplierInfo(float multiplier, Source source, float max_duration, ulong icon, int itemid, Timer _timer)
            {
                this.multiplier = multiplier;
                this.source = source;
                this.max_duration = max_duration;
                this._timer = _timer;
                this.icon = icon;
                this.itemid = itemid;
            }
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum Source
        {
            NodeHitFinal,
            TreeHitFinal,
            SkinHitFinal,
            TreeHit,
            NodeHit,
            SkinHit,

            CollectWildEntities,
            CollectWildBerries,
            CollectWildHemp,
            CollectWildPumpkin,
            CollectWildPotato,
            CollectWildCorn,
            CollectMushrooms,
            CollectOreNodes,
            CollectWoodNodes,

            CollectGrownEntities,
            CollectGrownBerries,
            CollectGrownHemp,
            CollectGrownCorn,
            CollectGrownPotato,
            CollectGrownPumpkin,

            SkinWolfFinal,
            SkinBearFinal,
            SkinChickenFinal,
            SkinBoarFinal,
            SkinStagFinal,
            SkinPolarBearFinal,

            CatchAnyFish,

            KillScientist,
            KillTunnelDweller,
            KillUnderwaterDweller,
            KillAnyAnimal,
            KillBear,
            KillStag,
            KillBoar,
            KillChicken,
            KillWolf,
            KillPolarBear,

            LootCrate,
            BreakBarrel,
            BreakRoadSign,

            SwipeCard,

            All,
        }

        #endregion

        #region Skill Tree Stuff

        ActiveMultiplierInfo ActiveMultiplier;

        [HookMethod("GetEventXP")]
        public double GetEventXP(BasePlayer player, BaseEntity entity, double value, string source_string = null)
        {
            if (entity == null && string.IsNullOrEmpty(source_string)) return 0;

            if (ActiveMultiplier == null) return 0;

            if (!permission.UserHasPermission(player.UserIDString, perm_use)) return 0;
            if (!IsSourceMatch(entity, ActiveMultiplier.source, source_string)) return 0;

            return value + (value * ActiveMultiplier.multiplier);
        }

        bool IsSourceMatch(BaseEntity entity, Source source, string source_string)
        {
            if (source == Source.All) return true;
            if (entity != null)
            {
                if (entity is GrowableEntity)
                {
                    switch (entity.ShortPrefabName)
                    {
                        case "hemp.entity":
                            return source == Source.CollectGrownHemp || source == Source.CollectGrownEntities;

                        case "corn.entity":
                            return source == Source.CollectGrownCorn || source == Source.CollectGrownEntities;

                        case "potato.entity":
                            return source == Source.CollectGrownPotato || source == Source.CollectGrownEntities;

                        case "pumpkin.entity":
                            return source == Source.CollectGrownPumpkin || source == Source.CollectGrownEntities;

                        case "black_berry.entity":
                        case "blue_berry.entity":
                        case "green_berry.entity":
                        case "red_berry.entity":
                        case "white_berry.entity":
                        case "yellow_berry.entity":
                            return source == Source.CollectGrownBerries || source == Source.CollectGrownEntities;

                        default: return false;
                    }
                }

                if (entity is CollectibleEntity)
                {
                    switch (entity.ShortPrefabName)
                    {
                        case "hemp-collectable":
                            return source == Source.CollectWildHemp || source == Source.CollectWildEntities;

                        case "corn-collectable":
                            return source == Source.CollectWildCorn || source == Source.CollectWildEntities;

                        case "pumpkin-collectable":
                            return source == Source.CollectWildPumpkin || source == Source.CollectWildEntities;

                        case "potato-collectable":
                            return source == Source.CollectWildPotato || source == Source.CollectWildEntities;

                        case "berry-black-collectable":
                        case "berry-blue-collectable":
                        case "berry-yellow-collectable":
                        case "berry-white-collectable":
                        case "berry-red-collectable":
                        case "berry-green-collectable":
                            return source == Source.CollectWildBerries || source == Source.CollectWildEntities;

                        case "mushroom-cluster-5":
                        case "mushroom-cluster-6":
                            return source == Source.CollectMushrooms || source == Source.CollectWildEntities;

                        case "sulfur-collectable":
                        case "metal-collectable":
                        case "stone-collectable":
                            return source == Source.CollectOreNodes || source == Source.CollectWildEntities;

                        case "wood-collectable":
                            return source == Source.CollectWoodNodes || source == Source.CollectWildEntities;

                        case "diesel_collectable":
                            return source == Source.CollectWildEntities;

                        default: return false;
                    }
                }

                if (entity is LootContainer)
                {
                    switch (entity.ShortPrefabName)
                    {
                        case "loot-barrel-1":
                        case "loot-barrel-2":
                        case "loot_barrel_1":
                        case "loot_barrel_2":
                        case "oil_barrel":
                            return source == Source.BreakBarrel;

                        case "crate_normal_2":
                        case "codelockedhackablecrate_oilrig":
                        case "codelockedhackablecrate":
                        case "heli_crate":
                        case "bradley_crate":
                        case "crate_basic":
                        case "crate_elite":
                        case "crate_mine":
                        case "crate_normal":
                        case "crate_normal_2_food":
                        case "crate_normal_2_medical":
                        case "crate_tools":
                        case "crate_underwater_advanced":
                        case "crate_underwater_basic":
                        case "crate_ammunition":
                        case "crate_food_1":
                        case "crate_food_2":
                        case "crate_fuel":
                        case "crate_medical":
                        case "trash-pile-1":
                        case "supply_drop":
                        case "vehicle_parts":
                        case "minecart":
                            return source == Source.LootCrate;

                        case "roadsign1":
                        case "roadsign2":
                        case "roadsign3":
                        case "roadsign4":
                        case "roadsign5":
                        case "roadsign6":
                        case "roadsign7":
                        case "roadsign8":
                        case "roadsign9":
                            return source == Source.BreakRoadSign;

                        default: return false;
                    }
                }

                if (entity is BaseAnimalNPC)
                {
                    switch (entity.ShortPrefabName)
                    {
                        case "bear":
                            return source == Source.KillBear || source == Source.KillAnyAnimal;

                        case "boar":
                            return source == Source.KillBoar || source == Source.KillAnyAnimal;

                        case "wolf":
                            return source == Source.KillWolf || source == Source.KillAnyAnimal;

                        case "stag":
                            return source == Source.KillStag || source == Source.KillAnyAnimal;

                        case "chicken":
                            return source == Source.KillChicken || source == Source.KillAnyAnimal;

                        case "polarbear":
                            return source == Source.KillPolarBear || source == Source.KillAnyAnimal;

                        default: return false;
                    }
                }

                if (entity is BaseCorpse)
                {
                    switch (entity.ShortPrefabName)
                    {
                        case "chicken.corpse":
                            return source == Source.SkinChickenFinal || source == Source.SkinHitFinal;

                        case "wolf.corpse":
                            return source == Source.SkinWolfFinal || source == Source.SkinHitFinal;

                        case "bear.corpse":
                            return source == Source.SkinBearFinal || source == Source.SkinHitFinal;

                        case "polarbear.corpse":
                            return source == Source.SkinPolarBearFinal || source == Source.SkinHitFinal;

                        case "boar.corpse":
                            return source == Source.SkinBoarFinal || source == Source.SkinHitFinal;

                        case "stag.corpse":
                            return source == Source.SkinStagFinal || source == Source.SkinHitFinal;

                        default: return false;
                    }
                }

                if (entity is ScientistNPC) return source == Source.KillScientist;
                if (entity is TunnelDweller) return source == Source.KillTunnelDweller;
                if (entity is UnderwaterDweller) return source == Source.KillUnderwaterDweller;
            }            

            if (string.IsNullOrEmpty(source_string)) return false;

            switch (source_string)
            {
                case "woodcutting":
                    return source == Source.TreeHit;

                case "woodcutting final":
                    return source == Source.TreeHitFinal || source == Source.TreeHit;

                case "mining":
                    return source == Source.NodeHit;

                case "mining final":
                    return source == Source.NodeHitFinal || source == Source.NodeHit;

                case "skinning":
                    return source == Source.SkinHit;

                case "skinning final":
                    return source == Source.SkinHitFinal || source == Source.SkinHit;

                case "fishing":
                    return source == Source.CatchAnyFish;

                case "green swipe":
                case "blue swipe":
                case "red swipe":
                    return source == Source.SwipeCard;
            }

            return false;
        }

        #endregion

        #region Hooks

        const string perm_admin = "skilltreexpevent.admin";
        const string perm_use = "skilltreexpevent.use";
        void Init()
        {
            permission.RegisterPermission(perm_admin, this);
            permission.RegisterPermission(perm_use, this);
        }

        void Unload()
        {
            DestroyUI();
            SourceDef?.Clear();
            if (SkillTree != null && SkillTree.IsLoaded) SkillTree.Call("SetXPEventState", false);
            if (ReqConfigSave)
            {
                Puts("Saving config.");
                SaveConfig();
            }

            foreach (var player in BasePlayer.activePlayerList)
                DestroyMoverUI(player);

            foreach (var kvp in timers)
                if (!kvp.Value.Destroyed) kvp.Value.Destroy();

            timers.Clear();
            timers = null;
        }

        Dictionary<string, Source> SourceDef = new Dictionary<string, Source>(StringComparer.InvariantCultureIgnoreCase);

        [PluginReference]
        private Plugin SkillTree, UINotify;

        public AnchorInfo Anchor;
        public class AnchorInfo
        {
            public float Anchor_min_x;
            public float Anchor_min_y;
            public float Anchor_max_x;
            public float Anchor_max_y;
            public float Offset_Min_x;
            public float Offset_Min_y;
            public float Offset_Max_x;
            public float Offset_Max_y;
        }

        bool ReqConfigSave = false;
        void LoadAnchorFromConfig()
        {
            if (Anchor == null) Anchor = new AnchorInfo();
            if (string.IsNullOrEmpty(config.iconAnchorSettings.anchor_min) || string.IsNullOrEmpty(config.iconAnchorSettings.anchor_max) || string.IsNullOrEmpty(config.iconAnchorSettings.offset_min) || string.IsNullOrEmpty(config.iconAnchorSettings.offset_max))
            {
                Puts("Error loading the anchor. Setting to default.");
                config.iconAnchorSettings = new Configuration.IconAnchorSettings();
                ReqConfigSave = true;
            }

            var strArray = config.iconAnchorSettings.anchor_min.Split(' ');
            if (strArray.Length != 2)
            {
                config.iconAnchorSettings.anchor_min = "1 1";
                ReqConfigSave = true;
                LoadAnchorFromConfig();
                return;
            }
            Anchor.Anchor_min_x = Convert.ToSingle(strArray[0]);
            Anchor.Anchor_min_y = Convert.ToSingle(strArray[1]);

            strArray = config.iconAnchorSettings.anchor_max.Split(' ');
            if (strArray.Length != 2)
            {
                config.iconAnchorSettings.anchor_max = "1 1";
                ReqConfigSave = true;
                LoadAnchorFromConfig();
                return;
            }
            Anchor.Anchor_max_x = Convert.ToSingle(strArray[0]);
            Anchor.Anchor_max_y = Convert.ToSingle(strArray[1]);

            strArray = config.iconAnchorSettings.offset_min.Split(' ');
            if (strArray.Length != 2)
            {
                config.iconAnchorSettings.offset_min = "-164.865 -52";
                ReqConfigSave = true;
                LoadAnchorFromConfig();
                return;
            }
            Anchor.Offset_Min_x = Convert.ToSingle(strArray[0]);
            Anchor.Offset_Min_y = Convert.ToSingle(strArray[1]);

            strArray = config.iconAnchorSettings.offset_max.Split(' ');
            if (strArray.Length != 2)
            {
                config.iconAnchorSettings.offset_max = "-7.135 -8";
                ReqConfigSave = true;
                LoadAnchorFromConfig();
                return;
            }
            Anchor.Offset_Max_x = Convert.ToSingle(strArray[0]);
            Anchor.Offset_Max_y = Convert.ToSingle(strArray[1]);
        }

        void SaveAnchorToConfig()
        {
            config.iconAnchorSettings.anchor_min = $"{Anchor.Anchor_min_x} {Anchor.Anchor_min_y}";
            config.iconAnchorSettings.anchor_max = $"{Anchor.Anchor_max_x} {Anchor.Anchor_max_y}";
            config.iconAnchorSettings.offset_min = $"{Anchor.Offset_Min_x} {Anchor.Offset_Min_y}";
            config.iconAnchorSettings.offset_max = $"{Anchor.Offset_Max_x} {Anchor.Offset_Max_y}";
            ReqConfigSave = true;
        }

        void OnServerSave()
        {
            if (ReqConfigSave)
            {
                ReqConfigSave = false;
                SaveConfig();
            }
        }


        void OnServerInitialized(bool initial)
        {
            if (SkillTree == null || !SkillTree.IsLoaded)
            {
                Puts("This plugin requires SkillTree to run. Unloading...");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }

            foreach (var source in Enum.GetValues(typeof(Source)).Cast<Source>())
            {
                SourceDef.Add(source.ToString(), source);
            }

            bool requiresSave = false;
            if (config.sources.Count == 0)
            {
                config.sources = DefaultSources;
                requiresSave = true;
            }
            else
            {
                foreach (var source in DefaultSources)
                {
                    if (config.sources.ContainsKey(source.Key)) continue;
                    config.sources.Add(source.Key, source.Value);
                    Puts($"Added new source xp event source: {source.Key}");
                    requiresSave = true;
                }
            }
            if (requiresSave) SaveConfig();

            LoadAnchorFromConfig();
            StartNextEventCountdown();
        }

        [HookMethod("IsXPEventRunning")]
        public bool IsXPEventRunning() => ActiveMultiplier != null;

        Timer NextEventTimer;
        void StartNextEventCountdown()
        {
            if (NextEventTimer != null && !NextEventTimer.Destroyed) NextEventTimer.Destroy();
            if (config.time_between_events <= 0) return;
            NextEventTimer = timer.Once(config.time_between_events, () =>
            {
                if (!EnoughPlayersOnline())
                {
                    StartNextEventCountdown();
                    return;
                }
                var source = GetRandomSource();
                SetupMultiplier(Mathf.Round(UnityEngine.Random.Range(source.Value.multiplier_min, source.Value.multiplier_max) * 10.0f) * 0.1f, UnityEngine.Random.RandomRange(source.Value.duration_min, source.Value.duration_max + 1), source.Key, source.Value.icon, source.Value.itemid);
            });
        }

        bool EnoughPlayersOnline()
        {
            if (config.minimum_player_count == 0) return true;
            int found = 0;
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (!permission.UserHasPermission(player.UserIDString, perm_use)) continue;
                if (++found >= config.minimum_player_count) return true;
            }

            return false;
        }

        KeyValuePair<Source, XPSources> GetRandomSource()
        {
            var totalWeight = config.sources.Sum(x => x.Value.source_weight);
            var roll = UnityEngine.Random.Range(0, totalWeight + 1);
            var count = 0;
            foreach (var source in config.sources)
            {
                count += source.Value.source_weight;
                if (roll <= count) return source;
            }

            Puts("Failed to find source using weight. Getting random one.");
            List<KeyValuePair<Source, XPSources>> list = Pool.GetList<KeyValuePair<Source, XPSources>>();
            list.AddRange(config.sources);
            var result = list.GetRandom();
            Pool.FreeList(ref list);
            return result;
        }

        public void SetupMultiplier(float multipler, int duration, Source source, ulong icon, int itemid)
        {
            if (ActiveMultiplier != null) DestroyActiveMultiplier();

            var elapsedTime = 0;
            ActiveMultiplier = new ActiveMultiplierInfo(multipler, source, duration, icon, itemid, timer.Every(config.hud_update_time, () =>
            {
                // Handle logic here for displaying info to players
                var timeLeft = duration - elapsedTime;
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (!permission.UserHasPermission(player.UserIDString, perm_use)) continue;
                    BonusXPUI(player, source, timeLeft, multipler, icon, itemid);
                }
                elapsedTime += config.hud_update_time;
                if (elapsedTime >= duration)
                {
                    // Handle timer ending stuff here.
                    DestroyActiveMultiplier();
                    DestroyUI();
                    StartNextEventCountdown();
                    foreach (var command in config.sources[source].commands_on_end)
                        rust.RunServerCommand(command);
                    return;
                }
            }));

            // Notification settings
            foreach (var player in BasePlayer.activePlayerList)
            {
                var message = lang.GetMessage("EventStartedNotification", this, player.UserIDString);
                CreateGameTip(message, player);
                SendNotify(player, message, config.notificationSettings.uiNotifySettings.notifyType);
                SendChatMessage(player, message);
            }

            if (SkillTree != null && SkillTree.IsLoaded)
            {
                SkillTree.Call("SetXPEventState", true);
            }

            foreach (var command in config.sources[source].commands_on_start)
                rust.RunServerCommand(command);

            var time = TimeSpan.FromSeconds(duration);
            SendDiscordMsg(string.Format(lang.GetMessage("DiscordMessageEventStarted", this), duration >= 3600 ? $"{time.Hours}h {time.Minutes}m {time.Seconds}s" : duration >= 60 ? $"{time.Minutes}m {time.Seconds}s" : $"{time.Seconds}s", lang.GetMessage(source.ToString(), this), multipler * 100));
        }

        public void DestroyActiveMultiplier()
        {
            if (ActiveMultiplier != null)
            {
                if (ActiveMultiplier._timer != null && !ActiveMultiplier._timer.Destroyed) ActiveMultiplier._timer.Destroy();
                ActiveMultiplier = null;
            }            
            if (SkillTree != null && SkillTree.IsLoaded) SkillTree.Call("SetXPEventState", false);
        }

        #endregion

        #region UI

        #region Event display panel

        private void BonusXPUI(BasePlayer player, Source source, int timeLeft, float multiplier, ulong icon, int itemid = 1751045826)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = config.uiColourSettings.mainPanelCol },
                RectTransform = { AnchorMin = $"{Anchor.Anchor_min_x} {Anchor.Anchor_min_y}", AnchorMax = $"{Anchor.Anchor_max_x} {Anchor.Anchor_max_y}", OffsetMin = $"{Anchor.Offset_Min_x} {Anchor.Offset_Min_y}", OffsetMax = $"{Anchor.Offset_Max_x} {Anchor.Offset_Max_y}" }
            }, "Hud", "BonusXPUI");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = config.uiColourSettings.backPanelCol },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-78.865 6", OffsetMax = "78.865 22" }
            }, "BonusXPUI", "Panel_9145");

            container.Add(new CuiElement
            {
                Name = "Source",
                Parent = "BonusXPUI",
                Components = {
                    new CuiTextComponent { Text = lang.GetMessage(source.ToString(), this, player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32.257 6", OffsetMax = "123.103 22" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "XPMultiplier",
                Parent = "BonusXPUI",
                Components = {
                    new CuiTextComponent { Text = string.Format(lang.GetMessage("UIMultiplier", this, player.UserIDString), multiplier * 100), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32.257 -8", OffsetMax = "78.863 6" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "TimeLeft",
                Parent = "BonusXPUI",
                Components = {
                    new CuiTextComponent { Text = string.Format(lang.GetMessage("UITime", this, player.UserIDString), GetTimeLeft(timeLeft)), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32.262 -22", OffsetMax = "78.868 -8" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = config.uiColourSettings.backPanelCol },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-78.867 -22", OffsetMax = "-34.867 22" }
            }, "BonusXPUI", "imgPanel");

            container.Add(new CuiElement
            {
                Name = "img",
                Parent = "imgPanel",
                Components = {
                    new CuiImageComponent { Color = "1 1 1 1", ItemId = itemid, SkinId = icon },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-20 -19.5", OffsetMax = "20 20.5" }
                }
            });

            CuiHelper.DestroyUi(player, "BonusXPUI");
            CuiHelper.AddUi(player, container);
        }

        string GetTimeLeft(int timeLeft)
        {
            if (timeLeft > TimeSpan.MaxValue.TotalSeconds) return "ALOT!";
            TimeSpan t = TimeSpan.FromSeconds(timeLeft);
            if (timeLeft > 3600) return string.Format("{0:D2}h {1:D2}m {2:D2}s", t.Hours, t.Minutes, t.Seconds);
            return string.Format("{0:D2}m {1:D2}s", t.Minutes, t.Seconds);
        }

        void DestroyUI()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "BonusXPUI");
            }
        }

        #endregion

        #region UI Mover

        [ChatCommand("stemoveui")]
        void MoveUICommand(BasePlayer player) => SendUIMover(player);

        void SendUIMover(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, perm_admin)) return;
            MoverUI(player);
            AnchorPanel(player);
            if (ActiveMultiplier == null) BonusXPUI(player, Source.BreakBarrel, 1000, 2, 0);
        }

        void DestroyMoverUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "MoverUI");
            CuiHelper.DestroyUi(player, "AnchorPanel");
            if (ActiveMultiplier == null) CuiHelper.DestroyUi(player, "BonusXPUI");
        }

        [ConsoleCommand("STCloseAdjusterPanel")]
        void ClosePanel(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            SaveAnchorToConfig();
            DestroyMoverUI(player);
        }

        #region Mover UI Panel

        [ConsoleCommand("STXPAdjustPanel")]
        void AdjustOffset(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            var moveX = Convert.ToSingle(arg.Args[0]);
            var moveY = Convert.ToSingle(arg.Args[1]);

            Anchor.Offset_Min_x += moveX;
            Anchor.Offset_Max_x += moveX;

            Anchor.Offset_Min_y += moveY;
            Anchor.Offset_Max_y += moveY;

            MoverUI(player);
            if (ActiveMultiplier == null) BonusXPUI(player, Source.BreakBarrel, 1000, 2, 0);
        }        

        private void MoverUI(BasePlayer player)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-16 -16", OffsetMax = "16 16" }
            }, "Overlay", "MoverUI");

            container.Add(new CuiButton
            {
                Button = { Color = "0.5411765 0.5411765 0.5411765 1", Command = "STCloseAdjusterPanel" },
                Text = { Text = "SAVE", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-16 -16", OffsetMax = "16 16" }
            }, "MoverUI", "Close");

            container.Add(new CuiButton
            {
                Button = { Color = "0.5411765 0.5411765 0.5411765 1", Command = $"STXPAdjustPanel {0} {2}" },
                Text = { Text = "UP", Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-16 18", OffsetMax = "16 50" }
            }, "MoverUI", "UpLow");

            container.Add(new CuiButton
            {
                Button = { Color = "0.5411765 0.5411765 0.5411765 1", Command = $"STXPAdjustPanel {0} {10}" },
                Text = { Text = "UP+", Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-16 52", OffsetMax = "16 84" }
            }, "MoverUI", "UpHigh");

            container.Add(new CuiButton
            {
                Button = { Color = "0.5411765 0.5411765 0.5411765 1", Command = $"STXPAdjustPanel {-2} {0}" },
                Text = { Text = "LEFT", Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-50 -16", OffsetMax = "-18 16" }
            }, "MoverUI", "LeftLow");

            container.Add(new CuiButton
            {
                Button = { Color = "0.5411765 0.5411765 0.5411765 1", Command = $"STXPAdjustPanel {-10} {0}" },
                Text = { Text = "LEFT+", Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-84 -16", OffsetMax = "-52 16" }
            }, "MoverUI", "LeftHigh");

            container.Add(new CuiButton
            {
                Button = { Color = "0.5411765 0.5411765 0.5411765 1", Command = $"STXPAdjustPanel {0} {-2}" },
                Text = { Text = "DOWN", Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-16 -50", OffsetMax = "16 -18" }
            }, "MoverUI", "DownLow");

            container.Add(new CuiButton
            {
                Button = { Color = "0.5411765 0.5411765 0.5411765 1", Command = $"STXPAdjustPanel {0} {-10}" },
                Text = { Text = "DOWN+", Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-16 -84", OffsetMax = "16 -52" }
            }, "MoverUI", "DownHigh");

            container.Add(new CuiButton
            {
                Button = { Color = "0.5411765 0.5411765 0.5411765 1", Command = $"STXPAdjustPanel {2} {0}" },
                Text = { Text = "RIGHT", Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "18 -16", OffsetMax = "50 16" }
            }, "MoverUI", "RightLow");

            container.Add(new CuiButton
            {
                Button = { Color = "0.5411765 0.5411765 0.5411765 1", Command = $"STXPAdjustPanel {10} {0}" },
                Text = { Text = "RIGHT+", Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "52 -16", OffsetMax = "84 16" }
            }, "MoverUI", "RightHigh");

            CuiHelper.DestroyUi(player, "MoverUI");
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Anchor Position panel

        const string AnchorPanelSelectedCol = "0.002124696 0.5849056 0 1";
        const string AnchorPanelDefaultCol = "0.5411765 0.5411765 0.5411765 1";

        enum Position
        {
            TopLeft,
            TopMiddle,
            TopRight,
            MiddleLeft,
            Middle,
            MiddleRight,
            BottomLeft,
            BottomMiddle,
            BottomRight
        }

        Position GetCurrentPosition()
        {
            if (Anchor.Anchor_min_x == 0 && Anchor.Anchor_min_y == 1 && Anchor.Anchor_max_x == 0 && Anchor.Anchor_max_y == 1) return Position.TopLeft;
            if (Anchor.Anchor_min_x == 0.5f && Anchor.Anchor_min_y == 1 && Anchor.Anchor_max_x == 0.5f && Anchor.Anchor_max_y == 1) return Position.TopMiddle;
            if (Anchor.Anchor_min_x == 1 && Anchor.Anchor_min_y == 1 && Anchor.Anchor_max_x == 1 && Anchor.Anchor_max_y == 1) return Position.TopRight;

            if (Anchor.Anchor_min_x == 0 && Anchor.Anchor_min_y == 0.5f && Anchor.Anchor_max_x == 0 && Anchor.Anchor_max_y == 0.5f) return Position.MiddleLeft;
            if (Anchor.Anchor_min_x == 0.5f && Anchor.Anchor_min_y == 0.5f && Anchor.Anchor_max_x == 0.5f && Anchor.Anchor_max_y == 0.5f) return Position.Middle;
            if (Anchor.Anchor_min_x == 1 && Anchor.Anchor_min_y == 0.5f && Anchor.Anchor_max_x == 1 && Anchor.Anchor_max_y == 0.5f) return Position.MiddleRight;

            if (Anchor.Anchor_min_x == 0 && Anchor.Anchor_min_y == 0 && Anchor.Anchor_max_x == 0 && Anchor.Anchor_max_y == 0) return Position.BottomLeft;
            if (Anchor.Anchor_min_x == 0.5f && Anchor.Anchor_min_y == 0 && Anchor.Anchor_max_x == 0.5f && Anchor.Anchor_max_y == 0) return Position.BottomMiddle;
            if (Anchor.Anchor_min_x == 1 && Anchor.Anchor_min_y == 0 && Anchor.Anchor_max_x == 1 && Anchor.Anchor_max_y == 0) return Position.BottomRight;

            return Position.TopRight;
        }

        float[] SetNewAnchor(Position position)
        {
            switch (position)
            {
                case Position.TopLeft: return new float[] { 0, 1, 0, 1 };
                case Position.TopMiddle: return new float[] { 0.5f, 1, 0.5f, 1 };
                case Position.TopRight: return new float[] { 1, 1, 1, 1 };

                case Position.MiddleLeft: return new float[] { 0, 0.5f, 0, 0.5f };
                case Position.Middle: return new float[] { 0.5f, 0.5f, 0.5f, 0.5f };
                case Position.MiddleRight: return new float[] { 1, 0.5f, 1, 0.5f };

                case Position.BottomLeft: return new float[] { 0, 0, 0, 0 };
                case Position.BottomMiddle: return new float[] { 0.5f, 0, 0.5f, 0 };
                case Position.BottomRight: return new float[] { 1, 0, 1, 0 };

                default: return new float[] { 1, 1, 1, 1 };
            }
        }

        [ConsoleCommand("STXPSetAnchorPos")]
        void SetAnchorPosition(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            var num = Convert.ToInt32(arg.Args[0]);
            var arr = SetNewAnchor((Position)num);            

            Anchor.Anchor_min_x = arr[0];
            Anchor.Anchor_min_y = arr[1];
            Anchor.Anchor_max_x = arr[2];
            Anchor.Anchor_max_y = arr[3];

            AnchorPanel(player);
            if (ActiveMultiplier == null) BonusXPUI(player, Source.BreakBarrel, 1000, 2, 0);
        }

        private void AnchorPanel(BasePlayer player)
        {
            var currentPos = GetCurrentPosition();

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.245283 0.245283 0.245283 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-237.1 -52", OffsetMax = "-133.1 52" }
            }, "Overlay", "AnchorPanel");

            container.Add(new CuiButton
            {
                Button = { Color = currentPos == Position.TopLeft ? AnchorPanelSelectedCol : AnchorPanelDefaultCol, Command = $"STXPSetAnchorPos {(int)Position.TopLeft}" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-50 18", OffsetMax = "-18 50" }
            }, "AnchorPanel", "TopLeft");

            container.Add(new CuiButton
            {
                Button = { Color = currentPos == Position.TopMiddle ? AnchorPanelSelectedCol : AnchorPanelDefaultCol, Command = $"STXPSetAnchorPos {(int)Position.TopMiddle}" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-16 18", OffsetMax = "16 50" }
            }, "AnchorPanel", "TopMiddle");

            container.Add(new CuiButton
            {
                Button = { Color = currentPos == Position.TopRight ? AnchorPanelSelectedCol : AnchorPanelDefaultCol, Command = $"STXPSetAnchorPos {(int)Position.TopRight}" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "18 18", OffsetMax = "50 50" }
            }, "AnchorPanel", "TopRight");

            container.Add(new CuiButton
            {
                Button = { Color = currentPos == Position.MiddleLeft ? AnchorPanelSelectedCol : AnchorPanelDefaultCol, Command = $"STXPSetAnchorPos {(int)Position.MiddleLeft}" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-50 -16", OffsetMax = "-18 16" }
            }, "AnchorPanel", "MiddleLeft");

            container.Add(new CuiButton
            {
                Button = { Color = currentPos == Position.Middle ? AnchorPanelSelectedCol : AnchorPanelDefaultCol, Command = $"STXPSetAnchorPos {(int)Position.Middle}" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-16 -16", OffsetMax = "16 16" }
            }, "AnchorPanel", "Middle");

            container.Add(new CuiButton
            {
                Button = { Color = currentPos == Position.MiddleRight ? AnchorPanelSelectedCol : AnchorPanelDefaultCol, Command = $"STXPSetAnchorPos {(int)Position.MiddleRight}" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "18 -16", OffsetMax = "50 16" }
            }, "AnchorPanel", "MiddleRight");

            container.Add(new CuiButton
            {
                Button = { Color = currentPos == Position.BottomLeft ? AnchorPanelSelectedCol : AnchorPanelDefaultCol, Command = $"STXPSetAnchorPos {(int)Position.BottomLeft}" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-50 -50", OffsetMax = "-18 -18" }
            }, "AnchorPanel", "BottomLeft");

            container.Add(new CuiButton
            {
                Button = { Color = currentPos == Position.BottomMiddle ? AnchorPanelSelectedCol : AnchorPanelDefaultCol, Command = $"STXPSetAnchorPos {(int)Position.BottomMiddle}" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-16 -50", OffsetMax = "16 -18" }
            }, "AnchorPanel", "BottomMiddle");

            container.Add(new CuiButton
            {
                Button = { Color = currentPos == Position.BottomRight ? AnchorPanelSelectedCol : AnchorPanelDefaultCol, Command = $"STXPSetAnchorPos {(int)Position.BottomRight}" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "18 -50", OffsetMax = "50 -18" }
            }, "AnchorPanel", "BottomRight");

            container.Add(new CuiElement
            {
                Name = "Label_1469",
                Parent = "AnchorPanel",
                Components = {
                    new CuiTextComponent { Text = "Anchor Position", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-50 54.3", OffsetMax = "50 80.3" }
                }
            });

            CuiHelper.DestroyUi(player, "AnchorPanel");
            CuiHelper.AddUi(player, container);
        }

        #endregion

            #endregion

            #endregion

        #region Commands

        [ConsoleCommand("stopxpevent")]
        void StopXPEventCMD(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, perm_admin)) return;

            if (ActiveMultiplier == null)
            {
                arg.ReplyWith("No XP events are running.");
                return;
            }

            DestroyActiveMultiplier();
            DestroyUI(); 
            StartNextEventCountdown();

            foreach (var p in BasePlayer.activePlayerList)
                PrintToChat(p, lang.GetMessage("EventEndedNotification", this, p.UserIDString));
        }

        [ConsoleCommand("startxpevent")]
        void StartXPEventCMD(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, perm_admin)) return;

            if (ActiveMultiplier != null)
            {
                arg.ReplyWith("There is already an event running. Stop this event with the [stopxpevent] command and try again.");
                return;
            }

            var task = !arg.Args.IsNullOrEmpty() ? string.Join("", arg.Args) : null;

            var source = task == null ? GetRandomSource() : ConvertToSource(task);
            SetupMultiplier(Mathf.Round(UnityEngine.Random.Range(source.Value.multiplier_min, source.Value.multiplier_max) * 10.0f) * 0.1f, UnityEngine.Random.RandomRange(source.Value.duration_min, source.Value.duration_max + 1), source.Key, source.Value.icon, source.Value.itemid);
            StartNextEventCountdown();

            arg.ReplyWith($"Starting {(arg.Args.IsNullOrEmpty() ? "random" : task)} xp event");
        }

        KeyValuePair<Source, XPSources> ConvertToSource(string task)
        {
            Source source;
            if (!SourceDef.TryGetValue(task, out source)) return GetRandomSource();

            return new KeyValuePair<Source, XPSources>(source, config.sources[source]);
        }

        #endregion       

        #region Helpers

        private Dictionary<ulong, Timer> timers = new Dictionary<ulong, Timer>();

        private void CreateGameTip(string text, BasePlayer player)
        {
            if (!config.notificationSettings.hintNotificationSettings.sendHint) return;
            if (player == null)
                return;
            Timer _timer;
            if (timers.TryGetValue(player.userID, out _timer))
            {
                _timer.Destroy();
            }
            if (!string.IsNullOrEmpty(config.notificationSettings.hintNotificationSettings.hintCol)) text = $"<color=#{config.notificationSettings.hintNotificationSettings.hintCol}>" + text + "</color>";
            player.SendConsoleCommand("gametip.hidegametip");
            player.SendConsoleCommand("gametip.showgametip", text);
            timers[player.userID] = timer.Once(config.notificationSettings.hintNotificationSettings.hintLength, () => player?.SendConsoleCommand("gametip.hidegametip"));
        }

        void SendNotify(BasePlayer player, string message, int type)
        {
            if (!config.notificationSettings.uiNotifySettings.enabled || UINotify == null || !UINotify.IsLoaded || string.IsNullOrEmpty(message)) return;
            UINotify.Call("SendNotify", player.userID.Get(), type, message);
        }

        void SendChatMessage(BasePlayer player, string message)
        {
            if (!config.notificationSettings.chatNotifySettings.enabled) return;
            if (!string.IsNullOrEmpty(config.notificationSettings.chatNotifySettings.hexColour)) message = $"<color=#{config.notificationSettings.chatNotifySettings.hexColour}>" + message + "</color>";
            PrintToChat(player, message);
        }

        #endregion

        #region Discord

        public void SendDiscordMsg(string message)
        {
            if (string.IsNullOrEmpty(config.discordSettings.webhook) || !config.discordSettings.webhook.Contains("discord.com/api/webhooks/")) return;
            try
            {
                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("Content-Type", "application/json");
                string payload = "{\"content\": \"" + message + "\"}";
                webrequest.Enqueue(config.discordSettings.webhook, payload, (code, response) =>
                {
                    if (code != 200 && code != 204)
                    {
                        if (response == null)
                        {
                            PrintWarning($"Discord didn't respond. Error Code: {code}. Try removing escape characters from your string such as \\n.");
                        }
                    }
                }, this, Core.Libraries.RequestMethod.POST, headers);
            }
            catch (Exception e)
            {
                Puts($"Failed. Error: {e?.Message}.\nIf this error was related to Bad Request, you may need to remove any escapes from your lang such as \\n.");
            }

        }

        #endregion
    }
}
