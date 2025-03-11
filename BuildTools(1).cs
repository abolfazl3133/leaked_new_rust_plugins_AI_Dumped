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
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;
using Global = Rust.Global;
using Time = UnityEngine.Time;
using Utility = Oxide.Core.Utility;
using Oxide.Plugins.BuildToolsExtensionMethods;

#if TESTING
using System.Diagnostics;
#endif

namespace Oxide.Plugins
{
	[Info("Build Tools", "Mevent", "1.5.37")]
	public class BuildTools : RustPlugin
	{
		#region Fields

		[PluginReference] private Plugin
			ImageLibrary = null,
			NoEscape = null,
			DefendableHomes = null,
			Clans = null,
			Friends = null,
			PersonalVaultDoor = null,
			Notify = null,
			UINotify = null,
			LangAPI = null;

		private const bool LangRu = false;

		private static BuildTools _instance;

		private enum Types
		{
			None = 0,
			Remove = 5,
			Down = 6,
			Wood = 1,
			Stone = 2,
			Metal = 3,
			TopTier = 4
		}

		private const string
			Layer = "UI.BuildTools",
			CrosshairLayer = "UI.BuildTools.Sight",
			CupboardLayer = "UI.BuildTools.Cupboard",
			CMD_Main_Console = "UI_Builder",
			PERM_EDIT = "BuildTools.edit",
			PermFree = "buildtools.free",
			HammerShortname = "hammer",
			ToolGunShortname = "toolgun",
			BuldingPlannerShortname = "building.planner";

		private Dictionary<string, string> _shortPrefabNamesToItem = new Dictionary<string, string>();

		private bool
			_needImageLibrary,
			_enabledImageLibrary;

		private Dictionary<int, Mode> _modeByType = new Dictionary<int, Mode>();

		private readonly Dictionary<uint, string> colors = new Dictionary<uint, string>()
		{
			[1] = "0.38 0.56 0.74 1.0", [2] = "0.45 0.71 0.34 1.0",
			[3] = "0.57 0.29 0.83 1.0", [4] = "0.42 0.17 0.11 1.0",
			[5] = "0.82 0.46 0.13 1.0", [6] = "0.87 0.87 0.87 1.0",
			[7] = "0.20 0.20 0.18 1.0", [8] = "0.40 0.33 0.27 1.0",
			[9] = "0.20 0.22 0.34 1.0", [10] = "0.24 0.35 0.20 1.0",
			[11] = "0.73 0.30 0.18 1.0", [12] = "0.78 0.53 0.39 1.0",
			[13] = "0.84 0.66 0.22 1.0", [14] = "0.34 0.33 0.31 1.0",
			[15] = "0.21 0.34 0.37 1.0", [16] = "0.66 0.61 0.56 1.0"
		};

		private const uint _useRandomColor = 23435621;

		#endregion

		#region Config

		private static Configuration _config;

		private class Configuration
		{
			[JsonProperty(PropertyName = LangRu ? "Разрешение для использования плагина (прим: buildtools.use)" : "Permission to use plugin (ex: buildtools.use)")]
			public string Permission = string.Empty;
			
			[JsonProperty(PropertyName = LangRu ? "Команды для удаления" : "Remove Commands")]
			public string[] RemoveCommands = {"remove"};

			[JsonProperty(PropertyName = LangRu ? "Команды для улучшения" : "Upgrade Commands")]
			public string[] UpgradeCommands = {"up", "building.upgrade"};

			[JsonProperty(PropertyName = LangRu ? "Команды для понижения" : "Downgrade Commands")]
			public string[] DowngradeCommands = {"down", "down.grade", "downgrade"};

			[JsonProperty(PropertyName = LangRu ? "Команды настроек" : "Settings Commands")]
			public string[] SettingsCommands = {"bskin", "buildingskin", "bsettings"};

			[JsonProperty(PropertyName = LangRu ? "Работать с Notify?" : "Work with Notify?")]
			public bool UseNotify = true;

			[JsonProperty(PropertyName = LangRu ? "Работать с PersonalVaultDoor?" : "Work with PersonalVaultDoor?")]
			public bool UsePersonalVaultDoor = true;

			[JsonProperty(PropertyName = LangRu ? "Включить работу с LangAPI?" : "Work with LangAPI?")]
			public bool UseLangAPI = true;

			[JsonProperty(PropertyName =
				LangRu ? "Использовать действия по удару молотком по объекту?" : "Use hammer hit actions on entity?")]
			public bool UseHammer = true;

			[JsonProperty(PropertyName =
				LangRu
					? "Переключение между режимами с помощью щелчка колёсика мыши?"
					: "Switching between modes with a middle click?")]
			public bool SwitchModesMiddleClick = false;

			[JsonProperty(PropertyName = LangRu ? "Настройки режимов" : "Setting Modes",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<Mode> Modes = new List<Mode>
			{
				new Mode
				{
					Enabled = true,
					Type = Types.Remove,
					Icon = "assets/icons/clear.png",
					Permission = string.Empty,
					UseSkins = false,
					Skins = new List<SkinConf>()
				},
				new Mode
				{
					Enabled = true,
					Type = Types.Wood,
					Icon = "assets/icons/level_wood.png",
					Permission = string.Empty,
					UseSkins = true,
					Skins = new List<SkinConf>
					{
						new SkinConf
						{
							Enabled = true,
							LangKey = "SkinLegacy Wood",
							Permission = string.Empty,
							Skin = 10232
						},
						new SkinConf
						{
							Enabled = true,
							LangKey = "SkinGingerbread",
							Permission = string.Empty,
							Skin = 2
						}
					}
				},
				new Mode
				{
					Enabled = true,
					Type = Types.Stone,
					Icon = "assets/icons/level_stone.png",
					Permission = string.Empty,
					UseSkins = true,
					Skins = new List<SkinConf>
					{
						new SkinConf
						{
							Enabled = true,
							LangKey = "SkinAdobe",
							Permission = string.Empty,
							Skin = 10220
						},
						new SkinConf
						{
							Enabled = true,
							LangKey = "SkinBrick",
							Permission = string.Empty,
							Skin = 10223
						},
						new SkinConf
						{
							Enabled = true,
							LangKey = "SkinBrutalist",
							Permission = string.Empty,
							Skin = 10225
						}
					}
				},
				new Mode
				{
					Enabled = true,
					Type = Types.Metal,
					Icon = "assets/icons/level_metal.png",
					Permission = string.Empty,
					UseSkins = true,
					Skins = new List<SkinConf>
					{
						new SkinConf
						{
							Enabled = true,
							LangKey = "SkinContainer",
							Permission = string.Empty,
							Skin = 10221,
							UseColors = true
						}
					}
				},
				new Mode
				{
					Enabled = true,
					Type = Types.TopTier,
					Icon = "assets/icons/level_top.png",
					Permission = string.Empty,
					UseSkins = false,
					Skins = new List<SkinConf>(),
				},
				new Mode
				{
					Enabled = true,
					Type = Types.Down,
					Icon = "assets/icons/demolish.png",
					Permission = string.Empty,
					UseSkins = false,
					Skins = new List<SkinConf>(),
				}
			};

			[JsonProperty(PropertyName = LangRu ? "Настройки улучшения" : "Upgrade Settings")]
			public UpgradeSettings Upgrade = new UpgradeSettings
			{
				PermissionToAll = "buildtools.all",
				ActionTime = 30,
				Cooldown = 0,
				VipCooldown = new Dictionary<string, int>
				{
					["buildtools.vip"] = 0,
					["buildtools.premium"] = 0
				},
				AfterWipe = 0,
				VipAfterWipe = new Dictionary<string, int>
				{
					["buildtools.vip"] = 0,
					["buildtools.premium"] = 0
				},
				AmountPerTick = 5,
				NotifyRequiredResources = false,
				ShiftAttackToUpgradeAll = false,
				UpdateSkinsOnRightClick = true,
				UpgradeBuildingOnBuilt = true
			};

			[JsonProperty(PropertyName = LangRu ? "Настройки удаления" : "Remove Settings")]
			public RemoveSettings Remove = new RemoveSettings
			{
				PermissionToAll = "buildtools.all",
				ActionTime = 30,
				Cooldown = 0,
				VipCooldown = new Dictionary<string, int>
				{
					["buildtools.vip"] = 0,
					["buildtools.premium"] = 0
				},
				AfterWipe = 0,
				VipAfterWipe = new Dictionary<string, int>
				{
					["buildtools.vip"] = 0,
					["buildtools.premium"] = 0
				},
				Condition = new ConditionSettings
				{
					Default = true,
					Percent = false,
					PercentValue = 0
				},
				ReturnItem = true,
				ReturnPercent = 100,
				ReturnPercents = new Dictionary<string, float>
				{
					["buildtools.vip"] = 100,
					["buildtools.premium"] = 100
				},
				CanFriends = true,
				CanClan = true,
				CanTeams = true,
				RemoveByCupboard = false,
				RemoveItemsContainer = false,
				BlockedList = new Dictionary<string, List<IgnoredBlock>>
				{
					["shortname 1"] = new List<IgnoredBlock>
					{
						new IgnoredBlock
						{
							Skins = new List<string> {"*"},
							CanRemove = false,
							ReturnItem = false,
							ReturnPercent = 100
						}
					},
					["shortname 2"] = new List<IgnoredBlock>
					{
						new IgnoredBlock
						{
							Skins = new List<string> {"*"},
							CanRemove = false,
							ReturnItem = false,
							ReturnPercent = 100
						}
					},
					["shortname 3"] = new List<IgnoredBlock>
					{
						new IgnoredBlock
						{
							Skins = new List<string> {"*"},
							CanRemove = false,
							ReturnItem = false,
							ReturnPercent = 100
						}
					},
				},
				BlockCooldown = new ActionCooldown
				{
					Default = 36000,
					Permissions = new Dictionary<string, float>
					{
						["buildtools.vip"] = 34000,
						["buildtools.premium"] = 32000
					}
				},
				AmountPerTick = 5,
				ShiftAttackToRemoveAll = false,
				Vision = new RemoveSettings.VisionRemoval
				{
					Enabled = false,
					Permission = "buildtools.vision",
					Distance = 5f,
					Crosshair = new CrosshairSettings
					{
						Enabled = false,
						Size = 30f,
						Thickness = 4f,
						Color = IColor.Create("#FFFFFF"),
						DisplayType = "Overlay"
					}
				}
			};

			[JsonProperty(PropertyName = LangRu ? "Настройки понижения" : "Downgrade Settings")]
			public DowngradeSettings Downgrade = new DowngradeSettings
			{
				PermissionToAll = "buildtools.all",
				ActionTime = 30,
				Cooldown = 0,
				VipCooldown = new Dictionary<string, int>
				{
					["buildtools.vip"] = 0,
					["buildtools.premium"] = 0
				},
				AfterWipe = 0,
				VipAfterWipe = new Dictionary<string, int>
				{
					["buildtools.vip"] = 0,
					["buildtools.premium"] = 0
				},
				CanFriends = true,
				CanClan = true,
				CanTeams = true,
				BlockedList = new Dictionary<string, List<string>>
				{
					["shortname 1"] = new List<string>
					{
						"*"
					},
					["shortname 2"] = new List<string>
					{
						"*"
					},
					["shortname 3"] = new List<string>
					{
						"*"
					}
				},
				AmountPerTick = 5,
				ShiftAttackToDowngradeAll = false,
				CanDowngradeToTwigs = false
			};

			[JsonProperty(PropertyName = LangRu ? "Настройки блокировки" : "Block Settings")]
			public BlockSettings Block = new BlockSettings
			{
				UseNoEscape = true,
				UseClans = true,
				UseFriends = true,
				UseCupboard = true,
				NeedCupboard = false
			};

			[JsonProperty(PropertyName = LangRu ? "Настройки UI" : "UI Settings")]
			public InterfaceSettings UI = new InterfaceSettings
			{
				DisplayType = "Overlay",
				Color1 = IColor.Create("#4B68FF"),
				Color2 = IColor.Create("#2C2C2C"),
				Color3 = IColor.Create("#B64040"),
				OffsetY = 0,
				OffsetX = 0,
				ProgressTitleTextSize = 12,
				ProgressTitleTextColor = IColor.Create("#FFFFFF", 60),
				SettingsBackgroundColor = IColor.Create("#0E0E10"),
				SettingsHeaderColor = IColor.Create("#161617"),
				SettingsImageBackgroundColor = IColor.Create("#161617"),
				SettingsSelectedColor = IColor.Create("#4B68FF"),
				SettingsNotSelectedColor = IColor.Create("#242425"),
				SettingsTextColor = IColor.Create("#FFFFFF"),
				PanelBackground = new ImageSettings
				{
					AnchorMin = "0 0",
					AnchorMax = "1 1",
					OffsetMin = "50 50",
					OffsetMax = "200 80",
					Sprite = string.Empty,
					Material = string.Empty,
					Image = string.Empty,
					Color = IColor.Create("#2C2C2C")
				},
				PanelCancelButton = new ButtonSettings
				{
					AnchorMin = "0 0", AnchorMax = "0 0",
					OffsetMin = "5 5", OffsetMax = "25 25",
					Align = TextAnchor.MiddleCenter,
					IsBold = false,
					FontSize = 14,
					Color = IColor.Create("#FFFFFF"),
					ButtonColor = IColor.Create("#000000", 00),
					Sprite = string.Empty,
					Material = string.Empty,
					Image = string.Empty,
					ImageColor = IColor.Create("#FFFFFF")
				},
				PanelSettingsBackground = new ImageSettings
				{
					AnchorMin = "0 0",
					AnchorMax = "1 1",
					OffsetMin = "15 50",
					OffsetMax = "45 80",
					Sprite = string.Empty,
					Material = string.Empty,
					Image = string.Empty,
					Color = IColor.Create("#2C2C2C")
				},
				PanelSettingsIcon = new ImageSettings
				{
					AnchorMin = "0 0",
					AnchorMax = "1 1",
					OffsetMin = "5 5",
					OffsetMax = "-5 -5",
					Sprite = "assets/icons/gear.png",
					Material = string.Empty,
					Image = string.Empty,
					Color = IColor.Create("#FFFFFF")
				},
				Skins = new InterfaceSettings.SkinsUI
				{
					TabUpIndent = 15,
					TabHeight = 20,
					TabWidth = 80,
					TabMargin = 5,
					ImageLeftIndent = -22.5f,
					ImageUpIndent = 60,
					ImageHeight = 190,
					ImageWidth = 190,
					ModeUpIndent = 60,
					ModeHeight = 40,
					ModeWidth = 130,
					ModeMargin = 15,
					SkinButtonLeftIndent = -167.5f,
					SettingsModalBackground = new ImageSettings
					{
						AnchorMin = "0 0",
						AnchorMax = "1 1",
						OffsetMin = "-210 -167.5",
						OffsetMax = "210 167.5",
						Sprite = string.Empty,
						Material = string.Empty,
						Image = string.Empty,
						Color = IColor.Create("#0E0E10")
					},
					SelectColorButton = new ButtonSettings
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = "-22.5 -280",
						OffsetMax = "167.5 -255",
						FontSize = 14,
						IsBold = true,
						Align = TextAnchor.MiddleCenter,
						Color = IColor.Create("#FFFFFF"),
						ButtonColor = IColor.Create("#FF4B4B"),
						Sprite = string.Empty,
						Material = string.Empty,
						Image = string.Empty,
						ImageColor = IColor.Create("#FFFFFF")
					}
				},
				SelectItemImage = "https://i.ibb.co/pJF7XJN/search.png",
			};

			[JsonProperty(PropertyName = LangRu ? "Настройки активного предмета" : "Active Item Settings")]
			public ActiveItemConf ActiveItem = new ActiveItemConf
			{
				Enabled = false,
				Items = new Dictionary<string, ActiveItemConf.ActiveItem>
				{
					[HammerShortname] = new ActiveItemConf.ActiveItem
					{
						Enabled = true,
						DefaultMode = Types.Remove,
						IgnoredSkins = new List<ulong>
						{
							1196009619u
						},
						SaveSelectedMode = false
					},
					[ToolGunShortname] = new ActiveItemConf.ActiveItem
					{
						Enabled = true,
						DefaultMode = Types.Remove,
						IgnoredSkins = new List<ulong>(),
						SaveSelectedMode = false
					},
					["building.planner"] = new ActiveItemConf.ActiveItem
					{
						Enabled = true,
						DefaultMode = Types.Stone,
						IgnoredSkins = new List<ulong>
						{
							1195976254u
						},
						SaveSelectedMode = false
					}
				}
			};

			[JsonProperty(PropertyName = LangRu ? "Настройки биндов" : "Binds Settings",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public BindsConf Binds = new BindsConf
			{
				Enabled = false,
				Buttons = new Dictionary<BUTTON, KeyActionConf>
				{
					[BUTTON.RELOAD] = new KeyActionConf
					{
						Enabled = false,
						Action = KeyAction.OpenOrChangeMode,
						TargetMode = Types.Remove,
						RequiredItems = new List<string>
						{
							"hammer",
							"toolgun"
						}
					},
					[BUTTON.USE] = new KeyActionConf
					{
						Enabled = false,
						Action = KeyAction.OpenOrChangeMode,
						TargetMode = Types.Stone,
						RequiredItems = new List<string>
						{
							"hammer",
							"toolgun",
							"building.planner"
						}
					},
					[BUTTON.FIRE_THIRD] = new KeyActionConf
					{
						Enabled = false,
						Action = KeyAction.ChangeModeToNext,
						TargetMode = Types.None,
						RequiredItems = new List<string>
						{
							"hammer",
							"toolgun",
							"building.planner"
						}
					},
				}
			};

			public VersionNumber Version;
		}

		private class BindsConf
		{
			[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = LangRu ? "Кнопки" : "Buttons",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<BUTTON, KeyActionConf> Buttons = new Dictionary<BUTTON, KeyActionConf>();
		}

		private class KeyActionConf
		{
			[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = LangRu ? "Действие" : "Action")] [JsonConverter(typeof(StringEnumConverter))]
			public KeyAction Action;

			[JsonProperty(PropertyName = LangRu ? "Режим" : "Mode")] [JsonConverter(typeof(StringEnumConverter))]
			public Types TargetMode;

			[JsonProperty(PropertyName = LangRu ? "Требуемый предмет в руке" : "Required items in hand",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> RequiredItems = new List<string>();
		}

		private enum KeyAction
		{
			OpenOrChangeMode,
			ChangeModeToNext,
		}

		private class CrosshairSettings
		{
			[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = LangRu ? "Размер" : "Size")]
			public float Size;

			[JsonProperty(PropertyName = LangRu ? "Толщина" : "Thickness")]
			public float Thickness;

			[JsonProperty(PropertyName = LangRu ? "Цвет" : "Color")]
			public IColor Color;

			[JsonProperty(PropertyName = LangRu ? "Тип отображения (Overlay/Hud)" : "Display type (Overlay/Hud)")]
			public string DisplayType;
		}

		private class ActiveItemConf
		{
			[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = LangRu ? "Предметы" : "Items",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, ActiveItem> Items;

			public class ActiveItem
			{
				[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
				public bool Enabled;

				[JsonProperty(PropertyName = LangRu ? "Режим по умолчанию" : "Default Mode")]
				[JsonConverter(typeof(StringEnumConverter))]
				public Types DefaultMode;

				[JsonProperty(PropertyName = LangRu ? "Игнорируемые скины" : "Ignored Skins",
					ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public List<ulong> IgnoredSkins = new List<ulong>();

				[JsonProperty(PropertyName =
					LangRu ? "Сохранять выделенный режим при закрытии?" : "Save the selected mode when closing?")]
				public bool SaveSelectedMode;

				[JsonIgnore] private Mode _mode;

				public Mode GetMode(BasePlayer player, string item)
				{
#if TESTING
					Debug.Log($"[ActiveItem.GetMode] player={player.UserIDString}, item={item}");
#endif

					if (SaveSelectedMode)
					{
						var selectedMode = PlayerData.GetOrLoad(player.UserIDString)?.GetSelectedMode(item);
						if (selectedMode != null)
						{
#if TESTING
							Debug.Log($"[ActiveItem.GetMode] SaveSelectedMode.return: {selectedMode.Type}");
#endif
							return selectedMode;
						}
					}

#if TESTING
					Debug.Log($"[ActiveItem.GetMode] return default");
#endif
					return _mode;
				}

				public void SetMode(Mode mode)
				{
					_mode = mode;
				}
			}

			public void Init()
			{
				foreach (var item in Items.Values)
					item.SetMode(_instance.GetModeByType(item.DefaultMode));
			}
		}

		private class ActionCooldown
		{
			[JsonProperty(PropertyName = LangRu ? "По умолчанию" : "Default")]
			public float Default;

			[JsonProperty(PropertyName = LangRu ? "Разрешения" : "Permissions",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, float> Permissions;

			public float GetCooldown(BasePlayer player)
			{
				var result = Default;

				foreach (var check in Permissions.Where(check =>
					         player.IPlayer.HasPermission(check.Key) && result < check.Value))
					result = check.Value;

				return result;
			}
		}

		private class InterfaceSettings
		{
			[JsonProperty(PropertyName = LangRu ? "Тип отображения (Overlay/Hud)" : "Display type (Overlay/Hud)")]
			public string DisplayType;

			[JsonProperty(PropertyName = LangRu ? "Цвет 1" : "Color 1")]
			public IColor Color1;

			[JsonProperty(PropertyName = LangRu ? "Цвет 2" : "Color 2")]
			public IColor Color2;

			[JsonProperty(PropertyName = LangRu ? "Цвет 3" : "Color 3")]
			public IColor Color3;

			[JsonProperty(PropertyName = LangRu ? "Сдвиг Y" : "Offset Y")]
			public float OffsetY;

			[JsonProperty(PropertyName = LangRu ? "Сдвиг X" : "Offset X")]
			public float OffsetX;

			[JsonProperty(PropertyName = LangRu ? "Размер текста заголовка прогресса" : "Progress Title Text Size")]
			public int ProgressTitleTextSize;

			[JsonProperty(PropertyName = LangRu ? "Цвет текста заголовка прогресса" : "Progress Title Text Color")]
			public IColor ProgressTitleTextColor;

			[JsonProperty(PropertyName = LangRu ? "Цвет фона настроек" : "Settings Background Color")]
			public IColor SettingsBackgroundColor;

			[JsonProperty(PropertyName = LangRu ? "Цвет заголовка настроек" : "Settings Header Color")]
			public IColor SettingsHeaderColor;

			[JsonProperty(PropertyName = LangRu ? "Цвет фона изображения настроек" : "Settings Image Background Color")]
			public IColor SettingsImageBackgroundColor;

			[JsonProperty(PropertyName = LangRu ? "Цвет выбранного в настройках" : "Settings Selected Color")]
			public IColor SettingsSelectedColor;

			[JsonProperty(PropertyName = LangRu ? "Цвет не выбранного в настройках" : "Settings Not Selected Color")]
			public IColor SettingsNotSelectedColor;

			[JsonProperty(PropertyName = LangRu ? "Цвет текста настроек" : "Settings Text Color")]
			public IColor SettingsTextColor;

			[JsonProperty(PropertyName = LangRu ? "Панель фна" : "Panel Background")]
			public ImageSettings PanelBackground;

			[JsonProperty(PropertyName = LangRu ? "Панель кнопки отмены" : "Panel Cancel Button")]
			public ButtonSettings PanelCancelButton;

			[JsonProperty(PropertyName = LangRu ? "Панель фона кнопки настроек" : "Panel Settings Background")]
			public ImageSettings PanelSettingsBackground;

			[JsonProperty(PropertyName = LangRu ? "Панель иконки кнопки настроек" : "Panel Settings Icon")]
			public ImageSettings PanelSettingsIcon;

			[JsonProperty(PropertyName = LangRu ? "Скины" : "Skins")]
			public SkinsUI Skins;

			[JsonProperty(PropertyName = LangRu ? "Изображение иконки выбора предмета" : "Select Item Image")]
			public string SelectItemImage;

			public class SkinsUI
			{
				[JsonProperty(PropertyName = LangRu ? "Отступ для вкладки сверху" : "Tab Up Indent")]
				public float TabUpIndent;

				[JsonProperty(PropertyName = LangRu ? "Высота вкладки" : "Tab Height")]
				public float TabHeight;

				[JsonProperty(PropertyName = LangRu ? "Ширина вкладки" : "Tab Width")]
				public float TabWidth;

				[JsonProperty(PropertyName = LangRu ? "Отступ между вкладками" : "Tab Margin")]
				public float TabMargin;

				[JsonProperty(PropertyName = LangRu ? "Отступ слева для изображения" : "Image Left Indent")]
				public float ImageLeftIndent;

				[JsonProperty(PropertyName = LangRu ? "Отступ сверху для изображения" : "Image Up Indent")]
				public float ImageUpIndent;

				[JsonProperty(PropertyName = LangRu ? "Высота изображения" : "Image Height")]
				public float ImageHeight;

				[JsonProperty(PropertyName = LangRu ? "Ширина изображения" : "Image Width")]
				public float ImageWidth;

				[JsonProperty(PropertyName = LangRu ? "Отступ сверху для режима" : "Mode Up Indent")]
				public float ModeUpIndent;

				[JsonProperty(PropertyName = LangRu ? "Высота режима" : "Mode Height")]
				public float ModeHeight;

				[JsonProperty(PropertyName = LangRu ? "Ширина режима" : "Mode Width")]
				public float ModeWidth;

				[JsonProperty(PropertyName = LangRu ? "Отступ между режимами" : "Mode Margin")]
				public float ModeMargin;

				[JsonProperty(PropertyName = LangRu ? "Отступ слева для кнопки скина" : "Skin Button Left Indent")]
				public float SkinButtonLeftIndent;

				[JsonProperty(PropertyName = LangRu ? "Фон модального окна настроек" : "Settings Modal Background")]
				public ImageSettings SettingsModalBackground;

				[JsonProperty(PropertyName = LangRu ? "Цвет кнопки выбора" : "Select Color Button")]
				public ButtonSettings SelectColorButton;
			}
		}

		#region Configuration.UI

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
					list.Add(new CuiElement()
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

					list.Add(new CuiElement()
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
				{
					list.Add(new CuiElement
					{
						Parent = name,
						Components =
						{
							GetTextComponent(msg),
							new CuiRectTransformComponent()
						}
					});
				}

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

		#endregion

		private class ConditionSettings
		{
			[JsonProperty(PropertyName = LangRu ? "По умолчанию (из игры)" : "Default (from game)")]
			public bool Default;

			[JsonProperty(PropertyName = LangRu ? "Использовать процент?" : "Use percent?")]
			public bool Percent;

			[JsonProperty(PropertyName = LangRu ? "Процент (значение)" : "Percent (value)")]
			public float PercentValue;
		}

		private class BlockSettings
		{
			[JsonProperty(PropertyName = LangRu ? "Работать с NoEscape?" : "Work with NoEscape?")]
			public bool UseNoEscape;

			[JsonProperty(PropertyName =
				LangRu
					? "Работать с Clans? (участники клана смогут удалять/улучшать)"
					: "Work with Clans? (clan members will be able to delete/upgrade)")]
			public bool UseClans;

			[JsonProperty(PropertyName =
				LangRu
					? "Работать с Friends? (друзья смогут удалять/улучшать)"
					: "Work with Friends? (friends will be able to delete/upgrade)")]
			public bool UseFriends;

			[JsonProperty(PropertyName =
				LangRu
					? "Могут ли те, кто имеет доступ к шкафу, удалять/улучшать?"
					: "Can those authorized in the cupboard delete/upgrade?")]
			public bool UseCupboard;

			[JsonProperty(PropertyName =
				LangRu ? "Требуется ли шкаф для улучшения/удаления?" : "Is an upgrade/remove cupboard required?")]
			public bool NeedCupboard;
		}

		private abstract class TotalSettings
		{
			[JsonProperty(PropertyName =
				LangRu
					? "Разрешения на изменение всех объектов (/command all)"
					: "Permission to modify all entities (/command all) ")]
			public string PermissionToAll;

			[JsonProperty(PropertyName = LangRu ? "Время действия" : "Time of action")]
			public int ActionTime;

			[JsonProperty(PropertyName =
				LangRu ? "Задержка (по умолчанию | 0 - отключить)" : "Cooldown (default | 0 - disable)")]
			public int Cooldown;

			[JsonProperty(PropertyName = LangRu ? "Задержки" : "Cooldowns",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, int> VipCooldown;

			[JsonProperty(PropertyName =
				LangRu
					? "Блокировка после вайпа (по умолчанию | 0 - отключить)"
					: "Block After Wipe (default | 0 - disable)")]
			public int AfterWipe;

			[JsonProperty(PropertyName = LangRu ? "Блокировки после вайпа" : "Block After Wipe",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, int> VipAfterWipe;

			public int GetCooldown(BasePlayer player)
			{
				return (from check in VipCooldown
					where player.IPlayer.HasPermission(check.Key)
					select check.Value).Prepend(Cooldown).Min();
			}

			public int GetWipeCooldown(BasePlayer player)
			{
				return (from check in VipAfterWipe
					where player.IPlayer.HasPermission(check.Key)
					select check.Value).Prepend(AfterWipe).Min();
			}

			public bool HasAllPermission(BasePlayer player)
			{
				return string.IsNullOrWhiteSpace(PermissionToAll) ||
				       _instance.permission.UserHasPermission(player.UserIDString, PermissionToAll);
			}
		}

		private class UpgradeSettings : TotalSettings
		{
			[JsonProperty(PropertyName =
				LangRu ? "Количество улучшаемых сущностей за такт" : "Amount of upgrade entities per tick")]
			public int AmountPerTick;

			[JsonProperty(PropertyName =
				LangRu
					? "Уведомлять игрока о необходимых ресурсах для улучшения здания, когда у него недостаточно ресурсов"
					: "Notify the player of the required resources for upgrading a building when they do not have enough resources")]
			public bool NotifyRequiredResources;

			[JsonProperty(PropertyName =
				LangRu ? "Нажмите Shift + Attack для upgrade all" : "Press Shift + Attack to Upgrade All")]
			public bool ShiftAttackToUpgradeAll;

			[JsonProperty(PropertyName =
				LangRu
					? "Обновлять скины зданий при внутриигровом улучшении?"
					: "Updating building skins on right-click upgrade?")]
			public bool UpdateSkinsOnRightClick;

			[JsonProperty(PropertyName = LangRu ? "Настройки скинов" : "Skins Settings")]
			public SkinsInfo Skins = new SkinsInfo
			{
				Enabled = true,
				Icon = "assets/icons/gear.png",
				Images = new Dictionary<ulong, string>
				{
					[10220] = "https://i.ibb.co/GRjNqrq/ty1gZVS.png",
					[10221] = "https://i.ibb.co/N1ZtZ1h/CSTnZ9V.png",
					[10223] = "https://i.ibb.co/6yv2gCp/SGB52rr.png",
					[10225] = "https://i.ibb.co/HgjXDCp/512fx512fdpx2x.png",
					[10232] = "https://i.ibb.co/zWcN5wR/512fx512f.png",
					[2] = "https://i.ibb.co/ZSjMHJt/gingerbread.png"
				}
			};

			[JsonProperty(PropertyName =
				LangRu
					? "Улучшать здания во время их строительства?"
					: "Upgrade buildings while they are being built?")]
			public bool UpgradeBuildingOnBuilt = true;
		}

		private class SkinsInfo
		{
			[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = LangRu ? "Иконка (assets/url)" : "Icon (assets/url)")]
			public string Icon;

			[JsonProperty(PropertyName = LangRu ? "Изображения для скинов" : "Image for skins",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<ulong, string> Images = new Dictionary<ulong, string>();
		}

		private class SkinConf
		{
			[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = LangRu ? "Ключ lang" : "Lang Key")]
			public string LangKey;

			[JsonProperty(PropertyName = LangRu ? "Разрешение" : "Permission")]
			public string Permission;

			[JsonProperty(PropertyName = LangRu ? "Скин" : "Skin")]
			public ulong Skin;

			[JsonProperty(PropertyName = LangRu ? "Использовать цвета?" : "Enable colors?")]
			public bool UseColors;
		}

		private class RemoveSettings : TotalSettings
		{
			[JsonProperty(
				PropertyName = LangRu
					? "Заблокированные предметы для удаления (предмет – настройки)"
					: "Blocked items to remove (prefab – settings)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, List<IgnoredBlock>> BlockedList;

			[JsonProperty(PropertyName = LangRu ? "Возвращать предмет" : "Return Item")]
			public bool ReturnItem;

			[JsonProperty(PropertyName = LangRu ? "Процент возвращаемого предмета" : "Returnable Item Percentage")]
			public float ReturnPercent;

			[JsonProperty(
				PropertyName = LangRu
					? "Проценты возвращаемых предметов (разрешение – процент)"
					: "Percentages of returnable items (permission – percent)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, float> ReturnPercents = new Dictionary<string, float>();

			[JsonProperty(PropertyName =
				LangRu ? "Могут ли друзья удалять? (Friends)" : "Can friends remove? (Friends)")]
			public bool CanFriends;

			[JsonProperty(PropertyName =
				LangRu ? "Могут ли члены клана удалять? (Clans)" : "Can clanmates remove? (Clans)")]
			public bool CanClan;

			[JsonProperty(PropertyName = LangRu ? "Могут ли товарищи по команде удалять?" : "Can teammates remove?")]
			public bool CanTeams;

			[JsonProperty(PropertyName =
				LangRu
					? "Удаление по шкафу? (только авторизованные в шкафе могут удалять)"
					: "Remove by cupboard? (those who are authorized in the cupboard can remove)")]
			public bool RemoveByCupboard;

			[JsonProperty(PropertyName =
				LangRu ? "Удалять контейнер, в котором находятся предметы" : "Remove container with items?")]
			public bool RemoveItemsContainer;

			[JsonProperty(PropertyName = LangRu ? "Настройка состояния" : "Condition Settings")]
			public ConditionSettings Condition;

			[JsonProperty(PropertyName =
				LangRu ? "Настройки задержки после спавна объекта" : "Block Cooldown After Spawn Settings")]
			public ActionCooldown BlockCooldown;

			[JsonProperty(PropertyName =
				LangRu ? "Количество удаляемых объектов за такт" : "Amount of remove entities per tick")]
			public int AmountPerTick;

			[JsonProperty(PropertyName =
				LangRu ? "Нажмите Shift + Attack для remove all" : "Press Shift + Attack to Remove All")]
			public bool ShiftAttackToRemoveAll;

			[JsonProperty(PropertyName = LangRu ? "Настройки зрительного удаления" : "Vision Removal")]
			public VisionRemoval Vision;

			public class VisionRemoval
			{
				[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
				public bool Enabled;

				[JsonProperty(PropertyName = LangRu ? "Разрешение" : "Permission")]
				public string Permission;

				[JsonProperty(PropertyName = LangRu ? "Дистанция" : "Distance")]
				public float Distance;

				[JsonProperty(PropertyName = LangRu ? "Прицел" : "Crosshair")]
				public CrosshairSettings Crosshair;
			}

			public float GetReturnPercent(BasePlayer player)
			{
				var result = ReturnPercent;

				foreach (var returnPercent in ReturnPercents)
					if (_instance?.permission?.UserHasPermission(returnPercent.Key, player.UserIDString) == true &&
					    returnPercent.Value > result)
						result = returnPercent.Value;

				return result;
			}

			[JsonIgnore] private Dictionary<string, Dictionary<string, IgnoredBlock>> _ignoredBlocks;

			public void Init()
			{
				_ignoredBlocks = new Dictionary<string, Dictionary<string, IgnoredBlock>>();

				foreach (var block in BlockedList)
				{
					_ignoredBlocks[block.Key] = new Dictionary<string, IgnoredBlock>();

					foreach (var ignoredBlock in block.Value)
					{
						foreach (var ignoredSkin in ignoredBlock.Skins)
						{
							IgnoredBlock check;
							if (!_ignoredBlocks[block.Key].TryGetValue(ignoredSkin, out check))
							{
								_ignoredBlocks[block.Key].Add(ignoredSkin, ignoredBlock);
							}
						}
					}
				}
			}

			public IgnoredBlock GetIgnoredBlock(BaseEntity entity)
			{
				Dictionary<string, IgnoredBlock> dictionary;
				if (!_ignoredBlocks.TryGetValue(entity.name, out dictionary)) return null;

				IgnoredBlock block;
				return
					dictionary.TryGetValue(entity.skinID.ToString(), out block) ? block :
					dictionary.TryGetValue("*", out block) ? block :
					null;
			}
		}

		private class DowngradeSettings : TotalSettings
		{
			[JsonProperty(PropertyName =
					LangRu
						? "Заблокированные предметы для понижения уровня (prefab – скины, при '*' - все скины)"
						: "Blocked items to downgrade (prefab – skins, but '*' - all skins)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, List<string>> BlockedList;

			[JsonProperty(PropertyName =
				LangRu ? "Могут ли друзья понижать уровень? (Friends)" : "Can friends downgrade? (Friends)")]
			public bool CanFriends;

			[JsonProperty(PropertyName =
				LangRu ? "Могут ли члены клана понижать уровень? (Clans)" : "Can clanmates downgrade? (Clans)")]
			public bool CanClan;

			[JsonProperty(PropertyName =
				LangRu ? "Могут ли товарищи по команде понижать уровень?" : "Can teammates downgrade?")]
			public bool CanTeams;

			[JsonProperty(PropertyName =
				LangRu ? "Количество объектов для понижения уровня за такт" : "Amount of downgrade entities per tick")]
			public int AmountPerTick;

			[JsonProperty(PropertyName =
				LangRu ? "Нажмите Shift + Attack для downgrade all" : "Press Shift + Attack to Downgrade All")]
			public bool ShiftAttackToDowngradeAll;

			[JsonProperty(PropertyName =
				LangRu ? "Можно ли понизить до соломы?" : "Is it possible to downgrade to Twigs?")]
			public bool CanDowngradeToTwigs;

			public bool IsIgnored(BaseEntity entity)
			{
				List<string> list;
				if (!BlockedList.TryGetValue(entity.name, out list))
					return false;
				return list.Contains("*") || list.Contains(entity.skinID.ToString());
			}
		}

		private class IgnoredBlock
		{
			[JsonProperty(PropertyName = LangRu ? "Скины (* – все)" : "Skins (* – all)")]
			public List<string> Skins;

			[JsonProperty(PropertyName = LangRu ? "Можно удалить?" : "Can remove?")]
			public bool CanRemove;

			[JsonProperty(PropertyName = LangRu ? "Вернуть предмет" : "Return Item")]
			public bool ReturnItem;

			[JsonProperty(PropertyName = LangRu ? "Процент возвращаемого предмета" : "Returnable Item Percentage")]
			public float ReturnPercent;
		}

		private class Mode
		{
			[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = LangRu ? "Иконка (assets/url)" : "Icon (assets/url)")]
			public string Icon;

			[JsonProperty(PropertyName =
				LangRu ? "Тип (Remove/Wood/Stone/Metal/TopTier)" : "Type (Remove/Wood/Stone/Metal/TopTier)")]
			[JsonConverter(typeof(StringEnumConverter))]
			public Types Type;

			[JsonProperty(PropertyName = LangRu ? "Разрешение (прим: buildtools.1)" : "Permission (ex: buildtools.1)")]
			public string Permission;

			[JsonProperty(PropertyName = LangRu ? "Включить скины?" : "Enable skins?")]
			public bool UseSkins;

			[JsonProperty(PropertyName = LangRu ? "Скины" : "Skins",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<SkinConf> Skins = new List<SkinConf>();

			public List<SkinConf> GetSkins(BasePlayer player)
			{
				return Skins.FindAll(x =>
					x.Enabled && (string.IsNullOrEmpty(x.Permission) ||
					              _instance.permission.UserHasPermission(player.UserIDString, x.Permission)));
			}
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				_config = Config.ReadObject<Configuration>();
				if (_config == null) throw new Exception();

				if (_config.Version < Version)
					UpdateConfigValues();

				SaveConfig();
			}
			catch (Exception ex)
			{
				PrintError("Your configuration file contains an error. Using default configuration values.");
				LoadDefaultConfig();
				Debug.LogException(ex);
			}
		}

		protected override void SaveConfig()
		{
			Config.WriteObject(_config);
		}

		protected override void LoadDefaultConfig()
		{
			_config = new Configuration();
		}

		private void UpdateConfigValues()
		{
			PrintWarning("Config update detected! Updating config values...");

			if (_config.Version != default(VersionNumber))
			{
				var defaultConfig = new Configuration();

				if (_config.Version < new VersionNumber(1, 3, 0))
					ConvertOldData();

				if (_config.Version < new VersionNumber(1, 5, 10))
				{
					_config.Remove.ReturnPercents = new Dictionary<string, float>
					{
						["buildtools.vip"] = 100,
						["buildtools.premium"] = 100
					};
				}

				if (_config.Version < new VersionNumber(1, 5, 14))
				{
					foreach (var mode in _config.Modes)
					{
						if (mode.Type == Types.Stone && mode.Skins.All(x => x.LangKey != "SkinBrick"))
						{
							mode.Skins.Add(new SkinConf
							{
								Enabled = true,
								LangKey = "SkinBrick",
								Permission = string.Empty,
								Skin = 10223
							});
						}
					}

					if (_config.Upgrade.Skins.Images.ContainsKey(10223) == false)
						_config.Upgrade.Skins.Images.TryAdd(10223, "https://i.ibb.co/6yv2gCp/SGB52rr.png");
				}

				if (_config.Version < new VersionNumber(1, 5, 17))
				{
					foreach (var activeItem in _config.ActiveItem.Items)
					{
						switch (activeItem.Key)
						{
							case HammerShortname:
							{
								activeItem.Value.IgnoredSkins = new List<ulong>
								{
									1196009619u
								};
								break;
							}
							case "building.planner":
							{
								activeItem.Value.IgnoredSkins = new List<ulong>
								{
									1195976254u
								};
								break;
							}
							default:
							{
								activeItem.Value.IgnoredSkins = new List<ulong>();
								break;
							}
						}
					}
				}

				if (_config.Version == new VersionNumber(1, 5, 18))
				{
					foreach (var activeItem in _config.ActiveItem.Items.Values)
						if (activeItem.IgnoredSkins == null)
							activeItem.IgnoredSkins = new List<ulong>();
				}

				if (_config.Version < new VersionNumber(1, 5, 20))
				{
					var oldValue = Config["Block Settings", "Is an upgrade/remove cupbaord required?"];
					if (oldValue != null)
					{
						_config.Block.NeedCupboard = Convert.ToBoolean(oldValue);
					}
				}

				if (_config.Version < new VersionNumber(1, 5, 21))
				{
					_config.Remove.BlockedList = new Dictionary<string, List<IgnoredBlock>>();

					var confObj = Config["Remove Settings", "Blocked items to remove (prefab)"];
					if (confObj != null)
					{
						var list = (IList) confObj;

						foreach (var block in list.Cast<string>().ToList())
						{
							_config.Remove.BlockedList.TryAdd(block, new List<IgnoredBlock>
							{
								new IgnoredBlock
								{
									Skins = new List<string> {"*"},
									CanRemove = false,
									ReturnItem = false,
									ReturnPercent = 100
								}
							});
						}
					}
				}

				if (_config.Version < new VersionNumber(1, 5, 12))
				{
					foreach (var mode in _config.Modes)
					{
						if (mode.Type == Types.Stone && mode.Skins.All(x => x.LangKey != "SkinBrutalist"))
						{
							mode.Skins.Add(new SkinConf
							{
								Enabled = true,
								LangKey = "SkinBrutalist",
								Permission = string.Empty,
								Skin = 10225
							});
						}
					}

					if (_config.Upgrade.Skins.Images.ContainsKey(10225) == false)
						_config.Upgrade.Skins.Images.TryAdd(10225, "https://i.ibb.co/HgjXDCp/512fx512fdpx2x.png");

					foreach (var check in _config.Upgrade.Skins.Images.ToArray())
					{
						string newValue;
						switch (check.Value)
						{
							case "https://i.imgur.com/ty1gZVS.png":
								newValue = "https://i.ibb.co/GRjNqrq/ty1gZVS.png";
								break;
							case "https://i.imgur.com/CSTnZ9V.png":
								newValue = "https://i.ibb.co/N1ZtZ1h/CSTnZ9V.png";
								break;
							case "https://i.imgur.com/SGB52rr.png":
								newValue = "https://i.ibb.co/6yv2gCp/SGB52rr.png";
								break;
							default:
								continue;
						}

						if (!string.IsNullOrWhiteSpace(newValue))
							_config.Upgrade.Skins.Images[check.Key] = newValue;
					}
				}

				if (_config.Version < new VersionNumber(1, 5, 28))
				{
					foreach (var mode in _config.Modes)
					{
						if (mode.Type == Types.Wood)
						{
							if (mode.Skins.All(x => x.LangKey != "SkinLegacy Wood"))
							{
								mode.Skins.Add(new SkinConf
								{
									Enabled = true,
									LangKey = "SkinLegacy Wood",
									Permission = string.Empty,
									Skin = 10232
								});
							}

							if (mode.Skins.All(x => x.LangKey != "SkinFrontier Decor Pack"))
							{
								mode.Skins.Add(new SkinConf
								{
									Enabled = true,
									LangKey = "SkinFrontier Decor Pack",
									Permission = string.Empty,
									Skin = 10226
								});
							}
						}
					}

					if (_config.Upgrade.Skins.Images.ContainsKey(10232) == false)
						_config.Upgrade.Skins.Images.TryAdd(10232, "https://i.ibb.co/zWcN5wR/512fx512f.png");

					if (_config.Upgrade.Skins.Images.ContainsKey(10226) == false)
						_config.Upgrade.Skins.Images.TryAdd(10226, "https://i.ibb.co/nCCRk4Q/512fx512f.png");

					if (_config.UI.PanelBackground == null)
						_config.UI.PanelBackground = defaultConfig.UI.PanelBackground;

					if (_config.UI.PanelCancelButton == null)
						_config.UI.PanelCancelButton = defaultConfig.UI.PanelCancelButton;

					if (_config.UI.PanelSettingsBackground == null)
						_config.UI.PanelSettingsBackground = defaultConfig.UI.PanelSettingsBackground;

					if (_config.UI.PanelSettingsIcon == null)
						_config.UI.PanelSettingsIcon = defaultConfig.UI.PanelSettingsIcon;

					if (_config.UI.Skins.SelectColorButton == null)
						_config.UI.Skins.SelectColorButton = defaultConfig.UI.Skins.SelectColorButton;

					if (_config.UI.Skins.SettingsModalBackground == null)
						_config.UI.Skins.SettingsModalBackground = defaultConfig.UI.Skins.SettingsModalBackground;
				}

				if (_config.Version < new VersionNumber(1, 5, 29))
				{
					foreach (var mode in _config.Modes)
						mode.Enabled = true;
				}

				if (_config.Version < new VersionNumber(1, 5, 30))
				{
					string skinImage;
					if (_config.Upgrade.Skins.Images.Remove(10226, out skinImage))
						_config.Upgrade.Skins.Images.TryAdd(2, "https://i.ibb.co/ZSjMHJt/gingerbread.png");

					foreach (var mode in _config.Modes.FindAll(x => x.Type == Types.Wood))
					{
						foreach (var modeSkin in mode.Skins.FindAll(modeSkin => modeSkin.Skin == 10226))
						{
							modeSkin.Skin = 2;
							modeSkin.LangKey = "SkinGingerbread";
						}
					}
				}
			}

			_config.Version = Version;
			PrintWarning("Config update completed!");
		}

		#endregion

		#region Data

		private EntitiesData _entitiesData;

		private void SaveEntities()
		{
			Interface.Oxide.DataFileSystem.WriteObject(Name + "_Entities", _entitiesData);
		}

		private void LoadEntities()
		{
			try
			{
				_entitiesData = Interface.Oxide.DataFileSystem.ReadObject<EntitiesData>(Name + "_Entities");
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			if (_entitiesData == null) _entitiesData = new EntitiesData();
		}

		private class EntitiesData
		{
			[JsonProperty(PropertyName = "Entities", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<ulong, DateTime> Entities = new Dictionary<ulong, DateTime>();
		}

		#endregion

		#region Hooks

		private void Init()
		{
			_instance = this;

			LoadEntities();

			RegisterPermissions();

			RegisterCommands();

			_config.Remove.Init();

			UnsubscribeEntities();

#if TESTING
			StopwatchWrapper.OnComplete = DebugMessage;
#endif
		}

		private void OnServerInitialized()
		{
			LoadImages();

			TryToLoadPersonalVaultDoor();

			LoadItemPrefabs();

			ClearData();

			LoadCacheModes();

			if (_config.ActiveItem.Enabled)
				_config.ActiveItem.Init();

#if EDITING_MODE
			LoadItems();
#endif
		}

		private void Unload()
		{
			if (_parsingSkins != null)
				ServerMgr.Instance.StopCoroutine(_parsingSkins);

			foreach (var player in BasePlayer.activePlayerList)
			{
				CuiHelper.DestroyUi(player, Layer);
				CuiHelper.DestroyUi(player, Layer + ".Settings");
				CuiHelper.DestroyUi(player, CrosshairLayer);
				CuiHelper.DestroyUi(player, Layer + ".Select.Color");

				PlayerData.SaveAndUnload(player.UserIDString);
			}

			Array.ForEach(_buildComponents.Values.ToArray(), build =>
			{
				if (build != null)
					build.Kill();
			});

			SaveEntities();

			_config = null;
			_instance = null;
		}

		private void OnPlayerConnected(BasePlayer player)
		{
			if (player == null || player.IsNpc) return;

			PlayerData.GetOrLoad(player.UserIDString);
		}

		private void OnPlayerDisconnected(BasePlayer player)
		{
			PlayerData.SaveAndUnload(player.UserIDString);

#if EDITING_MODE
			RemoveEditing(player);
#endif
		}

		private void OnPlayerInput(BasePlayer player, InputState input)
		{
			if (player == null || input == null) return;

			if (!string.IsNullOrWhiteSpace(_config.Permission) && !permission.UserHasPermission(player.UserIDString, _config.Permission))
				return;

			if (_config.Remove.Vision?.Enabled == true && input.WasJustPressed(BUTTON.FIRE_PRIMARY))
			{
				var build = GetBuild(player.userID);
				if (build == null) return;

				var mode = build.GetMode();
				if (mode == null ||
				    mode.Type != Types.Remove ||
				    ActiveItemIsBuildingPlan(player)) return;

				if (!string.IsNullOrWhiteSpace(_config.Remove.Vision.Permission) &&
				    !permission.UserHasPermission(player.UserIDString, _config.Remove.Vision.Permission))
					return;

				var entity = GetLookEntity(player);
				if (entity == null)
				{
					SendNotify(player, NotFoundEntity, 1);
					return;
				}

				var cupboard = player.GetBuildingPrivilege();
				if (cupboard == null)
				{
					SendNotify(player, NoCupboard, 1);
					return;
				}

				if (!CanRemoveEntity(player, entity))
					return;

				var ignoredBlock = _config.Remove.GetIgnoredBlock(entity);
				if (ignoredBlock != null)
				{
					if (ignoredBlock.CanRemove == false)
					{
						_instance.SendNotify(player, CantRemove, 1);
						return;
					}
				}

				var data = PlayerData.GetOrCreate(player.UserIDString);

				var cooldown = _config.Remove.GetCooldown(player);
				if (cooldown > 0 && data.HasCooldown(Types.Remove, cooldown))
				{
					SendNotify(player, RemoveCanThrough, 1,
						data.LeftTime(Types.Remove, cooldown));
					return;
				}

				var blockWipe = _config.Remove.GetWipeCooldown(player);
				if (blockWipe > 0 && PlayerData.HasWipeCooldown(blockWipe))
				{
					SendNotify(player, RemoveCanThrough, 1,
						PlayerData.WipeLeftTime(blockWipe));
					return;
				}

				entity.Invoke(() => RemoveEntity(player, entity, ignoredBlock), 0.11f);

				data.LastRemove = DateTime.UtcNow;
				return;
			}

			if (_config.SwitchModesMiddleClick && input.WasJustPressed(BUTTON.FIRE_THIRD))
			{
				GetBuild(player.userID)?.GoNext();
				return;
			}

			if (_config.Binds.Enabled)
			{
				foreach (var bind in _config.Binds.Buttons)
				{
					if (bind.Value.Enabled && input.WasJustPressed(bind.Key))
					{
						StartBindAction(player, bind.Value);
						return;
					}
				}
			}
		}

		private object OnHammerHit(BasePlayer player, HitInfo info)
		{
			if (player == null || info == null) return null;

			if (!string.IsNullOrWhiteSpace(_config.Permission) && !permission.UserHasPermission(player.UserIDString, _config.Permission))
				return null;

			var build = GetBuild(player.userID);
			if (build == null) return null;

			var mode = build.GetMode();
			if (mode == null || !ActiveItemIsHammerOrGunTools(player))
				return null;

			var entity = info.HitEntity as BaseCombatEntity;
			if (entity == null || entity.OwnerID == 0)
				return null;

			var cupboard = entity.GetBuildingPrivilege();
			if (cupboard == null)
			{
				if (_config.Block.NeedCupboard)
				{
					SendNotify(player, CupboardRequired, 1);
					return true;
				}
			}
			else
			{
				if (CheckIsCupboardDefendableHomes(cupboard))
					return null;

				if (!player.CanBuild())
				{
					SendNotify(player, BuildingBlocked, 1);
					return true;
				}
			}

			if (_config.Block.UseNoEscape && NoEscape != null && NoEscape.IsLoaded && IsRaidBlocked(player))
			{
				SendNotify(player, mode.Type == Types.Remove ? RemoveRaidBlocked : UpgradeRaidBlocked, 1);
				return true;
			}

			if (entity.OwnerID != player.userID) //NOT OWNER
			{
				var any =
					(_config.Block.UseFriends && IsFriends(player.OwnerID, entity.OwnerID)) ||
					(_config.Block.UseClans && IsClanMember(player.OwnerID, entity.OwnerID)) ||
					(_config.Block.UseCupboard && (cupboard == null || cupboard.IsAuthed(player)));

				if (!any)
				{
					SendNotify(player,
						mode.Type == Types.Remove ? CantRemove : mode.Type == Types.Down ? CantDowngrade : CantUpgrade,
						1);
					return true;
				}
			}

			switch (mode.Type)
			{
				case Types.Remove:
				{
					var cd = _config.Remove.BlockCooldown.GetCooldown(player);
					if (cd > 0)
					{
						DateTime created;
						if (_entitiesData.Entities.TryGetValue(entity.net.ID.Value, out created))
						{
							var leftTime = DateTime.Now.Subtract(created).TotalSeconds;
							if (leftTime > cd)
							{
								SendNotify(player, RemoveTimeLeft, 1, FormatTime(player, cd));
								return true;
							}
						}
					}

#if TESTING
					SayDebug($"[OnHammerHit.{player.UserIDString}] start check shift attack for remove");
#endif
					if (_config.Remove.ShiftAttackToRemoveAll && player.serverInput.WasDown(BUTTON.SPRINT))
					{
						RemoveAll(player);
						return true;
					}

					break;
				}

				case Types.Down:
				{
					var minLvlToDowngrade = 2;
					if (_config.Downgrade.CanDowngradeToTwigs)
					{
						minLvlToDowngrade = 1;
					}

					var block = entity as BuildingBlock;
					if (block == null || (int) block.grade < minLvlToDowngrade)
					{
						SendNotify(player, CantDowngrade, 1);
						return true;
					}

					var grade = GetTypes(block.grade - 1);
					if (grade == Types.None && !_config.Downgrade.CanDowngradeToTwigs)
					{
						SendNotify(player, CantDowngrade, 1);
						return true;
					}

					if (_config.Downgrade.ShiftAttackToDowngradeAll && player.serverInput.WasDown(BUTTON.SPRINT))
					{
						DowngradeAll(player, grade);
						return true;
					}

					break;
				}

				default:
				{
					var block = entity as BuildingBlock;
					if (block == null)
					{
						SendNotify(player, CantDowngrade, 1);
						return true;
					}

					if ((int) block.grade == (int) mode.Type)
					{
						var skin = PlayerData.GetOrLoad(player.UserIDString)?.GetSkin((int) GetEnum(mode.Type)) ?? 0UL;
						if (block.skinID == skin)
							return null;
					}

					if ((int) block.grade > (int) mode.Type)
					{
						SendNotify(player, CantDowngrade, 1);
						return true;
					}

					if (_config.Upgrade.ShiftAttackToUpgradeAll && player.serverInput.WasDown(BUTTON.SPRINT))
					{
						UpgradeAll(player, mode.Type);
						return true;
					}

					break;
				}
			}

			build.DoIt(entity);

#if TESTING
			SayDebug($"[{nameof(OnHammerHit)} do it ended!");
#endif
			return true;
		}

		private void OnEntityBuilt(Planner plan, GameObject go)
		{
			var player = plan.GetOwnerPlayer();
			if (player == null) return;

			var block = go.ToBaseEntity() as BuildingBlock;
			if (block == null) return;

			_entitiesData.Entities[block.net.ID.Value] = DateTime.Now;

			if (_config.Upgrade.UpgradeBuildingOnBuilt)
			{
				var build = GetBuild(player.userID);
				if (build == null) return;

				var mode = build.GetMode();
				if (mode == null || mode.Type == Types.Remove) return;

				build.DoIt(block);
			}
		}

		private void OnEntityKill(BuildingBlock block)
		{
			if (block == null || block.net?.ID.IsValid != true) return;

			_entitiesData?.Entities?.Remove(block.net.ID.Value);
		}

		#region Plugin References

		private void OnPluginLoaded(Plugin plugin)
		{
			switch (plugin.Name)
			{
				case "PersonalVaultDoor":
					_hasPersonalVaultDoor = true;
					break;
				case "ImageLibrary":
					_enabledImageLibrary = true;
					break;
			}
		}

		private void OnPluginUnloaded(Plugin plugin)
		{
			switch (plugin.Name)
			{
				case "PersonalVaultDoor":
					_hasPersonalVaultDoor = false;
					break;
				case "ImageLibrary":
					_enabledImageLibrary = false;
					break;
			}
		}

		#endregion

		private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
		{
			if (player == null || player.IsNpc) return;

			if (!string.IsNullOrWhiteSpace(_config.Permission) && !permission.UserHasPermission(player.UserIDString, _config.Permission))
				return;

			if (newItem != null &&
			    _config.ActiveItem.Items.TryGetValue(newItem.info.shortname, out var activeItem) &&
			    activeItem.Enabled &&
			    activeItem.IgnoredSkins?.Contains(newItem.skin) != true)
			{
				var build = AddOrGetBuild(player);
				if (build != null)
				{
					if (oldItem != null)
						SaveSelectedMode(player, build, oldItem);

					build.SetMode(activeItem.GetMode(player, newItem.info.shortname));
				}

				return;
			}

			if (oldItem != null &&
			    _config.ActiveItem.Items.TryGetValue(oldItem.info.shortname, out activeItem) &&
			    activeItem.Enabled &&
			    activeItem.IgnoredSkins?.Contains(oldItem.skin) != true)
			{
				CloseBuildingMode(player, oldItem);
			}
		}

		private object OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade)
		{
			if (block == null || player == null || (int) grade < 1) return null;

			if (!string.IsNullOrWhiteSpace(_config.Permission) && !permission.UserHasPermission(player.UserIDString, _config.Permission))
				return null;

			var build = GetBuild(player.userID);
			if (build != null) return null;

			var data = PlayerData.GetOrLoad(player.UserIDString);
			if (data == null) return null;

			var selectedSkin = data.GetSkin((int) grade);
			if (selectedSkin == 0 || block.skinID == selectedSkin) return null;

			NextTick(() =>
			{
				if (block == null || block.IsDestroyed) return;

#if TESTING
				SayDebug($"[OnStructureUpgrade] {player.UserIDString} {block.skinID} -> {selectedSkin})");
#endif
				
				block.ChangeGradeAndSkin(grade, selectedSkin, true);
			});

			return null;
		}

		#endregion

		#region Commands

		private void CmdRemove(IPlayer cov, string command, string[] args)
		{
			var player = cov.Object as BasePlayer;
			if (player == null) return;

			if (_needImageLibrary && _enabledImageLibrary == false)
			{
				SendNotify(player, NoILError, 1);

				BroadcastILNotInstalled();
				return;
			}

			if (!string.IsNullOrWhiteSpace(_config.Permission) && !cov.HasPermission(_config.Permission))
			{
				SendNotify(player, NoPermission, 1);
				return;
			}

			var mode = GetModeByType(Types.Remove);
			if (mode == null || (!string.IsNullOrEmpty(mode.Permission) && !cov.HasPermission(mode.Permission)))
			{
				SendNotify(player, NoPermission, 1);
				return;
			}

			if (args.Length > 0 && args[0] == "all")
			{
				RemoveAll(player);
				return;
			}

			AddOrGetBuild(player).SetMode(mode);
		}

		private void CmdUpgrade(IPlayer cov, string command, string[] args)
		{
			var player = cov.Object as BasePlayer;
			if (player == null) return;

			if (_needImageLibrary && _enabledImageLibrary == false)
			{
				SendNotify(player, NoILError, 1);

				BroadcastILNotInstalled();
				return;
			}

			if (!string.IsNullOrWhiteSpace(_config.Permission) && !cov.HasPermission(_config.Permission))
			{
				SendNotify(player, NoPermission, 1);
				return;
			}

			if (args.Length == 0)
			{
#if TESTING
				using (new StopwatchWrapper("Go Next building with 0 args took {0}ms."))
#endif
				{
					AddOrGetBuild(player).GoNext();
				}

				return;
			}

			switch (args[0])
			{
				case "all":
				{
					Types upgradeType;
					if (args.Length < 2 || ParseType(args[1], out upgradeType) == Types.None)
					{
						SendNotify(player, MsgErrorSyntaxUpgradeAll, 1, command, args[0]);
						return;
					}

					if (!CanUpgradeType(player, upgradeType))
						return;

					UpgradeAll(player, upgradeType);
					break;
				}

				default:
				{
					Types type;
					if (ParseType(args[0], out type) != Types.None)
					{
						var mode = GetPlayerModes(player)?.Find(x => x.Type == type);
						if (mode == null) return;

						if (!CanUpgradeType(player, type))
							return;
#if TESTING
						using (new StopwatchWrapper("Init building took {0}ms."))
#endif
						{
							var build = AddOrGetBuild(player);
							build.SetMode(mode);
						}
					}
					else
					{
#if TESTING
						using (new StopwatchWrapper("Go Next took {0}ms."))
#endif
						{
							AddOrGetBuild(player).GoNext();
						}
					}

					break;
				}
			}
		}

		private void CmdDowngrade(IPlayer cov, string command, string[] args)
		{
			var player = cov.Object as BasePlayer;
			if (player == null) return;

			if (_needImageLibrary && _enabledImageLibrary == false)
			{
				SendNotify(player, NoILError, 1);

				BroadcastILNotInstalled();
				return;
			}

			if (!string.IsNullOrWhiteSpace(_config.Permission) && !cov.HasPermission(_config.Permission))
			{
				SendNotify(player, NoPermission, 1);
				return;
			}

			var mode = GetModeByType(Types.Down);
			if (mode == null || (!string.IsNullOrEmpty(mode.Permission) && !cov.HasPermission(mode.Permission)))
			{
				SendNotify(player, NoPermission, 1);
				return;
			}

			if (args.Length == 0)
			{
#if TESTING
				using (new StopwatchWrapper("Go Next for Downgrade with 0 args took {0}ms."))
#endif
				{
					AddOrGetBuild(player).SetMode(mode);
				}

				return;
			}

			switch (args[0])
			{
				case "all":
				{
					Types upgradeType;
					if (args.Length < 2 || (ParseType(args[1], out upgradeType) == Types.None &&
					                        !_config.Downgrade.CanDowngradeToTwigs))
					{
						SendNotify(player, MsgErrorSyntaxDowngradeAll, 1, command, args[0]);
						return;
					}

					DowngradeAll(player, upgradeType);
					break;
				}

				default:
				{
					AddOrGetBuild(player).SetMode(mode);
					break;
				}
			}
		}

		private void CmdSettings(IPlayer cov, string command, string[] args)
		{
			var player = cov.Object as BasePlayer;
			if (player == null) return;

			if (_needImageLibrary && _enabledImageLibrary == false)
			{
				SendNotify(player, NoILError, 1);

				BroadcastILNotInstalled();
				return;
			}

			if (!string.IsNullOrWhiteSpace(_config.Permission) && !cov.HasPermission(_config.Permission))
			{
				SendNotify(player, NoPermission, 1);
				return;
			}

			OpenSettingsUI(player);
		}

		private void CmdParseBuildingSkins(IPlayer cov, string command, string[] args)
		{
			if ((cov.IsAdmin || cov.IsServer) == false) return;

			if (_parsingSkins != null)
			{
				cov.Reply("Parsing building skins in progress, wait for it");
				return;
			}

			TryParseBuildingSkins();
		}

		[ConsoleCommand(CMD_Main_Console)]
		private void CmdConsoleBuilding(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !arg.HasArgs()) return;

			switch (arg.Args[0])
			{
				case "mode":
				{
					int index;
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out index)) return;

					var mode = GetPlayerModes(player)[index];
					if (mode == null) return;

					AddOrGetBuild(player)?.SetMode(mode);
					break;
				}

				case "open_settings":
				{
					OpenSettingsUI(player);
					break;
				}

				case "settings_mode":
				{
					int type;
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out type)) return;

					SettingsUI(player, type);
					break;
				}

				case "settings_set":
				{
					int type;
					ulong skin;
					if (!arg.HasArgs(3) || !int.TryParse(arg.Args[1], out type) ||
					    !ulong.TryParse(arg.Args[2], out skin)) return;

					PlayerData.GetOrCreate(player.UserIDString)?.ChangeSkin(type, skin);

					SettingsUI(player, type);
					break;
				}

				case "close":
				{
					CloseBuildingMode(player, player.GetActiveItem());
					break;
				}

				case "color":
				{
					if (!arg.HasArgs(3)) return;

					var skin = arg.GetULong(2);

					switch (arg.GetString(1))
					{
						case "open":
						{
							SelectColorUI(player, skin);
							break;
						}

						case "select":
						{
							if (!arg.HasArgs(4)) return;

							var colorID = arg.GetUInt(3);

							var data = PlayerData.GetOrCreate(player.UserIDString);
							if (data == null) return;
							data.SetColor(skin, colorID);

							switch (colorID)
							{
								case _useRandomColor:
								{
									SelectColorUI(player, skin);
									break;
								}

								default:
								{
									player.SetInfo("client.SelectedShippingContainerBlockColour", colorID.ToString());
									break;
								}
							}

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

		#endregion

		#region Interface

		private void SettingsUI(BasePlayer player, int type = 1, bool first = false)
		{
			var container = new CuiElementContainer();

			var modes = GetSettingsPlayerModes(player);

			#region Background

			if (first)
			{
				container.Add(new CuiPanel()
				{
					RectTransform =
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5"
					},
					Image =
					{
						Color = "0 0 0 0"
					},
					CursorEnabled = true
				}, "Overlay", Layer + ".Settings", Layer + ".Settings");
			}

			#endregion

			#region Main

			container.Add(_config.UI.Skins.SettingsModalBackground.GetImage(Layer + ".Settings",
				Layer + ".Settings.Main", Layer + ".Settings.Main"));

			#region Header

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 0", OffsetMax = "0 50"
				},
				Image =
				{
					Color = _config.UI.SettingsHeaderColor.Get
				}
			}, Layer + ".Settings.Main", Layer + ".Settings.Header");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "30 0", OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, SkinChangerTitle),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = _config.UI.SettingsTextColor.Get
				}
			}, Layer + ".Settings.Header");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-50 -37.5",
					OffsetMax = "-25 -12.5"
				},
				Text =
				{
					Text = Msg(player, CloseButton),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 10,
					Color = _config.UI.SettingsTextColor.Get
				},
				Button =
				{
					Close = Layer + ".Settings",
					Color = _config.UI.SettingsSelectedColor.Get
				}
			}, Layer + ".Settings.Header");

#if EDITING_MODE
			if (permission.UserHasPermission(player.UserIDString, PERM_EDIT))
			{
				var btn = container.Add(new CuiPanel()
				{
					RectTransform =
					{
						AnchorMin = "1 1", AnchorMax = "1 1",
						OffsetMin = "-85 -37.5",
						OffsetMax = "-60 -12.5"
					},
					Image =
					{
						Color = HexToCuiColor("#519229")
					}
				}, Layer + ".Settings.Header", Layer + ".Settings.Header" + ".Button.Settings");

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "5 5",
						OffsetMax = "-5 -5",
					},
					Text = {Text = string.Empty},
					Button =
					{
						Color = "1 1 1 1",
						Command = $"{CMD_Main_Console} edit_config start",
						Sprite = "assets/icons/gear.png"
					}
				}, btn);
			}
#endif

			#endregion

			#region Modes

			var constSwitch =
				-(modes.Count * _config.UI.Skins.TabWidth + (modes.Count - 1) * _config.UI.Skins.TabMargin) / 2f;
			var xSwitch = constSwitch;

			foreach (var mode in modes)
			{
				container.Add(new CuiButton()
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = $"{xSwitch} {-_config.UI.Skins.TabUpIndent - _config.UI.Skins.TabHeight}",
						OffsetMax = $"{xSwitch + _config.UI.Skins.TabWidth} {-_config.UI.Skins.TabUpIndent}"
					},
					Text =
					{
						Text = Msg(player, $"SkinChanger_{mode.Type}"),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 14,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = ((Types) type == mode.Type
							? _config.UI.SettingsSelectedColor
							: _config.UI.SettingsNotSelectedColor).Get,
						Command = $"{CMD_Main_Console} settings_mode {(int) mode.Type}"
					}
				}, Layer + ".Settings.Main");

				xSwitch = xSwitch + _config.UI.Skins.TabWidth + _config.UI.Skins.TabMargin;
			}

			#endregion

			#region Mode Params

			var data = PlayerData.GetOrCreate(player.UserIDString);

			var selectedSkin = data.GetSkin(type);
			SkinConf selectSkinConf = null;

			var ySwitch = -_config.UI.Skins.ModeUpIndent;

			#region Default

			container.Add(new CuiButton()
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = $"{_config.UI.Skins.SkinButtonLeftIndent} {ySwitch - _config.UI.Skins.ModeHeight}",
					OffsetMax = $"{_config.UI.Skins.SkinButtonLeftIndent + _config.UI.Skins.ModeWidth} {ySwitch}"
				},
				Text =
				{
					Text = Msg(player, SkinChangerDefault),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = selectedSkin == 0
						? _config.UI.SettingsSelectedColor.Get
						: _config.UI.SettingsNotSelectedColor.Get,
					Command = $"{CMD_Main_Console} settings_set {type} 0"
				}
			}, Layer + ".Settings.Main", ".Settings.Skin.Default");

			ySwitch = ySwitch - _config.UI.Skins.ModeMargin - _config.UI.Skins.ModeHeight;

			#endregion

			#region List

			var nowMode = GetModeByType(type);
			if (nowMode != null && nowMode.UseSkins)
			{
				var skins = nowMode.GetSkins(player);

				foreach (var skin in skins)
				{
					var isSelected = selectedSkin == skin.Skin;
					if (isSelected)
						selectSkinConf = skin;

					container.Add(new CuiButton()
						{
							RectTransform =
							{
								AnchorMin = "0.5 1", AnchorMax = "0.5 1",
								OffsetMin =
									$"{_config.UI.Skins.SkinButtonLeftIndent} {ySwitch - _config.UI.Skins.ModeHeight}",
								OffsetMax =
									$"{_config.UI.Skins.SkinButtonLeftIndent + _config.UI.Skins.ModeWidth} {ySwitch}"
							},
							Text =
							{
								Text = Msg(player, skin.LangKey),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-bold.ttf",
								FontSize = 14,
								Color = "1 1 1 1"
							},
							Button =
							{
								Color = isSelected
									? _config.UI.SettingsSelectedColor.Get
									: _config.UI.SettingsNotSelectedColor.Get,
								Command = $"{CMD_Main_Console} settings_set {type} {skin.Skin}"
							}
						}, Layer + ".Settings.Main", $".Settings.Skin.{skin.Skin}");

					ySwitch = ySwitch - _config.UI.Skins.ModeMargin - _config.UI.Skins.ModeHeight;
				}
			}

			#endregion

			#region Image

			string imageURL;
			if (_config.Upgrade.Skins.Images.TryGetValue(selectedSkin, out imageURL))
			{
				container.Add(new CuiElement()
				{
					Name = Layer + $".Settings.Preview.{selectedSkin}",
					Parent = Layer + ".Settings.Main",
					Components =
					{
						new CuiRawImageComponent()
						{
							Png = _instance.ImageLibrary?.Call<string>("GetImage", imageURL)
						},
						new CuiRectTransformComponent()
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin =
								$"{_config.UI.Skins.ImageLeftIndent} {-_config.UI.Skins.ImageUpIndent - _config.UI.Skins.ImageHeight}",
							OffsetMax =
								$"{_config.UI.Skins.ImageLeftIndent + _config.UI.Skins.ImageWidth} {-_config.UI.Skins.ImageUpIndent}"
						}
					}
				});
			}
			else
			{
				container.Add(new CuiPanel()
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin =
								$"{_config.UI.Skins.ImageLeftIndent} {-_config.UI.Skins.ImageUpIndent - _config.UI.Skins.ImageHeight}",
							OffsetMax =
								$"{_config.UI.Skins.ImageLeftIndent + _config.UI.Skins.ImageWidth} {-_config.UI.Skins.ImageUpIndent}"
						},
						Image =
						{
							Color = _config.UI.SettingsImageBackgroundColor.Get
						}
					}, Layer + ".Settings.Main", Layer + $".Settings.Preview.{selectedSkin}");

				container.Add(new CuiLabel
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text =
						{
							Text = Msg(player, SkinChangerNoImageDescription),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 14,
							Color = "1 1 1 1"
						}
					}, Layer + $".Settings.Preview.{selectedSkin}");
			}

			if (selectSkinConf != null && selectSkinConf.UseColors)
			{
				container.AddRange(_config.UI.Skins.SelectColorButton.Get(
					Msg(player, MsgButtonSelectColor),
					$"{CMD_Main_Console} color open {selectedSkin}",
					Layer + ".Settings.Main"));
			}

			#endregion

			#endregion

			#endregion

			#region Save

			container.Add(new CuiButton()
			{
				RectTransform =
				{
					AnchorMin = "0.5 0", AnchorMax = "0.5 0",
					OffsetMin = "-60 -20",
					OffsetMax = "60 20"
				},
				Text =
				{
					Text = Msg(player, SkinChangerSave),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = _config.UI.SettingsTextColor.Get
				},
				Button =
				{
					Color = _config.UI.SettingsSelectedColor.Get,
					Close = Layer + ".Settings"
				}
			}, Layer + ".Settings.Main");

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private void SelectColorUI(
			BasePlayer player,
			ulong skin = 0UL)
		{
			var container = new CuiElementContainer();

			var colorID = PlayerData.GetOrLoad(player.UserIDString)?.GetColor(skin);

			var amountOnString = 4;

			var Size = 20f;
			var margin = 10f;

			var maxLines = Mathf.CeilToInt((float) colors.Count / amountOnString);

			var additionalMargin = 30f;

			var totalWidth = amountOnString * Size + (amountOnString - 1) * margin + additionalMargin;
			var totalHeight = maxLines * Size + (maxLines - 1) * margin + additionalMargin;

			#region Background

			container.Add(new CuiPanel()
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1"
				},
				Image =
				{
					Color = "0 0 0 0.9",
					Material = "assets/content/ui/uibackgroundblur.mat"
				},
				CursorEnabled = true
			}, "Overlay", Layer + ".Select.Color", Layer + ".Select.Color");

			container.Add(new CuiButton()
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text = {Text = string.Empty},
				Button =
				{
					Color = "0 0 0 0",
					Close = Layer + ".Select.Color"
				}
			}, Layer + ".Select.Color");

			#endregion

			#region Main

			container.Add(new CuiPanel()
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = $"-{totalWidth / 2f} -{totalHeight / 2f}",
					OffsetMax = $"{totalWidth / 2f} {totalHeight / 2f}"
				},
				Image = {Color = _config.UI.SettingsBackgroundColor.Get}
			}, Layer + ".Select.Color", Layer + ".Select.Color.Main", Layer + ".Select.Color.Main");

			#region Colors

			var constX = -(amountOnString * Size + (amountOnString - 1) * margin) / 2f;
			var xSwitch = constX;
			var ySwitch = -(additionalMargin / 2f);

			var index = 1;
			foreach (var color in colors)
			{
				container.Add(new CuiButton()
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = $"{xSwitch} {ySwitch - Size}",
						OffsetMax = $"{xSwitch + Size} {ySwitch}"
					},
					Text =
					{
						Text = color.Key == colorID ? Msg(player, MsgSelectedColor) : string.Empty,
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 1",
					},
					Button =
					{
						Color = color.Value,
						Command = $"{CMD_Main_Console} color select {skin} {color.Key}",
						Close = Layer + ".Select.Color"
					}
				}, Layer + ".Select.Color.Main");

				if (index % amountOnString == 0)
				{
					ySwitch = ySwitch - Size - margin;
					xSwitch = constX;
				}
				else
				{
					xSwitch += Size + margin;
				}

				index++;
			}

			#endregion

			#region Use Random

			var isRandom = colorID == _useRandomColor;

			container.Add(new CuiButton()
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 0",
					OffsetMin = "0 -35",
					OffsetMax = "0 -5"
				},
				Text =
				{
					Text = Msg(player, MsgUseRandom),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = isRandom ? _config.UI.SettingsSelectedColor.Get : _config.UI.SettingsNotSelectedColor.Get,
					Command = isRandom ? string.Empty : $"{CMD_Main_Console} color select {skin} {_useRandomColor}",
					Close = isRandom ? string.Empty : Layer + ".Select.Color.Main.UseRandom"
				}
			}, Layer + ".Select.Color.Main", Layer + ".Select.Color.Main.UseRandom");

			#endregion

			#endregion

			CuiHelper.AddUi(player, container);
		}

		#endregion

		#region Component

		private readonly Dictionary<ulong, BuildComponent> _buildComponents =
			new Dictionary<ulong, BuildComponent>();

		private BuildComponent GetBuild(ulong player)
		{
			BuildComponent build;
			return _buildComponents.TryGetValue(player, out build) ? build : null;
		}

		private BuildComponent AddOrGetBuild(BasePlayer player)
		{
			BuildComponent build;
			if (_buildComponents.TryGetValue(player.userID, out build))
				return build;

			build = player.gameObject.AddComponent<BuildComponent>();
			return build;
		}

		private class BuildComponent : FacepunchBehaviour
		{
			#region Fields

			private BasePlayer _player;

			private Mode _mode;

			private float _startTime;

			private readonly CuiElementContainer _container = new CuiElementContainer();

			private bool _started = true;

			private float _cooldown;

			#endregion

			#region Init

			private void Awake()
			{
				_player = GetComponent<BasePlayer>();

				_instance._buildComponents[_player.userID] = this;

				enabled = false;
			}

			public void SetMode(Mode mode)
			{
				if (mode == null)
					mode = GetPlayerModes(_player).FirstOrDefault();

				_mode = mode;

				_startTime = Time.time;

				_cooldown = GetCooldown();

				MainUi();

				enabled = true;

				_started = true;

				if (_mode?.Type == Types.Remove && _config.Remove.Vision?.Enabled == true &&
				    _config.Remove.Vision.Crosshair?.Enabled == true)
					CrosshairUi();
				else
					CuiHelper.DestroyUi(_player, CrosshairLayer);
			}

			#endregion

			#region Interface

			public void MainUi()
			{
				_container.Clear();

				_container.Add(new CuiPanel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "0 0"},
					Image = {Color = "0 0 0 0"}
				}, _config.UI.DisplayType, Layer, Layer);

				#region Modes

				var modes = GetPlayerModes(_player);

				var width = 30f;
				var margin = 5f;
				var xSwitch = 15f + _config.UI.OffsetX;

				for (var i = 0; i < modes.Count; i++)
				{
					var mode = modes[i];

					_container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = $"{xSwitch} {15 + _config.UI.OffsetY}",
								OffsetMax = $"{xSwitch + width} {45 + _config.UI.OffsetY}"
							},
							Image =
							{
								Color = mode.Type == _mode.Type ? _config.UI.Color1.Get : _config.UI.Color2.Get
							}
						}, Layer, Layer + $".Mode.{i}");

					#region Icon

					if (mode.Icon.Contains("assets/icon"))
						_container.Add(new CuiPanel
							{
								RectTransform =
								{
									AnchorMin = "0 0", AnchorMax = "1 1",
									OffsetMin = "5 5",
									OffsetMax = "-5 -5"
								},
								Image =
								{
									Sprite = $"{mode.Icon}"
								}
							}, Layer + $".Mode.{i}");
					else
						_container.Add(new CuiElement
						{
							Parent = Layer + $".Mode.{i}",
							Components =
							{
								new CuiRawImageComponent
									{Png = _instance.ImageLibrary?.Call<string>("GetImage", mode.Icon)},
								new CuiRectTransformComponent
								{
									AnchorMin = "0 0", AnchorMax = "1 1",
									OffsetMin = "5 5",
									OffsetMax = "-5 -5"
								}
							}
						});

					#endregion

					_container.Add(new CuiButton
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 1"
							},
							Text = {Text = string.Empty},
							Button =
							{
								Color = "0 0 0 0",
								Command = $"{CMD_Main_Console} mode {i}"
							}
						}, Layer + $".Mode.{i}");

					xSwitch += width + margin;

					if (i == 0)
						margin = 0f;
				}

				#endregion

				#region Settings

				if (_config.Upgrade.Skins.Enabled)
				{
					_container.Add(_config.UI.PanelSettingsBackground.GetImage(Layer, Layer + ".Btn.Settings"));

					_container.Add(_config.UI.PanelSettingsIcon.GetImage(Layer + ".Btn.Settings"));

					_container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1"
						},
						Text = {Text = string.Empty},
						Button =
						{
							Color = "0 0 0 0",
							Command = $"{CMD_Main_Console} open_settings"
						}
					}, Layer + ".Btn.Settings");
				}

				#endregion

				#region Update

				_container.Add(_config.UI.PanelBackground.GetImage(Layer, Layer + ".Panel"));

				_container.AddRange(_config.UI.PanelCancelButton.Get(_instance.Msg(_player, CloseMenu),
					$"{CMD_Main_Console} close",
					Layer + ".Panel"));

				#endregion

				CuiHelper.AddUi(_player, _container);
			}

			private void UpdateUi()
			{
				_container.Clear();

				_container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "30 0", OffsetMax = "0 0"
					},
					Image = {Color = "0 0 0 0"}
				}, Layer + ".Panel", Layer + ".Update", Layer + ".Update");

				#region Text

				_container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "0 0", OffsetMax = "0 0"
					},
					Text =
					{
						Text =
							$"{(_mode.Type == Types.Remove ? _instance.Msg(_player, RemoveTitle, GetLeftTime()) : _mode.Type == Types.Down ? _instance.Msg(_player, DowngradeTitle, GetLeftTime()) : _instance.Msg(_player, UpgradeTitle, _instance.Msg(_player, $"{_mode.Type}"), GetLeftTime()))}",
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = _config.UI.ProgressTitleTextSize,
						Color = _config.UI.ProgressTitleTextColor.Get
					}
				}, Layer + ".Update");

				#endregion

				#region Progress

				var progress = (Time.time - _startTime) / _cooldown;
				if (progress > 0)
				{
					_container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0",
							OffsetMin = "-30 0", OffsetMax = "0 2"
						},
						Image =
						{
							Color = "0 0 0 0"
						}
					}, Layer + ".Update", Layer + ".Update.Progress");

					_container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = $"{Mathf.Min(progress, 1f)} 1"
						},
						Image =
						{
							Color = _config.UI.Color3.Get
						}
					}, Layer + ".Update.Progress");
				}

				#endregion

				CuiHelper.AddUi(_player, _container);
			}

			private void CrosshairUi()
			{
				_container.Clear();

				_container.Add(new CuiPanel
				{
					RectTransform = {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5"},
					Image = {Color = "0 0 0 0"}
				}, _config.Remove.Vision.Crosshair.DisplayType, CrosshairLayer, CrosshairLayer);

				// <-
				_container.Add(new CuiPanel()
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin =
							$"-{_config.Remove.Vision.Crosshair.Size} -{_config.Remove.Vision.Crosshair.Thickness / 2f}",
						OffsetMax = $"0 {_config.Remove.Vision.Crosshair.Thickness / 2f}"
					},
					Image =
					{
						Color = _config.Remove.Vision.Crosshair.Color.Get
					}
				}, CrosshairLayer);

				// ->
				_container.Add(new CuiPanel()
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = $"0 -{_config.Remove.Vision.Crosshair.Thickness / 2f}",
						OffsetMax =
							$"{_config.Remove.Vision.Crosshair.Size} {_config.Remove.Vision.Crosshair.Thickness / 2f}"
					},
					Image =
					{
						Color = _config.Remove.Vision.Crosshair.Color.Get
					}
				}, CrosshairLayer);

				// /\
				_container.Add(new CuiPanel()
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = $"-{_config.Remove.Vision.Crosshair.Thickness / 2f} 0",
						OffsetMax =
							$"{_config.Remove.Vision.Crosshair.Thickness / 2f} {_config.Remove.Vision.Crosshair.Size}"
					},
					Image =
					{
						Color = _config.Remove.Vision.Crosshair.Color.Get
					}
				}, CrosshairLayer);

				// \/
				_container.Add(new CuiPanel()
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin =
							$"-{_config.Remove.Vision.Crosshair.Thickness / 2f} -{_config.Remove.Vision.Crosshair.Size}",
						OffsetMax = $"{_config.Remove.Vision.Crosshair.Thickness / 2f} 0"
					},
					Image =
					{
						Color = _config.Remove.Vision.Crosshair.Color.Get
					}
				}, CrosshairLayer);

				CuiHelper.AddUi(_player, _container);
			}

			#endregion

			#region Update

			private void FixedUpdate()
			{
				if (!_started) return;

				var timeLeft = Time.time - _startTime;
				if (timeLeft > _cooldown)
				{
					Kill();
					return;
				}

				UpdateUi();
			}

			#endregion

			#region Main

			public void DoIt(BaseCombatEntity entity)
			{
				if (entity == null) return;

#if TESTING
				SayDebug($"[DoIt] for entity={entity.net.ID}");
#endif

				switch (_mode.Type)
				{
					case Types.Remove:
					{
						if (!CanRemoveEntity(_player, entity))
							return;

						var ignoredBlock = _config.Remove.GetIgnoredBlock(entity);
						if (ignoredBlock != null)
						{
							if (ignoredBlock.CanRemove == false)
							{
								_instance.SendNotify(_player, CantRemove, 1);
								return;
							}
						}

						var data = PlayerData.GetOrCreate(_player.UserIDString);

						var cooldown = _config.Remove.GetCooldown(_player);
						if (cooldown > 0 && data.HasCooldown(_mode.Type, cooldown))
						{
							_instance.SendNotify(_player, RemoveCanThrough, 1,
								data.LeftTime(_mode.Type, cooldown));
							return;
						}

						var blockWipe = _config.Remove.GetWipeCooldown(_player);
						if (blockWipe > 0 && PlayerData.HasWipeCooldown(blockWipe))
						{
							_instance.SendNotify(_player, RemoveCanThrough, 1,
								PlayerData.WipeLeftTime(blockWipe));
							return;
						}

						entity.Invoke(() => RemoveEntity(_player, entity, ignoredBlock), 0.11f);

						data.LastRemove = DateTime.UtcNow;
						break;
					}

					case Types.Down:
					{
						var block = entity as BuildingBlock;
						if (block == null) return;

						var grades = block.blockDefinition.grades;
						if (grades == null) return;

						var currentGrade = block.grade;
						if (currentGrade <= 0)
						{
							_instance.SendNotify(_player, CantDowngrade, 1);
							return;
						}

						var targetGrade = currentGrade - 1;
#if TESTING
						SayDebug(
							$"[DoIt] targetGrade={targetGrade} | check2={grades.Length <= (int) targetGrade} | check3={grades[(int) targetGrade] == null}");
#endif

						if (grades.Length <= (int) targetGrade ||
						    grades[(int) targetGrade] == null)
						{
							_instance.SendNotify(_player, CantDowngrade, 1);
							return;
						}

						var data = PlayerData.GetOrCreate(_player.UserIDString);

						var skin = data.GetSkin((int) targetGrade);

						DowngradeBlock(block, targetGrade, skin);

						if (targetGrade != BuildingGrade.Enum.Twigs)
						{
							Effect.server.Run(
								"assets/bundled/prefabs/fx/build/promote_" + targetGrade.ToString().ToLower() +
								".prefab",
								block,
								0U, Vector3.zero, Vector3.zero);
						}
						
						data.LastDowngrade = DateTime.UtcNow;
						break;
					}

					default:
					{
						var block = entity as BuildingBlock;
						if (block == null) return;

						var data = PlayerData.GetOrCreate(_player.UserIDString);

						var cooldown = _config.Upgrade.GetCooldown(_player);
						if (cooldown > 0 && data.HasCooldown(_mode.Type, cooldown))
						{
							_instance.SendNotify(_player, UpgradeCanThrough, 1,
								data.LeftTime(_mode.Type, cooldown));
							return;
						}

						var blockWipe = _config.Upgrade.GetWipeCooldown(_player);
						if (blockWipe > 0 && PlayerData.HasWipeCooldown(blockWipe))
						{
							_instance.SendNotify(_player, UpgradeCanThrough, 1,
								PlayerData.WipeLeftTime(blockWipe));
							return;
						}

						var enumGrade = GetEnum(_mode.Type);

						var skin = data.GetSkin((int) enumGrade);

						var grade = block.blockDefinition.GetGrade(enumGrade, skin);
						if (grade == null || !block.CanChangeToGrade(enumGrade, skin, _player) ||
						    Interface.CallHook("OnStructureUpgrade", block, _player, enumGrade) != null ||
						    block.SecondsSinceAttacked < 30.0)
							return;

						if (!_instance.permission.UserHasPermission(_player.UserIDString, PermFree))
						{
							if (!block.CanAffordUpgrade(enumGrade, skin, _player))
							{
								_instance.SendNotify(_player, NotEnoughResources, 0);
								return;
							}

							block.PayForUpgrade(grade, _player);
						}

						UpgradeBuildingBlock(block, enumGrade, skin);

						if (enumGrade != BuildingGrade.Enum.Twigs)
						{
							Effect.server.Run(
								"assets/bundled/prefabs/fx/build/promote_" + enumGrade.ToString().ToLower() + ".prefab",
								block,
								0U, Vector3.zero, Vector3.zero);
						}
						
						var color = data.GetColor(skin);
						if (color > 0 && color != _useRandomColor)
							block.SetCustomColour(GetShippingContainerBlockColourForPlayer(_player));

						data.LastUpgrade = DateTime.UtcNow;
						break;
					}
				}

				_startTime = Time.time;
			}

			#endregion

			#region Utils

			private int GetLeftTime()
			{
				return Mathf.RoundToInt(_startTime + _cooldown - Time.time);
			}

			public void GoNext()
			{
				var modes = GetPlayerModes(_player);
				if (modes == null) return;

				if (_mode == null)
				{
					_mode = modes.FindAll(x => x.Type != Types.Remove).FirstOrDefault();
					SetMode(_mode);
					return;
				}

				var i = 0;
				for (; i < modes.Count; i++)
				{
					var mode = modes[i];

					if (mode == _mode)
						break;
				}

				i++;

				var nextMode = modes.Count <= i ? modes[0] : modes[i];

				_mode = nextMode;

				SetMode(nextMode);
			}

			public Mode GetMode()
			{
				return _mode;
			}

			private float GetCooldown()
			{
				switch (_mode.Type)
				{
					case Types.Remove:
						return _config.Remove.ActionTime;
					default:
						return _config.Upgrade.ActionTime;
				}
			}

			#endregion

			#region Destroy

			private void OnDestroy()
			{
				CancelInvoke();

				CuiHelper.DestroyUi(_player, Layer);
				CuiHelper.DestroyUi(_player, CrosshairLayer);

				_instance?._buildComponents.Remove(_player.userID);

				Destroy(this);
			}

			public void Kill()
			{
				enabled = false;

				_started = false;

				DestroyImmediate(this);
			}

			#endregion
		}

		#endregion

		#region Utils

		private void OpenSettingsUI(BasePlayer player)
		{
			var playerType = GetSettingsPlayerModes(player)?.FirstOrDefault();
			SettingsUI(player, playerType != null ? (int) playerType.Type : 0, first: true);
		}

		private List<Mode> GetSettingsPlayerModes(BasePlayer player)
		{
			var modes = GetPlayerModes(player, new[] {Types.None, Types.Remove, Types.Down});
			return modes;
		}

		private bool CanUpgradeType(BasePlayer player, Types upgradeType)
		{
			var mode = GetPlayerModes(player)?.Find(x => x.Type == upgradeType);
			if (mode == null) return false;

			if (!string.IsNullOrEmpty(mode.Permission) &&
			    !permission.UserHasGroup(player.UserIDString, mode.Permission))
			{
				SendNotify(player, NoPermission, 1);
				return false;
			}

			return true;
		}

		private string GetImage(string name) => (string) ImageLibrary?.Call("GetImage", name);

		private void AddImage(string url, string fileName, ulong imageId = 0) =>
			ImageLibrary.Call("AddImage", url, fileName, imageId);


		private void StartBindAction(BasePlayer player, KeyActionConf keyAction)
		{
			if (keyAction.RequiredItems.Count > 0)
			{
				var activeItem = player.GetActiveItem();
				if (activeItem == null || !keyAction.RequiredItems.Contains(activeItem.info.shortname))
					return;
			}

			switch (keyAction.Action)
			{
				case KeyAction.OpenOrChangeMode:
				{
					var build = AddOrGetBuild(player);
					if (build != null) build.SetMode(GetModeByType(keyAction.TargetMode));
					break;
				}

				case KeyAction.ChangeModeToNext:
				{
					GetBuild(player.userID)?.GoNext();
					break;
				}
			}
		}

		private void CloseBuildingMode(BasePlayer player, Item oldItem)
		{
			var build = GetBuild(player.userID);
			if (build == null) return;

			SaveSelectedMode(player, build, oldItem);

			build.Kill();
		}

		private void SaveSelectedMode(BasePlayer player, BuildComponent build, Item oldItem)
		{
			if (player == null || build == null || oldItem == null) return;

			ActiveItemConf.ActiveItem oldActiveItem;
			if (_config.ActiveItem.Items.TryGetValue(oldItem.info.shortname, out oldActiveItem) &&
			    oldActiveItem.Enabled &&
			    oldActiveItem.IgnoredSkins?.Contains(oldItem.skin) != true)
			{
				if (oldActiveItem.SaveSelectedMode)
				{
					PlayerData.GetOrCreate(player.UserIDString)?.SaveSelectedMode(oldItem.info.shortname,
						build.GetMode()?.Type ?? Types.None);
				}
			}
		}

		#region Parse Building Skins

		private Coroutine _parsingSkins;

		private void TryParseBuildingSkins()
		{
			var id = StringPool.Get("assets/prefabs/building core/foundation/foundation.prefab");
			if (id == 0) return;

			var construction = PrefabAttribute.server.Find<Construction>(id);
			if (construction == null) return;

			var toParse = new List<KeyValuePair<Types, ulong>>();

			Array.ForEach(construction.grades, grade =>
			{
				var skin = grade.gradeBase.skin;
				if (skin == 0 || skin > 999999 ||
				    _config.Upgrade.Skins.Images.ContainsKey(skin)) return;

				var type = GetTypes(grade.gradeBase.type);
				if (type == Types.None) return;

				toParse.Add(new KeyValuePair<Types, ulong>(type, skin));
			});

#if TESTING
			SayDebug($"[Parse Skins] {(string.Join(", ", toParse.Select(x => $"[{x.Key}] {x.Value}")))}");
#endif

			Puts($"{toParse.Count} building skins parsing process started.");

			_parsingSkins = ServerMgr.Instance.StartCoroutine(StartParsingSkins(toParse));
		}

		private IEnumerator StartParsingSkins(List<KeyValuePair<Types, ulong>> skins)
		{
			foreach (var check in skins)
			{
				yield return ParseSkinImage(check.Key, check.Value);

				yield return CoroutineEx.waitForSeconds(1);
			}

			_parsingSkins = null;

			Puts($"{skins.Count} building skins were parsed!");
		}

		private const string
			imagePattern = @"<img id=""preview_image"" class=""item_def_image"" src=""(?<src>.*?)""",
			titlePattern = @"<h2 class=""pageheader itemtitle"" style=""color: #35a3f1;"">(?<title>.*?)</h2>";

		private IEnumerator ParseSkinImage(Types type, ulong skin)
		{
			using (var www = UnityWebRequest.Get($"https://store.steampowered.com/itemstore/252490/detail/{skin}/"))
			{
				yield return www.SendWebRequest();

				if (www.result == UnityWebRequest.Result.ConnectionError ||
				    www.result == UnityWebRequest.Result.ProtocolError)
				{
					PrintError($"Failed to download icon: {www.error}");
					yield break;
				}

				var html = www.downloadHandler.text;

				var match = Regex.Match(html, imagePattern);
				if (!match.Success) yield break;

				var url = match.Groups["src"].Value;
				if (string.IsNullOrEmpty(url)) yield break;

				match = Regex.Match(html, titlePattern);
				if (!match.Success) yield break;

				var title = match.Groups["title"].Value;
				if (string.IsNullOrWhiteSpace(title)) yield break;

				if (_config.Upgrade.Skins.Images.TryAdd(skin, url))
				{
					var mode = _config.Modes.FirstOrDefault(x =>
						x.Type == type && x.Skins.All(modeSkin => modeSkin.Skin != skin));
					if (mode != null)
					{
						mode.Skins.Add(new SkinConf
						{
							Enabled = false,
							LangKey = $"Skin{title}",
							Permission = string.Empty,
							Skin = skin
						});

						lang.RegisterMessages(new Dictionary<string, string>
						{
							[$"Skin{title}"] = title
						}, this);

						SaveConfig();
					}
				}

				ImageLibrary.Call("AddImage", url, url, 0UL);
			}
		}

		#endregion

		private void UpgradeAll(BasePlayer player, Types upgradeType)
		{
			if (!_config.Upgrade.HasAllPermission(player))
			{
				SendNotify(player, NoPermission, 1);
				return;
			}

			var cupboard = player.GetBuildingPrivilege();
			if (cupboard == null)
			{
				SendNotify(player, NoCupboard, 1);
				return;
			}

			if (CheckIsCupboardDefendableHomes(cupboard))
			{
				SendNotify(player, MsgBlockedUpgradeDefendableHomes, 1);
				return;
			}

			if (!player.CanBuild())
			{
				SendNotify(player, BuildingBlocked, 1);
				return;
			}

			if (_config.Block.UseNoEscape && NoEscape != null && NoEscape.IsLoaded && IsRaidBlocked(player))
			{
				SendNotify(player, UpgradeRaidBlocked, 1);
				return;
			}

			var data = PlayerData.GetOrCreate(player.UserIDString);

			var cooldown = _config.Upgrade.GetCooldown(player);
			if (cooldown > 0 && data.HasCooldown(upgradeType, cooldown))
			{
				SendNotify(player, UpgradeCanThrough, 1,
					data.LeftTime(upgradeType, cooldown));
				return;
			}

			var blockWipe = _config.Upgrade.GetWipeCooldown(player);
			if (blockWipe > 0 && PlayerData.HasWipeCooldown(blockWipe))
			{
				SendNotify(player, UpgradeCanThrough, 1,
					PlayerData.WipeLeftTime(blockWipe));
				return;
			}

			var grade = GetEnum(upgradeType);

			List<BuildingBlock> buildingBlocks;
#if TESTING
			using (new StopwatchWrapper("Count building blocks took {0}ms."))
#endif
			{
				var building = cupboard.GetBuilding();
				if (building == null)
					return;

				buildingBlocks = building
					.buildingBlocks
					.Where(x => x.grade <= grade &&
					            x.CanChangeToGrade(grade, data.GetSkin((int) grade), player))
					.ToList();
				if (buildingBlocks.Count == 0) return;
			}

			var skin = data.GetSkin((int) grade);

			if (!permission.UserHasPermission(player.UserIDString, PermFree))
			{
				if (!CanAffordUpgrade(buildingBlocks, grade, skin, player))
				{
					if (_config.Upgrade.NotifyRequiredResources)
					{
						var calc = CalcUpgrade(buildingBlocks, grade, skin);
						if (calc != null && calc.Count > 0)
						{
							var value = calc.First();

							SendNotify(player, NotEnoughResourcesWithAmounts, 1,
								Msg(player, ResourcesWithAmounts, value.Value,
									Msg(player,
										ItemManager.FindItemDefinition(value.Key)?.displayName.english)));
							return;
						}
					}

					SendNotify(player, NotEnoughResources, 1);
					return;
				}

				PayForUpgrade(buildingBlocks, grade, skin, player);
			}

			Global.Runner.StartCoroutine(StartUpgrade(player, buildingBlocks, grade));

			SendNotify(player, SuccessfullyUpgrade, 0);
		}

		private void RemoveAll(BasePlayer player)
		{
			if (!_config.Remove.HasAllPermission(player))
			{
				SendNotify(player, NoPermission, 1);
				return;
			}

			var cupboard = player.GetBuildingPrivilege();
			if (cupboard == null)
			{
				SendNotify(player, NoCupboard, 1);
				return;
			}

			if (CheckIsCupboardDefendableHomes(cupboard))
			{
				SendNotify(player, MsgBlockedRemoveDefendableHomes, 1);
				return;
			}

			var data = PlayerData.GetOrCreate(player.UserIDString);

			var cooldown = _config.Remove.GetCooldown(player);
			if (cooldown > 0 && data.HasCooldown(Types.Remove, cooldown))
			{
				SendNotify(player, RemoveCanThrough, 1,
					data.LeftTime(Types.Remove, cooldown));
				return;
			}

			var blockWipe = _config.Remove.GetWipeCooldown(player);
			if (blockWipe > 0 && PlayerData.HasWipeCooldown(blockWipe))
			{
				SendNotify(player, RemoveCanThrough, 1,
					PlayerData.WipeLeftTime(blockWipe));
				return;
			}

			var building = cupboard.GetBuilding();
			if (building == null)
				return;

			var entities =
				BaseNetworkable.serverEntities
					.OfType<BaseCombatEntity>()
					.Where(x => !(x is BasePlayer) && x.GetBuildingPrivilege() == cupboard)
					.ToList();
			if (entities.Count == 0 || entities.Any(x => !CanRemoveEntity(player, x)))
				return;

			Global.Runner.StartCoroutine(StartRemove(player, entities));

			SendNotify(player, SuccessfullyRemove, 0);
		}

		private void DowngradeAll(BasePlayer player, Types downgradeType)
		{
			if (!_config.Downgrade.HasAllPermission(player))
			{
				SendNotify(player, NoPermission, 1);
				return;
			}

			var cupboard = player.GetBuildingPrivilege();
			if (cupboard == null)
			{
				SendNotify(player, NoCupboard, 1);
				return;
			}

			if (CheckIsCupboardDefendableHomes(cupboard))
			{
				SendNotify(player, MsgBlockedDowngradeDefendableHomes, 1);
				return;
			}

			if (!player.CanBuild())
			{
				SendNotify(player, BuildingBlocked, 1);
				return;
			}

			if (_config.Block.UseNoEscape && IsRaidBlocked(player))
			{
				SendNotify(player, DowngradeRaidBlocked, 1);
				return;
			}

			var data = PlayerData.GetOrCreate(player.UserIDString);

			var cooldown = _config.Downgrade.GetCooldown(player);
			if (cooldown > 0 && data.HasCooldown(Types.Down, cooldown))
			{
				SendNotify(player, DowngradeCanThrough, 1,
					data.LeftTime(Types.Down, cooldown));
				return;
			}

			var blockWipe = _config.Downgrade.GetWipeCooldown(player);
			if (blockWipe > 0 && PlayerData.HasWipeCooldown(blockWipe))
			{
				SendNotify(player, DowngradeCanThrough, 1,
					PlayerData.WipeLeftTime(blockWipe));
				return;
			}

			var grade = GetEnum(downgradeType);
			var skin = data.GetSkin((int) grade);

			var building = cupboard.GetBuilding();
			if (building == null || !building.HasBuildingBlocks())
				return;

			var blocks = new List<BuildingBlock>();

			foreach (var block in building.buildingBlocks)
			{
				if (grade >= block.grade)
					continue;

				if (!CanDowngradeEntity(player, block, grade, skin))
				{
					blocks.Clear();
					return;
				}

				blocks.Add(block);
			}

			if (blocks.Count == 0)
			{
				blocks.Clear();

				SendNotify(player, CantDowngradeBuilding, 1);
				return;
			}

			Global.Runner.StartCoroutine(StartDowngrade(player, blocks, grade));

			SendNotify(player, SuccessfullyDowngrade, 0);
		}

		private void LoadCacheModes()
		{
			foreach (var mode in _config.Modes)
			{
				if (mode.Enabled == false)
					continue;

				_modeByType[(int) mode.Type] = mode;
			}
		}

		private Mode GetModeByType(Types type)
		{
			return GetModeByType((int) type);
		}

		private Mode GetModeByType(int type)
		{
			Mode mode;
			return _modeByType.TryGetValue(type, out mode) ? mode : null;
		}

		private void ClearData()
		{
			var toRemove = Facepunch.Pool.Get<List<ulong>>();

			try
			{
				foreach (var dataEntity in _entitiesData.Entities)
					if (!BaseNetworkable.serverEntities.Contains(new NetworkableId(dataEntity.Key)))
						toRemove.Add(dataEntity.Key);

				toRemove?.ForEach(key => _entitiesData?.Entities?.Remove(key));
			}
			catch
			{
				// ignore
			}

			Facepunch.Pool.FreeUnmanaged(ref toRemove);
		}

		private void LoadItemPrefabs()
		{
			foreach (var itemDefinition in ItemManager.GetItemDefinitions())
			{
				var entityPrefab = itemDefinition.GetComponent<ItemModDeployable>()?.entityPrefab?.resourcePath;
				if (string.IsNullOrEmpty(entityPrefab))
					continue;

				var shortPrefabName = Utility.GetFileNameWithoutExtension(entityPrefab);
				if (!string.IsNullOrEmpty(shortPrefabName))
					_shortPrefabNamesToItem.TryAdd(shortPrefabName, itemDefinition.shortname);
			}
		}

		private string FormatTime(BasePlayer player, float seconds)
		{
			var time = TimeSpan.FromSeconds(seconds);

			var result = string.Empty;

			if (time.Days != 0)
				result += $"{Format(time.Days, Msg(player, TimeDay), Msg(player, TimeDays))} ";

			if (time.Hours != 0)
				result += $"{Format(time.Hours, Msg(player, TimeHour), Msg(player, TimeHours))} ";

			if (time.Minutes != 0)
				result += $"{Format(time.Minutes, Msg(player, TimeMinute), Msg(player, TimeMinutes))} ";

			if (time.Seconds != 0)
				result += $"{Format(time.Seconds, Msg(player, TimeSecond), Msg(player, TimeSeconds))} ";

			return result;
		}

		private string Format(int units, string form1, string form2)
		{
			return units == 1 ? $"{units} {form1}" : $"{units} {form2}";
		}

		private BaseCombatEntity GetLookEntity(BasePlayer player)
		{
			RaycastHit hit;
			if (!Physics.Raycast(player.eyes.position, player.eyes.HeadForward(), out hit,
				    _config.Remove.Vision.Distance,
				    Layers.Construction,
				    QueryTriggerInteraction.Ignore)) return null;

			return hit.GetEntity() as BaseCombatEntity;
		}

		private bool ActiveItemIsHammerOrGunTools(BasePlayer player)
		{
			var item = player.GetActiveItem()?.info.shortname ?? "null";
			return item == HammerShortname || item == ToolGunShortname;
		}

		private bool ActiveItemIsBuildingPlan(BasePlayer player)
		{
			var item = player.GetActiveItem();
			return item != null && item.info.shortname == BuldingPlannerShortname;
		}

		private void RegisterPermissions()
		{
			var permissionsToRegister = new HashSet<string>
			{
				PERM_EDIT,
				PermFree
			};

			if (!string.IsNullOrEmpty(_config.Permission))
				permissionsToRegister.Add(_config.Permission);
			
			if (!string.IsNullOrEmpty(_config.Upgrade.PermissionToAll))
				permissionsToRegister.Add(_config.Upgrade.PermissionToAll);
			
			if (!string.IsNullOrEmpty(_config.Remove.PermissionToAll))
				permissionsToRegister.Add(_config.Remove.PermissionToAll);
			
			if (!string.IsNullOrEmpty(_config.Downgrade.PermissionToAll))
				permissionsToRegister.Add(_config.Downgrade.PermissionToAll);
			
			if (_config.Remove.Vision?.Enabled == true)
				if (!string.IsNullOrEmpty(_config.Remove.Vision.Permission)) 
					permissionsToRegister.Add(_config.Remove.Vision.Permission);

			_config.Modes.ForEach(mode =>
			{
				if (mode.Enabled == false) return;

				if (!string.IsNullOrEmpty(mode.Permission)) permissionsToRegister.Add(mode.Permission);
				
				if (_config.Upgrade.Skins.Enabled && mode.UseSkins)
					foreach (var skin in mode.Skins)
						if (skin.Enabled)
							if (!string.IsNullOrEmpty(skin.Permission)) permissionsToRegister.Add(skin.Permission);
			});
			
			foreach (var cdPermission in _config.Upgrade.VipCooldown.Keys) if (!string.IsNullOrEmpty(cdPermission)) permissionsToRegister.Add(cdPermission);
			
			foreach (var cdPermission in _config.Upgrade.VipAfterWipe.Keys) if (!string.IsNullOrEmpty(cdPermission)) permissionsToRegister.Add(cdPermission);
			
			foreach (var cdPermission in _config.Remove.VipCooldown.Keys) if (!string.IsNullOrEmpty(cdPermission)) permissionsToRegister.Add(cdPermission);
			
			foreach (var cdPermission in _config.Remove.VipAfterWipe.Keys) if (!string.IsNullOrEmpty(cdPermission)) permissionsToRegister.Add(cdPermission);
		
			foreach (var cdPermission in _config.Remove.ReturnPercents.Keys) if (!string.IsNullOrEmpty(cdPermission)) permissionsToRegister.Add(cdPermission);
			
			foreach (var cdPermission in _config.Remove.BlockCooldown.Permissions.Keys) if (!string.IsNullOrEmpty(cdPermission)) permissionsToRegister.Add(cdPermission);
			
			foreach (var cdPermission in _config.Downgrade.VipCooldown.Keys) if (!string.IsNullOrEmpty(cdPermission)) permissionsToRegister.Add(cdPermission);
			
			foreach (var cdPermission in _config.Downgrade.VipAfterWipe.Keys) if (!string.IsNullOrEmpty(cdPermission)) permissionsToRegister.Add(cdPermission);

			foreach (var perm in permissionsToRegister)
			{
				if (permission.PermissionExists(perm)) continue;
				
				permission.RegisterPermission(perm, this);
			}

#if TESTING
			var sb = new StringBuilder();
			sb.AppendLine("Registered permissions:");
			foreach (var perm in permissionsToRegister) sb.AppendLine(perm);
			Puts(sb.ToString());
#endif
		}

		private void RegisterPermission(string value)
		{
			if (!string.IsNullOrEmpty(value) && !permission.PermissionExists(value))
				permission.RegisterPermission(value, this);
		}

		private void RegisterCommands()
		{
			AddCovalenceCommand(_config.UpgradeCommands, nameof(CmdUpgrade));

			AddCovalenceCommand(_config.RemoveCommands, nameof(CmdRemove));

			AddCovalenceCommand(_config.DowngradeCommands, nameof(CmdDowngrade));

			AddCovalenceCommand(_config.SettingsCommands, nameof(CmdSettings));

			AddCovalenceCommand("buildtools.parse.skins", nameof(CmdParseBuildingSkins));
		}

		private void UnsubscribeEntities()
		{
			if (_config.ActiveItem.Enabled == false)
				Unsubscribe(nameof(OnActiveItemChanged));

			if (_config.Remove.Vision?.Enabled == false && _config.SwitchModesMiddleClick == false &&
			    _config.Binds.Enabled == false)
				Unsubscribe(nameof(OnPlayerInput));

			if (_config.Upgrade.UpdateSkinsOnRightClick == false)
				Unsubscribe(nameof(OnStructureUpgrade));

			if (_config.UseHammer == false)
				Unsubscribe(nameof(OnHammerHit));
		}

		private void LoadImages()
		{
			_needImageLibrary = true;

			if (ImageLibrary == null || !ImageLibrary.IsLoaded)
			{
				BroadcastILNotInstalled();
			}
			else
			{
				_enabledImageLibrary = true;

				var imagesList = new Dictionary<string, string>();

				_config.Modes.FindAll(mode => mode.Enabled && !mode.Icon.Contains("assets/icon")).ForEach(mode =>
				{
					if (!string.IsNullOrEmpty(mode.Icon))
						imagesList.TryAdd(mode.Icon, mode.Icon);
				});

				if (_config.Upgrade.Skins.Enabled)
				{
					if (!_config.Upgrade.Skins.Icon.Contains("assets/icon") &&
					    !string.IsNullOrEmpty(_config.Upgrade.Skins.Icon))
						imagesList.TryAdd(_config.Upgrade.Skins.Icon, _config.Upgrade.Skins.Icon);

					foreach (var image in _config.Upgrade.Skins.Images.Values)
						if (!string.IsNullOrEmpty(image))
							imagesList.TryAdd(image, image);
				}

				if (!string.IsNullOrEmpty(_config.UI.PanelBackground.Image) &&
				    !_config.UI.PanelBackground.Image.Contains("assets/icon"))
					imagesList.TryAdd(_config.UI.PanelBackground.Image, _config.UI.PanelBackground.Image);

				if (!string.IsNullOrEmpty(_config.UI.PanelCancelButton.Image) &&
				    !_config.UI.PanelCancelButton.Image.Contains("assets/icon"))
					imagesList.TryAdd(_config.UI.PanelCancelButton.Image, _config.UI.PanelCancelButton.Image);

				if (!string.IsNullOrEmpty(_config.UI.PanelSettingsBackground.Image) &&
				    !_config.UI.PanelSettingsBackground.Image.Contains("assets/icon"))
					imagesList.TryAdd(_config.UI.PanelSettingsBackground.Image,
						_config.UI.PanelSettingsBackground.Image);

				if (!string.IsNullOrEmpty(_config.UI.PanelSettingsIcon.Image) &&
				    !_config.UI.PanelSettingsIcon.Image.Contains("assets/icon"))
					imagesList.TryAdd(_config.UI.PanelSettingsIcon.Image, _config.UI.PanelSettingsIcon.Image);

				if (!string.IsNullOrEmpty(_config.UI.Skins.SelectColorButton.Image) &&
				    !_config.UI.Skins.SelectColorButton.Image.Contains("assets/icon"))
					imagesList.TryAdd(_config.UI.Skins.SelectColorButton.Image,
						_config.UI.Skins.SelectColorButton.Image);

				if (!string.IsNullOrEmpty(_config.UI.Skins.SettingsModalBackground.Image) &&
				    !_config.UI.Skins.SettingsModalBackground.Image.Contains("assets/icon"))
					imagesList.TryAdd(_config.UI.Skins.SettingsModalBackground.Image,
						_config.UI.Skins.SettingsModalBackground.Image);

				if (!string.IsNullOrEmpty(_config.UI.SelectItemImage))
					imagesList.TryAdd(_config.UI.SelectItemImage, _config.UI.SelectItemImage);

				ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
			}
		}

		private void BroadcastILNotInstalled()
		{
			for (var i = 0; i < 5; i++) PrintError("IMAGE LIBRARY IS NOT INSTALLED.");
		}

		private static List<Mode> GetPlayerModes(BasePlayer player)
		{
			return _config.Modes.FindAll(mode =>
				mode.Enabled &&
				(string.IsNullOrEmpty(mode.Permission) ||
				 _instance.permission.UserHasPermission(player.UserIDString, mode.Permission)));
		}

		private static List<Mode> GetPlayerModes(BasePlayer player, Types[] ignoredTypes)
		{
			return _config.Modes.FindAll(x => x.Enabled && !ignoredTypes.Contains(x.Type) &&
			                                  (string.IsNullOrEmpty(x.Permission) ||
			                                   _instance.permission.UserHasPermission(
				                                   player.UserIDString,
				                                   x.Permission)));
		}

		private bool CheckIsCupboardDefendableHomes(BuildingPrivlidge cupboard)
		{
			return Convert.ToBoolean(DefendableHomes?.Call("IsCupboardDefendableHomes", cupboard));
		}

		private bool IsRaidBlocked(BasePlayer player)
		{
			return Convert.ToBoolean(NoEscape?.Call("IsRaidBlocked", player));
		}

		private bool IsClanMember(ulong playerID, ulong targetID)
		{
			return Convert.ToBoolean(Clans?.Call("HasFriend", playerID, targetID));
		}

		private bool IsFriends(ulong playerID, ulong friendId)
		{
			return Convert.ToBoolean(Friends?.Call("AreFriends", playerID, friendId));
		}

		private static bool IsTeammates(ulong player, ulong friend)
		{
			return RelationshipManager.ServerInstance.FindPlayersTeam(player)?.members?.Contains(friend) == true;
		}

		private static BuildingGrade.Enum GetEnum(Types type)
		{
			switch (type)
			{
				case Types.Wood:
					return BuildingGrade.Enum.Wood;
				case Types.Stone:
					return BuildingGrade.Enum.Stone;
				case Types.Metal:
					return BuildingGrade.Enum.Metal;
				case Types.TopTier:
					return BuildingGrade.Enum.TopTier;
				default:
					return BuildingGrade.Enum.None;
			}
		}

		private static Types GetTypes(BuildingGrade.Enum type)
		{
			switch (type)
			{
				case BuildingGrade.Enum.Wood:
					return Types.Wood;
				case BuildingGrade.Enum.Stone:
					return Types.Stone;
				case BuildingGrade.Enum.Metal:
					return Types.Metal;
				case BuildingGrade.Enum.TopTier:
					return Types.TopTier;
				default:
					return Types.None;
			}
		}

		private static void RemoveEntity(BasePlayer player, BaseCombatEntity entity,
			IgnoredBlock ignoredBlock)
		{
			if (_config.UsePersonalVaultDoor && _instance._hasPersonalVaultDoor &&
			    _instance.IsPersonalVaultDoor(entity))
			{
				_instance.CheckHitPersonalVaultDoor(player, entity);
				return;
			}

			if (Interface.CallHook("CanBuildToolsGiveRefund", player, entity) == null)
			{
				if (ignoredBlock != null)
				{
					if (ignoredBlock.ReturnItem)
					{
						GiveRefund(entity, player, true, ignoredBlock.ReturnPercent);
					}
				}
				else
				{
					if (_config.Remove.ReturnItem)
						GiveRefund(entity, player);
				}
			}

			if (_config.Remove.RemoveItemsContainer)
			{
				DropContainer(entity.GetComponent<StorageContainer>());
				DropContainer(entity.GetComponent<ContainerIOEntity>());
			}

			entity.Kill();
		}

		private static void DropContainer(StorageContainer container)
		{
			if (container == null || container.inventory.itemList.Count < 1) return;

			ItemContainer.Drop("assets/prefabs/misc/item drop/item_drop.prefab", container.GetDropPosition(),
				container.Transform.rotation, container.inventory);
		}

		private static void DropContainer(ContainerIOEntity container)
		{
			if (container == null || container.inventory.itemList.Count < 1) return;

			ItemContainer.Drop("assets/prefabs/misc/item drop/item_drop.prefab", container.GetDropPosition(),
				container.Transform.rotation, container.inventory);
		}

		private static bool CanRemoveEntity(BasePlayer player, BaseEntity entity)
		{
			if (entity.OwnerID == 0)
			{
				_instance.SendNotify(player, CantRemove, 1);
				return false;
			}

			if (!_config.Remove.RemoveItemsContainer)
			{
				var storageContainer = entity.GetComponent<StorageContainer>();
				if (storageContainer != null && storageContainer.inventory.itemList.Count > 0)
				{
					_instance.SendNotify(player, CRStorageNotEmpty, 1);
					return false;
				}

				var containerIO = entity.GetComponent<ContainerIOEntity>();
				if (containerIO != null && containerIO.inventory.itemList.Count > 0)
				{
					_instance.SendNotify(player, CRStorageNotEmpty, 1);
					return false;
				}
			}

			var combat = entity.GetComponent<BaseCombatEntity>();
			if (combat != null && combat.SecondsSinceAttacked < 30f)
			{
				_instance.SendNotify(player, CRDamaged, 1);
				return false;
			}

			if (Interface.CallHook("canRemove", player, entity) != null)
			{
				_instance.SendNotify(player, CRBeBlocked, 1);
				return false;
			}

			if (_config.Block.NeedCupboard && entity.GetBuildingPrivilege() == null)
			{
				_instance.SendNotify(player, CRBuildingBlock, 1);
				return false;
			}

			if (_config.Block.UseNoEscape && _instance.NoEscape != null && _instance.NoEscape.IsLoaded &&
			    _instance.IsRaidBlocked(player))
			{
				_instance.SendNotify(player, RemoveRaidBlocked, 1);
				return false;
			}

			if (player.userID != entity.OwnerID)
			{
				if (_config.Remove.RemoveByCupboard)
					return true;

				if (_config.Remove.CanClan && _instance.IsClanMember(player.userID, entity.OwnerID)) return true;

				if (_config.Remove.CanFriends && _instance.IsFriends(player.userID, entity.OwnerID)) return true;

				if (_config.Remove.CanTeams && IsTeammates(player.userID, entity.OwnerID)) return true;

				_instance.SendNotify(player, CRNotAccess, 1);
				return false;
			}

			return true;
		}

		private static bool CanDowngradeEntity(BasePlayer player, BuildingBlock entity, BuildingGrade.Enum grade,
			ulong skin)
		{
			if (entity.OwnerID == 0)
			{
				_instance.SendNotify(player, CantDowngrade, 1);
				return false;
			}

			var combat = entity.GetComponent<BaseCombatEntity>();
			if (combat != null && combat.SecondsSinceAttacked < 30f)
			{
				_instance.SendNotify(player, DowngradeDamaged, 1);
				return false;
			}

			if (Interface.CallHook("canDowngrade", player, entity) != null)
			{
				_instance.SendNotify(player, DowngradeBeBlocked, 1);
				return false;
			}

			var currentGrade = entity.grade;
			if (currentGrade <= 0)
			{
				_instance.SendNotify(player, CantDowngrade, 1);
				return false;
			}

			var grades = entity.blockDefinition.grades;
			if (grades == null) return false;

			var targetGrade = currentGrade - 1;
			if (grades.Length <= (int) targetGrade ||
			    grades[(int) targetGrade] == null)
			{
				_instance.SendNotify(player, CantDowngrade, 1);
				return false;
			}

			if (player.userID != entity.OwnerID)
			{
				if (_config.Downgrade.CanClan && _instance.IsClanMember(player.userID, entity.OwnerID)) return true;

				if (_config.Downgrade.CanFriends && _instance.IsFriends(player.userID, entity.OwnerID)) return true;

				if (_config.Downgrade.CanTeams && IsTeammates(player.userID, entity.OwnerID)) return true;

				_instance.SendNotify(player, DowngradeNotAccess, 1);
				return false;
			}

			return true;
		}

		private static void GiveRefund(BaseCombatEntity entity, BasePlayer player, bool usePercent = false,
			float mainPercent = 100)
		{
			var shortPrefabName = entity.ShortPrefabName;

			if (!_instance._shortPrefabNamesToItem.TryGetValue(shortPrefabName, out shortPrefabName))
				shortPrefabName = Regex.Replace(entity.ShortPrefabName, "\\.deployed|_deployed", "");

			var item = ItemManager.CreateByName(shortPrefabName, 1, entity.skinID);
			if (item != null)
			{
				HandleCondition(ref item, player, entity);

				player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
				return;
			}

			entity.BuildCost()?.ForEach(value =>
			{
				var percent = usePercent ? mainPercent : _config.Remove.GetReturnPercent(player);

				var amount = Convert.ToInt32(percent < 100
					? value.amount * (percent / 100f)
					: value.amount);

				item = ItemManager.Create(value.itemDef, amount);
				if (item == null) return;

				HandleCondition(ref item, player, entity);

				player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
			});
		}

		private static void HandleCondition(ref Item item, BasePlayer player, BaseCombatEntity entity)
		{
			if (_config.Remove.Condition.Default)
			{
				if (entity.pickup.setConditionFromHealth && item.hasCondition)
					item.conditionNormalized =
						Mathf.Clamp01(entity.healthFraction - entity.pickup.subtractCondition);
				entity.OnPickedUpPreItemMove(item, player);
			}

			if (_config.Remove.Condition.Percent)
				item.LoseCondition(item.maxCondition * (_config.Remove.Condition.PercentValue / 100f));
		}

		private static void UpgradeBuildingBlock(BuildingBlock block, BuildingGrade.Enum grade, ulong skin)
		{
			if (block == null || block.IsDestroyed) return;

#if TESTING
			SayDebug($"[UpgradeBuildingBlock] with block={block}, grade={grade}, skin={skin}");
#endif

			block.ChangeGradeAndSkin(grade, skin);
		}

		private static void DowngradeBlock(BuildingBlock block, BuildingGrade.Enum grade, ulong skin)
		{
			if (block == null || block.IsDestroyed) return;

#if TESTING
			SayDebug($"[DowngradeBlock] with block={block}, grade={grade}, skin={skin}");
#endif

			block.ChangeGradeAndSkin(grade, skin);
		}

		private bool CanAffordUpgrade(List<BuildingBlock> blocks,
			BuildingGrade.Enum targetGrade,
			ulong targetSkin,
			BasePlayer player)
		{
			var dict = new Dictionary<int, int>(); // itemId - amount

			foreach (var itemAmount in blocks.SelectMany(block =>
				         block.blockDefinition.GetGrade(targetGrade, targetSkin).CostToBuild(block.grade)))
			{
				int amount;
				if (!dict.TryGetValue(itemAmount.itemid, out amount))
					amount = player.inventory.GetAmount(itemAmount.itemid);

				if (amount < itemAmount.amount)
					return false;

				dict[itemAmount.itemid] = amount - Mathf.RoundToInt(itemAmount.amount);
			}

			return true;
		}

		private Dictionary<int, int> CalcUpgrade(List<BuildingBlock> blocks,
			BuildingGrade.Enum targetGrade,
			ulong targetSkin)
		{
			var dict = new Dictionary<int, int>(); // itemId - amount

			foreach (var itemAmount in blocks.SelectMany(block =>
				         block.blockDefinition.GetGrade(targetGrade, targetSkin).CostToBuild(block.grade)).ToList())
			{
				int amount;
				dict.TryGetValue(itemAmount.itemid, out amount);

				dict[itemAmount.itemid] = amount + Convert.ToInt32(itemAmount.amount);
			}

			return dict;
		}

		private static void PayForUpgrade(List<BuildingBlock> blocks, BuildingGrade.Enum targetGrade, ulong targetSkin,
			BasePlayer player)
		{
			var collect = new List<Item>();

			blocks.ForEach(block => block.blockDefinition.GetGrade(targetGrade, targetSkin).CostToBuild(block.grade)
				.ForEach(
					itemAmount =>
					{
						player.inventory.Take(collect, itemAmount.itemid, (int) itemAmount.amount);
						player.Command("note.inv " + itemAmount.itemid + " " +
						               (float) ((int) itemAmount.amount * -1.0));
					}));

			foreach (var obj in collect)
				obj.Remove();
		}

		private IEnumerator StartUpgrade(BasePlayer player, List<BuildingBlock> blocks, BuildingGrade.Enum grade)
		{
#if TESTING
			using (new StopwatchWrapper("Start Upgrade took {0}ms."))
#endif
			{
				var data = PlayerData.GetOrLoad(player.UserIDString);

				for (var i = 0; i < blocks.Count; i++)
				{
					var block = blocks[i];
					if (block == null || block.IsDestroyed) continue;
#if TESTING
					using (new StopwatchWrapper("Upgrade block in StartUpgrade took {0}ms."))
#endif
					{
						var skin = data?.GetSkin((int) grade) ?? 0;

						UpgradeBuildingBlock(block, grade, skin);

						var color = data?.GetColor(skin);
						if (color > 0 && color != _useRandomColor)
							block.SetCustomColour(GetShippingContainerBlockColourForPlayer(player));
					}

					if (i % _config.Upgrade.AmountPerTick == 0)
						yield return CoroutineEx.waitForFixedUpdate;
				}
			}
		}

		private IEnumerator StartRemove(BasePlayer player, List<BaseCombatEntity> entities)
		{
			for (var i = 0; i < entities.Count; i++)
			{
				var entity = entities[i];
				if (entity == null || entity.IsDestroyed) continue;

				RemoveEntity(player, entity, _config.Remove.GetIgnoredBlock(entity));

				if (i % _config.Remove.AmountPerTick == 0)
					yield return CoroutineEx.waitForFixedUpdate;
			}
		}

		private IEnumerator StartDowngrade(BasePlayer player, List<BuildingBlock> blocks, BuildingGrade.Enum grade)
		{
#if TESTING
			using (new StopwatchWrapper("Start Downgrade took {0}ms."))
#endif
			{
				var data = PlayerData.GetOrLoad(player.UserIDString);

				for (var i = 0; i < blocks.Count; i++)
				{
					var block = blocks[i];
					if (block == null || block.IsDestroyed) continue;
#if TESTING
					using (new StopwatchWrapper("Upgrade block in StartDowngrade took {0}ms."))
#endif
					{
						var skin = data?.GetSkin((int) grade) ?? 0;

						DowngradeBlock(block, grade, skin);
					}

					if (i % _config.Downgrade.AmountPerTick == 0)
						yield return CoroutineEx.waitForFixedUpdate;
				}
			}
		}

		private static Types ParseType(string arg, out Types type)
		{
			Types upgradeType;
			if (Enum.TryParse(arg, true, out upgradeType))
			{
				type = upgradeType;
				return type;
			}

			int value;
			if (int.TryParse(arg, out value) && value > 0 && value < 6)
			{
				type = (Types) value;
				return type;
			}

			type = Types.None;
			return type;
		}

		#region PersonalVaultDoor

		private bool _hasPersonalVaultDoor;

		private void TryToLoadPersonalVaultDoor()
		{
			if (PersonalVaultDoor != null && PersonalVaultDoor.IsLoaded)
				_hasPersonalVaultDoor = true;
		}

		private bool IsPersonalVaultDoor(BaseEntity entity)
		{
			return PersonalVaultDoor?.Call<bool>("IsVaultDoor", entity.skinID) ?? false;
		}

		private void CheckHitPersonalVaultDoor(BasePlayer player, BaseEntity entity)
		{
			PersonalVaultDoor?.Call("CheckHit", player, entity);
		}

		#endregion

		#endregion

		#region Lang

		private const string
			MsgErrorSyntaxUpgradeAll = "MsgErrorSyntaxUpgradeAll",
			MsgErrorSyntaxDowngradeAll = "MsgErrorSyntaxDowngradeAll",
			MsgBlockedDowngradeDefendableHomes = "MsgBlockedDowngradeDefendableHomes",
			MsgBlockedRemoveDefendableHomes = "MsgBlockedRemoveDefendableHomes",
			MsgBlockedUpgradeDefendableHomes = "MsgBlockedUpgradeDefendableHomes",
			MsgButtonSelectColor = "MsgButtonSelectColor",
			MsgSelectedColor = "MsgSelectedColor",
			MsgUseRandom = "MsgUseRandom",
			NotFoundEntity = "NotFoundEntity",
			DowngradeTitle = "DowngradeTitle",
			CantDowngradeBuilding = "CantDowngradeBuilding",
			CantDowngrade = "CantDowngrade",
			DowngradeRaidBlocked = "DowngradeRaidBlocked",
			DowngradeNotAccess = "DowngradeNotAccess",
			DowngradeBuildingBlock = "DowngradeBuildingBlock",
			DowngradeBeBlocked = "DowngradeBeBlocked",
			DowngradeDamaged = "DowngradeDamaged",
			DowngradeCanThrough = "DowngradeCanThrough",
			SuccessfullyDowngrade = "SuccessfullyDowngrade",
			SkinChangerDefault = "SkinChangerDefault",
			SkinChangerSave = "SkinChangerSave",
			SkinChangerNoImageDescription = "SkinChangerNoImageDescription",
			SkinChangerTitle = "SkinChangerTitle",
			CloseButton = "CloseButton",
			ResourcesWithAmounts = "ResourcesWithAmounts",
			NotEnoughResourcesWithAmounts = "NotEnoughResourcesWithAmounts",
			NoILError = "NoILError",
			TimeSeconds = "TimeSeconds",
			TimeSecond = "TimeSecond",
			TimeMinutes = "TimeMinutes",
			TimeMinute = "TimeMinute",
			TimeHours = "TimeHours",
			TimeHour = "TimeHour",
			TimeDays = "TimeDays",
			TimeDay = "TimeDay",
			RemoveTimeLeft = "RemoveTimeLeft",
			CRNeedCupboard = "CRNeedCupboard",
			CRNotAccess = "CRNotAccess",
			CRBuildingBlock = "CRBuildingBlock",
			CRBeBlocked = "CRBeBlocked",
			CRStorageNotEmpty = "CRStorageNotEmpty",
			CRDamaged = "CRDamaged",
			SuccessfullyRemove = "SuccessfullyRemove",
			CloseMenu = "CloseMenu",
			UpgradeTitle = "UpgradeTitle",
			RemoveTitle = "RemoveTitle",
			UpgradeCanThrough = "UpgradeCanThrough",
			RemoveCanThrough = "RemoveCanThrough",
			NoPermission = "NoPermission",
			SuccessfullyUpgrade = "SuccessfullyUpgrade",
			NoCupboard = "NoCupboard",
			CupboardRequired = "CupboardRequired",
			RemoveRaidBlocked = "RemoveRaidBlocked",
			UpgradeRaidBlocked = "UpgradeRaidBlocked",
			BuildingBlocked = "BuildingBlocked",
			CantUpgrade = "CantUpgrade",
			CantRemove = "CantRemove",
			NotEnoughResources = "NotEnoughResources";

		protected override void LoadDefaultMessages()
		{
			var en = new Dictionary<string, string>
			{
				[CloseButton] = "✕",
				[NotEnoughResources] = "Not enough resources to upgrade!",
				[NotEnoughResourcesWithAmounts] =
					"Not enough resources to upgrade! It takes {0} to upgrade a building.",
				[ResourcesWithAmounts] = "{0} {1}",
				[CantRemove] = "You cannot remove this entity.",
				[CantUpgrade] = "You cannot upgrade this entity.",
				[BuildingBlocked] = "You are building blocked",
				[UpgradeRaidBlocked] = "You cannot upgrade buildings <color=#81B67A>during a raid!</color>!",
				[RemoveRaidBlocked] = "You cannot upgrade or remove <color=#81B67A>during a raid!</color>!",
				[CupboardRequired] = "A Cupboard is required!",
				[NoCupboard] = "No cupboard found!",
				[SuccessfullyUpgrade] = "You have successfully upgraded a building",
				[NoPermission] = "You do not have permission to use this mode!",
				[UpgradeCanThrough] = "You can upgrade the building in: {0}s",
				[RemoveCanThrough] = "You can remove the building in: {0}s",
				[RemoveTitle] = "Remove in <color=white>{0}s</color>",
				[UpgradeTitle] = "Upgrade to {0} <color=white>{1}s</color>",
				[CloseMenu] = "✕",
				[SuccessfullyRemove] = "You have successfully removed a building",
				[CRDamaged] = "Can't remove: Server has disabled damaged objects from being removed.",
				[CRStorageNotEmpty] = "Can't remove: The entity storage is not empty.",
				[CRBeBlocked] = "Can't remove: An external plugin blocked the usage.",
				[CRBuildingBlock] = "Can't remove: Missing cupboard",
				[CRNotAccess] = "Can't remove: You don't have any rights to remove this.",
				[RemoveTimeLeft] = "Can't remove: The entity was built more than {0} ago.",
				[TimeDay] = "day",
				[TimeDays] = "days",
				[TimeHour] = "hour",
				[TimeHours] = "hours",
				[TimeMinute] = "minute",
				[TimeMinutes] = "minutes",
				[TimeSecond] = "second",
				[TimeSeconds] = "seconds",
				[NoILError] = "The plugin does not work correctly, contact the administrator!",
				[SkinChangerTitle] = "Settings",
				[SkinChangerNoImageDescription] = "Default skin",
				[SkinChangerSave] = "Save",
				[SkinChangerDefault] = "Default",
				[SuccessfullyDowngrade] = "You have successfully downgraded a building",
				[DowngradeCanThrough] = "You can downgrade the building in: {0}s",
				[DowngradeDamaged] = "Can't downgrade: Server has disabled damaged objects from being removed.",
				[DowngradeBeBlocked] = "Can't downgrade: An external plugin blocked the usage.",
				[DowngradeBuildingBlock] = "Can't downgrade: Missing cupboard",
				[DowngradeNotAccess] = "Can't downgrade: You don't have any rights to remove this.",
				[DowngradeRaidBlocked] = "You cannot downgrade buildings <color=#81B67A>during a raid!</color>!",
				[CantDowngrade] = "You cannot downgrade this entity.",
				[CantDowngradeBuilding] = "You cannot downgrade this building.",
				[DowngradeTitle] = "Downgrade in <color=white>{0}s</color>",
				[NotFoundEntity] = "Entity not found!",
				[MsgUseRandom] = "Random colors",
				[MsgSelectedColor] = "✕",
				[MsgButtonSelectColor] = "Select color",
				[MsgBlockedUpgradeDefendableHomes] =
					"You cannot upgrade buildings while participating in the Defendable Homes event",
				[MsgBlockedRemoveDefendableHomes] =
					"You cannot remove buildings while participating in the Defendable Homes event",
				[MsgBlockedDowngradeDefendableHomes] =
					"You cannot downgrade buildings while participating in the Defendable Homes event",
				[MsgErrorSyntaxDowngradeAll] = "Error syntax! Use: /{0} {1} [wood/stone/metal/toptier]",
				[MsgErrorSyntaxUpgradeAll] = "Error syntax! Use: /{0} {1} [wood/stone/metal/toptier]",
				["Wood"] = "wood",
				["Stone"] = "stone",
				["Metal"] = "metal",
				["TopTier"] = "HQM",
				["SkinChanger_Wood"] = "Wood",
				["SkinChanger_Stone"] = "Stone",
				["SkinChanger_Metal"] = "Metal",
				["SkinChanger_TopTier"] = "HQM",
				["SkinAdobe"] = "Adobe",
				["SkinBrick"] = "Brick",
				["SkinContainer"] = "Container",
				["SkinBrutalist"] = "Brutalist",
				["SkinLegacy Wood"] = "Legacy Wood",
				["SkinGingerbread"] = "Gingerbread",
			};

			var ru = new Dictionary<string, string>
			{
				[CloseButton] = "✕",
				[NotEnoughResources] = "Недостаточно ресурсов для улучшения!",
				[NotEnoughResourcesWithAmounts] =
					"Недостаточно ресурсов для улучшения! Для улучшения здания требуется {0}.",
				[ResourcesWithAmounts] = "{0} {1}",
				[CantRemove] = "Вы не можете удалить это строение.",
				[CantUpgrade] = "Вы не можете улучшить это строение.",
				[BuildingBlocked] = "Вы находитесь в зоне блокировки строительства",
				[UpgradeRaidBlocked] = "Вы не можете улучшать строения <color=#81B67A>во время рейда!</color>!",
				[RemoveRaidBlocked] = "Вы не можете удалять строения <color=#81B67A>во время рейда!</color>!",
				[CupboardRequired] = "Требуется шкаф с инструментами!",
				[NoCupboard] = "Шкаф с инструментами не найден!",
				[SuccessfullyUpgrade] = "Вы успешно улучшили строение",
				[NoPermission] = "У вас недостаточно разрешений, чтобы использовать этот режим!",
				[UpgradeCanThrough] = "Вы сможете улучшить строение через: {0}с",
				[RemoveCanThrough] = "Вы сможете удалить строение через: {0}с",
				[RemoveTitle] = "Удаление <color=white>{0}с</color>",
				[UpgradeTitle] = "Улучшение в {0} <color=white>{1}с</color>",
				[CloseMenu] = "✕",
				[SuccessfullyRemove] = "Вы успешно удалили строение",
				[CRDamaged] = "Не удается удалить: сервер отключил удаление поврежденных объектов.",
				[CRStorageNotEmpty] = "Не удается удалить: хранилище строения не является пустым.",
				[CRBeBlocked] = "Не удается удалить: внешний плагин заблокировал использование.",
				[CRBuildingBlock] = "Не удается удалить: отсутствует шкаф с инструментами.",
				[CRNotAccess] = "Не удается удалить: у вас нет доступа для удаляния этого строения.",
				[RemoveTimeLeft] = "Не удается удалить: строение создано более {0} назад.",
				[TimeDay] = "день",
				[TimeDays] = "дней",
				[TimeHour] = "час",
				[TimeHours] = "часов",
				[TimeMinute] = "минута",
				[TimeMinutes] = "минут",
				[TimeSecond] = "секунда",
				[TimeSeconds] = "секунд",
				[NoILError] = "Плагин работает некорректно, свяжитесь с администратором!",
				[SkinChangerTitle] = "Настройки",
				[SkinChangerNoImageDescription] = "Стандартный скин",
				[SkinChangerSave] = "Сохранить",
				[SkinChangerDefault] = "Стандартный",
				[SuccessfullyDowngrade] = "Вы успешно понизили класс строения",
				[DowngradeCanThrough] = "Вы сможете понизить класс строения через: {0}с",
				[DowngradeDamaged] =
					"Не удается выполнить понижение класса строения: сервер отключил удаление поврежденных объектов.",
				[DowngradeBeBlocked] =
					"Не удается выполнить понижение класса строения: внешний плагин заблокировал использование.",
				[DowngradeBuildingBlock] =
					"Не удается выполнить понижение класса строения: отсутствует шкаф с инструментами.",
				[DowngradeNotAccess] =
					"Не удается выполнить понижение класса строения: у вас нет доступа для удаляния этого строения.",
				[DowngradeRaidBlocked] = "Вы не можете понижать класс строения <color=#81B67A>во время рейда!</color>!",
				[CantDowngrade] = "Вы не можете понижать класс этого строения.",
				[CantDowngradeBuilding] = "Вы не можете понижать класс этого строения.",
				[DowngradeTitle] = "Понижение <color=white>{0}s</color>",
				[NotFoundEntity] = "Строение не найдено!",
				[MsgUseRandom] = "Случайные цвета",
				[MsgSelectedColor] = "✕",
				[MsgButtonSelectColor] = "Выбрать цвет",
				[MsgBlockedUpgradeDefendableHomes] =
					"Вы не можете улучшать строения участвуя в ивенте Defendable Homes",
				[MsgBlockedRemoveDefendableHomes] = "Вы не можете удалять строения участвуя в ивенте Defendable Homes",
				[MsgBlockedDowngradeDefendableHomes] =
					"Вы не можете понижать уровень строений во время участия в ивенте Defendable Homes",
				[MsgErrorSyntaxDowngradeAll] = "Ошибка синтаксиса! Используйте: /{0} {1} [wood/stone/metal/toptier]",
				[MsgErrorSyntaxUpgradeAll] = "Ошибка синтаксиса! Используйте: /{0} {1} [wood/stone/metal/toptier]",
				["Wood"] = "дерево",
				["Stone"] = "камень",
				["Metal"] = "метал",
				["TopTier"] = "МВК",
				["SkinAdobe"] = "Adobe",
				["SkinBrick"] = "Brick",
				["SkinContainer"] = "Container",
				["SkinBrutalist"] = "Brutalist",
				["SkinLegacy Wood"] = "Legacy Wood",
				["SkinGingerbread"] = "Gingerbread",
			};

#if EDITING_MODE
			foreach (var msg in RegisterEditingMessages())
				en.TryAdd(msg.Key, msg.Value);

			foreach (var msg in RegisterEditingMessages("ru"))
				ru.TryAdd(msg.Key, msg.Value);
#endif

			lang.RegisterMessages(en, this);

			lang.RegisterMessages(ru, this, "ru");
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
				Interface.Oxide.CallHook("SendNotify", player, type, Msg(key, player.UserIDString, obj));
			else
				Reply(player, key, obj);
		}

		#endregion

		#region Data

		private Dictionary<string, PlayerData> _usersData = new Dictionary<string, PlayerData>();

		private class PlayerData
		{
			#region Fields

			[JsonProperty(PropertyName = "Last Upgrade")]
			public DateTime LastUpgrade = new DateTime(1970, 1, 1, 0, 0, 0);

			[JsonProperty(PropertyName = "Last Remove")]
			public DateTime LastRemove = new DateTime(1970, 1, 1, 0, 0, 0);

			[JsonProperty(PropertyName = "Last Downgrade")]
			public DateTime LastDowngrade = new DateTime(1970, 1, 1, 0, 0, 0);

			[JsonProperty(PropertyName = "Skins", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<int, ulong> Skins = new Dictionary<int, ulong>();

			[JsonProperty(PropertyName = "Colors", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<ulong, uint> Colors = new Dictionary<ulong, uint>();

			[JsonProperty(PropertyName = "Selected Modes", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, Types> SelectedModes = new Dictionary<string, Types>();

			#endregion

			#region Main

			#region Selected Modes

			public Types GetSelectedType(string item)
			{
				Types type;
				return SelectedModes.TryGetValue(item, out type) ? type : Types.None;
			}

			public Mode GetSelectedMode(string item)
			{
				var type = GetSelectedType(item);
				return type == Types.None ? null : _instance.GetModeByType(type);
			}

			public void SaveSelectedMode(string item, Types type)
			{
				if (type == Types.None || string.IsNullOrWhiteSpace(item)) return;

				SelectedModes[item] = type;
			}

			#endregion

			public ulong GetSkin(int type)
			{
				ulong skin;

				Skins.TryGetValue(type, out skin);

				return skin;
			}

			public void ChangeSkin(int type, ulong skin)
			{
				if (skin != 0)
				{
					Skins[type] = skin;
				}
				else
				{
					Skins.Remove(type);
				}
			}

			public void SetColor(ulong skin, uint colorID)
			{
				Colors[skin] = colorID;
			}

			public uint GetColor(ulong skin)
			{
				uint color;
				return Colors.TryGetValue(skin, out color) ? color : 0;
			}

			public int LeftTime(Types type, int cooldown)
			{
				var time = GetLastTime(type);

				return (int) time.AddSeconds(cooldown).Subtract(DateTime.UtcNow).TotalSeconds;
			}

			private DateTime GetLastTime(Types type)
			{
				DateTime time;
				switch (type)
				{
					case Types.Remove:
						time = LastRemove;
						break;
					case Types.Down:
						time = LastDowngrade;
						break;
					default:
						time = LastUpgrade;
						break;
				}

				return time;
			}

			public bool HasCooldown(Types type, int cooldown)
			{
				var time = GetLastTime(type);

				return DateTime.UtcNow.Subtract(time).TotalSeconds < cooldown;
			}

			public static bool HasWipeCooldown(int cooldown)
			{
				return DateTime.UtcNow.Subtract(SaveRestore.SaveCreatedTime.ToUniversalTime()).TotalSeconds < cooldown;
			}

			public static int WipeLeftTime(int cooldown)
			{
				return (int) SaveRestore.SaveCreatedTime.ToUniversalTime().AddSeconds(cooldown)
					.Subtract(DateTime.UtcNow)
					.TotalSeconds;
			}

			#endregion

			#region Utils

			public static string BaseFolder() =>
				"BuildTools" + Path.DirectorySeparatorChar + "Players" + Path.DirectorySeparatorChar;

			public static string[] GetFiles()
			{
				return GetFiles(BaseFolder());
			}

			public static void Save(string userId)
			{
				PlayerData data;
				if (!_instance._usersData.TryGetValue(userId, out data))
					return;

				Interface.Oxide.DataFileSystem.WriteObject(BaseFolder() + userId, data);
			}

			public static void Unload(string userId)
			{
				_instance?._usersData?.Remove(userId);
			}

			public static void SaveAndUnload(string userId)
			{
				Save(userId);

				Unload(userId);
			}

			public static PlayerData GetOrLoad(string userId)
			{
				return userId.IsSteamId() ? GetOrLoad(BaseFolder(), userId) : null;
			}

			public static PlayerData GetOrCreate(string userId)
			{
				if (!userId.IsSteamId()) return null;

				return GetOrLoad(userId) ?? (_instance._usersData[userId] = new PlayerData());
			}

			public static PlayerData GetOrLoad(string baseFolder, string userId, bool load = true)
			{
				PlayerData data;
				if (_instance._usersData.TryGetValue(userId, out data)) return data;

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

			#endregion
		}

		#region Convert

		private void ConvertOldData()
		{
			var data = LoadOldPlayerData();

			ConvertOldPlayerData(data);

			ClearDataCache();
		}

		private OldPluginData LoadOldPlayerData()
		{
			OldPluginData data = null;
			try
			{
				data = Interface.Oxide.DataFileSystem.ReadObject<OldPluginData>(Name);
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			return data;
		}

		private void ConvertOldPlayerData(OldPluginData data)
		{
			data.Players.ToList().ForEach(playerData =>
			{
				var newData = PlayerData.GetOrCreate(playerData.Key.ToString());

				newData.LastUpgrade = playerData.Value.LastUpgrade;
				newData.LastRemove = playerData.Value.LastRemove;
			});
		}

		#region Classes

		private class OldPluginData
		{
			[JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<ulong, OldPlayerData> Players = new Dictionary<ulong, OldPlayerData>();
		}

		private class OldPlayerData
		{
			[JsonProperty(PropertyName = "Last Upgrade")]
			public DateTime LastUpgrade = new DateTime(1970, 1, 1, 0, 0, 0);

			[JsonProperty(PropertyName = "Last Remove")]
			public DateTime LastRemove = new DateTime(1970, 1, 1, 0, 0, 0);

			public int LeftTime(bool remove, int cooldown)
			{
				var time = remove
					? LastRemove
					: LastUpgrade;
				return (int) time.AddSeconds(cooldown).Subtract(DateTime.UtcNow).TotalSeconds;
			}

			public bool HasCooldown(bool remove, int cooldown)
			{
				var time = remove
					? LastRemove
					: LastUpgrade;

				return DateTime.UtcNow.Subtract(time).TotalSeconds < cooldown;
			}

			public static bool HasWipeCooldown(int cooldown)
			{
				return DateTime.UtcNow.Subtract(SaveRestore.SaveCreatedTime.ToUniversalTime()).TotalSeconds < cooldown;
			}

			public static int WipeLeftTime(int cooldown)
			{
				return (int) SaveRestore.SaveCreatedTime.ToUniversalTime().AddSeconds(cooldown)
					.Subtract(DateTime.UtcNow)
					.TotalSeconds;
			}
		}

		#endregion

		#region Utils

		private void ClearDataCache()
		{
			var players = BasePlayer.activePlayerList.Select(x => x.UserIDString).ToList();

			_usersData.Where(x => !players.Contains(x.Key))
				.ToList()
				.ForEach(data => { PlayerData.SaveAndUnload(data.Key); });
		}

		#endregion

		#endregion

		#endregion

		#region Editing

#if EDITING_MODE

		private const string
			ModalLayer = "UI.BuildTools.Modal",
			ModalMainLayer = "UI.BuildTools.Modal.Main",
			SecondModalLayer = "UI.BuildTools.Second.Modal",
			SecondModalMainLayer = "UI.BuildTools.Second.Modal.Main",
			SelectItemModalLayer = "UI.BuildTools.Select.Item.Modal";

		#region Interface

		private void EditConfigUI(BasePlayer player, int page = 0)
		{
			var editData = GetConfigEditing(player);
			if (editData == null) return;

			var constSwitch = 20f;
			var fieldsOnLine = 4;
			var maxFields = 12;
			var totalWidth = (fieldsOnLine * UI_PROPERTY_WIDTH + (fieldsOnLine - 1) * UI_PROPERTY_MARGIN_X) +
			                 constSwitch * 2;

			var propterties = editData.GetProperties().GetProperties();

			var container = new CuiElementContainer();

			#region Background

			container.Add(new CuiPanel()
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

			container.Add(new CuiPanel()
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

			container.Add(new CuiButton()
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

				container.Add(new CuiButton()
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
							: string.Empty,
					}
				}, ModalMainLayer, ModalMainLayer + ".BTN.Edit.Next");

				container.Add(new CuiButton()
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
							: string.Empty,
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
				{
					container.Add(new CuiLabel()
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
				}

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
					{
						if (first)
						{
							ySwitch -= UI_PROPERTY_TITLE_HEIGHT;

							first = false;
						}
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

						container.Add(new CuiLabel()
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
							if (i != g.Value.Count - 1)
							{
								ySwitch = ySwitch - UI_PROPERTY_HEIGHT - UI_PROPERTY_MARGIN_Y;
							}

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
				container.Add(new CuiPanel()
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

				container.Add(new CuiPanel()
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

				container.Add(new CuiLabel()
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

				container.Add(new CuiButton()
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
				container.Add(new CuiButton()
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

			container.Add(new CuiButton()
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

			JProperty target;
			JArray array;
			JProperty[] propterties;
			bool isSystemTypes;
			if (!TryGetArrayProperties(editData, selectedTab, out target, out array, out propterties,
				    out isSystemTypes))
				return;

			var container = new CuiElementContainer();

			#endregion

			#region Background

			var bgLayer = container.Add(new CuiPanel()
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

			var mainLayer = container.Add(new CuiPanel()
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

			container.Add(new CuiLabel()
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

			container.Add(new CuiButton()
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
			{
				foreach (var property in propterties)
				{
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
				}
			}

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

				container.Add(new CuiButton()
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

				container.Add(new CuiButton()
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

				container.Add(new CuiPanel()
				{
					RectTransform =
					{
						AnchorMin = "0 0",
						AnchorMax = "0 1",
						OffsetMin = "22.5 60",
						OffsetMax = "32.5 -60",
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
				container.Add(new CuiButton()
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
			{
				container.Add(new CuiButton()
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
			}

			#endregion

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private void ShowImageModalUI(BasePlayer player, EditData editData, string targetProperty,
			Action<string> onFinish = null)
		{
			if (editData == null) return;

			var container = new CuiElementContainer();

			container.Add(new CuiPanel()
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

			container.Add(new CuiButton()
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
							if (_enabledImageLibrary && Convert.ToBoolean(ImageLibrary.Call("HasImage", val)))
							{
								container.Add(new CuiElement()
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
			{
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
			}

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

			container.Add(new CuiPanel()
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
				container.Add(new CuiButton()
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

			container.Add(new CuiButton()
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

				container.Add(new CuiPanel()
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

				container.Add(new CuiLabel()
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

				container.Add(new CuiButton()
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

			IColor selectedColor;
			if (GetPropertyByPath(editData.GetSelectProperty(), editData.Object).Value
				    ?.TryParseObject(out selectedColor) != true)
				return;

			var container = new CuiElementContainer();

			#region Background

			var bgLayer = container.Add(new CuiPanel()
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

			var mainLayer = container.Add(new CuiPanel()
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

			container.Add(new CuiLabel()
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

			container.Add(new CuiButton()
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
			var total = (scale * 2) - 8f;

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
								$"{CMD_Main_Console} {editData.MainCommand} color set hex {ColorUtility.ToHtmlStringRGB(targetColor)}",
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
						new CuiOutlineComponent()
						{
							Color = HexToCuiColor("#575757"),
							Distance = "3 -3",
							UseGraphicAlpha = true
						}
					}
				});

				container.Add(new CuiLabel()
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
						new CuiRectTransformComponent()
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
						new CuiRectTransformComponent()
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
						new CuiImageComponent()
						{
							Color = HexToCuiColor("#000000", 95)
						},
						new CuiRectTransformComponent()
						{
							AnchorMin = "0 1", AnchorMax = "0 1",
							OffsetMin =
								$"{-10f + constSwitch} {(ySwitch - ((fieldTitleHeight + fieldHeight) * nestedLines + (nestedLines - 1) * fieldMarginY)) - 10f}",
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
					new CuiRectTransformComponent()
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
									$"{CMD_Main_Console} {commandMain} {commandToEditProperty} {property.Path.ReplaceSpaceToUnicode()} {!val}",
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
					var val = property.Value.ToString();

					container.Add(new CuiElement()
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
									new CuiRawImageComponent()
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

							container.Add(new CuiButton()
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
							{
								container.Add(new CuiButton()
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
					new CuiRectTransformComponent()
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
									$"{CMD_Main_Console} {commandMain} {commandToEditProperty} {property.Path.ReplaceSpaceToUnicode()} {!val}",
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
					IColor color;
					if (property.Value?.TryParseObject(out color) == true && color != null)
					{
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
					}

					break;
				}

				default:
				{
					var val = property.Value.ToString();

					container.Add(new CuiElement()
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
									new CuiRawImageComponent()
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

							container.Add(new CuiButton()
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
							{
								container.Add(new CuiButton()
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
					Color = "1 1 1 1",
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
				}
			}, parent);

			#endregion

			#region Right

			container.Add(new CuiButton()
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
				}
			}, parent);

			#endregion
		}

		#endregion

		#endregion Interface

		#region Helpers

		#region Items

		private ListDictionary<string, ListHashSet<string>> _itemsCategories =
			new ListDictionary<string, ListHashSet<string>>();

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

		private readonly Dictionary<string, string> _itemsTitles = new Dictionary<string, string>();

		private string GetItemName(BasePlayer player, string shortName)
		{
			string result;
			if (!_itemsTitles.TryGetValue(shortName, out result))
				_itemsTitles.Add(shortName, result = ItemManager.FindItemDefinition(shortName)?.displayName.english);

			if (_config.UseLangAPI && LangAPI != null && LangAPI.IsLoaded &&
			    LangAPI.Call<bool>("IsDefaultDisplayName", result))
				return LangAPI.Call<string>("GetItemDisplayName", shortName, result, player.UserIDString) ?? result;

			return result;
		}

		#endregion

		#region Data

		private EditData GetEditData(BasePlayer player, string mainCommand)
		{
			switch (mainCommand)
			{
				case "edit_config":
					return GetConfigEditing(player);

				default:
					throw new NotImplementedException("Not implemented edit data!");
			}
		}

		private void RemoveEditing(BasePlayer player)
		{
			RemoveConfigEditing(player);
		}

		private List<JTokenType> _editableTokenTypes = new List<JTokenType>
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

			private List<LastHook> _lastHooks = new List<LastHook>();

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
				{
					lastHook = _lastHooks.FindAll(x =>
						(!hasIgnoredHook || x.Hook != ignoredHook) &&
						(!hasIgnoredProperty || x.TargetProperty != ignoredProperty)).LastOrDefault();
				}
				else
				{
					lastHook = _lastHooks.LastOrDefault();
				}

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

		#region Config

		private Dictionary<ulong, EditConfigData> _editConfig = new Dictionary<ulong, EditConfigData>();

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
			EditConfigData editConfig;
			if (_editConfig.TryGetValue(player.userID, out editConfig))
				return editConfig;

			editConfig = new EditConfigData
			{
				Type = _config.GetType(),
				Object = JObject.FromObject(_config),
				IsGenerated = false,
				MainCommand = mainCMD,
				IgnoredProperties = new[]
				{
					(LangRu ? "Ежедневные награды" : "Daily awards"),
					(LangRu ? "Задержки (разрешение – задержка)" : "Cooldowns (permission – cooldown)")
				}
			};

			return _editConfig[player.userID] = editConfig;
		}

		private EditConfigData GetConfigEditing(BasePlayer player)
		{
			EditConfigData dayData;
			return _editConfig.TryGetValue(player.userID, out dayData) ? dayData : null;
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

		private Dictionary<ulong, Timer> _destroyTimes = new Dictionary<ulong, Timer>();

		private void DestroyEditingTimer(ulong player)
		{
			Timer destroyTimer;
			if (_destroyTimes.TryGetValue(player, out destroyTimer)) destroyTimer?.Destroy();
		}

		private void AddEditingTimer(ulong player, float delay, Action callback)
		{
			_destroyTimes[player] = timer.In(delay, callback);
		}

		#endregion

		#region Utils

		private static uint GetShippingContainerBlockColourForPlayer(BasePlayer player)
		{
			if (player == null) return 0;
			
			var infoInt = player.GetInfoInt("client.SelectedShippingContainerBlockColour", 0);
			return infoInt >= 0 ? (uint) infoInt : 0U;
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
							if (!args.HasLength(4)) return;

							var targetProperty = args[2];
							if (string.IsNullOrEmpty(targetProperty)) return;

							var target = GetPropertyByPath(targetProperty.ReplaceUnicodeToSpace(), editData.Object);
							if (target == null) return;

							var newValue = string.Join(" ", args.Skip(3));
							if (string.IsNullOrEmpty(newValue)) return;

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
									int selectedTab;
									if (!args.HasLength(4) || !int.TryParse(args[3], out selectedTab)) return;

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
									int selectedTab, page;
									if (!args.HasLength(6) || !int.TryParse(args[4], out selectedTab)
									                       || !int.TryParse(args[5], out page)) return;

									var targetProperty = args[3];
									if (string.IsNullOrEmpty(targetProperty)) return;

									var target = GetPropertyByPath(targetProperty.ReplaceUnicodeToSpace(),
										editData.Object);
									if (target == null) return;

									var newValue = string.Join(" ", args.Skip(6));
									if (string.IsNullOrEmpty(newValue)) return;

									var arr = target.Value as JArray;
									if (arr != null)
										arr[selectedTab] = JToken.FromObject(newValue);

									editData.SetLastHook(nameof(EditArrayUI), "edit_data", selectedTab, page);

									EditArrayUI(player, editData, selectedTab, page);
									break;
								}

								case "change":
								{
									int selectedTab, page;
									if (!args.HasLength(7) || !int.TryParse(args[3], out selectedTab)
									                       || !int.TryParse(args[4], out page)) return;

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
									int selectedTab, page;
									if (!args.HasLength(5) || !int.TryParse(args[3], out selectedTab)
									                       || !int.TryParse(args[4], out page)) return;

									var target = GetPropertyByPath(editData.GetTargetProperty(), editData.Object);

									var array = target?.Value as JArray;
									if (array == null) return;

									object newObject = null;
									if (_editableTokenTypes.Contains(array.First.Type))
									{
										var systemType = GetTypeFromJToken(array.First.Type);
										if (systemType != null)
										{
											newObject = CreateObjectByType<object>(systemType);
										}
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
									int selectedTab, page;
									if (!args.HasLength(5) || !int.TryParse(args[3], out selectedTab)
									                       || !int.TryParse(args[4], out page)) return;

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
									int selectedCategory;
									if (!args.HasLength(4) || !int.TryParse(args[3], out selectedCategory)) return;

									SelectItemUI(player, editData, selectedCategory);
									break;
								}

								case "select":
								{
									int itemID;
									if (!args.HasLength(4) || !int.TryParse(args[3], out itemID)) return;

									var definition = ItemManager.FindItemDefinition(itemID);
									if (definition == null) return;

									var targetProperty = GetPropertyByPath(
										editData.GetSelectProperty().ReplaceUnicodeToSpace(),
										editData.Object);
									if (targetProperty != null)
									{
										targetProperty.Value = JToken.FromObject(definition.shortname);

										foreach (var property in targetProperty.Parent.Children<JProperty>())
										{
											if (property.Name.Contains("Skin"))
											{
												property.Value = (ulong) 0;
											}
										}

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

											IColor color;
											if (targetProperty.Value?.TryParseObject(out color) != true) return;

											color.Hex = $"#{str}";

											color.UpdateColor();

											targetProperty.Value = JToken.FromObject(color);

											editData.UpdateObject();

											SelectColorUI(player, editData);
											break;
										}

										case "opacity":
										{
											float opacity;
											if (!args.HasLength(5) || !float.TryParse(args[4], out opacity)) return;

											if (opacity < 0 || opacity > 100)
												return;

											opacity = (float) Math.Round(opacity, 2);

											var targetProperty = GetPropertyByPath(
												editData.GetSelectProperty().ReplaceUnicodeToSpace(),
												editData.Object);
											if (targetProperty == null)
												return;

											IColor color;
											if (targetProperty.Value?.TryParseObject(out color) != true) return;

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

			var targetObj = array[selectedTab] as JObject;
			if (targetObj != null)
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
					var arr = JToken.FromObject(newValue) as JArray;
					if (arr != null)
						targetVal = arr;
					break;
				}
				case JTokenType.Integer:
				{
					long ulongVal;
					if (long.TryParse(newValue, out ulongVal))
						targetVal = ulongVal;
					else
					{
						int val;
						if (int.TryParse(newValue, out val))
							targetVal = val;
					}

					break;
				}
				case JTokenType.Float:
				{
					float val;
					if (float.TryParse(newValue, out val))
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
					bool val;
					if (bool.TryParse(newValue, out val))
						targetVal = val;
					break;
				}
				case JTokenType.Date:
				{
					DateTime val;
					if (DateTime.TryParse(newValue, out val))
						targetVal = val;
					break;
				}
				case JTokenType.Bytes:
				{
					byte val;
					if (byte.TryParse(newValue, out val))
						targetVal = val;
					break;
				}
				case JTokenType.Guid:
				{
					Guid val;
					if (Guid.TryParse(newValue, out val))
						targetVal = val;
					break;
				}
				case JTokenType.TimeSpan:
				{
					TimeSpan val;
					if (TimeSpan.TryParse(newValue, out val))
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
			if (type == typeof(string))
			{
				return string.Empty;
			}

			return (T) Activator.CreateInstance(type);
		}

		private static ulong TryGetSkinFromProperty(string targetProperty, JProperty property, JObject obj)
		{
			var skin = 0UL;

			JProperty skinProperty = null;
			var splitted = targetProperty.Split('.');
			if (splitted.Length > 0)
			{
				var parentObj = property.Parent as JObject;
				if (parentObj != null)
					skinProperty = parentObj.Properties()?.FirstOrDefault(x => x.Name.Contains("Skin"));
			}
			else
			{
				skinProperty = obj.Properties().FirstOrDefault(x => x.Name.Contains("Skin"));
			}

			if (skinProperty != null)
			{
				skin = (ulong) skinProperty.Value;
			}

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
						[EditingBtnAdd] = "✚",
					};
				}

				case "zh-CN":
				{
					return new Dictionary<string, string>
					{
						[EditingCfgTitle] = "编辑配置",
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
						[EditingBtnAdd] = "✚",
					};
				}

				default:
				{
					return new Dictionary<string, string>
					{
						[EditingCfgTitle] = "EDIT CONFIGURATION",
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
						[EditingBtnAdd] = "✚",
					};
				}
			}
		}

		#endregion Editing.Lang

#endif

		#endregion Editing

		#region Testing functions

#if TESTING
		private static void SayDebug(string message)
		{
			Debug.Log($"[BuildTools.Debug] {message}");
		}

		private void DebugMessage(string format, long time)
		{
			PrintWarning(format, time);
		}

		private class StopwatchWrapper : IDisposable
		{
			public StopwatchWrapper(string format)
			{
				Sw = Stopwatch.StartNew();
				Format = format;
			}

			public static Action<string, long> OnComplete { private get; set; }

			private string Format { get; }
			private Stopwatch Sw { get; }

			public long Time { get; private set; }

			public void Dispose()
			{
				Sw.Stop();
				Time = Sw.ElapsedMilliseconds;
				OnComplete(Format, Time);
			}
		}

#endif

		#endregion
	}
}

#region Extension Methods

namespace Oxide.Plugins.BuildToolsExtensionMethods
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
				{
					if (d.Key > c)
						c = d.Key;
				}

			return c;
		}

		public static int MinKeyBy<T>(this List<KeyValuePair<int, T>> list, Func<int, bool> b)
		{
			int? c = null;

			for (var i = 0; i < list.Count; i++)
			{
				var obj = list[i];

				if (b(obj.Key))
				{
					if (c == null || obj.Key < c)
						c = obj.Key;
				}
			}

			return c ?? 0;
		}

		public static int MinKey<T>(this List<KeyValuePair<int, T>> list)
		{
			int? c = null;

			for (var i = 0; i < list.Count; i++)
			{
				var obj = list[i];

				if (c == null || obj.Key < c)
					c = obj.Key;
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

				var targetComponent = component as T;
				if (targetComponent != null)
				{
					return i;
				}
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
				result = default(T);
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

			using (var reader = new JTokenReader(token))
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
			{
				var nestedObject = property.Value as JObject;
				if (nestedObject != null)
				{
					BuildTools.IColor color;
					if (property.Value?.TryParseObject(out color) == true)
					{
						list.Add(property);
						return list;
					}

					Dictionary<string, float> dict;
					if (property.Value?.TryParseObject(out dict) == true)
					{
						// ignored
						return list;
					}

					foreach (var nestedProperty in nestedObject.Properties())
						list.AddRange(GetProperties(nestedProperty));

					return list;
				}
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