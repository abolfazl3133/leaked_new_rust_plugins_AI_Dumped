﻿using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using ProtoBuf;
using Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AntiCheat", "WhitePlugins.ru", "2.2.23")]
    class AntiCheat : RustPlugin
    {
        static AntiCheat instance;

        class DataStorage
        {
            public Dictionary<ulong, ADMINDATA> AdminData = new Dictionary<ulong, ADMINDATA>();

            public DataStorage() { }
        }

        class ADMINDATA
        {
            public string Name;
            public bool Check;
        }

        DataStorage adata;
        private DynamicConfigFile AdminData;
        static DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);

        void LoadData()
        {
            try
            {
                PlayersListed = Interface.Oxide.DataFileSystem.ReadObject<
                    Dictionary<ulong, PlayerAntiCheat>
                >("AntiCheat/PlayerAntiCheat");
                adata = Interface
                    .GetMod()
                    .DataFileSystem.ReadObject<DataStorage>("AntiCheat/AdminData");
                if (adata == null)
                {
                    PrintError("AntiCheat/AdminData is null! Create new database");
                    adata = new DataStorage();
                    SaveDataAdmin();
                }
                if (PlayersListed == null)
                {
                    PrintError("AntiCheat/PlayerAntiCheat is null! Create new database");
                    PlayersListed = new Dictionary<ulong, PlayerAntiCheat>();
                    SavePlayerData();
                }
            }
            catch
            {
                adata = new DataStorage();
                PlayersListed = new Dictionary<ulong, PlayerAntiCheat>();
            }
        }

        static int b = 0;
        int DetectCountMacros = 10;
        int DetectPerMacros = 80;
        int DetectCountFSH = 10;
        bool SHEnable = true;
        bool FHEnable = true;
        bool SHEnabled = true;
        bool FHEnabled = true;
        bool MCREnabled = true;
        bool AIMEnabled = true;
        bool EnabledSilentAim = true;
        bool SHKickEnabled = false;
        bool FHKickEnabled = false;
        bool AntiRecoilEnabled = true;
        bool AIMLOCKEnabledBAN = true;
        bool AIMHACKEnabledBAN = true;
        static bool SendsLogs = true;
        float AimPercent = 50;
        float AimPercentOverCount = 40f;
        static bool textureenable = true;
        bool init = true;
        private List<string> ListWeapons = new List<string>() { "rifle.ak", "lmg.m249", };

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadConfigValues();
        }

        private void LoadConfigValues()
        {
            GetConfig("[Основное]", "Включить проверку на СпидХак?", ref SHEnable);
            GetConfig("[Основное]", "Включить проверку на ФлайХак?", ref FHEnable);
            GetConfig("[Макрос]", "Включить проверку на Макрос", ref AntiRecoilEnabled);
            var _ListWeapons = new List<object>() { "rifle.ak" };
            GetConfig("[Макрос]", "Список оружия, на какие действует проверка", ref _ListWeapons);
            ListWeapons = _ListWeapons.Select(p => p.ToString()).ToList();
            GetConfig(
                "[Основное]",
                "Включить автоматический бан за AIMLOCK",
                ref AIMLOCKEnabledBAN
            );
            GetConfig(
                "[Основное]",
                "Включить автоматический бан за AIMHACK",
                ref AIMHACKEnabledBAN
            );
            GetConfig("[Макрос]", "Включить автоматический бан за макрос", ref MCREnabled);
            GetConfig(
                "[Макрос]",
                "Количество детектов для автоматического бана за Макрос:",
                ref DetectCountMacros
            );
            GetConfig(
                "[Макрос]",
                "С какого процента начинать считать проверку на Макрос? (0-100%):",
                ref DetectPerMacros
            );
            GetConfig(
                "[Общее]",
                "Включить бан игроков за SpeedHack (Превышающее количество детектов)",
                ref SHEnabled
            );
            GetConfig(
                "[Основное]",
                "Включить kick игроков за SpeedHack (При каждом детекте)",
                ref SHKickEnabled
            );
            GetConfig(
                "[Общее]",
                "Включить бан игроков за FlyHack (Превышающее количество детектов)",
                ref FHEnabled
            );
            GetConfig(
                "[Основное]",
                "Включить kick игроков за FlyHack (При каждом детекте)",
                ref FHKickEnabled
            );
            GetConfig(
                "[Общее]",
                "Количество детектов для автоматического бана (FlyHack and SpeedHack):",
                ref DetectCountFSH
            );
            GetConfig(
                "[Основное]",
                "Включить отправку детектов в чат (По привилегии)?",
                ref SendsLogs
            );
            GetConfig(
                "[Аим]",
                "Процент попадания в голову для автоматического бана:",
                ref AimPercent
            );
            GetConfig("[Аим]", "Включить автоматический бан за Аим", ref AIMEnabled);
            GetConfig(
                "[Аим]",
                "Количество попаданий для автоматического бана, если процент попадания больше зазначеного в конфиге:",
                ref AimPercentOverCount
            );
            GetConfig("[Аим]", "Включить проверку на SilentAim?", ref EnabledSilentAim);
            GetConfig(
                "[Основное]",
                "Включить проверку на проникновение в текстуры (Пока тестируеться)?",
                ref textureenable
            );
            SaveConfig();
        }

        private void GetConfig<T>(string menu, string Key, ref T var)
        {
            if (Config[menu, Key] != null)
            {
                var = (T)Convert.ChangeType(Config[menu, Key], typeof(T));
            }
            Config[menu, Key] = var;
        }

        static int PlayerLayer = LayerMask.NameToLayer("Player (Server)");
        static int constructionColl = LayerMask.GetMask(new string[] { "Construction" });

        void Loaded()
        {
            LoadConfigValues();
        }

        int raycastCount = 0;

        void OnServerInitialized()
        {
            instance = this;
            LoadData();
            LoadDefaultConfig();
            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
            permission.RegisterPermission("anticheat.toggleadmin", this);
            permission.RegisterPermission("anticheat.sendlogs", this);
            Interface.Oxide
                .GetLibrary<Game.Rust.Libraries.Command>()
                .AddConsoleCommand("aim.check", this, "AimCheck");
            Interface.Oxide
                .GetLibrary<Game.Rust.Libraries.Command>()
                .AddConsoleCommand("aim.server", this, "AimCheckServer");
            Interface.Oxide
                .GetLibrary<Game.Rust.Libraries.Command>()
                .AddConsoleCommand("check.server", this, "CheckServer");
            foreach (var player in BasePlayer.activePlayerList)
                CreateInfo(player);
            init = true;
            timer.Repeat(360, 0, () => SaveAllDate());
        }

        void SaveAllDate()
        {
            if (!init)
                return;
            SavePlayerData();
            SaveDataAdmin();
        }

        [ConsoleCommand("ban.user")]
        private void cmdBan(ConsoleSystem.Arg arg)
        {
            var date = DateTime.Now.ToLocalTime().ToShortDateString();
            if (arg.Args == null || arg.Args.Length < 2)
            {
                arg.ReplyWith("Неверный синтаксис! Используйте ban.user <SteamID> <Причина>");
                return;
            }
            ulong targetId;
            if (!ulong.TryParse(arg.Args[0], out targetId))
            {
                arg.ReplyWith("Нужно ввести SteamID игрока какого хотите забанить");
                return;
            }
            if (!PlayersListed.ContainsKey(targetId))
                PlayersListed.Add(
                    targetId,
                    new PlayerAntiCheat()
                    {
                        Name = "null",
                        Deaths = 0,
                        Killed = 0,
                        Heads = 0,
                        Hits = 0,
                        Banned = true,
                        Date = date,
                        Reason = arg.Args[1],
                        BanCreator = arg.Player() != null ? arg.Player().displayName : "Console"
                    }
                );
            else
            {
                PlayersListed[targetId].Banned = true;
                PlayersListed[targetId].Date = date;
                PlayersListed[targetId].Reason = arg.Args[1];
                PlayersListed[targetId].BanCreator =
                    arg.Player() != null ? arg.Player().displayName : "Console";
            }
            BasePlayer target = BasePlayer.FindByID(targetId);
            if (target != null && target.IsConnected)
            {
                Kick(target, $"Вы были забанены. Причина: {arg.Args[1]}");
            }
            arg.ReplyWith($"{arg.Args[0]} забанен. Причина: {arg.Args[1]}!");
        }

        [ConsoleCommand("banlist")]
        private void BanListedPlayers(ConsoleSystem.Arg arg)
        {
            var bans = PlayersListed
                .Where(p => p.Value.Banned)
                .Select(
                    p =>
                        $"Игрок {p.Value.Name} ({p.Key})- Кто выдал: {p.Value.BanCreator} Дата: {p.Value.Date} Причина: {p.Value.Reason}"
                )
                .ToList();
            if (bans.Count > 0)
                arg.ReplyWith(string.Join("\n ", bans));
            else
                arg.ReplyWith("Список банов пустой");
        }

        [ConsoleCommand("unban.user")]
        private void UnbanCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !arg.Player().IsAdmin)
            {
                return;
            }
            if (arg.Args == null || arg.Args.Length != 1)
            {
                arg.ReplyWith("Неверный синтаксис! Используйте unban.user <SteamID>");
                return;
            }
            ulong target;
            if (!ulong.TryParse(arg.Args[0], out target))
            {
                arg.ReplyWith("Нужно ввести SteamID игрока какого хотите разбанить");
                return;
            }
            if (PlayersListed.ContainsKey(target))
            {
                PlayersListed[target].Banned = false;
                PlayersListed[target].Date = "";
                PlayersListed[target].Reason = "";
            }
            arg.ReplyWith($"{arg.Args[0]} разбанен");
        }

        object OnPlayerAttack(BasePlayer player, HitInfo info)
        {
            if (EnabledSilentAim)
            {
                if (player != null && info.HitEntity != null && info.HitEntity is BasePlayer)
                {
                    float y = Mathf.Abs(info.HitPositionWorld.y - info.HitEntity.CenterPoint().y);
                    if (y > 2f)
                    {
                        var messages = $"Обнаружен SilentAim! Стрельба с {y} м.";
                        PrintWarning(
                            $"[{DateTime.Now.ToShortTimeString()}] - (SilentAim) {player.displayName}({player.UserIDString})| Обнаружен SilentAim! Стрельба с {y} м."
                        );
                        LogToFile(
                            "log",
                            $"[{DateTime.Now.ToShortTimeString()}] - (SilentAim) {player.displayName}({player.UserIDString})| Обнаружен SilentAim! Стрельба с {y} м.",
                            this,
                            true
                        );
                        return true;
                    }
                }
            }
            return null;
        }

        object CanUserLogin(string name, string id, string ip)
        {
            if (PlayersListed.ContainsKey(ulong.Parse(id)))
            {
                if (PlayersListed[ulong.Parse(id)].Banned)
                    return $"Вы забанены на данном сервере!";
            }
            return null;
        }

        public List<BasePlayer> Players => BasePlayer.activePlayerList.ToList();

        public BasePlayer FindById(ulong id, ulong playerid = 1171488)
        {
            foreach (var player in Players)
            {
                if (!id.Equals(player.userID))
                    continue;
                return player;
            }
            return null;
        }

        public bool IsConnected(BasePlayer player) => BasePlayer.activePlayerList.Contains(player);

        public void Kick(BasePlayer player, string reason = "") => player.Kick(reason);

        public bool IsBanned(ulong id) => ServerUsers.Is(id, ServerUsers.UserGroup.Banned);

        public void Ban(ulong id, string reason = "")
        {
            if (IsBanned(id))
                return;
            var player = FindById(id);
            ServerUsers.Set(
                id,
                ServerUsers.UserGroup.Banned,
                player?.displayName ?? "Unknown",
                reason
            );
            ServerUsers.Save();
            if (player != null && IsConnected(player))
                Kick(player, reason);
        }

        private readonly Dictionary<ulong, AimLockData> aimlock =
            new Dictionary<ulong, AimLockData>();

        public class AimLockData
        {
            public int Ticks = 1;
            public string Body = "";
        }

        private bool IsNPC(BasePlayer player)
        {
            if (player == null)
                return false;
            if (player is NPCPlayer)
                return true;
            if (
                !(player.userID >= 76560000000000000L || player.userID <= 0L)
                || player.userID.ToString().Length < 17
            )
                return true;
            return false;
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            try
            {
                if (entity == null || entity?.net?.ID == null || info == null)
                    return;
                var victim = entity as BasePlayer;
                if (victim == null)
                    return;
                if (IsNPC(victim))
                    return;
                if (victim.GetComponent<NPCPlayer>() != null)
                    return;
                if (victim is BasePlayer)
                {
                    if (victim.IsSleeping())
                        return;
                    if (PlayersListed.ContainsKey(victim.userID))
                    {
                        PlayersListed[victim.userID].Deaths += 1;
                    }
                }
                BasePlayer attacker = info.Initiator.ToPlayer();
                if (attacker == null || attacker.GetComponent<NPCPlayer>() != null)
                    return;
                if (IsNPC(attacker))
                    return;
                if (attacker == victim)
                    return;
                if (info?.Initiator is BasePlayer)
                {
                    if (PlayersListed.ContainsKey(attacker.userID))
                        PlayersListed[attacker.userID].Killed += 1;
                }
                double aim = Math.Floor(
                    (
                        PlayersListed[attacker.userID].Heads
                        * 1f
                        / PlayersListed[attacker.userID].Hits
                        * 1f
                    ) * 100f
                );
                double kdr = Math.Round(
                    PlayersListed[attacker.userID].Killed
                        * 1f
                        / PlayersListed[attacker.userID].Deaths
                        * 1f,
                    2
                );
                if (
                    PlayersListed[attacker.userID].Hits > AimPercentOverCount
                    && aim > AimPercent
                    && kdr > 2
                    && AIMEnabled
                )
                {
                    var messages =
                        $"<color=#ffa500>[Античит детект]</color> (AimLock) {attacker.displayName}! Соотношение попаданий в голову {aim}% и КДР - ({kdr}) аномальные!";
                    foreach (var admin in BasePlayer.activePlayerList)
                        SendDetection(admin, messages);
                    if (AIMLOCKEnabledBAN)
                    {
                        Debug.LogWarning(
                            $"[Анти-чит] {attacker.displayName}({attacker.UserIDString}) забанен! Причина: AimLock!"
                        );
                        Ban(attacker.userID, "[Анти-чит] AimLock");
                        LogToFile(
                            "ban",
                            $"[{DateTime.Now.ToShortTimeString()}] - {attacker.displayName}({attacker.UserIDString}) забанен! Соотношение попаданий в голову {aim}% и КДР - ({kdr}) аномальные!",
                            this,
                            true
                        );
                    }
                }
            }
            catch (NullReferenceException) { }
        }

        private double GrabCurrentTime() =>
            DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

        private double LastAttack;

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            try
            {
                if (entity == null || entity?.net?.ID == null)
                    return null;
                BasePlayer attacker = info.Initiator.ToPlayer();
                if (attacker == null)
                    return null;
                if (attacker.GetComponent<NPCPlayer>() != null)
                    return null;
                var victim = entity as BasePlayer;
                if (victim == null)
                    return null;
                if (victim.GetComponent<NPCPlayer>() != null)
                    return null;
                if (IsNPC(attacker) || IsNPC(victim))
                    return null;
                var distance = info.Initiator.Distance(victim.transform.position);
                if (distance > 10)
                {
                    AimLockData bodylock;
                    if (!aimlock.TryGetValue(attacker.userID, out bodylock))
                        aimlock.Add(attacker.userID, bodylock = new AimLockData());
                    var _bodyPart =
                        entity?.skeletonProperties?.FindBone(info.HitBone)?.name?.english ?? "";
                    if (_bodyPart == "")
                        return null;
                    var time = GrabCurrentTime() - LastAttack;
                    if ((time > 30))
                        bodylock.Ticks = 1;
                    if (bodylock.Body == _bodyPart && _bodyPart != "lower spine")
                        bodylock.Ticks++;
                    else
                        bodylock.Ticks = 1;
                    if (bodylock.Ticks > 5)
                    {
                        var messages =
                            $"Обнаружен АимЛок! Обнаружений {bodylock.Ticks} |  {bodylock?.Body ?? ""} | {distance} м.";
                        Debug.LogWarning(
                            $"[Анти-чит] {attacker.displayName}({attacker.UserIDString}) Обнаружен АимЛок! Обнаружений {bodylock.Ticks} |  {bodylock?.Body ?? ""} | {distance} м."
                        );
                        LogToFile(
                            "log",
                            $"[{DateTime.Now.ToShortTimeString()}] - (АимЛок) {attacker.displayName}({attacker.UserIDString})| обнаружений {bodylock.Ticks} |  {bodylock?.Body ?? ""} | {distance} м.",
                            this,
                            true
                        );
                        bodylock.Ticks = 1;
                    }
                    bodylock.Body = _bodyPart;
                    if (PlayersListed.ContainsKey(attacker.userID))
                    {
                        PlayersListed[attacker.userID].Hits++;
                        if (info.isHeadshot)
                            PlayersListed[attacker.userID].Heads++;
                    }
                    double aim = Math.Floor(
                        (PlayersListed[attacker.userID].Heads / PlayersListed[attacker.userID].Hits)
                            * 100f
                    );
                    if (
                        PlayersListed[attacker.userID].Hits > AimPercentOverCount
                        && aim > AimPercent
                        && AIMEnabled
                    )
                    {
                        var messages =
                            $"<color=#ffa500>[Античит детект]</color> AimHack {attacker.displayName}({attacker.UserIDString})! Процент попаданий в голову слишком большой {aim}%";
                        foreach (var admin in BasePlayer.activePlayerList)
                            SendDetection(admin, messages);
                        if (AIMHACKEnabledBAN)
                        {
                            Debug.LogWarning(
                                $"[Анти-чит] {attacker.displayName}({attacker.UserIDString}) забанен! Причина: AimHack!"
                            );
                            Ban(attacker.userID, "[Анти-чит] AimHack");
                            LogToFile(
                                "ban",
                                $"[{DateTime.Now.ToShortTimeString()}] - {attacker.displayName}({attacker.UserIDString}) забанен! Процент попаданий в голову слишком большой {aim}%",
                                this,
                                true
                            );
                        }
                    }
                    LastAttack = GrabCurrentTime();
                }
            }
            catch (NullReferenceException) { }
            return null;
        }

        [ChatCommand("ac")]
        void cmdChatDetect(BasePlayer player, string command, string[] args)
        {
            if (
                !player.IsAdmin
                || !permission.UserHasPermission(player.UserIDString, "anticheat.toggleadmin")
            )
            {
                SendReply(player, "У вас нету привилегии использовать эту команду");
                return;
            }
            if (adata.AdminData.ContainsKey(player.userID))
            {
                if (adata.AdminData[player.userID].Check)
                {
                    adata.AdminData[player.userID].Check = adata.AdminData[player.userID].Check =
                        false;
                    SendReply(player, "Админ дебаг выключен. Вас не детектит.");
                    return;
                }
                else
                {
                    adata.AdminData[player.userID].Check = adata.AdminData[player.userID].Check =
                        true;
                    SendReply(player, "Админ дебаг включен. Вас детектит.");
                }
            }
            else
            {
                SendReply(player, "Вас нету в базе администраторов, пожалуйста перезейдите!");
            }
        }

        [HookMethod("AimCheck")]
        private void AimCheck(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !arg.Player().IsAdmin && !arg.Player())
            {
                return;
            }
            if (arg.Args == null || arg.Args.Length < 1)
            {
                arg.ReplyWith("Неверный синтаксис! Используйте aim.check <SteamID>");
                return;
            }
            if (arg.Args.Length == 1)
            {
                ulong FindPlayer;
                if (!ulong.TryParse(arg.Args[0], out FindPlayer))
                {
                    arg.ReplyWith("Нужно ввести SteamID игрока!");
                    return;
                }
                var check = PlayersListed.ContainsKey(FindPlayer);
                if (check)
                {
                    var target = PlayersListed[FindPlayer];
                    double aim =
                        target.Hits > 0
                            ? target.Heads > 0
                                ? target.Hits / target.Heads * 100
                                : 0
                            : 0;
                    arg.ReplyWith(
                        $"[Анти-чит] {target.Name}: Aim: {aim}% при {target.Hits} попаданиях (с растояния 10 метров и выше)"
                    );
                }
                else
                {
                    arg.ReplyWith("Игрока не найдено!");
                }
            }
            return;
        }

        [HookMethod("AimCheckServer")]
        private void AimCheckServer(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !arg.Player().IsAdmin)
            {
                return;
            }
            double popa = 0;
            double head = 0;
            var Top = (from x in PlayersListed select x);
            foreach (var top in Top)
            {
                popa = popa + top.Value.Hits;
                head = head + top.Value.Heads;
            }
            arg.ReplyWith(
                $"[Анти-чит]: В голову попадают в {Math.Floor((head * 1f / popa * 1f) * 100f)}% случаев (с растояния 10 метров и выше)"
            );
            return;
        }

        [HookMethod("CheckServer")]
        private void CheckServer(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !arg.Player().IsAdmin)
            {
                return;
            }
            int i = 0;
            string players = "";
            var reply = 28117;
            if (reply == 0) { }
            double popa = 0;
            double head = 0;
            string aimdesc = "";
            var Top = (from x in PlayersListed select x);
            foreach (var top in Top)
            {
                popa = popa + top.Value.Hits;
                head = head + top.Value.Heads;
            }
            double aimserver = Math.Floor((head * 1f / popa * 1f) * 100f);
            players =
                "----------------------------------Игроки---------------------------------- \n";
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (PlayersListed.ContainsKey(player.userID))
                {
                    var playerKey = PlayersListed[player.userID];
                    if (playerKey.Deaths == 0)
                        playerKey.Deaths = 1;
                    if (playerKey.Hits == 0)
                        playerKey.Hits = 1;
                    double aimprocent = Math.Floor(
                        (playerKey.Heads * 1f / playerKey.Hits * 1f) * 100f
                    );
                    double kdr = Math.Round(playerKey.Killed * 1f / playerKey.Deaths * 1f, 2);
                    double razn = aimserver - aimprocent;
                    if (playerKey.Hits < 30 || playerKey.Killed < 10)
                    {
                        aimdesc = "Новый игрок";
                    }
                    else if (razn > -5 && razn > 5 && kdr < 2)
                    {
                        aimdesc = "Простой игрок";
                    }
                    else if (razn > -5 && razn > 5 && kdr < 3)
                    {
                        aimdesc = "Подозрительный игрок";
                    }
                    else if (razn > -5 && razn > 5 && kdr >= 3)
                    {
                        aimdesc = "Очень подозрительный игрок";
                    }
                    else if (razn > -5 && razn < -8 && kdr < 2)
                    {
                        aimdesc = "Игрок с хорошей точностью в голову";
                    }
                    else if (razn > -5 && razn < -8 && kdr < 3)
                    {
                        aimdesc = "Скилловый игрок";
                    }
                    else if (razn > -5 && razn < -8 && kdr < 4)
                    {
                        aimdesc = "Подозрительный игрок";
                    }
                    if (razn > -5 && razn < -8 && kdr >= 4)
                    {
                        aimdesc = "Читер";
                    }
                    else if (razn > 5 && razn < 8 && kdr < 1)
                    {
                        aimdesc = "Игрок со слабым скиллом";
                    }
                    else if (razn > 5 && razn < 8 && kdr < 2)
                    {
                        aimdesc = "Подозрительный игрок";
                    }
                    else if (razn > 5 && razn < 8 && kdr < 3)
                    {
                        aimdesc = "Очень подозрительный игрок";
                    }
                    if (razn > 5 && razn < 8 && kdr >= 4)
                    {
                        aimdesc = "Читер";
                    }
                    i++;
                    players =
                        players
                        + $"{i}. {player.displayName} ({player.userID}) | aim: {aimprocent}% | kdr {kdr} | {aimdesc} \n";
                }
            }
            arg.ReplyWith(
                players
                    + "-------------------------------------------------------------------------------"
            );
        }

        void Unload()
        {
            DestroyAll<PlayerHack>();
            SavePlayerData();
            SaveDataAdmin();
        }

        void DestroyAll<T>()
        {
            UnityEngine.Object[] objects = GameObject.FindObjectsOfType(typeof(T));
            if (objects != null)
                foreach (UnityEngine.Object gameObj in objects)
                    GameObject.Destroy(gameObj);
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (player == null)
                return;
            if (
                player.IsAdmin
                || permission.UserHasPermission(player.UserIDString, "anticheat.toggleadmin")
            )
            {
                if (!adata.AdminData.ContainsKey(player.userID))
                {
                    adata.AdminData.Add(
                        player.userID,
                        new ADMINDATA() { Name = player.displayName, Check = false, }
                    );
                }
                else
                {
                    adata.AdminData[player.userID].Name = player.displayName;
                }
                if (adata.AdminData[player.userID].Check)
                    SendReply(
                        player,
                        "<color=RED>Внимание!</color> У вас включен админ дебаг. Советуем его отключить (/ac)"
                    );
            }
            else if (adata.AdminData.ContainsKey(player.userID))
                if (
                    !player.IsAdmin
                    || !permission.UserHasPermission(player.UserIDString, "anticheat.toggleadmin")
                )
                {
                    adata.AdminData[player.userID].Check = adata.AdminData[player.userID].Check =
                        true;
                }
            if (!PlayersListed.ContainsKey(player.userID))
                CreateInfo(player);
            else
            {
                if (player.displayName != PlayersListed[player.userID].Name)
                    PlayersListed[player.userID].Name = player.displayName;
            }
            new PluginTimers(this).Once(2f, () => CheckFLY(player));
            timer.Once(1f, () => RefreshPlayer(player));
        }

        void RefreshPlayer(BasePlayer player)
        {
            if (player.GetComponent<PlayerHack>() == null)
                player.gameObject.AddComponent<PlayerHack>();
        }

        private void AutoBan(BasePlayer player, string reason)
        {
            if (b == DetectCountFSH)
            {
                Ban(player.userID, "[Анти-чит] Banned: вы были забанены на сервере.");
                LogToFile(
                    "ban",
                    $"[{DateTime.Now.ToShortTimeString()}] -  Ban: Reason - {reason} {player.displayName}({player.UserIDString}) забанен! Количество детектов привысило заданный предел.  Предупреждений: {b + 1}",
                    this,
                    true
                );
            }
        }

        private void CheckFLY(BasePlayer player)
        {
            if (player == null)
                return;
            if (!player.IsConnected)
                return;
            var position = player.transform.position;
            int f = 0;
            new PluginTimers(this).Repeat(
                2f,
                0,
                () =>
                {
                    if (!player.IsConnected)
                        return;
                    if (adata.AdminData.ContainsKey(player.userID))
                    {
                        if (!adata.AdminData[player.userID].Check)
                            return;
                    }
                    if (
                        player.IsFlying
                        && !player.IsSwimming()
                        && !player.IsDead()
                        && !player.IsSleeping()
                        && !player.IsWounded()
                    )
                    {
                        f++;
                        if (f >= 1)
                        {
                            if (b == DetectCountFSH && FHEnabled)
                            {
                                AutoBan(player, "FlyHack");
                            }
                            else
                            {
                                if (FHKickEnabled)
                                {
                                    Kick(player, "[Анти-чит] Обнаружен FlyHack");
                                    Debug.LogError(
                                        $"[Анти-чит], {player.displayName}, ({player.UserIDString}) кикнут! Причина: FlyHack!"
                                    );
                                    LogToFile(
                                        "log",
                                        $"[{DateTime.Now.ToShortTimeString()}] - (FlyHack) Игрок {player.displayName}({player.UserIDString}) кикнут! Слишком долго находился в воздухе! Предупреждений: {b + 1}",
                                        this,
                                        true
                                    );
                                }
                                SendDetection(
                                    player,
                                    string.Format(
                                        "<color=#ffa500>[Античит детект]</color> "
                                            + "(FLYHack) Игрок"
                                            + player.displayName
                                            + $" Слишком долго находиться в воздухе! Предупреждений: {b + 1}"
                                    )
                                );
                                var messages = $"Обнаружен FlyHack Предупреждений: {b + 1}";
                                Debug.LogWarning(
                                    $"[Анти-чит] {player.displayName}({player.UserIDString}) Обнаружен FlyHack Предупреждений: {b + 1}"
                                );
                                LogToFile(
                                    "log",
                                    $"[{DateTime.Now.ToShortTimeString()}] - (FlyHack) У игрок {player.displayName}({player.UserIDString}) обнаружен FlyHack Предупреждений: {b + 1}",
                                    this,
                                    true
                                );
                                b++;
                                return;
                            }
                        }
                    }
                    else
                    {
                        f = 0;
                    }
                }
            );
        }

        public class PlayerHack : MonoBehaviour
        {
            public BasePlayer player;
            public Vector3 lastPosition;
            public Vector3 currentDirection;
            public bool isonGround;
            public float Distance3D;
            public float VerticalDistance;
            public float deltaTick;
            public float speedHackDetections = 0f;
            public double currentTick;
            public double lastTick;
            public double lastTickFly;
            public double lastTickSpeed;

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                InvokeRepeating("CheckPlayer", 1f, 1f);
                lastPosition = player.transform.position;
            }

            static DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);

            static double CurrentTime()
            {
                return DateTime.UtcNow.Subtract(epoch).TotalMilliseconds;
            }

            static List<PlayerHack> fpsCalled = new List<PlayerHack>();
            static double fpsTime;
            static bool fpsCheckCalled = false;

            static void CheckForHacks(PlayerHack hack)
            {
                CheckForSpeedHack(hack);
            }

            static int fpsIgnore = 30;

            void CheckPlayer()
            {
                if (!player.IsConnected)
                    GameObject.Destroy(this);
                currentTick = CurrentTime();
                deltaTick = (float)((currentTick - lastTick) / 1000.0);
                Distance3D = Vector3.Distance(player.transform.position, lastPosition) / deltaTick;
                VerticalDistance = (player.transform.position.y - lastPosition.y) / deltaTick;
                currentDirection = (player.transform.position - lastPosition).normalized;
                isonGround = player.IsOnGround();
                if (
                    !player.IsWounded()
                    && !player.IsDead()
                    && !player.IsSleeping()
                    && deltaTick < 1.1f
                    && Performance.current.frameRate > fpsIgnore
                )
                    CheckForHacks(this);
                lastPosition = player.transform.position;
                if (fpsCheckCalled)
                    if (!fpsCalled.Contains(this))
                    {
                        fpsCalled.Add(this);
                        fpsTime += (CurrentTime() - currentTick);
                    }
                lastTick = currentTick;
            }
        }

        static float minSpeedPerSecond = 10f;

        static double LogTime()
        {
            return DateTime.UtcNow.Subtract(epoch).TotalSeconds;
        }

        static Vector3 lastGroundPosition;

        static void CheckForSpeedHack(PlayerHack hack)
        {
            if (instance.adata.AdminData.ContainsKey(hack.player.userID))
            {
                if (!instance.adata.AdminData[hack.player.userID].Check)
                    return;
            }
            RaycastHit hit;
            if (
                Physics.Raycast(
                    hack.player.transform.position,
                    Vector3.down,
                    out hit,
                    LayerMask.GetMask("Construction")
                )
            )
            {
                if (hit.transform.position != lastGroundPosition)
                    return;
                lastGroundPosition = hit.transform.position;
            }
            if (hack.Distance3D < minSpeedPerSecond)
                return;
            if (hack.VerticalDistance < -8f)
                return;
            if (hack.lastTickSpeed == hack.lastTick)
            {
                if (
                    hack.player.IsSwimming()
                    && hack.player.IsDead()
                    && hack.player.IsSleeping()
                    && hack.player.IsWounded()
                )
                    return;
                if (hack.player.GetMounted())
                    return;
                hack.speedHackDetections++;
                if (instance.SHEnable)
                {
                    if (hack.player.IsOnGround())
                    {
                        if (b == instance.DetectCountFSH && instance.SHEnabled)
                        {
                            instance.AutoBan(hack.player, "SpeedHack");
                            instance.LogToFile(
                                "ban",
                                $"[{DateTime.Now.ToShortTimeString()}] - (Speedhack) Игрок {hack.player.displayName}({hack.player.UserIDString}) забанен! Двигался со скоростью выше нормы. Предупреждений: {b + 1}",
                                AntiCheat.instance,
                                true
                            );
                        }
                        else
                        {
                            if (instance.SHKickEnabled)
                            {
                                instance.Kick(hack.player, "[Анти-чит] Обнаружен SpeedHack");
                                Debug.LogError(
                                    $"[Анти-чит] {hack.player.displayName}({hack.player.UserIDString}) кикнут! Причина: SpeedHack!"
                                );
                            }
                            var messages =
                                $"Обнаружен Speedhack ({hack.Distance3D.ToString()} м/с) Предупреждений: {b + 1}";
                            instance.LogToFile(
                                "log",
                                $"[{DateTime.Now.ToShortTimeString()}] - (Speedhack) Игрок {hack.player.displayName}({hack.player.UserIDString}) двигаеться со скоростью выше нормы. Предупреждений: {b + 1}",
                                AntiCheat.instance,
                                true
                            );
                            instance.SendDetection(
                                hack.player,
                                string.Format(
                                    $"<color=#ffa500>[Античит детект]</color> (SPEEDHack) Игрок {hack.player.displayName}({hack.player.UserIDString}) двигаеться со скоростью выше нормы. Предупреждений: {b + 1}"
                                )
                            );
                            UnityEngine.Debug.LogError(
                                $"[Анти-чит] Игрок {hack.player.displayName}({hack.player.UserIDString}) двигаеться со скоростью выше нормы. Предупреждений: {b + 1}"
                            );
                            b++;
                        }
                    }
                }
            }
            else
            {
                hack.speedHackDetections = 0f;
            }
            hack.lastTickSpeed = hack.currentTick;
        }

        static int bulletmask;
        static DamageTypeList emptyDamage = new DamageTypeList();
        static Vector3 VectorDown = new Vector3(0f, -1f, 0f);
        static Hash<BasePlayer, float> lastWallhack = new Hash<BasePlayer, float>();
        Hash<ulong, ColliderCheckTest> playerWallcheck = new Hash<ulong, ColliderCheckTest>();
        static RaycastHit cachedRaycasthit;

        [HookMethod("WallhackKillCheck")]
        private void WallhackKillCheck(BasePlayer player, BasePlayer attacker, HitInfo hitInfo)
        {
            if (adata.AdminData.ContainsKey(player.userID))
            {
                if (adata.AdminData[player.userID].Check == false)
                    return;
            }
            if (
                Physics.Linecast(
                    attacker.eyes.position,
                    hitInfo.HitPositionWorld,
                    out cachedRaycasthit,
                    bulletmask
                )
            )
            {
                BuildingBlock block =
                    cachedRaycasthit.collider.GetComponentInParent<BuildingBlock>();
                if (block != null)
                {
                    if (block.blockDefinition.hierachyName == "wall.window")
                        return;
                    CancelDamage(hitInfo);
                    if (Time.realtimeSinceStartup - lastWallhack[attacker] > 0.5f)
                    {
                        lastWallhack[attacker] = Time.realtimeSinceStartup;
                        UnityEngine.Debug.LogError(
                            $"WalhackAttack обнаружен у {player.displayName}"
                        );
                        LogToFile(
                            "log",
                            $"[{DateTime.Now.ToShortTimeString()}] - (WalhackAttack) {player.displayName}({player.UserIDString}) нанес урон через препятствие!",
                            this,
                            true
                        );
                        SendDetection(
                            player,
                            string.Format(
                                "<color=#ffa500>[Античит детект]</color> "
                                    + "(WalhackAttack) "
                                    + player.displayName
                                    + " нанес урон через препятствие!"
                            )
                        );
                    }
                }
            }
        }

        private void CancelDamage(HitInfo hitinfo)
        {
            hitinfo.damageTypes = emptyDamage;
            hitinfo.HitEntity = null;
        }

        private readonly Dictionary<ulong, NoRecoilData> data =
            new Dictionary<ulong, NoRecoilData>();
        private readonly Dictionary<ulong, Timer> detections = new Dictionary<ulong, Timer>();
        private readonly int detectionDiscardSeconds = 300;
        private readonly int violationProbability = 30;
        private readonly int maximumViolations = 30;
        private readonly Dictionary<string, int> probabilityModifiers = new Dictionary<
            string,
            int
        >()
        {
            { "weapon.mod.muzzleboost", -5 },
            { "weapon.mod.silencer", -5 },
            { "weapon.mod.holosight", -5 },
            { "crouching", -8 },
            { "aiming", -5 }
        };
        private readonly List<string> blacklistedAttachments = new List<string>()
        {
            "weapon.mod.muzzlebreak",
            "weapon.mod.silencer",
            "weapon.mod.small.scope"
        };

        public class NoRecoilData
        {
            public int Ticks = 0;
            public int Count;
            public int Violations;
        }

        void OnWeaponFired(
            BaseProjectile projectile,
            BasePlayer player,
            ItemModProjectile mod,
            ProjectileShoot projectiles
        )
        {
            if (!AntiRecoilEnabled)
                return;
            if (player == null)
                return;
            if (IsNPC(player))
                return;
            if (adata.AdminData.ContainsKey(player.userID))
                if (!adata.AdminData[player.userID].Check)
                    return;
            var item = player.GetActiveItem();
            if (item == null)
                return;
            if (!(ListWeapons.Contains(item.info.shortname)))
                return;
            var counts = 0;
            foreach (Item attachment in item.contents.itemList)
                if (
                    attachment.info.shortname == "weapon.mod.muzzlebrake"
                    || attachment.info.shortname == "weapon.mod.holosight"
                )
                    counts++;
            if (counts == 2)
                return;
            if (item.contents.itemList.Any(x => blacklistedAttachments.Contains(x.info.shortname)))
                return;
            NoRecoilData info;
            if (!data.TryGetValue(player.userID, out info))
                data.Add(player.userID, info = new NoRecoilData());
            Vector3 eyesDirection = player.eyes.HeadForward();
            if (eyesDirection.y < -0.80)
                return;
            info.Ticks++;
            int probModifier = 0;
            foreach (Item attachment in item.contents.itemList)
                if (probabilityModifiers.ContainsKey(attachment.info.shortname))
                    probModifier += probabilityModifiers[attachment.info.shortname];
            if (player.modelState.aiming && probabilityModifiers.ContainsKey("aiming"))
                probModifier += probabilityModifiers["aiming"];
            if (player.IsDucked() && probabilityModifiers.ContainsKey("crouching"))
                probModifier += probabilityModifiers["crouching"];
            Timer detectionTimer;
            if (detections.TryGetValue(player.userID, out detectionTimer))
                detectionTimer.Reset(detectionDiscardSeconds);
            else
                detections.Add(
                    player.userID,
                    timer.Once(
                        detectionDiscardSeconds,
                        delegate()
                        {
                            if (info.Violations > 0)
                                info.Violations--;
                        }
                    )
                );
            timer.Once(
                0.5f,
                () =>
                {
                    ProcessRecoil(
                        projectile,
                        player,
                        mod,
                        projectiles,
                        info,
                        probModifier,
                        eyesDirection
                    );
                }
            );
        }

        private void ProcessRecoil(
            BaseProjectile projectile,
            BasePlayer player,
            ItemModProjectile mod,
            ProjectileShoot projectileShoot,
            NoRecoilData info,
            int probModifier,
            Vector3 eyesDirection
        )
        {
            if (
                projectile == null
                || player == null
                || mod == null
                || projectileShoot == null
                || info == null
                || eyesDirection == null
            )
                return;
            var nextEyesDirection = player.eyes.HeadForward();
            if (Math.Abs(nextEyesDirection.y - eyesDirection.y) < .009 && nextEyesDirection.y < .8)
                info.Count++;
            if (info.Ticks <= 10)
                return;
            var prob = 100 * info.Count / info.Ticks;
            var item = player.GetActiveItem();
            if (prob > ((100 - violationProbability) + probModifier))
            {
                if (prob > 100)
                    prob = 100;
                if (prob < DetectPerMacros)
                    return;
                info.Violations++;
                Debug.LogError(
                    "(Макрос) "
                        + player.displayName
                        + " SteamID "
                        + player.UserIDString
                        + ": вероятность "
                        + string.Format("{0}", prob)
                        + "% | обнаружений "
                        + info.Violations.ToString()
                        + "."
                );
                SendDetection(
                    player,
                    string.Format(
                        "<color=#ffa500>[Античит детект]</color> "
                            + "(NoRecoil) "
                            + "У игрока "
                            + player.displayName
                            + " обнаружен NoRecoil "
                            + ",вероятность "
                            + string.Format("{0}", prob)
                            + "% | обнаружений "
                            + info.Violations.ToString()
                    )
                );
                LogToFile(
                    "log",
                    $"[{DateTime.Now.ToShortTimeString()}] - (Макрос) "
                        + player.displayName
                        + " SteamID "
                        + player.UserIDString
                        + ": вероятность "
                        + string.Format("{0}", prob)
                        + "% | обнаружений "
                        + info.Violations.ToString()
                        + " | "
                        + item.info.shortname,
                    this,
                    true
                );
                if (info.Violations > DetectCountMacros && MCREnabled)
                {
                    Ban(player.userID, "[Анти-чит] Обнаружен скрипт для макроса");
                    LogToFile(
                        "ban",
                        $"[{DateTime.Now.ToShortTimeString()}] - (Макрос) Игрок"
                            + player.displayName
                            + "забанен. Вероятность "
                            + string.Format("{0}", prob)
                            + "% | обнаружений "
                            + info.Violations.ToString()
                            + " | "
                            + item.info.shortname,
                        this,
                        true
                    );
                }
            }
            info.Ticks = 0;
            info.Count = 0;
        }

        static Hash<BasePlayer, int> wallhackDetec = new Hash<BasePlayer, int>();

        public class ColliderCheckTest : MonoBehaviour
        {
            public BasePlayer player;
            Hash<Collider, Vector3> entryPosition = new Hash<Collider, Vector3>();
            SphereCollider col;
            public float teleportedBack;
            public Collider lastCollider;

            void Awake()
            {
                player = transform.parent.GetComponent<BasePlayer>();
                col = gameObject.AddComponent<SphereCollider>();
                col.radius = 0.1f;
                col.isTrigger = true;
                col.center = new Vector3(0f, 0.5f, 0f);
            }

            public static BaseEntity GetCollEntity(Vector3 entry, Vector3 exist)
            {
                var rayArray = Physics.RaycastAll(
                    exist,
                    entry,
                    Vector3.Distance(entry, exist),
                    constructionColl
                );
                for (int i = 0; i < rayArray.Length; i++)
                {
                    return rayArray[i].GetEntity();
                }
                return null;
            }

            void OnTriggerExit(Collider col)
            {
                if (textureenable)
                    if (entryPosition.ContainsKey(col))
                    {
                        BuildingBlock block = col.GetComponent<BuildingBlock>();
                        if (block != null)
                        {
                            if (
                                !block.gameObject.name.Contains("foundation.steps")
                                && !block.gameObject.name.Contains("block.halfheight.slanted")
                            )
                            {
                                instance.SendDetection(
                                    player,
                                    string.Format(
                                        $"{player.displayName},({player.userID}) Обнаружен TextureHack!"
                                    )
                                );
                                ForcePlayerBack(
                                    this,
                                    col,
                                    entryPosition[col],
                                    player.transform.position
                                );
                                if (Time.realtimeSinceStartup - lastWallhack[player] < 10f)
                                {
                                    instance.SendDetection(
                                        player,
                                        string.Format(
                                            $"{player.displayName},({player.userID}) Обнаружен WallHack! Детект № {wallhackDetec[player]}"
                                        )
                                    );
                                    wallhackDetec[player]++;
                                    instance.LogToFile(
                                        "log",
                                        $"[{DateTime.Now.ToShortTimeString()}] - (WallHack) {player.userID.ToString()} {player.displayName.ToString()} Обнаружен WallHack! Обнаружений {wallhackDetec[player]}",
                                        AntiCheat.instance,
                                        true
                                    );
                                }
                                lastWallhack[player] = Time.realtimeSinceStartup;
                            }
                        }
                        entryPosition.Remove(col);
                    }
            }

            void OnDestroy()
            {
                Destroy(gameObject);
                Destroy(col);
            }
        }

        static void ForcePlayerBack(
            ColliderCheckTest colcheck,
            Collider collision,
            Vector3 entryposition,
            Vector3 exitposition
        )
        {
            Vector3 rollBackPosition = GetRollBackPosition(entryposition, exitposition, 4f);
            Vector3 rollDirection = (entryposition - exitposition).normalized;
            foreach (
                RaycastHit rayhit in UnityEngine.Physics.RaycastAll(
                    rollBackPosition,
                    (exitposition - entryposition).normalized,
                    5f
                )
            )
            {
                if (rayhit.collider == collision)
                {
                    rollBackPosition = rayhit.point + rollDirection * 1f;
                }
            }
            colcheck.teleportedBack = Time.realtimeSinceStartup;
            colcheck.lastCollider = collision;
            ForcePlayerPosition(colcheck.player, rollBackPosition);
        }

        static Vector3 GetRollBackPosition(
            Vector3 entryposition,
            Vector3 exitposition,
            float distance
        )
        {
            distance = Vector3.Distance(exitposition, entryposition) + distance;
            var direction = (entryposition - exitposition).normalized;
            return (exitposition + (direction * distance));
        }

        static new void ForcePlayerPosition(BasePlayer player, Vector3 destination)
        {
            player.MovePosition(destination);
            player.ClientRPCPlayer(null, player, "ForcePositionTo", destination);
        }

        [HookMethod("OnBasePlayerAttacked")]
        private void OnBasePlayerAttacked(BasePlayer player, HitInfo hitInfo)
        {
            if (player.IsDead())
                return;
            if (hitInfo.Initiator == null)
                return;
            if (player.health - hitInfo.damageTypes.Total() > 0f)
                return;
            BasePlayer attacker = hitInfo.Initiator.ToPlayer();
            if (attacker == null)
                return;
            if (attacker == player)
                return;
            WallhackKillCheck(player, attacker, hitInfo);
        }

        public static void msgPlayer(BasePlayer player, string msg)
        {
            player.ChatMessage($"[Анти-Чит] {msg}");
        }

        public static void msgAll(string msg)
        {
            ConsoleNetwork.BroadcastToAllClients("chat.add", 0, $"[Анти-Чит] {msg}");
        }

        private void CreateInfo(BasePlayer player)
        {
            if (player == null)
                return;
            if (!PlayersListed.ContainsKey(player.userID))
                PlayersListed.Add(
                    player.userID,
                    new PlayerAntiCheat()
                    {
                        Deaths = 0,
                        Killed = 0,
                        Heads = 0,
                        Hits = 0,
                        Name = player.displayName
                    }
                );
        }

        private void SaveDataAdmin()
        {
            Interface.GetMod().DataFileSystem.WriteObject("AntiCheat/AdminData", adata);
        }

        private void SavePlayerData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("AntiCheat/PlayerAntiCheat", PlayersListed);
        }

        void SendDetection(BasePlayer player, string msg)
        {
            if (SendsLogs)
                if (permission.UserHasPermission(player.UserIDString, "anticheat.sendlogs"))
                    SendReply(player, msg);
        }

        public Dictionary<ulong, PlayerAntiCheat> PlayersListed =
            new Dictionary<ulong, PlayerAntiCheat>();

        public class PlayerAntiCheat
        {
            public string Name { get; set; }
            public int Killed { get; set; }
            public int Deaths { get; set; }
            public int Hits { get; set; }
            public int Heads { get; set; }
            public bool Banned;
            public string Date;
            public string Reason;
            public string BanCreator;
        }
    }
}
