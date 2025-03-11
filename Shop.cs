// #define TESTING

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Plugins.ShopExtensionMethods;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using Global = Rust.Global;
using Random = UnityEngine.Random;

#if CARBON
using Carbon.Base;
using Carbon.Modules;
#endif

namespace Oxide.Plugins
{
	[Info("Shop", "Mevent", "2.1.0")]
	public class Shop : RustPlugin
	{
		#region Fields

		[PluginReference] private Plugin
			ImageLibrary = null,
			ServerPanel = null,
			ItemCostCalculator = null,
			LangAPI = null,
			Notify = null,
			UINotify = null,
			NoEscape = null,
			Duel = null,
			Duelist = null;

		private static Shop _instance;

		private bool _enabledImageLibrary, _enabledServerPanel;

#if CARBON
        private ImageDatabaseModule imageDatabase;
#endif

		private const bool LangRu = true;

		private readonly Dictionary<int, ShopItem> _shopItems = new();

		private readonly Dictionary<ulong, Coroutine> _coroutines = new();

		private readonly Dictionary<string, List<(int itemID, string shortName)>> _itemsCategories = new();

		private readonly HashSet<string> _images = new();

		private const string
			Layer = "UI.Shop",
			ModalLayer = "UI.Shop.Modal",
			EditingLayer = "UI.Shop.Editing",
			CMD_Main_Console = "UI_Shop";

		public const string PermAdmin  =  "shop.admin";
		
		private const string
			PermFreeBypass = "shop.free",
			PermSetVM = "shop.setvm",
			PermSetNPC = "shop.setnpc";

		private const int _itemsPerTick = 10;

		private readonly Dictionary<ulong, NPCShop> _openedShopsNPC = new();

		private readonly Dictionary<ulong, Dictionary<string, object>> _itemEditing = new();

		private const BindingFlags bindingFlags = BindingFlags.Instance |
		                                          BindingFlags.NonPublic |
		                                          BindingFlags.Public;

		private readonly Dictionary<ulong, Dictionary<string, object>> _categoryEditing = new();

		private Timer _updateController;

		private readonly HashSet<ulong> _showAllCategories = new();

		private bool _isEnabledFavorites;

		private (bool status, string message) _initializedStatus = (false, string.Empty);

		#endregion

		#region Config

		private static Configuration _config;

		private class Configuration
		{
			#region Fields

			[JsonProperty(PropertyName = LangRu ? "Команды" : "Commands",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] Commands = {"shop", "shops"};

			[JsonProperty(PropertyName =
				LangRu ? "Включить переводы денег между игроками?" : "Enable money transfers between players?")]
			public bool Transfer = true;

			[JsonProperty(PropertyName = LangRu ? "Включить логирование в консоль?" : "Enable logging to the console?")]
			public bool LogToConsole = true;

			[JsonProperty(PropertyName = LangRu ? "Включить логирование в файл?" : "Enable logging to the file?")]
			public bool LogToFile = true;

			[JsonProperty(PropertyName =
				LangRu
					? "Загружать изображения при подключении к серверу?"
					: "Load images when logging into the server?")]
			public bool LoginImages = true;

			[JsonProperty(PropertyName = LangRu ? "Работать с Notify?" : "Work with Notify?")]
			public bool UseNotify = true;

			[JsonProperty(PropertyName = LangRu ? "Включить работу с LangAPI?" : "Work with LangAPI?")]
			public bool UseLangAPI = true;

			[JsonProperty(PropertyName =
				LangRu ? "Могут ли админы редактировать предметы? (флаг)" : "Can admins edit? (by flag)")]
			public bool FlagAdmin = false;

			[JsonProperty(PropertyName = LangRu ? "Поддержка NoEscape" : "Block (NoEscape)")]
			public bool BlockNoEscape = false;

			[JsonProperty(PropertyName = LangRu ? "Включить блокировку после вайпа" : "Wipe Block")]
			public bool WipeCooldown = false;

			[JsonProperty(PropertyName = LangRu ? "Длительность блокировки после вайпа" : "Wipe Cooldown")]
			public float WipeCooldownTimer = 3600;

			[JsonProperty(PropertyName = LangRu ? "Включить блокировку после респавна" : "Respawn Block")]
			public bool RespawnCooldown = true;

			[JsonProperty(PropertyName = LangRu ? "Длительность блокировки после респавна" : "Respawn Cooldown")]
			public float RespawnCooldownTimer = 60;

			[JsonProperty(PropertyName = LangRu ? "Запрещать открытие на дуэлях?" : "Blocking the opening in duels?")]
			public bool UseDuels = false;

			[JsonProperty(PropertyName =
				LangRu ? "Задержка между загрузкой изображений" : "Delay between loading images")]
			public float ImagesDelay = 1f;

			[JsonProperty(PropertyName = LangRu ? "Экономика" : "Economy")]
			public EconomyEntry Economy = new()
			{
				Type = EconomyType.Plugin,
				AddHook = "Deposit",
				BalanceHook = "Balance",
				RemoveHook = "Withdraw",
				Plug = "Economics",
				ShortName = "scrap",
				DisplayName = string.Empty,
				Skin = 0,
				EconomyTitle = new EconomyTitle("Economics"),
				EconomyBalance = new EconomyTitle("${0}"),
				EconomyPrice = new EconomyTitle("${0}"),
				EconomyFooterPrice = new EconomyTitle("${0}"),
			};

			[JsonProperty(PropertyName = LangRu ? "Дополнительная экономика" : "Additional Economics",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<AdditionalEconomy> AdditionalEconomics = new()
			{
				new AdditionalEconomy()
				{
					ID = 1,
					Enabled = true,
					Type = EconomyType.Plugin,
					AddHook = "AddPoints",
					BalanceHook = "CheckPoints",
					RemoveHook = "TakePoints",
					Plug = "ServerRewards",
					ShortName = "scrap",
					DisplayName = string.Empty,
					Skin = 0,
					EconomyTitle = new EconomyTitle("Server Rewards"),
					EconomyBalance = new EconomyTitle("{0} RP"),
					EconomyPrice = new EconomyTitle("{0} RP"),
					EconomyFooterPrice = new EconomyTitle("{0} RP"),
				}
			};

			[JsonProperty(
				PropertyName = LangRu
					? "Магазины NPC (NPC ID - категории магазина)"
					: "NPC Shops (NPC ID - shop categories)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, NPCShop> NPCs = new()
			{
				["1234567"] = new NPCShop()
				{
					Permission = string.Empty,
					Categories = new List<string>
					{
						"Tool",
						"Food"
					}
				},
				["7654321"] = new NPCShop()
				{
					Permission = string.Empty,
					Categories = new List<string>
					{
						"Weapon",
						"Ammunition"
					}
				},
				["4644687478"] = new NPCShop()
				{
					Permission = "shop.usenpc",
					Categories = new List<string>
					{
						"*"
					}
				}
			};

			[JsonProperty(PropertyName = LangRu ? "Интерфейс" : "Interface")]
			public UserInterface UI = new UserInterface
			{
				DisplayType = "Overlay",
				RoundDigits = 5,
				Color1 = new IColor("#161617"),
				Color2 = new IColor("#4B68FF"),
				Color3 = new IColor("#0E0E10"),
				Color5 = new IColor("#FF4B4B"),
				Color7 = new IColor("#CD3838"),
				Color8 = new IColor("#FFFFFF"),
				Color9 = new IColor("#4B68FF", 33),
				Color10 = new IColor("#4B68FF", 50),
				Color11 = new IColor("#161617", 95),
				Color12 = new IColor("#161617", 80),
				Color13 = new IColor("#0E0E10", 98)
			};
			
			[JsonProperty(PropertyName = LangRu ? "Заблокированные скины для продажи" : "Blocked skins for sell",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, List<ulong>> BlockedSkins = new()
			{
				["short name"] = new List<ulong>()
				{
					52,
					25
				},

				["short name 2"] = new List<ulong>()
				{
					52,
					25
				}
			};

			[JsonProperty(PropertyName = LangRu ? "Auto-Wipe настройки" : "Auto-Wipe Settings")]
			public WipeSettings Wipe = new()
			{
				Cooldown = true,
				Players = true,
				Limits = true
			};

			[JsonProperty(
				PropertyName = LangRu
					? "Кастомные Торговые Автоматы (Entity ID - settings)"
					: "Custom Vending Machines (Entity ID - settings)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<ulong, CustomVendingEntry> CustomVending =
				new()
				{
					[123343941] = new CustomVendingEntry()
					{
						Permission = string.Empty,
						Categories = new List<string>
						{
							"Cars", "Misc"
						}
					}
				};

			[JsonProperty(PropertyName =
				LangRu
					? "Настройки контейнеров для продажи товаров"
					: "Settings available containers for selling item")]
			public SellContainers SellContainers = new()
			{
				Enabled = true,
				Containers = new List<string>
				{
					"main",
					"belt"
				}
			};

			[JsonProperty(PropertyName = LangRu ? "Настройки повторной покупки" : "Buy Again Settings")]
			public BuyAgainConf BuyAgain = new()
			{
				Enabled = false,
				Permission = string.Empty,
				Image = "assets/icons/history_servers.png"
			};

			[JsonProperty(PropertyName = LangRu ? "Настройки форматирования" : "Formatting Settings")]
			public FormattingConf Formatting = new()
			{
				BuyPriceFormat = "G",
				SellPriceFormat = "G",
				ShoppingBagCostFormat = "G",
				BalanceFormat = "G"
			};

			[JsonProperty(PropertyName = LangRu ? "Скидка" : "Discount")]
			public DiscountConf Discount = new()
			{
				Enabled = true,
				Discount = new Dictionary<string, int>
				{
					["shop.default"] = 0,
					["shop.vip"] = 10
				}
			};

			[JsonProperty(PropertyName = LangRu ? "Уведомления" : "Notifications")]
			public NotificationsConf Notifications = new()
			{
				GeneralNotifications = new Dictionary<string, NotificationsConf.BaseNotification>
				{
					[NoPermission] = NotificationsConf.BaseNotification.Create(1),
					[ErrorSyntax] = NotificationsConf.BaseNotification.Create(1),
					[VMNotFoundCategories] = NotificationsConf.BaseNotification.Create(1),
					[VMNotFound] = NotificationsConf.BaseNotification.Create(1),
					[VMExists] = NotificationsConf.BaseNotification.Create(1),
					[VMInstalled] = NotificationsConf.BaseNotification.Create(0),
					[NPCNotFound] = NotificationsConf.BaseNotification.Create(1),
					[NPCInstalled] = NotificationsConf.BaseNotification.Create(0),
					[BuyCooldownMessage] = NotificationsConf.BaseNotification.Create(1),
					[ReceivedItems] = NotificationsConf.BaseNotification.Create(0),
					[SellNotify] = NotificationsConf.BaseNotification.Create(0),
					[SuccessfulTransfer] = NotificationsConf.BaseNotification.Create(0),
					[MsgIsFavoriteItem] = NotificationsConf.BaseNotification.Create(1),
					[MsgAddedToFavoriteItem] = NotificationsConf.BaseNotification.Create(0),
					[MsgNoFavoriteItem] = NotificationsConf.BaseNotification.Create(1),
					[MsgRemovedFromFavoriteItem] = NotificationsConf.BaseNotification.Create(0),
					[BuyLimitReached] = NotificationsConf.BaseNotification.Create(1),
					[DailyBuyLimitReached] = NotificationsConf.BaseNotification.Create(1),
					[UIMsgShopInInitialization] = NotificationsConf.BaseNotification.Create(1),
					[NoILError] = NotificationsConf.BaseNotification.Create(1),
					[NoUseDuel] = NotificationsConf.BaseNotification.Create(1),
				}
			};
            
			public VersionNumber Version;

			#endregion

			#region Classes

			public class NotificationsConf
			{
				#region Fields

				[JsonProperty(PropertyName = LangRu? "Основные уведомления (сообщение – настройки оповещения)" : "General Notifications (message - settings notification)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public Dictionary<string, BaseNotification> GeneralNotifications = new();

				#endregion
				
				#region Public Methods

				public void ShowNotify(BasePlayer player, string key, int type, params object[] obj)
				{
					if (GeneralNotifications.TryGetValue(key, out var targetNotify))
					{
						targetNotify.Show(player, key, obj);
					}
					else
					{
						_instance?.SendNotify(player, key, type, obj);
					}
				}
				
				#endregion Public Method

				#region Classes
				
				public class BaseNotification
				{
					#region Fields

					[JsonProperty(PropertyName = LangRu ? "Тип" : "Type")]
					public int Type;

					[JsonProperty(PropertyName = LangRu ? "Показывать уведомление?" : "Show notify?")]
					public bool ShowNotify = true;
					
					#endregion

					#region Public Methods

					public void Show(BasePlayer player, string key, params object[] args)
					{
						if (ShowNotify)
							_instance.SendNotify(player, key, Type, args);
						else
							_instance.Reply(player, key, args);
					}

					#endregion

					#region Constructors

					public static BaseNotification Create(int type)
					{
						return new BaseNotification()
						{
							Type = type,
							ShowNotify = true
						};
					}

					#endregion
				}
				
				#endregion
			}

			public class DiscountConf
			{
				[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
				public bool Enabled;

				[JsonProperty(PropertyName = LangRu ? "Скидка (%)" : "Discount (%)",
					ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public Dictionary<string, int> Discount = new();

				public int GetDiscount(BasePlayer player)
				{
					if (!Enabled) return 0;

					var result = 0;
					Discount.Where(check => player.HasPermission(check.Key)).ForEach(
						check =>
						{
							if (check.Value > result)
								result = check.Value;
						});

					return result;
				}
			}

			public class FormattingConf
			{
				[JsonProperty(PropertyName = LangRu ? "Формат цены покупки" : "Buy Price Format")]
				public string BuyPriceFormat;

				[JsonProperty(PropertyName = LangRu ? "Формат цены продажи" : "Sell Price Format")]
				public string SellPriceFormat;

				[JsonProperty(PropertyName = LangRu ? "Формат стоимости в корзине" : "Shopping Bag Cost Format")]
				public string ShoppingBagCostFormat;

				[JsonProperty(PropertyName = LangRu ? "Формат баланса" : "Balance Format")]
				public string BalanceFormat;
			}

			public class BuyAgainConf
			{
				[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
				public bool Enabled;

				[JsonProperty(PropertyName =
					LangRu ? "Разрешение (пример: shopru.buyagain)" : "Permission (ex: shop.buyagain)")]
				public string Permission;

				[JsonProperty(PropertyName = LangRu ? "Изображение" : "Image")]
				public string Image;

				public bool HasAccess(BasePlayer player)
				{
					return Enabled && (string.IsNullOrEmpty(Permission) || player.HasPermission(Permission));
				}
			}

			public enum SortType
			{
				None,
				Name,
				Amount,
				PriceDecrease,
				PriceIncrease
			}

			#endregion
		}

		#region Classes

		public class Localization
		{
			#region Fields

			[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
			public bool Enabled;
			
			[JsonProperty(PropertyName = LangRu ? "Текст (язык - текст)" : "Text (language - text)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, string> Messages = new();

			#endregion

			#region Helpers

			public string GetMessage(BasePlayer player = null)
			{
				if (Messages.Count == 0)
					throw new Exception("The use of localization is enabled, but there are no messages!");

				var userLang = "en";
				if (player != null) userLang = _instance.lang.GetLanguage(player.UserIDString);

				if (Messages.TryGetValue(userLang, out var message))
					return message;

				return Messages.TryGetValue("en", out message) ? message : Messages.ElementAt(0).Value;
			}

			#endregion
		}

		public class SellContainers
		{
			#region Fields

			[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
			public bool Enabled;

			[JsonProperty(
				PropertyName = LangRu
					? "Доступные Контейнеры (main, belt, wear)"
					: "Available Containers (main, belt, wear)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> Containers = new();

			#endregion

			#region Helpers

			public List<ItemContainer> GetContainers(BasePlayer player)
			{
				if (player == null || player.inventory == null)
					return new List<ItemContainer>();

				var list = new List<ItemContainer>();

				Containers.ForEach(cont =>
				{
					switch (cont)
					{
						case "main":
						{
							list.Add(player.inventory.containerMain);
							break;
						}
						case "belt":
						{
							list.Add(player.inventory.containerBelt);
							break;
						}
						case "wear":
						{
							list.Add(player.inventory.containerWear);
							break;
						}
					}
				});

				return list;
			}

			public Item[] AllItems(BasePlayer player)
			{
				return GetContainers(player)
					.SelectMany(cont => cont.itemList)
					.ToArray();
			}

			#endregion
		}

		public class SelectCurrencyUI
		{
			#region Fields
			[JsonProperty(PropertyName = "Background")]
			public ImageSettings Background;

			[JsonProperty(PropertyName = "Title")]
			public TextSettings Title;

			[JsonProperty(PropertyName = "Economy Title")]
			public TextSettings EconomyTitle;

			[JsonProperty(PropertyName = "Economy Panel Material")]
			public string EconomyPanelMaterial = string.Empty;

			[JsonProperty(PropertyName = "Economy Panel Sprite")]
			public string EconomyPanelSprite = string.Empty;

			[JsonProperty(PropertyName = "Selected Economy Color")]
			public IColor SelectedEconomyColor = IColor.CreateWhite();

			[JsonProperty(PropertyName = "Unselected Economy Color")]
			public IColor UnselectedEconomyColor = IColor.CreateWhite();

			[JsonProperty(PropertyName = "Economy Width")]
			public float EconomyWidth;

			[JsonProperty(PropertyName = "Economy Height")]
			public float EconomyHeight;

			[JsonProperty(PropertyName = "Economy Margin")]
			public float EconomyMargin;

			[JsonProperty(PropertyName = "Economy Indent")]
			public float EconomyIndent;

			[JsonProperty(PropertyName = "Frame Width")]
			public float FrameWidth;

			[JsonProperty(PropertyName = "Frame Indent")]
			public float FrameIndent;

			[JsonProperty(PropertyName = "Frame Header")]
			public float FrameHeader;

			[JsonProperty(PropertyName = "Close the menu after a currency change?")]
			public bool CloseAfterChange;
			#endregion
		}

		#region UI Settings
		
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

					if (HandleColor != null) cuiScrollbar.HandleColor = HandleColor.Get;
					if (HighlightColor != null) cuiScrollbar.HighlightColor = HighlightColor.Get;
					if (PressedColor != null) cuiScrollbar.PressedColor = PressedColor.Get;
					if (TrackColor != null) cuiScrollbar.TrackColor = TrackColor.Get;
					
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
						Color = Color.Get
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
						Color = Color.Get,
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
			
			public List<CuiElement> GetButton(
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

				list.Add(new CuiElement
				{
					Name = name,
					Parent = parent,
					DestroyUi = destroyUI,
					Components =
					{
						btn,
						GetRectTransform()
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
								? new CuiImageComponent {Color = ImageColor.Get, Sprite = Image}
								: new CuiRawImageComponent {Color = ImageColor.Get, Png = _instance.GetImage(Image)}),
								
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
	                Color = Color.Get
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

		public class IColor
		{
			#region Fields
			[JsonProperty(PropertyName = "HEX", NullValueHandling = NullValueHandling.Include)]
			public string Hex;

			[JsonProperty(PropertyName = "Opacity (0 - 100)", NullValueHandling = NullValueHandling.Include)]
			public float Alpha;
			#endregion
			
			#region Cache

			[JsonIgnore] private string _color;

			[JsonIgnore]
			public string Get
			{
				get
				{
					if (string.IsNullOrEmpty(_color))
						_color = GetColor();

					return _color;
				}
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

			#endregion
			
			#region Constructors

			public IColor()
			{
			}

			public IColor(string hex, float alpha = 100)
			{
				Hex = hex;
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

		private class CustomVendingEntry
		{
			#region Fields

			[JsonProperty(PropertyName = LangRu ? "Разрешение" : "Permissions")]
			public string Permission;

			[JsonProperty(PropertyName = LangRu ? "Категории (Названия) [* - все]" : "Categories (Titles) [* - all]",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> Categories;

			#endregion
		}

		private class WipeSettings
		{
			#region Fields

			[JsonProperty(PropertyName = LangRu ? "Сброс задержек?" : "Wipe Cooldowns?")]
			public bool Cooldown;

			[JsonProperty(PropertyName = LangRu ? "Сброс игроков?" : "Wipe Players?")]
			public bool Players;

			[JsonProperty(PropertyName = LangRu ? "Сброс лимитов" : "Wipe Limits?")]
			public bool Limits;

			#endregion
		}

		private class UserInterface
		{
			#region Fields

			[JsonProperty(PropertyName = LangRu ? "Тип отображения (Overlay/Hud)" : "Display type (Overlay/Hud)")]
			public string DisplayType;

			[JsonProperty(PropertyName =
				LangRu
					? "Количество цифр после десятичной точки для округления цен"
					: "Number of digits after decimal point for rounding prices")]
			public int RoundDigits;

			[JsonProperty(PropertyName = LangRu ? "Цвет 1" : "Color 1")]
			public IColor Color1;

			[JsonProperty(PropertyName = LangRu ? "Цвет 2" : "Color 2")]
			public IColor Color2;

			[JsonProperty(PropertyName = LangRu ? "Цвет 3" : "Color 3")]
			public IColor Color3;

			[JsonProperty(PropertyName = LangRu ? "Цвет 5" : "Color 5")]
			public IColor Color5;

			[JsonProperty(PropertyName = LangRu ? "Цвет 7" : "Color 7")]
			public IColor Color7;

			[JsonProperty(PropertyName = LangRu ? "Цвет 8" : "Color 8")]
			public IColor Color8;

			[JsonProperty(PropertyName = LangRu ? "Цвет 9" : "Color 9")]
			public IColor Color9;

			[JsonProperty(PropertyName = LangRu ? "Цвет 10" : "Color 10")]
			public IColor Color10;

			[JsonProperty(PropertyName = LangRu ? "Цвет 11" : "Color 11")]
			public IColor Color11;

			[JsonProperty(PropertyName = LangRu ? "Цвет 12" : "Color 12")]
			public IColor Color12;

			[JsonProperty(PropertyName = LangRu ? "Цвет 13" : "Color 13")]
			public IColor Color13;

			#endregion
		}

		public enum ScrollType
		{
			Horizontal,
			Vertical
		}

		#region Font
		
		private enum CuiElementFont
		{
			RobotoCondensedBold,
			RobotoCondensedRegular,
			DroidSansMono,
			PermanentMarker,
		}
		
		private static string GetFontByType(CuiElementFont fontType)
		{
			switch (fontType)
			{
				case CuiElementFont.RobotoCondensedBold:
					return "robotocondensed-bold.ttf";
				case CuiElementFont.RobotoCondensedRegular:
					return "robotocondensed-regular.ttf";
				case CuiElementFont.DroidSansMono:
					return "droidsansmono.ttf";
				case CuiElementFont.PermanentMarker:
					return "permanentmarker.ttf";
				default:
					throw new ArgumentOutOfRangeException(nameof(fontType), fontType, null);
			}
		}
		
		private static CuiElementFont GetFontTypeByFont(string font)
		{
			switch (font)
			{
				case "robotocondensed-bold.ttf":
					return CuiElementFont.RobotoCondensedBold;
				case "robotocondensed-regular.ttf":
					return CuiElementFont.RobotoCondensedRegular;
				case "droidsansmono.ttf":
					return CuiElementFont.DroidSansMono;
				case "permanentmarker.ttf":
					return CuiElementFont.PermanentMarker;
				default:
					throw new ArgumentOutOfRangeException(nameof(font), font, null);
			}
		}
		
		#endregion

		private class NPCShop
		{
			#region Fields

			[JsonProperty(PropertyName = LangRu ? "Разрешение" : "Permission")]
			public string Permission;

			[JsonProperty(PropertyName = LangRu ? "Категории [* - все]" : "Categories (Titles) [* - all]",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> Categories;

			#endregion

			#region Cache

			[JsonIgnore] public string BotID;

			#endregion
		}

		private class ShopCategory : ICloneable, IDisposable
		{
			#region Fields

			[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = LangRu ? "Тип категории" : "Category Type")]
			[JsonConverter(typeof(StringEnumConverter))]
			public Type CategoryType = Type.None;

			[JsonProperty(PropertyName = LangRu ? "Название" : "Title")]
			public string Title;

			[JsonProperty(PropertyName = LangRu ? "Разрешение" : "Permission")]
			public string Permission;

			[JsonProperty(PropertyName = LangRu ? "Тип сортировки" : "Sort Type")]
			[JsonConverter(typeof(StringEnumConverter))]
			public Configuration.SortType SortType = Configuration.SortType.None;

			[JsonProperty(PropertyName = LangRu ? "Предметы" : "Items",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<ShopItem> Items = new();

			[JsonProperty(PropertyName = LangRu ? "Локализация" : "Localization")]
			public Localization Localization = new();

			#endregion

			#region Cache

			[JsonIgnore] private int _id = -1;

			[JsonIgnore]
			public int ID
			{
				get
				{
					if (_id == -1)
						_id = Random.Range(0, int.MaxValue);

					return _id;
				}
			}

			[JsonIgnore] private List<ShopItem> _sortedItems;

			[JsonIgnore]
			public List<ShopItem> GetItems
			{
				get
				{
					switch (SortType)
					{
						case Configuration.SortType.None:
							return Items;

						default:
							if (_sortedItems == null) SortItems();

							return _sortedItems;
					}
				}
			}

			#endregion

			#region Helpers

			#region Moving

			#region Categories

			public void MoveCategoryUp()
			{
				var index = _itemsData.Shop.IndexOf(this);
				if (index > 0 && index < _itemsData.Shop.Count)
				{			
					(_itemsData.Shop[index], _itemsData.Shop[index - 1]) = (_itemsData.Shop[index - 1], _itemsData.Shop[index]); // Swap
				}
			}
			
			public void MoveCategoryDown()
			{
				var index = _itemsData.Shop.IndexOf(this);
				if (index >= 0 && index < _itemsData.Shop.Count - 1)
				{
					(_itemsData.Shop[index], _itemsData.Shop[index + 1]) = (_itemsData.Shop[index + 1], _itemsData.Shop[index]);
				}
			}

			#endregion

			#region Items

			public void MoveItemRight(ShopItem item)
			{
				var index = Items.IndexOf(item);
				if (index >= 0 && index < Items.Count - 1)
				{							
					(Items[index], Items[index + 1]) = (Items[index + 1], Items[index]); // Swap
				}
			}

			public void MoveItemLeft(ShopItem item)
			{
				var index = Items.IndexOf(item);
				if (index > 0 && index < Items.Count)
				{
					(Items[index], Items[index - 1]) = (Items[index - 1], Items[index]); // Swap
				}
			}

			#endregion
			
			#endregion
			
			public List<ShopItem> GetShopItems(BasePlayer player)
			{
				var selectedEconomy = _instance.API_GetShopPlayerSelectedEconomy(player.userID);
				
				switch (CategoryType)
				{
					case Type.Favorite:
					{
						if (_instance?.GetPlayerCart(player.userID) is not PlayerCartData playerCart) return new List<ShopItem>();

						var items = playerCart.GetFavoriteItems();

						switch (SortType)
						{
							case Configuration.SortType.None:
								return items;

							default:
								return SortItems(items, false);
						}
					}

					default:
					{
						switch (SortType)
						{
							case Configuration.SortType.None:
								return Items.FindAll(item => player.HasPermission(item.Permission) && (!item.Currencies.Enabled ||
									item.CanBuy && item.Currencies.HasCurrency(true, selectedEconomy) ||
									item.CanSell &&
									item.Currencies.HasCurrency(false, selectedEconomy)));

							default:
								if (_sortedItems == null) SortItems();
								
								return _sortedItems?.FindAll(item => player.HasPermission(item.Permission) && (!item.Currencies.Enabled ||
									item.CanBuy && item.Currencies.HasCurrency(true, selectedEconomy) || item.CanSell &&
									item.Currencies.HasCurrency(false, selectedEconomy)));
						}
					}
				}
			}

			public string GetTitle(BasePlayer player)
			{
				if (Localization is {Enabled: true})
					return Localization.GetMessage(player);

				return Title;
			}

			public List<ShopItem> SortItems(List<ShopItem> items = null, bool saveToCache = true)
			{
				items ??= Items;

				var sortedItems = items.ToList();

				switch (SortType)
				{
					case Configuration.SortType.Name:
						sortedItems.Sort((x, y) =>
							string.Compare(x.PublicTitle, y.PublicTitle, StringComparison.Ordinal));
						break;
					case Configuration.SortType.Amount:
						sortedItems.Sort((x, y) => x.Amount.CompareTo(y.Amount));
						break;
					case Configuration.SortType.PriceIncrease:
						sortedItems.Sort((x, y) => x.Amount.CompareTo(y.Price));
						break;
					case Configuration.SortType.PriceDecrease:
						sortedItems.Sort((x, y) => y.Amount.CompareTo(x.Price));
						break;
				}

				if (saveToCache)
					_sortedItems = sortedItems;

				LoadIDs(true);

				return sortedItems;
			}

			public void LoadIDs(bool sort = false)
			{
				if (sort)
					Items.ForEach(item => _instance._shopItems.Remove(item.ID));

				GetItems.ForEach(item =>
				{
					var id = item.ID;
					if (_instance._shopItems.ContainsKey(item.ID))
						id = _instance.GetId();
					_instance._shopItems.Add(id, item);

					if (item.Discount != null)
						foreach (var check in item.Discount)
							if (!string.IsNullOrEmpty(check.Key) && !_instance.permission.PermissionExists(check.Key))
								_instance.permission.RegisterPermission(check.Key, _instance);

					if (item.BuyCooldowns != null)
						foreach (var check in item.BuyCooldowns)
							if (!string.IsNullOrEmpty(check.Key) && !_instance.permission.PermissionExists(check.Key))
								_instance.permission.RegisterPermission(check.Key, _instance);

					if (item.SellCooldowns != null)
						foreach (var check in item.SellCooldowns)
							if (!string.IsNullOrEmpty(check.Key) && !_instance.permission.PermissionExists(check.Key))
								_instance.permission.RegisterPermission(check.Key, _instance);

					if (item.Genes is {Enabled: true})
						if (!item.Genes.TryInit()) 
							_instance?.PrintError($"Can't load the item with the ID {item.ID}. The number of genes in the item is incorrect ({item.Genes.GeneTypes.Count} genes, but it should be 6).");
				});
			}

			public object Clone()
			{
				return MemberwiseClone();
			}

			public void Dispose()
			{
				//null
			}

			#endregion

			#region Classes

			public enum Type
			{
				None,
				Favorite,
				Hided
			}

			#endregion
		}

		public enum ItemType
		{
			Item,
			Command,
			Plugin,
			Kit
		}

		public class ShopItem
		{
			#region Fields

			[JsonProperty(PropertyName = LangRu ? "Тип" : "Type")] [JsonConverter(typeof(StringEnumConverter))]
			public ItemType Type;

			[JsonProperty(PropertyName = "ID")] public int ID;

			[JsonProperty(PropertyName = LangRu ? "Изображение" : "Image")]
			public string Image;

			[JsonProperty(PropertyName = LangRu ? "Разрешение" : "Permission")]
			public string Permission = string.Empty;

			[JsonProperty(PropertyName = LangRu ? "Название" : "Title")]
			public string Title;

			[JsonProperty(PropertyName = LangRu ? "Описание" : "Description")]
			public string Description = string.Empty;

			[JsonProperty(PropertyName = LangRu ? "Команда (%steamid%)" : "Command (%steamid%)")]
			public string Command;

			[JsonProperty(PropertyName = "Kit")] public string Kit = string.Empty;

			[JsonProperty(PropertyName = LangRu ? "Плагин" : "Plugin")]
			public PluginItem Plugin;

			[JsonProperty(PropertyName =
				LangRu ? "DisplayName (пусто - по умолчанию)" : "DisplayName (empty - default)")]
			public string DisplayName;

			[JsonProperty(PropertyName = "ShortName")]
			public string ShortName;

			[JsonProperty(PropertyName = LangRu ? "Скин" : "Skin")]
			public ulong Skin;

			[JsonProperty(PropertyName = LangRu ? "Это чертёж" : "Is Blueprint")]
			public bool Blueprint;

			[JsonProperty(PropertyName = LangRu ? "Количество" : "Amount")]
			public int Amount;

			[JsonProperty(PropertyName = LangRu ? "Включить возможность покупать предмет?" : "Enable item buying?")]
			public bool CanBuy = true;

			[JsonProperty(PropertyName = LangRu ? "Цена" : "Price")]
			public double Price;

			[JsonProperty(PropertyName = LangRu ? "Включить возможность продавать предмет?" : "Enable item selling?")]
			public bool CanSell = true;

			[JsonProperty(PropertyName = LangRu ? "Цена продажи" : "Sell Price")]
			public double SellPrice;

			[JsonProperty(PropertyName = LangRu ? "Задержка покупки (0 - отключить)" : "Buy Cooldown (0 - disable)")]
			public float BuyCooldown;

			[JsonProperty(PropertyName = LangRu ? "Задержки покупки (0 - отключить)" : "Buy Cooldowns (0 - no limit)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, float> BuyCooldowns = new();

			[JsonProperty(PropertyName = LangRu ? "Задержка продажи (0 - отключить)" : "Sell Cooldown (0 - disable)")]
			public float SellCooldown;

			[JsonProperty(PropertyName = LangRu ? "Задержки продажи (0 - no limit)" : "Sell Cooldowns (0 - no limit)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, float> SellCooldowns = new();

			[JsonProperty(PropertyName = LangRu ? "Использовать пользовательскую скидку?" : "Use custom discount?")]
			public bool UseCustomDiscount;

			[JsonProperty(PropertyName = LangRu ? "Скидка (%)" : "Discount (%)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, int> Discount = new();

			[JsonProperty(PropertyName = LangRu ? "Лимит продаж (0 - без лимита)" : "Sell Limits (0 - no limit)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, int> SellLimits = new();

			[JsonProperty(PropertyName = LangRu ? "Лимит покупок (0 - без лимита)" : "Buy Limits (0 - no limit)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, int> BuyLimits = new();

			[JsonProperty(
				PropertyName = LangRu ? "Дневной лимит покупок (0 - без лимита)" : "Daily Buy Limits (0 - no limit)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, int> DailyBuyLimits = new();

			[JsonProperty(
				PropertyName = LangRu ? "Дневной лимит продаж (0 - без лимита)" : "Daily Sell Limits (0 - no limit)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, int> DailySellLimits = new();

			[JsonProperty(PropertyName =
				LangRu ? "Максимальное покупаемое количество (0 - отключить)" : "Max Buy Amount (0 - disable)")]
			public int BuyMaxAmount;

			[JsonProperty(PropertyName =
				LangRu ? "Максимальное продаваемое количество (0 - отключить)" : "Max Sell Amount (0 - disable)")]
			public int SellMaxAmount;

			[JsonProperty(PropertyName = LangRu ? "Быстрая Покупка" : "Force Buy")]
			public bool ForceBuy;

			[JsonProperty(PropertyName =
				LangRu ? "Запрещать разделение предмета на стаки?" : "Prohibit splitting item into stacks?")]
			public bool ProhibitSplit;

			[JsonProperty(PropertyName = LangRu ? "Локализация" : "Localization")]
			public Localization Localization = new();

			[JsonProperty(PropertyName = LangRu ? "Содержимое" : "Content")]
			public ItemContent Content = new();

			[JsonProperty(PropertyName = LangRu ? "Оружие" : "Weapon")]
			public ItemWeapon Weapon = new();

			[JsonProperty(PropertyName = LangRu ? "Гены" : "Genes")]
			public ItemGenes Genes = new();

			[JsonProperty(PropertyName = LangRu ? "Валюты" : "Currencies")]
			public ItemCurrency Currencies = new();
			
			#endregion

			#region Cache

			[JsonIgnore] private int _itemId = -1;

			[JsonIgnore]
			public int itemId
			{
				get
				{
					if (_itemId == -1)
						_itemId = ItemManager.FindItemDefinition(ShortName)?.itemid ?? -1;

					return _itemId;
				}
			}

			[JsonIgnore] private string _publicTitle;

			[JsonIgnore]
			public string PublicTitle
			{
				get
				{
					if (string.IsNullOrEmpty(_publicTitle))
						_publicTitle = GetName();

					return _publicTitle;
				}
			}

			[JsonIgnore] private ICuiComponent _image;

			public CuiElement GetImage(InterfacePosition position, string parent,
				string name = null)
			{
				var targetImage = _image == null && !_instance._enabledImageLibrary || string.IsNullOrEmpty(Image)
					? (_image = new CuiImageComponent
					{
						ItemId = itemId,
						SkinId = Skin
					})
					: new CuiRawImageComponent
					{
						Png = _instance.GetImage(Image)
					};
				
				return new CuiElement
				{
					Name = string.IsNullOrEmpty(name) ? CuiHelper.GetGuid() : name,
					Parent = parent,
					Components =
					{
						targetImage,
						position.GetRectTransform()
					}
				};
			}

			public CuiElement GetImage(string aMin, string aMax, string oMin, string oMax, string parent,
				string name = null)
			{
				var targetImage = _image == null && !_instance._enabledImageLibrary || string.IsNullOrEmpty(Image)
					? (_image = new CuiImageComponent
					{
						ItemId = itemId,
						SkinId = Skin
					})
					: new CuiRawImageComponent
					{
						Png = _instance.GetImage(Image)
					};

				return new CuiElement
				{
					Name = string.IsNullOrEmpty(name) ? CuiHelper.GetGuid() : name,
					Parent = parent,
					Components =
					{
						targetImage,
						new CuiRectTransformComponent
						{
							AnchorMin = aMin, AnchorMax = aMax,
							OffsetMin = oMin, OffsetMax = oMax
						}
					}
				};
			}

			public string GetPublicTitle(BasePlayer player)
			{
				if (Localization is {Enabled: true})
				{
					var msg = Localization.GetMessage(player);
					if (!string.IsNullOrEmpty(msg))
						return msg;
				}

				return GetName(player.UserIDString);
			}

			public string GetName(string userId = null)
			{
				if (!string.IsNullOrEmpty(Title))
					return Title;

				if (!string.IsNullOrEmpty(DisplayName))
					return DisplayName;

				if (!string.IsNullOrEmpty(ShortName))
				{
					var def = ItemManager.FindItemDefinition(ShortName);
					if (def != null)
					{
						var displayName = def.displayName.translated;
						
						if (_config.UseLangAPI)
							displayName = _instance?.GetItemDisplayNameFromLangAPI(ShortName, displayName, userId);
						
						return displayName;
					}
				}
				
				return string.Empty;
			}

			[JsonIgnore] private ItemDefinition _itemDefinition;

			[JsonIgnore]
			public ItemDefinition ItemDefinition
			{
				get
				{
					if (_itemDefinition == null && !string.IsNullOrEmpty(ShortName))
						_itemDefinition = ItemManager.FindItemDefinition(ShortName);

					return _itemDefinition;
				}
			}

			#endregion

			#region Helpers

			#region Favorite

			public bool IsFavorite(ulong playerID)
			{
				var playerCart = _instance?.GetPlayerCart(playerID) as PlayerCartData;
				return playerCart != null && playerCart.IsFavoriteItem(ID);
			}

			public bool CanBeFavorite(ulong playerID)
			{
				if (_instance?.TryGetNPCShop(playerID, out _) == true)
					return false;

				if (_instance?.TryGetCustomVending(playerID, out _) == true)
					return false;

				return true;
			}

			#endregion

			#region Same

			public bool CanTake(Item item)
			{
				if (item.info.shortname == ShortName && !item.isBroken && (Skin == 0 || item.skin == Skin))
					return !Genes.Enabled || Genes.IsSameGenes(item);

				return false;
			}

			#endregion

			public bool CanBeSold()
			{
				return CanSell && SellPrice >= 0.0;
			}			
			
			public bool CanBePurchased()
			{
				return CanBuy && Price >= 0.0;
			}			
			
			public float GetCooldown(string player, bool buy = true)
			{
				var result = buy ? BuyCooldown : SellCooldown;

				var dict = buy ? BuyCooldowns : SellCooldowns;

				dict.Where(check => player.HasPermission(check.Key)).ForEach(
					check =>
					{
						if (check.Value < result)
							result = check.Value;
					});

				return result;
			}

			public Dictionary<string, object> ToDictionary()
			{
				return new Dictionary<string, object>
				{
					["Generated"] = false,
					["ID"] = ID,
					["Type"] = Type,
					["Image"] = Image,
					["Title"] = Title,
					["Command"] = Command,
					["DisplayName"] = DisplayName,
					["ShortName"] = ShortName,
					["Skin"] = Skin,
					["Blueprint"] = Blueprint,
					["Buying"] = CanBuy,
					["Selling"] = CanSell,
					["Amount"] = Amount,
					["Price"] = Price,
					["SellPrice"] = SellPrice,
					["Plugin_Hook"] = Plugin.Hook,
					["Plugin_Name"] = Plugin.Plugin,
					["Plugin_Amount"] = Plugin.Amount
					// ["Content_Enabled"] = Content?.Enabled ?? false,
					// ["Weapon_Enabled"] = Weapon?.Enabled ?? false,
					// ["Weapon_AmmoType"] = Weapon?.AmmoType ?? string.Empty,
					// ["Weapon_AmmoAmount"] = Weapon?.AmmoAmount ?? 0
				};
			}

			public double GetPrice(BasePlayer player, int selectedEconomy)
			{
				var discount = GetDiscount(player);

				var priceValue = Price;
				if (Currencies is {Enabled: true} && Currencies.TryGetCurrency(true, selectedEconomy, out var currency))
				{
					priceValue = currency.Price;
				}

				return Math.Round(discount != 0 ? priceValue * (1f - discount / 100f) : priceValue, _config.UI.RoundDigits);
			}

			public double GetSellPrice(BasePlayer player, int selectedEconomy = 0)
			{
				var priceValue = SellPrice;
				
				if (Currencies is {Enabled: true} && Currencies.TryGetCurrency(false, selectedEconomy, out var currency))
				{
					priceValue = currency.Price;
				}

				return priceValue;
			}
			
			public int GetDiscount(BasePlayer player)
			{
				if (!UseCustomDiscount) return _config.Discount.GetDiscount(player);

				var result = 0;
				Discount.Where(check => player.HasPermission(check.Key)).ForEach(
					check =>
					{
						if (check.Value > result)
							result = check.Value;
					});

				return result;
			}

			public int GetLimit(BasePlayer player, bool buy = true, bool daily = false)
			{
				var dict = daily ? buy ? DailyBuyLimits : DailySellLimits
					: buy ? BuyLimits : SellLimits;

				if (dict.Count == 0)
					return 0;

				var result = 0;
				dict.Where(check => player.HasPermission(check.Key)).ForEach(
					check =>
					{
						if (check.Value > result)
							result = check.Value;
					});

				return result;
			}

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
						Plugin.Get(player, count);
						break;
					case ItemType.Kit:
						ToKit(player, count);
						break;
				}
			}

			private void ToKit(BasePlayer player, int count)
			{
				if (string.IsNullOrEmpty(Kit)) return;

				for (var i = 0; i < count; i++)
					Interface.Oxide.CallHook("GiveKit", player, Kit);
			}

			private void ToItem(BasePlayer player, int count)
			{
				if (ItemDefinition == null)
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

			private void GiveBlueprint(int count, BasePlayer player)
			{
				for (var i = 0; i < count; i++) GiveBlueprint(player);
			}

			private void GiveBlueprint(BasePlayer player)
			{
				var bp = ItemManager.CreateByName("blueprintbase");
				if (bp == null)
				{
					_instance?.PrintError("Error creating blueprintbase");
					return;
				}

				bp.blueprintTarget = ItemManager.FindItemDefinition(ShortName).itemid;

				if (!string.IsNullOrEmpty(DisplayName)) bp.name = DisplayName;

				player.GiveItem(bp, BaseEntity.GiveItemReason.PickedUp);
			}

			private void GiveItem(int amount, BasePlayer player)
			{
				var newItem = ItemManager.Create(ItemDefinition, amount, Skin);
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

				if (Genes is {Enabled: true}) 
					Genes.Build(newItem);

				player.GiveItem(newItem, BaseEntity.GiveItemReason.PickedUp);
			}

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

			public List<int> GetStacks(int amount)
			{
				amount *= Amount;

				var maxStack = ItemDefinition.stackable;

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

			public override string ToString()
			{
				switch (Type)
				{
					case ItemType.Item:
						return $"[ITEM-{ID}] {ShortName}x{Amount}(DN: {DisplayName}, SKIN: {Skin})";
					case ItemType.Command:
						return $"[COMMAND-{ID}] {Command}";
					case ItemType.Plugin:
						return
							$"[PLUGIN-{ID}] Name: {Plugin?.Plugin}, Hook: {Plugin?.Hook}, Amount: {Plugin?.Amount ?? 0}";
					case ItemType.Kit:
						return $"[KIT-{ID}] {Kit}";
					default:
						return base.ToString();
				}
			}

			#endregion

			#region Constructor

			public ShopItem()
			{
			}

			public ShopItem(Dictionary<string, object> dictionary)
			{
				var price = (double) dictionary["Price"];
				var sellPrice = (double) dictionary["SellPrice"];

				ID = (int) dictionary["ID"];
				Type = (ItemType) dictionary["Type"];
				Image = (string) dictionary["Image"];
				Title = (string) dictionary["Title"];
				Command = (string) dictionary["Command"];
				DisplayName = (string) dictionary["DisplayName"];
				ShortName = (string) dictionary["ShortName"];
				Skin = (ulong) dictionary["Skin"];
				Blueprint = (bool) dictionary["Blueprint"];
				CanBuy = (bool) dictionary["Buying"];
				CanSell = (bool) dictionary["Selling"];
				Amount = (int) dictionary["Amount"];
				Price = price;
				SellPrice = sellPrice;
				Plugin = new PluginItem
				{
					Hook = (string) dictionary["Plugin_Hook"],
					Plugin = (string) dictionary["Plugin_Name"],
					Amount = (int) dictionary["Plugin_Amount"]
				};
				Discount = new Dictionary<string, int>
				{
					["shop.default"] = 0,
					["shop.vip"] = 10
				};
				SellLimits = new Dictionary<string, int>
				{
					["shop.default"] = 0,
					["shop.vip"] = 0
				};
				BuyLimits = new Dictionary<string, int>
				{
					["shop.default"] = 0,
					["shop.vip"] = 0
				};
				DailyBuyLimits = new Dictionary<string, int>
				{
					["shop.default"] = 0,
					["shop.vip"] = 0
				};
				DailySellLimits = new Dictionary<string, int>
				{
					["shop.default"] = 0,
					["shop.vip"] = 0
				};
				BuyCooldowns = new Dictionary<string, float>
				{
					["shop.default"] = 0f,
					["shop.vip"] = 0f
				};

				SellCooldowns = new Dictionary<string, float>
				{
					["shop.default"] = 0f,
					["shop.vip"] = 0f
				};

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
				};

				Weapon = new ItemWeapon
				{
					Enabled = false,
					AmmoType = string.Empty,
					AmmoAmount = 1
				};

				Genes = new ItemGenes
				{
					Enabled = false,
					GeneTypes = new List<char>
					{
						'X', 'Y', 'G', 'W', 'H', 'W'
					}
				};

				Currencies = new ItemCurrency()
				{
					Enabled = false,
					Currencies = new Dictionary<int, CurrencyInfo>
					{
						[0] = new() {Price = price},
						[1] = new() {Price = price},
					},
					SellCurrencies = new Dictionary<int, CurrencyInfo>
					{
						[0] = new() {Price = sellPrice},
						[1] = new() {Price = sellPrice},
					}
				};
			}

			public static ShopItem GetDefault(int id, double itemCost, string shortName)
			{
				return new ShopItem
				{
					Type = ItemType.Item,
					ID = id,
					Price = itemCost,
					SellPrice = itemCost,
					Image = string.Empty,
					Title = string.Empty,
					Command = string.Empty,
					Plugin = new PluginItem(),
					DisplayName = string.Empty,
					ShortName = shortName,
					Skin = 0,
					Blueprint = false,
					Amount = 1,
					Discount = new Dictionary<string, int>
					{
						["shop.default"] = 0,
						["shop.vip"] = 10
					},
					SellLimits = new Dictionary<string, int>
					{
						["shop.default"] = 0,
						["shop.vip"] = 0
					},
					BuyLimits = new Dictionary<string, int>
					{
						["shop.default"] = 0,
						["shop.vip"] = 0
					},
					DailyBuyLimits = new Dictionary<string, int>
					{
						["shop.default"] = 0,
						["shop.vip"] = 0
					},
					DailySellLimits = new Dictionary<string, int>
					{
						["shop.default"] = 0,
						["shop.vip"] = 0
					},
					BuyMaxAmount = 0,
					SellMaxAmount = 0,
					ForceBuy = false,
					ProhibitSplit = false,
					Localization = new Localization
					{
						Enabled = false,
						Messages = new Dictionary<string, string>
						{
							["en"] = string.Empty,
							["fr"] = string.Empty
						}
					},
					BuyCooldowns = new Dictionary<string, float>
					{
						["shop.default"] = 0f,
						["shop.vip"] = 0f
					},
					SellCooldowns = new Dictionary<string, float>
					{
						["shop.default"] = 0f,
						["shop.vip"] = 0f
					},
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
					Genes = new ItemGenes
					{
						Enabled = false,
						GeneTypes = new List<char>
						{
							'X', 'Y', 'G', 'W', 'H', 'W'
						}
					},
					Currencies = new ItemCurrency
					{
						Enabled = false,
						Currencies = new Dictionary<int, CurrencyInfo>
						{
							[0] = new(){Price = itemCost},
							[1] = new(){Price = itemCost},
						},
						SellCurrencies = new Dictionary<int, CurrencyInfo>
						{
							[0] = new(){Price = itemCost},
							[1] = new(){Price = itemCost},
						}
					}
				};
			}

			#endregion
			
			#region Classes

			public class ItemCurrency
			{
				#region Fields

				[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
				public bool Enabled;
				
				[JsonProperty(PropertyName = LangRu? "Валюты для покупки предметов (ключ – ID экономики, при использовании экономики по умолчанию используйте 0)" : "Enabled currency for buying items (key - economy ID, if you use economy by default use 0)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public Dictionary<int, CurrencyInfo> Currencies = new();
				
				[JsonProperty(PropertyName = LangRu? "Валюты для продажи предметов (ключ – ID экономики, при использовании экономики по умолчанию используйте 0)" : "Currency for selling items (key - economy ID, if you use economy by default use 0)", ObjectCreationHandling = ObjectCreationHandling.Replace)] 
				public Dictionary<int, CurrencyInfo> SellCurrencies = new();

				#endregion

				#region Public Methods

				#region Buy or Sell
				
				public bool TryGetCurrency(bool buy, int playerCurrency, out CurrencyInfo currency)
				{
					var currencies = buy? Currencies : SellCurrencies; 
					return currencies.TryGetValue(playerCurrency, out currency);
				}

				public bool HasCurrency(bool buy, int playerCurrency)
				{
					var currencies = buy? Currencies : SellCurrencies;
					return currencies.ContainsKey(playerCurrency);
				}

				#endregion

				#region Global
				
				public bool HasCurrency(int playerCurrency)
				{
					return Currencies.ContainsKey(playerCurrency) || SellCurrencies.ContainsKey(playerCurrency);
				}

				#endregion
				
				#endregion
			}

			public class CurrencyInfo
			{
				[JsonProperty(PropertyName = LangRu? "Цена" : "Price")]
				public double Price;
			}
		
            
			#endregion
		}

		public class ItemGenes
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

		public class ItemContent
		{
			#region Fields

			[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = LangRu ? "Содержимое" : "Contents",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<ContentInfo> Contents = new();

			#endregion

			#region Helpers

			public void Build(Item item)
			{
				Contents?.ForEach(content => content?.Build(item));
			}

			#endregion

			#region Classes

			public class ContentInfo
			{
				[JsonProperty(PropertyName = "ShortName")]
				public string ShortName;

				[JsonProperty(PropertyName = LangRu ? "Состояние" : "Condition")]
				public float Condition;

				[JsonProperty(PropertyName = LangRu ? "Количество" : "Amount")]
				public int Amount;

				[JsonProperty(PropertyName = LangRu ? "Позиция" : "Position")]
				public int Position = -1;

				#region Helpers

				public void Build(Item item)
				{
					var content = ItemManager.CreateByName(ShortName, Mathf.Max(Amount, 1));
					if (content == null) return;
					content.condition = Condition;
					content.MoveToContainer(item.contents, Position);
				}

				#endregion
			}

			#endregion
		}

		public class ItemWeapon
		{
			#region Fields

			[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = LangRu ? "Тип боеприпасов" : "Ammo Type")]
			public string AmmoType;

			[JsonProperty(PropertyName = LangRu ? "Количество боеприпасов" : "Ammo Amount")]
			public int AmmoAmount;

			#endregion

			#region Helpers

			public void Build(Item item)
			{
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
		}

		public class PluginItem
		{
			[JsonProperty(PropertyName = "Hook")] public string Hook = string.Empty;

			[JsonProperty(PropertyName = "Plugin Name")]
			public string Plugin = string.Empty;

			[JsonProperty(PropertyName = "Amount")]
			public int Amount;

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
						plug.Call(Hook, player.userID.Get(), (double) Amount * count);
						break;
					}
					default:
					{
						plug.Call(Hook, player.userID.Get(), Amount * count);
						break;
					}
				}
			}
		}

		private class AdditionalEconomy : EconomyEntry
		{
			[JsonProperty(PropertyName = "ID")] public int ID;

			[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
			public bool Enabled;

			public bool IsSame(EconomyEntry configEconomy)
			{
				return Type == configEconomy.Type &&
				       Plug == configEconomy.Plug &&
				       ShortName == configEconomy.ShortName &&
				       Skin == configEconomy.Skin;
			}

			public AdditionalEconomy(EconomyEntry configEconomy)
			{
				Type = configEconomy.Type;
				Plug = configEconomy.Plug;
				AddHook = configEconomy.AddHook;
				RemoveHook = configEconomy.RemoveHook;
				BalanceHook = configEconomy.BalanceHook;
				ShortName = configEconomy.ShortName;
				DisplayName = configEconomy.DisplayName;
				Skin = configEconomy.Skin;
				EconomyTitle = configEconomy.EconomyTitle;
				EconomyBalance = configEconomy.EconomyBalance;
				EconomyPrice = configEconomy.EconomyPrice;
				EconomyFooterPrice = configEconomy.EconomyFooterPrice;
				ID = 0;
				Enabled = true;
			}

			[JsonConstructor]
			public AdditionalEconomy()
			{
			}
		}

		private enum EconomyType
		{
			Plugin,
			Item
		}

		private class EconomyEntry
		{
			#region Fields

			[JsonProperty(PropertyName = LangRu ? "Тип (Plugin/Item)" : "Type (Plugin/Item)")]
			[JsonConverter(typeof(StringEnumConverter))]
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

			[JsonProperty(PropertyName = "Skin")] 
			public ulong Skin;

			[JsonProperty(PropertyName = LangRu ? "Название" : "Title")]
			public EconomyTitle EconomyTitle;
			
			[JsonProperty(PropertyName = LangRu ? "Баланс" : "Balance")]
			public EconomyTitle EconomyBalance;
			
			[JsonProperty(PropertyName = LangRu ? "Стоимость" : "Price")]
			public EconomyTitle EconomyPrice;
			
			[JsonProperty(PropertyName = LangRu ? "Стоимость предметов в футере" : "Footer Items Price")]
			public EconomyTitle EconomyFooterPrice;
			
			/*
			[JsonProperty(PropertyName = LangRu ? "Lang Ключ (для названия)" : "Lang Key (for Title)")]
			public string TitleLangKey;

			[JsonProperty(PropertyName = LangRu ? "Lang Ключ (для баланса)" : "Lang Key (for Balance)")]
			public string BalanceLangKey;

			[JsonProperty(PropertyName = LangRu ? "Lang Ключ (для стоимости)" : "Lang Key (for Price)")]
			public string PriceLangKey;

			[JsonProperty(PropertyName = LangRu ? "Lang Ключ (для стоимости предметов в корзине)" : "Lang Key (for Footer Items Price)")]
			public string FooterPriceLangKey;
			*/

			#endregion Fields

			#region Public Methods
			public string GetTitle(BasePlayer player)
			{
				return EconomyTitle?.Get(player) ?? $"Error: Title not found for player {player.UserIDString}.";
			}

			public string GetBalanceTitle(BasePlayer player)
			{
				return EconomyBalance?.Get(player, ShowBalance(player).ToString(_config.Formatting.BalanceFormat)) 
				       ?? $"Error: Balance title not found for player {player.UserIDString}.";
			}

			public string GetPriceTitle(BasePlayer player, string formattedPrice)
			{
				return EconomyPrice?.Get(player, formattedPrice) 
				       ?? $"Error: Price title not found for player {player.UserIDString} with price {formattedPrice}.";
			}

			public string GetFooterPriceTitle(BasePlayer player, string formattedPrice)
			{
				return EconomyFooterPrice?.Get(player, formattedPrice) 
				       ?? $"Error: Footer price title not found for player {player.UserIDString} with price {formattedPrice}.";
			}

			public string GetDebugInfo()
			{
				switch (Type)
				{
					case EconomyType.Plugin:
						return $"{_config.Economy.Plug} – {_config.Economy.Type}";
					case EconomyType.Item:
						return $"{_config.Economy.ShortName} ({_config.Economy.Skin}) – {_config.Economy.Type}";
					default:
						throw new ArgumentOutOfRangeException();
				}
			}			
			#endregion Public Methods

			#region Economy Methods

			public double ShowBalance(BasePlayer player)
			{
				switch (Type)
				{
					case EconomyType.Plugin:
					{
						var plugin = _instance?.plugins?.Find(Plug);
						if (plugin == null) return 0;

						return Convert.ToDouble(plugin.Call(BalanceHook, player.userID.Get()));
					}
					case EconomyType.Item:
					{
						return ItemCount(player.inventory.AllItems(), ShortName, Skin);
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
								plugin.Call(AddHook, player.userID.Get(), (int) amount);
								break;
							default:
								plugin.Call(AddHook, player.userID.Get(), amount);
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
								plugin.Call(RemoveHook, player.userID.Get(), (int) amount);
								break;
							default:
								plugin.Call(RemoveHook, player.userID.Get(), amount);
								break;
						}

						return true;
					}
					case EconomyType.Item:
					{
						var playerItems = player.inventory.AllItems();
						var am = (int) amount;

						if (ItemCount(playerItems, ShortName, Skin) < am) return false;

						Take(playerItems, ShortName, Skin, am);
						return true;
					}
					default:
						return false;
				}
			}

			public bool Transfer(BasePlayer player, BasePlayer targetPlayer, double amount)
			{
				if (!RemoveBalance(player, amount))
					return false;

				AddBalance(targetPlayer, amount);
				return true;
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
		}

		private class EconomyTitle
		{
			#region Fields

			[JsonProperty(PropertyName = LangRu ? "Сообщение" : "Message")]
			public string Message = string.Empty;

			[JsonProperty(PropertyName = LangRu ? "Использовать локализованные сообщения?" : "Use localized messages?")]
			public bool UseLocalizedMessages = false;

			[JsonProperty(PropertyName = LangRu ? "Локализованные сообщения" : "Localized messages",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, string> LocalizedMessages = new();

			#endregion

			#region Public Methods

			public string Get(BasePlayer player, params object[] args)
			{
				return string.Format(GetMessage(player), args);
			}
		
			public string GetMessage(BasePlayer player)
			{
				if (UseLocalizedMessages && player != null)
				{
					var language = _instance?.lang?.GetLanguage(player.UserIDString);
					if (!string.IsNullOrWhiteSpace(language) && LocalizedMessages.TryGetValue(language, out var message))
						return message;
				}

				return Message;
			}

			#endregion

			#region Constructors

			public EconomyTitle() {}
			
			public EconomyTitle(string message)
			{
				Message = message;
				UseLocalizedMessages = false;
				LocalizedMessages = new Dictionary<string, string>
				{
					["en"] = message,
					["fr"] = message,
				};
			}
			
			public EconomyTitle(string message, Dictionary<string, string> localizedMessages)
			{
				Message = message;
				UseLocalizedMessages = localizedMessages is {Count: > 0};
				LocalizedMessages = localizedMessages ?? new Dictionary<string, string>();
			}
			
			public EconomyTitle(Dictionary<string, string> localizedMessages)
			{
				if (localizedMessages is {Count: > 0})
				{
					if (localizedMessages.TryGetValue("en", out var defaultMsg))
					{
						Message = defaultMsg;
						localizedMessages.Remove("en");
					}
					else
					{
						Message = string.Empty;
					}

					UseLocalizedMessages = localizedMessages is {Count: > 0};
					LocalizedMessages = localizedMessages ?? new Dictionary<string, string>();
				}
				else
				{
					Message = string.Empty;
					UseLocalizedMessages = false;
					LocalizedMessages = new Dictionary<string, string>
					{
						["en"] = string.Empty,
						["fr"] = string.Empty,
					};
				}
			}
			
			public EconomyTitle(string en, string ru, string znCN)
			{
				Message = en;
				UseLocalizedMessages = true;
				LocalizedMessages = new Dictionary<string, string>
				{
					["ru"] = ru,
					["zh-CN"] = znCN
				};
			}
			
			#endregion
		}
        
		#endregion

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
			var baseConfig = new Configuration();

			if (_config.Version != default)
			{
				PrintWarning("Config update detected! Updating config values...");

				if (_config.Version < new VersionNumber(1, 0, 21))
					_itemsData.Shop.ForEach(shop =>
					{
						shop.Items.ForEach(item =>
						{
							item.SellLimits = new Dictionary<string, int>
							{
								["shop.default"] = 0,
								["shop.vip"] = 0
							};
							item.BuyLimits = new Dictionary<string, int>
							{
								["shop.default"] = 0,
								["shop.vip"] = 0
							};
							item.DailyBuyLimits = new Dictionary<string, int>
							{
								["shop.default"] = 0,
								["shop.vip"] = 0
							};
							item.DailySellLimits = new Dictionary<string, int>
							{
								["shop.default"] = 0,
								["shop.vip"] = 0
							};
						});
					});

				if (_config.Version < new VersionNumber(1, 0, 24)) _config.UI.DisplayType = baseConfig.UI.DisplayType;

				if (_config.Version < new VersionNumber(1, 2, 17))
					_itemsData.Shop.ForEach(category =>
					{
						category.Localization = new Localization
						{
							Enabled = false,
							Messages = new Dictionary<string, string>
							{
								["en"] = category.Title
							}
						};

						category.Items.ForEach(item =>
						{
							item.Localization = new Localization
							{
								Enabled = false,
								Messages = new Dictionary<string, string>
								{
									["en"] = item.GetName()
								}
							};
						});
					});

				if (_config.Version < new VersionNumber(1, 2, 21))
					_itemsData.Shop.ForEach(category =>
					{
						category.Items.ForEach(item =>
						{
							item.BuyCooldowns = new Dictionary<string, float>
							{
								["shop.default"] = item.BuyCooldown,
								["shop.vip"] = item.BuyCooldown
							};

							item.SellCooldowns = new Dictionary<string, float>
							{
								["shop.default"] = item.SellCooldown,
								["shop.vip"] = item.SellCooldown
							};
						});
					});

				if (_config.Version < new VersionNumber(1, 2, 24))
					_itemsData.Shop.ForEach(category =>
					{
						category.Items.ForEach(item =>
						{
							item.Content = new ItemContent
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
							};

							item.Weapon = new ItemWeapon
							{
								Enabled = false,
								AmmoType = string.Empty,
								AmmoAmount = 1
							};
						});
					});

				if (_config.Version < new VersionNumber(1, 3, 0))
				{
					_config.Transfer = Convert.ToBoolean(Config.Get(LangRu
						? "Включить переводы денег между игроками?"
						: "Enable money transfers between players??"));

					var color1 = Config.Get<string>(LangRu ? "1 Цвет" : "First Color");
					var color2 = Config.Get<string>(LangRu ? "2 Цвет" : "Second Color");
					var color3 = Config.Get<string>(LangRu ? "3 Цвет" : "Third Color");
					var color4 = Config.Get<string>(LangRu ? "4 Цвет" : "Fourth Color");
					var color5 = Config.Get<string>(LangRu ? "5 Цвет" : "Fifth Color");
					var color6 = Config.Get<string>(LangRu ? "6 Цвет" : "Sixth Color");
					var color7 = Config.Get<string>(LangRu ? "7 Цвет" : "Seventh Color");

					_config.UI.Color1 = new IColor(color1);
					_config.UI.Color2 = new IColor(color2);
					_config.UI.Color3 = new IColor(color3);
					_config.UI.Color5 = new IColor(color5);
					_config.UI.Color7 = new IColor(color7);
					_config.UI.Color8 = new IColor("#FFFFFF");
					_config.UI.Color9 = new IColor(color2, 33);
					_config.UI.Color10 = new IColor(color2, 50);
					_config.UI.Color11 = new IColor(color1, 95);
					_config.UI.Color12 = new IColor(color1, 80);
					_config.UI.Color13 = new IColor(color3, 80);

					_itemsData.Shop.ForEach(category =>
					{
						category.Items.ForEach(item => { item.UseCustomDiscount = true; });
					});

					_itemsData.Shop.Insert(0, new ShopCategory
					{
						Enabled = false,
						CategoryType = ShopCategory.Type.Favorite,
						Title = "Favorites",
						Localization = new Localization
						{
							Enabled = false,
							Messages = new Dictionary<string, string>
							{
								["en"] = "Favorites",
								["fr"] = "Favoris"
							}
						},
						Permission = string.Empty,
						SortType = Configuration.SortType.None,
						Items = new List<ShopItem>()
					});
				}

				if (_config.Version < new VersionNumber(1, 3, 4))
					_itemsData.Shop.ForEach(category =>
					{
						category.Items.ForEach(item =>
						{
							item.Genes = new ItemGenes
							{
								Enabled = false,
								GeneTypes = new List<char>
								{
									'X', 'Y', 'G', 'W', 'H', 'W'
								}
							};
						});
					});

				if (_config.Version < new VersionNumber(1, 3, 7))
					_itemsData.Shop.ForEach(category =>
					{
						category.Items.ForEach(item =>
						{
							item.Currencies = new ShopItem.ItemCurrency
							{
								Enabled = false,
								Currencies = new Dictionary<int, ShopItem.CurrencyInfo>
								{
									[0] = new()
									{
										Price = item.Price
									},
									[1] = new()
									{
										Price = item.Price
									},
								},
								SellCurrencies = new Dictionary<int, ShopItem.CurrencyInfo>
								{
									[0] = new()
									{
										Price = item.SellPrice
									},
									[1] = new()
									{
										Price = item.SellPrice
									},
								}
							};
						});
					});

				/*
				if (_config.Version < new VersionNumber(1, 3, 13))
				{
					if (string.IsNullOrEmpty(_config.Economy.PriceLangKey))
						_config.Economy.PriceLangKey = baseConfig.Economy.PriceLangKey;

					foreach (var additionalEconomy in _config.AdditionalEconomics)
					{
						if (additionalEconomy.TitleLangKey == "sr_title") 
							additionalEconomy.PriceLangKey = "sr_price";
					}
				}
				*/

				if (_config.Version < new VersionNumber(2, 0, 0))
				{
					if (Config.Get(LangRu ? "Магазин" : "Shop") is List<object> categories)
					{
						var list = new List<ShopCategory>();
						foreach (var category in categories)
						{
							var shopCategory =
								JsonConvert.DeserializeObject<ShopCategory>(JsonConvert.SerializeObject(category));
							if (shopCategory == null)
							{
								continue;
							}

							list.Add(shopCategory);
						}

						_itemsData = new ItemsData
						{
							Shop = list
						};

						SaveItemsData();
					}

					if (LangRu)
					{
						_config.UI.Color1.Alpha = Convert.ToSingle(Config.Get("Интерфейс", "Цвет 1", "Непрозрачность (0 - 100)"));
						_config.UI.Color2.Alpha = Convert.ToSingle(Config.Get("Интерфейс", "Цвет 2", "Непрозрачность (0 - 100)"));
						_config.UI.Color3.Alpha = Convert.ToSingle(Config.Get("Интерфейс", "Цвет 3", "Непрозрачность (0 - 100)"));
						_config.UI.Color5.Alpha = Convert.ToSingle(Config.Get("Интерфейс", "Цвет 5", "Непрозрачность (0 - 100)"));
						_config.UI.Color7.Alpha = Convert.ToSingle(Config.Get("Интерфейс", "Цвет 7", "Непрозрачность (0 - 100)"));
						_config.UI.Color8.Alpha = Convert.ToSingle(Config.Get("Интерфейс", "Цвет 8", "Непрозрачность (0 - 100)"));
						_config.UI.Color9.Alpha = Convert.ToSingle(Config.Get("Интерфейс", "Цвет 9", "Непрозрачность (0 - 100)"));
						_config.UI.Color10.Alpha = Convert.ToSingle(Config.Get("Интерфейс", "Цвет 10", "Непрозрачность (0 - 100)"));
						_config.UI.Color11.Alpha = Convert.ToSingle(Config.Get("Интерфейс", "Цвет 11", "Непрозрачность (0 - 100)"));
						_config.UI.Color12.Alpha = Convert.ToSingle(Config.Get("Интерфейс", "Цвет 12", "Непрозрачность (0 - 100)"));
						_config.UI.Color13.Alpha = Convert.ToSingle(Config.Get("Интерфейс", "Цвет 13", "Непрозрачность (0 - 100)"));
					}
				}

				if (_config.Version >= new VersionNumber(2, 0, 0))
				{
					if (_config.Version < new VersionNumber(2, 0, 2))
					{
						if (_uiData == null)
							LoadUIData();

						if (_uiData != null)
						{
							if (_uiData.IsFullscreenUISet && _uiData.FullscreenUI?.SelectCurrency?.FrameIndent is -105)
							{
								_uiData.FullscreenUI.SelectCurrency.FrameIndent = 15;
							}

							if (_uiData.IsInMenuUISet && _uiData.InMenuUI?.SelectCurrency?.FrameIndent is -105)
							{
								_uiData.InMenuUI.SelectCurrency.FrameIndent = 15;
							}
						}
					}

					if (_config.Version < new VersionNumber(2, 1, 0))
					{
						if (_config.Version == new VersionNumber(2, 0, 0))
						{
							#region Load Config Variables

							(int ID, string TitleLangKey, string BalanceLangKey, string PriceLangKey)
								mainEconomy =
									new(
										0,
										Convert.ToString(Config.Get(LangRu ? "Экономика" : "Economy", LangRu ? "Lang Ключ (для названия)" : "Lang Key (for Title)")),
										Convert.ToString(Config.Get(LangRu ? "Экономика" : "Economy", LangRu ? "Lang Ключ (для баланса)" : "Lang Key (for Balance)")),
										Convert.ToString(Config.Get(LangRu ? "Экономика" : "Economy", LangRu ? "Lang Ключ (для стоимости)" : "Lang Key (for Price)")));

							var additionalEconomyTitles = new List<(int ID, string TitleLangKey, string BalanceLangKey, string PriceLangKey)>();

							if (Config.Get(LangRu ? "Дополнительная экономика" : "Additional Economics") is List<object> additionalEcoObj)
							{
								foreach (var obj in additionalEcoObj)
								{
									if (obj is not Dictionary<string, object> jsonObj) continue;
									if (!jsonObj.TryGetValue("ID", out var idObj)) continue;

									var id = Convert.ToInt32(idObj);
									var titleLangKey = Convert.ToString(jsonObj[LangRu ? "Lang Ключ (для названия)" : "Lang Key (for Title)"]);
									var balanceLangKey = Convert.ToString(jsonObj[LangRu ? "Lang Ключ (для баланса)" : "Lang Key (for Balance)"]);
									var priceLangKey = Convert.ToString(jsonObj[LangRu ? "Lang Ключ (для стоимости)" : "Lang Key (for Price)"]);

									additionalEconomyTitles.Add((id, titleLangKey, balanceLangKey, priceLangKey));
								}
							}

							#endregion

							var langToUpdate = new Dictionary<int, Dictionary<string, (string TitleLangKey, string BalanceLangKey, string PriceLangKey)>>();

							foreach (var targetLang in lang.GetLanguages(this))
							{
								var messageFile = GetMessageFile(Name, targetLang);
								if (messageFile is not null)
								{
									LoadEconomyUpdateMessages(mainEconomy);
									additionalEconomyTitles?.ForEach(LoadEconomyUpdateMessages);
								}

								void LoadEconomyUpdateMessages((int ID, string TitleLangKey, string BalanceLangKey, string PriceLangKey) targetEconomy)
								{
									if (messageFile.TryGetValue(targetEconomy.TitleLangKey, out var msgTitle) &&
										messageFile.TryGetValue(targetEconomy.BalanceLangKey, out var msgBalance) &&
										messageFile.TryGetValue(targetEconomy.PriceLangKey, out var msgPrice))
									{
										msgBalance = msgBalance.Replace("{1}", "RP");
										msgPrice = msgPrice.Replace("{1}", "RP");

										(string TitleLangKey, string BalanceLangKey, string PriceLangKey) newMessages = new(msgTitle, msgBalance, msgPrice);

										if (langToUpdate.TryGetValue(targetEconomy.ID, out var economyLangs))
										{
											economyLangs[targetLang] = newMessages;
										}
										else
										{
											langToUpdate.TryAdd(targetEconomy.ID, new Dictionary<string, (string TitleLangKey, string BalanceLangKey, string PriceLangKey)>
											{
												[targetLang] = newMessages
											});
										}
									}
								}
							}

							foreach (var (economyID, economyLangs) in langToUpdate)
							{
								var targetEconomy = economyID == 0 ? _config.Economy : _config.AdditionalEconomics.Find(ec => ec.ID == economyID);
								if (targetEconomy == null) continue;

								var dict = new Dictionary<string, Dictionary<string, string>>();

								foreach (var (targetLang, messages) in economyLangs)
								{
									if (dict.TryGetValue("TitleLangKey", out var titleLang))
									{
										titleLang[targetLang] = messages.TitleLangKey;
									}
									else
									{
										dict.TryAdd("TitleLangKey", new Dictionary<string, string>
										{
											[targetLang] = messages.TitleLangKey
										});
									}

									if (dict.TryGetValue("BalanceLangKey", out var balanceLang))
									{
										balanceLang[targetLang] = messages.BalanceLangKey;
									}
									else
									{
										dict.TryAdd("BalanceLangKey", new Dictionary<string, string>
										{
											[targetLang] = messages.BalanceLangKey
										});
									}

									if (dict.TryGetValue("PriceLangKey", out var priceLang))
									{
										priceLang[targetLang] = messages.PriceLangKey;
									}
									else
									{
										dict.TryAdd("PriceLangKey", new Dictionary<string, string>
										{
											[targetLang] = messages.PriceLangKey
										});
									}
								}

								if (dict.Count == 0) continue;

								foreach (var (msgkey, messages) in dict)
								{
									switch (msgkey)
									{
										case "TitleLangKey":
											targetEconomy.EconomyTitle = new EconomyTitle(messages);
											break;
										case "BalanceLangKey":
											targetEconomy.EconomyBalance = new EconomyTitle(messages);
											break;
										case "PriceLangKey":
											targetEconomy.EconomyPrice = new EconomyTitle(messages);
											targetEconomy.EconomyFooterPrice = new EconomyTitle(messages);
											break;
									}
								}
							}
						}
						else if (_config.Version >= new VersionNumber(2, 0, 1))
						{
							#region Load Config Variables

							(int ID, string Title, string TitleLangKey, string BalanceLangKey, string PriceLangKey)
								mainEconomy =
									new(
										0,
										Convert.ToString(Config.Get(LangRu ? "Экономика" : "Economy", LangRu ? "Название экономики" : "Economy Title")),
										Convert.ToString(Config.Get(LangRu ? "Экономика" : "Economy", LangRu ? "Lang Ключ (для названия)" : "Lang Key (for Title)")),
										Convert.ToString(Config.Get(LangRu ? "Экономика" : "Economy", LangRu ? "Lang Ключ (для баланса)" : "Lang Key (for Balance)")),
										Convert.ToString(Config.Get(LangRu ? "Экономика" : "Economy", LangRu ? "Lang Ключ (для стоимости)" : "Lang Key (for Price)")));

							var additionalEconomyTitles = new List<(int ID, string Title, string TitleLangKey, string BalanceLangKey, string PriceLangKey)>();

							if (Config.Get(LangRu ? "Дополнительная экономика" : "Additional Economics") is List<object> additionalEcoObj)
							{
								foreach (var obj in additionalEcoObj)
								{
									if (obj is not Dictionary<string, object> jsonObj) continue;
									if (!jsonObj.TryGetValue(LangRu ? "Название экономики" : "Economy Title", out var economyTitle)) continue;

									var id = Convert.ToInt32(jsonObj["ID"]);
									var titleLangKey = Convert.ToString(jsonObj[LangRu ? "Lang Ключ (для названия)" : "Lang Key (for Title)"]);
									var balanceLangKey = Convert.ToString(jsonObj[LangRu ? "Lang Ключ (для баланса)" : "Lang Key (for Balance)"]);
									var priceLangKey = Convert.ToString(jsonObj[LangRu ? "Lang Ключ (для стоимости)" : "Lang Key (for Price)"]);

									additionalEconomyTitles.Add((id, Convert.ToString(economyTitle), titleLangKey, balanceLangKey, priceLangKey));
								}
							}

							#endregion

							var langToUpdate = new Dictionary<int, Dictionary<string, (string TitleLangKey, string BalanceLangKey, string PriceLangKey)>>();

							foreach (var targetLang in lang.GetLanguages(this))
							{
								var messageFile = GetMessageFile(Name, targetLang);
								if (messageFile is not null)
								{
									LoadEconomyUpdateMessages(mainEconomy);
									additionalEconomyTitles?.ForEach(LoadEconomyUpdateMessages);
								}

								void LoadEconomyUpdateMessages((int ID, string Title, string TitleLangKey, string BalanceLangKey, string PriceLangKey) targetEconomy)
								{
									if (messageFile.TryGetValue(targetEconomy.TitleLangKey, out var msgTitle) &&
										messageFile.TryGetValue(targetEconomy.BalanceLangKey, out var msgBalance) &&
										messageFile.TryGetValue(targetEconomy.PriceLangKey, out var msgPrice))
									{
										var economyTitle = targetEconomy.Title ?? "RP";

										msgBalance = msgBalance.Replace("{1}", economyTitle);
										msgPrice = msgPrice.Replace("{1}", economyTitle);

										(string TitleLangKey, string BalanceLangKey, string PriceLangKey) newMessages = new(msgTitle, msgBalance, msgPrice);

										if (langToUpdate.TryGetValue(targetEconomy.ID, out var economyLangs))
										{
											economyLangs[targetLang] = newMessages;
										}
										else
										{
											langToUpdate.TryAdd(targetEconomy.ID, new Dictionary<string, (string TitleLangKey, string BalanceLangKey, string PriceLangKey)>
											{
												[targetLang] = newMessages
											});
										}
									}
								}
							}

							foreach (var (economyID, economyLangs) in langToUpdate)
							{
								var targetEconomy = economyID == 0 ? _config.Economy : _config.AdditionalEconomics.Find(ec => ec.ID == economyID);
								if (targetEconomy == null) continue;

								var dict = new Dictionary<string, Dictionary<string, string>>();

								foreach (var (targetLang, messages) in economyLangs)
								{
									if (dict.TryGetValue("TitleLangKey", out var titleLang))
									{
										titleLang[targetLang] = messages.TitleLangKey;
									}
									else
									{
										dict.TryAdd("TitleLangKey", new Dictionary<string, string>
										{
											[targetLang] = messages.TitleLangKey
										});
									}

									if (dict.TryGetValue("BalanceLangKey", out var balanceLang))
									{
										balanceLang[targetLang] = messages.BalanceLangKey;
									}
									else
									{
										dict.TryAdd("BalanceLangKey", new Dictionary<string, string>
										{
											[targetLang] = messages.BalanceLangKey
										});
									}

									if (dict.TryGetValue("PriceLangKey", out var priceLang))
									{
										priceLang[targetLang] = messages.PriceLangKey;
									}
									else
									{
										dict.TryAdd("PriceLangKey", new Dictionary<string, string>
										{
											[targetLang] = messages.PriceLangKey
										});
									}
								}

								if (dict.Count == 0) continue;

								foreach (var (msgkey, messages) in dict)
								{
									switch (msgkey)
									{
										case "TitleLangKey":
											targetEconomy.EconomyTitle = new EconomyTitle(messages);
											break;
										case "BalanceLangKey":
											targetEconomy.EconomyBalance = new EconomyTitle(messages);
											break;
										case "PriceLangKey":
											targetEconomy.EconomyPrice = new EconomyTitle(messages);
											targetEconomy.EconomyFooterPrice = new EconomyTitle(messages);
											break;
									}
								}
							}
						}
					}
				}
			}

			_config.Version = Version;
			PrintWarning("Config update completed!");
		}

		#endregion

		#region Data

		#region Players Data

		private Dictionary<ulong, PlayerData> _usersData = new();

		private class PlayerData
		{
			#region Fields

			[JsonProperty(PropertyName = "Cart")] public PlayerCartData PlayerCart = new();

			[JsonProperty(PropertyName = "NPC Cart")]
			public PlayerNPCCart NPCCart = new();

			[JsonProperty(PropertyName = "Limits")]
			public LimitData Limits = new();

			[JsonProperty(PropertyName = "Cooldowns")]
			public CooldownData Cooldowns = new();

			[JsonProperty(PropertyName = "Selected Economy")]
			public int SelectedEconomy;

			#endregion

			#region Helpers

			#region Economy

			public void SelectEconomy(int id)
			{
				SelectedEconomy = id;
			}

			public EconomyEntry GetEconomy()
			{
				if (_instance._economics.Count <= 1) return _config.Economy;

				return _instance._additionalEconomics.TryGetValue(SelectedEconomy, out var economyConf)
					? economyConf
					: _config.Economy;
			}

			#endregion

			#endregion

			#region Classes

			public class CooldownData
			{
				#region Fields

				[JsonProperty(PropertyName = "Cooldowns", ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public Dictionary<int, ItemData> Items = new();

				#endregion

				#region Helpers

				public ItemData GetCooldown(ShopItem item)
				{
					return Items.GetValueOrDefault(item.ID);
				}

				public int GetCooldownTime(string player, ShopItem item, bool buy)
				{
					var data = GetCooldown(item);
					if (data == null) return -1;

					return Convert.ToInt32(data.GetTime(buy).AddSeconds(item.GetCooldown(player, buy))
						.Subtract(DateTime.UtcNow).TotalSeconds);
				}

				public bool HasCooldown(ShopItem item, bool buy)
				{
					var data = GetCooldown(item);
					if (data == null) return false;

					return Convert.ToInt32(data.GetTime(buy).AddSeconds(buy ? item.BuyCooldown : item.SellCooldown)
						.Subtract(DateTime.UtcNow).TotalSeconds) <= 0;
				}

				public void RemoveCooldown(ShopItem item)
				{
					Items.Remove(item.ID);
				}

				public void SetCooldown(ShopItem item, bool buy)
				{
					Items.TryAdd(item.ID, new ItemData());

					if (buy)
						Items[item.ID].LastBuyTime = DateTime.UtcNow;
					else
						Items[item.ID].LastSellTime = DateTime.UtcNow;
				}

				#endregion

				#region Classes

				public class ItemData
				{
					public DateTime LastBuyTime = new(1970, 1, 1, 0, 0, 0);

					public DateTime LastSellTime = new(1970, 1, 1, 0, 0, 0);

					public DateTime GetTime(bool buy)
					{
						return buy ? LastBuyTime : LastSellTime;
					}
				}

				#endregion
			}

			public class LimitData
			{
				#region Fields

				[JsonProperty(PropertyName = "Limits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public Dictionary<int, ItemData> ItemsLimits = new();

				[JsonProperty(PropertyName = "Last Update Time")]
				public DateTime LastUpdate;

				[JsonProperty(PropertyName = "Daily Limits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public Dictionary<int, ItemData> DailyItemsLimits = new();

				#endregion

				#region Helpers

				public void AddItem(ShopItem item, bool buy, int amount, bool daily = false)
				{
					var totalAmount = item.Amount * amount;

					var dict = daily ? DailyItemsLimits : ItemsLimits;

					dict.TryAdd(item.ID, new ItemData());

					if (buy)
						dict[item.ID].Buy += totalAmount;
					else
						dict[item.ID].Sell += totalAmount;
				}

				public int GetLimit(ShopItem item, bool buy, bool daily = false)
				{
					if (daily && DateTime.UtcNow.Date != LastUpdate.Date) // auto wipe
					{
						LastUpdate = DateTime.UtcNow;
						DailyItemsLimits.Clear();
					}

					return (daily ? DailyItemsLimits : ItemsLimits).TryGetValue(item.ID, out var data)
						? buy ? data.Buy : data.Sell
						: 0;
				}

				#endregion

				#region Classes

				public class ItemData
				{
					public int Sell;

					public int Buy;
				}

				#endregion
			}

			#endregion

			#region Storage

			private static string BaseFolder()
			{
				return "Shop" + Path.DirectorySeparatorChar + "Players" + Path.DirectorySeparatorChar;
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

				return GetOrLoad(userId) ?? (_instance._usersData[userId] = new PlayerData());
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

			private static PlayerData ReadOnlyObject(string userId)
			{
				return Interface.Oxide.DataFileSystem.ExistsDatafile(userId)
					? Interface.Oxide.DataFileSystem.GetFile(userId).ReadObject<PlayerData>()
					: null;
			}

			public static void DoWipe(string userId, bool carts, bool cooldowns, bool limits)
			{
				if (carts && cooldowns && limits)
				{
					Interface.Oxide.DataFileSystem.DeleteDataFile(BaseFolder() + userId);
				}
				else
				{
					if (!ulong.TryParse(userId, out var userID)) return;

					var data = GetOrLoad(userID);
					if (data == null) return;

					if (carts)
					{
						data.PlayerCart = new PlayerCartData();
						data.NPCCart = new PlayerNPCCart();
					}

					if (limits) data.Limits = new LimitData();

					if (cooldowns) data.Cooldowns = new CooldownData();

					SaveAndUnload(userID);
				}
			}

			#endregion

			#endregion
		}

		#endregion Players Data

		#region Carts

		private class CartData
		{
			#region Fields

			[JsonProperty(PropertyName = "Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<int, int> Items = new();

			#endregion

			#region Helpers

			#region Cart

			public void AddCartItem(ShopItem item, BasePlayer player)
			{
				int result;
				if (Items.TryGetValue(item.ID, out var amount))
				{
					if (item.BuyMaxAmount > 0 && amount >= item.BuyMaxAmount) return;

					if (!CanAddItemToCart(player, item, amount + 1, out result))
					{
						_config?.Notifications?.ShowNotify(player, result == 1 ? BuyLimitReached : DailyBuyLimitReached, 1,
							item.GetPublicTitle(player));
						return;
					}

					AddCartItemAmount(item, 1);
				}
				else
				{
					if (!CanAddItemToCart(player, item, 1, out result))
					{
						_config?.Notifications?.ShowNotify(player, result == 1 ? BuyLimitReached : DailyBuyLimitReached, 1,
							item.GetPublicTitle(player));
						return;
					}

					AddCartItem(item, 1);
				}
			}

			private bool CanAddItemToCart(BasePlayer player, ShopItem item, int amount, out int result)
			{
				if (HasLimit(player, item, true, out var leftLimit) && amount >= leftLimit) //total Limit
				{
					result = 1;
					return false;
				}

				if (HasLimit(player, item, true, out leftLimit, true) && amount > leftLimit) //daily Limit
				{
					result = 2;
					return false;
				}

				result = 0;
				return true;
			}


			public void ChangeAmountItem(BasePlayer player, ShopItem item, int amount)
			{
				if (amount > 0)
				{
					if (HasLimit(player, item, true, out var totalLimit) && amount >= totalLimit)
						amount = Math.Min(totalLimit, amount);

					if (HasLimit(player, item, true, out var dailyLimit, true) && amount >= dailyLimit)
						amount = Math.Min(dailyLimit, amount);

					if (amount <= 0) return;

					SetCartItemAmount(item, amount);
				}
				else
				{
					RemoveCartItem(item);
				}
			}

			public int GetCartItemsAmount()
			{
				return GetShopItems().Sum(x => x.Key.Amount * x.Value);
			}

			public double GetCartPrice(BasePlayer player, bool again = false)
			{
				var selectedEconomy = _instance.API_GetShopPlayerSelectedEconomy(player.userID);
				return GetShopItems(again).Sum(x => x.Key.GetPrice(player, selectedEconomy) * x.Value);
			}

			public int GetCartItemsStacksAmount()
			{
				return GetShopItems().Sum(check => check.Key.Type == ItemType.Item && check.Key.ItemDefinition != null
					? check.Key.ProhibitSplit
						? 1
						: check.Key.GetStacks(check.Value).Count
					: 0);
			}

			public void ClearCartItems()
			{
				Items.Clear();
			}

			public void RemoveCartItem(ShopItem item)
			{
				Items.Remove(item.ID);
			}

			public void AddCartItem(ShopItem item, int id)
			{
				Items.Add(item.ID, id);
			}

			public void AddCartItemAmount(ShopItem item, int amount)
			{
				Items[item.ID] += amount;
			}

			public void SetCartItemAmount(ShopItem item, int amount)
			{
				Items[item.ID] = amount;
			}

			#endregion

			public Dictionary<ShopItem, int> GetShopItems(bool lastItems = false)
			{
				var dict = new Dictionary<ShopItem, int>();
				foreach (var check in Items)
				{
					var shopItem = _instance?.FindItemById(check.Key);
					if (shopItem != null) dict.TryAdd(shopItem, check.Value);
				}

				return dict;
			}

			#endregion
		}

		private class PlayerNPCCart
		{
			[JsonProperty(PropertyName = "NPC Carts", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, CartData> Carts = new();
		}

		private class PlayerCartData : CartData
		{
			#region Fields

			[JsonProperty(PropertyName = "Last Purchase Items",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<int, int> LastPurchaseItems = new();

			[JsonProperty(PropertyName = "Favorite Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public HashSet<int> FavoriteItems = new();

			#endregion

			#region Helpers

			#region Favorite

			public bool AddItemToFavorite(int itemID)
			{
				return FavoriteItems.Add(itemID);
			}

			public bool IsFavoriteItem(int itemID)
			{
				return FavoriteItems.Contains(itemID);
			}

			public bool RemoveItemFromFavorites(int itemID)
			{
				return FavoriteItems.Remove(itemID);
			}

			public List<ShopItem> GetFavoriteItems()
			{
				var list = new List<ShopItem>();

				foreach (var itemID in FavoriteItems)
				{
					var shopItem = _instance?.FindItemById(itemID);
					if (shopItem != null)
						list.Add(shopItem);
				}

				return list;
			}

			#endregion

			public new Dictionary<ShopItem, int> GetShopItems(bool lastItems = false)
			{
				var dict = new Dictionary<ShopItem, int>();
				foreach (var check in lastItems ? LastPurchaseItems : Items)
				{
					var shopItem = _instance?.FindItemById(check.Key);
					if (shopItem != null) dict.TryAdd(shopItem, check.Value);
				}

				return dict;
			}

			public void SaveLastPurchaseItems()
			{
				LastPurchaseItems = new Dictionary<int, int>(Items);
			}

			#endregion
		}

		#endregion
		
		#region Items Data
		
		private static ItemsData  _itemsData;
		
		private class ItemsData
		{
			[JsonProperty(PropertyName = LangRu ? "Категории Магазина" : "Shop Categories",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<ShopCategory> Shop = new();
		}
		
		private void SaveItemsData() => Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Shops/Default", _itemsData);

		private void LoadItemsData()
		{
			try
			{
				_itemsData = Interface.Oxide.DataFileSystem.ReadObject<ItemsData>($"{Name}/Shops/Default");
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			_itemsData ??= new ItemsData();
		}
		
		#endregion Items Data

		#region UI Data

		private static UIData _uiData = null;
		
		private void SaveUIData() => Interface.Oxide.DataFileSystem.WriteObject($"{Name}/UI", _uiData);
		
		private void LoadUIData()
		{
			try
			{
				_uiData = ReadOnlyDataObject<UIData>($"{Name}/UI");
			}
			catch (Exception e)
			{
				PrintError("Loading UI Data exception: " + e.ToString());
			}

			_uiData ??= new UIData();

			if (!_uiData.IsFullscreenUISet || _uiData.FullscreenUI == null)
			{
				_initializedStatus = (false, "not_installed_template");
			}
		}

		private T ReadOnlyDataObject<T>(string name)
		{
			var targetFile = Interface.Oxide.DataFileSystem.GetFile(name);
			if (targetFile == null || !targetFile.Exists()) return default;
			
			var settings = new JsonSerializerSettings()
			{
				MissingMemberHandling = MissingMemberHandling.Error
			};
			var deserializedObject = JsonConvert.DeserializeObject<T>(File.ReadAllText(targetFile.Filename), settings);
			return deserializedObject ?? default;
		}

		private void RegisterImagesFromUI(Dictionary<string,string> imagesList)
		{
			if (_uiData == null) return;

			if (_uiData.IsFullscreenUISet)
			{
				_uiData.FullscreenUI?.RegisterImages(imagesList);
			}
			
			if (_uiData.IsInMenuUISet)
			{
				_uiData.InMenuUI?.RegisterImages(imagesList);
			}
		}
		
		private class UIData
		{
			[JsonProperty(PropertyName ="Use expert mode for Fullscreen template?")]
			public bool UseExpertModeForFullscreenUI = false;

			[JsonProperty(PropertyName ="Is Full Screen UI Set")]
			public bool IsFullscreenUISet = false;

			[JsonProperty(PropertyName = "Full Screen UI")]
			public ShopUI FullscreenUI = null;

			[JsonProperty(PropertyName ="Use expert mode for In-Menu template?")]
			public bool UseExpertModeForInMenuUI = false;

			[JsonProperty(PropertyName = "Is In-Menu UI Set")]
			public bool IsInMenuUISet = false;

			[JsonProperty(PropertyName = "In-Menu UI")]
			public ShopUI InMenuUI = null;
		}

		public class ShopUI
		{
			#region Fields
			
			[JsonProperty(PropertyName = "Template Name")]
			public string TemplateName = null;
			
			[JsonProperty(PropertyName = "Select Currency Settings")]
			public SelectCurrencyUI SelectCurrency = new ();

			[JsonProperty(PropertyName = "Background Settings")]
			public ShopBackgroundUI ShopBackground = new ();

			[JsonProperty(PropertyName = "Shop Item Settings")]
			public ShopItemUI ShopItem = new ();

			[JsonProperty(PropertyName = "Categories Settings")]
			public CategoriesUI ShopCategories = new ();

			[JsonProperty(PropertyName = "Main Panel Settings")]
			public ShopContentUI ShopContent = new ();

			[JsonProperty(PropertyName = "Basket Settings")]
			public ShopBasketUI ShopBasket = new ();

			[JsonProperty(PropertyName = "No Items title")]
			public TextSettings NoItems = new ();

			[JsonProperty(PropertyName = "Shop Buy Modal Settings")]
			public ShopActionModalUI ShopBuyModal = new ();

			[JsonProperty(PropertyName = "Shop Sell Modal Settings")]
			public ShopActionModalUI ShopSellModal = new ();

			[JsonProperty(PropertyName = "Shop Item Description Modal Settings")]
			public ShopItemDescriptionModalUI ShopItemDescriptionModal = new();

			[JsonProperty(PropertyName = "Shop Confirmation Modal Settings")]
			public ShopConfirmationModalUI ShopConfirmationModal = new();
			
			#endregion

			#region Classes

			public class ShopBackgroundUI
			{
				#region Fields
				[JsonProperty(PropertyName = "Use background?")]
				public bool UseBackground;

				[JsonProperty(PropertyName = "Display type (Overlay/Hud)")]
				public string DisplayType;

				[JsonProperty(PropertyName = "Background")]
				public ImageSettings Background;

				[JsonProperty(PropertyName = "Close on click?")]
				public bool CloseOnClick;
				#endregion
			}
			
			public class ShopItemUI
			{
				#region Fields
				[JsonProperty(PropertyName = "Items On String")]
				public int ItemsOnString;

				[JsonProperty(PropertyName = "Strings")]
				public int Strings;

				[JsonProperty(PropertyName = "Item Height")]
				public float ItemHeight;

				[JsonProperty(PropertyName = "Item Width")]
				public float ItemWidth;

				[JsonProperty(PropertyName = "Margin")]
				public float Margin;

				[JsonProperty(PropertyName = "Background")]
				public ImageSettings Background;

				[JsonProperty(PropertyName = "Title")]
				public TextSettings Title;

				[JsonProperty(PropertyName = "Blueprint")]
				public InterfacePosition Blueprint;

				[JsonProperty(PropertyName = "Image")]
				public InterfacePosition Image;

				[JsonProperty(PropertyName = "Amount")]
				public AmountUI Amount;

				[JsonProperty(PropertyName = "Favorite")]
				public FavoriteUI Favorite;

				[JsonProperty(PropertyName = "Info")]
				public ButtonSettings Info;

				[JsonProperty(PropertyName = "Discount")]
				public DiscountUI Discount;

				[JsonProperty(PropertyName = "Buy Button")]
				public ActionButtonUI BuyButton;

				[JsonProperty(PropertyName = "Buy Button (if there is no Sell button)")]
				public ActionButtonUI BuyButtonIfNoSell;

				[JsonProperty(PropertyName = "Sell Button")]
				public ActionButtonUI SellButton;

				[JsonProperty(PropertyName = "Sell Button (if there is no Buy button)")]
				public ActionButtonUI SellButtonIfNoBuy;

				[JsonProperty(PropertyName = "Admin Panel")]
				public AdminUI AdminPanel = new();
				#endregion

				#region Classes

				public class AdminUI 
				{
					[JsonProperty(PropertyName = "Additional margin to the item panel")]
					public float AdditionalMargin;

					[JsonProperty(PropertyName = "Background")]
					public ImageSettings Background = new();

					[JsonProperty(PropertyName = "Edit Button")]
					public ButtonSettings ButtonEdit = new();

					[JsonProperty(PropertyName = "Move Right Button")]
					public ButtonSettings ButtonMoveRight = new();

					[JsonProperty(PropertyName = "Move Left Button")]
					public ButtonSettings ButtonMoveLeft = new();
				}

				public class AmountUI
				{
					[JsonProperty(PropertyName = "Title")]
					public TextSettings Title;

					[JsonProperty(PropertyName = "Background")]
					public ImageSettings Background;

					[JsonProperty(PropertyName = "Value")]
					public TextSettings Value;
				}

				public class FavoriteUI
				{
					[JsonProperty(PropertyName = "Add To Favorites")]
					public ButtonSettings AddToFavorites;

					[JsonProperty(PropertyName = "Remove From Favorites")]
					public ButtonSettings RemoveFromFavorites;
				}

				public class DiscountUI
				{
					[JsonProperty(PropertyName = "Background")]
					public ImageSettings Background;

					[JsonProperty(PropertyName = "Value")]
					public TextSettings Value;
				}

				public class ActionButtonUI
				{
					#region Fields
					[JsonProperty(PropertyName = "Cooldown")]
					public CooldownUI Cooldown;

					[JsonProperty(PropertyName = "Background")]
					public ImageSettings Background;

					[JsonProperty(PropertyName = "Title")]
					public TextSettings Title;

					[JsonProperty(PropertyName = "Price")]
					public TextSettings Price;
					#endregion

					#region Classes

					public class CooldownUI
					{
						[JsonProperty(PropertyName = "Background")]
						public ImageSettings Background;

						[JsonProperty(PropertyName = "Title")]
						public TextSettings Title;

						[JsonProperty(PropertyName = "Left Time")]
						public TextSettings LeftTime;
					}

					#endregion
				}

				#endregion
			}

			public class CategoriesUI
			{
				#region Fields

				[JsonProperty(PropertyName = "Background")]
				public ImageSettings Background = new();

				[JsonProperty(PropertyName = "Header")]
				public HeaderUI Header = new();

				[JsonProperty(PropertyName = "Categories panel")]
				public ImageSettings CategoriesPanel = new();

				[JsonProperty(PropertyName = "Category Settings")]
				public ShopCategoryUI ShopCategory = new();

				[JsonProperty(PropertyName = "Use scroll in categories?")]
				public bool UseScrollCategories;

				[JsonProperty(PropertyName = "Scroll in categories")]
				public ScrollViewUI CategoriesScrollView = new();

				[JsonProperty(PropertyName = "Back Button")]
				public ButtonSettings BackButton = new();

				[JsonProperty(PropertyName = "Next Button")]
				public ButtonSettings NextButton = new();

				[JsonProperty(PropertyName = "Admin Panel")]
				public ShopCategoryAdminPanelUI CategoryAdminPanel = new();
				#endregion

				#region Classes

				public class ShopCategoryAdminPanelUI
				{
					[JsonProperty(PropertyName = "Add Category Button")]
					public ButtonSettings ButtonAddCategory = new();

					[JsonProperty(PropertyName = "Category Display Checkbox")]
					public CheckBoxSettings CheckboxCategoriesDisplay = new();
				}

				public class ShopCategoryUI
				{
					#region Fields
					[JsonProperty(PropertyName = "Top indent for categories")]
					public float TopIndent;

					[JsonProperty(PropertyName = "Categories On Page")]
					public int CategoriesOnPage;

					[JsonProperty(PropertyName = "Left indent")]
					public float LeftIndent;

					[JsonProperty(PropertyName = "Width")]
					public float Width;

					[JsonProperty(PropertyName = "Height")]
					public float Height;

					[JsonProperty(PropertyName = "Margin")]
					public float Margin;

					[JsonProperty(PropertyName = "Selected Category")]
					public CategoryUI SelectedCategory = new();

					[JsonProperty(PropertyName = "Category")]
					public CategoryUI Category = new();

					[JsonProperty(PropertyName = "Admin Panel")]
					public AdminUI AdminPanel = new();
					#endregion Fields

					#region Classes

					public class AdminUI
					{
						[JsonProperty(PropertyName = "Additional margin to the item panel")]
						public float AdditionalMargin;

						[JsonProperty(PropertyName = "Background")]
						public ImageSettings Background = new();

						[JsonProperty(PropertyName = "Edit Button")]
						public ButtonSettings ButtonEdit = new();

						[JsonProperty(PropertyName = "Move UP Button")]
						public ButtonSettings ButtonMoveUp = new();

						[JsonProperty(PropertyName = "Move DOWN Button")]
						public ButtonSettings ButtonMoveDown = new();
					}

					#endregion Classes
				}

				public class CategoryUI
				{
					[JsonProperty(PropertyName = "Background")]
					public ImageSettings Background = new();

					[JsonProperty(PropertyName = "Title")]
					public TextSettings Title = new();
				}
				
				public class HeaderUI
				{
					[JsonProperty(PropertyName = "Header background")]
					public ImageSettings Background = new();

					[JsonProperty(PropertyName = "Title")]
					public TextSettings Title = new();
				}

				#endregion
			}

			public class ShopBasketUI
			{
				#region Fields
				
				[JsonProperty(PropertyName = "Use Shop Basket?")]
				public bool UseShopBasket;

				[JsonProperty(PropertyName = "Background")]
				public ImageSettings Background = new();

				[JsonProperty(PropertyName = "Header")]
				public HeaderUI Header = new();

				[JsonProperty(PropertyName = "Content")]
				public ContentUI Content = new();

				[JsonProperty(PropertyName = "Footer")]
				public FooterUI Footer = new();

				[JsonProperty(PropertyName = "Basket Item")]
				public BasketItemUI BasketItem = new();

				[JsonProperty(PropertyName = "Show confirmation menu?")]
				public bool ShowConfirmMenu;

				#endregion
				
				#region Classes

				public class BasketItemUI
				{
					[JsonProperty(PropertyName = "Items On Page")]
					public int ItemsOnPage;

					[JsonProperty(PropertyName = "Top indent")]
					public float TopIndent;

					[JsonProperty(PropertyName = "Left indent")]
					public float LeftIndent;

					[JsonProperty(PropertyName = "Width")]
					public float Width;

					[JsonProperty(PropertyName = "Height")]
					public float Height;

					[JsonProperty(PropertyName = "Margin")]
					public float Margin;

					[JsonProperty(PropertyName = "Background")]
					public ImageSettings Background = new();

					[JsonProperty(PropertyName = "Show background for image?")]
					public bool ShowImageBackground;

					[JsonProperty(PropertyName = "Image Background")]
					public ImageSettings ImageBackground = new();

					[JsonProperty(PropertyName = "Blueprint Image")]
					public InterfacePosition ImageBlueprint = new();

					[JsonProperty(PropertyName = "Item Image")]
					public InterfacePosition ImageItem = new();

					[JsonProperty(PropertyName = "Title")]
					public TextSettings Title = new();

					[JsonProperty(PropertyName = "Item Amount")]
					public TextSettings ItemAmount = new();

					[JsonProperty(PropertyName = "Remove Item Button")]
					public ButtonSettings ButtonRemoveItem = new();

					[JsonProperty(PropertyName = "Plus Amount Button")]
					public ButtonSettings ButtonPlusAmount = new();

					[JsonProperty(PropertyName = "Minus Amount Button")]
					public ButtonSettings ButtonMinusAmount = new();

					[JsonProperty(PropertyName = "Amount input field")]
					public TextSettings InputAmount = new();
				}
				
				public class HeaderUI
				{
					[JsonProperty(PropertyName = "Header background")]
					public ImageSettings Background;

					[JsonProperty(PropertyName = "Title")]
					public TextSettings Title;

					[JsonProperty(PropertyName = "Back button (used when scrolling is disabled)")]
					public ButtonSettings BackButton;

					[JsonProperty(PropertyName = "Next button (used when scrolling is disabled)")]
					public ButtonSettings NextButton;
				}

				public class ContentUI
				{
					#region Fields

					[JsonProperty(PropertyName = "Background")]
					public ImageSettings Background = new();

					[JsonProperty(PropertyName = "Use scroll in shopping bag?")]
					public bool UseScrollShoppingBag;

					[JsonProperty(PropertyName = "Scroll in shopping bag")]
					public ScrollViewUI ShoppingBagScrollView = new();

					#endregion
				}
				
				public class FooterUI
				{
					[JsonProperty(PropertyName = "Background")]
					public ImageSettings Background;

					[JsonProperty(PropertyName = "Buy Button (when the Buy Again button is available)")]
					public ButtonSettings BuyButtonWhenBuyAgain;

					[JsonProperty(PropertyName = "Buy Button")]
					public ButtonSettings BuyButton;

					[JsonProperty(PropertyName = "Buy Again Button")]
					public ButtonSettings BuyAgainButton;

					[JsonProperty(PropertyName = "Items Count (Title)")]
					public TextSettings ItemsCountTitle;

					[JsonProperty(PropertyName = "Items Count (Value)")]
					public TextSettings ItemsCountValue;

					[JsonProperty(PropertyName = "Items Cost (Title)")]
					public TextSettings ItemsCostTitle;

					[JsonProperty(PropertyName = "Items Cost (Value)")]
					public TextSettings ItemsCostValue;
				}

				#endregion
			}
			
			public class ShopContentUI
			{
				#region Fields
				
				[JsonProperty(PropertyName = "Background")]
				public ImageSettings Background = new();

				[JsonProperty(PropertyName = "Header")]
				public HeaderUI Header = new();

				[JsonProperty(PropertyName = "Content")]
				public ContentUI Content = new();

				[JsonProperty(PropertyName = "Items Left Indent")]
				public float ItemsLeftIndent;

				[JsonProperty(PropertyName = "Items Top Indent")]
				public float ItemsTopIndent;

				#endregion
				
				#region Classes

				public class HeaderUI
				{
					[JsonProperty(PropertyName = "Header background")]
					public ImageSettings Background;

					[JsonProperty(PropertyName = "Title")]
					public TextSettings Title;

					[JsonProperty(PropertyName = "Open Transfer Menu button")]
					public ButtonSettings ButtonTransfer;

					[JsonProperty(PropertyName = "Toggle Economy Button")]
					public ButtonSettings ButtonToggleEconomy;

					[JsonProperty(PropertyName = "Balance")]
					public BalanceUI Balance;

					[JsonProperty(PropertyName = "Use close button?")]
					public bool UseCloseButton;

					[JsonProperty(PropertyName = "Close button")]
					public ButtonSettings ButtonClose;
				}

				public class BalanceUI
				{
					[JsonProperty(PropertyName = "Background")]
					public ImageSettings Background;

					[JsonProperty(PropertyName = "Title")]
					public TextSettings Title;

					[JsonProperty(PropertyName = "Value")]
					public TextSettings Value;
				}
				
				public class ContentUI
				{
					#region Fields

					[JsonProperty(PropertyName = "Use scroll to list items?")]
					public bool UseScrollToListItems;

					[JsonProperty(PropertyName = "Background")]
					public ImageSettings Background;

					[JsonProperty(PropertyName = "Scroll to list items")]
					public ScrollViewUI ListItemsScrollView = new();

					[JsonProperty(PropertyName = "Back button (when scrolling is off)")]
					public ButtonSettings ButtonBack;

					[JsonProperty(PropertyName = "Next button (when scrolling is off)")]
					public ButtonSettings ButtonNext;

					[JsonProperty(PropertyName = "Search")]
					public SearchUI Search;

					[JsonProperty(PropertyName = "Add Item Button")]
					public ButtonSettings ButtonAddItem;

					#endregion
				}

				public class SearchUI
				{
					[JsonProperty(PropertyName = "Enabled")]
					public bool Enabled;

					[JsonProperty(PropertyName = "Background")]
					public ImageSettings Background;

					[JsonProperty(PropertyName = "Input field")]
					public TextSettings InputField;
				}
				
				#endregion
			}
			
			public class ShopActionModalUI
			{
				#region Fields

				[JsonProperty(PropertyName = "Display type (Overlay/Hud)")]
				public string DisplayType;

				[JsonProperty(PropertyName = "Background")]
				public ImageSettings Background;

				[JsonProperty(PropertyName = "Modal Panel")]
				public ImageSettings ModalPanel;

				[JsonProperty(PropertyName = "Item Background")]
				public ImageSettings ItemBackground;

				[JsonProperty(PropertyName = "Item Icon")]
				public InterfacePosition ItemIcon;

				[JsonProperty(PropertyName = "Item Name")]
				public TextSettings ItemName;

				[JsonProperty(PropertyName = "Description Title")]
				public TextSettings DescriptionTitle;

				[JsonProperty(PropertyName = "Item Description")]
				public TextSettings ItemDescription;

				[JsonProperty(PropertyName = "Amount Title")]
				public TextSettings AmountTitle;

				[JsonProperty(PropertyName = "Amount Value")]
				public TextSettings AmountValue;

				[JsonProperty(PropertyName = "Price")]
				public TextSettings Price;

				[JsonProperty(PropertyName = "Minus Amount Button")]
				public ButtonSettings ButtonMinusAmount;

				[JsonProperty(PropertyName = "Plus Amount Button")]
				public ButtonSettings ButtonPlusAmount;

				[JsonProperty(PropertyName = "Set Max Amount Button")]
				public ButtonSettings ButtonSetMaxAmount;

				[JsonProperty(PropertyName = "Action Button (Buy/Sell)")]
				public ButtonSettings ButtonAction;

				[JsonProperty(PropertyName = "Close Button Icon")]
				public ButtonSettings ButtonClose;
				#endregion

				#region Public Methods

				public void GetModal(BasePlayer player, CuiElementContainer container, ShopItem item,
					int amount,
					string itemPrice,
					string btnActionTitle,
					string cmdInput = "",
					string cmdMinus = "",
					string cmdPlus = "",
					string cmdMax = "",
					string cmdAction = "",
					string cmdClose = "")
				{
					container.Add(
						Background.GetImage(DisplayType, ModalLayer, ModalLayer));

					container.Add(
						ModalPanel.GetImage(ModalLayer, ModalLayer + ".Main", ModalLayer + ".Main"));

					container.Add(ItemBackground.GetImage(ModalLayer + ".Main",
						ModalLayer + ".Main.Item.Background"));

					container.Add(item.GetImage(ItemIcon, ModalLayer + ".Main.Item.Background"));
					
					container.Add(ItemName.GetText(item.GetPublicTitle(player), ModalLayer + ".Main"));

					if (!string.IsNullOrWhiteSpace(item.Description))
					{
						container.Add(
							DescriptionTitle.GetText(_instance?.Msg(player, UIShopActionDescriptionTitle), ModalLayer + ".Main"));
						container.Add(
							ItemDescription.GetText(_instance?.Msg(player, item.Description), ModalLayer + ".Main"));
					}

					container.Add(AmountTitle.GetText(_instance?.Msg(player, UIShopActionAmountTitle), ModalLayer + ".Main"));

					container.Add(new CuiElement
					{
						Parent = ModalLayer + ".Main",
						Components =
						{
							new CuiInputFieldComponent
							{
								Text = $"{amount * item.Amount}",
								FontSize = AmountValue.FontSize,
								Font = AmountValue.IsBold
									? "robotocondensed-bold.ttf"
									: "robotocondensed-regular.ttf",
								Align = AmountValue.Align,
								Color = AmountValue.Color.Get,
								Command = cmdInput,
								NeedsKeyboard = true,
								HudMenuInput = true,
								CharsLimit = 5
							},
							AmountValue.GetRectTransform()
						}
					});

					container.AddRange(ButtonMinusAmount.GetButton(_instance?.Msg(player, MinusTitle),
						cmdMinus, ModalLayer + ".Main"));
					container.AddRange(ButtonPlusAmount.GetButton(_instance?.Msg(player, PlusTitle),
						cmdPlus, ModalLayer + ".Main"));
					container.AddRange(ButtonSetMaxAmount.GetButton(_instance?.Msg(player, TitleMax),
						cmdMax, ModalLayer + ".Main"));

					container.Add(Price.GetText(GetPlayerEconomy(player).GetPriceTitle(player, itemPrice), ModalLayer + ".Main"));

					container.AddRange(ButtonAction.GetButton(btnActionTitle, cmdAction, ModalLayer + ".Main", close: ModalLayer));

					container.AddRange(ButtonClose.GetButton(string.Empty,
						cmdClose, ModalLayer + ".Main", close: ModalLayer));
				}

				#endregion
			}

			public class ShopConfirmationModalUI
			{
				#region Fields

				[JsonProperty(PropertyName = "Display type (Overlay/Hud)")]
				public string DisplayType;

				[JsonProperty(PropertyName = "Background")]
				public ImageSettings Background;

				[JsonProperty(PropertyName = "Close on click?")]
				public bool CloseOnClick;

				[JsonProperty(PropertyName = "Title")]
				public TextSettings Title;

				[JsonProperty(PropertyName = "Confirm Button")]
				public ButtonSettings ButtonConfirm;

				[JsonProperty(PropertyName = "Cancel Button")]
				public ButtonSettings ButtonCancel;

				#endregion Fields

				#region Public Methods

				public void GetModal(BasePlayer player, 
					CuiElementContainer container, 
					(string title, string cmd) confirm, 
					(string title, string cmd) cancel)
				{
					container.Add(
						Background.GetImage(DisplayType, ModalLayer, ModalLayer));

					if (CloseOnClick)
						container.Add(new CuiElement()
						{
							Parent = ModalLayer,
							Components =
							{
								new CuiButtonComponent()
								{
									Color = "0 0 0 0",
									Close = ModalLayer,
									Command = cancel.cmd
								},
								new CuiRectTransformComponent()
							}
						});
					
					container.Add(Title.GetText(_instance.Msg(player, PurchaseConfirmation), ModalLayer));

					container.AddRange(ButtonConfirm.GetButton(confirm.title, confirm.cmd, ModalLayer, close: ModalLayer));
					
					container.AddRange(ButtonCancel.GetButton(cancel.title, cancel.cmd, ModalLayer, close: ModalLayer));
				}

				#endregion Public Methods
			}

			public class ShopItemDescriptionModalUI
			{
				#region Fields

				[JsonProperty(PropertyName = "Display type (Overlay/Hud)")]
				public string DisplayType;

				[JsonProperty(PropertyName = "Background")]
				public ImageSettings Background;

				[JsonProperty(PropertyName = "Close on click?")]
				public bool CloseOnClick;

				[JsonProperty(PropertyName = "Modal Panel")]
				public ImageSettings ModalPanel;

				[JsonProperty(PropertyName = "Title")]
				public TextSettings Title;

				[JsonProperty(PropertyName = "Description")]
				public TextSettings Description;

				[JsonProperty(PropertyName = "Close Button Icon")]
				public ButtonSettings ButtonClose;

				#endregion

				#region Public Methods

				public void GetModal(BasePlayer player, CuiElementContainer container, ShopItem item,
					string cmdClose = "")
				{
					container.Add(
						Background.GetImage(DisplayType, ModalLayer, ModalLayer));

					if (CloseOnClick)
						container.Add(new CuiElement()
						{
							Parent = ModalLayer,
							Components =
							{
								new CuiButtonComponent()
								{
									Color = "0 0 0 0",
									Close = ModalLayer,
									Command = cmdClose
								},
								new CuiRectTransformComponent()
							}
						});
					
					container.Add(
						ModalPanel.GetImage(ModalLayer, ModalLayer + ".Main", ModalLayer + ".Main"));
					
					container.Add(Title.GetText(_instance?.Msg(player, UIShopItemDescriptionTitle), ModalLayer + ".Main"));
					
					container.Add(Description.GetText(_instance?.Msg(player, item.Description), ModalLayer + ".Main"));

					container.AddRange(ButtonClose.GetButton(string.Empty, cmdClose, ModalLayer + ".Main", close: ModalLayer));
				}

				#endregion
			}
            
			#endregion

			#region Public Methods

			public void RegisterImages(Dictionary<string, string> imagesList)
			{
				RegisterImage(ShopItem?.Background?.Image, ref imagesList);

				RegisterImage(ShopItem?.Amount?.Background?.Image, ref imagesList);
				RegisterImage(ShopItem?.Favorite?.AddToFavorites?.Image, ref imagesList);
				RegisterImage(ShopItem?.Favorite?.RemoveFromFavorites?.Image, ref imagesList);
				RegisterImage(ShopItem?.Info?.Image, ref imagesList);
				RegisterImage(ShopItem?.Discount?.Background?.Image, ref imagesList);
				RegisterImage(ShopItem?.BuyButton?.Background.Image, ref imagesList);
				RegisterImage(ShopItem?.BuyButton?.Cooldown.Background.Image, ref imagesList);
				RegisterImage(ShopItem?.SellButton?.Background.Image, ref imagesList);
				RegisterImage(ShopItem?.SellButton?.Cooldown.Background.Image, ref imagesList);
				
				RegisterImage(ShopCategories?.Background.Image, ref imagesList);
				RegisterImage(ShopCategories?.CategoriesPanel.Image, ref imagesList);
				RegisterImage(ShopCategories?.BackButton.Image, ref imagesList);
				RegisterImage(ShopCategories?.NextButton.Image, ref imagesList);
				RegisterImage(ShopCategories?.Header.Background.Image, ref imagesList);
				RegisterImage(ShopCategories?.ShopCategory.SelectedCategory.Background.Image, ref imagesList);
				RegisterImage(ShopCategories?.ShopCategory.Category.Background.Image, ref imagesList);
				
				RegisterImage(ShopContent?.Background.Image, ref imagesList);
				RegisterImage(ShopContent?.Header.Background.Image, ref imagesList);
				RegisterImage(ShopContent?.Header.ButtonClose.Image, ref imagesList);
				RegisterImage(ShopContent?.Header.ButtonTransfer.Image, ref imagesList);
				RegisterImage(ShopContent?.Header.ButtonToggleEconomy.Image, ref imagesList);
				RegisterImage(ShopContent?.Header.Balance.Background.Image, ref imagesList);
				RegisterImage(ShopContent?.Content.Background.Image, ref imagesList);
				RegisterImage(ShopContent?.Content.ButtonBack.Image, ref imagesList);
				RegisterImage(ShopContent?.Content.ButtonNext.Image, ref imagesList);
				RegisterImage(ShopContent?.Content.ButtonAddItem.Image, ref imagesList);
				RegisterImage(ShopContent?.Content.Search.Background.Image, ref imagesList);
				
				RegisterImage(ShopBasket?.Background.Image, ref imagesList);
				RegisterImage(ShopBasket?.Header.Background.Image, ref imagesList);
				RegisterImage(ShopBasket?.Header.BackButton.Image, ref imagesList);
				RegisterImage(ShopBasket?.Header.NextButton.Image, ref imagesList);
				RegisterImage(ShopBasket?.Content.Background.Image, ref imagesList);
				RegisterImage(ShopBasket?.Footer.Background.Image, ref imagesList);
				RegisterImage(ShopBasket?.Footer.BuyButtonWhenBuyAgain.Image, ref imagesList);
				RegisterImage(ShopBasket?.Footer.BuyButton.Image, ref imagesList);
				RegisterImage(ShopBasket?.Footer.BuyAgainButton.Image, ref imagesList);
				RegisterImage(ShopBasket?.Footer.BuyAgainButton.Image, ref imagesList);
				
				RegisterImage(ShopBasket?.BasketItem.Background.Image, ref imagesList);
				RegisterImage(ShopBasket?.BasketItem.ImageBackground.Image, ref imagesList);
				RegisterImage(ShopBasket?.BasketItem.ButtonRemoveItem.Image, ref imagesList);
				RegisterImage(ShopBasket?.BasketItem.ButtonPlusAmount.Image, ref imagesList);
				RegisterImage(ShopBasket?.BasketItem.ButtonMinusAmount.Image, ref imagesList);

				RegisterImage(ShopItem?.AdminPanel.Background.Image, ref imagesList);
				RegisterImage(ShopItem?.AdminPanel.ButtonEdit.Image, ref imagesList);
				RegisterImage(ShopItem?.AdminPanel.ButtonMoveLeft.Image, ref imagesList);
				RegisterImage(ShopItem?.AdminPanel.ButtonMoveRight.Image, ref imagesList);

				RegisterImage(ShopCategories?.ShopCategory.AdminPanel.Background.Image, ref imagesList);
				RegisterImage(ShopCategories?.ShopCategory.AdminPanel.ButtonEdit.Image, ref imagesList);
				RegisterImage(ShopCategories?.ShopCategory.AdminPanel.ButtonMoveDown.Image, ref imagesList);
				RegisterImage(ShopCategories?.ShopCategory.AdminPanel.ButtonMoveUp.Image, ref imagesList);
			}

			#endregion
		}
		
		#endregion UI Data
		
		#region Helpers

		#region Players

		private static IEnumerator StartOnPlayers(string[] players, Action<string> callback = null)
		{
			for (var i = 0; i < players.Length; i++)
			{
				callback?.Invoke(players[i]);

				if (i % 10 == 0)
					yield return CoroutineEx.waitForFixedUpdate;
			}
		}

		private static IEnumerator StartOnPlayers(List<ulong> players, Action<ulong> callback = null)
		{
			for (var i = 0; i < players.Count; i++)
			{
				callback?.Invoke(players[i]);

				if (i % 10 == 0)
					yield return CoroutineEx.waitForFixedUpdate;
			}
		}

		#endregion

		#region All Players Data

		private IEnumerator SaveCachedPlayersData()
		{
			var players = _usersData.Keys.ToList();
			if (players.Count > 0)
				yield return StartOnPlayers(players, PlayerData.Save);
			else
				yield return null;
		}

		private IEnumerator UnloadOfflinePlayersData()
		{
			foreach (var userID in _usersData.Keys.ToList())
			{
				if (!BasePlayer.TryFindByID(userID, out _))
					PlayerData.Unload(userID);
			}

			yield return null;
		}

		#endregion

		#region Wipe Players

		private Coroutine _wipePlayers;

		private void StartWipePlayers(bool carts, bool cooldowns, bool limits)
		{
			try
			{
				var players = PlayerData.GetFiles();
				if (players is not {Length: > 0})
				{
					PrintError("[On Server Wipe] in wipe players, no players found!");
					return;
				}
				
				_wipePlayers =
					Global.Runner.StartCoroutine(StartOnPlayers(players,
						userID => PlayerData.DoWipe(userID, carts, cooldowns, limits)));

				_usersData?.Clear();

				Puts("You have wiped player data!");
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

		#region Limits

		private void UseLimit(BasePlayer player, ShopItem item, bool buy, int amount, bool daily = false)
		{
			PlayerData.GetOrCreate(player.userID).Limits.AddItem(item, buy, amount, daily);
		}

		private int GetLimit(BasePlayer player, ShopItem item, bool buy, bool daily = false)
		{
			var hasLimit = item.GetLimit(player, buy, daily);
			if (hasLimit == 0)
				return 1;

			var used = PlayerData.GetOrCreate(player.userID).Limits.GetLimit(item, buy, daily);
			return hasLimit - used;
		}

		private static bool HasLimit(BasePlayer player, ShopItem item, bool buy, out int leftAmount, bool daily = false)
		{
			var hasLimit = item.GetLimit(player, buy, daily);
			if (hasLimit == 0)
			{
				leftAmount = 0;
				return false;
			}

			var used = PlayerData.GetOrCreate(player.userID).Limits.GetLimit(item, buy, daily);
			leftAmount = hasLimit - used;
			return true;
		}

		#endregion

		#region Cooldowns

		private PlayerData.CooldownData GetCooldown(ulong player)
		{
			return PlayerData.GetOrCreate(player).Cooldowns;
		}

		private PlayerData.CooldownData.ItemData GetCooldown(ulong player, ShopItem item)
		{
			return GetCooldown(player)?.GetCooldown(item);
		}

		private int GetCooldownTime(ulong player, ShopItem item, bool buy)
		{
			return GetCooldown(player)?.GetCooldownTime(player.ToString(), item, buy) ?? -1;
		}

		private bool HasCooldown(ulong player, ShopItem item, bool buy)
		{
			return GetCooldown(player)?.HasCooldown(item, buy) ?? false;
		}

		private void SetCooldown(BasePlayer player, ShopItem item, bool buy, bool needUpdate = false)
		{
			if (item.GetCooldown(player.UserIDString, buy) <= 0) return;

			GetCooldown(player.userID)?.SetCooldown(item, buy);

			if (needUpdate)
			{
				if (_itemsToUpdate.ContainsKey(player.userID))
				{
					if (!_itemsToUpdate[player.userID].Contains(item))
						_itemsToUpdate[player.userID].Add(item);
				}
				else
				{
					_itemsToUpdate.Add(player.userID, new List<ShopItem> {item});
				}

				CheckUpdateController();
			}
		}

		private void RemoveCooldown(ulong player, ShopItem item)
		{
			var data = PlayerData.GetOrLoad(player);
			if (data == null || data.Cooldowns.Items.Count == 0) return;

			var openedShop = GetShop(player);
			openedShop?.RemoveItemToUpdate(item);

			data.Cooldowns.RemoveCooldown(item);

			if (data.Cooldowns.Items.Count == 0)
				openedShop?.ClearItemsToUpdate();

			CheckUpdateController();
		}

		#endregion

		#region Economy

		private static EconomyEntry GetPlayerEconomy(BasePlayer player)
		{
			return PlayerData.GetOrCreate(player.userID)?.GetEconomy();
		}

		private static void SelectPlayerEconomy(BasePlayer player, int id)
		{
			PlayerData.GetOrCreate(player.userID)?.SelectEconomy(id);
		}

		#endregion

		#endregion

		#endregion

		#region Hooks

		private void Init()
		{
			_instance = this;
			
			LoadItemsData();
			
			LoadUIData();
			
			RegisterPermissions();

			CheckOnDuplicates();

			LoadEconomics();

#if TESTING
			StopwatchWrapper.OnComplete = DebugMessage;
#endif
		}

		private void OnServerInitialized()
		{
			LoadNPCs();

			LoadImages();

			LoadItems();

			CacheImages();

			LoadPlayers();

			LoadCustomVMs();

			RegisterCommands();

			CheckUpdateController();
			
			CheckInitializationStatus();
		}

		private void Unload()
		{
			try
			{
				foreach (var player in BasePlayer.activePlayerList)
				{
					CuiHelper.DestroyUi(player, Layer);
					CuiHelper.DestroyUi(player, ModalLayer);
					CuiHelper.DestroyUi(player, EditingLayer);

					OnPlayerDisconnected(player);
				}

				StopWipePlayers();

				StopConvertFrom1d2d26();
			}
			finally
			{
				_config = null;

				_instance = null;
			}
		}

		private void OnPlayerConnected(BasePlayer player)
		{
			if (player == null || player.IsNpc) return;

			GetAvatar(player.userID,
				avatar => ImageLibrary?.Call("AddImage", avatar, $"avatar_{player.UserIDString}"));

			if (_config.LoginImages)
				_coroutines[player.userID] = ServerMgr.Instance.StartCoroutine(LoadImages(player));
		}

		private void OnPlayerDisconnected(BasePlayer player)
		{
			if (player == null) return;

			PlayerData.SaveAndUnload(player.userID);

			CloseShopUI(player);
			
			if (_coroutines.TryGetValue(player.userID, out var coroutine) && coroutine != null)
				ServerMgr.Instance.StopCoroutine(coroutine);
		}

		private void OnNewSave()
		{
			if (_config.Wipe.Players || _config.Wipe.Cooldown || _config.Wipe.Limits)
				StartWipePlayers(_config.Wipe.Players, _config.Wipe.Cooldown, _config.Wipe.Limits);
		}

		#region Server Panel

		private void OnServerPanelCategoryPage(BasePlayer player, int category, int page)
		{
			CloseShopUI(player);
		}

		private void OnServerPanelClosed(BasePlayer player)
		{
			CloseShopUI(player);
		}

		#endregion

		#region Image Library

#if !CARBON
		private void OnPluginLoaded(Plugin plugin)
		{
			switch (plugin.Name)
			{
				case "ImageLibrary":
					timer.In(1, LoadImages);
					break;
				case "ServerPanel":
					_enabledServerPanel = true;
					break;
			}
		}

		private void OnPluginUnloaded(Plugin plugin)
		{
			switch (plugin.Name)
			{
				case "ImageLibrary":
					_enabledImageLibrary = false;
					break;
				case "ServerPanel":
					_enabledServerPanel = false;
					break;
			}
		}
#endif

		#endregion

		#region Vending Machine

		private object CanLootEntity(BasePlayer player, VendingMachine vendingMachine)
		{
			if (player == null || vendingMachine == null)
				return null;

			if (_config.CustomVending.TryGetValue(vendingMachine.net.ID.Value, out var customVending))
			{
				if (!string.IsNullOrEmpty(customVending.Permission) && !player.HasPermission(customVending.Permission))
				{
					_config?.Notifications?.ShowNotify(player, NoPermission, 1);
					return false;
				}

				_openedCustomVending[player.userID] = customVending;

				ShowShopUI(player, first: true);
				return false;
			}

			return null;
		}

		#endregion

		#region NPC

		private void OnUseNPC(BasePlayer npc, BasePlayer player)
		{
			if (npc == null || player == null) return;

			if (!_config.NPCs.TryGetValue(npc.UserIDString, out var npcShop) || npcShop == null) return;

			if (!string.IsNullOrEmpty(npcShop.Permission) && !player.HasPermission(npcShop.Permission))
			{
				_config?.Notifications?.ShowNotify(player, NoPermission, 1);
				return;
			}

			_openedShopsNPC[player.userID] = npcShop;

			ShowShopUI(player, first: true);
		}

		#endregion

		#endregion

		#region Commands

		[ConsoleCommand("openshopUI")]
		private void OpenShopUI(ConsoleSystem.Arg arg)
		{
			var player = arg?.Player();
			if (player == null) return;

			if (_openSHOP.ContainsKey(player.userID))
			{
				CuiHelper.DestroyUi(player, Layer);

				CloseShopUI(player);
			}
			else
			{
				ShowShopUI(player, first: true);
			}
		}

		private void CmdShopOpen(IPlayer cov, string command, string[] args)
		{
			var player = cov?.Object as BasePlayer;
			if (player == null) return;

			if (_initializedStatus.status is false)
			{				
				if (_initializedStatus.message == null)
				{
					_config?.Notifications?.ShowNotify(player, UIMsgShopInInitialization, 1);
					return;
				}
				
				_config?.Notifications?.ShowNotify(player, NoILError, 1);
				
				PrintError(ConvertInitializedStatus());
				return;
			}
            
			if (_config.UseDuels && InDuel(player))
			{
				_config?.Notifications?.ShowNotify(player, NoUseDuel, 1);
				return;
			}

			ShowShopUI(player, first: true);
		}

		private void CmdSetCustomVM(IPlayer cov, string command, string[] args)
		{
			var player = cov?.Object as BasePlayer;
			if (player == null) return;

			if (!player.HasPermission(PermSetVM))
			{
				_config?.Notifications?.ShowNotify(player, NoPermission, 1);
				return;
			}

			if (args.Length == 0)
			{
				_config?.Notifications?.ShowNotify(player, ErrorSyntax, 1, $"{command} [categories: cat1 cat2 ...]");
				return;
			}

			var categories = args.ToList();
			categories.RemoveAll(cat => !_itemsData.Shop.Exists(confCat => confCat.GetTitle(player) == cat));
			if (categories.Count == 0)
			{
				_config?.Notifications?.ShowNotify(player, VMNotFoundCategories, 1);
				return;
			}

			var workbench = GetLookVM(player);
			if (workbench == null)
			{
				_config?.Notifications?.ShowNotify(player, VMNotFound, 1);
				return;
			}

			if (_config.CustomVending.ContainsKey(workbench.net.ID.Value))
			{
				_config?.Notifications?.ShowNotify(player, VMExists, 1);
				return;
			}

			var conf = new CustomVendingEntry
			{
				Categories = categories
			};

			_config.CustomVending[workbench.net.ID.Value] = conf;

			SaveConfig();

			_config?.Notifications?.ShowNotify(player, VMInstalled, 0);

			Subscribe(nameof(CanLootEntity));
		}

		private void CmdSetShopNPC(IPlayer cov, string command, string[] args)
		{
			var player = cov?.Object as BasePlayer;
			if (player == null) return;

			if (!player.HasPermission(PermSetNPC))
			{
				_config?.Notifications?.ShowNotify(player, NoPermission, 1);
				return;
			}

			if (args.Length == 0)
			{
				_config?.Notifications?.ShowNotify(player, ErrorSyntax, 1, $"{command} [categories: cat1 cat2 ...]");
				return;
			}

			var categories = args.ToList();

			for (var i = 0; i < categories.Count; i++)
				categories[i] = categories[i].TrimEnd(',');

			categories.RemoveAll(cat => !_itemsData.Shop.Exists(confCat => confCat.GetTitle(player) == cat));
			if (categories.Count == 0)
			{
				_config?.Notifications?.ShowNotify(player, VMNotFoundCategories, 1);
				return;
			}

			var npc = GetLookNPC(player);
			if (npc == null)
			{
				_config?.Notifications?.ShowNotify(player, NPCNotFound, 1);
				return;
			}

			if (_config.NPCs.ContainsKey(npc.UserIDString))
			{
				_config?.Notifications?.ShowNotify(player, VMExists, 1);
				return;
			}

			var conf = new NPCShop
			{
				Categories = categories,
				BotID = npc.UserIDString
			};

			_config.NPCs[npc.UserIDString] = conf;

			SaveConfig();

			_config?.Notifications?.ShowNotify(player, NPCInstalled, 0);
		}

		[ConsoleCommand(CMD_Main_Console)]
		private void CmdConsoleShop(ConsoleSystem.Arg arg)
		{
			var player = arg?.Player();
			if (player == null || !arg.HasArgs()) return;

#if TESTING
			try
			{
#endif
				switch (arg.Args[0])
				{
					case "closeui":
					{
						CloseShopUI(player);
						break;
					}

					case "shop_search_input":
					{
						var search = string.Empty;
						if (arg.HasArgs(2)) search = string.Join(" ", arg.Args.Skip(1));

						var shop = GetShop(player);
						shop.OnChangeSearch(search, 0);
						
						UpdateUI(player, container => {
							ShopContentUI(player, container);
						});
						break;
					}

					case "shop_search_page":
					{
						if (!arg.HasArgs(2)) return;

						var searchPage = arg.GetInt(1);
						
						var shop = GetShop(player);
						shop.OnChangeSearch(shop.search, searchPage);
						
						UpdateUI(player, container => {
							ShopContentUI(player, container);
						});
						break;
					}

					case "shop_page":
					{
						if (!arg.HasArgs(2)) return;

						var shopPage = arg.GetInt(1);
						
						var shop = GetShop(player);
						shop.OnChangeShopPage(shopPage);
						
						UpdateUI(player, container => 
						{
							ShopContentUI(player, container);
						});
						break;
					}

					case "main_page":
					{
						if (!arg.HasArgs(3) || 
							!int.TryParse(arg.Args[1], out var categoryPage) ||
						    !int.TryParse(arg.Args[2], out var targetShopPage)) return;

						var search = string.Empty;
						if (arg.HasArgs(4)) search = string.Join(" ", arg.Args.Skip(3));

						if (string.IsNullOrEmpty(search) && categoryPage == -1)
							categoryPage = 0;

						var shop = GetShop(player);
						shop.OnChangeCategory(categoryPage, targetShopPage);

						UpdateUI(player, container =>
						{
							ShopContentUI(player, container);
						});
						break;
					}

					case "buyitem":
					{
						if (!arg.HasArgs(2) ||
						    !int.TryParse(arg.Args[1], out var id)) return;

						if (!TryFindItemById(id, out var shopItem))
							return;

						var playerCart = GetPlayerCart(player.userID);
						if (playerCart == null) return;

						playerCart.AddCartItem(shopItem, player);

						var cooldownTime = GetCooldownTime(player.userID, shopItem, true);
						if (cooldownTime > 0)
						{
							_config?.Notifications?.ShowNotify(player, BuyCooldownMessage, 1, shopItem.GetPublicTitle(player),
								FormatShortTime(player, TimeSpan.FromSeconds(cooldownTime)));
							return;
						}

						UpdateUI(player, container => { ShopBasketUI(container, player); });
						break;
					}

					case "categories_change_local_page":
					{
						if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out var targetCategoriesPage)) return;

						var shop = GetShop(player);
						shop?.Update();
						shop?.OnChangeCategoriesPage(targetCategoriesPage);

						UpdateUI(player, container =>
						{
							ShopCategoriesUI(container, player);
						});
						break;
					}

					case "categories_change":
					{
						if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out var targetCategoryIndex)) return;

						var shop = GetShop(player);

						shop.OnChangeCategory(targetCategoryIndex, 0);

						UpdateUI(player, container =>
						{
							CategoriesListUI(container, player);
                            
							ShopContentUI(player, container);
							
							ShopHeaderUI(player, container);
						});
						break;
					}

					case "cart_page":
					{
						if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out var page)) return;

						GetShop(player)?.OnChangeBasketPage(page);

						UpdateUI(player, container => { ShopBasketUI(container, player); });
						break;
					}

					case "cart_item_remove":
					{
						if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out var id)) return;

						if (!TryFindItemById(id, out var shopItem))
							return;

						var playerCart = GetPlayerCart(player.userID);
						if (playerCart == null) return;

						playerCart.RemoveCartItem(shopItem);

						UpdateUI(player, container => { ShopBasketUI( container, player); });
						break;
					}

					case "cart_item_change":
					{
						if (!arg.HasArgs(3) ||
						    !int.TryParse(arg.Args[1], out var index) ||
						    !int.TryParse(arg.Args[2], out var itemID) ||
						    !int.TryParse(arg.Args[3], out var amount)) return;

						if (!TryFindItemById(itemID, out var shopItem))
							return;

						var playerCart = GetPlayerCart(player.userID);
						if (playerCart == null) return;

						if (shopItem.BuyMaxAmount > 0 && amount > shopItem.BuyMaxAmount)
							amount = shopItem.BuyMaxAmount;

						playerCart.ChangeAmountItem(player, shopItem, amount);

						UpdateUI(player, container =>
						{
							if (amount > 0)
							{
								ShopCartItemUI(player, container, index, shopItem, amount);
								
								UpdateShopCartFooterUI(container, player, playerCart);
							}
							else
								ShopBasketUI(container, player);
						}); 
						break;
					}

					case "cart_try_buyitems":
					{
						UpdateUI(player, container => {
							GetShop(player)?.GetUI()?.ShopConfirmationModal?.GetModal(player, container, 
							(Msg(player, BuyTitle), $"{CMD_Main_Console} cart_buyitems"), 
							(Msg(player, CancelTitle), string.Empty));
						});
						// AcceptBuy(player);
						break;
					}

					case "cart_buyitems":
					{
						TryBuyItems(player);
						break;
					}

					case "fastbuyitem":
					{
						if (!arg.HasArgs(3) ||
						    !int.TryParse(arg.Args[1], out var itemId) ||
						    !int.TryParse(arg.Args[2], out var amount)) return;

						var item = FindItemById(itemId);
						if (item == null) return;

						if (_config.BlockNoEscape)
							if (NoEscape_IsBlocked(player))
							{
								ErrorUi(player, Msg(player, BuyRaidBlocked));
								return;
							}

						if (_config.WipeCooldown)
						{
							var timeLeft = Mathf.RoundToInt(_config.WipeCooldownTimer - GetSecondsFromWipe());
							if (timeLeft > 0)
							{
								ErrorUi(player,
									Msg(player, BuyWipeCooldown,
										FormatShortTime(timeLeft)));
								return;
							}
						}

						if (_config.RespawnCooldown)
						{
							var timeLeft = Mathf.RoundToInt(_config.RespawnCooldownTimer - player.TimeAlive());
							if (timeLeft > 0)
							{
								ErrorUi(player,
									Msg(player, BuyRespawnCooldown,
										FormatShortTime(timeLeft)));
								return;
							}
						}

						if (item.Type == ItemType.Item)
						{
							var totalAmount = item.GetStacks(amount).Count;

							var slots = player.inventory.containerBelt.capacity -
							            player.inventory.containerBelt.itemList.Count +
							            (player.inventory.containerMain.capacity -
							             player.inventory.containerMain.itemList.Count);
							if (slots < totalAmount)
							{
								ErrorUi(player, Msg(player, NotEnoughSpace));
								return;
							}
						}

						var limit = GetLimit(player, item, true);
						if (limit <= 0)
						{
							ErrorUi(player, Msg(player, BuyLimitReached, item.GetPublicTitle(player)));
							return;
						}
						
						var selectedEconomy = API_GetShopPlayerSelectedEconomy(player.userID);
						
						var price = item.GetPrice(player, selectedEconomy) * amount;
						var playerEconomy = GetPlayerEconomy(player);
						if (!player.HasPermission(PermFreeBypass) &&
						    !playerEconomy.RemoveBalance(player, price))
						{
							ErrorUi(player, Msg(player, NotMoney));
							return;
						}

						item.Get(player, amount);

						SetCooldown(player, item, true);
						UseLimit(player, item, true, amount);
						UseLimit(player, item, true, amount, true);

						Log(LogType.Buy, LogBuyItems, player.displayName, player.UserIDString,
							playerEconomy.GetPriceTitle(player, price.ToString(_config.Formatting.BuyPriceFormat)), item.ToString());

						UpdateUI(player, container =>
						{
							BalanceUi(container, player);

							ItemUI(player, item, container);
						});

						_config?.Notifications?.ShowNotify(player, ReceivedItems, 0);
						break;
					}

					case "try_buy_item":
					{
						if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out var itemId)) return;

						var item = FindItemById(itemId);
						if (item == null) return;
						
						var amount = 1;
						if (arg.HasArgs(3))
						{
							if (arg.GetString(2) == "all")
								amount = Mathf.FloorToInt((float) (GetPlayerEconomy(player).ShowBalance(player) /
								                                   item.GetPrice(player, API_GetShopPlayerSelectedEconomy(player.userID))));
							else
								amount = arg.GetInt(2);

							if (amount < 1) return;
						}

						var maxAmount = item.BuyMaxAmount;
						if (maxAmount > 0) amount = Mathf.Min(amount, maxAmount);

						ModalShopItemActionUI(player, item, true, amount);
						break;
					}

					case "try_sell_item":
					{
						if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out var itemId))
							return;

						var item = FindItemById(itemId);
						if (item == null) return;

						var amount = 1;
						if (arg.HasArgs(3))
						{
							if (arg.GetString(2) == "all")
								amount = Mathf.FloorToInt(ItemCount(PlayerItems(player), item.ShortName, item.Skin) /
								                          (float) item.Amount);
							else
								amount = arg.GetInt(2);

							if (amount < 1) return;
						}

						amount = Mathf.Max(1,
							Mathf.Min(amount,
								Mathf.CeilToInt(ItemCount(PlayerItems(player), item.ShortName, item.Skin) /
								                (float) item.Amount)));

						var maxAmount = item.SellMaxAmount;
						if (maxAmount > 0) amount = Mathf.Min(amount, maxAmount);

						ModalShopItemActionUI(player, item, false, amount);
						break;
					}
					
					case "sellitem":
					{
						if (!arg.HasArgs(3) ||
						    !int.TryParse(arg.Args[1], out var itemId) ||
						    !int.TryParse(arg.Args[2], out var amount)) return;

						var item = FindItemById(itemId);
						if (item == null) return;

						var cooldownTime = GetCooldownTime(player.userID, item, false);
						if (cooldownTime > 0)
						{
							ErrorUi(player, Msg(player, SellCooldownMessage));
							return;
						}

						if (_config.BlockNoEscape && NoEscape != null)
						{
							if (Convert.ToBoolean(NoEscape?.Call("IsBlocked", player)))
							{
								ErrorUi(player, Msg(player, SellRaidBlocked));
								return;
							}
						}

						if (_config.WipeCooldown)
						{
							var timeLeft = Mathf.RoundToInt(_config.WipeCooldownTimer - GetSecondsFromWipe());
							if (timeLeft > 0)
							{
								ErrorUi(player,
									Msg(player, SellWipeCooldown,
										FormatShortTime(timeLeft)));
								return;
							}
						}

						if (_config.RespawnCooldown)
						{
							var timeLeft = Mathf.RoundToInt(_config.RespawnCooldownTimer - player.TimeAlive());
							if (timeLeft > 0)
							{
								ErrorUi(player,
									Msg(player, SellRespawnCooldown,
										FormatShortTime(timeLeft)));
								return;
							}
						}

						var limit = GetLimit(player, item, false);
						if (limit <= 0)
						{
							ErrorUi(player, Msg(player, SellLimitReached, item.GetPublicTitle(player)));
							return;
						}

						limit = GetLimit(player, item, false, true);
						if (limit <= 0)
						{
							ErrorUi(player, Msg(player, DailySellLimitReached, item.GetPublicTitle(player)));
							return;
						}

						if (_config.BlockedSkins.TryGetValue(item.ShortName, out var blockedSkins))
							if (blockedSkins.Contains(item.Skin))
							{
								ErrorUi(player, Msg(player, SkinBlocked));
								return;
							}

						var totalAmount = item.Amount * amount;

						var playerItems = PlayerItems(player);

						if (ItemCount(playerItems, item) < totalAmount)
						{
							ErrorUi(player, Msg(player, NotEnough));
							return;
						}
						
						var selectedEconomy = API_GetShopPlayerSelectedEconomy(player.userID);

						var playerEconomy = GetPlayerEconomy(player);
						Log(LogType.Sell, LogSellItem, player.displayName, player.UserIDString,
							playerEconomy.GetPriceTitle(player, (item.GetSellPrice(player, selectedEconomy) * amount).ToString(_config.Formatting.SellPriceFormat)), item.ToString());

						Take(playerItems, item, totalAmount);

						playerEconomy.AddBalance(player, item.GetSellPrice(player, selectedEconomy) * amount);

						SetCooldown(player, item, false, true);
						UseLimit(player, item, false, amount);
						UseLimit(player, item, false, amount, true);

						GetShop(player)?.AddItemToUpdate(item);

						CheckUpdateController();

						UpdateUI(player, container =>
						{
							ShopItemButtonsUI(player, ref container, item, selectedEconomy);

							BalanceUi(container, player);
						});

						_config?.Notifications?.ShowNotify(player, SellNotify, 0, totalAmount, item.GetPublicTitle(player));
						break;
					}

					case "startedititem":
					{
						if (!IsAdmin(player) || !arg.HasArgs(2) ||
						    !int.TryParse(arg.Args[1], out var itemID)) return;

						_itemEditing.Remove(player.userID);

						EditUi(player, itemID, true);
						break;
					}

					case "edititem":
					{
						if (!IsAdmin(player) || !arg.HasArgs(3)) return;

						var key = arg.Args[1];
						var value = arg.Args[2];

						if (_itemEditing.ContainsKey(player.userID) && _itemEditing[player.userID].ContainsKey(key))
						{
							object newValue = null;

							switch (key)
							{
								case "Type":
								{
									if (Enum.TryParse(value, out ItemType type))
										newValue = type;
									break;
								}

								case "Plugin_Hook":
								case "Plugin_Name":
								case "Image":
								case "Command":
								case "Title":
								case "DisplayName":
								{
									newValue = string.Join(" ", arg.Args.Skip(2));
									break;
								}

								case "ShortName":
								{
									newValue = value;
									break;
								}

								case "Plugin_Amount":
								case "Amount":
								{
									if (int.TryParse(value, out var Value))
										newValue = Value;
									break;
								}

								case "SellPrice":
								case "Price":
								{
									if (value == "auto")
									{
										var shortName = _itemEditing[player.userID]["ShortName"].ToString();
										if (string.IsNullOrEmpty(shortName)) return;

										var amount = Convert.ToInt32(_itemEditing[player.userID]["Amount"]);
										if (amount <= 0) return;

										var def = ItemManager.FindItemDefinition(shortName);
										if (def == null) return;

										newValue = GetItemCost(def) * amount;
										break;
									}

									if (double.TryParse(value, out var Value))
										newValue = Value;
									break;
								}

								case "Skin":
								{
									if (ulong.TryParse(value, out var Value))
										newValue = Value;
									break;
								}

								case "Buying":
								case "Selling":
								case "Blueprint":
								{
									if (bool.TryParse(value, out var Value))
										newValue = Value;
									break;
								}
							}

							if (_itemEditing[player.userID][key].Equals(newValue))
								return;

							_itemEditing[player.userID][key] = newValue;
						}

						EditUi(player);
						break;
					}

					case "closeediting":
					{
						_itemEditing.Remove(player.userID);
						break;
					}

					case "saveitem":
					{
						if (!IsAdmin(player)) return;

						var edit = _itemEditing[player.userID];
						if (edit == null) return;

						var shop = GetShop(player);
						
						var newItem = new ShopItem(edit);

						var generated = Convert.ToBoolean(edit["Generated"]);
						if (generated)
						{
							var shopCategory = shop.GetSelectedShopCategory();
							shopCategory.Items.Add(newItem);
							shopCategory.SortItems();
						}
						else
						{
							var shopItem = FindItemById(Convert.ToInt32(edit["ID"]));
							if (shopItem != null)
							{
								shopItem.Type = newItem.Type;
								shopItem.ID = newItem.ID;
								shopItem.Image = newItem.Image;
								shopItem.Title = newItem.Title;
								shopItem.Command = newItem.Command;
								shopItem.Plugin = newItem.Plugin;
								shopItem.DisplayName = newItem.DisplayName;
								shopItem.Skin = newItem.Skin;
								shopItem.Blueprint = newItem.Blueprint;
								shopItem.CanBuy = newItem.CanBuy;
								shopItem.CanSell = newItem.CanSell;
								shopItem.Amount = newItem.Amount;
								shopItem.Price = newItem.Price;
								shopItem.SellPrice = newItem.SellPrice;
							}
						}

						if (!string.IsNullOrEmpty(newItem.Image))
							AddImage(newItem.Image, newItem.Image);

						_itemEditing.Remove(player.userID);

						SaveItemsData();

						LoadItems();

						if (generated)
						{
							shop.Update();
                            
							var maxShopPage = GetLastShopContentPage(player);

							shop.OnChangeShopPage(maxShopPage);
						}
                        
						UpdateUI(player, container =>
						{
							ShopContentUI(player, container);
						});
						break;
					}

					case "removeitem":
					{
						if (!IsAdmin(player)) return;

						var editing = _itemEditing[player.userID];
						if (editing == null) return;

						if (!TryFindItemById(Convert.ToInt32(editing["ID"]), out var shopItem))
							return;

						_itemsData.Shop.ForEach(shopCategory =>
						{
							if (shopCategory.Items.Remove(shopItem))
								shopCategory.SortItems();
						});

						_itemEditing.Remove(player.userID);

						SaveItemsData();

						var shop = GetShop(player);
                        
						var targetShopPage = Mathf.Min(shop.currentShopPage, GetLastShopContentPage(player));

						shop.OnChangeShopPage(targetShopPage);
						
						UpdateUI(player, container =>
						{
							ShopContentUI(player, container);
						});
						break;
					}

					case "selectitem":
					{
						if (!IsAdmin(player) || !arg.HasArgs(2) || !int.TryParse(arg.Args[1], out var category)) return;

						var cat = string.Empty;
						if (arg.HasArgs(3))
							cat = arg.Args[2];

						var page = 0;
						if (arg.HasArgs(4))
							int.TryParse(arg.Args[3], out page);

						var input = string.Empty;
						if (arg.HasArgs(5))
							input = string.Join(" ", arg.Args.Skip(4));

						SelectItem(player, category, cat, page, input);
						break;
					}

					case "takeitem":
					{
						if (!IsAdmin(player) || !arg.HasArgs(4) || !int.TryParse(arg.Args[1], out var category) ||
						    !int.TryParse(arg.Args[2], out var page)) return;

						_itemEditing[player.userID]["ShortName"] = arg.Args[3];

						EditUi(player);
						break;
					}

					case "item_info":
					{
						if (!arg.HasArgs(2) ||
						    !int.TryParse(arg.Args[1], out var itemId)) return;

						var item = FindItemById(itemId);
						if (item == null) return;

						var selectedEconomy = API_GetShopPlayerSelectedEconomy(player.userID);
						
						UpdateUI(player,container => 
						{
							GetShop(player)?.GetUI()?.ShopItemDescriptionModal.GetModal(player, container, item);
						});
						break;
					}

					case "transfer_start":
					{
						SelectTransferPlayerUI(player);
						break;
					}

					case "transfer_page":
					{
						var newTransferPage = arg.GetInt(1);

						SelectTransferPlayerUI(player, newTransferPage);
						break;
					}

					case "transfer_set_target":
					{
						if (!arg.HasArgs(2) || !ulong.TryParse(arg.Args[1], out var targetId)) return;

						TransferUi(player, targetId);
						break;
					}

					case "transfer_set_amount":
					{
						if (!arg.HasArgs(2) || !ulong.TryParse(arg.Args[1], out var targetId)) return;

						var amount = arg.GetFloat(2);

						amount = Mathf.Max(amount, 0);

						TransferUi(player, targetId, amount);
						break;
					}

					case "transfer_send":
					{
						if (!arg.HasArgs(3) || 
							!ulong.TryParse(arg.Args[1], out var targetId) ||
						    !float.TryParse(arg.Args[2], out var amount)) return;

						if (amount > 0)
						{
							var targetPlayer = BasePlayer.FindAwakeOrSleeping(targetId.ToString());
							if (targetPlayer == null)
							{
								ErrorUi(player, Msg(player, PlayerNotFound));
								return;
							}

							var selectedEconomy = GetPlayerEconomy(player);
							if (!selectedEconomy.Transfer(player, targetPlayer, amount))
							{
								ErrorUi(player, Msg(player, NotMoney));
								return;
							}

							_config?.Notifications?.ShowNotify(player, SuccessfulTransfer, 0, selectedEconomy.GetPriceTitle(player, amount.ToString(_config.Formatting.BuyPriceFormat)), targetPlayer.displayName);
						}

						break;
					}

					case "economy_try_change":
					{
						if (!arg.HasArgs(2) || 
						    !bool.TryParse(arg.Args[1], out var selected))
							return;
						
						UpdateUI(player, container =>
						{
							ShowChoiceEconomyUI(player, container, selected);
						});
						break;
					}

					case "economy_set":
					{
						if (!arg.HasArgs(2) || 
						    !int.TryParse(arg.Args[1], out var economyID))
							return;

						SelectPlayerEconomy(player, economyID);

						UpdateUI(player,
							container =>
							{
								ShopContentUI(player, container);
							
								ShopBasketUI(container, player);
															
								ShopHeaderUI(player, container);
                            });
						break;
					}

					case "start_edit_category":
					{
						if (!IsAdmin(player) || !arg.HasArgs(2)
						                     || !int.TryParse(arg.Args[1], out var categoryID)) return;

						var editFields = _categoryEditing[player.userID] = new Dictionary<string, object>();

						if (categoryID == -1)
						{
							editFields["generated"] = true;
							editFields["item"] = new ShopCategory
							{
								Enabled = false,
								CategoryType = ShopCategory.Type.None,
								Title = string.Empty,
								Permission = string.Empty,
								SortType = Configuration.SortType.None,
								Items = new List<ShopItem>(),
								Localization = new Localization
								{
									Enabled = false,
									Messages = new Dictionary<string, string>
									{
										["en"] = string.Empty,
										["fr"] = string.Empty
									}
								}
							};
						}
						else
						{
							var category = FindCategoryById(categoryID);
							if (category == null) return;

							editFields["generated"] = false;

							editFields["item"] = category.Clone();
						}

						EditCategoryUI(player, true);
						break;
					}

					case "edit_category_localization":
					{
						if (!IsAdmin(player) || !arg.HasArgs(3)) return;

						if (!_categoryEditing.TryGetValue(player.userID, out var editFields))
							return;

						ShopCategory category;
						if (!editFields.TryGetValue("item", out var obj) || (category = obj as ShopCategory) == null)
							return;

						var localization = category.Localization;
						if (localization == null) return;

						var fieldName = arg.Args[1];
						var fieldValue = arg.Args[2];

						switch (fieldName)
						{
							case "Enabled":
							{
								localization.Enabled = Convert.ToBoolean(fieldValue);
								break;
							}

							case "Messages":
							{
								if (!arg.HasArgs(5)) return;

								var paramValue = arg.Args[4];

								var hashCode = Convert.ToInt32(arg.Args[2]);

								KeyValuePair<string, string> msgField;

								switch (hashCode)
								{
									case 0:
									{
										msgField = new KeyValuePair<string, string>();
										break;
									}
									default:
									{
										msgField = localization.Messages.FirstOrDefault(
											x => x.GetHashCode() == hashCode);
										break;
									}
								}

								switch (arg.Args[3])
								{
									case "key":
									{
										msgField = new KeyValuePair<string, string>(paramValue, msgField.Value);
										break;
									}

									case "value":
									{
										if (!string.IsNullOrEmpty(paramValue))
											paramValue = string.Join(" ", arg.Args.Skip(4));

										msgField = new KeyValuePair<string, string>(msgField.Key, paramValue);
										break;
									}
								}

								if (hashCode == 0)
									localization.Messages.TryAdd(msgField.Key, msgField.Value);
								else
									localization.Messages[msgField.Key] = msgField.Value;
								break;
							}
						}

						EditCategoryUI(player);
						break;
					}

					case "edit_category_field":
					{
						if (!IsAdmin(player) || !arg.HasArgs(3)) return;

						if (!_categoryEditing.TryGetValue(player.userID, out var editFields))
							return;

						ShopCategory category;
						if (!editFields.TryGetValue("item", out var obj) || (category = obj as ShopCategory) == null)
							return;

						var fieldName = arg.Args[1];
						var newValue = arg.Args[2];

						var field = category.GetType().GetField(fieldName);
						if (field == null)
							return;

						object resultValue = null;
						switch (field.FieldType.Name)
						{
							case "String":
							{
								resultValue = string.Join(" ", arg.Args.Skip(2));
								break;
							}
							case "Int32":
							{
								if (int.TryParse(newValue, out var result))
									resultValue = result;
								break;
							}
							case "Single":
							{
								if (float.TryParse(newValue, out var result))
									resultValue = result;
								break;
							}
							case "Double":
							{
								if (double.TryParse(newValue, out var result))
									resultValue = result;
								break;
							}
							case "Boolean":
							{
								if (bool.TryParse(newValue, out var result))
									resultValue = result;
								break;
							}
						}

						if (resultValue != null && field.GetValue(category)?.Equals(resultValue) != true)
							field.SetValue(category, resultValue);

						EditCategoryUI(player);
						break;
					}

					case "close_edit_category":
					{
						if (!IsAdmin(player)) return;

						_categoryEditing.Remove(player.userID);
						break;
					}

					case "save_edit_category":
					{
						if (!IsAdmin(player)) return;

						if (!_categoryEditing.TryGetValue(player.userID, out var editFields))
							return;

						if (editFields["item"] is not ShopCategory category) return;

						var shop = GetShop(player);

						var generated = Convert.ToBoolean(editFields["generated"]);

						if (generated)
						{
							var shopUI = GetShop(player).GetUI();
							
							_itemsData.Shop.Add(category);

							var categories = GetCategories(player);

							var targetCategoriesPage = Mathf.FloorToInt((float) categories.Count / shopUI.ShopCategories.ShopCategory.CategoriesOnPage);

							if (categories.Count % shopUI.ShopCategories.ShopCategory.CategoriesOnPage == 0)
								targetCategoriesPage--;

							shop.Update();
							shop.OnChangeCategoriesPage(targetCategoriesPage);
							
							UpdateUI(player, container =>
							{
								ShopCategoriesUI(container, player);
							});
						}
						else
						{
							var old = FindCategoryById(category.ID);

							var index = _itemsData.Shop.IndexOf(old);
							if (index != -1)
								_itemsData.Shop[index] = category;

							old.Dispose();

							shop.Update();

							UpdateUI(player, container =>
							{
								ShopCategoriesUI(container, player);
							});
						}

						if (category.CategoryType == ShopCategory.Type.Favorite)
							_isEnabledFavorites = category.Enabled;

						_categoryEditing.Remove(player.userID);

						SaveItemsData();
						break;
					}

					case "remove_edit_category":
					{
						if (!IsAdmin(player)) return;

						if (!_categoryEditing.TryGetValue(player.userID, out var editFields))
							return;

						if (editFields["item"] is not ShopCategory category) return;

						var shop = GetShop(player);
						var targetCategoriesPage = shop.currentCategoriesPage;

						var isLastPage = targetCategoriesPage == GetLastCategoriesPage(player, GetCategories(player));

						var generated = Convert.ToBoolean(editFields["generated"]);
						if (generated) return;

						var old = FindCategoryById(category.ID);

						_itemsData.Shop.Remove(old);

						old.Dispose();
						category.Dispose();

						var categories = GetCategories(player);

						if (isLastPage)
							targetCategoriesPage = GetLastCategoriesPage(player, categories);

						shop.Update();
						shop.OnChangeCategoriesPage(targetCategoriesPage);
						
						UpdateUI(player, container =>
						{
							ShopCategoriesUI(container, player);
						});

						_categoryEditing.Remove(player.userID);

						SaveItemsData();
						break;
					}

					case "change_show_categories":
					{
						if (!IsAdmin(player)) return;

						if (!_showAllCategories.Add(player.userID))
							_showAllCategories.Remove(player.userID);

						GetShop(player)?.Update();

						UpdateUI(player, container =>
						{
							ShopCategoriesUI(container, player);
						});
						break;
					}
					
					case "edit_category_move":
					{
						if (!IsAdmin(player) || !arg.HasArgs(3)) return;

						var categoryID = arg.GetInt(1);
						var moveType = arg.GetString(2);
						
						var category = FindCategoryById(categoryID);
						if (category == null) return;

						switch (moveType)
						{
							case "up":
								category.MoveCategoryUp();
								break;
							case "down":
								category.MoveCategoryDown();
								break;
							default:
								PrintError("Unknown move type: {0}", moveType);
								return;
						}
						
						SaveItemsData();

						GetShop(player)?.Update();

						UpdateUI(player, container =>
						{
							ShopCategoriesUI(container, player);
						});
						break;
					}

					case "edit_item_move":
					{
						if (!IsAdmin(player) || !arg.HasArgs(3)) return;

						var itemID = arg.GetInt(1);
						var moveType = arg.GetString(2);

						var shop = GetShop(player);
						if (shop.currentCategoryIndex < 0) return;
						
						var list = GetCategories(player);
						if (shop.currentCategoryIndex >= list.Count) return;

						var shopCategory = list[shop.currentCategoryIndex];
						if (shopCategory == null) return;

						if (!TryFindItemById(itemID, out var shopItem))
							return;

						switch (moveType)
						{
							case "right":
								shopCategory.MoveItemRight(shopItem);
								break;
							case "left":
								shopCategory.MoveItemLeft(shopItem);
								break;
							default:
								PrintError("Unknown move type: {0}", moveType);
								return;
						}

						SaveItemsData();

						UpdateUI(player, container => 
						{
							ShopContentUI(player, container);
						});
						break;
					}
					
					case "cart_try_buy_again":
					{
						UpdateUI(player, container => {
							GetShop(player)?.GetUI()?.ShopConfirmationModal?.GetModal(player, container, 
							(Msg(player, BuyTitle), $"{CMD_Main_Console} cart_buy_again"), 
							(Msg(player, CancelTitle), string.Empty));
						});
						break;
					}

					case "cart_buy_again":
					{
						TryBuyItems(player, true);
						break;
					}

					case "favorites":
					{
						if (!arg.HasArgs(2))
							return;

						switch (arg.GetString(1))
						{
							case "item":
							{
								var itemID = arg.GetInt(3);

								if (!TryFindItemById(itemID, out var shopItem))
									return;

								if (GetPlayerCart(player.userID) is not PlayerCartData playerCart) return;

								switch (arg.GetString(2))
								{
									case "add":
									{
										if (!playerCart.AddItemToFavorite(itemID))
										{
											_config?.Notifications?.ShowNotify(player, MsgIsFavoriteItem, 1);
											return;
										}
										
										UpdateUI(player, container => 
										{
											ItemFavoriteUI(player, container, GetShop(player).GetUI(), shopItem);
										});

										_config?.Notifications?.ShowNotify(player, MsgAddedToFavoriteItem, 0, shopItem.GetPublicTitle(player));
										break;
									}

									case "remove":
									{
										if (!playerCart.RemoveItemFromFavorites(itemID))
										{
											_config?.Notifications?.ShowNotify(player, MsgNoFavoriteItem, 1);
											return;
										}

										var shop = GetShop(player);

										var shopCategory = GetCategories(player)[shop.currentCategoryIndex];
										if (shopCategory == null) return;

										var countItems = shopCategory.GetShopItems(player).Count;
										if (countItems - shop.currentShopPage * GetShopTotalItemsAmount(player) <= 0 && shop.currentShopPage > 0)
										{
											shop.OnChangeShopPage(shop.currentShopPage - 1);

											UpdateUI(player, container => 
											{
												ShopContentUI(player, container);
											});
										}
										else
										{
											UpdateUI(player, container => 
											{
												ItemFavoriteUI(player, container, GetShop(player).GetUI(), shopItem);
											});
										}

										_config?.Notifications?.ShowNotify(player, MsgRemovedFromFavoriteItem, 0, shopItem.GetPublicTitle(player));
										break;
									}
								}
								break;
							}
						}

						break;
					}
				}

#if TESTING
			}
			catch (Exception ex)
			{
				PrintError($"In the command '{CMD_Main_Console}' there was an error:\n{ex}");

				Debug.LogException(ex);
			}

			Puts($"Main command used with: {string.Join(", ", arg.Args)}");
#endif
		}

		private int GetLastCategoriesPage(BasePlayer player, List<ShopCategory> categories)
		{
			var shopUI = GetShop(player).GetUI();
			var targetCategoriesPage = Mathf.FloorToInt((float) categories.Count / shopUI.ShopCategories.ShopCategory.CategoriesOnPage);

			if (categories.Count % shopUI.ShopCategories.ShopCategory.CategoriesOnPage == 0)
				targetCategoriesPage--;
			return targetCategoriesPage;
		}
		
		private int GetLastShopContentPage(BasePlayer player)
		{
			var shop = GetShop(player);
			if (shop.Categories.Count <= 0) 
				return 0;
            
			var shopItemsCount = (shop.HasSearch()
				? SearchItem(player, shop.search)
				: shop.GetSelectedShopCategory()?.GetShopItems(player) ?? new List<ShopItem>()).Count;

			var shopTotalItemsAmount = GetShopTotalItemsAmount(player);
			
			var targetShopPage = Mathf.FloorToInt((float) shopItemsCount / shopTotalItemsAmount);
			if (shopItemsCount % shopTotalItemsAmount == 0)
				targetShopPage--;

			return targetShopPage;
		}
		
		[ConsoleCommand("shop.refill")]
		private void CmdConsoleRefill(ConsoleSystem.Arg arg)
		{
			if (!arg.IsAdmin) return;

			FillCategories();

			LoadItems();
		}

		[ConsoleCommand("shop.wipe")]
		private void CmdConsoleWipe(ConsoleSystem.Arg arg)
		{
			if (!arg.IsAdmin) return;

			if (_config.Wipe.Players || _config.Wipe.Cooldown || _config.Wipe.Limits)
				StartWipePlayers(_config.Wipe.Players, _config.Wipe.Cooldown, _config.Wipe.Limits);

			PrintWarning($"{Name} wiped!");
		}

		[ConsoleCommand("shop.reset")]
		private void CmdConsoleReset(ConsoleSystem.Arg arg)
		{
			if (!arg.IsServerside)
			{
				SendReply(arg, "This command can only be run from the SERVER CONSOLE.");
				return;
			}

			switch (arg.GetString(0))
			{
				case "template":
				{
					_uiData = new();
					SaveUIData();
					
					PrintWarning($"Template was reset!");
					break;
				}

				case "config":
				{
					_config = new();
					SaveConfig();
					
					PrintWarning($"Config was reset!");
					break;
				}

				case "items":
				{
					_itemsData  = new();
					SaveItemsData();
					
					PrintWarning($"Items was reset!");
					break;
				}
				
				case "full":
				{
					_uiData = new();
					SaveUIData();
			
					_config = new();
					SaveConfig();
			
					_itemsData  = new();
					SaveItemsData();

					PrintWarning($"Shop was reset!");
					break;
				}

				default:
				{
					SendReply(arg, $"Error syntax! Usage: {arg.cmd.FullName} [template/config/items/full] ");
					return;
				}
			}

			Interface.Oxide.ReloadPlugin("Shop");
		}

		[ConsoleCommand("shop.remove")]
		private void CmdConsoleRemoveItem(ConsoleSystem.Arg arg)
		{
			if (!arg.IsAdmin) return;

			if (!arg.HasArgs())
			{
				SendReply(arg, $"Error syntax! Usage: /{arg.cmd.FullName} [item/category] [item id/category name/all]");
				return;
			}

			var index = arg.Args[0];
			switch (index)
			{
				case "all":
					_itemsData.Shop.ForEach(shopCategory => shopCategory.Items.Clear());

					SendReply(arg, "All items from categories have been removed!");

					SaveItemsData();
					break;
				case "cat":
				case "cats":
				case "category":
				{
					if (arg.Args.Length < 2)
					{
						SendReply(arg, $"Error syntax! Usage: /{arg.cmd.FullName} {index} [category name/all]");
						return;
					}

					if (arg.Args[1] == "all")
					{
						_itemsData.Shop.Clear();

						var testCategory = new ShopCategory
						{
							Enabled = true,
							CategoryType = ShopCategory.Type.None,
							Title = "Test",
							Localization = new Localization
							{
								Enabled = false,
								Messages = new Dictionary<string, string>
								{
									["en"] = "Test",
									["fr"] = "Test"
								}
							},
							Permission = string.Empty,
							SortType = Configuration.SortType.None,
							Items = new List<ShopItem>
							{
								ShopItem.GetDefault(0, 100, "stones"),
								ShopItem.GetDefault(0, 100, "wood")
							}
						};

						_itemsData.Shop.Add(testCategory);

						SendReply(arg,
							"All categories were removed and one \"Test\" category was added with a couple of test items");

						SaveItemsData();
					}
					else
					{
						var catName = arg.Args[1];
						var category = FindCategoryByName(catName);
						if (category == null)
						{
							SendReply(arg, $"Category \"{catName}\" not found!");
							return;
						}

						_itemsData.Shop.Remove(category);

						SendReply(arg, $"Category \"{catName}\" successfully deleted!");

						SaveItemsData();
					}

					break;
				}

				case "item":
				case "items":
				{
					if (arg.Args.Length < 2)
					{
						SendReply(arg, $"Error syntax! Usage: /{arg.cmd.FullName} {index} [item id/all]");
						return;
					}

					var itemId = Convert.ToInt32(arg.Args[1]);
					var item = FindItemById(itemId);
					if (item == null)
					{
						SendReply(arg, $"Item \"{itemId}\" not found!");
						return;
					}

					_itemsData.Shop.ForEach(shopCategory =>
					{
						if (shopCategory.Items.Remove(item))
							shopCategory.SortItems();
					});

					SendReply(arg, $"Item \"{itemId}\" successfully deleted!");

					SaveItemsData();
					break;
				}
			}
		}

		[ConsoleCommand("shop.fill.icc")]
		private void CmdConsoleFillICC(ConsoleSystem.Arg arg)
		{
			if (!arg.IsAdmin) return;

			if (!arg.HasArgs())
			{
				SendReply(arg, "Error syntax! Usegae: shop.fill.icc [all/buy/sell]");
				return;
			}

			var type = -1;
			switch (arg.Args[0].ToLower())
			{
				case "buy":
				{
					type = 0;
					break;
				}
				case "sell":
				{
					type = 1;
					break;
				}
			}

			_itemsData.Shop.ForEach(category =>
			{
				if (category.CategoryType == ShopCategory.Type.Favorite) return;

				category.Items.ForEach(item =>
				{
					if (item.Type != ItemType.Item || item.ItemDefinition == null) return;

					var price = GetItemCost(item.ItemDefinition) * item.Amount;
					if (price <= 0) return;

					switch (type)
					{
						case 0:
						{
							item.Price = price;
							break;
						}
						case 1:
						{
							item.SellPrice = price;
							break;
						}
						default:
						{
							item.Price = price;
							item.SellPrice = price;
							break;
						}
					}
				});
			});

			Puts(
				$"The price has been updated for all items! Price type: {(type switch { 0 => "buy", 1 => "sell", _ => "all" })}");

			SaveItemsData();
		}

		#endregion

		#region Interface

		#region Shop

		#region UI Fields

		private int GetShopTotalItemsAmount(BasePlayer player)
		{
			var shopUI = GetShop(player).GetUI();
			return shopUI.ShopItem.ItemsOnString * shopUI.ShopItem.Strings;
		}
		
		#endregion

		#region UI Shop

		private CuiElementContainer ShowShopUI(BasePlayer player,
			bool first = false,
			bool categories = false,
			bool showGUI = true)
		{
			#region Fields
			
			var shop = GetShop(player);

			var shopUI = shop.GetUI();

			var container = new CuiElementContainer();

			#endregion

			#region Background

			if (first)
			{
				if (shopUI.ShopBackground.UseBackground)
				{
					container.Add(
					shopUI.ShopBackground.Background.GetImage(shopUI.ShopBackground.DisplayType, Layer, Layer));
					
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
					}, Layer, Layer + ".Background", Layer + ".Background");
					
				container.Add(new CuiElement()
					{
						Parent = Layer + ".Background",
						Components =
						{
							new CuiButtonComponent()
							{
								Color = "0 0 0 0",
								Close = Layer,
								Command = $"{CMD_Main_Console} closeui"
							},
							new CuiRectTransformComponent()
						}
					});
				}
			}

			#endregion

			#region Main

			container.Add(
				shopUI.ShopContent.Background.GetImage(Layer + ".Background", Layer + ".Main", Layer + ".Main"));
			
			ShopHeaderUI(player, container);

			ShopContentUI(player, container);

			#endregion

			#region Categories

			if (first || categories) ShopCategoriesUI(container, player);

			#endregion

			#region Cart

			if (first) ShopBasketUI(container, player);

			#endregion

            if (showGUI)
				CuiHelper.AddUi(player, container);

			return container;
		}

		#endregion

		#region UI Components

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

		private void ShopHeaderUI(BasePlayer player, CuiElementContainer container)
		{
			var shop = GetShop(player);

			var shopUI = shop.GetUI();

			container.Add(shopUI.ShopContent.Header.Background.GetImage(Layer + ".Main", Layer + ".Header",
				Layer + ".Header"));
			
			container.Add(shopUI.ShopContent.Header.Title.GetText(Msg(player, MainTitle), Layer + ".Header"));
			
			if (_config.Transfer)
				container.AddRange(shopUI.ShopContent.Header.ButtonTransfer.GetButton(Msg(player, TransferTitle), $"{CMD_Main_Console} transfer_start", Layer + ".Header"));

			if (_economics.Count > 1) 
				ShowChoiceEconomyUI(player, container);

			container.Add(shopUI.ShopContent.Header.Balance.Background.GetImage(Layer + ".Header", Layer + ".Balance", Layer + ".Balance"));
			
			container.Add(shopUI.ShopContent.Header.Balance.Title.GetText(Msg(player, YourBalance), Layer + ".Balance"));
			
			BalanceUi(container, player);

			if (shopUI.ShopContent.Header.UseCloseButton)
				container.AddRange(shopUI.ShopContent.Header.ButtonClose.GetButton(Msg(player, CloseButton), $"{CMD_Main_Console} closeui", Layer + ".Header", close: Layer));
		}

		#region Shop Categories

		private void ShopCategoriesUI(CuiElementContainer container,
			BasePlayer player)
		{
			var shop = GetShop(player);
			
			var shopUI = shop.GetUI();

			container.Add(shopUI.ShopCategories.Background.GetImage(Layer + ".Main", Layer + ".Categories",
				Layer + ".Categories"));
			
			#region Header
			
			container.Add(shopUI.ShopCategories.Header.Background.GetImage(Layer + ".Categories", Layer + ".Categories.Header"));
			
			container.Add(shopUI.ShopCategories.Header.Title.GetText(Msg(player, CategoriesTitle), Layer + ".Categories.Header"));

			#endregion

			#region Loop
			
			container.Add(shopUI.ShopCategories.CategoriesPanel.GetImage(Layer + ".Categories", Layer + ".Categories.Content", Layer + ".Categories.Content"));
			
			if (shopUI.ShopCategories.UseScrollCategories)
			{
				var targetMargin = shopUI.ShopCategories.ShopCategory.Margin;
				
				var pageCategoriesCount = (shopUI.ShopCategories.UseScrollCategories ? shop.Categories : shop.Categories.SkipAndTake(shop.currentCategoriesPage * shopUI.ShopCategories.ShopCategory.CategoriesOnPage,
					shopUI.ShopCategories.ShopCategory.CategoriesOnPage)).Count;

				if (IsAdmin(player))
				{
					targetMargin += shopUI.ShopCategories.ShopCategory.AdminPanel.AdditionalMargin;

					pageCategoriesCount += 1;
				}
				
				var totalHeight = pageCategoriesCount * shopUI.ShopCategories.ShopCategory.Height +
				                  (pageCategoriesCount - 1) * 
				                  targetMargin;

				totalHeight += shopUI.ShopCategories.CategoriesScrollView.AdditionalHeight;
				
				if (IsAdmin(player)) totalHeight += (shopUI.ShopCategories.ShopCategory.AdminPanel.AdditionalMargin + 30);
                
				totalHeight = Math.Max(totalHeight,  shopUI.ShopCategories.CategoriesScrollView.MinHeight);
				
				container.Add(new CuiElement
				{
					Parent = Layer + ".Categories.Content",
					Name = Layer + ".Categories.Scroll",
					DestroyUi = Layer + ".Categories.Scroll",
					Components =
					{
						shopUI.ShopCategories.CategoriesScrollView.GetScrollView(totalHeight),
					}
				});
			}

			CategoriesListUI(container, player);
			
			#endregion

			#region Pages

			if (!shopUI.ShopCategories.UseScrollCategories)
			{
				container.AddRange(shopUI.ShopCategories.BackButton.GetButton(Msg(player, BtnBack), shop.currentCategoriesPage != 0
					? $"{CMD_Main_Console} categories_change_local_page {shop.currentCategoriesPage - 1}"
					: string.Empty, Layer + ".Categories.Content"));

				container.AddRange(shopUI.ShopCategories.NextButton.GetButton(Msg(player, BtnNext), shop.Categories.Count > (shop.currentCategoriesPage + 1) * shopUI.ShopCategories.ShopCategory.CategoriesOnPage
					? $"{CMD_Main_Console} categories_change_local_page {shop.currentCategoriesPage + 1}"
					: string.Empty, Layer + ".Categories.Content"));
			}
			
			#endregion
		}

		private void CategoriesListUI(CuiElementContainer container, BasePlayer player)
		{
			var shop = GetShop(player);

			var shopUI = shop.GetUI();

			var offsetY = -shopUI.ShopCategories.ShopCategory.TopIndent;

			var categoryId = shop.currentCategoriesPage * shopUI.ShopCategories.ShopCategory.CategoriesOnPage;

			var targetMargin = shopUI.ShopCategories.ShopCategory.Margin;
			
			var targetLayer = shopUI.ShopCategories.UseScrollCategories ? Layer + ".Categories.Scroll" : Layer + ".Categories.Content";

			var pageCategories = shopUI.ShopCategories.UseScrollCategories ? shop.Categories : shop.Categories.SkipAndTake(shop.currentCategoriesPage * shopUI.ShopCategories.ShopCategory.CategoriesOnPage,
					shopUI.ShopCategories.ShopCategory.CategoriesOnPage);
			
			if (IsAdmin(player))
			{
				offsetY -= (shopUI.ShopCategories.ShopCategory.AdminPanel.AdditionalMargin + 30);

				targetMargin += shopUI.ShopCategories.ShopCategory.AdminPanel.AdditionalMargin;

				#region Check Show All

				container.Add(shopUI.ShopCategories.CategoryAdminPanel.CheckboxCategoriesDisplay.Background.GetImage(targetLayer, targetLayer + ".Admin.Show.All", targetLayer + ".Admin.Show.All"));
				
				container.AddRange(shopUI.ShopCategories.CategoryAdminPanel.CheckboxCategoriesDisplay.CheckboxButton.GetButton(_showAllCategories.Contains(player.userID) ? "✔" : string.Empty, $"{CMD_Main_Console} change_show_categories", targetLayer + ".Admin.Show.All", targetLayer + ".Admin.Show.All.Check", targetLayer + ".Admin.Show.All.Check"));
				
				CreateOutLine(ref container, targetLayer + ".Admin.Show.All.Check", shopUI.ShopCategories.CategoryAdminPanel.CheckboxCategoriesDisplay.CheckboxColor.Get, shopUI.ShopCategories.CategoryAdminPanel.CheckboxCategoriesDisplay.CheckboxSize);

				container.Add(shopUI.ShopCategories.CategoryAdminPanel.CheckboxCategoriesDisplay.Title.GetText(Msg(player, UICategoriesAdminShowAllTitle),targetLayer + ".Admin.Show.All.Check"));
				
				#endregion Check Show All

				#region Add Category

				container.AddRange(shopUI.ShopCategories.CategoryAdminPanel.ButtonAddCategory.GetButton( Msg(player, BtnAddCategory), $"{CMD_Main_Console} start_edit_category -1", targetLayer, targetLayer + ".Admin.Add.Category", targetLayer + ".Admin.Add.Category"));
				
				#endregion Add Category
			}

			foreach (var category in pageCategories)
			{
				container.Add(new CuiPanel()
					{
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "0 1",
							OffsetMin = $"{shopUI.ShopCategories.ShopCategory.LeftIndent} {offsetY - shopUI.ShopCategories.ShopCategory.Height}",
							OffsetMax = $"{shopUI.ShopCategories.ShopCategory.LeftIndent + shopUI.ShopCategories.ShopCategory.Width} {offsetY}"
						},
						Image =
						{
							Color = "0 0 0 0"
						}
					}, targetLayer, Layer + $".Category.{category.ID}", Layer + $".Category.{category.ID}");
				
				ShowCategoryUI(player, container, shop.currentCategoryIndex, category, categoryId, shopUI);

				offsetY = offsetY - 
				          shopUI.ShopCategories.ShopCategory.Height -
				          targetMargin;

				categoryId++;
			}
		}

		private void ShowCategoryUI(BasePlayer player, CuiElementContainer container,
			int currentCategoryIndex, 
			ShopCategory category, 
			int categoryId, 
			ShopUI shopUI)
		{
			var title = $"{category.GetTitle(player)}";

			if (!category.Enabled)
				title = $"[DISABLED] {title}";

			var isSelectedCategory = categoryId == currentCategoryIndex;

			container.Add((isSelectedCategory ? shopUI.ShopCategories.ShopCategory.SelectedCategory : shopUI.ShopCategories.ShopCategory.Category).Background.GetImage(Layer + $".Category.{category.ID}"));
				
			container.Add(
				(isSelectedCategory ? shopUI.ShopCategories.ShopCategory.SelectedCategory : shopUI.ShopCategories.ShopCategory.Category).Title.GetText(title, Layer + $".Category.{category.ID}"));

			container.Add(new CuiElement()
			{
				Parent = Layer + $".Category.{category.ID}",
				Components =
				{
					new CuiButtonComponent()
					{
						Color = "0 0 0 0",
						Command = !isSelectedCategory ? $"{CMD_Main_Console} categories_change {categoryId}" : string.Empty
					}
				}
			});
				
			if (IsAdmin(player))
			{
				if (!category.Enabled)
					container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "0 1",
								OffsetMin = "0 0",
								OffsetMax = "5 0"
							},
							Image =
							{
								Color = _config.UI.Color5.Get
							}
						}, Layer + $".Category.{category.ID}");
				

				container.Add(shopUI.ShopCategories.ShopCategory.AdminPanel.Background.GetImage(Layer + $".Category.{category.ID}", Layer + $".Category.{category.ID}.Settings", Layer + $".Category.{category.ID}.Settings"));

				container.AddRange(shopUI.ShopCategories.ShopCategory.AdminPanel.ButtonEdit.GetButton(Msg(player, BtnEditCategory), $"{CMD_Main_Console} start_edit_category {category.ID}", Layer + $".Category.{category.ID}.Settings"));
                
				container.AddRange(shopUI.ShopCategories.ShopCategory.AdminPanel.ButtonMoveUp.GetButton(string.Empty, $"{CMD_Main_Console} edit_category_move {category.ID} up", Layer + $".Category.{category.ID}.Settings"));
				container.AddRange(shopUI.ShopCategories.ShopCategory.AdminPanel.ButtonMoveDown.GetButton(string.Empty, $"{CMD_Main_Console} edit_category_move {category.ID} down", Layer + $".Category.{category.ID}.Settings"));
			}
		}

		#endregion

		private void ShopContentUI(BasePlayer player, 
			CuiElementContainer container)
		{
			var shop = GetShop(player);

			var shopUI = shop.GetUI();
            
			container.Add(shopUI.ShopContent.Content.Background.GetImage(Layer + ".Main", Layer + ".Shop.Content",
				Layer + ".Shop.Content"));
			
			if (shop.Categories.Count > 0)
			{				
				var (inPageItems, shopItemsCount) = GetPaginationShopItems(player);
				if (inPageItems.Count > 0)
				{
					var targetMarginY = shopUI.ShopItem.Margin;
					var targetTopIndent = -shopUI.ShopContent.ItemsTopIndent;
					
					if (IsAdmin(player))
					{
						targetMarginY += shopUI.ShopItem.AdminPanel.AdditionalMargin;

						targetTopIndent -= shopUI.ShopItem.AdminPanel.AdditionalMargin;
					}

					if (shopUI.ShopContent.Content.UseScrollToListItems)
					{
						var maxLines = Mathf.CeilToInt((float) shopItemsCount / shopUI.ShopItem.ItemsOnString);
						var totalHeight = maxLines * shopUI.ShopItem.ItemHeight + (maxLines - 1) * targetMarginY;

						totalHeight += Mathf.Abs(targetTopIndent);
						
						totalHeight += shopUI.ShopContent.Content.ListItemsScrollView.AdditionalHeight;
						
						totalHeight  = Math.Max(totalHeight, shopUI.ShopContent.Content.ListItemsScrollView.MinHeight);
                
						container.Add(new CuiElement
						{
							Parent = Layer + ".Shop.Content",
							Name = Layer + ".Shop.Scroll",
							DestroyUi = Layer + ".Shop.Scroll",
							Components =
							{
								shopUI.ShopContent.Content.ListItemsScrollView.GetScrollView(totalHeight),
							}
						});
					}
					
					#region Items

					var cdItems = inPageItems.FindAll(x =>
						GetCooldownTime(player.userID, x, true) > 0 || GetCooldownTime(player.userID, x, false) > 0);

					if (cdItems.Count > 0)
						shop.AddItemToUpdate(cdItems);

					CheckUpdateController();

					var targetShopLayer = shopUI.ShopContent.Content.UseScrollToListItems ? Layer + ".Shop.Scroll" :  Layer + ".Shop.Content";
					
					ShowGridUI(container, 0, 
						inPageItems.Count,
						shopUI.ShopItem.ItemsOnString, 
						shopUI.ShopItem.Margin,
						targetMarginY,
						shopUI.ShopItem.ItemWidth, 
						shopUI.ShopItem.ItemHeight,
						shopUI.ShopContent.ItemsLeftIndent,
						targetTopIndent,
						0, 0, 1, 1, "0 0 0 0", targetShopLayer,
						index => Layer + $".Item.{inPageItems[index].ID}.Background",
						index => Layer + $".Item.{inPageItems[index].ID}.Background",
						index => ItemUI(player, inPageItems[index], container));
					
					#endregion

					#region Search

					if (shopUI.ShopContent.Content.Search.Enabled)
					{
						container.Add(
							shopUI.ShopContent.Content.Search.Background.GetImage(targetShopLayer,
								Layer + ".Search"));
						
						container.Add(new CuiElement
						{
							Parent = Layer + ".Search",
							Components =
							{
								new CuiInputFieldComponent
								{
									FontSize = shopUI.ShopContent.Content.Search.InputField.FontSize,
									Align = shopUI.ShopContent.Content.Search.InputField.Align,
									Font = shopUI.ShopContent.Content.Search.InputField.IsBold ? "robotocondensed-bold.ttf" : "robotocondensed-regular.ttf",
									Command = $"{CMD_Main_Console} shop_search_input",
									Color = shopUI.ShopContent.Content.Search.InputField.Color.Get,
									CharsLimit = 32,
									NeedsKeyboard = true,
									Text = string.IsNullOrEmpty(shop.search) ? Msg(player, SearchTitle) : $"{shop.search}"
								},
								shopUI.ShopContent.Content.Search.InputField.GetRectTransform()
							}
						});
					}

					#endregion

					#region Pages

					if (!shopUI.ShopContent.Content.UseScrollToListItems)
					{
						var isSearch = shop.HasSearch();

						var shopTotalItemsAmount = GetShopTotalItemsAmount(player);
						
						var hasPages = isSearch ? shopItemsCount > 0 * shopTotalItemsAmount : shopItemsCount > (shop.currentShopPage + 1) * shopTotalItemsAmount || shop.currentShopPage != 0;
						if (hasPages)
						{
							container.AddRange(shopUI.ShopContent.Content.ButtonBack.GetButton(Msg(player, BackPage), isSearch
							? shop.currentSearchPage != 0
								? $"{CMD_Main_Console} shop_search_page {shop.currentSearchPage - 1}"
								: string.Empty
							: shop.currentShopPage != 0
								? $"{CMD_Main_Console} shop_page {shop.currentShopPage - 1}"
								: string.Empty, Layer + ".Shop.Content"));

						container.AddRange(shopUI.ShopContent.Content.ButtonNext.GetButton(Msg(player, NextPage), isSearch
							? shopItemsCount > (shop.currentSearchPage + 1) * shopTotalItemsAmount
								? $"{CMD_Main_Console} shop_search_page {shop.currentSearchPage + 1}"
								: string.Empty
							: shopItemsCount > (shop.currentShopPage + 1) * shopTotalItemsAmount
								? $"{CMD_Main_Console} shop_page {shop.currentShopPage + 1}"
								: string.Empty, Layer + ".Shop.Content"));
						}
					}

					#endregion
				}
				else
				{
					container.Add(shopUI.NoItems.GetText(Msg(player, UIMsgNoItems),
						Layer + ".Shop.Content"));
				}
				
				#region Add Item

				if (IsAdmin(player) && shop.canShowCategoriesMoveButtons)
					container.AddRange(shopUI.ShopContent.Content.ButtonAddItem.GetButton(Msg(player, BtnAddItem),
						$"{CMD_Main_Console} startedititem {GetId()}", Layer + ".Shop.Content"));

				#endregion
			}
		}

		#region Shop Cart

		private void ShopBasketUI(CuiElementContainer container, BasePlayer player)
		{
			var shop = GetShop(player);
			var shopUI = shop.GetUI();
			if (!shopUI.ShopBasket.UseShopBasket) return;

			var playerCartData = GetPlayerCart(player.userID);

			var shopItemsCount = playerCartData.GetShopItems().Count;
			var hasPages = shopItemsCount > shopUI.ShopBasket.BasketItem.ItemsOnPage;

			container.Add(shopUI.ShopBasket.Background.GetImage(Layer + ".Main", Layer + ".PlayerCart",
				Layer + ".PlayerCart"));
			
			#region Header

			container.Add(shopUI.ShopBasket.Header.Background.GetImage(Layer + ".PlayerCart", Layer + ".PlayerCart.Header", Layer + ".PlayerCart.Header"));

			container.Add(shopUI.ShopBasket.Header.Title.GetText(Msg(player, ShoppingBag),Layer + ".PlayerCart.Header"));

			#region Pages

			if (!shopUI.ShopBasket.Content.UseScrollShoppingBag && hasPages)
			{
				container.AddRange(shopUI.ShopBasket.Header.BackButton.GetButton(Msg(player, BackTitle), shop.currentBasketPage != 0 ? $"{CMD_Main_Console} cart_page {shop.currentBasketPage - 1}" : string.Empty, Layer + ".PlayerCart.Header"));

				container.AddRange(shopUI.ShopBasket.Header.NextButton.GetButton(Msg(player, NextTitle), playerCartData.Items.Count > (shop.currentBasketPage + 1) * shopUI.ShopCategories.ShopCategory.CategoriesOnPage
					? $"{CMD_Main_Console} cart_page {shop.currentBasketPage + 1}"
					: string.Empty, Layer + ".PlayerCart.Header"));
			}

			#endregion

			#endregion

			#region Items
			
			container.Add(shopUI.ShopBasket.Content.Background.GetImage(Layer + ".PlayerCart", Layer + ".PlayerCart.Content", Layer + ".PlayerCart.Content"));

			if (shopUI.ShopBasket.Content.UseScrollShoppingBag && hasPages)
			{
				var totalHeight = shopItemsCount * shopUI.ShopBasket.BasketItem.Height + (shopItemsCount - 1) * shopUI.ShopBasket.BasketItem.Margin;

				totalHeight += shopUI.ShopBasket.BasketItem.TopIndent;
				
				totalHeight += shopUI.ShopBasket.Content.ShoppingBagScrollView.AdditionalHeight;
				
                totalHeight  = Math.Max(totalHeight, shopUI.ShopBasket.Content.ShoppingBagScrollView.MinHeight);
                
				container.Add(new CuiElement
				{
					Parent = Layer + ".PlayerCart.Content",
					Name = Layer + ".PlayerCart.Scroll",
					DestroyUi = Layer + ".PlayerCart.Scroll",
					Components =
					{
						shopUI.ShopBasket.Content.ShoppingBagScrollView.GetScrollView(totalHeight),
					}
				});
			}

			var pageShopItems = shopUI.ShopBasket.Content.UseScrollShoppingBag
				? playerCartData.GetShopItems()
				: playerCartData.GetShopItems().Skip(shop.currentBasketPage * shopUI.ShopCategories.ShopCategory.CategoriesOnPage).Take(shopUI.ShopCategories.ShopCategory.CategoriesOnPage);

			ShopCartItemsListUI(container, player, pageShopItems, hasPages);

			#endregion

			#region Footer

			container.Add(shopUI.ShopBasket.Footer.Background.GetImage(Layer + ".PlayerCart", Layer + ".PlayerCart.Footer.Background", Layer + ".PlayerCart.Footer.Background"));

			UpdateShopCartFooterUI(container, player, playerCartData);

			#endregion
		}

		private void UpdateShopCartFooterUI(CuiElementContainer container, BasePlayer player, CartData playerCartData)
		{
			var shopUI = GetShop(player).GetUI();
			
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
			}, Layer + ".PlayerCart.Footer.Background", Layer + ".PlayerCart.Footer", Layer + ".PlayerCart.Footer");

			var useBuyAgain = _config.BuyAgain.HasAccess(player) &&
			                  (playerCartData as PlayerCartData)?.LastPurchaseItems.Count > 0;

			container.AddRange((useBuyAgain ? shopUI.ShopBasket.Footer.BuyButtonWhenBuyAgain : shopUI.ShopBasket.Footer.BuyButton).
				GetButton(Msg(player, BuyTitle), shopUI.ShopBasket.ShowConfirmMenu ? $"{CMD_Main_Console} cart_try_buyitems" : $"{CMD_Main_Console} cart_buyitems", Layer + ".PlayerCart.Footer"));
			
			if (useBuyAgain)
			{
				container.AddRange(shopUI.ShopBasket.Footer.BuyAgainButton.
					GetButton(string.Empty, $"{CMD_Main_Console} cart_try_buy_again", Layer + ".PlayerCart.Footer",
						Layer + ".PlayerCart.Footer.BuyAgain"));
			}
			
			container.Add(shopUI.ShopBasket.Footer.ItemsCountTitle.GetText(Msg(player, UIBasketFooterItemsCountTitle), Layer + ".PlayerCart.Footer"));
			container.Add(shopUI.ShopBasket.Footer.ItemsCountValue.GetText(Msg(player, UIBasketFooterItemsCountValue, playerCartData.GetCartItemsAmount()), Layer + ".PlayerCart.Footer"));

			container.Add(shopUI.ShopBasket.Footer.ItemsCostTitle.GetText(Msg(player, UIBasketFooterItemsCostTitle), Layer + ".PlayerCart.Footer"));
			container.Add(shopUI.ShopBasket.Footer.ItemsCostValue.GetText(GetPlayerEconomy(player).GetFooterPriceTitle(player, playerCartData.GetCartPrice(player, !(playerCartData.Items.Count > 0)).ToString(_config.Formatting.ShoppingBagCostFormat)), Layer + ".PlayerCart.Footer"));
		}

		private void ShopCartItemsListUI(CuiElementContainer container, BasePlayer player, Dictionary<ShopItem, int> pageShopItems,
			bool hasPages)
		{
			var shopUI = GetShop(player).GetUI();

			var ySwitch = -shopUI.ShopBasket.BasketItem.TopIndent;
			var i = 0;
			foreach (var (item, amount) in pageShopItems)
			{
				container.Add(new CuiPanel()
					{
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "0 1",
							OffsetMin = $"{shopUI.ShopBasket.BasketItem.LeftIndent} {ySwitch - shopUI.ShopBasket.BasketItem.Height}",
							OffsetMax = $"{shopUI.ShopBasket.BasketItem.LeftIndent + shopUI.ShopBasket.BasketItem.Width} {ySwitch}",
						},
						Image =
						{
							Color = "0 0 0 0"
						}
					},
					(shopUI.ShopBasket.Content.UseScrollShoppingBag && hasPages
						? Layer + ".PlayerCart.Scroll"
						: Layer + ".PlayerCart.Content"),
					Layer + $".PlayerCart.Item.{i}.Background", 
					Layer + $".PlayerCart.Item.{i}.Background");

				container.Add(
					shopUI.ShopBasket.BasketItem.Background.GetImage(Layer + $".PlayerCart.Item.{i}.Background"));
				
				if (shopUI.ShopBasket.BasketItem.ShowImageBackground)
					container.Add(
					shopUI.ShopBasket.BasketItem.ImageBackground.GetImage(Layer + $".PlayerCart.Item.{i}.Background"));
				
				ShopCartItemUI(player, container, i, item, amount);

				ySwitch = ySwitch - shopUI.ShopBasket.BasketItem.Height - shopUI.ShopBasket.BasketItem.Margin;

				i++;
			}
		}

		private void ShopCartItemUI(BasePlayer player, CuiElementContainer container, int index, ShopItem item, int amount)
		{
			var shopUI = GetShop(player).GetUI();

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
				}, Layer + $".PlayerCart.Item.{index}.Background", 
				Layer + $".PlayerCart.Item.{index}", 
				Layer + $".PlayerCart.Item.{index}");
				
			if (item.Blueprint)
			{
				container.Add(new CuiElement()
				{
					Parent =  Layer + $".PlayerCart.Item.{index}",
					Components =
					{
						new CuiImageComponent()
						{
							ItemId = ItemManager.blueprintBaseDef.itemid
						},
						shopUI.ShopBasket.BasketItem.ImageBlueprint.GetRectTransform()
					}
				});
			}
			
			container.Add(item.GetImage(shopUI.ShopBasket.BasketItem.ImageItem, Layer + $".PlayerCart.Item.{index}"));

			container.Add(shopUI.ShopBasket.BasketItem.Title.GetText(item.GetPublicTitle(player), Layer + $".PlayerCart.Item.{index}"));
			
			container.Add(shopUI.ShopBasket.BasketItem.ItemAmount.GetText(Msg(player, AmountTitle, item.Amount * amount), Layer + $".PlayerCart.Item.{index}"));
			
			#region Amount

			container.AddRange(shopUI.ShopBasket.BasketItem.ButtonRemoveItem.GetButton(Msg(player, RemoveTitle),
				$"{CMD_Main_Console} cart_item_remove {item.ID}", Layer + $".PlayerCart.Item.{index}"));

			container.AddRange(shopUI.ShopBasket.BasketItem.ButtonMinusAmount.GetButton(Msg(player, MinusTitle),
				$"{CMD_Main_Console} cart_item_change {index} {item.ID} {amount - 1}", Layer + $".PlayerCart.Item.{index}"));
			
			container.AddRange(shopUI.ShopBasket.BasketItem.ButtonPlusAmount.GetButton(Msg(player, PlusTitle),
				$"{CMD_Main_Console} cart_item_change {index} {item.ID} {amount + 1}", Layer + $".PlayerCart.Item.{index}"));

			container.Add(new CuiElement
			{
				Parent = Layer + $".PlayerCart.Item.{index}",
				Components =
				{
					new CuiInputFieldComponent
					{
						FontSize = shopUI.ShopBasket.BasketItem.InputAmount.FontSize,
						Font = shopUI.ShopBasket.BasketItem.InputAmount.IsBold ? "robotocondensed-bold.ttf" : "robotocondensed-regular.ttf",
						Align = shopUI.ShopBasket.BasketItem.InputAmount.Align,
						Color = shopUI.ShopBasket.BasketItem.InputAmount.Color.Get,
						Command = $"{CMD_Main_Console} cart_item_change {index} {item.ID}",
						Text = $"{amount}",
						CharsLimit = 5,
						NeedsKeyboard = true,
						HudMenuInput = true
					},
					shopUI.ShopBasket.BasketItem.InputAmount.GetRectTransform()
				}
			});

			#endregion
		}

		#endregion Shop Cart

		#endregion

		#region UI Modals

		private void ModalShopItemActionUI(BasePlayer player, ShopItem item, bool buy,
			int amount = 1)
		{
			var container = new CuiElementContainer();
			
			var selectedEconomy = API_GetShopPlayerSelectedEconomy(player.userID);

			var targetModalTemplate =
				buy ? GetShop(player).GetUI().ShopBuyModal : GetShop(player).GetUI().ShopSellModal;
			
			targetModalTemplate.GetModal(player, container, item, amount,
				buy
					? (item.GetPrice(player, selectedEconomy) * amount).ToString(_config.Formatting.BuyPriceFormat)
					: (item.GetSellPrice(player, selectedEconomy) * amount).ToString(_config.Formatting.SellPriceFormat),
				buy
					? Msg(player, BuyTitle)
					: Msg(player, SellTitle),
				buy
					? $"{CMD_Main_Console} try_buy_item {item.ID}"
					: $"{CMD_Main_Console} try_sell_item {item.ID}",
				buy
					? $"{CMD_Main_Console} try_buy_item {item.ID} {amount - 1}"
					: $"{CMD_Main_Console} try_sell_item {item.ID} {amount - 1}",
				buy
					? $"{CMD_Main_Console} try_buy_item {item.ID} {amount + 1}"
					: $"{CMD_Main_Console} try_sell_item {item.ID} {amount + 1}",
				buy
					? $"{CMD_Main_Console} try_buy_item {item.ID} all"
					: $"{CMD_Main_Console} try_sell_item {item.ID} all",
				buy
					? $"{CMD_Main_Console} fastbuyitem {item.ID} {amount}"
					: $"{CMD_Main_Console} sellitem {item.ID} {amount}",
				$"{CMD_Main_Console} shop_buy {item.ID}");
            
			CuiHelper.AddUi(player, container);
		}
        
		private void ErrorUi(BasePlayer player, string msg)
		{
			var container = new CuiElementContainer
			{
				{
					new CuiPanel
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Image = {Color = _config.UI.Color13.Get},
						CursorEnabled = true
					},
					_config.UI.DisplayType, ModalLayer, ModalLayer
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
						Image = {Color = _config.UI.Color5.Get}
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
							Text = Msg(player, ErrorMsg),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 120,
							Color = _config.UI.Color8.Get
						}
					},
					ModalLayer + ".Main"
				},
				{
					new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -175", OffsetMax = "0 -135"
						},
						Text =
						{
							Text = $"{msg}",
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = _config.UI.Color8.Get
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
							Text = Msg(player, ErrorClose),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 12,
							Color = _config.UI.Color8.Get
						},
						Button = {Color = _config.UI.Color7.Get, Close = ModalLayer}
					},
					ModalLayer + ".Main"
				}
			};

			CuiHelper.AddUi(player, container);
		}

		#endregion

		#region UI Shop Item

		private void BuyButtonUI(BasePlayer player, ref CuiElementContainer container, ShopItem shopItem, int selectedEconomy)
		{
			var shopUI = GetShop(player).GetUI();

			var hasSell = shopItem.CanBeSold();

			var buttonTemplate = hasSell ? shopUI.ShopItem.BuyButton : shopUI.ShopItem.BuyButtonIfNoSell;
			
			var cooldownTime = GetCooldownTime(player.userID, shopItem, true);
			if (cooldownTime > 0)
			{
				container.Add(buttonTemplate.Cooldown.Background.GetImage(Layer + $".Item.{shopItem.ID}",
					Layer + $".Item.{shopItem.ID}.Buy",
					Layer + $".Item.{shopItem.ID}.Buy"));

				container.Add(buttonTemplate.Cooldown.Title.GetText(Msg(player, BuyCooldownTitle),
					Layer + $".Item.{shopItem.ID}.Buy"));

				container.Add(buttonTemplate.Cooldown.LeftTime.GetText(
					$"{FormatShortTime(player, TimeSpan.FromSeconds(cooldownTime))}",
					Layer + $".Item.{shopItem.ID}.Buy"));
			}
			else
			{
				container.Add(buttonTemplate.Background.GetImage(Layer + $".Item.{shopItem.ID}",
					Layer + $".Item.{shopItem.ID}.Buy",
					Layer + $".Item.{shopItem.ID}.Buy"));

				container.Add(buttonTemplate.Title.GetText(Msg(player, BuyTitle),
					Layer + $".Item.{shopItem.ID}.Buy"));

				container.Add(buttonTemplate.Price.GetText(shopItem.Price <= 0.0
						? Msg(player, ItemPriceFree)
						: GetPlayerEconomy(player).GetPriceTitle(player, shopItem.GetPrice(player, selectedEconomy).ToString(_config.Formatting.BuyPriceFormat)),
					Layer + $".Item.{shopItem.ID}.Buy"));

				container.Add(new CuiButton
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text = {Text = string.Empty},
						Button =
						{
							Color = "0 0 0 0",
							Command = !shopUI.ShopBasket.UseShopBasket || shopItem.ForceBuy
								? $"{CMD_Main_Console} try_buy_item {shopItem.ID}"
								: $"{CMD_Main_Console} buyitem {shopItem.ID}"
						}
					}, Layer + $".Item.{shopItem.ID}.Buy");
			}
		}

		private void SellButtonUI(BasePlayer player, ref CuiElementContainer container, ShopItem shopItem, int selectedEconomy)
		{
			var shopUI = GetShop(player).GetUI();

			var hasBuy = shopItem.CanBePurchased();

			var buttonTemplate = hasBuy ? shopUI.ShopItem.SellButton : shopUI.ShopItem.SellButtonIfNoBuy;

			var cooldownTime = GetCooldownTime(player.userID, shopItem, false);
			if (cooldownTime > 0)
			{
				container.Add(buttonTemplate.Cooldown.Background.GetImage(Layer + $".Item.{shopItem.ID}",
					Layer + $".Item.{shopItem.ID}.Sell",
					Layer + $".Item.{shopItem.ID}.Sell"));

				container.Add(buttonTemplate.Cooldown.Title.GetText(Msg(player, SellCooldownTitle),
					Layer + $".Item.{shopItem.ID}.Sell"));

				container.Add(buttonTemplate.Cooldown.LeftTime.GetText(
					$"{FormatShortTime(player, TimeSpan.FromSeconds(cooldownTime))}",
					Layer + $".Item.{shopItem.ID}.Sell"));
			}
			else
			{
				container.Add(buttonTemplate.Background.GetImage(Layer + $".Item.{shopItem.ID}",
					Layer + $".Item.{shopItem.ID}.Sell",
					Layer + $".Item.{shopItem.ID}.Sell"));

				container.Add(buttonTemplate.Title.GetText(Msg(player, SellTitle),
					Layer + $".Item.{shopItem.ID}.Sell"));

				container.Add(buttonTemplate.Price.GetText(shopItem.SellPrice <= 0.0
						? Msg(player, ItemPriceFree)
						: GetPlayerEconomy(player).GetPriceTitle(player, shopItem.GetSellPrice(player, selectedEconomy).ToString(_config.Formatting.SellPriceFormat)),
					Layer + $".Item.{shopItem.ID}.Sell"));

				container.Add(new CuiButton
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text = {Text = string.Empty},
						Button =
						{
							Color = "0 0 0 0",
							Command = $"{CMD_Main_Console} try_sell_item {shopItem.ID}"
						}
					}, Layer + $".Item.{shopItem.ID}.Sell");
			}
		}

		private void ShopItemButtonsUI(BasePlayer player, ref CuiElementContainer container, ShopItem shopItem, int selectedEconomy)
		{
			if (shopItem.CanBePurchased())
				BuyButtonUI(player, ref container, shopItem, selectedEconomy);

			if (shopItem.CanBeSold())
				SellButtonUI(player, ref container, shopItem, selectedEconomy);
		}

		private void ItemUI(BasePlayer player, 
			ShopItem shopItem, 
			CuiElementContainer container)
		{
			var selectedEconomy = API_GetShopPlayerSelectedEconomy(player.userID);

			var shop = GetShop(player);

			var shopUI = shop.GetUI();

			container.Add(shopUI.ShopItem.Background.GetImage(Layer + $".Item.{shopItem.ID}.Background",
				Layer + $".Item.{shopItem.ID}",
				Layer + $".Item.{shopItem.ID}"));

			#region Blueprint

			if (shopItem.Blueprint)
				container.Add(new CuiElement
				{
					Parent = Layer + $".Item.{shopItem.ID}",
					Components =
					{
						new CuiImageComponent
						{
							ItemId = ItemManager.blueprintBaseDef.itemid
						},
						shopUI.ShopItem.Blueprint.GetRectTransform()
					}
				});

			#endregion

			container.Add(shopItem.GetImage(shopUI.ShopItem.Image, Layer + $".Item.{shopItem.ID}",
				Layer + $".Item.{shopItem.ID}.Image"));

			#region Title
			
			container.Add(shopUI.ShopItem.Title.GetText(shopItem.GetPublicTitle(player),
				Layer + $".Item.{shopItem.ID}"));

			#endregion

			#region Favorite

			if (_isEnabledFavorites && shopItem.CanBeFavorite(player.userID))
			{
				ItemFavoriteUI(player, container, shopUI, shopItem);
			}

			#endregion

			#region Discount

			var discount = shopItem.GetDiscount(player);
			if (discount > 0)
			{
				container.Add(shopUI.ShopItem.Discount.Background.GetImage(Layer + $".Item.{shopItem.ID}",
					Layer + $".Item.{shopItem.ID}.Discount"));

				container.Add(shopUI.ShopItem.Discount.Value.GetText($"-{discount}%",
					Layer + $".Item.{shopItem.ID}.Discount"));
			}

			#endregion

			#region Amount

			container.Add(
				shopUI.ShopItem.Amount.Title.GetText(Msg(player, ItemAmount), Layer + $".Item.{shopItem.ID}"));

			container.Add(shopUI.ShopItem.Amount.Background.GetImage(Layer + $".Item.{shopItem.ID}",
				Layer + $".Item.{shopItem.ID}.Amount"));

			container.Add(shopUI.ShopItem.Amount.Value.GetText(Msg(player, UIShopItemAmount, shopItem.Amount),
				Layer + $".Item.{shopItem.ID}.Amount"));

			#endregion

			#region Info

			if (!string.IsNullOrEmpty(shopItem.Description))
				container.AddRange(shopUI.ShopItem.Info.GetButton(
					Msg(player, InfoTitle),
					$"{CMD_Main_Console} item_info {shopItem.ID}",
					Layer + $".Item.{shopItem.ID}"));

			#endregion

			#region Buttons

			ShopItemButtonsUI(player, ref container, shopItem, selectedEconomy);

			#endregion

			#region Edit

			if (IsAdmin(player))
			{
				container.Add(shopUI.ShopItem.AdminPanel.Background.GetImage(Layer + $".Item.{shopItem.ID}", Layer + $".Item.{shopItem.ID}.Settings", Layer + $".Item.{shopItem.ID}.Settings"));

				container.AddRange(shopUI.ShopItem.AdminPanel.ButtonEdit.GetButton(Msg(player, BtnEditCategory), $"{CMD_Main_Console} startedititem {shopItem.ID}", Layer + $".Item.{shopItem.ID}.Settings"));

				if (shop.canShowCategoriesMoveButtons)
				{
					container.AddRange(shopUI.ShopItem.AdminPanel.ButtonMoveRight.GetButton("▶", $"{CMD_Main_Console} edit_item_move {shopItem.ID} right", Layer + $".Item.{shopItem.ID}.Settings"));
									
					container.AddRange(shopUI.ShopItem.AdminPanel.ButtonMoveLeft.GetButton("◀", $"{CMD_Main_Console} edit_item_move {shopItem.ID} left", Layer + $".Item.{shopItem.ID}.Settings"));
				}
			}

			#endregion

		}

		private void ItemFavoriteUI(BasePlayer player, CuiElementContainer container, ShopUI shopUI, ShopItem shopItem)
		{
			var isFavorite = shopItem.IsFavorite(player.userID);

			container.AddRange(
				(isFavorite
					? shopUI.ShopItem.Favorite.RemoveFromFavorites
					: shopUI.ShopItem.Favorite.AddToFavorites)
				.GetButton(string.Empty,
					isFavorite
						? $"{CMD_Main_Console} favorites item remove {shopItem.ID}"
						: $"{CMD_Main_Console} favorites item add {shopItem.ID}",
					Layer + $".Item.{shopItem.ID}", 
					Layer + $".Item.{shopItem.ID}.Favorite",
					 Layer + $".Item.{shopItem.ID}.Favorite"));
				
		}

		#endregion

		private void BalanceUi(CuiElementContainer container, BasePlayer player)
		{
			var shopUI = GetShop(player).GetUI();

			var nowEconomy = GetPlayerEconomy(player);

			container.Add(shopUI.ShopContent.Header.Balance.Value.GetText(nowEconomy.GetBalanceTitle(player), Layer + ".Balance", Layer + ".Balance.Value", Layer + ".Balance.Value")); 
		}

		private void ShowChoiceEconomyUI(BasePlayer player, CuiElementContainer container, bool selected = false)
		{
			var shopUI = GetShop(player).GetUI();

			container.AddRange(shopUI.ShopContent.Header.ButtonToggleEconomy.GetButton(Msg(player, UIContentHeaderButtonToggleEconomy), $"{CMD_Main_Console} economy_try_change {!selected}", Layer + ".Header", Layer + ".Change.Economy", Layer + ".Change.Economy"));

			#region Selection

			if (selected)
			{
				var nowEconomy = GetPlayerEconomy(player);

				var halfWidth = (_economics.Count * shopUI.SelectCurrency.EconomyWidth +
				                 (_economics.Count - 1) * shopUI.SelectCurrency.EconomyMargin) / 2f;

				var xSwitch = -halfWidth;

				#region Background

				container.Add(new CuiElement
				{
					Parent = Layer + ".Change.Economy",
					Name = Layer + ".Change.Economy.Panel",
					DestroyUi = Layer + ".Change.Economy.Panel",
					Components = 
					{
						shopUI.SelectCurrency.Background.GetImageComponent(),
						new CuiRectTransformComponent{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin =
							$"-{halfWidth + shopUI.SelectCurrency.FrameWidth} {shopUI.SelectCurrency.FrameIndent}",
						OffsetMax =
							$"{halfWidth + shopUI.SelectCurrency.FrameWidth} {shopUI.SelectCurrency.FrameIndent + shopUI.SelectCurrency.EconomyHeight + shopUI.SelectCurrency.FrameHeader}"
						}
					}
				});

				container.Add(shopUI.SelectCurrency.Title.GetText(Msg(player, ChoiceEconomy), Layer + ".Change.Economy.Panel"));

				#endregion

				#region Economics

				foreach (var economyConf in _economics)
				{
					var imageComponent = new CuiImageComponent{
						Color = economyConf.IsSame(nowEconomy)
								? shopUI.SelectCurrency.SelectedEconomyColor.Get
								: shopUI.SelectCurrency.UnselectedEconomyColor.Get,
					};

					if (!string.IsNullOrWhiteSpace(shopUI.SelectCurrency.EconomyPanelMaterial))
						imageComponent.Material = shopUI.SelectCurrency.EconomyPanelMaterial;

					if (!string.IsNullOrWhiteSpace(shopUI.SelectCurrency.EconomyPanelSprite))
						imageComponent.Sprite = shopUI.SelectCurrency.EconomyPanelSprite;

					container.Add(new CuiElement{
						Parent = Layer + ".Change.Economy.Panel",
						Name = Layer + $".Change.Economy.Panel.{economyConf.ID}",
						DestroyUi = Layer + $".Change.Economy.Panel.{economyConf.ID}",
						Components = {
							imageComponent,
							new CuiRectTransformComponent{
								AnchorMin = "0.5 0", AnchorMax = "0.5 0",
							OffsetMin = $"{xSwitch} {shopUI.SelectCurrency.EconomyIndent}",
							OffsetMax =
								$"{xSwitch + shopUI.SelectCurrency.EconomyWidth} {shopUI.SelectCurrency.EconomyIndent + shopUI.SelectCurrency.EconomyHeight}"
							}
						}
					});

					container.Add(shopUI.SelectCurrency.EconomyTitle.GetText(economyConf.GetTitle(player), Layer + $".Change.Economy.Panel.{economyConf.ID}"));

					container.Add(new CuiElement
					{
						Parent = Layer + $".Change.Economy.Panel.{economyConf.ID}",
						Components = {
							new CuiButtonComponent{
								Color = "0 0 0 0",
								Command = $"{CMD_Main_Console} economy_set {economyConf.ID}"
							},
							new CuiRectTransformComponent()
						}
					});
					
					xSwitch += shopUI.SelectCurrency.EconomyWidth + shopUI.SelectCurrency.EconomyMargin;
				}

				#endregion
			}
			
			#endregion
		}

		private static void ShowChoiceEconomyUpdateLayer(CuiElementContainer container)
		{
			container.Add(new CuiPanel
			{
				RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
				Image = { Color = "0 0 0 0" }
			}, Layer + ".Change.Economy", Layer + ".Change.Economy.Update", Layer + ".Change.Economy.Update");
		}

		#endregion

		#region Transfer

		private const float
			SELECT_PLAYER_WIDTH = 180f,
			SELECT_PLAYER_HEIGHT = 50f,
			SELECT_PLAYER_MARGIN_X = 20f,
			SELECT_PLAYER_MARGIN_Y = 30f,
			SELECT_PLAYER_CONST_SWITCH_Y = -180f,
			SELECT_PLAYER_CONST_SWITCH_X = -(SELECT_PLAYER_AMOUNT_ON_STRING * SELECT_PLAYER_WIDTH +
			                                 (SELECT_PLAYER_AMOUNT_ON_STRING - 1) * SELECT_PLAYER_MARGIN_X) / 2f,
			SELECT_PLAYER_PAGE_SIZE = 25f,
			SELECT_PLAYER_SELECTED_PAGE_SIZE = 40f,
			SELECT_PLAYER_PAGES_MARGIN = 5f;

		private const int
			SELECT_PLAYER_AMOUNT_ON_STRING = 4,
			SELECT_PLAYER_STRINGS = 5,
			SELECT_PLAYER_TOTAL_AMOUNT = SELECT_PLAYER_AMOUNT_ON_STRING * SELECT_PLAYER_STRINGS;

		private void SelectTransferPlayerUI(BasePlayer player, int selectPage = 0)
		{
			#region Fields

			var players = GetAvailablePlayersCount(player.userID);
			
			var container = new CuiElementContainer();

			#endregion
			
			#region Background

			container.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image =
				{
					Color = "0.19 0.19 0.18 0.65",
					Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
				},
				CursorEnabled = true
			}, _config.UI.DisplayType, ModalLayer, ModalLayer);

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text = {Text = string.Empty},
				Button =
				{
					Color = "0 0 0 0",
					Close = ModalLayer
				}
			}, ModalLayer);

			#endregion

			if (players > 0)
			{
				#region Title

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "0 -140",
						OffsetMax = "0 -100"
					},
					Text =
					{
						Text = Msg(player, SelectPlayerTitle),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 32,
						Color = _config.UI.Color8.Get
					}
				}, ModalLayer);

				#endregion

				#region Players

				var playersToShow = GetAvailablePlayersToTransfer(player.userID, selectPage * SELECT_PLAYER_TOTAL_AMOUNT, SELECT_PLAYER_TOTAL_AMOUNT);
				
				ShowGridUI(container, 0, playersToShow.Count, SELECT_PLAYER_AMOUNT_ON_STRING, SELECT_PLAYER_MARGIN_X, SELECT_PLAYER_MARGIN_Y, SELECT_PLAYER_WIDTH, SELECT_PLAYER_HEIGHT, SELECT_PLAYER_CONST_SWITCH_X, SELECT_PLAYER_CONST_SWITCH_Y, 0.5f, 0.5f, 1f, 1f, "0 0 0 0", ModalLayer, i => ModalLayer + $".Player.{i}", null,
					index =>
					{
						var member = playersToShow[index];
						
						container.Add(new CuiElement
						{
							Parent = ModalLayer + $".Player.{index}",
							Components =
							{
								new CuiRawImageComponent
								{
									Png = GetImage($"avatar_{member.userID}")
								},
								new CuiRectTransformComponent
								{
									AnchorMin = "0 0", AnchorMax = "0 0",
									OffsetMin = "0 0", OffsetMax = "50 50"
								}
							}
						});

						container.Add(new CuiLabel
							{
								RectTransform =
								{
									AnchorMin = "0 0.5", AnchorMax = "1 1",
									OffsetMin = "55 0", OffsetMax = "0 0"
								},
								Text =
								{
									Text = $"{member.displayName}",
									Align = TextAnchor.LowerLeft,
									Font = "robotocondensed-regular.ttf",
									FontSize = 18,
									Color = _config.UI.Color8.Get
								}
							}, ModalLayer + $".Player.{index}");

						container.Add(new CuiButton
							{
								RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
								Text = {Text = string.Empty},
								Button =
								{
									Color = "0 0 0 0",
									Command =
										$"{CMD_Main_Console} transfer_set_target {member.userID}"
								}
							}, ModalLayer + $".Player.{index}");
					});
				
				#endregion

				#region Pages

				var pages = (int) Math.Ceiling((double) players / SELECT_PLAYER_TOTAL_AMOUNT);
				if (pages > 1)
				{
					var xSwitch = -((pages - 1) * SELECT_PLAYER_PAGE_SIZE + (pages - 1) * SELECT_PLAYER_PAGES_MARGIN +
					            SELECT_PLAYER_SELECTED_PAGE_SIZE) / 2f;

					for (var j = 0; j < pages; j++)
					{
						var selected = selectPage == j;

						container.Add(new CuiButton
						{
							RectTransform =
							{
								AnchorMin = "0.5 0", AnchorMax = "0.5 0",
								OffsetMin = $"{xSwitch} 60",
								OffsetMax =
									$"{xSwitch + (selected ? SELECT_PLAYER_SELECTED_PAGE_SIZE : SELECT_PLAYER_PAGE_SIZE)} {60 + (selected ? SELECT_PLAYER_SELECTED_PAGE_SIZE : SELECT_PLAYER_PAGE_SIZE)}"
							},
							Text =
							{
								Text = $"{j + 1}",
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-bold.ttf",
								FontSize = selected ? 18 : 12,
								Color = _config.UI.Color8.Get
							},
							Button =
							{
								Color = _config.UI.Color2.Get,
								Command =
									$"{CMD_Main_Console} transfer_page {j}"
							}
						}, ModalLayer);

						xSwitch += (selected ? SELECT_PLAYER_SELECTED_PAGE_SIZE : SELECT_PLAYER_PAGE_SIZE) +
						           SELECT_PLAYER_PAGES_MARGIN;
					}
				}

				#endregion
			}
			else
			{
				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1"
					},
					Text =
					{
						Text = Msg(player, NoTransferPlayers),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 28,
						Color = "1 1 1 0.85"
					}
				}, ModalLayer);

				container.Add(new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text = {Text = string.Empty},
					Button =
					{
						Color = "0 0 0 0",
						Close = ModalLayer
					}
				}, ModalLayer);
			}

			CuiHelper.AddUi(player, container);
		}

		private void TransferUi(BasePlayer player,
			ulong targetId,
			float amount = 0)
		{
			var target = BasePlayer.FindByID(targetId);
			if (target == null) return;

			var container = new CuiElementContainer();

			#region Background

			container.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image =
				{
					Color = "0 0 0 0.9",
					Material = "assets/content/ui/uibackgroundblur.mat"
				},
				CursorEnabled = true
			}, "Overlay", ModalLayer, ModalLayer);

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text = {Text = string.Empty},
				Button =
				{
					Color = "0 0 0 0",
					Close = ModalLayer
				}
			}, ModalLayer);

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-125 -100",
					OffsetMax = "125 75"
				},
				Image =
				{
					Color = _config.UI.Color3.Get
				}
			}, ModalLayer, ModalLayer + ".Main");

			#region Header

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -50", OffsetMax = "0 0"
				},
				Image =
				{
					Color = _config.UI.Color1.Get
				}
			}, ModalLayer + ".Main", ModalLayer + ".Header");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "20 0", OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, TransferTitle),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = _config.UI.Color8.Get
				}
			}, ModalLayer + ".Header");

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
					Text = Msg(player, CloseButton),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 10,
					Color = _config.UI.Color8.Get
				},
				Button =
				{
					Close = ModalLayer,
					Color = _config.UI.Color2.Get
				}
			}, ModalLayer + ".Header");

			#endregion

			#region Player

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-105 -110",
					OffsetMax = "105 -60"
				},
				Image =
				{
					Color = _config.UI.Color1.Get
				}
			}, ModalLayer + ".Main", ModalLayer + ".Player");

			#region Avatar

			container.Add(new CuiElement
			{
				Parent = ModalLayer + ".Player",
				Components =
				{
					new CuiRawImageComponent
					{
						Png = GetImage($"avatar_{target.userID}")
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 0", AnchorMax = "0 0",
						OffsetMin = "5 5",
						OffsetMax = "45 45"
					}
				}
			});

			#endregion

			#region Name

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "50 0", OffsetMax = "0 0"
				},
				Text =
				{
					Text = $"{target.displayName}",
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 20,
					Color = _config.UI.Color8.Get
				}
			}, ModalLayer + ".Player");

			#endregion

			#endregion

			#region Send

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-105 -160",
					OffsetMax = "105 -120"
				},
				Image =
				{
					Color = _config.UI.Color1.Get
				}
			}, ModalLayer + ".Main", ModalLayer + ".Send");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 0.5", AnchorMax = "1 0.5",
					OffsetMin = "-85 -12.5",
					OffsetMax = "-5 12.5"
				},
				Text =
				{
					Text = Msg(player, TransferButton),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = _config.UI.Color8.Get
				},
				Button =
				{
					Color = _config.UI.Color2.Get,
					Close = ModalLayer,
					Command =
						$"{CMD_Main_Console} transfer_send {targetId} {amount}"
				}
			}, ModalLayer + ".Send");

			container.Add(new CuiElement
			{
				Parent = ModalLayer + ".Send",
				Components =
				{
					new CuiInputFieldComponent
					{
						FontSize = 12,
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						Command =
							$"{CMD_Main_Console} transfer_set_amount {targetId}",
						NeedsKeyboard = true,
						Color = "1 1 1 0.75",
						CharsLimit = 32,
						Text = $"{amount}",
						HudMenuInput = true
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "0 0", OffsetMax = "-90 0"
					}
				}
			});

			#endregion

			#endregion

			CuiHelper.AddUi(player, container);
		}

		#endregion

		#region Edit

		private void EditUi(BasePlayer player, int itemId = 0, bool First = false)
		{
			var shop = GetShop(player);
			var category = shop.currentCategoryIndex;
			var page = shop.currentShopPage;

			var container = new CuiElementContainer();

			#region Dictionary

			if (!_itemEditing.ContainsKey(player.userID))
			{
				var shopItem = FindItemById(itemId);
				if (shopItem != null)
					_itemEditing[player.userID] = shopItem.ToDictionary();
				else
					_itemEditing[player.userID] = new Dictionary<string, object>
					{
						["Generated"] = true,
						["ID"] = GetId(),
						["Type"] = ItemType.Item,
						["Image"] = string.Empty,
						["Title"] = string.Empty,
						["Command"] = string.Empty,
						["DisplayName"] = string.Empty,
						["ShortName"] = string.Empty,
						["Skin"] = 0UL,
						["Blueprint"] = false,
						["Buying"] = true,
						["Selling"] = true,
						["Amount"] = 1,
						["Price"] = 100.0,
						["SellPrice"] = 100.0,
						["Plugin_Hook"] = string.Empty,
						["Plugin_Name"] = string.Empty,
						["Plugin_Amount"] = 1
					};
			}

			#endregion

			var edit = _itemEditing[player.userID];

			#region Background

			if (First)
				container.Add(new CuiPanel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Image = {Color = _config.UI.Color11.Get},
					CursorEnabled = true
				}, _config.UI.DisplayType, EditingLayer, EditingLayer);

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-260 -240",
					OffsetMax = "260 260"
				},
				Image =
				{
					Color = _config.UI.Color3.Get
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
				Image = {Color = _config.UI.Color1.Get}
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
					Color = _config.UI.Color8.Get
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
					Text = Msg(player, CloseButton),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 10,
					Color = _config.UI.Color8.Get
				},
				Button =
				{
					Close = EditingLayer,
					Color = _config.UI.Color2.Get,
					Command = $"{CMD_Main_Console} closeediting"
				}
			}, EditingLayer + ".Header");

			#endregion

			#region Type

			var type = edit["Type"] as ItemType? ?? ItemType.Item;

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-70 -110",
					OffsetMax = "30 -80"
				},
				Text =
				{
					Text = Msg(player, ItemName),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = _config.UI.Color8.Get
				},
				Button =
				{
					Color = (type == ItemType.Item ? _config.UI.Color2 : _config.UI.Color10).Get,
					Command = $"{CMD_Main_Console} edititem Type {ItemType.Item}"
				}
			}, EditingLayer + ".Main");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "35 -110",
					OffsetMax = "135 -80"
				},
				Text =
				{
					Text = Msg(player, CmdName),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = _config.UI.Color8.Get
				},
				Button =
				{
					Color = (type == ItemType.Command ? _config.UI.Color2 : _config.UI.Color10).Get,
					Command = $"{CMD_Main_Console} edititem Type {ItemType.Command}"
				}
			}, EditingLayer + ".Main");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "140 -110",
					OffsetMax = "240 -80"
				},
				Text =
				{
					Text = Msg(player, PluginName),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = _config.UI.Color8.Get
				},
				Button =
				{
					Color = (type == ItemType.Plugin ? _config.UI.Color2 : _config.UI.Color10).Get,
					Command = $"{CMD_Main_Console} edititem Type {ItemType.Plugin}"
				}
			}, EditingLayer + ".Main");

			#endregion

			#region Command

			EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"-240 -110",
				"-75 -60",
				$"{CMD_Main_Console} edititem Command ",
				new KeyValuePair<string, object>("Command", edit["Command"]));

			#endregion

			#region Item

			var shortName = (string) edit["ShortName"];

			#region Image

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-240 -290", OffsetMax = "-100 -150"
				},
				Image = {Color = _config.UI.Color1.Get}
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
							SkinId = Convert.ToUInt64(edit["Skin"])
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "10 10", OffsetMax = "-10 -10"
						}
					}
				});

			#endregion

			#region Select Item

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-240 -325",
					OffsetMax = "-100 -295"
				},
				Text =
				{
					Text = Msg(player, BtnSelect),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = _config.UI.Color8.Get
				},
				Button =
				{
					Color = _config.UI.Color2.Get,
					Command = $"{CMD_Main_Console} selectitem {category}"
				}
			}, EditingLayer + ".Main");

			#endregion

			#region ShortName

			EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"-85 -190",
				"75 -130",
				$"{CMD_Main_Console} edititem ShortName ",
				new KeyValuePair<string, object>("ShortName", edit["ShortName"]));

			#endregion

			#region Skin

			EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"80 -190",
				"240 -130",
				$"{CMD_Main_Console} edititem Skin ",
				new KeyValuePair<string, object>("Skin", edit["Skin"]));

			#endregion

			#region DisplayName

			EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"-85 -260",
				"75 -200",
				$"{CMD_Main_Console} edititem DisplayName ",
				new KeyValuePair<string, object>("DisplayName", edit["DisplayName"]));

			#endregion

			#region Amount

			EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"80 -260",
				"240 -200",
				$"{CMD_Main_Console} edititem Amount ",
				new KeyValuePair<string, object>("Amount", edit["Amount"]));

			#endregion

			#region SellPrice

			var sellPriceLayout = CuiHelper.GetGuid();
			EditFieldUi(ref container, EditingLayer + ".Main", sellPriceLayout,
				"-85 -330",
				"75 -270",
				$"{CMD_Main_Console} edititem SellPrice ",
				new KeyValuePair<string, object>("SellPrice", edit["SellPrice"]));

			if (ItemCostCalculator != null)
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 0", AnchorMax = "1 1",
						OffsetMin = "-60 10",
						OffsetMax = "-5 -30"
					},
					Text =
					{
						Text = Msg(player, BtnCalculate),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = _config.UI.Color8.Get
					},
					Button =
					{
						Color = _config.UI.Color2.Get,
						Command = $"{CMD_Main_Console} edititem SellPrice auto"
					}
				}, sellPriceLayout);

			#endregion

			#region Price

			var priceLayout = CuiHelper.GetGuid();
			EditFieldUi(ref container, EditingLayer + ".Main", priceLayout,
				"80 -330",
				"240 -270",
				$"{CMD_Main_Console} edititem Price ",
				new KeyValuePair<string, object>("Price", edit["Price"]));

			if (ItemCostCalculator != null)
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 0", AnchorMax = "1 1",
						OffsetMin = "-60 10",
						OffsetMax = "-5 -30"
					},
					Text =
					{
						Text = Msg(player, BtnCalculate),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = _config.UI.Color8.Get
					},
					Button =
					{
						Color = _config.UI.Color2.Get,
						Command = $"{CMD_Main_Console} edititem Price auto"
					}
				}, priceLayout);

			#endregion

			#region Title

			EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"-240 -395",
				"-5 -335",
				$"{CMD_Main_Console} edititem Title ",
				new KeyValuePair<string, object>("Title", edit["Title"]));

			#endregion

			#region Image

			EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"5 -395",
				"240 -335",
				$"{CMD_Main_Console} edititem Image ",
				new KeyValuePair<string, object>("Image", edit["Image"]));

			#endregion

			#region Plugin Hook

			EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"-240 -460",
				"-85 -400",
				$"{CMD_Main_Console} edititem Plugin_Hook ",
				new KeyValuePair<string, object>("Plugin_Hook", edit["Plugin_Hook"]));

			#endregion

			#region Plugin Name

			EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"-80 -460",
				"80 -400",
				$"{CMD_Main_Console} edititem Plugin_Name ",
				new KeyValuePair<string, object>("Plugin_Name", edit["Plugin_Name"]));

			#endregion

			#region Plugin Amount

			EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"85 -460",
				"240 -400",
				$"{CMD_Main_Console} edititem Plugin_Amount ",
				new KeyValuePair<string, object>("Plugin_Amount", edit["Plugin_Amount"]));

			#endregion

			#endregion

			var generated = (bool) edit["Generated"];

			#region Save Button

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 0", AnchorMax = "0.5 0",
					OffsetMin = "-90 -5",
					OffsetMax = $"{(generated ? 90 : 55)} 25"
				},
				Text =
				{
					Text = Msg(player, BtnSave),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = _config.UI.Color8.Get
				},
				Button =
				{
					Color = _config.UI.Color2.Get,
					Command = $"{CMD_Main_Console} saveitem",
					Close = EditingLayer
				}
			}, EditingLayer + ".Main");

			if (!generated)
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0",
						OffsetMin = "60 -5",
						OffsetMax = "90 25"
					},
					Text =
					{
						Text = Msg(player, RemoveItem),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = _config.UI.Color8.Get
					},
					Button =
					{
						Color = _config.UI.Color5.Get,
						Command = $"{CMD_Main_Console} removeitem",
						Close = EditingLayer
					}
				}, EditingLayer + ".Main");

			#endregion

			#region Bools

			var blueprint = Convert.ToBoolean(edit["Blueprint"]);

			var xSwitch = -240f;

			CheckBoxUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"0.5 0", "0.5 0",
				$"{xSwitch} 10",
				$"{xSwitch + 10} 20",
				blueprint,
				$"{CMD_Main_Console} edititem Blueprint {!blueprint}",
				Msg(player, EditBlueprint));

			xSwitch += 340f;

			var buying = Convert.ToBoolean(edit["Buying"]);

			CheckBoxUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"0.5 0", "0.5 0",
				$"{xSwitch} 10",
				$"{xSwitch + 10} 20",
				buying,
				$"{CMD_Main_Console} edititem Buying {!buying}",
				Msg(player, "Buying"));

			xSwitch += 60f;

			var selling = Convert.ToBoolean(edit["Selling"]);

			CheckBoxUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"0.5 0", "0.5 0",
				$"{xSwitch} 10",
				$"{xSwitch + 10} 20",
				selling,
				$"{CMD_Main_Console} edititem Selling {!selling}",
				Msg(player, "Selling"));

			#endregion

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private void SelectItem(BasePlayer player, int category, string selectedCategory = "", int page = 0,
			string input = "")
		{
			if (string.IsNullOrEmpty(selectedCategory))
				selectedCategory = _itemsCategories.FirstOrDefault().Key;

			var container = new CuiElementContainer();

			#region Background

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text = {Text = string.Empty},
				Button =
				{
					Close = ModalLayer,
					Color = _config.UI.Color12.Get
				}
			}, _config.UI.DisplayType, ModalLayer, ModalLayer);

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
					Color = _config.UI.Color3.Get
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
			foreach (var cat in _itemsCategories)
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
						Text = $"{cat.Key}",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = _config.UI.Color8.Get
					},
					Button =
					{
						Color = selectedCategory == cat.Key
							? _config.UI.Color2.Get
							: _config.UI.Color1.Get,
						Command = $"{CMD_Main_Console} selectitem {category} {cat.Key}"
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

			GetAvailableItemsToSelect(selectedCategory, page, input, totalAmount, out var canSearch, out var itemsAmount, out var Items);
			
			ShowGridUI(container, 0, Items.Count, amountOnString, xMargin, yMargin, Width, Height, xSwitch, ySwitch, 0.5f, 0.5f, 1f, 1f, 
				_config.UI.Color1.Get, ModalLayer + ".Main", 
				index => ModalLayer + $".Item.{index}", null,
				index =>
				{
					var item = Items[index];
					
					container.Add(new CuiElement
					{
						Parent = ModalLayer + $".Item.{index}",
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
								Command = $"{CMD_Main_Console} takeitem {category} {page} {item.shortName}",
								Close = ModalLayer
							}
						}, ModalLayer + $".Item.{index}");
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
				Image = {Color = _config.UI.Color2.Get}
			}, ModalLayer + ".Main", ModalLayer + ".Search");

			container.Add(new CuiElement
			{
				Parent = ModalLayer + ".Search",
				Components =
				{
					new CuiInputFieldComponent
					{
						FontSize = 10,
						Font = "robotocondensed-regular.ttf",
						Align = TextAnchor.MiddleLeft,
						Command = $"{CMD_Main_Console} selectitem {category} {selectedCategory} 0 ",
						Color = "1 1 1 0.65",
						CharsLimit = 150,
						NeedsKeyboard = true,
						Text = canSearch ? $"{input}" : Msg(player, ItemSearch)
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
					Color = _config.UI.Color8.Get
				},
				Button =
				{
					Color = _config.UI.Color1.Get,
					Command = page != 0
						? $"{CMD_Main_Console} selectitem {category} {selectedCategory} {page - 1} {input}"
						: string.Empty
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
					Color = _config.UI.Color8.Get
				},
				Button =
				{
					Color = _config.UI.Color2.Get,
					Command = itemsAmount > (page + 1) * totalAmount
						? $"{CMD_Main_Console} selectitem {category} {selectedCategory} {page + 1} {input}"
						: string.Empty
				}
			}, ModalLayer + ".Main");

			#endregion

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private void EditCategoryUI(BasePlayer player, bool First = false)
		{
			var editFields = _categoryEditing[player.userID];
			if (editFields?["item"] is not ShopCategory category) return;
			
			var generated = Convert.ToBoolean(editFields["generated"]);

			var fields = category.GetType().GetFields(bindingFlags).ToList()
				.FindAll(field => field.GetCustomAttribute<JsonIgnoreAttribute>() == null);

			var container = new CuiElementContainer();
			
			#region Background

			if (First)
				container.Add(new CuiPanel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Image = {Color = _config.UI.Color11.Get},
					CursorEnabled = true
				}, _config.UI.DisplayType, EditingLayer, EditingLayer);

			#endregion
			
			#region Main

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-260 -240",
					OffsetMax = "260 260"
				},
				Image =
				{
					Color = _config.UI.Color3.Get
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
				Image = {Color = _config.UI.Color1.Get}
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
					Text = Msg(player, EditingCategoryTitle),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = _config.UI.Color8.Get
				}
			}, EditingLayer + ".Header");

			if (!generated)
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 1", AnchorMax = "1 1",
						OffsetMin = "-145 -37.5",
						OffsetMax = "-95 -12.5"
					},
					Text =
					{
						Text = Msg(player, "REMOVE"), //add to lang
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 12,
						Color = _config.UI.Color8.Get
					},
					Button =
					{
						Close = EditingLayer,
						Color = _config.UI.Color5.Get,
						Command = $"{CMD_Main_Console} remove_edit_category"
					}
				}, EditingLayer + ".Header");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-90 -37.5",
					OffsetMax = "-40 -12.5"
				},
				Text =
				{
					Text = Msg(player, "SAVE"), //add to lang
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 12,
					Color = _config.UI.Color8.Get
				},
				Button =
				{
					Close = EditingLayer,
					Color = HexToCuiColor("#50965F"),
					Command = $"{CMD_Main_Console} save_edit_category"
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
					Text = Msg(player, CloseButton),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 10,
					Color = _config.UI.Color8.Get
				},
				Button =
				{
					Close = EditingLayer,
					Color = _config.UI.Color2.Get,
					Command = $"{CMD_Main_Console} close_edit_category"
				}
			}, EditingLayer + ".Header");

			#endregion

			#region Fields

			var width = 150f;
			var height = 45f;
			var margin = 5f;
			var ySwitch = -65f;

			var itemsOnString = 3;

			var constXSwitch = 10f;
			var xSwitch = constXSwitch;

			#region Strings

			var element = 0;
			fields.FindAll(field => field.Name != "Image" && (field.FieldType == typeof(string) ||
			                                                  field.FieldType == typeof(double) ||
			                                                  field.FieldType == typeof(float) ||
			                                                  field.FieldType == typeof(bool) ||
			                                                  field.FieldType == typeof(int))).ForEach(field =>
			{
				var name = CuiHelper.GetGuid();

				if (field.FieldType == typeof(bool))
					EditBoolField(player, ref container, category, field,
						EditingLayer + ".Main", name,
						"0 1", "0 1",
						$"{xSwitch} {ySwitch - height}",
						$"{xSwitch + width} {ySwitch}",
						$"{CMD_Main_Console} edit_category_field {field.Name}"
					);
				else
					EditTextField(ref container, category, field,
						EditingLayer + ".Main", name,
						"0 1", "0 1",
						$"{xSwitch} {ySwitch - height}",
						$"{xSwitch + width} {ySwitch}",
						$"{CMD_Main_Console} edit_category_field {field.Name} "
					);

				if (++element % itemsOnString == 0)
				{
					xSwitch = constXSwitch;
					ySwitch = ySwitch - height - margin;
				}
				else
				{
					xSwitch += width + margin;
				}
			});

			ySwitch = ySwitch - height - margin;
			// ySwitch -= margin;

			#endregion

			#region Localization

			var localizationField = fields.Find(field => field.Name == "Localization");
			if (localizationField != null)
				EditFieldLocalization(
					player,
					ref container,
					ref localizationField,
					localizationField.GetValue(category),
					EditingLayer + ".Main", null,
					$"{CMD_Main_Console} edit_category_localization ",
					ref ySwitch);

			ySwitch -= margin;

			#endregion

			#endregion

			#endregion

			CuiHelper.AddUi(player, container);
		}

		#region Components

		private void FieldLocalizationMessage(ref CuiElementContainer container, string parent, string command,
			ref float ySwitch, KeyValuePair<string, string> msg, float height, float margin)
		{
			var msgName = CuiHelper.GetGuid();

			#region Key

			var key = msg.Key;

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1",
					AnchorMax = "0 1",
					OffsetMin = $"10 {ySwitch - height}",
					OffsetMax = $"50 {ySwitch}"
				},
				Image =
				{
					Color = "0 0 0 0"
				}
			}, parent, msgName + ".Key");

			CreateOutLine(ref container, msgName + ".Key", _config.UI.Color1.Get);

			container.Add(new CuiElement
			{
				Parent = msgName + ".Key",
				Components =
				{
					new CuiInputFieldComponent
					{
						FontSize = 12,
						Align = TextAnchor.MiddleLeft,
						Command = $"{command} key ",
						Color = "1 1 1 0.65",
						CharsLimit = 150,
						NeedsKeyboard = true,
						Text = $"{key}"
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "10 0", OffsetMax = "0 0"
					}
				}
			});

			#endregion

			#region Value

			var msgValue = msg.Value;

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1",
					AnchorMax = "0 1",
					OffsetMin = $"60 {ySwitch - height}",
					OffsetMax = $"160 {ySwitch}"
				},
				Image =
				{
					Color = "0 0 0 0"
				}
			}, parent, msgName + ".Value");

			CreateOutLine(ref container, msgName + ".Value", _config.UI.Color1.Get);

			container.Add(new CuiElement
			{
				Parent = msgName + ".Value",
				Components =
				{
					new CuiInputFieldComponent
					{
						FontSize = 12,
						Align = TextAnchor.MiddleLeft,
						Command = $"{command} value ",
						Color = "1 1 1 0.65",
						CharsLimit = 150,
						NeedsKeyboard = true,
						Text = $"{msgValue}"
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "10 0", OffsetMax = "0 0"
					}
				}
			});

			#endregion
		}

		private void CheckBoxUi(ref CuiElementContainer container, string parent, string name, string aMin, string aMax,
			string oMin, string oMax, bool enabled,
			string command, string text)
		{
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

			CreateOutLine(ref container, name, _config.UI.Color2.Get, 1);

			if (enabled)
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1"
					},
					Image = {Color = _config.UI.Color2.Get}
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
					Color = _config.UI.Color8.Get
				}
			}, name);
		}

		private void EditFieldUi(ref CuiElementContainer container,
			string parent,
			string name,
			string oMin,
			string oMax,
			string command,
			KeyValuePair<string, object> obj)
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
					OffsetMin = "0 -20", OffsetMax = "0 0"
				},
				Text =
				{
					Text = $"{obj.Key}".Replace("_", " "),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = _config.UI.Color8.Get
				}
			}, name);

			container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "0 0", OffsetMax = "0 -20"
					},
					Image = {Color = "0 0 0 0"}
				}, name, $"{name}.Value");

			CreateOutLine(ref container, $"{name}.Value", _config.UI.Color1.Get);

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
						Color = "1 1 1 0.65",
						CharsLimit = 150,
						NeedsKeyboard = true,
						Text = $"{obj.Value}"
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "10 0", OffsetMax = "0 0"
					}
				}
			});
		}

		private void EditTextField(ref CuiElementContainer container,
			object objectInfi,
			FieldInfo field,
			string parent, string name,
			string aMin, string aMax, string oMin, string oMax,
			string command)
		{
			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = aMin,
					AnchorMax = aMax,
					OffsetMin = oMin,
					OffsetMax = oMax
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
					OffsetMin = "0 -20", OffsetMax = "0 0"
				},
				Text =
				{
					Text = $"{field.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName ?? "UKNOWN"}",
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = _config.UI.Color8.Get
				}
			}, name);

			container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "0 0", OffsetMax = "0 -20"
					},
					Image = {Color = "0 0 0 0"}
				}, name, $"{name}.Value");

			CreateOutLine(ref container, $"{name}.Value", _config.UI.Color1.Get);

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
						Color = "1 1 1 0.65",
						CharsLimit = 150,
						NeedsKeyboard = true,
						Text = $"{field.GetValue(objectInfi)}"
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "10 0", OffsetMax = "0 0"
					}
				}
			});
		}

		private void EditBoolField(
			BasePlayer player,
			ref CuiElementContainer container,
			object objectInfi,
			FieldInfo field,
			string parent, string name,
			string aMin, string aMax, string oMin, string oMax,
			string command)
		{
			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = aMin,
					AnchorMax = aMax,
					OffsetMin = oMin,
					OffsetMax = oMax
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
					OffsetMin = "0 -20", OffsetMax = "0 0"
				},
				Text =
				{
					Text = $"{field.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName ?? "UKNOWN"}",
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = _config.UI.Color8.Get
				}
			}, name);

			container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "0 0", OffsetMax = "0 -20"
					},
					Image = {Color = "0 0 0 0"}
				}, name, $"{name}.Value");

			var boolValue = Convert.ToBoolean(field.GetValue(objectInfi));

			container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "0 0", OffsetMax = "-10 0"
					},
					Text =
					{
						Text = boolValue ? Msg(player, BtnBoolON) : Msg(player, BtnBoolOFF),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 14,
						Color = _config.UI.Color8.Get
					},
					Button =
					{
						Color = (boolValue ? _config.UI.Color2 : _config.UI.Color9).Get,
						Command = boolValue ? $"{command} false" : $"{command} true"
					}
				}, $"{name}.Value");
		}

		private void EditFieldLocalization(
			BasePlayer player,
			ref CuiElementContainer container,
			ref FieldInfo field,
			object fieldObject,
			string parent, string name,
			string command,
			ref float ySwitch)
		{
			if (string.IsNullOrEmpty(name))
				name = CuiHelper.GetGuid();

			var fields = fieldObject.GetType().GetFields(bindingFlags).ToList();

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = $"10 {ySwitch - 20f}",
					OffsetMax = $"100 {ySwitch}"
				},
				Text =
				{
					Text = $"{field.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName ?? "UKNOWN"}",
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = _config.UI.Color8.Get
				}
			}, parent, name);

			#region Enabled

			var enabledField = fields.Find(fld => fld.Name == "Enabled");
			if (enabledField != null)
			{
				var value = Convert.ToBoolean(enabledField.GetValue(fieldObject));

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "0 1",
						OffsetMin = "65 0",
						OffsetMax = "90 0"
					},
					Text =
					{
						Text = Msg(player, value ? BtnBoolON : BtnBoolOFF),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = _config.UI.Color8.Get
					},
					Button =
					{
						Color = (value ? _config.UI.Color2 : _config.UI.Color9).Get,
						Command = $"{command} {enabledField.Name} {!value}"
					}
				}, name);
			}

			#endregion

			ySwitch -= 25f;

			#region Fields

			var constXSwitch = 10f;
			var xSwitch = constXSwitch;

			var width = 150f;
			var height = 50f;
			var margin = 5f;

			var itemsOnString = 3;

			var element = 0;

			#region Dictionary

			var fieldMessages = fields.Find(fld => fld.Name == "Messages");
			if (fieldMessages != null)
			{
				var value = fieldMessages.GetValue(fieldObject);
				if (value is Dictionary<string, string> messages)
				{
					height = 20f;

					foreach (var msg in messages)
					{
						FieldLocalizationMessage(ref container, parent,
							$"{command} {fieldMessages.Name} {msg.GetHashCode()}", ref ySwitch, msg, height,
							margin);

						ySwitch = ySwitch - height - margin;
					}

					FieldLocalizationMessage(ref container, parent, $"{command} {fieldMessages.Name} {0}",
						ref ySwitch, new KeyValuePair<string, string>(), height, margin);

					ySwitch = ySwitch - height - margin;
				}
			}

			#endregion

			#region Text Fields

			height = 50f;

			var textFields = fields.FindAll(x => x.FieldType == typeof(string) ||
			                                     x.FieldType == typeof(double) ||
			                                     x.FieldType == typeof(float) ||
			                                     x.FieldType == typeof(int));

			foreach (var textField in textFields)
			{
				EditTextField(ref container,
					fieldObject,
					textField,
					parent,
					CuiHelper.GetGuid(),
					"0 1", "0 1",
					$"{xSwitch} {ySwitch - height}",
					$"{xSwitch + width} {ySwitch}",
					$"{command} {textField.Name} "
				);

				if (++element % itemsOnString == 0)
				{
					xSwitch = constXSwitch;
					ySwitch = ySwitch - height - margin;
				}
				else
				{
					xSwitch += width + margin;
				}
			}

			#endregion

			#endregion
		}

		#endregion

		#endregion

		#region Helpers

		private static void UpdateUI(BasePlayer player, Action<CuiElementContainer> callback)
		{
			var container = new CuiElementContainer();

			callback?.Invoke(container);

			CuiHelper.AddUi(player, container);
		}

		#endregion

		#endregion

		#region Utils

		private void CheckInitializationStatus()
		{
			if (_initializedStatus.status == false && string.IsNullOrWhiteSpace(_initializedStatus.message))
			{
				_initializedStatus = (true, null);
			}
			else
			{
				PrintError(ConvertInitializedStatus());

				InitInstaller();
			}
		}
		
		private string ConvertInitializedStatus()
		{
			switch (_initializedStatus.message)
			{
				case "not_installed_template":
					return $"No template is installed in the plugin. To install the plugin, run the command /shop.install. You must have the '{PermAdmin}' permission to execute this command.";

				case "not_installed_image_library":
					return "There is no image library installed in the plugin! Install the \"ImageLibrary\" plugin on the server! URL: https://umod.org/plugins/image-library";
				
				default:
					return $"Unknown error: {_initializedStatus.message}";
			}
		}

		private class EncryptDecrypt
		{
			public static string Decrypt(string cipherText, string key)
			{
				var iv = new byte[16];
				var buffer = Convert.FromBase64String(cipherText);

				using var aes = Aes.Create();
				aes.Key = Convert.FromBase64String(key);
				aes.IV = iv;

				var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

				using var memoryStream = new MemoryStream(buffer);
				using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
				{
					using (var streamReader = new StreamReader(cryptoStream))
					{
						return streamReader.ReadToEnd();
					}
				}
			}
		}
		
		private string GetItemDisplayNameFromLangAPI(string shortName, string displayName, string userID)
		{
			if (LangAPI != null && LangAPI.Call<bool>("IsDefaultDisplayName", displayName))
			{
				return LangAPI.Call<string>("GetItemDisplayName", shortName, displayName, userID) ?? displayName;
			}
			return displayName;
		}
		
		#region Transfer Helpers

		private static int GetAvailablePlayersCount(ulong player)
		{
			return Mathf.Max(BasePlayer.activePlayerList.Count - 1, 0);
		}

		private static List<BasePlayer> GetAvailablePlayersToTransfer(ulong player, int skip, int take)
		{
			var list = new List<BasePlayer>();
			
			for (var index = 0; index < BasePlayer.activePlayerList.Count; index++)
			{
				if (index < skip) continue;
				
				var targetPlayer = BasePlayer.activePlayerList[index];
				if (targetPlayer == null || targetPlayer.userID == player || !targetPlayer.IsConnected) continue;
				
				list.Add(targetPlayer);
				
				if (list.Count >= take) break;
			}

			return list;
		}

		#endregion

		#region Select Item Helpers

		private void GetAvailableItemsToSelect(string selectedCategory, int page, string input, int totalAmount,
			out bool canSearch,
			out int itemsAmount,
			out List<(int itemID, string shortName)> Items)
		{			
			canSearch = !string.IsNullOrEmpty(input) && input.Length > 2;

			if (canSearch)
			{
				var tempItems = _itemsCategories
					.SelectMany(x => x.Value)
					.Where(x => x.shortName.StartsWith(input) || x.shortName.Contains(input) ||
					            x.shortName.EndsWith(input));

				itemsAmount = tempItems.Count;
				Items = tempItems.SkipAndTake(page * totalAmount, totalAmount);
			}
			else
			{
				if (_itemsCategories.TryGetValue(selectedCategory, out var resultItems))
				{
					itemsAmount = resultItems.Count;
					Items = resultItems.SkipAndTake(page * totalAmount, totalAmount);
				}
				else
				{
					itemsAmount = 0;
					Items = new List<(int itemID, string shortName)>();
				}
			}
		}

		#endregion
		
		private void CloseShopUI(BasePlayer player)
		{
			_itemsToUpdate.Remove(player.userID);
			_openedShopsNPC.Remove(player.userID);
			_openedCustomVending.Remove(player.userID);
			
			RemoveOpenedShop(player.userID);
			CheckUpdateController();
			
			_itemEditing.Remove(player.userID);
		}

		private void TryBuyItems(BasePlayer player, bool again = false)
		{
#if TESTING
			var line = 0;
			try
			{
				line = 0;
#endif

				var playerCart = GetPlayerCart(player.userID);
				if (playerCart == null) return;

#if TESTING
				line = 1;
#endif

				var price = playerCart.GetCartPrice(player, again);
				if (price < 0.0) return;

#if TESTING
				line = 2;
#endif

				if (_config.BlockNoEscape && NoEscape_IsBlocked(player))
				{
					ErrorUi(player, Msg(player, BuyRaidBlocked));
					return;
				}

#if TESTING
				line = 3;
#endif

				if (_config.WipeCooldown)
				{
					var timeLeft = Mathf.RoundToInt(_config.WipeCooldownTimer - GetSecondsFromWipe());
					if (timeLeft > 0)
					{
						ErrorUi(player,
							Msg(player, BuyWipeCooldown,
								FormatShortTime(timeLeft)));
						return;
					}
				}

#if TESTING
				line = 4;
#endif

				if (_config.RespawnCooldown)
				{
					var timeLeft = Mathf.RoundToInt(_config.RespawnCooldownTimer - player.TimeAlive());
					if (timeLeft > 0)
					{
						ErrorUi(player,
							Msg(player, BuyRespawnCooldown,
								FormatShortTime(timeLeft)));
						return;
					}
				}

#if TESTING
				line = 5;
#endif

				var totalAmount = playerCart.GetCartItemsStacksAmount();
				var slots = player.inventory.containerBelt.capacity -
				            player.inventory.containerBelt.itemList.Count +
				            (player.inventory.containerMain.capacity -
				             player.inventory.containerMain.itemList.Count);
				if (slots < totalAmount)
				{
					ErrorUi(player, Msg(player, NotEnoughSpace));
					return;
				}
#if TESTING
				line = 6;
#endif
				var items = playerCart.GetShopItems(again);
				if (items.Any(x =>
				    {
					    var limit = GetLimit(player, x.Key, true);
					    if (limit <= 0)
					    {
						    ErrorUi(player, Msg(player, BuyLimitReached, x.Key.GetPublicTitle(player)));
						    return true;
					    }

					    limit = GetLimit(player, x.Key, true, true);
					    if (limit <= 0)
					    {
						    ErrorUi(player, Msg(player, DailyBuyLimitReached, x.Key.GetPublicTitle(player)));
						    return true;
					    }

					    return false;
				    }))
					return;
#if TESTING
				line = 7;
#endif
				if (!player.HasPermission(PermFreeBypass) &&
				    !GetPlayerEconomy(player).RemoveBalance(player, price))
				{
					ErrorUi(player, Msg(player, NotMoney));
					return;
				}

#if TESTING
				line = 8;
#endif
				
				ServerMgr.Instance.StartCoroutine(GiveCartItems(player, items.ToList(), price));

#if TESTING
				line = 9;
#endif

				if (!again)
				{
					(playerCart as PlayerCartData)?.SaveLastPurchaseItems();

#if TESTING
					line = 10;
#endif
					playerCart.ClearCartItems();
				}

#if TESTING
				line = 11;
#endif
				CuiHelper.DestroyUi(player, Layer);

				if (!again)
					if (!_config.BuyAgain.Enabled)
						playerCart.ClearCartItems();

				if (_enabledServerPanel) ServerPanel?.Call("API_OnServerPanelCallClose", player);

				CloseShopUI(player);

				_config?.Notifications?.ShowNotify(player, ReceivedItems, 0);

#if TESTING
			}
			catch (Exception e)
			{
				PrintError($"Error on line {line}: {e.Message}");
			}
#endif
		}

		private static Item[] PlayerItems(BasePlayer player)
		{
			return _config.SellContainers.Enabled
				? _config.SellContainers.AllItems(player)
				: player.inventory.AllItems();
		}

		private void LoadNPCs()
		{
			foreach (var check in _config.NPCs) check.Value.BotID = check.Key;
		}

		private readonly Dictionary<int, EconomyEntry> _additionalEconomics = new();

		private readonly List<AdditionalEconomy> _economics = new();

		private void LoadEconomics()
		{
			_economics.Clear();
			
			_config.AdditionalEconomics.FindAll(x => x.Enabled)
				.ForEach(x =>
				{
					if (x.ID == 0 || !_additionalEconomics.TryAdd(x.ID, x))
						PrintError($"Additional economy caching error. There are several economies with ID {x.ID}");
				});

			_economics.Add(new AdditionalEconomy(_config.Economy));
			_economics.AddRange(_config.AdditionalEconomics.FindAll(x => x.Enabled));
		}

		private bool NoEscape_IsBlocked(BasePlayer player)
		{
			return Convert.ToBoolean(NoEscape?.Call("IsBlocked", player));
		}

		private IEnumerator GiveCartItems(BasePlayer player, List<KeyValuePair<ShopItem, int>> items, double price)
		{
			var logItems = Pool.GetList<string>();

			var i = 0;

			foreach (var cartItem in items)
			{
				logItems.Add(cartItem.Key.ToString());
				
				cartItem.Key?.Get(player, cartItem.Value);

				SetCooldown(player, cartItem.Key, true);
				UseLimit(player, cartItem.Key, true, cartItem.Value);
				UseLimit(player, cartItem.Key, true, cartItem.Value, true);

				if (i++ % _itemsPerTick == 0)
					yield return CoroutineEx.waitForEndOfFrame;
			}

			Log(LogType.Buy, LogBuyItems, player.displayName, player.UserIDString,
				price, string.Join(", ", logItems));

			Pool.FreeList(ref logItems);
		}

		private NPCShop GetNPCShop(ulong playerID)
		{
			return _openedShopsNPC.GetValueOrDefault(playerID);
		}

		private bool TryGetNPCShop(ulong playerID, out NPCShop npcShop)
		{
			return _openedShopsNPC.TryGetValue(playerID, out npcShop);
		}

		private static RaycastHit? GetLookHitLayer(BasePlayer player, float maxDistance = 5f, int layerMask = -5)
		{
			return !Physics.Raycast(player.eyes.HeadRay(), out var hitInfo, maxDistance, layerMask,
				QueryTriggerInteraction.UseGlobal)
				? null
				: hitInfo;
		}

		private static RaycastHit? GetLookHit(BasePlayer player, float maxDistance = 5f)
		{
			return !Physics.Raycast(player.eyes.HeadRay(), out var hitInfo, maxDistance)
				? null
				: hitInfo;
		}

		private static VendingMachine GetLookVM(BasePlayer player)
		{
			return GetLookHit(player)?.GetEntity() as VendingMachine;
		}

		private static BasePlayer GetLookNPC(BasePlayer player)
		{
			return GetLookHitLayer(player, layerMask: LayerMask.GetMask("Player (Server)"))?.GetEntity() as BasePlayer;
		}

		private static Vector3 GetLookPoint(BasePlayer player)
		{
			return GetLookHit(player, 10f)?.point ?? player.ServerPosition;
		}

		private void RegisterPermissions()
		{
			var permissions = new HashSet<string>();

			_itemsData.Shop.ForEach(category =>
			{
				if (!string.IsNullOrEmpty(category.Permission))
					permissions.Add(category.Permission);

				foreach (var item in category.Items)
				{
					if (!string.IsNullOrEmpty(item.Permission))
						permissions.Add(item.Permission);

					if (item.UseCustomDiscount)
						foreach (var discountPermission in item.Discount.Keys)
							if (!string.IsNullOrEmpty(discountPermission))
								permissions.Add(discountPermission);
				}
			});

			foreach (var shop in _config.NPCs.Values)
				if (!string.IsNullOrEmpty(shop.Permission))
					permissions.Add(shop.Permission);

			foreach (var shop in _config.CustomVending.Values)
				if (!string.IsNullOrEmpty(shop.Permission))
					permissions.Add(shop.Permission);

			foreach (var discountPermission in _config.Discount.Discount.Keys)
				if (!string.IsNullOrEmpty(discountPermission))
					permissions.Add(discountPermission);

			permissions.Add(PermAdmin);
			permissions.Add(PermFreeBypass);
			permissions.Add(PermSetVM);
			permissions.Add(PermSetNPC);

			if (!string.IsNullOrEmpty(_config.BuyAgain.Permission))
				permissions.Add(_config.BuyAgain.Permission);

			foreach (var perm in permissions)
			{
				var lowerPerm = perm.ToLower();
				if (permission.PermissionExists(lowerPerm)) continue;

				permission.RegisterPermission(lowerPerm, this);
			}

			permissions.Clear();
		}

		private void RegisterCommands()
		{
			AddCovalenceCommand(_config.Commands, nameof(CmdShopOpen));

			AddCovalenceCommand("shop.setvm", nameof(CmdSetCustomVM));

			AddCovalenceCommand("shop.setnpc", nameof(CmdSetShopNPC));
		}

		private void CheckUpdateController()
		{
			if (_itemsToUpdate.Count == 0)
			{
				_updateController?.Destroy();
				return;
			}

			if (_updateController == null && _shopItems.Any(x => x.Value.BuyCooldown > 0 || x.Value.SellCooldown > 0))
				_updateController = timer.Every(1, ItemsUpdateController);
		}

		private void CacheImages()
		{
			foreach (var image in _shopItems.Values
				         .Select(shopItem =>
					         !string.IsNullOrEmpty(shopItem.Image) ? shopItem.Image : shopItem.ShortName))
				_images.Add(image);
		}

		private void LoadPlayers()
		{
			foreach (var player in BasePlayer.activePlayerList)
				OnPlayerConnected(player);
		}

		#region Custom Vending

		private readonly Dictionary<ulong, CustomVendingEntry> _openedCustomVending = new();

		private CustomVendingEntry GetCustomVending(ulong playerId)
		{
			return _openedCustomVending.GetValueOrDefault(playerId);
		}

		private bool TryGetCustomVending(ulong playerId, out CustomVendingEntry customVM)
		{
			return _openedCustomVending.TryGetValue(playerId, out customVM);
		}

		private void LoadCustomVMs()
		{
			var anyRemoved = false;

			_config.CustomVending.Keys.ToList().ForEach(wb =>
			{
				if (CheckCustomVending(wb))
					anyRemoved = true;
			});

			if (anyRemoved)
				SaveConfig();

			Subscribe(nameof(CanLootEntity));
		}

		private bool CheckCustomVending(ulong netId)
		{
			return BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as VendingMachine == null &&
			       _config.CustomVending.Remove(netId);
		}

		#endregion

		#region Avatar

		private readonly Regex _regex = new(@"<avatarFull><!\[CDATA\[(.*)\]\]></avatarFull>");

		private void GetAvatar(ulong userId, Action<string> callback)
		{
			if (callback == null) return;

			webrequest.Enqueue($"http://steamcommunity.com/profiles/{userId}?xml=1", null, (code, response) =>
			{
				if (code != 200 || response == null)
					return;

				var avatar = _regex.Match(response).Groups[1].ToString();
				if (string.IsNullOrEmpty(avatar))
					return;

				callback.Invoke(avatar);
			}, this);
		}

		#endregion

		private int GetSecondsFromWipe()
		{
			return (int) DateTime.UtcNow
				.Subtract(SaveRestore.SaveCreatedTime.ToUniversalTime()).TotalSeconds;
		}

		private static string FormatShortTime(int seconds)
		{
			return TimeSpan.FromSeconds(seconds).ToShortString();
		}

		private bool InDuel(BasePlayer player)
		{
			return Convert.ToBoolean(Duel?.Call("IsPlayerOnActiveDuel", player)) ||
			       Convert.ToBoolean(Duelist?.Call("inEvent", player));
		}

		private bool IsAdmin(BasePlayer player)
		{
			return player != null && ((player.IsAdmin && _config.FlagAdmin) || player.HasPermission(PermAdmin));
		}

		private int GetId()
		{
			var result = -1;

			do
			{
				var val = Random.Range(int.MinValue, int.MaxValue);

				if (!_shopItems.ContainsKey(val))
					result = val;
			} while (result == -1);

			return result;
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

		private IEnumerator LoadImages(BasePlayer player)
		{
			foreach (var image in _images)
			{
				if (player == null || !player.IsConnected) continue;

				SendImage(player, image);

				yield return CoroutineEx.waitForSeconds(_config.ImagesDelay);
			}
		}

		private ShopCategory FindCategoryByName(string name)
		{
			return _itemsData.Shop.Find(cat => cat.Title == name);
		}

		private ShopCategory FindCategoryById(int id)
		{
			return _itemsData.Shop.Find(cat => cat.ID == id);
		}

		private ShopItem FindItemById(int id)
		{
			return _shopItems.GetValueOrDefault(id);
		}

		private bool TryFindItemById(int id, out ShopItem shopItem)
		{
			return _shopItems.TryGetValue(id, out shopItem);
		}

		private void FillCategories()
		{
			_itemsData.Shop.Clear();

			var sw = Stopwatch.StartNew();

			var dict = new Dictionary<string, List<ItemDefinition>>();

			ItemManager.itemList.ForEach(item =>
			{
				var itemCategory = item.category.ToString();

				if (dict.TryGetValue(itemCategory, out var definitions))
					definitions.Add(item);
				else
					dict.Add(itemCategory, new List<ItemDefinition> {item});
			});

			var id = 0;

			var category = new ShopCategory
			{
				Enabled = true,
				CategoryType = ShopCategory.Type.Favorite,
				Title = "Favorites",
				Localization = new Localization
				{
					Enabled = false,
					Messages = new Dictionary<string, string>
					{
						["en"] = "Favorites",
						["fr"] = "Favoris"
					}
				},
				Permission = string.Empty,
				SortType = Configuration.SortType.None,
				Items = new List<ShopItem>()
			};

			_itemsData.Shop.Add(category);

			foreach (var check in dict)
			{
				category = new ShopCategory
				{
					Enabled = true,
					CategoryType = ShopCategory.Type.None,
					Title = check.Key,
					Localization = new Localization
					{
						Enabled = false,
						Messages = new Dictionary<string, string>
						{
							["en"] = check.Key,
							["fr"] = check.Key
						}
					},
					Permission = string.Empty,
					SortType = Configuration.SortType.None,
					Items = new List<ShopItem>()
				};

				check.Value
					.FindAll(itemDefinition => itemDefinition.shortname != "blueprintbase")
					.ForEach(
						itemDefinition =>
						{
							var itemCost = Math.Round(GetItemCost(itemDefinition));

							category.Items.Add(ShopItem.GetDefault(id++, itemCost, itemDefinition.shortname));
						});

				category.SortItems();

				_itemsData.Shop.Add(category);
			}

			SaveItemsData();

			sw.Stop();
			PrintWarning($"Shop was filled with items in {sw.ElapsedMilliseconds} ms!");
		}

		private double GetItemCost(ItemDefinition itemDefinition)
		{
			return ItemCostCalculator != null ? Convert.ToDouble(ItemCostCalculator?.Call("GetItemCost", itemDefinition)) : 100;
		}

		private void CheckOnDuplicates()
		{
			if (_itemsData.Shop.Count == 0) return;

			var items = new HashSet<int>();
			var duplicates = new HashSet<int>();

			_itemsData.Shop.ForEach(shopCategory =>
			{
				shopCategory.Items.ForEach(item =>
				{
					if (!items.Add(item.ID))
						duplicates.Add(item.ID);
				});
			});

			if (duplicates.Count > 0)
				PrintError(
					$"Matching item IDs found (Shop): {string.Join(", ", duplicates.Select(x => x.ToString()))}");
		}

		private void LoadItems()
		{
			_shopItems.Clear();

			#region Default Items

			ItemManager.itemList.ForEach(item =>
			{
				var itemCategory = item.category.ToString();
				
				if (_itemsCategories.ContainsKey(itemCategory))
				{
					if (!_itemsCategories[itemCategory].Contains((item.itemid, item.shortname)))
					{
						_itemsCategories[itemCategory].Add((item.itemid, item.shortname));
					}
				}
				else
				{
					_itemsCategories.Add(itemCategory, new List<(int itemID, string shortName)>
					{
						(item.itemid, item.shortname)
					});
				}
			});

			#endregion

			_itemsData.Shop.ForEach(category =>
			{
				if (!category.Enabled)
					return;

				if (category.CategoryType == ShopCategory.Type.Favorite)
					_isEnabledFavorites = true;

				category.LoadIDs();
			});
		}

		private List<ShopCategory> GetCategories(BasePlayer player)
		{
			var npcShop = GetNPCShop(player.userID);

			var customVM = GetCustomVending(player.userID);

			return _itemsData.Shop.FindAll(shopCategory =>
			{
				var enabled = shopCategory.Enabled || _showAllCategories.Contains(player.userID);
				if (!enabled)
					return false;

				var hasPermissions = string.IsNullOrEmpty(shopCategory.Permission) ||
				                     player.HasPermission(shopCategory.Permission);
				if (!hasPermissions)
					return false;

				if (npcShop != null)
					return npcShop.Categories.Contains("*") || npcShop.Categories.Contains(shopCategory.Title);

				if (customVM != null)
					return customVM.Categories.Contains("*") ||
					       customVM.Categories.Contains(shopCategory.GetTitle(player));

				if (shopCategory.CategoryType == ShopCategory.Type.Hided)
					return false;

				return true;
			});
		}

		private (List<ShopItem> items, int totalItemsCount) GetPaginationShopItems(BasePlayer player)
		{
			var shop = GetShop(player);
			
			var isSearch = shop.HasSearch();
			
			var shopItems = isSearch
				? SearchItem(player, shop.search)
				: shop.GetSelectedShopCategory()?.GetShopItems(player) ?? new List<ShopItem>();

            if (shop.GetUI().ShopContent.Content.UseScrollToListItems)
	            return (shopItems, shopItems.Count);
			
            var shopTotalItemsAmount = GetShopTotalItemsAmount(player);
			return (shopItems.SkipAndTake((isSearch ? shop.currentSearchPage : shop.currentShopPage) * shopTotalItemsAmount, shopTotalItemsAmount), shopItems.Count);
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

		private static int ItemCount(Item[] items, ShopItem shopItem)
		{
			return Array.FindAll(items,
					shopItem.CanTake)
				.Sum(item => item.amount);
		}
		
		private static void Take(Item[] itemList, ShopItem shopItem, int amountToTake)
		{
			if (amountToTake == 0) return;
			var takenAmount = 0;

			var itemsToTake = Pool.GetList<Item>();

			foreach (var item in itemList)
			{
				if (!shopItem.CanTake(item)) continue;
				
				var remainingAmount = amountToTake - takenAmount;
				if (remainingAmount <= 0) break;
				
				if (item.amount > remainingAmount)
				{
					item.MarkDirty();
					item.amount -= remainingAmount;
					break;
				}

				if (item.amount <= remainingAmount)
				{
					takenAmount += item.amount;
					itemsToTake.Add(item);
				}

				if (takenAmount == amountToTake)
					break;
			}

			foreach (var itemToTake in itemsToTake)
				itemToTake.RemoveFromContainer();

			Pool.FreeList(ref itemsToTake);
		}

		private static int ItemCount(Item[] items, string shortname, ulong skin)
		{
			return Array.FindAll(items,
					item => item.info.shortname == shortname && !item.isBroken && (skin == 0 || item.skin == skin))
				.Sum(item => item.amount);
		}

		private static void Take(IEnumerable<Item> itemList, string shortname, ulong skinId, int iAmount)
		{
			var num1 = 0;
			if (iAmount == 0) return;

			var list = Pool.GetList<Item>();

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

			Pool.FreeList(ref list);
		}

		private CartData GetPlayerCart(ulong playerID)
		{
			var data = PlayerData.GetOrCreate(playerID);

			if (TryGetNPCShop(playerID, out var npcShop))
			{
				if (!data.NPCCart.Carts.TryGetValue(npcShop.BotID, out var cartData))
					data.NPCCart.Carts.TryAdd(npcShop.BotID, cartData = new CartData());

				return cartData;
			}

			return data.PlayerCart ?? (data.PlayerCart = new PlayerCartData());
		}

		private string FormatShortTime(BasePlayer player, TimeSpan time)
		{
			if (time.Days != 0)
				return Msg(player, DaysFormat, time.Days);

			if (time.Hours != 0)
				return Msg(player, HoursFormat, time.Hours);

			if (time.Minutes != 0)
				return Msg(player, MinutesFormat, time.Minutes);

			if (time.Seconds != 0)
				return Msg(player, SecondsFormat, time.Seconds);

			return string.Empty;
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
		
		private void SendImage(BasePlayer player, string imageName)
		{
#if CARBON
			if (!HasImage(imageName) || player?.net?.connection == null)
				return;

			var crc = uint.Parse(GetImage(imageName));
			var array = FileStorage.server.Get(crc, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);
			if (array == null)
				return;

			CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo(player.net.connection)
			{
				channel = 2,
				method = Network.SendMethod.Reliable
			}, null, "CL_ReceiveFilePng", crc, (uint)array.Length, array);
#else
			ImageLibrary?.Call("SendImage", player, imageName);
#endif
		}
		
		private void LoadImages()
		{
#if CARBON
			imageDatabase = BaseModule.GetModule<ImageDatabaseModule>();
#endif
			var imagesList = new Dictionary<string, string>();

			_itemsData.Shop.ForEach(category =>
			{
				category.Items.ForEach(item =>
				{
					if (!string.IsNullOrEmpty(item.Image))
						imagesList.TryAdd(item.Image, item.Image);
				});
			});

			if (_config.BuyAgain.Enabled)
				if (!string.IsNullOrEmpty(_config.BuyAgain.Image)
				    && !_config.BuyAgain.Image.Contains("assets/icons"))
					imagesList.TryAdd(_config.BuyAgain.Image, _config.BuyAgain.Image);

			RegisterImagesFromUI(imagesList);

#if CARBON
            imageDatabase.Queue(false, imagesList);
#else
			timer.In(1f, () =>
			{
				if (ImageLibrary is not {IsLoaded: true})
				{
					_initializedStatus = (false, "not_installed_image_library");
					return;
				}
				
				ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
			});
#endif
		}

		private static void RegisterImage(string image, ref Dictionary<string, string> imagesList)
		{
			if (!string.IsNullOrWhiteSpace(image) && image.IsURL())
				imagesList.TryAdd(image, image);
		}

		#endregion Images

		#endregion

		#region Cooldown

		private readonly Dictionary<ulong, List<ShopItem>> _itemsToUpdate = new();

		private readonly List<(ulong playerId, ShopItem item)> _itemsToRemove = new();
		
		private void ItemsUpdateController()
		{
			foreach (var openedShop in _openSHOP.Values)
			{
				var player = openedShop.Player;

				var selectedEconomy = API_GetShopPlayerSelectedEconomy(player.userID);
				
				UpdateUI(player, container =>
				{
					foreach (var shopItem in openedShop.ItemsToUpdate)
					{
						ShopItemButtonsUI(player, ref container, shopItem, selectedEconomy);

						if (HasCooldown(player.userID, shopItem, true) &&
						    HasCooldown(player.userID, shopItem, false))
							_itemsToRemove.Add((player.userID, shopItem));
					}
				});
			}
			
			_itemsToRemove.ForEach(item => RemoveCooldown(item.playerId, item.item));
			_itemsToRemove.Clear();

			CheckUpdateController();
		}

		#endregion

		#region Log

		private enum LogType
		{
			Buy,
			Sell
		}

		private void Log(LogType type, string key, params object[] obj)
		{
			Log(type.ToString(), key, obj);
		}

		private void Log(string filename, string key, params object[] obj)
		{
			var text = string.Format(lang.GetMessage(key, this), obj);
			
			if (_config.LogToConsole) Puts(text);

			if (_config.LogToFile) LogToFile(filename, $"[{DateTime.Now}] {text}", this);
		}

		#endregion

		#region API

		private int API_GetShopPlayerSelectedEconomy(ulong playerID)
		{
			return PlayerData.GetOrCreate(playerID)?.SelectedEconomy ?? 0;
		}
		
		private CuiElementContainer API_OpenPlugin(BasePlayer player)
		{
            var container  = new CuiElementContainer();
            
			if (_initializedStatus.status is false)
			{				
				if (_initializedStatus.message == null)
				{
					_config?.Notifications?.ShowNotify(player, UIMsgShopInInitialization, 1);
					return container;
				}
				
				_config?.Notifications?.ShowNotify(player, NoILError, 1);
				
				PrintError(ConvertInitializedStatus());
				return container;
			}

			RemoveOpenedShop(player.userID);

			var shop = GetShop(player, false);

			var shopUI = shop.GetUI();

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

			container.Add(
				shopUI.ShopContent.Background.GetImage(Layer + ".Background", Layer + ".Main", Layer + ".Main"));
			
			ShopHeaderUI(player, container);

			ShopContentUI(player, container);

			#endregion

			#region Categories

			ShopCategoriesUI(container, player);

			#endregion

			#region Cart

			ShopBasketUI(container, player);

			#endregion

			return container;
		}

#if TESTING

		public void API_SetShopTemplate(ShopUI shopUI, bool ifFullscreen = true)
		{
			if (ifFullscreen)
			{
				_uiData.IsFullscreenUISet = true;
				_uiData.FullscreenUI = shopUI;
			}
			else
			{
				 _uiData.IsInMenuUISet = false;
				_uiData.InMenuUI = shopUI;
			}
		}
#endif
		
		#endregion
		
		#region Lang

		private const string
			UIMsgNoItems = "UIMsgNoItems",
			UIBasketFooterItemsCountTitle = "UIBasketFooterItemsCountTitle",
			UIBasketFooterItemsCountValue = "UIBasketFooterItemsCountValue",
			UIBasketFooterItemsCostTitle = "UIBasketFooterItemsCostTitle",
			UIContentHeaderButtonToggleEconomy = "UIContentHeaderButtonToggleEconomy",
			UIShopActionAmountTitle = "UIShopActionAmountTitle",
			UIShopActionDescriptionTitle = "UIShopActionDescriptionTitle",
			UIShopItemDescriptionTitle = "UIShopItemDescriptionTitle",
			UIShopItemAmount = "UIShopItemAmount",
			UICategoriesAdminShowAllTitle = "UICategoriesAdminShowAllTitle",
			UIMsgShopInInitialization = "UIMsgShopInInitialization",

			MsgRemovedFromFavoriteItem = "MsgRemovedFromFavoriteItem",
			MsgAddedToFavoriteItem = "MsgAddedToFavoriteItem",
			MsgIsFavoriteItem = "MsgIsFavoriteItem",
			MsgNoFavoriteItem = "MsgNoFavoriteItem",
			NoILError = "NoILError",
			BtnBoolON = "BtnBoolON",
			BtnBoolOFF = "BtnBoolOFF",
			BtnEditCategory = "BtnEditCategory",
			BtnAddCategory = "BtnAddCategory",
			EditingCategoryTitle = "EditingCategoryTitle",
			BtnCalculate = "BtnCalculate",
			ItemPriceFree = "ItemPriceFree",
			NPCInstalled = "NPCInstalled",
			NPCNotFound = "NPCNotFound",
			EditBlueprint = "EditBlueprint",
			ChoiceEconomy = "ChoiceEconomy",
			VMInstalled = "VMInstalled",
			VMExists = "VMExists",
			VMNotFound = "VMNotFound",
			VMNotFoundCategories = "VMNotFoundCategories",
			ErrorSyntax = "ErrorSyntax",
			NoPermission = "NoPermission",
			NoTransferPlayers = "NoTransferPlayers",
			TitleMax = "TitleMax",
			TransferButton = "TransferButton",
			TransferTitle = "TransferTitle",
			SuccessfulTransfer = "SuccessfulTransfer",
			PlayerNotFound = "PlayerNotFound",
			SelectPlayerTitle = "SelectPlayerTitle",
			BuyWipeCooldown = "BuyWipeCooldown",
			SellWipeCooldown = "SellWipeCooldown",
			BuyRespawnCooldown = "BuyRespawnCooldown",
			SellRespawnCooldown = "SellRespawnCooldown",
			LogSellItem = "LogSellItem",
			LogBuyItems = "LogBuyItems",
			SkinBlocked = "SkinBlocked",
			NoUseDuel = "NoUseDuel",
			DailySellLimitReached = "DailySellLimitReached",
			DailyBuyLimitReached = "DailyBuyLimitReached",
			SellLimitReached = "SellLimitReached",
			BuyLimitReached = "BuyLimitReached",
			InfoTitle = "InfoTitle",
			BuyRaidBlocked = "BuyRaidBlocked",
			SellRaidBlocked = "SellRaidBlocked",
			DaysFormat = "DaysFormat",
			HoursFormat = "HoursFormat",
			MinutesFormat = "MinutesFormat",
			SecondsFormat = "SecondsFormat",
			NotEnoughSpace = "NotEnoughtSpace",
			NotMoney = "NotMoney",
			ReceivedItems = "GiveItem",
			BuyTitle = "BuyTitle",
			SellTitle = "SellTitle",
			PlusTitle = "PlusTitle",
			MinusTitle = "MinusTitle",
			RemoveTitle = "RemoveTitle",
			AmountTitle = "AmountTitle",
			NextTitle = "NextTitle",
			BackTitle = "BackTitle",
			ItemAmount = "ItemAmount",
			CloseButton = "CloseButton",
			YourBalance = "YourBalance",
			MainTitle = "MainTitle",
			CategoriesTitle = "CategoriesTitle",
			ShoppingBag = "ShoppingBag",
			PurchaseConfirmation = "PurchaseConfirmation",
			CancelTitle = "CancelTitle",
			ErrorClose = "ErrorClose",
			BtnSave = "BtnSave",
			ErrorMsg = "ErrorMsg",
			NotEnough = "NotEnough",
			Back = "Back",
			Next = "Next",
			ItemName = "ItemName",
			CmdName = "CmdName",
			RemoveItem = "RemoveItem",
			ItemSearch = "ItemSearch",
			PluginName = "PluginName",
			BtnSelect = "BtnSelect",
			BtnAddItem = "AddItem",
			EditingTitle = "EditingTitle",
			SearchTitle = "SearchTitle",
			BackPage = "BackPage",
			NextPage = "NextPage",
			SellCooldownTitle = "SellCooldownTitle",
			BuyCooldownTitle = "BuyCooldownTitle",
			BuyCooldownMessage = "BuyCooldownMessage",
			SellCooldownMessage = "SellCooldownMessage",
			BtnNext = "BtnNext",
			BtnBack = "BtnBack",
			SellNotify = "SellNotify";

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				[DaysFormat] = " {0} d. ",
				[HoursFormat] = " {0} h. ",
				[MinutesFormat] = " {0} m. ",
				[SecondsFormat] = " {0} s. ",
				[NotEnoughSpace] = "Not enought space",
				[NotMoney] = "You don't have enough money!",
				[ReceivedItems] = "All items received!",
				[BuyTitle] = "Buy",
				[SellTitle] = "Sell",
				[PlusTitle] = "+",
				[MinusTitle] = "-",
				[RemoveTitle] = "Remove",
				[AmountTitle] = "Amount {0} pcs",
				[BackTitle] = "Back",
				[NextTitle] = "Next",
				[ItemAmount] = "Amt.",
				[CloseButton] = "✕",
				[YourBalance] = "Your Balance",
				[MainTitle] = "Shop",
				[CategoriesTitle] = "Categories",
				[ShoppingBag] = "Basket",
				[PurchaseConfirmation] = "Purchase confirmation",
				[CancelTitle] = "Cancel",
				[ErrorClose] = "CLOSE",
				[ErrorMsg] = "XXX",
				[NotEnough] = "You don't have enough item!",
				[BtnSelect] = "Select",
				[EditingTitle] = "Item editing",
				[ItemSearch] = "Item search",
				[Back] = "Back",
				[Next] = "Next",
				[RemoveItem] = "✕",
				[BtnSave] = "Save",
				[ItemName] = "Item",
				[CmdName] = "Command",
				[PluginName] = "Plugin",
				[BtnAddItem] = "Add Item",
				[SearchTitle] = "Search...",
				[BackPage] = "<",
				[NextPage] = ">",
				[SellCooldownTitle] = "Cooldown",
				[BuyCooldownTitle] = "Cooldown",
				[BuyCooldownMessage] = "You cannot buy the '{0}' item! Wait {1}",
				[SellCooldownMessage] = "You cannot sell the '{0}' item! Wait {1}",
				[BtnBack] = "▲",
				[BtnNext] = "▼",
				[SellNotify] = "You have successfully sold {0} pcs of {1}",
				[BuyRaidBlocked] = "You can't buy while blocked!",
				[SellRaidBlocked] = "You can't sell while blocked!",
				[BuyWipeCooldown] = "You can't buy for another {0}!",
				[SellWipeCooldown] = "You can't sell for another {0}!",
				[BuyRespawnCooldown] = "You can't buy for another {0}!",
				[SellRespawnCooldown] = "You can't sell for another {0}!",
				[InfoTitle] = "i",
				[DailyBuyLimitReached] =
					"You cannot buy the '{0}'. You have reached the daily limit. Come back tomorrow!",
				[DailySellLimitReached] =
					"You cannot buy the '{0}'. You have reached the daily limit. Come back tomorrow!",
				[BuyLimitReached] = "You cannot buy the '{0}'. You have reached the limit",
				[SellLimitReached] = "You cannot sell the '{0}'. You have reached the limit",
				[NoUseDuel] = "You are in a duel. The use of the shop is blocked.",
				[SkinBlocked] = "Skin is blocked for sale",
				[LogBuyItems] = "Player {0} ({1}) bought items for {2}$: {3}.",
				[LogSellItem] = "Player {0} ({1}) sold item for {2}$: {3}.",
				[SelectPlayerTitle] = "Select player to transfer",
				[PlayerNotFound] = "Player not found",
				[SuccessfulTransfer] = "Transferred {0} to player '{1}'",
				[TransferTitle] = "Transfer",
				[TransferButton] = "Send money",
				[TitleMax] = "MAX",
				[NoTransferPlayers] = "Unfortunately, there are currently no players available for transfer",
				[NoPermission] = "You don't have the required permission",
				[ErrorSyntax] = "Syntax error! Use: /{0}",
				[VMNotFoundCategories] = "Categories not found!",
				[VMNotFound] = "Vending Machine not found!",
				[VMExists] = "This Vending Machine is already in the config!",
				[VMInstalled] = "You have successfully installed the custom Vending Machine!",
				[ChoiceEconomy] = "Choice of currency",
				[EditBlueprint] = "Blueprint",
				[NPCNotFound] = "NPC not found!",
				[NPCInstalled] = "You have successfully installed the custom NPC!",
				[ItemPriceFree] = "FREE",
				[BtnCalculate] = "Calculate",
				[EditingCategoryTitle] = "Category editing",
				[BtnAddCategory] = "+",
				[BtnEditCategory] = "✎",
				[BtnBoolOFF] = "OFF",
				[BtnBoolON] = "ON",
				[NoILError] = "The plugin does not work correctly, contact the administrator!",
				[MsgNoFavoriteItem] = "You can't remove this item from favorites because it is not a favorite",
				[MsgIsFavoriteItem] = "You cannot add this item to favorites because it is already a favorite",
				[MsgAddedToFavoriteItem] = "Item '{0}' has been added to your favorites!",
				[MsgRemovedFromFavoriteItem] = "Item '{0}' has been removed from favorites!",
				[UIMsgNoItems] = "Sorry, there are currently no items available",
				[UIBasketFooterItemsCountTitle] = "Items",
				[UIBasketFooterItemsCountValue] = "{0} pcs",
				[UIBasketFooterItemsCostTitle] = "Cost",
				[UIContentHeaderButtonToggleEconomy] = "▲",
				[UIShopActionAmountTitle] = "AMOUNT",
				[UIShopActionDescriptionTitle] = "DESCRIPTION",
				[UIShopItemDescriptionTitle] = "DESCRIPTION",
				[UIShopItemAmount] = "{0}",
				[UICategoriesAdminShowAllTitle] = "SHOW ALL",
				[UIMsgShopInInitialization] = "The plugin is currently initializing. Please wait a moment while the process completes.",
			}, this);

			lang.RegisterMessages(new Dictionary<string, string>
			{
				[DaysFormat] = " {0} д. ",
				[HoursFormat] = " {0} ч. ",
				[MinutesFormat] = " {0} м. ",
				[SecondsFormat] = " {0} с. ",
				[NotEnoughSpace] = "Недостаточно места",
				[NotMoney] = "У вас недостаточно денег!",
				[ReceivedItems] = "Все предметы получены!",
				[BuyTitle] = "Купить",
				[SellTitle] = "Продать",
				[PlusTitle] = "+",
				[MinusTitle] = "-",
				[RemoveTitle] = "Удалить",
				[AmountTitle] = "Кол-во {0} шт",
				[BackTitle] = "Назад",
				[NextTitle] = "Вперёд",
				[ItemAmount] = "Кол.",
				[CloseButton] = "✕",
				[YourBalance] = "Ваш Баланс",
				[MainTitle] = "Магазин",
				[CategoriesTitle] = "Категории",
				[ShoppingBag] = "Корзина",
				[PurchaseConfirmation] = "Подтверждение покупки",
				[CancelTitle] = "Отменить",
				[ErrorClose] = "ЗАКРЫТЬ",
				[ErrorMsg] = "XXX",
				[NotEnough] = "У вас недостаточно предметов!",
				[BtnSelect] = "Выбрать",
				[EditingTitle] = "Редактирование предмета",
				[ItemSearch] = "Поиск предмета",
				[Back] = "Назад",
				[Next] = "Вперёд",
				[RemoveItem] = "✕",
				[BtnSave] = "Сохранить",
				[ItemName] = "Предмет",
				[CmdName] = "Команда",
				[PluginName] = "Плагин",
				[BtnAddItem] = "Добавить предмет",
				[SearchTitle] = "Поиск...",
				[BackPage] = "<",
				[NextPage] = ">",
				[SellCooldownTitle] = "Осталось",
				[BuyCooldownTitle] = "Осталось",
				[BuyCooldownMessage] = "Вы не можете купить '{0}'! Подождите {1}",
				[SellCooldownMessage] = "Вы не можете продать '{0}'! Подождите {1}",
				[BtnBack] = "▲",
				[BtnNext] = "▼",
				[SellNotify] = "Вы успешно продали {0} шт за {1}",
				[BuyRaidBlocked] = "Вы не можете покупать во время блокировки рейда!",
				[SellRaidBlocked] = "Вы не можете продавать во время блокировки рейда!",
				[BuyWipeCooldown] = "Вы не можете покупать ещё {0}!",
				[SellWipeCooldown] = "Вы не можете продавать ещё  {0}!",
				[BuyRespawnCooldown] = "Вы не можете покупать ещё  {0}!",
				[SellRespawnCooldown] = "Вы не можете продавать ещё  {0}!",
				[InfoTitle] = "i",
				[DailyBuyLimitReached] =
					"Вы не можете купить '{0}'. Вы достигли дневного лимита. Возвращайтесь завтра!",
				[DailySellLimitReached] =
					"Вы не можете продать '{0}'. Вы достигли дневного лимита. Возвращайтесь завтра!",
				[BuyLimitReached] = "Вы не можете купить '{0}'. Вы достигли лимита",
				[SellLimitReached] = "Вы не можете продать '{0}'. Вы достигли лимита",
				[NoUseDuel] = "Вы на дуэли. Использование магазина запрещено.",
				[SkinBlocked] = "Скин запрещён для продажи",
				[LogBuyItems] = "Player {0} ({1}) bought items for {2}$: {3}.",
				[LogSellItem] = "Player {0} ({1}) sold item for {2}$: {3}.",
				[SelectPlayerTitle] = "Выберите игрока для перевода",
				[PlayerNotFound] = "Игрок не найден",
				[SuccessfulTransfer] = "Переведено {0} игроку '{1}'",
				[TransferTitle] = "Переводы",
				[TransferButton] = "Отправить",
				[TitleMax] = "MAX",
				[NoTransferPlayers] = "К сожалению, в настоящее время нет игроков, доступных для перевода",
				[NoPermission] = "У вас нет необходимого разрешения",
				[ErrorSyntax] = "Syntax error! Use: /{0}",
				[VMNotFoundCategories] = "Категории не найдены!",
				[VMNotFound] = "Торговый Автомат не найден!",
				[VMExists] = "Этот торговый автомат уже в конфиге!",
				[VMInstalled] = "Вы успешно установили кастомный торговый автомат!",
				[ChoiceEconomy] = "Выбор валюты",
				[EditBlueprint] = "Чертёж",
				[NPCNotFound] = "NPC не найден!",
				[NPCInstalled] = "Вы успешно установили магазин NPC!",
				[ItemPriceFree] = "FREE",
				[BtnCalculate] = "Рассчитать",
				[EditingCategoryTitle] = "Редактирование категории",
				[BtnAddCategory] = "+",
				[BtnEditCategory] = "✎",
				[BtnBoolOFF] = "ВЫКЛ",
				[BtnBoolON] = "ВКЛ",
				[NoILError] = "Плагин работает некорректно, свяжитесь с администратором!",
				[MsgNoFavoriteItem] =
					"Вы не можете удалить этот предмет из избранного, потому что он не является избранным",
				[MsgIsFavoriteItem] =
					"Вы не можете добавить этот предмет в избранное, потому что он уже является избранным",
				[MsgAddedToFavoriteItem] = "Предмет '{0}' добавлен в избранное!",
				[MsgRemovedFromFavoriteItem] = "Предмет '{0}' удалён из избранного!",
				[UIMsgNoItems] = "К сожалению, в данный момент товаров в наличии нет",
				[UIBasketFooterItemsCountTitle] = "Предметы",
				[UIBasketFooterItemsCountValue] = "{0} шт",
				[UIBasketFooterItemsCostTitle] = "Цена",
				[UIContentHeaderButtonToggleEconomy] = "▲",
				[UIShopActionAmountTitle] = "КОЛИЧЕСТВО",
				[UIShopActionDescriptionTitle] = "ОПИСАНИЕ",
				[UIShopItemDescriptionTitle] = "ОПИСАНИЕ",
				[UICategoriesAdminShowAllTitle] = "ПОКАЗАТЬ ВСЕ",
				[UIMsgShopInInitialization] = "В настоящее время плагин инициализируется. Пожалуйста, подождите немного, пока процесс завершится."
			}, this, "ru");

			lang.RegisterMessages(new Dictionary<string, string>
			{
				[DaysFormat] = "{0}天",
				[HoursFormat] = "{0}小时",
				[MinutesFormat] = "{0}分",
				[SecondsFormat] = "{0}秒",
				[NotEnoughSpace] = "背包空间不足",
				[NotMoney] = "资金不够！",
				[ReceivedItems] = "已收到所有的物品！",
				[BuyTitle] = "购买",
				[SellTitle] = "售卖",
				[PlusTitle] = "+",
				[MinusTitle] = "-",
				[RemoveTitle] = "移除",
				[AmountTitle] = "数量 {0}件",
				[BackTitle] = "上一页",
				[NextTitle] = "下一页",
				[ItemAmount] = "数量",
				[CloseButton] = "✕",
				[YourBalance] = "您的余额",
				[MainTitle] = "商店",
				[CategoriesTitle] = "类别",
				[ShoppingBag] = "购物车",
				[PurchaseConfirmation] = "确认购买",
				[CancelTitle] = "取消",
				[ErrorClose] = "关闭",
				[ErrorMsg] = "XXX",
				[NotEnough] = "您没有足够的物品！",
				[BtnSelect] = "选择",
				[EditingTitle] = "物品编辑",
				[ItemSearch] = "物品搜索",
				[Back] = "上一页",
				[Next] = "下一页",
				[RemoveItem] = "✕",
				[BtnSave] = "保存",
				[ItemName] = "物品",
				[CmdName] = "指令",
				[PluginName] = "插件",
				[BtnAddItem] = "新增物品",
				[SearchTitle] = "搜索...",
				[BackPage] = "<",
				[NextPage] = ">",
				[SellCooldownTitle] = "冷却",
				[BuyCooldownTitle] = "冷却",
				[BuyCooldownMessage] = "您无法购买“{0}”商品！等待{1}",
				[SellCooldownMessage] = "您不能出售“{0}”商品！等待{1}",
				[BtnBack] = "▲",
				[BtnNext] = "▼",
				[SellNotify] = "您已成功售出 {0} 件 {1}",
				[BuyRaidBlocked] = "炸家封锁状态中无法购买！",
				[SellRaidBlocked] = "炸家封锁状态中不能出售！",
				[BuyWipeCooldown] = "您无法再购买{0}！",
				[SellWipeCooldown] = "您不能再售卖 {0}！",
				[BuyRespawnCooldown] = "您无法再购买{0}！",
				[SellRespawnCooldown] = "您不能再出售{0}！",
				[InfoTitle] = "我",
				[DailyBuyLimitReached] = "已达到每日限额，无法购买“{0}”",
				[DailySellLimitReached] = "已达到每日限额，无法售卖“{0}”",
				[BuyLimitReached] = "已达到最大限额，无法购买“{0}”",
				[SellLimitReached] = "已达到最大限额，无法出售“{0}”",
				[NoUseDuel] = "决斗中无法使用商店",
				[SkinBlocked] = "皮肤被无法出售",
				[LogBuyItems] = "玩家 {0} ({1}) 以 {2}$ 购买了物品：{3}",
				[LogSellItem] = "玩家 {0} ({1}) 以 {2}$ 的价格出售了物品：{3}",
				[SelectPlayerTitle] = "选择要转帐的玩家",
				[PlayerNotFound] = "未找到玩家",
				[SuccessfulTransfer] = "已将 {0} 转帐给玩家“{1}”",
				[TransferTitle] = "转帐",
				[TransferButton] = "寄钱",
				[TitleMax] = "最大上限",
				[NoTransferPlayers] = "目前没有玩家可以转帐",
				[NoPermission] = "您没有所需的权限",
				[ErrorSyntax] = "语法错误！使用：/{0}",
				[VMNotFoundCategories] = "未找到类别！",
				[VMNotFound] = "未找到自动售货机！",
				[VMExists] = "该自动售货机已在配置中！",
				[VMInstalled] = "您已成功安装客制的自动售货机！",
				[ChoiceEconomy] = "选择货币",
				[EditBlueprint] = "蓝图",
				[NPCNotFound] = "未找到NPC！",
				[NPCInstalled] = "您已成功安装自定义的NPC！",
				[ItemPriceFree] = "免费",
				[BtnCalculate] = "计算",
				[EditingCategoryTitle] = "类别编辑",
				[BtnAddCategory] = "+",
				[BtnEditCategory] = "✎",
				[BtnBoolOFF] = "关闭",
				[BtnBoolON] = "开启",
				[NoILError] = "插件无法正常使用，请联系管理员！",
				[MsgNoFavoriteItem] = "您无法从收藏夹中删除该项目，因为它不是收藏物品",
				[MsgIsFavoriteItem] = "您无法将此物件添加到收藏夹，因为它已经是收藏物品",
				[MsgAddedToFavoriteItem] = "物件“{0}”已添加到您的收藏夹！",
				[MsgRemovedFromFavoriteItem] = "物件“{0}”已从收藏夹中删除！",
				[UIMsgNoItems] = "抱歉，目前没有商品",
				[UIBasketFooterItemsCountTitle] = "商品",
				[UIBasketFooterItemsCountValue] = "{0} RP",
				[UIBasketFooterItemsCostTitle] = "费用",
				[UIContentHeaderButtonToggleEconomy] = "▲",
				[UIShopActionAmountTitle] = "金额",
				[UIShopActionDescriptionTitle] = "说明",
				[UIShopItemDescriptionTitle] = "说明",
				[UICategoriesAdminShowAllTitle] = "显示所有",
				[UIMsgShopInInitialization] = "插件正在初始化。请稍候。",
			}, this, "zh-CN");
		}

		private string Msg(BasePlayer player, string key, params object[] obj)
		{
			return string.Format(lang.GetMessage(key, this, player.UserIDString), obj);
		}

		private void Reply(BasePlayer player, string key, params object[] obj)
		{
			SendReply(player, Msg(player, key, obj));
		}

		private void SendNotify(BasePlayer player, string key, int type, params object[] obj)
		{
			if (_config.UseNotify && (Notify != null || UINotify != null))
				Interface.Oxide.CallHook("SendNotify", player, type, Msg(player, key, obj));
			else
				Reply(player, key, obj);
		}

		#endregion

		#region Cache

		#region Search

		private readonly Dictionary<string, HashSet<SearchInfo>> _searchCache = new();

		private class SearchInfo
		{
			public string Permission;

			public ShopItem Item;

			public SearchInfo(string permission, ShopItem item)
			{
				Permission = permission;
				Item = item;
			}
		}

		private List<ShopItem> SearchItem(BasePlayer player, string search)
		{
			if (_searchCache.TryGetValue(search, out var searchInfo))
				return searchInfo
					.Where(x => string.IsNullOrEmpty(x.Permission) || player.HasPermission(x.Permission))
					.Select(x => x.Item);

			var shop = GetShop(player);

			var items = new List<ShopItem>();
			shop.Categories.ForEach(category =>
			{
				category.GetShopItems(player).ForEach(item =>
				{
					var itemTitle = item.GetPublicTitle(player);
					if (itemTitle.StartsWith(search) ||
					    itemTitle.Contains(search) ||
					    item.ShortName.StartsWith(search) || item.ShortName.Contains(search))
					{
						items.Add(item);

						if (_searchCache.TryGetValue(search, out var cache))
							cache.Add(new SearchInfo(category.Permission, item));
						else
							_searchCache.Add(search, new HashSet<SearchInfo>
							{
								new(category.Permission, item)
							});
					}
				});
			});

			return items;
		}

		#endregion

		#region Fields

		private readonly Dictionary<ulong, OpenedShop> _openSHOP = new();

		private class OpenedShop
		{
			#region Fields

			public BasePlayer Player;

			public List<ShopCategory> Categories;

			public ListHashSet<ShopItem> ItemsToUpdate = new();

			private bool useMainUI;
			
			#endregion
			
			public OpenedShop(BasePlayer player, bool mainUI = true)
			{
				Player = player;

				useMainUI = mainUI;
				
				Update();

				UpdateAvailableShowCategoriesMoveButtons();
			}

			#region UI

			public ShopUI GetUI()
			{
				return useMainUI ? _uiData.FullscreenUI : _uiData.InMenuUI;
			}

			#endregion
			
			#region Updates

			public void Update()
			{
				Categories = _instance.GetCategories(Player);
			}

			public void ClearItemsToUpdate()
			{
				ItemsToUpdate.Clear();
			}

			public void AddItemToUpdate(ShopItem shopItem)
			{
				ItemsToUpdate.TryAdd(shopItem);
			}

			public void AddItemToUpdate(List<ShopItem> items)
			{
				ItemsToUpdate.Clear();

				ItemsToUpdate.AddRange(items);
			}

			public void RemoveItemToUpdate(ShopItem shopItem)
			{
				ItemsToUpdate.Remove(shopItem);
			}

			#endregion
	
			#region Categories

			public int 
				currentCategoryIndex, 
				currentCategoriesPage,
				currentShopPage,
				currentSearchPage,
				currentBasketPage;

			public string search;

			public bool canShowCategoriesMoveButtons = true;

			public void OnChangeCategory(int newCategoryIndex, int newCategoriesPage)
			{
				currentCategoryIndex = newCategoryIndex;
				currentCategoriesPage = newCategoriesPage;

				OnChangeShopPage(0);
				OnChangeSearch(string.Empty, 0);
			}

			public void OnChangeSearch(string newSearch, int newSearchPage)
			{
				search = newSearch;
				currentSearchPage = newSearchPage; 

				UpdateAvailableShowCategoriesMoveButtons();
			}

			public void OnChangeShopPage(int newShopPage)
			{
				currentShopPage = newShopPage;
			}

			public void OnChangeCategoriesPage(int newCategoriesPage)
			{
				currentCategoriesPage = newCategoriesPage;
			}

			public void OnChangeBasketPage(int newBasketPage)
			{
				currentBasketPage = newBasketPage;
			}

			public bool HasSearch()
			{
				return GetUI().ShopContent.Content.Search.Enabled && !string.IsNullOrEmpty(search);
			}

			public ShopCategory GetSelectedShopCategory()
			{
				if (HasSearch())
					return null;

				return currentCategoryIndex >= 0 && currentCategoryIndex < Categories.Count
					? Categories[currentCategoryIndex]
					: null;
			}

			private void UpdateAvailableShowCategoriesMoveButtons()
			{
				canShowCategoriesMoveButtons = CheckShowCategoryMoveButtons();
			}

			public bool CheckShowCategoryMoveButtons()
			{
				var shopCategory = GetSelectedShopCategory();
				return !HasSearch() && shopCategory is {SortType: Configuration.SortType.None} and not {CategoryType: ShopCategory.Type.Favorite};
			}

			#endregion Categories
		}

		private OpenedShop GetShop(BasePlayer player, bool mainUI = true)
		{
			if (!TryGetShop(player.userID, out var shop))
				_openSHOP.TryAdd(player.userID, shop = new OpenedShop(player, mainUI));

			return shop;
		}

		private OpenedShop GetShop(ulong player)
		{
			return _openSHOP.GetValueOrDefault(player);
		}

		private bool TryGetShop(ulong player, out OpenedShop shop)
		{
			return _openSHOP.TryGetValue(player, out shop);
		}

		private bool RemoveOpenedShop(ulong player)
		{
			return _openSHOP.Remove(player);
		}

		#endregion

		#endregion

		#region Convert

		#region Server Rewards

		#region Data

		[ConsoleCommand("shop.convert.sr")]
		private void CmdConvertSR(ConsoleSystem.Arg arg)
		{
			if (!arg.IsAdmin) return;

			if (arg.HasArgs())
			{
				if (bool.TryParse(arg.Args[0], out var clear) && clear) _itemsData.Shop.Clear();
			}

			var data = LoadSRData();
			if (data == null) return;

			ConvertSRData(data);
		}

		private SRRewardData LoadSRData()
		{
			SRRewardData rewarddata = null;

			try
			{
				rewarddata = Interface.Oxide.DataFileSystem.ReadObject<SRRewardData>("ServerRewards/reward_data");
			}
			catch
			{
				PrintWarning("No Server Rewards data found!");
			}

			return rewarddata;
		}

		private void ConvertSRData(SRRewardData rewarddata)
		{
			var totalCount = 0;

			ConvertingSRDataCommands(ref rewarddata, ref totalCount);

			ConvertingSRDataItems(ref rewarddata, ref totalCount);

			ConvertingSRDataKits(ref rewarddata, ref totalCount);

			SaveItemsData();

			LoadItems();

			PrintWarning($"{totalCount} items successfully converted from ServerRewards to Shop!");
		}

		private void ConvertingSRDataCommands(ref SRRewardData rewarddata, ref int count)
		{
			if (rewarddata == null) return;

			var category = new ShopCategory
			{
				Enabled = true,
				CategoryType = ShopCategory.Type.None,
				Title = "Commands",
				Permission = string.Empty,
				Localization = new Localization
				{
					Enabled = false,
					Messages = new Dictionary<string, string>
					{
						["en"] = "Commands",
						["fr"] = "Commands"
					}
				},
				SortType = Configuration.SortType.None,
				Items = new List<ShopItem>()
			};

			foreach (var check in rewarddata.commands)
			{
				var newItem = ShopItem.GetDefault(GetId(), check.Value.cost, string.Empty);
				newItem.Type = ItemType.Command;
				newItem.Image = !string.IsNullOrEmpty(check.Value.iconName) &&
				                (check.Value.iconName.StartsWith("http") || check.Value.iconName.StartsWith("www"))
					? check.Value.iconName
					: string.Empty;
				newItem.Description = check.Value.description;
				newItem.Command = string.Join("|", check.Value.commands);
				newItem.BuyCooldown = check.Value.cooldown;
				newItem.SellCooldown = check.Value.cooldown;

				category.Items.Add(newItem);

				count++;
			}

			if (category.Items.Count > 0)
				_itemsData.Shop.Add(category);
		}

		private void ConvertingSRDataItems(ref SRRewardData rewarddata, ref int count)
		{
			if (rewarddata == null) return;

			var noneCategory = new ShopCategory
			{
				Enabled = true,
				CategoryType = ShopCategory.Type.None,
				Title = "Items",
				Permission = string.Empty,
				Localization = new Localization
				{
					Enabled = false,
					Messages = new Dictionary<string, string>
					{
						["en"] = "Items",
						["fr"] = "Items"
					}
				},
				SortType = Configuration.SortType.None,
				Items = new List<ShopItem>()
			};

			foreach (var check in rewarddata.items)
			{
				ShopCategory category;
				if (check.Value.category == SRCategory.None)
				{
					category = noneCategory;
				}
				else
				{
					category = _itemsData.Shop.Find(x => x.Title == check.Value.category.ToString());

					if (category == null)
					{
						category = new ShopCategory
						{
							Enabled = true,
							CategoryType = ShopCategory.Type.None,
							Title = check.Value.category.ToString(),
							Permission = string.Empty,
							Localization = new Localization
							{
								Enabled = false,
								Messages = new Dictionary<string, string>
								{
									["en"] = check.Value.category.ToString(),
									["fr"] = check.Value.category.ToString()
								}
							},
							SortType = Configuration.SortType.None,
							Items = new List<ShopItem>()
						};

						_itemsData.Shop.Add(category);
					}
				}

				var newItem = ShopItem.GetDefault(GetId(), check.Value.cost, check.Value.shortname);
				newItem.Type = ItemType.Item;
				newItem.Image = !string.IsNullOrEmpty(check.Value.customIcon) &&
				                (check.Value.customIcon.StartsWith("http") || check.Value.customIcon.StartsWith("www"))
					? check.Value.customIcon
					: string.Empty;
				newItem.Amount = check.Value.amount;
				newItem.Skin = check.Value.skinId;
				newItem.BuyCooldown = check.Value.cooldown;
				newItem.SellCooldown = check.Value.cooldown;

				category.Items.Add(newItem);

				count++;
			}

			if (noneCategory.Items.Count > 0)
				_itemsData.Shop.Add(noneCategory);
		}

		private void ConvertingSRDataKits(ref SRRewardData rewarddata, ref int count)
		{
			if (rewarddata == null) return;

			var category = new ShopCategory
			{
				Enabled = true,
				CategoryType = ShopCategory.Type.None,
				Title = "Kits",
				Permission = string.Empty,
				Localization = new Localization
				{
					Enabled = false,
					Messages = new Dictionary<string, string>
					{
						["en"] = "Kits",
						["fr"] = "Kits"
					}
				},
				SortType = Configuration.SortType.None,
				Items = new List<ShopItem>()
			};

			foreach (var check in rewarddata.kits)
			{
				var newItem = ShopItem.GetDefault(GetId(), check.Value.cost, string.Empty);
				newItem.Type = ItemType.Kit;
				newItem.Image = !string.IsNullOrEmpty(check.Value.iconName) &&
				                (check.Value.iconName.StartsWith("http") || check.Value.iconName.StartsWith("www"))
					? check.Value.iconName
					: string.Empty;
				newItem.Description = check.Value.description;
				newItem.Kit = check.Value.kitName;
				newItem.BuyCooldown = check.Value.cooldown;
				newItem.SellCooldown = check.Value.cooldown;

				category.Items.Add(newItem);

				count++;
			}

			if (category.Items.Count > 0)
				_itemsData.Shop.Add(category);
		}

		#endregion

		#region Classes

		private enum SRCategory
		{
			None,
			Weapon,
			Construction,
			Items,
			Resources,
			Attire,
			Tool,
			Medical,
			Food,
			Ammunition,
			Traps,
			Misc,
			Component,
			Electrical,
			Fun
		}

		private class SRRewardData
		{
			public Dictionary<string, RewardItem> items = new();
			public SortedDictionary<string, RewardKit> kits = new();
			public SortedDictionary<string, RewardCommand> commands = new();

			public bool HasItems(SRCategory category)
			{
				foreach (var kvp in items)
					if (kvp.Value.category == category)
						return true;
				return false;
			}

			public class RewardItem : Reward
			{
				public string shortname, customIcon;
				public int amount;
				public ulong skinId;
				public bool isBp;
				public SRCategory category;
			}

			public class RewardKit : Reward
			{
				public string kitName, description, iconName;
			}

			public class RewardCommand : Reward
			{
				public string description, iconName;
				public List<string> commands = new();
			}

			public class Reward
			{
				public string displayName;
				public int cost;
				public int cooldown;
			}
		}

		#endregion

		#endregion

		#region V1.2.26

		#region Convert Commands

		[ConsoleCommand("shop.convert.from.1.2.26")]
		private void CmdConsoleConvertFrom1d2d26(ConsoleSystem.Arg arg)
		{
			if (!arg.IsServerside) return;

			StartConvertFrom1d2d26();

			PrintWarning("You have started converting Shop plugin version 1.2.26.");
		}

		#endregion

		#region Convert Methods

		private Coroutine _coroutineConvertFrom1d2d26;

		private void StartConvertFrom1d2d26()
		{
			StopConvertFrom1d2d26();

			_coroutineConvertFrom1d2d26 = ServerMgr.Instance.StartCoroutine(ConverterCoroutineFrom1d2d26());
		}

		private void StopConvertFrom1d2d26()
		{
			if (_coroutineConvertFrom1d2d26 != null)
				ServerMgr.Instance.StopCoroutine(_coroutineConvertFrom1d2d26);
		}

		private IEnumerator ConverterCoroutineFrom1d2d26()
		{
			var sw = Stopwatch.StartNew();

			yield return Old1d2d26Classes.StartConvertPlayers();

			yield return CoroutineEx.waitForEndOfFrame;

			yield return Old1d2d26Classes.StartConvertEconomyChoice();

			yield return CoroutineEx.waitForEndOfFrame;

			yield return Old1d2d26Classes.StartConvertLimits();

			yield return CoroutineEx.waitForEndOfFrame;

			yield return Old1d2d26Classes.StartConvertCooldowns();

			yield return CoroutineEx.waitForEndOfFrame;

			yield return SaveCachedPlayersData();

			yield return CoroutineEx.waitForEndOfFrame;

			yield return UnloadOfflinePlayersData();

			sw.Stop();

			PrintWarning($"Shop plugin data version 1.2.26 was converted in {sw.ElapsedMilliseconds}ms.");
		}

		#endregion

		#region Classes

		private class Old1d2d26Classes
		{
			#region Players

			public static IEnumerator StartConvertPlayers()
			{
				var pluginData = LoadPluginData();
				if (pluginData == null) yield break;

				try
				{
					foreach (var playerCart in pluginData.PlayerCarts)
					{
						var data = PlayerData.GetOrCreate(playerCart.Key);

						var newPlayerCartData = new PlayerCartData
						{
							Items = playerCart.Value.Items,
							LastPurchaseItems = playerCart.Value.LastPurchaseItems,
							FavoriteItems = new HashSet<int>()
						};

						var newNpcData = new PlayerNPCCart();
						foreach (var npcCart in playerCart.Value.NpcCarts)
							newNpcData.Carts.TryAdd(npcCart.Key, new CartData
							{
								Items = npcCart.Value.Items
							});

						data.PlayerCart = newPlayerCartData;
						data.NPCCart = newNpcData;
					}
				}
				catch (Exception e)
				{
					Debug.LogError(e);
				}
			}

			public static PluginData LoadPluginData()
			{
				PluginData data = null;
				try
				{
					data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>($"{_instance?.Name}/Players");
				}
				catch (Exception e)
				{
					_instance?.PrintError(e.ToString());
				}

				return data;
			}

			public class PluginData
			{
				[JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public Dictionary<ulong, PlayerCart> PlayerCarts = new();
			}

			public class PlayerCart
			{
				[JsonProperty(PropertyName = "Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public Dictionary<int, int> Items = new();

				[JsonProperty(PropertyName = "NPC Carts", ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public Dictionary<string, NPCCart> NpcCarts = new();

				[JsonProperty(PropertyName = "Last Purchase Items",
					ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public Dictionary<int, int> LastPurchaseItems = new();
			}

			public class NPCCart
			{
				[JsonProperty(PropertyName = "Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public Dictionary<int, int> Items = new();
			}

			#endregion

			#region Economy Choice

			public static IEnumerator StartConvertEconomyChoice()
			{
				var economyChoice = LoadEconomyChoice();
				if (economyChoice == null) yield break;

				try
				{
					foreach (var playerChoice in economyChoice.Players)
					{
						var data = PlayerData.GetOrCreate(playerChoice.Key);

						data.SelectedEconomy = playerChoice.Value;
					}
				}
				catch (Exception e)
				{
					Debug.LogError(e);
				}
			}

			public static EconomyChoice LoadEconomyChoice()
			{
				EconomyChoice data = null;
				try
				{
					data = Interface.Oxide.DataFileSystem.ReadObject<EconomyChoice>($"{_instance?.Name}/EconomyChoice");
				}
				catch (Exception e)
				{
					_instance?.PrintError(e.ToString());
				}

				return data;
			}

			public class EconomyChoice
			{
				[JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public Dictionary<ulong, int> Players = new();
			}

			#endregion

			#region Limits

			public static IEnumerator StartConvertLimits()
			{
				var limits = LoadPlayerLimits();
				if (limits == null) yield break;

				try
				{
					foreach (var limitData in limits.Players)
					{
						var data = PlayerData.GetOrCreate(limitData.Key);

						foreach (var itemLimit in limitData.Value.ItemsLimits)
							data.Limits.ItemsLimits.TryAdd(itemLimit.Key, new PlayerData.LimitData.ItemData
							{
								Sell = itemLimit.Value.Sell,
								Buy = itemLimit.Value.Buy
							});

						foreach (var itemLimit in limitData.Value.DailyItemsLimits)
							data.Limits.DailyItemsLimits.TryAdd(itemLimit.Key, new PlayerData.LimitData.ItemData
							{
								Sell = itemLimit.Value.Sell,
								Buy = itemLimit.Value.Buy
							});
					}
				}
				catch (Exception e)
				{
					Debug.LogError(e);
				}
			}

			public static PlayerLimits LoadPlayerLimits()
			{
				PlayerLimits data = null;
				try
				{
					data = Interface.Oxide.DataFileSystem.ReadObject<PlayerLimits>($"{_instance?.Name}/Limits");
				}
				catch (Exception e)
				{
					_instance?.PrintError(e.ToString());
				}

				return data;
			}

			public class PlayerLimits
			{
				[JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public Dictionary<ulong, PlayerLimitData> Players = new();
			}

			public class PlayerLimitData
			{
				[JsonProperty(PropertyName = "Limits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public Dictionary<int, ItemLimitData> ItemsLimits = new();

				[JsonProperty(PropertyName = "Last Update Time")]
				public DateTime LastUpdate;

				[JsonProperty(PropertyName = "Daily Limits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public Dictionary<int, ItemLimitData> DailyItemsLimits = new();
			}

			public class ItemLimitData
			{
				public int Sell;

				public int Buy;
			}

			#endregion

			#region Cooldown

			public static IEnumerator StartConvertCooldowns()
			{
				var cooldowns = LoadCooldowns();
				if (cooldowns == null) yield break;

				try
				{
					foreach (var cooldown in cooldowns)
					{
						var data = PlayerData.GetOrCreate(cooldown.Key);

						foreach (var oldCooldownData in cooldown.Value.Data)
							data.Cooldowns.Items.TryAdd(oldCooldownData.Key, new PlayerData.CooldownData.ItemData
							{
								LastBuyTime = oldCooldownData.Value.LastBuyTime,
								LastSellTime = oldCooldownData.Value.LastSellTime
							});
					}
				}
				catch (Exception e)
				{
					Debug.LogError(e);
				}
			}

			public static Dictionary<ulong, CooldownInfo> LoadCooldowns()
			{
				Dictionary<ulong, CooldownInfo> data = null;
				try
				{
					data = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, CooldownInfo>>(
						$"{_instance?.Name}/Cooldown");
				}
				catch (Exception e)
				{
					_instance?.PrintError(e.ToString());
				}

				return data;
			}

			public class CooldownInfo
			{
				[JsonProperty(PropertyName = "Cooldowns", ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public Dictionary<int, OldCooldownData> Data = new();
			}

			public class OldCooldownData
			{
				public DateTime LastBuyTime = new(1970, 1, 1, 0, 0, 0);

				public DateTime LastSellTime = new(1970, 1, 1, 0, 0, 0);
			}

			#endregion
		}

		#endregion

		#endregion
		
		#endregion

		#region Testing functions

#if TESTING
		private static void SayDebug(string message)
		{
			Debug.Log($"[Shop.Debug] {message}");
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

		private void API_SetShopTemplate(object data, bool isFullscreen)
		{
			if (isFullscreen)
			{
				_uiData.FullscreenUI = (ShopUI) data;
			}
			else
			{
				_uiData.InMenuUI = (ShopUI) data;
			}
		}

#endif

		#endregion
	
		#region Installer

		private ShopInstaller _shopInstaller = null;

		#region Installer Classes
		
		public class ShopTemplates
		{
			public ShopTemplate[] FullScreenTemplates;
    		public ShopTemplate[] InMenuTemplates;
			public ShopDependency[] CarbonDependencies;
			public ShopDependency[] Dependencies;
			public Dictionary<string, ShopInstallerLang> InstallerLang;
			public Dictionary<string, string> Images;
		}

		public class ShopTemplate
		{
			public string Title;

			public string BannerURL;

			public string VideoURL;

			public ShopUI SettingsUI;

			public Dictionary<string, ShopInstallerLang> TemplateLang;
		}

		public class ShopInstallerLang
		{
			public Dictionary<string, string> Messages;
		}

		public class ShopDependency
		{
			public string PluginName;
			public string PluginAuthor;
			public bool IsRequired;

			public Dictionary<string, (string Title, string Description)> Messages = new(); // status – message

			public string GetStatus()
			{
				var plugin = _instance?.plugins.Find(PluginName);
				if (plugin == null) return IsRequired ? "install" : "missing";
				
				if (!string.IsNullOrEmpty(PluginAuthor))
					return "missing";

				if (!IsVersionInRange(plugin.Version))
					return "wrong_version";

				return "ok";
			}

			#region Version

			public VersionNumber versionFrom = default;

			public VersionNumber versionTo = default;

			public bool IsVersionInRange(VersionNumber version)
			{
				return (versionFrom == versionTo) || 
				       (versionFrom == default && versionTo == default) || 
				       (versionFrom == default || version >= versionFrom) &&
				       (versionTo == default || version < versionTo);
			}

			#endregion
		}

		private class ShopInstaller
		{
			#region Installing

            public ulong Player = 0UL;
            
			public int step, targetTemplateIndex, targetInMenuTemplateIndex;

			public ShopTemplates shopData;

			public void StartInstall(ulong userID = 0UL)
			{
				Player = userID;
				
				step = 1;
			}

			public void SetStep(int newStep)
			{
				step = newStep;
			}

			public void SelectTemplate(int newTemplateIndex, bool isFullScreen)
			{
				if (isFullScreen)
					targetTemplateIndex = newTemplateIndex;
				else
					targetInMenuTemplateIndex = newTemplateIndex;
			}

			public void Finish()
			{
				_uiData = new();
				
				var fullscreenTemplate = GetSelectedTemplate(true);
				if (fullscreenTemplate != null)
				{
					_uiData.FullscreenUI = fullscreenTemplate.SettingsUI;

					_uiData.IsFullscreenUISet = true;

					if (fullscreenTemplate.TemplateLang != null) _instance.RegisterTemplateMessages(fullscreenTemplate.TemplateLang);
				}

				if (_instance.ServerPanel != null)
				{
					var menuTemplate = GetSelectedTemplate(false);
					if (menuTemplate != null)
					{
						_uiData.InMenuUI = menuTemplate.SettingsUI;
						
						_uiData.IsInMenuUISet = true;

						if (menuTemplate.TemplateLang != null) _instance.RegisterTemplateMessages(menuTemplate.TemplateLang);
					}
				}
				
				if (BasePlayer.TryFindByID(Player, out var player))
					player?.ChatMessage($"You have successfully completed the installation of the plugin! Now the plugin will reload (usually takes 5-10 seconds) and you will be able to use it!");
				else
					_instance.Puts($"You have successfully completed the installation of the plugin! Now the plugin will reload (usually takes 5-10 seconds) and you will be able to use it!");
				
				_instance.SaveUIData();

				if (_itemsData.Shop.Count == 0)
					_instance.FillCategories();
				
				_instance.LoadImages();

				Interface.Oxide.ReloadPlugin("Shop");
			}

			#endregion Installing

			#region Syncing
            
			private Hash<string, string> imageIds = new();

			public void LoadImages()
			{
				if (shopData.Images != null)
					foreach (var (name, base64) in shopData.Images)
						LoadImage(name, base64);
				
				foreach (var shopTemplate in shopData.FullScreenTemplates) _instance.AddImage(shopTemplate.BannerURL, shopTemplate.BannerURL);
				
				if (_instance.ServerPanel != null)
					foreach (var shopTemplate in shopData.InMenuTemplates) _instance.AddImage(shopTemplate.BannerURL, shopTemplate.BannerURL);
			}

			public string GetImage(string name)
			{
				if (imageIds.TryGetValue(name, out var value))
					return value;
				
				return string.Empty;
			}

			private void LoadImage(string name, string base64)
			{				
				var bytes = Convert.FromBase64String(base64);
				if (bytes.Length == 0) return;
				
				imageIds[name] = FileStorage.server.Store(bytes, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
			}

			public string GetColorFromDependencyStatus(string status)
			{
				return status switch
				{
					"ok" => HexToCuiColor("#78CF69"),
					"missing" or "wrong_version" => HexToCuiColor("#F8AB39"),
					"todo" => HexToCuiColor("#71B8ED"),
					_ => HexToCuiColor("#E44028")
				};
			}

			public ShopDependency[] GetSortedShopDependencies()
			{
				var dependencies = GetShopDependencies();
				Array.Sort(dependencies,
					(a, b) => string.Compare(b.GetStatus(), a.GetStatus(), StringComparison.Ordinal));
				return dependencies;
			}

			public ShopDependency[] GetShopDependencies()
			{
#if CARBON
				return shopData.CarbonDependencies;
#else
				return shopData.Dependencies;
#endif
			}

			public ShopTemplate GetTemplate(int index, bool isFullScreen)
			{
				return (isFullScreen ? shopData.FullScreenTemplates : shopData.InMenuTemplates)[index];
			}

			public ShopTemplate GetSelectedTemplate(bool isFullScreen)
			{
				var templates = isFullScreen ? shopData.FullScreenTemplates : shopData.InMenuTemplates;
				var targetIndex = isFullScreen ? targetTemplateIndex : targetInMenuTemplateIndex;

				return targetIndex >= 0 && targetIndex < templates.Length ? templates[targetIndex] : null;
			}

			#endregion Syncing

			#region Lang

			public string GetMessage(BasePlayer player, string key)
			{
				if (shopData.InstallerLang.Count == 0)
					throw new Exception("There are no messages!");

				var userLang = "en";
				if (player != null) userLang = _instance.lang.GetLanguage(player.UserIDString);

				if (shopData.InstallerLang.TryGetValue(userLang, out var messages))
					if (messages.Messages.TryGetValue(key, out var msg))
						return msg;

				if (shopData.InstallerLang.TryGetValue("en", out messages))
					if (messages.Messages.TryGetValue(key, out var msg))
						return msg;

				return key;
			}

			#endregion Lang
		}

		#endregion Classes

		#region Installer Init

		private void InitInstaller()
		{
			_shopInstaller = new ShopInstaller();

			LoadShopInstallerData(data =>
			{
				_shopInstaller.shopData = data;
					
				_instance.Puts("Shop data loaded successfully.");

				_shopInstaller.LoadImages();
			});

			cmd.AddConsoleCommand("UI_Shop_Installer", this, nameof(CmdConsoleShopInstaller));
		}
        
		private void LoadShopInstallerData(Action<ShopTemplates> callback = null)
		{
			_instance?.webrequest.Enqueue("https://gitlab.com/TheMevent/PluginsStorage/raw/main/ce7bd40bd7affedef6b91e0146f3d2ef_LoneDesign.json", null, (code, response) =>
			{
				if (code != 200)
				{
					_instance.PrintError($"Failed to load shop data. HTTP status code: {code}");
					return;
				}

				if (string.IsNullOrEmpty(response))
				{
					_instance.PrintError("Failed to load shop data. Response is null or empty.");
					return;
				}

				var jsonData = JObject.Parse(response)?["CipherShopData"]?.ToString();
				if (string.IsNullOrWhiteSpace(jsonData))
				{
					_instance.PrintError("Failed to load shop data. Response is not in the expected format."); 
					return;
				}
                
				var shopDataResponse = EncryptDecrypt.Decrypt(jsonData, "ektYMzlVOVN0M3lwbnc3OA==");
				if (string.IsNullOrWhiteSpace(shopDataResponse))
				{
					_instance.PrintError("Failed to decrypt shop data. Response is not in the expected format.");
					return;
				}
		        
				try
				{
					var data = JsonConvert.DeserializeObject<ShopTemplates>(shopDataResponse);
					if (data == null)
					{
						_instance.PrintError("Failed to deserialize shop data. Response is not in the expected format.");
						return;
					}

					callback?.Invoke(data);
				}
				catch (Exception ex)
				{
					_instance.PrintError($"Error loading shop data: {ex.Message}");
				}
			}, _instance);
		}

		#endregion

		#region Installer Commands

		[ChatCommand("shop.install")]
		private void CmdChatShopInstaller(BasePlayer player, string command, string[] args)
		{
			if (player == null) return;

			if (!player.HasPermission(PermAdmin))
			{
				SendReply(player, "You don't have permission!");
				
				PrintError($"Player {player.UserIDString} tried to run the installer, but he doesn't have {PermAdmin} permission");
				return;
			}

			if (_shopInstaller == null)
			{
				SendReply(player, 
					$"Shop has already been installed! To run the installer again, you need to reset Shop. To reset, use the 'shop.reset' command (only console)!");
				return;
			}
			
			_shopInstaller.StartInstall(player.userID);

			ShowInstallerUI(player);
		}
		
		[ConsoleCommand("shop.install")]
		private void CmdConsoleShopInstall(ConsoleSystem.Arg arg)
		{
			if (!arg.IsServerside) return;

			if (_shopInstaller == null)
			{
				SendReply(arg, $"Shop has already been installed! To run the installer again, you need to reset Shop. To reset, use the 'shop.reset' command (only console)!");
				return;
			}

			SendReply(arg, DisplayTemplatesAndDependencies());

			if (arg.HasArgs())
			{
				_shopInstaller.StartInstall();

				var templateIndex = arg.GetInt(0);

				if (templateIndex < 0 || templateIndex >= _shopInstaller.shopData.FullScreenTemplates.Length)
				{
					SendReply(arg, $"Invalid fullscreen template index: {templateIndex}");
					return;
				}
				
				_shopInstaller.SelectTemplate(templateIndex, true);
				
				if (arg.HasArgs(2))
				{
					var inMenuTemplateIndex = arg.GetInt(1);

					if (inMenuTemplateIndex < 0 || inMenuTemplateIndex >= _shopInstaller.shopData.InMenuTemplates.Length)
					{
						SendReply(arg, $"Invalid in-menu template index: {inMenuTemplateIndex}");
						return;
					}

					_shopInstaller.SelectTemplate(inMenuTemplateIndex, false);
				}
				
				_shopInstaller.Finish();
				
				SendReply(arg, "Shop installation completed successfully!");
			}
			else
			{
				SendReply(arg, "Please specify the template index and in-menu template index. Example: shop.install <templateIndex> <inMenuTemplateIndex>");
			}
		}

		private string DisplayTemplatesAndDependencies()
		{
			var sb = new StringBuilder();
			
			sb.AppendLine("Available Fullscreen Templates:");
			for (var i = 0; i < _shopInstaller.shopData.FullScreenTemplates.Length; i++) 
				sb.AppendLine($"{i}: {_shopInstaller.shopData.FullScreenTemplates[i].Title}");

			sb.AppendLine("Available In-Menu Templates:");
			for (var i = 0; i < _shopInstaller.shopData.InMenuTemplates.Length; i++) 
				sb.AppendLine($"{i}: {_shopInstaller.shopData.InMenuTemplates[i].Title}");

			var dependencies = _shopInstaller.GetSortedShopDependencies();
			sb.AppendLine("Dependencies:");
			foreach (var dependency in dependencies) 
				sb.AppendLine($"{dependency.PluginName} by {dependency.PluginAuthor} - Status: {dependency.GetStatus()}");

			return sb.ToString();
		}

		[ConsoleCommand("shop.manage")]
		private void CmdConsoleShopManage(ConsoleSystem.Arg arg)
		{
			if (!arg.IsServerside) return;

			switch (arg.GetString(0))
			{
				case "economy":
				{
					switch (arg.GetString(1))
					{
						case "list":
						{
							var economies = new List<string>
							{
								$"0: {_config.Economy.GetDebugInfo()} (Enabled)",
							};

							foreach (var additionalEconomy in _config.AdditionalEconomics) 
								economies.Add($"{additionalEconomy.ID}: {_config.Economy.GetDebugInfo()} ({(additionalEconomy.Enabled ? "Enabled" : "Disabled")})");
							
							var sb = new StringBuilder();
							sb.AppendLine("Available Economies:");
							economies.ForEach(economy => sb.AppendLine(economy));
							SendReply(arg, sb.ToString());
							break;
						}

						case "set":
						{
							var economyID = arg.GetInt(2);
							var targetEconomyPlugin = arg.GetString(3);

							var economy = economyID == 0 ? _config.Economy : _config.AdditionalEconomics.Find(x => x.ID == economyID);
							if (economy == null)
							{
								SendReply(arg, $"Invalid economy ID: {economyID}");
								return;
							}
							
							if (ConfigureEconomy(economy, targetEconomyPlugin, arg.GetUInt64(3)))
							{
								SaveConfig();
								LoadEconomics();
								SendReply(arg, $"Economy {economyID} successfully updated to {targetEconomyPlugin}.");
							}
							else
							{
								SendReply(arg, $"Failed to update economy {economyID} to {targetEconomyPlugin}.");
							}
							
							break;
						}
					}
                    
					break;
				}
				
				default:
				{
					var sb = new StringBuilder();
					sb.AppendLine("Available commands:");
					sb.AppendLine("shop.manage economy list – Displays a list of all available economies.");
					sb.AppendLine("shop.manage economy set <economy_ID> <name> – Set an economy.");
					
					SendReply(arg, sb.ToString());
					break;
				}
			}

			#region Methods

			bool ConfigureEconomy(EconomyEntry economy, string targetEconomyPlugin, ulong targetSkin = 0UL)
			{
				switch (targetEconomyPlugin)
				{
					case "Economics":
						ConfigureEconomyPlugin(economy, "Economics", "Deposit", "Balance", "Withdraw");
						return true;

					case "ServerRewards":
						ConfigureEconomyPlugin(economy, "ServerRewards", "AddPoints", "CheckPoints", "TakePoints");
						return true;

					case "BankSystem":
						ConfigureEconomyPlugin(economy, "BankSystem", "API_BankSystemDeposit", "API_BankSystemBalance", "API_BankSystemWithdraw");
						return true;

					case "IQEconomic":
						ConfigureEconomyPlugin(economy, "IQEconomic", "API_SET_BALANCE", "API_GET_BALANCE", "API_REMOVE_BALANCE");
						return true;

					default:
						return ConfigureItemEconomy(economy, targetEconomyPlugin, targetSkin);
				}
			}

			void ConfigureEconomyPlugin(EconomyEntry economy, string pluginName, string addHook, string balanceHook, string removeHook)
			{
				economy.Type = EconomyType.Plugin;
				economy.Plug = pluginName;
				economy.AddHook = addHook;
				economy.BalanceHook = balanceHook;
				economy.RemoveHook = removeHook;
			}

			bool ConfigureItemEconomy(EconomyEntry economy, string itemName, ulong skin = 0UL)
			{
				var economyDef = ItemManager.FindItemDefinition(itemName);
				if (economyDef != null)
				{
					economy.Type = EconomyType.Item;
					economy.ShortName = economyDef.shortname;
					economy.Skin = skin;
					return true;
				}

				return false;
			}
			
			#endregion
		}
		
		private void CmdConsoleShopInstaller(ConsoleSystem.Arg arg)
		{
			var player = arg?.Player();
			if (player == null || !IsAdmin(player)) return;

			switch (arg.GetString(0))
			{
				case "change_step":
				{
					var newStep = arg.GetInt(1);

					_shopInstaller.SetStep(newStep);

					ShowInstallerUI(player);
					break;
				}

				case "select_template":
				{
					var newTemplateIndex = arg.GetInt(1);
					var isFullScreen = arg.GetBool(2); 

					_shopInstaller.SelectTemplate(newTemplateIndex, isFullScreen);
					
					UpdateUI(player, container =>
					{
						LoopTemplates(container, isFullScreen);
					});
					break;
				}

				case "finish":
				{
					_shopInstaller.Finish();
					break;
				}

				case "template_preview":
				{
					var templateIndex = arg.GetInt(1);
					var isFullScreen = arg.GetBool(2); 

					var template = _shopInstaller.GetTemplate(templateIndex, isFullScreen);
					if (template == null || string.IsNullOrWhiteSpace(template.BannerURL) || !template.BannerURL.IsURL())
						return;

					player.Command("client.playvideo", template.BannerURL);
					break;
				}
			}
		}

		#endregion

		#region Installer UI

		private const int Dependency_Height = 58, Dependency_Margin_Y = 12,
			UI_Installer_Template_Margin_Y = 20,
			UI_Installer_Template_Margin_X = 19,
			UI_Installer_Template_Width = 350,
			UI_Installer_Template_Height = 192;

		private void ShowInstallerUI(BasePlayer player)
		{
			var container = new CuiElementContainer();

			#region Background

			container.Add(new CuiPanel
			{
				CursorEnabled = true,
				Image =
				{
					Material = "assets/content/ui/namefontmaterial.mat",
					Color = "0 0 0 1"
				},
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"}
			}, "Overlay", Layer, Layer);

			#endregion Background

			#region Header

			container.Add(new CuiPanel
			{
				Image =
				{
					Color = HexToCuiColor("#929292", 5),
					Material = "assets/content/ui/namefontmaterial.mat",
				},
				RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -112", OffsetMax = "0 0"}
			}, Layer, Layer + ".Header");

			#region Title

			container.Add(new CuiElement
			{
				Parent = Layer + ".Header",
				Components =
				{
					new CuiTextComponent
					{
						Text = _shopInstaller.GetMessage(player, "UIHeaderTitle"),
						Font = "robotocondensed-bold.ttf", FontSize = 32,
						Align = TextAnchor.LowerLeft, Color = "0.6 0.6078432 0.6117647 1"
					},
					new CuiRectTransformComponent
						{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "90 46", OffsetMax = "0 0"}
				}
			});

			#endregion Title

			#region Description

			container.Add(new CuiElement
			{
				Parent = Layer + ".Header",
				Components =
				{
					new CuiTextComponent
					{
						Text = _shopInstaller.GetMessage(player, "UIHeaderDescription"),
						Font = "robotocondensed-regular.ttf", FontSize = 14,
						Align = TextAnchor.UpperLeft, Color = "0.6 0.6078432 0.6117647 1"
					},
					new CuiRectTransformComponent
						{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "90 0", OffsetMax = "0 -68"}
				}
			});

			#endregion Description

			#region Icon

			container.Add(new CuiElement
			{
				Parent = Layer + ".Header",
				Components =
				{
					new CuiRawImageComponent
					{
						Png = _shopInstaller.GetImage("Shop_Installer_HeaderIcon")
					},
					new CuiRectTransformComponent
						{AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "45 -20", OffsetMax = "80 20"}
				}
			});

			#endregion Icon

			#region Button.Close

			container.Add(new CuiPanel
			{
				Image =
				{
					Color = "0.8941177 0.2509804 0.1568628 1",	
					Material = "assets/content/ui/namefontmaterial.mat",
				},
				RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-40 -40", OffsetMax = "0 0"}
			}, Layer + ".Header", Layer + ".Header.Button.Close");

			#region Icon

			container.Add(new CuiPanel
			{
				Image = {Color = "1 1 1 0.9", Sprite = "assets/icons/close.png"},
				RectTransform =
					{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-11 -11", OffsetMax = "11 11"}
			}, Layer + ".Header.Button.Close");

			#endregion Icon

			container.Add(new CuiElement()
			{
				Parent = Layer + ".Header.Button.Close",
				Components =
				{
					new CuiButtonComponent()
					{
						Close = Layer,
						Color = "0 0 0 0"
					},
					new CuiRectTransformComponent()
				}
			});

			#endregion

			#endregion Header

			#region Steps

			if (ServerPanel != null)
			{
				switch (_shopInstaller.step)
				{
					case 1:
						ShowWelcomeStep(player, container);
						break;

					case 2:
						ShowDependenciesStep(player, container);
						break;

					case 3:
						ShowSelectTemplateStep(player, container);
						break;

					case 4:
						ShowSelectTemplateStep(player, container, false);
						break;

					case 5:
						ShowFinishStep(player, container);
						break;
				}
			}
			else
			{
				switch (_shopInstaller.step)
				{
					case 1:
						ShowWelcomeStep(player, container);
						break;

					case 2:
						ShowDependenciesStep(player, container);
						break;

					case 3:
						ShowSelectTemplateStep(player, container);
						break;

					case 4:
						ShowFinishStep(player, container);
						break;
				}
			}

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private void ShowFinishStep(BasePlayer player, CuiElementContainer container)
		{
			#region Background

			container.Add(new CuiPanel()
			{
				Image = {Color = "0 0 0 0"},
				RectTransform = {AnchorMin = "0 0.5", AnchorMax = "1 0.5", OffsetMin = "0 -360", OffsetMax = "0 248"}
			}, Layer, Layer + ".Main", Layer + ".Main");

			#endregion

			#region Label.Welcome

			container.Add(new CuiElement
			{
				Parent = Layer + ".Main",
				Components =
				{
					new CuiTextComponent
					{
						Text = _shopInstaller.GetMessage(player, "UIFinishTitle"),
						Font = "robotocondensed-regular.ttf", FontSize = 32,
						Align = TextAnchor.MiddleCenter, Color = "0.8862745 0.8588235 0.827451 1"
					},
					new CuiRectTransformComponent
						{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-400 196", OffsetMax = "400 252"}
				}
			});

			#endregion Label.Welcome

			#region Label.Thank.For.Buy

			container.Add(new CuiElement
			{
				Parent = Layer + ".Main",
				Components =
				{
					new CuiTextComponent
					{
						Text = _shopInstaller.GetMessage(player, "UIFinishDescription"),
						Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.UpperCenter,
						Color = "0.8862745 0.8588235 0.827451 1"
					},
					new CuiRectTransformComponent
						{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-400 -114", OffsetMax = "400 151" }
				}
			});

			#endregion Label.Thank.For.Buy

			#region QR.Panel

			container.Add(new CuiPanel
			{
				Image = {Color = "0 0 0 0"},
				RectTransform = {AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-120 63", OffsetMax = "120 137"}
			}, Layer + ".Main", Layer + ".QR.Panel");

			#region qr code

			container.Add(new CuiElement
			{
				Parent = Layer + ".QR.Panel",
				Components =
				{
					new CuiRawImageComponent
					{
						Png = _shopInstaller.GetImage("Shop_Installer_Mevent_Discord_QR")
					},
					new CuiRectTransformComponent
						{AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "0 -37", OffsetMax = "74 37"}
				}
			});

			#endregion qr code

			#region title

			container.Add(new CuiElement
			{
				Parent = Layer + ".QR.Panel",
				Components =
				{
					new CuiTextComponent
					{
						Text = _shopInstaller.GetMessage(player, "UIQRMeventDiscordTitle"),
						Font = "robotocondensed-regular.ttf", FontSize = 14,
						Align = TextAnchor.MiddleLeft, Color = "0.8862745 0.8588235 0.827451 1"
					},
					new CuiRectTransformComponent
						{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "90 40", OffsetMax = "0 -12"}
				}
			});

			#endregion title

			#region description

			container.Add(new CuiElement
			{
				Parent = Layer + ".QR.Panel",
				Components =
				{
					new CuiInputFieldComponent()
					{
						Text = "https://discord.gg/kWtvUaTyBh",
						Font = "robotocondensed-regular.ttf",
						FontSize = 11, Align = TextAnchor.UpperLeft, 
						Color = "0.8862745 0.8588235 0.827451 0.5019608",
						HudMenuInput = true
					},
					new CuiRectTransformComponent
						{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "90 0", OffsetMax = "0 -35"}
				}
			});

			#endregion description

			#endregion QR.Panel

			#region Btn.Start.Install

			container.Add(new CuiButton()
			{
				Button =
				{
					Material = "assets/content/ui/namefontmaterial.mat",
					Color = "0 0.372549 0.7176471 1",
					Command = $"UI_Shop_Installer finish",
					Close = Layer
				},
				Text =
				{
					Text = _shopInstaller.GetMessage(player, "BtnFinish"),
					Font = "robotocondensed-regular.ttf", FontSize = 15,
					Align = TextAnchor.MiddleCenter, Color = "0.8862745 0.8588235 0.827451 1"
				},
				RectTransform =
					{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-120 -114", OffsetMax = "120 -54"}
			}, Layer + ".Main");

			#endregion Btn.Start.Install

			#region Line

			container.Add(new CuiPanel
			{
				Image = {Color = "0.2156863 0.2156863 0.2156863 0.5019608"},
				RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "360 -127", OffsetMax = "-360 -125"}
			}, Layer + ".Main");

			#endregion Line
		}

		private void ShowSelectTemplateStep(BasePlayer player, CuiElementContainer container, bool isFullScreen = true)
		{
			#region Background

			container.Add(new CuiPanel()
			{
				Image = {Color = "0 0 0 0"},
				RectTransform = {AnchorMin = "0 0.5", AnchorMax = "1 0.5", OffsetMin = "0 -360", OffsetMax = "0 248"}
			}, Layer, Layer + ".Main", Layer + ".Main");

			#endregion

			#region Title

			container.Add(new CuiElement
			{
				Parent = Layer + ".Main",
				Components = 
				{
					new CuiTextComponent 
					{ 
						Text = _shopInstaller.GetMessage(player, isFullScreen ? "UISelectTemplateTitleFullscreen" : "UISelectTemplateTitleInMenu"),
						Font = "robotocondensed-regular.ttf", FontSize = 32, Align = TextAnchor.MiddleCenter, Color = "0.8862745 0.8588235 0.827451 1"
					},
					new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-400 234", OffsetMax = "400 289" }
				}
			});

			#endregion Title

			#region Label.Message

			container.Add(new CuiElement
			{
				Parent = Layer + ".Main",
				Components =
				{
					new CuiTextComponent
					{
						Text = _shopInstaller.GetMessage(player, "UISelectTemplateDescription"),
						Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.UpperCenter,
						Color = "0.8862745 0.8588235 0.827451 1"
					},
					new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-400 147", OffsetMax = "400 234" }
				}
			});

			#endregion Label.Message

			#region Line

			container.Add(new CuiPanel
			{
				Image = {Color = HexToCuiColor("#373737", 50)},
				RectTransform = {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-360 145", OffsetMax = "360 147"}
			}, Layer + ".Main");

			#endregion Line

			#region ScrollView

			var targetTemplate = isFullScreen ? _shopInstaller.shopData.FullScreenTemplates : _shopInstaller.shopData.InMenuTemplates;

			var templateLines = Mathf.CeilToInt(targetTemplate.Length / 2f);

			var totalHeight = templateLines * UI_Installer_Template_Height + (templateLines - 1) * UI_Installer_Template_Margin_Y;

			totalHeight += 100;

			totalHeight = Math.Max(totalHeight, 410);

			container.Add(new CuiPanel
			{
				Image = {Color = "0 0 0 0"},
				RectTransform =
					{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-360 -293", OffsetMax = "377 123"}
			}, Layer + ".Main", Layer + ".ScrollBackground");

			container.Add(new CuiElement()
			{
				Parent = Layer + ".ScrollBackground",
				Name = Layer + ".ScrollView",
				DestroyUi = Layer + ".ScrollView",
				Components =
				{
					new CuiScrollViewComponent
					{
						MovementType = ScrollRect.MovementType.Elastic,
						Vertical = true,
						Inertia = true,
						Horizontal = false,
						Elasticity = 0.25f,
						DecelerationRate = 0.3f,
						ScrollSensitivity = 24f,
						ContentTransform = new CuiRectTransform
						{
							AnchorMin = "0 1",
							AnchorMax = "1 1",
							OffsetMin = $"0 -{totalHeight}",
							OffsetMax = "0 0"
						},
						VerticalScrollbar = new CuiScrollbar()
						{
							Size = 3f, AutoHide = false,
							HandleColor = HexToCuiColor("#D74933"),
						},
					}
				}
			});

			#endregion ScrollView

			#region Templates

			LoopTemplates(container, isFullScreen);

			#endregion

			#region Hover

			container.Add(new CuiElement
			{
				Name = Layer + ".Hover",
				Parent = Layer + ".Main",
				Components =
				{
					new CuiRawImageComponent
						{Color = "0 0 0 1", Sprite = "assets/content/ui/UI.Background.Transparent.Linear.psd"},
					new CuiRectTransformComponent
						{AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 105"}
				}
			});

			#endregion Hover

			#region Btn.Start.Install

			container.Add(new CuiButton()
			{
				Button =
				{
					Material = "assets/content/ui/namefontmaterial.mat",
					Color = "0 0.372549 0.7176471 1",
					Command = $"UI_Shop_Installer change_step {_shopInstaller.step + 1}"
				},
				Text =
				{
					Text = _shopInstaller.GetMessage(player, "BtnContinueInstalling"),
					Font = "robotocondensed-regular.ttf", FontSize = 15,
					Align = TextAnchor.MiddleCenter, Color = "0.8862745 0.8588235 0.827451 1"
				},
				RectTransform = {AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "10 20", OffsetMax = "250 62"}
			}, Layer + ".Main");

			#endregion Btn.Start.Install

			#region Btn.Go.Back

			container.Add(new CuiButton()
			{
				RectTransform =
					{AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-250 20", OffsetMax = "-10 62"},
				Text =
				{
					Text = _shopInstaller.GetMessage(player, "BtnGoBack"),
					Font = "robotocondensed-regular.ttf", FontSize = 15,
					Align = TextAnchor.MiddleCenter, Color = "0.8862745 0.8588235 0.827451 0.6"
				},
				Button =
				{
					Material = "assets/content/ui/namefontmaterial.mat",
					Color = "0.145098 0.145098 0.145098 1",
					Command = $"UI_Shop_Installer change_step {_shopInstaller.step - 1}"
				},
			}, Layer + ".Main");

			#endregion Btn.Go.Back
		}

		private void LoopTemplates(CuiElementContainer container,  bool isFullScreen)
		{
			var offsetX = 0;
			var offsetY = 0;

			var targetTemplate = isFullScreen ? _shopInstaller.shopData.FullScreenTemplates : _shopInstaller.shopData.InMenuTemplates;

			for (var i = 0; i < targetTemplate.Length; i++)
			{
				var panelTemplate = targetTemplate[i];
				var isSelected = (isFullScreen ? _shopInstaller.targetTemplateIndex : _shopInstaller.targetInMenuTemplateIndex) == i;

				container.Add(new CuiElement
				{
					Name = Layer + $".Templates.{i}",
					DestroyUi = Layer + $".Templates.{i}",
					Parent = Layer + ".ScrollView",
					Components =
					{
						new CuiImageComponent()
						{
							Color = "0 0 0 0"
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 1", AnchorMax = "0 1",
							OffsetMin = $"{offsetX} {offsetY - UI_Installer_Template_Height}",
							OffsetMax = $"{offsetX + UI_Installer_Template_Width} {offsetY}"
						}
					}
				});

				#region Banner Image

				container.Add(new CuiElement
				{
					Parent = Layer + $".Templates.{i}",
					Components =
					{
						new CuiRawImageComponent
						{
							Png = GetImage(panelTemplate.BannerURL)
						},
						new CuiRectTransformComponent
							{AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-175 0", OffsetMax = "175 160"}
					}
				});

				#endregion

				#region Outline

				container.Add(new CuiElement
				{
					Parent = Layer + $".Templates.{i}",
					Components =
					{
						new CuiRawImageComponent
						{
							Png = _shopInstaller.GetImage(isSelected
								? "Shop_Installer_BannerOutline_Selected"
								: "Shop_Installer_BannerOutline")
						},
						new CuiRectTransformComponent 
						{
							AnchorMin = "0 0", AnchorMax = "0 0", 
							OffsetMin = "0 0", OffsetMax = "350 198"
						}
					}
				});

				#endregion

				#region Title

				container.Add(new CuiElement
				{
					Parent = Layer + $".Templates.{i}",
					Components =
					{
						new CuiTextComponent
						{
							Text = panelTemplate.Title, Font = "robotocondensed-regular.ttf", FontSize = 15,
							Align = TextAnchor.MiddleLeft, Color = "0.8862745 0.8588235 0.827451 1"
						},
						new CuiRectTransformComponent
							{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "58 160", OffsetMax = "0 0"}
					}
				});

				#endregion

				#region Button

				container.Add(new CuiElement()
				{
					Parent = Layer + $".Templates.{i}",
					Components =
					{
						new CuiButtonComponent()
						{
							Color = "0 0 0 0",
							Command = $"UI_Shop_Installer select_template {i} {isFullScreen}"
						},
						new CuiRectTransformComponent()
					}
				});

				#endregion

				#region Show Video Button

				if (!string.IsNullOrWhiteSpace(panelTemplate.VideoURL))
					container.Add(new CuiElement()
					{
						Parent = Layer + $".Templates.{i}",
						Components =
						{
							new CuiButtonComponent()
							{
								Color = "0 0 0 0",
								Command = $"UI_Shop_Installer template_preview {i} {isFullScreen}"
							},
							new CuiRectTransformComponent
							{
								AnchorMin = "0 1", AnchorMax = "0 1",
								OffsetMin = "0 -43", OffsetMax = "43 0"
							}
						}
					});

				#endregion Show Video Button

				#region Calculate Position

				if ((i + 1) % 2 == 0)
				{
					offsetX = 0;
					offsetY = offsetY - UI_Installer_Template_Height - UI_Installer_Template_Margin_Y;
				}
				else
				{
					offsetX += UI_Installer_Template_Width + UI_Installer_Template_Margin_X;
				}

				#endregion
			}
		}

		private void ShowDependenciesStep(BasePlayer player, CuiElementContainer container)
		{
			#region Background

			container.Add(new CuiPanel()
			{
				Image = {Color = "0 0 0 0"},
				RectTransform = {AnchorMin = "0 0.5", AnchorMax = "1 0.5", OffsetMin = "0 -360", OffsetMax = "0 248"}
			}, Layer, Layer + ".Main", Layer + ".Main");

			#endregion

			#region Label.Message

			container.Add(new CuiElement
			{
				Parent = Layer + ".Main",
				Components =
				{
					new CuiTextComponent
					{
						Text = _shopInstaller.GetMessage(player, "UIDependenciesDescription"),
						Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.UpperCenter,
						Color = "0.8862745 0.8588235 0.827451 1"
					},
					new CuiRectTransformComponent
						{ AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-400 171", OffsetMax = "400 244"}
				}
			});

			#endregion Label.Message

			#region Line

			container.Add(new CuiPanel
			{
				Image = {Color = "0.2156863 0.2156863 0.2156863 0.5019608"},
				RectTransform = {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-360 169", OffsetMax = "360 171" }
			}, Layer + ".Main");

			#endregion Line

			#region ScrollView

			var totalHeight = _shopInstaller.GetShopDependencies().Length * Dependency_Height + (_shopInstaller.GetShopDependencies().Length - 1) * Dependency_Margin_Y;

			totalHeight += 100;

			totalHeight = Math.Max(totalHeight, 410);

			container.Add(new CuiPanel
			{
				Image = {Color = "0 0 0 0"},
				RectTransform =
					{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-360 -262", OffsetMax = "377 148"}
			}, Layer + ".Main", Layer + ".ScrollBackground", Layer + ".ScrollBackground");

			container.Add(new CuiElement()
			{
				Parent = Layer + ".ScrollBackground",
				Name = Layer + ".ScrollView",
				DestroyUi = Layer + ".ScrollView",
				Components =
				{
					new CuiScrollViewComponent
					{
						MovementType = ScrollRect.MovementType.Elastic,
						Vertical = true,
						Inertia = true,
						Horizontal = false,
						Elasticity = 0.25f,
						DecelerationRate = 0.3f,
						ScrollSensitivity = 24f,
						ContentTransform = new CuiRectTransform
						{
							AnchorMin = "0 1",
							AnchorMax = "1 1",
							OffsetMin = $"0 -{totalHeight}",
							OffsetMax = "0 0"
						},
						VerticalScrollbar = new CuiScrollbar()
						{
							Size = 5f, AutoHide = false,
							HandleColor = HexToCuiColor("#D74933"),
						},
					}
				}
			});

			#endregion ScrollView

			#region Dependencies

			var mainOffset = 0;
			foreach (var panelDependency in _shopInstaller.GetSortedShopDependencies())
			{
				var status = panelDependency.GetStatus();

				container.Add(new CuiPanel
					{
						Image =
						{
							Color = "0.572549 0.572549 0.572549 0.2"
						},
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "0 1",
							OffsetMin = $"0 {mainOffset - Dependency_Height}", OffsetMax = $"720 {mainOffset}"
						}
					}, Layer + ".ScrollView", Layer + $".Dependencies.{panelDependency.PluginName}",
					Layer + $".Dependencies.{panelDependency.PluginName}");

				#region Title

				container.Add(new CuiElement
				{
					Parent = Layer + $".Dependencies.{panelDependency.PluginName}",
					Components =
					{
						new CuiTextComponent
						{
							Text = panelDependency.PluginName,
							Font = "robotocondensed-bold.ttf", FontSize = 15,
							Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#E2DBD3")
						},
						new CuiRectTransformComponent
							{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "46 0", OffsetMax = "-495 0"}
					}
				});

				#endregion

				#region Status.Icon

				var colorIcon = _shopInstaller.GetColorFromDependencyStatus(status);

				container.Add(new CuiElement
				{
					Parent = Layer + $".Dependencies.{panelDependency.PluginName}",
					Components =
					{
						new CuiRawImageComponent
						{
							Color = colorIcon,
							Sprite = "assets/content/ui/Waypoint.Outline.TeamTop.png"
						},
						new CuiRectTransformComponent
							{AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "18 -9", OffsetMax = "36 9"}
					}
				});

				#endregion Status.Icon

				#region Status.Title

				container.Add(new CuiElement
				{
					Parent = Layer + $".Dependencies.{panelDependency.PluginName}",
					Components =
					{
						new CuiTextComponent
						{
							Text = _shopInstaller.GetMessage(player,panelDependency.Messages[status].Title),
							Font = "robotocondensed-bold.ttf", FontSize = 14,
							Align = TextAnchor.LowerLeft, Color = HexToCuiColor("#E2DBD3")
						},
						new CuiRectTransformComponent
							{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "250 26", OffsetMax = "0 0"}
					}
				});

				#endregion Status.Title

				#region Status.Description

				container.Add(new CuiElement
				{
					Parent = Layer + $".Dependencies.{panelDependency.PluginName}",
					Components =
					{
						new CuiInputFieldComponent()
						{
							Text = _shopInstaller.GetMessage(player,panelDependency.Messages[status].Description),
							ReadOnly = true,
							Font = "robotocondensed-regular.ttf", FontSize = 12,
							Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#E2DBD3", 50)
						},
						new CuiRectTransformComponent
							{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "250 0", OffsetMax = "0 -35"}
					}
				});

				#endregion Status.Description

				#region Line

				container.Add(new CuiPanel
					{
						Image = {Color = "0.2156863 0.2156863 0.2156863 1"},
						RectTransform =
							{AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = "224 9", OffsetMax = "226 -13"}
					}, Layer + $".Dependencies.{panelDependency.PluginName}");

				#endregion

				mainOffset = mainOffset - Dependency_Height - Dependency_Margin_Y;
			}

			#endregion

			#region Hover

			container.Add(new CuiElement
			{
				Name = Layer + ".Hover",
				Parent = Layer + ".Main",
				Components =
				{
					new CuiRawImageComponent
						{Color = "0 0 0 1", Sprite = "assets/content/ui/UI.Background.Transparent.Linear.psd"},
					new CuiRectTransformComponent
						{AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 105"}
				}
			});

			#endregion Hover

			#region Btn.Start.Install

			container.Add(new CuiButton()
			{
				Button =
				{
					Material = "assets/content/ui/namefontmaterial.mat",
					Color = "0 0.372549 0.7176471 1",
					Command = $"UI_Shop_Installer change_step {_shopInstaller.step + 1}"
				},
				Text =
				{
					Text = _shopInstaller.GetMessage(player, "BtnContinueInstalling"),
					Font = "robotocondensed-regular.ttf", FontSize = 15,
					Align = TextAnchor.MiddleCenter, Color = "0.8862745 0.8588235 0.827451 1"
				},
				RectTransform =
					{AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "10 20", OffsetMax = "250 62"}
			}, Layer + ".Main");

			#endregion Btn.Start.Install

			#region Btn.Go.Back

			container.Add(new CuiButton()
			{
				RectTransform =
					{AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-250 20", OffsetMax = "-10 62"},
				Text =
				{
					Text = _shopInstaller.GetMessage(player, "BtnGoBack"),
					Font = "robotocondensed-regular.ttf", FontSize = 15,
					Align = TextAnchor.MiddleCenter, Color = "0.8862745 0.8588235 0.827451 0.6"
				},
				Button =
				{
					Material = "assets/content/ui/namefontmaterial.mat",
					Color = "0.145098 0.145098 0.145098 1",
					Command = $"UI_Shop_Installer change_step {_shopInstaller.step - 1}"
				},
			}, Layer + ".Main");

			#endregion Btn.Go.Back
		}
		
		private void ShowWelcomeStep(BasePlayer player, CuiElementContainer container)
		{
			#region Background

			container.Add(new CuiPanel()
			{
				Image = {Color = "0 0 0 0"},
				RectTransform = {AnchorMin = "0 0.5", AnchorMax = "1 0.5", OffsetMin = "0 -360", OffsetMax = "0 248"}
			}, Layer, Layer + ".Main", Layer + ".Main");

			#endregion

			#region Label.Welcome

			container.Add(new CuiElement
			{
				Parent = Layer + ".Main",
				Components =
				{
					new CuiTextComponent
					{
						Text = _shopInstaller.GetMessage(player, "UIWelcome"),
						Font = "robotocondensed-regular.ttf", FontSize = 32,
						Align = TextAnchor.MiddleCenter, Color = "0.8862745 0.8588235 0.827451 1"
					},
					new CuiRectTransformComponent
						{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-400 196", OffsetMax = "400 252"}
				}
			});

			#endregion Label.Welcome

			#region Label.Thank.For.Buy

			container.Add(new CuiElement
			{
				Parent = Layer + ".Main",
				Components =
				{
					new CuiTextComponent
					{
						Text = _shopInstaller.GetMessage(player, "UIThankForBuying"),
						Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.UpperCenter,
						Color = "0.8862745 0.8588235 0.827451 1"
					},
					new CuiRectTransformComponent
						{ AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-400 -84", OffsetMax = "400 151"}
				}
			});

			#endregion Label.Thank.For.Buy

			#region Btn.Start.Install

			container.Add(new CuiButton()
			{
				Button =
				{					
					Material = "assets/content/ui/namefontmaterial.mat",
					Color = "0 0.372549 0.7176471 1",
					Command = $"UI_Shop_Installer change_step {_shopInstaller.step + 1}"
				},
				Text =
				{
					Text = _shopInstaller.GetMessage(player, "BtnStartInstall"),
					Font = "robotocondensed-regular.ttf", FontSize = 15,
					Align = TextAnchor.MiddleCenter, Color = "0.8862745 0.8588235 0.827451 1"
				},
				RectTransform =
					{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-120 -96", OffsetMax = "120 -36"}
			}, Layer + ".Main");

			#endregion Btn.Start.Install

			#region Btn.Cancel

			container.Add(new CuiPanel
			{
				Image = {Color = "0 0 0 0"},
				RectTransform =
					{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-120 -151", OffsetMax = "120 -121"}
			}, Layer + ".Main", Layer + ".Btn.Cancel");

			#region Title

			container.Add(new CuiElement
			{
				Parent = Layer + ".Btn.Cancel",
				Components =
				{
					new CuiTextComponent
					{
						Text = _shopInstaller.GetMessage(player, "BtnCancelAndClose"),
						Font = "robotocondensed-regular.ttf", FontSize = 14,
						Align = TextAnchor.MiddleCenter, Color = "0.8862745 0.8588235 0.827451 0.5019608"
					},
					new CuiRectTransformComponent
						{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "22 0", OffsetMax = "0 0"}
				}
			});

			#endregion Title

			#region Icon

			container.Add(new CuiElement
			{
				Parent = Layer + ".Btn.Cancel",
				Components =
				{
					new CuiImageComponent
						{Color = "0.8862745 0.8588235 0.827451 0.5019608", Sprite = "assets/icons/close.png"},
					new CuiRectTransformComponent
						{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-112 -7", OffsetMax = "-98 7"}
				}
			});

			#endregion Icon

			#region Button

			container.Add(new CuiElement
			{
				Parent = Layer + ".Btn.Cancel",
				Components =
				{
					new CuiButtonComponent()
					{
						Color = "0 0 0 0",
						Command = $"UI_Shop_Installer cancel",
						Close = Layer
					},
					new CuiRectTransformComponent()
				}
			});

			#endregion Button

			#endregion Btn.Cancel

			#region Line

			container.Add(new CuiPanel
			{
				Image = {Color = "0.2156863 0.2156863 0.2156863 0.5019608"},
				RectTransform = {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-280 177", OffsetMax = "280 179"}
			}, Layer + ".Main");

			#endregion Line
		}

		#endregion

		#region Installer Helpers

		private Dictionary<string, string> GetMessageFile(string plugin, string langKey = "en")
		{
			if (string.IsNullOrEmpty(plugin))
				return null;
			foreach (var invalidFileNameChar in Path.GetInvalidFileNameChars())
				langKey = langKey.Replace(invalidFileNameChar, '_');

			var path = Path.Combine(Interface.Oxide.LangDirectory,
				string.Format("{0}{1}{2}.json", langKey, Path.DirectorySeparatorChar, plugin));
			return !File.Exists(path)
				? null
				: JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path));
		}

		private void RegisterTemplateMessages(Dictionary<string, ShopInstallerLang> templateLang)
		{
			foreach (var (langKey, msgData) in templateLang)
			{
				var existingMessages = GetMessageFile(Name, langKey);
				if (existingMessages == null) continue;

				foreach (var (key, value) in msgData.Messages)
					existingMessages[key] = value;

				var str1 = $"{(object) langKey}{(object) Path.DirectorySeparatorChar}{(object) _instance.Name}.json";

				File.WriteAllText(Path.Combine(Interface.Oxide.LangDirectory, str1),
					JsonConvert.SerializeObject(existingMessages, Formatting.Indented));
			}
		}

		private void ReplaceMessages(ref Dictionary<string, string> existingMessages,
			Dictionary<string, string> messages)
		{
			foreach (var message in messages)
			{
				existingMessages[message.Key] = message.Value;
			}
		}

		#endregion
        
		#endregion Installer
	}
}

#region Extension Methods

namespace Oxide.Plugins.ShopExtensionMethods
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

		public static float Min<T>(this IEnumerable<T> source, Func<T, float> selector)
		{
			using var e = source.GetEnumerator();
			var value = selector(e.Current);
			if (float.IsNaN(value)) return value;

			while (e.MoveNext())
			{
				var x = selector(e.Current);
				if (x < value)
					value = x;
				else if (float.IsNaN(x)) return x;
			}

			return value;
		}

		public static float Max<TSource>(this IEnumerable<TSource> source, Func<TSource, float> selector)
		{
			using var e = source.GetEnumerator();
			if (!e.MoveNext()) return 0;

			var value = selector(e.Current);
			while (e.MoveNext())
			{
				var x = selector(e.Current);
				if (x > value) value = x;
			}

			return value;
		}

		public static T FirstOrDefault<T>(this IEnumerable<T> a, Func<T, bool> b = null)
		{
			using var c = a.GetEnumerator();
			while (c.MoveNext())
				if (b == null || b(c.Current))
					return c.Current;

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
			using var c = a.GetEnumerator();
			while (c.MoveNext()) b.Add(c.Current);

			return b;
		}

		public static HashSet<T> ToHashSet<T>(this IEnumerable<T> a)
		{
			return new HashSet<T>(a);
		}

		public static List<T> Where<T>(this IEnumerable<T> source, Func<T, bool> predicate)
		{
			var c = new List<T>();

			using var d = source.GetEnumerator();
			while (d.MoveNext())
				if (predicate(d.Current))
					c.Add(d.Current);

			return c;
		}

		public static List<T> OfType<T>(this IEnumerable<BaseNetworkable> a) where T : BaseEntity
		{
			var b = new List<T>();
			using var c = a.GetEnumerator();
			while (c.MoveNext())
			{
				var entity = c.Current as T;
				if (entity != null)
					b.Add(entity);
			}

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

		private static bool IsReallyValid(this BaseNetworkable a)
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

		public static List<T> SkipAndTake<T>(this List<T> source, int skip, int take)
		{
			var index = Mathf.Min(Mathf.Max(skip, 0), source.Count);
			return source.GetRange(index, Mathf.Min(take, source.Count - index));
		}
	}
}

#endregion Extension Methods