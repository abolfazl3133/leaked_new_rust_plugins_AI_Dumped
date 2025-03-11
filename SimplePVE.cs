/*
*  <----- End-User License Agreement ----->
*  Copyright © 2024 Iftebinjan
*  Devoloper: Iftebinjan (Contact: https://discord.gg/HFaGs8YwsH)
*  
*  You may not copy, modify, merge, publish, distribute, sublicense, or sell copies of This Software without the Developer’s consent
*  
*  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO,
*  THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS
*  BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE
*  GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
*  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using System.Collections.Generic;
using System.Linq;
using Network;
using System;
using Oxide.Core.Libraries.Covalence;
using System.Globalization;
using System.IO;
using Oxide.Core.Configuration;
using Steamworks.ServerList;
using Rust;
using ProtoBuf;
using ConVar;
using Oxide.Core.Libraries;
using CompanionServer.Handlers;
using System.Runtime;
using Facepunch;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Collections;
using Oxide.Game.Rust.Libraries;
using Facepunch.Math;
using Oxide.Plugins.SimplePVEExtensionMethods;
using Rust.Ai.Gen2;

namespace Oxide.Plugins
{
    [Info("SimplePVE", "Ifte", "1.2.6")]
    [Description("An easy simple PVE plugin for your server to modify or change PVE rules.")]
    public class SimplePVE : RustPlugin
    {
        /*------------------------------------
         *
         *          SimplePVE by Ifte
         *     Support: https://discord.gg/cCWadjcapr
         *     Fiverr: https://www.fiverr.com/s/e2pkYY
         *           Eror404 not found
         *
         ------------------------------------*/

        /*------------------------------------
        * 
        *   UPDATES
        * --------------
        * 1.2.0 - Latest with NewUI, Added new schedule system with weekdays support. Added config for Commands to execute when pvp purge starts and ends(Only from schedules)
        * 1.2.1 - Fixed ZombieHorde plugin, Fixed Tugboat vehicle damage, Added support for Backpack(Backpack no longer be pickup or open by other players)
        * 1.2.2 - Added new configuration, all rules configurations moved to Oxide/Data/SimplePVE/, Config values changed, Fixed DropBox & Mail not opening(More entitys option in the config)
        * 1.2.3 - Updated for latest force wipe, Added New Status UI for PVE status Zone Status Schedule Stats, New Configuration added, Fixed a bug where backpack cant be pickup after death by entity, Fixed few Zone Bugs, More bug added.
        * 1.2.4 - Updated for New Forced Update, Added support for FClan, Added support for codelock raid, Fixed Shark not damageing players
        * 1.2.5 - Fixed Status UI, Fixed APIs Not Calling, Working APIs(IsServerPVE, GetPVPStartTimeRemaining, GetPVPEndTimeRemaining, AddToExcludeZone, RemoveFromExcludeZone, IsStatusEnabled_API), 
        * 1.2.6 - Update for recent force wipe, Fixed for new wolf ai and wolf damage
        * 
        *   BUGS
        * ---------
        * Updates coming in
        * 
        ------------------------------------*/

        #region Varriables

        [PluginReference]
        Plugin ZoneManager, ImageLibrary, Clans, Friends, RaidableBases, Convoy, HeliSignals, AbandonedBases, FClan;

        private static SimplePVE ins;
        private Configuration config;
        private ScheduleSave savedSch;
        private bool update = true;
        private bool PVPRun = false;
        private bool ChangedConfig = false;
        private bool DiscordMessageSent = false;
        private bool UsingStatusUI = false;
        private Dictionary<ulong, PVPDelay> pvp_delays = new Dictionary<ulong, PVPDelay>();
        enum PVERules
        {
            GRules,
            PRules,
            NRules,
            ERules,
            LRules,
            ZRules
        }

        private class PVPDelay
        {
            public string ZoneID;
            public Timer DelayTimer;
        }
        //permissions
        private const string PvEAdminPerms = "simplepve.admin";
        private const string PvEAdminLootPerms = "simplepve.adminloot";
        private const string PvEAdminDamagePerms = "simplepve.admindamage";

        #endregion

        #region Configs 
        private class Configuration
        {
            [JsonProperty(PropertyName = "PVE Enabled")]
            public bool pveEnabledConfig { get; set; }

            public class PVPDelayNotify
            {
                [JsonProperty("PVP delay time in seconds(If enabled from ZoneRules)")]
                public int pvpdelaytime { get; set; }

                [JsonProperty("Show PVP Delay Message")]
                public bool pvpDelayMsgEnabled { get; set; }

                [JsonProperty("PVP Delay Message")]
                public string pvpDelayMessage { get; set; }
            }

            [JsonProperty(PropertyName = "Notifications")]
            public DisplayNotify displayNotify { get; set; }

            public class DisplayNotify
            {
                [JsonProperty("Prefix")]
                public string prefix { get; set; }

                [JsonProperty("Chat Avatar")]
                public ulong chatAvatar { get; set; }

                [JsonProperty("Show PVE Icon Overlay")]
                public bool showPvEOverlay { get; set; }

                [JsonProperty("Show PVE Warning Messages")]
                public bool showDamageM { get; set; }

                [JsonProperty("PVE Warning Type(Chat/UI/Both)")]
                public string showDamageMType { get; set; }

                [JsonProperty("PVE Warning Chat Message")]
                public string showDamageMTypeMessage { get; set; }

                [JsonProperty("PVE Warning UI Settings")]
                public WarningUI warningUI { get; set; }

                public class WarningUI
                {
                    [JsonProperty("Anchor Min")]
                    public string WarningAMin { get; set; }

                    [JsonProperty("Anchor Max")]
                    public string WarningAMax { get; set; }

                    [JsonProperty("Warning Image URL")]
                    public string WarningImgUrl { get; set; }
                }

                [JsonProperty("Show Loot Protection Messages")]
                public bool LPMessagesOn { get; set; }

                [JsonProperty("Loot Protection Type(Chat/UI/Both)")]
                public string LPType { get; set; }

                [JsonProperty("Loot Protection Chat Message")]
                public string LPChatMessage { get; set; }

                [JsonProperty("Loot Protection UI Settings")]
                public LPUISetting lPUISetting { get; set; }

                public class LPUISetting
                {
                    [JsonProperty("Anchor Min")]
                    public string WarningAMin { get; set; }

                    [JsonProperty("Anchor Max")]
                    public string WarningAMax { get; set; }

                    [JsonProperty("Warning Loot UI Image URL")]
                    public string WarningImgUrl { get; set; }
                }


                [JsonProperty("PVE/PVP Icon UI Settings")]
                public PEPUISetting pUISetting { get; set; }

                public class PEPUISetting
                {
                    [JsonProperty("Anchor Min")]
                    public string pepAMin { get; set; }

                    [JsonProperty("Anchor Max")]
                    public string pepAMax { get; set; }

                    [JsonProperty("Offset Min")]
                    public string pepOMin { get; set; }

                    [JsonProperty("Offset Max")]
                    public string pepOMax { get; set; }

                }

            }

            [JsonProperty(PropertyName = "Loot Protection Excluded Entitys", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> excludedEntitys { get; set; }

            [JsonProperty(PropertyName = "Schedules Setting")]
            public Sche sche { get; set; }
            public class Sche
            {
                [JsonProperty("UTC Time Difference")]
                public int utcTimeDif { get; set; }

                [JsonProperty("Enable Weekly Schedule Only")]
                public bool enableweeklysch { get; set; }

                [JsonProperty("PVP On Notify Message")]
                public string pvpOnMessage { get; set; }

                [JsonProperty("PVE On Notify Message")]
                public string pveOnMessage { get; set; }

                [JsonProperty("PVP Purge Start Before Message")]
                public string pvppurgeStartMsg { get; set; }

                [JsonProperty("PVP Purge Ending Before Message")]
                public string pvppurgeEndingMsg { get; set; }

            }

            [JsonProperty(PropertyName = "PVP Delay Setting")]
            public PVPDelayNotify delaySetting { get; set; }


            [JsonProperty(PropertyName = "Discord Setting")]
            public DiscordSetting discordSetting { get; set; }

            public class DiscordSetting
            {
                [JsonProperty("Enable Discord Notification")]
                public bool enableDiscordNotify { get; set; }
                [JsonProperty("Discord Webhook URL")]
                public string discordWebhookURL { get; set; }
                [JsonProperty("Enable Message Before PVP Time Start")]
                public bool messageBeforeStart { get; set; }
                [JsonProperty("Before PVP Time Start Minutes")]
                public int messageBeforeStartMinutes { get; set; }
                [JsonProperty("PVP Message Embed Color(Hexa)")]
                public string pvpmessageEmbed { get; set; }
                [JsonProperty("Before PVP Time Start Message Content")]
                public string[] pvpTimeMessage { get; set; }

                [JsonProperty("Enable Message Before PVE Time Start")]
                public bool messageBeforeStartPVE { get; set; }
                [JsonProperty("Before PVE Time Start Minutes")]
                public int messageBeforeStartMinutesPVE { get; set; }
                [JsonProperty("PVE Message Embed Color")]
                public string pvemessageEmbed { get; set; }
                [JsonProperty("Before PVE Time Start Message Content")]
                public string[] pveTimeMessage { get; set; }

            }
            [JsonProperty(PropertyName = "Commands when PVP Purge Starts")]
            public List<string> commandsExefrompvpstart { get; set; }

            [JsonProperty(PropertyName = "Commands when PVP Purge Ends")]
            public List<string> commandsExefrompvpends { get; set; }

            [JsonProperty(PropertyName = "Exclude Zone IDs From Rules")]
            public List<string> excludedZoneIDs { get; set; }


            [JsonProperty(PropertyName = "Status UI")]
            public StatusUI statusUI { get; set; }

            public class StatusUI
            {
                [JsonProperty(PropertyName = "Enable Status")]
                public bool status { get; set; }

                [JsonProperty(PropertyName = "Interval Time In Seconds")]
                public int interval { get; set; }

                [JsonProperty(PropertyName = "PVE/PVP Status")]
                public PVPorPVEStatus peStatus { get; set; }

                [JsonProperty(PropertyName = "Zone Status")]
                public ZoneStatus zoneStatus { get; set; }

                [JsonProperty(PropertyName = "Purge Schedule Status")]
                public PurgeStatus purgeStatus { get; set; }
            }

            public class PVPorPVEStatus
            {
                [JsonProperty(PropertyName = "Status Enable")]
                public bool status { get; set; }

                [JsonProperty(PropertyName = "PVE Status")]
                public Setting PVEStatus { get; set; }

                [JsonProperty(PropertyName = "PVP Status")]
                public Setting PVPStatus { get; set; }
            }

            public class ZoneStatus
            {
                [JsonProperty(PropertyName = "Status Enable")]
                public bool status { get; set; }

                [JsonProperty(PropertyName = "In PVP Area Status")]
                public Setting pvparea { get; set; }
            }

            public class PurgeStatus
            {
                [JsonProperty(PropertyName = "Status Enable")]
                public bool status { get; set; }

                [JsonProperty(PropertyName = "Purge PVP Status")]
                public Setting purgepvpStatus { get; set; }

                [JsonProperty(PropertyName = "Purge PVE Status")]
                public Setting purgepveStatus { get; set; }
            }


            public class Setting
            {
                [JsonProperty(PropertyName = "Enable Status")]
                public bool status { get; set; }

                [JsonProperty(PropertyName = "Text List")]
                public string[] strings { get; set; }

                [JsonProperty(PropertyName = "Text Color")]
                public string textColor { get; set; }

                [JsonProperty(PropertyName = "Icon Image Url")]
                public string icon { get; set; }

                [JsonProperty(PropertyName = "Icon Image Color")]
                public string iconColor { get; set; }

                [JsonProperty(PropertyName = "Status Color")]
                public string statusColor { get; set; }
            }

            [JsonProperty(PropertyName = "Config Version (Don't Edit This)")]
            public string version { get; set; }

            public static Configuration CreateConfig()
            {
                return new Configuration
                {
                    pveEnabledConfig = true,
                    displayNotify = new DisplayNotify
                    {
                        showDamageM = true,
                        prefix = "<color=#00ffff>[SimplePVE]</color>: ",
                        chatAvatar = 0,
                        showDamageMType = "Both",
                        showDamageMTypeMessage = "PVE enabled on this server, blocking damage.",
                        warningUI = new DisplayNotify.WarningUI
                        {
                            WarningAMin = "0.786 0.722",
                            WarningAMax = "0.99 0.815",
                            WarningImgUrl = "https://i.postimg.cc/0jZNDr9x/Add-a-subheading-2.png"
                        },
                        showPvEOverlay = true,
                        LPMessagesOn = true,
                        LPType = "Both",
                        LPChatMessage = "This entity is protected!",
                        lPUISetting = new DisplayNotify.LPUISetting
                        {
                            WarningAMin = "0.786 0.722",
                            WarningAMax = "0.99 0.815",
                            WarningImgUrl = "https://i.postimg.cc/SxSsS67s/Add-a-subheading-1.png"
                        },
                        pUISetting = new DisplayNotify.PEPUISetting
                        {
                            pepAMin = "0.5 0",
                            pepAMax = "0.5 0",
                            pepOMin = "190 30",
                            pepOMax = "250 60"
                        }
                    },
                    statusUI = new StatusUI()
                    {
                        status = true,
                        interval = 1,
                        peStatus = new PVPorPVEStatus()
                        {
                            status = true,
                            PVEStatus = new Setting()
                            {
                                status = true,
                                strings = new string[] 
                                { 
                                    "PVE ENABLED", 
                                    "RAID IS DISABLED",
                                    "PVP IS DISABLED",
                                },
                                textColor = "#FFFFFF",
                                icon = "https://i.postimg.cc/4ymwFnSC/quality.png",
                                iconColor = "#A4FE0A",
                                statusColor = "#718D48"
                            },
                            PVPStatus = new Setting()
                            {
                                status = true,
                                strings = new string[]
                                {
                                    "PVP ENABLED",
                                    "RAID IS ENABLED",
                                    "PVE IS DISABLED",
                                    "PVP IS ENABLED",
                                },
                                textColor = "#FFFFFF",
                                icon = "https://i.postimg.cc/50YVChxW/human-skull-with-crossed-bones-silhouette.png",
                                iconColor = "#FFFFFF",
                                statusColor = "#B22222"
                            }
                        },
                        zoneStatus = new ZoneStatus()
                        {
                            status = true,
                            pvparea = new Setting()
                            {
                                status = true,
                                strings = new string[]
                                {
                                    "YOU'RE IN THE PVP ZONE"
                                },
                                textColor = "#FFFFFF",
                                icon = "https://i.postimg.cc/qRGxrMBR/fighting-game.png",
                                iconColor = "#FFFFFF",
                                statusColor = "#A30000"
                            }
                        },
                        purgeStatus = new PurgeStatus()
                        {
                            status = true,
                            purgepvpStatus = new Setting()
                            {
                                status = true,
                                strings = new string[]
                                {
                                    "PVP ENABLEING IN {TIMER}",
                                    "RAID ENABLEING IN {TIMER}",
                                    "PVP PURGING IN {TIMER}"
                                },
                                textColor = "#FFFFFF",
                                icon = "https://i.postimg.cc/0j2n3rzp/time.png",
                                iconColor = "#FFFFFF",
                                statusColor = "#A30000"
                            },
                            purgepveStatus = new Setting()
                            {
                                status = true,
                                strings = new string[]
                                {
                                    "PVE ENABLING IN {TIMER}",
                                    "RAID DISABLEING IN {TIMER}",
                                    "PVE PURGING IN {TIMER}"
                                },
                                textColor = "#FFFFFF",
                                icon = "https://i.postimg.cc/rydRF7cc/time.png",
                                iconColor = "#FFFFFF",
                                statusColor = "#718D48"
                            }
                        }
                    },
                    excludedEntitys = new List<string>() 
                    {
                        "mailbox.deployed",
                        "dropbox.deployed"
                    },
                    sche = new Sche
                    {
                        utcTimeDif = 360,
                        enableweeklysch = true,
                        pvpOnMessage = "<#990000>PVP enabled on the server. Now you can raid others bases and fight!</color>",
                        pveOnMessage = "<#008000>PVE enabled. Raid and fight is now prohibited!</color>",
                        pvppurgeStartMsg = "PVP time is approaching! {TIMELEFT} minutes left.",
                        pvppurgeEndingMsg = "PVP time is ending! {TIMELEFT} minutes left.",

                    },
                    delaySetting = new PVPDelayNotify
                    {
                        pvpdelaytime = 10,
                        pvpDelayMsgEnabled = true,
                        pvpDelayMessage = "You will be removed from PVP state in <color=#DC143C>{delay}</color> seconds.",
                    },
                    discordSetting = new DiscordSetting
                    {
                        enableDiscordNotify = false,
                        discordWebhookURL = string.Empty,
                        messageBeforeStart = false,
                        messageBeforeStartMinutes = 30,
                        pvpmessageEmbed = "#990000",
                        pvpTimeMessage = new string[]
                        {
                            "🔔 **Attention Rust Survivors!**",
                            "",
                            "PVP purge approaching!",
                            "In {Minutes} Minutes🕒",
                            "",
                            "Prepare yourselves for intense battles! ⚔️",
                        },
                        messageBeforeStartPVE = false,
                        messageBeforeStartMinutesPVE = 30,
                        pvemessageEmbed = "#32CD32",
                        pveTimeMessage = new string[]
                        {
                            "🔔 **Attention Rust Survivors!**",
                            "",
                            "PVP purge ending soon!",
                            "In {Minutes} Minutes🕒",
                            "",
                            "Make your final moves wisely!",
                            "Collect your victories and gear up for the next day!"
                        }
                    },
                    commandsExefrompvpstart = new List<string>{ },
                    commandsExefrompvpends = new List<string> { },
                    excludedZoneIDs = new List<string>
                    {
                        "999",
                        "6969"
                    },
                    version = "1.2.5",

                };
            }


        }


        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();

            string currentVersion = "1.2.6"; // Update this to your current plugin version

            if (config.version != currentVersion)
            {
                // Backup old configuration
                Puts("Config version updated. Backing up old config.");
                BackupOldConfig();

                // Load default config for the new version
                //config = Configuration.CreateConfig();

                config.version = currentVersion;
                SaveConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            config = Configuration.CreateConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config, true);
        }

        private void BackupOldConfig()
        {
            string backupFileName = $"{Name}_config_backup_ver{config.version}_{Time:yyyy-MM-dd_HH-mm-ss}.json";
            string backupFilePath = Path.Combine(Interface.Oxide.ConfigDirectory, backupFileName);
            Config.WriteObject(config, false, backupFilePath);
        }

        #endregion

        #region Data files

        private RulesData rulesData;

        private void LoadRulesData()
        {
            rulesData = Interface.Oxide.DataFileSystem.ReadObject<RulesData>($"{Name}/RulesData");

            // If rulesData is null or empty, populate it with default rules
            if (rulesData == null || rulesData.IsRulesDataEmpty(rulesData))
            {
                rulesData = RulesData.CreateDefaultRules();
                SaveRulesData();
            }
        }

        private void SaveRulesData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/RulesData", rulesData);
        }

        private class RulesData
        {
            [JsonProperty(PropertyName = "General Rules")]
            public GRules gRules { get; set; }

            public class GRules
            {
                [JsonProperty("Allow Friendly Fire")]
                public bool aFriendlyFire { get; set; }

                [JsonProperty("Allow Suicide")]
                public bool allowSuicide { get; set; }

                [JsonProperty("Kill Sleepers")]
                public bool killSleepers { get; set; }

                [JsonProperty("Player Radiation Damage")]
                public bool pRadiation { get; set; }

                [JsonProperty("Allow Codelock Raid")]
                public bool aCodelockRaid { get; set; }
            }

            [JsonProperty(PropertyName = "Player Rules")]
            public PlayerRules playerRules { get; set; }

            public class PlayerRules
            {
                [JsonProperty("Player Can Hurt Players")]
                public bool pCanHurtp { get; set; }

                [JsonProperty("Player Can Hurt Animals")]
                public bool pCanHurta { get; set; }

                [JsonProperty("Player Can Hurt NPC")]
                public bool pCanHurtn { get; set; }

                [JsonProperty("Player Can Damage(Helicopters, Vehicles)")]
                public bool pCanDamEntitys { get; set; }

                [JsonProperty("Player Can Damage Other Player Buildings")]
                public bool pCanDamOtherpBuildings { get; set; }

                [JsonProperty("Player Can Damage Own Buildings")]
                public bool pCanDamageOwnBuildings { get; set; }

                [JsonProperty("Player Can Damage Own Teammates Building")]
                public bool pCanDamOwnTeamamtesBuilding { get; set; }

                [JsonProperty("Player Can Damage Other Player Twigs")]
                public bool PallowTwigDamage { get; set; }

            }

            [JsonProperty(PropertyName = "Npc Rules")]
            public NpcRules npcRules { get; set; }
            public class NpcRules
            {
                [JsonProperty("Npc Can Hurt Players")]
                public bool NpcHurtP { get; set; }

                [JsonProperty("Animal Can Hurt Players")]
                public bool AnimalCanHurtP { get; set; }

                [JsonProperty("Patrol Heli Can Hurt Player")]
                public bool PatrolHurtP { get; set; }

                [JsonProperty("Patrol Heli Can Hurt Buildings")]
                public bool PatrolHurtB { get; set; }

                [JsonProperty("Player SamSite Ignore Players")]
                public bool PSamigP { get; set; }

                [JsonProperty("Npc SamSite Ignore Players")]
                public bool NSamigP { get; set; }

                [JsonProperty("Bradley Can Hurt Players")]
                public bool bradleyHurtP { get; set; }

            }

            [JsonProperty(PropertyName = "Entity Rules")]
            public EntityRules entityRules { get; set; }

            public class EntityRules
            {

                [JsonProperty("Walls Entitys Can Hurt Players(Example: High External Wall)")]
                public bool WallHurtP { get; set; }

                [JsonProperty("Barricade Entitys Can Hurt Players(Example: Wooden Barricade)")]
                public bool BarrHurtP { get; set; }

                [JsonProperty("MLRS Rocket Can Damage Players or Buildings")]
                public bool MlrsHurtPB { get; set; }

                [JsonProperty("Vehicle Entitys Can Hurt Players(Example: Cars, Mini, ScrapHeli)")]
                public bool VehicleHurtP { get; set; }

                [JsonProperty("Disable Traps(BearTrap, LandMine, GunTrap, FlameTurret, AutoTurret)")]
                public bool trapD { get; set; }

                [JsonProperty("Enable Vehicle Collsion Damage")]
                public bool enableVehicleCollsionD { get; set; }

            }

            [JsonProperty(PropertyName = "Loot Rules")]
            public LootRules lootRules { get; set; }

            public class LootRules
            {
                [JsonProperty("Use Loot Protection")]
                public bool useLP { get; set; }

                [JsonProperty("Admin Can Loot All")]
                public bool adminCanLootAll { get; set; }

                [JsonProperty("Teams Can Access Loot")]
                public bool teamsAccessLoot { get; set; }

                [JsonProperty("Player can loot other player in PVP Zones")]
                public bool pclpvpzones { get; set; }

                [JsonProperty("Anyone can open Player called SupplyDrops")]
                public bool anyoneOpenSupplyDrop { get; set; }

            }

            [JsonProperty(PropertyName = "Zone Rules")]
            public ZoneRules zoneRules { get; set; }

            public class ZoneRules
            {
                [JsonProperty("Use ZoneManager")]
                public bool zoneManager { get; set; }

                [JsonProperty("Disable PVE Rules In Zones")]
                public bool disableRulesZone { get; set; }

                [JsonProperty("Enable PVP Delay from ZoneManager")]
                public bool enablePVPDelay { get; set; }
            }

            public static RulesData CreateDefaultRules()
            {
                return new RulesData
                {
                    zoneRules = new ZoneRules
                    {
                        zoneManager = true,
                        disableRulesZone = true,
                        enablePVPDelay = true,
                    },
                    gRules = new GRules
                    {
                        aFriendlyFire = true,
                        allowSuicide = true,
                        killSleepers = false,
                        pRadiation = true,
                        aCodelockRaid = false,
                    },
                    playerRules = new PlayerRules
                    {
                        pCanHurtp = false,
                        pCanHurta = true,
                        pCanHurtn = true,
                        pCanDamEntitys = true,
                        pCanDamOtherpBuildings = false,
                        pCanDamageOwnBuildings = true,
                        pCanDamOwnTeamamtesBuilding = true,
                        PallowTwigDamage = true,
                    },
                    npcRules = new NpcRules
                    {
                        NpcHurtP = true,
                        AnimalCanHurtP = true,
                        PatrolHurtP = true,
                        PatrolHurtB = true,
                        PSamigP = true,
                        NSamigP = true,
                        bradleyHurtP = true

                    },
                    entityRules = new EntityRules
                    {
                        WallHurtP = true,
                        BarrHurtP = true,
                        MlrsHurtPB = false,
                        VehicleHurtP = false,
                        trapD = true,
                        enableVehicleCollsionD = true,
                    },
                    lootRules = new LootRules
                    {
                        useLP = true,
                        adminCanLootAll = true,
                        teamsAccessLoot = true,
                        pclpvpzones = false,
                        anyoneOpenSupplyDrop = false
                    },
                };
            }

            public bool IsRulesDataEmpty(RulesData data)
            {
                return data.gRules == null &&
                       data.playerRules == null &&
                       data.npcRules == null &&
                       data.entityRules == null &&
                       data.lootRules == null &&
                       data.zoneRules == null;
            }
        }


        #endregion

        #region Commands Pack

        [Command("simplepve")]
        private void CommandEnable(IPlayer p, string cmd, string[] args)
        {
            if (!p.HasPermission(PvEAdminPerms))
            {
                p.Message("You don't have permission to use this command.");
                return;
            }

            if (args.Length == 0)
            {
                p.Message("Usage: /simplepve <on|off>");
                return;
            }

            bool enablePVE = false;

            switch (args[0].ToLower())
            {
                case "on":
                    enablePVE = true;
                    break;
                case "off":
                    enablePVE = false;
                    break;
                default:
                    p.Message("Invalid argument. Usage: /simplepve <on|off>");
                    return;
            }

            if (config.pveEnabledConfig == enablePVE)
            {
                p.Message($"SimplePVE is already {(enablePVE ? "enabled" : "disabled")}.");
                return;
            }

            config.pveEnabledConfig = enablePVE;
            SaveConfig();

            string message = config.pveEnabledConfig ? "SimplePVE enabled" : "SimplePVE disabled";
            p.Message(message);

            if (config.pveEnabledConfig)
            {
                Subscribe();
            }
            else
            {
                NotSubscribe();
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                RemovePVPPanel(player);
                RemovePVEPanel(player);

                if (config.pveEnabledConfig)
                {
                    ShowPVEUI(player);
                }
                else
                {
                    ShowPVPUi(player);
                }
            }
        }

        [Command("rsp")]
        private void ReloadPlugin(IPlayer player, string cmd, string[] args)
        {
            if (!player.HasPermission(PvEAdminPerms))
            {
                player.Message("You don't have permission to use this command.");
                return;
            }
            player.Command("oxide.reload SimplePVE");

            player.Message("SimplePVE reloaded.");
        }

        [ChatCommand("sprules")]
        private void SimplePVEGui(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PvEAdminPerms))
            {
                player.ChatMessage("You don't have permission to use this command.");
                return;
            }
            SimplePVEMainGui(player);
            PVERulesUI(player, PVERules.GRules);
        }

        [ChatCommand("notest")]
        private void SimplePVEGuiClose(BasePlayer player, string cmd, string[] args)
        {
            //StatusUI(player);
        }

        [ConsoleCommand("spvecui")]
        private void SPVECUICMD(ConsoleSystem.Arg args)
        {
            var player = args?.Player();

            string command = args.Args[0];

            switch (command)
            {
                case "pverules":
                    PVERulesUI(player, PVERules.GRules);
                    break;
                case "pveschedules":
                    var time = DateTime.Now;
                    PVESchedules(player);
                    break;
                case "grules":
                    PVERulesUI(player, PVERules.GRules);
                    break;
                case "prules":
                    PVERulesUI(player, PVERules.PRules);
                    break;
                case "nrules":
                    PVERulesUI(player, PVERules.NRules);
                    break;
                case "erules":
                    PVERulesUI(player, PVERules.ERules);
                    break;
                case "lrules":
                    PVERulesUI(player, PVERules.LRules);
                    break;
                case "zrules":
                    PVERulesUI(player, PVERules.ZRules);
                    break;
                case "closeall":
                    CuiHelper.DestroyUi(player, "SimplePVECUI");
                    /*if (ChangedConfig == true)
                    {
                        Server.Command($"o.reload {Name}");
                        ChangedConfig = false;
                    }      */              
                    break;

            }
        }
        [ConsoleCommand("grulesSetting")]
        private void GrulesCcmd(ConsoleSystem.Arg args)
        {
            var player = args?.Player();
            string propertyName = args.Args[0];
            SetProperty(propertyName);
            PVERulesUI(player, PVERules.GRules);

        }
        [ConsoleCommand("prulesSetting")]
        private void PrulesCcmd(ConsoleSystem.Arg args)
        {
            var player = args?.Player();
            string propertyName = args.Args[0];
            SetProperty(propertyName);
            PVERulesUI(player, PVERules.PRules);
        }
        [ConsoleCommand("nrulesSetting")]
        private void NrulesCcmd(ConsoleSystem.Arg args)
        {
            var player = args?.Player();
            string propertyName = args.Args[0];
            SetProperty(propertyName);
            PVERulesUI(player, PVERules.NRules);
        }
        [ConsoleCommand("erulesSetting")]
        private void ErulesCcmd(ConsoleSystem.Arg args)
        {
            var player = args?.Player();
            string propertyName = args.Args[0];
            SetProperty(propertyName);
            PVERulesUI(player, PVERules.ERules);
        }
        [ConsoleCommand("lrulesSetting")]
        private void LrulesCcmd(ConsoleSystem.Arg args)
        {
            var player = args?.Player();
            string propertyName = args.Args[0];
            SetProperty(propertyName);
            PVERulesUI(player, PVERules.LRules);
        }
        [ConsoleCommand("zrulesSetting")]
        private void ZrulesCcmd(ConsoleSystem.Arg args)
        {
            var player = args?.Player();
            string propertyName = args.Args[0];
            SetProperty(propertyName);  
            PVERulesUI(player, PVERules.ZRules);
        }
        private void SetProperty(string propertyName)
        {
            if (config == null) return;
            PropertyInfo gRulesProperty = typeof(RulesData.GRules).GetProperty(propertyName);
            PropertyInfo pRulesProperty = typeof(RulesData.PlayerRules).GetProperty(propertyName);
            PropertyInfo nRulesProperty = typeof(RulesData.NpcRules).GetProperty(propertyName);
            PropertyInfo eRulesProperty = typeof(RulesData.EntityRules).GetProperty(propertyName);
            PropertyInfo lRulesProperty = typeof(RulesData.LootRules).GetProperty(propertyName);
            PropertyInfo zRulesProperty = typeof(RulesData.ZoneRules).GetProperty(propertyName);

            ChangedConfig = true;

            if (gRulesProperty != null)
            {
                bool currentValue = (bool)gRulesProperty.GetValue(rulesData.gRules);
                bool newValue = !currentValue;
                gRulesProperty.SetValue(rulesData.gRules, newValue);
            }
            else if (pRulesProperty != null)
            {
                bool currentValue = (bool)pRulesProperty.GetValue(rulesData.playerRules);
                bool newValue = !currentValue;
                pRulesProperty.SetValue(rulesData.playerRules, newValue);
            }
            else if (nRulesProperty != null)
            {
                bool currentValue = (bool)nRulesProperty.GetValue(rulesData.npcRules);
                bool newValue = !currentValue;
                nRulesProperty.SetValue(rulesData.npcRules, newValue);
            }
            else if (eRulesProperty != null)
            {
                bool currentValue = (bool)eRulesProperty.GetValue(rulesData.entityRules);
                bool newValue = !currentValue;
                eRulesProperty.SetValue(rulesData.entityRules, newValue);
            }
            else if (lRulesProperty != null)
            {
                bool currentValue = (bool)lRulesProperty.GetValue(rulesData.lootRules);
                bool newValue = !currentValue;
                lRulesProperty.SetValue(rulesData.lootRules, newValue);
            }
            else if (zRulesProperty != null)
            {
                bool currentValue = (bool)zRulesProperty.GetValue(rulesData.zoneRules);
                bool newValue = !currentValue;
                zRulesProperty.SetValue(rulesData.zoneRules, newValue);
            }          
        }

        #endregion

        #region Plugin Loads

        void Init()
        {            
            Puts("Cleaning up...");
            Puts("Initializing rules.....");
            //permission
            permission.RegisterPermission(PvEAdminPerms, this);
            permission.RegisterPermission(PvEAdminLootPerms, this);
            permission.RegisterPermission(PvEAdminDamagePerms, this);
            //Commands
            AddCovalenceCommand("simplepve", "CommandEnable");
            AddCovalenceCommand("rsp", "ReloadPlugin");

            NotSubscribe();
        }

        private void OnServerInitialized()
        {
            ins = this;

            if (config.pveEnabledConfig)
            {
                Subscribe();
            }
            else { NotSubscribe(); }

            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }

            //Images load from ImageLibrary
            LoadImages();
            UsingStatusUI = config.statusUI.status;

            timer.In(1f, OnServerInitializedLate);
        }
        private void OnServerInitializedLate()
        {
            //Start Schedules
            ServerMgr.Instance.StartCoroutine(Schedules());
        }
        private void Loaded()
        {
            LoadSchTime();
            LoadRulesData();
        }

        private void OnServerSave()
        {
            SaveRulesData();
            SaveSchTime();
        }

        private void Unload()
        {
            SaveRulesData();
            SaveSchTime();

            DestroyStatsUIServer();

            NotSubscribe();
            foreach (var player in BasePlayer.activePlayerList)
            {
                RemovePVEPanel(player);
                RemovePVPPanel(player);
                RemoveUIWarning(player);
                RemoveWarningLoot(player);
                CuiHelper.DestroyUi(player, "SimplePVECUI");

                OnPlayerDisconnected(player);
            }
            update = false;
            ChangedConfig = false;
            DiscordMessageSent = false;
        }

        private void Subscribe()
        {
            Subscribe(nameof(OnEntitySpawned));
            Subscribe(nameof(OnEntityTakeDamage));
            if (rulesData.lootRules.useLP)
            {
                Subscribe(nameof(CanLootEntity));
                Subscribe(nameof(CanLootPlayer));
                Subscribe(nameof(CanAdministerVending));
                Subscribe(nameof(OnOvenToggle));
                Subscribe(nameof(CanBuild));
                Subscribe(nameof(OnRackedWeaponMount));
                Subscribe(nameof(OnRackedWeaponSwap));
                Subscribe(nameof(OnRackedWeaponTake));
                Subscribe(nameof(OnRackedWeaponUnload));
                Subscribe(nameof(OnRackedWeaponLoad));
                //new
                Subscribe(nameof(OnItemDropped));
                Subscribe(nameof(OnLootEntity));
            }
            if (rulesData.entityRules.trapD)
            {
                Subscribe(nameof(OnTrapTrigger));
                Subscribe(nameof(CanBeTargeted));
            }
            if (rulesData.npcRules.NSamigP || rulesData.npcRules.PSamigP)
            {
                Subscribe(nameof(OnSamSiteTarget));
            }

        }

        private void NotSubscribe()
        {
            Unsubscribe(nameof(OnEntityTakeDamage));
            Unsubscribe(nameof(CanLootEntity));
            Unsubscribe(nameof(CanLootPlayer));
            Unsubscribe(nameof(CanAdministerVending));
            Unsubscribe(nameof(OnOvenToggle));
            Unsubscribe(nameof(OnTrapTrigger));
            Unsubscribe(nameof(CanBeTargeted));
            Unsubscribe(nameof(OnSamSiteTarget));
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(CanBuild));
            //Racked
            Unsubscribe(nameof(OnRackedWeaponMount));
            Unsubscribe(nameof(OnRackedWeaponSwap));
            Unsubscribe(nameof(OnRackedWeaponTake));
            Unsubscribe(nameof(OnRackedWeaponUnload));
            Unsubscribe(nameof(OnRackedWeaponLoad));
            //new
            Unsubscribe(nameof(OnItemDropped));
            Unsubscribe(nameof(OnLootEntity));

        }
        #endregion

        #region PVE Rules

        private bool Debug = false;

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hit)
        {
            if (!config.pveEnabledConfig || entity == null || entity.IsDestroyed || entity.IsDead() || hit == null 
                || (hit.InitiatorPlayer != null && permission.UserHasPermission(hit.InitiatorPlayer.UserIDString, PvEAdminDamagePerms)))
            {
                return null;
            }
            
            if (Interface.CallHook("CanEntityTakeDamage", new object[] { entity, hit }) is bool val)
            {
                if (val)
                {
                    return null;
                }

                noDamage(hit);
                if (hit.InitiatorPlayer?.userID.IsSteamId() == true)
                {
                    ShowWarningAlert(hit.InitiatorPlayer);
                }
                return true;
            }

            if (Interface.CallHook("IsHeliSignalObject", entity.skinID) != null) return null;

            if (entity is PatrolHelicopter || entity.ShortPrefabName.Contains("patrolheli") || entity is BradleyAPC || entity.ShortPrefabName.Contains("bradley"))
            {
                //if (Debug) Puts($"Attacker: {hit.Initiator} & Victim: {entity.ShortPrefabName} & Rules No rules");
                return null;
            }

            if (!CheckIncludes(entity, hit))
            {
                noDamage(hit);
                if (hit.InitiatorPlayer?.userID.IsSteamId() == true)
                {
                    ShowWarningAlert(hit.InitiatorPlayer);
                }
                if (Debug) Puts($"Attacker: {hit.Initiator.ShortPrefabName} Himself & Damaged: {entity.ShortPrefabName}");
                return true;
            }

            //if (Debug) Puts($"Attacker: {hit.Initiator.ShortPrefabName} Himself & Damaged: {entity.ShortPrefabName}");

            return null;
        }     
      
        private bool CheckIncludes(BaseCombatEntity entity, HitInfo hit)
        {
            //Puts(inPVPBase.Count.ToString());
            var damageType = hit.damageTypes.GetMajorityDamageType();

            if (damageType == DamageType.Suicide)
            {
                if (entity is BasePlayer victim1 && victim1.userID.IsSteamId() && rulesData.gRules.allowSuicide)
                {
                    if (Debug) Puts($"Attacker: Himself & Damages From: Suicide & Rules {rulesData.gRules.allowSuicide}");
                    return true;
                }
                else
                {
                    if (entity is BasePlayer)
                        entity.SendMessage("Suicide is disabled on this server!");
                    return false;
                }
            }

            if (damageType == DamageType.Radiation || damageType == DamageType.RadiationExposure)
            {
                if (entity is BasePlayer && rulesData.gRules.pRadiation)
                {
                    if (Debug) Puts($"Attacker: {damageType} & Damages From: Rad & Rules {rulesData.gRules.pRadiation}");                    
                    return true;
                }
            }

            if (damageType == DamageType.Fall ||
                damageType == DamageType.Drowned ||
                damageType == DamageType.Decay ||
                damageType == DamageType.Cold ||
                damageType == DamageType.Hunger ||
                damageType == DamageType.Poison ||
                damageType == DamageType.Thirst ) //ew
            {
                return true;
            }

            if (rulesData.zoneRules.zoneManager && ZoneManager != null)
            {
                var weapon = hit.Initiator ?? hit.Weapon ?? hit.WeaponPrefab;
                Initiator(hit, weapon);
                List<string> entityLoc = GetAllZones(entity);
                List<string> iniLoc = GetAllZones(weapon);

                if (CheckExclusion(entityLoc, iniLoc))
                {
                    if (Debug) Puts($"Attacker: {weapon.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules {rulesData.zoneRules.disableRulesZone}");
                    return rulesData.zoneRules.disableRulesZone;
                }
            }

            if (entity is DecayEntity)
            {
                if (hit.Initiator is BasePlayer)
                {
                    BasePlayer player = hit.Initiator as BasePlayer;
                    if (player.userID.IsSteamId() && entity.ShortPrefabName.Contains("cupboard"))
                    {
                        if (entity.OwnerID == player.userID || IsAlliedWithPlayer(entity.OwnerID, player.userID))
                        {
                            if (Debug) Puts($"Attacker: {player.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules {rulesData.playerRules.pCanDamageOwnBuildings}");
                            return rulesData.playerRules.pCanDamageOwnBuildings || rulesData.playerRules.pCanDamOwnTeamamtesBuilding;
                        }
                        else if (entity.OwnerID == 0)
                        {
                            if (Debug) Puts($"Attacker: {player.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules Entity is server");
                            return true;
                        }
                        else
                        {
                            if (Debug) Puts($"Attacker: {player.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules {rulesData.playerRules.pCanDamOtherpBuildings}");
                            return rulesData.playerRules.pCanDamOtherpBuildings;
                        }
                    }
                }
            }

            if (entity is BasePlayer && hit.Initiator is BasePlayer)
            {
                BasePlayer attacker = hit.Initiator as BasePlayer;
                BasePlayer victim = entity as BasePlayer;


                //check friendly fire
                if (attacker.userID.IsSteamId() && victim.userID.IsSteamId() && IsAlliedWithPlayer(attacker.userID, victim.userID))
                {
                    if (Debug) Puts($"Attacker: {attacker.ShortPrefabName} & Victim: {victim.ShortPrefabName} & Rules {rulesData.gRules.aFriendlyFire}");
                    return rulesData.gRules.aFriendlyFire;

                } //check sleepers
                else if (attacker.userID.IsSteamId() && victim.userID.IsSteamId() && !IsAlliedWithPlayer(attacker.userID, victim.userID))
                {
                    if (victim.IsSleeping())
                    {
                        if (Debug) Puts($"Attacker: {attacker.ShortPrefabName} & Victim: {victim.ShortPrefabName} & Rules {rulesData.gRules.killSleepers}");
                        return rulesData.gRules.killSleepers;

                    }
                    /*else if (inPVPBase.Contains(victim.userID) && inPVPBase.Contains(attacker.userID)) //fix raidable bases pvp base
                    {
                        return true;
                    }*/
                    else if (rulesData.zoneRules.enablePVPDelay && CheckPVPDelay(victim, attacker)) //pvp delay from zones
                    {
                        if (Debug) Puts($"Attacker: {attacker.ShortPrefabName} & Victim: {victim.ShortPrefabName} & Rules In PVP ZoneManager");
                        return true;
                    }
                    //player can hurt players
                    else if (rulesData.playerRules.pCanHurtp == true && victim.IsConnected)
                    {
                        if (Debug) Puts($"Attacker: {attacker.ShortPrefabName} & Victim: {victim.ShortPrefabName} & Rules {rulesData.playerRules.pCanHurtp}");
                        return true;
                    }
                    else
                    {
                        if (Debug) Puts($"Attacker: {attacker.ShortPrefabName} & Victim: {victim.ShortPrefabName} & Rules damages for player type");
                        return false;
                    }
                }
            }

            if (hit.Initiator is BasePlayer attacker1)
            {
                if (attacker1.userID.IsSteamId())
                {
                    if (IsHostileEntity(entity))
                    {
                        if (Debug) Puts($"Attacker: {attacker1.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules {rulesData.playerRules.pCanHurtn}");
                        return rulesData.playerRules.pCanHurtn;
                    }
                    else if (IsFriendlyEntity(entity))
                    {
                        if (Debug) Puts($"Attacker: {attacker1.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules {rulesData.playerRules.pCanHurta}");
                        return rulesData.playerRules.pCanHurta;
                    }
                    else if (IsDamagingEntity(entity))
                    {
                        if (Debug) Puts($"Attacker: {attacker1.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules {rulesData.playerRules.pCanDamEntitys}");

                        BuildingPrivlidge privs = entity.GetBuildingPrivilege();
                        if (privs != null)
                        {
                            foreach (ulong auth in privs.authorizedPlayers.Select(x => x.userid).ToArray())
                            {
                                if (attacker1.userID == auth)
                                {
                                    return rulesData.playerRules.pCanDamageOwnBuildings;
                                }else
                                {
                                    return false;
                                }
                            }
                        }
                        else
                        {
                            return rulesData.playerRules.pCanDamEntitys;
                        }
                        
                    }

                    
                    /*if (entity.HasParent() && entity.GetParentEntity() is Tugboat tugboat)
                    {
                        if (tugboat && CheckTugboat(tugboat, attacker1))
                        {
                            if (Debug) Puts($"Attacker: {attacker1.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules {config.playerRules.pCanDamEntitys} Tugboat");
                            return config.playerRules.pCanDamageOwnBuildings;
                        }                     
                    }*/

                    //twig
                    if (entity is BuildingBlock && attacker1.userID.IsSteamId())
                    {
                        var twig = entity as BuildingBlock;
                        if (twig.grade == BuildingGrade.Enum.Twigs)
                        {
                            if (entity.OwnerID == 0) return true;
                            if (entity.OwnerID == attacker1.userID || IsAlliedWithPlayer(entity.OwnerID, attacker1.userID) || BuildingAuth(twig, attacker1)) //|| BuildingAuth(twig, attacker1) //allow twig damage even if he is not owner or teammates but twig is on his tc area 
                            {
                                if (Debug) Puts($"Attacker: {attacker1.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules {rulesData.playerRules.PallowTwigDamage}");
                                return true;
                            }
                            else
                            {
                                if (Debug) Puts($"Attacker: {attacker1.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules {rulesData.playerRules.PallowTwigDamage}");
                                return rulesData.playerRules.PallowTwigDamage;
                            }
                        }
                    }
                    //check wooden shelter
                    if (entity.ShortPrefabName.Contains("legacy.shelter.wood"))
                    {
                        if (entity.OwnerID == 0) return true;
                        if (entity.OwnerID == attacker1.userID)
                        {
                            if (Debug) Puts($"Attacker: {attacker1.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules Legacy Wooden Shelter");
                            return true;
                        }
                        else
                        {
                            return rulesData.playerRules.pCanDamOtherpBuildings;
                        }
                    }
                    // Check if the entity is a BuildingPrivilege
                    if (entity is BuildingPrivlidge)
                    {
                        object owned = PlayerOwnsTC(attacker1.userID, entity as BuildingPrivlidge);

                        if (owned is bool && !(bool)owned)
                        {
                            // If the player doesn't own the building, check if they can damage other player builds
                            if (Debug) Puts($"Attacker: {attacker1.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules {rulesData.playerRules.pCanDamOtherpBuildings}");
                            return rulesData.playerRules.pCanDamOtherpBuildings;
                        }
                        else
                        {
                            // If the player owns the building or is allied with teammates, check if they can damage own buildings
                            if (Debug) Puts($"Attacker: {attacker1.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules {rulesData.playerRules.pCanDamOwnTeamamtesBuilding}");
                            return rulesData.playerRules.pCanDamageOwnBuildings || rulesData.playerRules.pCanDamOwnTeamamtesBuilding;
                        }
                    }
                    // Check if the entity is a BaseEntity
                    else if (entity is BaseEntity && attacker1.userID.IsSteamId())
                    {
                        object ownsEntity = OwnsItem(attacker1.userID, entity);

                        if (ownsEntity is bool && (bool)ownsEntity)
                        {
                            // If the player or their team owns the entity, check if they can damage it
                            if (Debug) Puts($"Attacker: {attacker1.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules BaseEntity");
                            return true;
                        }
                        else
                        {
                            if (IsAuthtoTC(entity, attacker1) && entity.OwnerID != attacker1.userID)
                            {
                                return true;
                            }
                            else
                            {
                                // If the player doesn't own the entity, check if they can damage other player entities
                                if (Debug) Puts($"Attacker: {attacker1.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules {rulesData.playerRules.pCanDamOtherpBuildings}");
                                return rulesData.playerRules.pCanDamOtherpBuildings;
                            }                           
                        }
                    }                   
                }
            }

            if (hit.Initiator is BaseAnimalNPC || hit.Initiator is BaseNPC2)
            {
                if (entity is BasePlayer)
                {
                    if (Debug) Puts($"Attacker: {hit.Initiator.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules {rulesData.npcRules.AnimalCanHurtP}");
                    return rulesData.npcRules.AnimalCanHurtP;
                }
                else
                {
                    //if (Debug) Puts($"Attacker: {hit.Initiator.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules Animal cruelity");
                    return true;
                }
            }

            if (hit.Initiator is ScientistNPC || hit.Initiator is NPCPlayer || hit.Initiator is HumanNPC ||
                hit.Initiator is TunnelDweller || hit.Initiator is UnderwaterDweller || hit.Initiator is ScarecrowNPC || hit.Initiator is FrankensteinPet || hit.Initiator is SimpleShark ||
                (hit.Initiator != null && hit.Initiator.ShortPrefabName.Contains("zombie")) || (hit.Initiator != null && hit.Initiator.ShortPrefabName.Contains("CustomScientistNPC")))
            {
                if (entity is BasePlayer victim2)
                {
                    if (victim2.userID.IsSteamId() && !victim2.IsNpc)
                    {
                        if (Debug) Puts($"Attacker: {hit.Initiator.ShortPrefabName} & Victim: {victim2.ShortPrefabName} & Rules {rulesData.npcRules.NpcHurtP}");
                        return rulesData.npcRules.NpcHurtP;
                    }
                    else
                    {
                        if (Debug) Puts($"Attacker: {hit.Initiator.ShortPrefabName} & Victim: {victim2.ShortPrefabName} & Rules {rulesData.npcRules.NpcHurtP}");
                        return true;
                    }
                }
                else if (entity != null && (entity is BuildingBlock || entity is BuildingPrivlidge))
                {
                    if (Debug) Puts($"Attacker: {hit.Initiator.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules attacker npc vic buildings");
                    return true;
                }
                else return true;
            }

            if (hit.Initiator is PatrolHelicopter || (hit.WeaponPrefab != null && (hit.WeaponPrefab.ShortPrefabName.Equals("rocket_heli") || hit.WeaponPrefab.ShortPrefabName.Equals("rocket_heli_napalm"))) 
                || (hit.Initiator is PatrolHelicopter && hit.damageTypes.Has(DamageType.Explosion)) 
                || (hit.Initiator != null && (hit.Initiator.ShortPrefabName.Equals("oilfireballsmall") || hit.Initiator.ShortPrefabName.Equals("napalm"))))
            {
                if (entity is BasePlayer)
                {
                    if (Debug) Puts($"Attacker: {hit.Initiator.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules {rulesData.npcRules.PatrolHurtP}");
                    return rulesData.npcRules.PatrolHurtP;
                }
                else if (entity is BuildingBlock || entity is BaseEntity)
                {
                    if (Debug) Puts($"Attacker: {hit.Initiator.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules {rulesData.npcRules.PatrolHurtP}");
                    return rulesData.npcRules.PatrolHurtB;
                }
            }

            if (hit.Initiator is BaseEntity wall)
            {
                if (wall.PrefabName.Contains("wall.external") || wall.ShortPrefabName.Contains("gates.external.high"))
                {
                    if (Debug) Puts($"Attacker: {wall.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules {rulesData.entityRules.WallHurtP}");
                    return rulesData.entityRules.WallHurtP;
                }
                else if (wall.ShortPrefabName.Contains("barricade"))
                {
                    if (Debug) Puts($"Attacker: {wall.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules {rulesData.entityRules.BarrHurtP}");
                    return rulesData.entityRules.BarrHurtP;
                }
            }

            if (hit.Initiator is BaseModularVehicle vehicle && vehicle.name.Contains("modularcar"))
            {
                if (entity is BasePlayer vP)
                {
                    if (vP.userID.IsSteamId())
                    {
                        if (!vP.IsSleeping())
                        {
                            if (Debug) Puts($"Attacker: {hit.Initiator.ShortPrefabName} & Victim: {vP.ShortPrefabName} & Rules {rulesData.entityRules.VehicleHurtP}");
                            return rulesData.entityRules.VehicleHurtP;
                        }
                        else if (rulesData.gRules.killSleepers)
                        {
                            if (Debug) Puts($"Attacker: {hit.Initiator.ShortPrefabName} & Victim: {vP.ShortPrefabName} & Rules {rulesData.gRules.killSleepers}");
                            return true;
                        }
                    }
                }
                else if (entity != null && (entity is BaseAnimalNPC || IsHostileEntity(entity)))
                {
                    if (Debug) Puts($"Attacker: {hit.Initiator.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules Attacker Vehicle & Victim all hostiles and animal");
                    return true;
                }
                else
                {
                    if (Debug) Puts($"Attacker: {hit.Initiator.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules Attack cars vic all");
                    return rulesData.entityRules.enableVehicleCollsionD;
                }
            }

            if (hit.Initiator is Minicopter || hit.Initiator is ScrapTransportHelicopter || hit.Initiator is AttackHelicopter)
            {
                if (entity is BasePlayer || entity is BuildingBlock)
                {
                    if (Debug) Puts($"Attacker: {hit.Initiator.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules {rulesData.entityRules.VehicleHurtP}");
                    return rulesData.entityRules.VehicleHurtP;
                }
            }

            if (hit.Initiator is SamSite)
            {
                if (entity is Minicopter || entity is ScrapTransportHelicopter || entity is Parachute || entity is BasePlayer 
                    || entity is HotAirBalloon || entity is HotAirBalloonArmor || entity is AttackHelicopter)
                {
                    if (!rulesData.npcRules.NSamigP || !rulesData.npcRules.PSamigP)
                    {
                        if (Debug) Puts($"Attacker: {hit.Initiator.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules {rulesData.npcRules.NSamigP} {rulesData.npcRules.PSamigP}");
                        return true;
                    }
                    else
                    {
                        if (Debug) Puts($"Attacker: {hit.Initiator.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules {rulesData.npcRules.NSamigP} {rulesData.npcRules.PSamigP}");
                        return false;
                    }
                }
            }
            var findIni = hit.Initiator ?? hit.Weapon ?? hit.WeaponPrefab;
            //trap damages
            if (entity is BasePlayer victim3 && hit.Initiator != null && (hit.Initiator is BaseTrap || hit.Initiator is GunTrap || hit.Initiator is FlameTurret || hit.Initiator is AutoTurret 
                || hit.Initiator is TeslaCoil || hit.Initiator is ReactiveTarget))
            {
                if (!rulesData.entityRules.trapD && victim3.userID.IsSteamId() || ((!rulesData.entityRules.trapD || rulesData.entityRules.trapD) && (victim3.IsNpc || IsHostileEntity(victim3))) || hit.Initiator.OwnerID == 0)
                {
                    if (Debug) Puts($"Attacker: {hit.Initiator.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules {rulesData.entityRules.trapD}");
                    return true;
                }
                else
                {
                    return false;
                }
            }

            TryInitiator(hit, findIni);

            //default collision damage
            if (entity is BaseVehicle)
            {
                BaseVehicle vehicleGet = entity as BaseVehicle;
                if ((vehicleGet.GetDriver() != null && findIni == vehicleGet) || vehicleGet.GetDriver() == null)
                {
                    if (Debug) Puts($"Attacker: {hit.Initiator.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules {rulesData.entityRules.enableVehicleCollsionD}");
                    return rulesData.entityRules.enableVehicleCollsionD;
                }
            }

            //Bradly Damages           
            if (findIni is BradleyAPC || findIni?.ShortPrefabName == "maincannonshell")
            {
                if (entity is BasePlayer || entity is BaseEntity)
                {
                    if (Debug) Puts($"Attacker: {hit.Initiator.ShortPrefabName} & Victim: {entity.ShortPrefabName} & Rules {rulesData.npcRules.bradleyHurtP}");
                    return rulesData.npcRules.bradleyHurtP;
                }
            }

            if ((findIni != null && findIni.ShortPrefabName.Contains("oilfireball2")) 
                || (findIni != null && findIni.ShortPrefabName.Contains("oilfireballsmall")) 
                || (findIni != null && findIni.ShortPrefabName.Contains("fireball")))
            {
                if (entity is BasePlayer || entity is BaseEntity)
                {
                    BasePlayer hitter = GetOwnerPlayer(findIni);
                    if (hitter != null)
                    {
                        return false;
                    }
                    else return false;
                }
            }

            //train
            if (entity.ShortPrefabName.Contains("trainbarricade"))
            {
                return true;
            }            
            
            if (findIni != null && findIni?.ShortPrefabName == "campfire")
            {
                return true;
            }



            return false;
        }


        private bool CheckPVPDelay(BasePlayer victim, BasePlayer attacker)
        {
            PVPDelay victimDelay;
            if (pvp_delays.TryGetValue(victim.userID, out victimDelay))
            {
                if (!string.IsNullOrEmpty(victimDelay.ZoneID) && IsPlayerInZone(victimDelay, attacker)) //ZonePlayer attacked delayed player
                {
                    return true;
                }
                PVPDelay attackerDelay;
                if (pvp_delays.TryGetValue(attacker.userID, out attackerDelay) && victimDelay.ZoneID == attackerDelay.ZoneID) //Delayed player attacked delayed player
                {
                    return true;
                }
            }
            else
            {
                PVPDelay attackerDelay;
                if (pvp_delays.TryGetValue(attacker.userID, out attackerDelay))
                {
                    if (!string.IsNullOrEmpty(attackerDelay.ZoneID) && IsPlayerInZone(attackerDelay, victim)) //DelayedPlayer attack ZonePlayer
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsPlayerInZone(PVPDelay delay, BasePlayer player)
        {
            return (bool)ZoneManager.Call("IsPlayerInZone", delay.ZoneID, player);
        }

        private BasePlayer GetOwnerPlayer(BaseEntity entity)
        {
            if (entity != null)
            {
                BasePlayer ownerPlayer = entity.OwnerID != 0 ? BasePlayer.FindByID(entity.OwnerID) : null;
                return ownerPlayer;
            }
            return null;
        }


        //entitys Methods
        private bool IsHostileEntity(BaseCombatEntity entity)
        {
            return entity.ShortPrefabName.Contains("scientistnpc") || entity.ShortPrefabName.Contains("scientist") || entity.ShortPrefabName.Contains("frankensteinpet") ||
                   entity.ShortPrefabName.Contains("zombie") || entity.ShortPrefabName.Contains("tunneldweller") ||
                   entity.ShortPrefabName.Contains("underwaterdweller") || entity.ShortPrefabName.Contains("scarecrow") ||
                   entity.ShortPrefabName.Contains("xmasdwelling") || entity.ShortPrefabName.Contains("gingerbread") || entity.ShortPrefabName.Contains("CustomScientistNPC") || 
                   entity.ShortPrefabName.Contains("zombie") || entity.ShortPrefabName.Contains("shark");
        }

        private bool IsFriendlyEntity(BaseCombatEntity entity)
        {
            return entity.ShortPrefabName.Contains("bear") || entity.ShortPrefabName.Contains("boar") ||
                   entity.ShortPrefabName.Contains("chicken") || entity.ShortPrefabName.Contains("stag") ||
                   entity.ShortPrefabName.Contains("wolf") || entity.ShortPrefabName.Contains("deer") || entity.ShortPrefabName.Contains("wolf2") || entity is BaseAnimalNPC || entity is BaseNPC || entity is BaseNPC2;
        }

        private bool IsDamagingEntity(BaseCombatEntity entity)
        {
            return entity.name.Contains("modularcar") || entity.name.Contains("mini") ||
                   entity.name.Contains("scraptransport") || entity.name.Contains("attackheli") ||
                   entity.name.Contains("row") || entity.name.Contains("rhib") ||
                   entity.name.Contains("helicopter") ||
                   entity.name.Contains("bike");
        }

        private bool CheckTugboat(Tugboat boat, BasePlayer player)
        {
            return !boat.children.IsNullOrEmpty() && boat.children.Exists(child => child is VehiclePrivilege vehiclePrivilege && vehiclePrivilege.AnyAuthed() && vehiclePrivilege.IsAuthed(player));
        }

        //methods
        private void noDamage(HitInfo hit)
        {
            hit.damageTypes = new DamageTypeList();
            hit.DidHit = false;
            hit.DoHitEffects = false;
            hit.damageTypes.ScaleAll(0);
        }

        private void TryInitiator(HitInfo hitInfo, BaseEntity weapon)
        {
            if (weapon == null)
            {
                return;
            }

            if (!(hitInfo.Initiator is BasePlayer) && weapon.creatorEntity is BasePlayer)
            {
                hitInfo.Initiator = weapon.creatorEntity;
            }

            if (hitInfo.Initiator == null)
            {
                hitInfo.Initiator = weapon.GetParentEntity();
            }

            if (hitInfo.Initiator == null)
            {
                hitInfo.Initiator = weapon;
            }
        }

        public bool IsAlliedWithPlayer(ulong playerId, ulong targetId) //changed
        {
            if (playerId == targetId)
            {
                return true;
            }

            if (RelationshipManager.ServerInstance.playerToTeam.TryGetValue(playerId, out var team) && team.members.Contains(targetId))
            {
                return true;
            }

            if (Clans != null && (bool)Clans.Call("IsClanMember", playerId.ToString(), targetId.ToString()))
            {
                return true;
            }

            if (FClan != null && (bool)Clans.Call("IsClanMember", playerId.ToString(), targetId.ToString()))
            {
                return true;
            }

            if (Friends != null && (bool)Friends.Call("AreFriends", playerId.ToString(), targetId.ToString()))
            {
                return true;
            }

            return false;
        }

        private bool canBreakTC(BasePlayer player, DecayEntity Entity)
        {
            foreach (var p in player.currentTeam.ToString())
            {
                if (p == Entity.OwnerID) { return true; }
            }
            return false;
        }

        private object PlayerOwnsTC(ulong p, BuildingPrivlidge privilege)
        {
            if (p == null || privilege == null) return null;

            BuildingManager.Building building = privilege.GetBuilding();
            if (building != null)
            {
                BuildingPrivlidge pr = building.GetDominatingBuildingPrivilege();

                foreach (ulong authroized in pr.authorizedPlayers.Select(x => x.userid).ToArray())
                {
                    if (privilege.OwnerID == p || p == authroized || IsAlliedWithPlayer(authroized, privilege.OwnerID))
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }

                }
                return false;
            }
            return null;

        }
        private bool BuildingAuth(BuildingBlock block, BasePlayer player)
        {
            var building = block.GetBuilding();
            if (building == null) return false;
            var priv = building.GetDominatingBuildingPrivilege();
            return priv != null && priv.IsAuthed(player);
        }

        private bool EntityAuth(BaseEntity entity, BasePlayer player)
        {
            if (entity.OwnerID == 0) return true;

            return player.CanBuild(entity.WorldSpaceBounds());
        }

        private bool TugboatAuthed(Tugboat tugboat, BasePlayer attacker)
        {
            return !tugboat.children.IsNullOrEmpty() && tugboat.children.Exists(child => child is VehiclePrivilege vehiclePrivilege && vehiclePrivilege.AnyAuthed() && vehiclePrivilege.IsAuthed(attacker));
        }

        private object OwnsItem(ulong p, BaseEntity e)
        {
            if (p == null || e == null)
            {
                return null;
            }
            if (e.OwnerID == 0) return true;

            BuildingPrivlidge priv = e.GetBuildingPrivilege();
            bool hasPriv = false;
            if (priv != null)
            {
                foreach (ulong authrized in priv.authorizedPlayers.Select(x => x.userid).ToArray())
                {
                    if (p == authrized)
                    {
                        //player has privs on the entity
                        hasPriv = true;
                        break;
                    }
                    else
                    {
                        if (IsAlliedWithPlayer(authrized, e.OwnerID))
                        {
                            hasPriv = true;
                            //player is teams with the owner
                            break;
                        }
                    }
                }
            }
            else
            {
                if (e.OwnerID == p || IsAlliedWithPlayer(e.OwnerID, p))
                {
                    return true;
                }
                else
                {
                    return false;
                }              
            }
            bool isFriend = IsAlliedWithPlayer(p, e.OwnerID);
            if (hasPriv)
            {
                if (rulesData.playerRules.pCanDamageOwnBuildings && e.OwnerID == p)
                {
                    return true; // Player owns entity and has privs
                }
                else if (isFriend && rulesData.playerRules.pCanDamOwnTeamamtesBuilding)
                {
                    return true; // Player is allied with the entity owner & has Building priv
                }
            }

            return false; // Default to false if none of the above conditions are met

        }

        private object OwnsItemTest(ulong p, BaseEntity e)
        {
            if (p == null || e == null)
            {
                return null;
            }
            if (e.OwnerID == 0) return true;

            if (e is BuildingBlock)
            {
                BuildingManager.Building building = (e as BuildingBlock)?.GetBuilding();

                if (building != null)
                {
                    BuildingPrivlidge privs = building.GetDominatingBuildingPrivilege();

                    foreach (ulong auth in privs.authorizedPlayers.Select(x => x.userid).ToArray())
                    {
                        if (e?.OwnerID == p)
                        {
                            return true;
                        }
                        else if (p == auth)
                        {
                            return true;
                        }
                        else
                        {
                            object isfr = IsAlliedWithPlayer(auth, e.OwnerID);
                            if (isfr != null && isfr is bool && (bool)isfr)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            else
            {
                BuildingPrivlidge priv = e.GetBuildingPrivilege();
                bool hasPriv = false;
                if (priv != null)
                {
                    foreach (ulong authrized in priv.authorizedPlayers.Select(x => x.userid).ToArray())
                    {
                        if (p == authrized)
                        {
                            //player has privs on the entity
                            hasPriv = true;
                            break;
                        }
                        else
                        {
                            if (IsAlliedWithPlayer(authrized, e.OwnerID))
                            {
                                hasPriv = true;
                                //player is teams with the owner
                                break;
                            }
                        }
                    }
                }
                else
                {
                    return true;
                }

                bool isFriend = IsAlliedWithPlayer(p, e.OwnerID);
                if (hasPriv)
                {
                    if (rulesData.playerRules.pCanDamageOwnBuildings && e.OwnerID == p)
                    {
                        return true; // Player owns entity and has privs
                    }
                    else if (isFriend && rulesData.playerRules.pCanDamOwnTeamamtesBuilding)
                    {
                        return true; // Player is allied with the entity owner & has Building priv
                    }
                }
            }

            return false; // Default to false if none of the above conditions are met
        }

        private void Initiator(HitInfo hitInfo, BaseEntity weapon)
        {
            if (weapon == null)
            {
                return;
            }

            if (!(hitInfo.Initiator is BasePlayer) && weapon.creatorEntity is BasePlayer)
            {
                hitInfo.Initiator = weapon.creatorEntity;
            }

            if (hitInfo.Initiator == null)
            {
                hitInfo.Initiator = weapon.GetParentEntity();
            }

            if (hitInfo.Initiator == null)
            {
                hitInfo.Initiator = weapon;
            }
        }

        private bool CheckExclusion(List<string> locations1, List<string> locations2)
        {
            if (locations1 == null || locations2 == null) return false;
            List<string> locations = GetSharedLocations(locations1, locations2);

            if (!locations.IsNullOrEmpty())
            {
                foreach (string loc in locations)
                {                    
                    return true;
                }

            }
            return false;
        }

        private List<string> GetSharedLocations(List<string> e0Locations, List<string> e1Locations)
        {
            return e0Locations.Intersect(e1Locations).ToList();
        }

        private bool CheckConvoy(BaseEntity entity)
        {
            if (Convoy != null)
            {
                object result = Convoy.CallHook("IsConvoyVehicle", entity);
                if (result is bool)
                {
                    return (bool)result;
                }
            }
            return false;
        }

        #endregion

        #region Looting Rules               

        private void OnItemDropped(Item item, BaseEntity entity)
        {
            if (!rulesData.lootRules.useLP) return;

            if (item.info.itemid == -907422733 || item.info.itemid == 2068884361)
            {
                if (entity.OwnerID == 0)
                {
                    entity.OwnerID = item.GetOwnerPlayer()?.userID ?? 0;
                }                  
            }
        }

        private object OnItemPickup(Item item, BasePlayer player)
        {
            if (player == null || item == null) return null;

            if (config.statusUI.status) ModifiersCustom.GetValueOrDefault(player.userID)?.items.CheckForTime(item.info.shortname); //status

            if (!rulesData.lootRules.useLP) return null;            
            if (rulesData.lootRules.adminCanLootAll && permission.UserHasPermission(player.UserIDString, PvEAdminLootPerms)) return null;

            BaseEntity entity = item?.GetWorldEntity();

            // Check if player and entity are valid, and entity has a valid owner
            if (entity != null && entity.OwnerID != 0)
            {
                // Check if the item is one of the specified item IDs, player is not an NPC, and player is a Steam user
                if ((item.info.itemid == -907422733 || item.info.itemid == 2068884361) && !player.IsNpc && player.userID.IsSteamId())
                {
                    // Allow pickup if the player is the owner, allied with the owner, or in the zone
                    if (entity.OwnerID == player.userID || IsAlliedWithPlayer(entity.OwnerID, player.userID) || PlayersIsInZone(player))
                    {
                        return null;
                    }

                    // Otherwise, show a warning and prevent pickup
                    ShowWarningLoot(player);
                    return false;
                }
            }

            return null;
        }

        private object OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (!rulesData.lootRules.useLP) return null;
            if (player == null || entity == null) return null;
            if (rulesData.lootRules.adminCanLootAll && permission.UserHasPermission(player.UserIDString, PvEAdminLootPerms)) return null;

            Item item = entity?.GetItem();
            if (player != null && item != null && entity.OwnerID != 0 && (item.info.itemid == -907422733 || item.info.itemid == 2068884361) && !player.IsNpc && player.userID.IsSteamId())
            {
                if (entity.OwnerID != player.userID && !IsAlliedWithPlayer(entity.OwnerID, player.userID))
                {
                    NextFrame(() =>
                    {
                        player.inventory.loot.Clear();
                        player.inventory.loot.SendImmediate();
                    });

                    ShowWarningLoot(player);
                }
            }
            return null;
        }


        private object CanLootEntity(BasePlayer player, LootableCorpse corpse)
        {
            if (!rulesData.lootRules.useLP) return null;
            if (player == null || corpse == null) return null;
            if (rulesData.lootRules.adminCanLootAll && permission.UserHasPermission(player.UserIDString, PvEAdminLootPerms)) return null;
            if (corpse.playerSteamID < 76561197960265728L || corpse.playerSteamID == player.userID || (rulesData.lootRules.teamsAccessLoot && IsAlliedWithPlayer(corpse.playerSteamID, player.userID))) return null;
            if (rulesData.lootRules.pclpvpzones)
            {
                if (ZoneManagerCheck(player, corpse))
                {
                    return null;
                }
            }
            ShowWarningLoot(player);
            return false;
        }      
        private object CanLootEntity(BasePlayer player, DroppedItemContainer container)
        {
            if (!rulesData.lootRules.useLP) return null;
            if (player == null || container == null) return null;
            if (container.playerSteamID == 0) return null;
            if (rulesData.lootRules.adminCanLootAll && permission.UserHasPermission(player.UserIDString, PvEAdminLootPerms)) return null;

            if (container.name.Contains("item_drop_backpack"))
            {
                if (ZoneManager != null && rulesData.lootRules.pclpvpzones && ZoneManagerCheck(player, container))
                {
                    return null;
                }
                if (container.playerSteamID < 76561197960265728L || container.playerSteamID == player.userID || (rulesData.lootRules.teamsAccessLoot && IsAlliedWithPlayer(container.OwnerID, player.userID)))
                    return null;
                ShowWarningLoot(player);
                return false;
            }

            return null;
        }
        private object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (!rulesData.lootRules.useLP) return null;
            if (player == null || container == null) return null;
            BaseEntity entity = container as BaseEntity;
            BaseEntity childentity = entity;
            entity = CheckParent(entity);

            if (config.excludedEntitys.Count > 0 && config.excludedEntitys.Contains(entity.ShortPrefabName)) return null;

            if (IsVendingOpen(player, entity) || IsDropBoxOpen(player, entity)) return null;

            if (rulesData.lootRules.adminCanLootAll && permission.UserHasPermission(player.UserIDString, PvEAdminLootPerms)) return null;

            if (entity.OwnerID == player.userID || (rulesData.lootRules.teamsAccessLoot && IsAlliedWithPlayer(entity.OwnerID, player.userID)) || IsAuthtoTC(entity, player) || (entity is SupplyDrop && rulesData.lootRules.anyoneOpenSupplyDrop))
            {
                return null;
            }

            if (entity.OwnerID != player.userID && !IsAlliedWithPlayer(entity.OwnerID, player.userID) && entity.OwnerID != 0)
            {
                ShowWarningLoot(player);
                return false;
            }

            return null;
        }       
        private object CanLootPlayer(BasePlayer target, BasePlayer player)
        {
            if (!rulesData.lootRules.useLP) return null;
            if (player == null || target == null) return null;
            if (target.userID == 0) return null;
            if (rulesData.lootRules.adminCanLootAll && permission.UserHasPermission(player.UserIDString, PvEAdminLootPerms)) return null;

            if (target.userID == player.userID || (rulesData.lootRules.teamsAccessLoot && IsAlliedWithPlayer(target.userID, player.userID))) return null;
            if (rulesData.lootRules.pclpvpzones)
            {
                if (ZoneManagerCheck(player, target))
                {
                    return null;
                }
            }
            ShowWarningLoot(player);
            return false;
        }

        private object CanPickupEntity(BasePlayer player, BaseCombatEntity ent)
        {
            if (!rulesData.lootRules.useLP) return null;
            if (player == null || ent == null) return null;
            BaseEntity entity = ent as BaseEntity;
            if (entity.OwnerID == 0) return null;
            if (rulesData.lootRules.adminCanLootAll && permission.UserHasPermission(player.UserIDString, PvEAdminLootPerms)) return null;

            if (entity.OwnerID == player.userID || (rulesData.lootRules.teamsAccessLoot && IsAlliedWithPlayer(entity.OwnerID, player.userID)) || IsAuthtoTC(entity, player)) return null;

            if (ent.OwnerID != 0 && entity.OwnerID != player.userID && !IsAlliedWithPlayer(entity.OwnerID, player.userID))
            {
                ShowWarningLoot(player);
                return false;
            }

            return null;
        }

        private object CanAdministerVending(BasePlayer player, VendingMachine vending)
        {
            if (!rulesData.lootRules.useLP) return null;
            if (player == null || vending == null) return null;
            if (vending.OwnerID == 0) return null;
            if (rulesData.lootRules.adminCanLootAll && permission.UserHasPermission(player.UserIDString, PvEAdminLootPerms)) return null;

            if (vending.OwnerID == player.userID || (rulesData.lootRules.teamsAccessLoot && IsAlliedWithPlayer(vending.OwnerID, player.userID))) return null;

            ShowWarningLoot(player);
            return false;
        }

        private object OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            if (!rulesData.lootRules.useLP) return null;
            if (player == null || oven == null) return null;
            if (oven.OwnerID == 0) return null;
            if (rulesData.lootRules.adminCanLootAll && permission.UserHasPermission(player.UserIDString, PvEAdminLootPerms)) return null;

            if (oven.OwnerID == player.userID || (rulesData.lootRules.teamsAccessLoot && IsAlliedWithPlayer(oven.OwnerID, player.userID))) return null;

            ShowWarningLoot(player);
            return false;

        }

        private BaseEntity CheckParent(BaseEntity entity)
        {
            if (entity.HasParent())
            {
                BaseEntity parententity = entity.GetParentEntity();
                if (parententity is MiningQuarry)
                {
                    entity.OwnerID = parententity.OwnerID;
                    entity = parententity;
                }
            }
            return entity;
        }

        private bool IsAuthtoTC(BaseEntity entity, BasePlayer player)
        {
            //BaseEntity entity = ent as BaseEntity;
            BuildingPrivlidge bPrev = player.GetBuildingPrivilege(new OBB(entity.transform.position, entity.transform.rotation, entity.bounds));
            BuildingPrivlidge noPrev = entity.GetBuildingPrivilege();
            if (bPrev == null)
            {
                return false;
            }
            else
            {              
                if (bPrev.IsAuthed(player)) return true;
            }
            return false;
        }

        private bool IsVendingOpen(BasePlayer player, BaseEntity entity)
        {
            if (entity is VendingMachine)
            {
                VendingMachine shopFront = entity as VendingMachine;
                if (shopFront.PlayerInfront(player)) return true;
                return false;
            }
            return false;
        }
        private bool IsDropBoxOpen(BasePlayer player, BaseEntity entity)
        {
            if (entity is DropBox)
            {
                DropBox dropboxFront = entity as DropBox;
                if (dropboxFront.PlayerInfront(player)) return true;
                return false;
            }
            return false;
        }

        private bool ZoneManagerCheck(BasePlayer player, BaseEntity entity)
        {
            List<string> playerZones = GetAllZones(player);
            List<string> entityZones = GetAllZones(entity);

            if (CheckExclusion(playerZones, entityZones))
            {
                return true;
            }
            return false;
        }

        private bool CheckRackBox(BasePlayer player, BaseEntity entity)
        {
            if (entity == null || player == null) return true;
            if (entity.OwnerID == 0) return true;
            if (rulesData.lootRules.adminCanLootAll && permission.UserHasPermission(player.UserIDString, PvEAdminLootPerms)) return true;
            if (entity.OwnerID == player.userID || (rulesData.lootRules.teamsAccessLoot && IsAlliedWithPlayer(entity.OwnerID, player.userID)) || IsAuthtoTC(entity, player)) return true;

            return false;
        }

        private object OnRackedWeaponMount(Item item, BasePlayer player, WeaponRack rack)
        {
            if (!rulesData.lootRules.useLP) return null;
            if (rack == null || player == null || item == null) return null;

            BaseEntity entity = rack as BaseEntity;
            if (CheckRackBox(player, entity))
            {
                return null;
            }

            ShowWarningLoot(player);
            return false;
        }

        private object OnRackedWeaponSwap(Item item, WeaponRackSlot weaponTaking, BasePlayer player, WeaponRack rack)
        {
            if (!rulesData.lootRules.useLP) return null;
            if (rack == null || player == null || item == null) return null;

            BaseEntity entity = rack as BaseEntity;
            if (CheckRackBox(player, entity))
            {
                return null;
            }

            ShowWarningLoot(player);
            return false;
        }

        private object OnRackedWeaponTake(Item item, BasePlayer player, WeaponRack rack)
        {
            if (!rulesData.lootRules.useLP) return null;
            if (rack == null || player == null || item == null) return null;

            BaseEntity entity = rack as BaseEntity;
            if (CheckRackBox(player, entity))
            {
                return null;
            }

            ShowWarningLoot(player);
            return false;
        }

        private object OnRackedWeaponUnload(Item item, BasePlayer player, WeaponRack rack)
        {
            if (!rulesData.lootRules.useLP) return null;
            if (rack == null || player == null || item == null) return null;

            BaseEntity entity = rack as BaseEntity;
            if (CheckRackBox(player, entity))
            {
                return null;
            }

            ShowWarningLoot(player);
            return false;
        }

        private object OnRackedWeaponLoad(Item item, ItemDefinition ammoItem, BasePlayer player, WeaponRack rack)
        {
            if (!rulesData.lootRules.useLP) return null;
            if (rack == null || player == null || item == null) return null;

            BaseEntity entity = rack as BaseEntity;
            if (CheckRackBox(player, entity))
            {
                return null;
            }

            ShowWarningLoot(player);
            return false;
        }

        /// <summary>
        ///  Credit to rightful owner of the code: Kaucsenta
        ///  https://umod.org/plugins/anti-ladder-and-twig
        /// </summary>

        private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            if (!rulesData.lootRules.useLP) return null;

            BasePlayer player = planner.GetOwnerPlayer();
            if (player == null) return null;
            if (permission.UserHasPermission(player.UserIDString, PvEAdminLootPerms)) return null;

            bool raidbaseFlag = true;
            bool abandonedBaseFlag = false;

            if (IsSpecialPrefab(prefab) && target.entity != null)
            {
                if (IsValidEntity(target.entity))
                {
                    if (target.entity.GetBuildingPrivilege() != null)
                    {
                        foreach (var tmpplayer in target.entity.GetBuildingPrivilege().authorizedPlayers)
                        {
                            if (covalence.Players.FindPlayerById(tmpplayer.userid.ToString()) != null)
                            {
                                // If there is any valid player authorized, then it is not a raidable base
                                raidbaseFlag = false;
                            }

                            if (tmpplayer.userid == target.player.userID || IsAlliedWithPlayer(tmpplayer.userid, target.player.userID))
                            {
                                return null; // Player is authorized, allow building
                            }
                        }

                        if (System.Convert.ToBoolean(AbandonedBases?.Call("EventTerritory", target.position)))
                        {
                            // If the entity is abandoned, then ladder can be placed like raidable bases
                            abandonedBaseFlag = true;
                        }

                        if (raidbaseFlag || abandonedBaseFlag)
                        {
                            return null; // It is a raidable base or an abandoned base, allow building
                        }
                        else
                        {
                            SendChatMessage(player, config.displayNotify.prefix + config.displayNotify.LPChatMessage);
                            return false; // Do not allow building
                        }
                    }
                }
            }

            return null; // Default case, return null
        }

        private bool IsSpecialPrefab(Construction prefab)
        {
            return prefab.fullName.Contains("floor.prefab") || prefab.fullName.Contains("ladder.wooden.wall") || prefab.fullName.Contains("floor.frame") || prefab.fullName.Contains("floor.triangle.frame")
                || prefab.fullName.Contains("floor.triangle") || prefab.fullName.Contains("shopfront") || prefab.fullName.Contains("shutter") || prefab.fullName.Contains("neon") || prefab.fullName.Contains("sign");
        }

        private bool IsValidEntity(BaseEntity entity)
        {
            return entity is SimpleBuildingBlock || entity is BuildingBlock || entity is ShopFront || entity is StabilityEntity || entity is NeonSign || entity is Signage;
        }



        #endregion

        #region UIs

        #region PVE or PvP info  & Loot Ui
        private const string UIA = "UILayerAlert";
        private List<ulong> NotifyACool = new List<ulong>();
        private List<ulong> NotifyMCool = new List<ulong>();
        private void ShowWarningAlert(BasePlayer player)
        {
            if (player == null) return;
            if (!config.displayNotify.showDamageM) return;

            if (config.displayNotify.showDamageMType == "Both")
            {
                ShowUIWarning(player);
                ShowWarningMessage(player);

            }
            else if (config.displayNotify.showDamageMType == "UI") { ShowUIWarning(player); }
            else if (config.displayNotify.showDamageMType == "Chat") { ShowWarningMessage(player); }
        }

        private void ShowWarningMessage(BasePlayer player)
        {
            if (player == null) return;
            if (!NotifyMCool.Contains(player.userID))
            {
                SendChatMessage(player, config.displayNotify.prefix + config.displayNotify.showDamageMTypeMessage);
                NotifyMCool.Add(player.userID);
                timer.In(10, () => { NotifyMCool.Remove(player.userID); });
            }
        }

        private void SendChatMessage(BasePlayer player, string message)
        {
            Player.Message(player, message, config.displayNotify.chatAvatar);
        }

        private const string newUI = "WarningUI";
        private void ShowUIWarning(BasePlayer player)
        {
            if (NotifyACool.Contains(player.userID)) { return; }

            CuiElementContainer container = new CuiElementContainer
            {
                {
                    new CuiElement
                    {
                        Name = newUI,
                        Parent = "Overlay",
                        Components = {
                            new CuiRawImageComponent
                            {
                                Png = GetImage(config.displayNotify.warningUI.WarningImgUrl),
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = config.displayNotify.warningUI.WarningAMin,
                                AnchorMax = config.displayNotify.warningUI.WarningAMax,
                            }
                        }
                    }
                }
            };

            CuiHelper.DestroyUi(player, newUI);
            NotifyACool.Add(player.userID);
            CuiHelper.AddUi(player, container);

            timer.In(10, () => RemoveUIWarning(player));

        }
        private void RemoveUIWarning(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, newUI);

            if (NotifyACool.Contains(player.userID)) NotifyACool.Remove(player.userID);
        }

        //Show Looting Warning UI
        private List<ulong> WarningLoot = new List<ulong>();
        private List<ulong> WarningMLoot = new List<ulong>();
        private const string wLootC = "WarningLoot";
        private void UIWarningLoot(BasePlayer player)
        {
            if (WarningLoot.Contains(player.userID)) { return; }

            CuiElementContainer container = new CuiElementContainer
            {
                {
                    new CuiElement
                    {
                        Name = wLootC,
                        Parent = "Overlay",
                        Components = {
                            new CuiRawImageComponent
                            {
                                Png = GetImage(config.displayNotify.lPUISetting.WarningImgUrl),

                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = config.displayNotify.lPUISetting.WarningAMin,
                                AnchorMax = config.displayNotify.lPUISetting.WarningAMax,

                            }
                        }
                    }
                }
            };

            CuiHelper.DestroyUi(player, wLootC);
            WarningLoot.Add(player.userID);
            CuiHelper.AddUi(player, container);

            timer.In(5, () => RemoveWarningLoot(player));
        }
        private void RemoveWarningLoot(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, wLootC);

            if (WarningLoot.Contains(player.userID)) WarningLoot.Remove(player.userID);
        }
        private void ShowWarningLoot(BasePlayer player)
        {
            if (player == null) return;
            if (!config.displayNotify.LPMessagesOn) return;

            if (config.displayNotify.LPType == "Both")
            {
                UIWarningLoot(player);
                ShowLootWarningMessage(player);
            }
            else if (config.displayNotify.LPType == "UI") { UIWarningLoot(player); }
            else if (config.displayNotify.LPType == "Chat") { ShowLootWarningMessage(player); }
        }
        private void ShowLootWarningMessage(BasePlayer player)
        {
            if (player == null) return;
            if (!WarningMLoot.Contains(player.userID))
            {
                SendChatMessage(player, config.displayNotify.prefix + config.displayNotify.LPChatMessage);
                WarningMLoot.Add(player.userID);
                timer.In(10, () => { WarningMLoot.Remove(player.userID); });
            }
        }

        //Colors Hexa
        public static string Color(string hexColor, float alpha)
        {
            if (hexColor.StartsWith("#"))
                hexColor = hexColor.TrimStart('#');

            int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
            int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
            int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);

            return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
        }
        private void ShowPVEUI(BasePlayer player)
        {
            if (!config.displayNotify.showPvEOverlay) return;

            CuiElementContainer container = new CuiElementContainer
                {
                    {
                        new CuiPanel
                        {
                        RectTransform = { AnchorMin = config.displayNotify.pUISetting.pepAMin, AnchorMax = config.displayNotify.pUISetting.pepAMax, OffsetMin = config.displayNotify.pUISetting.pepOMin, OffsetMax = config.displayNotify.pUISetting.pepOMax },
                        Image = { Color = "0.3 0.8 0.1 0.8", FadeIn = 1f }
                        },
                        "Hud", "PVEUI"
                    }
                };


            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = config.displayNotify.pUISetting.pepAMin, AnchorMax = config.displayNotify.pUISetting.pepAMax, OffsetMin = config.displayNotify.pUISetting.pepOMin, OffsetMax = config.displayNotify.pUISetting.pepOMax },
                Text = { Text = "PVE", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 13, Font = "robotocondensed-bold.ttf" },
                FadeOut = 1f
            }, "Hud", "textpve");

            RemovePVEPanel(player);
            CuiHelper.AddUi(player, container);
        }

        private void RemovePVEPanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "textpve");
            CuiHelper.DestroyUi(player, "PVEUI");

        }

        private void ShowPVPUi(BasePlayer player)
        {
            if (!config.displayNotify.showPvEOverlay) return;

            CuiElementContainer container = new CuiElementContainer
                {
                    {
                        new CuiPanel
                        {
                        RectTransform = { AnchorMin = config.displayNotify.pUISetting.pepAMin, AnchorMax = config.displayNotify.pUISetting.pepAMax, OffsetMin = config.displayNotify.pUISetting.pepOMin, OffsetMax = config.displayNotify.pUISetting.pepOMax },
                        Image = { Color = "0.8 0.2 0.2 0.8", FadeIn = 1f }
                        },
                        "Hud", "PVPUI"
                    }
                };

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = config.displayNotify.pUISetting.pepAMin, AnchorMax = config.displayNotify.pUISetting.pepAMax, OffsetMin = config.displayNotify.pUISetting.pepOMin, OffsetMax = config.displayNotify.pUISetting.pepOMax },
                Text = { Text = "PVP", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 13, Font = "robotocondensed-bold.ttf" },
                FadeOut = 1f
            }, "Hud", "textpvp");

            RemovePVPPanel(player);

            CuiHelper.AddUi(player, container);
        }
        private void RemovePVPPanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "textpvp");
            CuiHelper.DestroyUi(player, "PVPUI");

        }

        

        #endregion

        #region Schedules UI
        private void SimplePVEMainGui(BasePlayer player)
        {
            var container = new CuiElementContainer();
            /*container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.2 0.1960784 0.2 1" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-515.949 -619.479", OffsetMax = "515.941 -68.521" }
            }, "Overlay", "SimplePVECUI");
            */

            container.Add(new CuiElement
            {
                Name = "SimplePVECUI",
                Parent = "Overlay",
                Components =
                {
                    new CuiRawImageComponent { Png = GetImage("https://i.postimg.cc/SxC1PDyF/Untitled-design.png") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-515.949 -619.479", OffsetMax = "515.941 -68.521" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-515.939 -107.605", OffsetMax = "515.961 0" }
            }, "SimplePVECUI", "TopBarPanel");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.1372549 0.1529412 0.1372549 1" }, //0.1411765 0.1372549 0.1372549 1
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-515.963 -54.839", OffsetMax = "515.938 0.001" }
            }, "TopBarPanel", "TitlePanel");

            container.Add(new CuiElement
            {
                Name = "Title",
                Parent = "TitlePanel",
                Components = {
                    new CuiTextComponent { Text = "SimplePVE", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-257.589 -27.42", OffsetMax = "257.589 27.42" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "1 0 0 0.6666667", Command = "spvecui closeall" },
                Text = { Text = "X", Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-38.9 -42.42", OffsetMax = "-3.9 -12.42" }
            }, "TitlePanel", "CloseBtn");

            container.Add(new CuiButton
            {
                Button = { Color = "0.1294118 0.5333334 0.2196078 1", Command = "spvecui pverules" },
                Text = { Text = "PVE Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "103.851 7.5", OffsetMax = "336.149 42.5" }
            }, "TopBarPanel", "PVERules");

            container.Add(new CuiButton
            {
                Button = { Color = "0.1294118 0.5333334 0.2196078 1", Command = "spvecui pveschedules" },
                Text = { Text = "Schedules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "399.791 7.5", OffsetMax = "632.089 42.5" }
            }, "TopBarPanel", "Schedules");

            CuiHelper.DestroyUi(player, "SimplePVECUI");
            CuiHelper.AddUi(player, container);
        }

        private void PVERulesUI(BasePlayer player, PVERules rules)
        {
            var container = new CuiElementContainer();

            if (rules == PVERules.GRules)
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image = { Color = "1 1 1 0" },
                    RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-515.939 0.005", OffsetMax = "515.961 443.355" }
                }, "SimplePVECUI", "MainShit");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1372549 0.1529412 0.1372549 1", Command = "spvecui grules" },//0.1019608 0.09803922 0.1019608 1
                    Text = { Text = "General Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "1 -35", OffsetMax = "172.984 0" }
                }, "MainShit", "GeneralRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui prules" },//0.2941177 0.2941177 0.2941177 1
                    Text = { Text = "Player Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "172.988 -35", OffsetMax = "344.972 0" }
                }, "MainShit", "PlayerRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui nrules" },
                    Text = { Text = "NPC Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "344.968 -35", OffsetMax = "516.952 0" }
                }, "MainShit", "NPCRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui erules" },
                    Text = { Text = "Entity Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "516.948 -35", OffsetMax = "688.932 0" }
                }, "MainShit", "EntityRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui lrules" },
                    Text = { Text = "Loot Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "688.928 -35", OffsetMax = "860.912 0" }
                }, "MainShit", "LootRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui zrules" },
                    Text = { Text = "ZoneManager", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "860.908 -35", OffsetMax = "1032.892 0" }
                }, "MainShit", "ZoneManager");

                int yOffset = 0;
                int gapOffset = 0;
                int panelsPerColumn = 14; // Number of panels per column
                int panelsCount = 0;
                int columnCount = 0;
                int columnGap = 520; // Gap between columns
                foreach (var property in typeof(RulesData.GRules).GetProperties())
                {
                    string propertyName = property.Name;
                    var nProperty = property.GetCustomAttribute<JsonPropertyAttribute>();
                    string name = nProperty.PropertyName;//
                    object propertyValue = property.GetValue(rulesData.gRules);
                    container.Add(new CuiPanel
                    {
                        CursorEnabled = true,
                        Image = { Color =  ColorOdd(panelsCount)},//0.1019608 0.09803922 0.1019608 1
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{-0.005 + gapOffset} -{74.5 + yOffset}", OffsetMax = $"{511.505 + gapOffset} -{49.5 + yOffset}" }
                    }, "MainShit", $"Template_{propertyName}");
                    container.Add(new CuiElement
                    {
                        Name = $"Label_{propertyName}",
                        Parent = $"Template_{propertyName}",
                        Components = {
                        new CuiTextComponent { Text = $"{name}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                        new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "24.097 -12.5", OffsetMax = "512.211 12.5" }
                    }
                    });
                    container.Add(new CuiButton
                    {
                        Button = { Color = "0 0 0 0", Command = $"grulesSetting {propertyName}" },
                        Text = { Text = $"{propertyValue}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = (bool)propertyValue ? "0 0.5019608 0 1" : "0.5568628 0.1960784 0.1960784 1" },
                        RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-127.788 -12.5", OffsetMax = "0 12.5" }
                    }, $"Template_{propertyName}", $"State_{propertyName}");
                    yOffset += 28;
                    panelsCount++;
                    // Check if it's time to start a new column
                    if (panelsCount >= panelsPerColumn)
                    {
                        yOffset = 0;
                        panelsCount = 0;
                        columnCount++;
                        // Move to the next column with the specified gap
                        gapOffset += columnCount * columnGap;
                    }
                }

            }
            else if (rules == PVERules.PRules)
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image = { Color = "1 1 1 0" },
                    RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-515.939 0.005", OffsetMax = "515.961 443.355" }
                }, "SimplePVECUI", "MainShit");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui grules" },
                    Text = { Text = "General Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "1 -35", OffsetMax = "172.984 0" }
                }, "MainShit", "GeneralRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1372549 0.1529412 0.1372549 1", Command = "spvecui prules" },
                    Text = { Text = "Player Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "172.988 -35", OffsetMax = "344.972 0" }
                }, "MainShit", "PlayerRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui nrules" },
                    Text = { Text = "NPC Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "344.968 -35", OffsetMax = "516.952 0" }
                }, "MainShit", "NPCRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui erules" },
                    Text = { Text = "Entity Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "516.948 -35", OffsetMax = "688.932 0" }
                }, "MainShit", "EntityRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui lrules" },
                    Text = { Text = "Loot Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "688.928 -35", OffsetMax = "860.912 0" }
                }, "MainShit", "LootRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui zrules" },
                    Text = { Text = "ZoneManager", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "860.908 -35", OffsetMax = "1032.892 0" }
                }, "MainShit", "ZoneManager");

                int yOffset = 0;
                int gapOffset = 0;
                int panelsPerColumn = 14; // Number of panels per column
                int panelsCount = 0;
                int columnCount = 0;
                int columnGap = 520; // Gap between columns
                foreach (var property in typeof(RulesData.PlayerRules).GetProperties())
                {
                    string propertyName = property.Name;
                    var nProperty = property.GetCustomAttribute<JsonPropertyAttribute>();
                    string name = nProperty.PropertyName;
                    object propertyValue = property.GetValue(rulesData.playerRules);
                    container.Add(new CuiPanel
                    {
                        Image = { Color = ColorOdd(panelsCount) },
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{-0.005 + gapOffset} -{74.5 + yOffset}", OffsetMax = $"{511.505 + gapOffset} -{49.5 + yOffset}" }
                    }, "MainShit", $"Template_{propertyName}");
                    container.Add(new CuiElement
                    {
                        Name = $"Label_{propertyName}",
                        Parent = $"Template_{propertyName}",
                        Components = {
                        new CuiTextComponent { Text = $"{name}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                        new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "24.097 -12.5", OffsetMax = "512.211 12.5" }
                    }
                    });
                    container.Add(new CuiButton
                    {
                        Button = { Color = "0 0 0 0", Command = $"prulesSetting {propertyName}" },
                        Text = { Text = $"{propertyValue}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = (bool)propertyValue ? "0 0.5019608 0 1" : "0.5568628 0.1960784 0.1960784 1" },
                        RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-127.788 -12.5", OffsetMax = "0 12.5" }
                    }, $"Template_{propertyName}", $"State_{propertyName}");
                    yOffset += 28;
                    panelsCount++;
                    // Check if it's time to start a new column
                    if (panelsCount >= panelsPerColumn)
                    {
                        yOffset = 0;
                        panelsCount = 0;
                        columnCount++;
                        // Move to the next column with the specified gap
                        gapOffset += columnCount * columnGap;
                    }
                }

            }
            else if (rules == PVERules.NRules)
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image = { Color = "1 1 1 0" },
                    RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-515.939 0.005", OffsetMax = "515.961 443.355" }
                }, "SimplePVECUI", "MainShit");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui grules" },
                    Text = { Text = "General Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "1 -35", OffsetMax = "172.984 0" }
                }, "MainShit", "GeneralRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui prules" },
                    Text = { Text = "Player Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "172.988 -35", OffsetMax = "344.972 0" }
                }, "MainShit", "PlayerRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1372549 0.1529412 0.1372549 1", Command = "spvecui nrules" },
                    Text = { Text = "NPC Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "344.968 -35", OffsetMax = "516.952 0" }
                }, "MainShit", "NPCRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui erules" },
                    Text = { Text = "Entity Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "516.948 -35", OffsetMax = "688.932 0" }
                }, "MainShit", "EntityRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui lrules" },
                    Text = { Text = "Loot Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "688.928 -35", OffsetMax = "860.912 0" }
                }, "MainShit", "LootRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui zrules" },
                    Text = { Text = "ZoneManager", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "860.908 -35", OffsetMax = "1032.892 0" }
                }, "MainShit", "ZoneManager");

                int yOffset = 0;
                int gapOffset = 0;
                int panelsPerColumn = 14; // Number of panels per column
                int panelsCount = 0;
                int columnCount = 0;
                int columnGap = 520; // Gap between columns
                foreach (var property in typeof(RulesData.NpcRules).GetProperties())
                {
                    string propertyName = property.Name;
                    var nProperty = property.GetCustomAttribute<JsonPropertyAttribute>();
                    string name = nProperty.PropertyName;//
                    object propertyValue = property.GetValue(rulesData.npcRules);
                    container.Add(new CuiPanel
                    {
                        CursorEnabled = true,
                        Image = { Color = ColorOdd(panelsCount) },
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{-0.005 + gapOffset} -{74.5 + yOffset}", OffsetMax = $"{511.505 + gapOffset} -{49.5 + yOffset}" }
                    }, "MainShit", $"Template_{propertyName}");
                    container.Add(new CuiElement
                    {
                        Name = $"Label_{propertyName}",
                        Parent = $"Template_{propertyName}",
                        Components = {
        new CuiTextComponent { Text = $"{name}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
        new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "24.097 -12.5", OffsetMax = "512.211 12.5" }
    }
                    });
                    container.Add(new CuiButton
                    {
                        Button = { Color = "0 0 0 0", Command = $"nrulesSetting {propertyName}" },
                        Text = { Text = $"{propertyValue}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = (bool)propertyValue ? "0 0.5019608 0 1" : "0.5568628 0.1960784 0.1960784 1" },
                        RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-127.788 -12.5", OffsetMax = "0 12.5" }
                    }, $"Template_{propertyName}", $"State_{propertyName}");
                    yOffset += 28;
                    panelsCount++;
                    // Check if it's time to start a new column
                    if (panelsCount >= panelsPerColumn)
                    {
                        yOffset = 0;
                        panelsCount = 0;
                        columnCount++;
                        // Move to the next column with the specified gap
                        gapOffset += columnCount * columnGap;
                    }
                }

            }
            else if (rules == PVERules.ERules)
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image = { Color = "1 1 1 0" },
                    RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-515.939 0.005", OffsetMax = "515.961 443.355" }
                }, "SimplePVECUI", "MainShit");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui grules" },
                    Text = { Text = "General Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "1 -35", OffsetMax = "172.984 0" }
                }, "MainShit", "GeneralRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui prules" },
                    Text = { Text = "Player Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "172.988 -35", OffsetMax = "344.972 0" }
                }, "MainShit", "PlayerRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui nrules" },
                    Text = { Text = "NPC Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "344.968 -35", OffsetMax = "516.952 0" }
                }, "MainShit", "NPCRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1372549 0.1529412 0.1372549 1", Command = "spvecui erules" },
                    Text = { Text = "Entity Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "516.948 -35", OffsetMax = "688.932 0" }
                }, "MainShit", "EntityRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui lrules" },
                    Text = { Text = "Loot Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "688.928 -35", OffsetMax = "860.912 0" }
                }, "MainShit", "LootRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui zrules" },
                    Text = { Text = "ZoneManager", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "860.908 -35", OffsetMax = "1032.892 0" }
                }, "MainShit", "ZoneManager");

                int yOffset = 0;
                int gapOffset = 0;
                int panelsPerColumn = 14; // Number of panels per column
                int panelsCount = 0;
                int columnCount = 0;
                int columnGap = 520; // Gap between columns
                foreach (var property in typeof(RulesData.EntityRules).GetProperties())
                {
                    string propertyName = property.Name;
                    var nProperty = property.GetCustomAttribute<JsonPropertyAttribute>();
                    string name = nProperty.PropertyName;//
                    object propertyValue = property.GetValue(rulesData.entityRules);
                    container.Add(new CuiPanel
                    {
                        CursorEnabled = true,
                        Image = { Color = ColorOdd(panelsCount) },
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{-0.005 + gapOffset} -{74.5 + yOffset}", OffsetMax = $"{511.505 + gapOffset} -{49.5 + yOffset}" }
                    }, "MainShit", $"Template_{propertyName}");
                    container.Add(new CuiElement
                    {
                        Name = $"Label_{propertyName}",
                        Parent = $"Template_{propertyName}",
                        Components = {
        new CuiTextComponent { Text = $"{name}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
        new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "24.097 -12.5", OffsetMax = "512.211 12.5" }
    }
                    });
                    container.Add(new CuiButton
                    {
                        Button = { Color = "0 0 0 0", Command = $"erulesSetting {propertyName}" },
                        Text = { Text = $"{propertyValue}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = (bool)propertyValue ? "0 0.5019608 0 1" : "0.5568628 0.1960784 0.1960784 1" },
                        RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-127.788 -12.5", OffsetMax = "0 12.5" }
                    }, $"Template_{propertyName}", $"State_{propertyName}");
                    yOffset += 28;
                    panelsCount++;
                    // Check if it's time to start a new column
                    if (panelsCount >= panelsPerColumn)
                    {
                        yOffset = 0;
                        panelsCount = 0;
                        columnCount++;
                        // Move to the next column with the specified gap
                        gapOffset += columnCount * columnGap;
                    }
                }

            }
            else if (rules == PVERules.LRules)
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image = { Color = "1 1 1 0" },
                    RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-515.939 0.005", OffsetMax = "515.961 443.355" }
                }, "SimplePVECUI", "MainShit");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui grules" },
                    Text = { Text = "General Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "1 -35", OffsetMax = "172.984 0" }
                }, "MainShit", "GeneralRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui prules" },
                    Text = { Text = "Player Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "172.988 -35", OffsetMax = "344.972 0" }
                }, "MainShit", "PlayerRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui nrules" },
                    Text = { Text = "NPC Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "344.968 -35", OffsetMax = "516.952 0" }
                }, "MainShit", "NPCRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui erules" },
                    Text = { Text = "Entity Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "516.948 -35", OffsetMax = "688.932 0" }
                }, "MainShit", "EntityRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1372549 0.1529412 0.1372549 1", Command = "spvecui lrules" },
                    Text = { Text = "Loot Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "688.928 -35", OffsetMax = "860.912 0" }
                }, "MainShit", "LootRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui zrules" },
                    Text = { Text = "ZoneManager", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "860.908 -35", OffsetMax = "1032.892 0" }
                }, "MainShit", "ZoneManager");

                int yOffset = 0;
                int gapOffset = 0;
                int panelsPerColumn = 14; // Number of panels per column
                int panelsCount = 0;
                int columnCount = 0;
                int columnGap = 520; // Gap between columns
                foreach (var property in typeof(RulesData.LootRules).GetProperties())
                {
                    string propertyName = property.Name;
                    var nProperty = property.GetCustomAttribute<JsonPropertyAttribute>();
                    string name = nProperty.PropertyName;//
                    object propertyValue = property.GetValue(rulesData.lootRules);
                    container.Add(new CuiPanel
                    {
                        CursorEnabled = true,
                        Image = { Color = ColorOdd(panelsCount) },
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{-0.005 + gapOffset} -{74.5 + yOffset}", OffsetMax = $"{511.505 + gapOffset} -{49.5 + yOffset}" }
                    }, "MainShit", $"Template_{propertyName}");
                    container.Add(new CuiElement
                    {
                        Name = $"Label_{propertyName}",
                        Parent = $"Template_{propertyName}",
                        Components = {
        new CuiTextComponent { Text = $"{name}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
        new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "24.097 -12.5", OffsetMax = "512.211 12.5" }
    }
                    });
                    container.Add(new CuiButton
                    {
                        Button = { Color = "0 0 0 0", Command = $"lrulesSetting {propertyName}" },
                        Text = { Text = $"{propertyValue}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = (bool)propertyValue ? "0 0.5019608 0 1" : "0.5568628 0.1960784 0.1960784 1" },
                        RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-127.788 -12.5", OffsetMax = "0 12.5" }
                    }, $"Template_{propertyName}", $"State_{propertyName}");
                    yOffset += 28;
                    panelsCount++;
                    // Check if it's time to start a new column
                    if (panelsCount >= panelsPerColumn)
                    {
                        yOffset = 0;
                        panelsCount = 0;
                        columnCount++;
                        // Move to the next column with the specified gap
                        gapOffset += columnCount * columnGap;
                    }
                }

            }
            else if (rules == PVERules.ZRules)
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image = { Color = "1 1 1 0" },
                    RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-515.939 0.005", OffsetMax = "515.961 443.355" }
                }, "SimplePVECUI", "MainShit");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui grules" },
                    Text = { Text = "General Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "1 -35", OffsetMax = "172.984 0" }
                }, "MainShit", "GeneralRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui prules" },
                    Text = { Text = "Player Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "172.988 -35", OffsetMax = "344.972 0" }
                }, "MainShit", "PlayerRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui nrules" },
                    Text = { Text = "NPC Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "344.968 -35", OffsetMax = "516.952 0" }
                }, "MainShit", "NPCRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui erules" },
                    Text = { Text = "Entity Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "516.948 -35", OffsetMax = "688.932 0" }
                }, "MainShit", "EntityRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1215686 0.1372549 0.1215686 1", Command = "spvecui lrules" },
                    Text = { Text = "Loot Rules", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "688.928 -35", OffsetMax = "860.912 0" }
                }, "MainShit", "LootRules");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1372549 0.1529412 0.1372549 1", Command = "spvecui zrules" },
                    Text = { Text = "ZoneManager", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "860.908 -35", OffsetMax = "1032.892 0" }
                }, "MainShit", "ZoneManager");

                int yOffset = 0;
                int gapOffset = 0;
                int panelsPerColumn = 14; // Number of panels per column
                int panelsCount = 0;
                int columnCount = 0;
                int columnGap = 520; // Gap between columns
                foreach (var property in typeof(RulesData.ZoneRules).GetProperties())
                {
                    string propertyName = property.Name;
                    var nProperty = property.GetCustomAttribute<JsonPropertyAttribute>();
                    string name = nProperty.PropertyName;//
                    object propertyValue = property.GetValue(rulesData.zoneRules);
                    container.Add(new CuiPanel
                    {
                        CursorEnabled = true,
                        Image = { Color = ColorOdd(panelsCount) },
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{-0.005 + gapOffset} -{74.5 + yOffset}", OffsetMax = $"{511.505 + gapOffset} -{49.5 + yOffset}" }
                    }, "MainShit", $"Template_{propertyName}");
                    container.Add(new CuiElement
                    {
                        Name = $"Label_{propertyName}",
                        Parent = $"Template_{propertyName}",
                        Components = {
        new CuiTextComponent { Text = $"{name}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
        new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "24.097 -12.5", OffsetMax = "512.211 12.5" }
    }
                    });
                    container.Add(new CuiButton
                    {
                        Button = { Color = "0 0 0 0", Command = $"zrulesSetting {propertyName}" },
                        Text = { Text = $"{propertyValue}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = (bool)propertyValue ? "0 0.5019608 0 1" : "0.5568628 0.1960784 0.1960784 1" },
                        RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-127.788 -12.5", OffsetMax = "0 12.5" }
                    }, $"Template_{propertyName}", $"State_{propertyName}");
                    yOffset += 28;
                    panelsCount++;
                    // Check if it's time to start a new column
                    if (panelsCount >= panelsPerColumn)
                    {
                        yOffset = 0;
                        panelsCount = 0;
                        columnCount++;
                        // Move to the next column with the specified gap
                        gapOffset += columnCount * columnGap;
                    }
                }

            }

            CuiHelper.DestroyUi(player, "MainShit");
            CuiHelper.AddUi(player, container);
        }

        private string ColorOdd(int number)
        {
            if (number % 2 == 0)
            {
                return "0.1215686 0.1372549 0.1215686 1";
            }
            else
            {
                return "0.1372549 0.1529412 0.1372549 1";
            }
        }

        private void PVESchedules(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-515.939 0.005", OffsetMax = "515.961 443.355" }
            }, "SimplePVECUI", "MainShit");
            //time 
            var timenow = DateTime.Now;
            container.Add(new CuiButton
            {
                Button = { Color = "0 0.5019608 0 1", Command = $"sch selectdate {timenow.Month} {timenow.Year}" },  //create a schedules
                Text = { Text = "Create Schedule", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-502.618 175.67", OffsetMax = "-364.549 210.67" }
            }, "MainShit", "CreateSchBTN");

            //create enable days sch btn
            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-359.034 175.67", OffsetMax = "-220.965 210.67" }
            }, "MainShit", "EnableWeeklySCH");

            container.Add(new CuiButton
            {
                Button = { Color = config.sche.enableweeklysch ? "0 0.5019608 0 1" : "0.4588236 0.2 0.1764706 1", Command = "sch changetodays" },
                Text = { Text = "Enable Weekly Schedule", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-69.034 -17.5", OffsetMax = "69.036 17.5" }
            }, "EnableWeeklySCH", "Button_4654");

            //create a list of schedules here from data
            container.Add(new CuiElement
            {
                Name = "SchListTitle",
                Parent = "MainShit",
                Components = {
                    new CuiTextComponent { Text = "Schedules List", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-85.427 174.487", OffsetMax = "85.427 206.433" }
                }
            });

            if (savedSch.SchedulesTime.Count < 1)
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image = { Color = "0.1411765 0.1372549 0.1372549 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-201.04 -5.848", OffsetMax = "201.04 50" }
                }, "MainShit", "NotingFound");

                container.Add(new CuiElement
                {
                    Name = "Label_3872",
                    Parent = "NotingFound",
                    Components = {
                    new CuiTextComponent { Text = "No Schedules Found!", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "0.4588236 0.2 0.1764706 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-201.04 -27.925", OffsetMax = "201.04 27.924" }
                    }
                });
                CuiHelper.DestroyUi(player, "MainShit");
                CuiHelper.AddUi(player, container);
                return;
            }
            // Define variables to track the positioning
            int initialXOffset = -490; // Initial X offset for the first column
            int initialYOffset = 115; // Initial Y offset for the first column
            int xOffset = initialXOffset; // Current X offset
            int yOffset = initialYOffset; // Current Y offset
            int entrySpacing = 40; // Vertical spacing between entries
            int entriesPerColumn = 9; // Number of entries in a column
            int currentEntryCount = 0; // Initialize entry count

            foreach (string kvp in savedSch.SchedulesTime.Keys)
            {
                PVPTime value = savedSch.SchedulesTime[kvp];
                container.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image = { Color = "0.1411765 0.1372549 0.1372549 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{xOffset} {yOffset}", OffsetMax = $"{xOffset + 261} {yOffset + 36}" }
                }, "MainShit", $"Leftside_{kvp}");

                container.Add(new CuiElement
                {
                    Name = "SchsName",
                    Parent = $"Leftside_{kvp}",
                    Components = {
                    new CuiTextComponent { Text = $"{value.StartDate}", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-118.744 -18.051", OffsetMax = "-15.293 18.052" }
                }
                });
                container.Add(new CuiButton
                {
                    Button = { Color = "0 0.5019608 0 1", Command = $"sch edit {kvp}" },// edit command
                    Text = { Text = "Edit", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-15.293 -13", OffsetMax = "53.968 13" }
                }, $"Leftside_{kvp}", "EditBTN");
                container.Add(new CuiButton
                {
                    Button = { Color = "0.4588236 0.2 0.1764706 1", Command = $"sch removesch {kvp}" },
                    Text = { Text = "Delete", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "56.969 -13", OffsetMax = "126.23 13" }
                }, $"Leftside_{kvp}", "DelBTN");

                // Update Y offset for the next entry
                yOffset -= entrySpacing;
                currentEntryCount++;

                // Check if we need to start a new column
                if (currentEntryCount >= entriesPerColumn)
                {
                    xOffset += 290; // Move to the right for the next column
                    yOffset = initialYOffset; // Reset Y offset
                    currentEntryCount = 0; // Reset entry count for the new column
                }
            }

            CuiHelper.DestroyUi(player, "MainShit");
            CuiHelper.AddUi(player, container);
        }

        private void SelectDate(BasePlayer player, int month, int year, bool creating = true, string oldDate = "")
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-515.939 0.005", OffsetMax = "515.961 443.355" }
            }, "SimplePVECUI", "MainShit");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.2980392 0.2862745 0.2784314 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-237.463 -206.433", OffsetMax = "228.408 206.433" }
            }, "MainShit", "SelectDatePanel");
            container.Add(new CuiPanel
            {
                Image = { Color = "0.2039216 0.2078432 0.2117647 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-182.641 161.719", OffsetMax = "191.689 197.821" }
            }, "SelectDatePanel", "Back");
            string monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month);
            container.Add(new CuiElement
            {
                Name = $"{monthName}_",
                Parent = "SelectDatePanel",
                Components = {
                    new CuiTextComponent { Text = $"{monthName}", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-89.056 -45.383", OffsetMax = "4.527 -7.617" }
                }
            });
            container.Add(new CuiElement
            {
                Name = $"{year}_",
                Parent = "SelectDatePanel",
                Components = {
                    new CuiTextComponent { Text = $"{year}", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "4.527 -45.383", OffsetMax = "98.11 -7.617" }
                }
            });
            if (month > DateTime.Now.Month || year > DateTime.Now.Year)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = "1 1 1 0", Command = creating ? $"sch prevsch {month} {year}" : $"sch prevsch00 {month} {year} {creating} {oldDate}" },
                    Text = { Text = "<<", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-182.644 -45.546", OffsetMax = "-89.061 -7.78" }
                }, "SelectDatePanel", "Left");
            }
            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = creating ? $"sch nextsch {month} {year}" : $"sch nextsch00 {month} {year} {creating} {oldDate}" },
                Text = { Text = ">>", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "98.106 -45.383", OffsetMax = "191.689 -7.617" }
            }, "SelectDatePanel", "Right");
            var amountOfDays = DateTime.DaysInMonth(year, month);
            int x = 0, y = 0;
            for (int i = 1; i <= amountOfDays; i++)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = "0.1411765 0.1372549 0.1372549 1", Command = creating ? $"sch openday {i} {month} {year}" : $"sch changeenddate {i} {month} {year} {oldDate}" },
                    Text = { Text = $"{i}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = $"{-210.472 + (x * 65)} {-108.985 - (y * 60)}", OffsetMax = $"{-160.472 + (x * 65)} {-58.985 - (y * 60)}" }
                }, "SelectDatePanel", $"{i}_");
                x++;
                if (x >= 7)
                {
                    y++;
                    x = 0;
                }
            }

            CuiHelper.DestroyUi(player, "MainShit");
            CuiHelper.AddUi(player, container);

        }

        private void OpenSelectDate(BasePlayer player, int day, int month, int year, string oldDate)
        {
            savedSch.SchedulesTime[$"{oldDate}"].EndDate = $"{day}/{month}/{year}";
            DateTime date = DateTime.ParseExact(oldDate, "d/M/yyyy", null);
            // Extract day, month, and year components
            int d = date.Day;
            int m = date.Month;
            int y = date.Year;
            CreateSch(player, d.ToString(), m.ToString(), y.ToString());
        }

        private void CreateSch(BasePlayer player, string day, string month, string year, bool create = true)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-515.939 0.005", OffsetMax = "515.961 443.355" }
            }, "SimplePVECUI", "MainShit");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.2980392 0.2862745 0.2784314 1", Material = "assets/content/ui/uibackgroundblur.mat" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-172.592 -106.972", OffsetMax = "172.592 154.972" }
            }, "MainShit", "CreateCUI");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.2039216 0.2078431 0.2117647 1" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-172.589 -35.661", OffsetMax = "172.591 0.385" }
            }, "CreateCUI", "TopBar");

            container.Add(new CuiElement
            {
                Name = "Title",
                Parent = "TopBar",
                Components = {
                    new CuiTextComponent { Text = "Create A New Schedule", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-159.912 -13.851", OffsetMax = "19.712 13.85" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "0.1411765 0.1372549 0.1372549 1" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-166.45 -115.151", OffsetMax = "160.849 -79.049" }
            }, "CreateCUI", "StartTime");

            container.Add(new CuiElement
            {
                Name = "Label_8226",
                Parent = "StartTime",
                Components = {
                    new CuiTextComponent { Text = "Start Time :", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "0.355 -18.051", OffsetMax = "106.265 18.052" }
                }
            });
            /// start time
            /*container.Add(new CuiElement
            {
                Name = "Day",
                Parent = "StartTime",
                Components = {
                    new CuiTextComponent { Text = $"{savedSch.SchedulesTime[$"{day}/{month}/{year}"].StartMinute}M", Color = "0.2039216 0.2078432 0.2117647 1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "47.633 -13", OffsetMax = "131.567 13" }
                }
            });*/
            container.Add(new CuiElement
            {
                Name = "Day",
                Parent = "StartTime",
                Components = {
                    new CuiInputFieldComponent { Text = $"{savedSch.SchedulesTime[$"{day}/{month}/{year}"].StartMinute}M", Command = $"sch changesm {day} {month} {year}", Color = "0.2039216 0.2078432 0.2117647 1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, CharsLimit = 0, IsPassword = false },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "47.633 -13", OffsetMax = "131.567 13" }
                }
            });

            /*container.Add(new CuiElement
            {
                Name = "Year",
                Parent = "StartTime",
                Components = {
                    new CuiTextComponent { Text = $"{savedSch.SchedulesTime[$"{day}/{month}/{year}"].StartHour}H", Color = "0.2039216 0.2078432 0.2117647 1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.735 -13", OffsetMax = "22.809 13" }
                }
            });
            */
            container.Add(new CuiElement
            {
                Name = "Year",
                Parent = "StartTime",
                Components = {
                    new CuiInputFieldComponent { Text = $"{savedSch.SchedulesTime[$"{day}/{month}/{year}"].StartHour}H", Command = $"sch changesh {day} {month} {year}", Color = "0.2039216 0.2078432 0.2117647 1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, CharsLimit = 0, IsPassword = false },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.735 -13", OffsetMax = "22.809 13" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "0.1411765 0.1372549 0.1372549 1" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-166.45 -75.249", OffsetMax = "60.754 -39.147" }
            }, "CreateCUI", "StartDate");

            container.Add(new CuiElement
            {
                Name = "Label_8226",
                Parent = "StartDate",
                Components = {
                    new CuiTextComponent { Text = "Start Date :", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "0.355 -18.051", OffsetMax = "106.265 18.052" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Label_2552",
                Parent = "StartDate",
                Components = {
                    new CuiTextComponent { Text = $"{savedSch.SchedulesTime[$"{day}/{month}/{year}"].StartDate}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "0.001 -18.051", OffsetMax = "113.601 18.052" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "0.1411765 0.1372549 0.1372549 1" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-166.45 -154.751", OffsetMax = "160.85 -118.649" }
            }, "CreateCUI", "EndDate");

            container.Add(new CuiElement
            {
                Name = "Label_8226",
                Parent = "EndDate",
                Components = {
                    new CuiTextComponent { Text = "End Date :", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "0.355 -18.051", OffsetMax = "106.265 18.052" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.2039216 0.2078432 0.2117647 1", Command = $"sch selectdatefalse {month} {year} {day}" }, //select date command
                Text = { Text = "Select", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.5019608 0.5019608 0.5019608 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.385 -13", OffsetMax = "23.159 13" }
            }, "EndDate", "Button_369");

            container.Add(new CuiElement
            {
                Name = "EndDate",
                Parent = "EndDate",
                Components = {
                    new CuiTextComponent { Text = $"{savedSch.SchedulesTime[$"{day}/{month}/{year}"].EndDate}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "48.241 -18.051", OffsetMax = "157.453 18.051" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "0.1411765 0.1372549 0.1372549 1" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-166.45 -194.051", OffsetMax = "160.849 -157.949" }
            }, "CreateCUI", "EndTime");

            container.Add(new CuiElement
            {
                Name = "Label_8226",
                Parent = "EndTime",
                Components = {
                    new CuiTextComponent { Text = "End Time :", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "0.355 -18.051", OffsetMax = "106.265 18.052" }
                }
            });
            //time
            /*container.Add(new CuiElement
            {
                Name = "Day",
                Parent = "EndTime",
                Components = {
                    new CuiTextComponent { Text = $"{savedSch.SchedulesTime[$"{day}/{month}/{year}"].EndMinute}M",Color = "0.2039216 0.2078432 0.2117647 1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter},
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "47.633 -13", OffsetMax = "131.567 13" }
                }
            });*/
            container.Add(new CuiElement
            {
                Name = "Day",
                Parent = "EndTime",
                Components = {
                    new CuiInputFieldComponent { Text = $"{savedSch.SchedulesTime[$"{day}/{month}/{year}"].EndMinute}M", Command = $"sch changeem {day} {month} {year}", Color = "0.2039216 0.2078432 0.2117647 1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, CharsLimit = 0, IsPassword = false },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "47.633 -13", OffsetMax = "131.567 13" }
                }
            });
            /*container.Add(new CuiElement
            {
                Name = "Year",
                Parent = "EndTime",
                Components = {
                    new CuiTextComponent { Text = $"{savedSch.SchedulesTime[$"{day}/{month}/{year}"].EndHour}H", Color = "0.2039216 0.2078432 0.2117647 1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.735 -13", OffsetMax = "22.809 13" }
                }
            });*/
            container.Add(new CuiElement
            {
                Name = "Year",
                Parent = "EndTime",
                Components = {
                    new CuiInputFieldComponent { Text = $"{savedSch.SchedulesTime[$"{day}/{month}/{year}"].EndHour}H", Command = $"sch changeeh {day} {month} {year}", Color = "0.2039216 0.2078432 0.2117647 1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, CharsLimit = 0, IsPassword = false },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.735 -13", OffsetMax = "22.809 13" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.5528213 0.9528302 0.5568399 1", Command = "spvecui pveschedules" },
                Text = { Text = "Save", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "86.327 -126", OffsetMax = "166.872 -100" }
            }, "CreateCUI", "Create");

            CuiHelper.DestroyUi(player, "MainShit");
            CuiHelper.AddUi(player, container);
        }

        private enum Mode
        {
            PVE,
            PVP
        }

        private class ScheduleSave
        {
            public Dictionary<string, PVPTime> SchedulesTime = new Dictionary<string, PVPTime>();
        }

        private class PVPTime
        {
            public string StartDate;
            public int StartHour;
            public int StartMinute;
            public string EndDate;
            public int EndHour;
            public int EndMinute;
            public bool IsWeekDay;
        }

        private void LoadSchTime()
        {
            savedSch = Interface.Oxide.DataFileSystem.ReadObject<ScheduleSave>($"{Name}/SchedulesTime");
        }

        private void SaveSchTime()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/SchedulesTime", savedSch);
        }

        [ConsoleCommand("sch")]
        private void SchTimeCMD(ConsoleSystem.Arg args)
        {
            var player = args?.Player();

            string command = args.GetString(0);
            int time;

            switch (command)
            {
                case "openday":
                    if (savedSch.SchedulesTime.ContainsKey($"{args.GetString(1)}/{args.GetString(2)}/{args.GetString(3)}"))
                    {
                        CreateSch(player, args.GetString(1), args.GetString(2), args.GetString(3));
                        return;
                    }
                    savedSch.SchedulesTime.Add($"{args.GetString(1)}/{args.GetString(2)}/{args.GetString(3)}", new PVPTime { StartHour = 12, StartMinute = 30, EndHour = 23, EndMinute = 30, StartDate = $"{args.GetString(1)}/{args.GetString(2)}/{args.GetString(3)}", EndDate = $"{args.GetString(1)}/{args.GetString(2)}/{args.GetString(3)}", IsWeekDay = false });
                    CreateSch(player, args.GetString(1), args.GetString(2), args.GetString(3));
                    break;
                case "openweekday":
                    if (savedSch.SchedulesTime.ContainsKey($"{args.GetString(1)}"))
                    {
                        CreateSchForWeekDay(player, args.GetString(1));
                        return;
                    }
                    savedSch.SchedulesTime.Add($"{args.GetString(1)}", new PVPTime { StartHour = 12, StartMinute = 30, EndHour = 23, EndMinute = 30, StartDate = $"{args.GetString(1)}", EndDate = $"{args.GetString(1)}", IsWeekDay = true });
                    CreateSchForWeekDay(player, args.GetString(1));
                    break;
                case "selectdate":
                    if (!config.sche.enableweeklysch)
                    {
                        SelectDate(player, args.GetInt(1), args.GetInt(2));
                    }
                    else
                    {
                        OpenDaysMenu(player);
                    }
                    break;
                case "selectdatefalse":
                    SelectDate(player, args.GetInt(1), args.GetInt(2), false, $"{args.GetInt(3)}/{args.GetInt(1)}/{args.GetInt(2)}");
                    break;
                case "changeenddate":
                    OpenSelectDate(player, args.GetInt(1), args.GetInt(2), args.GetInt(3), args.GetString(4));
                    break;
                case "changesm":
                    if (!savedSch.SchedulesTime.ContainsKey($"{args.GetString(1)}/{args.GetString(2)}/{args.GetString(3)}")) return;
                    time = args.GetInt(4);
                    if (time > 59) time = 59;
                    if (time < 0) time = 0;
                    savedSch.SchedulesTime[$"{args.GetString(1)}/{args.GetString(2)}/{args.GetString(3)}"].StartMinute = time;
                    CreateSch(player, args.GetString(1), args.GetString(2), args.GetString(3));
                    break;
                case "changesh":
                    if (!savedSch.SchedulesTime.ContainsKey($"{args.GetString(1)}/{args.GetString(2)}/{args.GetString(3)}")) return;
                    time = args.GetInt(4);
                    if (time > 24) time = 24;
                    if (time < 0) time = 0;
                    savedSch.SchedulesTime[$"{args.GetString(1)}/{args.GetString(2)}/{args.GetString(3)}"].StartHour = time;
                    CreateSch(player, args.GetString(1), args.GetString(2), args.GetString(3));
                    break;
                case "changeem":
                    if (!savedSch.SchedulesTime.ContainsKey($"{args.GetString(1)}/{args.GetString(2)}/{args.GetString(3)}")) return;
                    time = args.GetInt(4);
                    if (time > 59) time = 59;
                    if (time < 0) time = 0;
                    savedSch.SchedulesTime[$"{args.GetString(1)}/{args.GetString(2)}/{args.GetString(3)}"].EndMinute = time;
                    CreateSch(player, args.GetString(1), args.GetString(2), args.GetString(3));
                    break;
                case "changeeh":
                    if (!savedSch.SchedulesTime.ContainsKey($"{args.GetString(1)}/{args.GetString(2)}/{args.GetString(3)}")) return;
                    time = args.GetInt(4);
                    if (time > 24) time = 24;
                    if (time < 0) time = 0;
                    savedSch.SchedulesTime[$"{args.GetString(1)}/{args.GetString(2)}/{args.GetString(3)}"].EndHour = time;
                    CreateSch(player, args.GetString(1), args.GetString(2), args.GetString(3));
                    break;
                case "removesch":
                    if (savedSch.SchedulesTime.ContainsKey(args.GetString(1)))
                    {
                        savedSch.SchedulesTime.Remove(args.GetString(1));
                    }
                    PVESchedules(player);
                    break;
                case "edit":
                    string dateTime = args.GetString(1);
                    if (dateTime.Contains("day"))
                    {
                        CreateSchForWeekDay(player, dateTime);
                        return;
                    }
                    DateTime date = DateTime.ParseExact(dateTime, "d/M/yyyy", null);
                    // Extract day, month, and year components
                    int d = date.Day;
                    int m = date.Month;
                    int y = date.Year;
                    CreateSch(player, d.ToString(), m.ToString(), y.ToString());
                    break;
                case "nextsch":
                    int month = args.GetInt(1), year = args.GetInt(2);
                    if (month >= 12)
                    {
                        SelectDate(player, 1, (year + 1));
                    }
                    else { SelectDate(player, (month + 1), year); }
                    break;
                case "prevsch":
                    int premonth = args.GetInt(1), preyear = args.GetInt(2);
                    if (premonth <= 1)
                    {
                        SelectDate(player, 12, (preyear - 1));
                    }
                    else { SelectDate(player, (premonth - 1), preyear); }
                    break;
                case "nextsch00":
                    int month00 = args.GetInt(1), year00 = args.GetInt(2);
                    if (month00 >= 12)
                    {
                        SelectDate(player, 1, (year00 + 1), args.GetBool(3), args.GetString(4));
                    }
                    else { SelectDate(player, (month00 + 1), year00, args.GetBool(3), args.GetString(4)); }
                    break;
                case "prevsch00":
                    int premonth00 = args.GetInt(1), preyear00 = args.GetInt(2);
                    if (premonth00 <= 1)
                    {
                        SelectDate(player, 12, (preyear00 - 1), args.GetBool(3), args.GetString(4));
                    }
                    else { SelectDate(player, (premonth00 - 1), preyear00, args.GetBool(3), args.GetString(4)); }
                    break;
                case "changetodays": //new schedules day
                    config.sche.enableweeklysch = !config.sche.enableweeklysch;
                    SaveConfig();
                    PVESchedules(player);
                    break;
                case "changesmday":
                    if (!savedSch.SchedulesTime.ContainsKey($"{args.GetString(1)}")) return;
                    time = args.GetInt(2);
                    if (time > 59) time = 59;
                    if (time < 0) time = 0;
                    savedSch.SchedulesTime[$"{args.GetString(1)}"].StartMinute = time;
                    CreateSchForWeekDay(player, args.GetString(1));
                    break;
                case "changeshday":
                    if (!savedSch.SchedulesTime.ContainsKey($"{args.GetString(1)}")) return;
                    time = args.GetInt(2);
                    if (time > 24) time = 24;
                    if (time < 0) time = 0;
                    savedSch.SchedulesTime[$"{args.GetString(1)}"].StartHour = time;
                    CreateSchForWeekDay(player, args.GetString(1));
                    break;
                case "changeemday":
                    if (!savedSch.SchedulesTime.ContainsKey($"{args.GetString(1)}")) return;
                    time = args.GetInt(2);
                    if (time > 59) time = 59;
                    if (time < 0) time = 0;
                    savedSch.SchedulesTime[$"{args.GetString(1)}"].EndMinute = time;
                    CreateSchForWeekDay(player, args.GetString(1));
                    break;
                case "changeehday":
                    if (!savedSch.SchedulesTime.ContainsKey($"{args.GetString(1)}")) return;
                    time = args.GetInt(2);
                    if (time > 24) time = 24;
                    if (time < 0) time = 0;
                    savedSch.SchedulesTime[$"{args.GetString(1)}"].EndHour = time;
                    CreateSchForWeekDay(player, args.GetString(1));
                    break;

            }

        }

        private void ShowPVPPurgeHud(BasePlayer player)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.7764707 0.2352941 0.1607843 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "452.616 -231.151", OffsetMax = "625.358 -205.603" }
            }, "Overlay", "PVPHud");

            container.Add(new CuiElement
            {
                Name = "Label_3928",
                Parent = "PVPHud",
                Components = {
                    new CuiTextComponent { Text = "PVP Purge In", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-83.942 -12.774", OffsetMax = "18.332 12.774" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "0.5490196 0.1843137 0.08627451 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "34.458 -12.774", OffsetMax = "86.371 12.774" }
            }, "PVPHud", "Panel_9108");

            container.Add(new CuiElement
            {
                Name = "Label_4112",
                Parent = "Panel_9108",
                Components = {
                    new CuiTextComponent { Text = "", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25.957 -12.774", OffsetMax = "25.956 12.774" }
                }
            });

            CuiHelper.DestroyUi(player, "PVPHud");
            CuiHelper.AddUi(player, container);
        }

        private void OpenDaysMenu(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.2196079 0.2196079 0.2196079 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-114.625 -200.549", OffsetMax = "114.625 154.971" }
            }, "MainShit", "DaysList");
            container.Add(new CuiPanel
            {
                Image = { Color = "0.2078431 0.2078431 0.2078431 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-114.626 136.061", OffsetMax = "114.624 177.759" }
            }, "DaysList", "TitleDay");
            container.Add(new CuiElement
            {
                Name = "Label_60",
                Parent = "TitleDay",
                Components = {
                    new CuiTextComponent { Text = "Select Day", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-103.341 -17.819", OffsetMax = "103.341 17.819" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.1333333 0.1333333 0.1333333 1", Command = "sch openweekday Sunday" },
                Text = { Text = "Sunday", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-52.6 81.702", OffsetMax = "47.4 120.77" }
            }, "DaysList", "Sunday");

            container.Add(new CuiButton
            {
                Button = { Color = "0.1333333 0.1333333 0.1333333 1", Command = "sch openweekday Monday" },
                Text = { Text = "Monday", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-52.6 42.634", OffsetMax = "47.4 81.702" }
            }, "DaysList", "Monday");

            container.Add(new CuiButton
            {
                Button = { Color = "0.1333333 0.1333333 0.1333333 1", Command = "sch openweekday Tuesday" },
                Text = { Text = "Tuesday", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-52.6 3.566", OffsetMax = "47.4 42.634" }
            }, "DaysList", "Tuesday");

            container.Add(new CuiButton
            {
                Button = { Color = "0.1333333 0.1333333 0.1333333 1", Command = "sch openweekday Wednesday" },
                Text = { Text = "Wednesday", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-52.6 -35.502", OffsetMax = "47.4 3.566" }
            }, "DaysList", "Wednesday");

            container.Add(new CuiButton
            {
                Button = { Color = "0.1333333 0.1333333 0.1333333 1", Command = "sch openweekday Thursday" },
                Text = { Text = "Thursday", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-52.6 -70.798", OffsetMax = "47.4 -31.73" }
            }, "DaysList", "Thurday");

            container.Add(new CuiButton
            {
                Button = { Color = "0.1333333 0.1333333 0.1333333 1", Command = "sch openweekday Friday" },
                Text = { Text = "Friday", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-52.6 -109.866", OffsetMax = "47.4 -70.798" }
            }, "DaysList", "Friday");

            container.Add(new CuiButton
            {
                Button = { Color = "0.1333333 0.1333333 0.1333333 1", Command = "sch openweekday Saturday" },
                Text = { Text = "Saturday", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-52.6 -148.934", OffsetMax = "47.4 -109.866" }
            }, "DaysList", "Saturday");

            CuiHelper.DestroyUi(player, "DaysList");
            CuiHelper.AddUi(player, container);
        }

        private void CreateSchForWeekDay(BasePlayer player, string day)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-515.939 0.005", OffsetMax = "515.961 443.355" }
            }, "SimplePVECUI", "MainShit");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.2980392 0.2862745 0.2784314 1", Material = "assets/content/ui/uibackgroundblur.mat" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-172.592 -106.972", OffsetMax = "172.592 154.972" }
            }, "MainShit", "CreateCUI");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.2039216 0.2078431 0.2117647 1" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-172.589 -35.661", OffsetMax = "172.591 0.385" }
            }, "CreateCUI", "TopBar");

            container.Add(new CuiElement
            {
                Name = "Title",
                Parent = "TopBar",
                Components = {
                    new CuiTextComponent { Text = "Create A New Schedule", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-159.912 -13.851", OffsetMax = "19.712 13.85" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "0.1411765 0.1372549 0.1372549 1" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-166.45 -115.151", OffsetMax = "160.849 -79.049" }
            }, "CreateCUI", "StartTime");

            container.Add(new CuiElement
            {
                Name = "Label_8226",
                Parent = "StartTime",
                Components = {
                    new CuiTextComponent { Text = "Start Time :", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "0.355 -18.051", OffsetMax = "106.265 18.052" }
                }
            });
            /// start time
            /*container.Add(new CuiElement
            {
                Name = "Day",
                Parent = "StartTime",
                Components = {
                    new CuiTextComponent { Text = $"{savedSch.SchedulesTime[$"{day}"].StartMinute}M", Color = "0.2039216 0.2078432 0.2117647 1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "47.633 -13", OffsetMax = "131.567 13" }
                }
            });*/
            container.Add(new CuiElement
            {
                Name = "Day",
                Parent = "StartTime",
                Components = {
                    new CuiInputFieldComponent { Text = $"{savedSch.SchedulesTime[$"{day}"].StartMinute}M", Command = $"sch changesmday {day}", Color = "0.2039216 0.2078432 0.2117647 1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, CharsLimit = 0, IsPassword = false },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "47.633 -13", OffsetMax = "131.567 13" }
                }
            });
            /*container.Add(new CuiElement
            {
                Name = "Year",
                Parent = "StartTime",
                Components = {
                    new CuiTextComponent { Text = $"{savedSch.SchedulesTime[$"{day}"].StartHour}H",Color = "0.2039216 0.2078432 0.2117647 1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.735 -13", OffsetMax = "22.809 13" }
                }
            });*/
            container.Add(new CuiElement
            {
                Name = "Year",
                Parent = "StartTime",
                Components = {
                    new CuiInputFieldComponent { Text = $"{savedSch.SchedulesTime[$"{day}"].StartHour}H", Command = $"sch changeshday {day}", Color = "0.2039216 0.2078432 0.2117647 1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, CharsLimit = 0, IsPassword = false },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.735 -13", OffsetMax = "22.809 13" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "0.1411765 0.1372549 0.1372549 1" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-166.45 -75.249", OffsetMax = "60.754 -39.147" }
            }, "CreateCUI", "StartDate");

            container.Add(new CuiElement
            {
                Name = "Label_8226",
                Parent = "StartDate",
                Components = {
                    new CuiTextComponent { Text = "Start Date :", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "0.355 -18.051", OffsetMax = "106.265 18.052" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Label_2552",
                Parent = "StartDate",
                Components = {
                    new CuiTextComponent { Text = $"{savedSch.SchedulesTime[$"{day}"].StartDate}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "0.001 -18.051", OffsetMax = "113.601 18.052" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "0.1411765 0.1372549 0.1372549 1" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-166.45 -154.751", OffsetMax = "160.85 -118.649" }
            }, "CreateCUI", "EndDate");

            container.Add(new CuiElement
            {
                Name = "Label_8226",
                Parent = "EndDate",
                Components = {
                    new CuiTextComponent { Text = "End Date :", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "0.355 -18.051", OffsetMax = "106.265 18.052" }
                }
            });

            /*container.Add(new CuiButton
            {
                Button = { Color = "0.2039216 0.2078432 0.2117647 1", Command = $"sch selectdatefalse {month} {year} {day}" }, //select date command
                Text = { Text = "Select", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.5019608 0.5019608 0.5019608 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.385 -13", OffsetMax = "23.159 13" }
            }, "EndDate", "Button_369");*/

            container.Add(new CuiElement
            {
                Name = "EndDate",
                Parent = "EndDate",
                Components = {
                    new CuiTextComponent { Text = $"{savedSch.SchedulesTime[$"{day}"].EndDate}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "48.241 -18.051", OffsetMax = "157.453 18.051" }
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "0.1411765 0.1372549 0.1372549 1" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-166.45 -194.051", OffsetMax = "160.849 -157.949" }
            }, "CreateCUI", "EndTime");

            container.Add(new CuiElement
            {
                Name = "Label_8226",
                Parent = "EndTime",
                Components = {
                    new CuiTextComponent { Text = "End Time :", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "0.355 -18.051", OffsetMax = "106.265 18.052" }
                }
            });
            //time
            /*container.Add(new CuiElement
            {
                Name = "Day",
                Parent = "EndTime",
                Components = {
                    new CuiTextComponent { Text = $"{savedSch.SchedulesTime[$"{day}"].EndMinute}M",Color = "0.2039216 0.2078432 0.2117647 1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter},
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "47.633 -13", OffsetMax = "131.567 13" }
                }
            });*/
            container.Add(new CuiElement
            {
                Name = "Day",
                Parent = "EndTime",
                Components = {
                    new CuiInputFieldComponent { Text = $"{savedSch.SchedulesTime[$"{day}"].EndMinute}M", Command = $"sch changeemday {day}", Color = "0.2039216 0.2078432 0.2117647 1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, CharsLimit = 0, IsPassword = false },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "47.633 -13", OffsetMax = "131.567 13" }
                }
            });
            /*container.Add(new CuiElement
            {
                Name = "Year",
                Parent = "EndTime",
                Components = {
                    new CuiTextComponent { Text = $"{savedSch.SchedulesTime[$"{day}"].EndHour}H", Color = "0.2039216 0.2078432 0.2117647 1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.735 -13", OffsetMax = "22.809 13" }
                }
            });*/
            container.Add(new CuiElement
            {
                Name = "Year",
                Parent = "EndTime",
                Components = {
                    new CuiInputFieldComponent { Text = $"{savedSch.SchedulesTime[$"{day}"].EndHour}H", Command = $"sch changeehday {day}", Color = "0.2039216 0.2078432 0.2117647 1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, CharsLimit = 0, IsPassword = false },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.735 -13", OffsetMax = "22.809 13" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.5528213 0.9528302 0.5568399 1", Command = "spvecui pveschedules" },
                Text = { Text = "Save", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "86.327 -126", OffsetMax = "166.872 -100" }
            }, "CreateCUI", "Create");

            CuiHelper.DestroyUi(player, "MainShit");
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Status UI

        private void ShowStatusUI(BasePlayer player, Configuration.Setting status, string message, int modifyCount, double timer = -1)
        {          
            if (config.statusUI.status)
            {
                StatusUI(player, message, status.statusColor, status.textColor, status.icon, status.iconColor, modifyCount);
            }
                
        }

        private Dictionary<ulong, ModifiersCheck> ModifiersCustom = new Dictionary<ulong, ModifiersCheck>();

        private class ModifiersCheck
        {
            public Dictionary<string, float> items = new Dictionary<string, float>();
        }

        private void StatusUI(BasePlayer player, string text, string StatusColor, string TextColor, string imageUrl, string imageColor, int modifyCount) //string text, string StatusColor, string TextColor, string imageUrl, string imageColor 
        {
            var container = new CuiElementContainer();

            double OffsetMinY = 100 + (modifyCount * 28);
            double OffsetMaxY = 126.12 + (modifyCount * 28);

            container.Add(new CuiPanel
            {
                CursorEnabled = false, //AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-208.115 99.14", OffsetMax = "-16.105 126.12" Anchor Middle - AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "431.885 -260.86", OffsetMax = "623.895 -233.88"
                Image = { Color = Color(StatusColor, 1), Material = "assets/content/ui/namefontmaterial.mat", Sprite = "assets/content/ui/ui.background.tile.psd" }, //28.99 
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = $"-208.115 {OffsetMinY}", OffsetMax = $"-16.105 {OffsetMaxY}" } //up AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-208.115 128.13", OffsetMax = "-16.105 155.11"
            }, "Hud", "StatusUI");

            container.Add(new CuiElement
            {
                Name = "Icon",
                Parent = "StatusUI",
                Components = {
                    new CuiRawImageComponent { Color = Color(imageColor, 1), Png = GetImage(imageUrl) },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-91 -7.5", OffsetMax = "-76 7.5" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Label_9402",
                Parent = "StatusUI",
                Components = {
                    new CuiTextComponent { Text = text, Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = Color(TextColor, 1) },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-69.848 -11.285", OffsetMax = "93.003 11.285" }
                }
            });

            CuiHelper.DestroyUi(player, "StatusUI");
            CuiHelper.AddUi(player, container);
        }

        private void DestroyStatusUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "StatusUI");
        }

        private void DestroyStatsUIServer()
        {
            foreach(var player in BasePlayer.activePlayerList)
            {
                DestroyStatusUI(player);
            }
        }

        private string FormatTime(double time)
        {
            TimeSpan dateDifference = TimeSpan.FromSeconds(time);

            if (dateDifference.Days > 0)
                return $"{dateDifference.Days}D:{dateDifference.Hours:00}H";

            if (dateDifference.Hours > 0)
                return $"{dateDifference.Hours}H:{dateDifference.Minutes:00}M";

            if (dateDifference.Minutes > 0)
                return $"{dateDifference.Minutes}M:{dateDifference.Seconds:00}S";

            return $"{dateDifference.Seconds}S";
        }
        #endregion

        #endregion

        #region Schedules Timer
        private DateTime Time => DateTime.UtcNow.AddMinutes(config.sche.utcTimeDif);

        private IEnumerator Schedules()
        {
            //TimeSpan statusBarInterval = TimeSpan.FromSeconds(config.statusUI.interval);
            //DateTime nextStatusBarTime = Time.Add(statusBarInterval)/

            while (update)
            {
                if (config.discordSetting.enableDiscordNotify) SendMessage();

                // Check if it's time to show the status bar
                /*if (config.statusUI.status && !UseSimpleStatus)
                {
                    if (Time >= nextStatusBarTime)
                    {
                        ShowStatusBar();
                        nextStatusBarTime = Time.Add(statusBarInterval);
                    }
                }*/

                if (IsItTime())
                {
                    if (PVPRun)
                    {
                        yield return new WaitForSeconds(5f);
                        continue;
                    }
                    
                    PVPRun = true;
                    PrintWarning("Raid time has begun!");
                    EnablePVP();
                    //ShowStatusBar();
                    DiscordMessageSent = false;
                }
                else
                {                   
                    if (!PVPRun)
                    {
                        yield return new WaitForSeconds(5f);
                        continue;
                    }
                    PVPRun = false;
                    EnablePVE();
                    //ShowStatusBar();
                    DiscordMessageSent = false;
                }

                // Sleep for a while before checking again
                yield return new WaitForSeconds(5f);
            }

        }

        private bool IsItTime()
        {
            DateTime currentTime = Time;

            foreach (var kvp in savedSch.SchedulesTime)
            {
                PVPTime schedule = kvp.Value;

                if (config.sche.enableweeklysch)
                {
                    if (schedule.IsWeekDay)
                    {
                        if (currentTime.DayOfWeek == ParseDayOfWeek(schedule.StartDate))
                        {
                            DateTime startTimeDay = currentTime.Date.AddHours(schedule.StartHour).AddMinutes(schedule.StartMinute);
                            DateTime endTimeDay = currentTime.Date.AddHours(schedule.EndHour).AddMinutes(schedule.EndMinute);

                            //Puts($"{currentTime} >= {startTimeDay} + {currentTime} <= {endTimeDay}");
                            if (currentTime >= startTimeDay && currentTime <= endTimeDay)
                            {
                                //Puts("Its true");
                                return true;
                            }
                        }
                    }                                      
                }
                else
                {
                    if (!schedule.IsWeekDay)
                    {
                        // Parse the start and end dates and times
                        DateTime startDate = DateTime.ParseExact(schedule.StartDate, "d/M/yyyy", null);
                        DateTime endDate = DateTime.ParseExact(schedule.EndDate, "d/M/yyyy", null);
                        DateTime startTime = startDate.AddHours(schedule.StartHour).AddMinutes(schedule.StartMinute);
                        DateTime endTime = endDate.AddHours(schedule.EndHour).AddMinutes(schedule.EndMinute);

                        // Check if the current time is within the schedule
                        if (currentTime >= startTime && currentTime <= endTime)
                        {
                            return true;
                        }
                    }                  
                }
                
            }
            return false;
        }

        private DayOfWeek ParseDayOfWeek(string dayOfWeek)
        {
            switch (dayOfWeek.ToLower())
            {
                case "sunday":
                    return DayOfWeek.Sunday;
                case "monday":
                    return DayOfWeek.Monday;
                case "tuesday":
                    return DayOfWeek.Tuesday;
                case "wednesday":
                    return DayOfWeek.Wednesday;
                case "thursday":
                    return DayOfWeek.Thursday;
                case "friday":
                    return DayOfWeek.Friday;
                case "saturday":
                    return DayOfWeek.Saturday;
                default:
                    throw new ArgumentException("Invalid day of the week: " + dayOfWeek);
            }
        }

        private TimeSpan CalculateTimeDifference()
        {
            DateTime currentTime = Time;

            foreach (var kvp in savedSch.SchedulesTime)
            {
                PVPTime schedule = kvp.Value;

                if (config.sche.enableweeklysch)
                {
                    if (schedule.IsWeekDay)
                    {
                        DayOfWeek scheduleDay = ParseDayOfWeek(schedule.StartDate);
                        DateTime startTimeDay = currentTime.Date.AddHours(schedule.StartHour).AddMinutes(schedule.StartMinute);

                        if (currentTime.DayOfWeek == scheduleDay)
                        {
                            TimeSpan timeDifferenceDay = startTimeDay - currentTime;
                            return timeDifferenceDay;
                        }
                        else
                        {
                            int daysUntilNextOccurrence = ((int)scheduleDay - (int)currentTime.DayOfWeek + 7) % 7;
                            if (daysUntilNextOccurrence == 0) daysUntilNextOccurrence = 7;

                            DateTime nextOccurrence = currentTime.AddDays(daysUntilNextOccurrence).Date.AddHours(schedule.StartHour).AddMinutes(schedule.StartMinute);
                            TimeSpan timeDifferenceNextOccurrence = nextOccurrence - currentTime;

                            return timeDifferenceNextOccurrence;
                        }
                    }
                    
                }
                else
                {
                    if (!schedule.IsWeekDay)
                    {
                        DateTime startDate = DateTime.ParseExact(schedule.StartDate, "d/M/yyyy", null);
                        DateTime startTime = startDate.AddHours(schedule.StartHour).AddMinutes(schedule.StartMinute);

                        // Calculate time difference
                        TimeSpan timeDifference = startTime - currentTime;

                        return timeDifference;
                    }                    
                }               
            }

            // Return a default TimeSpan if no valid schedule is found
            return TimeSpan.Zero;
        }

        private TimeSpan CalculateTimeDifferenceToEnd()
        {
            DateTime currentTime = Time;

            foreach (var kvp in savedSch.SchedulesTime)
            {
                PVPTime schedule = kvp.Value;

                if (config.sche.enableweeklysch)
                {
                    if (schedule.IsWeekDay)
                    {
                        if (currentTime.DayOfWeek == ParseDayOfWeek(schedule.EndDate))
                        {
                            DateTime endTimeDay = currentTime.Date.AddHours(schedule.EndHour).AddMinutes(schedule.EndMinute);

                            TimeSpan timeDifferenceDay = endTimeDay - currentTime;

                            if (timeDifferenceDay.TotalMinutes > 0)
                            {
                                return timeDifferenceDay;
                            }
                        }
                    }                   
                }
                else
                {
                    if (!schedule.IsWeekDay)
                    {
                        DateTime endDate = DateTime.ParseExact(schedule.EndDate, "d/M/yyyy", null);
                        DateTime endTime = endDate.AddHours(schedule.EndHour).AddMinutes(schedule.EndMinute);

                        // Calculate time difference to the end time
                        TimeSpan timeDifferenceToEnd = endTime - currentTime;

                        // Check if the scheduled end time is in the future
                        if (timeDifferenceToEnd.TotalMinutes > 0)
                        {
                            return timeDifferenceToEnd;
                        }
                    }                    
                }                
            }

            // Return a default TimeSpan if no valid schedule is found
            return TimeSpan.Zero;
        }

        private void EnablePVP()
        {
            config.pveEnabledConfig = false;
            SaveConfig();
            NotSubscribe();
            foreach (var player in BasePlayer.activePlayerList)
            {
                RemovePVEPanel(player);
                RemovePVPPanel(player);
                ShowPVPUi(player);
                player.ChatMessage(config.displayNotify.prefix + config.sche.pvpOnMessage);
            }
            //execute commands from list
            foreach (var command in config.commandsExefrompvpstart)
            {
                Server.Command(command);
            }               
            Interface.CallHook("OnSPVEPurgeStarted");
        }
        private void EnablePVE()
        {
            config.pveEnabledConfig = true;
            SaveConfig();
            Subscribe();
            foreach (var player in BasePlayer.activePlayerList)
            {
                RemovePVPPanel(player);
                RemovePVEPanel(player);
                ShowPVEUI(player);
                player.ChatMessage(config.displayNotify.prefix + config.sche.pveOnMessage);
            }
            //execute commands from list
            foreach (var command in config.commandsExefrompvpends)
            {
                Server.Command(command);
            }
            Interface.CallHook("OnSPVEPurgeEnded");
        }

        private void SendPVPStartMessage()
        {
            if (config.discordSetting.enableDiscordNotify && config.discordSetting.discordWebhookURL != null && config.discordSetting.messageBeforeStart)
            {
                string message = string.Join("\n", config.discordSetting.pvpTimeMessage);
                SendDiscordMessage(config.discordSetting.discordWebhookURL, "", new List<string> { message.Replace("{Minutes}", config.discordSetting.messageBeforeStartMinutes.ToString()) }, config.discordSetting.pvpmessageEmbed,false);
            }
        }
        private void SendPVPEndMessage()
        {
            if (config.discordSetting.enableDiscordNotify && config.discordSetting.discordWebhookURL != null && config.discordSetting.messageBeforeStartPVE)
            {
                string message = string.Join("\n", config.discordSetting.pveTimeMessage);
                SendDiscordMessage(config.discordSetting.discordWebhookURL, "", new List<string> { message.Replace("{Minutes}", config.discordSetting.messageBeforeStartMinutesPVE.ToString()) }, config.discordSetting.pvemessageEmbed,false);
            }
        }
        
        private void SendMessage()
        {
            if (DiscordMessageSent)
                return;

            if (config.discordSetting.enableDiscordNotify && config.discordSetting.discordWebhookURL != null)
            {
                TimeSpan timeDifference = CalculateTimeDifference();
                TimeSpan timeDifferenceEnd = CalculateTimeDifferenceToEnd();

                if (Math.Abs(timeDifference.TotalMinutes - config.discordSetting.messageBeforeStartMinutes) < 0.1)
                {
                    Puts($"PVP time is approaching! {Math.Round(timeDifference.TotalMinutes)} minutes left.");
                    Broadcast(config.sche.pvppurgeStartMsg.Replace("{TIMELEFT}", Math.Round(timeDifference.TotalMinutes).ToString()));
                    SendPVPStartMessage();
                    DiscordMessageSent = true;
                }
                else if (Math.Abs(timeDifferenceEnd.TotalMinutes - config.discordSetting.messageBeforeStartMinutesPVE) < 0.1)
                {
                    Puts($"PVP time is ending! {Math.Round(timeDifferenceEnd.TotalMinutes)} minutes left.");
                    Broadcast(config.sche.pvppurgeEndingMsg.Replace("{TIMELEFT}", Math.Round(timeDifferenceEnd.TotalMinutes).ToString()));
                    SendPVPEndMessage();
                    DiscordMessageSent = true;
                }
            }

        }

        private void Broadcast(string Message)
        {
            foreach(var player in BasePlayer.activePlayerList)
            {
                if (player != null)
                    SendChatMessage(player, Message);
            }
        }

        #endregion

        #region Zone Manager 

        private List<string> GetAllZones(BaseEntity entity)
        {
            if (!rulesData.zoneRules.zoneManager || entity == null) return null;

            List<string> locations = new List<string>();

            List<string> zmloc = new List<string>();
            string zname;

            if (entity is BasePlayer player)
            {
                string[] zmlocplr = (string[])ZoneManager.Call("GetPlayerZoneIDs", new object[] { player });
                foreach (string s in zmlocplr)
                {
                    zmloc.Add(s);
                }
            }
            else if (entity.IsValid())
            {
                string[] zmlocent = (string[])ZoneManager.Call("GetEntityZoneIDs", new object[] { entity });
                foreach (string s in zmlocent)
                {
                    zmloc.Add(s);
                }
            }
                       
            if (zmloc != null && zmloc.Count > 0)
            {
                foreach (string s in zmloc)
                {
                    locations.Add(s);
                    zname = (string)ZoneManager.Call("GetZoneName", s);
                    if (zname != null) locations.Add(zname);
                }
            }

            foreach (var zones in config.excludedZoneIDs)
            {
                if (ZoneManager.Call("CheckZoneID", zones) != null)
                {
                    zname = (string)ZoneManager.Call("GetZoneName", zones);

                    if (zname != null)
                    {
                        locations.RemoveAll(x => x == zones || x == zname);
                    }
                }
            }

            return locations;

        }

        private bool PlayerInPVPZone(BasePlayer player)
        {
            string[] playerZoneINRightNow = (string[])ZoneManager.Call("GetPlayerZoneIDs", new object[] { player });

            if (playerZoneINRightNow == null || playerZoneINRightNow.Length == 0)
            {
                return false;
            }

            foreach (string zone in playerZoneINRightNow)
            {
                if (!config.excludedZoneIDs.Contains(zone))
                {
                    return true;
                }
            }

            return false;
        }

        private bool PlayersIsInZone(BasePlayer player)
        {
            string[] playerZoneINRightNow = (string[])ZoneManager.Call("GetPlayerZoneIDs", new object[] { player });

            if (playerZoneINRightNow != null && playerZoneINRightNow.Length > 0)
            {
                return true;
            }

            return false;
        }

        private void OnEnterZone(string zoneID, BasePlayer player)
        {
            RemovePVPDelay(player.userID);

            if (PlayersIsInZone(player) && rulesData.zoneRules.disableRulesZone && rulesData.zoneRules.zoneManager && config.pveEnabledConfig && !config.excludedZoneIDs.Contains(zoneID))
            {
                RemovePVEPanel(player);
                NextTick(() => ShowPVPUi(player));
            }
        }

        private void OnExitZone(string ZoneID, BasePlayer player) // Called when a player leaves a zone
        {
            if (!config.pveEnabledConfig)
            {
                RemovePVPPanel(player);
                RemovePVEPanel(player);
                NextTick(() => ShowPVPUi(player));
                return;
            }

            var delay = GetToZone(player, ZoneID);

            int delayTime = rulesData.zoneRules.enablePVPDelay ? config.delaySetting.pvpdelaytime : 0;

            if (rulesData.zoneRules.enablePVPDelay && config.delaySetting.pvpdelaytime > 0 && config.delaySetting.pvpDelayMsgEnabled)
            {
                SendChatMessage(player, config.displayNotify.prefix + config.delaySetting.pvpDelayMessage.Replace("{delay}", config.delaySetting.pvpdelaytime.ToString()));
            }

            //if (config.zoneRules.enablePVPDelay && delayTime > 0) ShowPVPUi(player);

            delay.DelayTimer = timer.Once(delayTime, () =>
            {
                inPVPBase.Remove(player.userID);

                RemovePVPDelay(player.userID);

                if (config.pveEnabledConfig)
                {
                    RemovePVPPanel(player); 
                    NextTick(() => ShowPVEUI(player));
                }
                else
                {
                    RemovePVEPanel(player);
                    NextTick(() => ShowPVPUi(player));
                }
            });
        }

        //Raidable bases exclude
        private HashSet<ulong> inRaidableBasesZone = new HashSet<ulong>();
        private HashSet<ulong> inPVPBase = new HashSet<ulong>();

        private void OnPlayerEnteredRaidableBase(BasePlayer player, Vector3 raidPos, bool allowPVP, int mode)
        {
            inRaidableBasesZone.Add(player.userID);

            RemovePVEPanel(player);
            RemovePVPPanel(player);
        }

        private void OnPlayerExitedRaidableBase(BasePlayer player, Vector3 raidPos, bool allowPVP, int mode)
        {
            inRaidableBasesZone.Remove(player.userID);

            if (!allowPVP)
            {
                if (config.pveEnabledConfig)
                {
                    ShowPVEUI(player);
                }
                else
                {
                    ShowPVPUi(player);
                }
            }           
        }

        private void OnPlayerPvpDelayExpired(BasePlayer target)
        {
            if (config.pveEnabledConfig)
            {
                ShowPVEUI(target);
            }
            else
            {
                ShowPVPUi(target);
            }
        }
        /*private void OnPlayerEnteredRaidableBase(BasePlayer player, Vector3 raidPos, bool allowPVP, int mode)
        {
            inRaidableBasesZone.Add(player.userID);
            if (allowPVP)
            {
                Puts("added to pvp base");
                inPVPBase.Add(player.userID);
                RemovePVPDelay(player.userID);
            }

            RemovePVEPanel(player);
            RemovePVPPanel(player);
        }

        private void OnPlayerExitedRaidableBase(BasePlayer player, Vector3 raidPos, bool allowPVP, int mode)
        {
            inRaidableBasesZone.Remove(player.userID);

            if (allowPVP)
            {
                Puts("remove from pvp base");
                OnExitZone(raidPos.ToString(), player);                 
            }
            else
            {
                if (config.pveEnabledConfig)
                {
                    ShowPVEUI(player);
                }
                else
                {
                    ShowPVPUi(player);
                }
            }
        }*/

        private PVPDelay GetToZone(BasePlayer player, string ZoneID)
        {
            PVPDelay Delay;
            if (pvp_delays.TryGetValue(player.userID, out Delay))
            {
                Delay.DelayTimer?.Destroy();
            }
            else
            {
                Delay = new PVPDelay();
                pvp_delays.Add(player.userID, Delay);
            }
            Delay.ZoneID = ZoneID;

            return Delay;
        }

        private void RemovePVPDelay(ulong userID)
        {
            if (pvp_delays.TryGetValue(userID, out PVPDelay Delay))
            {
                Delay.DelayTimer?.Destroy();
                pvp_delays.Remove(userID);
            }
        }

        private bool IsSafeZone(string zoneID)
        {
            Dictionary<string, string> fields = (Dictionary<string, string>)ZoneManager.Call("ZoneFieldList", zoneID);

            if (fields != null && fields.ContainsKey("safezone"))
            {
                string safezoneValue = fields["safezone"];
                bool safezoneBoolValue;
                if (bool.TryParse(safezoneValue, out safezoneBoolValue))
                {
                    return safezoneBoolValue;
                }
            }

            return false;
        }

        #endregion

        #region HOOKS

        private void CanPickupEntity(BasePlayer basePlayer, BaseEntity entity)
        {
            if (!config.statusUI.status) return;

            if (basePlayer == null || entity == null) { return; }
            var name = entity?.name;
            NextTick(() =>
            {
                if (basePlayer != null && name != null)
                {
                    ModifiersCustom.GetValueOrDefault(basePlayer.userID)?.items.CheckForTime(name);
                }
            });
        }

        private void OnStructureUpgrade(BaseCombatEntity entity, BasePlayer basePlayer, BuildingGrade.Enum grade)
        {
            if (!config.statusUI.status) return;

            if (basePlayer == null) { return; }
            ModifiersCustom.GetValueOrDefault(basePlayer.userID)?.items.CheckForTime($"removed {grade}");
        }

        private void OnStructureRepair(BuildingBlock entity, BasePlayer basePlayer)
        {
            if (!config.statusUI.status) return;

            NextTick(() =>
            {
                if (entity != null && basePlayer != null)
                {
                    ModifiersCustom.GetValueOrDefault(basePlayer.userID)?.items.CheckForTime($"removed {entity.grade}");
                }
            });
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BasePlayer basePlayer, Item item)
        {
            if (!config.statusUI.status) return;

            if (basePlayer == null || item == null) { return; }
            ModifiersCustom.GetValueOrDefault(basePlayer.userID)?.items.CheckForTime(item.info.shortname);
        }

        private void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer basePlayer)
        {
            if (!config.statusUI.status) return;

            if (basePlayer == null) { return; }
            collectible.itemList.ForEach(item =>
            {
                ModifiersCustom.GetValueOrDefault(basePlayer.userID)?.items.CheckForTime(item.itemDef.shortname);
            });
        }

        ////
        private object OnCodeEntered(CodeLock codeLock, BasePlayer player, string code)
        {
            if (rulesData.gRules.aCodelockRaid) 
                return null;

            if (!IsAlliedWithPlayer(codeLock.GetParentEntity().OwnerID, player.userID))
            {
                SendChatMessage(player, "You are not in a team with the player.");
                return true;
            }

            return null;
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId())
                return;

            RemovePVPDelay(player.userID);
        }

        //Mlrs damages disable or enable
        private void OnEntitySpawned(MLRSRocket rocket)
        {
            if (rocket.IsRealNull()) return;

            // Find all MLRS systems in the vicinity
            var systems = FindEntitiesOfType<MLRS>(rocket.transform.position, 15f);

            if (systems.Count == 0 || CheckIsEventTerritory(systems[0].TrueHitPos)) return;

            // Get the owner of the MLRS system
            var owner = systems[0].rocketOwnerRef.Get(true) as BasePlayer;

            if (owner.IsRealNull()) return;

            // Check if the MLRS rocket's owner is the owner of entities within a certain radius
            DamageEntitiesAroundOwner(rocket, owner);
        }

        private void DamageEntitiesAroundOwner(MLRSRocket rocket, BasePlayer owner)
        {
            // Find all entities within a certain radius of the MLRS rocket
            var entities = FindEntitiesInRadius<BaseEntity>(rocket.transform.position, 15f);

            foreach (var entity in entities)
            {
                // Check if the entity is owned by the MLRS rocket's owner
                if ((entity.OwnerID > 0 || entity.OwnerID == owner.userID || IsAlliedWithPlayer(entity.OwnerID, owner.userID)) && !rulesData.entityRules.MlrsHurtPB)
                {
                    // Allow the MLRS rocket to damage this entity
                    // Perform any additional damage calculations or logic here
                    rocket.SetDamageScale(0f);
                    rocket.damageTypes.Clear();
                }
                else
                {

                    // Prevent the MLRS rocket from damaging this entity
                    // You may want to cancel the damage event or take appropriate action here
                }
            }
        }

        private static List<T> FindEntitiesOfType<T>(Vector3 a, float n, int m = -1) where T : BaseEntity
        {
            int hits = UnityEngine.Physics.OverlapSphereNonAlloc(a, n, Vis.colBuffer, m, QueryTriggerInteraction.Collide);
            List<T> entities = new List<T>();
            for (int i = 0; i < hits; i++)
            {
                var entity = Vis.colBuffer[i]?.ToBaseEntity();
                if (entity is T) entities.Add(entity as T);
                Vis.colBuffer[i] = null;
            }
            return entities;
        }

        private static List<T> FindEntitiesInRadius<T>(Vector3 position, float radius, int layerMask = -1) where T : BaseEntity
        {
            Collider[] colliders = UnityEngine.Physics.OverlapSphere(position, radius, layerMask, QueryTriggerInteraction.Collide);
            List<T> entities = new List<T>();

            foreach (var collider in colliders)
            {
                var entity = collider.GetComponentInParent<T>();
                if (entity != null)
                {
                    entities.Add(entity);
                }
            }

            return entities;
        }

        private bool CheckIsEventTerritory(Vector3 position)
        {
            if (AbandonedBases != null && Convert.ToBoolean(AbandonedBases?.Call("EventTerritory", position))) return true;
            if (RaidableBases != null && Convert.ToBoolean(RaidableBases?.Call("EventTerritory", position))) return true;
            return false;
        }


        //bear & landmine trap ignores if true
        private object OnTrapTrigger(BaseTrap t, GameObject g)
        {
            var p = g.GetComponent<BasePlayer>();

            if (p == null || t == null)
            {
                return null;
            }

            if (Interface.CallHook("CanEntityTrapTrigger", new object[] { t, p }) is bool val)
            {
                return val ? (object)null : true;
            }

            if (inRaidableBasesZone.Contains(p.userID)) return null;

            if (p.IsNpc || !p.userID.IsSteamId())
            {
                return null;
            }
            else if (p.userID.IsSteamId() && rulesData.entityRules.trapD)
            {
                return false;
            }
            else return null;
        }

        //Gun trap ignore players if true
        private object CanBeTargeted(BasePlayer t, GunTrap g)
        {
            if (t == null || g == null) return null;

            try
            {
                object extCanEntityBeTargeted = Interface.CallHook("CanEntityBeTargeted", new object[] { t, g });
                if (!ReferenceEquals(extCanEntityBeTargeted, null) && extCanEntityBeTargeted is bool && (bool)extCanEntityBeTargeted)
                {
                    return null;
                }
            }
            catch { }

            if (inRaidableBasesZone.Contains(t.userID)) return null;

            if (t.IsNpc || !t.userID.IsSteamId())
            {
                return null;
            }
            else if (g.OwnerID == 0)
            {
                return null;
            }
            else if (t.userID.IsSteamId() && rulesData.entityRules.trapD)
            {
                return false;
            }
            else return null;
        }

        //flame turret ignore player if true
        private object CanBeTargeted(BasePlayer t, FlameTurret g)
        {
            if (t == null || g == null) return null;

            try
            {
                object extCanEntityBeTargeted = Interface.CallHook("CanEntityBeTargeted", new object[] { t, g });
                if (!ReferenceEquals(extCanEntityBeTargeted, null) && extCanEntityBeTargeted is bool && (bool)extCanEntityBeTargeted)
                {
                    return null;
                }
            }
            catch { }

            if (inRaidableBasesZone.Contains(t.userID)) return null;

            if (t.IsNpc || !t.userID.IsSteamId())
            {
                return null;
            }
            else if (g.OwnerID == 0)
            {
                return null;
            }
            else if (t.userID.IsSteamId() && rulesData.entityRules.trapD)
            {
                return false;
            }
            else return null;
        }
        //auto turrets ignore players if true
        private object CanBeTargeted(BasePlayer t, AutoTurret g)
        {
            if (t == null || g == null) return null;

            try
            {
                object extCanEntityBeTargeted = Interface.CallHook("CanEntityBeTargeted", new object[] { t, g });
                if (!ReferenceEquals(extCanEntityBeTargeted, null) && extCanEntityBeTargeted is bool && (bool)extCanEntityBeTargeted)
                {
                    return null;
                }
            }
            catch { }

            if (inRaidableBasesZone.Contains(t.userID)) return null;

            if (t.IsNpc || !t.userID.IsSteamId() || t.ShortPrefabName.Contains("zombie"))
            {
                return null;
            }
            else if (g.OwnerID == 0)
            {
                return null;
            }
            else if (t.userID.IsSteamId() && rulesData.entityRules.trapD)
            {
                return false;
            }
            else return null;
        }



        //NPC and Player sam site ignores if true
        private object OnSamSiteTarget(SamSite sam, BaseMountable mountable)
        {
            if (sam == null) return null;
            if (mountable == null) return null;            

            BasePlayer p = GetMountedPlayer(mountable);

            if (p.IsValid())
            {
                try
                {
                    object extCanEntityBeTargeted = Interface.CallHook("CanEntityBeTargeted", new object[] { p, sam });
                    if (!ReferenceEquals(extCanEntityBeTargeted, null) && extCanEntityBeTargeted is bool && (bool)extCanEntityBeTargeted)
                    {
                        return null;
                    }
                }
                catch { }

                if (inRaidableBasesZone.Contains(p.userID)) return null;
                //npc sam to player
                if (sam.OwnerID == 0 && rulesData.npcRules.NSamigP)
                {
                    return true;
                    //player sam to player
                }
                else if (sam.OwnerID > 0 && rulesData.npcRules.PSamigP)
                {
                    return true;
                }
                else if (IsAlliedWithPlayer(sam.OwnerID, p.userID))
                {
                    return true;
                }
            }
            else { return true; }
            
            return null;
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId()) return;

            if (config.displayNotify.showPvEOverlay && config.pveEnabledConfig)
            {
                ShowPVEUI(player);
            }
            else
            {
                ShowPVPUi(player);
            }

            if (config.statusUI.status)
            {
                AddStatusUI(player);
                ModifiersCustom.Add(player.userID, new ModifiersCheck());
            }
                
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player != null && config.statusUI.status)
            {
                RemoveStatusUI(player);
                ModifiersCustom.Remove(player.userID);
            }                
        }

        private BasePlayer GetMountedPlayer(BaseMountable m)
        {
            if (m.GetMounted())
            {
                return m.GetMounted();
            }

            if (m as BaseVehicle)
            {
                BaseVehicle v = m as BaseVehicle;

                foreach (BaseVehicle.MountPointInfo point in v.mountPoints)
                {
                    if (point.mountable.IsValid() && point.mountable.GetMounted())
                    {
                        return point.mountable.GetMounted();
                    }
                }

            }
            return null;
        }

        private Dictionary<string, string> Images = new Dictionary<string, string>();

        private void AddImage(string url)
        {
            if (!ImageLibrary.Call<bool>("HasImage", url)) ImageLibrary.Call("AddImage", url, url);
            timer.In(0.1f, () => { Images.Add(url, ImageLibrary.Call<string>("GetImage", url)); });
        }

        private void LoadImages()
        {
            if (ImageLibrary == null)
            {
                PrintError("[ImageLibrary] not found!");
                return;
            }
            AddImage(config.displayNotify.warningUI.WarningImgUrl);
            AddImage(config.displayNotify.lPUISetting.WarningImgUrl);
            AddImage("https://i.postimg.cc/SxC1PDyF/Untitled-design.png");
            AddImage(config.statusUI.peStatus.PVEStatus.icon);
            AddImage(config.statusUI.peStatus.PVPStatus.icon);
            AddImage(config.statusUI.zoneStatus.pvparea.icon);
            AddImage(config.statusUI.purgeStatus.purgepvpStatus.icon);
            AddImage(config.statusUI.purgeStatus.purgepveStatus.icon);
        }

        private string GetImage(string url)
        {
            return Images[url];
        }

        #endregion

        #region Mono

        private void AddStatusUI(BasePlayer player)
        {
            RemoveStatusUI(player);
            player.gameObject.AddComponent<StatusUIMono>();
        }

        private void RemoveStatusUI(BasePlayer player)
        {
            var gameObject = player.GetComponent<StatusUIMono>();
            if (gameObject != null) GameObject.DestroyImmediate(gameObject);
        }


        public class StatusUIMono : MonoBehaviour
        {
            private BasePlayer player;
            private bool sendUI;
            private int modifyCount;
            private Configuration.Setting config;
            private double timeRemaining;
            private string message;
            private List<(Configuration.Setting statusConfig, double timeRemaining, string message)> statusUI = new List<(Configuration.Setting, double, string)>();

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                if (player == null)
                {
                    enabled = false;
                    return;
                }

                GetConfig();
                CheckUI(); // Initialize config and timeRemaining

                InvokeRepeating(nameof(UpdateUI), 0.2f, 0.2f);
                InvokeRepeating(nameof(CheckModifiers), 0.2f, 0.2f);
                InvokeRepeating(nameof(CheckUI), 10f, 10f);
            }

            private void UpdateUI()
            {
                if (player == null || !player.IsConnected || !player.IsAlive())
                {
                    return;
                }

                if (sendUI)
                {
                    sendUI = false;
                    ins.ShowStatusUI(player, config, message,modifyCount, timeRemaining);
                    modifyCount = 0;
                }
                else
                {
                    sendUI = true;
                }
            }

            private void CheckUI()
            {
                if (statusUI.Count == 0)
                {
                    return;
                }

                var random = new System.Random();
                int index = random.Next(statusUI.Count);
                var selectedStatus = statusUI[index];
                config = selectedStatus.statusConfig;
                timeRemaining = selectedStatus.timeRemaining;
                message = selectedStatus.message.Replace("{TIMER}", ins.FormatTime(timeRemaining));

                GetConfig();
            }

            private void GetConfig()
            {
                statusUI.Clear();

                // Zone status check
                if (ins.config.statusUI.zoneStatus.status && ins.ZoneManager != null)
                {
                    if (ins.PlayerInPVPZone(player) && ins.rulesData.zoneRules.disableRulesZone
                        && ins.rulesData.zoneRules.zoneManager && ins.config.pveEnabledConfig
                        && ins.config.statusUI.zoneStatus.pvparea.status)
                    {
                        statusUI.Add((ins.config.statusUI.zoneStatus.pvparea, 0, ins.config.statusUI.zoneStatus.pvparea.strings.GetRandom()));
                    }
                }

                // PVE/PVP status check
                if (ins.config.statusUI.peStatus.status)
                {
                    var status = ins.config.pveEnabledConfig ? ins.config.statusUI.peStatus.PVEStatus : ins.config.statusUI.peStatus.PVPStatus;
                    var msg = ins.config.pveEnabledConfig ? ins.config.statusUI.peStatus.PVEStatus.strings.GetRandom() : ins.config.statusUI.peStatus.PVPStatus.strings.GetRandom();
                    if (status.status)
                    {
                        statusUI.Add((status, 0, msg));
                    }
                }

                // Purge status check
                if (ins.config.statusUI.purgeStatus.status)
                {
                    var timepvp = ins.CalculateTimeDifference();
                    var timepve = ins.CalculateTimeDifferenceToEnd();
                    if (ins.config.statusUI.purgeStatus.purgepvpStatus.status && timepvp.TotalSeconds > 0)
                    {
                        statusUI.Add((ins.config.statusUI.purgeStatus.purgepvpStatus, timepvp.TotalSeconds, ins.config.statusUI.purgeStatus.purgepvpStatus.strings.GetRandom()));
                    }
                    else if (ins.config.statusUI.purgeStatus.purgepveStatus.status && timepve.TotalSeconds > 0)
                    {
                        statusUI.Add((ins.config.statusUI.purgeStatus.purgepveStatus, timepve.TotalSeconds, ins.config.statusUI.purgeStatus.purgepveStatus.strings.GetRandom()));
                    }
                }
            }

            private void CheckModifiers()
            {
                modifyCount = 0;

                // Temperature
                if (player.metabolism.temperature.value <= 5 || player.metabolism.temperature.value >= 40)
                {
                    modifyCount++;
                }

                // Building Privilege
                var priv = player.GetNearestBuildingPrivledge();
                if (priv != null)
                {
                    if (priv.IsAuthed(player))
                    {
                        modifyCount += 2;
                    }
                    else if (player.GetActiveItem()?.info.shortname == "hammer" || player.GetActiveItem()?.info.shortname == "building.planner")
                    {
                        modifyCount++;
                    }
                }

                // Comfort
                if (player.currentComfort > 0) modifyCount++;

                // Calories
                if (player.metabolism.calories.value < 40) modifyCount++;

                // Hydration
                if (player.metabolism.hydration.value < 35) modifyCount++;

                // Radiation
                if (player.metabolism.radiation_poison.value > 0) modifyCount++;

                // Wetness
                if (player.metabolism.wetness.value >= 0.02) modifyCount++;

                // Oxygen
                if (player.metabolism.oxygen.value < 1f) modifyCount++;

                // Crafting
                if (player.inventory.crafting.queue.Count > 0) modifyCount++;

                // Active Modifiers
                if (player.modifiers.ActiveModifierCoount > 0) modifyCount ++;

                // Safe Zone
                if (player.HasPlayerFlag(BasePlayer.PlayerFlags.SafeZone)) modifyCount++;

                // Mount Status
                if (player.isMounted && (player.GetMountedVehicle() != null || player.GetMounted() is RidableHorse)) modifyCount++;

                // Craft Level
                if (player.currentCraftLevel > 0) modifyCount++;

                // Custom Modifiers
                if (ins.ModifiersCustom.TryGetValue(player.userID, out var value))
                {
                    value.items.RemoveIfTime(x => x.Value < UnityEngine.Time.realtimeSinceStartup);
                    modifyCount += value.items.Count;
                }
            }

            private void OnDestroy()
            {
                CancelInvoke();
                CuiHelper.DestroyUi(player, "StatusUI");
            }
        }



        #endregion

        #region Discord

        private void SendDiscordMessage(string webhook, string title, List<string> embeds, string color ,bool inline = false)
        {
            int dColor = HexToDiscordColor(color);

            Embed embed = new Embed();
            foreach (var item in embeds)
            {
                embed.AddField(title, item, inline, dColor);
            }

            webrequest.Enqueue(webhook, new DiscordMessage("@everyone", embed).ToJson(), (code, response) => { },
                this,
                RequestMethod.POST, new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" }
                });
        }

        private class DiscordMessage
        {
            public DiscordMessage(string content, params Embed[] embeds)
            {
                Content = content;
                Embeds = embeds.ToList();
            }

            [JsonProperty("content")] public string Content { get; set; }
            [JsonProperty("embeds")] public List<Embed> Embeds { get; set; }

            public string ToJson()
            {
                return JsonConvert.SerializeObject(this);
            }
        }

        private class Embed
        {
            public int color
            {
                get; set;
            }
            [JsonProperty("fields")] public List<Field> Fields { get; set; } = new List<Field>();

            public Embed AddField(string name, string value, bool inline, int colors)
            {
                Fields.Add(new Field(name, Regex.Replace(value, "<.*?>", string.Empty), inline));
                color = colors;
                return this;
            }
        }

        private class Field
        {
            public Field(string name, string value, bool inline)
            {
                Name = name;
                Value = value;
                Inline = inline;
            }

            [JsonProperty("name")] public string Name { get; set; }
            [JsonProperty("value")] public string Value { get; set; }
            [JsonProperty("inline")] public bool Inline { get; set; }
        }
        public int HexToDiscordColor(string hexColor)
        {
            // Remove the '#' if present
            hexColor = hexColor.TrimStart('#');

            // Parse the hex color string to a 32-bit integer
            int colorValue = int.Parse(hexColor, System.Globalization.NumberStyles.HexNumber);

            // Discord uses a 24-bit color representation, so discard the alpha channel
            colorValue &= 0xFFFFFF;

            return colorValue;
        }

        #endregion

        #region APIs

        [HookMethod("GetPVPStartTimeRemaining")]
        public TimeSpan GetPVPStartTimeRemaining()
        {
            return CalculateTimeDifference();
        }

        [HookMethod("GetPVPEndTimeRemaining")]
        public TimeSpan GetPVPEndTimeRemaining()
        {
            return CalculateTimeDifferenceToEnd();
        }

        [HookMethod("API_SetProperty")]
        private void API_SetProperty(string PropertyName)
        {
            SetProperty(PropertyName);
        }

        [HookMethod("AddToExcludeZone")]
        public bool AddToExcludeZone(string ID)
        {
            if (string.IsNullOrEmpty(ID) || config == null || config.excludedZoneIDs == null)
                return false;

            if (!config.excludedZoneIDs.Contains(ID))
            {
                config.excludedZoneIDs.Add(ID);
                SaveConfig();
            }
            return true;
        }

        [HookMethod("RemoveFromExcludeZone")]
        public bool RemoveFromExcludeZone(string ID)
        {
            if (config.excludedZoneIDs.Remove(ID))
            {
                SaveConfig();
                return true;
            }
            return false;
        }

        [HookMethod("IsStatusEnabled_API")]
        public bool IsStatusEnabled_API()
        {
            return config.statusUI.status;
        }

        [HookMethod("IsServerPVE")]
        public bool IsServerPVE()
        {
            return config.pveEnabledConfig;
        }

        #endregion


    }
}

namespace Oxide.Plugins.SimplePVEExtensionMethods
{
    public static class ExtensionMethods
    {
        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (T element in source) action(element);
        }

        public static void RemoveIfTime<TKey, TValue>(this Dictionary<TKey, TValue> dict, Func<KeyValuePair<TKey, TValue>, bool> condition)
        {
            foreach (var cur in dict.Where(condition).ToList())
            {
                dict.Remove(cur.Key);
            }
        }

        public static void CheckForTime<TKey>(this Dictionary<TKey, float> dict, TKey key)
        {
            dict[key] = UnityEngine.Time.realtimeSinceStartup + 3.6f;
        }

    }
}
