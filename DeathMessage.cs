using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Rust;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core;
using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("DeathMessage", "Damo/beee / M&B-Studios", "1.1.2", ResourceId = 0)]
    [Description("Players can freely switch between the two modes of death tips")]
    class DeathMessage : RustPlugin
    {

        #region 设置文档
        private ConfigData configData;
        private int _TempText = 0;
        private const string Permission_registration = "deathmessage.admin";
        private static DeathMessage Instance;

        private HashSet<ulong> _DeathMessageType = new HashSet<ulong>();

        public TextAnchor _textAnchor = TextAnchor.MiddleLeft;

        private ulong Last_attacker;
        private class ConfigData
        {
            public Oxide.Core.VersionNumber Version;

            [JsonProperty("➊ Global Messages settings")]
            public EnableSettings Enable_settings;

            [JsonProperty("➊ Discord settings")]
            public DiscordEnableSettings Discord_settings;

            [JsonProperty("➋ Display name modification and activation")]
            public AboutName About_name;

            [JsonProperty("➌ Other settings")]
            public OtherSettings Other_settings;

            [JsonProperty("➍ Lang settings")]
            public Dictionary<string, string> Lang_settings;

        }
        private class EnableSettings
        {
            [JsonProperty("Enable About Animal")]
            public bool Enable_Animal;

            [JsonProperty("Enable About Entitys")]
            public bool Enable_Entity;

            [JsonProperty("Enable About NPC")]
            public bool Enable_NPC;

            [JsonProperty("Enable Player Deaths")]
            public bool Enable_Player;

            [JsonProperty("Enable About Suicide")]
            public bool Enable_About_Suicide;
        }
        private class DiscordEnableSettings
        {
            [JsonProperty("Webhook URL")]
            public string DiscordWebHookUrl;

            [JsonProperty("Bot Name")]
            public string DiscordBotName;

            [JsonProperty("Bot Avatar Link")]
            public string DiscordAvatarLink;

            [JsonProperty("Enable Animal Deaths")]
            public bool Enable_Animal;

            [JsonProperty("Enable Entities Deaths")]
            public bool Enable_Entity;

            [JsonProperty("Enable NPC Deaths")]
            public bool Enable_NPC;

            [JsonProperty("Enable Player Deaths")]
            public bool Enable_Player;
        }
        private class OtherSettings
        {
            [JsonProperty("Default command")]
            public string Default_command;

            [JsonProperty("Chat Icon Id")]
            public string Chat_Icon;

            [JsonProperty("Default display(true = FloatUI , false = Chat box)")]
            public bool Defaultdisplay;

            [JsonProperty("FloatUI message closing time second")]
            public int Ui_time;

            [JsonProperty("Click on FloatUI switch to the chat box in seconds")]
            public int switching_time;
        }

        private class AboutName
        {
            [JsonProperty("➀ Animal name")]
            public Dictionary<string, EnableData> Animal_Name;

            [JsonProperty("➁ NPC name")]
            public Dictionary<string, EnableData> NPC_name;

            [JsonProperty("➂ Entity name")]
            public Dictionary<string, EnableData> Entity_Name;

            [JsonProperty("➃ Weapon name")]
            public Dictionary<string, string> Default_weapon;

            [JsonProperty("➄ Body part name")]
            public Dictionary<string, string> Body_part_name;
        }
        private class EnableData
        {
            [JsonProperty("Enable")]
            public bool Enable;

            [JsonProperty("Display name")]
            public string Display_name;

        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        private void LoadVariables()
        {
            LoadConfigVariables();
            //1.1.1 update
    if (!configData.Enable_settings.Enable_About_Suicide)
    {
        Puts("Updating config.");
        configData.Enable_settings.Enable_About_Suicide = true; // Setze den Standardwert
        Puts("Added 'Enable About Suicide' setting to configuration.");
    }

            configData.Version = Version;
            SaveConfig(configData);
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                Version = Version,
                Enable_settings = new EnableSettings
                {
                    Enable_Animal = true,
                    Enable_Entity = true,
                    Enable_NPC = true,
                    Enable_Player = true,
                    Enable_About_Suicide = true
                },
                Discord_settings = new DiscordEnableSettings
                {
                    DiscordBotName = "Death Messages Bot",
                    DiscordAvatarLink = "https://avatarfiles.alphacoders.com/128/128573.png",
                    DiscordWebHookUrl = "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks",
                    Enable_Animal = true,
                    Enable_Entity = true,
                    Enable_NPC = true,
                    Enable_Player = true
                },
                About_name = new AboutName
                {
                    Animal_Name = new Dictionary<string, EnableData>
                    {
                        {"bear" ,new EnableData {Enable = true,Display_name = "bear" } },
                        {"polarbear" ,new EnableData {Enable = true,Display_name = "polarbear" } },
                        {"boar" ,new EnableData {Enable = true,Display_name = "boar" } },
                        {"chicken" ,new EnableData {Enable = true,Display_name = "chicken" } },
                        {"stag" ,new EnableData {Enable = true,Display_name = "stag" } },
                        {"wolf" ,new EnableData {Enable = true,Display_name = "wolf" } },
                        {"testridablehorse" ,new EnableData {Enable = true,Display_name = "testridablehorse" } },
                        {"horse" ,new EnableData {Enable = true,Display_name = "horse" } },
                        {"zombie" ,new EnableData {Enable = true,Display_name = "zombie" } },
                    },
                    Entity_Name = new Dictionary<string, EnableData>
                    {
                        {"bradleyapc" ,new EnableData {Enable = true,Display_name = "bradleyapc" } },
                        {"patrolhelicopter" ,new EnableData {Enable = true,Display_name = "patrolhelicopter" } },
                        {"autoturret_deployed" ,new EnableData {Enable = false,Display_name = "autoturret_deployed" } },
                        {"flameturret.deployed" ,new EnableData {Enable = false,Display_name = "flameturret.deployed" } },
                        {"guntrap.deployed" ,new EnableData {Enable = false,Display_name = "guntrap.deployed" } },
                        {"landmine" ,new EnableData {Enable = false,Display_name = "landmine" } },
                        {"beartrap" ,new EnableData {Enable = false,Display_name = "beartrap" } },
                        {"sam_site_turret_deployed" ,new EnableData {Enable = false,Display_name = "sam_site_turret_deployed" } },
                        {"sentry.scientist.static" ,new EnableData {Enable = false,Display_name = "sentry.scientist.static" } },
                    },
                    NPC_name = new Dictionary<string, EnableData>
                    {
                        {"scarecrow" ,new EnableData {Enable = true,Display_name = "scarecrow" } },
                        {"bandit_conversationalist" ,new EnableData {Enable = true,Display_name = "bandit_conversationalist" } },
                        {"bandit_shopkeeper" ,new EnableData {Enable = true,Display_name = "bandit_shopkeeper" } },
                        {"boat_shopkeeper" ,new EnableData {Enable = true,Display_name = "boat_shopkeeper" } },
                        {"missionprovider_bandit_a" ,new EnableData {Enable = true,Display_name = "missionprovider_bandit_a" } },
                        {"missionprovider_bandit_b" ,new EnableData {Enable = true,Display_name = "missionprovider_bandit_b" } },
                        {"missionprovider_fishing_a" ,new EnableData {Enable = true,Display_name = "missionprovider_fishing_a" } },
                        {"missionprovider_fishing_b" ,new EnableData {Enable = true,Display_name = "missionprovider_fishing_b" } },
                        {"missionprovider_outpost_a" ,new EnableData {Enable = true,Display_name = "missionprovider_outpost_a" } },
                        {"missionprovider_outpost_b" ,new EnableData {Enable = true,Display_name = "missionprovider_outpost_b" } },
                        {"missionprovider_stables_a" ,new EnableData {Enable = true,Display_name = "missionprovider_stables_a" } },
                        {"missionprovider_stables_b" ,new EnableData {Enable = true,Display_name = "missionprovider_stables_b" } },
                        {"npc_bandit_guard" ,new EnableData {Enable = true,Display_name = "npc_bandit_guard" } },
                        {"npc_tunneldweller" ,new EnableData {Enable = true,Display_name = "npc_tunneldweller" } },
                        {"npc_underwaterdweller" ,new EnableData {Enable = true,Display_name = "npc_underwaterdweller" } },
                        {"player" ,new EnableData {Enable = true,Display_name = "player" } },
                        {"scientistnpc_patrol" ,new EnableData {Enable = true,Display_name = "scientistnpc_patrol" } },
                        {"scientistnpc_peacekeeper" ,new EnableData {Enable = true,Display_name = "scientistnpc_peacekeeper" } },
                        {"scientistnpc_roam" ,new EnableData {Enable = true,Display_name = "scientistnpc_roam" } },
                        {"scientistnpc_roamtethered" ,new EnableData {Enable = true,Display_name = "scientistnpc_roamtethered" } },
                        {"stables_shopkeeper" ,new EnableData {Enable = true,Display_name = "stables_shopkeeper" } },
                    },
                    Default_weapon = new Dictionary<string, string>
                    {
                     {"grenade","grenade"},
                     {"explosive","explosive"},
                     {"heat","heat"},
                     {"Assault Rifle","Assault Rifle"},
                     {"LR-300 Assault Rifle","LR-300 Assault Rifle"},
                     {"L96 Rifle","L96 Rifle"},
                     {"Bolt Action Rifle","Bolt Action Rifle"},
                     {"Semi-Automatic Rifle","Semi-Automatic Rifle"},
                     {"Semi-Automatic Pistol","Semi-Automatic Pistol"},
                     {"Spas-12 Shotgun","Spas-12 Shotgun"},
                     {"M92 Pistol","M92 Pistol"},
                     {"Crossbow","Crossbow"},
                     {"Compound Bow","Compound Bow"},
                     {"Eoka Pistol","Eoka Pistol"},
                     {"Nailgun","Nailgun"},
                     {"Multiple Grenade Launcher","Multiple Grenade Launcher"},
                     {"Waterpipe Shotgun","Waterpipe Shotgun"},
                     {"Flame Thrower","Flame Thrower"},
                     {"Revolver","Revolver"},
                     {"Python Revolver","Python Revolver"},
                     {"Pump Shotgun","Pump Shotgun"},
                     {"Custom SMG","Custom SMG"},
                     {"MP5A4","MP5A4"},
                     {"Thompson","Thompson"},
                     {"Double Barrel Shotgun","Double Barrel Shotgun"},
                     {"M39 Rifle","M39 Rifle"},
                     {"Rocket Launcher","Rocket Launcher"},
                     {"M249","M249"},
                     {"Chainsaw","Chainsaw"},
                     {"Jackhammer","Jackhammer"},
                     {"Salvaged Sword","Salvaged Sword"},
                     {"Hunting Bow","Hunting Bow"},
                     {"Longsword","Longsword"},
                     {"Salvaged Cleaver","Salvaged Cleaver"},
                     {"Combat Knife","Combat Knife"},
                     {"Wooden Spear","Wooden Spear"},
                     {"Stone Hatchet","Stone Hatchet"},
                     {"Rock","Rock"},
                     {"Torch","Torch"},
                     {"Salvaged Axe","Salvaged Axe"},
                     {"Salvaged Hammer","Salvaged Hammer"},
                     {"Pickaxe","Pickaxe"},
                     {"Mace","Mace"},
                     {"Bone Knife","Bone Knife"},
                     {"Hatchet","Hatchet"},
                     {"Salvaged Icepick","Salvaged Icepick"},
                     {"Stone Spear","Stone Spear"},
                     {"Flashlight","Flashlight"},
                     {"Butcher Knife","Butcher Knife"},
                     {"Bone Club","Bone Club"},
                     {"Candy Cane Club","Candy Cane Club"},
                     {"Stone Pickaxe","Stone Pickaxe"},
                    },
                    Body_part_name = new Dictionary<string, string>
                    {
                     {"Arm","Arm"},
                     {"Chest","Chest"},
                     {"Head","Head"},
                     {"Leg","Leg"},
                     {"Hand","Hand"},
                     {"Foot","Foot"},
                     {"Stomach","Stomach"},
                     {"Body","Body"},
                    }
                },
                Other_settings = new OtherSettings
                {
                    Default_command = "dm",
                    Chat_Icon = "0",
                    Defaultdisplay = true,
                    switching_time = 10,
                    Ui_time = 10,
                },
                Lang_settings = new Dictionary<string, string>
                {
                    {"ChatTitle", ""},
                    {"NoPermission", "Not have permission !"},
                    {"MessageTochat", "Toggle death message to <color=#FFFF00>ChatBox</color>"},
                    {"MessageToFloatUI","Toggle death message to <color=#66FF00>FloatUI</color>"},
                    {"ButtonSwitch", "Auto switch death message to <color=#FFFF00>ChatBox</color> <color=#FF0000>{0}</color> seconds"},
                    {"Reset", "Reset"},
                    {"DIY", "DIY control panel"},
                    {"FloatUILocation", "UI Location"},
                    {"LengthWidth", "Length Width"},
                    {"FontSize", "Font size"},
                    {"IntervalStretch", "Interval stretch"},
                    {"DisplayNumber", "Display number"},
                    {"FontPosition", "Font position"},
                    {"NPCKillPlayer","<color=#66FF00>{0}</color> <color=#66FFFF>{1}</color> Kill <color=#FFFF00>{2}</color> <color=#FF9900>{3}</color> m" },
                    {"PlayerSuicide","<color=#FFFF00>{0}</color> suicide"},
                    {"PlayerKillPlayer","<color=#66FF00>{0}</color> <color=#66FFFF>{1}</color> Kill <color=#FFFF00>{2}</color> <color=#6699FF>{3}</color> <color=#FF9900>{4}</color> m"},
                    {"PlayerKillNPC","<color=#66FF00>{0}</color> <color=#66FFFF>{1}</color> Kill <color=#FFFF00>{2}</color> <color=#6699FF>{3}</color> <color=#FF9900>{4}</color> m"},
                    {"PlayerKillEntity","<color=#66FF00>{0}</color> <color=#66FFFF>{1}</color> Kill <color=#FFFF00>{2}</color> <color=#FF9900>{3}</color> m"},
                    {"PlayerKillAnimal","<color=#66FF00>{0}</color> <color=#66FFFF>{1}</color> Kill <color=#FFFF00>{2}</color> <color=#FF9900>{3}</color> m"},
                    {"PlayerKillBradleyapc","<color=#66FF00>{0}</color> <color=#66FFFF>{1}</color> Kill <color=#FFFF00>{2}</color> <color=#FF9900>{3}</color> m"},
                    {"PlayerKillPatrolHelicopter","<color=#66FF00>{0}</color> <color=#66FFFF>{1}</color> Kill <color=#FFFF00>{2}</color> <color=#FF9900>{3}</color> m"},
                    {"EntityKillPlayer","<color=#66FF00>{0}</color> Kill <color=#FFFF00>{1}</color> <color=#FF9900>{2}</color> m"},
                    {"BradleyapcKillPlayer","<color=#66FF00>{0}</color> Kill <color=#FFFF00>{1}</color> <color=#FF9900>{2}</color> m"},
                    {"PatrolHelicopterKillPlayer","<color=#66FF00>{0}</color> Kill <color=#FFFF00>{1}</color> <color=#FF9900>{2}</color> m"},
                    {"AnimalKillPlayer","<color=#66FF00>{0}</color> Kill <color=#FFFF00>{1}</color> <color=#FF9900>{2}</color> m"},
                }
            };
            SaveConfig(config);
        }

        #endregion

        #region 存档数据
        DamoData damoData;
        class DamoData
        {
            public float[] pos = new float[] { 0.6f, 0.97f, 1f, 1f, 6f, 0.03f, 18, 0 };
        }
        private void LoadData()
        {
            try
            {
                damoData = Interface.Oxide.DataFileSystem.ReadObject<DamoData>(Name);
            }
            catch
            {
                ClearData();
            }
        }

        private void OnServerSave() => SaveData();

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, damoData);
        private void ClearData()
        {
            damoData = new DamoData();
            SaveData();
        }
        #endregion

        #region 钩子相关

        private void Init()
        {
            LoadVariables();
            LoadData();
        }

        private void OnServerInitialized()
        {
            PrintWarning(
                $"Support: M&B-Studios\n Contact Discord: mbstudios");
            Instance = this;
            AddCovalenceCommand(configData.Other_settings.Default_command, nameof(DeathMessageCommand));
            permission.RegisterPermission(Permission_registration, this);
            Get_textAnchor();

            foreach (var item in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(item);
            }
        }
        private void Unload()
        {
            SaveData();
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "_ControlMenuUi");
                Destroy_All(player);
            }
            Instance = null;
            configData = null;
        }
        private void OnPlayerDisconnected(BasePlayer player)
        {
            _DeathMessageType.Remove(player.userID);
            if (_playersUIHandler.ContainsKey(player.userID))
            {
                _playersUIHandler.Remove(player.userID);
            }
        }
        private void OnPlayerConnected(BasePlayer player)
        {
            if (configData.Other_settings.Defaultdisplay)
            {
                _DeathMessageType.Remove(player.userID);
            }
            else
            {
                _DeathMessageType.Add(player.userID);
            }
        }
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;
            if (entity is BaseHelicopter)
            {
                if (info.InitiatorPlayer != null && info.InitiatorPlayer.userID.IsSteamId())
                {
                    Last_attacker = info.InitiatorPlayer.userID;
                }

            }
        }
        private void OnEntityDeath(BaseCombatEntity victim, HitInfo info)
        {
            if (victim == null || info == null) return;
            if (victim.ShortPrefabName.Contains("corpse")) return;
            try
            {
                if (info.Initiator != null)
                {
                    var Uncertain_attacker = info.Initiator;
                    if (Uncertain_attacker is BasePlayer)
                    {
                        var Attack_player = Uncertain_attacker as BasePlayer;
                        if (Attack_player.userID < 700000000000)
                        {
                            if (victim is BasePlayer)
                            {
                                var Victim_player = victim as BasePlayer;
                                if (Victim_player.userID > 700000000000)
                                {
                                    if (configData.Enable_settings.Enable_NPC || configData.Discord_settings.Enable_NPC)
                                    {
                                        if (IS_BotSpawn_Name(Attack_player.displayName))
                                        {
                                            UPGP(GetNPCEnableState(), "NPCKillPlayer", Attack_player.displayName, Weapon_Name(Attack_player, info), Victim_player.displayName, Attack_distance(info));
                                        }
                                        else
                                        {
                                            EnableData data;
                                            if (configData.About_name.NPC_name.TryGetValue(Attack_player.ShortPrefabName, out data))
                                            {
                                                if (data.Enable)
                                                {
                                                    UPGP(GetNPCEnableState(), "NPCKillPlayer", data.Display_name, Weapon_Name(Attack_player, info), Victim_player.displayName, Attack_distance(info));
                                                }
                                            }
                                            else
                                            {
                                                configData.About_name.NPC_name.Add(Attack_player.ShortPrefabName, new EnableData { Enable = false, Display_name = Attack_player.ShortPrefabName });
                                                SaveConfig(configData);

                                                Puts($"Added a new ShortPrefabName To be activated.. {Attack_player.ShortPrefabName}");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (victim is BasePlayer)
                            {
                                var Victim_player = victim as BasePlayer;

                                if (Victim_player.userID > 700000000000)
                                {
                                    if(configData.Enable_settings.Enable_Player || configData.Discord_settings.Enable_Player)
                                    {
                                if (Attack_player == victim)
                                {
                                    // Prüfen, ob Suizidnachrichten aktiviert sind, bevor eine Nachricht gesendet wird
                                    if(configData.Enable_settings.Enable_About_Suicide)
                                    {
                                             UPGP(BroadcastTo.Both, "PlayerSuicide", Attack_player.displayName);
                                    }
                                }
                                        else
                                        {
                                            UPGP(BroadcastTo.Both, "PlayerKillPlayer", Attack_player.displayName, Weapon_Name(Attack_player, info), Victim_player.displayName, GetBodyName(info), Attack_distance(info));
                                        }
                                    }
                                }
                                else if (configData.Enable_settings.Enable_NPC || configData.Discord_settings.Enable_NPC)
                                {
                                    if (IS_BotSpawn_Name(Victim_player.displayName))
                                    {
                                        UPGP(GetNPCEnableState(), "PlayerKillNPC", Attack_player.displayName, Weapon_Name(Attack_player, info), Victim_player.displayName, GetBodyName(info), Attack_distance(info));
                                    }
                                    else
                                    {
                                        EnableData data;
                                        if (configData.About_name.NPC_name.TryGetValue(victim.ShortPrefabName, out data))
                                        {
                                            if (data.Enable)
                                            {
                                                UPGP(GetNPCEnableState(), "PlayerKillNPC", Attack_player.displayName, Weapon_Name(Attack_player, info), data.Display_name, GetBodyName(info), Attack_distance(info));
                                            }
                                        }
                                        else
                                        {
                                            configData.About_name.NPC_name.Add(victim.ShortPrefabName, new EnableData { Enable = false, Display_name = victim.ShortPrefabName });
                                            SaveConfig(configData);
                                            Puts($"Added a new ShortPrefabName To be activated.. {victim.ShortPrefabName}");
                                        }
                                    }

                                }
                            }
                            else
                            {
                                if (victim is BaseAnimalNPC)
                                {
                                    if (configData.Enable_settings.Enable_Animal || configData.Discord_settings.Enable_Animal)
                                    {
                                        EnableData data;
                                        if (configData.About_name.Animal_Name.TryGetValue(victim.ShortPrefabName, out data))
                                        {
                                            if (data.Enable)
                                            {
                                                UPGP(GetAnimalEnableState(), "PlayerKillAnimal", Attack_player.displayName, Weapon_Name(Attack_player, info), data.Display_name, Attack_distance(info));
                                            }
                                        }
                                        else
                                        {
                                            configData.About_name.Animal_Name.Add(victim.ShortPrefabName, new EnableData { Enable = false, Display_name = victim.ShortPrefabName });
                                            SaveConfig(configData);

                                            Puts($"Added a new ShortPrefabName To be activated.. {victim.ShortPrefabName}");
                                        }
                                    }

                                }
                                else if (victim is BaseHelicopter)
                                {

                                    if (configData.Enable_settings.Enable_Entity || configData.Discord_settings.Enable_Entity)
                                    {
                                        EnableData data;
                                        if (configData.About_name.Entity_Name.TryGetValue(victim.ShortPrefabName, out data))
                                        {
                                            if (data.Enable)
                                            {
                                                UPGP(GetEntityEnableState(), "PlayerKillPatrolHelicopter", Attack_player.displayName, Weapon_Name(Attack_player, info), data.Display_name, Attack_distance(info));
                                            }

                                        }
                                        else
                                        {
                                            configData.About_name.Entity_Name.Add(victim.ShortPrefabName, new EnableData { Enable = false, Display_name = victim.ShortPrefabName });
                                            SaveConfig(configData);

                                            Puts($"Added a new ShortPrefabName To be activated.. {victim.ShortPrefabName}");
                                        }
                                    }


                                }
                                else if (victim is BradleyAPC)
                                {
                                    if (configData.Enable_settings.Enable_Entity || configData.Discord_settings.Enable_Entity)
                                    {
                                        EnableData data;
                                        if (configData.About_name.Entity_Name.TryGetValue(victim.ShortPrefabName, out data))
                                        {
                                            if (data.Enable)
                                            {
                                                UPGP(GetEntityEnableState(), "PlayerKillBradleyapc", Attack_player.displayName, Weapon_Name(Attack_player, info), data.Display_name, Attack_distance(info));
                                            }
                                        }
                                        else
                                        {

                                            configData.About_name.Entity_Name.Add(victim.ShortPrefabName, new EnableData { Enable = false, Display_name = victim.ShortPrefabName });
                                            SaveConfig(configData);

                                            Puts($"Added a new ShortPrefabName To be activated.. {victim.ShortPrefabName}");
                                        }
                                    }

                                }
                                else
                                {
                                    if (configData.Enable_settings.Enable_Entity || configData.Discord_settings.Enable_Entity)
                                    {
                                        EnableData data;
                                        if (configData.About_name.Entity_Name.TryGetValue(victim.ShortPrefabName, out data))
                                        {
                                            if (data.Enable)
                                            {
                                                UPGP(GetEntityEnableState(), "PlayerKillEntity", Attack_player.displayName, Weapon_Name(Attack_player, info), data.Display_name, Attack_distance(info)); ;
                                            }
                                        }
                                        else
                                        {

                                            configData.About_name.Entity_Name.Add(victim.ShortPrefabName, new EnableData { Enable = false, Display_name = victim.ShortPrefabName });
                                            SaveConfig(configData);

                                            Puts($"Added a new ShortPrefabName To be activated.. {victim.ShortPrefabName}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else if (victim is BasePlayer)
                    {
                        var Victim_player = victim as BasePlayer;
                        if (Victim_player.userID > 700000000000)
                        {
                            if (Uncertain_attacker is BaseAnimalNPC)
                            {

                                if (configData.Enable_settings.Enable_Animal || configData.Discord_settings.Enable_Animal)
                                {
                                    EnableData data;
                                    if (configData.About_name.Animal_Name.TryGetValue(Uncertain_attacker.ShortPrefabName, out data))
                                    {
                                        if (data.Enable)
                                        {
                                            UPGP(GetAnimalEnableState(), "AnimalKillPlayer", data.Display_name, Victim_player.displayName, Attack_distance(info));
                                        }
                                    }
                                    else
                                    {
                                        configData.About_name.Animal_Name.Add(Uncertain_attacker.ShortPrefabName, new EnableData { Enable = false, Display_name = Uncertain_attacker.ShortPrefabName });
                                        SaveConfig(configData);

                                        Puts($"Added a new ShortPrefabName To be activated.. {Uncertain_attacker.ShortPrefabName}");
                                    }
                                }


                            }
                            else if (Uncertain_attacker is BaseHelicopter)
                            {
                                if (configData.Enable_settings.Enable_Entity || configData.Discord_settings.Enable_Entity)
                                {
                                    EnableData data;
                                    if (configData.About_name.Entity_Name.TryGetValue(Uncertain_attacker.ShortPrefabName, out data))
                                    {
                                        if (data.Enable)
                                        {
                                            UPGP(GetEntityEnableState(), "PatrolHelicopterKillPlayer", data.Display_name, Victim_player.displayName, Attack_distance(info));
                                        }
                                    }
                                    else
                                    {

                                        configData.About_name.Entity_Name.Add(Uncertain_attacker.ShortPrefabName, new EnableData { Enable = false, Display_name = Uncertain_attacker.ShortPrefabName });
                                        SaveConfig(configData);

                                        Puts($"Added a new ShortPrefabName To be activated.. {Uncertain_attacker.ShortPrefabName}");
                                    }
                                }

                            }
                            else if (Uncertain_attacker is BradleyAPC)
                            {

                                if (configData.Enable_settings.Enable_Entity || configData.Discord_settings.Enable_Entity)
                                {
                                    EnableData data;
                                    if (configData.About_name.Entity_Name.TryGetValue(Uncertain_attacker.ShortPrefabName, out data))
                                    {
                                        if (data.Enable)
                                        {
                                            UPGP(GetEntityEnableState(), "BradleyapcKillPlayer", data.Display_name, Victim_player.displayName, Attack_distance(info));
                                        }
                                    }
                                    else
                                    {

                                        configData.About_name.Entity_Name.Add(Uncertain_attacker.ShortPrefabName, new EnableData { Enable = false, Display_name = Uncertain_attacker.ShortPrefabName });
                                        SaveConfig(configData);

                                        Puts($"Added a new ShortPrefabName To be activated.. {Uncertain_attacker.ShortPrefabName}");
                                    }
                                }


                            }
                            else
                            {
                                if (configData.Enable_settings.Enable_Entity || configData.Discord_settings.Enable_Entity)
                                {
                                    EnableData data;
                                    if (configData.About_name.Entity_Name.TryGetValue(Uncertain_attacker.ShortPrefabName, out data))
                                    {
                                        if (data.Enable)
                                        {
                                            UPGP(GetEntityEnableState(), "EntityKillPlayer", data.Display_name, Victim_player.displayName, Attack_distance(info));
                                        }
                                    }
                                    else
                                    {

                                        configData.About_name.Entity_Name.Add(Uncertain_attacker.ShortPrefabName, new EnableData { Enable = false, Display_name = Uncertain_attacker.ShortPrefabName });
                                        SaveConfig(configData);

                                        Puts($"Added a new ShortPrefabName To be activated.. {Uncertain_attacker.ShortPrefabName}");

                                    }
                                }
                            }

                        }
                    }
                }
                else
                {
                    if (victim is BaseHelicopter)
                    {
                        if (configData.Enable_settings.Enable_Entity || configData.Discord_settings.Enable_Entity)
                        {
                            EnableData data;
                            if (configData.About_name.Entity_Name.TryGetValue(victim.ShortPrefabName, out data))
                            {
                                var pl = FindPlayer(Last_attacker);
                                if (pl != null)
                                {
                                    if (data.Enable)
                                    {
                                        UPGP(GetEntityEnableState(), "PlayerKillEntity", pl.displayName, Weapon_Name(pl, info), data.Display_name, Attack_distance(info));
                                    }
                                }
                            }
                            else
                            {
                                Puts($"Added a new ShortPrefabName To be activated.. {victim.ShortPrefabName}");
                                configData.About_name.Entity_Name.Add(victim.ShortPrefabName, new EnableData { Enable = false, Display_name = victim.ShortPrefabName });
                                SaveConfig(configData);
                            }
                        }

                    }
                }
            }
            catch
            {

            }
        }
        #endregion

        #region 程序方法

        private void Get_textAnchor()
        {
            switch ((int)damoData.pos[7])
            {
                case 0:
                    _textAnchor = TextAnchor.UpperRight;
                    break;
                case 1:
                    _textAnchor = TextAnchor.UpperLeft;
                    break;
                case 2:
                    _textAnchor = TextAnchor.MiddleCenter;
                    break;
            }
        }

        void Send_Kill_Player_Note(String langKey, String killerName = null, String shortnameWeapon = null,
            String victomPlayer = null, String distanceKiller = null, String bodyName = "Body")
        {
            Item weaponInfo = null;
            if (shortnameWeapon != null)
                weaponInfo = ItemManager.CreateByName(shortnameWeapon);
            
            UPGP(BroadcastTo.Both, langKey, killerName, weaponInfo != null ? weaponInfo.info.displayName.english : shortnameWeapon, victomPlayer, bodyName, distanceKiller);

            if (weaponInfo != null)
            {
                weaponInfo.Remove();
                weaponInfo = null;
            }
        } 
        private void UPGP(BroadcastTo broadcastTo, string Key, string A = null, string B = null, string C = null, string D = null, string E = null)
        {
            string Text = null;
            if (configData.Lang_settings.ContainsKey(Key))
            {
                Text = string.Format(configData.Lang_settings[Key], A, B, C, D, E);
            }

            if(Text == null) return;

            if(broadcastTo == BroadcastTo.InGame || broadcastTo == BroadcastTo.Both)
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (!_DeathMessageType.Contains(player.userID))
                    {
                        UpUIMessage(player, Text);
                    }
                    else
                    {
                        rust.SendChatMessage(player, configData.Lang_settings["ChatTitle"], Text, configData.Other_settings.Chat_Icon);
                    }
                }
            }

            if(broadcastTo == BroadcastTo.Discord || broadcastTo == BroadcastTo.Both) BroadcastToDiscord(Text);
        }
        private BasePlayer FindPlayer(string nameOrIdOrIp)
        {
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString == nameOrIdOrIp)
                    return activePlayer;
                if (activePlayer.displayName.Contains(nameOrIdOrIp, CompareOptions.OrdinalIgnoreCase))
                    return activePlayer;
                if (activePlayer.net?.connection != null && activePlayer.net.connection.ipaddress == nameOrIdOrIp)
                    return activePlayer;
            }
            foreach (var sleepingPlayer in BasePlayer.sleepingPlayerList)
            {
                if (sleepingPlayer.UserIDString == nameOrIdOrIp)
                    return sleepingPlayer;
                if (sleepingPlayer.displayName.Contains(nameOrIdOrIp, CompareOptions.OrdinalIgnoreCase))
                    return sleepingPlayer;
            }
            return null;
        }
        private BasePlayer FindPlayer(ulong userId)
        {
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.userID == userId)
                    return activePlayer;
            }
            return null;
        }

        private string GetBodyName(HitInfo hitInfo)
        {

            string bone = HitBodyPos(hitInfo);
            if (configData.About_name.Body_part_name.ContainsKey(bone))
            {
                return configData.About_name.Body_part_name[bone];
            }
            else
            {
                configData.About_name.Body_part_name.Add(bone, bone);
                SaveConfig(configData);
                return bone;
            }
        }
        private string HitBodyPos(HitInfo hitInfo)
        {
            var hitArea = hitInfo?.boneArea ?? (HitArea)(-1);
            return (int)hitArea == -1 ? "Body" : hitArea.ToString();
        }

        private bool IS_BotSpawn_Name(string displayName)
        {
            try
            {
                ulong.Parse(displayName);
                return false;
            }
            catch
            {
                return true;
            }
        }

        private string Weapon_Name(BasePlayer player, HitInfo hit)
        {
            try
            {
                Item weapon = hit.Weapon.GetItem();
                if (weapon != null)
                {
                    if (weapon.name != null)
                    {
                        return weapon.name;
                    }
                    else if (configData.About_name.Default_weapon.ContainsKey(weapon.info.displayName.english))
                    {
                        return configData.About_name.Default_weapon[weapon.info.displayName.english];
                    }
                    else
                    {
                        configData.About_name.Default_weapon.Add(weapon.info.displayName.english, weapon.info.displayName.english);
                        SaveConfig(configData);
                        return weapon.info.displayName.english;
                    }
                }
                if (hit.WeaponPrefab != null)
                {
                    if (configData.About_name.Default_weapon.ContainsKey(hit.WeaponPrefab.ShortPrefabName))
                    {
                        return configData.About_name.Default_weapon[hit.WeaponPrefab.ShortPrefabName];
                    }
                    else
                    {
                        configData.About_name.Default_weapon.Add(hit.WeaponPrefab.ShortPrefabName, hit.WeaponPrefab.ShortPrefabName);
                        SaveConfig(configData);
                        return hit.WeaponPrefab.ShortPrefabName;
                    }
                }
                if (hit.damageTypes.GetMajorityDamageType() == DamageType.Explosion)
                {
                    if (configData.About_name.Default_weapon.ContainsKey("explosive"))
                    {
                        return configData.About_name.Default_weapon["explosive"];
                    }
                    else
                    {
                        configData.About_name.Default_weapon.Add("explosive", "explosive");
                        SaveConfig(configData);
                        return "explosive";
                    }
                }
                if (!hit.damageTypes.IsBleedCausing() && hit.damageTypes.Has(DamageType.Explosion))
                {
                    if (configData.About_name.Default_weapon.ContainsKey("explosive"))
                    {
                        return configData.About_name.Default_weapon["explosive"];
                    }
                    else
                    {
                        configData.About_name.Default_weapon.Add("explosive", "explosive");
                        SaveConfig(configData);
                        return "explosive";
                    }
                }
                if (hit.damageTypes.GetMajorityDamageType() == DamageType.Heat || (!hit.damageTypes.IsBleedCausing() && hit.damageTypes.Has(DamageType.Heat)))
                {
                    if (configData.About_name.Default_weapon.ContainsKey("heat"))
                    {
                        return configData.About_name.Default_weapon["heat"];
                    }
                    else
                    {
                        configData.About_name.Default_weapon.Add("heat", "heat");
                        SaveConfig(configData);
                        return "heat";
                    }
                }
                if (hit.ProjectilePrefab.name != null)
                {
                    if (configData.About_name.Default_weapon.ContainsKey(hit.ProjectilePrefab.name))
                    {
                        return configData.About_name.Default_weapon[hit.ProjectilePrefab.name];
                    }
                    else
                    {
                        configData.About_name.Default_weapon.Add(hit.ProjectilePrefab.name, hit.ProjectilePrefab.name);
                        SaveConfig(configData);
                        return hit.ProjectilePrefab.name;
                    }
                }
                else
                {
                    weapon = player.GetActiveItem();
                    if (weapon != null && weapon.GetHeldEntity() is AttackEntity)
                    {
                        if (weapon.name != null)
                        {
                            return weapon.name;
                        }
                        else if (configData.About_name.Default_weapon.ContainsKey(weapon.info.displayName.english))
                        {
                            return configData.About_name.Default_weapon[weapon.info.displayName.english];
                        }
                        else
                        {
                            configData.About_name.Default_weapon.Add(weapon.info.displayName.english, weapon.info.displayName.english);
                            SaveConfig(configData);
                            return weapon.info.displayName.english;
                        }
                    }
                    if (configData.About_name.Default_weapon.ContainsKey("unknown"))
                    {
                        return configData.About_name.Default_weapon["unknown"];
                    }
                    else
                    {
                        configData.About_name.Default_weapon.Add("unknown", "");
                        SaveConfig(configData);
                        return "";
                    }
                }
            }
            catch
            {
                if (configData.About_name.Default_weapon.ContainsKey("unknown"))
                {
                    return configData.About_name.Default_weapon["unknown"];
                }
                else
                {
                    configData.About_name.Default_weapon.Add("unknown", "");
                    SaveConfig(configData);
                    return "";
                }
            }
        }
        private string Attack_distance(HitInfo info)
        {
            return (info?.InitiatorPlayer != null ? (int)Vector3.Distance(info.InitiatorPlayer.transform.position, info.HitPositionWorld) : (int)info.ProjectileDistance).ToString();
        }

        #endregion

        #region Ui相关

        private CuiElement DanTextC(string parent, string Nam, string text, string anchorMin, string anchorMax, int DX = 11, TextAnchor align = TextAnchor.UpperLeft)
        { return new CuiElement { Parent = parent, Name = Nam, Components = { new CuiTextComponent { Text = text, FontSize = DX, Color = "1 1 1 1", Align = align }, new CuiRectTransformComponent { AnchorMin = anchorMin, AnchorMax = anchorMax }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "-.5 .5" } } }; }
        private CuiElement DanText(string parent, string Nam, string text, string anchorMin, string anchorMax, int DX = 11, TextAnchor align = TextAnchor.UpperLeft)
        { return new CuiElement { Parent = parent, Name = Nam, FadeOut = 0.5f, Components = { new CuiTextComponent { Text = text, FontSize = DX, Color = "1 1 1 1", Align = align }, new CuiRectTransformComponent { AnchorMin = anchorMin, AnchorMax = anchorMax }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "-.5 .5" } } }; }
        private CuiElement CuiText(string parent, string text, string anchorMin, string anchorMax, string yanst = "1 1 1 1", int DX = 12, TextAnchor align = TextAnchor.MiddleCenter)
        { return new CuiElement { Parent = parent, Components = { new CuiTextComponent { Text = text, FontSize = DX, Color = yanst, Align = align }, new CuiRectTransformComponent { AnchorMin = anchorMin, AnchorMax = anchorMax }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "-.5 .5" } } }; }
        private CuiElement YxText(string parent, string text, string anchorMin, string anchorMax, int DX = 11, TextAnchor align = TextAnchor.UpperLeft)
        { return new CuiElement { Parent = parent, Components = { new CuiTextComponent { Text = text, FontSize = DX, Color = "1 1 1 1", Align = align }, new CuiRectTransformComponent { AnchorMin = anchorMin, AnchorMax = anchorMax }, new CuiOutlineComponent { Color = "0 0 0 1", Distance = "-.5 .5" } } }; }
        private CuiPanel CreatePanel(string anchorMin, string anchorMax, string color = "0 0 0 0", bool SB = false)
        { return new CuiPanel { Image = { Color = color }, RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax }, CursorEnabled = SB }; }
        private CuiButton GButton(string ml)
        { return new CuiButton { Button = { Command = ml, Color = "0 0 0 0" }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }, Text = { Text = null, FontSize = 12, Align = TextAnchor.MiddleCenter } }; }
        private CuiButton DMButton(string dz, string ml, string Zx, string zd, string wb = null, string ys = "0 0 0 0", int dx = 12, TextAnchor ddd = TextAnchor.MiddleCenter)
        { return new CuiButton { Button = { Close = dz, Command = ml, Color = ys }, RectTransform = { AnchorMin = Zx, AnchorMax = zd }, Text = { Text = wb, FontSize = dx, Align = ddd } }; }
        private void Remove_UI(BasePlayer player, int r_num)
        {
            UIHandler data;
            if (_playersUIHandler.TryGetValue(player.userID, out data))
            {
                if (data._UI_List.Contains(r_num))
                {
                    if (data._Remove.Contains(r_num))
                    {
                        data._Remove.Remove(r_num);
                    }
                    else
                    {
                        CuiHelper.DestroyUi(player, $"_Message{r_num}");
                        data._UI_List.Remove(r_num);
                    }
                }
            }
        }
        private void Destroy_All(BasePlayer player)
        {
            UIHandler data;
            if (_playersUIHandler.TryGetValue(player.userID, out data))
            {
                foreach (var item in data._UI_List)
                {
                    CuiHelper.DestroyUi(player, $"_Message{item}");
                }
                data.num = 0;
                data._Remove.Clear();
                data._UI_List.Clear();
            }
        }
        private Dictionary<ulong, UIHandler> _playersUIHandler = new Dictionary<ulong, UIHandler>();
        private UIHandler GetUiInfo(BasePlayer player)
        {
            UIHandler value;
            if (!_playersUIHandler.TryGetValue(player.userID, out value))
            {
                _playersUIHandler[player.userID] = new UIHandler();
                return _playersUIHandler[player.userID];
            }
            return value;
        }
        private class UIHandler
        {
            public List<int> _UI_List = new List<int>();
            public HashSet<int> _Remove = new HashSet<int>();
            public int num;
        }

        private string _mianbanyou_A = ".745 .2";
        private string _mianbanyou_B = ".99 .7";

        public void ControlMenuUi(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "_ControlMenuUi");

            var elements = new CuiElementContainer();

            var Menu = elements.Add(CreatePanel(_mianbanyou_A, _mianbanyou_B, "0.247 0.247 0.247 1", true), "Overlay", "_ControlMenuUi");

            elements.Add(CreatePanel("0 .93", "1 1", "0.149 0.153 0.153 1"), Menu);



            elements.Add(CuiText(Menu, string.Format(configData.Lang_settings["DIY"]), ".05 .93", "1 1", "1 1 1 1", 14, TextAnchor.MiddleLeft));

            elements.Add(DMButton(Menu, "deathmessage.ove", ".93 .94", ".99 .99", "✕", "0.827 0.271 0.173 1"), Menu);

            var bjpy = elements.Add(CreatePanel("0 .8", "1 .9", "0 0 0 0"), Menu);

            elements.Add(CuiText(bjpy, string.Format(configData.Lang_settings["FloatUILocation"]), "0 0", ".25 1", "1 1 1 1", 12, TextAnchor.MiddleRight));

            elements.Add(DMButton(null, "deathmessage.ui wz 0 2 -0.02", ".3 .2", ".37 .8", "◄", "0 0 0 .5"), bjpy);
            elements.Add(DMButton(null, "deathmessage.ui wz 0 2 0.02", ".48 .2", ".55 .8", "►", "0 0 0 .5"), bjpy);
            elements.Add(DMButton(null, "deathmessage.ui wz 1 3 0.02", ".66 .2", ".73 .8", "▲", "0 0 0 .5"), bjpy);
            elements.Add(DMButton(null, "deathmessage.ui wz 1 3 -0.02", ".84 .2", ".91 .8", "▼", "0 0 0 .5"), bjpy);

            var bjdx = elements.Add(CreatePanel("0 .65", "1 .75", "0 0 0 0"), Menu);

            elements.Add(CuiText(bjdx, string.Format(configData.Lang_settings["LengthWidth"]), "0 0", ".25 1", "1 1 1 1", 12, TextAnchor.MiddleRight));

            elements.Add(DMButton(null, "deathmessage.ui zy 0 2 0.02", ".3 .2", ".37 .8", "↔", "0 0 0 .5"), bjdx);
            elements.Add(DMButton(null, "deathmessage.ui zy 0 2 -0.02", ".48 .2", ".55 .8", "⇄", "0 0 0 .5"), bjdx);

            elements.Add(DMButton(null, "deathmessage.ui zy 1 3 0.02", ".66 .2", ".73 .8", "↕", "0 0 0 .5"), bjdx);
            elements.Add(DMButton(null, "deathmessage.ui zy 1 3 -0.02", ".84 .2", ".91 .8", "⇅", "0 0 0 .5"), bjdx);


            var zidx = elements.Add(CreatePanel("0 .5", "1 .6", "0 0 0 0"), Menu);

            elements.Add(CuiText(zidx, string.Format(configData.Lang_settings["FontSize"]), "0 0", ".25 1", "1 1 1 1", 12, TextAnchor.MiddleRight));

            elements.Add(DMButton(null, "deathmessage.ui dan 6 1", ".3 .2", ".37 .8", "+", "0 0 0 .5", 14), zidx);
            elements.Add(DMButton(null, "deathmessage.ui dan 6 -1", ".48 .2", ".55 .8", "-", "0 0 0 .5"), zidx);

            var zijj = elements.Add(CreatePanel("0 .35", "1 .45", "0 0 0 0"), Menu);

            elements.Add(CuiText(zijj, string.Format(configData.Lang_settings["IntervalStretch"]), "0 0", ".25 1", "1 1 1 1", 12, TextAnchor.MiddleRight));

            elements.Add(DMButton(null, "deathmessage.ui dan 5 0.01", ".3 .2", ".37 .8", "+", "0 0 0 .5"), zijj);
            elements.Add(DMButton(null, "deathmessage.ui dan 5 -0.01", ".48 .2", ".55 .8", "-", "0 0 0 .5"), zijj);

            var zits = elements.Add(CreatePanel("0 .2", "1 .3", "0 0 0 0"), Menu);

            elements.Add(CuiText(zits, string.Format(configData.Lang_settings["DisplayNumber"]), "0 0", ".25 1", "1 1 1 1", 12, TextAnchor.MiddleRight));

            elements.Add(DMButton(null, "deathmessage.ui dan 4 1", ".3 .2", ".37 .8", "+", "0 0 0 .5"), zits);
            elements.Add(DMButton(null, "deathmessage.ui dan 4 -1", ".48 .2", ".55 .8", "-", "0 0 0 .5"), zits);


            var ziwz = elements.Add(CreatePanel("0 .05", "1 .15", "0 0 0 0"), Menu);

            elements.Add(CuiText(ziwz, string.Format(configData.Lang_settings["FontPosition"]), "0 0", ".25 1", "1 1 1 1", 12, TextAnchor.MiddleRight));

            elements.Add(DMButton(null, "deathmessage.ui dan 7 1", ".3 .2", ".37 .8", "∞", "0 0 0 .5"), ziwz);

            if (_mianbanyou_A != ".745 .2")
            {
                elements.Add(DMButton(null, "deathmessage.ui pzy", ".84 .35", ".91 .45", "◨", "0 0 0 .5", 20), Menu);
            }
            else
            {
                elements.Add(DMButton(null, "deathmessage.ui pzy", ".84 .35", ".91 .45", "◧", "0 0 0 .5", 20), Menu);
            }
            elements.Add(DMButton(null, "deathmessage.ui res", ".68 .05", ".93 .22", string.Format(configData.Lang_settings["Reset"]), "0.129 0.373 0.573 .8", 20), Menu);

            CuiHelper.AddUi(player, elements);
        }
        private void UpUIMessage(BasePlayer player, string Stext)
        {
            var data = GetUiInfo(player);
            var elements = new CuiElementContainer();
            if (data._UI_List.Count == 0 || data.num >= damoData.pos[4])
            {
                data.num = 0;
                data._Remove.Clear();
            }
            if (data._UI_List.Contains(data.num))
            {
                CuiHelper.DestroyUi(player, $"_Message{data.num}");
                data._UI_List.Remove(data.num);
                data._Remove.Add(data.num);
            }
            int r_num = data.num;
            elements.Add(DanText("Hud", $"_Message{data.num}", Stext, $"{damoData.pos[0]} {damoData.pos[1] - damoData.pos[5] * data.num}", $"{damoData.pos[2]} {damoData.pos[3] - damoData.pos[5] * data.num}", (int)damoData.pos[6], _textAnchor));
            elements.Add(GButton("deathmessage.cmd"), $"_Message{data.num}");
            data._UI_List.Add(data.num);
            data.num++;
            CuiHelper.AddUi(player, elements);
            timer.Once(configData.Other_settings.Ui_time, () =>
            {
                if (player != null)
                {
                    Remove_UI(player, r_num);
                }
            });
        }

        private List<string> TempText = new List<string>
        {
            {"<color=#66FF00>Terry</color> <color=#66FFFF>Assault Rifle</color> Kill <color=#FFFF00>Red Mary</color> <color=#FF9900>15</color> m"},
            {"<color=#66FF00>Billy.King</color> <color=#66FFFF>Double Barrel Shotgun</color> Kill <color=#FFFF00>Terry</color> <color=#6699FF>Head</color> <color=#FF9900>5</color> m"},
            {"<color=#66FF00>Grea Kevin</color> <color=#66FFFF>Compound Bow</color> Kill <color=#FFFF00>John Wilson</color> <color=#6699FF>Body</color> <color=#FF9900>20</color> m"},
            {"<color=#66FF00>Edward Adam Davis</color> <color=#66FFFF>L96 Rifle</color> Kill <color=#FFFF00>Guy de Maupassant</color> <color=#FF9900>60</color> m"},
            {"<color=#66FF00>Francisco Franco</color> <color=#66FFFF>Custom SMG</color> Kill <color=#FFFF00>bear</color> <color=#FF9900>7</color> m"},
            {"<color=#66FF00>bear</color> Kill <color=#FFFF00>Diego Rodrigueez de Silva y Velasquez</color> <color=#FF9900>3</color> m"},
        };
        private void UpUIMessageText(BasePlayer player)
        {
            UIHandler data;
            if (_playersUIHandler.TryGetValue(player.userID, out data))
            {
                data.num = 0;
                foreach (var item in data._UI_List)
                {
                    CuiHelper.DestroyUi(player, $"_Message{item}");
                }
            }
            timer.Repeat(0.01f, (int)damoData.pos[4], () =>
            {
                if (player != null)
                {
                    UpUIMessageRE(player);
                }
            });

        }
        private void UpUIMessageRE(BasePlayer player)
        {
            _TempText++;
            var data = GetUiInfo(player);
            var elements = new CuiElementContainer();
            if (data._UI_List.Count == 0 || data.num >= damoData.pos[4])
            {
                data.num = 0;
                data._Remove.Clear();
            }
            if (data._UI_List.Contains(data.num))
            {
                CuiHelper.DestroyUi(player, $"_Message{data.num}");
                data._UI_List.Remove(data.num);
                data._Remove.Add(data.num);
            }
            elements.Add(DanTextC("Hud", $"_Message{data.num}", $"{TempText[_TempText]}", $"{damoData.pos[0]} {damoData.pos[1] - damoData.pos[5] * data.num}", $"{damoData.pos[2]} {damoData.pos[3] - damoData.pos[5] * data.num}", (int)damoData.pos[6], _textAnchor));
            elements.Add(DMButton(null, null, "0 0", "1 1", null, "0 0 0 .5"), $"_Message{data.num}");
            data._UI_List.Add(data.num);
            data.num++;
            CuiHelper.AddUi(player, elements);
            _TempText++;
            if (_TempText >= TempText.Count)
            {
                _TempText = 0;
            }
        }
        #endregion

        #region 命令相关
        private void DeathMessageCommand(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = (BasePlayer)iplayer.Object;
            string Text = null;
            if (args.Length == 1)
            {
                if (args[0].ToLower() == "diy")
                {
                    if (!permission.UserHasPermission(player.UserIDString, Permission_registration))
                    {
                        Text = string.Format(configData.Lang_settings["NoPermission"]);
                        rust.SendChatMessage(player, configData.Lang_settings["ChatTitle"], Text, configData.Other_settings.Chat_Icon);
                        return;
                    }
                    ControlMenuUi(player);
                }

            }
            else
            {
                if (_DeathMessageType.Contains(player.userID))
                {
                    _DeathMessageType.Remove(player.userID);
                    if (configData.Lang_settings.ContainsKey("MessageToFloatUI"))
                    {
                        Text = string.Format(configData.Lang_settings["MessageToFloatUI"]);
                        rust.SendChatMessage(player, configData.Lang_settings["ChatTitle"], Text, configData.Other_settings.Chat_Icon);
                    }
                }
                else
                {
                    _DeathMessageType.Add(player.userID);
                    Destroy_All(player);
                    if (configData.Lang_settings.ContainsKey("MessageTochat"))
                    {
                        Text = string.Format(configData.Lang_settings["MessageTochat"]);
                        rust.SendChatMessage(player, configData.Lang_settings["ChatTitle"], Text, configData.Other_settings.Chat_Icon);
                    }
                }
            }
        }


        [ConsoleCommand("deathmessage.ui")]
        private void cmdDeathMessageui(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, Permission_registration))
            {
                var Text = string.Format(configData.Lang_settings["NoPermission"]);
                rust.SendChatMessage(player, configData.Lang_settings["ChatTitle"], Text, configData.Other_settings.Chat_Icon);
                return;
            }
            if (arg.Args[0] == "pzy")
            {
                if (_mianbanyou_A == ".745 .2")
                {
                    _mianbanyou_A = ".01 .2";
                    _mianbanyou_B = ".245 .7";
                }
                else
                {
                    _mianbanyou_A = ".745 .2";
                    _mianbanyou_B = ".99 .7";
                }
                ControlMenuUi(player);
                return;
            }
            switch (arg.Args[0])
            {
                case "wz":
                    float _value = float.Parse(arg.Args[3]);
                    damoData.pos[int.Parse(arg.Args[1])] += _value;
                    damoData.pos[int.Parse(arg.Args[2])] += _value;
                    break;
                case "zy":
                    _value = float.Parse(arg.Args[3]);
                    damoData.pos[int.Parse(arg.Args[1])] -= _value;
                    damoData.pos[int.Parse(arg.Args[2])] += _value;
                    break;
                case "dan":
                    _value = float.Parse(arg.Args[2]);
                    int key = int.Parse(arg.Args[1]);
                    damoData.pos[key] += _value;

                    switch (key)
                    {
                        case 7:
                            if (damoData.pos[key] > 2)
                            {
                                damoData.pos[key] = 0;
                            }
                            Get_textAnchor();
                            break;
                        case 4:
                            if (damoData.pos[key] < 1)
                            {
                                damoData.pos[key] = 1;
                            }
                            break;
                        case 5:
                            if (damoData.pos[key] < 0.01)
                            {
                                damoData.pos[key] = 0.01f;
                            }
                            break;
                        case 6:
                            if (damoData.pos[key] < 1)
                            {
                                damoData.pos[key] = 1;
                            }
                            break;
                    }
                    break;
                case "res":
                    damoData.pos = new float[] { 0.6f, 0.97f, 1f, 1f, 6f, 0.03f, 18, 0 };
                    Get_textAnchor();
                    break;

            }
            UpUIMessageText(player);

        }
        [ConsoleCommand("deathmessage.ove")]
        private void cmdDeathOver(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            Destroy_All(player);
        }
        [ConsoleCommand("deathmessage.cmd")]
        private void cmdDeathMessage(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            ulong userID = player.userID;
            _DeathMessageType.Add(userID);
            string Text = null;
            if (configData.Lang_settings.ContainsKey("ButtonSwitch"))
            {
                Text = string.Format(configData.Lang_settings["ButtonSwitch"], configData.Other_settings.switching_time);
                rust.SendChatMessage(player, configData.Lang_settings["ChatTitle"], Text, configData.Other_settings.Chat_Icon);
                timer.Once(configData.Other_settings.switching_time, () =>
                {
                    _DeathMessageType.Remove(userID);
                });
            }
            Destroy_All(player);
        }
        #endregion
    
        #region Discord Functions

        private enum BroadcastTo
        {
            InGame,
            Discord,
            Both,
            Neither
        }

        private BroadcastTo GetNPCEnableState(){
            if(configData.Enable_settings.Enable_NPC && configData.Discord_settings.Enable_NPC)
                return BroadcastTo.Both;

            if(configData.Enable_settings.Enable_NPC && !configData.Discord_settings.Enable_NPC)
                return BroadcastTo.InGame;
            
            if(!configData.Enable_settings.Enable_NPC && configData.Discord_settings.Enable_NPC)
                return BroadcastTo.Discord;
            
            return BroadcastTo.Neither;
        }

        private BroadcastTo GetAnimalEnableState(){
            if(configData.Enable_settings.Enable_Animal && configData.Discord_settings.Enable_Animal)
                return BroadcastTo.Both;

            if(configData.Enable_settings.Enable_Animal && !configData.Discord_settings.Enable_Animal)
                return BroadcastTo.InGame;
            
            if(!configData.Enable_settings.Enable_Animal && configData.Discord_settings.Enable_Animal)
                return BroadcastTo.Discord;
            
            return BroadcastTo.Neither;
        }

        private BroadcastTo GetEntityEnableState(){
            if(configData.Enable_settings.Enable_Entity && configData.Discord_settings.Enable_Entity)
                return BroadcastTo.Both;

            if(configData.Enable_settings.Enable_Entity && !configData.Discord_settings.Enable_Entity)
                return BroadcastTo.InGame;
            
            if(!configData.Enable_settings.Enable_Entity && configData.Discord_settings.Enable_Entity)
                return BroadcastTo.Discord;
            
            return BroadcastTo.Neither;
        }
        
        private void BroadcastToDiscord(string message)
        {
            if(!string.IsNullOrEmpty(configData.Discord_settings.DiscordWebHookUrl) 
            && configData.Discord_settings.DiscordWebHookUrl != "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks")
            {
                webrequest.Enqueue(configData.Discord_settings.DiscordWebHookUrl, 
                (new DiscordMessage(ClearFormatting(message), configData.Discord_settings.DiscordBotName, configData.Discord_settings.DiscordAvatarLink)).ToJson(), 
                DiscordSendMessageCallback, this, RequestMethod.POST, _headers);
            }
        }

        private readonly Dictionary<string, string> _headers = new Dictionary<string, string>
		{
			{"Content-Type", "application/json"}
		};

        private class DiscordMessage
		{
			[JsonProperty("content")] private string Content { get; set; }
			[JsonProperty("username")] private string Username { get; set; }
			[JsonProperty("avatar_url")] private string AvatarURL { get; set; }

			public DiscordMessage(string content, string username, string avatarurl)
			{
				Content = content;
                Username = username;
                AvatarURL = avatarurl;
			}

			public DiscordMessage AddContent(string content)
			{
				Content = content;
				return this;
			}

			public string GetContent()
			{
				return Content;
			}

			public string ToJson()
			{
				return JsonConvert.SerializeObject(this, Formatting.None,
					new JsonSerializerSettings {NullValueHandling = NullValueHandling.Ignore});
			}
		}

        private void DiscordSendMessageCallback(int code, string message)
		{
			switch (code)
			{
				case 204:
				{
					//ignore
					return;
				}
				case 401:
					var objectJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(message);
					int messageCode;
					if (objectJson["code"] != null && int.TryParse(objectJson["code"].ToString(), out messageCode))
						if (messageCode == 50027)
						{
							PrintError("Invalid Webhook Token");
							return;
						}

					break;
				case 404:
					PrintError("Invalid Webhook (404: Not Found)");
					return;
				case 405:
					PrintError("Invalid Webhook (405: Method Not Allowed)");
					return;
				case 429:
					message =
						"You are being rate limited. To avoid this try to increase queue interval in your config file.";
					break;
				case 500:
					message = "There are some issues with Discord server (500 Internal Server Error)";
					break;
				case 502:
					message = "There are some issues with Discord server (502 Bad Gateway)";
					break;
				default:
					message = $"DiscordSendMessageCallback: code = {code} message = {message}";
					break;
			}

			PrintError(message);
		}
        
        public static string ClearFormatting(string msg)
        {
            string unformatted = Regex.Replace(msg, "<.*?>", string.Empty);

            return unformatted;
        }

        #endregion
    }
}
