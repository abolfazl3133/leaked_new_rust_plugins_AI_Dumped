using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Random = UnityEngine.Random;
using Oxide.Core;
using ConVar;
using ru = Oxide.Game.Rust;
using Oxide.Core.Plugins;
using System.Collections;

namespace Oxide.Plugins
{
    [Info("XRate", "https://discord.gg/TrJ7jnS233", "2.0.1")]
    [Description("НАСТРОЙКА РЕЙТОВ ДОБЫЧИ (ОПТИМИЗИРОВАНО)")]
    public class XRate : RustPlugin
    {
        const bool fermensEN = false;

        #region Config
        private PluginConfig config;
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

        class boxes
        {
            [JsonProperty(fermensEN ? "Helicopter crates" : "С вертолета")]
            public float heli;

            [JsonProperty(fermensEN ? "Bradley crates" : "С танка")]
            public float tank;

            [JsonProperty(fermensEN ? "Locked crates" : "Закрытые")]
            public float loke;

            [JsonProperty(fermensEN ? "Supply crates" : "Аир дроп")]
            public float aird;

            [JsonProperty(fermensEN ? "Elite crates" : "Элитные")]
            public float elit;

            [JsonProperty(fermensEN ? "Regular crates" : "Обычные")]
            public float simp;

            [JsonProperty(fermensEN ? "Barrels" : "Бочки")]
            public float bare;
        }

        class rateset
        {
            [JsonProperty(fermensEN ? "Collectible & growable" : "Поднимаемые ресурсы")]
            public float grab;

            [JsonProperty(fermensEN ? "Gather" : "Добываемые ресурсы")]
            public float gather;

            [JsonProperty(fermensEN ? "Sulfur" : "Сульфур")]
            public float sulfur;

            [JsonProperty(fermensEN ? "Quarry" : "С карьера")]
            public float carier;

            [JsonProperty(fermensEN ? "Crates & barrels" : "С ящиков/бочек v2")]
            public boxes containers;

            [JsonProperty(fermensEN ? "Scientists" : "С ученых")]
            public float npc;

            [JsonProperty(fermensEN ? "Melting speed" : "Скорость переплавки")]
            public float speed;
        }

        class daynight
        {
            [JsonProperty(fermensEN ? "Enable?" : "Включить?")]
            public bool enable;

            [JsonProperty(fermensEN ? "Night length" : "Длина ночи")]
            public float night;

            [JsonProperty(fermensEN ? "Day length" : "Длина дня")]
            public float day;

            [JsonProperty(fermensEN ? "Autoskip night" : "Автопропуск ночи")]
            public bool skipnight;

            [JsonProperty(fermensEN ? "Voteskip night" : "Голосование за пропуск ночи")]
            public bool vote;

            [JsonProperty(fermensEN ? "Nightly increase in rates (ex. 1.0 - increase by 100% , 0 - disable)" : "Ночное увелечение рейтов (прим. 1.0 - на 100%, 0 - выключить)")]
            public float upnight;
        }

        private class PluginConfig
        {
            [JsonProperty(fermensEN ? "Experimental. Do not touch" : "Экспериментально. Не трогать!")]
            public bool exp;

            [JsonProperty(fermensEN ? "Disable accelerated melting" : "Отключить ускоренную плавку")]
            public bool speed;

            [JsonProperty(fermensEN ? "Furnace prefabs (where accelerated smelting will work)" : "Префабы печек (где будет работать ускоренная плавка)")]
            public List<string> prefabs;

            [JsonProperty(fermensEN ? "Default rates" : "Рейты у обычных игроков")]
            public rateset rates;

            [JsonProperty(fermensEN ? "Adjusting the length of day and night" : "Настройка дня и ночи")]
            public daynight daynight;

            [JsonProperty(fermensEN ? "Messages []" : "Сообщения []")]
            public Dictionary<string, string> messages { get; set; } = new Dictionary<string, string>
            {
                { "NightHasCome", fermensEN ? "<size=15><color=#ccff33>Night has fallen</color>, gather and loot rates increased by <color=#ccff33>{num}%</color>!</size>\n<size=10><color =#ccff33>/rate</color> - find out your current rates.</size>" : "<size=15><color=#ccff33>Наступила ночь</color>, рейты добычи увеличены на <color=#ccff33>{num}%</color>!</size>\n<size=10><color=#ccff33>/rate</color> - узнать текущие ваши рейты.</size>" },
                { "DayHasCome", fermensEN ? "<size=15><color=#ccff33>The day has come</color>, gather and loot rates are back!</size>\n<size=10><color=#ccff33>/rate</color> - find out your current rates.</size>" : "<size=15><color=#ccff33>Наступил день</color>, рейты добычи стали прежними!</size>\n<size=10><color=#ccff33>/rate</color> - узнать текущие ваши рейты.</size>" },
                { "INFORMATION", fermensEN ? "<color=#ccff33>INFORMATION | {name}</color>\nPick up: x<color=#F0E68C>{0}</color>\nGather: x<color=#F0E68C>{1}</color> <size=10>(sulfur: x <color=#F0E68C>{6}</color>)</size>\nQuarry: x<color=#F0E68C>{2}</color>\nCrates/barrels: x<color=#F0E68C>{3} </color>\nLoot from the scientist: x<color=#F0E68C>{4}</color>\nSmelting Speed: x<color=#F0E68C>{5}</color>" : "<color=#ccff33>INFORATE | {name}</color>\nПоднимаемые: x<color=#F0E68C>{0}</color>\nДобываемые: x<color=#F0E68C>{1}</color> <size=10>(cульфур: x<color=#F0E68C>{6}</color>)</size>\nКарьер: x<color=#F0E68C>{2}</color>\nЯщики/бочки: x<color=#F0E68C>{3}</color>\nNPC: x<color=#F0E68C>{4}</color>\nСкорость переплавки: x<color=#F0E68C>{5}</color>"},
                { "SkipNight", fermensEN ? "<color=yellow>The majority voted for the day. Let's skip the night...</color>" : "<color=yellow>Большинство проголосовало за день. Пропускаем ночь...</color>" },
                { "NoSkipNight", fermensEN ?"<color=yellow>—Let there be light! - said the electrician and cut the wires.</color>" : "<color=yellow>— Да будет свет! — сказал электрик и перерезал провода.</color>" },
                { "NoActive", fermensEN ? "<color=yellow>VOTING IS NOT ACTIVE!</color>" : "<color=yellow>ГОЛОСОВАНИЕ НЕ АКТИВНО!</color>" },
                { "Voted", fermensEN ? "<color=yellow>YOU ALREADY VOTE!</color>" : "<color=yellow>ВЫ УЖЕ ГОЛОСОВАЛИ!</color>" },
                { "Night", fermensEN ? "<color=yellow>Vote for NIGHT successfully received.</color>" : "<color=yellow>Голос за НОЧЬ успешно принят.</color>" },
                { "Day", fermensEN ? "<color=yellow>Vote for the DAY successfully received.</color>" : "<color=yellow>Голос за ДЕНЬ успешно принят.</color>" }
            };

            [JsonProperty(fermensEN ? "Premium rates [permission|setting]" : "Привилегии")]
            public Dictionary<string, rateset> privilige;

            [JsonProperty(fermensEN ? "Blacklist, for what won't work" : "На что не увеличивать рейты?")]
            public string[] blacklist;

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    speed = false,
                    privilige = new Dictionary<string, rateset>()
                    {
                        { "xrate.x3", new rateset{ carier = 3f, gather = 3f, grab = 3f, npc = 3f, speed = 4f, sulfur = 2.5f, containers = new boxes{ tank = 3f, simp = 3f, loke = 3f, heli = 3f, elit =3f, bare = 3f,aird = 3f } } },
                        { "xrate.x4", new rateset{ carier = 4f, gather = 4f, grab = 4f, npc = 4f, speed = 4f, sulfur = 2.5f, containers = new boxes{ tank = 4f, simp = 4f, loke = 4f, heli = 4f, elit =4f, bare = 4f,aird = 4f } } }
                    },
                    rates = new rateset { carier = 2f, gather = 2f, grab = 2f, npc = 2f, speed = 2f, sulfur = 2f, containers = new boxes { tank = 2f, simp = 2f, loke = 2f, heli = 2f, elit = 2f, bare = 2f, aird = 2f } },
                    daynight = new daynight
                    {
                        day = 50f,
                        night = 10f,
                        enable = true,
                        skipnight = false,
                        upnight = 0f,
                        vote = false
                    },
                    exp = false,
                    blacklist = new string[]
                    {
                        "sticks",
                        "flare"
                    },
                    prefabs = _prefabs,
                };
            }
        }
        private static List<string> _prefabs = new List<string> { { "furnace" }, { "furnace.large" }, { "refinery_small_deployed" }, { "electricfurnace.deployed" } };
        #endregion

        #region getrate
        Dictionary<string, rateset> cash = new Dictionary<string, rateset>();
        Dictionary<ulong, float> cashcariers = new Dictionary<ulong, float>();
        static XRate ins;
        void Init()
        {
            ins = this;
            Unsubscribe(nameof(OnFuelConsume));
            Unsubscribe(nameof(OnOvenToggle));
        }

        bool skip;
        bool isday;
        void OnHour()
        {
            if (TOD_Sky.Instance.Cycle.Hour > TOD_Sky.Instance.SunriseTime && TOD_Sky.Instance.Cycle.Hour <= 19f && !isday) OnSunrise();
            else if ((TOD_Sky.Instance.Cycle.Hour >= 19f || TOD_Sky.Instance.Cycle.Hour < TOD_Sky.Instance.SunriseTime) && isday) OnSunset();
        }

        void OnSunrise()
        {
            TOD_Sky.Instance.Components.Time.DayLengthInMinutes = daytime;
            isday = true;
            if (upnight > 1f)
            {
                Server.Broadcast(config.messages["DayHasCome"]);
                nightupdate();
            }
        }

        #region ГОЛОСОВАНИЕ
        const string REFRESHGUI = "[{\"name\":\"daytext\",\"parent\":\"day\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{day}\",\"fontSize\":18,\"align\":\"MiddleCenter\",\"color\":\"1 1 1 0.7921728\",\"fadeIn\":0.5},{\"type\":\"UnityEngine.UI.Outline\",\"color\":\"0 0 0 0.392941\",\"distance\":\"0.5 0.5\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmax\":\"0 0\"}]},{\"name\":\"neighttext\",\"parent\":\"night\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{night}\",\"fontSize\":18,\"align\":\"MiddleCenter\",\"color\":\"1 1 1 0.7921569\",\"fadeIn\":0.5},{\"type\":\"UnityEngine.UI.Outline\",\"color\":\"0 0 0 0.3948711\",\"distance\":\"0.5 0.5\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmax\":\"0 0\"}]}]";
        const string GUI = "[{\"name\":\"Main\",\"parent\":\"Overlay\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0 0 0 0.2035446\",\"fadeIn\":0.5},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 1\",\"anchormax\":\"0.5 1\",\"offsetmin\":\"-100 -65\",\"offsetmax\":\"100 -35\"}]},{\"name\":\"day\",\"parent\":\"Main\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"chat.say /voteday\",\"color\":\"1 1 1 0.3929416\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"0.5 1\",\"offsetmax\":\"0 0\"}]},{\"name\":\"daytext\",\"parent\":\"day\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{day}\",\"fontSize\":18,\"align\":\"MiddleCenter\",\"color\":\"1 1 1 0.7921728\",\"fadeIn\":0.5},{\"type\":\"UnityEngine.UI.Outline\",\"color\":\"0 0 0 0.392941\",\"distance\":\"0.5 0.5\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmax\":\"0 0\"}]},{\"name\":\"night\",\"parent\":\"Main\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"chat.say /votenight\",\"color\":\"0 0 0 0.3929408\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0\",\"anchormax\":\"1 1\",\"offsetmax\":\"0 0\"}]},{\"name\":\"neighttext\",\"parent\":\"night\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{night}\",\"fontSize\":18,\"align\":\"MiddleCenter\",\"color\":\"1 1 1 0.7921569\",\"fadeIn\":0.5},{\"type\":\"UnityEngine.UI.Outline\",\"color\":\"0 0 0 0.3948711\",\"distance\":\"0.5 0.5\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmax\":\"0 0\"}]}]";
        static string CONSTVOTE = "";

        void CLEARVOTE()
        {
            Vtimer?.Destroy();
            Vday = 0;
            Vnight = 0;
            voted.Clear();
        }

        void StartVote()
        {
            activevote = true;
            CLEARVOTE();
            Debug.LogWarning(fermensEN ? "-Vote to skip the night-" : "-Голосование за пропуск ночи-");
            Server.Broadcast(fermensEN ? "<color=yellow>Voting for skipping the night has begun. Click on DAY or NIGHT or write in the chat /voteday - for the day or /votenight - for the night.</color>" : "<color=yellow>Начато голосование за пропуск ночи. Нажмите на ДЕНЬ или НОЧЬ или пропишите в чат /voteday - за день или /votenight - за ночь.</color>");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = Network.Net.sv.connections }, null, "DestroyUI", "Main");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = Network.Net.sv.connections }, null, "AddUI", CONSTVOTE);
            Vtimer = timer.Once(30f, () => EndVote());
        }

        void EndVote()
        {
            activevote = false;
            if (Vday > Vnight)
            {
                TOD_Sky.Instance.Cycle.Hour += (24 - TOD_Sky.Instance.Cycle.Hour) + TOD_Sky.Instance.SunriseTime;
                OnSunrise();
                Server.Broadcast(config.messages["SkipNight"], 1);
                Debug.LogWarning(fermensEN ? "-Skip the night-" : "-Пропускаем ночь-");
            }
            else
            {
                Debug.LogWarning(fermensEN ? "-The night remains-" : "-Ночь остается-");
                Server.Broadcast(config.messages["NoSkipNight"], 1);
            }
            CLEARVOTE();
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = Network.Net.sv.connections }, null, "DestroyUI", "Main");
        }

        Timer Vtimer;
        bool activevote;
        static int Vday;
        static int Vnight;
        static List<ulong> voted = new List<ulong>();

        private void REFRESHME()
        {
            List<Network.Connection> sendto = Network.Net.sv.connections.Where(x => voted.Contains(x.userid)).ToList();
            string RGUI = REFRESHGUI.Replace("{day}", Vday.ToString()).Replace("{night}", Vnight.ToString());
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = sendto }, null, "DestroyUI", "daytext");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = sendto }, null, "DestroyUI", "neighttext");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = sendto }, null, "AddUI", RGUI);
        }

        private void cmdvoteday(BasePlayer player, string command, string[] args)
        {
            if (!CHECKPOINT(player)) return;

            player.ChatMessage(config.messages["Day"]);
            Vday++;
            voted.Add(player.userID);
            REFRESHME();
            if (Vday > BasePlayer.activePlayerList.Count * 0.6f) EndVote();
        }

        private void cmdvotenight(BasePlayer player, string command, string[] args)
        {
            if (!CHECKPOINT(player)) return;

            player.ChatMessage(config.messages["Night"]);
            Vnight++;
            voted.Add(player.userID);
            REFRESHME();
            if (Vnight > BasePlayer.activePlayerList.Count * 0.6f) EndVote();
        }

        bool CHECKPOINT(BasePlayer player)
        {
            if (!activevote)
            {
                player.ChatMessage(config.messages["NoActive"]);
                return false;
            }

            if (voted.Contains(player.userID))
            {
                player.ChatMessage(config.messages["Voted"]);
                return false;
            }

            return true;
        }
        #endregion

        void OnSunset()
        {
            if (skip) return;
            if (config.daynight.skipnight)
            {
                Env.time = 23.99f;
                skip = true;
                timer.Once(8f, () =>
                {
                    Env.time = TOD_Sky.Instance.SunriseTime;
                    skip = false;
                });
                Debug.Log(fermensEN ? "Skip night" : "Пропускаем ночь.");
                return;
            }
            else if (config.daynight.vote) StartVote();

            TOD_Sky.Instance.Components.Time.DayLengthInMinutes = nighttime;
            isday = false;
            if (upnight > 1f)
            {
                Server.Broadcast(config.messages["NightHasCome"].Replace("{num}", (config.daynight.upnight * 100f).ToString()));
                nightupdate();
            }
        }

        void nightupdate()
        {
            if (cash.Count > 0) foreach (var id in cash.ToList()) getuserrate(id.Key);
        }

        float daytime;
        float nighttime;
        float upnight;
        TOD_Time comp;


        void OnServerInitialized()
        {
            if (!config.prefabs.Contains("electricfurnace.deployed"))
            {
                config.prefabs.Add("electricfurnace.deployed");
            }
            if (!config.prefabs.Contains("refinery_small_deployed"))
            {
                config.prefabs.Add("refinery_small_deployed");
            }
            SaveConfig();
            permission.RegisterPermission("xrate.instant", this);
            if (config.daynight.enable)
            {
                if (config.daynight.vote)
                {
                    CONSTVOTE = GUI.Replace("{day}", fermensEN ? "DAY" : "ДЕНЬ").Replace("{night}", fermensEN ? "NIGHT" : "НОЧЬ");
                    Interface.Oxide.GetLibrary<ru.Libraries.Command>(null).AddChatCommand("voteday", this, "cmdvoteday");
                    Interface.Oxide.GetLibrary<ru.Libraries.Command>(null).AddChatCommand("votenight", this, "cmdvotenight");
                }
                daytime = config.daynight.day * 24f / (19f - TOD_Sky.Instance.SunriseTime);
                nighttime = config.daynight.night * 24f / (24f - (19f - TOD_Sky.Instance.SunriseTime));
                upnight = 1f + config.daynight.upnight;
                comp = TOD_Sky.Instance.Components.Time;
                comp.ProgressTime = true;
                comp.UseTimeCurve = false;
                comp.OnSunrise += OnSunrise;
                comp.OnSunset += OnSunset;
                comp.OnHour += OnHour;

                if (TOD_Sky.Instance.Cycle.Hour > TOD_Sky.Instance.SunriseTime && TOD_Sky.Instance.Cycle.Hour <= 19f) OnSunrise();
                else OnSunset();
            }

            if (!config.speed) ServerMgr.Instance.StartCoroutine(Initializating());

            foreach (string perm in config.privilige.Keys) permission.RegisterPermission(perm, this);
            foreach (BasePlayer player in BasePlayer.activePlayerList) getuserrate(player.UserIDString);
        }

        IEnumerator Initializating()
        {
            yield return CoroutineEx.waitForSeconds(1f);

            Subscribe(nameof(OnOvenToggle));
            Subscribe(nameof(OnFuelConsume));

            yield break;
        }

        void OnGroupPermissionGranted(string name, string perm)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (permission.UserHasGroup(player.UserIDString, name)) getuserrate(player.UserIDString);
            }
        }

        void OnGroupPermissionRevoked(string name, string perm)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (permission.UserHasGroup(player.UserIDString, name)) getuserrate(player.UserIDString);
            }
        }

        void OnUserGroupRemoved(string id, string groupName)
        {
            getuserrate(id);
        }

        void OnUserGroupAdded(string id, string groupName)
        {
            getuserrate(id);
        }

        void OnUserPermissionGranted(string id, string permName)
        {
            getuserrate(id);
        }

        void OnUserPermissionRevoked(string id, string permName)
        {
            getuserrate(id);
        }

        void OnPlayerConnected(BasePlayer player)
        {
            getuserrate(player.UserIDString);
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (cash.ContainsKey(player.UserIDString)) cash.Remove(player.UserIDString);
        }
        #endregion

        #region Rates
        [ChatCommand("rate")]
        private void cmdRATE(BasePlayer player, string command, string[] args)
        {
            player.ChatMessage(config.messages["INFORMATION"].Replace("{name}", player.displayName).Replace("{0}", GetRate(player.UserIDString).grab.ToString()).Replace("{1}", GetRate(player.UserIDString).gather.ToString()).Replace("{2}", GetRate(player.UserIDString).carier.ToString()).Replace("{3}", GetRate(player.UserIDString).containers.simp.ToString()).Replace("{4}", GetRate(player.UserIDString).npc.ToString()).Replace("{5}", GetRate(player.UserIDString).speed.ToString()).Replace("{6}", GetRate(player.UserIDString).sulfur.ToString()));
        }

        [PluginReference] private Plugin ZREWARDME, FROre;

        rateset GetRate(string userid)
        {
            rateset rateset;
            if (!cash.TryGetValue(userid, out rateset)) return getuserrate(userid);
            return rateset;
        }

        rateset getuserrate(string id, float bonus = 0f)
        {
            rateset rate = config.privilige.LastOrDefault(x => permission.UserHasPermission(id, x.Key)).Value ?? config.rates;
            if (!cash.ContainsKey(id)) cash[id] = new rateset();
            if (ZREWARDME != null && bonus == 0f) bonus = ZREWARDME.Call<float>("APIBONUS", id);
            if (upnight > 1f && !isday)
            {
                cash[id].carier = rate.carier * upnight;
                cash[id].gather = rate.gather * upnight;
                cash[id].grab = rate.grab * upnight;
                cash[id].containers = new boxes();
                cash[id].containers.bare = rate.containers.bare * upnight;
                cash[id].containers.elit = rate.containers.elit * upnight;
                cash[id].containers.heli = rate.containers.heli * upnight;
                cash[id].containers.loke = rate.containers.loke * upnight;
                cash[id].containers.simp = rate.containers.simp * upnight;
                cash[id].containers.tank = rate.containers.tank * upnight;
                cash[id].containers.aird = rate.containers.aird * upnight;
                cash[id].npc = rate.npc * upnight;
                cash[id].sulfur = rate.sulfur * upnight;
            }
            else
            {
                cash[id].carier = rate.carier;
                cash[id].gather = rate.gather;
                cash[id].grab = rate.grab;
                cash[id].containers = new boxes();
                cash[id].containers.bare = rate.containers.bare;
                cash[id].containers.elit = rate.containers.elit;
                cash[id].containers.heli = rate.containers.heli;
                cash[id].containers.loke = rate.containers.loke;
                cash[id].containers.simp = rate.containers.simp;
                cash[id].containers.tank = rate.containers.tank;
                cash[id].containers.aird = rate.containers.aird;
                cash[id].npc = rate.npc;
                cash[id].sulfur = rate.sulfur;
            }

            if (bonus > 0f)
            {
                cash[id].gather += bonus;
                cash[id].grab += bonus;
                cash[id].sulfur += bonus;
            }

            cash[id].speed = rate.speed;
            return cash[id];
        }

        void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
            if (collectible == null || collectible.itemList.Count() == 0) return;

            foreach (var item in collectible.itemList)
            {
                if (item == null) continue;
                if (config.blacklist.Contains(item.itemDef.shortname)) return;
                item.amount = (int)(item.amount * GetRate(player.UserIDString).grab);
            }
        }

        void OnGrowableGather(GrowableEntity plant, Item item, BasePlayer player)
        {
            if (player == null || item == null) return;
            if (config.blacklist.Contains(item.info.shortname)) return;
            item.amount = (int)(item.amount * GetRate(player.UserIDString).grab);
        }

        void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (config.blacklist.Contains(item.info.shortname)) return;
            if (item.info.itemid.Equals(-1157596551)) item.amount = (int)(item.amount * cash[player.UserIDString].sulfur);
            else item.amount = (int)(item.amount * GetRate(player.UserIDString).gather);
        }

        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (config.blacklist.Contains(item.info.shortname)) return;
            BasePlayer player = entity.ToPlayer();
            if (player != null)
            {
                if (item.info.itemid.Equals(-1157596551)) item.amount = (int)(item.amount * GetRate(player.UserIDString).sulfur);
                else item.amount = (int)(item.amount * GetRate(player.UserIDString).gather);
            }
            else
            {
                if (item.info.itemid.Equals(-1157596551)) item.amount *= (int)(item.amount * config.rates.sulfur);
                else item.amount *= (int)(item.amount * config.rates.gather);
            }
        }

        private void CashCarier(ulong id, ulong netid)
        {
            rateset rate = config.privilige.LastOrDefault(x => permission.UserHasPermission(id.ToString(), x.Key)).Value ?? config.rates;
            cashcariers[netid] = rate.carier;
        }

        private object OnExcavatorGather(ExcavatorArm arm, Item item)
        {
            if (config.blacklist.Contains(item.info.shortname)) return null;
            item.amount = (int)(item.amount * config.rates.carier);
            return null;
        }

        private void OnQuarryToggled(BaseEntity entity, BasePlayer player)
        {
            CashCarier(player.userID, entity.net.ID.Value);
        }

        void OnQuarryGather(MiningQuarry quarry, Item item)
        {
            if (config.blacklist.Contains(item.info.shortname)) return;

            float rate;
            if (!cashcariers.TryGetValue(quarry.net.ID.Value, out rate))
            {
                rate = config.rates.carier;
            }

            if (!isday && upnight > 1f)
            {
                item.amount = (int)(item.amount * rate * upnight);
            }
            else
            {
                item.amount = (int)(item.amount * rate);
            }

        }

        void OnContainerDropItems(ItemContainer container)
        {
            LootContainer lootcont = container.entityOwner as LootContainer;
            if (lootcont == null || lootcont.OwnerID != 0) return;
            ulong ID = lootcont.net.ID.Value;
            if (CHECKED.Contains(ID)) return;
            var player = lootcont?.lastAttacker?.ToPlayer();

            if (player != null && cash.ContainsKey(player.UserIDString)) UPRATELOOT(player, lootcont, GetRate(player.UserIDString).containers.bare);
            else UPRATELOOT(player, lootcont, config.rates.containers.bare);
        }

        private void OnEntityDeath(BaseNetworkable entity, HitInfo info)
        {
            if (entity is BaseHelicopter && config.exp)
            {
                HackableLockedCrate ent = (HackableLockedCrate)GameManager.server.CreateEntity("assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab", entity.transform.position, entity.transform.rotation);
                ent.Spawn();
            }
        }

        List<ulong> CHECKED = new List<ulong>();

        private void OnLootEntity(BasePlayer player, object entity)
        {
            if (player == null || entity == null) return;
            if (entity is NPCPlayerCorpse)
            {
                NPCPlayerCorpse nPCPlayerCorpse = (NPCPlayerCorpse)entity;
                if (nPCPlayerCorpse == null) return;
                ulong ID = nPCPlayerCorpse.net.ID.Value;
                if (CHECKED.Contains(ID)) return;
                rateset rateset;
                if (!cash.TryGetValue(player.UserIDString, out rateset)) return;
                ItemContainer cont = nPCPlayerCorpse.containers.FirstOrDefault();
                foreach (var item in cont.itemList.Where(x => x.info.stackable > 1))
                {
                    int maxstack = item.MaxStackable();
                    if (maxstack == 1 || config.blacklist.Contains(item.info.shortname) || item.IsBlueprint()) continue;
                    item.amount = (int)(item.amount * rateset.npc);
                    if (item.amount > maxstack) item.amount = maxstack;
                }
                CHECKED.Add(ID);
            }
            else if (entity is LootContainer)
            {
                LootContainer lootcont = entity as LootContainer;
                if (lootcont == null) return;
                ulong ID = lootcont.net.ID.Value;
                if (CHECKED.Contains(ID)) return;
                //  Debug.Log(lootcont.PrefabName + " " + lootcont.OwnerID);
                rateset rateset;
                if (!cash.TryGetValue(player.UserIDString, out rateset)) return;
                //  Debug.Log(lootcont.PrefabName + " ++");
                if (entity is HackableLockedCrate || entity is LockedByEntCrate)
                {
                    // Debug.Log("-1-");
                    UPRATELOOT(player, lootcont, rateset.containers.loke);
                }
                else if (lootcont.prefabID == 1314849795)
                {
                    // Debug.Log("-2-");
                    UPRATELOOT(player, lootcont, rateset.containers.heli); // верт-ящик
                }
                else if (lootcont.prefabID == 3286607235)
                {
                    // Debug.Log("-3-");
                    UPRATELOOT(player, lootcont, rateset.containers.elit); // элит-ящик
                }
                else if (lootcont.prefabID == 1737870479)
                {
                    //  Debug.Log("-4-");
                    UPRATELOOT(player, lootcont, rateset.containers.tank); // танк-ящик
                }
                else if (entity is SupplyDrop)
                {
                    //  Debug.Log("-5-");
                    UPRATELOOT(player, lootcont, rateset.containers.aird);
                }
                else if (lootcont.OwnerID == 0)
                {
                    //  Debug.Log("-6-");
                    UPRATELOOT(player, lootcont, rateset.containers.simp);
                }

                CHECKED.Add(ID);
            }
        }
        private void UPRATELOOT(BasePlayer player, LootContainer lootContainer, float rateup)
        {
            foreach (var item in lootContainer.inventory.itemList)
            {
                int maxstack = item.MaxStackable();
                if (config.blacklist.Contains(item.info.shortname) || item.IsBlueprint() || maxstack == 1) continue;
                int amount = (int)(item.amount * rateup);
                if (amount < 1) amount = 1;
                else if (amount > maxstack) amount = maxstack;
                item.amount = amount;
            }
        }
        #endregion

        #region Smelt
        private void Unload()
        {
            if (comp != null)
            {
                comp.OnSunrise -= OnSunrise;
                comp.OnSunset -= OnSunset;
                comp.OnHour -= OnHour;
            }

            if (BasePlayer.activePlayerList.Count > 0) CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = Network.Net.sv.connections }, null, "DestroyUI", "Main");

            timer.Once(1f, () => ins = null);
        }

        Dictionary<string, int> DefaultSmeltSpeed = new Dictionary<string, int>
        {
          //  { "bbq.deployed", 15 },
           // { "campfire", 2 },
            { "furnace", 3 },
         //  { "hobobarrel.deployed", 2 },
           // { "lantern.deployed", 1 },
            { "furnace.large", 15 },
            { "refinery_small_deployed", 15 },
         //   { "fireplace.deployed", 2 },
        //    { "tunalight.deployed", 1 },
            { "electricfurnace.deployed", 5 }
        };

        private object OnOvenToggle(StorageContainer st, BasePlayer player)
        {
            var entity = st.GetEntity();

            if (!config.prefabs.Contains(entity.ShortPrefabName)) return null;

            int dspeed;
            if (entity is BaseOven)
            {
                if (DefaultSmeltSpeed.TryGetValue(entity.ShortPrefabName, out dspeed))
                {
                    int number = (int)GetRate(player.UserIDString).speed;

                    BaseOven oven = entity as BaseOven;
                    oven.smeltSpeed = number * dspeed;
                }
            }
            return null;
        }

        object OnFuelConsume(BaseOven oven, Item fuel, ItemModBurnable burnable)
        {
            int dspeed;
            if (DefaultSmeltSpeed.TryGetValue(oven.ShortPrefabName, out dspeed) && oven.smeltSpeed > dspeed)
            {
                int rate = oven.smeltSpeed / dspeed;
                if (fuel.amount <= rate) rate = fuel.amount;

                if (oven.allowByproductCreation && burnable.byproductItem != null && burnable.byproductChance < Random.Range(0.0f, 1f))
                {
                    Item obj = ItemManager.Create(burnable.byproductItem, burnable.byproductAmount * rate);
                    if (!obj.MoveToContainer(oven.inventory))
                    {
                        oven.OvenFull();
                        obj.Drop(oven.inventory.dropPosition, oven.inventory.dropVelocity, new Quaternion());
                    }
                }
                if (fuel.amount <= rate)
                {
                    fuel.Remove();
                }
                else
                {
                    fuel.UseItem(rate);
                    fuel.fuel = burnable.fuelAmount;
                    fuel.MarkDirty();
                }
                return false;
            }
            return null;
        }
        #endregion

        #region Instant
        private void OnPlayerAttack(BasePlayer player, HitInfo hit)
        {
            if (player == null || hit == null || !permission.UserHasPermission(player.UserIDString, "xrate.instant") || hit.HitEntity == null) return;

            if (hit.HitEntity is OreResourceEntity)
            {
                OreResourceEntity oreResourceEntity = hit.HitEntity as OreResourceEntity;
                if (!oreResourceEntity.IsDestroyed && oreResourceEntity._hotSpot != null) oreResourceEntity._hotSpot.Kill();
                oreResourceEntity._hotSpot = null;
                hit.gatherScale = 666;
            }
            else if (hit.HitEntity is TreeEntity)
            {
                (hit.HitEntity as TreeEntity).hasBonusGame = false;
                hit.gatherScale = 666;
            }
            else if (hit.HitEntity.ShortPrefabName != null && (hit.HitEntity.ShortPrefabName.Contains("dead_log") || hit.HitEntity.ShortPrefabName.Contains("driftwood")))
            {
                hit.gatherScale = 666;
            }
        }
        #endregion
    }
}