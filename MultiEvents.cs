using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using WebSocketSharp;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
	[Info("MultiEvents", "https://discord.gg/TrJ7jnS233", "1.40.0")]
	public class MultiEvents : RustPlugin
	{
		#region Fields

		[PluginReference] private Plugin Duel, ImageLibrary;

		private const string Layer = "UI.MultiEvents";
		private int time;

		private BasePlayer winner;
		private BasePlayer runner;
		private readonly List<string> Winners = new List<string>();
		private bool hasStarted;
		private string nowEvent;
		private Dictionary<BasePlayer, float> PlayersTop = new Dictionary<BasePlayer, float>();
		private readonly List<LootContainer> LookingLoot = new List<LootContainer>();
		private static MultiEvents _instance;
		private static Vector3 EventPosition;
		private static FoundationDrop cEvent;
		private MonumentInfo beginning_mission;
		private MonumentInfo end_mission;

		private readonly Dictionary<int, RadZone> RadiationZones = new Dictionary<int, RadZone>();
		private static readonly int playerLayer = LayerMask.GetMask("Player (Server)");


		private Action aStartEvent;
		private Action EventRepeating;

		public enum ItemTypes
		{
			Item,
			Command
		}

		private readonly List<Downgrader> Downgraders = new List<Downgrader>();
		private readonly List<SCmarker> SCmarkers = new List<SCmarker>();
		private readonly List<RadZone> Zones = new List<RadZone>();

		#endregion

		#region Config

		private static Configuration _config;

		private class Configuration
		{
			[JsonProperty("Использовать автоматический запуск ивентов?")]
			public bool useTimer = true;

			[JsonProperty("Задержка между эвентами")]
			public int Delay = 3600;

			[JsonProperty("Отклонение от стандартного времени (рандомное время)")]
			public int RandomTime = 900;

			[JsonProperty("Максимальная длина никнейма")]
			public int NameLenght = 16;

			[JsonProperty("Эвенты доступные для игроков", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> EnabledEvents = new List<string>
			{
				"KingMountain", "CollectionResources", "HuntAnimal", "HelicopterPet", "LookingLoot", "SpecialCargo",
				"FoundationDrop"
			};

			[JsonProperty("Право на запуск ивента")]
			public string perm_admin = "multievents.admin";

			[JsonProperty("Команда открытия склада")]
			public string cmdStorage = "storage";

			[JsonProperty("Включить оповещение о получении награды?")]
			public bool EnableRewardAlert = true;

			[JsonProperty("Настройка эвента ЛЮБИМЧИК ВЕРТОЛЁТА")]
			public EventSettings HelicopterPet = new EventSettings
			{
				MinPlayers = 4,
				TimeDelay = 300,
				ImageUrl = "https://i.imgur.com/owDanC1.png",
				ui = new Interface
				{
					color = "0.024 0.016 0.17 0.7",
					oMin = "-225 -305",
					oMax = "-5 -5"
				},
				AwardItems = new List<Loot>
				{
					new Loot
					{
						itemenabled = true,
						cashenabled = true,
						items = new List<List<Items>>
						{
							new List<Items>
							{
								new Items
								{
									Type = ItemTypes.Item,
									Item = new ItemConfClass
									{
										ShortName = "autoturret",
										SkinID = 0,
										minAmount = 1,
										maxAmount = 1
									},
									DisplayName = string.Empty,
									Command = string.Empty,
									ImageURL = string.Empty
								},
								new Items
								{
									Type = ItemTypes.Command,
									Item = new ItemConfClass
									{
										SkinID = 0,
										ShortName = string.Empty,
										minAmount = 1,
										maxAmount = 1
									},
									Command = "addgroup %steamid% vip 1d",
									DisplayName = "VIP на 1 день",
									ImageURL = "https://i.imgur.com/GuJUyLH.png"
								}
							}
						},
						cash = new Cash
						{
							function = "Deposit",
							plugin = "Economics",
							amount = 400
						}
					}
				}
			};

			[JsonProperty("Настройка эвента ОХОТА НА ЖИВОТНЫХ")]
			public HuntAnimalSettings HuntAnimal = new HuntAnimalSettings
			{
				MinPlayers = 4,
				TimeDelay = 300,
				ImageUrl = "https://i.imgur.com/9gTw3kN.png",
				Chicken = 1,
				Wolf = 4,
				Boar = 4,
				Deer = 4,
				Horse = 4,
				Bear = 10,
				ui = new Interface
				{
					color = "0.024 0.016 0.17 0.7",
					oMin = "-225 -305",
					oMax = "-5 -5"
				},
				AwardItems = new List<Loot>
				{
					new Loot
					{
						itemenabled = true,
						cashenabled = true,
						items = new List<List<Items>>
						{
							new List<Items>
							{
								new Items
								{
									Type = ItemTypes.Item,
									Item = new ItemConfClass
									{
										ShortName = "autoturret",
										SkinID = 0,
										minAmount = 1,
										maxAmount = 1
									},
									DisplayName = string.Empty,
									Command = string.Empty,
									ImageURL = string.Empty
								},
								new Items
								{
									Type = ItemTypes.Command,
									Item = new ItemConfClass
									{
										SkinID = 0,
										ShortName = string.Empty,
										minAmount = 1,
										maxAmount = 1
									},
									Command = "addgroup %steamid% vip 1d",
									DisplayName = "VIP на 1 день",
									ImageURL = "https://i.imgur.com/GuJUyLH.png"
								}
							}
						},
						cash = new Cash
						{
							function = "Deposit",
							plugin = "Economics",
							amount = 400
						}
					},
					new Loot
					{
						itemenabled = true,
						cashenabled = true,
						items = new List<List<Items>>
						{
							new List<Items>
							{
								new Items
								{
									Type = ItemTypes.Item,
									Item = new ItemConfClass
									{
										ShortName = "autoturret",
										SkinID = 0,
										minAmount = 1,
										maxAmount = 1
									},
									DisplayName = string.Empty,
									Command = string.Empty,
									ImageURL = string.Empty
								},
								new Items
								{
									Type = ItemTypes.Command,
									Item = new ItemConfClass
									{
										SkinID = 0,
										ShortName = string.Empty,
										minAmount = 1,
										maxAmount = 1
									},
									Command = "addgroup %steamid% vip 1d",
									DisplayName = "VIP на 1 день",
									ImageURL = "https://i.imgur.com/GuJUyLH.png"
								}
							}
						},
						cash = new Cash
						{
							function = "Deposit",
							plugin = "Economics",
							amount = 400
						}
					},
					new Loot
					{
						itemenabled = true,
						cashenabled = true,
						items = new List<List<Items>>
						{
							new List<Items>
							{
								new Items
								{
									Type = ItemTypes.Item,
									Item = new ItemConfClass
									{
										ShortName = "autoturret",
										SkinID = 0,
										minAmount = 1,
										maxAmount = 1
									},
									DisplayName = string.Empty,
									Command = string.Empty,
									ImageURL = string.Empty
								},
								new Items
								{
									Type = ItemTypes.Command,
									Item = new ItemConfClass
									{
										SkinID = 0,
										ShortName = string.Empty,
										minAmount = 1,
										maxAmount = 1
									},
									Command = "addgroup %steamid% vip 1d",
									DisplayName = "VIP на 1 день",
									ImageURL = "https://i.imgur.com/GuJUyLH.png"
								}
							}
						},
						cash = new Cash
						{
							function = "Deposit",
							plugin = "Economics",
							amount = 400
						}
					}
				}
			};

			[JsonProperty("Настройка эвента КОРОЛЬ ГОРЫ")]
			public KingMountainSettings KingMountain = new KingMountainSettings
			{
				checkDuels = true,
				checkHotair = true,
				checkMinicopter = true,
				checkScrapheli = true,
				checkBuilding = true,
				MinPlayers = 4,
				TimeDelay = 300,
				ImageUrl = "https://i.imgur.com/dLWgxg7.png",
				ui = new Interface
				{
					color = "0.024 0.016 0.17 0.7",
					oMin = "-225 -305",
					oMax = "-5 -5"
				},
				AwardItems = new List<Loot>
				{
					new Loot
					{
						itemenabled = true,
						cashenabled = true,
						items = new List<List<Items>>
						{
							new List<Items>
							{
								new Items
								{
									Type = ItemTypes.Item,
									Item = new ItemConfClass
									{
										ShortName = "autoturret",
										SkinID = 0,
										minAmount = 1,
										maxAmount = 1
									},
									DisplayName = string.Empty,
									Command = string.Empty,
									ImageURL = string.Empty
								},
								new Items
								{
									Type = ItemTypes.Command,
									Item = new ItemConfClass
									{
										SkinID = 0,
										ShortName = string.Empty,
										minAmount = 1,
										maxAmount = 1
									},
									Command = "addgroup %steamid% vip 1d",
									DisplayName = "VIP на 1 день",
									ImageURL = "https://i.imgur.com/GuJUyLH.png"
								}
							}
						},
						cash = new Cash
						{
							function = "Deposit",
							plugin = "Economics",
							amount = 400
						}
					}
				}
			};

			[JsonProperty("Настройка эвента СБОР РЕСУРСОВ")]
			public CollectionResourcesSettings CollectionResources = new CollectionResourcesSettings
			{
				MinPlayers = 4,
				TimeDelay = 300,
				ImageUrl = "https://i.imgur.com/FggJeaH.png",
				ui = new Interface
				{
					color = "0.024 0.016 0.17 0.7",
					oMin = "-225 -305",
					oMax = "-5 -5"
				},
				BlockItems = new List<string>
				{
					"jackhammer", "chainsaw"
				},
				AwardItems = new List<Loot>
				{
					new Loot
					{
						itemenabled = true,
						cashenabled = true,
						items = new List<List<Items>>
						{
							new List<Items>
							{
								new Items
								{
									Type = ItemTypes.Item,
									Item = new ItemConfClass
									{
										ShortName = "autoturret",
										SkinID = 0,
										minAmount = 1,
										maxAmount = 1
									},
									DisplayName = string.Empty,
									Command = string.Empty,
									ImageURL = string.Empty
								},
								new Items
								{
									Type = ItemTypes.Command,
									Item = new ItemConfClass
									{
										SkinID = 0,
										ShortName = string.Empty,
										minAmount = 1,
										maxAmount = 1
									},
									Command = "addgroup %steamid% vip 1d",
									DisplayName = "VIP на 1 день",
									ImageURL = "https://i.imgur.com/GuJUyLH.png"
								}
							}
						},
						cash = new Cash
						{
							function = "Deposit",
							plugin = "Economics",
							amount = 400
						}
					},
					new Loot
					{
						itemenabled = true,
						cashenabled = true,
						items = new List<List<Items>>
						{
							new List<Items>
							{
								new Items
								{
									Type = ItemTypes.Item,
									Item = new ItemConfClass
									{
										ShortName = "autoturret",
										SkinID = 0,
										minAmount = 1,
										maxAmount = 1
									},
									DisplayName = string.Empty,
									Command = string.Empty,
									ImageURL = string.Empty
								},
								new Items
								{
									Type = ItemTypes.Command,
									Item = new ItemConfClass
									{
										SkinID = 0,
										ShortName = string.Empty,
										minAmount = 1,
										maxAmount = 1
									},
									Command = "addgroup %steamid% vip 1d",
									DisplayName = "VIP на 1 день",
									ImageURL = "https://i.imgur.com/GuJUyLH.png"
								}
							}
						},
						cash = new Cash
						{
							function = "Deposit",
							plugin = "Economics",
							amount = 400
						}
					},
					new Loot
					{
						itemenabled = true,
						cashenabled = true,
						items = new List<List<Items>>
						{
							new List<Items>
							{
								new Items
								{
									Type = ItemTypes.Item,
									Item = new ItemConfClass
									{
										ShortName = "autoturret",
										SkinID = 0,
										minAmount = 1,
										maxAmount = 1
									},
									DisplayName = string.Empty,
									Command = string.Empty,
									ImageURL = string.Empty
								},
								new Items
								{
									Type = ItemTypes.Command,
									Item = new ItemConfClass
									{
										SkinID = 0,
										ShortName = string.Empty,
										minAmount = 1,
										maxAmount = 1
									},
									Command = "addgroup %steamid% vip 1d",
									DisplayName = "VIP на 1 день",
									ImageURL = "https://i.imgur.com/GuJUyLH.png"
								}
							}
						},
						cash = new Cash
						{
							function = "Deposit",
							plugin = "Economics",
							amount = 400
						}
					}
				}
			};

			[JsonProperty("Настройка эвента В ПОИСКАХ ЛУТА")]
			public LookingLootSettings LookingLoot = new LookingLootSettings
			{
				MinPlayers = 4,
				TimeDelay = 300,
				ImageUrl = "https://i.imgur.com/4ZOIs33.png",
				Barrels = new List<string>
				{
					"assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab",
					"assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab",
					"assets/bundled/prefabs/radtown/loot_barrel_1.prefab",
					"assets/bundled/prefabs/radtown/loot_barrel_2.prefab",
					"assets/bundled/prefabs/radtown/oil_barrel.prefab"
				},
				ui = new Interface
				{
					color = "0.024 0.016 0.17 0.7",
					oMin = "-225 -305",
					oMax = "-5 -5"
				},
				AwardItems = new List<Loot>
				{
					new Loot
					{
						itemenabled = true,
						cashenabled = true,
						items = new List<List<Items>>
						{
							new List<Items>
							{
								new Items
								{
									Type = ItemTypes.Item,
									Item = new ItemConfClass
									{
										ShortName = "autoturret",
										SkinID = 0,
										minAmount = 1,
										maxAmount = 1
									},
									DisplayName = string.Empty,
									Command = string.Empty,
									ImageURL = string.Empty
								},
								new Items
								{
									Type = ItemTypes.Command,
									Item = new ItemConfClass
									{
										SkinID = 0,
										ShortName = string.Empty,
										minAmount = 1,
										maxAmount = 1
									},
									Command = "addgroup %steamid% vip 1d",
									DisplayName = "VIP на 1 день",
									ImageURL = "https://i.imgur.com/GuJUyLH.png"
								}
							}
						},
						cash = new Cash
						{
							function = "Deposit",
							plugin = "Economics",
							amount = 400
						}
					}
				}
			};

			[JsonProperty("Настройка эвента ДОСТАВКА ОСОБОГО ГРУЗА")]
			public SpecialCargoSettings SpecialCargo = new SpecialCargoSettings
			{
				tpBlock = true,
				MarkerName = "ОСОБЫЙ ГРУЗ",
				MinPlayers = 4,
				TimeDelay = 1800,
				ImageUrl = "https://i.imgur.com/Dp9bMSl.png",
				timeNewRunner = 6,
				timeStart = 4,
				blockMonuments = new List<string>
				{
					"oilrig", "cave", "power_sub"
				},
				ui = new Interface
				{
					color = "0.024 0.016 0.17 0.7",
					oMin = "-225 -305",
					oMax = "-5 -5"
				},
				AwardItems = new List<Loot>
				{
					new Loot
					{
						itemenabled = true,
						cashenabled = true,
						items = new List<List<Items>>
						{
							new List<Items>
							{
								new Items
								{
									Type = ItemTypes.Item,
									Item = new ItemConfClass
									{
										ShortName = "autoturret",
										SkinID = 0,
										minAmount = 1,
										maxAmount = 1
									},
									DisplayName = string.Empty,
									Command = string.Empty,
									ImageURL = string.Empty
								},
								new Items
								{
									Type = ItemTypes.Command,
									Item = new ItemConfClass
									{
										SkinID = 0,
										ShortName = string.Empty,
										minAmount = 1,
										maxAmount = 1
									},
									Command = "addgroup %steamid% vip 1d",
									DisplayName = "VIP на 1 день",
									ImageURL = "https://i.imgur.com/GuJUyLH.png"
								}
							}
						},
						cash = new Cash
						{
							function = "Deposit",
							plugin = "Economics",
							amount = 400
						}
					}
				}
			};

			[JsonProperty("Настройка эвента ПАДАЮЩИЕ ПЛАТФОРМЫ")]
			public FoundationDropSettings FoundationDrop = new FoundationDropSettings
			{
				AwardItems = new List<Loot>
				{
					new Loot
					{
						itemenabled = true,
						cashenabled = true,
						items = new List<List<Items>>
						{
							new List<Items>
							{
								new Items
								{
									Type = ItemTypes.Item,
									Item = new ItemConfClass
									{
										ShortName = "autoturret",
										SkinID = 0,
										minAmount = 1,
										maxAmount = 1
									},
									DisplayName = string.Empty,
									Command = string.Empty,
									ImageURL = string.Empty
								},
								new Items
								{
									Type = ItemTypes.Command,
									Item = new ItemConfClass
									{
										SkinID = 0,
										ShortName = string.Empty,
										minAmount = 1,
										maxAmount = 1
									},
									Command = "addgroup %steamid% vip 1d",
									DisplayName = "VIP на 1 день",
									ImageURL = "https://i.imgur.com/GuJUyLH.png"
								}
							}
						},
						cash = new Cash
						{
							function = "Deposit",
							plugin = "Economics",
							amount = 400
						}
					}
				},
				MinPlayers = 4,
				TimeDelay = 1800,
				ImageUrl = "https://i.imgur.com/B4DCBs2.png",
				ArenaSize = 10,
				DelayDestroy = 5f,
				WaitTime = 60,
				IntensityRadiation = 10f,
				DisableDefaultRadiation = false,
				ui = new Interface
				{
					color = "0.024 0.016 0.17 0.7",
					oMin = "-225 -305",
					oMax = "-5 -5"
				},
				infoUI = new Interface
				{
					color = "0.024 0.016 0.17 0.7",
					oMin = "-225 -100",
					oMax = "-5 -5"
				},
				CanTrade = true,
				CanBackpack = true,
				UseTruePVE = true,
				commands = new List<string>
				{
					"bp",
					"backpack",
					"skin",
					"skinbox",
					"rec",
					"tpa",
					"tpr",
					"sethome",
					"home",
					"kit",
					"remove"
				}
			};
		}

		public class Interface
		{
			[JsonProperty("Цвет фона")] public string color;
			[JsonProperty("Offset Min")] public string oMin;
			[JsonProperty("Offset Max")] public string oMax;
		}

		public class EventSettings
		{
			[JsonProperty("Настройка интерфейса ивента")]
			public Interface ui;

			[JsonProperty("Сколько игроков требуется для начала эвента?")]
			public int MinPlayers;

			[JsonProperty("Время продолжительности эвента (в секундах)")]
			public int TimeDelay;

			[JsonProperty("Ссылка на картинку")] public string ImageUrl;

			[JsonProperty("Настройка приза для победителя", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<Loot> AwardItems;
		}

		public class HuntAnimalSettings : EventSettings
		{
			[JsonProperty("Сколько очков даётся за курицу?")]
			public int Chicken;

			[JsonProperty("Сколько очков даётся за волка?")]
			public int Wolf;

			[JsonProperty("Сколько очков даётся за кабана?")]
			public int Boar;

			[JsonProperty("Сколько очков даётся за оленя?")]
			public int Deer;

			[JsonProperty("Сколько очков даётся за лошадь?")]
			public int Horse;

			[JsonProperty("Сколько очков даётся за медведя?")]
			public int Bear;
		}

		public class CollectionResourcesSettings : EventSettings
		{
			[JsonProperty("Список предметов, добыча при помощи которых не учитывается",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> BlockItems;
		}

		public class KingMountainSettings : EventSettings
		{
			[JsonProperty("Блокировать людей на дуэдли (Duels)")]
			public bool checkDuels;

			[JsonProperty("Блокировать людей в миникоптере")]
			public bool checkMinicopter;

			[JsonProperty("Блокировать людей в скрап вертолёте")]
			public bool checkScrapheli;

			[JsonProperty("Блокировать людей в воздушномшаре")]
			public bool checkHotair;

			[JsonProperty("Блокировать людей в билде (когда игрок авторизован в шкафу)")]
			public bool checkBuilding;
		}

		public class LookingLootSettings : EventSettings
		{
			[JsonProperty("Лутание каких бочек считаются в эвенте?",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> Barrels;
		}

		public class SpecialCargoSettings : EventSettings
		{
			[JsonProperty("Время показа уведомления о запуске ивента")]
			public float timeStart;

			[JsonProperty("Время показа уведомления об объявлении бегущего")]
			public float timeNewRunner;

			[JsonProperty("Название маркера для карты")]
			public string MarkerName;

			[JsonProperty("Блокировать телепортацию для бегущего?")]
			public bool tpBlock;

			[JsonProperty("Запрещённые монументы для игры", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> blockMonuments;
		}

		public class FoundationDropSettings : EventSettings
		{
			[JsonProperty("Настройка интерфейса с информацией о количестве игроков и блоков")]
			public Interface infoUI;

			[JsonProperty("Размер арены в квадратах")]
			public int ArenaSize;

			[JsonProperty("Интвервал между удалениями блоков")]
			public float DelayDestroy;

			[JsonProperty("Время ожидания игроков с момента объявления ивента")]
			public int WaitTime;

			[JsonProperty("Интенсивность созданой радиации")]
			public float IntensityRadiation;

			[JsonProperty(
				"Отключить стандартную радиацию на РТ (Это нужно в случае если у Вас отключена радиация, плагин включит её обратно но уберёт на РТ)")]
			public bool DisableDefaultRadiation;

			[JsonProperty(PropertyName = "Заблокировать трейд?")]
			public bool CanTrade;

			[JsonProperty(PropertyName = "Заблокировать backpack?")]
			public bool CanBackpack;

			[JsonProperty(PropertyName = "Включить работу с True PVE?")]
			public bool UseTruePVE;

			[JsonProperty("Заблокированные команды", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> commands;
		}

		public class Loot
		{
			[JsonProperty("Выдавать предмет?")] public bool itemenabled;
			[JsonProperty("Выдавать баланс?")] public bool cashenabled;

			[JsonProperty("Настройка предмета", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<List<Items>> items;

			[JsonProperty("Настройка баланса")] public Cash cash;
		}

		public class ItemConfClass : ItemClass
		{
			[JsonProperty(PropertyName = "Минимальное количество предмета")]
			public int minAmount;

			[JsonProperty(PropertyName = "Максимальное количество предмета")]
			public int maxAmount;
		}

		public class Items
		{
			[JsonProperty(PropertyName = "Тип предмета")] [JsonConverter(typeof(StringEnumConverter))]
			public ItemTypes Type;

			[JsonProperty(PropertyName = "Предмет")]
			public ItemConfClass Item;

			[JsonProperty(PropertyName = "Команда")]
			public string Command;

			[JsonProperty(PropertyName = "Отображаемое имя (если ковычки - дефолт из игры)")]
			public string DisplayName;

			[JsonProperty(PropertyName = "Картинка (если пусто - по шортнейму)")]
			public string ImageURL;

			public DataItem ToDataItem()
			{
				return new DataItem
				{
					Type = Type,
					item = new ItemClass
					{
						ShortName = Item.ShortName,
						SkinID = Item.SkinID
					},
					Command = Command,
					DisplayName = DisplayName,
					Amount = Type == ItemTypes.Item ? Random.Range(Item.minAmount, Item.maxAmount) : 1,
					ImageURL = ImageURL
				};
			}
		}

		public class Cash
		{
			[JsonProperty("Название функции для вызова")]
			public string function;

			[JsonProperty("Название плагина")] public string plugin;
			[JsonProperty("Количество денег")] public int amount;
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				_config = Config.ReadObject<Configuration>();
				if (_config == null) throw new Exception();
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

		#endregion

		#region Data

		private static PluginData _data;

		private void SaveData()
		{
			Core.Interface.Oxide.DataFileSystem.WriteObject(Name, _data);
		}

		private void LoadData()
		{
			try
			{
				_data = Core.Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			if (_data == null) _data = new PluginData();
		}

		private class PluginData
		{
			[JsonProperty(PropertyName = "Players Data", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<ulong, List<DataItem>> PlayersData = new Dictionary<ulong, List<DataItem>>();
		}

		public class DataItem
		{
			[JsonProperty(PropertyName = "Тип предмета")]
			public ItemTypes Type;

			[JsonProperty(PropertyName = "Предмет")]
			public ItemClass item;

			[JsonProperty(PropertyName = "Команда")]
			public string Command;

			[JsonProperty(PropertyName = "Отображаемое имя (если ковычки - дефолт из игры)")]
			public string DisplayName;

			[JsonProperty(PropertyName = "Количество")]
			public int Amount;

			[JsonProperty(PropertyName = "Картинка (если пусто - по шортнейму)")]
			public string ImageURL;

			public Item ToItem()
			{
				var newItem = ItemManager.CreateByName(item.ShortName, Amount, item.SkinID);

				if (newItem == null)
				{
					_instance?.PrintError($"Error creating item with shortname '{item.ShortName}'");
					return null;
				}

				if (!string.IsNullOrEmpty(DisplayName)) newItem.name = DisplayName;
				return newItem;
			}

			public void GiveCmd(BasePlayer player)
			{
				var command = Command.Replace("\n", "|")
					.Replace("%steamid%", player.UserIDString, StringComparison.OrdinalIgnoreCase).Replace("%username%",
						player.displayName, StringComparison.OrdinalIgnoreCase);
				_instance?.Server.Command(command);
			}
		}

		public class ItemClass
		{
			[JsonProperty(PropertyName = "Короткое название предмета")]
			public string ShortName;

			[JsonProperty(PropertyName = "Skin ID предмета")]
			public ulong SkinID;
		}

		#endregion

		#region Initialization

		private void OnServerInitialized()
		{
			LoadData();

			PrintWarning("  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
			PrintWarning($"     {Name} v{Version} loading");
			if (!ImageLibrary)
			{
				PrintError("   Install plugin: 'ImageLibrary'");
				Core.Interface.Oxide.UnloadPlugin(Title);
				return;
			}

			PrintWarning("        Plugin loaded - OK");
			PrintWarning("  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");

			if (_config.useTimer)
			{
				aStartEvent = () => StartEvent(_config.EnabledEvents.GetRandom());
				InvokeHandler.Instance.InvokeRandomized(aStartEvent, _config.Delay, _config.Delay, _config.RandomTime);
			}

			cmd.AddChatCommand(_config.cmdStorage, this, nameof(CmdChatOpenStorage));

			AddCovalenceCommand("event", nameof(CmdEventConsole));

			if (!permission.PermissionExists(_config.perm_admin, this))
				permission.RegisterPermission(_config.perm_admin, this);

			#region ImageLibrary{RPID}

			ImageLibrary.Call("AddImage", _config.CollectionResources.ImageUrl, "CollectionResources");
			ImageLibrary.Call("AddImage", _config.FoundationDrop.ImageUrl, "FoundationDrop");
			ImageLibrary.Call("AddImage", _config.HelicopterPet.ImageUrl, "HelicopterPet");
			ImageLibrary.Call("AddImage", _config.HuntAnimal.ImageUrl, "HuntAnimal");
			ImageLibrary.Call("AddImage", _config.KingMountain.ImageUrl, "KingMountain");
			ImageLibrary.Call("AddImage", _config.LookingLoot.ImageUrl, "LookingLoot");
			ImageLibrary.Call("AddImage", _config.SpecialCargo.ImageUrl, "SpecialCargo");
			ImageLibrary.Call("AddImage", "https://i.imgur.com/Okt1BMH.png", "ME_Background_image");
			ImageLibrary.Call("AddImage", "https://i.imgur.com/DpLldC8.png", "ME_Logo_image");

			foreach (var img in _config.CollectionResources.AwardItems.SelectMany(p => p.items).SelectMany(p => p))
				if (!string.IsNullOrEmpty(img.ImageURL))
					ImageLibrary.Call("AddImage", img.ImageURL, img.ImageURL);

			foreach (var img in _config.FoundationDrop.AwardItems.SelectMany(p => p.items).SelectMany(p => p))
				if (!string.IsNullOrEmpty(img.ImageURL))
					ImageLibrary.Call("AddImage", img.ImageURL, img.ImageURL);

			foreach (var img in _config.HelicopterPet.AwardItems.SelectMany(p => p.items).SelectMany(p => p))
				if (!string.IsNullOrEmpty(img.ImageURL))
					ImageLibrary.Call("AddImage", img.ImageURL, img.ImageURL);

			foreach (var img in _config.HuntAnimal.AwardItems.SelectMany(p => p.items).SelectMany(p => p))
				if (!string.IsNullOrEmpty(img.ImageURL))
					ImageLibrary.Call("AddImage", img.ImageURL, img.ImageURL);

			foreach (var img in _config.KingMountain.AwardItems.SelectMany(p => p.items).SelectMany(p => p))
				if (!string.IsNullOrEmpty(img.ImageURL))
					ImageLibrary.Call("AddImage", img.ImageURL, img.ImageURL);

			foreach (var img in _config.LookingLoot.AwardItems.SelectMany(p => p.items).SelectMany(p => p))
				if (!string.IsNullOrEmpty(img.ImageURL))
					ImageLibrary.Call("AddImage", img.ImageURL, img.ImageURL);

			foreach (var img in _config.SpecialCargo.AwardItems.SelectMany(p => p.items).SelectMany(p => p))
				if (!string.IsNullOrEmpty(img.ImageURL))
					ImageLibrary.Call("AddImage", img.ImageURL, img.ImageURL);

			foreach (var img in _data.PlayersData.SelectMany(p => p.Value))
				if (!string.IsNullOrEmpty(img.ImageURL))
					ImageLibrary.Call("AddImage", img.ImageURL, img.ImageURL);

			#endregion

			#region FoundationDrop

			var entityList = BaseNetworkable.serverEntities.entityList.Values;
			for (var i = 0; i < entityList.Count; i++)
			{
				var entity = entityList[i] as BuildingBlock;

				if (entity == null || entity.IsDestroyed || entity.OwnerID != 98596) continue;

				entity.Kill();
			}

			#endregion

			_instance = this;
		}

		private void Init()
		{
			Unsubscribe(nameof(OnCollectiblePickup));
			Unsubscribe(nameof(OnGrowableGather));
			Unsubscribe(nameof(OnDispenserBonus));
			Unsubscribe(nameof(OnDispenserGather));
			Unsubscribe(nameof(OnEntityDeath));
			Unsubscribe(nameof(OnLootEntity));
			Unsubscribe(nameof(OnPlayerDeath));
			Unsubscribe(nameof(OnEntityKill));
			Unsubscribe(nameof(ClearTeleport));
			Unsubscribe(nameof(OnPlayerDisconnected));
			Unsubscribe(nameof(DropFoundation));
			Unsubscribe(nameof(OnUserCommand));
			Unsubscribe(nameof(OnServerCommand));
			Unsubscribe(nameof(OnPlayerLand));
			Unsubscribe(nameof(OnRunPlayerMetabolism));
			Unsubscribe(nameof(CanTeleport));
			Unsubscribe(nameof(CanTrade));
			Unsubscribe(nameof(CanBackpack));
			Unsubscribe(nameof(CanEntityTakeDamage));
		}

		private void OnServerSave()
		{
			SaveData();
		}

		private void Unload()
		{
			SaveData();

			InvokeHandler.Instance.CancelInvoke(aStartEvent);

			InvokeHandler.Instance.CancelInvoke(EventRepeating);
			InvokeHandler.Instance.CancelInvoke(DestroyEvent);

			if (hasStarted)
				try
				{
					DestroyEvent();
				}
				catch (NullReferenceException)
				{
					foreach (var player in BasePlayer.activePlayerList)
					{
						CuiHelper.DestroyUi(player, Layer);
						CuiHelper.DestroyUi(player, Layer + ".Notification");
						CuiHelper.DestroyUi(player, Layer + ".SpecialCargo");
						CuiHelper.DestroyUi(player, Layer + ".FoundationDrop.Play");
					}


					foreach (var entity in BaseNetworkable.serverEntities.OfType<BuildingBlock>())
					{
						if (entity == null || entity.IsDestroyed || entity.OwnerID != 98596) continue;

						entity.Kill();
					}

					Downgraders.ToList().ForEach(component =>
					{
						if (component != null)
							component.Kill();
					});

					SCmarkers.ToList().ForEach(component =>
					{
						if (component != null)
							component.Kill();
					});

					Zones.ToList().ForEach(component =>
					{
						if (component != null)
							component.Kill();
					});
				}

			_instance = null;
			cEvent = null;
			_config = null;
			_data = null;
		}

		#endregion

		#region Functions

		private void StartEvent(string type, BasePlayer target = null)
		{
			InvokeHandler.Instance.CancelInvoke(EventRepeating);
			InvokeHandler.Instance.CancelInvoke(DestroyEvent);

			if (hasStarted || !string.IsNullOrEmpty(nowEvent))
			{
				DestroyEvent();
				return;
			}

			if (Core.Interface.CallHook("OnEventStart", type, target) != null)
				return;

			nowEvent = type;

			PlayersTop = new Dictionary<BasePlayer, float>();

			switch (type)
			{
				case "KingMountain":
				{
					if (BasePlayer.activePlayerList.Count < _config.KingMountain.MinPlayers && target == null)
					{
						nowEvent = null;
						return;
					}

					foreach (var player in BasePlayer.activePlayerList)
						TopUI(player, "all",
							GetMessage(type, player.UserIDString),
							oMin: _config.KingMountain.ui.oMin, oMax: _config.KingMountain.ui.oMax,
							color: _config.KingMountain.ui.color);

					time = _config.KingMountain.TimeDelay;

					EventRepeating = () =>
					{
						var list = PlayersTop.OrderByDescending(p => p.Value).Take(8).ToList();

						foreach (var player in BasePlayer.activePlayerList)
						{
							if (player == null || player.IsNpc) continue;

							TopUI(player, "refresh", list: list);

							if (player.GetMounted() || player.IsFlying
							                        || _config.KingMountain.checkBuilding && player.IsBuildingAuthed()
							                        || _config.KingMountain.checkDuels && IsDuelPlayer(player)
							                        || _config.KingMountain.checkScrapheli &&
							                        player.GetComponentInParent<ScrapTransportHelicopter>() != null
							                        || _config.KingMountain.checkMinicopter &&
							                        player.GetComponentInParent<MiniCopter>() != null
							                        || _config.KingMountain.checkHotair &&
							                        player.GetComponentInParent<HotAirBalloon>() != null) continue;

							if (PlayersTop.ContainsKey(player))
								PlayersTop[player] = player.transform.position.y;
							else
								PlayersTop.Add(player, player.transform.position.y);
						}

						time--;
					};

					InvokeHandler.Instance.InvokeRepeating(EventRepeating, 0, 1);

					InvokeHandler.Instance.Invoke(DestroyEvent, _config.KingMountain.TimeDelay + 1);
				}
					break;
				case "CollectionResources":
				{
					if (BasePlayer.activePlayerList.Count < _config.CollectionResources.MinPlayers && target == null)
					{
						nowEvent = null;
						return;
					}

					Subscribe(nameof(OnCollectiblePickup));
					Subscribe(nameof(OnGrowableGather));
					Subscribe(nameof(OnDispenserBonus));
					Subscribe(nameof(OnDispenserGather));

					foreach (var player in BasePlayer.activePlayerList)
						TopUI(player, "all",
							GetMessage(type, player.UserIDString),
							oMin: _config.CollectionResources.ui.oMin, oMax: _config.CollectionResources.ui.oMax,
							color: _config.CollectionResources.ui.color);

					time = _config.CollectionResources.TimeDelay;

					EventRepeating = () =>
					{
						var list = PlayersTop.OrderByDescending(p => p.Value).Take(8).ToList();
						foreach (var player in BasePlayer.activePlayerList)
							TopUI(player, "refresh", list: list);

						time--;
					};

					InvokeHandler.Instance.InvokeRepeating(EventRepeating, 0, 1);

					InvokeHandler.Instance.Invoke(DestroyEvent, _config.CollectionResources.TimeDelay + 1);
				}
					break;
				case "HuntAnimal":
				{
					if (BasePlayer.activePlayerList.Count < _config.HuntAnimal.MinPlayers && target == null)
					{
						nowEvent = null;
						return;
					}

					Subscribe(nameof(OnEntityDeath));

					foreach (var player in BasePlayer.activePlayerList)
						TopUI(player, "all",
							GetMessage(type, player.UserIDString),
							oMin: _config.HuntAnimal.ui.oMin, oMax: _config.HuntAnimal.ui.oMax,
							color: _config.HuntAnimal.ui.color);

					time = _config.HuntAnimal.TimeDelay;

					EventRepeating = () =>
					{
						var list = PlayersTop.OrderByDescending(p => p.Value).Take(8).ToList();
						foreach (var player in BasePlayer.activePlayerList)
							TopUI(player, "refresh", list: list);

						time--;
					};

					InvokeHandler.Instance.InvokeRepeating(EventRepeating, 0, 1);

					InvokeHandler.Instance.Invoke(DestroyEvent, _config.HuntAnimal.TimeDelay + 1);
				}
					break;
				case "HelicopterPet":
				{
					if (BasePlayer.activePlayerList.Count < _config.HelicopterPet.MinPlayers && target == null)
					{
						nowEvent = null;
						return;
					}

					Subscribe(nameof(OnEntityKill));
					Subscribe(nameof(HeliPet));

					foreach (var player in BasePlayer.activePlayerList)
						TopUI(player, "all",
							GetMessage(type, player.UserIDString),
							oMin: _config.HelicopterPet.ui.oMin, oMax: _config.HelicopterPet.ui.oMax,
							color: _config.HelicopterPet.ui.color);

					time = _config.HelicopterPet.TimeDelay;

					var heli = GameManager.server.CreateEntity(
						"assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab") as BaseHelicopter;
					if (heli != null)
					{
						heli.OwnerID = 999999999;
						heli.Spawn();
						heli.transform.position = GetHeliSpawn(Vector3.zero); // new Vector3(0, 350, 0);
						heli.gameObject.AddComponent<HeliPet>();
						NextTick(() =>
						{
							if (heli != null) heli.myAI.Update();
						});
					}

					EventRepeating = () =>
					{
						var list = PlayersTop.OrderByDescending(p => p.Value).Take(8).ToList();
						foreach (var player in BasePlayer.activePlayerList)
							TopUI(player, "refresh", list: list);

						time--;
					};

					InvokeHandler.Instance.InvokeRepeating(EventRepeating, 0, 1);

					InvokeHandler.Instance.Invoke(DestroyEvent, _config.HelicopterPet.TimeDelay + 1);
				}
					break;
				case "LookingLoot":
				{
					if (BasePlayer.activePlayerList.Count < _config.LookingLoot.MinPlayers && target == null)
					{
						nowEvent = null;
						return;
					}

					Subscribe(nameof(OnLootEntity));
					Subscribe(nameof(OnEntityDeath));

					foreach (var player in BasePlayer.activePlayerList)
						TopUI(player, "all",
							GetMessage(type, player.UserIDString),
							oMin: _config.LookingLoot.ui.oMin, oMax: _config.LookingLoot.ui.oMax,
							color: _config.LookingLoot.ui.color);

					time = _config.LookingLoot.TimeDelay;

					EventRepeating = () =>
					{
						var list = PlayersTop?.OrderByDescending(p => p.Value)?.Take(8)?.ToList();

						if (list != null)
							foreach (var player in BasePlayer.activePlayerList)
								TopUI(player, "refresh", list: list);

						time--;
					};

					InvokeHandler.Instance.InvokeRepeating(EventRepeating, 0, 1);

					InvokeHandler.Instance.Invoke(DestroyEvent, _config.LookingLoot.TimeDelay + 1);
				}
					break;
				case "SpecialCargo":
				{
					if (BasePlayer.activePlayerList.Count < _config.SpecialCargo.MinPlayers && target == null)
					{
						nowEvent = null;
						return;
					}

					Subscribe(nameof(OnPlayerDeath));
					Subscribe(nameof(SCmarker));
					if (_config.SpecialCargo.tpBlock) Subscribe(nameof(CanTeleport));

					time = _config.SpecialCargo.TimeDelay;

					var monuments = TerrainMeta.Path.Monuments.FindAll(CanMonument).ToList();

					if (monuments.Count < 2)
					{
						nowEvent = null;
						return;
					}

					beginning_mission = monuments.GetRandom();
					if (beginning_mission == null)
					{
						nowEvent = null;
						return;
					}

					monuments.Remove(beginning_mission);

					end_mission = monuments.GetRandom();
					if (end_mission == null)
					{
						beginning_mission = null;
						nowEvent = null;
						return;
					}

					foreach (var player in BasePlayer.activePlayerList)
						UI_Notification(player,
							GetMessage("SpecialCargo.New", player.UserIDString,
								beginning_mission.displayPhrase.translated), ".Notification.New");

					timer.In(_config.SpecialCargo.timeStart, () =>
					{
						foreach (var player in BasePlayer.activePlayerList)
							CuiHelper.DestroyUi(player, Layer + ".Notification.New");
					});

					//add image to map
					beginning_mission.gameObject.AddComponent<SCmarker>();

					EventRepeating = () =>
					{
						foreach (var player in BasePlayer.activePlayerList)
						{
							if (player == null || player.IsNpc) continue;

							if (runner == null)
							{
								if (beginning_mission != null &&
								    !(Vector3.Distance(beginning_mission.transform.position,
									    player.transform.position) < 100f)) continue;

								runner = player;
								UI_Notification(player,
									GetMessage("SpecialCargo.CreateRunner", player.UserIDString,
										end_mission.displayPhrase.translated,
										GetGridString(end_mission.transform.position)), ".Notification.CreateRunner");

								timer.In(_config.SpecialCargo.timeNewRunner,
									() => CuiHelper.DestroyUi(player, Layer + ".Notification.CreateRunner"));

								if (beginning_mission != null)
									beginning_mission.GetComponent<SCmarker>()?.Kill();

								player.gameObject.AddComponent<SCPlayerMarker>()?.SpawnMarker();
								TopUI(player, nowEvent);
								break;
							}

							if (runner == player)
							{
								if (end_mission != null &&
								    !(Vector3.Distance(end_mission.transform.position, player.transform.position) <
								      100f)) continue;
								DestroyEvent();
								break;
							}
						}

						time--;
					};

					InvokeHandler.Instance.InvokeRepeating(EventRepeating, 0, 1);

					InvokeHandler.Instance.Invoke(DestroyEvent, _config.SpecialCargo.TimeDelay);
				}
					break;
				case "FoundationDrop":
				{
					if (BasePlayer.activePlayerList.Count < _config.FoundationDrop.MinPlayers && target == null)
					{
						nowEvent = null;
						return;
					}

					EventPosition = new Vector3(-(float) World.Size / 2, 400, -(float) World.Size / 2);

					Subscribe(nameof(ClearTeleport));
					Subscribe(nameof(OnPlayerDisconnected));
					Subscribe(nameof(DropFoundation));
					Subscribe(nameof(OnServerCommand));
					Subscribe(nameof(OnUserCommand));
					Subscribe(nameof(OnPlayerLand));
					Subscribe(nameof(OnRunPlayerMetabolism));
					Subscribe(nameof(CanTrade));
					Subscribe(nameof(CanBackpack));
					Subscribe(nameof(CanEntityTakeDamage));

					time = _config.FoundationDrop.WaitTime;
					cEvent = new FoundationDrop();
					cEvent?.InitializeEvent(_config.FoundationDrop.WaitTime);
				}
					break;
			}

			hasStarted = true;
		}

		private void DestroyEvent()
		{
			if (Core.Interface.CallHook("OnEventDestroy") != null)
				return;

			InvokeHandler.Instance.CancelInvoke(EventRepeating);
			InvokeHandler.Instance.CancelInvoke(DestroyEvent);

			try
			{
				switch (nowEvent)
				{
					case "KingMountain":
						if (PlayersTop.Count > 0)
						{
							var i = 0;
							foreach (var player in PlayersTop.OrderByDescending(p => p.Value)
								.Take(_config.KingMountain.AwardItems.Count).Select(x => x.Key))
							{
								if (player == null) continue;

								DropItem(player, nowEvent, i);
								Winners.Add(player.displayName);
								i++;
							}
						}

						break;
					case "CollectionResources":
						Unsubscribe(nameof(OnCollectiblePickup));
						Unsubscribe(nameof(OnGrowableGather));
						Unsubscribe(nameof(OnDispenserBonus));
						Unsubscribe(nameof(OnDispenserGather));

						if (PlayersTop.Count > 0)
						{
							var i = 0;
							foreach (var player in PlayersTop.OrderByDescending(p => p.Value)
								.Take(_config.CollectionResources.AwardItems.Count).Select(x => x.Key))
							{
								if (player == null) continue;

								DropItem(player, nowEvent, i);
								Winners.Add(player.displayName);
								i++;
							}
						}

						break;
					case "HuntAnimal":
						Unsubscribe(nameof(OnEntityDeath));

						if (PlayersTop.Count > 0)
						{
							var i = 0;
							foreach (var player in PlayersTop.OrderByDescending(p => p.Value)
								.Take(_config.HuntAnimal.AwardItems.Count).Select(x => x.Key))
							{
								if (player == null) continue;

								DropItem(player, nowEvent, i);
								Winners.Add(player.displayName);
								i++;
							}
						}

						break;
					case "HelicopterPet":
						Unsubscribe(nameof(OnEntityKill));

						foreach (var entity in BaseNetworkable.serverEntities.OfType<BaseHelicopter>())
						{
							if (entity == null || entity.IsDestroyed || entity.OwnerID != 999999999) continue;
							entity.Kill();
						}

						if (PlayersTop.Count > 0)
						{
							var i = 0;
							foreach (var player in PlayersTop.OrderByDescending(p => p.Value)
								.Take(_config.HelicopterPet.AwardItems.Count).Select(x => x.Key))
							{
								if (player == null) continue;

								DropItem(player, nowEvent, i);
								Winners.Add(player.displayName);
								i++;
							}
						}

						break;
					case "LookingLoot":
						Unsubscribe(nameof(OnLootEntity));
						Unsubscribe(nameof(OnEntityDeath));

						if (PlayersTop.Count > 0)
						{
							var i = 0;
							foreach (var player in PlayersTop.OrderByDescending(p => p.Value)
								.Take(_config.LookingLoot.AwardItems.Count).Select(x => x.Key))
							{
								if (player == null) continue;

								DropItem(player, nowEvent, i);
								Winners.Add(player.displayName);
								i++;
							}
						}

						break;
					case "SpecialCargo":
						Unsubscribe(nameof(OnPlayerDeath));
						Unsubscribe(nameof(SCmarker));

						foreach (var marker in UnityEngine.Object.FindObjectsOfType<SCmarker>())
							if (marker != null)
								marker.Kill();

						if (_config.SpecialCargo.tpBlock) Unsubscribe(nameof(CanTeleport));

						if (runner != null)
						{
							winner = runner;
							DropItem(runner, nowEvent);
							runner.gameObject.GetComponent<SCPlayerMarker>()?.Kill();
							Winners.Add(runner.displayName);
						}

						break;
					case "FoundationDrop":
						Unsubscribe(nameof(ClearTeleport));
						Unsubscribe(nameof(OnPlayerDisconnected));
						Unsubscribe(nameof(DropFoundation));
						Unsubscribe(nameof(OnUserCommand));
						Unsubscribe(nameof(OnServerCommand));
						Unsubscribe(nameof(OnPlayerLand));
						Unsubscribe(nameof(OnRunPlayerMetabolism));
						Unsubscribe(nameof(CanTrade));
						Unsubscribe(nameof(CanBackpack));
						Unsubscribe(nameof(CanEntityTakeDamage));

						cEvent?.FinishEvent();
						cEvent = null;
						break;
				}
			}
			catch (Exception e)
			{
				//ignore
			}


			if (PlayersTop.Count == 0 && winner == null)
			{
				foreach (var player in BasePlayer.activePlayerList)
				{
					CuiHelper.DestroyUi(player, Layer);
					UI_Notification(player, GetMessage("WINNER.NOTFOUND", player.UserIDString));
				}

				timer.In(4, () =>
				{
					PlayersTop.Clear();

					foreach (var player in BasePlayer.activePlayerList)
						CuiHelper.DestroyUi(player, Layer + ".Notification");
				});
			}
			else
			{
				var messageKey = Winners.Count > 1 ? "EVENT.END.MULTI" : "EVENT.END";
				var winns = Winners.Count > 1 ? string.Join(", ", Winners) :
					Winners.Count == 1 ? Winners.First() : string.Empty;

				foreach (var player in BasePlayer.activePlayerList)
				{
					CuiHelper.DestroyUi(player, Layer);
					CuiHelper.DestroyUi(player, Layer + ".SpecialCargo");
					CuiHelper.DestroyUi(player, Layer + ".FoundationDrop.Play");

					if (string.IsNullOrEmpty(winns)) continue;

					UI_Notification(player,
						winner == player
							? GetMessage("EVENT.YOUWINNER", player.UserIDString)
							: GetMessage(messageKey, player.UserIDString, GetMessage(nowEvent, player.UserIDString),
								winns));
				}

				timer.In(4, () =>
				{
					PlayersTop.Clear();

					foreach (var player in BasePlayer.activePlayerList)
						CuiHelper.DestroyUi(player, Layer + ".Notification");
				});
			}

			winner = null;
			nowEvent = null;
			hasStarted = false;

			runner = null;
			beginning_mission = null;
			end_mission = null;
			PlayersTop?.Clear();
			LookingLoot?.Clear();
			Winners?.Clear();

			Core.Interface.CallHook("OnEventDestroyed");
		}

		private void DropItem(BasePlayer player, string type, int temp = 0)
		{
			if (player == null) return;

			var items = Pool.GetList<Items>();
			Cash cash = null;

			Loot loot = null;

			switch (type)
			{
				case "KingMountain":
					if (_config.KingMountain.AwardItems.Count > temp)
						loot = _config.KingMountain.AwardItems[temp];
					break;
				case "CollectionResources":

					if (_config.CollectionResources.AwardItems.Count > temp)
						loot = _config.CollectionResources.AwardItems[temp];
					break;
				case "HuntAnimal":

					if (_config.HuntAnimal.AwardItems.Count > temp)
						loot = _config.HuntAnimal.AwardItems[temp];
					break;
				case "HelicopterPet":

					if (_config.HelicopterPet.AwardItems.Count > temp)
						loot = _config.HelicopterPet.AwardItems[temp];
					break;
				case "LookingLoot":

					if (_config.LookingLoot.AwardItems.Count > temp)
						loot = _config.LookingLoot.AwardItems[temp];
					break;
				case "SpecialCargo":

					if (_config.SpecialCargo.AwardItems.Count > temp)
						loot = _config.SpecialCargo.AwardItems[temp];
					break;
				case "FoundationDrop":

					if (_config.FoundationDrop.AwardItems.Count > temp)
						loot = _config.FoundationDrop.AwardItems[temp];
					break;
			}

			if (loot != null)
			{
				if (loot.itemenabled)
					foreach (var item in loot.items)
					{
						var it = item?.GetRandom();
						if (it == null) continue;
						items.Add(it);
					}

				if (loot.cashenabled) cash = loot.cash;
			}

			if (items.Count > 0)
				foreach (var item in items)
				{
					var dataItem = item?.ToDataItem();
					if (dataItem == null) continue;

					AddItem(player, dataItem);
				}

			Pool.FreeList(ref items);

			if (cash != null)
			{
				var plugin = plugins.Find(cash.plugin);

				if (plugin == null)
				{
					PrintError("ECONOMY PLUGIN NOT FOUND");
					return;
				}

				switch (cash.plugin)
				{
					case "RustStore":
					{
						plugin.Call(cash.function, player.userID, cash.amount, new Action<string>(result =>
						{
							if (result == "SUCCESS")
							{
								Core.Interface.Oxide.LogDebug(
									$"Игрок {player.displayName} получил {cash.amount} на баланс в магазине");
								return;
							}

							Core.Interface.Oxide.LogDebug($"Баланс не был изменен, ошибка: {result}");
						}));
						break;
					}
					case "GameStoresRUST":
					{
						var args = cash.function.Split(' ');
						if (args.Length != 3)
						{
							PrintError(
								"Не правильно настроена выдача для магазина GameStoresRUST! (ИД_магазина_в_сервисе Секретный_ключ ИД_сервера_в_сервисе)");
							return;
						}

						webrequest.Enqueue(
							$"https://gamestores.ru/api/?shop_id={args[0]}&secret={args[1]}&server={args[2]}&action=moneys&type=plus&steam_id={player.UserIDString}&amount={cash.amount}",
							"", (code, response) =>
							{
								switch (code)
								{
									case 0:
									{
										PrintError("Api does not responded to a request");
										break;
									}
									case 200:
									{
										PrintToConsole($"{player.displayName} wins {cash.amount} in award.");
										break;
									}
									case 404:
									{
										PrintError("Plese check your configuration! [404]");
										break;
									}
								}
							}, this);
						break;
					}
					case "Economics":
					{
						plugin.Call(cash.function, player.userID, (double) cash.amount);
						break;
					}
					default:
					{
						plugin.Call(cash.function, player.userID, cash.amount);
						break;
					}
				}
			}
		}

		#endregion

		#region Hooks

		private void OnPlayerDisconnected(BasePlayer player, string reason)
		{
			if (player == null || cEvent == null || !cEvent.PlayerConnected.ContainsKey(player.userID)) return;

			cEvent.LeftEvent(player);
		}

		private void OnPlayerConnected(BasePlayer player)
		{
			if (player == null) return;

			GetAvatar(player.userID,
				avatar => ImageLibrary?.Call("AddImage", avatar, $"avatar_{player.UserIDString}"));

			if (!_data.PlayersData.ContainsKey(player.userID))
				_data.PlayersData.Add(player.userID, new List<DataItem>());

			if (!hasStarted || string.IsNullOrEmpty(nowEvent)) return;

			switch (nowEvent)
			{
				case "KingMountain":
				{
					TopUI(player, "all", GetMessage(nowEvent, player.UserIDString), oMin: _config.KingMountain.ui.oMin,
						oMax: _config.KingMountain.ui.oMax, color: _config.KingMountain.ui.color);
					break;
				}
				case "CollectionResources":
				{
					TopUI(player, "all", GetMessage(nowEvent, player.UserIDString),
						oMin: _config.CollectionResources.ui.oMin, oMax: _config.CollectionResources.ui.oMax,
						color: _config.CollectionResources.ui.color);
					break;
				}
				case "HuntAnimal":
				{
					TopUI(player, "all", GetMessage(nowEvent, player.UserIDString), oMin: _config.HuntAnimal.ui.oMin,
						oMax: _config.HuntAnimal.ui.oMax, color: _config.HuntAnimal.ui.color);
					break;
				}
				case "HelicopterPet":
				{
					TopUI(player, "all", GetMessage(nowEvent, player.UserIDString), oMin: _config.HelicopterPet.ui.oMin,
						oMax: _config.HelicopterPet.ui.oMax, color: _config.HelicopterPet.ui.color);
					break;
				}
				case "LookingLoot":
				{
					TopUI(player, "all", GetMessage(nowEvent, player.UserIDString), oMin: _config.LookingLoot.ui.oMin,
						oMax: _config.LookingLoot.ui.oMax, color: _config.LookingLoot.ui.color);
					break;
				}
			}
		}

		private string CanTeleport(BasePlayer player)
		{
			if (player == null || runner == null || player.userID != runner.userID) return null;

			return GetMessage("CantTP", player.UserIDString);
		}

		private object CanTrade(BasePlayer player)
		{
			if (player == null || !hasStarted || cEvent == null
			    || !cEvent.PlayerConnected.ContainsKey(player.userID)
			    || !_config.FoundationDrop.CanTrade) return null;

			return true;
		}

		private object CanBackpack(BasePlayer player)
		{
			if (player == null || !hasStarted || cEvent == null
			    || !cEvent.PlayerConnected.ContainsKey(player.userID)
			    || !_config.FoundationDrop.CanBackpack) return null;

			return true;
		}

		private object CanBuild(Planner plan, Construction prefab, Construction.Target target)
		{
			if (plan == null || prefab == null) return null;

			var player = target.player;
			if (player == null) return null;

			if (Vector3.Distance(player.transform.position, EventPosition) <
			    _config.FoundationDrop.ArenaSize * 1.5f)
			{
				SendReply(player, GetMessage("CantBuild", player.UserIDString));
				return false;
			}

			return null;
		}

		private object CanEntityTakeDamage(BasePlayer player, HitInfo info)
		{
			if (player == null || info == null || !hasStarted || cEvent == null ||
			    !_config.FoundationDrop.UseTruePVE) return null;

			var attacker = info.InitiatorPlayer;
			if (attacker == null) return null;

			if (!cEvent.PlayerConnected.ContainsKey(player.userID) ||
			    !cEvent.PlayerConnected.ContainsKey(attacker.userID)) return null;

			return true;
		}

		private object OnPlayerLand(BasePlayer player, float num)
		{
			if (player == null || num == 0 || !hasStarted || cEvent == null ||
			    !cEvent.PlayerConnected.ContainsKey(player.userID)) return null;
			return false;
		}

		private void OnRunPlayerMetabolism(PlayerMetabolism metabolism, BasePlayer player)
		{
			if (metabolism == null || player == null || !hasStarted || cEvent == null ||
			    !cEvent.PlayerConnected.ContainsKey(player.userID)) return;

			if (player.metabolism.temperature.value < 20) player.metabolism.temperature.value = 21;
		}

		private object OnUserCommand(IPlayer player, string command, string[] args)
		{
			command = command.TrimStart('/').Substring(command.IndexOf(".", StringComparison.Ordinal) + 1).ToLower();
			if (!hasStarted || !_config.FoundationDrop.commands.Contains(command.ToLower())) return null;

			var basePlayer = player.Object as BasePlayer;
			if (basePlayer == null) return null;

			if (!cEvent.PlayerConnected.ContainsKey(basePlayer.userID)) return null;

			SendReply(basePlayer, GetMessage("CommandBlocked", basePlayer.UserIDString));
			return true;
		}


		private object OnServerCommand(ConsoleSystem.Arg arg)
		{
			var connection = arg.Connection;
			if (connection == null || string.IsNullOrEmpty(arg.cmd?.FullName) || cEvent == null) return null;

			if (!_config.FoundationDrop.commands.Contains(arg.cmd.Name.ToLower()) &&
			    !_config.FoundationDrop.commands.Contains(arg.cmd.FullName.ToLower())) return null;

			var player = arg.Player();
			if (player == null) return null;

			if (!cEvent.PlayerConnected.ContainsKey(player.userID)) return null;

			player.ChatMessage(GetMessage("CommandBlocked", player.UserIDString));
			return true;
		}

		private void OnEntityKill(BaseHelicopter entity)
		{
			if (!hasStarted || entity == null || entity.OwnerID != 999999999) return;
			DestroyEvent();
		}

		private void OnPlayerDeath(BasePlayer player, HitInfo info)
		{
			if (player == null || player != runner || info == null || info.InitiatorPlayer == null) return;

			CuiHelper.DestroyUi(player, Layer + ".SpecialCargo");
			player.GetComponent<SCPlayerMarker>()?.Kill();

			if (info.InitiatorPlayer.IsNpc || info.InitiatorPlayer == player)
			{
				runner = BasePlayer.activePlayerList[Random.Range(0, BasePlayer.activePlayerList.Count - 1)];
				for (var i = 0; i < BasePlayer.activePlayerList.Count; i++)
					UI_Notification(BasePlayer.activePlayerList[i],
						GetMessage("RUNNER.BYNPC", BasePlayer.activePlayerList[i].UserIDString, runner.displayName));
			}
			else
			{
				runner = info.InitiatorPlayer;
				UI_Notification(runner,
					GetMessage("RUNNER.FORRUNNER", runner.UserIDString, end_mission.displayPhrase.translated,
						GetGridString(end_mission.transform.position)));
				for (var i = 0; i < BasePlayer.activePlayerList.Count; i++)
					if (BasePlayer.activePlayerList[i] != runner)
						UI_Notification(BasePlayer.activePlayerList[i],
							GetMessage("RUNNER.PLAYER", BasePlayer.activePlayerList[i].UserIDString, player.displayName,
								runner.displayName));
			}

			InvokeHandler.Instance.Invoke(() =>
			{
				for (var i = 0; i < BasePlayer.activePlayerList.Count; i++)
					CuiHelper.DestroyUi(BasePlayer.activePlayerList[i], Layer + ".Notification");
			}, _config.SpecialCargo.timeNewRunner);

			if (runner != null)
			{
				TopUI(runner, nowEvent);
				runner.gameObject.AddComponent<SCPlayerMarker>()?.SpawnMarker();
			}
		}

		private void OnLootEntity(BasePlayer player, LootContainer entity)
		{
			if (entity == null || player == null || LookingLoot.Contains(entity)) return;

			LookingLoot.Add(entity);

			AddToDictionary(player, 1);
		}

		private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
		{
			if (entity == null || info == null || info.InitiatorPlayer == null || info.InitiatorPlayer.IsNpc) return;

			switch (nowEvent)
			{
				case "HuntAnimal":
					if (entity is Chicken)
						AddToDictionary(info.InitiatorPlayer, _config.HuntAnimal.Chicken);
					else if (entity is Wolf)
						AddToDictionary(info.InitiatorPlayer, _config.HuntAnimal.Wolf);
					else if (entity is Boar)
						AddToDictionary(info.InitiatorPlayer, _config.HuntAnimal.Boar);
					else if (entity is Stag)
						AddToDictionary(info.InitiatorPlayer, _config.HuntAnimal.Deer);
					else if (entity is Bear)
						AddToDictionary(info.InitiatorPlayer, _config.HuntAnimal.Bear);
					else if (entity is Horse) AddToDictionary(info.InitiatorPlayer, _config.HuntAnimal.Horse);

					return;
				case "LookingLoot":
					if (_config.LookingLoot.Barrels.Contains(entity.PrefabName))
						AddToDictionary(info.InitiatorPlayer, 1);
					return;
			}
		}

		private void OnCollectiblePickup(Item item, BasePlayer player)
		{
			OnLoot(player, item);
		}

		private void OnGrowableGather(GrowableEntity entity, Item item, BasePlayer player)
		{
			OnLoot(player, item);
		}

		private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
		{
			OnLoot(player, item);
		}

		private void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
		{
			OnLoot(player, item);
		}

		private void OnLoot(BasePlayer player, Item item)
		{
			if (player == null || item == null
			                   || _config.CollectionResources.BlockItems.Contains(
				                   player.GetActiveItem()?.info.shortname)
			) return;

			AddToDictionary(player, item.amount);
		}

		#endregion

		#region Interface

		private void TopUI(BasePlayer player, string Type, string TopName = "", string description = "",
			string oMin = "", string oMax = "", string color = "", List<KeyValuePair<BasePlayer, float>> list = null)
		{
			var container = new CuiElementContainer();
			var position = 35;

			switch (Type)
			{
				case "all":
					container.Add(new CuiPanel
					{
						CursorEnabled = false,
						RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = oMin, OffsetMax = oMax},
						Image = {Color = color}
					}, "Hud", Layer);
					container.Add(new CuiLabel
					{
						RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -25", OffsetMax = "0 -5"},
						Text = {Text = $"<b>{TopName}</b>", FontSize = 15, Align = TextAnchor.MiddleCenter}
					}, Layer);
					container.Add(new CuiPanel
					{
						RectTransform =
							{AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "10 -28", OffsetMax = "-10 -27"},
						Image = {Color = "1 1 1 1"}
					}, Layer, Layer + ".Line");

					container.Add(new CuiLabel
					{
						RectTransform =
							{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "24 0", OffsetMax = "0 0"},
						Text =
						{
							Text = !string.IsNullOrEmpty(description) ? description : "", FontSize = 16,
							Align = TextAnchor.MiddleLeft
						}
					}, Layer, Layer + ".Text");

					CuiHelper.DestroyUi(player, Layer);
					break;
				case "refresh":
					CuiHelper.DestroyUi(player, Layer + ".Timer");
					container.Add(new CuiLabel
					{
						RectTransform =
							{AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 20"},
						Text =
						{
							Text = GetMessage("EVENT.Timer", player.UserIDString, $"{time:0}"), FontSize = 12,
							Align = TextAnchor.MiddleCenter
						}
					}, Layer, Layer + ".Timer");
					container.Add(new CuiPanel
					{
						RectTransform =
							{AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "10 0", OffsetMax = "-10 1"},
						Image = {Color = "1 1 1 1"}
					}, Layer + ".Timer", Layer + ".Timer.Line");

					if (list != null)
						for (var i = 0; i < list.Count; i++)
						{
							var check = list[i];

							CuiHelper.DestroyUi(player, Layer + $".TopLabel.{i}");

							container.Add(new CuiPanel
							{
								RectTransform =
								{
									AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"20 -{position + 20}",
									OffsetMax = $"-20 -{position}"
								},
								Image = {Color = "0 0 0 0"}
							}, Layer, Layer + $".TopLabel.{i}");

							container.Add(new CuiLabel
							{
								RectTransform =
									{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "24 0", OffsetMax = "0 0"},
								Text =
								{
									Text = $"<b>{GetShortNickname(check.Key)}</b>", FontSize = 16,
									Align = TextAnchor.MiddleLeft
								}
							}, Layer + $".TopLabel.{i}", Layer + $".TopLabel.{i}.Text");

							container.Add(new CuiElement
							{
								Name = Layer + $".TopLabel.{i}.Avatar",
								Parent = Layer + $".TopLabel.{i}",
								Components =
								{
									new CuiRawImageComponent
									{
										Png = ImageLibrary.Call<string>("GetImage", $"avatar_{check.Key.UserIDString}")
									},
									new CuiRectTransformComponent
										{AnchorMin = "0 0", AnchorMax = "0 1", OffsetMax = "20 0"}
								}
							});
							container.Add(new CuiLabel
							{
								RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
								Text =
								{
									Text = nowEvent == "KingMountain" ? $"{check.Value:0} м" :
										nowEvent == "CollectionResources" ? $"{check.Value} шт" : $"{check.Value}",
									FontSize = 16, Align = TextAnchor.MiddleRight
								}
							}, Layer + $".TopLabel.{i}", Layer + $".TopLabel.{i}.Height");
							position += 30;
						}

					break;
				case "FoundationDrop_Timer":
					CuiHelper.DestroyUi(player, Layer + ".Timer");
					container.Add(new CuiLabel
					{
						RectTransform =
							{AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 20"},
						Text =
						{
							Text = GetMessage("FOUNDROP.WaitTime", player.UserIDString, $"{time:0}"), FontSize = 12,
							Align = TextAnchor.MiddleCenter
						}
					}, Layer, Layer + ".Timer");
					container.Add(new CuiPanel
					{
						RectTransform =
							{AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "10 0", OffsetMax = "-10 1"},
						Image = {Color = "1 1 1 1"}
					}, Layer + ".Timer", Layer + ".Timer.Line");
					container.Add(new CuiPanel
					{
						RectTransform =
							{AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "10 0", OffsetMax = "-10 1"},
						Image = {Color = "1 1 1 1"}
					}, Layer + ".Timer", Layer + ".Timer.Line");
					break;
				case "FoundationDrop.Upd":
					CuiHelper.DestroyUi(player, Layer + ".Text");
					container.Add(new CuiLabel
					{
						RectTransform =
							{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "10 0", OffsetMax = "-10 -40"},
						Text =
						{
							Text = GetMessage("FOUNDROP.BlockAndPlayers", player.UserIDString,
								cEvent.BlockList.Count.ToString(), cEvent.PlayerConnected.Count.ToString()),
							FontSize = 18, Align = TextAnchor.UpperLeft
						}
					}, Layer, Layer + ".Text");
					break;
				case "SpecialCargo":
					CuiHelper.DestroyUi(player, Layer + ".SpecialCargo");
					container.Add(new CuiPanel
					{
						CursorEnabled = false,
						RectTransform =
							{AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-225 -45", OffsetMax = "-5 -5"},
						Image = {Color = "0.024 0.016 0.17 0.7"}
					}, "Hud", Layer + ".SpecialCargo");
					container.Add(new CuiLabel
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text =
						{
							Text = GetMessage("SpecialCargo.Purpose", player.UserIDString,
								end_mission.displayPhrase.translated,
								GetGridString(end_mission.transform.position)),
							FontSize = 15, Align = TextAnchor.MiddleCenter
						}
					}, Layer + ".SpecialCargo");
					break;
			}

			CuiHelper.AddUi(player, container);
		}

		private static void UI_Notification(BasePlayer player, string message, string Name = ".Notification",
			string color = "0.98 0.37 0.41 0.69")
		{
			CuiHelper.DestroyUi(player, Layer + Name);
			CuiHelper.AddUi(player, new CuiElementContainer
			{
				{
					new CuiButton
					{
						RectTransform =
							{AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -100", OffsetMax = "0 -50"},
						Button = {Color = color},
						Text =
						{
							FadeIn = 1f, Color = "1 1 1 1", FontSize = 18, Align = TextAnchor.MiddleCenter,
							Text = $"{message}"
						}
					},
					"Overlay",
					Layer + Name
				}
			});
		}

		private void Help_UI(BasePlayer player, int page)
		{
			var container = new CuiElementContainer();
			if (_config.EnabledEvents.Count <= page || page < 0)
				page = 0;
			var list = _config.EnabledEvents[page];
			container.Add(new CuiPanel
			{
				CursorEnabled = true,
				RectTransform =
					{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-450 -255", OffsetMax = "450 255"},
				Image = {Color = "0.024 0.017 0.17 0.76"}
			}, "Overlay", Layer + ".Help");
			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "10 10", AnchorMax = "-10 -10"},
				Text = {Text = ""},
				Button = {Color = "0 0 0 0", Close = Layer + ".Help"}
			}, Layer + ".Help");
			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "40 10", OffsetMax = "120 35"},
				Button = {Color = "0 0 0 0", Command = $"UI_Event page {page - 1}"},
				Text =
				{
					Text = GetMessage("UI.Back", player.UserIDString), FontSize = 20, Align = TextAnchor.LowerLeft,
					Font = "robotocondensed-bold.ttf"
				}
			}, Layer + ".Help", Layer + ".Help.Pages.Back");
			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-120 10", OffsetMax = "-40 35"},
				Button = {Color = "0 0 0 0", Command = $"UI_Event page {page + 1}"},
				Text =
				{
					Text = GetMessage("UI.Next", player.UserIDString), FontSize = 20, Align = TextAnchor.LowerRight,
					Font = "robotocondensed-bold.ttf"
				}
			}, Layer + ".Help", Layer + ".Help.Pages.Next");
			container.Add(new CuiLabel
			{
				RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "40 -65", OffsetMax = "-40 0"},
				Text =
				{
					Text = GetMessage("UI.Name", player.UserIDString), FontSize = 38,
					Align = TextAnchor.MiddleCenter
				}
			}, Layer + ".Help", Layer + ".Help.Logo");
			container.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMax = "0 1"},
				Image = {Color = "1 1 1 1"}
			}, Layer + ".Help.Logo", Layer + ".Help.Logo.Line");
			container.Add(new CuiElement
			{
				Name = Layer + ".Help.Logo.Image",
				Parent = Layer + ".Help.Logo",
				Components =
				{
					new CuiImageComponent {Png = (string) ImageLibrary.Call("GetImage", "ME_Logo_image")},
					new CuiRectTransformComponent
						{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-142 -26", OffsetMax = "-90 26"}
				}
			});
			container.Add(new CuiElement
			{
				Name = Layer + ".Help.Event",
				Parent = Layer + ".Help",
				Components =
				{
					new CuiImageComponent {Png = (string) ImageLibrary.Call("GetImage", "ME_Background_image")},
					new CuiRectTransformComponent
						{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-328 -110", OffsetMax = "-90 140"}
				}
			});
			container.Add(new CuiLabel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 -186"},
				Text =
				{
					Text = GetMessage("UI." + list, player.UserIDString), Color = "0 0 0 1", FontSize = 26,
					Align = TextAnchor.MiddleCenter
				}
			}, Layer + ".Help.Event", Layer + ".Help.Event.Name");
			container.Add(new CuiElement
			{
				Name = Layer + ".Help.Event.Image",
				Parent = Layer + ".Help.Event",
				Components =
				{
					new CuiRawImageComponent {Png = (string) ImageLibrary.Call("GetImage", list)},
					new CuiRectTransformComponent
						{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "13 63", OffsetMax = "-13 -15"}
				}
			});
			container.Add(new CuiLabel
			{
				RectTransform =
					{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-45 -200", OffsetMax = "370 170"},
				Text =
				{
					Text = GetMessage("UI.Description." + list, player.UserIDString), FontSize = 22,
					Align = TextAnchor.UpperLeft
				}
			}, Layer + ".Help", Layer + ".Help.Logo");

			container.Add(new CuiButton
			{
				RectTransform =
					{AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-50 10", OffsetMax = "50 35"},
				Button = {Color = "0 0 0 0", Command = "UI_Event storage"},
				Text =
				{
					Text = GetMessage("UI.Storage", player.UserIDString), FontSize = 20,
					Align = TextAnchor.LowerCenter, Color = "0.48 0.41 0.9 1", Font = "robotocondensed-bold.ttf"
				}
			}, Layer + ".Help", Layer + ".Help.Cmd.Storage");

			if (player.IsAdmin || permission.UserHasPermission(player.UserIDString, _config.perm_admin))
			{
				container.Add(new CuiButton
				{
					RectTransform =
						{AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-50 -25", OffsetMax = "50 0"},
					Button = {Color = "0 0 0 0", Command = "event start"},
					Text =
					{
						Text = GetMessage("UI.Random", player.UserIDString), FontSize = 20,
						Align = TextAnchor.LowerCenter, Color = "0.48 0.41 0.9 1", Font = "robotocondensed-bold.ttf"
					}
				}, Layer + ".Help", Layer + ".Help.Cmd.Random");
				container.Add(new CuiButton
				{
					RectTransform =
						{AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-170 -25", OffsetMax = "-60 0"},
					Button = {Color = "0 0 0 0", Command = "event cancel"},
					Text =
					{
						Text = GetMessage("UI.Cancel", player.UserIDString), FontSize = 16,
						Align = TextAnchor.LowerCenter, Color = "1 0.01 0.24 1", Font = "robotocondensed-bold.ttf"
					}
				}, Layer + ".Help", Layer + ".Help.Cmd.Cancel");
				container.Add(new CuiButton
				{
					RectTransform =
						{AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "60 -25", OffsetMax = "170 0"},
					Button = {Color = "0 0 0 0", Command = $"event start {list}"},
					Text =
					{
						Text = GetMessage("UI.Start", player.UserIDString), FontSize = 16,
						Align = TextAnchor.LowerCenter, Color = "0 0.6 0 1", Font = "robotocondensed-bold.ttf"
					}
				}, Layer + ".Help", Layer + ".Help.Cmd.Start");
			}

			CuiHelper.DestroyUi(player, Layer + ".Help");
			CuiHelper.AddUi(player, container);
		}

		private const string storageLayer = Layer + ".Storage";

		private void StorageUI(BasePlayer player, int page = 0)
		{
			var userData = GetUserData(player.userID);
			if (userData == null) return;

			var container = new CuiElementContainer();

			container.Add(new CuiPanel
			{
				CursorEnabled = true,
				RectTransform =
					{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-450 -255", OffsetMax = "450 255"},
				Image = {Color = "0.024 0.017 0.17 0.76"}
			}, "Overlay", storageLayer);
			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "10 10", AnchorMax = "-10 -10"},
				Text = {Text = ""},
				Button = {Color = "0 0 0 0", Close = storageLayer}
			}, storageLayer);

			container.Add(new CuiLabel
			{
				RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "40 -65", OffsetMax = "-40 0"},
				Text =
				{
					Text = GetMessage("UI.Storage", player.UserIDString), FontSize = 38,
					Align = TextAnchor.MiddleCenter
				}
			}, storageLayer, storageLayer + ".Logo");

			container.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMax = "0 1"},
				Image = {Color = "1 1 1 1"}
			}, storageLayer + ".Logo", storageLayer + ".Logo.Line");
			container.Add(new CuiElement
			{
				Name = storageLayer + ".Logo.Image",
				Parent = storageLayer + ".Logo",
				Components =
				{
					new CuiImageComponent {Png = ImageLibrary.Call<string>("GetImage", "ME_Logo_image")},
					new CuiRectTransformComponent
						{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-142 -26", OffsetMax = "-90 26"}
				}
			});

			if (userData.Count > 0)
			{
				var items = userData.Skip(page * 32).Take(32).ToList();

				var tempList = items.Take(8).ToList();
				var ySwitch = 180;
				var xSwitch = -((10 * (tempList.Count - 1) + 90 * tempList.Count) / 2);
				for (var i = 0; i < items.Count; i++)
				{
					var item = items[i];
					if (item == null) continue;

					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{xSwitch} {ySwitch - 90}",
							OffsetMax = $"{xSwitch + 90} {ySwitch}"
						},
						Image = {Color = "0.64 0.62 1 0.3"}
					}, storageLayer, storageLayer + $".Item.{i}");

					container.Add(new CuiElement
					{
						Parent = storageLayer + $".Item.{i}",
						Components =
						{
							new CuiRawImageComponent
							{
								Png = ImageLibrary.Call<string>("GetImage",
									!string.IsNullOrEmpty(item.ImageURL) ? item.ImageURL : item.item.ShortName)
							},
							new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
						}
					});

					container.Add(new CuiButton
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text = {Text = ""},
						Button =
						{
							Command = $"UI_Event openmodal {page} {userData.IndexOf(item)}", Color = "0 0 0 0"
						}
					}, storageLayer + $".Item.{i}");

					xSwitch += 100;

					if ((i + 1) % 8 == 0)
					{
						ySwitch -= 100;
						tempList = items.Skip(8).Take(8).ToList();
						xSwitch = -((10 * (tempList.Count - 1) + 90 * tempList.Count) / 2);
					}
				}

				container.Add(new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "40 10", OffsetMax = "120 35"},
					Button = {Color = "0 0 0 0", Command = page - 1 >= 0 ? $"UI_Event modalpage {page - 1}" : ""},
					Text =
					{
						Text = GetMessage("UI.Back", player.UserIDString), FontSize = 20, Align = TextAnchor.LowerLeft,
						Font = "robotocondensed-bold.ttf"
					}
				}, storageLayer);
				container.Add(new CuiButton
				{
					RectTransform = {AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-120 10", OffsetMax = "-40 35"},
					Button =
					{
						Color = "0 0 0 0",
						Command = userData.Count > (page + 1) * 32 ? $"UI_Event modalpage {page + 1}" : ""
					},
					Text =
					{
						Text = GetMessage("UI.Next", player.UserIDString), FontSize = 20, Align = TextAnchor.LowerRight,
						Font = "robotocondensed-bold.ttf"
					}
				}, storageLayer);
			}
			else
			{
				container.Add(new CuiLabel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text =
					{
						Text = GetMessage("UI.StorageEmpty", player.UserIDString), FontSize = 38,
						Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf"
					}
				}, storageLayer);
			}

			CuiHelper.DestroyUi(player, storageLayer);
			CuiHelper.AddUi(player, container);
		}

		private const string modalLayer = Layer + ".Modal.Give";

		private void ModalGiveUI(BasePlayer player, int page, int itemid)
		{
			if (player == null) return;

			var data = GetUserData(player.userID);
			if (data == null || itemid < 0 || data.Count <= itemid) return;
			var item = data[itemid];
			if (item == null) return;

			var container = new CuiElementContainer();

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Button = {Close = modalLayer, Color = "0 0 0 0.95"},
				Text = {Text = ""}
			}, "Overlay", modalLayer);

			container.Add(new CuiPanel
			{
				RectTransform =
					{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 -200", OffsetMax = "150 200"},
				Image = {Color = "0.71 0.69 1 0.3"}
			}, modalLayer, modalLayer + ".Main");

			container.Add(new CuiElement
			{
				Parent = modalLayer + ".Main",
				Components =
				{
					new CuiRawImageComponent
					{
						Png = ImageLibrary.Call<string>("GetImage",
							!string.IsNullOrEmpty(item.ImageURL) ? item.ImageURL : item.item.ShortName)
					},
					new CuiRectTransformComponent
						{AnchorMin = "0.5 0.75", AnchorMax = "0.5 0.75", OffsetMin = "-64 -64", OffsetMax = "64 64"}
				}
			});

			var itemInfo = !string.IsNullOrEmpty(item.DisplayName)
				? item.DisplayName
				: ItemManager.FindItemDefinition(item.item.ShortName)?.displayName.translated ?? "";

			container.Add(new CuiLabel
			{
				RectTransform = {AnchorMin = "0.1 0", AnchorMax = "1 0.45"},
				Text =
				{
					Text = GetMessage("MODAL.ItemInfo", player.UserIDString, itemInfo, item.Amount),
					Align = TextAnchor.UpperLeft, FontSize = 18, Color = "1 1 1 1",
					Font = "robotocondensed-bold.ttf"
				}
			}, modalLayer + ".Main");

			container.Add(new CuiButton
			{
				RectTransform =
					{AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-100 20", OffsetMax = "100 70"},
				Button = {Command = $"UI_Event giveitem {page} {itemid}", Color = "1 1 1 0.3"},
				Text =
				{
					Text = GetMessage("MODAL.Give", player.UserIDString), Align = TextAnchor.MiddleCenter,
					FontSize = 18, Color = "1 1 1 1", Font = "robotocondensed-bold.ttf"
				}
			}, modalLayer + ".Main");

			CuiHelper.DestroyUi(player, modalLayer);
			CuiHelper.AddUi(player, container);
		}

		#endregion

		#region Commands

		private void CmdChatOpenStorage(BasePlayer player, string command, string[] args)
		{
			StorageUI(player);
		}

		private void CmdEventConsole(IPlayer player, string command, string[] args)
		{
			var basePlayer = player.Object as BasePlayer;

			if (args.Length == 0)
			{
				if (basePlayer != null)
					Help_UI(basePlayer, 0);
				return;
			}

			switch (args[0].ToLower())
			{
				case "start":
				{
					if (!player.IsAdmin && !permission.UserHasPermission(player.Id, _config.perm_admin)) return;

					if (hasStarted)
					{
						player.Reply(GetMessage("EVENT.ErrorStarted", player.Id));
						return;
					}

					if (args.Length >= 2 && !_config.EnabledEvents.Contains(args[1]))
					{
						player.Reply(GetMessage("EVENT.Error", player.Id));
						return;
					}

					player.Reply(GetMessage("EVENT.Start", player.Id));

					StartEvent(args.Length >= 2 ? args[1] : _config.EnabledEvents.GetRandom(), basePlayer);

					break;
				}
				case "cancel":
				{
					if (!player.IsAdmin && !permission.UserHasPermission(player.Id, _config.perm_admin)) return;
					if (!hasStarted)
					{
						player.Reply(GetMessage("EVENT.NotStart", player.Id));
						return;
					}

					player.Reply(GetMessage("EVENT.Cancel", player.Id));
					DestroyEvent();
					break;
				}
				case "join":
				{
					if (!hasStarted)
					{
						player.Reply(GetMessage("EVENT.NotStart", player.Id));
						return;
					}

					if (nowEvent != "FoundationDrop")
					{
						player.Reply(GetMessage("EVENT.NotFD", player.Id));
						return;
					}

					if (basePlayer != null)
						cEvent?.JoinEvent(basePlayer);
					break;
				}
			}
		}

		[ConsoleCommand("UI_Event")]
		private void CmdConsole(ConsoleSystem.Arg args)
		{
			var player = args.Player();
			if (player == null) return;

			if (!args.HasArgs())
			{
				player.SendConsoleCommand("chat.say /event");
				return;
			}

			switch (args.Args[0].ToLower())
			{
				case "page":
				{
					int page;
					if (!args.HasArgs(2) || !int.TryParse(args.Args[1], out page)) return;
					Help_UI(player, page);
				}
					break;
				case "storage":
				{
					CuiHelper.DestroyUi(player, Layer + ".Help");

					StorageUI(player);
					break;
				}
				case "openmodal":
				{
					int page, itemid;
					if (!args.HasArgs(3) || !int.TryParse(args.Args[1], out page) ||
					    !int.TryParse(args.Args[2], out itemid)) return;
					ModalGiveUI(player, page, itemid);
					break;
				}
				case "giveitem":
				{
					int page, itemid;
					if (!args.HasArgs(3) || !int.TryParse(args.Args[1], out page) ||
					    !int.TryParse(args.Args[2], out itemid)) return;
					var dataItems = GetUserData(player.userID);
					if (dataItems == null) return;

					if (itemid >= 0 && dataItems.Count > itemid)
					{
						var item = dataItems[itemid];

						if (item == null)
						{
							PrintError("Error getting item from data");
							return;
						}

						GiveItem(player, item);
					}

					CuiHelper.DestroyUi(player, modalLayer);

					StorageUI(player);
					break;
				}
				case "modalpage":
				{
					int page;
					if (!args.HasArgs(2) || !int.TryParse(args.Args[1], out page)) return;
					StorageUI(player, page);
					break;
				}
			}
		}

		#endregion

		#region Localization

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				{"EVENT.END", "Event {0} completed.\nWon {1}."},
				{"EVENT.END.MULTI", "Event {0} is over.\nWon or {1}."},
				{"EVENT.YOUWINNER", "You are the winner!\nCongratulations!"},
				{"EVENT.ErrorStarted", "Event is already running"},
				{"EVENT.Error", "Event is forbidden to launch!!! Check if the entered query is correct"},
				{"EVENT.Start", "You have launched the event"},
				{"EVENT.NotStart", "No event running"},
				{"EVENT.Cancel", "You canceled the event"},
				{"EVENT.NotFD", "You are wrong with the event"},
				{"EVENT.Timer", "LEFT {0}"},
				{"WINNER.NOTFOUND", "WINNER NOT FOUND"},
				{"KingMountain", "KING OF THE HILL"},
				{"CollectionResources", "COLLECTION OF RESOURCES"},
				{"HuntAnimal", "ANIMAL HUNT"},
				{"HelicopterPet", "PET Helicopter"},
				{"LookingLoot", "SEARCHING FOR LOOT"},
				{"SpecialCargo", "SPECIAL CARGO"},
				{"FoundationDrop", "FALLING FOUNDATIONS"},
				{
					"GET.RUNNER",
					"Player {0} picked up the special cargo.\nkill {0} to pick up the loot! The player is marked on your map."
				},
				{"RUNNER.BYNPC", "The running player was killed by NPC.\nNew runner is {0}"},
				{"RUNNER.PLAYER", "The running player {0} was killed\n New runner is {1}"},
				{"RUNNER.FORRUNNER", "You have received the special cargo! Carry it to the goal {0} ({1})"},
				{
					"FOUNDROP.JOIN",
					"A field of 10x10 will spawn and will gradually collapse. The player who stays on top for the longest time wins.\n\nPlease write\n/event join\n"
				},
				{"FOUNDROP.WaitTime", "<b>BEFORE BEGINNING EVENT: {0}</b>"},
				{"FOUNDROP.BlockAndPlayers", "<b>Blocks on the field: {0}\nPlayers on the field: {1} </b>"},
				{
					"FOUNDROP.Started",
					"<size=16>The event has already begun, <color=#538fef>you did not have time</color>!</size>"
				},
				{"FOUNDROP.Connected", "<size=16>You are already a participant of the event!</size>"},
				{"UI.Random", "<b>RANDOM</b>"},
				{"UI.Cancel", "<b>CANCEL</b>"},
				{"UI.Start", "<b>START</b>"},
				{"UI.Back", "<b>BACK</b>"},
				{"UI.Next", "<b>NEXT</b>"},
				{"UI.Name", "<b>EVENTS</b>"},
				{"UI.Storage", "<b>STORAGE</b>"},
				{"UI.KingMountain", "<b>KING OF\nTHE HILL</b>"},
				{"UI.CollectionResources", "<b>COLLECTION\nOF RESOURCES</b>"},
				{"UI.HuntAnimal", "<b>ANIMAL\nHUNTING</b>"},
				{"UI.HelicopterPet", "<b>PET Helicopter</b>"},
				{"UI.LookingLoot", "<b>SEARCHING\nFOR LOOT</b>"},
				{"UI.SpecialCargo", "<b>SPECIAL CARGO</b>"},
				{"UI.FoundationDrop", "<b>FALLING\nFOUNDATIONS</b>"},
				{
					"UI.Description.KingMountain",
					"<b>Show us who is the King of the Hill on the server. Climb to the highest point on the map (mountains, monuments, buildings) and stay on it until the end. You can build, but you´re not allowed to be within range of your Tool Cupboard.\nTime: 5 min\nAward: random item </b>"
				},
				{
					"UI.Description.CollectionResources",
					"<b>Your task is to collect the most resources in given time.\nPlayers points are the total number of resources collected.\n\nTime: 5 min\nAward: random item </b>"
				},
				{
					"UI.Description.HuntAnimal",
					"<b>In this event you have to kill the most animals. Animals grant different amounts of points.\n1 point for a chicken, for a wolf/boar/deer/horse 4 points, for a bear 10 points.\n\nTime: 5 min\nAward: random item </b>"
				},
				{
					"UI.Description.HelicopterPet",
					"<b>A patrol helicopter appears in the center of the map. You must attract his attention and forcing it to shoot at you.\nEvery second the helicopter is fighting with you, you get one point. (+1 optional, if helicopter is sending missiles at you)\nThe helicopter can simultaneously fight two players. In this case, both players will get points.\n\nTime: 5 min\nAward: random item </b>"
				},
				{
					"UI.Description.LookingLoot",
					"<b>In this event, you need to loot the most.\neach crate/barrel can only be looted by one player.\nYou do not need to pick up loot, just open boxes or break barrels.\n\nTime: 5 min\nAward: random item </b>"
				},
				{
					"UI.Description.SpecialCargo",
					"<b>A airdrop with a special cargo will be dropped on the map. The cargo needs to be picked up and brought to a target destination.\nSpecial cargo is displayed on the map as a golden airdrop.\nThe person who picked up the cargo is displayed for everyone on the map.\nThe target destination can be seen only by the player who picked up the cargo.\n\nTime: 30 min\nAward: random item </b>"
				},
				{
					"UI.Description.FoundationDrop",
					"<b>An fun event in a special arena.\nAll players appear on a 10x10 field of foundations.\nEach 5 seconds, one of the foundations will fall until there is only one foundation left.\nIf there are several players left on the last field, they get a sword and radiation begins.\nThe player who stays alive for the longest time wins.\n\nTime: ~ 10 min. Award: random item</b>"
				},
				{
					"SpecialCargo.New",
					"Airdrop with the <color=red>special cargo</color> was dropped over {0}. Pick it up and try to carry it to the target destination.\nThe special cargo is marked on the map as a golden airdrop."
				},
				{
					"SpecialCargo.CreateRunner",
					"You picked up a special cargo. Carry it to {0} ({1}).\nBe careful, other players can see you on the map and you will probably be hunted."
				},
				{"SpecialCargo.Purpose", "<b>YOUR GOAL: {0} ({1})</b>"},
				{"MODAL.ItemInfo", "Name: {0}\n\nAmount: {1}"},
				{"MODAL.Give", "<b>TAKE</b>"},
				{"UI.StorageEmpty", "<b>STORAGE IS EMPTY</b>"},
				{"CommandBlocked", "Command is blocked"},
				{"GiveReward", "As a reward you get {0}"},
				{"ErrorItemCreating", "Error creating item. Contact your admin!"},
				{"CantTP", "Teleportation for the runner is prohibited!"},
				{"CantBuild", "It is forbidden to build near falling platforms!"}
			}, this);
			lang.RegisterMessages(new Dictionary<string, string>
			{
				{"EVENT.END", "Ивент {0} закончен.\nПобедил {1}."},
				{"EVENT.END.MULTI", "Ивент {0} закончен.\nПобедили {1}."},
				{"EVENT.YOUWINNER", "Вы стали победителем ивента!\nПоздравляем!"},
				{"EVENT.ErrorStarted", "Ивент уже запущен"},
				{"EVENT.Error", "Ивент запрещён к запуску!!! Проверьте правильность введённого запроса"},
				{"EVENT.Start", "Вы запустили ивент"},
				{"EVENT.NotStart", "Ивент не запущен"},
				{"EVENT.Cancel", "Вы отменили ивент"},
				{"EVENT.NotFD", "Вы ошиблись ивентом"},
				{"EVENT.Timer", "ОСТАЛОСЬ {0}"},
				{"WINNER.NOTFOUND", "ПОБЕДИТЕЛЬ НЕ НАЙДЕН"},
				{"KingMountain", "КОРОЛЬ ГОРЫ"},
				{"CollectionResources", "СБОР РЕСУРСОВ"},
				{"HuntAnimal", "ОХОТА НА ЖИВОТНЫХ"},
				{"HelicopterPet", "ЛЮБИМЧИК ВЕРТОЛЁТА"},
				{"LookingLoot", "В ПОИСКАХ ЛУТА"},
				{"SpecialCargo", "ОСОБЫЙ ГРУЗ"},
				{"FoundationDrop", "ПАДАЮЩИЕ ПЛАТФОРМЫ"},
				{
					"GET.RUNNER",
					"Игрок {0} подобрал ящик с заветным лутом.\n Он бежит на РТ, спешите чтобы забрать у него лут! Он отмечен у тебя на карте."
				},
				{"RUNNER.BYNPC", "Бегущий игрок был убит NPC\n Новым бегуном назначен {0}"},
				{"RUNNER.PLAYER", "Бегущий игрок {0} был убит\n Новым бегуном назначен {1}"},
				{"RUNNER.FORRUNNER", "Вы получили особый груз! Донесите его до цели {0} ({1})"},
				{
					"FOUNDROP.JOIN",
					"Ивент на специальной арене. Поле 10x10, которое будет постепенно обваливаться. Побеждает тот, кто последний останется на поле.\n\nДля участия напишите\n/event join"
				},
				{"FOUNDROP.WaitTime", "<b>ДО НАЧАЛА ИВЕНТА: {0}</b>"},
				{"FOUNDROP.BlockAndPlayers", "<b>Блоков на поле: {0}\nИгроков на поле: {1}</b>"},
				{"FOUNDROP.Started", "<size=16>Мероприятие уже началось, вы <color=#538fef>не успели</color>!</size>"},
				{"FOUNDROP.Connected", "<size=16>Вы уже участник мероприятия!</size>"},
				{"UI.Random", "<b>РАНДОМ</b>"},
				{"UI.Cancel", "<b>ОТМЕНИТЬ</b>"},
				{"UI.Start", "<b>ЗАПУСТИТЬ</b>"},
				{"UI.Back", "<b>НАЗАД</b>"},
				{"UI.Next", "<b>ДАЛЕЕ</b>"},
				{"UI.Name", "<b>ИВЕНТЫ</b>"},
				{"UI.Storage", "<b>СКЛАД</b>"},
				{"UI.KingMountain", "<b>КОРОЛЬ\nГОРЫ</b>"},
				{"UI.CollectionResources", "<b>СБОР\nРЕСУРСОВ</b>"},
				{"UI.HuntAnimal", "<b>ОХОТА\nНА ЖИВОТНЫХ</b>"},
				{"UI.HelicopterPet", "<b>ЛЮБИМЧИК\nВЕРТОЛЁТА</b>"},
				{"UI.LookingLoot", "<b>В ПОИСКАХ ЛУТА</b>"},
				{"UI.SpecialCargo", "<b>ОСОБЫЙ ГРУЗ</b>"},
				{"UI.FoundationDrop", "<b>ПАДАЮЩИЕ\nПЛАТФОРМЫ</b>"},
				{
					"UI.Description.KingMountain",
					"<b>Здесь Вы должны показать, кто настоящий король на сервере. Заберитесь на самую высокую точку на карте (на любую гору) и оставайтесь на ней до самого конца. Вы можете строиться там, но не можете находиться в зоне действия своего шкафа.\nПобеждает игрок, который к концу ивента будет выше, чем остальные игроки.\n\nВремя: 5 минут\nНаграда: рандомный предмет</b>"
				},
				{
					"UI.Description.CollectionResources",
					"<b>Ваша задача собрать больше ресурсов, чем другие игроки.\nЗасчитывается стучание по камням с рудой, подбор руды с земли, срывание кустов ткани, разделывание трупов животных и людей и подбор грибов.\nСумма очков игрока равно общему количеству добытых реусрсов.\n\nВремя: 5 мин\nНаграда: рандомный предмет</b>"
				},
				{
					"UI.Description.HuntAnimal",
					"<b>В этом ивенте Вы должны убивать всех животных. За разных животных даётся разное кол-во очков.\nЗа курицу даётся 1 очко, за волка/кабана/оленя/лошадь 4 очка, за медведя 10 очков.\n\nВремя: 5 минут\nНаграда: рандомный предмет.</b>"
				},
				{
					"UI.Description.HelicopterPet",
					"<b>В центре карты появляется патрульный вертолёт. Вы должны привлекать его внимание, заставляя стрелять в Вас.\nКаждую секунду, пока вертолёт ведётся на Вас, Вы получаете одно очко на счёт. (+1 допольное, если вертолёт хочет пустить в Вас ракеты)\nВертолёт может одновременно реагировать на двух игроков, тогда каждый будет получать по одному очку.\n\nВремя: 5 минут\nНаграда: рандомный предмет</b>"
				},
				{
					"UI.Description.LookingLoot",
					"<b>В этом ивенте Вам нужно лутать ящики и бочки.\n2 игрока не могут залутать один и тот же ящик.\nБочки достаточно разбить выстрелом из далека.\nЗабирать лут не обязательно, достаточно открыть ящик или разбить бочку.\n\nВремя: 5 минут\n Награда: рандомный предмет</b>"
				},
				{
					"UI.Description.SpecialCargo",
					"<b>Над одним из рт будет сброшен аирдроп с ценным грузом. Его нужно подобрать и донести на другой рт.\nЭтот груз отображается на карте как золотой аирдроп.\nЧеловек, который его несёт, отображается у всех на карте.\nТочку назначения видит только игрок, который несёт груз.\n\nВремя: 30 минут\nНаграда: рандомный предмет</b>"
				},
				{
					"UI.Description.FoundationDrop",
					"<b>Ивент на специальной арене.\nВсе игроки появляются на поле 10x10 из фундаментов.\nКаждые 5 секунд один из фундаментов будет проваливаться и так до того момента, пока не останется только один фундамент.\nЕсли к концу на поле осталось несколько игроков, они получают меч и на них начинает действовать радиация.\nПобеждает тот, кто последний остался на поле.\n\nВремя: ~10 минут.\nНаграда: рандомный предмет</b>"
				},
				{
					"SpecialCargo.New",
					"Над {0} был сброшен аирдроп с ценным грузом. Подбери его и донеси до цели.\nГруз отмечен на карте как золотой аирдроп."
				},
				{
					"SpecialCargo.CreateRunner",
					"Вы подобрали особый груз. Донесите его до {0} ({1}).\nНо будьте осторожны, на вас будут охотится игроки, желающие забрать ценный лут себе."
				},
				{"SpecialCargo.Purpose", "<b>ВАША ЦЕЛЬ: {0} ({1})</b>"},
				{"MODAL.ItemInfo", "Название: {0}\n\nКоличество: {1}"},
				{"MODAL.Give", "<b>ЗАБРАТЬ</b>"},
				{"UI.StorageEmpty", "<b>СКЛАД ПУСТ</b>"},
				{"CommandBlocked", "Команда заблокирована"},
				{"GiveReward", "В качестве награды вы получаете {0}"},
				{"ErrorItemCreating", "Ошибка создания предмета. Обратитесь к администратору!"},
				{"CantTP", "Телепортация для бегущего запрещена!"},
				{"CantBuild", "Запрещено строится вблизи падающих платформ!"}
			}, this, "ru");
		}

		private string GetMessage(string messageKey, string playerID, params object[] args)
		{
			return string.Format(lang.GetMessage(messageKey, this, playerID), args);
		}

		#endregion

		#region Script

		private static string GetGridString(Vector3 pos)
		{
			var alpha = new[]
			{
				'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
				'U', 'V', 'W', 'X', 'Y', 'Z'
			};

			pos.z = -pos.z;
			pos += new Vector3(TerrainMeta.Size.x, 0, TerrainMeta.Size.z) * .5f;

			var cubeSize = 146.14f;

			var xCube = (int) (pos.x / cubeSize);
			var zCube = (int) (pos.z / cubeSize);

			var firstLetterIndex = xCube / alpha.Length - 1;
			var firstLetter = "";
			if (firstLetterIndex >= 0)
				firstLetter = $"{alpha[firstLetterIndex]}";

			var xStr = $"{firstLetter}{alpha[xCube % 26]}";
			var zStr = $"{zCube}";

			return $"{xStr}{zStr}";
		}

		private static void ClearTeleport(BasePlayer player, Vector3 position)
		{
			if (player.IsDead() && player.IsConnected)
			{
				player.RespawnAt(position, Quaternion.identity);
				return;
			}

			var mount = player.GetMounted();
			if (mount != null) mount.DismountPlayer(player);
			if (player.net?.connection != null) player.ClientRPCPlayer(null, player, "StartLoading");
			player.StartSleeping();
			player.MovePosition(position);
			if (player.net?.connection != null) player.ClientRPCPlayer(null, player, "ForcePositionTo", position);
			if (player.net?.connection != null) player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
			player.UpdateNetworkGroup();
			player.SendNetworkUpdateImmediate();
			if (player.net?.connection == null) return;
			try
			{
				player.ClearEntityQueue();
			}
			catch
			{
				// ignored
			}

			player.SendFullSnapshot();
		}

		private void InitializeFoundationEvent(int startDelay)
		{
			cEvent?.StartEvent(startDelay);

			foreach (var player in BasePlayer.activePlayerList)
				TopUI(player, "all",
					GetMessage(nowEvent, player.UserIDString),
					GetMessage("FOUNDROP.JOIN", player.UserIDString),
					_config.FoundationDrop.ui.oMin, _config.FoundationDrop.ui.oMax,
					_config.FoundationDrop.ui.color);

			EventRepeating = () =>
			{
				foreach (var player in BasePlayer.activePlayerList)
					TopUI(player, "FoundationDrop_Timer");

				time--;
			};

			InvokeHandler.Instance.InvokeRepeating(EventRepeating, 0, 1);
		}

		private class FoundationDrop
		{
			private bool Started;
			public bool Finished;
			private bool Given;

			private Action mainAction;

			public Action destroyAction;

			public readonly Dictionary<ulong, PlayerInfo> PlayerConnected = new Dictionary<ulong, PlayerInfo>();
			public readonly List<BaseEntity> BlockList = new List<BaseEntity>();

			public class PlayerInfo
			{
				public Vector3 position;
				public List<SaveItem> Items;
			}

			private readonly uint buildingID = BuildingManager.server.NewBuildingID();

			public void JoinEvent(BasePlayer player)
			{
				if (Started)
				{
					_instance.SendReply(player, _instance.GetMessage("FOUNDROP.Started", player.UserIDString));
					return;
				}

				if (PlayerConnected.ContainsKey(player.userID))
				{
					_instance.SendReply(player, _instance.GetMessage("FOUNDROP.Connected", player.UserIDString));
					return;
				}

				PlayerConnected.Add(player.userID, new PlayerInfo
				{
					position = player.transform.position,
					Items = GetPlayerItems(player)
				});

				ClearTeleport(player, EventPosition + new Vector3(0, 5, 0));
				player.inventory.Strip();
				player.health = 100;
				player.metabolism.hydration.value = 250;
				player.metabolism.calories.value = 500;
			}

			public void LeftEvent(BasePlayer player)
			{
				CuiHelper.DestroyUi(player, Layer);
				player.inventory.Strip();

				var pInfo = PlayerConnected[player.userID];

				player.metabolism.Reset();

				ClearTeleport(player, pInfo.position + new Vector3(0, 1, 0));

				player.children.RemoveAll(x => x is DroppedItem);

				_instance.GiveItems(player, pInfo.Items);

				InvokeHandler.Instance.Invoke(() => PlayerConnected.Remove(player.userID), 0.21f);
			}

			private void HandlePlayers()
			{
				if (Finished || _instance == null) return;

				if (PlayerConnected.Count <= 1)
				{
					_instance.winner = BasePlayer.FindByID(PlayerConnected.Keys.FirstOrDefault());
					if (_instance.winner != null) _instance.DropItem(_instance.winner, _instance.nowEvent);
					_instance.DestroyEvent();
					return;
				}

				if (PlayerConnected.Count > 1 && BlockList.Count == 1)
				{
					var lastblock = BlockList.Last();
					if (lastblock == null) return;

					lastblock.gameObject.GetComponent<Downgrader>()?.Kill();
					if (Given == false)
					{
						foreach (var userID in PlayerConnected.Keys)
						{
							var target = BasePlayer.FindByID(userID);
							if (target != null)
								target.GiveItem(ItemManager.CreateByName("salvaged.sword"),
									BaseEntity.GiveItemReason.PickedUp);
						}

						Given = true;
					}

					var instanceID = lastblock.GetInstanceID();
					if (!_instance.RadiationZones.ContainsKey(instanceID))
						_instance.InitializeZone(lastblock.transform.position,
							_config.FoundationDrop.IntensityRadiation,
							instanceID);
				}

				foreach (var userID in PlayerConnected.Keys.ToList())
				{
					var target = BasePlayer.FindByID(userID);
					if (target == null)
					{
						PlayerConnected.Remove(userID);
					}
					else
					{
						if (Vector3.Distance(EventPosition, target.transform.position) >
						    _config.FoundationDrop.ArenaSize * 5)
							LeftEvent(target);

						_instance.TopUI(target, "FoundationDrop.Upd");
					}
				}

				mainAction = HandlePlayers;
				InvokeHandler.Instance.Invoke(mainAction, 1);
			}

			public void StartEvent(int startDelay)
			{
				mainAction = () =>
				{
					Started = true;

					InvokeHandler.Instance.CancelInvoke(_instance.EventRepeating);

					foreach (var player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, Layer);

					foreach (var check in PlayerConnected.ToList())
					{
						var player = BasePlayer.FindByID(check.Key);
						if (player != null)
							_instance.TopUI(player, "all",
								_instance.GetMessage(_instance.nowEvent, player.UserIDString),
								oMin: _config.FoundationDrop.infoUI.oMin, oMax: _config.FoundationDrop.infoUI.oMax,
								color: _config.FoundationDrop.infoUI.color);
						else
							PlayerConnected.Remove(check.Key);
					}

					if (PlayerConnected.Count <= 1)
					{
						_instance?.DestroyEvent();
						return;
					}

					_instance?.DropFoundation();
					HandlePlayers();
				};

				InvokeHandler.Instance.Invoke(mainAction, startDelay);
			}

			public void InitializeEvent(int startDelay)
			{
				Started = false;

				ServerMgr.Instance.StartCoroutine(InitializeFoundation());

				InvokeHandler.Instance.Invoke(() => _instance?.InitializeFoundationEvent(startDelay), 1f);
			}

			public void FinishEvent()
			{
				Finished = true;
				Given = false;

				InvokeHandler.Instance.CancelInvoke(mainAction);
				InvokeHandler.Instance.CancelInvoke(destroyAction);

				_instance?.Zones.ToList().ForEach(comp =>
				{
					if (comp != null)
						comp.Kill();
				});

				_instance?.RadiationZones.Clear();

				foreach (var player in BasePlayer.activePlayerList)
				{
					CuiHelper.DestroyUi(player, Layer);
					CuiHelper.DestroyUi(player, Layer + ".FoundationDrop.Play");

					if (PlayerConnected.ContainsKey(player.userID)) LeftEvent(player);
				}

				foreach (var entity in BaseNetworkable.serverEntities.OfType<BuildingBlock>())
				{
					if (entity == null || entity.IsDestroyed || entity.OwnerID != 98596) continue;

					entity.Kill();
				}
			}

			private IEnumerator InitializeFoundation()
			{
				for (var i = -_config.FoundationDrop.ArenaSize / 2; i < _config.FoundationDrop.ArenaSize / 2; i++)
				{
					for (var t = -_config.FoundationDrop.ArenaSize / 2; t < _config.FoundationDrop.ArenaSize / 2; t++)
					{
						var newFoundation =
							GameManager.server.CreateEntity("assets/prefabs/building core/foundation/foundation.prefab",
								EventPosition + new Vector3(i * 3, 4, t * 3)) as BuildingBlock;
						if (newFoundation == null) continue;

						newFoundation.enableSaving = false;
						newFoundation.Spawn();
						newFoundation.OwnerID = 98596;

						newFoundation.SetGrade(BuildingGrade.Enum.TopTier);
						newFoundation.UpdateSkin();
						newFoundation.SetHealthToMax();

						newFoundation.AttachToBuilding(buildingID);

						newFoundation.SendNetworkUpdate();

						cEvent.BlockList.Add(newFoundation);

						yield return null;
					}

					yield return null;
				}
			}
		}

		#region Radiation Control{RPID}

		private void OnServerRadiation()
		{
			Array.ForEach(UnityEngine.Object.FindObjectsOfType<TriggerRadiation>(), UnityEngine.Object.Destroy);
		}

		private void InitializeZone(Vector3 Location, float intensity, int ZoneID)
		{
			var radius = 10f;
			if (!ConVar.Server.radiation) ConVar.Server.radiation = true;

			if (_config.FoundationDrop.DisableDefaultRadiation)
				OnServerRadiation();

			var newZone = new GameObject().AddComponent<RadZone>();
			newZone.Activate(Location, radius, intensity, ZoneID);

			RadiationZones.Add(ZoneID, newZone);
		}

		public class RadZone : MonoBehaviour
		{
			private int ID;
			private Vector3 Position;
			private float ZoneRadius;
			private float RadiationAmount;

			private void Awake()
			{
				gameObject.layer = (int) Rust.Layer.Reserved1;
				gameObject.name = "FoundationDropZone";
				var rigidbody = gameObject.AddComponent<Rigidbody>();
				rigidbody.useGravity = false;
				rigidbody.isKinematic = true;

				_instance?.Zones.Add(this);
			}

			public void Activate(Vector3 pos, float radius, float amount, int ZoneID)
			{
				ID = ZoneID;
				Position = pos;
				ZoneRadius = radius;
				RadiationAmount = amount;
				gameObject.name = $"Foundation{ID}";
				transform.position = Position;
				transform.rotation = new Quaternion();

				UpdateCollider();

				gameObject.SetActive(true);
				enabled = true;

				var Rads = gameObject.GetComponent<TriggerRadiation>() ?? gameObject.AddComponent<TriggerRadiation>();
				Rads.RadiationAmountOverride = RadiationAmount;
				Rads.interestLayers = playerLayer;
				Rads.enabled = true;
			}

			private void OnDestroy()
			{
				_instance?.Zones.Remove(this);

				Destroy(gameObject);
				Destroy(this);
			}

			private void UpdateCollider()
			{
				var sphereCollider = gameObject.GetComponent<SphereCollider>() ??
				                     gameObject.AddComponent<SphereCollider>();
				sphereCollider.isTrigger = true;
				sphereCollider.radius = ZoneRadius;
			}

			public void Kill()
			{
				Destroy(this);
			}
		}

		#endregion

		#region Utils{RPID}

		private void DropFoundation()
		{
			if (cEvent.Finished || cEvent.BlockList.Count == 1) return;

			var cBlock = cEvent.BlockList.GetRandom();
			if (cBlock == null)
			{
				cEvent.BlockList.Remove(cBlock);
				DropFoundation();
				return;
			}

			cBlock.gameObject.AddComponent<Downgrader>();

			cEvent.destroyAction = DropFoundation;
			InvokeHandler.Instance.Invoke(cEvent.destroyAction, _config.FoundationDrop.DelayDestroy);
		}

		#endregion

		private class Downgrader : FacepunchBehaviour
		{
			private BuildingBlock block;

			private void Awake()
			{
				block = GetComponent<BuildingBlock>();

				if (block == null)
				{
					Kill();
					return;
				}

				InvokeRepeating(Downgrade, 0, _config.FoundationDrop.DelayDestroy / 5);

				_instance?.Downgraders.Add(this);
			}

			private void Downgrade()
			{
				if (block.grade == BuildingGrade.Enum.Twigs)
				{
					block.Kill();
					Kill();
					return;
				}

				block.SetGrade(block.grade - 1);
				block.transform.position -= new Vector3(0, 0.5f, 0);
				block.SendNetworkUpdate();
				block.UpdateSkin();
			}

			private void RemoveFromList(BaseEntity entity)
			{
				if (cEvent != null && cEvent.BlockList.Contains(entity))
					cEvent.BlockList.Remove(entity);
			}

			private void OnDestroy()
			{
				if (block != null)
					RemoveFromList(block);

				CancelInvoke();

				_instance?.Downgraders.Remove(this);

				Destroy(this);
			}

			public void Kill()
			{
				Destroy(this);
			}
		}

		private class HeliPet : FacepunchBehaviour
		{
			private BaseHelicopter helicopter;

			private void Awake()
			{
				helicopter = GetComponent<BaseHelicopter>();
				InvokeRepeating(Cheking, 1, 1);
			}

			private void Cheking()
			{
				if (helicopter == null || helicopter.IsDestroyed)
				{
					Kill();
					return;
				}

				var phi = helicopter.GetComponent<PatrolHelicopterAI>();

				foreach (var p in phi._targetList.Select(p => p.ply))
					switch (phi._currentState)
					{
						case PatrolHelicopterAI.aiState.ORBIT:
							_instance?.AddToDictionary(p, 1);
							break;
						case PatrolHelicopterAI.aiState.STRAFE:
							_instance?.AddToDictionary(p, 2);
							break;
					}
			}

			private void OnDestroy()
			{
				CancelInvoke();
				Destroy(this);
			}

			public void Kill()
			{
				Destroy(this);
			}
		}

		private class SCmarker : FacepunchBehaviour
		{
			private MonumentInfo monument;
			private MapMarker mapMarker;

			private void Awake()
			{
				monument = GetComponent<MonumentInfo>();

				mapMarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/cratemarker.prefab",
					monument.transform.position) as MapMarker;
				if (mapMarker == null) return;

				mapMarker.name = _config.SpecialCargo.MarkerName;
				mapMarker.Spawn();
				mapMarker.enabled = true;

				_instance?.SCmarkers.Add(this);
			}

			private void OnDestroy()
			{
				CancelInvoke();

				if (mapMarker != null && !mapMarker.IsDestroyed) mapMarker.Kill();

				_instance?.SCmarkers.Remove(this);

				Destroy(this);
			}

			public void Kill()
			{
				Destroy(this);
			}
		}

		private class SCPlayerMarker : FacepunchBehaviour
		{
			private BasePlayer player;
			private MapMarker mapMarker;

			private void Awake()
			{
				player = GetComponent<BasePlayer>();
			}

			public void SpawnMarker()
			{
				mapMarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/cratemarker.prefab",
					player.transform.position) as MapMarker;
				if (mapMarker == null) return;

				mapMarker.name = _config.SpecialCargo.MarkerName;
				mapMarker.Spawn();
				mapMarker.enabled = true;
				InvokeRepeating(UpdatePostion, 0.2f, 0.2f);
			}

			public void UpdatePostion()
			{
				if (player == null)
				{
					Destroy(this);
					return;
				}

				mapMarker.transform.position = player.transform.position;
				mapMarker.SendNetworkUpdate();
			}

			private void OnDestroy()
			{
				CancelInvoke();
				if (mapMarker != null) mapMarker.Kill();
				Destroy(this);
			}

			public void Kill()
			{
				Destroy(this);
			}
		}

		#endregion

		#region Utils

		private bool IsDuelPlayer(BasePlayer player)
		{
			var dueler = Duel?.Call("IsPlayerOnActiveDuel", player);

			return dueler as bool? ?? false;
		}


		private void GiveItems(BasePlayer player, List<SaveItem> Items)
		{
			if (player == null || Items == null) return;

			Items.ForEach(kitem =>
			{
				GiveItem(player,
					BuildItem(kitem.ShortName, kitem.Amount, kitem.SkinID, kitem.Condition, kitem.Blueprint,
						kitem.Weapon, kitem.Content),
					kitem.Container == "belt" ? player.inventory.containerBelt :
					kitem.Container == "wear" ? player.inventory.containerWear : player.inventory.containerMain);
			});
		}

		private static void GiveItem(BasePlayer player, Item item, ItemContainer cont = null)
		{
			if (player == null || item == null) return;

			var inv = player.inventory;
			if (inv == null) return;

			if (cont != null)
			{
				if (!item.MoveToContainer(cont)) item.Drop(player.GetCenter(), player.GetDropVelocity());
			}
			else
			{
				player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
			}
		}

		private static Item BuildItem(string ShortName, int Amount, ulong SkinID, float Condition, int blueprintTarget,
			Weapon weapon, List<ItemContent> Content)
		{
			var item = ItemManager.CreateByName(ShortName, Amount > 1 ? Amount : 1, SkinID);
			item.condition = Condition;

			if (blueprintTarget != 0)
				item.blueprintTarget = blueprintTarget;

			if (weapon != null)
			{
				var baseProjectile = item.GetHeldEntity() as BaseProjectile;
				if (baseProjectile != null)
				{
					baseProjectile.primaryMagazine.contents = weapon.ammoAmount;
					baseProjectile.primaryMagazine.ammoType = ItemManager.FindItemDefinition(weapon.ammoType);
				}
			}

			if (Content != null)
				foreach (var cont in Content)
				{
					var new_cont = ItemManager.CreateByName(cont.ShortName, cont.Amount);
					new_cont.condition = cont.Condition;
					new_cont.MoveToContainer(item.contents);
				}

			return item;
		}

		private static List<SaveItem> GetPlayerItems(BasePlayer player)
		{
			var result = new List<SaveItem>();
			foreach (var item in player.inventory.containerWear.itemList)
			{
				if (item == null) continue;
				var iteminfo = ItemToKit(item, "wear");
				result.Add(iteminfo);
			}

			foreach (var item in player.inventory.containerMain.itemList)
			{
				if (item == null) continue;
				var iteminfo = ItemToKit(item, "main");
				result.Add(iteminfo);
			}

			foreach (var item in player.inventory.containerBelt.itemList)
			{
				if (item == null) continue;
				var iteminfo = ItemToKit(item, "belt");
				result.Add(iteminfo);
			}

			return result;
		}

		private class SaveItem
		{
			public string ShortName { get; set; }
			public int Amount { get; set; }
			public int Blueprint { get; set; }
			public ulong SkinID { get; set; }
			public string Container { get; set; }
			public float Condition { get; set; }
			public int Change { get; set; }
			public Weapon Weapon { get; set; }
			public List<ItemContent> Content { get; set; }
		}

		private class Weapon
		{
			public string ammoType { get; set; }
			public int ammoAmount { get; set; }
		}

		private class ItemContent
		{
			public string ShortName { get; set; }
			public float Condition { get; set; }
			public int Amount { get; set; }
		}

		private static SaveItem ItemToKit(Item item, string container)
		{
			var kitem = new SaveItem
			{
				Amount = item.amount,
				Container = container,
				SkinID = item.skin,
				Blueprint = item.blueprintTarget,
				ShortName = item.info.shortname,
				Condition = item.condition,
				Change = 100,
				Weapon = null,
				Content = null
			};
			if (item.info.category == ItemCategory.Weapon)
			{
				var weapon = item.GetHeldEntity() as BaseProjectile;
				if (weapon != null)
					kitem.Weapon = new Weapon
					{
						ammoType = weapon.primaryMagazine.ammoType.shortname,
						ammoAmount = weapon.primaryMagazine.contents
					};
			}

			if (item.contents != null)
			{
				kitem.Content = new List<ItemContent>();
				foreach (var cont in item.contents.itemList)
					kitem.Content.Add(new ItemContent
					{
						Amount = cont.amount,
						Condition = cont.condition,
						ShortName = cont.info.shortname
					});
			}

			return kitem;
		}

		private static bool CanMonument(MonumentInfo monument)
		{
			return monument != null && _config.SpecialCargo.blockMonuments.All(blockMonument =>
				!monument.name.ToLower().Contains(blockMonument.ToLower()));
		}

		private void AddToDictionary(BasePlayer player, float amount)
		{
			if (!PlayersTop.ContainsKey(player))
				PlayersTop.Add(player, amount);
			else
				PlayersTop[player] += amount;
		}

		private void AddItem(BasePlayer player, DataItem item)
		{
			if (player == null || item == null) return;

			var userData = GetUserData(player.userID);
			if (userData == null) return;

			userData.Add(item);

			SaveData();

			if (_config.EnableRewardAlert)
				SendReply(player,
					GetMessage("GiveReward", player.UserIDString,
						!string.IsNullOrEmpty(item.DisplayName)
							? item.DisplayName
							: ItemManager.FindItemDefinition(item.item.ShortName).displayName.translated));
		}

		private void GiveItem(BasePlayer player, DataItem item)
		{
			var userData = GetUserData(player.userID);
			if (userData == null) return;

			if (item.Type == ItemTypes.Command)
			{
				item.GiveCmd(player);
			}
			else
			{
				var toItem = item.ToItem();
				if (toItem != null)
					player.GiveItem(toItem, BaseEntity.GiveItemReason.PickedUp);
				else
					SendReply(player, GetMessage("ErrorItemCreating", player.UserIDString));
			}

			if (userData.Contains(item))
				userData.Remove(item);

			SaveData();
		}

		private static List<DataItem> GetUserData(ulong userID)
		{
			if (!_data.PlayersData.ContainsKey(userID))
				_data.PlayersData.Add(userID, new List<DataItem>());

			return _data.PlayersData[userID];
		}

		private static float GetGroundPosition(Vector3 pos)
		{
			var y = TerrainMeta.HeightMap.GetHeight(pos);
			RaycastHit hit;
			if (Physics.Raycast(new Vector3(pos.x, pos.y + 200f, pos.z), Vector3.down, out hit, Mathf.Infinity,
				    LayerMask.GetMask("Terrain", "World", "Default", "Construction", "Deployed")) &&
			    !hit.collider.name.Contains("rock_cliff")) return Mathf.Max(hit.point.y, y);
			return y;
		}

		private static Vector3 GetHeliSpawn(Vector3 pos)
		{
			var position = pos;
			position.y = GetGroundPosition(pos) + 50;
			return position;
		}

		private static string GetShortNickname(BasePlayer player)
		{
			return player.displayName.Length > _config.NameLenght
				? player.displayName.Substring(0, _config.NameLenght)
				: player.displayName;
		}

		#region Avatar

		private readonly Regex Regex = new Regex(@"<avatarFull><!\[CDATA\[(.*)\]\]></avatarFull>");

		private void GetAvatar(ulong userId, Action<string> callback)
		{
			if (callback == null) return;

			webrequest.Enqueue($"http://steamcommunity.com/profiles/{userId}?xml=1", null, (code, response) =>
			{
				if (code != 200 || response == null)
					return;

				var avatar = Regex.Match(response).Groups[1].ToString();
				if (avatar.IsNullOrEmpty())
					return;

				callback.Invoke(avatar);
			}, this);
		}

		#endregion

		#endregion

		#region API

		private bool API_StartEvent(string toEvent)
		{
			if (hasStarted || !_config.EnabledEvents.Contains(toEvent)) return false;

			StartEvent(toEvent);
			return true;
		}

		private bool API_StartRandomEvent()
		{
			if (hasStarted) return false;

			StartEvent(_config.EnabledEvents.GetRandom());
			return true;
		}

		private bool IsFoundationDropPlayer(ulong userId)
		{
			return hasStarted && nowEvent == "FoundationDrop" && cEvent != null &&
			       cEvent.PlayerConnected.ContainsKey(userId);
		}

		private bool IsEventPlayer(ulong userId)
		{
			return hasStarted &&
			       (PlayersTop.Any(x => x.Key.userID == userId) ||
			        runner != null && runner.userID == userId ||
			        cEvent != null && cEvent.PlayerConnected.ContainsKey(userId));
		}

		#endregion
	}
}