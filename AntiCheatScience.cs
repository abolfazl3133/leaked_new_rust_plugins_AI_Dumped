using System;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("AntiCheatScience", "https://discord.gg/TrJ7jnS233", "3.4.2")]
    [Description("Лучший анти чит для пиратского Rust | discord: pressfwd")]
    class AntiCheatScience : RustPlugin
    {
        [PluginReference] Plugin MultiFighting, Battles, AimTrain, ImageLibrary, NightVision;

        #region Configuration

        private static ConfigData _config;

        public class ConfigData
        {
            public OtherConfig Global { get; set; } = new OtherConfig();
            public AimConfig Aim { get; set; } = new AimConfig();
            public PilotFireConfig PilotFire { get; set; } = new PilotFireConfig();
            public AimLockConfig AimLock { get; set; } = new AimLockConfig();
            public CodeLockConfig CodeLock { get; set; } = new CodeLockConfig();
            public NHDetect NightFire { get; set; } = new NHDetect();
            public ManipConfig Manipulator { get; set; } = new ManipConfig();
            public FlyFire FlyFire { get; set; } = new FlyFire();
            public MeleeAttackConfig MeleeAttack { get; set; } = new MeleeAttackConfig();
            public SAimConfig SilentAim { get; set; } = new SAimConfig();
            public FlyConfig FlyHack { get; set; } = new FlyConfig();
            public NFDConfig NoFallDamage { get; set; } = new NFDConfig();
            public SpiderConfig SpiderHack { get; set; } = new SpiderConfig();
            public StashConfig Traps { get; set; } = new StashConfig();
            public SteamProxyConfig Steam { get; set; } = new SteamProxyConfig();
            public LogAndConfig Logs { get; set; } = new LogAndConfig();
            public RAConfig RustAPP { get; set; } = new RAConfig();
        }

        public class OtherConfig
        {
            [JsonProperty("Консольная команда для блокировки? %steamid% - айди игрока , %reason% - причина")]
            public string BanCommand { get; set; } = "ban %steamid% \"%reason%\"";

            [JsonProperty("Консольная команда для кика? %steamid% - айди игрока , %reason% - причина")]
            public string KickCommand { get; set; } = "kick %steamid% \"%reason%\"";

            [JsonProperty("Дискорд айди тех.администратора (как его получить?: https://www.youtube.com/watch?v=9T0KqA8akrY | нет, видео не мое)")]
            public string DiscordID { get; set; } = "неизвестный";

            [JsonProperty("Блокировать урон при детекте?")]
            public bool isAimBlocDamage { get; set; } = false;

            [JsonProperty("Вы подтверждаете что плагин настроен? (версия 3.4.1)")]
            public bool isSettingsS { get; set; } = false;
            /*[JsonProperty("Максимум информации о вычислении и детектах в консоль?")]
            public bool isDetectDebug { get; set; } = false;*/
        }

        public class AimConfig
        {
            [JsonProperty("Причина бана/кика за AIM")]
            public string NameAim { get; set; } = "AC - Протокол #1!";

            [JsonProperty("Мера наказания за AIM RustAPP (1 - Бан, 2 - Бан на всех серверах, 3 - На сервере по IP, 4 - на всех серверах по IP")]
            public int banMeraAim { get; set; } = 1;

            [JsonIgnore]
            [JsonProperty("Мера наказания за AIM?(true - Бан, false - Кик)")]
            public bool isAimBan { get; set; } = true;

            public RifleConfig НастройкаВинтовок { get; set; } = new RifleConfig();
            public PistolConfig НастройкаПистолетов { get; set; } = new PistolConfig();
            public PPConfig НастройкаПП { get; set; } = new PPConfig();
            public SniperConfig НастройкаСнайперскихВинтовок { get; set; } = new SniperConfig();
            public BowConfig НастройкаЛуков { get; set; } = new BowConfig();

            public class RifleConfig
            {
                [JsonIgnore]
                [JsonProperty("За попадание в голову на сколько метров кикать? (Снайперки уже учтены)")]
                public float metrrifle { get; set; } = 220f;


                [JsonProperty("Секунд до сброса детектов?")]
                public float isTwoRifCD { get; set; } = 5;

                [JsonIgnore]
                [JsonProperty("Сколько попаданий за N сек в голову?")]
                public int isTwoRifH { get; set; } = 3;

                [JsonIgnore]
                [JsonProperty("Сколько попаданий за N сек в тело?")]
                public int isTwoRifB { get; set; } = 9;
            }
            public class PistolConfig
            {
                [JsonIgnore]
                [JsonProperty("За попадание в голову c пистолетов на сколько метров кикать?")]
                public float metrpistol { get; set; } = 140f;

                [JsonProperty("Секунд до сброса детектов?")]
                public float isTwoPistolCD { get; set; } = 5;

                [JsonIgnore]
                [JsonProperty("Сколько попаданий за N сек в голову?")]
                public int isTwoPistolH { get; set; } = 3;

                [JsonIgnore]
                [JsonProperty("Сколько попаданий за N сек в тело?")]
                public int isTwoPistolB { get; set; } = 6;
            }
            public class PPConfig
            {
                [JsonIgnore]
                [JsonProperty("За попадание в голову с ПП?")]
                public float metrpp { get; set; } = 180f;


                [JsonProperty("Секунд до сброса детектов?")]
                public float isTwoPPCD { get; set; } = 5;

                [JsonIgnore]
                [JsonProperty("Сколько попаданий за N сек в голову?")]
                public int isTwoPPH { get; set; } = 3;

                [JsonIgnore]
                [JsonProperty("Сколько попаданий за N сек в тело?")]
                public int isTwoPPB { get; set; } = 5;
            }
            public class SniperConfig
            {

                [JsonProperty("Float")]
                public float metrsniper { get; set; } = 500f;
            }
            public class BowConfig
            {
                [JsonIgnore]
                [JsonProperty("За попадание в голову c луков на сколько метров кикать?")]
                public float metrbow { get; set; } = 75f;


                [JsonProperty("Мера наказания?(true - Бан, false - Кик)")]
                public bool isbowBan { get; set; } = true;


                [JsonProperty("Секунд до сброса детектов?")]
                public float isTwoBowCD { get; set; } = 5;

                [JsonIgnore]
                [JsonProperty("Сколько попаданий за N сек в голову?")]
                public int isTwoBowH { get; set; } = 2;

                [JsonIgnore]
                [JsonProperty("Сколько попаданий за N сек в тело?")]
                public int isTwoBowB { get; set; } = 3;
            }
        }

        public class PilotFireConfig
        {
            [JsonProperty("Причина бана/кика за стрельбу с водительского места")]
            public string NameAimDrive { get; set; } = "AC - Протокол #2!";

            [JsonProperty("Мера наказания за стрельбу с вод. места RustAPP (1 - Бан, 2 - Бан на всех серверах, 3 - На сервере по IP, 4 - на всех серверах по IP")]
            public int banMeraAimDrive { get; set; } = 1;
        }

        public class AimLockConfig
        {
            [JsonProperty("Причина бана/кика за AimLock")]
            public string NameAimLock { get; set; } = "AC - Протокол #5!";

            [JsonProperty("Мера наказания за AimLock RustAPP (1 - Бан, 2 - Бан на всех серверах, 3 - На сервере по IP, 4 - на всех серверах по IP")]
            public int banMeraAimLock { get; set; } = 1;

            [JsonProperty("Мера наказания за AimLock?(true - Бан, false - Кик)")]
            public bool isAimLockBan { get; set; } = true;
        }

        public class CodeLockConfig
        {
            [JsonProperty("Причина детекта за ввод пароля от дома забаненого игрока")]
            public string NameCodeLock { get; set; } = "AC - Протокол #6!";
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
            [JsonIgnore]
            [JsonProperty("Банить за тестовый детект манипулятора? (не мало жалоб за ложный бан, хотя видео док-ва не дают)")]
            public bool isManipT { get; set; } = true;


            [JsonProperty("Причина бана/кика за манипулятор")]
            public string NameAimM { get; set; } = "AC - Протокол #7!";

            [JsonProperty("Мера наказания за манипулятор RustAPP (1 - Бан, 2 - Бан на всех серверах, 3 - На сервере по IP, 4 - на всех серверах по IP")]
            public int banMeraAimM { get; set; } = 1;

        }

        public class FlyFire
        {
            [JsonProperty("Причина бана/кика за стрельбу в воздухе")]
            public string NameFlyFire { get; set; } = "AC - Протокол #8!";

            [JsonProperty("Мера наказания за стрельбу в воздухе RustAPP (1 - Бан, 2 - Бан на всех серверах, 3 - На сервере по IP, 4 - на всех серверах по IP")]
            public int banMeraFlyFire { get; set; } = 1;
        }

        public class MeleeAttackConfig
        {
            [JsonProperty("Причина бана/кика за MeleeAttack")]
            public string NameMeleeAttack { get; set; } = "AC - Протокол #10!";

            [JsonProperty("Мера наказания за MeleeAttack RustAPP (1 - Бан, 2 - Бан на всех серверах, 3 - На сервере по IP, 4 - на всех серверах по IP")]
            public int banMeraMeleeAttack { get; set; } = 1;

            [JsonProperty("Мера наказания за MeleeAttack?(true - Бан, false - Кик)")]
            public bool isMeleeAttackBan { get; set; } = true;
        }

        public class SAimConfig
        {
            [JsonProperty("Причина бана/кика за SilentAim")]
            public string NameSAim { get; set; } = "AC - Протокол #11!";

            [JsonProperty("Мера наказания за SilentAim RustAPP (1 - Бан, 2 - Бан на всех серверах, 3 - На сервере по IP, 4 - на всех серверах по IP")]
            public int banMeraSAim { get; set; } = 1;

            [JsonProperty("Кол-во детектов за 15 сек для бана")]
            public int maxDetect { get; set; } = 3;

            [JsonProperty("Мера наказания за SilentAim?(true - Бан, false - Кик)")]
            public bool isSAimBan { get; set; } = true;
        }

        public class FlyConfig
        {
            [JsonIgnore]
            [JsonProperty("Включить наказание за FlyHack?")]
            public bool isFly { get; set; } = true;

            [JsonIgnore]
            [JsonProperty("Мера наказания?(true - Бан, false - Кик)")]
            public bool isFlyBan { get; set; } = false;

            [JsonIgnore]
            [JsonProperty("За сколько детектов наказывать за FlyHack?")]
            public int DCount { get; set; } = 3;


            [JsonProperty("Причина бана/кика за FlyHack")]
            public string NameFly { get; set; } = "AC - Протокол #3!";
        }

        public class NFDConfig
        {
            [JsonProperty("Детектить? (BETA функционал)")]
            public bool isNFD { get; set; } = true;

            [JsonProperty("Причина бана/кика за NoFallDamage")]
            public string NameNFD { get; set; } = "AC - Протокол #12!";

            [JsonProperty("Мера наказания за NFD RustAPP (1 - Бан, 2 - Бан на всех серверах, 3 - На сервере по IP, 4 - на всех серверах по IP")]
            public int banMeraNFD { get; set; } = 1;
        }

        public class SpiderConfig
        {
            [JsonProperty("Детектить? (BETA функционал)")]
            public bool isSpider { get; set; } = true;

            [JsonProperty("Причина бана/кика за SpiderHack")]
            public string NameSpider { get; set; } = "AC - Протокол #13!";

            [JsonProperty("Мера наказания за SpiderHack RustAPP (1 - Бан, 2 - Бан на всех серверах, 3 - На сервере по IP, 4 - на всех серверах по IP")]
            public int banMeraSpider { get; set; } = 1;
        }

        public class StashConfig
        {
            [JsonProperty("Банить за откапывание чужих стешей?")]
            public bool isStash { get; set; } = true;

            [JsonProperty("Банить за откапывание N стешей")]
            public int stashBCount { get; set; } = 3;

            [JsonProperty("Причина бана/кика за откапывание чужих стешей")]
            public string NameStash { get; set; } = "AC - Протокол #4!";

            [JsonProperty("Мера наказания за стеши RustAPP (1 - Бан, 2 - Бан на всех серверах, 3 - На сервере по IP, 4 - на всех серверах по IP")]
            public int banMeraStash { get; set; } = 1;
        }

        public class SteamProxyConfig
        {
            [JsonProperty("Проверять игроков на Proxy через ip2location?")]
            public bool PROXY_CHECT { get; set; } = false;

            [JsonProperty("API Ключ для проверок IP игроков (https://www.ip2location.io/)")]
            public string YOUR_API_KEY { get; set; } = "000000";

            [JsonProperty("Проверять дату регистрации через steam?")]
            public bool STEAM_CHECT { get; set; } = false;

            [JsonProperty("API Ключ (ОБЯЗАТЕЛЬНО!!) https://steamcommunity.com/dev/apikey")]
            public string SteamAPI { get; set; } = "123123";

            [JsonProperty("Не пускать аккаунт если он создан мение N дней назад?")]
            public int SteamDays { get; set; } = 5;

            [JsonProperty("Пермишенс с которым можно зайти с молодого аккаунта")]
            public string SteamDaysIgnore { get; set; } = "ignoremolodoiacc";
        }

        public class LogAndConfig
        {
            [JsonProperty("Ссылка на вебхук Discord")]
            public string Webhook { get; set; } = "1";

            [JsonProperty("Картинка в логировании (версия стандартная)")]
            public string WebhookImage { get; set; } = "https://media.giphy.com/media/V4pAZ7bxf1rOFoLCWS/giphy.gif";

            /*[JsonProperty("Версия логирования? (true - стандартная | false - сразу и по делу(By MEGARGAN(не работает, я не успел доделать)))")]
            public bool LogVersion { get; set; } = true;*/
            /*[JsonProperty("Использовать дополнительный блок логирования?")]
            public bool steamlog { get; set; } = true;*/
        }

        public class RAConfig
        {
            [JsonProperty("Работать с RustAPP?")]
            public bool isRustAPP { get; set; } = false;

            [JsonProperty("Айди, репорт на которых отправить нельзя 765000000000000, 76500000000000001")]
            public List<string> idAntiReport { get; set; } = new List<string>();

            [JsonProperty("Банить автоматически если игрок получил N кол-во репортов?")]
            public bool isRustAPPbanAuto { get; set; } = false;

            [JsonProperty("Сколько репортов нужно получить для автоматического бана?")]
            public int banAutoInt { get; set; } = 5;

            [JsonProperty("Мера наказания за макс. репортов RustAPP (1 - Бан, 2 - Бан на всех серверах, 3 - На сервере по IP, 4 - на всех серверах по IP")]
            public int banMeraMR { get; set; } = 1;

            [JsonProperty("Банить только в RustAPP?")]
            public bool isRustAPPbanOnly { get; set; } = false;
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
                Debug.LogError("[AntiCheatScience] ОШИБКА КОНФИГУРАЦИИ, ЗАГРУЗКА КОНФИГУРАЦИИ ПО УМОЛЧАНИЮ");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        List<string> triggerWord = new List<string> {
            "bay", "pirate", "sliv", "fixed", "fix", "lick", "слив", "sliv", "слитые", "rustbay", "rustpirate", "skuli"
        };

        void OnPluginLoaded(Plugin plugin)
        {
            foreach (var pluginSliv in plugins.GetAll())
            {
                if (pluginSliv.Author != null && pluginSliv.Author.ToLower().Contains(triggerWord.ToString()) || pluginSliv.Description != null && pluginSliv.Description.ToLower().Contains(triggerWord.ToString()))
                {
                    Debug.LogError("ФУ, МУСОРЩИК, УДАЛЯЙ СЛИТЫЕ ПЛАГИНЫ!!!");
                    Debug.LogError("ФУ, МУСОРЩИК, УДАЛЯЙ СЛИТЫЕ ПЛАГИНЫ!!");
                    Debug.LogError("ФУ, МУСОРЩИК, УДАЛЯЙ СЛИТЫЕ ПЛАГИНЫ!");
                    Debug.LogError("ФУ, МУСОРЩИК, УДАЛЯЙ СЛИТЫЕ ПЛАГИНЫ");
                    timer.Once(5f, () =>
                    {
                        Interface.Oxide.RootPluginManager.RemovePlugin(pluginSliv);
                        rust.RunServerCommand("quit");
                    });
                }
            }
        }

        private int MeleeDetect = 0;

        object OnMeleeAttack(BasePlayer player, HitInfo info)
        {
            if (info == null || info.HitEntity == null)
                return null;

            var hitPlayer = info.HitEntity as BasePlayer;
            if (hitPlayer == null || BasePlayer.sleepingPlayerList.Contains(hitPlayer))
                return null;

            var parentEntity = player.GetParentEntity();
            if (parentEntity is CargoShip || parentEntity is BaseBoat || parentEntity is Horse || parentEntity is RidableHorse || parentEntity is HorseCorpse || parentEntity is Tugboat || parentEntity is ScrapTransportHelicopter || parentEntity is Minicopter || parentEntity is MiningQuarry)
                return null;

            if (hitPlayer.GetMountedVehicle() || player.GetMountedVehicle())
                return null;
            
            if (MultiFighting != null)
            {
                if ((bool)MultiFighting.CallHook("IsSteam", player.Connection))
                    return null;
            }

            if (Battles != null)
            {
                if ((bool)Battles.CallHook("IsPlayerOnBattle", player.userID))
                    return null;
            }

            if (AimTrain != null)
            {
                if ((bool)AimTrain.CallHook("IsAimTraining", player.userID))
                    return null;
            }

            float distance = Vector3.Distance(player.transform.position, hitPlayer.transform.position);
            if (distance <= 0.9588763f)
                return null;

            if (info.HitEntity.IsNpc)
                return null;

            float num = 1f + ConVar.AntiHack.melee_forgiveness;
            float melee_clientframes = ConVar.AntiHack.melee_clientframes;
            float melee_serverframes = ConVar.AntiHack.melee_serverframes;
            float num2 = (player.desyncTimeClamped + melee_clientframes / 60f + melee_serverframes * Mathx.Max(UnityEngine.Time.deltaTime, UnityEngine.Time.smoothDeltaTime, UnityEngine.Time.fixedDeltaTime)) * num;

            Vector3 startPos = player.eyes.position;
            BaseMelee weapon = info.WeaponPrefab as BaseMelee;
            float maxDistance = weapon.maxDistance + weapon.attackRadius;
            float maxRadius = weapon.attackRadius + num2 * 0.5f;
            float maxHitDistance = maxDistance + num2 * 0.5f;

            RaycastHit hit;
            bool inRadius = Physics.SphereCast(startPos, maxRadius + 0.3f, player.eyes.BodyForward(), out hit, maxHitDistance, 2048 | 131072 | 1218519297, QueryTriggerInteraction.Ignore) && hit.collider.name.Contains("player");
            string weaponM = info.Weapon?.GetItem()?.info.shortname;
            if (weaponM.Contains("jackhammer") || weaponM.Contains("chainsaw") || weaponM.Contains("flash") || weaponM.Contains("torch"))
                return null;

            if (!inRadius)
            {
                player.stats.combat.LogInvalid(info, "Урон заблокирован АнтиЧитом");
                MeleeDetect++;
                DetectNo10(player, MeleeDetect, false);
                if (MeleeDetect == 1)
                {
                    timer.Once(30f, () => MeleeDetect = 0);
                }
                if (MeleeDetect == 3)
                {
                    DetectNo10(player, MeleeDetect, true);
                    if (_config.MeleeAttack.isMeleeAttackBan)
                    {
                        if (_config.RustAPP.isRustAPP)
                        {
                            if (_config.MeleeAttack.banMeraMeleeAttack == 1)
                            {
                                rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.MeleeAttack.NameMeleeAttack} [AntiCheatScience]\"");
                            }
                            else if (_config.MeleeAttack.banMeraMeleeAttack == 2)
                            {
                                rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.MeleeAttack.NameMeleeAttack} [AntiCheatScience]\" --global");
                            }
                            else if (_config.MeleeAttack.banMeraMeleeAttack == 3)
                            {
                                rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.MeleeAttack.NameMeleeAttack} [AntiCheatScience]\" --ban-ip");
                            }
                            else if (_config.MeleeAttack.banMeraMeleeAttack == 4)
                            {
                                rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.MeleeAttack.NameMeleeAttack} [AntiCheatScience]\" --ban-ip --global");
                            }
                        }
                        if (!_config.RustAPP.isRustAPPbanOnly)
                        {
                            string commanda = _config.Global.BanCommand.Replace("%steamid%", player.userID.ToString()).Replace("%reason%", $"{_config.MeleeAttack.NameMeleeAttack} [AntiCheatScience]");
                            rust.RunServerCommand($"{commanda}");
                        }
                    }
                    else
                    {
                        string commanda = _config.Global.KickCommand.Replace("%steamid%", player.userID.ToString()).Replace("%reason%", $"{_config.MeleeAttack.NameMeleeAttack} [AntiCheatScience]");
                        rust.RunServerCommand($"{commanda}");
                    }
                }
                if (_config.Global.isAimBlocDamage)
                {
                    return true;
                }
                return null;
            }
            return null;
        }

        /*private const string DummyPrefab = "assets/prefabs/player/player.prefab";
        private const float DummyDuration = 1f;
        private const float SpawnProbability = 0.15f;
        private BaseEntity dummyPlayer;
        private Vector3 spawnPosition;

        private void SpawnDummyPlayer(BasePlayer suspectPlayer)
        {
            if (dummyPlayer != null && !dummyPlayer.IsDestroyed)
            {
                dummyPlayer.Kill();
            }

            Vector3 forward = suspectPlayer.eyes.HeadForward();
            Vector3 left = Vector3.Cross(forward, Vector3.up).normalized;

            float forwardDistance = UnityEngine.Random.Range(5f, 6f); 
            float leftOffset = 0.7f; 

            spawnPosition = suspectPlayer.eyes.position + forward * forwardDistance + left * leftOffset;

            dummyPlayer = GameManager.server.CreateEntity(DummyPrefab, spawnPosition, suspectPlayer.eyes.rotation);
            if (dummyPlayer == null) return;

            dummyPlayer.Spawn();

            dummyPlayer.gameObject.AddComponent<DummyHitDetector>().Initialize(this, suspectPlayer);

            timer.Once(DummyDuration, () =>
            {
                if (dummyPlayer != null && !dummyPlayer.IsDestroyed)
                {
                    dummyPlayer.Kill();
                    dummyPlayer = null;
                }
            });
        }

        private class DummyHitDetector : MonoBehaviour
        {
            private AntiCheatScience plugin;
            private BasePlayer suspect;
            private int DummyDetect = 0;

            public void Initialize(AntiCheatScience plugin, BasePlayer suspect)
            {
                this.plugin = plugin;
                this.suspect = suspect;
            }

            private void OnCollisionEnter(Collision collision)
            {
                var hitPlayer = collision.gameObject.GetComponent<BasePlayer>();
                if (hitPlayer != null && hitPlayer == suspect)
                {
                    DummyDetect++;

                }
            }
        }*/

        protected override void LoadDefaultConfig() => _config = new ConfigData();
        protected override void SaveConfig() => Config.WriteObject(_config, true);

        Timer spiderTimer;
        private Dictionary<ulong, bool> playerReportsIQ = new Dictionary<ulong, bool>();
        private Dictionary<ulong, int> spiderDetectC = new Dictionary<ulong, int>();
        private string _Layer1 = "SpecMenu";
        void SpecMenu(BasePlayer adminPlayer, BasePlayer player)
        {
            CuiHelper.DestroyUi(adminPlayer, _Layer1);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel 
            {
                CursorEnabled = false,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.9 0.5", AnchorMax = "0.9 0.5", OffsetMin = "-400 -250", OffsetMax = "100 -50" }
            }, "Overlay", _Layer1);

            container.Add(new CuiElement
            {
                Parent = _Layer1,
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = (string) ImageLibrary.Call("GetImage","bg")
                    }
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.255 0.76", AnchorMax = "1 0.76", OffsetMin = "0 0", OffsetMax = "0 0" },
                Text = { Text = $"{player.displayName}", Align = TextAnchor.MiddleLeft, FontSize = 18, VerticalOverflow = VerticalWrapMode.Overflow }
            }, _Layer1);

            /*container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.17 0.65", AnchorMax = "1 0.65", OffsetMin = "0 0", OffsetMax = "0 0" },
                Text = { Text = $"{banReason}", Align = TextAnchor.MiddleLeft, FontSize = 18, VerticalOverflow = VerticalWrapMode.Overflow }
            }, _Layer1);*/

            container.Add(new CuiElement
            {
                Parent = _Layer1,
                Name = "ban",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = (string) ImageLibrary.Call("GetImage","ban")
                    },
                    new CuiRectTransformComponent{AnchorMin = "0.2 0.2",AnchorMax = "0.2 0.2",OffsetMin = "-60 -20",OffsetMax = "60 20"}
                }
            });
            
            container.Add(new CuiButton
            {
                Button = { Command = $"cmd.sosiban {player.UserIDString} {adminPlayer.UserIDString} \"Результат слежки\"", Color = "0 0 0 0"},
                Text = { Text = ""},
                RectTransform = { AnchorMin = "0.1 0.1",AnchorMax = "0.9 0.9"}
            }, "ban");

            container.Add(new CuiElement
            {
                Parent = _Layer1,
                Name = "stop",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = (string) ImageLibrary.Call("GetImage","stop")
                    },
                    new CuiRectTransformComponent{AnchorMin = "0.8 0.2", AnchorMax = "0.8 0.2", OffsetMin = "-60 -20", OffsetMax = "60 20"}
                }
            });
            
            container.Add(new CuiButton
            {
                Button = { Command = "cmd.acspec stop", Color = "0 0 0 0"},
                Text = { Text = ""},
                RectTransform = { AnchorMin = "0.1 0.1",AnchorMax = "0.9 0.9"}
            }, "stop");

            /*container.Add(new CuiElement
            {
                Parent = _Layer1,
                Name = "HelpBtn",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = (string) ImageLibrary.Call("GetImage","helpbtn")
                    },
                    new CuiRectTransformComponent{AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = "5 245",OffsetMax = "145 325"}
                }
            });

            container.Add(new CuiButton
            {
                Button = { Command = "smhelpmenu", Color = "0 0 0 0"},
                Text = { Text = ""},
                RectTransform = { AnchorMin = "0.1 0.1",AnchorMax = "0.9 0.9"}
            }, "HelpBtn"); */

            /*container.Add(new CuiElement
            {
                Parent = _Layer1,
                Name = "ReportBtn",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = (string) ImageLibrary.Call("GetImage","reportbtn")
                    },
                    new CuiRectTransformComponent{AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = "5 170",OffsetMax = "145 250"}
                }
            });
            container.Add(new CuiButton
            {
                Button = { Command = "chat.say /report", Color = "0 0 0 0"},
                Text = { Text = ""},
                RectTransform = { AnchorMin = "0.1 0.1",AnchorMax = "0.9 0.9"}
            }, "ReportBtn");*/

            /*container.Add(new CuiElement
            {
                Parent = _Layer1,
                Name = "ChatBtn",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = (string) ImageLibrary.Call("GetImage","chatbtn")
                    },
                    new CuiRectTransformComponent{AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = "5 95",OffsetMax = "145 175"}
                }
            });

            container.Add(new CuiButton
            {
                Button = { Command = "chat.say /chat", Color = "0 0 0 0"},
                Text = { Text = ""},
                RectTransform = { AnchorMin = "0.1 0.1",AnchorMax = "0.9 0.9"}
            }, "ChatBtn");*/

            /*container.Add(new CuiElement
            {
                Parent = _Layer1,
                Name = "MenuBtn1",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = (string) ImageLibrary.Call("GetImage","menubtn")
                    },
                    new CuiRectTransformComponent{AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = "40 20",OffsetMax = "110 45"}
                }
            });
            container.Add(new CuiButton
            {
                Button = { Command = "smfirstmenu", Color = "0 0 0 0"},
                Text = { Text = ""},
                RectTransform = { AnchorMin = "0.1 0.1",AnchorMax = "0.9 0.9"}
            }, "MenuBtn1");*/
            
            CuiHelper.AddUi(adminPlayer, container);
        }
        void OnSendedReport(BasePlayer Sender, UInt64 TargetID, String Reason)
        {
            BasePlayer victim = BasePlayer.FindByID(TargetID);
            if (victim != null || victim != null && victim.IsAdmin)
               return;
            SeeSpider(victim);
        }
        void OnStartedChecked(BasePlayer Target, BasePlayer Moderator, Boolean IsConsole = false)
        {
            if (playerReportsIQ.ContainsKey(Target.userID))
            {
                playerReportsIQ.Remove(Target.userID);
            }
        }
        void RustApp_OnCheckNoticeShowed(BasePlayer player)
        {
            if (playerReportsIQ.ContainsKey(player.userID))
            {
                playerReportsIQ.Remove(player.userID);
            }
        }
        object OnConstructionPlace(BaseEntity entity, Construction component, Construction.Target constructionTarget, BasePlayer player)
        {  
            if (playerReportsIQ.ContainsKey(player.userID))
            {
                if (playerReportsIQ[player.userID])
                {
                    if (spiderDetectC.ContainsKey(player.userID))
                    {
                        if (spiderDetectC[player.userID] != 0)
                        {
                            if (entity.ToString().Contains("floor"))
                            {
                                return "ты кого хочешь ноебать?";
                            }
                        }
                    }
                }
            }
            return null;
        }
        void SeeSpider(BasePlayer player)
        {
            if (!playerReportsIQ.ContainsKey(player.userID))
            {
                playerReportsIQ.Add(player.userID, true);
            }

            if (_config.SpiderHack.isSpider)
            {
                Vector3 oldPos = player.transform.position;
                spiderTimer = timer.Every(0.5f, () =>
                {
                    if (playerReportsIQ[player.userID])
                    {
                        /*if (!player.IsOnGround() && /*player.IsInWaterVolume || player.WaterFactor ||*/ /*player.IsRunning())
                        {
                            Puts("ебанат");
                        }*/
                        string nearbyObjects = GetNearbyObjects(player.transform.position, 1.5f);
                        if (HasForbiddenObjects(nearbyObjects))
                        {
                            return;
                        }
                        if (player.GetMountedVehicle())
                        {
                            if (!spiderTimer.Destroyed)
                                spiderTimer.Destroy();
                            timer.Once(60f, () =>
                            {
                                SeeSpider(player);
                            });
                        }
                        if (playerReportsIQ[player.userID])
                        {
                            float cumulativeYIncrease = 0f;
                            Vector3 newPos = player.transform.position;
                            float deltaX = Mathf.Abs(newPos.x - oldPos.x);
                            float deltaY = newPos.y - oldPos.y;
                            float deltaZ = Mathf.Abs(newPos.z - oldPos.z);

                            if (deltaY > 0)
                            {
                                cumulativeYIncrease += deltaY;
                            }

                            oldPos = newPos;

                            if (cumulativeYIncrease >= 0.7f && deltaX <= 0.7f && deltaZ <= 0.7f)
                            {
                                if (!spiderDetectC.ContainsKey(player.userID))
                                {
                                    spiderDetectC.Add(player.userID, 0);
                                }
                                spiderDetectC[player.userID]++;
                                cumulativeYIncrease = 0f; 

                                if (spiderDetectC[player.userID] >= 3)
                                {
                                    DetectNo13(player);
                                    playerReportsIQ[player.userID] = false;
                                    if (!spiderTimer.Destroyed)
                                        spiderTimer.Destroy();

                                    if (_config.RustAPP.isRustAPP)
                                    {
                                        if (_config.SpiderHack.banMeraSpider == 1)
                                        {
                                            rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.SpiderHack.NameSpider} [AntiCheatScience]\"");
                                        }
                                        else if (_config.SpiderHack.banMeraSpider == 2)
                                        {
                                            rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.SpiderHack.NameSpider} [AntiCheatScience]\" --global");
                                        }
                                        else if (_config.SpiderHack.banMeraSpider == 3)
                                        {
                                            rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.SpiderHack.NameSpider} [AntiCheatScience]\" --ban-ip");
                                        }
                                        else if (_config.SpiderHack.banMeraSpider == 4)
                                        {
                                            rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.SpiderHack.NameSpider} [AntiCheatScience]\" --ban-ip --global");
                                        }
                                    }
                                    if (!_config.RustAPP.isRustAPPbanOnly)
                                    {
                                        string commanda = _config.Global.BanCommand.Replace("%steamid%", player.userID.ToString()).Replace("%reason%", _config.SpiderHack.NameSpider);
                                        rust.RunServerCommand($"{commanda}");
                                    }
                                    spiderDetectC[player.userID] = 0;
                                }
                            }
                            else
                            {
                                if (deltaY <= 0)
                                {
                                    cumulativeYIncrease = 0f;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (!spiderTimer.Destroyed)
                            spiderTimer.Destroy();
                    }
                });
            }
        }

        private Dictionary<string, int> playerReportsRA = new Dictionary<string, int>();
        object RustApp_CanIgnoreReport(string target_steam_id, string initiator_steam_id)
        {
            if (_config.RustAPP.idAntiReport.Contains(target_steam_id))
                return false;
            if (UInt64.TryParse(target_steam_id, out ulong numValue))
            {
                BasePlayer victim = BasePlayer.FindByID(numValue);
                SeeSpider(victim);
            }
            if (_config.RustAPP.isRustAPPbanAuto)
            {
                if (playerReportsRA.ContainsKey(target_steam_id))
                {
                    playerReportsRA[target_steam_id]++;
                }
                else
                {
                    playerReportsRA.Add(target_steam_id, 1);
                }
                if (playerReportsRA[target_steam_id] == _config.RustAPP.banAutoInt)
                {
                    if (_config.RustAPP.isRustAPP)
                    {
                        if (_config.RustAPP.banMeraMR == 1)
                        {
                            rust.RunServerCommand($"ra.ban {target_steam_id} \"Привысил макс. кол-во репортов [AntiCheatScience]\"");
                        }
                        else if (_config.RustAPP.banMeraMR == 2)
                        {
                            rust.RunServerCommand($"ra.ban {target_steam_id} \"Привысил макс. кол-во репортов [AntiCheatScience]\" --global");
                        }
                        else if (_config.RustAPP.banMeraMR == 3)
                        {
                            rust.RunServerCommand($"ra.ban {target_steam_id} \"Привысил макс. кол-во репортов [AntiCheatScience]\" --ban-ip");
                        }
                        else if (_config.RustAPP.banMeraMR == 4)
                        {
                            rust.RunServerCommand($"ra.ban {target_steam_id} \"Привысил макс. кол-во репортов [AntiCheatScience]\" --ban-ip --global");
                        }
                    }
                    if (!_config.RustAPP.isRustAPPbanOnly)
                    {
                        string commanda = _config.Global.BanCommand.Replace("%steamid%", target_steam_id).Replace("%reason%", "Привысил макс. кол-во репортов [AntiCheatScience]");
                        rust.RunServerCommand($"{commanda}");
                    }
                }
            }
            return null;
        }

        Dictionary<ulong, float> Land = new Dictionary<ulong, float>();

        void OnPlayerLanded(BasePlayer player, float num)
        {
            if (!_config.NoFallDamage.isNFD)
                return;
            NextTick( () =>
            {
                if (!(Math.Abs(player.Health() - Math.Abs(Land[player.userID])) > 5))
                {
                    NoFallDamage(player);
                }
            });
        }

        object OnPlayerLand(BasePlayer player, float num)
        {
            if (!_config.NoFallDamage.isNFD)
                return null;
            if (!Land.ContainsKey(player.userID))
            {
                Land.Add(player.userID, player.Health());
            }
            else
            {
                Land[player.userID] = player.Health();
            }
            return null;
        }

        Dictionary<ulong, int> JA = new Dictionary<ulong, int>();
        Dictionary<ulong, int> JAReserve = new Dictionary<ulong, int>();
        Dictionary<ulong, int> JA_detect = new Dictionary<ulong, int>();
        Dictionary<ulong, int> JAReserve_detect = new Dictionary<ulong, int>();
        Dictionary<ulong, bool> cdJ = new Dictionary<ulong, bool>();
        private Dictionary<ulong, List<JumpInfo>> playerJumps = new Dictionary<ulong, List<JumpInfo>>();
        private class JumpInfo
        {
            public float Time;
            public Vector3 Position;

            public JumpInfo(float time, Vector3 position)
            {
                Time = time;
                Position = position;
            }
        }
        Timer checktimerJ;

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (input.WasJustPressed(BUTTON.JUMP))
            {
                if (player.Connection.authLevel > 0 || player.IsBuildingAuthed())
                    return;

                if (cdJ.ContainsKey(player.userID))
                {
                    if (cdJ[player.userID])
                        return;
                }
                if (!JA.ContainsKey(player.userID))
                {
                    JA.Add(player.userID, 0);
                }
                JA[player.userID]++;
                timer.Once(0.35f, () =>
                {
                    JA[player.userID] = 0;
                    /*if (!JAReserve.ContainsKey(player.userID))
                    {
                        JAReserve.Add(player.userID, 0);
                    }
                    JAReserve[player.userID]++;
                    timer.Once(0.05f, () =>
                    {
                        JAReserve[player.userID] = 0;
                    });*/
                });
                if (!cdJ.ContainsKey(player.userID))
                {
                    cdJ.Add(player.userID, false);
                }
                cdJ[player.userID] = true;
                timer.Once(1f, () =>
                {
                    checktimerJ = timer.Every(0.1f, () =>
                    {
                        if (player.IsOnGround())
                        {
                            cdJ[player.userID] = false;
                            checktimerJ.Destroy();
                        }
                    });
                });
            }
        }
        void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod, ProtoBuf.ProjectileShoot projectiles)
        {
            if (player.Connection.authLevel > 0)
                return;
            if (player.GetMountedVehicle() != null)
            {
                if (player.GetMountedVehicle().IsDriver(player))
                {
                    string vehicle = player.GetMountedVehicle().ToString();
                    if (vehicle.Contains("testridablehorse"))
                        return;
                    PilotFire(player);
                    return;
                }
            }
            bool ladder = false;
            if (player.GetParentEntity() is BaseLadder)
            {
                ladder = true;
            }
            Item item = projectile?.GetItem();
            if (item != null && weaponbows.Contains(item.info.shortname) || item.info.shortname.Contains("bow.compound"))
                return;
            Collider[] colliders = Physics.OverlapSphere(player.transform.position, 2f);
            Dictionary<string, int> objectCounts = new Dictionary<string, int>();

            foreach (Collider collider in colliders)
            {
                string objectName = collider.gameObject.name.ToLower();
                if (objectName == "lootbarrel")
                    return;
            }
            if (!JA.ContainsKey(player.userID))
            {
                JA.Add(player.userID, 0);
            }
            /*if (!JAReserve.ContainsKey(player.userID))
            {
                JAReserve.Add(player.userID, 0);
            }*/
            if (!JA_detect.ContainsKey(player.userID))
            {
                JA_detect.Add(player.userID, 0);
            }
            /*if (!JAReserve_detect.ContainsKey(player.userID))
            {
                JAReserve_detect.Add(player.userID, 0);
            }*/
            if (JA[player.userID] > 0)
            {
                JA_detect[player.userID]++;
                if (JA_detect[player.userID] == 1)
                {
                    timer.Once(7f, () =>
                    {
                        JA_detect[player.userID] = 0;
                    });
                }
            }
            /*if (JAReserve[player.userID] > 0 && !player.IsOnGround())
            {
                JAReserve_detect[player.userID]++;
                if (JAReserve_detect[player.userID] == 1)
                {
                    timer.Once(7f, () =>
                    {
                        JAReserve_detect[player.userID] = 0;
                    });
                }
            }*/
            if (/*JAReserve_detect[player.userID] >= 1 && */JA_detect[player.userID] > 1 || ladder == true)
            {
                DetectNo8(player, item.info.shortname);
                if (_config.RustAPP.isRustAPP)
                {
                    if (_config.FlyFire.banMeraFlyFire == 1)
                    {
                        rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.FlyFire.NameFlyFire} [AntiCheatScience]\"");
                    }
                    else if (_config.FlyFire.banMeraFlyFire == 2)
                    {
                        rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.FlyFire.NameFlyFire} [AntiCheatScience]\" --global");
                    }
                    else if (_config.FlyFire.banMeraFlyFire == 3)
                    {
                        rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.FlyFire.NameFlyFire} [AntiCheatScience]\" --ban-ip");
                    }
                    else if (_config.FlyFire.banMeraFlyFire == 4)
                    {
                        rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.FlyFire.NameFlyFire} [AntiCheatScience]\" --ban-ip --global");
                    }
                }
                if (!_config.RustAPP.isRustAPPbanOnly)
                {
                    string commanda = _config.Global.BanCommand.Replace("%steamid%", player.userID.ToString()).Replace("%reason%", _config.FlyFire.NameFlyFire);
                    rust.RunServerCommand($"{commanda}");
                }
            }
            /*else if (JAReserve_detect[player.userID] >= 2)
            {
                DetectNo8(player, item.info.shortname);
                if (_config.RustAPP.isRustAPP)
                {
                    if (_config.FlyFire.banMeraFlyFire == 1)
                    {
                        rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.FlyFire.NameFlyFire} [AntiCheatScience]\"");
                    }
                    else if (_config.FlyFire.banMeraFlyFire == 2)
                    {
                        rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.FlyFire.NameFlyFire} [AntiCheatScience]\" --global");
                    }
                    else if (_config.FlyFire.banMeraFlyFire == 3)
                    {
                        rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.FlyFire.NameFlyFire} [AntiCheatScience]\" --ban-ip");
                    }
                    else if (_config.FlyFire.banMeraFlyFire == 4)
                    {
                        rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.FlyFire.NameFlyFire} [AntiCheatScience]\" --ban-ip --global");
                    }
                }
                if (!_config.RustAPP.isRustAPPbanOnly)
                {
                    string commanda = _config.Global.BanCommand.Replace("%steamid%", player.userID.ToString()).Replace("%reason%", _config.FlyFire.NameFlyFire);
                    rust.RunServerCommand($"{commanda}");
                }
            }
            else if (JA_detect[player.userID] >= 2)
            {
                DetectNo8(player, item.info.shortname);
                if (_config.RustAPP.isRustAPP)
                {
                    if (_config.FlyFire.banMeraFlyFire == 1)
                    {
                        rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.FlyFire.NameFlyFire} [AntiCheatScience]\"");
                    }
                    else if (_config.FlyFire.banMeraFlyFire == 2)
                    {
                        rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.FlyFire.NameFlyFire} [AntiCheatScience]\" --global");
                    }
                    else if (_config.FlyFire.banMeraFlyFire == 3)
                    {
                        rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.FlyFire.NameFlyFire} [AntiCheatScience]\" --ban-ip");
                    }
                    else if (_config.FlyFire.banMeraFlyFire == 4)
                    {
                        rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.FlyFire.NameFlyFire} [AntiCheatScience]\" --ban-ip --global");
                    }
                }
                if (!_config.RustAPP.isRustAPPbanOnly)
                {
                    string commanda = _config.Global.BanCommand.Replace("%steamid%", player.userID.ToString()).Replace("%reason%", _config.FlyFire.NameFlyFire);
                    rust.RunServerCommand($"{commanda}");
                }
            }*/
        }

        [ConsoleCommand("shadowban")]
        private void anticheatShadowReportCommand(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                SendReply(arg, "У вас нет прав для использования этой команды.");
                return;
            }

            SendReply(arg, "Команда временно не работает. Обращайтесь лично к разработчику в дискорд: pressfwd");
        }

        private bool bugReportCooldown = false;
        [ConsoleCommand("anticheatbug")]
        private void anticheatbugReportCommand(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                SendReply(arg, "У вас нет прав для использования этой команды.");
                return;
            }
            if (arg.Args == null || arg.Args.Length < 1)
            {
                SendReply(arg, "Использование: <anticheatbug> \"описание проблемы\"");
                return;
            }

            if (bugReportCooldown)
            {
                SendReply(arg, "Вы уже отправляли БагРепорт или плагин только загрузился. Попробуйте позже. Обычно КД на отправку составляет от 30 до 120 минут.");
                return;
            }

            string identifier = arg.Args[0];
            string sN = ConVar.Server.hostname;

            RequestBugReport(bugreport.Replace("[sN]", $"{sN}")
                .Replace("[dannie]", $"{identifier}")
                .Replace("[id]", $"{_config.Global.DiscordID}"));

            SendReply(arg, "Ваш запрос отправлен! Помните! Злоупотребление не доведет до хорошего!");
            bugReportCooldown = true;
            timer.Once(7200f, () =>
            {
                bugReportCooldown = false;
            });
        }

        [ChatCommand("fdcheck")]
        private void falldamagecheckcommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.userID.ToString(), "AntiCheatScience.can.NFDcheck"))
                return;
            if (args.Length < 1)
            {
                player.ChatMessage("Используйте: </fdcheck \"НикНейм\">");
                return;
            }

            string displayName = args[0];
            BasePlayer targetPlayer = FindPlayerByDisplayName(displayName);
            if (targetPlayer == null)
            {
                SendReply(player, "Игрок не найден.");
                return;
            }

            float originalHealth = targetPlayer.health;
            if (originalHealth >= 30 && targetPlayer.TimeAlive() > 10 && targetPlayer.IsOnGround())
            {
                Vector3 newPosition = player.transform.position + new Vector3(0, 7, 0);
                player.Teleport(newPosition);

                timer.Once(1f, () =>
                {
                    if (Mathf.Approximately(targetPlayer.health, originalHealth))
                    {
                        NoFallDamage(targetPlayer);
                    }
                    else
                    {
                        targetPlayer.health = originalHealth;
                        SendReply(targetPlayer, "Вы были подвержены проверке на NoFallDamage и <color=green>успешно</color> её прошли! не волнуйтесь! здоровье восстановлено.");
                    }
                });
            }
            else
            {
                SendReply(player, "Что то пошло не так! сейчас нельзя проверить этого игрока! попробуйте позже");
            }
        }

        private BasePlayer FindPlayerByDisplayName(string displayName)
        {
            BasePlayer targetPlayer = null;

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player.displayName.ToLower().Contains(displayName.ToLower()))
                {
                    targetPlayer = player;
                    break;
                }
            }

            return targetPlayer;
        }

        bool steam = true;
        private void OnServerInitialized()
        {
            ImageLibrary.Call("AddImage", "https://gspics.org/images/2024/05/27/0qOYpy.png","bg");
            ImageLibrary.Call("AddImage", "https://gspics.org/images/2024/05/27/0qOopE.png","ban");
            ImageLibrary.Call("AddImage", "https://gspics.org/images/2024/05/27/0qOy0j.png","stop");
            ImageLibrary.Call("AddImage", "https://gspics.org/images/2024/05/27/0qO7PJ.png","freeze");
            ImageLibrary.Call("AddImage", "https://gspics.org/images/2024/05/27/0qO5Ye.png","nfdc");
            ImageLibrary.Call("AddImage", "https://gspics.org/images/2024/05/27/0qOlJX.png","unfreeze");
            permission.RegisterPermission(_config.Steam.SteamDaysIgnore, this);
            permission.RegisterPermission("AntiCheatScience.can.seedetect", this);
            permission.RegisterPermission("AntiCheatScience.can.NFDcheck", this);
            permission.RegisterPermission("AntiCheatScience.can.spectate", this);
            ConVar.AntiHack.eye_protection = 5;
            if (_config.Steam.SteamAPI == "123123")
            {
                Debug.LogError("[AntiCheatScience] Вы не настроили обязательный параметр!! вставьте SteamAPI ключ!!");
                Interface.Oxide.UnloadPlugin(Title);
            }
            else
            {
                Debug.LogWarning("[AntiCheatScience] SteamAPI настроен.");
            }

            if (_config.Logs.Webhook == "1")
            {
                Debug.LogError("[AntiCheatScience] Вы не настроили обязательный параметр!! вставьте discord webhook!!");
                Interface.Oxide.UnloadPlugin(Title);
            }
            else
            {
                Debug.LogWarning("[AntiCheatScience] discord webhook настроен.");
            }

            if (_config.Global.DiscordID == "неизвестный")
            {
                Debug.LogError("[AntiCheatScience] Вы не настроили обязательный параметр!! вставьте discord id пользователя!!");
                Interface.Oxide.UnloadPlugin(Title);
            }
            else
            {
                Debug.LogWarning("[AntiCheatScience] discord id настроен. ");
            }

            if (!_config.Global.isSettingsS)
            {
                Debug.LogError("[AntiCheatScience] Вы не настроили обязательный параметр!! Дайте подтверждение в разделе \"общее\"!!");
                Interface.Oxide.UnloadPlugin(Title);
            }

            Debug.LogWarning("[AntiCheatScience] Автоматизированая версия, для баг репорта введите: <anticheatbug \"описание проблемы\">");
            Debug.LogWarning("[AntiCheatScience] Для полной проверки игрока и анализации проблемы введите <shadowban \"steamid\">, вся статистика игрока уйдет разработчику плагина, далее игрок будет вызван на проверку через базу данных");
            Debug.LogWarning("[AntiCheatScience] Не злоупотребляйте!!! за флуд Ваш сервер будет занесен в ЧС, Ваши заявки будут игнорироваться, либо плагин перестанет работать на Вашем сервере");
            bugReportCooldown = true;
            timer.Once(1800, () =>
            {
                bugReportCooldown = false;
            });
            Interface.Oxide.DataFileSystem.GetDatafile("ACS_JOIN");
            /*dummyPlayer.Kill();*/
        }

        void Unload()
        {
            if (this.Title == "AntiCheatScience")
            {
                ConVar.AntiHack.eye_protection = 0;
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    if (permission.UserHasPermission(player.UserIDString, "AntiCheatScience.can.seedetect"))
                        CuiHelper.DestroyUi(player, _Layer1);
                }
            }
            var data = Interface.Oxide.DataFileSystem.GetFile("ACS_JOIN");
            data.Save();
            Interface.Oxide.DataFileSystem.SaveDatafile("ACS_JOIN");
        }

        void OnServerShutdown()
        {
            ConVar.AntiHack.eye_protection = 0;
        }

        #endregion

        #region AIM_Detect

        float metrpopal = 0f;
        Timer checktimer;

        Dictionary<ulong, int> playerHeadshotCounts = new Dictionary<ulong, int>();
        Dictionary<ulong, int> playerBodyshotCounts = new Dictionary<ulong, int>();
        Dictionary<ulong, int> AimLockToD = new Dictionary<ulong, int>();
        Dictionary<ulong, int> AimLockToB = new Dictionary<ulong, int>();
        Dictionary<ulong, int> AimLockShot = new Dictionary<ulong, int>();
        Dictionary<ulong, bool> AimLockNaProverke = new Dictionary<ulong, bool>();
        List<string> weaponrifle = new List<string> {
                    "rifle.ak", "rifle.lr300", "rifle.semiauto", "lmg.m249", "rifle.ak.diver", "rifle.ak.ice", "rifle.sks"
                };

        List<string> weaponpistol = new List<string> {
                    "pistol.m92", "pistol.semiauto", "pistol.prototype17", "pistol.python", "pistol.eoka", "pistol.revolver"
                };

        List<string> weaponpp = new List<string> {
                    "smg.mp5", "smg.2", "smg.thompson"
                };

        List<string> weaponbows = new List<string> {
                    "bow.hunting", "crossbow", "legacy"
                };

        List<string> weaponsnipers = new List<string> {
                    "rifle.l96", "rifle.bolt"
                };
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BasePlayer && info.Initiator is BasePlayer)
            {
                var victim = (BasePlayer)entity;
                var attacker = (BasePlayer)info.Initiator;
                if (victim == null | attacker == null)
                    return;
                if (!AimLockNaProverke.ContainsKey(attacker.userID))
                {
                    AimLockNaProverke.Add(attacker.userID, false);
                }
                if (AimLockNaProverke[attacker.userID] == true)
                    info.damageTypes.ScaleAll(0.01f);

                metrpopal = Vector3.Distance(victim.transform.position, attacker.transform.position);

                if (!playerHeadshotCounts.ContainsKey(attacker.userID))
                {
                    playerHeadshotCounts.Add(attacker.userID, 0);
                }
                if (!playerBodyshotCounts.ContainsKey(attacker.userID))
                {
                    playerBodyshotCounts.Add(attacker.userID, 0);
                }
                if (AimTrain != null)
                {
                    if ((bool)AimTrain.CallHook("IsAimTraining", attacker.userID))
                        return;
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
                                string banReason = $"{_config.Manipulator.NameAimM}";

                                DetectNo7(attacker, victim, weaponM, kickCount, distanseM, false);
                                kickCount++;
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
                                    if (_config.Manipulator.isManipT)
                                    {
                                        if (_config.RustAPP.isRustAPP)
                                        {
                                            if (_config.Manipulator.banMeraAimM == 1)
                                            {
                                                rust.RunServerCommand($"ra.ban {attacker.userID.ToString()} \"{banReason} [AntiCheatScience]\"");
                                            }
                                            else if (_config.Manipulator.banMeraAimM == 2)
                                            {
                                                rust.RunServerCommand($"ra.ban {attacker.userID.ToString()} \"{banReason} [AntiCheatScience]\" --global");
                                            }
                                            else if (_config.Manipulator.banMeraAimM == 3)
                                            {
                                                rust.RunServerCommand($"ra.ban {attacker.userID.ToString()} \"{banReason} [AntiCheatScience]\" --ban-ip");
                                            }
                                            else if (_config.Manipulator.banMeraAimM == 4)
                                            {
                                                rust.RunServerCommand($"ra.ban {attacker.userID.ToString()} \"{banReason} [AntiCheatScience]\" --ban-ip --global");
                                            }
                                        }
                                        if (!_config.RustAPP.isRustAPPbanOnly)
                                        {
                                            string commanda = _config.Global.BanCommand.Replace("%steamid%", attacker.userID.ToString()).Replace("%reason%", banReason);
                                            rust.RunServerCommand($"{commanda}");
                                        }
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

                string weaponfire = info.Weapon?.GetItem()?.info.shortname;

                if (!attacker.IsNpc && !victim.IsNpc)
                {
                    if (weaponbows.Contains(weaponfire) && metrpopal > _config.Aim.НастройкаЛуков.metrbow)
                    {
                        if (info.isHeadshot)
                        {
                            playerHeadshotCounts[attacker.userID]++;
                            //DetectNo1(attacker, victim, weaponfire, metrpopal, playerHeadshotCounts[attacker.userID], _config.Aim.НастройкаЛуков.isTwoBowH, false);
                            timer.Once(_config.Aim.НастройкаЛуков.isTwoBowCD, () =>
                            {
                                playerHeadshotCounts[attacker.userID] = 0;
                            });
                            if (playerHeadshotCounts[attacker.userID] >= _config.Aim.НастройкаЛуков.isTwoBowH)
                            {
                                info.damageTypes.ScaleAll(0.01f);
                                DetectNo1(attacker, victim, weaponfire, metrpopal, playerHeadshotCounts[attacker.userID], _config.Aim.НастройкаЛуков.isTwoBowH, true);
                            }
                        }
                        else
                        {
                            playerBodyshotCounts[attacker.userID]++;
                            //DetectNo1B(attacker, victim, weaponfire, metrpopal, playerBodyshotCounts[attacker.userID], _config.Aim.НастройкаЛуков.isTwoBowB, false);
                            timer.Once(_config.Aim.НастройкаЛуков.isTwoBowCD, () =>
                            {
                                playerBodyshotCounts[attacker.userID] = 0;
                            });
                            if (playerBodyshotCounts[attacker.userID] >= _config.Aim.НастройкаЛуков.isTwoBowB)
                            {
                                info.damageTypes.ScaleAll(0.01f);
                                DetectNo1B(attacker, victim, weaponfire, metrpopal, playerBodyshotCounts[attacker.userID], _config.Aim.НастройкаЛуков.isTwoBowB, true);
                            }
                        }
                    }

                    if (weaponpistol.Contains(weaponfire) && metrpopal > _config.Aim.НастройкаПистолетов.metrpistol)
                    {
                        if (info.isHeadshot)
                        {
                            playerHeadshotCounts[attacker.userID]++;
                            //DetectNo1(attacker, victim, weaponfire, metrpopal, playerHeadshotCounts[attacker.userID], _config.Aim.НастройкаПистолетов.isTwoPistolH, false);
                            timer.Once(_config.Aim.НастройкаПистолетов.isTwoPistolCD, () =>
                            {
                                playerHeadshotCounts[attacker.userID] = 0;
                            });
                            if (playerHeadshotCounts[attacker.userID] >= _config.Aim.НастройкаПистолетов.isTwoPistolH)
                            {
                                info.damageTypes.ScaleAll(0.01f);
                                DetectNo1(attacker, victim, weaponfire, metrpopal, playerHeadshotCounts[attacker.userID], _config.Aim.НастройкаПистолетов.isTwoPistolH, true);
                            }
                        }
                        else
                        {
                            playerBodyshotCounts[attacker.userID]++;
                            //DetectNo1B(attacker, victim, weaponfire, metrpopal, playerBodyshotCounts[attacker.userID], _config.Aim.НастройкаПистолетов.isTwoPistolB, false);
                            timer.Once(_config.Aim.НастройкаПистолетов.isTwoPistolCD, () =>
                            {
                                playerBodyshotCounts[attacker.userID] = 0;
                            });
                            if (playerBodyshotCounts[attacker.userID] >= _config.Aim.НастройкаПистолетов.isTwoPistolB)
                            {
                                info.damageTypes.ScaleAll(0.01f);
                                DetectNo1B(attacker, victim, weaponfire, metrpopal, playerBodyshotCounts[attacker.userID], _config.Aim.НастройкаПистолетов.isTwoPistolB, true);
                            }
                        }
                    }

                    if (weaponpp.Contains(weaponfire) && metrpopal > _config.Aim.НастройкаПП.metrpp)
                    {
                        if (info.isHeadshot)
                        {
                            playerHeadshotCounts[attacker.userID]++;
                            //DetectNo1(attacker, victim, weaponfire, metrpopal, playerHeadshotCounts[attacker.userID], _config.Aim.НастройкаПП.isTwoPPH, false);
                            timer.Once(_config.Aim.НастройкаПП.isTwoPPCD, () =>
                            {
                                playerHeadshotCounts[attacker.userID] = 0;
                            });
                            if (playerHeadshotCounts[attacker.userID] >= _config.Aim.НастройкаПП.isTwoPPH)
                            {
                                info.damageTypes.ScaleAll(0.01f);
                                DetectNo1(attacker, victim, weaponfire, metrpopal, playerHeadshotCounts[attacker.userID], _config.Aim.НастройкаПП.isTwoPPH, true);
                            }
                        }
                        else
                        {
                            playerBodyshotCounts[attacker.userID]++;
                            //DetectNo1B(attacker, victim, weaponfire, metrpopal, playerBodyshotCounts[attacker.userID], _config.Aim.НастройкаПП.isTwoPPB, false);
                            timer.Once(_config.Aim.НастройкаПП.isTwoPPCD, () =>
                            {
                                playerBodyshotCounts[attacker.userID] = 0;
                            });
                            if (playerBodyshotCounts[attacker.userID] >= _config.Aim.НастройкаПП.isTwoPPB)
                            {
                                info.damageTypes.ScaleAll(0.01f);
                                DetectNo1B(attacker, victim, weaponfire, metrpopal, playerBodyshotCounts[attacker.userID], _config.Aim.НастройкаПП.isTwoPPB, true);
                            }
                        }
                    }

                    if (weaponrifle.Contains(weaponfire) && metrpopal > _config.Aim.НастройкаВинтовок.metrrifle)
                    {
                        if (info.isHeadshot)
                        {
                            playerHeadshotCounts[attacker.userID]++;
                            //DetectNo1(attacker, victim, weaponfire, metrpopal, playerHeadshotCounts[attacker.userID], _config.Aim.НастройкаВинтовок.isTwoRifH, false);
                            timer.Once(_config.Aim.НастройкаВинтовок.isTwoRifCD, () =>
                            {
                                playerHeadshotCounts[attacker.userID] = 0;
                            });
                            if (playerHeadshotCounts[attacker.userID] >= _config.Aim.НастройкаВинтовок.isTwoRifH)
                            {
                                info.damageTypes.ScaleAll(0.01f);
                                DetectNo1(attacker, victim, weaponfire, metrpopal, playerHeadshotCounts[attacker.userID], _config.Aim.НастройкаВинтовок.isTwoRifH, true);
                            }
                        }
                        else
                        {
                            playerBodyshotCounts[attacker.userID]++;
                            //DetectNo1B(attacker, victim, weaponfire, metrpopal, playerBodyshotCounts[attacker.userID], _config.Aim.НастройкаВинтовок.isTwoRifB, false);
                            timer.Once(_config.Aim.НастройкаВинтовок.isTwoRifCD, () =>
                            {
                                playerBodyshotCounts[attacker.userID] = 0;
                            });
                            if (playerBodyshotCounts[attacker.userID] >= _config.Aim.НастройкаВинтовок.isTwoRifB)
                            {
                                info.damageTypes.ScaleAll(0.01f);
                                DetectNo1B(attacker, victim, weaponfire, metrpopal, playerBodyshotCounts[attacker.userID], _config.Aim.НастройкаВинтовок.isTwoRifB, true);
                            }
                        }
                    }

                    if (metrpopal > 100f && metrpopal < 200f && /*weaponrifle.Contains(weaponfire)*/ weaponfire == "rifle.ak")
                    {
                        if (!AimLockToD.ContainsKey(attacker.userID))
                        {
                            AimLockToD.Add(attacker.userID, 0);
                        }
                        if (!AimLockToB.ContainsKey(attacker.userID))
                        {
                            AimLockToB.Add(attacker.userID, 0);
                        }
                        if (!AimLockToD.ContainsKey(attacker.userID))
                        {
                            AimLockNaProverke.Add(attacker.userID, false);
                        }

                        AimLockToD[attacker.userID]++;

                        timer.Once(60f, () =>
                        {
                            AimLockToD[attacker.userID] = 0;
                        });

                        if (AimLockToD[attacker.userID] == 8 && attacker.TimeAlive() > 1800)
                        {
                            if (checktimer == null)
                            {
                                AimLockNaProverke[attacker.userID] = true;
                                
                                Vector3 initialAttackerPosition = attacker.transform.position;
                                Vector3 initialVictimPosition = victim.transform.position;

                                checktimer = timer.Every(0.1f, () =>
                                {
                                    Vector3 victimPosition = victim.transform.position;
                                    var MaxRadius = 0.395f;
                                    var MaxRadiusNear = 0.080f;

                                    float incrementPerUnit = 0.07550f;
                                    float incrementPerUnitNear = 0.03400f;

                                    float unitsDifference = (metrpopal - 100) / 5;

                                    float incrementToAdd = unitsDifference * incrementPerUnit;
                                    float incrementToAddNear = unitsDifference * incrementPerUnitNear;

                                    float newMaxRadius = MaxRadius + incrementToAdd;
                                    float newMaxRadiusNear = MaxRadiusNear + incrementToAddNear;

                                    RaycastHit hit;
                                    bool hitDetected = Physics.SphereCast(attacker.eyes.position, newMaxRadius, attacker.eyes.BodyForward(), out hit, Vector3.Distance(attacker.eyes.position, victimPosition), 2048 | 131072 | 1218519297, QueryTriggerInteraction.Ignore);
                                    bool NearDetected = Physics.SphereCast(attacker.eyes.position, newMaxRadiusNear, attacker.eyes.BodyForward(), out hit, Vector3.Distance(attacker.eyes.position, victimPosition), 2048 | 131072 | 1218519297, QueryTriggerInteraction.Ignore);

                                    if (hitDetected && !NearDetected)
                                    {
                                        AimLockToB[attacker.userID]++;
                                    }
                                });

                                timer.Once(7f, () =>
                                {
                                    Vector3 finalAttackerPosition = attacker.transform.position;
                                    Vector3 finalVictimPosition = victim.transform.position;

                                    float initialDistance = Vector3.Distance(initialAttackerPosition, initialVictimPosition);
                                    float finalDistance = Vector3.Distance(finalAttackerPosition, finalVictimPosition);

                                    if (Mathf.Abs(finalDistance - initialDistance) < 15f)
                                    {
                                        return;
                                    }

                                    checktimer.Destroy();
                                    checktimer = null;
                                    Puts($"[{attacker.displayName} | {attacker.userID}] - получил [{AimLockToB[attacker.userID]}/70] детектов AimLock! Выпустив {AimLockShot[attacker.userID]} пуль во время проверки");
                                    AimLockNaProverke[attacker.userID] = false;
                                    if (AimLockShot[attacker.userID] >= 1 && AimLockToB[attacker.userID] >= 30)
                                    {
                                        DetectNo5(attacker, victim, weaponfire, metrpopal, AimLockToB[attacker.userID]);
                                    }
                                    AimLockShot[attacker.userID] = 0;
                                    AimLockToB[attacker.userID] = 0;
                                });
                            }
                            else
                            {
                                info.damageTypes.ScaleAll(0.01f);
                            }

                        }
                    }
                }

                if (_config.NightFire.isNH)
                {
                    if (metrpopal > _config.NightFire.metrnh)
                    {
                        if (!victim.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
                        {
                            if (!victim.HasPlayerFlag(BasePlayer.PlayerFlags.Wounded))
                            {
                                var currentTime = ConVar.Env.time;
                                var roundedMetrpopal = Math.Round(metrpopal, 2);
                                var roundedCurrentTime = Math.Round(currentTime % 24, 0);
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
                                bool isNightVisionINT = false;
                                if (NightVision != null)
                                {
                                    if ((bool)NightVision.CallHook("IsPlayerTimeLocked", attacker))
                                        isNightVisionINT = true;
                                }
                                if (IsNighttime() && (!hasFlashlight || !hasNightVision || !isNightVisionINT))
                                {
                                    string jsonwebhooknightshot = jsonwebhooknightshotRU;
                                    DetectNo9(attacker, victim, metrpopal, roundedCurrentTime, weaponnight, hasFlashlight, hasNightVision);
                                    Puts($"Игрок {attacker.displayName} {_config.NightFire.NameNightShot}");
                                    string banReason = $"{_config.NightFire.NameNightShot}";
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
            if (!_config.Traps.isStash)
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
                if (MultiFighting != null)
                    steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
                
                string playerInfo = CheckInfoS(player.userID.ToString());

                if (detectCounts[player.userID] != _config.Traps.stashBCount)
                {
                    RequestDC(jsonwebhookstashdetectRU.Replace("[steamid]", $"{player.userID}")
                        .Replace("[banordetect]", "Блокировка")
                        .Replace("[reason]", _config.Traps.NameStash)
                        .Replace("[grid]", Grid)
                        .Replace("[cords]", player.transform.position.ToString())
                        .Replace("[dCount]", DCount.ToString())
                        .Replace("[mCount]", _config.Traps.stashBCount.ToString())
                        .Replace("[image]", _config.Logs.WebhookImage)
                        .Replace("[player]", $"{player.displayName} | {player.userID}")
                        .Replace("[pirate]", steam ? "Да" : "Нет")
                        .Replace("[sleepbag]", aresleepbag ? "Да" : "Нет")
                        .Replace("[playerInfo]", playerInfo));
                }

                if (detectCounts[player.userID] >= _config.Traps.stashBCount)
                {
                    RequestDC(jsonwebhookstashdetectRU.Replace("[steamid]", $"{player.userID}")
                        .Replace("[banordetect]", "Детект")
                        .Replace("[reason]", _config.Traps.NameStash)
                        .Replace("[grid]", Grid)
                        .Replace("[cords]", player.transform.position.ToString())
                        .Replace("[dCount]", DCount.ToString())
                        .Replace("[mCount]", _config.Traps.stashBCount.ToString())
                        .Replace("[image]", _config.Logs.WebhookImage)
                        .Replace("[player]", $"{player.displayName} | {player.userID}")
                        .Replace("[pirate]", steam ? "Да" : "Нет")
                        .Replace("[sleepbag]", aresleepbag ? "Да" : "Нет")
                        .Replace("[playerInfo]", playerInfo));

                    string banReason = $"{_config.Traps.NameStash}";

                    if (_config.RustAPP.isRustAPP)
                    {
                        if (_config.Traps.banMeraStash == 1)
                        {
                            rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{banReason} [AntiCheatScience]\"");
                        }
                        else if (_config.Traps.banMeraStash == 2)
                        {
                            rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{banReason} [AntiCheatScience]\" --global");
                        }
                        else if (_config.Traps.banMeraStash == 3)
                        {
                            rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{banReason} [AntiCheatScience]\" --ban-ip");
                        }
                        else if (_config.Traps.banMeraStash == 4)
                        {
                            rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{banReason} [AntiCheatScience]\" --ban-ip --global");
                        }
                    }
                    if (!_config.RustAPP.isRustAPPbanOnly)
                    {
                        string commanda = _config.Global.BanCommand.Replace("%steamid%", player.userID.ToString()).Replace("%reason%", banReason);
                        rust.RunServerCommand($"{commanda}");
                    }
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
            if (player.isMounted || player.HasParent() || player.IsOnGround())
                return false;
            if (type == AntiHackType.FlyHack)
            {
                string banReason = $"{_config.FlyHack.NameFly}";
                /*if (player.GetParentEntity() != null && player.GetParentEntity().ShortPrefabName == "ladder.wooden.wall")
                {
                    return false;
                }*/

                if (!_config.FlyHack.isFly)
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
                if (_config.FlyHack.isFlyBan)
                {
                    DetectNo3(player, true);
                    string commanda = _config.Global.BanCommand.Replace("%steamid%", player.userID.ToString()).Replace("%reason%", banReason);
                    rust.RunServerCommand($"{commanda}");
                }
                if (!_config.FlyHack.isFlyBan)
                {
                    DetectNo3(player, false);
                    rust.RunServerCommand($"kick {player.userID} \"{banReason}\"");
                }
                Puts($"Игрок {player.userID} {_config.FlyHack.NameFly}");
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
            string[] forbiddenObjects = new string[] { "quarry_main", "quarry_track", "Server", "assets/prefabs/building/ladder.wall.wood/ladder.wooden.wall.prefab",
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
            player.SetPlayerFlag(BasePlayer.PlayerFlags.IsDeveloper, true);
            NextTick( () => {
                player.SendConsoleCommand("camspeed 0.0");
                NextTick( () => {
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsDeveloper, false);
                });
            });
            
            string playerName = player.displayName;
            if (playerName.Contains("1488") || playerName.Contains("卐"))
                player.Kick("Помни о наших ветеранах, маленький ублюдок");
            string playerIP = player.net.connection.ipaddress.Split(':')[0];
            if (_config.Steam.PROXY_CHECT)
            {
                CheckPlayerIP(player.userID.ToString(), playerIP, playerName);
            }
            CheckInfoS(player.userID.ToString());
            var steamID = player.userID.ToString();
            if (!_PlayerEyes.ContainsKey(steamID))
            {
                _PlayerEyes.Add(steamID, Vector3.zero);
            }
            if (_config.Steam.STEAM_CHECT)
            {
                if (permission.UserHasPermission(player.userID.ToString(), _config.Steam.SteamDaysIgnore))
                    return;

                CheckAccountAge(player.userID.ToString());
            }
        }

        private bool isSteam(BasePlayer player)
        {
            if (MultiFighting == null)
                return true;

            return MultiFighting.Call<bool>("IsSteam", player);
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
                string url = $"https://api.ip2location.io/?key={_config.Steam.YOUR_API_KEY}&ip={playerIP}";
                webrequest.Enqueue(url, "", (code, response) =>
                {
                    if (code == 200 && !string.IsNullOrEmpty(response))
                    {
                        IPInfo ipInfo = JsonConvert.DeserializeObject<IPInfo>(response);
                        bool isProxy = ipInfo.is_proxy;
                        if (isProxy)
                        {
                            Puts($"Игрок {playerName} ({playerIP}) использует PROXY, бан :D");
                            if (_config.RustAPP.isRustAPP)
                            {
                                rust.RunServerCommand($"ra.ban {userID.ToString()} \"Использование PROXY [AntiCheatScience]\" --ban-ip --global");
                            }
                            if (!_config.RustAPP.isRustAPPbanOnly)
                            {
                                string commanda = _config.Global.BanCommand.Replace("%steamid%", userID.ToString()).Replace("%reason%", "Плохой мальчик, выключай PROXY :D");
                                rust.RunServerCommand($"{commanda}");
                            }
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

        #region CodeBan
        
        private static bool IsBanned(ulong userid)
        {
            return ServerUsers.Is(userid, ServerUsers.UserGroup.Banned);
        }

        private void OnCodeEntered(CodeLock codeLock, BasePlayer player, string code)
        {
            ulong owner = codeLock.OwnerID;
            if (player == null || code != codeLock.code) 
                return;

            if (IsBanned(owner))
            {
                string playerName = player.displayName;
                string Grid = GetGridString(player.transform.position);
                string playerInfo = CheckInfoS(player.userID.ToString());
                if (MultiFighting != null)
                    steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
                
                RequestDC(jsonwebhookcodeRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[reason]", _config.CodeLock.NameCodeLock)
                    .Replace("[grid]", Grid)
                    .Replace("[image]", _config.Logs.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo)
                    .Replace("[owner]", $"{owner}"));
            }
        }   
        
        #endregion

        #region Logs
        private void DetectNo3(BasePlayer player, bool banlogorlog)
        {
            string Grid = GetGridString(player.transform.position);
            if (MultiFighting != null)
                steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
            ChatDetect(player, _config.FlyHack.NameFly);
            
            string playerInfo = CheckInfoS(player.userID.ToString());
            string nearbyObjects = GetNearbyObjects(player.transform.position, 2.5f);
            if (banlogorlog)
            {
                RequestDC(jsonwebhookviolationRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Блокировка")
                    .Replace("[reason]", _config.FlyHack.NameFly)
                    .Replace("[grid]", Grid)
                    .Replace("[object]", nearbyObjects)
                    .Replace("[cords]", player.transform.position.ToString())
                    .Replace("[image]", _config.Logs.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo));
            }
            else
            {
                RequestDC(jsonwebhookviolationRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Кик")
                    .Replace("[reason]", _config.FlyHack.NameFly)
                    .Replace("[grid]", Grid)
                    .Replace("[object]", nearbyObjects)
                    .Replace("[cords]", player.transform.position.ToString())
                    .Replace("[image]", _config.Logs.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo));
            }
        }
        /*private void DetectNo4(BasePlayer player, int DCount,/* bool aresleepbag,*/ /*bool banlogorlog)
        {
            string Grid = GetGridString(player.transform.position);
            bool steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
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
        /*.Replace("[playerInfo]", playerInfo));
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
        /*.Replace("[playerInfo]", playerInfo));
    ChatDetect(player, _config.НастройкаАима.NameAim);
}
}*/
        private void DetectNo10(BasePlayer player, int detect, bool banlogorlog)
        {
            string Grid = GetGridString(player.transform.position);
            if (MultiFighting != null)
                steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
            ChatDetect(player, _config.MeleeAttack.NameMeleeAttack);
            
            string playerInfo = CheckInfoS(player.userID.ToString());
            if (banlogorlog)
            {
                RequestDC(jsonwebhookrapidRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Блокировка")
                    .Replace("[reason]", _config.MeleeAttack.NameMeleeAttack)
                    .Replace("[grid]", Grid)
                    .Replace("[image]", _config.Logs.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[dCount]", detect.ToString())
                    .Replace("[mCount]", "3")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo));
            }
            else
            {
                RequestDC(jsonwebhookrapidRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Детект")
                    .Replace("[reason]", _config.MeleeAttack.NameMeleeAttack)
                    .Replace("[grid]", Grid)
                    .Replace("[dCount]", detect.ToString())
                    .Replace("[mCount]", "3")
                    .Replace("[image]", _config.Logs.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo));
            }
        }
        private void DetectNo11(BasePlayer player, int detect, bool banlogorlog)
        {
            string Grid = GetGridString(player.transform.position);
            if (MultiFighting != null)
                steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);

            string playerInfo = CheckInfoS(player.userID.ToString());
            if (banlogorlog)
            {
                RequestDC(jsonwebhookrapidRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Блокировка")
                    .Replace("[reason]", _config.SilentAim.NameSAim)
                    .Replace("[image]", _config.Logs.WebhookImage)
                    .Replace("[grid]", Grid)
                    .Replace("[dCount]", detect.ToString())
                    .Replace("[mCount]", _config.SilentAim.maxDetect.ToString())
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo));
            }
            else
            {
                RequestDC(jsonwebhookrapidRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Детект")
                    .Replace("[reason]", _config.SilentAim.NameSAim)
                    .Replace("[dCount]", detect.ToString())
                    .Replace("[mCount]", _config.SilentAim.maxDetect.ToString())
                    .Replace("[image]", _config.Logs.WebhookImage)
                    .Replace("[grid]", Grid)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo));
            }
        }
        private void DetectNo12(BasePlayer player)
        {
            string Grid = GetGridString(player.transform.position);
            if (MultiFighting != null)
                steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
            ChatDetect(player, _config.NoFallDamage.NameNFD);

            string playerInfo = CheckInfoS(player.userID.ToString());

            RequestDC(jsonwebhookNFDRU.Replace("[steamid]", $"{player.userID}")
                .Replace("[banordetect]", "Блокировка")
                .Replace("[reason]", _config.NoFallDamage.NameNFD)
                .Replace("[image]", _config.Logs.WebhookImage)
                .Replace("[grid]", Grid)
                .Replace("[player]", $"{player.displayName} | {player.userID}")
                .Replace("[pirate]", steam ? "Да" : "Нет")
                .Replace("[playerInfo]", playerInfo));
        }
        private void DetectNo13(BasePlayer player)
        {
            string Grid = GetGridString(player.transform.position);
            if (MultiFighting != null)
                steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
            ChatDetect(player, _config.SpiderHack.NameSpider);

            string playerInfo = CheckInfoS(player.userID.ToString());

            RequestDC(jsonwebhookspiderRU.Replace("[steamid]", $"{player.userID}")
                .Replace("[banordetect]", "Блокировка")
                .Replace("[reason]", _config.SpiderHack.NameSpider)
                .Replace("[image]", _config.Logs.WebhookImage)
                .Replace("[grid]", Grid)
                .Replace("[player]", $"{player.displayName} | {player.userID}")
                .Replace("[pirate]", steam ? "Да" : "Нет")
                .Replace("[playerInfo]", playerInfo));
        }
        private void DetectNo9(BasePlayer player, BasePlayer victim, float metrpopal, double time, string weaponnight, bool hasFlashlight, bool hasNightVision)
        {
            string Grid = GetGridString(player.transform.position);
            if (MultiFighting != null)
                steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
            ChatDetect(player, _config.NightFire.NameNightShot);

            string playerInfo = CheckInfoS(player.userID.ToString());

            RequestDC(jsonwebhooknightshotRU.Replace("[steamid]", $"{player.userID}")
                .Replace("[reason]", _config.NightFire.NameNightShot)
                .Replace("[metr]", Math.Round(metrpopal).ToString())
                .Replace("[grid]", Grid)
                .Replace("[flash]", hasFlashlight || hasNightVision ? "Есть" : "Нет")
                .Replace("[time]", time.ToString())
                .Replace("[weapon]", weaponnight)
                .Replace("[image]", _config.Logs.WebhookImage)
                .Replace("[player]", $"{player.displayName} | {player.userID}")
                .Replace("[pirate]", steam ? "Да" : "Нет")
                .Replace("[playerInfo]", playerInfo)
                .Replace("[victim]", /*$"{victim.displayName} | {victim.userID}"*/ $"{victim.displayName}"));
        }
        private void DetectNo7(BasePlayer player, BasePlayer victim, string weaponname, int dCount, float metrp, bool banlogorlog)
        {
            string Grid = GetGridString(player.transform.position);
            if (MultiFighting != null)
                steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
            ChatDetect(player, _config.Manipulator.NameAimM);
                
            string playerInfo = CheckInfoS(player.userID.ToString());
            if (banlogorlog)
            {
                RequestDC(jsonwebhookmanipRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Блокировка")
                    .Replace("[reason]", _config.Manipulator.NameAimM)
                    .Replace("[metr]", Math.Round(metrp).ToString())
                    .Replace("[grid]", Grid)
                    .Replace("[weapon]", weaponname)
                    .Replace("[dCount]", dCount.ToString())
                    .Replace("[mCount]", "3")
                    .Replace("[image]", _config.Logs.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo)
                    .Replace("[victim]", /*$"{victim.displayName} | {victim.userID}"*/ $"{victim.displayName}"));
            }
            else
            {
                RequestDC(jsonwebhookmanipRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Детект")
                    .Replace("[reason]", _config.Manipulator.NameAimM)
                    .Replace("[metr]", Math.Round(metrp).ToString())
                    .Replace("[grid]", Grid)
                    .Replace("[weapon]", weaponname)
                    .Replace("[dCount]", dCount.ToString())
                    .Replace("[mCount]", "3")
                    .Replace("[image]", _config.Logs.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo)
                    .Replace("[victim]", /*$"{victim.displayName} | {victim.userID}"*/ $"{victim.displayName}"));
            }
        }
        private void DetectNo8(BasePlayer player, string weaponname)
        {
            string Grid = GetGridString(player.transform.position);
            if (MultiFighting != null)
                steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
            ChatDetect(player, _config.FlyFire.NameFlyFire);
                
            string playerInfo = CheckInfoS(player.userID.ToString());

            RequestDC(jsonwebhookflyfireRU.Replace("[steamid]", $"{player.userID}")
                .Replace("[reason]", _config.FlyFire.NameFlyFire)
                .Replace("[grid]", Grid)
                .Replace("[weapon]", weaponname)
                .Replace("[image]", _config.Logs.WebhookImage)
                .Replace("[player]", $"{player.displayName} | {player.userID}")
                .Replace("[pirate]", steam ? "Да" : "Нет")
                .Replace("[playerInfo]", playerInfo));
        }
        private void DetectNo1(BasePlayer player, BasePlayer victim, string weaponname, float metrpopal, int dCount, int mCount, bool banlogorlog)
        {
            string Grid = GetGridString(player.transform.position);
            if (MultiFighting != null)
                steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
                
            string playerInfo = CheckInfoS(player.userID.ToString());

            ChatDetect(player, _config.Aim.NameAim);

            if (banlogorlog)
            {
                if (_config.Aim.isAimBan)
                {
                    if (_config.RustAPP.isRustAPP)
                    {
                        if (_config.Aim.banMeraAim == 1)
                        {
                            rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.Aim.NameAim} [AntiCheatScience]\"");
                        }
                        else if (_config.Aim.banMeraAim == 2)
                        {
                            rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.Aim.NameAim} [AntiCheatScience]\" --global");
                        }
                        else if (_config.Aim.banMeraAim == 3)
                        {
                            rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.Aim.NameAim} [AntiCheatScience]\" --ban-ip");
                        }
                        else if (_config.Aim.banMeraAim == 4)
                        {
                            rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.Aim.NameAim} [AntiCheatScience]\" --ban-ip --global");
                        }
                    }
                    if (!_config.RustAPP.isRustAPPbanOnly)
                    {
                        string commanda = _config.Global.BanCommand.Replace("%steamid%", player.userID.ToString()).Replace("%reason%", _config.Aim.NameAim);
                        rust.RunServerCommand($"{commanda}");
                    }
                }
                else
                {
                    rust.RunServerCommand($"kick \"{player.userID}\" \"{_config.Aim.NameAim}\"");
                }

                RequestDC(jsonwebhookaimdetectRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Блокировка")
                    .Replace("[headorbody]", "Голова")
                    .Replace("[metr]", Math.Round(metrpopal).ToString())
                    .Replace("[grid]", Grid)
                    .Replace("[reason]", _config.Aim.NameAim)
                    .Replace("[weapon]", weaponname)
                    .Replace("[dCount]", dCount.ToString())
                    .Replace("[mCount]", mCount.ToString())
                    .Replace("[image]", _config.Logs.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo)
                    .Replace("[victim]", /*$"{victim.displayName} | {victim.userID}"*/ $"{victim.displayName}"));
            }
            else
            {
                /*RequestDC(jsonwebhookaimdetectRU.Replace("[steamid]", $"{player.userID}")
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
                    .Replace("[playerInfo]", playerInfo)*/
                    //.Replace("[victim]", /*$"{victim.displayName} | {victim.userID}"*/ $"{victim.displayName}"));
            }
        }
        private void DetectNo2(BasePlayer player)
        {
            string Grid = GetGridString(player.transform.position);
            if (MultiFighting != null)
                steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
            ChatDetect(player, _config.PilotFire.NameAimDrive);
                
            string playerInfo = CheckInfoS(player.userID.ToString());

            RequestDC(jsonwebhookaimdriveRU.Replace("[steamid]", $"{player.userID}")
                .Replace("[banordetect]", "Блокировка")
                .Replace("[grid]", Grid)
                .Replace("[reason]", _config.PilotFire.NameAimDrive)
                .Replace("[image]", _config.Logs.WebhookImage)
                .Replace("[player]", $"{player.displayName} | {player.userID}")
                .Replace("[pirate]", steam ? "Да" : "Нет")
                .Replace("[playerInfo]", playerInfo));
        }
        private void DetectNo5(BasePlayer player, BasePlayer victim, string weaponname, float metrpopal, int dCount)
        {
            string Grid = GetGridString(player.transform.position);
            if (MultiFighting != null)
                steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
                
            string playerInfo = CheckInfoS(player.userID.ToString());

            ChatDetect(player, _config.AimLock.NameAimLock);

            if (_config.AimLock.isAimLockBan)
            {
                if (_config.RustAPP.isRustAPP)
                {
                    if (_config.AimLock.banMeraAimLock == 1)
                    {
                        rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.AimLock.NameAimLock} [AntiCheatScience]\"");
                    }
                    else if (_config.AimLock.banMeraAimLock == 2)
                    {
                        rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.AimLock.NameAimLock} [AntiCheatScience]\" --global");
                    }
                    else if (_config.AimLock.banMeraAimLock == 3)
                    {
                        rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.AimLock.NameAimLock} [AntiCheatScience]\" --ban-ip");
                    }
                    else if (_config.AimLock.banMeraAimLock == 4)
                    {
                        rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.AimLock.NameAimLock} [AntiCheatScience]\" --ban-ip --global");
                    }
                }
                if (!_config.RustAPP.isRustAPPbanOnly)
                {
                    string commanda = _config.Global.KickCommand.Replace("%steamid%", player.userID.ToString()).Replace("%reason%", _config.AimLock.NameAimLock);
                    rust.RunServerCommand($"{commanda}");
                }
            }
            else
            {
                string commanda = _config.Global.KickCommand.Replace("%steamid%", player.userID.ToString()).Replace("%reason%", _config.AimLock.NameAimLock);
                rust.RunServerCommand($"{commanda}");
            }

            RequestDC(jsonwebhookaimlockRU.Replace("[steamid]", $"{player.userID}")
                .Replace("[banordetect]", _config.AimLock.isAimLockBan ? "Блокировка" : "Кик")
                .Replace("[metr]", Math.Round(metrpopal).ToString())
                .Replace("[grid]", Grid)
                .Replace("[reason]", _config.AimLock.NameAimLock)
                .Replace("[weapon]", weaponname)
                .Replace("[dCount]", dCount.ToString())
                .Replace("[image]", _config.Logs.WebhookImage)
                .Replace("[player]", $"{player.displayName} | {player.userID}")
                .Replace("[pirate]", steam ? "Да" : "Нет")
                .Replace("[playerInfo]", playerInfo)
                .Replace("[victim]", /*$"{victim.displayName} | {victim.userID}"*/ $"{victim.displayName}"));
        }
        private void DetectNo1B(BasePlayer player, BasePlayer victim, string weaponname, float metrpopal, int bCount, int mbCount, bool banlogorlog)
        {
            string Grid = GetGridString(player.transform.position);
            if (MultiFighting != null)
                steam = (bool)MultiFighting.CallHook("IsSteam", player.Connection);
                
            string playerInfo = CheckInfoS(player.userID.ToString());
            if (banlogorlog)
            {
                if (_config.Aim.isAimBan)
                {
                    if (_config.RustAPP.isRustAPP)
                    {
                        if (_config.Aim.banMeraAim == 1)
                        {
                            rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.Aim.NameAim} [AntiCheatScience]\"");
                        }
                        else if (_config.Aim.banMeraAim == 2)
                        {
                            rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.Aim.NameAim} [AntiCheatScience]\" --global");
                        }
                        else if (_config.Aim.banMeraAim == 3)
                        {
                            rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.Aim.NameAim} [AntiCheatScience]\" --ban-ip");
                        }
                        else if (_config.Aim.banMeraAim == 4)
                        {
                            rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.Aim.NameAim} [AntiCheatScience]\" --ban-ip --global");
                        }
                    }
                    if (!_config.RustAPP.isRustAPPbanOnly)
                    {
                        string commanda = _config.Global.BanCommand.Replace("%steamid%", player.userID.ToString()).Replace("%reason%", _config.Aim.NameAim);
                        rust.RunServerCommand($"{commanda}");
                    }
                }
                else
                {
                    rust.RunServerCommand($"kick \"{player.userID}\" \"{_config.Aim.NameAim}\"");
                }

                RequestDC(jsonwebhookaimdetectRU.Replace("[steamid]", $"{player.userID}")
                    .Replace("[banordetect]", "Блокировка")
                    .Replace("[headorbody]", "Тело")
                    .Replace("[metr]", Math.Round(metrpopal).ToString())
                    .Replace("[grid]", Grid)
                    .Replace("[reason]", _config.Aim.NameAim)
                    .Replace("[weapon]", weaponname)
                    .Replace("[dCount]", bCount.ToString())
                    .Replace("[mCount]", mbCount.ToString())
                    .Replace("[image]", _config.Logs.WebhookImage)
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
                    .Replace("[reason]", _config.Aim.NameAim)
                    .Replace("[weapon]", weaponname)
                    .Replace("[dCount]", bCount.ToString())
                    .Replace("[mCount]", mbCount.ToString())
                    .Replace("[image]", _config.Logs.WebhookImage)
                    .Replace("[player]", $"{player.displayName} | {player.userID}")
                    .Replace("[pirate]", steam ? "Да" : "Нет")
                    .Replace("[playerInfo]", playerInfo)
                    .Replace("[victim]", /*$"{victim.displayName} | {victim.userID}"*/ $"{victim.displayName}"));
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
            string urlsc = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={_config.Steam.SteamAPI}&steamids={userID}";
            webrequest.Enqueue(urlsc, "", (code, response) =>
            {
                if (code == 403)
                {
                    Debug.LogError("[AntiCheatScience] [STEAM API] Ошибка " + code + " Возможно API Key устарел");
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

                    if (accountAge.TotalDays < _config.Steam.SteamDays)
                    {
                        rust.RunServerCommand($"kick {userID} \"слишком молодой аккаунт!\"");
                    }
                }
                else
                {
                    Debug.LogError("[AntiCheatScience] [STEAM API] Ошибка запроса " + code);
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
            "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Логирование-[banordetect]\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Координаты X Y Z\",\"value\":\"[cords]\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true},{\"name\":\"Обьекты рядом\",\"value\":\"[object]\",\"inline\":false}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Играет с лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";
        private string jsonwebhookcodeRU =
            "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Логирование-Детект\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Владелец кодового замка\",\"value\":\"[owner]\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Играет с лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";
        private string jsonwebhookaimdetectRU =
            "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Логирование-[banordetect]\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Оружие\",\"value\":\"[weapon]\",\"inline\":true},{\"name\":\"Расстояние\",\"value\":\"[metr]метров\",\"inline\":true},{\"name\":\"Детектов\",\"value\":\"[dCount]/[mCount]\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true},{\"name\":\"Пострадавший\",\"value\":\"[victim]\",\"inline\":true},{\"name\":\"ХитБокс\",\"value\":\"[headorbody]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Играет с лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";
        private string jsonwebhookaimdriveRU =
            "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Логирование-[banordetect]\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Играет с лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";
        private string jsonwebhookaimlockRU =
            "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Логирование-[banordetect]\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Оружие\",\"value\":\"[weapon]\",\"inline\":true},{\"name\":\"Расстояние\",\"value\":\"[metr]метров\",\"inline\":true},{\"name\":\"Детектов\",\"value\":\"[dCount]/70\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true},{\"name\":\"Пострадавший\",\"value\":\"[victim]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Играет с лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";
        private string jsonwebhookstashdetectRU =
            "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Логирование-[banordetect]\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Координаты X Y Z\",\"value\":\"[cords]\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true},{\"name\":\"Рядом спальник\",\"value\":\"[sleepbag]\",\"inline\":true},{\"name\":\"Детектов\",\"value\":\"[dCount]/[mCount]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Играет с лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";
        private string jsonwebhooknightshotRU =
            "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Логирование-Детект\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Оружие\",\"value\":\"[weapon]\",\"inline\":true},{\"name\":\"Расстояние\",\"value\":\"[metr]метров\",\"inline\":true},{\"name\":\"Время\",\"value\":\"[time]\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true},{\"name\":\"Пострадавший\",\"value\":\"[victim]\",\"inline\":true},{\"name\":\"Фонарик/ПНВ\",\"value\":\"[flash]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Играет с лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";
        private string jsonwebhookpilotshotRU =
            "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Логирование-[banordetect]\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Координаты X Y Z\",\"value\":\"[cords]\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Играет с лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";
        private string jsonwebhookmanipRU =
            "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Логирование-[banordetect]\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Оружие\",\"value\":\"[weapon]\",\"inline\":true},{\"name\":\"Расстояние\",\"value\":\"[metr]метров\",\"inline\":true},{\"name\":\"Детектов\",\"value\":\"[dCount]/[mCount]\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true},{\"name\":\"Пострадавший\",\"value\":\"[victim]\",\"inline\":true},{\"name\":\"Пинг\",\"value\":\"(Пока нет инфо.)\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Играет с лицензии: [pirate]\\n[playerInfo]\\nМогуть быть ложные детекты из за пинга 170+\\nРекомендуемо запросить результаты SpeedTest\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";
        private string jsonwebhookflyfireRU =
            "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Логирование-Блокировка\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Оружие\",\"value\":\"[weapon]\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Играет с лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";
        private string jsonwebhookrapidRU =
            "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Логирование-[banordetect]\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Детектов\",\"value\":\"[dCount]/[mCount]\",\"inline\":true},{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Играет с лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";
        private string jsonwebhookNFDRU =
            "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Логирование-[banordetect]\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Играет с лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";
        private string jsonwebhookspiderRU =
            "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-Логирование-[banordetect]\",\"description\":\"Детект игрока ([player])\\n**[reason]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null,\"fields\":[{\"name\":\"Координаты\",\"value\":\"[grid]\",\"inline\":true}],\"image\":{\"url\":\"[image]\"}},{\"description\":\"Играет с лицензии: [pirate]\\n[playerInfo]\",\"color\":null,\"author\":{\"name\":\"Кликни сюда что бы перейти в профиль игрока\",\"url\":\"https://steamcommunity.com/profiles/[steamid]/\",\"icon_url\":\"https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/2048px-Steam_icon_logo.svg.png\"}}],\"attachments\":[]}";
        private string bugreport =
            "{\"content\":null,\"embeds\":[{\"title\":\"AntiCheatScience-БагРепорт\",\"description\":\"Пришел БагРепорт с сервера **[sN]**\",\"url\":\"https://hazard-plugins.space/index.php?resources/anticheatscience.20/\",\"color\":null},{\"description\":\"Проблема: [dannie]\\n\\nДискорд тех.администратора: <@[id]>\",\"color\":null}],\"attachments\":[]}";

        private void RequestBugReport(string payload, Action<int> callback = null)
        {
            Dictionary<string, string> header = new Dictionary<string, string>();
            header.Add("Content-Type", "application/json");

            webrequest.Enqueue("https://discord.com/api/webhooks/1236462204514209923/cfG57btYN-aodizL0hMZFo0N4ovDDkymWkYu1oPN0otsUFMdWrk8VVen4GZYnW7GnwR5", payload, (code, response) =>
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

        private void RequestDC(string payload, Action<int> callback = null)
        {
            Dictionary<string, string> header = new Dictionary<string, string>();
            header.Add("Content-Type", "application/json");

            webrequest.Enqueue(_config.Logs.Webhook, payload, (code, response) =>
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

        Dictionary<ulong, string> unicalID = new Dictionary<ulong, string>();
        int uniqueChislo = 0;
        void ChatDetect(BasePlayer player, string banReason)
        {
            foreach (BasePlayer adminPlayer in BasePlayer.activePlayerList)
            {
                if (permission.UserHasPermission(adminPlayer.UserIDString, "AntiCheatScience.can.seedetect"))
                {
                    if (adminPlayer != null)
                    {
                        if (!unicalID.ContainsKey(player.userID))
                        {
                            uniqueChislo++;
                            unicalID.Add(player.userID, $"{uniqueChislo}");
                        }
                        string playerName = player.displayName;
                        string message = $"Игрок <color=yellow>{playerName}</color> задетекчен: <color=red>{banReason}</color>\nНачните слежку по команде: /acspec {unicalID[player.userID]}";
                        SendReply(adminPlayer, message);
                    }
                }
            }
        }

        [ConsoleCommand("cmd.acspec")]
        private void CmdAcspec(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
            {
                var player = arg.Connection.player as BasePlayer;
                if (player.Connection.authLevel == 0 || !permission.UserHasPermission(player.UserIDString, "AntiCheatScience.can.spectate"))
                {
                    SendReply(player, "У Вас нет прав на использование этой команды!");
                    return;
                }
                string action = arg.GetString(0);
                if (action == "stop")
                {
                    arg.Player().SendConsoleCommand("chat.say", "/acspec stop");
                }
            }
        }

        [ConsoleCommand("cmd.sosiban")]
        private void CmdSosiban(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
            {
                var player = arg.Connection.player as BasePlayer;
                if (player.Connection.authLevel == 0 || !permission.UserHasPermission(player.UserIDString, "AntiCheatScience.can.spectate"))
                {
                    SendReply(player, "У Вас нет прав на использование этой команды!");
                    return;
                }
                string id = arg.GetString(0);
                string adminid = arg.GetString(1);
                string reason = arg.GetString(2);
                if (id != null && adminid != null && reason != null)
                {
                    player.Respawn();
                    CuiHelper.DestroyUi(player, _Layer1);
                    if (_config.RustAPP.isRustAPP)
                    {
                        if (_config.PilotFire.banMeraAimDrive == 1)
                        {
                            rust.RunServerCommand($"ra.ban {id} \"{reason} [by spec {adminid}]\"");
                        }
                        else if (_config.PilotFire.banMeraAimDrive == 2)
                        {
                            rust.RunServerCommand($"ra.ban {id} \"{reason} [by spec {adminid}]\" --global");
                        }
                        else if (_config.PilotFire.banMeraAimDrive == 3)
                        {
                            rust.RunServerCommand($"ra.ban {id} \"{reason} [by spec {adminid}]\" --ban-ip");
                        }
                        else if (_config.PilotFire.banMeraAimDrive == 4)
                        {
                            rust.RunServerCommand($"ra.ban {id} \"{reason} [by spec {adminid}]\" --ban-ip --global");
                        }
                    }
                    if (!_config.RustAPP.isRustAPPbanOnly)
                    {
                        string commanda = _config.Global.BanCommand.Replace("%steamid%", id).Replace("%reason%", $"{reason} [by spec {adminid}]");
                        rust.RunServerCommand($"{commanda}");
                    }
                }
            }
        }

        [ChatCommand("acspec")]
        void SpectateCommand(BasePlayer player, string command, string[] args)
        {
            if (player.Connection.authLevel == 0 || !permission.UserHasPermission(player.UserIDString, "AntiCheatScience.can.spectate"))
            {
                SendReply(player, "У Вас нет прав на использование этой команды!");
                return;
            }

            if (args.Length == 0 || args.Length > 1)
            {
                SendReply(player, "Использование: /acspec Уникальный айди или stop для того что бы остановить");
                return;
            }

            if (args[0].Contains("stop"))
            {
                StopSpec(player);
                return;
            }

            string uniqueId = args[0];
            ulong targetUserId = 0;
            bool found = false;

            foreach (KeyValuePair<ulong, string> entry in unicalID)
            {
                if (entry.Value == uniqueId)
                {
                    targetUserId = entry.Key;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                SendReply(player, "Игрок с указанным уникальным айди не найден.");
                return;
            }

            BasePlayer targetPlayer = BasePlayer.FindByID(targetUserId);
            if (targetPlayer != null)
            {
                StartSpec(player, targetPlayer);
                SpecMenu(player, targetPlayer);
                /*string message = $"Админ <color=yellow>{player.displayName}</color> начал слежку за: <color=red>{targetPlayer.displayName}</color>, с уникальным айди: <color=red>{unicalID[player.userID]}</color>\nНачните слежку по команде: /acspec {unicalID[player.userID]}";
                SendReply(player, message);*/
            }
            else
            {
                SendReply(player, "Игрок с указанным уникальным айди не найден в сети.");
            }
        }

        void StartSpec(BasePlayer player, BasePlayer target)
        {
            if (!player.IsSpectating())
            {
                player.StartSpectating();
                player.UpdateSpectateTarget(target.displayName);
            }
            else
            {
                player.UpdateSpectateTarget(target.displayName);
            }
        }

        void StopSpec(BasePlayer player)
        {
            if (player.IsSpectating())
            {
                player.StopSpectating();
                string message = $"Вы закончили слежку.";
                SendReply(player, message);
                player.Respawn();
                CuiHelper.DestroyUi(player, _Layer1);
            }
            else
            {
                string message = $"Вы не следите.";
                SendReply(player, message);
            }
        }

        Dictionary<string, Vector3> _PlayerEyes = new Dictionary<string, Vector3>();
        const float TickPadding = 0.1f;

        private void SimulateProjectile(ref Vector3 position, ref Vector3 velocity, ref float partialTime, float travelTime, Vector3 gravity, float drag, out Vector3 prevPosition, out Vector3 prevVelocity)
        {
            float chsilo = 0.03125f;
            prevPosition = position;
            prevVelocity = velocity;
            if (partialTime > Mathf.Epsilon)
            {
                float chsilo2 = chsilo - partialTime;
                if (travelTime < chsilo2)
                {
                    prevPosition = position;
                    prevVelocity = velocity;
                    position += velocity * travelTime;
                    partialTime += travelTime;
                    return;
                }
                prevPosition = position;
                prevVelocity = velocity;
                position += velocity * chsilo2;
                velocity += gravity * chsilo;
                velocity -= velocity * (drag * chsilo);
                travelTime -= chsilo2;
            }
            int chsilo3 = Mathf.FloorToInt(travelTime / chsilo);
            for (int i = 0; i < chsilo3; i++)
            {
                prevPosition = position;
                prevVelocity = velocity;
                position += velocity * chsilo;
                velocity += gravity * chsilo;
                velocity -= velocity * (drag * chsilo);
            }
            partialTime = travelTime - chsilo * (float)chsilo3;
            if (partialTime > Mathf.Epsilon)
            {
                prevPosition = position;
                prevVelocity = velocity;
                position += velocity * partialTime;
            }
        }

        private int SAimDetect = 0;
        object OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (info?.HitEntity == null || attacker == null)
                return null;

            if (!(info.HitEntity is BasePlayer victimBP) || !(info.Initiator is BasePlayer))
                return null;

            if (victimBP == null || info.HitEntity.IsNpc)
                return null;

            if (!AimLockNaProverke.ContainsKey(attacker.userID))
            {
                AimLockNaProverke.Add(attacker.userID, false);
            }

            if (!AimLockShot.ContainsKey(attacker.userID))
            {
                AimLockShot.Add(attacker.userID, 0);
            }

            if (AimLockShot.ContainsKey(attacker.userID) && AimLockNaProverke[attacker.userID])
            {
                AimLockShot[attacker.userID]++;
            }

            if (MultiFighting != null)
            {
                if ((bool)MultiFighting.CallHook("IsSteam", attacker.Connection))
                    return null;
            }

            if (Battles != null)
            {
                if ((bool)Battles.CallHook("IsPlayerOnBattle", attacker.userID))
                    return null;
            }

            if (AimTrain != null)
            {
                if ((bool)AimTrain.CallHook("IsAimTraining", attacker.userID))
                    return null;
            }

            string steamID = attacker.userID.ToString();

            if (_PlayerEyes.TryGetValue(steamID, out Vector3 startPos))
            {
                if (attacker.firedProjectiles.TryGetValue(info.ProjectileID, out BasePlayer.FiredProjectile firedProjectile))
                {
                    if (firedProjectile.protection > 0)
                    {
                        if (firedProjectile.protection >= 4)
                        {
                            if (info.HitEntity is BasePlayer)
                            {
                                if (attacker.GetParentEntity() is CargoShip || attacker.GetParentEntity() is BaseBoat || attacker.GetParentEntity() is Tugboat || attacker.GetParentEntity() is ScrapTransportHelicopter || attacker.GetParentEntity() is Minicopter || attacker.GetParentEntity() is MiningQuarry)
                                    return null;

                                Vector3 projectileVelocity = info.ProjectileVelocity;
                                bool isLowSpeed = projectileVelocity.magnitude < 100f;
                                Vector3 nextProjPos = startPos + projectileVelocity * (isLowSpeed ? 0.008f : 0.005f);
                                Vector3 realDirection = startPos + attacker.eyes.BodyForward() * projectileVelocity.magnitude * (isLowSpeed ? 0.008f : 0.005f);
                                float pointDist = Vector3.Distance(realDirection, nextProjPos);
                                float hitDist = Vector3.Distance(startPos, info.HitPositionWorld);
                                BasePlayer player = info.HitEntity as BasePlayer;
                                float maxAngle = (attacker.GetMounted() != null ? attacker.desyncTimeClamped * 0.12f : 0f) + attacker.desyncTimeClamped * 0.12f + (player.modelState.sprinting ? attacker.desyncTimeClamped * (hitDist > 15f ? 0.005f : 0.11f) : 0f)
                                    + (attacker.modelState.sprinting ? attacker.desyncTimeClamped * 0.005f : 0f) - (isLowSpeed ? 0f : hitDist * 0.0002f);

                                string weaponM = info.Weapon?.GetItem()?.info.shortname;
                                if (Vector3.Distance(attacker.transform.position, info.HitPositionWorld) > 170f || Vector3.Distance(attacker.transform.position, info.HitPositionWorld) < 5f)
                                    return null;

                                BaseProjectile weapon = info.Weapon as BaseProjectile;
                                if (weaponM.Contains("rifle.ak") || weaponM.Contains("rifle.lr300") || weaponM.Contains("hmlmg") || weaponM.Contains("lmg.m249"))
                                {
                                    var attachments = weapon.GetItem()?.contents?.itemList;
                                    if (attachments != null)
                                    {
                                        foreach (Item mod in attachments)
                                        {
                                            if (mod.info.shortname.ToLower().Contains("small.scope") || mod.info.shortname.ToLower().Contains("8x.scope"))
                                            {
                                                return null;
                                            }
                                        }
                                    }
                                }

                                if (pointDist > maxAngle + 0.675f)
                                {
                                    attacker.stats.combat.LogInvalid(info, "Урон заблокирован АнтиЧитом");
                                    SAimDetect++;
                                    DetectNo11(attacker, SAimDetect, false);
                                    if (SAimDetect == 1)
                                    {
                                        timer.Once(15f, () =>
                                        {
                                            SAimDetect = 0;
                                        });
                                    }

                                    if (SAimDetect == _config.SilentAim.maxDetect)
                                    {
                                        DetectNo11(attacker, SAimDetect, true);
                                        if (_config.SilentAim.isSAimBan)
                                        {
                                            if (_config.RustAPP.isRustAPP)
                                            {
                                                if (_config.SilentAim.banMeraSAim == 1)
                                                {
                                                    rust.RunServerCommand($"ra.ban {attacker.userID.ToString()} \"{_config.SilentAim.NameSAim} [AntiCheatScience]\"");
                                                }
                                                else if (_config.SilentAim.banMeraSAim == 2)
                                                {
                                                    rust.RunServerCommand($"ra.ban {attacker.userID.ToString()} \"{_config.SilentAim.NameSAim} [AntiCheatScience]\" --global");
                                                }
                                                else if (_config.SilentAim.banMeraSAim == 3)
                                                {
                                                    rust.RunServerCommand($"ra.ban {attacker.userID.ToString()} \"{_config.SilentAim.NameSAim} [AntiCheatScience]\" --ban-ip");
                                                }
                                                else if (_config.SilentAim.banMeraSAim == 4)
                                                {
                                                    rust.RunServerCommand($"ra.ban {attacker.userID.ToString()} \"{_config.SilentAim.NameSAim} [AntiCheatScience]\" --ban-ip --global");
                                                }
                                            }
                                            if (!_config.RustAPP.isRustAPPbanOnly)
                                            {
                                                string commanda = _config.Global.BanCommand.Replace("%steamid%", attacker.userID.ToString()).Replace("%reason%", $"{_config.SilentAim.NameSAim} [AntiCheatScience]");
                                                rust.RunServerCommand($"{commanda}");
                                            }

                                            if (!_config.RustAPP.isRustAPPbanOnly)
                                            {
                                                string commanda = _config.Global.BanCommand.Replace("%steamid%", attacker.userID.ToString()).Replace("%reason%", $"{_config.SilentAim.NameSAim} [AntiCheatScience]");
                                                rust.RunServerCommand(commanda);
                                            }
                                        }
                                        else
                                        {
                                            string commanda = _config.Global.KickCommand.Replace("%steamid%", attacker.userID.ToString()).Replace("%reason%", $"{_config.SilentAim.NameSAim} [AntiCheatScience]");
                                            rust.RunServerCommand(commanda);
                                        }
                                    }

                                    if (_config.Global.isAimBlocDamage)
                                    {
                                        return true;
                                    }

                                    return null;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                if (!_PlayerEyes.ContainsKey(steamID))
                {
                    _PlayerEyes.Add(steamID, Vector3.zero);
                }
            }

            return null;
        }

        void PilotFire(BasePlayer attacker)
        {
            if (attacker.GetParentEntity() is BaseBoat || attacker.GetParentEntity() is Horse || attacker.GetParentEntity() is RidableHorse || attacker.GetParentEntity() is HorseCorpse)
                return;
            DetectNo2(attacker);
            if (_config.RustAPP.isRustAPP)
            {
                if (_config.PilotFire.banMeraAimDrive == 1)
                {
                    rust.RunServerCommand($"ra.ban {attacker.userID.ToString()} \"{_config.PilotFire.NameAimDrive} [AntiCheatScience]\"");
                }
                else if (_config.PilotFire.banMeraAimDrive == 2)
                {
                    rust.RunServerCommand($"ra.ban {attacker.userID.ToString()} \"{_config.PilotFire.NameAimDrive} [AntiCheatScience]\" --global");
                }
                else if (_config.PilotFire.banMeraAimDrive == 3)
                {
                    rust.RunServerCommand($"ra.ban {attacker.userID.ToString()} \"{_config.PilotFire.NameAimDrive} [AntiCheatScience]\" --ban-ip");
                }
                else if (_config.PilotFire.banMeraAimDrive == 4)
                {
                    rust.RunServerCommand($"ra.ban {attacker.userID.ToString()} \"{_config.PilotFire.NameAimDrive} [AntiCheatScience]\" --ban-ip --global");
                }
            }
            if (!_config.RustAPP.isRustAPPbanOnly)
            {
                string commanda = _config.Global.BanCommand.Replace("%steamid%", attacker.userID.ToString()).Replace("%reason%", $"{_config.PilotFire.NameAimDrive} [AntiCheatScience]");
                rust.RunServerCommand($"{commanda}");
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (_PlayerEyes.ContainsKey(player.userID.ToString()))
                _PlayerEyes.Remove(player.userID.ToString());
        }

        void NoFallDamage(BasePlayer player)
        {
            if (player.GetParentEntity() is BaseLadder)
                return;
            DetectNo12(player);
            if (_config.RustAPP.isRustAPP)
            {
                if (_config.NoFallDamage.banMeraNFD == 1)
                {
                    rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.NoFallDamage.NameNFD} [AntiCheatScience]\"");
                }
                else if (_config.NoFallDamage.banMeraNFD == 2)
                {
                    rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.NoFallDamage.NameNFD} [AntiCheatScience]\" --global");
                }
                else if (_config.NoFallDamage.banMeraNFD == 3)
                {
                    rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.NoFallDamage.NameNFD} [AntiCheatScience]\" --ban-ip");
                }
                else if (_config.NoFallDamage.banMeraNFD == 4)
                {
                    rust.RunServerCommand($"ra.ban {player.userID.ToString()} \"{_config.NoFallDamage.NameNFD} [AntiCheatScience]\" --ban-ip --global");
                }
            }
            if (!_config.RustAPP.isRustAPPbanOnly)
            {
                string commanda = _config.Global.BanCommand.Replace("%steamid%", player.userID.ToString()).Replace("%reason%", $"{_config.NoFallDamage.NameNFD} [AntiCheatScience]");
                rust.RunServerCommand($"{commanda}");
            }
        }
    }
}