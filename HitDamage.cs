using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("HitDamage", "Sara", "1.0.5")]
    public class HitDamage : RustPlugin
    {   
        
        private void MyCommand (BasePlayer player, string command)
        {
            if (command != config.toggleCmd) return;

            if (!permission.UserHasPermission(player.UserIDString, "hitdamage.use")) return;

            if (permission.UserHasPermission(player.UserIDString, "hitdamage.noshow"))
            {
                permission.RevokeUserPermission(player.UserIDString, "hitdamage.noshow");
                SendReply(player, "Hit damage turned on.");
            }
            else 
            {
                permission.GrantUserPermission(player.UserIDString, "hitdamage.noshow", this);
                SendReply(player, "Hit damage turned off.");
            }

        }


        private List<BasePlayer> ignore = new List<BasePlayer>{};  

        void OnServerInitialized()
        {   
            if (config.toggleCmd == null) 
            {
                config.toggleCmd = "hit";
                SaveConfig();
            }

            cmd.AddChatCommand(config.toggleCmd, this, "MyCommand");

            if (config.perm)
            {
                permission.RegisterPermission("hitdamage.use", this);
                permission.RegisterPermission("hitdamage.noshow", this);
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.IsAdmin)
                    ignore.Add(player);    
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsAdmin)
                ignore.Add(player);
        }

        void OnEntityTakeDamage(BaseAnimalNPC victim, HitInfo info)
        {
            var initiator = info.InitiatorPlayer;

            if (initiator == null || initiator.IsNpc)
            {
                return;
            }

            if (config.perm && !permission.UserHasPermission(initiator.UserIDString, "hitdamage.use"))
            {
                return;
            }

            if (config.perm && permission.UserHasPermission(initiator.UserIDString, "hitdamage.noshow"))
            {
                return;
            }

            try {

                if (victim != null)
                {
                    float boneMultiplier = info.damageProperties.GetMultiplier(info.boneArea);

                    float totalDamage = info.damageTypes.Total();

                    float damageCalculated = totalDamage * boneMultiplier;

                    var color = config.colors.low;
                    if (damageCalculated > config.thresholds.mid)
                    {
                        color = config.colors.mid;
                    }
                    if (damageCalculated > config.thresholds.high)
                    {
                        color = config.colors.high;
                    }

                    int size = config.size;

                    if (Vector3.Distance(victim.transform.position, initiator.transform.position) < 16f)
                    {
                        size = config.closeSize;
                    }

                    int vysledok = (int)Math.Round(damageCalculated);

                    var pozicia = info.PointEnd;
                    pozicia.y = pozicia.y + (UnityEngine.Random.Range(85, 100) / 100);
                    pozicia.x = pozicia.x + (UnityEngine.Random.Range(-120, 120) / 100);
                    
                    SetFlag(info.InitiatorPlayer, true);
                    info.InitiatorPlayer.SendConsoleCommand("ddraw.text", 0.7f, Color.black, pozicia, $"<size={size}.5>{vysledok}</size>");
                    info.InitiatorPlayer.SendConsoleCommand("ddraw.text", 0.7f, Color.white, pozicia, $"<size={size}><color={color}>{vysledok}</color></size>");
                    SetFlag(info.InitiatorPlayer, false);

                }
            }
            catch 
            {
                //
            }
        }


        void OnEntityTakeDamage(BasePlayer victim, HitInfo info)
        {
            var initiator = info.InitiatorPlayer;

            if (initiator == null || initiator.IsNpc)
            {
                return;
            }

            if (config.perm && !permission.UserHasPermission(initiator.UserIDString, "hitdamage.use"))
            {
                return;
            }

            if (config.perm && permission.UserHasPermission(initiator.UserIDString, "hitdamage.noshow"))
            {
                return;
            }

            try {
                
                if (victim.IsNpc && config.npcs)
                {

                    float boneMultiplier = info.damageProperties.GetMultiplier(info.boneArea);

                    float totalDamage = info.damageTypes.Total();

                    float damageCalculated = totalDamage * boneMultiplier;


                    

                    var color = config.colors.low;
                    if (damageCalculated > config.thresholds.mid)
                    {
                        color = config.colors.mid;
                    }
                    if (damageCalculated > config.thresholds.high)
                    {
                        color = config.colors.high;
                    }

                    int size = config.size;

                    if (Vector3.Distance(victim.transform.position, initiator.transform.position) < 16f)
                    {
                        size = config.closeSize;
                    }

                    int vysledok = (int)Math.Round(damageCalculated);

                    var pozicia = info.PointEnd;
                    pozicia.y = pozicia.y + (UnityEngine.Random.Range(85, 100) / 100);
                    pozicia.x = pozicia.x + (UnityEngine.Random.Range(-120, 120) / 100);

                    SetFlag(info.InitiatorPlayer, true);
                    info.InitiatorPlayer.SendConsoleCommand("ddraw.text", 0.7f, Color.black, pozicia, $"<size={size}.5>{vysledok}</size>");
                    info.InitiatorPlayer.SendConsoleCommand("ddraw.text", 0.7f, Color.white, pozicia, $"<size={size}><color={color}>{vysledok}</color></size>");
                    SetFlag(info.InitiatorPlayer, false);

                }
                

                if (!victim.IsNpc && config.players)
                {
                    
                    float protection = 0f;

                    var itemColection = headArmor;

                    if (info.boneArea == HitArea.Chest || info.boneArea == HitArea.Stomach || info.boneArea == HitArea.Arm)
                    {
                        itemColection = chestArmor;
                    }

                    if (info.boneArea == HitArea.Leg || info.boneArea == HitArea.Foot)
                    {
                        itemColection = legsArmor;
                    }



                    foreach (var item in victim.inventory.containerWear.itemList)
                    {
                        if (itemColection.ContainsKey(item.info.itemid))
                        {
                            if (info.damageTypes.IsMeleeType())
                            {
                                protection += itemColection[item.info.itemid].melee;
                            }
                            else
                            {
                                protection += itemColection[item.info.itemid].projectile;
                            }
                        }
                    }

                    float boneMultiplier = info.damageProperties.GetMultiplier(info.boneArea);

                    float totalDamage = info.damageTypes.Total();

                    float damageCalculated = (totalDamage * boneMultiplier) - (protection * (totalDamage * boneMultiplier) / 100);


                    

                    var color = config.colors.low;
                    if (damageCalculated > config.thresholds.mid)
                    {
                        color = config.colors.mid;
                    }
                    if (damageCalculated > config.thresholds.high)
                    {
                        color = config.colors.high;
                    }

                    int size = config.size;

                    if (Vector3.Distance(victim.transform.position, initiator.transform.position) < 16f)
                    {
                        size = config.closeSize;
                    }

                    int vysledok = (int)Math.Round(damageCalculated);

                    var pozicia = info.PointEnd;
                    pozicia.y = pozicia.y + (UnityEngine.Random.Range(85, 100) / 100);
                    pozicia.x = pozicia.x + (UnityEngine.Random.Range(-120, 120) / 100);

                    SetFlag(info.InitiatorPlayer, true);
                    info.InitiatorPlayer.SendConsoleCommand("ddraw.text", 0.7f, Color.black, pozicia, $"<size={size}.5>{vysledok}</size>");
                    info.InitiatorPlayer.SendConsoleCommand("ddraw.text", 0.7f, Color.white, pozicia, $"<size={size}><color={color}>{vysledok}</color></size>");
                    SetFlag(info.InitiatorPlayer, false);
                    var victimEyes = new Vector3(victim.eyes.position.x, victim.eyes.position.y + 0.2f, victim.eyes.position.z);

                    if (victim.IsWounded())
                    {
                        victimEyes = victim.eyes.worldCrawlingPosition;
                    }

                    NextTick(() => {
                        if (victim.IsWounded())
                        {   
                            SetFlag(info.InitiatorPlayer, true);
                            info.InitiatorPlayer.SendConsoleCommand("ddraw.text", 1f, Color.black, victimEyes, $"<size=23>WOUNDED</size>");
                            info.InitiatorPlayer.SendConsoleCommand("ddraw.text", 1f, Color.white, victimEyes, $"<size=23>WOUNDED</size>");
                            SetFlag(info.InitiatorPlayer, false);
                        }
                        if (victim.IsDead())
                        {   
                            SetFlag(info.InitiatorPlayer, true);
                            info.InitiatorPlayer.SendConsoleCommand("ddraw.text", 1f, Color.black, victimEyes, $"<size=23.5>DEAD</size>");
                            info.InitiatorPlayer.SendConsoleCommand("ddraw.text", 1f, Color.white, victimEyes, $"<size=23>DEAD</size>");
                            SetFlag(info.InitiatorPlayer, false);
                        }
                    });
                }
            }
            catch 
            {
                //
            }
        }

        void SetFlag(BasePlayer player, bool setTo)
        {   
            if (ignore.Contains(player)) return;

            player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, setTo);
            player.SendNetworkUpdateImmediate();
        }



        class Protection
        {
            public float melee;
            public float projectile;
        }

        private Dictionary<int, Protection> headArmor = new Dictionary<int, Protection>
        {
            { -702051347,  new Protection
                {
                    melee = 10f,
                    projectile = 5f

                }
            },
            { -1022661119,  new Protection
                {
                    melee = 10f,
                    projectile = 10f

                }
            },
            { 1675639563,  new Protection
                {
                    melee = 10f,
                    projectile = 10f

                }
            },
            { -1903165497,  new Protection
                {
                    melee = 40f,
                    projectile = 25f

                }
            },
            { -23994173,  new Protection
                {
                    melee = 15f,
                    projectile = 15f

                }
            },
            { 850280505,  new Protection
                {
                    melee = 50f,
                    projectile = 20f

                }
            },
            { 1877339384,  new Protection
                {
                    melee = 15f,
                    projectile = 15f

                }
            },
            { 968019378,  new Protection
                {
                    melee = 50f,
                    projectile = 20f

                }
            },
            { -803263829,  new Protection
                {
                    melee = 50f,
                    projectile = 35f

                }
            },
            { -22883916,  new Protection
                {
                    melee = 60f,
                    projectile = 30f

                }
            },
            { -1569700847,  new Protection
                {
                    melee = 25f,
                    projectile = 20f

                }
            },
            { 1181207482,  new Protection
                {
                    melee = 80f,
                    projectile = 90f

                }
            },
            { -2012470695,  new Protection
                {
                    melee = 15f,
                    projectile = 15f

                }
            },
            { -194953424,  new Protection
                {
                    melee = 70f,
                    projectile = 50f

                }
            },
            { -1539025626,  new Protection
                {
                    melee = 25f,
                    projectile = 20f

                }
            },
            { -1518883088,  new Protection
                {
                    melee = 20f,
                    projectile = 15f

                }
            },
            { 1315082560,  new Protection
                {
                    melee = 60f,
                    projectile = 30f

                }
            },
            { -986782031,  new Protection
                {
                    melee = 60f,
                    projectile = 30f

                }
            },
            { 271048478,  new Protection
                {
                    melee = 60f,
                    projectile = 30f

                }
            },
            { 671063303,  new Protection
                {
                    melee = 80f,
                    projectile = 25f

                }
            },
            { 709206314,  new Protection
                {
                    melee = 60f,
                    projectile = 30f

                }
            },
            { -1478212975,  new Protection
                {
                    melee = 60f,
                    projectile = 30f

                }
            },
            { -2094954543,  new Protection
                {
                    melee = 25f,
                    projectile = 15f

                }
            },
            { -470439097,  new Protection
                {
                    melee = 30f,
                    projectile = 30f

                }
            },
            { 1266491000,  new Protection
                {
                    melee = 30f,
                    projectile = 30f

                }
            },
            { 86840834,  new Protection
                {
                    melee = 30f,
                    projectile = 30f

                }
            },
            { -1506417026,  new Protection
                {
                    melee = 30f,
                    projectile = 25f

                }
            },
            { -1785231475,  new Protection
                {
                    melee = 30f,
                    projectile = 25f

                }
            },
        };

        private Dictionary<int, Protection> chestArmor = new Dictionary<int, Protection>
        {
            { -470439097,  new Protection
                {
                    melee = 30f,
                    projectile = 30f

                }
            },
            { 1746956556,  new Protection
                {
                    melee = 40f,
                    projectile = 25f

                }
            },
            { 21402876,  new Protection
                {
                    melee = 5f,
                    projectile = 0f

                }
            },
            { 602741290,  new Protection
                {
                    melee = 10f,
                    projectile = 10f

                }
            },
            { 1266491000,  new Protection
                {
                    melee = 30f,
                    projectile = 30f

                }
            },
            { -1102429027,  new Protection
                {
                    melee = 70f,
                    projectile = 75f

                }
            },
            { 3222790,  new Protection
                {
                    melee = 10f,
                    projectile = 10f

                }
            },
            { 980333378,  new Protection
                {
                    melee = 40f,
                    projectile = 10f

                }
            },
            { 196700171,  new Protection
                {
                    melee = 15f,
                    projectile = 15f

                }
            },
            { 1751045826,  new Protection
                {
                    melee = 15f,
                    projectile = 20f

                }
            },
            { -1163532624,  new Protection
                {
                    melee = 20f,
                    projectile = 15f

                }
            },
            { 1366282552,  new Protection
                {
                    melee = 5f,
                    projectile = 5f

                }
            },
            { 935692442,  new Protection
                {
                    melee = 15f,
                    projectile = 15f

                }
            },
            { -763071910,  new Protection
                {
                    melee = 15f,
                    projectile = 20f

                }
            },
            { 1110385766,  new Protection
                {
                    melee = 20f,
                    projectile = 25f

                }
            },
            { 86840834,  new Protection
                {
                    melee = 30f,
                    projectile = 30f

                }
            },
            { -1506417026,  new Protection
                {
                    melee = 30f,
                    projectile = 25f

                }
            },
            { -2002277461,  new Protection
                {
                    melee = 25f,
                    projectile = 20f

                }
            },
            { -699558439,  new Protection
                {
                    melee = 25f,
                    projectile = 10f

                }
            },
            { -2025184684,  new Protection
                {
                    melee = 15f,
                    projectile = 15f

                }
            },
            { -48090175,  new Protection
                {
                    melee = 30f,
                    projectile = 20f

                }
            },
            { -1785231475,  new Protection
                {
                    melee = 30f,
                    projectile = 25f

                }
            },
            { 223891266,  new Protection
                {
                    melee = 15f,
                    projectile = 15f

                }
            },
            { -1108136649,  new Protection
                {
                    melee = 5f,
                    projectile = 10f

                }
            },
            { 1608640313,  new Protection
                {
                    melee = 10f,
                    projectile = 10f

                }
            },
            { 418081930,  new Protection
                {
                    melee = 40f,
                    projectile = 10f

                }
            },
        };

        private Dictionary<int, Protection> legsArmor = new Dictionary<int, Protection>
        {
            { -470439097,  new Protection
                {
                    melee = 30f,
                    projectile = 30f

                }
            },
            { 1746956556,  new Protection
                {
                    melee = 40f,
                    projectile = 25f

                }
            },
            { -1549739227,  new Protection
                {
                    melee = 10f,
                    projectile = 10f

                }
            },
            { -761829530,  new Protection
                {
                    melee = 5f,
                    projectile = 5f

                }
            },
            { 1992974553,  new Protection
                {
                    melee = 10f,
                    projectile = 10f

                }
            },
            { -1000573653,  new Protection
                {
                    melee = 0f,
                    projectile = 0f

                }
            },
            { 1266491000,  new Protection
                {
                    melee = 30f,
                    projectile = 30f

                }
            },
            { -1778159885,  new Protection
                {
                    melee = 70f,
                    projectile = 75f

                }
            },
            { 794356786,  new Protection
                {
                    melee = 5f,
                    projectile = 5f

                }
            },
            { 1722154847,  new Protection
                {
                    melee = 10f,
                    projectile = 10f

                }
            },
            { -1773144852,  new Protection
                {
                    melee = 10f,
                    projectile = 10f

                }
            },
            { 86840834,  new Protection
                {
                    melee = 30f,
                    projectile = 30f

                }
            },
            { -1506417026,  new Protection
                {
                    melee = 30f,
                    projectile = 25f

                }
            },
            { 237239288,  new Protection
                {
                    melee = 15f,
                    projectile = 15f

                }
            },
            { 1850456855,  new Protection
                {
                    melee = 25f,
                    projectile = 20f

                }
            },
            { -1695367501,  new Protection
                {
                    melee = 10f,
                    projectile = 10f

                }
            },
            { -1785231475,  new Protection
                {
                    melee = 30f,
                    projectile = 25f

                }
            },
            { 832133926,  new Protection
                {
                    melee = 40f,
                    projectile = 10f

                }
            },
        };

        #region Configuration

        private Configuration config;
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = Configuration.CreateConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        class Configuration
        {
            [JsonProperty("Require Permission")]
            public bool perm { get; set; }

            [JsonProperty("Toggle Command ('Require Permissions' needs to be true)")]
            public string toggleCmd { get; set; }

            [JsonProperty("Display on NPCs")]
            public bool npcs { get; set; }

            [JsonProperty("Display on Players")]
            public bool players { get; set; }

            [JsonProperty("Font size")]
            public int size { get; set; }

            [JsonProperty("Font size when closer")]
            public int closeSize { get; set; }

            [JsonProperty(PropertyName = "Damage Colors")]
            public Colors colors { get; set; }

            public class Colors
            {
                [JsonProperty("Low Damage")]
                public string low { get; set; }

                [JsonProperty("Medium Damage")]
                public string mid { get; set; }

                [JsonProperty("High Damage")]
                public string high { get; set; }
            }

            [JsonProperty(PropertyName = "Damage Tresholds")]
            public Thresholds thresholds { get; set; }

            public class Thresholds
            {

                [JsonProperty("Medium Damage at")]
                public float mid { get; set; }

                [JsonProperty("High Damage at")]
                public float high { get; set; }
            }

            public static Configuration CreateConfig()
            {
                return new Configuration
                {   
                    perm = false,
                    toggleCmd = "hit",
                    npcs = true,
                    players = true,
                    size = 20,
                    closeSize = 25,
                    colors = new HitDamage.Configuration.Colors
                    {
                        low = "#ffedd6",
                        mid = "#d69a4a",
                        high = "#ff3a00",
                    },

                    thresholds = new HitDamage.Configuration.Thresholds
                    { 
                        mid = 15,
                        high = 40
                    }
                };
            }
        }

        #endregion
    }
}

