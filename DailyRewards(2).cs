// #define TESTING

#define EDITING_MODE

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Epic.OnlineServices.UserInfo;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Plugins.DailyRewardsExtensionMethods;
using Rust;
using UnityEngine;
using Color = UnityEngine.Color;
using Random = UnityEngine.Random;

#if CARBON
using Carbon.Base;
using Carbon.Modules;
#endif

namespace Oxide.Plugins
{
	[Info("Daily Rewards")]
	public class DailyRewards : RustPlugin
	{
		#region Fields

		[PluginReference] private Plugin
			AFKAPI = null,
			ImageLibrary = null,
			WipeBlock = null,
			NoEscape = null,
			Notify = null,
			UINotify = null,
			LangAPI = null;

		private static DailyRewards _instance;

#if CARBON
        private ImageDatabaseModule imageDatabase;
#endif

		private bool _enabledImageLibrary;

		private const bool LangRu = false;

		private const string
			Layer = "UI.DailyRewards",
			MainLayer = "UI.DailyRewards.Main",
			ModalLayer = "UI.DailyRewards.Modal",
			ModalMainLayer = "UI.DailyRewards.Modal.Main",
			SecondModalLayer = "UI.DailyRewards.Second.Modal",
			SecondModalMainLayer = "UI.DailyRewards.Second.Modal.Main",
			SelectItemModalLayer = "UI.DailyRewards.Select.Item.Modal",
			CMD_Main_Console = "UI_DailyRewards",
			PERM_EDIT = "dailyrewards.edit";

		private TimeZoneInfo _pluginTimeZone;

		private Dictionary<int, AwardInfo> _awardsByID = new();

		#endregion

		#region Config

		private static Configuration _config;

		private class Configuration
		{
			[JsonProperty(PropertyName = LangRu ? "Команды" : "Commands",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] Commands = {"rewards", "daily"};

			[JsonProperty(PropertyName = LangRu ? "Включить работу с Notify?" : "Work with Notify?")]
			public bool UseNotify = true;

			[JsonProperty(PropertyName = LangRu ? "Включить работу с LangAPI?" : "Work with LangAPI?")]
			public bool UseLangAPI = true;

			[JsonProperty(PropertyName = LangRu ? "Вайпать при новом сохранении карты" : "Wipe on new map save")]
			public bool WipeOnNewSave = false;

			[JsonProperty(PropertyName = LangRu ? "Настройка задержки" : "Cooldown Settings")]
			public CooldownSettings Cooldown = new()
			{
				Enabled = true,
				DefaultCooldown = 3600,
				Cooldowns = new Dictionary<string, float>
				{
					["dailyrewards.vip"] = 3100,
					["dailyrewards.premium"] = 2600
				},
				CheckAFK = false
			};

			[JsonProperty(PropertyName = LangRu ? "Ежедневные награды" : "Daily awards",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public SortedDictionary<int, AwardDayInfo> DailyAwards = new()
			{
				[1] = new AwardDayInfo
				{
					Enabled = true,
					Image = "https://i.ibb.co/7Gs1yFn/image.png",
					Title = "GUNS",
					Awards = new List<AwardInfo>
					{
						AwardInfo.CreateItem("pistol.revolver"),
						AwardInfo.CreateItem("ammo.pistol", 64)
					},
					IsSpecialDay = false
				},
				[2] = new AwardDayInfo
				{
					Enabled = true,
					Image = "https://i.ibb.co/RhL03Qn/image.png",
					Title = "GUNS",
					Awards = new List<AwardInfo>
					{
						AwardInfo.CreateItem("pistol.m92"),
						AwardInfo.CreateItem("ammo.pistol", 128)
					},
					IsSpecialDay = false
				},
				[3] = new AwardDayInfo
				{
					Enabled = true,
					Image = "https://i.ibb.co/1ZFQKS7/image.png",
					Title = "GUNS",
					Awards = new List<AwardInfo>
					{
						AwardInfo.CreateItem("rifle.semiauto"),
						AwardInfo.CreateItem("ammo.rifle", 64)
					},
					IsSpecialDay = false
				},
				[4] = new AwardDayInfo
				{
					Enabled = true,
					Image = "https://i.ibb.co/5BNXFPZ/image.png",
					Title = "GUNS",
					Awards = new List<AwardInfo>
					{
						AwardInfo.CreateItem("rifle.m39"),
						AwardInfo.CreateItem("ammo.rifle", 128)
					},
					IsSpecialDay = false
				},
				[5] = new AwardDayInfo
				{
					Enabled = true,
					Image = "https://i.ibb.co/Cbz55sP/image.png",
					Title = "GUNS",
					Awards = new List<AwardInfo>
					{
						AwardInfo.CreateItem("rifle.ak"),
						AwardInfo.CreateItem("ammo.rifle", 128),
						AwardInfo.CreateItem("ammo.rifle", 128)
					},
					IsSpecialDay = false
				},
				[6] = new AwardDayInfo
				{
					Enabled = true,
					Image = "https://i.ibb.co/NZnjSFz/kuyura.png",
					Title = "CASH",
					Awards = new List<AwardInfo>
					{
						AwardInfo.CreateCommand("deposit %steamid% 100", 1, "https://i.ibb.co/NZnjSFz/kuyura.png")
					},
					IsSpecialDay = false
				},
				[7] = new AwardDayInfo
				{
					Enabled = true,
					Image = "https://i.ibb.co/NZnjSFz/kuyura.png",
					Title = "CASH",
					Awards = new List<AwardInfo>
					{
						AwardInfo.CreateCommand("deposit %steamid% 1000", 1, "https://i.ibb.co/NZnjSFz/kuyura.png")
					},
					IsSpecialDay = true
				}
			};

			[JsonProperty(PropertyName = LangRu ? "Настройки инвентаря" : "Inventory settings")]
			public InventorySettings Inventory = new()
			{
				Enabled = true,
				RetainItemsOnWipe = true,
				UseCooldownAfterWipe = false,
				CooldownAfterWipe = 0,
				UseBuildingBlocked = false,
				UseCombatBlocked = false,
				UseRaidBlocked = false
			};

			[JsonProperty(PropertyName =
				LangRu ? "Разрешение (прим: dailyrewards.use)" : "Permission (ex: dailyrewards.use)")]
			public string Permission = "dailyrewards.use";

			[JsonProperty(PropertyName = LangRu ? "Настройки сброса дня" : "Daily reset settings")]
			public ResetSettings Reset = new()
			{
				TimeZone = "Europe/London",
				Time = "05:00"
			};

			[JsonProperty(PropertyName = LangRu ? "Настройки оповещений" : "Notification settings")]
			public NotificationsSettings Notifications = new()
			{
				OnCooldownEnded = new NotificationsSettings.CooldownEnded
				{
					Type = 2130354,
					ShowOnPlayerConnected = false,
					ShowOnCooldownEnded = false
				},
				HasCooldown = new NotificationsSettings.CooldownHas
				{
					Type = 2130355,
					ShowOnPlayerConnected = false
				},
				PickedUpDailyReward = new NotificationsSettings.TakedDay
				{
					Type = 2130356,
					ShowOnPlayerPickedUpReward = true
				}
			};

			[JsonProperty(PropertyName =
				LangRu
					? "Показывать интерфейс с наградами когда игрок подключился к серверу?"
					: "Show UI with rewards when player connects to server?")]
			public bool ShowUIWhenPlayerConnected = false;

			[JsonProperty(PropertyName = LangRu ? "Настройки UI" : "UI settings")]
			public InterfaceSettings UI = InterfaceSettings.GenerateFullscreenTemplate();
			
			[JsonProperty(PropertyName = LangRu ? "Настройки UI в меню" : "Menu UI settings")]
			public InterfaceSettings MenuUI = InterfaceSettings.GenerateMenuTemplateV2();
			
			public VersionNumber Version;

			#region Helpers

			public AwardDayInfo GetDailyAwards(int day)
			{
				return DailyAwards.GetValueOrDefault(day);
			}

			#endregion
		}

		private abstract class Switchable
		{
			[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
			public bool Enabled;
		}

		private class NotificationsSettings
		{
			[JsonProperty(PropertyName = LangRu ? "При окончании задержки" : "On Cooldown Ended")]
			public CooldownEnded OnCooldownEnded;

			[JsonProperty(PropertyName = LangRu ? "При наличии задержки" : "Has Cooldown")]
			public CooldownHas HasCooldown;

			[JsonProperty(PropertyName = LangRu ? "При получении ежедневной награды" : "Picked up daily reward")]
			public TakedDay PickedUpDailyReward;

			public class TakedDay
			{
				[JsonProperty(PropertyName = LangRu ? "Тип" : "Type")]
				public int Type;

				[JsonProperty(PropertyName =
					LangRu
						? "Показывать когда игрок забрал ежедневную награду?"
						: "Show when the player has picked up the daily reward?")]
				public bool ShowOnPlayerPickedUpReward;

				public void Show(BasePlayer player)
				{
					_instance.SendNotify(player, NotifyTakedAward, Type);
				}
			}

			public class CooldownEnded
			{
				[JsonProperty(PropertyName = LangRu ? "Тип" : "Type")]
				public int Type;

				[JsonProperty(PropertyName =
					LangRu
						? "Показывать при подключении игрока к серверу?"
						: "Show when a player has connected to the server?")]
				public bool ShowOnPlayerConnected;

				[JsonProperty(PropertyName =
					LangRu ? "Показывать при окончании задержки?" : "Show when the cooldown is over?")]
				public bool ShowOnCooldownEnded;

				public void Show(BasePlayer player)
				{
					_instance.SendNotify(player, NotifyCooldownEnded, Type);
				}
			}

			public class CooldownHas
			{
				[JsonProperty(PropertyName = LangRu ? "Тип" : "Type")]
				public int Type;

				[JsonProperty(PropertyName =
					LangRu
						? "Показывать при подключении игрока к серверу?"
						: "Show when a player has connected to the server?")]
				public bool ShowOnPlayerConnected;

				public void Show(BasePlayer player, string leftTime)
				{
					_instance.SendNotify(player, NotifyHasCooldown, Type, leftTime);
				}
			}
		}

		private class InterfaceSettings
		{
			#region Fields
			
			[JsonProperty(PropertyName = LangRu? "Ширина награды" : "Award Width")]
			public float AwardWidth;

			[JsonProperty(PropertyName = LangRu? "Высота награды" : "Award Height")]
			public float AwardHeight;

			[JsonProperty(PropertyName = LangRu? "Отступ по X между наградами" : "Award Indent (X)")]
			public float AwardIndentX;

			[JsonProperty(PropertyName = LangRu? "Количество наград на линии" : "Awards On Line")]
			public int AwardsOnLine;

			[JsonProperty(PropertyName = LangRu ? "Отступ сверху" : "Up Indent")]
			public float UpIndent;

			[JsonProperty(PropertyName = LangRu ? "Цвет доступного дня" : "Available Day Color")]
			public IColor AvaiableDayColor;

			[JsonProperty(PropertyName = LangRu ? "Фон" : "Background")]
			public BackgroundSettings Background;

			[JsonProperty(PropertyName = LangRu ? "Изображение при отсутствии награды" : "No Award Image")]
			public string NoAwardImage;

			[JsonProperty(PropertyName = LangRu ? "Цвет заблокированного дня" : "Blocked Day Color")]
			public IColor BlockedDayColor;

			[JsonProperty(PropertyName =
				LangRu ? "Цвет заблокированного дня с задержкой" : "Blocked With Cooldown Day Color")]
			public IColor BlockedWithCooldownDayColor;

			[JsonProperty(PropertyName = LangRu ? "Изображение кнопки Назад" : "Button Back Image")]
			public string ButtonBackImage;

			[JsonProperty(PropertyName = LangRu ? "Изображение кнопки Вперёд" : "Button Next Image")]
			public string ButtonNextImage;

			[JsonProperty(PropertyName = LangRu ? "Изображение иконки редактирования" : "Edit Image")]
			public string EditImage;

			[JsonProperty(PropertyName = LangRu ? "Цвет особого дня" : "Special Day Color")]
			public IColor SpecialDayColor;

			[JsonProperty(PropertyName =
				LangRu ? "Изображение особого дня на финальном дне" : "Special day Image for the final day")]
			public string SpecialDayImageForFinalDay;

			[JsonProperty(PropertyName = LangRu ? "Цвет полосы прогресса" : "Progress Bar Color")]
			public IColor ProgressBarColor;

			[JsonProperty(PropertyName = LangRu ? "Изображение иконки Rust" : "Rust Icon Image")]
			public string RustIconImage;

			[JsonProperty(PropertyName = LangRu ? "Изображение секретного дня" : "Secret Day Image")]
			public string SecretDayImage;

			[JsonProperty(PropertyName = LangRu ? "Изображение иконки выбора предмета" : "Select Item Image")]
			public string SelectItemImage;

			[JsonProperty(PropertyName =
				LangRu ? "Показывать изображение на секретном дне?" : "Show image on secret day?")]
			public bool ShowImageOnSecretDay;

			[JsonProperty(PropertyName = LangRu ? "Цвет полученного дня" : "Taked Day Color")]
			public IColor TakedDayColor;

			[JsonProperty(PropertyName =
				LangRu
					? "Использовать цвет для полностью заполненной полосы прогресса"
					: "Use color for a fully filled progress bar?")]
			public bool UseFullyFilledProgressBarColor;

			[JsonProperty(PropertyName =
				LangRu ? "Цвет полностью заполненной полосы прогресса" : "Fully Filled Progress Bar Color")]
			public IColor FullyFilledProgressBarColor;

			[JsonProperty(PropertyName =
				LangRu ? "Использовать цвет для особого дня?" : "Use color for the special day?")]
			public bool UseSpecialDayColor;

			[JsonProperty(PropertyName = LangRu ? "Основная панель" : "Main Panel")]
			public ImageSettings ContentPanel;

			[JsonProperty(PropertyName = LangRu ? "Заголовок плагина" : "Plugin title")]
			public TextSettings TitlePlugin;

			[JsonProperty(PropertyName = LangRu ? "Описание плагина" : "Plugin description")]
			public TextSettings DescriptionPlugin;

			[JsonProperty(PropertyName = LangRu? "Показать линию?" : "Show line?")]
			public bool ShowLine;
			
			[JsonProperty(PropertyName = LangRu? "Линия" : "Line")]
			public ImageSettings HeaderLine = new();
			
			[JsonProperty(PropertyName = LangRu ? "Кнопка инвентаря" : "Inventory button")]
			public ButtonSettings InventoryButton;

			[JsonProperty(PropertyName = LangRu ? "Кнопка закрытия" : "Close button")]
			public ButtonSettings CloseButton;

			[JsonProperty(PropertyName = LangRu ? "Кнопка настроек" : "Settings button")]
			public ImageSettings SettingsButton;

			[JsonProperty(PropertyName = LangRu ? "Кнопка Назад (активная)" : "Back page button (active)")]
			public ButtonSettings PageButtonBackWhenActive;

			[JsonProperty(PropertyName = LangRu ? "Кнопка Назад (неактивная)" : "Back page button (inactive)")]
			public ButtonSettings PageButtonBackWhenInactive;

			[JsonProperty(PropertyName = LangRu ? "Кнопка Вперёд (активная)" : "Next page button (active)")]
			public ButtonSettings PageButtonNextWhenActive;

			[JsonProperty(PropertyName = LangRu ? "Кнопка Вперёд (неактивная)" : "Next page button (inactive)")]
			public ButtonSettings PageButtonNextWhenInactive;

			[JsonProperty(PropertyName = LangRu ? "Фон полосы прогресса" : "Progress bar background")]
			public ImageSettings ProgressBarBackground;

			[JsonProperty(PropertyName = LangRu ? "Панель задержки" : "Cooldown panel")]
			public ImageSettings CooldownPanel;

			[JsonProperty(PropertyName = LangRu ? "Заголовок задержки" : "Cooldown title")]
			public TextSettings CooldownTitle;

			[JsonProperty(PropertyName = LangRu ? "Панель оставшихся дней" : "Left Days panel")]
			public ImageSettings LeftDaysPanel;

			[JsonProperty(PropertyName = LangRu ? "Заголовок оставшихся дней" : "Left Days title")]
			public TextSettings LeftDaysTitle;

			[JsonProperty(PropertyName = LangRu ? "Количество оставшихся дней" : "Left Days amount")]
			public TextSettings LeftDaysAmount;

			[JsonProperty(PropertyName =
				LangRu ? "[Информация о награде] Кнопка Назад (активная)" : "[Award Info] Back page button (active)")]
			public ButtonSettings AwardInfoPageButtonBackWhenActive;

			[JsonProperty(PropertyName =
				LangRu
					? "[Информация о награде] Кнопка Назад (неактивная)"
					: "[Award Info] Back page button (inactive)")]
			public ButtonSettings AwardInfoPageButtonBackWhenInactive;

			[JsonProperty(PropertyName =
				LangRu ? "[Информация о награде] Кнопка Вперёд (активная)" : "[Award Info] Next page button (active)")]
			public ButtonSettings AwardInfoPageButtonNextWhenActive;

			[JsonProperty(PropertyName =
				LangRu
					? "[Информация о награде] Кнопка Вперёд (неактивная)"
					: "[Award Info] Next page button (inactive)")]
			public ButtonSettings AwardInfoPageButtonNextWhenInactive;

			[JsonProperty(PropertyName =
				LangRu ? "[Информация о награде] Количество страниц" : "[Award Info] Pages amount")]
			public TextSettings AwardInfoPagesNumber;

			[JsonProperty(PropertyName =
				LangRu ? "[Информация о награде] Кнопка закрытия" : "[Award Info] Close button")]
			public ButtonSettings AwardInfoCloseButton;

			[JsonProperty(PropertyName = LangRu ? "[Информация о награде] Заголовок" : "[Award Info] Title")]
			public TextSettings AwardInfoTitle;

			[JsonProperty(PropertyName = LangRu ? "[Информация о награде] Панель" : "[Award Info] Panel")]
			public ImageSettings AwardInfoPanel;

			[JsonProperty(PropertyName =
				LangRu
					? "[Информация о награде] Панель (в режиме редактирования)"
					: "[Award Info] Panel (in editing mode)")]
			public ImageSettings AwardInfoPanelInEditing;

			[JsonProperty(PropertyName =
				LangRu ? "[Информация о награде] Цвет фона предмета" : "[Award Info] Item background color")]
			public IColor AwardInfoItemBackgroundColor;

			[JsonProperty(PropertyName = LangRu ? "Настройка инвентаря" : "Inventory settings")]
			public InventoryUI Inventory = new();
			
			#endregion Fields

			#region Classes
			
			public class InventoryUI
			{
				[JsonProperty(PropertyName = LangRu ? "Заголовок" : "Title")]
				public TextSettings Title;

				[JsonProperty(PropertyName = LangRu ? "Описание" : "Description")]
				public TextSettings Description;

				[JsonProperty(PropertyName = LangRu ? "Кнопка Назад" : "Back Button")]
				public ButtonSettings ButtonBack;

				[JsonProperty(PropertyName = LangRu ? "Кнопка Закрыть" : "Close Button")]
				public ButtonSettings ButtonClose;

				[JsonProperty(PropertyName = LangRu? "Показать линию?" : "Show line?")]
				public bool ShowLine;
			
				[JsonProperty(PropertyName = LangRu? "Линия" : "Line")]
				public ImageSettings Line = new();

				[JsonProperty(PropertyName = LangRu? "Ширина предмета в инвентаре" : "Inventory Item Width")]
				public float InventoryItemWidth;
			
				[JsonProperty(PropertyName = LangRu? "Высота предмета в инвентаре" : "Inventory Item Height")]
				public float InventoryItemHeight;
			
				[JsonProperty(PropertyName = LangRu? "Отступ между предметами в инвентаре (X)" : "Inventory Items Margin (X)")]
				public float InventoryItemMarginX;
			
				[JsonProperty(PropertyName = LangRu? "Отступ между предметами в инвентаре (Y)" : "Inventory Items Margin (Y)")]
				public float InventoryItemMarginY;
			
				[JsonProperty(PropertyName = LangRu? "Количество предметов на строке в инвентаре" : "Inventory Items On Line")]
				public int InventoryItemsOnLine;
			
				[JsonProperty(PropertyName = LangRu? "Максимальное количество строк предметов в инвентаре" : "Inventory Max Items Lines")]
				public int InventoryMaxItemsLines;
			
				[JsonProperty(PropertyName = LangRu? "Отступ сверху для предметов в инвентаре" : "Top indent for items in inventory")]
				public float InventoryItemsTopIndent;
                
				#region Public Methods

				public int GetInventoryTotalAmount()
				{
					return InventoryMaxItemsLines * InventoryItemsOnLine;
				}

				#endregion
			}

			#endregion

			#region Templates

			public static InterfaceSettings GenerateFullscreenTemplate()
			{
				return new InterfaceSettings
				{
					AwardWidth = 145,
					AwardHeight = 215,
					AwardIndentX = 10,
					AwardsOnLine = 7,
					UpIndent = 15,
					Background = new BackgroundSettings
					{
						AnchorMin = "0 0",
						AnchorMax = "1 1",
						OffsetMin = "0 0",
						OffsetMax = "0 0",
						DisplayType = "Overlay",
						Sprite = "assets/content/textures/generic/fulltransparent.tga",
						Image = "https://i.ibb.co/K6LTT51/image-1.png",
						Material = string.Empty,
						Color = IColor.Create("#FFFFFF")
					},
					NoAwardImage = "https://i.ibb.co/sW7csSS/image.png",
					ShowImageOnSecretDay = false,
					SecretDayImage = "https://i.ibb.co/P4K4HvY/image.png",
					EditImage = "https://i.ibb.co/XyJfGSv/image.png",
					SelectItemImage = "https://i.ibb.co/pJF7XJN/search.png",
					RustIconImage = "https://i.ibb.co/Tbt6ZWS/image.png",
					TakedDayColor = IColor.Create("#525557"),
					AvaiableDayColor = IColor.Create("#EF5125"),
					BlockedDayColor = IColor.Create("#525557"),
					BlockedWithCooldownDayColor = IColor.Create("#525557"),
					UseSpecialDayColor = false,
					ContentPanel = new ImageSettings
					{
						AnchorMin = "0 0.5", AnchorMax = "1 0.5", OffsetMin = "40 -100", OffsetMax = "-40 115",
						Color = IColor.Create("#000000", 0)
					},
					TitlePlugin = new TextSettings
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = "-537.5 160",
						OffsetMax = "537.5 225",
						Align = TextAnchor.UpperLeft,
						IsBold = true,
						FontSize = 52,
						Color = IColor.Create("#DCDCDC")
					},
					DescriptionPlugin = new TextSettings
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = "-537.5 35",
						OffsetMax = "537.5 150",
						Align = TextAnchor.UpperLeft,
						IsBold = false,
						FontSize = 20,
						Color = IColor.Create("#FFFFFF",
							90)
					},
					ShowLine = false,
					HeaderLine = new ImageSettings(),
					InventoryButton = new ButtonSettings
					{
						AnchorMin = "1 1",
						AnchorMax = "1 1",
						OffsetMin = "-180 -55",
						OffsetMax = "-90 -15",
						Align = TextAnchor.MiddleCenter,
						IsBold = true,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#EF5125"),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = string.Empty,
						ImageColor = IColor.Create("#FFFFFF")
					},
					CloseButton = new ButtonSettings
					{
						AnchorMin = "1 1",
						AnchorMax = "1 1",
						OffsetMin = "-80 -55",
						OffsetMax = "-40 -15",
						Align = TextAnchor.MiddleCenter,
						IsBold = false,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#EF5125"),
						Sprite = "assets/icons/close.png",
						Material = null,
						Image = string.Empty,
						ImageColor = IColor.Create("#FFFFFF")
					},
					SettingsButton = new ImageSettings
					{
						AnchorMin = "1 1", AnchorMax = "1 1",
						OffsetMin = "-225 -55",
						OffsetMax = "-185 -15",
						Color = IColor.Create("#519229")
					},
					PageButtonBackWhenActive = new ButtonSettings
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = "-590 -135",
						OffsetMax = "-560 -105",
						Align = TextAnchor.MiddleCenter,
						IsBold = false,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#000000",
							00),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = "https://i.ibb.co/27g86Sk/image.png",
						ImageColor = IColor.Create("#EF5125")
					},
					PageButtonBackWhenInactive = new ButtonSettings
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = "-590 -135",
						OffsetMax = "-560 -105",
						Align = TextAnchor.MiddleCenter,
						IsBold = false,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#000000",
							00),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = "https://i.ibb.co/27g86Sk/image.png",
						ImageColor = IColor.Create("#FFFFFF",
							20)
					},
					PageButtonNextWhenActive = new ButtonSettings
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = "560 -135",
						OffsetMax = "590 -105",
						Align = TextAnchor.MiddleCenter,
						IsBold = false,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#000000",
							00),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = "https://i.ibb.co/SPT0vN1/image.png",
						ImageColor = IColor.Create("#EF5125")
					},
					PageButtonNextWhenInactive = new ButtonSettings
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = "560 -135",
						OffsetMax = "590 -105",
						Align = TextAnchor.MiddleCenter,
						IsBold = false,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#000000",
							00),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = "https://i.ibb.co/SPT0vN1/image.png",
						ImageColor = IColor.Create("#FFFFFF",
							20)
					},
					ProgressBarBackground = new ImageSettings
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = "-537.5 -255",
						OffsetMax = "537.5 -250",
						Sprite = string.Empty,
						Material = string.Empty,
						Image = string.Empty,
						Color = IColor.Create("#646668")
					},
					CooldownPanel = new ImageSettings
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = "-100 -305",
						OffsetMax = "120 -275",
						Color = IColor.Create("#EF5125")
					},
					CooldownTitle = new TextSettings
					{
						AnchorMin = "0 0",
						AnchorMax = "0 1",
						OffsetMin = "-450 0",
						OffsetMax = "-10 0",
						Align = TextAnchor.MiddleRight,
						IsBold = false,
						FontSize = 20,
						Color = IColor.Create("#FFFFFF",
							90)
					},
					LeftDaysPanel = new ImageSettings
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = "495 -300",
						OffsetMax = "550 -280",
						Color = IColor.Create("#EF5125")
					},
					LeftDaysTitle = new TextSettings
					{
						AnchorMin = "0 0",
						AnchorMax = "0 1",
						OffsetMin = "-200 0",
						OffsetMax = "-5 0",
						Align = TextAnchor.MiddleRight,
						IsBold = true,
						FontSize = 16,
						Color = IColor.Create("#FFFFFF",
							90)
					},
					LeftDaysAmount = new TextSettings
					{
						AnchorMin = "0 0",
						AnchorMax = "1 1",
						OffsetMin = "0 0",
						OffsetMax = "0 0",
						Align = TextAnchor.MiddleCenter,
						IsBold = true,
						FontSize = 16,
						Color = IColor.Create("#FFFFFF",
							90)
					},
					SpecialDayColor = IColor.Create("#FF9A1E"),
					SpecialDayImageForFinalDay = "https://i.ibb.co/Zd1jcwz/image.png",
					ButtonBackImage = "https://i.ibb.co/27g86Sk/image.png",
					ButtonNextImage = "https://i.ibb.co/SPT0vN1/image.png",
					ProgressBarColor = IColor.Create("#EF5125"),
					UseFullyFilledProgressBarColor = false,
					FullyFilledProgressBarColor = IColor.Create("#FF9A1E"),
					AwardInfoPageButtonBackWhenActive = new ButtonSettings
					{
						AnchorMin = "0 0",
						AnchorMax = "0 0",
						OffsetMin = "30 120",
						OffsetMax = "60 150",
						Align = TextAnchor.MiddleCenter,
						IsBold = false,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#000000",
							00),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = "https://i.ibb.co/27g86Sk/image.png",
						ImageColor = IColor.Create("#EF5125")
					},
					AwardInfoPageButtonBackWhenInactive = new ButtonSettings
					{
						AnchorMin = "0 0",
						AnchorMax = "0 0",
						OffsetMin = "30 120",
						OffsetMax = "60 150",
						Align = TextAnchor.MiddleCenter,
						IsBold = false,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#000000",
							00),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = "https://i.ibb.co/27g86Sk/image.png",
						ImageColor = IColor.Create("#FFFFFF",
							20)
					},
					AwardInfoPageButtonNextWhenActive = new ButtonSettings
					{
						AnchorMin = "1 0",
						AnchorMax = "1 0",
						OffsetMin = "-60 120",
						OffsetMax = "-30 150",
						Align = TextAnchor.MiddleCenter,
						IsBold = false,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#000000",
							00),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = "https://i.ibb.co/SPT0vN1/image.png",
						ImageColor = IColor.Create("#EF5125")
					},
					AwardInfoPageButtonNextWhenInactive = new ButtonSettings
					{
						AnchorMin = "1 0",
						AnchorMax = "1 0",
						OffsetMin = "-60 120",
						OffsetMax = "-30 150",
						Align = TextAnchor.MiddleCenter,
						IsBold = false,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#000000",
							00),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = "https://i.ibb.co/SPT0vN1/image.png",
						ImageColor = IColor.Create("#FFFFFF",
							20)
					},
					AwardInfoPagesNumber = new TextSettings
					{
						AnchorMin = "0.5 0",
						AnchorMax = "0.5 0",
						OffsetMin = "-125 120",
						OffsetMax = "125 150",
						Align = TextAnchor.MiddleCenter,
						IsBold = true,
						FontSize = 22,
						Color = IColor.Create("#FFFFFF",
							40)
					},
					AwardInfoCloseButton = new ButtonSettings
					{
						AnchorMin = "0.5 0",
						AnchorMax = "0.5 0",
						OffsetMin = "-180 30",
						OffsetMax = "180 85",
						Align = TextAnchor.MiddleCenter,
						IsBold = true,
						FontSize = 32,
						Color = IColor.Create("#FDEDE9"),
						ButtonColor = IColor.Create("#519229")
					},
					AwardInfoTitle = new TextSettings
					{
						AnchorMin = "0 1",
						AnchorMax = "1 1",
						OffsetMin = "0 -100",
						OffsetMax = "0 0",
						Align = TextAnchor.MiddleCenter,
						IsBold = true,
						FontSize = 36,
						Color = IColor.Create("#DCDCDC")
					},
					AwardInfoPanel = new ImageSettings
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = "-210 -317.5",
						OffsetMax = "210 317.5",
						Color = IColor.Create("#202224")
					},
					AwardInfoPanelInEditing = new ImageSettings
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = "-410 -317.5",
						OffsetMax = "10 317.5",
						Color = IColor.Create("#202224")
					},
					AwardInfoItemBackgroundColor = IColor.Create("#38393F"),
					Inventory = new InventoryUI
					{
						Title = new TextSettings
						{
							AnchorMin = "0.5 0.5",
							AnchorMax = "0.5 0.5",
							OffsetMin = "-550 160",
							OffsetMax = "550 225",
							Align = TextAnchor.UpperLeft,
							IsBold = true,
							FontSize = 52,
							Color = IColor.Create("#DCDCDC")
						},
						Description = new TextSettings
						{
							AnchorMin = "0.5 0.5",
							AnchorMax = "0.5 0.5",
							OffsetMin = "-550 35",
							OffsetMax = "550 150",
							Align = TextAnchor.UpperLeft,
							IsBold = true,
							FontSize = 20,
							Color = IColor.Create("#FFFFFF", 90)
						},
						ButtonBack = new ButtonSettings
						{
							AnchorMin = "1 1", AnchorMax = "1 1",
							OffsetMin = "-180 -55",
							OffsetMax = "-90 -15",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 14,
							Color = IColor.Create("#FFFFFF"),
							ButtonColor = IColor.Create("#EF5125")
						},
						ButtonClose = new ButtonSettings
						{
							AnchorMin = "1 1", AnchorMax = "1 1",
							OffsetMin = "-80 -55",
							OffsetMax = "-40 -15",
							Color = IColor.Create("#000000", 0),
							ButtonColor = IColor.Create("#EF5125"),
							Sprite = "assets/icons/close.png"
						},
						InventoryItemWidth = 120,
						InventoryItemHeight = 120,
						InventoryItemMarginX = 10,
						InventoryItemMarginY = 10,
						InventoryItemsOnLine = 8,
						InventoryMaxItemsLines = 2,
						InventoryItemsTopIndent = 65
					}
				};
			}

			public static InterfaceSettings GenerateMenuTemplateV1()
			{
				return new InterfaceSettings
				{
					AwardWidth = 145,
					AwardHeight = 215,
					AwardIndentX = 30,
					AwardsOnLine = 7,
					UpIndent = -125,
					Background = new BackgroundSettings
					{
						AnchorMin = "0 0",
						AnchorMax = "1 1",
						OffsetMin = "0 0",
						OffsetMax = "0 0",
						DisplayType = "Overlay",
						Sprite = "assets/content/textures/generic/fulltransparent.tga",
						Image = "https://i.ibb.co/K6LTT51/image-1.png",
						Material = string.Empty,
						Color = IColor.Create("#FFFFFF")
					},
					NoAwardImage = "https://i.ibb.co/sW7csSS/image.png",
					ShowImageOnSecretDay = false,
					SecretDayImage = "https://i.ibb.co/P4K4HvY/image.png",
					EditImage = "https://i.ibb.co/XyJfGSv/image.png",
					SelectItemImage = "https://i.ibb.co/pJF7XJN/search.png",
					RustIconImage = "https://i.ibb.co/Tbt6ZWS/image.png",
					TakedDayColor = IColor.Create("#525557"),
					AvaiableDayColor = IColor.Create("#EF5125"),
					BlockedDayColor = IColor.Create("#525557"),
					BlockedWithCooldownDayColor = IColor.Create("#525557"),
					UseSpecialDayColor = false,
					ContentPanel = new ImageSettings
					{
						AnchorMin = "0 0.5", AnchorMax = "1 0.5", OffsetMin = "40 -140", OffsetMax = "-47 75",
						Color = IColor.Create("#000000", 0)
					},
					TitlePlugin = new TextSettings
					{
						AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "40 -70", OffsetMax = "-45 -20",
						Align = TextAnchor.UpperLeft,
						IsBold = false,
						FontSize = 0,
						Color = IColor.Create("#000000", 0)
					},
					DescriptionPlugin = new TextSettings
					{
						AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "40 -120", OffsetMax = "840 -20",
						Align = TextAnchor.MiddleLeft,
						IsBold = false,
						FontSize = 18,
						Color = IColor.Create("#E2DBD3", 90)
					},
					ShowLine = false,
					HeaderLine = new ImageSettings(),
					InventoryButton = new ButtonSettings
					{
						AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-130 -90", OffsetMax = "-40 -50",
						Align = TextAnchor.MiddleCenter,
						IsBold = true,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#EF5125"),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = string.Empty,
						ImageColor = IColor.Create("#FFFFFF")
					},
					CloseButton = new ButtonSettings(),
					SettingsButton = new ImageSettings
					{
						AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-175 -90", OffsetMax = "-135 -50",
						Color = IColor.Create("#519229")
					},
					PageButtonBackWhenActive = new ButtonSettings
					{
						AnchorMin = "0 0.5",
						AnchorMax = "0 0.5",
						OffsetMin = "-30 -40",
						OffsetMax = "-10 -15",
						Align = TextAnchor.MiddleCenter,
						IsBold = false,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#000000", 00),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = "https://i.ibb.co/27g86Sk/image.png",
						ImageColor = IColor.Create("#EF5125")
					},
					PageButtonBackWhenInactive = new ButtonSettings
					{
						AnchorMin = "0 0.5",
						AnchorMax = "0 0.5",
						OffsetMin = "-30 -40",
						OffsetMax = "-10 -15",
						Align = TextAnchor.MiddleCenter,
						IsBold = false,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#000000", 00),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = "https://i.ibb.co/27g86Sk/image.png",
						ImageColor = IColor.Create("#FFFFFF", 20)
					},
					PageButtonNextWhenActive = new ButtonSettings
					{
						AnchorMin = "1 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "10 -40",
						OffsetMax = "30 -15",
						Align = TextAnchor.MiddleCenter,
						IsBold = false,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#000000", 00),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = "https://i.ibb.co/SPT0vN1/image.png",
						ImageColor = IColor.Create("#EF5125")
					},
					PageButtonNextWhenInactive = new ButtonSettings
					{
						AnchorMin = "1 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "10 -40",
						OffsetMax = "30 -15",
						Align = TextAnchor.MiddleCenter,
						IsBold = false,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#000000", 00),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = "https://i.ibb.co/SPT0vN1/image.png",
						ImageColor = IColor.Create("#FFFFFF", 20)
					},
					ProgressBarBackground = new ImageSettings
					{
						AnchorMin = "0 0.5", AnchorMax = "1 0.5", OffsetMin = "40 -190", OffsetMax = "-47 -186",
						Color = IColor.Create("#646668")
					},
					CooldownPanel = new ImageSettings
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-112 -250", OffsetMax = "105 -220",
						Color = IColor.Create("#EF5125")
					},
					CooldownTitle = new TextSettings
					{
						AnchorMin = "0 0",
						AnchorMax = "0 1",
						OffsetMin = "-450 0",
						OffsetMax = "-10 0",
						Align = TextAnchor.MiddleRight,
						IsBold = false,
						FontSize = 20,
						Color = IColor.Create("#FFFFFF", 90)
					},
					LeftDaysPanel = new ImageSettings
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "340 -250", OffsetMax = "395 -220",
						Color = IColor.Create("#EF5125")
					},
					LeftDaysTitle = new TextSettings
					{
						AnchorMin = "0 0", AnchorMax = "0 1",
						OffsetMin = "-200 0",
						OffsetMax = "-5 0",
						Align = TextAnchor.MiddleRight,
						IsBold = true,
						FontSize = 16,
						Color = IColor.Create("#FFFFFF", 90)
					},
					LeftDaysAmount = new TextSettings
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "0 0", OffsetMax = "0 0",
						Align = TextAnchor.MiddleCenter,
						IsBold = true,
						FontSize = 16,
						Color = IColor.Create("#FFFFFF", 90)
					},
					SpecialDayColor = IColor.Create("#FF9A1E"),
					SpecialDayImageForFinalDay = "https://i.ibb.co/Zd1jcwz/image.png",
					ButtonBackImage = "https://i.ibb.co/27g86Sk/image.png",
					ButtonNextImage = "https://i.ibb.co/SPT0vN1/image.png",
					ProgressBarColor = IColor.Create("#EF5125"),
					UseFullyFilledProgressBarColor = false,
					FullyFilledProgressBarColor = IColor.Create("#FF9A1E"),
					AwardInfoPageButtonBackWhenActive = new ButtonSettings
					{
						AnchorMin = "0 0",
						AnchorMax = "0 0",
						OffsetMin = "30 120",
						OffsetMax = "60 150",
						Align = TextAnchor.MiddleCenter,
						IsBold = false,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#000000", 00),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = "https://i.ibb.co/27g86Sk/image.png",
						ImageColor = IColor.Create("#EF5125")
					},
					AwardInfoPageButtonBackWhenInactive = new ButtonSettings
					{
						AnchorMin = "0 0",
						AnchorMax = "0 0",
						OffsetMin = "30 120",
						OffsetMax = "60 150",
						Align = TextAnchor.MiddleCenter,
						IsBold = false,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#000000", 00),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = "https://i.ibb.co/27g86Sk/image.png",
						ImageColor = IColor.Create("#FFFFFF", 20)
					},
					AwardInfoPageButtonNextWhenActive = new ButtonSettings
					{
						AnchorMin = "1 0",
						AnchorMax = "1 0",
						OffsetMin = "-60 120",
						OffsetMax = "-30 150",
						Align = TextAnchor.MiddleCenter,
						IsBold = false,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#000000", 00),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = "https://i.ibb.co/SPT0vN1/image.png",
						ImageColor = IColor.Create("#EF5125")
					},
					AwardInfoPageButtonNextWhenInactive = new ButtonSettings
					{
						AnchorMin = "1 0",
						AnchorMax = "1 0",
						OffsetMin = "-60 120",
						OffsetMax = "-30 150",
						Align = TextAnchor.MiddleCenter,
						IsBold = false,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#000000", 00),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = "https://i.ibb.co/SPT0vN1/image.png",
						ImageColor = IColor.Create("#FFFFFF", 20)
					},
					AwardInfoPagesNumber = new TextSettings
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0",
						OffsetMin = "-125 120",
						OffsetMax = "125 150",
						Align = TextAnchor.MiddleCenter,
						IsBold = true,
						FontSize = 22,
						Color = IColor.Create("#FFFFFF", 40)
					},
					AwardInfoCloseButton = new ButtonSettings
					{
						AnchorMin = "0.5 0",
						AnchorMax = "0.5 0",
						OffsetMin = "-180 30",
						OffsetMax = "180 85",
						Align = TextAnchor.MiddleCenter,
						IsBold = true,
						FontSize = 32,
						Color = IColor.Create("#FDEDE9"),
						ButtonColor = IColor.Create("#519229")
					},
					AwardInfoTitle = new TextSettings
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "0 -100",
						OffsetMax = "0 0",
						Align = TextAnchor.MiddleCenter,
						IsBold = true,
						FontSize = 36,
						Color = IColor.Create("#DCDCDC")
					},
					AwardInfoPanel = new ImageSettings
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = "-210 -317.5",
						OffsetMax = "210 317.5",
						Color = IColor.Create("#202224")
					},
					AwardInfoPanelInEditing = new ImageSettings
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = "-410 -317.5",
						OffsetMax = "10 317.5",
						Color = IColor.Create("#202224")
					},
					AwardInfoItemBackgroundColor = IColor.Create("#38393F"),
					Inventory = new InventoryUI
					{
						Title = new TextSettings
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "40 -70", OffsetMax = "-45 -20",
							Align = TextAnchor.UpperLeft,
							IsBold = false,
							FontSize = 0,
							Color = IColor.Create("#000000", 0)
						},
						Description = new TextSettings
						{
							AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "40 -120", OffsetMax = "840 -20",
							Align = TextAnchor.MiddleLeft,
							IsBold = false,
							FontSize = 18,
							Color = IColor.Create("#E2DBD3", 90)
						},
						ShowLine = false,
						Line = new(),
						ButtonBack = new ButtonSettings
						{
							AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-130 -90", OffsetMax = "-40 -50",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 14,
							Color = IColor.Create("#FFFFFF"),
							ButtonColor = IColor.Create("#EF5125")
						},
						ButtonClose = new ButtonSettings(),
						InventoryItemWidth = 100,
						InventoryItemHeight = 100,
						InventoryItemMarginX = 8,
						InventoryItemMarginY = 8,
						InventoryItemsOnLine = 10,
						InventoryMaxItemsLines = 3,
						InventoryItemsTopIndent = 60
					}
				};
			}

			public static InterfaceSettings GenerateMenuTemplateV2()
			{
				return new InterfaceSettings
				{
					AwardWidth = 145,
					AwardHeight = 215,
					AwardIndentX = 32,
					AwardsOnLine = 5,
					UpIndent = -85,
					Background = new BackgroundSettings
					{
						AnchorMin = "0 0",
						AnchorMax = "1 1",
						OffsetMin = "0 0",
						OffsetMax = "0 0",
						DisplayType = "Overlay",
						Sprite = "assets/content/textures/generic/fulltransparent.tga",
						Image = "https://i.ibb.co/K6LTT51/image-1.png",
						Material = string.Empty,
						Color = IColor.Create("#FFFFFF")
					},
					NoAwardImage = "https://i.ibb.co/sW7csSS/image.png",
					ShowImageOnSecretDay = false,
					SecretDayImage = "https://i.ibb.co/P4K4HvY/image.png",
					EditImage = "https://i.ibb.co/XyJfGSv/image.png",
					SelectItemImage = "https://i.ibb.co/pJF7XJN/search.png",
					RustIconImage = "https://i.ibb.co/Tbt6ZWS/image.png",
					TakedDayColor = IColor.Create("#525557"),
					AvaiableDayColor = IColor.Create("#EF5125"),
					BlockedDayColor = IColor.Create("#525557"),
					BlockedWithCooldownDayColor = IColor.Create("#525557"),
					UseSpecialDayColor = false,
					ContentPanel = new ImageSettings
					{
						AnchorMin = "0 0.5", AnchorMax = "1 0.5", OffsetMin = "40 -140", OffsetMax = "-47 75",
						Color = IColor.Create("#000000", 0)
					},
					TitlePlugin = new TextSettings
					{
						AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "40 -70", OffsetMax = "-45 -20",
						Align = TextAnchor.UpperLeft,
						IsBold = true,
						FontSize = 32,
						Color = IColor.Create("#CF432D", 90)
					},
					DescriptionPlugin = new TextSettings
					{
						AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "40 -200", OffsetMax = "840 -90",
						Align = TextAnchor.MiddleLeft,
						IsBold = false,
						FontSize = 18,
						Color = IColor.Create("#E2DBD3", 90)
					},
					ShowLine = true,
					HeaderLine = new ImageSettings
					{
						AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "40 -71", OffsetMax = "-82 -69",
						Color = IColor.Create("#373737", 50)
					},
					InventoryButton = new ButtonSettings
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "346 150", OffsetMax = "436 190",
						Align = TextAnchor.MiddleCenter,
						IsBold = true,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#EF5125"),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = string.Empty,
						ImageColor = IColor.Create("#FFFFFF")
					},
					CloseButton = new ButtonSettings(),
					SettingsButton = new ImageSettings
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "301 150", OffsetMax = "341 190",
						Color = IColor.Create("#519229")
					},
					PageButtonBackWhenActive = new ButtonSettings
					{
						AnchorMin = "0 0.5",
						AnchorMax = "0 0.5",
						OffsetMin = "-30 -40",
						OffsetMax = "-10 -15",
						Align = TextAnchor.MiddleCenter,
						IsBold = false,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#000000", 00),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = "https://i.ibb.co/27g86Sk/image.png",
						ImageColor = IColor.Create("#EF5125")
					},
					PageButtonBackWhenInactive = new ButtonSettings
					{
						AnchorMin = "0 0.5",
						AnchorMax = "0 0.5",
						OffsetMin = "-30 -40",
						OffsetMax = "-10 -15",
						Align = TextAnchor.MiddleCenter,
						IsBold = false,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#000000", 00),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = "https://i.ibb.co/27g86Sk/image.png",
						ImageColor = IColor.Create("#FFFFFF", 20)
					},
					PageButtonNextWhenActive = new ButtonSettings
					{
						AnchorMin = "1 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "10 -40",
						OffsetMax = "30 -15",
						Align = TextAnchor.MiddleCenter,
						IsBold = false,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#000000", 00),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = "https://i.ibb.co/SPT0vN1/image.png",
						ImageColor = IColor.Create("#EF5125")
					},
					PageButtonNextWhenInactive = new ButtonSettings
					{
						AnchorMin = "1 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "10 -40",
						OffsetMax = "30 -15",
						Align = TextAnchor.MiddleCenter,
						IsBold = false,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#000000", 00),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = "https://i.ibb.co/SPT0vN1/image.png",
						ImageColor = IColor.Create("#FFFFFF", 20)
					},
					ProgressBarBackground = new ImageSettings
					{
						AnchorMin = "0 0.5", AnchorMax = "1 0.5", OffsetMin = "40 -190", OffsetMax = "-47 -186",
						Color = IColor.Create("#646668")
					},
					CooldownPanel = new ImageSettings
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-112 -250", OffsetMax = "105 -220",
						Color = IColor.Create("#EF5125")
					},
					CooldownTitle = new TextSettings
					{
						AnchorMin = "0 0",
						AnchorMax = "0 1",
						OffsetMin = "-450 0",
						OffsetMax = "-10 0",
						Align = TextAnchor.MiddleRight,
						IsBold = false,
						FontSize = 20,
						Color = IColor.Create("#FFFFFF", 90)
					},
					LeftDaysPanel = new ImageSettings
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "340 -250", OffsetMax = "395 -220",
						Color = IColor.Create("#EF5125")
					},
					LeftDaysTitle = new TextSettings
					{
						AnchorMin = "0 0", AnchorMax = "0 1",
						OffsetMin = "-200 0",
						OffsetMax = "-5 0",
						Align = TextAnchor.MiddleRight,
						IsBold = true,
						FontSize = 16,
						Color = IColor.Create("#FFFFFF", 90)
					},
					LeftDaysAmount = new TextSettings
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "0 0", OffsetMax = "0 0",
						Align = TextAnchor.MiddleCenter,
						IsBold = true,
						FontSize = 16,
						Color = IColor.Create("#FFFFFF", 90)
					},
					SpecialDayColor = IColor.Create("#FF9A1E"),
					SpecialDayImageForFinalDay = "https://i.ibb.co/Zd1jcwz/image.png",
					ButtonBackImage = "https://i.ibb.co/27g86Sk/image.png",
					ButtonNextImage = "https://i.ibb.co/SPT0vN1/image.png",
					ProgressBarColor = IColor.Create("#EF5125"),
					UseFullyFilledProgressBarColor = false,
					FullyFilledProgressBarColor = IColor.Create("#FF9A1E"),
					AwardInfoPageButtonBackWhenActive = new ButtonSettings
					{
						AnchorMin = "0 0",
						AnchorMax = "0 0",
						OffsetMin = "30 120",
						OffsetMax = "60 150",
						Align = TextAnchor.MiddleCenter,
						IsBold = false,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#000000", 00),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = "https://i.ibb.co/27g86Sk/image.png",
						ImageColor = IColor.Create("#EF5125")
					},
					AwardInfoPageButtonBackWhenInactive = new ButtonSettings
					{
						AnchorMin = "0 0",
						AnchorMax = "0 0",
						OffsetMin = "30 120",
						OffsetMax = "60 150",
						Align = TextAnchor.MiddleCenter,
						IsBold = false,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#000000", 00),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = "https://i.ibb.co/27g86Sk/image.png",
						ImageColor = IColor.Create("#FFFFFF", 20)
					},
					AwardInfoPageButtonNextWhenActive = new ButtonSettings
					{
						AnchorMin = "1 0",
						AnchorMax = "1 0",
						OffsetMin = "-60 120",
						OffsetMax = "-30 150",
						Align = TextAnchor.MiddleCenter,
						IsBold = false,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#000000", 00),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = "https://i.ibb.co/SPT0vN1/image.png",
						ImageColor = IColor.Create("#EF5125")
					},
					AwardInfoPageButtonNextWhenInactive = new ButtonSettings
					{
						AnchorMin = "1 0",
						AnchorMax = "1 0",
						OffsetMin = "-60 120",
						OffsetMax = "-30 150",
						Align = TextAnchor.MiddleCenter,
						IsBold = false,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#000000", 00),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = "https://i.ibb.co/SPT0vN1/image.png",
						ImageColor = IColor.Create("#FFFFFF", 20)
					},
					AwardInfoPagesNumber = new TextSettings
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0",
						OffsetMin = "-125 120",
						OffsetMax = "125 150",
						Align = TextAnchor.MiddleCenter,
						IsBold = true,
						FontSize = 22,
						Color = IColor.Create("#FFFFFF", 40)
					},
					AwardInfoCloseButton = new ButtonSettings
					{
						AnchorMin = "0.5 0",
						AnchorMax = "0.5 0",
						OffsetMin = "-180 30",
						OffsetMax = "180 85",
						Align = TextAnchor.MiddleCenter,
						IsBold = true,
						FontSize = 32,
						Color = IColor.Create("#FDEDE9"),
						ButtonColor = IColor.Create("#519229")
					},
					AwardInfoTitle = new TextSettings
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "0 -100",
						OffsetMax = "0 0",
						Align = TextAnchor.MiddleCenter,
						IsBold = true,
						FontSize = 36,
						Color = IColor.Create("#DCDCDC")
					},
					AwardInfoPanel = new ImageSettings
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = "-210 -317.5",
						OffsetMax = "210 317.5",
						Color = IColor.Create("#202224")
					},
					AwardInfoPanelInEditing = new ImageSettings
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = "-410 -317.5",
						OffsetMax = "10 317.5",
						Color = IColor.Create("#202224")
					},
					AwardInfoItemBackgroundColor = IColor.Create("#38393F"),
					Inventory = new InventoryUI
					{
						Title = new TextSettings
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "40 -70", OffsetMax = "-45 -20",
							Align = TextAnchor.UpperLeft,
							IsBold = true,
							FontSize = 32,
							Color = IColor.Create("#CF432D", 90)
						},
						Description = new TextSettings
						{
							AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "40 -200", OffsetMax = "840 -90",
							Align = TextAnchor.MiddleLeft,
							IsBold = false,
							FontSize = 18,
							Color = IColor.Create("#E2DBD3")
						},
						ShowLine = true,
						Line = new ImageSettings
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "40 -71", OffsetMax = "-82 -69",
							Color = IColor.Create("#373737", 50)
						},
						ButtonBack = new ButtonSettings
						{
							AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "346 150", OffsetMax = "436 190",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 14,
							Color = IColor.Create("#FFFFFF"),
							ButtonColor = IColor.Create("#EF5125")
						},
						ButtonClose = new ButtonSettings(),
						InventoryItemWidth = 100,
						InventoryItemHeight = 100,
						InventoryItemMarginX = 8,
						InventoryItemMarginY = 8,
						InventoryItemsOnLine = 7,
						InventoryMaxItemsLines = 3,
						InventoryItemsTopIndent = 120
					}
				};
			}

			#endregion Templates
		}

		private class BackgroundSettings : ImageSettings
		{
			[JsonProperty(PropertyName = LangRu ? "Тип отображения (Overlay/Hud)" : "Display type (Overlay/Hud)")]
			public string DisplayType;

			public CuiElement Get(string name, string destroyUI, bool needCursor)
			{
				var image = GetImage(DisplayType, name, destroyUI);

				if (needCursor)
					image.Components.Add(new CuiNeedsCursorComponent());

				return image;
			}
		}

		private class ImageSettings : InterfacePosition
		{
			[JsonProperty(PropertyName = LangRu ? "Спрайт" : "Sprite")]
			public string Sprite;

			[JsonProperty(PropertyName = LangRu ? "Материал" : "Material")]
			public string Material;

			[JsonProperty(PropertyName = LangRu ? "Изображение" : "Image")]
			public string Image;

			[JsonProperty(PropertyName = LangRu ? "Цвет" : "Color")]
			public IColor Color;

			private ICuiComponent GetImage()
			{
				if (!string.IsNullOrEmpty(Image))
				{
					var rawImage = new CuiRawImageComponent
					{
						Png = _instance.GetImage(Image),
						Color = Color.Get
					};

					if (!string.IsNullOrEmpty(Sprite))
						rawImage.Sprite = Sprite;

					if (!string.IsNullOrEmpty(Material))
						rawImage.Material = Material;

					return rawImage;
				}

				var image = new CuiImageComponent
				{
					Color = Color.Get
				};

				if (!string.IsNullOrEmpty(Sprite))
					image.Sprite = Sprite;

				if (!string.IsNullOrEmpty(Material))
					image.Material = Material;

				return image;
			}

			public CuiElement GetImage(string parent,
				string name = null,
				string destroyUI = null)
			{
				if (string.IsNullOrEmpty(name))
					name = CuiHelper.GetGuid();

				return new CuiElement
				{
					Name = name,
					Parent = parent,
					DestroyUi = destroyUI,
					Components =
					{
						GetImage(),
						GetPosition()
					}
				};
			}
		}

		private class ButtonSettings : TextSettings
		{
			[JsonProperty(PropertyName = LangRu ? "Цвет кнопки" : "Button Color")]
			public IColor ButtonColor;

			[JsonProperty(PropertyName = LangRu ? "Спрайт" : "Sprite")]
			public string Sprite;

			[JsonProperty(PropertyName = LangRu ? "Материал" : "Material")]
			public string Material;

			[JsonProperty(PropertyName = LangRu ? "Изображение" : "Image")]
			public string Image;

			[JsonProperty(PropertyName = LangRu ? "Цвет изображения" : "Image Color")]
			public IColor ImageColor;

			public List<CuiElement> Get(
				string msg,
				string cmd,
				string parent,
				string name = null,
				string destroyUI = null,
				string close = null)
			{
				if (string.IsNullOrEmpty(name))
					name = CuiHelper.GetGuid();

				var list = new List<CuiElement>();

				var btn = new CuiButtonComponent
				{
					Color = ButtonColor.Get
				};

				if (!string.IsNullOrEmpty(cmd))
					btn.Command = cmd;

				if (!string.IsNullOrEmpty(close))
					btn.Close = close;

				if (!string.IsNullOrEmpty(Sprite))
					btn.Sprite = Sprite;

				if (!string.IsNullOrEmpty(Material))
					btn.Material = Material;

				if (!string.IsNullOrEmpty(Image))
				{
					list.Add(new CuiElement
					{
						Name = name,
						Parent = parent,
						DestroyUi = destroyUI,
						Components =
						{
							new CuiRawImageComponent
							{
								Png = _instance.GetImage(Image),
								Color = ImageColor.Get
							},
							GetPosition()
						}
					});

					list.Add(new CuiElement
					{
						Parent = name,
						Components =
						{
							btn,
							new CuiRectTransformComponent()
						}
					});
				}
				else
				{
					list.Add(new CuiElement
					{
						Name = name,
						Parent = parent,
						DestroyUi = destroyUI,
						Components =
						{
							btn,
							GetPosition()
						}
					});
				}

				if (!string.IsNullOrEmpty(msg))
					list.Add(new CuiElement
					{
						Parent = name,
						Components =
						{
							GetTextComponent(msg),
							new CuiRectTransformComponent()
						}
					});

				return list;
			}
		}

		private class TextSettings : InterfacePosition
		{
			[JsonProperty(PropertyName = LangRu ? "Размер шрифта" : "Font Size")]
			public int FontSize;

			[JsonProperty(PropertyName = LangRu ? "Жирный?" : "Is Bold?")]
			public bool IsBold;

			[JsonProperty(PropertyName = LangRu ? "Выравнивание" : "Align")]
			[JsonConverter(typeof(StringEnumConverter))]
			public TextAnchor Align;

			[JsonProperty(PropertyName = LangRu ? "Цвет" : "Color")]
			public IColor Color;

			public CuiTextComponent GetTextComponent(string msg)
			{
				return new CuiTextComponent
				{
					Text = msg,
					FontSize = FontSize,
					Font = IsBold ? "robotocondensed-bold.ttf" : "robotocondensed-regular.ttf",
					Align = Align,
					Color = Color.Get
				};
			}

			public CuiElement GetText(string msg, string parent, string name = null, string destroyUI = null)
			{
				if (string.IsNullOrEmpty(name))
					name = CuiHelper.GetGuid();

				return new CuiElement
				{
					Name = name,
					Parent = parent,
					DestroyUi = destroyUI,
					Components =
					{
						GetTextComponent(msg),
						GetPosition()
					}
				};
			}
		}

		private class InterfacePosition
		{
			public string AnchorMin;

			public string AnchorMax;

			public string OffsetMin;

			public string OffsetMax;

			public CuiRectTransformComponent GetPosition()
			{
				return new CuiRectTransformComponent
				{
					AnchorMin = AnchorMin,
					AnchorMax = AnchorMax,
					OffsetMin = OffsetMin,
					OffsetMax = OffsetMax
				};
			}
		}

		public class IColor
		{
			[JsonProperty(PropertyName = LangRu ? "Непрозрачность (0 - 100)" : "Opacity (0 - 100)",
				NullValueHandling = NullValueHandling.Include)]
			public float Alpha;

			[JsonProperty(PropertyName = "HEX", NullValueHandling = NullValueHandling.Include)]
			public string Hex;

			public static IColor Create(string hex, float alpha = 100)
			{
				return new IColor
				{
					Hex = hex,
					Alpha = alpha
				};
			}

			[JsonIgnore] private string _color;

			[JsonIgnore]
			public string Get
			{
				get
				{
					if (string.IsNullOrEmpty(_color))
						UpdateColor();

					return _color;
				}
			}

			public void UpdateColor()
			{
				_color = GetColor();
			}

			private string GetColor()
			{
				if (string.IsNullOrEmpty(Hex)) Hex = "#FFFFFF";

				var str = Hex.Trim('#');
				if (str.Length != 6) throw new Exception(Hex);
				var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
				var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
				var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

				return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100}";
			}
		}

		private class InventorySettings
		{
			[JsonProperty(PropertyName =
				LangRu ? "Включить хранение предметов в инвентаре?" : "Enable item storage in inventory?")]
			public bool Enabled;

			[JsonProperty(PropertyName =
				LangRu ? "Сохраняются ли полученные награды при вайпе?" : "Do the items received retain when wiping?")]
			public bool RetainItemsOnWipe;

			[JsonProperty(PropertyName = LangRu ? "Включить задержку после вайпа?" : "Use a cooldown after a wipe?")]
			public bool UseCooldownAfterWipe;

			[JsonProperty(PropertyName = LangRu ? "Задержка после вайпа" : "Cooldown after a wipe")]
			public float CooldownAfterWipe;

			[JsonProperty(PropertyName =
				LangRu ? "Запрещать брать предметы в Building Blocked?" : "Prohibit taking items in Building Blocked?")]
			public bool UseBuildingBlocked;

			[JsonProperty(PropertyName =
				LangRu ? "Запрещать брать предметы при блокировке боя?" : "Prohibit taking items when combat blocked?")]
			public bool UseCombatBlocked;

			[JsonProperty(PropertyName =
				LangRu ? "Запрещать брать предметы при блокировке рейда?" : "Prohibit taking items when raid blocked?")]
			public bool UseRaidBlocked;
		}

		private class CooldownSettings : Switchable
		{
			[JsonProperty(PropertyName =
				LangRu ? "Проверять на АФК? (используя AFKAPI)" : "Checking for AFK? (using AFKAPI)")]
			public bool CheckAFK;

			[JsonProperty(
				PropertyName = LangRu ? "Задержки (разрешение – задержка)" : "Cooldowns (permission – cooldown)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, float> Cooldowns = new();

			[JsonProperty(PropertyName = LangRu ? "Задержка по умолчанию" : "Default Cooldown")]
			public float DefaultCooldown;

			public float GetCooldown(string playerID)
			{
				var cd = DefaultCooldown;

				foreach (var check in Cooldowns)
					if (_instance.permission.UserHasPermission(playerID, check.Key) && check.Value < cd)
						cd = check.Value;

				return cd;
			}
		}

		private class ResetSettings
		{
			[JsonProperty(PropertyName = LangRu ? "Время сброса (hh:mm)" : "Time to reset (hh:mm)")]
			public string Time;

			[JsonProperty(PropertyName = LangRu ? "Временная зона" : "Time Zone")]
			public string TimeZone;

			public TimeSpan GetResetTime()
			{
				var time = TimeSpan.ParseExact(Time, "hh':'mm", CultureInfo.InvariantCulture);

#if TESTING
				SayDebug($"[GetResetTime] {time}");
#endif

				return time;
			}

			public static DateTime GetNowTime()
			{
				return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _instance._pluginTimeZone);
			}
		}


		private class AwardDayInfo : Switchable
		{
			[JsonProperty(PropertyName = LangRu ? "Награды" : "Awards",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<AwardInfo> Awards;

			[JsonProperty(PropertyName = LangRu ? "Изображение" : "Image")]
			public string Image;

			[JsonProperty(PropertyName = LangRu ? "Заголовок" : "Title")]
			public string Title;

			[JsonProperty(PropertyName = LangRu ? "Особый день?" : "Is Special Day?")]
			public bool IsSpecialDay;

			public static AwardDayInfo GetDefault()
			{
				return new AwardDayInfo
				{
					Enabled = false,
					Awards = new List<AwardInfo>(),
					Image = string.Empty,
					Title = string.Empty,
					IsSpecialDay = false
				};
			}

			public string GetTitle()
			{
				if (!string.IsNullOrEmpty(Title))
					return Title;

				if (Awards.Count > 0)
				{
					var award = Awards[0];
					if (award != null && award.Definition != null)
						return award.Definition.category.ToString().ToUpper();
				}

				return string.Empty;
			}

			public ICuiComponent GetImage()
			{
				if (!string.IsNullOrEmpty(Image))
					return new CuiRawImageComponent
					{
						Png = _instance?.GetImage(Image)
					};

				if (Awards.Count > 0)
				{
					var award = Awards[0];
					if (award != null && award.Definition != null)
						return new CuiImageComponent
						{
							ItemId = award.Definition.itemid,
							SkinId = award.Skin
						};
				}

				return new CuiImageComponent
				{
					Color = "0 0 0 0"
				};
			}

			public List<AwardInfo> GetAvailableAwards(BasePlayer player, bool showAll)
			{
				if (showAll)
					return Awards;

				return Awards.FindAll(award =>
					string.IsNullOrEmpty(award.Permission) ||
					_instance.permission.UserHasPermission(player.UserIDString, award.Permission));
			}
		}

		private class AwardInfo
		{
			#region Fields

			[JsonProperty(PropertyName = LangRu ? "Тип" : "Type")] [JsonConverter(typeof(StringEnumConverter))]
			public ItemType Type;

			[JsonProperty(PropertyName = "ID")] public int ID;

			[JsonProperty(PropertyName =
				LangRu ? "Разрешение (прим: dailyrewards.vip)" : "Permission (ex: dailyrewards.vip)")]
			public string Permission = string.Empty;

			[JsonProperty(PropertyName = LangRu ? "Изображение" : "Image")]
			public string Image = string.Empty;

			[JsonProperty(PropertyName = LangRu ? "Команда (%steamid%)" : "Command (%steamid%)")]
			public string Command = string.Empty;

			[JsonProperty(PropertyName = LangRu ? "Название набора" : "Kit Name")]
			public string Kit = string.Empty;

			[JsonProperty(PropertyName = LangRu ? "Настройки плагина" : "Plugin settings")]
			public PluginItem Plugin;

			[JsonProperty(PropertyName =
				LangRu ? "Отображаемое имя (пусто – по умолчанию)" : "DisplayName (empty - default)")]
			public string DisplayName = string.Empty;

			[JsonProperty(PropertyName = "ShortName")]
			public string ShortName = string.Empty;

			[JsonProperty(PropertyName = LangRu ? "Скин" : "Skin")]
			public ulong Skin;

			[JsonProperty(PropertyName = LangRu ? "Это Blueprint?" : "Is Blueprint?")]
			public bool Blueprint;

			[JsonProperty(PropertyName = LangRu ? "Количество" : "Amount")]
			public int Amount;

			[JsonProperty(PropertyName = LangRu ? "Настройка содержимого" : "Content settings")]
			public ItemContent Content;

			[JsonProperty(PropertyName = LangRu ? "Настройка оружия" : "Weapon settings")]
			public ItemWeapon Weapon;

			[JsonProperty(PropertyName =
				LangRu ? "Запрещать разделение предмета на стаки?" : "Prohibit splitting item into stacks?")]
			public bool ProhibitSplit;

			#endregion

			#region Main

			public void Get(BasePlayer player, int count = 1)
			{
				switch (Type)
				{
					case ItemType.Item:
						ToItem(player, count);
						break;
					case ItemType.Command:
						ToCommand(player, count);
						break;
					case ItemType.Plugin:
						ToPlugin(player, count);
						break;
					case ItemType.Kit:
						ToKit(player, count);
						break;
				}
			}


			#region Item

			private void ToItem(BasePlayer player, int count)
			{
				if (Definition == null)
				{
					Debug.LogError($"Error creating item with ShortName '{ShortName}'");
					return;
				}

				if (Blueprint)
				{
					GiveBlueprint(Amount * count, player);
				}
				else
				{
					if (ProhibitSplit)
						GiveItem(Amount * count, player);
					else
						GetStacks(count)?.ForEach(stack => GiveItem(stack, player));
				}
			}

			private void GiveItem(int amount, BasePlayer player)
			{
				var newItem = ItemManager.Create(Definition, amount, Skin);
				if (newItem == null)
				{
					_instance?.PrintError($"Error creating item with ShortName '{ShortName}'");
					return;
				}

				if (!string.IsNullOrEmpty(DisplayName)) newItem.name = DisplayName;

				if (Weapon is {Enabled: true})
					Weapon.Build(newItem);

				if (Content is {Enabled: true})
					Content.Build(newItem);

				player.GiveItem(newItem, BaseEntity.GiveItemReason.PickedUp);
			}

			public List<int> GetStacks(int amount)
			{
				amount *= Amount;

				var maxStack = Definition.stackable;

				var list = new List<int>();

				if (maxStack == 0) maxStack = 1;

				while (amount > maxStack)
				{
					amount -= maxStack;
					list.Add(maxStack);
				}

				list.Add(amount);

				return list;
			}

			#region Blueprint

			private void GiveBlueprint(int count, BasePlayer player)
			{
				for (var i = 0; i < count; i++) GiveBlueprint(player);
			}

			private void GiveBlueprint(BasePlayer player)
			{
				var bp = ItemManager.Create(ItemManager.blueprintBaseDef);
				if (bp == null)
				{
					_instance?.PrintError("Error creating blueprintbase");
					return;
				}

				bp.blueprintTarget = ItemManager.FindItemDefinition(ShortName).itemid;

				if (!string.IsNullOrEmpty(DisplayName)) bp.name = DisplayName;

				player.GiveItem(bp, BaseEntity.GiveItemReason.PickedUp);
			}

			#endregion

			#endregion

			#region Command

			private void ToCommand(BasePlayer player, int count)
			{
				var pos = GetLookPoint(player);

				for (var i = 0; i < count; i++)
				{
					var command = Command.Replace("\n", "|")
						.Replace("%steamid%", player.UserIDString, StringComparison.OrdinalIgnoreCase)
						.Replace("%username%", player.displayName, StringComparison.OrdinalIgnoreCase)
						.Replace("%player.z%", pos.z.ToString(CultureInfo.InvariantCulture),
							StringComparison.OrdinalIgnoreCase)
						.Replace("%player.x%", pos.x.ToString(CultureInfo.InvariantCulture),
							StringComparison.OrdinalIgnoreCase)
						.Replace("%player.y%", pos.y.ToString(CultureInfo.InvariantCulture),
							StringComparison.OrdinalIgnoreCase);

					foreach (var check in command.Split('|'))
						if (check.Contains("chat.say"))
						{
							var args = check.Split(' ');
							player.SendConsoleCommand(
								$"{args[0]}  \" {string.Join(" ", args.ToList().GetRange(1, args.Length - 1))}\" 0");
						}
						else
						{
							_instance?.Server.Command(check);
						}
				}
			}

			private RaycastHit? GetLookHit(BasePlayer player, float maxDistance = 5f)
			{
				return !Physics.Raycast(player.eyes.HeadRay(), out var hitInfo, maxDistance)
					? null
					: hitInfo;
			}

			private Vector3 GetLookPoint(BasePlayer player)
			{
				return GetLookHit(player, 10f)?.point ?? player.ServerPosition;
			}

			#endregion

			#region Kit

			private void ToKit(BasePlayer player, int count)
			{
				if (string.IsNullOrEmpty(Kit)) return;

				for (var i = 0; i < count; i++)
					Interface.Oxide.CallHook("GiveKit", player, Kit);
			}

			#endregion

			#region Plugin

			private void ToPlugin(BasePlayer player, int count)
			{
				Plugin?.Get(player, count);
			}

			#endregion

			#endregion

			#region Classes

			public enum ItemType
			{
				Item = 940820526,
				Command = 539924573,
				Plugin = 1160980268,
				Kit = 1582116888
			}

			public class ItemContent : Switchable
			{
				#region Fields

				[JsonProperty(PropertyName = LangRu ? "Содержимое" : "Contents",
					ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public List<ContentInfo> Contents = new();

				#endregion

				#region Classes

				public class ContentInfo : Switchable
				{
					[JsonProperty(PropertyName = LangRu ? "Количество" : "Amount")]
					public int Amount;

					[JsonProperty(PropertyName = LangRu ? "Состояние" : "Condition")]
					public float Condition;

					[JsonProperty(PropertyName = LangRu ? "Позиция" : "Position")]
					public int Position = -1;

					[JsonProperty(PropertyName = "ShortName")]
					public string ShortName;

					#region Utils

					public void Build(Item item)
					{
						if (!Enabled) return;

						var content = ItemManager.CreateByName(ShortName, Mathf.Max(Amount, 1));
						if (content == null) return;
						content.condition = Condition;
						content.MoveToContainer(item.contents, Position);
					}

					#endregion
				}

				#endregion

				#region Utils

				public void Build(Item item)
				{
					if (!Enabled) return;

					Contents?.ForEach(content => content?.Build(item));
				}

				#endregion
			}

			public class ItemWeapon : Switchable
			{
				#region Utils

				public void Build(Item item)
				{
					if (!Enabled) return;

					var heldEntity = item.GetHeldEntity();
					if (heldEntity != null)
					{
						heldEntity.skinID = item.skin;

						var baseProjectile = heldEntity as BaseProjectile;
						if (baseProjectile != null && !string.IsNullOrEmpty(AmmoType))
						{
							baseProjectile.primaryMagazine.contents = Mathf.Max(AmmoAmount, 0);
							baseProjectile.primaryMagazine.ammoType =
								ItemManager.FindItemDefinition(AmmoType);
						}

						heldEntity.SendNetworkUpdate();
					}
				}

				#endregion

				#region Fields

				[JsonProperty(PropertyName = LangRu ? "Тип боеприпасов" : "Ammo Type")]
				public string AmmoType;

				[JsonProperty(PropertyName = LangRu ? "Количество боеприпасов" : "Ammo Amount")]
				public int AmmoAmount;

				#endregion
			}

			public class PluginItem
			{
				[JsonProperty(PropertyName = LangRu ? "Количество" : "Amount")]
				public int Amount;

				[JsonProperty(PropertyName = LangRu ? "Хук" : "Hook")]
				public string Hook = string.Empty;

				[JsonProperty(PropertyName = LangRu ? "Название плагина" : "Plugin Name")]
				public string Plugin = string.Empty;

				public void Get(BasePlayer player, int count = 1)
				{
					var plug = _instance?.plugins.Find(Plugin);
					if (plug == null)
					{
						_instance?.PrintError($"Plugin '{Plugin}' not found !!! ");
						return;
					}

					switch (Plugin)
					{
						case "Economics":
						{
							plug.Call(Hook, player.UserIDString, (double) Amount * count);
							break;
						}
						default:
						{
							plug.Call(Hook, player.UserIDString, Amount * count);
							break;
						}
					}
				}
			}

			#endregion

			#region Cache

			[JsonIgnore] private ItemDefinition _definition;

			[JsonIgnore]
			public ItemDefinition Definition
			{
				get
				{
					if (_definition == null && !string.IsNullOrEmpty(ShortName))
						_definition = ItemManager.FindItemDefinition(ShortName);

					return _definition;
				}
			}

			#endregion

			#region Constructors

			public static AwardInfo CreateItem(string shortName, int amount = 1, ulong skin = 0UL,
				string displayName = "")
			{
				return new AwardInfo
				{
					Type = ItemType.Item,
					ID = Random.Range(0, int.MaxValue),
					Permission = string.Empty,
					Image = string.Empty,
					Command = string.Empty,
					Kit = string.Empty,
					Plugin = new PluginItem
					{
						Hook = string.Empty,
						Plugin = string.Empty,
						Amount = 0
					},
					DisplayName = displayName ?? string.Empty,
					ShortName = shortName ?? string.Empty,
					Skin = skin,
					Blueprint = false,
					Amount = Mathf.Max(amount, 1),
					Content = new ItemContent
					{
						Enabled = false,
						Contents = new List<ItemContent.ContentInfo>
						{
							new()
							{
								ShortName = string.Empty,
								Condition = 100,
								Amount = 1,
								Position = -1
							}
						}
					},
					Weapon = new ItemWeapon
					{
						Enabled = false,
						AmmoType = string.Empty,
						AmmoAmount = 1
					},
					ProhibitSplit = false
				};
			}

			public static AwardInfo CreateCommand(string command, int amount, string image)
			{
				return new AwardInfo
				{
					Type = ItemType.Command,
					ID = Random.Range(0,
						int.MaxValue),
					Permission = string.Empty,
					Image = image ?? string.Empty,
					Command = command,
					Kit = string.Empty,
					Plugin = new PluginItem
					{
						Hook = string.Empty,
						Plugin = string.Empty,
						Amount = 0
					},
					DisplayName = string.Empty,
					ShortName = string.Empty,
					Skin = 0,
					Blueprint = false,
					Amount = Mathf.Max(amount,
						1),
					Content = new ItemContent
					{
						Enabled = false,
						Contents = new List<ItemContent.ContentInfo>
						{
							new()
							{
								ShortName = string.Empty,
								Condition = 100,
								Amount = 1,
								Position = -1
							}
						}
					},
					Weapon = new ItemWeapon
					{
						Enabled = false,
						AmmoType = string.Empty,
						AmmoAmount = 1
					},
					ProhibitSplit = false
				};
			}

			#endregion
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				_config = Config.ReadObject<Configuration>();
				if (_config == null)
					throw new Exception();

				if (_config.Version < Version)
					UpdateConfigValues();

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
			Config?.WriteObject(_config);
		}

		protected override void LoadDefaultConfig()
		{
			_config = new Configuration();
		}

		private void UpdateConfigValues()
		{
			PrintWarning("Config update detected! Updating config values...");

			if (_config.Version != default)
				if (_config.Version < new VersionNumber(1, 0, 3))
				{
					var checkAFK = Convert.ToBoolean(Config.Get(LangRu ? "Настройка задержки" : "Cooldown Settings",
						LangRu ? "Проверять на АФК? (используя AFKChecker)" : "Checking for AFK? (using AFKChecker)"));

					_config.Cooldown.CheckAFK = checkAFK;
				}

			_config.Version = Version;
			PrintWarning("Config update completed!");
		}

		#endregion

		#region Data

		private Dictionary<ulong, PlayerData> _usersData = new();

		private class PlayerData
		{
			#region Fields

			[JsonProperty(PropertyName = "Player ID")]
			public string PlayerID;

			[JsonProperty(PropertyName = "Last Take")]
			public DateTime LastTake;

			[JsonProperty(PropertyName = "Last Reset")]
			public DateTime LastReset;

			[JsonProperty(PropertyName = "Played Time")]
			public float PlayedTime;

			[JsonProperty(PropertyName = "Taked Days", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public HashSet<int> TakedDays = new();

			[JsonProperty(PropertyName = "Stored Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, int> StoredItems = new();

			#endregion

			#region Helpers

			#region Stored Items

			public List<KeyValuePair<string, AwardInfo>> GetStoredItems(int skip, int take)
			{
				var list = new List<KeyValuePair<string, AwardInfo>>();

				var i = 0;
				foreach (var item in StoredItems)
				{
					if (!_instance.TryFindAwardByID(item.Value, out var awardInfo)) continue;

					if (i >= skip) list.Add(new KeyValuePair<string, AwardInfo>(item.Key, awardInfo));

					i++;

					if (list.Count >= take) break;
				}

				return list;
			}

			public int GetItemID(string guid)
			{
				return StoredItems.GetValueOrDefault(guid);
			}

			public void OnGiveItem(string guid)
			{
				StoredItems?.Remove(guid);
			}

			public void AddItemToStorage(AwardInfo award)
			{
				if (award == null) return;
				
				StoredItems?.TryAdd(CuiHelper.GetGuid(), award.ID);
			}

			#endregion

			#region Cooldown

			public bool HasCooldown()
			{
				var cd = GetCooldown();

				return cd > 0;
			}

			public float GetCooldown()
			{
				var cd = _config.Cooldown.GetCooldown(PlayerID);
				if (cd <= 0)
					return 0;

				return cd - PlayedTime;
			}
			
			public bool TryGetCooldown(out float value)
			{
				var cd = _config.Cooldown.GetCooldown(PlayerID);
				if (cd <= 0)
				{
					value = 0;
					return false;
				}

				value = cd - PlayedTime;
				return value > 0;
			}

			#endregion

			#region Take Day
			
			public void OnTake(int day)
			{
#if TESTING
				SayDebug($"[OnTake.{PlayerID}] day={day}");
#endif

				LastTake = DateTime.UtcNow;

				TakedDays.Add(day);

#if TESTING
				SayDebug($"[OnTake.{PlayerID}] TakedDays={string.Join(", ", TakedDays)}");
#endif
				
				StartReset();
			}

			public bool IsTaked(int day)
			{
				return TakedDays.Contains(day);
			}

			public bool CanTake(ulong player, int day)
			{
				if (IsTaked(day)) return false;

				var awards = _instance.GetAvailabeAwards(player);

				int nextDayToTake;
				if (TakedDays.Count == 0)
				{
					nextDayToTake = awards.MinKey();
				}
				else
				{
					var lastTakedDay = TakedDays.Append(0).Max();

					nextDayToTake = awards.MinKeyBy(x => x > lastTakedDay);
				}

				return nextDayToTake == day && CanTakeNextDay(player);
			}

			#endregion

			#region Next Day

			public bool HasNextDay(BasePlayer player)
			{
				var lastTakedDay = TakedDays.Append(0).Max();
				var nextDayToTake = _instance.GetAvailabeAwards(player.userID).MinKeyBy(day => day > lastTakedDay);

				var daysToAwait = Mathf.Max(nextDayToTake - lastTakedDay, 0);

				return daysToAwait > 0;
			}

			public bool CanTakeNextDay(ulong player)
			{
				if (LastTake == default)
					return true;

				var currentTime = ResetSettings.GetNowTime();

				var lastTakedDay = TakedDays.Count > 0 ? TakedDays.Max() : 0;

				var availableAwardDays = _instance.GetAvailabeAwards(player).Select(x => x.day).Where(day => day > lastTakedDay).ToList();
				if (availableAwardDays.Count == 0) return false;
			
				var nextDayToTake = availableAwardDays.Min();
				if (nextDayToTake <= 0) return false;
			
				var daysToAwait = Mathf.Max(nextDayToTake - lastTakedDay, 0);
				if (daysToAwait <= 0) return false;

				var minTimeToTake = LastTake.Date.AddDays(daysToAwait)
					.AddSeconds(_config.Reset.GetResetTime().TotalSeconds);

				return currentTime > minTimeToTake;
			}

			#endregion

			#region Reset

			public void StartReset()
			{
#if TESTING
				SayDebug("[StartReset] init");
#endif
				LastReset = DateTime.UtcNow;

				PlayedTime = 0f;
			}

			public void TryResetTime(ulong player)
			{
#if TESTING
				SayDebug($"[TryResetTime.{player}] init");
#endif

				if (LastReset == default)
				{
#if TESTING
					SayDebug(
						$"[TryResetTime.{player}] last reset not set: {LastReset} (is default). Start reset!");
#endif

					StartReset();
					return;
				}

				var canTakeNextDay = CanTakeNextDay(player);
				var hasCooldown = HasCooldown();

#if TESTING
				SayDebug($"[TryResetTime.{player}] canTakeNextDay={canTakeNextDay} hasCooldown={hasCooldown}");
#endif

				if (canTakeNextDay && hasCooldown)
				{
#if TESTING
					SayDebug($"[TryResetTime.{player}] cann't take next day");
#endif

					return;
				}

				var now = ResetSettings.GetNowTime();

				var resetTime = _config.Reset.GetResetTime();

				var dayToReset = LastReset.Date.AddDays(1).AddSeconds(resetTime.TotalSeconds);

#if TESTING
				SayDebug($"[TryResetTime.{player}] dayToReset={dayToReset}, now={now}, resetTime={resetTime}");
#endif

				if (now > dayToReset)
				{
#if TESTING
					SayDebug($"[TryResetTime.{player}] reset");
#endif
					StartReset();
				}
			}

			#endregion
			
			#endregion

			#region Storage

			private static string BaseFolder()
			{
				return "DailyRewards" + Path.DirectorySeparatorChar + "Players" + Path.DirectorySeparatorChar;
			}

			public static PlayerData GetOrLoad(ulong userId)
			{
				if (!userId.IsSteamId()) return null;

				return GetOrLoad(BaseFolder(), userId);
			}

			public static PlayerData GetNotLoad(ulong userId)
			{
				if (!userId.IsSteamId()) return null;

				var data = GetOrLoad(BaseFolder(), userId, false);

				return data;
			}


			public static PlayerData GetOrLoad(string baseFolder, ulong userId, bool load = true)
			{
				if (_instance._usersData.TryGetValue(userId, out var data)) return data;

				try
				{
					data = ReadOnlyObject(baseFolder + userId);
				}
				catch (Exception e)
				{
					Interface.Oxide.LogError(e.ToString());
				}

				return load
					? _instance._usersData[userId] = data
					: data;
			}

			public static PlayerData GetOrCreate(ulong userId)
			{
				if (!userId.IsSteamId()) return null;

				return GetOrLoad(userId) ?? (_instance._usersData[userId] = new PlayerData
				{
					PlayerID = userId.ToString()
				});
			}

			public static bool IsLoaded(ulong userId)
			{
				return _instance._usersData.ContainsKey(userId);
			}

			public static void Save()
			{
				_instance?._usersData?.Keys.ToList().ForEach(Save);
			}

			public static void Save(ulong userId)
			{
				if (!_instance._usersData.TryGetValue(userId, out var data))
					return;

				Interface.Oxide.DataFileSystem.WriteObject(BaseFolder() + userId, data);
			}

			public static void SaveAndUnload(ulong userId)
			{
				Save(userId);

				Unload(userId);
			}

			public static void Unload(ulong userId)
			{
				_instance._usersData.Remove(userId);
			}

			#region Helpers

			public static string[] GetFiles()
			{
				return GetFiles(BaseFolder());
			}

			public static string[] GetFiles(string baseFolder)
			{
				try
				{
					var json = ".json".Length;
					var paths = Interface.Oxide.DataFileSystem.GetFiles(baseFolder);
					for (var i = 0; i < paths.Length; i++)
					{
						var path = paths[i];
						var separatorIndex = path.LastIndexOf(Path.DirectorySeparatorChar);

						// We have to do this since GetFiles returns paths instead of filenames
						// And other methods require filenames
						paths[i] = path.Substring(separatorIndex + 1, path.Length - separatorIndex - 1 - json);
					}

					return paths;
				}
				catch
				{
					return Array.Empty<string>();
				}
			}

			private static PlayerData ReadOnlyObject(string name)
			{
				return Interface.Oxide.DataFileSystem.ExistsDatafile(name)
					? Interface.Oxide.DataFileSystem.GetFile(name).ReadObject<PlayerData>()
					: null;
			}

			public static void DoWipe(string userId)
			{
				var playerUserID = ulong.Parse(userId);
				if (!playerUserID.IsSteamId()) return;

				_instance?._usersData?.Remove(playerUserID);

				if (_config.Inventory.Enabled && _config.Inventory.RetainItemsOnWipe)
				{
					var data = GetNotLoad(playerUserID);
					if (data == null) return;

					if (data.StoredItems.Count > 0)
					{
						var newData = new PlayerData
						{
							PlayerID = data.PlayerID,
							StoredItems = data.StoredItems
						};

						Interface.Oxide.DataFileSystem.WriteObject(BaseFolder() + userId, newData);
					}
					else
					{
						Interface.Oxide.DataFileSystem.DeleteDataFile(BaseFolder() + userId);
					}
				}
				else
				{
					Interface.Oxide.DataFileSystem.DeleteDataFile(BaseFolder() + userId);
				}
			}

			#endregion

			#endregion
		}

		#endregion

		#region Hooks

		private void Init()
		{
			_instance = this;
		}

		private void OnServerInitialized()
		{
			LoadImages();

			LoadAwardIDs();

			LoadTimeZone();

			if (_config.Cooldown.Enabled)
				InitPlayTimeEngine();

			RegisterCommands();

			RegisterPermissions();

			LoadPlayers();
			
#if EDITING_MODE
			LoadItems();
#endif
		}

		private void Unload()
		{
			DestroyPlayTimeEngine();

			StopCoroutines();

			foreach (var player in BasePlayer.activePlayerList)
			{
				CuiHelper.DestroyUi(player, Layer);
				CuiHelper.DestroyUi(player, ModalLayer);
				CuiHelper.DestroyUi(player, SecondModalLayer);
				CuiHelper.DestroyUi(player, SelectItemModalLayer);
				CuiHelper.DestroyUi(player, ModalLayer + ".Show.Preview");

				CuiHelper.DestroyUi(player, SecondModalLayer + ".Array");
				CuiHelper.DestroyUi(player, SecondModalLayer + ".Object");

				PlayerData.SaveAndUnload(player.userID);
			}

			_instance = null;
			_config = null;
		}

		private void OnPlayerConnected(BasePlayer player)
		{
			if (player == null || !player.userID.IsSteamId()) return;

			var data = _config.Cooldown.Enabled
				? PlayerData.GetOrCreate(player.userID)
				: PlayerData.GetOrLoad(player.userID);
			if (data != null)
			{
#if TESTING
				SayDebug($"[OnPlayerConnected.{player.UserIDString}] call TryResetTime");
#endif

				data.TryResetTime(player.userID);

#if TESTING
				SayDebug($"[OnPlayerConnected.{player.UserIDString}] call TryResetTime done");
#endif

				if (_config.Cooldown.Enabled && data.HasCooldown())
				{
#if TESTING
					SayDebug($"[OnPlayerConnected.{player.UserIDString}] cooldown handler inserted");
#endif
					_playTimeEngine.Insert(player.userID);
				}

				if (_config.Notifications.OnCooldownEnded.ShowOnPlayerConnected)
					if (data.CanTakeNextDay(player.userID) && !data.HasCooldown())
					{
#if TESTING
						SayDebug($"[OnPlayerConnected.{player.UserIDString}] send notification OnCooldownEnded");
#endif
						_config.Notifications.OnCooldownEnded.Show(player);
					}

				if (_config.Notifications.HasCooldown.ShowOnPlayerConnected)
					if (data.HasNextDay(player) && data.HasCooldown())
					{
#if TESTING
						SayDebug($"[OnPlayerConnected.{player.UserIDString}] send notification HasCooldown");
#endif
						_config.Notifications.HasCooldown.Show(player,
							TimeSpan.FromSeconds(data.GetCooldown()).ToShortString());
					}

#if TESTING
				SayDebug($"[OnPlayerConnected.{player.UserIDString}] call TryResetTime done");
#endif
			}

			if (_config.ShowUIWhenPlayerConnected && (string.IsNullOrEmpty(_config.Permission) ||
			                                          permission.UserHasPermission(player.UserIDString,
				                                          _config.Permission)))
			{
#if TESTING
				SayDebug($"[OnPlayerConnected.{player.UserIDString}] call ShowUI");
#endif

				NextTick(() => OpenAwardsUI(player));
			}
		}

		private void OnPlayerDisconnected(BasePlayer player)
		{
			if (player == null || !player.userID.IsSteamId()) return;

            if (_config.Cooldown.Enabled)
				_playTimeEngine.Remove(player.userID);

			PlayerData.SaveAndUnload(player.userID);

			RemoveOpenedRewardPlayer(player.userID);

#if EDITING_MODE
			RemoveEditing(player);
#endif
		}

		#region Wipe

		private void OnNewSave(string filename)
		{
			if (_config.WipeOnNewSave)
				StartWipePlayers();
		}

		#endregion

		#region Image Library

#if !CARBON
		private void OnPluginLoaded(Plugin plugin)
		{
			if (plugin.Name == "ImageLibrary")
			{
				timer.In(1, LoadImages);
			}
		}

		private void OnPluginUnloaded(Plugin plugin)
		{
			if (plugin.Name == "ImageLibrary") _enabledImageLibrary = false;
		}
#endif

		#endregion

		#region Server Panel

		private void OnServerPanelCategoryPage(BasePlayer player, int category, int page)
		{
			RemoveOpenedRewardPlayer(player.userID);
		}

		private void OnServerPanelClosed(BasePlayer player)
		{
			RemoveOpenedRewardPlayer(player.userID);
		}

		#endregion
        
		#endregion

		#region Commands

		private void CmdOpenRewards(IPlayer cov, string command, string[] args)
		{
			var player = cov?.Object as BasePlayer;
			if (player == null) return;

			if (!string.IsNullOrWhiteSpace(_config.Permission) &&
			    !permission.UserHasPermission(player.UserIDString, _config.Permission))
			{
				SendNotify(player, NoPermissions, 1);
				return;
			}

			if (IsOpenedRewardPlayer(player.userID))
			{
				UpdateUI(player, container =>
				{
					AwardsContentUI(player, ref container);
						
					AwardsProgressUI(player, ref container);
				});
			}
			else
				OpenAwardsUI(player);
		}

		[ConsoleCommand(CMD_Main_Console)]
		private void CmdRewardsConsole(ConsoleSystem.Arg arg)
		{
			var player = arg?.Player();
			if (player == null || !arg.HasArgs()) return;

			switch (arg.Args[0])
			{
				case "close_preview":
				{
					DestroyEditingTimer(player.userID);
					break;
				}

				case "close":
				{
					RemoveOpenedRewardPlayer(player.userID);
					break;
				}

				case "page":
				{
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out var page)) return;

					GetOpenedRewardPlayer(player.userID)?.OnChangePage(page);

					UpdateUI(player, container => AwardsContentUI(player, ref container));
					break;
				}

				case "take_day":
				{
					if (!arg.HasArgs(2) ||
					    !int.TryParse(arg.Args[1], out var day)) return;

					var data = PlayerData.GetOrCreate(player.userID);
					if (data == null ||
					    !data.CanTake(player.userID, day) ||
					    (_config.Cooldown.Enabled && data.GetCooldown() > 0))
						return;

					var dailyAward = _config.GetDailyAwards(day);
					if (dailyAward == null)
						return;

					data.OnTake(day);

					if (_config.Inventory.Enabled)
						dailyAward.GetAvailableAwards(player, false)?.ForEach(award => data?.AddItemToStorage(award));
					else
						NextTick(() => dailyAward?.GetAvailableAwards(player, false)?.ForEach(award => award?.Get(player)));

					PlayerData.Save(player.userID);
					
					UpdateUI(player, container =>
					{
						AwardsContentUI(player, ref container);
						
						AwardsProgressUI(player, ref container);
					});
					
					if (_config.Notifications.PickedUpDailyReward.ShowOnPlayerPickedUpReward)
						_config.Notifications.PickedUpDailyReward.Show(player);
					break;
				}

				case "show_info":
				{
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out var day)) return;

					var page = arg.GetInt(2);

					AwardInfoUI(player, day, page);
					break;
				}

				case "inventory":
				{
					switch (arg.Args[1])
					{
						case "open":
						{
							GetOpenedRewardPlayer(player.userID)?.TryDestroyTimer();

							InventoryUI(player);
							break;
						}

						case "back":
						{
							AwardsUi(player);
							break;
						}

						case "page":
						{
							if (!arg.HasArgs(3) ||
							    !int.TryParse(arg.Args[2], out var page)) return;

							InventoryUI(player, page);
							break;
						}

						case "give":
						{
							if (!arg.HasArgs(4) ||
							    !int.TryParse(arg.Args[3], out var index)) return;

							var itemGuid = arg.Args[2];
							if (string.IsNullOrEmpty(itemGuid)) return;

							if (_config.Inventory.UseCooldownAfterWipe)
							{
								var fromWipe = WipeBlock?.IsLoaded == true
									? GetSecondsFromWipeFromWipeBlock()
									: GetSecondsFromWipe();

								var leftTime = _config.Inventory.CooldownAfterWipe - fromWipe;
								if (leftTime > 0)
								{
									ShowInventoryItemStatus(player, index, false);

									SendNotify(player, CantTakeItemWipeBlock, 1, leftTime);
									return;
								}
							}

							if (_config.Inventory.UseBuildingBlocked &&
							    !player.CanBuild())
							{
								ShowInventoryItemStatus(player, index, false);

								SendNotify(player, CantTakeItemBuildingBlocked, 1);
								return;
							}

							if (_config.Inventory.UseCombatBlocked && NE_IsCombatBlocked(player))
							{
								ShowInventoryItemStatus(player, index, false);

								SendNotify(player, CantTakeItemCombatBlocked, 1);
								return;
							}

							if (_config.Inventory.UseRaidBlocked && NE_IsRaidBlocked(player))
							{
								SendNotify(player, CantTakeItemRaidBlocked, 1);
								return;
							}

							var data = PlayerData.GetOrLoad(player.userID);

							var itemID = data.GetItemID(itemGuid);
							if (itemID == default)
							{
								ShowInventoryItemStatus(player, index, false);
								return;
							}

							var award = FindAwardByID(itemID);
							if (award == null)
							{
								ShowInventoryItemStatus(player, index, false);
								return;
							}

							award.Get(player);

							data.OnGiveItem(itemGuid);

							PlayerData.Save(player.userID);

							ShowInventoryItemStatus(player, index, true);
							break;
						}

						case "close":
						{
							RemoveOpenedRewardPlayer(player.userID);
							break;
						}
					}

					break;
				}

#if EDITING_MODE
				default:
				{
					if (!permission.UserHasPermission(player.UserIDString, PERM_EDIT))
						return;

					switch (arg.Args[0])
					{
						case "confirm":
						{
							if (!arg.HasArgs(2)) return;

							var action = arg.Args[1];
							switch (action)
							{
								case "day_remove":
								{
									if (!arg.HasArgs(5)) return;

									if (!int.TryParse(arg.Args[3], out var mainPage) ||
									    !int.TryParse(arg.Args[4], out var dayID))
										return;

									switch (arg.Args[2])
									{
										case "open":
										{
											ConfirmActionUI(player, action, mainPage.ToString(), dayID.ToString());
											break;
										}

										case "cancel":
										{
											// ignore
											break;
										}

										case "accept":
										{
											_config.DailyAwards.Remove(dayID);

											SaveConfig();

											var targetUI = GetOpenedRewardPlayer(player.userID).GetUI();

											var maxPage = Mathf.Max(
													Mathf.CeilToInt(
														(float) CountAvailabeAwards(player) / targetUI.AwardsOnLine) - 1,
													0);
											
											if (maxPage != mainPage)
												GetOpenedRewardPlayer(player.userID)?.OnChangePage(mainPage - 1);
											
											AwardsUi(player);
											break;
										}
									}

									break;
								}
							}

							break;
						}

						case "day_add":
						{
							if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out var day)) return;

							_config.DailyAwards.TryAdd(day, AwardDayInfo.GetDefault());

							SaveConfig();
							
							var openedRewardPlayer = GetOpenedRewardPlayer(player.userID);
							if (openedRewardPlayer != null)
							{
								var targetUI = openedRewardPlayer.GetUI();

								var pages = Mathf.CeilToInt((float) CountAvailabeAwards(player) / targetUI.AwardsOnLine);

								var targetPage = Mathf.Max(pages - 1, 0);

								openedRewardPlayer?.OnChangePage(targetPage);
							}
							
							AwardsUi(player);

							StartEditDay(player, day);
							break;
						}

						case "edit_item":
						{
							if (!arg.HasArgs(2)) return;

							StartEditAction(arg.Args[1], arg.Args, player, GetAwardEditing(player), args =>
							{
								if (!args.HasLength(4) ||
								    !int.TryParse(args[2], out var day) ||
								    !int.TryParse(args[3], out var awardID)) return;

								var page = 0;
								if (args.HasLength(5))
									int.TryParse(args[4], out page);

								StartEditAward(player, day, awardID, page);
							});
							break;
						}

						case "edit_day":
						{
							if (!arg.HasArgs(2)) return;

							StartEditAction(arg.Args[1], arg.Args, player, GetDayEditing(player), args =>
							{
								if (!args.HasLength(3) ||
								    !int.TryParse(args[2], out var day)) return;

								StartEditDay(player, day);
							});
							break;
						}

						case "edit_config":
						{
							if (!arg.HasArgs(2)) return;

							switch (arg.Args[1])
							{
								default:
								{
									StartEditAction(arg.Args[1], arg.Args, player, GetConfigEditing(player),
										args => { StartEditConfig(player); });
									break;
								}
							}

							break;
						}
					}

					break;
				}
#endif
			}
		}

		[ConsoleCommand("dailyrewards.wipe")]
		private void CmdRewardsWipeConsole(ConsoleSystem.Arg arg)
		{
			if (!arg.IsServerside) return;

			StartWipePlayers();
		}

		[ConsoleCommand("dailyrewards.manage")]
		private void CmdRewardsManageConsole(ConsoleSystem.Arg arg)
		{
			if (!arg.IsServerside) return;

			switch (arg.GetString(0))
			{
				case "playtime":
				{
					var targetID = arg.GetString(2);
					var targetPlayer = BasePlayer.FindAwakeOrSleeping(targetID);
					if (targetPlayer == null)
					{
						SendReply(arg, "Player not found");
						return;
					}

					switch (arg.GetString(1))
					{
						case "set":
						{
							var amount = arg.GetFloat(3);
							
							var data = PlayerData.GetOrCreate(targetPlayer.userID);
							if (data == null)
							{
								SendReply(arg, "Player not found");
								return;
							}

							data.PlayedTime = amount;
							
							PlayerData.Save(targetPlayer.userID);
							
							SendReply(arg, $"Player {targetPlayer.displayName} playtime set to {amount}");
							break;
						}
					}
					
					break;
				}
				
				default:
				{
					var sb = new StringBuilder();
					sb.AppendLine($"Daily Rewards Commands:");
					sb.AppendLine($"{arg.cmd.FullName} playtime set <steam_id> <amount> – set player playtime");
					
					SendReply(arg, sb.ToString());
					break;
				}
			}
		}

		[ConsoleCommand("dailyrewards.top")]
		private void CmdRewardsTopPlayersConsole(ConsoleSystem.Arg arg)
		{
			if (!arg.IsServerside) return;

			var format = arg.GetString(0);
			var limit = arg.GetInt(1, 10);

			StopTopPlayersCoroutine();

			switch (format)
			{
				case "csv":
				{
					var csv = new StringBuilder();

					csv.AppendLine("Top Players:");

					StartTopPlayers(players =>
					{
						csv.AppendLine("Display Name,Steam ID,Max Day");

						foreach (var topPlayer in players)
							csv.AppendLine($"{topPlayer.DisplayName},{topPlayer.PlayerID},{topPlayer.MaxTakedDay}");

						SendReply(arg, csv.ToString());
					}, limit);
					break;
				}

				default:
				{
					var sb = new StringBuilder();

					sb.AppendLine("Top Players:");

					StartTopPlayers(players =>
					{
						foreach (var topPlayer in players)
							sb.AppendLine(
								$"\"{topPlayer.DisplayName}\" ({topPlayer.PlayerID}) has max day = {topPlayer.MaxTakedDay}");

						SendReply(arg, sb.ToString());
					}, limit);
					break;
				}
			}
		}

		[ConsoleCommand("dailyrewards.template")]
		private void CmdRewardsSetTemplate(ConsoleSystem.Arg arg)
		{
			if (!arg.IsServerside) return;

			var format = arg.GetString(0);
			
			if (!arg.HasArgs(1) || format != "fullscreen" && format!= "inmenu")
			{
				SendReply(arg, $"Error syntax! Use: {arg.cmd.FullName} [fullscreen/inmenu] [target_template]");
				return;
			}

			InterfaceSettings targetTemplate = null;
			switch (format)
			{
				case "fullscreen":
				{
					targetTemplate = InterfaceSettings.GenerateFullscreenTemplate();
					break;
				}
				case "inmenu":
				{
					switch (arg.GetString(1))
					{
						case "1":
						{
							targetTemplate = InterfaceSettings.GenerateMenuTemplateV1();
							break;
						}

						case "2":
						{
							targetTemplate = InterfaceSettings.GenerateMenuTemplateV2();
							break;
						}

						default:
						{
							SendReply(arg,
								$"Error syntax! Use: {arg.cmd.FullName} [fullscreen/inmenu] [1/2]");
							return;
						}
					}

					break;
				}
				
				default:
				{
					SendReply(arg, $"Error syntax! Use: {arg.cmd.FullName} [fullscreen/inmenu]");
					return;
				}
			}

			if (targetTemplate == null)
			{
				SendReply(arg, $"Error syntax! Use: {arg.cmd.FullName} [fullscreen/inmenu]");
				return;
			}

			if (format == "fullscreen")
				_config.UI = targetTemplate;
			else
				_config.MenuUI = targetTemplate;
			
			SaveConfig();

			SendReply(arg, $"'{format}'  UI successfully set!");
		}

		#endregion

		#region Interface

		private void OpenAwardsUI(BasePlayer player)
		{
			GetOrCreateOpenedRewardPlayer(player);

			AwardsUi(player, first: true);
		}

		private void AwardsUi(BasePlayer player, bool first = false)
		{
			#region Fields

			var container = new CuiElementContainer();
			
			#endregion

			#region Background

			if (first)
				container.Add(_config.UI.Background.Get(Layer, Layer, true));

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image =
				{
					Color = "0 0 0 0"
				}
			}, Layer, MainLayer, MainLayer);

			#endregion

			AwardsHeaderUI(player, container);

			AwardsContentUI(player, ref container);

			AwardsProgressUI(player, ref container);

			CuiHelper.AddUi(player, container);
		}

		private void AwardsProgressUI(BasePlayer player, ref CuiElementContainer container)
		{			
			var openedReward = GetOpenedRewardPlayer(player.userID);
			if (openedReward == null) return;
		
			var targetUI = openedReward.GetUI();

			var awards = openedReward.availableAwards;

			var data = PlayerData.GetOrCreate(player.userID);

			#region Progress

			#region Bar

			container.Add(targetUI.ProgressBarBackground.GetImage(MainLayer, MainLayer + ".Progress", MainLayer + ".Progress"));

			var progress = Math.Round((float) data.TakedDays.Append(0).Max() / awards.Count, 2);
			if (progress > 0.0)
			{
				var isFull = targetUI.UseFullyFilledProgressBarColor && Math.Abs(progress - 1f) < 0.05;

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0",
						AnchorMax = $"{Math.Min(progress, 1)} 1"
					},
					Image =
					{
						Color =
							isFull ? targetUI.FullyFilledProgressBarColor.Get : targetUI.ProgressBarColor.Get
					}
				}, MainLayer + ".Progress");
			}

			#endregion

			#region Cooldown

			if (_config.Cooldown.Enabled)
			{
				container.Add(targetUI.CooldownPanel.GetImage(MainLayer, MainLayer + ".Cooldown"));

				container.Add(targetUI.CooldownTitle.GetText(Msg(player, PluginLeftCooldownTitle),
					MainLayer + ".Cooldown"));

				UpdateCooldownUI(player, data, ref container);
			}

			#endregion

			#region Left Days

			container.Add(targetUI.LeftDaysPanel.GetImage(MainLayer, MainLayer + ".LeftDays"));

			container.Add(targetUI.LeftDaysTitle.GetText(Msg(player, PluginDaysPassedTitle),
				MainLayer + ".LeftDays"));

			container.Add(targetUI.LeftDaysAmount.GetText($"{data.TakedDays.Append(0).Max()}",
				MainLayer + ".LeftDays", MainLayer + ".LeftDays.Value"));

			#endregion

			#endregion
		}

		private void AwardsContentUI(BasePlayer player, ref CuiElementContainer container)
		{
			var openedReward = GetOpenedRewardPlayer(player.userID);
			if (openedReward == null) return;
		
			var targetUI = openedReward.GetUI();

			var awards = openedReward.availableAwards;

			var data = PlayerData.GetOrCreate(player.userID);

			container.Add(targetUI.ContentPanel.GetImage(MainLayer, MainLayer + ".Content", MainLayer + ".Content"));
			
			#region Awards

			if (awards.Count > 0)
			{
				var constSwitchX = -(Mathf.Min(awards.Count, targetUI.AwardsOnLine) * targetUI.AwardWidth +
				                     (Mathf.Min(awards.Count, targetUI.AwardsOnLine) - 1) * targetUI.AwardIndentX) / 2f;

				var xSwitch = constSwitchX;
				var ySwitch = -targetUI.UpIndent;
				
				foreach (var awardDay in awards.SkipAndTake(openedReward.currentPage * targetUI.AwardsOnLine, targetUI.AwardsOnLine))
				{
					container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
								OffsetMin = $"{xSwitch} {ySwitch - targetUI.AwardHeight}",
								OffsetMax = $"{xSwitch + targetUI.AwardWidth} {ySwitch}"
							},
							Image =
							{
								Color = "0 0 0 0"
							}
						}, MainLayer + ".Content", MainLayer + $".Item.{awardDay.day}", MainLayer + $".Item.{awardDay.day}");

					container.Add(new CuiPanel
						{
							RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
							Image =
							{
								Color = "0 0 0 0"
							}
						}, MainLayer + $".Item.{awardDay.day}",
						MainLayer + $".Item.{awardDay.day}.Background",
						MainLayer + $".Item.{awardDay.day}.Background");

					ShowAwardDayUI(player, data, awardDay.day, awardDay.dayInfo, ref container);

					#region Admin Functions

					if (permission.UserHasPermission(player.UserIDString, PERM_EDIT))
					{
						if (_config.DailyAwards.ContainsKey(awardDay.day))
						{
							#region Edit

							container.Add(new CuiPanel
								{
									RectTransform =
									{
										AnchorMin = "0 1", AnchorMax = "0 1",
										OffsetMin = "10 10",
										OffsetMax = "35 35"
									},
									Image =
									{
										Color = HexToCuiColor("#519229")
									}
								}, MainLayer + $".Item.{awardDay.day}", MainLayer + $".Item.{awardDay.day}.Edit");

							container.Add(new CuiElement
							{
								Name = MainLayer + $".Item.{awardDay.day}.Edit.Image",
								Parent = MainLayer + $".Item.{awardDay.day}.Edit",
								Components =
								{
									new CuiRawImageComponent
									{
										Png = GetImage(targetUI.EditImage)
									},
									new CuiRectTransformComponent
									{
										AnchorMin = "0 0",
										AnchorMax = "1 1",
										OffsetMin = "4 4",
										OffsetMax = "-4 -4"
									}
								}
							});

							container.Add(new CuiButton
								{
									RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
									Text = {Text = string.Empty},
									Button =
									{
										Color = "0 0 0 0",
										Command = $"{CMD_Main_Console} edit_day start {awardDay.day}"
									}
								}, MainLayer + $".Item.{awardDay.day}.Edit",
								MainLayer + $".Item.{awardDay.day}.Edit.Btn");

							#endregion

							#region Remove

							container.Add(new CuiPanel
								{
									RectTransform =
									{
										AnchorMin = "0 1", AnchorMax = "0 1",
										OffsetMin = "40 10",
										OffsetMax = "65 35"
									},
									Image =
									{
										Color = HexToCuiColor("#EF5125")
									}
								}, MainLayer + $".Item.{awardDay.day}", MainLayer + $".Item.{awardDay.day}.Remove");

							container.Add(new CuiPanel
								{
									RectTransform =
									{
										AnchorMin = "0 0", AnchorMax = "1 1",
										OffsetMin = "2 2", OffsetMax = "-2 -2"
									},
									Image =
									{
										Sprite = "assets/icons/clear.png"
									}
								}, MainLayer + $".Item.{awardDay.day}.Remove");

							container.Add(new CuiButton
								{
									RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
									Text = {Text = string.Empty},
									Button =
									{
										Color = "0 0 0 0",
										Command = $"{CMD_Main_Console} confirm day_remove open {openedReward.currentPage} {awardDay.day}"
									}
								}, MainLayer + $".Item.{awardDay.day}.Remove",
								MainLayer + $".Item.{awardDay.day}.Remove.Btn");

							#endregion

							#region Add

							if (awards.Max(x => x.day) == awardDay.day ||
							    !_config.DailyAwards.ContainsKey(awardDay.day + 1))
								container.Add(new CuiButton
									{
										RectTransform =
										{
											AnchorMin = "1 1", AnchorMax = "1 1",
											OffsetMin = "-40 10",
											OffsetMax = "0 50"
										},
										Text =
										{
											Text = Msg(player, EditingBtnADD),
											Align = TextAnchor.MiddleCenter,
											Font = "robotocondensed-regular.ttf",
											FontSize = 24,
											Color = HexToCuiColor("#FFFFFF", 90)
										},
										Button =
										{
											Color = HexToCuiColor("#228BA1"),
											Close = MainLayer + $".Item.{awardDay.day}.Add",
											Command = $"{CMD_Main_Console} day_add {awardDay.day + 1}"
										}
									}, MainLayer + $".Item.{awardDay.day}", MainLayer + $".Item.{awardDay.day}.Add");

							#endregion
						}
						else
						{
							#region Add

							container.Add(new CuiButton
								{
									RectTransform =
									{
										AnchorMin = "0 1", AnchorMax = "0 1",
										OffsetMin = "10 10",
										OffsetMax = "35 35"
									},
									Text =
									{
										Text = Msg(player, EditingBtnADD),
										Align = TextAnchor.MiddleCenter,
										Font = "robotocondensed-regular.ttf",
										FontSize = 18,
										Color = HexToCuiColor("#FFFFFF", 90)
									},
									Button =
									{
										Color = HexToCuiColor("#228BA1"),
										Close = MainLayer + $".Item.{awardDay.day}.Add",
										Command = $"{CMD_Main_Console} edit_day start {awardDay.day}"
									}
								}, MainLayer + $".Item.{awardDay.day}", MainLayer + $".Item.{awardDay.day}.Add");

							#endregion
						}
					}

					#endregion

					xSwitch += targetUI.AwardWidth + targetUI.AwardIndentX;
				}
			}
			else
			{
				if (permission.UserHasPermission(player.UserIDString, PERM_EDIT))
					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
							OffsetMin = "-105 -15",
							OffsetMax = "105 15"
						},
						Text =
						{
							Text = Msg(player, EditingBtnADDFirstAward),
							FontSize = 14,
							Font = "robotocondensed-bold.ttf",
							Align = TextAnchor.MiddleCenter,
							Color = IColor.Create("#FFFFFF", 90).Get
						},
						Button =
						{
							Color = IColor.Create("#EF5125").Get,
							Command = $"{CMD_Main_Console} day_add 1",
							Close = MainLayer + ".BTN.Add.First.Award"
						}
					}, MainLayer + ".Content", MainLayer + ".BTN.Add.First.Award");
			}

			#endregion
			
			#region Pages

			if (awards.Count > targetUI.AwardsOnLine)
			{
				var hasBackPage = openedReward.currentPage != 0;

				var hasNextPage = awards.Count > (openedReward.currentPage + 1) * targetUI.AwardsOnLine;

				#region Back

				container.AddRange((hasBackPage
					? targetUI.PageButtonBackWhenActive
					: targetUI.PageButtonBackWhenInactive).Get(string.Empty,
					hasBackPage ? $"{CMD_Main_Console} page {openedReward.currentPage - 1}" : string.Empty,
					MainLayer + ".Content",
					MainLayer + ".Btn.Back",
					MainLayer + ".Btn.Back"));

				#endregion

				#region Next

				container.AddRange((hasNextPage
					? targetUI.PageButtonNextWhenActive
					: targetUI.PageButtonNextWhenInactive).Get(string.Empty,
					hasNextPage ? $"{CMD_Main_Console} page {openedReward.currentPage + 1}" : "",
					MainLayer + ".Content",
					MainLayer + ".Btn.Next",
					MainLayer + ".Btn.Next"));

				#endregion
			}

			#endregion
		}

		private void AwardsHeaderUI(BasePlayer player, CuiElementContainer container)
		{
			var openedRewardPlayer = GetOpenedRewardPlayer(player.userID);
			var targetUI = openedRewardPlayer.GetUI();

			container.Add(targetUI.TitlePlugin.GetText(Msg(player, PluginTitle), MainLayer));

			container.Add(targetUI.DescriptionPlugin.GetText(Msg(player, PluginDescription), MainLayer));

			if (targetUI.ShowLine)
				container.Add(targetUI.HeaderLine.GetImage(MainLayer));
			
			if (_config.Inventory.Enabled)
				container.AddRange(targetUI.InventoryButton.Get(
					Msg(player, PluginButtonInventory),
					$"{CMD_Main_Console} inventory open",
					MainLayer,
					MainLayer + ".Button.Inventory"));

			#region Settings

			if (permission.UserHasPermission(player.UserIDString, PERM_EDIT))
			{
				container.Add(targetUI.SettingsButton.GetImage(MainLayer, MainLayer + ".Button.Settings",
					MainLayer + ".Button.Settings"));
                
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "10 10",
						OffsetMax = "-10 -10"
					},
					Text = {Text = string.Empty},
					Button =
					{
						Color = "1 1 1 1",
						Command = $"{CMD_Main_Console} edit_config start",
						Sprite = "assets/icons/gear.png"
					}
				}, MainLayer + ".Button.Settings");
			}

			#endregion

			#region Close

			if (openedRewardPlayer.useMainUI)
				container.AddRange(targetUI.CloseButton.Get(string.Empty, $"{CMD_Main_Console} close", MainLayer,
				MainLayer + ".Button.Close", close: Layer));

			#endregion
		}

		private void ShowAwardDayUI(BasePlayer player, PlayerData data, int awardDay, AwardDayInfo award,
			ref CuiElementContainer container)
		{
			var openedRewardPlayer = GetOpenedRewardPlayer(player.userID);
			var targetUI = openedRewardPlayer.GetUI();

			if (award == null)
			{
				ShowAwardBackgroundUI(awardDay, container);

				AwardFrammedUI(player, ref container,
					MainLayer + $".Item.{awardDay}.Update",
					targetUI.BlockedDayColor.Get,
					Msg(player, DailyCardDayNumber, awardDay),
					Msg(player, DailyCardDayEmpty),
					string.Empty,
					Msg(player, DailyCardNoAward),
					string.Empty,
					new CuiRawImageComponent
					{
						Png = GetImage(targetUI.NoAwardImage)
					});
				return;
			}

			if (data.IsTaked(awardDay))
			{
				ShowAwardBackgroundUI(awardDay, container);

				var frameColor = targetUI.UseSpecialDayColor && award.IsSpecialDay
					? targetUI.SpecialDayColor.Get
					: targetUI.TakedDayColor.Get;

				AwardFrammedUI(player, ref container,
					MainLayer + $".Item.{awardDay}.Update",
					frameColor,
					Msg(player, DailyCardDayNumber, awardDay),
					award.GetTitle(),
					$"{CMD_Main_Console} show_info {awardDay}",
					Msg(player, DailyCardReceived),
					string.Empty,
					award.GetImage());
			}
			else if (data.CanTake(player.userID, awardDay))
			{
				ShowAwardBackgroundUI(awardDay, container);

				if (_config.Cooldown.Enabled && data.TryGetCooldown(out var leftTime))
				{
					var frameColor = targetUI.UseSpecialDayColor && award.IsSpecialDay
						? targetUI.SpecialDayColor.Get
						: targetUI.BlockedWithCooldownDayColor.Get;

					AwardFrammedUI(player, ref container,
						MainLayer + $".Item.{awardDay}.Update",
						frameColor,
						Msg(player, DailyCardDayNumber, awardDay),
						award.GetTitle(),
						$"{CMD_Main_Console} show_info {awardDay}",
						TimeSpan.FromSeconds(leftTime).ToShortString(),
						string.Empty,
						award.GetImage());
				}
				else
				{
					var frameColor = targetUI.UseSpecialDayColor && award.IsSpecialDay
						? targetUI.SpecialDayColor.Get
						: targetUI.AvaiableDayColor.Get;

					AwardFrammedUI(player, ref container,
						MainLayer + $".Item.{awardDay}.Update",
						frameColor,
						Msg(player, DailyCardDayNumber, awardDay),
						award.GetTitle(),
						$"{CMD_Main_Console} show_info {awardDay}",
						Msg(player, DailyCardBtnTake),
						$"{CMD_Main_Console} take_day {awardDay}",
						award.GetImage());
				}
			}
			else
			{
				if (targetUI.ShowImageOnSecretDay)
				{
					container.Add(new CuiElement
					{
						Parent = MainLayer + $".Item.{awardDay}.Background",
						Name = MainLayer + $".Item.{awardDay}.Update",
						DestroyUi = MainLayer + $".Item.{awardDay}.Update",
						Components =
						{
							new CuiRawImageComponent
							{
								Png = GetImage(targetUI.UseSpecialDayColor && award.IsSpecialDay
									? targetUI.SpecialDayImageForFinalDay
									: targetUI.SecretDayImage),
								Color = "1 1 1 1",
								Sprite = "assets/content/textures/generic/fulltransparent.tga"
							},
							new CuiRectTransformComponent
							{
								AnchorMin = "0 0", AnchorMax = "1 1"
							}
						}
					});
				}
				else
				{
					ShowAwardBackgroundUI(awardDay, container);

					var frameColor = targetUI.UseSpecialDayColor && award.IsSpecialDay
						? targetUI.SpecialDayColor.Get
						: targetUI.BlockedDayColor.Get;

					AwardFrammedUI(player, ref container,
						MainLayer + $".Item.{awardDay}.Update",
						frameColor,
						Msg(player, DailyCardDayNumber, awardDay),
						award.GetTitle(),
						$"{CMD_Main_Console} show_info {awardDay}",
						Msg(player, DailyCardBlocked),
						string.Empty,
						award.GetImage());
				}
			}
		}

		private void UpdateCooldownUI(BasePlayer player, PlayerData data, ref CuiElementContainer container)
		{
			float leftTime = 0;
			var hasCooldown = data.CanTakeNextDay(player.userID) && (leftTime = data.GetCooldown()) > 0;

			container.Add(new CuiElement
			{
				Name = MainLayer + ".Cooldown.Value",
				DestroyUi = MainLayer + ".Cooldown.Value",
				Parent = MainLayer + ".Cooldown",
				Components =
				{
					new CuiTextComponent
					{
						Text = hasCooldown
							? TimeSpan.FromSeconds(leftTime).ToShortString()
							: Msg(player, PluginLeftCooldownPassed),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 18,
						Color = HexToCuiColor("#FFFFFF", 90)
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 0", AnchorMax = "1 1"
					}
				}
			});
		}

		private const float
			AWARDS_ITEM_HEIGHT = 80f,
			AWARDS_ITEM_WIDTH = 80f,
			AWARDS_ITEM_MARGIN_Y = 5f,
			AWARDS_ITEM_MARGIN_X = 5f;

		private const int
			AWARDS_ITEM_ITEMS_ON_LINE = 4,
			AWARDS_ITEM_LINES = 4;

		private int AWARDS_ITEM_TOTAL_AMOUNT => AWARDS_ITEM_ITEMS_ON_LINE * AWARDS_ITEM_LINES;

		private void AwardInfoUI(BasePlayer player, int awardDay, int page = 0)
		{
			#region Fields

			var container = new CuiElementContainer();

			var dayInfo = GetAwardByDay(awardDay);

#if EDITING_MODE
			var isEditMode = HasDayEditing(player);
#else
			var isEditMode = false;
#endif

			var items = dayInfo?.GetAvailableAwards(player, isEditMode) ?? new List<AwardInfo>();

			var ySwitch = -125f;

			var itemsToShow = items.SkipAndTake(AWARDS_ITEM_TOTAL_AMOUNT * page, AWARDS_ITEM_TOTAL_AMOUNT);

			var itemsToShowOnLine = Mathf.Min(AWARDS_ITEM_ITEMS_ON_LINE, itemsToShow.Count);

			#endregion

			#region Background

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1"
				},
				Image =
				{
					Color = HexToCuiColor("#000000", 98)
				}
			}, "Overlay", ModalLayer, ModalLayer);

			#endregion

			#region Main

			container.Add(
				(isEditMode ? _config.UI.AwardInfoPanelInEditing : _config.UI.AwardInfoPanel).GetImage(ModalLayer,
					ModalMainLayer, ModalMainLayer));

			#endregion

			#region Title

			container.Add(_config.UI.AwardInfoTitle.GetText(Msg(player, AwardsInfoTitle), ModalMainLayer));

			#endregion

			#region Items

			var xSwitch = -(itemsToShowOnLine * AWARDS_ITEM_WIDTH + (itemsToShowOnLine - 1) * AWARDS_ITEM_MARGIN_X) /
			              2f;
			
			for (var i = 0; i < itemsToShow.Count; i++)
			{
				var item = itemsToShow[i];

				container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = $"{xSwitch} {ySwitch - AWARDS_ITEM_HEIGHT}",
							OffsetMax = $"{xSwitch + AWARDS_ITEM_WIDTH} {ySwitch}"
						},
						Image =
						{
							Color = _config.UI.AwardInfoItemBackgroundColor.Get
						}
					}, ModalMainLayer, ModalMainLayer + $".Item.{i}");

				#region Image

				ShowAwardImage(item, ref container, ModalMainLayer + $".Item.{i}", new CuiRectTransformComponent
				{
					AnchorMin = "0 0",
					AnchorMax = "1 1",
					OffsetMin = "10 10",
					OffsetMax = "-10 -10"
				});

				#endregion

				#region Amount

				container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0",
							AnchorMax = "1 1",
							OffsetMin = "5 5",
							OffsetMax = "-5 -5"
						},
						Text =
						{
							Text = Msg(player, ItemAmountTitle, item.Amount),
							Align = TextAnchor.LowerRight,
							Font = "robotocondensed-bold.ttf",
							FontSize = 18,
							Color = HexToCuiColor("#DCDCDC")
						}
					}, ModalMainLayer + $".Item.{i}");

				#endregion

				#region Edit

				if (isEditMode)
				{
					container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "1 1", AnchorMax = "1 1",
								OffsetMin = "-25 -25", OffsetMax = "0 0"
							},
							Image =
							{
								Color = HexToCuiColor("#519229")
							}
						}, ModalMainLayer + $".Item.{i}", ModalMainLayer + $".Item.{i}.Edit");

					container.Add(new CuiElement
					{
						Parent = ModalMainLayer + $".Item.{i}.Edit",
						Components =
						{
							new CuiRawImageComponent
							{
								Png = GetImage(_config.UI.EditImage)
							},
							new CuiRectTransformComponent
							{
								AnchorMin = "0 0",
								AnchorMax = "1 1",
								OffsetMin = "5 5",
								OffsetMax = "-5 -5"
							}
						}
					});

					container.Add(new CuiButton
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 1"
							},
							Text = {Text = string.Empty},
							Button =
							{
								Color = "0 0 0 0",
								Command = $"{CMD_Main_Console} edit_item start {awardDay} {item.ID} {page}"
							}
						}, ModalMainLayer + $".Item.{i}.Edit", ModalMainLayer + $".Item.{i}.Edit.BTN");
				}

				#endregion

				#region Calculate Position

				if ((i + 1) % AWARDS_ITEM_ITEMS_ON_LINE == 0)
				{
					itemsToShowOnLine = Mathf.Min(AWARDS_ITEM_ITEMS_ON_LINE, itemsToShow.Count - i - 1);
					xSwitch =
						-(itemsToShowOnLine * AWARDS_ITEM_WIDTH + (itemsToShowOnLine - 1) * AWARDS_ITEM_MARGIN_X) / 2f;

					ySwitch = ySwitch - AWARDS_ITEM_HEIGHT - AWARDS_ITEM_MARGIN_Y;
				}
				else
				{
					xSwitch += AWARDS_ITEM_WIDTH + AWARDS_ITEM_MARGIN_X;
				}

				#endregion
			}

			#endregion

			#region Pages

			var pagesCount = Mathf.CeilToInt((float) (items.Count + 1) / AWARDS_ITEM_TOTAL_AMOUNT);
			if (pagesCount > 1)
			{
				var hasBackPage = page != 0;

				var hasNextPage = items.Count > (page + 1) * AWARDS_ITEM_TOTAL_AMOUNT;

				container.AddRange(
					(hasBackPage
						? _config.UI.AwardInfoPageButtonBackWhenActive
						: _config.UI.AwardInfoPageButtonBackWhenInactive).Get(string.Empty,
						hasBackPage ? $"{CMD_Main_Console} show_info {awardDay} {page - 1}" : string.Empty,
						ModalMainLayer,
						ModalMainLayer + ".Btn.Back",
						ModalMainLayer + ".Btn.Back"));

				container.AddRange((hasNextPage
					? _config.UI.AwardInfoPageButtonNextWhenActive
					: _config.UI.AwardInfoPageButtonNextWhenInactive).Get(string.Empty,
					hasNextPage ? $"{CMD_Main_Console} show_info {awardDay} {page + 1}" : "",
					ModalMainLayer,
					ModalMainLayer + ".Btn.Next",
					ModalMainLayer + ".Btn.Next"));

				container.Add(_config.UI.AwardInfoPagesNumber.GetText(Msg(player, PagesCount, page + 1, pagesCount),
					ModalMainLayer, ModalMainLayer + ".Pages.Number"));
			}

			#endregion

			#region Close Button

			container.AddRange(_config.UI.AwardInfoCloseButton.Get(
				Msg(player, AwardsInfoClose),
				isEditMode ? $"{CMD_Main_Console} edit_day close" : string.Empty,
				ModalMainLayer, close: ModalLayer));

			#endregion

			#region Edit Mode

#if EDITING_MODE
			if (isEditMode)
			{
				var dayEditing = GetDayEditing(player);
				if (dayEditing != null)
				{
					#region Header

					#region Title

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = "0 0",
							OffsetMax = "0 40"
						},
						Text =
						{
							Text = Msg(player, EditingTitleDay, awardDay),
							Align = TextAnchor.MiddleLeft,
							Font = "robotocondensed-bold.ttf",
							FontSize = 22,
							Color = HexToCuiColor("#DCDCDC")
						}
					}, ModalMainLayer);

					#endregion

					#region Close Edit

					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "1 1", AnchorMax = "1 1",
							OffsetMin = "-30 5",
							OffsetMax = "0 35"
						},
						Text =
						{
							Text = Msg(player, EditingBtnClose),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 22,
							Color = HexToCuiColor("#EF5125")
						},
						Button =
						{
							Color = "0 0 0 0",
							Close = ModalLayer,
							Command = $"{CMD_Main_Console} edit_day close"
						}
					}, ModalMainLayer, ModalMainLayer + ".BTN.Close.Edit");

					#endregion

					#region Add Item

					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "1 1", AnchorMax = "1 1",
							OffsetMin = "-65 5",
							OffsetMax = "-35 35"
						},
						Text =
						{
							Text = Msg(player, EditingBtnADD),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 22,
							Color = HexToCuiColor("#EF5125")
						},
						Button =
						{
							Color = "0 0 0 0",
							Command = $"{CMD_Main_Console} edit_item start {awardDay} {-1}"
						}
					}, ModalMainLayer, ModalMainLayer + ".BTN.Add.Item");

					#endregion

					#endregion

					#region Properties

					var constSwitch = 20f;
					var fieldsOnLine = 2;

					var properties = dayEditing.GetProperties();

					var maxLines = Mathf.CeilToInt((float) properties.Length / fieldsOnLine);

					var totalHeight = maxLines * (UI_PROPERTY_TITLE_HEIGHT + UI_PROPERTY_HEIGHT) +
					                  (maxLines - 1) * UI_PROPERTY_MARGIN_Y
					                  + constSwitch * 2
					                  + UI_EDITING_FOOTER_HEIGHT;

					var propertiesLayer = container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "1 0.5", AnchorMax = "1 0.5",
							OffsetMin = $"20 -{totalHeight / 2}",
							OffsetMax = $"590 {totalHeight / 2}"
						},
						Image =
						{
							Color = HexToCuiColor("#38393F")
						}
					}, ModalMainLayer, ModalMainLayer + ".Award.Edit");

					#region Fields

					xSwitch = constSwitch;
					ySwitch = -constSwitch;

					var index = 1;

					foreach (var property in properties)
						if (property.HasValues)
							PropertyUI(
								player,
								property,
								propertiesLayer,
								ref container,
								property.Name,
								"edit_day",
								"property",
								ref index,
								ref fieldsOnLine,
								UI_PROPERTY_TITLE_HEIGHT,
								ref xSwitch,
								ref ySwitch,
								ref constSwitch,
								UI_PROPERTY_WIDTH,
								UI_PROPERTY_HEIGHT,
								UI_PROPERTY_MARGIN_Y,
								UI_PROPERTY_MARGIN_X);

					#endregion

					#region Footer

					EditingFooterUI(player, ref container, ModalMainLayer + ".Award.Edit", ModalLayer,
						dayEditing.MainCommand,
						0f, 0f,
						!dayEditing.IsGenerated, string.Empty, true, string.Empty);

					#endregion

					#endregion

					// #endregion
				}
			}
#endif

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private void ShowAwardImage(ref CuiElementContainer container,
			string parent,
			CuiRectTransformComponent rectTransform,
			string image = "",
			int itemid = 0,
			ulong skin = 0UL)
		{
			if (_enabledImageLibrary && !string.IsNullOrEmpty(image))
			{
				container.Add(new CuiElement
				{
					Name = parent + ".Image",
					DestroyUi = parent + ".Image",
					Parent = parent,
					Components =
					{
						new CuiRawImageComponent
						{
							Png = GetImage(image)
						},
						rectTransform
					}
				});
				return;
			}

			var def = ItemManager.FindItemDefinition(itemid);
			if (def != null)
			{
				container.Add(new CuiElement
				{
					Name = parent + ".Image",
					DestroyUi = parent + ".Image",
					Parent = parent,
					Components =
					{
						new CuiImageComponent
						{
							ItemId = itemid,
							SkinId = skin
						},
						rectTransform
					}
				});
				return;
			}

			if (_enabledImageLibrary)
				container.Add(new CuiElement
				{
					Name = parent + ".Image",
					DestroyUi = parent + ".Image",
					Parent = parent,
					Components =
					{
						new CuiRawImageComponent
						{
							Png = GetImage("NONE")
						},
						rectTransform
					}
				});
		}

		private void ShowAwardImage(AwardInfo item, ref CuiElementContainer container, string parent,
			CuiRectTransformComponent rectTransform)
		{
			if (_enabledImageLibrary && !string.IsNullOrEmpty(item.Image))
			{
				container.Add(new CuiElement
				{
					Name = parent + ".Image",
					DestroyUi = parent + ".Image",
					Parent = parent,
					Components =
					{
						new CuiRawImageComponent
						{
							Png = GetImage(item.Image)
						},
						rectTransform
					}
				});
			}
			else if (item.Definition != null)
			{
				container.Add(new CuiElement
				{
					Name = parent + ".Image",
					DestroyUi = parent + ".Image",
					Parent = parent,
					Components =
					{
						new CuiImageComponent
						{
							ItemId = item.Definition.itemid,
							SkinId = item.Skin
						},
						rectTransform
					}
				});
			}
			else
			{
				if (_enabledImageLibrary)
					container.Add(new CuiElement
					{
						Name = parent + ".Image",
						DestroyUi = parent + ".Image",
						Parent = parent,
						Components =
						{
							new CuiRawImageComponent
							{
								Png = GetImage("NONE")
							},
							rectTransform
						}
					});
			}
		}

		private void InventoryUI(BasePlayer player, int page = 0, bool first = false)
		{
			var openedRewardPlayer = GetOpenedRewardPlayer(player.userID);
			var targetUI = openedRewardPlayer?.GetUI()?.Inventory;
			if (targetUI == null) return;

			var data = PlayerData.GetOrLoad(player.userID);
			if (data == null) return;

			var container = new CuiElementContainer();

			if (first)
				container.Add(_config.UI.Background.Get(Layer, Layer, true));

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image =
				{
					Color = "0 0 0 0"
				}
			}, Layer, MainLayer, MainLayer);

			#endregion

			#region Header

			container.Add(targetUI.Title.GetText(Msg(player, InventoryTitle), MainLayer));
			container.Add(targetUI.Description.GetText(Msg(player, InventoryDescription), MainLayer, MainLayer + ".Description"));
			
			if (targetUI.ShowLine)
				container.Add(targetUI.Line.GetImage(MainLayer));

			container.AddRange(targetUI.ButtonBack.Get(Msg(player, InventoryButtonBack), $"{CMD_Main_Console} inventory back", MainLayer, MainLayer + ".Button.Inventory"));
			
			if (openedRewardPlayer.useMainUI)
				container.AddRange(targetUI.ButtonClose.Get(Msg(player, string.Empty), $"{CMD_Main_Console} inventory close", MainLayer, MainLayer + ".Button.Close", close: Layer));
			
			#endregion

			#region Items

			var items = data.GetStoredItems(page * targetUI.GetInventoryTotalAmount(), targetUI.GetInventoryTotalAmount());

			var ySwitch = -targetUI.InventoryItemsTopIndent;
			var constSwitchX = -(targetUI.InventoryItemsOnLine * targetUI.InventoryItemWidth + (targetUI.InventoryItemsOnLine - 1) * targetUI.InventoryItemMarginX) /
			                   2f;
			var xSwitch = constSwitchX;

			for (var index = 0; index < targetUI.GetInventoryTotalAmount(); index++)
			{
				container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 0.5",
							AnchorMax = "0.5 0.5",
							OffsetMin = $"{xSwitch} {ySwitch - targetUI.InventoryItemHeight}",
							OffsetMax = $"{xSwitch + targetUI.InventoryItemWidth} {ySwitch}"
						},
						Image =
						{
							Color = HexToCuiColor("#38393F")
						}
					}, MainLayer, MainLayer + $".Item.{index}", MainLayer + $".Item.{index}");

				if (index < items.Count)
				{
					var (key, award) = items[index];

					ShowAwardImage(award, ref container, MainLayer + $".Item.{index}", new CuiRectTransformComponent
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = "-40 -40",
						OffsetMax = "40 40"
					});

					container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0",
								AnchorMax = "1 1",
								OffsetMin = "5 5",
								OffsetMax = "-5 -5"
							},
							Text =
							{
								Text = Msg(player, ItemAmountTitle, award.Amount),
								Align = TextAnchor.LowerRight,
								Font = "robotocondensed-bold.ttf",
								FontSize = 18,
								Color = HexToCuiColor("#DCDCDC")
							}
						}, MainLayer + $".Item.{index}");

					container.Add(new CuiButton
						{
							RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
							Text = {Text = string.Empty},
							Button =
							{
								Color = "0 0 0 0",
								Close = MainLayer + $".Item.{index}.Hover",
								Command = $"{CMD_Main_Console} inventory give {key} {index}"
							}
						}, MainLayer + $".Item.{index}", MainLayer + $".Item.{index}.Hover");
				}
				else
				{
					container.Add(new CuiElement
					{
						Parent = MainLayer + $".Item.{index}",
						Components =
						{
							new CuiRawImageComponent
							{
								Png = GetImage(_config.UI.RustIconImage)
							},
							new CuiRectTransformComponent
							{
								AnchorMin = "0.5 0.5",
								AnchorMax = "0.5 0.5",
								OffsetMin = "-40 -40",
								OffsetMax = "40 40"
							}
						}
					});
				}

				if ((index + 1) % targetUI.InventoryItemsOnLine == 0)
				{
					ySwitch += targetUI.InventoryItemHeight + targetUI.InventoryItemMarginY;
					xSwitch = constSwitchX;
				}
				else
				{
					xSwitch += targetUI.InventoryItemWidth + targetUI.InventoryItemMarginX;
				}
			}

			#endregion

			#region Pages

			PagesInventoryUI(player, page, ref container, data, targetUI.GetInventoryTotalAmount());

			#endregion

			CuiHelper.AddUi(player, container);
		}

		#region UI.Components

		private static void UpdateUI(BasePlayer player, Action<CuiElementContainer> callback)
		{
			var container = new CuiElementContainer();

			callback?.Invoke(container);

			CuiHelper.AddUi(player, container);
		}

		private static void ShowAwardBackgroundUI(int awardDay, CuiElementContainer container)
		{
			container.Add(new CuiPanel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Image =
					{
						Color = "0.141 0.137 0.109 0.8",
						Sprite = "assets/content/ui/ui.background.tile.psd",
						Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
					}
				}, MainLayer + $".Item.{awardDay}.Background", MainLayer + $".Item.{awardDay}.Update",
				MainLayer + $".Item.{awardDay}.Update");
		}
		
		private void AwardFrammedUI(BasePlayer player,
			ref CuiElementContainer container,
			string parent,
			string frameColor,
			string firstLineText,
			string secondLineText,
			string commandInfo,
			string btnTitle,
			string btnCommand,
			ICuiComponent image)
		{
			OutlineUI(ref container, frameColor, parent);

			#region Image

			if (image != null)
				container.Add(new CuiElement
				{
					Name = parent + ".Image",
					Parent = parent,
					Components =
					{
						image,
						new CuiRectTransformComponent
						{
							AnchorMin = "0.5 1",
							AnchorMax = "0.5 1",
							OffsetMin = "-55 -128",
							OffsetMax = "55 -18"
						}
					}
				});

			#endregion

			#region Info

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 0",
					OffsetMin = "15 35",
					OffsetMax = "-15 75"
				},
				Text =
				{
					Text = $"{firstLineText}",
					Align = TextAnchor.UpperLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 12,
					Color = HexToCuiColor("#EF5125")
				}
			}, parent);

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 0",
					OffsetMin = "15 35",
					OffsetMax = "-15 60"
				},
				Text =
				{
					Text = $"{secondLineText}",
					Align = TextAnchor.UpperLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = HexToCuiColor("#FFFFFF", 90)
				}
			}, parent);

			#endregion

			#region Button.Info

			if (!string.IsNullOrEmpty(commandInfo))
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 1", AnchorMax = "1 1",
						OffsetMin = "-25 -25",
						OffsetMax = "0 0"
					},
					Text =
					{
						Text = Msg(player, AwardInfoShow),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 18,
						Color = HexToCuiColor("#FFFFFF", 50)
					},
					Button =
					{
						Color = frameColor,
						Command = commandInfo
					}
				}, parent);

			#endregion

			#region Button

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0 0",
					AnchorMax = "1 0",
					OffsetMin = "0 0",
					OffsetMax = "0 36.5"
				},
				Text =
				{
					Text = btnTitle,
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 20,
					Color = HexToCuiColor("#FFFFFF", 90)
				},
				Button =
				{
					Color = frameColor,
					Command = btnCommand
				}
			}, parent, parent + ".Btn");

			#endregion
		}

		private void PagesInventoryUI(BasePlayer player, int page, ref CuiElementContainer container,
			PlayerData data, int totalAmount)
		{
			#region Back

			container.Add(new CuiElement
			{
				Parent = MainLayer,
				Name = MainLayer + ".Btn.Back",
				DestroyUi = MainLayer + ".Btn.Back",
				Components =
				{
					new CuiRawImageComponent
					{
						Png = GetImage(_config.UI.ButtonBackImage),
						Color = page != 0 ? HexToCuiColor("#EF5125") : HexToCuiColor("#FFFFFF", 20)
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 0.5",
						AnchorMax = "0 0.5",
						OffsetMin = "50 -75",
						OffsetMax = "80 -45"
					}
				}
			});

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text = {Text = string.Empty},
				Button =
				{
					Color = "0 0 0 0",
					Command = page != 0 ? $"{CMD_Main_Console} inventory page {page - 1}" : string.Empty
				}
			}, MainLayer + ".Btn.Back");

			#endregion

			#region Next

			container.Add(new CuiElement
			{
				Parent = MainLayer,
				Name = MainLayer + ".Btn.Next",
				DestroyUi = MainLayer + ".Btn.Next",
				Components =
				{
					new CuiRawImageComponent
					{
						Png = GetImage(_config.UI.ButtonNextImage),
						Color = data.StoredItems.Count > (page + 1) * totalAmount
							? HexToCuiColor("#EF5125")
							: HexToCuiColor("#FFFFFF", 20)
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "1 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "-80 -75",
						OffsetMax = "-50 -45"
					}
				}
			});

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text = {Text = string.Empty},
				Button =
				{
					Color = "0 0 0 0",
					Command = data.StoredItems.Count > (page + 1) * totalAmount
						? $"{CMD_Main_Console} inventory page {page + 1}"
						: string.Empty
				}
			}, MainLayer + ".Btn.Next");

			#endregion

			#region Number

			var pagesCount = Mathf.CeilToInt(Mathf.Max(data.StoredItems.Count, 1f) / totalAmount);

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "1 0", AnchorMax = "1 0",
					OffsetMin = "-205 50",
					OffsetMax = "-105 90"
				},
				Text =
				{
					Text = Msg(player, PagesCount, page + 1, pagesCount),
					Align = TextAnchor.LowerRight,
					Font = "robotocondensed-bold.ttf",
					FontSize = 32,
					Color = HexToCuiColor("#FFFFFF", 40)
				}
			}, MainLayer, MainLayer + ".Pages.Number", MainLayer + ".Pages.Number");

			#endregion
		}

		private void ShowInventoryItemStatus(BasePlayer player, int index, bool successfull)
		{
			var container = new CuiElementContainer();

			container.Add(new CuiPanel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Image =
					{
						Color = successfull ? HexToCuiColor("#74884A", 90) : HexToCuiColor("#CD4632", 33)
					}
				}, MainLayer + $".Item.{index}",
				MainLayer + $".Item.{index}.Hover",
				MainLayer + $".Item.{index}.Hover");

			container.Add(new CuiLabel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text =
					{
						Text = successfull ? Msg(player, AwardReceived) : Msg(player, AwardReceiveError),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 18,
						Color = HexToCuiColor("#FFFFFF", 90)
					}
				}, MainLayer + $".Item.{index}.Hover");

			CuiHelper.AddUi(player, container);
		}

		private void OutlineUI(ref CuiElementContainer container, string frameColor, string parent)
		{
			//down
			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0",
					AnchorMax = "1 0",
					OffsetMin = "0 0",
					OffsetMax = "0 36.5"
				},
				Image =
				{
					Color = frameColor
				}
			}, parent);

			//up
			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1",
					AnchorMax = "1 1",
					OffsetMin = "0 -2",
					OffsetMax = "0 0"
				},
				Image =
				{
					Color = frameColor
				}
			}, parent);

			//left
			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0",
					AnchorMax = "0 1",
					OffsetMin = "0 0",
					OffsetMax = "2 0"
				},
				Image =
				{
					Color = frameColor
				}
			}, parent);

			//right
			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "1 0",
					AnchorMax = "1 1",
					OffsetMin = "-2 0",
					OffsetMax = "0 0"
				},
				Image =
				{
					Color = frameColor
				}
			}, parent);
		}

		#endregion

		#endregion Interface

		#region Editing

#if EDITING_MODE

		#region Interface

		private void EditAwardUI(BasePlayer player)
		{
			var editData = GetAwardEditing(player);
			var propterties = editData?.GetProperties();
			if (propterties == null) return;

			var container = new CuiElementContainer();

			#region Background

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1"
				},
				Image =
				{
					Color = HexToCuiColor("#000000", 98)
				}
			}, "Overlay", SecondModalLayer, SecondModalLayer);

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-420 -340",
					OffsetMax = "420 300"
				},
				Image =
				{
					Color = HexToCuiColor("#202224")
				}
			}, SecondModalLayer, SecondModalMainLayer);

			#region Header

			#region Title

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 0",
					OffsetMax = "0 50"
				},
				Text =
				{
					Text = Msg(player, editData.IsGenerated ? EditingAwardAdd : EditingAwardEdit),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 24,
					Color = HexToCuiColor("#DCDCDC")
				}
			}, SecondModalMainLayer);

			#endregion

			#region Close

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-30 15",
					OffsetMax = "0 45"
				},
				Text =
				{
					Text = Msg(player, EditingBtnClose),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 22,
					Color = HexToCuiColor("#EF5125")
				},
				Button =
				{
					Color = "0 0 0 0",
					Command = $"{CMD_Main_Console} {editData.MainCommand} close",
					Close = SecondModalLayer
				}
			}, SecondModalMainLayer, SecondModalMainLayer + ".BTN.Close.Edit");

			#endregion

			#endregion

			#region Content

			#region Select Item

			var shortNameProperty = Array.Find(propterties, f => f.Name.Contains("ShortName"));
			if (shortNameProperty != null)
			{
				var image = string.Empty;
				var itemid = 0;
				var skin = 0UL;

				foreach (var prop in propterties)
				{
					if ((prop.Name.Contains("Image") || prop.Name.Contains("Изображение")) &&
					    string.IsNullOrEmpty(image))
					{
						var imageVal = prop.Value?.ToString();
						if (!string.IsNullOrEmpty(imageVal))
						{
							image = imageVal;
							break;
						}
					}

					if (prop.Name.Contains("ShortName"))
					{
						var val = prop.Value?.ToString();
						if (!string.IsNullOrEmpty(val))
						{
							var def = ItemManager.FindItemDefinition(val);
							if (def != null) itemid = def.itemid;
						}

						continue;
					}

					if (prop.Name.Contains("Skin") || prop.Name.Contains("Скин"))
					{
						var val = prop.Value?.ToString();
						if (!string.IsNullOrEmpty(val))
						{
							var uVal = Convert.ToUInt64(val);
							if (uVal > 0) skin = uVal;
						}
					}
				}

				SelectItemElementUI(player,
					$"{CMD_Main_Console} {editData.MainCommand} select start {shortNameProperty.Path.ReplaceSpaceToUnicode()} {nameof(EditAwardUI)}",
					ref container, SecondModalMainLayer,
					new CuiRectTransformComponent
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = "20 -160",
						OffsetMax = "275 -20"
					},
					(ic, parent) => ShowAwardImage(ref ic, parent,
						new CuiRectTransformComponent
						{
							AnchorMin = "0.5 0.5",
							AnchorMax = "0.5 0.5",
							OffsetMin = "-60 -60",
							OffsetMax = "60 60"
						}, image, itemid, skin));
			}

			#endregion

			#region Fields

			var constSwitch = 295f;
			var fieldsOnLine = 2;

			var xSwitch = constSwitch;
			var ySwitch = -20f;

			var index = 1;

			for (var i = 0; i < propterties.Length; i++)
			{
				var property = propterties[i];
				if (!property.HasValues) continue;

				var useCalculator = true;

				if (i + 1 < propterties.Length)
				{
					var nextProperty = propterties[i + 1];
					if (nextProperty != null && nextProperty.Value.Type == JTokenType.Object) useCalculator = false;
				}

				PropertyUI(
					player,
					property,
					SecondModalMainLayer,
					ref container,
					property.Name,
					"edit_item",
					"property",
					ref index,
					ref fieldsOnLine,
					UI_PROPERTY_TITLE_HEIGHT,
					ref xSwitch,
					ref ySwitch,
					ref constSwitch,
					UI_PROPERTY_WIDTH,
					UI_PROPERTY_HEIGHT,
					UI_PROPERTY_MARGIN_Y,
					UI_PROPERTY_MARGIN_X,
					useCalculator);
			}

			#endregion

			#endregion

			#region Footer

			EditingFooterUI(player, ref container, SecondModalMainLayer, SecondModalLayer, editData.MainCommand,
				0f, 0f,
				editData.IsGenerated == false, string.Empty, true, string.Empty, editData.ID);

			#endregion

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private void ConfirmActionUI(BasePlayer player, string action, params string[] obj)
		{
			var container = new CuiElementContainer();

			#region Background

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1"
				},
				Image =
				{
					Color = HexToCuiColor("#000000", 90)
				}
			}, "Overlay", ModalLayer, ModalLayer);

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-210 -95",
					OffsetMax = "210 95"
				},
				Image =
				{
					Color = HexToCuiColor("#38393F")
				}
			}, ModalLayer, ModalMainLayer, ModalMainLayer);

			#endregion

			#region Title

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -105",
					OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, EditingAwardRemove),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 36,
					Color = HexToCuiColor("#DCDCDC")
				}
			}, ModalMainLayer);

			#endregion

			#region Buttons

			#region Accept

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 0", AnchorMax = "0.5 0",
					OffsetMin = "-160 30",
					OffsetMax = "-10 85"
				},
				Text =
				{
					Text = Msg(player, EditingBtnAccept),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 28,
					Color = HexToCuiColor("#FDEDE9")
				},
				Button =
				{
					Color = HexToCuiColor("#EF5125"),
					Close = ModalLayer,
					Command = $"{CMD_Main_Console} confirm {action} accept {string.Join(" ", obj)}"
				}
			}, ModalMainLayer, ModalMainLayer + ".BTN.Accept");

			#endregion

			#region Cancel

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 0", AnchorMax = "0.5 0",
					OffsetMin = "10 30",
					OffsetMax = "160 85"
				},
				Text =
				{
					Text = Msg(player, EditingBtnCancel),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 28,
					Color = HexToCuiColor("#FDEDE9")
				},
				Button =
				{
					Color = HexToCuiColor("#228BA1"),
					Close = ModalLayer,
					Command = $"{CMD_Main_Console} confirm {action} cancel {string.Join(" ", obj)}"
				}
			}, ModalMainLayer, ModalMainLayer + ".BTN.Cancel");

			#endregion

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private void EditConfigUI(BasePlayer player, int page = 0)
		{
			var editData = GetConfigEditing(player);
			if (editData == null) return;

			var constSwitch = 20f;
			var fieldsOnLine = 4;
			var maxFields = 12;
			var totalWidth = fieldsOnLine * UI_PROPERTY_WIDTH + (fieldsOnLine - 1) * UI_PROPERTY_MARGIN_X +
			                 constSwitch * 2;

			var propterties = editData.GetProperties().GetProperties();

			var container = new CuiElementContainer();

			#region Background

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1"
				},
				Image =
				{
					Color = HexToCuiColor("#000000", 98)
				}
			}, "Overlay", ModalLayer, ModalLayer);

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = $"-{totalWidth / 2f} -300",
					OffsetMax = $"{totalWidth / 2f} 300"
				},
				Image =
				{
					Color = HexToCuiColor("#202224")
				}
			}, ModalLayer, ModalMainLayer);

			#region Header

			#region Title

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 0",
					OffsetMax = "0 50"
				},
				Text =
				{
					Text = Msg(player, EditingCfgTitle),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 24,
					Color = HexToCuiColor("#DCDCDC")
				}
			}, ModalMainLayer);

			#endregion

			#region Close

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-30 15",
					OffsetMax = "0 45"
				},
				Text =
				{
					Text = Msg(player, EditingBtnClose),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 22,
					Color = HexToCuiColor("#EF5125")
				},
				Button =
				{
					Color = "0 0 0 0",
					Command = $"{CMD_Main_Console} {editData.MainCommand} close",
					Close = ModalLayer
				}
			}, ModalMainLayer, ModalMainLayer + ".BTN.Close.Edit");

			#endregion

			#region Pages

			if (propterties.Count > maxFields)
			{
				var hasBackPage = page != 0;

				var hasNextPage = propterties.Count > (page + 1) * maxFields;

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 1", AnchorMax = "1 1",
						OffsetMin = "-65 15",
						OffsetMax = "-35 45"
					},
					Text =
					{
						Text = Msg(player, EditingBtnNext),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 22,
						Color = hasNextPage ? HexToCuiColor("#EF5125") : HexToCuiColor("#FFFFFF", 20)
					},
					Button =
					{
						Color = "0 0 0 0",
						Command = hasNextPage
							? $"{CMD_Main_Console} {editData.MainCommand} page {page + 1}"
							: string.Empty
					}
				}, ModalMainLayer, ModalMainLayer + ".BTN.Edit.Next");

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 1", AnchorMax = "1 1",
						OffsetMin = "-100 15",
						OffsetMax = "-70 45"
					},
					Text =
					{
						Text = Msg(player, EditingBtnBack),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 22,
						Color = hasBackPage ? HexToCuiColor("#EF5125") : HexToCuiColor("#FFFFFF", 20)
					},
					Button =
					{
						Color = "0 0 0 0",
						Command = hasBackPage
							? $"{CMD_Main_Console} {editData.MainCommand} page {page - 1}"
							: string.Empty
					}
				}, ModalMainLayer, ModalMainLayer + ".BTN.Edit.Back");
			}

			#endregion

			#endregion

			#region Content

			#region Fields

			var ySwitch = -40f;

			var index = 1;

			foreach (var group in propterties.SkipAndTake(page * maxFields, maxFields)
				         .GroupBy(x => x.PropertyRootNames()).ToDictionary(x => x.Key, x => x.ToList()))
			{
				var globalSwitchY = ySwitch;

				var bgLayer = container.Add(new CuiPanel
				{
					Image =
					{
						Color = HexToCuiColor("#2F3134")
					}
				}, ModalMainLayer);

				var elementIndex = container.Count - 1;

				if (!string.IsNullOrEmpty(group.Key))
					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = "0 -20", OffsetMax = "0 0"
						},
						Text =
						{
							Text = $"{group.Key}",
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 12,
							Color = "1 1 1 1"
						}
					}, bgLayer);

				var properties = group.Value.GroupBy(g =>
				{
					var parents = g.PropertyParentsName();
					return parents.Length > 1 ? parents[1] : string.Empty;
				}).ToDictionary(x => x.Key, x => x.ToList());

				var propertiesHeight = 0f;

				var nextGroupMaxItems = 0;
				var first = true;
				foreach (var g in properties)
				{
					if (!string.IsNullOrEmpty(g.Key))
						if (first)
						{
							ySwitch -= UI_PROPERTY_TITLE_HEIGHT;

							first = false;
						}

					var localSwitchY = ySwitch;
					var xSwitch = constSwitch;

					var maxLines = Mathf.CeilToInt((float) g.Value.Count / fieldsOnLine);

					var maxItemsOnLine = Mathf.Min(g.Value.Count, fieldsOnLine);

					if (maxItemsOnLine > nextGroupMaxItems)
						nextGroupMaxItems = maxItemsOnLine;

					var groupColor = GetScaledColor(Color.black, Color.red, 0.5f, 0.6f);

					if (!string.IsNullOrEmpty(g.Key))
					{
						localSwitchY -= UI_PROPERTY_MARGIN_Y;

						var newBgLayer = container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "0 1", AnchorMax = "0 1",
								OffsetMin =
									$"{xSwitch - 10f} {ySwitch - 15f - ((UI_PROPERTY_HEIGHT + UI_PROPERTY_TITLE_HEIGHT) * maxLines + (maxLines - 1) * UI_PROPERTY_MARGIN_Y)}",
								OffsetMax =
									$"{xSwitch + 10f + UI_PROPERTY_WIDTH * maxItemsOnLine + (maxItemsOnLine - 1) * UI_PROPERTY_MARGIN_X} {ySwitch + 20f}"
							},
							Image =
							{
								Color = groupColor
							}
						}, ModalMainLayer);

						container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 1", AnchorMax = "1 1",
								OffsetMin = "0 -20", OffsetMax = "0 0"
							},
							Text =
							{
								Text = $"{g.Key}",
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-bold.ttf",
								FontSize = 12,
								Color = "1 1 1 1"
							}
						}, newBgLayer);
					}

					for (var i = 0; i < g.Value.Count; i++)
					{
						var prop = g.Value[i];

						PropertyWithBackgroundUI(
							player,
							prop,
							ModalMainLayer,
							ref container,
							prop.Name,
							"edit_config",
							"property",
							ref index,
							UI_PROPERTY_TITLE_HEIGHT,
							ref xSwitch,
							ref ySwitch,
							UI_PROPERTY_WIDTH,
							UI_PROPERTY_HEIGHT);

						if ((i + 1) % fieldsOnLine == 0)
						{
							if (i != g.Value.Count - 1) ySwitch = ySwitch - UI_PROPERTY_HEIGHT - UI_PROPERTY_MARGIN_Y;

							xSwitch = constSwitch;
						}
						else
						{
							xSwitch += UI_PROPERTY_WIDTH + UI_PROPERTY_MARGIN_X;
						}
					}

					ySwitch = localSwitchY - 15f - ((UI_PROPERTY_HEIGHT + UI_PROPERTY_TITLE_HEIGHT) * maxLines +
					                                (maxLines - 1) * UI_PROPERTY_MARGIN_Y);

					propertiesHeight += (UI_PROPERTY_HEIGHT + UI_PROPERTY_TITLE_HEIGHT) * maxLines +
					                    (maxLines - 1) * UI_PROPERTY_MARGIN_Y + 15f;

					if (!string.IsNullOrEmpty(g.Key))
						propertiesHeight += UI_PROPERTY_MARGIN_Y;
				}

				var rectIndex = container[elementIndex].Components.IndexOf<CuiRectTransformComponent>();
				if (rectIndex != -1)
					container[elementIndex].Components[rectIndex] = new CuiRectTransformComponent
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin =
							$"{constSwitch - 10f} {globalSwitchY - propertiesHeight}",
						OffsetMax =
							$"{constSwitch + nextGroupMaxItems * UI_PROPERTY_WIDTH + (nextGroupMaxItems - 1) * UI_PROPERTY_MARGIN_X + 10f} {globalSwitchY + 20f}"
					};

				ySwitch = globalSwitchY - propertiesHeight - UI_PROPERTY_MARGIN_Y;

				index++;
			}

			#endregion

			#endregion

			#region Footer

			EditingFooterUI(player, ref container, ModalMainLayer, ModalLayer, editData.MainCommand,
				0f, 0f,
				false, string.Empty, true, string.Empty);

			#endregion

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private void EditingFooterUI(BasePlayer player, ref CuiElementContainer container,
			string parent, string closeLayer, string mainCommand,
			float leftIndent,
			float rightIndent,
			bool hasRemove,
			string cmdRemove,
			bool hasSave,
			string cmdSave,
			params object[] obj)
		{
			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 0",
					OffsetMin = $"{leftIndent} 0",
					OffsetMax = $"-{rightIndent} {UI_EDITING_FOOTER_HEIGHT}"
				},
				Image =
				{
					Color = HexToCuiColor("#2F3134")
				}
			}, parent, parent + ".Footer");

			#region Remove

			if (hasRemove)
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0.5",
						AnchorMax = "0 0.5",
						OffsetMin = "20 -15",
						OffsetMax = "220 15"
					},
					Image =
					{
						Color = HexToCuiColor("#EF5125")
					}
				}, parent + ".Footer", parent + ".Footer.BTN.Remove.Item");

				#region Image

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0.5", AnchorMax = "0 0.5",
						OffsetMin = "15 -10",
						OffsetMax = "35 10"
					},
					Image =
					{
						Sprite = "assets/icons/clear.png"
					}
				}, parent + ".Footer.BTN.Remove.Item");

				#endregion

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "50 0",
						OffsetMax = "0 0"
					},
					Text =
					{
						Text = Msg(player, EditingBtnRemove),
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-bold.ttf",
						FontSize = 22,
						Color = HexToCuiColor("#FDEDE9")
					}
				}, parent + ".Footer.BTN.Remove.Item");

				container.Add(new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text = {Text = string.Empty},
					Button =
					{
						Color = "0 0 0 0",
						Close = closeLayer,
						Command =
							!string.IsNullOrEmpty(cmdRemove)
								? cmdRemove
								: $"{CMD_Main_Console} {mainCommand} remove {string.Join(" ", obj)}"
					}
				}, parent + ".Footer.BTN.Remove.Item");
			}

			#endregion

			#region Save

			if (hasSave)
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "-220 -15",
						OffsetMax = "-20 15"
					},
					Text =
					{
						Text = Msg(player, EditingBtnSave),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 22,
						Color = HexToCuiColor("#FDEDE9")
					},
					Button =
					{
						Color = HexToCuiColor("#519229"),
						Close = closeLayer,
						Command =
							!string.IsNullOrEmpty(cmdSave) ? cmdSave : $"{CMD_Main_Console} {mainCommand} save"
					}
				}, parent + ".Footer", parent + ".Footer.BTN.Save.Item");

			#endregion
		}

		private void SelectItemElementUI(BasePlayer player,
			string command,
			ref CuiElementContainer container,
			string parent,
			CuiRectTransformComponent position,
			Action<CuiElementContainer, string> onImage = null)
		{
			container.Add(new
				CuiElement
				{
					Name = parent + ".Preview.Item",
					Parent = parent,
					Components =
					{
						new CuiImageComponent
						{
							Color = HexToCuiColor("#2F3134")
						},
						position
					}
				});

			#region Image

			onImage?.Invoke(container, parent + ".Preview.Item");

			#endregion

			#region Button

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 0",
					OffsetMin = "0 -30",
					OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, EditingSelectItem),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = HexToCuiColor("#FDEDE9")
				},
				Button =
				{
					Color = HexToCuiColor("#228BA1"),
					Command = command
				}
			}, parent + ".Preview.Item");

			#endregion
		}

		private void EditArrayUI(BasePlayer player,
			EditData editData,
			int selectedTab = 0,
			int page = 0)
		{
			#region Fields

			if (editData == null) return;

			if (!TryGetArrayProperties(editData, selectedTab, out var target, out var array, out var propterties,
				    out var isSystemTypes))
				return;

			var container = new CuiElementContainer();

			#endregion

			#region Background

			var bgLayer = container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1"
				},
				Image =
				{
					Color = HexToCuiColor("#000000", 98)
				}
			}, "Overlay", SecondModalLayer + ".Array", SecondModalLayer + ".Array");

			#endregion

			#region Main

			var mainLayer = container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-480 -275",
					OffsetMax = "480 275"
				},
				Image =
				{
					Color = HexToCuiColor("#202224")
				}
			}, bgLayer, SecondModalMainLayer + ".Array", SecondModalMainLayer + ".Array");

			#endregion

			#region Header

			#region Title

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 0",
					OffsetMax = "0 50"
				},
				Text =
				{
					Text = Msg(player, EditingModalTitle, target.Name),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 24,
					Color = HexToCuiColor("#DCDCDC")
				}
			}, mainLayer);

			#endregion

			#region Close

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-30 15",
					OffsetMax = "0 45"
				},
				Text =
				{
					Text = Msg(player, EditingBtnClose),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 22,
					Color = HexToCuiColor("#EF5125")
				},
				Button =
				{
					Color = "0 0 0 0",
					Close = bgLayer,
					Command = $"{CMD_Main_Console} {editData.MainCommand} array close"
				}
			}, mainLayer, mainLayer + ".BTN.Close.Edit");

			#endregion

			#endregion

			#region Content

			var constSwitch = 130f;
			var fieldsOnLine = 3;

			var xSwitch = constSwitch;
			var ySwitch = -20f;

			var index = 1;

			if (propterties != null)
				foreach (var property in propterties)
					if (property.HasValues)
						PropertyUI(
							player,
							property,
							mainLayer,
							ref container,
							property.Name,
							editData.MainCommand,
							isSystemTypes
								? $"array change_system {target.Path.ReplaceSpaceToUnicode()} {selectedTab} {page}"
								: $"array change {selectedTab} {page}",
							ref index,
							ref fieldsOnLine,
							UI_PROPERTY_TITLE_HEIGHT,
							ref xSwitch,
							ref ySwitch,
							ref constSwitch,
							UI_PROPERTY_WIDTH,
							UI_PROPERTY_HEIGHT,
							UI_PROPERTY_MARGIN_Y,
							UI_PROPERTY_MARGIN_X);

			#endregion

			#region Footer

			EditingFooterUI(player, ref container, mainLayer, mainLayer, editData.MainCommand,
				135f, 0f,
				editData.IsGenerated == false,
				$"{CMD_Main_Console} {editData.MainCommand} array remove {selectedTab} {page}",
				false, string.Empty);

			#endregion

			#region Pages

			var pagesOnScreen = 15;

			#region Buttons

			if (array.Count + 1 > pagesOnScreen)
			{
				var pagesCount = Mathf.CeilToInt((float) (array.Count + 1) / pagesOnScreen);

				#region Back

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = "15 -55",
						OffsetMax = "50 -20"
					},
					Text =
					{
						Text = Msg(player, EditingBtnUp),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 16,
						Color = page != 0 ? HexToCuiColor("#EF5125") : HexToCuiColor("#FFFFFF", 20)
					},
					Button =
					{
						Color = "0 0 0 0",
						Close = mainLayer + ".Btn.Back",
						Command = page != 0
							? $"{CMD_Main_Console} {editData.MainCommand} array page {selectedTab} {page - 1}"
							: string.Empty
					}
				}, mainLayer, mainLayer + ".Btn.Back");

				#endregion

				#region Next

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "0 0",
						OffsetMin = "15 20",
						OffsetMax = "50 55"
					},
					Text =
					{
						Text = Msg(player, EditingBtnDown),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 16,
						Color = pagesCount > page ? HexToCuiColor("#EF5125") : HexToCuiColor("#FFFFFF", 20)
					},
					Button =
					{
						Color = "0 0 0 0",
						Close = mainLayer + ".Btn.Next",
						Command = pagesCount > page
							? $"{CMD_Main_Console} {editData.MainCommand} array page {selectedTab} {page + 1}"
							: string.Empty
					}
				}, mainLayer, mainLayer + ".Btn.Next");

				#endregion

				#region Progress

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0",
						AnchorMax = "0 1",
						OffsetMin = "22.5 60",
						OffsetMax = "32.5 -60"
					},
					Image =
					{
						Color = HexToCuiColor("#38393F")
					}
				}, mainLayer, mainLayer + ".Progress.Background");

				if (pagesCount > 1)
				{
					var size = 1.0 / pagesCount;

					var pSwitch = 0.0;

					for (var y = pagesCount - 1; y >= 0; y--)
					{
						container.Add(new CuiButton
						{
							RectTransform = {AnchorMin = $"0 {pSwitch}", AnchorMax = $"1 {pSwitch + size}"},
							Text = {Text = string.Empty},
							Button =
							{
								Command = $"{CMD_Main_Console} {editData.MainCommand} array page {selectedTab} {y}",
								Color = y == page ? HexToCuiColor("#808285") : "0 0 0 0"
							}
						}, mainLayer + ".Progress.Background");

						pSwitch += size;
					}
				}

				#endregion
			}

			#endregion

			#region Table

			var pages = array.Skip(page * pagesOnScreen).Take(pagesOnScreen).ToArray();

			var btnUpIndent = 15f;
			var btnHeight = 30f;
			var btnWidth = 50f;
			var btnLeftIndent = 55f;
			var btnMarginY = 5f;

			for (var i = 0; i < pages.Length; i++)
			{
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0 1",
						AnchorMax = "0 1",
						OffsetMin = $"{btnLeftIndent} -{btnUpIndent + btnHeight}",
						OffsetMax = $"{btnLeftIndent + btnWidth} -{btnUpIndent}"
					},
					Text =
					{
						Text = $"{i + 1}",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 16,
						Color = selectedTab == i ? HexToCuiColor("#FDEDE9") : HexToCuiColor("#FDEDE9", 20)
					},
					Button =
					{
						Color = selectedTab == i ? HexToCuiColor("#228BA1") : HexToCuiColor("#2F3134"),
						Command = $"{CMD_Main_Console} {editData.MainCommand} array page {i} {page}"
					}
				}, mainLayer);

				btnUpIndent = btnUpIndent + btnHeight + btnMarginY;
			}

			if (pages.Length < pagesOnScreen)
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0 1",
						AnchorMax = "0 1",
						OffsetMin = $"{btnLeftIndent} -{btnUpIndent + btnHeight}",
						OffsetMax = $"{btnLeftIndent + btnWidth} -{btnUpIndent}"
					},
					Text =
					{
						Text = Msg(player, EditingBtnAdd),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 16,
						Color = HexToCuiColor("#FDEDE9", 20)
					},
					Button =
					{
						Color = HexToCuiColor("#2F3134"),
						Command = $"{CMD_Main_Console} {editData.MainCommand} array add {selectedTab} {page}"
					}
				}, mainLayer);

			#endregion

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private void ShowImageModalUI(BasePlayer player, EditData editData, string targetProperty,
			Action<string> onFinish = null)
		{
			if (editData == null) return;

			var container = new CuiElementContainer();

			container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1"
					},
					Image =
					{
						Color = HexToCuiColor("#000000", 98)
					},
					CursorEnabled = true
				}, "Overlay",
				ModalLayer + ".Show.Preview",
				ModalLayer + ".Show.Preview");

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text = {Text = string.Empty},
				Button =
				{
					Color = "0 0 0 0",
					Close = ModalLayer + ".Show.Preview",
					Command = $"{CMD_Main_Console} close_preview"
				}
			}, ModalLayer + ".Show.Preview");

			container.Add(new CuiElement
			{
				Name = ModalLayer + ".Show.Preview.Background",
				Parent = ModalLayer + ".Show.Preview",
				Components =
				{
					new CuiImageComponent
					{
						Color = HexToCuiColor("#2F3134")
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
						OffsetMin = "-120 -90",
						OffsetMax = "120 90"
					},
					new CuiOutlineComponent
					{
						Color = HexToCuiColor("#575757"),
						Distance = "1 -1"
					}
				}
			});

			var notImage = true;

			if (!string.IsNullOrEmpty(targetProperty))
			{
				var property = GetPropertyByPath(targetProperty, editData.Object);
				if (property != null)
				{
					var val = property.Value.ToString();
					if (!string.IsNullOrEmpty(val))
					{
						if (property.Name.Contains("ShortName"))
						{
							var def = ItemManager.FindItemDefinition(property.Value.ToString());
							if (def != null)
							{
								var skin = TryGetSkinFromProperty(targetProperty, property, editData.Object);

								container.Add(new CuiPanel
								{
									RectTransform =
									{
										AnchorMin = "0.5 0.5",
										AnchorMax = "0.5 0.5",
										OffsetMin = "-60 -60",
										OffsetMax = "60 60"
									},
									Image =
									{
										ItemId = def.itemid,
										SkinId = skin
									}
								}, ModalLayer + ".Show.Preview.Background");

								notImage = false;
							}
						}
						else
						{
							if (_enabledImageLibrary && HasImage(val))
							{
								container.Add(new CuiElement
								{
									Parent = ModalLayer + ".Show.Preview.Background",
									Components =
									{
										new CuiRawImageComponent
										{
											Png = GetImage(val)
										},
										new CuiRectTransformComponent
										{
											AnchorMin = "0.5 0.5",
											AnchorMax = "0.5 0.5",
											OffsetMin = "-60 -60",
											OffsetMax = "60 60"
										}
									}
								});

								notImage = false;
							}
						}
					}
				}
			}

			if (notImage && _enabledImageLibrary)
				container.Add(new CuiElement
				{
					Parent = ModalLayer + ".Show.Preview.Background",
					Components =
					{
						new CuiRawImageComponent
						{
							Png = GetImage("NONE")
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0.5 0.5",
							AnchorMax = "0.5 0.5",
							OffsetMin = "-60 -60",
							OffsetMax = "60 60"
						}
					}
				});

			CuiHelper.AddUi(player, container);

			onFinish?.Invoke(ModalLayer + ".Show.Preview");
		}

		private int UI_SELECT_ITEM_AMOUNT_ON_STRING = 20;

		private float
			UI_SELECT_ITEM_WIDTH = 58f,
			UI_SELECT_ITEM_HEIGHT = 70f,
			UI_SELECT_ITEM_MARGIN = 5f;

		private void SelectItemUI(BasePlayer player,
			EditData editData,
			int selectedCategory = 0)
		{
			if (editData == null) return;

			var container = new CuiElementContainer();

			#region Background

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1"
				},
				Image =
				{
					Color = HexToCuiColor("#202224")
				}
			}, "Overlay", SelectItemModalLayer, SelectItemModalLayer);

			#endregion

			#region Tabs

			var tabWidth = 80f;
			var tabMarginY = 5f;

			var xSwitch = -(_itemsCategories.Keys.Count * tabWidth + (_itemsCategories.Keys.Count - 1) * tabMarginY) /
			              2f;

			for (var i = 0; i < _itemsCategories.Keys.Count; i++)
			{
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = $"{xSwitch} -30",
						OffsetMax = $"{xSwitch + tabWidth} -10"
					},
					Text =
					{
						Text = _itemsCategories.Keys[i],
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = selectedCategory == i ? HexToCuiColor("#228BA1") : HexToCuiColor("#2F3134"),
						Command = $"{CMD_Main_Console} {editData.MainCommand} select page {i}"
					}
				}, SelectItemModalLayer);

				xSwitch += tabWidth + tabMarginY;
			}

			#endregion

			#region Close

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-30 -30",
					OffsetMax = "-10 -10"
				},
				Text = {Text = string.Empty},
				Button =
				{
					Color = HexToCuiColor("#EF5125"),
					Command = $"{CMD_Main_Console} {editData.MainCommand} select close",
					Sprite = "assets/icons/close.png",
					Close = SelectItemModalLayer
				}
			}, SelectItemModalLayer);

			#endregion

			#region Items

			var ySwitch = 45f;

			var constSwitchX = -(UI_SELECT_ITEM_AMOUNT_ON_STRING * UI_SELECT_ITEM_WIDTH +
			                     (UI_SELECT_ITEM_AMOUNT_ON_STRING - 1) * UI_SELECT_ITEM_MARGIN) / 2f;
			xSwitch = constSwitchX;

			var items = _itemsCategories.Values[selectedCategory];
			for (var i = 0; i < items.Count; i++)
			{
				var def = ItemManager.FindItemDefinition(items[i]);
				if (def == null) continue;

				container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = $"{xSwitch} -{ySwitch + UI_SELECT_ITEM_HEIGHT}",
							OffsetMax = $"{xSwitch + UI_SELECT_ITEM_WIDTH} -{ySwitch}"
						},
						Image =
						{
							Color = "0 0 0 0"
						}
					}, SelectItemModalLayer, SelectItemModalLayer + $".Item.{i}");

				#region Image

				container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = "-25 -55",
							OffsetMax = "25 -5"
						},
						Image =
						{
							ItemId = def.itemid
						}
					}, SelectItemModalLayer + $".Item.{i}");

				#endregion

				#region Title

				container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0",
							OffsetMin = "0 0",
							OffsetMax = "0 15"
						},
						Text =
						{
							Text = $"{GetItemName(player, def.shortname)}",
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 8,
							Color = "1 1 1 1"
						}
					}, SelectItemModalLayer + $".Item.{i}");

				#endregion

				container.Add(new CuiButton
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text = {Text = string.Empty},
						Button =
						{
							Color = "0 0 0 0",
							Close = SelectItemModalLayer,
							Command = $"{CMD_Main_Console} {editData.MainCommand} select select {def.itemid}"
						}
					}, SelectItemModalLayer + $".Item.{i}");

				if ((i + 1) % UI_SELECT_ITEM_AMOUNT_ON_STRING == 0)
				{
					ySwitch = ySwitch + UI_SELECT_ITEM_HEIGHT + UI_SELECT_ITEM_MARGIN;
					xSwitch = constSwitchX;
				}
				else
				{
					xSwitch += UI_SELECT_ITEM_WIDTH + UI_SELECT_ITEM_MARGIN;
				}
			}

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private void SelectColorUI(BasePlayer player,
			EditData editData)
		{
			if (editData == null) return;

			if (GetPropertyByPath(editData.GetSelectProperty(), editData.Object).Value
				    ?.TryParseObject(out IColor selectedColor) != true)
				return;

			var container = new CuiElementContainer();

			#region Background

			var bgLayer = container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1"
				},
				Image =
				{
					Color = HexToCuiColor("#000000", 98)
				}
			}, "Overlay", SelectItemModalLayer, SelectItemModalLayer);

			#endregion

			#region Main

			var mainLayer = container.Add(new CuiPanel
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
			}, bgLayer, SecondModalMainLayer, SecondModalMainLayer);

			#region Header

			#region Title

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 0",
					OffsetMax = "0 40"
				},
				Text =
				{
					Text = Msg(player, EditingSelectColorTitle),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 22,
					Color = HexToCuiColor("#DCDCDC")
				}
			}, mainLayer);

			#endregion

			#region Close

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-30 5",
					OffsetMax = "0 35"
				},
				Text =
				{
					Text = Msg(player, EditingBtnClose),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 22,
					Color = HexToCuiColor("#EF5125")
				},
				Button =
				{
					Color = "0 0 0 0",
					Close = bgLayer,
					Command = $"{CMD_Main_Console} {editData.MainCommand} color close"
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

					container.Add(new CuiButton
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
							Command =
								$"{CMD_Main_Console} {editData.MainCommand} color set hex {ColorUtility.ToHtmlStringRGB(targetColor)}"
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

				container.Add(new CuiElement
				{
					Name = mainLayer + ".Selected.Color",
					Parent = mainLayer,
					Components =
					{
						new CuiImageComponent
						{
							Color = selectedColor.Get
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

				container.Add(new CuiLabel
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
						Text = Msg(player, EditingSelectedColor),
						Align = TextAnchor.UpperLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 14,
						Color = "1 1 1 1"
					}
				}, mainLayer + ".Selected.Color");

				#endregion

				#region Input

				#region HEX

				container.Add(new CuiElement
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

				container.Add(new CuiLabel
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
						Text = Msg(player, EditingSelectColorHEX),
						Align = TextAnchor.UpperLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = HexToCuiColor("#FFFFFF")
					}
				}, mainLayer + ".Selected.Color.Input.HEX");

				container.Add(new CuiElement
				{
					Parent = mainLayer + ".Selected.Color.Input.HEX",
					Components =
					{
						new CuiInputFieldComponent
						{
							FontSize = 10,
							Align = TextAnchor.MiddleCenter,
							Command = $"{CMD_Main_Console} {editData.MainCommand} color set hex",
							Color = HexToCuiColor("#575757"),
							CharsLimit = 150,
							Text = $"{selectedColor.Hex}",
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

				container.Add(new CuiElement
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

				container.Add(new CuiLabel
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
						Text = Msg(player, EditingSelectColorOpacity),
						Align = TextAnchor.UpperLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = HexToCuiColor("#FFFFFF")
					}
				}, mainLayer + ".Selected.Color.Input.Opacity");

				container.Add(new CuiElement
				{
					Parent = mainLayer + ".Selected.Color.Input.Opacity",
					Components =
					{
						new CuiInputFieldComponent
						{
							FontSize = 10,
							Align = TextAnchor.MiddleCenter,
							Command = $"{CMD_Main_Console} {editData.MainCommand} color set opacity",
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

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private float
			UI_PROPERTY_WIDTH = 255f,
			UI_PROPERTY_HEIGHT = 30f,
			UI_PROPERTY_MARGIN_Y = 30f,
			UI_PROPERTY_MARGIN_X = 20f,
			UI_PROPERTY_TITLE_HEIGHT = 20f,
			UI_EDITING_FOOTER_HEIGHT = 50f;

		private int PropertyUI(
			BasePlayer player,
			JProperty property,
			string parent,
			ref CuiElementContainer container,
			string propertyTitle,
			string commandMain,
			string commandToEditProperty,
			ref int index,
			ref int fieldsOnLine,
			float fieldTitleHeight,
			ref float xSwitch,
			ref float ySwitch,
			ref float constSwitch,
			float fieldWidth,
			float fieldHeight,
			float fieldMarginY,
			float fieldMarginX,
			bool useCalculator = true
		)
		{
			var linesAmount = 0;

			if (property.Value.Type == JTokenType.Object)
			{
				var nestedIndex = 1;
				var nestedFieldsOnLine = 3;

				var nestedObject = (JObject) property.Value;
				var nestedProperties = nestedObject.Properties().ToArray();

				var nestedFieldMarginY = fieldMarginY + 25;

				ySwitch = ySwitch - fieldHeight - nestedFieldMarginY;

				linesAmount = 1;

				xSwitch = constSwitch;

				#region Background

				var nestedLines = Mathf.CeilToInt((float) nestedProperties.Length / nestedFieldsOnLine);

				container.Add(new CuiElement
				{
					Name = parent + ".Nested.Background",
					Parent = parent,
					Components =
					{
						new CuiImageComponent
						{
							Color = HexToCuiColor("#000000", 95)
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 1", AnchorMax = "0 1",
							OffsetMin =
								$"{-10f + constSwitch} {ySwitch - ((fieldTitleHeight + fieldHeight) * nestedLines + (nestedLines - 1) * fieldMarginY) - 10f}",
							OffsetMax =
								$"{10f + (constSwitch + nestedFieldsOnLine * fieldWidth + (nestedFieldsOnLine - 1) * fieldMarginX)} {ySwitch + 20f}"
						},
						new CuiOutlineComponent
						{
							Color = HexToCuiColor("#575757"),
							Distance = "1 -1"
						}
					}
				});

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "0 -25",
						OffsetMax = "0 -5"
					},
					Text =
					{
						Text = $"[{property.Name}]",
						Align = TextAnchor.UpperCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					}
				}, parent + ".Nested.Background");

				#endregion

				for (var i = 0; i < nestedProperties.Length; i++)
				{
					var nestedProperty = nestedProperties[i];

					var calc = i != nestedProperties.Length - 1;

					linesAmount += PropertyUI(
						player,
						nestedProperty,
						parent,
						ref container,
						nestedProperty.Name,
						commandMain,
						commandToEditProperty,
						ref nestedIndex,
						ref nestedFieldsOnLine,
						fieldTitleHeight,
						ref xSwitch,
						ref ySwitch,
						ref constSwitch,
						fieldWidth,
						fieldHeight,
						fieldMarginY,
						fieldMarginX,
						calc);
				}

				return linesAmount;
			}

			container.Add(new CuiElement
			{
				Parent = parent,
				Name = parent + $".Field.{index}",
				Components =
				{
					new CuiImageComponent
					{
						Color = HexToCuiColor("#2F3134")
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = $"{xSwitch} {ySwitch - fieldTitleHeight - fieldHeight}",
						OffsetMax = $"{xSwitch + fieldWidth} {ySwitch - fieldTitleHeight}"
					},
					new CuiOutlineComponent
					{
						Color = HexToCuiColor("#575757"),
						Distance = "1 -1"
					}
				}
			});

			container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1",
						AnchorMax = "1 1",
						OffsetMin = "0 0",
						OffsetMax = $"0 {fieldTitleHeight}"
					},
					Text =
					{
						Text = propertyTitle,
						Align = TextAnchor.UpperLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = HexToCuiColor("#FFFFFF")
					}
				}, parent + $".Field.{index}");

			switch (property.Value.Type)
			{
				case JTokenType.Boolean:
				{
					var val = Convert.ToBoolean(property.Value);

					container.Add(new CuiButton
						{
							RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
							Text =
							{
								Text = Msg(player, val ? EditingBtnAccept : EditingBtnCancel),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-bold.ttf",
								FontSize = 16,
								Color = HexToCuiColor("#FDEDE9")
							},
							Button =
							{
								Color = val ? HexToCuiColor("#519229") : HexToCuiColor("#575757"),
								Command =
									$"{CMD_Main_Console} {commandMain} {commandToEditProperty} {property.Path.ReplaceSpaceToUnicode()} {!val}"
							}
						}, parent + $".Field.{index}");
					break;
				}

				case JTokenType.Array:
				{
					container.Add(new CuiButton
						{
							RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
							Text =
							{
								Text = Msg(player, EditingBtnOpen),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-bold.ttf",
								FontSize = 16,
								Color = HexToCuiColor("#FDEDE9")
							},
							Button =
							{
								Color = HexToCuiColor("#575757"),
								Command =
									$"{CMD_Main_Console} {commandMain} array start {property.Path.ReplaceSpaceToUnicode()}"
							}
						}, parent + $".Field.{index}");
					break;
				}

				default:
				{
					if (Enum.TryParse(property.Value.ToString(), out AwardInfo.ItemType itemType) &&
					    Enum.IsDefined(typeof(AwardInfo.ItemType), itemType))
					{
						FieldEnumUI(player, parent + $".Field.{index}", ref container, itemType.ToString(),
							$"{CMD_Main_Console} {commandMain} enum {property.Path.ReplaceSpaceToUnicode()} back",
							$"{CMD_Main_Console} {commandMain} enum {property.Path.ReplaceSpaceToUnicode()} next");
					}
					else
					{
						var val = property.Value.ToString();

						container.Add(new CuiElement
						{
							Parent = parent + $".Field.{index}",
							Components =
							{
								new CuiInputFieldComponent
								{
									FontSize = 10,
									Align = TextAnchor.MiddleLeft,
									Command =
										$"{CMD_Main_Console} {commandMain} {commandToEditProperty} {property.Path.ReplaceSpaceToUnicode()}",
									Color = HexToCuiColor("#575757"),
									CharsLimit = 150,
									Text = !string.IsNullOrEmpty(val)
										? val
										: string.Empty,
									NeedsKeyboard = true
								},
								new CuiRectTransformComponent
								{
									AnchorMin = "0 0", AnchorMax = "1 1",
									OffsetMin = "10 0", OffsetMax = "0 0"
								}
							}
						});

						if (property.Value.Type == JTokenType.String)
						{
							var isShortname = property.Name.Contains("ShortName");
							if (isShortname || property.Name.Contains("Image") || property.Name.Contains("Изображение"))
							{
								var elementName = CuiHelper.GetGuid();

								container.Add(new CuiElement
								{
									Name = elementName,
									Parent = parent + $".Field.{index}",
									Components =
									{
										new CuiRawImageComponent
										{
											Png = GetImage(_config.UI.SelectItemImage),
											Color = HexToCuiColor("#575757")
										},
										new CuiRectTransformComponent
										{
											AnchorMin = "1 0.5", AnchorMax = "1 0.5",
											OffsetMin = "-15 -5",
											OffsetMax = "-5 5"
										}
									}
								});

								container.Add(new CuiButton
								{
									RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
									Text = {Text = string.Empty},
									Button =
									{
										Color = "0 0 0 0",
										Command =
											$"{CMD_Main_Console} {commandMain} show_modal {property.Path.ReplaceSpaceToUnicode()}"
									}
								}, elementName);

								if (isShortname)
									container.Add(new CuiButton
									{
										RectTransform =
										{
											AnchorMin = "1 0.5", AnchorMax = "1 0.5",
											OffsetMin = "-40 -10",
											OffsetMax = "-20 10"
										},
										Text =
										{
											Text = Msg(player, EditingBtnAdd),
											Align = TextAnchor.MiddleCenter,
											Font = "robotocondensed-bold.ttf",
											FontSize = 16,
											Color = HexToCuiColor("#EF5125")
										},
										Button =
										{
											Color = "0 0 0 0",
											Command =
												$"{CMD_Main_Console} {commandMain} select start {property.Path.ReplaceSpaceToUnicode()}"
										}
									}, elementName);
							}
						}
					}

					break;
				}
			}

			#region Calculate Position

			if (useCalculator)
			{
				if (index % fieldsOnLine == 0)
				{
					ySwitch = ySwitch - fieldHeight - fieldMarginY;

					linesAmount++;

					if (index == 6)
					{
						constSwitch = 20;
						fieldsOnLine = 3;
					}

					xSwitch = constSwitch;
				}
				else
				{
					xSwitch += fieldWidth + fieldMarginX;
				}
			}

			#endregion

			index++;

			return linesAmount;
		}

		private void PropertyWithBackgroundUI(
			BasePlayer player,
			JProperty property,
			string parent,
			ref CuiElementContainer container,
			string propertyTitle,
			string commandMain,
			string commandToEditProperty,
			ref int index,
			float fieldTitleHeight,
			ref float xSwitch,
			ref float ySwitch,
			float fieldWidth,
			float fieldHeight
		)
		{
			container.Add(new CuiElement
			{
				Parent = parent,
				Name = parent + $".Field.{index}.Input",
				Components =
				{
					new CuiImageComponent
					{
						Color = HexToCuiColor("#2F3134")
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = $"{xSwitch} {ySwitch - fieldTitleHeight - fieldHeight}",
						OffsetMax = $"{xSwitch + fieldWidth} {ySwitch - fieldTitleHeight}"
					},
					new CuiOutlineComponent
					{
						Color = HexToCuiColor("#575757"),
						Distance = "1 -1"
					}
				}
			});

			container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1",
						AnchorMax = "1 1",
						OffsetMin = "0 0",
						OffsetMax = $"0 {fieldTitleHeight}"
					},
					Text =
					{
						Text = propertyTitle,
						Align = TextAnchor.UpperLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = HexToCuiColor("#FFFFFF")
					}
				}, parent + $".Field.{index}.Input");

			switch (property.Value.Type)
			{
				case JTokenType.Boolean:
				{
					var val = Convert.ToBoolean(property.Value);

					container.Add(new CuiButton
						{
							RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
							Text =
							{
								Text = Msg(player, val ? EditingBtnAccept : EditingBtnCancel),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-bold.ttf",
								FontSize = 16,
								Color = HexToCuiColor("#FDEDE9")
							},
							Button =
							{
								Color = val ? HexToCuiColor("#519229") : HexToCuiColor("#575757"),
								Command =
									$"{CMD_Main_Console} {commandMain} {commandToEditProperty} {property.Path.ReplaceSpaceToUnicode()} {!val}"
							}
						}, parent + $".Field.{index}.Input");
					break;
				}

				case JTokenType.Array:
				{
					container.Add(new CuiButton
						{
							RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
							Text =
							{
								Text = Msg(player, EditingBtnOpen),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-bold.ttf",
								FontSize = 16,
								Color = HexToCuiColor("#FDEDE9")
							},
							Button =
							{
								Color = HexToCuiColor("#575757"),
								Command =
									$"{CMD_Main_Console} {commandMain} array start {property.Path.ReplaceSpaceToUnicode()}"
							}
						}, parent + $".Field.{index}.Input");
					break;
				}

				case JTokenType.Object:
				{
					if (property.Value?.TryParseObject(out IColor color) == true && color != null)
						container.Add(new CuiButton
							{
								RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
								Text =
								{
									Text = $"{color.Hex}",
									Align = TextAnchor.MiddleCenter,
									Font = "robotocondensed-bold.ttf",
									FontSize = 16,
									Color = HexToCuiColor("#FDEDE9")
								},
								Button =
								{
									Color = color.Get,
									Command =
										$"{CMD_Main_Console} {commandMain} color start {property.Path.ReplaceSpaceToUnicode()}"
								}
							}, parent + $".Field.{index}.Input");

					break;
				}

				default:
				{
					if (Enum.TryParse(property.Value.ToString(), out AwardInfo.ItemType itemType) &&
					    Enum.IsDefined(typeof(AwardInfo.ItemType), itemType))
					{
						FieldEnumUI(player, parent + $".Field.{index}.Input", ref container, itemType.ToString(),
							$"{CMD_Main_Console} {commandMain} enum {property.Path.ReplaceSpaceToUnicode()} back",
							$"{CMD_Main_Console} {commandMain} enum {property.Path.ReplaceSpaceToUnicode()} next");
					}
					else
					{
						var val = property.Value.ToString();

						container.Add(new CuiElement
						{
							Parent = parent + $".Field.{index}.Input",
							Components =
							{
								new CuiInputFieldComponent
								{
									FontSize = 10,
									Align = TextAnchor.MiddleLeft,
									Command =
										$"{CMD_Main_Console} {commandMain} {commandToEditProperty} {property.Path.ReplaceSpaceToUnicode()}",
									Color = HexToCuiColor("#575757"),
									CharsLimit = 150,
									Text = !string.IsNullOrEmpty(val)
										? val
										: string.Empty,
									NeedsKeyboard = true
								},
								new CuiRectTransformComponent
								{
									AnchorMin = "0 0", AnchorMax = "1 1",
									OffsetMin = "10 0", OffsetMax = "0 0"
								}
							}
						});

						if (property.Value.Type == JTokenType.String)
						{
							var isShortname = property.Name.Contains("ShortName");
							if (isShortname || property.Name.Contains("Image") || property.Name.Contains("Изображение"))
							{
								var elementName = CuiHelper.GetGuid();

								container.Add(new CuiElement
								{
									Name = elementName,
									Parent = parent + $".Field.{index}.Input",
									Components =
									{
										new CuiRawImageComponent
										{
											Png = GetImage(_config.UI.SelectItemImage),
											Color = HexToCuiColor("#575757")
										},
										new CuiRectTransformComponent
										{
											AnchorMin = "1 0.5", AnchorMax = "1 0.5",
											OffsetMin = "-15 -5",
											OffsetMax = "-5 5"
										}
									}
								});

								container.Add(new CuiButton
								{
									RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
									Text = {Text = string.Empty},
									Button =
									{
										Color = "0 0 0 0",
										Command =
											$"{CMD_Main_Console} {commandMain} show_modal {property.Path.ReplaceSpaceToUnicode()}"
									}
								}, elementName);

								if (isShortname)
									container.Add(new CuiButton
									{
										RectTransform =
										{
											AnchorMin = "1 0.5", AnchorMax = "1 0.5",
											OffsetMin = "-40 -10",
											OffsetMax = "-20 10"
										},
										Text =
										{
											Text = Msg(player, EditingBtnAdd),
											Align = TextAnchor.MiddleCenter,
											Font = "robotocondensed-bold.ttf",
											FontSize = 16,
											Color = HexToCuiColor("#EF5125")
										},
										Button =
										{
											Color = "0 0 0 0",
											Command =
												$"{CMD_Main_Console} {commandMain} select start {property.Path.ReplaceSpaceToUnicode()}"
										}
									}, elementName);
							}
						}
					}

					break;
				}
			}
		}

		#region Components

		private void FieldEnumUI(BasePlayer player,
			string parent,
			ref CuiElementContainer container,
			string value,
			string cmdBack,
			string cmdNext)
		{
			container.Add(new CuiLabel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text =
				{
					Text = $"{value}",
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				}
			}, parent);

			#region Back

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "0 1",
					OffsetMin = "0 0",
					OffsetMax = "15 0"
				},
				Text =
				{
					Text = Msg(player, EditingBtnBack),
					Align = TextAnchor.MiddleRight,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = "0 0 0 0",
					Command = cmdBack
					// $"{CMD_Main_Console} {commandMain} enum {property.Path.ReplaceSpaceToUnicode()} back"
				}
			}, parent);

			#endregion

			#region Right

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 0", AnchorMax = "1 1",
					OffsetMin = "-15 0",
					OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, EditingBtnNext),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = "0 0 0 0",
					Command = cmdNext
					// $"{CMD_Main_Console} {commandMain} enum {property.Path.ReplaceSpaceToUnicode()} next"
				}
			}, parent);

			#endregion
		}

		#endregion

		#endregion Interface

		#region Helpers

		#region Items

		private ListDictionary<string, ListHashSet<string>> _itemsCategories = new();

		private void LoadItems()
		{
			ItemManager.itemList.ForEach(item =>
			{
				var itemCategory = item.category.ToString();

				if (_itemsCategories.ContainsKey(itemCategory))
					_itemsCategories[itemCategory].TryAdd(item.shortname);
				else
					_itemsCategories.Add(itemCategory, new ListHashSet<string>
					{
						item.shortname
					});
			});
		}

		#endregion

		#region Data

		private EditData GetEditData(BasePlayer player, string mainCommand)
		{
			switch (mainCommand)
			{
				case "edit_config":
					return GetConfigEditing(player);

				case "edit_day":
					return GetDayEditing(player);

				case "edit_item":
					return GetAwardEditing(player);

				default:
					throw new NotImplementedException("Not implemented edit data!");
			}
		}

		private void RemoveEditing(BasePlayer player)
		{
			RemoveAwardEditing(player);

			RemoveDayEditing(player);

			RemoveConfigEditing(player);
		}

		private List<JTokenType> _editableTokenTypes = new()
		{
			JTokenType.Integer, JTokenType.Float, JTokenType.String, JTokenType.Boolean, JTokenType.Date,
			JTokenType.TimeSpan, JTokenType.Uri
		};

		private class EditData
		{
			public Type Type;

			public JObject Object;

			public bool IsGenerated;

			public string MainCommand;

			public string[] IgnoredProperties;

			public void UpdateObject()
			{
				var newObj = Object.ToObject(Type);

				Object = JObject.FromObject(newObj);
			}

			#region Last Hook

			private List<LastHook> _lastHooks = new();

			public class LastHook
			{
				public string Hook;

				public string TargetProperty;

				public object[] Params;

				public LastHook(string hook, string targetProperty, object[] @params)
				{
					Hook = hook;
					TargetProperty = targetProperty;
					Params = @params;
				}
			}

			public bool RemoveLastHook(string hook, string targetProperty)
			{
				var lastHook = _lastHooks.LastOrDefault(x => x.Hook == hook && x.TargetProperty == targetProperty);
				if (lastHook == null) return false;

				return _lastHooks.Remove(lastHook);
			}

			public void SetLastHook(string hook, params object[] param)
			{
#if TESTING
				Debug.Log($"[SetLastHook] hook={hook}, param={string.Join(", ", param)}");
#endif

				SetLastHookAndProperty(hook, string.Empty, param);
			}

			public void SetLastHookAndProperty(string hook, string targetProperty, params object[] param)
			{
				if (_lastHooks.Count > 0)
				{
					var lastHook = _lastHooks.LastOrDefault(x =>
						x.Hook == hook && x.TargetProperty == targetProperty && x.Params == param);
					if (lastHook != null) return;
				}

				_lastHooks.Add(new LastHook(hook, targetProperty, param));
			}

			public void CallLastHook(BasePlayer player, string ignoredHook = "", string ignoredProperty = "")
			{
				if (_lastHooks.Count <= 0)
					return;

				var hasIgnoredHook = !string.IsNullOrEmpty(ignoredHook);
				var hasIgnoredProperty = !string.IsNullOrEmpty(ignoredProperty);

				LastHook lastHook;
				if (hasIgnoredHook || hasIgnoredProperty)
					lastHook = _lastHooks.FindAll(x =>
						(!hasIgnoredHook || x.Hook != ignoredHook) &&
						(!hasIgnoredProperty || x.TargetProperty != ignoredProperty)).LastOrDefault();
				else
					lastHook = _lastHooks.LastOrDefault();

				if (lastHook == null || string.IsNullOrEmpty(lastHook.Hook))
					return;

				var paramsToCall = new List<object>
				{
					player
				};

				if (lastHook.Params.Length > 0)
					foreach (var param in lastHook.Params)
					{
						var key = param as string;
						if (!string.IsNullOrEmpty(key) && key == "edit_data")
						{
							var editData = _instance?.GetEditData(player, MainCommand);
							if (editData != null)
								paramsToCall.Add(editData);
						}
						else
						{
							paramsToCall.Add(param);
						}
					}

				if (!string.IsNullOrEmpty(lastHook.TargetProperty))
					SetTargetProperty(lastHook.TargetProperty);

				_instance?.Call(lastHook.Hook, paramsToCall.ToArray());
			}

			#endregion

			#region Target Property

			private string TargetProperty;

			public void SetTargetProperty(string newProperty)
			{
				TargetProperty = newProperty;
			}

			public string GetTargetProperty()
			{
				return TargetProperty;
			}

			public void ClearTargetProperty()
			{
				TargetProperty = null;
			}

			#endregion

			#region Select Property

			private string SelectProperty;

			public void SetSelectProperty(string newProperty)
			{
#if TESTING
				Debug.Log($"[SetSelectProperty] before: {SelectProperty}");
#endif

				SelectProperty = newProperty;

#if TESTING
				Debug.Log($"[SetSelectProperty] after: {SelectProperty}");
#endif
			}

			public string GetSelectProperty()
			{
				return SelectProperty;
			}

			public void ClearSelectProperty()
			{
				SelectProperty = null;
			}

			#endregion

			#region Properies

			public JProperty[] GetProperties()
			{
				var properties = Object?.Properties()
					.OrderByDescending(x => x.HasValues && x.Value.Type != JTokenType.Object)
					.ToArray();

				return IgnoredProperties == null
					? properties
					: Array.FindAll(properties ?? Array.Empty<JProperty>(),
						property => !IgnoredProperties.Contains(property.Name));
			}

			#endregion

			#region Main Hooks

			public virtual void Save(BasePlayer player, object obj)
			{
				_instance?.SaveConfig();
			}

			public virtual void Remove(BasePlayer player, string[] args)
			{
				_instance?.SaveConfig();
			}

			public virtual void Close(BasePlayer player, params string[] args)
			{
				// ignore
			}

			#endregion
		}

		#region Day

		private Dictionary<ulong, EditDayData> _editDay = new();

		private class EditDayData : EditData
		{
			public int Day;

			public override void Save(BasePlayer player, object obj)
			{
				if (obj is not AwardDayInfo dayAward) return;

				if (_config.DailyAwards.TryGetValue(Day, out var targetDay))
				{
					dayAward.Awards = targetDay.Awards.ToList();

					_config.DailyAwards[Day] = dayAward;
				}
				else
				{
					_config.DailyAwards.TryAdd(Day, dayAward);
				}

				base.Save(player, obj);

				if (!string.IsNullOrEmpty(dayAward.Image))
					_instance.AddImage(dayAward.Image, dayAward.Image);

				_instance.RemoveDayEditing(player);

				var openedRewardPlayer = _instance.GetOpenedRewardPlayer(player.userID);
				if (openedRewardPlayer != null)
				{
					var targetUI = openedRewardPlayer.GetUI();
					
					var targetPage = Mathf.Max(Mathf.CeilToInt((float) Day / targetUI.AwardsOnLine) - 1, 0);
					
					openedRewardPlayer.OnChangePage(targetPage);
				}
				
				_instance.AwardsUi(player);
			}

			public override void Close(BasePlayer player, params string[] args)
			{
				base.Close(player, args);

				_instance.RemoveDayEditing(player);
			}
		}

		private EditDayData InitDayEditing(BasePlayer player,
			int awardDay,
			string mainCMD)
		{
			if (_editDay.TryGetValue(player.userID, out var editAwardData))
				return editAwardData;

			var isGenerated = false;

			if (!_config.DailyAwards.TryGetValue(awardDay, out var day))
			{
				day = AwardDayInfo.GetDefault();

				isGenerated = true;
			}

			if (day != null)
				editAwardData = new EditDayData
				{
					Type = day.GetType(),
					Day = awardDay,
					Object = JObject.FromObject(day),
					IsGenerated = isGenerated,
					MainCommand = mainCMD,
					IgnoredProperties = new[]
					{
						LangRu ? "Награды" : "Awards"
					}
				};

			return _editDay[player.userID] = editAwardData;
		}

		private EditDayData GetDayEditing(BasePlayer player)
		{
			return _editDay.GetValueOrDefault(player.userID);
		}

		private bool HasDayEditing(BasePlayer player)
		{
			return _editDay.ContainsKey(player.userID);
		}

		private bool RemoveDayEditing(BasePlayer player)
		{
			return _editDay.Remove(player.userID);
		}

		private void StartEditDay(BasePlayer player, int day)
		{
			var editData = InitDayEditing(player, day, "edit_day");
			if (editData == null) return;

			editData.SetLastHook(nameof(AwardInfoUI), day);

			AwardInfoUI(player, day);
		}

		#endregion

		#region Awards

		private Dictionary<ulong, EditAwardData> _editAward = new();

		private class EditAwardData : EditData
		{
			public int Day;

			public int ID;

			public int ModalPage;

			public override void Save(BasePlayer player, object obj)
			{
				var award = obj as AwardInfo;
				if (award == null) return;

				var image = award.Image;

				if (IsGenerated)
				{
					if (_config.DailyAwards.ContainsKey(Day))
					{
						_config.DailyAwards[Day].Awards.Add(award);
					}
					else
					{
						var dayInfo = AwardDayInfo.GetDefault();
						dayInfo.Awards.Add(award);

						_config.DailyAwards.Add(Day, dayInfo);
					}
				}
				else
				{
					if (_config.DailyAwards.TryGetValue(Day, out var dayInfo))
					{
						var targetIndex = dayInfo.Awards.FindIndex(x => x.ID == ID);

						var oldImage = dayInfo.Awards[targetIndex].Image;
						if (award.Image == oldImage)
							image = string.Empty;

						dayInfo.Awards[targetIndex] = award;
					}
				}

				base.Save(player, obj);

				_instance.RegisterPermission(award.Permission);

				if (!string.IsNullOrEmpty(image))
					_instance.AddImage(image, image);

				_instance.RemoveAwardEditing(player);

				_instance.AwardInfoUI(player, Day, ModalPage);
			}

			public override void Remove(BasePlayer player, string[] args)
			{
				if (!args.HasLength()) return;

				var itemID = Convert.ToInt32(args[0]);

				if (!IsGenerated)
					if (_config.DailyAwards.TryGetValue(Day, out var dayInfo))
						dayInfo.Awards.RemoveAll(x => x.ID == itemID);

				base.Remove(player, args);

				_instance.RemoveAwardEditing(player);

				_instance.AwardInfoUI(player, Day, ModalPage);
			}

			public override void Close(BasePlayer player, params string[] args)
			{
				base.Close(player, args);

				_instance.RemoveAwardEditing(player);

				CallLastHook(player);
			}
		}

		private EditAwardData InitAwardEditing(BasePlayer player,
			int awardDay,
			int awardID,
			int modalPage,
			string mainCMD)
		{
			if (_editAward.TryGetValue(player.userID, out var editAwardData))
				return editAwardData;

			AwardInfo award = null;

			var isGenerated = awardID == -1;
			if (isGenerated)
			{
				award = new AwardInfo
				{
					Type = AwardInfo.ItemType.Item,
					ID = _instance.GenerateAwardID(),
					Image = string.Empty,
					Command = string.Empty,
					Kit = string.Empty,
					Plugin = new AwardInfo.PluginItem
					{
						Hook = string.Empty,
						Plugin = string.Empty,
						Amount = 0
					},
					DisplayName = string.Empty,
					ShortName = string.Empty,
					Skin = 0,
					Blueprint = false,
					Amount = 1,
					Content = new AwardInfo.ItemContent
					{
						Enabled = false,
						Contents = new List<AwardInfo.ItemContent.ContentInfo>
						{
							new()
							{
								Enabled = false,
								ShortName = string.Empty,
								Condition = 100,
								Amount = 1,
								Position = -1
							}
						}
					},
					Weapon = new AwardInfo.ItemWeapon
					{
						Enabled = false,
						AmmoType = string.Empty,
						AmmoAmount = 1
					},
					ProhibitSplit = false
				};
			}
			else
			{
				if (_config.DailyAwards.TryGetValue(awardDay, out var dayInfo))
					award = dayInfo.Awards.Find(x => x.ID == awardID);
			}

			if (award != null)
				editAwardData = new EditAwardData
				{
					Type = award.GetType(),
					ID = awardID,
					Day = awardDay,
					ModalPage = modalPage,
					Object = JObject.FromObject(award),
					IsGenerated = isGenerated,
					MainCommand = mainCMD,
					IgnoredProperties = Array.Empty<string>()
				};

			return _editAward[player.userID] = editAwardData;
		}

		private EditAwardData GetAwardEditing(BasePlayer player)
		{
			return _editAward.GetValueOrDefault(player.userID);
		}

		private bool RemoveAwardEditing(BasePlayer player)
		{
			return _editAward.Remove(player.userID);
		}

		private void StartEditAward(BasePlayer player, int day, int awardID, int page)
		{
			var editData = InitAwardEditing(player, day, awardID, page, "edit_item");
			if (editData == null) return;

			editData.SetLastHook(nameof(EditAwardUI));

			EditAwardUI(player);
		}

		#endregion

		#region Config

		private Dictionary<ulong, EditConfigData> _editConfig = new();

		private class EditConfigData : EditData
		{
			public override void Save(BasePlayer player, object obj)
			{
				// var oldConfig = _config;

				var newConfig = obj as Configuration;
				if (newConfig == null) return;

				_config = newConfig;

				base.Save(player, obj);

				_instance.RegisterCommands();

				_instance.RegisterPermissions();

				_instance.LoadImages();

				_instance.LoadAwardIDs();

				_instance.LoadTimeZone();

				_instance.RemoveConfigEditing(player);
			}

			public override void Close(BasePlayer player, params string[] args)
			{
				base.Close(player, args);

				_instance.RemoveConfigEditing(player);
			}
		}

		private EditConfigData InitConfigEditing(BasePlayer player, string mainCMD)
		{
			if (_editConfig.TryGetValue(player.userID, out var editConfig))
				return editConfig;

			editConfig = new EditConfigData
			{
				Type = _config.GetType(),
				Object = JObject.FromObject(_config),
				IsGenerated = false,
				MainCommand = mainCMD,
				IgnoredProperties = new[]
				{
					LangRu ? "Ежедневные награды" : "Daily awards",
					LangRu ? "Задержки (разрешение – задержка)" : "Cooldowns (permission – cooldown)"
				}
			};

			return _editConfig[player.userID] = editConfig;
		}

		private EditConfigData GetConfigEditing(BasePlayer player)
		{
			return _editConfig.GetValueOrDefault(player.userID);
		}

		private bool HasConfigEditing(BasePlayer player)
		{
			return _editConfig.ContainsKey(player.userID);
		}

		private bool RemoveConfigEditing(BasePlayer player)
		{
			return _editConfig.Remove(player.userID);
		}

		private void StartEditConfig(BasePlayer player)
		{
			var editData = InitConfigEditing(player, "edit_config");
			if (editData == null) return;

			editData.SetLastHook(nameof(EditConfigUI));

			EditConfigUI(player);
		}

		private void EditConfigChangePage(string[] args, BasePlayer player, EditData editData)
		{
			var page = 0;

			if (args.HasLength(3))
				int.TryParse(args[2], out page);

			editData.SetLastHook(nameof(EditConfigUI), page);

			EditConfigUI(player, page);
		}

		#endregion

		#endregion Main

		#region Colors

		private string GetScaledColor(Color startColor, Color endColor, float scale, float alpha = 1f)
		{
			var color = Color.Lerp(startColor, endColor, scale);

			return $"{(double) color.r / 255:F2} {(double) color.b / 255:F2} {(double) color.g / 255:F2} {alpha:F2}";
		}

		#endregion

		#region Destroy Timers

		private Dictionary<ulong, Timer> _destroyTimes = new();

		private void DestroyEditingTimer(ulong player)
		{
			if (_destroyTimes.TryGetValue(player, out var destroyTimer)) destroyTimer?.Destroy();
		}

		private void AddEditingTimer(ulong player, float delay, Action callback)
		{
			_destroyTimes[player] = timer.In(delay, callback);
		}

		#endregion

		#region Utils

		private void StartEditAction(string mainAction, string[] args, BasePlayer player, EditData editData = null,
			Action<string[]> onStart = null)
		{
			switch (mainAction)
			{
				case "start":
				{
					onStart?.Invoke(args);
					break;
				}

				default:
				{
					if (editData == null) return;

					switch (mainAction)
					{
						case "show_modal":
						{
							if (!args.HasLength(3)) return;

							var targetProperty = args[2];
							if (string.IsNullOrEmpty(targetProperty)) return;

							targetProperty = targetProperty.ReplaceUnicodeToSpace();

							ShowImageModalUI(player, editData, targetProperty, destroyLayer =>
							{
								DestroyEditingTimer(player.userID);

								AddEditingTimer(player.userID, 3f, () => CuiHelper.DestroyUi(player, destroyLayer));
							});
							break;
						}

						case "page":
						{
							switch (args[0])
							{
								case "edit_config":
								{
									EditConfigChangePage(args, player, editData);
									break;
								}
							}

							break;
						}

						case "property":
						{
							if (!args.HasLength(3)) return;

							var targetProperty = args[2];
							if (string.IsNullOrEmpty(targetProperty)) return;

							var target = GetPropertyByPath(targetProperty.ReplaceUnicodeToSpace(), editData.Object);
							if (target == null) return;

							var newValue = string.Join(" ", args.Skip(3));

							var targetVal = TryConvertObjectFromString(target, newValue);
							if (targetVal != null)
							{
								target.Value = JToken.FromObject(targetVal);

								editData.UpdateObject();
							}

							editData.CallLastHook(player);
							break;
						}

						case "array":
						{
							switch (args[2])
							{
								case "start":
								{
									if (!args.HasLength(4)) return;

									var targetProperty = args[3];
									if (string.IsNullOrEmpty(targetProperty)) return;

									targetProperty = targetProperty.ReplaceUnicodeToSpace();

									editData.SetTargetProperty(targetProperty);

									editData.SetLastHookAndProperty(nameof(EditArrayUI), targetProperty, "edit_data");

									EditArrayUI(player, editData);
									break;
								}

								case "page":
								{
									if (!args.HasLength(4) || !int.TryParse(args[3], out var selectedTab)) return;

									var page = 0;
									if (args.HasLength(5))
										int.TryParse(args[4], out page);

									editData.SetLastHookAndProperty(nameof(EditArrayUI), editData.GetTargetProperty(),
										"edit_data", selectedTab, page);

									EditArrayUI(player, editData, selectedTab, page);
									break;
								}

								case "change_system":
								{
									if (!args.HasLength(6) || !int.TryParse(args[4], out var selectedTab)
									                       || !int.TryParse(args[5], out var page)) return;

									var targetProperty = args[3];
									if (string.IsNullOrEmpty(targetProperty)) return;

									var target = GetPropertyByPath(targetProperty.ReplaceUnicodeToSpace(),
										editData.Object);
									if (target == null) return;

									var newValue = string.Join(" ", args.Skip(6));
									if (string.IsNullOrEmpty(newValue)) return;

									if (target.Value is JArray arr)
										arr[selectedTab] = JToken.FromObject(newValue);

									editData.SetLastHook(nameof(EditArrayUI), "edit_data", selectedTab, page);

									EditArrayUI(player, editData, selectedTab, page);
									break;
								}

								case "change":
								{
									if (!args.HasLength(7) || !int.TryParse(args[3], out var selectedTab)
									                       || !int.TryParse(args[4], out var page)) return;

									var targetProperty = args[5];
									if (string.IsNullOrEmpty(targetProperty)) return;

									var target = GetPropertyByPath(targetProperty.ReplaceUnicodeToSpace(),
										editData.Object);
									if (target == null) return;

									var newValue = string.Join(" ", args.Skip(6));
									if (string.IsNullOrEmpty(newValue)) return;

									var targetVal = TryConvertObjectFromString(target, newValue);
									if (targetVal != null)
										target.Value = JToken.FromObject(targetVal);

									editData.SetLastHook(nameof(EditArrayUI), "edit_data", selectedTab, page);

									EditArrayUI(player, editData, selectedTab, page);
									break;
								}

								case "add":
								{
									if (!args.HasLength(5) || !int.TryParse(args[3], out var selectedTab)
									                       || !int.TryParse(args[4], out var page)) return;

									var target = GetPropertyByPath(editData.GetTargetProperty(), editData.Object);

									var array = target?.Value as JArray;
									if (array == null) return;

									object newObject = null;
									if (_editableTokenTypes.Contains(array.First.Type))
									{
										var systemType = GetTypeFromJToken(array.First.Type);
										if (systemType != null) newObject = CreateObjectByType<object>(systemType);
									}
									else
									{
										// Get the type of the nested objects in the JArray
										var elementType = array.First.GetType();

										// Create a new instance of the object
										newObject = CreateObjectByType<object>(elementType);
									}

									if (newObject != null)
									{
										// Add the new object to the JArray
										array.Add(JToken.FromObject(newObject));

										editData.UpdateObject();
									}

									selectedTab++;

									editData.SetLastHook(nameof(EditArrayUI), selectedTab, page);

									EditArrayUI(player, editData, selectedTab, page);
									break;
								}

								case "remove":
								{
									if (!args.HasLength(5) || !int.TryParse(args[3], out var selectedTab)
									                       || !int.TryParse(args[4], out var page)) return;

									var target = GetPropertyByPath(editData.GetTargetProperty(), editData.Object);

									var array = target.Value as JArray;
									if (array == null) return;

									if (array.Count > 1)
									{
										array.RemoveAt(selectedTab);

										editData.UpdateObject();
									}
									else
									{
										array[0]["Enabled"] = JToken.FromObject(false);
									}

									selectedTab = Mathf.Min(array.Count - 1, selectedTab);

									editData.SetLastHook(nameof(EditArrayUI), "edit_data", selectedTab, page);

									EditArrayUI(player, editData, selectedTab, page);
									break;
								}

								case "close":
								{
									var targetProperty = editData.GetTargetProperty();

									editData.ClearTargetProperty();

									editData.RemoveLastHook(nameof(EditArrayUI), targetProperty);

									editData.CallLastHook(player, nameof(EditArrayUI), targetProperty);
									break;
								}
							}

							break;
						}

						case "enum":
						{
							if (!args.HasLength(3)) return;

							var targetProperty = args[2];
							if (string.IsNullOrEmpty(targetProperty)) return;

							targetProperty = targetProperty.ReplaceUnicodeToSpace();

							var target = GetPropertyByPath(targetProperty, editData.Object);
							if (target == null) return;

							if (Enum.TryParse(target.Value.ToString(), out AwardInfo.ItemType currentType) &&
							    Enum.IsDefined(typeof(AwardInfo.ItemType), currentType))
							{
								AwardInfo.ItemType? newType = null;

								switch (args[3])
								{
									case "back":
									{
										newType = (AwardInfo.ItemType) currentType.Previous();
										break;
									}

									case "next":
									{
										newType = (AwardInfo.ItemType) currentType.Next();
										break;
									}
								}

								if (newType != null) target.Value = JToken.FromObject(newType);
							}

							editData.CallLastHook(player);
							break;
						}

						case "select":
						{
							switch (args[2])
							{
								case "start":
								{
									if (!args.HasLength(4)) return;

									var targetProperty = args[3];
									if (string.IsNullOrEmpty(targetProperty)) return;

									if (args.HasLength(5))
									{
										var lastHook = args[4];
										if (!string.IsNullOrEmpty(lastHook))
										{
											if (args.HasLength(6))
												editData.SetLastHook(lastHook, args.Skip(5));
											else
												editData.SetLastHook(lastHook);
										}
									}

									editData.SetSelectProperty(targetProperty.ReplaceUnicodeToSpace());

									SelectItemUI(player, editData);
									break;
								}

								case "close":
								{
									editData.ClearSelectProperty();
									break;
								}

								case "page":
								{
									if (!args.HasLength(4) || !int.TryParse(args[3], out var selectedCategory)) return;

									SelectItemUI(player, editData, selectedCategory);
									break;
								}

								case "select":
								{
									if (!args.HasLength(4) || !int.TryParse(args[3], out var itemID)) return;

									var definition = ItemManager.FindItemDefinition(itemID);
									if (definition == null) return;

									var targetProperty = GetPropertyByPath(
										editData.GetSelectProperty().ReplaceUnicodeToSpace(),
										editData.Object);
									if (targetProperty != null)
									{
										targetProperty.Value = JToken.FromObject(definition.shortname);

										foreach (var property in targetProperty.Parent.Children<JProperty>())
											if (property.Name.Contains("Skin"))
												property.Value = (ulong) 0;

										editData.UpdateObject();
									}

									editData.ClearSelectProperty();

									editData.CallLastHook(player);
									break;
								}
							}

							break;
						}

						case "color":
						{
							switch (args[2])
							{
								case "start":
								{
									if (!args.HasLength(4)) return;

									var targetProperty = args[3];
									if (string.IsNullOrEmpty(targetProperty)) return;

									if (args.HasLength(5))
									{
										var lastHook = args[4];
										if (!string.IsNullOrEmpty(lastHook))
										{
											if (args.HasLength(6))
												editData.SetLastHook(lastHook, args.Skip(5));
											else
												editData.SetLastHook(lastHook);
										}
									}

									editData.SetSelectProperty(targetProperty.ReplaceUnicodeToSpace());

									SelectColorUI(player, editData);
									break;
								}

								case "close":
								{
									editData.ClearSelectProperty();
									break;
								}

								case "set":
								{
									if (!args.HasLength(4)) return;

									switch (args[3])
									{
										case "hex":
										{
											if (!args.HasLength(5)) return;

											var hex = string.Join(" ", args.Skip(4));
											if (string.IsNullOrWhiteSpace(hex))
												return;

											var str = hex.Trim('#');
											if (!str.IsHex())
												return;

											var targetProperty = GetPropertyByPath(
												editData.GetSelectProperty().ReplaceUnicodeToSpace(),
												editData.Object);
											if (targetProperty == null)
												return;

											if (targetProperty.Value?.TryParseObject(out IColor color) != true) return;

											color.Hex = $"#{str}";

											color.UpdateColor();

											targetProperty.Value = JToken.FromObject(color);

											editData.UpdateObject();

											SelectColorUI(player, editData);
											break;
										}

										case "opacity":
										{
											if (!args.HasLength(5) || !float.TryParse(args[4], out var opacity)) return;

											if (opacity is < 0 or > 100)
												return;

											opacity = (float) Math.Round(opacity, 2);

											var targetProperty = GetPropertyByPath(
												editData.GetSelectProperty().ReplaceUnicodeToSpace(),
												editData.Object);
											if (targetProperty == null)
												return;

											if (targetProperty.Value?.TryParseObject(out IColor color) != true) return;

											color.Alpha = opacity;

											color.UpdateColor();

											targetProperty.Value = JToken.FromObject(color);

											editData.UpdateObject();

											SelectColorUI(player, editData);
											break;
										}
									}

									break;
								}
							}

							break;
						}

						case "close":
						{
							editData.Close(player);
							break;
						}

						case "save":
						{
							var targetObject = editData.Object.ToObject(editData.Type);
							if (targetObject == null) return;

							editData.Save(player, targetObject);
							break;
						}

						case "remove":
						{
							editData.Remove(player, args.Skip(2).ToArray());
							break;
						}
					}

					break;
				}
			}
		}

		private bool TryGetArrayProperties(EditData editData, int selectedTab,
			out JProperty target,
			out JArray array,
			out JProperty[] propterties,
			out bool isSystemTypes)
		{
			isSystemTypes = false;
			propterties = null;

			target = GetPropertyByPath(editData.GetTargetProperty(), editData.Object);
			array = (JArray) target?.Value;
			if (array == null)
			{
				propterties = Array.Empty<JProperty>();
				return false;
			}

			if (array[selectedTab] is JObject targetObj)
			{
				propterties = targetObj.Properties()
					.OrderByDescending(x => x.HasValues && x.Value.Type != JTokenType.Object)
					.ToArray();
				return true;
			}

			var targetVal = array[selectedTab];
			if (targetVal != null && _editableTokenTypes.Contains(targetVal.Type))
			{
				isSystemTypes = true;

				propterties = new[]
				{
					new JProperty(string.Empty, targetVal)
				};
				return true;
			}

			return false;
		}

		private static object TryConvertObjectFromString(JProperty target, string newValue)
		{
			object targetVal = null;
			switch (target.Value.Type)
			{
				case JTokenType.Object:
				{
					var targetOBJ = JToken.FromObject(newValue);
					if (targetOBJ != null)
						targetVal = targetOBJ;
					break;
				}
				case JTokenType.Array:
				{
					if (JToken.FromObject(newValue) is JArray arr)
						targetVal = arr;
					break;
				}
				case JTokenType.Integer:
				{
					if (long.TryParse(newValue, out var ulongVal))
					{
						targetVal = ulongVal;
					}
					else
					{
						if (int.TryParse(newValue, out var val))
							targetVal = val;
					}

					break;
				}
				case JTokenType.Float:
				{
					if (float.TryParse(newValue, out var val))
						targetVal = val;
					break;
				}
				case JTokenType.Null:
				case JTokenType.String:
				{
					targetVal = newValue;
					break;
				}
				case JTokenType.Boolean:
				{
					if (bool.TryParse(newValue, out var val))
						targetVal = val;
					break;
				}
				case JTokenType.Date:
				{
					if (DateTime.TryParse(newValue, out var val))
						targetVal = val;
					break;
				}
				case JTokenType.Bytes:
				{
					if (byte.TryParse(newValue, out var val))
						targetVal = val;
					break;
				}
				case JTokenType.Guid:
				{
					if (Guid.TryParse(newValue, out var val))
						targetVal = val;
					break;
				}
				case JTokenType.TimeSpan:
				{
					if (TimeSpan.TryParse(newValue, out var val))
						targetVal = val;
					break;
				}
			}

			return targetVal;
		}

		private static Type GetTypeFromJToken(JTokenType tokenType)
		{
			switch (tokenType)
			{
				case JTokenType.Integer:
					return typeof(int);

				case JTokenType.Float:
					return typeof(float);

				case JTokenType.String:
					return typeof(string);

				case JTokenType.Boolean:
					return typeof(bool);

				case JTokenType.Date:
					return typeof(DateTime);

				case JTokenType.Guid:
					return typeof(Guid);

				case JTokenType.Uri:
					return typeof(Uri);

				case JTokenType.TimeSpan:
					return typeof(TimeSpan);

				default:
					return null;
			}
		}

		private object CreateObjectByType<T>(Type type)
		{
			if (type == typeof(string)) return string.Empty;

			return (T) Activator.CreateInstance(type);
		}

		private static ulong TryGetSkinFromProperty(string targetProperty, JProperty property, JObject obj)
		{
			var skin = 0UL;

			JProperty skinProperty = null;
			var splitted = targetProperty.Split('.');
			if (splitted.Length > 0)
			{
				if (property.Parent is JObject parentObj)
					skinProperty = parentObj.Properties()?.FirstOrDefault(x => x.Name.Contains("Skin"));
			}
			else
			{
				skinProperty = obj.Properties().FirstOrDefault(x => x.Name.Contains("Skin"));
			}

			if (skinProperty != null) skin = (ulong) skinProperty.Value;

			return skin;
		}

		private static JProperty GetPropertyByPath(string propertyPath, JObject obj)
		{
			return (JProperty) obj.SelectToken(propertyPath).Parent;
		}

		#endregion

		#endregion Helpers

		#region Editing.Lang

		private const string
			EditingCfgTitle = "EditingCfgTitle",
			EditingAwardRemove = "EditingAwardRemove",
			EditingAwardEdit = "EditingAwardEdit",
			EditingAwardAdd = "EditingAwardAdd",
			EditingTitleDay = "EditingTitleDay",
			EditingSelectItem = "EditingSelectItem",
			EditingBtnOpen = "EditingBtnOpen",
			EditingSelectColorOpacity = "EditingSelectColorOpacity",
			EditingSelectColorHEX = "EditingSelectColorHEX",
			EditingSelectedColor = "EditingSelectedColor",
			EditingSelectColorTitle = "EditingSelectColorTitle",
			EditingBtnAdd = "EditingBtnAdd",
			EditingBtnDown = "EditingBtnDown",
			EditingBtnUp = "EditingBtnUp",
			EditingModalTitle = "EditingModalTitle",
			EditingBtnSave = "EditingBtnSave",
			EditingBtnRemove = "EditingBtnRemove",
			EditingBtnBack = "EditingBtnBack",
			EditingBtnNext = "EditingBtnNext",
			EditingBtnCancel = "EditingBtnCancel",
			EditingBtnAccept = "EditingBtnAccept",
			EditingBtnClose = "EditingBtnClose";

		private Dictionary<string, string> RegisterEditingMessages(string langKey = "en")
		{
			switch (langKey)
			{
				case "ru":
				{
					return new Dictionary<string, string>
					{
						[EditingCfgTitle] = "РЕДАКТИРОВАНИЕ КОНФИГУРАЦИИ",

						[EditingTitleDay] = "ДЕНЬ {0}. РЕДАКТИРОВАНИЕ",
						[EditingAwardAdd] = "РЕДАКТИРОВАТЬ НАГРАДУ",
						[EditingAwardRemove] = "УДАЛИТЬ НАГРАДУ?",
						[EditingSelectItem] = "ВЫБРАТЬ ПРЕДМЕТ",

						[EditingBtnOpen] = "РЕДАКТИРОВАТЬ",
						[EditingSelectColorOpacity] = "Непрозрачность (0-100):",
						[EditingSelectColorHEX] = "HEX:",
						[EditingSelectedColor] = "Выбранный цвет:",
						[EditingSelectColorTitle] = "ВЫБОР ЦВЕТА",
						[EditingModalTitle] = "РЕДАКТИРОВАНИЕ: {0}",
						[EditingBtnSave] = "СОХРАНИТЬ",
						[EditingBtnRemove] = "УДАЛИТЬ",
						[EditingBtnClose] = "✕",
						[EditingBtnAccept] = "ДА",
						[EditingBtnCancel] = "НЕТ",
						[EditingBtnNext] = "▶",
						[EditingBtnBack] = "◀",
						[EditingBtnUp] = "⬆",
						[EditingBtnDown] = "⬇",
						[EditingBtnAdd] = "✚"
					};
				}

				case "zh-CN":
				{
					return new Dictionary<string, string>
					{
						[EditingCfgTitle] = "编辑配置",

						[EditingTitleDay] = "第{0}天。编辑",
						[EditingAwardAdd] = "编辑添加奖状",
						[EditingAwardRemove] = "删除?",
						[EditingSelectItem] = "选择项目",

						[EditingBtnOpen] = "编辑",
						[EditingSelectColorOpacity] = "不透明度 (0-100):",
						[EditingSelectColorHEX] = "HEX:",
						[EditingSelectedColor] = "所选颜色:",
						[EditingSelectColorTitle] = "所选颜色",
						[EditingModalTitle] = "编辑: {0}",
						[EditingBtnSave] = "保存",
						[EditingBtnRemove] = "删除",
						[EditingBtnClose] = "✕",
						[EditingBtnAccept] = "是",
						[EditingBtnCancel] = "不",
						[EditingBtnNext] = "▶",
						[EditingBtnBack] = "◀",
						[EditingBtnUp] = "⬆",
						[EditingBtnDown] = "⬇",
						[EditingBtnAdd] = "✚"
					};
				}

				default:
				{
					return new Dictionary<string, string>
					{
						[EditingCfgTitle] = "EDIT CONFIGURATION",

						[EditingTitleDay] = "DAY {0}. EDITING",
						[EditingAwardAdd] = "ADD AWARD",
						[EditingAwardEdit] = "EDIT AWARD",
						[EditingAwardRemove] = "REMOVE AWARD?",
						[EditingSelectItem] = "SELECT ITEM",

						[EditingBtnOpen] = "EDIT",
						[EditingSelectColorOpacity] = "Opacity (0-100):",
						[EditingSelectColorHEX] = "HEX:",
						[EditingSelectedColor] = "Selected color:",
						[EditingSelectColorTitle] = "COLOR PICKER",
						[EditingModalTitle] = "EDITING: {0}",
						[EditingBtnSave] = "SAVE",
						[EditingBtnRemove] = "REMOVE",
						[EditingBtnClose] = "✕",
						[EditingBtnAccept] = "YES",
						[EditingBtnCancel] = "NO",
						[EditingBtnNext] = "▶",
						[EditingBtnBack] = "◀",
						[EditingBtnUp] = "⬆",
						[EditingBtnDown] = "⬇",
						[EditingBtnAdd] = "✚"
					};
				}
			}
		}

		#endregion Editing.Lang

#endif

		#endregion Editing

		#region Utils

		#region Opened Reward Player

		private Dictionary<ulong, OpenedRewardPlayer> _openedRewardPlayers = new();

		private Dictionary<ulong, Timer> _updateTimes = new();
		
		private class OpenedRewardPlayer
		{
			#region Fields
			
			public BasePlayer Player;

			public bool useMainUI;

			public List<(int day, AwardDayInfo dayInfo)> availableAwards;
			
			#endregion Fields
			
			public OpenedRewardPlayer(BasePlayer player, bool mainUI = true)
			{
				Player = player;
				useMainUI = mainUI;
				
				UpdateAwards();
			}

			#region Pages

			public int currentPage;

			public void OnChangePage(int page)
			{
				currentPage = page;

				if (_config.Cooldown.Enabled)
				{
					if (currentPage == updateAwardPage)
						CheckIfNeedUpdateAward();
					else
					{
						
						TryDestroyTimer();
					}
				}
			}

			#endregion

			#region Update

			public void UpdateAwards()
			{
				availableAwards = _instance.GetAvailabeAwards(Player.userID);

				CheckIfNeedUpdateAward();
			}

			public bool needUpdateAward = false;

			public (int day, AwardDayInfo dayInfo)? awardToUpdate = null;

			private int updateAwardPage = 0;
			
			private void CheckIfNeedUpdateAward()
			{
				if (!_config.Cooldown.Enabled || availableAwards == null) return;
				
				var data = PlayerData.GetOrLoad(Player.userID);
				if (data?.HasCooldown() != true) return;
				
				var targetAward = availableAwards.Find(award => data.CanTake(Player.userID, award.day));
				if (targetAward == default) return;
				
				awardToUpdate = targetAward;
								
				needUpdateAward = true;

				updateAwardPage = currentPage;
				
				TryDestroyTimer();

				SetUpdateTimer(_instance.timer.Every(1, CooldownUpdateAction));
			}

			#endregion

			#region Times

			private Timer GetUpdateTimer()
			{
				return _instance?._updateTimes?.TryGetValue(Player.userID, out var timer) == true ? timer : null;
			}

			public void TryDestroyTimer()
			{
				needUpdateAward = false;
				
				GetUpdateTimer()?.Destroy();
			}

			private void SetUpdateTimer(Timer timer)
			{
				_instance._updateTimes[Player.userID] = timer;
			}

			private void CooldownUpdateAction()
			{
				UpdateUI(Player, container =>
				{
					var data = PlayerData.GetOrLoad(Player.userID);
					if (data == null) return;
					
					_instance.UpdateCooldownUI(Player, data, ref container);
					
					if (awardToUpdate.HasValue)
						_instance.ShowAwardDayUI(Player, data, awardToUpdate.Value.day, awardToUpdate.Value.dayInfo, ref container);
				});
			}	
			
			#endregion

			#region UI

			public InterfaceSettings GetUI()
			{
				return useMainUI ? _config.UI : _config.MenuUI;
			}

			#endregion
		}

		private OpenedRewardPlayer GetOrCreateOpenedRewardPlayer(BasePlayer player, bool mainUI = true)
		{
			if (!_openedRewardPlayers.TryGetValue(player.userID, out var openedRewardPlayer))
				_openedRewardPlayers.TryAdd(player.userID, openedRewardPlayer = new OpenedRewardPlayer(player, mainUI));

			return openedRewardPlayer;
		}

		private OpenedRewardPlayer GetOpenedRewardPlayer(ulong userID)
		{
			return _openedRewardPlayers.GetValueOrDefault(userID);
		}

		private bool IsOpenedRewardPlayer(ulong userID)
		{
			return _openedRewardPlayers.ContainsKey(userID);
		}

		private void RemoveOpenedRewardPlayer(BasePlayer player)
		{
			RemoveOpenedRewardPlayer(player.userID);
		}
		
		private void RemoveOpenedRewardPlayer(ulong userID)
		{
			GetOpenedRewardPlayer(userID)?.TryDestroyTimer();
			
			_openedRewardPlayers.Remove(userID);
		}
		
		#endregion

		#region Wipe

		private int GetSecondsFromWipeFromWipeBlock()
		{
			return Convert.ToInt32(WipeBlock?.Call("SecondsFromWipe"));
		}

		private int GetSecondsFromWipe()
		{
			return (int) DateTime.Now.ToUniversalTime().Subtract(SaveRestore.SaveCreatedTime.ToUniversalTime())
				.TotalSeconds;
		}

		private bool NE_IsCombatBlocked(BasePlayer player)
		{
			return Convert.ToBoolean(NoEscape?.Call("IsCombatBlocked", player));
		}

		private bool NE_IsRaidBlocked(BasePlayer player)
		{
			return Convert.ToBoolean(NoEscape?.Call("IsRaidBlocked", player));
		}

		#endregion
		
		#region Coroutines

		private IEnumerator StartOnPlayers(string[] players, Action<string> callback = null)
		{
			for (var i = 0; i < players.Length; i++)
			{
				callback?.Invoke(players[i]);

				if (i % 10 == 0)
					yield return CoroutineEx.waitForFixedUpdate;
			}
		}

		private IEnumerator WipeAllPlayers(string[] players)
		{
			for (var i = players.Length - 1; i >= 0; i--)
			{
				PlayerData.DoWipe(players[i]);

				if (i % 10 == 0)
					yield return CoroutineEx.waitForFixedUpdate;
			}

			yield return CoroutineEx.waitForFixedUpdate;

			foreach (var player in BasePlayer.activePlayerList)
				OnPlayerConnected(player);

			Puts("You have wiped player data!");
		}

		private void StopCoroutines()
		{
			StopWipePlayers();

			StopTopPlayersCoroutine();
		}

		#region Top Players

		private Coroutine _topPlayers;

		private class TopPlayer
		{
			public string DisplayName;

			public string PlayerID;

			public int MaxTakedDay;
		}

		private IEnumerator StartOnTopPlayers(string[] players, Action<List<TopPlayer>> callback, int limit)
		{
			var topPlayers = new List<TopPlayer>();

			for (var i = 0; i < players.Length; i++)
			{
				var data = PlayerData.GetNotLoad(ulong.Parse(players[i]));
				if (data != null)
				{
					var maxTakedDay = data.TakedDays.Append(0).Max();
					if (maxTakedDay > 0)
					{
						var player = covalence.Players.FindPlayerById(players[i]);

						var name = player != null && !string.IsNullOrEmpty(player.Name) ? player.Name : "UNKNOWN";

						topPlayers.Add(new TopPlayer
						{
							DisplayName = name,
							PlayerID = data.PlayerID,
							MaxTakedDay = maxTakedDay
						});
					}
				}

				if (i % 10 == 0)
					yield return CoroutineEx.waitForFixedUpdate;
			}

			topPlayers.Sort((x, y) => y.MaxTakedDay.CompareTo(x.MaxTakedDay));

			if (limit > 0)
				topPlayers = topPlayers.SkipAndTake(0, limit);

			callback?.Invoke(topPlayers);
		}

		private void StartTopPlayers(Action<List<TopPlayer>> callback, int limit)
		{
			try
			{
				var players = PlayerData.GetFiles();
				if (players is {Length: > 0})
					_topPlayers =
						Global.Runner.StartCoroutine(StartOnTopPlayers(players, callback, limit));
			}
			catch (Exception e)
			{
				PrintError($"[Top Players] error: {e.Message}");
			}
		}

		private void StopTopPlayersCoroutine()
		{
			if (_topPlayers != null)
				Global.Runner.StopCoroutine(_topPlayers);
		}

		#endregion

		#region Wipe

		private Coroutine _wipePlayers;

		private void StartWipePlayers()
		{
			try
			{
				var players = PlayerData.GetFiles();
				if (players is {Length: > 0})
					_wipePlayers =
						Global.Runner.StartCoroutine(WipeAllPlayers(players));
			}
			catch (Exception e)
			{
				PrintError($"[On Server Wipe] in wipe players, error: {e.Message}");
			}
		}

		private void StopWipePlayers()
		{
			if (_wipePlayers != null)
				Global.Runner.StopCoroutine(_wipePlayers);
		}

		#endregion

		#endregion

		private void LoadAwardIDs()
		{
			_awardsByID.Clear();

			foreach (var check in _config.DailyAwards)
			foreach (var award in check.Value.Awards)
				if (!_awardsByID.TryAdd(award.ID, award))
					PrintError($"Found a duplicate item ID: {award.ID}");
		}

		private int GenerateAwardID()
		{
			int id;
			do
			{
				id = Random.Range(0, int.MaxValue);
			} while (_awardsByID.ContainsKey(id));

			return id;
		}

		private AwardInfo FindAwardByID(int id)
		{
			return _awardsByID.GetValueOrDefault(id);
		}

		private bool TryFindAwardByID(int id, out AwardInfo awardInfo)
		{
			return _awardsByID.TryGetValue(id, out awardInfo);
		}

		private readonly Dictionary<string, string> _itemsTitles = new();

		private string GetItemName(BasePlayer player, string shortName)
		{
			if (!_itemsTitles.TryGetValue(shortName, out var result))
				_itemsTitles.Add(shortName, result = ItemManager.FindItemDefinition(shortName)?.displayName.english);

			if (_config.UseLangAPI && LangAPI is {IsLoaded: true} &&
			    LangAPI.Call<bool>("IsDefaultDisplayName", result))
				return LangAPI.Call<string>("GetItemDisplayName", shortName, result, player.UserIDString) ?? result;

			return result;
		}

		private bool CHECK_IsAFK(ulong target)
		{
			return Convert.ToBoolean(AFKAPI?.Call("IsPlayerAFK", target));
		}

		private AwardDayInfo GetAwardByDay(int day)
		{
			return _config.DailyAwards.GetValueOrDefault(day);
		}

		private List<(int day, AwardDayInfo dayInfo)> GetAvailabeAwards(ulong player, bool hasInConfig = false)
		{
			var isAdmin = permission.UserHasPermission(player.ToString(), PERM_EDIT);

			var result = new SortedDictionary<int, AwardDayInfo>();

			if (_config.UI.ShowImageOnSecretDay)
			{
				var maxDay = _config.DailyAwards.MaxDayBy((_, val) => isAdmin || val.Enabled);

				for (var day = 1; day <= maxDay; day++)
					if (_config.DailyAwards.TryGetValue(day, out var awardDay))
					{
						result.TryAdd(day, awardDay);
					}
					else
					{
						if (!hasInConfig)
							result.TryAdd(day, null);
					}
			}
			else
			{
				foreach (var dayInfo in _config.DailyAwards)
					if (isAdmin || dayInfo.Value.Enabled)
						result.TryAdd(dayInfo.Key, dayInfo.Value);
			}

			var list = new List<(int day, AwardDayInfo dayInfo)>();
			foreach (var (day, dayInfo) in result)
			{
				list.Add((day, dayInfo));
			}

			return list;
		}

		private int CountAvailabeAwards(BasePlayer player)
		{
			var isAdmin = permission.UserHasPermission(player.UserIDString, PERM_EDIT);

			var result = 0;

			foreach (var dayInfo in _config.DailyAwards)
				if (isAdmin || dayInfo.Value.Enabled)
					result++;

			return result;
		}

		private void UpdateNotification(bool load)
		{
			if (_config.UseNotify && (Notify != null || UINotify != null))
			{
				var types = Pool.Get<List<int>>();

				if (_config.Notifications.OnCooldownEnded.ShowOnCooldownEnded ||
				    _config.Notifications.OnCooldownEnded.ShowOnPlayerConnected)
					types.Add(_config.Notifications.OnCooldownEnded.Type);

				if (_config.Notifications.HasCooldown.ShowOnPlayerConnected)
					types.Add(_config.Notifications.HasCooldown.Type);

				types.ForEach(type => Interface.Oxide.CallHook("TryToggleType", type, load));

				Pool.FreeUnmanaged(ref types);
			}
		}

		private void LoadTimeZone()
		{
			try
			{
				_pluginTimeZone = TimeZoneInfo.FindSystemTimeZoneById(_config.Reset.TimeZone);
			}
			catch (TimeZoneNotFoundException ex)
			{
				_pluginTimeZone = TimeZoneInfo.Local;

				_config.Reset.TimeZone = TimeZoneInfo.Local.Id;
				SaveConfig();

				PrintError(
					"Your computer does not have a Time Zone specified in the configuration, so your computer's Time Zone has been set.");
			}
			catch (Exception ex)
			{
				_pluginTimeZone = TimeZoneInfo.Local;

				PrintError("Your time zone setting is incorrect. Using local time zone.");
				Debug.LogException(ex);
			}
		}

		#region Images

		private void AddImage(string url, string fileName, ulong imageId = 0)
		{
#if CARBON
			imageDatabase.Queue(true, new Dictionary<string, string>
			{
				[fileName] = url
			});
#else
			ImageLibrary?.Call("AddImage", url, fileName, imageId);
#endif
		}
		
		private string GetImage(string name)
		{
#if CARBON
			return imageDatabase.GetImageString(name);
#else
			return Convert.ToString(ImageLibrary?.Call("GetImage", name));
#endif
		}

		private bool HasImage(string name)
		{
#if CARBON
			return Convert.ToBoolean(imageDatabase?.HasImage(name));
#else
			return Convert.ToBoolean(ImageLibrary?.Call("HasImage", name));
#endif
		}
		
		private void LoadImages()
		{
#if CARBON
			imageDatabase = BaseModule.GetModule<ImageDatabaseModule>();
#endif
			_enabledImageLibrary = true;

			var imagesList = new Dictionary<string, string>();

			foreach (var dayInfo in _config.DailyAwards.Values)
			{
				RegisterImage(dayInfo.Image, ref imagesList);

				dayInfo.Awards.ForEach(award => RegisterImage(award.Image, ref imagesList));
			}

			RegisterImage(_config.UI.Background.Image, ref imagesList);

			RegisterImage(_config.UI.NoAwardImage, ref imagesList);

			RegisterImage(_config.UI.SecretDayImage, ref imagesList);

			RegisterImage(_config.UI.EditImage, ref imagesList);

			RegisterImage(_config.UI.SelectItemImage, ref imagesList);

			RegisterImage(_config.UI.RustIconImage, ref imagesList);

			RegisterImage(_config.UI.SpecialDayImageForFinalDay, ref imagesList);

			RegisterImage(_config.UI.ButtonBackImage, ref imagesList);

			RegisterImage(_config.UI.ButtonNextImage, ref imagesList);

			RegisterImage(_config.UI.PageButtonBackWhenActive.Image, ref imagesList);

			RegisterImage(_config.UI.PageButtonBackWhenInactive.Image, ref imagesList);

			RegisterImage(_config.UI.PageButtonNextWhenActive.Image, ref imagesList);

			RegisterImage(_config.UI.PageButtonNextWhenInactive.Image, ref imagesList);

#if CARBON
            imageDatabase.Queue(false, imagesList);
#else
			timer.In(1f, () =>
			{
				if (ImageLibrary is not {IsLoaded: true})
				{
					_enabledImageLibrary = false;
					
					BroadcastILNotInstalled();
					return;
				}
				
				ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
			});
#endif
		}

		private static void RegisterImage(string image, ref Dictionary<string, string> imagesList)
		{
			if (!string.IsNullOrEmpty(image))
				imagesList.TryAdd(image, image);
		}

		private void BroadcastILNotInstalled()
		{
			for (var i = 0; i < 5; i++) PrintError("IMAGE LIBRARY IS NOT INSTALLED.");
		}
		
		#endregion Images
		
		private void LoadPlayers()
		{
			foreach (var player in BasePlayer.activePlayerList)
				OnPlayerConnected(player);
		}

		private void RegisterCommands()
		{
			AddCovalenceCommand(_config.Commands, nameof(CmdOpenRewards));
		}

		private void RegisterPermissions()
		{
			var permissions = new HashSet<string>
			{
				PERM_EDIT
			};

			if (!string.IsNullOrEmpty(_config.Permission))
				permissions.Add(_config.Permission);

			foreach (var dayInfo in _config.DailyAwards.Values)
			foreach (var dayInfoAward in dayInfo.Awards)
				if (!string.IsNullOrEmpty(dayInfoAward.Permission))
					permissions.Add(dayInfoAward.Permission);

			foreach (var cooldown in _config.Cooldown.Cooldowns.Keys)
				if (!string.IsNullOrEmpty(cooldown))
					permissions.Add(cooldown);

			foreach (var perm in permissions)
				RegisterPermission(perm);
		}

		private void RegisterPermission(string value)
		{
			if (!string.IsNullOrEmpty(value) && !permission.PermissionExists(value))
				permission.RegisterPermission(value, this);
		}

		private static string HexToCuiColor(string hex, float alpha = 100)
		{
			if (string.IsNullOrEmpty(hex)) hex = "#FFFFFF";

			var str = hex.Trim('#');
			if (str.Length != 6) throw new Exception(hex);
			var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
			var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
			var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

			return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {alpha / 100f}";
		}

		#endregion

		#region API
		
		private CuiElementContainer API_OpenPlugin(BasePlayer player)
		{
			RemoveOpenedRewardPlayer(player.userID);

			var openedRewardPlayer = GetOrCreateOpenedRewardPlayer(player, false);

			var container  = new CuiElementContainer();
            
			#region Background

			container.Add(new CuiPanel()
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image = {Color = "0 0 0 0"}
			}, "UI.Server.Panel.Content", "UI.Server.Panel.Content.Plugin", "UI.Server.Panel.Content.Plugin");

			container.Add(new CuiPanel()
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image = {Color = "0 0 0 0"}
			}, "UI.Server.Panel.Content.Plugin", Layer, Layer);

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image =
				{
					Color = "0 0 0 0"
				}
			}, Layer, MainLayer, MainLayer);

			AwardsHeaderUI(player, container);

			AwardsContentUI(player, ref container);

			AwardsProgressUI(player, ref container);

			#endregion

			return container;
		}

		#endregion
        
		#region Play Time Engine

		private PlayTimeEngine _playTimeEngine;

		private void InitPlayTimeEngine()
		{
			_playTimeEngine = new GameObject().AddComponent<PlayTimeEngine>();
		}

		private void DestroyPlayTimeEngine()
		{
			if (_playTimeEngine != null)
				_playTimeEngine.Kill();
		}

		private class PlayTimeEngine : FacepunchBehaviour
		{
			private ListHashSet<ulong> _players = new();

			private List<ulong> _playersToRemove = new();

			#region Init

			private void Awake()
			{
				InvokeRepeating(HandlePlayers, 1, 1);
			}

			#endregion

			#region Main

			private void HandlePlayers()
			{
				_playersToRemove.Clear();

				foreach (var userID in _players)
				{
					var player = RelationshipManager.FindByID(userID);
					if (player == null)
					{
						_playersToRemove.Add(userID);
						continue;
					}

					if (_config.Cooldown.CheckAFK && _instance.CHECK_IsAFK(player.userID))
						continue;

					var data = PlayerData.GetOrCreate(player.userID);

					data.PlayedTime += 1;

					if (!data.HasCooldown())
					{
						_playersToRemove.Add(player.userID);

						if (_config.Notifications.OnCooldownEnded.ShowOnCooldownEnded)
							_config.Notifications.OnCooldownEnded.Show(player);

						Interface.CallHook("OnDailyRewardsCooldownEnded", player);
					}
				}

				_playersToRemove.ForEach(userID => _players.Remove(userID));
			}

			public void Add(ulong player)
			{
				var data = PlayerData.GetOrLoad(player);
				if (data == null) return;

				if (data.HasCooldown())
					_players.Add(player);
			}

			public void Insert(ulong player)
			{
				_players.TryAdd(player);
			}

			public void Remove(ulong player)
			{
				_players.Remove(player);
			}

			#endregion

			#region Destroy

			private void OnDestroy()
			{
				CancelInvoke();

				Destroy(gameObject);
				Destroy(this);
			}

			public void Kill()
			{
				DestroyImmediate(this);
			}

			#endregion
		}

		#endregion

		#region Lang

		private const string
			NotifyTakedAward = "NotifyTakedAward",
			EditingBtnADDFirstAward = "EditingBtnADDFirstAward",
			NotifyCooldownEnded = "NotifyCooldownEnded",
			NotifyHasCooldown = "NotifyHasCooldown",
			PluginTitle = "PluginTitle",
			PluginDescription = "PluginDescription",
			PluginButtonInventory = "PluginButtonInventory",
			PluginLeftCooldownTitle = "PluginLeftCooldownTitle",
			PluginDaysPassedTitle = "PluginDaysPassedTitle",
			PluginLeftCooldownPassed = "PluginLeftCooldownPassed",
			PagesCount = "PagesCount",
			AwardsInfoTitle = "AwardsInfoTitle",
			AwardsInfoClose = "AwardsInfoClose",
			AwardInfoShow = "AwardInfoShow",
			AwardReceived = "AwardReceived",
			AwardReceiveError = "AwardReceiveError",
			InventoryTitle = "InventoryTitle",
			InventoryDescription = "InventoryDescription",
			InventoryButtonBack = "InventoryButtonBack",
			ItemAmountTitle = "ItemAmountTitle",
			DailyCardDayNumber = "DailyCardDayNumber",
			DailyCardDayEmpty = "DailyCardDayEmpty",
			DailyCardNoAward = "DailyCardNoAward",
			DailyCardReceived = "DailyCardGived",
			DailyCardBtnTake = "DailyCardBtnTake",
			DailyCardBlocked = "DailyCardBlocked",
			CantTakeItemRaidBlocked = "CantTakeItemRaidBlocked",
			CantTakeItemCombatBlocked = "CantTakeItemCombatBlocked",
			CantTakeItemBuildingBlocked = "CantTakeItemBuildingBlocked",
			CantTakeItemWipeBlock = "CantTakeItemWipeBlock",
			NoPermissions = "NoPermissions",
			EditingBtnADD = "EditingBtnADD",
			NotFoundEntity = "NotFoundEntity";

		protected override void LoadDefaultMessages()
		{
			var en = new Dictionary<string, string>
			{
				[NotifyTakedAward] = "Click to go to the inventory",
				[NotifyCooldownEnded] = "Click to open",
				[NotifyHasCooldown] = "IN {0} (click to open)",

				[EditingBtnADDFirstAward] = "ADD FIRST AWARD",
				[EditingBtnADD] = "✚",
				[NoPermissions] = "You don't have permissions!",
				[CantTakeItemWipeBlock] = "You can't take this item: blockage after wipe is in effect for {0}",
				[CantTakeItemBuildingBlocked] = "You can't take this item: you are in a building blocked zone!",
				[CantTakeItemCombatBlocked] = "You cannot take this item: you are combat blocked",
				[CantTakeItemRaidBlocked] = "You cannot take this item: you are raid blocked",

				[PluginTitle] = "DAILY REWARDS",
				[PluginDescription] =
					"Not only exciting game moments but also generous gifts are waiting for you on our server.\n" +
					"You can get valuable items and resources every day if you log into the server regularly.\n" +
					"<color=#EF5125>Spend 1 hour on the server</color> to mark your daily login <b><color=#EF5125>[ every day before 05:00 UTC ]</color></b>",
				[PluginButtonInventory] = "INVENTORY",
				[PluginLeftCooldownTitle] = "TO THE DAILY ENTRY MARK:",
				[PluginDaysPassedTitle] = "Total days passed:",
				[PluginLeftCooldownPassed] = "PASSED ✔",

				[PagesCount] = "<color=white>{0}</color>/{1}",

				[AwardsInfoTitle] = "ITEMS IN THE AWARD",
				[AwardsInfoClose] = "CLOSE",
				[AwardInfoShow] = "?",
				[AwardReceived] = "Item\nreceived",
				[AwardReceiveError] = "Receiving\nError",

				[InventoryTitle] = "INVENTORY",
				[InventoryDescription] =
					"This is where you store your items and rewards for completing tasks and achievements on the server",
				[InventoryButtonBack] = "BACK",

				[ItemAmountTitle] = "x{0}",

				[DailyCardDayNumber] = "DAY {0}",
				[DailyCardDayEmpty] = "EMPTY",
				[DailyCardNoAward] = "NO AWARD",
				[DailyCardReceived] = "RECEIVED",
				[DailyCardBtnTake] = "TAKE",
				[DailyCardBlocked] = "BLOCKED"
			};

			var ru = new Dictionary<string, string>
			{
				[NotifyTakedAward] = "Нажмите, чтобы перейти в инвентарь",
				[NotifyCooldownEnded] = "Нажмите чтобы открыть",
				[NotifyHasCooldown] = "через {0} (нажмите чтобы открыть)",

				[EditingBtnADDFirstAward] = "ДОБАВИТЬ ПЕРВУЮ НАГРАДУ",
				[EditingBtnADD] = "✚",
				[NoPermissions] = "У вас недостаточно разрешений!",
				[CantTakeItemWipeBlock] =
					"Вы не можете взять этот предмет: блокировка после wipe действует в течение {0}",
				[CantTakeItemBuildingBlocked] =
					"Вы не можете взять этот предмет: вы находитесь в зоне действия чужого шкафа!",
				[CantTakeItemCombatBlocked] = "Вы не можете взять этот предмет: у вас боевая блокировка!",
				[CantTakeItemRaidBlocked] = "Вы не можете взять этот предмет: у вас рейд блок!",

				[PluginTitle] = "НАГРАДА ЗА ЕЖЕДНЕВНЫЙ ВХОД",
				[PluginDescription] =
					"На нашем сервере вас ждут не только захватывающие игровые моменты, но и щедрые подарки.\n" +
					"Вы можете получать ценные предметы и ресурсы каждый день, если будете регулярно заходить на сервер.\n" +
					"<color=#EF5125>Проведите 1 час на сервере</color>, чтобы поставить отметку о ежедневном входе <b><color=#EF5125>[ каждый день до 05:00 UTC ]</color></b>",
				[PluginButtonInventory] = "ИНВЕНТАРЬ",
				[PluginLeftCooldownTitle] = "ОСТАЛОСЬ ДО ОТМЕТКИ О ЕЖЕДНЕВНОМ ВХОДЕ:",
				[PluginDaysPassedTitle] = "Всего дней пройдено:",
				[PluginLeftCooldownPassed] = "ОТМЕТКА ПРОЙДЕНА ✔",

				[PagesCount] = "<color=white>{0}</color>/{1}",

				[AwardsInfoTitle] = "ПРЕДМЕТЫ В НАГРАДЕ",
				[AwardsInfoClose] = "ЗАКРЫТЬ",
				[AwardInfoShow] = "?",
				[AwardReceived] = "Предмет\nполучен",
				[AwardReceiveError] = "Ошибка\nполучения",

				[InventoryTitle] = "ИНВЕНТАРЬ",
				[InventoryDescription] =
					"Здесь хранятся ваши предметы и награды, получение за выполнение задания и достижения на сервере",
				[InventoryButtonBack] = "НАЗАД",

				[ItemAmountTitle] = "x{0}",

				[DailyCardDayNumber] = "ДЕНЬ {0}",
				[DailyCardDayEmpty] = "ОТСУТСТВУЕТ",
				[DailyCardNoAward] = "НЕТ НАГРАДЫ",
				[DailyCardReceived] = "ПОЛУЧЕНО",
				[DailyCardBtnTake] = "ЗАБРАТЬ",
				[DailyCardBlocked] = "БЛОКИРОВАНО"
			};

			var zhCN = new Dictionary<string, string>
			{
				[NotifyTakedAward] = "点击进入库存",
				[NotifyCooldownEnded] = "点击打开",
				[NotifyHasCooldown] = "通过 {0}（按下打开）",

				[EditingBtnADDFirstAward] = "增加第一个奖项",
				[EditingBtnADD] = "✚",
				[NoPermissions] = "你们没有许可证!",
				[CantTakeItemWipeBlock] = "您不能拿走此物品：擦拭后锁定的有效期为 {0}",
				[CantTakeItemBuildingBlocked] = "您不能拿走此物品：您在别人的橱柜范围内!",
				[CantTakeItemCombatBlocked] = "您不能使用此道具：您已被战斗锁定!",
				[CantTakeItemRaidBlocked] = "您不能使用此道具：您有突袭阻断!",

				[PluginTitle] = "每日奖励",
				[PluginDescription] =
					"在我们的服务器上，等待您的不仅有激动人心的游戏时刻，还有丰厚的礼品。\n" +
					"如果您定期登录服务器，每天都可以获得珍贵的物品和资源。\n" +
					"<color=#EF5125>在服务器上花费 1 小时</color> 以记录每日登录情况 <b><color=#EF5125>[ 每日至 05:00 UTC ]</color></b>",
				[PluginButtonInventory] = "库存",
				[PluginLeftCooldownTitle] = "到每日参赛标志:",
				[PluginDaysPassedTitle] = "总旅行天数:",
				[PluginLeftCooldownPassed] = "标记通过 ✔",

				[PagesCount] = "<color=white>{0}</color>/{1}",

				[AwardsInfoTitle] = "奖品",
				[AwardsInfoClose] = "关闭",
				[AwardInfoShow] = "?",
				[AwardReceived] = "项目\n收到",
				[AwardReceiveError] = "接收\n错误",

				[InventoryTitle] = "库存",
				[InventoryDescription] =
					"您可在此存放物品以及完成服务器任务和成就后获得的奖励",
				[InventoryButtonBack] = "返回",

				[ItemAmountTitle] = "x{0}",

				[DailyCardDayNumber] = "第{0}天",
				[DailyCardDayEmpty] = "空",
				[DailyCardNoAward] = "无奖",
				[DailyCardReceived] = "收到",
				[DailyCardBtnTake] = "采取",
				[DailyCardBlocked] = "封锁"
			};

#if EDITING_MODE
			foreach (var msg in RegisterEditingMessages())
				en.TryAdd(msg.Key, msg.Value);

			foreach (var msg in RegisterEditingMessages("ru"))
				ru.TryAdd(msg.Key, msg.Value);

			foreach (var msg in RegisterEditingMessages("zh-CN"))
				zhCN.TryAdd(msg.Key, msg.Value);
#endif

			lang.RegisterMessages(en, this);

			lang.RegisterMessages(ru, this, "ru");

			lang.RegisterMessages(zhCN, this, "zh-CN");
		}

		private string Msg(string key, string userid = null, params object[] obj)
		{
			return string.Format(lang.GetMessage(key, this, userid), obj);
		}

		private string Msg(BasePlayer player, string key, params object[] obj)
		{
			return string.Format(lang.GetMessage(key, this, player.UserIDString), obj);
		}

		private void Reply(BasePlayer player, string key, params object[] obj)
		{
			player.ChatMessage(Msg(key, player.UserIDString, obj));
		}

		private void SendNotify(BasePlayer player, string key, int type, params object[] obj)
		{
			if (_config.UseNotify && (Notify != null || UINotify != null))
				Interface.Oxide.CallHook("SendNotify", player, type, Msg(player, key, obj));
			else
				Reply(player, key, obj);
		}

		#endregion

		#region Testing Functions

#if TESTING
		private static void SayDebug(string message)
		{
			Debug.Log($"[TESTING] {message}");

			// _instance?.LogToFile("debug", $"[{DateTime.UtcNow}] {message}", _instance);
		}
#endif

		#endregion
	}
}

#region Extension Methods

namespace Oxide.Plugins.DailyRewardsExtensionMethods
{
	// ReSharper disable ForCanBeConvertedToForeach
	// ReSharper disable LoopCanBeConvertedToQuery
	public static class ExtensionMethods
	{
		#region LINQ

		public static List<T> SkipAndTake<T>(this List<T> source, int skip, int take)
		{
			var index = Mathf.Min(Mathf.Max(skip, 0), source.Count);
			return source.GetRange(index, Mathf.Min(take, source.Count - index));
		}

		public static int MaxDayBy<T>(this IDictionary<int, T> a, Func<int, T, bool> b)
		{
			var c = 0;

			foreach (var d in a)
				if (b(d.Key, d.Value))
					if (d.Key > c)
						c = d.Key;

			return c;
		}

		public static int MinKeyBy<T>(this List<(int, T)> list, Func<int, bool> b)
		{
			int? c = null;

			for (var i = 0; i < list.Count; i++)
			{
				var obj = list[i];

				if (b(obj.Item1))
					if (c == null || obj.Item1 < c)
						c = obj.Item1;
			}

			return c ?? 0;
		}

		public static int MinKey<T>(this List<(int, T)> list)
		{
			int? c = null;

			for (var i = 0; i < list.Count; i++)
			{
				var obj = list[i];

				if (c == null || obj.Item1 < c)
					c = obj.Item1;
			}

			return c ?? 0;
		}

		public static int MaxDayBy<T>(this List<KeyValuePair<int, T>> list, Func<int, bool> b)
		{
			var c = 0;

			for (var i = 0; i < list.Count; i++)
			{
				var obj = list[i];

				if (b(obj.Key) && obj.Key > c) c = obj.Key;
			}

			return c;
		}

		public static Enum Next(this Enum input)
		{
			var values = Enum.GetValues(input.GetType());
			var j = Array.IndexOf(values, input) + 1;
			return values.Length == j ? (Enum) values.GetValue(0) : (Enum) values.GetValue(j);
		}

		public static Enum Previous(this Enum input)
		{
			var values = Enum.GetValues(input.GetType());
			var j = Array.IndexOf(values, input) - 1;
			return j == -1 ? (Enum) values.GetValue(values.Length - 1) : (Enum) values.GetValue(j);
		}

		#endregion

		public static int IndexOf<T>(this List<ICuiComponent> components) where T : class
		{
			for (var i = 0; i < components.Count; i++)
			{
				var component = components[i];

				if (component is T targetComponent) return i;
			}

			return -1;
		}

		public static string PropertyParentName(this JProperty property)
		{
			if (property.Parent == null)
				return string.Empty;

			for (JToken jtoken2 = property.Parent; jtoken2 != null; jtoken2 = jtoken2.Parent)
				switch (jtoken2.Type)
				{
					case JTokenType.Property:
					{
						var jproperty = (JProperty) jtoken2;

						return jproperty.Name;
					}
				}

			return string.Empty;
		}

		public static string PropertyRootNames(this JProperty property)
		{
			if (property.Parent == null)
				return string.Empty;

			var list = new List<string>();

			for (JToken jtoken2 = property.Parent; jtoken2 != null; jtoken2 = jtoken2.Parent)
				switch (jtoken2.Type)
				{
					case JTokenType.Property:
					{
						var jproperty = (JProperty) jtoken2;

						list.Add(jproperty.Name);
						break;
					}
				}

			if (list.Count > 0) return list.LastOrDefault();

			return string.Empty;
		}

		public static string[] PropertyParentsName(this JProperty property)
		{
			if (property.Parent == null)
				return Array.Empty<string>();

			var list = new List<string>();

			for (JToken jtoken2 = property.Parent; jtoken2 != null; jtoken2 = jtoken2.Parent)
				switch (jtoken2.Type)
				{
					case JTokenType.Property:
					{
						var jproperty = (JProperty) jtoken2;

						list.Add(jproperty.Name);
						break;
					}
				}

			list.Reverse();

			return list.ToArray();
		}

		public static bool TryParseJson<T>(this string str, out T result)
		{
			var success = true;
			var settings = new JsonSerializerSettings
			{
				Error = (sender, args) =>
				{
					success = false;
					args.ErrorContext.Handled = true;
				},
				MissingMemberHandling = MissingMemberHandling.Error
			};
			result = JsonConvert.DeserializeObject<T>(str, settings);
			return success;
		}

		public static bool TryParseObject<T>(this JToken token, out T result)
		{
			if (token == null)
			{
				result = default;
				return false;
			}

			var success = true;
			var settings = new JsonSerializerSettings
			{
				Error = (sender, args) =>
				{
					success = false;
					args.ErrorContext.Handled = true;
				},
				MissingMemberHandling = MissingMemberHandling.Error
			};

			var jsonSerializer = JsonSerializer.CreateDefault(settings);

			using var reader = new JTokenReader(token);
			result = jsonSerializer.Deserialize<T>(reader);

			return success;
		}

		public static List<JProperty> GetProperties(this JProperty[] arr)
		{
			var properties = new List<JProperty>();

			foreach (var property in arr)
				properties.AddRange(GetProperties(property));

			return properties;
		}

		private static List<JProperty> GetProperties(this JProperty property)
		{
			var list = new List<JProperty>();

			if (property.Value.Type == JTokenType.Object)
				if (property.Value is JObject nestedObject)
				{
					DailyRewards.IColor color;
					if (property.Value?.TryParseObject(out color) == true)
					{
						list.Add(property);
						return list;
					}

					Dictionary<string, float> dict;
					if (property.Value?.TryParseObject(out dict) == true)
						// ignored
						return list;

					foreach (var nestedProperty in nestedObject.Properties())
						list.AddRange(GetProperties(nestedProperty));

					return list;
				}

			list.Add(property);
			return list;
		}

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

		public static bool HasLength(this string[] arr, int iMinimum = 1)
		{
			return arr != null && arr.Length >= iMinimum;
		}

		public static string ReplaceSpaceToUnicode(this string str)
		{
			return str.Replace(" ", "U+0020");
		}

		public static string ReplaceUnicodeToSpace(this string str)
		{
			return str.Replace("U+0020", " ");
		}

		public static bool IsHex(this string s)
		{
			return s.Length == 6 && Regex.IsMatch(s, "^[0-9A-Fa-f]+$");
		}

#if TESTING
		public static void SayDebug(this object input, string message)
		{
			Debug.Log($"[TESTING] {message}: {input}");
		}
#endif
	}
}

#endregion Extension Methods