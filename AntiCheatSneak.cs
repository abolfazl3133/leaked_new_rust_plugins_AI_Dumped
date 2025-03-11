using System;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using UnityEngine;
using Rust;
using Newtonsoft.Json;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Linq;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Libraries;

namespace Oxide.Plugins
{
    [Info("AntiCheatSneak", "PRESSF", "6.1")]
    [Description("discord: pressfwd")]
    class AntiCheatSneak : RustPlugin
    {
        [PluginReference] Plugin MultiFighting;

        #region Configuration

        private static ConfigData _config;

        public class ConfigData
        {
            public OtherConfig Общее { get; set; } = new OtherConfig();
            public AimConfig НастройкаАима { get; set; } = new AimConfig();
            public NHDetect ДетектНочью { get; set; } = new NHDetect();
            public ManipConfig Манипулятор { get; set; } = new ManipConfig();
            public FlyConfig ФлайХак { get; set; } = new FlyConfig();
            public StashConfig Ловушки { get; set; } = new StashConfig();
            public SteamProxyConfig Стим { get; set; } = new SteamProxyConfig();
            public LogAndConfig Логирование { get; set; } = new LogAndConfig();
        }

        public class OtherConfig
        {
            [JsonProperty("Консольная команда для блокировки? пример (bss_ban)")]
            public string BanCommand { get; set; } = "ban";
            [JsonProperty("Кикать после бана?(если игрока банит и не кикает автоматом)")]
            public bool isBanKick { get; set; } = true;
        }

        public class AimConfig
        {
            [JsonProperty("Причина бана/кика за AIM")]
            public string NameAim { get; set; } = "AC - Протокол #1!";
            [JsonProperty("Мера наказания за AIM?(true - Бан, false - Кик)")]
            public bool isAimBan { get; set; } = true;
            [JsonProperty("Блокировать дамаг при попадании в голову на расстоянии?")]
            public bool isAimBlocDamage { get; set; } = false;

            public RifleConfig НастройкаВинтовок { get; set; } = new RifleConfig();
            public PistolConfig НастройкаПистолетов { get; set; } = new PistolConfig();
            public PPConfig НастройкаПП { get; set; } = new PPConfig();
            public SniperConfig НастройкаСнайперскихВинтовок { get; set; } = new SniperConfig();
            public BowConfig НастройкаЛуков { get; set; } = new BowConfig();

            public class RifleConfig
            {
                [JsonProperty("За попадание в голову на сколько метров кикать? (Снайперки уже учтены)")]
                public float metrrifle { get; set; } = 200f;
                [JsonProperty("Список shortname оружия")]
                public List<string> weaponrifle { get; set; } = new List<string>();
                [JsonProperty("Секунд до сброса детектов?")]
                public float isTwoRifCD { get; set; } = 5;
                [JsonProperty("Сколько попаданий за N сек в голову?")]
                public int isTwoRifH { get; set; } = 3;
                [JsonProperty("Сколько попаданий за N сек в тело?")]
                public int isTwoRifB { get; set; } = 10;
            }
            public class PistolConfig
            {
                [JsonProperty("За попадание в голову c пистолетов на сколько метров кикать?")]
                public float metrpistol { get; set; } = 140f;
                [JsonProperty("Список shortname оружия")]
                public List<string> weaponpistol { get; set; } = new List<string>();
                [JsonProperty("Секунд до сброса детектов?")]
                public float isTwoPistolCD { get; set; } = 5;
                [JsonProperty("Сколько попаданий за N сек в голову?")]
                public int isTwoPistolH { get; set; } = 2;
                [JsonProperty("Сколько попаданий за N сек в тело?")]
                public int isTwoPistolB { get; set; } = 8;
            }
            public class PPConfig
            {
                [JsonProperty("За попадание в голову с ПП?")]
                public float metrpp { get; set; } = 180f;
                [JsonProperty("Список shortname оружия")]
                public List<string> weaponpp { get; set; } = new List<string>();
                [JsonProperty("Секунд до сброса детектов?")]
                public float isTwoPPCD { get; set; } = 5;
                [JsonProperty("Сколько попаданий за N сек в голову?")]
                public int isTwoPPH { get; set; } = 2;
                [JsonProperty("Сколько попаданий за N сек в тело?")]
                public int isTwoPPB { get; set; } = 2;
            }
            public class SniperConfig
            {
                [JsonProperty("Список shortname оружия")]
                public List<string> weaponsniper { get; set; } = new List<string>();
                [JsonProperty("За попадание в голову с снайперки?")]
                public float metrsniper { get; set; } = 500f;
            }
            public class BowConfig
            {
                [JsonProperty("За попадание в голову c луков на сколько метров кикать?")]
                public float metrbow { get; set; } = 70f;
                [JsonProperty("Мера наказания?(true - Бан, false - Кик)")]
                public bool isbowBan { get; set; } = true;
                [JsonProperty("Список shortname оружия")]
                public List<string> weaponbow { get; set; } = new List<string>();
                [JsonProperty("Секунд до сброса детектов?")]
                public float isTwoBowCD { get; set; } = 5;
                [JsonProperty("Сколько попаданий за N сек в голову?")]
                public int isTwoBowH { get; set; } = 2;
                [JsonProperty("Сколько попаданий за N сек в тело?")]
                public int isTwoBowB { get; set; } = 3;
            }
        }

        public class NHDetect
        {
            [JsonProperty("Логировать попадание в ночное время суток?")]
            public bool isNH { get; set; } = true;
            [JsonProperty("Расстояние для детекта")]
            public float metrnh { get; set; } = 100f;
            [JsonProperty("Причина детекта за попадание в ночное время")]
            public string NameNightShot { get; set; } = "AC - Протокол #9!";
        }

        public class ManipConfig
        {
            [JsonProperty("Банить за тестовый детект манипулятора? (не мало жалоб за ложный бан, хотя видео док-ва не дают)")]
            public bool isManipT { get; set; } = true;
            [JsonProperty("Причина бана/кика за манипулятор")]
            public string NameAimM { get; set; } = "AC - Протокол #7!";
            [JsonProperty("Причина бана/кика за быструю стрельбу")]
            public string NameAimMR { get; set; } = "AC - Протокол #8!";
            [JsonProperty("Детектить стрельу с водительского места?")]
            public bool isAimDrive { get; set; } = false;
            [JsonProperty("Причина бана/кика за стрельбу с водительского места")]
            public string NameAimDrive { get; set; } = "AC - Протокол #2!";
        }

        public class FlyConfig
        {
            [JsonProperty("Включить наказание за FlyHack?")]
            public bool isFly { get; set; } = false;
            [JsonProperty("Мера наказания?(true - Бан, false - Кик)")]
            public bool isFlyBan { get; set; } = true;
            [JsonProperty("За сколько детектов наказывать за FlyHack?")]
            public int DCount { get; set; } = 3;
            [JsonProperty("Причина бана/кика за FlyHack")]
            public string NameFly { get; set; } = "AC - Протокол #3!";
        }

        public class StashConfig
        {
            [JsonProperty("Банить за откапывание чужих стешей?")]
            public bool isStash { get; set; } = true;
            [JsonProperty("Банить за откапывание N стешей")]
            public int stashBCount { get; set; } = 3;
            [JsonProperty("Причина бана/кика за откапывание чужих стешей")]
            public string NameStash { get; set; } = "AC - Протокол #4!";
        }

        public class SteamProxyConfig
        {
            [JsonProperty("Проверять игроков на Proxy через ip2location?")]
            public bool PROXY_CHECT { get; set; } = false; 
            [JsonProperty("API Ключ для проверок IP игроков (https://www.ip2location.io/)")]
            public string YOUR_API_KEY { get; set; } = "000000"; 
            [JsonProperty("Проверять дату регистрации через steam?")]
            public bool STEAM_CHECT { get; set; } = false; 
            [JsonProperty("API Ключ https://steamcommunity.com/dev/apikey")]
            public string SteamAPI { get; set; } = "123123";
            [JsonProperty("Не пускать аккаунт если он создан мение N дней назад?")]
            public int SteamDays { get; set; } = 5;
        }

        public class LogAndConfig
        {
            [JsonProperty("Ссылка на вебхук Discord")]
            public string Webhook { get; set; } = "";
            [JsonProperty("Картинка в логировании (версия стандартная)")]
            public string WebhookImage { get; set; } = "https://media.giphy.com/media/V4pAZ7bxf1rOFoLCWS/giphy.gif";
            [JsonProperty("Версия логирования? (true - стандартная | false - сразу и по делу(By MEGARGAN(не работает, я не успел доделать)))")]
            public bool LogVersion { get; set; } = true;
            /*[JsonProperty("Использовать дополнительный блок логирования?")]
            public bool steamlog { get; set; } = true;*/
        }


        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<ConfigData>();
                if (_config == null) LoadDefaultConfig();
            }
            catch
            {
                Puts("ОШИБКА КОНФИГУРАЦИИ, ЗАГРУЗКА КОНФИГУРАЦИИ ПО УМОЛЧАНИЮ");
                LoadDefaultConfig();
            }

             NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => _config = new ConfigData();
        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private void OnServerInitialized()
        {
            permission.RegisterPermission("anticheatsneak.canseedetect.menu", this);
            permission.RegisterPermission("anticheatsneak.canseedetect.chat", this);
            ConVar.AntiHack.eye_protection = 5;
        }
        void Unload()
        {
            if (this.Title == "AntiCheatSneak")
            {
                ConVar.AntiHack.eye_protection = 0;
            }
            var data = Interface.Oxide.DataFileSystem.GetFile("ACS_JOIN");

            data.Save();
        }
        void OnServerShutdown()
        {
            ConVar.AntiHack.eye_protection = 0;
        }

        #endregion

        #region WrongShot
        
        private Dictionary<ulong, int> bodyshotCount = new Dictionary<ulong, int>();
        private Dictionary<ulong, float> lastBodyshotTime = new Dictionary<ulong, float>();
        private Dictionary<ulong, int> headshotCountW = new Dictionary<ulong, int>();
        private Dictionary<ulong, float> lastHeadshotTime = new Dictionary<ulong, float>();
        private const float headshotTimeout = 0.1f;
        private const float minDistanceh = 150f;
        private const float maxDistanceh = 500f;
        private const float bodyshotTimeout = 0.1f;
        private const float minDistance = 170f;
        private const float maxDistance = 500f;

        private void OnPlayerAttack(BasePlayer attacker, HitInfo hitInfo)
        {
            if (attacker == null || hitInfo == null || hitInfo.HitEntity == null || !(hitInfo.HitEntity is BasePlayer))
                return;

            BasePlayer targetPlayer = hitInfo.HitEntity as BasePlayer;
            if (targetPlayer.GetMountedVehicle() is BaseVehicle vehicle)
            {
                if (vehicle.GetDriver() == attacker)
                {
                    DetectNo2(attacker);
                }
            }

            float distance = Vector3.Distance(attacker.transform.position, hitInfo.HitEntity.transform.position);
            string weaponShortname = attacker.GetActiveItem()?.info?.shortname;

            if (!hitInfo.isHeadshot && Vector3.Distance(attacker.transform.position, hitInfo.HitEntity.transform.position) >= minDistance && Vector3.Distance(attacker.transform.position, hitInfo.HitEntity.transform.position) <= maxDistance)
            {
                ulong attackerID = attacker.userID;

                if (bodyshotCount.ContainsKey(attackerID))
                {
                    bodyshotCount[attackerID]++;
                    if (bodyshotCount[attackerID] >= 4 && UnityEngine.Time.realtimeSinceStartup - lastBodyshotTime[attackerID] <= bodyshotTimeout)
                    {
                        DetectNo8(attacker, weaponShortname, true);
                        Puts($"Игрок {attacker.displayName} {_config.Манипулятор.NameAimMR}");
                        string banReason = $"{_config.Манипулятор.NameAimMR}";
                        if (_config.НастройкаАима.isAimBan)
                        {
                            rust.RunServerCommand($"{_config.Общее.BanCommand} \"{attacker.userID}\" \"{banReason}\"");
                        }
                        if (!_config.НастройкаАима.isAimBan)
                        {
                            rust.RunServerCommand($"kick \"{attacker.userID}\" \"{banReason}\"");
                        }
                        bodyshotCount.Remove(attackerID);
                        lastBodyshotTime.Remove(attackerID);
                    }
                }
                else
                {
                    bodyshotCount.Add(attackerID, 1);
                }

                lastBodyshotTime[attackerID] = UnityEngine.Time.realtimeSinceStartup;
            }
            else
            {
                ulong attackerID = attacker.userID;
                if (bodyshotCount.ContainsKey(attackerID))
                {
                    bodyshotCount.Remove(attackerID);
                    lastBodyshotTime.Remove(attackerID);
                }
            }
            if (hitInfo.isHeadshot && Vector3.Distance(attacker.transform.position, hitInfo.HitEntity.transform.position) >= minDistanceh && Vector3.Distance(attacker.transform.position, hitInfo.HitEntity.transform.position) <= maxDistanceh)
            {
                ulong attackerID = attacker.userID;

                if (headshotCountW.ContainsKey(attackerID))
                {
                    headshotCountW[attackerID]++;
                    if (headshotCountW[attackerID] >= 3 && UnityEngine.Time.realtimeSinceStartup - lastHeadshotTime[attackerID] <= headshotTimeout)
                    {
                        DetectNo8(attacker, weaponShortname, true);
                        Puts($"Игрок {attacker.displayName} {_config.Манипулятор.NameAimMR}");
                        string banReason = $"{_config.Манипулятор.NameAimMR}";
                        if (_config.НастройкаАима.isAimBan)
                        {
                            rust.RunServerCommand($"{_config.Общее.BanCommand} \"{attacker.userID}\" \"{banReason}\"");
                        }
                        if (!_config.НастройкаАима.isAimBan)
                        {
                            rust.RunServerCommand($"kick \"{attacker.userID}\" \"{banReason}\"");
                        }
                    }
                }
                else
                {
                    headshotCountW.Add(attackerID, 1);
                }

                lastHeadshotTime[attackerID] = UnityEngine.Time.realtimeSinceStartup;
            }
            else
            {
                ulong attackerID = attacker.userID;
                if (headshotCountW.ContainsKey(attackerID))
                {
                    headshotCountW.Remove(attackerID);
                    lastHeadshotTime.Remove(attackerID);
                }
            }
        }
        
        #endregion

        #region AIM_Detect
        
        float resetTime = 10f;
        float metrpopal = 0f;

        Dictionary<ulong, int> playerHeadshotCounts = new Dictionary<ulong, int>();
        Dictionary<ulong, int> playerBodyshotCounts = new Dictionary<ulong, int>();

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BasePlayer && info.Initiator is BasePlayer)
            {
                var victim = (BasePlayer)entity;
                var attacker = (BasePlayer)info.Initiator;
                metrpopal = Vector3.Distance(victim.transform.position, attacker.transform.position);
                if (!playerHeadshotCounts.ContainsKey(attacker.userID)) 
                {
                    playerHeadshotCounts.Add(attacker.userID, 0);
                }
                if (!playerBodyshotCounts.ContainsKey(attacker.userID)) 
                {
                    playerBodyshotCounts.Add(attacker.userID, 0);
                }
                var data = Interface.Oxide.DataFileSystem.GetFile("ACS_MDD");
                var playerData = data["players", attacker.userID.ToString()] as Dictionary<string, object>;

                if (playerData != null)
                {
                    string dateString = playerData["date"].ToString();
                    DateTime lastDate = DateTime.Parse(dateString);
                    DateTime currentDate = DateTime.Now;
                        
                    TimeSpan difference = currentDate - lastDate;
                    if (difference.TotalSeconds <= 2)
                    {
                        ulong steamId = attacker.userID;
                        if (eyeHackKicks.TryGetValue(steamId, out int kickCount))
                        {
                            string nearbyObjects3 = GetNearbyObjects(attacker.transform.position, 5f);
                            if (HasForbiddenObjectsManipDoor(nearbyObjects3))
                            {
                                Puts($"Игрок {attacker.userID} находится рядом с запрещенными объектами и не будет кикнут.");
                                return;
                            }

                            string weaponM = info.Weapon?.GetItem()?.info.shortname;
                            float distanseM = Vector3.Distance(victim.transform.position, attacker.transform.position);
                            if (Vector3.Distance(victim.transform.position, attacker.transform.position) > 5f)
                            {
                                string banReason = $"{_config.Манипулятор.NameAimM}";

                                ChatDetect(attacker, banReason);
                                DetectNo7(attacker, victim, weaponM, kickCount, distanseM, false);
                                kickCount++;
                                Puts($"Игрок {attacker.displayName} {_config.Манипулятор.NameAimM}");
                                eyeHackKicks[steamId] = kickCount;

                                if (kickCount == 1)
                                {
                                    timer.Once(600f, () =>
                                    {
                                        eyeHackKicks[steamId] = 0;
                                    });
                                }

                                if (kickCount >= 3)
                                {
                                    if (_config.Манипулятор.isManipT)
                                    {
                                        rust.RunServerCommand($"{_config.Общее.BanCommand} {attacker.userID} \"{banReason}\"");
                                    }
                                    DetectNo7(attacker, victim, weaponM, kickCount, distanseM, true);
                                    eyeHackKicks[steamId] = 0;
                                }
                            }
                        }
                        else
                        {
                            eyeHackKicks[steamId] = 0;
                        }
                    }
                }

                string weaponfireRifle = info.Weapon?.GetItem()?.info.shortname;
                string weaponfirePp = info.Weapon?.GetItem()?.info.shortname;
                string weaponfirePistol = info.Weapon?.GetItem()?.info.shortname;
                string weaponfireSniper = info.Weapon?.GetItem()?.info.shortname;
                string weaponfireBows = info.Weapon?.GetItem()?.info.shortname;

                if (weaponfireRifle != null && _config.НастройкаАима.НастройкаВинтовок.weaponrifle.Contains(weaponfireRifle))
                {
                    if (metrpopal > _config.НастройкаАима.НастройкаВинтовок.metrrifle)
                    {
                        if (_config.НастройкаАима.isAimBlocDamage)
                        {
                            info.damageTypes.ScaleAll(0.01f);
                        }
                        if (info.isHeadshot)
                        {
                           playerHeadshotCounts[attacker.userID] += 1;
                            if (playerHeadshotCounts[attacker.userID] == 1)
                            {
                                timer.Once(_config.НастройкаАима.НастройкаВинтовок.isTwoRifCD, () => playerHeadshotCounts[attacker.userID] = 0);
                            }

                            int dCount = playerHeadshotCounts[attacker.userID];
                            int mCount = _config.НастройкаАима.НастройкаВинтовок.isTwoRifH;
                            if (dCount == mCount)
                            {
                                DetectNo1(attacker, victim, weaponfireRifle, metrpopal, dCount, mCount, true);
                                playerHeadshotCounts[attacker.userID] = 0;
                            }
                            else
                            {
                                DetectNo1(attacker, victim, weaponfireRifle, metrpopal, dCount, mCount, false);
                            }
                        }
                        else
                        {
                            playerBodyshotCounts[attacker.userID] += 1;
                            if (playerBodyshotCounts[attacker.userID] == 1)
                            {
                                timer.Once(_config.НастройкаАима.НастройкаВинтовок.isTwoRifCD, () => playerBodyshotCounts[attacker.userID] = 0);
                            }

                            int bCount = playerBodyshotCounts[attacker.userID];
                            int mbCount = _config.НастройкаАима.НастройкаВинтовок.isTwoRifB;
                            if (bCount == mbCount)
                            {
                                DetectNo1B(attacker, victim, weaponfireRifle, metrpopal, bCount, mbCount, true);
                                playerBodyshotCounts[attacker.userID] = 0;
                            }
                            else
                            {
                                DetectNo1B(attacker, victim, weaponfireRifle, metrpopal, bCount, mbCount, false);
                            }
                        }
                    }
                }

               if (weaponfirePp != null && _config.НастройкаАима.НастройкаПП.weaponpp.Contains(weaponfirePp))
                {
                    if (metrpopal > _config.НастройкаАима.НастройкаПП.metrpp)
                    {
                        if (_config.НастройкаАима.isAimBlocDamage)
                        {
                            info.damageTypes.ScaleAll(0.01f);
                        }
                        if (info.isHeadshot)
                        {
                            playerHeadshotCounts[attacker.userID] += 1;
                            if (playerHeadshotCounts[attacker.userID] == 1)
                            {
                                timer.Once(_config.НастройкаАима.НастройкаПП.isTwoPPCD, () => playerHeadshotCounts[attacker.userID] = 0);
                            }

                            int dCount = playerHeadshotCounts[attacker.userID];
                            int mCount = _config.НастройкаАима.НастройкаПП.isTwoPPH;
                            if (dCount == mCount)
                            {
                                DetectNo1(attacker, victim, weaponfirePp, metrpopal, dCount, mCount, true);
                                playerHeadshotCounts[attacker.userID] = 0;
                            }
                            else
                            {
                                DetectNo1(attacker, victim, weaponfirePp, metrpopal, dCount, mCount, false);
                            }
                        }
                        else
                        {
                            playerBodyshotCounts[attacker.userID] += 1;
                            if (playerBodyshotCounts[attacker.userID] == 1)
                            {
                                timer.Once(_config.НастройкаАима.НастройкаПП.isTwoPPCD, () => playerBodyshotCounts[attacker.userID] = 0);
                            }

                            int bCount = playerBodyshotCounts[attacker.userID];
                            int mbCount = _config.НастройкаАима.НастройкаПП.isTwoPPB;
                            if (bCount == mbCount)
                            {
                                DetectNo1B(attacker, victim, weaponfirePp, metrpopal, bCount, mbCount, true);
                                playerBodyshotCounts[attacker.userID] = 0;
                            }
                            else
                            {
                                DetectNo1B(attacker, victim, weaponfirePp, metrpopal, bCount, mbCount, false);
                            }
                        }
                    }
                }

                if (weaponfirePistol != null && _config.НастройкаАима.НастройкаПистолетов.weaponpistol.Contains(weaponfirePistol))
                {
                    if (metrpopal > _config.НастройкаАима.НастройкаПистолетов.metrpistol)
                    {
                        if (_config.НастройкаАима.isAimBlocDamage)
                        {
                            info.damageTypes.ScaleAll(0.01f);
                        }
                        if (info.isHeadshot)
                        {
                            playerHeadshotCounts[attacker.userID] += 1;
                            if (playerHeadshotCounts[attacker.userID] == 1)
                            {
                                timer.Once(_config.НастройкаАима.НастройкаПистолетов.isTwoPistolCD, () => playerHeadshotCounts[attacker.userID] = 0);
                            }

                            int dCount = playerHeadshotCounts[attacker.userID];
                            int mCount = _config.НастройкаАима.НастройкаПистолетов.isTwoPistolH;
                            if (dCount == mCount)
                            {
                                DetectNo1(attacker, victim, weaponfirePistol, metrpopal, dCount, mCount, true);
                                playerHeadshotCounts[attacker.userID] = 0;
                            }
                            else
                            {
                                DetectNo1(attacker, victim, weaponfirePistol, metrpopal, dCount, mCount, false);
                            }
                        }
                        else
                        {
                            playerBodyshotCounts[attacker.userID] += 1;
                            if (playerBodyshotCounts[attacker.userID] == 1)
                            {
                                timer.Once(_config.НастройкаАима.НастройкаПистолетов.isTwoPistolCD, () => playerBodyshotCounts[attacker.userID] = 0);
                            }

                            int bCount = playerBodyshotCounts[attacker.userID];
                            int mbCount = _config.НастройкаАима.НастройкаПистолетов.isTwoPistolB;
                            if (bCount == mbCount)
                            {
                                DetectNo1B(attacker, victim, weaponfirePistol, metrpopal, bCount, mbCount, true);
                                playerBodyshotCounts[attacker.userID] = 0;
                            }
                            else
                            {
                                DetectNo1B(attacker, victim, weaponfirePistol, metrpopal, bCount, mbCount, false);
                            }
                        }
                    }
                }

                if (weaponfireBows != null && _config.НастройкаАима.НастройкаЛуков.weaponbow.Contains(weaponfireBows))
                {
                    if (metrpopal > _config.НастройкаАима.НастройкаЛуков.metrbow)
                    {
                        if (_config.НастройкаАима.isAimBlocDamage)
                        {
                            info.damageTypes.ScaleAll(0.01f);
                        }
                        if (info.isHeadshot)
                        {
                            playerHeadshotCounts[attacker.userID] += 1;
                            if (playerHeadshotCounts[attacker.userID] == 1)
                            {
                                timer.Once(_config.НастройкаАима.НастройкаЛуков.isTwoBowCD, () => playerHeadshotCounts[attacker.userID] = 0);
                            }

                            int dCount = playerHeadshotCounts[attacker.userID];
                            int mCount = _config.НастройкаАима.НастройкаЛуков.isTwoBowH;
                            if (dCount == mCount)
                            {
                                DetectNo1(attacker, victim, weaponfireBows, metrpopal, dCount, mCount, true);
                                playerHeadshotCounts[attacker.userID] = 0;
                            }
                            else
                            {
                                DetectNo1(attacker, victim, weaponfireBows, metrpopal, dCount, mCount, false);
                            }
                        }
                        else
                        {
                            playerBodyshotCounts[attacker.userID] += 1;
                            if (playerBodyshotCounts[attacker.userID] == 1)
                            {
                                timer.Once(_config.НастройкаАима.НастройкаЛуков.isTwoBowCD, () => playerBodyshotCounts[attacker.userID] = 0);
                            }

                            int bCount = playerBodyshotCounts[attacker.userID];
                            int mbCount = _config.НастройкаАима.НастройкаЛуков.isTwoBowB;
                            if (bCount == mbCount)
                            {
                                DetectNo1B(attacker, victim, weaponfireBows, metrpopal, bCount, mbCount, true);
                                playerBodyshotCounts[attacker.userID] = 0;
                            }
                            else
                            {
                                DetectNo1B(attacker, victim, weaponfireBows, metrpopal, bCount, mbCount, false);
                            }
                        }
                    }
                }

                if (weaponfireSniper != null && _config.НастройкаАима.НастройкаСнайперскихВинтовок.weaponsniper.Contains(weaponfireSniper))
                {
                    if (metrpopal > _config.НастройкаАима.НастройкаСнайперскихВинтовок.metrsniper && info.isHeadshot)
                    {
                        if (_config.НастройкаАима.isAimBlocDamage)
                        {
                            info.damageTypes.ScaleAll(0.01f);
                        }

                        playerHeadshotCounts[attacker.userID] += 1;
                        if (playerHeadshotCounts[attacker.userID] == 1)
                        {
                            timer.Once(10f, () => playerHeadshotCounts[attacker.userID] = 0);
                        }

                        int dCount = playerHeadshotCounts[attacker.userID];
                        int mCount = 1;
                        if (dCount == mCount)
                        {
                            DetectNo1(attacker, victim, weaponfireSniper, metrpopal, dCount, mCount, true);
                        }
                        else
                        {
                            DetectNo1(attacker, victim, weaponfireSniper, metrpopal, dCount, mCount, false);
                        }
                    }
                }

                if (_config.ДетектНочью.isNH)
                {
                    if (metrpopal > _config.ДетектНочью.metrnh)
                    {
                        if (!victim.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
                        {
                            if (!victim.HasPlayerFlag(BasePlayer.PlayerFlags.Wounded))
                            {
                                var currentTime = ConVar.Env.time;
                                var roundedMetrpopal = Math.Round(metrpopal, 2);
                                var roundedCurrentTime = Math.Round(currentTime % 24, 0);
                                var nightStartTime = 23f;
                                var nightEndTime = 5f;
                                bool hasFlashlight = false;
                                bool hasNightVision = false;
                                string weaponnight = info.Weapon?.GetItem()?.info.shortname;
                                if (attacker.GetActiveItem()?.GetHeldEntity() is BaseProjectile weapon)
                                {
                                    hasFlashlight = weapon.HasFlag(BaseEntity.Flags.On);
                                }
                                foreach (var item in attacker.inventory.containerWear.itemList)
                                {
                                    if (item.info.shortname == "nightvisiongoggles")
                                    {
                                        hasNightVision = true;
                                        break;
                                    }
                                }
                                if (IsNighttime() && (!hasFlashlight || !hasNightVision))
                                {
                                    string jsonwebhooknightshot = jsonwebhooknightshotRU;
                                    DetectNo9(attacker, victim, metrpopal, roundedCurrentTime, weaponnight, hasFlashlight, hasNightVision);
                                    Puts($"Игрок {attacker.displayName} {_config.ДетектНочью.NameNightShot}");
                                    string banReason = $"{_config.ДетектНочью.NameNightShot}";
                                    ChatDetect(attacker, banReason);
                                }
                            }
                        }
                    }
                }
            }
        }

        private bool IsNighttime()
        {
            float time = TOD_Sky.Instance.Cycle.Hour;
            return time >= 23f || time < 5f;
        }
        
        #endregion

        #region stash 

        private Dictionary<ulong, int> detectCounts = new Dictionary<ulong, int>();

        private void OnStashExposed(StashContainer stash, BasePlayer player)
        {
            if (!detectCounts.ContainsKey(player.userID))
            {
                detectCounts[player.userID] = 0;
            }
            OnStashTriggered(stash, player, false);
        }

        private void OnStashTriggered(StashContainer stash, BasePlayer player, bool stashWasDestroyed)
        {   
            if (!_config.Ловушки.isStash)
                return;
            
            if (stash.OwnerID != player.userID)
            {
                if (player.Team != null && player.Team.members != null && player.Team.members.Contains(stash.OwnerID))
                {
                    return;
                }
                
                if (!detectCounts.ContainsKey(player.userID))
                {
                    detectCounts[player.userID] = 0;
                }
                
                if (detectCounts[player.userID] == 1)
                {
                    timer.Once(900f, () =>
                    {
                        detectCounts[player.userID] = 0;
                    });
                }
                bool aresleepbag = false;
                string nearbyObjectsSTASH = GetNearbyObjectsSTASH(player.transform.position, 10f);
                if (HasForbiddenObjectsSTASH(nearbyObjectsSTASH))
                {
                    aresleepbag = true;
                }
                
                detectCounts[player.userID]++;
                int DCount = detectCounts[player.userID];
                string Grid = GetGridString(player.transform.position);
                bool steam = isSteam(player);
                string playerInfo = CheckInfoS(player.userID.ToString());
                
                if (detectCounts[player.userID] != _config.Ловушки.stashBCount)
                {
                    RequestDC(jsonwebhookstashdetectRU.Replace("[steamid]", $"{player.userID}")
                        .Replace("[banordetect]", "Блокировка")
                        .Replace("[reason]", _config.Ловушки.NameStash)
                        .Replace("[grid]", Grid)
                        .Replace("[cords]", player.transform.position.ToString())
                        .Replace("[dCount]", DCount.ToString())
                        .Replace("[mCount]", _config.Ловушки.stashBCount.ToString())
                        .Replace("[image]", _config.Логирование.WebhookImage)
                        .Replace("[player]", $"{player.displayName} | {player.userID}")
                        .Replace("[pirate]", steam ? "Да" : "Нет")
                        .Replace("[sleepbag]", aresleepbag ? "Да" : "Нет")
                        .Replace("[playerInfo]", playerInfo));
                }
                
                if (detectCounts[player.userID] >= _config.Ловушки.stashBCount)
                {                    
                    RequestDC(jsonwebhookstashdetectRU.Replace("[steamid]", $"{player.userID}")
                        .Replace("[banordetect]", "Детект")
                        .Replace("[reason]", _config.Ловушки.NameStash)
                        .Replace("[grid]", Grid)
                        .Replace("[cords]", player.transform.position.ToString())
                        .Replace("[dCount]", DCount.ToString())
                        .Replace("[mCount]", _config.Ловушки.stashBCount.ToString())
                        .Replace("[image]", _config.Логирование.WebhookImage)
                        .Replace("[player]", $"{player.displayName} | {player.userID}")
                        .Replace("[pirate]", steam ? "Да" : "Нет")
                        .Replace("[sleepbag]", aresleepbag ? "Да" : "Нет")
                        .Replace("[playerInfo]", playerInfo));
                    ChatDetect(player, _config.НастройкаАима.NameAim);
                    
                    string banReason = $"{_config.Ловушки.NameStash}";
                    
                    rust.RunServerCommand($"{_config.Общее.BanCommand} {player.userID} \"{banReason}\"");
                }
            }
        }

        string GetNearbyObjectsSTASH(Vector3 position, float radius)
        {
            Collider[] colliders = Physics.OverlapSphere(position, radius);
            Dictionary<string, int> objectCounts = new Dictionary<string, int>();

            foreach (Collider collider in colliders)
            {
                string objectName = collider.gameObject.name;

                if (objectCounts.ContainsKey(objectName))
                {
                    objectCounts[objectName]++;
                }
                else
                {
                    objectCounts[objectName] = 1;
                }
            }

            string nearbyObjectsSTASH = "";
            foreach (KeyValuePair<string, int> entry in objectCounts)
            {
                nearbyObjectsSTASH += entry.Key;
                if (entry.Value > 1)
                {
                    nearbyObjectsSTASH += $" ({entry.Value} шт.)";
                }
                nearbyObjectsSTASH += ", ";
            }

            if (nearbyObjectsSTASH.Length > 0)
            {
                nearbyObjectsSTASH = nearbyObjectsSTASH.TrimEnd(',', ' ');
            }

            return nearbyObjectsSTASH;
        }

        bool HasForbiddenObjectsSTASH(string nearbyObjectsSTASH)
        {
            string[] forbiddenObjectsSTASH = new string[] { "assets/bundled/prefabs/static/sleepingbag_static.prefab" };

            foreach (string forbiddenObjectSTASH in forbiddenObjectsSTASH)
            {
                if (nearbyObjectsSTASH.Contains(forbiddenObjectSTASH))
                {
                    return true;
                }
            }
            return false;
        }

        #endregion
        
        #region Fly

        private Dictionary<ulong, int> eyeHackKicks = new Dictionary<ulong, int>();
        
        object OnPlayerViolation(BasePlayer player, AntiHackType type, float amount)
        {
            if (player.net.connection.authLevel >= 1)
                return false;
            if (type == AntiHackType.FlyHack)
            {
                string banReason = $"{_config.ФлайХак.NameFly}";
                ChatDetect(player, banReason);
                if (player.GetParentEntity() != null && player.GetParentEntity().ShortPrefabName == "ladder.wooden.wall")
                {
                    return false;
                }

                if (!_config.ФлайХак.isFly)
                    return null;
                string nearbyObjects = GetNearbyObjects(player.transform.position, 2.5f);
                string nearbyObjects2 = GetNearbyObjects(player.transform.position, 30f);
                if (HasForbiddenObjectsHeli(nearbyObjects2))
                {
                    Puts($"Игрок {player.userID} находится рядом с запрещенными объектами и не будет кикнут.");
                    return false;
                }
                if (HasForbiddenObjects(nearbyObjects))
                {
                    Puts($"Игрок {player.userID} находится рядом с запрещенными объектами и не будет кикнут.");
                    return false;
                }
                string playerName = player.displayName;
                if (_config.ФлайХак.isFlyBan)
                {
                    DetectNo3(player, true);
                    rust.RunServerCommand($"{_config.Общее.BanCommand} {player.userID} \"{banReason}\"");
                }
                if (!_config.ФлайХак.isFlyBan)
                {
                    DetectNo3(player, false);
                    rust.RunServerCommand($"kick {player.userID} \"{banReason}\"");
                }
                Puts($"Игрок {player.userID} {_config.ФлайХак.NameFly}");
                return false;
            }
            else if (type == AntiHackType.EyeHack)
            {

                var data = Interface.Oxide.DataFileSystem.GetFile("ACS_MDD");
                var playerData = new Dictionary<string, object>
                {
                    ["date"] = DateTime.Now.ToString(),
                };
                data["players", player.userID.ToString()] = playerData;
                data.Save();

                return false;
            }
            return false;
        }
        string GetNearbyObjects(Vector3 position, float radius)
        {
            Collider[] colliders = Physics.OverlapSphere(position, radius);
            Dictionary<string, int> objectCounts = new Dictionary<string, int>();

            foreach (Collider collider in colliders)
            {
                string objectName = collider.gameObject.name;
                if (objectName == "assets/prefabs/player/player.prefab" ||
                    objectName == "Prevent_Movement" ||
                    objectName == "New Game Object" ||
                    objectName == "prevent_building" ||
                    objectName == "RadiationSphere" ||
                    objectName == "Fog Volume" ||
                    objectName == "TargetDetection")
                {
                    continue;
                }

                if (objectCounts.ContainsKey(objectName))
                {
                    objectCounts[objectName]++;
                }
                else
                {
                    objectCounts[objectName] = 1;
                }
            }

            string nearbyObjects = "";
            foreach (KeyValuePair<string, int> entry in objectCounts)
            {
                nearbyObjects += entry.Key;
                if (entry.Value > 1)
                {
                    nearbyObjects += $" ({entry.Value} шт.)";
                }
                nearbyObjects += ", ";
            }

            if (nearbyObjects.Length > 0)
            {
                nearbyObjects = nearbyObjects.TrimEnd(',', ' ');
            }

            return nearbyObjects;
        }

        bool HasForbiddenObjects(string nearbyObjects)
        {
            string[] forbiddenObjects = new string[] { "quarry_main", "quarry_track", "Server",
                "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab", 
                "assets/content/vehicles/boats/cargoship/cargoshiptest.prefab",
                "assets/prefabs/misc/supply drop/supply_drop.prefab", "assets/prefabs/misc/supply", "drop/supply_drop.prefab",
                "assets/prefabs/deployable/quarry/engineswitch.prefab", "minicopter.entity", "assets/content/vehicles/minicopter/minicopter.entity.prefab", "Ladder_4", "Ladder Trigger" };

            foreach (string forbiddenObject in forbiddenObjects)
            {
                if (nearbyObjects.Contains(forbiddenObject))
                {
                    return true;
                }
            }
            return false;
        }
        bool HasForbiddenObjectsHeli(string nearbyObjects)
        {
            string[] forbiddenObjects = new string[] {
                "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab", 
                "assets/content/vehicles/boats/cargoship/cargoshiptest.prefab", "assets/prefabs/misc/supply", "drop/supply_drop.prefab",
                "minicopter.entity", "assets/content/vehicles/minicopter/minicopter.entity.prefab" };

            foreach (string forbiddenObject in forbiddenObjects)
            {
                if (nearbyObjects.Contains(forbiddenObject))
                {
                    return true;
                }
            }
            return false;
        }
        bool HasForbiddenObjectsManipDoor(string nearbyObjects)
        {
            string[] forbiddenObjects = new string[] { "quarry_main", "quarry_track", "Server", "assets/prefabs/building/door.double.hinged/door.double.hinged.metal.prefab", "assets/prefabs/building/door.hinged/door.hinged.wood.prefab", "assets/prefabs/building/door.hinged/door.hinged.metal.prefab", "assets/prefabs/building/door.hinged/door.hinged.toptier.prefab", "assets/prefabs/building/door.double.hinged/door.double.hinged.wood.prefab", "assets/prefabs/building/door.double.hinged/door.double.hinged.toptier.prefab", "assets/prefabs/building/wall.frame.garagedoor/wall.frame.garagedoor.prefab", "assets/bundled/prefabs/static/door.hinged.garage_a.prefab",
                "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab", "assets/content/structures/train_wagons/train_wagon_a.prefab", "assets/content/structures/train_wagons/train_wagon_b.prefab",
                "assets/content/structures/train_wagons/train_wagon_c.prefab", "assets/content/structures/train_wagons/train_wagon_d.prefab", "assets/content/structures/train_wagons/train_wagon_e.prefab",
                "assets/content/structures/train_crane/train_crane_a.prefab", "assets/content/structures/train_tracks/crane_track_150x900.prefab", "assets/content/structures/train_tracks/crane_track_150x900_end.prefab",
                "assets/content/structures/train_tracks/train_track_3x18.prefab", "assets/content/structures/train_tracks/train_track_3x36.prefab", "assets/content/structures/train_tracks/train_track_3x3_end.prefab",
                "assets/content/structures/train_tracks/train_track_3x9.prefab", "assets/content/structures/train_tracks/train_track_bend_45.prefab", "assets/content/structures/train_tracks/train_track_nogravel_3x18.prefab",
                "assets/content/structures/train_tracks/train_track_nogravel_3x36.prefab", "assets/content/structures/train_tracks/train_track_nogravel_3x3_end.prefab", "assets/content/structures/train_tracks/train_track_nogravel_3x9.prefab",
                "assets/content/structures/train_tracks/train_track_nogravel_bend_45.prefab", "assets/content/structures/train_tracks/train_track_nogravel_sleft_3x027.prefab", "assets/content/structures/train_tracks/train_track_nogravel_sright_3x27.prefab", "assets/content/structures/train_tracks/train_track_sleft_3x027.prefab",
                "assets/content/structures/train_tracks/train_track_sright_3x27.prefab", "assets/content/structures/harbor/tugboat/tugboat_a.prefab", "assets/content/structures/harbor/tugboat/tugboat_a_interior.prefab",
                "assets/content/structures/harbor/tugboat/tugboat_a_snow.prefab", "assets/content/vehicles/boats/cargoship/cargoshiptest.prefab", "assets/content/vehicles/boats/rhib/rhib.prefab", "assets/content/vehicles/boats/rhib/subents/fuel_storage.prefab",
                "assets/content/vehicles/boats/rhib/subents/rhib_storage.prefab", "assets/content/vehicles/boats/rowboat/metalrowboat.prefab", "assets/content/vehicles/boats/rowboat/oldwoodenrowboat.prefab", "assets/content/vehicles/boats/rowboat/rowboat.prefab",
                "assets/content/vehicles/boats/rowboat/subents/fuel_storage.prefab", "assets/content/vehicles/boats/rowboat/subents/rowboat_storage.prefab",
                "assets/content/vehicles/boats/cargoship/cargoshiptest.prefab",
                "assets/prefabs/misc/supply drop/supply_drop.prefab", "assets/prefabs/misc/supply", "drop/supply_drop.prefab",
                "assets/prefabs/deployable/quarry/engineswitch.prefab", "minicopter.entity", "assets/content/vehicles/minicopter/minicopter.entity.prefab" };

            foreach (string forbiddenObject in forbiddenObjects)
            {
                if (nearbyObjects.Contains(forbiddenObject))
                {
                    return true;
                }
            }
            return false;
        }
        
        #endregion
        
        #region connect

        void OnPlayerConnected(BasePlayer player)
        {
            string playerName = player.displayName;
            string playerIP = player.net.connection.ipaddress.Split(':')[0];
            if (_config.Стим.PROXY_CHECT)
            {
                CheckPlayerIP(player.userID.ToString(), playerIP, playerName);
            }
            if (_config.Стим.STEAM_CHECT)
            {
                CheckAccountAge(player.userID.ToString());
            }
            CheckInfoS(player.userID.ToString());
        }

        private bool isSteam(BasePlayer player)
        {
            if (!player == null)
            {
                if (MultiFighting == null) return true;
                return MultiFighting.Call<bool>("IsSteam", player);
            }
            else 
            {
                return true;
            }
        }

        private void CheckPlayerIP(string userID, string playerIP, string playerName)
        {
            var data = Interface.Oxide.DataFileSystem.GetFile("PlayerIPs");
            if (data.Exists() && data["players", userID] != null)
            {
                var playerData = data["players", userID] as Dictionary<string, object>;
                bool isProxy = (bool)playerData["is_proxy"];
                if (isProxy)
                {
                    Puts($"Игрок {playerName} ({playerIP}) использует PROXY, бан :D");
                }
                else
                {
                    Puts($"Игрок {playerName} ({playerIP}) не использует PROXY, хороший мальчик :D");
                }
            }
            else
            {
                string url = $"https://api.ip2location.io/?key={_config.Стим.YOUR_API_KEY}&ip={playerIP}";
                webrequest.Enqueue(url, "", (code, response) =>
                {
                    if (code == 200 && !string.IsNullOrEmpty(response))
                    {
                        IPInfo ipInfo = JsonConvert.DeserializeObject<IPInfo>(response);
                        bool isProxy = ipInfo.is_proxy;
                        if (isProxy)
                        {
                            Puts($"Игрок {playerName} ({playerIP}) использует PROXY, бан :D");
                            rust.RunServerCommand($"{_config.Общее.BanCommand} \"{userID}\" \"Плохой мальчик, выключай PROXY\"");
                            // тут позже логирование
                        }
                        else
                        {
                            Puts($"Игрок {playerName} ({playerIP}) не использует PROXY, хороший мальчик :D");
                        }
                        SavePlayerIP(userID, playerIP, isProxy);
                    }
                }, this, RequestMethod.GET);
            }
        }

        private void SavePlayerIP(string userID, string playerIP, bool isProxy)
        {
            var data = Interface.Oxide.DataFileSystem.GetFile("PlayerIPs");
            var playerData = new Dictionary<string, object>
            {
                ["ip"] = playerIP,
                ["is_proxy"] = isProxy
            };
            data["players", userID] = playerData;
            data.Save();
        }

        [System.Serializable]
        public class IPInfo
        {
            public string ip;
            public string country_code;
            public string country_name;
            public string region_name;
            public string city_name;
            public float latitude;
            public float longitude;
            public string zip_code;
            public string time_zone;
            public string asn;
            public string @as;
            public bool is_proxy;
        }
        
        #endregion
        
        /*#region CodeBan
        
        private static bool IsBanned(ulong userid)
        {
            return ServerUsers.Is(userid, ServerUsers.UserGroup.Banned);
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
            if (IsBanned(owner))
            {
                if (_config.isCodeBan)
                {
                    string playerName = player.displayName;
                    string banReason = $"{_config.NameCodeBan}";
                    string jsonwebhookcode = jsonwebhookcodeRU;
                    RequestDC(jsonwebhookcode.Replace("[steamid]", player.displayName + " | " + player.userID)
                        .Replace("[Reason]", _config.NameCodeBan));
                    rust.RunServerCommand($"{_config.BanCommand} \"{player.userID}\" \"{banReason}\"");
                    Puts($"Игрок {player.userID} {_config.NameCodeBan}");
                    BasePlayer targetPlayer = BasePlayer.FindByID(player.userID);
                }
            }
        }      
        
        #endregion*/

        #region Logs
        private void DetectNo2(BasePlayer player)
        {
            string Grid = GetGridString(player.transform.position);
            bool steam = isSteam(player);
            string playerInfo = CheckInfoS(player.userID.ToString());
            string nearbyObjects = GetNearbyObjects(player.transform.position, 2.5f);
            if (_config.Манипулятор.isAimDrive)
            {
                    RequestDC(jsonwebhookviolationRU.Replace("[steamid]", $"{player.userID}")
                        .Replace("[banordetect]", "Блокировка")
                        .Replace("[reason]", _config.Манипулятор.NameAimDrive)
                        .Replace("[grid]", Grid)
                        .Replace("[cords]", player.transform.position.ToString())
                        .Replace("[image]", _config.Логирование.WebhookImage)
                        .Replace("[player]", $"{player.displayName} | {player.userID}")
                        .Replace("[pirate]", steam ? "Да" : "Нет")
                        .Replace("[playerInfo]", playerInfo));
                    rust.RunServerCommand($"{_config.Общее.BanCommand} \"{player.userID}\" \"{_config.Манипулятор.NameAimDrive}\"");
            }
        }
        private void DetectNo3(BasePlayer player, bool banlogorlog)
        {
            string Grid = GetGridString(player.transform.position);
            bool steam = isSteam(player);
            string playerInfo = CheckInfoS(player.userID.ToString());
            string nearbyObjects = GetNearbyObjects(player.transform.position, 2.5f);
            if (banlogorlog)
            {
                RequestDC(jsonwebhookviolationRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Блокировка")
                    .Replace("[reason]", _config.ФлайХак.NameFly)
                    .Replace("[grid]", Grid)
                    .Replace("[object]", nearbyObjects)
                    .Replace("[cords]", player.transform.position.ToString())
                    .Replace("[image]", _config.Логирование.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo));
            }
            else 
            {
                RequestDC(jsonwebhookviolationRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Кик")
                    .Replace("[reason]", _config.ФлайХак.NameFly)
                    .Replace("[grid]", Grid)
                    .Replace("[object]", nearbyObjects)
                    .Replace("[cords]", player.transform.position.ToString())
                    .Replace("[image]", _config.Логирование.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo));
                ChatDetect(player, _config.НастройкаАима.NameAim);
            }
        }
        private void DetectNo4(BasePlayer player, int DCount,/* bool aresleepbag,*/ bool banlogorlog)
        {
            string Grid = GetGridString(player.transform.position);
            bool steam = isSteam(player);
            string playerInfo = CheckInfoS(player.userID.ToString());
            if (banlogorlog)
            {
                RequestDC(jsonwebhookstashdetectRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Блокировка")
                    .Replace("[reason]", _config.Ловушки.NameStash)
                    .Replace("[grid]", Grid)
                    .Replace("[cords]", player.transform.position.ToString())
                    .Replace("[dCount]", DCount.ToString())
                    .Replace("[mCount]", _config.Ловушки.stashBCount.ToString())
                    .Replace("[image]", _config.Логирование.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    /*.Replace("[sleepbag]", aresleepbag ? "Да" : "Нет")*/
                    .Replace("[playerInfo]", playerInfo));
            }
            else 
            {
                RequestDC(jsonwebhookstashdetectRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Детект")
                    .Replace("[reason]", _config.Ловушки.NameStash)
                    .Replace("[grid]", Grid)
                    .Replace("[cords]", player.transform.position.ToString())
                    .Replace("[dCount]", DCount.ToString())
                    .Replace("[mCount]", _config.Ловушки.stashBCount.ToString())
                    .Replace("[image]", _config.Логирование.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    /*.Replace("[sleepbag]", aresleepbag ? "Да" : "Нет")*/
                    .Replace("[playerInfo]", playerInfo));
                ChatDetect(player, _config.НастройкаАима.NameAim);
            }
        }
        private void DetectNo8(BasePlayer player, string weaponname, bool banlogorlog)
        {
            string Grid = GetGridString(player.transform.position);
            bool steam = isSteam(player);
            string playerInfo = CheckInfoS(player.userID.ToString());
            if (banlogorlog)
            {
                RequestDC(jsonwebhookrapidRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Блокировка")
                    .Replace("[reason]", _config.Манипулятор.NameAimMR)
                    .Replace("[grid]", Grid)
                    .Replace("[weapon]", weaponname)
                    .Replace("[image]", _config.Логирование.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo));
            }
            /*else 
            {
                RequestDC(jsonwebhookrapidRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Детект")
                    .Replace("[reason]", _config.Манипулятор.NameAimMR)
                    .Replace("[grid]", Grid)
                    .Replace("[weapon]", weaponname)
                    .Replace("[dCount]", dCount.ToString())
                    .Replace("[mCount]", "3")
                    .Replace("[image]", _config.Логирование.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo));
                ChatDetect(player, _config.НастройкаАима.NameAim);
            }*/
        }
        private void DetectNo9(BasePlayer player, BasePlayer victim, float metrpopal, double time, string weaponnight, bool hasFlashlight, bool hasNightVision)
        {
            string Grid = GetGridString(player.transform.position);
            bool steam = isSteam(player);
            string playerInfo = CheckInfoS(player.userID.ToString());
            
            RequestDC(jsonwebhooknightshotRU.Replace("[steamid]", $"{player.userID}")
                .Replace("[reason]", _config.ДетектНочью.NameNightShot)
                .Replace("[metr]", Math.Round(metrpopal).ToString())
                .Replace("[grid]", Grid)
                .Replace("[flash]", hasFlashlight || hasNightVision ? "Есть" : "Нет")
                .Replace("[time]", time.ToString())
                .Replace("[weapon]", weaponnight)
                .Replace("[image]", _config.Логирование.WebhookImage)
                .Replace("[player]", $"{player.displayName} | {player.userID}")
                .Replace("[pirate]", steam ? "Да" : "Нет")
                .Replace("[playerInfo]", playerInfo)
                .Replace("[victim]", /*$"{victim.displayName} | {victim.userID}"*/ $"{victim.displayName}"));
        }
        private void DetectNo7(BasePlayer player, BasePlayer victim, string weaponname, int dCount, float metrp, bool banlogorlog)
        {
            string Grid = GetGridString(player.transform.position);
            bool steam = isSteam(player);
            string playerInfo = CheckInfoS(player.userID.ToString());
            if (banlogorlog)
            {
                RequestDC(jsonwebhookmanipRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Блокировка")
                    .Replace("[reason]", _config.Манипулятор.NameAimM)
                    .Replace("[metr]", Math.Round(metrp).ToString())
                    .Replace("[grid]", Grid)
                    .Replace("[weapon]", weaponname)
                    .Replace("[dCount]", dCount.ToString())
                    .Replace("[mCount]", "3")
                    .Replace("[image]", _config.Логирование.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo)
                    .Replace("[victim]", /*$"{victim.displayName} | {victim.userID}"*/ $"{victim.displayName}"));
            }
            else 
            {
                RequestDC(jsonwebhookmanipRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Детект")
                    .Replace("[reason]", _config.Манипулятор.NameAimM)
                    .Replace("[metr]", Math.Round(metrp).ToString())
                    .Replace("[grid]", Grid)
                    .Replace("[weapon]", weaponname)
                    .Replace("[dCount]", dCount.ToString())
                    .Replace("[mCount]", "3")
                    .Replace("[image]", _config.Логирование.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo)
                    .Replace("[victim]", /*$"{victim.displayName} | {victim.userID}"*/ $"{victim.displayName}"));
                ChatDetect(player, _config.НастройкаАима.NameAim);
            }
        }
        private void DetectNo1(BasePlayer player, BasePlayer victim, string weaponname, float metrpopal, int dCount, int mCount, bool banlogorlog)
        {
            string Grid = GetGridString(player.transform.position);
            bool steam = isSteam(player);
            string playerInfo = CheckInfoS(player.userID.ToString());
            if (banlogorlog)
            {
                if (_config.НастройкаАима.isAimBan)
                {
                   rust.RunServerCommand($"{_config.Общее.BanCommand} \"{player.userID}\" \"{_config.НастройкаАима.NameAim}\"");
                }
                else
                {
                    rust.RunServerCommand($"kick \"{player.userID}\" \"{_config.НастройкаАима.NameAim}\"");
                }

                RequestDC(jsonwebhookaimdetectRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Блокировка")
                    .Replace("[headorbody]", "Голова")
                    .Replace("[metr]", Math.Round(metrpopal).ToString())
                    .Replace("[grid]", Grid)
                    .Replace("[reason]", _config.НастройкаАима.NameAim)
                    .Replace("[weapon]", weaponname)
                    .Replace("[dCount]", dCount.ToString())
                    .Replace("[mCount]", mCount.ToString())
                    .Replace("[image]", _config.Логирование.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo)
                    .Replace("[victim]", /*$"{victim.displayName} | {victim.userID}"*/ $"{victim.displayName}"));
            }
            else 
            {
                RequestDC(jsonwebhookaimdetectRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Детект")
                    .Replace("[headorbody]", "Голова")
                    .Replace("[metr]", Math.Round(metrpopal).ToString())
                    .Replace("[grid]", Grid)
                    .Replace("[reason]", _config.НастройкаАима.NameAim)
                    .Replace("[weapon]", weaponname)
                    .Replace("[dCount]", dCount.ToString())
                    .Replace("[mCount]", mCount.ToString())
                    .Replace("[image]", _config.Логирование.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo)
                    .Replace("[victim]", /*$"{victim.displayName} | {victim.userID}"*/ $"{victim.displayName}"));
                ChatDetect(player, _config.НастройкаАима.NameAim);
            }
        }
        private void DetectNo1B(BasePlayer player, BasePlayer victim, string weaponname, float metrpopal, int bCount, int mbCount, bool banlogorlog)
        {
            string Grid = GetGridString(player.transform.position);
            bool steam = isSteam(player);
            string playerInfo = CheckInfoS(player.userID.ToString());
            if (banlogorlog)
            {
                if (_config.НастройкаАима.isAimBan)
                {
                   rust.RunServerCommand($"{_config.Общее.BanCommand} \"{player.userID}\" \"{_config.НастройкаАима.NameAim}\"");
                }
                else
                {
                    rust.RunServerCommand($"kick \"{player.userID}\" \"{_config.НастройкаАима.NameAim}\"");
                }

                RequestDC(jsonwebhookaimdetectRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Блокировка")
                    .Replace("[headorbody]", "Тело")
                    .Replace("[metr]", Math.Round(metrpopal).ToString())
                    .Replace("[grid]", Grid)
                    .Replace("[reason]", _config.НастройкаАима.NameAim)
                    .Replace("[weapon]", weaponname)
                    .Replace("[dCount]", bCount.ToString())
                    .Replace("[mCount]", mbCount.ToString())
                    .Replace("[image]", _config.Логирование.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo)
                    .Replace("[victim]", /*$"{victim.displayName} | {victim.userID}"*/ $"{victim.displayName}"));
            }
            else 
            {
                RequestDC(jsonwebhookaimdetectRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Детект")
                    .Replace("[headorbody]", "Тело")
                    .Replace("[metr]", Math.Round(metrpopal).ToString())
                    .Replace("[grid]", Grid)
                    .Replace("[reason]", _config.НастройкаАима.NameAim)
                    .Replace("[weapon]", weaponname)
                    .Replace("[dCount]", bCount.ToString())
                    .Replace("[mCount]", mbCount.ToString())
                    .Replace("[image]", _config.Логирование.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo)
                    .Replace("[victim]", /*$"{victim.displayName} | {victim.userID}"*/ $"{victim.displayName}"));
                ChatDetect(player, _config.НастройкаАима.NameAim);
            }
        }

        private string GetGridString(Vector3 position)
        {
            Vector2 adjPosition = new Vector2((World.Size / 2) + position.x, (World.Size / 2) - position.z);
            return $"{NumberToString((int)(adjPosition.x / 150))}{(int)(adjPosition.y / 150)}";
        }

        private string NumberToString(int number)
        {
            bool a = number > 26;
            Char c = (Char)(65 + (a ? number - 26 : number));
            return a ? "A" + c : c.ToString();
        }

        private string CheckInfoS(string userID)
        {
            var data = Interface.Oxide.DataFileSystem.GetFile("ACS_JOIN");
            var playerData = data["players", userID] as Dictionary<string, object>;

            if (playerData != null)
            {
                DateTime firstJoin = Convert.ToDateTime(playerData["FirstJoin"]);
                double accountAge = Convert.ToDouble(playerData["accountAge"]);
                int roundedAccountAge = (int)Math.Round(accountAge);

                string infoString = $"Первый раз зашел на сервер: {firstJoin} (по данным анти-чита)\\nАккаунт создан: {roundedAccountAge}д. назад";

                return infoString;
            }

            return "Информация о регистрации и входе не найдена.";
        }

        #endregion

        #region SteamDay

        private void CheckAccountAge(string userID)
        {
            string urlsc = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={_config.Стим.SteamAPI}&steamids={userID}";
                webrequest.Enqueue(urlsc, "", (code, response) =>
                {
                    if (code == 403)
                    {
                        Debug.LogError("[AntiCheatSneak] Ошибка " + code + " Возможно API Key устарел");
                        return;
                    }

                    if (code == 200)
                    {
                        INFO info = new INFO();
                        resp steamResponse = JsonConvert.DeserializeObject<resp>(response);

                        int datetime = steamResponse.response.players[0].timecreated ?? 0;
                        DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        DateTime create = epoch.AddSeconds(datetime).AddHours(3);

                        TimeSpan accountAge = DateTime.UtcNow - create;

                        var data = Interface.Oxide.DataFileSystem.GetFile("ACS_JOIN");
                        var playerData = data["players", userID] as Dictionary<string, object>;

                        if (playerData == null)
                        {
                            playerData = new Dictionary<string, object>
                            {
                                ["FirstJoin"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss"),
                                ["accountAge"] = accountAge.TotalDays
                            };

                            data["players", userID] = playerData;
                            data.Save();
                        }
                        else
                        {
                            // пон
                        }

                        if (accountAge.TotalDays < _config.Стим.SteamDays)
                        {
                            rust.RunServerCommand($"kick {userID} \"слишком молодой аккаунт!\"");
                        }
                    }
                    else
                    {
                        Debug.LogError("[AntiCheatSneak] Ошибка " + code);
                    }
                }, this, RequestMethod.GET, null, 0f);
            
        }


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
        

        #endregion

        #region RequestDC

        private string jsonwebhookviolationRU =
            "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatSneak-Логирование-[banordetect]\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatsneak.4/\",\"color\":null,\"fields\":[{\"name\":\"Координаты X Y Z\",\"value\":\"[cords]\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true},{\"name\":\"Обьекты рядом\",\"value\":\"[object]\",\"inline\":false}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Наличие лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";
        private string jsonwebhookaimdetectRU =
            "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatSneak-Логирование-[banordetect]\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatsneak.4/\",\"color\":null,\"fields\":[{\"name\":\"Оружие\",\"value\":\"[weapon]\",\"inline\":true},{\"name\":\"Расстояние\",\"value\":\"[metr]метров\",\"inline\":true},{\"name\":\"Детектов\",\"value\":\"[dCount]/[mCount]\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true},{\"name\":\"Пострадавший\",\"value\":\"[victim]\",\"inline\":true},{\"name\":\"ХитБокс\",\"value\":\"[headorbody]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Наличие лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";
        private string jsonwebhookstashdetectRU =
            "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatSneak-Логирование-[banordetect]\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatsneak.4/\",\"color\":null,\"fields\":[{\"name\":\"Координаты X Y Z\",\"value\":\"[cords]\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true},{\"name\":\"Рядом спальник\",\"value\":\"[sleepbag]\",\"inline\":true},{\"name\":\"Детектов\",\"value\":\"[dCount]/[mCount]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Наличие лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";
        private string jsonwebhooknightshotRU =
            "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatSneak-Логирование-Детект\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatsneak.4/\",\"color\":null,\"fields\":[{\"name\":\"Оружие\",\"value\":\"[weapon]\",\"inline\":true},{\"name\":\"Расстояние\",\"value\":\"[metr]метров\",\"inline\":true},{\"name\":\"Время\",\"value\":\"[time]\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true},{\"name\":\"Пострадавший\",\"value\":\"[victim]\",\"inline\":true},{\"name\":\"Фонарик/ПНВ\",\"value\":\"[flash]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Наличие лицензии: [pirate]\\n[playerInfo]\\nМогуть быть ложные детекты из за пинга 170+\\nРекомендуемо запросить результаты SpeedTest\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";
        private string jsonwebhookpilotshotRU =
            "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatSneak-Логирование-[banordetect]\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatsneak.4/\",\"color\":null,\"fields\":[{\"name\":\"Координаты X Y Z\",\"value\":\"[cords]\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Наличие лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";
        private string jsonwebhookmanipRU =
            "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatSneak-Логирование-[banordetect]\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatsneak.4/\",\"color\":null,\"fields\":[{\"name\":\"Оружие\",\"value\":\"[weapon]\",\"inline\":true},{\"name\":\"Расстояние\",\"value\":\"[metr]метров\",\"inline\":true},{\"name\":\"Детектов\",\"value\":\"[dCount]/[mCount]\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true},{\"name\":\"Пострадавший\",\"value\":\"[victim]\",\"inline\":true},{\"name\":\"Пинг\",\"value\":\"(Пока нет инфо.)\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Наличие лицензии: [pirate]\\n[playerInfo]\\nМогуть быть ложные детекты из за пинга 170+\\nРекомендуемо запросить результаты SpeedTest\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";
        private string jsonwebhookrapidRU =
            "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatSneak-Логирование-[banordetect]\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatsneak.4/\",\"color\":null,\"fields\":[{\"name\":\"Оружие\",\"value\":\"[weapon]\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Наличие лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";

        private void RequestDC(string payload, Action<int> callback = null)
        {
            Dictionary<string, string> header = new Dictionary<string, string>();
            header.Add("Content-Type", "application/json");
            
            webrequest.Enqueue(_config.Логирование.Webhook, payload, (code, response) =>
            {
                if (code != 200 && code != 204)
                {
                    if (response != null)
                    {
                        try
                        {
                            JObject json = JObject.Parse(response);
                            if (code == 429)
                            {
                                float seconds =
                                    float.Parse(Math.Ceiling((double)(int)json["retry_after"] / 1000).ToString());
                            }
                            else
                            {
                                PrintWarning(
                                    $" Discord rejected that payload! Responded with \"{json["message"].ToString()}\" Code: {code}");
                            }
                        }
                        catch
                        {
                            PrintWarning(
                                $"Failed to get a valid response from discord! Error: \"{response}\" Code: {code}");
                        }
                    }
                    else
                    {
                        PrintWarning($"Discord didn't respond (down?) Code: {code}");
                    }
                }

                try
                {
                    callback?.Invoke(code);
                }
                catch (Exception ex)
                {
                }

            }, this, RequestMethod.POST, header);
        }

        #endregion RequestDC

        void ChatDetect(BasePlayer player, string banReason)
        {
            BasePlayer adminPlayer = FindAdminWithPermissionC();
            if (adminPlayer != null)
            {
                string playerName = player.displayName; 
                BasePlayer attacker = player; 
                string message = $"Игрок {playerName} задетекчен: {banReason}";
                SendReply(adminPlayer, message);
            }
        }

        BasePlayer FindAdminWithPermissionC()
        {
            foreach (var adminPlayer in BasePlayer.activePlayerList)
            {
                if (permission.UserHasPermission(adminPlayer.UserIDString, "anticheatsneak.canseedetect.chat"))
                {
                    return adminPlayer;
                }
            }
            return null;
        }
    }
}