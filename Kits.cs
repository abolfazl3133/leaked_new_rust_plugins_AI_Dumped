// #define TESTING

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;	
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Plugins.KitsExtensionMethods;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

#if CARBON
using Carbon.Base;
using Carbon.Modules;
#endif

namespace Oxide.Plugins
{
	[Info("Kits", "Mevent", "2.1.9")]
	public class Kits : RustPlugin
	{
		#region Fields

		[PluginReference] 
		private Plugin
			ImageLibrary = null,
			ServerPanel = null,
			CopyPaste = null,
			Notify = null,
			UINotify = null,
			NoEscape = null;

		private static Kits _instance;

		private const string
			PERM_ADMIN = "Kits.admin",
			
			Layer = "UI.Kits",
			InfoLayer = "UI.Kits.Info",
			EditingLayer = "UI.Kits.Editing",
			ModalLayer = "UI.Kits.Modal",
			CategoryAll = "all";

		private bool _enabledImageLibrary;

#if CARBON
        private ImageDatabaseModule imageDatabase;
#endif
		
		private const bool LangRu = false;
		
		private readonly Dictionary<ulong, Dictionary<string, object>> _kitEditing = new();

		private readonly Dictionary<ulong, Dictionary<string, object>> _itemEditing = new();

		private readonly Dictionary<string, List<(int itemID, string shortName)>> _itemsCategories = new();

		private (bool spStatus, int categoryID) _serverPanelCategory = (false, -1); // key - use serverPanel, value - category id
        
		private int _lastKitID;

		#endregion

		#region Config

		private Configuration _config;

		private class Configuration
		{
			[JsonProperty(PropertyName = LangRu ? "Автовайп" : "Automatic wipe on wipe")]
			public bool AutoWipe = false;

			[JsonProperty(PropertyName =
				LangRu
					? "Сохранять выданные наборы (с помощью команды kits.givekit) при вайпе?"
					: "Save given kits (via the kits.givekit command) on wipe?")]
			public bool SaveGivenKitsOnWipe = false;

			[JsonProperty(PropertyName = LangRu ? "Стандартный Цвет Набора" : "Default Kit Color")]
			public string KitColor = "#A0A935";

			[JsonProperty(PropertyName = LangRu ? "Работать с Notify?" : "Work with Notify?")]
			public bool UseNotify = true;

			[JsonProperty(PropertyName =
				LangRu ? "Работать с NoEscape? (Raid/Combat block)" : "Use NoEscape? (Raid/Combat block)")]
			public bool UseNoEscape = false;

			[JsonProperty(PropertyName = LangRu ? "Использовать блокировку рейда?" : "Use Raid Blocked?")]
			public bool UseRaidBlock = true;

			[JsonProperty(PropertyName = LangRu ? "Использовать блокировку комбата?" : "Use Combat Blocked?")]
			public bool UseCombatBlock = true;

			[JsonProperty(PropertyName =
				LangRu ? "Могут ли администраторы редактировать предметы? (по флаг)" : "Can admins edit? (by flag)")]
			public bool FlagAdmin = true;

			[JsonProperty(PropertyName = LangRu ? "Whitelist для NoEscape" : "Whitelist for NoEscape",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> NoEscapeWhiteList = new()
			{
				"kit name 1",
				"kit name 2"
			};

			[JsonProperty(PropertyName = LangRu ? "Команды" : "Commands",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] Commands = {"kit", "kits"};

			[JsonProperty(PropertyName = "Economy")]
			public EconomyConf Economy = new()
			{
				Type = EconomyConf.EconomyType.Plugin,
				AddHook = "Deposit",
				BalanceHook = "Balance",
				RemoveHook = "Withdraw",
				Plug = "Economics",
				ShortName = "scrap",
				DisplayName = string.Empty,
				Skin = 0
			};

			[JsonProperty(PropertyName = LangRu ? "Настройки Редкости" : "Rarity Settings",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<RarityColor> RarityColors = new()
			{
				new RarityColor(40, "#A0A935")
			};

			[JsonProperty(PropertyName = LangRu ? "Авто наборы" : "Auto Kits",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> AutoKits = new()
			{
				"autokit", "autokit_vip", "autokit_premium"
			};

			[JsonProperty(PropertyName = LangRu ? "Получение автонабора 1 раз?" : "Getting an auto kit 1 time?")]
			public bool OnceAutoKit = false;

			[JsonProperty(PropertyName =
				LangRu ? "Разрешить включать/выключать автокиты?" : "Allow to enable/disable autokit?")]
			public bool UseChangeAutoKit = false;

			[JsonProperty(PropertyName =
				LangRu ? "Разрешение для включения/выключения автокитов" : "Permission to enable/disable autokit")]
			public string ChangeAutoKitPermission = "kits.changeautokit";

			[JsonProperty(PropertyName = LangRu ? "Игнорировать проверку автокита?" : "Ignore auto-kit checking?")]
			public bool IgnoreAutoKitChecking = true;

			[JsonProperty(PropertyName =
				LangRu
					? "Обновлять меню наборов во время операций с разрешениями?"
					: "Update the kits menu during permissions operations?")]
			public bool OnPermissionsUpdate = false;

			[JsonProperty(PropertyName = "Logs")] public LogInfo Logs = new()
			{
				Console = true,
				File = true
			};

			[JsonProperty(PropertyName =
				LangRu ? "Показывать оповещение об отсутвии прав?" : "Show No Permission Description?")]
			public bool ShowNoPermDescription = true;

			[JsonProperty(PropertyName = LangRu ? "Показывать все наборы?" : "Show All Kits?")]
			public bool ShowAllKits = false;

			[JsonProperty(PropertyName =
				LangRu
					? "Показывать набор, когда закончилось количество использований?"
					: "Show the kit when the number of uses is up?")]
			public bool ShowUsesEnd = false;

			[JsonProperty(PropertyName = "CopyPaste Parameters",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> CopyPasteParameters = new()
			{
				"deployables", "true", "inventories", "true"
			};

			[JsonProperty(PropertyName = LangRu ? "Блокировка в Building Block?" : "Block in Building Block?")]
			public bool BlockBuilding = false;

			[JsonProperty(PropertyName = LangRu ? "NPC Наборы" : "NPC Kits",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, NpcKitsData> NpcKits = new()
			{
				["1234567"] = new NpcKitsData
				{
					Description = "Free Kits",
					Kits = new List<string>
					{
						"kit_one",
						"kit_two"
					}
				},
				["7654321"] = new NpcKitsData
				{
					Description = "VIPs Kits",
					Kits = new List<string>
					{
						"kit_three",
						"kit_four"
					}
				}
			};

			[JsonProperty(PropertyName = LangRu ? "Описание" : "Description")]
			public MenuDescription Description = new()
			{
				AnchorMin = "0 0", AnchorMax = "1 0",
				OffsetMin = "0 -55", OffsetMax = "0 -5",
				Enabled = true,
				Color = new IColor("#0E0E10", 100),
				FontSize = 18,
				Font = "robotocondensed-bold.ttf",
				Align = TextAnchor.MiddleCenter,
				TextColor = new IColor("#FFFFFF", 100),
				Description = string.Empty
			};

			[JsonProperty(PropertyName = LangRu ? "Информация о наборе" : "Info Kit Description")]
			public DescriptionSettings InfoKitDescription = new()
			{
				AnchorMin = "0.5 1", AnchorMax = "0.5 1",
				OffsetMin = "-125 -55", OffsetMax = "125 -5",
				Enabled = true,
				Color = new IColor("#0E0E10", 100),
				FontSize = 18,
				Font = "robotocondensed-bold.ttf",
				Align = TextAnchor.MiddleCenter,
				TextColor = new IColor("#FFFFFF", 100)
			};

			[JsonProperty(PropertyName = LangRu ? "Интерфейс" : "Interface")]
			public UserInterface UI = UserInterface.GenerateFullscreenTemplateOldStyle();
			
			[JsonProperty(PropertyName = LangRu ? "Интерфейс для меню" : "Menu UI")]
			public UserInterface MenuUI = UserInterface.GenerateMenuTemplateRustV1();
			
			[JsonProperty(PropertyName = LangRu ? "Пользовательские названия для наборов" : "Custom Title for Kits")]
			public CustomTitles CustomTitles = new()
			{
				Enabled = false,
				KitTitles = new Dictionary<string, CustomTitles.KitTitle>
				{
					["custom_kit"] = new()
					{
						Enabled = false,
						Titles = new Dictionary<string, CustomTitles.TitleConf>
						{
							["NoPermissionDescription"] = new()
							{
								Enabled = false,
								Messages = new Dictionary<string, string>
								{
									["en"] = "You don't have permission to get this kit",
									["fr"] = "Vous n'avez pas l'autorisation d'obtenir ce kit"
								}
							},
							["KitAvailable"] = new()
							{
								Enabled = false,
								Messages = new Dictionary<string, string>
								{
									["en"] = "KIT AVAILABLE\nTO RECEIVE",
									["fr"] = "KIT DISPONIBLE\nPOUR RECEVOIR"
								}
							}
						}
					},
					["second_custom_kit"] = new()
					{
						Enabled = false,
						Titles = new Dictionary<string, CustomTitles.TitleConf>
						{
							["NoPermissionDescription"] = new()
							{
								Enabled = false,
								Messages = new Dictionary<string, string>
								{
									["en"] = "You don't have permission to get this kit",
									["fr"] = "Vous n'avez pas l'autorisation d'obtenir ce kit"
								}
							},
							["KitAvailable"] = new()
							{
								Enabled = false,
								Messages = new Dictionary<string, string>
								{
									["en"] = "KIT AVAILABLE\nTO RECEIVE",
									["fr"] = "KIT DISPONIBLE\nPOUR RECEVOIR"
								}
							}
						}
					}
				}
			};

			[JsonProperty(PropertyName = LangRu ? "Наборы, скрытые в интерфейсе" : "Kits hidden in the interface",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] KitsHidden =
			{
				"Enter the name of the kit here",
				"Example of a string for the second kit"
			};

			public VersionNumber Version;
		}

		private class LogoConf : InterfacePosition
		{
			[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = LangRu ? "Изображение" : "Image")]
			public string Image;
		}

		private class CustomTitles
		{
			[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
			public bool Enabled;

			[JsonProperty(
				PropertyName = LangRu
					? "Названия наборов (название набора – настройки)"
					: "Kit Titles (kit name – settings)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, KitTitle> KitTitles = new();

			public class TitleConf
			{
				[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
				public bool Enabled;

				[JsonProperty(PropertyName = LangRu ? "Текст (язык - текст)" : "Text (language - text)",
					ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public Dictionary<string, string> Messages = new();

				public string GetMessage(BasePlayer player = null)
				{
					if (Messages.Count == 0)
						throw new Exception("The use of custom titles is enabled, but there are no messages!");

					var userLang = "en";
					if (player != null) userLang = _instance.lang.GetLanguage(player.UserIDString);

					return Messages.TryGetValue(userLang, out var message) ? message :
						Messages.TryGetValue("en", out message) ? message : Messages.ElementAt(0).Value;
				}
			}

			public class KitTitle
			{
				[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
				public bool Enabled;

				[JsonProperty(PropertyName = LangRu ? "Название (ключ – настройки)" : "Titles (key – settings)",
					ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public Dictionary<string, TitleConf> Titles = new();
			}
		}

		private class EconomyConf
		{
			#region Fields

			[JsonProperty(PropertyName = "Type (Plugin/Item)")] [JsonConverter(typeof(StringEnumConverter))]
			public EconomyType Type;

			[JsonProperty(PropertyName = "Plugin name")]
			public string Plug;

			[JsonProperty(PropertyName = "Balance add hook")]
			public string AddHook;

			[JsonProperty(PropertyName = "Balance remove hook")]
			public string RemoveHook;

			[JsonProperty(PropertyName = "Balance show hook")]
			public string BalanceHook;

			[JsonProperty(PropertyName = "ShortName")]
			public string ShortName;

			[JsonProperty(PropertyName = "Display Name (empty - default)")]
			public string DisplayName;

			[JsonProperty(PropertyName = "Skin")] public ulong Skin;

			#endregion

			#region Public Methods

			public double ShowBalance(BasePlayer player)
			{
				switch (Type)
				{
					case EconomyType.Plugin:
					{
						var plugin = _instance?.plugins?.Find(Plug);
						if (plugin == null) return 0;

						return Math.Round(Convert.ToDouble(plugin.Call(BalanceHook, player.UserIDString)));
					}
					case EconomyType.Item:
					{
						return PlayerItemsCount(player, ShortName, Skin);
					}
					default:
						return 0;
				}
			}

			public void AddBalance(BasePlayer player, double amount)
			{
				switch (Type)
				{
					case EconomyType.Plugin:
					{
						var plugin = _instance?.plugins?.Find(Plug);
						if (plugin == null) return;

						switch (Plug)
						{
							case "BankSystem":
							case "ServerRewards":
							case "IQEconomic":
								plugin.Call(AddHook, player.UserIDString, (int) amount);
								break;
							default:
								plugin.Call(AddHook, player.UserIDString, amount);
								break;
						}

						break;
					}
					case EconomyType.Item:
					{
						var am = (int) amount;

						var item = ToItem(am);
						if (item == null) return;

						player.GiveItem(item);
						break;
					}
				}
			}

			public bool RemoveBalance(BasePlayer player, double amount)
			{
				switch (Type)
				{
					case EconomyType.Plugin:
					{
						if (ShowBalance(player) < amount) return false;

						var plugin = _instance?.plugins.Find(Plug);
						if (plugin == null) return false;

						switch (Plug)
						{
							case "BankSystem":
							case "ServerRewards":
							case "IQEconomic":
								plugin.Call(RemoveHook, player.UserIDString, (int) amount);
								break;
							default:
								plugin.Call(RemoveHook, player.UserIDString, amount);
								break;
						}

						return true;
					}
					case EconomyType.Item:
					{
						var playerItems = Pool.Get<List<Item>>();
						player.inventory.GetAllItems(playerItems);
					
						var am = (int) amount;

						if (ItemCount(playerItems, ShortName, Skin) < am)
						{
							Pool.Free(ref playerItems);
							return false;
						}

						Take(playerItems, ShortName, Skin, am);
						Pool.Free(ref playerItems);
						return true;
					}
					default:
						return false;
				}
			}

			#endregion

			#region Private Methods

			private int PlayerItemsCount(BasePlayer player, string shortname, ulong skin)
			{
				var items = Pool.Get<List<Item>>();
				player.inventory.GetAllItems(items);
			
				var result = ItemCount(items, shortname, skin);
			
				Pool.Free(ref items);
				return result;
			}

			private int ItemCount(List<Item> items, string shortname, ulong skin)
			{
				return items.FindAll(item =>
						item.info.shortname == shortname && !item.isBroken && (skin == 0 || item.skin == skin))
					.Sum(item => item.amount);
			}

			private static void Take(List<Item> itemList, string shortname, ulong skinId, int iAmount)
			{
				if (iAmount == 0) return;

				var list = Pool.Get<List<Item>>();

				var num1 = 0;
				foreach (var item in itemList)
				{
					if (item.info.shortname != shortname ||
					    (skinId != 0 && item.skin != skinId) || item.isBroken) continue;

					var num2 = iAmount - num1;
					if (num2 <= 0) continue;
					if (item.amount > num2)
					{
						item.MarkDirty();
						item.amount -= num2;
						break;
					}

					if (item.amount <= num2)
					{
						num1 += item.amount;
						list.Add(item);
					}

					if (num1 == iAmount)
						break;
				}

				foreach (var obj in list)
					obj.RemoveFromContainer();

				Pool.FreeUnmanaged(ref list);
			}

			private Item ToItem(int amount)
			{
				var item = ItemManager.CreateByName(ShortName, amount, Skin);
				if (item == null)
				{
					Debug.LogError($"Error creating item with ShortName: '{ShortName}'");
					return null;
				}

				if (!string.IsNullOrEmpty(DisplayName)) item.name = DisplayName;

				return item;
			}

			#endregion

			#region Classes

			public enum EconomyType
			{
				Plugin,
				Item
			}

			#endregion
		}

		private enum InterfaceStyle
		{
			OldStyle,
			NewRust
		}
		
		private class UserInterface
		{
			#region Fields

			[JsonProperty(PropertyName = LangRu ? "Стиль" : "Style")]
			[JsonConverter(typeof(StringEnumConverter))]
			public InterfaceStyle Style;
			
			[JsonProperty(PropertyName = LangRu ? "Показать категории" : "Show Categories")]
			public bool ShowCategories;
			
			[JsonProperty(PropertyName = LangRu ? "Высота" : "Height")]
			public float Height;

			[JsonProperty(PropertyName = LangRu ? "Ширина" : "Width")]
			public float Width;

			[JsonProperty(PropertyName = LangRu ? "Высота набора" : "Kit Height")]
			public float KitHeight;

			[JsonProperty(PropertyName = LangRu ? "Ширина набора" : "Kit Width")]
			public float KitWidth;

			[JsonProperty(PropertyName = LangRu ? "Отступ" : "Margin")]
			public float Margin;

			[JsonProperty(PropertyName = LangRu ? "Кол-во наборов на строке" : "Kits On String")]
			public int KitsOnString;

			[JsonProperty(PropertyName = LangRu ? "Кол-во строк" : "Strings")]
			public int Strings;

			[JsonProperty(PropertyName = LangRu ? "Отступ по слева" : "Left Indent")]
			public float LeftIndent;

			[JsonProperty(PropertyName = LangRu ? "Отступ по вертикали" : "Y Indent")]
			public float YIndent;

			[JsonProperty(PropertyName = LangRu ? "Настройки доступности набора" : "Kit Available Settings")]
			public InterfacePosition KitAvailable;

			[JsonProperty(PropertyName = LangRu ? "Настройки количества набора" : "Kit Amount Settings")]
			public KitAmountSettings KitAmount = new();

			[JsonProperty(PropertyName = LangRu ? "Настройки КД набора" : "Kit Cooldown Settings")]
			public InterfacePosition KitCooldown = new();

			[JsonProperty(PropertyName = LangRu ? "Настройки продажи" : "Kit Sale Settings")]
			public InterfacePosition KitSale = new();

			[JsonProperty(PropertyName =
				LangRu ? "Настройка КД набора (с количеством)" : "Kit Cooldown Settings (with amount)")]
			public InterfacePosition KitAmountCooldown;

			[JsonProperty(PropertyName = LangRu ? "Настройки отсутствия прав" : "No Permission Settings")]
			public InterfacePosition NoPermission;

			[JsonProperty(PropertyName =
				LangRu ? "Закрывать интерфейс после получения набора?" : "Close the interface after receiving a kit?")]
			public bool CloseAfterReceive;

			[JsonProperty(PropertyName = LangRu ? "Настройки логотипа" : "Logo Settings")]
			public LogoConf Logo;

			[JsonProperty(LangRu ? "Главный фон" : "Main background")]
			public ImageSettings MainBackground = new();

			[JsonProperty(LangRu ? "Главная панель" : "Main panel")]
			public ImageSettings MainPanel = new();

			[JsonProperty(PropertyName = LangRu ? "Настройки заголовка" : "Header Settings")]
			public KitsPanelHeaderUI HeaderPanel = new();

			[JsonProperty(PropertyName = LangRu ? "Настройки панели китов" : "Kits Panel Settings")]
			public KitsPanelContentUI ContentPanel = new();

			[JsonProperty(PropertyName = LangRu ? "Настройки кита" : "Kit Settings")]
			public KitsPanelKitUI KitPanel = new();
			
			[JsonProperty(PropertyName = LangRu ? "Настройки категорий" : "Category Settings")]
			public KitsCategoryUI Categories = new();
			
			[JsonProperty(PropertyName = LangRu ? "Цвет 1" : "Color 1")]
			public IColor ColorOne = IColor.CreateWhite();

			[JsonProperty(PropertyName = LangRu ? "Цвет 2" : "Color 2")]
			public IColor ColorTwo = IColor.CreateWhite();

			[JsonProperty(PropertyName = LangRu ? "Цвет 3" : "Color 3")]
			public IColor ColorThree = IColor.CreateWhite();

			[JsonProperty(PropertyName = LangRu ? "Цвет 4" : "Color 4")]
			public IColor ColorFour = IColor.CreateWhite();

			[JsonProperty(PropertyName = LangRu ? "Цвет 5" : "Color 5")]
			public IColor ColorFive = IColor.CreateWhite();

			[JsonProperty(PropertyName = LangRu ? "Цвет Red" : "Color Red")]
			public IColor ColorRed = IColor.CreateWhite();

			[JsonProperty(PropertyName = LangRu ? "Цвет White" : "Color White")]
			public IColor ColorWhite = IColor.CreateWhite();

			[JsonProperty(PropertyName = LangRu ? "Цвет фона" : "Background Color")]
			public IColor ColorBackground = IColor.CreateWhite();

			#endregion

			#region Classes
			
			public class KitsCategoryUI
			{
				[JsonProperty(PropertyName = "Background")]
				public InterfacePosition Background = new();
				
				[JsonProperty(PropertyName = "Category Height")]
				public float Height;

				[JsonProperty(PropertyName = "Category Width")]
				public float Width;

				[JsonProperty(PropertyName = "Category Margin")]
				public float Margin;
                
				[JsonProperty(PropertyName = "Categories Scroll Minimal Width")]
				public float CategoriesScrollMinWidth;
                
				[JsonProperty(PropertyName = "Active Category")]
				public ButtonSettings ActiveCategory = new();

				[JsonProperty(PropertyName = "Inactive Category")]
				public ButtonSettings InactiveCategory = new();
			}

			public class KitsPanelHeaderUI
			{
				[JsonProperty(PropertyName = "Background")]
				public ImageSettings Background = new();
				
				[JsonProperty(PropertyName = "Title")]
				public TextSettings Title = new();
				
				[JsonProperty(PropertyName = "Show Line?")]
				public bool ShowLine;
                
				[JsonProperty(PropertyName = "Line")]
				public ImageSettings Line = new();

				[JsonProperty(PropertyName = "Close Button")]
				public ButtonSettings ButtonClose = new();
			}

			public class KitsPanelContentUI
			{
				[JsonProperty(PropertyName = "Background")]
				public ImageSettings Background = new();
				
				[JsonProperty(PropertyName = "Button Back")]
				public ButtonSettings ButtonBack = new();
				
				[JsonProperty(PropertyName = "Button Next")]
				public ButtonSettings ButtonNext = new();
				
				[JsonProperty(PropertyName = "Button Create Kit")]
				public ButtonSettings ButtonCreateKit = new();

				[JsonProperty(PropertyName = "Show All Kits Checkbox")]
				public CheckBoxSettings CheckboxShowAllKits = new();
			}
            
            public class KitsPanelKitUI
            {
                [JsonProperty(PropertyName = "Background")]
				public ImageSettings Background = new();
				
				[JsonProperty(PropertyName = "Show Name?")]
				public bool ShowName;

				[JsonProperty(PropertyName = "Kit Name")]
				public TextSettings KitName = new();
			
				[JsonProperty(PropertyName = "Show Number?")]
                public bool ShowNumber;
                
				[JsonProperty(PropertyName = "Kit Number")]
				public TextSettings KitNumber = new();
				
				[JsonProperty(PropertyName = "Kit Image")]
				public InterfacePosition KitImage = new();
				
				[JsonProperty(PropertyName = "Kit Button Take")]
				public ButtonSettings KitButtonTake = new();
				
				[JsonProperty(PropertyName = "Kit Button Take (when show info)")]
				public ButtonSettings KitButtonTakeWhenShowInfo = new();
				
				[JsonProperty(PropertyName = "Kit Button Info")]
				public ButtonSettings KitButtonInfo = new();
				
				[JsonProperty(PropertyName = "Show Line?")]
				public bool ShowLine;
                
				[JsonProperty(PropertyName = "Kit Line")]
				public InterfacePosition KitLine = new();

				[JsonProperty(PropertyName = "Display Kit Contents")]
				public bool DisplayKitContents;
                
				[JsonProperty(PropertyName = LangRu ? "Настройки содержимого" : "Contents Settings")]
				public ContentsSettings Contents = new();
            }
            
            public class ContentsSettings
            {
	            [JsonProperty(PropertyName = "Size")]
	            public float Size = 40f;

	            [JsonProperty(PropertyName = "Margin")]
	            public float Margin = 5f;

	            [JsonProperty(PropertyName = "Up Indent")]
	            public float UpIndent = 35f;

	            [JsonProperty(PropertyName = "Item Background")]
	            public ImageSettings ItemBackground = new();
	            
	            [JsonProperty(PropertyName = "Item Image")]
	            public InterfacePosition ItemImage = new();
	            
	            [JsonProperty(PropertyName = "Item Amount")]
	            public TextSettings ItemAmount = new();
	            
	            [JsonProperty(PropertyName = "Kit Cooldown Settings")]
	            public ButtonSettings KitCooldown = new();

	            [JsonProperty(PropertyName = "Kit Cooldown With Amount Settings")]
	            public ButtonSettings KitCooldownWithAmount = new();
				
	            [JsonProperty(PropertyName = "Kit Amount Settings")]
	            public TextSettings KitAmount = new();
				
	            [JsonProperty(PropertyName = "Kit Sale Settings")]
	            public ButtonSettings KitSale = new();
            }

			#endregion

			#region Templates

			public static UserInterface GenerateFullscreenTemplateOldStyle()
			{
				return new UserInterface()
				{
					Style = InterfaceStyle.OldStyle,
					Height = 455,
					Width = 640,
					KitHeight = 165,
					KitWidth = 135f,
					Margin = 10f,
					KitsOnString = 4,
					Strings = 2,
					LeftIndent = 35,
					YIndent = -50f,
					KitAvailable = new InterfacePosition
					{
						AnchorMin = "0 1",
						AnchorMax = "1 1",
						OffsetMin = "0 -100",
						OffsetMax = "0 -75"
					},
					KitAmount = new KitAmountSettings
					{
						AnchorMin = "0.5 1",
						AnchorMax = "0.5 1",
						OffsetMin = "-125",
						OffsetMax = "-120",
						Width = 115
					},
					KitCooldown = new InterfacePosition
					{
						AnchorMin = "0.5 1",
						AnchorMax = "0.5 1",
						OffsetMin = "-32.5 -125",
						OffsetMax = "32.5 -105"
					},
					KitSale = new InterfacePosition
					{
						AnchorMin = "0.5 1",
						AnchorMax = "0.5 1",
						OffsetMin = "-32.5 -115",
						OffsetMax = "32.5 -95"
					},
					KitAmountCooldown = new InterfacePosition
					{
						AnchorMin = "0 1",
						AnchorMax = "1 1",
						OffsetMin = "0 -120",
						OffsetMax = "0 -95"
					},
					NoPermission = new InterfacePosition
					{
						AnchorMin = "0 1",
						AnchorMax = "1 1",
						OffsetMin = "0 -100",
						OffsetMax = "0 -75"
					},
					CloseAfterReceive = true,
					Logo = new LogoConf
					{
						AnchorMin = "0 0.5",
						AnchorMax = "0 0.5",
						OffsetMin = "10 -20",
						OffsetMax = "50 20",
						Enabled = false,
						Image = string.Empty
					},
					MainBackground = new ImageSettings()
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						Color = new IColor("#191919", 90),
						Sprite = "assets/content/ui/UI.Background.TileTex.psd",
						Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
						CursorEnabled = true
					},
					MainPanel = new ImageSettings()
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = "-320 -225",
						OffsetMax = "320 230",
						Color = new IColor("#0E0E10", 100),
						Material = "assets/content/ui/uibackgroundblur.mat"
					},
					HeaderPanel = new KitsPanelHeaderUI()
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 1",
							AnchorMax = "1 1",
							OffsetMin = "0 -50",
							OffsetMax = "0 0",
                            Color = new IColor("#161617", 100),
						},
						Title = new TextSettings()
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "10 0",
							OffsetMax = "0 0",
							Align = TextAnchor.MiddleLeft,
							IsBold = true,
							FontSize = 14,
							Color = IColor.CreateWhite()
						},
						ButtonClose = new ButtonSettings
						{
							AnchorMin = "1 1", AnchorMax = "1 1",
							OffsetMin = "-35 -37.5",
							OffsetMax = "-10 -12.5",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 10,
							Color = IColor.CreateWhite(),
							ButtonColor = new IColor("#4B68FF", 100),
						}
					},
					ContentPanel = new KitsPanelContentUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 0",
							AnchorMax = "1 1",
							OffsetMin = "0 0",
							OffsetMax = "0 -50",
							Color = IColor.CreateTransparent()
						},
						ButtonBack = new ButtonSettings()
						{
							AnchorMin = "1 1", AnchorMax = "1 1",
							OffsetMin = "-132.5 -32.5",
							OffsetMax = "-72.5 -10",
							Align = TextAnchor.MiddleCenter,
							IsBold = false,
							FontSize = 10,
							Color = new IColor("#FFFFFF", 100),
							ButtonColor = new IColor("#161617", 100),
						},
						ButtonNext = new ButtonSettings()
						{
							AnchorMin = "1 1", AnchorMax = "1 1",
							OffsetMin = "-70 -32.5",
							OffsetMax = "-10 -10",
							Align = TextAnchor.MiddleCenter,
							IsBold = false,
							FontSize = 10,
							Color = new IColor("#FFFFFF", 100),
							ButtonColor = new IColor("#4B68FF", 100),
						},
						ButtonCreateKit = new ButtonSettings()
						{
							AnchorMin = "1 1", AnchorMax = "1 1",
							OffsetMin = "-237.5 -32.5",
							OffsetMax = "-142.5 -10",
							Align = TextAnchor.MiddleCenter,
							IsBold = false,
							FontSize = 10,
							Color = new IColor("#FFFFFF", 100),
							ButtonColor = new IColor("#4B68FF", 100),
						},
						CheckboxShowAllKits = new CheckBoxSettings()
						{
							Background = new ImageSettings
							{
								AnchorMin = "0 1", 
								AnchorMax = "0 1",
								OffsetMin = "35 -25",
								OffsetMax = "45 -15",
								Color = IColor.CreateTransparent()
							},
							CheckboxButton = new ButtonSettings
							{
								AnchorMin = "0 0.5", AnchorMax = "0 0.5",
								OffsetMin = "5 -7", OffsetMax = "19 7",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 10,
								Color = IColor.CreateWhite(),
								ButtonColor = IColor.CreateTransparent(),
							},
							CheckboxColor = new IColor("#4B68FF", 100),
							CheckboxSize = 2f,
							Title = new TextSettings
							{
								AnchorMin = "1 0",
								AnchorMax = "1 1",
								OffsetMin = "4 -4",
								OffsetMax = "104 4",
								IsBold = true, FontSize = 10, Align = TextAnchor.MiddleLeft,
								Color = IColor.CreateWhite()
							}
						},
					},
					KitPanel = new KitsPanelKitUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 0",
							AnchorMax = "1 1",
							OffsetMin = "0 30",
							OffsetMax = "0 0",
							Color = new IColor("#161617", 100)
						},
						ShowName = true,
						KitName = new TextSettings()
						{
							AnchorMin = "0.5 1",
							AnchorMax = "0.5 1",
							OffsetMin = "-45 -75",
							OffsetMax = "45 0",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 16,
							Color = IColor.CreateWhite(),
						},
						ShowNumber = true,
						KitNumber = new TextSettings()
						{
							AnchorMin = "0.5 1", 
							AnchorMax = "0.5 1",
							OffsetMin = "-45 -75",
							OffsetMax = "45 0",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 60,
							Color = new IColor("#303030", 100),
						},
						KitImage = new InterfacePosition
						{
							AnchorMin = "0.5 1",
							AnchorMax = "0.5 1",
							OffsetMin = "-32 -75",
							OffsetMax = "32 -11"
						},
						KitButtonTake = new ButtonSettings
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = "0 -165",
							OffsetMax = "0 -140",
							Align = TextAnchor.MiddleCenter,
							IsBold = false,
							FontSize = 10,
							Color = IColor.CreateWhite(),
							ButtonColor = new IColor("#161617", 100),
						},
						KitButtonTakeWhenShowInfo = new ButtonSettings
						{
							AnchorMin = "0 1", AnchorMax = "0 1",
							OffsetMin = "0 -165",
							OffsetMax = "105 -140",
							Align = TextAnchor.MiddleCenter,
							IsBold = false,
							FontSize = 10,
							Color = IColor.CreateWhite(),
							ButtonColor = new IColor("#161617", 100),
						},
						KitButtonInfo = new ButtonSettings
						{
							AnchorMin = "1 1", AnchorMax = "1 1",
							OffsetMin = "-25 -165",
							OffsetMax = "0 -140",
							Align = TextAnchor.MiddleCenter,
							IsBold = false,
							FontSize = 10,
							Color = IColor.CreateWhite(),
							ButtonColor = new IColor("#161617", 100),
						},
						ShowLine = true,
						KitLine = new InterfacePosition
						{
							AnchorMin = "0 0", AnchorMax = "1 0",
							OffsetMin = "0 0", OffsetMax = "0 2"
						}
					},
					Categories = new KitsCategoryUI(),
					ColorOne = new IColor("#161617", 100),
					ColorTwo = new IColor("#0E0E10", 100),
					ColorThree = new IColor("#4B68FF", 100),
					ColorFour = new IColor("#303030", 100),
					ColorFive = new IColor("#0E0E10", 98),
					ColorRed = new IColor("#FF4B4B", 100),
					ColorWhite = new IColor("#FFFFFF", 100),
					ColorBackground = new IColor("#0E0E10", 100),
				};
			}

			public static UserInterface GenerateFullscreenTemplateRust()
			{
				return new UserInterface()
				{
					Style = InterfaceStyle.NewRust,
					Height = 455,
					// Width = 816,
					Width = 688,
					KitHeight = 238,
					KitWidth = 160,
					Margin = 16,
					KitsOnString = 4,
					Strings = 2,
					// LeftIndent = 64,
					LeftIndent = 40,
					YIndent = -10,
					KitAvailable = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
					},
					KitAmount = new KitAmountSettings
					{
						AnchorMin = "0.5 0",
						AnchorMax = "0.5 0",
						OffsetMin = "0",
						OffsetMax = "4",
						Width = 140
					},
					KitCooldown = new InterfacePosition
					{
						AnchorMin = "0 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "0 -10",
						OffsetMax = "0 20"
					},
					KitSale = new InterfacePosition
					{
						AnchorMin = "0 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "0 -10",
						OffsetMax = "0 20"
					},
					KitAmountCooldown = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", 
						OffsetMin = "0 4", OffsetMax = "0 0"
					},
					NoPermission = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
					},
					CloseAfterReceive = true,
					Logo = new LogoConf
					{
						AnchorMin = "0 0.5",
						AnchorMax = "0 0.5",
						OffsetMin = "10 -20",
						OffsetMax = "50 20",
						Enabled = false,
						Image = string.Empty
					},
					MainBackground = new ImageSettings()
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						Color = new IColor("#191919", 90),
						Sprite = "assets/content/ui/UI.Background.TileTex.psd",
						Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
						CursorEnabled = true
					},
					MainPanel = new ImageSettings()
					{
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-384 -295",
                        OffsetMax = "384 295",
                        Color = new IColor("#191919", 50),
                        Material = "assets/content/ui/uibackgroundblur.mat"
					},
					HeaderPanel = new KitsPanelHeaderUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 1", AnchorMax = "1 1", 
							OffsetMin = "0 -50", OffsetMax = "0 0",
							Color = new IColor("#494949"),
							Sprite = "assets/content/ui/UI.Background.Transparent.LinearLTR.tga"
						},
						Title = new TextSettings()
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "20 0", OffsetMax = "0 0",
							Align = TextAnchor.MiddleLeft,
							IsBold = true,
							FontSize = 18,
							Color = new IColor("#E2DBD3")
						},
						ButtonClose = new ButtonSettings
						{
                            AnchorMin = "1 1",
                            AnchorMax = "1 1",
                            OffsetMin = "-40 -40",
                            OffsetMax = "0 0",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 10,
							ImageColor = new IColor("#E2DBD3"),
							ButtonColor = new IColor("#D74933"),
							UseCustomPositionImage = true,
							ImagePosition = new InterfacePosition
							{
								AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-9 -9", OffsetMax = "9 9"
							},
							Image = "assets/icons/close.png",
							Material = "assets/content/ui/namefontmaterial.mat",
						}
					},
					ContentPanel = new KitsPanelContentUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 0",
							AnchorMax = "1 1",
							OffsetMin = "0 0",
							OffsetMax = "0 -50",
							Color = IColor.CreateTransparent()
						},
						ButtonBack = new ButtonSettings()
						{
							AnchorMin = "0 0.5", AnchorMax = "0 0.5", 
							OffsetMin = "5 -2", 
							OffsetMax = "35 28",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 14,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#696969", 30),
							Material = "assets/content/ui/namefontmaterial.mat"
						},
						ButtonNext = new ButtonSettings()
						{
							AnchorMin = "1 0.5", AnchorMax = "1 0.5",
							OffsetMin = "-35 -2", 
							OffsetMax = "-5 28",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 14,
							Color = new IColor("#FFFFFF", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat"
						},
						ButtonCreateKit = new ButtonSettings()
						{
							AnchorMin = "1 0", AnchorMax = "1 0", 
							OffsetMin = "-120 8", OffsetMax = "-40 32",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#71B8ED", 100),
							ButtonColor = new IColor("#71B8ED", 5),
							Material = "assets/content/ui/namefontmaterial.mat"
						},
						CheckboxShowAllKits = new CheckBoxSettings()
						{
							Background = new ImageSettings
							{
								AnchorMin = "0 0", 
								AnchorMax = "0 0",
								OffsetMin = "40 10", 
								OffsetMax = "60 30",
								Color = IColor.CreateTransparent()
							},
							CheckboxButton = new ButtonSettings
							{
								AnchorMin = "0 0.5", AnchorMax = "0 0.5",
								OffsetMin = "0 -7", OffsetMax = "14 7",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 10,
								Color = IColor.CreateWhite(),
								ButtonColor = IColor.CreateTransparent(),
							},
							CheckboxColor = new IColor("#38393F"),
							CheckboxSize = 3f,
							Title = new TextSettings
							{
								AnchorMin = "1 0",
								AnchorMax = "1 1",
								OffsetMin = "5 -4",
								OffsetMax = "105 4",
								IsBold = true, FontSize = 10, Align = TextAnchor.MiddleLeft,
								Color = IColor.CreateWhite()
							}
						},
					},
					KitPanel = new KitsPanelKitUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "0 0", OffsetMax = "0 0",
							Color = new IColor("#696969", 10),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd",
						},
						ShowName = true,
						KitName = new TextSettings()
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "10 -35", OffsetMax = "-10 -5",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 18,
							Color = new IColor("#E2DBD3", 100),
						},
						ShowNumber = true,
						KitNumber = new TextSettings()
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-60 -155", OffsetMax = "60 -35",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 60,
							Color = new IColor("#E2DBD3", 20),
						},
						KitImage = new InterfacePosition
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-60 -155", OffsetMax = "60 -35",
						},
						KitButtonTake = new ButtonSettings
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-70 10", OffsetMax = "70 38",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 13,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						KitButtonTakeWhenShowInfo = new ButtonSettings
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-70 10", OffsetMax = "40 38",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 13,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						KitButtonInfo = new ButtonSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "125 10", OffsetMax = "-10 38",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 13,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#F19F39", 60),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
					},
					Categories = new KitsCategoryUI(),
					ColorOne = new IColor("#E2DBD3", 100),
					ColorTwo = new IColor("#D74933", 100),
					ColorThree = new IColor("#8B8B8B", 15),
					ColorFour = new IColor("#38393F", 50),
					ColorFive = new IColor("#E2DBD3", 20),
					ColorRed = new IColor("#FF4B4B", 100),
					ColorWhite = new IColor("#FFFFFF", 100),
					ColorBackground = IColor.CreateTransparent()
				};
			}

			public static UserInterface GenerateFullscreenTemplateWithCategoriesRust()
			{
				return new UserInterface()
				{
					Style = InterfaceStyle.NewRust,
					Height = 455,
					Width = 688,
					KitHeight = 238,
					KitWidth = 160,
					Margin = 16,
					KitsOnString = 4,
					Strings = 2,
					LeftIndent = 40,
					YIndent = 0,
					ShowCategories = true,
					KitAvailable = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
					},
					KitAmount = new KitAmountSettings
					{
						AnchorMin = "0.5 0",
						AnchorMax = "0.5 0",
						OffsetMin = "0",
						OffsetMax = "4",
						Width = 140
					},
					KitCooldown = new InterfacePosition
					{
						AnchorMin = "0 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "0 -10",
						OffsetMax = "0 20"
					},
					KitSale = new InterfacePosition
					{
						AnchorMin = "0 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "0 -10",
						OffsetMax = "0 20"
					},
					KitAmountCooldown = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", 
						OffsetMin = "0 4", OffsetMax = "0 0"
					},
					NoPermission = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
					},
					CloseAfterReceive = true,
					Logo = new LogoConf
					{
						AnchorMin = "0 0.5",
						AnchorMax = "0 0.5",
						OffsetMin = "10 -20",
						OffsetMax = "50 20",
						Enabled = false,
						Image = string.Empty
					},
					MainBackground = new ImageSettings()
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						Color = new IColor("#191919", 90),
						Sprite = "assets/content/ui/UI.Background.TileTex.psd",
						Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
						CursorEnabled = true
					},
					MainPanel = new ImageSettings()
					{
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-384 -310",
                        OffsetMax = "384 310",
                        Color = new IColor("#191919", 50),
                        Material = "assets/content/ui/uibackgroundblur.mat"
					},
					HeaderPanel = new KitsPanelHeaderUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 1", AnchorMax = "1 1", 
							OffsetMin = "0 -50", OffsetMax = "0 0",
							Color = new IColor("#494949"),
							Sprite = "assets/content/ui/UI.Background.Transparent.LinearLTR.tga"
						},
						Title = new TextSettings()
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "20 0", OffsetMax = "0 0",
							Align = TextAnchor.MiddleLeft,
							IsBold = true,
							FontSize = 18,
							Color = new IColor("#E2DBD3")
						},
						ButtonClose = new ButtonSettings
						{
                            AnchorMin = "1 1",
                            AnchorMax = "1 1",
                            OffsetMin = "-40 -40",
                            OffsetMax = "0 0",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 10,
							ImageColor = new IColor("#E2DBD3"),
							ButtonColor = new IColor("#D74933"),
							UseCustomPositionImage = true,
							ImagePosition = new InterfacePosition
							{
								AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-9 -9", OffsetMax = "9 9"
							},
							Image = "assets/icons/close.png",
							Material = "assets/content/ui/namefontmaterial.mat",
						}
					},
					
					ContentPanel = new KitsPanelContentUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 0",
							AnchorMax = "1 1",
							OffsetMin = "0 0",
							OffsetMax = "0 -95",
							Color = IColor.CreateTransparent()
						},
						ButtonBack = new ButtonSettings()
						{
							AnchorMin = "0 0.5", AnchorMax = "0 0.5", 
							OffsetMin = "5 2", 
							OffsetMax = "35 32",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 14,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#696969", 30),
							Material = "assets/content/ui/namefontmaterial.mat"
						},
						ButtonNext = new ButtonSettings()
						{
							AnchorMin = "1 0.5", AnchorMax = "1 0.5",
							OffsetMin = "-35 2", 
							OffsetMax = "-5 32",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 14,
							Color = new IColor("#FFFFFF", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat"
						},
						ButtonCreateKit = new ButtonSettings()
						{
							AnchorMin = "1 0", AnchorMax = "1 0", 
							OffsetMin = "-120 6", OffsetMax = "-40 28",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#71B8ED", 100),
							ButtonColor = new IColor("#71B8ED", 5),
							Material = "assets/content/ui/namefontmaterial.mat"
						},
						CheckboxShowAllKits = new CheckBoxSettings()
						{
							Background = new ImageSettings
							{
								AnchorMin = "0 0", 
								AnchorMax = "0 0",
								OffsetMin = "40 10", 
								OffsetMax = "60 30",
								Color = IColor.CreateTransparent()
							},
							CheckboxButton = new ButtonSettings
							{
								AnchorMin = "0 0.5", AnchorMax = "0 0.5",
								OffsetMin = "0 -7", OffsetMax = "14 7",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 10,
								Color = IColor.CreateWhite(),
								ButtonColor = IColor.CreateTransparent(),
							},
							CheckboxColor = new IColor("#38393F"),
							CheckboxSize = 3f,
							Title = new TextSettings
							{
								AnchorMin = "1 0",
								AnchorMax = "1 1",
								OffsetMin = "4 -4",
								OffsetMax = "104 4",
								IsBold = true, FontSize = 10, Align = TextAnchor.MiddleLeft,
								Color = IColor.CreateWhite()
							}
						},
					},
					
					KitPanel = new KitsPanelKitUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "0 0", OffsetMax = "0 0",
							Color = new IColor("#696969", 10),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd",
						},
						ShowName = true,
						KitName = new TextSettings()
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "10 -35", OffsetMax = "-10 -5",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 18,
							Color = new IColor("#E2DBD3", 100),
						},
						ShowNumber = true,
						KitNumber = new TextSettings()
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-60 -155", OffsetMax = "60 -35",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 60,
							Color = new IColor("#E2DBD3", 20),
						},
						KitImage = new InterfacePosition
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-60 -155", OffsetMax = "60 -35",
						},
						KitButtonTake = new ButtonSettings
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-70 10", OffsetMax = "70 38",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 13,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						KitButtonTakeWhenShowInfo = new ButtonSettings
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-70 10", OffsetMax = "40 38",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 13,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						KitButtonInfo = new ButtonSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "125 10", OffsetMax = "-10 38",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 13,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#F19F39", 60),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
					},
					
					Categories = new KitsCategoryUI
					{
						Background = InterfacePosition.CreatePosition("0 1", "1 1", "40 -90", "-40 -60"),
						Height = 25,
						Width = 100,
						Margin = 5,
						CategoriesScrollMinWidth = 688,
						ActiveCategory = new ButtonSettings
						{
							ButtonColor = new IColor("#CF432D", 80),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd",
							Color = new IColor("#E2DBD3", 100),
							IsBold = true,
							FontSize = 14,
                            Align = TextAnchor.MiddleCenter
						},
						InactiveCategory = new ButtonSettings
						{
							ButtonColor = new IColor("#696969", 30),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd",
							Color = new IColor("#E2DBD3", 90),
							IsBold = true,
							FontSize = 14,
                            Align = TextAnchor.MiddleCenter
						},
					},
					
					ColorOne = new IColor("#E2DBD3", 100),
					ColorTwo = new IColor("#D74933", 100),
					ColorThree = new IColor("#8B8B8B", 15),
					ColorFour = new IColor("#38393F", 50),
					ColorFive = new IColor("#E2DBD3", 20),
					ColorRed = new IColor("#FF4B4B", 100),
					ColorWhite = new IColor("#FFFFFF", 100),
					ColorBackground = new IColor("#000000", 0),
				};
			}

			public static UserInterface GenerateFullscreenTemplateWithContentsRust()
			{
				return new UserInterface()
				{
					Style = InterfaceStyle.NewRust,
					// Height = 820,
					// Height = 555,
					Height = 455,
					Width = 800,
					// Width = 688,
					KitHeight = 256,
					// KitHeight = 358,
					KitWidth = 188,
					// KitWidth = 285,
					Margin = 16,
					KitsOnString = 4,
					Strings = 2,
					LeftIndent = 40,
					YIndent = 0,
					ShowCategories = false,
					KitAvailable = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
					},
					KitAmount = new KitAmountSettings
					{
						AnchorMin = "0.5 0",
						AnchorMax = "0.5 0",
						OffsetMin = "0",
						OffsetMax = "4",
						Width = 140
					},
					KitCooldown = new InterfacePosition
					{
						AnchorMin = "0 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "0 -10",
						OffsetMax = "0 20"
					},
					KitSale = new InterfacePosition
					{
						AnchorMin = "0 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "0 -10",
						OffsetMax = "0 20"
					},
					KitAmountCooldown = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", 
						OffsetMin = "0 4", OffsetMax = "0 0"
					},
					NoPermission = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
					},
					CloseAfterReceive = true,
					Logo = new LogoConf
					{
						AnchorMin = "0 0.5",
						AnchorMax = "0 0.5",
						OffsetMin = "10 -20",
						OffsetMax = "50 20",
						Enabled = false,
						Image = string.Empty
					},
					MainBackground = new ImageSettings()
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						Color = new IColor("#191919", 90),
						Sprite = "assets/content/ui/UI.Background.TileTex.psd",
						Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
						CursorEnabled = true
					},
					MainPanel = new ImageSettings()
					{
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-440 -310",
                        OffsetMax = "440 310",
                        Color = new IColor("#191919", 50),
                        Material = "assets/content/ui/uibackgroundblur.mat"
					},
					HeaderPanel = new KitsPanelHeaderUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 1", AnchorMax = "1 1", 
							OffsetMin = "0 -50", OffsetMax = "0 0",
							Color = new IColor("#494949"),
							Sprite = "assets/content/ui/UI.Background.Transparent.LinearLTR.tga"
						},
						Title = new TextSettings()
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "20 0", OffsetMax = "0 0",
							Align = TextAnchor.MiddleLeft,
							IsBold = true,
							FontSize = 18,
							Color = new IColor("#E2DBD3")
						},
						ButtonClose = new ButtonSettings
						{
                            AnchorMin = "1 1",
                            AnchorMax = "1 1",
                            OffsetMin = "-40 -40",
                            OffsetMax = "0 0",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 10,
							ImageColor = new IColor("#E2DBD3"),
							ButtonColor = new IColor("#D74933"),
							UseCustomPositionImage = true,
							ImagePosition = new InterfacePosition
							{
								AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-9 -9", OffsetMax = "9 9"
							},
							Image = "assets/icons/close.png",
							Material = "assets/content/ui/namefontmaterial.mat",
						}
					},
					
					ContentPanel = new KitsPanelContentUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 0",
							AnchorMax = "1 1",
							OffsetMin = "0 0",
							OffsetMax = "0 -60",
							Color = IColor.CreateTransparent()
						},
						ButtonBack = new ButtonSettings()
						{
							AnchorMin = "0 0.5", AnchorMax = "0 0.5", 
							OffsetMin = "5 0", 
							OffsetMax = "35 30",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 14,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#696969", 30),
							Material = "assets/content/ui/namefontmaterial.mat"
						},
						ButtonNext = new ButtonSettings()
						{
							AnchorMin = "1 0.5", AnchorMax = "1 0.5",
							OffsetMin = "-35 0", 
							OffsetMax = "-5 30",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 14,
							Color = new IColor("#FFFFFF", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat"
						},
						ButtonCreateKit = new ButtonSettings()
						{
							AnchorMin = "1 0", AnchorMax = "1 0", 
							OffsetMin = "-120 6", OffsetMax = "-40 28",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#71B8ED", 100),
							ButtonColor = new IColor("#71B8ED", 5),
							Material = "assets/content/ui/namefontmaterial.mat"
						},
						CheckboxShowAllKits = new CheckBoxSettings()
						{
							Background = new ImageSettings
							{
								AnchorMin = "0 0", 
								AnchorMax = "0 0",
								OffsetMin = "40 10", 
								OffsetMax = "60 30",
								Color = IColor.CreateTransparent()
							},
							CheckboxButton = new ButtonSettings
							{
								AnchorMin = "0 0.5", AnchorMax = "0 0.5",
								OffsetMin = "0 -7", OffsetMax = "14 7",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 10,
								Color = IColor.CreateWhite(),
								ButtonColor = IColor.CreateTransparent(),
							},
							CheckboxColor = new IColor("#38393F"),
							CheckboxSize = 3f,
							Title = new TextSettings
							{
								AnchorMin = "1 0",
								AnchorMax = "1 1",
								OffsetMin = "5 -4",
								OffsetMax = "105 4",
								IsBold = true, FontSize = 10, Align = TextAnchor.MiddleLeft,
								Color = IColor.CreateWhite()
							}
						},
					},
					
					KitPanel = new KitsPanelKitUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "0 0", OffsetMax = "0 0",
							Color = new IColor("#696969", 10),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd",
						},
						ShowName = true,
						KitName = new TextSettings()
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "10 -20", OffsetMax = "-10 0",
							Align = TextAnchor.MiddleLeft,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#E2DBD3", 100),
						},
						ShowNumber = false,
						KitNumber = new TextSettings()
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-60 -155", OffsetMax = "60 -35",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 60,
							Color = new IColor("#E2DBD3", 20),
						},
						KitImage = new InterfacePosition
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-60 -155", OffsetMax = "60 -35",
						},
						KitButtonTake = new ButtonSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						KitButtonTakeWhenShowInfo = new ButtonSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						KitButtonInfo = new ButtonSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "125 10", OffsetMax = "-10 38",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 13,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#F19F39", 60),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						DisplayKitContents = true,
						Contents = new ContentsSettings
						{
							Size = 28,
							Margin = 2,
							UpIndent = 20,
							ItemBackground = new ImageSettings
							{
								AnchorMin = "0.5 1",
								AnchorMax = "0.5 1",
								OffsetMin = "0 0",
								OffsetMax = "0 0",
								Color = new IColor("#696969", 30)
							},
							ItemImage = InterfacePosition.CreatePosition("0 0", "1 1", "2 2", "-2 -2"),
							ItemAmount = new TextSettings()
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "0 0", OffsetMax = "-2 -2",
								Align = TextAnchor.LowerRight,
								IsBold = true,
								FontSize = 6,
								Color = IColor.CreateWhite()
							},
							KitCooldown = new ButtonSettings
							{
								AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 14,
								Color = new IColor("#D1CBC4"),
								ButtonColor = new IColor("#696969", 30),
								Material = "assets/content/ui/namefontmaterial.mat",
								Sprite = "assets/content/ui/UI.Background.Tile.psd"
							},
							KitCooldownWithAmount = new ButtonSettings
							{
								AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 12,
								Color = new IColor("#D1CBC4"),
								ButtonColor = new IColor("#696969", 30),
								Material = "assets/content/ui/namefontmaterial.mat",
								Sprite = "assets/content/ui/UI.Background.Tile.psd"
							},
							KitAmount = new TextSettings
							{
								AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -20", OffsetMax = "-10 0",
								IsBold = true,
								Color = new IColor("#D1CBC4"),
								FontSize = 12, 
								Align = TextAnchor.MiddleRight
							},
							KitSale = new ButtonSettings
							{
								AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 12,
								Color = new IColor("#E2DBD3", 100),
								ButtonColor = new IColor("#D74933", 100),
								Material = "assets/content/ui/namefontmaterial.mat",
								Sprite = "assets/content/ui/UI.Background.Tile.psd"
							}
						}
					},
					
					Categories = new KitsCategoryUI
					{
						Background = InterfacePosition.CreatePosition("0 1", "1 1", "0 -46", "0 -10"),
						Height = 30,
						Width = 100,
						Margin = 5,
						CategoriesScrollMinWidth = 890,
						ActiveCategory = new ButtonSettings
						{
							ButtonColor = new IColor("#CF432D", 50),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd",
							Color = new IColor("#E2DBD3", 100),
							IsBold = true,
							FontSize = 14,
							Align = TextAnchor.MiddleCenter
						},
						InactiveCategory = new ButtonSettings
						{
							ButtonColor = new IColor("#696969", 15),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd",
							Color = new IColor("#E2DBD3", 90),
							IsBold = true,
							FontSize = 14,
							Align = TextAnchor.MiddleCenter
						},
					},
					
					ColorOne = new IColor("#E2DBD3", 100),
					ColorTwo = new IColor("#D74933", 100),
					ColorThree = new IColor("#8B8B8B", 15),
					ColorFour = new IColor("#38393F", 50),
					ColorFive = new IColor("#E2DBD3", 20),
					ColorRed = new IColor("#FF4B4B", 100),
					ColorWhite = new IColor("#FFFFFF", 100),
					ColorBackground = new IColor("#000000", 0),
				};
			}

			public static UserInterface GenerateFullscreenTemplateWithContentsAndCategoriesRust()
			{
				return new UserInterface()
				{
					Style = InterfaceStyle.NewRust,
					// Height = 820,
					// Height = 555,
					Height = 512,
					// Height = 455,
					Width = 800,
					// Width = 688,
					KitHeight = 236,
					// KitHeight = 358,
					KitWidth = 188,
					// KitWidth = 285,
					Margin = 16,
					KitsOnString = 4,
					Strings = 2,
					LeftIndent = 40,
					YIndent = 0,
					ShowCategories = true,
					KitAvailable = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
					},
					KitAmount = new KitAmountSettings
					{
						AnchorMin = "0.5 0",
						AnchorMax = "0.5 0",
						OffsetMin = "0",
						OffsetMax = "4",
						Width = 140
					},
					KitCooldown = new InterfacePosition
					{
						AnchorMin = "0 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "0 -10",
						OffsetMax = "0 20"
					},
					KitSale = new InterfacePosition
					{
						AnchorMin = "0 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "0 -10",
						OffsetMax = "0 20"
					},
					KitAmountCooldown = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", 
						OffsetMin = "0 4", OffsetMax = "0 0"
					},
					NoPermission = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
					},
					CloseAfterReceive = true,
					Logo = new LogoConf
					{
						AnchorMin = "0 0.5",
						AnchorMax = "0 0.5",
						OffsetMin = "10 -20",
						OffsetMax = "50 20",
						Enabled = false,
						Image = string.Empty
					},
					MainBackground = new ImageSettings()
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						Color = new IColor("#191919", 90),
						Sprite = "assets/content/ui/UI.Background.TileTex.psd",
						Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
						CursorEnabled = true
					},
					MainPanel = new ImageSettings()
					{
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-440 -310",
                        OffsetMax = "440 310",
                        Color = new IColor("#191919", 50),
                        Material = "assets/content/ui/uibackgroundblur.mat"
					},
					HeaderPanel = new KitsPanelHeaderUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 1", AnchorMax = "1 1", 
							OffsetMin = "0 -50", OffsetMax = "0 0",
							Color = new IColor("#494949"),
							Sprite = "assets/content/ui/UI.Background.Transparent.LinearLTR.tga"
						},
						Title = new TextSettings()
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "20 0", OffsetMax = "0 0",
							Align = TextAnchor.MiddleLeft,
							IsBold = true,
							FontSize = 18,
							Color = new IColor("#E2DBD3")
						},
						ButtonClose = new ButtonSettings
						{
                            AnchorMin = "1 1",
                            AnchorMax = "1 1",
                            OffsetMin = "-40 -40",
                            OffsetMax = "0 0",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 10,
							ImageColor = new IColor("#E2DBD3"),
							ButtonColor = new IColor("#D74933"),
							UseCustomPositionImage = true,
							ImagePosition = new InterfacePosition
							{
								AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-9 -9", OffsetMax = "9 9"
							},
							Image = "assets/icons/close.png",
							Material = "assets/content/ui/namefontmaterial.mat",
						}
					},
					
					ContentPanel = new KitsPanelContentUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 0",
							AnchorMax = "1 1",
							OffsetMin = "0 0",
							OffsetMax = "0 -95",
							Color = IColor.CreateTransparent()
						},
						ButtonBack = new ButtonSettings()
						{
							AnchorMin = "0 0.5", AnchorMax = "0 0.5", 
							OffsetMin = "5 4", 
							OffsetMax = "35 34",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 14,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#696969", 30),
							Material = "assets/content/ui/namefontmaterial.mat"
						},
						ButtonNext = new ButtonSettings()
						{
							AnchorMin = "1 0.5", AnchorMax = "1 0.5",
							OffsetMin = "-35 4", 
							OffsetMax = "-5 34",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 14,
							Color = new IColor("#FFFFFF", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat"
						},
						ButtonCreateKit = new ButtonSettings()
						{
							AnchorMin = "1 0", AnchorMax = "1 0", 
							OffsetMin = "-120 6", OffsetMax = "-40 28",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#71B8ED", 100),
							ButtonColor = new IColor("#71B8ED", 5),
							Material = "assets/content/ui/namefontmaterial.mat"
						},
						CheckboxShowAllKits = new CheckBoxSettings()
						{
							Background = new ImageSettings
							{
								AnchorMin = "0 0", 
								AnchorMax = "0 0",
								OffsetMin = "40 10", 
								OffsetMax = "60 30",
								Color = IColor.CreateTransparent()
							},
							CheckboxButton = new ButtonSettings
							{
								AnchorMin = "0 0.5", AnchorMax = "0 0.5",
								OffsetMin = "0 -7", OffsetMax = "14 7",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 10,
								Color = IColor.CreateWhite(),
								ButtonColor = IColor.CreateTransparent(),
							},
							CheckboxColor = new IColor("#38393F"),
							CheckboxSize = 3f,
							Title = new TextSettings
							{
								AnchorMin = "1 0",
								AnchorMax = "1 1",
								OffsetMin = "4 -4",
								OffsetMax = "104 4",
								IsBold = true, FontSize = 10, Align = TextAnchor.MiddleLeft,
								Color = IColor.CreateWhite()
							}
						},
					},
					
					KitPanel = new KitsPanelKitUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "0 0", OffsetMax = "0 0",
							Color = new IColor("#696969", 10),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd",
						},
						ShowName = true,
						KitName = new TextSettings()
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "10 -20", OffsetMax = "-10 0",
							Align = TextAnchor.MiddleLeft,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#E2DBD3", 100),
						},
						ShowNumber = false,
						KitNumber = new TextSettings()
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-60 -155", OffsetMax = "60 -35",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 60,
							Color = new IColor("#E2DBD3", 20),
						},
						KitImage = new InterfacePosition
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-60 -155", OffsetMax = "60 -35",
						},
						KitButtonTake = new ButtonSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						KitButtonTakeWhenShowInfo = new ButtonSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						KitButtonInfo = new ButtonSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "125 10", OffsetMax = "-10 38",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 13,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#F19F39", 60),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						DisplayKitContents = true,
						Contents = new ContentsSettings
						{
							Size = 28,
							Margin = 2,
							UpIndent = 20,
							ItemBackground = new ImageSettings
							{
								AnchorMin = "0.5 1",
								AnchorMax = "0.5 1",
								OffsetMin = "0 0",
								OffsetMax = "0 0",
								Color = new IColor("#696969", 30)
							},
							ItemImage = InterfacePosition.CreatePosition("0 0", "1 1", "2 2", "-2 -2"),
							ItemAmount = new TextSettings()
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "0 0", OffsetMax = "-2 -2",
								Align = TextAnchor.LowerRight,
								IsBold = true,
								FontSize = 6,
								Color = IColor.CreateWhite()
							},
							KitCooldown = new ButtonSettings
							{
								AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 14,
								Color = new IColor("#D1CBC4"),
								ButtonColor = new IColor("#696969", 30),
								Material = "assets/content/ui/namefontmaterial.mat",
								Sprite = "assets/content/ui/UI.Background.Tile.psd"
							},
							KitCooldownWithAmount = new ButtonSettings
							{
								AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 12,
								Color = new IColor("#D1CBC4"),
								ButtonColor = new IColor("#696969", 30),
								Material = "assets/content/ui/namefontmaterial.mat",
								Sprite = "assets/content/ui/UI.Background.Tile.psd"
							},
							KitAmount = new TextSettings
							{
								AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -20", OffsetMax = "-10 0",
								IsBold = true,
								Color = new IColor("#D1CBC4"),
								FontSize = 12, 
								Align = TextAnchor.MiddleRight
							},
							KitSale = new ButtonSettings
							{
								AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 12,
								Color = new IColor("#E2DBD3", 100),
								ButtonColor = new IColor("#D74933", 100),
								Material = "assets/content/ui/namefontmaterial.mat",
								Sprite = "assets/content/ui/UI.Background.Tile.psd"
							}
						}
					},
					
					Categories = new KitsCategoryUI
					{
						Background = InterfacePosition.CreatePosition("0 1", "1 1", "40 -90", "-40 -60"),
						Height = 25,
						Width = 100,
						Margin = 5,
						CategoriesScrollMinWidth = 800,
						ActiveCategory = new ButtonSettings
						{
							ButtonColor = new IColor("#CF432D", 80),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd",
							Color = new IColor("#E2DBD3", 100),
							IsBold = true,
							FontSize = 14,
							Align = TextAnchor.MiddleCenter
						},
						InactiveCategory = new ButtonSettings
						{
							ButtonColor = new IColor("#696969", 30),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd",
							Color = new IColor("#E2DBD3", 90),
							IsBold = true,
							FontSize = 14,
							Align = TextAnchor.MiddleCenter
						},
					},
					
					ColorOne = new IColor("#E2DBD3", 100),
					ColorTwo = new IColor("#D74933", 100),
					ColorThree = new IColor("#8B8B8B", 15),
					ColorFour = new IColor("#38393F", 50),
					ColorFive = new IColor("#E2DBD3", 20),
					ColorRed = new IColor("#FF4B4B", 100),
					ColorWhite = new IColor("#FFFFFF", 100),
					ColorBackground = new IColor("#000000", 0),
				};
			}

			public static UserInterface GenerateMenuTemplateRustV1()
			{
				return new UserInterface()
				{
					Style = InterfaceStyle.NewRust,
					Height = 455,
					Width = 640,
					KitHeight = 238,
					KitWidth = 160,
					Margin = 16,
					KitsOnString = 7,
					Strings = 2,
					LeftIndent = 0,
					YIndent = 0,
					KitAvailable = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
					},
					KitAmount = new KitAmountSettings
					{
						AnchorMin = "0.5 0",
						AnchorMax = "0.5 0",
						OffsetMin = "0",
						OffsetMax = "4",
						Width = 140
					},
					KitCooldown = new InterfacePosition
					{
						AnchorMin = "0 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "0 -10",
						OffsetMax = "0 20"
					},
					KitSale = new InterfacePosition
					{
						AnchorMin = "0 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "0 -10",
						OffsetMax = "0 20"
					},
					KitAmountCooldown = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", 
						OffsetMin = "0 4", OffsetMax = "0 0"
					},
					NoPermission = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
					},
					CloseAfterReceive = true,
					Logo = new LogoConf
					{
						AnchorMin = "0 0.5",
						AnchorMax = "0 0.5",
						OffsetMin = "10 -20",
						OffsetMax = "50 20",
						Enabled = false,
						Image = string.Empty
					},
					HeaderPanel = new KitsPanelHeaderUI()
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 1", AnchorMax = "1 1", 
							OffsetMin = "40 -50", OffsetMax = "-40 0",
							Color = IColor.CreateTransparent()
						},
						Title = new TextSettings()
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -50", OffsetMax = "-5 0",
							IsBold = true,
							FontSize = 0,
							Align = TextAnchor.UpperLeft,
							Color = IColor.CreateTransparent()
						},
						ShowLine = false,
						Line = new(),
						ButtonClose = new(),
					},
					ContentPanel = new KitsPanelContentUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 0.5", AnchorMax = "1 0.5",
							OffsetMin = "32 -250", OffsetMax = "-32 250",
							Color = IColor.CreateTransparent()
						},
						ButtonBack = new ButtonSettings()
						{
							AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-65 6", OffsetMax = "-35 36",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#38393F", 100)
						},
						ButtonNext = new ButtonSettings()
						{
							AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-30 6", OffsetMax = "0 36",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#FFFFFF", 100),
							ButtonColor = new IColor("#D74933", 100)
						},
						ButtonCreateKit = new ButtonSettings()
						{
							AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-160 6", OffsetMax = "-70 36",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 14,
							Color = new IColor("#71B8ED", 100),
							ButtonColor = new IColor("#71B8ED", 20),
						},
						CheckboxShowAllKits = new CheckBoxSettings()
						{
							Background = new ImageSettings
							{
								AnchorMin = "0 1", 
								AnchorMax = "0 1",
								OffsetMin = "0 10", 
								OffsetMax = "22 32",
								Color = IColor.CreateTransparent()
							},
							CheckboxButton = new ButtonSettings
							{
								AnchorMin = "0 0.5", AnchorMax = "0 0.5",
								OffsetMin = "5 -7", OffsetMax = "19 7",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 10,
								Color = IColor.CreateWhite(),
								ButtonColor = IColor.CreateTransparent(),
							},
							CheckboxColor = new IColor("#38393F"),
							CheckboxSize = 3f,
							Title = new TextSettings
							{
								AnchorMin = "1 0",
								AnchorMax = "1 1",
								OffsetMin = "4 -4",
								OffsetMax = "104 4",
								IsBold = true, FontSize = 10, Align = TextAnchor.MiddleLeft,
								Color = IColor.CreateWhite()
							}
						},
					},
					
					KitPanel = new KitsPanelKitUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "0 0", OffsetMax = "0 0",
							Color = new IColor("#38393F", 40),
						},
						ShowName = true,
						KitName = new TextSettings()
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "10 -35", OffsetMax = "-10 -5",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 18,
							Color = new IColor("#E2DBD3", 100),
						},
						ShowNumber = true,
						KitNumber = new TextSettings()
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-60 -155", OffsetMax = "60 -35",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 60,
							Color = new IColor("#E2DBD3", 20),
						},
						KitImage = new InterfacePosition
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-60 -155", OffsetMax = "60 -35"
						},
						KitButtonTake = new ButtonSettings
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-70 10", OffsetMax = "70 38",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 13,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						KitButtonTakeWhenShowInfo = new ButtonSettings
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-70 10", OffsetMax = "40 38",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 13,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						KitButtonInfo = new ButtonSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "125 10", OffsetMax = "-10 38",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 13,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#F19F39", 60),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						DisplayKitContents = false,
						Contents = new ContentsSettings()
					},
					Categories = new KitsCategoryUI(),
					ColorOne = new IColor("#E2DBD3", 100),
					ColorTwo = new IColor("#D74933", 100),
					ColorThree = new IColor("#8B8B8B", 15),
					ColorFour = new IColor("#38393F", 50),
					ColorFive = new IColor("#E2DBD3", 20),
					ColorRed = new IColor("#FF4B4B", 100),
					ColorWhite = new IColor("#FFFFFF", 100),
					ColorBackground = new IColor("#000000", 0),
				};
			}

			public static UserInterface GenerateMenuTemplateRustWithCategoriesV1()
			{
				return new UserInterface()
				{
					Style = InterfaceStyle.NewRust,
					Height = 455,
					Width = 640,
					KitHeight = 238,
					KitWidth = 160,
					Margin = 14,
					KitsOnString = 7,
					Strings = 2,
					LeftIndent = 0,
					YIndent = -4,
					ShowCategories = true,
					KitAvailable = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
					},
					KitAmount = new KitAmountSettings
					{
						AnchorMin = "0.5 0",
						AnchorMax = "0.5 0",
						OffsetMin = "0",
						OffsetMax = "4",
						Width = 140
					},
					KitCooldown = new InterfacePosition
					{
						AnchorMin = "0 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "0 -10",
						OffsetMax = "0 20"
					},
					KitSale = new InterfacePosition
					{
						AnchorMin = "0 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "0 -10",
						OffsetMax = "0 20"
					},
					KitAmountCooldown = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", 
						OffsetMin = "0 4", OffsetMax = "0 0"
					},
					NoPermission = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
					},
					CloseAfterReceive = true,
					Logo = new LogoConf
					{
						AnchorMin = "0 0.5",
						AnchorMax = "0 0.5",
						OffsetMin = "10 -20",
						OffsetMax = "50 20",
						Enabled = false,
						Image = string.Empty
					},
					HeaderPanel = new KitsPanelHeaderUI()
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 1", AnchorMax = "1 1", 
							OffsetMin = "40 -50", OffsetMax = "-40 0",
							Color = IColor.CreateTransparent()
						},
						Title = new TextSettings()
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -50", OffsetMax = "-5 0",
							IsBold = true,
							FontSize = 0,
							Align = TextAnchor.UpperLeft,
							Color = IColor.CreateTransparent()
						},
						ShowLine = false,
						Line = new(),
						ButtonClose = new(),
					},
					
					ContentPanel = new KitsPanelContentUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 0.5", AnchorMax = "1 0.5",
							OffsetMin = "40 -250", OffsetMax = "-40 220",
							Color = IColor.CreateTransparent()
						},
						ButtonBack = new ButtonSettings()
						{
							AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-65 0", OffsetMax = "-35 20",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#38393F", 100)
						},
						ButtonNext = new ButtonSettings()
						{
							AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-30 0", OffsetMax = "0 20",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#FFFFFF", 100),
							ButtonColor = new IColor("#D74933", 100)
						},
						ButtonCreateKit = new ButtonSettings()
						{
							AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-160 0", OffsetMax = "-70 20",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#71B8ED", 100),
							ButtonColor = new IColor("#71B8ED", 20),
						},
						CheckboxShowAllKits = new CheckBoxSettings()
						{
							Background = new ImageSettings
							{
								AnchorMin = "0 1", 
								AnchorMax = "0 1",
								OffsetMin = "0 4", 
								OffsetMax = "22 16",
								Color = IColor.CreateTransparent()
							},
							CheckboxButton = new ButtonSettings
							{
								AnchorMin = "0 0.5", AnchorMax = "0 0.5",
								OffsetMin = "5 -7", OffsetMax = "19 7",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 10,
								Color = IColor.CreateWhite(),
								ButtonColor = IColor.CreateTransparent(),
							},
							CheckboxColor = new IColor("#38393F"),
							CheckboxSize = 3f,
							Title = new TextSettings
							{
								AnchorMin = "1 0",
								AnchorMax = "1 1",
								OffsetMin = "4 -4",
								OffsetMax = "104 4",
								IsBold = true, FontSize = 10, Align = TextAnchor.MiddleLeft,
								Color = IColor.CreateWhite()
							}
						},
					},
					
					KitPanel = new KitsPanelKitUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "0 0", OffsetMax = "0 0",
							Color = new IColor("#38393F", 40),
						},
						ShowName = true,
						KitName = new TextSettings()
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "10 -35", OffsetMax = "-10 -5",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 18,
							Color = new IColor("#E2DBD3", 100),
						},
						ShowNumber = true,
						KitNumber = new TextSettings()
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-60 -155", OffsetMax = "60 -35",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 60,
							Color = new IColor("#E2DBD3", 20),
						},
						KitImage = new InterfacePosition
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-60 -155", OffsetMax = "60 -35"
						},
						KitButtonTake = new ButtonSettings
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-70 10", OffsetMax = "70 38",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 13,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						KitButtonTakeWhenShowInfo = new ButtonSettings
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-70 10", OffsetMax = "40 38",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 13,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						KitButtonInfo = new ButtonSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "125 10", OffsetMax = "-10 38",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 13,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#F19F39", 60),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						DisplayKitContents = false,
						Contents = new ContentsSettings()
					},
					
					Categories = new KitsCategoryUI
					{
						Background = InterfacePosition.CreatePosition("0 1", "1 1", "40 -30", "-40 0"),
						Height = 24,
						Width = 100,
						Margin = 5,
						CategoriesScrollMinWidth = 1200,
						ActiveCategory = new ButtonSettings
						{
							ButtonColor = new IColor("#CF432D", 80),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd",
							Color = new IColor("#E2DBD3", 100),
							IsBold = true,
							FontSize = 14,
							Align = TextAnchor.MiddleCenter
						},
						InactiveCategory = new ButtonSettings
						{
							ButtonColor = new IColor("#696969", 30),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd",
							Color = new IColor("#E2DBD3", 90),
							IsBold = true,
							FontSize = 14,
							Align = TextAnchor.MiddleCenter
						},
					},

					ColorOne = new IColor("#E2DBD3", 100),
					ColorTwo = new IColor("#D74933", 100),
					ColorThree = new IColor("#8B8B8B", 15),
					ColorFour = new IColor("#38393F", 50),
					ColorFive = new IColor("#E2DBD3", 20),
					ColorRed = new IColor("#FF4B4B", 100),
					ColorWhite = new IColor("#FFFFFF", 100),
					ColorBackground = new IColor("#000000", 0),
				};
			}

			public static UserInterface GenerateMenuTemplateRustWithContentsV1()
			{
				return new UserInterface()
				{
					Style = InterfaceStyle.NewRust,
					Height = 455,
					Width = 640,
					KitHeight = 236,
					KitWidth = 188,
					// KitHeight = 238,
					// KitWidth = 160,
					Margin = 16,
					KitsOnString = 6,
					Strings = 2,
					LeftIndent = 0,
					YIndent = 0,
					KitAvailable = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
					},
					KitAmount = new KitAmountSettings
					{
						AnchorMin = "0.5 0",
						AnchorMax = "0.5 0",
						OffsetMin = "0",
						OffsetMax = "4",
						Width = 140
					},
					KitCooldown = new InterfacePosition
					{
						AnchorMin = "0 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "0 -10",
						OffsetMax = "0 20"
					},
					KitSale = new InterfacePosition
					{
						AnchorMin = "0 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "0 -10",
						OffsetMax = "0 20"
					},
					KitAmountCooldown = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", 
						OffsetMin = "0 4", OffsetMax = "0 0"
					},
					NoPermission = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
					},
					CloseAfterReceive = true,
					Logo = new LogoConf
					{
						AnchorMin = "0 0.5",
						AnchorMax = "0 0.5",
						OffsetMin = "10 -20",
						OffsetMax = "50 20",
						Enabled = false,
						Image = string.Empty
					},
					HeaderPanel = new KitsPanelHeaderUI()
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 1", AnchorMax = "1 1", 
							OffsetMin = "40 -50", OffsetMax = "-40 0",
							Color = IColor.CreateTransparent()
						},
						Title = new TextSettings()
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -50", OffsetMax = "-5 0",
							IsBold = true,
							FontSize = 0,
							Align = TextAnchor.UpperLeft,
							Color = IColor.CreateTransparent()
						},
						ShowLine = false,
						Line = new(),
						ButtonClose = new(),
					},
					
					ContentPanel = new KitsPanelContentUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 0.5", AnchorMax = "1 0.5",
							OffsetMin = "32 -250", OffsetMax = "-32 260",
							Color = IColor.CreateTransparent()
						},
						ButtonBack = new ButtonSettings()
						{
							AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-65 6", OffsetMax = "-35 36",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#38393F", 100)
						},
						ButtonNext = new ButtonSettings()
						{
							AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-30 6", OffsetMax = "0 36",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#FFFFFF", 100),
							ButtonColor = new IColor("#D74933", 100)
						},
						ButtonCreateKit = new ButtonSettings()
						{
							AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-160 6", OffsetMax = "-70 36",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 14,
							Color = new IColor("#71B8ED", 100),
							ButtonColor = new IColor("#71B8ED", 20),
						},
						CheckboxShowAllKits = new CheckBoxSettings()
						{
							Background = new ImageSettings
							{
								AnchorMin = "0 1", 
								AnchorMax = "0 1",
								OffsetMin = "0 10", 
								OffsetMax = "22 32",
								Color = IColor.CreateTransparent()
							},
							CheckboxButton = new ButtonSettings
							{
								AnchorMin = "0 0.5", AnchorMax = "0 0.5",
								OffsetMin = "5 -7", OffsetMax = "19 7",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 10,
								Color = IColor.CreateWhite(),
								ButtonColor = IColor.CreateTransparent(),
							},
							CheckboxColor = new IColor("#38393F"),
							CheckboxSize = 3f,
							Title = new TextSettings
							{
								AnchorMin = "1 0",
								AnchorMax = "1 1",
								OffsetMin = "4 -4",
								OffsetMax = "104 4",
								IsBold = true, FontSize = 10, Align = TextAnchor.MiddleLeft,
								Color = IColor.CreateWhite()
							}
						},
					},
					
					KitPanel = new KitsPanelKitUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "0 0", OffsetMax = "0 0",
							Color = new IColor("#38393F", 40),
						},
						ShowName = true,
						KitName = new TextSettings()
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "10 -20", OffsetMax = "-10 0",
							Align = TextAnchor.MiddleLeft,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#E2DBD3", 100),
						},
						ShowNumber = false,
						KitNumber = new TextSettings(),
						KitImage = new InterfacePosition
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-60 -155", OffsetMax = "60 -35"
						},
						KitButtonTake = new ButtonSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						KitButtonTakeWhenShowInfo = new ButtonSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						KitButtonInfo = new ButtonSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "125 10", OffsetMax = "-10 38",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 13,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#F19F39", 60),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						DisplayKitContents = true,
						Contents = new ContentsSettings
						{
							Size = 28,
							Margin = 2,
							UpIndent = 20,
							ItemBackground = new ImageSettings
							{
								AnchorMin = "0.5 1",
								AnchorMax = "0.5 1",
								OffsetMin = "0 0",
								OffsetMax = "0 0",
								Color = new IColor("#696969", 30)
							},
							ItemImage = InterfacePosition.CreatePosition("0 0", "1 1", "2 2", "-2 -2"),
							ItemAmount = new TextSettings()
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "0 0", OffsetMax = "-2 -2",
								Align = TextAnchor.LowerRight,
								IsBold = true,
								FontSize = 6,
								Color = IColor.CreateWhite()
							},
							KitCooldown = new ButtonSettings
							{
								AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 14,
								Color = new IColor("#D1CBC4"),
								ButtonColor = new IColor("#696969", 30),
								Material = "assets/content/ui/namefontmaterial.mat",
								Sprite = "assets/content/ui/UI.Background.Tile.psd"
							},
							KitCooldownWithAmount = new ButtonSettings
							{
								AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 12,
								Color = new IColor("#D1CBC4"),
								ButtonColor = new IColor("#696969", 30),
								Material = "assets/content/ui/namefontmaterial.mat",
								Sprite = "assets/content/ui/UI.Background.Tile.psd"
							},
							KitAmount = new TextSettings
							{
								AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -20", OffsetMax = "-10 0",
								IsBold = true,
								Color = new IColor("#D1CBC4"),
								FontSize = 12, 
								Align = TextAnchor.MiddleRight
							},
							KitSale = new ButtonSettings
							{
								AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 12,
								Color = new IColor("#E2DBD3", 100),
								ButtonColor = new IColor("#D74933", 100),
								Material = "assets/content/ui/namefontmaterial.mat",
								Sprite = "assets/content/ui/UI.Background.Tile.psd"
							}
						}
					},
					Categories = new KitsCategoryUI(),
					ColorOne = new IColor("#E2DBD3", 100),
					ColorTwo = new IColor("#D74933", 100),
					ColorThree = new IColor("#8B8B8B", 15),
					ColorFour = new IColor("#38393F", 50),
					ColorFive = new IColor("#E2DBD3", 20),
					ColorRed = new IColor("#FF4B4B", 100),
					ColorWhite = new IColor("#FFFFFF", 100),
					ColorBackground = new IColor("#000000", 0),
				};
			}

			public static UserInterface GenerateMenuTemplateRustWithContentsAndCategoriesV1()
			{
				return new UserInterface()
				{
					Style = InterfaceStyle.NewRust,
					Height = 455,
					Width = 640,
					KitHeight = 236,
					KitWidth = 188,
					// KitHeight = 238,
					// KitWidth = 160,
					Margin = 16,
					KitsOnString = 6,
					Strings = 2,
					LeftIndent = 0,
					YIndent = -25,
					ShowCategories = true,
					KitAvailable = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
					},
					KitAmount = new KitAmountSettings
					{
						AnchorMin = "0.5 0",
						AnchorMax = "0.5 0",
						OffsetMin = "0",
						OffsetMax = "4",
						Width = 140
					},
					KitCooldown = new InterfacePosition
					{
						AnchorMin = "0 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "0 -10",
						OffsetMax = "0 20"
					},
					KitSale = new InterfacePosition
					{
						AnchorMin = "0 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "0 -10",
						OffsetMax = "0 20"
					},
					KitAmountCooldown = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", 
						OffsetMin = "0 4", OffsetMax = "0 0"
					},
					NoPermission = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
					},
					CloseAfterReceive = true,
					Logo = new LogoConf
					{
						AnchorMin = "0 0.5",
						AnchorMax = "0 0.5",
						OffsetMin = "10 -20",
						OffsetMax = "50 20",
						Enabled = false,
						Image = string.Empty
					},
					HeaderPanel = new KitsPanelHeaderUI()
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 1", AnchorMax = "1 1", 
							OffsetMin = "40 -50", OffsetMax = "-40 0",
							Color = IColor.CreateTransparent()
						},
						Title = new TextSettings()
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -50", OffsetMax = "-5 0",
							IsBold = true,
							FontSize = 0,
							Align = TextAnchor.UpperLeft,
							Color = IColor.CreateTransparent()
						},
						ShowLine = false,
						Line = new(),
						ButtonClose = new(),
					},
					
					ContentPanel = new KitsPanelContentUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 0.5", AnchorMax = "1 0.5",
							OffsetMin = "40 -250", OffsetMax = "-40 250",
							Color = IColor.CreateTransparent()
						},
						ButtonBack = new ButtonSettings()
						{
							AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-65 -20", OffsetMax = "-35 0",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#38393F", 100)
						},
						ButtonNext = new ButtonSettings()
						{
							AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-30 -20", OffsetMax = "0 0",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#FFFFFF", 100),
							ButtonColor = new IColor("#D74933", 100)
						},
						ButtonCreateKit = new ButtonSettings()
						{
							AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-160 -20", OffsetMax = "-70 0",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#71B8ED", 100),
							ButtonColor = new IColor("#71B8ED", 20),
						},
						CheckboxShowAllKits = new CheckBoxSettings()
						{
							Background = new ImageSettings
							{
								AnchorMin = "0 1", 
								AnchorMax = "0 1",
								OffsetMin = "0 -16", 
								OffsetMax = "22 -4",
								Color = IColor.CreateTransparent()
							},
							CheckboxButton = new ButtonSettings
							{
								AnchorMin = "0 0.5", AnchorMax = "0 0.5",
								OffsetMin = "5 -7", OffsetMax = "19 7",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 10,
								Color = IColor.CreateWhite(),
								ButtonColor = IColor.CreateTransparent(),
							},
							CheckboxColor = new IColor("#38393F"),
							CheckboxSize = 3f,
							Title = new TextSettings
							{
								AnchorMin = "1 0",
								AnchorMax = "1 1",
								OffsetMin = "4 -4",
								OffsetMax = "104 4",
								IsBold = true, FontSize = 10, Align = TextAnchor.MiddleLeft,
								Color = IColor.CreateWhite()
							}
						},
					},
					
					KitPanel = new KitsPanelKitUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "0 0", OffsetMax = "0 0",
							Color = new IColor("#38393F", 40),
						},
						ShowName = true,
						KitName = new TextSettings()
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "10 -20", OffsetMax = "-10 0",
							Align = TextAnchor.MiddleLeft,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#E2DBD3", 100),
						},
						ShowNumber = false,
						KitNumber = new TextSettings(),
						KitImage = new InterfacePosition
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-60 -155", OffsetMax = "60 -35"
						},
						KitButtonTake = new ButtonSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						KitButtonTakeWhenShowInfo = new ButtonSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						KitButtonInfo = new ButtonSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "125 10", OffsetMax = "-10 38",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 13,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#F19F39", 60),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						DisplayKitContents = true,
						Contents = new ContentsSettings
						{
							Size = 28,
							Margin = 2,
							UpIndent = 20,
							ItemBackground = new ImageSettings
							{
								AnchorMin = "0.5 1",
								AnchorMax = "0.5 1",
								OffsetMin = "0 0",
								OffsetMax = "0 0",
								Color = new IColor("#696969", 30)
							},
							ItemImage = InterfacePosition.CreatePosition("0 0", "1 1", "2 2", "-2 -2"),
							ItemAmount = new TextSettings()
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "0 0", OffsetMax = "-2 -2",
								Align = TextAnchor.LowerRight,
								IsBold = true,
								FontSize = 6,
								Color = IColor.CreateWhite()
							},
							KitCooldown = new ButtonSettings
							{
								AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 14,
								Color = new IColor("#D1CBC4"),
								ButtonColor = new IColor("#696969", 30),
								Material = "assets/content/ui/namefontmaterial.mat",
								Sprite = "assets/content/ui/UI.Background.Tile.psd"
							},
							KitCooldownWithAmount = new ButtonSettings
							{
								AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 12,
								Color = new IColor("#D1CBC4"),
								ButtonColor = new IColor("#696969", 30),
								Material = "assets/content/ui/namefontmaterial.mat",
								Sprite = "assets/content/ui/UI.Background.Tile.psd"
							},
							KitAmount = new TextSettings
							{
								AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -20", OffsetMax = "-10 0",
								IsBold = true,
								Color = new IColor("#D1CBC4"),
								FontSize = 12, 
								Align = TextAnchor.MiddleRight
							},
							KitSale = new ButtonSettings
							{
								AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 12,
								Color = new IColor("#E2DBD3", 100),
								ButtonColor = new IColor("#D74933", 100),
								Material = "assets/content/ui/namefontmaterial.mat",
								Sprite = "assets/content/ui/UI.Background.Tile.psd"
							}
						}
					},
					
					Categories = new KitsCategoryUI
					{
						Background = InterfacePosition.CreatePosition("0 1", "1 1", "40 -20", "-40 10"),
						Height = 24,
						Width = 100,
						Margin = 5,
						CategoriesScrollMinWidth = 1200,
						ActiveCategory = new ButtonSettings
						{
							ButtonColor = new IColor("#CF432D", 80),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd",
							Color = new IColor("#E2DBD3", 100),
							IsBold = true,
							FontSize = 14,
							Align = TextAnchor.MiddleCenter
						},
						InactiveCategory = new ButtonSettings
						{
							ButtonColor = new IColor("#696969", 30),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd",
							Color = new IColor("#E2DBD3", 90),
							IsBold = true,
							FontSize = 14,
							Align = TextAnchor.MiddleCenter
						},
					},
					ColorOne = new IColor("#E2DBD3", 100),
					ColorTwo = new IColor("#D74933", 100),
					ColorThree = new IColor("#8B8B8B", 15),
					ColorFour = new IColor("#38393F", 50),
					ColorFive = new IColor("#E2DBD3", 20),
					ColorRed = new IColor("#FF4B4B", 100),
					ColorWhite = new IColor("#FFFFFF", 100),
					ColorBackground = new IColor("#000000", 0),
				};
			}

			public static UserInterface GenerateMenuTemplateRustV2()
			{
				return new UserInterface()
				{
					Style = InterfaceStyle.NewRust,
					Height = 455,
					Width = 640,
					KitHeight = 238,
					KitWidth = 160,
					Margin = 16,
					KitsOnString = 5,
					Strings = 2,
					LeftIndent = 0,
					YIndent = 0,
					KitAvailable = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
					},
					KitAmount = new KitAmountSettings
					{
						AnchorMin = "0.5 0",
						AnchorMax = "0.5 0",
						OffsetMin = "0",
						OffsetMax = "4",
						Width = 140
					},
					KitCooldown = new InterfacePosition
					{
						AnchorMin = "0 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "0 -10",
						OffsetMax = "0 20"
					},
					KitSale = new InterfacePosition
					{
						AnchorMin = "0 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "0 -10",
						OffsetMax = "0 20"
					},
					KitAmountCooldown = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", 
						OffsetMin = "0 4", OffsetMax = "0 0"
					},
					NoPermission = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
					},
					CloseAfterReceive = true,
					Logo = new LogoConf
					{
						AnchorMin = "0 0.5",
						AnchorMax = "0 0.5",
						OffsetMin = "10 -20",
						OffsetMax = "50 20",
						Enabled = false,
						Image = string.Empty
					},
					HeaderPanel = new KitsPanelHeaderUI()
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 1", AnchorMax = "1 1", 
							OffsetMin = "40 -70", OffsetMax = "-40 -20",
							Color = IColor.CreateTransparent()
						},
						Title = new TextSettings()
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -50", OffsetMax = "-5 0",
							IsBold = true,
							FontSize = 32,
							Align = TextAnchor.UpperLeft,
							Color = new IColor("#CF432D", 90),
						},
						ShowLine = true,
						Line = new ImageSettings
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -51", OffsetMax = "-42 -49",
							Color = new IColor("#373737", 50)
                        },
						ButtonClose = new ButtonSettings
						{
							AnchorMin = "1 1", AnchorMax = "1 1",
							OffsetMin = "-35 -37.5",
							OffsetMax = "-10 -12.5",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 10,
							Color = IColor.CreateWhite(),
							ButtonColor = new IColor("#4B68FF", 100),
						}
					},
					ContentPanel = new KitsPanelContentUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 0.5", AnchorMax = "1 0.5",
							OffsetMin = "40 -290", OffsetMax = "-40 200",
							Color = IColor.CreateTransparent()
						},
						ButtonBack = new ButtonSettings()
						{
							AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-87 6", OffsetMax = "-57 36",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#38393F", 100)
						},
						ButtonNext = new ButtonSettings()
						{
							AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-52 6", OffsetMax = "-22 36",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#FFFFFF", 100),
							ButtonColor = new IColor("#D74933", 100)
						},
						ButtonCreateKit = new ButtonSettings()
						{
							AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-182 6", OffsetMax = "-92 36",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 14,
							Color = new IColor("#71B8ED", 100),
							ButtonColor = new IColor("#71B8ED", 20),
						},
						CheckboxShowAllKits = new CheckBoxSettings()
						{
							Background = new ImageSettings
							{
								AnchorMin = "0 1", 
								AnchorMax = "0 1",
								OffsetMin = "0 10", 
								OffsetMax = "22 32",
								Color = IColor.CreateTransparent()
							},
							CheckboxButton = new ButtonSettings
							{
								AnchorMin = "0 0.5", AnchorMax = "0 0.5",
								OffsetMin = "5 -7", OffsetMax = "19 7",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 10,
								Color = IColor.CreateWhite(),
								ButtonColor = IColor.CreateTransparent(),
							},
							CheckboxColor = new IColor("#38393F"),
							CheckboxSize = 3f,
							Title = new TextSettings
							{
								AnchorMin = "1 0",
								AnchorMax = "1 1",
								OffsetMin = "4 -4",
								OffsetMax = "104 4",
								IsBold = true, FontSize = 10, Align = TextAnchor.MiddleLeft,
								Color = IColor.CreateWhite()
							}
						},
					},
					
					KitPanel = new KitsPanelKitUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "0 0", OffsetMax = "0 0",
							Color = new IColor("#38393F", 40),
						},
						ShowName = true,
						KitName = new TextSettings()
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "10 -35", OffsetMax = "-10 -5",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 18,
							Color = new IColor("#E2DBD3", 100),
						},
						ShowNumber = true,
						KitNumber = new TextSettings()
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-60 -155", OffsetMax = "60 -35",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 60,
							Color = new IColor("#E2DBD3", 20),
						},
						KitImage = new InterfacePosition
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-60 -155", OffsetMax = "60 -35"
						},
						KitButtonTake = new ButtonSettings
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-70 10", OffsetMax = "70 38",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 13,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						KitButtonTakeWhenShowInfo = new ButtonSettings
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-70 10", OffsetMax = "40 38",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 13,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						KitButtonInfo = new ButtonSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "125 10", OffsetMax = "-10 38",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 13,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#F19F39", 60),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						DisplayKitContents = false,
						Contents = new ContentsSettings()
					},
					Categories = new KitsCategoryUI(),
					ColorOne = new IColor("#E2DBD3", 100),
					ColorTwo = new IColor("#D74933", 100),
					ColorThree = new IColor("#8B8B8B", 15),
					ColorFour = new IColor("#38393F", 50),
					ColorFive = new IColor("#E2DBD3", 20),
					ColorRed = new IColor("#FF4B4B", 100),
					ColorWhite = new IColor("#FFFFFF", 100),
					ColorBackground = new IColor("#000000", 0),
				};
			}

			public static UserInterface GenerateMenuTemplateRustWithCategoriesV2()
			{
				return new UserInterface()
				{
					Style = InterfaceStyle.NewRust,
					Height = 455,
					Width = 640,
					KitHeight = 238,
					KitWidth = 160,
					Margin = 5,
					KitsOnString = 5,
					Strings = 2,
					LeftIndent = 0,
					YIndent = -24,
					ShowCategories = true,
					KitAvailable = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
					},
					KitAmount = new KitAmountSettings
					{
						AnchorMin = "0.5 0",
						AnchorMax = "0.5 0",
						OffsetMin = "0",
						OffsetMax = "4",
						Width = 140
					},
					KitCooldown = new InterfacePosition
					{
						AnchorMin = "0 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "0 -10",
						OffsetMax = "0 20"
					},
					KitSale = new InterfacePosition
					{
						AnchorMin = "0 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "0 -10",
						OffsetMax = "0 20"
					},
					KitAmountCooldown = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", 
						OffsetMin = "0 4", OffsetMax = "0 0"
					},
					NoPermission = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
					},
					CloseAfterReceive = true,
					Logo = new LogoConf
					{
						AnchorMin = "0 0.5",
						AnchorMax = "0 0.5",
						OffsetMin = "10 -20",
						OffsetMax = "50 20",
						Enabled = false,
						Image = string.Empty
					},
					HeaderPanel = new KitsPanelHeaderUI()
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 1", AnchorMax = "1 1", 
							OffsetMin = "40 -70", OffsetMax = "-40 -20",
							Color = IColor.CreateTransparent()
						},
						Title = new TextSettings()
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -50", OffsetMax = "-5 0",
							IsBold = true,
							FontSize = 32,
							Align = TextAnchor.UpperLeft,
							Color = new IColor("#CF432D", 90),
						},
						ShowLine = true,
						Line = new ImageSettings
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -51", OffsetMax = "0 -49",
							Color = new IColor("#373737", 50)
                        },
						ButtonClose = new ButtonSettings
						{
							AnchorMin = "1 1", AnchorMax = "1 1",
							OffsetMin = "-35 -37.5",
							OffsetMax = "-10 -12.5",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 10,
							Color = IColor.CreateWhite(),
							ButtonColor = new IColor("#4B68FF", 100),
						}
					},
					ContentPanel = new KitsPanelContentUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 0.5", AnchorMax = "1 0.5",
							OffsetMin = "40 -290", OffsetMax = "-40 200",
							Color = IColor.CreateTransparent()
						},
						ButtonBack = new ButtonSettings()
						{
							AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-65 -20", OffsetMax = "-35 0",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#38393F", 100)
						},
						ButtonNext = new ButtonSettings()
						{
							AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-30 -20", OffsetMax = "0 0",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#FFFFFF", 100),
							ButtonColor = new IColor("#D74933", 100)
						},
						ButtonCreateKit = new ButtonSettings()
						{
							AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-160 -20", OffsetMax = "-70 0",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#71B8ED", 100),
							ButtonColor = new IColor("#71B8ED", 20),
						},
						CheckboxShowAllKits = new CheckBoxSettings()
						{
							Background = new ImageSettings
							{
								AnchorMin = "0 1", 
								AnchorMax = "0 1",
								OffsetMin = "0 -20", 
								OffsetMax = "22 2",
								Color = IColor.CreateTransparent()
							},
							CheckboxButton = new ButtonSettings
							{
								AnchorMin = "0 0.5", AnchorMax = "0 0.5",
								OffsetMin = "5 -7", OffsetMax = "19 7",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 10,
								Color = IColor.CreateWhite(),
								ButtonColor = IColor.CreateTransparent(),
							},
							CheckboxColor = new IColor("#38393F"),
							CheckboxSize = 3f,
							Title = new TextSettings
							{
								AnchorMin = "1 0",
								AnchorMax = "1 1",
								OffsetMin = "4 -4",
								OffsetMax = "104 4",
								IsBold = true, FontSize = 10, Align = TextAnchor.MiddleLeft,
								Color = IColor.CreateWhite()
							}
						},
					},
					
					KitPanel = new KitsPanelKitUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "0 0", OffsetMax = "0 0",
							Color = new IColor("#38393F", 40),
						},
						ShowName = true,
						KitName = new TextSettings()
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "10 -35", OffsetMax = "-10 -5",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 18,
							Color = new IColor("#E2DBD3", 100),
						},
						ShowNumber = true,
						KitNumber = new TextSettings()
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-60 -155", OffsetMax = "60 -35",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 60,
							Color = new IColor("#E2DBD3", 20),
						},
						KitImage = new InterfacePosition
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-60 -155", OffsetMax = "60 -35"
						},
						KitButtonTake = new ButtonSettings
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-70 10", OffsetMax = "70 38",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 13,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						KitButtonTakeWhenShowInfo = new ButtonSettings
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-70 10", OffsetMax = "40 38",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 13,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						KitButtonInfo = new ButtonSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "125 10", OffsetMax = "-10 38",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 13,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#F19F39", 60),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						DisplayKitContents = false,
						Contents = new ContentsSettings()
					},
					
					Categories = new KitsCategoryUI
					{
						Background = InterfacePosition.CreatePosition("0 1", "1 1", "40 -110", "-37 -80"),
						Height = 24,
						Width = 100,
						Margin = 5,
						CategoriesScrollMinWidth = 890,
						ActiveCategory = new ButtonSettings
						{
							ButtonColor = new IColor("#CF432D", 50),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd",
							Color = new IColor("#E2DBD3", 100),
							IsBold = true,
							FontSize = 14,
							Align = TextAnchor.MiddleCenter
						},
						InactiveCategory = new ButtonSettings
						{
							ButtonColor = new IColor("#696969", 15),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd",
							Color = new IColor("#E2DBD3", 90),
							IsBold = true,
							FontSize = 14,
							Align = TextAnchor.MiddleCenter
						},
					},
					
					ColorOne = new IColor("#E2DBD3", 100),
					ColorTwo = new IColor("#D74933", 100),
					ColorThree = new IColor("#8B8B8B", 15),
					ColorFour = new IColor("#38393F", 50),
					ColorFive = new IColor("#E2DBD3", 20),
					ColorRed = new IColor("#FF4B4B", 100),
					ColorWhite = new IColor("#FFFFFF", 100),
					ColorBackground = new IColor("#000000", 0),
				};
			}

			public static UserInterface GenerateMenuTemplateRustWithContentsV2()
			{
				return new UserInterface()
				{
					Style = InterfaceStyle.NewRust,
					Height = 455,
					Width = 640,
					KitHeight = 236,
					KitWidth = 188,
					// KitHeight = 358,
					// KitWidth = 285,
					Margin = 16,
					KitsOnString = 4,
					Strings = 2,
					LeftIndent = 0,
					YIndent = 0,
					ShowCategories = false,
					KitAvailable = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
					},
					KitAmount = new KitAmountSettings
					{
						AnchorMin = "0.5 0",
						AnchorMax = "0.5 0",
						OffsetMin = "0",
						OffsetMax = "4",
						Width = 140
					},
					KitCooldown = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 15", OffsetMax = "-5 60"
					},
					KitSale = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 15", OffsetMax = "-5 60"
					},
					KitAmountCooldown = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "10 10", OffsetMax = "-10 38"
					},
					NoPermission = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
					},
					CloseAfterReceive = true,
					Logo = new LogoConf
					{
						AnchorMin = "0 0.5",
						AnchorMax = "0 0.5",
						OffsetMin = "10 -20",
						OffsetMax = "50 20",
						Enabled = false,
						Image = string.Empty
					},
					HeaderPanel = new KitsPanelHeaderUI()
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 1", AnchorMax = "1 1", 
							OffsetMin = "40 -70", OffsetMax = "-37 -20",
							Color = IColor.CreateTransparent()
						},
						Title = new TextSettings()
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -50", OffsetMax = "-5 0",
							IsBold = true,
							FontSize = 32,
							Align = TextAnchor.UpperLeft,
							Color = new IColor("#CF432D", 90),
						},
						ShowLine = true,
						Line = new ImageSettings
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -51", OffsetMax = "0 -49",
							Color = new IColor("#373737", 50)
                        },
						ButtonClose = new ButtonSettings
						{
							AnchorMin = "1 1", AnchorMax = "1 1",
							OffsetMin = "-35 -37.5",
							OffsetMax = "-10 -12.5",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 10,
							Color = IColor.CreateWhite(),
							ButtonColor = new IColor("#4B68FF", 100),
						}
					},
					ContentPanel = new KitsPanelContentUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 0.5", AnchorMax = "1 0.5",
							OffsetMin = "40 -300", OffsetMax = "-40 230",
							Color = IColor.CreateTransparent()
						},
						ButtonBack = new ButtonSettings()
						{
							AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-65 6", OffsetMax = "-35 36",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#38393F", 100)
						},
						ButtonNext = new ButtonSettings()
						{
							AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-30 6", OffsetMax = "0 36",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#FFFFFF", 100),
							ButtonColor = new IColor("#D74933", 100)
						},
						ButtonCreateKit = new ButtonSettings()
						{
							AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-160 6", OffsetMax = "-70 36",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 14,
							Color = new IColor("#71B8ED", 100),
							ButtonColor = new IColor("#71B8ED", 20),
						},
						CheckboxShowAllKits = new CheckBoxSettings()
						{
							Background = new ImageSettings
							{
								AnchorMin = "0 0", 
								AnchorMax = "0 0",
								OffsetMin = "0 10", 
								OffsetMax = "22 32",
								Color = IColor.CreateTransparent()
							},
							CheckboxButton = new ButtonSettings
							{
								AnchorMin = "0 0.5", AnchorMax = "0 0.5",
								OffsetMin = "5 -7", OffsetMax = "19 7",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 10,
								Color = IColor.CreateWhite(),
								ButtonColor = IColor.CreateTransparent(),
							},
							CheckboxColor = new IColor("#38393F"),
							CheckboxSize = 3f,
							Title = new TextSettings
							{
								AnchorMin = "1 0",
								AnchorMax = "1 1",
								OffsetMin = "4 -4",
								OffsetMax = "104 4",
								IsBold = true, FontSize = 10, Align = TextAnchor.MiddleLeft,
								Color = IColor.CreateWhite()
							}
						},
					},
					
					KitPanel = new KitsPanelKitUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "0 0", OffsetMax = "0 0",
							Color = new IColor("#38393F", 40),
						},
						ShowName = true,
						KitName = new TextSettings()
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "10 -20", OffsetMax = "-10 0",
							Align = TextAnchor.MiddleLeft,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#E2DBD3", 100),
						},
						ShowNumber = false,
						KitNumber = new TextSettings(),
						KitImage = new InterfacePosition
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-60 -155", OffsetMax = "60 -35"
						},
						KitButtonTake = new ButtonSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						KitButtonTakeWhenShowInfo = new ButtonSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						KitButtonInfo = new ButtonSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "125 10", OffsetMax = "-10 38",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 13,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#F19F39", 60),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						DisplayKitContents = true,
						Contents = new ContentsSettings
						{
							Size = 28,
							Margin = 2,
							UpIndent = 20,
							// Size = 40,
							// Margin = 5,
							// UpIndent = 35,
							ItemBackground = new ImageSettings
							{
								AnchorMin = "0.5 1",
								AnchorMax = "0.5 1",
								OffsetMin = "0 0",
								OffsetMax = "0 0",
								Color = new IColor("#696969", 30)
							},
							ItemImage = InterfacePosition.CreatePosition("0 0", "1 1", "2 2", "-2 -2"),
							ItemAmount = new TextSettings()
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "0 0", OffsetMax = "-2 -2",
								Align = TextAnchor.LowerRight,
								IsBold = true,
								FontSize = 6,
								Color = IColor.CreateWhite()
							},
							KitCooldown = new ButtonSettings
							{
								AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 14,
								Color = new IColor("#D1CBC4"),
								ButtonColor = new IColor("#696969", 30),
								Material = "assets/content/ui/namefontmaterial.mat",
								Sprite = "assets/content/ui/UI.Background.Tile.psd"
							},
							KitCooldownWithAmount = new ButtonSettings
							{
								AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 14,
								Color = new IColor("#D1CBC4"),
								ButtonColor = new IColor("#696969", 30),
								Material = "assets/content/ui/namefontmaterial.mat",
								Sprite = "assets/content/ui/UI.Background.Tile.psd"
							},
							KitAmount = new TextSettings
							{
								AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -20", OffsetMax = "-10 0",
								IsBold = true,
								Color = new IColor("#D1CBC4"),
								FontSize = 12, 
								Align = TextAnchor.MiddleRight
							},
							KitSale = new ButtonSettings
							{
								AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 12,
								Color = new IColor("#FFFFFF", 100),
								ButtonColor = new IColor("#D74933", 100),
								Material = "assets/content/ui/namefontmaterial.mat",
								Sprite = "assets/content/ui/UI.Background.Tile.psd"
							}
						}
					},
					
					Categories = new KitsCategoryUI(),
					ColorOne = new IColor("#E2DBD3", 100),
					ColorTwo = new IColor("#D74933", 100),
					ColorThree = new IColor("#8B8B8B", 15),
					ColorFour = new IColor("#38393F", 50),
					ColorFive = new IColor("#E2DBD3", 20),
					ColorRed = new IColor("#FF4B4B", 100),
					ColorWhite = new IColor("#FFFFFF", 100),
					ColorBackground = new IColor("#000000", 0),
				};
			}

			public static UserInterface GenerateMenuTemplateRustWithContentsAndCategoriesV2()
			{
				return new UserInterface()
				{
					Style = InterfaceStyle.NewRust,
					Height = 455,
					Width = 640,
					KitHeight = 236,
					KitWidth = 188,
					// KitHeight = 358,
					// KitWidth = 285,
					Margin = 12,
					KitsOnString = 4,
					Strings = 2,
					LeftIndent = 0,
					YIndent = -10,
					ShowCategories = true,
					KitAvailable = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
					},
					KitAmount = new KitAmountSettings
					{
						AnchorMin = "0.5 0",
						AnchorMax = "0.5 0",
						OffsetMin = "0",
						OffsetMax = "4",
						Width = 140
					},
					KitCooldown = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 15", OffsetMax = "-5 60"
					},
					KitSale = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 15", OffsetMax = "-5 60"
					},
					KitAmountCooldown = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "10 10", OffsetMax = "-10 38"
					},
					NoPermission = new InterfacePosition
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
					},
					CloseAfterReceive = true,
					Logo = new LogoConf
					{
						AnchorMin = "0 0.5",
						AnchorMax = "0 0.5",
						OffsetMin = "10 -20",
						OffsetMax = "50 20",
						Enabled = false,
						Image = string.Empty
					},
					HeaderPanel = new KitsPanelHeaderUI()
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 1", AnchorMax = "1 1", 
							OffsetMin = "40 -70", OffsetMax = "-37 -20",
							Color = IColor.CreateTransparent()
						},
						Title = new TextSettings()
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -50", OffsetMax = "-5 0",
							IsBold = true,
							FontSize = 32,
							Align = TextAnchor.UpperLeft,
							Color = new IColor("#CF432D", 90),
						},
						ShowLine = true,
						Line = new ImageSettings
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -51", OffsetMax = "0 -49",
							Color = new IColor("#373737", 50)
                        },
						ButtonClose = new ButtonSettings
						{
							AnchorMin = "1 1", AnchorMax = "1 1",
							OffsetMin = "-35 -37.5",
							OffsetMax = "-10 -12.5",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 10,
							Color = IColor.CreateWhite(),
							ButtonColor = new IColor("#4B68FF", 100),
						}
					},
					ContentPanel = new KitsPanelContentUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 0.5", AnchorMax = "1 0.5",
							OffsetMin = "40 -320", OffsetMax = "-40 205",
							// Color = IColor.CreateBlack()
							Color = IColor.CreateTransparent()
						},
						ButtonBack = new ButtonSettings()
						{
							AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-65 36", OffsetMax = "-35 66",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#38393F", 100)
						},
						ButtonNext = new ButtonSettings()
						{
							AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-30 36", OffsetMax = "0 66",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#FFFFFF", 100),
							ButtonColor = new IColor("#D74933", 100)
						},
						ButtonCreateKit = new ButtonSettings()
						{
							AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-65 10", OffsetMax = "0 30",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 11,
							Color = new IColor("#71B8ED", 100),
							ButtonColor = new IColor("#71B8ED", 20),
						},
						CheckboxShowAllKits = new CheckBoxSettings()
						{
							Background = new ImageSettings
							{
								AnchorMin = "0 0", 
								AnchorMax = "0 0",
								OffsetMin = "0 10", 
								OffsetMax = "22 32",
								Color = IColor.CreateTransparent()
							},
							CheckboxButton = new ButtonSettings
							{
								AnchorMin = "0 0.5", AnchorMax = "0 0.5",
								OffsetMin = "5 -7", OffsetMax = "19 7",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 10,
								Color = IColor.CreateWhite(),
								ButtonColor = IColor.CreateTransparent(),
							},
							CheckboxColor = new IColor("#38393F"),
							CheckboxSize = 3f,
							Title = new TextSettings
							{
								AnchorMin = "1 0",
								AnchorMax = "1 1",
								OffsetMin = "4 -4",
								OffsetMax = "104 4",
								IsBold = true, FontSize = 10, Align = TextAnchor.MiddleLeft,
								Color = IColor.CreateWhite()
							}
						},
					},
					
					KitPanel = new KitsPanelKitUI
					{
						Background = new ImageSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "0 0", OffsetMax = "0 0",
							Color = new IColor("#38393F", 40),
						},
						ShowName = true,
						KitName = new TextSettings()
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "10 -20", OffsetMax = "-10 0",
							Align = TextAnchor.MiddleLeft,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#E2DBD3", 100),
						},
						ShowNumber = false,
						KitNumber = new TextSettings(),
						KitImage = new InterfacePosition
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-60 -155", OffsetMax = "60 -35"
						},
						KitButtonTake = new ButtonSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						KitButtonTakeWhenShowInfo = new ButtonSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 12,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#D74933", 100),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						KitButtonInfo = new ButtonSettings
						{
							AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "125 10", OffsetMax = "-10 38",
							Align = TextAnchor.MiddleCenter,
							IsBold = true,
							FontSize = 13,
							Color = new IColor("#E2DBD3", 100),
							ButtonColor = new IColor("#F19F39", 60),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd"
						},
						DisplayKitContents = true,
						Contents = new ContentsSettings
						{
							Size = 28,
							Margin = 2,
							UpIndent = 20,
							ItemBackground = new ImageSettings
							{
								AnchorMin = "0.5 1",
								AnchorMax = "0.5 1",
								OffsetMin = "0 0",
								OffsetMax = "0 0",
								Color = new IColor("#696969", 30)
							},
							ItemImage = InterfacePosition.CreatePosition("0 0", "1 1", "2 2", "-2 -2"),
							ItemAmount = new TextSettings()
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "0 0", OffsetMax = "-2 -2",
								Align = TextAnchor.LowerRight,
								IsBold = true,
								FontSize = 6,
								Color = IColor.CreateWhite()
							},
							KitCooldown = new ButtonSettings
							{
								AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 14,
								Color = new IColor("#D1CBC4"),
								ButtonColor = new IColor("#696969", 30),
								Material = "assets/content/ui/namefontmaterial.mat",
								Sprite = "assets/content/ui/UI.Background.Tile.psd"
							},
							KitCooldownWithAmount = new ButtonSettings
							{
								AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 12,
								Color = new IColor("#D1CBC4"),
								ButtonColor = new IColor("#696969", 30),
								Material = "assets/content/ui/namefontmaterial.mat",
								Sprite = "assets/content/ui/UI.Background.Tile.psd"
							},
							KitAmount = new TextSettings
							{
								AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -20", OffsetMax = "-10 0",
								IsBold = true,
								Color = new IColor("#D1CBC4"),
								FontSize = 12, 
								Align = TextAnchor.MiddleRight
							},
							KitSale = new ButtonSettings
							{
								AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 5", OffsetMax = "-5 30",
								Align = TextAnchor.MiddleCenter,
								IsBold = true,
								FontSize = 12,
								Color = new IColor("#E2DBD3", 100),
								ButtonColor = new IColor("#D74933", 100),
								Material = "assets/content/ui/namefontmaterial.mat",
								Sprite = "assets/content/ui/UI.Background.Tile.psd"
							}
						}
					},
					
					Categories = new KitsCategoryUI
					{
						Background = InterfacePosition.CreatePosition("0 1", "1 1", "40 -110", "-37 -80"),
						Height = 24,
						Width = 100,
						Margin = 5,
						CategoriesScrollMinWidth = 890,
						ActiveCategory = new ButtonSettings
						{
							ButtonColor = new IColor("#CF432D", 80),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd",
							Color = new IColor("#E2DBD3", 100),
							IsBold = true,
							FontSize = 14,
							Align = TextAnchor.MiddleCenter
						},
						InactiveCategory = new ButtonSettings
						{
							ButtonColor = new IColor("#696969", 30),
							Material = "assets/content/ui/namefontmaterial.mat",
							Sprite = "assets/content/ui/UI.Background.Tile.psd",
							Color = new IColor("#E2DBD3", 90),
							IsBold = true,
							FontSize = 14,
							Align = TextAnchor.MiddleCenter
						},
					},
					
					ColorOne = new IColor("#E2DBD3", 100),
					ColorTwo = new IColor("#D74933", 100),
					ColorThree = new IColor("#8B8B8B", 15),
					ColorFour = new IColor("#38393F", 50),
					ColorFive = new IColor("#E2DBD3", 20),
					ColorRed = new IColor("#FF4B4B", 100),
					ColorWhite = new IColor("#FFFFFF", 100),
					ColorBackground = new IColor("#000000", 0),
				};
			}

			#endregion
		}

		private class KitAmountSettings : InterfacePosition
		{
			[JsonProperty(PropertyName = LangRu ? "Ширина" : "Width")]
			public float Width;
		}

		private class NpcKitsData
		{
			[JsonProperty(PropertyName = LangRu ? "Описание" : "Description")]
			public string Description;

			[JsonProperty(PropertyName = LangRu ? "Наборы" : "Kits",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> Kits = new();
		}

		#region UI Configuration

		public enum ScrollType
		{
			Horizontal,
			Vertical
		}

		public class ScrollViewUI
		{
			#region Fields

			[JsonProperty(PropertyName = "Scroll Type")] [JsonConverter(typeof(StringEnumConverter))]
			public ScrollType ScrollType;

			[JsonProperty(PropertyName = "Movement Type")] [JsonConverter(typeof(StringEnumConverter))]
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
				{
					contentRect = new CuiRectTransform()
					{
						AnchorMin = "0 0", AnchorMax = "0 1",
						OffsetMin = "0 0",
						OffsetMax = $"{totalWidth} 0"
					};
				}
				else
				{
					contentRect = new CuiRectTransform()
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = $"0 -{totalWidth}",
						OffsetMax = "0 0"
					};
				}

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
					var cuiScrollbar = new CuiScrollbar()
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

		public class CheckBoxSettings
		{
			[JsonProperty(PropertyName = "Background")]
			public ImageSettings Background;

			[JsonProperty(PropertyName = "Checkbox")]
			public ButtonSettings CheckboxButton;

			[JsonProperty(PropertyName = "Checkbox Size")]
			public float CheckboxSize;

			[JsonProperty(PropertyName = "Checkbox Color")]
			public IColor CheckboxColor;

			[JsonProperty(PropertyName = "Title")]
			public TextSettings Title;
		}

		public class ImageSettings : InterfacePosition
		{
			#region Fields
			[JsonProperty(PropertyName = "Sprite")]
			public string Sprite = string.Empty;

			[JsonProperty(PropertyName = "Material")]
			public string Material = string.Empty;

			[JsonProperty(PropertyName = "Image")]
			public string Image = string.Empty;

			[JsonProperty(PropertyName = "Color")]
			public IColor Color = IColor.CreateTransparent();

			[JsonProperty(PropertyName = "Cursor Enabled")]
			public bool CursorEnabled = false;

			[JsonProperty(PropertyName = "Keyboard Enabled")]
			public bool KeyboardEnabled = false;
			#endregion

			#region Private Methods

			[JsonIgnore] private ICuiComponent _imageComponent;
			
			public ICuiComponent GetImageComponent()
			{
				if (_imageComponent != null) return _imageComponent;
				
				if (!string.IsNullOrEmpty(Image))
				{
					var rawImage = new CuiRawImageComponent
					{
						Png = _instance.GetImage(Image),
						Color = Color.Get()
					};

					if (!string.IsNullOrEmpty(Sprite))
						rawImage.Sprite = Sprite;

					if (!string.IsNullOrEmpty(Material))
						rawImage.Material = Material;

					_imageComponent = rawImage;
				}
				else
				{
					var image = new CuiImageComponent
					{
						Color = Color.Get(),
					};

					if (!string.IsNullOrEmpty(Sprite))
						image.Sprite = Sprite;

					if (!string.IsNullOrEmpty(Material))
						image.Material = Material;

					_imageComponent = image;
				}

				return _imageComponent;
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

			public CuiElement GetImage(string parent,
				string name = null,
				string destroyUI = null)
			{
				if (string.IsNullOrEmpty(name))
					name = CuiHelper.GetGuid();

				var element = new CuiElement
				{
					Name = name,
					Parent = parent,
					DestroyUi = destroyUI,
					Components =
					{
						GetImageComponent(),
						GetRectTransform()
					}
				};

				if (CursorEnabled)
					element.Components.Add(new CuiNeedsCursorComponent());
				
				if (KeyboardEnabled)
					element.Components.Add(new CuiNeedsKeyboardComponent());
				
				return element;
			}

			#endregion

			#region Constructors

			public ImageSettings(){}
			
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

			[JsonProperty(PropertyName = "Image")]
			public string Image = string.Empty;

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

			public CuiButtonComponent GetButtonComponent(
				string cmd,
				string close = null)
			{
				var btn = new CuiButtonComponent
				{
					Color = ButtonColor.Get()
				};

				if (!string.IsNullOrEmpty(cmd))
					btn.Command = cmd;

				if (!string.IsNullOrEmpty(close))
					btn.Close = close;

				if (!string.IsNullOrEmpty(Sprite))
					btn.Sprite = Sprite;

				if (!string.IsNullOrEmpty(Material))
					btn.Material = Material;

				return btn;
			}
			
			public List<CuiElement> GetButton(
				string msg,
				string cmd,
				string parent,
				string name = null,
				string destroyUI = null,
				string close = null,
				CuiRectTransformComponent rect = null)
			{
				if (string.IsNullOrEmpty(name))
					name = CuiHelper.GetGuid();

				var list = new List<CuiElement>();

				list.Add(new CuiElement
				{
					Name = name,
					Parent = parent,
					DestroyUi = destroyUI,
					Components =
					{
						GetButtonComponent(cmd, close),
						rect ?? GetRectTransform()
					}
				});
                
				if (!string.IsNullOrEmpty(Image))
				{
					list.Add(new CuiElement
					{
						Parent = name,
						Components =
						{
							(Image.StartsWith("assets/")
								? new CuiImageComponent {Color = ImageColor.Get(), Sprite = Image}
								: new CuiRawImageComponent {Color = ImageColor.Get(), Png = _instance.GetImage(Image)}),
								
							UseCustomPositionImage && ImagePosition != null ? ImagePosition?.GetRectTransform() : new CuiRectTransformComponent()
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
								GetTextComponent(msg),
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
			#region Fields
			[JsonProperty(PropertyName = "Font Size")]
			public int FontSize = 12;

			[JsonProperty(PropertyName = "Is Bold?")]
			public bool IsBold = false;

			[JsonProperty(PropertyName = "Align")]
			[JsonConverter(typeof(StringEnumConverter))]
			public TextAnchor Align = TextAnchor.UpperLeft;

			[JsonProperty(PropertyName = "Color")] public IColor Color = IColor.CreateWhite();
			#endregion Fields

			#region Public Methods

			public CuiTextComponent GetTextComponent(string msg)
			{
				return new CuiTextComponent
				{
					Text = msg ?? string.Empty,
					FontSize = FontSize,
					Font = IsBold ? "robotocondensed-bold.ttf" : "robotocondensed-regular.ttf",
					Align = Align,
					Color = Color.Get()
				};
			}

			public CuiElement GetText(string msg,
				string parent,
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
						GetTextComponent(msg),
						GetRectTransform()
					}
				};
			}

			#endregion
		}

		public class InterfacePosition
		{
			#region Fields

			[JsonProperty(PropertyName = "AnchorMin")]
			public string AnchorMin = "0 0";

			[JsonProperty(PropertyName = "AnchorMax")]
			public string AnchorMax = "1 1";

			[JsonProperty(PropertyName = "OffsetMin")]
			public string OffsetMin = "0 0";

			[JsonProperty(PropertyName = "OffsetMax")]
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
			
			public InterfacePosition(){}

			public InterfacePosition(InterfacePosition other)
			{
				AnchorMin = other.AnchorMin;
				AnchorMax = other.AnchorMin;
				OffsetMin = other.AnchorMin;
				OffsetMax = other.AnchorMin;
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
					OffsetMax = offsetMax,
				};
			}

			public static InterfacePosition CreatePosition(CuiRectTransform rectTransform)
			{
				return new InterfacePosition
				{
					AnchorMin = rectTransform.AnchorMin,
					AnchorMax = rectTransform.AnchorMax,
					OffsetMin = rectTransform.OffsetMin,
					OffsetMax = rectTransform.OffsetMax,
				};
			}

			public static InterfacePosition CreateFullStretch()
			{
				return new InterfacePosition
				{
					AnchorMin = "0 0",
					AnchorMax = "1 1",
					OffsetMin = "0 0",
					OffsetMax = "0 0",
				};
			}
            
			public static InterfacePosition CreateCenter()
			{
				return new InterfacePosition
				{
					AnchorMin = "0.5 0.5",
					AnchorMax = "0.5 0.5",
					OffsetMin = "0 0",
					OffsetMax = "0 0",
				};
			}
            
			#endregion Constructors
		}

		private class DescriptionSettings : InterfacePosition
		{
			#region Fields

			[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = LangRu ? "Цвет фона" : "Background Color")]
			public IColor Color;

			[JsonProperty(PropertyName = "Font Size")]
			public int FontSize;

			[JsonProperty(PropertyName = "Font")] public string Font;

			[JsonProperty(PropertyName = "Align")] [JsonConverter(typeof(StringEnumConverter))]
			public TextAnchor Align;

			[JsonProperty(PropertyName = "Text Color")]
			public IColor TextColor;

			#endregion

			#region Public Methods

			public void Get(ref CuiElementContainer container, string parent, string name = null,
				string description = null)
			{
				if (!Enabled || string.IsNullOrEmpty(description)) return;

				if (string.IsNullOrEmpty(name))
					name = CuiHelper.GetGuid();

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = AnchorMin, AnchorMax = AnchorMax,
						OffsetMin = OffsetMin, OffsetMax = OffsetMax
					},
					Image = {Color = Color.Get()}
				}, parent, name);

				container.Add(new CuiLabel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text =
					{
						Text = description ?? string.Empty,
						Align = Align,
						Font = Font,
						FontSize = FontSize,
						Color = TextColor.Get()
					}
				}, name);
			}

			#endregion
		}

		private class MenuDescription : DescriptionSettings
		{
			[JsonProperty(PropertyName = LangRu ? "Описание" : "Description")]
			public string Description;
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

				if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

				var str = HEX.Trim('#');
				if (str.Length != 6)
					throw new Exception(HEX);

				var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
				var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
				var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

				_cachedResult = $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100}";
				_isCached = true;

				return _cachedResult;
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
				return new IColor("#FFFFFF", 100);
			}

			public static IColor CreateBlack()
			{
				return new IColor("#000000", 100);
			}
			
			#endregion
		}

		#endregion
		
		private class LogInfo
		{
			[JsonProperty(PropertyName = "To Console")]
			public bool Console;

			[JsonProperty(PropertyName = "To File")]
			public bool File;
		}

		private class RarityColor
		{
			[JsonProperty(PropertyName = LangRu ? "Шанс" : "Chance")]
			public int Chance;

			[JsonProperty(PropertyName = LangRu ? "Цвет" : "Color")]
			public string Color;

			public RarityColor(int chance, string color)
			{
				Chance = chance;
				Color = color;
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
	            
	            if (_config.Version > Version)
		            _config.Version = Version;

	            SaveConfig();
            }
            catch (Exception ex)
            {
	            PrintError("Your configuration file contains an error. Using default configuration values. Message: " + ex.Message);
	            LoadDefaultConfig();
            }
            
            LoadData();
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

			var baseConfig = new Configuration();

			if (_config.Version != default)
			{
				if (_config.Version < new VersionNumber(1, 0, 25))
					_config.UI.KitSale = baseConfig.UI.KitSale;

				if (_config.Version < new VersionNumber(1, 2, 0)) StartConvertOldData();

				if (_config.Version < new VersionNumber(2, 1, 4))
				{
					_config.UI.MainBackground = new ImageSettings()
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						Color = new IColor("#191919", 90),
						Sprite = "assets/content/ui/UI.Background.TileTex.psd",
						Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
						CursorEnabled = true
					};
					
					var width = Convert.ToSingle(Config.Get(LangRu ? "Интерфейс" : "Interface", LangRu ? "Ширина" : "Width"));
					var height = Convert.ToSingle(Config.Get(LangRu ? "Интерфейс" : "Interface", LangRu ? "Высота" : "Height"));
					if (width > 0 || height > 0)
					{
						var color = JsonConvert.DeserializeObject<IColor>(JsonConvert.SerializeObject(Config.Get(LangRu ? "Интерфейс" : "Interface", LangRu ? "Цвет фона" : "Background Color")));
					
						_config.UI.MainPanel = new ImageSettings
						{
							AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
							OffsetMin = $"-{width/2f} -{height/2f}",
							OffsetMax = $"{width/2} {height/2f}",
							Color = color ?? IColor.CreateTransparent()
						};
					}
				}
			}

			_config.Version = Version;
			PrintWarning("Config update completed!");
		}

		#endregion

		#region Data

		#region Disabled Auto Kits
        
		private List<ulong> _disablesAutoKits = new();

		private void LoadDisabledAutoKits()
		{
			if (!_config.UseChangeAutoKit) return;
			
			try
			{ 
				_disablesAutoKits = Interface.Oxide.DataFileSystem.ReadObject<List<ulong>>($"{Name}/DisabledAutoKits");
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			_disablesAutoKits ??= new List<ulong>();
		}

		private void SaveDisabledAutoKits()
		{
			if (!_config.UseChangeAutoKit) return;

			Interface.Oxide.DataFileSystem.WriteObject($"{Name}/DisabledAutoKits", _disablesAutoKits);
		}

		#endregion

		#region Kits
		
		private PluginData _data;

		private void LoadKits()
		{
			try
			{
				_data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>($"{Name}/Kits");
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			_data ??= new PluginData();
		}

		private void SaveKits()
		{
			Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Kits", _data);
		}
		
		#region Classes

		private class PluginData
		{
			[JsonProperty(PropertyName = "Kits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<Kit> Kits = new();
		}

		private class Kit
		{
			#region Fields

			[JsonIgnore] public int ID;

			[JsonProperty(PropertyName = "Name")] public string Name;

			[JsonProperty(PropertyName = "Display Name")]
			public string DisplayName;

			[JsonProperty(PropertyName = "Color")] public string Color;

			[JsonProperty(PropertyName = "Permission")]
			public string Permission;

			[JsonProperty(PropertyName = "Description")]
			public string Description;

			[JsonProperty(PropertyName = "Image")] public string Image;

			[JsonProperty(PropertyName = "Hide")] public bool Hide;

			[JsonProperty(PropertyName = "Category")] public string Category;
			
			[JsonProperty(PropertyName = "ShowInfo")] [DefaultValue(true)]
			public bool ShowInfo;

			[JsonProperty(PropertyName = "Amount")]
			public int Amount;

			[JsonProperty(PropertyName = "Cooldown")]
			public double Cooldown;

			[JsonProperty(PropertyName = "Wipe Block")]
			public double CooldownAfterWipe;

			[JsonProperty(PropertyName = "Use Building")]
			public bool UseBuilding;

			[JsonProperty(PropertyName = "Building")]
			public string Building;

			[JsonProperty(PropertyName = "Enable sale")]
			public bool Sale;

			[JsonProperty(PropertyName = "Selling price")]
			public int Price;

			[JsonProperty(PropertyName = "Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<KitItem> Items;

			[JsonProperty(PropertyName = "Use commands on receiving?")]
			public bool UseCommandsOnReceiving;

			[JsonProperty(PropertyName = "Commands on receiving (via '|')",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string CommandsOnReceiving;

			[JsonProperty(PropertyName = "Use slot for backpack?")]
			public bool UseSlotForBackpack;

			#endregion

			#region Public Methods

			public void Get(BasePlayer player)
			{
				Items?.ForEach(item => item?.Get(player));

				UseCommands(player);
			}

			public void UseCommands(BasePlayer player)
			{
				if (!UseCommandsOnReceiving || string.IsNullOrWhiteSpace(CommandsOnReceiving)) return;

				var pos = GetLookPoint(player);

				var command = CommandsOnReceiving.Replace("\n", "|")
					.Replace("%steamid%", player.UserIDString, StringComparison.OrdinalIgnoreCase)
					.Replace("%username%", player.displayName, StringComparison.OrdinalIgnoreCase)
					.Replace("%player.z%", pos.z.ToString(CultureInfo.InvariantCulture),
						StringComparison.OrdinalIgnoreCase)
					.Replace("%player.x%", pos.x.ToString(CultureInfo.InvariantCulture),
						StringComparison.OrdinalIgnoreCase)
					.Replace("%player.y%", pos.y.ToString(CultureInfo.InvariantCulture),
						StringComparison.OrdinalIgnoreCase);

				foreach (var check in command.Split('|'))
					_instance?.Server.Command(check);
			}

			private Vector3 GetLookPoint(BasePlayer player)
			{
				return GetLookHit(player, 10f)?.point ?? player.ServerPosition;
			}
				
			private RaycastHit? GetLookHit(BasePlayer player, float maxDistance = 5f)
			{
				return !Physics.Raycast(player.eyes.HeadRay(), out var hitInfo, maxDistance)
					? null
					: hitInfo;
			}
			
			#region Moving

			public void MoveRight()
			{
				var index = _instance._data.Kits.LastIndexOf(this);
				if (index >= 0 && index < _instance._data.Kits.Count - 1) 
					(_instance._data.Kits[index], _instance._data.Kits[index + 1]) = (_instance._data.Kits[index + 1], _instance._data.Kits[index]); // Swap
			}

			public void MoveLeft()
			{
				var index = _instance._data.Kits.LastIndexOf(this);
				if (index > 0 && index < _instance._data.Kits.Count)
					(_instance._data.Kits[index], _instance._data.Kits[index - 1]) =
						(_instance._data.Kits[index - 1], _instance._data.Kits[index]); // Swap
			}

			#endregion
	
			#endregion

			#region Cache

			[JsonIgnore] public Dictionary<int, KitItem> dictMainItems = new();

			[JsonIgnore] public Dictionary<int, KitItem> dictBeltItems = new();

			[JsonIgnore] public Dictionary<int, KitItem> dictWearItems = new();

			[JsonIgnore] public int beltCount;

			[JsonIgnore] public int wearCount;

			[JsonIgnore] public int mainCount;

			public void Update()
			{
				LoadContainers();

				GenerateJObject();
			}

			private void LoadContainers()
			{
				dictWearItems.Clear();
				dictBeltItems.Clear();
				dictMainItems.Clear();

				beltCount = 0;
				wearCount = 0;
				mainCount = 0;

				Items.ForEach(item =>
				{
					switch (item.Container)
					{
						case "wear":
							dictWearItems[item.Position] = item;

							wearCount++;
							break;
						case "belt":
							dictBeltItems[item.Position] = item;

							beltCount++;
							break;
						case "main":
							dictMainItems[item.Position] = item;

							mainCount++;
							break;
					}
				});
			}

			public KitItem GetItemByContainerAndPosition(string container, int position)
			{
				KitItem item;
				switch (container)
				{
					case "wear":
					{
						return dictWearItems.TryGetValue(position, out item) ? item : null;
					}
					case "belt":
					{
						return dictBeltItems.TryGetValue(position, out item) ? item : null;
					}
					default:
					{
						return dictMainItems.TryGetValue(position, out item) ? item : null;
					}
				}
			}

			#endregion

			#region JObject

			[JsonIgnore] private JObject _jObject;

			[JsonIgnore]
			internal JObject ToJObject
			{
				get
				{
					if (_jObject == null) GenerateJObject();

					return _jObject;
				}
			}

			private void GenerateJObject()
			{
				_jObject = new JObject
				{
					["Name"] = DisplayName,
					["Description"] = Description,
					["Category"] = Category,
					["RequiredPermission"] = Permission,
					["MaximumUses"] = Amount,
					["Cost"] = Price,
					["IsHidden"] = Hide,
					["CopyPasteFile"] = Building,
					["KitImage"] = Image,
					["MainItems"] = new JArray(),
					["WearItems"] = new JArray(),
					["BeltItems"] = new JArray()
				};

				foreach (var kitItem in dictMainItems.Values)
					(_jObject["MainItems"] as JArray)?.Add(kitItem.ToJObject);

				foreach (var kitItem in dictWearItems.Values)
					(_jObject["WearItems"] as JArray)?.Add(kitItem.ToJObject);

				foreach (var kitItem in dictBeltItems.Values)
					(_jObject["BeltItems"] as JArray)?.Add(kitItem.ToJObject);
			}

			#endregion
		}

		private enum KitItemType
		{
			Item,
			Command
		}

		private class KitItem
		{
			[JsonProperty(PropertyName = "Type")] [JsonConverter(typeof(StringEnumConverter))]
			public KitItemType Type;

			[JsonProperty(PropertyName = "Command")]
			public string Command;

			[JsonProperty(PropertyName = "ShortName")]
			public string ShortName;

			[JsonProperty(PropertyName = "DisplayName")]
			public string DisplayName;

			[JsonProperty(PropertyName = "Amount")]
			public int Amount;

			[JsonProperty(PropertyName = "Blueprint")]
			public int Blueprint;

			[JsonProperty(PropertyName = "SkinID")]
			public ulong SkinID;

			[JsonProperty(PropertyName = "Container")]
			public string Container;

			[JsonProperty(PropertyName = "Condition")]
			public float Condition;

			[JsonProperty(PropertyName = "Chance")]
			public int Chance;

			[JsonProperty(PropertyName = "Position", DefaultValueHandling = DefaultValueHandling.Populate)]
			[DefaultValue(-1)]
			public int Position;

			[JsonProperty(PropertyName = "Image")] public string Image = string.Empty;

			[JsonProperty(PropertyName = "Weapon")]
			public Weapon Weapon;

			[JsonProperty(PropertyName = "Content", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<ItemContent> Content;

			[JsonProperty(PropertyName = "Text")] public string Text;

			[JsonProperty(PropertyName = "Genes")]
			public ItemGenes Genes = new();

			public void Get(BasePlayer player)
			{
				if (Chance < 100 && Random.Range(0, 100) > Chance) return;

				switch (Type)
				{
					case KitItemType.Item:
						GiveItem(player, BuildItem(),
							Container switch
							{
								"belt" => player.inventory.containerBelt,
								"wear" => player.inventory.containerWear,
								_ => player.inventory.containerMain
							});
						break;

					case KitItemType.Command:
						ToCommand(player);
						break;
				}
			}

			private void ToCommand(BasePlayer player)
			{
				var command = Command.Replace("\n", "|")
					.Replace("%steamid%", player.UserIDString, StringComparison.OrdinalIgnoreCase).Replace("%username%",
						player.displayName, StringComparison.OrdinalIgnoreCase);

				foreach (var check in command.Split('|')) _instance?.Server.Command(check);
			}

			private static void GiveItem(BasePlayer player, Item item, ItemContainer cont = null)
			{
				if (item == null) return;
				var playerInventory = player.inventory;

				var moved = item.MoveToContainer(cont, item.position) || item.MoveToContainer(cont) ||
				            item.MoveToContainer(playerInventory.containerMain);
				if (!moved)
				{
					if (cont == playerInventory.containerBelt)
						moved = item.MoveToContainer(playerInventory.containerWear);
					if (cont == playerInventory.containerWear)
						moved = item.MoveToContainer(playerInventory.containerBelt);
				}

				if (!moved)
					item.Drop(player.GetCenter(), player.GetDropVelocity());
			}

			[JsonIgnore] private ItemDefinition _itemDefinition;

			[JsonIgnore]
			public ItemDefinition ItemDefinition
			{
				get
				{
					if (_itemDefinition == null) _itemDefinition = ItemManager.FindItemDefinition(ShortName);

					return _itemDefinition;
				}
			}

			public Item BuildItem()
			{
				var item = ItemManager.Create(ItemDefinition, Mathf.Max(Amount, 1), SkinID);
				item.condition = Condition;

				item.position = Position;

				if (Blueprint != 0)
					item.blueprintTarget = Blueprint;

				if (!string.IsNullOrEmpty(DisplayName))
					item.name = DisplayName;

				if (!string.IsNullOrEmpty(Text))
					item.text = Text;

				if (Weapon != null)
				{
					var heldEntity = item.GetHeldEntity();
					if (heldEntity != null)
					{
						heldEntity.skinID = SkinID;

						var baseProjectile = heldEntity as BaseProjectile;
						if (baseProjectile != null && !string.IsNullOrEmpty(Weapon.ammoType))
						{
							baseProjectile.primaryMagazine.contents = Weapon.ammoAmount;
							baseProjectile.primaryMagazine.ammoType =
								ItemManager.FindItemDefinition(Weapon.ammoType);
						}

						heldEntity.SendNetworkUpdate();
					}
				}

				Content?.ForEach(cont =>
				{
					var newCont = ItemManager.CreateByName(cont.ShortName, cont.Amount, cont.Skin);
					newCont.condition = cont.Condition;
					newCont.MoveToContainer(item.contents);
				});

				if (Genes is {Enabled: true}) 
					Genes.Build(item);

				return item;
			}

			public static KitItem FromOld(ItemData item, string container)
			{
				var newItem = new KitItem
				{
					Content =
						item.Contents?.Select(x =>
								new ItemContent {ShortName = x.Shortname, Condition = x.Condition, Amount = x.Amount})
							.ToList() ?? new List<ItemContent>(),
					Weapon = new Weapon {ammoAmount = item.Ammo, ammoType = item.Ammotype},
					Container = container,
					SkinID = item.Skin,
					Command = string.Empty,
					Chance = 100,
					Blueprint = string.IsNullOrEmpty(item.BlueprintShortname) ? 0 : 1,
					Condition = item.Condition,
					Amount = item.Amount,
					ShortName = item.Shortname,
					Type = KitItemType.Item,
					Position = item.Position,
					Image = string.Empty,
					Genes = new ItemGenes
					{
						Enabled = false,
						GeneTypes = new List<char>
						{
							'X', 'Y', 'G', 'W', 'H', 'W'
						}
					}
				};

				return newItem;
			}

			[JsonIgnore] private int _itemId = -1;

			[JsonIgnore]
			public int itemId
			{
				get
				{
					if (_itemId == -1)
						UpdateItemID();
					return _itemId;
				}
			}

			[JsonIgnore] private ICuiComponent _image;

			public CuiElement GetImage(string aMin, string aMax, string oMin, string oMax, string parent,
				string name = null)
			{
				return GetImage(new CuiRectTransformComponent
				{
					AnchorMin = aMin, AnchorMax = aMax,
					OffsetMin = oMin, OffsetMax = oMax
				}, parent, name);
			}
			
			public CuiElement GetImage(CuiRectTransformComponent rect, string parent,
				string name = null)
			{
				if (_image == null)
					GenerateNewImage();

				return new CuiElement
				{
					Name = string.IsNullOrEmpty(name) ? CuiHelper.GetGuid() : name,
					Parent = parent,
					Components =
					{
						_image,
						rect
					}
				};
			}

			private void GenerateNewImage()
			{
				if (_instance._enabledImageLibrary && !string.IsNullOrEmpty(Image))
					_image = new CuiRawImageComponent
					{
						Png = _instance.GetImage(Image)
					};
				else
					_image = new CuiImageComponent
					{
						ItemId = itemId,
						SkinId = SkinID
					};
			}

			private void UpdateItemID()
			{
				_itemId = ItemManager.FindItemDefinition(ShortName)?.itemid ?? -1;
			}

			public void Update()
			{
				UpdateItemID();

				GenerateNewImage();

				GenerateJObject();
			}

			#region JObject

			[JsonIgnore] private JObject _jObject;

			[JsonIgnore]
			public JObject ToJObject
			{
				get
				{
					if (_jObject == null) GenerateJObject();

					return _jObject;
				}
			}

			private void GenerateJObject()
			{
				_jObject = new JObject
				{
					["Shortname"] = ShortName,
					["DisplayName"] = DisplayName,
					["SkinID"] = SkinID,
					["Amount"] = Amount,
					["Condition"] = Condition,
					["MaxCondition"] = Condition,
					["IsBlueprint"] = Blueprint != 0,
					["Ammo"] = Weapon?.ammoAmount,
					["AmmoType"] = Weapon?.ammoType,
					["Text"] = Text,
					["Contents"] = new JArray()
				};

				Content?.ForEach(x =>
				{
					(_jObject["Contents"] as JArray)?.Add(new JObject
					{
						["Shortname"] = x.ShortName,
						["DisplayName"] = string.Empty,
						["SkinID"] = 0,
						["Amount"] = x.Amount,
						["Condition"] = x.Condition,
						["MaxCondition"] = x.Condition,
						["IsBlueprint"] = false,
						["Ammo"] = 0,
						["AmmoType"] = string.Empty,
						["Text"] = string.Empty,
						["Contents"] = new JArray()
					});
				});
			}

			#endregion
		}

		private class Weapon
		{
			public string ammoType;

			public int ammoAmount;
		}

		private class ItemContent
		{
			public string ShortName;

			public float Condition;

			public int Amount;
			
			public ulong Skin;
		}

		private class ItemGenes
		{
			#region Fields

			[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = LangRu ? "Типы генов" : "Gene types",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<char> GeneTypes = new();

			#endregion

			#region Cache

			[JsonIgnore] private int _encodedGenes;

			public bool TryInit()
			{
				if (GeneTypes is not {Count: 6})
					return false;
				
				var genes = new GrowableGenes();
				for (var i = 0; i < GeneTypes.Count; i++) genes.Genes[i].Set(ConvertGeneType(GeneTypes[i]));

				_encodedGenes = GrowableGeneEncoding.EncodeGenesToInt(genes);
				return true;
			}

			#endregion

			#region Helpers

			public void Build(Item item)
			{
				if (GeneTypes is not {Count: 6})
					return;
				
				GrowableGeneEncoding.EncodeGenesToItem(_encodedGenes, item);
			}

			private static GrowableGenetics.GeneType ConvertGeneType(char geneType)
			{
				return char.ToLower(geneType) switch
				{
					'g' => GrowableGenetics.GeneType.GrowthSpeed,
					'y' => GrowableGenetics.GeneType.Yield,
					'h' => GrowableGenetics.GeneType.Hardiness,
					'x' => GrowableGenetics.GeneType.Empty,
					'w' => GrowableGenetics.GeneType.WaterRequirement,
					_ => GrowableGenetics.GeneType.Empty
				};
			}

			public bool IsSameGenes(Item item)
			{
				return item.instanceData != null && _encodedGenes == item.instanceData.dataInt;
			}
			
			#endregion
		}
		
		#endregion
		
		#endregion

		#region Global

		private void LoadData()
		{
			LoadKits();
			
			LoadDisabledAutoKits();
		}

		#endregion

		#endregion

		#region Hooks
		
		private void Init()
		{
			_instance = this;

			CacheKits();

			if (!_config.OnPermissionsUpdate)
			{
				Unsubscribe(nameof(OnUserPermissionGranted));
				Unsubscribe(nameof(OnUserPermissionRevoked));
			}
		}

		private void OnServerInitialized()
		{
			LoadImages();
			
			LoadServerPanel();

			LoadItems();

			FillCategories();
			
			RegisterPermissions();

			RegisterCommands();
		}

		private void Unload()
		{
			if (_wipePlayers != null)
				ServerMgr.Instance.StopCoroutine(_wipePlayers);

			foreach (var player in BasePlayer.activePlayerList)
			{
				CuiHelper.DestroyUi(player, Layer);
				CuiHelper.DestroyUi(player, InfoLayer);
				CuiHelper.DestroyUi(player, EditingLayer);
				CuiHelper.DestroyUi(player, ModalLayer);

				PlayerData.SaveAndUnload(player.UserIDString);
			}

			_instance = null;
		}

		#region Wipe

		private void OnNewSave(string filename)
		{
			if (!_config.AutoWipe) return;

			DoWipePlayers();
		}

		#endregion

		#region Server Panel

		private void OnServerPanelCategoryPage(BasePlayer player, int category, int page)
		{
			RemoveOpenedKits(player);
		}

		private void OnServerPanelClosed(BasePlayer player)
		{
			RemoveOpenedKits(player);
		}

		private void OnReceiveCategoryInfo(int categoryID)
		{
			_serverPanelCategory.categoryID = categoryID;
		}
		
		#endregion
		
		private void OnPlayerRespawned(BasePlayer player)
		{
			if (player == null || (_config.UseChangeAutoKit && _disablesAutoKits.Contains(player.userID))) return;

			var kits = GetAutoKits(player);
			if (kits.Count == 0)
				return;

			player.inventory.Strip();

			if (_config.OnceAutoKit)
			{
				var lastKit = kits.LastOrDefault();
				if (lastKit != null) ProcessAutoKit(player, lastKit);
			}
			else
			{
				kits.ForEach(kit => ProcessAutoKit(player, kit));
			}
		}

		private void OnPlayerDisconnected(BasePlayer player)
		{
			RemoveOpenedKits(player);
			_kitEditing.Remove(player.userID);
			_itemEditing.Remove(player.userID);

			PlayerData.SaveAndUnload(player.UserIDString);
		}

		private void OnPlayerDeath(BasePlayer player, HitInfo info)
		{
			if (player == null) return;

			CuiHelper.DestroyUi(player, Layer);

			OnPlayerDisconnected(player);
		}

		private void OnUseNPC(BasePlayer npc, BasePlayer player)
		{
			if (npc == null || player == null || !_config.NpcKits.ContainsKey(npc.UserIDString)) return;

			SetOpenedKits(player, npc.userID);
			
			MainUi(player, first: true);
		}

		#region Image Library

		private void OnPluginLoaded(Plugin plugin)
		{
			switch (plugin.Name)
			{
				case nameof(ImageLibrary):
					timer.In(1, LoadImages);
					break;
				case nameof(ServerPanel):
					timer.In(1, LoadServerPanel);
					break;
			}
		}

		private void OnPluginUnloaded(Plugin plugin)
		{
			switch (plugin.Name)
			{
				case nameof(ImageLibrary):
					_enabledImageLibrary = false;
					break;
				case nameof(ServerPanel):
					_serverPanelCategory.spStatus = false;
					break;
			}
		}

		#endregion

		#region Permissions

		private void OnUserPermissionGranted(string id, string permName)
		{
			UpdateOpenedUI(id, permName);
		}

		private void OnUserPermissionRevoked(string id, string permName)
		{
			UpdateOpenedUI(id, permName);
		}

		#endregion

		#endregion

		#region Commands

		private void CmdOpenKits(IPlayer cov, string command, string[] args)
		{
			var player = cov?.Object as BasePlayer;
			if (player == null) return;

			if (_enabledImageLibrary == false)
			{
				SendNotify(player, NoILError, 1);

				BroadcastILNotInstalled();
				return;
			}

			if (args.Length == 0)
			{
				if (_serverPanelCategory.spStatus &&  _serverPanelCategory.categoryID != -1)
				{
					ServerPanel?.Call("API_OnServerPanelOpenCategoryByID", player, _serverPanelCategory.categoryID);
				}
				else
				{
					TryCreateOpenedKits(player, out _);
				
					MainUi(player, first: true);
				}
				return;
			}

			switch (args[0])
			{
				case "help":
				{
					Reply(player, KitsHelp, command);
					break;
				}

				case "list":
				{
					Reply(player, KitsList,
						string.Join(", ", GetAvailableKitList(player).Select(x => $"'{x.DisplayName}'")));
					break;
				}

				case "remove":
				{
					if (!IsAdmin(player)) return;

					var name = string.Join(" ", args.Skip(1));
					if (string.IsNullOrEmpty(name))
					{
						SendNotify(player, KitNotFound, 1, name);
						return;
					}

					var kit = GetAvailableKitList(player)?.Find(x => x.DisplayName == name);
					if (kit == null)
					{
						SendNotify(player, KitNotFound, 1, name);
						return;
					}
					
					DoRemoveKit(kit);
                    
					SendNotify(player, KitRemoved, 0, name);
					break;
				}

				case "autokit":
				{
					if (!_config.UseChangeAutoKit) return;

					if (!string.IsNullOrEmpty(_config.ChangeAutoKitPermission) &&
					    !cov.HasPermission(_config.ChangeAutoKitPermission))
					{
						ErrorUi(player, Msg(player, NoPermission));
						return;
					}

					bool enabled;
					if (_disablesAutoKits.Contains(player.userID))
					{
						_disablesAutoKits.Remove(player.userID);

						enabled = true;
					}
					else
					{
						_disablesAutoKits.Add(player.userID);

						enabled = false;
					}

					if (enabled)
						SendNotify(player, ChangeAutoKitOn, 0);
					else
						SendNotify(player, ChangeAutoKitOff, 1);

					SaveDisabledAutoKits();
					break;
				}

				default:
				{
					var name = string.Join(" ", args);
					if (string.IsNullOrEmpty(name))
					{
						SendNotify(player, KitNotFound, 1, name);
						return;
					}

					var kit = GetAvailableKitList(player, checkAmount: false)
						.Find(x => string.Equals(x.Name, name, StringComparison.InvariantCultureIgnoreCase) ||
						           string.Equals(x.DisplayName, name, StringComparison.InvariantCultureIgnoreCase));
					if (kit == null)
					{
						SendNotify(player, KitNotFound, 1, name);
						return;
					}

					GiveKitToPlayer(player, kit, chat: true);
					break;
				}
			}
		}

		[ConsoleCommand("UI_Kits")]
		private void CmdKitsConsole(ConsoleSystem.Arg arg)
		{
			var player = arg?.Player();
			if (player == null || !arg.HasArgs()) return;
			
#if TESTING
			SayDebug($"UI_Kits called: {string.Join(" ", arg.Args)}");
#endif

			switch (arg.Args[0])
			{
				case "close":
				{
					RemoveOpenedKits(player);

					StopEditing(player);
					break;
				}

				case "stopedit":
				{
					StopEditing(player);
					break;
				}

				case "main":
				{
					var openedKits = GetOpenedKits(player);
					switch (arg.GetString(1))
					{
						case "page":
						{
							var newPage = arg.GetInt(2);
							openedKits.OnChangeCurrentPage(newPage);
							
							UpdateUI(player, container =>
							{
								MainKitsContentUI(player, container);
							});
							break;
						}

						case "show_all":
						{
							var newShowAll = arg.GetBool(2);
							openedKits.OnChangeShowAll(newShowAll);
							
							UpdateUI(player, container =>
							{
								MainKitsContentUI(player, container);
							});
							break;
						}
					}
					break;
				}

				case "infokit":
				{
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out var kitId)) return;

					var kit = FindKitByID(kitId);
					if (kit == null) return;

					StopEditing(player);

					InfoKitUi(player, kit);
					break;
				}

				case "givekit":
				{
					if (!arg.HasArgs(2) ||
					    !int.TryParse(arg.Args[1], out var kitId)) return;
					
					var kit = FindKitByID(kitId);
					if (kit == null) return;

					GiveKitToPlayer(player, kit);
					break;
				}

				case "editkit":
				{
					if (!IsAdmin(player)) return;
					
					if (!arg.HasArgs(2) || !bool.TryParse(arg.Args[1], out var creating)) return;

					var kitId = -1;
					if (arg.HasArgs(3))
						int.TryParse(arg.Args[2], out kitId);

					if (arg.HasArgs(4) && (!arg.HasArgs(5) || string.IsNullOrEmpty(arg.Args[4])))
						return;

					if (arg.HasArgs(5))
					{
						var key = arg.Args[3];
						var value = arg.Args[4];

						if (_kitEditing.ContainsKey(player.userID) && _kitEditing[player.userID].ContainsKey(key))
						{
							object newValue = null;

							switch (key)
							{
								case "UseSlotForBackpack":
								case "UseCommandsOnReceiving":
								case "ShowInfo":
								case "Hide":
								case "AutoKit":
								case "Sale":
								{
									if (value == "delete")
										newValue = default(bool);
									else if (bool.TryParse(value, out var result)) newValue = result;
									break;
								}
								case "Amount":
								case "Price":
								{
									if (value == "delete")
										newValue = default(int);
									else if (int.TryParse(value, out var result))
										newValue = result;
									break;
								}
								case "Cooldown":
								case "CooldownAfterWipe":
								{
									if (value == "delete")
										newValue = default(double);
									else if (double.TryParse(value, out var result))
										newValue = result;
									break;
								}
								case "Category":
								case "CommandsOnReceiving":
								case "Description":
								case "DisplayName":
								{
									newValue = value == "delete" ? string.Empty : string.Join(" ", arg.Args.Skip(4));
									break;
								}
								default:
								{
									newValue = value == "delete" ? string.Empty : value;
									break;
								}
							}

							if (_kitEditing[player.userID][key] != null &&
							    _kitEditing[player.userID][key].Equals(newValue))
								return;

							_kitEditing[player.userID][key] = newValue;
						}
					}

					EditingKitUi(player, creating, kitId);
					break;
				}

				case "edit_kit_position":
				{
					if (!IsAdmin(player) || !arg.HasArgs(3)) return;

					var kitID = arg.GetInt(1);
					var moveType = arg.GetString(2);

					var kit = FindKitByID(kitID);
					if (kit == null) return;

					switch (moveType)
					{
						case "right":
							kit.MoveRight();
							break;
						case "left":
							kit.MoveLeft();
							break;
						default:
							PrintError("Unknown move type: {0}", moveType);
							return;
					}

					CacheKits();
					
					SaveKits();
					
					GetOpenedKits(player)?.UpdateKits();

					UpdateUI(player, container =>
					{
						MainKitsContentUI(player, container);
					});
					break;
				}

				case "takeitem":
				{
					if (!IsAdmin(player)) return;

					if (!arg.HasArgs(6) ||
					    !_itemEditing.ContainsKey(player.userID) ||
					    !int.TryParse(arg.Args[1], out var page) ||
					    !int.TryParse(arg.Args[3], out var kitId) ||
					    !int.TryParse(arg.Args[4], out var slot))
						return;

					var container = arg.Args[2];

					_itemEditing[player.userID]["ShortName"] = arg.Args[5];
					_itemEditing[player.userID]["SkinID"] = 0UL;

					EditingItemUi(player, kitId, slot, container);
					break;
				}

				case "selectitem":
				{
					if (!IsAdmin(player)) return;

					if (!arg.HasArgs(4) ||
					    !_itemEditing.ContainsKey(player.userID) ||
					    !int.TryParse(arg.Args[2], out var kitId) ||
					    !int.TryParse(arg.Args[3], out var slot))
						return;

					var container = arg.Args[1];

					var selectedCategory = string.Empty;
					if (arg.HasArgs(5))
						selectedCategory = arg.Args[4];

					var page = 0;
					if (arg.HasArgs(6))
						int.TryParse(arg.Args[5], out page);

					var input = string.Empty;
					if (arg.HasArgs(7))
						input = string.Join(" ", arg.Args.Skip(6));

					SelectItem(player, kitId, slot, container, selectedCategory, page, input);
					break;
				}

				case "edit_item":
				{
					switch (arg.GetString(1))
					{
						case "start":
						{
							var container = arg.GetString(2);
							var kitId = arg.GetInt(3);
							var slot = arg.GetInt(4);
							
							EditingItemUi(player, kitId, slot, container, true);
							break;
						}

						case "close":
						{
							StopEditing(player);
							
							var openedKits = GetOpenedKits(player);
							var targetUI = openedKits.useMainUI ? _config.UI : _config.MenuUI;
							if (!targetUI.KitPanel.DisplayKitContents)
							{
								var kitId = arg.GetInt(2);
								
								var kit = FindKitByID(kitId);
								if (kit == null) return;

								StopEditing(player);

								InfoKitUi(player, kit);
							}
							break;
						}
					}
					break;
				}

				case "edititem":
				{
					if (!IsAdmin(player)) return;

					if (!arg.HasArgs(6) ||
					    !int.TryParse(arg.Args[2], out var kitId) ||
					    !int.TryParse(arg.Args[3], out var slot)) return;

					var container = arg.Args[1];

					var key = arg.Args[4];
					var value = arg.Args[5];

					if (_itemEditing.ContainsKey(player.userID) && _itemEditing[player.userID].ContainsKey(key))
					{
						object newValue = null;

						switch (key)
						{
							case "Type":
							{
								if (value == "delete")
									newValue = default(KitItemType);
								else if (Enum.TryParse(value, out KitItemType type))
									newValue = type;
								break;
							}
							case "Command":
							{
								newValue = value == "delete" ? string.Empty : string.Join(" ", arg.Args.Skip(5));
								break;
							}
							case "DisplayName":
							{
								newValue = value == "delete" ? string.Empty : string.Join(" ", arg.Args.Skip(5));
								break;
							}
							case "ShortName":
							{
								if (value == "delete")
								{
									newValue = string.Empty;
								}
								else
								{
									newValue = value;
									_itemEditing[player.userID]["SkinID"] = 0UL;
								}

								break;
							}
							case "SkinID":
							{
								if (value == "delete")
									newValue = default(ulong);
								else if (ulong.TryParse(value, out var result))
									newValue = result;
								break;
							}
							case "Amount":
							case "Blueprint":
							case "Chance":
							{
								if (value == "delete")
									newValue = default(int);
								else if (int.TryParse(value, out var result))
									newValue = result;
								break;
							}
						}

						if (_itemEditing[player.userID][key]?.Equals(newValue) == true)
							return;

						_itemEditing[player.userID][key] = newValue;
					}

					EditingItemUi(player, kitId, slot, container);
					break;
				}

				case "saveitem":
				{
					if (!IsAdmin(player)) return;

					if (!arg.HasArgs(4) ||
					    !int.TryParse(arg.Args[1], out var kitId) ||
					    !int.TryParse(arg.Args[2], out var slot)) return;

					var container = arg.Args[3];
					if (string.IsNullOrEmpty(container)) return;

					var editing = _itemEditing[player.userID];
					if (editing == null) return;

					var kit = FindKitByID(kitId);
					if (kit == null) return;

					var item = kit.GetItemByContainerAndPosition(container, slot);
					var hasItem = item != null;
					var newItem = item == null || editing["ShortName"].ToString() != item.ShortName;

					item ??= new KitItem();

					item.Type = (KitItemType) editing["Type"];
					item.Command = editing["Command"].ToString();
					item.Container = editing["Container"].ToString();
					item.ShortName = editing["ShortName"].ToString();
					item.DisplayName = editing["DisplayName"].ToString();
					item.Amount = (int) editing["Amount"];
					item.Blueprint = (int) editing["Blueprint"];
					item.Chance = (int) editing["Chance"];
					item.SkinID = (ulong) editing["SkinID"];
					item.Position = (int) editing["Position"];

					if (newItem)
					{
						var info = ItemManager.FindItemDefinition(item.ShortName);
						if (info != null) 
							item.Condition = info.condition.max;
					}

					if (!hasItem) kit.Items.Add(item);

					item.Update();

					kit.Update();

					StopEditing(player);

					SaveKits();

					var openedKits = GetOpenedKits(player);
					var targetUI = openedKits.useMainUI ? _config.UI : _config.MenuUI;
					if (targetUI.KitPanel.DisplayKitContents)
						UpdateUI(player, cont => RefreshKitUi(ref cont, player, kit));
					else
						InfoKitUi(player, kit);

					break;
				}

				case "removeitem":
				{
					if (!IsAdmin(player)) return;

					if (!arg.HasArgs(4) ||
					    !int.TryParse(arg.Args[1], out var kitId) ||
					    !int.TryParse(arg.Args[2], out var slot)) return;

					var editing = _itemEditing[player.userID];
					if (editing == null) return;

					var kit = FindKitByID(kitId);
					if (kit == null) return;

					var item = kit.GetItemByContainerAndPosition(arg.Args[3], slot);
					if (item != null)
						kit.Items.Remove(item);

					kit.Update();

					StopEditing(player);

					SaveKits();

					var openedKits = GetOpenedKits(player);
					var targetUI = openedKits.useMainUI ? _config.UI : _config.MenuUI;
					if (targetUI.KitPanel.DisplayKitContents)
						UpdateUI(player, cont => RefreshKitUi(ref cont, player, kit));
					else
						InfoKitUi(player, kit);
					break;
				}

				case "savekit":
				{
					if (!IsAdmin(player)) return;

					if (!arg.HasArgs(3) ||
					    !bool.TryParse(arg.Args[1], out var creating) ||
					    !int.TryParse(arg.Args[2], out var kitId)) return;

					var editing = _kitEditing[player.userID];
					if (editing == null) return;

					Kit kit;
					if (creating)
					{
						kit = new Kit
						{
							ID = ++_lastKitID,
							Name = Convert.ToString(editing["Name"]),
							DisplayName = Convert.ToString(editing["DisplayName"]),
							Description = Convert.ToString(editing["Description"]),
							Category = Convert.ToString(editing["Category"]),
							Color = Convert.ToString(editing["Color"]),
							Permission = Convert.ToString(editing["Permission"]),
							Image = Convert.ToString(editing["Image"]),
							Hide = Convert.ToBoolean(editing["Hide"]),
							ShowInfo = Convert.ToBoolean(editing["ShowInfo"]),
							Amount = Convert.ToInt32(editing["Amount"]),
							Cooldown = Convert.ToDouble(editing["Cooldown"]),
							CooldownAfterWipe = Convert.ToDouble(editing["CooldownAfterWipe"]),
							Sale = Convert.ToBoolean(editing["Sale"]),
							Price = Convert.ToInt32(editing["Price"]),
							Items = new List<KitItem>(),
							UseCommandsOnReceiving = Convert.ToBoolean(editing["UseCommandsOnReceiving"]),
							CommandsOnReceiving = Convert.ToString(editing["CommandsOnReceiving"]),
							UseSlotForBackpack = Convert.ToBoolean(editing["UseSlotForBackpack"])
						};

						_data.Kits.Add(kit);

						var kitIndex = _data.Kits.IndexOf(kit);

						_kitByName[kit.Name] = kitIndex;
						_kitByID[kit.ID] = kitIndex;
					}
					else
					{
						kit = FindKitByID(kitId);
						if (kit == null) return;

						var oldName = kit.Name;

						kit.Name = Convert.ToString(editing["Name"]);
						kit.DisplayName = Convert.ToString(editing["DisplayName"]);
						kit.Description = Convert.ToString(editing["Description"]);
						kit.Category = Convert.ToString(editing["Category"]);
						kit.Color = Convert.ToString(editing["Color"]);
						kit.Permission = Convert.ToString(editing["Permission"]);
						kit.Image = Convert.ToString(editing["Image"]);
						kit.Hide = Convert.ToBoolean(editing["Hide"]);
						kit.ShowInfo = Convert.ToBoolean(editing["ShowInfo"]);
						kit.Amount = Convert.ToInt32(editing["Amount"]);
						kit.Cooldown = Convert.ToDouble(editing["Cooldown"]);
						kit.CooldownAfterWipe = Convert.ToDouble(editing["CooldownAfterWipe"]);
						kit.Sale = Convert.ToBoolean(editing["Sale"]);
						kit.Price = Convert.ToInt32(editing["Price"]);
						kit.UseCommandsOnReceiving = Convert.ToBoolean(editing["UseCommandsOnReceiving"]);
						kit.CommandsOnReceiving = Convert.ToString(editing["CommandsOnReceiving"]);
						kit.UseSlotForBackpack = Convert.ToBoolean(editing["UseSlotForBackpack"]);

						if (oldName != kit.Name)
						{
							_kitByName.Remove(oldName);

							_kitByName[kit.Name] = _data.Kits.IndexOf(kit);
						}
					}

					var autoKit = Convert.ToBoolean(editing["AutoKit"]);
					if (autoKit)
					{
						if (!_config.AutoKits.Contains(kit.Name))
						{
							_config.AutoKits.Add(kit.Name);
							SaveConfig();
						}
					}
					else
					{
						_config.AutoKits.Remove(kit.Name);
						SaveConfig();
					}

					StopEditing(player);

					var perm = kit.Permission.ToLower();
					if (!string.IsNullOrEmpty(perm) && !permission.PermissionExists(perm))
						permission.RegisterPermission(perm, this);

					if (!string.IsNullOrEmpty(kit.Image))
						ImageLibrary?.Call("AddImage", kit.Image, kit.Image);

					SaveKits();
					
					GetOpenedKits(player)?.UpdateKits();

					UpdateUI(player, container =>
					{
						MainKitsContentUI(player, container);
					});
					break;
				}

				case "removekit":
				{
					if (!IsAdmin(player)) return;

					if (!arg.HasArgs(2) ||
					    !int.TryParse(arg.Args[1], out var kitId)) return;

					var kit = FindKitByID(kitId);
					if (kit == null) return;

					DoRemoveKit(kit);
					
					UpdateUI(player, container =>
					{
						MainKitsContentUI(player, container);
					});
					break;
				}

				case "frominv":
				{
					if (!IsAdmin(player)) return;

					if (!arg.HasArgs(2) ||
					    !int.TryParse(arg.Args[1], out var kitId)) return;

					var kit = FindKitByID(kitId);
					if (kit == null) return;

					var kitItems = GetPlayerItems(player);
					if (kitItems == null) return;

					kit.Items = kitItems;

					kit.Update();

					SaveKits();

					InfoKitUi(player, kit);
					break;
				}

				case "refresh_kit":
				{
					var kitId = arg.GetInt(1);
					
					var kit = FindKitByID(kitId);
					if (kit == null) return;

                    UpdateUI(player, container =>
                    {
	                    RefreshKitUi(ref container, player, kit);
                    });
                    
					break;
				}

				case "category":
				{
					var targetCategory = arg.GetString(1);
					if (string.IsNullOrEmpty(targetCategory)) return;

					if (targetCategory == CategoryAll)
						targetCategory = null;
					
					var openedKits = GetOpenedKits(player);
					openedKits.ActiveCategory = targetCategory;
					openedKits.UpdateKits();
                    
					UpdateUI(player, container =>
					{
						MainKitsCategories(player, container);
						
						MainKitsContentUI(player, container);
					});
					break;
				}
			}
		}

		[ConsoleCommand("kits.reset")]
		private void CmdKitsReset(ConsoleSystem.Arg arg)
		{
			if (!(arg.IsServerside || arg.IsAdmin)) return;

			switch (arg.GetString(0))
			{
				case "data":
				{
					var targetID = arg.GetString(1);
					if (string.IsNullOrEmpty(targetID))
					{
						SendReply(arg, "Error syntax! Use: /kits.reset data [<targetID>/all] or /kits.reset data <targetID> <kitName>");
						return;
					}
					
					switch (targetID)
					{
						case "all":
						{
							SendReply(arg, "Resetting all players data...");
							
							DoWipePlayers(count => SendReply(arg, $"{count} players data successfully reset!"));
							break;
						}

						default:
						{
							var targetPlayer = covalence.Players.FindPlayerById(targetID);
							if (targetPlayer == null)
							{
								SendReply(arg, "Player not found!");
								return;
							}
							
							if (arg.Args.Length == 3)
							{
								var kitName = arg.GetString(2);

								var success = PlayerData.ResetKit(targetID, kitName);
								
								SendReply(arg,
									success
										? $"The kit '{kitName}' for player with SteamID {targetID} has been successfully reset!"
										: $"Failed to reset the kit '{kitName}' for player with SteamID {targetID}. Kit may not exist.");
							}
							else
							{
								
								PlayerData.DoWipe(targetID, isWipe: false);
							
								SendReply(arg, $"The data of the player with SteamID {targetID} has been successfully reset!");
							}
							break;
						}
					}
					
					break;
				}

				case "kits":
				{
					_data.Kits.Clear();

					SaveKits();

					SendReply(arg, "You successfully reset all kits!");
					break;
				}

				default:
				{
					SendReply(arg, "Error syntax! Use: /kits.reset [data/kits]");
					break;
				}
			}
		}

		[ConsoleCommand("kits.give")]
		private void CmdKitsGive(ConsoleSystem.Arg arg)
		{
			if (!(arg.IsServerside || arg.IsAdmin)) return;

			if (!arg.HasArgs(2))
			{
				SendReply(arg, $"Error syntax! Use: {arg.cmd.FullName} [name/steamid] [kitname]");
				return;
			}

			var nameOrSteamID = arg.Args[0];
			var target = BasePlayer.Find(nameOrSteamID);
			if (target == null && !nameOrSteamID.IsSteamId())
			{
				SendReply(arg, $"Player '{nameOrSteamID}' not found!");
				return;
			}

			var kitName = arg.Args[1];
			var kit = FindKitByName(kitName);
			if (kit == null)
			{
				SendReply(arg, $"Kit '{kitName}' not found!");
				return;
			}

			kit.Items.ForEach(item => item.Get(target));

			SendReply(arg, $"Player '{nameOrSteamID}' successfully received a kit '{kitName}'");

			Interface.CallHook("OnKitRedeemed", target, kit.Name);
			
			PlayerData.Save(target.UserIDString);

			Log(target, kit.Name);
		}

		[ConsoleCommand("kits.givekit")]
		private void CmdKitsGiveKit(ConsoleSystem.Arg arg)
		{
			if (!(arg.IsServerside || arg.IsAdmin)) return;

			if (!arg.HasArgs(2))
			{
				SendReply(arg, $"Error syntax! Use: {arg.cmd.FullName} [name/steamid] [kitname] [amount]");
				return;
			}

			var steamIdOrName = arg.Args[0];

			var target = BasePlayer.FindAwakeOrSleeping(steamIdOrName);
			if (target == null && !steamIdOrName.IsSteamId())
			{
				SendReply(arg, $"Player '{steamIdOrName}' not found!");
				return;
			}

			var kit = FindKitByName(arg.Args[1]);
			if (kit == null)
			{
				SendReply(arg, $"Kit '{arg.Args[1]}' not found!");
				return;
			}

			var amount = 1;
			if (arg.HasArgs(3))
				int.TryParse(arg.Args[2], out amount);

			var playerData = PlayerData.GetOrCreateKitData(steamIdOrName, kit.Name);
			if (playerData == null) return;

			playerData.HasAmount += amount;

			SendReply(arg, $"Player '{steamIdOrName}' successfully received a kit '{arg.Args[1]}' ({amount} pcs)");

			Log(target, kit.Name);
		}

		[ConsoleCommand("kits.template")]
		private void CmdKitsSetTemplate(ConsoleSystem.Arg arg)
		{
			if (!arg.IsServerside)
			{
				SendReply(arg, "The 'kits.template' command can only be used from the server console.");
				return;
			}

			if (!arg.HasArgs())
			{
				var sb = Pool.Get<StringBuilder>();
				try
				{
					sb.AppendLine("Available Templates:");
					sb.AppendLine("  Fullscreen:");
					sb.AppendLine("    - old");
					sb.AppendLine("    - rust");
					sb.AppendLine("      - normal");
					sb.AppendLine("      - content");
					sb.AppendLine("      - categories");
					sb.AppendLine("      - content_categories");
					sb.AppendLine("  InMenu:");
					sb.AppendLine("    - rust:");
					sb.AppendLine("      - normal (V1/V2)");
					sb.AppendLine("      - content (V1/V2)");
					sb.AppendLine("      - categories (V1/V2)");
					sb.AppendLine("      - content_categories (V1/V2)");
					sb.AppendLine();
					sb.AppendLine("Examples:");
					sb.AppendLine("  kits.template fullscreen old - Sets the fullscreen UI to the old style template.");
					sb.AppendLine("  kits.template fullscreen rust normal - Sets the inmenu UI to the rust normal template.");
					sb.AppendLine("  kits.template fullscreen rust content - Sets the inmenu UI to the rust content template.");
					sb.AppendLine("  kits.template fullscreen rust categories - Sets the inmenu UI to the rust categories template.");
					sb.AppendLine("  kits.template fullscreen rust content_categories - Sets the inmenu UI to the rust content and categories template.");
					sb.AppendLine("  kits.template inmenu rust normal 1 - Sets the inmenu UI to the rust normal template, for ServerPanel template version 1.");
					sb.AppendLine("  kits.template inmenu rust content 2 - Sets the inmenu UI to the rust content template, for ServerPanel template version 2.");
					sb.AppendLine("  kits.template inmenu rust categories 1 - Sets the inmenu UI to the rust categories template, for ServerPanel template version 1.");
					sb.AppendLine("  kits.template inmenu rust content_categories 1 - Sets the inmenu UI to the rust content and categories template, for ServerPanel template version 2.");

					SendReply(arg, sb.ToString());
				}
				finally
				{
					Pool.FreeUnmanaged(ref sb);
				}
				return;
			}
			
			var format = arg.GetString(0);
			var style = arg.GetString(1);
			var rustType = arg.GetString(2);
			var menuVersion = arg.GetInt(3);

			if (!arg.HasArgs(2) || (format != "fullscreen" && format != "inmenu") || (style != "old" && style != "rust"))
			{                
				SendReply(arg, $"Error syntax! Use: {arg.cmd.FullName} [fullscreen/inmenu] [old/rust] [template_type] [menu_version - if inmenu]");
				return;
			}

			if (format == "inmenu" && (!arg.HasArgs(3) || (menuVersion != 1 && menuVersion != 2)))
			{
				SendReply(arg, $"Error syntax! For 'inmenu' you must specify a menu version (V1 or V2): {arg.cmd.FullName} [fullscreen/inmenu] [old/rust] [template_type] [V1/V2]");
				return;
			}
			
			UserInterface targetTemplate = null;
			switch (format)
			{
				case "fullscreen":
					switch (style)
					{
						case "old":
						{
							targetTemplate = UserInterface.GenerateFullscreenTemplateOldStyle();
							break;
						}
					
						case "rust":
						{
							targetTemplate = rustType switch
							{
								"content" => UserInterface.GenerateFullscreenTemplateWithContentsRust(),
								"categories" => UserInterface.GenerateFullscreenTemplateWithCategoriesRust(),
								"content_categories" => UserInterface.GenerateFullscreenTemplateWithContentsAndCategoriesRust(),
								_ => UserInterface.GenerateFullscreenTemplateRust()
							};
							break;
						}
					}

					break;
				case "inmenu":
					switch (style)
					{
						case "old":
						{
							SendReply(arg, "Unfortunately this is not working yet");
							return;
						}
					
						case "rust":
						{
							switch (rustType)
							{
								case "content":
									targetTemplate = menuVersion switch
									{
										1 => UserInterface.GenerateMenuTemplateRustWithContentsV1(),
										2 => UserInterface.GenerateMenuTemplateRustWithContentsV2(),
										_ => null
									};
									break;
								case "categories":
									targetTemplate = menuVersion switch
									{
										1 => UserInterface.GenerateMenuTemplateRustWithCategoriesV1(),
										2 => UserInterface.GenerateMenuTemplateRustWithCategoriesV2(),
										_ => null
									};
									break;
								case "content_categories":
									targetTemplate = menuVersion switch
									{
										1 => UserInterface.GenerateMenuTemplateRustWithContentsAndCategoriesV1(),
										2 => UserInterface.GenerateMenuTemplateRustWithContentsAndCategoriesV2(),
										_ => null
									};
									break;
								default:
									targetTemplate = menuVersion switch
									{
										1 => UserInterface.GenerateMenuTemplateRustV1(),
										2 => UserInterface.GenerateMenuTemplateRustV2(),
										_ => null
									};
									break;
							}
							break;
						}
					}
					break;
			}

			if (targetTemplate == null)
			{
				SendReply(arg, $"Error syntax! Use: {arg.cmd.FullName} [fullscreen/inmenu] [old_style/new_rust]");
				return;
			}

			if (format == "fullscreen")
				_config.UI = targetTemplate;
			else
				_config.MenuUI = targetTemplate;
			
			SaveConfig();

			SendReply(arg, $"'{format}'  UI successfully set to '{style}'!");
		}

		#endregion

		#region Interface

		#region Main UI
		
		private const ulong DEFAULT_MAIN_TARGET_ID = 0;
		private const int DEFAULT_MAIN_PAGE = 0;
		private const bool DEFAULT_MAIN_SHOW_ALL = false;

		private void MainUi(BasePlayer player,
			bool first = false)
		{
			var container = new CuiElementContainer();

			#region Background

			if (first) MainKitsBackground(player, container);

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1"
				},
				Image =
				{
					Color = "0 0 0 0"
				}
			}, Layer + ".Background", Layer + ".Main", Layer + ".Main");

			MainKitsHeader(player, container);

			MainKitsCategories(player, container);

			MainKitsContentUI(player, container, first: true);
			
			MainKitsDescriptionUI(player, ref container);

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private void MainKitsDescriptionUI(BasePlayer player, ref CuiElementContainer container)
		{
			var openedKits = GetOpenedKits(player);
			var targetUI = openedKits.useMainUI ? _config.UI : _config.MenuUI;
			
			var description = openedKits.npcID == 0
				? _config.Description.Description
				: _config.NpcKits.TryGetValue(openedKits.npcID.ToString(), out var npcKit)
					? npcKit.Description
					: string.Empty;

			_config.Description.Get(ref container, Layer + ".Main", null, description);
			switch (targetUI.Style)
			{
				default:
				{
					_config.Description.Get(ref container, Layer + ".Main", null, description);
					break;
				}
			}
		}

		private void MainKitsContentUI(BasePlayer player, CuiElementContainer container, bool first = false)
		{
			var openedKits = GetOpenedKits(player);
			var targetUI = openedKits.useMainUI ? _config.UI : _config.MenuUI;

			container.Add(targetUI.ContentPanel.Background.GetImage(Layer + ".Main", Layer + ".Content", Layer + ".Content"));

			#region Buttons

			container.AddRange(
				targetUI.ContentPanel.ButtonBack.GetButton("<", openedKits.currentPage != 0
						? $"UI_Kits main page {openedKits.currentPage - 1}"
						: "", 
					Layer + ".Content"));
			
			container.AddRange(
				targetUI.ContentPanel.ButtonNext.GetButton(">", openedKits.availableKits.Count >
				                                                (openedKits.currentPage + 1) * openedKits.GetTotalKitsAmount()
						? $"UI_Kits main page {openedKits.currentPage + 1}"
						: "", 
					Layer + ".Content"));
			
			if (IsAdmin(player))
			{
				#region Check Show All

				container.Add(targetUI.ContentPanel.CheckboxShowAllKits.Background.GetImage(Layer + ".Content", Layer + ".Content" + ".Admin.Show.All", Layer + ".Content" + ".Admin.Show.All"));
				
				container.AddRange(targetUI.ContentPanel.CheckboxShowAllKits.CheckboxButton.GetButton(openedKits.showAll ? "✔" : string.Empty, $"UI_Kits main show_all {!openedKits.showAll}", Layer + ".Content" + ".Admin.Show.All", Layer + ".Content" + ".Admin.Show.All.Check", Layer + ".Content" + ".Admin.Show.All.Check"));
				
				CreateOutLine(ref container, Layer + ".Content" + ".Admin.Show.All.Check", targetUI.ContentPanel.CheckboxShowAllKits.CheckboxColor.Get(), targetUI.ContentPanel.CheckboxShowAllKits.CheckboxSize);

				container.Add(targetUI.ContentPanel.CheckboxShowAllKits.Title.GetText(Msg(player, ShowAll),Layer + ".Content" + ".Admin.Show.All.Check"));
				
				#endregion Check Show All

				container.AddRange(
					targetUI.ContentPanel.ButtonCreateKit.GetButton(Msg(player, CreateKit), "UI_Kits editkit True", Layer + ".Content"));
			}

			#endregion
			
			#region No Items

			if (openedKits.availableKits.Count == 0)
				switch (targetUI.Style)
				{
					case InterfaceStyle.NewRust:
					{
						container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "0 25", OffsetMax = "0 -85"
							},
							Text =
							{
								Text = Msg(player, NotAvailableKits),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-bold.ttf",
								FontSize = 20,
								Color = "1 1 1 0.45"
							}
						}, Layer + ".Content");
						return;
					}

					case InterfaceStyle.OldStyle:
					default:
					{
						container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "0 25", OffsetMax = "0 -85"
							},
							Text =
							{
								Text = Msg(player, NotAvailableKits),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-bold.ttf",
								FontSize = 20,
								Color = "1 1 1 0.45"
							}
						}, Layer + ".Content");
						return;
					}
				}

			#endregion No Items

			#region Kit List
			
			MainKitsListUI(player, container);

			#endregion Kit List
		}

		private void MainKitsCategories(BasePlayer player, CuiElementContainer container)
		{
			var openedKits = GetOpenedKits(player);
			var targetUI = openedKits.useMainUI ? _config.UI : _config.MenuUI;
			if (!targetUI.ShowCategories) return;

			var categories = Pool.Get<List<string>>();

			try
			{
				#region LoadAvailableCategories

				categories.Add(CategoryAll);

				for (var i = 0; i < _data.Kits.Count; i++)
				{
					var kit = _data.Kits[i];

					if (string.IsNullOrEmpty(kit.Category) || categories.Contains(kit.Category)) continue;
					
					if (IsAdmin(player) || !kit.Hide && permission.UserHasPermission(player.UserIDString, $"kits.{kit.Category}"))
						categories.Add(kit.Category);
				}

				#endregion LoadAvailableCategories
				
				#region ScrollView

				var totalHeight = categories.Count * targetUI.Categories.Width + (categories.Count - 1) * targetUI.Categories.Margin;

				totalHeight = Mathf.Max(totalHeight, targetUI.Categories.CategoriesScrollMinWidth);
				
				container.Add(new CuiElement
				{
					Name = Layer + ".Categories.ScrollView",
					DestroyUi = Layer + ".Categories.ScrollView",
					Parent = Layer + ".Main",
					Components = 
					{
						new CuiImageComponent { Color = "0 0 0 0" },
						new CuiScrollViewComponent 
						{
							Horizontal = true,
							Vertical = false,
							MovementType = UnityEngine.UI.ScrollRect.MovementType.Clamped,
							Elasticity = 0.25f,
							Inertia = true,
							DecelerationRate = 0.3f,
							ContentTransform = new CuiRectTransform()
							{
								AnchorMin = $"0 0",
								AnchorMax = $"0 1",
								OffsetMin = $"0 0",
								OffsetMax = $"{totalHeight} 0"
							},
							ScrollSensitivity = 14.0f,
							HorizontalScrollbar = new CuiScrollbar 
							{
								Invert = true,
								AutoHide = true,
								HighlightColor = HexToCuiColor("#D74933"),
								HandleColor = HexToCuiColor("#D74933"),
								PressedColor = HexToCuiColor("#D74933"),
								TrackColor = HexToCuiColor("#373737"),
								Size = 2,
								HandleSprite = "assets/content/ui/UI.Background.Tile.psd"
							}
						},
						targetUI.Categories.Background.GetRectTransform()
					}
				});

				#endregion

				#region Categories Loop

				var activeCat = string.IsNullOrEmpty(openedKits.ActiveCategory) ? CategoryAll : openedKits.ActiveCategory;

				for (var i = 0; i < categories.Count; i++)
				{
					var targetCategory = categories[i];
					var isActiveCat = targetCategory == activeCat;

					var categoryLayer = Layer + $".Content.Cat.{i}";
					
					var offsetLeft = i * targetUI.Categories.Width + i * targetUI.Categories.Margin;

					container.AddRange((isActiveCat
						? targetUI.Categories.ActiveCategory
						: targetUI.Categories.InactiveCategory).GetButton(Msg(player, targetCategory), isActiveCat ? string.Empty : $"UI_Kits category {targetCategory}", 
						Layer + ".Categories.ScrollView", categoryLayer, categoryLayer, 
						rect: new CuiRectTransformComponent()
						{
							AnchorMin = "0 1", AnchorMax = "0 1", 
							OffsetMin = $"{offsetLeft} -{targetUI.Categories.Height}", OffsetMax = $"{offsetLeft + targetUI.Categories.Width} 0"
						}));
				}

				#endregion
			}
			finally
			{
				Pool.FreeUnmanaged(ref categories);
			}
		}
		
		private void MainKitsListUI(BasePlayer player, CuiElementContainer container)
		{
			var openedKits = GetOpenedKits(player);
			var targetUI = openedKits.useMainUI ? _config.UI : _config.MenuUI;

			var totalAmount = openedKits.GetTotalKitsAmount();
			var isAdmin = IsAdmin(player);

			ShowGridUI(container, 0,
				openedKits.kitsToUpdate.Count,
				targetUI.KitsOnString,
				targetUI.Margin,
				targetUI.Margin,
				targetUI.KitWidth,
				targetUI.KitHeight,
				targetUI.LeftIndent,
				targetUI.YIndent,
				0, 0, 1, 1, "0 0 0 0", Layer + ".Content",
				index => Layer + $".Kit.{openedKits.kitsToUpdate[index].ID}.Main",
				index => Layer + $".Kit.{openedKits.kitsToUpdate[index].ID}.Main",
				index => KitUI(player, container, openedKits.kitsToUpdate[index], openedKits.currentPage, totalAmount,
					index, isAdmin, openedKits.showAll));
		}

		private void KitUI(BasePlayer player,
			CuiElementContainer container,
			Kit kit,
			int currentPage,
			int totalAmount,
			int kitIndex,
			bool isAdmin,
			bool kitsShowAll)
		{
			var openedKits = GetOpenedKits(player);
			var targetUI = openedKits.useMainUI ? _config.UI : _config.MenuUI;

			container.Add(targetUI.KitPanel.Background.GetImage(Layer + $".Kit.{kit.ID}.Main", 
				Layer + $".Kit.{kit.ID}.Main.Background",
				Layer + $".Kit.{kit.ID}.Main.Background"));

			if (targetUI.KitPanel.DisplayKitContents)
			{
				#region Name

				if (targetUI.KitPanel.ShowName)
					container.Add(targetUI.KitPanel.KitName.GetText(kit.DisplayName ?? string.Empty,
						Layer + $".Kit.{kit.ID}.Main.Background"));

				#endregion

				#region Move Buttons

				if (isAdmin && kitsShowAll)
				{
					AddMoveButton(container, Layer + $".Kit.{kit.ID}.Main.Background", "left", -20,
						$"UI_Kits edit_kit_position {kit.ID} left");

					AddMoveButton(container, Layer + $".Kit.{kit.ID}.Main.Background", "right", -2,
						$"UI_Kits edit_kit_position {kit.ID} right");
					
					container.Add(new CuiButton
						{
							RectTransform =
							{
								AnchorMin = "1 1", AnchorMax = "1 1",
								OffsetMin = $"0 -53", OffsetMax = $"10 -38"
							},
							Text =
							{
								Text = "✎",
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-regular.ttf",
								FontSize = 7,
								Color = HexToCuiColor("#71B8ED"), 
							},
							Button =
							{
								Command = $"UI_Kits editkit {false} {kit.ID}",
								Color = HexToCuiColor("#71B8ED", 20)
							}
						}, Layer + $".Kit.{kit.ID}.Main.Background");
				}

				#endregion
			}
			else
			{
				#region Number

				if (targetUI.KitPanel.ShowNumber)
				{
					var number = currentPage * totalAmount + kitIndex + 1;

					container.Add(targetUI.KitPanel.KitNumber.GetText($"#{number}",
						Layer + $".Kit.{kit.ID}.Main.Background"));
				}

				#endregion

				#region Image

				if (_enabledImageLibrary && !string.IsNullOrEmpty(kit.Image))
					container.Add(new CuiElement
					{
						Parent = Layer + $".Kit.{kit.ID}.Main.Background",
						Components =
						{
							new CuiRawImageComponent {Png = GetImage(kit.Image)},
							new CuiRectTransformComponent
							{
								AnchorMin = targetUI.KitPanel.KitImage.AnchorMin,
								AnchorMax = targetUI.KitPanel.KitImage.AnchorMax,
								OffsetMin = targetUI.KitPanel.KitImage.OffsetMin,
								OffsetMax = targetUI.KitPanel.KitImage.OffsetMax
							}
						}
					});

				#endregion

				#region Line

				if (targetUI.KitPanel.ShowLine)
					container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = targetUI.KitPanel.KitLine.AnchorMin,
								AnchorMax = targetUI.KitPanel.KitLine.AnchorMax,
								OffsetMin = targetUI.KitPanel.KitLine.OffsetMin,
								OffsetMax = targetUI.KitPanel.KitLine.OffsetMax
							},
							Image = {Color = HexToCuiColor(kit.Color)}
						}, Layer + $".Kit.{kit.ID}.Main.Background");

				#endregion

				#region Name

				if (targetUI.KitPanel.ShowName)
					container.Add(targetUI.KitPanel.KitName.GetText(kit.DisplayName ?? string.Empty,
						Layer + $".Kit.{kit.ID}.Main.Background"));

				#endregion

				#region Button.Take

				container.AddRange(
					(isAdmin && kitsShowAll || kit.ShowInfo
						? targetUI.KitPanel.KitButtonTakeWhenShowInfo
						: targetUI.KitPanel.KitButtonTake).GetButton(Msg(player, KitTake), $"UI_Kits givekit {kit.ID}",
						Layer + $".Kit.{kit.ID}.Main.Background"));

				#endregion

				#region Button.Info

				if (isAdmin && kitsShowAll || kit.ShowInfo)
					container.AddRange(targetUI.KitPanel.KitButtonInfo.GetButton(Msg(player, KitInfo),
						$"UI_Kits infokit {kit.ID}",
						Layer + $".Kit.{kit.ID}.Main.Background"));

				#endregion

				#region Move Buttons

				if (isAdmin && kitsShowAll)
				{
					AddMoveButton(container, Layer + $".Kit.{kit.ID}.Main.Background", "left", -20,
						$"UI_Kits edit_kit_position {kit.ID} left");

					AddMoveButton(container, Layer + $".Kit.{kit.ID}.Main.Background", "right", -2,
						$"UI_Kits edit_kit_position {kit.ID} right");
				}

				#endregion
			}
			
			RefreshKitUi(ref container, player, kit);
		}

		private void MainKitsHeader(BasePlayer player, CuiElementContainer container)
		{
			var openedKits = GetOpenedKits(player);
			var targetUI = openedKits.useMainUI ? _config.UI : _config.MenuUI;

			container.Add(targetUI.HeaderPanel.Background.GetImage(Layer + ".Main", Layer + ".Header", Layer + ".Header"));
			container.Add(targetUI.HeaderPanel.Title.GetText(Msg(player, MainTitle),  Layer + ".Header"));
			
			if (targetUI.HeaderPanel.ShowLine) container.Add(targetUI.HeaderPanel.Line.GetImage(Layer + ".Header"));
			
			if (openedKits.useMainUI)
				container.AddRange(targetUI.HeaderPanel.ButtonClose.GetButton(Msg(player, Close), "UI_Kits close", Layer + ".Header", close: Layer));
		}

		private void MainKitsBackground(BasePlayer player, CuiElementContainer container)
		{
			var openedKits = GetOpenedKits(player);
			var targetUI = openedKits.useMainUI ? _config.UI : _config.MenuUI;

			container.Add(targetUI.MainBackground.GetImage("Overlay", Layer, Layer));

			container.Add(new CuiElement
			{
				Parent = Layer,
				Components = 
				{
					new CuiButtonComponent
					{
						Color = "0 0 0 0",
						Close = Layer,
						Command = "UI_Kits close"
					}
				}
			});
			
			container.Add(targetUI.MainPanel.GetImage(Layer, Layer + ".Background", Layer + ".Background"));
		}
		
		#endregion

		#region Kit Info UI

		private void InfoKitUi(BasePlayer player, Kit kit)
		{
			var openedKits = GetOpenedKits(player);
			var targetUI = openedKits.useMainUI ? _config.UI : _config.MenuUI;

			var container = new CuiElementContainer();

			switch (targetUI.Style)
			{
				case InterfaceStyle.NewRust:
				{
					#region Fields
					
					var Size = 70f;
					var Margin = 10f;

					var amountOnString = 6;

					#endregion
					
					#region Background

					container.Add(new CuiPanel
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Image =
						{
							Color = HexToCuiColor("#000000", 90),
							Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
						},
						CursorEnabled = true
					}, "Overlay", InfoLayer, InfoLayer);

					#endregion

					#region Header

					container.Add(new CuiElement
					{
						Parent = InfoLayer,
						Components = {
							new CuiTextComponent
							{
								Text = Msg(player, UI_MeventRust_InfoKit_Title), 
								Font = "robotocondensed-bold.ttf",
								FontSize = 24,
								Align = TextAnchor.MiddleLeft, 
								Color = HexToCuiColor("#E8DDD5")
							},
							new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-250 225", OffsetMax = "220 265" }
						}
					});

					if (IsAdmin(player))
					{
						container.Add(new CuiButton()
						{
							Text = { Text = Msg(player, Edit), Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#71B8ED") },
							Button =
							{
								Command = $"UI_Kits editkit {false} {kit.ID}",
								Color = HexToCuiColor("#71B8ED", 10), 
								Material = "assets/content/ui/namefontmaterial.mat",
								Sprite = "assets/content/ui/UI.Background.Tile.psd"
							},
							RectTransform ={ AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "70 230", OffsetMax = "185 260" }
						},InfoLayer);
					}

					#region Button.Close
					
					var buttonClose = container.Add(new CuiButton()
					{
						Text = { Text = string.Empty },
						Button =
						{
							Color = targetUI.ColorTwo.Get(), 
							Material = "assets/content/ui/namefontmaterial.mat",
							Command = "UI_Kits stopedit",
							Close = InfoLayer
						},
						RectTransform ={ AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "190 230", OffsetMax = "220 260" }
					},InfoLayer);
					
					#region Button.Close Icon
					
					container.Add(new CuiElement
					{
						Parent = buttonClose,
						Components = {
							new CuiImageComponent { Color = targetUI.ColorOne.Get(), Sprite = "assets/icons/close.png" },
							new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8 -8", OffsetMax = "8 8" }
						}
					});

					#endregion Button.Close Icon
					
					#endregion Button.Close
					
					#endregion

					#region Items

					#region Main Container

					var itemContainer = "main";
					
					container.Add(new CuiPanel
						{
							Image = {Color = "0 0 0 0"},
							RectTransform =
							{
								AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-250 -85", OffsetMax = "220 225"
							}
						}, InfoLayer, InfoLayer + $".Container.{itemContainer}.Section", InfoLayer + $".Container.{itemContainer}.Section");

					ShowGridUI(container, 0,
						amountOnString * 4,
						amountOnString,
						Margin,
						Margin,
						Size,
						Size,
						0,
						0,
						0, 0, 1, 1, "0 0 0 0", InfoLayer + $".Container.{itemContainer}.Section",
						slot => InfoLayer + $".Container.{itemContainer}.Section.Item.{slot}",
						slot => InfoLayer + $".Container.{itemContainer}.Section.Item.{slot}",
						slot => InfoKitItemUI(player, kit, container, slot, itemContainer));

					#endregion

					#region Wear Container

					itemContainer = "wear";
					amountOnString = 7;
					
					container.Add(new CuiPanel
						{
							Image = {Color = "0 0 0 0"},
							RectTransform =
							{
								AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-275 -175", OffsetMax = "275 -105"
							}
						}, InfoLayer, InfoLayer + $".Container.{itemContainer}.Section", InfoLayer + $".Container.{itemContainer}.Section");

					ShowGridUI(container, 
						0,
						amountOnString,
						amountOnString,
						Margin,
						Margin,
						Size,
						Size,
						0f,
						0,
						0, 0, 1, 1, "0 0 0 0", 
						InfoLayer + $".Container.{itemContainer}.Section",
						slot => InfoLayer + $".Container.{itemContainer}.Section.Item.{slot}",
						slot => InfoLayer + $".Container.{itemContainer}.Section.Item.{slot}",
						slot => InfoKitItemUI(player, kit, container, slot, itemContainer));

					#endregion
					
					#region Belt Container

					itemContainer = "belt";
					amountOnString = 6;
					
					container.Add(new CuiPanel
						{
							Image = {Color = "0 0 0 0"},
							RectTransform =
							{
								AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-235 -265", OffsetMax = "235 -195"
							}
						}, InfoLayer, InfoLayer + $".Container.{itemContainer}.Section", InfoLayer + $".Container.{itemContainer}.Section");

					ShowGridUI(container, 0,
						amountOnString,
						amountOnString,
						Margin,
						Margin,
						Size,
						Size,
						0f,
						0,
						0, 0, 1, 1, "0 0 0 0", InfoLayer + $".Container.{itemContainer}.Section",
						slot => InfoLayer + $".Container.{itemContainer}.Section.Item.{slot}",
						slot => InfoLayer + $".Container.{itemContainer}.Section.Item.{slot}",
						slot => InfoKitItemUI(player, kit, container, slot, itemContainer));

					#endregion

					#endregion
					break;
				}

				default:
				{
					#region Fields

					var Size = 70f;
					var Margin = 5f;

					var ySwitch = -125f;
					var amountOnString = 6;
					var constSwitch = -(amountOnString * Size + (amountOnString - 1) * Margin) / 2f;

					var total = 0;

					#endregion

					#region Background

					container.Add(new CuiPanel
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Image =
						{
							Color = targetUI.ColorFive.Get(),
							Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",

						}
					}, "Overlay", InfoLayer, InfoLayer);

					#endregion

					#region Header

					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = "112.5 -140", OffsetMax = "222.5 -115"
						},
						Text =
						{
							Text = Msg(player, ComeBack),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 10,
							Color = "1 1 1 1"
						},
						Button =
						{
							Color = targetUI.ColorThree.Get(),
							Command = "UI_Kits stopedit",
							Close = InfoLayer
						}
					}, InfoLayer);

					#region Change Button

					if (IsAdmin(player))
					{
						container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "0.5 1", AnchorMax = "0.5 1",
								OffsetMin = "-12.5 -140", OffsetMax = "102.5 -115"
							},
							Image = {Color = "0 0 0 0"}
						}, InfoLayer, InfoLayer + ".Btn.Change");

						CreateOutLine(ref container, InfoLayer + ".Btn.Change", targetUI.ColorThree.Get(), 1);

						container.Add(new CuiButton
						{
							RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
							Text =
							{
								Text = Msg(player, Edit),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-regular.ttf",
								FontSize = 10,
								Color = "1 1 1 1"
							},
							Button =
							{
								Color = "0 0 0 0",
								Command = $"UI_Kits editkit {false} {kit.ID}",
								Close = InfoLayer
							}
						}, InfoLayer + ".Btn.Change");
					}

					#endregion

					#endregion

					#region Main

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = $"{constSwitch} {ySwitch - 15f}", OffsetMax = $"0 {ySwitch}"
						},
						Text =
						{
							Text = Msg(player, ContainerMain),
							Align = TextAnchor.MiddleLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = "1 1 1 1"
						}
					}, InfoLayer);

					ySwitch -= 20f;

					var xSwitch = constSwitch;

					for (var slot = 0; slot < amountOnString * 4; slot++)
					{
						var kitItem = kit.GetItemByContainerAndPosition("main", slot);

						InfoItemUi(ref container, player,
							slot,
							$"{xSwitch} {ySwitch - Size}",
							$"{xSwitch + Size} {ySwitch}",
							kit,
							kitItem,
							total,
							"main");

						if ((slot + 1) % amountOnString == 0)
						{
							xSwitch = constSwitch;
							ySwitch = ySwitch - Size - Margin;
						}
						else
						{
							xSwitch += Size + Margin;
						}

						total++;
					}

					#endregion

					#region Wear

					ySwitch -= 5f;

					amountOnString = 7;

					constSwitch = -(amountOnString * Size + (amountOnString - 1) * Margin) / 2f;

					if (kit.UseSlotForBackpack) amountOnString++;
					// constSwitch -= Size + Margin * 3;
					xSwitch = constSwitch;

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = $"{constSwitch} {ySwitch - 15f}", OffsetMax = $"0 {ySwitch}"
						},
						Text =
						{
							Text = Msg(player, ContainerWear),
							Align = TextAnchor.MiddleLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = "1 1 1 1"
						}
					}, InfoLayer);

					ySwitch -= 20f;

					for (var slot = 0; slot < amountOnString; slot++)
					{
						var kitItem = kit.GetItemByContainerAndPosition("wear", slot);

						InfoItemUi(ref container, player,
							slot,
							$"{xSwitch} {ySwitch - Size}",
							$"{xSwitch + Size} {ySwitch}",
							kit,
							kitItem,
							total,
							"wear");

						if ((slot + 1) % amountOnString == 0)
						{
							xSwitch = constSwitch;
							ySwitch = ySwitch - Size - Margin;
						}
						else
						{
							if (slot == ItemContainer.BackpackSlotIndex - 1)
								xSwitch += Size + Margin * 3;
							else
								xSwitch += Size + Margin;
						}

						total++;
					}

					#endregion

					#region Belt

					ySwitch -= 5f;

					amountOnString = 6;

					constSwitch = -(amountOnString * Size + (amountOnString - 1) * Margin) / 2f;

					xSwitch = constSwitch;

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = $"{constSwitch} {ySwitch - 15f}", OffsetMax = $"0 {ySwitch}"
						},
						Text =
						{
							Text = Msg(player, ContainerBelt),
							Align = TextAnchor.MiddleLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = "1 1 1 1"
						}
					}, InfoLayer);

					ySwitch -= 20f;

					for (var slot = 0; slot < amountOnString; slot++)
					{
						var kitItem = kit.GetItemByContainerAndPosition("belt", slot);

						InfoItemUi(ref container, player,
							slot,
							$"{xSwitch} {ySwitch - Size}",
							$"{xSwitch + Size} {ySwitch}",
							kit,
							kitItem,
							total,
							"belt");

						if ((slot + 1) % amountOnString == 0)
						{
							xSwitch = constSwitch;
							ySwitch = ySwitch - Size - Margin;
						}
						else
						{
							xSwitch += Size + Margin;
						}

						total++;
					}

					#endregion
					break;
				}
			}
			
			#region Description

			_config.InfoKitDescription.Get(ref container, InfoLayer, null, kit.Description);

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private void InfoKitItemUI(BasePlayer player, Kit kit, CuiElementContainer container, int slot, string itemContainer)
		{
			var targetUI = GetOpenedKits(player).useMainUI ? _config.UI : _config.MenuUI;
			
			container.Add(new CuiPanel()
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1"
					},
					Image =
					{
						Color = targetUI.ColorThree.Get(),
						Material = "assets/content/ui/namefontmaterial.mat",
						Sprite = "assets/content/ui/UI.Background.Tile.psd"
					}
				}, 
				InfoLayer + $".Container.{itemContainer}.Section.Item.{slot}",
				InfoLayer + $".Container.{itemContainer}.Section.Item.{slot}.Panel",
				InfoLayer + $".Container.{itemContainer}.Section.Item.{slot}.Panel");

			var kitItem = kit.GetItemByContainerAndPosition(itemContainer, slot);
			if (kitItem != null)
			{
				container.Add(kitItem.GetImage("0 0", "1 1", "10 10", "-10 -10",
					InfoLayer + $".Container.{itemContainer}.Section.Item.{slot}.Panel"));

				container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "2.5 3.5", OffsetMax = "-2.5 -2.5"
						},
						Text =
						{
							Text = $"x{kitItem.Amount}",
							Align = TextAnchor.LowerRight,
							Font = "robotocondensed-regular.ttf",
							FontSize = 10,
							Color = "1 1 1 1"
						}
					}, InfoLayer + $".Container.{itemContainer}.Section.Item.{slot}.Panel");

				var color = _config.RarityColors.Find(x => x.Chance == kitItem.Chance);
				if (color != null)
				{
					container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 0",
								OffsetMin = "0 0", OffsetMax = "0 2"
							},
							Image =
							{
								Color = HexToCuiColor(color.Color)
							}
						}, InfoLayer + $".Container.{itemContainer}.Section.Item.{slot}.Panel");

					container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "2.5 2.5", OffsetMax = "-2.5 -2.5"
							},
							Text =
							{
								Text = $"{kitItem.Chance}%",
								Align = TextAnchor.UpperLeft,
								Font = "robotocondensed-regular.ttf",
								FontSize = 10,
								Color = "1 1 1 1"
							}
						}, InfoLayer + $".Container.{itemContainer}.Section.Item.{slot}.Panel");
				}
			}

			
			if (IsAdmin(player))
				container.Add(new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text = {Text = string.Empty},
					Button =
					{
						Color = "0 0 0 0",
						Command =
							$"UI_Kits edit_item start {itemContainer} {kit.ID} {slot}",
						Close = InfoLayer
					}
				}, InfoLayer + $".Container.{itemContainer}.Section.Item.{slot}.Panel");
		}
		
		#endregion Kit Info UI
		
		#region Editing UI

		private const float
			Kit_Editing_Height = 40f,
			Kit_Editing_Width = 225f,
			Kit_Editing_Margin_X = 35f,
			Kit_Editing_Margin_Y = 10f;

		private void EditingKitUi(BasePlayer player, bool creating, int kitId = -1)
		{
			#region Dictionary

			if (!_kitEditing.ContainsKey(player.userID))
			{
				if (kitId != -1)
				{
					var kit = FindKitByID(kitId);
					if (kit == null) return;

					_kitEditing.Add(player.userID, new Dictionary<string, object>
					{
						["Name"] = kit.Name,
						["DisplayName"] = kit.DisplayName,
						["Color"] = kit.Color,
						["Permission"] = kit.Permission,
						["Description"] = kit.Description,
						["Category"] = kit.Category,
 						["Image"] = kit.Image,
						["Hide"] = kit.Hide,
						["ShowInfo"] = kit.ShowInfo,
						["Amount"] = kit.Amount,
						["Cooldown"] = kit.Cooldown,
						["CooldownAfterWipe"] = kit.CooldownAfterWipe,
						["Sale"] = kit.Sale,
						["Price"] = kit.Price,
						["AutoKit"] = _config.AutoKits.Contains(kit.Name),
						["UseCommandsOnReceiving"] = kit.UseCommandsOnReceiving,
						["CommandsOnReceiving"] = kit.CommandsOnReceiving,
						["UseSlotForBackpack"] = kit.UseSlotForBackpack
					});
				}
				else
				{
					_kitEditing.Add(player.userID, new Dictionary<string, object>
					{
						["Name"] = CuiHelper.GetGuid(),
						["DisplayName"] = "My Kit",
						["Color"] = _config.KitColor,
						["Permission"] = $"{Name}.default",
						["Description"] = string.Empty,
						["Category"] = string.Empty,
						["Image"] = string.Empty,
						["Hide"] = true,
						["ShowInfo"] = true,
						["Amount"] = 0,
						["Cooldown"] = 0.0,
						["CooldownAfterWipe"] = 0.0,
						["Sale"] = false,
						["Price"] = 0,
						["AutoKit"] = false,
						["UseCommandsOnReceiving"] = false,
						["CommandsOnReceiving"] = string.Empty,
						["UseSlotForBackpack"] = true
					});
				}
			}

			#endregion

			var container = new CuiElementContainer();

			var totalHeight = Mathf.RoundToInt(Mathf.Max(_kitEditing[player.userID].Count / 2, 1) *
				(Kit_Editing_Height + Kit_Editing_Margin_Y) + 150f);

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image =
				{
					Color = HexToCuiColor("#161617", 80)
				}
			}, "Overlay", EditingLayer, EditingLayer);

			container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = $"-260 -{totalHeight / 2f}",
						OffsetMax = $"260 {totalHeight / 2f}"
					},
					Image =
					{
						Color = HexToCuiColor("#0E0E10")
					}
				}, EditingLayer, $"{EditingLayer}.Main", $"{EditingLayer}.Main");

			#endregion

			#region Header

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -50",
					OffsetMax = "0 0"
				},
				Image = {Color = HexToCuiColor("#161617")}
			}, EditingLayer + ".Main", EditingLayer + ".Header");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "10 0",
					OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, CreateOrEditKit),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = HexToCuiColor("#FFFFFF")
				}
			}, EditingLayer + ".Header");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-35 -37.5",
					OffsetMax = "-10 -12.5"
				},
				Text =
				{
					Text = Msg(player, Close),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 10,
					Color = HexToCuiColor("#FFFFFF")
				},
				Button =
				{
					Close = EditingLayer,
					Color = HexToCuiColor("#4B68FF"),
					Command = "UI_Kits stopedit"
				}
			}, EditingLayer + ".Header");

			#endregion

			#region Fields

			var ySwitch = -60f;

			var i = 1;
			foreach (var (param, value) in _kitEditing[player.userID])
			{
				var xSwitch = i % 2 == 0 ? Kit_Editing_Margin_X / 2f : -Kit_Editing_Width - Kit_Editing_Margin_X / 2f;

				var title = param.GetFieldTitle<Kit>();

				if (value is bool boolValue)
					EditBoolFieldUi(player, ref container, EditingLayer + ".Main", EditingLayer + $".Editing.{i}",
						$"{xSwitch} {ySwitch - Kit_Editing_Height}",
						$"{xSwitch + Kit_Editing_Width} {ySwitch}",
						$"UI_Kits editkit {creating} {kitId} {param} ",
						title,
						boolValue);
				else
					EditFieldUi(player, ref container, EditingLayer + ".Main", EditingLayer + $".Editing.{i}",
						$"{xSwitch} {ySwitch - Kit_Editing_Height}",
						$"{xSwitch + Kit_Editing_Width} {ySwitch}",
						$"UI_Kits editkit {creating} {kitId} {param} ",
						title,
						value);

				if (i % 2 == 0) ySwitch = ySwitch - Kit_Editing_Height - Kit_Editing_Margin_Y;

				i++;
			}

			#endregion

			#region Buttons

			#region Save Kit

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "0 0",
					OffsetMin = "15 10",
					OffsetMax = "115 35"
				},
				Text =
				{
					Text = Msg(player, SaveKit),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = HexToCuiColor("#4B68FF"),
					Command = $"UI_Kits savekit {creating} {kitId}",
					Close = EditingLayer
				}
			}, EditingLayer + ".Main");

			#endregion

			#region Add From Inventory

			if (!creating)
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0",
						OffsetMin = "-100 10",
						OffsetMax = "100 35"
					},
					Text =
					{
						Text = Msg(player, CopyItems),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = HexToCuiColor("#50965F"),
						Command = $"UI_Kits frominv {kitId}",
						Close = EditingLayer
					}
				}, EditingLayer + ".Main");

			#endregion

			#region Remove Kit

			if (!creating)
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 0", AnchorMax = "1 0",
						OffsetMin = "-115 10",
						OffsetMax = "-15 35"
					},
					Text =
					{
						Text = Msg(player, RemoveKit),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = HexToCuiColor("#FF4B4B"),
						Command = $"UI_Kits removekit {kitId}",
						Close = EditingLayer
					}
				}, EditingLayer + ".Main");

			#endregion

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private void EditingItemUi(BasePlayer player, int kitId, int slot, string itemContainer,
			bool First = false)
		{
			var container = new CuiElementContainer();

			#region Dictionary

			if (!_itemEditing.ContainsKey(player.userID))
			{
				var kit = FindKitByID(kitId);
				if (kit == null) return;

				var item = kit.GetItemByContainerAndPosition(itemContainer, slot);
				if (item != null)
					_itemEditing.Add(player.userID, new Dictionary<string, object>
					{
						["Type"] = item.Type,
						["Command"] = item.Command,
						["Container"] = item.Container,
						["ShortName"] = item.ShortName,
						["DisplayName"] = item.DisplayName,
						["Amount"] = item.Amount,
						["Blueprint"] = item.Blueprint,
						["SkinID"] = item.SkinID,
						["Chance"] = item.Chance,
						["Position"] = item.Position
					});
				else
					_itemEditing.Add(player.userID, new Dictionary<string, object>
					{
						["Type"] = KitItemType.Item,
						["Container"] = itemContainer,
						["Command"] = string.Empty,
						["ShortName"] = string.Empty,
						["DisplayName"] = string.Empty,
						["Amount"] = 1,
						["Blueprint"] = 0,
						["SkinID"] = 0UL,
						["Chance"] = 100,
						["Position"] = slot
					});
			}

			#endregion

			var edit = _itemEditing[player.userID];

			#region Background

			if (First)
				container.Add(new CuiPanel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Image = {Color = HexToCuiColor("#161617", 80)},
					CursorEnabled = true
				}, "Overlay", EditingLayer, EditingLayer);

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-260 -240",
					OffsetMax = "260 250"
				},
				Image =
				{
					Color = HexToCuiColor("#0E0E10")
				}
			}, EditingLayer, EditingLayer + ".Main", EditingLayer + ".Main");

			#region Header

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -50",
					OffsetMax = "0 0"
				},
				Image = {Color = HexToCuiColor("#161617")}
			}, EditingLayer + ".Main", EditingLayer + ".Header");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "10 0",
					OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, EditingTitle),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = HexToCuiColor("#FFFFFF")
				}
			}, EditingLayer + ".Header");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-35 -37.5",
					OffsetMax = "-10 -12.5"
				},
				Text =
				{
					Text = Msg(player, Close),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 10,
					Color = HexToCuiColor("#FFFFFF")
				},
				Button =
				{
					Close = EditingLayer,
					Color = HexToCuiColor("#4B68FF"),
					Command = $"UI_Kits edit_item close {kitId}"
				}
			}, EditingLayer + ".Header");

			#endregion

			#region Type

			var type = edit["Type"] as KitItemType? ?? KitItemType.Item;

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "10 -110",
					OffsetMax = "115 -80"
				},
				Text =
				{
					Text = Msg(player, ItemName),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = type == KitItemType.Item ? HexToCuiColor("#4B68FF") : HexToCuiColor("#4B68FF", 50),
					Command = $"UI_Kits edititem {itemContainer} {kitId} {slot} Type {KitItemType.Item}"
				}
			}, EditingLayer + ".Main");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "135 -110",
					OffsetMax = "240 -80"
				},
				Text =
				{
					Text = Msg(player, CmdName),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = type == KitItemType.Command ? HexToCuiColor("#4B68FF") : HexToCuiColor("#4B68FF", 50),
					Command = $"UI_Kits edititem {itemContainer} {kitId} {slot} Type {KitItemType.Command}"
				}
			}, EditingLayer + ".Main");

			#endregion

			#region Command

			EditFieldUi(player, ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"-240 -110",
				"0 -60",
				$"UI_Kits edititem {itemContainer} {kitId} {slot} Command ",
				"Command", edit["Command"]);

			#endregion

			#region Item

			var shortName = (string) edit["ShortName"];

			#region Image

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-240 -265", OffsetMax = "-105 -130"
				},
				Image = {Color = HexToCuiColor("#161617")}
			}, EditingLayer + ".Main", EditingLayer + ".Image");

			if (!string.IsNullOrEmpty(shortName))
				container.Add(new CuiElement
				{
					Parent = EditingLayer + ".Image",
					Components =
					{
						new CuiImageComponent
						{
							ItemId = ItemManager.FindItemDefinition(shortName)?.itemid ?? 0,
							SkinId = (ulong) edit["SkinID"]
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "10 10", OffsetMax = "-10 -10"
						}
					}
				});

			#endregion

			#region ShortName

			EditFieldUi(player, ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"-85 -190",
				"70 -130",
				$"UI_Kits edititem {itemContainer} {kitId} {slot} ShortName ",
				"ShortName", edit["ShortName"]);

			#endregion

			#region Skin

			EditFieldUi(player, ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"85 -190",
				"240 -130",
				$"UI_Kits edititem {itemContainer} {kitId} {slot} SkinID ",
				"SkinID", edit["SkinID"]);

			#endregion

			#region Select Item

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-85 -265",
					OffsetMax = "55 -235"
				},
				Text =
				{
					Text = Msg(player, BtnSelect),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = HexToCuiColor("#4B68FF"),
					Command = $"UI_Kits selectitem {itemContainer} {kitId} {slot}"
				}
			}, EditingLayer + ".Main");

			#endregion

			#region Blueprint

			var bp = edit["Blueprint"] as int? ?? 0;
			CheckBoxUi(ref container,
				EditingLayer + ".Main",
				CuiHelper.GetGuid(),
				"0.5 1", "0.5 1",
				"65 -255",
				"75 -245",
				bp == 1,
				$"UI_Kits edititem {itemContainer} {kitId} {slot} Blueprint {(bp == 0 ? 1 : 0)}",
				Msg(player, BluePrint)
			);

			#endregion

			#region Amount

			EditFieldUi(player, ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"-240 -345",
				"-7.5 -285",
				$"UI_Kits edititem {itemContainer} {kitId} {slot} Amount ",
				"Amount", edit["Amount"]);

			#endregion

			#region Chance

			EditFieldUi(player, ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"7.5 -345",
				"240 -285",
				$"UI_Kits edititem {itemContainer} {kitId} {slot} Chance ",
				"Chance", edit["Chance"]);

			#endregion

			#region Display Name

			EditFieldUi(player, ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"-240 -425",
				"240 -365",
				$"UI_Kits edititem {itemContainer} {kitId} {slot} DisplayName ",
				"DisplayName",
				edit["DisplayName"]);

			#endregion

			#endregion

			#region Save Button

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 0", AnchorMax = "0.5 0",
					OffsetMin = "-90 10",
					OffsetMax = $"{(slot == -1 ? 90 : 55)} 40"
				},
				Text =
				{
					Text = Msg(player, BtnSave),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = HexToCuiColor("#4B68FF"),
					Command = $"UI_Kits saveitem {kitId} {slot} {itemContainer}",
					Close = EditingLayer
				}
			}, EditingLayer + ".Main");

			#endregion

			#region Remove Item

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 0", AnchorMax = "0.5 0",
					OffsetMin = "60 10",
					OffsetMax = "90 40"
				},
				Text =
				{
					Text = Msg(player, RemoveItem),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = HexToCuiColor("#FF4B4B"),
					Command = $"UI_Kits removeitem {kitId} {slot} {itemContainer}",
					Close = EditingLayer
				}
			}, EditingLayer + ".Main");

			#endregion

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private void SelectItem(BasePlayer player, int kitId, int slot, string itemContainer,
			string selectedCategory = "", int page = 0, string input = "")
		{
			if (string.IsNullOrEmpty(selectedCategory)) selectedCategory = _itemsCategories.FirstOrDefault().Key;

			var container = new CuiElementContainer();

			#region Background

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text = {Text = string.Empty},
				Button =
				{
					Close = ModalLayer,
					Color = HexToCuiColor("#161617", 80)
				}
			}, "Overlay", ModalLayer, ModalLayer);

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-260 -270",
					OffsetMax = "260 280"
				},
				Image =
				{
					Color = HexToCuiColor("#0E0E10")
				}
			}, ModalLayer, ModalLayer + ".Main");

			#region Categories

			var amountOnString = 4;
			var Width = 120f;
			var Height = 25f;
			var xMargin = 5f;
			var yMargin = 5f;

			var constSwitch = -(amountOnString * Width + (amountOnString - 1) * xMargin) / 2f;
			var xSwitch = constSwitch;
			var ySwitch = -15f;

			var i = 1;
			foreach (var category in _itemsCategories)
			{
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = $"{xSwitch} {ySwitch - Height}",
						OffsetMax = $"{xSwitch + Width} {ySwitch}"
					},
					Text =
					{
						Text = $"{category.Key}",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = selectedCategory == category.Key
							? HexToCuiColor("#4B68FF")
							: HexToCuiColor("#161617"),
						Command = $"UI_Kits selectitem {itemContainer} {kitId} {slot}  {category.Key}"
					}
				}, ModalLayer + ".Main");

				if (i % amountOnString == 0)
				{
					ySwitch = ySwitch - Height - yMargin;
					xSwitch = constSwitch;
				}
				else
				{
					xSwitch += xMargin + Width;
				}

				i++;
			}

			#endregion

			#region Items

			amountOnString = 5;

			var strings = 4;
			var totalAmount = amountOnString * strings;

			ySwitch = ySwitch - yMargin - Height - 10f;

			Width = 85f;
			Height = 85f;
			xMargin = 15f;
			yMargin = 5f;

			constSwitch = -(amountOnString * Width + (amountOnString - 1) * xMargin) / 2f;
			xSwitch = constSwitch;

			i = 1;

			var canSearch = !string.IsNullOrEmpty(input) && input.Length > 2;

			var temp = canSearch
				? _itemsCategories
					.SelectMany(x => x.Value)
					.Where(x => x.shortName.StartsWith(input) || x.shortName.Contains(input) ||
					            x.shortName.EndsWith(input))
				: _itemsCategories[selectedCategory];

			var itemsAmount = temp.Count;

			temp.SkipAndTake(page * totalAmount, totalAmount).ForEach(item =>
			{
				container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = $"{xSwitch} {ySwitch - Height}",
							OffsetMax = $"{xSwitch + Width} {ySwitch}"
						},
						Image = {Color = HexToCuiColor("#161617")}
					}, ModalLayer + ".Main", ModalLayer + $".Item.{item}");

				container.Add(new CuiElement
				{
					Parent = ModalLayer + $".Item.{item}",
					Components =
					{
						new CuiImageComponent
						{
							ItemId = item.itemID
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "5 5", OffsetMax = "-5 -5"
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
							Command = $"UI_Kits takeitem {page} {itemContainer} {kitId} {slot} {item.shortName}",
							Close = ModalLayer
						}
					}, ModalLayer + $".Item.{item}");

				if (i % amountOnString == 0)
				{
					xSwitch = constSwitch;
					ySwitch = ySwitch - yMargin - Height;
				}
				else
				{
					xSwitch += xMargin + Width;
				}

				i++;
			});

			#endregion

			#region Search

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0", AnchorMax = "0.5 0",
					OffsetMin = "-90 10", OffsetMax = "90 35"
				},
				Image = {Color = HexToCuiColor("#4B68FF")}
			}, ModalLayer + ".Main", ModalLayer + ".Search");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "10 0", OffsetMax = "0 0"
				},
				Text =
				{
					Text = canSearch ? $"{input}" : Msg(player, ItemSearch),
					Align = canSearch ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = canSearch ? "1 1 1 0.8" : "1 1 1 1"
				}
			}, ModalLayer + ".Search");

			container.Add(new CuiElement
			{
				Parent = ModalLayer + ".Search",
				Components =
				{
					new CuiInputFieldComponent
					{
						FontSize = 10,
						Align = TextAnchor.MiddleLeft,
						Command = $"UI_Kits selectitem {itemContainer} {kitId} {slot} {selectedCategory} 0 ",
						Color = "1 1 1 0.95",
						CharsLimit = 150,
						NeedsKeyboard = true
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "10 0", OffsetMax = "0 0"
					}
				}
			});

			#endregion

			#region Pages

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "0 0",
					OffsetMin = "10 10",
					OffsetMax = "80 35"
				},
				Text =
				{
					Text = Msg(player, Back),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = HexToCuiColor("#161617"),
					Command = page != 0
						? $"UI_Kits selectitem {itemContainer} {kitId} {slot} {selectedCategory} {page - 1} {input}"
						: ""
				}
			}, ModalLayer + ".Main");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 0", AnchorMax = "1 0",
					OffsetMin = "-80 10",
					OffsetMax = "-10 35"
				},
				Text =
				{
					Text = Msg(player, Next),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = HexToCuiColor("#4B68FF"),
					Command = itemsAmount > (page + 1) * totalAmount
						? $"UI_Kits selectitem {itemContainer} {kitId} {slot} {selectedCategory} {page + 1} {input}"
						: ""
				}
			}, ModalLayer + ".Main");

			#endregion

			#endregion

			CuiHelper.AddUi(player, container);
		}

		#endregion Editing UI

		#region UI.Components
		
		private static void UpdateUI(BasePlayer player, Action<CuiElementContainer> callback)
		{
			if (player == null) return;

			var container = new CuiElementContainer();

			callback?.Invoke(container);

			CuiHelper.AddUi(player, container);
		}

		
		private static void ShowGridUI(CuiElementContainer container, 
			int startIndex, int count,
			int itemsOnString,
			float marginX,
			float marginY,
			float itemWidth,
			float itemHeight,
			float offsetX,
			float offsetY,
			float aMinX, float aMaxX, float aMinY, float aMaxY,
			string backgroundColor,
			string parent, 
			Func<int, string> panelName = null,
			Func<int, string> destroyName = null,
			Action<int> callback = null)
		{
			var xSwitch = offsetX;
			var ySwitch = offsetY;

			for (var i = startIndex; i < count; i++)
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = $"{aMinX} {aMinY}", AnchorMax = $"{aMaxX} {aMaxY}",
						OffsetMin = $"{xSwitch} {ySwitch - itemHeight}", 
						OffsetMax = $"{xSwitch + itemWidth} {ySwitch}"
					},
					Image = { Color = backgroundColor }
				}, parent, panelName != null ? panelName(i) : CuiHelper.GetGuid(), destroyName != null ? destroyName(i) : string.Empty);
				
				callback?.Invoke(i);
				
				if ((i + 1) % itemsOnString == 0)
				{
					xSwitch = offsetX;
					ySwitch = ySwitch - itemHeight - marginY;
				}
				else
				{
					xSwitch += itemWidth + marginX;
				}
			}
		}

		private void AddMoveButton(CuiElementContainer container,
			string layerParent,
			string direction,
			int yOffset,
			string moveCommand)
		{
			container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 1", AnchorMax = "1 1",
						OffsetMin = $"0 {yOffset - 15}", OffsetMax = $"10 {yOffset}"
					},
					Text =
					{
						Text = direction == "right" ? "▶" : "◀",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 7,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = direction == "right" ? HexToCuiColor("#50965F") : HexToCuiColor("#FF4B4B"),
						Command = moveCommand
					}
				}, layerParent, layerParent + $".Move.{direction}");
		}

		private void ErrorUi(BasePlayer player, string msg)
		{
			var container = new CuiElementContainer
			{
				{
					new CuiPanel
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Image = {Color = _config.UI.ColorFive.Get()},
						CursorEnabled = true
					},
					"Overlay", ModalLayer, ModalLayer
				},
				{
					new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 0.5",
							AnchorMax = "0.5 0.5",
							OffsetMin = "-127.5 -75",
							OffsetMax = "127.5 140"
						},
						Image = {Color = _config.UI.ColorRed.Get()}
					},
					ModalLayer, ModalLayer + ".Main"
				},
				{
					new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -165", OffsetMax = "0 0"
						},
						Text =
						{
							Text = Msg(player, UI_Error_Title),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 120,
							Color = _config.UI.ColorWhite.Get()
						}
					},
					ModalLayer + ".Main"
				},
				{
					new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -175", OffsetMax = "0 -155"
						},
						Text =
						{
							Text = msg ?? string.Empty,
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = _config.UI.ColorWhite.Get()
						}
					},
					ModalLayer + ".Main"
				},
				{
					new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 30"
						},
						Text =
						{
							Text = Msg(player, BtnClose),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = _config.UI.ColorWhite.Get()
						},
						Button = {Color = HexToCuiColor("#CD3838"), Close = ModalLayer}
					},
					ModalLayer + ".Main"
				}
			};

			CuiHelper.AddUi(player, container);
		}

		private void EditFieldUi(BasePlayer player, ref CuiElementContainer container,
			string parent,
			string name,
			string oMin,
			string oMax,
			string command,
			string label,
			object fieldValue)
		{
			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = $"{oMin}",
					OffsetMax = $"{oMax}"
				},
				Image =
				{
					Color = "0 0 0 0"
				}
			}, parent, name);

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -10", OffsetMax = "0 0"
				},
				Text =
				{
					Text = label,
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 8,
					Color = "1 1 1 1"
				}
			}, name);

			container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "0 0", OffsetMax = "0 -10"
					},
					Image = {Color = "0 0 0 0"}
				}, name, $"{name}.Value");

			CreateOutLine(ref container, $"{name}.Value", HexToCuiColor("#161617", 100));

			container.Add(new CuiElement
			{
				Parent = $"{name}.Value",
				Components =
				{
					new CuiInputFieldComponent
					{
						FontSize = 12,
						Align = TextAnchor.MiddleLeft,
						Command = $"{command}",
						Color = "1 1 1 0.4",
						CharsLimit = 150,
						NeedsKeyboard = true,
						Text = $"{fieldValue}",
						Font = "robotocondensed-regular.ttf"
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "10 0", OffsetMax = "0 0"
					}
				}
			});

			container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 1", AnchorMax = "1 1",
						OffsetMin = "-30 -40", OffsetMax = "0 0"
					},
					Text =
					{
						Text = Msg(player, EditRemoveField),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 14,
						Color = HexToCuiColor("#161617", 100)
					},
					Button =
					{
						Color = "0 0 0 0",
						Command = $"{command}delete"
					}
				}, $"{name}.Value");
		}

		private void EditBoolFieldUi(BasePlayer player, ref CuiElementContainer container,
			string parent,
			string name,
			string oMin,
			string oMax,
			string command,
			string label,
			bool value)
		{
			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1",
					AnchorMax = "0.5 1",
					OffsetMin = $"{oMin}",
					OffsetMax = $"{oMax}"
				},
				Image =
				{
					Color = "0 0 0 0"
				}
			}, parent, name);

			CreateOutLine(ref container, name, HexToCuiColor("#161617", 100));

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "10 0", OffsetMax = "0 0"
				},
				Text =
				{
					Text = label,
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				}
			}, name);

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 0.5", AnchorMax = "1 0.5",
					OffsetMin = "-40 -10", OffsetMax = "-10 10"
				},
				Text =
				{
					Text = value ? "✔" : string.Empty,
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = HexToCuiColor("#161617", 100),
					Command = $"{command} {!value}"
				}
			}, name);
		}

		private void CheckBoxUi(ref CuiElementContainer container,
			string parent, string name, string aMin, string aMax,
			string oMin, string oMax, bool enabled,
			string command, string text,
			string color = null,
			int outlineSize = 3)
		{
			color ??= HexToCuiColor("4B68FF", 100);
			
			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = aMin, AnchorMax = aMax,
					OffsetMin = oMin,
					OffsetMax = oMax
				},
				Image = {Color = "0 0 0 0"}
			}, parent, name);
			
			CreateOutLine(ref container, name, color, outlineSize);

			if (enabled)
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1"
					},
					Image = {Color = HexToCuiColor("4B68FF", 100)}
				}, name);


			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text = {Text = string.Empty},
				Button =
				{
					Color = "0 0 0 0",
					Command = $"{command}"
				}
			}, name);

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "1 0.5", AnchorMax = "1 0.5",
					OffsetMin = "5 -10",
					OffsetMax = "100 10"
				},
				Text =
				{
					Text = $"{text}",
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = HexToCuiColor("#FFFFFF", 100)
				}
			}, name);
		}

		private void InfoItemUi(ref CuiElementContainer container, BasePlayer player,
			int slot,
			string oMin,
			string oMax,
			Kit kit,
			KitItem kitItem, int total, string itemContainer)
		{
			container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 1",
						AnchorMax = "0.5 1",
						OffsetMin = $"{oMin}",
						OffsetMax = $"{oMax}"
					},
					Image =
					{
						Color = _config.UI.ColorOne.Get()
					}
				}, InfoLayer, InfoLayer + $".Item.{total}");

			if (kitItem != null)
			{
				container.Add(kitItem.GetImage("0 0", "1 1", "10 10", "-10 -10", InfoLayer + $".Item.{total}"));

				container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "2.5 3.5", OffsetMax = "-2.5 -2.5"
						},
						Text =
						{
							Text = $"x{kitItem.Amount}",
							Align = TextAnchor.LowerRight,
							Font = "robotocondensed-regular.ttf",
							FontSize = 10,
							Color = "1 1 1 1"
						}
					}, InfoLayer + $".Item.{total}");

				var color = _config.RarityColors.Find(x => x.Chance == kitItem.Chance);
				if (color != null)
				{
					container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 0",
								OffsetMin = "0 0", OffsetMax = "0 2"
							},
							Image =
							{
								Color = HexToCuiColor(color.Color)
							}
						}, InfoLayer + $".Item.{total}");

					container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "2.5 2.5", OffsetMax = "-2.5 -2.5"
							},
							Text =
							{
								Text = $"{kitItem.Chance}%",
								Align = TextAnchor.UpperLeft,
								Font = "robotocondensed-regular.ttf",
								FontSize = 10,
								Color = "1 1 1 1"
							}
						}, InfoLayer + $".Item.{total}");
				}
			}

			if (IsAdmin(player))
				container.Add(new CuiButton
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text = {Text = string.Empty},
						Button =
						{
							Color = "0 0 0 0",
							Command =
								$"UI_Kits edit_item start {itemContainer} {kit.ID} {slot}",
							Close = InfoLayer
						}
					}, InfoLayer + $".Item.{total}");
		}

		
		private void InfoItemUiV2(ref CuiElementContainer container, BasePlayer player,
			int slot,
			string oMin,
			string oMax,
			Kit kit,
			KitItem kitItem, int total, string itemContainer)
		{
			var openedKits = GetOpenedKits(player);
			var targetUI = openedKits.useMainUI ? _config.UI : _config.MenuUI;
			
			var itemLayer = Layer + $".Kit.{kit.ID}" + $".Item.{total}";

			container.Add(new CuiElement()
			{
				Parent = Layer + $".Kit.{kit.ID}",
				Name = itemLayer,
				DestroyUi = itemLayer,
				Components =
				{
					targetUI.KitPanel.Contents.ItemBackground.GetImageComponent(),
					new CuiRectTransformComponent()
					{
						AnchorMin = "0.5 1",
						AnchorMax = "0.5 1",
						OffsetMin = oMin,
						OffsetMax = oMax
					}
				}
			});

			if (kitItem != null)
			{
				container.Add(kitItem.GetImage(targetUI.KitPanel.Contents.ItemImage.GetRectTransform(), itemLayer));

				container.Add(targetUI.KitPanel.Contents.ItemAmount.GetText($"x{kitItem.Amount}", itemLayer));

				var color = _config.RarityColors.Find(x => x.Chance == kitItem.Chance);
				if (color != null)
				{
					container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 0",
								OffsetMin = "0 0", OffsetMax = "0 2"
							},
							Image =
							{
								Color = HexToCuiColor(color.Color)
							}
						}, itemLayer);

					container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "2.5 2.5", OffsetMax = "-2.5 -2.5"
							},
							Text =
							{
								Text = $"{kitItem.Chance}%",
								Align = TextAnchor.UpperLeft,
								Font = "robotocondensed-regular.ttf",
								FontSize = 10,
								Color = "1 1 1 1"
							}
						}, itemLayer);
				}
			}
			
			if (IsAdmin(player))
				container.Add(new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text = {Text = string.Empty},
					Button =
					{
						Color = "0 0 0 0",
						Command =
							$"UI_Kits edit_item start {itemContainer} {kit.ID} {slot}",
						Close = InfoLayer
					}
				}, itemLayer);

		}

		private void RefreshKitUi(ref CuiElementContainer container, BasePlayer player, Kit kit)
		{
			var playerData = PlayerData.GetOrCreateKitData(player.UserIDString, kit.Name);
			if (playerData == null) return;

			var openedKits = GetOpenedKits(player);
			var targetUI = openedKits.useMainUI ? _config.UI : _config.MenuUI;
			switch (targetUI.Style)
			{
				case InterfaceStyle.NewRust:
				{
					if (targetUI.KitPanel.DisplayKitContents)
					{
						container.Add(new CuiPanel
							{
								RectTransform =
								{
									AnchorMin = "0 0", AnchorMax = "1 1",
									OffsetMin = "0 0", OffsetMax = "0 0"
								},
								Image = {Color = "0 0 0 0"}
							},
							Layer + $".Kit.{kit.ID}.Main",
							Layer + $".Kit.{kit.ID}",
							Layer + $".Kit.{kit.ID}");
						

						if (kit.Amount > 0) 
							container.Add(targetUI.KitPanel.Contents.KitAmount.GetText(Msg(player, UI_MeventRust_Amount, playerData.Amount, kit.Amount), Layer + $".Kit.{kit.ID}"));

						var currentTime = GetCurrentTime();

						if (IsKitCooldown(kit, playerData, currentTime, out var isCooldown, out var wipeBlock))
						{
							var time = GetCooldownTimeRemaining(kit, playerData, currentTime, isCooldown);

							if (kit.Amount > 0)
							{
								container.Add(new CuiElement()
								{
									Parent = Layer + $".Kit.{kit.ID}",
									Name = Layer + $".Kit.{kit.ID}.Cooldown",
									DestroyUi = Layer + $".Kit.{kit.ID}.Cooldown",
									Components = 
									{
										targetUI.KitPanel.Contents.KitCooldownWithAmount.GetButtonComponent(string.Empty, string.Empty),
										targetUI.KitPanel.Contents.KitCooldownWithAmount.GetRectTransform()
									}
								});
								
								container.Add(new CuiElement()
								{
									Parent = Layer + $".Kit.{kit.ID}.Cooldown",
									Components =
									{
										new CuiTextComponent()
										{
											Text = "<b>↺</b> %TIME_LEFT%", 
											Align = targetUI.KitPanel.Contents.KitCooldownWithAmount.Align,
											Font = targetUI.KitPanel.Contents.KitCooldownWithAmount.IsBold ? "robotocondensed-bold.ttf" : "robotocondensed-regular.ttf", 
											FontSize = targetUI.KitPanel.Contents.KitCooldownWithAmount.FontSize,
											Color = targetUI.KitPanel.Contents.KitCooldownWithAmount.Color.Get()
										},
										new CuiRectTransformComponent
										{
											AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
										},
										new CuiCountdownComponent
										{
											EndTime = 0,
											StartTime = (float) time.TotalSeconds,
											Step = 1,
											TimerFormat = TimerFormat.HoursMinutesSeconds,
											DestroyIfDone = true,
											Command = $"UI_Kits refresh_kit {kit.ID}"
										}
									}
								});
							}
							else
							{
								container.Add(new CuiElement()
								{
									Parent = Layer + $".Kit.{kit.ID}",
									Name = Layer + $".Kit.{kit.ID}.Cooldown",
									DestroyUi = Layer + $".Kit.{kit.ID}.Cooldown",
									Components = 
									{
										targetUI.KitPanel.Contents.KitCooldown.GetButtonComponent(string.Empty, string.Empty),
										targetUI.KitPanel.Contents.KitCooldown.GetRectTransform()
									}
								});
								
								container.Add(new CuiElement()
								{
									Parent = Layer + $".Kit.{kit.ID}.Cooldown",
									Components =
									{
										new CuiTextComponent()
										{
											Text = "<b>↺</b> %TIME_LEFT%", 
											Align = targetUI.KitPanel.Contents.KitCooldown.Align,
											Font = targetUI.KitPanel.Contents.KitCooldown.IsBold ? "robotocondensed-bold.ttf" : "robotocondensed-regular.ttf", 
											FontSize = targetUI.KitPanel.Contents.KitCooldown.FontSize,
											Color = targetUI.KitPanel.Contents.KitCooldown.Color.Get()
										},
										new CuiRectTransformComponent
										{
											AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
										},
										new CuiCountdownComponent
										{
											EndTime = 0,
											StartTime = (float) time.TotalSeconds,
											Step = 1,
											TimerFormat = TimerFormat.HoursMinutesSeconds,
											DestroyIfDone = true,
											Command = $"UI_Kits refresh_kit {kit.ID}"
										}
									}
								});
							}
						}
						else
						{
							if (kit.Sale)
							{
								container.AddRange(targetUI.KitPanel.Contents.KitSale.GetButton(Msg(player, kit.Name, PriceFormat, kit.Price),
								$"UI_Kits givekit {kit.ID}", Layer + $".Kit.{kit.ID}", Layer + $".Kit.{kit.ID}.Sale",
									Layer + $".Kit.{kit.ID}.Sale"));
							}
							else
							{
								container.AddRange(targetUI.KitPanel.KitButtonTake.GetButton(
									Msg(player, kit.Name, KitTake),
									$"UI_Kits givekit {kit.ID}",
									Layer + $".Kit.{kit.ID}", Layer + $".Kit.{kit.ID}.Take",
									Layer + $".Kit.{kit.ID}.Take"));
							}
						}
						
						#region Contents

						var ySwitch = -targetUI.KitPanel.Contents.UpIndent;

						#region Main

						var amountOnString = 6;

						var constSwitch = -(amountOnString * targetUI.KitPanel.Contents.Size + (amountOnString - 1) * targetUI.KitPanel.Contents.Margin) / 2f;

						var total = 0;

						var xSwitch = constSwitch;

						for (var slot = 0; slot < amountOnString * 4; slot++)
						{
							var kitItem = kit.GetItemByContainerAndPosition("main", slot);

							InfoItemUiV2(ref container, player,
								slot,
								$"{xSwitch} {ySwitch - targetUI.KitPanel.Contents.Size}",
								$"{xSwitch + targetUI.KitPanel.Contents.Size} {ySwitch}",
								kit,
								kitItem,
								total,
								"main");

							if ((slot + 1) % amountOnString == 0)
							{
								xSwitch = constSwitch;
								ySwitch = ySwitch - targetUI.KitPanel.Contents.Size - targetUI.KitPanel.Contents.Margin;
							}
							else
							{
								xSwitch += targetUI.KitPanel.Contents.Size + targetUI.KitPanel.Contents.Margin;
							}

							total++;
						}

						#endregion

						#region Wear

						amountOnString = 6;

						constSwitch = -(amountOnString * targetUI.KitPanel.Contents.Size + (amountOnString - 1) * targetUI.KitPanel.Contents.Margin) / 2f;

						xSwitch = constSwitch;

						ySwitch -= targetUI.KitPanel.Contents.Margin;

						for (var slot = 0; slot < amountOnString; slot++)
						{
							var kitItem = kit.GetItemByContainerAndPosition("wear", slot);

							InfoItemUiV2(ref container, player,
								slot,
								$"{xSwitch} {ySwitch - targetUI.KitPanel.Contents.Size}",
								$"{xSwitch + targetUI.KitPanel.Contents.Size} {ySwitch}",
								kit,
								kitItem,
								total,
								"wear");

							if ((slot + 1) % amountOnString == 0)
							{
								xSwitch = constSwitch;
								ySwitch = ySwitch - targetUI.KitPanel.Contents.Size - targetUI.KitPanel.Contents.Margin;
							}
							else
							{
								xSwitch += targetUI.KitPanel.Contents.Size + targetUI.KitPanel.Contents.Margin;
							}

							total++;
						}

						#endregion

						#region Belt

						amountOnString = 6;

						constSwitch = -(amountOnString * targetUI.KitPanel.Contents.Size + (amountOnString - 1) * targetUI.KitPanel.Contents.Margin) / 2f;

						xSwitch = constSwitch;

						ySwitch -= targetUI.KitPanel.Contents.Margin;

						for (var slot = 0; slot < amountOnString; slot++)
						{
							var kitItem = kit.GetItemByContainerAndPosition("belt", slot);

							InfoItemUiV2(ref container, player,
								slot,
								$"{xSwitch} {ySwitch - targetUI.KitPanel.Contents.Size}",
								$"{xSwitch + targetUI.KitPanel.Contents.Size} {ySwitch}",
								kit,
								kitItem,
								total,
								"belt");

							if ((slot + 1) % amountOnString == 0)
							{
								xSwitch = constSwitch;
								ySwitch = ySwitch - targetUI.KitPanel.Contents.Size - targetUI.KitPanel.Contents.Margin;
							}
							else
							{
								xSwitch += targetUI.KitPanel.Contents.Size + targetUI.KitPanel.Contents.Margin;
							}

							total++;
						}

						#endregion

						#endregion
					}
					else
					{
						container.Add(new CuiPanel
							{
								RectTransform =
								{
									AnchorMin = "0.5 0", AnchorMax = "0.5 0",
									OffsetMin = "-70 50", OffsetMax = "70 90"
								},
								Image = {Color = "0 0 0 0"}
							},
							Layer + $".Kit.{kit.ID}.Main",
							Layer + $".Kit.{kit.ID}",
							Layer + $".Kit.{kit.ID}");

						if (_config.ShowAllKits && _config.ShowNoPermDescription &&
						    !string.IsNullOrEmpty(kit.Permission) &&
						    !player.HasPermission(kit.Permission))
						{
							container.Add(new CuiLabel
								{
									RectTransform =
									{
										AnchorMin = targetUI.NoPermission.AnchorMin,
										AnchorMax = targetUI.NoPermission.AnchorMax,
										OffsetMin = targetUI.NoPermission.OffsetMin,
										OffsetMax = targetUI.NoPermission.OffsetMax
									},
									Text =
									{
										Text = Msg(player, kit.Name, NoPermissionDescription),
										Font = "robotocondensed-bold.ttf", FontSize = 12,
										Align = TextAnchor.MiddleCenter,
										Color = HexToCuiColor("#E2DBD3", 50)
									}
								}, Layer + $".Kit.{kit.ID}");
							return;
						}

						if (playerData.HasAmount > 0)
						{
							#region Title

							container.Add(new CuiLabel
								{
									RectTransform =
									{
										AnchorMin = targetUI.KitAmountCooldown.AnchorMin,
										AnchorMax = targetUI.KitAmountCooldown.AnchorMax,
										OffsetMin = targetUI.KitAmountCooldown.OffsetMin,
										OffsetMax = targetUI.KitAmountCooldown.OffsetMax
									},
									Text =
									{
										Text = Msg(player, kit.Name, KitYouHave),
										Font = "robotocondensed-bold.ttf", FontSize = 14,
										Align = TextAnchor.MiddleCenter, Color = targetUI.ColorOne.Get()
									}
								}, Layer + $".Kit.{kit.ID}");

							#endregion

							#region Points

							var amount = Mathf.Min(playerData.HasAmount, 9);

							var width = amount == 1
								? targetUI.KitAmount.Width
								: targetUI.KitAmount.Width / amount * 0.9f;

							var margin = (targetUI.KitAmount.Width - width * amount) / (amount - 1);

							var xSwitch = -(targetUI.KitAmount.Width / 2f);

							for (var i = 0; i < amount; i++)
							{
								container.Add(new CuiPanel
									{
										RectTransform =
										{
											AnchorMin = targetUI.KitAmount.AnchorMin,
											AnchorMax = targetUI.KitAmount.AnchorMax,
											OffsetMin = $"{xSwitch} {targetUI.KitAmount.OffsetMin}",
											OffsetMax = $"{xSwitch + width} {targetUI.KitAmount.OffsetMax}"
										},
										Image =
										{
											Color = HexToCuiColor("#71B8ED", 70),
											Material = "assets/content/ui/namefontmaterial.mat"
										}
									}, Layer + $".Kit.{kit.ID}");

								xSwitch += width + margin;
							}

							#endregion
						}
						else
						{
							var currentTime = GetCurrentTime();

							if (IsKitCooldown(kit, playerData, currentTime, out var isCooldown, out var wipeBlock))
							{
								var time = GetCooldownTimeRemaining(kit, playerData, currentTime, isCooldown);

								if (kit.Amount > 0)
								{
									container.Add(new CuiElement()
									{
										Name = Layer + $".Kit.{kit.ID}.Cooldown",
										DestroyUi = Layer + $".Kit.{kit.ID}.Cooldown",
										Parent = Layer + $".Kit.{kit.ID}",
										Components =
										{
											new CuiTextComponent()
											{
												Text = "%TIME_LEFT%", Font = "robotocondensed-bold.ttf",
												FontSize = 14, Align = TextAnchor.MiddleCenter,
												Color = targetUI.ColorOne.Get()
											},
											new CuiRectTransformComponent
											{
												AnchorMin = targetUI.KitAmountCooldown.AnchorMin,
												AnchorMax = targetUI.KitAmountCooldown.AnchorMax,
												OffsetMin = targetUI.KitAmountCooldown.OffsetMin,
												OffsetMax = targetUI.KitAmountCooldown.OffsetMax
											},
											new CuiCountdownComponent
											{
												EndTime = 0,
												StartTime = (float) time.TotalSeconds,
												Step = 1,
												TimerFormat = TimerFormat.HoursMinutesSeconds,
												DestroyIfDone = true,
												Command = $"UI_Kits refresh_kit {kit.ID}"
											}
										}
									});
								}
								else
								{
									container.Add(new CuiPanel
										{
											Image =
											{
												Color = HexToCuiColor("#71B8ED", 10),
												Material = "assets/content/ui/namefontmaterial.mat"
											},
											RectTransform =
											{
												AnchorMin = targetUI.KitCooldown.AnchorMin,
												AnchorMax = targetUI.KitCooldown.AnchorMax,
												OffsetMin = targetUI.KitCooldown.OffsetMin,
												OffsetMax = targetUI.KitCooldown.OffsetMax
											}
										}, Layer + $".Kit.{kit.ID}", Layer + $".Kit.{kit.ID}.Cooldown");

									container.Add(new CuiElement()
									{
										Parent = Layer + $".Kit.{kit.ID}.Cooldown",
										Components =
										{
											new CuiTextComponent()
											{
												Text = "%TIME_LEFT%", Font = "robotocondensed-bold.ttf",
												FontSize = 14, Align = TextAnchor.MiddleCenter,
												Color = targetUI.ColorOne.Get()
											},
											new CuiRectTransformComponent
											{
												AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
											},
											new CuiCountdownComponent
											{
												EndTime = 0,
												StartTime = (float) time.TotalSeconds,
												Step = 1,
												TimerFormat = TimerFormat.HoursMinutesSeconds,
												DestroyIfDone = true,
												Command = $"UI_Kits refresh_kit {kit.ID}"
											}
										}
									});
								}
							}
							else
							{
								if (kit.Sale)
								{
									container.Add(new CuiPanel
										{
											RectTransform =
											{
												AnchorMin = targetUI.KitSale.AnchorMin,
												AnchorMax = targetUI.KitSale.AnchorMax,
												OffsetMin = targetUI.KitSale.OffsetMin,
												OffsetMax = targetUI.KitSale.OffsetMax
											},
											Image =
											{
												Color = HexToCuiColor("#71B8ED", 10),
												Material = "assets/content/ui/namefontmaterial.mat"
											}
										}, Layer + $".Kit.{kit.ID}", Layer + $".Kit.{kit.ID}.Sale");

									container.Add(new CuiLabel
										{
											RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
											Text =
											{
												Text = Msg(player, kit.Name, PriceFormat, kit.Price),
												Align = TextAnchor.MiddleCenter,
												Font = "robotocondensed-bold.ttf",
												FontSize = 12,
												Color = "1 1 1 1"
											}
										}, Layer + $".Kit.{kit.ID}.Sale");
								}
								else
								{
									container.Add(new CuiLabel
										{
											RectTransform =
											{
												AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0",
												OffsetMax = "0 0"
											},
											Text =
											{
												Text = Msg(player, kit.Name, KitAvailableTitle),
												Font = "robotocondensed-bold.ttf", FontSize = 12,
												Align = TextAnchor.UpperCenter, Color = HexToCuiColor("#E2DBD3", 50)
											}
										}, Layer + $".Kit.{kit.ID}");
								}
							}

							if (kit.Amount > 0)
							{
								var kitAmountWidth = 140f;

								var amount = Mathf.Min(kit.Amount, 9);

								var hasAmount = kit.Amount > 9 ? 9 * playerData.Amount / kit.Amount : playerData.Amount;

								var width = amount == 1
									? kitAmountWidth
									: kitAmountWidth / amount * 0.9f;

								var margin = (kitAmountWidth - width * amount) / (amount - 1);

								var xSwitch = -(kitAmountWidth / 2f);

								for (var i = 0; i < amount; i++)
								{
									container.Add(new CuiPanel
										{
											RectTransform =
											{
												AnchorMin = "0.5 0",
												AnchorMax = "0.5 0",
												OffsetMin = $"{xSwitch} 0",
												OffsetMax = $"{xSwitch + width} 4"
											},
											Image =
											{
												Color = i < hasAmount
													? HexToCuiColor("#71B8ED", 70)
													: HexToCuiColor("#71B8ED", 10),
												Material = "assets/content/ui/namefontmaterial.mat"
											}
										}, Layer + $".Kit.{kit.ID}");

									xSwitch += width + margin;
								}
							}
						}
					}

					break;
				}

				default:
				{
					container.Add(new CuiPanel
						{
							RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1"},
							Image = {Color = "0 0 0 0"}
						}, Layer + $".Kit.{kit.ID}.Main", Layer + $".Kit.{kit.ID}", Layer + $".Kit.{kit.ID}");

					if (_config.ShowAllKits && _config.ShowNoPermDescription && !string.IsNullOrEmpty(kit.Permission) &&
					    !player.HasPermission(kit.Permission))
					{
						container.Add(new CuiLabel
							{
								RectTransform =
								{
									AnchorMin = targetUI.NoPermission.AnchorMin,
									AnchorMax = targetUI.NoPermission.AnchorMax,
									OffsetMin = targetUI.NoPermission.OffsetMin,
									OffsetMax = targetUI.NoPermission.OffsetMax
								},
								Text =
								{
									Text = Msg(player, kit.Name, NoPermissionDescription),
									Align = TextAnchor.MiddleCenter,
									Font = "robotocondensed-regular.ttf",
									FontSize = 10,
									Color = targetUI.ColorFour.Get()
								}
							}, Layer + $".Kit.{kit.ID}");
						return;
					}

					if (playerData.HasAmount > 0)
					{
						#region Title

						container.Add(new CuiLabel
							{
								RectTransform =
								{
									AnchorMin = targetUI.KitAmountCooldown.AnchorMin,
									AnchorMax = targetUI.KitAmountCooldown.AnchorMax,
									OffsetMin = targetUI.KitAmountCooldown.OffsetMin,
									OffsetMax = targetUI.KitAmountCooldown.OffsetMax
								},
								Text =
								{
									Text = Msg(player, kit.Name, KitYouHave),
									Align = TextAnchor.MiddleCenter,
									Font = "robotocondensed-bold.ttf",
									FontSize = 12,
									Color = "1 1 1 1"
								}
							}, Layer + $".Kit.{kit.ID}");

						#endregion

						#region Points

						var amount = Mathf.Min(playerData.HasAmount, 9);

						var width = amount == 1
							? targetUI.KitAmount.Width
							: targetUI.KitAmount.Width / amount * 0.9f;

						var margin = (targetUI.KitAmount.Width - width * amount) / (amount - 1);

						var xSwitch = -(targetUI.KitAmount.Width / 2f);

						for (var i = 0; i < amount; i++)
						{
							container.Add(new CuiPanel
								{
									RectTransform =
									{
										AnchorMin = targetUI.KitAmount.AnchorMin,
										AnchorMax = targetUI.KitAmount.AnchorMax,
										OffsetMin = $"{xSwitch} {targetUI.KitAmount.OffsetMin}",
										OffsetMax = $"{xSwitch + width} {targetUI.KitAmount.OffsetMax}"
									},
									Image =
									{
										Color = HexToCuiColor(kit.Color)
									}
								}, Layer + $".Kit.{kit.ID}");

							xSwitch += width + margin;
						}

						#endregion
					}
					else
					{
						var currentTime = GetCurrentTime();

						if (IsKitCooldown(kit, playerData, currentTime, out var isCooldown, out var wipeBlock))
						{
							var time = GetCooldownTimeRemaining(kit, playerData, currentTime, isCooldown);

							if (kit.Amount > 0)
							{
								container.Add(new CuiElement()
								{
									Name = Layer + $".Kit.{kit.ID}.Cooldown",
									DestroyUi = Layer + $".Kit.{kit.ID}.Cooldown",
									Parent = Layer + $".Kit.{kit.ID}",
									Components =
									{
										new CuiTextComponent()
										{
											Text = "%TIME_LEFT%", Align = TextAnchor.MiddleCenter,
											Font = "robotocondensed-bold.ttf",
											FontSize = 12,
											Color = "1 1 1 1"
										},
										new CuiRectTransformComponent
										{
											AnchorMin = targetUI.KitAmountCooldown.AnchorMin,
											AnchorMax = targetUI.KitAmountCooldown.AnchorMax,
											OffsetMin = targetUI.KitAmountCooldown.OffsetMin,
											OffsetMax = targetUI.KitAmountCooldown.OffsetMax
										},
										new CuiCountdownComponent
										{
											EndTime = 0,
											StartTime = (float) time.TotalSeconds,
											Step = 1,
											TimerFormat = TimerFormat.HoursMinutesSeconds,
											DestroyIfDone = true,
											Command = $"UI_Kits refresh_kit {kit.ID}"
										}
									}
								});
							}
							else
							{
								container.Add(new CuiPanel
									{
										RectTransform =
										{
											AnchorMin = targetUI.KitCooldown.AnchorMin,
											AnchorMax = targetUI.KitCooldown.AnchorMax,
											OffsetMin = targetUI.KitCooldown.OffsetMin,
											OffsetMax = targetUI.KitCooldown.OffsetMax
										},
										Image = {Color = HexToCuiColor(kit.Color)}
									}, Layer + $".Kit.{kit.ID}", Layer + $".Kit.{kit.ID}.Cooldown");

								container.Add(new CuiElement()
								{
									Parent = Layer + $".Kit.{kit.ID}.Cooldown",
									Components =
									{
										new CuiTextComponent()
										{
											Text = "%TIME_LEFT%", Align = TextAnchor.MiddleCenter,
											Font = "robotocondensed-bold.ttf",
											FontSize = 12,
											Color = "1 1 1 1"
										},
										new CuiRectTransformComponent
										{
											AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"
										},
										new CuiCountdownComponent
										{
											EndTime = 0,
											StartTime = (float) time.TotalSeconds,
											Step = 1,
											TimerFormat = TimerFormat.HoursMinutesSeconds,
											DestroyIfDone = true,
											Command = $"UI_Kits refresh_kit {kit.ID}"
										}
									}
								});
							}
						}
						else
						{
							if (kit.Sale)
							{
								container.Add(new CuiPanel
									{
										RectTransform =
										{
											AnchorMin = targetUI.KitSale.AnchorMin,
											AnchorMax = targetUI.KitSale.AnchorMax,
											OffsetMin = targetUI.KitSale.OffsetMin,
											OffsetMax = targetUI.KitSale.OffsetMax
										},
										Image = {Color = HexToCuiColor(kit.Color)}
									}, Layer + $".Kit.{kit.ID}", Layer + $".Kit.{kit.ID}.Sale");

								container.Add(new CuiLabel
									{
										RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
										Text =
										{
											Text = Msg(player, kit.Name, PriceFormat, kit.Price),
											Align = TextAnchor.MiddleCenter,
											Font = "robotocondensed-bold.ttf",
											FontSize = 12,
											Color = "1 1 1 1"
										}
									}, Layer + $".Kit.{kit.ID}.Sale");
							}
							else
							{
								container.Add(new CuiLabel
									{
										RectTransform =
										{
											AnchorMin = targetUI.KitAvailable.AnchorMin,
											AnchorMax = targetUI.KitAvailable.AnchorMax,
											OffsetMin = targetUI.KitAvailable.OffsetMin,
											OffsetMax = targetUI.KitAvailable.OffsetMax
										},
										Text =
										{
											Text = Msg(player, kit.Name, KitAvailableTitle),
											Align = TextAnchor.MiddleCenter,
											Font = "robotocondensed-regular.ttf",
											FontSize = 10,
											Color = targetUI.ColorFour.Get()
										}
									}, Layer + $".Kit.{kit.ID}");
							}
						}

						if (kit.Amount > 0)
						{
							var amount = Mathf.Min(kit.Amount, 9);

							var hasAmount = kit.Amount > 9 ? 9 * playerData.Amount / kit.Amount : playerData.Amount;

							var width = amount == 1
								? targetUI.KitAmount.Width
								: targetUI.KitAmount.Width / amount * 0.9f;

							var margin = (targetUI.KitAmount.Width - width * amount) / (amount - 1);

							var xSwitch = -(targetUI.KitAmount.Width / 2f);

							for (var i = 0; i < amount; i++)
							{
								container.Add(new CuiPanel
									{
										RectTransform =
										{
											AnchorMin = targetUI.KitAmount.AnchorMin,
											AnchorMax = targetUI.KitAmount.AnchorMax,
											OffsetMin = $"{xSwitch} {targetUI.KitAmount.OffsetMin}",
											OffsetMax = $"{xSwitch + width} {targetUI.KitAmount.OffsetMax}"
										},
										Image =
										{
											Color = i < hasAmount ? HexToCuiColor(kit.Color) : targetUI.ColorTwo.Get()
										}
									}, Layer + $".Kit.{kit.ID}");

								xSwitch += width + margin;
							}
						}
					}

					break;
				}
			}
		}

		#endregion

		#endregion

		#region Kit Helpers

		private bool GiveKitToPlayer(BasePlayer player, Kit kit,
			bool force = false,
			bool chat = false, bool usingUI = true)
		{ 
			if (!CanPlayerRedeemKit(player, kit, force, chat))
				return false;
			
			if (!force)
				ServerMgr.Instance.StartCoroutine(GiveKitItems(player, kit));
			else
				FastGiveKitItems(player, kit);

			kit.UseCommands(player);

			UpdatePlayerData(player, kit, force);

			SendNotify(player, KitClaimed, 0, kit.DisplayName);

			Interface.CallHook("OnKitRedeemed", player, kit.Name);

			PlayerData.Save(player.UserIDString);
			
			Log(player, kit.Name);

			HandleKitRedeemedUI(player, usingUI);
			return true;
		}

		private void HandleKitRedeemedUI(BasePlayer player, bool usingUI)
		{
			if (usingUI)
			{
				if (_config.UI.CloseAfterReceive)
				{
					ServerPanel?.Call("API_OnServerPanelCallClose", player);
					
					RemoveOpenedKits(player);

					CuiHelper.DestroyUi(player, Layer);
				}
				else
				{
					UpdateUI(player, container =>
					{
						MainKitsContentUI(player, container);
					});
				}
			}
		}

		private void UpdatePlayerData(BasePlayer player, Kit kit, bool force)
		{
			var playerData = PlayerData.GetOrCreateKitData(player.UserIDString, kit.Name);
			if (!force && playerData != null)
			{
				if (playerData.HasAmount > 0)
				{
					playerData.HasAmount -= 1;
				}
				else
				{
					if (kit.Amount > 0) playerData.Amount += 1;

					if (kit.Cooldown > 0) 
						playerData.Cooldown = GetCurrentTime() + GetCooldown(kit.Cooldown, player);
				}
			}
		}

		private bool CanPlayerRedeemKit(BasePlayer player, Kit kit, bool force, bool chat)
		{
			if (player == null || kit == null) return false;

			if (Interface.Oxide.CallHook("canRedeemKit", player) != null)
				return false;

			if (force) return true;
			
			var playerData = PlayerData.GetOrCreateKitData(player.UserIDString, kit.Name);
			if (playerData == null) return false;

			var kitConditions = CheckConditionsForKit(player, kit, playerData);
			if (kitConditions.Success) return true;
			
			SendMessageToNotifyOrUI(player, kitConditions.ErrorMessage, 1, chat);
			return false;
		}

		private (bool Success, string ErrorMessage) CheckConditionsForKit(BasePlayer player, Kit kit,
			PlayerData.KitData playerData)
		{
			if (playerData is not {HasAmount: > 0} &&
			    !string.IsNullOrEmpty(kit.Permission) &&
			    !player.HasPermission(kit.Permission))
				return (false, Msg(player, NoPermission));

			if (_config.BlockBuilding && !player.CanBuild()) 
				return (false, Msg(player, BBlocked));

			if (kit.CooldownAfterWipe > 0)
			{
				var leftTime = LeftWipeBlockTime(kit.CooldownAfterWipe);
				if (leftTime > 0)
					return (false, Msg(player, KitCooldown,
						FormatShortTime(TimeSpan.FromSeconds(leftTime))));
			}

			if (_config.UseNoEscape && !_config.NoEscapeWhiteList.Contains(kit.Name))
			{
				if (_config.UseRaidBlock && RaidBlocked(player)) return (false, Msg(player, NoEscapeCombatBlocked));

				if (_config.UseCombatBlock && CombatBlocked(player)) return (false, Msg(player, NoEscapeCombatBlocked));
			}

			if (playerData is {HasAmount: <= 0})
			{
				if (kit.Amount > 0 && playerData.Amount >= kit.Amount) return (false, Msg(player, KitLimit));

				var currentTime = GetCurrentTime();

				if (kit.Cooldown > 0 && playerData.Cooldown > currentTime)
					return (false, Msg(player, KitCooldown,
						FormatShortTime(TimeSpan.FromSeconds(playerData.Cooldown - currentTime))));
			}

			var totalCount = kit.beltCount + kit.wearCount + kit.mainCount;
			if (player.inventory.containerBelt.capacity - player.inventory.containerBelt.itemList.Count <
			    kit.beltCount ||
			    player.inventory.containerWear.capacity - player.inventory.containerWear.itemList.Count <
			    kit.wearCount ||
			    player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count <
			    kit.mainCount)
				if (totalCount > player.inventory.containerMain.capacity -
				    player.inventory.containerMain.itemList.Count)
					return (false, Msg(player, NotEnoughSpace));

			if (playerData is not {HasAmount: > 0} && kit.Sale &&
			    !_config.Economy.RemoveBalance(player, kit.Price))
				return (false, Msg(player, NotMoney));

			if (kit.UseBuilding && CopyPaste != null && !string.IsNullOrEmpty(kit.Building))
			{
				var success = CopyPaste?.Call("TryPasteFromSteamId", player.userID.Get(), kit.Building,
					_config.CopyPasteParameters.ToArray());
				if (success is string) return (false, Msg(player, BuildError));
			}

			return (true, null);
		}

		private void ProcessAutoKit(BasePlayer player, Kit kit)
		{
			var playerData = PlayerData.GetOrCreateKitData(player.UserIDString, kit.Name);
			if (!_config.IgnoreAutoKitChecking && !CheckConditionsForKit(player, kit, playerData).Success) return;
			
			kit.Get(player);
				
			kit.UseCommands(player);
                
			UpdatePlayerData(player, kit, false);
				
			Interface.CallHook("OnKitRedeemed", player, kit.Name);
		}

		private const int _itemsPerTick = 10;

		private IEnumerator GiveKitItems(BasePlayer player, Kit kit)
		{
			for (var index = 0; index < kit.Items.Count; index++)
			{
				kit.Items[index]?.Get(player);

				if (index % _itemsPerTick == 0)
					yield return CoroutineEx.waitForEndOfFrame;
			}
		}

		private void FastGiveKitItems(BasePlayer player, Kit kit)
		{
			kit.Items.ForEach(item => item?.Get(player));
		}

		private double GetCooldown(double cooldown, BasePlayer player)
		{
			var cd = Interface.CallHook("OnKitCooldown", player, cooldown);
			return cd != null ? Convert.ToDouble(cd) : cooldown;
		}

		private List<KitItem> GetPlayerItems(BasePlayer player)
		{
			var kitItems = new List<KitItem>();

			player.inventory.containerWear.itemList.FindAll(item => item != null && !item.IsLocked())
				.ForEach(item => kitItems.Add(ItemToKit(item, "wear")));

			player.inventory.containerMain.itemList.FindAll(item => item != null && !item.IsLocked())
				.ForEach(item => kitItems.Add(ItemToKit(item, "main")));

			player.inventory.containerBelt.itemList.FindAll(item => item != null && !item.IsLocked())
				.ForEach(item => kitItems.Add(ItemToKit(item, "belt")));

			return kitItems;
		}

		private KitItem ItemToKit(Item item, string container)
		{
			var kitItem = new KitItem
			{
				Amount = item.amount,
				Container = container,
				SkinID = item.skin,
				Blueprint = item.blueprintTarget,
				ShortName = item.info.shortname,
				DisplayName = !string.IsNullOrEmpty(item.name) ? item.name : string.Empty,
				Condition = item.condition,
				Weapon = null,
				Content = null,
				Chance = 100,
				Command = string.Empty,
				Position = item.position,
				Text = item.text,
				Image = string.Empty,
				Genes = new ItemGenes
				{
					Enabled = false,
					GeneTypes = new List<char>
					{
						'X', 'Y', 'G', 'W', 'H', 'W'
					}
				}
			};

			if (item.info.category == ItemCategory.Weapon && item.GetHeldEntity() is BaseProjectile weapon)
			{
				kitItem.Weapon = new Weapon
					{
						ammoType = weapon.primaryMagazine.ammoType.shortname,
						ammoAmount = weapon.primaryMagazine.contents
					};
			}

			if (item.contents != null && item.contents.itemList.Count > 0)
			{
				kitItem.Content = new List<ItemContent>(item.contents.itemList.Count);
				
				foreach (var cont in item.contents.itemList)
				{
					kitItem.Content.Add(new ItemContent
					{
						Amount = cont.amount,
						Condition = cont.condition,
						ShortName = cont.info.shortname,
						Skin = cont.skin
					});
				}
			}
			
			return kitItem;
		}

		#endregion

		#region Utils

		private List<string> GetAllCategories()
		{
            if (_data == null) LoadKits();
            
			var categories = Pool.Get<List<string>>();
			
			_data?.Kits?.ForEach(kit =>
			{
				if (!string.IsNullOrEmpty(kit.Category) && !categories.Contains(kit.Category))
					categories.Add(kit.Category);
			});
			
			return categories;
		}

		private TimerFormat GetTimerFormat(TimeSpan time)
		{
			if (time.Days > 0)
				return TimerFormat.DaysHoursMinutes;
			
			if (time.Hours > 0)
				return TimerFormat.HoursMinutes;
			
            if (time.Minutes > 0)
				return TimerFormat.MinutesSeconds;

			return TimerFormat.SecondsHundreth;
		}

		#region OpenedKits
		
		private readonly Dictionary<ulong, OpenedKits> _openKITS = new();
		
		private class OpenedKits
		{
			#region Fields

			public BasePlayer Player;
			
			public bool useMainUI;

			public ulong npcID;
			
			public string ActiveCategory;

			#endregion
			
			public OpenedKits(BasePlayer player, ulong targetID = DEFAULT_MAIN_TARGET_ID, bool mainUI = true)
			{
				Player = player;

				npcID = targetID;
                
				useMainUI = mainUI;
				
				UpdateKits(true);
			}

			#region Updates
			
			public List<Kit> availableKits, kitsToUpdate;

			public void SetKitsToUpdate(List<Kit> kits)
			{
				kitsToUpdate = kits;
			}

			public void UpdateKitsToUpdate()
			{
				var totalKitsAmount = GetTotalKitsAmount();
				SetKitsToUpdate(availableKits.SkipAndTake(currentPage * totalKitsAmount, totalKitsAmount));
			}
			
			public void UpdateKits(bool first = false)
			{
				availableKits = _instance.GetAvailableKits(Player, !first);

				UpdateKitsToUpdate();
			}

			public int GetTotalKitsAmount()
			{
				var targetUI=useMainUI ? _instance._config.UI : _instance._config.MenuUI;

				return targetUI.KitsOnString * targetUI.Strings;
			}

			#endregion

			#region Pages

			public int currentPage = DEFAULT_MAIN_PAGE;

			public bool showAll = DEFAULT_MAIN_SHOW_ALL;

			public void OnChangeCurrentPage(int page)
			{
				currentPage = page;
				
				if (availableKits.Count > currentPage * GetTotalKitsAmount() == false)
					OnChangeCurrentPage(Mathf.Max(0, currentPage - 1));
				
				UpdateKitsToUpdate();
			}
			
			public void OnChangeShowAll(bool newShowAll)
			{
				showAll = newShowAll;
				
				UpdateKits();
			}

			#endregion
		}

		private OpenedKits GetOpenedKits(BasePlayer player, bool mainUI = true)
		{
            if (!_openKITS.TryGetValue(player.userID, out var openedKits))
	            _openKITS.TryAdd(player.userID, openedKits = new OpenedKits(player, mainUI: mainUI));

            return openedKits;
		}
		
		private bool TryCreateOpenedKits(BasePlayer player, out OpenedKits openedKits, bool mainUI = true)
		{
			RemoveOpenedKits(player.userID);
			
			return _openKITS.TryAdd(player.userID, openedKits = new OpenedKits(player, mainUI: mainUI));
		}

		private bool IsOpenedKits(BasePlayer player)
		{
			return _openKITS.ContainsKey(player.userID);
		}
		
		private bool RemoveOpenedKits(BasePlayer player)
		{
			return _openKITS.Remove(player.userID);
		}

		private bool RemoveOpenedKits(ulong userID)
		{
			return _openKITS.Remove(userID);
		}

		private OpenedKits SetOpenedKits(BasePlayer player, 
			ulong targetID = DEFAULT_MAIN_TARGET_ID,
			int page = DEFAULT_MAIN_PAGE, 
			bool showAll = DEFAULT_MAIN_SHOW_ALL)
		{
			var openedKits = new OpenedKits(player, targetID, mainUI: true);
			openedKits.OnChangeCurrentPage(page);
			openedKits.OnChangeShowAll(showAll);
			_openKITS[player.userID] = openedKits;
			return openedKits;
		}
		
		#endregion

		#region Find Kits

		private Dictionary<string, int> _kitByName = new();

		private Dictionary<int, int> _kitByID = new();

		private Kit FindKitByName(string name)
		{
			return _kitByName.TryGetValue(name, out var index) ? _data.Kits[index] : null;
		}

		private Kit FindKitByID(int id)
		{
			return _kitByID.TryGetValue(id, out var index) ? _data.Kits[index] : null;
		}

		private bool TryFindKitByID(int id, out Kit kit)
		{
			if (_kitByID.TryGetValue(id, out var index))
			{
				kit = _data.Kits[index];
				return kit != null;
			}

			kit = null;
			return false;
		}

		#endregion

		#region Wipe

		private Coroutine _wipePlayers;

		private IEnumerator StartOnAllPlayers(string[] players,
			Action<string> callback = null,
			Action onFinish = null)
		{
			for (var i = 0; i < players.Length; i++)
			{
				callback?.Invoke(players[i]);

				if (i % 10 == 0)
					yield return CoroutineEx.waitForFixedUpdate;
			}

			onFinish?.Invoke();
			
			_wipePlayers = null;
		}

		private void DoWipePlayers(Action<int> callback = null)
		{
			try
			{
				var players = PlayerData.GetFiles();
				if (players is {Length: > 0})
				{
					var playersCount = players.Length;
					
					_wipePlayers =
						ServerMgr.Instance.StartCoroutine(StartOnAllPlayers(
							players, 
							userID => PlayerData.DoWipe(userID),
							() => _usersData?.Clear()));

					callback?.Invoke(playersCount);
				}
			}
			catch (Exception e)
			{
				PrintError($"[On Server Wipe] in wipe players, error: {e.Message}");
			}
		}

		#endregion

		private void CacheKits()
		{
			_kitByName.Clear();
			_kitByID.Clear();

			for (var index = 0; index < _data.Kits.Count; index++)
			{
				var kit = _data.Kits[index];

				kit.Update();
				kit.ID = _lastKitID++;

				_kitByName[kit.Name] = index;
				_kitByID[kit.ID] = index;
			}
		}
        
		private void DoRemoveKit(Kit kit)
		{
			_kitByName.Remove(kit.Name);
			_kitByID.Remove(kit.ID);
			_data.Kits.Remove(kit);

			CacheKits();
			SaveKits();
		}
		
		private void RegisterPermissions()
		{
			permission.RegisterPermission(PERM_ADMIN, this);

			if (!permission.PermissionExists(_config.ChangeAutoKitPermission))
				permission.RegisterPermission(_config.ChangeAutoKitPermission, this);

			_data.Kits.ForEach(kit =>
			{
				var perm = kit.Permission;

				if (!string.IsNullOrEmpty(perm) && !permission.PermissionExists(perm))
					permission.RegisterPermission(perm, this);

				if (!string.IsNullOrEmpty(kit.Category))
				{
					var categoryPerm = "kits." + kit.Category;
					if (!permission.PermissionExists(categoryPerm)) 
						permission.RegisterPermission(categoryPerm, this);
				}
			});
		}

		private void RegisterCommands()
		{
			AddCovalenceCommand(_config.Commands, nameof(CmdOpenKits));
		}

		private void LoadServerPanel()
		{
			_serverPanelCategory.spStatus = ServerPanel is {IsLoaded: true};

			ServerPanel?.Call("API_OnServerPanelProcessCategory", Name);
		}
		
		private bool RaidBlocked(BasePlayer player)
		{
			return Convert.ToBoolean(NoEscape?.Call("IsRaidBlocked", player) ?? false);
		}

		private bool CombatBlocked(BasePlayer player)
		{
			return Convert.ToBoolean(NoEscape?.Call("IsCombatBlocked", player) ?? false);
		}

		private void StopEditing(BasePlayer player)
		{
			_itemEditing.Remove(player.userID);
			_kitEditing.Remove(player.userID);
		}

		private void FillCategories()
		{
			ItemManager.itemList?.ForEach(item =>
			{
				var itemCategory = item.category.ToString();

				if (_itemsCategories.ContainsKey(itemCategory))
				{
					if (!_itemsCategories[itemCategory].Contains((item.itemid, item.shortname)))
						_itemsCategories[itemCategory].Add((item.itemid, item.shortname));
				}
				else
				{
					_itemsCategories.Add(itemCategory, new List<(int itemID, string shortName)>
					{
						(item.itemid, item.shortname)
					});
				}
			});
		}

		private void LoadItems()
		{
			for (var k = 0; k < _data.Kits.Count; k++)
			{
				var kit = _data.Kits[k];
				var belt = 0;
				var main = 0;
				var wear = 0;

				for (var i = 0; i < kit.Items.Count; i++)
				{
					var item = kit.Items[i];
					
					if (item.Genes is {Enabled: true} && !item.Genes.TryInit())
						PrintError(
							$"Can't load the item in kit '{kit.Name}'. The number of genes in the item is incorrect ({item?.Genes?.GeneTypes.Count} genes, but it should be 6).");

					if (item.Position == -1)
						switch (item.Container)
						{
							case "belt":
								item.Position = belt;
								belt++;
								break;
							case "main":
								item.Position = main;
								main++;
								break;
							case "wear":
								item.Position = wear;
								wear++;
								break;
						}
				}
			}

			SaveKits();
		}

		#region Image Helpers

		private string GetImage(string name)
		{
#if CARBON
			return imageDatabase.GetImageString(name);
#else
			return Convert.ToString(ImageLibrary?.Call("GetImage", name));
#endif
		}

		private void LoadImages()
		{
#if CARBON
			imageDatabase = BaseModule.GetModule<ImageDatabaseModule>();
#endif
			_enabledImageLibrary = true;

			var imagesList = new Dictionary<string, string>
			{
				["mevent_arrow_up"] = "https://i.ibb.co/BcSZtPm/arrow-up.png",
				["mevent_arrow_down"] = "https://i.ibb.co/C60QSX0/down.png"
			};

			_data.Kits.ForEach(kit =>
			{
				RegisterImage(kit.Image, ref imagesList);

				kit.Items.ForEach(item => RegisterImage(item.Image, ref imagesList));
			});

			if (_config.UI.Logo.Enabled) RegisterImage(_config.UI.Logo.Image, ref imagesList);

#if CARBON
            imageDatabase.Queue(true, imagesList);
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

		private void BroadcastILNotInstalled()
		{
			for (var i = 0; i < 5; i++) PrintError("IMAGE LIBRARY IS NOT INSTALLED.");
		}

		private static void RegisterImage(string image, ref Dictionary<string, string> imagesList)
		{
			if (!string.IsNullOrEmpty(image))
				imagesList.TryAdd(image, image);
		}

		#endregion

		private static string HexToCuiColor(string hex, float alpha = 100)
		{
			if (string.IsNullOrEmpty(hex)) hex = "#FFFFFF";

			var str = hex.Trim('#');
			if (str.Length != 6) throw new Exception(hex);
			var r = byte.Parse(str.AsSpan(0, 2), NumberStyles.HexNumber);
			var g = byte.Parse(str.AsSpan(2, 2), NumberStyles.HexNumber);
			var b = byte.Parse(str.AsSpan(4, 2), NumberStyles.HexNumber);

			return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {alpha / 100f}";
		}

		private static string FormatShortTime(TimeSpan time)
		{
			return time.ToShortString();
		}

		private static void CreateOutLine(ref CuiElementContainer container, string parent, string color,
			float size = 2)
		{
			container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0",
						AnchorMax = "1 0",
						OffsetMin = $"{size} 0",
						OffsetMax = $"-{size} {size}"
					},
					Image = {Color = color}
				},
				parent);
			container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 1",
						AnchorMax = "1 1",
						OffsetMin = $"{size} -{size}",
						OffsetMax = $"-{size} 0"
					},
					Image = {Color = color}
				},
				parent);
			container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "0 1",
						OffsetMin = "0 0",
						OffsetMax = $"{size} 0"
					},
					Image = {Color = color}
				},
				parent);
			container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "1 0",
						AnchorMax = "1 1",
						OffsetMin = $"-{size} 0",
						OffsetMax = "0 0"
					},
					Image = {Color = color}
				},
				parent);
		}

		private List<Kit> GetAvailableKitList(BasePlayer player, 
			string targetId = "0", 
			bool showAll = false,
			bool checkAmount = true, bool gui = false)
		{
			if (showAll && IsAdmin(player))
				return _data.Kits;
			
			return _data.Kits.FindAll(kit => IsKitAvailable(player, targetId, checkAmount, gui, kit, DEFAULT_MAIN_TARGET_ID.ToString()));
		}

		private List<Kit> GetAvailableKits(BasePlayer player, bool checkOpenedKits = true)
		{
			string activeCategory = null;
			var targetId = DEFAULT_MAIN_TARGET_ID;
			if (checkOpenedKits)
			{
				var openedKits = GetOpenedKits(player);
				if (openedKits.showAll && IsAdmin(player))
					return _data.Kits;

				targetId = openedKits.npcID;
				activeCategory = openedKits.ActiveCategory;
			}
			
			return _data.Kits.FindAll(kit => IsKitAvailable(player, targetId.ToString(), true, true, kit, DEFAULT_MAIN_TARGET_ID.ToString(), activeCategory));
		}
		
		private bool IsKitAvailable(BasePlayer player, string targetId, bool checkAmount, bool gui, Kit kit,
			string ignoredTargetID,
			string activeCategory = null)
		{
			if (kit.Hide || (gui && _config.KitsHidden.Contains(kit.Name)))
				return false;
			
			if (!string.IsNullOrEmpty(activeCategory) && activeCategory != kit.Category) return false;
			
			if (targetId != ignoredTargetID && !(_config.NpcKits.TryGetValue(targetId, out var npcKitsData) &&
			                                     npcKitsData.Kits.Contains(kit.Name)))
				return false;

			var data = PlayerData.GetOrCreateKitData(player.UserIDString, kit.Name);
			if (checkAmount && !_config.ShowUsesEnd && !(kit.Amount == 0 || (kit.Amount > 0 &&
			                                                                 (data?.Amount ?? 0) < kit.Amount)))
				return false;

			return _config.ShowAllKits || player.HasPermission(kit.Permission) ||
			       data is {HasAmount: > 0};
		}

		private List<Kit> GetAutoKits(BasePlayer player)
		{
			return _data.Kits
				.FindAll(kit => kit.Name == "autokit" || (_config.AutoKits.Contains(kit.Name) &&
				                                          (string.IsNullOrEmpty(kit.Permission) ||
				                                           player.HasPermission(kit.Permission))));
		}
		
		private int SecondsFromWipe()
		{
			return (int) DateTime.UtcNow
				.Subtract(SaveRestore.SaveCreatedTime.ToUniversalTime()).TotalSeconds;
		}

		private double LeftWipeBlockTime(double cooldown)
		{
			var leftTime = cooldown - SecondsFromWipe();
			return Math.Max(leftTime, 0);
		}

		private double UnBlockTime(double amount)
		{
			return TimeSpan.FromTicks(SaveRestore.SaveCreatedTime.ToUniversalTime().Ticks).TotalSeconds + amount;
		}

		private static double GetCurrentTime()
		{
			return TimeSpan.FromTicks(DateTime.UtcNow.Ticks).TotalSeconds;
		}

		private bool IsAdmin(BasePlayer player)
		{
			return player != null && ((player.IsAdmin && _config.FlagAdmin) || player.HasPermission(PERM_ADMIN));
		}

		private void UpdateOpenedUI(string id, string permName)
		{
			if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(permName) ||
			    !_data.Kits.Exists(x => x.Permission.Equals(permName))) return;

			var player = BasePlayer.Find(id);
			if (player == null) return;

			if (IsOpenedKits(player)) MainUi(player);
		}

		#endregion

		#region Log

		private void Log(BasePlayer player, string kitname)
		{
			if (player == null) return;

			var text = $"{player.displayName}[{player.UserIDString}] - Received Kit: {kitname}";

			if (_config.Logs.Console)
				Puts(text);

			if (_config.Logs.File)
				LogToFile(Name, $"[{DateTime.Now}] {text}", this);
		}

		#endregion

		#region Lang

		private const string
			UI_MeventRust_InfoKit_Title = "UI_MeventRust_InfoKit_Title",
			UI_MeventRust_Amount = "UI_MeventRust_Amount",
			UI_Error_Title = "UI_Error_Title",
			
			NoILError = "NoILError",
			KitShowInfo = "KitShowInfo",
			KitYouHave = "KitYouHave",
			EditRemoveField = "EditRemoveField",
			ChangeAutoKitOn = "ChangeAutoKitOn",
			ChangeAutoKitOff = "ChangeAutoKitOff",
			NoEscapeCombatBlocked = "NoEscapeCombatBlocked",
			NoEscapeRaidBlocked = "NoEscapeRaidBlocked",
			NotMoney = "NotMoney",
			PriceFormat = "PriceFormat",
			KitExist = "KitExist",
			KitNotExist = "KitNotExist",
			KitRemoved = "KitRemoved",
			AccessDenied = "AccessDenied",
			KitLimit = "KitLimit",
			KitCooldown = "KitCooldown",
			KitCreate = "KitCreate",
			KitClaimed = "KitClaimed",
			NotEnoughSpace = "NotEnoughtSpace",
			NotifyTitle = "NotifyTitle",
			Close = "Close",
			MainTitle = "MainTitle",
			Back = "Back",
			Next = "Next",
			NotAvailableKits = "NoAvailabeKits",
			CreateKit = "CreateKit",
			ListKits = "ListKits",
			ShowAll = "ShowAll",
			KitInfo = "KitInfo",
			KitTake = "KitGet",
			ComeBack = "ComeBack",
			Edit = "Edit",
			ContainerMain = "ContainerMain",
			ContainerWear = "ContaineWear",
			ContainerBelt = "ContainerBelt",
			CreateOrEditKit = "CreateOrEditKit",
			EnableKit = "EnableKit",
			AutoKit = "AutoKit",
			EnabledSale = "EnabledSale",
			SaveKit = "SaveKit",
			CopyItems = "CopyItems",
			RemoveKit = "RemoveKit",
			EditingTitle = "EditingTitle",
			ItemName = "ItemName",
			CmdName = "CmdName",
			BtnSelect = "BtnSelect",
			BluePrint = "BluePrint",
			BtnSave = "BtnSave",
			ItemSearch = "ItemSearch",
			BtnClose = "BtnClose",
			KitAvailableTitle = "KitAvailable",
			KitsList = "KitsList",
			KitsHelp = "KitsHelp",
			KitNotFound = "KitNotFound",
			RemoveItem = "RemoveItem",
			NoPermission = "NoPermission",
			BuildError = "BuildError",
			BBlocked = "BuildingBlocked",
			NoPermissionDescription = "NoPermissionDescription";

		protected override void LoadDefaultMessages()
		{
			var en = new Dictionary<string, string>
			{
				[KitExist] = "Kit with the same name already exist",
				[KitCreate] = "You have created a new kit - {0}",
				[KitNotExist] = "This kit doesn't exist",
				[KitRemoved] = "Kit {0} was removed",
				[AccessDenied] = "Access denied",
				[KitLimit] = "Usage limite reached",
				[KitCooldown] = "You will be able to use this kit after: {0}",
				[NotEnoughSpace] = "Can't redeem kit. Not enought space",
				[KitClaimed] = "You have claimed kit - {0}",
				[NotifyTitle] = "KITS",
				[Close] = "✕",
				[MainTitle] = "KITS",
				[Back] = "Back",
				[Next] = "Next",
				[NotAvailableKits] = "NO KITS AVAILABLE FOR YOU :(",
				[CreateKit] = "+ ADD KIT",
				[ListKits] = "List of kits",
				[ShowAll] = "SHOW ALL",
				[KitInfo] = "i",
				[KitTake] = "Take",
				[ComeBack] = "Come back",
				[Edit] = "+ EDIT KIT",
				[ContainerMain] = "Main",
				[ContainerWear] = "Wear",
				[ContainerBelt] = "Belt",
				[CreateOrEditKit] = "Create/Edit Kit",
				[EnableKit] = "Enable kit",
				[AutoKit] = "Auto kit",
				[EnabledSale] = "Enable sale",
				[SaveKit] = "Save kit",
				[CopyItems] = "Copy items from inventory",
				[RemoveKit] = "Remove kit",
				[EditingTitle] = "Item editing",
				[ItemName] = "Item",
				[CmdName] = "Command",
				[BtnSelect] = "Select",
				[BluePrint] = "Blueprint",
				[BtnSave] = "Save",
				[ItemSearch] = "Item search",
				[BtnClose] = "CLOSE",
				[KitAvailableTitle] = "KIT AVAILABLE\nTO RECEIVE",
				[KitsList] = "List of kits: {0}",
				[KitsHelp] =
					"KITS HELP\n- /{0} help - get help with kits\n- /{0} list - get a list of available kits\n- /{0} [name] - get the kit",
				[KitNotFound] = "Kit '{0}' not found",
				[RemoveItem] = "✕",
				[NoPermission] = "You don't have permission to get this kit",
				[BuildError] = "Can't place the building here",
				[BBlocked] = "Cannot do that while building blocked.",
				[NoPermissionDescription] = "PURCHASE THIS KIT AT\nSERVERNAME.GG",
				[PriceFormat] = "{0}$",
				[NotMoney] = "You don't have enough money!",
				[NoEscapeRaidBlocked] = "You cannot take this kit when you are raid blocked",
				[NoEscapeCombatBlocked] = "You cannot take this kit when you are combat blocked",
				[ChangeAutoKitOn] = "You have enabled autokits",
				[ChangeAutoKitOff] = "You have disabled autokits",
				[EditRemoveField] = "✕",
				[KitYouHave] = "YOU HAVE",
				[KitShowInfo] = "Show Info",
				[NoILError] = "The plugin does not work correctly, contact the administrator!",
				[UI_MeventRust_InfoKit_Title] = "KIT CONTENTS",
				[UI_MeventRust_Amount] = "QTY: <color=#D74933>{0}</color>/{1}",
				[UI_Error_Title] = "XXX",
			};
			
			var ru = new Dictionary<string, string>
			{
				[KitExist] = "Набор с похожим названием уже существует",
				[KitCreate] = "Вы создали новый набор - {0}",
				[KitNotExist] = "Набор не найден",
				[KitRemoved] = "Набор {0} удалён",
				[AccessDenied] = "Доступ запрещён",
				[KitLimit] = "Достигнут лимит использования",
				[KitCooldown] = "Вы сможете использовать этот набор после: {0}",
				[NotEnoughSpace] = "Невозможно получить набор. Не достаточно места в инвентаре",
				[KitClaimed] = "Вы получили набор - {0}",
				[NotifyTitle] = "KITS",
				[Close] = "✕",
				[MainTitle] = "НАБОРЫ",
				[Back] = "Назад",
				[Next] = "Вперёд",
				[NotAvailableKits] = "ДЛЯ ВАС НЕТ ДОСТУПНЫХ НАБОРОВ :(",
				[CreateKit] = "+ СОЗДАТЬ НАБОР",
				[ListKits] = "Список наборов",
				[ShowAll] = "Показать все",
				[KitInfo] = "i",
				[KitTake] = "Получить",
				[ComeBack] = "Назад",
				[Edit] = "+ РЕДАКТИРОВАТЬ",
				[ContainerMain] = "Основной",
				[ContainerWear] = "Одежда",
				[ContainerBelt] = "Пояс",
				[CreateOrEditKit] = "Создать/Изменить Набор",
				[EnableKit] = "Включить",
				[AutoKit] = "Автокит",
				[EnabledSale] = "Включить продажу",
				[SaveKit] = "Сохранить набор",
				[CopyItems] = "Копировать предметы из инвентаря",
				[RemoveKit] = "Удалить набор",
				[EditingTitle] = "Редактирование предмета",
				[ItemName] = "Item",
				[CmdName] = "Command",
				[BtnSelect] = "Выбрать",
				[BluePrint] = "Blueprint",
				[BtnSave] = "Сохранить",
				[ItemSearch] = "Поиск предмета",
				[BtnClose] = "ЗАКРЫТЬ",
				[KitAvailableTitle] = "НАБОР ДОСТУПЕН\nДЛЯ ПОЛУЧЕНИЯ",
				[KitsList] = "Список наборов: {0}",
				[KitsHelp] =
					"ИНФОРМАЦИЯ О НАБОРАх\n- /{0} help - получить информацию о наборах\n- /{0} list - получить список доступных наборов\n- /{0} [name] - получить набор",
				[KitNotFound] = "Набор '{0}' не найден",
				[RemoveItem] = "✕",
				[NoPermission] = "У вас нет прав на получение этого набора",
				[BuildError] = "Здесть невозможность установить строение",
				[BBlocked] = "Получение набора в Building Block запрещено!",
				[NoPermissionDescription] = "КУПИТЕ ЭТОТ НАБОР НА\nSERVERNAME.GG",
				[PriceFormat] = "{0}$",
				[NotMoney] = "У вас недостаточно денег!",
				[NoEscapeRaidBlocked] = "У вас блокировка рейда! Вы не можете взять этот набор",
				[NoEscapeCombatBlocked] = "У вас блокировка боя! Вы не можете взять этот набор",
				[ChangeAutoKitOn] = "Вы включили автокиты",
				[ChangeAutoKitOff] = "Вы выключили автокиты",
				[EditRemoveField] = "✕",
				[KitYouHave] = "У ВАС ЕСТЬ",
				[KitShowInfo] = "ПОКАЗАТЬ ИНФО",
				[NoILError] = "Плагин работает некорректно, свяжитесь с администратором!",
				[UI_MeventRust_InfoKit_Title] = "СОДЕРЖИМОЕ НАБОРА",
				[UI_MeventRust_Amount] = "К-ВО: <color=#D74933>{0}</color>/{1}",
				[UI_Error_Title] = "XXX",
			};
			
			var de = new Dictionary<string, string>
			{
				[KitExist] = "Kit mit dem gleichen Namen existiert bereits",
				[KitCreate] = "Sie haben einen neuen Kit erstellt - {0}",
				[KitNotExist] = "Dieser Kit existiert nicht",
				[KitRemoved] = "Kit {0} wurde entfernt",
				[AccessDenied] = "Zugriff verweigert",
				[KitLimit] = "Nutzungslimit erreicht",
				[KitCooldown] = "Sie können diesen Kit nach verwenden: {0}",
				[NotEnoughSpace] = "Kit kann nicht eingelöst werden. Nicht genügend Platz",
				[KitClaimed] = "Sie haben den Kit beansprucht - {0}",
				[NotifyTitle] = "KITS",
				[Close] = "✕",
				[MainTitle] = "KITS",
				[Back] = "Zurück",
				[Next] = "Weiter",
				[NotAvailableKits] = "KEINE KITS FÜR SIE VERFÜGBAR :(",
				[CreateKit] = "+ ADD KIT",
				[ListKits] = "Liste der Kits",
				[ShowAll] = "Alle anzeigen",
				[KitInfo] = "i",
				[KitTake] = "Nehmen",
				[ComeBack] = "Zurück",
				[Edit] = "+ EDIT KIT",
				[ContainerMain] = "Hauptinventar",
				[ContainerWear] = "Ausrüstung",
				[ContainerBelt] = "Gürtel",
				[CreateOrEditKit] = "Kit erstellen/bearbeiten",
				[EnableKit] = "Kit aktivieren",
				[AutoKit] = "Auto-Kit",
				[EnabledSale] = "Verkauf aktivieren",
				[SaveKit] = "Kit speichern",
				[CopyItems] = "Gegenstände aus Inventar kopieren",
				[RemoveKit] = "Kit entfernen",
				[EditingTitle] = "Gegenstand bearbeiten",
				[ItemName] = "Gegenstand",
				[CmdName] = "Befehl",
				[BtnSelect] = "Auswählen",
				[BluePrint] = "Blaupause",
				[BtnSave] = "Speichern",
				[ItemSearch] = "Gegenstandsuche",
				[BtnClose] = "SCHLIESSEN",
				[KitAvailableTitle] = "KIT VERFÜGBAR\nZUR ENTNAHME",
				[KitsList] = "Liste der Kits: {0}",
				[KitsHelp] =
					"HILFE ZU KITS\n- /{0} help - Hilfe zu Kits erhalten\n- /{0} list - Liste der verfügbaren Kits erhalten\n- /{0} [name] - Kit erhalten",
				[KitNotFound] = "Kit '{0}' nicht gefunden",
				[RemoveItem] = "✕",
				[NoPermission] = "Sie haben keine Berechtigung, diesen Kit zu erhalten",
				[BuildError] = "Gebäude kann hier nicht platziert werden",
				[BBlocked] = "Aktion während Baublock nicht möglich.",
				[NoPermissionDescription] = "KAUFEN SIE DIESEN KIT AUF\nSERVERNAME.GG",
				[PriceFormat] = "{0}$",
				[NotMoney] = "Sie haben nicht genug Geld!",
				[NoEscapeRaidBlocked] = "Sie können diesen Kit nicht nehmen, wenn Sie im Raidblock sind",
				[NoEscapeCombatBlocked] = "Sie können diesen Kit nicht nehmen, wenn Sie im Kampfblock sind",
				[ChangeAutoKitOn] = "Sie haben Auto-Kits aktiviert",
				[ChangeAutoKitOff] = "Sie haben Auto-Kits deaktiviert",
				[EditRemoveField] = "✕",
				[KitYouHave] = "SIE HABEN",
				[KitShowInfo] = "Info anzeigen",
				[NoILError] = "Das Plugin funktioniert nicht korrekt, kontaktieren Sie den Administrator!",
				[UI_MeventRust_InfoKit_Title] = "KIT-INHALT",
				[UI_MeventRust_Amount] = "ZAHL: <color=#D74933>{0}</color>/{1}",
				[UI_Error_Title] = "XXX",
			};
			
			var fr = new Dictionary<string, string>
			{
				[KitExist] = "Kit avec le même nom existe déjà",
				[KitCreate] = "Vous avez créé un nouveau kit - {0}",
				[KitNotExist] = "Ce kit n'existe pas",
				[KitRemoved] = "Le kit {0} a été supprimé",
				[AccessDenied] = "Accès refusé",
				[KitLimit] = "Limite d'utilisation atteinte",
				[KitCooldown] = "Vous pourrez utiliser ce kit après : {0}",
				[NotEnoughSpace] = "Impossible de récupérer le kit. Pas assez d'espace",
				[KitClaimed] = "Vous avez réclamé le kit - {0}",
				[NotifyTitle] = "KITS",
				[Close] = "✕",
				[MainTitle] = "KITS",
				[Back] = "Retour",
				[Next] = "Suivant",
				[NotAvailableKits] = "PAS DE KITS DISPONIBLES POUR VOUS :(",
				[CreateKit] = "+ ADD KIT",
				[ListKits] = "Liste des kits",
				[ShowAll] = "Tout afficher",
				[KitInfo] = "i",
				[KitTake] = "Prendre",
				[ComeBack] = "Retour",
				[Edit] = "+ EDIT KIT",
				[ContainerMain] = "Principal",
				[ContainerWear] = "Équipement",
				[ContainerBelt] = "Ceinture",
				[CreateOrEditKit] = "Créer/Modifier un kit",
				[EnableKit] = "Activer le kit",
				[AutoKit] = "Kit auto",
				[EnabledSale] = "Activer la vente",
				[SaveKit] = "Enregistrer le kit",
				[CopyItems] = "Copier les objets de l'inventaire",
				[RemoveKit] = "Supprimer le kit",
				[EditingTitle] = "Modification de l'objet",
				[ItemName] = "Objet",
				[CmdName] = "Commande",
				[BtnSelect] = "Sélectionner",
				[BluePrint] = "Blueprint",
				[BtnSave] = "Enregistrer",
				[ItemSearch] = "Recherche d'objet",
				[BtnClose] = "FERMER",
				[KitAvailableTitle] = "KIT DISPONIBLE\nPOUR RÉCUPÉRATION",
				[KitsList] = "Liste des kits : {0}",
				[KitsHelp] =
					"AIDE SUR LES KITS\n- /{0} help - obtenir de l'aide sur les kits\n- /{0} list - obtenir la liste des kits disponibles\n- /{0} [name] - obtenir le kit",
				[KitNotFound] = "Kit '{0}' introuvable",
				[RemoveItem] = "✕",
				[NoPermission] = "Vous n'avez pas la permission d'obtenir ce kit",
				[BuildError] = "Impossible de placer le bâtiment ici",
				[BBlocked] = "Impossible d'effectuer cette action pendant le blocage de construction.",
				[NoPermissionDescription] = "ACHETEZ CE KIT SUR\nSERVERNAME.GG",
				[PriceFormat] = "{0}$",
				[NotMoney] = "Vous n'avez pas assez d'argent !",
				[NoEscapeRaidBlocked] = "Vous ne pouvez pas prendre ce kit lorsque vous êtes bloqué en raid",
				[NoEscapeCombatBlocked] = "Vous ne pouvez pas prendre ce kit lorsque vous êtes bloqué au combat",
				[ChangeAutoKitOn] = "Vous avez activé les kits automatiques",
				[ChangeAutoKitOff] = "Vous avez désactivé les kits automatiques",
				[EditRemoveField] = "✕",
				[KitYouHave] = "VOUS AVEZ",
				[KitShowInfo] = "Afficher les infos",
				[NoILError] = "Le plugin ne fonctionne pas correctement, contactez l'administrateur !",
				[UI_MeventRust_InfoKit_Title] = "CONTENU DU KIT",
				[UI_MeventRust_Amount] = "QTE: <color=#D74933>{0}</color>/{1}",
				[UI_Error_Title] = "XXX",
			};

			var categories = GetAllCategories();
			try
			{
				categories.Add(CategoryAll);
				
				categories.ForEach(category =>
				{
					var categoryName = category.ToUpper();
					en.TryAdd(category, categoryName);
					ru.TryAdd(category, categoryName);
					de.TryAdd(category, categoryName);
					fr.TryAdd(category, categoryName);
				});
			}
			finally
			{
				Pool.FreeUnmanaged(ref categories);
			}
            
			lang.RegisterMessages(en, this);
			lang.RegisterMessages(ru, this, "ru");
			lang.RegisterMessages(de, this, "de");
			lang.RegisterMessages(fr, this, "fr");
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
			SendReply(player, Msg(key, player.UserIDString, obj));
		}

		private string Msg(BasePlayer player, string kitName, string key, params object[] obj)
		{
			if (_config.CustomTitles.Enabled)
				if (_config.CustomTitles.KitTitles.TryGetValue(kitName, out var kitTitle) && kitTitle.Enabled)
					if (kitTitle.Titles.TryGetValue(key, out var titleConf) && titleConf.Enabled)
					{
						var msg = titleConf.GetMessage(player);
						if (!string.IsNullOrEmpty(msg)) return string.Format(msg, obj);
					}

			return Msg(player, key, obj);
		}

		private void SendNotify(BasePlayer player, string key, int type, params object[] obj)
		{
			if (_config.UseNotify && (Notify != null || UINotify != null))
				Interface.Oxide.CallHook("SendNotify", player, type, Msg(player, key, obj));
			else
				Reply(player, key, obj);
		}

		private void SendNotifyOrUI(BasePlayer player, string key, int type, bool chat, params object[] obj)
		{
			if (_config.UseNotify && (Notify != null || UINotify != null))
				Interface.Oxide.CallHook("SendNotify", player, type, Msg(player, key, obj));
			else if (chat)
				Reply(player, key, obj);
			else
				ErrorUi(player, Msg(player, key, obj));
		}

		private void SendMessageToNotifyOrUI(BasePlayer player, string message, int type, bool chat)
		{
			if (_config.UseNotify && (Notify != null || UINotify != null))
				Interface.Oxide.CallHook("SendNotify", player, type, message);
			else if (chat)
				SendReply(player, message);
			else
				ErrorUi(player, message);
		}

		#endregion

		#region API

		private bool TryClaimKit(BasePlayer player, string name, bool usingUI)
		{
			return !string.IsNullOrEmpty(name) &&
			       GiveKitToPlayer(player, FindKitByName(name), usingUI: usingUI);
		}

		private void GetKitNames(List<string> list)
		{
			list.AddRange(GetAllKits());
		}

		private string[] GetAllKits()
		{
			return _data.Kits.Select(kit => kit.Name).ToArray();
		}

		private object GetKitInfo(string name)
		{
			var kit = FindKitByName(name);
			if (kit == null) return null;

			var obj = new JObject
			{
				["name"] = kit.Name,
				["permission"] = kit.Permission,
				["max"] = kit.Amount,
				["image"] = kit.Image,
				["hide"] = kit.Hide,
				["description"] = kit.Description,
				["category"] = kit.Category,
				["cooldown"] = kit.Cooldown,
				["building"] = kit.Building,
				["authlevel"] = 0,
				["items"] = new JArray(kit.Items.Select(itemData => new JObject
				{
					["amount"] = itemData.Amount,
					["container"] = itemData.Container,
					["itemid"] = itemData.itemId,
					["skinid"] = itemData.SkinID,
					["weapon"] = !string.IsNullOrEmpty(itemData.Weapon?.ammoType),
					["blueprint"] = itemData.Blueprint,
					["mods"] = new JArray(itemData.Content?.Select(x =>
						ItemManager.FindItemDefinition(x.ShortName).itemid) ?? new List<int>())
				}))
			};

			return obj;
		}

		private string[] GetKitContents(string name)
		{
			var kit = FindKitByName(name);
			if (kit == null) return null;

			var items = new List<string>();
			foreach (var item in kit.Items)
			{
				var itemstring = $"{item.ShortName}_{item.Amount}";
				if (item.Content.Count > 0)
					itemstring = item.Content.Aggregate(itemstring, (current, mod) => current + $"_{mod.ShortName}");

				items.Add(itemstring);
			}

			return items.ToArray();
		}

		private double GetKitCooldown(string name)
		{
			return FindKitByName(name)?.Cooldown ?? 0;
		}

		private double PlayerKitCooldown(ulong ID, string name)
		{
			return PlayerData.GetNotLoadKitData(ID.ToString(), name)?.Cooldown ?? 0.0;
		}

		private int KitMax(string name)
		{
			return FindKitByName(name)?.Amount ?? 0;
		}

		private double PlayerKitMax(ulong ID, string name)
		{
			return PlayerData.GetNotLoadKitData(ID.ToString(), name)?.Amount ?? 0;
		}

		private string KitImage(string name)
		{
			return FindKitByName(name)?.Image ?? string.Empty;
		}

		private string GetKitImage(string name)
		{
			return KitImage(name);
		}

		private string GetKitDescription(string name)
		{
			return FindKitByName(name)?.Description ?? string.Empty;
		}

		private int GetKitMaxUses(string name)
		{
			return FindKitByName(name)?.Amount ?? 0;
		}

		private int GetPlayerKitUses(ulong userId, string name)
		{
			return GetPlayerKitUses(userId.ToString(), name);
		}

		private int GetPlayerKitUses(string userId, string name)
		{
			var kitData = PlayerData.GetNotLoadKitData(userId, name);
			return kitData?.Amount ?? 0;
		}

		private void SetPlayerKitUses(ulong userId, string name, int amount)
		{
			var data = PlayerData.GetOrCreateKitData(userId.ToString(), name);
			if (data == null) return;

			data.Amount = amount;
		}

		private double GetPlayerKitCooldown(string userId, string name)
		{
			return GetPlayerKitCooldown(ulong.Parse(userId), name);
		}

		private double GetPlayerKitCooldown(ulong userId, string name)
		{
			var data = PlayerData.GetNotLoadKitData(userId.ToString(), name);
			if (data == null) return 0;

			return Mathf.Max((float) (data.Cooldown - GetCurrentTime()), 0f);
		}

		private bool IsKitCooldown(Kit kit, PlayerData.KitData playerData, double currentTime, out bool cooldown,
			out bool wipeBlock)
		{
			wipeBlock = false;
			cooldown = kit.Cooldown > 0 && playerData.Cooldown > currentTime;
			if (cooldown)
				return true;

			wipeBlock = kit.CooldownAfterWipe > 0 && LeftWipeBlockTime(kit.CooldownAfterWipe) > 0;
			return wipeBlock;
		}

		private TimeSpan GetCooldownTimeRemaining(Kit kit, PlayerData.KitData playerData, double currentTime,
			bool isCooldown)
		{
			return isCooldown ? TimeSpan.FromSeconds(playerData.Cooldown - currentTime) : TimeSpan.FromSeconds(LeftWipeBlockTime(kit.CooldownAfterWipe));
		}

		private bool GiveKit(BasePlayer player, string name, bool usingUI)
		{
#if TESTING
			Puts($"[GiveKit] player={player?.UserIDString}, name={name}, usingUI={usingUI}");
#endif
			return GiveKitToPlayer(player, FindKitByName(name), true, usingUI: usingUI);
		}

		private bool isKit(string name)
		{
			return IsKit(name);
		}

		private bool IsKit(string name)
		{
			return _data.Kits.Exists(x => x.Name == name);
		}

		private bool HasKitAccess(string userId, string name)
		{
			var kit = FindKitByName(name);
			return kit != null && userId.HasPermission(kit.Permission);
		}

		private int GetPlayerKitAmount(string userId, string name)
		{
			var kit = FindKitByName(name);
			if (kit == null)
				return 0;

			var playerData = PlayerData.GetNotLoadKitData(userId, name);
			if (playerData is {HasAmount: > 0})
				return playerData.HasAmount;

			return 0;
		}

		private JObject GetKitObject(string name)
		{
			return FindKitByName(name)?.ToJObject;
		}

		private IEnumerable<Item> CreateKitItems(string name)
		{
			var kit = FindKitByName(name);
			if (kit == null) yield break;
			
			foreach (var kitItem in kit.Items.FindAll(kitItem => kitItem.Type == KitItemType.Item))
				yield return kitItem.BuildItem();
		}
		
		private CuiElementContainer API_OpenPlugin(BasePlayer player)
		{
			RemoveOpenedKits(player.userID);
			
			TryCreateOpenedKits(player, out _, false);
			
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
			}, "UI.Server.Panel.Content.Plugin", Layer + ".Background", Layer + ".Background");

			#endregion

			#region Main
			
			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1"
				},
				Image =
				{
					Color = "0 0 0 0"
				}
			}, Layer + ".Background", Layer + ".Main", Layer + ".Main");
			
			MainKitsHeader(player, container);

			MainKitsCategories(player, container);
            
			MainKitsContentUI(player, container, first: true);
			
			MainKitsDescriptionUI(player, ref container);

			#endregion
			
			return container;
		}
		
		#endregion

		#region Convert

		#region uMod Kits

		[ConsoleCommand("kits.convert")]
		private void OldKitsConvert(ConsoleSystem.Arg arg)
		{
			if (!arg.IsAdmin) return;

			OldData oldKits = null;

			try
			{
				oldKits = Interface.Oxide.DataFileSystem.ReadObject<OldData>("Kits/kits_data");
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			var amount = 0;

			oldKits?._kits.ToList().ForEach(oldKit =>
			{
				var kit = new Kit
				{
					ID = ++_lastKitID,
					Name = oldKit.Value.Name,
					DisplayName = oldKit.Value.Name,
					Permission = oldKit.Value.RequiredPermission,
					Amount = oldKit.Value.MaximumUses,
					Cooldown = oldKit.Value.Cooldown,
					Description = oldKit.Value.Description,
					Hide = oldKit.Value.IsHidden,
					Building = oldKit.Value.CopyPasteFile,
					Image = oldKit.Value.KitImage,
					Color = _config.KitColor,
					ShowInfo = true,
					Items = new List<KitItem>()
				};

				foreach (var item in oldKit.Value.MainItems)
					kit.Items.Add(KitItem.FromOld(item, "main"));

				foreach (var item in oldKit.Value.WearItems)
					kit.Items.Add(KitItem.FromOld(item, "wear"));

				foreach (var item in oldKit.Value.BeltItems)
					kit.Items.Add(KitItem.FromOld(item, "belt"));

				_data.Kits.Add(kit);

				var kitIndex = _data.Kits.IndexOf(kit);

				_kitByName[kit.Name] = kitIndex;
				_kitByID[kit.ID] = kitIndex;

				amount++;
			});

			Puts($"{amount} kits was converted!");

			SaveKits();
		}

		private class OldData
		{
			[JsonProperty] public Dictionary<string, OldKitsData> _kits = new(StringComparer.OrdinalIgnoreCase);
		}

		private class OldKitsData
		{
			public string Name;
			public string Description;
			public string RequiredPermission;

			public int MaximumUses;
			public int RequiredAuth;
			public int Cooldown;
			public int Cost;

			public bool IsHidden;

			public string CopyPasteFile;
			public string KitImage;

			public ItemData[] MainItems;
			public ItemData[] WearItems;
			public ItemData[] BeltItems;
		}

		private class ItemData
		{
			public string Shortname;

			public ulong Skin;

			public int Amount;

			public float Condition;

			public float MaxCondition;

			public int Ammo;

			public string Ammotype;

			public int Position;

			public int Frequency;

			public string BlueprintShortname;

			public ItemData[] Contents;
		}

		#endregion

		#region Old Data

		private void StartConvertOldData()
		{
			var data = LoadOldData();
			if (data != null)
				timer.In(0.3f, () =>
				{
					ConvertOldData(data);

					PrintWarning($"{data.Count} players was converted!");
				});
		}

		private Dictionary<ulong, Dictionary<string, OldKitData>> LoadOldData()
		{
			Dictionary<ulong, Dictionary<string, OldKitData>> players = null;
			try
			{
				players =
					Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<string, OldKitData>>>(
						$"{Name}/Data");
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			return players ?? new Dictionary<ulong, Dictionary<string, OldKitData>>();
		}

		private void ConvertOldData(Dictionary<ulong, Dictionary<string, OldKitData>> players)
		{
			foreach (var check in players)
			{
				var userId = check.Key.ToString();

				var data = PlayerData.GetOrCreate(userId);

				foreach (var kitData in check.Value)
					data.Kits[kitData.Key] = new PlayerData.KitData
					{
						Amount = kitData.Value.Amount,
						Cooldown = kitData.Value.Cooldown,
						HasAmount = kitData.Value.HasAmount
					};

				PlayerData.SaveAndUnload(userId);
			}
		}

		#region Classes

		private class OldKitData
		{
			public int Amount;

			public double Cooldown;

			public int HasAmount;
		}

		#endregion

		#endregion

		#endregion

		#region Player Data

		private Dictionary<string, PlayerData> _usersData = new();

		private class PlayerData
		{
			#region Main

			#region Fields

			[JsonProperty(PropertyName = "Kits Data", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, KitData> Kits = new();

			#endregion

			#region Classes

			public class KitData
			{
				[JsonProperty(PropertyName = "Amount")]
				public int Amount;

				[JsonProperty(PropertyName = "Cooldown")]
				public double Cooldown;

				[JsonProperty(PropertyName = "HasAmount")]
				public int HasAmount;
			}

			#endregion

			#endregion

			#region Helpers

			private static string BaseFolder()
			{
				return "Kits" + Path.DirectorySeparatorChar + "Players" + Path.DirectorySeparatorChar;
			}

			public static PlayerData GetOrLoad(string userId)
			{
				if (!userId.IsSteamId()) return null;

				return GetOrLoad(BaseFolder(), userId);
			}

			public static KitData GetOrLoadKitData(string userId,
				string kitName,
				bool addKit = true)
			{
				var data = GetOrLoad(userId);
				if (data == null) return null;

				if (data.Kits.TryGetValue(kitName, out var kitData))
					return kitData;

				return
					addKit
						? data.Kits[kitName] = new KitData()
						: null;
			}
			
			private static PlayerData GetOrLoad(string baseFolder, string userId, bool load = true)
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

			public static PlayerData GetOrCreate(string userId)
			{
				if (!userId.IsSteamId()) return null;

				return GetOrLoad(userId) ?? (_instance._usersData[userId] = new PlayerData());
			}

			public static KitData GetOrCreateKitData(string userId, string kitName)
			{
				var data = GetOrCreate(userId);
				if (data == null) return null;

				if (data.Kits.TryGetValue(kitName, out var kitData))
					return kitData;

				return data.Kits[kitName] = new KitData();
			}

			public static void Save()
			{
				foreach (var userId in _instance._usersData.Keys)
					Save(userId);
			}

			public static void Save(string userId)
			{
				if (!_instance._usersData.TryGetValue(userId, out var data))
					return;

				Interface.Oxide.DataFileSystem.WriteObject(BaseFolder() + userId, data);
			}

			public static void SaveAndUnload(string userId)
			{
				Save(userId);

				Unload(userId);
			}

			public static void Unload(string userId)
			{
				_instance._usersData.Remove(userId);
			}

			#endregion

			#region Utils

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

			#endregion

			#region Wipe

			public static void DoWipe(string userId, bool isWipe = true)
			{
				if (isWipe && _instance?._config?.SaveGivenKitsOnWipe == true)
				{
					var data = GetNotLoad(userId);
					if (data == null) return;

					data.Kits.RemoveAll((key, value) => value.HasAmount <= 0);

					if (data.Kits.Count > 0)
					{
						foreach (var kitData in data.Kits)
						{
							kitData.Value.Amount = 0;
							kitData.Value.Cooldown = 0;
						}

						Interface.Oxide.DataFileSystem.WriteObject(BaseFolder() + userId, data);
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

			public static bool ResetKit(string userId, string kitName)
			{
				var data = GetNotLoad(userId);
				if (data == null || !data.Kits.Remove(kitName)) return false;
				
				Interface.Oxide.DataFileSystem.WriteObject(BaseFolder() + userId, data);
				return true;
			}

			#endregion

			#region All Players

			public static void StartAll(Action<PlayerData> action)
			{
				var users = GetFiles(BaseFolder());

				foreach (var userId in users)
				{
					var loaded = _instance._usersData.ContainsKey(userId);

					var data = GetOrLoad(userId);
					if (data == null) continue;

					action.Invoke(data);

					Save(userId);

					if (!loaded)
						Unload(userId);
				}
			}

			public static List<PlayerData> GetAll()
			{
				var users = GetFiles(BaseFolder());

				var list = new List<PlayerData>();

				foreach (var userId in users)
				{
					var data = GetNotLoad(userId);
					if (data == null) continue;

					list.Add(data);
				}

				return list;
			}

			public static PlayerData GetNotLoad(string userId)
			{
				return GetOrLoad(BaseFolder(), userId, false);
			}

			public static KitData GetNotLoadKitData(string userId, string kitName)
			{
				var data = GetNotLoad(userId);
				return data?.Kits.GetValueOrDefault(kitName);
			}

			#endregion
		}

		#endregion

		#region Testing Functions

#if TESTING
		private static void SayDebug(string message)
		{
			Debug.Log($"[Kits] {message}");
		}
#endif

		#endregion
	}
}

#region Extension Methods

namespace Oxide.Plugins.KitsExtensionMethods
{
	// ReSharper disable ForCanBeConvertedToForeach
	// ReSharper disable LoopCanBeConvertedToQuery
	public static class ExtensionMethods
	{
		internal static Permission perm;

		public static bool IsURL(this string uriName)
		{
			return Uri.TryCreate(uriName, UriKind.Absolute, out var uriResult) &&
			       (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
		}

		public static bool All<T>(this IList<T> a, Func<T, bool> b)
		{
			for (var i = 0; i < a.Count; i++)
				if (!b(a[i]))
					return false;
			return true;
		}

		public static int Average(this IList<int> a)
		{
			if (a.Count == 0) return 0;
			var b = 0;
			for (var i = 0; i < a.Count; i++) b += a[i];
			return b / a.Count;
		}

		public static T ElementAt<T>(this IEnumerable<T> a, int b)
		{
			using var c = a.GetEnumerator();
			while (c.MoveNext())
			{
				if (b == 0) return c.Current;
				b--;
			}

			return default;
		}

		public static bool Exists<T>(this IEnumerable<T> a, Func<T, bool> b = null)
		{
			using var c = a.GetEnumerator();
			while (c.MoveNext())
				if (b == null || b(c.Current))
					return true;

			return false;
		}

		public static T FirstOrDefault<T>(this IEnumerable<T> a, Func<T, bool> b = null)
		{
			using (var c = a.GetEnumerator())
			{
				while (c.MoveNext())
					if (b == null || b(c.Current))
						return c.Current;
			}

			return default;
		}

		public static int RemoveAll<T, V>(this IDictionary<T, V> a, Func<T, V, bool> b)
		{
			var c = new List<T>();
			using (var d = a.GetEnumerator())
			{
				while (d.MoveNext())
					if (b(d.Current.Key, d.Current.Value))
						c.Add(d.Current.Key);
			}

			c.ForEach(e => a.Remove(e));
			return c.Count;
		}

		public static IEnumerable<V> Select<T, V>(this IEnumerable<T> a, Func<T, V> b)
		{
			var c = new List<V>();
			using var d = a.GetEnumerator();
			while (d.MoveNext()) c.Add(b(d.Current));

			return c;
		}

		public static List<TResult> Select<T, TResult>(this List<T> source, Func<T, TResult> selector)
		{
			if (source == null || selector == null) return new List<TResult>();

			var r = new List<TResult>(source.Count);
			for (var i = 0; i < source.Count; i++) r.Add(selector(source[i]));

			return r;
		}

		public static List<T> SkipAndTake<T>(this List<T> source, int skip, int take)
		{
			var index = Mathf.Min(Mathf.Max(skip, 0), source.Count);
			return source.GetRange(index, Mathf.Min(take, source.Count - index));
		}

		public static string[] Skip(this string[] a, int count)
		{
			if (a.Length == 0) return Array.Empty<string>();
			var c = new string[a.Length - count];
			var n = 0;
			for (var i = 0; i < a.Length; i++)
			{
				if (i < count) continue;
				c[n] = a[i];
				n++;
			}

			return c;
		}

		public static List<T> Skip<T>(this IList<T> source, int count)
		{
			if (count < 0)
				count = 0;

			if (source == null || count > source.Count)
				return new List<T>();

			var result = new List<T>(source.Count - count);
			for (var i = count; i < source.Count; i++)
				result.Add(source[i]);
			return result;
		}

		public static Dictionary<T, V> Skip<T, V>(
			this IDictionary<T, V> source,
			int count)
		{
			var result = new Dictionary<T, V>();
			using var iterator = source.GetEnumerator();
			for (var i = 0; i < count; i++)
				if (!iterator.MoveNext())
					break;

			while (iterator.MoveNext()) result.Add(iterator.Current.Key, iterator.Current.Value);

			return result;
		}

		public static List<T> Take<T>(this IList<T> a, int b)
		{
			var c = new List<T>();
			for (var i = 0; i < a.Count; i++)
			{
				if (c.Count == b) break;
				c.Add(a[i]);
			}

			return c;
		}

		public static Dictionary<T, V> Take<T, V>(this IDictionary<T, V> a, int b)
		{
			var c = new Dictionary<T, V>();
			foreach (var f in a)
			{
				if (c.Count == b) break;
				c.Add(f.Key, f.Value);
			}

			return c;
		}

		public static Dictionary<T, V> ToDictionary<S, T, V>(this IEnumerable<S> a, Func<S, T> b, Func<S, V> c)
		{
			var d = new Dictionary<T, V>();
			using var e = a.GetEnumerator();
			while (e.MoveNext()) d[b(e.Current)] = c(e.Current);

			return d;
		}

		public static List<T> ToList<T>(this IEnumerable<T> a)
		{
			var b = new List<T>();
			using (var c = a.GetEnumerator())
			{
				while (c.MoveNext()) b.Add(c.Current);
			}

			return b;
		}

		public static HashSet<T> ToHashSet<T>(this IEnumerable<T> a)
		{
			return new HashSet<T>(a);
		}

		public static List<T> Where<T>(this List<T> source, Predicate<T> predicate)
		{
			if (source == null)
				return new List<T>();

			if (predicate == null)
				return new List<T>();

			return source.FindAll(predicate);
		}

		public static List<T> Where<T>(this List<T> source, Func<T, int, bool> predicate)
		{
			if (source == null)
				return new List<T>();

			if (predicate == null)
				return new List<T>();

			var r = new List<T>();
			for (var i = 0; i < source.Count; i++)
				if (predicate(source[i], i))
					r.Add(source[i]);
			return r;
		}

		public static List<T> Where<T>(this IEnumerable<T> source, Func<T, bool> predicate)
		{
			var c = new List<T>();

			using (var d = source.GetEnumerator())
			{
				while (d.MoveNext())
					if (predicate(d.Current))
						c.Add(d.Current);
			}

			return c;
		}

		public static List<T> OfType<T>(this IEnumerable<BaseNetworkable> a) where T : BaseEntity
		{
			var b = new List<T>();
			using var c = a.GetEnumerator();
			while (c.MoveNext())
				if (c.Current is T entity)
					b.Add(entity);

			return b;
		}

		public static int Sum<T>(this IList<T> a, Func<T, int> b)
		{
			var c = 0;
			for (var i = 0; i < a.Count; i++)
			{
				var d = b(a[i]);
				if (!float.IsNaN(d)) c += d;
			}

			return c;
		}

		public static T LastOrDefault<T>(this List<T> source)
		{
			if (source == null || source.Count == 0)
				return default;

			return source[^1];
		}

		public static int Count<T>(this List<T> source, Func<T, bool> predicate)
		{
			if (source == null)
				return 0;

			if (predicate == null)
				return 0;

			var count = 0;
			for (var i = 0; i < source.Count; i++)
				checked
				{
					if (predicate(source[i])) count++;
				}

			return count;
		}

		public static TAccumulate Aggregate<TSource, TAccumulate>(this List<TSource> source, TAccumulate seed,
			Func<TAccumulate, TSource, TAccumulate> func)
		{
			if (source == null) throw new Exception("Aggregate: source is null");

			if (func == null) throw new Exception("Aggregate: func is null");

			var result = seed;
			for (var i = 0; i < source.Count; i++) result = func(result, source[i]);
			return result;
		}

		public static int Sum(this IList<int> a)
		{
			var c = 0;
			for (var i = 0; i < a.Count; i++)
			{
				var d = a[i];
				if (!float.IsNaN(d)) c += d;
			}

			return c;
		}

		public static bool HasPermission(this string userID, string b)
		{
			perm ??= Interface.Oxide.GetLibrary<Permission>();
			return !string.IsNullOrEmpty(userID) && (string.IsNullOrEmpty(b) || perm.UserHasPermission(userID, b));
		}

		public static bool HasPermission(this BasePlayer a, string b)
		{
			return a.UserIDString.HasPermission(b);
		}

		public static bool HasPermission(this ulong a, string b)
		{
			return a.ToString().HasPermission(b);
		}

		public static bool IsReallyConnected(this BasePlayer a)
		{
			return a.IsReallyValid() && a.net.connection != null;
		}

		public static bool IsKilled(this BaseNetworkable a)
		{
			return (object) a == null || a.IsDestroyed;
		}

		public static bool IsNull<T>(this T a) where T : class
		{
			return a == null;
		}

		public static bool IsNull(this BasePlayer a)
		{
			return (object) a == null;
		}

		public static bool IsReallyValid(this BaseNetworkable a)
		{
			return !((object) a == null || a.IsDestroyed || a.net == null);
		}

		public static void SafelyKill(this BaseNetworkable a)
		{
			if (a.IsKilled()) return;
			a.Kill();
		}

		public static bool CanCall(this Plugin o)
		{
			return o is {IsLoaded: true};
		}

		public static bool IsInBounds(this OBB o, Vector3 a)
		{
			return o.ClosestPoint(a) == a;
		}

		public static bool IsHuman(this BasePlayer a)
		{
			return !(a.IsNpc || !a.userID.IsSteamId());
		}

		public static BasePlayer ToPlayer(this IPlayer user)
		{
			return user.Object as BasePlayer;
		}

		public static List<TResult> SelectMany<TSource, TResult>(this List<TSource> source,
			Func<TSource, List<TResult>> selector)
		{
			if (source == null || selector == null)
				return new List<TResult>();

			var result = new List<TResult>(source.Count);
			source.ForEach(i => selector(i).ForEach(j => result.Add(j)));
			return result;
		}

		public static IEnumerable<TResult> SelectMany<TSource, TResult>(
			this IEnumerable<TSource> source,
			Func<TSource, IEnumerable<TResult>> selector)
		{
			using var item = source.GetEnumerator();
			while (item.MoveNext())
			{
				using var result = selector(item.Current).GetEnumerator();
				while (result.MoveNext()) yield return result.Current;
			}
		}

		public static int Sum<TSource>(this IEnumerable<TSource> source, Func<TSource, int> selector)
		{
			var sum = 0;

			using var element = source.GetEnumerator();
			while (element.MoveNext()) sum += selector(element.Current);

			return sum;
		}

		public static double Sum<TSource>(this IEnumerable<TSource> source, Func<TSource, double> selector)
		{
			var sum = 0.0;

			using var element = source.GetEnumerator();
			while (element.MoveNext()) sum += selector(element.Current);

			return sum;
		}

		public static bool Any<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
		{
			if (source == null) return false;

			using var element = source.GetEnumerator();
			while (element.MoveNext())
				if (predicate(element.Current))
					return true;

			return false;
		}
		
		public static string GetFieldTitle<T>(this string field)
		{
			var fieldInfo = typeof(T).GetField(field);
			if (fieldInfo == null) return field;

			var jsonAttribute = fieldInfo.GetCustomAttribute<JsonPropertyAttribute>();
			return jsonAttribute == null ? field : jsonAttribute.PropertyName;
		}
	}
}

#endregion Extension Methods 