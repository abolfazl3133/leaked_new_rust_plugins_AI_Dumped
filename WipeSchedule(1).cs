// #define SCHEDULE_DEBUG

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Plugins.WipeScheduleEx;
using UnityEngine;
using UnityEngine.UI;

# if CARBON
	using Carbon.Base;
	using Carbon.Modules;
# endif

namespace Oxide.Plugins
{
    [Info("Wipe Schedule", "Mevent", "2.0.8")]
    public class WipeSchedule : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin ImageLibrary = null, ServerPanel = null;

        public const bool LangRu = true;

        private static WipeSchedule Instance;

        private const string
            Layer = "UI.WipeSchedule",
            Command = "command.wipe.schedule",
            PermissionUse = "wipeschedule.admin";

        private string[] MonthNames =
        {
            "January", "February", "March", "April", "May", "June", "July", "August", "September", "October",
            "November", "December"
        };

        private static DateTime currentDateTime
        {
            get
            {
#if SCHEDULE_DEBUG
					return DateTime.UtcNow.ToLocalTime();
#else
                return DateTime.UtcNow.AddHours(Instance?.config?.TimeZone ?? 0);
#endif
            }
        }

        #endregion

        #region Configuration

        private Configuration config = Configuration.GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
#if SCHEDULE_DEBUG
						LoadDefaultConfig();
#else
                config = Config.ReadObject<Configuration>();
#endif

                if (config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        protected override void LoadDefaultConfig()
        {
            config = Configuration.GetDefaultConfig();
        }

        public static Dictionary<string, LangCustom> GetDefaultCustomLang()
        {
            return new Dictionary<string, LangCustom>
            {
                ["Custom Event"] = new("Событие сервера", "Custom Event"),
                ["Custom Event Description"] = new("Описание события сервера", "Custom Event Description")
            };
        }

        public class Configuration
        {
            [JsonProperty(LangRu ? "Команды для открытия календаря" : "Commands to open the calendar",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] Commands = {"wipe", "wipedata"};

            [JsonProperty(LangRu ? "Временная зона" : "Time Zone")]
            public int TimeZone = 2;

            [JsonProperty(LangRu ? "Свои ключи в переводчик" : "Your keys in the translator")]
            public Dictionary<string, LangCustom> CustomLang;

            [JsonProperty(LangRu
                ? "Для какого шаблона серверного меню использовать настройку ГУИ? (V1, V2)"
                : "Which server menu template should I use the GUI setting for? (V1, V2)")]
            public PatternServerMenu Pattern;

            public Dictionary<string, LangCustom> GetCustomLang()
            {
                if (CustomLang == null) return GetDefaultCustomLang();
                return CustomLang;
            }

            public static Configuration GetDefaultConfig()
            {
                return new Configuration
                {
                    TimeZone = 2,
                    CustomLang = GetDefaultCustomLang(),
                    Pattern = PatternServerMenu.V1
                };
            }
        }

        public struct LangCustom
        {
            public string ru;
            public string en;

            public LangCustom(string ru, string en)
            {
                this.ru = ru;
                this.en = en;
            }
        }

        #endregion

        #region Localization

        private const string
            Calendar = "Calendar",
            SetupScheduleTitle = "SetupScheduleTitle",
            SetupEventTitle = "SetupEventTitle",
            NotifySelection = "NotifySelection",
            ADD_EVENT_BUTTON = "ADD_EVENT_BUTTON",
            DELETE_EVENT_BUTTON = "DELETE_EVENT_BUTTON",
            SAVE_EVENT_BUTTON = "SAVE_EVENT_BUTTON",
            SwitchEventType = "SwitchEventType",
            TitleInputName = "TitleInputName",
            TitleInputDescription = "TitleInputDescription",
            AddCustomColorEvent = "AddCustomColorEvent",
            EnableForChangeDefaultColor = "EnableForChangeDefaultColor",
            AddCustomIconEvent = "AddCustomIconEvent",
            EnableForChangeDefaultIcon = "EnableForChangeDefaultIcon",
            NotifyNotEditTypeSelection = "NotifyNotEditTypeSelection",
            SetupSchedule = "SetupSchedule",
            ScheduleInfo = "ScheduleInfo",
            TitleEditDateTime = "TitleEditDateTime",
            TitleSelectedMonths = "SelectedMonths",
            TitleEditTime = "TitleEditTime",
            TitleEditDate = "TitleEditDate",
            TitleSelectedDays = "TitleSelectedDays",
            TitleSelectedDaysOfWeek = "TitleSelectedDaysOfWeek",
            TitleSelectedNumWeekOfMonth = "TitleSelectedNumWeekOfMonth",
            TitleResult = "TitleResult",
            Result_month = "Result_month",
            Result_not_month = "Result_not_month",
            Result_days = "Result_days",
            Result_not_days = "Result_not_days",
            Result_weeks = "Result_weeks",
            Result_not_weeks = "Result_not_weeks",
            Result_num_weeks = "Result_num_weeks",
            Result_not_num_weeks = "Result_not_num_weeks",
            Result_repeat = "Result_repeat",
            Result_use = "Result_use",
            Result_disable = "Result_disable",
            Result_num1 = "Result_num1",
            Result_num2 = "Result_num2",
            Result_num3 = "Result_num3",
            Result_num4 = "Result_num4",
            Result_num5 = "Result_num5";

        protected override void LoadDefaultMessages()
        {
            var ru = new Dictionary<string, string>
            {
                [Calendar] = "КАЛЕНДАРЬ",
                [SetupScheduleTitle] = "НАСТРОЙКА РАСПИСАНИЯ СОБЫТИЙ",
                [SetupEventTitle] = "НАСТРОЙКА СОБЫТИЯ",
                [NotifySelection] = "Выберите событие для настройки или добавьте новое.",
                [ADD_EVENT_BUTTON] = "Добавить событие",
                [DELETE_EVENT_BUTTON] = "Удалить",
                [SAVE_EVENT_BUTTON] = "Сохранить",
                [SwitchEventType] = "Выберите тип события",
                [TitleInputName] = "Введите название события",
                [TitleInputDescription] = "Введите описание события",
                [AddCustomColorEvent] = "Введите цвет события в формате HEX (1-6 символ-цвет 7,8-прозрачность)",
                [EnableForChangeDefaultColor] =
                    "Включите, что бы при сохранении установить дефолтный цвет для данного типа событий",
                [AddCustomIconEvent] = "Введите ссылку на свое изображение иконки события",
                [EnableForChangeDefaultIcon] =
                    "Включите, что бы при сохранении установить дефолтную иконку для данного типа событий",
                [NotifyNotEditTypeSelection] = "Нельзя редактировать этот тип события.",
                [SetupSchedule] = "ИЗМЕНИТЬ ИВЕНТЫ",
                [ScheduleInfo] =
                    "ДЛЯ ПОЛУЧЕНИЯ ДОП. ИНФОРМАЦИИ ВВЕДИТЕ КОМАНДУ <b><color=#64A1CF>/info</color></b> В ЧАТЕ",
                [DayOfWeek.Sunday.ToString()] = "ВОСКРЕСЕНЬЕ",
                [DayOfWeek.Monday.ToString()] = "ПОНЕДЕЛЬНИК",
                [DayOfWeek.Tuesday.ToString()] = "ВТОРНИК",
                [DayOfWeek.Wednesday.ToString()] = "СРЕДА",
                [DayOfWeek.Thursday.ToString()] = "ЧЕТВЕРГ",
                [DayOfWeek.Friday.ToString()] = "ПЯТНИЦА",
                [DayOfWeek.Saturday.ToString()] = "СУББОТА",
                [TypeEvent.GlobalWipe.ToString()] = "Глобальный вайп",
                [TypeEvent.WipeMap.ToString()] = "Вайп карты",
                [TypeEvent.WipeBlock.ToString()] = "Вайп блок",
                [TypeEvent.EventManager.ToString()] = "Глобальное событие",
                [TypeEvent.CustomEvent.ToString()] = "Событие",
                [TitleEditDateTime] = "Тип настройки даты",
                [TypeSchedule.DateTime.ToString()] = "Один раз",
                [TypeSchedule.CustomDate.ToString()] = "Повторять",
                [TitleEditTime] = "Время(часы/минуты)",
                [TitleEditDate] = "Дата(день/месяц/год)",
                [TitleSelectedMonths] = "Укажите месяца для повтора",
                [ModeRepeat.None.ToString()] = "Не повторяется",
                [ModeRepeat.Days.ToString()] = "Дни",
                [ModeRepeat.Weeks.ToString()] = "Недели",
                [ModeRepeat.Months.ToString()] = "Месяцы",
                [ModeRepeat.Years.ToString()] = "Годы",
                [nameof(ModeRepeat)] = "Выберите режим повтора",
                [TitleSelectedDays] = "Дни месяца",
                [TitleSelectedDaysOfWeek] = "Дни недели",
                [TitleSelectedNumWeekOfMonth] = "Укажите интервалы дней недели",
                [TitleResult] = "Результат настройки даты события",
                [Result_month] = "каждый указанный месяц, ",
                [Result_not_month] = "Не выбраны месяца.",
                [Result_days] = "в выбранные числа месяца, ",
                [Result_not_days] = "Не выбраны числа месяца.",
                [Result_weeks] = "в выбранные дни недели, ",
                [Result_not_weeks] = "Не выбраны дни недели.",
                [Result_num_weeks] = "каждую {0} указанную неделю месяца, ",
                [Result_not_num_weeks] = "Не выбран интервал дней недели.",
                [Result_repeat] = "Событие повторяется {0} в {1}",
                [Result_use] = "Событие состоится {0} в {1}",
                [Result_disable] = "Событие отключено",
                [Result_num1] = "первую",
                [Result_num2] = "вторую",
                [Result_num3] = "третью",
                [Result_num4] = "четвертую",
                [Result_num5] = "пятую",
                [MonthNames[0]] = "Январь",
                [MonthNames[1]] = "Февраль",
                [MonthNames[2]] = "Март",
                [MonthNames[3]] = "Апрель",
                [MonthNames[4]] = "Май",
                [MonthNames[5]] = "Июнь",
                [MonthNames[6]] = "Июль",
                [MonthNames[7]] = "Август",
                [MonthNames[8]] = "Сентябрь",
                [MonthNames[9]] = "Октябрь",
                [MonthNames[10]] = "Ноябрь",
                [MonthNames[11]] = "Декабрь"
            };

            var en = new Dictionary<string, string>
            {
                [Calendar] = "CALENDAR",
                [SetupScheduleTitle] = "EVENT SCHEDULE SETUP",
                [SetupEventTitle] = "EVENT SETUP",
                [NotifySelection] = "Select an event to configure or add a new one.",
                [ADD_EVENT_BUTTON] = "Add Event",
                [DELETE_EVENT_BUTTON] = "Delete",
                [SAVE_EVENT_BUTTON] = "Save",
                [SwitchEventType] = "Select event type",
                [TitleInputName] = "Enter event name",
                [TitleInputDescription] = "Enter event description",
                [AddCustomColorEvent] = "Enter event color in HEX format (1-6 color symbols, 7,8 - transparency)",
                [EnableForChangeDefaultColor] = "Enable to set the default color for this type of events upon saving",
                [AddCustomIconEvent] = "Enter the link to your event icon image",
                [EnableForChangeDefaultIcon] = "Enable to set the default icon for this type of events upon saving",
                [NotifyNotEditTypeSelection] = "This event type cannot be edited.",
                [SetupSchedule] = "CHANGE EVENTS",
                [ScheduleInfo] = "FOR MORE INFORMATION, TYPE <b><color=#64A1CF>/info</color></b> IN CHAT",
                [DayOfWeek.Sunday.ToString()] = "SUNDAY",
                [DayOfWeek.Monday.ToString()] = "MONDAY",
                [DayOfWeek.Tuesday.ToString()] = "TUESDAY",
                [DayOfWeek.Wednesday.ToString()] = "WEDNESDAY",
                [DayOfWeek.Thursday.ToString()] = "THURSDAY",
                [DayOfWeek.Friday.ToString()] = "FRIDAY",
                [DayOfWeek.Saturday.ToString()] = "SATURDAY",
                [TypeEvent.GlobalWipe.ToString()] = "Global Wipe",
                [TypeEvent.WipeMap.ToString()] = "Map Wipe",
                [TypeEvent.WipeBlock.ToString()] = "Block Wipe",
                [TypeEvent.EventManager.ToString()] = "Global Event",
                [TypeEvent.CustomEvent.ToString()] = "Event",
                [TitleEditDateTime] = "Date Setting Type",
                [TypeSchedule.DateTime.ToString()] = "Once",
                [TypeSchedule.CustomDate.ToString()] = "Repeat",
                [TitleEditTime] = "Time (hours/minutes)",
                [TitleEditDate] = "Date (day/month/year)",
                [TitleSelectedMonths] = "Specify months for repetition",
                [ModeRepeat.None.ToString()] = "Does not repeat",
                [ModeRepeat.Days.ToString()] = "Days",
                [ModeRepeat.Weeks.ToString()] = "Weeks",
                [ModeRepeat.Months.ToString()] = "Months",
                [ModeRepeat.Years.ToString()] = "Years",
                [nameof(ModeRepeat)] = "Select repeat mode",
                [TitleSelectedDays] = "Days of the month",
                [TitleSelectedDaysOfWeek] = "Days of the week",
                [TitleSelectedNumWeekOfMonth] = "Specify intervals of days of the week",
                [TitleResult] = "Event date setup result",
                [Result_month] = "every specified month, ",
                [Result_not_month] = "No months selected.",
                [Result_days] = "on the selected days of the month, ",
                [Result_not_days] = "No days of the month selected.",
                [Result_weeks] = "on the selected days of the week, ",
                [Result_not_weeks] = "No days of the week selected.",
                [Result_num_weeks] = "every {0} specified week of the month, ",
                [Result_not_num_weeks] = "No interval of days of the week selected.",
                [Result_repeat] = "The event repeats {0} on {1}",
                [Result_use] = "The event will take place {0} at {1}",
                [Result_disable] = "The event is disabled",
                [Result_num1] = "first",
                [Result_num2] = "second",
                [Result_num3] = "third",
                [Result_num4] = "fourth",
                [Result_num5] = "fifth",
                [MonthNames[0]] = MonthNames[0],
                [MonthNames[1]] = MonthNames[1],
                [MonthNames[2]] = MonthNames[2],
                [MonthNames[3]] = MonthNames[3],
                [MonthNames[4]] = MonthNames[4],
                [MonthNames[5]] = MonthNames[5],
                [MonthNames[6]] = MonthNames[6],
                [MonthNames[7]] = MonthNames[7],
                [MonthNames[8]] = MonthNames[8],
                [MonthNames[9]] = MonthNames[9],
                [MonthNames[10]] = MonthNames[10],
                [MonthNames[11]] = MonthNames[11]
            };
            foreach (var custom in config.GetCustomLang())
                if (!string.IsNullOrEmpty(custom.Key) && !string.IsNullOrEmpty(custom.Value.ru) &&
                    !string.IsNullOrEmpty(custom.Value.en))
                {
                    if (ru.ContainsKey(custom.Key)) ru[custom.Key] = custom.Value.ru;
                    else ru.TryAdd(custom.Key, custom.Value.ru);

                    if (en.ContainsKey(custom.Key)) en[custom.Key] = custom.Value.en;
                    else en.TryAdd(custom.Key, custom.Value.en);
                }

            lang.RegisterMessages(ru, this, "ru");
            lang.RegisterMessages(en, this);
        }

        private static string GetMessage(string key, string userid = null, params object[] obj)
        {
            return string.Format(Instance.lang.GetMessage(key, Instance, userid), obj);
        }

        #endregion

        #region Initialization

        private void Init()
        {
            Instance = this;
            PlayerUIController.Init();
        }

        private void OnServerInitialized()
        {
            FullScreenTransform = new CuiRectTransformComponent
                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"};

            LoadInterfaceUI();
            LoadSchedule();
            LoadIcons();
            LoadColors();

            LoadImages();

            RegisterPermissions();

            RegisterCommands();
        }

        private void Unload()
        {
            PlayerUIController.Unload();
            Instance = null;
            FullScreenTransform = null;
        }

        #endregion

        #region Data

        private ScreenInterfaceSetup UIFullScreen;
        private UserInterfaceSetup UIMenu;
        private Dictionary<TypeEvent, IColor> EventsColors;
        private Dictionary<TypeEvent, ImageSettings> IconSetup;
        private List<ScheduleToDay> Schedule = new();
        private readonly List<ScheduleToDay> ScheduleEventsManager = new();
        private string FILE_Schedule = $"{nameof(WipeSchedule)}/Schedule";
        private string FILE_IconSetup = $"{nameof(WipeSchedule)}/IconSetup";
        private string FILE_EventsColors = $"{nameof(WipeSchedule)}/EventsColors";

        private delegate T GetDef<out T>() where T : class;

        private void LoadInterfaceUI()
        {
#if SCHEDULE_DEBUG
	                UIMenu = UserInterfaceSetup.GenerateUIMenuV1();
	                UIFullScreen = UserInterfaceSetup.GenerateFullScreen();
#else
            switch (config.Pattern)
            {
                case PatternServerMenu.V1:
                    UIMenu = LoadDateFile($"{nameof(WipeSchedule)}/SetupUI/UIMenuV1",
                        UserInterfaceSetup.GenerateUIMenuV1);
                    break;
                case PatternServerMenu.V2:
                    UIMenu = LoadDateFile($"{nameof(WipeSchedule)}/SetupUI/UIMenuV2",
                        UserInterfaceSetup.GenerateUIMenuV2);
                    break;
                default:
                    UIMenu = LoadDateFile($"{nameof(WipeSchedule)}/SetupUI/UIMenuV2",
                        UserInterfaceSetup.GenerateUIMenuV2);
                    break;
            }

            UIFullScreen = LoadDateFile($"{nameof(WipeSchedule)}/SetupUI/UIFulScreen",
                UserInterfaceSetup.GenerateFullScreen);
#endif
        }

        private void LoadSchedule()
        {
#if SCHEDULE_DEBUG
					Schedule = GetDefaultSchedule();
					ScheduleEventsManager.Clear();
					ScheduleEventsManager.AddRange(GetTestScheduleEvent());
#else
            Schedule = LoadDateFile(FILE_Schedule, GetDefaultSchedule);
#endif
        }

        private void LoadIcons()
        {
#if SCHEDULE_DEBUG
					IconSetup = GetDefaultIconsEvent();
#else
            IconSetup = LoadDateFile(FILE_IconSetup, GetDefaultIconsEvent);
#endif
        }

        private void LoadColors()
        {
#if SCHEDULE_DEBUG
					EventsColors = GetDefaultColorsEvent();
#else
            EventsColors = LoadDateFile(FILE_EventsColors, GetDefaultColorsEvent);
#endif
        }

        private static void SaveDateFile<T>(string name, T value) where T : class
        {
            Interface.Oxide.DataFileSystem.WriteObject(name, value);
        }

        private static T LoadDateFile<T>(string name, GetDef<T> callbackDefault) where T : class
        {
            T obj = null;
            try
            {
                obj = !Interface.Oxide.DataFileSystem.ExistsDatafile(name) ? callbackDefault.Invoke() : Interface.Oxide.DataFileSystem.ReadObject<T>(name);
            }
            finally
            {
                obj ??= callbackDefault.Invoke();
            }

            SaveDateFile(name, obj);
            return obj;
        }

        private List<ScheduleToDay> GetDefaultSchedule()
        {
            return new List<ScheduleToDay>
            {
                new(TypeSchedule.CustomDate, TypeEvent.GlobalWipe, "Global Wipe")
                {
                    CustomDateTimeEvent = new CustomDateTime(
                        ModeRepeat.Months, 1, 1,
                        new bool[7] {false, false, false, true, false, false, false}, new MyTime(20, 0), false, false)
                },
                new(TypeSchedule.CustomDate, TypeEvent.WipeMap, "Wipe Map")
                {
                    CustomDateTimeEvent = new CustomDateTime(
                        ModeRepeat.Months, 1, 1,
                        new bool[7] {false, false, false, true, false, false, false}, new MyTime(20, 0), false, false)
                },
                new(TypeSchedule.CustomDate, TypeEvent.WipeBlock, "Wipe Block")
                {
                    CustomDateTimeEvent = new CustomDateTime(
                        ModeRepeat.Weeks, 1, 1,
                        new bool[7] {true, true, false, false, false, false, false}, new MyTime(20, 0), false, false)
                },
                new(TypeSchedule.CustomDate, TypeEvent.CustomEvent, "Custom Event")
                {
                    Descriptions = "Custom Event Description",
                    CustomDateTimeEvent = new CustomDateTime(
                        ModeRepeat.Weeks, 1, 1,
                        new bool[7] {false, false, true, false, false, false, false}, new MyTime(20, 0), false, false)
                }
            };
        }

#if SCHEDULE_DEBUG
			private List<ScheduleToDay> GetTestScheduleEvent()
			{
				return new()
				{
					new(TypeSchedule.CustomDate, TypeEvent.EventManager, "EventManager 1")
					{
						CustomDateTimeEvent =
 new CustomDateTime(ModeRepeat.Weeks, 1, 1, new bool[7]{ false, false,  false,  false,  false,  true,  false },
							new MyTime(18, 0), false, false)
					},
					new(TypeSchedule.CustomDate, TypeEvent.EventManager, "EventManager 2")
					{
						CustomDateTimeEvent =
 new CustomDateTime(ModeRepeat.Weeks, 1, 1, new bool[7]{ false, false,  false,  false,  false,  true,  false },
							new MyTime(18, 0), false, false)
					},
				};
			}
#endif

        private Dictionary<TypeEvent, IColor> GetDefaultColorsEvent()
        {
            return new Dictionary<TypeEvent, IColor>
            {
                [TypeEvent.GlobalWipe] = new("#E44028", 25),
                [TypeEvent.WipeMap] = new("#29e340", 15),
                [TypeEvent.WipeBlock] = new("#C8AD1E", 15),
                [TypeEvent.EventManager] = new("#9f19a8", 15),
                [TypeEvent.CustomEvent] = new("#5870E9", 15)
            };
        }

        private Dictionary<TypeEvent, ImageSettings> GetDefaultIconsEvent()
        {
            return new Dictionary<TypeEvent, ImageSettings>
            {
                [TypeEvent.GlobalWipe] = new()
                {
                    AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0",
                    Color = new IColor("#FFFFFF"), Image = "https://i.ibb.co/mbM7mGQ/2a80d35cd540.png"
                },
                [TypeEvent.WipeMap] = new()
                {
                    AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0",
                    Color = new IColor("#FFFFFF"), Image = "https://i.ibb.co/mbM7mGQ/2a80d35cd540.png"
                },
                [TypeEvent.WipeBlock] = new()
                {
                    AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0",
                    Color = new IColor("#FFFFFF"), Image = "https://i.ibb.co/mbM7mGQ/2a80d35cd540.png"
                },
                [TypeEvent.EventManager] = new()
                {
                    AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0",
                    Color = new IColor("#FFFFFF"), Image = "https://i.ibb.co/mbM7mGQ/2a80d35cd540.png"
                },
                [TypeEvent.CustomEvent] = new()
                {
                    AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0",
                    Color = new IColor("#FFFFFF"), Image = "https://i.ibb.co/mbM7mGQ/2a80d35cd540.png"
                }
            };
        }

        private class ScheduleToDay
        {
            public TypeSchedule ScheduleType = TypeSchedule.DateTime;
            public TypeEvent EventType = TypeEvent.GlobalWipe;
            public bool Enable = true;
            public string Name = "NameEvent";
            public DateTime DateTimeEvent = DateTime.UtcNow;
            public CustomDateTime CustomDateTimeEvent;
            public string Descriptions = "Description";
            public string UrlIcon = "";
            public IColor Color;

            public ScheduleToDay()
            {
            }

            public ScheduleToDay(TypeSchedule type, TypeEvent @event, string name)
            {
                ScheduleType = type;
                EventType = @event;
                Name = name;
            }

            public bool HasEventOfDay(DateTime dateTime)
            {
                return Enable && dateTime.Year == DateTimeEvent.Year && dateTime.Month == DateTimeEvent.Month &&
                       dateTime.Day == DateTimeEvent.Day;
            }

            private bool HasEventOfMonth(DateTime dateTime)
            {
                return dateTime.Year == DateTimeEvent.Year && dateTime.Month == DateTimeEvent.Month;
            }

            public void SetScheduleOfMonth(DateTime startDate, List<ScheduleToDay> list)
            {
                if (!Enable) return;

                if (startDate.Date < new DateTime(DateTimeEvent.Year, DateTimeEvent.Month, 1).Date) return;

                var daysInMonth = DateTime.DaysInMonth(startDate.Year, startDate.Month);

                switch (CustomDateTimeEvent.Mode)
                {
                    case ModeRepeat.None:
                    {
                        list.Add(new ScheduleToDay
                        {
                            ScheduleType = TypeSchedule.DateTime, Enable = true, EventType = EventType,
                            Name = Name, UrlIcon = UrlIcon, Descriptions = Descriptions,
                            Color = Color,
                            DateTimeEvent = new DateTime(DateTimeEvent.Year, DateTimeEvent.Month, DateTimeEvent.Day,
                                CustomDateTimeEvent.Times.Hour, CustomDateTimeEvent.Times.Minute, 0)
                        });

                        return;
                    }
                    case ModeRepeat.Days:
                    {
                        if (CustomDateTimeEvent.DaysInterval < 2) return;

                        for (var i = 0; i < daysInMonth; i++)
                            if ((i + 1) % CustomDateTimeEvent.DaysInterval == 0)
                                list.Add(new ScheduleToDay
                                {
                                    ScheduleType = TypeSchedule.DateTime, Enable = true, EventType = EventType,
                                    Name = Name, UrlIcon = UrlIcon, Descriptions = Descriptions,
                                    Color = Color,
                                    DateTimeEvent = new DateTime(startDate.Year, startDate.Month, i + 1,
                                        CustomDateTimeEvent.Times.Hour, CustomDateTimeEvent.Times.Minute, 0)
                                });

                        return;
                    }

                    case ModeRepeat.Weeks:
                    {
                        var dateTime1 = new DateTime(startDate.Year, startDate.Month, 1);
                        for (var i = 0; i < daysInMonth; i++)
                        {
                            var newDay = dateTime1.AddDays(i);

                            var weeks = Mathf.CeilToInt((float) (newDay.Subtract(DateTimeEvent).TotalDays / 7f));

                            if (CustomDateTimeEvent.DaysInterval > 1 && weeks % CustomDateTimeEvent.DaysInterval != 0)
                                continue;

                            if (CustomDateTimeEvent.HasDayOfWeek(newDay.DayOfWeek))
                                list.Add(new ScheduleToDay
                                {
                                    ScheduleType = TypeSchedule.DateTime, Enable = true, EventType = EventType,
                                    Name = Name, UrlIcon = UrlIcon, Descriptions = Descriptions,
                                    Color = Color,
                                    DateTimeEvent = new DateTime(startDate.Year, startDate.Month, i + 1,
                                        CustomDateTimeEvent.Times.Hour, CustomDateTimeEvent.Times.Minute, 0)
                                });
                        }

                        return;
                    }

                    case ModeRepeat.Months:
                    {
                        if (CustomDateTimeEvent.MonthRepeatOnDay)
                        {
                            if (CustomDateTimeEvent.DaysInterval > 1)
                            {
                                var diff = GetMonthsDifference(new DateTime(DateTimeEvent.Year, DateTimeEvent.Month, 1),
                                    new DateTime(startDate.Year, startDate.Month, 1));
                                if (diff > 0 && diff % CustomDateTimeEvent.DaysInterval != 0)
                                    return;
                            }

                            for (var i = 0; i < daysInMonth; i++)
                            {
                                var day = i + 1;
                                if (day == DateTimeEvent.Day)
                                    list.Add(new ScheduleToDay
                                    {
                                        ScheduleType = TypeSchedule.DateTime, Enable = true, EventType = EventType,
                                        Name = Name, UrlIcon = UrlIcon, Descriptions = Descriptions,
                                        Color = Color,
                                        DateTimeEvent = new DateTime(startDate.Year, startDate.Month, day,
                                            CustomDateTimeEvent.Times.Hour, CustomDateTimeEvent.Times.Minute, 0)
                                    });
                            }
                        }

                        if (CustomDateTimeEvent.MonthRepeatOnDayOfWeek)
                        {
                            if (CustomDateTimeEvent.WeeksInterval > 1)
                            {
                                var diff = GetMonthsDifference(new DateTime(DateTimeEvent.Year, DateTimeEvent.Month, 1),
                                    new DateTime(startDate.Year, startDate.Month, 1));
                                if (diff > 0 && diff % CustomDateTimeEvent.WeeksInterval != 0)
                                    return;
                            }

                            var dayOfWeek = DateTimeEvent.DayOfWeek;
                            var weekOfMonth = GetWeekOfMonth(DateTimeEvent);

                            var firstDayOfMonth = new DateTime(startDate.Year, startDate.Month, 1);

                            for (var i = 0; i < daysInMonth; i++)
                            {
                                var currentDate = firstDayOfMonth.AddDays(i);

                                var isSecondSunday = currentDate.DayOfWeek == dayOfWeek &&
                                                     GetWeekOfMonth(currentDate) == weekOfMonth;
                                if (isSecondSunday)
                                    list.Add(new ScheduleToDay
                                    {
                                        ScheduleType = TypeSchedule.DateTime, Enable = true, EventType = EventType,
                                        Name = Name, UrlIcon = UrlIcon, Descriptions = Descriptions,
                                        Color = Color,
                                        DateTimeEvent = new DateTime(startDate.Year, startDate.Month, i + 1,
                                            CustomDateTimeEvent.Times.Hour, CustomDateTimeEvent.Times.Minute, 0)
                                    });
                            }
                        }

                        return;
                    }

                    case ModeRepeat.Years:
                    {
                        if (DateTimeEvent.Month == startDate.Month)
                        {
                            var diff = startDate.Year - DateTimeEvent.Year;
                            if (diff > 0 && diff % CustomDateTimeEvent.DaysInterval != 0)
                                return;

                            list.Add(new ScheduleToDay
                            {
                                ScheduleType = TypeSchedule.DateTime, Enable = true, EventType = EventType,
                                Name = Name, UrlIcon = UrlIcon, Descriptions = Descriptions,
                                Color = Color,
                                DateTimeEvent = new DateTime(startDate.Year, startDate.Month, DateTimeEvent.Day,
                                    CustomDateTimeEvent.Times.Hour, CustomDateTimeEvent.Times.Minute, 0)
                            });
                        }

                        return;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private static int GetMonthsDifference(DateTime startDate, DateTime endDate)
        {
            if (startDate > endDate) return 0;

            var years = endDate.Year - startDate.Year;

            var months = years * 12;

            months += endDate.Month - startDate.Month;

            if (endDate.Day < startDate.Day) months--;

            return months;
        }

        private struct CustomDateTime
        {
            public ModeRepeat Mode;
            public int DaysInterval;
            public int WeeksInterval;
            public bool[] DaysOfWeek;
            public MyTime Times;

            public bool MonthRepeatOnDay, MonthRepeatOnDayOfWeek;

            public CustomDateTime(ModeRepeat mode, int daysInterval, int weeksInterval, bool[] daysOfWeek, MyTime times,
                bool monthRepeatOnDay, bool monthRepeatOnDayOfWeek)
            {
                Mode = mode;
                Times = times;

                DaysOfWeek = new bool[7];
                DaysOfWeek.SetArray(daysOfWeek, false);

                DaysInterval = daysInterval;
                WeeksInterval = weeksInterval;

                MonthRepeatOnDay = monthRepeatOnDay;
                MonthRepeatOnDayOfWeek = monthRepeatOnDayOfWeek;
            }

            public bool HasDayOfWeek(DayOfWeek dayOfWeek)
            {
                return DaysOfWeek != null && DaysOfWeek[(int) dayOfWeek];
            }
        }

        public struct MyTime
        {
            public int Hour;
            public int Minute;

            public MyTime(int hour, int minute)
            {
                Hour = hour;
                Minute = minute;
            }
        }

        private class ScreenInterfaceSetup : UserInterfaceSetup
        {
            [JsonProperty(LangRu ? "Цвет фона экрана" : "Screen background color")]
            public IColor BackgroundScreen;

            [JsonProperty("Material")] public string Material;

            [JsonProperty("Sprite")] public string Sprite;

            [JsonProperty(LangRu ? "Кнопка закрытия" : "Button Close")]
            public ButtonCloseSetup ButtonClose = new();

            public void AddScreenUI(List<CuiElement> container, string parent, string name, string destroy)
            {
                container.Add(new CuiElement
                {
                    Parent = parent,
                    Name = name,
                    DestroyUi = destroy,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = BackgroundScreen.Get(),
                            Sprite = "assets/content/ui/UI.Background.TileTex.psd",
                            Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                            ImageType = Image.Type.Tiled
                        },
                        FullScreenTransform,
                        new CuiNeedsCursorComponent(),
                        new CuiNeedsKeyboardComponent()
                    }
                });
            }
        }

        private class UserInterfaceSetup
        {
            [JsonProperty(LangRu ? "Главная панель" : "Main panel")]
            public ImageSettings MainPanel;

            [JsonProperty(LangRu ? "Заголовок" : "Header")]
            public PanelHeaderUI HeaderPanel;

            [JsonProperty(LangRu ? "Кнопка изменения расписания" : "Schedule change button")]
            public ButtonSettings ButtonSetup;

            [JsonProperty(LangRu ? "Календарь" : "Calendar")]
            public PanelCalendarUI PanelCalendar;

            #region Classes

            public class PanelHeaderUI
            {
                [JsonProperty(LangRu ? "Фон" : "Background")]
                public ImageSettings Background = new();

                [JsonProperty(LangRu ? "Показать заголовок?" : "Show the title?")]
                public bool ShowHeader;

                [JsonProperty(LangRu ? "Заголовок" : "Title")]
                public TextSettings Header = new();

                [JsonProperty(LangRu ? "Показать линию" : "Show Line?")]
                public bool ShowLine;

                [JsonProperty(LangRu ? "Линия" : "Line")]
                public ImageSettings Line = new();
            }

            public class PanelCalendarUI
            {
                [JsonProperty(LangRu ? "Фон" : "Background")]
                public ImageSettings Background = new();

                [JsonProperty(LangRu ? "Дни Недели" : "Day Of Week")]
                public DayOfWeekPanelUI DaysOfWeekPanel = new();

                [JsonProperty(LangRu ? "Таблица дней" : "Table of days")]
                public DaysTableUI DaysTable = new();

                [JsonProperty(LangRu ? "Расписание" : "Schedule")]
                public PanelScheduleUI PanelSchedule = new();

                public class DayOfWeekPanelUI
                {
                    [JsonProperty(LangRu ? "Фон" : "Background")]
                    public ImageSettings Background = new();

                    [JsonProperty(LangRu ? "Название" : "Title")]
                    public TextSettings Title = new();
                }

                public class DaysTableUI
                {
                    [JsonProperty(LangRu ? "Фон" : "Background")]
                    public ImageSettings Background = new();

                    [JsonProperty(LangRu ? "Отступы по горизонтали (%)" : "Horizontal margins (%)")]
                    public float MarginsHorizontal = 10;

                    [JsonProperty(LangRu ? "Отступы по вертикали (%)" : "Vertical margins (%)")]
                    public float MarginsVertical = 10;

                    [JsonProperty(LangRu
                        ? "Фон дня в таблице текущего месяца"
                        : "Background of the day in the current month table")]
                    public ButtonSettings BackgroundDayTableInside = new();

                    [JsonProperty(LangRu
                        ? "Фон дня в таблице за пределами текущего месяца"
                        : "The background of the day in the table outside the current month")]
                    public ButtonSettings BackgroundDayTableOutside = new();
                }

                public class PanelScheduleUI
                {
                    [JsonProperty(LangRu ? "Фон панели месяца" : "Background of the month panel")]
                    public ImageSettings BackgroundMonth = new();

                    [JsonProperty(LangRu ? "Название месяца" : "Title of the month")]
                    public TextSettings TitleMonth = new();

                    [JsonProperty(LangRu ? "Кнопка предыдущего месяца" : "The button of the previous month")]
                    public ButtonSettings ButtonPreviousMonth = new();

                    [JsonProperty(LangRu ? "Кнопка следующего месяца" : "Next Month's button")]
                    public ButtonSettings ButtonNextMonth = new();

                    [JsonProperty(LangRu ? "Панель расписания дня" : "The schedule panel of the day")]
                    public PanelShowDayUI PanelShowDay = new();

                    [JsonProperty(LangRu ? "Панель аннотации" : "The Annotation panel")]
                    public ScheduleInfoUI ScheduleInfo = new();

                    public class PanelShowDayUI
                    {
                        [JsonProperty(LangRu ? "Фон" : "Background")]
                        public ImageSettings Background = new();

                        [JsonProperty(LangRu ? "Заголовок" : "Header")]
                        public PanelHeaderUI PanelHeader = new();

                        [JsonProperty(LangRu ? "Панель прокрутки событий" : "Event Scroll panel")]
                        public ScrollPanelUI ScrollPanel = new();

                        public class PanelHeaderUI
                        {
                            [JsonProperty(LangRu ? "Фон" : "Background")]
                            public ImageSettings Background = new();

                            [JsonProperty(LangRu ? "Число" : "Title")]
                            public TextSettings Number = new();

                            [JsonProperty(LangRu ? "Месяц" : "Month")]
                            public TextSettings Month = new();

                            [JsonProperty(LangRu ? "День недели" : "Day of the week")]
                            public TextSettings DayOfWeek = new();

                            [JsonProperty(LangRu ? "Показать линию" : "Show Line?")]
                            public bool ShowLine;

                            [JsonProperty(LangRu ? "Линия" : "Line")]
                            public ImageSettings Line = new();
                        }
                    }

                    public class ScheduleInfoUI
                    {
                        [JsonProperty(LangRu ? "Фон" : "Background")]
                        public ImageSettings Background = new();

                        [JsonProperty(LangRu ? "Панель информации" : "Information Panel")]
                        public ImageSettings BackgroundInfo = new();

                        [JsonProperty(LangRu ? "Иконка" : "Icon")]
                        public ImageSettings Icon = new();

                        [JsonProperty(LangRu ? "Текст информации" : "The text of the information")]
                        public TextSettings TextInfo = new();

                        [JsonProperty(LangRu ? "Фон панели аннотации" : "Background of the annotation panel")]
                        public ImageSettings BackgroundAnnotation = new();

                        [JsonProperty(
                            LangRu ? "Размер цветового маркера события" : "The size of the event color marker")]
                        public Vector2 SizeMarkerColor;

                        [JsonProperty(LangRu ? "Аннотация цвета события" : "Event color Annotation")]
                        public TextSettings Annotation = new();
                    }
                }
            }

            public class ScrollPanelUI
            {
                [JsonProperty(LangRu ? "Фон" : "Background")]
                public ImageSettings Background = new();

                [JsonProperty(LangRu ? "Вид прокрутки" : "Scroll view")]
                public ScrollViewUI ScrollView = new();

                [JsonProperty(LangRu ? "Содержимое прокрутки" : "Scroll Content")]
                public ContentScrollUI ContentScroll = new();

                public class ContentScrollUI
                {
                    [JsonProperty(LangRu ? "Отступы" : "Margins")]
                    public MarginsInScrollUI MarginsInScroll;

                    [JsonProperty(LangRu ? "Фон элемента в прокрутке" : "Scrolling element background")]
                    public ImageSettings BackgroundContent = new();

                    [JsonProperty(LangRu ? "Высота элемента в прокрутке" : "The height of the element in the scroll")]
                    public float Height;

                    [JsonProperty(LangRu ? "Позиция иконки в элементе" : "The position of the icon in the element")]
                    public InterfacePosition IconTransform = new();

                    [JsonProperty(LangRu ? "Время события" : "Time of the event")]
                    public TextSettings TimeEvent = new();

                    [JsonProperty(LangRu ? "Название типа события" : "Name of the event type")]
                    public TextSettings TitleEvent = new();

                    [JsonProperty(LangRu ? "Кнопка информации о событии" : "Event Information button")]
                    public ButtonSettings ButtonInfo = new();

                    [JsonProperty(LangRu
                        ? "Цвет затенения расписания при открытии описания"
                        : "The shading color of the schedule when opening the description")]
                    public IColor ColorSchedule = new("#000000", 80);

                    [JsonProperty(LangRu ? "Длина панели описания" : "Description Panel length")]
                    public float PanelDescriptionLength = 250;

                    [JsonProperty(LangRu ? "Высота панели описания" : "Description panel height")]
                    public float PanelDescriptionHeight = 120;

                    [JsonProperty(LangRu ? "Фон заголовка панели описания" : "Description panel title background")]
                    public ImageSettings TitlePanelDescription = new();

                    [JsonProperty(LangRu ? "Название события" : "Event title")]
                    public TextSettings TitleEventDescription = new();

                    [JsonProperty(LangRu
                        ? "Фон панели описания панели описания"
                        : "Description panel background description panel")]
                    public ImageSettings DescriptionPanelBackground = new();

                    [JsonProperty(LangRu ? "Описание события" : "Description of the event")]
                    public TextSettings Description = new();
                }
            }

            #endregion

            public static ScreenInterfaceSetup GenerateFullScreen()
            {
                return new ScreenInterfaceSetup
                {
                    BackgroundScreen = new IColor("#191919", 90),
                    Sprite = "assets/content/ui/UI.Background.TileTex.psd",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                    ButtonClose = new ButtonCloseSetup
                    {
                        BackgroundClose = new ImageSettings
                        {
                            AnchorMin = "1 1",
                            AnchorMax = "1 1",
                            OffsetMin = "-40 -40",
                            OffsetMax = "0 0",
                            Color = new IColor("#E44028"),
                            Sprite = "assets/content/ui/UI.Background.TileTex.psd"
                        },
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-9 -9",
                        OffsetMax = "9 9",
                        Color = new IColor("#FFFFFF"),
                        Sprite = "assets/icons/close.png",
                        Material = null
                    },
                    ButtonSetup = new ButtonSettings
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = "20 10",
                        OffsetMax = "180 40",
                        ButtonColor = new IColor("#252525"),
                        Align = TextAnchor.MiddleCenter,
                        Color = new IColor("#e0d9d1", 60),
                        FontSize = 14,
                        IsBold = true,
                        Sprite = "assets/content/ui/UI.Background.TileTex.psd"
                    },
                    MainPanel = new ImageSettings
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-500 -255",
                        OffsetMax = "500 255",
                        Color = new IColor("#191919", 50),
                        Material = "assets/content/ui/uibackgroundblur.mat"
                    },
                    HeaderPanel = new PanelHeaderUI
                    {
                        Background = new ImageSettings
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = "0 -40",
                            OffsetMax = "-40 0",
                            Color = new IColor("#494949"),
                            Sprite = "assets/content/ui/UI.Background.Transparent.LinearLTR.tga"
                        },
                        ShowHeader = true,
                        Header = new TextSettings
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = "20 0",
                            OffsetMax = "0 0",
                            Align = TextAnchor.MiddleLeft,
                            IsBold = true,
                            FontSize = 18,
                            Color = new IColor("#E2DBD3")
                        },
                        ShowLine = false,
                        Line = new ImageSettings
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 0",
                            OffsetMin = "0 -2",
                            OffsetMax = "0 0",
                            Color = new IColor("#373737", 50)
                        }
                    },
                    PanelCalendar = new PanelCalendarUI
                    {
                        Background = new ImageSettings
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = "20 50",
                            OffsetMax = "-20 -60",
                            Color = new IColor("#ffffff", 0f)
                        },
                        DaysOfWeekPanel = new PanelCalendarUI.DayOfWeekPanelUI
                        {
                            Background = new ImageSettings
                            {
                                AnchorMin = "0 1",
                                AnchorMax = "1 1",
                                OffsetMin = "0 -38",
                                OffsetMax = "-366 0",
                                Color = new IColor("#696969", 20),
                                Sprite = "assets/content/ui/ui.background.tile.psd",
                                Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                            },
                            Title = new TextSettings
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                                OffsetMin = "0 0",
                                OffsetMax = "0 0",
                                Align = TextAnchor.MiddleCenter,
                                IsBold = true,
                                FontSize = 13,
                                Color = new IColor("#e0d9d1")
                            }
                        },
                        DaysTable = new PanelCalendarUI.DaysTableUI
                        {
                            Background = new ImageSettings
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                                OffsetMin = "0 0",
                                OffsetMax = "-366 -48",
                                Color = new IColor("#ffffff", 0f)
                            },
                            MarginsVertical = 2,
                            MarginsHorizontal = 1.5f,
                            BackgroundDayTableInside = new ButtonSettings
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                                OffsetMin = "0 0",
                                OffsetMax = "0 0",
                                ButtonColor = new IColor("#696969", 15),
                                Sprite = "assets/content/ui/ui.background.tile.psd",
                                Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                                Align = TextAnchor.MiddleCenter,
                                IsBold = true,
                                FontSize = 35,
                                Color = new IColor("#e0d9d1")
                            },
                            BackgroundDayTableOutside = new ButtonSettings
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                                OffsetMin = "0 0",
                                OffsetMax = "0 0",
                                ButtonColor = new IColor("#696969", 5),
                                Sprite = "assets/content/ui/ui.background.tile.psd",
                                Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                                Align = TextAnchor.MiddleCenter,
                                IsBold = true,
                                FontSize = 25,
                                Color = new IColor("#e0d9d1", 20)
                            }
                        },
                        PanelSchedule = new PanelCalendarUI.PanelScheduleUI
                        {
                            BackgroundMonth = new ImageSettings
                            {
                                AnchorMin = "1 1",
                                AnchorMax = "1 1",
                                OffsetMin = "-346 -38",
                                OffsetMax = "0 0",
                                Color = new IColor("#696969", 60),
                                Sprite = "assets/content/ui/UI.Background.TileTex.psd"
                            },
                            TitleMonth = new TextSettings
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                                OffsetMin = "0 0",
                                OffsetMax = "0 0",
                                Align = TextAnchor.MiddleCenter,
                                IsBold = true,
                                FontSize = 22,
                                Color = new IColor("#e0d9d1")
                            },
                            ButtonPreviousMonth = new ButtonSettings
                            {
                                AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "10 -8", OffsetMax = "23 8",
                                ButtonColor = new IColor("#ffffff", 0),
                                ImageColor = new IColor("#ffffff"),
                                Image = "https://i.ibb.co/F5cBPDm/be9d0d8d0f86.png"
                            },
                            ButtonNextMonth = new ButtonSettings
                            {
                                AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-23 -8", OffsetMax = "-10 8",
                                ButtonColor = new IColor("#ffffff", 0),
                                ImageColor = new IColor("#ffffff"),
                                Image = "https://i.ibb.co/3Tz8KmF/5f96eeaf05fa.png"
                            },
                            PanelShowDay = new PanelCalendarUI.PanelScheduleUI.PanelShowDayUI
                            {
                                Background = new ImageSettings
                                {
                                    AnchorMin = "1 0",
                                    AnchorMax = "1 1",
                                    OffsetMin = "-346 110",
                                    OffsetMax = "0 -48",
                                    Color = new IColor("#ffffff", 0f)
                                },
                                PanelHeader = new PanelCalendarUI.PanelScheduleUI.PanelShowDayUI.PanelHeaderUI
                                {
                                    Background = new ImageSettings
                                    {
                                        AnchorMin = "0 1",
                                        AnchorMax = "1 1",
                                        OffsetMin = "0 -60",
                                        OffsetMax = "0 -3",
                                        Color = new IColor("#ffffff", 50),
                                        Image = "https://i.ibb.co/8gHqy9p/banner.png"
                                        // Sprite = "assets/content/ui/ui.background.transparent.linear.psd"
                                    },
                                    ShowLine = true,
                                    Line = new ImageSettings
                                    {
                                        AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 3",
                                        Color = new IColor("#D74933")
                                    },
                                    Number = new TextSettings
                                    {
                                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-142.5 -40",
                                        OffsetMax = "-30 40",
                                        Align = TextAnchor.MiddleRight,
                                        IsBold = true,
                                        FontSize = 56,
                                        Color = new IColor("#e34026")
                                    },
                                    Month = new TextSettings
                                    {
                                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-20 0",
                                        OffsetMax = "120 40",
                                        // AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-40 2.25", OffsetMax = "80 32.25",
                                        Align = TextAnchor.LowerLeft,
                                        IsBold = true,
                                        FontSize = 20,
                                        Color = new IColor("#e34026")
                                    },
                                    DayOfWeek = new TextSettings
                                    {
                                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-20 -20",
                                        OffsetMax = "120 0",
                                        // AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-40 -17.75", OffsetMax = "79 2.25",
                                        Align = TextAnchor.UpperLeft,
                                        IsBold = false,
                                        FontSize = 14,
                                        Color = new IColor("#e0d9d1", 50)
                                    }
                                },
                                ScrollPanel = new ScrollPanelUI
                                {
                                    Background = new ImageSettings
                                    {
                                        AnchorMin = "0 0",
                                        AnchorMax = "1 1",
                                        OffsetMin = "0 0",
                                        OffsetMax = "0 -66",
                                        Color = new IColor("#ffffff", 0f)
                                    },
                                    ScrollView = new ScrollViewUI
                                    {
                                        Scrollbar = new ScrollViewUI.ScrollBarSettings
                                        {
                                            AutoHide = true,
                                            Size = 3,
                                            HandleColor = new IColor("#D74933"),
                                            HighlightColor = new IColor("#D74933"),
                                            PressedColor = new IColor("#D74933"),
                                            HandleSprite = "assets/content/ui/UI.Background.TileTex.psd",
                                            TrackColor = new IColor("#38393F", 40),
                                            TrackSprite = "assets/content/ui/UI.Background.TileTex.psd"
                                        },
                                        ScrollType = ScrollType.Vertical,
                                        MovementType = ScrollRect.MovementType.Clamped,
                                        Elasticity = 0.1f,
                                        DecelerationRate = 1,
                                        ScrollSensitivity = 10,
                                        MinHeight = 0
                                    },
                                    ContentScroll = new ScrollPanelUI.ContentScrollUI
                                    {
                                        BackgroundContent = new ImageSettings
                                        {
                                            AnchorMin = "0 0",
                                            AnchorMax = "1 1",
                                            OffsetMin = "0 0",
                                            OffsetMax = "0 0",
                                            Color = new IColor("#696969", 30),
                                            Sprite = "assets/content/ui/UI.Background.Transparent.LinearLTR.tga"
                                        },
                                        Height = 40,
                                        MarginsInScroll = new MarginsInScrollUI(0, 5),
                                        IconTransform = new InterfacePosition
                                        {
                                            AnchorMin = "0 0.5",
                                            AnchorMax = "0 0.5",
                                            OffsetMin = "10 -10",
                                            OffsetMax = "30 10"
                                        },
                                        TimeEvent = new TextSettings
                                        {
                                            AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "40 -18",
                                            OffsetMax = "-40 0",
                                            Align = TextAnchor.LowerLeft,
                                            IsBold = true,
                                            FontSize = 11,
                                            Color = new IColor("#e0d9d1")
                                        },
                                        TitleEvent = new TextSettings
                                        {
                                            AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "40 0",
                                            OffsetMax = "-40 24",
                                            Align = TextAnchor.UpperLeft,
                                            IsBold = true,
                                            FontSize = 14,
                                            Color = new IColor("#e0d9d1", 50)
                                        },
                                        ButtonInfo = new ButtonSettings
                                        {
                                            AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-30 -10",
                                            OffsetMax = "-10 10",
                                            ButtonColor = new IColor("#ffffff", 0),
                                            ImageColor = new IColor("#ffffff"),
                                            Image = "https://i.ibb.co/M1zPJbp/5e3c035f6001.png"
                                        },

                                        PanelDescriptionLength = 250,
                                        PanelDescriptionHeight = 120,
                                        ColorSchedule = new IColor("#000000", 50),

                                        TitlePanelDescription = new ImageSettings
                                        {
                                            AnchorMin = "0 1",
                                            AnchorMax = "1 1",
                                            OffsetMin = "0 -20",
                                            OffsetMax = "2 0",
                                            Color = new IColor("#64A1CF"),
                                            Sprite = "assets/content/ui/UI.Background.TileTex.psd",
                                            Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                                        },
                                        TitleEventDescription = new TextSettings
                                        {
                                            AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 0", OffsetMax = "0 0",
                                            Align = TextAnchor.MiddleLeft,
                                            IsBold = true,
                                            FontSize = 14,
                                            Color = new IColor("#ffffff")
                                        },
                                        DescriptionPanelBackground = new ImageSettings
                                        {
                                            AnchorMin = "0 0",
                                            AnchorMax = "1 1",
                                            OffsetMin = "0 0",
                                            OffsetMax = "0 -20",
                                            Color = new IColor("#696969", 50),
                                            Sprite = "assets/content/ui/UI.Background.TileTex.psd",
                                            Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                                        },
                                        Description = new TextSettings
                                        {
                                            AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5",
                                            OffsetMax = "-5 -5",
                                            Align = TextAnchor.UpperLeft,
                                            IsBold = true,
                                            FontSize = 11,
                                            Color = new IColor("#999694")
                                        }
                                    }
                                }
                            },
                            ScheduleInfo = new PanelCalendarUI.PanelScheduleUI.ScheduleInfoUI
                            {
                                Background = new ImageSettings
                                {
                                    AnchorMin = "1 0",
                                    AnchorMax = "1 0",
                                    OffsetMin = "-346 0",
                                    OffsetMax = "0 100",
                                    Color = new IColor("#ffffff", 0f)
                                },
                                BackgroundInfo = new ImageSettings
                                {
                                    AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -38", OffsetMax = "0 0",
                                    Color = new IColor("#71B8ED", 5),
                                    Sprite = "assets/content/ui/ui.background.tile.psd",
                                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                                },
                                Icon = new ImageSettings
                                {
                                    AnchorMin = "0 0",
                                    AnchorMax = "0 1",
                                    OffsetMin = "0 0",
                                    OffsetMax = "38 0",
                                    Color = new IColor("#ffffff"),
                                    Image = "https://i.ibb.co/rHBx0pN/icon-info-panel.png"
                                },
                                TextInfo = new TextSettings
                                {
                                    AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "50 0", OffsetMax = "0 0",
                                    Align = TextAnchor.MiddleLeft,
                                    IsBold = false,
                                    FontSize = 11,
                                    Color = new IColor("#64A1CF", 50)
                                },
                                BackgroundAnnotation = new ImageSettings
                                {
                                    AnchorMin = "0 0",
                                    AnchorMax = "1 1",
                                    OffsetMin = "0 0",
                                    OffsetMax = "0 -38",
                                    Color = new IColor("#ffffff", 0f)
                                },
                                SizeMarkerColor = new Vector2(14, 14),
                                Annotation = new TextSettings
                                {
                                    AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "5 -3", OffsetMax = "150 3",
                                    Align = TextAnchor.MiddleLeft,
                                    IsBold = true,
                                    FontSize = 12,
                                    Color = new IColor("#999694")
                                }
                            }
                        }
                    }
                };
            }

            public static UserInterfaceSetup GenerateUIMenuV2()
            {
                return new UserInterfaceSetup
                {
                    MainPanel = new ImageSettings
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "0 -600",
                        OffsetMax = "940 0",
                        Color = new IColor("#ffffff", 0),
                        Sprite = "assets/content/ui/UI.Background.TileTex.psd"
                    },
                    HeaderPanel = new PanelHeaderUI
                    {
                        Background = new ImageSettings
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = "40 -70",
                            OffsetMax = "-10 -20",
                            Color = new IColor("#ffffff", 0)
                        },
                        ShowHeader = true,
                        Header = new TextSettings
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = "0 0",
                            OffsetMax = "0 0",
                            Align = TextAnchor.UpperLeft,
                            IsBold = true,
                            FontSize = 32,
                            Color = new IColor("#ce432d", 90)
                        },
                        ShowLine = true,
                        Line = new ImageSettings
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 0",
                            OffsetMin = "0 -2",
                            OffsetMax = "0 0",
                            Color = new IColor("#373737", 50)
                        }
                    },
                    ButtonSetup = new ButtonSettings
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = "40 30",
                        OffsetMax = "200 60",
                        ButtonColor = new IColor("#252525"),
                        Align = TextAnchor.MiddleCenter,
                        Color = new IColor("#e0d9d1", 60),
                        FontSize = 14,
                        IsBold = true,
                        Sprite = "assets/content/ui/UI.Background.TileTex.psd"
                    },
                    PanelCalendar = new PanelCalendarUI
                    {
                        Background = new ImageSettings
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = "40 70",
                            OffsetMax = "-10 -90",
                            Color = new IColor("#ffffff", 0f)
                        },
                        DaysOfWeekPanel = new PanelCalendarUI.DayOfWeekPanelUI
                        {
                            Background = new ImageSettings
                            {
                                AnchorMin = "0 1",
                                AnchorMax = "1 1",
                                OffsetMin = "0 -38",
                                OffsetMax = "-322 0",
                                Color = new IColor("#696969", 20),
                                Sprite = "assets/content/ui/ui.background.tile.psd",
                                Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                            },
                            Title = new TextSettings
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                                OffsetMin = "0 0",
                                OffsetMax = "0 0",
                                Align = TextAnchor.MiddleCenter,
                                IsBold = true,
                                FontSize = 13,
                                Color = new IColor("#e0d9d1")
                            }
                        },
                        DaysTable = new PanelCalendarUI.DaysTableUI
                        {
                            Background = new ImageSettings
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                                OffsetMin = "0 0",
                                OffsetMax = "-322 -48",
                                Color = new IColor("#ffffff", 0f)
                            },
                            MarginsVertical = 2,
                            MarginsHorizontal = 1.5f,
                            BackgroundDayTableInside = new ButtonSettings
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                                OffsetMin = "0 0",
                                OffsetMax = "0 0",
                                ButtonColor = new IColor("#696969", 15),
                                Sprite = "assets/content/ui/ui.background.tile.psd",
                                Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                                Align = TextAnchor.MiddleCenter,
                                IsBold = true,
                                FontSize = 35,
                                Color = new IColor("#e0d9d1")
                            },
                            BackgroundDayTableOutside = new ButtonSettings
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                                OffsetMin = "0 0",
                                OffsetMax = "0 0",
                                ButtonColor = new IColor("#696969", 5),
                                Sprite = "assets/content/ui/ui.background.tile.psd",
                                Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                                Align = TextAnchor.MiddleCenter,
                                IsBold = true,
                                FontSize = 25,
                                Color = new IColor("#e0d9d1", 20)
                            }
                        },
                        PanelSchedule = new PanelCalendarUI.PanelScheduleUI
                        {
                            BackgroundMonth = new ImageSettings
                            {
                                AnchorMin = "1 1",
                                AnchorMax = "1 1",
                                OffsetMin = "-312 -38",
                                OffsetMax = "0 0",
                                Color = new IColor("#696969", 20),
                                Sprite = "assets/content/ui/ui.background.tile.psd",
                                Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                            },
                            TitleMonth = new TextSettings
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                                OffsetMin = "0 0",
                                OffsetMax = "0 0",
                                Align = TextAnchor.MiddleCenter,
                                IsBold = true,
                                FontSize = 22,
                                Color = new IColor("#e0d9d1")
                            },
                            ButtonPreviousMonth = new ButtonSettings
                            {
                                AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "10 -8", OffsetMax = "23 8",
                                ButtonColor = new IColor("#ffffff", 0),
                                ImageColor = new IColor("#ffffff"),
                                Image = "https://i.ibb.co/F5cBPDm/be9d0d8d0f86.png"
                            },
                            ButtonNextMonth = new ButtonSettings
                            {
                                AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-23 -8", OffsetMax = "-10 8",
                                ButtonColor = new IColor("#ffffff", 0),
                                ImageColor = new IColor("#ffffff"),
                                Image = "https://i.ibb.co/3Tz8KmF/5f96eeaf05fa.png"
                            },
                            PanelShowDay = new PanelCalendarUI.PanelScheduleUI.PanelShowDayUI
                            {
                                Background = new ImageSettings
                                {
                                    AnchorMin = "0 0",
                                    AnchorMax = "1 1",
                                    OffsetMin = "580 110",
                                    OffsetMax = "0 -48",
                                    Color = new IColor("#ffffff", 0f)
                                },
                                PanelHeader = new PanelCalendarUI.PanelScheduleUI.PanelShowDayUI.PanelHeaderUI
                                {
                                    Background = new ImageSettings
                                    {
                                        AnchorMin = "0 1",
                                        AnchorMax = "1 1",
                                        OffsetMin = "0 -60",
                                        OffsetMax = "0 -3",
                                        Color = new IColor("#ffffff", 50),
                                        Image = "https://i.ibb.co/8gHqy9p/banner.png"
                                        // Sprite = "assets/content/ui/ui.background.transparent.linear.psd"
                                    },
                                    ShowLine = true,
                                    Line = new ImageSettings
                                    {
                                        AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 3",
                                        Color = new IColor("#D74933")
                                    },
                                    Number = new TextSettings
                                    {
                                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-142.5 -40",
                                        OffsetMax = "-30 40",
                                        Align = TextAnchor.MiddleRight,
                                        IsBold = true,
                                        FontSize = 56,
                                        Color = new IColor("#e34026")
                                    },
                                    Month = new TextSettings
                                    {
                                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-20 0",
                                        OffsetMax = "120 40",
                                        // AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-40 2.25", OffsetMax = "80 32.25",
                                        Align = TextAnchor.LowerLeft,
                                        IsBold = true,
                                        FontSize = 20,
                                        Color = new IColor("#e34026")
                                    },
                                    DayOfWeek = new TextSettings
                                    {
                                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-20 -20",
                                        OffsetMax = "120 0",
                                        // AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-40 -17.75", OffsetMax = "79 2.25",
                                        Align = TextAnchor.UpperLeft,
                                        IsBold = false,
                                        FontSize = 14,
                                        Color = new IColor("#e0d9d1", 50)
                                    }
                                },
                                ScrollPanel = new ScrollPanelUI
                                {
                                    Background = new ImageSettings
                                    {
                                        AnchorMin = "0 0",
                                        AnchorMax = "1 1",
                                        OffsetMin = "0 0",
                                        OffsetMax = "0 -66",
                                        Color = new IColor("#ffffff", 0f)
                                    },
                                    ScrollView = new ScrollViewUI
                                    {
                                        Scrollbar = new ScrollViewUI.ScrollBarSettings
                                        {
                                            AutoHide = true,
                                            Size = 3,
                                            HandleColor = new IColor("#D74933"),
                                            HighlightColor = new IColor("#D74933"),
                                            PressedColor = new IColor("#D74933"),
                                            HandleSprite = "assets/content/ui/UI.Background.TileTex.psd",
                                            TrackColor = new IColor("#38393F", 40),
                                            TrackSprite = "assets/content/ui/UI.Background.TileTex.psd"
                                        },
                                        ScrollType = ScrollType.Vertical,
                                        MovementType = ScrollRect.MovementType.Clamped,
                                        Elasticity = 0.1f,
                                        DecelerationRate = 1,
                                        ScrollSensitivity = 10,
                                        MinHeight = 210
                                    },
                                    ContentScroll = new ScrollPanelUI.ContentScrollUI
                                    {
                                        BackgroundContent = new ImageSettings
                                        {
                                            AnchorMin = "0 0",
                                            AnchorMax = "1 1",
                                            OffsetMin = "0 0",
                                            OffsetMax = "0 0",
                                            Color = new IColor("#696969", 30),
                                            Sprite = "assets/content/ui/UI.Background.Transparent.LinearLTR.tga"
                                        },
                                        Height = 40,
                                        MarginsInScroll = new MarginsInScrollUI(0, 5),
                                        IconTransform = new InterfacePosition
                                        {
                                            AnchorMin = "0 0.5",
                                            AnchorMax = "0 0.5",
                                            OffsetMin = "10 -10",
                                            OffsetMax = "30 10"
                                        },
                                        TimeEvent = new TextSettings
                                        {
                                            AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "40 -18",
                                            OffsetMax = "-40 0",
                                            Align = TextAnchor.LowerLeft,
                                            IsBold = true,
                                            FontSize = 11,
                                            Color = new IColor("#e0d9d1")
                                        },
                                        TitleEvent = new TextSettings
                                        {
                                            AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "40 0",
                                            OffsetMax = "-40 24",
                                            Align = TextAnchor.UpperLeft,
                                            IsBold = true,
                                            FontSize = 14,
                                            Color = new IColor("#e0d9d1", 50)
                                        },
                                        ButtonInfo = new ButtonSettings
                                        {
                                            AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-30 -10",
                                            OffsetMax = "-10 10",
                                            ButtonColor = new IColor("#ffffff", 0),
                                            ImageColor = new IColor("#ffffff"),
                                            Image = "https://i.ibb.co/M1zPJbp/5e3c035f6001.png"
                                        },
                                        PanelDescriptionLength = 250,
                                        PanelDescriptionHeight = 120,
                                        ColorSchedule = new IColor("#000000", 50),
                                        // ColorSchedule = new("#000000", 99),
                                        TitlePanelDescription = new ImageSettings
                                        {
                                            AnchorMin = "0 1",
                                            AnchorMax = "1 1",
                                            OffsetMin = "0 -20",
                                            OffsetMax = "2 0",
                                            Color = new IColor("#64A1CF"),
                                            Sprite = "assets/content/ui/UI.Background.TileTex.psd",
                                            Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                                        },
                                        TitleEventDescription = new TextSettings
                                        {
                                            AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 0", OffsetMax = "0 0",
                                            Align = TextAnchor.MiddleLeft,
                                            IsBold = true,
                                            FontSize = 14,
                                            Color = new IColor("#ffffff")
                                        },
                                        DescriptionPanelBackground = new ImageSettings
                                        {
                                            AnchorMin = "0 0",
                                            AnchorMax = "1 1",
                                            OffsetMin = "0 0",
                                            OffsetMax = "0 -20",
                                            Color = new IColor("#696969", 50),
                                            Sprite = "assets/content/ui/UI.Background.TileTex.psd",
                                            Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                                        },
                                        Description = new TextSettings
                                        {
                                            AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5",
                                            OffsetMax = "-5 -5",
                                            Align = TextAnchor.UpperLeft,
                                            IsBold = true,
                                            FontSize = 11,
                                            Color = new IColor("#999694")
                                        }
                                    }
                                }
                            },
                            ScheduleInfo = new PanelCalendarUI.PanelScheduleUI.ScheduleInfoUI
                            {
                                Background = new ImageSettings
                                {
                                    AnchorMin = "0 0",
                                    AnchorMax = "1 0",
                                    OffsetMin = "580 0",
                                    OffsetMax = "0 100",
                                    Color = new IColor("#ffffff", 0f)
                                },
                                BackgroundInfo = new ImageSettings
                                {
                                    AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -38", OffsetMax = "0 0",
                                    Color = new IColor("#71B8ED", 5),
                                    Sprite = "assets/content/ui/ui.background.tile.psd",
                                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                                },
                                Icon = new ImageSettings
                                {
                                    AnchorMin = "0 0",
                                    AnchorMax = "0 1",
                                    OffsetMin = "0 0",
                                    OffsetMax = "38 0",
                                    Color = new IColor("#ffffff"),
                                    Image = "https://i.ibb.co/rHBx0pN/icon-info-panel.png"
                                },
                                TextInfo = new TextSettings
                                {
                                    AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "50 0", OffsetMax = "0 0",
                                    Align = TextAnchor.MiddleLeft,
                                    IsBold = false,
                                    FontSize = 11,
                                    Color = new IColor("#64A1CF", 50)
                                },
                                BackgroundAnnotation = new ImageSettings
                                {
                                    AnchorMin = "0 0",
                                    AnchorMax = "1 1",
                                    OffsetMin = "0 0",
                                    OffsetMax = "0 -38",
                                    Color = new IColor("#ffffff", 0f)
                                },
                                SizeMarkerColor = new Vector2(14, 14),
                                Annotation = new TextSettings
                                {
                                    AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "5 -3", OffsetMax = "150 3",
                                    Align = TextAnchor.MiddleLeft,
                                    IsBold = true,
                                    FontSize = 12,
                                    Color = new IColor("#999694")
                                }
                            }
                        }
                    }
                };
            }

            public static UserInterfaceSetup GenerateUIMenuV1()
            {
                return new UserInterfaceSetup
                {
                    MainPanel = new ImageSettings
                    {
                        AnchorMin = "0.5 1",
                        AnchorMax = "0.5 1",
                        OffsetMin = "-600 -550",
                        OffsetMax = "600 0",
                        Color = new IColor("#ffffff", 0),
                        Sprite = "assets/content/ui/UI.Background.TileTex.psd"
                    },
                    HeaderPanel = new PanelHeaderUI
                    {
                        Background = new ImageSettings
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = "0 0",
                            OffsetMax = "0 0",
                            Color = new IColor("#ffffff", 0)
                        },
                        ShowHeader = false,
                        Header = new TextSettings
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = "0 0",
                            OffsetMax = "0 0",
                            Align = TextAnchor.UpperLeft,
                            IsBold = true,
                            FontSize = 32,
                            Color = new IColor("#ce432d", 90)
                        },
                        ShowLine = false,
                        Line = new ImageSettings
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 0",
                            OffsetMin = "0 -2",
                            OffsetMax = "0 0",
                            Color = new IColor("#373737", 50)
                        }
                    },
                    ButtonSetup = new ButtonSettings
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = "0 10",
                        OffsetMax = "160 40",
                        ButtonColor = new IColor("#252525"),
                        Align = TextAnchor.MiddleCenter,
                        Color = new IColor("#e0d9d1", 60),
                        FontSize = 14,
                        IsBold = true,
                        Sprite = "assets/content/ui/UI.Background.TileTex.psd"
                    },
                    PanelCalendar = new PanelCalendarUI
                    {
                        Background = new ImageSettings
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = "0 70",
                            OffsetMax = "-10 -10",
                            Color = new IColor("#ffffff", 0f)
                        },
                        DaysOfWeekPanel = new PanelCalendarUI.DayOfWeekPanelUI
                        {
                            Background = new ImageSettings
                            {
                                AnchorMin = "0 1",
                                AnchorMax = "1 1",
                                OffsetMin = "0 -38",
                                OffsetMax = "-356 0",
                                Color = new IColor("#696969", 20),
                                Sprite = "assets/content/ui/ui.background.tile.psd",
                                Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                            },
                            Title = new TextSettings
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                                OffsetMin = "0 0",
                                OffsetMax = "0 0",
                                Align = TextAnchor.MiddleCenter,
                                IsBold = true,
                                FontSize = 16,
                                Color = new IColor("#e0d9d1")
                            }
                        },
                        DaysTable = new PanelCalendarUI.DaysTableUI
                        {
                            Background = new ImageSettings
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                                OffsetMin = "0 0",
                                OffsetMax = "-356 -48",
                                Color = new IColor("#ffffff", 0f)
                            },
                            MarginsVertical = 2,
                            MarginsHorizontal = 1.5f,
                            BackgroundDayTableInside = new ButtonSettings
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                                OffsetMin = "0 0",
                                OffsetMax = "0 0",
                                ButtonColor = new IColor("#696969", 15),
                                Sprite = "assets/content/ui/ui.background.tile.psd",
                                Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                                Align = TextAnchor.MiddleCenter,
                                IsBold = true,
                                FontSize = 35,
                                Color = new IColor("#e0d9d1")
                            },
                            BackgroundDayTableOutside = new ButtonSettings
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                                OffsetMin = "0 0",
                                OffsetMax = "0 0",
                                ButtonColor = new IColor("#696969", 5),
                                Sprite = "assets/content/ui/ui.background.tile.psd",
                                Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                                Align = TextAnchor.MiddleCenter,
                                IsBold = true,
                                FontSize = 25,
                                Color = new IColor("#e0d9d1", 20)
                            }
                        },
                        PanelSchedule = new PanelCalendarUI.PanelScheduleUI
                        {
                            BackgroundMonth = new ImageSettings
                            {
                                AnchorMin = "1 1",
                                AnchorMax = "1 1",
                                OffsetMin = "-346 -38",
                                OffsetMax = "0 0",
                                Color = new IColor("#696969", 60),
                                Sprite = "assets/content/ui/UI.Background.TileTex.psd"
                            },
                            TitleMonth = new TextSettings
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                                OffsetMin = "0 0",
                                OffsetMax = "0 0",
                                Align = TextAnchor.MiddleCenter,
                                IsBold = true,
                                FontSize = 22,
                                Color = new IColor("#e0d9d1")
                            },
                            ButtonPreviousMonth = new ButtonSettings
                            {
                                AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "10 -8", OffsetMax = "23 8",
                                ButtonColor = new IColor("#ffffff", 0),
                                ImageColor = new IColor("#ffffff"),
                                Image = "https://i.ibb.co/F5cBPDm/be9d0d8d0f86.png"
                            },
                            ButtonNextMonth = new ButtonSettings
                            {
                                AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-23 -8", OffsetMax = "-10 8",
                                ButtonColor = new IColor("#ffffff", 0),
                                ImageColor = new IColor("#ffffff"),
                                Image = "https://i.ibb.co/3Tz8KmF/5f96eeaf05fa.png"
                            },
                            PanelShowDay = new PanelCalendarUI.PanelScheduleUI.PanelShowDayUI
                            {
                                Background = new ImageSettings
                                {
                                    AnchorMin = "1 0",
                                    AnchorMax = "1 1",
                                    OffsetMin = "-346 110",
                                    OffsetMax = "0 -48",
                                    Color = new IColor("#ffffff", 0f)
                                },
                                PanelHeader = new PanelCalendarUI.PanelScheduleUI.PanelShowDayUI.PanelHeaderUI
                                {
                                    Background = new ImageSettings
                                    {
                                        AnchorMin = "0 1",
                                        AnchorMax = "1 1",
                                        OffsetMin = "0 -60",
                                        OffsetMax = "0 -3",
                                        Color = new IColor("#ffffff", 50),
                                        Image = "https://i.ibb.co/8gHqy9p/banner.png"
                                        // Sprite = "assets/content/ui/ui.background.transparent.linear.psd"
                                    },
                                    ShowLine = true,
                                    Line = new ImageSettings
                                    {
                                        AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 3",
                                        Color = new IColor("#D74933")
                                    },
                                    Number = new TextSettings
                                    {
                                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-142.5 -40",
                                        OffsetMax = "-30 40",
                                        Align = TextAnchor.MiddleRight,
                                        IsBold = true,
                                        FontSize = 56,
                                        Color = new IColor("#e34026")
                                    },
                                    Month = new TextSettings
                                    {
                                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-20 0",
                                        OffsetMax = "120 40",
                                        // AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-40 2.25", OffsetMax = "80 32.25",
                                        Align = TextAnchor.LowerLeft,
                                        IsBold = true,
                                        FontSize = 20,
                                        Color = new IColor("#e34026")
                                    },
                                    DayOfWeek = new TextSettings
                                    {
                                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-20 -20",
                                        OffsetMax = "120 0",
                                        // AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-40 -17.75", OffsetMax = "79 2.25",
                                        Align = TextAnchor.UpperLeft,
                                        IsBold = false,
                                        FontSize = 14,
                                        Color = new IColor("#e0d9d1", 50)
                                    }
                                },
                                ScrollPanel = new ScrollPanelUI
                                {
                                    Background = new ImageSettings
                                    {
                                        AnchorMin = "0 0",
                                        AnchorMax = "1 1",
                                        OffsetMin = "0 0",
                                        OffsetMax = "0 -80",
                                        Color = new IColor("#ffffff", 0f)
                                    },
                                    ScrollView = new ScrollViewUI
                                    {
                                        Scrollbar = new ScrollViewUI.ScrollBarSettings
                                        {
                                            AutoHide = true,
                                            Size = 3,
                                            HandleColor = new IColor("#D74933"),
                                            HighlightColor = new IColor("#D74933"),
                                            PressedColor = new IColor("#D74933"),
                                            HandleSprite = "assets/content/ui/UI.Background.TileTex.psd",
                                            TrackColor = new IColor("#38393F", 40),
                                            TrackSprite = "assets/content/ui/UI.Background.TileTex.psd"
                                        },
                                        ScrollType = ScrollType.Vertical,
                                        MovementType = ScrollRect.MovementType.Clamped,
                                        Elasticity = 0.1f,
                                        DecelerationRate = 1,
                                        ScrollSensitivity = 10,
                                        MinHeight = 210
                                        // MinHeight = 290,
                                    },
                                    ContentScroll = new ScrollPanelUI.ContentScrollUI
                                    {
                                        BackgroundContent = new ImageSettings
                                        {
                                            AnchorMin = "0 0",
                                            AnchorMax = "1 1",
                                            OffsetMin = "0 0",
                                            OffsetMax = "0 0",
                                            Color = new IColor("#696969", 30),
                                            Sprite = "assets/content/ui/UI.Background.Transparent.LinearLTR.tga"
                                        },
                                        Height = 40,
                                        MarginsInScroll = new MarginsInScrollUI(0, 5),
                                        IconTransform = new InterfacePosition
                                        {
                                            AnchorMin = "0 0.5",
                                            AnchorMax = "0 0.5",
                                            OffsetMin = "10 -10",
                                            OffsetMax = "30 10"
                                        },
                                        TimeEvent = new TextSettings
                                        {
                                            AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "40 -18",
                                            OffsetMax = "-40 0",
                                            Align = TextAnchor.LowerLeft,
                                            IsBold = true,
                                            FontSize = 11,
                                            Color = new IColor("#e0d9d1")
                                        },
                                        TitleEvent = new TextSettings
                                        {
                                            AnchorMin = "0 0", AnchorMax = "1 0.5", OffsetMin = "40 0",
                                            OffsetMax = "-40 0",
                                            Align = TextAnchor.UpperLeft,
                                            IsBold = true,
                                            FontSize = 14,
                                            Color = new IColor("#e0d9d1", 50)
                                        },
                                        ButtonInfo = new ButtonSettings
                                        {
                                            AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-30 -10",
                                            OffsetMax = "-10 10",
                                            ButtonColor = new IColor("#ffffff", 0),
                                            ImageColor = new IColor("#ffffff"),
                                            Image = "https://i.ibb.co/M1zPJbp/5e3c035f6001.png"
                                        },
                                        PanelDescriptionLength = 250,
                                        PanelDescriptionHeight = 120,
                                        ColorSchedule = new IColor("#000000", 50),
                                        // ColorSchedule = new("#000000", 99),
                                        TitlePanelDescription = new ImageSettings
                                        {
                                            AnchorMin = "0 1",
                                            AnchorMax = "1 1",
                                            OffsetMin = "0 -20",
                                            OffsetMax = "2 0",
                                            Color = new IColor("#64A1CF"),
                                            Sprite = "assets/content/ui/UI.Background.TileTex.psd",
                                            Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                                        },
                                        TitleEventDescription = new TextSettings
                                        {
                                            AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 0", OffsetMax = "0 0",
                                            Align = TextAnchor.MiddleLeft,
                                            IsBold = true,
                                            FontSize = 14,
                                            Color = new IColor("#ffffff")
                                        },
                                        DescriptionPanelBackground = new ImageSettings
                                        {
                                            AnchorMin = "0 0",
                                            AnchorMax = "1 1",
                                            OffsetMin = "0 0",
                                            OffsetMax = "0 -20",
                                            Color = new IColor("#696969", 50),
                                            Sprite = "assets/content/ui/UI.Background.TileTex.psd",
                                            Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                                        },
                                        Description = new TextSettings
                                        {
                                            AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5",
                                            OffsetMax = "-5 -5",
                                            Align = TextAnchor.UpperLeft,
                                            IsBold = true,
                                            FontSize = 11,
                                            Color = new IColor("#999694")
                                        }
                                    }
                                }
                            },
                            ScheduleInfo = new PanelCalendarUI.PanelScheduleUI.ScheduleInfoUI
                            {
                                Background = new ImageSettings
                                {
                                    AnchorMin = "1 0",
                                    AnchorMax = "1 0",
                                    OffsetMin = "-346 0",
                                    OffsetMax = "0 100",
                                    Color = new IColor("#ffffff", 0f)
                                },
                                BackgroundInfo = new ImageSettings
                                {
                                    AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -38", OffsetMax = "0 0",
                                    Color = new IColor("#71B8ED", 5),
                                    Sprite = "assets/content/ui/ui.background.tile.psd",
                                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                                },
                                Icon = new ImageSettings
                                {
                                    AnchorMin = "0 0",
                                    AnchorMax = "0 1",
                                    OffsetMin = "0 0",
                                    OffsetMax = "38 0",
                                    Color = new IColor("#ffffff"),
                                    Image = "https://i.ibb.co/rHBx0pN/icon-info-panel.png"
                                },
                                TextInfo = new TextSettings
                                {
                                    AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "50 0", OffsetMax = "0 0",
                                    Align = TextAnchor.MiddleLeft,
                                    IsBold = false,
                                    FontSize = 11,
                                    Color = new IColor("#64A1CF", 50)
                                },
                                BackgroundAnnotation = new ImageSettings
                                {
                                    AnchorMin = "0 0",
                                    AnchorMax = "1 1",
                                    OffsetMin = "0 0",
                                    OffsetMax = "0 -38",
                                    Color = new IColor("#ffffff", 0f)
                                },
                                SizeMarkerColor = new Vector2(14, 14),
                                Annotation = new TextSettings
                                {
                                    AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "5 -3", OffsetMax = "150 3",
                                    Align = TextAnchor.MiddleLeft,
                                    IsBold = true,
                                    FontSize = 12,
                                    Color = new IColor("#999694")
                                }
                            }
                        }
                    }
                };
            }
        }

        private struct MarginsInScrollUI
        {
            [JsonProperty(LangRu ? "Слева - справа" : "Left - right")]
            public float x;

            [JsonProperty(LangRu ? "Между элементами" : "Between the elements")]
            public float y;

            public MarginsInScrollUI(float x, float y)
            {
                this.x = x;
                this.y = y;
            }
        }

        private class ButtonCloseSetup : InterfacePosition
        {
            [JsonProperty(PropertyName = "Background Button Close")]
            public ImageSettings BackgroundClose = new();

            [JsonProperty(PropertyName = "Color Button Close")]
            public IColor Color = IColor.CreateTransparent();

            [JsonProperty(PropertyName = "Sprite Button Close")]
            public string Sprite = string.Empty;

            [JsonProperty(PropertyName = "Material Button Close")]
            public string Material = string.Empty;
        }

        [JsonConverter(typeof(StringEnumConverter))]
        private enum TypeEvent
        {
            GlobalWipe,
            WipeMap,
            WipeBlock,
            EventManager,
            CustomEvent
        }

        [JsonConverter(typeof(StringEnumConverter))]
        private enum ModeRepeat
        {
            None = -1,
            Days = 0,
            Weeks = 1,
            Months = 2,
            Years = 3
        }

        [JsonConverter(typeof(StringEnumConverter))]
        private enum TypeSchedule
        {
            DateTime,
            CustomDate
        }

        #endregion

        #region HookMethods

        private void OnServerPanelClosed(BasePlayer player)
        {
            PlayerUIController.Remove(player);
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            PlayerUIController.Remove(player);
        }

#if !SCHEDULE_DEBUG
        private void AddEventManagerSchedule(List<(string, string, int hour, int minutes, bool[])> scheduleGlobalEvents)
        {
            if (scheduleGlobalEvents is not {Count: > 0}) return;

            ScheduleEventsManager.Clear();

            foreach (var newEvent in scheduleGlobalEvents)
            {
                var sch = new ScheduleToDay(TypeSchedule.CustomDate, TypeEvent.EventManager, newEvent.Item1)
                {
                    Descriptions = newEvent.Item2,
                    CustomDateTimeEvent = new CustomDateTime(ModeRepeat.Weeks, 0, 0, newEvent.Item5,
                        new MyTime(newEvent.Item3, newEvent.Item4), false, false)
                };

                ScheduleEventsManager.Add(sch);
            }
        }
#endif

        #endregion

        #region Methods

        private void CmdWipeSchedule(IPlayer cov, string command, string[] args)
        {
            var player = cov?.Object as BasePlayer;
            if (player == null) return;

            var container = PlayerUIController.GetContainer(player, PlayerUIController.TypeUI.FullScreen);
            CuiHelper.AddUi(player, container);
        }

        private CuiElementContainer API_OpenPlugin(BasePlayer player)
        {
            return PlayerUIController.GetContainer(player, PlayerUIController.TypeUI.ServerMenu);
        }

        [ConsoleCommand(Command)]
        private void CmdConsoleWipeSchedule(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !arg.HasArgs()) return;

            PlayerUIController.OnClickButton(player, arg.Args);
        }

        #endregion

        #region Utils

        private static CuiRectTransformComponent FullScreenTransform;

        private void RegisterPermissions()
        {
            if (!permission.PermissionExists(PermissionUse, this)) permission.RegisterPermission(PermissionUse, this);
        }

        private void RegisterCommands()
        {
            AddCovalenceCommand(config.Commands, nameof(CmdWipeSchedule));
        }

        private static string HexToCuiColor(string hex, float alpha = 100)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#FFFFFF";

            var str = hex.Trim('#');
            if (str.Length != 6) throw new Exception(hex);
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {alpha / 100}";
        }

        private static string GenerateDateOrdinalMessage(DateTime targetDate, bool useSmall = false)
        {
            var weekOfMonth = GetWeekOfMonth(targetDate);

            var dayOfWeek = targetDate.ToString("dddd", CultureInfo.InvariantCulture);

            return (useSmall ? "on" : "On") + $" the {weekOfMonth}{weekOfMonth.GetOrdinalSuffix()} {dayOfWeek} of";
        }

        private static int GetWeekOfMonth(DateTime date)
        {
            return (date.Day - 1) / 7 + 1;
        }

        #region Working with Images

# if CARBON
			private ImageDatabaseModule imageDatabase;
# endif

        internal bool AddImage(string url, string fileName, ulong id = 0)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(fileName)) return false;
# if CARBON
				imageDatabase.Queue(true, new Dictionary<string, string>
				{
					[fileName] = url
				});
				return true;
# else
            return Convert.ToBoolean(ImageLibrary?.Call("AddImage", url, fileName, id));
# endif
        }

        internal string GetImage(string name, ulong imageId = 0, bool returnUrl = false)
        {
# if CARBON
				return imageDatabase.GetImageString(name);
# else
            return Convert.ToString(ImageLibrary?.Call("GetImage", name, imageId, returnUrl));
# endif
        }

        internal bool HasImage(string name, ulong imageId = 0)
        {
# if CARBON
				return Convert.ToBoolean(imageDatabase.HasImage(name));
# else
            return Convert.ToBoolean(ImageLibrary?.Call("HasImage", name, imageId));
# endif
        }

        internal string GetPng(string url)
        {
            return HasImage(url) || AddImage(url, url) ? GetImage(url) : url;
        }

        private void LoadImages()
        {
# if CARBON
				imageDatabase = BaseModule.GetModule<ImageDatabaseModule>();
#endif

            var imagesList = new Dictionary<string, string>();

            if (!ServerPanel)
            {
                imagesList.TryAdd("ServerPanel_Editor_Switch_On",
                    "https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/ServerPanel/serverpanel-editor-icon-switch-on.png");
                imagesList.TryAdd("ServerPanel_Editor_Switch_Off",
                    "https://gitlab.com/TheMevent/PluginsStorage/raw/main/Images/ServerPanel/serverpanel-editor-icon-switch-off.png");
            }

            imagesList.TryAdd("WipeSchedule_Button_Add", "https://i.ibb.co/zNH6yF4/e89542bdb416.png");
            imagesList.TryAdd("WipeSchedule_Icon_URL", "https://i.ibb.co/NCfwpWf/0b86a7cff47d.png");
            imagesList.TryAdd("WipeSchedule_CheckBox_On", "https://i.ibb.co/vHQJ8YX/5187797db3f6.png");
            imagesList.TryAdd("WipeSchedule_CheckBox_Off", "https://i.ibb.co/r0LGNfr/7f58ba40b25a.png");

            var imageSettingsList = Pool.Get<List<ImageSettings>>();

            try
            {
                UIFullScreen.ExtractObjects(imageSettingsList);
                UIMenu.ExtractObjects(imageSettingsList);

                foreach (var image in imageSettingsList)
                    if (!string.IsNullOrEmpty(image.Image) && image.Image.StartsWith("http") &&
                        !imagesList.ContainsKey(image.Image)
                       )
                        imagesList.TryAdd(image.Image, image.Image);
            }
            finally
            {
                Pool.FreeUnmanaged(ref imageSettingsList);
            }


#if CARBON
	            imageDatabase.Queue(false, imagesList);
#else
            timer.In(1f, () =>
            {
                if (ImageLibrary is not {IsLoaded: true})
                {
                    BroadcastILNotInstalled();
                    return;
                }

                ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
            });
#endif
        }

        private void BroadcastILNotInstalled()
        {
            for (var i = 0; i < 5; i++) PrintError("IMAGE LIBRARY IS NOT INSTALLED.");
        }

        #endregion

        #endregion

        #region Classes Plugin

        private const float
            UI_ADMIN_SETTINGS_FIELD_HEIGHT = 45f,
            UI_ADMIN_SETTINGS_FIELD_WIDTH = 200f,
            UI_ADMIN_SETTINGS_FIELD_MARGIN_Y = 20f,
            UI_ADMIN_SETTINGS_FIELD_MARGIN_X = 50f,
            UI_ADMIN_SETTINGS_FIELD_INDENT_Y = 0f,
            UI_ADMIN_SETTINGS_FIELD_INDENT_X = 33f,
            UI_ADMIN_SETTINGS_DROPDOWN_ELEMENT_HEIGHT = 30f,
            UI_ADMIN_SETTINGS_DROPDOWN_ELEMENT_MARGIN_Y = 5f,
            UI_ADMIN_SETTINGS_DROPDOWN_ELEMENT_INDENT_Y = 0f;

        private class AdminUIController : PlayerUIController
        {
            private static List<ScheduleToDay> allSchedule = new();
            private int selectedIndex = -1;
            private ScheduleToDay selectedSchedule;
            private ScheduleToDay newEvent;

            private TypeEvent[] typesEventUses = new TypeEvent[4]
                {TypeEvent.CustomEvent, TypeEvent.GlobalWipe, TypeEvent.WipeMap, TypeEvent.WipeBlock};

            private int selectedIndexTypeEvent;
            private int selectedIndexTypeSchedule;
            private int selectedIndexModeRepeat;
            private string selectedNameEvent, selectedDescription, selectedColorEvent, selectedUrl;
            private ImageSettings selectedIcon;
            private bool selectedUseChangeDefaultIcon, selectedUseChangeDefaultColor;
            private IColor selectedColor;
            private int inputDay, inputMonth, inputYear, inputHours, inputMinutes;
            private int inputInterval, inputWeeksInterval;
            private bool monthRepeatOnDay, monthRepeatOnDayOfWeek;

            private bool[] weekEnabled = new bool[7];

            public AdminUIController(BasePlayer player, TypeUI typeUI) : base(player, typeUI)
            {
                UpdateSchedule();

                OwnerContainer.AddRange(setupPanel.ButtonSetup.GetButton(
                    GetMessage(SetupSchedule, owner.UserIDString), Command + " " + "setup.schedule", LayerGeneral,
                    Layer + ".ButtonSetup"));
            }

            private void AddOwnerEditPanel()
            {
                #region Background

                OwnerContainer.Add(new CuiElement
                {
                    Parent = "Overlay",
                    Name = LayerGeneral + ".SetupSchedule",
                    DestroyUi = LayerGeneral + ".SetupSchedule",
                    Components =
                    {
                        FullScreenTransform,
                        new CuiImageComponent
                        {
                            Color = "0 0 0 0.5",
                            ImageType = Image.Type.Tiled,
                            // Sprite = "assets/content/ui/UI.Background.Tile.psd",
                            Material = "assets/content/ui/uibackgroundblur.mat"
                        }
                    }
                });

                OwnerContainer.Add(new CuiElement
                {
                    Parent = LayerGeneral + ".SetupSchedule",
                    Name = LayerGeneral + ".SetupSchedule.Background",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = new IColor("#191919", 90).Get(),
                            Sprite = "assets/content/ui/UI.Background.TileTex.psd",
                            Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-440 -280",
                            OffsetMax = "440 280"
                        }
                    }
                });

                #endregion

                #region Header

                OwnerContainer.Add(new CuiElement
                {
                    Parent = LayerGeneral + ".SetupSchedule.Background",
                    Name = LayerGeneral + ".SetupSchedule.Title.Background",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = new IColor("#494949").Get(),
                            Sprite = "assets/content/ui/UI.Background.Transparent.LinearLTR.tga"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = "0 -40",
                            OffsetMax = "0 0"
                        }
                    }
                });

                OwnerContainer.Add(new CuiElement
                {
                    Parent = LayerGeneral + ".SetupSchedule.Title.Background",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMessage(SetupScheduleTitle, owner.UserIDString),
                            Align = TextAnchor.MiddleLeft,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 18,
                            Color = new IColor("#E2DBD3").Get()
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = "20 0",
                            OffsetMax = "0 0"
                        }
                    }
                });

                #endregion

                #region Close

                OwnerContainer.Add(new CuiElement
                {
                    Parent = LayerGeneral + ".SetupSchedule.Background",
                    Name = LayerGeneral + ".button.close.setup.background",
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Command = Command + " " + "close.setup",
                            Color = new IColor("#E44028").Get(),
                            Sprite = "assets/content/ui/UI.Background.TileTex.psd"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "1 1",
                            AnchorMax = "1 1",
                            OffsetMin = "-40 -40",
                            OffsetMax = "0 0"
                        }
                    }
                });

                OwnerContainer.Add(new CuiElement
                {
                    Parent = LayerGeneral + ".button.close.setup.background",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = new IColor("#FFFFFF").Get(),
                            Sprite = "assets/icons/close.png"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = "10 10",
                            OffsetMax = "-10 -10"
                        }
                    }
                });

                #endregion

                #region Setup Event

                OwnerContainer.Add(new CuiElement
                {
                    Parent = LayerGeneral + ".SetupSchedule.Background",
                    Name = LayerGeneral + ".PaneSetupEvent.Background",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = new IColor("#292929", 60).Get(),
                            Sprite = "assets/content/ui/UI.Background.Transparent.LinearLTR.tga"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = "280 15",
                            OffsetMax = "-15 -55"
                        }
                    }
                });

                #region Header

                OwnerContainer.Add(new CuiElement
                {
                    Name = LayerGeneral + ".PaneSetupEvent.Title.text",
                    Parent = LayerGeneral + ".PaneSetupEvent.Background",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = new IColor("#ffffff", 0).Get()
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = "0 -50",
                            OffsetMax = "0 0"
                        }
                    }
                });

                OwnerContainer.Add(new CuiElement
                {
                    Parent = LayerGeneral + ".PaneSetupEvent.Title.text",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMessage(SetupEventTitle, owner.UserIDString),
                            Align = TextAnchor.MiddleLeft,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 32,
                            Color = new IColor("#CF432D", 90).Get()
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = "20 0",
                            OffsetMax = "0 0"
                        }
                    }
                });

                #endregion

                #endregion
            }

            private void AddEditScheduleUI()
            {
                var count = allSchedule.Count;

                float margins_x = 10, margins_y = 5, h = 40;
                var height = h * (count + 1) + margins_y * (count + 1) + margins_y;
                if (height < 480)
                    height = 480;

                #region Scroll View

                CuiRectTransformComponent transformContent = new()
                    {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -{height}", OffsetMax = "0 0"};

                OwnerContainer.Add(new CuiElement
                {
                    Parent = LayerGeneral + ".SetupSchedule.Background",
                    Name = LayerGeneral + ".Scroll.Background",
                    DestroyUi = LayerGeneral + ".Scroll.Background",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = new IColor("#292929", 20).Get(),
                            Material = "assets/content/ui/uibackgroundblur.mat"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 1",
                            OffsetMin = "15 15",
                            OffsetMax = "270 -55"
                        }
                    }
                });

                OwnerContainer.Add(new CuiElement
                {
                    Parent = LayerGeneral + ".Scroll.Background",
                    Name = LayerGeneral + ".Scroll.Schedule.Content",
                    Components =
                    {
                        new CuiScrollViewComponent
                        {
                            MovementType = ScrollRect.MovementType.Clamped,
                            Vertical = true,
                            Inertia = true,
                            Horizontal = false,
                            Elasticity = 0.25f,
                            DecelerationRate = 0.3f,
                            ScrollSensitivity = 24f,
                            ContentTransform = transformContent,
                            VerticalScrollbar = new CuiScrollbar
                            {
                                AutoHide = true,
                                Size = 3,
                                HandleColor = HexToCuiColor("#D74933"),
                                HighlightColor = HexToCuiColor("#D74933"),
                                PressedColor = HexToCuiColor("#D74933"),
                                HandleSprite = "assets/content/ui/UI.Background.TileTex.psd",
                                TrackColor = HexToCuiColor("#38393F", 40),
                                TrackSprite = "assets/content/ui/UI.Background.TileTex.psd"
                            }
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"}
                    }
                });

                #endregion

                #region Events List

                for (var i = 0; i < allSchedule.Count; i++)
                {
                    OwnerContainer.Add(new CuiElement
                    {
                        Parent = LayerGeneral + ".Scroll.Schedule.Content",
                        Name = LayerGeneral + $".Scroll.Content.Event.{i}.Background",
                        DestroyUi = LayerGeneral + $".Scroll.Content.Event.{i}.Background",
                        Components =
                        {
                            new CuiButtonComponent
                            {
                                Command = Command + " " + $".use.event {i}",
                                Color = "0 0 0 0"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 1", AnchorMax = "1 1",
                                OffsetMin = $"{margins_x} -{h * i + margins_y * i + h + margins_y}",
                                OffsetMax = $"-{margins_x} -{h * i + margins_y * i + margins_y}"
                            }
                        }
                    });

                    EditableScheduleFieldUI(i);
                }

                #endregion

                #region Button.Add

                OwnerContainer.Add(new CuiElement
                {
                    Parent = LayerGeneral + ".Scroll.Schedule.Content",
                    Name = LayerGeneral + ".Scroll.Content.AddButton",
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Command = Command + " add.event",
                            Color = new IColor("#494949", 60).Get(),
                            Sprite = "assets/content/ui/UI.Background.Transparent.LinearLTR.tga"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "1 1",
                            OffsetMin = $"{margins_x} -{h * count + margins_y * count + h + margins_y}",
                            OffsetMax = $"-{margins_x} -{h * count + margins_y * count + margins_y}"
                        }
                    }
                });

                OwnerContainer.Add(new CuiElement
                {
                    Parent = LayerGeneral + ".Scroll.Content.AddButton",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = Instance.GetImage("WipeSchedule_Button_Add")
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0.5",
                            AnchorMax = "0 0.5",
                            OffsetMin = "10 -10",
                            OffsetMax = "30 10"
                        }
                    }
                });

                OwnerContainer.Add(new CuiElement
                {
                    Parent = LayerGeneral + ".Scroll.Content.AddButton",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMessage(ADD_EVENT_BUTTON, owner.UserIDString),
                            Align = TextAnchor.MiddleLeft,
                            Color = new IColor("#e0d9d1", 60).Get(),
                            FontSize = 14,
                            Font = "robotocondensed-bold.ttf"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = "40 0",
                            OffsetMax = "0 0"
                        }
                    }
                });

                #endregion
            }

            private void EditableScheduleFieldUI(int index)
            {
                var targetSchedule = allSchedule[index];

                OwnerContainer.Add(new CuiElement
                {
                    Parent = LayerGeneral + $".Scroll.Content.Event.{index}.Background",
                    Name = LayerGeneral + $".Scroll.Content.Event.{index}",
                    DestroyUi = LayerGeneral + $".Scroll.Content.Event.{index}",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color =
                                index == selectedIndex ? HexToCuiColor("#CF432D", 80) : HexToCuiColor("#494949", 60),
                            Sprite = "assets/content/ui/UI.Background.Transparent.LinearLTR.tga"
                        },
                        new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                    }
                });

                #region Icon

                var imageComponent =
                    !string.IsNullOrEmpty(targetSchedule.UrlIcon) && targetSchedule.UrlIcon.StartsWith("http")
                        ? new CuiRawImageComponent {Color = "1 1 1 1", Png = Instance.GetPng(targetSchedule.UrlIcon)}
                        : Instance.IconSetup.TryGetValue(targetSchedule.EventType, out var icon)
                            ? icon.GetImageComponent()
                            : null;

                if (imageComponent != null)
                    OwnerContainer.Add(new CuiElement
                    {
                        Parent = LayerGeneral + $".Scroll.Content.Event.{index}",
                        Name = LayerGeneral + $".Scroll.Content.Event.Icon.{index}",
                        Components =
                        {
                            imageComponent,
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0.5",
                                AnchorMax = "0 0.5",
                                OffsetMin = "10 -10",
                                OffsetMax = "30 10"
                            }
                        }
                    });

                #endregion

                OwnerContainer.Add(new CuiElement
                {
                    Parent = LayerGeneral + $".Scroll.Content.Event.{index}",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = targetSchedule.ScheduleType == TypeSchedule.CustomDate
                                ? targetSchedule.CustomDateTimeEvent.Times.ToStringTime_HM()
                                : targetSchedule.DateTimeEvent.ToStringDateTime_DMYHM(),
                            Align = TextAnchor.MiddleLeft,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 14,
                            Color = new IColor("#e0d9d1").Get()
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0.5", AnchorMax = "1 1", OffsetMin = "40 0", OffsetMax = "-40 0"
                        }
                    }
                });

                OwnerContainer.Add(new CuiElement
                {
                    Parent = LayerGeneral + $".Scroll.Content.Event.{index}",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMessage(targetSchedule.EventType.ToString(), owner.UserIDString),
                            Align = TextAnchor.UpperLeft,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 14,
                            Color = new IColor("#e0d9d1", 50).Get()
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 0.5", OffsetMin = "40 0", OffsetMax = "-40 0"
                        }
                    }
                });

                if (targetSchedule.EventType != TypeEvent.EventManager)
                {
                    OwnerContainer.Add(new CuiElement
                    {
                        Parent = LayerGeneral + $".Scroll.Content.Event.{index}",
                        Name = LayerGeneral + $".Scroll.Content.Event.switch.{index}",
                        DestroyUi = LayerGeneral + $".Scroll.Content.Event.switch.{index}",
                        Components =
                        {
                            new CuiButtonComponent
                            {
                                Color = "0 0 0 0",
                                Command = Command + " " + $".enable {index}"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "1 0.5", AnchorMax = "1 0.5",
                                OffsetMin = "-40 -8", OffsetMax = "0 8"
                            }
                        }
                    });

                    OwnerContainer.Add(new CuiElement
                    {
                        Parent = LayerGeneral + $".Scroll.Content.Event.switch.{index}",
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = Instance.GetImage(targetSchedule.Enable
                                    ? "ServerPanel_Editor_Switch_On"
                                    : "ServerPanel_Editor_Switch_Off")
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1"
                            }
                        }
                    });
                }
            }

            private void AddEditPanelUI()
            {
                OwnerContainer.Add(new CuiElement
                {
                    Parent = LayerGeneral + ".PaneSetupEvent.Background",
                    Name = LayerGeneral + ".EditorPanel",
                    DestroyUi = LayerGeneral + ".EditorPanel",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0 0 0 0"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = "0 0",
                            OffsetMax = "0 -50"
                        }
                    }
                });

                if (selectedIndex < 0)
                {
                    OwnerContainer.Add(new CuiElement
                    {
                        Parent = LayerGeneral + ".EditorPanel",
                        Name = LayerGeneral + ".EditorPanel.notify",
                        DestroyUi = LayerGeneral + ".EditorPanel.notify",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = GetMessage(NotifySelection, owner.UserIDString),
                                Align = TextAnchor.UpperLeft,
                                Font = "robotocondensed-regular.ttf",
                                FontSize = 18,
                                Color = new IColor("#E2DBD3").Get()
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 1",
                                AnchorMax = "1 1",
                                OffsetMin = "20 -50",
                                OffsetMax = "0 0"
                            }
                        }
                    });
                }
                else
                {
                    if (selectedIndexTypeEvent < 0)
                    {
                        OwnerContainer.Add(new CuiElement
                        {
                            Parent = LayerGeneral + ".EditorPanel",
                            Name = LayerGeneral + ".EditorPanel.notify",
                            DestroyUi = LayerGeneral + ".EditorPanel.notify",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = GetMessage(NotifyNotEditTypeSelection, owner.UserIDString),
                                    Align = TextAnchor.UpperLeft,
                                    Font = "robotocondensed-regular.ttf",
                                    FontSize = 18,
                                    Color = new IColor("#E2DBD3").Get()
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 1",
                                    AnchorMax = "1 1",
                                    OffsetMin = "0 -100",
                                    OffsetMax = "0 -50"
                                }
                            }
                        });

                        OwnerContainer.Add(new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin = "1 0",
                                AnchorMax = "1 0",
                                OffsetMin = "-320 5",
                                OffsetMax = "-170 35"
                            },
                            Text =
                            {
                                Text = GetMessage(SAVE_EVENT_BUTTON, owner.UserIDString),
                                Align = TextAnchor.MiddleCenter,
                                Color = new IColor("#E2DBD3", 90).Get(),
                                FontSize = 14,
                                Font = "robotocondensed-bold.ttf"
                            },
                            Button =
                            {
                                Color = new IColor("#005FB7").Get(),
                                Command = Command + " " + $"save.event {selectedIndex}",
                                Sprite = "assets/content/ui/UI.Background.TileTex.psd"
                            }
                        }, LayerGeneral + ".EditorPanel", LayerGeneral + ".EditorPanel.Button.save");
                    }
                    else
                    {
                        if (selectedSchedule != null)
                        {
                            OwnerContainer.Add(new CuiButton
                            {
                                RectTransform =
                                {
                                    AnchorMin = "1 0",
                                    AnchorMax = "1 0",
                                    OffsetMin = "-155 5",
                                    OffsetMax = "-5 35"
                                },
                                Text =
                                {
                                    Text = GetMessage(DELETE_EVENT_BUTTON, owner.UserIDString),
                                    Align = TextAnchor.MiddleCenter,
                                    Color = new IColor("#e0d9d1", 60).Get(),
                                    FontSize = 14,
                                    Font = "robotocondensed-bold.ttf"
                                },
                                Button =
                                {
                                    Color = new IColor("#252525").Get(),
                                    Command = Command + " " + $"delete.event {selectedIndex}",
                                    Sprite = "assets/content/ui/UI.Background.TileTex.psd"
                                }
                            }, LayerGeneral + ".EditorPanel", LayerGeneral + ".EditorPanel.Button.delete");

                            OwnerContainer.Add(new CuiButton
                            {
                                RectTransform =
                                {
                                    AnchorMin = "1 0",
                                    AnchorMax = "1 0",
                                    OffsetMin = "-320 5",
                                    OffsetMax = "-170 35"
                                },
                                Text =
                                {
                                    Text = GetMessage(SAVE_EVENT_BUTTON, owner.UserIDString),
                                    Align = TextAnchor.MiddleCenter,
                                    Color = new IColor("#E2DBD3", 90).Get(),
                                    FontSize = 14,
                                    Font = "robotocondensed-bold.ttf"
                                },
                                Button =
                                {
                                    Color = new IColor("#005FB7").Get(),
                                    Command = Command + " " + $"save.event {selectedIndex}",
                                    Sprite = "assets/content/ui/UI.Background.TileTex.psd"
                                }
                            }, LayerGeneral + ".EditorPanel", LayerGeneral + ".EditorPanel.Button.save");
                        }
                    }
                }

                if (selectedIndex < 0)
                {
                    // ignore
                }
                else
                {
                    if (selectedIndexTypeEvent < 0)
                    {
                        // ignore
                    }
                    else
                    {
                        if (selectedSchedule != null)
                        {
                            var offsetY = -UI_ADMIN_SETTINGS_FIELD_INDENT_Y;
                            var offsetX = UI_ADMIN_SETTINGS_FIELD_INDENT_X;

                            #region SwitchEventType

                            OwnerContainer.Add(new CuiPanel
                            {
                                RectTransform =
                                {
                                    AnchorMin = "0 1",
                                    AnchorMax = "0 1",
                                    OffsetMin = $"{offsetX} {offsetY - UI_ADMIN_SETTINGS_FIELD_HEIGHT}",
                                    OffsetMax = $"{offsetX + UI_ADMIN_SETTINGS_FIELD_WIDTH} {offsetY}"
                                },
                                Image =
                                {
                                    Color = "0 0 0 0"
                                }
                            }, LayerGeneral + ".EditorPanel", LayerGeneral + ".switch.type_event");

                            AddSettingField(1, LayerGeneral + ".switch.type_event",
                                SwitchEventType, Command + " " + ".type.event",
                                typesEventUses[selectedIndexTypeEvent].ToString());

                            #endregion

                            offsetY = offsetY - UI_ADMIN_SETTINGS_FIELD_HEIGHT - UI_ADMIN_SETTINGS_FIELD_MARGIN_Y;

                            #region TitleInputName

                            OwnerContainer.Add(new CuiPanel
                            {
                                RectTransform =
                                {
                                    AnchorMin = "0 1",
                                    AnchorMax = "0 1",
                                    OffsetMin = $"{offsetX} {offsetY - UI_ADMIN_SETTINGS_FIELD_HEIGHT}",
                                    OffsetMax = $"{offsetX + UI_ADMIN_SETTINGS_FIELD_WIDTH} {offsetY}"
                                },
                                Image =
                                {
                                    Color = "0 0 0 0"
                                }
                            }, LayerGeneral + ".EditorPanel", LayerGeneral + ".switch.name_event");

                            AddSettingField(0, LayerGeneral + ".switch.name_event",
                                TitleInputName, Command + " " + ".input.name",
                                selectedNameEvent);

                            #endregion

                            offsetY = offsetY - UI_ADMIN_SETTINGS_FIELD_HEIGHT - UI_ADMIN_SETTINGS_FIELD_MARGIN_Y;

                            #region TitleInputDescription

                            OwnerContainer.Add(new CuiPanel
                            {
                                RectTransform =
                                {
                                    AnchorMin = "0 1",
                                    AnchorMax = "0 1",
                                    OffsetMin = $"{offsetX} {offsetY - UI_ADMIN_SETTINGS_FIELD_HEIGHT}",
                                    OffsetMax = $"{offsetX + UI_ADMIN_SETTINGS_FIELD_WIDTH} {offsetY}"
                                },
                                Image =
                                {
                                    Color = "0 0 0 0"
                                }
                            }, LayerGeneral + ".EditorPanel", LayerGeneral + ".switch.description_event");

                            AddSettingField(0, LayerGeneral + ".switch.description_event",
                                TitleInputDescription, Command + " " + ".input.description",
                                selectedDescription);

                            #endregion

                            offsetY = offsetY - UI_ADMIN_SETTINGS_FIELD_HEIGHT - UI_ADMIN_SETTINGS_FIELD_MARGIN_Y;

                            #region IconSettings

                            OwnerContainer.Add(new CuiPanel
                            {
                                RectTransform =
                                {
                                    AnchorMin = "0 1",
                                    AnchorMax = "0 1",
                                    OffsetMin = $"{offsetX} {offsetY - 90}",
                                    OffsetMax = $"{offsetX + UI_ADMIN_SETTINGS_FIELD_WIDTH} {offsetY}"
                                },
                                Image =
                                {
                                    Color = "0 0 0 0"
                                }
                            }, LayerGeneral + ".EditorPanel", LayerGeneral + ".switch.icon_settings");

                            AddSettingField(3, LayerGeneral + ".switch.icon_settings",
                                "ICON SETTINGS", Command + " .input.url",
                                selectedIcon,
                                selectedUseChangeDefaultIcon);

                            #endregion

                            offsetY = offsetY - 90f - UI_ADMIN_SETTINGS_FIELD_MARGIN_Y;

                            #region ColorEdit

                            OwnerContainer.Add(new CuiPanel
                            {
                                RectTransform =
                                {
                                    AnchorMin = "0 1",
                                    AnchorMax = "0 1",
                                    OffsetMin = $"{offsetX} {offsetY - UI_ADMIN_SETTINGS_FIELD_HEIGHT}",
                                    OffsetMax = $"{offsetX + UI_ADMIN_SETTINGS_FIELD_WIDTH} {offsetY}"
                                },
                                Image =
                                {
                                    Color = "0 0 0 0"
                                }
                            }, LayerGeneral + ".EditorPanel", LayerGeneral + ".switch.color_edit");

                            AddSettingField(2, LayerGeneral + ".switch.color_edit",
                                AddCustomColorEvent, Command + " " + ".input.color start",
                                selectedColor);

                            #endregion

                            offsetY = -UI_ADMIN_SETTINGS_FIELD_INDENT_Y;
                            offsetX = offsetX + UI_ADMIN_SETTINGS_FIELD_WIDTH + UI_ADMIN_SETTINGS_FIELD_MARGIN_X;

                            #region DateTime

                            OwnerContainer.Add(new CuiPanel
                            {
                                Image = {Color = "0 0 0 0"},
                                RectTransform =
                                {
                                    AnchorMin = "0 1", AnchorMax = "0 1",
                                    OffsetMin = $"{offsetX} {offsetY - 45}", OffsetMax = $"{offsetX + 200} {offsetY}"
                                }
                            }, LayerGeneral + ".EditorPanel", LayerGeneral + ".switch.date_time");

                            #region Titles

                            OwnerContainer.Add(new CuiElement
                            {
                                Parent = LayerGeneral + ".switch.date_time",
                                Components =
                                {
                                    new CuiTextComponent
                                    {
                                        Text = "DATE", Font = "robotocondensed-bold.ttf", FontSize = 10,
                                        Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#E2DBD3", 90)
                                    },
                                    new CuiRectTransformComponent
                                        {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -15", OffsetMax = "-75 0"}
                                }
                            });

                            OwnerContainer.Add(new CuiElement
                            {
                                Parent = LayerGeneral + ".switch.date_time",
                                Components =
                                {
                                    new CuiTextComponent
                                    {
                                        Text = "TIME", Font = "robotocondensed-bold.ttf", FontSize = 10,
                                        Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#E2DBD3", 90)
                                    },
                                    new CuiRectTransformComponent
                                        {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "135 -15", OffsetMax = "0 0"}
                                }
                            });

                            #endregion

                            #region Elements

                            DateTimeElement("day", "0 0", "30 30");
                            DateTimeElement("month", "35 0", "65 30");
                            DateTimeElement("year", "70 0", "125 30");

                            DateTimeElement("hour", "135 0", "165 30");
                            DateTimeElement("minutes", "170 0", "200 30");

                            #endregion

                            void DateTimeElement(string type, string offsetMin, string offsetMax)
                            {
                                OwnerContainer.Add(new CuiPanel
                                    {
                                        Image = {Color = "0 0 0 0.7"},
                                        RectTransform =
                                        {
                                            AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = offsetMin,
                                            OffsetMax = offsetMax
                                        }
                                    }, LayerGeneral + ".switch.date_time",
                                    LayerGeneral + ".switch.date_time" + ".param." + type);

                                DateTimeElementValue(type);
                            }

                            #endregion

                            offsetY = offsetY - 45f - UI_ADMIN_SETTINGS_FIELD_MARGIN_Y;

                            #region Repeat Every

                            OwnerContainer.Add(new CuiPanel
                            {
                                Image = {Color = "0 0 0 0"},
                                RectTransform =
                                {
                                    AnchorMin = "0 1", AnchorMax = "0 1",
                                    OffsetMin = $"{offsetX} {offsetY - 45}", OffsetMax = $"{offsetX + 200} {offsetY}"
                                }
                            }, LayerGeneral + ".EditorPanel", LayerGeneral + ".switch.type_schedule");

                            OwnerContainer.Add(new CuiElement
                            {
                                Parent = LayerGeneral + ".switch.type_schedule",
                                Components =
                                {
                                    new CuiTextComponent
                                    {
                                        Text = "REPEAT EVERY", Font = "robotocondensed-bold.ttf", FontSize = 10,
                                        Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#E2DBD3", 90)
                                    },
                                    new CuiRectTransformComponent
                                        {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -15", OffsetMax = "0 0"}
                                }
                            });

                            OwnerContainer.Add(new CuiPanel
                                {
                                    CursorEnabled = false,
                                    Image = {Color = HexToCuiColor("#000000", 70)},
                                    RectTransform =
                                        {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "-30 30"}
                                }, LayerGeneral + ".switch.type_schedule",
                                LayerGeneral + ".switch.type_schedule" + ".value.bg");

                            DrawToggleButton();

                            #endregion

                            offsetY = offsetY - 45f - UI_ADMIN_SETTINGS_FIELD_MARGIN_Y;

                            #region Repeat Params

                            OwnerContainer.Add(new CuiPanel
                                {
                                    Image = {Color = "0 0 0 0"},
                                    RectTransform =
                                    {
                                        AnchorMin = "0 1", AnchorMax = "0 1",
                                        OffsetMin = $"{offsetX} {offsetY - 45}",
                                        OffsetMax = $"{offsetX + 200} {offsetY}"
                                    }
                                }, LayerGeneral + ".EditorPanel", LayerGeneral + ".switch.repeat_params.background",
                                LayerGeneral + ".switch.repeat_params.background");

                            UpdateRepeatParams();

                            #endregion
                        }
                    }
                }
            }

            private void AddColorSelectionPanel()
            {
                #region Background

                var bgLayer = OwnerContainer.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1"
                    },
                    Image =
                    {
                        Color = HexToCuiColor("#000000", 98)
                    }
                }, "Overlay", LayerGeneral + ".color.selector", LayerGeneral + ".color.selector");

                #endregion

                #region Main

                var mainLayer = OwnerContainer.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                        OffsetMin = "-240 -260",
                        OffsetMax = "240 260"
                    },
                    Image =
                    {
                        Color = HexToCuiColor("#202224")
                    }
                }, bgLayer, LayerGeneral + ".color.selector" + ".Main", LayerGeneral + ".color.selector" + ".Main");

                #endregion

                #region Header

                #region Title

                OwnerContainer.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "1 1",
                        OffsetMin = "0 0",
                        OffsetMax = "0 40"
                    },
                    Text =
                    {
                        Text = "COLOR PICKER",
                        Align = TextAnchor.MiddleLeft,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 22,
                        Color = HexToCuiColor("#DCDCDC")
                    }
                }, mainLayer);

                #endregion

                #region Close

                OwnerContainer.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "1 1", AnchorMax = "1 1",
                        OffsetMin = "-30 5",
                        OffsetMax = "0 35"
                    },
                    Text =
                    {
                        Text = "X",
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 22,
                        Color = HexToCuiColor("#EF5125")
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Close = bgLayer,
                        Command = Command + " .input.color close"
                    }
                }, mainLayer, mainLayer + ".BTN.Close.Edit");

                #endregion

                #endregion

                #region Colors

                var topRightColor = Color.blue;
                var bottomRightColor = Color.green;
                var topLeftColor = Color.red;
                var bottomLeftColor = Color.yellow;

                var scale = 20f;
                var total = scale * 2 - 8f;

                var width = 20f;
                var height = 20f;

                var constSwitchX = -((int) scale * width) / 2f;
                var xSwitch = constSwitchX;
                var ySwitch = -20f;

                for (var y = 0f; y < scale; y += 1f)
                {
                    var heightColor = Color.Lerp(topRightColor, bottomRightColor, y.Scale(0f, scale, 0f, 1f));

                    for (float x = 0; x < scale; x += 1f)
                    {
                        var widthColor = Color.Lerp(topLeftColor, bottomLeftColor, (x + y).Scale(0f, total, 0f, 1f));
                        var targetColor = Color.Lerp(widthColor, heightColor, x.Scale(0f, scale, 0f, 1f)) * 1f;

                        OwnerContainer.Add(new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                                OffsetMin = $"{xSwitch} {ySwitch - height}",
                                OffsetMax = $"{xSwitch + width} {ySwitch}"
                            },
                            Text = {Text = string.Empty},
                            Button =
                            {
                                Color = $"{targetColor.r} {targetColor.g} {targetColor.b} 1",
                                Command = Command + $" .input.color set hex {ColorUtility.ToHtmlStringRGB(targetColor)}"
                            }
                        }, mainLayer);

                        xSwitch += width;
                    }

                    xSwitch = constSwitchX;
                    ySwitch -= height;
                }

                #endregion

                #region Selected Color

                if (selectedColor != null)
                {
                    #region Show Color

                    OwnerContainer.Add(new CuiElement
                    {
                        Name = mainLayer + ".Selected.Color",
                        Parent = mainLayer,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = HexToCuiColor(selectedColor.HEX),
                                Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0",
                                AnchorMax = "0.5 0",
                                OffsetMin = $"{constSwitchX} 30",
                                OffsetMax = $"{constSwitchX + 100f} 60"
                            },
                            new CuiOutlineComponent
                            {
                                Color = HexToCuiColor("#575757"),
                                Distance = "3 -3",
                                UseGraphicAlpha = true
                            }
                        }
                    });

                    OwnerContainer.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = "0 0",
                            OffsetMax = "0 25"
                        },
                        Text =
                        {
                            Text = "Selected color:",
                            Align = TextAnchor.UpperLeft,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 14,
                            Color = "1 1 1 1"
                        }
                    }, mainLayer + ".Selected.Color");

                    #endregion

                    #region Input

                    #region HEX

                    OwnerContainer.Add(new CuiElement
                    {
                        Name = mainLayer + ".Selected.Color.Input.HEX",
                        Parent = mainLayer,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = HexToCuiColor("#2F3134")
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                                OffsetMin = $"{Mathf.Abs(constSwitchX) - 180} 30",
                                OffsetMax = $"{Mathf.Abs(constSwitchX) - 100} 60"
                            },
                            new CuiOutlineComponent
                            {
                                Color = HexToCuiColor("#575757"),
                                Distance = "1 -1"
                            }
                        }
                    });

                    OwnerContainer.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = "0 0",
                            OffsetMax = "0 20"
                        },
                        Text =
                        {
                            Text = "HEX",
                            Align = TextAnchor.UpperLeft,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = HexToCuiColor("#FFFFFF")
                        }
                    }, mainLayer + ".Selected.Color.Input.HEX");

                    OwnerContainer.Add(new CuiElement
                    {
                        Parent = mainLayer + ".Selected.Color.Input.HEX",
                        Components =
                        {
                            new CuiInputFieldComponent
                            {
                                FontSize = 10,
                                Align = TextAnchor.MiddleCenter,
                                Command = Command + " .input.color set hex",
                                Color = HexToCuiColor("#575757"),
                                CharsLimit = 150,
                                Text = selectedColor.HEX ?? string.Empty,
                                NeedsKeyboard = true
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1",
                                OffsetMin = "0 0", OffsetMax = "0 0"
                            }
                        }
                    });

                    #endregion

                    #region Opacity

                    OwnerContainer.Add(new CuiElement
                    {
                        Name = mainLayer + ".Selected.Color.Input.Opacity",
                        Parent = mainLayer,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = HexToCuiColor("#2F3134")
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                                OffsetMin = $"{Mathf.Abs(constSwitchX) - 90} 30",
                                OffsetMax = $"{Mathf.Abs(constSwitchX)} 60"
                            },
                            new CuiOutlineComponent
                            {
                                Color = HexToCuiColor("#575757"),
                                Distance = "1 -1"
                            }
                        }
                    });

                    OwnerContainer.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = "0 0",
                            OffsetMax = "0 20"
                        },
                        Text =
                        {
                            Text = "Opacity (0-100)",
                            Align = TextAnchor.UpperLeft,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = HexToCuiColor("#FFFFFF")
                        }
                    }, mainLayer + ".Selected.Color.Input.Opacity");

                    OwnerContainer.Add(new CuiElement
                    {
                        Parent = mainLayer + ".Selected.Color.Input.Opacity",
                        Components =
                        {
                            new CuiInputFieldComponent
                            {
                                FontSize = 10,
                                Align = TextAnchor.MiddleCenter,
                                Command = Command + " .input.color set opacity",
                                Color = HexToCuiColor("#575757"),
                                CharsLimit = 150,
                                Text = $"{selectedColor.Alpha}",
                                NeedsKeyboard = true
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1",
                                OffsetMin = "0 0", OffsetMax = "0 0"
                            }
                        }
                    });

                    #endregion

                    #endregion
                }

                #endregion
            }

            private void AddSettingField(int type, string parentLayer, string titleKey, string inputCommand,
                params object[] initialValue)
            {
                OwnerContainer.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    },
                    Image =
                    {
                        Color = "0 0 0 0"
                    }
                }, parentLayer, parentLayer + ".panel");

                #region Title

                if (!string.IsNullOrEmpty(titleKey))
                    OwnerContainer.Add(new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = "0 -15",
                            OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = GetMessage(titleKey, owner.UserIDString),
                            Align = TextAnchor.MiddleLeft,
                            Color = HexToCuiColor("#E2DBD3", 90),
                            FontSize = 10,
                            Font = "robotocondensed-regular.ttf"
                        }
                    }, parentLayer + ".panel");

                #endregion Title

                #region Value

                switch (type)
                {
                    case 0: // input
                    {
                        OwnerContainer.Add(new CuiPanel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 0",
                                OffsetMin = "0 0",
                                OffsetMax = "0 30"
                            },
                            Image =
                            {
                                Color = HexToCuiColor("#000000", 70)
                            }
                        }, parentLayer + ".panel", parentLayer + ".panel.value");
                        break;
                    }

                    case 1: // selector
                    {
                        #region Buttons

                        OwnerContainer.Add(new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin = "1 0",
                                AnchorMax = "1 0",
                                OffsetMin = "-30 0",
                                OffsetMax = "0 30"
                            },
                            Text =
                            {
                                Text = ">",
                                Align = TextAnchor.MiddleCenter,
                                Color = HexToCuiColor("#E2DBD3", 90),
                                FontSize = 20,
                                Font = "robotocondensed-bold.ttf"
                            },
                            Button =
                            {
                                Command = inputCommand + " next",
                                Color = HexToCuiColor("#4D4D4D", 50),
                                Sprite = "assets/content/ui/UI.Background.TileTex.psd"
                            }
                        }, parentLayer + ".panel");

                        OwnerContainer.Add(new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "0 0",
                                OffsetMin = "0 0",
                                OffsetMax = "30 30"
                            },
                            Text =
                            {
                                Text = "<",
                                Align = TextAnchor.MiddleCenter,
                                Color = HexToCuiColor("#E2DBD3", 90),
                                FontSize = 20,
                                Font = "robotocondensed-bold.ttf"
                            },
                            Button =
                            {
                                Command = inputCommand + " previous",
                                Color = HexToCuiColor("#4D4D4D", 50),
                                Sprite = "assets/content/ui/UI.Background.TileTex.psd"
                            }
                        }, parentLayer + ".panel");

                        #endregion

                        OwnerContainer.Add(new CuiPanel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 0",
                                OffsetMin = "30 0",
                                OffsetMax = "-30 30"
                            },
                            Image =
                            {
                                Color = HexToCuiColor("#000000", 70)
                            }
                        }, parentLayer + ".panel", parentLayer + ".panel.value");
                        break;
                    }

                    case 2: // color
                    {
                        OwnerContainer.Add(new CuiElement
                        {
                            Name = parentLayer + ".panel.value",
                            Parent = parentLayer + ".panel",
                            Components =
                            {
                                new CuiButtonComponent
                                {
                                    Color = "0 0 0 0",
                                    Command = inputCommand
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0",
                                    AnchorMax = "1 0",
                                    OffsetMin = "0 0",
                                    OffsetMax = "0 30"
                                }
                            }
                        });
                        break;
                    }

                    case 3: // icon
                    {
                        #region Image

                        OwnerContainer.Add(new CuiElement
                        {
                            Name = parentLayer + ".panel.image",
                            Parent = parentLayer + ".panel",
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Color = "0 0 0 1"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 1",
                                    AnchorMax = "0 1",
                                    OffsetMin = "0 -45",
                                    OffsetMax = "30 -15"
                                }
                            }
                        });

                        #endregion

                        #region Input

                        OwnerContainer.Add(new CuiElement
                        {
                            Name = parentLayer + ".panel.value",
                            Parent = parentLayer + ".panel",
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Color = HexToCuiColor("#000000", 70)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 1",
                                    AnchorMax = "1 1",
                                    OffsetMin = "30 -45",
                                    OffsetMax = "0 -15"
                                }
                            }
                        });

                        #region Icon

                        OwnerContainer.Add(new CuiElement
                        {
                            Name = parentLayer + ".panel.value.icon",
                            Parent = parentLayer + ".panel.value",
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Color = HexToCuiColor("#4D4D4D", 50)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "1 0",
                                    AnchorMax = "1 0",
                                    OffsetMin = "-30 0",
                                    OffsetMax = "0 30"
                                }
                            }
                        });

                        OwnerContainer.Add(new CuiElement
                        {
                            Parent = parentLayer + ".panel.value.icon",
                            Components =
                            {
                                new CuiRawImageComponent
                                {
                                    Png = Instance.GetImage("WipeSchedule_Icon_URL")
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                                    OffsetMin = "-6 -6", OffsetMax = "6 6"
                                }
                            }
                        });

                        #endregion

                        #endregion

                        #region ForAll

                        OwnerContainer.Add(new CuiElement
                        {
                            Name = parentLayer + ".panel.forall",
                            Parent = parentLayer + ".panel",
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Color = "0 0 0 0"
                                },
                                new CuiRectTransformComponent
                                    {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -80", OffsetMax = "0 -55"}
                            }
                        });

                        OwnerContainer.Add(new CuiElement
                        {
                            Parent = parentLayer + ".panel.forall",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = "APPLY AN ICON FOR EVERYONE EVENTS OF THIS TYPE",
                                    Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.UpperLeft,
                                    Color = HexToCuiColor("#E2DBD3", 90)
                                },
                                new CuiRectTransformComponent
                                    {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "-60 0"}
                            }
                        });

                        OwnerContainer.Add(new CuiElement
                        {
                            Name = parentLayer + ".panel.forall.btn",
                            Parent = parentLayer + ".panel.forall",
                            Components =
                            {
                                new CuiButtonComponent
                                {
                                    Command = inputCommand + " switch",
                                    Color = "0 0 0 0"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-42 -8.5",
                                    OffsetMax = "0 8.5"
                                }
                            }
                        });

                        #endregion ForAll

                        break;
                    }
                }

                UpdateSettingsFieldValue(type, parentLayer, inputCommand, false, initialValue);

                #endregion Value
            }

            private void UpdateSettingsFieldValue(int type, string parentLayer, string inputCommand,
                bool update = false, params object[] initialValue)
            {
                switch (type)
                {
                    case 0: // input
                    {
                        #region Input

                        OwnerContainer.Add(new CuiElement
                        {
                            Parent = parentLayer + ".panel.value",
                            Name = parentLayer + ".panel.value.input",
                            Update = update,
                            Components =
                            {
                                new CuiInputFieldComponent
                                {
                                    Text = GetMessage(initialValue[0].ToString(), owner.UserIDString),
                                    Align = TextAnchor.MiddleLeft,
                                    Color = HexToCuiColor("#E2DBD3", 50),
                                    FontSize = 12,
                                    Font = "robotocondensed-regular.ttf",
                                    Command = inputCommand,
                                    NeedsKeyboard = true
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", AnchorMax = "1 1",
                                    OffsetMin = "15 0", OffsetMax = "-15 0"
                                }
                            }
                        });

                        #endregion

                        break;
                    }

                    case 1: // selector
                    {
                        #region Input

                        OwnerContainer.Add(new CuiElement
                        {
                            Parent = parentLayer + ".panel.value",
                            Name = parentLayer + ".panel.value.input",
                            Update = update,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = GetMessage(initialValue[0].ToString(), owner.UserIDString),
                                    Align = TextAnchor.MiddleCenter,
                                    Color = HexToCuiColor("#E2DBD3", 90),
                                    FontSize = 12,
                                    Font = "robotocondensed-regular.ttf"
                                },
                                new CuiRectTransformComponent()
                            }
                        });

                        #endregion

                        break;
                    }

                    case 2: // color
                    {
                        if (initialValue[0] is not IColor color) return;

                        #region Color

                        OwnerContainer.Add(new CuiElement
                        {
                            Parent = parentLayer + ".panel.value",
                            Name = parentLayer + ".panel.value.color",
                            Update = update,
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Color = color.Get(),
                                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", AnchorMax = "1 1"
                                }
                            }
                        });

                        #endregion

                        #region Additional

                        if (!update)
                        {
                            OwnerContainer.Add(new CuiPanel
                                {
                                    RectTransform =
                                    {
                                        AnchorMin = "0 0", AnchorMax = "0.6 1",
                                        OffsetMin = "0 0", OffsetMax = "0 0"
                                    },
                                    Image =
                                    {
                                        Color = HexToCuiColor("#000000", 75),
                                        Material = "assets/content/ui/uibackgroundblur.mat"
                                    }
                                }, parentLayer + ".panel.value.color",
                                parentLayer + ".panel.value.color.hover");

                            OwnerContainer.Add(new CuiElement
                            {
                                Parent = parentLayer + ".panel.value.color.hover",
                                Components =
                                {
                                    new CuiRawImageComponent
                                    {
                                        Png = Instance.GetImage("ServerPanel_Editor_Select")
                                    },
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0 0.5", AnchorMax = "0 0.5",
                                        OffsetMin = "12 -9", OffsetMax = "30 9"
                                    }
                                }
                            });
                        }

                        OwnerContainer.Add(new CuiElement
                        {
                            Name = parentLayer + ".panel.value.color.hover.value",
                            Parent = parentLayer + ".panel.value.color.hover",
                            Update = update,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = color.HEX ?? string.Empty,
                                    Align = TextAnchor.MiddleLeft,
                                    Font = "robotocondensed-bold.ttf",
                                    FontSize = 12,
                                    Color = HexToCuiColor("#E2DBD3", 90)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", AnchorMax = "1 1",
                                    OffsetMin = "35 0", OffsetMax = "0 0"
                                }
                            }
                        });

                        #endregion

                        break;
                    }

                    case 3: // icon
                    {
                        if (initialValue[0] is not ImageSettings icon || initialValue[1] is not bool useDefault) return;

                        #region Image

                        OwnerContainer.Add(new CuiElement
                        {
                            Parent = parentLayer + ".panel.image",
                            Name = parentLayer + ".panel.image.value",
                            Update = update,
                            Components =
                            {
                                icon.GetImageComponent(),
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", AnchorMax = "1 1",
                                    OffsetMin = "4 4", OffsetMax = "-4 -4"
                                }
                            }
                        });

                        #endregion

                        #region Input

                        OwnerContainer.Add(new CuiElement
                        {
                            Parent = parentLayer + ".panel.value",
                            Name = parentLayer + ".panel.value.input",
                            Update = update,
                            Components =
                            {
                                new CuiInputFieldComponent
                                {
                                    Text = string.IsNullOrEmpty(selectedUrl) ? "default" : selectedUrl,
                                    Align = TextAnchor.MiddleLeft,
                                    Color = HexToCuiColor("#E2DBD3", 50),
                                    FontSize = 12,
                                    Font = "robotocondensed-regular.ttf",
                                    Command = inputCommand + " input",
                                    NeedsKeyboard = true,
                                    LineType = InputField.LineType.SingleLine
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", AnchorMax = "1 1",
                                    OffsetMin = "15 0", OffsetMax = "-35 0"
                                }
                            }
                        });

                        #endregion

                        #region ForAll

                        OwnerContainer.Add(new CuiElement
                        {
                            Name = parentLayer + ".panel.forall.btn.status",
                            Parent = parentLayer + ".panel.forall.btn",
                            Update = update,
                            Components =
                            {
                                new CuiRawImageComponent
                                {
                                    Png = Instance.GetImage(useDefault
                                        ? "ServerPanel_Editor_Switch_On"
                                        : "ServerPanel_Editor_Switch_Off")
                                },
                                new CuiRectTransformComponent()
                            }
                        });

                        #endregion Forall

                        break;
                    }
                }
            }

            private void UpdateRepeatParams()
            {
                OwnerContainer.Add(new CuiPanel
                    {
                        Image = {Color = "0 0 0 0"},
                        RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1"}
                    }, LayerGeneral + ".switch.repeat_params.background", LayerGeneral + ".switch.repeat_params",
                    LayerGeneral + ".switch.repeat_params");

                switch ((ModeRepeat) selectedIndexModeRepeat)
                {
                    case ModeRepeat.Days: // day
                    {
                        #region Title

                        OwnerContainer.Add(new CuiElement
                        {
                            Parent = LayerGeneral + ".switch.repeat_params",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = "REPEAT EVERY", Font = "robotocondensed-bold.ttf", FontSize = 10,
                                    Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#E2DBD3", 90)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -15",
                                    OffsetMax = "0 0"
                                }
                            }
                        });

                        #endregion

                        #region Input

                        OwnerContainer.Add(new CuiPanel
                            {
                                Image = {Color = HexToCuiColor("#000000", 70)},
                                RectTransform =
                                {
                                    AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -45",
                                    OffsetMax = "30 -15"
                                }
                            }, LayerGeneral + ".switch.repeat_params",
                            LayerGeneral + ".switch.repeat_params" + ".input");

                        OwnerContainer.Add(new CuiElement
                        {
                            Parent = LayerGeneral + ".switch.repeat_params" + ".input",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = "DAYS", Font = "robotocondensed-regular.ttf", FontSize = 12,
                                    Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#E2DBD3", 90)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "5 0",
                                    OffsetMax = "55 0"
                                }
                            }
                        });

                        UpdateRepeatDaysIntervalUI();

                        #endregion

                        break;
                    }

                    case ModeRepeat.Weeks: // weeks
                    {
                        #region Title

                        OwnerContainer.Add(new CuiElement
                        {
                            Parent = LayerGeneral + ".switch.repeat_params",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = "REPEAT EVERY", Font = "robotocondensed-bold.ttf", FontSize = 10,
                                    Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#E2DBD3", 90)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -15",
                                    OffsetMax = "0 0"
                                }
                            }
                        });

                        #endregion

                        #region Input

                        OwnerContainer.Add(new CuiPanel
                        {
                            Image = {Color = HexToCuiColor("#000000", 70)},
                            RectTransform =
                            {
                                AnchorMin = "0 1", AnchorMax = "0 1",
                                OffsetMin = "0 -45",
                                OffsetMax = "30 -15"
                            }
                        }, LayerGeneral + ".switch.repeat_params", LayerGeneral + ".switch.repeat_params" + ".input");

                        OwnerContainer.Add(new CuiElement
                        {
                            Parent = LayerGeneral + ".switch.repeat_params" + ".input",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = "WEEKS", Font = "robotocondensed-regular.ttf", FontSize = 12,
                                    Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#E2DBD3", 90)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "5 0",
                                    OffsetMax = "55 0"
                                }
                            }
                        });

                        UpdateRepeatWeeksIntervalUI();

                        #endregion

                        #region Day Of Weeks

                        OwnerContainer.Add(new CuiElement
                        {
                            Parent = LayerGeneral + ".switch.repeat_params",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = "REPEAT ON", Font = "robotocondensed-bold.ttf", FontSize = 10,
                                    Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#E2DBD3", 90)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -65",
                                    OffsetMax = "0 -50"
                                }
                            }
                        });

                        OwnerContainer.Add(new CuiPanel
                            {
                                Image = {Color = HexToCuiColor("#000000", 0)},
                                RectTransform =
                                {
                                    AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -95",
                                    OffsetMax = "0 -65"
                                }
                            }, LayerGeneral + ".switch.repeat_params",
                            LayerGeneral + ".switch.repeat_params" + ".list.days");

                        AddRepeatWeeksDay(0, "0 -15", "30 15");
                        AddRepeatWeeksDay(1, "35 -15", "65 15");
                        AddRepeatWeeksDay(2, "70 -15", "100 15");
                        AddRepeatWeeksDay(3, "105 -15", "135 15");
                        AddRepeatWeeksDay(4, "140 -15", "170 15");
                        AddRepeatWeeksDay(5, "175 -15", "205 15");
                        AddRepeatWeeksDay(6, "210 -15", "240 15");

                        void AddRepeatWeeksDay(int day, string offsetMin, string offsetMax)
                        {
                            OwnerContainer.Add(new CuiPanel
                                {
                                    Image =
                                    {
                                        Color = "0 0 0 0"
                                    },
                                    RectTransform =
                                    {
                                        AnchorMin = "0 0.5", AnchorMax = "0 0.5",
                                        OffsetMin = offsetMin,
                                        OffsetMax = offsetMax
                                    }
                                }, LayerGeneral + ".switch.repeat_params" + ".list.days",
                                LayerGeneral + ".switch.repeat_params" + ".list.days" + "." + day);

                            UpdateRepeatWeeksDayUI(day);
                        }

                        #endregion

                        UpdateRepeatWeeksDescriptionUI();
                        break;
                    }

                    case ModeRepeat.Months: // months
                    {
                        #region Title

                        OwnerContainer.Add(new CuiElement
                        {
                            Parent = LayerGeneral + ".switch.repeat_params",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = "REPEAT ON", Font = "robotocondensed-bold.ttf", FontSize = 10,
                                    Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#E2DBD3", 90)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -15",
                                    OffsetMax = "0 0"
                                }
                            }
                        });

                        #endregion

                        #region Section Today

                        OwnerContainer.Add(new CuiPanel
                            {
                                CursorEnabled = false,
                                Image = {Color = HexToCuiColor("#000000", 0)},
                                RectTransform =
                                {
                                    AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -45",
                                    OffsetMax = "0 -15"
                                }
                            }, LayerGeneral + ".switch.repeat_params",
                            LayerGeneral + ".switch.repeat_params" + ".section.today");

                        AddCheckboxButton(LayerGeneral + ".switch.repeat_params" + ".section.today",
                            Command + " " + ".edit_repeat day status", monthRepeatOnDay);

                        OwnerContainer.Add(new CuiElement
                        {
                            Parent = LayerGeneral + ".switch.repeat_params" + ".section.today",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = $"{inputDay.FormatDayNumberWithSuffix()} of every",
                                    Font = "robotocondensed-regular.ttf",
                                    FontSize = 10,
                                    Align = TextAnchor.MiddleLeft,
                                    Color = HexToCuiColor("#E2DBD3", 90)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "30 0",
                                    OffsetMax = "-65 0"
                                }
                            }
                        });

                        OwnerContainer.Add(new CuiElement
                        {
                            Parent = LayerGeneral + ".switch.repeat_params" + ".section.today",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = "month", Font = "robotocondensed-regular.ttf", FontSize = 11,
                                    Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#E2DBD3", 90)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "-30 0",
                                    OffsetMax = "0 0"
                                }
                            }
                        });

                        #region Input

                        OwnerContainer.Add(new CuiPanel
                            {
                                CursorEnabled = false,
                                Image = {Color = HexToCuiColor("#000000", 70)},
                                RectTransform =
                                {
                                    AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-65 -15",
                                    OffsetMax = "-35 15"
                                }
                            }, LayerGeneral + ".switch.repeat_params" + ".section.today",
                            LayerGeneral + ".switch.repeat_params" + ".section.today" + ".input");

                        UpdateRepeatMonthIntervalOnDayUI();

                        #endregion

                        #endregion

                        #region Section Nday

                        OwnerContainer.Add(new CuiPanel
                            {
                                CursorEnabled = false,
                                Image = {Color = HexToCuiColor("#000000", 0)},
                                RectTransform =
                                {
                                    AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -80",
                                    OffsetMax = "0 -50"
                                }
                            }, LayerGeneral + ".switch.repeat_params",
                            LayerGeneral + ".switch.repeat_params" + ".section.target_day");

                        AddCheckboxButton(LayerGeneral + ".switch.repeat_params" + ".section.target_day",
                            Command + " " + ".edit_repeat week status", monthRepeatOnDayOfWeek);

                        OwnerContainer.Add(new CuiElement
                        {
                            Parent = LayerGeneral + ".switch.repeat_params" + ".section.target_day",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = GenerateDateOrdinalMessage(new DateTime(inputYear, inputMonth, inputDay)),
                                    Font = "robotocondensed-regular.ttf",
                                    FontSize = 10, Align = TextAnchor.MiddleLeft,
                                    Color = HexToCuiColor("#E2DBD3", 90)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", AnchorMax = "1 1",
                                    OffsetMin = "30 0", OffsetMax = "-65 0"
                                }
                            }
                        });

                        OwnerContainer.Add(new CuiElement
                        {
                            Parent = LayerGeneral + ".switch.repeat_params" + ".section.target_day",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = "month", Font = "robotocondensed-regular.ttf", FontSize = 11,
                                    Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#E2DBD3", 90)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "-30 0",
                                    OffsetMax = "0 0"
                                }
                            }
                        });

                        #region Input

                        OwnerContainer.Add(new CuiPanel
                            {
                                CursorEnabled = false,
                                Image = {Color = HexToCuiColor("#000000", 70)},
                                RectTransform =
                                {
                                    AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-65 -15",
                                    OffsetMax = "-35 15"
                                }
                            }, LayerGeneral + ".switch.repeat_params" + ".section.target_day",
                            LayerGeneral + ".switch.repeat_params" + ".section.target_day" + ".input");

                        UpdateRepeatMonthIntervalOnWeekUI();

                        #endregion

                        #endregion

                        UpdateRepeatMonthsDescriptionUI();
                        break;
                    }

                    case ModeRepeat.Years: // years
                    {
                        #region Title

                        OwnerContainer.Add(new CuiElement
                        {
                            Parent = LayerGeneral + ".switch.repeat_params",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = "REPEAT ON", Font = "robotocondensed-bold.ttf", FontSize = 10,
                                    Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#E2DBD3", 90)
                                },
                                new CuiRectTransformComponent
                                    {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -15", OffsetMax = "0 0"}
                            }
                        });

                        #endregion

                        #region Section Repeat

                        OwnerContainer.Add(new CuiPanel
                            {
                                CursorEnabled = false,
                                Image = {Color = HexToCuiColor("#000000", 0)},
                                RectTransform =
                                    {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -45", OffsetMax = "0 -15"}
                            }, LayerGeneral + ".switch.repeat_params",
                            LayerGeneral + ".switch.repeat_params" + ".section.repeat");

                        OwnerContainer.Add(new CuiElement
                        {
                            Parent = LayerGeneral + ".switch.repeat_params" + ".section.repeat",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = GenerateDateOrdinalMessage(new DateTime(inputYear, inputMonth, inputDay)),
                                    Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft,
                                    Color = HexToCuiColor("#E2DBD3", 90)
                                },
                                new CuiRectTransformComponent
                                    {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "-65 0"}
                            }
                        });

                        OwnerContainer.Add(new CuiPanel
                            {
                                CursorEnabled = false,
                                Image = {Color = HexToCuiColor("#000000", 70)},
                                RectTransform =
                                {
                                    AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-65 -15",
                                    OffsetMax = "-35 15"
                                }
                            }, LayerGeneral + ".switch.repeat_params" + ".section.repeat",
                            LayerGeneral + ".switch.repeat_params" + ".section.repeat" + ".input");

                        OwnerContainer.Add(new CuiElement
                        {
                            Parent = LayerGeneral + ".switch.repeat_params" + ".section.repeat",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = "years", Font = "robotocondensed-regular.ttf", FontSize = 11,
                                    Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#E2DBD3", 90)
                                },
                                new CuiRectTransformComponent
                                    {AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "-30 0", OffsetMax = "0 0"}
                            }
                        });

                        UpdateRepeatYearsIntervalUI();

                        #endregion

                        UpdateRepeatYearsDescriptionUI();

                        break;
                    }
                }
            }

            private void UpdateRepeatYearsDescriptionUI(bool update = false)
            {
                #region Description

                OwnerContainer.Add(new CuiElement
                {
                    Name = LayerGeneral + ".switch.repeat_params" + ".description",
                    Parent = LayerGeneral + ".switch.repeat_params",
                    Update = update,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GenerateRepeatMessage(),
                            Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.UpperLeft,
                            Color = HexToCuiColor("#E2DBD3", 90)
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 -130", OffsetMax = "0 -55"}
                    }
                });

                #endregion

                #region Helpers

                string GenerateRepeatMessage()
                {
                    var message = Pool.Get<StringBuilder>();
                    try
                    {
                        message.Append("What you got: Repeat ");

                        message.Append(GenerateDateOrdinalMessage(new DateTime(inputYear, inputMonth, inputDay), true));
                        message.Append(" of every ");

                        if (inputInterval <= 1)
                            message.Append("year.");
                        else
                            message.Append($"{inputInterval.FormatDayNumberWithSuffix()} year.");

                        return message.ToString();
                    }
                    finally
                    {
                        Pool.FreeUnmanaged(ref message);
                    }
                }

                #endregion
            }

            private void UpdateRepeatMonthsDescriptionUI(bool update = false)
            {
                #region Description

                OwnerContainer.Add(new CuiElement
                {
                    Name = LayerGeneral + ".switch.repeat_params" + ".description",
                    Parent = LayerGeneral + ".switch.repeat_params",
                    Update = update,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GenerateRepeatMessage(),
                            Font = "robotocondensed-regular.ttf", FontSize = 10,
                            Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#E2DBD3", 90)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 0",
                            OffsetMin = "0 -130",
                            OffsetMax = "0 -90"
                        }
                    }
                });

                #endregion

                #region Helpers

                string GenerateRepeatMessage()
                {
                    var message = Pool.Get<StringBuilder>();
                    try
                    {
                        if (!monthRepeatOnDay && !monthRepeatOnDayOfWeek)
                        {
                            message.Append("Repeat is OFF");
                            return message.ToString();
                        }

                        message.Append("What you got: Repeat on the ");

                        if (monthRepeatOnDay)
                        {
                            message.Append($"{inputDay.FormatDayNumberWithSuffix()} of every ");

                            if (inputInterval <= 1)
                                message.Append("month.");
                            else
                                message.Append($"{inputInterval.FormatDayNumberWithSuffix()} month.");

                            if (monthRepeatOnDayOfWeek)
                            {
                                message.Length -= 1;

                                message.Append(" and ");
                            }
                        }

                        if (monthRepeatOnDayOfWeek)
                        {
                            message.Append(GenerateDateOrdinalMessage(new DateTime(inputYear, inputMonth, inputDay),
                                true));
                            message.Append(" of every ");

                            if (inputWeeksInterval <= 1)
                                message.Append("month.");
                            else
                                message.Append($"{inputWeeksInterval.FormatDayNumberWithSuffix()} month.");
                        }

                        return message.ToString();
                    }
                    finally
                    {
                        Pool.FreeUnmanaged(ref message);
                    }
                }

                #endregion
            }

            private void DateTimeElementValue(string type, bool update = false)
            {
                var elementValue = type switch
                {
                    "day" => inputDay < 0 ? "DD" : $"{inputDay:00}",
                    "month" => inputMonth < 0 ? "MM" : $"{inputMonth:00}",
                    "year" => inputYear < 0 ? "YYYY" : $"{inputYear:0000}",
                    "hour" => inputHours < 0 ? "HH" : $"{inputHours:00}",
                    "minutes" => inputMinutes < 0 ? "MM" : $"{inputMinutes:00}",
                    _ => string.Empty
                };

                OwnerContainer.Add(new CuiElement
                {
                    Name = LayerGeneral + ".switch.date_time" + ".param." + type + ".input",
                    Parent = LayerGeneral + ".switch.date_time" + ".param." + type,
                    Update = update,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Text = elementValue ?? string.Empty,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12, Align = TextAnchor.MiddleCenter,
                            Color = HexToCuiColor("#E2DBD3", 50),
                            NeedsKeyboard = true,
                            Command = Command + " inputDateTime " + type
                        },
                        new CuiRectTransformComponent()
                    }
                });
            }

            private void UpdateRepeatYearsIntervalUI(bool update = false)
            {
                OwnerContainer.Add(new CuiElement
                {
                    Name = LayerGeneral + ".switch.repeat_params" + ".section.repeat" + ".input" + ".value",
                    Parent = LayerGeneral + ".switch.repeat_params" + ".section.repeat" + ".input",
                    Update = update,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Text = $"{inputInterval:0}",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Align = TextAnchor.MiddleCenter,
                            Color = HexToCuiColor("#E2DBD3", 50),
                            NeedsKeyboard = true,
                            Command = Command + " " + ".edit_repeat interval"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1"
                        }
                    }
                });
            }

            private void UpdateRepeatMonthIntervalOnWeekUI(bool update = false)
            {
                OwnerContainer.Add(new CuiElement
                {
                    Name = LayerGeneral + ".switch.repeat_params" + ".section.target_day" + ".input" + ".value",
                    Parent = LayerGeneral + ".switch.repeat_params" + ".section.target_day" + ".input",
                    Update = update,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Text = $"{inputWeeksInterval:0}",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Align = TextAnchor.MiddleCenter,
                            Color = HexToCuiColor("#E2DBD3", 50),
                            NeedsKeyboard = true,
                            Command = Command + " " + ".edit_repeat week interval"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1"
                        }
                    }
                });
            }

            private void UpdateRepeatMonthIntervalOnDayUI(bool update = false)
            {
                OwnerContainer.Add(new CuiElement
                {
                    Name = LayerGeneral + ".switch.repeat_params" + ".section.today" + ".input" + ".value",
                    Parent = LayerGeneral + ".switch.repeat_params" + ".section.today" + ".input",
                    Update = update,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Text = $"{inputInterval:0}",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Align = TextAnchor.MiddleCenter,
                            Color = HexToCuiColor("#E2DBD3", 50),
                            NeedsKeyboard = true,
                            Command = Command + " " + ".edit_repeat day interval"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1"
                        }
                    }
                });
            }

            private void AddCheckboxButton(string btnParent, string btnCommand,
                bool btnStatus = false,
                bool update = false)
            {
                var layerCheckbox = btnParent + ".checkbox";

                if (!update)
                    OwnerContainer.Add(new CuiElement
                    {
                        Name = layerCheckbox,
                        Parent = btnParent,
                        Components =
                        {
                            new CuiButtonComponent
                            {
                                Color = "0 0 0 0",
                                Command = btnCommand
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0.5", AnchorMax = "0 0.5",
                                OffsetMin = "0 -11", OffsetMax = "22 11"
                            }
                        }
                    });

                OwnerContainer.Add(new CuiElement
                {
                    Name = layerCheckbox + ".status",
                    Parent = layerCheckbox,
                    Update = update,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = Instance.GetImage(btnStatus ? "WipeSchedule_CheckBox_On" : "WipeSchedule_CheckBox_Off")
                        },
                        new CuiRectTransformComponent()
                    }
                });
            }

            private void UpdateRepeatWeeksDayUI(int dayIndex, bool update = false)
            {
                var targetDay = (DayOfWeek) dayIndex;

                var isSelectedDay = weekEnabled[dayIndex];

                var buttonLayer = LayerGeneral + ".switch.repeat_params" + ".list.days" + "." + dayIndex;

                OwnerContainer.Add(new CuiElement
                {
                    Name = buttonLayer + ".btn",
                    Parent = buttonLayer,
                    Update = update,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Command = Command + " " + ".edit_repeat day " + dayIndex,
                            Color = isSelectedDay ? HexToCuiColor("#E44028") : HexToCuiColor("#000000", 70)
                        },
                        new CuiRectTransformComponent()
                    }
                });

                OwnerContainer.Add(new CuiElement
                {
                    Name = buttonLayer + ".btn.label",
                    Parent = buttonLayer + ".btn",
                    Update = update,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = targetDay.ToString().Substring(0, 3), Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Align = TextAnchor.MiddleCenter,
                            Color = isSelectedDay ? HexToCuiColor("#E2DBD3", 90) : HexToCuiColor("#E2DBD3", 50)
                        },
                        new CuiRectTransformComponent()
                    }
                });
            }

            private void UpdateRepeatWeeksIntervalUI(bool update = false)
            {
                OwnerContainer.Add(new CuiElement
                {
                    Name = LayerGeneral + ".switch.repeat_params" + ".input" + ".value",
                    Parent = LayerGeneral + ".switch.repeat_params" + ".input",
                    Update = update,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Text = $"{inputInterval:0}",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Align = TextAnchor.MiddleCenter,
                            Color = HexToCuiColor("#E2DBD3", 50),
                            NeedsKeyboard = true,
                            Command = Command + " " + ".edit_repeat interval"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1"
                        }
                    }
                });
            }

            private void UpdateRepeatWeeksDescriptionUI(bool update = false)
            {
                OwnerContainer.Add(new CuiElement
                {
                    Name = LayerGeneral + ".switch.repeat_params" + ".description",
                    Parent = LayerGeneral + ".switch.repeat_params",
                    Update = update,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GenerateRepeatMessage(),
                            Font = "robotocondensed-regular.ttf", FontSize = 10,
                            Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#E2DBD3", 90)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 0",
                            OffsetMin = "0 -125",
                            OffsetMax = "0 -105"
                        }
                    }
                });

                #region Helpers

                string GenerateRepeatMessage()
                {
                    var message = Pool.Get<StringBuilder>();
                    var days = Pool.Get<StringBuilder>();

                    try
                    {
                        message.Append("What you got: Repeat on ");

                        for (var i = 0; i < weekEnabled.Length; i++)
                            if (weekEnabled[i])
                                switch (i)
                                {
                                    case 0:
                                        days.Append("Sun, ");
                                        break;
                                    case 1:
                                        days.Append("Mon, ");
                                        break;
                                    case 2:
                                        days.Append("Tue, ");
                                        break;
                                    case 3:
                                        days.Append("Wed, ");
                                        break;
                                    case 4:
                                        days.Append("Thu, ");
                                        break;
                                    case 5:
                                        days.Append("Fri, ");
                                        break;
                                    case 6:
                                        days.Append("Sat, ");
                                        break;
                                }

                        if (days.Length > 0)
                        {
                            days.Length -= 2;

                            if (inputInterval <= 1)
                            {
                                message.Append(days);
                            }
                            else
                            {
                                message.Append($"Every {inputInterval} week on ");
                                message.Append(days);
                            }
                        }
                        else
                        {
                            message.Append("No days selected.");
                        }

                        return message.ToString();
                    }
                    finally
                    {
                        Pool.FreeUnmanaged(ref message);
                        Pool.FreeUnmanaged(ref days);
                    }
                }

                #endregion
            }

            private void UpdateRepeatDaysIntervalUI(bool update = false)
            {
                OwnerContainer.Add(new CuiElement
                {
                    Name = LayerGeneral + ".switch.repeat_params" + ".input" + ".value",
                    Parent = LayerGeneral + ".switch.repeat_params" + ".input",
                    Update = update,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Text = $"{inputInterval:0}",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Align = TextAnchor.MiddleCenter,
                            Color = HexToCuiColor("#E2DBD3", 50),
                            NeedsKeyboard = true,
                            Command = Command + " " + ".edit_repeat interval"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0",
                            OffsetMax = "0 0"
                        }
                    }
                });

                #region Description

                OwnerContainer.Add(new CuiElement
                {
                    Name = LayerGeneral + ".switch.repeat_params" + ".description",
                    Parent = LayerGeneral + ".switch.repeat_params",
                    Update = update,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"What you got: Repeat every {inputInterval:0}th day.",
                            Font = "robotocondensed-regular.ttf", FontSize = 10,
                            Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#E2DBD3", 90)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 -75",
                            OffsetMax = "0 -55"
                        }
                    }
                });

                #endregion
            }

            private void DrawToggleButton(bool enabledDropdown = false)
            {
                #region Title

                OwnerContainer.Add(new CuiElement
                {
                    Name = LayerGeneral + ".switch.type_schedule" + ".value.title",
                    DestroyUi = LayerGeneral + ".switch.type_schedule" + ".value.title",
                    Parent = LayerGeneral + ".switch.type_schedule" + ".value.bg",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMessage(((ModeRepeat) selectedIndexModeRepeat).ToString(), owner.UserIDString),
                            Font = "robotocondensed-regular.ttf", FontSize = 12,
                            Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#E2DBD3", 90)
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "10 0", OffsetMax = "0 0"}
                    }
                });

                #endregion

                #region Toggle Button

                OwnerContainer.Add(new CuiButton
                    {
                        Text =
                        {
                            Text = enabledDropdown ? "▼" : "▲", Font = "robotocondensed-regular.ttf", FontSize = 16,
                            Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#E2DBD3", 90)
                        },
                        Button =
                        {
                            Command = Command + " " + $".try_select_date {!enabledDropdown}",
                            Color = HexToCuiColor("#4D4D4D", 50),
                            Sprite = "assets/content/ui/UI.Background.Tile.psd"
                        },
                        RectTransform =
                            {AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-30 0", OffsetMax = "0 30"}
                    }, LayerGeneral + ".switch.type_schedule",
                    LayerGeneral + ".switch.type_schedule" + ".toggle.btn",
                    LayerGeneral + ".switch.type_schedule" + ".toggle.btn");

                #endregion

                #region Dropdown

                if (enabledDropdown)
                {
                    var modeRepeats = (ModeRepeat[]) Enum.GetValues(typeof(ModeRepeat));
                    Array.Sort(modeRepeats, (x, y) => x.CompareTo(y));

                    var dropdownOffsetY = -UI_ADMIN_SETTINGS_DROPDOWN_ELEMENT_INDENT_Y;

                    var totalHeight = modeRepeats.Length * UI_ADMIN_SETTINGS_DROPDOWN_ELEMENT_HEIGHT +
                                      (modeRepeats.Length - 1) * UI_ADMIN_SETTINGS_DROPDOWN_ELEMENT_MARGIN_Y;

                    totalHeight = Mathf.Max(totalHeight, 170f);

                    var layerDropdown = LayerGeneral + ".switch.type_schedule" + ".dropdown";
                    OwnerContainer.Add(new CuiPanel
                        {
                            CursorEnabled = false,
                            Image =
                            {
                                Color = HexToCuiColor("#000000", 70),
                                Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                            },
                            RectTransform =
                            {
                                AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "-170 0", OffsetMax = "0 180"
                            }
                        }, LayerGeneral + ".switch.type_schedule" + ".toggle.btn",
                        layerDropdown,
                        layerDropdown);

                    var layerScrollView = layerDropdown + ".scrollview";
                    OwnerContainer.Add(new CuiElement
                    {
                        Parent = layerDropdown,
                        Name = layerScrollView,
                        DestroyUi = layerScrollView,
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiScrollViewComponent
                            {
                                MovementType = ScrollRect.MovementType.Clamped,
                                Vertical = true,
                                Inertia = true,
                                Horizontal = false,
                                Elasticity = 0.25f,
                                DecelerationRate = 0.3f,
                                ScrollSensitivity = 24f,
                                ContentTransform = new CuiRectTransform
                                {
                                    AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -{totalHeight}",
                                    OffsetMax = "0 0"
                                },
                                VerticalScrollbar = new CuiScrollbar
                                {
                                    Size = 3,
                                    HandleColor = new IColor("#D74933").Get(),
                                    HighlightColor = new IColor("#D74933").Get(),
                                    PressedColor = new IColor("#D74933").Get(),
                                    HandleSprite = "assets/content/ui/UI.Background.TileTex.psd",
                                    TrackColor = new IColor("#38393F", 40).Get(),
                                    TrackSprite = "assets/content/ui/UI.Background.TileTex.psd"
                                }
                            },
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"}
                        }
                    });

                    foreach (var modeRepeat in modeRepeats)
                    {
                        var isSelected = selectedIndexModeRepeat == (int) modeRepeat;

                        OwnerContainer.Add(new CuiButton
                        {
                            Text =
                            {
                                Text = GetMessage(modeRepeat.ToString(), owner.UserIDString),
                                Font = "robotocondensed-bold.ttf", FontSize = 12,
                                Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#E2DBD3", 90)
                            },
                            Button =
                            {
                                Command = Command + " .set_date " + modeRepeat,
                                Close = layerDropdown,
                                Color = isSelected ? HexToCuiColor("#E44028", 90) : HexToCuiColor("#4D4D4D", 50),
                                Sprite = "assets/content/ui/UI.Background.Tile.psd"
                            },
                            RectTransform =
                            {
                                AnchorMin = "0 1", AnchorMax = "0 1",
                                OffsetMin = $"0 {dropdownOffsetY - UI_ADMIN_SETTINGS_DROPDOWN_ELEMENT_HEIGHT}",
                                OffsetMax = $"182 {dropdownOffsetY}"
                            }
                        }, layerScrollView);

                        dropdownOffsetY = dropdownOffsetY - UI_ADMIN_SETTINGS_DROPDOWN_ELEMENT_HEIGHT -
                                          UI_ADMIN_SETTINGS_DROPDOWN_ELEMENT_MARGIN_Y;
                    }
                }

                #endregion
            }

            private void Reset()
            {
                selectedIndex = -1;
                selectedSchedule = null;
                selectedIndexTypeEvent = 0;
                selectedIndexTypeSchedule = 0;
                selectedIndexModeRepeat = 0;
                selectedNameEvent = "";
                selectedDescription = "";
                selectedColor = null;
                selectedColorEvent = "";
                selectedUrl = "";
                selectedIcon = null;
                selectedUseChangeDefaultIcon = false;
                selectedUseChangeDefaultColor = false;
                inputDay = toDay;
                inputMonth = currentMonth;
                inputYear = currentYear;
                inputHours = currentHour;
                inputMinutes = currentMinute;
                inputInterval = inputWeeksInterval = 1;

                weekEnabled.SetArray(null, index => index == 3);

                newEvent = null;
            }

            private void UpdateSchedule()
            {
                allSchedule.Clear();
                allSchedule.AddRange(Instance.Schedule);
                allSchedule.AddRange(Instance.ScheduleEventsManager);
            }

            private int GetIndexTypeEvent(ScheduleToDay schedule)
            {
                var index = -1;
                for (var i = 0; i < typesEventUses.Length; i++)
                    if (typesEventUses[i] == schedule.EventType)
                    {
                        index = i;
                        break;
                    }

                return index;
            }

            private int GetMaxDaysInMonth(int month, int year)
            {
                return month < 0 || year < 0 ? 31 : DateTime.DaysInMonth(year, month);
            }

            public override void ProcessingCommand(string[] args)
            {
                switch (args[0])
                {
                    case "close.setup":
                    {
                        if (newEvent != null) allSchedule.Remove(newEvent);

                        Reset();

                        CuiHelper.DestroyUi(owner, LayerGeneral + ".SetupSchedule");

                        UpdateUI(() =>
                        {
                            UpdateScheduleMonth(selectedDateTime);
                            UpdateScheduleDay(selectedDateTime);
                            AddTableDate();
                            AddScheduleCurrentDay();
                        });

                        owner.Invoke(() => SaveDateFile(Instance.FILE_Schedule, Instance.Schedule), 0.1f);
                        break;
                    }
                    case "setup.schedule":
                    {
                        UpdateUI(() =>
                        {
                            AddOwnerEditPanel();
                            AddEditScheduleUI();
                            AddEditPanelUI();
                        });
                        break;
                    }
                    case ".enable":
                    {
                        if (args.Length != 2 || !int.TryParse(args[1], out var index) || allSchedule.Count <= index)
                            return;

                        var _schedule = allSchedule[index];
                        if (_schedule.EventType == TypeEvent.EventManager)
                            return;

                        _schedule.Enable = !_schedule.Enable;

                        UpdateUI(() =>
                        {
                            EditableScheduleFieldUI(index);

                            // if (_schedule == selectedSchedule) AddUpdateResult();
                        });
                        break;
                    }
                    case ".use.event":
                    {
                        if (args.Length != 2 || !int.TryParse(args[1], out var num) || selectedIndex == num) return;

                        if (newEvent != null) return;

                        var oldSelected = selectedIndex;

                        Reset();
                        selectedIndex = num;
                        selectedSchedule = allSchedule[selectedIndex];
                        selectedIndexTypeEvent = GetIndexTypeEvent(selectedSchedule);
                        selectedNameEvent = selectedSchedule.Name;
                        selectedDescription = selectedSchedule.Descriptions;
                        selectedUrl = selectedSchedule.UrlIcon;
                        selectedIcon = string.IsNullOrEmpty(selectedUrl)
                            ? Instance.IconSetup[selectedSchedule.EventType]
                            : new ImageSettings(selectedUrl, new IColor("#ffffff"),
                                InterfacePosition.CreateFullStretch());

                        selectedColor = selectedSchedule.Color ?? (selectedIndexTypeEvent >= 0
                            ? Instance.EventsColors[typesEventUses[selectedIndexTypeEvent]]
                            : Instance.EventsColors[TypeEvent.EventManager]);

                        selectedColorEvent = selectedSchedule.Color == null ? "" : selectedColor.HEX;

                        selectedIndexTypeSchedule = (int) selectedSchedule.ScheduleType;
                        inputHours = selectedSchedule.ScheduleType == TypeSchedule.DateTime
                            ? selectedSchedule.DateTimeEvent.Hour
                            : selectedSchedule.CustomDateTimeEvent.Times.Hour;
                        inputMinutes = selectedSchedule.ScheduleType == TypeSchedule.DateTime
                            ? selectedSchedule.DateTimeEvent.Minute
                            : selectedSchedule.CustomDateTimeEvent.Times.Minute;
                        inputDay = selectedSchedule.DateTimeEvent.Day;
                        inputMonth = selectedSchedule.DateTimeEvent.Month;
                        inputYear = selectedSchedule.DateTimeEvent.Year;
                        inputInterval = selectedSchedule.CustomDateTimeEvent.DaysInterval;
                        inputWeeksInterval = selectedSchedule.CustomDateTimeEvent.WeeksInterval;
                        monthRepeatOnDay = selectedSchedule.CustomDateTimeEvent.MonthRepeatOnDay;
                        monthRepeatOnDayOfWeek = selectedSchedule.CustomDateTimeEvent.MonthRepeatOnDayOfWeek;

                        selectedIndexModeRepeat = (int) selectedSchedule.CustomDateTimeEvent.Mode;

                        weekEnabled.SetArray(selectedSchedule.CustomDateTimeEvent.DaysOfWeek, false);

                        UpdateUI(() =>
                        {
                            if (oldSelected >= 0) EditableScheduleFieldUI(oldSelected);

                            EditableScheduleFieldUI(selectedIndex);

                            AddEditPanelUI();
                        });
                        break;
                    }
                    case "add.event":
                    {
                        if (newEvent != null) return;
                        Reset();
                        newEvent = new ScheduleToDay(TypeSchedule.DateTime, TypeEvent.CustomEvent, "new event")
                        {
                            DateTimeEvent = currentDateTime, Enable = true
                        };

                        allSchedule.Add(newEvent);
                        selectedIndex = allSchedule.Count - 1;
                        selectedSchedule = allSchedule[selectedIndex];

                        selectedIndexTypeEvent = GetIndexTypeEvent(selectedSchedule);
                        selectedNameEvent = selectedSchedule.Name;
                        selectedDescription = selectedSchedule.Descriptions;

                        selectedUrl = selectedSchedule.UrlIcon;
                        selectedIcon = string.IsNullOrEmpty(selectedUrl)
                            ? Instance.IconSetup[selectedSchedule.EventType]
                            : new ImageSettings(selectedUrl, new IColor("#ffffff"),
                                InterfacePosition.CreateFullStretch());

                        selectedColor = selectedSchedule.Color == null
                            ? selectedIndexTypeEvent >= 0
                                ? Instance.EventsColors[typesEventUses[selectedIndexTypeEvent]]
                                : Instance.EventsColors[TypeEvent.EventManager]
                            : selectedSchedule.Color;

                        selectedColorEvent = selectedSchedule.Color == null ? "" : selectedColor.HEX;

                        selectedIndexTypeSchedule = (int) selectedSchedule.ScheduleType;
                        inputHours = selectedSchedule.ScheduleType == TypeSchedule.DateTime
                            ? selectedSchedule.DateTimeEvent.Hour
                            : selectedSchedule.CustomDateTimeEvent.Times.Hour;
                        inputMinutes = selectedSchedule.ScheduleType == TypeSchedule.DateTime
                            ? selectedSchedule.DateTimeEvent.Minute
                            : selectedSchedule.CustomDateTimeEvent.Times.Minute;
                        inputDay = selectedSchedule.DateTimeEvent.Day;
                        inputMonth = selectedSchedule.DateTimeEvent.Month;
                        inputYear = selectedSchedule.DateTimeEvent.Year;
                        inputInterval = selectedSchedule.CustomDateTimeEvent.DaysInterval;

                        selectedIndexModeRepeat = (int) selectedSchedule.CustomDateTimeEvent.Mode;

                        weekEnabled.SetArray(selectedSchedule.CustomDateTimeEvent.DaysOfWeek, false);

                        UpdateUI(() =>
                        {
                            AddEditScheduleUI();
                            AddEditPanelUI();
                        });
                        break;
                    }
                    case ".type.event":
                    {
                        if (args.Length != 2) return;

                        switch (args[1])
                        {
                            case "next":
                            {
                                if (selectedIndexTypeEvent == typesEventUses.Length - 1) selectedIndexTypeEvent = 0;
                                else selectedIndexTypeEvent++;
                                break;
                            }
                            case "previous":
                            {
                                if (selectedIndexTypeEvent == 0) selectedIndexTypeEvent = typesEventUses.Length - 1;
                                else selectedIndexTypeEvent--;
                                break;
                            }
                        }

                        if (typesEventUses[selectedIndexTypeEvent] == TypeEvent.GlobalWipe ||
                            typesEventUses[selectedIndexTypeEvent] == TypeEvent.WipeMap ||
                            typesEventUses[selectedIndexTypeEvent] == TypeEvent.WipeBlock)
                            selectedIndexTypeSchedule = (int) TypeSchedule.CustomDate;
                        else
                            selectedIndexTypeSchedule = (int) TypeSchedule.DateTime;

                        UpdateUI(() =>
                        {
                            UpdateSettingsFieldValue(1, LayerGeneral + ".switch.type_event",
                                Command + " " + ".type.event", true, typesEventUses[selectedIndexTypeEvent].ToString());

                            EditableScheduleFieldUI(selectedIndex);
                        });

                        var type = selectedIndexTypeEvent >= 0
                            ? typesEventUses[selectedIndexTypeEvent]
                            : TypeEvent.EventManager;

                        // selectedIcon = string.IsNullOrEmpty(selectedUrl) ? Instance.IconSetup[selectedSchedule.EventType] : new (selectedUrl, new("#ffffff", 100), InterfacePosition.CreateFullStretch());
                        if (string.IsNullOrEmpty(selectedUrl))
                        {
                            selectedIcon = Instance.IconSetup[type];

                            UpdateUI(() =>
                            {
                                UpdateSettingsFieldValue(3, LayerGeneral + ".switch.icon_settings",
                                    Command + " .input.url", true, selectedIcon, selectedUseChangeDefaultIcon);
                            });
                        }

                        if (string.IsNullOrEmpty(selectedColorEvent))
                        {
                            selectedColor = selectedSchedule.Color ?? (selectedIndexTypeEvent >= 0
                                ? Instance.EventsColors[typesEventUses[selectedIndexTypeEvent]]
                                : Instance.EventsColors[TypeEvent.EventManager]);

                            UpdateUI(() =>
                            {
                                UpdateSettingsFieldValue(2, LayerGeneral + ".switch.color_edit",
                                    Command + " " + ".input.color", true, selectedColor);
                            });
                        }

                        break;
                    }

                    case ".try_select_date":
                    {
                        if (args.Length != 2) return;

                        var newEnabledDropdown = Convert.ToBoolean(args[1]);

                        UpdateUI(() => { DrawToggleButton(newEnabledDropdown); });
                        break;
                    }

                    case ".set_date":
                    {
                        if (args.Length != 2) return;

                        if (Enum.TryParse(args[1], out ModeRepeat modeRepeat))
                            selectedIndexModeRepeat = (int) modeRepeat;

                        inputInterval = 1;
                        inputWeeksInterval = 1;

                        UpdateUI(() =>
                        {
                            DrawToggleButton();

                            UpdateRepeatParams();
                        });
                        break;
                    }

                    case ".edit_repeat":
                    {
                        switch ((ModeRepeat) selectedIndexModeRepeat)
                        {
                            case ModeRepeat.Days:
                            {
                                switch (args[1])
                                {
                                    case "interval":
                                    {
                                        if (args.Length < 3 || !int.TryParse(args[2], out var interval))
                                            //TODO: 0
                                            return;

                                        inputInterval = interval;

                                        UpdateUI(() => { UpdateRepeatDaysIntervalUI(true); });
                                        break;
                                    }
                                }

                                break;
                            }
                            case ModeRepeat.Weeks:
                            {
                                switch (args[1])
                                {
                                    case "interval":
                                    {
                                        if (args.Length < 3 || !int.TryParse(args[2], out var interval))
                                            //TODO: 0
                                            return;

                                        inputInterval = interval;

                                        UpdateUI(() =>
                                        {
                                            UpdateRepeatWeeksIntervalUI(true);
                                            UpdateRepeatWeeksDescriptionUI(true);
                                        });
                                        break;
                                    }

                                    case "day":
                                    {
                                        if (args.Length < 3 || !int.TryParse(args[2], out var dayIndex))
                                            //TODO: 0
                                            return;

                                        weekEnabled[dayIndex] = !weekEnabled[dayIndex];

                                        UpdateUI(() =>
                                        {
                                            UpdateRepeatWeeksDayUI(dayIndex, true);
                                            UpdateRepeatWeeksDescriptionUI(true);
                                        });
                                        break;
                                    }
                                }

                                break;
                            }
                            case ModeRepeat.Months:
                            {
                                if (args.Length < 3) return;

                                switch (args[1])
                                {
                                    case "day":
                                    {
                                        switch (args[2])
                                        {
                                            case "interval":
                                            {
                                                if (args.Length < 4 || !int.TryParse(args[3], out var interval))
                                                    //TODO: 0
                                                    return;

                                                inputInterval = interval;

                                                UpdateUI(() =>
                                                {
                                                    UpdateRepeatMonthIntervalOnDayUI(true);
                                                    UpdateRepeatMonthsDescriptionUI(true);
                                                });
                                                break;
                                            }

                                            case "status":
                                            {
                                                monthRepeatOnDay = !monthRepeatOnDay;

                                                UpdateUI(() =>
                                                {
                                                    AddCheckboxButton(
                                                        LayerGeneral + ".switch.repeat_params" + ".section.today",
                                                        Command + " " + ".edit_repeat day status", monthRepeatOnDay,
                                                        true);
                                                    UpdateRepeatMonthsDescriptionUI(true);
                                                });
                                                break;
                                            }
                                        }

                                        break;
                                    }

                                    case "week":
                                    {
                                        switch (args[2])
                                        {
                                            case "interval":
                                            {
                                                if (args.Length < 4 || !int.TryParse(args[3], out var interval))
                                                    //TODO: 0
                                                    return;

                                                inputWeeksInterval = interval;

                                                UpdateUI(() =>
                                                {
                                                    UpdateRepeatMonthIntervalOnWeekUI(true);
                                                    UpdateRepeatMonthsDescriptionUI(true);
                                                });
                                                break;
                                            }

                                            case "status":
                                            {
                                                monthRepeatOnDayOfWeek = !monthRepeatOnDayOfWeek;

                                                UpdateUI(() =>
                                                {
                                                    AddCheckboxButton(
                                                        LayerGeneral + ".switch.repeat_params" + ".section.target_day",
                                                        Command + " " + ".edit_repeat week status",
                                                        monthRepeatOnDayOfWeek, true);
                                                    UpdateRepeatMonthsDescriptionUI(true);
                                                });
                                                break;
                                            }
                                        }

                                        break;
                                    }
                                }

                                break;
                            }
                            case ModeRepeat.Years:
                            {
                                switch (args[1])
                                {
                                    case "interval":
                                    {
                                        if (args.Length < 3 || !int.TryParse(args[2], out var interval))
                                            //TODO: 0
                                            return;

                                        inputInterval = interval;

                                        UpdateUI(() =>
                                        {
                                            UpdateRepeatYearsIntervalUI(true);
                                            UpdateRepeatYearsDescriptionUI(true);
                                        });
                                        break;
                                    }
                                }

                                break;
                            }
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        break;
                    }

                    case "inputDateTime":
                    {
                        if (args.Length <= 1) return;

                        var value = -1;
                        if (args.Length > 2 && (!int.TryParse(args[2], out value) || value < -1)) value = -1;

                        switch (args[1])
                        {
                            case "hour":
                            {
                                inputHours = value < 0 ? inputHours : Mathf.Clamp(value, 0, 23);
                                break;
                            }
                            case "minutes":
                            {
                                inputMinutes = value < 0 ? inputMinutes : Mathf.Clamp(value, 0, 59);
                                break;
                            }
                            case "day":
                            {
                                inputDay = value < 0 ? inputDay : Mathf.Clamp(value, 1, 31);
                                break;
                            }
                            case "month":
                            {
                                inputMonth = value < 0 ? inputMonth : Mathf.Clamp(value, 1, 12);
                                break;
                            }
                            case "year":
                            {
                                inputYear = value < 0
                                    ? inputYear
                                    : Mathf.Clamp(value, currentYear, currentYear + 100);
                                break;
                            }
                        }

                        UpdateUI(() =>
                        {
                            DateTimeElementValue(args[1], true);
                            UpdateRepeatParams();
                        });
                        break;
                    }
                    case ".input.description":
                    {
                        selectedDescription =
                            args.Length > 1 ? string.Join(" ", args, 1, args.Length - 1) : string.Empty;

                        UpdateUI(() =>
                        {
                            UpdateSettingsFieldValue(0, LayerGeneral + ".switch.description_event",
                                Command + " " + ".input.description", true, selectedDescription);
                        });
                        break;
                    }
                    case ".input.name":
                    {
                        selectedNameEvent = args.Length > 1 ? string.Join(" ", args, 1, args.Length - 1) : string.Empty;

                        UpdateUI(() =>
                        {
                            UpdateSettingsFieldValue(0, LayerGeneral + ".switch.name_event",
                                Command + " " + ".input.name", true, selectedNameEvent);
                        });
                        break;
                    }
                    case ".input.url":
                    {
                        switch (args[1])
                        {
                            case "input":
                            {
                                selectedUrl = args.Length > 2
                                    ? string.Join(" ", args, 2, args.Length - 2)
                                    : string.Empty;

                                selectedIcon = string.IsNullOrEmpty(selectedUrl)
                                    ? Instance.IconSetup[selectedSchedule.EventType]
                                    : new ImageSettings(selectedUrl, new IColor("#ffffff"),
                                        InterfacePosition.CreateFullStretch());
                                break;
                            }
                            case "switch":
                            {
                                selectedUseChangeDefaultIcon = !selectedUseChangeDefaultIcon;
                                break;
                            }
                        }

                        UpdateUI(() =>
                        {
                            UpdateSettingsFieldValue(3, LayerGeneral + ".switch.icon_settings",
                                Command + " .input.url", true, selectedIcon, selectedUseChangeDefaultIcon);
                        });
                        break;
                    }
                    case ".input.color":
                    {
                        if (args.Length < 2) return;

                        switch (args[1])
                        {
                            case "start":
                            {
                                UpdateUI(() => { AddColorSelectionPanel(); });
                                break;
                            }

                            case "close":
                            {
                                if (selectedColor == null)
                                {
                                    selectedColorEvent = string.Empty;
                                    selectedColor = selectedSchedule.Color ?? (selectedIndexTypeEvent >= 0
                                        ? Instance.EventsColors[typesEventUses[selectedIndexTypeEvent]]
                                        : Instance.EventsColors[TypeEvent.EventManager]);

                                    selectedColorEvent = selectedSchedule.Color == null ? "" : selectedColor.HEX;
                                }

                                break;
                            }

                            case "set":
                            {
                                switch (args[2])
                                {
                                    case "hex":
                                    {
                                        if (args.Length > 3)
                                        {
                                            var hex = args[3];
                                            if (string.IsNullOrEmpty(hex)) return;

                                            var str = hex.Trim('#');
                                            if (!str.IsHex())
                                                return;

                                            var newColorHex = '#' + str;
                                            selectedColor.HEX = newColorHex;
                                            selectedColorEvent = newColorHex;
                                        }

                                        break;
                                    }

                                    case "opacity":
                                    {
                                        if (args.Length > 3)
                                        {
                                            var opacity = Convert.ToSingle(args[3]);
                                            if (opacity is < 0 or > 100)
                                                return;

                                            opacity = (float) Math.Round(opacity, 2);

                                            selectedColor.Alpha = opacity;
                                        }

                                        break;
                                    }
                                }

                                selectedColor.UpdateCache();

                                UpdateUI(() =>
                                {
                                    UpdateSettingsFieldValue(2, LayerGeneral + ".switch.color_edit",
                                        Command + " .input.color start", true, selectedColor);

                                    AddColorSelectionPanel();
                                });
                                break;
                            }
                        }

                        break;
                    }

                    case "save.event":
                    {
                        if (args.Length != 2 || !int.TryParse(args[1], out var index) || index != selectedIndex) return;

                        if (selectedIndexTypeEvent < 0)
                        {
                            if (selectedUseChangeDefaultColor)
                            {
                                Instance.EventsColors[TypeEvent.EventManager] = selectedColor;
                                selectedSchedule.Color = null;
                                //сохранение дефолтных цветов
                                SaveDateFile(Instance.FILE_EventsColors, Instance.EventsColors);
                            }

                            if (selectedUseChangeDefaultIcon)
                            {
                                Instance.IconSetup[TypeEvent.EventManager] = selectedIcon;
                                selectedSchedule.UrlIcon = "";
                                //сохранение дефолтных иконок
                                SaveDateFile(Instance.FILE_IconSetup, Instance.IconSetup);
                            }
                        }
                        else
                        {
                            if (newEvent != null) Instance.Schedule.Add(selectedSchedule);
                            selectedSchedule.Name = selectedNameEvent;
                            selectedSchedule.Descriptions = selectedDescription;
                            selectedSchedule.EventType = typesEventUses[selectedIndexTypeEvent];

                            if (selectedUseChangeDefaultColor)
                            {
                                Instance.EventsColors[selectedSchedule.EventType] = selectedColor;
                                selectedSchedule.Color = null;

                                //сохранение дефолтных цветов
                                SaveDateFile(Instance.FILE_EventsColors, Instance.EventsColors);
                            }
                            else if (!string.IsNullOrEmpty(selectedColorEvent))
                            {
                                selectedSchedule.Color = new IColor(selectedColor.HEX, selectedColor.Alpha);
                            }

                            if (selectedUseChangeDefaultIcon)
                            {
                                Instance.IconSetup[selectedSchedule.EventType] = selectedIcon;
                                selectedSchedule.UrlIcon = "";
                                //сохранение дефолтных иконок
                                SaveDateFile(Instance.FILE_IconSetup, Instance.IconSetup);
                            }
                            else
                            {
                                selectedSchedule.UrlIcon = selectedUrl;
                            }

                            selectedSchedule.ScheduleType = (TypeSchedule) selectedIndexTypeSchedule;

                            selectedSchedule.DateTimeEvent = new DateTime(inputYear, inputMonth, inputDay, inputHours,
                                inputMinutes, 0);

                            selectedSchedule.CustomDateTimeEvent = new CustomDateTime(
                                (ModeRepeat) selectedIndexModeRepeat, inputInterval, inputWeeksInterval,
                                weekEnabled, new MyTime(inputHours, inputMinutes), monthRepeatOnDay,
                                monthRepeatOnDayOfWeek);

                            //сохранение списка событий
                            SaveDateFile(Instance.FILE_Schedule, Instance.Schedule);
                        }

                        var schedule = selectedSchedule;
                        Reset();
                        UpdateSchedule();

                        UpdateUI(AddEditScheduleUI);

                        var ind = allSchedule.IndexOf(schedule);
                        owner.Invoke(() => ProcessingCommand(new string[2] {".use.event", ind + ""}), 0.1f);

                        break;
                    }

                    case "delete.event":
                    {
                        if (args.Length != 2 || !int.TryParse(args[1], out var index) ||
                            index != selectedIndex ||
                            selectedIndexTypeEvent < 0)
                            return;

                        if (newEvent == null) Instance.Schedule.Remove(selectedSchedule);

                        //сохранение списка событий
                        SaveDateFile(Instance.FILE_Schedule, Instance.Schedule);

                        Reset();
                        UpdateSchedule();

                        UpdateUI(() =>
                        {
                            AddEditScheduleUI();
                            AddEditPanelUI();
                        });

                        break;
                    }
                }

                base.ProcessingCommand(args);
            }
        }

        private class PlayerUIController
        {
            public static Dictionary<string, PlayerUIController> PlayersUI = new();
            public readonly CuiElementContainer OwnerContainer = new();
            protected BasePlayer owner;
            public TypeUI Type;
            public UserInterfaceSetup setupPanel;
            private List<ScheduleToDay> scheduleOfSelectedMonth = Pool.Get<List<ScheduleToDay>>();
            private List<ScheduleToDay> scheduleOfSelectedDay;
            protected DateTime selectedDateTime = currentDateTime;
            protected int selectedMonth = currentMonth;
            protected int selectedYear = currentYear;
            protected Calendar calendar = CultureInfo.InvariantCulture.Calendar;
            protected bool isOpenDescription;

            #region Layers

            protected const string
                LayerGeneral = Layer + ".Background",
                LayerDayBackground = Layer + ".Day.Background",
                LayerDayColorEvent = Layer + ".Day.Color.Event",
                LayerToDay = Layer + ".ToDay";

            #endregion

            public PlayerUIController(BasePlayer player, TypeUI typeUI)
            {
                if (PlayersUI.ContainsKey(player.UserIDString))
                {
                    Remove(player);
                    CuiHelper.DestroyUi(player, Layer);
                }

                PlayersUI.Add(player.UserIDString, this);
                owner = player;
                Type = typeUI;

                switch (Type)
                {
                    case TypeUI.FullScreen:
                    {
                        setupPanel = Instance.UIFullScreen;
                        var screenInterfaceSetup = (ScreenInterfaceSetup) setupPanel;

                        screenInterfaceSetup.AddScreenUI(OwnerContainer, "Overlay", Layer, Layer);
                        OwnerContainer.Add(setupPanel.MainPanel.GetImage(Layer, LayerGeneral, LayerGeneral));

                        OwnerContainer.Add(new CuiElement
                        {
                            Parent = LayerGeneral,
                            Name = LayerGeneral + ".button.close.background",
                            Components =
                            {
                                new CuiButtonComponent
                                {
                                    Command = Command + " " + "close",
                                    Color = screenInterfaceSetup.ButtonClose.BackgroundClose.Color.Get(),
                                    Sprite = screenInterfaceSetup.ButtonClose.BackgroundClose.Sprite,
                                    Material = screenInterfaceSetup.ButtonClose.BackgroundClose.Material
                                },
                                screenInterfaceSetup.ButtonClose.BackgroundClose.GetRectTransform()
                            }
                        });

                        OwnerContainer.Add(new CuiElement
                        {
                            Parent = LayerGeneral + ".button.close.background",
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Color = screenInterfaceSetup.ButtonClose.Color.Get(),
                                    Sprite = screenInterfaceSetup.ButtonClose.Sprite,
                                    Material = screenInterfaceSetup.ButtonClose.Material
                                },
                                screenInterfaceSetup.ButtonClose.GetRectTransform()
                            }
                        });
                        break;
                    }
                    case TypeUI.ServerMenu:
                    {
                        setupPanel = Instance.UIMenu;
                        OwnerContainer.Add(new CuiElement
                        {
                            Parent = "UI.Server.Panel.Content", Name = "UI.Server.Panel.Content.Plugin",
                            DestroyUi = "UI.Server.Panel.Content.Plugin", Components = {FullScreenTransform}
                        });

                        OwnerContainer.Add(new CuiElement
                        {
                            Parent = "UI.Server.Panel.Content.Plugin", Name = Layer, DestroyUi = Layer,
                            Components = {FullScreenTransform}
                        });

                        OwnerContainer.Add(setupPanel.MainPanel.GetImage(Layer, LayerGeneral, LayerGeneral));
                        break;
                    }
                }


                UpdateScheduleMonth(selectedDateTime);
                UpdateScheduleDay(selectedDateTime);
                CreateUI();
            }

            protected static int currentMonth => currentDateTime.Month;
            protected static int currentYear => currentDateTime.Year;
            protected static int toDay => currentDateTime.Day;
            protected static int currentHour => currentDateTime.Hour;
            protected static int currentMinute => currentDateTime.Minute;

            private void CreateUI()
            {
                #region Header

                if (setupPanel.HeaderPanel != null)
                {
                    OwnerContainer.Add(
                        setupPanel.HeaderPanel.Background.GetImage(LayerGeneral, Layer + ".Title.Background"));
                    if (setupPanel.HeaderPanel.ShowHeader)
                        OwnerContainer.Add(setupPanel.HeaderPanel.Header.CreateText(
                            GetMessage(Calendar, owner.UserIDString), Layer + ".Title.Background", Layer + ".Title"));
                    if (setupPanel.HeaderPanel.ShowLine)
                        OwnerContainer.Add(setupPanel.HeaderPanel.Line.GetImage(Layer + ".Title.Background",
                            Layer + ".Title.Line"));
                }

                #endregion

                OwnerContainer.Add(setupPanel.PanelCalendar.Background.GetImage(LayerGeneral,
                    Layer + ".Calendar.Background", Layer + ".Calendar.Background"));

                #region DaysOfWeekPanel

                OwnerContainer.Add(
                    setupPanel.PanelCalendar.DaysOfWeekPanel.Background.GetImage(Layer + ".Calendar.Background",
                        Layer + ".DaysOfWeek.Background"));

                var margins_x = setupPanel.PanelCalendar.DaysTable.MarginsHorizontal / 100f;
                var step = (1f - margins_x * 6f) / 7f;

                for (var i = 1; i <= 7; i++)
                {
                    var week = i == 7 ? 0 : i;

                    var weekTitle = GetMessage(((DayOfWeek) week).ToString(), owner.UserIDString);

                    var weekCustomSize = setupPanel.PanelCalendar.DaysOfWeekPanel.Title.FontSize;

                    if (weekTitle.Length > 8)
                        weekCustomSize -= 1;

                    if (weekTitle.Length > 9)
                        weekCustomSize -= 1;

                    if (weekTitle.Length > 10)
                        weekCustomSize -= 1;

                    OwnerContainer.Add(new CuiElement
                    {
                        Parent = Layer + ".DaysOfWeek.Background",
                        Name = Layer + $".DayOfWeek.{i}",
                        Components =
                        {
                            setupPanel.PanelCalendar.DaysOfWeekPanel.Title.GetTextComponent(weekTitle,
                                customFontSize: weekCustomSize),
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"{(i - 1) * step + (i - 1) * margins_x} 0",
                                AnchorMax = $"{i * step + (i - 1) * margins_x} 1",
                                OffsetMin = "0 0", OffsetMax = "0 0"
                            }
                        }
                    });
                }

                #endregion DaysOfWeekPanel

                OwnerContainer.Add(
                    setupPanel.PanelCalendar.DaysTable.Background.GetImage(Layer + ".Calendar.Background",
                        Layer + ".Table.Background"));
                OwnerContainer.Add(
                    setupPanel.PanelCalendar.PanelSchedule.BackgroundMonth.GetImage(Layer + ".Calendar.Background",
                        Layer + ".Month.Background"));
                OwnerContainer.Add(new CuiElement
                {
                    Parent = Layer + ".Month.Background", Name = Layer + ".Month.layer.title", Components =
                    {
                        FullScreenTransform
                    }
                });
                OwnerContainer.AddRange(setupPanel.PanelCalendar.PanelSchedule.ButtonPreviousMonth.GetButton("",
                    Command + " " + "month previous", Layer + ".Month.Background"));
                OwnerContainer.AddRange(setupPanel.PanelCalendar.PanelSchedule.ButtonNextMonth.GetButton("",
                    Command + " " + "month next", Layer + ".Month.Background"));

                OwnerContainer.Add(
                    setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.Background.GetImage(
                        Layer + ".Calendar.Background", Layer + ".ScheduleToDay.Background"));
                OwnerContainer.Add(
                    setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.PanelHeader.Background.GetImage(
                        Layer + ".ScheduleToDay.Background", Layer + ".ScheduleToDay.Title"));
                if (setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.PanelHeader.ShowLine)
                    OwnerContainer.Add(
                        setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.PanelHeader.Line.GetImage(
                            Layer + ".ScheduleToDay.Title", Layer + ".ScheduleToDay.Title.Line"));

                OwnerContainer.Add(
                    setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.ScrollPanel.Background.GetImage(
                        Layer + ".ScheduleToDay.Background", Layer + ".Scroll.Background"));

                OwnerContainer.Add(
                    setupPanel.PanelCalendar.PanelSchedule.ScheduleInfo.Background.GetImage(
                        Layer + ".Calendar.Background", Layer + ".ScheduleInfo.Background"));
                OwnerContainer.Add(
                    setupPanel.PanelCalendar.PanelSchedule.ScheduleInfo.BackgroundInfo.GetImage(
                        Layer + ".ScheduleInfo.Background", Layer + ".ScheduleInfo.PanelInfo"));
                OwnerContainer.Add(
                    setupPanel.PanelCalendar.PanelSchedule.ScheduleInfo.Icon.GetImage(Layer + ".ScheduleInfo.PanelInfo",
                        Layer + ".ScheduleInfo.PanelInfo.icon"));
                OwnerContainer.Add(setupPanel.PanelCalendar.PanelSchedule.ScheduleInfo.TextInfo.CreateText(
                    GetMessage(ScheduleInfo, owner.UserIDString), Layer + ".ScheduleInfo.PanelInfo",
                    Layer + ".ScheduleInfo.PanelInfo.text"));
                OwnerContainer.Add(setupPanel.PanelCalendar.PanelSchedule.ScheduleInfo.BackgroundAnnotation.GetImage(
                    Layer + ".ScheduleInfo.Background", Layer + ".ScheduleInfo.Annotation.Background"));

                #region Markers List

                var size = setupPanel.PanelCalendar.PanelSchedule.ScheduleInfo.SizeMarkerColor;
                size.y /= 2;
                var events = Enum.GetNames(typeof(TypeEvent));

                var countLine = (int) (events.Length / 2f);
                if (countLine == 0) countLine = 1;
                else if (countLine < events.Length / 2f) countLine++;

                step = 1f / (countLine + 1);

                int x = 0, y = 0;
                for (var i = 0; i < events.Length; i++)
                {
                    if (x == 2)
                    {
                        x = 0;
                        y++;
                    }

                    var color = Instance.EventsColors.TryGetValue((TypeEvent) i, out var col) ? col.Get() : "0 0 0 0";

                    OwnerContainer.Add(new CuiElement
                    {
                        Parent = Layer + ".ScheduleInfo.Annotation.Background",
                        Name = Layer + $".ScheduleInfo.Annotation.color{i}",
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = color,
                                Sprite = "assets/content/ui/ui.background.tile.psd",
                                Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"{x * 0.5f} {1f - (step * y + step)}",
                                AnchorMax = $"{x * 0.5f} {1f - (step * y + step)}",
                                OffsetMin = $"0 -{size.y}",
                                OffsetMax = $"{size.x} {size.y}"
                            }
                        }
                    });
                    OwnerContainer.Add(setupPanel.PanelCalendar.PanelSchedule.ScheduleInfo.Annotation.CreateText(
                        GetMessage(events[i], owner.UserIDString), Layer + $".ScheduleInfo.Annotation.color{i}"));
                    x++;
                }

                #endregion

                AddMonthTitle();
                AddTableDate();
                AddScheduleCurrentDay();
            }

            private void AddDescriptionEvent(int i)
            {
                var targetSchedule = scheduleOfSelectedDay[i];

                OwnerContainer.Add(new CuiElement
                {
                    Name = Layer + ".Event.Description.Shadow",
                    DestroyUi = Layer + ".Event.Description.Shadow",
                    Parent = Layer + ".Scroll.View",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.ScrollPanel.ContentScroll
                                .ColorSchedule.Get(),
                            Material = "assets/content/ui/menuui/mainmenu.modal.mat"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1"
                        }
                    }
                });

                float margins_x = setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.ScrollPanel.ContentScroll
                        .MarginsInScroll.x,
                    margins_y = setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.ScrollPanel.ContentScroll
                        .MarginsInScroll.y,
                    h = setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.ScrollPanel.ContentScroll.Height;

                OwnerContainer.Add(new CuiElement
                {
                    Parent = Layer + ".Scroll.View",
                    Name = Layer + "Event.Description.Background",
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "1 1",
                            OffsetMin = $"{margins_x} -{h * i + margins_y * i + h + margins_y}",
                            OffsetMax = $"-{margins_x} -{h * i + margins_y * i + margins_y}"
                        }
                    }
                });

                OwnerContainer.AddRange(
                    setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.ScrollPanel.ContentScroll.ButtonInfo.GetButton(
                        "", Command + " " + "info" + " " + i, Layer + "Event.Description.Background",
                        Layer + "Event.Description.layer"));

                OwnerContainer.Add(new CuiElement
                {
                    Parent = Layer + "Event.Description.layer",
                    Name = Layer + "Event.Description.panel",
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin =
                                $"-{setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.ScrollPanel.ContentScroll.PanelDescriptionLength} -{setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.ScrollPanel.ContentScroll.PanelDescriptionHeight}",
                            OffsetMax = "0 0"
                        }
                    }
                });
                OwnerContainer.Add(
                    setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.ScrollPanel.ContentScroll.TitlePanelDescription
                        .GetImage(Layer + "Event.Description.panel", Layer + "Event.Description.Title.panel"));
                OwnerContainer.Add(
                    setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.ScrollPanel.ContentScroll
                        .DescriptionPanelBackground.GetImage(Layer + "Event.Description.panel",
                            Layer + "Event.Description.Description.panel"));
                OwnerContainer.Add(
                    setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.ScrollPanel.ContentScroll.TitleEventDescription
                        .CreateText(
                            GetMessage(
                                string.IsNullOrEmpty(targetSchedule.Name)
                                    ? targetSchedule.EventType.ToString()
                                    : targetSchedule.Name, owner.UserIDString),
                            Layer + "Event.Description.Title.panel"));
                OwnerContainer.Add(
                    setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.ScrollPanel.ContentScroll.Description
                        .CreateText(GetMessage(targetSchedule.Descriptions, owner.UserIDString),
                            Layer + "Event.Description.Description.panel"));
            }

            private void AddMonthTitle()
            {
                OwnerContainer.Add(setupPanel.PanelCalendar.PanelSchedule.TitleMonth.CreateText(
                    GetMessage(Instance.MonthNames[selectedMonth - 1], owner.UserIDString) + " " + selectedYear,
                    Layer + ".Month.layer.title", Layer + ".Month.Title", Layer + ".Month.Title"));
            }

            protected void AddScheduleCurrentDay()
            {
                OwnerContainer.Add(setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.PanelHeader.Number.CreateText(
                    selectedDateTime.Day + "", Layer + ".ScheduleToDay.Title", Layer + ".ScheduleToDay.Title.Number",
                    Layer + ".ScheduleToDay.Title.Number"));
                OwnerContainer.Add(setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.PanelHeader.Month.CreateText(
                    GetMessage(Instance.MonthNames[selectedDateTime.Month - 1], owner.UserIDString),
                    Layer + ".ScheduleToDay.Title", Layer + ".ScheduleToDay.Title.Mont",
                    Layer + ".ScheduleToDay.Title.Mont"));
                OwnerContainer.Add(setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.PanelHeader.DayOfWeek.CreateText(
                    GetMessage(selectedDateTime.DayOfWeek.ToString(), owner.UserIDString),
                    Layer + ".ScheduleToDay.Title", Layer + ".ScheduleToDay.Title.DayOfWeek",
                    Layer + ".ScheduleToDay.Title.DayOfWeek"));

                float margins_x = setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.ScrollPanel.ContentScroll
                        .MarginsInScroll.x,
                    margins_y = setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.ScrollPanel.ContentScroll
                        .MarginsInScroll.y,
                    h = setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.ScrollPanel.ContentScroll.Height;
                var height = h * scheduleOfSelectedDay.Count + margins_y * scheduleOfSelectedDay.Count + margins_y;
                height += setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.ScrollPanel.ContentScroll
                    .PanelDescriptionHeight;
                if (height < setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.ScrollPanel.ScrollView.MinHeight)
                    height = setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.ScrollPanel.ScrollView.MinHeight;
                CuiRectTransformComponent transformContent = new()
                    {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -{height}", OffsetMax = "0 0"};

                OwnerContainer.Add(new CuiElement
                {
                    Parent = Layer + ".Scroll.Background",
                    Name = Layer + ".Scroll.View",
                    DestroyUi = Layer + ".Scroll.View",
                    Components =
                    {
                        setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.ScrollPanel.ScrollView.GetScrollView(
                            transformContent),
                        FullScreenTransform
                    }
                });

                for (var i = 0; i < scheduleOfSelectedDay.Count; i++)
                {
                    var targetSchedule = scheduleOfSelectedDay[i];

                    OwnerContainer.Add(new CuiElement
                    {
                        Parent = Layer + ".Scroll.View",
                        Name = Layer + $".Scroll.Content.Event.{i}",
                        Components =
                        {
                            setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.ScrollPanel.ContentScroll
                                .BackgroundContent.GetImageComponent(),
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 1", AnchorMax = "1 1",
                                OffsetMin = $"{margins_x} -{h * i + margins_y * i + h + margins_y}",
                                OffsetMax = $"-{margins_x} -{h * i + margins_y * i + margins_y}"
                            }
                        }
                    });

                    #region Icon

                    var imageComponent =
                        !string.IsNullOrEmpty(targetSchedule.UrlIcon) && targetSchedule.UrlIcon.StartsWith("http")
                            ? new CuiRawImageComponent
                                {Color = "1 1 1 1", Png = Instance.GetPng(targetSchedule.UrlIcon)}
                            : Instance.IconSetup.TryGetValue(targetSchedule.EventType, out var icon)
                                ? icon.GetImageComponent()
                                : null;
                    if (imageComponent != null)
                        OwnerContainer.Add(new CuiElement
                        {
                            Parent = Layer + $".Scroll.Content.Event.{i}",
                            Name = Layer + $".Scroll.Content.Event.Icon.{i}",
                            Components =
                            {
                                imageComponent,
                                setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.ScrollPanel.ContentScroll
                                    .IconTransform.GetRectTransform()
                            }
                        });

                    #endregion

                    OwnerContainer.Add(
                        setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.ScrollPanel.ContentScroll.TimeEvent
                            .CreateText(targetSchedule.DateTimeEvent.ToStringTime_HM(),
                                Layer + $".Scroll.Content.Event.{i}"));
                    OwnerContainer.Add(
                        setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.ScrollPanel.ContentScroll.TitleEvent
                            .CreateText(
                                GetMessage(
                                    string.IsNullOrEmpty(targetSchedule.Name)
                                        ? targetSchedule.EventType.ToString()
                                        : targetSchedule.Name, owner.UserIDString),
                                Layer + $".Scroll.Content.Event.{i}"));

                    OwnerContainer.AddRange(
                        setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.ScrollPanel.ContentScroll.ButtonInfo
                            .GetButton(string.Empty, Command + " " + "info" + " " + i,
                                Layer + $".Scroll.Content.Event.{i}",
                                customRect: setupPanel.PanelCalendar.PanelSchedule.PanelShowDay.ScrollPanel
                                    .ContentScroll.ButtonInfo.GetRectTransform()));
                }
            }

            protected void AddTableDate()
            {
                var monthDateTime = new DateTime(selectedYear, selectedMonth, 1);
                int x = 0, y = 0;
                float margins_x = setupPanel.PanelCalendar.DaysTable.MarginsHorizontal / 100f,
                    margins_y = setupPanel.PanelCalendar.DaysTable.MarginsVertical / 100f;
                var step_x = (1f - margins_x * 6f) / 7f;
                var step_y = (1f - margins_y * 5f) / 6f;
                var _week = monthDateTime.DayOfWeek;
                var start = _week == DayOfWeek.Sunday ? -6 : 1 - (int) _week;
                for (var i = 0; i < 42; i++)
                {
                    var tableNum = i + start;
                    if (x == 7)
                    {
                        y++;
                        x = 0;
                    }

                    var dateTime = monthDateTime.AddDays(tableNum);
                    var day = dateTime.Day.ToString();
                    TryGetColorPriority(dateTime, out var color);


                    var background = setupPanel.PanelCalendar.DaysTable.BackgroundDayTableInside;

                    if (tableNum < 0 ||
                        tableNum >= calendar.GetDaysInMonth(selectedDateTime.Year, selectedDateTime.Month))
                        background = setupPanel.PanelCalendar.DaysTable.BackgroundDayTableOutside;

                    OwnerContainer.AddRange(background.GetButton(day, Command + " " + $".use.day {tableNum}",
                        Layer + ".Table.Background", Layer + $".DayTable.{i}", Layer + $".DayTable.{i}",
                        customButtonColor: color?.Get(),
                        customTextColor: HexToCuiColor(color?.HEX ?? "#FFFFFF"),
                        customRect: new CuiRectTransformComponent
                        {
                            AnchorMin =
                                $"{x * step_x + x * margins_x} {1f - (y * step_y + step_y + y * margins_y)}",
                            AnchorMax =
                                $"{x * step_x + step_x + x * margins_x} {1f - (y * step_y + y * margins_y)}",
                            OffsetMin = "0 0", OffsetMax = "0 0"
                        }));

                    x++;
                }
            }

            protected void UpdateScheduleMonth(DateTime dateTime)
            {
                // scheduleOfSelectedMonth = Instance.Schedule.FindAll(x => x.HasEventOfMonth(dateTime)) ?? new();

                scheduleOfSelectedMonth.Clear();
                foreach (var item in Instance.Schedule) item.SetScheduleOfMonth(dateTime, scheduleOfSelectedMonth);
                foreach (var item in Instance.ScheduleEventsManager)
                    item.SetScheduleOfMonth(dateTime, scheduleOfSelectedMonth);
            }

            protected void UpdateScheduleDay(DateTime dateTime)
            {
                scheduleOfSelectedDay = scheduleOfSelectedMonth.FindAll(x => x.HasEventOfDay(dateTime)) ??
                                        new List<ScheduleToDay>();
                scheduleOfSelectedDay.Sort((x, y) => x.DateTimeEvent.CompareTo(y.DateTimeEvent));
            }

            protected bool TryGetColorPriority(DateTime dateTime, out IColor color)
            {
                color = null;

                var days = scheduleOfSelectedMonth.FindAll(x => x.HasEventOfDay(dateTime));
                if (days.Count == 0)
                    return false;

                var targetDay = days[0];
                if (targetDay.Color != null)
                {
                    color = targetDay.Color;
                    return true;
                }

                Instance.EventsColors.TryGetValue(targetDay.EventType, out color);

                return color != null;
            }

            public virtual void ProcessingCommand(string[] args)
            {
                if (isOpenDescription)
                {
                    isOpenDescription = false;

                    CuiHelper.DestroyUi(owner, Layer + ".Event.Description.Shadow");
                    CuiHelper.DestroyUi(owner, Layer + "Event.Description.Background");

                    if (args[0] == "info") return;
                }

                switch (args[0])
                {
                    case "close":
                        CuiHelper.DestroyUi(owner, Layer);
                        Pool.FreeUnmanaged(ref scheduleOfSelectedMonth);
                        Remove(owner);
                        return;
                    case "month":
                    {
                        if (args.Length != 2) return;

                        switch (args[1])
                        {
                            case "previous":
                            {
                                if (selectedMonth == 1)
                                {
                                    selectedYear--;
                                    selectedMonth = 12;
                                }
                                else
                                {
                                    selectedMonth--;
                                }

                                break;
                            }

                            case "next":
                            {
                                if (selectedMonth == 12)
                                {
                                    selectedYear++;
                                    selectedMonth = 1;
                                }
                                else
                                {
                                    selectedMonth++;
                                }

                                break;
                            }
                            default:
                                return;
                        }

                        UpdateScheduleMonth(new DateTime(selectedYear, selectedMonth, 1));

                        UpdateUI(() =>
                        {
                            AddMonthTitle();
                            AddTableDate();
                        });
                        return;
                    }
                    case ".use.day":
                    {
                        if (args.Length != 2 || !int.TryParse(args[1], out var num)) return;

                        var monthDateTime = new DateTime(selectedYear, selectedMonth, 1);
                        selectedDateTime = monthDateTime.AddDays(num);
                        UpdateScheduleDay(selectedDateTime);

                        UpdateUI(() => { AddScheduleCurrentDay(); });
                        return;
                    }
                    case "info":
                    {
                        if (args.Length < 2 || !int.TryParse(args[1], out var index) ||
                            scheduleOfSelectedDay.Count <= index) return;

                        isOpenDescription = true;

                        UpdateUI(() => { AddDescriptionEvent(index); });
                        return;
                    }
                    default: return;
                }
            }

            public static void OnClickButton(BasePlayer player, string[] args)
            {
                if (PlayersUI.TryGetValue(player.UserIDString, out var ui)) ui.ProcessingCommand(args);
            }

            public static CuiElementContainer GetContainer(BasePlayer player, TypeUI typeUI)
            {
                if (Instance.permission.UserHasPermission(player.UserIDString, PermissionUse))
                    return new AdminUIController(player, typeUI).OwnerContainer;
                return new PlayerUIController(player, typeUI).OwnerContainer;
            }

            public static void Remove(BasePlayer player)
            {
                PlayersUI?.Remove(player.UserIDString);
            }

            public static void Unload()
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(player, Layer);
                    CuiHelper.DestroyUi(player, LayerGeneral + ".SetupSchedule");
                }

                PlayersUI = null;
            }

            public static void Init()
            {
                PlayersUI = new Dictionary<string, PlayerUIController>();
            }

            public enum TypeUI
            {
                FullScreen,
                ServerMenu
            }

            #region Helpers

            public void UpdateUI(Action callback = null)
            {
                OwnerContainer.Clear();

                callback?.Invoke();

                CuiHelper.AddUi(owner, OwnerContainer);
            }

            #endregion
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum PatternServerMenu
        {
            V1,
            V2
        }

        #endregion

        #region UI Elements Configuration Classes

        public class TextBackground : TextSettings
        {
            [JsonIgnore] private List<CuiElement> _cacheList = new();

            [JsonProperty(LangRu ? "Фон" : "Background")]
            public ImageSettings Background = new();

            public List<CuiElement> GetTextBackground(string text,
                string parentLayer,
                string nameLayer,
                string nameTextLayer = null,
                string destroyLayer = null)
            {
                _cacheList.Clear();
                _cacheList.Add(Background.GetImage(parentLayer, nameLayer, destroyLayer));
                _cacheList.Add(CreateText(text, nameLayer, nameTextLayer, nameTextLayer));
                return _cacheList;
            }
        }

        public class TitleElement : TextSettings
        {
            [JsonProperty(LangRu ? "Использовать текст?" : "Should I use the text?")]
            public bool CanUseTitle = false;

            public void AddTitle(List<CuiElement> list, string parent, string name, string destroy, string text)
            {
                if (!CanUseTitle) return;
                list.Add(CreateText(text, parent, name, destroy));
            }
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum ScrollType
        {
            Horizontal,
            Vertical
        }

        public class ScrollViewUI
        {
            #region Fields

            [JsonProperty(PropertyName = "Scroll Type (Horizontal, Vertical)")]
            public ScrollType ScrollType;

            [JsonProperty(PropertyName = "Movement Type (Unrestricted, Elastic, Clamped)")]
            [JsonConverter(typeof(StringEnumConverter))]
            public ScrollRect.MovementType MovementType;

            [JsonProperty(PropertyName = "Elasticity")]
            public float Elasticity;

            [JsonProperty(PropertyName = "Deceleration Rate")]
            public float DecelerationRate;

            [JsonProperty(PropertyName = "Scroll Sensitivity")]
            public float ScrollSensitivity;

            [JsonProperty(PropertyName = "Minimal Height")]
            public float MinHeight;

            [JsonProperty(PropertyName = "Additional Height")]
            public float AdditionalHeight;

            [JsonProperty(PropertyName = "Scrollbar Settings")]
            public ScrollBarSettings Scrollbar = new();

            #endregion

            #region Public Methods

            public CuiScrollViewComponent GetScrollView(float totalWidth)
            {
                return GetScrollView(CalculateContentRectTransform(totalWidth));
            }

            public CuiScrollViewComponent GetScrollView(CuiRectTransform contentTransform)
            {
                var cuiScrollView = new CuiScrollViewComponent
                {
                    MovementType = MovementType,
                    Elasticity = Elasticity,
                    DecelerationRate = DecelerationRate,
                    ScrollSensitivity = ScrollSensitivity,
                    ContentTransform = contentTransform,
                    Inertia = true
                };

                switch (ScrollType)
                {
                    case ScrollType.Vertical:
                    {
                        cuiScrollView.Vertical = true;
                        cuiScrollView.Horizontal = false;

                        cuiScrollView.VerticalScrollbar = Scrollbar.Get();
                        break;
                    }

                    case ScrollType.Horizontal:
                    {
                        cuiScrollView.Horizontal = true;
                        cuiScrollView.Vertical = false;

                        cuiScrollView.HorizontalScrollbar = Scrollbar.Get();
                        break;
                    }
                }

                return cuiScrollView;
            }

            public CuiRectTransform CalculateContentRectTransform(float totalWidth)
            {
                CuiRectTransform contentRect;
                if (ScrollType == ScrollType.Horizontal)
                    contentRect = new CuiRectTransform
                    {
                        AnchorMin = "0 0", AnchorMax = "0 1",
                        OffsetMin = "0 0",
                        OffsetMax = $"{totalWidth} 0"
                    };
                else
                    contentRect = new CuiRectTransform
                    {
                        AnchorMin = "0 1", AnchorMax = "1 1",
                        OffsetMin = $"0 -{totalWidth}",
                        OffsetMax = "0 0"
                    };

                return contentRect;
            }

            #endregion

            #region Classes

            public class ScrollBarSettings
            {
                #region Fields

                [JsonProperty(PropertyName = "Invert")]
                public bool Invert;

                [JsonProperty(PropertyName = "Auto Hide")]
                public bool AutoHide;

                [JsonProperty(PropertyName = "Handle Sprite")]
                public string HandleSprite = string.Empty;

                [JsonProperty(PropertyName = "Size")] public float Size;

                [JsonProperty(PropertyName = "Handle Color")]
                public IColor HandleColor = IColor.CreateWhite();

                [JsonProperty(PropertyName = "Highlight Color")]
                public IColor HighlightColor = IColor.CreateWhite();

                [JsonProperty(PropertyName = "Pressed Color")]
                public IColor PressedColor = IColor.CreateWhite();

                [JsonProperty(PropertyName = "Track Sprite")]
                public string TrackSprite = string.Empty;

                [JsonProperty(PropertyName = "Track Color")]
                public IColor TrackColor = IColor.CreateWhite();

                #endregion

                #region Public Methods

                public CuiScrollbar Get()
                {
                    var cuiScrollbar = new CuiScrollbar
                    {
                        Size = Size
                    };

                    if (Invert) cuiScrollbar.Invert = Invert;
                    if (AutoHide) cuiScrollbar.AutoHide = AutoHide;
                    if (!string.IsNullOrEmpty(HandleSprite)) cuiScrollbar.HandleSprite = HandleSprite;
                    if (!string.IsNullOrEmpty(TrackSprite)) cuiScrollbar.TrackSprite = TrackSprite;

                    if (HandleColor != null) cuiScrollbar.HandleColor = HandleColor.Get();
                    if (HighlightColor != null) cuiScrollbar.HighlightColor = HighlightColor.Get();
                    if (PressedColor != null) cuiScrollbar.PressedColor = PressedColor.Get();
                    if (TrackColor != null) cuiScrollbar.TrackColor = TrackColor.Get();

                    return cuiScrollbar;
                }

                #endregion
            }

            #endregion
        }

        public class ImageSettings : InterfacePosition
        {
            #region Fields

            [JsonProperty(PropertyName = "Sprite")]
            public string Sprite;

            [JsonProperty(PropertyName = "Material")]
            public string Material;

            [JsonProperty(PropertyName = "Image")] public string Image = string.Empty;

            [JsonProperty(PropertyName = "Color")] public IColor Color = IColor.CreateTransparent();

            [JsonProperty(PropertyName = "Cursor Enabled")]
            public bool CursorEnabled = false;

            [JsonProperty(PropertyName = "Keyboard Enabled")]
            public bool KeyboardEnabled = false;

            #endregion

            #region Private Methods

            [JsonIgnore] private ICuiComponent _imageCache;

            public ICuiComponent GetImageComponent(string customColor = null)
            {
                if (!string.IsNullOrEmpty(Image))
                {
                    var rawImage = new CuiRawImageComponent
                    {
                        Png = Instance.GetPng(Image),
                        Color = customColor ?? Color.Get()
                    };

                    if (!string.IsNullOrEmpty(Sprite))
                        rawImage.Sprite = Sprite;

                    if (!string.IsNullOrEmpty(Material))
                        rawImage.Material = Material;

                    _imageCache = rawImage;
                }
                else
                {
                    var image = new CuiImageComponent
                    {
                        Color = customColor ?? Color.Get()
                    };

                    if (!string.IsNullOrEmpty(Sprite))
                        image.Sprite = Sprite;

                    if (!string.IsNullOrEmpty(Material))
                        image.Material = Material;

                    _imageCache = image;
                }

                return _imageCache;
            }

            public ICuiComponent CreateImageComponent()
            {
                ICuiComponent component;
                if (!string.IsNullOrEmpty(Image))
                    component = new CuiRawImageComponent
                    {
                        Png = Instance.GetPng(Image),
                        Color = Color.Get(),
                        Sprite = Sprite,
                        Material = Material
                    };
                else
                    component = new CuiImageComponent
                    {
                        Color = Color.Get(),
                        Sprite = Sprite,
                        Material = Material
                    };

                return component;
            }

            #endregion

            #region Public Methods

            public bool TryGetImageURL(out string url)
            {
                if (!string.IsNullOrWhiteSpace(Image) && Image.IsURL())
                {
                    url = Image;
                    return true;
                }

                url = null;
                return false;
            }

            public CuiElement GetImage(
                string parent,
                string name = null,
                string destroyUI = null,
                string customColor = null)
            {
                if (string.IsNullOrEmpty(name)) name = CuiHelper.GetGuid();
                var image = new CuiElement
                {
                    Name = name, Parent = parent, DestroyUi = destroyUI,
                    Components = {GetRectTransform(), GetImageComponent(customColor)}
                };
                if (CursorEnabled) image.Components.Add(new CuiNeedsCursorComponent());
                if (KeyboardEnabled) image.Components.Add(new CuiNeedsKeyboardComponent());

                return image;
            }

            public CuiElement CreateImage(string parent, string name, string destroyUI)
            {
                if (string.IsNullOrEmpty(name)) name = CuiHelper.GetGuid();
                var image = new CuiElement
                {
                    Parent = parent, Name = name, DestroyUi = destroyUI, Components =
                    {
                        GetRectTransform(), CreateImageComponent()
                    }
                };
                return image;
            }

            #endregion

            #region Constructors

            public ImageSettings()
            {
            }

            public ImageSettings(string imageURL, IColor color, InterfacePosition position) : base(position)
            {
                Image = imageURL;
                Color = color;
            }

            #endregion
        }

        public class ButtonSettings : TextSettings
        {
            #region Fields

            [JsonProperty(PropertyName = "Button Color")]
            public IColor ButtonColor = IColor.CreateWhite();

            [JsonProperty(PropertyName = "Sprite")]
            public string Sprite = string.Empty;

            [JsonProperty(PropertyName = "Material")]
            public string Material = string.Empty;

            [JsonProperty(PropertyName = "Image")] public string Image = string.Empty;

            [JsonProperty(PropertyName = "Image Color")]
            public IColor ImageColor = IColor.CreateWhite();

            [JsonProperty(PropertyName = "Use custom image position settings?")]
            public bool UseCustomPositionImage = false;

            [JsonProperty(PropertyName = "Custom image position settings")]
            public InterfacePosition ImagePosition = CreateFullStretch();

            #endregion

            #region Public Methods

            public bool TryGetImageURL(out string url)
            {
                if (!string.IsNullOrWhiteSpace(Image) && Image.IsURL())
                {
                    url = Image;
                    return true;
                }

                url = null;
                return false;
            }

            public List<CuiElement> GetButton(
                string msg,
                string cmd,
                string parent,
                string name = null,
                string destroyUI = null,
                string close = null,
                string customButtonColor = null,
                string customTextColor = null,
                CuiRectTransformComponent customRect = null)
            {
                if (string.IsNullOrEmpty(name))
                    name = CuiHelper.GetGuid();

                var list = new List<CuiElement>();

                var btn = new CuiButtonComponent
                {
                    Color = customButtonColor ?? ButtonColor?.Get()
                };

                if (!string.IsNullOrEmpty(cmd))
                    btn.Command = cmd;

                if (!string.IsNullOrEmpty(close))
                    btn.Close = close;

                if (!string.IsNullOrEmpty(Sprite))
                    btn.Sprite = Sprite;

                if (!string.IsNullOrEmpty(Material))
                    btn.Material = Material;

                list.Add(new CuiElement
                {
                    Name = name,
                    Parent = parent,
                    DestroyUi = destroyUI,
                    Components =
                    {
                        btn,
                        customRect ?? GetRectTransform()
                    }
                });

                if (!string.IsNullOrEmpty(Image))
                {
                    list.Add(new CuiElement
                    {
                        Parent = name,
                        Components =
                        {
                            Image.StartsWith("assets/")
                                ? new CuiImageComponent {Color = ImageColor.Get(), Sprite = Image}
                                : new CuiRawImageComponent {Color = ImageColor.Get(), Png = Instance.GetPng(Image)},

                            UseCustomPositionImage && ImagePosition != null
                                ? ImagePosition?.GetRectTransform()
                                : new CuiRectTransformComponent()
                        }
                    });
                }
                else
                {
                    if (!string.IsNullOrEmpty(msg))
                        list.Add(new CuiElement
                        {
                            Parent = name,
                            Components =
                            {
                                GetTextComponent(msg, customTextColor),
                                new CuiRectTransformComponent()
                            }
                        });
                }

                return list;
            }

            #endregion
        }

        public class TextSettings : InterfacePosition
        {
            protected const int orderText = order + 100;

            #region Fields

            [JsonProperty(PropertyName = "Font Size", Order = orderText)]
            public int FontSize = 12;

            [JsonProperty(PropertyName = "Is Bold?", Order = orderText + 1)]
            public bool IsBold;

            [JsonConverter(typeof(StringEnumConverter))]
            [JsonProperty(
                LangRu
                    ? "Выравнивание (UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight)"
                    : "Align (UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight)",
                Order = orderText + 2)]
            public TextAnchor Align;

            [JsonProperty(PropertyName = "Color", Order = orderText + 3)]
            public IColor Color = IColor.CreateWhite();

            #endregion Fields

            #region Public Methods

            public CuiTextComponent GetTextComponent(string msg, string customTextColor = null,
                int? customFontSize = null)
            {
                return new CuiTextComponent
                {
                    Text = msg ?? string.Empty,
                    FontSize = customFontSize ?? FontSize,
                    Font = IsBold ? "robotocondensed-bold.ttf" : "robotocondensed-regular.ttf",
                    Align = Align,
                    Color = customTextColor ?? Color?.Get() ?? "1 1 1 1",
                    VerticalOverflow = VerticalWrapMode.Overflow
                };
            }

            public CuiElement CreateText(
                string msg,
                string parent,
                string name = null,
                string destroyUI = null)
            {
                if (string.IsNullOrEmpty(name))
                    name = CuiHelper.GetGuid();
                return new CuiElement
                {
                    Parent = parent, Name = name, DestroyUi = destroyUI,
                    Components = {GetRectTransform(), GetTextComponent(msg)}
                };
            }

            #endregion
        }

        public class InterfacePosition
        {
            #region Fields

            protected const int order = 0;

            [JsonProperty("AnchorMin", Order = order)]
            public string AnchorMin = "0 0";

            [JsonProperty("AnchorMax", Order = order + 1)]
            public string AnchorMax = "1 1";

            [JsonProperty("OffsetMin", Order = order + 2)]
            public string OffsetMin = "0 0";

            [JsonProperty("OffsetMax", Order = order + 3)]
            public string OffsetMax = "0 0";

            #endregion

            #region Cache

            [JsonIgnore] private CuiRectTransformComponent _position;

            #endregion

            #region Public Methods

            public CuiRectTransformComponent GetRectTransform()
            {
                if (_position != null) return _position;

                var rect = new CuiRectTransformComponent();

                if (!string.IsNullOrEmpty(AnchorMin))
                    rect.AnchorMin = AnchorMin;

                if (!string.IsNullOrEmpty(AnchorMax))
                    rect.AnchorMax = AnchorMax;

                if (!string.IsNullOrEmpty(OffsetMin))
                    rect.OffsetMin = OffsetMin;

                if (!string.IsNullOrEmpty(OffsetMax))
                    rect.OffsetMax = OffsetMax;

                _position = rect;

                return _position;
            }

            #endregion

            #region Constructors

            public InterfacePosition()
            {
            }

            public InterfacePosition(InterfacePosition other)
            {
                AnchorMin = other.AnchorMin;
                AnchorMax = other.AnchorMax;
                OffsetMin = other.OffsetMin;
                OffsetMax = other.OffsetMax;
            }

            public static InterfacePosition CreatePosition(float aMinX, float aMinY, float aMaxX, float aMaxY,
                float oMinX, float oMinY, float oMaxX, float oMaxY)
            {
                return new InterfacePosition
                {
                    AnchorMin = $"{aMinX} {aMinY}",
                    AnchorMax = $"{aMaxX} {aMaxY}",
                    OffsetMin = $"{oMinX} {oMinY}",
                    OffsetMax = $"{oMaxX} {oMaxY}"
                };
            }

            public static InterfacePosition CreatePosition(
                string anchorMin = "0 0",
                string anchorMax = "1 1",
                string offsetMin = "0 0",
                string offsetMax = "0 0")
            {
                return new InterfacePosition
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax,
                    OffsetMin = offsetMin,
                    OffsetMax = offsetMax
                };
            }

            public static InterfacePosition CreatePosition(CuiRectTransform rectTransform)
            {
                return new InterfacePosition
                {
                    AnchorMin = rectTransform.AnchorMin,
                    AnchorMax = rectTransform.AnchorMax,
                    OffsetMin = rectTransform.OffsetMin,
                    OffsetMax = rectTransform.OffsetMax
                };
            }

            public static InterfacePosition CreateFullStretch()
            {
                return new InterfacePosition
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1",
                    OffsetMin = "0 0",
                    OffsetMax = "0 0"
                };
            }

            public static InterfacePosition CreateCenter()
            {
                return new InterfacePosition
                {
                    AnchorMin = "0.5 0.5",
                    AnchorMax = "0.5 0.5",
                    OffsetMin = "0 0",
                    OffsetMax = "0 0"
                };
            }

            #endregion Constructors
        }

        public class IColor
        {
            #region Fields

            [JsonProperty(PropertyName = "HEX")] public string HEX;

            [JsonProperty(PropertyName = LangRu ? "Непрозрачность (0 - 100)" : "Opacity (0 - 100)")]
            public float Alpha;

            #endregion

            #region Public Methods

            [JsonIgnore] private string _cachedResult;

            [JsonIgnore] private bool _isCached;

            public string Get()
            {
                if (_isCached)
                    return _cachedResult;

                UpdateCache();

                return _cachedResult;
            }

            public void UpdateCache()
            {
                if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

                var str = HEX.Trim('#');
                if (str.Length != 6)
                    throw new Exception(HEX);

                var r = byte.Parse(str.AsSpan(0, 2), NumberStyles.HexNumber);
                var g = byte.Parse(str.AsSpan(2, 2), NumberStyles.HexNumber);
                var b = byte.Parse(str.AsSpan(4, 2), NumberStyles.HexNumber);

                _cachedResult = $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100}";
                _isCached = true;
            }

            #endregion

            #region Constructors

            public IColor()
            {
            }

            public IColor(string hex, float alpha = 100)
            {
                HEX = hex;
                Alpha = alpha;
            }

            public static IColor Create(string hex, float alpha = 100)
            {
                return new IColor(hex, alpha);
            }

            public static IColor CreateTransparent()
            {
                return new IColor("#000000", 0);
            }

            public static IColor CreateWhite()
            {
                return new IColor("#FFFFFF");
            }

            public static IColor CreateBlack()
            {
                return new IColor("#000000");
            }

            #endregion
        }

        #endregion
    }
}

namespace Oxide.Plugins.WipeScheduleEx
{
    public static class ExtensionMethods
    {
        public static float Scale(this float oldValue, float oldMin, float oldMax, float newMin, float newMax)
        {
            var oldRange = oldMax - oldMin;
            var newRange = newMax - newMin;
            var newValue = (oldValue - oldMin) * newRange / oldRange + newMin;

            return newValue;
        }

        public static int Scale(this int oldValue, int oldMin, int oldMax, int newMin, int newMax)
        {
            var oldRange = oldMax - oldMin;
            var newRange = newMax - newMin;
            var newValue = (oldValue - oldMin) * newRange / oldRange + newMin;

            return newValue;
        }

        public static long Scale(this long oldValue, long oldMin, long oldMax, long newMin, long newMax)
        {
            var oldRange = oldMax - oldMin;
            var newRange = newMax - newMin;
            var newValue = (oldValue - oldMin) * newRange / oldRange + newMin;

            return newValue;
        }

        public static bool IsHex(this string s)
        {
            return s.Length == 6 && Regex.IsMatch(s, "^[0-9A-Fa-f]+$");
        }

        public static string ToStringDate_DMY(this DateTime dateTime)
        {
            return $"{dateTime.Day:00}.{dateTime.Month:00}.{dateTime.Year:0000}";
        }

        public static string ToStringDateTime_DMYHM(this DateTime dateTime)
        {
            return
                $"{dateTime.Day:00}.{dateTime.Month:00}.{dateTime.Year:0000} {dateTime.Hour:00}:{dateTime.Minute:00}";
        }

        public static string ToStringTime_HM(this DateTime dateTime)
        {
            return $"{dateTime.Hour:00}:{dateTime.Minute:00}";
        }

        public static string ToStringTime_HM(this WipeSchedule.MyTime time)
        {
            return $"{time.Hour:00}:{time.Minute:00}";
        }

        public static bool IsURL(this string uriName)
        {
            return Uri.TryCreate(uriName, UriKind.Absolute, out var uriResult) &&
                   (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        public static TResult Min<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector)
        {
            if (source == null) throw new Exception();

            if (selector == null) throw new Exception();

            var @default = Comparer<TResult>.Default;
            var val = default(TResult);
            if (val == null)
            {
                using var enumerator = source.GetEnumerator();
                do
                {
                    if (!enumerator.MoveNext()) return val;

                    val = selector(enumerator.Current);
                } while (val == null);

                while (enumerator.MoveNext())
                {
                    var val2 = selector(enumerator.Current);
                    if (val2 != null && @default.Compare(val2, val) < 0) val = val2;
                }
            }
            else
            {
                using var enumerator2 = source.GetEnumerator();
                if (!enumerator2.MoveNext()) throw new Exception();

                val = selector(enumerator2.Current);
                while (enumerator2.MoveNext())
                {
                    var val3 = selector(enumerator2.Current);
                    if (@default.Compare(val3, val) < 0) val = val3;
                }
            }

            return val;
        }

        public static TSource ElementAt<TSource>(this IEnumerable<TSource> source, int index)
        {
            var movements = 0;
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (movements == index) return enumerator.Current;
                    movements++;
                }
            }

            return default;
        }

        public static bool Exist<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            var result = false;
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                    if (predicate(enumerator.Current))
                    {
                        result = true;
                        break;
                    }
            }

            ;
            return result;
        }

        public static void SetArray<T>(this T[] origin, T[] value, T def)
        {
            var length = value == null ? 0 : value.Length;
            for (var i = 0; i < origin.Length; i++)
                if (i < length) origin[i] = value[i];
                else origin[i] = def;
        }

        public static void SetArray<T>(this T[] origin, T[] value, Func<int, T> def)
        {
            var length = value == null ? 0 : value.Length;
            for (var i = 0; i < origin.Length; i++)
                if (i < length) origin[i] = value[i];
                else origin[i] = def.Invoke(i);
        }

        public static void ExtractObjects<T>(this object obj, List<T> value) where T : class
        {
            ExtractObjectsRecursive(obj, value);
        }

        public static void ExtractObjectsRecursive<T>(this object obj, List<T> imageSettingsList)
        {
            if (obj == null) return;

            var type = obj.GetType();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

            // Проверяем свойства
            foreach (var property in properties)
            {
                var value = property.GetValue(obj);
                if (value is T imageSettings)
                    imageSettingsList.Add(imageSettings);
                else if (value != null && property.PropertyType.IsClass && !IsPrimitiveType(property.PropertyType))
                    ExtractObjectsRecursive(value,
                        imageSettingsList); // Рекурсивно извлекаем ссылки из вложенных объектов
            }

            // Проверяем поля
            foreach (var field in fields)
            {
                var value = field.GetValue(obj);
                if (value is T imageSettings)
                    imageSettingsList.Add(imageSettings);
                else if (value != null && field.FieldType.IsClass && !IsPrimitiveType(field.FieldType))
                    ExtractObjectsRecursive(value,
                        imageSettingsList); // Рекурсивно извлекаем ссылки из вложенных объектов
            }
        }

        public static bool IsPrimitiveType(this Type type)
        {
            return type.IsPrimitive || type == typeof(string) || type == typeof(decimal);
        }

        public static string FormatDayNumberWithSuffix(this int day)
        {
            return day + day.GetOrdinalSuffix();
        }

        public static string GetOrdinalSuffix(this int number)
        {
            if (number is >= 11 and <= 13) return "th";

            return (number % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th"
            };
        }
    }
}