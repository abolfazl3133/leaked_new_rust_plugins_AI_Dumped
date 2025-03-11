using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using UnityEngine;
using Formatter = Oxide.Core.Libraries.Covalence.Formatter;

namespace Oxide.Plugins
{
    [Info("RatesController", "Vlad-00003", "3.3.2")]
    [Description("All-in-one customizable rate system for the server")]
    /* 
     * Author info:
     *   E-mail: Vlad-00003@mail.ru
     *   Vk: vk.com/vlad_00003
     * TODO: Вывести частоту срабатывания переплавки в печах
     */
    class RatesController : RustPlugin
    {
        #region Vars

        private static PluginConfig _config;
        private static RatesController _ratesController;
        private PluginData _data;
        private bool _isDay = true;
        private Dictionary<ItemModCookable, float> _smeltRatesBackup = new Dictionary<ItemModCookable, float>();
        private Dictionary<ulong,string> _quarryOwners = new Dictionary<ulong, string>();
        private CoalConfig _defaultCoal;

        private static class Constants
        {
            public const string ExcavatorArmPrefab = "assets/content/structures/excavator/prefabs/excavator_yaw.prefab";
        }

        #endregion

        #region Configuration

        #region Rates

        private sealed class RateType
        {
            private readonly string _name;

            private RateType(string name)
            {
                _name = name;
            }
            public override string ToString()
            {
                return _name;
            }

            public static implicit operator string(RateType type) => type._name;

            public static readonly RateType Quarry = new RateType("Добываемые ресурсы в карьере");
            public static readonly RateType Excavator = new RateType("Добываемые ресурсы в экскаваторе");
            public static readonly RateType Gather = new RateType("Добываемые ресурсы");
            public static readonly RateType Loot = new RateType("Получаемый лут");
            public static readonly RateType Pickup = new RateType("Подбираемые ресурсы");
            public static readonly RateType OvenSpeed = new RateType("Скорость печей");
            public static readonly RateType[] Available = {Quarry, Excavator, Gather, Loot, Pickup, OvenSpeed};
        }

        private class Rates
        {
            [JsonProperty("Днём")]
            private Dictionary<string, float> _day = new Dictionary<string, float>();
            [JsonProperty("Ночью")]
            private Dictionary<string, float> _night = new Dictionary<string, float>();

            public float GetRate(RateType type, bool day = true)
            {
                var dict = day ? _day : _night;
                float val;
                return dict.TryGetValue(type, out val) ? val : 1f;
            }
            
            #region Default Сonfig

            public static Rates Default => DefaultWith(1f);

            public static Rates DefaultWith(float rate) => new Rates
            {
                _day = RateType.Available.ToDictionary(x => (string) x, x => rate),
                _night = RateType.Available.ToDictionary(x => (string) x, x => rate)
            };

            #endregion
        }

        #endregion

        #region Server Time Config

        private class SavedDateConfig
        {
            [JsonProperty("Устанавливать дату при запуске плагина")]
            public bool Use;
            [JsonProperty("День")]
            private int _day;
            [JsonProperty("Месяц")]
            private int _month;
            [JsonProperty("Год")]
            private int _year;
            [JsonProperty("Час")]
            private int _hour;
            [JsonProperty("Минута")]
            private int _minute;
            [JsonProperty("Секунда")]
            private int _second;

            [JsonIgnore]
            public DateTime Date;

            #region Default Config

            public static SavedDateConfig DefaultConfig
            {
                get
                {
                    var now = DateTime.Now;
                    return new SavedDateConfig
                    {
                        Use = false,
                        _day = now.Day,
                        _month = now.Month,
                        _year = now.Year,
                        _hour = 12,
                        _minute = 0,
                        _second = 0
                    };
                }
            }

            #endregion
            
            [OnDeserialized]
            internal void OnDeserializedMethod(StreamingContext context)
            {
                Date = new DateTime(_year,_month,_day,_hour,_minute,_second);
            }
        }

        private class DayNightSwitchConfig
        {
            [JsonProperty("Рассчитывать время по закату/восходу (false - по часам)")]
            public bool UseSun;
            
            [JsonProperty("Час начала дня (игровое время)")]
            public int DayStartHour;
            
            [JsonProperty("Час начала ночи (игровое время)")]
            public int NightStartHour;

            #region Default Config

            public static DayNightSwitchConfig DefaultConfig => new DayNightSwitchConfig
            {
                UseSun = true,
                DayStartHour = 6,
                NightStartHour = 18
            };

            #endregion

            [JsonIgnore]
            public float DayMultiplier;
            [JsonIgnore]
            public float NightMultiplier;
            
            [OnDeserialized]
            internal void OnDeserializedMethod(StreamingContext context)
            {
                DayMultiplier = 24.0f / (NightStartHour - DayStartHour);
                NightMultiplier = 24.0f / (24.0f - (NightStartHour - DayStartHour));
            }
        }
        
        private class TimeConfig
        {
            [JsonProperty("Длина дня (в минутах)")]
            public int DayLength;

            [JsonProperty("Длина ночи (в минутах)")]
            public int NightLength;

            [JsonProperty("Настройки смены дня/ночи")]
            public DayNightSwitchConfig DayNightSwitchConfig;
            
            [JsonProperty("Восстанавливать состояние времени при запуске плагина")]
            public bool RestoreState;

            [JsonProperty("Устанавливаемая при запуске плагина дата")]
            public SavedDateConfig SavedDateConfig;
            
            #region Default Config

            public static TimeConfig DefaultConfig => new TimeConfig
            {
                DayLength = 30,
                NightLength = 30,
                DayNightSwitchConfig = DayNightSwitchConfig.DefaultConfig,
                RestoreState = true,
                SavedDateConfig = SavedDateConfig.DefaultConfig
            };

            #endregion
        }

        #endregion

        #region General Options

        private class ChatConfig
        {
            [JsonProperty("Выводить сообщения в чат о начале дня или ночи")]
            public bool NotifyTod;

            [JsonProperty("Формат сообщений в чате")]
            private string _chatFormat;

            [JsonIgnore]
            public string ChatFormat;

            #region Default Config

            public static ChatConfig DefaultConfig => new ChatConfig
            {
                NotifyTod = true,
                _chatFormat = "<color=#ff0000>[RatesController]</color>: {0}"
            };

            #endregion
            
            [OnDeserialized]
            internal void OnDeserializedMethod(StreamingContext context)
            {
                ChatFormat =  Formatter.ToUnity(_chatFormat);
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class DefaultModifiers
        {
            [JsonProperty("Время переработки ресурсов (в секундах)")]
            public Dictionary<string, float> SmeltRates;
            [JsonProperty("Множители добываемых ресурсов")]
            private Dictionary<string, float> _gatherRates;
            [JsonProperty("Множители добываемых ресурсов в карьере")]
            private Dictionary<string, float> _quarryRates;
            [JsonProperty("Множители добываемых ресурсов в экскаваторе")]
            private Dictionary<string, float> _excavatorRates;
            [JsonProperty("Множители подбираемых ресурсов")]
            private Dictionary<string, float> _pickupRates;
            
            #region Default Config

            public static DefaultModifiers DefaultConfig
            {
                get
                {
                    var modifiers = new DefaultModifiers
                    {
                        SmeltRates = new Dictionary<string, float>(),
                        _gatherRates = new Dictionary<string, float>
                        {
                            ["Animal Fat"] = 1.0f,
                            ["Raw Bear Meat"] = 1.0f,
                            ["Bone Fragments"] = 1.0f,
                            ["Cloth"] = 1.0f,
                            ["High Quality Metal Ore"] = 1.0f,
                            ["Human Skull"] = 1.0f,
                            ["Leather"] = 1.0f,
                            ["Metal Ore"] = 1.0f,
                            ["Pork"] = 1.0f,
                            ["Raw Chicken Breast"] = 1.0f,
                            ["Raw Human Meat"] = 1.0f,
                            ["Raw Wolf Meat"] = 1.0f,
                            ["Stones"] = 1.0f,
                            ["Sulfur Ore"] = 1.0f,
                            ["Wolf Skull"] = 1.0f,
                            ["Wood"] = 1.0f,
                            ["Raw Deer Meat"] = 1.0f,
                            ["Cactus Flesh"] = 1.0f
                        },
                        _quarryRates = new Dictionary<string, float>
                        {
                            ["High Quality Metal Ore"] = 1.0f,
                            ["Sulfur Ore"] = 1.0f,
                            ["Stones"] = 1.0f,
                            ["Metal Fragments"] = 1.0f,
                            ["Crude Oil"] = 1.0f
                        },
                        _excavatorRates = GameManager.server.FindPrefab(Constants.ExcavatorArmPrefab)
                            .GetComponent<ExcavatorArm>()?.resourcesToMine
                            .ToDictionary(x => x.itemDef.displayName.english, x => 1f),
                        _pickupRates = new Dictionary<string, float>
                        {
                            ["Metal Ore"] = 1.0f,
                            ["Stones"] = 1.0f,
                            ["Sulfur Ore"] = 1.0f,
                            ["Wood"] = 1.0f,
                            ["Hemp Seed"] = 1.0f,
                            ["Corn Seed"] = 1.0f,
                            ["Pumpkin Seed"] = 1.0f,
                            ["Cloth"] = 1.0f,
                            ["Pumpkin"] = 1.0f,
                            ["Corn"] = 1.0f,
                            ["Wolf Skull"] = 1.0f
                        }
                    };

                    ItemManager.GetItemDefinitions().ForEach(def =>
                    {
                        var cookable = def.GetComponent<ItemModCookable>();
                        if (!cookable)
                            return;
                        modifiers.SmeltRates[def.displayName.english] = cookable.cookTime;
                    });

                    return modifiers;
                }
            }


            #endregion

            public float GetModifier(RateType type, ItemDefinition def)
            {
                if (type == RateType.Gather)
                    return GetModifier(def, _gatherRates);
                if (type == RateType.Quarry)
                    return GetModifier(def, _quarryRates);
                if (type == RateType.Excavator)
                    return GetModifier(def, _excavatorRates);
                if (type == RateType.Pickup)
                    return GetModifier(def, _pickupRates);
                return 1f;
            }

            private float GetModifier(ItemDefinition def, Dictionary<string, float> data)
            {
                float mod;
                if (data.TryGetValue(def.displayName.english, out mod) || data.TryGetValue(def.shortname, out mod))
                    return mod;
                return 1f;
            }
        }

        #endregion

        #region Coal and HQM

         private class HqmConfig
        {
            [JsonProperty("Добавить МВК в список бонусов всех рудных жил")]
            public bool AddHqm;

            [JsonProperty("Количество МВК в жиле")]
            private int _amount;

            [JsonIgnore]
            public ItemDefinition HqmDefinition;
            
            [JsonIgnore]
            public ItemAmount ItemAmount => new ItemAmount(HqmDefinition, _amount);

            #region Default Config

            public static HqmConfig DefaultConfig => new HqmConfig
            {
                AddHqm = false,
                _amount = 2
            };

            #endregion

            [OnDeserialized]
            internal void OnDeserializedMethod(StreamingContext context)
            {
                HqmDefinition = ItemManager.FindItemDefinition("hq.metal.ore");
            }
        }

        private class CoalConfig
        {
            [JsonProperty("Шанс производства")]
            private int _chance;
            [JsonProperty("Количество")]
            public int Amount;

            //UnityEngine.Random.Range(0f, 1f) > burnable.byproductChance
            [JsonIgnore]
            public float Chance;

            #region Default Config

            public static CoalConfig DefaultConfig
            {
                get
                {
                    var coal = ItemManager.FindItemDefinition("wood").GetComponent<ItemModBurnable>();
                    return new CoalConfig
                    {
                        _chance = Mathf.RoundToInt(100 - coal.byproductChance*100),
                        Amount = coal.byproductAmount
                    };
                }
            }

            #endregion

            public static CoalConfig Save()
            {
                var coal = ItemManager.FindItemDefinition("wood").GetComponent<ItemModBurnable>();
                return new CoalConfig
                {
                    Amount = coal.byproductAmount,
                    Chance = coal.byproductChance
                };
            }
            
            [OnDeserialized]
            internal void OnDeserializedMethod(StreamingContext context)
            {
                if (Amount < 0)
                    Amount = 0;
                if (_chance <= 0 || Amount <= 0)
                    Chance = 2;
                else
                    Chance = (100 - _chance) / 100f;
            }
        }

        private class CoalConfigs
        {
            [JsonProperty("Днём")]
            public CoalConfig Day;
            [JsonProperty("Ночью")]
            public CoalConfig Night;

            #region Default Config

            public static CoalConfigs DefaultConfig => new CoalConfigs
            {
                Day = CoalConfig.DefaultConfig,
                Night = CoalConfig.DefaultConfig
            };

            #endregion

            public CoalConfig Get(bool isDay) => isDay ? Day : Night;
        }

        #endregion

        #region Loot
        
        private class AllowedItemsConfig
        {
            [JsonProperty("Тип списка (0 - никак не управлять предметами, 1 - чёрный список, 2 - белый список).")]
            private int _type;

            [JsonProperty("Список предметов")]
            public List<string> Items;
            
            #region Default Config

            public static AllowedItemsConfig DefaultConfig => new AllowedItemsConfig
            {
                _type = 1,
                Items = new List<string> { "Blueprint","Rotten Apple", "Spoiled Wolf Meat", "Spoiled Chicken", "Spoiled Human Meat"}
            };

            #endregion

            public bool ShouldChange(Item item)
            {
                switch (_type)
                {
                    case 1:
                        return !Items.Any(x => x == item.info.displayName.english || x == item.info.shortname);
                    case 2:
                        return Items.Any(x => x == item.info.displayName.english || x == item.info.shortname);
                    default:
                        return true;
                }
            }
        }

        private class LootConfig
        {
            [JsonProperty("Предметы, на которые действуют множители")]
            public AllowedItemsConfig AllowedItems;
            [JsonProperty("Предметы, которые не будут выпадать вообще")]
            public List<string> BlackList;
            [JsonProperty("Множитель для предметов, не указанных в списке ниже")]
            public float DefaultModifier;
            [JsonProperty("Список множителей отдельных предметов (SkinID:Множитель)")]
            public Dictionary<string, Dictionary<ulong, float>> CustomModifiers;

            #region Default Config

            public static LootConfig DefaultConfig => new LootConfig
            {
                AllowedItems = AllowedItemsConfig.DefaultConfig,
                BlackList = new List<string>{"Rotten Apple"},
                DefaultModifier = 1f,
                CustomModifiers = new Dictionary<string, Dictionary<ulong, float>>
                {
                    ["battery.small"] = new Dictionary<ulong, float>
                    {
                        [0] = 1f,
                        [12331] = 2f
                    },
                    ["scrap"] = new Dictionary<ulong, float>
                    {
                        [0] = 1f
                    }
                }
            };

            #endregion

            public bool ShouldDrop(Item item) =>
                !BlackList.Any(x => x == item.info.displayName.english || x == item.info.shortname);

            public float GetModifier(Item item)
            {
                Dictionary<ulong, float> skins;
                if (!CustomModifiers.TryGetValue(item.info.shortname, out skins) &&
                    !CustomModifiers.TryGetValue(item.info.displayName.english, out skins))
                    return DefaultModifier;
                float mod;
                return !skins.TryGetValue(item.skin, out mod) ? DefaultModifier : mod;
            }
        }

        #endregion

        private class PluginConfig
        {
            [JsonProperty("Настройки сообщений в чате")]
            public ChatConfig ChatConfig;

            [JsonProperty("Настройки даты и времени")]
            public TimeConfig TimeConfig;

            [JsonProperty("Металл высокого качества")]
            public HqmConfig HqmConfig;

            [JsonProperty("Производство угля при сжигании дерева")]
            public CoalConfigs CoalConfigs;

            [JsonProperty("Настройки лута")]
            public LootConfig LootConfig;

            [JsonProperty("Общие множители")]
            public Rates DefaultRates;

            [JsonProperty("Множители по привилегиям")]
            public Dictionary<string, Rates> CustomRates = new Dictionary<string, Rates>(StringComparer.InvariantCultureIgnoreCase);

            [JsonProperty("Множители ресурсов")]
            public DefaultModifiers DefaultModifiers;

            [JsonProperty("Дополнительные привилегии (прибавляются к основным, любые источники)")]
            public Dictionary<string, float> AdditionalPermissions = new Dictionary<string, float>(StringComparer.InvariantCultureIgnoreCase);

            [JsonProperty("Переключатели множителей в печах")]
            public Dictionary<string, bool> OvenSwitches;

            #region Default Config

            public static PluginConfig DefaultConfig => new PluginConfig
            {
                ChatConfig = ChatConfig.DefaultConfig,
                TimeConfig = TimeConfig.DefaultConfig,
                HqmConfig = HqmConfig.DefaultConfig,
                CoalConfigs = CoalConfigs.DefaultConfig,
                LootConfig = LootConfig.DefaultConfig,
                DefaultRates = Rates.Default,
                CustomRates = 
                {
                    [nameof(RatesController)+".vip"] = Rates.DefaultWith(2f),
                    [nameof(RatesController)+".premium"] = Rates.DefaultWith(3f)
                },
                DefaultModifiers = DefaultModifiers.DefaultConfig,
                AdditionalPermissions =
                {
                    ["RatesController.NameReward"] = 0.3f
                },
                OvenSwitches = null
            };
            #endregion
            
        }

        #endregion
        
        #region Data

        private class PluginData
        {
            public bool IsFrozen;
        }

        #endregion

        #region Config and Data initialization

        #region Data

        private void LoadData()
        {
            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Title);
            }
            catch (Exception ex)
            {
                PrintError($"Failed to load data (is the file corrupt?) - no previously created recycles would work ({ex.Message})");
                _data = new PluginData();
            }
        }
        private void SaveData()
        {
            if(TimeController.Initialized)
                _data.IsFrozen = TimeController.Instance.IsFrozen;
            Interface.Oxide.DataFileSystem.WriteObject(Title, _data);
        }

        #endregion

        #region Config

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();

            if (ShouldUpdateConfig())
                SaveConfig();

            foreach (var customRate in _config.CustomRates)
            {
                permission.RegisterPermission(customRate.Key, this);
            }
            foreach (var addPerm in _config.AdditionalPermissions)
            {
                permission.RegisterPermission(addPerm.Key, this);
            }


            LoadData();
        }

        private bool ShouldUpdateConfig()
        {
            bool res = false;
            if (_config.TimeConfig.DayLength < 0)
            {
                PrintWarning("Day length can't be less then 0, setting to 0");
                _config.TimeConfig.DayLength = 0;
                res = true;
            }

            if (_config.TimeConfig.NightLength < 0)
            {
                PrintWarning("Night length can't be less then 0, setting to 0");
                _config.TimeConfig.NightLength = 0;
                res = true;
            }

            if (_config.TimeConfig.DayLength == 0 && _config.TimeConfig.NightLength == 0)
            {
                PrintWarning("Both night and day length can't be 0 at the same time, setting day length to 30");
                _config.TimeConfig.DayLength = 30;
                res = true;
            }

            if (!ConfigExists<Dictionary<string,object>>("Дополнительные привилегии (прибавляются к основным, любые источники)"))
            {
                PrintWarning("New option added to the config file - additional rates");
                _config.AdditionalPermissions = new Dictionary<string, float>() {["RatesController.NameReward"] = 0.3f};
                res = true;
            }

            if (!ConfigExists<double>("Настройки лута", "Множитель для предметов, не указанных в списке ниже"))
            {
                PrintWarning("New options added to the config file - custom loot items modifiers, furnace selector");
                _config.LootConfig.DefaultModifier = 1f;
                _config.LootConfig.CustomModifiers = LootConfig.DefaultConfig.CustomModifiers;
                res = true;
            }
            return res;
        }

        protected override void LoadDefaultConfig()
        {
            _config = PluginConfig.DefaultConfig;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }
        private bool ConfigExists<T>(params string[] path)
        {
            var value = Config.Get(path);
            return value is T;
        }

        #endregion

        #endregion

        #region Localization
        private static string GetMessage(string langKey, string playerId = null)
        {
            return _ratesController.lang.GetMessage(langKey, _ratesController, playerId);
        }

        private static string GetMessage(string langKey, string playerId = null, params object[] args)
        {
            return args.Length == 0 ? GetMessage(langKey, playerId) : string.Format(GetMessage(langKey, playerId), args);
        }

        private static void SendResponse(BasePlayer player, string langKey, params object[] args)
        {
            if (!player || !player.IsConnected)
                return;
            player.ChatMessage(string.Format(_config.ChatConfig.ChatFormat, GetMessage(langKey, player.UserIDString, args)));
        }

        private static void SendResponse(IPlayer player, string langKey, params object[] args)
        {
            player.Reply(string.Format(_config.ChatConfig.ChatFormat, GetMessage(langKey, player.Id, args)));
        }
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [RateType.Quarry] = "Quarry gather rates: {0}",
                [RateType.Excavator] = "Excavator gather rates: {0}",
                [RateType.Gather] = "Gather rates: {0}",
                [RateType.Loot] = "Loot rates: {0}",
                [RateType.Pickup] = "Pickup rates: {0}",
                [RateType.OvenSpeed] = "Oven speed: {0}",
                ["RatesPersonal"] = "Your personal rates:\n{0}",
                ["DayStarted"] = "The day has come!\n{0}",
                ["NightStarted"] ="The night has come!\n{0}",
                ["PositiveRate"] = "<color=green>{0:P0}</color>",
                ["NegativeRate"] = "<color=red>{0:P0}</color>",
                ["NeutralRate"] = "{0:P0}"
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [RateType.Quarry] = "Добываемые ресурсы в карьере: {0}",
                [RateType.Excavator] = "Добываемые ресурсы в экскаваторе: {0}",
                [RateType.Gather] = "Добываемые ресурсы: {0}",
                [RateType.Loot] = "Получаемый лут: {0}",
                [RateType.Pickup] = "Подбираемые ресурсы: {0}",
                [RateType.OvenSpeed] = "Скорость печей: {0}",
                ["RatesPersonal"] = "Ваши личные рейты:\n{0}",
                ["DayStarted"] = "Наступает день!\n{0}",
                ["NightStarted"] ="Наступает ночь!\n{0}",
                ["PositiveRate"] = "<color=green>{0:P0}</color>",
                ["NegativeRate"] = "<color=red>{0:P0}</color>",
                ["NeutralRate"] = "{0:P0}"
            }, this, "ru");

        }

        #endregion
        
        #region Time controller
        
        private class TimeController: FacepunchBehaviour
        {
            private int _attempt;
            private TOD_Sky _sky;
            private TOD_Time _time;
            private bool _isDaySet;
            private float _lastKnownTime; //env.time set workaround
            private bool _initialized;

            public static TimeController Instance { get; private set; }
            public static bool Initialized => Instance && Instance._initialized;

            #region Instance public properties

            public bool IsFrozen
            {
                get { return !_time.ProgressTime; }
                set { _time.ProgressTime = !value; }
            }

            #endregion

            #region Private properties

            private bool IsDay => DayStart <= _sky.Cycle.Hour && _sky.Cycle.Hour < NightStart;

            private float DayStart
            {
                get
                {
                    if (!_config.TimeConfig.DayNightSwitchConfig.UseSun)
                        return _config.TimeConfig.DayNightSwitchConfig.DayStartHour;
                    return _sky.SunriseTime;
                }
            }

            private float NightStart
            {
                get
                {
                    if (!_config.TimeConfig.DayNightSwitchConfig.UseSun)
                        return _config.TimeConfig.DayNightSwitchConfig.NightStartHour;
                    return _sky.SunsetTime;
                }
            }

            private float DayLength
            {
                get
                {
                    if (!_config.TimeConfig.DayNightSwitchConfig.UseSun)
                        return _config.TimeConfig.DayLength * _config.TimeConfig.DayNightSwitchConfig.DayMultiplier;
                    return _config.TimeConfig.DayLength * (24.0f / (_sky.SunsetTime - _sky.SunriseTime));
                }
            }

            private float NightLength
            {
                get
                {
                    if (!_config.TimeConfig.DayNightSwitchConfig.UseSun)
                        return _config.TimeConfig.NightLength * _config.TimeConfig.DayNightSwitchConfig.NightMultiplier;
                    return _config.TimeConfig.NightLength * (24.0f / (24.0f - (_sky.SunsetTime - _sky.SunriseTime)));
                }
            }

            #endregion

            #region Initialization

            public void Initialize()
            {
                _sky = TOD_Sky.Instance;
                if (_sky == null)
                {
                    if (_attempt++ < 10)
                    {
                        Invoke(Initialize,1f);
                        return;
                    }
                    _ratesController.PrintError("Failed to get TOD_Sky instance in {0} attempts.", _attempt);
                    return;
                }
                _ratesController.Puts("TOD_Sky found after {0} attempts", _attempt);
                _time = _sky.Components.Time;
                if (_time == null)
                {
                    _ratesController.PrintError("Can't find time component!");
                    return;
                }
                
                _initialized = true;

                SetData();
                
                if(IsDay)
                    OnDayStarted();
                else
                    OnNightStarted();
                _time.OnHour += OnHour;
            }
            
            private void SetData()
            {
                _time.UseTimeCurve = false;
                if (_config.TimeConfig.SavedDateConfig.Use)
                    _sky.Cycle.DateTime = _config.TimeConfig.SavedDateConfig.Date;
                if (_config.TimeConfig.RestoreState)
                    _time.ProgressTime = !_ratesController._data.IsFrozen;
                

                _lastKnownTime = _sky.Cycle.Hour;
            }

            #endregion
            
            #region Day/Night Switch
            
            private void OnDayStarted()
            {
                if (!Initialized)
                    return;
                var dayLength = DayLength;
                if (dayLength <= 0)
                {
                    print("Day skipped");
                    _sky.Cycle.Hour = NightStart;
                    _lastKnownTime = _sky.Cycle.Hour;
                    OnNightStarted();
                    return;
                }

                _time.DayLengthInMinutes = dayLength;
                Interface.CallHook("OnDayStarted");
                _isDaySet = true;
            }

            private void OnNightStarted()
            {
                if (!Initialized)
                    return;
                var nightLength = NightLength;
                if (nightLength <= 0)
                {
                    print("Night skipped");
                    _sky.Cycle.Hour += 24 - _sky.Cycle.Hour + DayStart;
                    _lastKnownTime = _sky.Cycle.Hour;
                    OnDayStarted();
                    return;
                }

                _time.DayLengthInMinutes = nightLength;
                Interface.CallHook("OnNightStarted");
                _isDaySet = false;
            }

            #endregion
            
            #region Actions
            private void OnHour()
            {
                if(IsDay && !_isDaySet)
                    OnDayStarted();
                if(!IsDay && _isDaySet)
                    OnNightStarted();
            }

            //env.time set workaround
            private void Update()
            {
                if (!Initialized)
                    return;
                if (Math.Abs(_sky.Cycle.Hour - _lastKnownTime) > Time.deltaTime)
                    OnHour();
                _lastKnownTime = _sky.Cycle.Hour;
            }

            #endregion
          
            public void Kill()
            {
                if (!Initialized)
                    return;
                _time.OnHour -= OnHour;
                Destroy(gameObject);
                Instance = null;
            }

            public static TimeController Create()
            {
                if (Instance)
                {
                    _ratesController.PrintWarning("TimeController wasn't destroyed");
                    Instance.Kill();
                }
                var go = new GameObject(nameof(RatesController) + "." + nameof(TimeController));
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<TimeController>();
                return Instance;
            }
        }

        #endregion

        #region Initialization and quitting

        private void Init()
        {
            _ratesController = this;
            Unsubscribe("OnPluginLoaded");
            Unsubscribe("OnPlayerDisconnected");
            Unsubscribe("OnPluginUnloaded");
            Unsubscribe("OnPlayerConnected");
        }
        private void Unload()
        {
            LootController.KillAll();
            TimeController.Instance?.Kill();
            UpdateCoalRates(true);
            OnServerSave();
            foreach (var pair in _smeltRatesBackup)
                pair.Key.cookTime = pair.Value;
            
            _config = null;
            _ratesController = null;
            _data = null;
            _defaultCoal = null;
            _smeltRatesBackup.Clear();
            _smeltRatesBackup = null;
            _quarryOwners.Clear();
            _quarryOwners = null;
            _ratesCache.Clear();
        }

        private void OnServerSave() =>  SaveData();
        private void OnServerInitialized()
        {
            var ovens = GameManager.server.preProcessed.prefabList.Values
                .Select(x => x.GetComponent<BaseOven>())
                .Where(x => x != null && (int)x.temperature >= 2);
            if (_config.OvenSwitches == null)
            {
                _config.OvenSwitches = ovens.ToDictionary(x => x.ShortPrefabName, x => true);
                SaveConfig();
                PrintWarning("Oven switches list was null, filling with default values. Config was overwritten");
            }
            else
            {
                var changed = false;
                foreach (var oven in ovens)
                {
                    if (_config.OvenSwitches.ContainsKey(oven.ShortPrefabName))
                        continue;
                    _config.OvenSwitches[oven.ShortPrefabName] = true;
                    changed = true;
                }

                if (changed)
                {
                    SaveConfig();
                    PrintWarning("New type(s) of oven added to the Oven Switch List");
                }
            }
            _defaultCoal = CoalConfig.Save();
            TimeController.Create().Initialize();
            ItemManager.GetItemDefinitions().ForEach(SetCookableDefinition);
            UpdateCoalRates();
        }

        
        #endregion

        #region Time Hooks
        
        private void OnDayStarted()
        {
            _isDay = true;
            UpdateCoalRates();
            _ratesCache.Clear();
            if (!_config.ChatConfig.NotifyTod)
                return;
            foreach (var player in BasePlayer.activePlayerList)
                SendRates(player, "DayStarted");
            
        }

        private void OnNightStarted()
        {
            _isDay = false;
            UpdateCoalRates();
            _ratesCache.Clear();
            if (!_config.ChatConfig.NotifyTod)
                return;
            foreach (var player in BasePlayer.activePlayerList)
                SendRates(player, "NightStarted");
        }

        #endregion

        #region Oxide Hooks (Rates Cache)

        private void OnUserGroupAdded(string playerId, string groupName)
        {
            if (permission.GetGroupPermissions(groupName).Any(groupPermission =>
                    _config.CustomRates.ContainsKey(groupPermission) || _config.AdditionalPermissions.ContainsKey(groupPermission)))
            {
                _ratesCache.Remove(playerId);
            }
        }

        private void OnUserGroupRemoved(string playerId, string groupName)
        {
            if (permission.GetGroupPermissions(groupName).Any(groupPermission
                    => _config.CustomRates.ContainsKey(groupPermission) || _config.AdditionalPermissions.ContainsKey(groupPermission)))
            {
                _ratesCache.Remove(playerId);
            }
        }

        private void OnUserPermissionGranted(string playerId, string perm)
        {
            if (!_config.CustomRates.ContainsKey(perm) && !_config.AdditionalPermissions.ContainsKey(perm))
                return;
            _ratesCache.Remove(playerId);
        }

        private void OnUserPermissionRevoked(string playerId, string perm)
        {
            if (!_config.CustomRates.ContainsKey(perm) && !_config.AdditionalPermissions.ContainsKey(perm))
                return;
            _ratesCache.Remove(playerId);
        }

        private void OnGroupPermissionGranted(string group, string perm)
        {
            if (!_config.CustomRates.ContainsKey(perm) && !_config.AdditionalPermissions.ContainsKey(perm))
                return;
            foreach (var user in permission.GetUsersInGroup(group))
                _ratesCache.Remove(user.Split(' ')[0]);
        }

        private void OnGroupPermissionRevoked(string group, string perm)
        {
            if (!_config.CustomRates.ContainsKey(perm) && !_config.AdditionalPermissions.ContainsKey(perm))
                return;
            foreach (var user in permission.GetUsersInGroup(group))
                _ratesCache.Remove(user.Split(' ')[0]);
        }

        #endregion

        #region Oxide Hooks (Sub)
        
        private void OnQuarryToggled(MiningQuarry quarry, BasePlayer player)
        {
            if (!player || !quarry || !quarry.IsOn() || quarry.OwnerID != 0)
                return;
            _quarryOwners[quarry.net.ID.Value] = player.UserIDString;
        }
        private void OnExcavatorResourceSet(ExcavatorArm arm, string resource, BasePlayer player)
        {
            if (!player || !arm || arm.OwnerID != 0)
                return;
            _quarryOwners[arm.net.ID.Value] = player.UserIDString;
        }
        
        private void CanMoveItem(Item item, PlayerInventory playerLoot, ItemContainerId targetContainer, int targetSlot, int amount)
        {
            var container = item.GetRootContainer()?.entityOwner as LootContainer;
            if (!container)
                return; 
            LootController.GetOrAdd(container).OnMove(item, playerLoot, amount);
        }

        private void OnItemAction(Item item, string action, BasePlayer player)
        {
            if (action != "drop")
                return;
            var container = item.GetRootContainer()?.entityOwner as LootContainer;
            if (!container)
                return;
            LootController.GetOrAdd(container).OnDrop(item, player);
        }


        private void OnItemAddedToContainer(ItemContainer inventory, Item item)
        {
            var container = item.GetRootContainer()?.entityOwner as LootContainer;
            if (!container)
                return;
            LootController.GetOrAdd(container).OnRefill(inventory);
        }
        #endregion

        #region Oxide Hooks (Rates)
        
        private void OnQuarryGather(MiningQuarry quarry, Item item)
        {
            UpdateAmount(item, RateType.Quarry, GetQuarryOwner(quarry));
        } 
       
        private void OnExcavatorGather(ExcavatorArm arm, Item item)
        {
            UpdateAmount(item, RateType.Excavator, GetQuarryOwner(arm));
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            UpdateAmount(item, RateType.Gather, player.UserIDString);
            if (!_config.HqmConfig.AddHqm || dispenser.gatherType != ResourceDispenser.GatherType.Ore)
                return;
            if (dispenser.finishBonus.Any(x => x.itemDef == _config.HqmConfig.HqmDefinition))
                return;
            dispenser.finishBonus.Add(_config.HqmConfig.ItemAmount);
        }

        private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            UpdateAmount(item, RateType.Gather, player.UserIDString);
        }

        private object OnCollectiblePickup(CollectibleEntity entity, BasePlayer player)
        {
            if (!player || !entity)
                return null;
            entity.itemList = UpdateAmounts(entity.itemList, RateType.Pickup, player.UserIDString).ToArray();
            return entity.itemList == null ? (object) false : null;
        }
        
        void OnGrowableGathered(GrowableEntity plant, Item item, BasePlayer player)
        {
            string owner = player.UserIDString;
            if (plant.OwnerID != 0)
                owner = plant.OwnerID.ToString();
            UpdateAmount(item, RateType.Pickup, owner);
        }
        private void OnLootEntity(BasePlayer player, LootContainer container)
        {
            LootController.GetOrAdd(container).OnOpen(player);
        }
        private void OnLootEntityEnd(BasePlayer player, LootContainer container)
        {
            LootController.GetOrAdd(container).OnClose(player);
        }

        private void OnContainerDropItems(ItemContainer container)
        {
            var lootContainer = container?.entityOwner as LootContainer;
            if (lootContainer == null)
                return;
            LootController.GetOrAdd(lootContainer).OnDropItems();
        }

        #region Cooking

        private object OnOvenCook(BaseOven oven, Item fuelItem)
        {
            bool isOn;
            if (_config.OvenSwitches.TryGetValue(oven.ShortPrefabName, out isOn) && !isOn)
                return null;
            
            if ((fuelItem == null && !oven.CanRunWithNoFuel) || oven.OwnerID == 0 || (int)oven.temperature < 2)
                return null;
            var userRate = GetUserRate(RateType.OvenSpeed, oven.OwnerID.ToString());

            foreach (var item2 in oven.inventory.itemList.Where(item2 => item2.position >= oven._inputSlotIndex && item2.position < oven._inputSlotIndex + oven.inputSlots && !item2.HasFlag(global::Item.Flag.Cooking)))
            {
                item2.SetFlag(global::Item.Flag.Cooking, true);
                item2.MarkDirty();
            }

            IncreaseCookTime(oven, 0.5f * oven.GetSmeltingSpeed() * userRate);
            var slot = oven.GetSlot(BaseEntity.Slot.FireMod);
            if (slot)
                slot.SendMessage("Cook", 0.5f, SendMessageOptions.DontRequireReceiver);

            if (fuelItem != null)
            {
                var burnable = fuelItem.info.GetComponent<ItemModBurnable>();
                var requiredFuel = 0.5f * userRate * (oven.cookingTemperature / 200f);
                if (!fuelItem.HasFlag(global::Item.Flag.OnFire))
                {
                    fuelItem.SetFlag(global::Item.Flag.OnFire, true);
                    fuelItem.MarkDirty();
                }
                if (fuelItem.fuel >= requiredFuel)
                {
                    fuelItem.fuel -= requiredFuel;
                    if (fuelItem.fuel <= 0f)
                    {
                        oven.ConsumeFuel(fuelItem, burnable);
                    }
                }
                else
                {
                    while (requiredFuel > 0)
                    {
                        var dif = fuelItem.fuel - requiredFuel;
                        if (dif >= 0)
                        {
                            fuelItem.fuel -= requiredFuel;
                            if (fuelItem.fuel <= 0f)
                            {
                                oven.ConsumeFuel(fuelItem, burnable);
                            }
                            break;
                        }
                        requiredFuel -= fuelItem.fuel;
                        oven.ConsumeFuel(fuelItem, burnable);
                        if (!fuelItem.IsValid())
                            break;
                    
                    }
                
                }
            }
            //oven.OnCooked();
            if (oven is ModularCarOven)
            {
                if (WaterLevel.Test(oven.transform.position, true, false))
                {
                    oven.StopCooking();
                }
            }
            Interface.CallHook("OnOvenCooked", oven, fuelItem, slot);
            return false;

        }
        private void IncreaseCookTime(BaseOven oven,  float amount)
        {
            List<global::Item> list = Facepunch.Pool.GetList<global::Item>();
            foreach (global::Item item in oven.inventory.itemList)
            {
                if (item.HasFlag(global::Item.Flag.Cooking))
                {
                    list.Add(item);
                }
            }
            float num = amount / (float)list.Count;
            foreach (global::Item item2 in list)
            {
                item2.OnCycle(num);
            }
            Facepunch.Pool.FreeList<global::Item>(ref list);
        }

        #endregion


        #endregion

        #region Console Commands

        [ConsoleCommand("env.freeze")]
        private void FreezeCmd(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 2) 
                return;
            if (!TimeController.Initialized)
                return;
            TimeController.Instance.IsFrozen = !TimeController.Instance.IsFrozen;
            arg.ReplyWith(TimeController.Instance.IsFrozen ? "Frozen" : "Unfrozen");
        }

        #endregion

        #region Chat Commands

        [ChatCommand("Rates")]
        private void RatesCmd(BasePlayer player, string command, string[] args)
        {
            SendRates(player, "RatesPersonal");
        }

        #endregion
        
        #region Loot Controller

        private class LootController : FacepunchBehaviour
        {
            private static readonly Dictionary<LootContainer, LootController> LootControllers = new Dictionary<LootContainer, LootController>();
            private LootContainer _lootContainer;
            private Dictionary<BasePlayer, ItemContainer> _containers;
            private bool ShouldBeUpdated => _lootContainer.initialLootSpawn && _lootContainer.shouldRefreshContents;
            private bool _destroyOnEmpty;

            #region Awake and destroy

            private void Awake()
            {
                _lootContainer = (LootContainer)gameObject.ToBaseEntity();
                _containers = new Dictionary<BasePlayer, ItemContainer>();
                _destroyOnEmpty = _lootContainer.destroyOnEmpty;
                if(ShouldBeUpdated) _lootContainer.destroyOnEmpty = false;
            }

            private void OnDestroy()
            {
                LootControllers.Remove(_lootContainer);
                foreach (var pair in _containers)
                {
                    if (pair.Key?.inventory?.loot?.containers?.Remove(pair.Value) == true)
                        pair.Key.inventory.loot.SendImmediate();
                    
                    pair.Value.Kill();
                }
                _containers.Clear();
                _containers = null;
            }
            private void Kill()
            {
                if(ShouldBeUpdated) _lootContainer.destroyOnEmpty = _destroyOnEmpty;
                Destroy(this);
            }

            #endregion

            #region Static Methods

            public static LootController GetOrAdd(LootContainer container)
            {
                LootController controller;
                if (LootControllers.TryGetValue(container, out controller)) 
                    return controller;

                controller = container.gameObject.AddComponent<LootController>();
                LootControllers[container] = controller;
                return controller;
            }

            public static void KillAll()
            {
                foreach (var pair in LootControllers)
                {
                    pair.Value.Kill();
                }
                LootControllers.Clear();
            }

            #endregion
            
            #region Private Methods

            private void RefillAll()
            {
                foreach (var pair in _containers)
                {
                    Refill(pair.Value,pair.Key);
                }
            }
            private void Refill(ItemContainer container, BasePlayer player)
            {
                while (container.itemList.Count > 0)
                {
                    var item = container.itemList[0];
                    item.RemoveFromContainer();
                    item.Remove();
                }
                FillItems(container,player);
            }
            private void FillItems(ItemContainer container, BasePlayer player)
            {
                var anyRemoved = false;
                foreach (var item in _lootContainer.inventory.itemList)
                {
                    if (!_config.LootConfig.ShouldDrop(item))
                        continue;

                    var copy = CreateCopy(item);

                    if (_config.LootConfig.AllowedItems.ShouldChange(copy))
                        anyRemoved |= _ratesController.UpdateAmount(copy, RateType.Loot, player.UserIDString, _config.LootConfig.GetModifier(copy));
                    container.Insert(copy);
                }
                if(anyRemoved)
                    ItemManager.DoRemoves();
            }
            
            private ItemContainer CopyInventory()
            {
                var container =new ItemContainer
                {
                    allowedContents = _lootContainer.inventory.allowedContents,
                    availableSlots =  new List<ItemSlot>(_lootContainer.inventory.availableSlots),
                    canAcceptItem = _lootContainer.inventory.canAcceptItem,
                    capacity =  _lootContainer.inventory.capacity,
                    dirty = _lootContainer.inventory.dirty,
                    entityOwner = _lootContainer,
                    flags = _lootContainer.inventory.flags,
                    isServer = true,
                    maxStackSize = _lootContainer.inventory.maxStackSize,
                    onItemAddedRemoved = _lootContainer.inventory.onItemAddedRemoved,
                    onlyAllowedItems = _lootContainer.inventory.onlyAllowedItems?.ToArray(),
                    onPreItemRemove = _lootContainer.inventory.onPreItemRemove,
                    parent = _lootContainer.inventory.parent,
                    playerOwner = _lootContainer.inventory.playerOwner,
                    temperature = _lootContainer.inventory.temperature
                };
                container.GiveUID();
                return container;
            }

            private ItemContainer GetContainer(BasePlayer player)
            {
                ItemContainer container;
                if (_containers.TryGetValue(player, out container))
                {
                    container.SetLocked(_lootContainer.inventory.IsLocked());
                    return container;
                }

                container = CopyInventory();
                _containers[player] = container;
                Refill(container,player);
                return container;
            }
            #endregion

            #region Public Methods

            public void OnDropItems()
            {
                if (!ShouldBeUpdated)
                    return;
                var player = _lootContainer.lastAttacker as BasePlayer;
                if (!player)
                    return;
                var anyRemoved = false;
                foreach (var item in _lootContainer.inventory.itemList.ToArray())
                {
                    if (!_config.LootConfig.ShouldDrop(item))
                    {
                        item.Remove();
                        anyRemoved = true;
                    }

                    if (_config.LootConfig.AllowedItems.ShouldChange(item))
                       anyRemoved |= _ratesController.UpdateAmount(item,RateType.Loot, player.UserIDString, _config.LootConfig.GetModifier(item));
                }
                if(anyRemoved)
                    ItemManager.DoRemoves();
            }
            public void OnOpen(BasePlayer player)
            {
                if (!ShouldBeUpdated)
                    return;
                
                player.inventory.loot.AddContainer(GetContainer(player));
            }

            public void OnClose(BasePlayer player)
            {
                if (!ShouldBeUpdated)
                    return;
                var container = GetContainer(player);
                if (_destroyOnEmpty && !_lootContainer.IsDestroyed && (container?.itemList == null || container.itemList.Count == 0))
                    _lootContainer.Kill();
            }

            public void OnMove(Item item, PlayerInventory playerLoot, int amount)
            {
                if (!ShouldBeUpdated)
                    return;
                var amountBefore = _containers[playerLoot.baseEntity].GetSlot(item.position)?.amount;
                if (!amountBefore.HasValue)
                    return;
                var player = playerLoot.baseEntity;
                var position = item.position;
                var info = item.info;
                Invoke(() =>
                {
                    var newItem = _containers[playerLoot.baseEntity].GetSlot(position);
                    var amountAfter = newItem?.amount ?? 0;
                    if (newItem == null || newItem.info != info) 
                        amountAfter = 0;
                    var change = amountBefore.Value - amountAfter;
                    if (change <= 0)
                        return;
                    _lootContainer.inventory.GetSlot(position)?.UseItem(change);
                    foreach (var pair in _containers.Where(pair => pair.Key != player))
                        pair.Value.GetSlot(position)?.UseItem(change);
                },0.1f);
            }
            public void OnDrop(Item item, BasePlayer player)
            {
                if (!ShouldBeUpdated)
                    return;
                _lootContainer.inventory.GetSlot(item.position)?.UseItem(item.amount);
                foreach (var pair in _containers)
                {
                    if (pair.Key == player)
                        continue;
                    var slot = pair.Value.GetSlot(item.position);
                    slot?.UseItem(item.amount);
                }
            }

            public void OnRefill(ItemContainer container)
            {
                if (!ShouldBeUpdated || !_lootContainer.BlockPlayerItemInput ||container != _lootContainer.inventory || IsInvoking(RefillAll))
                    return;
                Invoke(RefillAll, 0.1f);
            }

            #endregion
        }


        #endregion

        #region Helpers
        
        #region Chat

        private void SendRates(BasePlayer player, string headerKey)
        {
            var rates = RateType.Available.Select(x => FormatRates(x, player.UserIDString));
            SendResponse(player, headerKey, string.Join("\n", rates));
        }

        private string FormatRates(RateType type, string userId)
        {
            var rate = GetUserRate(type, userId);
            var rateFormat = rate > 1f ? "PositiveRate" : rate < 1f ? "NegativeRate" : "NeutralRate";
            return GetMessage(type, userId, GetMessage(rateFormat, userId, rate));
        }

        #endregion
        
        #region Quarry

        public string GetQuarryOwner(BaseEntity quarry)
        {
            string owner;
            if (quarry.OwnerID != 0 || !_quarryOwners.TryGetValue(quarry.net.ID.Value, out owner))
                owner = quarry.OwnerID.ToString();
            return owner;
        }

        #endregion

        #region Definition updater

        private void UpdateCoalRates(bool restore = false)
        {
            var coal = ItemManager.FindItemDefinition("wood").GetComponent<ItemModBurnable>();
            var config = restore ? _defaultCoal : _config.CoalConfigs.Get(_isDay);
            coal.byproductAmount = config.Amount;
            coal.byproductChance = config.Chance;
        }

        private void SetCookableDefinition(ItemDefinition def)
        {
            var cookable = def.GetComponent<ItemModCookable>();
            if (!cookable)
                return;
            _smeltRatesBackup[cookable] = cookable.cookTime;
            float time;
            if (_config.DefaultModifiers.SmeltRates.TryGetValue(def.displayName.english, out time) ||
                _config.DefaultModifiers.SmeltRates.TryGetValue(def.shortname, out time))
            {
                cookable.cookTime = time;
            }
        }

        #endregion

        #region Items

        private static Item CreateCopy(Item item)
        {
            var copy = new Item
            {
                isServer = true,
                info =  item.info,
                uid = new ItemId(Net.sv.TakeUID()),
                name = item.name,
                text = item.text,
                amount = item.amount,
                position = item.position,
                busyTime = item.busyTime,
                removeTime = item.removeTime,
                flags = item.flags,
                skin = item.skin,
                _condition = item._condition,
                _maxCondition = item._maxCondition
            };
            if (item.instanceData != null)
            {
                copy.instanceData = item.instanceData.Copy();
                copy.instanceData.ShouldPool = false;
            }
            copy.OnItemCreated();
            copy.OnVirginSpawn();
            return copy;
        }

        #endregion

        private static IEnumerable<float> SplitDeltaBy(float delta, float step)
        {
            for(var i =0; i < Mathf.FloorToInt(delta/step); i++)
                yield return step;
            var modulo = delta%step;
            if(modulo > 0)
                yield return modulo;
        }
        /// <summary>
        /// Returns true is item was updated, false if it was removed
        /// </summary>
        /// <param name="item"></param>
        /// <param name="type"></param>
        /// <param name="userId"></param>
        /// <param name="additionalMod"></param>
        /// <returns></returns>
        private bool UpdateAmount(Item item, RateType type, string userId, float additionalMod = 1f)
        {
            var modifier = GetUserRate(type, userId) * _config.DefaultModifiers.GetModifier(type, item.info) * additionalMod;
            if (Mathf.Approximately(modifier, 0))
            {
                item.Remove();
                return false;
            }
            item.amount = Mathf.Clamp(Mathf.RoundToInt(item.amount * modifier), 1, item.info.stackable);
            return true;
        }

        private IEnumerable<ItemAmount> UpdateAmounts(ItemAmount[] itemAmounts, RateType type, string userId)
        {
            if (itemAmounts == null || itemAmounts.Length == 0)
                yield break;
            var rate = GetUserRate(type, userId);
            if(Mathf.Approximately(rate, 0))
                yield break;
            foreach (var itemAmount in itemAmounts)
            {
                if (itemAmount == null || itemAmount.itemDef == null || itemAmount.itemDef.shortname == "‌‌‌‍​﻿﻿")
                    continue;
                var mod = rate * _config.DefaultModifiers.GetModifier(type, itemAmount.itemDef);
                if (Mathf.Approximately(mod, 0))
                    continue;
                itemAmount.amount = Mathf.Clamp(Mathf.RoundToInt(itemAmount.amount * mod), 1, itemAmount.itemDef.stackable); 
                yield return itemAmount;
            }
        }
        
        private readonly Dictionary<string,Dictionary<RateType, float>> _ratesCache = new Dictionary<string, Dictionary<RateType, float>>();
        private float GetUserRate(RateType rate, string userId)
        {
            Dictionary<RateType, float> cache;
            if (!_ratesCache.TryGetValue(userId, out cache))
                cache = _ratesCache[userId] = new Dictionary<RateType, float>();
            float userRate;
            if (cache.TryGetValue(rate, out userRate))
                return userRate;
            var possibleRates = _config.CustomRates.Where(x => permission.UserHasPermission(userId, x.Key))
                .Select(x => x.Value.GetRate(rate, _isDay));
            userRate = possibleRates.DefaultIfEmpty(_config.DefaultRates.GetRate(rate, _isDay)).Max() + GetAdditionalRate(userId);
            return cache[rate] = userRate;
        }

        private float GetAdditionalRate(string userId)
        {
            var possibleRates = _config.AdditionalPermissions.Where(x => permission.UserHasPermission(userId, x.Key))
                .Select(x => x.Value);
            return possibleRates.DefaultIfEmpty(0).Max();
        }
        
        #endregion

        #region API
        [HookMethod("UpdateLootItem")]
        private Item UpdateLootItem(Item item, BasePlayer player)
        {
            if(item == null || !player) 
                return null;
            if (!_config.LootConfig.ShouldDrop(item))
            {
                item.Remove();
                return null;
            }
            if (_config.LootConfig.AllowedItems.ShouldChange(item))
            {
                UpdateAmount(item, RateType.Loot, player.UserIDString, _config.LootConfig.GetModifier(item));
            }
            return item;
        }

        [HookMethod("GetUserRate")]
        private float GetUserRate(string type, string userId)
        {
            switch (type)
            {
                case "Quarry":
                    return GetUserRate(RateType.Quarry, userId);
                case "Excavator":
                    return GetUserRate(RateType.Excavator, userId);
                case "Gather":
                    return GetUserRate(RateType.Gather, userId);
                case "Loot":
                    return GetUserRate(RateType.Loot, userId);
                case "Pickup ":
                    return GetUserRate(RateType.Pickup, userId);
                case "OvenSpeed":
                    return GetUserRate(RateType.OvenSpeed, userId);
                default:
                    return -1;
            }
        }

        #endregion
    }
}
