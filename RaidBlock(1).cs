using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ConVar;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;
using VLB;
using Oxide.Plugins.RaidBlockExt;
using UnityEngine.Networking;
using Layer = Rust.Layer;
using Pool = Facepunch.Pool;
using Time = UnityEngine.Time;

namespace Oxide.Plugins
{
	[Info("RaidBlock", "DezLife & Mercury", "1.1.24")]
	public class RaidBlock : RustPlugin
	{
        /// <summary>
        /// - Исправлена проблема с блокировкой комманд. Нужно заного заполнить команды в конфигурации. без слэша в нижнем регистре
        /// - Добавлена поддержка плагина RaidableBases - настраивается в конфигурации.
        /// </summary>
        
        #region ReferencePlugins

        [PluginReference] Plugin Friends, Clans, IQChat;
        
        #region IQChat

        private List<BasePlayer> playerInCache = new();
        private void SendChat(string message, BasePlayer player, Single timeout = 0f, Chat.ChatChannel channel = Chat.ChatChannel.Global)
        {
            if (playerInCache.Contains(player)) return;
            if (timeout != 0)
            {
                playerInCache.Add(player);
                player.Invoke(() => playerInCache.Remove(player), timeout);
            }
            
            Configuration.IQChat chat = config.IQChatSetting;
            if (IQChat)
                if (chat.UIAlertUse)
                    IQChat?.Call("API_ALERT_PLAYER_UI", player, message);
                else IQChat?.Call("API_ALERT_PLAYER", player, message, chat.CustomPrefix, chat.CustomAvatar);
            else player.SendConsoleCommand("chat.add", channel, 0, message);
        }
        #endregion
        
        private bool IsFriends(BasePlayer player, ulong targetID)
        {
            ulong[] friendList = GetFriendList(player);
            return friendList != null && friendList.Contains(targetID);
        }
        
        private ulong[] GetFriendList(BasePlayer targetPlayer)
        {
            List<ulong> friendList = new();
            if (Friends)
            {
                if (Friends.Call("GetFriends", targetPlayer.userID) is ulong[] friends)
                    friendList.AddRange(friends);
            }
            
            if (Clans)
            {
                if (Clans.Call("GetClanMembers", targetPlayer.UserIDString) is ulong[] clanMembers)
                    friendList.AddRange(clanMembers);
            }

            if(targetPlayer.Team != null)
                friendList.AddRange(targetPlayer.Team.members);

            return friendList.ToArray();
        }

        #endregion

        #region Var
        
        public static RaidBlock Instance;

        private static InterfaceBuilder _interface;
        private List<RaidableZone> _raidZoneComponents = new(); 
        private static ImageUI _imageUI;
        private const Boolean LanguageEn = true;
        
        private const string GENERIC_MAP_MARKER_PREFAB = "assets/prefabs/tools/map/genericradiusmarker.prefab";
        private const string EXPLOSION_MAP_MARKER_PREFAB = "assets/prefabs/tools/map/explosionmarker.prefab";
        private const string VENDING_MAP_MARKER_PREFAB = "assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab";
        private const string VISUALIZATION_SPHERE_PREFAB = "assets/prefabs/visualization/sphere.prefab";
        
        private const string VISUALIZATION_BR_SPHERE_PREFAB_BLUE = "assets/bundled/prefabs/modding/events/twitch/br_sphere.prefab";
        private const string VISUALIZATION_BR_SPHERE_PREFAB_GREEN = "assets/bundled/prefabs/modding/events/twitch/br_sphere_green.prefab";
        private const string VISUALIZATION_BR_SPHERE_PREFAB_PURPLE = "assets/bundled/prefabs/modding/events/twitch/br_sphere_purple.prefab";
        private const string VISUALIZATION_BR_SPHERE_PREFAB_RED = "assets/bundled/prefabs/modding/events/twitch/br_sphere_red.prefab";
        
        private const string RB_WHITE_AND_BLACK_LIST_ITEM = "toolgun";
        private const ulong RB_WHITELIST_ITEM_SKIN = 3100653846;
        private const ulong RB_BLACKLIST_ITEM_SKIN = 3100653751;
        
        private readonly string _permIgnoreRaid = ".ignore";
        private readonly string _permHelperToolGun = ".toolgun";


        #endregion

        #region Types

        private enum SphereTypes
        {
            BlackSphere,
            BRSphere,
        }
        
        private enum BRZoneColor
        {
            Blue,
            Green,
            Purple,
            Red
        }
        
        private enum MarkerTypes
        {
            Explosion,
            MarkerRadius,
            ExplosionMarkerRadius,
            MarkerRadiusTimer,
        }

        #endregion

        #region Configuration
        private Configuration config;

        private class Configuration
        {
            public class IQChat
            {
                [JsonProperty(LanguageEn ? "IQChat : Custom prefix in the chat" : "IQChat : Кастомный префикс в чате")]
                public String CustomPrefix = "[RaidBlock]";
                [JsonProperty(LanguageEn ? "IQChat : Custom avatar in the chat (If required)" : "IQChat : Кастомный аватар в чате(Если требуется)")]
                public String CustomAvatar = "0";
                [JsonProperty(LanguageEn ? "IQChat : Use UI notifications" : "IQChat : Использовать UI уведомления")]
                public Boolean UIAlertUse = false;
            }
            
            public class RaidableBases
            {
                [JsonProperty(LanguageEn ? "RaidableBases: Enable support for Raidable Bases?" : "RaidableBases : Включить поддержку Raidable Bases ?")]
                public bool useRaidableBases = false;
                [JsonProperty(LanguageEn ? "RaidableBases: Blocking time (Seconds)" : "RaidableBases: Время блокировки (Секунды)")]
                public int RaidBlockDuration = 600;
                [JsonProperty(LanguageEn ? "RaidableBases : Give a raid block when entering the Raidable Bases zone" : "RaidableBases : Давать рейд блок при входе в зону Raidable Bases")]
                public bool BlockOnEnterZone = false;
                [JsonProperty(LanguageEn ? "RaidableBases : Remove the raid block when leaving the Raidable Bases zone" : "RaidableBases : Снимать рейд блок при выходе из зоны Raidable Bases")]
                public bool BlockOnExitZone = false;
            }
            public class RaidBlockActionsBlocked
            {
                [JsonProperty(LanguageEn ? "Forbid building repair" : "Запретить починку строений")]
                public bool CanRepairObjects = true;
                [JsonProperty(LanguageEn ? "Forbid picking up items (furnaces, boxes, etc.)" : "Запретить поднятие вещей (печки/ящики и т.д)")]
                public bool CanPickUpObjects = false;
                [JsonProperty(LanguageEn ? "Forbid upgrading buildings" : "Запретить улучшение строений")]
                public bool CanUpgradeObjects = true;
                [JsonProperty(LanguageEn ? "Forbid building removal" : "Запретить удаление строений")]
                public bool CanDemolishObjects = true;
                [JsonProperty(LanguageEn ? "Forbid teleportation" : "Запретить телепортацию")]
                public bool CanUseTeleport = true;
                [JsonProperty(LanguageEn ? "Forbid the use of kits" : "Запретить использование китов")]
                public bool CanUseKit = true;
                [JsonProperty(LanguageEn ? "Forbid the use of trade" : "Запретить использование обмена (Trade)")]
                public bool CanUseTrade = true;
                [JsonProperty(LanguageEn ? "Allow building" : "Запретить строение")] 
                public bool CanBuildTwig = true;
                [JsonProperty(LanguageEn ? "Allow object placement (furnaces, boxes, etc.)" : "Запретить размещение объектов (Печки, ящики и другое)")]
                public bool CanDeployObjects = false;
                [JsonProperty(LanguageEn ? "List of objects allowed to build/place during the raidblock (shortname)" : "Список объектов, которые разрешено строить/размещать во время рейдблока (PrefabName)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<string> RbDeployWhiteList = new()
                {
                    "bed_deployed",
                    "ladder.wooden.wall",
                };
                [JsonProperty(LanguageEn ? "List of prohibited commands when blocking is active [specify them without a slash (/) in lower case]" : "Список запрещенных команд при активной блокировки [указывайте их без слэша (/) в нижнем регистре]", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<string> RbBlackListCommand = new()
                {
                    "commandexample",
                };
            }
            public class RaidBlockDetect
            {
                [JsonProperty(LanguageEn ? "List of items that will trigger a raid block upon destruction (prefabID)" : "Список предметов за которые при уничтожении будет даваться рейдблок (prefabID)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public HashSet<uint> RbBlackList = new()
                {
                    12312312,
                };
                [JsonProperty(LanguageEn ? "List of items that will not trigger a raid block upon destruction (prefabID)" : "Список предметов за которые при уничтожении не будет даваться рейдблок (prefabID)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public HashSet<uint> RbWhiteList = new()
                {
                    1231223,
                };
                [JsonProperty(LanguageEn ? "Ignore objects with a maximum health state less than N (0 - disabled)" : "Игнорировать объекты с максимальным состоянием здоровья меньше N (0 - отключено)")]
                public int IgnoreEntsHealth = 0;
                [JsonProperty(LanguageEn ? "Activate raidblock upon the destruction of own or friends' buildings" : "Активировать рейдблок при уничтожении собственного строения или строения друзей")]
                public bool RaidYourself = false;
                [JsonProperty(LanguageEn ? "Activate raidblock if there is no tool cupboard in the building" : "Активировать рейдблок если в строении нет шкафа")]
                public bool CanRaidIfNotCupboard = false;
            }

            public class RaidBlock
            {
                [JsonProperty(LanguageEn ? "Radius of blocking zone (Meters)" : "Радиус зоны блокировки (Метры)")]
                public int RaidBlockDistance = 130;
                [JsonProperty(LanguageEn ? "Blocking time (Seconds)" : "Время блокировки (Секунды)")]
                public int RaidBlockDuration = 300;
                [JsonProperty(LanguageEn ? "Use dynamic raid zone (shift the zone center to the explosion location)" : "Использовать динамичную зону рейда (смещение центра зоны к месту взрыва)")]
                public bool IsDynamicRaidZone = false;
                [JsonProperty(LanguageEn ? "Spread raidblock to all team players" : "Распространять рейд блок на всех игроков в команде инициатора рейда")]
                public bool RaidBlockShareOnFriends = true; 
                [JsonProperty(LanguageEn ? "When entering the active raid block zone, activate the player raid block" : "При входе в активную зону рейдблока - активировать рейдблок игроку")]
                public bool RaidBlockOnEnterRaidZone = true;
                [JsonProperty(LanguageEn ? "Upon exiting the raid block zone, deactivate the raid block for the player" : "При выходе из зоны рейдблока - деактивировать рейдблок игроку")]
                public bool RaidBlockOnExitRaidZone = false;
                [JsonProperty(LanguageEn ? "When exiting the raid block zone, leave a lock for N seconds (leave 0 if you don't need it)" : "При выходе из зоны рейдблока - оставлять N секунд блокировки (оставьте 0 - если вам не нужно это)")] 
                public Int32 TimeLeftOnExitZone = 0;
                [JsonProperty(LanguageEn ? "Activate raid block for all players within the effective radius after the raid starts" : "Активировать рейдблок для всех игроков в радиусе действия после начала рейда")]
                public bool RaidBlockAddedAllPlayersInZoneRaid = true;
                [JsonProperty(LanguageEn ? "Deactivate the raid block for the player upon death" : "Деактивировать рейдблок игроку после смерти")]
                public bool RaidBlockOnPlayerDeath = true;
                [JsonProperty(LanguageEn ? "Raid zone map marker settings" : "Настройки маркера на карте в зоне рейда")]
                public MapMarkerSettings mapMarkerSettings = new();
                [JsonProperty(LanguageEn ? "Visual raid zone (Dome) settings" : "Настройка визуальной зоны рейда (купол)")]
                public SphereSettings RaidZoneSphereSettings = new();
                [JsonProperty(LanguageEn ? "Integration settings with RaidableBases" : "Настройки интеграции с RaidableBases")]
                public RaidableBases RaidableBasesIntegration = new();

                public class SphereSettings
                {
                    [JsonProperty(LanguageEn ? "Activate visual raid zone (Dome)" : "Активировать визуальную зону рейда (купол)")]
                    public bool IsSphereEnabled = false;
                    [JsonProperty(LanguageEn ? "Choose marker type: 0 - standard dome, 1 - BattleRoyale dome" : "Выберете тип купола : 0 - cтандартный купол, 1 - BattleRoyale купол")]
                    public SphereTypes SphereType = SphereTypes.BlackSphere;
                    [JsonProperty(LanguageEn ? "Color for BattleRoyale dome: 0 - blue | 1 - green | 2 - purple | 3 - red" : "Цвет для BattleRoyale купола : 0 - blue | 1 - green | 2 - purple | 3 - red")]
                    public BRZoneColor BRZoneColor = BRZoneColor.Blue;
                    [JsonProperty(LanguageEn ? "Transparency level of the standard dome (Lower values mean more transparency. The value should not exceed 5)" : "Уровень прозрачности стандартного купола (Чем меньше - тем прозрачнее. Значения должно быть не более 5)")]
                    public int DomeTransparencyLevel = 3;
                    
                    public string GetBRZonePrefab()
                    {
                        return BRZoneColor switch
                        {
                            BRZoneColor.Blue => VISUALIZATION_BR_SPHERE_PREFAB_BLUE,
                            BRZoneColor.Green => VISUALIZATION_BR_SPHERE_PREFAB_GREEN,
                            BRZoneColor.Purple => VISUALIZATION_BR_SPHERE_PREFAB_PURPLE,
                            BRZoneColor.Red => VISUALIZATION_BR_SPHERE_PREFAB_RED,
                            _ => throw new ArgumentOutOfRangeException(nameof(BRZoneColor), BRZoneColor, null)
                        };
                    }
                }
                
                public class MapMarkerSettings
                {
                    [JsonProperty(LanguageEn ? "Display the block zone on the G map" : "Отображать зону блокировки на G карте")]
                    public bool IsRaidBlockMarkerEnabled = true;
                    [JsonProperty(LanguageEn ? "Choose marker type: 0 - Explosion, 1 - Circle, 2 - Explosion + Circle, 3 - Circle + Timer" : "Выберите тип маркера: 0 - Explosion | 1 - Marker Radius | 2 - Explosion + Marker Radius | 3 - Marker Radius + Timer")]
                    public MarkerTypes RaidBlockMarkerType = MarkerTypes.ExplosionMarkerRadius;
                    [JsonProperty(LanguageEn ? "Marker color (without #) (For marker types 1, 2, and 3)" : "Цвет маркера (без #) (Для маркера типа 1, 2 и 3)")]
                    public string MarkerColorHex = "f3ecad";
                    [JsonProperty(LanguageEn ? "Outline color (without #) (For marker types 1, 2, and 3)" : "Цвет обводки (без #) (Для маркера типа 1, 2 и 3)")]
                    public string OutlineColorHex = "ff3535";
                    [JsonProperty(LanguageEn ? "Marker transparency (For marker types 1, 2, and 3)" : "Прозрачность маркера (Для маркера типа 1, 2 и 3)")]
                    public float MarkerAlpha = 0.5f;
                }
            }
            
            public class RaidBlockUi
            {
                [JsonProperty(LanguageEn ? "Interface variant (0, 1, 2) - example: " : "Вариант интерфейса (0, 1, 2)")]
                public int UiType = 0;
                [JsonProperty(LanguageEn ? "Interface layer: Overlay - will overlay other UI, Hud - will be overlaid by other interfaces" : "Слой интерфейса : Overlay - будет перекрывать другие UI, Hud - будет перекрываться другим интерфейсом")]
                public string Layers = "Hud";
                [JsonProperty(LanguageEn ? "Vertical padding" : "Вертикальный отступ")]
                public int OffsetY = 0;
                [JsonProperty(LanguageEn ? "Horizontal padding" : "Горизонтальный отступ")]
                public int OffsetX = 0;
                
                [JsonProperty(LanguageEn ? "Interface settings for variant 0" : "Настройки интерфейса для варианта 0")]
                public RaidBlockUiSettings InterfaceSettingsVariant0 = new()
                {
                    BackgroundColor = "0.1921569 0.1921569 0.1921569 1",
                    IconColor = "0 0.7764706 1 1",
                    AdditionalElementsColor = "",
                    MainTextColor = "1 1 1 1",
                    SecondaryTextColor = "1 1 1 0.5019608",
                    ProgressBarMainColor = "0.3411765 0.5490196 0.9607843 1",
                    ProgressBarBackgroundColor = "1 1 1 0.1019608",
                    SmoothTransition = 0.222f,
                };
                
                [JsonProperty(LanguageEn ? "Interface settings for variant 1" : "Настройки интерфейса для варианта 1")]
                public RaidBlockUiSettings InterfaceSettingsVariant1 = new()
                {
                    BackgroundColor = "0.9607843 0.772549 0.7333333 0.7019608",
                    IconColor = "1 1 1 1",
                    AdditionalElementsColor = "0.9215686 0.3058824 0.172549 1",
                    MainTextColor = "0.1921569 0.1921569 0.1921569 1",
                    SecondaryTextColor = "0.1320755 0.1320755 0.1320755 1",
                    ProgressBarMainColor = "0.9215686 0.3058824 0.172549 1",
                    ProgressBarBackgroundColor = "1 1 1 0.4117647",
                    SmoothTransition = 0.222f
                };
                
                [JsonProperty(LanguageEn ? "Interface settings for variant 2" : "Настройки интерфейса для варианта 2")]
                public RaidBlockUiSettings InterfaceSettingsVariant2 = new()
                {
                    BackgroundColor = "0.1921569 0.1921569 0.1921569 1",
                    IconColor = "0.9411765 0.3137255 0.286 1",
                    AdditionalElementsColor = "0.9568627 0.3607843 0.2623232 1",
                    MainTextColor = "1 1 1 1",
                    SecondaryTextColor = "1 1 1 0.5019608",
                    ProgressBarMainColor = "1 1 1 1",
                    ProgressBarBackgroundColor = "1 1 1 0.4117647",
                    SmoothTransition = 0.222f
                };
                
                public class RaidBlockUiSettings
                {
                    [JsonProperty(LanguageEn ? "Background color (RGBA)" : "Цвет фона (RGBA)")]
                    public string BackgroundColor;
                    [JsonProperty(LanguageEn ? "Icon color (RGBA)" : "Цвет иконки (RGBA)")]
                    public string IconColor;
                    [JsonProperty(LanguageEn ? "Color of additional elements (RGBA)" : "Цвет дополнительных элементов (RGBA)")]
                    public string AdditionalElementsColor;
                    [JsonProperty(LanguageEn ? "Main text color (RGBA)" : "Цвет основного текста (RGBA)")]
                    public string MainTextColor;
                    [JsonProperty(LanguageEn ? "Secondary text Color (RGBA)" : "Цвет второстепенного текста (RGBA)")]
                    public string SecondaryTextColor;
                    [JsonProperty(LanguageEn ? "Main color of the progress-bar (RGBA)" : "Основной цвет прогресс-бара (RGBA)")]
                    public string ProgressBarMainColor;
                    [JsonProperty(LanguageEn ? "Background Color of the Progress Bar (RGBA)" : "Цвет фона прогресс-бара (RGBA)")]
                    public string ProgressBarBackgroundColor;
                    [JsonProperty(LanguageEn ? "Delay before the UI appears and disappears (for smooth transitions)" : "Задержка перед появлением и исчезновением UI (для плавности)")]
                    public float SmoothTransition;
                }
            }
            
            [JsonProperty(LanguageEn ? "Main raidblock settings" : "Основные настройки рейд-блока")] 
            public RaidBlock RaidBlockMain = new();
            [JsonProperty(LanguageEn ? "Setting up triggers for the raidblock" : "Настройка триггеров для рейдблока")] 
            public RaidBlockDetect BlockDetect = new();
            [JsonProperty(LanguageEn ? "Setting restrictions during the raid block" : "Настройка ограничений во время рейдблока")] 
            public RaidBlockActionsBlocked ActionsBlocked = new();
            [JsonProperty(LanguageEn ? "Interface settings" : "Настройки интерфейса")] 
            public RaidBlockUi RaidBlockInterface = new();
            [JsonProperty(LanguageEn ? "Setting IQChat" : "Настройка IQChat")]
            public IQChat IQChatSetting = new IQChat();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new Exception();
                }

                SaveConfig();
            }
            catch
            {
                for (int i = 0; i < 3; i++)
                {
                    PrintError(LanguageEn ? "Configuration file is corrupt! Check your config file at https://jsonlint.com/" : "Вы допустили ошибку синтаксиса в конфигурационном файле! Проверьте файл на https://jsonlint.com/");
                }

                LoadDefaultConfig();
            }
            ValidateConfig();
            SaveConfig();
        }

        private void ValidateConfig()
        {
            if (config.RaidBlockMain.RaidZoneSphereSettings.DomeTransparencyLevel > 5)
            {
                config.RaidBlockMain.RaidZoneSphereSettings.DomeTransparencyLevel = 3;
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
        }
        #endregion  
        
        #region Lang
		
		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
                ["RAIDBLOCK_ACTION_BLOCKED"] = "You are not allowed to perform this action during a raid. Please wait {0}.",
                ["RAIDBLOCK_ENTER_RAID_ZONE"] = "You have entered the raid zone! Some features will be restricted for {0}.",
                ["RAIDBLOCK_EXIT_RAID_ZONE"] = "You have exited the raid zone! Features are now unlocked.",
                ["RAIDBLOCK_END_RAID"] = "The block has been deactivated. Features are now unlocked.",
                ["RAIDBLOCK_ENTER_RAID_INITIATOR"] = "You destroyed someone else's object! Raid block activated for {0}.\nSome features are temporarily unavailable.",
                ["RAIDBLOCK_OWNER_NOTIFY"] = "Your base in quadrant {0} has been raided!",
                ["RAIDBLOCK_UI_IN_RAIDZONE"] = "YOU ARE IN THE RAID ZONE",
                ["RAIDBLOCK_UI_TIMER"] = "Time left: {0}",
                ["RAIDBLOCK_UI_TITLE"] = "RAID BLOCK",
                ["RAIDBLOCK_UI_DESCRIPTIONS_V1"] = "It seems that you are being raided, and some features have been restricted.",
                ["RAIDBLOCK_UI_DESCRIPTIONS_V2"] = "Some features have been disabled.",
                ["RAIDBLOCK_KILL_PLAYER"] = "You were killed! Functions are unlocked.",
                ["RAIDBLOCK_TOOLGUN_BLACKLIST_ITEM_NAME"] = "RAID BLOCK - BLACK LIST",
                ["RAIDBLOCK_TOOLGUN_WHITELIST_ITEM_NAME"] = "RAID BLOCK - WHITE LIST",
                ["RAIDBLOCK_TOOLGUN_PERMISSION_DENIED"] = "You do not have permission to use this command",
                ["RAIDBLOCK_TOOLGUN_ALREADY_IN_INVENTORY"] = "You already have '<color=#FFA500>ToolGun - {0}</color>' in your inventory",
                ["RAIDBLOCK_TOOLGUN_REMOVED_FROM_WHITELIST"] = "The item '<color=#FF6347>{0} ({1})</color>' has been removed from the whitelist",
                ["RAIDBLOCK_TOOLGUN_ADDED_TO_WHITELIST"] = "The item '<color=#00FF7F>{0} ({1})</color>' has been added to the whitelist",
                ["RAIDBLOCK_TOOLGUN_REMOVED_FROM_BLACKLIST"] = "The item '<color=#FF3232>{0} ({1})</color>' has been removed from the blacklist",
                ["RAIDBLOCK_TOOLGUN_ADDED_TO_BLACKLIST"] = "The item '<color=#00FF7F>{0} ({1})</color>' has been added to the blacklist",
                ["RAIDBLOCK_TOOLGUN_BLACKLIST_USAGE"] = "You have received '<color=#FF6347>ToolGun - {0}</color>'.\nTo add an item to the blacklist - shoot it.\nA repeat shot will remove the item from the blacklist.\nFor destroyed items in this list - a raid block will be activated",
                ["RAIDBLOCK_TOOLGUN_WHITELIST_USAGE"] = "You have received '<color=#FF6347>ToolGun - {0}</color>'.\nTo add an item to the whitelist - shoot it.\nA repeat shot will remove the item from the whitelist.\nFor destroyed items in this list - a raid block will not be activated",
            }, this);
            
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["RAIDBLOCK_ACTION_BLOCKED"] = "Вам запрещено совершать это действие во время рейда. Подождите {0}.",
                ["RAIDBLOCK_ENTER_RAID_ZONE"] = "Вы вошли в зону рейда! Некоторые функции будут ограничены в течение {0}.",
                ["RAIDBLOCK_EXIT_RAID_ZONE"] = "Вы вышли из зоны рейда! Функции разблокированы.",
                ["RAIDBLOCK_END_RAID"] = "Блок деактивирован. Функции разблокированы.",
                ["RAIDBLOCK_ENTER_RAID_INITIATOR"] = "Вы уничтожили чужой объект! Активирован рейд блок на {0}.\nНекоторые функции временно недоступны.",
                ["RAIDBLOCK_OWNER_NOTIFY"] = "Ваш дом в квадрате {0} подвергся рейду!",
                ["RAIDBLOCK_UI_IN_RAIDZONE"] = "ВЫ НАХОДИТЕСЬ В ЗОНЕ РЕЙДА",
                ["RAIDBLOCK_UI_TIMER"] = "Осталось {0}",
                ["RAIDBLOCK_UI_TITLE"] = "RAID BLOCK",
                ["RAIDBLOCK_UI_DESCRIPTIONS_V1"] = "Похоже, что вас начали рейдить, и некоторые функции были ограничены.",
                ["RAIDBLOCK_UI_DESCRIPTIONS_V2"] = "Некоторые функции были отключены.",
                ["RAIDBLOCK_KILL_PLAYER"] = "Вы были убиты! Функции разблокированы.",
                ["RAIDBLOCK_TOOLGUN_BLACKLIST_ITEM_NAME"] = "RAID BLOCK - BLACK LIST",
                ["RAIDBLOCK_TOOLGUN_WHITELIST_ITEM_NAME"] = "RAID BLOCK - WHITE LIST",
                ["RAIDBLOCK_TOOLGUN_PERMISSION_DENIED"] = " У вас нет прав для использования этой команды",
                ["RAIDBLOCK_TOOLGUN_ALREADY_IN_INVENTORY"] = "у вас уже есть '<color=#FFA500>ToolGun - {0}</color>' в инвентаре",
                ["RAIDBLOCK_TOOLGUN_REMOVED_FROM_WHITELIST"] = "Предмет '<color=#FF6347>{0} ({1})</color>' был удален из белого списка",
                ["RAIDBLOCK_TOOLGUN_ADDED_TO_WHITELIST"] = "Предмет '<color=#00FF7F>{0} ({1})</color>' был добавлен в белый список",
                ["RAIDBLOCK_TOOLGUN_REMOVED_FROM_BLACKLIST"] = "Предмет '<color=#FF3232>{0} ({1})</color>' был удален из черного списка",
                ["RAIDBLOCK_TOOLGUN_ADDED_TO_BLACKLIST"] = "Предмет '<color=#00FF7F>{0} ({1})</color>' был добавлен в черный список",
                ["RAIDBLOCK_TOOLGUN_BLACKLIST_USAGE"] = " Вы получили '<color=#FF6347>ToolGun - {0}</color>'.\nЧтобы занести предмет в черный список - выстрелите в него.\nПовторный выстрел удалит предмет из черного списка.\nЗа уничтоженные предметы в данном списке - будет активирован рейдблок",
                ["RAIDBLOCK_TOOLGUN_WHITELIST_USAGE"] = " Вы получили '<color=#FF6347>ToolGun - {0}</color>'.\nЧтобы занести предмет в белый список - выстрелите в него.\nПовторный выстрел удалит предмет из белого списка.\nЗа уничтоженные предметы в данном списке - не будет активирован рейдблок",
            }, this, "ru");
        }

		#endregion
		
		#region Hooks

        private void Unload()
        {
            foreach (RaidableZone obj in _raidZoneComponents) 
                UnityEngine.Object.DestroyImmediate(obj);

            foreach (RaidPlayer rPlayer in raidPlayersList)
                if(rPlayer != null)
                    rPlayer.Kill(true);

            if (_imageUI != null)
            {
                _imageUI.UnloadImages();
                _imageUI = null;
            }
            _interface = null;
            Instance = null;
        }

        private void Init()
        {
            Instance = this;
            UnsubscribeHook(true, true);
        }
        
        private void OnServerInitialized()
        {
            permission.RegisterPermission(Name + _permIgnoreRaid,this);
            permission.RegisterPermission(Name + _permHelperToolGun,this);

            _imageUI = new ImageUI();
            _imageUI.DownloadImage();
            
            SubscribeHook(true, false);
        }
        
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info) => OnEntCheck(entity, info);
        
        private void OnPlayerConnected(BasePlayer player)
        {
            if (player.TryGetComponent(out RaidPlayer rp))
                rp.CrateUI();
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (player == null || player.IsDead() || !player.IsConnected) return;
            RaidableZone rbZone = GetRbZone(player.transform.position);
            if (rbZone == null) return;
            rbZone.AddPlayer(player);
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo hitInfo)
        {
            if (player == null || hitInfo == null || !player.userID.IsSteamId())
                return;
            RaidPlayer raidPlayer = GetRaidPlayer(player);
            if (raidPlayer != null)
            {
                Instance.SendChat("RAIDBLOCK_KILL_PLAYER".GetAdaptedMessage(player.UserIDString), player);
                UnityEngine.Object.DestroyImmediate(raidPlayer);
            }
        }

        #endregion

        #region [Raidable Bases]

        void OnPlayerEnteredRaidableBase(BasePlayer player, Vector3 raidPos, bool allowPVP, int mode)
        {
            float time = config.RaidBlockMain.RaidableBasesIntegration.RaidBlockDuration;
            RaidPlayer raidPlayer = player.GetOrAddComponent<RaidPlayer>();
            SendChat("RAIDBLOCK_ENTER_RAID_ZONE".GetAdaptedMessage(player.UserIDString, time.ToTimeFormat()), player, 3f);
            
            raidPlayer.ActivateBlock(Time.realtimeSinceStartup + time);

            Interface.CallHook("OnEnterRaidZone", player);
        }

        void OnPlayerExitedRaidableBase(BasePlayer player, Vector3 raidPos, bool allowPVP, int mode)
        {
            RaidPlayer rp = GetRaidPlayer(player);
            if (rp != null)
            {
                UnityEngine.Object.DestroyImmediate(rp);
                SendChat("RAIDBLOCK_EXIT_RAID_ZONE".GetAdaptedMessage(player.UserIDString), player);

                Interface.CallHook("OnExitRaidZone", player);
            }
            
            RaidableZone rbZone = GetRbZone(player.transform.position);
            if (rbZone == null) return;
            rbZone.AddPlayer(player);
        }

        #endregion

        #region HooksBlockedActions
 
        private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            BasePlayer player = planner.GetOwnerPlayer();
            if (player == null) return null;
            string shortname = prefab.hierachyName[(prefab.hierachyName.IndexOf("/", StringComparison.Ordinal) + 1)..];
            
            if (config.ActionsBlocked.RbDeployWhiteList.Contains(shortname))
                return null;
            Deployable deployable = planner.GetDeployable();
            if (deployable != null && !config.ActionsBlocked.CanDeployObjects)
                return null;
            if (prefab.defaultGrade != null && !config.ActionsBlocked.CanBuildTwig)
                return null;

            return CanActions(player);
        }
        
        private object OnPlayerCommand(BasePlayer player, String command, String[] args)
        {
            if (player == null) return null;
            
            if (args != null && args.Length != 0)
                command += " " + String.Join(" ", args);
            
            String onlyCommand = !String.IsNullOrWhiteSpace(command) && command.Contains(" ") ? command.Substring(0, command.IndexOf(" ", StringComparison.Ordinal)) : command;
            
            return config.ActionsBlocked.RbBlackListCommand.Contains(onlyCommand.ToLower()) ? CanActions(player) : null;
        }

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || arg.cmd.FullName == "chat.say") return null;
			
            String command = arg.cmd.Name;
            if (arg.Args != null && arg.Args.Length != 0)
                command += " " + String.Join(" ", arg.Args);
            
            String onlyCommand = !String.IsNullOrWhiteSpace(command) && command.Contains(" ") ? command.Substring(0, command.IndexOf(" ", StringComparison.Ordinal)) : command;
			
            return config.ActionsBlocked.RbBlackListCommand.Contains(onlyCommand.ToLower()) ? CanActions(player) : null;
        }
        
        private object OnStructureRepair(BaseCombatEntity entity, BasePlayer player) => CanActions(player);
        private object OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade) => CanActions(player);
        private object OnStructureDemolish(BaseCombatEntity entity, BasePlayer player, bool immediate) => CanActions(player);
        private object OnStructureRotate(BaseCombatEntity entity, BasePlayer player) => CanActions(player);

        private object CanPickupEntity(BasePlayer player, BaseEntity entity) => CanActions(player);
        #endregion

        #region Api
        private object CanBGrade(BasePlayer player, int grade, BuildingBlock buildingBlock, Planner planner) => CanActions(player); 
        private object CanTeleport(BasePlayer player) => CanActions(player, true);
        private object canTeleport(BasePlayer player) => CanActions(player);
        private object CanRedeemKit(BasePlayer player) => CanActions(player);
        private object canRemove(BasePlayer player) => CanActions(player);
        private object CanRemove(BasePlayer player) => CanActions(player);
        private object canTrade(BasePlayer player) => CanTrade(player);
        private object CanTrade(BasePlayer player) => CanActions(player, true);

        #region Call

        private bool IsBlocked(BasePlayer player)
        {
            RaidPlayer obj = player.GetComponent<RaidPlayer>();
            return obj != null && obj.UnblockTimeLeft > 0;
        }
        
        private RaidPlayer GetRaidPlayer(BasePlayer player)
        {
            RaidPlayer obj = player.GetComponent<RaidPlayer>();
            return obj;
        }
        
        private bool IsRaidBlocked(string playerId)
        {
            BasePlayer target = BasePlayer.Find(playerId);
            return target != null && IsBlocked(target);
        }
        
        private bool IsRaidBlocked(ulong playerId)
        {
            BasePlayer target = BasePlayer.Find(playerId.ToString());
            return target != null && IsBlocked(target);
        }
        
        private bool IsRaidBlocked(BasePlayer player) => IsBlocked(player);
        
        private bool IsRaidBlock(ulong userId) => IsRaidBlocked(userId);
        
        private int ApiGetTime(ulong userId)
        {
            if (!IsRaidBlocked(userId.ToString())) return 0;
            BasePlayer player = BasePlayer.FindByID(userId);
            if (player == null) return 0;
            RaidPlayer obj = player.GetComponent<RaidPlayer>();
            return obj == null ? 0 : int.Parse(obj.UnblockTimeLeft.ToString(CultureInfo.InvariantCulture));
        }
        #endregion
        
        #endregion
        
        #region Metods

        private void CheckUnsubscribeOrSubscribeHooks()
        {
            if (_raidZoneComponents.Count == 0)
                UnsubscribeHook(false, true);
            else
                SubscribeHook(false, true);
        }

        private void SubscribeHook(bool main, bool raidActions)
        {
            if (main)
            {
                Subscribe(nameof(OnEntityDeath));
                Subscribe(nameof(OnPlayerConnected));
                if (config.RaidBlockMain.RaidableBasesIntegration.useRaidableBases)
                {
                    if(config.RaidBlockMain.RaidableBasesIntegration.BlockOnEnterZone)
                        Subscribe(nameof(OnPlayerEnteredRaidableBase));
                    if(config.RaidBlockMain.RaidableBasesIntegration.BlockOnExitZone)
                        Subscribe(nameof(OnPlayerExitedRaidableBase));
                }
            }

            if (raidActions)
            {
                
                if (config.ActionsBlocked.RbBlackListCommand != null && config.ActionsBlocked.RbBlackListCommand.Count != 0)
                {
                    if (config.ActionsBlocked.RbBlackListCommand.Count == 1 && config.ActionsBlocked.RbBlackListCommand[0].Equals("commandExample")) return;
				
                    Subscribe(nameof(OnPlayerCommand));
                    Subscribe(nameof(OnServerCommand));
                }
                if (config.RaidBlockMain.RaidBlockOnPlayerDeath)
                {
                    Subscribe(nameof(OnPlayerDeath));
                }
                if (config.ActionsBlocked.CanUseKit)
                {
                    Subscribe(nameof(CanRedeemKit));
                }
                if (config.ActionsBlocked.CanUseTeleport)
                {
                    Subscribe(nameof(canTeleport));
                    Subscribe(nameof(CanTeleport));
                }
                if (config.ActionsBlocked.CanUseTrade)
                {
                    Subscribe(nameof(canTrade));
                    Subscribe(nameof(CanTrade));
                }
                if (config.ActionsBlocked.CanDemolishObjects)
                {
                    Subscribe(nameof(OnStructureDemolish));
                    Subscribe(nameof(canRemove));
                    Subscribe(nameof(CanRemove));
                }
                if (config.ActionsBlocked.CanRepairObjects)
                {
                    Subscribe(nameof(OnStructureRepair));
                }
                if (config.ActionsBlocked.CanBuildTwig || config.ActionsBlocked.CanDeployObjects || config.ActionsBlocked.RbDeployWhiteList.Count > 0)
                {
                    Subscribe(nameof(CanBuild));
                }
                if (config.ActionsBlocked.CanUpgradeObjects)
                {
                    Subscribe(nameof(OnStructureUpgrade));
                    Subscribe(nameof(CanBGrade));
                    Subscribe(nameof(OnStructureRotate));
                }
                if (config.ActionsBlocked.CanPickUpObjects)
                {
                    Subscribe(nameof(CanPickupEntity));
                }
            }
        }

        private void UnsubscribeHook(bool main, bool raidActions)
        {
            if (main)
            {
                Unsubscribe(nameof(OnEntityDeath));
                Unsubscribe(nameof(OnPlayerConnected));
                Unsubscribe(nameof(OnPlayerEnteredRaidableBase));
                Unsubscribe(nameof(OnPlayerExitedRaidableBase));
            }

            if (raidActions)
            {
                Unsubscribe(nameof(OnPlayerDeath));
                Unsubscribe(nameof(CanBGrade));
                Unsubscribe(nameof(CanBuild));
                Unsubscribe(nameof(OnStructureUpgrade));
                Unsubscribe(nameof(OnStructureRepair));
                Unsubscribe(nameof(OnStructureDemolish));
                Unsubscribe(nameof(OnStructureRotate));
                Unsubscribe(nameof(canTeleport));
                Unsubscribe(nameof(CanTeleport));
                Unsubscribe(nameof(OnPlayerCommand));
                Unsubscribe(nameof(OnServerCommand));
                Unsubscribe(nameof(canRemove));
                Unsubscribe(nameof(CanRemove));
                Unsubscribe(nameof(canTrade));
                Unsubscribe(nameof(CanTrade));
                Unsubscribe(nameof(CanRedeemKit));
                Unsubscribe(nameof(CanPickupEntity));
            }
            
        }
        private static void RunEffect(BasePlayer player, string prefab)
        {
            Effect effect = new Effect();
            effect.Init(Effect.Type.Generic, player.transform.position, Vector3.zero);
            effect.pooledString = prefab;
            EffectNetwork.Send(effect, player.net.connection);
        }

        private object CanActions(BasePlayer player, bool returnMessage = false)
        {
            RaidPlayer playPlayer = player.GetComponent<RaidPlayer>();
            if (playPlayer != null && playPlayer.UnblockTimeLeft > 0)
            {
                RunEffect(player, "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab");
                if (returnMessage == false)
                {
                    SendChat("RAIDBLOCK_ACTION_BLOCKED".GetAdaptedMessage(player.UserIDString, playPlayer.UnblockTimeLeft.ToTimeFormat()), player);
                    return false;
                }
                else
                {
                    return "RAIDBLOCK_ACTION_BLOCKED".GetAdaptedMessage(player.UserIDString, playPlayer.UnblockTimeLeft.ToTimeFormat());
                }
            }
            
            return null;
        }
        
        private void OnEntCheck(BaseCombatEntity entity, HitInfo info)
        {
            DamageType? majorityDamageType = info?.damageTypes.GetMajorityDamageType();
            if (majorityDamageType == DamageType.Decay)
                return;

            if(!config.BlockDetect.CanRaidIfNotCupboard && entity.GetBuildingPrivilege() == null)
                return;

            BasePlayer raider = info?.InitiatorPlayer ? info.InitiatorPlayer : entity.lastAttacker as BasePlayer;
            if(raider == null) return;
            
            if (IsBlackList(entity) || (IsBlockedClass(entity) && !IsWhiteList(entity)))
            {
                if (CheckEntity(entity, info, raider))
                {
                    BasePlayer ownerPlayer = BasePlayer.FindByID(entity.OwnerID);
                    if (ownerPlayer != null && ownerPlayer.IsConnected)
                        SendChat("RAIDBLOCK_OWNER_NOTIFY".GetAdaptedMessage(ownerPlayer.UserIDString, GetGridString(entity.transform.position)), ownerPlayer, 3f);

                    CreateOrRefreshRaidblock(entity.transform.position, raider);
                }
            }
        }
        private void CreateOrRefreshRaidblock(Vector3 position, BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, Name + _permIgnoreRaid)) 
                return;
            
            if (Interface.Call("CanRaidBlock", player, position) != null)
                return;

            RaidableZone raidableZone = _raidZoneComponents != null && _raidZoneComponents.Count != 0 ? _raidZoneComponents.FirstOrDefault(x => x != null && Vector3.Distance(position, x.transform.position) < config.RaidBlockMain.RaidBlockDistance) : null;
            if (raidableZone != null)
                raidableZone.RefreshTimer(position, player);
            else
            {
                raidableZone = new GameObject().AddComponent<RaidableZone>();
                _raidZoneComponents.Add(raidableZone);
                raidableZone.CreateRaidZone(position, player);
            }
            
            Interface.CallHook("OnRaidBlock", position);
        }
        

        [ChatCommand("rbtest")]
        void rbtest(BasePlayer player)
        {
            if(!player.IsAdmin) return;
            CreateOrRefreshRaidblock(player.transform.position, player);
        }
        
        private string GetGridString(Vector3 position)
        {
            int x = Mathf.FloorToInt((position.x + World.Size / 2) / 150);
            int z = Mathf.FloorToInt((position.z + World.Size / 2) / 150);
            return $"{(char)('A' + x)}{z + 1}";
        }

        private bool CheckEntity(BaseCombatEntity entity, HitInfo info, BasePlayer raider)
        {
            if (entity.OwnerID == 0)
                return false;
            if (config.BlockDetect.IgnoreEntsHealth > 0 && entity.MaxHealth() < config.BlockDetect.IgnoreEntsHealth)
                return false;
            if (entity is BuildingBlock { grade: BuildingGrade.Enum.Twigs } block) return false;
 
            if (raider != null && config.BlockDetect.RaidYourself == false)
            {
                if (raider.userID == entity.OwnerID)
                    return false;
                if (IsFriends(raider, entity.OwnerID))
                    return false;
            }
            
            return true;
        }
        
        private bool IsBlockedClass(BaseCombatEntity entity) => entity is BuildingBlock or Door or SimpleBuildingBlock or Workbench or Barricade;

        private bool IsWhiteList(BaseCombatEntity entity)
        {
            return config.BlockDetect.RbWhiteList.Any(whiteEnt => entity.prefabID == whiteEnt);
        }
        private bool IsBlackList(BaseCombatEntity entity)
        {
            return config.BlockDetect.RbBlackList.Any(blackEnt => entity.prefabID == blackEnt);
        }

        #endregion

        #region ToolGunAdminHelper
        
        private object OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (player == null || info is null) return null;

            if (player.GetActiveItem() is not { } activeItem) return null;
    
            if (!activeItem.info.shortname.Equals(RB_WHITE_AND_BLACK_LIST_ITEM) ||
                (activeItem.skin is not RB_WHITELIST_ITEM_SKIN and not RB_BLACKLIST_ITEM_SKIN))
                return null;

            if (!HasPermission(player)) return null;

            UpdateWhiteOrBlackList(player, activeItem.skin, info);

            return false;
        }

        private void UpdateWhiteOrBlackList(BasePlayer player, ulong skinId, HitInfo info)
        {
            BaseEntity entity = info.HitEntity;
            if (entity == null) return;

            uint prefabId = entity.prefabID;
            HashSet<uint> listToUpdate = skinId switch
            {
                RB_WHITELIST_ITEM_SKIN => config.BlockDetect.RbWhiteList,
                RB_BLACKLIST_ITEM_SKIN => config.BlockDetect.RbBlackList,
                _ => null
            };

            if (listToUpdate == null) return;

            string messageKey = UpdateList(listToUpdate, prefabId) 
                ? skinId == RB_WHITELIST_ITEM_SKIN ? "RAIDBLOCK_TOOLGUN_ADDED_TO_WHITELIST" : "RAIDBLOCK_TOOLGUN_ADDED_TO_BLACKLIST"
                : skinId == RB_WHITELIST_ITEM_SKIN ? "RAIDBLOCK_TOOLGUN_REMOVED_FROM_WHITELIST" : "RAIDBLOCK_TOOLGUN_REMOVED_FROM_BLACKLIST";

            GameTipsSendPlayer(player, messageKey.GetAdaptedMessage(player.UserIDString, entity.ShortPrefabName, prefabId));

            SaveConfig();
            return;

            bool UpdateList(HashSet<uint> list, uint id)
            {
                if (list.Contains(id))
                {
                    list.Remove(id);
                    return false;
                }

                list.Add(id);
                return true;
            }
        }

        
        [ChatCommand("rb.white")]
        private void RaidBlockDetectWhiteList(BasePlayer player)
        {
            if (!HasPermission(player))
                return;
            
            string itemName = "RAIDBLOCK_TOOLGUN_WHITELIST_ITEM_NAME".GetAdaptedMessage(player.UserIDString);
            
            if (FindItemInInventory(player, RB_WHITELIST_ITEM_SKIN))
            {
                GameTipsSendPlayer(player, "RAIDBLOCK_TOOLGUN_ALREADY_IN_INVENTORY".GetAdaptedMessage(player.UserIDString, itemName), error: true);
                return;
            }

            CreateToolGunItem(player, itemName, RB_WHITELIST_ITEM_SKIN);
            GameTipsSendPlayer(player, "RAIDBLOCK_TOOLGUN_WHITELIST_USAGE".GetAdaptedMessage(player.UserIDString, itemName));
        }

        [ChatCommand("rb.black")]
        private void RaidBlockDetectBlackList(BasePlayer player)
        {
            if (!HasPermission(player))
                return;
            
            string itemName = "RAIDBLOCK_TOOLGUN_BLACKLIST_ITEM_NAME".GetAdaptedMessage(player.UserIDString);

            if (FindItemInInventory(player, RB_BLACKLIST_ITEM_SKIN))
            {
                GameTipsSendPlayer(player, "RAIDBLOCK_TOOLGUN_ALREADY_IN_INVENTORY".GetAdaptedMessage(player.UserIDString, itemName), error: true);
                return;
            }

            CreateToolGunItem(player, itemName, RB_BLACKLIST_ITEM_SKIN);
            GameTipsSendPlayer(player, "RAIDBLOCK_TOOLGUN_BLACKLIST_USAGE".GetAdaptedMessage(player.UserIDString, itemName));
        }

        private bool HasPermission(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, Name + _permHelperToolGun) && !player.IsAdmin)
            {
                SendChat("RAIDBLOCK_TOOLGUN_PERMISSION_DENIED".GetAdaptedMessage(player.UserIDString), player);
                return false;
            }
            return true;
        }

        private bool FindItemInInventory(BasePlayer player, ulong skinId)
        {
            return player.inventory.containerMain.itemList.Any(item => item.skin == skinId) ||
                   player.inventory.containerBelt.itemList.Any(item => item.skin == skinId) ||
                   player.inventory.containerWear.itemList.Any(item => item.skin == skinId);
        }

        private void GameTipsSendPlayer(BasePlayer player, string message, float seconds = 10f, bool error = false)
        {
            player.SendConsoleCommand("gametip.hidegametip");
            string tips = error ? "showtoast 1" : "showgametip"; 
            player.SendConsoleCommand($"gametip.{tips} \"{message}\"");

            DeleteNotification(player, seconds);
        }
        private readonly Dictionary<BasePlayer, Timer> _playerTimer = new Dictionary<BasePlayer, Timer>();

        private void DeleteNotification(BasePlayer player, float seconds)
        {
            Timer timers = timer.Once(seconds, () =>
            {
                player.SendConsoleCommand("gametip.hidegametip");
            });

            if (_playerTimer.ContainsKey(player))
            {
                if (_playerTimer[player] != null && !_playerTimer[player].Destroyed) _playerTimer[player].Destroy();
                _playerTimer[player] = timers;
            }
            else _playerTimer.Add(player, timers);
        }

        private void CreateToolGunItem(BasePlayer player, string name, ulong skinId)
        {
            Item toolGun = ItemManager.CreateByName(RB_WHITE_AND_BLACK_LIST_ITEM, 1, skinId);
            toolGun.name = name;
            toolGun.info.stackable = 1;
            player.GiveItem(toolGun);
        }

        #endregion

        private List<RaidPlayer> raidPlayersList = new();
        private class RaidPlayer : FacepunchBehaviour
        {
            public BasePlayer player;
            public Single blockEnds;
            public float UnblockTimeLeft => Convert.ToInt32(blockEnds - Time.realtimeSinceStartup);
        
            #region UnityHooks
            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                
                if(!Instance.raidPlayersList.Contains(this))
                    Instance.raidPlayersList.Add(this);
                
                Interface.CallHook("OnRaidBlockStarted", player);
            }

            public void Kill(Boolean force = false)
            {
                if (!force)
                {
                    if (Instance.raidPlayersList.Contains(this))
                        Instance.raidPlayersList.Remove(this);
                }

                DestroyImmediate(this);
            }
            private void OnDestroy()
            {
                CuiHelper.DestroyUi(player, InterfaceBuilder.RB_MAIN);
                Interface.CallHook("OnRaidBlockStopped", player);
            }
        
            #endregion
        
            #region Metods
        
            public void UpdateTime(Single time, Boolean customTime = false)
            {
                if (customTime)
                {
                    blockEnds = time;
                    return;
                }
                
                if (time > blockEnds)
                    blockEnds = time;
            }
        
            public void ActivateBlock(Single time)
            {
                if(time > blockEnds)
                    blockEnds = time;
                CrateUI();
                InvokeRepeating(CheckTimeLeft, 0, 1);
            }
            private void CheckTimeLeft()
            {
                if (UnblockTimeLeft > 0)
                {
                    RefreshUI();
                }
                else
                {
                    CuiHelper.DestroyUi(player, InterfaceBuilder.RB_MAIN);
                    CancelInvoke(nameof(CheckTimeLeft));
                    Kill();
                }
            }
            #endregion
        
            #region UI Methods
            
            public void CrateUI() => Instance.DrawUI_RB_Main(player, UnblockTimeLeft);
            private void RefreshUI() => Instance.DrawUI_RB_Updated(player, UnblockTimeLeft);
        
            #endregion
        }
        
        
        private static RaidableZone GetRbZone(Vector3 position)
        {
            List<SphereCollider> sphereColliders = new ();
            Vis.Colliders(position, 0.1f, sphereColliders);
            if (sphereColliders.Count <= 0) return null;
            foreach (SphereCollider sCollider in sphereColliders)
            {
                if (!sCollider.gameObject.TryGetComponent(out RaidableZone rbZone)) continue;
                return rbZone;
            }
            return null;
        }
        private class RaidableZone : MonoBehaviour
        {
            #region Vars
            
            private Dictionary<BasePlayer, RaidPlayer> _playersAndComponentZone = new();
            
            private MapMarkerExplosion marker;
            private MapMarkerGenericRadius mapMarkerGenericRadius;
            private VendingMachineMapMarker vendingMakrer;
            private List<BaseEntity> _spheres = new();
            
            private SphereCollider triggerZone;
            private BasePlayer initiatorRaid;
            
            private Single timeToUnblock;
            private Single UnblockTimeLeft => Convert.ToInt32(timeToUnblock - Time.realtimeSinceStartup);
            private Int32 raidBlockDistance;
            private Int32 raidBlockDuration;
            private bool IsDynamicRaidZone;

            
            #endregion

            #region Metods

            #region VisualizationSphere

            private void CreateSphere()
            {
                Configuration.RaidBlock.SphereSettings settings = Instance.config.RaidBlockMain.RaidZoneSphereSettings;
                bool isBlackSphere = settings.SphereType == SphereTypes.BlackSphere;
    
                int domeTransparencyLevel = isBlackSphere ? settings.DomeTransparencyLevel : 1;
                string spherePrefab = isBlackSphere ? VISUALIZATION_SPHERE_PREFAB : settings.GetBRZonePrefab();

                for (int i = 0; i < domeTransparencyLevel; i++)
                {
                    SphereEntity sphere = GameManager.server.CreateEntity(spherePrefab, transform.position) as SphereEntity;
                    if (sphere != null)
                    {
                        sphere.currentRadius = raidBlockDistance * 2;
                        sphere.lerpSpeed = 0f;
                        sphere.enableSaving = false;
                        sphere.Spawn();
                        _spheres.Add(sphere);
                    }
                }
            }

            #endregion

            #region MapMarker

            private void CreateMapMarker()
            {
                MarkerTypes raidBlockType = Instance.config.RaidBlockMain.mapMarkerSettings.RaidBlockMarkerType;
                if (raidBlockType is MarkerTypes.MarkerRadiusTimer)
                {
                    CreateVendingMapMarker();
                }
                
                if (raidBlockType is MarkerTypes.ExplosionMarkerRadius or MarkerTypes.MarkerRadius or MarkerTypes.MarkerRadiusTimer)
                {
                    CreateGenericRadiusMapMarker();
                }
                
                if (raidBlockType is MarkerTypes.Explosion or MarkerTypes.ExplosionMarkerRadius)
                {
                    CreateExplosionMapMarker();
                }
                InvokeRepeating(nameof(UpdateGenericRadiusMapMarker), 10f, 10f);
            }

            private void CreateExplosionMapMarker()
            {
                marker = GameManager.server.CreateEntity(EXPLOSION_MAP_MARKER_PREFAB, transform.position) as MapMarkerExplosion;
                marker.enableSaving = false;
                marker.Spawn();
            }
            
            private void CreateVendingMapMarker()
            {
                vendingMakrer = GameManager.server.CreateEntity(VENDING_MAP_MARKER_PREFAB, transform.position) as VendingMachineMapMarker;
                vendingMakrer.enableSaving = false;
                vendingMakrer.markerShopName = "RAID BLOCK: " + UnblockTimeLeft.ToTimeFormat();
                vendingMakrer.Spawn();
            }

            private void CreateGenericRadiusMapMarker()
            {
                if (!ColorUtility.TryParseHtmlString($"#{Instance.config.RaidBlockMain.mapMarkerSettings.MarkerColorHex}", out Color color1) ||
                    !ColorUtility.TryParseHtmlString($"#{Instance.config.RaidBlockMain.mapMarkerSettings.OutlineColorHex}", out Color color2)) return;
                mapMarkerGenericRadius = GameManager.server.CreateEntity(GENERIC_MAP_MARKER_PREFAB) as MapMarkerGenericRadius;
                if (mapMarkerGenericRadius == null) return;
                mapMarkerGenericRadius.color1 = color1;
                mapMarkerGenericRadius.color2 = color2;
                mapMarkerGenericRadius.radius = (raidBlockDistance / 100f) * CalculateRadius();
                mapMarkerGenericRadius.alpha = Instance.config.RaidBlockMain.mapMarkerSettings.MarkerAlpha;
                mapMarkerGenericRadius.enableSaving = false;
                if (vendingMakrer != null)
                    mapMarkerGenericRadius.SetParent(vendingMakrer);
                else
                {
                    mapMarkerGenericRadius.transform.position = transform.position;
                }
                mapMarkerGenericRadius.Spawn();
                mapMarkerGenericRadius.SendUpdate();
                return;
                

                float CalculateRadius()
                {
                    const float a = 100 / 6f;
                    float b = Mathf.Sqrt(a) / 2f;
                    float c = World.Size / 1300f;
                    float d = b / c;

                    return d;
                }
               
            }

            public void UpdateGenericRadiusMapMarker()
            {
                if (vendingMakrer.IsValid())
                {
                    vendingMakrer.markerShopName = "RAID BLOCK: " + UnblockTimeLeft.ToTimeFormat();
                    vendingMakrer.SendNetworkUpdate();
                }
                if (marker.IsValid())
                    marker.SendNetworkUpdate();
                if (mapMarkerGenericRadius.IsValid())
                    mapMarkerGenericRadius.SendUpdate();
            }

            #endregion

            #region Core

            private void UpdateZonePosition(Vector3 position)
            {
                transform.position = position;
            
                if (marker.IsValid())
                {
                    marker.transform.position = position;
                }
                if (vendingMakrer.IsValid())
                {
                    vendingMakrer.markerShopName = "RAID BLOCK: " + UnblockTimeLeft.ToTimeFormat();
                    vendingMakrer.transform.position = position;
                }
                else if (mapMarkerGenericRadius.IsValid())
                {
                    mapMarkerGenericRadius.transform.position = position;
                }

                UpdateGenericRadiusMapMarker();

                foreach (BaseEntity sphere in _spheres)
                {
                    sphere.transform.position = position;
                    sphere.SendNetworkUpdate();
                }
            }
            
            private void InitializeTriggerZone()
            {
                triggerZone = gameObject.AddComponent<SphereCollider>();

                triggerZone.radius = raidBlockDistance;
                triggerZone.gameObject.layer = (int) Layer.Reserved1;
                triggerZone.transform.SetParent(transform, true);
                triggerZone.isTrigger = true;
            }

            public void CreateRaidZone(Vector3 raidPos, BasePlayer initiator)
            {
                Instance.CheckUnsubscribeOrSubscribeHooks();
          
                transform.position = raidPos;
                initiatorRaid = initiator;

                InitializeTriggerZone();
                AddPlayer(initiatorRaid, true);
                
                if (Instance.config.RaidBlockMain.mapMarkerSettings.IsRaidBlockMarkerEnabled)
                    CreateMapMarker();

                if (Instance.config.RaidBlockMain.RaidZoneSphereSettings.IsSphereEnabled)
                    CreateSphere();
                
                if (Instance.config.RaidBlockMain.RaidBlockAddedAllPlayersInZoneRaid) 
                    AddAllPlayerInZoneDistance();

                if (Instance.config.RaidBlockMain.RaidBlockShareOnFriends)
                    AddAllPlayerFriendsInitiator(initiator);
                
                Interface.CallHook("OnCreatedRaidZone", raidPos, initiator);
            }
            
            public void RefreshTimer(Vector3 pos, BasePlayer initiatorReply = null)
            {
                if (IsDynamicRaidZone)
                    UpdateZonePosition(pos);
                timeToUnblock = Time.realtimeSinceStartup + raidBlockDuration;
                CancelInvoke(nameof(EndRaid));
                Invoke(nameof(EndRaid), raidBlockDuration);

                if (initiatorReply != null && !_playersAndComponentZone.ContainsKey(initiatorReply))
                {
                    AddPlayer(initiatorReply);
                    
                    if (Instance.config.RaidBlockMain.RaidBlockShareOnFriends)
                        AddAllPlayerFriendsInitiator(initiatorReply);
                }
                
                foreach (KeyValuePair<BasePlayer, RaidPlayer> playerAndComponent in _playersAndComponentZone)
                {
                    RaidPlayer raidPlayer = playerAndComponent.Value;
                    if (raidPlayer != null)
                    {
                        if (Vector3.Distance(playerAndComponent.Key.transform.position,
                                triggerZone.transform.position) >= raidBlockDistance)
                        {
                            Instance.NextTick(() =>
                            {
                                _playersAndComponentZone.Remove(playerAndComponent.Key);
                            });
                            continue;
                        }
                        raidPlayer.UpdateTime(timeToUnblock);
                    }
                }
            }
            
            private void EndRaid() 
            {
                Interface.CallHook("OnRaidBlockStopped", transform.position);
                Instance._raidZoneComponents.Remove(this);
                Instance.CheckUnsubscribeOrSubscribeHooks();
                
                Destroy(this);
            }
            
            #endregion

            #region Player Action

            private void AddAllPlayerInZoneDistance() 
            {
                List<BasePlayer> players = Pool.GetList<BasePlayer>();
                Vis.Entities(transform.position, raidBlockDistance, players);
                foreach (BasePlayer player in players)
                {
                    if(_playersAndComponentZone.ContainsKey(player))
                        continue;
                    AddPlayer(player, true);
                }
                
                Pool.FreeList(ref players);
            }
            
            private void AddAllPlayerFriendsInitiator(BasePlayer initiator) 
            {
                foreach (UInt64 playerID in Instance.GetFriendList(initiator))
                {
                    BasePlayer player = BasePlayer.FindByID(playerID);
                    if (player == null) continue;
                    if(_playersAndComponentZone.ContainsKey(player))
                        continue;
                    
                    AddPlayer(player, true);
                }
            }
            
            public void AddPlayer(BasePlayer player, Boolean force = false)
            {
                if (!Instance.config.RaidBlockMain.RaidBlockOnEnterRaidZone && !force) return;
                RaidPlayer raidPlayer = player.GetOrAddComponent<RaidPlayer>();
                
                Single leftTimeZone = raidPlayer.UnblockTimeLeft > UnblockTimeLeft
                    ? raidPlayer.UnblockTimeLeft
                    : UnblockTimeLeft;
                
                if (player == initiatorRaid)
                {
                    Instance.SendChat("RAIDBLOCK_ENTER_RAID_INITIATOR".GetAdaptedMessage(player.UserIDString, leftTimeZone.ToTimeFormat()), player);
                    initiatorRaid = null;
                }
                else if(!_playersAndComponentZone.ContainsKey(player))
                    Instance.SendChat("RAIDBLOCK_ENTER_RAID_ZONE".GetAdaptedMessage(player.UserIDString, leftTimeZone.ToTimeFormat()), player, 3f);
            
                _playersAndComponentZone[player] = raidPlayer;
                
                raidPlayer.ActivateBlock(timeToUnblock);

                Interface.CallHook("OnEnterRaidZone", player);
            }

            public void RemovePlayer(BasePlayer player)
            {
                if(!_playersAndComponentZone.ContainsKey(player)) return;
                RaidPlayer raidPlayer = _playersAndComponentZone[player];
                if (raidPlayer == null) return;
                
                if (Instance.config.RaidBlockMain.RaidBlockOnExitRaidZone && Instance.config.RaidBlockMain.TimeLeftOnExitZone == 0)
                {
                    Destroy(_playersAndComponentZone[player]);
                    Instance.SendChat("RAIDBLOCK_EXIT_RAID_ZONE".GetAdaptedMessage(player.UserIDString), player);
                }
                else if(Instance.config.RaidBlockMain.TimeLeftOnExitZone != 0)
                {
                    if (raidPlayer.UnblockTimeLeft > Instance.config.RaidBlockMain.TimeLeftOnExitZone)
                        raidPlayer.UpdateTime(Time.realtimeSinceStartup + Instance.config.RaidBlockMain.TimeLeftOnExitZone, true);
                }
                _playersAndComponentZone.Remove(player);
                
                player.Invoke(() => RecheackRbZone(player), 0.1f);

                Interface.CallHook("OnExitRaidZone", player);
            }
            
            public void RecheackRbZone(BasePlayer player)
            {
                if (player == null || player.IsDead() || !player.IsConnected) return;
                
                RaidableZone rbZone = GetRbZone(player.transform.position);
                if (rbZone == null) return;
                rbZone.AddPlayer(player);
            }
            
            #endregion
            
            #endregion
            
            #region Init

            private void Awake()
            {
                raidBlockDistance = Instance.config.RaidBlockMain.RaidBlockDistance;
                raidBlockDuration = Instance.config.RaidBlockMain.RaidBlockDuration;
                IsDynamicRaidZone = Instance.config.RaidBlockMain.IsDynamicRaidZone;

                RefreshTimer(transform.position);
            }
            
            #endregion

            #region Hooks

            private void OnDestroy()
            {
                CancelInvoke(nameof(UpdateGenericRadiusMapMarker));
                if (marker.IsValid())
                    marker.Kill();
                
                if (vendingMakrer.IsValid())
                    vendingMakrer.Kill();

                if (mapMarkerGenericRadius.IsValid())
                    mapMarkerGenericRadius.Kill();
                
                foreach (BaseEntity sphere in _spheres)
                    if (sphere.IsValid())
                        sphere.Kill();
                
                marker = null;
                mapMarkerGenericRadius = null;
                _spheres = null;
                
                Destroy(triggerZone);
            }
        
            private void OnTriggerEnter(Collider collider)
            {
                BasePlayer player = collider.GetComponentInParent<BasePlayer>();
                if (player != null && player.net?.connection!=null)
                {
                    AddPlayer(player);
                }
            }
        
            private void OnTriggerExit(Collider collider)
            {
                BasePlayer player = collider.GetComponentInParent<BasePlayer>();
                if (player != null && player.net?.connection!=null)
                {
                    RemovePlayer(player);
                }
            }
            
            #endregion
        }
        
        #region Refference

        #region Friends
        
        private List<BasePlayer> GetPlayerFriends(BasePlayer player)
        {
            List<BasePlayer> teamMembers = new List<BasePlayer>();
            if (RelationshipManager.maxTeamSize > 0 && player.currentTeam != 0UL)
            {
                foreach (ulong member in player.Team.members)
                {
                    BasePlayer playerInTeam = BasePlayer.FindByID(member);
                    if(playerInTeam != null && playerInTeam != player)
                        teamMembers.Add(playerInTeam);
                }
            }
            else if (Friends)
            {
                ulong[] playersInTeam = Friends?.Call<ulong[]>("GetFriends", player.userID) ?? new ulong[]{};
                foreach (ulong member in playersInTeam)
                {
                    BasePlayer playerInTeam = BasePlayer.FindByID(member);
                    if(playerInTeam != null)
                        teamMembers.Add(playerInTeam);
                }
            }

            return teamMembers;
        }


        #endregion

        #endregion

        #region UI
        
        private void DrawUI_RB_Main(BasePlayer player,  float timeLeft = 0f)
        {
            string Interface = InterfaceBuilder.GetInterface(InterfaceBuilder.RB_MAIN);
            if (Interface == null) return;

            switch (InterfaceBuilder.TypeUi)
            {
                case 0:
                    Interface = Interface.Replace("%Descriptions%", "RAIDBLOCK_UI_DESCRIPTIONS_V1".GetAdaptedMessage(player.UserIDString));
                    break;
                case 1:
                    Interface = Interface.Replace("%Descriptions%", "RAIDBLOCK_UI_DESCRIPTIONS_V2".GetAdaptedMessage(player.UserIDString));
                    break;
            }

            Interface = Interface.Replace("%Title%", "RAIDBLOCK_UI_TITLE".GetAdaptedMessage(player.UserIDString));


            CuiHelper.DestroyUi(player, InterfaceBuilder.RB_MAIN);
            CuiHelper.AddUi(player, Interface);
            
            DrawUI_RB_Updated(player, timeLeft == 0 ? config.RaidBlockMain.RaidBlockDuration : timeLeft, true);
        }	
        
        private void DrawUI_RB_Updated(BasePlayer player, float timeLeft, bool upd = false)
        {
            string Interface = InterfaceBuilder.GetInterface(InterfaceBuilder.RB_PROGRESS_BAR);
            if (Interface == null) return;
            double factor = InterfaceBuilder.Factor * timeLeft / config.RaidBlockMain.RaidBlockDuration;

            Interface = Interface.Replace("%left%", factor.ToString(CultureInfo.InvariantCulture));
            Interface = Interface.Replace("%TimeLeft%", "RAIDBLOCK_UI_TIMER".GetAdaptedMessage(player.UserIDString, timeLeft.ToTimeFormat()));
            if(!upd)
                Interface = Interface.Replace("0.222", "0");

            CuiHelper.DestroyUi(player, InterfaceBuilder.RB_PROGRESS);
            CuiHelper.DestroyUi(player, InterfaceBuilder.RB_PROGRESS_TIMER);
            CuiHelper.AddUi(player, Interface);
        }
        

        private class InterfaceBuilder
		{
			#region Vars

			private static InterfaceBuilder _instance;
            public const string RB_MAIN = "RB_MAIN";
            public const string RB_PROGRESS_BAR = "RB_PROGRESS_BAR";
            public const string RB_PROGRESS = "RB_PROGRESS";
            public const string RB_PROGRESS_TIMER = "RB_PROGRESS_TIMER";

            public static int TypeUi;
            public static double Factor;
            private Configuration.RaidBlockUi.RaidBlockUiSettings _uiSettings;
            private static float _fade;

			private Dictionary<string, string> _interfaces;

			#endregion

			#region Main
			public InterfaceBuilder()
			{
				_instance = this;
				_interfaces = new Dictionary<string, string>();
                TypeUi = Instance.config.RaidBlockInterface.UiType;
                Factor = TypeUi switch
                {
                    0 => 142,
                    1 => 195,
                    _ => 130
                };
                _uiSettings = TypeUi switch
                {
                    0 => Instance.config.RaidBlockInterface.InterfaceSettingsVariant0,
                    1 => Instance.config.RaidBlockInterface.InterfaceSettingsVariant1,
                    2 => Instance.config.RaidBlockInterface.InterfaceSettingsVariant2,
                    _ => throw new ArgumentOutOfRangeException()
                };
                _fade = _uiSettings.SmoothTransition;
              

                switch (TypeUi)
                {
                    case 0:
                        BuildingRaidBlockMain();
                        BuildingRaidBlockUpdated();
                        break;
                    case 1:
                        BuildingRaidBlockMainV2();
                        BuildingRaidBlockUpdatedV2();
                        break;
                    case 2:
                        BuildingRaidBlockMainV3();
                        BuildingRaidBlockUpdatedV3();
                        break;
                }
            }

			private static void AddInterface(string name, string json)
			{
				if (_instance._interfaces.ContainsKey(name))
				{
					Instance.PrintError($"Error! Tried to add existing cui elements! -> {name}");
					return;
				}

				_instance._interfaces.Add(name, json);
			}

			public static string GetInterface(string name)
			{
				string json;
				if (_instance._interfaces.TryGetValue(name, out json) == false)
				{
					Instance.PrintWarning($"Warning! UI elements not found by name! -> {name}");
				}

				return json;
			}

			public static void DestroyAll()
			{
				foreach (BasePlayer player in BasePlayer.activePlayerList)
					CuiHelper.DestroyUi(player, RB_MAIN);
			}

			#endregion

			#region Building UI V-1

			private void BuildingRaidBlockMain()
            {

				CuiElementContainer container = new CuiElementContainer();
                
                container.Add(new CuiPanel
                {
                    FadeOut = _fade,
                    CursorEnabled = false,
                    Image = { Color = "0 0 0 0", FadeIn = _fade },
                    RectTransform ={ AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = $"{-179.006 + Instance.config.RaidBlockInterface.OffsetX} {-34.5 + Instance.config.RaidBlockInterface.OffsetY}", OffsetMax = $"{-0.006 + Instance.config.RaidBlockInterface.OffsetX} {34.5 + Instance.config.RaidBlockInterface.OffsetY}" }
                },Instance.config.RaidBlockInterface.Layers ,RB_MAIN);
                
                container.Add(new CuiElement
                {
                    FadeOut = _fade,
                    Name = "RB_BACKGROUND",
                    Parent = RB_MAIN,
                    Components = {
                        new CuiRawImageComponent { Color = _uiSettings.BackgroundColor, Png = _imageUI.GetImage("RB_FON0"), FadeIn = _fade },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1"}
                    }
                });
                
                container.Add(new CuiPanel
                {
                    FadeOut = _fade,
                    CursorEnabled = false,
                    Image = { Color = _uiSettings.ProgressBarBackgroundColor, FadeIn = _fade },
                    RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-71.001 9.367", OffsetMax = "70.999 12.033" }
                },RB_MAIN,RB_PROGRESS_BAR);
                
                container.Add(new CuiLabel
                {
                    FadeOut = _fade,
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-71.001 -9.637", OffsetMax = "84.101 14.717" },
                    Text = { Text = "%Descriptions%", Font = "robotocondensed-regular.ttf", FontSize = 8, Align = TextAnchor.MiddleLeft, Color = _uiSettings.SecondaryTextColor, FadeIn = _fade }
                }, RB_MAIN);
                
                container.Add(new CuiLabel
                {
                    FadeOut = _fade,
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-71.001 14.717", OffsetMax = "-9.043 29.111"},
                    Text = { Text = "%Title%", Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = _uiSettings.MainTextColor , FadeIn = _fade}
                }, RB_MAIN);
                
                container.Add(new CuiElement
                {
                    Name = "RB_ICON",
                    Parent = RB_MAIN,
                    Components = {
                        new CuiRawImageComponent { Color = _uiSettings.IconColor, Png = _imageUI.GetImage("RB_VARIANT0_ICON") },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-7.38 16.914", OffsetMax = "2.62 26.914" }
                    }
                });

                AddInterface(RB_MAIN, container.ToJson());
			}
            
            private void BuildingRaidBlockUpdated()
            {
                CuiElementContainer container = new CuiElementContainer();
                container.Add(new CuiPanel
                {
                    FadeOut = _fade,
                    CursorEnabled = false,
                    Image = { Color = _uiSettings.ProgressBarMainColor, FadeIn = _fade },
                    RectTransform ={ AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "0 -1.333", OffsetMax = "%left% 1.333" }
                },RB_PROGRESS_BAR,RB_PROGRESS);

                container.Add(new CuiLabel
                {
                    FadeOut = _fade,
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-71.001 -21.162", OffsetMax = "35.499 -9.638"},
                    Text = {  Text = "%TimeLeft%", Font = "robotocondensed-regular.ttf", FontSize = 9, Align = TextAnchor.MiddleLeft, Color = _uiSettings.MainTextColor }
                }, RB_MAIN, RB_PROGRESS_TIMER);

                AddInterface(RB_PROGRESS_BAR, container.ToJson());
            }
            #endregion
            
            #region Building UI V-2

			private void BuildingRaidBlockMainV2()
            {
				CuiElementContainer container = new CuiElementContainer();
                container.Add(new CuiPanel
                {
                    FadeOut = _fade,
                    CursorEnabled = false,
                    Image = { Color = "1 1 1 0", FadeIn = _fade },
                    RectTransform ={ AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = $"{-200.01 + Instance.config.RaidBlockInterface.OffsetX} {-20 + Instance.config.RaidBlockInterface.OffsetY}", OffsetMax = $"{-0.01 + Instance.config.RaidBlockInterface.OffsetX} {20 + Instance.config.RaidBlockInterface.OffsetY}" }

                },Instance.config.RaidBlockInterface.Layers,RB_MAIN);
                
                container.Add(new CuiElement
                {
                    FadeOut = _fade,
                    Name = "RB_BACKGROUND",
                    Parent = RB_MAIN,
                    Components = {
                        new CuiRawImageComponent { Color = _uiSettings.BackgroundColor , Png = _imageUI.GetImage("RB_FON1"), FadeIn = _fade },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-100.001 -17", OffsetMax = "99.999 20" }
                    }
                });
                
                container.Add(new CuiPanel
                {
                    FadeOut = _fade,
                    CursorEnabled = false,
                    Image = { Color = _uiSettings.ProgressBarBackgroundColor, FadeIn = _fade },
                    RectTransform ={ AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-97.5 0", OffsetMax = "97.5 3.33" }
                },RB_MAIN,RB_PROGRESS_BAR);

                container.Add(new CuiLabel
                {
                    FadeOut = _fade,
                    RectTransform = {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-64.622 0", OffsetMax = "-13.368 15.562" },
                    Text = {  Text = "%Title%", Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = _uiSettings.MainTextColor, FadeIn = _fade }
                }, RB_MAIN);
                
                container.Add(new CuiLabel
                {
                    FadeOut = _fade,
                    RectTransform = {  AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-64.622 -12.76", OffsetMax = "68.698 2.76" },
                    Text = { Text = "%Descriptions%", Font = "robotocondensed-regular.ttf", FontSize = 8, Align = TextAnchor.MiddleLeft, Color = _uiSettings.SecondaryTextColor, FadeIn = _fade}
                }, RB_MAIN);
                
                container.Add(new CuiElement
                {
                    Name = "RB_ICON_FON",
                    Parent = RB_MAIN,
                    Components = {
                        new CuiRawImageComponent { Color = _uiSettings.AdditionalElementsColor, Png = _imageUI.GetImage("RB_VARIANT1_ICON_FON") },
                        new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "8.199 -10", OffsetMax = "32.199 13" }
                    }
                });

                container.Add(new CuiElement
                {
                    Name = "RB_ICON",
                    Parent = "RB_ICON_FON",
                    Components = {
                        new CuiRawImageComponent { Color = _uiSettings.IconColor, Png = _imageUI.GetImage("RB_VARIANT1_ICON") },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-9 -8.5", OffsetMax = "9 8.5" }
                    }
                });
                
                AddInterface(RB_MAIN, container.ToJson());
			}
            
            private void BuildingRaidBlockUpdatedV2()
            {
                CuiElementContainer container = new CuiElementContainer();
                container.Add(new CuiPanel
                {
                    FadeOut = _fade,
                    CursorEnabled = false,
                    Image = { Color = _uiSettings.ProgressBarMainColor, FadeIn = _fade },
                    RectTransform ={ AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "0 -1.665", OffsetMax = "%left% 1.665" }
                },RB_PROGRESS_BAR,RB_PROGRESS);
                AddInterface(RB_PROGRESS_BAR, container.ToJson());
            }
            #endregion
            
            #region Building UI V-3

			private void BuildingRaidBlockMainV3()
            {
				CuiElementContainer container = new CuiElementContainer();
                
                container.Add(new CuiPanel
                {
                    FadeOut = _fade,
                    CursorEnabled = false,
                    Image = { Color = "1 1 1 0", FadeIn = _fade },
                    RectTransform ={ AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = $"{-200 + Instance.config.RaidBlockInterface.OffsetX} {-28 + Instance.config.RaidBlockInterface.OffsetY}", OffsetMax = $"{0 + Instance.config.RaidBlockInterface.OffsetX} {28 + Instance.config.RaidBlockInterface.OffsetY}" }
                },Instance.config.RaidBlockInterface.Layers,RB_MAIN);
                
                container.Add(new CuiElement
                {
                    FadeOut = _fade,
                    Name = "RB_BACKGROUND",
                    Parent = RB_MAIN,
                    Components = {
                        new CuiRawImageComponent { Color = _uiSettings.BackgroundColor, Png = _imageUI.GetImage("RB_FON2"), FadeIn = _fade },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1"}
                    }
                });
                
                container.Add(new CuiPanel
                {
                    FadeOut = _fade,
                    CursorEnabled = false,
                    Image = { Color = _uiSettings.ProgressBarBackgroundColor, FadeIn = _fade },
                    RectTransform ={ AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-46.03 9.425", OffsetMax = "83.97 12.755" }
                },RB_MAIN,RB_PROGRESS_BAR);
                
                container.Add(new CuiLabel
                {
                    FadeOut = _fade,
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-2.954 1.549", OffsetMax = "83.97 21.851" },
                    Text = { Text = "%Title%", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleRight, Color = _uiSettings.MainTextColor, FadeIn = _fade}
                }, RB_MAIN);
                
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = _uiSettings.AdditionalElementsColor },
                    RectTransform ={ AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-7.9 -25", OffsetMax = "-1.9 26" }
                },RB_MAIN,"RB_RIGHT_PANEL");
                
                container.Add(new CuiElement
                {
                    Name = "RB_ICON",
                    Parent = RB_MAIN,
                    Components = {
                        new CuiRawImageComponent { Color = _uiSettings.IconColor, Png = _imageUI.GetImage("RB_VARIANT2_ICON") },
                        new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "2.3 -20", OffsetMax = "42.3 20" }
                    }
                });
                
                AddInterface(RB_MAIN, container.ToJson());
			}
            
            private void BuildingRaidBlockUpdatedV3()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = _uiSettings.ProgressBarMainColor, FadeIn = _fade },
                    RectTransform ={ AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "0 -1.665", OffsetMax = "%left% 1.665" }
                },RB_PROGRESS_BAR,RB_PROGRESS);
                
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-46.03 -12.245", OffsetMax = "83.97 5.465" },
                    Text = { Text = "%TimeLeft%", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleRight, Color = _uiSettings.SecondaryTextColor, FadeIn = _fade}
                }, RB_MAIN, RB_PROGRESS_TIMER);

                AddInterface(RB_PROGRESS_BAR, container.ToJson());
            }
            #endregion
		}

        #endregion
        
        #region ImageLoader

		private class ImageUI
		{
			private readonly string _paths;
			private readonly string _printPath;
			private readonly Dictionary<string, ImageData> _images;

			private enum ImageStatus
			{
				NotLoaded,
				Loaded,
				Failed
			}

			public ImageUI()
			{
				_paths = Instance.Name + "/Images/";
				_printPath = "data/" + _paths;
				_images = new Dictionary<string, ImageData>
				{
					{ "RB_FON0", new ImageData() },
					{ "RB_FON1", new ImageData() },
					{ "RB_FON2", new ImageData() },
					{ "RB_VARIANT0_ICON", new ImageData() },
					{ "RB_VARIANT1_ICON_FON", new ImageData() },
					{ "RB_VARIANT1_ICON", new ImageData() },
					{ "RB_VARIANT2_ICON", new ImageData() },
				};
			}

			private class ImageData
			{
				public ImageStatus Status = ImageStatus.NotLoaded;
				public string Id { get; set; }
			}

			public string GetImage(string name)
			{
				ImageData image;
				if (_images.TryGetValue(name, out image) && image.Status == ImageStatus.Loaded)
					return image.Id;
				return null;
			}

			public void DownloadImage()
			{
				KeyValuePair<string, ImageData>? image = null;
				foreach (KeyValuePair<string, ImageData> img in _images)
				{
					if (img.Value.Status == ImageStatus.NotLoaded)
					{
						image = img;
						break;
					}
				}

				if (image != null)
				{
					ServerMgr.Instance.StartCoroutine(ProcessDownloadImage(image.Value));
				}
				else
				{
					List<string> failedImages = new List<string>();

					foreach (KeyValuePair<string, ImageData> img in _images)
					{
						if (img.Value.Status == ImageStatus.Failed)
						{
							failedImages.Add(img.Key);
						}
					}

					if (failedImages.Count > 0)
					{
						string images = string.Join(", ", failedImages);
						Instance.PrintError(LanguageEn
							? $"Failed to load the following images: {images}. Perhaps you did not upload them to the '{_printPath}' folder."
							: $"Не удалось загрузить следующие изображения: {images}. Возможно, вы не загрузили их в папку '{_printPath}'.");
						Interface.Oxide.UnloadPlugin(Instance.Name);
					}
					else
					{
						Instance.Puts(LanguageEn
							? $"{_images.Count} images downloaded successfully!"
							: $"{_images.Count} изображений успешно загружено!");
                        
                        Instance.Puts(LanguageEn ? "Generating the interface, please wait for approximately 5-10 seconds" : "Генерируем интерфейс, ожидайте ~5-10 секунд");
                        _interface = new InterfaceBuilder();
                        Instance.Puts(LanguageEn ? "The interface has been successfully loaded" : "Интерфейс успешно загружен!");
					}
				}
			}

			public void UnloadImages()
			{
				foreach (KeyValuePair<string, ImageData> item in _images)
					if (item.Value.Status == ImageStatus.Loaded)
						if (item.Value?.Id != null)
							FileStorage.server.Remove(uint.Parse(item.Value.Id), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);

				_images?.Clear();
			}

            private IEnumerator ProcessDownloadImage(KeyValuePair<string, ImageData> image)
			{
                string url = "file://" + Interface.Oxide.DataDirectory + "/" + _paths + image.Key + ".png";

                using UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);
                yield return www.SendWebRequest();

                if (www.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError)
                {
                    image.Value.Status = ImageStatus.Failed;
                }
                else
                {
                    Texture2D tex = DownloadHandlerTexture.GetContent(www);
                    image.Value.Id = FileStorage.server.Store(tex.EncodeToPNG(), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
                    image.Value.Status = ImageStatus.Loaded;
                    UnityEngine.Object.DestroyImmediate(tex);
                }

                DownloadImage();
            }
		}

		#endregion
    }
}

namespace Oxide.Plugins.RaidBlockExt
{
    public static class ExtensionMethods
    {
        private static readonly Lang Lang = Interface.Oxide.GetLibrary<Lang>();

        #region GetLang

        public static string GetAdaptedMessage(this string langKey, string userID = null, params object[] args)
        {
            string message = Lang.GetMessage(langKey, RaidBlock.Instance, userID);

            if (args == null || args.Length == 0)
            {
                return message;
            }
            
            return new StringBuilder().AppendFormat(message, args).ToString();
        }

        #endregion
        
        #region TimeFormat

        public static string ToTimeFormat(this float source)
        {
            TimeSpan ts = TimeSpan.FromSeconds(source);
            return $"{ts.Minutes:D1}:{ts.Seconds:D2}";
        }

        #endregion
        
    }
}