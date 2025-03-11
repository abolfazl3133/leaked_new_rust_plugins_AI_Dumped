using System;
using System.Collections.Generic;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System.Linq;
using UnityEngine;
using Oxide.Core;
using Random = UnityEngine.Random;
using Rust;
using ProtoBuf;
using System.Collections;
using System.Text.RegularExpressions;
using Oxide.Core.Libraries;

namespace Oxide.Plugins
{
    [Info("XAntiCheat", "XAntiCheats", "2.0.3")]
    public class XAntiCheat : RustPlugin
    {
        static XAntiCheat fermens;
        const bool fermensEN = false;

        const bool debugmode = false;
        const string ipinnfourl = "https://ipinfo.io/{ip}/privacy?token={token}";
        const bool enablefull = true;
        const string ipinfosingup = "https://ipinfo.io/signup";

        #region message
        Dictionary<string, string> messages = new Dictionary<string, string>();
        private string GetMessage(string key, string userId)
        {
            return lang.GetMessage(key, this, userId);
        }
        #endregion

        #region CODE LOCK
        private static bool IsBanned(ulong userid)
        {
            return ServerUsers.Is(userid, ServerUsers.UserGroup.Banned);
        }

        private static bool IsImprisoned(ulong userid)
        {
            if (fermens.PrisonBitch == null) return false;
            return fermens.PrisonBitch.Call<bool>("ISIMPRISONED", userid);
        }

        private void OnCodeEntered(CodeLock codeLock, BasePlayer player, string code)
        {
            if (player == null) return;
            ulong owner = codeLock.OwnerID;
            if (owner == 0UL || code != codeLock.code) return;
            if (!codeLock.IsLocked())
            {
                codeLock.OwnerID = player.userID;
                return;
            }
            bool bann = IsBanned(owner);
            bool unprisoned = IsImprisoned(owner);

            if (bann || unprisoned)
            {
                ADDLOG("CODELOCK", messages["logCODELOCK"].Replace("{name}", player.displayName).Replace("{steamid}", player.UserIDString).Replace("{owner}", owner.ToString()), config.cODELOCK.webhook, 2);
                if (config.cODELOCK.enable)
                {
                    timer.Once(config.cODELOCK.seconds, () =>
                    {
                        BAN(player.UserIDString, config.cODELOCK.reason, config.cODELOCK.hours, player.displayName, config.cODELOCK.webhook);
                    });
                }
            }

        }

        object CanChangeCode(BasePlayer player, CodeLock codeLock, string newCode, bool isGuestCode)
        {
            codeLock.OwnerID = player.userID;
            return null;
        }
        #endregion

        #region КОНФИГ
        private static PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            config = PluginConfig.DefaultConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        class SILENT
        {
            [JsonProperty(fermensEN ? "Enable autoban?" : "Банить?")]
            public bool enable;

            [JsonProperty(fermensEN ? "Number of detections for a ban" : "Автобан (не знаешь, не трогай!)")]
            public int xdetects;

            [JsonProperty(fermensEN ? "Ban reason" : "Причина бана")]
            public string reason;

            [JsonProperty("Discord Webhook")]
            public string webhook;

            [JsonProperty(fermensEN ? "How many hours to ban?" : "На сколько часов бан?")]
            public int hours;
        }

        class SPIDER
        {
            [JsonProperty(fermensEN ? "Enable autoban?" : "Банить?")]
            public bool enable;

            [JsonProperty(fermensEN ? "Ban reason" : "Причина бана")]
            public string reason;

            [JsonProperty("Discord Webhook")]
            public string webhook;

            [JsonProperty(fermensEN ? "How many hours to ban?" : "На сколько часов бан?")]
            public int hours;
        }

        class FLY
        {
            [JsonProperty(fermensEN ? "Enable autoban?" : "Банить?")]
            public bool enable;

            [JsonProperty(fermensEN ? "Ban reason" : "Причина бана")]
            public string reason;

            [JsonProperty("Discord Webhook")]
            public string webhook;

            [JsonProperty(fermensEN ? "How many hours to ban?" : "На сколько часов бан?")]
            public int hours;
        }

        class SPINERBOT
        {
            [JsonProperty(fermensEN ? "Enable autoban?" : "Банить?")]
            public bool enable;

            [JsonProperty(fermensEN ? "Ban reason" : "Причина бана")]
            public string reason;

            [JsonProperty("Discord Webhook")]
            public string webhook;

            [JsonProperty(fermensEN ? "How many hours to ban?" : "На сколько часов бан?")]
            public int hours;
        }

        class NORECOIL
        {
            [JsonProperty(fermensEN ? "Enable autoban?" : "Банить?")]
            public bool enable;

            [JsonProperty(fermensEN ? "Ban reason" : "Причина бана")]
            public string reason;

            [JsonProperty(fermensEN ? "Delay in seconds before the ban" : "Задержка в секундах перед баном, после детекта")]
            public float seconds;

            [JsonProperty("Discord Webhook")]
            public string webhook;

            [JsonProperty(fermensEN ? "How many hours to ban?" : "На сколько часов бан?")]
            public int hours;
        }

        class HITMOD
        {
            [JsonProperty(fermensEN ? "Enable autoban?" : "Банить?")]
            public bool enable;

            [JsonProperty(fermensEN ? "Ban reason" : "Причина бана")]
            public string reason;

            [JsonProperty("Discord Webhook")]
            public string webhook;

            [JsonProperty(fermensEN ? "How many hours to ban?" : "На сколько часов бан?")]
            public int hours;
        }

        class CODELOCK
        {
            [JsonProperty(fermensEN ? "Enable autoban?" : "Банить?")]
            public bool enable;

            [JsonProperty(fermensEN ? "Ban reason" : "Причина бана")]
            public string reason;

            [JsonProperty("Discord Webhook")]
            public string webhook;

            [JsonProperty(fermensEN ? "Delay in seconds before the ban" : "Задержка в секундах перед баном, после детекта")]
            public float seconds;

            [JsonProperty(fermensEN ? "How many hours to ban?" : "На сколько часов бан?")]
            public int hours;
        }

        class TEAMBAN
        {
            [JsonProperty(fermensEN ? "Enable autoban?" : "Банить?")]
            public bool enable;

            [JsonProperty(fermensEN ? "Ban reason" : "Причина бана")]
            public string reason;

            [JsonProperty("Discord Webhook")]
            public string webhook;

            [JsonProperty(fermensEN ? "Ban if there are N bans in the team" : "Банить, если в команде N забаненных")]
            public int num;

            [JsonProperty(fermensEN ? "How many hours to ban?" : "На сколько часов бан?")]
            public int hours;
        }

        class ESPSTASH
        {
            [JsonProperty(fermensEN ? "Number of stashs" : "Количество")]
            public int amount;

            [JsonProperty("Discord Webhook")]
            public string webhook;

            [JsonProperty(fermensEN ? "Possible loot" : "Возможный лут")]
            public Dictionary<string, int> loots;
        }

        class DEBUGCAMERA
        {
            [JsonProperty(fermensEN ? "Enable autoban?" : "Банить?")]
            public bool enable;

            [JsonProperty("Discord Webhook")]
            public string webhook;
        }

        private class PluginConfig
        {
            [JsonProperty("-")]
            public string six;

            [JsonProperty("SteamAPI")]
            public string steampi;

            [JsonProperty(fermensEN ? "Silent Aim: setup" : "Silent Aim: настройка")]
            public SILENT sILENT;

            [JsonProperty(fermensEN ? "Spider: setup" : "Spider: настройка")]
            public SPIDER sPIDER;

            [JsonProperty(fermensEN ? "ESP SmallStash: setup" : "ESP SmallStash: настройка")]
            public ESPSTASH ESPStash;

            [JsonProperty(fermensEN ? "FLY: setup" : "FLY: настройка")]
            public FLY fLY;

            [JsonProperty(fermensEN ? "TEAMBAN: setup" : "TEAMBAN: настройка")]
            public TEAMBAN tEAMBAN;

            [JsonProperty(fermensEN ? "CODELOCK: setup" : "CODELOCK: настройка")]
            public CODELOCK cODELOCK;

            [JsonProperty(fermensEN ? "HITMOD: setup" : "HITMOD: настройка")]
            public HITMOD hITMOD;

            [JsonProperty(fermensEN ? "NORECOIL: setup" : "NORECOIL: настройка")]
            public NORECOIL nORECOIL;

            [JsonProperty(fermensEN ? "SPINERBOT: setup" : "SPINERBOT: настройка")]
            public SPINERBOT sPINERBOT;

            [JsonProperty("Debug camera")]
            public DEBUGCAMERA dEBUGCAMERA;

            [JsonProperty(fermensEN ? "Display steam account details when a player connect?" : "Отображать данные при подключении игрока?")]
            public bool show;

            [JsonProperty(fermensEN ? "Do not ban Steam players?" : "Не банить Steam игроков?")]
            public bool steamplayer;

            [JsonProperty(fermensEN ? "Send to jail if there is PrisonBitch plugin" : "Отправлять в тюрьму, если есть плагин PrisonBitch")]
            public bool prison;

            [JsonProperty(fermensEN ? "Ban not configured steam accounts?" : "Банить не настроеные аккаунты?")]
            public bool bannensatroyen;

            [JsonProperty(fermensEN ? "Ban accounts less than X days old" : "Банить аккаунты, которым меньше X дней")]
            public int banday;

            [JsonProperty(fermensEN ? "How long to ban new steam accounts (hours)" : "На сколько часов банить новые аккаунты")]
            public int bannewaccountday;

            [JsonProperty(fermensEN ? "Kick not configured steam accounts?" : "Кикать не настроенные аккаунты")]
            public bool kicknenastoyen;

            [JsonProperty(fermensEN ? "Kick private steam accounts?" : "Кикать приватные аккаунты")]
            public bool kickprivate;

            [JsonProperty(fermensEN ? "Kick players using VPN" : "Кикать игроков использующих VPN")]
            public bool kickvpn;

            [JsonProperty(fermensEN ? "Don't kick steam players for private, not configured or new account?" : "Не кикать лицухи?")]
            public bool steamkick;

            [JsonProperty(fermensEN ? "Don't ban steam players for new account?" : "Не банить лицушников за новые аккаунты?")]
            public bool steam;

            [JsonProperty(fermensEN ? "Write/save logs [0 - no | 1 - only bans | 2 - all]" : "Писать/сохранять логи [0 - нет | 1 - только баны | 2 - все]")]
            public int logspriority;

            [JsonProperty(fermensEN ? "Discord: Channel ID" : "Discord: ID канала")]
            public string discordid;

            [JsonProperty(fermensEN ? "Logs in language" : "Логи на языке")]
            public string lang { get; set; } = fermensEN ? "en" : "ru";

            [JsonProperty(fermensEN ? "Logs of hits from a firearm to the console" : " Логи попаданий с огнестрела в консоль")]
            public bool logs;

            [JsonProperty(fermensEN ? "IPINFO TOKEN" : "IPINFO ТОКЕН")]
            public string ipinfotoken;

            [JsonProperty("tt")]
            public string tt;

            [JsonProperty(fermensEN ? "Ban patterns" : "Шаблоны банов")]
            public Dictionary<string, string> pattern = new Dictionary<string, string>();

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    six = "-",
                    ipinfotoken = ipinfosingup,
                    kickvpn = false,
                    tEAMBAN = new TEAMBAN
                    {
                        enable = true,
                        hours = 168,
                        webhook = "",
                        reason = "\"bb with teammates\"",
                        num = 2
                    },
                    cODELOCK = new CODELOCK
                    {
                        enable = true,
                        hours = 336,
                        webhook = "",
                        reason = "\"Ban Detected!\"",
                        seconds = 75
                    },
                    sPIDER = new SPIDER
                    {
                        hours = 720,
                        webhook = "",
                        reason = "\"Cheat Detected! (2)\""
                    },
                    sILENT = new SILENT
                    {
                        xdetects = 7,
                        webhook = "",
                        hours = 1440,
                        reason = "\"Cheat Detected! (1)\""
                    },
                    fLY = new FLY
                    {
                        hours = 720,
                        webhook = "",
                        reason = "\"Cheat Detected! (3)\""
                    },
                    hITMOD = new HITMOD
                    {
                        enable = true,
                        hours = 720,
                        webhook = "",
                        reason = "\"Cheat Detected! (6)\""
                    },
                    sPINERBOT = new SPINERBOT
                    {
                        enable = true,
                        hours = 720,
                        webhook = "",
                        reason = "\"Cheat Detected! (10)\""
                    },
                    nORECOIL = new NORECOIL
                    {
                        enable = true,
                        hours = 720,
                        seconds = 30f,
                        webhook = "",
                        reason = "\"Cheat Detected! (11)\""
                    },
                    prison = true,
                    steamkick = true,
                    dEBUGCAMERA = new DEBUGCAMERA
                    {
                        enable = true,
                        webhook = ""
                    },
                    logs = false,
                    steam = true,
                    steampi = defaultsteamapi,
                    banday = 5,
                    kicknenastoyen = true,
                    kickprivate = true,
                    tt = "nothing",
                    bannensatroyen = false,
                    show = true,
                    bannewaccountday = 120,
                    steamplayer = false,
                    pattern = new Dictionary<string, string>
                    {
                        { "BAN.ACCOUNT", "ban {steamid} {reason} {time}" },
                        { "PRISON.ACCOUNT", "prison.add {steamid} {time} {reason}" },
                        { "EBSBAN.ACCOUNT", "ban {steamid} {time}h {reason}" }
                    },
                    logspriority = 2,
                    ESPStash = new ESPSTASH
                    {
                        amount = 100,
                        loots = new Dictionary<string, int>
                        {
                            { "rifle.ak", 1 },
                            { "rifle.bolt", 1 },
                            { "rifle.l96", 1 },
                            { "rifle.lr300", 1 },
                            { "rifle.semiauto", 1 },
                            { "wood", 10000 },
                            { "stones", 10000 },
                            { "metal.refined", 50 },
                            { "metal.fragments", 10000 },
                            { "metal.facemask", 1 },
                            { "scrap", 500 },
                        }
                    },
                    discordid = ""
                };
            }
        }
        #endregion

        #region WebHook
        private static void SendDiscordMessage(string reason, string desc, string webhook)
        {
            var embed = new Embed()
            .AddField(reason, desc, true);

            fermens.webrequest.Enqueue(webhook, new DiscordMessage("", embed).ToJson(), (code, response) => { },
            fermens,
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
            [JsonProperty("fields")] public List<Field> Fields { get; set; } = new List<Field>();

            public Embed AddField(string name, string value, bool inline)
            {
                Fields.Add(new Field(name, Regex.Replace(value, "<.*?>", string.Empty), inline));

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
        #endregion

        int nsnext = 0;
        [ChatCommand("ns")]
        private void COMMANSTASH(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            player.Teleport(stashContainers[nsnext].transform.position + Vector3.up * 1.5f);
            nsnext++;
            if (stashContainers.Count >= nsnext) nsnext = 0;
        }

        Vector3 lastshash = Vector3.zero;
        [ChatCommand("ls")]
        private void COMMALS(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            if (lastshash == Vector3.zero)
            {
                player.ChatMessage(GetMessage("adminLs", player.UserIDString));
                return;
            }
            player.Teleport(lastshash + Vector3.up * 1.5f);
        }

        [PluginReference] private Plugin PrisonBitch, EnhancedBanSystem;

        private static void BAN(string steamid, string reason, int time, string displayname, string webhook, bool checkteam = true)
        {
            if (!enablefull) return;

            ulong usteam = Convert.ToUInt64(steamid);
            if (config.steamplayer)
            {
                BasePlayer pl = BasePlayer.FindByID(usteam);
                if (pl != null)
                {
                    if (fermens.ISSTEAM(pl.Connection))
                    {
                        ADDLOG("STEAM_PLAYER", fermens.messages["logSTEAM_PLAYER"].Replace("{name}", pl.displayName).Replace("{steamid}", pl.UserIDString), webhook, 2);
                        return;
                    }
                }
            }

            ulong usteamid = Convert.ToUInt64(steamid);

            if (checkteam)
            {
                if (config.tEAMBAN.enable)
                {
                    BasePlayer basePlayer = BasePlayer.FindByID(usteam);
                    if (basePlayer != null && basePlayer.Team != null && basePlayer.Team.members.Count > 1)
                    {
                        int banned = basePlayer.Team.members.Count(x => IsBanned(x) || IsImprisoned(x));
                        if (banned >= config.tEAMBAN.num - 1)
                        {
                            foreach (var z in basePlayer.Team.members)
                            {
                                if (basePlayer.userID == z) continue;
                                string strid = z.ToString();
                                BasePlayer basePlayer2 = BasePlayer.FindByID(z);
                                string name = basePlayer2 != null ? basePlayer2.displayName : strid;
                                fermens.timer.Once(1f, () => BAN(z.ToString(), config.tEAMBAN.reason, config.tEAMBAN.hours, name, config.tEAMBAN.webhook, false));
                            }
                        }
                    }
                }
            }

            if (config.prison && prison)
            {
                if (!IsImprisoned(usteamid))
                {
                    ADDLOG("PRISON", fermens.messages["logPRISON"].Replace("{name}", displayname).Replace("{steamid}", steamid).Replace("{days}", (time * 1f / 24f).ToString("F1")).Replace("{reason}", reason), webhook, 1);
                    time = (int)(time * 60f);
                    fermens.Server.Command(config.pattern["PRISON.ACCOUNT"].Replace("{steamid}", steamid).Replace("{time}", time.ToString()).Replace("{reason}", reason));
                }
            }
            else
            {
                if (!IsBanned(usteamid))
                {
                    fermens.Server.Command(fermens.patterban.Replace("{steamid}", steamid).Replace("{time}", time.ToString()).Replace("{reason}", reason));
                    ADDLOG("BAN", fermens.messages["logBAN"].Replace("{name}", displayname).Replace("{steamid}", steamid).Replace("{days}", (time * 1f / 24f).ToString("F1")).Replace("{reason}", reason), webhook, 1);
                }
            }
        }

        const int flylimit = 2;
        const int spiderlimit = 2;


        private Dictionary<BasePlayer, ANTICHEAT> anticheatPlayers = new Dictionary<BasePlayer, ANTICHEAT>();

        class ANTICHEAT : MonoBehaviour
        {
            private int stash;
            private int silent;
            private int spider;
            private int fly;
            private float flyheight;

            BasePlayer player;
            private DateTime lastban;

            private DateTime firsthit;
            private DateTime hitmod;
            string lasthit;
            int hits;
            float distancehit;
            string weaponhit;

            public DateTime LastFires;
            public int fires;
            string weaponfire;
            float posfire;
            float posfirel;
            float posfirer;

            int norecoil;
            Vector3 lastshot;

            private int numdetecthit;

            bool macromove;
            Vector3 startshoots;

            Vector3 direction;

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                if (player == null || !debugmode && enablefull && (player.IsAdmin || fermens.permission.UserHasPermission(player.UserIDString, "XAntiCheat.allow")))
                {
                    Destroy(this);
                    return;
                }

                fermens.anticheatPlayers.Add(player, this);

                lasthit = "";
                silent = 0;
                weaponhit = "";
                weaponfire = "";
                if (config.dEBUGCAMERA.enable || debugmode) InvokeRepeating(nameof(TICK), 0f, 10f);
                direction = player.eyes.HeadRay().direction;
                InvokeRepeating(nameof(SILENTCLEAR), 0f, 120f);
                // InvokeRepeating(nameof(WH), 0f, 1f);
            }

            private void SILENTCLEAR()
            {
                flyiing = 1;
                RaycastHit hit;
                var raycast = Physics.Raycast(player.transform.position, Vector3.down, out hit, 20f);
                if (raycast)
                {
                    if (hit.distance > 4f && hit.distance < 5f && !player.isMounted && !player.IsSleeping() && !player.IsWounded())
                    {
                        Debug.LogWarning($"[{flyiing}] {player.displayName} {player.UserIDString} | {hit.collider.name} [{hit.distance} m.] {player.IsFlying}");
                        flyiingList.Add(player.transform.position);
                        //  InvokeRepeating(nameof(FLYING), 1f, 1f);
                    }

                }
                spider = 0;
                silent = 0;
                fly = 0;
                flyheight = 0;
            }

            private List<Vector3> flyiingList = new List<Vector3>();
            private int flyiing;

            public void ADDFLY(float distance)
            {
                if (distance <= flyheight) return;
                fly++;
                flyheight = distance;
                ADDLOG("FLY", fermens.messages["logDETECT"].Replace("{name}", player.displayName).Replace("{steamid}", player.UserIDString).Replace("{detect}", fly.ToString()).Replace("{detectlimit}", flylimit.ToString()), config.fLY.webhook, 2);
                if (config.fLY.enable && fly >= flylimit && lastban < DateTime.Now)
                {
                    BAN(player.UserIDString, config.fLY.reason, config.fLY.hours, player.displayName, config.fLY.webhook);
                    lastban = DateTime.Now.AddSeconds(10f);
                    flyheight = 0;
                    fly = 0;
                    return;
                }
            }

            bool norecoilbanned;
            public void ADDFIRE(string weapon)
            {
                Vector3 vector3 = player.transform.position;
                double sec = (DateTime.Now - LastFires).TotalSeconds;
                Vector3 current = player.eyes.HeadForward();
                direction = current;
                if (config.sPINERBOT.enable)
                {
                    if (current.y >= -0.984808 && current.y <= -0.9848076)
                    {
                        spiner++;
                        if (spiner >= 30)
                        {
                            BAN(player.UserIDString, config.sPINERBOT.reason, config.sPINERBOT.hours, player.displayName, config.sPINERBOT.webhook);
                            lastban = DateTime.Now.AddSeconds(10f);
                            spiner = 0;
                        }
                    }
                    else
                    {
                        spiner = 0;
                    }
                }

                if (fires == 0)
                {
                    macromove = false;
                    norecoil = 0;
                    sec = 0;
                    posfire = current.y;
                    posfirel = current.x;
                    posfirer = current.z;
                }
                else
                {
                    if (!macromove && startshoots != vector3)
                    {
                        macromove = true;
                    }
                }

                startshoots = vector3;
                float razn = Mathf.Abs(posfire - current.y);
                float raznl = Mathf.Abs(posfirel - current.x);
                float raznr = Mathf.Abs(posfirer - current.z);

                posfire = current.y;
                posfirel = current.x;
                posfirer = current.z;

                if (debugmode && fires > 0) Debug.Log($"{player.displayName} [#{fires.ToString()}][Y:{current.y}][x: {raznl.ToString()} | y: {razn.ToString()} | z: {raznr.ToString()}]");

                if (config.nORECOIL.enable && fires > 0 && razn == 0f && raznl == 0f && raznr == 0f && !norecoilbanned)
                {
                    norecoil++;
                    if (norecoil >= 10)
                    {
                        norecoilbanned = true;
                        fermens.timer.Once(config.nORECOIL.seconds, () =>
                        {
                            BAN(player.UserIDString, config.nORECOIL.reason, config.nORECOIL.hours, player.displayName, config.nORECOIL.webhook);
                            lastban = DateTime.Now.AddSeconds(10f);
                            norecoil = 0;
                        });
                    }
                }

                if (current.y < 0.9f && sec < 0.2f && razn <= 0.003f && raznl <= 0.003f && raznr <= 0.003f)
                {
                    fires++;
                    LastFires = DateTime.Now;
                    weaponfire = weapon;
                    if (IsInvoking(nameof(FIREEND))) CancelInvoke(nameof(FIREEND));
                }

                Invoke(nameof(FIREEND), 0.21f);
            }

            private void FIREEND()
            {
                norecoil = 0;
                fires = 0;
            }

            public void ADDHIT(string hitbone, string weapon, float distance)
            {
                if (IsInvalidHit(hitbone, distance))
                    return;

                UpdateHitData(hitbone, weapon, distance);

                if (hits >= 5)
                {
                    LogHitData();

                    if (ShouldBanPlayer())
                    {
                        BAN(player.UserIDString, config.hITMOD.reason, config.hITMOD.hours, player.displayName, config.hITMOD.webhook);
                        lastban = DateTime.Now.AddSeconds(10f);
                    }

                    CheckForSuspiciousHit();
                    CLEARHIT();
                }
            }

            private bool IsInvalidHit(string hitbone, float distance)
            {
                return hitbone == "N/A" || distance < 30f;
            }

            private void UpdateHitData(string hitbone, string weapon, float distance)
            {
                if (hitbone != lasthit && lasthit != "")
                {
                    CLEARHIT();
                    return;
                }

                if (hits == 0)
                {
                    hitmod = DateTime.Now;
                    firsthit = DateTime.Now;
                }
                hits++;

                lasthit = hitbone;
                distancehit += distance;
                if (hits >= 2) distancehit /= 2;
                if (!weaponhit.Contains(weapon)) weaponhit += (hits >= 2 ? ", " : string.Empty) + weapon;
            }

            private void LogHitData()
            {
                DateTime dateTime = new DateTime((DateTime.Now - firsthit).Ticks);
                ADDLOG("HITMOD", fermens.messages["logHITMod"].Replace("{name}", player.displayName).Replace("{steamid}", player.UserIDString).Replace("{hitbone}", lasthit).Replace("{average}", distancehit.ToString("F1")).Replace("{weaponhit}", weaponhit).Replace("{minutes}", dateTime.ToString("HH:mm:ss")), config.hITMOD.webhook, 2);
            }

            private bool ShouldBanPlayer()
            {
                return distancehit > 100 && (weaponhit.Contains("bow_hunting.entity") || weaponhit.Contains("crossbow.entity") || weaponhit.Contains("bow.compound") || weaponhit.Contains("pistol_eoka.entity"))
                    || distancehit > 65 && (weaponhit == "bow_hunting.entity" || weaponhit == "crossbow.entity" || weaponhit == "bow.compound")
                    || distancehit > 40 && weaponhit == "pistol_eoka.entity";
            }

            private void CheckForSuspiciousHit()
            {
                if (!weaponhit.Contains("l96.entity"))
                {
                    if ((DateTime.Now - hitmod).TotalMinutes < 10f)
                    {
                        numdetecthit++;
                        if (numdetecthit >= 3)
                        {
                            BAN(player.UserIDString, config.hITMOD.reason, config.hITMOD.hours, player.displayName, config.hITMOD.webhook);
                            lastban = DateTime.Now.AddSeconds(10f);
                        }
                    }
                    else
                    {
                        numdetecthit = 0;
                    }
                }

                hitmod = DateTime.Now;
            }

            private void CLEARHIT()
            {
                hits = 0;
                lasthit = "";
                distancehit = 0f;
                weaponhit = "";
            }


            public void ADDSTASH()
            {
                stash++;
                if (stash >= 2 && lastban < DateTime.Now)
                {
                    BAN(player.UserIDString, "\"CheatDetected (6)\"", config.fLY.hours, player.displayName, config.ESPStash.webhook);
                    lastban = DateTime.Now.AddSeconds(10f);
                    stash = 0;
                    return;
                }
            }

            public void ADDSPIDER()
            {
                spider++;
                ADDLOG("SPIDER", fermens.messages["logDETECT"].Replace("{name}", player.displayName).Replace("{steamid}", player.UserIDString).Replace("{detect}", spider.ToString()).Replace("{detectlimit}", spiderlimit.ToString()), config.sPIDER.webhook, 2);
                if (config.sPIDER.enable && spider >= spiderlimit && lastban < DateTime.Now)
                {
                    BAN(player.UserIDString, config.sPIDER.reason, config.sPIDER.hours, player.displayName, config.sPIDER.webhook);
                    lastban = DateTime.Now.AddSeconds(10f);
                    spider = 0;
                    return;
                }
            }

            public void ADDSILENT(int amount)
            {
                silent += amount;
                ADDLOG("SAIM", fermens.messages["logDETECT"].Replace("{name}", player.displayName).Replace("{steamid}", player.UserIDString).Replace("{detect}", silent.ToString()).Replace("{detectlimit}", config.sILENT.xdetects.ToString()), config.sILENT.webhook, 2);
                if (config.sILENT.enable && silent >= config.sILENT.xdetects && lastban < DateTime.Now)
                {
                    BAN(player.UserIDString, config.sILENT.reason, config.sILENT.hours, player.displayName, config.sILENT.webhook);
                    lastban = DateTime.Now.AddSeconds(10f);
                    silent = 0;
                    return;
                }
            }

            private int spiner;
            private Vector3 lastposition;
            // private int spinerdetect;

            private void TICK()
            {
                if (enablefull)
                {
                    player.SendConsoleCommand("noclip");
                    player.SendConsoleCommand("debugcamera");
                    player.SendConsoleCommand("debugcamera_unfreeze");
                    player.SendConsoleCommand("camspeed 0");
                }

            }

            public void DoDestroy() => Destroy(this);

            private void OnDestroy()
            {
                if (IsInvoking(nameof(TICK))) CancelInvoke(nameof(TICK));
                if (IsInvoking(nameof(FIREEND))) CancelInvoke(nameof(FIREEND));
                fermens.anticheatPlayers.Remove(player);
            }
        }

        private void OnPlayerBanned(string name, ulong id, string address, string reason)
        {
            if (reason == "Cheat Detected!") ADDLOG($"DebugCamera", fermens.messages["logBAN"].Replace("{name}", name).Replace("{steamid}", id.ToString()).Replace("{reason}", "FakeAdmin/DebugCamera"), config.dEBUGCAMERA.webhook, 1);
        }

        const string defaultsteamapi = "https://steamcommunity.com/dev/apikey";
        class resp
        {
            public avatar response;
        }

        class avatar
        {
            public List<Players> players;
        }

        class Players
        {
            public int? profilestate;
            public int? timecreated;
        }

        class INFO
        {
            public DateTime dateTime;
            public bool profilestate;
            public bool steam;
            public Dictionary<string, Dictionary<string, int>> hitinfo;
        }

        Dictionary<ulong, INFO> PLAYERINFO = new Dictionary<ulong, INFO>();

        private void Init()
        {
            fermens = this;
            //   Unsubscribe(nameof(OnPlayerConnected));
        }

        #region Grid
        Dictionary<string, Vector3> Grids = new Dictionary<string, Vector3>();
        const float calgon = 0.0066666666666667f;
        void CreateSpawnGrid()
        {
            var worldSize = (ConVar.Server.worldsize);
            float offset = worldSize / 2;
            var gridWidth = (calgon * worldSize);
            float step = worldSize / gridWidth;

            string start = "";

            char letter = 'A';
            int number = 0;

            for (float zz = offset; zz > -offset; zz -= step)
            {
                for (float xx = -offset; xx < offset; xx += step)
                {
                    Grids.Add($"{start}{letter}{number}", new Vector3(xx - 55f, 0, zz));
                    if (letter.ToString().ToUpper() == "Z")
                    {
                        start = "A";
                        letter = 'A';
                    }
                    else
                    {
                        letter = (char)(((int)letter) + 1);
                    }


                }
                number++;
                start = "";
                letter = 'A';
            }
        }

        private string GetNameGrid(Vector3 pos)
        {
            return Grids.Where(x => x.Value.x < pos.x && x.Value.x + 150f > pos.x && x.Value.z > pos.z && x.Value.z - 150f < pos.z).FirstOrDefault().Key;
        }
        #endregion

        private static bool prison = false;
        private List<StashContainer> stashContainers = new List<StashContainer>();
        private float sizeworldx;
        private float sizeworldz;

        List<Vector3> OntheMap = new List<Vector3>();
        void foundmonuments()
        {
            OntheMap.Clear();
            foreach (var z in TerrainMeta.Path.Monuments)
            {
                if (z.name.Contains("/cave") || z.name.Contains("/tiny") || z.name.Contains("/power substations") || z.name.Contains("OilrigAI")) continue;
                Vector3 pos = z.transform.position;
                if (!OntheMap.Contains(pos)) OntheMap.Add(pos);
            }
        }

        private Vector3 RANDOMPOS() => new Vector3(Random.Range(-sizeworldx, sizeworldx), 400f, Random.Range(-sizeworldz, sizeworldz));

        List<string> names = new List<string>();

        private Vector3 FINDSPAWNPOINT(int num = 1)
        {
            if (num >= 300) return Vector3.zero;
            Vector3 pos = RANDOMPOS();

            RaycastHit hitInfo;
            if (!Physics.Raycast(pos, Vector3.down, out hitInfo, 450f, Layers.Solid)) return FINDSPAWNPOINT(num++);
            if (hitInfo.collider == null || hitInfo.collider.name != "Terrain") return FINDSPAWNPOINT(num++);
            if (hitInfo.point.y - TerrainMeta.WaterMap.GetHeight(hitInfo.point) < 0) return FINDSPAWNPOINT(num++);
            if (WaterLevel.Test(hitInfo.point, true, true)) return FINDSPAWNPOINT(num++);
            if (OntheMap.Any(x => Vector3.Distance(x, hitInfo.point) < 170f)) return FINDSPAWNPOINT(num++);
            if (stashContainers.Any(x => Vector3.Distance(x.transform.position, hitInfo.point) < 30f)) return FINDSPAWNPOINT(num++);
            if (Mathf.Abs((TerrainMeta.HeightMap.GetHeight(hitInfo.point) - hitInfo.point.y)) > 0.1f) return FINDSPAWNPOINT(num++);
            if (Mathf.Abs((TerrainMeta.HeightMap.GetHeight(hitInfo.point + Vector3.left * 0.2f) - hitInfo.point.y)) > 0.01f) return FINDSPAWNPOINT(num++);
            if (Mathf.Abs((TerrainMeta.HeightMap.GetHeight(hitInfo.point + Vector3.right * 0.2f) - hitInfo.point.y)) > 0.01f) return FINDSPAWNPOINT(num++);
            if (Mathf.Abs((TerrainMeta.HeightMap.GetHeight(hitInfo.point + Vector3.forward * 0.2f) - hitInfo.point.y)) > 0.01f) return FINDSPAWNPOINT(num++);
            if (Mathf.Abs((TerrainMeta.HeightMap.GetHeight(hitInfo.point + Vector3.back * 0.2f) - hitInfo.point.y)) > 0.01f) return FINDSPAWNPOINT(num++);
            return hitInfo.point;
        }

        private void CanSeeStash(BasePlayer player, StashContainer stash)
        {
            if (stash.OwnerID != 0 || !stashContainers.Contains(stash)) return;
            ADDLOG("ESPStash", fermens.messages["logESPStash"].Replace("{name}", player.displayName).Replace("{steamid}", player.UserIDString).Replace("{grid}", GetNameGrid(stash.transform.position)), config.ESPStash.webhook, 2);
            timer.Once(75f, () =>
            {
                if (!player.IsConnected) return;
                ANTICHEAT aNTICHEAT;
                if (!anticheatPlayers.TryGetValue(player, out aNTICHEAT)) return;
                aNTICHEAT.ADDSTASH();
            });
            lastshash = stash.transform.position;
            stashContainers.Remove(stash);
        }

        void OnEntityKill(StashContainer stash)
        {
            if (stash.OwnerID != 0 || !stashContainers.Contains(stash)) return;
            List<BasePlayer> list = Pool.GetList<BasePlayer>();
            Vis.Entities<BasePlayer>(stash.transform.position, 4f, list, 131072);
            foreach (var player in list) ADDLOG("ESPStash", fermens.messages["logESPStash"].Replace("{name}", player.displayName).Replace("{steamid}", player.UserIDString).Replace("{grid}", GetNameGrid(stash.transform.position)), config.ESPStash.webhook, 2);
            lastshash = stash.transform.position;
            stashContainers.Remove(stash);
        }

        string patterban = "";

        private string token = "270220221000fermens";
        private string namer = "XAntiCheat";

        private void OnServerInitialized()
        {
            ServerMgr.Instance.StartCoroutine(GetCallback());
        }

        #region WEBCONFIG
        public Dictionary<string, string> messagesEN = new Dictionary<string, string>
        {
            { "logCODELOCK", "{name}({steamid}) entered the password for the code lock of the banned player({owner})!" },
            { "logSTEAM_PLAYER", "{name}({steamid}) redeemed from blocking." },
            { "logPRISON", "Sent to jail {name}({steamid}) for {days} days [{reason}]" },
            { "logBAN", "Banned {name}({steamid}) for {days} days [{reason}]" },
            { "logMACRO", "{name}({steamid}) | shots {fires} | used {weaponfire} | moved: {macromove} | detect #{detect}" },
            { "logDETECT", "{name}({steamid}) detects {detect}/{detectlimit}" },
            { "logHITMod", "{name}({steamid}) | {hitbone} | average distance {average} | used {weaponhit} | ({minutes})" },
            { "logFLY", "{name}({steamid}) - [{elements}] - height: {height} m. ({collidername}) {desc}" },
            { "logESPStash", "{name}({steamid}) - grid {grid}" },
            { "NEW.ACCOUNT", "Suspicious account" },
            { "KICK.PRIVATE", "Open your profile to play on this server! (private profile)" },
            { "KICK.NENASTROYEN", "Set up a profile to play on this server!" },
            { "KICK.VPN", "It is forbidden to play with VPN on the server! (VPN DETECTED)" },
            { "debugStashs", "Created {count} stashe traps" },
            { "adminLs", "<color=yellow>Haven't unearthed a single stesh yet!</color>" },
            { "adminAcLogs", "XAC - Latest logs:\n" },
            { "adminAcNoLogs", "XAC - The logs are empty :(" },
            { "debugConnect0", "------------\n{name} ({steamid})" },
            { "debugConnect1", "\nGame version: {steam}" },
            { "debugConnect2", "\nAccount set up: {ns}" },
            { "debugConnect3", "\nAccount created: {date}" },
            { "debugConnect4", "\nProfile private: Yes" },
            { "descFly", "[for_consideration!in_building]" }
        };

        public Dictionary<string, string> messagesRU = new Dictionary<string, string>
        {
            { "logCODELOCK", "{name}({steamid}) ввёл пароль от кодового замка забаненного игрока ({owner})!" },
            { "logSTEAM_PLAYER", "{name}({steamid}) отмазали от бана." },
            { "logPRISON", "Отправили в тюрьму {name}({steamid}) на {days} дней [{reason}]" },
            { "logBAN", "Забанили {name}({steamid}) на {days} дней [{reason}]" },
            { "logMACRO", "{name}({steamid}) | выстрелов {fires} | использовал {weaponfire} | двигался: {macromove} | детект #{detect}" },
            { "logDETECT", "{name}({steamid}) детектов {detect}/{detectlimit}" },
            { "logHITMod", "{name}({steamid}) | {hitbone} | средняя дистанция {average} | использовал {weaponhit} | ({minutes})" },
            { "logFLY", "{name}({steamid}) - [{elements}] - высота: {height} м. ({collidername}) {desc}" },
            { "logESPStash", "{name}({steamid}) - квадрат {grid}" },
            { "NEW.ACCOUNT", "Подозрительный аккаунт" },
            { "KICK.PRIVATE", "Откройте профиль, чтобы играть на этом сервере! (private profile)" },
            { "KICK.NENASTROYEN", "Настройте профиль, чтобы играть на этом сервере!" },
            { "KICK.VPN", "На сервере запрещено играть с VPN! (VPN DETECTED)" },
            { "debugStashs", "Создали {count} стешей-ловушек" },
            { "adminLs", "<color=yellow>Еще не раскопали ни одного стеша!</color>" },
            { "adminAcLogs", "XAC - Последние логи:\n" },
            { "adminAcNoLogs", "XAC - В логах пусто :(" },
            { "debugConnect0", "------------\n{name} ({steamid})" },
            { "debugConnect1", "\nВерсия игры: {steam}" },
            { "debugConnect2", "\nАккаунт настроен: {ns}" },
            { "debugConnect3", "\nАккаунт создан: {date}" },
            { "debugConnect4", "\nПрофиль закрытый: Да" }
        };

        IEnumerator GetCallback()
        {
            Debug.Log("[XAntiCheat] Initialization...");

            lang.RegisterMessages(messagesEN, this, "en");
            lang.RegisterMessages(messagesRU, this, "ru");
            messages = lang.GetMessages(config.lang, this);
            CreateSpawnGrid();

            if (config.ipinfotoken == ipinfosingup)
            {
                Debug.LogWarning(fermensEN ? "Enter the token for IPINFO in the config if you want to enable auto-detection of VPN usage by the player!" : "Введите в конфиг токен для IPINFO, если хотите включить автоопределение использования игроком VPN!");
            }
            foundmonuments();

            sizeworldx = TerrainMeta.Size.x / 2.5f;
            sizeworldz = TerrainMeta.Size.z / 2.5f;

            namefile = DateTime.Now.ToString("MM/dd");
            LOGS = Interface.Oxide.DataFileSystem.ReadObject<List<string>>("XAC/" + namefile);


            if (string.IsNullOrEmpty(config.steampi) || config.steampi == defaultsteamapi)
            {
                Debug.LogError(fermensEN ? "SPECIFY STEAMAPI IN CONFIG!" : "УКАЖИТЕ STEAMAPI В КОНФИГЕ!");
                if (!debugmode) yield break;
            }

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player.GetComponent<ANTICHEAT>() == null) player.gameObject.AddComponent<ANTICHEAT>();
            }

            timer.Once(5f, () => { if (PrisonBitch != null) prison = true; });

            permission.RegisterPermission("XAntiCheat.allow", this);
            permission.RegisterPermission("XAntiCheat.skip", this);
            permission.RegisterPermission("XAntiCheat.command", this);
            permission.RegisterPermission("XAntiCheat.chat", this);

            timer.Every(3600f, () => Save());

            stashContainers.Clear();

            if (!config.pattern.ContainsKey("EBSBAN.ACCOUNT"))
            {
                config.pattern.Add("EBSBAN.ACCOUNT", "ban {steamid} {time}h {reason}");
                SaveConfig();
            }

            patterban = EnhancedBanSystem != null ? config.pattern["EBSBAN.ACCOUNT"] : config.pattern["BAN.ACCOUNT"];

            int i = 0;
            while (i < config.ESPStash.amount)
            {
                Vector3 pos = FINDSPAWNPOINT();
                if (pos == Vector3.zero) continue;
                StashContainer stashContainer = GameManager.server.CreateEntity("assets/prefabs/deployable/small stash/small_stash_deployed.prefab", pos, new Quaternion(), true) as StashContainer;
                stashContainer.enableSaving = false;
                stashContainer.Spawn();
                int max = Random.Range(2, 7);
                int current = 0;
                foreach (var z in config.ESPStash.loots)
                {
                    if (Random.Range(0f, 1f) >= 0.65f)
                    {
                        if (current < max)
                        {
                            Item item = ItemManager.CreateByName(z.Key, Random.Range(1, z.Value));
                            if (item != null)
                            {
                                if (item.hasCondition)
                                {
                                    item.LoseCondition(Random.Range(0f, 100f));
                                    BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                                    if (weapon != null)
                                    {
                                        if (weapon.primaryMagazine != null)
                                        {
                                            weapon.primaryMagazine.contents = Random.Range(1, weapon.primaryMagazine.capacity + 1);
                                        }
                                    }
                                }
                                if (!item.MoveToContainer(stashContainer.inventory, Random.Range(0, 6), false)) item.MoveToContainer(stashContainer.inventory);
                                current++;
                            }
                        }
                    }
                }
                stashContainer.SetHidden(true);
                stashContainers.Add(stashContainer);
                i++;
            }
            Debug.Log(fermens.messages["debugStashs"].Replace("{count}", stashContainers.Count.ToString()));

            moders.Clear();
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (fermens.permission.UserHasPermission(player.UserIDString, "XAntiCheat.chat") && !moders.Contains(player)) moders.Add(player);
            }

            yield break;
        }
        #endregion

        private readonly int constructionColl = LayerMask.GetMask(new string[] { "Construction", "Deployable", "Prevent Building", "Deployed" });
        private readonly int buildingLayer = LayerMask.GetMask("Terrain", "World", "Construction", "Deployed");
        const string sspiral = "block.stair.spiral";
        const string sroof = "roof";
        const string sfly = "supply_drop";
        const string prefroof = "roof";
        const string prefspiral = "stairs.spiral";
        const string iceberg = "iceberg";

        private void OnPlayerViolation(BasePlayer player, AntiHackType type, float amount)
        {
            if (type == AntiHackType.FlyHack && !IsBattles(player.userID))
            {
                ANTICHEAT anticheat;
                if (!anticheatPlayers.TryGetValue(player, out anticheat)) return;

                List<BaseEntity> entitiesAroundPlayer = Pool.GetList<BaseEntity>();
                Vis.Entities<BaseEntity>(player.transform.position, 2f, entitiesAroundPlayer);

                List<TreeEntity> treesAroundPlayer = Pool.GetList<TreeEntity>();
                Vis.Entities<TreeEntity>(player.transform.position, 6f, treesAroundPlayer);

                string elements = "";
                string desc = "";
                bool isPlayerNearby = false;
                bool multipleEntitiesNearby = entitiesAroundPlayer.Count > 1;

                foreach (var entity in entitiesAroundPlayer)
                {
                    if (entity is BasePlayer && (entity as BasePlayer) != player)
                    {
                        isPlayerNearby = true;
                        desc += "[False! Is it on a player?]";
                        break;
                    }
                    elements += entity.ShortPrefabName + (multipleEntitiesNearby ? " | " : "");
                }

                RaycastHit hit;
                bool raycastHit = Physics.Raycast(player.transform.position, Vector3.down, out hit, 20f);
                if (raycastHit)
                {
                    bool isSpider = false;
                    bool isSupplyDrop = hit.collider.name.Contains(sfly);
                    bool isSpiralStairs = hit.collider.name.Contains(prefspiral);
                    bool isRoof = hit.collider.name.Contains(prefroof);
                    bool isIceberg = hit.collider.name.Contains(iceberg);

                    RaycastHit hit2;
                    bool raycastHit2 = Physics.Raycast(player.transform.position, player.eyes.BodyForward(), out hit2, 1f);
                    if (raycastHit2 && hit2.collider.name.Contains("wall"))
                    {
                        isSpider = true;
                    }

                    if (!isSpiralStairs) isSpiralStairs = elements.Contains(sspiral);
                    if (isSpiralStairs)
                    {
                        //desc += "[False! Spiral stairs]";
                        return;
                    }

                    if (!isSupplyDrop) isSupplyDrop = elements.Contains(sfly);
                    if (isSupplyDrop)
                    {
                        //desc += "[False! Supply drop]";
                        return;
                    }

                    if (!isRoof) isRoof = elements.Contains(sroof);
                    if (isRoof)
                    {
                        //desc += "[False! Roof]";
                        return;
                    }

                    bool isTreeNearby = treesAroundPlayer.Count > 0;
                    if (isTreeNearby)
                    {
                        // desc += "[False! Tree]";
                        return;
                    }

                    bool isInsideBuilding = hit.collider.name.Contains("assets/prefabs/building core");
                    if (isInsideBuilding)
                    {
                        if (messages.TryGetValue("descFly", out string descMessage))
                        {
                            desc += descMessage;
                        }
                        else
                        {
                            desc += "Default message for descFly key";
                        }
                    }

                    float distanceToGround = player.Distance(hit.point);
                    ADDLOG("FLY", messages["logFLY"].Replace("{name}", player.displayName)
                                               .Replace("{steamid}", player.UserIDString)
                                               .Replace("{elements}", elements)
                                               .Replace("{height}", distanceToGround.ToString("F1"))
                                               .Replace("{collidername}", hit.collider.name)
                                               .Replace("{desc}", desc),
                           config.fLY.webhook, 1);

                    if (isRoof || isSupplyDrop || isSpiralStairs || isIceberg || isTreeNearby || isPlayerNearby)
                        return;

                    if (isSpider && distanceToGround >= 3f && distanceToGround <= 12f)
                        anticheat.ADDSPIDER();
                    else if (!multipleEntitiesNearby && distanceToGround >= 3f && distanceToGround <= 7f)
                        anticheat.ADDFLY(distanceToGround);
                }
            }
        }



        private static string namefile;
        private static List<string> LOGS = new List<string>();
        private static List<BasePlayer> moders = new List<BasePlayer>();
        private static void ADDLOG(string whatis, string desc, string webhook, int priority)
        {
            if (config.logspriority >= priority)
            {
                string text = $"-[{whatis}]-" + " " + desc;
                Debug.LogWarning(text);
                if (!string.IsNullOrEmpty(config.discordid))
                {
                    // if (fermens.DiscordCore != null) fermens.DiscordCore.Call("SendMessageToChannel", config.discordid, text);
                    fermens.uDiscord = fermens.plugins.Find("uDiscord");
                    if (fermens.uDiscord != null) fermens.uDiscord.Call("SendMessageToChannel", config.discordid, text);
                    if (fermens.HaxBot != null)
                    {
                        fermens.HaxBot.Call("MESSAGE", text, (uint)14177041, config.discordid);
                    }
                }

                if (!string.IsNullOrEmpty(webhook)) SendDiscordMessage(whatis, desc, webhook);

                foreach (var z in moders)
                {
                    z.ChatMessage(text);
                }

                LOGS.Add($"[{DateTime.Now.ToShortTimeString()}] " + text);
            }
        }

        private void Save()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"XAC/{namefile}", LOGS);
            Debug.Log("[XAntiCheat] Save logs.");
        }

        [ConsoleCommand("ac.logs")]
        private void cmdlastlogs(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (arg.IsAdmin || player != null && permission.UserHasPermission(player.UserIDString, "XAntiCheat.command"))
            {
                if (LOGS.Count > 0)
                {
                    int number;
                    if (!arg.HasArgs() || !int.TryParse(arg.Args[0], out number)) number = 10;
                    int skip = LOGS.Count - number;
                    if (skip < 0) skip = 0;
                    string text = string.Join("\n", LOGS.Skip(skip).Take(number).ToArray());
                    arg.ReplyWith(messages["adminAcLogs"] + text + "\n------------------");
                }
                else
                {
                    arg.ReplyWith(messages["adminAcNoLogs"]);
                }
            }
        }

        Dictionary<ulong, List<BasePlayer.FiredProjectile>> projectiles = new Dictionary<ulong, List<BasePlayer.FiredProjectile>>();

        [ConsoleCommand("ac.accuracy")]
        private void cmdsaaccuracy(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            string text = "-----------------\nXAntiCheat - hit accuracy";
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                shoots shoots;
                if (_shoots.TryGetValue(player.userID, out shoots))
                {

                    int countPlayer = shoots.success;
                    int countShoots = shoots.number;
                    text += $"\n{player.displayName}({player.UserIDString}) | {countPlayer}/{countShoots} | {string.Format("{0:N2}%", countShoots > 0 ? (countPlayer * 100f / countShoots) : 0)}";
                }
            }

            Debug.Log(text + "\n-----------------");
        }

        [ConsoleCommand("ac.save")]
        private void cmdsavecommand(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            Save();
        }

        private void Unload()
        {
            foreach (var z in stashContainers)
            {
                if (!z.IsDestroyed) z.Kill();
            }

            Save();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                ANTICHEAT aNTICHEAT;
                if (anticheatPlayers.TryGetValue(player, out aNTICHEAT)) aNTICHEAT.DoDestroy();
            }

            timer.Once(1f, () =>
            {
                fermens = null;
                anticheatPlayers.Clear();
            });
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (!player.IsConnected) return;
            if (player.IsReceivingSnapshot)
            {
                timer.Once(1f, () => OnPlayerConnected(player));
                return;
            }
            if (player.GetComponent<ANTICHEAT>() == null) player.gameObject.AddComponent<ANTICHEAT>();
            if (fermens.permission.UserHasPermission(player.UserIDString, "XAntiCheat.chat") && !moders.Contains(player)) moders.Add(player);
            ServerMgr.Instance.StartCoroutine(GETINFO(player));
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {

            ANTICHEAT aNTICHEAT;
            if (anticheatPlayers.TryGetValue(player, out aNTICHEAT)) aNTICHEAT.DoDestroy();
            if (moders.Contains(player)) moders.Remove(player);
        }

        class Eka
        {
            public Vector3 s;
            public Vector3 t;
        }

        Dictionary<ulong, shoots> _shoots = new Dictionary<ulong, shoots>();

        class shoots
        {
            public int number;
            public int success;
        }

        private void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile itemModProjectile, ProjectileShoot projectileShoot)
        {
            if (projectile == null || player == null || itemModProjectile == null || projectileShoot == null) return;

            shoots shoots;
            if (!_shoots.TryGetValue(player.userID, out shoots)) _shoots.Add(player.userID, new shoots { number = 1, success = 0 });
            else
            {
                shoots.number += projectileShoot.projectiles.Count;
                _shoots[player.userID] = shoots;
            }

            ANTICHEAT aNTICHEAT;

            if (!anticheatPlayers.TryGetValue(player, out aNTICHEAT) || projectile.primaryMagazine.capacity > projectile.primaryMagazine.definition.builtInSize) return;
            aNTICHEAT.ADDFIRE(projectile.GetItem().info.name);
        }

        private void OnEntityTakeDamage(object entity, HitInfo info)
        {
            if (info == null || info.Weapon == null || info.InitiatorPlayer == null || info.damageTypes != null && info.damageTypes.IsMeleeType()) return;
            if (info.InitiatorPlayer.IsNpc) return;

            shoots shoots;
            if (!_shoots.TryGetValue(info.InitiatorPlayer.userID, out shoots)) return;

            if (!(entity is BasePlayer)) return;
            BasePlayer player = entity as BasePlayer;
            if (player == null || player.IsNpc || !player.IsConnected || player.IsSleeping() || info.InitiatorPlayer == player || player.Team != null && player.Team.members.Contains(info.InitiatorPlayer.userID))
            {
                shoots.number -= 1;
                _shoots[info.InitiatorPlayer.userID] = shoots;
                return;
            }

            shoots.success += 1;
            _shoots[info.InitiatorPlayer.userID] = shoots;

            string weapon = info.WeaponPrefab != null && !string.IsNullOrEmpty(info.WeaponPrefab.ShortPrefabName) ? info.WeaponPrefab.ShortPrefabName : "x";
            string bone = !string.IsNullOrEmpty(info.boneName) ? info.boneName : "x";
            float distance = info.ProjectileDistance;

            if (config.logs) Debug.Log($"-- {info.InitiatorPlayer.displayName}({info.InitiatorPlayer.UserIDString}) [{weapon} | {bone} | {distance.ToString("F1")} m.] => {player.displayName}({player.UserIDString})");

            ANTICHEAT aNTICHEAT;
            if (!anticheatPlayers.TryGetValue(info.InitiatorPlayer, out aNTICHEAT)) return;

            aNTICHEAT.ADDHIT(bone, weapon, distance);

        }

        private bool IsNPC(BasePlayer player)
        {
            if (player is NPCPlayer) return true;
            if (!(player.userID >= 76560000000000000L || player.userID <= 0L)) return true;
            return false;
        }

        [PluginReference] Plugin MultiFighting, Battles, HaxBot, uDiscord;

        private bool IsBattles(ulong userid)
        {
            return Battles != null && Battles.Call<bool>("IsPlayerOnBattle", userid);
        }

        private bool ISSTEAM(Network.Connection connection)
        {
            if (MultiFighting == null) return true;
            return MultiFighting.Call<bool>("IsSteam", connection);
        }

        class tok
        {
            public string key;
            public uint appid;
            public string ticket;
        }
        IEnumerator GETINFO(BasePlayer player)
        {
            yield return new WaitForSeconds(1f);
            if (!player.IsConnected) yield break;
            yield return new WaitForEndOfFrame();
            webrequest.Enqueue($"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={config.steampi}&steamids={player.UserIDString}&format=json", null, (code, response) =>
            {
                if (response != null && code == 200)
                {
                    if (player.IsConnected)
                    {
                        string steamid = player.UserIDString;
                        string text = messages["debugConnect0"].Replace("{name}", player.displayName).Replace("{steamid}", steamid);
                        bool act = false;
                        INFO iNFO = new INFO();
                        resp sr = JsonConvert.DeserializeObject<resp>(response);
                        int datetime = sr.response.players[0].timecreated ?? 0;
                        DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        DateTime create = epoch.AddSeconds(datetime).AddHours(3);
                        bool steam = ISSTEAM(player.Connection);
                        text += messages["debugConnect1"].Replace("{steam}", (steam ? "Steam" : "No-Steam"));
                        int nastr = sr.response.players[0].profilestate ?? 0;
                        bool ns = ISNASTROEN(nastr);
                        text += messages["debugConnect2"].Replace("{ns}", ns ? (fermensEN ? "Yes" : "Да") : (fermensEN ? "No" : "Нет"));
                        if (!ns && config.kicknenastoyen && !debugmode)
                        {
                            if (!permission.UserHasPermission(steamid, "XAntiCheat.allow") && !permission.UserHasPermission(steamid, "XAntiCheat.skip"))
                            {
                                timer.Once(30f, () => player.Kick(GetMessage("KICK.NENASTROYEN", steamid)));
                                act = true;
                            }
                        }
                        if (datetime > 0)
                        {
                            text += messages["debugConnect3"].Replace("{date}", create.ToShortDateString());
                        }
                        else
                        {
                            text += messages["debugConnect4"];
                            if (!steam || !config.steamkick)
                            {
                                if (config.kickprivate && !debugmode && !permission.UserHasPermission(steamid, "XAntiCheat.allow") && !permission.UserHasPermission(steamid, "XAntiCheat.skip"))
                                {
                                    timer.Once(30f, () => player.Kick(GetMessage("KICK.PRIVATE", steamid)));
                                    act = true;
                                }
                            }
                        }

                        if (config.show) Debug.Log(text + "\n------------");

                        if (!permission.UserHasPermission(steamid, "XAntiCheat.allow") && !debugmode && !permission.UserHasPermission(steamid, "XAntiCheat.skip") && (config.bannensatroyen && nastr != 1 || create.AddDays(config.banday) > DateTime.Now))
                        {
                            if (!act && (!steam || steam && config.steam)) Server.Command(patterban.Replace("{steamid}", steamid).Replace("{reason}", GetMessage("NEW.ACCOUNT", steamid)).Replace("{time}", config.bannewaccountday.ToString()));
                        }
                    }
                }
            }, this);

            yield return new WaitForEndOfFrame();
            //VPN
            if (string.IsNullOrEmpty(config.ipinfotoken) || config.ipinfotoken == ipinfosingup) yield break;
            string[] ip = player.IPlayer.Address.Split(':');
            webrequest.Enqueue(ipinnfourl.Replace("{token}", config.ipinfotoken).Replace("{ip}", ip[0]), null, (code, response) =>
            {
                if (response != null && code == 200)
                {
                    if (!player.IsConnected) return;
                    VPNINFO sr = JsonConvert.DeserializeObject<VPNINFO>(response);
                    bool VPN = sr.vpn;
                    Debug.Log($"[{player.displayName}({player.UserIDString}) | IP: {ip[0]} | VPN: {(VPN ? "Yes" : "No")}]");
                    if (!VPN) return;
                    if (config.kickvpn && !permission.UserHasPermission(player.UserIDString, "XAntiCheat.allow") && !permission.UserHasPermission(player.UserIDString, "XAntiCheat.skip"))
                    {
                        timer.Once(30f, () => Server.Command($"kick {player.UserIDString} \"{GetMessage("KICK.VPN", player.UserIDString)}\""));
                    }
                }
            }, this);
            yield break;
        }

        class VPNINFO
        {
            public bool vpn;
            public bool proxy;
            public bool tor;
            public bool hosting;
        }

        private bool ISNASTROEN(int num)
        {
            if (num == 1) return true;
            return false;
        }
    }
}