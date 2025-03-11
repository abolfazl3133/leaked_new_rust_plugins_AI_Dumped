using Enumerable = System.Linq.Enumerable;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Plugins.ShopExtensionMethods;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
	/*ПЛАГИН БЫЛ ПОФИКШЕН С ПОМОЩЬЮ ПРОГРАММЫ СКАЧАНОЙ С https://discord.gg/dNGbxafuJn */ [Info("Shop", "https://discord.gg/dNGbxafuJn", "1.2.14")]
	public class Shop : RustPlugin
	{
		#region Fields

		[PluginReference] private Plugin
			ImageLibrary = null,
			ItemCostCalculator = null,
			Notify = null,
			UINotify = null,
			NoEscape = null,
			Duel = null,
			Duelist = null;

		private static Shop _instance;

		private bool _enabledImageLibrary;

		private readonly Dictionary<int, ShopItem> _shopItems = new Dictionary<int, ShopItem>();

		private readonly Dictionary<BasePlayer, Coroutine> _coroutines = new Dictionary<BasePlayer, Coroutine>();

		private readonly Dictionary<string, List<KeyValuePair<int, string>>> _itemsCategories =
			new Dictionary<string, List<KeyValuePair<int, string>>>();

		private readonly List<string> _images = new List<string>();

		private List<ulong> _openedUI = new List<ulong>();

		private const string Layer = "UI.Shop";

		private const string ModalLayer = "UI.Shop.Modal";

		private const string EditingLayer = "UI.Shop.Editing";

		private const string PermAdmin = "Shop.admin";

		private const string PermFreeBypass = "Shop.free";

		private const string PermSetVM = "Shop.setvm";

		private const string PermSetNPC = "Shop.setnpc";

		private const int _itemsPerTick = 10;

		private readonly Dictionary<ulong, DataCart> _carts = new Dictionary<ulong, DataCart>();

		private readonly Dictionary<ulong, Dictionary<string, DataCart>> _cartsNPC =
			new Dictionary<ulong, Dictionary<string, DataCart>>();

		private readonly Dictionary<BasePlayer, NPCShop> _openedShops = new Dictionary<BasePlayer, NPCShop>();

		private readonly Dictionary<BasePlayer, Dictionary<string, object>> _itemEditing =
			new Dictionary<BasePlayer, Dictionary<string, object>>();

		private Timer _updateController;

		#endregion

		#region Colors

		private string _firstColor;
		private string _secondColor;
		private string _thirdColor;
		private string _fourthColor;
		private string _fifthColor;
		private string _sixthColor;
		private string _seventhColor;

		#endregion

		#region Config

		private static Configuration _config;

		private class Configuration
		{
			#region Fields

			[JsonProperty(PropertyName = "Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] Commands = {"shop", "shops"};

			[JsonProperty(PropertyName = "Enable money transfers between players??")]
			public bool Transfer = true;

			[JsonProperty(PropertyName = "Enable logging to the console?")]
			public bool LogToConsole = true;

			[JsonProperty(PropertyName = "Enable logging to the file?")]
			public bool LogToFile = true;

			[JsonProperty(PropertyName = "Load images when logging into the server?")]
			public bool LoginImages = true;

			[JsonProperty(PropertyName = "Work with Notify?")]
			public bool UseNotify = true;

			[JsonProperty(PropertyName = "Can admins edit? (by flag)")]
			public bool FlagAdmin = true;

			[JsonProperty(PropertyName = "Block (NoEscape)")]
			public bool BlockNoEscape = false;

			[JsonProperty(PropertyName = "Wipe Block")]
			public bool WipeCooldown = false;

			[JsonProperty(PropertyName = "Wipe Cooldown")]
			public float WipeCooldownTimer = 3600;

			[JsonProperty(PropertyName = "Respawn Block")]
			public bool RespawnCooldown = true;

			[JsonProperty(PropertyName = "Respawn Cooldown")]
			public float RespawnCooldownTimer = 60;

			[JsonProperty(PropertyName = "Blocking the opening in duels?")]
			public bool UseDuels = false;

			[JsonProperty(PropertyName = "Delay between loading images")]
			public float ImagesDelay = 1f;

			[JsonProperty(PropertyName = "Economy")]
			public EconomyConf Economy = new EconomyConf
			{
				Type = EconomyType.Plugin,
				AddHook = "Deposit",
				BalanceHook = "Balance",
				RemoveHook = "Withdraw",
				Plug = "Economics",
				ShortName = "scrap",
				DisplayName = string.Empty,
				Skin = 0,
				TitleLangKey = "LangTitle",
				BalanceLangKey = "BalanceTitle"
			};

			[JsonProperty(PropertyName = "Additional Economics",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<AdditionalEconomy> AdditionalEconomics = new List<AdditionalEconomy>
			{
				new AdditionalEconomy
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
					TitleLangKey = "sr_title",
					BalanceLangKey = "sr_balance"
				}
			};

			[JsonProperty(PropertyName = "Shop", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<ShopCategory> Shop = new List<ShopCategory>();

			[JsonProperty(PropertyName = "First Color")]
			public string FirstColor = "#161617";

			[JsonProperty(PropertyName = "Second Color")]
			public string SecondColor = "#4B68FF";

			[JsonProperty(PropertyName = "Third Color")]
			public string ThirdColor = "#0E0E10";

			[JsonProperty(PropertyName = "Fourth Color")]
			public string FourthColor = "#A0A935";

			[JsonProperty(PropertyName = "Fifth Color")]
			public string FifthColor = "#FF4B4B";

			[JsonProperty(PropertyName = "Sixth Color")]
			public string SixthColor = "#324192";

			[JsonProperty(PropertyName = "Seventh Color")]
			public string SeventhColor = "#CD3838";

			[JsonProperty(PropertyName = "NPC Shops (NPC ID - shop categories)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, NPCShop> NPCs = new Dictionary<string, NPCShop>
			{
				["1234567"] = new NPCShop
				{
					Shops = new List<string>
					{
						"Tool",
						"Food"
					}
				},
				["7654321"] = new NPCShop
				{
					Shops = new List<string>
					{
						"Weapon",
						"Ammunition"
					}
				},
				["4644687478"] = new NPCShop
				{
					Shops = new List<string>
					{
						"*"
					}
				}
			};

			[JsonProperty(PropertyName = "Interface")]
			public UserInterface UI = new UserInterface
			{
				DisplayType = "Overlay",
				Width = 770,
				Height = 500,
				CategoriesOnString = 9,
				CategoriesMargin = 7.5f,
				CategoriesHeight = 40,
				ItemsOnString = 4,
				Strings = 2,
				ItemWidth = 150,
				ItemHeight = 165,
				Margin = 35,
				UseScrollCategories = true,
				EnableSearch = true,
				RoundDigits = 5,
				SelectCurrency = new SelectCurrencyUI
				{
					Title = new LabelSettings
					{
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 14,
						Color = new IColor("#FFFFFF")
					},
					TitlePosition = new InterfacePosition
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "0 -30", OffsetMax = "0 0"
					},
					EconomyTitle = new LabelSettings
					{
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = new IColor("#FFFFFF")
					},
					SelectedEconomyColor = new IColor("#4B68FF"),
					UnselectedEconomyColor = new IColor("#4B68FF", 33),
					EconomyWidth = 105f,
					EconomyHeight = 25f,
					EconomyMargin = 5f,
					EconomyIndent = 5f,
					FrameWidth = 10,
					FrameIndent = 15,
					FrameHeader = 35,
					CloseAfterChange = true
				}
			};

			[JsonProperty(PropertyName = "Blocked skins for sell",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, List<ulong>> BlockedSkins = new Dictionary<string, List<ulong>>
			{
				["short name"] = new List<ulong>
				{
					52,
					25
				},

				["short name 2"] = new List<ulong>
				{
					52,
					25
				}
			};

			[JsonProperty(PropertyName = "Auto-Wipe Settings")]
			public WipeSettings Wipe = new WipeSettings
			{
				Cooldown = true,
				Players = true,
				Limits = true
			};

			[JsonProperty(PropertyName = "Custom Vending Machines (Entity ID - settings)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<ulong, CustomVendingConf> CustomVending =
				new Dictionary<ulong, CustomVendingConf>
				{
					[123343941] = new CustomVendingConf
					{
						Categories = new List<string>
						{
							"Cars", "Misc"
						}
					}
				};

			[JsonProperty(PropertyName = "Settings available containers for selling item")]
			public SellContainers SellContainers = new SellContainers
			{
				Enabled = true,
				Containers = new List<string>
				{
					"main",
					"belt"
				}
			};

			public VersionNumber Version;

			#endregion

			#region Classes

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

		private class SellContainers
		{
			[JsonProperty(PropertyName = "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = "Available Containers (main, belt, wear)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> Containers = new List<string>();

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
				var items = new List<Item>();

				GetContainers(player).ForEach(cont => items.AddRange(cont.itemList));

				return items.ToArray();
			}
		}

		private class SelectCurrencyUI
		{
			[JsonProperty(PropertyName = "Title")] public LabelSettings Title;

			[JsonProperty(PropertyName = "Title Position")]
			public InterfacePosition TitlePosition;

			[JsonProperty(PropertyName = "Economy Title")]
			public LabelSettings EconomyTitle;

			[JsonProperty(PropertyName = "Selected Economy Color")]
			public IColor SelectedEconomyColor;

			[JsonProperty(PropertyName = "Unselected Economy Color")]
			public IColor UnselectedEconomyColor;

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
		}

		private class InterfacePosition
		{
			public string AnchorMin;

			public string AnchorMax;

			public string OffsetMin;

			public string OffsetMax;
		}

		private class LabelSettings
		{
			[JsonProperty(PropertyName = "FontSize")]
			public int FontSize;

			[JsonProperty(PropertyName = "Align")] [JsonConverter(typeof(StringEnumConverter))]
			public TextAnchor Align;

			[JsonProperty(PropertyName = "Color")] public IColor Color;

			[JsonProperty(PropertyName = "Font")] public string Font;
		}

		private class IColor
		{
			[JsonProperty(PropertyName = "HEX")] public string Hex;

			[JsonProperty(PropertyName = "Opacity (0 - 100)")]
			public readonly float Alpha;

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

			public IColor()
			{
			}

			public IColor(string hex, float alpha = 100)
			{
				Hex = hex;
				Alpha = alpha;
			}
		}

		private class CustomVendingConf
		{
			[JsonProperty(PropertyName = "Categories (Titles) [* - all]",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> Categories;
		}

		private class WipeSettings
		{
			[JsonProperty(PropertyName = "Wipe Cooldowns?")]
			public bool Cooldown;

			[JsonProperty(PropertyName = "Wipe Players?")]
			public bool Players;

			[JsonProperty(PropertyName = "Wipe Limits?")]
			public bool Limits;
		}

		private class UserInterface
		{
			[JsonProperty(PropertyName = "Display type (Overlay/Hud)")]
			public string DisplayType;

			[JsonProperty(PropertyName = "Height")]
			public float Height;

			[JsonProperty(PropertyName = "Width")] public float Width;

			[JsonProperty(PropertyName = "Categories On Page")]
			public int CategoriesOnString;

			[JsonProperty(PropertyName = "Categories Margin")]
			public float CategoriesMargin;

			[JsonProperty(PropertyName = "Categories Height")]
			public float CategoriesHeight;

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

			[JsonProperty(PropertyName = "Use scroll in categories?")]
			public bool UseScrollCategories;

			[JsonProperty(PropertyName = "Enable search?")]
			public bool EnableSearch;

			[JsonProperty(PropertyName = "Number of digits after decimal point for rounding prices")]
			public int RoundDigits;

			[JsonProperty(PropertyName = "Select Currency Settings")]
			public SelectCurrencyUI SelectCurrency;
		}

		private class NPCShop
		{
			[JsonProperty(PropertyName = "Categories (Titles) [* - all]",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> Shops;

			[JsonIgnore] public string BotID;
		}

		private class ShopCategory
		{
			[JsonProperty(PropertyName = "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = "Title")] public string Title;

			[JsonProperty(PropertyName = "Permission")]
			public string Permission;

			[JsonProperty(PropertyName = "Sort Type")]
			[JsonConverter(typeof(StringEnumConverter))]
			[DefaultValue(Configuration.SortType.None)]
			public Configuration.SortType SortType;

			[JsonProperty(PropertyName = "Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<ShopItem> Items;

			[JsonIgnore] public List<ShopItem> SortedItems;

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
							if (SortedItems == null) SortItems();

							return SortedItems;
					}
				}
			}

			public void SortItems()
			{
				switch (SortType)
				{
					case Configuration.SortType.Name:
					{
						SortedItems = Items.ToList();
						SortedItems.Sort((x, y) =>
							string.Compare(x.PublicTitle, y.PublicTitle, StringComparison.Ordinal));
						break;
					}
					case Configuration.SortType.Amount:
					{
						SortedItems = Items.ToList();
						SortedItems.Sort((x, y) => x.Amount.CompareTo(y.Amount));
						break;
					}
					case Configuration.SortType.PriceIncrease:
					{
						SortedItems = Items.ToList();
						SortedItems.Sort((x, y) => x.Amount.CompareTo(y.Price));
						break;
					}
					case Configuration.SortType.PriceDecrease:
					{
						SortedItems = Items.ToList();
						SortedItems.Sort((x, y) => y.Amount.CompareTo(x.Price));
						break;
					}
					default:
					{
						SortedItems = Items.ToList();
						break;
					}
				}

				LoadIDs(true);
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

					if (item.Type == ItemType.Item && item.Definition == null && !string.IsNullOrEmpty(item.ShortName))
						item.Definition = !string.IsNullOrEmpty(item.ShortName)
							? ItemManager.FindItemDefinition(item.ShortName)
							: null;
				});
			}
		}

		private enum ItemType
		{
			Item,
			Command,
			Plugin,
			Kit
		}

		private class ShopItem
		{
			[JsonProperty(PropertyName = "Type")] [JsonConverter(typeof(StringEnumConverter))]
			public ItemType Type;

			[JsonProperty(PropertyName = "ID")] public int ID;

			[JsonProperty(PropertyName = "Image")] public string Image;

			[JsonProperty(PropertyName = "Title")] public string Title;

			[JsonProperty(PropertyName = "Description")]
			public string Description = string.Empty;

			[JsonProperty(PropertyName = "Command (%steamid%)")]
			public string Command;

			[JsonProperty(PropertyName = "Kit")] public string Kit = string.Empty;

			[JsonProperty(PropertyName = "Plugin")]
			public PluginItem Plugin;

			[JsonProperty(PropertyName = "DisplayName (empty - default)")]
			public string DisplayName;

			[JsonProperty(PropertyName = "ShortName")]
			public string ShortName;

			[JsonProperty(PropertyName = "Skin")] public ulong Skin;

			[JsonProperty(PropertyName = "Is Blueprint")]
			public bool Blueprint;

			[JsonProperty(PropertyName = "Amount")]
			public int Amount;

			[JsonProperty(PropertyName = "Price")] public double Price;

			[JsonProperty(PropertyName = "Sell Price")]
			public double SellPrice;

			[JsonProperty(PropertyName = "Buy Cooldown (0 - disable)")]
			public float BuyCooldown;

			[JsonProperty(PropertyName = "Sell Cooldown (0 - disable)")]
			public float SellCooldown;

			[JsonProperty(PropertyName = "Discount (%)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, int> Discount = new Dictionary<string, int>();

			[JsonProperty(PropertyName = "Sell Limits (0 - no limit)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, int> SellLimits = new Dictionary<string, int>();

			[JsonProperty(PropertyName = "Buy Limits (0 - no limit)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, int> BuyLimits = new Dictionary<string, int>();

			[JsonProperty(PropertyName = "Daily Buy Limits (0 - no limit)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, int> DailyBuyLimits = new Dictionary<string, int>();

			[JsonProperty(PropertyName = "Daily Sell Limits (0 - no limit)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, int> DailySellLimits = new Dictionary<string, int>();

			[JsonProperty(PropertyName = "Max Buy Amount (0 - disable)")]
			public int BuyMaxAmount;

			[JsonProperty(PropertyName = "Max Sell Amount (0 - disable)")]
			public int SellMaxAmount;

			[JsonProperty(PropertyName = "Force Buy")]
			public bool ForceBuy;

			[JsonProperty(PropertyName = "Prohibit splitting item into stacks?")]
			public bool ProhibitSplit;

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

			public CuiElement GetImage(string aMin, string aMax, string oMin, string oMax, string parent,
				string name = null)
			{
				if (_image == null)
				{
					if (_instance._enabledImageLibrary && !string.IsNullOrEmpty(Image))
						_image = new CuiRawImageComponent
						{
							Png = _instance.ImageLibrary.Call<string>("GetImage", Image)
						};
					else
						_image = new CuiImageComponent
						{
							ItemId = itemId,
							SkinId = Skin
						};
				}

				return new CuiElement
				{
					Name = string.IsNullOrEmpty(name) ? CuiHelper.GetGuid() : name,
					Parent = parent,
					Components =
					{
						_image,
						new CuiRectTransformComponent
						{
							AnchorMin = aMin, AnchorMax = aMax,
							OffsetMin = oMin, OffsetMax = oMax
						}
					}
				};
			}

			private string GetName()
			{
				if (!string.IsNullOrEmpty(Title))
					return Title;

				if (!string.IsNullOrEmpty(DisplayName))
					return DisplayName;

				var def = ItemManager.FindItemDefinition(ShortName);
				if (!string.IsNullOrEmpty(ShortName) && def != null)
					return def.displayName.translated;

				return string.Empty;
			}

			[JsonIgnore] public ItemDefinition Definition;

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
					["Amount"] = Amount,
					["Price"] = Price,
					["SellPrice"] = SellPrice,
					["Plugin_Hook"] = Plugin.Hook,
					["Plugin_Name"] = Plugin.Plugin,
					["Plugin_Amount"] = Plugin.Amount
				};
			}

			public ShopItem()
			{
			}

			public ShopItem(Dictionary<string, object> dictionary)
			{
				ID = (int) dictionary["ID"];
				Type = (ItemType) dictionary["Type"];
				Image = (string) dictionary["Image"];
				Title = (string) dictionary["Title"];
				Command = (string) dictionary["Command"];
				DisplayName = (string) dictionary["DisplayName"];
				ShortName = (string) dictionary["ShortName"];
				Skin = (ulong) dictionary["Skin"];
				Blueprint = (bool) dictionary["Blueprint"];
				Amount = (int) dictionary["Amount"];
				Price = (double) dictionary["Price"];
				SellPrice = (double) dictionary["SellPrice"];
				Plugin = new PluginItem
				{
					Hook = (string) dictionary["Plugin_Hook"],
					Plugin = (string) dictionary["Plugin_Name"],
					Amount = (int) dictionary["Plugin_Amount"]
				};
				Discount = new Dictionary<string, int>();
			}

			public double GetPrice(BasePlayer player)
			{
				var discount = GetDiscount(player);

				var price = Price;

				return Math.Round(discount != 0 ? price * (1f - discount / 100f) : price,
					_config.UI.RoundDigits);
			}

			public int GetDiscount(BasePlayer player)
			{
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
					ProhibitSplit = false
				};
			}
		}

		private class PluginItem
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
						plug.Call(Hook, player.userID, (double) Amount * count);
						break;
					}
					default:
					{
						plug.Call(Hook, player.userID, Amount * count);
						break;
					}
				}
			}
		}

		private class AdditionalEconomy : EconomyConf
		{
			[JsonProperty(PropertyName = "ID")] public int ID;

			[JsonProperty(PropertyName = "Enabled")]
			public bool Enabled;

			public bool IsSame(EconomyConf configEconomy)
			{
				return Type == configEconomy.Type &&
				       Plug == configEconomy.Plug &&
				       ShortName == configEconomy.ShortName &&
				       Skin == configEconomy.Skin;
			}

			public AdditionalEconomy(EconomyConf configEconomy)
			{
				Type = configEconomy.Type;
				Plug = configEconomy.Plug;
				AddHook = configEconomy.AddHook;
				RemoveHook = configEconomy.RemoveHook;
				BalanceHook = configEconomy.BalanceHook;
				ShortName = configEconomy.ShortName;
				DisplayName = configEconomy.DisplayName;
				Skin = configEconomy.Skin;
				TitleLangKey = configEconomy.TitleLangKey;
				BalanceLangKey = configEconomy.BalanceLangKey;
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

		private class EconomyConf
		{
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

			[JsonProperty(PropertyName = "Lang Key (for Title)")]
			public string TitleLangKey;

			[JsonProperty(PropertyName = "Lang Key (for Balance)")]
			public string BalanceLangKey;

			public string GetTitle(BasePlayer player)
			{
				return _instance.Msg(player, TitleLangKey);
			}

			public string GetBalanceTitle(BasePlayer player)
			{
				return _instance.Msg(player, BalanceLangKey, ShowBalance(player));
			}

			public double ShowBalance(BasePlayer player)
			{
				switch (Type)
				{
					case EconomyType.Plugin:
					{
						var plugin = _instance?.plugins?.Find(Plug);
						if (plugin == null) return 0;

						return Math.Round(Convert.ToDouble(plugin.Call(BalanceHook, player.userID)), 2);
					}
					case EconomyType.Item:
					{
						return ItemCount(Enumerable.ToArray(Enumerable.Concat(player.inventory.containerMain?.itemList ?? Enumerable.Empty<Item>(), Enumerable.Concat(player.inventory.containerBelt?.itemList ?? Enumerable.Empty<Item>(), player.inventory.containerWear?.itemList ?? Enumerable.Empty<Item>()))), ShortName, Skin);
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
								plugin.Call(AddHook, player.userID, (int) amount);
								break;
							default:
								plugin.Call(AddHook, player.userID, amount);
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
								plugin.Call(RemoveHook, player.userID, (int) amount);
								break;
							default:
								plugin.Call(RemoveHook, player.userID, amount);
								break;
						}

						return true;
					}
					case EconomyType.Item:
					{
						var playerItems = Enumerable.Concat(player.inventory.containerMain?.itemList ?? Enumerable.Empty<Item>(), Enumerable.Concat(player.inventory.containerBelt?.itemList ?? Enumerable.Empty<Item>(), player.inventory.containerWear?.itemList ?? Enumerable.Empty<Item>()));
						var am = (int) amount;

						if (ItemCount(Enumerable.ToArray(playerItems), ShortName, Skin) < am) return false;

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
			catch
			{
				PrintError("Your configuration file contains an error. Using default configuration values.");
				LoadDefaultConfig();
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

			var baseConfig = new Configuration();

			if (_config.Version == default(VersionNumber) && _config.Version < new VersionNumber(1, 0, 21))
			{
				_data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
				if (_data != null)
					Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Players", _data);
			}

			if (_config.Version < new VersionNumber(1, 0, 21))
				_config.Shop.ForEach(shop =>
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

			_config.Version = Version;
			PrintWarning("Config update completed!");
		}

		#endregion

		#region Data

		private PluginData _data;

		private void SaveData()
		{
			SavePlayers();

			SaveCooldown();

			SaveLimits();

			SaveSelectEconomy();
		}

		private void SavePlayers()
		{
			_data.PlayerCarts.Clear();

			foreach (var check in _carts) _data.PlayerCarts.Add(check.Key, check.Value.ToPlayerCart());

			foreach (var cartData in _cartsNPC)
			{
				PlayerCart cart;
				if (!_data.PlayerCarts.TryGetValue(cartData.Key, out cart))
					_data.PlayerCarts.Add(cartData.Key, cart = new PlayerCart());

				foreach (var npcData in cartData.Value)
					cart.NpcCarts[npcData.Key] = new NPCCart
					{
						Items = npcData.Value.GetPlayerCartItems()
					};
			}

			Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Players", _data);
		}

		private void SaveCooldown()
		{
			Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Cooldown", _cooldown);
		}

		private void SaveLimits()
		{
			Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Limits", _limits);
		}

		private void SaveSelectEconomy()
		{
			Interface.Oxide.DataFileSystem.WriteObject($"{Name}/EconomyChoice", _economyChoice);
		}

		private void LoadData()
		{
			try
			{
				_data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>($"{Name}/Players");
				_cooldown =
					Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, CooldownInfo>>($"{Name}/Cooldown");
				_limits = Interface.Oxide.DataFileSystem.ReadObject<PlayerLimits>($"{Name}/Limits");
				_economyChoice = Interface.Oxide.DataFileSystem.ReadObject<EconomyChoice>($"{Name}/EconomyChoice");
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			if (_data == null) _data = new PluginData();
			if (_cooldown == null) _cooldown = new Dictionary<ulong, CooldownInfo>();
			if (_limits == null) _limits = new PlayerLimits();
			if (_economyChoice == null) _economyChoice = new EconomyChoice();
		}

		private class PluginData
		{
			[JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<ulong, PlayerCart> PlayerCarts = new Dictionary<ulong, PlayerCart>();
		}

		private class PlayerCart
		{
			[JsonProperty(PropertyName = "Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<int, int> Items = new Dictionary<int, int>();

			[JsonProperty(PropertyName = "NPC Carts", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, NPCCart> NpcCarts = new Dictionary<string, NPCCart>();
		}

		private class NPCCart
		{
			[JsonProperty(PropertyName = "Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<int, int> Items = new Dictionary<int, int>();
		}

		private class DataCart
		{
			public readonly Dictionary<ShopItem, int> Items = new Dictionary<ShopItem, int>();

			public void AddCartItem(ShopItem item, BasePlayer player)
			{
				int result;
				int amount;
				if (Items.TryGetValue(item, out amount))
				{
					if (item.BuyMaxAmount > 0 && amount >= item.BuyMaxAmount) return;

					if (!CanCartAdd(player, item, amount + 1, out result))
					{
						_instance.SendNotify(player, result == 1 ? BuyLimitReached : DailyBuyLimitReached, 1,
							item.PublicTitle);
						return;
					}

					Items[item]++;
				}
				else
				{
					if (!CanCartAdd(player, item, 1, out result))
					{
						_instance.SendNotify(player, result == 1 ? BuyLimitReached : DailyBuyLimitReached, 1,
							item.PublicTitle);
						return;
					}

					Items.Add(item, 1);
				}
			}

			private bool CanCartAdd(BasePlayer player, ShopItem item, int amount, out int result)
			{
				int leftLimit;
				if (HasLimit(player, item, true, out leftLimit) && amount >= leftLimit) //total Limit
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

			public void RemoveCartItem(ShopItem item)
			{
				Items.Remove(item);
			}

			public void ChangeAmountItem(BasePlayer player, ShopItem item, int amount)
			{
				if (amount > 0)
				{
					int totalLimit;
					if (HasLimit(player, item, true, out totalLimit) && amount >= totalLimit)
						amount = Math.Min(totalLimit, amount);

					int dailyLimit;
					if (HasLimit(player, item, true, out dailyLimit, true) && amount >= dailyLimit)
						amount = Math.Min(dailyLimit, amount);

					if (amount <= 0) return;

					Items[item] = amount;
				}
				else
				{
					Items.Remove(item);
				}
			}

			public int GetAmount()
			{
				return Items.Sum(x => x.Key.Amount * x.Value);
			}

			public double GetPrice(BasePlayer player)
			{
				return Items.Sum(x => x.Key.GetPrice(player) * x.Value);
			}

			public int GetTotalAmount()
			{
				return
					Items.Sum(check => check.Key.Type == ItemType.Item && check.Key.Definition != null
						? check.Key.ProhibitSplit
							? 1
							: check.Key.GetStacks(check.Value).Count
						: 0);
			}

			public void ClearItems()
			{
				Items.Clear();
			}

			#region Data

			public PlayerCart ToPlayerCart()
			{
				return new PlayerCart
				{
					Items = Items.ToDictionary(x => x.Key.ID, y => y.Value)
				};
			}

			public Dictionary<int, int> GetPlayerCartItems()
			{
				return Items.ToDictionary(x => x.Key.ID, y => y.Value);
			}

			public DataCart()
			{
			}

			public DataCart(PlayerCart cart)
			{
				Items = cart.Items.ToDictionary(x => _instance.FindItemById(x.Key), y => y.Value);
			}

			public DataCart(Dictionary<int, int> items)
			{
				Items = items.ToDictionary(x => _instance.FindItemById(x.Key), y => y.Value);
			}

			#endregion
		}

		#endregion

		#region Hooks

		private void Init()
		{
			_instance = this;

			LoadData();

			LoadColors();

			RegisterPermissions();

			CheckOnDuplicates();

			LoadEconomics();
		}

		private void OnServerInitialized()
		{
			FillCategories();

			LoadItems();

			LoadNPCs();

			LoadImages();

			ItemsToDict();

			CacheImages();

			LoadCarts();

			LoadPlayers();

			LoadCustomVMs();

			RegisterCommands();

			CheckUpdateController();

			LoadCacheUI();
		}

		private void Unload()
		{
			foreach (var player in BasePlayer.activePlayerList)
			{
				CuiHelper.DestroyUi(player, Layer);
				CuiHelper.DestroyUi(player, ModalLayer);
				CuiHelper.DestroyUi(player, EditingLayer);

				OnPlayerDisconnected(player);
			}

			SaveData();

			_config = null;
			_instance = null;
		}

		private void OnPlayerConnected(BasePlayer player)
		{
			if (player == null) return;

			GetAvatar(player.userID,
				avatar => ImageLibrary?.Call("AddImage", avatar, $"avatar_{player.UserIDString}"));

			if (_config.LoginImages)
				_coroutines[player] = ServerMgr.Instance.StartCoroutine(LoadImages(player));
		}

		private void OnPlayerDisconnected(BasePlayer player)
		{
			if (player == null) return;

			_itemsToUpdate.Remove(player);
			_openedShops.Remove(player);
			_openSHOP.Remove(player);

			CheckUpdateController();

			_itemEditing.Remove(player);

			Coroutine coroutine;
			if (_coroutines.TryGetValue(player, out coroutine) && coroutine != null)
				ServerMgr.Instance.StopCoroutine(coroutine);
		}

		private void OnUseNPC(BasePlayer npc, BasePlayer player)
		{
			if (npc == null || player == null) return;

			NPCShop npcShop;
			if (!_config.NPCs.TryGetValue(npc.UserIDString, out npcShop) || npcShop == null) return;

			_openedShops[player] = npcShop;

			MainUi(player, first: true);
		}

		private void OnNewSave()
		{
			if (_config.Wipe.Players)
				_data.PlayerCarts.Clear();

			if (_config.Wipe.Cooldown)
				_cooldown.Clear();

			if (_config.Wipe.Limits)
				_limits.Players.Clear();

			SaveData();
		}

		#region Image Library

		private void OnPluginLoaded(Plugin plugin)
		{
			if (plugin.Name == "ImageLibrary") _enabledImageLibrary = true;
		}

		private void OnPluginUnloaded(Plugin plugin)
		{
			if (plugin.Name == "ImageLibrary") _enabledImageLibrary = false;
		}

		#endregion

		#region Vending Machine

		private object CanLootEntity(BasePlayer player, VendingMachine vendingMachine)
		{
			if (player == null || vendingMachine == null)
				return null;

			CustomVendingConf customVendingConf;
			if (_config.CustomVending.TryGetValue(vendingMachine.net.ID.Value, out customVendingConf))
			{
				_openedCustomVending[player] = customVendingConf;

				MainUi(player, first: true);
				return false;
			}

			return null;
		}

		#endregion

		#endregion

		#region Commands

		[ConsoleCommand("openshopUI")]
		private void OpenShopUI(ConsoleSystem.Arg arg)
		{
			var player = arg?.Player();
			if (player == null) return;

			if (_openedUI.Contains(player.userID))
			{
				CuiHelper.DestroyUi(player, Layer);
				_openedUI.Remove(player.userID);
			}
			else
			{
				MainUi(player, first: true);
				_openedUI.Add(player.userID);
			}
		}

		private void CmdShopOpen(IPlayer cov, string command, string[] args)
		{
			var player = cov?.Object as BasePlayer;
			if (player == null) return;

			if (_config.UseDuels && InDuel(player))
			{
				SendNotify(player, NoUseDuel, 1);
				return;
			}

			MainUi(player, first: true);
		}

		private void CmdSetCustomVM(IPlayer cov, string command, string[] args)
		{
			var player = cov?.Object as BasePlayer;
			if (player == null) return;

			if (!player.HasPermission(PermSetVM))
			{
				SendNotify(player, NoPermission, 1);
				return;
			}

			if (args.Length == 0)
			{
				SendNotify(player, ErrorSyntax, 1, $"{command} [categories: cat1 cat2 ...]");
				return;
			}

			var categories = args.ToList();
			categories.RemoveAll(cat => !_config.Shop.Exists(confCat => confCat.Title == cat));
			if (categories.Count == 0)
			{
				SendNotify(player, VMNotFoundCategories, 1);
				return;
			}

			var workbench = GetLookVM(player);
			if (workbench == null)
			{
				SendNotify(player, VMNotFound, 1);
				return;
			}

			if (_config.CustomVending.ContainsKey(workbench.net.ID.Value))
			{
				SendNotify(player, VMExists, 1);
				return;
			}

			var conf = new CustomVendingConf
			{
				Categories = categories
			};

			_config.CustomVending[workbench.net.ID.Value] = conf;

			SaveConfig();

			SendNotify(player, VMInstalled, 0);

			Subscribe(nameof(CanLootEntity));
		}

		private void CmdSetShopNPC(IPlayer cov, string command, string[] args)
		{
			var player = cov?.Object as BasePlayer;
			if (player == null) return;

			if (!player.HasPermission(PermSetNPC))
			{
				SendNotify(player, NoPermission, 1);
				return;
			}

			if (args.Length == 0)
			{
				SendNotify(player, ErrorSyntax, 1, $"{command} [categories: cat1 cat2 ...]");
				return;
			}

			var categories = args.ToList();
			categories.RemoveAll(cat => !_config.Shop.Exists(confCat => confCat.Title == cat));
			if (categories.Count == 0)
			{
				SendNotify(player, VMNotFoundCategories, 1);
				return;
			}

			var npc = GetLookNPC(player);
			if (npc == null)
			{
				SendNotify(player, NPCNotFound, 1);
				return;
			}

			if (_config.NPCs.ContainsKey(npc.UserIDString))
			{
				SendNotify(player, VMExists, 1);
				return;
			}

			var conf = new NPCShop
			{
				Shops = categories,
				BotID = npc.UserIDString
			};

			_config.NPCs[npc.UserIDString] = conf;

			SaveConfig();

			SendNotify(player, NPCInstalled, 0);
		}

		[ConsoleCommand("UI_Shop")]
		private void CmdConsoleShop(ConsoleSystem.Arg arg)
		{
			var player = arg?.Player();
			if (player == null || !arg.HasArgs()) return;

			switch (arg.Args[0])
			{
				case "closeui":
				{
					_itemsToUpdate.Remove(player);
					_openedShops.Remove(player);
					_openSHOP.Remove(player);
					CheckUpdateController();
					break;
				}

				case "search_page":
				{
					int catPage, page, searchPage;
					if (!arg.HasArgs(4) || !int.TryParse(arg.Args[1], out catPage) ||
					    !int.TryParse(arg.Args[2], out page) ||
					    !int.TryParse(arg.Args[3], out searchPage)) return;

					var search = string.Empty;
					if (arg.HasArgs(5)) search = string.Join(" ", arg.Args.Skip(4));

					MainUi(player, catPage, page, search, searchPage);
					break;
				}

				case "main_page":
				{
					int catPage, page;
					if (!arg.HasArgs(3) || !int.TryParse(arg.Args[1], out catPage) ||
					    !int.TryParse(arg.Args[2], out page)) return;

					var search = string.Empty;
					if (arg.HasArgs(4)) search = string.Join(" ", arg.Args.Skip(3));

					if (string.IsNullOrEmpty(search) && catPage == -1)
						catPage = 0;

					MainUi(player, catPage, page, search);
					break;
				}

				case "buyitem":
				{
					int id;
					if (!arg.HasArgs(2) ||
					    !int.TryParse(arg.Args[1], out id)) return;

					var shopItem = FindItemById(id);
					if (shopItem == null) return;

					var playerCart = GetPlayerCart(player);
					if (playerCart == null) return;

					playerCart.AddCartItem(shopItem, player);

					var cooldownTime = GetCooldownTime(player.userID, shopItem, true);
					if (cooldownTime > 0)
					{
						SendNotify(player, BuyCooldownMessage, 1, shopItem.PublicTitle,
							FormatShortTime(player, TimeSpan.FromSeconds(cooldownTime)));
						return;
					}

					var container = new CuiElementContainer();
					RefreshCart(ref container, player, playerCart);
					CuiHelper.AddUi(player, container);
					break;
				}

				case "changeicat":
				{
					int catPage, iCategory;
					if (!arg.HasArgs(3) || !int.TryParse(arg.Args[1], out catPage) ||
					    !int.TryParse(arg.Args[2], out iCategory)) return;

					var container = new CuiElementContainer();
					RefreshCategories(ref container, player, GetCategories(player, GetShopByPlayer(player)), catPage,
						iCategory);
					CuiHelper.AddUi(player, container);
					break;
				}

				case "gocat":
				{
					int catPage, iCategory;
					if (!arg.HasArgs(3) || !int.TryParse(arg.Args[1], out catPage) ||
					    !int.TryParse(arg.Args[2], out iCategory)) return;

					MainUi(player, catPage, categories: true, iCategory: iCategory);
					break;
				}

				case "cart_page":
				{
					int page;
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out page)) return;

					var playerCart = GetPlayerCart(player);
					if (playerCart == null) return;

					var container = new CuiElementContainer();
					RefreshCart(ref container, player, playerCart, page);
					CuiHelper.AddUi(player, container);
					break;
				}

				case "cart_item_remove":
				{
					int id;
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out id)) return;

					var shopItem = FindItemById(id);
					if (shopItem == null) return;

					var playerCart = GetPlayerCart(player);
					if (playerCart == null) return;

					playerCart.RemoveCartItem(shopItem);

					var container = new CuiElementContainer();
					RefreshCart(ref container, player, playerCart);
					CuiHelper.AddUi(player, container);
					break;
				}

				case "cart_item_change":
				{
					int id, amount;
					if (!arg.HasArgs(3) ||
					    !int.TryParse(arg.Args[1], out id) ||
					    !int.TryParse(arg.Args[2], out amount)) return;

					var shopItem = FindItemById(id);
					if (shopItem == null) return;

					var playerCart = GetPlayerCart(player);
					if (playerCart == null) return;

					if (shopItem.BuyMaxAmount > 0 && amount > shopItem.BuyMaxAmount)
						amount = shopItem.BuyMaxAmount;

					playerCart.ChangeAmountItem(player, shopItem, amount);

					var container = new CuiElementContainer();
					RefreshCart(ref container, player, playerCart);
					CuiHelper.AddUi(player, container);
					break;
				}

				case "cart_try_buyitems":
				{
					AcceptBuy(player);
					break;
				}

				case "cart_buyitems":
				{
					var playerCart = GetPlayerCart(player);
					if (playerCart == null) return;

					var price = playerCart.GetPrice(player);
					if (price <= 0) return;

					if (_config.BlockNoEscape && NoEscape_IsBlocked(player))
					{
						ErrorUi(player, Msg(player, BuyRaidBlocked));
						return;
					}

					if (_config.WipeCooldown)
					{
						var seconds = SecondsFromWipe();
						if (seconds < _config.WipeCooldownTimer)
						{
							ErrorUi(player,
								Msg(player, BuyWipeCooldown,
									FormatShortTime(seconds)));
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

					var totalAmount = playerCart.GetTotalAmount();

					var slots = player.inventory.containerBelt.capacity -
					            player.inventory.containerBelt.itemList.Count +
					            (player.inventory.containerMain.capacity -
					             player.inventory.containerMain.itemList.Count);
					if (slots < totalAmount)
					{
						ErrorUi(player, Msg(player, NotEnoughSpace));
						return;
					}

					if (playerCart.Items.Any(x =>
					    {
						    var limit = GetLimit(player, x.Key, true);
						    if (limit <= 0)
						    {
							    ErrorUi(player, Msg(player, BuyLimitReached, x.Key.PublicTitle));
							    return true;
						    }

						    limit = GetLimit(player, x.Key, true, true);
						    if (limit <= 0)
						    {
							    ErrorUi(player, Msg(player, DailyBuyLimitReached, x.Key.PublicTitle));
							    return true;
						    }

						    return false;
					    }))
						return;

					if (!player.HasPermission(PermFreeBypass) &&
					    !EconomyChoice.GetEconomy(player).RemoveBalance(player, price))
					{
						ErrorUi(player, Msg(player, NotMoney));
						return;
					}

					ServerMgr.Instance.StartCoroutine(GiveCartItems(player, playerCart.Items.ToList(), price));

					playerCart.ClearItems();

					CuiHelper.DestroyUi(player, Layer);
					_carts.Remove(player.userID);

					_itemsToUpdate.Remove(player);
					_openedShops.Remove(player);
					_openSHOP.Remove(player);

					CheckUpdateController();

					SendNotify(player, ReceivedItems, 0);
					break;
				}

				case "fastbuyitem":
				{
					int itemId, amount;
					if (!arg.HasArgs(3) ||
					    !int.TryParse(arg.Args[1], out itemId) ||
					    !int.TryParse(arg.Args[2], out amount)) return;

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
						var seconds = SecondsFromWipe();
						if (seconds < _config.WipeCooldownTimer)
						{
							ErrorUi(player,
								Msg(player, BuyWipeCooldown,
									FormatShortTime(seconds)));
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

					switch (item.Type)
					{
						case ItemType.Item:
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

							break;
						}
					}

					var limit = GetLimit(player, item, true);
					if (limit <= 0)
					{
						ErrorUi(player, Msg(player, BuyLimitReached, item.PublicTitle));
						return;
					}

					var price = item.GetPrice(player) * amount;
					if (!player.HasPermission(PermFreeBypass) &&
					    !EconomyChoice.GetEconomy(player).RemoveBalance(player, price))
					{
						ErrorUi(player, Msg(player, NotMoney));
						return;
					}

					var logItems = Pool.GetList<string>();

					logItems.Add(item.ToString());

					item.Get(player, amount);

					SetCooldown(player, item, true);
					UseLimit(player, item, true, amount);
					UseLimit(player, item, true, amount, true);

					Log("Buy", LogBuyItems, player.displayName, player.UserIDString,
						price, string.Join(", ", logItems));

					CuiHelper.DestroyUi(player, Layer);
					_carts.Remove(player.userID);

					_itemsToUpdate.Remove(player);
					_openedShops.Remove(player);
					_openSHOP.Remove(player);

					CheckUpdateController();

					SendNotify(player, ReceivedItems, 0);
					break;
				}

				case "tryoperateitem":
				{
					int itemId;
					bool buy;
					if (!arg.HasArgs(3) || !int.TryParse(arg.Args[1], out itemId) ||
					    !bool.TryParse(arg.Args[2], out buy)) return;

					var item = FindItemById(itemId);
					if (item == null) return;

					var amount = 1;
					if (arg.HasArgs(4))
					{
						if (arg.Args[3] == "all")
							amount = buy
								// buy
								? Mathf.FloorToInt((float) (EconomyChoice.GetEconomy(player).ShowBalance(player) /
								                            item.GetPrice(player)))
								// sell
								: Mathf.FloorToInt(ItemCount(PlayerItems(player), item.ShortName, item.Skin) /
								                   (float) item.Amount);
						else
							int.TryParse(arg.Args[3], out amount);

						if (amount < 1)
							return;

						if (buy == false) //sell
							amount = Mathf.Max(1,
								Mathf.Min(amount,
									Mathf.CeilToInt(ItemCount(PlayerItems(player), item.ShortName, item.Skin) /
									                (float) item.Amount)));
					}

					if (buy)
					{
						if (item.BuyMaxAmount > 0)
							amount = Mathf.Min(amount, item.BuyMaxAmount);
					}
					else
					{
						if (item.SellMaxAmount > 0)
							amount = Mathf.Min(amount, item.SellMaxAmount);
					}

					SecondModalUi(player, item, buy, amount);
					break;
				}

				case "sellitem":
				{
					int itemId, amount;
					if (!arg.HasArgs(3) ||
					    !int.TryParse(arg.Args[1], out itemId) ||
					    !int.TryParse(arg.Args[2], out amount)) return;

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
						var success = NoEscape?.Call("IsBlocked", player);
						if (success is bool && (bool) success)
						{
							ErrorUi(player, Msg(player, SellRaidBlocked));
							return;
						}
					}

					if (_config.WipeCooldown)
					{
						var seconds = SecondsFromWipe();
						if (seconds < _config.WipeCooldownTimer)
						{
							ErrorUi(player,
								Msg(player, SellWipeCooldown,
									FormatShortTime(seconds)));

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
						ErrorUi(player, Msg(player, SellLimitReached, item.PublicTitle));
						return;
					}

					limit = GetLimit(player, item, false, true);
					if (limit <= 0)
					{
						ErrorUi(player, Msg(player, DailySellLimitReached, item.PublicTitle));
						return;
					}

					List<ulong> blockedSkins;
					if (_config.BlockedSkins.TryGetValue(item.ShortName, out blockedSkins))
						if (blockedSkins.Contains(item.Skin))
						{
							ErrorUi(player, Msg(player, SkinBlocked));
							return;
						}

					var totalAmount = item.Amount * amount;
					var playerItems = PlayerItems(player);

					if (ItemCount(playerItems, item.ShortName, item.Skin) < totalAmount)
					{
						ErrorUi(player, Msg(player, NotEnough));
						return;
					}

					Log("Sell", LogSellItem, player.displayName, player.UserIDString,
						item.SellPrice * amount, item.ToString());

					Take(playerItems, item.ShortName, item.Skin, totalAmount);

					EconomyChoice.GetEconomy(player).AddBalance(player, item.SellPrice * amount);

					SetCooldown(player, item, false, true);
					UseLimit(player, item, false, amount);
					UseLimit(player, item, false, amount, true);

					if (_itemsToUpdate.ContainsKey(player))
					{
						if (!_itemsToUpdate[player].Contains(item))
							_itemsToUpdate[player].Add(item);
					}
					else
					{
						_itemsToUpdate.Add(player, new List<ShopItem> {item});
					}

					CheckUpdateController();

					var container = new CuiElementContainer();
					SellButtonUi(player, ref container, item);
					BalanceUi(ref container, player);
					CuiHelper.AddUi(player, container);

					SendNotify(player, SellNotify, 0, totalAmount, item.PublicTitle);
					break;
				}

				case "startedititem":
				{
					int id, category, page;
					if (!IsAdmin(player) || !arg.HasArgs(4) ||
					    !int.TryParse(arg.Args[1], out category) ||
					    !int.TryParse(arg.Args[2], out page) ||
					    !int.TryParse(arg.Args[3], out id)) return;

					_itemEditing.Remove(player);

					EditUi(player, category, page, id, true);
					break;
				}

				case "edititem":
				{
					int category, page;
					if (!IsAdmin(player) ||
					    !arg.HasArgs(5) ||
					    !int.TryParse(arg.Args[1], out category) ||
					    !int.TryParse(arg.Args[2], out page)) return;

					var key = arg.Args[3];
					var value = arg.Args[4];

					if (_itemEditing.ContainsKey(player) && _itemEditing[player].ContainsKey(key))
					{
						object newValue = null;

						switch (key)
						{
							case "Type":
							{
								ItemType type;
								if (Enum.TryParse(value, out type))
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
								newValue = string.Join(" ", arg.Args.Skip(4));
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
								int Value;

								if (int.TryParse(value, out Value))
									newValue = Value;
								break;
							}

							case "SellPrice":
							case "Price":
							{
								double Value;

								if (double.TryParse(value, out Value))
									newValue = Value;
								break;
							}

							case "Skin":
							{
								ulong Value;
								if (ulong.TryParse(value, out Value))
									newValue = Value;
								break;
							}

							case "Blueprint":
							{
								bool Value;
								if (bool.TryParse(value, out Value))
									newValue = Value;
								break;
							}
						}

						if (_itemEditing[player][key].Equals(newValue))
							return;

						_itemEditing[player][key] = newValue;
					}

					EditUi(player, category, page);
					break;
				}

				case "closeediting":
				{
					_itemEditing.Remove(player);
					break;
				}

				case "saveitem":
				{
					int category, page;
					if (!IsAdmin(player) || !arg.HasArgs(3) || !int.TryParse(arg.Args[1], out category) ||
					    !int.TryParse(arg.Args[2], out page)) return;

					var edit = _itemEditing[player];
					if (edit == null) return;

					var npcShop = GetShopByPlayer(player);

					var newItem = new ShopItem(edit);

					var generated = (bool) edit["Generated"];
					if (generated)
					{
						var shopCategory = GetCategories(player, npcShop)[category];
						shopCategory.Items.Add(newItem);
						shopCategory.SortItems();
					}
					else
					{
						var shopItem = FindItemById((int) edit["ID"]);
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
							shopItem.Amount = newItem.Amount;
							shopItem.Price = newItem.Price;
							shopItem.SellPrice = newItem.SellPrice;
						}
					}

					if (_enabledImageLibrary && !string.IsNullOrEmpty(newItem.Image))
						ImageLibrary.Call("AddImage", newItem.Image, newItem.Image);

					_itemEditing.Remove(player);

					SaveConfig();

					ItemsToDict();

					if (category == -1)
						category = 0;

					MainUi(player, category, page, string.Empty, 0, true);
					break;
				}

				case "removeitem":
				{
					int category;
					if (!IsAdmin(player) || !arg.HasArgs(2) || !int.TryParse(arg.Args[1], out category)) return;

					var editing = _itemEditing[player];
					if (editing == null) return;

					var shopItem = FindItemById((int) editing["ID"]);
					if (shopItem == null) return;

					_config.Shop.ForEach(shopCategory =>
					{
						if (shopCategory.Items.Remove(shopItem))
							shopCategory.SortItems();
					});

					_itemEditing.Remove(player);

					SaveConfig();

					if (category == -1)
						category = 0;

					MainUi(player, category, first: true);
					break;
				}

				case "selectitem":
				{
					int category;
					if (!IsAdmin(player) || !arg.HasArgs(2) || !int.TryParse(arg.Args[1], out category)) return;

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
					int category, page;
					if (!IsAdmin(player) || !arg.HasArgs(4) || !int.TryParse(arg.Args[1], out category) ||
					    !int.TryParse(arg.Args[2], out page)) return;

					_itemEditing[player]["ShortName"] = arg.Args[3];

					EditUi(player, category, page);
					break;
				}

				case "item_info":
				{
					int itemId, catPage, shopPage;
					bool status;
					if (!arg.HasArgs(5) ||
					    !int.TryParse(arg.Args[1], out itemId) ||
					    !int.TryParse(arg.Args[2], out catPage) ||
					    !int.TryParse(arg.Args[3], out shopPage) ||
					    !bool.TryParse(arg.Args[4], out status)) return;

					var item = FindItemById(itemId);
					if (item == null) return;

					var container = new CuiElementContainer();
					ItemUi(player, item, ref container, catPage, shopPage, !status);
					CuiHelper.DestroyUi(player, Layer + $".Item.{item.ID}");
					CuiHelper.AddUi(player, container);
					break;
				}

				case "pselect":
				{
					int catPage, shopPage, searchPage;
					if (!arg.HasArgs(4) ||
					    !int.TryParse(arg.Args[1], out catPage) ||
					    !int.TryParse(arg.Args[2], out shopPage) ||
					    !int.TryParse(arg.Args[3], out searchPage)) return;

					var search = string.Empty;
					if (arg.HasArgs(5))
						search = string.Join(" ", arg.Args.Skip(4));

					SelectPlayerUi(player, catPage, shopPage, searchPage, search);
					break;
				}

				case "pselect_page":
				{
					int catPage, shopPage, searchPage, selectPage;
					if (!arg.HasArgs(5) ||
					    !int.TryParse(arg.Args[1], out catPage) ||
					    !int.TryParse(arg.Args[2], out shopPage) ||
					    !int.TryParse(arg.Args[3], out searchPage) ||
					    !int.TryParse(arg.Args[4], out selectPage)) return;

					var search = string.Empty;
					if (arg.HasArgs(6))
						search = string.Join(" ", arg.Args.Skip(5));

					SelectPlayerUi(player, catPage, shopPage, searchPage, search, selectPage);
					break;
				}

				case "try_ptransfer":
				{
					int catPage, shopPage, searchPage;
					ulong targetId;
					if (!arg.HasArgs(5) ||
					    !int.TryParse(arg.Args[1], out catPage) ||
					    !int.TryParse(arg.Args[2], out shopPage) ||
					    !int.TryParse(arg.Args[3], out searchPage) ||
					    !ulong.TryParse(arg.Args[4], out targetId)) return;

					var search = string.Empty;
					if (arg.HasArgs(6))
						search = string.Join(" ", arg.Args.Skip(5));

					TransferUi(player, targetId, catPage, shopPage, searchPage, search);
					break;
				}

				case "ptransfer_amount":
				{
					int catPage, shopPage, searchPage;
					ulong targetId;
					bool hasSearch;
					float amount;
					if (!arg.HasArgs(6) ||
					    !int.TryParse(arg.Args[1], out catPage) ||
					    !int.TryParse(arg.Args[2], out shopPage) ||
					    !int.TryParse(arg.Args[3], out searchPage) ||
					    !ulong.TryParse(arg.Args[4], out targetId) ||
					    !bool.TryParse(arg.Args[5], out hasSearch))
						return;

					var search = string.Empty;
					if (hasSearch)
					{
						if (!arg.HasArgs(8) || !float.TryParse(arg.Args[7], out amount)) return;

						search = arg.Args[6];
					}
					else if (!arg.HasArgs(7) || !float.TryParse(arg.Args[6], out amount))
					{
						return;
					}

					amount = Mathf.Max(amount, 0);

					TransferUi(player, targetId, catPage, shopPage, searchPage, search, amount);
					break;
				}

				case "ptransfer_send":
				{
					int catPage, shopPage, searchPage;
					ulong targetId;
					bool hasSearch;
					float amount;
					if (!arg.HasArgs(6) ||
					    !int.TryParse(arg.Args[1], out catPage) ||
					    !int.TryParse(arg.Args[2], out shopPage) ||
					    !int.TryParse(arg.Args[3], out searchPage) ||
					    !ulong.TryParse(arg.Args[4], out targetId) ||
					    !bool.TryParse(arg.Args[5], out hasSearch))
						return;

					var search = string.Empty;
					if (hasSearch)
					{
						if (!arg.HasArgs(8) || !float.TryParse(arg.Args[7], out amount)) return;

						search = arg.Args[6];
					}
					else if (!arg.HasArgs(7) || !float.TryParse(arg.Args[6], out amount))
					{
						return;
					}

					if (amount > 0)
					{
						var targetPlayer = BasePlayer.FindAwakeOrSleeping(targetId.ToString());
						if (targetPlayer == null)
						{
							ErrorUi(player, Msg(player, PlayerNotFound));
							return;
						}

						if (!EconomyChoice.GetEconomy(player).Transfer(player, targetPlayer, amount))
						{
							ErrorUi(player, Msg(player, NotMoney));
							return;
						}

						SendNotify(player, SuccessfulTransfer, 0, amount, targetPlayer.displayName);
					}

					MainUi(player, catPage, shopPage, search?.Replace("-", " "), searchPage,
						true);
					break;
				}

				case "goback":
				{
					int catPage, shopPage, searchPage;
					if (!arg.HasArgs(4) ||
					    !int.TryParse(arg.Args[1], out catPage) ||
					    !int.TryParse(arg.Args[2], out shopPage) ||
					    !int.TryParse(arg.Args[3], out searchPage))
						return;

					var search = string.Empty;
					if (arg.HasArgs(5))
						search = string.Join(" ", arg.Args.Skip(4));

					MainUi(player, catPage, shopPage, search, searchPage, true);
					break;
				}

				case "economychange":
				{
					bool select;
					if (!arg.HasArgs(2) || !bool.TryParse(arg.Args[1], out select))
						return;

					var container = new CuiElementContainer();
					BalanceUi(ref container, player, select);
					CuiHelper.AddUi(player, container);
					break;
				}

				case "economy_set":
				{
					int id;
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out id))
						return;

					EconomyChoice.SelectEconomy(player, id);

					var container = new CuiElementContainer();
					BalanceUi(ref container, player, !_config.UI.SelectCurrency.CloseAfterChange);
					CuiHelper.AddUi(player, container);
					break;
				}
			}
		}

		[ConsoleCommand("shop.refill")]
		private void CmdConsoleRefill(ConsoleSystem.Arg arg)
		{
			if (!arg.IsAdmin) return;

			FillCategories();

			ItemsToDict();
		}

		[ConsoleCommand("shop.wipe")]
		private void CmdConsoleWipe(ConsoleSystem.Arg arg)
		{
			if (!arg.IsAdmin) return;

			if (_config.Wipe.Players)
				_data.PlayerCarts.Clear();

			if (_config.Wipe.Cooldown)
				_cooldown.Clear();

			if (_config.Wipe.Limits)
				_limits.Players.Clear();

			PrintWarning($"{Name} wiped!");

			SaveData();
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
					_config.Shop.ForEach(shopCategory => shopCategory.Items.Clear());

					SendReply(arg, "All items from categories have been removed!");

					SaveConfig();
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
						_config.Shop.Clear();

						var testCategory = new ShopCategory
						{
							Enabled = true,
							Title = "Test",
							Permission = string.Empty,
							SortType = Configuration.SortType.None,
							Items = new List<ShopItem>
							{
								ShopItem.GetDefault(0, 100, "stones"),
								ShopItem.GetDefault(0, 100, "wood")
							}
						};

						_config.Shop.Add(testCategory);

						SendReply(arg,
							"All categories were removed and one \"Test\" category was added with a couple of test items");

						SaveConfig();
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

						_config.Shop.Remove(category);

						SendReply(arg, $"Category \"{catName}\" successfully deleted!");

						SaveConfig();
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

					_config.Shop.ForEach(shopCategory =>
					{
						if (shopCategory.Items.Remove(item))
							shopCategory.SortItems();
					});

					SendReply(arg, $"Item \"{itemId}\" successfully deleted!");

					SaveConfig();
					break;
				}
			}
		}

		#endregion

		#region Interface

		private int MainTotalAmount => _config.UI.ItemsOnString * _config.UI.Strings;

		private float MainSwitch => -(_config.UI.ItemsOnString * _config.UI.ItemWidth +
		                              (_config.UI.ItemsOnString - 1) * _config.UI.Margin) / 2f;

		private void MainUi(BasePlayer player, int catPage = 0, int shopPage = 0,
			string search = "",
			int searchPage = 0,
			bool first = false,
			bool categories = false,
			int iCategory = 0)
		{
			#region Fields

			var shop = GetShop(player);

			var shopCategories = shop.Categories;

			var playerCart = GetPlayerCart(player);

			var isSearch = _config.UI.EnableSearch && !string.IsNullOrEmpty(search);
			var shopItems = isSearch
				? SearchItem(player, search)
				: shopCategories[catPage].GetItems;

			var container = new CuiElementContainer();

			#endregion

			#region Background

			if (first)
			{
				CuiHelper.DestroyUi(player, Layer);

				container.AddRange(getMainBg);
			}

			#endregion

			#region Main

			container.Add(_mainPanel);

			#region Header

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = $"-{_config.UI.Width / 2f} {_config.UI.Height / 2f - 50}",
					OffsetMax = $"{_config.UI.Width / 2f} {_config.UI.Height / 2f}"
				},
				Image =
				{
					Color = _firstColor
				}
			}, Layer + ".Main", Layer + ".Header");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "20 0", OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, MainTitle),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = "1 1 1 1"
				}
			}, Layer + ".Header");

			var xSwitch = -25f;
			var width = 25;

			#region Close

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = $"{xSwitch - width} -37.5",
					OffsetMax = $"{xSwitch} -12.5"
				},
				Text =
				{
					Text = Msg(player, CloseButton),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Close = Layer,
					Color = _secondColor,
					Command = "UI_Shop closeui"
				}
			}, Layer + ".Header");

			xSwitch = xSwitch - width - 5;

			#endregion

			#region Transfer

			if (_config.Transfer)
			{
				width = 115;
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 1", AnchorMax = "1 1",
						OffsetMin = $"{xSwitch - width} -37.5",
						OffsetMax = $"{xSwitch} -12.5"
					},
					Text =
					{
						Text = Msg(player, TransferTitle),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					},
					Button =
					{
						Close = Layer,
						Color = _secondColor,
						Command = $"UI_Shop pselect {catPage} {shopPage} {searchPage} {search}"
					}
				}, Layer + ".Header");

				xSwitch = xSwitch - width - 5;
			}

			#endregion

			#region Add item

			if (IsAdmin(player))
			{
				width = 70;

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 1", AnchorMax = "1 1",
						OffsetMin = $"{xSwitch - width} -37.5",
						OffsetMax = $"{xSwitch} -12.5"
					},
					Text =
					{
						Text = Msg(player, BtnAddItem),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _secondColor,
						Command = $"UI_Shop startedititem {catPage} {shopPage} {GetId()}"
					}
				}, Layer + ".Header");

				xSwitch = xSwitch - width - 5;
			}

			#endregion

			#region Balance

			width = 105;

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = $"{xSwitch - width} -37.5",
					OffsetMax = $"{xSwitch} -12.5"
				},
				Image =
				{
					Color = "0 0 0 0"
				}
			}, Layer + ".Header", Layer + ".Balance");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "0 1",
					OffsetMin = "-200 0", OffsetMax = "-5 0"
				},
				Text =
				{
					Text = Msg(player, YourBalance),
					Align = TextAnchor.MiddleRight,
					Font = "robotocondensed-bold.ttf",
					FontSize = 12,
					Color = "1 1 1 1"
				}
			}, Layer + ".Balance");

			BalanceUi(ref container, player);

			#endregion

			#endregion

			if (shopCategories.Count > 0)
			{
				#region Items

				xSwitch = MainSwitch;
				var ySwitch = _config.UI.Height / 2f - 70f;

				var inPageItems = shopItems
					.Skip((isSearch ? searchPage : shopPage) * MainTotalAmount)
					.Take(MainTotalAmount);

				var cdItems = inPageItems.FindAll(x =>
					GetCooldownTime(player.userID, x, true) > 0 || GetCooldownTime(player.userID, x, false) > 0);

				if (cdItems.Count > 0)
					_itemsToUpdate[player] = cdItems;
				else
					_itemsToUpdate.Remove(player);

				CheckUpdateController();

				for (var i = 0; i < inPageItems.Count; i++)
				{
					var shopItem = inPageItems[i];
					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = $"{xSwitch} {ySwitch - _config.UI.ItemHeight}",
							OffsetMax = $"{xSwitch + _config.UI.ItemWidth} {ySwitch}"
						},
						Image =
						{
							Color = "0 0 0 0"
						}
					}, Layer + ".Main", Layer + $".Item.{shopItem.ID}.Background");

					ItemUi(player, shopItem, ref container, catPage, shopPage);

					if ((i + 1) % _config.UI.ItemsOnString == 0)
					{
						xSwitch = MainSwitch;
						ySwitch = ySwitch - _config.UI.Margin - _config.UI.ItemHeight;
					}
					else
					{
						xSwitch += _config.UI.Margin + _config.UI.ItemWidth;
					}
				}

				#endregion

				#region Search

				if (_config.UI.EnableSearch)
				{
					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0",
							OffsetMin = $"-140 {-_config.UI.Height / 2f + 20}",
							OffsetMax = $"60 {-_config.UI.Height / 2f + 55}"
						},
						Image =
						{
							Color = _sixthColor
						}
					}, Layer + ".Main", Layer + ".Search");

					container.Add(new CuiElement
					{
						Parent = Layer + ".Search",
						Components =
						{
							new CuiInputFieldComponent
							{
								FontSize = 12,
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-regular.ttf",
								Command = $"UI_Shop search_page {catPage} {shopPage} {searchPage} ",
								Color = "1 1 1 0.65",
								CharsLimit = 32,
								NeedsKeyboard = true,
								Text = string.IsNullOrEmpty(search) ? Msg(player, SearchTitle) : $"{search}"
							},
							new CuiRectTransformComponent
							{
								AnchorMin = "0 0", AnchorMax = "1 1"
							}
						}
					});
				}

				#endregion

				#region Pages

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0",
						OffsetMin = $"{(_config.UI.EnableSearch ? 65 : -37.5f)} {-_config.UI.Height / 2f + 20}",
						OffsetMax = $"{(_config.UI.EnableSearch ? 100 : -2.5f)} {-_config.UI.Height / 2f + 55}"
					},
					Text =
					{
						Text = Msg(player, BackPage),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _sixthColor,
						Command = isSearch
							? searchPage != 0
								? $"UI_Shop search_page {catPage} {shopPage} {searchPage - 1} {search}"
								: ""
							: shopPage != 0
								? $"UI_Shop main_page {catPage} {shopPage - 1} {search}"
								: ""
					}
				}, Layer + ".Main");

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0",
						OffsetMin = $"{(_config.UI.EnableSearch ? 105 : 2.5f)} {-_config.UI.Height / 2f + 20}",
						OffsetMax = $"{(_config.UI.EnableSearch ? 140 : 37.5f)} {-_config.UI.Height / 2f + 55}"
					},
					Text =
					{
						Text = Msg(player, NextPage),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _secondColor,
						Command = isSearch
							? shopItems.Count > (searchPage + 1) * MainTotalAmount
								? $"UI_Shop search_page {catPage} {shopPage} {searchPage + 1} {search}"
								: ""
							: shopItems.Count > (shopPage + 1) * MainTotalAmount
								? $"UI_Shop main_page {catPage} {shopPage + 1} {search}"
								: ""
					}
				}, Layer + ".Main");

				#endregion
			}

			#endregion

			#region Categories

			if (first || categories) RefreshCategories(ref container, player, shopCategories, catPage, iCategory);

			#endregion

			#region Cart

			if (first) RefreshCart(ref container, player, playerCart);

			#endregion

			CuiHelper.DestroyUi(player, Layer + ".Main");
			CuiHelper.AddUi(player, container);
		}

		private void RefreshCategories(ref CuiElementContainer container, BasePlayer player,
			List<ShopCategory> shopCategories, int catPage, int iCategory = 0)
		{
			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "0 1",
					OffsetMin = "-240 0", OffsetMax = "-10 0"
				},
				Image =
				{
					Color = _thirdColor
				}
			}, Layer + ".Background", Layer + ".Categories");

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
					Color = _firstColor
				}
			}, Layer + ".Categories", Layer + ".Categories.Header");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "20 0", OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, CategoriesTitle),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = "1 1 1 1"
				}
			}, Layer + ".Categories.Header");

			#endregion

			#region Loop

			var ySwitch = -65f;

			var catId = iCategory * _config.UI.CategoriesOnString;
			var categories = shopCategories.Skip(iCategory * _config.UI.CategoriesOnString)
				.Take(_config.UI.CategoriesOnString);

			for (var j = 0; j < categories.Count; j++)
			{
				var category = categories[j];

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = $"10 {ySwitch - _config.UI.CategoriesHeight}",
						OffsetMax = $"200 {ySwitch}"
					},
					Text =
					{
						Text = $"{category.Title}",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 14,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = catId == catPage ? _secondColor : _firstColor,
						Command = catId != catPage ? $"UI_Shop gocat {catId} {iCategory}" : ""
					}
				}, Layer + ".Categories");

				ySwitch = ySwitch - _config.UI.CategoriesMargin - _config.UI.CategoriesHeight;

				catId++;
			}

			#endregion

			#region Pages

			if (_config.UI.UseScrollCategories)
			{
				var pages = (int) Math.Ceiling((double) shopCategories.Count / _config.UI.CategoriesOnString);

				if (pages > 1)
				{
					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "-20 15", OffsetMax = "-10 -65"
						},
						Image = {Color = "0 0 0 0"}
					}, Layer + ".Categories", Layer + ".Pages");

					var size = 1.0 / pages;

					var pSwitch = 0.0;

					for (var z = pages - 1; z >= 0; z--)
					{
						container.Add(new CuiButton
						{
							RectTransform = {AnchorMin = $"0 {pSwitch}", AnchorMax = $"1 {pSwitch + size}"},
							Button =
							{
								Command = $"UI_Shop changeicat {catPage} {z}",
								Color = z == iCategory ? _secondColor : _firstColor
							},
							Text = {Text = ""}
						}, Layer + ".Pages");

						pSwitch += size;
					}
				}
			}
			else
			{
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 0",
						AnchorMax = "1 0",
						OffsetMin = "-25 37",
						OffsetMax = "-5 57"
					},
					Text =
					{
						Text = Msg(player, BtnBack),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 9,
						Color = "1 1 1 0.95"
					},
					Button =
					{
						Color = HexToCuiColor(_config.SecondColor, 33),
						Command = iCategory != 0 ? $"UI_Shop changeicat {catPage} {iCategory - 1}" : ""
					}
				}, Layer + ".Categories");

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 0",
						AnchorMax = "1 0",
						OffsetMin = "-25 15",
						OffsetMax = "-5 35"
					},
					Text =
					{
						Text = Msg(player, BtnNext),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 0.95"
					},
					Button =
					{
						Color = _secondColor,
						Command = shopCategories.Count > (iCategory + 1) * _config.UI.CategoriesOnString
							? $"UI_Shop changeicat {catPage} {iCategory + 1}"
							: ""
					}
				}, Layer + ".Categories");
			}

			#endregion

			CuiHelper.DestroyUi(player, Layer + ".Categories");
		}

		private const int CartTotalAmount = 7;
		private const float CartHeight = 45f;
		private const float CartMargin = 5f;

		private void RefreshCart(ref CuiElementContainer container, BasePlayer player, DataCart playerCart,
			int cartPage = 0)
		{
			var ySwitch = -60f;

			var i = 0;

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "1 0", AnchorMax = "1 1",
					OffsetMin = "10 0", OffsetMax = "240 0"
				},
				Image =
				{
					Color = _thirdColor
				}
			}, Layer + ".Background", Layer + ".PlayerCart");

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
					Color = _firstColor
				}
			}, Layer + ".PlayerCart", Layer + ".PlayerCart.Header");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "20 0", OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, ShoppingBag),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = "1 1 1 1"
				}
			}, Layer + ".PlayerCart.Header");

			#region Pages

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-128 -35", OffsetMax = "-73 -15"
				},
				Text =
				{
					Text = Msg(player, BackTitle),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = _thirdColor,
					Command = cartPage != 0 ? $"UI_Shop cart_page {cartPage - 1}" : ""
				}
			}, Layer + ".PlayerCart.Header");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-70 -35", OffsetMax = "-15 -15"
				},
				Text =
				{
					Text = Msg(player, NextTitle),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = _secondColor,
					Command = playerCart.Items.Count > (cartPage + 1) * CartTotalAmount
						? $"UI_Shop cart_page {cartPage + 1}"
						: ""
				}
			}, Layer + ".PlayerCart.Header");

			#endregion

			#endregion

			#region Items

			foreach (var check in playerCart.Items.Skip(cartPage * CartTotalAmount).Take(CartTotalAmount))
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = $"-105 {ySwitch - CartHeight}",
						OffsetMax = $"105 {ySwitch}"
					},
					Image =
					{
						Color = _firstColor
					}
				}, Layer + ".PlayerCart", Layer + $".PlayerCart.Item.{i}");

				if (check.Key.Blueprint)
					container.Add(new CuiElement
					{
						Parent = Layer + $".PlayerCart.Item.{i}",
						Components =
						{
							new CuiRawImageComponent
							{
								Png = ImageLibrary.Call<string>("GetImage", "blueprintbase")
							},
							new CuiRectTransformComponent
							{
								AnchorMin = "0 0",
								AnchorMax = "0 0",
								OffsetMin = "10 5",
								OffsetMax = "45 40"
							}
						}
					});

				container.Add(check.Key.GetImage("0 0", "0 0", "10 5", "45 40", Layer + $".PlayerCart.Item.{i}"));

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0.5", AnchorMax = "1 1",
						OffsetMin = "50 0", OffsetMax = "0 0"
					},
					Text =
					{
						Text = $"{check.Key.PublicTitle}",
						Align = TextAnchor.LowerLeft,
						Font = "robotocondensed-bold.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					}
				}, Layer + $".PlayerCart.Item.{i}");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 0.5",
						OffsetMin = "50 0", OffsetMax = "0 0"
					},
					Text =
					{
						Text = Msg(player, AmountTitle, check.Key.Amount * check.Value),
						Align = TextAnchor.UpperLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					}
				}, Layer + $".PlayerCart.Item.{i}");

				#region Amount

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 0.5", AnchorMax = "1 0.5",
						OffsetMin = "-60 -17.5", OffsetMax = "-10 -2.5"
					},
					Text =
					{
						Text = Msg(player, RemoveTitle),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _secondColor,
						Command = $"UI_Shop cart_item_remove {check.Key.ID}"
					}
				}, Layer + $".PlayerCart.Item.{i}");

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 0.5", AnchorMax = "1 0.5",
						OffsetMin = "-60 2.5", OffsetMax = "-45 17.5"
					},
					Text =
					{
						Text = Msg(player, MinusTitle),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _secondColor,
						Command = $"UI_Shop cart_item_change {check.Key.ID} {check.Value - 1}"
					}
				}, Layer + $".PlayerCart.Item.{i}");

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 0.5", AnchorMax = "1 0.5",
						OffsetMin = "-25 2.5", OffsetMax = "-10 17.5"
					},
					Text =
					{
						Text = Msg(player, PlusTitle),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _secondColor,
						Command = $"UI_Shop cart_item_change {check.Key.ID} {check.Value + 1}"
					}
				}, Layer + $".PlayerCart.Item.{i}");

				container.Add(new CuiElement
				{
					Parent = Layer + $".PlayerCart.Item.{i}",
					Components =
					{
						new CuiInputFieldComponent
						{
							FontSize = 10,
							Font = "robotocondensed-regular.ttf",
							Align = TextAnchor.MiddleCenter,
							Command = $"UI_Shop cart_item_change {check.Key.ID} ",
							Color = "1 1 1 0.95",
							CharsLimit = 5,
							NeedsKeyboard = true,
							Text = $"{check.Value}"
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "1 0.5", AnchorMax = "1 0.5",
							OffsetMin = "-45 2.5", OffsetMax = "-25 17.5"
						}
					}
				});

				#endregion

				ySwitch = ySwitch - CartMargin - CartHeight;

				i++;
			}

			#endregion

			#region Footer

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 0",
					OffsetMin = "0 0", OffsetMax = "0 80"
				},
				Image =
				{
					Color = _firstColor
				}
			}, Layer + ".PlayerCart", Layer + ".PlayerCart.Footer");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 0", AnchorMax = "0.5 0",
					OffsetMin = "-90 10", OffsetMax = "90 40"
				},
				Text =
				{
					Text = Msg(player, BuyTitle),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = _secondColor,
					Command = "UI_Shop cart_try_buyitems"
				}
			}, Layer + ".PlayerCart.Footer");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-90 -40", OffsetMax = "90 0"
				},
				Text =
				{
					Text = Msg(player, ItemsTitle, playerCart.GetAmount()),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				}
			}, Layer + ".PlayerCart.Footer");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-90 -40", OffsetMax = "90 0"
				},
				Text =
				{
					Text = Msg(player, CostTitle, playerCart.GetPrice(player)),
					Align = TextAnchor.MiddleRight,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				}
			}, Layer + ".PlayerCart.Footer");

			#endregion

			CuiHelper.DestroyUi(player, Layer + ".PlayerCart");
		}

		private void AcceptBuy(BasePlayer player)
		{
			var container = new CuiElementContainer
			{
				{
					new CuiPanel
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Image = {Color = HexToCuiColor(_config.ThirdColor, 99)}
					},
					_config.UI.DisplayType, ModalLayer
				},
				{
					new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0.5 0.5",
							AnchorMax = "0.5 0.5",
							OffsetMin = "-70 40",
							OffsetMax = "70 60"
						},
						Text =
						{
							Text = Msg(player, PurchaseConfirmation),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 12,
							Color = "1 1 1 1"
						}
					},
					ModalLayer
				},
				{
					new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0.5 0.5",
							AnchorMax = "0.5 0.5",
							OffsetMin = "-70 10",
							OffsetMax = "70 40"
						},
						Text =
						{
							Text = Msg(player, BuyTitle),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 10,
							Color = "1 1 1 1"
						},
						Button =
						{
							Color = _secondColor,
							Command = "UI_Shop cart_buyitems",
							Close = ModalLayer
						}
					},
					ModalLayer
				},
				{
					new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0.5 0.5",
							AnchorMax = "0.5 0.5",
							OffsetMin = "-70 -22.5",
							OffsetMax = "70 7.5"
						},
						Text =
						{
							Text = Msg(player, CancelTitle),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 10,
							Color = "1 1 1 1"
						},
						Button = {Color = HexToCuiColor(_config.SecondColor, 33), Close = ModalLayer}
					},
					ModalLayer
				}
			};


			CuiHelper.DestroyUi(player, ModalLayer);
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
						Image = {Color = HexToCuiColor(_config.ThirdColor, 98)},
						CursorEnabled = true
					},
					_config.UI.DisplayType, ModalLayer
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
						Image = {Color = _fifthColor}
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
							Color = "1 1 1 1"
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
							Color = "1 1 1 1"
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
							Color = "1 1 1 1"
						},
						Button = {Color = _seventhColor, Close = ModalLayer}
					},
					ModalLayer + ".Main"
				}
			};

			CuiHelper.DestroyUi(player, ModalLayer);
			CuiHelper.AddUi(player, container);
		}

		private void SecondModalUi(BasePlayer player, ShopItem item, bool buy, int amount = 1)
		{
			var container = new CuiElementContainer
			{
				{
					new CuiPanel
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Image = {Color = HexToCuiColor(_config.ThirdColor, 98)},
						CursorEnabled = true
					},
					_config.UI.DisplayType, ModalLayer
				},
				{
					new CuiButton
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text = {Text = ""},
						Button = {Color = "0 0 0 0", Close = ModalLayer}
					},
					ModalLayer
				},
				{
					new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
							OffsetMin = "-95 -100", OffsetMax = "95 110"
						},
						Image =
						{
							Color = _firstColor
						}
					},
					ModalLayer, ModalLayer + ".Main"
				},
				item.GetImage("0.5 1", "0.5 1", "-45 -100", "45 -10", ModalLayer + ".Main"),
				{
					new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = "0 -135", OffsetMax = "0 -95"
						},
						Text =
						{
							Text = $"{item.PublicTitle}",
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 16,
							Color = "1 1 1 1"
						}
					},
					ModalLayer + ".Main"
				}
			};

			#region Sell Btn

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-60 -190", OffsetMax = "60 -170"
				},
				Text =
				{
					Text = buy
						? Msg(player, BuyBtn, item.GetPrice(player) * amount)
						: Msg(player, SellBtn, item.SellPrice * amount),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = _secondColor,
					Command = buy ? $"UI_Shop fastbuyitem {item.ID} {amount}" : $"UI_Shop sellitem {item.ID} {amount}",
					Close = ModalLayer
				}
			}, ModalLayer + ".Main");

			#endregion

			#region Input

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-60 -165", OffsetMax = "-45 -150"
				},
				Text =
				{
					Text = Msg(player, MinusTitle),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = _secondColor,
					Command = $"UI_Shop tryoperateitem {item.ID} {buy} {amount - 1}"
				}
			}, ModalLayer + ".Main");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "15 -165", OffsetMax = "30 -150"
				},
				Text =
				{
					Text = Msg(player, PlusTitle),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = _secondColor,
					Command = $"UI_Shop tryoperateitem {item.ID} {buy} {amount + 1}"
				}
			}, ModalLayer + ".Main");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "35 -165", OffsetMax = "60 -150"
				},
				Text =
				{
					Text = Msg(player, TitleMax),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = _secondColor,
					Command = $"UI_Shop tryoperateitem {item.ID} {buy} all"
				}
			}, ModalLayer + ".Main");

			container.Add(new CuiElement
			{
				Parent = ModalLayer + ".Main",
				Components =
				{
					new CuiInputFieldComponent
					{
						FontSize = 10,
						Font = "robotocondensed-regular.ttf",
						Align = TextAnchor.MiddleCenter,
						Command = $"UI_Shop tryoperateitem {item.ID} {buy} ",
						Color = "1 1 1 0.95",
						CharsLimit = 5,
						NeedsKeyboard = true,
						Text = $"{amount * item.Amount}"
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = "-45 -165", OffsetMax = "15 -150"
					}
				}
			});

			#endregion

			CuiHelper.DestroyUi(player, ModalLayer);
			CuiHelper.AddUi(player, container);
		}

		private void BalanceUi(ref CuiElementContainer container, BasePlayer player, bool select = false)
		{
			var nowEconomy = EconomyChoice.GetEconomy(player);

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "0.99 0.99"},
				Text =
				{
					Text = nowEconomy.GetBalanceTitle(player),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = _secondColor,
					Command = $"UI_Shop economychange {!select}",
					Close = Layer + ".Balance.Value"
				}
			}, Layer + ".Balance", Layer + ".Balance.Value");

			if (select && _economics.Count > 1)
			{
				var halfWidth = (_economics.Count * _config.UI.SelectCurrency.EconomyWidth +
				                 (_economics.Count - 1) * _config.UI.SelectCurrency.EconomyMargin) / 2f;
				var xSwitch = -halfWidth;

				#region Background

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin =
							$"-{halfWidth + _config.UI.SelectCurrency.FrameWidth} {_config.UI.SelectCurrency.FrameIndent}",
						OffsetMax =
							$"{halfWidth + _config.UI.SelectCurrency.FrameWidth} {_config.UI.SelectCurrency.FrameIndent + _config.UI.SelectCurrency.EconomyHeight + _config.UI.SelectCurrency.FrameHeader}"
					},
					Image =
					{
						Color = _firstColor
					}
				}, Layer + ".Balance.Value", Layer + ".Balance.Background");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = _config.UI.SelectCurrency.TitlePosition.AnchorMin,
						AnchorMax = _config.UI.SelectCurrency.TitlePosition.AnchorMax,
						OffsetMin = _config.UI.SelectCurrency.TitlePosition.OffsetMin,
						OffsetMax = _config.UI.SelectCurrency.TitlePosition.OffsetMax
					},
					Text =
					{
						Text = Msg(player, ChoiceEconomy),
						Align = _config.UI.SelectCurrency.Title.Align,
						Font = _config.UI.SelectCurrency.Title.Font,
						FontSize = _config.UI.SelectCurrency.Title.FontSize,
						Color = _config.UI.SelectCurrency.Title.Color.Get
					}
				}, Layer + ".Balance.Background");

				#endregion

				#region Economics

				for (var i = 0; i < _economics.Count; i++)
				{
					var economyConf = _economics[i];

					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0",
							OffsetMin = $"{xSwitch} {_config.UI.SelectCurrency.EconomyIndent}",
							OffsetMax =
								$"{xSwitch + _config.UI.SelectCurrency.EconomyWidth} {_config.UI.SelectCurrency.EconomyIndent + _config.UI.SelectCurrency.EconomyHeight}"
						},
						Text =
						{
							Text = economyConf.GetTitle(player),
							Align = _config.UI.SelectCurrency.EconomyTitle.Align,
							Font = _config.UI.SelectCurrency.EconomyTitle.Font,
							FontSize = _config.UI.SelectCurrency.EconomyTitle.FontSize,
							Color = _config.UI.SelectCurrency.EconomyTitle.Color.Get
						},
						Button =
						{
							Color = economyConf.IsSame(nowEconomy)
								? _config.UI.SelectCurrency.SelectedEconomyColor.Get
								: _config.UI.SelectCurrency.UnselectedEconomyColor.Get,
							Command = $"UI_Shop economy_set {economyConf.ID}"
						}
					}, Layer + ".Balance.Background");

					xSwitch += _config.UI.SelectCurrency.EconomyWidth + _config.UI.SelectCurrency.EconomyMargin;
				}

				#endregion
			}

			CuiHelper.DestroyUi(player, Layer + ".Balance.Value");
		}

		private void EditUi(BasePlayer player, int category, int page, int itemId = 0, bool First = false)
		{
			var container = new CuiElementContainer();

			#region Dictionary

			if (!_itemEditing.ContainsKey(player))
			{
				var shopItem = FindItemById(itemId);
				if (shopItem != null)
					_itemEditing[player] = shopItem.ToDictionary();
				else
					_itemEditing[player] = new Dictionary<string, object>
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
						["Amount"] = 1,
						["Price"] = 100.0,
						["SellPrice"] = 100.0,
						["Plugin_Hook"] = string.Empty,
						["Plugin_Name"] = string.Empty,
						["Plugin_Amount"] = 1
					};
			}

			#endregion

			var edit = _itemEditing[player];

			#region Background

			if (First)
			{
				CuiHelper.DestroyUi(player, EditingLayer);

				container.Add(new CuiPanel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Image = {Color = HexToCuiColor(_config.FirstColor, 95)},
					CursorEnabled = true
				}, _config.UI.DisplayType, EditingLayer);
			}

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
					Color = _thirdColor
				}
			}, EditingLayer, EditingLayer + ".Main");

			#region Header

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -50",
					OffsetMax = "0 0"
				},
				Image = {Color = _firstColor}
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
					Color = "1 1 1 1"
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
					Color = "1 1 1 1"
				},
				Button =
				{
					Close = EditingLayer,
					Color = _secondColor,
					Command = "UI_Shop closeediting"
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
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = HexToCuiColor(_config.SecondColor, type == ItemType.Item ? 100 : 50),
					Command = $"UI_Shop edititem {category} {page} Type {ItemType.Item}"
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
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = HexToCuiColor(_config.SecondColor, type == ItemType.Command ? 100 : 50),
					Command = $"UI_Shop edititem {category} {page} Type {ItemType.Command}"
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
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = HexToCuiColor(_config.SecondColor, type == ItemType.Plugin ? 100 : 50),
					Command = $"UI_Shop edititem {category} {page} Type {ItemType.Plugin}"
				}
			}, EditingLayer + ".Main");

			#endregion

			#region Command

			EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"-240 -110",
				"-75 -60",
				$"UI_Shop edititem {category} {page} Command ",
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
				Image = {Color = _firstColor}
			}, EditingLayer + ".Main", EditingLayer + ".Image");

			if (_enabledImageLibrary && !string.IsNullOrEmpty(shortName))
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
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = _secondColor,
					Command = $"UI_Shop selectitem {category}"
				}
			}, EditingLayer + ".Main");

			#endregion

			#region ShortName

			EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"-85 -190",
				"75 -130",
				$"UI_Shop edititem {category} {page} ShortName ",
				new KeyValuePair<string, object>("ShortName", edit["ShortName"]));

			#endregion

			#region Skin

			EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"80 -190",
				"240 -130",
				$"UI_Shop edititem {category} {page} Skin ",
				new KeyValuePair<string, object>("Skin", edit["Skin"]));

			#endregion

			#region DisplayName

			EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"-85 -260",
				"75 -200",
				$"UI_Shop edititem {category} {page} DisplayName ",
				new KeyValuePair<string, object>("DisplayName", edit["DisplayName"]));

			#endregion

			#region Amount

			EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"80 -260",
				"240 -200",
				$"UI_Shop edititem {category} {page} Amount ",
				new KeyValuePair<string, object>("Amount", edit["Amount"]));

			#endregion

			#region SellPrice

			EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"-85 -330",
				"75 -270",
				$"UI_Shop edititem {category} {page} SellPrice ",
				new KeyValuePair<string, object>("SellPrice", edit["SellPrice"]));

			#endregion

			#region Price

			EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"80 -330",
				"240 -270",
				$"UI_Shop edititem {category} {page} Price ",
				new KeyValuePair<string, object>("Price", edit["Price"]));

			#endregion

			#region Title

			EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"-240 -395",
				"-5 -335",
				$"UI_Shop edititem {category} {page} Title ",
				new KeyValuePair<string, object>("Title", edit["Title"]));

			#endregion

			#region Image

			EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"5 -395",
				"240 -335",
				$"UI_Shop edititem {category} {page} Image ",
				new KeyValuePair<string, object>("Image", edit["Image"]));

			#endregion

			#region Plugin Hook

			EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"-240 -460",
				"-85 -400",
				$"UI_Shop edititem {category} {page} Plugin_Hook ",
				new KeyValuePair<string, object>("Plugin_Hook", edit["Plugin_Hook"]));

			#endregion

			#region Plugin Name

			EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"-80 -460",
				"80 -400",
				$"UI_Shop edititem {category} {page} Plugin_Name ",
				new KeyValuePair<string, object>("Plugin_Name", edit["Plugin_Name"]));

			#endregion

			#region Plugin Amount

			EditFieldUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"85 -460",
				"240 -400",
				$"UI_Shop edititem {category} {page} Plugin_Amount ",
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
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = _secondColor,
					Command = $"UI_Shop saveitem {category} {page}",
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
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _fifthColor,
						Command = $"UI_Shop removeitem {category}",
						Close = EditingLayer
					}
				}, EditingLayer + ".Main");

			#endregion

			#region Bools

			var blueprint = Convert.ToBoolean(edit["Blueprint"]);

			var xSwitch = -240f;

			var bpText = Msg(player, EditBlueprint);

			// var margin = 5f;

			// var size = bpText.Length * (2 + 10 * 0.25);

			CheckBoxUi(ref container, EditingLayer + ".Main", CuiHelper.GetGuid(),
				"0.5 0", "0.5 0",
				$"{xSwitch} 10",
				$"{xSwitch + 10} 20",
				blueprint,
				$"UI_Shop edititem {category} {page} Blueprint {!blueprint}",
				bpText);

			//xSwitch += size + margin;

			#endregion

			#endregion

			CuiHelper.DestroyUi(player, EditingLayer + ".Main");
			CuiHelper.AddUi(player, container);
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

			CreateOutLine(ref container, name, _secondColor, 1);

			if (enabled)
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1"
					},
					Image = {Color = _secondColor}
				}, name);

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text = {Text = ""},
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
					Color = "1 1 1 1"
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
					Color = "1 1 1 1"
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

			CreateOutLine(ref container, $"{name}.Value", _firstColor);

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
				Text = {Text = ""},
				Button =
				{
					Close = ModalLayer,
					Color = HexToCuiColor(_config.FirstColor, 80)
				}
			}, _config.UI.DisplayType, ModalLayer);

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
					Color = _thirdColor
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
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = selectedCategory == cat.Key
							? _secondColor
							: _firstColor,
						Command = $"UI_Shop selectitem {category} {cat.Key}"
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
					.Where(x => x.Value.StartsWith(input) || x.Value.Contains(input) || x.Value.EndsWith(input))
				: _itemsCategories[selectedCategory];

			var itemsAmount = temp.Count;
			var Items = temp.Skip(page * totalAmount).Take(totalAmount);

			Items.ForEach(item =>
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = $"{xSwitch} {ySwitch - Height}",
						OffsetMax = $"{xSwitch + Width} {ySwitch}"
					},
					Image = {Color = _firstColor}
				}, ModalLayer + ".Main", ModalLayer + $".Item.{item.Key}");

				container.Add(new CuiElement
				{
					Parent = ModalLayer + $".Item.{item.Key}",
					Components =
					{
						new CuiImageComponent
						{
							ItemId = item.Key
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
					Text = {Text = ""},
					Button =
					{
						Color = "0 0 0 0",
						Command = $"UI_Shop takeitem {category} {page} {item.Value}",
						Close = ModalLayer
					}
				}, ModalLayer + $".Item.{item.Key}");

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
				Image = {Color = _secondColor}
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
						Command = $"UI_Shop selectitem {category} {selectedCategory} 0 ",
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
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = _firstColor,
					Command = page != 0
						? $"UI_Shop selectitem {category} {selectedCategory} {page - 1} {input}"
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
					Color = _secondColor,
					Command = itemsAmount > (page + 1) * totalAmount
						? $"UI_Shop selectitem {category} {selectedCategory} {page + 1} {input}"
						: ""
				}
			}, ModalLayer + ".Main");

			#endregion

			#endregion

			CuiHelper.DestroyUi(player, ModalLayer);
			CuiHelper.AddUi(player, container);
		}

		private void BuyButtonUi(BasePlayer player, ref CuiElementContainer container, ShopItem shopItem)
		{
			var cooldownTime = GetCooldownTime(player.userID, shopItem, true);
			if (cooldownTime > 0)
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = "-65 -165", OffsetMax = "65 -135"
					},
					Image =
					{
						Color = HexToCuiColor(_config.SecondColor, 33)
					}
				}, Layer + $".Item.{shopItem.ID}", Layer + $".Item.{shopItem.ID}.Buy");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "5 0", OffsetMax = "0 0"
					},
					Text =
					{
						Text = Msg(player, BuyCooldownTitle),
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					}
				}, Layer + $".Item.{shopItem.ID}.Buy");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "0 0", OffsetMax = "-10 0"
					},
					Text =
					{
						Text = $"{FormatShortTime(player, TimeSpan.FromSeconds(cooldownTime))}",
						Align = TextAnchor.MiddleRight,
						Font = "robotocondensed-bold.ttf",
						FontSize = 14,
						Color = "1 1 1 1"
					}
				}, Layer + $".Item.{shopItem.ID}.Buy");
			}
			else
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = "-65 -165", OffsetMax = "65 -135"
					},
					Image =
					{
						Color = _secondColor
					}
				}, Layer + $".Item.{shopItem.ID}", Layer + $".Item.{shopItem.ID}.Buy");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "5 0", OffsetMax = "0 0"
					},
					Text =
					{
						Text = Msg(player, BuyTitle),
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					}
				}, Layer + $".Item.{shopItem.ID}.Buy");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "0 0", OffsetMax = "-10 0"
					},
					Text =
					{
						Text = Msg(player, ItemPrice, shopItem.GetPrice(player)),
						Align = TextAnchor.MiddleRight,
						Font = "robotocondensed-bold.ttf",
						FontSize = 14,
						Color = "1 1 1 1"
					}
				}, Layer + $".Item.{shopItem.ID}.Buy");

				container.Add(new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text = {Text = ""},
					Button =
					{
						Color = "0 0 0 0",
						Command = shopItem.ForceBuy
							? $"UI_Shop tryoperateitem {shopItem.ID} {true}"
							: $"UI_Shop buyitem {shopItem.ID}"
					}
				}, Layer + $".Item.{shopItem.ID}.Buy");
			}

			CuiHelper.DestroyUi(player, Layer + $".Item.{shopItem.ID}.Buy");
		}

		private void SellButtonUi(BasePlayer player, ref CuiElementContainer container, ShopItem shopItem)
		{
			var cooldownTime = GetCooldownTime(player.userID, shopItem, false);
			if (cooldownTime > 0)
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = "-50 -15", OffsetMax = "50 5"
					},
					Image =
					{
						Color = HexToCuiColor(_config.FifthColor, 65)
					}
				}, Layer + $".Item.{shopItem.ID}", Layer + $".Item.{shopItem.ID}.Sell");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "5 0", OffsetMax = "-5 0"
					},
					Text =
					{
						Text = Msg(player, SellCooldownTitle),
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					}
				}, Layer + $".Item.{shopItem.ID}.Sell");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "0 0", OffsetMax = "-10 0"
					},
					Text =
					{
						Text = $"{FormatShortTime(player, TimeSpan.FromSeconds(cooldownTime))}",
						Align = TextAnchor.MiddleRight,
						Font = "robotocondensed-bold.ttf",
						FontSize = 14,
						Color = "1 1 1 1"
					}
				}, Layer + $".Item.{shopItem.ID}.Sell");
			}
			else
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = "-50 -15", OffsetMax = "50 5"
					},
					Image =
					{
						Color = _fifthColor
					}
				}, Layer + $".Item.{shopItem.ID}", Layer + $".Item.{shopItem.ID}.Sell");


				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "5 0", OffsetMax = "-5 0"
					},
					Text =
					{
						Text = Msg(player, SellTitle),
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					}
				}, Layer + $".Item.{shopItem.ID}.Sell");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "0 0", OffsetMax = "-10 0"
					},
					Text =
					{
						Text = Msg(player, ItemPrice, shopItem.SellPrice),
						Align = TextAnchor.MiddleRight,
						Font = "robotocondensed-bold.ttf",
						FontSize = 14,
						Color = "1 1 1 1"
					}
				}, Layer + $".Item.{shopItem.ID}.Sell");

				container.Add(new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text = {Text = ""},
					Button =
					{
						Color = "0 0 0 0",
						Command = $"UI_Shop tryoperateitem {shopItem.ID} {false}"
					}
				}, Layer + $".Item.{shopItem.ID}.Sell");
			}

			CuiHelper.DestroyUi(player, Layer + $".Item.{shopItem.ID}.Sell");
		}

		private void ItemUi(BasePlayer player, ShopItem shopItem, ref CuiElementContainer container, int catPage,
			int shopPage, bool status = false)
		{
			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -150", OffsetMax = "0 0"
				},
				Image =
				{
					Color = _firstColor
				}
			}, Layer + $".Item.{shopItem.ID}.Background", Layer + $".Item.{shopItem.ID}");

			if (status)
			{
				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "0 -135", OffsetMax = "0 0"
					},
					Text =
					{
						Text = $"{shopItem.Description}",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 14,
						Color = "1 1 1 1"
					}
				}, Layer + $".Item.{shopItem.ID}");
			}
			else
			{
				#region Blueprint

				if (shopItem.Blueprint)
					container.Add(new CuiElement
					{
						Parent = Layer + $".Item.{shopItem.ID}",
						Components =
						{
							new CuiRawImageComponent
							{
								Png = ImageLibrary.Call<string>("GetImage", "blueprintbase")
							},
							new CuiRectTransformComponent
							{
								AnchorMin = "0.5 1",
								AnchorMax = "0.5 1",
								OffsetMin = "-35 -85",
								OffsetMax = "35 -15"
							}
						}
					});

				#endregion

				container.Add(shopItem.GetImage("0.5 1", "0.5 1", "-35 -85", "35 -15", Layer + $".Item.{shopItem.ID}",
					Layer + $".Item.{shopItem.ID}.Image"));

				#region Name

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "0 -100", OffsetMax = "0 -85"
					},
					Text =
					{
						Text = $"{shopItem.PublicTitle}",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 0.95"
					}
				}, Layer + $".Item.{shopItem.ID}");

				#endregion
			}

			#region Amount

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-65 -130", OffsetMax = "-5 -110"
				},
				Text =
				{
					Text = Msg(player, ItemAmount),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = "1 1 1 1"
				}
			}, Layer + $".Item.{shopItem.ID}");

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-10 -130", OffsetMax = "30 -110"
				},
				Image =
				{
					Color = HexToCuiColor(_config.SecondColor, 33)
				}
			}, Layer + $".Item.{shopItem.ID}", Layer + $".Item.{shopItem.ID}.Amount");

			container.Add(new CuiLabel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text =
				{
					Text = $"{shopItem.Amount}",
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = "1 1 1 1"
				}
			}, Layer + $".Item.{shopItem.ID}.Amount");

			#endregion

			#region Info

			if (!string.IsNullOrEmpty(shopItem.Description))
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = "35 -130", OffsetMax = "55 -110"
					},
					Text =
					{
						Text = Msg(player, InfoTitle),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 14,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = HexToCuiColor("#000000"),
						Command = $"UI_Shop item_info {shopItem.ID} {catPage} {shopPage} {status}"
					}
				}, Layer + $".Item.{shopItem.ID}");

			#endregion

			#region Discount

			var discount = shopItem.GetDiscount(player);
			if (discount > 0)
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "1 1", AnchorMax = "1 1",
						OffsetMin = "-30 -45", OffsetMax = "10 -25"
					},
					Image =
					{
						Color = _fourthColor
					}
				}, Layer + $".Item.{shopItem.ID}", Layer + $".Item.{shopItem.ID}.Discount");

				container.Add(new CuiLabel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text =
					{
						Text = $"-{discount}%",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					}
				}, Layer + $".Item.{shopItem.ID}.Discount");
			}

			#endregion

			#region Edit

			if (IsAdmin(player))
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1"
					},
					Text = {Text = ""},
					Button =
					{
						Color = "0 0 0 0",
						Command = $"UI_Shop startedititem {catPage} {shopPage} {shopItem.ID}"
					}
				}, Layer + $".Item.{shopItem.ID}.Image");

			#endregion

			#region Button

			if (shopItem.Price > 0.0) BuyButtonUi(player, ref container, shopItem);

			#endregion

			#region Sell

			if (shopItem.SellPrice > 0.0) SellButtonUi(player, ref container, shopItem);

			#endregion
		}

		private void SelectPlayerUi(BasePlayer player, int catPage,
			int shopPage,
			int searchPage,
			string search,
			int selectPage = 0)
		{
			#region Fields

			var Width = 180f;
			var Height = 50f;
			var xMargin = 20f;
			var yMargin = 30f;

			var amountOnString = 4;
			var strings = 5;
			var totalAmount = amountOnString * strings;

			var constSwitch = -(amountOnString * Width + (amountOnString - 1) * xMargin) / 2f;

			var xSwitch = constSwitch;
			var ySwitch = -180f;

			var i = 1;

			var players =
				BasePlayer.activePlayerList.Where(x => x != player).ToList();

			#endregion

			var container = new CuiElementContainer();

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
			}, "Overlay", Layer);

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text = {Text = ""},
				Button =
				{
					Color = "0 0 0 0",
					Close = Layer,
					Command = $"UI_Shop goback {catPage} {shopPage} {searchPage} {search}"
				}
			}, Layer);

			#endregion

			if (players.Count > 0)
			{
				#region Title

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = "-200 -140",
						OffsetMax = "200 -100"
					},
					Text =
					{
						Text = Msg(player, SelectPlayerTitle),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 32,
						Color = "1 1 1 1"
					}
				}, Layer);

				#endregion

				#region Players

				var members = players.Skip(selectPage * totalAmount).Take(totalAmount);
				for (var j = 0; j < members.Count; j++)
				{
					var member = members[j];

					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = $"{xSwitch} {ySwitch - Height}",
							OffsetMax = $"{xSwitch + Width} {ySwitch}"
						},
						Image =
						{
							Color = "0 0 0 0"
						}
					}, Layer, Layer + $".Player.{i}");

					if (_enabledImageLibrary)
						container.Add(new CuiElement
						{
							Parent = Layer + $".Player.{i}",
							Components =
							{
								new CuiRawImageComponent
									{Png = ImageLibrary.Call<string>("GetImage", $"avatar_{member.userID}")},
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
							Color = "1 1 1 1"
						}
					}, Layer + $".Player.{i}");

					container.Add(new CuiButton
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text = {Text = ""},
						Button =
						{
							Color = "0 0 0 0",
							Command =
								$"UI_Shop try_ptransfer {catPage} {shopPage} {searchPage} {member.userID} {search}"
						}
					}, Layer + $".Player.{i}");

					if (i % amountOnString == 0)
					{
						xSwitch = constSwitch;
						ySwitch = ySwitch - Height - yMargin;
					}
					else
					{
						xSwitch += Width + xMargin;
					}

					i++;
				}

				#endregion

				#region Pages

				var pageSize = 25f;
				var selPageSize = 40f;
				xMargin = 5f;

				var pages = (int) Math.Ceiling((double) players.Count / totalAmount);
				if (pages > 1)
				{
					xSwitch = -((pages - 1) * pageSize + (pages - 1) * xMargin + selPageSize) / 2f;

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
									$"{xSwitch + (selected ? selPageSize : pageSize)} {60 + (selected ? selPageSize : pageSize)}"
							},
							Text =
							{
								Text = $"{j + 1}",
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-bold.ttf",
								FontSize = selected ? 18 : 12,
								Color = "1 1 1 1"
							},
							Button =
							{
								Color = _secondColor,
								Command =
									$"UI_Shop pselect_page {catPage} {shopPage} {searchPage} {j} {search}"
							}
						}, Layer);

						xSwitch += (selected ? selPageSize : pageSize) + xMargin;
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
				}, Layer);

				container.Add(new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text = {Text = ""},
					Button =
					{
						Color = "0 0 0 0",
						Close = Layer,
						Command = $"UI_Shop goback {catPage} {shopPage} {searchPage} {search}"
					}
				}, Layer);
			}

			CuiHelper.DestroyUi(player, Layer);
			CuiHelper.AddUi(player, container);
		}

		private void TransferUi(BasePlayer player,
			ulong targetId,
			int catPage,
			int shopPage,
			int searchPage,
			string search,
			float amount = 0)
		{
			var hasSearch = !string.IsNullOrEmpty(search);
			if (hasSearch)
				search = search.Replace(" ", "-");

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
			}, "Overlay", Layer);

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text = {Text = ""},
				Button =
				{
					Color = "0 0 0 0",
					Close = Layer,
					Command = $"UI_Shop goback {catPage} {shopPage} {searchPage} {search}"
				}
			}, Layer);

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
					Color = _thirdColor
				}
			}, Layer, Layer + ".Main");

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
					Color = _firstColor
				}
			}, Layer + ".Main", Layer + ".Header");

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
					Color = "1 1 1 1"
				}
			}, Layer + ".Header");

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
					Color = "1 1 1 1"
				},
				Button =
				{
					Close = Layer,
					Color = _secondColor,
					Command = $"UI_Shop goback {catPage} {shopPage} {searchPage} {search}"
				}
			}, Layer + ".Header");

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
					Color = _firstColor
				}
			}, Layer + ".Main", Layer + ".Player");

			#region Avatar

			if (_enabledImageLibrary)
				container.Add(new CuiElement
				{
					Parent = Layer + ".Player",
					Components =
					{
						new CuiRawImageComponent
						{
							Png = ImageLibrary.Call<string>("GetImage", $"avatar_{target.userID}")
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
					Color = "1 1 1 1"
				}
			}, Layer + ".Player");

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
					Color = _firstColor
				}
			}, Layer + ".Main", Layer + ".Send");

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
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = _secondColor,
					Close = Layer,
					Command =
						$"UI_Shop ptransfer_send {catPage} {shopPage} {searchPage} {targetId} {hasSearch} {search} {amount}"
				}
			}, Layer + ".Send");

			container.Add(new CuiElement
			{
				Parent = Layer + ".Send",
				Components =
				{
					new CuiInputFieldComponent
					{
						FontSize = 12,
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						Command =
							$"UI_Shop ptransfer_amount {catPage} {shopPage} {searchPage} {targetId} {hasSearch} {search} ",
						Color = "1 1 1 0.75",
						CharsLimit = 32,
						NeedsKeyboard = true,
						Text = $"{amount}"
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

			CuiHelper.DestroyUi(player, Layer);
			CuiHelper.AddUi(player, container);
		}

		#endregion

		#region Utils

		private static Item[] PlayerItems(BasePlayer player)
		{
			return Enumerable.ToArray(_config.SellContainers.Enabled
				? _config.SellContainers.AllItems(player)
				: Enumerable.Concat(player.inventory.containerMain?.itemList ?? Enumerable.Empty<Item>(), Enumerable.Concat(player.inventory.containerBelt?.itemList ?? Enumerable.Empty<Item>(), player.inventory.containerWear?.itemList ?? Enumerable.Empty<Item>())));
		}

		private void LoadItems()
		{
			_config.Shop.SelectMany(x => x.Items).ForEach(item =>
			{
				var x = item.ItemDefinition;
			});
		}

		private void LoadNPCs()
		{
			foreach (var check in _config.NPCs) check.Value.BotID = check.Key;
		}

		private readonly Dictionary<int, EconomyConf> _additionalEconomics = new Dictionary<int, EconomyConf>();

		private readonly List<AdditionalEconomy> _economics = new List<AdditionalEconomy>();

		private void LoadEconomics()
		{
			_config.AdditionalEconomics.FindAll(x => x.Enabled)
				.ForEach(x =>
				{
					if (x.ID == 0 || _additionalEconomics.ContainsKey(x.ID))
					{
						PrintError($"Additional economy caching error. There are several economies with ID {x.ID}");
						return;
					}

					_additionalEconomics[x.ID] = x;
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

			Log("Buy", LogBuyItems, player.displayName, player.UserIDString,
				price, string.Join(", ", logItems));

			Pool.FreeList(ref logItems);
		}

		private NPCShop GetShopByPlayer(BasePlayer player)
		{
			NPCShop shop;
			return _openedShops.TryGetValue(player, out shop) ? shop : null;
		}

		private static RaycastHit? GetLookHitLayer(BasePlayer player, float maxDistance = 5f, int layerMask = -5)
		{
			RaycastHit hitInfo;
			return !Physics.Raycast(player.eyes.HeadRay(), out hitInfo, maxDistance, layerMask,
				QueryTriggerInteraction.UseGlobal)
				? (RaycastHit?) null
				: hitInfo;
		}

		private static RaycastHit? GetLookHit(BasePlayer player, float maxDistance = 5f)
		{
			RaycastHit hitInfo;
			return !Physics.Raycast(player.eyes.HeadRay(), out hitInfo, maxDistance)
				? (RaycastHit?) null
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

		private void LoadColors()
		{
			_firstColor = HexToCuiColor(_config.FirstColor);
			_secondColor = HexToCuiColor(_config.SecondColor);
			_thirdColor = HexToCuiColor(_config.ThirdColor);
			_fourthColor = HexToCuiColor(_config.FourthColor);
			_fifthColor = HexToCuiColor(_config.FifthColor);
			_sixthColor = HexToCuiColor(_config.SixthColor);
			_seventhColor = HexToCuiColor(_config.SeventhColor);
		}

		private void RegisterPermissions()
		{
			_config.Shop.ForEach(category =>
			{
				if (!string.IsNullOrEmpty(category.Permission) && !permission.PermissionExists(category.Permission))
					permission.RegisterPermission(category.Permission, this);
			});

			permission.RegisterPermission(PermAdmin, this);
			permission.RegisterPermission(PermFreeBypass, this);
			permission.RegisterPermission(PermSetVM, this);
			permission.RegisterPermission(PermSetNPC, this);
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
					         !string.IsNullOrEmpty(shopItem.Image) ? shopItem.Image : shopItem.ShortName)
				         .Where(image => !_images.Contains(image))) _images.Add(image);
		}

		private void LoadCarts()
		{
			foreach (var cartData in _data.PlayerCarts)
			{
				_carts.Add(cartData.Key, new DataCart(cartData.Value));

				if (cartData.Value.NpcCarts.Count > 0)
				{
					_cartsNPC.Add(cartData.Key, new Dictionary<string, DataCart>());
					foreach (var npcCart in cartData.Value.NpcCarts)
						_cartsNPC[cartData.Key].Add(npcCart.Key, new DataCart(npcCart.Value.Items));
				}
			}
		}

		private void LoadPlayers()
		{
			foreach (var player in BasePlayer.activePlayerList)
				OnPlayerConnected(player);
		}

		#region Custom Vending

		private readonly Dictionary<BasePlayer, CustomVendingConf> _openedCustomVending =
			new Dictionary<BasePlayer, CustomVendingConf>();

		private void LoadCustomVMs()
		{
			_config.CustomVending.ToList().ForEach(wb => CheckCustomVending(wb.Key));

			Subscribe(nameof(CanLootEntity));
		}

		private void CheckCustomVending(ulong netId)
		{
			var vendingMachine = BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as VendingMachine;
			if (vendingMachine != null) return;

			_config.CustomVending.Remove(netId);
			SaveConfig();
		}

		#endregion

		#region Avatar

		private readonly Regex _regex = new Regex(@"<avatarFull><!\[CDATA\[(.*)\]\]></avatarFull>");

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

		private int SecondsFromWipe()
		{
			return (int) DateTime.Now.ToUniversalTime()
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

				ImageLibrary?.Call("SendImage", player, image);

				yield return CoroutineEx.waitForSeconds(_config.ImagesDelay);
			}
		}

		private ShopCategory FindCategoryByName(string name)
		{
			return _config.Shop.Find(cat => cat.Title == name);
		}

		private ShopItem FindItemById(int id)
		{
			ShopItem item;
			return _shopItems.TryGetValue(id, out item) ? item : null;
		}

		private void FillCategories()
		{
			if (_config.Shop.Count != 0)
				return;

			_config.Shop.Clear();

			var sw = Stopwatch.StartNew();

			var dict = new Dictionary<string, List<ItemDefinition>>();

			ItemManager.itemList.ForEach(item =>
			{
				var itemCategory = item.category.ToString();

				if (dict.ContainsKey(itemCategory))
					dict[itemCategory].Add(item);
				else
					dict.Add(itemCategory, new List<ItemDefinition> {item});
			});

			var id = 0;
			foreach (var check in dict)
			{
				var category = new ShopCategory
				{
					Enabled = true,
					Title = check.Key,
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

				_config.Shop.Add(category);
			}

			SaveConfig();

			sw.Stop();
			PrintWarning($"The store was filled with items in {sw.ElapsedMilliseconds} ms!");
		}

		private double GetItemCost(ItemDefinition itemDefinition)
		{
			return ItemCostCalculator?.Call<double>("GetItemCost", itemDefinition) ?? 100;
		}

		private void CheckOnDuplicates()
		{
			if (_config.Shop.Count == 0) return;

			var items = new List<int>();
			var duplicates = new List<int>();

			_config.Shop.SelectMany(x => x.Items).ForEach(item =>
			{
				if (items.Contains(item.ID))
				{
					duplicates.Add(item.ID);
					return;
				}

				items.Add(item.ID);
			});

			if (duplicates.Count > 0)
				PrintError(
					$"Matching item IDs found (Shop): {string.Join(", ", duplicates.Select(x => x.ToString()))}");
		}

		private void ItemsToDict()
		{
			_shopItems.Clear();

			ItemManager.itemList.ForEach(item =>
			{
				var itemCategory = item.category.ToString();

				var kvp = new KeyValuePair<int, string>(item.itemid, item.shortname);

				if (_itemsCategories.ContainsKey(itemCategory))
				{
					if (!_itemsCategories[itemCategory].Contains(kvp))
						_itemsCategories[itemCategory].Add(kvp);
				}
				else
				{
					_itemsCategories.Add(itemCategory, new List<KeyValuePair<int, string>> {kvp});
				}
			});

			_config.Shop.ForEach(category => category.LoadIDs());
		}

		private List<ShopCategory> GetCategories(BasePlayer player, NPCShop npcShop = null)
		{
			CustomVendingConf customVendingConf;
			_openedCustomVending.TryGetValue(player, out customVendingConf);

			return _config.Shop.FindAll(cat =>
			{
				var enabled = cat != null && cat.Enabled;
				if (!enabled)
					return false;

				var hasPermissions = string.IsNullOrEmpty(cat.Permission) || player.HasPermission(cat.Permission);
				if (!hasPermissions)
					return false;

				var useNpc = npcShop == null || npcShop.Shops.Contains("*") || npcShop.Shops.Contains(cat.Title);
				if (!useNpc)
					return false;

				var useVending = customVendingConf == null ||
				                 customVendingConf.Categories.Contains("*") ||
				                 customVendingConf.Categories.Contains(cat.Title);
				if (!useVending)
					return false;

				return true;
			});
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

		private static int ItemCount(Item[] items, string shortname, ulong skin)
		{
			return items.Where(item =>
					item.info.shortname == shortname && !item.isBroken && (skin == 0 || item.skin == skin))
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

		private DataCart GetPlayerCart(BasePlayer player)
		{
			DataCart cart;

			var npcShop = GetShopByPlayer(player);
			if (npcShop != null)
			{
				Dictionary<string, DataCart> dataCarts;
				if (!_cartsNPC.TryGetValue(player.userID, out dataCarts))
					_cartsNPC[player.userID] = new Dictionary<string, DataCart>();

				if (!_cartsNPC[player.userID].TryGetValue(npcShop.BotID, out cart))
					_cartsNPC[player.userID].Add(npcShop.BotID, cart = new DataCart());

				return cart;
			}

			if (!_carts.TryGetValue(player.userID, out cart))
				_carts.Add(player.userID, cart = new DataCart());
			return cart;
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

		private void LoadImages()
		{
			if (!ImageLibrary)
			{
				PrintError("IMAGE LIBRARY IS NOT INSTALLED.");
			}
			else
			{
				_enabledImageLibrary = true;

				var imagesList = new Dictionary<string, string>();

				_config.Shop.ForEach(category =>
				{
					category.Items.ForEach(item =>
					{
						if (!string.IsNullOrEmpty(item.Image) && !imagesList.ContainsKey(item.Image))
							imagesList.Add(item.Image, item.Image);
					});
				});

				ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
			}
		}

		#endregion

		#region Cooldown

		private readonly Dictionary<BasePlayer, List<ShopItem>> _itemsToUpdate =
			new Dictionary<BasePlayer, List<ShopItem>>();

		private Dictionary<ulong, CooldownInfo> _cooldown = new Dictionary<ulong, CooldownInfo>();

		private List<BasePlayer> toRemove = new List<BasePlayer>();

		private List<KeyValuePair<BasePlayer, ShopItem>> toRemove2 = new List<KeyValuePair<BasePlayer, ShopItem>>();

		private void ItemsUpdateController()
		{
			toRemove.Clear();
			toRemove2.Clear();

			foreach (var check in _itemsToUpdate)
			{
				if (check.Key == null)
				{
					toRemove.Add(check.Key);
					continue;
				}

				var container = new CuiElementContainer();

				Array.ForEach(check.Value.ToArray(), shopItem =>
				{
					if (shopItem.Price > 0.0) BuyButtonUi(check.Key, ref container, shopItem);

					if (shopItem.SellPrice > 0.0) SellButtonUi(check.Key, ref container, shopItem);

					if (HasCooldown(check.Key.userID, shopItem, true) &&
					    HasCooldown(check.Key.userID, shopItem, false))
						toRemove2.Add(new KeyValuePair<BasePlayer, ShopItem>(check.Key, shopItem));
				});

				CuiHelper.AddUi(check.Key, container);
			}

			toRemove2.ForEach(x => RemoveCooldown(x.Key, x.Value));

			toRemove.ForEach(x => _itemsToUpdate.Remove(x));

			CheckUpdateController();
		}

		private CooldownInfo GetCooldown(ulong player)
		{
			CooldownInfo cooldownInfo;
			return _cooldown.TryGetValue(player, out cooldownInfo) ? cooldownInfo : null;
		}

		private CooldownData GetCooldown(ulong player, ShopItem item)
		{
			return GetCooldown(player)?.GetCooldown(item);
		}

		private int GetCooldownTime(ulong player, ShopItem item, bool buy)
		{
			return GetCooldown(player)?.GetCooldownTime(item, buy) ?? -1;
		}

		private bool HasCooldown(ulong player, ShopItem item, bool buy)
		{
			return GetCooldown(player)?.HasCooldown(item, buy) ?? false;
		}

		private void SetCooldown(BasePlayer player, ShopItem item, bool buy, bool needUpdate = false)
		{
			if ((buy ? item.BuyCooldown : item.SellCooldown) <= 0) return;

			if (_cooldown.ContainsKey(player.userID))
				_cooldown[player.userID].SetCooldown(item, buy);
			else
				_cooldown.Add(player.userID, new CooldownInfo().SetCooldown(item, buy));

			if (needUpdate)
			{
				if (_itemsToUpdate.ContainsKey(player))
				{
					if (!_itemsToUpdate[player].Contains(item))
						_itemsToUpdate[player].Add(item);
				}
				else
				{
					_itemsToUpdate.Add(player, new List<ShopItem> {item});
				}

				CheckUpdateController();
			}
		}

		private void RemoveCooldown(BasePlayer player, ShopItem item)
		{
			if (!_cooldown.ContainsKey(player.userID)) return;

			_itemsToUpdate[player].Remove(item);

			_cooldown[player.userID].RemoveCooldown(item);

			if (_cooldown[player.userID].Data.Count == 0)
			{
				_cooldown.Remove(player.userID);

				_itemsToUpdate.Remove(player);
			}

			CheckUpdateController();
		}

		private class CooldownInfo
		{
			#region Fields

			[JsonProperty(PropertyName = "Cooldowns", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public readonly Dictionary<int, CooldownData> Data = new Dictionary<int, CooldownData>();

			#endregion

			#region Utils

			public CooldownData GetCooldown(ShopItem item)
			{
				CooldownData data;
				return Data.TryGetValue(item.ID, out data) ? data : null;
			}

			public int GetCooldownTime(ShopItem item, bool buy)
			{
				var data = GetCooldown(item);
				if (data == null) return -1;

				return (int) ((buy ? data.LastBuyTime : data.LastSellTime).AddSeconds(
					buy ? item.BuyCooldown : item.SellCooldown) - DateTime.Now).TotalSeconds;
			}

			public bool HasCooldown(ShopItem item, bool buy)
			{
				var data = GetCooldown(item);
				if (data == null) return false;

				return (int) ((buy ? data.LastBuyTime : data.LastSellTime).AddSeconds(
					buy ? item.BuyCooldown : item.SellCooldown) - DateTime.Now).TotalSeconds <= 0;
			}

			public void RemoveCooldown(ShopItem item)
			{
				Data.Remove(item.ID);
			}

			public CooldownInfo SetCooldown(ShopItem item, bool buy)
			{
				if (!Data.ContainsKey(item.ID))
					Data.Add(item.ID, new CooldownData());

				if (buy)
					Data[item.ID].LastBuyTime = DateTime.Now;
				else
					Data[item.ID].LastSellTime = DateTime.Now;

				return this;
			}

			#endregion
		}

		private class CooldownData
		{
			public DateTime LastBuyTime = new DateTime(1970, 1, 1, 0, 0, 0);

			public DateTime LastSellTime = new DateTime(1970, 1, 1, 0, 0, 0);
		}

		#endregion

		#region Limits

		private PlayerLimits _limits;

		private class PlayerLimits
		{
			[JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public readonly Dictionary<ulong, PlayerLimitData> Players = new Dictionary<ulong, PlayerLimitData>();

			public static PlayerLimitData GetOrAdd(ulong member)
			{
				if (!_instance._limits.Players.ContainsKey(member))
					_instance._limits.Players.Add(member, new PlayerLimitData());

				return _instance._limits.Players[member];
			}
		}

		private class PlayerLimitData
		{
			[JsonProperty(PropertyName = "Limits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public readonly Dictionary<int, ItemLimitData> ItemsLimits = new Dictionary<int, ItemLimitData>();

			[JsonProperty(PropertyName = "Last Update Time")]
			public DateTime LastUpdate;

			[JsonProperty(PropertyName = "Daily Limits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public readonly Dictionary<int, ItemLimitData> DailyItemsLimits = new Dictionary<int, ItemLimitData>();

			public void AddItem(ShopItem item, bool buy, int amount, bool daily = false)
			{
				var totalAmount = item.Amount * amount;

				var dict = daily ? DailyItemsLimits : ItemsLimits;

				if (!dict.ContainsKey(item.ID)) dict.Add(item.ID, new ItemLimitData());

				if (buy)
					dict[item.ID].Buy += totalAmount;
				else
					dict[item.ID].Sell += totalAmount;
			}

			public int GetLimit(ShopItem item, bool buy, bool daily = false)
			{
				if (daily && DateTime.Now.Date != LastUpdate.Date) // auto wipe
				{
					LastUpdate = DateTime.Now;
					DailyItemsLimits.Clear();
				}

				ItemLimitData data;
				return (daily ? DailyItemsLimits : ItemsLimits).TryGetValue(item.ID, out data)
					? buy ? data.Buy : data.Sell
					: 0;
			}
		}

		private class ItemLimitData
		{
			public int Sell;

			public int Buy;
		}

		private void UseLimit(BasePlayer player, ShopItem item, bool buy, int amount, bool daily = false)
		{
			PlayerLimits.GetOrAdd(player.userID).AddItem(item, buy, amount, daily);
		}

		private int GetLimit(BasePlayer player, ShopItem item, bool buy, bool daily = false)
		{
			var hasLimit = item.GetLimit(player, buy, daily);
			if (hasLimit == 0)
				return 1;

			var used = PlayerLimits.GetOrAdd(player.userID).GetLimit(item, buy, daily);
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

			var used = PlayerLimits.GetOrAdd(player.userID).GetLimit(item, buy, daily);
			leftAmount = hasLimit - used;
			return true;
		}

		#endregion

		#region Economy Choice

		private EconomyChoice _economyChoice;

		private class EconomyChoice
		{
			[JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<ulong, int> Players = new Dictionary<ulong, int>();

			public static EconomyConf GetEconomy(BasePlayer player)
			{
				if (_instance._economics.Count > 1)
				{
					int id;
					if (_instance._economyChoice.Players.TryGetValue(player.userID, out id))
					{
						EconomyConf economyConf;
						if (_instance._additionalEconomics.TryGetValue(id, out economyConf)) return economyConf;
					}
				}

				return _config.Economy;
			}

			public static void SelectEconomy(BasePlayer player, int id)
			{
				switch (id)
				{
					case 0:
					{
						_instance._economyChoice.Players.Remove(player.userID);
						break;
					}
					default:
					{
						_instance._economyChoice.Players[player.userID] = id;
						break;
					}
				}
			}
		}

		#endregion

		#region Log

		private void Log(string filename, string key, params object[] obj)
		{
			var text = string.Format(lang.GetMessage(key, this), obj);
			if (_config.LogToConsole) Puts(text);

			if (_config.LogToFile) LogToFile(filename, $"[{DateTime.Now}] {text}", this);
		}

		#endregion

		#region Lang

		private const string
			NPCInstalled = "NPCInstalled",
			NPCNotFound = "NPCNotFound",
			EditBlueprint = "EditBlueprint",
			ChoiceEconomy = "ChoiceEconomy",
			LangTitle = "LangTitle",
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
			BalanceTitle = "BalanceTitle",
			BuyTitle = "BuyTitle",
			SellTitle = "SellTitle",
			ItemsTitle = "ItemsTitle",
			CostTitle = "CostTitle",
			PlusTitle = "PlusTitle",
			MinusTitle = "MinusTitle",
			RemoveTitle = "RemoveTitle",
			AmountTitle = "AmountTitle",
			NextTitle = "NextTitle",
			BackTitle = "BackTitle",
			ItemPrice = "ItemPrice",
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
			BuyBtn = "BuyBtn",
			SellBtn = "SellBtn",
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
				[BalanceTitle] = "{0} RP",
				[BuyTitle] = "Buy",
				[SellTitle] = "Sell",
				[ItemsTitle] = "Items\n<b>{0} pcs</b>",
				[CostTitle] = "Cost\n<b>{0} RP</b>",
				[PlusTitle] = "+",
				[MinusTitle] = "-",
				[RemoveTitle] = "Remove",
				[AmountTitle] = "Amount {0} pcs",
				[BackTitle] = "Back",
				[NextTitle] = "Next",
				[ItemPrice] = "{0} RP",
				[ItemAmount] = "Amt.",
				[CloseButton] = "✕",
				[YourBalance] = "Your Balance",
				[MainTitle] = "Shop",
				[CategoriesTitle] = "Categories",
				[ShoppingBag] = "Shopping Bag",
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
				[SellBtn] = "SELL FOR {0} RP",
				[BuyBtn] = "BUY FOR {0} RP",
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
				[SuccessfulTransfer] = "Transferred {0}RP to player '{1}'",
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
				[LangTitle] = "Economics",
				[ChoiceEconomy] = "Choice of currency",
				[EditBlueprint] = "Blueprint",
				[NPCNotFound] = "NPC not found!",
				[NPCInstalled] = "You have successfully installed the custom NPC!",
				["sr_title"] = "Server Rewards",
				["sr_balance"] = "{0} RP"
			}, this);
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

		#region Interface

		private CuiElementContainer getMainBg = new CuiElementContainer();

		private CuiElement _mainPanel;

		private void LoadCacheUI()
		{
			LoadMainBg();

			LoadMainPanel();
		}

		private void LoadMainBg()
		{
			getMainBg.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image =
				{
					Color = "0 0 0 0.9",
					Material = "assets/content/ui/uibackgroundblur.mat"
				},
				CursorEnabled = true
			}, _config.UI.DisplayType, Layer);

			getMainBg.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text = {Text = ""},
				Button =
				{
					Color = "0 0 0 0",
					Close = Layer,
					Command = "UI_Shop closeui"
				}
			}, Layer);

			getMainBg.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = $"-{_config.UI.Width / 2f} -{_config.UI.Height / 2f}",
					OffsetMax = $"{_config.UI.Width / 2f} {_config.UI.Height / 2f}"
				},
				Image =
				{
					Color = _thirdColor
				}
			}, Layer, Layer + ".Background");
		}

		private void LoadMainPanel()
		{
			_mainPanel = new CuiElement
			{
				Name = Layer + ".Main",
				Parent = Layer + ".Background",
				Components =
				{
					new CuiImageComponent
					{
						Color = "0 0 0 0"
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5"
					}
				}
			};
		}

		#endregion

		#region Search

		private readonly Dictionary<string, HashSet<SearchInfo>> _searchCache =
			new Dictionary<string, HashSet<SearchInfo>>();

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
			HashSet<SearchInfo> searchInfo;
			if (_searchCache.TryGetValue(search, out searchInfo))
				return searchInfo.Where(x => string.IsNullOrEmpty(x.Permission) || player.HasPermission(x.Permission))
					.Select(x => x.Item);

			var shop = GetShop(player);

			var items = new List<ShopItem>();
			shop.Categories.ForEach(category =>
			{
				category.Items.ForEach(item =>
				{
					if (item.PublicTitle.StartsWith(search) || item.PublicTitle.Contains(search) ||
					    item.ShortName.StartsWith(search) || item.ShortName.Contains(search))
					{
						items.Add(item);

						if (_searchCache.ContainsKey(search))
							_searchCache[search].Add(new SearchInfo(category.Permission, item));
						else
							_searchCache.Add(search, new HashSet<SearchInfo>
							{
								new SearchInfo(category.Permission, item)
							});
					}
				});
			});

			return items;
		}

		#endregion

		#region Fields

		private readonly Dictionary<BasePlayer, OpenedShop> _openSHOP = new Dictionary<BasePlayer, OpenedShop>();

		private class OpenedShop
		{
			public List<ShopCategory> Categories;

			public OpenedShop(BasePlayer player)
			{
				Categories = _instance.GetCategories(player, _instance.GetShopByPlayer(player));
			}
		}

		private OpenedShop GetShop(BasePlayer player)
		{
			OpenedShop shop;
			if (!_openSHOP.TryGetValue(player, out shop)) _openSHOP.Add(player, shop = new OpenedShop(player));

			return shop;
		}

		#endregion

		#endregion

		#region Convert

		#region Server Rewards

		private SRRewardData rewarddata;

		#region Data

		[ConsoleCommand("shop.convert.sr")]
		private void CmdConvertSR(ConsoleSystem.Arg arg)
		{
			if (!arg.IsAdmin) return;

			if (arg.HasArgs())
			{
				bool clear;
				if (bool.TryParse(arg.Args[0], out clear) && clear) _config.Shop.Clear();
			}

			LoadSRData();

			ConvertSRData();
		}

		private void LoadSRData()
		{
			try
			{
				rewarddata = Interface.Oxide.DataFileSystem.ReadObject<SRRewardData>("ServerRewards/reward_data");
			}
			catch
			{
				PrintWarning("No reward data found! Creating a new data file");
			}
		}

		private void ConvertSRData()
		{
			var totalCount = 0;

			ConvertingSRDataCommands(ref totalCount);

			ConvertingSRDataItems(ref totalCount);

			ConvertingSRDataKits(ref totalCount);

			SaveConfig();

			ItemsToDict();

			PrintWarning($"{totalCount} items successfully converted from ServerRewards to Shop!");
		}

		private void ConvertingSRDataCommands(ref int count)
		{
			if (rewarddata == null) return;

			var category = new ShopCategory
			{
				Enabled = true,
				Title = "Commands",
				Permission = string.Empty,
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
				_config.Shop.Add(category);
		}

		private void ConvertingSRDataItems(ref int count)
		{
			if (rewarddata == null) return;

			var noneCategory = new ShopCategory
			{
				Enabled = true,
				Title = "Items",
				Permission = string.Empty,
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
					category = _config.Shop.Find(x => x.Title == check.Value.category.ToString());

					if (category == null)
					{
						category = new ShopCategory
						{
							Enabled = true,
							Title = check.Value.category.ToString(),
							Permission = string.Empty,
							SortType = Configuration.SortType.None,
							Items = new List<ShopItem>()
						};

						_config.Shop.Add(category);
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
				_config.Shop.Add(noneCategory);
		}

		private void ConvertingSRDataKits(ref int count)
		{
			if (rewarddata == null) return;

			var category = new ShopCategory
			{
				Enabled = true,
				Title = "Kits",
				Permission = string.Empty,
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
				_config.Shop.Add(category);
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
			public Dictionary<string, RewardItem> items = new Dictionary<string, RewardItem>();
			public SortedDictionary<string, RewardKit> kits = new SortedDictionary<string, RewardKit>();
			public SortedDictionary<string, RewardCommand> commands = new SortedDictionary<string, RewardCommand>();

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
				public List<string> commands = new List<string>();
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

		#endregion
	}
}

#region Extension Methods

namespace Oxide.Plugins.ShopExtensionMethods
{
	[SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
	[SuppressMessage("ReSharper", "LoopCanBeConvertedToQuery")]
	public static class ExtensionMethods
	{
		internal static Permission p;

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
			using (var c = a.GetEnumerator())
			{
				while (c.MoveNext())
				{
					if (b == 0) return c.Current;
					b--;
				}
			}

			return default(T);
		}

		public static bool Exists<T>(this IEnumerable<T> a, Func<T, bool> b = null)
		{
			using (var c = a.GetEnumerator())
			{
				while (c.MoveNext())
					if (b == null || b(c.Current))
						return true;
			}

			return false;
		}

		public static float Min<T>(this IEnumerable<T> source, Func<T, float> selector)
		{
			float value;
			using (var e = source.GetEnumerator())
			{
				value = selector(e.Current);
				if (float.IsNaN(value)) return value;

				while (e.MoveNext())
				{
					var x = selector(e.Current);
					if (x < value)
						value = x;
					else if (float.IsNaN(x)) return x;
				}
			}

			return value;
		}

		public static float Max<TSource>(this IEnumerable<TSource> source, Func<TSource, float> selector)
		{
			float value;
			using (var e = source.GetEnumerator())
			{
				if (!e.MoveNext()) return 0;

				value = selector(e.Current);
				while (e.MoveNext())
				{
					var x = selector(e.Current);
					if (x > value) value = x;
				}
			}

			return value;
		}

		public static T FirstOrDefault<T>(this IEnumerable<T> a, Func<T, bool> b = null)
		{
			using (var c = a.GetEnumerator())
			{
				while (c.MoveNext())
					if (b == null || b(c.Current))
						return c.Current;
			}

			return default(T);
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
			using (var d = a.GetEnumerator())
			{
				while (d.MoveNext()) c.Add(b(d.Current));
			}

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
			using (var iterator = source.GetEnumerator())
			{
				for (var i = 0; i < count; i++)
					if (!iterator.MoveNext())
						break;

				while (iterator.MoveNext()) result.Add(iterator.Current.Key, iterator.Current.Value);
			}

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
			using (var e = a.GetEnumerator())
			{
				while (e.MoveNext()) d[b(e.Current)] = c(e.Current);
			}

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
			using (var c = a.GetEnumerator())
			{
				while (c.MoveNext())
				{
					var entity = c.Current as T;
					if (entity != null)
						b.Add(entity);
				}
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

		public static bool HasPermission(this string a, string b)
		{
			if (p == null) p = Interface.Oxide.GetLibrary<Permission>();
			return !string.IsNullOrEmpty(a) && p.UserHasPermission(a, b);
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
			return o != null && o.IsLoaded;
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
			using (var item = source.GetEnumerator())
			{
				while (item.MoveNext())
					using (var result = selector(item.Current).GetEnumerator())
					{
						while (result.MoveNext()) yield return result.Current;
					}
			}
		}

		public static int Sum<TSource>(this IEnumerable<TSource> source, Func<TSource, int> selector)
		{
			var sum = 0;

			using (var element = source.GetEnumerator())
			{
				while (element.MoveNext()) sum += selector(element.Current);
			}

			return sum;
		}

		public static double Sum<TSource>(this IEnumerable<TSource> source, Func<TSource, double> selector)
		{
			var sum = 0.0;

			using (var element = source.GetEnumerator())
			{
				while (element.MoveNext()) sum += selector(element.Current);
			}

			return sum;
		}

		public static bool Any<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
		{
			if (source == null) return false;

			using (var element = source.GetEnumerator())
			{
				while (element.MoveNext())
					if (predicate(element.Current))
						return true;
			}

			return false;
		}
	}
}

#endregion Extension Methods
/* Boosty - https://boosty.to/skulidropek 
Discord - https://discord.gg/k3hXsVua7Q 
Discord The Rust Bay - https://discord.gg/Zq3TVjxKWk  */