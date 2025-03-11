using static Oxide.Plugins.CRaidController.ConfigData;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CRaidController", "https://discord.gg/TrJ7jnS233", "4.0.7")]
    [Description("System for managing raid")]
    class CRaidController : RustPlugin
    {
        #region FIELDS

        [PluginReference] readonly Plugin ImageLibrary;
        [PluginReference] readonly Plugin CustomStatusFramework;
        [PluginReference] readonly Plugin RaidableBases;
        private static CRaidController _instance;
        private static MainController _mainController;
        private static Dictionary<ulong, PlayerController> _playerControllers;
        private static Dictionary<ulong, AdminController> _adminControllers;
        private static Images _images;
        private static CodeFlingApi _codeFlingApi;
        private static Feed _feed;
        private static FireBallManager _fireBallManager;
        private static PlayerSetting _playerSetting;
        private static PlayerData _playerData;
        private static TcController _tcController;
        private const string UI_ADMIN_PANEL = "craidcontroller.ui.admin.panel";
        private const string UI_ADMIN_PANEL_LOCKER = "craidcontroller.ui.admin.panel.locker";
        private const string UI_PLAYER_PANEL = "craidcontroller.ui.player.panel";
        private const string UI_PLAYER_PANEL_LOCKER = "craidcontroller.ui.player.panel.locker";
        private const string UI_PLAYER_UI_STATIC = "craidcontroller.player.ui.static";
        private const string UI_PLAYER_UI_PROGRESS = "craidcontroller.player.ui.progress";
        private const string UI_PLAYER_TC_PANEL = "craidcontroller.player.tc.panel";
        private const string COLOR_ERROR = "#cd4632";
        private const string COLOR_SUCCESS = "#6c9633";
        private const string COLOR_PRIMARY = "#bf8e51";
        private const string ITEM_TYPE_ADD = "add";
        private const string ITEM_TYPE_ADD_COMPLETE = "addComplete";
        private const string ITEM_TC_PROTECT = "Bypass.TcProtectItem";
        private const string ITEM_TC_NO_PROTECT = "Bypass.TcNoProtectItem";
        private const string ITEM_TYPE_PAGE = "page";
        private const string ITEM_TYPE_REMOVE = "remove";
        private const string SOUND_ERROR = "assets/prefabs/weapons/toolgun/effects/repairerror.prefab";
        private const string SOUND_ALERT = "assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab";
        private readonly List<string> _uiList = new List<string> {"Default"};
        private readonly List<string> _forceRefresh = new List<string> { "Ui.Ymax", "Ui.BackgroundOpacity", "Ui.BackgroundColor", "Ui.HideNotTime", "Ui.HideIsTime", "Ui.AllowPlayerHide", "Ui.AllowPlayerChange"};
        private readonly List<string> _timezones = new List<string>{"-12", "-11", "-10", "-9", "-8", "-7", "-6", "-5", "-4", "-3", "-2", "-1","0","+1", "+2", "+3", "+4", "+5", "+6", "+7", "+8", "+9", "+10", "+11", "+12", "+13", "+14"};
        private readonly List<string> _languages = new List<string> { "en", "fr", "es", "de", "it", "pt", "nl", "ru", "zh", "ja"};
        private readonly List<string> _messageType = new List<string> { "Chat", "Notifications" };
        private readonly List<string> _forcedEntityBypass = new List<string> { "door_barricade", "door_barricade_a", "door_barricade_b", "cover" };
        public enum ActiveUI { Disabled, Activated, CustomStatusFramework }
        public enum AdminPanelTab { Global, Bypass, Message, Ui, Schedule }
        public enum ScheduleStatus { AllowedAllDay, NotAllowedAllDay, AllowDuringSchedule}
        public enum TcBypass { Disabled, AllPlayer, LeaderOnly }
        public enum UiType { PlayerUI, AdminUI, AllUI }
        #endregion

        #region SERVER HOOKS

        private void OnServerInitialized()
        {
            if (ImageLibrary == null)
                PrintWarning("ImageLibrary not found, image not loaded !");

            _instance = this;
            GameObject mainObject = new GameObject("CRaidController.MainControllerObject");
            GameObject fireballObject = new GameObject("CRaidController.FireballObject");
            _mainController = mainObject.AddComponent<MainController>();
            _adminControllers = new Dictionary<ulong, AdminController>();
            _playerControllers = new Dictionary<ulong, PlayerController>();
            _images = new Images();
            _codeFlingApi = new CodeFlingApi();
            _feed = new Feed();
            _fireBallManager = fireballObject.AddComponent<FireBallManager>();
            _playerSetting = new PlayerSetting();
            _playerData = new PlayerData();
            _tcController = new TcController();
            cmd.AddChatCommand(_cfg == null ? "craid" : _cfg.Global.Command, this, "CmdDrawInterface");

            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
        }

        private void Unload()
        {
            PlayerController[] playerObjects = UnityEngine.Object.FindObjectsOfType<PlayerController>();
            if (playerObjects != null)
            {
                foreach (var obj in playerObjects)
                    UnityEngine.Object.DestroyImmediate(obj);
            }

            AdminController[] adminObjects = UnityEngine.Object.FindObjectsOfType<AdminController>();
            if (adminObjects != null)
            {
                foreach (var obj in adminObjects)
                    UnityEngine.Object.DestroyImmediate(obj);
            }

            if (_mainController != null)
                UnityEngine.Object.DestroyImmediate(_mainController.gameObject);

            if (_fireBallManager != null)
                UnityEngine.Object.DestroyImmediate(_fireBallManager.gameObject);

            _playerData.SaveData();
            _tcController.SaveData();
        }

        private void OnNewSave(string filename)
        {
            _playerData.ResetData();
            _tcController.ResetData();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player?.IsReceivingSnapshot != false || player.IsSleeping())
            {
                timer.In(1, () => OnPlayerConnected(player));
                return;
            }

            if (!_playerControllers.ContainsKey(player.userID) && !player.GetComponent<PlayerController>())
                _playerControllers[player.userID] = player.gameObject.AddComponent<PlayerController>();

            if (player.IsAdmin && !_adminControllers.ContainsKey(player.userID) && !player.GetComponent<AdminController>())
                _adminControllers[player.userID] = player.gameObject.AddComponent<AdminController>();
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (_playerControllers.ContainsKey(player.userID))
            {
                UnityEngine.Object.Destroy(_playerControllers[player.userID]);
                _playerControllers.Remove(player.userID);
            }

            if (_adminControllers.ContainsKey(player.userID))
            {
                UnityEngine.Object.Destroy(_adminControllers[player.userID]);
                _adminControllers.Remove(player.userID);
            }
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (!_cfg.Enable || entity == null || info == null || info?.Initiator == null)
                return;

            if (RaidableBases != null && IsRaidableBase(entity))
                return;

            if (entity is Barricade && (_forcedEntityBypass.Contains(entity.ShortPrefabName)))
                return;

            if (!(entity is BuildingBlock || entity is SimpleBuildingBlock || entity is Door || entity.PrefabName.Contains("deploy")))
                return;

            if (info?.Initiator is FireBall && entity != null && _fireBallManager.IsExist(entity.net.ID.Value))
            {
                if (info?.damageTypes.GetMajorityDamageType() != Rust.DamageType.Heat)
                    return;

                info?.damageTypes.ScaleAll(0f);
                FireBall fireball = info?.Initiator as FireBall;

                if (fireball != null && !fireball.IsDestroyed)
                    fireball.Kill();
            }
            else if (info?.Initiator is BasePlayer && info?.InitiatorPlayer != null)
            {
                BasePlayer attacker = info?.InitiatorPlayer;

                if (_cfg.Global.BlockDamage && !IsOnline(attacker)) //Catch player disconnect for taking damage
                {
                    Nullify(info, entity);
                    return;
                }

                if (attacker.IsNpc)
                    return;

                if (_cfg.Global.BlockDamage)
                {
                    if (_mainController.currentSchedule.isRaidTime)
                    {
                        if (_playerData.data[attacker.userID].TimePlayed > _cfg.Global.RaidDelay)
                        {
                            return;
                        }
                        else
                        {
                            _playerControllers[attacker.userID].alertMessage.AddAlert("Alert.RaidDelay", 1);
                            Nullify(info, entity);
                        }

                        if (_playerData.data[entity.OwnerID].TimePlayed > _cfg.Global.RaidProtectionDuration)
                        {
                            return;
                        }
                        else
                        {
                            _playerControllers[attacker.userID].alertMessage.AddAlert("Alert.RaidProtectionDuration", 1);
                            Nullify(info, entity);
                        }

                        return;
                    }

                    if (_cfg.Bypass.Admin && attacker.IsAdmin)
                        return;

                    if (_cfg.Bypass.Owner && IsOwner(attacker.userID, entity.OwnerID))
                        return;

                    if (_cfg.Bypass.Mate && IsMate(attacker, entity.OwnerID))
                        return;

                    if (_cfg.Bypass.Twig && IsTwigs(entity))
                        return;

                    if (_cfg.Bypass.NoTc && !IsProtectedByTC(entity))
                        return;

                    if (_cfg.Bypass.TcDecay && IsProtectedByTCAndDecaying(entity))
                        return;

                    if (_cfg.Bypass.TcProtectItem.Contains(GetItemShortnameFromEntity(entity)) && IsProtectedByTC(entity))
                        return;

                    if (_cfg.Bypass.TcNoProtectItem.Contains(GetItemShortnameFromEntity(entity)) && !IsProtectedByTC(entity))
                        return;

                    if (_cfg.Bypass.BuildingPrivilege && entity.GetBuildingPrivilege().IsAuthed(attacker.userID))
                        return;

                    if (_cfg.Bypass.TcBypass != TcBypass.Disabled && _tcController.TcDisabledProtection(entity.net.ID.Value))
                        return;

                    Nullify(info, entity);
                }

                _playerControllers[attacker.userID].alertMessage.AddAlert("Alert.NotRaidTime", 1);
            }
        }

        private void OnLootEntity(BasePlayer player, BuildingPrivlidge tc)
        {
            if (!_cfg.Enable || !_cfg.Global.BlockDamage || _cfg.Bypass.TcBypass == TcBypass.Disabled || player == null || tc == null)
                return;

            if (_cfg.Bypass.TcBypass == TcBypass.LeaderOnly && (player.Team == null || player.userID != player.Team.teamLeader))
                return;

            if (_playerControllers.ContainsKey(player.userID))
            {
                PlayerController playerController = _playerControllers[player.userID];
                playerController.DrawTCBypassPanel(tc.net.ID.Value);
            }
        }
        private void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            if (player == null || !_playerControllers.ContainsKey(player.userID))
                return;

            PlayerController playerController = _playerControllers[player.userID];
            playerController.DestroyUI(tc: true);
            playerController.inTc = 0;
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!_cfg.Enable && entity == null && info == null && info?.Initiator == null)
                return;

            if (!(info?.Initiator is BasePlayer) && info?.InitiatorPlayer == null)
                return;

            if (_mainController.currentSchedule.isRaidTime)
                return;

            if (_cfg.Global.BlockDamage)
                return;

            BasePlayer attacker = info?.InitiatorPlayer;

            if(attacker == null) 
                return;

            if (_cfg.Global.CmdAllTeam && attacker.Team != null)
            {
                List<ulong> members = GetTeamMembers(attacker);
                if (members != null)
                {
                    foreach (var p in members)
                    {
                        string cmd = _cfg.Global.CmdExecute.Replace("{player}", p.ToString());
                        rust.RunServerCommand(cmd);
                    }
                }
            }
            else
            {
                string cmd = _cfg.Global.CmdExecute.Replace("{player}", attacker.UserIDString);
                rust.RunServerCommand(cmd);
            }
        }

        #endregion

        #region MAIN CONTOLLER

        internal class MainController : FacepunchBehaviour
        {
            public DateTime now;
            public bool configChanged;
            public CurrentSchedule currentSchedule;
            private DateTime cacheMessage;
            private DateTime cacheNextStartMessage;
            private DateTime cacheNextEndMessage;
            private bool raidStartedAlert;

            public class CurrentSchedule
            {
                private static MainController _parent;
                public Dictionary<DayOfWeek, ScheduleSetting> schedule;
                public DateTime cachedTime;
                public DayOfWeek day;
                public ScheduleStatus status;
                public bool isRaidTime;
                public Range start;
                public Range end;

                public class Range
                {
                    public DayOfWeek day;
                    public int hour;
                    public int minute;
                }

                public CurrentSchedule(MainController parent)
                {
                    _parent = parent;
                    schedule = _cfg.Schedule;
                    CheckAndCleanScheduleStatus();
                    cachedTime = _parent.now;
                    day = cachedTime.DayOfWeek;
                    status = schedule[day].Status;
                    isRaidTime = false;
                    start = new Range();
                    end = new Range();
                    LoadCurrentSchedule();                    
                    /* //******************************************************************** DEBUG ****************************************************
                    _instance.Puts("--------------------------------------------------------------");
                    _instance.Puts("cachedTime: " + cachedTime);
                    _instance.Puts("dayOfWeek: " + ToDateLangFormat(day, "fr"));
                    _instance.Puts("status: " + status);
                    _instance.Puts("isRaidTime: " + isRaidTime);
                    _instance.Puts("startDay: " + ToDateLangFormat(start.day, "fr"));
                    _instance.Puts("startHour: " + start.hour);
                    _instance.Puts("startMinute: " + start.minute);
                    _instance.Puts("endDay: " + ToDateLangFormat(end.day, "fr"));
                    _instance.Puts("endHour: " + end.hour);
                    _instance.Puts("endMinute: " + end.minute);
                    _instance.Puts("ProgressBarMaxWidth: " + ProgressBarMaxWidth());
                    _instance.Puts("ProgressPercent: " + ProgressPercent());
                    _instance.Puts("StartValue: " + StartValue(null));
                    _instance.Puts("EndValue: " + EndValue(null));
                    _instance.Puts("--------------------------------------------------------------");
                    //******************************************************************************************************************************* */
                    
                }

                public void LoadCurrentSchedule()
                {
                    Range startBestValue = GetStartCurrentSchedule().Item1;
                    bool raidTime = GetStartCurrentSchedule().Item2;
                    Range endBestValue = GetEndCurrentSchedule(raidTime);
                    start.day = startBestValue.day;
                    start.hour = startBestValue.hour;
                    start.minute = startBestValue.minute;
                    end.day = endBestValue.day;
                    end.hour = endBestValue.hour;
                    end.minute = endBestValue.minute;
                    isRaidTime = raidTime;
                }

                private Tuple<Range, bool> GetStartCurrentSchedule(Range startBestValue = null, bool raidTime = false)
                {
                    for (int daysChecked = 0; daysChecked < 7; daysChecked++)
                    {
                        DayOfWeek prevDay = cachedTime.AddDays(-daysChecked).DayOfWeek;
                        ScheduleStatus prevDayStatus = schedule[prevDay].Status;

                        if (prevDayStatus == ScheduleStatus.AllowedAllDay)
                        {
                            if (daysChecked == 0)
                                raidTime = true;

                            if (!raidTime)
                                break;

                            startBestValue = new Range() { day = prevDay, hour = 0, minute = 0 };
                            continue;
                        }
                        else if (prevDayStatus == ScheduleStatus.NotAllowedAllDay)
                        {
                            if (daysChecked == 0)
                                raidTime = false;

                            if (raidTime)
                                break;

                            startBestValue = new Range() { day = prevDay, hour = 0, minute = 0 };
                            continue;
                        }
                        else if (prevDayStatus == ScheduleStatus.AllowDuringSchedule)
                        {
                            if (daysChecked == 0)
                            {
                                raidTime = false;

                                foreach (var range in schedule[prevDay].TimeRange)
                                {
                                    TimeSpan startTime = new TimeSpan(range.StartHour, range.StartMinute, 0);
                                    TimeSpan endTime = new TimeSpan(range.EndHour, range.EndMinute, 0);

                                    if (_parent.now.TimeOfDay >= startTime && _parent.now.TimeOfDay <= endTime)
                                    {
                                        raidTime = true;
                                        startBestValue = new Range() { day = prevDay, hour = range.StartHour, minute = range.StartMinute };

                                        if (range.StartHour == 0 && range.StartMinute == 0)
                                            continue;

                                        return Tuple.Create<Range, bool>(startBestValue, raidTime);
                                    }
                                }

                                if (!raidTime)
                                {
                                    foreach (var range in schedule[prevDay].TimeRange)
                                    {
                                        TimeSpan endTime = new TimeSpan(range.EndHour, range.EndMinute, 0);

                                        if (_parent.now.TimeOfDay > endTime)
                                        {
                                            startBestValue = new Range() { day = prevDay, hour = range.EndHour, minute = range.EndMinute };
                                            return Tuple.Create<Range, bool>(startBestValue, raidTime);
                                        }
                                    }

                                    startBestValue = new Range() { day = prevDay, hour = 0, minute = 0 };
                                    continue;
                                }
                                break;
                            }
                            else
                            {
                                if (raidTime)
                                {
                                    TimeRange range = schedule[prevDay].TimeRange.LastOrDefault();

                                    if (range.StartHour == 0 && range.StartMinute == 0)
                                    {
                                        if (range.EndHour == 23 && range.EndMinute == 59)
                                        {
                                            startBestValue = new Range() { day = prevDay, hour = range.StartHour, minute = range.StartMinute };
                                            continue;
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        if (range.EndHour == 23 && range.EndMinute == 59)
                                            startBestValue = new Range() { day = prevDay, hour = range.StartHour, minute = range.StartMinute };

                                        break;
                                    }
                                }
                                else
                                {
                                    TimeRange range = schedule[prevDay].TimeRange.LastOrDefault();

                                    if (range.EndHour == 23 && range.EndMinute == 59)
                                        break;

                                    startBestValue = new Range() { day = prevDay, hour = range.EndHour, minute = range.EndMinute };
                                    break;
                                }
                            }
                        }
                    }

                    return Tuple.Create<Range, bool>(startBestValue, raidTime);
                }

                private Range GetEndCurrentSchedule(bool raidTime, Range endBestValue = null)
                {
                    for (int daysChecked = 0; daysChecked < 7; daysChecked++)
                    {
                        DayOfWeek nextDay = cachedTime.AddDays(daysChecked).DayOfWeek;
                        ScheduleStatus nextDayStatus = schedule[nextDay].Status;

                        if (nextDayStatus == ScheduleStatus.AllowedAllDay)
                        {
                            if (!raidTime)
                                break;

                            endBestValue = new Range() { day = nextDay, hour = 23, minute = 59 };
                            continue;
                        }
                        else if (nextDayStatus == ScheduleStatus.NotAllowedAllDay)
                        {
                            if (raidTime)
                                break;

                            endBestValue = new Range() { day = nextDay, hour = 23, minute = 59 };
                            continue;
                        }
                        else if (nextDayStatus == ScheduleStatus.AllowDuringSchedule)
                        {
                            if (daysChecked == 0)
                            {
                                if (raidTime)
                                {
                                    foreach (var range in schedule[nextDay].TimeRange)
                                    {
                                        TimeSpan startTime = new TimeSpan(range.StartHour, range.StartMinute, 0);
                                        TimeSpan endTime = new TimeSpan(range.EndHour, range.EndMinute, 0);

                                        if (_parent.now.TimeOfDay >= startTime && _parent.now.TimeOfDay <= endTime)
                                        {
                                            endBestValue = new Range() { day = nextDay, hour = range.EndHour, minute = range.EndMinute };

                                            if (range.EndHour == 23 && range.EndMinute == 59)
                                                continue;

                                            return endBestValue;
                                        }
                                    }
                                }
                                else
                                {
                                    foreach (var range in schedule[nextDay].TimeRange)
                                    {
                                        TimeSpan startTime = new TimeSpan(range.StartHour, range.StartMinute, 0);

                                        if (_parent.now.TimeOfDay < startTime)
                                        {
                                            endBestValue = new Range() { day = nextDay, hour = range.StartHour, minute = range.StartMinute };
                                            return endBestValue;
                                        }
                                    }

                                    endBestValue = new Range() { day = nextDay, hour = 23, minute = 59 };
                                    continue;
                                }
                            }
                            else
                            {
                                if (raidTime)
                                {
                                    TimeRange range = schedule[nextDay].TimeRange.FirstOrDefault();

                                    if (range.StartHour == 0 && range.StartMinute == 0)
                                    {
                                        if (range.EndHour == 23 && range.EndMinute == 59)
                                        {
                                            endBestValue = new Range() { day = nextDay, hour = range.EndHour, minute = range.EndMinute };
                                            continue;
                                        }
                                        else
                                        {
                                            endBestValue = new Range() { day = nextDay, hour = range.EndHour, minute = range.EndMinute };
                                            break;
                                        }
                                    }
                                    else
                                        break;

                                }
                                else
                                {
                                    TimeRange range = schedule[nextDay].TimeRange.FirstOrDefault();
                                    endBestValue = new Range() { day = nextDay, hour = range.StartHour, minute = range.StartMinute };
                                    break;
                                }
                            }
                        }
                    }

                    return endBestValue;
                }

                private void CheckAndCleanScheduleStatus()
                {
                    foreach (var schedule in schedule)
                    {
                        if (schedule.Value.Status == ScheduleStatus.AllowDuringSchedule)
                        {
                            if(schedule.Value.TimeRange.Count == 0)
                            {
                                schedule.Value.Status = ScheduleStatus.NotAllowedAllDay;
                            }
                            else
                            {
                                foreach(var x in schedule.Value.TimeRange)
                                {
                                    if (x.StartHour == 0 && x.StartMinute == 0 && x.EndHour == 23 && x.EndMinute == 59)
                                        schedule.Value.Status = ScheduleStatus.AllowedAllDay;
                                }
                            }
                        }
                    }
                }

                private double CalculateProgressPercentage()
                {
                    int startDayValue = ((int)start.day + 6) % 7;
                    int nowDayValue = ((int)_parent.now.DayOfWeek + 6) % 7;
                    int endDayValue = ((int)end.day + 6) % 7;
                    int startDaysDifference = nowDayValue == startDayValue ? 0 : startDayValue - nowDayValue;
                    int endDaysDifference = nowDayValue == endDayValue ? 0 : endDayValue - nowDayValue;

                    if (startDaysDifference < 0)
                        startDaysDifference -= 7;

                    if (endDaysDifference < 0)
                        endDaysDifference += 7;

                    DateTime startTime = _parent.now.AddDays(startDaysDifference).Date.AddHours(start.hour).AddMinutes(start.minute);
                    DateTime endTime = _parent.now.AddDays(endDaysDifference).Date.AddHours(end.hour).AddMinutes(end.minute);
                    TimeSpan totalDuration = endTime - startTime;
                    TimeSpan timeElapsed = _parent.now - startTime;
                    return (timeElapsed.TotalSeconds / totalDuration.TotalSeconds) * 100;
                }

                public float ProgressBarMaxWidth() => 1 - ((float) CalculateProgressPercentage() / 100);
                public string ProgressPercent() => Math.Round(CalculateProgressPercentage()).ToString();
                public string StartValue(BasePlayer player) => $"{ToDateLangFormat(start.day, player == null ? "en" : _playerSetting.data[player.userID].Language).ToUpper()}: {ToDateLangFormat(start.hour+"h"+start.minute, player == null ? "en" : _playerSetting.data[player.userID].Language)}";
                public string EndValue(BasePlayer player) => $"{ToDateLangFormat(end.day, player == null ? "en" : _playerSetting.data[player.userID].Language).ToUpper()}: {ToDateLangFormat(end.hour + "h" + end.minute, player == null ? "en" : _playerSetting.data[player.userID].Language)}";
            }

            private void Awake()
            {
                if (AllowDuringScheduleNoData() != null)
                    AlertAllowDuringScheduleNoData();

                now = UpdateTime();
                cacheMessage = now;
                cacheNextStartMessage = now;
                cacheNextEndMessage = now;
                currentSchedule = new CurrentSchedule(this);
                InvokeRepeating(UpdateController, 0f, 1f);
                InvokeRepeating(NotifyPlayers, 0f, 1f);
            }

            private void UpdateController()
            {
                now = UpdateTime();
                bool isTimeToUpdate = false;

                if (now.TimeOfDay > new TimeSpan(currentSchedule.end.hour, currentSchedule.end.minute, 0) || now.DayOfWeek != currentSchedule.day)
                    isTimeToUpdate = true;

                if (isTimeToUpdate || CfgScheduleHasChanged())
                {
                    if (AllowDuringScheduleNoData() != null)
                        AlertAllowDuringScheduleNoData();

                    currentSchedule = new CurrentSchedule(this);
                    configChanged = false;
                    raidStartedAlert = false;
                    cacheMessage = now;
                    cacheNextStartMessage = now;
                    cacheNextEndMessage = now;
                }
            }

            private void NotifyPlayers()
            {
                if (cacheMessage.Minute == now.Minute)
                    return;

                cacheMessage = now;

                if (currentSchedule.status == ScheduleStatus.AllowedAllDay)
                {
                    if (_cfg.Message.AllowedMessage && !raidStartedAlert)
                    {
                        raidStartedAlert = true;
                        _instance.SendToAllPlayerControllers(key: "Alert.AllowedMessage");
                    }

                    if (_cfg.Message.MinuteToEndMessage > 0 && cacheNextEndMessage < now)
                    {
                        _instance.SendToAllPlayerControllers(key: "Alert.AllowedAllDayMessage", args: new object[] { currentSchedule.end.day, $"{ToTimeString(currentSchedule.end.hour)}h{ToTimeString(currentSchedule.end.minute)}" });
                        cacheNextEndMessage = now.AddMinutes(_cfg.Message.MinuteToEndMessage);
                    }
                }
                else if (currentSchedule.status == ScheduleStatus.NotAllowedAllDay)
                {
                    if (_cfg.Message.FinishedMessage && raidStartedAlert)
                    {
                        raidStartedAlert = false;
                        _instance.SendToAllPlayerControllers(key: "Alert.NotAllowedMessage", style: 1);
                    }

                    if (_cfg.Message.MinuteToStartMessage > 0 && cacheNextStartMessage < now)
                    {
                        _instance.SendToAllPlayerControllers(key: "Alert.NotAllowedAllDayMessage", style: 1, args: new object[] { currentSchedule.end.day, $"{ToTimeString(currentSchedule.end.hour)}h{ToTimeString(currentSchedule.end.minute)}" });
                        cacheNextStartMessage = now.AddMinutes(_cfg.Message.MinuteToStartMessage);
                    }
                }
                else if (currentSchedule.status == ScheduleStatus.AllowDuringSchedule)
                {
                    if (currentSchedule.isRaidTime)
                    {
                        if (_cfg.Message.AllowedMessage && !raidStartedAlert)
                        {
                            raidStartedAlert = true;
                            _instance.SendToAllPlayerControllers(key: "Alert.AllowedMessage");
                        }

                        if (_cfg.Message.MinuteToEndMessage > 0 && cacheNextEndMessage < now)
                        {
                            if (now.DayOfWeek == currentSchedule.end.day)
                                _instance.SendToAllPlayerControllers(key: "Alert.MinuteToEndMessageSameDay", style: 1, args: new object[] { GetRemainingTime(currentSchedule.end.day, currentSchedule.end.hour, currentSchedule.end.minute) });
                            else
                                _instance.SendToAllPlayerControllers(key: "Alert.MinuteToEndMessage", style: 1, args: new object[] { currentSchedule.end.day, $"{ToTimeString(currentSchedule.end.hour)}h{ToTimeString(currentSchedule.end.minute)}" });

                            cacheNextEndMessage = now.AddMinutes(_cfg.Message.MinuteToEndMessage);
                        }
                    }
                    else
                    {
                        if (_cfg.Message.FinishedMessage && raidStartedAlert)
                        {
                            raidStartedAlert = false;
                            _instance.SendToAllPlayerControllers(key: "Alert.NotAllowedMessage", style: 1);
                        }

                        if (_cfg.Message.MinuteToStartMessage > 0 && cacheNextStartMessage < now)
                        {
                            if (now.DayOfWeek == currentSchedule.end.day)
                                _instance.SendToAllPlayerControllers(key: "Alert.MinuteToStartMessageSameDay", args: new object[] { GetRemainingTime(currentSchedule.end.day, currentSchedule.end.hour, currentSchedule.end.minute) });
                            else
                                _instance.SendToAllPlayerControllers(key: "Alert.MinuteToStartMessage", args: new object[] { currentSchedule.end.day, $"{ToTimeString(currentSchedule.end.hour)}h{ToTimeString(currentSchedule.end.minute)}" });

                            cacheNextStartMessage = now.AddMinutes(_cfg.Message.MinuteToStartMessage);
                        }
                    }
                }
            }

            public string GetRemainingTime(DayOfWeek day, int hour, int minute)
            {
                DateTime nextRaidEndTime = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);

                if (nextRaidEndTime <= now)
                    nextRaidEndTime = nextRaidEndTime.AddDays(1);

                TimeSpan timeSpan = nextRaidEndTime - now;
                int hours = timeSpan.Hours;
                int minutes = timeSpan.Minutes;
                return $"{hours}h{ToTimeString(minutes + 1)}m";
            }

            public List<string> AllowDuringScheduleNoData(BasePlayer player = null)
            {
                List<string> scheduleNoData = null;

                foreach (var scheduleStatus in _cfg.Schedule)
                {
                    if (scheduleStatus.Value.Status == ScheduleStatus.AllowDuringSchedule && scheduleStatus.Value.TimeRange.Count == 0)
                        (scheduleNoData ?? (scheduleNoData = new List<string>())).Add(ToDateLangFormat(scheduleStatus.Key, player != null ? _playerSetting.data[player.userID].Language : "en"));
                }

                return scheduleNoData;
            }

            private void AlertAllowDuringScheduleNoData() => _instance.PrintWarning(Lang("AdminPanel.Error.AllowDuringScheduleNoData", null, new object[] { string.Join(", ", AllowDuringScheduleNoData()) }));

            public bool CfgScheduleHasChanged() => configChanged;

            private DateTime UpdateTime() => DateTime.UtcNow.AddHours(Convert.ToInt32(_cfg.Global.Timezone));
            private void OnDestroy()
            {
                CancelInvoke(UpdateController);
                CancelInvoke(NotifyPlayers);
            }
        }

        #endregion

        #region PLAYER CONTOLLER

        internal class PlayerController : FacepunchBehaviour
        {
            private BasePlayer player;
            private PlayerSetting.Setting playerSetting;
            private PlayerData.Data playerData;
            public AlertMessage alertMessage;
            public DateTime cachedTime;
            public DayOfWeek currentDay;
            public bool staticUICreated;
            public bool forceUpdate;
            public bool locker;
            public ulong inTc;

            private void Awake()
            {
                player = GetComponent<BasePlayer>();

                if (!_playerSetting.data.ContainsKey(player.userID))
                    _playerSetting.data[player.userID] = new PlayerSetting.Setting();

                playerSetting = _playerSetting.data[player.userID];

                if (!_playerData.data.ContainsKey(player.userID))
                    _playerData.data[player.userID] = new PlayerData.Data();

                playerData = _playerData.data[player.userID];

                if(!player.GetComponent<AlertMessage>())
                    alertMessage = player.gameObject.AddComponent<AlertMessage>();

                cachedTime = _mainController.currentSchedule.cachedTime;
                currentDay = _mainController.now.DayOfWeek;
                InvokeRepeating(DrawPlayerGameUI, 0f, _cfg.Ui.Rate);
                InvokeRepeating(UpdatePlayerTimes, 0f, 60f);
            }

            public void DrawLocker()
            {
                CuiHelper.AddUi(player, UI.CreateContainer(name: UI_PLAYER_PANEL_LOCKER, needsCursor: true, parent: "Under"));
                locker = true;
            }

            public void DrawPanel()
            {
                if (!locker)
                    DrawLocker();

                var container = UI.CreateContainer(name: UI_PLAYER_PANEL, color: GetColor(hex: "#000000", alpha: 0.75f), parent: "Hud", blur: false);
                UI.CreatePanel(ref container, parent: UI_PLAYER_PANEL, name: "playerPanel", color: GetColor(hex: "#000000", alpha: 0.8f), xMin: 0.35f, xMax: 0.65f, yMin: 0.2f, yMax: 0.85f, blur: true);
                UI.CreateSprite(ref container, parent: "playerPanel", name: "left", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), xMax: 0.005f);
                UI.CreateSprite(ref container, parent: "playerPanel", name: "right", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), xMin: 0.995f);
                UI.CreatePanel(ref container, parent: "playerPanel", name: "main", color: GetColor(hex: "#000000", alpha: 0.7f), xMin: 0.005f, xMax: 0.995f);
                UI.CreatePanel(ref container, parent: "main", name: "title", xMin: 0.05f, xMax: 0.95f, yMin: 0.9f);
                UI.CreateText(ref container, parent: "title", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.3f), text: $"» {_instance.Title.ToUpper()}", size: 25, xMin: 0.05f, align: TextAnchor.LowerLeft, font: "robotocondensed-bold.ttf", outlineColor: GetColor(hex: "#000000", alpha: 0.1f));
                UI.CreatePanel(ref container, parent: "main", name: "marge", xMin: 0.05f, xMax: 0.95f, yMin: 0.1f, yMax: 0.875f);

                if (_adminControllers.ContainsKey(player.userID))
                {
                    UI.CreateSprite(ref container, parent: "title", name: "admin", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_ERROR, alpha: 0.4f), outlineColor: GetColor(hex: "#000000", alpha: 0.1f), xMin: 0.75f, yMax: 0.7f, DistanceX: 2f);
                    UI.CreatePanel(ref container, parent: "admin", name: "separator", color: GetColor(hex: "#000000", alpha: 0.6f), xMax: 0.991f, yMax: 0.05f);
                    UI.CreateProtectedButton(ref container, parent: "title", name: "btn", xMin: 0.75f, yMax: 0.7f, command: "craidcontroller.admin.open");
                    UI.CreateText(ref container, parent: "btn", color: GetColor(hex: COLOR_ERROR), text: _instance.lang.GetMessage("PlayerPanel.Btn.Admin", _instance, player.UserIDString), size: 11, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                }

                int index = 0;
                const float height = 0.1f;
                const float spacing = 0.02f;
                float yMax1 = 1 - (index * (height + spacing));
                float yMin1 = yMax1 - height;
                UI.CreatePanel(ref container, parent: "marge", name: "language", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: yMin1, yMax: yMax1);
                UI.CreateImage(ref container, parent: "language", name: "img", image: "assets/icons/translate.png", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), outlineColor: GetColor(hex: "#000000", alpha: 0.8f), xMin: 0.05f, xMax: 0.1f, yMin: 0.25f, yMax: 0.75f, DistanceX: 0.1f, DistanceY: 0.1f);
                UI.CreateText(ref container, parent: "language", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("PlayerPanel.Language", _instance, player.UserIDString), size: 10, xMin: 0.125f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                index++;
                yMax1 = 1 - (index * (height + spacing));
                yMin1 = yMax1 - height;
                UI.CreatePanel(ref container, parent: "marge", name: "languageInput", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.35f), xMin: 0.1f, yMin: yMin1, yMax: yMax1);
                UI.CreateText(ref container, parent: "languageInput", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("PlayerPanel.Language.Input", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.5f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                UI.CreatePanel(ref container, parent: "languageInput", name: "languageInputLabel", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.55f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                UI.CreateSlider(ref container, parent: "languageInputLabel", name: "slider", current: playerSetting.Language, command: "craidcontroller.player.setting Language"); index++;

                if (_cfg.Ui.ActiveUi == ActiveUI.Activated)
                {
                    if (_cfg.Ui.AllowPlayerHide)
                    {
                        float yMax = 1 - (index * (height + spacing));
                        float yMin = yMax - height;
                        UI.CreatePanel(ref container, parent: "marge", name: "hideUi", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: yMin, yMax: yMax);
                        UI.CreateImage(ref container, parent: "hideUi", name: "img", image: "assets/icons/clear_list.png", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), outlineColor: GetColor(hex: "#000000", alpha: 0.8f), xMin: 0.05f, xMax: 0.1f, yMin: 0.25f, yMax: 0.75f, DistanceX: 0.1f, DistanceY: 0.1f);
                        UI.CreateText(ref container, parent: "hideUi", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("PlayerPanel.HideUi", _instance, player.UserIDString), size: 10, xMin: 0.125f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        index++;
                        yMax = 1 - (index * (height + spacing));
                        yMin = yMax - height;
                        UI.CreatePanel(ref container, parent: "marge", name: "hideUiInput", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.35f), xMin: 0.1f, yMin: yMin, yMax: yMax);
                        UI.CreateText(ref container, parent: "hideUiInput", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("PlayerPanel.HideUi.Input", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.5f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        UI.CreatePanel(ref container, parent: "hideUiInput", name: "hideUiInputLabel", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.875f, xMax: 0.95f, yMin: 0.2f, yMax: 0.75f);
                        UI.CreateCheckbox(ref container, parent: "hideUiInputLabel", name: "checkbox", color: GetColor(COLOR_PRIMARY, 0.7f), command: $"craidcontroller.player.setting HideUi {playerSetting.HideUi}", isOn: playerSetting.HideUi);
                        index++;
                    }

                    if (_cfg.Ui.AllowPlayerChange && !playerSetting.HideUi)
                    {
                        float yMax = 1 - (index * (height + spacing));
                        float yMin = yMax - height;
                        UI.CreatePanel(ref container, parent: "marge", name: "ui", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: yMin, yMax: yMax);
                        UI.CreateImage(ref container, parent: "ui", name: "img", image: "assets/icons/workshop.png", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), outlineColor: GetColor(hex: "#000000", alpha: 0.8f), xMin: 0.05f, xMax: 0.1f, yMin: 0.25f, yMax: 0.75f, DistanceX: 0.1f, DistanceY: 0.1f);
                        UI.CreateText(ref container, parent: "ui", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("PlayerPanel.Ui", _instance, player.UserIDString), size: 10, xMin: 0.125f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        index++;
                        yMax = 1 - (index * (height + spacing));
                        yMin = yMax - height;
                        UI.CreatePanel(ref container, parent: "marge", name: "uiInput", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.35f), xMin: 0.1f, yMin: yMin, yMax: yMax);
                        UI.CreateText(ref container, parent: "uiInput", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("PlayerPanel.Ui.Input", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.3f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        UI.CreatePanel(ref container, parent: "uiInput", name: "uiInputLabelUi", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.35f, xMax: 0.69f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreateSlider(ref container, parent: "uiInputLabelUi", name: "sliderUi", current: _instance._uiList[playerSetting.Ui], command: "craidcontroller.player.setting Ui");
                        UI.CreatePanel(ref container, parent: "uiInput", name: "uiInputLabelSide", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.7f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreateSlider(ref container, parent: "uiInputLabelSide", name: "sliderSide", current: playerSetting.Side == 0 ? _instance.lang.GetMessage("AdminPanel.Setting.Ui.Side.0", _instance, player.UserIDString) : _instance.lang.GetMessage("AdminPanel.Setting.Ui.Side.1", _instance, player.UserIDString), command: "craidcontroller.player.setting Side");
                        index++;
                    }
                }

                UI.CreatePanel(ref container, parent: "main", name: "close", yMax: 0.1f);
                UI.CreateProtectedButton(ref container, parent: "close", textColor: GetColor(COLOR_ERROR), text: _instance.lang.GetMessage("Btn.Close", _instance, player.UserIDString), size: 11, command: "craidcontroller.player.close");
                CuiHelper.AddUi(player, container);
            }

            private void DrawPlayerGameUI()
            {
                if(forceUpdate || cachedTime < _mainController.currentSchedule.cachedTime || currentDay != _mainController.now.DayOfWeek)
                {
                    DestroyPlayerGameUI(true);
                    DestroyCustomStatusFramework();
                    cachedTime = _mainController.currentSchedule.cachedTime;
                    currentDay = _mainController.now.DayOfWeek;
                    forceUpdate = false;
                    return;
                }

                if (_instance.CustomStatusFramework != null && _cfg.Ui.ActiveUi == ActiveUI.CustomStatusFramework)
                {
                    CreatePlayerCustomStatusFramework();
                    return;
                }

                if (_cfg.Ui.ActiveUi == ActiveUI.Disabled)
                    return;

                if (_cfg.Ui.AllowPlayerHide && playerSetting.HideUi)
                    return;

                if (_cfg.Ui.HideNotTime && !_mainController.currentSchedule.isRaidTime)
                    return;

                if (_cfg.Ui.HideIsTime && _mainController.currentSchedule.isRaidTime)
                    return;

                if(!staticUICreated)
                {
                    var containerStatic = UI.CreateContainer(name: UI_PLAYER_UI_STATIC, fadeIn: 0, fadeOut: 1f, parent: "Under");
                    CreatePlayerGameUIStatic(ref containerStatic, UI_PLAYER_UI_STATIC, _cfg.Ui.AllowPlayerChange ? playerSetting.Ui : _cfg.Ui.DefaultUi);
                    CuiHelper.AddUi(player, containerStatic);
                    staticUICreated = true;
                }

                DestroyPlayerGameUI();
                var containerProgress = UI.CreateContainer(name: UI_PLAYER_UI_PROGRESS, fadeIn: 0, fadeOut: 1f, parent: "Under");
                CreatePlayerGameUIProgress(ref containerProgress, UI_PLAYER_UI_PROGRESS, _cfg.Ui.AllowPlayerChange ? playerSetting.Ui : _cfg.Ui.DefaultUi);
                CuiHelper.AddUi(player, containerProgress);
            }

            public void DrawTCBypassPanel(ulong id)
            {
                inTc = id;
                var container = UI.CreateContainer(name: UI_PLAYER_TC_PANEL, color: GetColor(hex: "#FFFFFF", alpha: 0.1f), xMin: 0.655f, xMax: 0.825f, yMin: 0.024f, yMax: 0.115f, fadeIn: 0, fadeOut: 0f);
                UI.CreatePanel(ref container, parent: UI_PLAYER_TC_PANEL, name: "tcPanel");
                UI.CreatePanel(ref container, parent: "tcPanel", name: "title", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.025f, xMax: 0.975f, yMin: 0.6f, yMax: 0.92f);
                UI.CreatePanel(ref container, parent: "title", name: "underline", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.8f), yMax: 0.025f);
                UI.CreateImage(ref container, parent: "title", name: "img", image: "assets/icons/grenade.png", color: GetColor(hex: "#FFFFFF", alpha: 0.5f), outlineColor: GetColor(hex: "#000000", alpha: 0.8f), xMin: 0.025f, xMax: 0.1f, yMin: 0.2f, yMax: 0.8f, DistanceX: 0.1f, DistanceY: 0.1f);
                UI.CreateText(ref container, parent: "title", color: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: _instance.lang.GetMessage("Ui.TcBypass.Title", _instance, player.UserIDString), size: 8, xMin: 0.125f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));

                UI.CreatePanel(ref container, parent: "tcPanel", name: "field", color: GetColor(hex: !_tcController.TcDisabledProtection(id) ? "#5f3333" : "#526434"), xMin: 0.025f, xMax: 0.975f, yMin: 0.08f, yMax: 0.5f);

                if (!_mainController.currentSchedule.isRaidTime)
                {
                    if (!_tcController.TcDisabledProtection(id))
                        UI.CreateProtectedButton(ref container, parent: "field", textColor: GetColor(hex: "#be3b3b"), text: _instance.lang.GetMessage("Ui.TcBypass.Disable", _instance, player.UserIDString), size: 10, command: $"craidcontroller.player.tc.bypass disable {id}");
                    else
                        UI.CreateText(ref container, parent: "field", color: GetColor(hex: "#90c13b"), text: _instance.lang.GetMessage("Ui.TcBypass.NotUpdateNow", _instance, player.UserIDString), size: 10);
                }
                else
                {
                    if (!_tcController.TcDisabledProtection(id))
                        UI.CreateProtectedButton(ref container, parent: "field", textColor: GetColor(hex: "#be3b3b"), text: _instance.lang.GetMessage("Ui.TcBypass.Disable", _instance, player.UserIDString), size: 10, command: $"craidcontroller.player.tc.bypass disable {id}");
                    else
                        UI.CreateProtectedButton(ref container, parent: "field", textColor: GetColor(hex: "#90c13b"), text: _instance.lang.GetMessage("Ui.TcBypass.Enable", _instance, player.UserIDString), size: 10, command: $"craidcontroller.player.tc.bypass enable {id}");
                }

                CuiHelper.AddUi(player, container);
            }

            public void DrawTCBypassPanelModal(ulong id)
            {
                inTc = id;
                var container = UI.CreateContainer(name: UI_PLAYER_TC_PANEL + "modal", color: GetColor(hex: "#000000", alpha: 0.75f), blur: true);
                UI.CreatePanel(ref container, parent: UI_PLAYER_TC_PANEL + "modal", name: "tcPanel", color: GetColor(hex: "#000000", alpha: 0.8f), xMin: 0.35f, xMax: 0.65f, yMin:0.4f, yMax:0.6f);
                UI.CreateSprite(ref container, parent: "tcPanel", name: "left", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), xMax: 0.005f);
                UI.CreateSprite(ref container, parent: "tcPanel", name: "right", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), xMin: 0.995f);
                UI.CreatePanel(ref container, parent: "tcPanel", name: "main", color: GetColor(hex: "#000000", alpha: 0.7f), xMin: 0.005f, xMax: 0.995f);
                UI.CreatePanel(ref container, parent: "tcPanel", name: "title", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), xMin: 0.025f, xMax: 0.975f, yMin: 0.6f, yMax: 0.92f);
                UI.CreateImage(ref container, parent: "title", name: "img", image: "assets/icons/info.png", color: GetColor(hex: "#FFFFFF", alpha: 0.5f), outlineColor: GetColor(hex: "#000000", alpha: 0.8f), xMin: 0.025f, xMax: 0.1f, yMin: 0.2f, yMax: 0.8f, DistanceX: 0.1f, DistanceY: 0.1f);
                UI.CreateText(ref container, parent: "title", color: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: _instance.lang.GetMessage("Ui.TcBypass.Modal", _instance, player.UserIDString), size: 11, xMin: 0.125f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                UI.CreatePanel(ref container, parent: "tcPanel", name: "field", xMin: 0.025f, xMax: 0.975f, yMin: 0.08f, yMax: 0.5f);
                UI.CreateProtectedButton(ref container, parent: "field", color: GetColor(hex: "#526434"), textColor: GetColor(hex: "#90c13b"), text: _instance.lang.GetMessage("Btn.Confirm", _instance, player.UserIDString), size: 15, xMin: 0.05f, xMax: 0.45f, command: $"craidcontroller.player.tc.bypass confirm {id}");
                UI.CreateProtectedButton(ref container, parent: "field", color: GetColor(hex: "#5f3333"), textColor: GetColor(hex: "#be3b3b"), text: _instance.lang.GetMessage("Btn.Cancel", _instance, player.UserIDString), size: 15, xMin: 0.55f, xMax: 0.95f, command: $"craidcontroller.player.tc.bypass cancel {id}");
                CuiHelper.AddUi(player, container);
            }

            private void DestroyPlayerGameUI(bool alsoStatic = false)
            {
                if (alsoStatic)
                {
                    CuiHelper.DestroyUi(player, UI_PLAYER_UI_STATIC);
                    staticUICreated = false;
                }

                CuiHelper.DestroyUi(player, UI_PLAYER_UI_PROGRESS);
            }

            private void DestroyCustomStatusFramework()
            {
                if (_instance.CustomStatusFramework == null)
                    return;

                _instance.CustomStatusFramework.Call("ClearStatus", player, "canRaid");
                _instance.CustomStatusFramework.Call("ClearStatus", player, "!canRaid");
            }

            public void CreatePlayerCustomStatusFramework()
            {
                bool isRaidTime = _mainController.currentSchedule.isRaidTime;
                string statusKey = isRaidTime ? "canRaid" : "!canRaid";
                string oppositeStatusKey = isRaidTime ? "!canRaid" : "canRaid";
                string colorBg = isRaidTime ? GetColor(hex: "#526434") : GetColor(hex: "#5f3333");
                string color = isRaidTime ? GetColor(hex: "#90c13b") : GetColor(hex: "#be3b3b");
                string text = _instance.lang.GetMessage(isRaidTime ? "Ui.CustomStatusFramework.Allowed" : "Ui.CustomStatusFramework.NotAllowed", _instance, player.UserIDString);
                string subText = _mainController.GetRemainingTime(_mainController.currentSchedule.end.day, _mainController.currentSchedule.end.hour, _mainController.currentSchedule.end.minute);

                if (_instance.CustomStatusFramework.Call<bool>("HasStatus", player, oppositeStatusKey))
                    _instance.CustomStatusFramework.Call("ClearStatus", player, oppositeStatusKey);

                if (!_instance.CustomStatusFramework.Call<bool>("HasStatus", player, statusKey))
                    _instance.CustomStatusFramework.Call("SetStatus", player, statusKey, colorBg, text, color, subText, color, "CFL_canRaid", color);
                else
                    _instance.CustomStatusFramework.Call("UpdateStatus", player, statusKey, colorBg, text, color, subText, color, "CFL_canRaid", color);
            }

            public void CreatePlayerGameUIStatic(ref CuiElementContainer container, string parent, int ui)
            {
                switch (ui)
                {
                    case 0:

                        UI.CreatePanel(ref container, parent: parent, name: parent + ui, color: GetColor(hex: _cfg.Ui.BackgroundColor, alpha: _cfg.Ui.BackgroundOpacity), xMin: GetSide() == 0 ? 0.01f : 0.78f, xMax: GetSide() == 0 ? 0.22f : 0.99f, yMin: _cfg.Ui.Ymax - 0.1f, yMax: _cfg.Ui.Ymax);
                        UI.CreatePanel(ref container, parent: parent + ui, name: "marge", xMin: 0.025f, xMax: 0.975f);
                        UI.CreateText(ref container, parent: "marge", name: "day", color: GetColor(hex: "#ffffff"), text: ToDateLangFormat(currentDay, playerSetting.Language).ToUpper(), size: 18, yMin: 0.6f, align: TextAnchor.MiddleLeft, font: "robotocondensed-bold.ttf", outlineColor: GetColor(hex: "#000000", alpha: 0.1f));
                        UI.CreateText(ref container, parent: "marge", name: "title", color: GetColor(hex: "#ffffff", alpha: 0.6f), text: _mainController.currentSchedule.isRaidTime ? _instance.lang.GetMessage("Ui.RaidAllowed", _instance, player.UserIDString) : _instance.lang.GetMessage("Ui.RaidNotAllowed", _instance, player.UserIDString), size: 14, yMin: 0.6f, align: TextAnchor.MiddleRight, outlineColor: GetColor(hex: "#000000", alpha: 0.1f));
                        UI.CreatePanel(ref container, parent: "marge", name: "progressBar", color: GetColor(hex: "#000000", alpha: 0.5f), yMin: 0.5f, yMax: 0.65f);
                        UI.CreatePanel(ref container, parent: "marge", name: "start", xMax: 0.45f, yMin: 0.225f, yMax: 0.45f);
                        UI.CreateImage(ref container, parent: "start", name: "imgStart", image: "assets/icons/stopwatch.png", color: GetColor(hex: "#FFFFFF", alpha: 0.85f), outlineColor: GetColor(hex: "#000000", alpha: 0.8f), xMin: 0.025f, xMax: 0.105f, yMin: 0.25f, yMax: 0.75f, DistanceX: 0.1f, DistanceY: 0.1f);
                        UI.CreateText(ref container, parent: "start", name: "text", color: GetColor(hex: "#FFFFFF", alpha: 0.6f), text: $" » {_mainController.currentSchedule.StartValue(player)}", size: 9, xMin: 0.125f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.1f));
                        UI.CreatePanel(ref container, parent: "marge", name: "end", xMax: 0.45f, yMax: 0.225f);
                        UI.CreateImage(ref container, parent: "end", name: "imgEnd", image: "assets/icons/sleeping.png", color: GetColor(hex: "#FFFFFF", alpha: 0.85f), outlineColor: GetColor(hex: "#000000", alpha: 0.8f), xMin: 0.025f, xMax: 0.105f, yMin: 0.25f, yMax: 0.75f, DistanceX: 0.1f, DistanceY: 0.1f);
                        UI.CreateText(ref container, parent: "end", name: "text", color: GetColor(hex: "#FFFFFF", alpha: 0.6f), text: $" » {_mainController.currentSchedule.EndValue(player)}", size: 9, xMin: 0.125f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.1f));

                        break;
                }
            }

            public void CreatePlayerGameUIProgress(ref CuiElementContainer container, string parent, int ui)
            {
                switch (ui)
                {
                    case 0:

                        UI.CreatePanel(ref container, parent: parent, name: parent + ui, xMin: GetSide() == 0 ? 0.01f : 0.78f, xMax: GetSide() == 0 ? 0.22f : 0.99f, yMin: _cfg.Ui.Ymax - 0.1f, yMax: _cfg.Ui.Ymax);
                        UI.CreatePanel(ref container, parent: parent + ui, name: "marge", xMin: 0.025f, xMax: 0.975f);
                        UI.CreatePanel(ref container, parent: "marge", name: "progressBar", color: GetColor(hex: _mainController.currentSchedule.isRaidTime ? COLOR_SUCCESS : COLOR_ERROR), xMin: 0.005f, xMax: 0.995f - _mainController.currentSchedule.ProgressBarMaxWidth(), yMin: 0.51f, yMax: 0.64f);
                        UI.CreateText(ref container, parent: "marge", name: "percent", color: GetColor(hex: "#ffffff"), text: $"{_mainController.currentSchedule.ProgressPercent()}%", size: 8, xMin: 0.005f, xMax: 0.995f, yMin: 0.51f, yMax: 0.64f, outlineColor: GetColor(hex: "#000000", alpha: 0.1f));

                        break;
                }
            }

            public void DestroyUI(bool panel = false, bool locker = false, bool ui = false, bool tc = false)
            {
                if(panel)
                    CuiHelper.DestroyUi(player, UI_PLAYER_PANEL);

                if (ui)
                {
                    DestroyPlayerGameUI(true);
                    DestroyCustomStatusFramework();
                }
                
                if (locker)
                {
                    CuiHelper.DestroyUi(player, UI_PLAYER_PANEL_LOCKER);
                    this.locker = false;
                }

                if (tc)
                {
                    CuiHelper.DestroyUi(player, UI_PLAYER_TC_PANEL);
                    CuiHelper.DestroyUi(player, UI_PLAYER_TC_PANEL + "modal");
                }
            }

            private int GetSide() => !_cfg.Ui.AllowPlayerChange ? _cfg.Ui.Side : playerSetting.Side;
            private void UpdatePlayerTimes() => ++playerData.TimePlayed;
            private void OnDestroy()
            {
                CancelInvoke(DrawPlayerGameUI);
                CancelInvoke(UpdatePlayerTimes);
                DestroyUI(panel: true, locker: true, ui: true, tc: true);
                Destroy(alertMessage);
            }
        }

        #endregion

        #region ADMIN CONTOLLER

        internal class AdminController : FacepunchBehaviour
        {
            private BasePlayer player;
            public AdminPanelTab activeTab;
            public string feedTab;
            public int feedPage;
            public int feedNews;
            public bool locker;
            public int tcProtectItemPage;
            public int tcNoProtectItemPage;
            public string scheduleActiveDay = string.Empty;
            public TimeRange timeRangeCurrent = new TimeRange() { StartHour = -1, StartMinute = -1, EndHour = -1, EndMinute = -1 };
            public bool timeRangeCurrentEndOfDay = true;

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                activeTab = AdminPanelTab.Global;
                feedTab = _instance.Title;
            }

            public void DrawLocker()
            {
                CuiHelper.AddUi(player, UI.CreateContainer(name: UI_ADMIN_PANEL_LOCKER, needsCursor: true));
                locker = true;
            }

            public void DrawPanel()
            {
                if (!locker)
                    DrawLocker();

                var container = UI.CreateContainer(name: UI_ADMIN_PANEL, color: GetColor(hex: "#000000", alpha: 0.75f), blur: true);
                UI.CreatePanel(ref container, parent: UI_ADMIN_PANEL, name: "background", color: GetColor(hex: "#000000", alpha: 0.8f));
                UI.CreatePanel(ref container, parent: "background", name: "header", color: GetColor(hex: "#000000", alpha: 0.6f), yMin: 0.85f);
                UI.CreatePanel(ref container, parent: "header", name: "title", xMax: 0.6f, yMin: 0.45f, yMax: 0.98f);
                UI.CreateText(ref container, parent: "title", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.3f), text: $"» {_instance.Title.ToUpper()}", size: 25, xMin: 0.1f, align: TextAnchor.LowerLeft, font: "robotocondensed-bold.ttf", outlineColor: GetColor(hex: "#000000", alpha: 0.1f));
                UI.CreatePanel(ref container, parent: "title", name: "separator", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.3f), yMax: 0.01f);
                UI.CreatePanel(ref container, parent: "header", name: "user", xMin: 0.75f, xMax: 0.9f, yMin: 0.4f, yMax: 0.8f);
                UI.CreateSprite(ref container, parent: "user", name: "avatar", sprite: "assets/content/ui/ui.background.transparent.radial.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), xMin: 0.05f, xMax: 0.2f, yMin: 0.2f, yMax: 0.8f);
                UI.CreateImage(ref container, parent: "avatar", image: GetImage(name: player.UserIDString), color: GetColor(hex: "#FFFFFF"), xMin: 0.05f, xMax: 0.92f, yMin: 0.05f, yMax: 0.92f);
                UI.CreatePanel(ref container, parent: "user", name: "name", xMin: 0.2f, xMax: 0.95f, yMin: 0.2f, yMax: 0.8f);
                UI.CreateText(ref container, parent: "name", color: GetColor(hex: "#9da6ab"), text: player.displayName, size: 14, xMin: 0.1f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.1f));
                UI.CreatePanel(ref container, parent: "header", name: "close", xMin: 0.825f, xMax: 0.9f, yMin: 0.005f, yMax: 0.3f);
                UI.CreateSprite(ref container, parent: "close", name: "sprite", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_ERROR, alpha: 0.5f), outlineColor: GetColor(hex: "#000000", alpha: 0.1f), DistanceX: 5f);
                UI.CreatePanel(ref container, "sprite", "separator", GetColor(hex: "#000000", alpha: 0.6f), xMax: 0.991f, yMax: 0.05f);
                UI.CreateImage(ref container, parent: "close", image: "assets/icons/exit.png", color: GetColor(hex: COLOR_ERROR, alpha: 0.5f), xMin: 0.7f, xMax: 0.85f, yMin: 0.3f, yMax: 0.75f);
                UI.CreateProtectedButton(ref container, parent: "close", textColor: GetColor(hex: COLOR_ERROR), text: _instance.lang.GetMessage("Btn.Close", _instance, player.UserIDString), size: 14, xMin: 0.2f, command: "craidcontroller.admin.close", align: TextAnchor.MiddleLeft);
                UI.CreatePanel(ref container, parent: "header", name: "navbar", xMin: 0.1f, xMax: 0.6f, yMin: 0.005f, yMax: 0.3f);
                DrawTabs(ref container, parent: "navbar", activeTab: activeTab);
                UI.CreatePanel(ref container, parent: "header", name: "separator", color: GetColor(hex: "#000000", alpha: 0.2f), yMax: 0.0005f);
                UI.CreatePanel(ref container, parent: "background", name: "page", xMin: 0.1f, xMax: 0.9f, yMin: 0.05f, yMax: 0.8f);
                DrawActivePage(ref container, parent: "page", activeTab: activeTab);
                CuiHelper.AddUi(player, container);
            }

            private void DrawTabs(ref CuiElementContainer container, string parent, AdminPanelTab activeTab)
            {
                int tabIndex = 0;
                float offset = tabIndex == 0 ? 0 : (1f / Enum.GetNames(typeof(AdminPanelTab)).Length);
                float width = 1f / Enum.GetNames(typeof(AdminPanelTab)).Length;

                foreach (AdminPanelTab tab in Enum.GetValues(typeof(AdminPanelTab)))
                {
                    if (!_cfg.Global.BlockDamage && tab == AdminPanelTab.Bypass)
                        continue;

                    string tabTitle = _instance.lang.GetMessage($"AdminPanel.Tab.{tab}", _instance, player.UserIDString).ToUpper();
                    string textColor = (tab == activeTab) ? GetColor(hex: COLOR_PRIMARY) : GetColor(hex: "#9da6ab");

                    if (tab == activeTab)
                    {
                        UI.CreateSprite(ref container, parent: parent, name: "activeTab", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), outlineColor: GetColor(hex: "#000000", alpha: 0.1f), xMin: offset, xMax: offset + width, DistanceX: 5f);
                        UI.CreatePanel(ref container, parent: "activeTab", name: "separator", color: GetColor(hex: "#000000", alpha: 0.6f), xMax: 0.991f, yMax: 0.05f);
                    }

                    UI.CreateProtectedButton(ref container, parent: parent, name: "tab", xMin: offset, xMax: offset + width, command: $"craidcontroller.admin.switchtab {(int)tab}");
                    UI.CreateText(ref container, parent: "tab", color: textColor, text: tabTitle, size: 14, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                    offset += width;
                    tabIndex++;
                }
            }

            private void DrawActivePage(ref CuiElementContainer container, string parent, AdminPanelTab activeTab)
            {
                #region PLUGIN INFO

                UI.CreatePanel(ref container, parent: parent, name: "info", xMin: 0.70f, xMax: 0.95f, yMin: 0.75f);
                UI.CreateSprite(ref container, parent: "info", name: "left", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), xMax: 0.005f);
                UI.CreateSprite(ref container, parent: "info", name: "right", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), xMin: 0.995f);
                UI.CreatePanel(ref container, parent: "info", name: "main", color: GetColor(hex: "#000000", alpha: 0.7f), xMin: 0.005f, xMax: 0.995f);
                UI.CreatePanel(ref container, parent: "info", name: "infoTitle", xMin: 0.05f, xMax: 0.95f, yMin: 0.8f, yMax: 0.97f);
                UI.CreateImage(ref container, parent: "infoTitle", image: "assets/icons/examine.png", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), outlineColor: GetColor(hex: "#000000", alpha: 0.8f), xMin: 0.90f, xMax: 0.97f, yMin: 0.25f, yMax: 0.8f, DistanceX: 0.5f, DistanceY: 0.5f);
                UI.CreateText(ref container, parent: "infoTitle", color: GetColor(hex: COLOR_PRIMARY), text: _instance.lang.GetMessage("AdminPanel.Tab.Main.Info.Title", _instance, player.UserIDString), size: 14, xMin: 0.05f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                UI.CreatePanel(ref container, parent: "info", name: "panel", xMin: 0.05f, xMax: 0.95f, yMin: 0.05f, yMax: 0.85f);
                UI.CreatePanel(ref container, parent: "panel", name: "imagePanel", xMax: 0.365f, yMin: 0.1f, yMax: 0.8f);
                UI.CreateSprite(ref container, parent: "imagePanel", name: "bg", sprite: "assets/content/ui/ui.background.transparent.radial.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f));
                UI.CreateImage(ref container, parent: "bg", image: GetImage(name: _instance.Title), color: GetColor(hex: "#FFFFFF"), xMin: 0.025f, xMax: 0.97f, yMin: 0.025f, yMax: 0.97f);
                UI.CreatePanel(ref container, parent: "panel", name: "rightPanel", xMin: 0.4f, yMin: 0.1f, yMax: 0.8f);
                UI.CreatePanel(ref container, parent: "rightPanel", name: "pluginAuthor", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: 0.76f, yMax: 0.96f);
                UI.CreateText(ref container, parent: "pluginAuthor", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Tab.Main.Info.Author", _instance, player.UserIDString), size: 9, xMin: 0.05f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                UI.CreatePanel(ref container, parent: "pluginAuthor", name: "pluginAuthorInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.55f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                UI.CreateText(ref container, parent: "pluginAuthorInput", color: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: _codeFlingApi.data.ContainsKey("file_author") ? _codeFlingApi.data["file_author"] : _instance.Author, size: 8, xMin: 0.05f, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                string versionText = _codeFlingApi.data.ContainsKey("file_version") ? _codeFlingApi.data["file_version"] : _instance.Version.ToString();
                Version version1 = new Version(versionText);
                Version version2 = new Version(_instance.Version.ToString());
                string currentVersionColor = version2 >= version1 ? GetColor(hex: "#FFFFFF", alpha: 0.5f) : GetColor("#cd4632", 0.7f);
                UI.CreatePanel(ref container, parent: "rightPanel", name: "currentVersion", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: 0.52f, yMax: 0.72f);
                UI.CreateText(ref container, parent: "currentVersion", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Tab.Main.Info.CurrentVersion", _instance, player.UserIDString), size: 9, xMin: 0.05f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                UI.CreatePanel(ref container, parent: "currentVersion", name: "currentVersionInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.55f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                UI.CreateText(ref container, parent: "currentVersionInput", color: currentVersionColor, text: _instance.Version.ToString(), size: 8, xMin: 0.05f, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                UI.CreatePanel(ref container, parent: "rightPanel", name: "codeFlingVersion", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: 0.28f, yMax: 0.48f);
                UI.CreateText(ref container, parent: "codeFlingVersion", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Tab.Main.Info.CodeflingVersion", _instance, player.UserIDString), size: 9, xMin: 0.05f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                UI.CreatePanel(ref container, parent: "codeFlingVersion", name: "codeFlingVersionInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.55f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                UI.CreateText(ref container, parent: "codeFlingVersionInput", color: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: versionText, size: 8, xMin: 0.05f, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                UI.CreatePanel(ref container, parent: "rightPanel", name: "lastUpdated", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: 0.04f, yMax: 0.24f);
                UI.CreateText(ref container, parent: "lastUpdated", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Tab.Main.Info.LastUpdated", _instance, player.UserIDString), size: 9, xMin: 0.05f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                UI.CreatePanel(ref container, parent: "lastUpdated", name: "lastUpdatedInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.55f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                UI.CreateText(ref container, parent: "lastUpdatedInput", color: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: _codeFlingApi.data.ContainsKey("file_updated") ? new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(double.Parse(_codeFlingApi.data["file_updated"])).ToString("dd/MM/yyyy") : DateTime.Now.ToString(), size: 8, xMin: 0.05f, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));

                #endregion

                #region FAST SETTING

                UI.CreatePanel(ref container, parent: parent, name: "fastSetting", xMin: 0.70f, xMax: 0.95f, yMin: 0.55f, yMax: 0.7f);
                UI.CreateSprite(ref container, parent: "fastSetting", name: "left", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), xMax: 0.005f);
                UI.CreateSprite(ref container, parent: "fastSetting", name: "right", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), xMin: 0.995f);
                UI.CreatePanel(ref container, parent: "fastSetting", name: "main", color: GetColor(hex: "#000000", alpha: 0.7f), xMin: 0.005f, xMax: 0.995f);
                UI.CreatePanel(ref container, parent: "fastSetting", name: "fastSettingTitle", xMin: 0.05f, xMax: 0.95f, yMin: 0.6f, yMax: 0.97f);
                UI.CreateImage(ref container, parent: "fastSettingTitle", image: "assets/icons/tools.png", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), outlineColor: GetColor(hex: "#000000", alpha: 0.8f), xMin: 0.90f, xMax: 0.97f, yMin: 0.25f, yMax: 0.8f, DistanceX: 0.5f, DistanceY: 0.5f);
                UI.CreateText(ref container, parent: "fastSettingTitle", color: GetColor(hex: COLOR_PRIMARY), text: _instance.lang.GetMessage("AdminPanel.Tab.Main.FastSetting.Title", _instance, player.UserIDString), size: 14, xMin: 0.05f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                UI.CreatePanel(ref container, parent: "fastSetting", name: "panel", xMin: 0.05f, xMax: 0.95f, yMin: 0.05f, yMax: 0.85f);
                UI.CreatePanel(ref container, parent: "panel", name: "enable", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: 0.2f, yMax: 0.55f);
                UI.CreateText(ref container, parent: "enable", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Tab.Main.FastSetting.Toggle", _instance, player.UserIDString), size: 9, xMin: 0.05f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                UI.CreatePanel(ref container, parent: "enable", name: "enableInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.875f, xMax: 0.95f, yMin: 0.2f, yMax: 0.75f);
                UI.CreateCheckbox(ref container, parent: "enableInput", name: "checkbox", color: GetColor(COLOR_PRIMARY, 0.7f), command: $"craidcontroller.admin.modify bool Enable {_cfg.Enable}", isOn: _cfg.Enable);

                #endregion

                #region FEED

                List<Feed.News> newsList = _feed.GetNewsList(tab: feedTab).OrderByDescending(n => n.Date).ToList();
                UI.CreatePanel(ref container, parent: parent, name: "feed", xMin: 0.70f, xMax: 0.95f, yMax: 0.50f);
                UI.CreateSprite(ref container, parent: "feed", name: "left", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), xMax: 0.005f);
                UI.CreateSprite(ref container, parent: "feed", name: "right", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), xMin: 0.995f);
                UI.CreatePanel(ref container, parent: "feed", name: "main", color: GetColor(hex: "#000000", alpha: 0.7f), xMin: 0.005f, xMax: 0.995f);
                UI.CreatePanel(ref container, parent: "feed", name: "feedTitle", xMin: 0.05f, xMax: 0.95f, yMin: 0.9f, yMax: 0.97f);
                UI.CreateImage(ref container, parent: "feedTitle", image: "assets/icons/broadcast.png", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), outlineColor: GetColor(hex: "#000000", alpha: 0.8f), xMin: 0.90f, xMax: 0.97f, yMin: 0.25f, yMax: 0.8f, DistanceX: 0.5f, DistanceY: 0.5f);
                UI.CreateText(ref container, parent: "feedTitle", color: GetColor(hex: COLOR_PRIMARY), text: _instance.lang.GetMessage("AdminPanel.Tab.Main.Feed.Title", _instance, player.UserIDString), size: 14, xMin: 0.05f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                UI.CreatePanel(ref container, parent: "feed", name: "feedTab", xMin: 0.05f, xMax: 0.95f, yMin: 0.82f, yMax: 0.88f);
                UI.CreatePanel(ref container, parent: "feedTab", name: "adminFeedTabLeft", xMax: 0.5f);
                UI.CreatePanel(ref container, parent: "feedTab", name: "adminFeedTabRight", xMin: 0.5f);
                string adminFeedTabThisTextColor = (feedTab == _instance.Title) ? GetColor(hex: COLOR_PRIMARY) : GetColor(hex: "#9da6ab");
                string adminFeedTabOtherTextColor = (feedTab == "Other") ? GetColor(hex: COLOR_PRIMARY) : GetColor(hex: "#9da6ab");

                if (feedTab == _instance.Title)
                {
                    UI.CreateSprite(ref container, parent: "adminFeedTabLeft", name: "sprite", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), outlineColor: GetColor(hex: "#000000", alpha: 0.1f), DistanceX: 2.5f);
                    UI.CreatePanel(ref container, parent: "sprite", name: "separator", color: GetColor(hex: "#000000", alpha: 0.6f), xMax: 0.991f, yMax: 0.05f);
                }
                if (feedTab == "Other")
                {
                    UI.CreateSprite(ref container, parent: "adminFeedTabRight", name: "sprite", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), outlineColor: GetColor(hex: "#000000", alpha: 0.1f), DistanceX: 2.5f);
                    UI.CreatePanel(ref container, parent: "sprite", name: "separator", color: GetColor(hex: "#000000", alpha: 0.6f), xMax: 0.991f, yMax: 0.05f);
                }

                UI.CreateProtectedButton(ref container, parent: "adminFeedTabLeft", name: "left", command: $"craidcontroller.admin.feed.tab {_instance.Title} 0");
                UI.CreateText(ref container, parent: "left", color: adminFeedTabThisTextColor, text: _instance.Title.ToUpper(), size: 11, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                UI.CreateProtectedButton(ref container, parent: "adminFeedTabRight", name: "right", textColor: adminFeedTabOtherTextColor, size: 11, command: "craidcontroller.admin.feed.tab Other 0");
                UI.CreateText(ref container, parent: "right", color: adminFeedTabOtherTextColor, text: _instance.lang.GetMessage("AdminPanel.Tab.Main.Feed.Tab.Others", _instance, player.UserIDString), size: 11, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                UI.CreatePanel(ref container, parent: "feed", name: "feedContent", xMin: 0.05f, xMax: 0.95f, yMin: 0.05f, yMax: 0.80f);
                int totalPages = ((newsList.Count - 1) / 5) + 1;

                if (feedPage < 0)
                    feedPage = 0;
                else if (feedPage >= totalPages)
                    feedPage = totalPages - 1;

                for (int i = 0; i < 5; i++)
                {
                    int newsIndex = (feedPage * 5) + i;

                    if (newsList.Count == 0)
                    {
                        UI.CreatePanel(ref container, parent: "feedContent", name: "feedContentPanel", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: 0.83f);
                        UI.CreateText(ref container, parent: "feedContentPanel", name: "noData", color: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: _instance.lang.GetMessage("AdminPanel.Tab.Main.Feed.NoData", _instance, player.UserIDString), size: 11, xMin: 0.05f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                    }

                    if (newsIndex >= newsList.Count)
                        break;

                    Feed.News news = newsList[newsIndex];
                    float offset = 1f - (i * 0.17f);
                    string date = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds((news.Date - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds).ToString("yyyy-MM-dd\\THH:mm:ss");
                    UI.CreatePanel(ref container, parent: "feedContent", name: "feedContentPanel", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: offset - 0.16f, yMax: offset);
                    UI.CreateText(ref container, parent: "feedContentPanel", name: "title", color: GetColor(hex: "#FFFFFF"), text: news.Title, size: 11, xMin: 0.05f, yMin: 0.15f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                    if (_feed.IsRead(player.userID, news.ID)) UI.CreateImage(ref container, parent: "feedContentPanel", name: "read", image: "assets/icons/check.png", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), outlineColor: GetColor(hex: "#000000", alpha: 0.4f), xMin: 0.9f, xMax: 0.95f, yMin: 0.4f, yMax: 0.8f, DistanceX: 0.5f, DistanceY: 0.5f);
                    UI.CreateText(ref container, parent: "feedContentPanel", name: "plugin", color: GetColor(hex: "#9da6ab"), text: news.Plugin, size: 7, xMax: 0.95f, yMax: 0.4f, align: TextAnchor.MiddleRight, font: "permanentmarker.ttf", outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                    UI.CreateText(ref container, parent: "feedContentPanel", name: "date", color: GetColor(hex: "#9da6ab"), text: ToDateLangFormat(date, _playerSetting.data[player.userID].Language), size: 7, xMin: 0.05f, yMax: 0.4f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                    UI.CreateProtectedButton(ref container, parent: "feedContentPanel", command: $"craidcontroller.admin.feed.show {news.ID}");
                }

                UI.CreatePanel(ref container, parent: "feed", name: "feedPagination", xMin: 0.05f, xMax: 0.95f, yMin: 0.05f, yMax: 0.1f);
                int maxVisiblePages = Math.Min(10, totalPages);
                const float squareSize = 0.05f;
                const float spaceBetweenSquares = 0.02f;
                float totalWidth = (maxVisiblePages * squareSize) + ((maxVisiblePages - 1) * spaceBetweenSquares);
                float xOffset = (1 - totalWidth) / 2;

                for (int i = 0; i < maxVisiblePages; i++)
                {
                    float xMin = xOffset + (i * (squareSize + spaceBetweenSquares));
                    float xMax = xMin + squareSize;
                    string feedPaginationColor = (feedPage == i) ? GetColor(hex: COLOR_PRIMARY) : GetColor(hex: COLOR_PRIMARY, alpha: 0.5f);
                    UI.CreateProtectedButton(ref container, parent: "feedPagination", color: feedPaginationColor, textColor: GetColor(hex: "#FFFFFF"), text: (i + 1).ToString(), size: 8, xMin: xMin, xMax: xMax, yMin: 0.25f, command: $"craidcontroller.admin.feed.tab {feedTab} {i}");
                }

                #endregion

                switch (activeTab)
                {
                    #region GLOBAL

                    case AdminPanelTab.Global:

                        UI.CreatePanel(ref container, parent: parent, name: "global", color: GetColor(hex: "#000000", alpha: 0.4f), xMin: 0.05f, xMax: 0.685f);
                        UI.CreateSprite(ref container, parent: "global", name: "left", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.1f), xMax: 0.005f);
                        UI.CreateSprite(ref container, parent: "global", name: "right", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.1f), xMin: 0.995f);
                        UI.CreatePanel(ref container, parent: "global", name: "marge", xMin: 0.05f, xMax: 0.95f, yMin: 0.05f, yMax: 0.95f);
                        UI.CreatePanel(ref container, parent: "marge", name: "command", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: 0.92f);
                        UI.CreateText(ref container, parent: "command", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Global.Command", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        UI.CreatePanel(ref container, parent: "command", name: "commandInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreateInputField(ref container, parent: "commandInput", name: "field", color: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: _cfg.Global.Command, size: 10, command: "craidcontroller.admin.modify string Global.Command ^[a-zA-Z]*$");
                        UI.CreatePanel(ref container, parent: "marge", name: "blockDamage", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: 0.82f, yMax: 0.9f);
                        UI.CreateText(ref container, parent: "blockDamage", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Global.BlockDamage", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        UI.CreatePanel(ref container, parent: "blockDamage", name: "blockDamageInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreatePanel(ref container, parent: "blockDamageInput", name: "blockDamageInputMarge", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.44f, xMax: 0.56f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreateCheckbox(ref container, parent: "blockDamageInputMarge", name: "checkbox", color: GetColor(COLOR_PRIMARY, 0.7f), command: $"craidcontroller.admin.modify bool Global.BlockDamage {_cfg.Global.BlockDamage}", isOn: _cfg.Global.BlockDamage);
                        bool blockDamage = _cfg.Global.BlockDamage;

                        if (!blockDamage)
                        {
                            UI.CreatePanel(ref container, parent: "marge", name: "cmdExecute", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.35f), xMin: 0.1f, yMin: 0.72f, yMax: 0.8f);
                            UI.CreateText(ref container, parent: "cmdExecute", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Global.CmdExecute", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                            UI.CreatePanel(ref container, parent: "cmdExecute", name: "cmdExecuteInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.61f, xMax: 0.97f, yMin: 0.15f, yMax: 0.85f);
                            UI.CreateInputField(ref container, parent: "cmdExecuteInput", name: null, color: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: _cfg.Global.CmdExecute, size: 10, command: "craidcontroller.admin.modify string Global.CmdExecute ^[a-zA-Z0-9\\W]+$");
                            UI.CreatePanel(ref container, parent: "marge", name: "cmdAllTeam", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.35f), xMin: 0.1f, yMin: 0.62f, yMax: 0.7f);
                            UI.CreateText(ref container, parent: "cmdAllTeam", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Global.CmdAllTeam", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                            UI.CreatePanel(ref container, parent: "cmdAllTeam", name: "cmdAllTeamInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.61f, xMax: 0.97f, yMin: 0.15f, yMax: 0.85f);
                            UI.CreatePanel(ref container, parent: "cmdAllTeamInput", name: "cmdAllTeamInputMarge", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.44f, xMax: 0.56f, yMin: 0.15f, yMax: 0.85f);
                            UI.CreateCheckbox(ref container, parent: "cmdAllTeamInputMarge", name: "checkbox", color: GetColor(COLOR_PRIMARY, 0.7f), command: $"craidcontroller.admin.modify bool Global.CmdAllTeam {_cfg.Global.CmdAllTeam}", isOn: _cfg.Global.CmdAllTeam);
                        }

                        UI.CreatePanel(ref container, parent: "marge", name: "timezone", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: blockDamage ? 0.72f : 0.52f, yMax: blockDamage ? 0.8f : 0.6f);
                        UI.CreateText(ref container, parent: "timezone", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Global.Timezone", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        UI.CreatePanel(ref container, parent: "timezone", name: "timezonePreview", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.81f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreateText(ref container, parent: "timezonePreview", color: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: DateTime.UtcNow.AddHours(Convert.ToInt32(_cfg.Global.Timezone)).ToString("dd/MM/yyyy (HH:mm)"), size: 10, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        UI.CreatePanel(ref container, parent: "timezone", name: "timezoneInput", color: GetColor(hex: "#000000", alpha: 0.75f), xMin: 0.82f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreateSlider(ref container, parent: "timezoneInput", name: "slider", current: _cfg.Global.Timezone, command: "craidcontroller.admin.slider list Global.Timezone _timezones");

                        if (_cfg.Global.BlockDamage)
                        {
                            UI.CreatePanel(ref container, parent: "marge", name: "raidDelay", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: blockDamage ? 0.62f : 0.42f, yMax: blockDamage ? 0.7f : 0.5f);
                            UI.CreateText(ref container, parent: "raidDelay", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Global.RaidDelay", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                            UI.CreatePanel(ref container, parent: "raidDelay", name: "raidDelayInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                            UI.CreateInputField(ref container, parent: "raidDelayInput", name: null, color: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: _cfg.Global.RaidDelay.ToString(), size: 10, command: "craidcontroller.admin.modify string Global.RaidDelay ^[0-9]*$");
                            UI.CreatePanel(ref container, parent: "marge", name: "raidProtectionDuration", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: blockDamage ? 0.52f : 0.32f, yMax: blockDamage ? 0.6f : 0.4f);
                            UI.CreateText(ref container, parent: "raidProtectionDuration", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Global.RaidProtectionDuration", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                            UI.CreatePanel(ref container, parent: "raidProtectionDuration", name: "raidProtectionDurationInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                            UI.CreateInputField(ref container, parent: "raidProtectionDurationInput", name: null, color: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: _cfg.Global.RaidProtectionDuration.ToString(), size: 10, command: "craidcontroller.admin.modify string Global.RaidProtectionDuration ^[0-9]*$");
                            //******************************************************************** TODO ****************************************************
                            //UI.CreatePanel(ref container, parent: "marge", name: "maxRaidPerPlayer", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: blockDamage ? 0.42f : 0.22f, yMax: blockDamage ? 0.5f : 0.3f);
                            //UI.CreateText(ref container, parent: "maxRaidPerPlayer", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Global.MaxRaidPerPlayer", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                            //UI.CreatePanel(ref container, parent: "maxRaidPerPlayer", name: "maxRaidPerPlayerInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                            //UI.CreateIntPanel(ref container, parent: "maxRaidPerPlayerInput", name: "int", current: _cfg.Global.MaxRaidPerPlayer, command: "craidcontroller.admin.modify int Global.MaxRaidPerPlayer");
                            //UI.CreatePanel(ref container, parent: "marge", name: "maxRaidPerTeam", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: blockDamage ? 0.32f : 0.12f, yMax: blockDamage ? 0.4f : 0.2f);
                            //UI.CreateText(ref container, parent: "maxRaidPerTeam", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Global.MaxRaidPerTeam", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                            //UI.CreatePanel(ref container, parent: "maxRaidPerTeam", name: "maxRaidPerTeamInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                            //UI.CreatePanel(ref container, parent: "maxRaidPerTeamInput", name: "maxRaidPerTeamInputMarge", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.44f, xMax: 0.56f, yMin: 0.15f, yMax: 0.85f);
                            //UI.CreateCheckbox(ref container, parent: "maxRaidPerTeamInputMarge", name: "checkbox", color: GetColor(COLOR_PRIMARY, 0.7f), command: $"craidcontroller.admin.modify bool Global.MaxRaidPerTeam {_cfg.Global.MaxRaidPerTeam}", isOn: _cfg.Global.MaxRaidPerTeam);
                            //******************************************************************************************************************************
                        }

                        break;

                    #endregion

                    #region BYPASS

                    case AdminPanelTab.Bypass:

                        TcBypass tcBypass = _cfg.Bypass.TcBypass;
                        string tcBypassString = string.Empty;

                        switch (tcBypass)
                        {
                            case TcBypass.Disabled:
                                tcBypassString = _instance.lang.GetMessage("AdminPanel.Setting.Bypass.TcBypass.Disabled", _instance, player.UserIDString);
                                break;
                            case TcBypass.AllPlayer:
                                tcBypassString = _instance.lang.GetMessage("AdminPanel.Setting.Bypass.TcBypass.AllPlayer", _instance, player.UserIDString);
                                break;
                            case TcBypass.LeaderOnly:
                                tcBypassString = _instance.lang.GetMessage("AdminPanel.Setting.Bypass.TcBypass.LeaderOnly", _instance, player.UserIDString);
                                break;
                        }

                        UI.CreatePanel(ref container, parent: parent, name: "bypass", color: GetColor(hex: "#000000", alpha: 0.4f), xMin: 0.05f, xMax: 0.685f);
                        UI.CreateSprite(ref container, parent: "bypass", name: "left", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.1f), xMax: 0.005f);
                        UI.CreateSprite(ref container, parent: "bypass", name: "right", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.1f), xMin: 0.995f);
                        UI.CreatePanel(ref container, parent: "bypass", name: "marge", xMin: 0.05f, xMax: 0.95f, yMin: 0.05f, yMax: 0.95f);
                        UI.CreatePanel(ref container, parent: "marge", name: "owner", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: 0.92f);
                        UI.CreateText(ref container, parent: "owner", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Bypass.Owner", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        UI.CreatePanel(ref container, parent: "owner", name: "ownerInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreatePanel(ref container, parent: "ownerInput", name: "ownerInputMarge", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.44f, xMax: 0.56f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreateCheckbox(ref container, parent: "ownerInputMarge", name: "checkbox", color: GetColor(COLOR_PRIMARY, 0.7f), command: $"craidcontroller.admin.modify bool Bypass.Owner {_cfg.Bypass.Owner}", isOn: _cfg.Bypass.Owner);
                        UI.CreatePanel(ref container, parent: "marge", name: "admin", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: 0.82f, yMax: 0.9f);
                        UI.CreateText(ref container, parent: "admin", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Bypass.Admin", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        UI.CreatePanel(ref container, parent: "admin", name: "adminInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreatePanel(ref container, parent: "adminInput", name: "adminInputMarge", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.44f, xMax: 0.56f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreateCheckbox(ref container, parent: "adminInputMarge", name: "checkbox", color: GetColor(COLOR_PRIMARY, 0.7f), command: $"craidcontroller.admin.modify bool Bypass.Admin {_cfg.Bypass.Admin}", isOn: _cfg.Bypass.Admin);
                        UI.CreatePanel(ref container, parent: "marge", name: "mate", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: 0.72f, yMax: 0.8f);
                        UI.CreateText(ref container, parent: "mate", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Bypass.Mate", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        UI.CreatePanel(ref container, parent: "mate", name: "mateInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreatePanel(ref container, parent: "mateInput", name: "mateInputMarge", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.44f, xMax: 0.56f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreateCheckbox(ref container, parent: "mateInputMarge", name: "checkbox", color: GetColor(COLOR_PRIMARY, 0.7f), command: $"craidcontroller.admin.modify bool Bypass.Mate {_cfg.Bypass.Mate}", isOn: _cfg.Bypass.Mate);
                        UI.CreatePanel(ref container, parent: "marge", name: "twig", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: 0.62f, yMax: 0.7f);
                        UI.CreateText(ref container, parent: "twig", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Bypass.Twig", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        UI.CreatePanel(ref container, parent: "twig", name: "twigInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreatePanel(ref container, parent: "twigInput", name: "twigInputMarge", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.44f, xMax: 0.56f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreateCheckbox(ref container, parent: "twigInputMarge", name: "checkbox", color: GetColor(COLOR_PRIMARY, 0.7f), command: $"craidcontroller.admin.modify bool Bypass.Twig {_cfg.Bypass.Twig}", isOn: _cfg.Bypass.Twig);
                        UI.CreatePanel(ref container, parent: "marge", name: "tcDecay", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: 0.52f, yMax: 0.6f);
                        UI.CreateText(ref container, parent: "tcDecay", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Bypass.TcDecay", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        UI.CreatePanel(ref container, parent: "tcDecay", name: "tcDecayInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreatePanel(ref container, parent: "tcDecayInput", name: "tcDecayInputMarge", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.44f, xMax: 0.56f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreateCheckbox(ref container, parent: "tcDecayInputMarge", name: "checkbox", color: GetColor(COLOR_PRIMARY, 0.7f), command: $"craidcontroller.admin.modify bool Bypass.TcDecay {_cfg.Bypass.TcDecay}", isOn: _cfg.Bypass.TcDecay);
                        UI.CreatePanel(ref container, parent: "marge", name: "noTc", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: 0.42f, yMax: 0.5f);
                        UI.CreateText(ref container, parent: "noTc", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Bypass.NoTc", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        UI.CreatePanel(ref container, parent: "noTc", name: "noTcInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreatePanel(ref container, parent: "noTcInput", name: "noTcInputMarge", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.44f, xMax: 0.56f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreateCheckbox(ref container, parent: "noTcInputMarge", name: "checkbox", color: GetColor(COLOR_PRIMARY, 0.7f), command: $"craidcontroller.admin.modify bool Bypass.NoTc {_cfg.Bypass.NoTc}", isOn: _cfg.Bypass.NoTc);
                        UI.CreatePanel(ref container, parent: "marge", name: "buildingPrivilege", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: 0.32f, yMax: 0.4f);
                        UI.CreateText(ref container, parent: "buildingPrivilege", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Bypass.BuildingPrivilege", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        UI.CreatePanel(ref container, parent: "buildingPrivilege", name: "buildingPrivilegeInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreatePanel(ref container, parent: "buildingPrivilegeInput", name: "buildingPrivilegeInputMarge", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.44f, xMax: 0.56f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreateCheckbox(ref container, parent: "buildingPrivilegeInputMarge", name: "checkbox", color: GetColor(COLOR_PRIMARY, 0.7f), command: $"craidcontroller.admin.modify bool Bypass.BuildingPrivilege {_cfg.Bypass.BuildingPrivilege}", isOn: _cfg.Bypass.BuildingPrivilege);
                        UI.CreatePanel(ref container, parent: "marge", name: "TCBypass", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: 0.22f, yMax: 0.3f);
                        UI.CreateText(ref container, parent: "TCBypass", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Bypass.TcBypass", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        UI.CreatePanel(ref container, parent: "TCBypass", name: "TCBypassInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreateSlider(ref container, parent: "TCBypassInput", name: "slider", current: tcBypassString, command: "craidcontroller.admin.slider tcBypass");
                        UI.CreatePanel(ref container, parent: "marge", name: "tcProtectItem", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), xMax: 0.49f, yMax: 0.2f);
                        UI.CreateText(ref container, parent: "tcProtectItem", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Bypass.TcProtectItem", _instance, player.UserIDString), size: 10, xMin: 0.025f, yMax: 0.95f, align: TextAnchor.UpperLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        UI.CreatePanel(ref container, parent: "tcProtectItem", name: "tcProtectItemPanel", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.02f, xMax: 0.975f, yMin: 0.05f, yMax: 0.7f);
                        DrawItem(ref container, parent: "tcProtectItemPanel", propriety: "Bypass.TcProtectItem", page: tcProtectItemPage, itemsPerPage: 4);
                        UI.CreatePanel(ref container, parent: "marge", name: "tcNoProtectItem", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), xMin: 0.51f, yMax: 0.2f);
                        UI.CreateText(ref container, parent: "tcNoProtectItem", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Bypass.TcNoProtectItem", _instance, player.UserIDString), size: 10, xMin: 0.025f, yMax: 0.95f, align: TextAnchor.UpperLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        UI.CreatePanel(ref container, parent: "tcNoProtectItem", name: "tcNoProtectItemPanel", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.02f, xMax: 0.975f, yMin: 0.05f, yMax: 0.7f);
                        DrawItem(ref container, parent: "tcNoProtectItemPanel", propriety: "Bypass.TcNoProtectItem", page: tcNoProtectItemPage, itemsPerPage: 4);

                        break;

                    #endregion

                    #region MESSAGE

                    case AdminPanelTab.Message:

                        UI.CreatePanel(ref container, parent: parent, name: "message", color: GetColor(hex: "#000000", alpha: 0.4f), xMin: 0.05f, xMax: 0.685f);
                        UI.CreateSprite(ref container, parent: "message", name: "left", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.1f), xMax: 0.005f);
                        UI.CreateSprite(ref container, parent: "message", name: "right", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.1f), xMin: 0.995f);
                        UI.CreatePanel(ref container, parent: "message", name: "marge", xMin: 0.05f, xMax: 0.95f, yMin: 0.05f, yMax: 0.95f);
                        UI.CreatePanel(ref container, parent: "marge", name: "messageType", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: 0.92f);
                        UI.CreateText(ref container, parent: "messageType", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Message.MessageType", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        UI.CreatePanel(ref container, parent: "messageType", name: "messageTypeInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreateSlider(ref container, parent: "messageTypeInput", name: "slider", current: _cfg.Message.MessageType, command: "craidcontroller.admin.slider list Message.MessageType _messageType");
                        bool messageTypeChat = _cfg.Message.MessageType == "Chat";

                        if (messageTypeChat)
                        {
                            UI.CreatePanel(ref container, parent: "marge", name: "syntaxPrefix", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.35f), xMin: 0.1f, yMin: 0.82f, yMax: 0.9f);
                            UI.CreateText(ref container, parent: "syntaxPrefix", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Message.Syntax.Prefix", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                            UI.CreatePanel(ref container, parent: "syntaxPrefix", name: "syntaxPrefixInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.61f, xMax: 0.97f, yMin: 0.15f, yMax: 0.85f);
                            UI.CreateInputField(ref container, parent: "syntaxPrefixInput", name: null, color: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: _cfg.Message.Syntax.Prefix, size: 10, command: "craidcontroller.admin.modify string Message.Syntax.Prefix ^[a-zA-Z0-9\\W]+$");

                            UI.CreatePanel(ref container, parent: "marge", name: "syntaxIcon", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.35f), xMin: 0.1f, yMin: 0.72f, yMax: 0.8f);
                            UI.CreateText(ref container, parent: "syntaxIcon", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Message.Syntax.Icon", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                            UI.CreatePanel(ref container, parent: "syntaxIcon", name: "syntaxIconInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.61f, xMax: 0.97f, yMin: 0.15f, yMax: 0.85f);
                            UI.CreateInputField(ref container, parent: "syntaxIconInput", name: null, color: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: _cfg.Message.Syntax.Icon, size: 10, command: "craidcontroller.admin.modify string Message.Syntax.Icon ^[0-9]{1,17}$", charLimit: 17);
                        }

                        UI.CreatePanel(ref container, parent: "marge", name: "effect", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: !messageTypeChat ? 0.82f : 0.62f, yMax: !messageTypeChat ? 0.9f : 0.7f);
                        UI.CreateText(ref container, parent: "effect", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Message.Effect", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        UI.CreatePanel(ref container, parent: "effect", name: "effectInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreatePanel(ref container, parent: "effectInput", name: "effectInputMarge", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.44f, xMax: 0.56f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreateCheckbox(ref container, parent: "effectInputMarge", name: "checkbox", color: GetColor(COLOR_PRIMARY, 0.7f), command: $"craidcontroller.admin.modify bool Message.Effect {_cfg.Message.Effect}", isOn: _cfg.Message.Effect);                        
                        UI.CreatePanel(ref container, parent: "marge", name: "minuteToStartMessage", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: !messageTypeChat ? 0.72f : 0.52f, yMax: !messageTypeChat ? 0.8f : 0.6f);
                        UI.CreateText(ref container, parent: "minuteToStartMessage", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Message.MinuteToStartMessage", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        UI.CreatePanel(ref container, parent: "minuteToStartMessage", name: "minuteToStartMessageInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreateInputField(ref container, parent: "minuteToStartMessageInput", name: null, color: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: _cfg.Message.MinuteToStartMessage.ToString(), size: 10, command: "craidcontroller.admin.modify string Message.MinuteToStartMessage ^[0-9]*$");
                        UI.CreatePanel(ref container, parent: "marge", name: "minuteToEndMessage", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: !messageTypeChat ? 0.62f : 0.42f, yMax: !messageTypeChat ? 0.7f : 0.5f);
                        UI.CreateText(ref container, parent: "minuteToEndMessage", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Message.MinuteToEndMessage", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        UI.CreatePanel(ref container, parent: "minuteToEndMessage", name: "minuteToEndMessageInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreateInputField(ref container, parent: "minuteToEndMessageInput", name: null, color: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: _cfg.Message.MinuteToEndMessage.ToString(), size: 10, command: "craidcontroller.admin.modify string Message.MinuteToEndMessage ^[0-9]*$");
                        UI.CreatePanel(ref container, parent: "marge", name: "allowedMessage", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: !messageTypeChat ? 0.52f : 0.32f, yMax: !messageTypeChat ? 0.6f : 0.4f);
                        UI.CreateText(ref container, parent: "allowedMessage", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Message.AllowedMessage", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        UI.CreatePanel(ref container, parent: "allowedMessage", name: "allowedMessageInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreatePanel(ref container, parent: "allowedMessageInput", name: "allowedMessageInputMarge", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.44f, xMax: 0.56f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreateCheckbox(ref container, parent: "allowedMessageInputMarge", name: "checkbox", color: GetColor(COLOR_PRIMARY, 0.7f), command: $"craidcontroller.admin.modify bool Message.AllowedMessage {_cfg.Message.AllowedMessage}", isOn: _cfg.Message.AllowedMessage);
                        UI.CreatePanel(ref container, parent: "marge", name: "finishedMessage", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: !messageTypeChat ? 0.42f : 0.22f, yMax: !messageTypeChat ? 0.5f : 0.3f);
                        UI.CreateText(ref container, parent: "finishedMessage", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Message.FinishedMessage", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        UI.CreatePanel(ref container, parent: "finishedMessage", name: "finishedMessageInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreatePanel(ref container, parent: "finishedMessageInput", name: "finishedMessageInputMarge", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.44f, xMax: 0.56f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreateCheckbox(ref container, parent: "finishedMessageInputMarge", name: "checkbox", color: GetColor(COLOR_PRIMARY, 0.7f), command: $"craidcontroller.admin.modify bool Message.FinishedMessage {_cfg.Message.FinishedMessage}", isOn: _cfg.Message.FinishedMessage);
                        UI.CreatePanel(ref container, parent: "marge", name: "failMessage", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: !messageTypeChat ? 0.32f : 0.12f, yMax: !messageTypeChat ? 0.4f : 0.2f);
                        UI.CreateText(ref container, parent: "failMessage", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Message.FailMessage", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        UI.CreatePanel(ref container, parent: "failMessage", name: "failMessageInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreatePanel(ref container, parent: "failMessageInput", name: "failMessageInputMarge", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.44f, xMax: 0.56f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreateCheckbox(ref container, parent: "failMessageInputMarge", name: "checkbox", color: GetColor(COLOR_PRIMARY, 0.7f), command: $"craidcontroller.admin.modify bool Message.FailMessage {_cfg.Message.FailMessage}", isOn: _cfg.Message.FailMessage);

                        break;

                    #endregion

                    #region UI

                    case AdminPanelTab.Ui:

                        ActiveUI activeUi = _cfg.Ui.ActiveUi;
                        string activeUiString = string.Empty;

                        switch (activeUi)
                        {
                            case ActiveUI.Disabled:
                                activeUiString = _instance.lang.GetMessage("AdminPanel.Setting.Ui.ActiveUi.Disabled", _instance, player.UserIDString);
                                break;
                            case ActiveUI.Activated:
                                activeUiString = _instance.lang.GetMessage("AdminPanel.Setting.Ui.ActiveUi.Activated", _instance, player.UserIDString);
                                break;
                            case ActiveUI.CustomStatusFramework:
                                activeUiString = _instance.lang.GetMessage("AdminPanel.Setting.Ui.ActiveUi.CustomStatusFramework", _instance, player.UserIDString);
                                break;
                        }

                        UI.CreatePanel(ref container, parent: parent, name: "ui", color: GetColor(hex: "#000000", alpha: 0.4f), xMin: 0.05f, xMax: 0.685f);
                        UI.CreateSprite(ref container, parent: "ui", name: "left", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.1f), xMax: 0.005f);
                        UI.CreateSprite(ref container, parent: "ui", name: "right", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.1f), xMin: 0.995f);
                        UI.CreatePanel(ref container, parent: "ui", name: "marge", xMin: 0.05f, xMax: 0.95f, yMin: 0.05f, yMax: 0.95f);
                        UI.CreatePanel(ref container, parent: "marge", name: "activeUi", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: 0.92f);
                        UI.CreateText(ref container, parent: "activeUi", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Ui.ActiveUi", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        UI.CreatePanel(ref container, parent: "activeUi", name: "activeUiInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                        UI.CreateSlider(ref container, parent: "activeUiInput", name: "slider", current: activeUiString, command: "craidcontroller.admin.slider activeUi");


                        if (_cfg.Ui.ActiveUi == ActiveUI.Activated)
                        {
                            UI.CreatePanel(ref container, parent: "marge", name: "defaultUi", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.35f), xMin: 0.1f, yMin: 0.82f, yMax: 0.9f);
                            UI.CreateText(ref container, parent: "defaultUi", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Ui.DefaultUi", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                            UI.CreateProtectedButton(ref container, parent: "defaultUi", name: "btn", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.7f), textColor: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: _instance.lang.GetMessage("Btn.Select", _instance, player.UserIDString), size: 10, xMin: 0.65f, xMax: 0.81f, yMin: 0.15f, yMax: 0.85f, command: "craidcontroller.admin.ui show");
                            UI.CreatePanel(ref container, parent: "defaultUi", name: "defaultUiInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.82f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                            UI.CreateText(ref container, parent: "defaultUiInput", color: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: $"UI n°{_cfg.Ui.DefaultUi}", size: 10, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                            UI.CreatePanel(ref container, parent: "marge", name: "side", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.35f), xMin: 0.1f, yMin: 0.72f, yMax: 0.8f);
                            UI.CreateText(ref container, parent: "side", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Ui.Side", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                            UI.CreateProtectedButton(ref container, parent: "side", name: "btnLeft", color: _cfg.Ui.Side == 0 ? GetColor(hex: COLOR_PRIMARY, alpha: 0.7f) : GetColor(hex: "#000000", alpha: 0.5f), textColor: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: _instance.lang.GetMessage("AdminPanel.Setting.Ui.Side.0", _instance, player.UserIDString), size: 10, xMin: 0.65f, xMax: 0.81f, yMin: 0.15f, yMax: 0.85f, command: "craidcontroller.admin.ui.side 0");
                            UI.CreateProtectedButton(ref container, parent: "side", name: "btnRight", color: _cfg.Ui.Side == 1 ? GetColor(hex: COLOR_PRIMARY, alpha: 0.7f) : GetColor(hex: "#000000", alpha: 0.5f), textColor: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: _instance.lang.GetMessage("AdminPanel.Setting.Ui.Side.1", _instance, player.UserIDString), size: 10, xMin: 0.82f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f, command: "craidcontroller.admin.ui.side 1");
                            UI.CreatePanel(ref container, parent: "marge", name: "yMax", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.35f), xMin: 0.1f, yMin: 0.62f, yMax: 0.7f);
                            UI.CreateText(ref container, parent: "yMax", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Ui.Ymax", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                            UI.CreatePanel(ref container, parent: "yMax", name: "yMaxInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                            UI.CreateFloatPanel(ref container, parent: "yMaxInput", name: "float", current: _cfg.Ui.Ymax, command: "craidcontroller.admin.modify float Ui.Ymax", min: 0f, max: 1f, increment:0.01f);
                            UI.CreatePanel(ref container, parent: "marge", name: "backgound", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.35f), xMin: 0.1f, yMin: 0.52f, yMax: 0.6f);
                            UI.CreateText(ref container, parent: "backgound", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Ui.Background", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                            UI.CreatePanel(ref container, parent: "backgound", name: "backgoundInputColor", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.81f, yMin: 0.15f, yMax: 0.85f);
                            UI.CreateInputField(ref container, parent: "backgoundInputColor", name: "color", color: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: _cfg.Ui.BackgroundColor, size: 10, command: "craidcontroller.admin.modify string Ui.BackgroundColor ^#[a-fA-F0-9]{6}$");
                            UI.CreatePanel(ref container, parent: "backgound", name: "backgoundInputOpacity", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.82f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                            UI.CreateFloatPanel(ref container, parent: "backgoundInputOpacity", name: "float", current: _cfg.Ui.BackgroundOpacity, command: "craidcontroller.admin.modify float Ui.BackgroundOpacity", min: 0f, max: 1f, increment: 0.05f);
                            UI.CreatePanel(ref container, parent: "marge", name: "hideNotTime", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.35f), xMin: 0.1f, yMin: 0.42f, yMax: 0.5f);
                            UI.CreateText(ref container, parent: "hideNotTime", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Ui.HideNotTime", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                            UI.CreatePanel(ref container, parent: "hideNotTime", name: "hideNotTimeInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                            UI.CreatePanel(ref container, parent: "hideNotTimeInput", name: "hideNotTimeInputMarge", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.44f, xMax: 0.56f, yMin: 0.15f, yMax: 0.85f);
                            UI.CreateCheckbox(ref container, parent: "hideNotTimeInputMarge", name: "checkbox", color: GetColor(COLOR_PRIMARY, 0.7f), command: $"craidcontroller.admin.modify bool Ui.HideNotTime {_cfg.Ui.HideNotTime}", isOn: _cfg.Ui.HideNotTime);
                            UI.CreatePanel(ref container, parent: "marge", name: "hideIsTime", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.35f), xMin: 0.1f, yMin: 0.32f, yMax: 0.4f);
                            UI.CreateText(ref container, parent: "hideIsTime", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Ui.HideIsTime", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                            UI.CreatePanel(ref container, parent: "hideIsTime", name: "hideIsTimeInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                            UI.CreatePanel(ref container, parent: "hideIsTimeInput", name: "hideIsTimeInputMarge", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.44f, xMax: 0.56f, yMin: 0.15f, yMax: 0.85f);
                            UI.CreateCheckbox(ref container, parent: "hideIsTimeInputMarge", name: "checkbox", color: GetColor(COLOR_PRIMARY, 0.7f), command: $"craidcontroller.admin.modify bool Ui.HideIsTime {_cfg.Ui.HideIsTime}", isOn: _cfg.Ui.HideIsTime);
                            UI.CreatePanel(ref container, parent: "marge", name: "allowPlayerHide", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.35f), xMin: 0.1f, yMin: 0.22f, yMax: 0.3f);
                            UI.CreateText(ref container, parent: "allowPlayerHide", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Ui.AllowPlayerHide", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                            UI.CreatePanel(ref container, parent: "allowPlayerHide", name: "allowPlayerHideInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                            UI.CreatePanel(ref container, parent: "allowPlayerHideInput", name: "allowPlayerHideInputMarge", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.44f, xMax: 0.56f, yMin: 0.15f, yMax: 0.85f);
                            UI.CreateCheckbox(ref container, parent: "allowPlayerHideInputMarge", name: "checkbox", color: GetColor(COLOR_PRIMARY, 0.7f), command: $"craidcontroller.admin.modify bool Ui.AllowPlayerHide {_cfg.Ui.AllowPlayerHide}", isOn: _cfg.Ui.AllowPlayerHide);
                            UI.CreatePanel(ref container, parent: "marge", name: "allowPlayerChange", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.35f), xMin: 0.1f, yMin: 0.12f, yMax: 0.2f);
                            UI.CreateText(ref container, parent: "allowPlayerChange", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Ui.AllowPlayerChange", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                            UI.CreatePanel(ref container, parent: "allowPlayerChange", name: "allowPlayerChangeInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                            UI.CreatePanel(ref container, parent: "allowPlayerChangeInput", name: "allowPlayerChangeInputMarge", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.44f, xMax: 0.56f, yMin: 0.15f, yMax: 0.85f);
                            UI.CreateCheckbox(ref container, parent: "allowPlayerChangeInputMarge", name: "checkbox", color: GetColor(COLOR_PRIMARY, 0.7f), command: $"craidcontroller.admin.modify bool Ui.AllowPlayerChange {_cfg.Ui.AllowPlayerChange}", isOn: _cfg.Ui.AllowPlayerChange);
                            UI.CreatePanel(ref container, parent: "marge", name: "rate", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.35f), xMin: 0.1f, yMin: 0.02f, yMax: 0.1f);
                            UI.CreateText(ref container, parent: "rate", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Ui.Rate", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                            UI.CreatePanel(ref container, parent: "rate", name: "rateInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.65f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                            UI.CreateFloatPanel(ref container, parent: "rateInput", name: "float", current: _cfg.Ui.Rate, command: "craidcontroller.admin.modify float Ui.Rate");
                        }
                        else if (_cfg.Ui.ActiveUi == ActiveUI.CustomStatusFramework)
                        {
                            if (_instance.CustomStatusFramework == null)
                            {
                                UI.CreatePanel(ref container, parent: "marge", name: "customStatusFrameworkNotFound", color: GetColor(hex: COLOR_ERROR, alpha: 0.35f), xMin: 0.1f, yMin: 0.82f, yMax: 0.9f);
                                UI.CreateText(ref container, parent: "customStatusFrameworkNotFound", color: GetColor(hex: COLOR_ERROR), text: Lang("AdminPanel.Setting.Ui.ActiveUi.CustomStatusFramework.NotFound", player.UserIDString), size: 10, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                            }
                            else
                            {
                                UI.CreatePanel(ref container, parent: "marge", name: "customStatusFrameworkFound", color: GetColor(hex: COLOR_SUCCESS, alpha: 0.35f), xMin: 0.1f, yMin: 0.82f, yMax: 0.9f);
                                UI.CreateText(ref container, parent: "customStatusFrameworkFound", color: GetColor(hex: COLOR_SUCCESS), text: Lang("AdminPanel.Setting.Ui.ActiveUi.CustomStatusFramework.Found", player.UserIDString), size: 10, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                            }
                        }
                        else if (_cfg.Ui.ActiveUi == ActiveUI.Disabled)
                        {
                            UI.CreatePanel(ref container, parent: "marge", name: "disabled", color: GetColor(hex: COLOR_ERROR, alpha: 0.35f), xMin: 0.1f, yMin: 0.82f, yMax: 0.9f);
                            UI.CreateText(ref container, parent: "disabled", color: GetColor(hex: COLOR_ERROR), text: Lang("AdminPanel.Setting.Ui.ActiveUi.Disabled.Text", player.UserIDString), size: 10, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        }

                        break;

                    #endregion

                    #region SCHEDULE

                    case AdminPanelTab.Schedule:

                        float yMinDay = 0.92f;
                        float yMaxDay = 1f;
                        UI.CreatePanel(ref container, parent: parent, name: "schedule", color: GetColor(hex: "#000000", alpha: 0.4f), xMin: 0.05f, xMax: 0.685f);
                        UI.CreateSprite(ref container, parent: "schedule", name: "left", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.1f), xMax: 0.005f);
                        UI.CreateSprite(ref container, parent: "schedule", name: "right", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.1f), xMin: 0.995f);
                        UI.CreatePanel(ref container, parent: "schedule", name: "marge", xMin: 0.05f, xMax: 0.95f, yMin: 0.05f, yMax: 0.95f);
                        UI.CreatePanel(ref container, parent: "marge", name: "leftSchedule", xMax: 0.49f);

                        for (int i = 0; i < 7; i++)
                        {
                            DayOfWeek dayOfWeek = (DayOfWeek)(((int)DayOfWeek.Monday + i) % 7);
                            ScheduleSetting schedule = _mainController.currentSchedule.schedule[dayOfWeek];
                            ScheduleStatus status = schedule.Status;
                            List<TimeRange> timeRange = schedule.TimeRange;
                            string statusString = string.Empty;

                            switch (status)
                            {
                                case ScheduleStatus.AllowDuringSchedule:
                                    statusString = _instance.lang.GetMessage("AdminPanel.Schedule.Status.AllowDuringSchedule", _instance, player.UserIDString);
                                    break;
                                case ScheduleStatus.NotAllowedAllDay:
                                    statusString = _instance.lang.GetMessage("AdminPanel.Schedule.Status.NotAllowedAllDay", _instance, player.UserIDString);
                                    break;
                                case ScheduleStatus.AllowedAllDay:
                                    statusString = _instance.lang.GetMessage("AdminPanel.Schedule.Status.AllowedAllDay", _instance, player.UserIDString);
                                    break;
                            }

                            UI.CreatePanel(ref container, parent: "leftSchedule", name: dayOfWeek.ToString(), color: status == ScheduleStatus.AllowDuringSchedule && timeRange.Count == 0 ? GetColor(hex: COLOR_ERROR, alpha: 0.35f) : scheduleActiveDay == dayOfWeek.ToString() ? GetColor(hex: COLOR_PRIMARY, alpha: 0.35f) : GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: yMinDay, yMax: yMaxDay);
                            UI.CreateText(ref container, parent: dayOfWeek.ToString(), color: GetColor(hex: "#FFFFFF"), text: ToDateLangFormat(dayOfWeek, _playerSetting.data[player.userID].Language), size: 10, xMin: 0.05f, xMax: 0.25f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                            UI.CreatePanel(ref container, parent: dayOfWeek.ToString(), name: dayOfWeek.ToString() + "Input", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.25f, xMax: 0.875f, yMin: 0.15f, yMax: 0.85f);
                            UI.CreateSlider(ref container, parent: dayOfWeek.ToString() + "Input", name: "slider", current: statusString, command: $"craidcontroller.admin.slider schedule {dayOfWeek}");
                            UI.CreatePanel(ref container, parent: dayOfWeek.ToString(), name: "btn", color: GetColor(hex: "#000000", alpha: 0.6f), xMin: 0.9f, xMax: 0.97f, yMin: 0.25f, yMax: 0.75f);
                            UI.CreateImage(ref container, parent: "btn", image: "assets/icons/gear.png", color: GetColor(hex: COLOR_PRIMARY, alpha: 1f), outlineColor: GetColor(hex: "#000000", alpha: 0.8f), xMin: 0.2f, xMax: 0.8f, yMin: 0.2f, yMax: 0.8f, DistanceX: 0.5f, DistanceY: 0.5f);
                            UI.CreateProtectedButton(ref container, parent: "btn", command: $"craidcontroller.admin.schedule view {dayOfWeek}");
                            yMinDay -= 0.1f;
                            yMaxDay -= 0.1f;
                        }

                        if (_mainController.AllowDuringScheduleNoData() != null)
                        {
                            UI.CreatePanel(ref container, parent: "leftSchedule", name: "errorAllowDuringScheduleNoData", color: GetColor(hex: COLOR_ERROR, alpha: 0.35f), yMax: 0.08f);
                            UI.CreateImage(ref container, parent: "errorAllowDuringScheduleNoData", image: "assets/icons/stopwatch.png", color: GetColor(hex: COLOR_ERROR), outlineColor: GetColor(hex: "#000000", alpha: 0.8f), xMin: 0.01f, xMax: 0.1f, yMin: 0.2f, yMax: 0.8f, DistanceX: 0.5f, DistanceY: 0.5f);
                            UI.CreateText(ref container, parent: "errorAllowDuringScheduleNoData", color: GetColor(hex: COLOR_ERROR), text: Lang("AdminPanel.Error.AllowDuringScheduleNoData", player.UserIDString, new object[] { string.Join(", ", _mainController.AllowDuringScheduleNoData(player)) }), size: 10, xMin: 0.12f, xMax: 0.95f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                        }

                        if (scheduleActiveDay != string.Empty)
                        {
                            float xMinSchedule = 0.82f;
                            float xMaxSchedule = 0.9f;
                            DayOfWeek current = (DayOfWeek)Enum.Parse(typeof(DayOfWeek), scheduleActiveDay);
                            UI.CreatePanel(ref container, parent: "marge", name: "rightSchedule", color: GetColor(hex: "#000000", alpha: 0.7f), xMin: 0.51f);
                            UI.CreateText(ref container, parent: "rightSchedule", color: GetColor(hex: COLOR_PRIMARY), text: ToDateLangFormat(current, _playerSetting.data[player.userID].Language).ToUpper(), size: 15, yMin: 0.92f, font: "robotocondensed-bold.ttf", outlineColor: GetColor(hex: "#000000", alpha: 0.1f));
                            UI.CreateText(ref container, parent: "rightSchedule", name: "hide", color: GetColor(hex: COLOR_PRIMARY), text: "<", size: 15, xMin: 0.05f, xMax: 0.1f, yMin: 0.92f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.1f));
                            UI.CreateProtectedButton(ref container, parent: "hide", command: $"craidcontroller.admin.schedule hide {scheduleActiveDay}");

                            if (CheckCanAddTimeRange(current))
                            {
                                UI.CreateImage(ref container, parent: "rightSchedule", image: "assets/icons/authorize.png", color: GetColor(hex: COLOR_SUCCESS, alpha: 0.7f), outlineColor: GetColor(hex: "#000000", alpha: 0.8f), xMin: 0.91f, xMax: 0.98f, yMin: 0.95f, yMax: 0.99f, DistanceX: 0.1f, DistanceY: 0.1f);
                                UI.CreateProtectedButton(ref container, parent: "rightSchedule", xMin: 0.91f, xMax: 0.98f, yMin: 0.95f, yMax: 0.99f, command: $"craidcontroller.admin.schedule add {scheduleActiveDay}");
                            }

                            if (_mainController.currentSchedule.schedule.ContainsKey(current))
                            {
                                int indexTimeRanges = 0;

                                foreach (var range in _mainController.currentSchedule.schedule[current].TimeRange)
                                {
                                    UI.CreatePanel(ref container, parent: "rightSchedule", name: scheduleActiveDay, color: GetColor(hex: "#FFFFFF", alpha: 0.1f), xMin: 0.05f, xMax: 0.95f, yMin: xMinSchedule, yMax: xMaxSchedule);
                                    UI.CreateImage(ref container, parent: scheduleActiveDay, name: "imgStart", image: "assets/icons/stopwatch.png", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), outlineColor: GetColor(hex: "#000000", alpha: 0.8f), xMin: 0.025f, xMax: 0.075f, yMin: 0.35f, yMax: 0.65f, DistanceX: 0.1f, DistanceY: 0.1f);
                                    UI.CreateText(ref container, parent: scheduleActiveDay, color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Schedule.TimeRange.Start", _instance, player.UserIDString), size: 10, xMin: 0.1f, xMax: 0.2f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                                    UI.CreatePanel(ref container, parent: scheduleActiveDay, name: $"{scheduleActiveDay}InputStart", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.225f, xMax: 0.375f, yMin: 0.15f, yMax: 0.85f);
                                    UI.CreateText(ref container, parent: $"{scheduleActiveDay}InputStart", color: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: $"{ToTimeString(range.StartHour)}h{ToTimeString(range.StartMinute)}", size: 10, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                                    UI.CreateImage(ref container, parent: scheduleActiveDay, name: "imgEnd", image: "assets/icons/sleeping.png", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), outlineColor: GetColor(hex: "#000000", alpha: 0.8f), xMin: 0.425f, xMax: 0.475f, yMin: 0.35f, yMax: 0.65f, DistanceX: 0.1f, DistanceY: 0.1f);
                                    UI.CreateText(ref container, parent: scheduleActiveDay, color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Schedule.TimeRange.End", _instance, player.UserIDString), size: 10, xMin: 0.5f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                                    UI.CreatePanel(ref container, parent: scheduleActiveDay, name: $"{scheduleActiveDay}InputEnd", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.625f, xMax: 0.775f, yMin: 0.15f, yMax: 0.85f);
                                    UI.CreateText(ref container, parent: $"{scheduleActiveDay}InputEnd", color: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: GetEndTimeRange(ToTimeString(range.EndHour), ToTimeString(range.EndMinute)), size: 10, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                                    //******************************************************************** TODO ****************************************************
                                    // UI.CreatePanel(ref container, parent: scheduleActiveDay, name: "btnEdit", color: GetColor(hex: "#000000", alpha: 0.6f), xMin: 0.8f, xMax: 0.87f, yMin: 0.25f, yMax: 0.75f);
                                    // UI.CreateImage(ref container, parent: "btnEdit", image: "assets/icons/examine.png", color: GetColor(hex: COLOR_PRIMARY, alpha: 1f), outlineColor: GetColor(hex: "#000000", alpha: 0.8f), xMin: 0.2f, xMax: 0.8f, yMin: 0.2f, yMax: 0.8f, DistanceX: 0.5f, DistanceY: 0.5f);
                                    // UI.CreateProtectedButton(ref container, parent: "btnEdit", command: $"craidcontroller.admin.schedule edit {scheduleActiveDay} {indexTimeRanges}");
                                    //******************************************************************************************************************************
                                    UI.CreatePanel(ref container, parent: scheduleActiveDay, name: "btnDelete", color: GetColor(hex: "#000000", alpha: 0.6f), xMin: 0.9f, xMax: 0.97f, yMin: 0.25f, yMax: 0.75f);
                                    UI.CreateImage(ref container, parent: "btnDelete", image: "assets/icons/demolish_immediate.png", color: GetColor(hex: COLOR_ERROR, alpha: 1f), outlineColor: GetColor(hex: "#000000", alpha: 0.8f), xMin: 0.2f, xMax: 0.8f, yMin: 0.2f, yMax: 0.8f, DistanceX: 0.5f, DistanceY: 0.5f);
                                    UI.CreateProtectedButton(ref container, parent: "btnDelete", command: $"craidcontroller.admin.schedule delete {scheduleActiveDay} {indexTimeRanges}");
                                    xMinSchedule -= 0.1f;
                                    xMaxSchedule -= 0.1f;
                                    indexTimeRanges++;
                                }
                            }
                        }

                        break;

                        #endregion
                }
            }

            public void DrawTimeRangeShow()
            {
                DayOfWeek current = (DayOfWeek)Enum.Parse(typeof(DayOfWeek), scheduleActiveDay);
                var lastTimeRange = GetLastTimeRange(current);
                var container = UI.CreateContainer(name: UI_ADMIN_PANEL + "timeRange", color: GetColor(hex: "#000000", alpha: 0.75f), parent: "Overlay", blur: true);
                var currentEndHour = 0;
                int currentEndMinute = 0;
                UI.CreatePanel(ref container, parent: UI_ADMIN_PANEL + "timeRange", name: "timeRangeShow", xMin: 0.4f, xMax: 0.6f, yMin: 0.2f, yMax: 0.8f);
                UI.CreateSprite(ref container, parent: "timeRangeShow", name: "left", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), xMax: 0.005f);
                UI.CreateSprite(ref container, parent: "timeRangeShow", name: "right", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), xMin: 0.995f);
                UI.CreatePanel(ref container, parent: "timeRangeShow", name: "main", color: GetColor(hex: "#000000", alpha: 0.7f), xMin: 0.005f, xMax: 0.995f);
                UI.CreatePanel(ref container, parent: "main", name: "title", xMin: 0.05f, xMax: 0.95f, yMin: 0.9f);
                UI.CreateText(ref container, parent: "title", color: GetColor(hex: COLOR_PRIMARY), text: ToDateLangFormat(current, _playerSetting.data[player.userID].Language).ToUpper(), size: 15, font: "robotocondensed-bold.ttf", outlineColor: GetColor(hex: "#000000", alpha: 0.1f));
                UI.CreatePanel(ref container, parent: "main", name: "marge", xMin: 0.05f, xMax: 0.95f, yMin: 0.1f, yMax: 0.90f);
                UI.CreatePanel(ref container, parent: "marge", name: "start", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: 0.9f);
                UI.CreateImage(ref container, parent: "start", name: "imgStart", image: "assets/icons/stopwatch.png", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), outlineColor: GetColor(hex: "#000000", alpha: 0.8f), xMin: 0.025f, xMax: 0.1f, yMin: 0.25f, yMax: 0.75f, DistanceX: 0.1f, DistanceY: 0.1f);
                UI.CreateText(ref container, parent: "start", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Schedule.TimeRange.OnEdit.Start", _instance, player.UserIDString), size: 10, xMin: 0.125f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                var currentStartHour = timeRangeCurrent.StartHour == -1 ? ((lastTimeRange?.EndHour) ?? 0) : timeRangeCurrent.StartHour;
                var minStartHour = lastTimeRange?.EndHour ?? 0;
                UI.CreatePanel(ref container, parent: "marge", name: "startHour", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.35f), xMin: 0.1f, yMin: 0.78f, yMax: 0.88f);
                UI.CreateText(ref container, parent: "startHour", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Schedule.TimeRange.OnEdit.Hour", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.5f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                UI.CreatePanel(ref container, parent: "startHour", name: "startHourInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.55f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                UI.CreateIntPanel(ref container, parent: "startHourInput", name: "int", current: currentStartHour, command: $"craidcontroller.admin.schedule inAdd {scheduleActiveDay} 1", min: minStartHour, max: 23, timeInt: true);
                int currentStartMinute = (timeRangeCurrent.StartMinute == -1 && (timeRangeCurrent.StartHour == -1 || (lastTimeRange?.EndHour ?? 0) == timeRangeCurrent.StartHour)) ? (lastTimeRange == null ? 0 : lastTimeRange.EndMinute + 1) : (timeRangeCurrent.StartMinute == -1 ? 0 : timeRangeCurrent.StartMinute);
                var minStartMinute = timeRangeCurrent.StartHour == -1 ? lastTimeRange == null ? 0 : lastTimeRange.EndMinute + 1 : ((lastTimeRange?.EndHour ?? 0) == timeRangeCurrent.StartHour ? lastTimeRange == null ? 0 : lastTimeRange.EndMinute + 1 : 0);
                UI.CreatePanel(ref container, parent: "marge", name: "startMinute", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.35f), xMin: 0.1f, yMin: 0.66f, yMax: 0.76f);
                UI.CreateText(ref container, parent: "startMinute", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Schedule.TimeRange.OnEdit.Minute", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.5f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                UI.CreatePanel(ref container, parent: "startMinute", name: "startMinuteInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.55f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                UI.CreateIntPanel(ref container, parent: "startMinuteInput", name: "int", current: currentStartMinute, command: $"craidcontroller.admin.schedule inAdd {scheduleActiveDay} 2", min: minStartMinute, max: 59, timeInt: true);
                UI.CreatePanel(ref container, parent: "marge", name: "end", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: 0.54f, yMax: 0.64f);
                UI.CreateImage(ref container, parent: "end", name: "imgEnd", image: "assets/icons/sleeping.png", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), outlineColor: GetColor(hex: "#000000", alpha: 0.8f), xMin: 0.025f, xMax: 0.1f, yMin: 0.25f, yMax: 0.75f, DistanceX: 0.1f, DistanceY: 0.1f);
                UI.CreateText(ref container, parent: "end", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Schedule.TimeRange.OnEdit.End", _instance, player.UserIDString), size: 10, xMin: 0.125f, xMax: 0.6f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                UI.CreatePanel(ref container, parent: "marge", name: "endOfDay", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.35f), xMin: 0.1f, yMin: 0.42f, yMax: 0.52f);
                UI.CreateText(ref container, parent: "endOfDay", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Schedule.TimeRange.OnEdit.EndOfDay", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.75f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                UI.CreatePanel(ref container, parent: "endOfDay", name: "endOfDayInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.8f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                UI.CreatePanel(ref container, parent: "endOfDayInput", name: "endOfDayInputMarge", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.14f, xMax: 0.86f, yMin: 0.15f, yMax: 0.85f);
                UI.CreateCheckbox(ref container, parent: "endOfDayInputMarge", name: "checkbox", color: GetColor(COLOR_PRIMARY, 0.7f), command: $"craidcontroller.admin.schedule inAdd {scheduleActiveDay} {timeRangeCurrentEndOfDay}", isOn: timeRangeCurrentEndOfDay);

                if (!timeRangeCurrentEndOfDay)
                {
                    currentEndHour = timeRangeCurrent.EndHour == -1 ? (timeRangeCurrent.StartHour == -1 ? (lastTimeRange?.EndHour ?? 0) : timeRangeCurrent.StartHour) : timeRangeCurrent.EndHour;
                    var minEndHour = timeRangeCurrent.StartHour == -1 ? (lastTimeRange?.EndHour ?? 0) : timeRangeCurrent.StartHour;
                    UI.CreatePanel(ref container, parent: "marge", name: "endHour", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.35f), xMin: 0.1f, yMin: 0.3f, yMax: 0.4f);
                    UI.CreateText(ref container, parent: "endHour", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Schedule.TimeRange.OnEdit.Hour", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.5f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                    UI.CreatePanel(ref container, parent: "endHour", name: "endHourInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.55f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                    UI.CreateIntPanel(ref container, parent: "endHourInput", name: "int", current: currentEndHour, command: $"craidcontroller.admin.schedule inAdd {scheduleActiveDay} 3", min: minEndHour, max: 23, timeInt: true);
                    currentEndMinute = timeRangeCurrent.EndMinute != -1 ? timeRangeCurrent.EndMinute : (timeRangeCurrent.EndHour != -1 ? 0 : (timeRangeCurrent.StartHour == -1 && timeRangeCurrent.StartMinute == -1 ? (lastTimeRange?.EndMinute ?? 0) + 2 : (timeRangeCurrent.StartHour == -1 && timeRangeCurrent.StartMinute != -1 ? timeRangeCurrent.StartMinute + 1 : (timeRangeCurrent.StartMinute == -1 ? ((lastTimeRange?.EndHour ?? 0) == timeRangeCurrent.StartHour ? (lastTimeRange?.EndMinute ?? 0) + 2 : 1) : timeRangeCurrent.StartMinute + 1))));
                    int minEndMinute = timeRangeCurrent.EndHour != -1 ? 0 : (timeRangeCurrent.StartHour == -1 && timeRangeCurrent.StartMinute == -1 ? (lastTimeRange?.EndMinute ?? 0) + 2 : (timeRangeCurrent.StartHour == -1 && timeRangeCurrent.StartMinute != -1 ? timeRangeCurrent.StartMinute + 1 : (timeRangeCurrent.StartMinute == -1 ? ((lastTimeRange?.EndHour ?? 0) == timeRangeCurrent.StartHour ? (lastTimeRange?.EndMinute ?? 0) + 2 : 1) : timeRangeCurrent.StartMinute + 1)));
                    UI.CreatePanel(ref container, parent: "marge", name: "endMinute", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.35f), xMin: 0.1f, yMin: 0.18f, yMax: 0.28f);
                    UI.CreateText(ref container, parent: "endMinute", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Schedule.TimeRange.OnEdit.Minute", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.5f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                    UI.CreatePanel(ref container, parent: "endMinute", name: "endMinuteInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.55f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                    UI.CreateIntPanel(ref container, parent: "endMinuteInput", name: "int", current: currentEndMinute, command: $"craidcontroller.admin.schedule inAdd {scheduleActiveDay} 4", min: minEndMinute, max: 59, timeInt: true);
                }

                UI.CreatePanel(ref container, parent: "marge", name: "submit", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.7f), xMin: 0.3f, xMax: 0.7f, yMax: 0.1f);
                UI.CreateProtectedButton(ref container, parent: "submit", textColor: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: _instance.lang.GetMessage("Btn.Submit", _instance, player.UserIDString), size: 11, command: $"craidcontroller.admin.schedule addComplete {scheduleActiveDay} {currentStartHour} {currentStartMinute} {(timeRangeCurrentEndOfDay ? 23 : currentEndHour)} {(timeRangeCurrentEndOfDay ? 59 : currentEndMinute)}");
                UI.CreatePanel(ref container, parent: "main", name: "close", yMax: 0.075f);
                UI.CreateProtectedButton(ref container, parent: "close", textColor: GetColor(COLOR_ERROR), text: _instance.lang.GetMessage("Btn.Close", _instance, player.UserIDString), size: 11, command: "craidcontroller.admin.close popup clear");
                CuiHelper.AddUi(player, container);
            }

            public void DrawFeedShow()
            {
                Feed.News news = _feed.data[feedNews];
                var container = UI.CreateContainer(name: UI_ADMIN_PANEL + "feed", color: GetColor(hex: "#000000", alpha: 0.75f), parent: "Overlay", blur: true);
                UI.CreatePanel(ref container, parent: UI_ADMIN_PANEL + "feed", name: "feedShow", xMin: 0.3f, xMax: 0.7f, yMin: 0.2f, yMax: 0.7f);
                UI.CreateSprite(ref container, parent: "feedShow", name: "left", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), xMax: 0.005f);
                UI.CreateSprite(ref container, parent: "feedShow", name: "right", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), xMin: 0.995f);
                UI.CreatePanel(ref container, parent: "feedShow", name: "main", color: GetColor(hex: "#000000", alpha: 0.7f), xMin: 0.005f, xMax: 0.995f);
                UI.CreatePanel(ref container, parent: "main", name: "title", xMin: 0.05f, xMax: 0.95f, yMin: 0.85f, yMax: 0.95f);
                UI.CreateText(ref container, parent: "title", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.3f), text: $"» {news.Title.ToUpper()}", size: 20, align: TextAnchor.UpperLeft, font: "robotocondensed-bold.ttf", outlineColor: GetColor(hex: "#000000", alpha: 0.1f));
                string date = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds((news.Date - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds).ToString("yyyy-MM-dd\\THH:mm:ss");
                UI.CreatePanel(ref container, parent: "title", name: "plugin", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), xMin: 1f - (news.Plugin.Length * 0.01f), yMin: 0.5f, yMax: 0.85f);
                UI.CreateText(ref container, parent: "plugin", name: "text", color: GetColor(hex: "#9da6ab"), text: news.Plugin, size: 8, font: "permanentmarker.ttf", outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                UI.CreateText(ref container, parent: "title", name: "date", color: GetColor(hex: "#9da6ab"), text: ToDateLangFormat(date, _playerSetting.data[player.userID].Language), size: 8, xMin: 0.05f, yMin: 0.15f, align: TextAnchor.LowerLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                UI.CreateText(ref container, parent: "main", name: "description", color: GetColor(hex: "#FFFFFF"), text: news.Description, size: 10, xMin: 0.05f, xMax: 0.95f, yMin: 0.15f, yMax: 0.8f, align: TextAnchor.UpperLeft);
                UI.CreatePanel(ref container, parent: "main", name: "close", yMax: 0.075f);
                UI.CreateProtectedButton(ref container, parent: "close", textColor: GetColor(COLOR_PRIMARY), text: _instance.lang.GetMessage("Btn.Close", _instance, player.UserIDString), size: 14, xMin: 0.425f, xMax: 0.575f, command: "craidcontroller.admin.close popup", align: TextAnchor.MiddleCenter);
                CuiHelper.AddUi(player, container);
            }

            public void DrawUiList()
            {
                var container = UI.CreateContainer(name: UI_ADMIN_PANEL + "uiList", color: GetColor(hex: "#000000", alpha: 0.75f), parent: "Overlay", blur: true);
                UI.CreatePanel(ref container, parent: UI_ADMIN_PANEL + "uiList", name: "uiList", xMin: 0.4f, xMax: 0.6f, yMin: 0.2f, yMax: 0.8f);
                UI.CreateSprite(ref container, parent: "uiList", name: "left", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), xMax: 0.005f);
                UI.CreateSprite(ref container, parent: "uiList", name: "right", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), xMin: 0.995f);
                UI.CreatePanel(ref container, parent: "uiList", name: "main", color: GetColor(hex: "#000000", alpha: 0.7f), xMin: 0.005f, xMax: 0.995f);
                UI.CreatePanel(ref container, parent: "main", name: "title", xMin: 0.05f, xMax: 0.95f, yMin: 0.9f);
                UI.CreateText(ref container, parent: "title", color: GetColor(hex: COLOR_PRIMARY), text: _instance.lang.GetMessage("AdminPanel.Setting.Ui.Select.Title", _instance, player.UserIDString), size: 15, font: "robotocondensed-bold.ttf", outlineColor: GetColor(hex: "#000000", alpha: 0.1f));
                UI.CreatePanel(ref container, parent: "main", name: "marge", xMin: 0.05f, xMax: 0.95f, yMin: 0.1f, yMax: 0.90f);
                UI.CreatePanel(ref container, parent: "marge", name: "uiTitle_0", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: 0.9f);
                UI.CreateText(ref container, parent: "uiTitle_0", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Ui.Select.0", _instance, player.UserIDString), size: 10, xMin: 0.125f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                UI.CreatePanel(ref container, parent: "marge", name: "ui_0", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), yMin: 0.68f, yMax: 0.88f);
                UI.CreateImage(ref container, parent: "ui_0", image: GetImage(name: "ui_0"), color: GetColor(hex: "#FFFFFF"));
                UI.CreateProtectedButton(ref container, parent: "ui_0", command: "craidcontroller.admin.ui select Ui.DefaultUi 0");
                UI.CreatePanel(ref container, parent: "main", name: "close", yMax: 0.075f);
                UI.CreateProtectedButton(ref container, parent: "close", textColor: GetColor(COLOR_ERROR), text: _instance.lang.GetMessage("Btn.Close", _instance, player.UserIDString), size: 11, command: "craidcontroller.admin.close popup");
                CuiHelper.AddUi(player, container);
            }

            public void DrawDeployables(string propriety, int page = 0, string search = "")
            {
                CuiHelper.DestroyUi(player, UI_ADMIN_PANEL + "deployables");
                var container = UI.CreateContainer(name: UI_ADMIN_PANEL + "deployables", color: GetColor(hex: "#000000", alpha: 0.9f), parent: "Overlay", blur: true);
                UI.CreatePanel(ref container, parent: UI_ADMIN_PANEL + "deployables", name: "deployablesShow", xMin: 0.3f, xMax: 0.7f, yMin: 0.2f, yMax: 0.7f);
                UI.CreateSprite(ref container, parent: "deployablesShow", name: "left", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), xMax: 0.005f);
                UI.CreateSprite(ref container, parent: "deployablesShow", name: "right", sprite: "assets/content/ui/ui.background.transparent.linear.psd", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.5f), xMin: 0.995f);
                UI.CreatePanel(ref container, parent: "deployablesShow", name: "main", color: GetColor(hex: "#000000", alpha: 0.7f), xMin: 0.005f, xMax: 0.995f);
                UI.CreatePanel(ref container, parent: "main", name: "search", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), xMin: 0.05f, xMax: 0.95f, yMin: 0.9f, yMax: 0.98f);
                UI.CreateText(ref container, parent: "search", color: GetColor(hex: "#FFFFFF"), text: _instance.lang.GetMessage("AdminPanel.Setting.Bypass.Item.Search", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.3f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.3f));
                UI.CreatePanel(ref container, parent: "search", name: "searchInput", color: GetColor(hex: "#000000", alpha: 0.5f), xMin: 0.35f, xMax: 0.975f, yMin: 0.15f, yMax: 0.85f);
                UI.CreateInputField(ref container, parent: "searchInput", color: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: search, size: 11, command: $"craidcontroller.admin.item add {propriety} {page}");
                UI.CreatePanel(ref container, parent: "main", name: "list", yMax: 0.9f);
                Tuple<object, PropertyInfo> propertyTuple = FindProperty(_cfg, propriety);
                object parentObj = propertyTuple.Item1;
                PropertyInfo property = propertyTuple.Item2;

                if (property == null && property.PropertyType != typeof(List<string>) && parentObj == null)
                    return;

                List<string> existingItems = new List<string>();

                if (property.GetValue(parentObj) != null)
                    existingItems = (List<string>)property.GetValue(parentObj);

                List<ItemDefinition> deployables = GetAllDeployables().Where(item => item.shortname.Contains(search) && !existingItems.Contains(item.shortname)).ToList();
                const int itemsPerPage = 8;
                int startIndex = page * itemsPerPage;
                int totalPages = (int)Math.Ceiling((double)deployables.Count / itemsPerPage);

                if (startIndex >= deployables.Count)
                    return;

                for (int i = startIndex; i < Math.Min(startIndex + itemsPerPage, deployables.Count); i++)
                {
                    DrawItemList(ref container, "list", deployables[i].shortname, i, startIndex);
                    UI.CreateProtectedButton(ref container, parent: $"itemPanel_{i}", command: $"craidcontroller.admin.item addComplete {propriety} {deployables[i].shortname}");
                }

                if (page > 0)
                    UI.CreateProtectedButton(ref container, parent: "main", name: "previous", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.75f), textColor: GetColor(hex: "#FFFFFF"), text: "<", size: 11, xMin: 0.44f, xMax: 0.49f, yMin: 0.08f, yMax: 0.13f, command: $"craidcontroller.admin.item add {propriety} {page - 1} {search}");

                if (page < totalPages - 1)
                    UI.CreateProtectedButton(ref container, parent: "main", name: "next", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.75f), textColor: GetColor(hex: "#FFFFFF"), text: ">", size: 11, xMin: 0.51f, xMax: 0.56f, yMin: 0.08f, yMax: 0.13f, command: $"craidcontroller.admin.item add {propriety} {page + 1} {search}");

                UI.CreatePanel(ref container, parent: "main", name: "close", yMax: 0.075f);
                UI.CreateProtectedButton(ref container, parent: "close", textColor: GetColor(COLOR_PRIMARY), text: _instance.lang.GetMessage("Btn.Close", _instance, player.UserIDString), size: 14, xMin: 0.425f, xMax: 0.575f, command: "craidcontroller.admin.close popup");
                CuiHelper.AddUi(player, container);
            }

            public void DrawItem(ref CuiElementContainer container, string parent, string propriety, int page, int itemsPerPage = 8)
            {
                Tuple<object, PropertyInfo> propertyTuple = FindProperty(_cfg, propriety);
                object parentObj = propertyTuple.Item1;
                PropertyInfo property = propertyTuple.Item2;

                if (property == null && property.PropertyType != typeof(List<string>) && parentObj == null)
                    return;

                List<string> itemList = new List<string>();

                if (property.GetValue(parentObj) != null)
                    itemList = (List<string>)property.GetValue(parentObj);

                int startIndex = page * itemsPerPage;
                int totalPages = (int)Math.Ceiling((double)itemList.Count / itemsPerPage);
                UI.CreateImage(ref container, parent: parent, image: "assets/icons/authorize.png", color: GetColor(hex: COLOR_SUCCESS, alpha: 0.7f), outlineColor: GetColor(hex: "#000000", alpha: 0.8f), xMin: 0.9f, xMax: 0.95f, yMin: 0.03f, yMax: 0.21f, DistanceX: 0.1f, DistanceY: 0.1f);
                UI.CreateProtectedButton(ref container, parent: parent, xMin: 0.9f, xMax: 0.95f, yMin: 0.04f, yMax: 0.14f, command: $"craidcontroller.admin.item add {propriety}");

                if (itemList.IsEmpty())
                {
                    UI.CreatePanel(ref container, parent: parent, name: "itemPanelEmpty", color: GetColor(hex: "#FFFFFF", alpha: 0.1f), xMin: 0.02f, xMax: 0.97f, yMin: 0.50f, yMax: 0.93f);
                    UI.CreateText(ref container, parent: "itemPanelEmpty", color: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: _instance.lang.GetMessage("AdminPanel.Setting.Bypass.Item.NoData", _instance, player.UserIDString), size: 10, xMin: 0.05f, xMax: 0.95f, yMax: 0.95f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.1f));
                    return;
                }

                if (startIndex >= itemList.Count)
                    return;

                for (int i = startIndex; i < Math.Min(startIndex + itemsPerPage, itemList.Count); i++)
                {
                    DrawItemList(ref container, parent, itemList[i], i, startIndex, true);
                    UI.CreateImage(ref container, parent: $"itemPanel_{i}", image: "assets/icons/clear.png", color: GetColor(hex: COLOR_ERROR, alpha: 0.7f), outlineColor: GetColor(hex: "#000000", alpha: 0.8f), xMin: 0.80f, xMax: 0.97f, yMin: 0.7f, yMax: 0.95f, DistanceX: 0.1f, DistanceY: 0.1f);
                    UI.CreateProtectedButton(ref container, parent: $"itemPanel_{i}", xMin: 0.80f, xMax: 0.97f, yMin: 0.7f, yMax: 0.95f, command: $"craidcontroller.admin.item remove {propriety} {itemList[i]}");
                }

                if (page > 0)
                    UI.CreateProtectedButton(ref container, parent: parent, name: "previous", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.75f), textColor: GetColor(hex: "#FFFFFF"), text: "<", size: 10, xMin: 0.44f, xMax: 0.49f, yMin: 0.03f, yMax: 0.21f, command: $"craidcontroller.admin.item page {propriety} {page - 1}");

                if (page < totalPages - 1)
                    UI.CreateProtectedButton(ref container, parent: parent, name: "next", color: GetColor(hex: COLOR_PRIMARY, alpha: 0.75f), textColor: GetColor(hex: "#FFFFFF"), text: ">", size: 10, xMin: 0.51f, xMax: 0.56f, yMin: 0.03f, yMax: 0.21f, command: $"craidcontroller.admin.item page {propriety} {page + 1}");
            }

            private void DrawItemList(ref CuiElementContainer container, string parent, string itemName, int index, int startIndex, bool resizeImg = false)
            {
                int row = (index - startIndex) / 4;
                int column = (index - startIndex) % 4;
                const float panelWidth = 0.225f;
                float panelHeight = resizeImg ? 0.65f : 0.36f;
                const float xOffset = 0.02f;
                const float yOffset = 0.035f;
                float xMin = xOffset + ((panelWidth + xOffset) * column);
                float xMax = xMin + panelWidth;
                float yMin = 1 - yOffset - ((panelHeight + yOffset) * (row + 1));
                float yMax = yMin + panelHeight;
                string panelName = $"itemPanel_{index}";
                UI.CreatePanel(ref container, parent: parent, name: panelName, color: GetColor(hex: "#FFFFFF", alpha: 0.05f), xMin: xMin, xMax: xMax, yMin: yMin, yMax: yMax);
                UI.CreateImage(ref container, parent: panelName, image: GetImage(name: itemName), color: GetColor(hex: "#FFFFFF"), xMin: resizeImg ? 0.3f : 0.2f, xMax: resizeImg ? 0.7f : 0.8f, yMin: 0.3f, yMax: 0.9f);
                UI.CreateText(ref container, parent: panelName, color: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: itemName, size: 7, yMax: 0.25f, outlineColor: GetColor(hex: "#000000", alpha: 0.1f));
            }

            public void DrawError(string text)
            {
                CuiHelper.DestroyUi(player, UI_ADMIN_PANEL + "error");
                var container = UI.CreateContainer(name: UI_ADMIN_PANEL + "error", xMin: 0.15f, xMax: 0.64f, yMin: 0.815f, yMax: 0.84f, parent: "Overlay");
                UI.CreatePanel(ref container, parent: UI_ADMIN_PANEL + "error", name: "error");
                UI.CreateText(ref container, parent: "error", color: GetColor(hex: COLOR_ERROR), text: $"* {text}", size: 10, xMin: 0.05f, align: TextAnchor.MiddleLeft, outlineColor: GetColor(hex: "#000000", alpha: 0.1f));
                CuiHelper.AddUi(player, container);
                SendEffect(player, SOUND_ERROR);
            }

            private TimeRange GetLastTimeRange(DayOfWeek day) => (_mainController.currentSchedule.schedule.ContainsKey(day) && _mainController.currentSchedule.schedule[day].TimeRange.Count > 0) ? _mainController.currentSchedule.schedule[day].TimeRange.Last() : null;

            private string GetEndTimeRange(string hour, string minute) => (hour == "23" && minute == "59") ? "00h00" : $"{hour}h{minute}";
            private bool CheckCanAddTimeRange(DayOfWeek day)
            {
                List<TimeRange> timeRanges = _mainController.currentSchedule.schedule[day].TimeRange;

                if (timeRanges.Count == 0)
                    return true;

                TimeRange lastTimeRange = timeRanges.LastOrDefault();

                if (lastTimeRange == null)
                    return true;

                if (lastTimeRange.EndHour == 23 && lastTimeRange.EndMinute >= 50)
                    return false;

                return true;
            }

            public void DestroyUI(bool locker = false)
            {
                CuiHelper.DestroyUi(player, UI_ADMIN_PANEL);
                CuiHelper.DestroyUi(player, UI_ADMIN_PANEL + "feed");
                CuiHelper.DestroyUi(player, UI_ADMIN_PANEL + "uiList");
                CuiHelper.DestroyUi(player, UI_ADMIN_PANEL + "timeRange");
                CuiHelper.DestroyUi(player, UI_ADMIN_PANEL + "deployables");
                CuiHelper.DestroyUi(player, UI_ADMIN_PANEL + "error");

                if (locker)
                {
                    CuiHelper.DestroyUi(player, UI_ADMIN_PANEL_LOCKER);
                    this.locker = false;
                }
            }

            private void OnDestroy() => DestroyUI(locker: true);
        }

        #endregion

        #region COMMANDS

        private void CmdDrawInterface(BasePlayer player)
        {
            if (_playerControllers.ContainsKey(player.userID))
            {
                PlayerController playerController = _playerControllers[player.userID];
                playerController.DrawPanel();
            }
        }

        [ConsoleCommand("craidcontroller.player.close")]
        private void UiCommandPlayerClose(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (player == null || !_playerControllers.ContainsKey(player.userID))
                return;

            PlayerController playerController = _playerControllers[player.userID];
            playerController.DestroyUI(panel: true, locker: true);
        }

        [ConsoleCommand("craidcontroller.player.tc.bypass")]
        private void UiCommandPlayerTcBypass(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (player == null || !_playerControllers.ContainsKey(player.userID))
                return;

            PlayerController playerController = _playerControllers[player.userID];

            switch (arg.Args[0])
            {
                case "enable":

                    _tcController.EnableTcProtection(Convert.ToUInt64(arg.Args[1]));

                    break;

                case "disable":

                    if (!_mainController.currentSchedule.isRaidTime)
                    {
                        playerController.DrawTCBypassPanelModal(Convert.ToUInt64(arg.Args[1]));
                        return;
                    }
                    else
                    {
                        _tcController.DisableTcProtection(Convert.ToUInt64(arg.Args[1]));
                    }

                    break;

                case "confirm":

                        _tcController.DisableTcProtection(Convert.ToUInt64(arg.Args[1]));

                    break;
            }
            
            playerController.DestroyUI(tc: true);
            playerController.DrawTCBypassPanel(Convert.ToUInt64(arg.Args[1]));
        }

        [ConsoleCommand("craidcontroller.player.setting")]
        private void UiCommandPlayerSetting(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (player == null || !_playerControllers.ContainsKey(player.userID))
                return;

            PlayerController playerController = _playerControllers[player.userID];
            PlayerSetting.Setting playerSetting = _playerSetting.data[player.userID];

            switch (arg.Args[0])
            {
                case "HideUi":

                    playerSetting.HideUi = !bool.Parse(arg.Args[1]);

                    break;

                case "Ui":

                    var directionUi = arg.Args[1];
                    int currentUi = playerSetting.Ui;
                    currentUi = directionUi == "next" ? (currentUi < _uiList.Count - 1 ? currentUi + 1 : 0) : (directionUi == "previous" ? (currentUi > 0 ? currentUi - 1 : _uiList.Count - 1) : currentUi);
                    playerSetting.Ui = currentUi;

                    break;

                case "Side":

                    playerSetting.Side = playerSetting.Side == 0 ? 1 : 0;

                    break;

                case "Language":

                    var directionLang = arg.Args[1];
                    string currentLang = playerSetting.Language;
                    int currentIndex = _languages.IndexOf(currentLang);
                    currentIndex = (directionLang == "next") ? ((currentIndex < _languages.Count - 1) ? (currentIndex + 1) : 0) : ((currentIndex > 0) ? (currentIndex - 1) : (_languages.Count - 1));
                    playerSetting.Language = _languages[currentIndex];

                    break;
            }

            _playerSetting.SaveData();
            playerController.forceUpdate = true;
            playerController.DestroyUI(panel: true);
            playerController.DrawPanel();
        }

        [ConsoleCommand("craidcontroller.admin.open")]
        private void UiCommandAdminOpen(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (player == null || !_playerControllers.ContainsKey(player.userID) || !_adminControllers.ContainsKey(player.userID))
                return;

            AdminController adminController = _adminControllers[player.userID];
            PlayerController playerController = _playerControllers[player.userID];
            playerController.DestroyUI(panel: true, locker: true);
            adminController.DrawPanel();
        }

        [ConsoleCommand("craidcontroller.admin.close")]
        private void UiCommandAdminClose(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (player == null || !_adminControllers.ContainsKey(player.userID))
                return;

            AdminController adminController = _adminControllers[player.userID];

            if (arg?.Args?.Length >= 1 && arg.Args[0] == "popup")
            {
                if(arg?.Args?.Length >= 2 && arg.Args[1] == "clear")
                {
                    adminController.timeRangeCurrent = new TimeRange() { StartHour = -1, StartMinute = -1, EndHour = -1, EndMinute = -1 };
                    adminController.timeRangeCurrentEndOfDay = true;
                }

                adminController.DestroyUI();
                adminController.DrawPanel();
                return;
            }

            adminController.DestroyUI(locker: true);
        }

        [ConsoleCommand("craidcontroller.admin.switchtab")]
        private void UiCommandAdminSwitchTab(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (!_adminControllers.ContainsKey(player.userID))
                return;

            AdminController adminController = _adminControllers[player.userID];
            adminController.activeTab = (AdminPanelTab)Enum.Parse(typeof(AdminPanelTab), arg.Args[0]);
            adminController.DestroyUI();
            adminController.DrawPanel();
        }

        [ConsoleCommand("craidcontroller.admin.feed.tab")]
        private void UiCommandAdminadminFeedTab(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (!_adminControllers.ContainsKey(player.userID))
                return;

            AdminController adminController = _adminControllers[player.userID];
            adminController.feedTab = arg.Args[0];
            adminController.feedPage = Convert.ToInt32(arg.Args[1]);
            adminController.DestroyUI();
            adminController.DrawPanel();
        }

        [ConsoleCommand("craidcontroller.admin.feed.show")]
        private void UiCommandAdminFeedShow(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (!_adminControllers.ContainsKey(player.userID))
                return;

            AdminController adminController = _adminControllers[player.userID];
            adminController.feedNews = Convert.ToInt32(arg.Args[0]);
            adminController.DrawFeedShow();

            if (!_feed.IsRead(player.userID, arg.Args[0]))
                _feed.MarkRead(player.userID, arg.Args[0]);
        }

        [ConsoleCommand("craidcontroller.admin.schedule")]
        private void UiCommandAdminSchedule(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (!_adminControllers.ContainsKey(player.userID))
                return;

            AdminController adminController = _adminControllers[player.userID];

            string type = arg.Args[0];
            DayOfWeek day = (DayOfWeek)Enum.Parse(typeof(DayOfWeek), arg.Args[1]);
            var timeRanges = _mainController.currentSchedule.schedule[day].TimeRange;

            switch (type)
            {
                case "hide":

                    adminController.scheduleActiveDay = string.Empty;

                    break;
                case "view":

                    adminController.scheduleActiveDay = day.ToString();

                    break;

                case "add":

                    adminController.DrawTimeRangeShow();

                    return;

                case "inAdd":

                    int index;
                    if (int.TryParse(arg.Args[2], out index))
                    {
                        string value = arg.GetString(3);
                        adminController.timeRangeCurrent =
                            index == 1 ? new TimeRange() { StartHour = Convert.ToInt32(value), StartMinute = -1, EndHour = -1, EndMinute = -1} :
                            index == 2 ? new TimeRange() { StartHour = adminController.timeRangeCurrent.StartHour, StartMinute = Convert.ToInt32(value), EndHour = -1, EndMinute = -1 } :
                            index == 3 ? new TimeRange() { StartHour = adminController.timeRangeCurrent.StartHour, StartMinute = adminController.timeRangeCurrent.StartMinute, EndHour = Convert.ToInt32(value), EndMinute = -1 } :
                            new TimeRange() { StartHour = adminController.timeRangeCurrent.StartHour, StartMinute = adminController.timeRangeCurrent.StartMinute, EndHour = adminController.timeRangeCurrent.EndHour, EndMinute = Convert.ToInt32(value) };
                    }
                    else
                    {
                        adminController.timeRangeCurrentEndOfDay = !bool.Parse(arg.Args[2]);
                    }

                    CuiHelper.DestroyUi(player, UI_ADMIN_PANEL + "timeRange");
                    adminController.DrawTimeRangeShow();

                    return;

                case "addComplete":

                    timeRanges.Add(new TimeRange { StartHour = Convert.ToInt32(arg.Args[2]), StartMinute = Convert.ToInt32(arg.Args[3]), EndHour = Convert.ToInt32(arg.Args[4]), EndMinute = Convert.ToInt32(arg.Args[5]) });
                    adminController.timeRangeCurrent = new TimeRange() { StartHour = -1, StartMinute = -1, EndHour = -1, EndMinute = -1 };
                    adminController.timeRangeCurrentEndOfDay = true;
                    _mainController.configChanged = true;
                    SaveConfig();

                    break;

                case "delete":

                    if (timeRanges.Count > Convert.ToInt32(arg.Args[2]))
                    {
                        timeRanges.RemoveAt(Convert.ToInt32(arg.Args[2]));
                        _mainController.configChanged = true;
                        SaveConfig();
                    }

                    break;
            }

            adminController.DestroyUI();
            adminController.DrawPanel();
        }

        [ConsoleCommand("craidcontroller.admin.item")]
        private void UiCommandAdminItem(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (!_adminControllers.ContainsKey(player.userID))
                return;

            AdminController adminController = _adminControllers[player.userID];

            switch (arg.Args[0])
            {
                case ITEM_TYPE_REMOVE:

                    ModifyList(arg.Args[2], arg.Args[1]);

                    break;

                case ITEM_TYPE_ADD:

                    string propriety = arg.Args[1];
                    int page;

                    if (arg.Args.Length >= 3 && int.TryParse(arg.Args[2], out page))
                    {
                        if (arg.Args.Length > 3)
                        {
                            string search = string.Join(" ", arg.Args.Skip(3));
                            adminController.DrawDeployables(propriety, page, search);
                        }
                        else
                        {
                            adminController.DrawDeployables(propriety, page);
                        }
                    }
                    else
                    {
                        adminController.DrawDeployables(propriety);
                    }

                    return;

                case ITEM_TYPE_ADD_COMPLETE:

                    ModifyList(arg.Args[2], arg.Args[1], true);

                    break;

                case ITEM_TYPE_PAGE:

                    if(arg.Args[1] == ITEM_TC_PROTECT)
                        adminController.tcProtectItemPage = Convert.ToInt32(arg.Args[2]);

                    if (arg.Args[1] == ITEM_TC_NO_PROTECT)
                        adminController.tcNoProtectItemPage = Convert.ToInt32(arg.Args[2]);

                    break;
            }

            adminController.DestroyUI();
            adminController.DrawPanel();
        }

        [ConsoleCommand("craidcontroller.admin.slider")]
        private void UiCommandAdminSlider(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (!_adminControllers.ContainsKey(player.userID))
                return;

            AdminController adminController = _adminControllers[player.userID];

            switch (arg.Args[0])
            {
                case "list":

                    ModifySlider<string>(arg.Args[3], arg.Args[1], (List<string>)GetType().GetField(arg.Args[2], BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this));
                    break;

                case "schedule":

                    ModifyScheduleStatus(arg.Args[2], (DayOfWeek)Enum.Parse(typeof(DayOfWeek), arg.Args[1]));
                    _mainController.configChanged = true;
                    break;

                case "activeUi":

                    ModifyActiveUi(arg.Args[1]);
                    _mainController.configChanged = true;
                    break;

                case "tcBypass":

                    ModifyTcBypass(arg.Args[1]);
                    break;

            }

            adminController.DestroyUI();
            adminController.DrawPanel();
        }

        [ConsoleCommand("craidcontroller.admin.ui")]
        private void UiCommandAdminUiList(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (!_adminControllers.ContainsKey(player.userID))
                return;

            AdminController adminController = _adminControllers[player.userID];

            string type = arg.Args[0];

            switch (type)
            {
                case "show":

                    adminController.DrawUiList();
                    return;

                case "select":

                    ModifyProperty<int>(Convert.ToInt32(arg.Args[2]), arg.Args[1]);
                    _mainController.configChanged = true;
                    break;
            }

            adminController.DestroyUI();
            adminController.DrawPanel();
        }

        [ConsoleCommand("craidcontroller.admin.ui.side")]
        private void UiCommandAdminUiSide(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (!_adminControllers.ContainsKey(player.userID))
                return;

            AdminController adminController = _adminControllers[player.userID];

            _cfg.Ui.Side = Convert.ToInt32(arg.Args[0]);
            SaveConfig();
            _mainController.configChanged = true;
            adminController.DestroyUI();
            adminController.DrawPanel();
        }

        [ConsoleCommand("craidcontroller.admin.modify")]
        private void UiCommandAdminModify(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (!_adminControllers.ContainsKey(player.userID))
                return;

            AdminController adminController = _adminControllers[player.userID];
            string propertyName = arg.Args[1];

            switch (arg.Args[0])
            {
                case "int":

                    ModifyProperty<int>(Convert.ToInt32(arg.Args[2]), propertyName);

                    break;

                case "float":

                    ModifyProperty<float>(float.Parse(arg.Args[2]), propertyName);
                    break;

                case "bool":

                    ModifyProperty<bool>(!bool.Parse(arg.Args[2]), propertyName);

                    if (propertyName == "Enable")
                    {
                        SaveConfig();
                        Interface.Oxide.ReloadPlugin(Title);
                    }

                    break;

                case "string":

                    string value = string.Join(" ", arg.Args.Skip(3));

                    if (string.IsNullOrEmpty(value))
                            return;

                    string pattern = arg.Args[2];
                    Regex regex = new Regex(pattern);

                    if (!regex.IsMatch(value))
                    {
                        string errorKey = GetErrorKeyByPattern(pattern);
                        if (!string.IsNullOrEmpty(errorKey))
                        {
                            string error = lang.GetMessage(errorKey, _instance, player.UserIDString);
                            adminController.DrawError(error);
                            return;
                        }
                    }

                    if (pattern == "^[0-9]*$")
                    {
                        ModifyProperty<int>(Convert.ToInt32(value), propertyName);
                    }
                    else
                    {
                        ModifyProperty<string>(value, propertyName);

                        if (propertyName == "Global.Command" && value != _cfg.Global.Command)
                            cmd.AddChatCommand(value, this, "CmdDrawInterface");
                    }

                break;
            }

            if (_forceRefresh.Contains(propertyName))
                _mainController.configChanged = true;

            adminController.DestroyUI();
            adminController.DrawPanel();
        }

        #endregion

        #region CONFIG

        public static ConfigData _cfg;
        public class ConfigData
        {
            public bool Enable { get; set; }

            public GlobalSetting Global { get; set; }

            public BypassSetting Bypass { get; set; }

            public MessageSetting Message { get; set; }

            public UiSetting Ui { get; set; }

            public Dictionary<DayOfWeek, ScheduleSetting> Schedule { get; set; }

            public class GlobalSetting
            {
                public string Command { get; set; }
                public bool BlockDamage { get; set; }
                public string CmdExecute { get; set; }
                public bool CmdAllTeam { get; set; }
                public string Timezone { get; set; }
                //******************************************************************** TODO ****************************************************
                //public int MaxRaidPerPlayer { get; set; }
                //public bool MaxRaidPerTeam { get; set; }
                //******************************************************************************************************************************
                public int RaidDelay { get; set; }
                public int RaidProtectionDuration { get; set; }
            }
            public class BypassSetting
            {
                public bool Owner { get; set; }
                public bool Admin { get; set; }
                public bool Mate { get; set; }
                public bool Twig { get; set; }
                public bool TcDecay { get; set; }
                public bool NoTc { get; set; }
                public bool BuildingPrivilege { get; set; }
                public TcBypass TcBypass{ get; set; }
                public List<string> TcProtectItem { get; set; }
                public List<string> TcNoProtectItem { get; set; }
            }
            public class MessageSetting
            {
                public string MessageType { get; set; }
                public SyntaxConfig Syntax { get; set; }
                public bool Effect { get; set; }
                public int MinuteToStartMessage { get; set; }
                public int MinuteToEndMessage { get; set; }
                public bool AllowedMessage { get; set; }
                public bool FinishedMessage { get; set; }
                public bool FailMessage { get; set; }
            }

            public class SyntaxConfig
            {
                public string Prefix { get; set; }
                public string Icon { get; set; }
            }

            public class UiSetting
            {
                public ActiveUI ActiveUi { get; set; }
                public int DefaultUi { get; set; }
                public int Side { get; set; }
                public float Ymax { get; set; }
                public string BackgroundColor { get; set; }
                public float BackgroundOpacity { get; set; }
                public bool HideNotTime { get; set; }
                public bool HideIsTime { get; set; }
                public bool AllowPlayerHide { get; set; }
                public bool AllowPlayerChange { get; set; }
                public float Rate { get; set; }
            }

            public class ScheduleSetting
            {
                public ScheduleStatus Status { get; set; }
                public List<TimeRange> TimeRange { get; set; }
            }

            public class TimeRange
            {
                public int StartHour { get; set; }
                public int StartMinute { get; set; }
                public int EndHour { get; set; }
                public int EndMinute { get; set; }
            }
        }

        public static ConfigData GetDefaultConfig() => new ConfigData
        {
            Enable = true,
            Global = new GlobalSetting
            {
                Command = "craid",
                BlockDamage = true,
                CmdExecute = "ban {player} 'Raiding out of schedules'",
                CmdAllTeam = true,
                Timezone = "0",
                //******************************************************************** TODO ****************************************************
                //MaxRaidPerPlayer = 2,
                //MaxRaidPerTeam = true,
                //******************************************************************************************************************************
                RaidDelay = 60,
                RaidProtectionDuration = 60
            },
            Bypass = new BypassSetting
            {
                Owner = true,
                Admin = true,
                Mate = true,
                Twig = false,
                TcDecay = false,
                NoTc = false,
                BuildingPrivilege = false,
                TcBypass = TcBypass.Disabled,
                TcProtectItem = new List<string>{},
                TcNoProtectItem = new List<string>{}
            },
            Message = new MessageSetting
            {
                MessageType = "Notifications",
                Syntax = new SyntaxConfig
                {
                    Prefix = "<color=#ebd077>• Raid Controller: </color>",
                    Icon = "0"
                },
                Effect = true,
                MinuteToStartMessage = 30,
                MinuteToEndMessage = 30,
                AllowedMessage = true,
                FinishedMessage = true,
                FailMessage = true,
            },
            Ui = new UiSetting
            {
                ActiveUi = ActiveUI.Activated,
                DefaultUi = 0,
                Side = 1,
                Ymax = 0.99f,
                BackgroundColor = "#000000",
                BackgroundOpacity = 0f,
                HideNotTime = false,
                HideIsTime = false,
                AllowPlayerHide = true,
                AllowPlayerChange = true,
                Rate = 1.0f,
            },
            Schedule = new Dictionary<DayOfWeek, ScheduleSetting>
            {
                {
                    DayOfWeek.Monday, new ScheduleSetting
                    {
                        Status = ScheduleStatus.AllowedAllDay,
                        TimeRange = new List<TimeRange>()
                    }
                },
                {
                    DayOfWeek.Tuesday, new ScheduleSetting
                    {
                        Status = ScheduleStatus.AllowedAllDay,
                        TimeRange = new List<TimeRange>()
                        {
                            new TimeRange
                            {
                                StartHour = 10,
                                StartMinute = 0,
                                EndHour = 15,
                                EndMinute = 0,
                            }
                        }
                    }
                },
                {
                    DayOfWeek.Wednesday, new ScheduleSetting
                    {
                        Status = ScheduleStatus.NotAllowedAllDay,
                        TimeRange = new List<TimeRange>()
                    }
                },
                {
                    DayOfWeek.Thursday, new ScheduleSetting
                    {
                        Status = ScheduleStatus.AllowedAllDay,
                        TimeRange = new List<TimeRange>()
                    }
                },
                {
                    DayOfWeek.Friday, new ScheduleSetting
                    {
                        Status = ScheduleStatus.AllowedAllDay,
                        TimeRange = new List<TimeRange>()
                    }
                },
                {
                    DayOfWeek.Saturday, new ScheduleSetting
                    {
                        Status = ScheduleStatus.AllowedAllDay,
                        TimeRange = new List<TimeRange>()
                    }
                },
                {
                    DayOfWeek.Sunday, new ScheduleSetting
                    {
                        Status = ScheduleStatus.AllowedAllDay,
                        TimeRange = new List<TimeRange>()
                    }
                }
            }
        };

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _cfg = Config.ReadObject<ConfigData>();

                if (_cfg == null || (_cfg.Global == null || _cfg.Bypass == null || _cfg.Message == null || _cfg.Ui == null || _cfg.Schedule == null))
                    LoadDefaultConfig();
            }
            catch
            {
                PrintError($"Configuration file is corrupt! If you have just upgraded to version {Version}, delete the configuration file and reload. Else if check your config file at https://jsonlint.com/");
                LoadDefaultConfig();
                return;
            }

            SaveConfig();
        }
        protected override void LoadDefaultConfig() => _cfg = GetDefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(_cfg);

        #endregion

        #region HELPERS

        private List<ulong> GetTeamMembers(BasePlayer player)
        {
            if(player.Team == null)
                return null;

            return player.Team.members;
        }

        private void SendToAllPlayerControllers(string key, int style = 2, params object[] args)
        {
            foreach (var x in _playerControllers)
                _playerControllers[x.Key].alertMessage.AddAlert(key, style, args);
        }

        public string GetItemShortnameFromEntity(BaseEntity entity)
        {
            if (entity == null || entity.ShortPrefabName == null || entity.ShortPrefabName == string.Empty)
                return string.Empty;

            string prefabName = entity.ShortPrefabName;
            prefabName = prefabName.Replace("_deployed", "")
                                   .Replace(".deployed", "")
                                   .Replace("_", ".")
                                   .Replace(".entity", "");
            return prefabName;
        }

        private void Nullify(HitInfo info, BaseCombatEntity entity)
        {
            info.damageTypes = new Rust.DamageTypeList();
            info.HitEntity = null;
            info.HitMaterial = 0;
            info.PointStart = Vector3.zero;
            _fireBallManager.CatchFireBall(entity.net.ID.Value);
        }

        public bool IsOwner(ulong initiatorID, ulong entityOwnerID) => initiatorID == entityOwnerID;

        private bool IsOnline(BasePlayer player) => BasePlayer.activePlayerList.Contains(player);

        private bool IsRaidableBase(BaseEntity entity) => RaidableBases.Call<bool>("HasEventEntity", entity);

        private bool IsMate(BasePlayer player, ulong target)
        {
            if (IsOwner(player.userID, target))
                return false;

            if (player.Team == null)
                return false;

            if (player.Team.members.Contains(target))
                return true;

            return false;
        }

        private bool IsTwigs(BaseEntity entity)
        {
            if (entity is BuildingBlock)
            {
                BuildingBlock block = entity as BuildingBlock;
                if (block.grade == BuildingGrade.Enum.Twigs)
                    return true;
            }

            return false;
        }

        private bool IsProtectedByTC(BaseEntity entity)
        {
            BuildingPrivlidge buildingPrivilege = entity.GetBuildingPrivilege();

            if (buildingPrivilege != null)
                return true;

            return false;
        }

        private bool IsProtectedByTCAndDecaying(BaseEntity entity)
        {
            BuildingPrivlidge buildingPrivilege = entity.GetBuildingPrivilege();

            if (buildingPrivilege != null)
            {
                float minutesLeft = buildingPrivilege.GetProtectedMinutes();

                if (minutesLeft == 0)
                    return true;
            }

            return false;
        }

        private static string GetColor(string hex, float alpha = 1f)
        {
            var color = System.Drawing.ColorTranslator.FromHtml(htmlColor: hex);
            var r = Convert.ToInt16(value: color.R) / 255f;
            var g = Convert.ToInt16(value: color.G) / 255f;
            var b = Convert.ToInt16(value: color.B) / 255f;
            return $"{r} {g} {b} {alpha}";
        }

        private static string Lang(string key, string id = null, params object[] args) => string.Format(_instance.lang.GetMessage(key, _instance, id), args);

        private static void SendEffect(BasePlayer player, string sound)
        {
            var effect = new Effect(sound, player, 0, Vector3.zero, Vector3.forward);
            EffectNetwork.Send(effect, player.net.connection);
        }

        private static string GetErrorKeyByPattern(string pattern)
        {
            switch (pattern)
            {
                case "^[a-zA-Z]*$": // STRING WITHOUT SPACE
                    return "AdminPanel.Error.1";
                case "^[0-9]*$": // INT WITHOUT SPACE
                    return "AdminPanel.Error.2";
                case "^#[a-fA-F0-9]{6}$": // HEX
                    return "AdminPanel.Error.3";
                case "^[0-9]{1,17}$": // STEAM ID
                    return "AdminPanel.Error.4";
                default:
                    return null;
            }
        }

        private static string ToTimeString(int value) => value < 10 ? $"0{value}" : value.ToString();

        private static string ToDateLangFormat(object value, string lang)
        {
            CultureInfo cultureInfo = new CultureInfo(lang);
            DateTimeFormatInfo langDate = cultureInfo.DateTimeFormat;

            if (value is DayOfWeek)
            {
                DayOfWeek dayOfWeek = (DayOfWeek)value;
                return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(langDate.GetDayName(dayOfWeek));
            }
            else if (value is string)
            {
                string timeString = value as string;
                DateTime parsedTime, parsedDateTime;

                if (DateTime.TryParseExact(timeString, "H\\hm", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedTime))
                    return parsedTime.ToString("t", langDate);
                if (DateTime.TryParseExact(timeString, "yyyy-MM-dd\\THH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDateTime))
                    return parsedDateTime.ToString("» dd MMMM yyyy (HH:mm)", langDate);
                else
                    return value.ToString();
            }
            else
            {
                return value.ToString();
            }
        }

        private static Tuple<object, PropertyInfo> FindProperty(object obj, string propertyName)
        {
            string[] propertyNameParts = propertyName.Split('.');
            object currentObj = obj;
            PropertyInfo currentProperty = null;

            foreach (string part in propertyNameParts)
            {
                if (currentObj == null)
                    return Tuple.Create<object, PropertyInfo>(null, null);

                PropertyInfo foundProperty = Array.Find(currentObj.GetType().GetProperties(), property => property.Name == part);

                if (foundProperty == null)
                    return Tuple.Create<object, PropertyInfo>(null, null);

                if (part != propertyNameParts.Last())
                    currentObj = foundProperty.GetValue(currentObj, null);

                currentProperty = foundProperty;
            }

            return Tuple.Create(currentObj, currentProperty);
        }

        private static void ModifyProperty<T>(T value, string propertyName)
        {
            Tuple<object, PropertyInfo> propertyTuple = FindProperty(_cfg, propertyName);
            object parentObj = propertyTuple.Item1;
            PropertyInfo property = propertyTuple.Item2;

            if (property == null || property.PropertyType != typeof(T) || parentObj == null)
                return;

            property.SetValue(parentObj, value);
            _instance.SaveConfig();
        }

        public static void ModifySlider<T>(string direction, string propertyName, IEnumerable<T> values)
        {
            Tuple<object, PropertyInfo> propertyTuple = FindProperty(_cfg, propertyName);
            object parentObj = propertyTuple.Item1;
            PropertyInfo property = propertyTuple.Item2;

            if (property == null || !values.Any() || parentObj == null)
                return;

            T currentValue = (T)property.GetValue(parentObj);
            int currentIndex = values.ToList().IndexOf(currentValue);

            if (direction == "next")
            {
                currentIndex = currentIndex < values.Count() - 1 ? currentIndex + 1 : 0;
            }
            else if (direction == "previous")
            {
                currentIndex = currentIndex > 0 ? currentIndex - 1 : values.Count() - 1;
            }

            T newValue = values.ElementAt(currentIndex);
            property.SetValue(parentObj, newValue);

            _instance.SaveConfig();
        }

        public static void ModifyList(string value, string propertyName, bool add = false)
        {
            Tuple<object, PropertyInfo> propertyTuple = FindProperty(_cfg, propertyName);
            object parentObj = propertyTuple.Item1;
            PropertyInfo property = propertyTuple.Item2;

            if (property == null || property.PropertyType != typeof(List<string>) || parentObj == null)
                return;

            List<string> list = (List<string>)property.GetValue(parentObj);

            if (add)
                list.Add(value);
            else
                list.Remove(value);

            property.SetValue(parentObj, list);
            _instance.SaveConfig();
        }

        public static void ModifyScheduleStatus(string direction, DayOfWeek day)
        {
            if (!_cfg.Schedule.ContainsKey(day))
                return;

            ScheduleStatus currentStatus = _cfg.Schedule[day].Status;
            List<ScheduleStatus> statusValues = Enum.GetValues(typeof(ScheduleStatus)).Cast<ScheduleStatus>().ToList();
            int currentIndex = statusValues.IndexOf(currentStatus);

            if (direction == "next")
            {
                currentIndex = currentIndex < statusValues.Count - 1 ? currentIndex + 1 : 0;
            }
            else if (direction == "previous")
            {
                currentIndex = currentIndex > 0 ? currentIndex - 1 : statusValues.Count - 1;
            }

            ScheduleStatus newStatus = statusValues.ElementAt(currentIndex);
            _cfg.Schedule[day].Status = newStatus;

            _instance.SaveConfig();
        }

        public static void ModifyActiveUi(string direction)
        {
            ActiveUI currentUi = _cfg.Ui.ActiveUi;
            List<ActiveUI> uiValues = Enum.GetValues(typeof(ActiveUI)).Cast<ActiveUI>().ToList();
            int currentIndex = uiValues.IndexOf(currentUi);

            if (direction == "next")
            {
                currentIndex = currentIndex < uiValues.Count - 1 ? currentIndex + 1 : 0;
            }
            else if (direction == "previous")
            {
                currentIndex = currentIndex > 0 ? currentIndex - 1 : uiValues.Count - 1;
            }

            _cfg.Ui.ActiveUi = uiValues.ElementAt(currentIndex);
            _instance.SaveConfig();
        }

        public static void ModifyTcBypass(string direction)
        {
            TcBypass current = _cfg.Bypass.TcBypass;
            List<TcBypass> uiValues = Enum.GetValues(typeof(TcBypass)).Cast<TcBypass>().ToList();
            int currentIndex = uiValues.IndexOf(current);

            if (direction == "next")
            {
                currentIndex = currentIndex < uiValues.Count - 1 ? currentIndex + 1 : 0;
            }
            else if (direction == "previous")
            {
                currentIndex = currentIndex > 0 ? currentIndex - 1 : uiValues.Count - 1;
            }

            _cfg.Bypass.TcBypass = uiValues.ElementAt(currentIndex);
            _instance.SaveConfig();
        }

        private static List<ItemDefinition> GetAllDeployables() => ItemManager.GetItemDefinitions().Where(item => item.GetComponent<ItemModDeployable>() != null).ToList();

        private static string GetImage(string name, ulong skin = 0)
        {
            if (_instance.ImageLibrary == null)
                return string.Empty;

            string imageId = _instance.ImageLibrary.Call<string>("GetImage", name, skin);
            return imageId ?? string.Empty;
        }

        #endregion

        #region UI

        public static class UI
        {
            public static CuiElementContainer CreateContainer(string name, string color = "0 0 0 0", float xMin = 0, float xMax = 1, float yMin = 0, float yMax = 1, float OxMin = 0, float OxMax = 0, float OyMin = 0, float OyMax = 0, float fadeIn = 0.2f, float fadeOut = 0.1f, bool needsCursor = false, bool needsKeyboard = false, string parent = "Overlay", bool blur = false)
            {
                if (blur)
                {
                    return new CuiElementContainer
                    {
                        {
                            new CuiPanel
                            {
                                Image = {Color = color, Material = "assets/content/ui/uibackgroundblur.mat", FadeIn = fadeIn},
                                RectTransform = {AnchorMin = $"{xMin} {yMin}", AnchorMax = $"{xMax} {yMax}", OffsetMin = $"{OxMin} {OyMin}", OffsetMax = $"{OxMax} {OyMax}"},
                                FadeOut = fadeOut,
                                CursorEnabled = needsCursor,
                                KeyboardEnabled = needsKeyboard
                            },
                            new CuiElement().Parent = parent,
                            name
                        }
                    };
                }
                else
                {
                    return new CuiElementContainer
                    {
                        {
                            new CuiPanel
                            {
                                Image = {Color = color, FadeIn = fadeIn},
                                RectTransform = {AnchorMin = $"{xMin} {yMin}", AnchorMax = $"{xMax} {yMax}", OffsetMin = $"{OxMin} {OyMin}", OffsetMax = $"{OxMax} {OyMax}"},
                                FadeOut = fadeOut,
                                CursorEnabled = needsCursor,
                                KeyboardEnabled = needsKeyboard
                            },
                            new CuiElement().Parent = parent,
                            name
                        }
                    };
                }
            }

            public static void CreatePanel(ref CuiElementContainer container, string parent, string name = null, string color = "0 0 0 0", float xMin = 0, float xMax = 1, float yMin = 0, float yMax = 1, float OxMin = 0, float OxMax = 0, float OyMin = 0, float OyMax = 0, bool blur = false, float fadeIn = 0f, float fadeOut = 0f, bool needsCursor = false, bool needsKeyboard = false)
            {
                if (blur)
                {
                    container.Add(new CuiPanel
                    {
                        Image = { Color = color, Material = "assets/content/ui/uibackgroundblur.mat", FadeIn = fadeIn },
                        RectTransform = { AnchorMin = $"{xMin} {yMin}", AnchorMax = $"{xMax} {yMax}", OffsetMin = $"{OxMin} {OyMin}", OffsetMax = $"{OxMax} {OyMax}" },
                        FadeOut = fadeOut,
                        CursorEnabled = needsCursor,
                        KeyboardEnabled = needsKeyboard
                    },
                    parent,
                    name);
                }
                else
                {
                    container.Add(new CuiPanel
                    {
                        Image = { Color = color, FadeIn = fadeIn },
                        RectTransform = { AnchorMin = $"{xMin} {yMin}", AnchorMax = $"{xMax} {yMax}", OffsetMin = $"{OxMin} {OyMin}", OffsetMax = $"{OxMax} {OyMax}" },
                        FadeOut = fadeOut,
                        CursorEnabled = needsCursor,
                        KeyboardEnabled = needsKeyboard
                    },
                    parent,
                    name ?? "");
                }
            }

            public static void CreateText(ref CuiElementContainer container, string parent, string name = null, string color = "0 0 0 0", string text = "", int size = 0, float xMin = 0, float xMax = 1, float yMin = 0, float yMax = 1, float OxMin = 0, float OxMax = 0, float OyMin = 0, float OyMax = 0, TextAnchor align = TextAnchor.MiddleCenter, string font = "robotocondensed-regular.ttf", float fadeIn = 0f, float fadeOut = 0f, string outlineColor = "0 0 0 0")
            {
                container.Add(new CuiElement
                {
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = text,
                            FontSize = size,
                            Font = font,
                            Align = align,
                            Color = color,
                            FadeIn = fadeIn,
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"{xMin} {yMin}",
                            AnchorMax = $"{xMax} {yMax}",
                            OffsetMin = $"{OxMin} {OyMin}",
                            OffsetMax = $"{OxMax} {OyMax}"
                        },
                        new CuiOutlineComponent
                        {
                            Distance = "1 1",
                            Color = outlineColor
                        }
                    },
                    Parent = parent,
                    Name = name ?? "",
                    FadeOut = fadeOut,
                });
            }

            public static void CreateProtectedButton(ref CuiElementContainer container, string parent, string name = null, string color = "0 0 0 0", string textColor = "0 0 0 0", string text = "", int size = 0, float xMin = 0, float xMax = 1, float yMin = 0, float yMax = 1, float OxMin = 0, float OxMax = 0, float OyMin = 0, float OyMax = 0, string command = null, TextAnchor align = TextAnchor.MiddleCenter, string font = "robotocondensed-regular.ttf", float fadeIn = 0f, float fadeOut = 0f, bool needsCursor = false, bool needsKeyboard = false)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = color, FadeIn = fadeIn, Command = command },
                    RectTransform = { AnchorMin = $"{xMin} {yMin}", AnchorMax = $"{xMax} {yMax}", OffsetMin = $"{OxMin} {OyMin}", OffsetMax = $"{OxMax} {OyMax}" },
                    Text = { Text = text, FontSize = size, Font = font, Align = align, Color = textColor, FadeIn = fadeIn },
                    FadeOut = fadeOut
                },
                parent,
                name ?? "");

                if (needsCursor)
                {
                    container.Add(new CuiElement
                    {
                        Components = { new CuiNeedsCursorComponent() },
                        Parent = parent,
                        Name = name ?? "",
                        FadeOut = fadeOut,
                    });
                }

                if (needsKeyboard)
                {
                    container.Add(new CuiElement
                    {
                        Components = { new CuiNeedsCursorComponent() },
                        Parent = parent,
                        Name = name ?? "",
                        FadeOut = fadeOut,
                    });
                }
            }

            public static void CreateSprite(ref CuiElementContainer container, string parent, string name = null, string sprite = "", string color = "0 0 0 0", string outlineColor = "0 0 0 0", float xMin = 0, float xMax = 1, float yMin = 0, float yMax = 1, float OxMin = 0, float OxMax = 0, float OyMin = 0, float OyMax = 0, float DistanceX = 0, float DistanceY = 0, float fadeIn = 0f, float fadeOut = 0f)
            {
                container.Add(new CuiElement
                {
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Sprite = sprite,
                            Color = color,
                            FadeIn = fadeIn
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"{xMin} {yMin}",
                            AnchorMax = $"{xMax} {yMax}",
                            OffsetMin = $"{OxMin} {OyMin}",
                            OffsetMax = $"{OxMax} {OyMax}"
                        },
                        new CuiOutlineComponent
                        {
                            Distance = $"{DistanceX} {DistanceY}",
                            Color = outlineColor
                        }
                    },
                    Parent = parent,
                    Name = name ?? "",
                    FadeOut = fadeOut
                });
            }

            public static void CreateImage(ref CuiElementContainer container, string parent, string name = null, string image = "", string color = "0 0 0 0", string outlineColor = "0 0 0 0", float xMin = 0, float xMax = 1, float yMin = 0, float yMax = 1, float OxMin = 0, float OxMax = 0, float OyMin = 0, float OyMax = 0, float DistanceX = 0, float DistanceY = 0, float fadeIn = 0f, float fadeOut = 0f, string material = "assets/icons/iconmaterial.mat")
            {
                if (image.StartsWith("http") || image.StartsWith("www"))
                {
                    container.Add(new CuiElement
                    {
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Url = image,
                                Sprite = "assets/content/textures/generic/fulltransparent.tga",
                                Color = color,
                                FadeIn = fadeIn
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"{xMin} {yMin}",
                                AnchorMax = $"{xMax} {yMax}",
                                OffsetMin = $"{OxMin} {OyMin}",
                                OffsetMax = $"{OxMax} {OyMax}"
                            }
                        },
                        Parent = parent,
                        Name = name ?? "",
                        FadeOut = fadeOut
                    });
                }
                else if (image.StartsWith("assets"))
                {
                    container.Add(new CuiElement
                    {
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Material = material,
                                Color = color,
                                Sprite = image,
                                FadeIn = fadeIn
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"{xMin} {yMin}",
                                AnchorMax = $"{xMax} {yMax}",
                                OffsetMin = $"{OxMin} {OyMin}",
                                OffsetMax = $"{OxMax} {OyMax}"
                            },
                            new CuiOutlineComponent
                            {
                                Distance = $"{DistanceX} {DistanceY}",
                                Color = outlineColor
                            }
                        },
                        Parent = parent,
                        Name = name ?? "",
                        FadeOut = fadeOut
                    });
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = image,
                                Sprite = "assets/content/textures/generic/fulltransparent.tga",
                                Color = color,
                                FadeIn = fadeIn
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"{xMin} {yMin}",
                                AnchorMax = $"{xMax} {yMax}",
                                OffsetMin = $"{OxMin} {OyMin}",
                                OffsetMax = $"{OxMax} {OyMax}"
                            }
                        },
                        Parent = parent,
                        Name = name ?? "",
                        FadeOut = fadeOut,
                    });
                }
            }

            public static void CreateInputField(ref CuiElementContainer container, string parent, string name = null, string color = "0 0 0 0", string text = "", int size = 0, float xMin = 0, float xMax = 1, float yMin = 0, float yMax = 1, float OxMin = 0, float OxMax = 0, float OyMin = 0, float OyMax = 0, string command = null, int charLimit = 250, TextAnchor align = TextAnchor.MiddleCenter, string font = "robotocondensed-regular.ttf", bool isPassword = false, float fadeOut = 0f)
            {
                container.Add(new CuiElement
                {
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Text = text,
                            CharsLimit = charLimit,
                            Color = color,
                            IsPassword = isPassword,
                            Command = command,
                            Font = font,
                            FontSize = size,
                            Align = align,
                            NeedsKeyboard = true
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"{xMin} {yMin}",
                            AnchorMax = $"{xMax} {yMax}",
                            OffsetMin = $"{OxMin} {OyMin}",
                            OffsetMax = $"{OxMax} {OyMax}"
                        }
                    },
                    Parent = parent,
                    Name = name ?? "",
                    FadeOut = fadeOut,
                });
            }

            public static void CreateSlider(ref CuiElementContainer container, string parent, string name, string current, string command)
            {
                CreateProtectedButton(ref container, parent: parent, name: name, textColor: GetColor(hex: COLOR_PRIMARY), text: "<", size: 12, xMin: 0.025f, xMax: 0.1f, yMin: 0.1f, yMax: 0.9f, command: command + " previous");
                CreatePanel(ref container, parent: parent, name: name + "current", xMin: 0.15f, xMax: 0.85f, yMin: 0.1f, yMax: 0.9f);
                CreateText(ref container, parent: name + "current", name: null, color: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: current, size: 10);
                CreateProtectedButton(ref container, parent: parent, name: name, textColor: GetColor(hex: COLOR_PRIMARY), text: ">", size: 12, xMin: 0.9f, xMax: 0.975f, yMin: 0.1f, yMax: 0.9f, command: command + " next");
            }

            public static void CreateIntPanel(ref CuiElementContainer container, string parent, string name, int current, string command, int min = 0, int max = 1000, bool timeInt = false)
            {
                CreateProtectedButton(ref container, parent: parent, name: name, textColor: current <= min ? GetColor("#FFFFFF", alpha: 0.5f) : GetColor(hex: COLOR_PRIMARY), text: "-", size: 12, xMin: 0.25f, xMax: 0.35f, yMin: 0.1f, yMax: 0.9f, command: current <= min ? "" : $"{command} {current - 1}");
                CreatePanel(ref container, parent: parent, name: name + "current", xMin: 0.4f, xMax: 0.6f, yMin: 0.1f, yMax: 0.9f);
                CreateText(ref container, parent: name + "current", name: null, color: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: timeInt ? ToTimeString(current) : current.ToString(), size: 10);
                CreateProtectedButton(ref container, parent: parent, name: name, textColor: current >= max ? GetColor("#FFFFFF", alpha: 0.5f) : GetColor(hex: COLOR_PRIMARY), text: "+", size: 12, xMin: 0.65f, xMax: 0.75f, yMin: 0.1f, yMax: 0.9f, command: current >= max ? "" : $"{command} {current + 1}");
            }

            public static void CreateFloatPanel(ref CuiElementContainer container, string parent, string name, float current, string command, float min = 0.1f, float max = 100f, float increment = 0.5f)
            {
                CreateProtectedButton(ref container, parent: parent, name: name, textColor: current <= min ? GetColor("#FFFFFF", alpha: 0.5f) : GetColor(hex: COLOR_PRIMARY), text: "-", size: 12, xMin: 0.15f, xMax: 0.3f, yMin: 0.1f, yMax: 0.9f, command: current <= min ? "" : $"{command} {Math.Round(current - increment, 2)}");
                CreatePanel(ref container, parent: parent, name: name + "current", xMin: 0.35f, xMax: 0.65f, yMin: 0.1f, yMax: 0.9f);
                CreateText(ref container, parent: name + "current", name: null, color: GetColor(hex: "#FFFFFF", alpha: 0.5f), text: current.ToString(), size: 10);
                CreateProtectedButton(ref container, parent: parent, name: name, textColor: current >= max ? GetColor("#FFFFFF", alpha: 0.5f) : GetColor(hex: COLOR_PRIMARY), text: "+", size: 12, xMin: 0.7f, xMax: 0.85f, yMin: 0.1f, yMax: 0.9f, command: current >= max ? "" : $"{command} {Math.Round(current + increment, 2)}");
            }

            public static void CreateCheckbox(ref CuiElementContainer container, string parent, string name, string color, string command, bool isOn)
            {
                CreateProtectedButton(ref container, parent: parent, name: name, command: command);

                if (!isOn)
                    return;

                CreateImage(ref container, parent: name, name: null, image: "assets/icons/check.png", color: color, xMin: 0.1f, xMax: 0.9f, yMin: 0.1f, yMax: 0.9f);
            }
        }

        #endregion

        #region IMAGES

        public class Images
        {
            public Dictionary<string, string> data;

            public Images()
            {
                data = new Dictionary<string, string>
                {
                    { "ui_0", "https://i49.servimg.com/u/f49/14/08/21/53/ui_011.png" },
                    { "CFL_canRaid", "https://i49.servimg.com/u/f49/14/08/21/53/canrai11.png" }
                };
                LoadDeployableItem();
                LoadImage();
            }

            private string GetItemImageUrl(ItemDefinition item) => $"https://rustlabs.com/img/items180/{item.shortname}.png";

            public void LoadDeployableItem()
            {
                foreach (ItemDefinition item in GetAllDeployables())
                    data.Add(item.shortname, GetItemImageUrl(item));
            }

            public void LoadImage()
            {
                if (_instance.ImageLibrary == null)
                    return;

                if (!_instance.ImageLibrary.IsLoaded)
                {
                    _instance.timer.Once(1f, () => LoadImage());
                    return;
                }

                _instance.ImageLibrary?.Call("ImportImageList", "CRaidController", data);
            }
        }

        #endregion

        #region API

        public class CodeFlingApi
        {
            public Dictionary<string, string> data;

            public CodeFlingApi()
            {
                data = new Dictionary<string, string>();
                Load();
            }

            public void Load()
            {
                _instance.webrequest.Enqueue("https://codefling.com/capi/category-2/?do=apicall", null, (code, response) =>
                {
                    if (code != 200 || response == null)
                        return;

                    JObject jsonResponse = JObject.Parse(response);
                    JArray fileArray = (JArray)jsonResponse["file"];

                    foreach (JToken fileToken in fileArray)
                    {
                        string fileName = fileToken["file_name"].ToString();

                        if (string.Equals(fileName.ToLower(), _instance.Title.ToLower(), StringComparison.OrdinalIgnoreCase))
                        {
                            JToken fileInfoToken = fileToken;
                            data.Add("file_id", fileInfoToken["file_id"].ToString());
                            data.Add("file_name", fileInfoToken["file_name"].ToString());
                            data.Add("file_image", fileInfoToken["file_image"]["url"].ToString());
                            _instance.ImageLibrary?.Call("AddImage", fileInfoToken["file_image"]["url"].ToString(), fileInfoToken["file_name"].ToString());
                            data.Add("file_version", fileInfoToken["file_version"].ToString());
                            data.Add("file_author", fileInfoToken["file_author"].ToString());
                            data.Add("file_updated", fileInfoToken["file_updated"].ToString());
                            break;
                        }
                    }
                }, _instance);
            }
        }

        public class Feed
        {
            public Dictionary<int, News> data;
            public Dictionary<ulong, List<string>> feedRead;

            public Feed()
            {
                data = new Dictionary<int, News>();
                feedRead = new Dictionary<ulong, List<string>>();
                Load();
                LoadData();
            }

            public class News
            {
                public string ID { get; set; }
                public string Title { get; set; }
                public string Description { get; set; }
                public DateTime Date { get; set; }
                public string Plugin { get; set; }
            }

            private void LoadData() => feedRead = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<string>>>("tfFeedRead");

            private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject("tfFeedRead", feedRead);

            public bool IsRead(ulong player, string newsId)
            {
                if (!feedRead.ContainsKey(player))
                    return false;

                return feedRead[player].Contains(newsId);
            }

            public void MarkRead(ulong player, string newsId)
            {
                if (!feedRead.ContainsKey(player))
                    feedRead[player] = new List<string>();

                if (!feedRead[player].Contains(newsId))
                    feedRead[player].Add(newsId);

                SaveData();
            }

            public void Load()
            {
                _instance.webrequest.Enqueue("https://tf-crazy.alwaysdata.net/feed.php", null, (code, response) =>
                {
                    if (code != 200 || response == null)
                        return;

                    JArray jsonResponse = JArray.Parse(response);

                    foreach (JToken newsToken in jsonResponse)
                    {
                        News news = new News
                        {
                            ID = newsToken["id"].ToString(),
                            Title = newsToken["title"].ToString(),
                            Description = newsToken["description"].ToString().Replace("\r", "\n"),
                            Date = DateTime.Parse(newsToken["date"].ToString()),
                            Plugin = newsToken["plugin"].ToString()
                        };
                        data.Add(Convert.ToInt32(news.ID), news);
                    }
                }, _instance);
            }

            public List<News> GetNewsList(string tab)
            {
                List<News> newsList;

                if (tab == _instance.Title)
                    newsList = new List<News>(data.Values).FindAll(n => n.Plugin == _instance.Title);
                else
                    newsList = new List<News>(data.Values).FindAll(n => n.Plugin != _instance.Title);

                return newsList.OrderByDescending(n => n.Date).ToList();
            }
        }

        #endregion

        #region DEV API

        private bool API_IsRaidTime() => _mainController.currentSchedule.isRaidTime;
        private List<int> API_StartSchedule()
        {
            return new List<int>
            {
                (int)_mainController.currentSchedule.start.day,
                _mainController.currentSchedule.start.hour,
                _mainController.currentSchedule.start.minute
            };
        }
        private List<int> API_EndSchedule()
        {
            return new List<int>
            {
                (int)_mainController.currentSchedule.end.day,
                _mainController.currentSchedule.end.hour,
                _mainController.currentSchedule.end.minute
            };
        }

        #endregion

        #region CATCH FIREBALL

        internal class FireBallManager : FacepunchBehaviour
        {
            private List<FireBallImpact> fireBallImpacts = new List<FireBallImpact>();
            private float maxImpactTime = 40f;

            public void Awake() => InvokeRepeating(Refresh, 0f, 1f);

            private class FireBallImpact
            {
                public ulong entityId;
                public float impactTime;

                public FireBallImpact(ulong id)
                {
                    entityId = id;
                    impactTime = Time.time;
                }
            }

            private void Refresh()
            {
                float currentTime = Time.time;

                for (int i = fireBallImpacts.Count - 1; i >= 0; i--)
                {
                    if (currentTime - fireBallImpacts[i].impactTime > maxImpactTime)
                        fireBallImpacts.RemoveAt(i);
                }
            }

            public void CatchFireBall(ulong entityId)
            {
                if (IsExist(entityId))
                    return;

                fireBallImpacts.Add(new FireBallImpact(entityId));
            }

            public bool IsExist(ulong entityId) => fireBallImpacts.Find(impact => impact.entityId == entityId) != null;

            private void OnDestroy() => CancelInvoke(Refresh);
        }

        #endregion

        #region ALERT MESSAGE

        internal class AlertMessage : FacepunchBehaviour
        {
            private List<Message> message = new List<Message>();
            private BasePlayer player;
            private float lastSentTime = 0f;

            public void Awake()
            {
                player = GetComponent<BasePlayer>();
                InvokeRepeating(Refresh, 0f, 1f);
            }

            private class Message
            {
                public bool send;
                public string key;
                public int style;
                public float time;
                public object[] args;

                public Message(string key, int style, params object[] args)
                {
                    this.key = key;
                    this.style = style;
                    time = Time.time;
                    this.args = args;
                }
            }

            private void Refresh()
            {
                float currentTime = Time.time;

                if (currentTime - lastSentTime >= 10)
                {
                    Message nextMessage = message.FirstOrDefault(m => !m.send);

                    if (nextMessage != null)
                    {
                        SendAlert(nextMessage.key, nextMessage.style, nextMessage.args);
                        nextMessage.send = true;
                        lastSentTime = currentTime;
                    }
                }

                for (int i = message.Count - 1; i >= 0; i--)
                {
                    if (message[i].send && currentTime - message[i].time > 10)
                        message.RemoveAt(i);
                }
            }

            public void AddAlert(string key, int style = 2, params object[] args)
            {
                if (IsExist(key))
                    return;

                message.Add(new Message(key, style, args));
            }

            private void SendAlert(string key, int style, params object[] args)
            {
                bool useBroadcast = _cfg.Message.MessageType == _instance._messageType[0];
                bool showToast = _cfg.Message.MessageType == _instance._messageType[1];
                bool playEffect = _cfg.Message.Effect;

                if (player == null)
                    return;

                string language = _playerSetting.data[player.userID].Language;
                object[] formattedArgs = args.Select(arg => ToDateLangFormat(arg, language)).ToArray();
                string message = Lang(key, player.UserIDString, formattedArgs);

                if (useBroadcast)
                    _instance.Player.Message(player, _cfg.Message.Syntax.Prefix + message, Convert.ToUInt64(_cfg.Message.Syntax.Icon));

                if (showToast)
                    player.SendConsoleCommand("showtoast", style, message);

                if (playEffect)
                    SendEffect(player, style == 1 ? SOUND_ERROR : SOUND_ALERT);
            }

            public bool IsExist(string key) => message.Find(m => m.key == key) != null;
            private void OnDestroy() => CancelInvoke(Refresh);
        }

        #endregion

        #region PLAYER SETTING

        public class PlayerSetting
        {
            public Dictionary<ulong, Setting> data;

            public PlayerSetting()
            {
                data = new Dictionary<ulong, Setting>();
                LoadData();
            }

            public class Setting
            {
                public bool HideUi { get; set; }
                public int Ui { get; set; } = _cfg.Ui.DefaultUi;
                public int Side { get; set; }
                public string Language { get; set; } = "en";
            }

            private void LoadData() => data = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Setting>>($"{_instance.Title}/PlayerSetting");

            public void SaveData() => Interface.Oxide.DataFileSystem.WriteObject($"{_instance.Title}/PlayerSetting", data);
        }

        #endregion

        #region TC CONTOLLER

        internal class TcController
        {
            public List<TC> tc = new List<TC>();

            public class TC
            {
                public ulong id;
                public bool disabled = true;

                public TC(ulong id)
                {
                    this.id = id;
                }
            }

            public TcController()
            {
                LoadData();
            }

            private void LoadData() => tc = Interface.Oxide.DataFileSystem.ReadObject<List<TC>>($"{_instance.Title}/TcBypass");

            public void SaveData() => Interface.Oxide.DataFileSystem.WriteObject($"{_instance.Title}/TcBypass", tc);

            public void ResetData() => tc.Clear();

            public void DisableTcProtection(ulong entity)
            {
                if (FindTc(entity) == null)
                {
                    tc.Add(new TC(entity));
                    return;
                }

                FindTc(entity).disabled = true;
            }
            public void EnableTcProtection(ulong entity)
            {
                if (FindTc(entity) == null)
                    return;

                FindTc(entity).disabled = false;
            }

            public bool TcDisabledProtection(ulong entity)
            {
                if (FindTc(entity) == null)
                    return false;

                return FindTc(entity).disabled;
            }

            public TC FindTc(ulong entity) => tc.Find(tC => tC.id == entity);
        }

        #endregion

        #region PLAYER DATA

        public class PlayerData
        {
            public Dictionary<ulong, Data> data;

            public PlayerData()
            {
                data = new Dictionary<ulong, Data>();
                LoadData();
            }

            public class Data
            {
                public double TimePlayed { get; set; }
            }

            private void LoadData() => data = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Data>>($"{_instance.Title}/PlayerData");

            public void SaveData() => Interface.Oxide.DataFileSystem.WriteObject($"{_instance.Title}/PlayerData", data);

            public void ResetData() => data.Clear();
        }

        #endregion

        #region LOCALIZATION

        protected override void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AdminPanel.Error.1"] = "Only alphabetic strings without spaces or special characters",
                ["AdminPanel.Error.2"] = "Only numeric strings without spaces or special characters",
                ["AdminPanel.Error.3"] = "Please note that your hex code must start with the '#' symbol and contains 6 caracters. Example for black '#000000'",
                ["AdminPanel.Error.4"] = "Only a numeric string of maximum 17 characters (Steam ID)",
                ["AdminPanel.Error.AllowDuringScheduleNoData"] = "Status 'AllowDuringSchedule' is defined, but no time range is saved. The status 'NotAllowedAllDay' will be enforced for [{0}]",
                ["AdminPanel.Schedule.Status.AllowedAllDay"] = "<color=green>Allowed</color> all day",                
                ["AdminPanel.Schedule.Status.AllowDuringSchedule"] = "Scheduled raids",
                ["AdminPanel.Schedule.Status.NotAllowedAllDay"] = "<color=red>Not Allowed</color> all day",
                ["AdminPanel.Schedule.TimeRange.End"] = "End",
                ["AdminPanel.Schedule.TimeRange.OnEdit.End"] = "End time of the raids",
                ["AdminPanel.Schedule.TimeRange.OnEdit.EndOfDay"] = "Raids until the end of the day ?",
                ["AdminPanel.Schedule.TimeRange.OnEdit.Hour"] = "Hour",
                ["AdminPanel.Schedule.TimeRange.OnEdit.Minute"] = "Minute",
                ["AdminPanel.Schedule.TimeRange.OnEdit.Start"] = "Start time of the raids",
                ["AdminPanel.Schedule.TimeRange.Start"] = "Start",
                ["AdminPanel.Setting.Bypass.Admin"] = "Authorizes the admin to override raid restrictions",
                ["AdminPanel.Setting.Bypass.BuildingPrivilege"] = "Authorizes to override raid restrictions if have building privileges",
                ["AdminPanel.Setting.Bypass.Item.NoData"] = "There are currently no items submitted to an exception",
                ["AdminPanel.Setting.Bypass.Item.Search"] = "Search:",
                ["AdminPanel.Setting.Bypass.Mate"] = "Authorizes team members to destroy items/buildings of another member, even if there are restrictions in place",
                ["AdminPanel.Setting.Bypass.NoTc"] = "Permits raiding bases do not have a tool cupboard, even if there are restrictions in place",
                ["AdminPanel.Setting.Bypass.Owner"] = "Authorizes entity owner to override raid restrictions",
                ["AdminPanel.Setting.Bypass.TcBypass"] = "Allows to disable the protection of a base (Tool Cupboard), allowing to raided them during unauthorized hours",
                ["AdminPanel.Setting.Bypass.TcBypass.AllPlayer"] = "For all players",
                ["AdminPanel.Setting.Bypass.TcBypass.Disabled"] = "Disabled",
                ["AdminPanel.Setting.Bypass.TcBypass.LeaderOnly"] = "Team leader only",
                ["AdminPanel.Setting.Bypass.TcDecay"] = "Permits raiding bases that are protected by a tool cupboard while in a state of decay, even if there are restrictions in place",
                ["AdminPanel.Setting.Bypass.TcNoProtectItem"] = "Destroyable item <color=red>not protected</color> by a tool cupboard, in restrictions",
                ["AdminPanel.Setting.Bypass.TcProtectItem"] = "Destroyable item <color=green>protected</color> by a tool cupboard, in restrictions",
                ["AdminPanel.Setting.Bypass.Twig"] = "Allows the destruction of wood (Twig) buildings during raids, even if there are restrictions in place",
                ["AdminPanel.Setting.Global.BlockDamage"] = "Block damage outside of raid hours",
                ["AdminPanel.Setting.Global.CmdAllTeam"] = "Apply this command to all players of the team?",
                ["AdminPanel.Setting.Global.CmdExecute"] = "Console command to be sent when a player violates the raid timings (when entity destroy). Use {player}. Example: ban {player} 'Raiding out of schedules'",
                ["AdminPanel.Setting.Global.Command"] = "Chat command to open player's configuration interface",
                ["AdminPanel.Setting.Global.MaxRaidPerPlayer"] = "Maximum number of raids allowed per player per day. (0 for disable)",
                ["AdminPanel.Setting.Global.MaxRaidPerTeam"] = "Keep the raid limit counter in sync with team members",
                ["AdminPanel.Setting.Global.RaidDelay"] = "The minimum amount of time (minutes) that a player must wait before being able to initiate a raid after joining the server. (0 for disable)",
                ["AdminPanel.Setting.Global.RaidProtectionDuration"] = "The minimum amount of time (minutes) that a player or team is protected from being raided after joining the server. (0 for disable)",
                ["AdminPanel.Setting.Global.Timezone"] = "The UTC timezone of your server",
                ["AdminPanel.Setting.Message.AllowedMessage"] = "Send a message when raids are allowed",
                ["AdminPanel.Setting.Message.Effect"] = "Enable sound notification for new messages",
                ["AdminPanel.Setting.Message.FailMessage"] = "Send a message to the player when they attempt to damage structures outside the raid period",
                ["AdminPanel.Setting.Message.FinishedMessage"] = "Send a message when raids are over",
                ["AdminPanel.Setting.Message.MessageType"] = "The type of message to alert your players",
                ["AdminPanel.Setting.Message.MinuteToEndMessage"] = "Specify the delay (in minutes) between each message that alerts the ending of raids. (0 for disable)",
                ["AdminPanel.Setting.Message.MinuteToStartMessage"] = "Specify the delay (in minutes) between each message that alerts the beginning of raids. (0 for disable)",
                ["AdminPanel.Setting.Message.Syntax.Icon"] = "Icon before messages (Steam ID, 0 for default Rust logo)",
                ["AdminPanel.Setting.Message.Syntax.Prefix"] = "Prefix before messages (you can use tags like <color>, <size>, etc...)",
                ["AdminPanel.Setting.Ui.ActiveUi"] = "Enable in-game UI for players",
                ["AdminPanel.Setting.Ui.ActiveUi.Activated"] = "Activated",
                ["AdminPanel.Setting.Ui.ActiveUi.Disabled"] = "Disabled",
                ["AdminPanel.Setting.Ui.ActiveUi.Disabled.Text"] = "No in-game interface is enabled for players",
                ["AdminPanel.Setting.Ui.ActiveUi.CustomStatusFramework"] = "CustomStatusFramework",
                ["AdminPanel.Setting.Ui.ActiveUi.CustomStatusFramework.Found"] = "CustomStatusFramework plugin is enabled",
                ["AdminPanel.Setting.Ui.ActiveUi.CustomStatusFramework.NotFound"] = "CustomStatusFramework plugin is not installed on the server. The status 'Disabled' will be forced",
                ["AdminPanel.Setting.Ui.AllowPlayerChange"] = "Allow player to change ui",
                ["AdminPanel.Setting.Ui.AllowPlayerHide"] = "Allow player to hide UI",
                ["AdminPanel.Setting.Ui.Background"] = "Color of the background and its opacity (1 corresponds to totally opaque)",
                ["AdminPanel.Setting.Ui.DefaultUi"] = "Choose the default UI",
                ["AdminPanel.Setting.Ui.HideIsTime"] = "Hide UI if is raid time",
                ["AdminPanel.Setting.Ui.HideNotTime"] = "Hide UI if is not raid time",
                ["AdminPanel.Setting.Ui.Rate"] = "Refresh rate of the display",
                ["AdminPanel.Setting.Ui.Select.0"] = "Default v4",
                ["AdminPanel.Setting.Ui.Select.Title"] = "Choose the default interface",
                ["AdminPanel.Setting.Ui.Side.0"] = "Left",
                ["AdminPanel.Setting.Ui.Side.1"] = "Right",
                ["AdminPanel.Setting.Ui.Side"] = "Positioning of the interface",
                ["AdminPanel.Setting.Ui.Ymax"] = "Positioning from the top (1 corresponds to the top of the screen)",
                ["AdminPanel.Tab.Bypass"] = "Exception",
                ["AdminPanel.Tab.Global"] = "General",
                ["AdminPanel.Tab.Main.Feed.NoData"] = "There is currently no news on the feed",
                ["AdminPanel.Tab.Main.Feed.Tab.Others"] = "OTHERS",
                ["AdminPanel.Tab.Main.Feed.Title"] = "LATEST NEWS FROM THE AUTHOR",
                ["AdminPanel.Tab.Main.Info.Author"] = "Author",
                ["AdminPanel.Tab.Main.Info.CodeflingVersion"] = "CodeFling version",
                ["AdminPanel.Tab.Main.Info.CurrentVersion"] = "Current version",
                ["AdminPanel.Tab.Main.Info.LastUpdated"] = "Last updated",
                ["AdminPanel.Tab.Main.Info.Title"] = "PLUGIN INFORMATION",
                ["AdminPanel.Tab.Main.FastSetting.Title"] ="FAST SETTING",
                ["AdminPanel.Tab.Main.FastSetting.Toggle"] = "Enable or disable the plugin",
                ["AdminPanel.Tab.Message"] = "Message",
                ["AdminPanel.Tab.Schedule"] = "Scheduling",
                ["AdminPanel.Tab.Ui"] = "Interface",
                ["Alert.AllowedAllDayMessage"] = "Raids are allowed all day. They will end {0} at {1}",
                ["Alert.AllowedMessage"] = "The raids can begin !",
                ["Alert.FinishedMessage"] = "The raids have ended !",
                ["Alert.MinuteToEndMessage"] = "Raids will end {0} at {1}",
                ["Alert.MinuteToEndMessageSameDay"] = "The raids will end in {0}",
                ["Alert.MinuteToStartMessage"] = "The next raid slot is {0} at {1}",
                ["Alert.MinuteToStartMessageSameDay"] = "The raids will start in {0}",
                ["Alert.NotAllowedAllDayMessage"] = "Raids are not allowed all the day. They will start {0} at {1}",
                ["Alert.NotRaidTime"] = "You cannot raid outside of authorized times!",
                ["Alert.RaidDelay"] = "You do not have enough time played on the server to be able to raid",
                ["Alert.RaidProtectionDuration"] = "You cannot raid the player, they have not played enough time on the server .",
                ["Btn.Cancel"] = "CANCEL",
                ["Btn.Confirm"] = "CONFIRM",
                ["Btn.Close"] = "CLOSE",
                ["Btn.Select"] = "SELECT",
                ["Btn.Submit"] = "SUBMIT",
                ["PlayerPanel.Btn.Admin"] = "ADMIN PANEL",
                ["PlayerPanel.HideUi"] = "Is the player interface bothering you? You can disable it. However, you will still be notified by notifications",
                ["PlayerPanel.HideUi.Input"] = "Disable the in-game interface?",
                ["PlayerPanel.Language"] = "This option allows you to have the days (in notifications or UI) adapted to your language",
                ["PlayerPanel.Language.Input"] = "Language",
                ["PlayerPanel.Ui"] = "If you'd like more display options, send me your creation (PSD).\nOn Discord at <color=#bf8e51>TF Crazy#5791</color>. I might add it, with your pseudo",
                ["PlayerPanel.Ui.Input"] = "In-game interface",
                ["Ui.RaidAllowed"] = "• Raid allowed",
                ["Ui.RaidNotAllowed"] = "• Raid not allowed",
                ["Ui.CustomStatusFramework.Allowed"] = "Allowed",
                ["Ui.CustomStatusFramework.NotAllowed"] = "Not allowed",
                ["Ui.TcBypass.Title"] = "Allow this base to be 'raidable' outside the authorized periods",
                ["Ui.TcBypass.Disable"] = "Disable protection for this base!",
                ["Ui.TcBypass.Enable"] = "Enable protection for this base!",
                ["Ui.TcBypass.Modal"] = "Are you sure? You will be able to reactivate the protection only at the next authorized raid schedules",
                ["Ui.TcBypass.NotUpdateNow"] = "Can be activated during raid hours only",
            }, this);

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AdminPanel.Error.1"] = "Seules les chaînes alphabétiques sans espaces ni caractères spéciaux",
                ["AdminPanel.Error.2"] = "Seules les chaînes numériques sans espaces ni caractères spéciaux",
                ["AdminPanel.Error.3"] = "Veuillez noter que votre code hexadécimal doit commencer par le symbole '#' et contenir 6 caractères. Exemple pour le noir '#000000'",
                ["AdminPanel.Error.4"] = "Seulement une chaîne numérique de 17 caractères maximum (ID Steam)",
                ["AdminPanel.Error.AllowDuringScheduleNoData"] = "Statut 'AllowDuringSchedule' est définie. mais aucune plage horaire n'est sauvegardée. Le statut 'NotAllowedAllDay' sera appliqué pour [{0}]",
                ["AdminPanel.Schedule.Status.AllowedAllDay"] = "<color=green>Autorisés</color> toute la journée",                
                ["AdminPanel.Schedule.Status.AllowDuringSchedule"] = "Soumis à des horaires",
                ["AdminPanel.Schedule.Status.NotAllowedAllDay"] = "<color=red>Non autorisés</color> toute la journée",
                ["AdminPanel.Schedule.TimeRange.End"] = "Fin",
                ["AdminPanel.Schedule.TimeRange.OnEdit.End"] = "Heure de fin des raids",
                ["AdminPanel.Schedule.TimeRange.OnEdit.EndOfDay"] = "Raids jusqu'à la fin de la journée ?",
                ["AdminPanel.Schedule.TimeRange.OnEdit.Hour"] = "Heure",
                ["AdminPanel.Schedule.TimeRange.OnEdit.Minute"] = "Minute",
                ["AdminPanel.Schedule.TimeRange.OnEdit.Start"] = "Heure de début des raids",
                ["AdminPanel.Schedule.TimeRange.Start"] = "Début",
                ["AdminPanel.Setting.Bypass.Admin"] = "Autorise l'administrateur à contourner les restrictions de raid",
                ["AdminPanel.Setting.Bypass.BuildingPrivilege"] = "Permet de contiurner les restrictions de raid si le joueur à les buildings privileges",
                ["AdminPanel.Setting.Bypass.Item.NoData"] = "Il n'y a actuellement aucun élément soumis à une exception",
                ["AdminPanel.Setting.Bypass.Item.Search"] = "Rechercher:",
                ["AdminPanel.Setting.Bypass.Mate"] = "Autorise les membres de l'équipe à détruire les objets/bâtiments d'un autre membre, même s'il y a des restrictions en place",
                ["AdminPanel.Setting.Bypass.NoTc"] = "Permet de raid des bases qui n'ont pas d'armoire à outils, même s'il y a des restrictions en place",
                ["AdminPanel.Setting.Bypass.Owner"] = "Autorise le propriétaire de l'entité à contourner les restrictions de raid",
                ["AdminPanel.Setting.Bypass.TcBypass"] = "Permet de désactiver la protection d'une base (TC), permettant de la raids durant des horaires non autorisés",
                ["AdminPanel.Setting.Bypass.TcBypass.AllPlayer"] = "Pour tous les joueurs",
                ["AdminPanel.Setting.Bypass.TcBypass.Disabled"] = "Désactivé",
                ["AdminPanel.Setting.Bypass.TcBypass.LeaderOnly"] = "Chef d'équipe uniquement",
                ["AdminPanel.Setting.Bypass.TcDecay"] = "Permet de raid des bases protégées par une armoire à outils pendant qu'elles sont en train de se dégrader, même s'il y a des restrictions en place",
                ["AdminPanel.Setting.Bypass.TcNoProtectItem"] = "Objet destructible <color=red>non protégé</color> par une armoire à outils, pendant les restrictions",
                ["AdminPanel.Setting.Bypass.TcProtectItem"] = "Objet destructible <color=green>protégé</color> par une armoire à outils, pendant les restrictions",
                ["AdminPanel.Setting.Bypass.Twig"] = "Permet la destruction des bâtiments en bois (Twig) pendant les raids, même s'il y a des restrictions en place",
                ["AdminPanel.Setting.Global.BlockDamage"] = "Bloquer les dégâts en dehors des heures de raid",
                ["AdminPanel.Setting.Global.CmdAllTeam"] = "Appliquer cette commande à tous les joueurs de l'équipe?",
                ["AdminPanel.Setting.Global.CmdExecute"] = "Commande de la console à envoyer lorsqu'un joueur viole les horaires de raid (lors de la destruction d'une entité). Utilisez {player}. Exemple: ban {player} 'Raiding out of schedules'",
                ["AdminPanel.Setting.Global.Command"] = "Commande de chat pour ouvrir l'interface de configuration du joueur",
                ["AdminPanel.Setting.Global.MaxRaidPerPlayer"] = "Nombre maximum de raids autorisés par joueur et par jour. (0 pour désactiver)",
                ["AdminPanel.Setting.Global.MaxRaidPerTeam"] = "Maintenir le compteur de limite de raid synchronisé avec les membres de l'équipe",
                ["AdminPanel.Setting.Global.RaidDelay"] = "Temps minimum (en minutes) qu'un joueur doit attendre avant de pouvoir lancer un raid après avoir rejoint le serveur. (0 pour désactiver)",
                ["AdminPanel.Setting.Global.RaidProtectionDuration"] = "Temps minimum (en minutes) pendant lequel un joueur ou une équipe est protégé contre les raids après avoir rejoint le serveur. (0 pour désactiver)",
                ["AdminPanel.Setting.Global.Timezone"] = "Le fuseau horaire UTC de votre serveur",
                ["AdminPanel.Setting.Message.AllowedMessage"] = "Envoyer un message lorsque les raids sont autorisés",
                ["AdminPanel.Setting.Message.Effect"] = "Activer la notification sonore pour les nouveaux messages",
                ["AdminPanel.Setting.Message.FailMessage"] = "Envoyer un message au joueur lorsqu'il tente d'infliger des dégâts aux structures en dehors de la période de raid",
                ["AdminPanel.Setting.Message.FinishedMessage"] = "Envoyer un message lorsque les raids sont terminés",
                ["AdminPanel.Setting.Message.MessageType"] = "Le type de message pour alerter vos joueurs",
                ["AdminPanel.Setting.Message.MinuteToEndMessage"] = "Spécifier le délai (en minutes) entre chaque message qui annonce la fin des raids. (0 pour désactiver)",
                ["AdminPanel.Setting.Message.MinuteToStartMessage"] = "Spécifier le délai (en minutes) entre chaque message qui annonce le début des raids. (0 pour désactiver)",
                ["AdminPanel.Setting.Message.Syntax.Icon"] = "Icône avant les messages (ID Steam, 0 pour le logo Rust par défaut)",
                ["AdminPanel.Setting.Message.Syntax.Prefix"] = "Préfixe avant les messages (vous pouvez utiliser des balises comme <color>, <size>, etc...)",
                ["AdminPanel.Setting.Ui.ActiveUi"] = "Activer l'interface en jeu pour les joueurs",
                ["AdminPanel.Setting.Ui.ActiveUi.Activated"] = "Activé",
                ["AdminPanel.Setting.Ui.ActiveUi.Disabled"] = "Désactivé",
                ["AdminPanel.Setting.Ui.ActiveUi.Disabled.Text"] = "Aucune interface en jeu n'est activée pour les joueurs",
                ["AdminPanel.Setting.Ui.ActiveUi.CustomStatusFramework"] = "CustomStatusFramework",
                ["AdminPanel.Setting.Ui.ActiveUi.CustomStatusFramework.Found"] = "Le plugin CustomStatusFramework est activé",
                ["AdminPanel.Setting.Ui.ActiveUi.CustomStatusFramework.NotFound"] = "Le plugin CustomStatusFramework n'est pas installé sur le serveur. Le statut 'Désactivé' sera forcé",
                ["AdminPanel.Setting.Ui.AllowPlayerChange"] = "Permettre au joueur de changer l'interface",
                ["AdminPanel.Setting.Ui.AllowPlayerHide"] = "Permettre au joueur de masquer l'interface",
                ["AdminPanel.Setting.Ui.Background"] = "Couleur du fond et son opacité (1 correspond à totalement opaque)",
                ["AdminPanel.Setting.Ui.DefaultUi"] = "Choisir l'interface par défaut",
                ["AdminPanel.Setting.Ui.HideIsTime"] = "Masquer l'interface si c'est l'heure des raids",
                ["AdminPanel.Setting.Ui.HideNotTime"] = "Masquer l'interface si ce n'est pas l'heure de raids",
                ["AdminPanel.Setting.Ui.Rate"] = "Taux de rafraîchissement de l'affichage",
                ["AdminPanel.Setting.Ui.Select.0"] = "Défaut v4",
                ["AdminPanel.Setting.Ui.Select.Title"] = "Choisissez l'interface par défaut",
                ["AdminPanel.Setting.Ui.Side.0"] = "Gauche",
                ["AdminPanel.Setting.Ui.Side.1"] = "Droite",
                ["AdminPanel.Setting.Ui.Side"] = "Positionnement de l'interface",
                ["AdminPanel.Setting.Ui.Ymax"] = "Positionnement à partir du haut (1 correspond au haut de l'écran)",
                ["AdminPanel.Tab.Bypass"] = "Exception",
                ["AdminPanel.Tab.Global"] = "Général",
                ["AdminPanel.Tab.Main.Feed.NoData"] = "Il n'y a actuellement aucune actualité dans le fil",
                ["AdminPanel.Tab.Main.Feed.Tab.Others"] = "AUTRES",
                ["AdminPanel.Tab.Main.Feed.Title"] = "DERNIÈRES NOUVELLES DE L'AUTEUR",
                ["AdminPanel.Tab.Main.Info.Author"] = "Auteur",
                ["AdminPanel.Tab.Main.Info.CodeflingVersion"] = "Version CodeFling",
                ["AdminPanel.Tab.Main.Info.CurrentVersion"] = "Version actuelle",
                ["AdminPanel.Tab.Main.Info.LastUpdated"] = "Dernière mise à jour",
                ["AdminPanel.Tab.Main.Info.Title"] = "INFORMATIONS SUR LE PLUGIN",
                ["AdminPanel.Tab.Main.FastSetting.Title"] = "PARAMÈTRES RAPIDES",
                ["AdminPanel.Tab.Main.FastSetting.Toggle"] = "Activer ou désactiver le plugin",
                ["AdminPanel.Tab.Message"] = "Message",
                ["AdminPanel.Tab.Schedule"] = "Planification",
                ["AdminPanel.Tab.Ui"] = "Interface",
                ["Alert.AllowedAllDayMessage"] = "Les raids sont autorisés toute la journée. Ils se termineront {0} à {1}",
                ["Alert.AllowedMessage"] = "Les raids peuvent commencer !",
                ["Alert.FinishedMessage"] = "Les raids sont terminés !",
                ["Alert.MinuteToEndMessage"] = "Les raids se termineront {0} à {1}",
                ["Alert.MinuteToEndMessageSameDay"] = "Les raids se termineront dans {0}",
                ["Alert.MinuteToStartMessage"] = "La prochaine plage de raid sera {0} à {1}",
                ["Alert.MinuteToStartMessageSameDay"] = "Les raids commenceront dans {0}",
                ["Alert.NotAllowedAllDayMessage"] = "Les raids ne sont pas autorisés aujourd'hui. Ils démarreront {0} à {1}",
                ["Alert.NotRaidTime"] = "Vous ne pouvez pas raid en dehors des horaires autorisées !",
                ["Alert.RaidDelay"] = "Vous n'avez pas assez de temps de jeu sur le serveur pour pouvoir raid",
                ["Alert.RaidProtectionDuration"] = "Vous ne pouvez pas raid le joueur, il n'a pas assez de temps de jeu sur le serveur",
                ["Btn.Cancel"] = "ANNULER",
                ["Btn.Confirm"] = "CONFIRMER",
                ["Btn.Close"] = "FERMER",
                ["Btn.Select"] = "SÉLECTIONNER",
                ["Btn.Submit"] = "SOUMETTRE",
                ["PlayerPanel.Btn.Admin"] = "ADMINISTRATION",
                ["PlayerPanel.HideUi"] = "L'interface du joueur vous dérange ? Vous pouvez la désactiver. Cependant, vous serez toujours informé par les notifications",
                ["PlayerPanel.HideUi.Input"] = "Désactiver l'interface en jeu ?",
                ["PlayerPanel.Language"] = "Cette option permet d'avoir les jours (dans les notifications ou l'interface) adaptés à votre langue",
                ["PlayerPanel.Language.Input"] = "Langue",
                ["PlayerPanel.Ui"] = "Si vous souhaitez plus d'options d'affichage, envoyez-moi votre création (PSD).\nSur Discord à <color=#bf8e51>TF Crazy#5791</color>. Je pourrais l'ajouter, avec votre pseudo",
                ["PlayerPanel.Ui.Input"] = "Interface en jeu",
                ["Ui.RaidAllowed"] = "• Raid autorisé",
                ["Ui.RaidNotAllowed"] = "• Raid non autorisé",
                ["Ui.CustomStatusFramework.Allowed"] = "Autorisé",
                ["Ui.CustomStatusFramework.NotAllowed"] = "Non autorisé",
                ["Ui.TcBypass.Title"] = "Permettre à cette base d'être 'raidable' en dehors des périodes autorisées",
                ["Ui.TcBypass.Disable"] = "Désactiver la protection de cette base !",
                ["Ui.TcBypass.Enable"] = "Activer la protection de cette base !",
                ["Ui.TcBypass.Modal"] = "Êtes-vous sûr ? Vous ne pourrez réactiver la protection qu'à l'occasion des prochaines plages horaires autorisées",
                ["Ui.TcBypass.NotUpdateNow"] = "Peut être activé pendant les heures de raid uniquement",
            }, this, "fr");
        }

        #endregion

    }
}