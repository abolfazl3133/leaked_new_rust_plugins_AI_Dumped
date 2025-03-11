// #define TESTING

#if TESTING
using System.Diagnostics;
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Plugins.CraftsExtensionMethods;
using Rust;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

#if CARBON
using Carbon.Base;
using Carbon.Modules;
#endif

namespace Oxide.Plugins
{
	[Info("Crafts", "Mevent", "2.10.18")]
	public class Crafts : RustPlugin
	{
		#region Fields

		[PluginReference] private Plugin
			ImageLibrary = null,
			SpawnModularCar = null,
			Notify = null,
			UINotify = null,
			LangAPI = null;

		private static Crafts _instance;

#if CARBON
		private ImageDatabaseModule imageDatabase;
#endif

		private bool _enabledImageLibrary;

		private const bool LangRu = false;
		
		private const string
			Layer = "UI.Crafts",
			EditLayer = "UI.Crafts.Edit",
			PermEdit = "crafts.edit",
			PermSetWb = "crafts.setworkbench",
			PermBypass = "crafts.bypass";

		private List<CraftInfo> _crafts = new();

		private readonly ListHashSet<RecyclerComponent> _recyclers = new();

		private readonly ListHashSet<CarController> _cars = new();

		private readonly List<BasePlayer> _openedUi = new();

		private readonly Dictionary<string, List<KeyValuePair<int, string>>> _itemsCategories = new();

		private readonly ListHashSet<SafeZoneComponent> _safeZones = new();

		private enum WorkbenchLevel
		{
			None = 0,
			One = 1,
			Two = 2,
			Three = 3
		}

		private enum CraftType
		{
			Command,
			Vehicle,
			Item,
			Recycler,
			ModularCar
		}

		private readonly Dictionary<int, ItemForCraft> _itemsById = new();

		private readonly Dictionary<int, CraftInfo> _craftsById = new();

		private readonly Dictionary<BasePlayer, Dictionary<string, object>> _craftEditing = new();

		private readonly Dictionary<BasePlayer, Dictionary<string, object>> _itemEditing = new();

		private readonly Dictionary<ulong, CustomWorkbenchConf> _openedCustomWb = new();

		#endregion

		#region Config

		private static Configuration _config;

		private class Configuration
		{
			[JsonProperty(PropertyName = LangRu ? "Команды" : "Commands",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] Commands = {"craft", "crafts"};

			[JsonProperty(PropertyName =
				LangRu ? "Закрывать UI при повторном использовании команды?" : "Close UI when reusing a command?")]
			public bool CloseReusing = false;

			[JsonProperty(PropertyName = LangRu ? "Включить работу с Notify?" : "Work with Notify?")]
			public bool UseNotify = true;

			[JsonProperty(PropertyName = LangRu ? "Включить работу с LangAPI?" : "Work with LangAPI?")]
			public bool UseLangAPI = true;

			[JsonProperty(PropertyName = LangRu ? "Разрешение (пример: crafts.use)" : "Permission (ex: crafts.use)")]
			public string Permission = string.Empty;

			[JsonProperty(PropertyName = LangRu ? "Экономика" : "Economy")]
			public EconomyConf Economy = new EconomyConf
			{
				Show = false,
				Type = EconomyConf.EconomyType.Plugin,
				AddHook = "Deposit",
				BalanceHook = "Balance",
				RemoveHook = "Withdraw",
				Plug = "Economics",
				ShortName = "scrap",
				DisplayName = string.Empty,
				Skin = 0
			};

			[JsonProperty(PropertyName = "Categories", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<Category> Categories = new List<Category>
			{
				new Category
				{
					Enabled = true,
					Permission = string.Empty,
					Title = "Vehicles",
					Color = IColor.Create("#161617", 100),
					Crafts = new List<CraftInfo>
					{
						new CraftInfo.CraftInfoBuilder()
							.SetEnabled(true)
							.SetImage("https://i.ibb.co/mqYTq4P/image.png")
							.SetTitle("Minicopter")
							.SetDescription("Fast air transport")
							.SetCmdToGive("givecopter")
							.SetPermission("crafts.all")
							.SetCraftableItem("electric.flasherlight", 2080145158, 1, "Minicopter")
							.SetType(CraftType.Vehicle)
							.SetPrefab("assets/content/vehicles/minicopter/minicopter.entity.prefab")
							.SetCustomizeRotation(true)
							.SetReturnItem(true)
							.SetWorkbenchLevel(WorkbenchLevel.One)
							.SetCraftMenuDescription(new Localization()
							{
								Enabled = false,
								Messages = new Dictionary<string, string>
								{
									["en"] = "Fast air transport",
									["fr"] = "Transport aérien rapide"
								}
							})
							.Build(),
						new CraftInfo.CraftInfoBuilder()
							.SetEnabled(true)
							.SetImage("https://i.ibb.co/tmsNRcB/newkopter.png")
							.SetTitle("Attack Helicopter")
							.SetDescription("Fast air transport")
							.SetCmdToGive("giveattackhelicopter")
							.SetPermission("crafts.all")
							.SetCraftableItem("electric.flasherlight", 3043024177, 1, "Attack Helicopter")
							.SetType(CraftType.Vehicle)
							.SetPrefab("assets/content/vehicles/attackhelicopter/attackhelicopter.entity.prefab")
							.SetCustomizeRotation(true)
							.SetReturnItem(true)
							.SetWorkbenchLevel(WorkbenchLevel.Two)
							.SetCraftMenuDescription(new Localization()
							{
								Enabled = false,
								Messages = new Dictionary<string, string>
								{
									["en"] = "Fast air transport",
									["fr"] = "Transport aérien rapide"
								}
							})
							.Build(),
						new CraftInfo.CraftInfoBuilder()
							.SetEnabled(true)
							.SetImage("https://i.ibb.co/LJdqQ6z/image.png")
							.SetTitle("Row Boat")
							.SetDescription("Slow water transport")
							.SetCmdToGive("giverowboat")
							.SetPermission("crafts.all")
							.SetCraftableItem("innertube", 2080150023, 1, "Row Boat")
							.SetType(CraftType.Vehicle)
							.SetPrefab("assets/content/vehicles/boats/rowboat/rowboat.prefab")
							.SetCustomizeRotation(true)
							.SetReturnItem(true)
							.SetWorkbenchLevel(WorkbenchLevel.Two)
							.SetCraftMenuDescription(new Localization()
							{
								Enabled = false,
								Messages = new Dictionary<string, string>
								{
									["en"] = "Slow water transport",
									["fr"] = "Transport lent de l'eau"
								}
							})
							.Build(),
						new CraftInfo.CraftInfoBuilder()
							.SetEnabled(true)
							.SetImage("https://i.ibb.co/HpkTsBZ/image.png")
							.SetTitle("RHIB")
							.SetDescription("Slow water transport")
							.SetCmdToGive("giverhibboat")
							.SetPermission("crafts.all")
							.SetCraftableItem("innertube", 2080150770, 1, "RHIB")
							.SetType(CraftType.Vehicle)
							.SetPrefab("assets/content/vehicles/boats/rhib/rhib.prefab")
							.SetCustomizeRotation(true)
							.SetReturnItem(true)
							.SetWorkbenchLevel(WorkbenchLevel.Three)
							.SetCraftMenuDescription(new Localization()
							{
								Enabled = false,
								Messages = new Dictionary<string, string>
								{
									["en"] = "Slow water transport",
									["fr"] = "Transport lent de l'eau"
								}
							})
							.Build(),
						new CraftInfo.CraftInfoBuilder()
							.SetEnabled(true)
							.SetImage("https://i.ibb.co/gr64fDx/image.png")
							.SetTitle("Hot Air Balloon")
							.SetDescription("Slow air transport")
							.SetCmdToGive("givehotair")
							.SetPermission("crafts.all")
							.SetCraftableItem("box.repair.bench", 2080152635, 1, "Hot Air Balloon")
							.SetType(CraftType.Vehicle)
							.SetPrefab("assets/prefabs/deployable/hot air balloon/hotairballoon.prefab")
							.SetCustomizeRotation(true)
							.SetReturnItem(true)
							.SetWorkbenchLevel(WorkbenchLevel.Three)
							.SetCraftMenuDescription(new Localization()
							{
								Enabled = false,
								Messages = new Dictionary<string, string>
								{
									["en"] = "Slow air transport",
									["fr"] = "Lenteur du transport aérien"
								}
							})
							.Build(),
						new CraftInfo.CraftInfoBuilder()
							.SetEnabled(true)
							.SetImage("https://i.ibb.co/nzXCNws/image.png")
							.SetTitle("Transport Helicopter")
							.SetDescription("Fast air transport")
							.SetCmdToGive("givescrapheli")
							.SetPermission("crafts.all")
							.SetCraftableItem("electric.flasherlight", 2080154394, 1, "Transport Helicopter")
							.SetType(CraftType.Vehicle)
							.SetPrefab("assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab")
							.SetCustomizeRotation(true)
							.SetReturnItem(true)
							.SetWorkbenchLevel(WorkbenchLevel.Three)
							.SetCraftMenuDescription(new Localization()
							{
								Enabled = false,
								Messages = new Dictionary<string, string>
								{
									["en"] = "Fast air transport",
									["fr"] = "Transport aérien rapide"
								}
							})
							.Build(),
						new CraftInfo.CraftInfoBuilder()
							.SetEnabled(true)
							.SetImage("https://i.ibb.co/gdcVNwr/image.png")
							.SetTitle("Snowmobile")
							.SetDescription("Conquers snow biomes")
							.SetCmdToGive("givesnowmobile")
							.SetPermission("crafts.all")
							.SetCraftableItem("electric.flasherlight", 2747934628, 1, "Snowmobile")
							.SetType(CraftType.Vehicle)
							.SetPrefab("assets/content/vehicles/snowmobiles/snowmobile.prefab")
							.SetCustomizeRotation(true)
							.SetReturnItem(true)
							.SetWorkbenchLevel(WorkbenchLevel.Two)
							.SetCraftMenuDescription(new Localization()
							{
								Enabled = false,
								Messages = new Dictionary<string, string>
								{
									["en"] = "Conquers snow biomes",
									["fr"] = "À la conquête des biomes enneigés"
								}
							})
							.Build(),
						new CraftInfo.CraftInfoBuilder()
							.SetEnabled(true)
							.SetImage("https://i.ibb.co/jWC9Mrd/image.png")
							.SetTitle("Submarine Solo")
							.SetDescription("Fast underwater transport")
							.SetCmdToGive("givesolosubmarine")
							.SetPermission("crafts.all")
							.SetCraftableItem("barricade.sandbags", 3043043385, 1, "Submarine Solo")
							.SetType(CraftType.Vehicle)
							.SetPrefab("assets/content/vehicles/submarine/submarinesolo.entity.prefab")
							.SetCustomizeRotation(true)
							.SetReturnItem(true)
							.SetWorkbenchLevel(WorkbenchLevel.Three)
							.SetCraftMenuDescription(new Localization()
							{
								Enabled = false,
								Messages = new Dictionary<string, string>
								{
									["en"] = "Fast underwater transport",
									["fr"] = "Transport subaquatique rapide"
								}
							})
							.Build(),

						new CraftInfo.CraftInfoBuilder()
							.SetEnabled(true)
							.SetImage("https://i.ibb.co/qYNQhTJ/image.png")
							.SetTitle("Submarine Duo")
							.SetDescription("Fast underwater transport")
							.SetCmdToGive("giveduosubmarine")
							.SetPermission("crafts.all")
							.SetCraftableItem("barricade.sandbags", 3043043217, 1, "Submarine Duo")
							.SetType(CraftType.Vehicle)
							.SetPrefab("assets/content/vehicles/submarine/submarineduo.entity.prefab")
							.SetCustomizeRotation(true)
							.SetReturnItem(true)
							.SetWorkbenchLevel(WorkbenchLevel.Three)
							.SetCraftMenuDescription(new Localization()
							{
								Enabled = false,
								Messages = new Dictionary<string, string>
								{
									["en"] = "Fast underwater transport",
									["fr"] = "Transport subaquatique rapide"
								}
							})
							.Build(),

						new CraftInfo.CraftInfoBuilder()
							.SetEnabled(true)
							.SetImage("https://i.ibb.co/80hKRcm/tugboat.png")
							.SetTitle("Tugboat")
							.SetDescription("Slow water transport")
							.SetCmdToGive("givetugboat")
							.SetPermission("crafts.all")
							.SetCraftableItem("innertube", 3043043217, 1, "Tugboat")
							.SetType(CraftType.Vehicle)
							.SetPrefab("assets/content/vehicles/boats/tugboat/tugboat.prefab")
							.SetCustomizeRotation(true)
							.SetReturnItem(true)
							.SetWorkbenchLevel(WorkbenchLevel.Three)
							.SetCraftMenuDescription(new Localization()
							{
								Enabled = false,
								Messages = new Dictionary<string, string>
								{
									["en"] = "Slow water transport",
									["fr"] = "Transport lent de l'eau"
								}
							})
							.Build(),
						
						new CraftInfo.CraftInfoBuilder()
							.SetEnabled(true)
							.SetImage("https://i.ibb.co/hggGgFJ/horse.png")
							.SetTitle("Horse")
							.SetDescription("5KM/H")
							.SetCmdToGive("givehorse")
							.SetPermission("crafts.all")
							.SetCraftableItem("barricade.sandbags", 3258220653, 1, "Horse")
							.SetType(CraftType.Vehicle)
							.SetPrefab("assets/rust.ai/nextai/testridablehorse.prefab")
							.SetCustomizeRotation(true)
							.SetReturnItem(true)
							.SetWorkbenchLevel(WorkbenchLevel.Three)
							.SetCraftMenuDescription(new Localization()
							{
								Enabled = false,
								Messages = new Dictionary<string, string>
								{
									["en"] = "5KM/H",
									["fr"] = "5KM/H"
								}
							})
							.Build(),
					}
				},
				new Category
				{
					Enabled = true,
					Permission = string.Empty,
					Title = "Cars",
					Color = IColor.Create("#161617", 100),
					Crafts = new List<CraftInfo>
					{
						new CraftInfo.CraftInfoBuilder()
							.SetEnabled(true)
							.SetImage("https://i.ibb.co/MG4N1cs/image.png")
							.SetTitle("Sedan")
							.SetDescription("5KM/H")
							.SetCmdToGive("givesedan")
							.SetPermission("crafts.all")
							.SetCraftableItem("woodcross", 2080151780, 1, "Sedan")
							.SetType(CraftType.Vehicle)
							.SetPrefab("assets/content/vehicles/sedan_a/sedantest.entity.prefab")
							.SetCustomizeRotation(true)
							.SetReturnItem(true)
							.SetWorkbenchLevel(WorkbenchLevel.Two)
							.SetCraftMenuDescription(new Localization()
							{
								Enabled = false,
								Messages = new Dictionary<string, string>
								{
									["en"] = "5KM/H",
									["fr"] = "5KM/H"
								}
							})
							.Build(),
						new CraftInfo.CraftInfoBuilder()
							.SetEnabled(true)
							.SetImage("https://i.ibb.co/MG4N1cs/image.png")
							.SetTitle("Ferrari")
							.SetDescription("25KM/H")
							.SetCmdToGive("givemod1")
							.SetPermission("crafts.all")
							.SetCraftableItem("electric.flasherlight", 2244308598, 1, "Ferrari")
							.SetType(CraftType.ModularCar)
							.SetWorkbenchLevel(WorkbenchLevel.Two)
							.SetModular(new ModularCarConf()
							{
								CodeLock = true,
								KeyLock = false,
								EnginePartsTier = 2,
								FreshWaterAmount = 0,
								FuelAmount = 140,
								Modules = new[]
								{
									"vehicle.1mod.engine",
									"vehicle.1mod.cockpit.armored",
									"vehicle.1mod.cockpit.armored"
								}
							})
							.SetCraftMenuDescription(new Localization()
							{
								Enabled = false,
								Messages = new Dictionary<string, string>
								{
									["en"] = "25KM/H",
									["fr"] = "25KM/H"
								}
							})
							.Build()
					}
				},
				new Category
				{
					Enabled = true,
					Permission = string.Empty,
					Title = "Misc",
					Color = IColor.Create("#161617", 100),
					Crafts = new List<CraftInfo>
					{
						new CraftInfo.CraftInfoBuilder()
							.SetEnabled(true)
							.SetImage("https://i.ibb.co/mHRkk5P/image.png")
							.SetTitle("Home Recycler")
							.SetCmdToGive("giverecycler")
							.SetPermission("crafts.all")
							.SetCraftableItem("research.table", 2186833264, 1, "Home Recycler")
							.SetType(CraftType.Recycler)
							.SetPrefab("assets/bundled/prefabs/static/recycler_static.prefab")
							.SetReturnItem(true)
							.SetWorkbenchLevel(WorkbenchLevel.Two)
							.Build(),
						new CraftInfo.CraftInfoBuilder()
							.SetEnabled(true)
							.SetImage("https://i.ibb.co/024076x/image.png")
							.SetCmdToGive("givelr300")
							.SetPermission("crafts.all")
							.SetCraftableItem("rifle.lr300")
							.SetType(CraftType.Item)
							.SetWorkbenchLevel(WorkbenchLevel.None)
							.Build()
					}
				}
			};

			[JsonProperty(PropertyName = LangRu ? "Настройка Верстаков" : "Workbenches Setting",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<WorkbenchLevel, IColor> Workbenches =
				new Dictionary<WorkbenchLevel, IColor>
				{
					[WorkbenchLevel.None] = IColor.Create("#FFFFFF", 00),
					[WorkbenchLevel.One] = IColor.Create("#74884A", 100),
					[WorkbenchLevel.Two] = IColor.Create("#B19F56", 100),
					[WorkbenchLevel.Three] = IColor.Create("#B43D3D", 100)
				};

			[JsonProperty(PropertyName = LangRu ? "Настройка Переработчика" : "Recycler Settings")]
			public RecyclerConfig Recycler = new RecyclerConfig
			{
				Enabled = true,
				UseSpeed = true,
				Speed = 5f,
				Radius = 7.5f,
				Text = "<size=19>RECYCLER</size>\n<size=15>{0}/{1}</size>",
				Color = "#C5D0E6",
				Delay = 0.75f,
				Available = true,
				Owner = true,
				Amounts = new[]
					{0.9f, 0, 0, 0, 0, 0.5f, 0, 0, 0, 0.9f, 0.5f, 0.5f, 0, 1, 1, 0.5f, 0, 0, 0, 0, 0, 1, 1},
				Scale = 0.5f,
				DDraw = true,
				Building = true,
				Destroy = new RecyclerDestroy
				{
					CheckGround = true,
					Item = true,
					Effects = new List<string>
					{
						"assets/bundled/prefabs/fx/item_break.prefab",
						"assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
					}
				}
			};

			[JsonProperty(PropertyName = LangRu ? "Настройка Машины" : "Car Settings")]
			public CarConfig Car = new CarConfig
			{
				ActiveItems = new ActiveItemOptions
				{
					Disable = true,
					BlackList = new[]
					{
						"explosive.timed", "rocket.launcher", "surveycharge", "explosive.satchel"
					}
				},
				Radius = 7.5f,
				Text = "<size=15>{0}/{1}</size>",
				Color = "#C5D0E6",
				Delay = 0.75f
			};

			[JsonProperty(PropertyName = LangRu
					? "Кастомные верстаки (Entity ID - настройки)"
					: "Custom Workbench Stations (Entity ID - settings)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<ulong, CustomWorkbenchConf> CustomWorkbench =
				new Dictionary<ulong, CustomWorkbenchConf>
				{
					[123343941] = new CustomWorkbenchConf
					{
						Categories = new List<string>
						{
							"Cars", "Misc"
						},
						SafeZone = true,
						SafeZoneRadius = 5,
						WorkBench = new Dictionary<string, int>
						{
							["crafts.default"] = 0,
							["crafts.premium"] = 3
						}
					}
				};

			[JsonProperty(PropertyName = LangRu ? "Настройка UI" : "UI Settings")]
			public UserInterface UI = new UserInterface
			{
				CatWidth = 90,
				CatMargin = 5,
				CatHeight = 25,
				Craft = new UserInterface.CraftItem
				{
					Width = 125,
					Height = 125,
					Margin = 10,
					IndentY = -115,
					AmountOnString = 5,
					MaxLines = 2,
					Title = new TextSettings()
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "0 -105", OffsetMax = "0 -80",
						Align = TextAnchor.MiddleCenter,
						IsBold = true,
						FontSize = 14,
						Color = IColor.Create("#FFFFFF")
					},
					Description = new TextSettings()
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "0 0", OffsetMax = "0 -100",
						Align = TextAnchor.UpperCenter,
						IsBold = false,
						FontSize = 10,
						Color = IColor.Create("#FFFFFF", 50)
					},
					Image = new InterfacePosition()
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = "-36 -84", OffsetMax = "36 -12"
					},
					WorkbenchLevelLine = new InterfacePosition()
					{
						AnchorMin = "0 0", AnchorMax = "1 0",
						OffsetMin = "0 0", OffsetMax = "0 2"
					}
				},
				CategoriesAmountOnPage = 7,
				PageSize = 25,
				PageSelectedSize = 40,
				PagesMargin = 5,
				Color1 = IColor.Create("#0E0E10", 100),
				Color2 = IColor.Create("#161617", 100),
				Color3 = IColor.Create("#FFFFFF", 100),
				Color4 = IColor.Create("#4B68FF", 100),
				Color5 = IColor.Create("#74884A", 100),
				Color6 = IColor.Create("#CD4632", 100),
				Color7 = IColor.Create("#595651", 100),
				Color8 = IColor.Create("#4B68FF", 70),
				Color9 = IColor.Create("#0E0E10", 98),
				Color10 = IColor.Create("#4B68FF", 50),
				Color11 = IColor.Create("#FF4B4B", 100),
				BackgroundImage = string.Empty,
				CloseAfterCraft = true,
				CraftMenuSettings = new UserInterface.CraftMenu
				{
					ItemWidth = 40,
					ItemHeight = 165,
					ItemMargin = 105,
					MaxItemsOnString = 7,
					ItemUpIndent = 285,
					Description = new PanelSettings
					{
						AnchorMin = "0.5 0",
						AnchorMax = "0.5 0",
						OffsetMin = "-200 50",
						OffsetMax = "200 130",
						FontSize = 14,
						Align = TextAnchor.MiddleCenter,
						Color = IColor.Create("#FFFFFF", 100),
						Font = "robotocondensed-bold.ttf",
						BackgroundColor = IColor.Create("#0E0E10", 100)
					}
				}
			};

			public VersionNumber Version;
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

		#endregion Configuration.UI

		private class Localization
		{
			[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = LangRu ? "Текст (язык - текст)" : "Text (language - text)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, string> Messages = new Dictionary<string, string>();

			public string GetMessage(BasePlayer player = null)
			{
				if (Messages.Count == 0)
					throw new Exception("The use of localization is enabled, but there are no messages!");

				var userLang = "en";
				if (player != null) userLang = _instance.lang.GetLanguage(player.UserIDString);

				string message;
				if (Messages.TryGetValue(userLang, out message))
					return message;

				if (Messages.TryGetValue("en", out message))
					return message;

				return Messages.ElementAt(0).Value;
			}
		}

		private class EconomyConf
		{
			public enum EconomyType
			{
				Plugin,
				Item
			}

			[JsonProperty(PropertyName = LangRu ? "Показывать Баланс" : "Show Balance")]
			public bool Show;

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

			[JsonProperty(PropertyName = "Skin")] public ulong Skin;

			public double ShowBalance(BasePlayer player)
			{
				switch (Type)
				{
					case EconomyType.Plugin:
					{
						var plugin = _instance?.plugins?.Find(Plug);
						if (plugin == null) return 0;

						return Math.Round(Convert.ToDouble(plugin.Call(BalanceHook, player.UserIDString)), 2);
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

		private class CustomWorkbenchConf
		{
			[JsonProperty(PropertyName = LangRu ? "Категории (Названия) [* - все]" : "Categories (Titles) [* - all]",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> Categories;

			[JsonProperty(PropertyName = LangRu ? "Включать safe zone" : "Use safe zone")]
			public bool SafeZone;

			[JsonProperty(PropertyName = LangRu ? "Радиус Safe Zone" : "Safe Zone Radius")]
			public float SafeZoneRadius;

			[JsonProperty(PropertyName = LangRu ? "Уровень Верстака" : "WorkBench Level",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, int> WorkBench = new Dictionary<string, int>
			{
				["crafts.default"] = 0,
				["crafts.premium"] = 0
			};

			public WorkbenchLevel GetWorkBenchLevel(BasePlayer player)
			{
				var result = 0;

				foreach (var check in WorkBench)
					if (!string.IsNullOrEmpty(check.Key) &&
					    _instance.permission.UserHasPermission(
						    player.UserIDString, check.Key)
					    && check.Value > result)
						result = check.Value;

				return (WorkbenchLevel) result;
			}
		}

		private class UserInterface
		{
			[JsonProperty(PropertyName = LangRu ? "Ширина категории" : "Category Width")]
			public float CatWidth;

			[JsonProperty(PropertyName = LangRu ? "Высота категории" : "Category Height")]
			public float CatHeight;

			[JsonProperty(PropertyName = LangRu ? "Отступ между категориями" : "Category Margin")]
			public float CatMargin;

			[JsonProperty(PropertyName = LangRu ? "Кол-во категорий на странице" : "Categories Amount On Page")]
			public int CategoriesAmountOnPage;

			[JsonProperty(PropertyName = LangRu ? "Крафт" : "Craft Item")]
			public CraftItem Craft;

			[JsonProperty(PropertyName = LangRu ? "Размер страницы" : "Page Size")]
			public float PageSize;

			[JsonProperty(PropertyName = LangRu ? "Размер выделенной страницы" : "Page Selected Size")]
			public float PageSelectedSize;

			[JsonProperty(PropertyName = LangRu ? "Отступ между страницами" : "Pages Margin")]
			public float PagesMargin;

			[JsonProperty(PropertyName = LangRu ? "Цвет 1" : "Color 1")]
			public IColor Color1;

			[JsonProperty(PropertyName = LangRu ? "Цвет 2" : "Color 2")]
			public IColor Color2;

			[JsonProperty(PropertyName = LangRu ? "Цвет 3" : "Color 3")]
			public IColor Color3;

			[JsonProperty(PropertyName = LangRu ? "Цвет 4" : "Color 4")]
			public IColor Color4;

			[JsonProperty(PropertyName = LangRu ? "Цвет 5" : "Color 5")]
			public IColor Color5;

			[JsonProperty(PropertyName = LangRu ? "Цвет 6" : "Color 6")]
			public IColor Color6;

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

			[JsonProperty(PropertyName = LangRu ? "Фоновое изображение" : "Background Image")]
			public string BackgroundImage;

			[JsonProperty(PropertyName = LangRu ? "Закрывать после крафта?" : "Close after craft?")]
			public bool CloseAfterCraft;

			[JsonProperty(PropertyName = LangRu ? "Меню крафта" : "Craft Menu")]
			public CraftMenu CraftMenuSettings;

			public class CraftMenu
			{
				[JsonProperty(PropertyName = LangRu ? "Ширина предмета" : "Item Width")]
				public float ItemWidth;

				[JsonProperty(PropertyName = LangRu ? "Высота предмета" : "Item Height")]
				public float ItemHeight;

				[JsonProperty(PropertyName = LangRu ? "Отступ между предметами" : "Item Margin")]
				public float ItemMargin;

				[JsonProperty(PropertyName =
					LangRu ? "Максимальное кол-во предметов на строке" : "Max Items On String")]
				public int MaxItemsOnString;

				[JsonProperty(PropertyName = LangRu ? "Верхний отступ для предметов" : "Item Up Indent")]
				public float ItemUpIndent;

				[JsonProperty(PropertyName = LangRu ? "Описание" : "Description")]
				public PanelSettings Description;
			}

			public class CraftItem
			{
				[JsonProperty(PropertyName = LangRu ? "Ширина" : "Width")]
				public float Width;

				[JsonProperty(PropertyName = LangRu ? "Высота" : "Height")]
				public float Height;

				[JsonProperty(PropertyName = LangRu ? "Отступ между" : "Margin")]
				public float Margin;

				[JsonProperty(PropertyName = LangRu ? "Отступ сверху" : "Y Indent")]
				public float IndentY;

				[JsonProperty(PropertyName = LangRu ? "Кол-во на строке" : "Amount On String")]
				public int AmountOnString;

				[JsonProperty(PropertyName = LangRu ? "Максимальное количество строк" : "Max Lines")]
				public int MaxLines;

				[JsonProperty(PropertyName = LangRu ? "Название" : "Title")]
				public TextSettings Title;

				[JsonProperty(PropertyName = LangRu ? "Описание" : "Description")]
				public TextSettings Description;

				[JsonProperty(PropertyName = LangRu ? "Изображение" : "Image")]
				public InterfacePosition Image;

				[JsonProperty(PropertyName = LangRu ? "Линия уровня верстака" : "Workbench Level Line")]
				public InterfacePosition WorkbenchLevelLine;
			}
		}

		private class LabelSettings : InterfacePosition
		{
			[JsonProperty(PropertyName = "FontSize")]
			public int FontSize;

			[JsonProperty(PropertyName = "Align")] [JsonConverter(typeof(StringEnumConverter))]
			public TextAnchor Align;

			[JsonProperty(PropertyName = "Color")] public IColor Color;

			[JsonProperty(PropertyName = "Font")] public string Font;
		}

		private class PanelSettings : LabelSettings
		{
			[JsonProperty(PropertyName = "Background Color")]
			public IColor BackgroundColor;
		}

		private class Category
		{
			[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = LangRu ? "Разрешение (пример: crafts.vip)" : "Permission (ex: crafts.vip)")]
			public string Permission;

			[JsonProperty(PropertyName = LangRu ? "Название" : "Title")]
			public string Title;

			[JsonProperty(PropertyName = LangRu ? "Фоновый цвет" : "Background color")]
			public IColor Color;

			[JsonProperty(PropertyName = LangRu ? "Предметы" : "Items",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<CraftInfo> Crafts;
		}

		private class CraftInfo
		{
			#region Fields

			[JsonProperty(PropertyName = "ID")] public int ID;

			[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = LangRu ? "Изображение" : "Image")]
			public string Image;

			[JsonProperty(PropertyName = LangRu ? "Название" : "Title")]
			public string Title;

			[JsonProperty(PropertyName = LangRu ? "Описание" : "Description")]
			public string Description;

			[JsonProperty(PropertyName = LangRu ? "Команда (для выдачи предмета)" : "Command (to give an item)")]
			public string CmdToGive;

			[JsonProperty(PropertyName = LangRu ? "Разрешение (пример: crafts.vip)" : "Permission (ex: crafts.vip)")]
			public string Permission;

			[JsonProperty(PropertyName = LangRu ? "Отображаемое имя" : "Display Name")]
			public string DisplayName;

			[JsonProperty(PropertyName = LangRu ? "Shortname" : "ShortName")]
			public string ShortName;

			[JsonProperty(PropertyName = LangRu ? "Количество" : "Amount")]
			public int Amount;

			[JsonProperty(PropertyName = LangRu ? "Скин" : "Skin")]
			public ulong SkinID;

			[JsonProperty(PropertyName =
				LangRu ? "Тип (Item/Command/Vehicle/Recycler)" : "Type (Item/Command/Vehicle/Recycler)")]
			[JsonConverter(typeof(StringEnumConverter))]
			public CraftType Type;

			[JsonProperty(PropertyName = LangRu ? "Префаб" : "Prefab")]
			public string Prefab;

			[JsonProperty(PropertyName = LangRu ? "Команда при получении" : "Command on give")]
			public string GiveCommand;

			[JsonProperty(PropertyName = LangRu ? "Уровень верстака" : "Workbench Level")]
			public WorkbenchLevel Level;

			[JsonProperty(PropertyName = LangRu ? "Проверять дистацию" : "Distance Check")]
			public bool UseDistance;

			[JsonProperty(PropertyName = LangRu ? "Дистанция" : "Distance")]
			public float Distance;

			[JsonProperty(PropertyName = LangRu ? "Установка на землю" : "Place the ground")]
			public bool Ground;

			[JsonProperty(PropertyName = LangRu ? "Установка на строение" : "Place the structure")]
			public bool Structure;

			[JsonProperty(PropertyName = LangRu ? "Предметы для крафта" : "Items For Craft",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<ItemForCraft> Items;

			[JsonProperty(PropertyName = LangRu ? "Modular Car" : "For Modular Car")]
			public ModularCarConf Modular;

			[JsonProperty(PropertyName = LangRu ? "Задержка" : "Cooldown")]
			public float Cooldown;

			[JsonProperty(PropertyName = LangRu ? "Множественный крафт" : "Multiple Craft")]
			public bool MultipleCraft;

			[JsonProperty(PropertyName = LangRu ? "Максимальное количество крафтов" : "Maximum number of crafts")]
			public int MaxAmount;

			[JsonProperty(PropertyName = LangRu ? "Включить Крафт?" : "Enable Craft?")]
			public bool Craft;

			[JsonProperty(PropertyName = LangRu ? "Включить Продажу?" : "Enable Sales?")]
			public bool Sale;

			[JsonProperty(PropertyName = LangRu ? "Стоимость" : "Cost")]
			public float Cost;

			[JsonProperty(PropertyName =
				LangRu ? "Установить OwnerID для дочерних контейнеров?" : "Set OwnerID for child containers?")]
			public bool SetOwnerID;

			[JsonProperty(PropertyName =
				LangRu ? "Необходимо настроить ротацию при спавне?" : "Need to customize the rotation when spawning?")]
			public bool CustomizeRotationOnSpawning;

			[JsonProperty(PropertyName =
				LangRu ? "Настройки описания (в меню крафта)" : "Description settings (in the craft menu)")]
			public Localization CraftMenuDescription;

			[JsonProperty(PropertyName =
				LangRu
					? "Должен ли предмет быть возвращен после его удаления? (с помощью плагина BuildTools)"
					: "Should the item be returned when it is removed? (using the BuildTools plugin)")]
			public bool ReturnItem;

			[JsonProperty(PropertyName = LangRu ? "Выдавая предмет игроку, разбивай его на стаки?" : "When giving an item to a player, split it into stacks?")]
			public bool SplitStacks;
			
			[JsonProperty(PropertyName = LangRu ? "Выдавать предмет одним стаком?" : "Give an item to a single stack?")]
			public bool SingleStack;
			
			#endregion Fields

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

			[JsonIgnore]
			public int PUBLIC_ID
			{
				get
				{
					while (ID == 0)
					{
						var val = Random.Range(int.MinValue, int.MaxValue);
						if (_instance._craftsById.ContainsKey(val)) continue;

						ID = val;
					}

					return ID;
				}
			}

			[JsonIgnore] private string _publicTitle;

			[JsonIgnore]
			public string PublicTitle
			{
				get
				{
					if (string.IsNullOrEmpty(_publicTitle))
						_publicTitle = GetItemName();

					return _publicTitle;
				}
			}

			#endregion Cache

			#region Helpers

			public void GiveCraftToPlayer(BasePlayer player)
			{
				switch (Type)
				{
					case CraftType.Command:
					{
						var command = GiveCommand.Replace("\n", "|")
							.Replace("%steamid%", player.UserIDString, StringComparison.OrdinalIgnoreCase).Replace(
								"%username%",
								player.displayName, StringComparison.OrdinalIgnoreCase);
						
						foreach (var check in command.Split('|')) _instance?.Server.Command(check);
						break;
					}

					default:
					{
						GiveItem(player);
						break;
					}
				}
			}
			
			#region Item

			public void GiveItem(BasePlayer player, int amount = 1)
			{
				if (player == null) return;

				var item = CreateItem(amount);
				if (item != null) player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
			}

			public Item CreateItem(int amount = 1)
			{
				var newItem = ItemManager.CreateByName(ShortName, Mathf.Max(Amount * amount, 1), SkinID);
				if (newItem == null)
				{
					Debug.LogError($"Error creating item with ShortName: '{ShortName}'");
					return null;
				}

				if (!string.IsNullOrEmpty(DisplayName)) newItem.name = DisplayName;

				return newItem;
			}

			public List<int> GetItemStacks(int amount)
			{
				amount *= Amount;

				var maxStack = ItemManager.FindItemDefinition(ShortName)?.stackable ?? 0;

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

			#endregion Item

			private string GetItemName()
			{
				if (!string.IsNullOrEmpty(Title))
					return Title;

				if (!string.IsNullOrEmpty(DisplayName))
					return DisplayName;

				var def = ItemManager.FindItemDefinition(ShortName);
				return !string.IsNullOrEmpty(ShortName) && def != null ? def.displayName.translated : string.Empty;
			}

			#region Entity

			public void SpawnEntity(BasePlayer player, Vector3 pos, Quaternion rot)
			{
				BaseEntity entity;
				switch (Type)
				{
					case CraftType.ModularCar:
					{
						_instance?.SpawnModularCar?.Call("API_SpawnPresetCar", player, Modular.ToDictionary());
						break;
					}
					case CraftType.Vehicle:
					{
						entity = GameManager.server.CreateEntity(Prefab, pos,
							CustomizeRotationOnSpawning
								? Quaternion.Euler(0, player.GetNetworkRotation().eulerAngles.y - 90, 0)
								: rot);

						SpawnEntityCommon(player, entity);
						break;
					}
					default:
					{
						entity = GameManager.server.CreateEntity(Prefab, pos, rot);

						SpawnEntityCommon(player, entity);
						break;
					}
				}
			}

			private void SpawnEntityCommon(BasePlayer player, BaseEntity entity)
			{
				if (entity == null) return;

				if (player.GetParentEntity() is Tugboat tugboat) 
					entity.SetParent(tugboat, true);
				
				entity.skinID = SkinID;
				entity.OwnerID = player.userID;
				entity.Spawn();

				if (SetOwnerID)
					foreach (var container in entity.GetComponentsInChildren<StorageContainer>())
						container.OwnerID = player.userID;
			}

			#endregion

			#region API

			public Dictionary<string, object> ToDictionary()
			{
				return new Dictionary<string, object>
				{
					["Generated"] = false,
					["Enabled"] = Enabled,
					["Image"] = Image,
					["Title"] = Title,
					["Description"] = Description,
					["CmdToGive"] = CmdToGive,
					["Permission"] = Permission,
					["DisplayName"] = DisplayName,
					["ShortName"] = ShortName,
					["Amount"] = Amount,
					["SkinID"] = SkinID,
					["Type"] = Type,
					["Prefab"] = Prefab,
					["GiveCommand"] = GiveCommand,
					["Level"] = Level,
					["UseDistance"] = UseDistance,
					["Distance"] = Distance,
					["Ground"] = Ground,
					["Structure"] = Structure,
					["Items"] = Items,
					["Craft"] = Craft,
					["Sales"] = Sale,
					["Cost"] = Cost
				};
			}

			#endregion

			#region Cooldown

			public void UpdateCooldown(BasePlayer player)
			{
				PlayerCooldown.GetOrAdd(player.userID)?.Add(this);
			}

			public bool HasCooldown(BasePlayer player, out int timeLeft)
			{
				timeLeft = 0;

				if (Cooldown <= 0)
					return false;

				var data = PlayerCooldown.GetOrAdd(player.userID);
				if (data == null) return false;

				timeLeft = data.GetCooldown(this);
				return timeLeft > 0;
			}

			#endregion Cooldown

			#endregion Helpers

			#region Constructors

			#region Builder

			public class CraftInfoBuilder
			{
				private CraftInfo craftInfo = new CraftInfo
				{
					Enabled = false,
					Image = string.Empty,
					Title = string.Empty,
					Description = string.Empty,
					CmdToGive = string.Empty,
					Permission = string.Empty,
					DisplayName = string.Empty,
					ShortName = string.Empty,
					Amount = 0,
					SkinID = 0,
					Type = CraftType.Item,
					Prefab = string.Empty,
					GiveCommand = string.Empty,
					Level = WorkbenchLevel.None,
					UseDistance = true,
					Distance = 1.5f,
					Ground = true,
					Structure = true,
					Items = new List<ItemForCraft>
					{
						new ItemForCraft(string.Empty, "gears", 5, 0),
						new ItemForCraft(string.Empty, "roadsigns", 5, 0),
						new ItemForCraft(string.Empty, "metal.fragments", 2000, 0)
					},
					Modular = new ModularCarConf
					{
						CodeLock = true,
						KeyLock = false,
						EnginePartsTier = 2,
						FreshWaterAmount = 0,
						FuelAmount = 140,
						Modules = new[]
						{
							"vehicle.1mod.engine",
							"vehicle.1mod.cockpit.armored",
							"vehicle.1mod.cockpit.armored"
						}
					},
					Cooldown = 0,
					MultipleCraft = false,
					MaxAmount = 0,
					Craft = true,
					Sale = false,
					Cost = 100,
					SetOwnerID = false,
					CustomizeRotationOnSpawning = false,
					CraftMenuDescription = new Localization
					{
						Enabled = false,
						Messages = new Dictionary<string, string>
						{
							["en"] = string.Empty,
							["fr"] = string.Empty,
							["de"] = string.Empty,
							["ru"] = string.Empty
						}
					},
					ReturnItem = false,
					SplitStacks = false,
					SingleStack = false,
				};

				public CraftInfoBuilder SetEnabled(bool enabled)
				{
					craftInfo.Enabled = enabled;
					return this;
				}

				public CraftInfoBuilder SetType(CraftType type)
				{
					craftInfo.Type = type;
					return this;
				}

				public CraftInfoBuilder SetImage(string image)
				{
					craftInfo.Image = image;
					return this;
				}

				public CraftInfoBuilder SetTitle(string title)
				{
					craftInfo.Title = title;
					return this;
				}

				public CraftInfoBuilder SetDescription(string description)
				{
					craftInfo.Description = description;
					return this;
				}

				public CraftInfoBuilder SetPrefab(string prefab)
				{
					craftInfo.Prefab = prefab;
					return this;
				}

				public CraftInfoBuilder SetPermission(string permission)
				{
					craftInfo.Permission = permission;
					return this;
				}

				public CraftInfoBuilder SetCmdToGive(string cmdToGive)
				{
					craftInfo.CmdToGive = cmdToGive;
					return this;
				}

				public CraftInfoBuilder SetGiveCommand(string giveCommand)
				{
					craftInfo.GiveCommand = giveCommand;
					return this;
				}

				public CraftInfoBuilder SetCraftableItem(string shortname, ulong skinID = 0UL, int amount = 1,
					string displayName = "")
				{
					craftInfo.ShortName = shortname;
					craftInfo.SkinID = skinID;
					craftInfo.Amount = amount;
					craftInfo.DisplayName = displayName;
					return this;
				}

				public CraftInfoBuilder SetRecipeItems(List<ItemForCraft> items)
				{
					craftInfo.Items = items;
					return this;
				}

				public CraftInfoBuilder SetDistance(bool useDistance, float distance)
				{
					craftInfo.UseDistance = useDistance;
					craftInfo.Distance = distance;
					return this;
				}

				public CraftInfoBuilder SetSpawnParameters(bool ground, bool structure)
				{
					craftInfo.Ground = ground;
					craftInfo.Structure = structure;
					return this;
				}

				public CraftInfoBuilder SetModular(ModularCarConf modular)
				{
					craftInfo.Modular = modular;
					return this;
				}

				public CraftInfoBuilder SetCooldown(int cooldown)
				{
					craftInfo.Cooldown = cooldown;
					return this;
				}

				public CraftInfoBuilder SetMultipleCraft(bool multipleCraft)
				{
					craftInfo.MultipleCraft = multipleCraft;
					return this;
				}

				public CraftInfoBuilder SetMaxAmount(int maxAmount)
				{
					craftInfo.MaxAmount = maxAmount;
					return this;
				}

				public CraftInfoBuilder SetCraft(bool craft)
				{
					craftInfo.Craft = craft;
					return this;
				}

				public CraftInfoBuilder SetSale(bool sale)
				{
					craftInfo.Sale = sale;
					return this;
				}

				public CraftInfoBuilder SetCost(int cost)
				{
					craftInfo.Cost = cost;
					return this;
				}

				public CraftInfoBuilder SetReturnItem(bool returnItem)
				{
					craftInfo.ReturnItem = returnItem;
					return this;
				}

				public CraftInfoBuilder SetOwnerID(bool setOwnerID)
				{
					craftInfo.SetOwnerID = setOwnerID;
					return this;
				}

				public CraftInfoBuilder SetCustomizeRotation(bool customizeRotation)
				{
					craftInfo.CustomizeRotationOnSpawning = customizeRotation;
					return this;
				}

				public CraftInfoBuilder SetCraftMenuDescription(Localization localization)
				{
					craftInfo.CraftMenuDescription = localization;
					return this;
				}

				public CraftInfoBuilder SetWorkbenchLevel(WorkbenchLevel level)
				{
					craftInfo.Level = level;
					return this;
				}

				public CraftInfo Build()
				{
					return craftInfo;
				}
			}

			#endregion

			#endregion
		}

		private class ItemForCraft
		{
			[JsonProperty(PropertyName = LangRu ? "Изображение" : "Image")]
			public string Image;

			[JsonProperty(PropertyName = LangRu ? "Shortname" : "ShortName")]
			public string ShortName;

			[JsonProperty(PropertyName = LangRu ? "Количество" : "Amount")]
			public int Amount;

			[JsonProperty(PropertyName = LangRu ? "Скин" : "Skin")]
			public ulong SkinID;

			[JsonProperty(PropertyName = LangRu ? "Название (пусто - стандартное)" : "Title (empty - default)")]
			public string Title = string.Empty;

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
					{
						if (string.IsNullOrEmpty(Title))
							_publicTitle = ItemManager.FindItemDefinition(ShortName)?.displayName.translated ??
							               "UNKNOWN";
						else
							_publicTitle = Title;
					}

					return _publicTitle;
				}
			}

			[JsonIgnore] private int _id = -1;

			[JsonIgnore]
			public int ID
			{
				get
				{
					while (_id == -1)
					{
						var val = Random.Range(int.MinValue, int.MaxValue);
						if (_instance._itemsById.ContainsKey(val)) continue;

						_id = val;
					}

					return _id;
				}
			}

			public Dictionary<string, object> ToDictionary()
			{
				return new Dictionary<string, object>
				{
					["Generated"] = false,
					["ID"] = ID,
					["Image"] = Image,
					["ShortName"] = ShortName,
					["Amount"] = Amount,
					["SkinID"] = SkinID,
					["Title"] = Title
				};
			}

			public string GetItemDisplayName(BasePlayer player)
			{
				return _config.UseLangAPI && _instance.LangAPI != null &&
				       _instance.LangAPI.Call<bool>("IsDefaultDisplayName", PublicTitle)
					? _instance.LangAPI.Call<string>("GetItemDisplayName", ShortName, PublicTitle,
						player.UserIDString) ?? PublicTitle
					: PublicTitle;
			}

			#region Constructor

			public ItemForCraft()
			{
			}

			public ItemForCraft(string image, string shortname, int amount, ulong skin)
			{
				Image = image;
				ShortName = shortname;
				Amount = amount;
				SkinID = skin;
			}

			#endregion
		}

		private class ModularCarConf
		{
			[JsonProperty(PropertyName = "CodeLock")]
			public bool CodeLock;

			[JsonProperty(PropertyName = "KeyLock")]
			public bool KeyLock;

			[JsonProperty(PropertyName = "Engine Parts Tier")]
			public int EnginePartsTier;

			[JsonProperty(PropertyName = "Fresh Water Amount")]
			public int FreshWaterAmount;

			[JsonProperty(PropertyName = "Fuel Amount")]
			public int FuelAmount;

			[JsonProperty(PropertyName = "Modules", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] Modules;

			public Dictionary<string, object> ToDictionary()
			{
				return new Dictionary<string, object>
				{
					["CodeLock"] = CodeLock,
					["KeyLock"] = KeyLock,
					["EnginePartsTier"] = EnginePartsTier,
					["FreshWaterAmount"] = FreshWaterAmount,
					["FuelAmount"] = FuelAmount,
					["Modules"] = Modules
				};
			}
		}

		private class CarConfig
		{
			[JsonProperty(PropertyName = LangRu ? "Активные предметы (в руке)" : "Active Items (in hand)")]
			public ActiveItemOptions ActiveItems;

			[JsonProperty(PropertyName = LangRu ? "DDraw Радиус" : "DDraw Radius")]
			public float Radius;

			[JsonProperty(PropertyName = LangRu ? "DDraw Текст" : "DDraw Text")]
			public string Text;

			[JsonProperty(PropertyName = LangRu ? "DDraw Цвет" : "DDraw Color")]
			public string Color;

			[JsonProperty(PropertyName = LangRu ? "DDraw Задержка (сек)" : "DDraw Delay (sec)")]
			public float Delay;
		}

		public class ActiveItemOptions
		{
			[JsonProperty(PropertyName = LangRu ? "Запретить хранить все предметы" : "Forbid to hold all items")]
			public bool Disable;

			[JsonProperty(
				PropertyName = LangRu
					? "Список заблокированных предметов (shortname)"
					: "List of blocked items (shortname)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] BlackList;
		}

		private class RecyclerConfig
		{
			[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = LangRu ? "Изменить скорость переработки?" : "Use recycling speed?")]
			public bool UseSpeed;

			[JsonProperty(PropertyName = LangRu ? "Скорость переработки" : "Recycling speed")]
			public float Speed;

			[JsonProperty(PropertyName =
				LangRu ? "Показывать дамаг на переработчике" : "Use DDraw? (showing damage on the recycler)")]
			public bool DDraw;

			[JsonProperty(PropertyName =
				LangRu ? "Радиус в котором будет показан текст на переработчике" : "DDraw Radius")]
			public float Radius;

			[JsonProperty(PropertyName = LangRu ? "Текст на переработчике" : "DDraw Text")]
			public string Text;

			[JsonProperty(PropertyName = LangRu ? "Цвет текста на переработчике" : "DDraw Color")]
			public string Color;

			[JsonProperty(PropertyName = LangRu ? "Время показа текста на переработчике (сек)" : "DDraw Delay (sec)")]
			public float Delay;

			[JsonProperty(PropertyName = LangRu ? "Можно ли подбирать переработчик" : "Enabled pickup?")]
			public bool Available;

			[JsonProperty(PropertyName = LangRu ? "Подбор только владельцем?" : "Only owner can pickup")]
			public bool Owner;

			[JsonProperty(PropertyName =
				LangRu ? "Право на постройку для подбора" : "Check ability to build for pickup")]
			public bool Building;

			[JsonProperty(PropertyName = LangRu ? "Настройка BaseProtection" : "BaseProtection Settings",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public float[] Amounts;

			[JsonProperty(PropertyName = LangRu ? "Множитель урона по переработчику" : "Damage Scale")]
			public float Scale;

			[JsonProperty(PropertyName = LangRu ? "Настройки разрушения" : "Destroy Settings")]
			public RecyclerDestroy Destroy;

			[JsonIgnore] private CraftInfo _craft;

			[JsonIgnore]
			public Item GetItem
			{
				get
				{
					if (_craft == null)
						_craft = _instance._crafts.Find(x => x.Type == CraftType.Recycler);

					return _craft?.CreateItem();
				}
			}
		}

		private class RecyclerDestroy
		{
			[JsonProperty(PropertyName =
				LangRu
					? "Проверять землю для переработчика? (разрушение при отсутствии)"
					: "Check ground for recyclers? (destroy on missng)")]
			public bool CheckGround;

			[JsonProperty(PropertyName = LangRu ? "Выдавать предмет при разрушении?" : "Give item on destroy?")]
			public bool Item;

			[JsonProperty(PropertyName = LangRu ? "Эффекты при разрушении" : "Effects on destroy",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> Effects;
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
				var cfg = new Configuration();

				if (_config.Version < new VersionNumber(2, 4, 0))
					_config.Categories.ForEach(cat => { cat.Color = IColor.Create("#161617", 100); });

				if (_config.Version < new VersionNumber(2, 10, 4))
				{
					_config.Recycler.Enabled = true;

					_config.Recycler.UseSpeed = true;
				}

				if (_config.Version < new VersionNumber(2, 10, 9))
				{
					var margin = Convert.ToSingle(Config["UI Settings", "Item Margin"]);
					var width = Convert.ToSingle(Config["UI Settings", "Item Width"]);

					_config.UI.CraftMenuSettings = cfg.UI.CraftMenuSettings;
					_config.UI.CraftMenuSettings.ItemMargin = margin;
					_config.UI.CraftMenuSettings.ItemWidth = width;
				}

				if (_config.Version < new VersionNumber(2, 10, 10))
				{
					_config.Categories.ForEach(category =>
					{
						category.Crafts.ForEach(craft =>
						{
							switch (craft.Image)
							{
								case "https://i.imgur.com/YXjADeE.png":
									craft.Image = "https://i.ibb.co/mqYTq4P/image.png";
									break;
								case "https://i.imgur.com/dmWQOm6.png":
									craft.Image = "https://i.ibb.co/LJdqQ6z/image.png";
									break;
								case "https://i.imgur.com/CgpVw2j.png":
									craft.Image = "https://i.ibb.co/HpkTsBZ/image.png";
									break;
								case "https://i.imgur.com/cp2Xx2A.png":
									craft.Image = "https://i.ibb.co/gr64fDx/image.png";
									break;
								case "https://i.imgur.com/7JZE0Lr.png":
									craft.Image = "https://i.ibb.co/nzXCNws/image.png";
									break;
								case "https://i.imgur.com/xj0N3lI.png":
									craft.Image = "https://i.ibb.co/gdcVNwr/image.png";
									break;
								case "https://i.imgur.com/eioxlvK.png":
									craft.Image = "https://i.ibb.co/MG4N1cs/image.png";
									break;
								case "https://i.imgur.com/z7X5D5V.png":
									craft.Image = "https://i.ibb.co/nwsDTbW/image.png";
									break;
								case "https://i.imgur.com/LLB2AVi.png":
									craft.Image = "https://i.ibb.co/mHRkk5P/image.png";
									break;
								case "https://i.imgur.com/mw1T17x.png":
									craft.Image = "https://i.ibb.co/024076x/image.png";
									break;
							}

							craft.CraftMenuDescription = new Localization
							{
								Enabled = false,
								Messages = new Dictionary<string, string>
								{
									["en"] = craft.Description ?? string.Empty,
									["fr"] = craft.Description ?? string.Empty
								}
							};

							if (craft.Type == CraftType.Vehicle)
								craft.CustomizeRotationOnSpawning = true;
						});

						if (category.Title == "Vehicles")
						{
							category.Crafts.Add(new CraftInfo
							{
								Enabled = false,
								Image = "https://i.ibb.co/tmsNRcB/newkopter.png",
								Title = "Attack Helicopter",
								Description = "Fast air transport",
								CmdToGive = "giveattackhelicopter",
								Permission = "crafts.all",
								DisplayName = "Attack Helicopter",
								ShortName = "electric.flasherlight",
								Amount = 1,
								SkinID = 3043024177,
								Type = CraftType.Vehicle,
								Prefab = "assets/content/vehicles/attackhelicopter/attackhelicopter.entity.prefab",
								Level = WorkbenchLevel.Two,
								UseDistance = true,
								Distance = 1.5f,
								GiveCommand = string.Empty,
								Ground = true,
								Structure = true,
								Items = new List<ItemForCraft>
								{
									new ItemForCraft(string.Empty, "gears", 5, 0),
									new ItemForCraft(string.Empty, "roadsigns", 5, 0),
									new ItemForCraft(string.Empty, "metal.fragments", 2000, 0)
								},
								Modular = new ModularCarConf
								{
									CodeLock = true,
									KeyLock = false,
									EnginePartsTier = 2,
									FreshWaterAmount = 0,
									FuelAmount = 140,
									Modules = new[]
									{
										"vehicle.1mod.engine",
										"vehicle.1mod.cockpit.armored",
										"vehicle.1mod.cockpit.armored"
									}
								},
								Cooldown = 0,
								MultipleCraft = false,
								MaxAmount = 0,
								Craft = true,
								Sale = false,
								Cost = 100,
								SetOwnerID = false,
								CustomizeRotationOnSpawning = true,
								CraftMenuDescription = new Localization
								{
									Enabled = false,
									Messages = new Dictionary<string, string>
									{
										["en"] = "Fast air transport",
										["fr"] = "Transport aérien rapide"
									}
								},
								ReturnItem = true
							});

							category.Crafts.Add(new CraftInfo
							{
								Enabled = false,
								Image = "https://i.ibb.co/jWC9Mrd/image.png",
								Title = "Submarine Solo",
								Description = "Fast underwater transport",
								CmdToGive = "givesolosubmarine",
								Permission = "crafts.all",
								DisplayName = "Submarine Solo",
								ShortName = "barricade.sandbags",
								Amount = 1,
								SkinID = 3043043385,
								Type = CraftType.Vehicle,
								Prefab = "assets/content/vehicles/submarine/submarinesolo.entity.prefab",
								Level = WorkbenchLevel.Three,
								UseDistance = true,
								Distance = 1.5f,
								GiveCommand = string.Empty,
								Ground = true,
								Structure = true,
								Items = new List<ItemForCraft>
								{
									new ItemForCraft(string.Empty, "gears", 5, 0),
									new ItemForCraft(string.Empty, "roadsigns", 5, 0),
									new ItemForCraft(string.Empty, "metal.fragments", 2000, 0)
								},
								Modular = new ModularCarConf
								{
									CodeLock = true,
									KeyLock = false,
									EnginePartsTier = 2,
									FreshWaterAmount = 0,
									FuelAmount = 140,
									Modules = new[]
									{
										"vehicle.1mod.engine",
										"vehicle.1mod.cockpit.armored",
										"vehicle.1mod.cockpit.armored"
									}
								},
								Cooldown = 0,
								MultipleCraft = false,
								MaxAmount = 0,
								Craft = true,
								Sale = false,
								Cost = 100,
								SetOwnerID = false,
								CustomizeRotationOnSpawning = true,
								CraftMenuDescription = new Localization
								{
									Enabled = false,
									Messages = new Dictionary<string, string>
									{
										["en"] = "Fast underwater transport",
										["fr"] = "Transport subaquatique rapide"
									}
								},
								ReturnItem = true
							});

							category.Crafts.Add(new CraftInfo
							{
								Enabled = false,
								Image = "https://i.ibb.co/qYNQhTJ/image.png",
								Title = "Submarine Duo",
								Description = "Fast underwater transport",
								CmdToGive = "giveduosubmarine",
								Permission = "crafts.all",
								DisplayName = "Submarine Duo",
								ShortName = "barricade.sandbags",
								Amount = 1,
								SkinID = 3043043217,
								Type = CraftType.Vehicle,
								Prefab = "assets/content/vehicles/submarine/submarineduo.entity.prefab",
								Level = WorkbenchLevel.Three,
								UseDistance = true,
								Distance = 1.5f,
								GiveCommand = string.Empty,
								Ground = true,
								Structure = true,
								Items = new List<ItemForCraft>
								{
									new ItemForCraft(string.Empty, "gears", 5, 0),
									new ItemForCraft(string.Empty, "roadsigns", 5, 0),
									new ItemForCraft(string.Empty, "metal.fragments", 2000, 0)
								},
								Modular = new ModularCarConf
								{
									CodeLock = true,
									KeyLock = false,
									EnginePartsTier = 2,
									FreshWaterAmount = 0,
									FuelAmount = 140,
									Modules = new[]
									{
										"vehicle.1mod.engine",
										"vehicle.1mod.cockpit.armored",
										"vehicle.1mod.cockpit.armored"
									}
								},
								Cooldown = 0,
								MultipleCraft = false,
								MaxAmount = 0,
								Craft = true,
								Sale = false,
								Cost = 100,
								SetOwnerID = false,
								CustomizeRotationOnSpawning = true,
								CraftMenuDescription = new Localization
								{
									Enabled = false,
									Messages = new Dictionary<string, string>
									{
										["en"] = "Fast underwater transport",
										["fr"] = "Transport subaquatique rapide"
									}
								},
								ReturnItem = true
							});

							category.Crafts.Add(new CraftInfo
							{
								Enabled = false,
								Image = "https://i.ibb.co/80hKRcm/tugboat.png",
								Title = "Tugboat",
								Description = "Slow water transport",
								CmdToGive = "givetugboat",
								Permission = "crafts.all",
								DisplayName = "Tugboat",
								ShortName = "barricade.sandbags",
								Amount = 1,
								SkinID = 3043043727,
								Type = CraftType.Vehicle,
								Prefab = "assets/content/vehicles/boats/tugboat/tugboat.prefab",
								Level = WorkbenchLevel.Three,
								UseDistance = true,
								Distance = 1.5f,
								GiveCommand = string.Empty,
								Ground = true,
								Structure = true,
								Items = new List<ItemForCraft>
								{
									new ItemForCraft(string.Empty, "gears", 5, 0),
									new ItemForCraft(string.Empty, "roadsigns", 5, 0),
									new ItemForCraft(string.Empty, "metal.fragments", 2000, 0)
								},
								Modular = new ModularCarConf
								{
									CodeLock = true,
									KeyLock = false,
									EnginePartsTier = 2,
									FreshWaterAmount = 0,
									FuelAmount = 140,
									Modules = new[]
									{
										"vehicle.1mod.engine",
										"vehicle.1mod.cockpit.armored",
										"vehicle.1mod.cockpit.armored"
									}
								},
								Cooldown = 0,
								MultipleCraft = false,
								MaxAmount = 0,
								Craft = true,
								Sale = false,
								Cost = 100,
								SetOwnerID = false,
								CustomizeRotationOnSpawning = true,
								CraftMenuDescription = new Localization
								{
									Enabled = false,
									Messages = new Dictionary<string, string>
									{
										["en"] = "Slow water transport",
										["fr"] = "Transport lent de l'eau"
									}
								},
								ReturnItem = true
							});
						}
					});

					_config.UI.CraftMenuSettings.Description = cfg.UI.CraftMenuSettings.Description;
				}

				if (_config.Version < new VersionNumber(2, 10, 12))
				{
					_config.UI.Craft = cfg.UI.Craft;

					var craftWidth = Config.Get<float>("UI Settings", "Craft Width");
					_config.UI.Craft.Width = craftWidth;

					var craftHeight = Config.Get<float>("UI Settings", "Craft Height");
					_config.UI.Craft.Height = craftHeight;

					var craftMargin = Config.Get<float>("UI Settings", "Craft Margin");
					_config.UI.Craft.Margin = craftMargin;

					var craftYIndent = Config.Get<float>("UI Settings", "Craft Y Indent");
					_config.UI.Craft.IndentY = craftYIndent;

					var craftAmountOnString = Config.Get<int>("UI Settings", "Craft Amount On String");
					_config.UI.Craft.AmountOnString = craftAmountOnString;

					var craftStrings = Config.Get<int>("UI Settings", "Craft Strings");
					_config.UI.Craft.MaxLines = craftStrings;
				}
			}

			_config.Version = Version;
			PrintWarning("Config update completed!");
		}

		#endregion

		#region Data

		private PlayerCooldown _cooldown;

		private void LoadCooldown()
		{
			try
			{
				_cooldown = Interface.Oxide.DataFileSystem.ReadObject<PlayerCooldown>($"{Name}/Cooldowns");
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			if (_cooldown == null) _cooldown = new PlayerCooldown();
		}

		private void SaveCooldown()
		{
			Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Cooldowns", _cooldown);
		}

		private class PlayerCooldown
		{
			[JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<ulong, PlayerCooldownData> Players = new Dictionary<ulong, PlayerCooldownData>();

			public static PlayerCooldownData GetOrAdd(ulong member)
			{
				_instance._cooldown.Players.TryAdd(member, new PlayerCooldownData());

				return _instance._cooldown.Players[member];
			}
		}

		private class PlayerCooldownData
		{
			[JsonProperty(PropertyName = "LastTime", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<int, DateTime> LastTime = new Dictionary<int, DateTime>();

			public void Add(CraftInfo craft)
			{
				if (craft.Cooldown > 0) LastTime[craft.PUBLIC_ID] = DateTime.Now;
			}

			public int GetCooldown(CraftInfo craft)
			{
				DateTime time;
				if (LastTime.TryGetValue(craft.PUBLIC_ID, out time))
					return (int) time.AddSeconds(craft.Cooldown).Subtract(DateTime.Now).TotalSeconds;

				return 0;
			}
		}

		#endregion

		#region Hooks

		private void Init()
		{
			_instance = this;

			_crafts = _config.Categories.SelectMany(x => x.Crafts).ToList();

			RegisterPermissions();

			RegisterCommands();

			LoadCooldown();

#if TESTING
			StopwatchWrapper.OnComplete = DebugMessage;
#endif
		}

		private void OnServerInitialized(bool initial)
		{
			LoadItems();

			LoadImages();

			LoadCustomWorkbenches();

			if (!initial)
			{
#if TESTING
				using (new StopwatchWrapper("Loading entities took {0}ms."))
#endif
				{
					foreach (var ent in BaseNetworkable.serverEntities.OfType<BaseCombatEntity>())
						OnEntitySpawned(ent);
				}
			}

			UnsubscribeHooks();

			if (_config.Categories.Exists(cat => cat.Crafts.Exists(craft => craft.Cooldown > 0)))
				timer.Every(1, UpdateCooldown);
		}

		private void Unload()
		{
			DestroyUi();

			DestroyRecyclers();

			DestroyCars();

			DestroyCustomWorkbenches();

			SaveCooldown();

			_instance = null;
			_config = null;
		}

		private void OnEntityBuilt(Planner held, GameObject go)
		{
			if (held == null || go == null) return;

			var player = held.GetOwnerPlayer();
			if (player == null) return;

			var entity = go.ToBaseEntity();
			if (entity == null || entity.skinID == 0) return;

			var craft = _crafts.Find(x =>
				(x.Type == CraftType.Vehicle || x.Type == CraftType.Recycler || x.Type == CraftType.ModularCar) &&
				x.SkinID == entity.skinID);
			if (craft == null) return;

			var transform = entity.transform;

			var itemName = craft.PublicTitle;

			NextTick(() =>
			{
				if (entity != null) entity.Kill();
			});

			if (Physics.Raycast(transform.position + new Vector3(0, 0.1f, 0), Vector3.down, out var rHit, 4f,
				    LayerMask.GetMask("Construction")) && rHit.GetEntity() != null)
			{
				if (!craft.Structure)
				{
					Reply(player, OnStruct, itemName);
					craft.GiveCraftToPlayer(player);
					return;
				}
			}
			else
			{
				if (!craft.Ground)
				{
					Reply(player, OnGround, itemName);
					craft.GiveCraftToPlayer(player);
					return;
				}
			}

			if (craft.UseDistance && Vector3.Distance(player.ServerPosition, transform.position) < craft.Distance)
			{
				Reply(player, BuildDistance, craft.Distance);
				craft.GiveCraftToPlayer(player);
				return;
			}

			craft.SpawnEntity(player, transform.position, transform.rotation);
		}

		private object CanResearchItem(BasePlayer player, Item item)
		{
			if (player == null || item == null ||
			    !_crafts.Exists(craft => craft.Enabled && craft.Type == CraftType.Vehicle &&
			                             craft.ShortName == item.info.shortname &&
			                             (item.skin == 0 || craft.SkinID == item.skin))) return null;

			return false;
		}

		private void OnEntitySpawned(BaseCombatEntity entity)
		{
			if (entity == null) return;

			if (_config.Recycler.Enabled && entity is Recycler)
			{
				entity.gameObject.AddComponent<RecyclerComponent>();
				return;
			}

			if (entity is BasicCar) entity.gameObject.AddComponent<CarController>();
		}

		private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
		{
			if (entity == null || entity.OwnerID == 0) return;

			var recycler = entity.GetComponent<RecyclerComponent>();
			if (recycler != null)
			{
				info.damageTypes.ScaleAll(_config.Recycler.Scale);
				recycler.DDraw();
			}

			var car = entity.GetComponent<CarController>();
			if (car != null)
			{
				car.ManageDamage(info);
				car.DDraw();
			}
		}

		private object OnRecyclerToggle(Recycler recycler, BasePlayer player)
		{
			if (recycler == null || player == null) return null;

			var recyclerComponent = recycler.GetComponent<RecyclerComponent>();
			if (recyclerComponent == null) return null;

			if (!recycler.IsOn())
			{
				foreach (var obj in recycler.inventory.itemList)
					obj.CollectedForCrafting(player);

				recyclerComponent.StartRecycling();
			}
			else
			{
				recyclerComponent.StopRecycling();
			}

			return false;
		}

		private bool? CanBuildToolsGiveRefund(BasePlayer player, BaseEntity entity)
		{
			if (player == null || entity == null) return null;

			var targetCraft = _crafts.Find(craftInfo => craftInfo.Enabled &&
			                                            craftInfo.Type == CraftType.Vehicle &&
			                                            craftInfo.Prefab == entity.PrefabName &&
			                                            craftInfo.SkinID == entity.skinID);
			if (targetCraft == null || !targetCraft.ReturnItem) return null;

			targetCraft.GiveItem(player);
			return true;
		}

		private void OnHammerHit(BasePlayer player, HitInfo info)
		{
			if (player == null || info == null) return;

			var entity = info.HitEntity;
			if (entity == null || entity.OwnerID == 0) return;

			var recyclerComponent = entity.GetComponent<RecyclerComponent>();
			if (recyclerComponent == null) return;

			if (!_config.Recycler.Available)
			{
				Reply(player, NotTake);
				return;
			}

			recyclerComponent.TryPickup(player);
		}

		private void OnPlayerDisconnected(BasePlayer player)
		{
			if (player == null) return;

			_updateCooldown.Remove(player);
			_openedCustomWb.Remove(player.userID);
		}

		private void OnEntityGroundMissing(Recycler recycler)
		{
			if (recycler == null) return;

			var component = recycler.GetComponent<RecyclerComponent>();
			if (component == null) return;

			component.DestroyActions();
		}

		private object CanLootEntity(BasePlayer player, Workbench workbench)
		{
			if (player == null || workbench == null || workbench.net == null)
				return null;

			CustomWorkbenchConf customWb;
			if (!_config.CustomWorkbench.TryGetValue(workbench.net.ID.Value, out customWb)) return null;

			_openedCustomWb[player.userID] = customWb;

			MainUi(player, first: true);
			return false;
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

		#endregion

		#region Commands

		private void CmdOpenCrafts(IPlayer cov, string command, string[] args)
		{
			var player = cov?.Object as BasePlayer;
			if (player == null) return;

			if (_enabledImageLibrary == false)
			{
				SendNotify(player, NoILError, 1);

				BroadcastILNotInstalled();
				return;
			}

			if (!string.IsNullOrEmpty(_config.Permission) &&
			    !permission.UserHasPermission(player.UserIDString, _config.Permission))
			{
				SendNotify(player, NoPermission, 1);
				return;
			}

			if (_config.CloseReusing)
			{
				if (_openedUi.Contains(player))
				{
					CuiHelper.DestroyUi(player, Layer);

					_updateCooldown.Remove(player);

					_openedUi.Remove(player);

					return;
				}

				_openedUi.Add(player);
			}

			MainUi(player, first: true);
		}

		private void CmdGiveCrafts(IPlayer cov, string command, string[] args)
		{
			if ((cov.IsAdmin || cov.IsServer) == false) return;

			if (args.Length == 0)
			{
				cov.Reply($"Error syntax! Use: /{command} [name/steamId]");
				return;
			}

			var craft = _crafts.Find(x => x.CmdToGive == command);
			if (craft == null) return;

			var target = covalence.Players.FindPlayer(args[0])?.Object as BasePlayer;
			if (target == null)
			{
				PrintError($"Player '{args[0]}' not found!");
				return;
			}

			craft.GiveCraftToPlayer(target);
			SendNotify(target, GotCraft, 0, craft.PublicTitle);
		}

		private void CmdSetCustomWb(IPlayer cov, string command, string[] args)
		{
			var player = cov?.Object as BasePlayer;
			if (player == null) return;

			if (!permission.UserHasPermission(player.UserIDString, PermSetWb))
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
			categories.RemoveAll(cat => !_config.Categories.Exists(confCat => confCat.Title == cat));
			if (categories.Count == 0)
			{
				SendNotify(player, WbNotFoundCategories, 1);
				return;
			}

			var workbench = GetLookWorkbench(player);
			if (workbench == null || workbench.net == null)
			{
				SendNotify(player, WbNotFoundWorkbench, 1);
				return;
			}

			if (_config.CustomWorkbench.ContainsKey(workbench.net.ID.Value))
			{
				SendNotify(player, WbWorkbenchExists, 1);
				return;
			}

			var conf = new CustomWorkbenchConf
			{
				Categories = categories
			};

			_config.CustomWorkbench[workbench.net.ID.Value] = conf;

			SaveConfig();

			SendNotify(player, WbWorkbenchInstalled, 0);
			InitCustomWorkbench(workbench, conf);
		}

		[ConsoleCommand("UI_Crafts")]
		private void CmdConsoleCrafts(ConsoleSystem.Arg arg)
		{
			var player = arg?.Player();
			if (player == null || !arg.HasArgs()) return;

			switch (arg.Args[0])
			{
				case "close":
				{
					_updateCooldown.Remove(player);
					_openedCustomWb.Remove(player.userID);
					break;
				}

				case "page":
				{
					int category, page = 0, catPage = 0;
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out category)) return;

					if (arg.HasArgs(3))
						int.TryParse(arg.Args[2], out page);

					if (arg.HasArgs(4))
						int.TryParse(arg.Args[3], out catPage);

					MainUi(player, category, page, catPage);
					break;
				}

				case "back":
				{
					int category, page, catPage;
					if (!arg.HasArgs(4) || !int.TryParse(arg.Args[1], out category) ||
					    !int.TryParse(arg.Args[2], out page) ||
					    !int.TryParse(arg.Args[3], out catPage)) return;

					_craftEditing.Remove(player);
					_itemEditing.Remove(player);

					MainUi(player, category, page, catPage, true);
					break;
				}

				case "trycraft":
				{
					int category, page, catPage, itemId;
					if (!arg.HasArgs(5) || !int.TryParse(arg.Args[1], out category) ||
					    !int.TryParse(arg.Args[2], out page) ||
					    !int.TryParse(arg.Args[3], out catPage) ||
					    !int.TryParse(arg.Args[4], out itemId)) return;

					_updateCooldown.Remove(player);

					CraftUi(player, category, page, catPage, itemId);
					break;
				}

				case "craft":
				{
					int category, page, catPage, itemId, amount;
					if (!arg.HasArgs(5) || !int.TryParse(arg.Args[1], out category) ||
					    !int.TryParse(arg.Args[2], out page) ||
					    !int.TryParse(arg.Args[3], out catPage) ||
					    !int.TryParse(arg.Args[4], out itemId) ||
					    !int.TryParse(arg.Args[5], out amount)) return;

					var craft = GetPlayerCategories(player)[category].Crafts.Find(x => x.PUBLIC_ID == itemId);
					if (craft == null || !craft.Craft) return;
					
					var bypass = HasBypass(player);
					if (!bypass)
					{
						if (!HasWorkbench(player, craft.Level))
						{
							Reply(player, NotWorkbench);
							return;
						}

						var allItems = Pool.Get<List<Item>>();
						player.inventory.GetAllItems(allItems);

						var notEnoughResources = craft.Items.Exists(item =>
							!HasAmount(allItems, item.ShortName, item.SkinID, item.Amount * amount));
                
						Pool.FreeUnmanaged(ref allItems);

						if (notEnoughResources)
						{
							SendNotify(player, NotEnoughResources, 1);
							return;
						}
					}

					var slots = player.inventory.containerBelt.capacity -
					            player.inventory.containerBelt.itemList.Count +
					            (player.inventory.containerMain.capacity -
					             player.inventory.containerMain.itemList.Count);
					if (slots < amount)
					{
						SendNotify(player, NotEnoughSpace, 1);
						return;
					}

					craft.UpdateCooldown(player);

					ServerMgr.Instance.StartCoroutine(TakeAndGiveCraftItems(player, amount, craft, bypass));

					SendNotify(player, SuccessfulCraft, 0, craft.PublicTitle);

					if (_config.UI.CloseAfterCraft)
					{
						_updateCooldown.Remove(player);
						_openedCustomWb.Remove(player.userID);
					}
					else
					{
						MainUi(player, category, page, catPage, true);
					}

					break;
				}

				case "buy_craft":
				{
					int category, page, catPage, itemId, amount;
					if (!arg.HasArgs(5) || !int.TryParse(arg.Args[1], out category) ||
					    !int.TryParse(arg.Args[2], out page) ||
					    !int.TryParse(arg.Args[3], out catPage) ||
					    !int.TryParse(arg.Args[4], out itemId) ||
					    !int.TryParse(arg.Args[5], out amount)) return;

					var craft = GetPlayerCategories(player)[category].Crafts.Find(x => x.PUBLIC_ID == itemId);
					if (craft == null || !craft.Sale) return;

					if (!HasWorkbench(player, craft.Level))
					{
						Reply(player, NotWorkbench);
						return;
					}

					var totalAmount = amount;

					switch (craft.Type)
					{
						case CraftType.Item:
						{
							totalAmount = craft.SplitStacks ? craft.GetItemStacks(amount).Count :
								craft.SingleStack ? 1 : amount;
							break;
						}
					}

					var slots = player.inventory.containerBelt.capacity -
					            player.inventory.containerBelt.itemList.Count +
					            (player.inventory.containerMain.capacity -
					             player.inventory.containerMain.itemList.Count);
					if (slots < totalAmount)
					{
						SendNotify(player, NotEnoughSpace, 1);
						return;
					}

					if (!HasBypass(player) && !_config.Economy.RemoveBalance(player, craft.Cost * amount))
					{
						SendNotify(player, NotMoney, 1);
						return;
					}

					craft.UpdateCooldown(player);

					ServerMgr.Instance.StartCoroutine(GiveCraftItems(player, amount, craft));

					SendNotify(player, SuccessfulCraft, 0, craft.PublicTitle);

					if (_config.UI.CloseAfterCraft)
					{
						_updateCooldown.Remove(player);
						_openedCustomWb.Remove(player.userID);
					}
					else
					{
						MainUi(player, category, page, catPage, true);
					}

					break;
				}

				case "start_edit":
				{
					int category, page, catPage, craftId;
					if (!arg.HasArgs(5) ||
					    !int.TryParse(arg.Args[1], out category) ||
					    !int.TryParse(arg.Args[2], out page) ||
					    !int.TryParse(arg.Args[3], out catPage) ||
					    !int.TryParse(arg.Args[4], out craftId))
						return;

					EditUi(player, category, page, catPage, craftId);
					break;
				}

				case "edit":
				{
					int category, page, catPage, craftId, itemsPage;
					if (!arg.HasArgs(8) ||
					    !int.TryParse(arg.Args[1], out category) ||
					    !int.TryParse(arg.Args[2], out page) ||
					    !int.TryParse(arg.Args[3], out catPage) ||
					    !int.TryParse(arg.Args[4], out craftId) ||
					    !int.TryParse(arg.Args[5], out itemsPage) ||
					    string.IsNullOrEmpty(arg.Args[6]) || string.IsNullOrEmpty(arg.Args[7])) return;

					var key = arg.Args[6];
					var value = arg.Args[7];

					if (_craftEditing.ContainsKey(player) && _craftEditing[player].ContainsKey(key))
					{
						object newValue;

						switch (key)
						{
							case "Amount":
							{
								int result;
								if (int.TryParse(value, out result))
									newValue = result;
								else
									return;
								break;
							}

							case "SkinID":
							{
								ulong result;
								if (ulong.TryParse(value, out result))
									newValue = result;
								else
									return;
								break;
							}

							case "Cost":
							{
								float result;
								if (float.TryParse(value, out result))
									newValue = result;
								else
									return;
								break;
							}

							case "Type":
							{
								CraftType result;
								if (Enum.TryParse(value, out result))
									newValue = result;
								else
									return;
								break;
							}

							case "Level":
							{
								WorkbenchLevel result;
								if (Enum.TryParse(value, out result))
									newValue = result;
								else
									return;
								break;
							}

							case "Craft":
							case "Sales":
							case "Enabled":
							case "UseDistance":
							case "Ground":
							case "Structure":
							{
								bool result;
								if (bool.TryParse(value, out result))
									newValue = result;
								else
									return;
								break;
							}

							case "Description":
							{
								newValue = string.Join(" ", arg.Args.Skip(7));
								break;
							}

							default:
							{
								newValue = value;
								break;
							}
						}

						_craftEditing[player][key] = newValue;
					}

					EditUi(player, category, page, catPage, craftId);
					break;
				}

				case "save_edit":
				{
					int category, page, catPage, craftId;
					if (!arg.HasArgs(5) ||
					    !int.TryParse(arg.Args[1], out category) ||
					    !int.TryParse(arg.Args[2], out page) ||
					    !int.TryParse(arg.Args[3], out catPage) ||
					    !int.TryParse(arg.Args[4], out craftId))
						return;

					Dictionary<string, object> values;
					if (!_craftEditing.TryGetValue(player, out values) || values == null) return;

					var generated = Convert.ToBoolean(values["Generated"]);
					var craft = generated ? new CraftInfo() : FindCraftById(craftId);
					if (craft == null) return;

					craft.Enabled = Convert.ToBoolean(values["Enabled"]);
					craft.Image = (string) values["Image"];
					craft.Title = (string) values["Title"];
					craft.Description = (string) values["Description"];
					craft.CmdToGive = (string) values["CmdToGive"];
					craft.Permission = (string) values["Permission"];
					craft.DisplayName = (string) values["DisplayName"];
					craft.ShortName = (string) values["ShortName"];
					craft.Prefab = (string) values["Prefab"];
					craft.GiveCommand = (string) values["GiveCommand"];
					craft.Amount = Convert.ToInt32(values["Amount"]);
					craft.SkinID = Convert.ToUInt64(values["SkinID"]);
					craft.Type = (CraftType) values["Type"];
					craft.Level = (WorkbenchLevel) values["Level"];
					craft.UseDistance = Convert.ToBoolean(values["UseDistance"]);
					craft.Distance = Convert.ToSingle(values["Distance"]);
					craft.Structure = Convert.ToBoolean(values["Structure"]);
					craft.Items = values["Items"] as List<ItemForCraft>;
					craft.Craft = Convert.ToBoolean(values["Craft"]);
					craft.Sale = Convert.ToBoolean(values["Sales"]);
					craft.Cost = Convert.ToSingle(values["Cost"]);

					if (generated)
						GetPlayerCategories(player)[category].Crafts.Add(craft);

					_craftEditing.Remove(player);
					_itemEditing.Remove(player);

					SaveConfig();

					MainUi(player, category, page, catPage, true);
					break;
				}

				case "delete_edit":
				{
					int category, page, catPage, craftId;
					if (!arg.HasArgs(4) ||
					    !int.TryParse(arg.Args[1], out category) ||
					    !int.TryParse(arg.Args[2], out page) ||
					    !int.TryParse(arg.Args[3], out catPage) ||
					    !int.TryParse(arg.Args[4], out craftId))
						return;

					var craft = FindCraftById(craftId);
					if (craft == null) return;

					GetPlayerCategories(player)[category].Crafts.Remove(craft);

					_craftEditing.Remove(player);
					_itemEditing.Remove(player);

					SaveConfig();

					MainUi(player, category, page, catPage, true);
					break;
				}

				case "edit_page":
				{
					int category, page, catPage, craftId, itemsPage;
					if (!arg.HasArgs(6) ||
					    !int.TryParse(arg.Args[1], out category) ||
					    !int.TryParse(arg.Args[2], out page) ||
					    !int.TryParse(arg.Args[3], out catPage) ||
					    !int.TryParse(arg.Args[4], out craftId) ||
					    !int.TryParse(arg.Args[5], out itemsPage))
						return;

					EditUi(player, category, page, catPage, craftId, itemsPage);
					break;
				}

				case "stopedit":
				{
					_craftEditing.Remove(player);
					_itemEditing.Remove(player);
					break;
				}

				case "start_edititem":
				{
					int category, page, catPage, craftId, itemsPage, itemId;
					if (!arg.HasArgs(7) ||
					    !int.TryParse(arg.Args[1], out category) ||
					    !int.TryParse(arg.Args[2], out page) ||
					    !int.TryParse(arg.Args[3], out catPage) ||
					    !int.TryParse(arg.Args[4], out craftId) ||
					    !int.TryParse(arg.Args[5], out itemsPage) ||
					    !int.TryParse(arg.Args[6], out itemId))
						return;

					_itemEditing.Remove(player);

					EditItemUi(player, category, page, catPage, craftId, itemsPage, itemId);
					break;
				}

				case "edititem":
				{
					int category, page, catPage, craftId, itemsPage, itemId;
					if (!arg.HasArgs(9) ||
					    !int.TryParse(arg.Args[1], out category) ||
					    !int.TryParse(arg.Args[2], out page) ||
					    !int.TryParse(arg.Args[3], out catPage) ||
					    !int.TryParse(arg.Args[4], out craftId) ||
					    !int.TryParse(arg.Args[5], out itemsPage) ||
					    !int.TryParse(arg.Args[6], out itemId) ||
					    string.IsNullOrEmpty(arg.Args[7]) || string.IsNullOrEmpty(arg.Args[8]))
						return;

					var key = arg.Args[7];
					var value = arg.Args[8];

					if (_itemEditing.ContainsKey(player) && _itemEditing[player].ContainsKey(key))
					{
						object newValue = null;

						switch (key)
						{
							case "Amount":
							{
								int result;
								if (value == "delete")
									newValue = 1;
								else if (int.TryParse(value, out result))
									newValue = result;
								break;
							}
							case "SkinID":
							{
								ulong result;
								if (value == "delete")
									newValue = 0UL;
								else if (ulong.TryParse(value, out result))
									newValue = result;
								break;
							}
							default:
							{
								newValue = value == "delete" ? string.Empty : value;
								break;
							}
						}

						_itemEditing[player][key] = newValue;
					}

					EditItemUi(player, category, page, catPage, craftId, itemsPage, itemId);
					break;
				}

				case "saveitem":
				{
					int category, page, catPage, craftId, itemsPage, itemId;
					if (!arg.HasArgs(7) ||
					    !int.TryParse(arg.Args[1], out category) ||
					    !int.TryParse(arg.Args[2], out page) ||
					    !int.TryParse(arg.Args[3], out catPage) ||
					    !int.TryParse(arg.Args[4], out craftId) ||
					    !int.TryParse(arg.Args[5], out itemsPage) ||
					    !int.TryParse(arg.Args[6], out itemId))
						return;

					Dictionary<string, object> values;
					if (!_itemEditing.TryGetValue(player, out values) || values == null) return;

					var generated = Convert.ToBoolean(values["Generated"]);
					var item = generated ? new ItemForCraft() : FindItemById(itemId);
					if (item == null) return;

					item.Image = values["Image"].ToString();
					item.ShortName = values["ShortName"].ToString();
					item.Amount = Convert.ToInt32(values["Amount"]);
					item.SkinID = Convert.ToUInt64(values["SkinID"]);

					if (generated)
						((List<ItemForCraft>) _craftEditing[player]["Items"]).Add(item);
					else
						_craftEditing.Remove(player);

					_itemEditing.Remove(player);

					SaveConfig();

					EditUi(player, category, page, catPage, craftId, itemsPage);
					break;
				}

				case "removeitem":
				{
					int category, page, craftId, itemsPage, itemId;
					if (!arg.HasArgs(6) ||
					    !int.TryParse(arg.Args[1], out category) ||
					    !int.TryParse(arg.Args[2], out page) ||
					    !int.TryParse(arg.Args[3], out craftId) ||
					    !int.TryParse(arg.Args[4], out itemsPage) ||
					    !int.TryParse(arg.Args[5], out itemId))
						return;

					var craft = FindCraftById(craftId);
					if (craft == null) return;

					var item = FindItemById(itemId);
					if (item == null) return;

					craft.Items.Remove(item);

					_craftEditing.Remove(player);
					_itemEditing.Remove(player);

					SaveConfig();

					EditUi(player, category, page, craftId, itemsPage);
					break;
				}

				case "selectitem":
				{
					int category, page, catPage, craftId, itemsPage, itemId;
					if (!arg.HasArgs(7) ||
					    !int.TryParse(arg.Args[1], out category) ||
					    !int.TryParse(arg.Args[2], out page) ||
					    !int.TryParse(arg.Args[3], out catPage) ||
					    !int.TryParse(arg.Args[4], out craftId) ||
					    !int.TryParse(arg.Args[5], out itemsPage) ||
					    !int.TryParse(arg.Args[6], out itemId))
						return;

					var selectedCategory = string.Empty;
					if (arg.HasArgs(8))
						selectedCategory = arg.Args[7];

					var localPage = 0;
					if (arg.HasArgs(9))
						int.TryParse(arg.Args[8], out localPage);

					var input = string.Empty;
					if (arg.HasArgs(10))
						input = string.Join(" ", arg.Args.Skip(9));

					SelectItemUi(player, category, page, catPage, craftId, itemsPage, itemId, selectedCategory,
						localPage,
						input);
					break;
				}

				case "takeitem":
				{
					int category, page, catPage, craftId, itemsPage, itemId;
					if (!arg.HasArgs(7) ||
					    !int.TryParse(arg.Args[1], out category) ||
					    !int.TryParse(arg.Args[2], out page) ||
					    !int.TryParse(arg.Args[3], out catPage) ||
					    !int.TryParse(arg.Args[4], out craftId) ||
					    !int.TryParse(arg.Args[5], out itemsPage) ||
					    !int.TryParse(arg.Args[6], out itemId))
						return;

					var shortName = arg.Args[7];
					if (string.IsNullOrEmpty(shortName)) return;

					_itemEditing[player]["ShortName"] = shortName;

					EditItemUi(player, category, page, catPage, craftId, itemsPage, itemId);
					break;
				}

				case "craft_ui":
				{
					int category, page, catPage, itemId, itemsPage, amount;
					if (!arg.HasArgs(7) || !int.TryParse(arg.Args[1], out category)
					                    || !int.TryParse(arg.Args[2], out page)
					                    || !int.TryParse(arg.Args[3], out catPage)
					                    || !int.TryParse(arg.Args[4], out itemId)
					                    || !int.TryParse(arg.Args[5], out itemsPage)
					                    || !int.TryParse(arg.Args[6], out amount))
						return;

					amount = Mathf.Max(amount, 1);

					CraftUi(player, category, page, catPage, itemId, itemsPage, amount);
					break;
				}
			}
		}

		#endregion

		#region Interface

		private void MainUi(BasePlayer player, int category = 0, int page = 0, int catPage = 0, bool first = false)
		{
			var categories = GetPlayerCategories(player);
			if (categories == null) return;

			var updateCooldown = new PlayerUpdateCooldown(category, page, catPage);

			var catsOnPage = _config.UI.CategoriesAmountOnPage;

			var container = new CuiElementContainer();

			#region Background

			int totalAmount;
			float margin;
			float ySwitch;
			float height;

			if (first)
			{
				container.Add(new CuiPanel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Image =
					{
						Color = "0 0 0 0.9",
						Material = "assets/content/ui/uibackgroundblur.mat"
					},
					CursorEnabled = true
				}, "Overlay", Layer, Layer);

				if (!string.IsNullOrEmpty(_config.UI.BackgroundImage))
					container.Add(new CuiElement
					{
						Parent = Layer,
						Components =
						{
							new CuiRawImageComponent
								{Png = GetImage(_config.UI.BackgroundImage)},
							new CuiRectTransformComponent
							{
								AnchorMin = "0 0", AnchorMax = "1 1"
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
						Close = Layer,
						Command = "UI_Crafts close"
					}
				}, Layer);

				#region Workbenches

				totalAmount = 3;
				height = 15f;
				margin = 5f;

				ySwitch = (totalAmount * height + (totalAmount - 1) * margin) / 2f;

				for (var wb = 1; wb <= totalAmount; wb++)
				{
					container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "1 0.5", AnchorMax = "1 0.5",
								OffsetMin = $"-235 {ySwitch - height}",
								OffsetMax = $"0 {ySwitch}"
							},
							Image =
							{
								Color = "0 0 0 0"
							}
						}, Layer, Layer + $".Workbench.{wb}");

					container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "0 0.5", AnchorMax = "0 0.5",
								OffsetMin = "0 -1.5", OffsetMax = "40 1.5"
							},
							Image =
							{
								Color = _config.Workbenches[(WorkbenchLevel) wb].Get
							}
						}, Layer + $".Workbench.{wb}");

					container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "45 0", OffsetMax = "0 0"
							},
							Text =
							{
								Text = Msg(player, WorkbenchLvl, wb),
								Align = TextAnchor.MiddleLeft,
								Font = "robotocondensed-regular.ttf",
								FontSize = 12,
								Color = "1 1 1 1"
							}
						}, Layer + $".Workbench.{wb}");

					ySwitch = ySwitch - height - margin;
				}

				#endregion

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0",
						OffsetMin = "-200 0", OffsetMax = "200 130"
					},
					Text =
					{
						Text = Msg(player, CraftsDescription),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 14,
						Color = "1 1 1 0.5"
					}
				}, Layer);
			}

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-350 -225",
					OffsetMax = "350 225"
				},
				Image =
				{
					Color = _config.UI.Color1.Get
				}
			}, Layer, Layer + ".Main", Layer + ".Main");

			#region Header

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -50",
					OffsetMax = "0 0"
				},
				Image = {Color = _config.UI.Color2.Get}
			}, Layer + ".Main", Layer + ".Header");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "30 0",
					OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, TitleMenu),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = _config.UI.Color3.Get
				}
			}, Layer + ".Header");

			var xSwitch = -25f;
			float width = 25;

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
					Color = _config.UI.Color3.Get
				},
				Button =
				{
					Close = Layer,
					Color = _config.UI.Color4.Get,
					Command = "UI_Crafts close"
				}
			}, Layer + ".Header");

			xSwitch = xSwitch - width - 5;

			if (CanEdit(player))
			{
				width = 90;
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
						Text = Msg(player, CraftCreate),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 10,
						Color = _config.UI.Color3.Get
					},
					Button =
					{
						Color = _config.UI.Color1.Get,
						Command = $"UI_Crafts start_edit {category} {page} {catPage} -1"
					}
				}, Layer + ".Header");

				xSwitch = xSwitch - width - 5;
			}

			if (_config.Economy.Show)
			{
				width = 90;
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
						Color = _config.UI.Color4.Get
					}
				}, Layer + ".Header", Layer + ".Header.Balance");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1"
					},
					Text =
					{
						Text = Msg(player, BalanceTitle, _config.Economy.ShowBalance(player)),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 10,
						Color = _config.UI.Color3.Get
					}
				}, Layer + ".Header.Balance");

				xSwitch = xSwitch - width - 5;
			}

			width = 25;

			#endregion

			#region Categories.Pages

			if (categories.Count > catsOnPage)
			{
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
						Text = Msg(player, BtnNext),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 10,
						Color = _config.UI.Color3.Get
					},
					Button =
					{
						Color = _config.UI.Color1.Get,
						Command = categories.Count > (catPage + 1) * catsOnPage
							? $"UI_Crafts page {category} {page} {catPage + 1}"
							: ""
					}
				}, Layer + ".Header");

				xSwitch = xSwitch - width - 5;
				width = 25;

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
						Text = Msg(player, BtnBack),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 10,
						Color = _config.UI.Color3.Get
					},
					Button =
					{
						Color = _config.UI.Color1.Get,
						Command = catPage != 0
							? $"UI_Crafts page {category} {page} {catPage - 1}"
							: ""
					}
				}, Layer + ".Header");
			}

			#endregion

			#region Categories

			width = _config.UI.CatWidth;
			margin = _config.UI.CatMargin;

			xSwitch = 25f;

			var v = 0;
			foreach (var cat in categories.SkipAndTake(catPage * catsOnPage, catsOnPage))
			{
				var index = v + catPage * catsOnPage;

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = $"{xSwitch} {-70 - _config.UI.CatHeight}",
						OffsetMax = $"{xSwitch + width} -70"
					},
					Text =
					{
						Text = $"{cat.Title}",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = index == category ? _config.UI.Color4.Get : cat.Color.Get,
						Command = $"UI_Crafts page {index} 0 {catPage}"
					}
				}, Layer + ".Main");

				xSwitch += width + margin;
				v++;
			}

			#endregion

			#region Crafts

			var lines = _config.UI.Craft.MaxLines;
			totalAmount = _config.UI.Craft.AmountOnString * lines;

			xSwitch = -(_config.UI.Craft.AmountOnString * _config.UI.Craft.Width +
			            (_config.UI.Craft.AmountOnString - 1) * _config.UI.Craft.Margin) / 2f;
			ySwitch = _config.UI.Craft.IndentY;

			var playerCrafts = GetPlayerCrafts(player, categories[category]);
			var crafts = playerCrafts.SkipAndTake(page * totalAmount, totalAmount);

			if (crafts.Count > 0)
				for (var i = 0; i < crafts.Count; i++)
				{
					var craft = crafts[i];

					container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "0.5 1", AnchorMax = "0.5 1",
								OffsetMin = $"{xSwitch} {ySwitch - _config.UI.Craft.Height}",
								OffsetMax = $"{xSwitch + _config.UI.Craft.Width} {ySwitch}"
							},
							Image =
							{
								Color = _config.UI.Color2.Get
							}
						}, Layer + ".Main", Layer + $".Craft.{craft.PUBLIC_ID}");

					if (ImageLibrary != null && ImageLibrary.IsLoaded)
					{
						if (!string.IsNullOrEmpty(craft.Image))
							container.Add(new CuiElement
							{
								Parent = Layer + $".Craft.{craft.PUBLIC_ID}",
								Components =
								{
									new CuiRawImageComponent
									{
										Png = GetImage(craft.Image)
									},
									_config.UI.Craft.Image.GetPosition()
								}
							});
						else
						{
							container.Add(new CuiElement
							{
								Parent = Layer + $".Craft.{craft.PUBLIC_ID}",
								Components =
								{
									new CuiImageComponent()
									{
										ItemId = craft.itemId,
										SkinId = craft.SkinID
									},
									_config.UI.Craft.Image.GetPosition()
								}
							});
						}
					}

					container.Add(_config.UI.Craft.Title?.GetText(
						craft.PublicTitle,
						Layer + $".Craft.{craft.PUBLIC_ID}"));

					if (!string.IsNullOrEmpty(craft.Description))
						container.Add(_config.UI.Craft.Description?.GetText(
							craft.Description,
							Layer + $".Craft.{craft.PUBLIC_ID}"));

					if (craft.Level > 0)
						container.Add(new CuiElement()
						{
							Parent = Layer + $".Craft.{craft.PUBLIC_ID}",
							Components =
							{
								new CuiImageComponent()
								{
									Color = _config.Workbenches[craft.Level].Get
								},
								_config.UI.Craft.WorkbenchLevelLine.GetPosition()
							}
						});

					if (CooldownUi(ref container, player, craft, category, page, catPage))
						updateCooldown.Items.Add(craft);

					if (CanEdit(player))
					{
						if (!craft.Enabled)
							container.Add(new CuiPanel
								{
									RectTransform =
									{
										AnchorMin = "0 1", AnchorMax = "0 1",
										OffsetMin = "5 -15", OffsetMax = "15 -5"
									},
									Image =
									{
										Color = _config.UI.Color4.Get
									}
								}, Layer + $".Craft.{craft.PUBLIC_ID}");

						container.Add(new CuiButton
							{
								RectTransform =
								{
									AnchorMin = "1 1", AnchorMax = "1 1",
									OffsetMin = "-15 -15", OffsetMax = "-5 -5"
								},
								Text =
								{
									Text = ""
								},
								Button =
								{
									Color = "1 1 1 1",
									Sprite = "assets/icons/gear.png",
									Command = $"UI_Crafts start_edit {category} {page} {catPage} {craft.PUBLIC_ID}"
								}
							}, Layer + $".Craft.{craft.PUBLIC_ID}");
					}

					if ((i + 1) % _config.UI.Craft.AmountOnString == 0)
					{
						ySwitch = ySwitch - _config.UI.Craft.Height - _config.UI.Craft.Margin;
						xSwitch = -(_config.UI.Craft.AmountOnString * _config.UI.Craft.Width +
						            (_config.UI.Craft.AmountOnString - 1) * _config.UI.Craft.Margin) / 2f;
					}
					else
					{
						xSwitch += _config.UI.Craft.Width + _config.UI.Craft.Margin;
					}
				}

			#endregion

			#region Pages

			var pageSize = _config.UI.PageSize;
			var selPageSize = _config.UI.PageSelectedSize;
			margin = _config.UI.PagesMargin;

			var pages = Mathf.CeilToInt((float) playerCrafts.Count / totalAmount);
			if (pages > 1)
			{
				xSwitch = -((pages - 1) * pageSize + (pages - 1) * margin + selPageSize) / 2f;

				for (var j = 0; j < pages; j++)
				{
					var selected = page == j;

					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0",
							OffsetMin = $"{xSwitch} 10",
							OffsetMax =
								$"{xSwitch + (selected ? selPageSize : pageSize)} {10 + (selected ? selPageSize : pageSize)}"
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
							Color = _config.UI.Color4.Get,
							Command = $"UI_Crafts page {category} {j} {catPage}"
						}
					}, Layer + ".Main");

					xSwitch += (selected ? selPageSize : pageSize) + margin;
				}
			}

			#endregion

			#endregion

			CuiHelper.AddUi(player, container);

			_updateCooldown[player] = updateCooldown;
		}

		private void CraftUi(BasePlayer player, int category, int page, int catPage, int itemId,
			int itemsPage = 0,
			int amount = 1)
		{
			var craft = GetPlayerCategories(player)[category].Crafts.Find(x => x.PUBLIC_ID == itemId);
			if (craft == null) return;

			var bypass = HasBypass(player);

			amount = Mathf.Max(amount, 1);

			if (craft.MaxAmount > 0)
				amount = Mathf.Min(amount, craft.MaxAmount);
			
			var container = new CuiElementContainer();

			#region Background

			container.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image =
				{
					Color = "0.19 0.19 0.18 0.3",
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
					Command = $"UI_Crafts back {category} {page} {catPage}"
				}
			}, Layer);

			if (!string.IsNullOrEmpty(_config.UI.BackgroundImage))
				container.Add(new CuiElement
				{
					Parent = Layer,
					Components =
					{
						new CuiRawImageComponent
							{Png = GetImage(_config.UI.BackgroundImage)},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0", AnchorMax = "1 1"
						}
					}
				});

			#endregion

			#region Title

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-250 -230", OffsetMax = "250 -165"
				},
				Text =
				{
					Text = Msg(player, CraftTitle, craft.PublicTitle.ToUpper()),
					Align = TextAnchor.LowerCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 24,
					Color = "1 1 1 0.4"
				}
			}, Layer);

			#endregion

			#region Items

			var notItem = false;

			var items = craft.Items.SkipAndTake(itemsPage * _config.UI.CraftMenuSettings.MaxItemsOnString,
				_config.UI.CraftMenuSettings.MaxItemsOnString);

			var xSwitch = -(items.Count * _config.UI.CraftMenuSettings.ItemWidth +
			                (items.Count - 1) * _config.UI.CraftMenuSettings.ItemMargin) /
			              2f;

			var ySwitch = -_config.UI.CraftMenuSettings.ItemUpIndent;

			var allItems = Pool.Get<List<Item>>();
			player.inventory.GetAllItems(allItems);

			items.ForEach(item =>
			{
				container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = $"{xSwitch} {ySwitch - _config.UI.CraftMenuSettings.ItemHeight}",
							OffsetMax = $"{xSwitch + _config.UI.CraftMenuSettings.ItemWidth} {ySwitch}"
						},
						Image =
						{
							Color = "0 0 0 0"
						}
					}, Layer, Layer + $".Item.{xSwitch}");

				if (!string.IsNullOrEmpty(item.Image))
					container.Add(new CuiElement
					{
						Parent = Layer + $".Item.{xSwitch}",
						Components =
						{
							new CuiRawImageComponent
							{
								Png = GetImage(item.Image)
							},
							new CuiRectTransformComponent
							{
								AnchorMin = "0.5 1", AnchorMax = "0.5 1",
								OffsetMin = "-64 -128", OffsetMax = "64 0"
							}
						}
					});
				else
					container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "0.5 1", AnchorMax = "0.5 1",
								OffsetMin = "-64 -128", OffsetMax = "64 0"
							},
							Image =
							{
								ItemId = item.itemId,
								SkinId = item.SkinID
							}
						}, Layer + $".Item.{xSwitch}");

				container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0",
							OffsetMin = "-100 10", OffsetMax = "100 30"
						},
						Text =
						{
							Text = item.GetItemDisplayName(player),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 16,
							Color = "1 1 1 0.5"
						}
					}, Layer + $".Item.{xSwitch}");

				container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0",
							OffsetMin = "-100 -25", OffsetMax = "100 0"
						},
						Text =
						{
							Text = Msg(player, CraftItemAmount, item.Amount * amount),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 16,
							Color = "1 1 1 0.5"
						}
					}, Layer + $".Item.{xSwitch}");

				var hasAmount = HasAmount(allItems, item.ShortName, item.SkinID, item.Amount * amount);
				if (!hasAmount)
					notItem = true;

				container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0",
							OffsetMin = "-50 0", OffsetMax = "50 3"
						},
						Image =
						{
							Color = hasAmount ? _config.UI.Color5.Get : _config.UI.Color6.Get,
							Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
						}
					}, Layer + $".Item.{xSwitch}");

				xSwitch += _config.UI.CraftMenuSettings.ItemWidth + _config.UI.CraftMenuSettings.ItemMargin;
			});

			Pool.FreeUnmanaged(ref allItems);
			
			#endregion

			#region Items.Pages

			if (craft.Items.Count > _config.UI.CraftMenuSettings.MaxItemsOnString)
			{
				if (itemsPage != 0)
					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0 0.5", AnchorMax = "0 0.5",
							OffsetMin = "10 -105",
							OffsetMax = "40 -75"
						},
						Text =
						{
							Text = Msg(player, BtnBack),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 18,
							Color = "1 1 1 1"
						},
						Button =
						{
							Color = _config.UI.Color4.Get,
							Command =
								$"UI_Crafts craft_ui {category} {page} {catPage} {itemId} {itemsPage - 1} {amount}"
						}
					}, Layer);

				if (craft.Items.Count > (itemsPage + 1) * _config.UI.CraftMenuSettings.MaxItemsOnString)
					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "1 0.5", AnchorMax = "1 0.5",
							OffsetMin = "-40 -105",
							OffsetMax = "-10 -75"
						},
						Text =
						{
							Text = Msg(player, BtnNext),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 18,
							Color = "1 1 1 1"
						},
						Button =
						{
							Color = _config.UI.Color4.Get,
							Command =
								$"UI_Crafts craft_ui {category} {page} {catPage} {itemId} {itemsPage + 1} {amount}"
						}
					}, Layer);
			}

			#endregion

			#region Buttons

			var width = 110f;
			var margin = 20f;

			var maxAmount = 1;

			if (craft.Craft)
				maxAmount++;

			if (craft.Sale)
				maxAmount++;

			xSwitch = -(maxAmount * width + (maxAmount - 1) * margin) / 2f;

			if (craft.Craft)
			{
				if (bypass && notItem)
					notItem = false;

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = $"{xSwitch} -530",
						OffsetMax = $"{xSwitch + width} -485"
					},
					Text =
					{
						Text = Msg(player, CraftButton),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 12,
						Color = notItem ? "1 1 1 0.7" : "1 1 1 1"
					},
					Button =
					{
						Color = notItem ? _config.UI.Color7.Get : _config.UI.Color4.Get,
						Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
						Command = notItem ? "" : $"UI_Crafts craft {category} {page} {catPage} {itemId} {amount}",
						Close = _config.UI.CloseAfterCraft ? Layer : string.Empty
					}
				}, Layer);

				xSwitch += width + margin;
			}

			if (craft.Sale)
			{
				var hasMoney = bypass || _config.Economy.ShowBalance(player) >= craft.Cost;

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = $"{xSwitch} -530",
						OffsetMax = $"{xSwitch + width} -485"
					},
					Text =
					{
						Text = Msg(player, BuyButton, craft.Cost),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 12,
						Color = hasMoney ? "1 1 1 1" : "1 1 1 0.7"
					},
					Button =
					{
						Color = hasMoney ? _config.UI.Color4.Get : _config.UI.Color7.Get,
						Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
						Command = hasMoney ? $"UI_Crafts buy_craft {category} {page} {catPage} {itemId} {amount}" : "",
						Close = _config.UI.CloseAfterCraft ? Layer : string.Empty
					}
				}, Layer);

				xSwitch += width + margin;
			}


			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = $"{xSwitch} -530",
					OffsetMax = $"{xSwitch + width} -485"
				},
				Text =
				{
					Text = Msg(player, CraftCancelButton),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 12,
					Color = "1 1 1 0.7"
				},
				Button =
				{
					Color = _config.UI.Color7.Get,
					Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
					Close = Layer,
					Command = $"UI_Crafts back {category} {page} {catPage}"
				}
			}, Layer);

			#endregion

			#region Amount

			if (craft.MultipleCraft)
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = "-100 -580",
						OffsetMax = "100 -550"
					},
					Image =
					{
						Color = "0 0 0 0"
					}
				}, Layer, Layer + ".Amount");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0",
						AnchorMax = "0.33 1"
					},
					Text =
					{
						Text = "Amount: ",
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 14,
						Color = "1 1 1 1"
					}
				}, Layer + ".Amount");

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.33 0",
						AnchorMax = "1 1"
					},
					Image =
					{
						Color = _config.UI.Color5.Get,
						Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
					}
				}, Layer + ".Amount", Layer + ".Amount.Label");

				container.Add(new CuiElement
				{
					Parent = Layer + ".Amount.Label",
					Components =
					{
						new CuiInputFieldComponent
						{
							FontSize = 12,
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							Command = $"UI_Crafts craft_ui {category} {page} {catPage} {itemId} {itemsPage} ",
							Color = "1 1 1 0.95",
							CharsLimit = 9,
							Text = $"{amount}",
							HudMenuInput = true
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 0", OffsetMax = "-5 0"
						}
					}
				});
			}

			#endregion

			#region Description

			if (craft.CraftMenuDescription?.Enabled == true)
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = _config.UI.CraftMenuSettings.Description.AnchorMin,
						AnchorMax = _config.UI.CraftMenuSettings.Description.AnchorMax,
						OffsetMin = _config.UI.CraftMenuSettings.Description.OffsetMin,
						OffsetMax = _config.UI.CraftMenuSettings.Description.OffsetMax
					},
					Image =
					{
						Color = _config.UI.CraftMenuSettings.Description.BackgroundColor.Get
					}
				}, Layer, Layer + ".Description");

				container.Add(new CuiLabel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text =
					{
						Text = craft.CraftMenuDescription.GetMessage(player),
						FontSize = _config.UI.CraftMenuSettings.Description.FontSize,
						Font = _config.UI.CraftMenuSettings.Description.Font ?? "robotocondensed-bold.ttf",
						Align = _config.UI.CraftMenuSettings.Description.Align,
						Color = _config.UI.CraftMenuSettings.Description.Color.Get
					}
				}, Layer + ".Description");
			}

			#endregion

			CuiHelper.DestroyUi(player, Layer);
			CuiHelper.AddUi(player, container);
		}

		private void EditUi(BasePlayer player, int category, int page, int catPage, int craftId, int itemsPage = 0)
		{
			#region Dictionary

			if (!_craftEditing.ContainsKey(player))
			{
				var craft = FindCraftById(craftId);
				if (craft != null)
					_craftEditing[player] = craft.ToDictionary();
				else
					_craftEditing[player] = new Dictionary<string, object>
					{
						["Generated"] = true,
						["Enabled"] = false,
						["Image"] = string.Empty,
						["Title"] = string.Empty,
						["Description"] = string.Empty,
						["CmdToGive"] = string.Empty,
						["Permission"] = string.Empty,
						["DisplayName"] = string.Empty,
						["ShortName"] = string.Empty,
						["Amount"] = 1,
						["SkinID"] = 0UL,
						["Type"] = CraftType.Command,
						["Prefab"] = string.Empty,
						["GiveCommand"] = string.Empty,
						["Level"] = WorkbenchLevel.None,
						["UseDistance"] = false,
						["Distance"] = 0f,
						["Ground"] = false,
						["Structure"] = false,
						["Craft"] = true,
						["Sales"] = false,
						["Cost"] = 100f,
						["Items"] = new List<ItemForCraft>()
					};
			}

			#endregion

			var edit = _craftEditing[player];

			var container = new CuiElementContainer();

			#region Background

			container.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image =
				{
					Color = _config.UI.Color9.Get
				}
			}, Layer, EditLayer + ".Background");

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text = {Text = ""},
				Button =
				{
					Color = "0 0 0 0",
					Close = EditLayer + ".Background",
					Command = "UI_Crafts stopedit"
				}
			}, EditLayer + ".Background");

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-240 -275",
					OffsetMax = "240 275"
				},
				Image =
				{
					Color = _config.UI.Color1.Get
				}
			}, EditLayer + ".Background", EditLayer);

			#region Header

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -50",
					OffsetMax = "0 0"
				},
				Image = {Color = _config.UI.Color2.Get}
			}, EditLayer, Layer + ".Header");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "30 0",
					OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, CraftEditingTitle),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = _config.UI.Color3.Get
				}
			}, Layer + ".Header");

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
					Color = _config.UI.Color3.Get
				},
				Button =
				{
					Close = EditLayer + ".Background",
					Color = _config.UI.Color4.Get,
					Command = "UI_Crafts stopedit"
				}
			}, Layer + ".Header");

			#endregion

			#region Image

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "10 -200",
					OffsetMax = "145 -65"
				},
				Image = {Color = _config.UI.Color2.Get}
			}, EditLayer, Layer + ".Image");

			if (!string.IsNullOrEmpty(edit["Image"].ToString()) || !string.IsNullOrEmpty(edit["ShortName"].ToString()))
			{
				if (!string.IsNullOrEmpty(edit["Image"].ToString()))
					container.Add(new CuiElement
					{
						Parent = Layer + ".Image",
						Components =
						{
							new CuiRawImageComponent
							{
								Png = GetImage(edit["Image"].ToString())
							},
							new CuiRectTransformComponent
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "5 5", OffsetMax = "-5 -5"
							}
						}
					});
				else
					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "5 5", OffsetMax = "-5 -5"
						},
						Image =
						{
							ItemId = ItemManager.FindItemDefinition(edit["ShortName"].ToString())?.itemid ?? 0,
							SkinId = Convert.ToUInt64(edit["SkinID"])
						}
					}, Layer + ".Image");
			}

			#region Input

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 0",
					OffsetMin = "0 -20", OffsetMax = "0 0"
				},
				Image =
				{
					Color = _config.UI.Color8.Get
				}
			}, Layer + ".Image", Layer + ".Image.Input");

			if (!string.IsNullOrEmpty(edit["Image"].ToString()))
				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "5 0", OffsetMax = "-5 0"
					},
					Text =
					{
						Text = $"{edit["Image"]}",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 0.45"
					}
				}, Layer + ".Image.Input");

			container.Add(new CuiElement
			{
				Parent = Layer + ".Image.Input",
				Components =
				{
					new CuiInputFieldComponent
					{
						FontSize = 10,
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-bold.ttf",
						Command = $"UI_Crafts edit {category} {page} {catPage} {craftId} {itemsPage} Image ",
						Color = "1 1 1 0.95",
						CharsLimit = 9,
						HudMenuInput = true,
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"
					}
				}
			});

			#endregion

			#endregion

			#region Types

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "155 -85",
					OffsetMax = "205 -65"
				},
				Text =
				{
					Text = Msg(player, CraftTypeTitle),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = "1 1 1 1"
				}
			}, EditLayer);

			var xSwitch = 155f;
			var width = 60f;
			var margin = 5f;

			var type = edit["Type"] as CraftType? ?? CraftType.Item;
			foreach (var craftType in Enum.GetValues(typeof(CraftType)).Cast<CraftType>())
			{
				var nowStatus = type == craftType;
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = $"{xSwitch} -105",
						OffsetMax = $"{xSwitch + width} -85"
					},
					Text =
					{
						Text = $"{craftType}",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = nowStatus ? _config.UI.Color10.Get : _config.UI.Color4.Get,
						Command = $"UI_Crafts edit {category} {page} {catPage} {craftId} {itemsPage} Type {craftType}"
					}
				}, EditLayer);

				xSwitch += width + margin;
			}

			#endregion

			#region Work Bench

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "155 -135",
					OffsetMax = "300 -115"
				},
				Text =
				{
					Text = Msg(player, CraftWorkbenchTitle),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = "1 1 1 1"
				}
			}, EditLayer);

			xSwitch = 155f;
			width = 76.25f;
			margin = 5f;

			foreach (var wbLevel in Enum.GetValues(typeof(WorkbenchLevel)).Cast<WorkbenchLevel>())
			{
				container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "0 1",
							OffsetMin = $"{xSwitch} -155",
							OffsetMax = $"{xSwitch + width} -135"
						},
						Text =
						{
							Text = $"{wbLevel}",
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 10,
							Color = "1 1 1 1"
						},
						Button =
						{
							Color = _config.Workbenches[wbLevel].Get,
							Command =
								$"UI_Crafts edit {category} {page} {catPage} {craftId} {itemsPage} Level {wbLevel}"
						}
					}, EditLayer, Layer + $".WorkBench.{wbLevel}");

				var lvl = (WorkbenchLevel) edit["Level"];
				if (lvl == wbLevel)
					CreateOutLine(ref container, Layer + $".WorkBench.{wbLevel}", _config.UI.Color2.Get);

				xSwitch += width + margin;
			}

			#endregion

			#region Prefab

			EditFieldUi(player, ref container, EditLayer, CuiHelper.GetGuid(),
				"-85 -215",
				"235 -165",
				$"UI_Crafts edit {category} {page} {catPage} {craftId} {itemsPage} Prefab ",
				new KeyValuePair<string, object>("Prefab", edit["Prefab"]));

			#endregion

			#region Fields

			width = 150f;
			margin = 5;
			var yMargin = 5f;
			var height = 45f;
			var ySwitch = -225f;
			var fieldsOnString = 3;

			var constSwitch = -(fieldsOnString * width + (fieldsOnString - 1) * margin) / 2f;
			xSwitch = constSwitch;

			var i = 1;
			foreach (var obj in _craftEditing[player]
				         .Where(x => x.Key != "Generated"
				                     && x.Key != "ID"
				                     && x.Key != "Prefab"
				                     && x.Key != "Enabled"
				                     && x.Key != "Craft"
				                     && x.Key != "Sales"
				                     && x.Key != "Type"
				                     && x.Key != "Level"
				                     && x.Key != "UseDistance"
				                     && x.Key != "Ground"
				                     && x.Key != "Structure"
				                     && x.Key != "Items"))
			{
				EditFieldUi(player, ref container, EditLayer, Layer + $".Editing.{i}",
					$"{xSwitch} {ySwitch - height}",
					$"{xSwitch + width} {ySwitch}",
					$"UI_Crafts edit {category} {page} {catPage} {craftId} {itemsPage} {obj.Key} ",
					obj);

				if (i % fieldsOnString == 0)
				{
					ySwitch = ySwitch - height - yMargin;
					xSwitch = constSwitch;
				}
				else
				{
					xSwitch += width + margin;
				}

				i++;
			}

			#endregion

			#region Items

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "10 -445",
					OffsetMax = "100 -425"
				},
				Text =
				{
					Text = Msg(player, CraftItemsTitle),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = "1 1 1 1"
				}
			}, EditLayer);

			var amountOnString = 7;
			width = 60f;
			height = 60f;
			margin = 5f;

			ySwitch = -450f;

			xSwitch = 10f;

			var items = (List<ItemForCraft>) edit["Items"];
			if (items != null)
			{
				foreach (var craftItem in items.SkipAndTake(amountOnString * itemsPage, amountOnString))
				{
					container.Add(new CuiPanel
						{
							RectTransform =
							{
								AnchorMin = "0 1", AnchorMax = "0 1",
								OffsetMin = $"{xSwitch} {ySwitch - height}",
								OffsetMax = $"{xSwitch + width} {ySwitch}"
							},
							Image =
							{
								Color = _config.UI.Color2.Get
							}
						}, EditLayer, Layer + $".Craft.Item.{xSwitch}");

					if (!string.IsNullOrEmpty(craftItem.Image))
						container.Add(new CuiElement
						{
							Parent = Layer + $".Craft.Item.{xSwitch}",
							Components =
							{
								new CuiRawImageComponent
								{
									Png = GetImage(craftItem.Image)
								},
								new CuiRectTransformComponent
								{
									AnchorMin = "0 0", AnchorMax = "1 1",
									OffsetMin = "5 5", OffsetMax = "-5 -5"
								}
							}
						});
					else
						container.Add(new CuiPanel
							{
								RectTransform =
								{
									AnchorMin = "0 0", AnchorMax = "1 1",
									OffsetMin = "5 5", OffsetMax = "-5 -5"
								},
								Image =
								{
									ItemId = craftItem.itemId,
									SkinId = craftItem.SkinID
								}
							}, Layer + $".Craft.Item.{xSwitch}");

					container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "0 2",
								OffsetMax = "-2 0"
							},
							Text =
							{
								Text = $"{craftItem.Amount}",
								Align = TextAnchor.LowerRight,
								Font = "robotocondensed-regular.ttf",
								FontSize = 10,
								Color = "1 1 1 0.9"
							}
						}, Layer + $".Craft.Item.{xSwitch}");

					container.Add(new CuiButton
						{
							RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
							Text = {Text = ""},
							Button =
							{
								Color = "0 0 0 0",
								Command =
									$"UI_Crafts start_edititem {category} {page} {catPage} {craftId} {itemsPage} {craftItem.ID}"
							}
						}, Layer + $".Craft.Item.{xSwitch}");

					xSwitch += margin + width;
				}

				#region Buttons

				#region Add

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = "45 -445",
						OffsetMax = "65 -425"
					},
					Text =
					{
						Text = Msg(player, CraftItemsAddTitle),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _config.UI.Color4.Get,
						Command = $"UI_Crafts start_edititem {category} {page} {catPage} {craftId} {itemsPage} -1"
					}
				}, EditLayer);

				#endregion

				#region Back

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = "70 -445",
						OffsetMax = "90 -425"
					},
					Text =
					{
						Text = Msg(player, BtnBack),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _config.UI.Color4.Get,
						Command = itemsPage != 0
							? $"UI_Crafts edit_page {category} {page} {catPage} {craftId} {itemsPage - 1}"
							: ""
					}
				}, EditLayer);

				#endregion

				#region Next

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = "95 -445",
						OffsetMax = "115 -425"
					},
					Text =
					{
						Text = Msg(player, BtnNext),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _config.UI.Color4.Get,
						Command = items.Count > (itemsPage + 1) * amountOnString
							? $"UI_Crafts edit_page {category} {page} {catPage} {craftId} {itemsPage + 1}"
							: ""
					}
				}, EditLayer);

				#endregion

				#endregion
			}

			#endregion

			#endregion

			#region Params

			xSwitch = constSwitch;

			ySwitch = ySwitch - height - 10f;

			#region Enabled

			var enabled = Convert.ToBoolean(_craftEditing[player]["Enabled"]);

			var text = Msg(player, EnableCraft);

			CheckBoxUi(ref container, EditLayer, Layer + ".Editing.Enabled", "0.5 1", "0.5 1",
				$"{xSwitch} {ySwitch - 10}",
				$"{xSwitch + 10} {ySwitch}",
				enabled,
				$"UI_Crafts edit {category} {page} {catPage} {craftId} {itemsPage} Enabled {!enabled}",
				text
			);

			xSwitch += (text.Length + 1) * 10 * 0.5f + 10;

			#endregion

			#region Craft

			var craftEnabled = Convert.ToBoolean(_craftEditing[player]["Craft"]);

			text = Msg(player, EnableCrafting);

			CheckBoxUi(ref container, EditLayer, Layer + ".Editing.Enabled", "0.5 1", "0.5 1",
				$"{xSwitch} {ySwitch - 10}",
				$"{xSwitch + 10} {ySwitch}",
				craftEnabled,
				$"UI_Crafts edit {category} {page} {catPage} {craftId} {itemsPage} Craft {!craftEnabled}",
				text
			);

			xSwitch += (text.Length + 1) * 10 * 0.5f + 10;

			#endregion

			#region Sales

			var sales = Convert.ToBoolean(_craftEditing[player]["Sales"]);

			text = Msg(player, EnableSales);

			CheckBoxUi(ref container, EditLayer, Layer + ".Editing.Enabled", "0.5 1", "0.5 1",
				$"{xSwitch} {ySwitch - 10}",
				$"{xSwitch + 10} {ySwitch}",
				sales,
				$"UI_Crafts edit {category} {page} {catPage} {craftId} {itemsPage} Sales {!sales}",
				text
			);

			xSwitch += (text.Length + 1) * 10 * 0.5f + 10;

			#endregion

			#region UseDistance

			var useDistance = Convert.ToBoolean(_craftEditing[player]["UseDistance"]);

			text = Msg(player, EditUseDistance);

			CheckBoxUi(ref container, EditLayer, Layer + ".Editing.UseDistance", "0.5 1", "0.5 1",
				$"{xSwitch} {ySwitch - 10}",
				$"{xSwitch + 10} {ySwitch}",
				useDistance,
				$"UI_Crafts edit {category} {page} {catPage} {craftId} {itemsPage} UseDistance {!useDistance}",
				text
			);

			xSwitch += (text.Length + 1) * 10 * 0.5f + 10;

			#endregion

			#region Ground

			var ground = Convert.ToBoolean(_craftEditing[player]["Ground"]);

			text = Msg(player, EditGround);

			CheckBoxUi(ref container, EditLayer, Layer + ".Editing.Ground", "0.5 1", "0.5 1",
				$"{xSwitch} {ySwitch - 10}",
				$"{xSwitch + 10} {ySwitch}",
				ground,
				$"UI_Crafts edit {category} {page} {catPage} {craftId} {itemsPage} Ground {!ground}",
				text
			);

			xSwitch += (text.Length + 1) * 10 * 0.5f + 10;

			#endregion

			#region Structure

			var structure = Convert.ToBoolean(_craftEditing[player]["Structure"]);

			text = Msg(player, EnableStructure);

			CheckBoxUi(ref container, EditLayer, Layer + ".Editing.Structure", "0.5 1", "0.5 1",
				$"{xSwitch} {ySwitch - 10}",
				$"{xSwitch + 10} {ySwitch}",
				structure,
				$"UI_Crafts edit {category} {page} {catPage} {craftId} {itemsPage} Structure {!structure}",
				text
			);

			#endregion

			#endregion

			#region Buttons

			var generated = Convert.ToBoolean(_craftEditing[player]["Generated"]);

			#region Save

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 0", AnchorMax = "0.5 0",
					OffsetMin = $"{(generated ? -90 : -105)} -12",
					OffsetMax = $"{(generated ? 90 : 75)} 12"
				},
				Text =
				{
					Text = Msg(player, CraftSaveTitle),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 14,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = _config.UI.Color4.Get,
					Command = $"UI_Crafts save_edit {category} {page} {catPage} {craftId}"
				}
			}, EditLayer);

			#endregion

			#region Delete

			if (!generated)
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0",
						OffsetMin = "80 -12",
						OffsetMax = "110 12"
					},
					Text =
					{
						Text = Msg(player, CraftRemoveTitle),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 14,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _config.UI.Color11.Get,
						Command = $"UI_Crafts delete_edit {category} {page} {catPage} {craftId}"
					}
				}, EditLayer);

			#endregion

			#endregion

			CuiHelper.DestroyUi(player, EditLayer + ".Background");
			CuiHelper.AddUi(player, container);
		}

		private void EditFieldUi(BasePlayer player, ref CuiElementContainer container,
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
					OffsetMin = "0 -15", OffsetMax = "0 0"
				},
				Text =
				{
					Text = $"{Msg(player, obj.Key)}".Replace("_", " "),
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

			container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "10 0", OffsetMax = "0 0"
					},
					Text =
					{
						Text = $"{obj.Value}",
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 0.15"
					}
				}, $"{name}.Value");

			CreateOutLine(ref container, $"{name}.Value", _config.UI.Color2.Get);

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
						Color = "1 1 1 0.99",
						CharsLimit = 150
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "10 0", OffsetMax = "0 0"
					}
				}
			});
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

			CreateOutLine(ref container, name, _config.UI.Color4.Get, 1);

			if (enabled)
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1"
					},
					Image = {Color = _config.UI.Color4.Get}
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

		private void EditItemUi(BasePlayer player, int category, int page, int catPage, int craftId, int itemsPage,
			int itemId)
		{
			#region Dictionary

			if (!_itemEditing.ContainsKey(player))
			{
				var itemById = FindItemById(itemId);
				if (itemById != null)
					_itemEditing[player] = itemById.ToDictionary();
				else
					_itemEditing[player] = new Dictionary<string, object>
					{
						["Generated"] = true,
						["ID"] = 0,
						["Image"] = string.Empty,
						["ShortName"] = string.Empty,
						["Amount"] = 1,
						["SkinID"] = 0UL,
						["Title"] = string.Empty
					};
			}

			#endregion

			var edit = _itemEditing[player];

			var container = new CuiElementContainer();

			#region Background

			container.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image =
				{
					Color = _config.UI.Color9.Get
				}
			}, Layer, EditLayer + ".Background");

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text = {Text = ""},
				Button =
				{
					Color = "0 0 0 0",
					Command = $"UI_Crafts edit_page {category} {page} {catPage} {craftId} {itemsPage}"
				}
			}, EditLayer + ".Background");

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-240 -120",
					OffsetMax = "240 120"
				},
				Image =
				{
					Color = _config.UI.Color1.Get
				}
			}, EditLayer + ".Background", EditLayer);

			#region Header

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -50",
					OffsetMax = "0 0"
				},
				Image = {Color = _config.UI.Color2.Get}
			}, EditLayer, EditLayer + ".Header");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "30 0",
					OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, ItemEditingTitle),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = _config.UI.Color3.Get
				}
			}, EditLayer + ".Header");

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
					Color = _config.UI.Color3.Get
				},
				Button =
				{
					Color = _config.UI.Color4.Get,
					Command = $"UI_Crafts edit_page {category} {page} {catPage} {craftId} {itemsPage}"
				}
			}, EditLayer + ".Header");

			#endregion

			#region Image

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "10 -200",
					OffsetMax = "145 -65"
				},
				Image = {Color = _config.UI.Color2.Get}
			}, EditLayer, EditLayer + ".Image");

			if (!string.IsNullOrEmpty(edit["Image"].ToString()) || !string.IsNullOrEmpty(edit["ShortName"].ToString()))
			{
				if (!string.IsNullOrEmpty(edit["Image"].ToString()))
					container.Add(new CuiElement
					{
						Parent = EditLayer + ".Image",
						Components =
						{
							new CuiRawImageComponent
							{
								Png = GetImage(edit["Image"].ToString())
							},
							new CuiRectTransformComponent
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "5 5", OffsetMax = "-5 -5"
							}
						}
					});
				else
					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "5 5", OffsetMax = "-5 -5"
						},
						Image =
						{
							ItemId = ItemManager.FindItemDefinition(edit["ShortName"].ToString())?.itemid ?? 0,
							SkinId = Convert.ToUInt64(edit["SkinID"])
						}
					}, EditLayer + ".Image");
			}

			#region Input

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 0",
					OffsetMin = "0 -20", OffsetMax = "0 0"
				},
				Image =
				{
					Color = _config.UI.Color8.Get
				}
			}, EditLayer + ".Image", EditLayer + ".Image.Input");

			if (!string.IsNullOrEmpty(edit["Image"].ToString()))
				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "5 0", OffsetMax = "-5 0"
					},
					Text =
					{
						Text = $"{edit["Image"]}",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 0.45"
					}
				}, EditLayer + ".Image.Input");

			container.Add(new CuiElement
			{
				Parent = EditLayer + ".Image.Input",
				Components =
				{
					new CuiInputFieldComponent
					{
						FontSize = 10,
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-bold.ttf",
						Command = $"UI_Crafts edit {category} {page} {catPage} {craftId} {itemsPage} Image ",
						Color = "1 1 1 0.95",
						CharsLimit = 9
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"
					}
				}
			});

			#endregion

			#endregion

			#region Title

			EditFieldUi(player, ref container, EditLayer, CuiHelper.GetGuid(),
				"-85 -105",
				"235 -65",
				$"UI_Crafts edititem {category} {page} {catPage} {craftId} {itemsPage} {itemId} Title ",
				new KeyValuePair<string, object>("Title", edit["Title"]));

			#endregion

			#region Amount

			EditFieldUi(player, ref container, EditLayer, CuiHelper.GetGuid(),
				"-85 -155",
				"70 -115",
				$"UI_Crafts edititem {category} {page} {catPage} {craftId} {itemsPage} {itemId} Amount ",
				new KeyValuePair<string, object>("Amount", edit["Amount"]));

			#endregion

			#region Skin

			EditFieldUi(player, ref container, EditLayer, CuiHelper.GetGuid(),
				"80 -155",
				"235 -115",
				$"UI_Crafts edititem {category} {page} {catPage} {craftId} {itemsPage} {itemId} SkinID ",
				new KeyValuePair<string, object>("SkinID", edit["SkinID"]));

			#endregion

			#region ShortName

			EditFieldUi(player, ref container, EditLayer, CuiHelper.GetGuid(),
				"-85 -205",
				"70 -165",
				$"UI_Crafts edititem {category} {page} {catPage} {craftId} {itemsPage} {itemId} Shortname ",
				new KeyValuePair<string, object>("ShortName", edit["ShortName"]));

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "80 -205", OffsetMax = "180 -185"
				},
				Text =
				{
					Text = Msg(player, CraftSelect),
					Align = TextAnchor.MiddleCenter,
					FontSize = 10,
					Font = "robotocondensed-regular.ttf",
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = _config.UI.Color4.Get,
					Command = $"UI_Crafts selectitem {category} {page} {catPage} {craftId} {itemsPage} {itemId}"
				}
			}, EditLayer);

			#endregion

			#region Buttons

			var creating = Convert.ToBoolean(_itemEditing[player]["Generated"]);

			#region Save

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 0", AnchorMax = "0.5 0",
					OffsetMin = $"{(creating ? -90 : -105)} -12",
					OffsetMax = $"{(creating ? 90 : 75)} 12"
				},
				Text =
				{
					Text = Msg(player, CraftSaveTitle),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 14,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = _config.UI.Color4.Get,
					Command = $"UI_Crafts saveitem {category} {page} {catPage} {craftId} {itemsPage} {itemId}"
				}
			}, EditLayer);

			#endregion

			#region Delete

			if (!creating)
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0",
						OffsetMin = "80 -12",
						OffsetMax = "110 12"
					},
					Text =
					{
						Text = Msg(player, CraftRemoveTitle),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 14,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = _config.UI.Color11.Get,
						Command = $"UI_Crafts removeitem {category} {page} {craftId} {itemsPage} {itemId}"
					}
				}, EditLayer);

			#endregion

			#endregion

			#endregion

			CuiHelper.DestroyUi(player, EditLayer + ".Background");
			CuiHelper.AddUi(player, container);
		}

		private void SelectItemUi(BasePlayer player, int category, int page, int catPage, int craftId, int itemsPage,
			int itemId,
			string selectedCategory = "",
			int localPage = 0,
			string input = "")
		{
			if (string.IsNullOrEmpty(selectedCategory)) selectedCategory = _itemsCategories.FirstOrDefault().Key;

			var container = new CuiElementContainer();

			#region Background

			container.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image =
				{
					Color = _config.UI.Color9.Get
				}
			}, Layer, EditLayer + ".Background");

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text = {Text = ""},
				Button =
				{
					Color = "0 0 0 0",
					Command = $"UI_Crafts start_edititem {category} {page} {catPage} {craftId} {itemsPage} {itemId}"
				}
			}, EditLayer + ".Background");

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
					Color = _config.UI.Color1.Get
				}
			}, EditLayer + ".Background", EditLayer);

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
							? _config.UI.Color4.Get
							: _config.UI.Color2.Get,
						Command =
							$"UI_Crafts selectitem {category} {page} {catPage} {craftId} {itemsPage} {itemId} {cat.Key}"
					}
				}, EditLayer);

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
			var Items = temp.SkipAndTake(localPage * totalAmount, totalAmount);

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
						Image = {Color = _config.UI.Color2.Get}
					}, EditLayer, EditLayer + $".Item.{item.Key}");

				container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "5 5", OffsetMax = "-5 -5"
						},
						Image =
						{
							ItemId = item.Key
						}
					}, EditLayer + $".Item.{item.Key}");

				container.Add(new CuiButton
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text = {Text = ""},
						Button =
						{
							Color = "0 0 0 0",
							Command =
								$"UI_Crafts takeitem {category} {page} {catPage} {craftId} {itemsPage} {itemId} {item.Value}"
						}
					}, EditLayer + $".Item.{item.Key}");

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
				Image = {Color = _config.UI.Color4.Get}
			}, EditLayer, EditLayer + ".Search");

			container.Add(new CuiElement
			{
				Parent = EditLayer + ".Search",
				Components =
				{
					new CuiInputFieldComponent
					{
						FontSize = 10,
						Align = TextAnchor.MiddleLeft,
						Command =
							$"UI_Crafts selectitem {category} {page} {catPage} {craftId} {itemsPage} {itemId} {selectedCategory} 0 ",
						Color = "1 1 1 0.95",
						CharsLimit = 150,
						Text = canSearch ? $"{input}" : Msg(player, ItemSearch),
						HudMenuInput = true,
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
					Color = _config.UI.Color2.Get,
					Command = localPage != 0
						? $"UI_Crafts selectitem {category} {page} {catPage} {craftId} {itemsPage} {itemId} {selectedCategory} {localPage - 1} {input}"
						: ""
				}
			}, EditLayer);

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
					Color = _config.UI.Color4.Get,
					Command = itemsAmount > (localPage + 1) * totalAmount
						? $"UI_Crafts selectitem {category} {page} {catPage} {craftId} {itemsPage} {itemId} {selectedCategory} {localPage + 1} {input}"
						: ""
				}
			}, EditLayer);

			#endregion

			#endregion

			CuiHelper.DestroyUi(player, EditLayer + ".Background");
			CuiHelper.AddUi(player, container);
		}

		private bool CooldownUi(ref CuiElementContainer container, BasePlayer player, CraftInfo craft, int category,
			int page,
			int catPage,
			bool update = false)
		{
			int leftTime;
			if (craft.HasCooldown(player, out leftTime))
			{
				if (update)
					CuiHelper.DestroyUi(player, Layer + $".Craft.{craft.PUBLIC_ID}.Cooldown");

				container.Add(new CuiPanel
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Image = {Color = "0 0 0 0.3", Material = "assets/content/ui/uibackgroundblur.mat"}
					}, Layer + $".Craft.{craft.PUBLIC_ID}", Layer + $".Craft.{craft.PUBLIC_ID}.Cooldown");

				container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
							OffsetMin = "-32.5 -10", OffsetMax = "32.5 10"
						},
						Image =
						{
							Color = _config.UI.Color1.Get
						}
					}, Layer + $".Craft.{craft.PUBLIC_ID}.Cooldown", Layer + $".Craft.{craft.PUBLIC_ID}.Cooldown.Text");

				container.Add(new CuiLabel
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text =
						{
							Text = $"{FormatShortTime(leftTime)}",
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 12,
							Color = "1 1 1 1"
						}
					}, Layer + $".Craft.{craft.PUBLIC_ID}.Cooldown.Text");

				return true;
			}

			if (update)
				CuiHelper.DestroyUi(player, Layer + $".Craft.{craft.PUBLIC_ID}.Btn");

			container.Add(new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text = {Text = ""},
					Button =
					{
						Color = "0 0 0 0",
						Command = $"UI_Crafts trycraft {category} {page} {catPage} {craft.PUBLIC_ID}"
					}
				}, Layer + $".Craft.{craft.PUBLIC_ID}", Layer + $".Craft.{craft.PUBLIC_ID}.Btn");

			return false;
		}

		#endregion

		#region Utils

		private void UnsubscribeHooks()
		{
			if (!_config.Recycler.UseSpeed)
				Unsubscribe(nameof(OnRecyclerToggle));
		}

		private bool HasBypass(BasePlayer player)
		{
			return player != null && permission.UserHasPermission(player.UserIDString, PermBypass);
		}

		private void LoadCustomWorkbenches()
		{
			_config.CustomWorkbench.ToList().ForEach(wb => InitCustomWorkbench(wb.Key, wb.Value));
		}

		private void InitCustomWorkbench(ulong netId, CustomWorkbenchConf conf)
		{
			var workBench = BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as Workbench;
			if (workBench == null)
			{
				_config.CustomWorkbench.Remove(netId);
				SaveConfig();
				return;
			}

			if (conf.SafeZone)
				new GameObject().AddComponent<SafeZoneComponent>().Activate(workBench, conf.SafeZoneRadius);
		}

		private void InitCustomWorkbench(Workbench workBench, CustomWorkbenchConf conf)
		{
			if (workBench == null) return;

			if (conf.SafeZone)
				new GameObject().AddComponent<SafeZoneComponent>().Activate(workBench, conf.SafeZoneRadius);
		}

		private void DestroyCustomWorkbenches()
		{
			for (var i = _safeZones.Count - 1; i >= 0; i--)
			{
				var safeZone = _safeZones[i];
				if (safeZone != null) safeZone.Kill();
			}
		}

		private void DestroyCars()
		{
			for (var i = _cars.Count - 1; i >= 0; i--)
			{
				var car = _cars[i];
				if (car != null) car.Kill();
			}
		}

		private void DestroyRecyclers()
		{
			for (var i = _recyclers.Count - 1; i >= 0; i--)
			{
				var recycler = _recyclers[i];
				if (recycler != null)
					recycler.Kill();
			}
		}

		private void DestroyUi()
		{
			foreach (var player in BasePlayer.activePlayerList)
			{
				CuiHelper.DestroyUi(player, Layer);
				CuiHelper.DestroyUi(player, EditLayer + ".Background");
			}
		}

		private void CheckCars()
		{
			if (!SpawnModularCar && _crafts.Exists(x => x.Enabled && x.Type == CraftType.ModularCar))
				PrintError("SpawnModularCar IS NOT INSTALLED.");
		}

		private static void SendEffect(string effect, Vector3 pos)
		{
			if (string.IsNullOrEmpty(effect)) return;

			Effect.server.Run(effect, pos);
		}

		#region Cooldown

		private readonly Dictionary<BasePlayer, PlayerUpdateCooldown> _updateCooldown =
			new Dictionary<BasePlayer, PlayerUpdateCooldown>();

		private class PlayerUpdateCooldown
		{
			public List<CraftInfo> Items = new List<CraftInfo>();

			public int category;
			public int page;
			public int catPage;

			public PlayerUpdateCooldown(int category, int page, int catPage)
			{
				this.category = category;
				this.page = page;
				this.catPage = catPage;
			}
		}

		private void UpdateCooldown()
		{
			var toRemove = Pool.Get<List<BasePlayer>>();

			_updateCooldown.ToList().ForEach(check =>
			{
				if (check.Key == null)
				{
					toRemove.Add(check.Key);
					return;
				}

				var container = new CuiElementContainer();

				check.Value.Items.ToList().ForEach(item =>
				{
					if (!CooldownUi(ref container, check.Key, item, check.Value.category, check.Value.page,
						    check.Value.catPage, true))
						check.Value.Items.Remove(item);
				});

				CuiHelper.AddUi(check.Key, container);

				if (check.Value.Items.Count <= 0) toRemove.Add(check.Key);
			});

			toRemove.ForEach(x => _updateCooldown.Remove(x));

			Pool.FreeUnmanaged(ref toRemove);
		}

		#endregion

		private static string FormatShortTime(int time)
		{
			return TimeSpan.FromSeconds(time).ToShortString();
		}

		private bool CanEdit(BasePlayer player)
		{
			return permission.UserHasPermission(player.UserIDString, PermEdit);
		}

		private void LoadItems()
		{
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

			foreach (var craft in _config.Categories.SelectMany(x => x.Crafts))
			{
				_craftsById[craft.PUBLIC_ID] = craft;

				craft.Items.ForEach(item => _instance._itemsById[item.ID] = item);
			}

			SaveConfig();
		}

		private ItemForCraft FindItemById(int id)
		{
			ItemForCraft item;
			return _itemsById.TryGetValue(id, out item) ? item : null;
		}

		private CraftInfo FindCraftById(int id)
		{
			CraftInfo craft;
			return _craftsById.TryGetValue(id, out craft) ? craft : null;
		}

		private int GetId()
		{
			var result = -1;

			do
			{
				var val = Random.Range(int.MinValue, int.MaxValue);

				if (!_crafts.Exists(craft => craft.PUBLIC_ID == val))
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

		#region Working With Images

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
			return Convert.ToBoolean(imageDatabase.HasImage(name));
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

			if (!string.IsNullOrEmpty(_config.UI.BackgroundImage))
				imagesList.TryAdd(_config.UI.BackgroundImage, _config.UI.BackgroundImage);

			_crafts.ForEach(craft =>
			{
				if (!string.IsNullOrEmpty(craft.Image))
					imagesList.TryAdd(craft.Image, craft.Image);

				craft.Items.ForEach(item =>
				{
					if (!string.IsNullOrEmpty(item.Image))
						imagesList.TryAdd(item.Image, item.Image);
				});
			});
			
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

		#endregion

		private void RegisterPermissions()
		{
			permission.RegisterPermission(PermEdit, this);

			permission.RegisterPermission(PermSetWb, this);

			permission.RegisterPermission(PermBypass, this);

			if (!string.IsNullOrEmpty(_config.Permission) && !permission.PermissionExists(_config.Permission))
				permission.RegisterPermission(_config.Permission, this);

			_config.Categories.ForEach(category =>
			{
				if (!string.IsNullOrEmpty(category.Permission) && !permission.PermissionExists(category.Permission))
					permission.RegisterPermission(category.Permission, this);

				category.Crafts.ForEach(item =>
				{
					if (!string.IsNullOrEmpty(item.Permission) && !permission.PermissionExists(item.Permission))
						permission.RegisterPermission(item.Permission, this);
				});
			});

			foreach (var workbench in _config.CustomWorkbench.SelectMany(check =>
				         check.Value.WorkBench.Where(workbench =>
					         !string.IsNullOrEmpty(workbench.Key) && !permission.PermissionExists(workbench.Key))))
				permission.RegisterPermission(workbench.Key, this);
		}

		private void RegisterCommands()
		{
			AddCovalenceCommand(_config.Commands, nameof(CmdOpenCrafts));

			AddCovalenceCommand(
				_crafts.FindAll(x => !string.IsNullOrEmpty(x.CmdToGive)).Select(x => x.CmdToGive).ToArray(),
				nameof(CmdGiveCrafts));

			AddCovalenceCommand("crafts.setwb", nameof(CmdSetCustomWb));
		}

		private List<Category> GetPlayerCategories(BasePlayer player)
		{
			CustomWorkbenchConf customWb;
			_openedCustomWb.TryGetValue(player.userID, out customWb);

			return _config.Categories.FindAll(cat => cat.Enabled && (string.IsNullOrEmpty(cat.Permission) ||
			                                                         permission.UserHasPermission(player.UserIDString,
				                                                         cat.Permission)) &&
			                                         (customWb == null || customWb.Categories.Contains("*") ||
			                                          customWb.Categories.Contains(cat.Title)));
		}

		private List<CraftInfo> GetPlayerCrafts(BasePlayer player, Category category)
		{
			return category.Crafts.FindAll(craft => (craft.Enabled || CanEdit(player)) &&
			                                        (string.IsNullOrEmpty(craft.Permission) ||
			                                         permission.UserHasPermission(player.UserIDString,
				                                         craft.Permission)));
		}

		private static Color HexToUnityColor(string hex)
		{
			if (string.IsNullOrEmpty(hex)) hex = "#FFFFFFFF";

			var str = hex.Trim('#');

			if (str.Length == 6)
				str += "FF";

			if (str.Length != 8) throw new Exception(hex);

			var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
			var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
			var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
			var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

			Color color = new Color32(r, g, b, a);

			return color;
		}

		private static void SetPlayerFlag(BasePlayer player, BasePlayer.PlayerFlags f, bool b)
		{
			if (b)
			{
				if (player.HasPlayerFlag(f)) return;
				player.playerFlags |= f;
			}
			else
			{
				if (!player.HasPlayerFlag(f)) return;
				player.playerFlags &= ~f;
			}

			player.SendNetworkUpdateImmediate();
		}

		private const int _itemsPerTick = 5;

		private IEnumerator TakeAndGiveCraftItems(BasePlayer player, int amount, CraftInfo craft,
			bool bypass)
		{
			if (!bypass)
			{
				var allItems = Pool.Get<List<Item>>();
				player.inventory.GetAllItems(allItems);
				
				craft.Items.ForEach(item => Take(allItems, item.ShortName, item.SkinID, item.Amount * amount));
                
				Pool.FreeUnmanaged(ref allItems);
			}

			for (var i = 0; i < amount; i++)
			{
				craft.GiveCraftToPlayer(player);

				if (i + 1 % _itemsPerTick == 0) yield return CoroutineEx.waitForFixedUpdate;
			}
		}

		private IEnumerator GiveCraftItems(BasePlayer player, int amount, CraftInfo craft)
		{
			if (craft.Type == CraftType.Item && craft.SingleStack)
			{
				craft.GiveItem(player, amount);
			}
			else
			{
				for (var i = 0; i < amount; i++)
				{
					craft.GiveCraftToPlayer(player);

					if (i + 1 % _itemsPerTick == 0) yield return CoroutineEx.waitForFixedUpdate;
				}
			}
		}

		private static bool HasAmount(List<Item> items, string shortname, ulong skin, int amount)
		{
			return ItemCount(items, shortname, skin) >= amount;
		}

		private static int PlayerItemsCount(BasePlayer player, string shortname, ulong skin)
		{
			var items = Pool.Get<List<Item>>();
			player.inventory.GetAllItems(items);
			
			var result = ItemCount(items, shortname, skin);
			
			Pool.Free(ref items);
			return result;
		}

		private static int ItemCount(List<Item> items, string shortname, ulong skin)
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

		private static bool HasWorkbench(BasePlayer player, WorkbenchLevel level)
		{
			CustomWorkbenchConf customWb;
			if (_instance._openedCustomWb.TryGetValue(player.userID, out customWb))
			{
				var lvl = customWb.GetWorkBenchLevel(player);

				if (lvl > level)
					level = lvl;
			}

			return level == WorkbenchLevel.Three ? player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench3)
				: level == WorkbenchLevel.Two ? player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench3) ||
				                                player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench2)
				: level == WorkbenchLevel.One ? player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench3) ||
				                                player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench2) ||
				                                player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench1)
				: level == WorkbenchLevel.None;
		}

		private Workbench GetLookWorkbench(BasePlayer player)
		{
			RaycastHit RaycastHit;
			if (!Physics.Raycast(player.eyes.HeadRay(), out RaycastHit, 5f)) return null;
			return RaycastHit.GetEntity() as Workbench;
		}

		#endregion

		#region Components

		#region Recycler Component

		private class RecyclerComponent : FacepunchBehaviour
		{
			private Recycler _recycler;

			private readonly BaseEntity[] _sensesResults = new BaseEntity[64];

			private void Awake()
			{
				_recycler = GetComponent<Recycler>();

				_instance?._recyclers.TryAdd(this);

				if (_recycler.OwnerID != 0)
				{
					_recycler.baseProtection = ScriptableObject.CreateInstance<ProtectionProperties>();
					_recycler.baseProtection.amounts = _config.Recycler.Amounts;

					_recycler.gameObject.AddComponent<GroundWatch>();
					_recycler.gameObject.AddComponent<DestroyOnGroundMissing>();

					if (_config.Recycler.Destroy.CheckGround && (!_recycler.HasParent() || _recycler.GetParentEntity() is not Tugboat))
						InvokeRepeating(CheckGroundMissing, 5, 5);
				}
			}

			private void CheckGroundMissing()
			{
				float distance = 3;
				if (Physics.Raycast(_recycler.transform.position + new Vector3(0, 0.1f, 0), Vector3.down,
					    out var hitInfo, 4f, LayerMask.GetMask("Terrain", "Construction")))
					distance = hitInfo.distance;

				if (distance > 0.2f)
				{
					_recycler.Kill();

					DestroyActions();
				}
			}

			public void DestroyActions()
			{
				if (_config.Recycler.Destroy.Item)
					_config.Recycler.GetItem.DropAndTossUpwards(_recycler.Transform.position);

				_config.Recycler.Destroy.Effects.ForEach(effect => SendEffect(effect, _recycler.Transform.position));
			}

			public void DDraw()
			{
				if (_recycler == null)
				{
					Kill();
					return;
				}

				if (_recycler.OwnerID == 0 || !_config.Recycler.DDraw)
					return;

				var inSphere = BaseEntity.Query.Server.GetInSphere(_recycler.transform.position,
					_config.Recycler.Radius,
					_sensesResults, entity => entity is BasePlayer);
				if (inSphere == 0)
					return;

				for (var i = 0; i < inSphere; i++)
				{
					var user = _sensesResults[i] as BasePlayer;
					if (user == null || user.IsDestroyed || !user.IsConnected || user.IsNpc ||
					    !user.userID.IsSteamId()) continue;

					if (user.Connection.authLevel < 2) SetPlayerFlag(user, BasePlayer.PlayerFlags.IsAdmin, true);

					user.SendConsoleCommand("ddraw.text", _config.Recycler.Delay,
						HexToUnityColor(_config.Recycler.Color),
						_recycler.transform.position + Vector3.up,
						string.Format(_config.Recycler.Text, _recycler.health, _recycler._maxHealth));

					if (user.Connection.authLevel < 2) SetPlayerFlag(user, BasePlayer.PlayerFlags.IsAdmin, false);
				}
			}

			#region Methods

			public void StartRecycling()
			{
				if (_recycler.IsOn())
					return;

				InvokeRepeating(RecycleThink, _config.Recycler.Speed, _config.Recycler.Speed);
				Effect.server.Run(_recycler.startSound.resourcePath, _recycler, 0U, Vector3.zero, Vector3.zero);
				_recycler.SetFlag(BaseEntity.Flags.On, true);

				_recycler.SendNetworkUpdateImmediate();
			}

			public void StopRecycling()
			{
				CancelInvoke(RecycleThink);

				if (!_recycler.IsOn())
					return;

				Effect.server.Run(_recycler.stopSound.resourcePath, _recycler, 0U, Vector3.zero, Vector3.zero);
				_recycler.SetFlag(BaseEntity.Flags.On, false);
				_recycler.SendNetworkUpdateImmediate();
			}

			public void RecycleThink()
			{
				var flag = false;
				var num1 = _recycler.recycleEfficiency;
				for (var slot1 = 0; slot1 < 6; ++slot1)
				{
					var slot2 = _recycler.inventory.GetSlot(slot1);
					if (slot2 != null)
					{
						if (Interface.CallHook("OnItemRecycle", slot2, _recycler) != null)
						{
							if (HasRecyclable())
								return;
							StopRecycling();
							return;
						}

						if (slot2.info.Blueprint == null) continue;

						if (slot2.hasCondition)
							num1 = Mathf.Clamp01(
								num1 * Mathf.Clamp(slot2.conditionNormalized * slot2.maxConditionNormalized, 0.1f,
									1f));

						var num2 = 1;
						if (slot2.amount > 1)
							num2 = Mathf.CeilToInt(Mathf.Min(slot2.amount, slot2.info.stackable * 0.1f));

						var callRecycleAmount = Interface.CallHook("OnItemRecycleAmount", slot2, num2, _recycler);
						if (callRecycleAmount is int)
							num2 = (int) callRecycleAmount;

						if (slot2.info.Blueprint.scrapFromRecycle > 0)
						{
							var iAmount = slot2.info.Blueprint.scrapFromRecycle * num2;
							if (slot2.info.stackable == 1 && slot2.hasCondition)
								iAmount = Mathf.CeilToInt(iAmount * slot2.conditionNormalized);
							if (iAmount >= 1)
								_recycler.MoveItemToOutput(ItemManager.CreateByName("scrap", iAmount));
						}

						if (!string.IsNullOrEmpty(slot2.info.Blueprint.RecycleStat))
						{
							var list = Pool.Get<List<BasePlayer>>();
							Vis.Entities(transform.position, 3f, list, 131072);
							list.ForEach(basePlayer =>
							{
								if (basePlayer.IsAlive() && !basePlayer.IsSleeping() &&
								    basePlayer.inventory.loot.entitySource == _recycler)
								{
									basePlayer.stats.Add(slot2.info.Blueprint.RecycleStat, num2,
										Stats.Steam | Stats.Life);
									basePlayer.stats.Save();
								}
							});

							Pool.FreeUnmanaged(ref list);
						}

						slot2.UseItem(num2);

						using var enumerator = slot2.info.Blueprint.ingredients.GetEnumerator();
						while (enumerator.MoveNext())
						{
							var current = enumerator.Current;
							if (current != null && current.itemDef.shortname != "scrap")
							{
								var num3 = current.amount / slot2.info.Blueprint.amountToCreate;
								var num4 = 0;
								if (num3 <= 1.0)
								{
									for (var index = 0; index < num2; ++index)
										if (Core.Random.Range(0.0f, 1f) <= num3 * (double) num1)
											++num4;
								}
								else
								{
									num4 = Mathf.CeilToInt(
										Mathf.Clamp(num3 * num1 * Core.Random.Range(1f, 1f), 0.0f,
											current.amount) *
										num2);
								}

								if (num4 > 0)
								{
									var num5 = Mathf.CeilToInt(num4 / (float) current.itemDef.stackable);
									for (var index = 0; index < num5; ++index)
									{
										var iAmount = num4 > current.itemDef.stackable
											? current.itemDef.stackable
											: num4;
										if (!_recycler.MoveItemToOutput(ItemManager.Create(current.itemDef,
											    iAmount)))
											flag = true;
										num4 -= iAmount;
										if (num4 <= 0)
											break;
									}
								}
							}
						}

						break;
					}
				}

				if (!flag && HasRecyclable())
					return;
				StopRecycling();
			}

			private bool HasRecyclable()
			{
				for (var slot1 = 0; slot1 < 6; ++slot1)
				{
					var slot2 = _recycler.inventory.GetSlot(slot1);
					if (slot2 != null)
					{
						var can = Interface.CallHook("CanRecycle", _recycler, slot2);
						if (can is bool canRecycle)
							return canRecycle;

						if (slot2.info.Blueprint != null)
							return true;
					}
				}

				return false;
			}

			#endregion

			#region Destroy

			public void TryPickup(BasePlayer player)
			{
				if (_config.Recycler.Building && !player.CanBuild())
				{
					_instance.Reply(player, CantBuild);
					return;
				}

				if (_config.Recycler.Owner && _recycler.OwnerID != player.userID)
				{
					_instance.Reply(player, OnlyOwner);
					return;
				}

				if (_recycler.SecondsSinceDealtDamage < 30f)
				{
					_instance.Reply(player, RecentlyDamaged);
					return;
				}

				_recycler.Kill();

				var craft = _instance._crafts.Find(x => x.Type == CraftType.Recycler && x.SkinID == _recycler.skinID);
				if (craft == null)
				{
					_instance.Reply(player, CannotGive);
					return;
				}

				craft.GiveCraftToPlayer(player);
			}

			private void OnDestroy()
			{
				CancelInvoke();

				_instance?._recyclers.Remove(this);

				Destroy(this);
			}

			public void Kill()
			{
				Destroy(this);
			}

			#endregion
		}

		#endregion

		#region Car Component

		public class CarController : FacepunchBehaviour
		{
			public BasicCar entity;
			public BasePlayer player;
			public bool isDiving;

			private bool _allowHeldItems;
			private string[] _disallowedItems;

			private readonly BaseEntity[] _sensesResults = new BaseEntity[64];

			private void Awake()
			{
				entity = GetComponent<BasicCar>();

				_allowHeldItems = !_config.Car.ActiveItems.Disable;
				_disallowedItems = _config.Car.ActiveItems.BlackList;

				_instance?._cars.TryAdd(this);
			}

			private void Update()
			{
				UpdateHeldItems();
				CheckWaterLevel();
			}

			public void ManageDamage(HitInfo info)
			{
				if (isDiving)
				{
					NullifyDamage(info);
					return;
				}

				if (info.damageTypes.Total() >= entity.health)
				{
					isDiving = true;
					NullifyDamage(info);
					OnDeath();
				}
			}

			public void DDraw()
			{
				if (entity == null)
				{
					Kill();
					return;
				}

				if (entity.OwnerID == 0)
					return;

				var inSphere = BaseEntity.Query.Server.GetInSphere(entity.transform.position, _config.Car.Radius,
					_sensesResults, ent => ent is BasePlayer);
				if (inSphere == 0)
					return;

				for (var i = 0; i < inSphere; i++)
				{
					var user = _sensesResults[i] as BasePlayer;
					if (user == null || user.IsDestroyed || !user.IsConnected || user.IsNpc ||
					    !user.userID.IsSteamId()) continue;

					if (user.Connection.authLevel < 2) SetPlayerFlag(user, BasePlayer.PlayerFlags.IsAdmin, true);

					user.SendConsoleCommand("ddraw.text", _config.Car.Delay, HexToUnityColor(_config.Car.Color),
						entity.transform.position + new Vector3(0.25f, 1, 0),
						string.Format(_config.Car.Text, entity.health, entity._maxHealth));

					if (user.Connection.authLevel < 2) SetPlayerFlag(user, BasePlayer.PlayerFlags.IsAdmin, false);
				}
			}

			private void NullifyDamage(HitInfo info)
			{
				info.damageTypes = new DamageTypeList();
				info.HitEntity = null;
				info.HitMaterial = 0;
				info.PointStart = Vector3.zero;
			}

			public void UpdateHeldItems()
			{
				if (player == null)
					return;

				var item = player.GetActiveItem();
				if (item == null || item.GetHeldEntity() == null)
					return;

				if (_disallowedItems.Contains(item.info.shortname) || !_allowHeldItems)
				{
					_instance?.Reply(player, ItemNotAllowed);

					var slot = item.position;
					item.SetParent(null);
					item.MarkDirty();

					Invoke(() =>
					{
						if (player == null) return;
						item.SetParent(player.inventory.containerBelt);
						item.position = slot;
						item.MarkDirty();
					}, 0.15f);
				}
			}

			public void CheckWaterLevel()
			{
				if (entity.WaterFactor() > 0.7f)
					StopToDie();
			}

			public void StopToDie(bool death = true)
			{
				if (entity != null)
				{
					entity.SetFlag(BaseEntity.Flags.Reserved1, false);

					foreach (var wheel in entity.wheels)
					{
						wheel.wheelCollider.motorTorque = 0;
						wheel.wheelCollider.brakeTorque = float.MaxValue;
					}

					entity.GetComponent<Rigidbody>().velocity = Vector3.zero;

					if (player != null)
						entity.DismountPlayer(player);
				}

				if (death) OnDeath();
			}

			private void OnDeath()
			{
				isDiving = true;

				if (player != null)
					player.EnsureDismounted();

				Invoke(() =>
				{
					Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/heli_explosion.prefab",
						transform.position);
					_instance.NextTick(() =>
					{
						if (entity != null && !entity.IsDestroyed)
							entity.DieInstantly();
						Destroy(this);
					});
				}, 5f);
			}

			private void OnDestroy()
			{
				_instance?._cars.Remove(this);
			}

			public void Kill()
			{
				StopToDie(false);
				Destroy(this);
			}
		}

		#endregion

		#region Safe Zone

		private class SafeZoneComponent : FacepunchBehaviour
		{
			private readonly int playerLayer = LayerMask.GetMask("Player (Server)");

			private Rigidbody _rigidbody;

			private SphereCollider _sphereCollider;

			private TriggerSafeZone _safeZone;

			private void Awake()
			{
				gameObject.layer = (int) Rust.Layer.Reserved1;

				_rigidbody = gameObject.AddComponent<Rigidbody>();
				_rigidbody.useGravity = false;
				_rigidbody.isKinematic = true;

				_instance?._safeZones.TryAdd(this);
			}

			public void Activate(BaseEntity parent, float radius)
			{
				transform.SetPositionAndRotation(parent.transform.position, new Quaternion());

				UpdateCollider(radius);

				gameObject.SetActive(true);
				enabled = true;
				_safeZone = gameObject.GetComponent<TriggerSafeZone>() ?? gameObject.AddComponent<TriggerSafeZone>();
				_safeZone.interestLayers = playerLayer;
				_safeZone.enabled = true;
			}

			private void UpdateCollider(float ZoneRadius)
			{
				_sphereCollider = gameObject.GetComponent<SphereCollider>();

				if (_sphereCollider == null)
				{
					_sphereCollider = gameObject.AddComponent<SphereCollider>();
					_sphereCollider.isTrigger = true;
				}

				_sphereCollider.radius = ZoneRadius;
			}

			private void OnDestroy()
			{
				_instance?._safeZones.Remove(this);

				Destroy(gameObject);
				Destroy(this);
			}

			public void Kill()
			{
				DestroyImmediate(this);
			}
		}

		#endregion

		#endregion

		#region Lang

		private const string
			NoILError = "NoILError",
			BalanceTitle = "BalanceTitle",
			EnableSales = "EnableSales",
			EnableCrafting = "EnableCrafting",
			NotMoney = "NotMoney",
			BuyButton = "BuyButton",
			NotEnoughSpace = "NotEnoughSpace",
			ErrorSyntax = "ErrorSyntax",
			WbWorkbenchInstalled = "WbWorkbenchInstalled",
			WbWorkbenchExists = "WbWorkbenchExists",
			WbNotFoundWorkbench = "WbNotFoundWorkbench",
			WbNotFoundCategories = "WbNotFoundCategories",
			CraftItemAmount = "CraftItemAmount",
			CraftSelect = "CraftSelect",
			CraftCreate = "CraftCreate",
			Back = "Back",
			Next = "Next",
			ItemSearch = "ItemSearch",
			CraftRemoveTitle = "CraftRemoveTitle",
			BtnNext = "BtnNext",
			BtnBack = "BtnBack",
			CraftItemsAddTitle = "CraftItemsAddTitle",
			CraftSaveTitle = "CraftSaveTitle",
			CraftItemsTitle = "CraftItemsTitle",
			CraftWorkbenchTitle = "CraftWorkbenchTitle",
			CraftTypeTitle = "CraftTypeTitle",
			ItemEditingTitle = "ItemEditingTitle",
			CraftEditingTitle = "CraftEditingTitle",
			EnableStructure = "EnableStructure",
			EditGround = "EditGround",
			EditUseDistance = "EditUseDistance",
			EnableCraft = "EnableCraft",
			NotWorkbench = "NotWorkbench",
			CraftsDescription = "CraftsDescription",
			WorkbenchLvl = "WorkbenchLvl",
			GotCraft = "GotCraft",
			NoPermission = "NoPermission",
			SuccessfulCraft = "SuccessfulCraft",
			NotEnoughResources = "NotEnoughResources",
			CraftCancelButton = "CraftCancelButton",
			CraftButton = "CraftButton",
			CraftTitle = "CraftTitle",
			TitleMenu = "TitleMenu",
			CloseButton = "CloseButton",
			OnGround = "OnGround",
			BuildDistance = "BuildDistance",
			OnStruct = "OnStruct",
			NotTake = "NotTake",
			ItemNotAllowed = "ItemNotAllowed",
			CantBuild = "CantBuild",
			OnlyOwner = "OnlyOwner",
			RecentlyDamaged = "RecentlyDamaged",
			CannotGive = "CannotGive";

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				[NoPermission] = "You don't have the required permission",
				[TitleMenu] = "Crafts Menu",
				[CloseButton] = "✕",
				[CraftTitle] = "TO CRAFT {0} YOU NEED",
				[CraftButton] = "CRAFT",
				[CraftCancelButton] = "CANCEL",
				[NotEnoughResources] = "Not enough resources",
				[SuccessfulCraft] = "You have successfully crafted the '{0}'",
				[OnGround] = "{0} can't put it on the ground!",
				[BuildDistance] = "Built closer than {0}m is blocked!",
				[OnStruct] = "{0} can't put on the buildings!",
				[NotTake] = "Pickup disabled!",
				[ItemNotAllowed] = "Item blocked!",
				[CantBuild] = "You must have the permission to build.",
				[OnlyOwner] = "Only the owner can pick up the recycler!",
				[RecentlyDamaged] = "The recycler has recently been damaged, you can take it in 30 seconds!",
				[CannotGive] = "Call the administrator. The recycler cannot be give",
				[GotCraft] = "You got a '{0}'",
				[WorkbenchLvl] = "Workbench LVL {0}",
				[CraftsDescription] =
					"Select the desired item from the list of all items, sort them by category, after which you can find out the cost of manufacturing and the most efficient way to create an item.",
				[NotWorkbench] = "Not enough workbench level for craft!",
				[EnableCraft] = "Enabled",
				[EditUseDistance] = "Distance Check",
				[EditGround] = "Place the ground",
				[EnableStructure] = "Place the structure",
				[CraftEditingTitle] = "Creating/editing craft",
				[ItemEditingTitle] = "Creating/editing item",
				[CraftTypeTitle] = "Type",
				[CraftWorkbenchTitle] = "WorkBench",
				[CraftItemsTitle] = "Items",
				[CraftSaveTitle] = "Save",
				[CraftItemsAddTitle] = "+",
				[BtnBack] = "◀",
				[BtnNext] = "▶",
				[CraftRemoveTitle] = "✕",
				[ItemSearch] = "Item search",
				[Back] = "Back",
				[Next] = "Next",
				[CraftCreate] = "Create Craft",
				[CraftSelect] = "Select",
				[CraftItemAmount] = "{0} pcs",
				[WbNotFoundCategories] = "Categories not found!",
				[WbNotFoundWorkbench] = "Workbench not found!",
				[WbWorkbenchExists] = "This Workbench is already in the config!",
				[WbWorkbenchInstalled] = "You have successfully installed the custom Workbench!",
				[ErrorSyntax] = "Syntax error! Use: /{0}",
				[NotEnoughSpace] = "Not enought space",
				[BuyButton] = "{0} RP",
				[NotMoney] = "You don't have enough money!",
				[EnableSales] = "Sales",
				[EnableCrafting] = "Craft",
				[BalanceTitle] = "{0} RP",
				[NoILError] = "The plugin does not work correctly, contact the administrator!",
				["Enabled"] = "Enabled",
				["Image"] = "Image",
				["Title"] = "Title",
				["Description"] = "Description",
				["CmdToGive"] = "Command (to give an item)",
				["Permission"] = "Permission (ex: crafts.vip)",
				["DisplayName"] = "Display Name",
				["ShortName"] = "ShortName",
				["Amount"] = "Amount",
				["SkinID"] = "Skin",
				["Prefab"] = "Prefab",
				["GiveCommand"] = "Command on give",
				["Distance"] = "Distance"
			}, this);

			lang.RegisterMessages(new Dictionary<string, string>
			{
				[NoPermission] = "У вас нет необходимого разрешения",
				[TitleMenu] = "Меню Крафтов",
				[CloseButton] = "✕",
				[CraftTitle] = "ДЛЯ КРАФТА {0} ВАМ НУЖНО",
				[CraftButton] = "КРАФТ",
				[CraftCancelButton] = "ОТМЕНИТЬ",
				[NotEnoughResources] = "Недостаточно ресурсов",
				[SuccessfulCraft] = "Вы успешно скрафтили '{0}'",
				[OnGround] = "{0} нельзя установить на землю!",
				[BuildDistance] = "Устанавливать ближе {0}м запрещено!",
				[OnStruct] = "{0} нельзя установить на строениях!",
				[NotTake] = "Подроб отключён!",
				[ItemNotAllowed] = "Предмет заблокирован!",
				[CantBuild] = "У вас должно быть разрешение на строительство.",
				[OnlyOwner] = "Только владелец может забрать переработчик!",
				[RecentlyDamaged] = "Переработчик недавно был поврежден, его можно забрать за 30 секунд!",
				[CannotGive] = "Свяжитесь с администратором: невозможно получить переработчик",
				[GotCraft] = "Вы получили '{0}'",
				[WorkbenchLvl] = "Верстак {0} уровня",
				[CraftsDescription] =
					"Выберите нужный предмет из списка всех предметов, отсортируйте их по категориям, после чего вы сможете узнать стоимость изготовления и наиболее эффективным способом создать предмет.",
				[NotWorkbench] = "Не достаточный уровень верстака для крафта!",
				[EnableCraft] = "Включено",
				[EditUseDistance] = "Проверять дистацию",
				[EditGround] = "Установка на землю",
				[EnableStructure] = "Установка на строение",
				[CraftEditingTitle] = "Создание/изменение крафта",
				[ItemEditingTitle] = "Создание/изменение предмета",
				[CraftTypeTitle] = "Тип",
				[CraftWorkbenchTitle] = "Верстак",
				[CraftItemsTitle] = "Предметы",
				[CraftSaveTitle] = "Сохранить",
				[CraftItemsAddTitle] = "+",
				[BtnBack] = "◀",
				[BtnNext] = "▶",
				[CraftRemoveTitle] = "✕",
				[ItemSearch] = "Поиск",
				[Back] = "Назад",
				[Next] = "Вперёд",
				[CraftCreate] = "Создать Крафт",
				[CraftSelect] = "Выбрать",
				[CraftItemAmount] = "{0} шт",
				[WbNotFoundCategories] = "Категории не найлены!",
				[WbNotFoundWorkbench] = "Верстак не найден!",
				[WbWorkbenchExists] = "Верстак уже находится в конфиге!",
				[WbWorkbenchInstalled] = "Вы успешно установили кастомный верстак!",
				[ErrorSyntax] = "Ошибка синтаксиса! Используйте: /{0}",
				[NotEnoughSpace] = "Недостаточно места",
				[BuyButton] = "{0} RP",
				[NotMoney] = "У вас недостаточно денег!",
				[EnableSales] = "Продажа",
				[EnableCrafting] = "Крафт",
				[BalanceTitle] = "{0} RP",
				[NoILError] = "Плагин работает некорректно, свяжитесь с администратором!",
				["Enabled"] = "Включено",
				["Image"] = "Изображение",
				["Title"] = "Название",
				["Description"] = "Описание",
				["CmdToGive"] = "Команда (для выдачи предмета)",
				["Permission"] = "Разрешение (пример: crafts.vip)",
				["DisplayName"] = "Отображаемое имя",
				["ShortName"] = "ShortName",
				["Amount"] = "Количество",
				["SkinID"] = "Скин",
				["Prefab"] = "Префаб",
				["GiveCommand"] = "Команда при получении",
				["Distance"] = "Дистанция"
			}, this, "ru");
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

		#region Testing functions

#if TESTING
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

namespace Oxide.Plugins.CraftsExtensionMethods
{
	// ReSharper disable ForCanBeConvertedToForeach
	// ReSharper disable LoopCanBeConvertedToQuery
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

		public static float Sum<TSource>(this IEnumerable<TSource> source, Func<TSource, float> selector)
		{
			var sum = 0f;

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

		public static bool All<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
		{
			if (source == null) return false;

			using (var element = source.GetEnumerator())
			{
				while (element.MoveNext())
					if (!predicate(element.Current))
						return false;
			}

			return true;
		}

		public static int Count<TSource>(this IEnumerable<TSource> source)
		{
			if (source == null) return 0;

			var collectionOfT = source as ICollection<TSource>;
			if (collectionOfT != null)
				return collectionOfT.Count;

			var collection = source as ICollection;
			if (collection != null)
				return collection.Count;

			var count = 0;
			using (var e = source.GetEnumerator())
			{
				checked
				{
					while (e.MoveNext()) count++;
				}
			}

			return count;
		}

		public static List<TSource> OrderByDescending<TSource, TKey>(this List<TSource> source,
			Func<TSource, TKey> keySelector, IComparer<TKey> comparer = null)
		{
			if (source == null) return new List<TSource>();

			if (keySelector == null) return new List<TSource>();

			if (comparer == null) comparer = Comparer<TKey>.Default;

			var result = new List<TSource>(source);
			var lambdaComparer = new ReverseLambdaComparer<TSource, TKey>(keySelector, comparer);
			result.Sort(lambdaComparer);
			return result;
		}

		internal sealed class ReverseLambdaComparer<T, U> : IComparer<T>
		{
			private IComparer<U> comparer;
			private Func<T, U> selector;

			public ReverseLambdaComparer(Func<T, U> selector, IComparer<U> comparer)
			{
				this.comparer = comparer;
				this.selector = selector;
			}

			public int Compare(T x, T y)
			{
				return comparer.Compare(selector(y), selector(x));
			}
		}

		public static IEnumerable<TResult> Cast<TResult>(this IEnumerable source)
		{
			return CastIterator<TResult>(source);
		}

		private static IEnumerable<TResult> CastIterator<TResult>(IEnumerable source)
		{
			foreach (var obj in source) yield return (TResult) obj;
		}
	}
}

#endregion Extension Methods