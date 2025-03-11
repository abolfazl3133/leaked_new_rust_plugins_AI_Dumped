// #define TESTING

#if TESTING
using System.Diagnostics;
#endif
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

#if CARBON
using Carbon.Base;
using Carbon.Modules;
#endif

namespace Oxide.Plugins;

[Info("Battlepass", "Mevent", "1.37.20")]
public class Battlepass : RustPlugin
{
	#region Fields

	[PluginReference] private Plugin
		ImageLibrary = null,
		PlayerDatabase = null,
		Notify = null,
		UINotify = null,
		NoEscape = null;

	private const string
		Layer = "UI.Battlepass",
		ModalLayer = "UI.Battlepass.Modal";

	private static Battlepass _instance;

	private bool _needUpdate;

	private Timer _updateMissions, _refreshCooldown;

	private bool _enabledImageLibrary;

#if CARBON
	private ImageDatabaseModule imageDatabase;
#endif

	private readonly Dictionary<int, ItemConf> _itemById = new();

	private readonly Dictionary<ulong, List<ItemConf>> _openedCaseItems = new();

	private List<GeneralMission> _generalMissions = new();

	private List<int> _privateMissions = new();

	private readonly List<BasePlayer> _missionPlayers = new();

	private DateTime _nextTime;

	private readonly DateTime epoch = new(1970, 1, 1, 0, 0, 0);

	private enum MissionType
	{
		Gather,
		Kill,
		Craft,
		Look,
		Build,
		Upgrade,
		Fishing,
		LootCrate,
		Swipe,
		RaidableBases,
		RecycleItem,
		HackCrate,
		PurchaseFromNpc,
		ArcticBaseEvent,
		GasStationEvent,
		SputnikEvent,
		ShipwreckEvent,
		HarborEvent,
		JunkyardEvent,
		SatDishEvent,
		WaterEvent,
		AirEvent,
		PowerPlantEvent,
		ArmoredTrainEvent,
		ConvoyEvent,
		SurvivalArena,
		KillBoss
	}

	private class GeneralMission
	{
		public int ID;
		public MissionConfig Mission;

		public GeneralMission(int id, MissionConfig mission)
		{
			ID = id;
			Mission = mission;
		}

		public void Check(BasePlayer player, ref PlayerData data, MissionType type, Item item = null,
			BaseEntity entity = null,
			int targetGrade = 0, int itemAmount = -1,
			string mode = null,
			ItemDefinition definition = null)
		{
			if (Mission.Type != type || !_config.Missions.ContainsKey(ID)) return;

			data.Missions.TryAdd(ID, 0);

			if (data.Missions[ID] >= Mission.Amount) return;

			var missionAmount = GetMissionAmount(Mission, item, entity, targetGrade, itemAmount, mode, definition);
#if TESTING
			SayDebug($"[Check.{player.userID}] type: {type}, amount: {missionAmount}");
#endif

			data.Missions[ID] += missionAmount;

			if (data.Missions[ID] >= Mission.Amount)
			{
				CompleteMission(player, Mission);
				if (_config.ResetQuestAfterComplete) data.Missions[ID] = 0;
			}
		}
	}

	private enum ItemType
	{
		Item,
		Command,
		Plugin
	}

	#endregion

	#region Config

	private static Configuration _config;

	private class Configuration
	{
		[JsonProperty(PropertyName = "Command")]
		public string Command = "pass";

		[JsonProperty(PropertyName = "Commands for Missions page")]
		public string[] MissionsCommands = {"passmissions", "pmissions"};

		[JsonProperty(PropertyName = "Commands for Cases page")]
		public string[] CasesCommands = {"passcase"};

		[JsonProperty(PropertyName = "Commands for Inventory page")]
		public string[] InventoryCommands = {"passinv"};

		[JsonProperty(PropertyName = "Work with Notify?")]
		public bool UseNotify = true;

		[JsonProperty(PropertyName = "Permission")]
		public string Permission = "battlepass.use";

		[JsonProperty(PropertyName = "Background")]
		public string Background = "https://i.ibb.co/0JbMVnV/image.png";

		[JsonProperty(PropertyName = "Logo")] public string Logo = "https://i.ibb.co/GMMWyB4/image.png";

		[JsonProperty(PropertyName = "Reset the quest after completing it?")]
		public bool ResetQuestAfterComplete = false;

		[JsonProperty(PropertyName = "Wipe Settings")]
		public WipeSettings Wipe = new()
		{
			Players = false,
			Missions = false
		};

		[JsonProperty(PropertyName = "Currency 1")]
		public FirstCurrencyClass FirstCurrency = new()
		{
			Image = "https://i.ibb.co/ThrdX3r/image.png",
			UseDefaultCur = true,
			AddHook = "Deposit",
			BalanceHook = "Balance",
			RemoveHook = "Withdraw",
			Plug = "Economics",
			Rates = new Dictionary<string, float>
			{
				["battlepass.vip"] = 2f,
				["battlepass.premium"] = 3f
			}
		};

		[JsonProperty(PropertyName = "Use 2nd currency?")]
		public bool useSecondCur = true;

		[JsonProperty(PropertyName = "Currency 2")]
		public SecondCurrencyClass SecondCurrency = new()
		{
			Permission = "battlepass.vip",
			Image = "https://i.ibb.co/gRbyTFW/image.png",
			UseDefaultCur = true,
			AddHook = "Deposit",
			BalanceHook = "Balance",
			RemoveHook = "Withdraw",
			Plug = "Economics"
		};

		[JsonProperty(PropertyName = "Image Top Awards")]
		public string AdvanceAwardImg = "https://i.ibb.co/4jcqqhf/image.png";

		[JsonProperty(PropertyName = "Background for cases")]
		public string CaseBG = "https://i.ibb.co/kg8tR4K/image.png";

		[JsonProperty(PropertyName = "Cases", ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public List<CaseClass> Cases = new()
		{
			new()
			{
				DisplayName = "Newbie Case",
				Permission = "",
				Image = "https://i.ibb.co/chhtKTK/9KIoJ2G.png",
				FCost = 150,
				PCost = 75,
				Items = new List<ItemConf>
				{
					new() { Title = "Wood", Chance = 80, Type = ItemType.Item, Shortname = "wood", Amount = 1000 },
					new() { Title = "Stones", Chance = 75, Type = ItemType.Item, Shortname = "stones", Amount = 1500 },
					new() { Title = "Bandage", Chance = 70, Type = ItemType.Item, Shortname = "bandage", Amount = 5 },
					new() { Title = "Pistol", Chance = 50, Type = ItemType.Item, Shortname = "pistol.revolver", Amount = 1 },
					new() { Title = "Low Grade Fuel", Chance = 40, Type = ItemType.Item, Shortname = "lowgradefuel", Amount = 50 },
					new() { Title = "Hunting Bow", Chance = 30, Type = ItemType.Item, Shortname = "bow.hunting", Amount = 1 },
					new() { Title = "Leather Gloves", Chance = 25, Type = ItemType.Item, Shortname = "gloves.leather", Amount = 1 }
				}
			},
			new()
			{
				DisplayName = "Amateur Case",
				Permission = "battlepass.use",
				Image = "https://i.ibb.co/9yMhCrR/5bur68a.png",
				FCost = 500,
				PCost = 250,
				Items = new List<ItemConf>
				{
					new() { Title = "Metal Fragments", Chance = 75, Type = ItemType.Item, Shortname = "metal.fragments", Amount = 500 },
					new() { Title = "SMG", Chance = 60, Type = ItemType.Item, Shortname = "smg.thompson", Amount = 1 },
					new() { Title = "Medical Syringe", Chance = 55, Type = ItemType.Item, Shortname = "syringe.medical", Amount = 2 },
					new() { Title = "Road Sign Jacket", Chance = 50, Type = ItemType.Item, Shortname = "jacket.roadsign", Amount = 1 },
					new() { Title = "Gunpowder", Chance = 45, Type = ItemType.Item, Shortname = "gunpowder", Amount = 250 },
					new() { Title = "Crossbow", Chance = 40, Type = ItemType.Item, Shortname = "crossbow", Amount = 1 },
					new() { Title = "Tactical Gloves", Chance = 35, Type = ItemType.Item, Shortname = "gloves.tactical", Amount = 1 }
				}
			},
			new()
			{
				DisplayName = "Professional Case",
				Permission = "battlepass.use",
				Image = "https://i.ibb.co/4Y4K8vm/kZyZqy9.png",
				FCost = 1000,
				PCost = 500,
				Items = new List<ItemConf>
				{
					new() { Title = "Sulfur", Chance = 70, Type = ItemType.Item, Shortname = "sulfur.ore", Amount = 1000 },
					new() { Title = "Assault Rifle", Chance = 60, Type = ItemType.Item, Shortname = "rifle.ak", Amount = 1 },
					new() { Title = "Metal Facemask", Chance = 55, Type = ItemType.Item, Shortname = "mask.metal", Amount = 1 },
					new() { Title = "Explosives", Chance = 50, Type = ItemType.Item, Shortname = "explosives", Amount = 2 },
					new() { Title = "Rocket", Chance = 45, Type = ItemType.Item, Shortname = "ammo.rocket.basic", Amount = 1 },
					new() { Title = "L96 Sniper", Chance = 40, Type = ItemType.Item, Shortname = "rifle.l96", Amount = 1 },
					new() { Title = "HV Rocket", Chance = 35, Type = ItemType.Item, Shortname = "ammo.rocket.hv", Amount = 1 }
				}
			},
			new()
			{
				DisplayName = "Master Case",
				Permission = "battlepass.use",
				Image = "https://i.ibb.co/FBpxMmK/NvHk5Sw.png",
				FCost = 1500,
				PCost = 750,
				Items = new List<ItemConf>
				{
					new() { Title = "High Quality Metal", Chance = 65, Type = ItemType.Item, Shortname = "metal.refined", Amount = 50 },
					new() { Title = "M249", Chance = 55, Type = ItemType.Item, Shortname = "lmg.m249", Amount = 1 },
					new() { Title = "Armored Door", Chance = 50, Type = ItemType.Item, Shortname = "door.hinged.metal", Amount = 1 },
					new() { Title = "C4", Chance = 45, Type = ItemType.Item, Shortname = "explosive.timed", Amount = 1 },
					new() { Title = "Rocket Launcher", Chance = 40, Type = ItemType.Item, Shortname = "rocket.launcher", Amount = 1 },
					new() { Title = "Incendiary Rocket", Chance = 35, Type = ItemType.Item, Shortname = "ammo.rocket.incendiary", Amount = 2 },
					new() { Title = "Auto Turret", Chance = 30, Type = ItemType.Item, Shortname = "autoturret", Amount = 1 }
				}
			},
			new()
			{
				DisplayName = "Legendary Case",
				Permission = "battlepass.use",
				Image = "https://i.ibb.co/XLPg66N/3mtbqji.png",
				FCost = 2000,
				PCost = 1000,
				Items = new List<ItemConf>
				{
					new() { Title = "Elite Crate", Chance = 60, Type = ItemType.Item, Shortname = "crate_elite", Amount = 1 },
					new() { Title = "Military Crate", Chance = 55, Type = ItemType.Item, Shortname = "crate_military", Amount = 1 },
					new() { Title = "MLRS Rocket", Chance = 50, Type = ItemType.Item, Shortname = "ammo.rocket.mlrs", Amount = 2 },
					new() { Title = "Sam Site", Chance = 45, Type = ItemType.Item, Shortname = "samsite", Amount = 1 },
					new() { Title = "Attack Helicopter", Chance = 40, Type = ItemType.Item, Shortname = "minihelicopter.repair", Amount = 1 },
					new() { Title = "Tank", Chance = 35, Type = ItemType.Item, Shortname = "car.tank", Amount = 1 },
					new() { Title = "M2 Bradley APC", Chance = 25, Type = ItemType.Item, Shortname = "bradleyapc", Amount = 1 }
				}
			},
			new()
			{
				DisplayName = "Pirate Treasure",
				Permission = "battlepass.vip",
				Image = "https://i.ibb.co/M2bNGcM/tsPPUhg.png",
				FCost = 3000,
				PCost = 1500,
				Items = new List<ItemConf>
				{
					new() { Title = "Golden Revolver", Chance = 50, Type = ItemType.Item, Shortname = "pistol.python", Skin = 123456789, Amount = 1 },
					new() { Title = "Treasure Map", Chance = 45, Type = ItemType.Item, Shortname = "map", Skin = 987654321, Amount = 1 },
					new() { Title = "Ancient Coin", Chance = 40, Type = ItemType.Item, Shortname = "coin", Skin = 555555555, Amount = 5 },
					new() { Title = "Pirate Cutlass", Chance = 35, Type = ItemType.Item, Shortname = "longsword", Skin = 111222333, Amount = 1 },
					new() { Title = "Cannon Shells", Chance = 30, Type = ItemType.Item, Shortname = "ammo.shotgun.slug", Amount = 25 },
					new() { Title = "Rum Barrel", Chance = 25, Type = ItemType.Item, Shortname = "water.barrel", Skin = 444555666, Amount = 1 },
					new() { Title = "Captain's Hat", Chance = 20, Type = ItemType.Item, Shortname = "hat.captain", Skin = 777888999, Amount = 1 }
				}
			}
		};

		[JsonProperty(PropertyName = "Total missions per day")]
		public int MissionsCount = 7;

		[JsonProperty(PropertyName =
			"How many hours are missions updated?")]
		public int MissionHours = 24;

		[JsonProperty(PropertyName = "Settings shared missions",
			ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public Dictionary<int, MissionConfig> Missions = new()
		{
			[1] = MissionConfig.CreateDefault(MissionType.Gather, "stones", "Collect 5000 stones", 5000, 50,
				useAdvanceAward: true,
				advanceAward: ItemConf.CreateItem(1031, "rifle.ak", 1, 1230963555, "Talon AK-47")),

			[2] = MissionConfig.CreateDefault(MissionType.Kill, "player", "Kill 3 players", 3, 90,
				useAdvanceAward: true,
				advanceAward: ItemConf.CreateItem(1031, "rifle.ak", 1, 1230963555, "Talon AK-47")),

			[3] = MissionConfig.CreateDefault(MissionType.Craft, "ammo.rocket.basic", "Craft 15 rockets", 15, 75,
				useAdvanceAward: true,
				advanceAward: ItemConf.CreateItem(1031, "rifle.ak", 1, 1230963555, "Talon AK-47")),

			[4] = MissionConfig.CreateDefault(MissionType.Look, "metalspring", "Loot 10 metal springs", 10, 50,
				useAdvanceAward: true,
				advanceAward: ItemConf.CreateItem(1031, "rifle.ak", 1, 1230963555, "Talon AK-47")),

			[5] = MissionConfig.CreateDefault(MissionType.Build, "wall.external.high.stone",
				"Build 25 high exterior stone walls to protect your home", 25, 100),

			[6] = MissionConfig.CreateDefault(MissionType.Upgrade, "foundation.prefab",
				"Upgrade 10 Foundations to Metal", 10, 60,
				grade: 3),

			[7] = MissionConfig.CreateDefault(MissionType.Gather, "wood", "Collect 10000 wood", 10000, 50,
				useAdvanceAward: true,
				advanceAward: ItemConf.CreateItem(1031, "rifle.ak", 1, 1230963555, "Talon AK-47")),

			[8] = MissionConfig.CreateDefault(MissionType.Craft, "door.hinged.toptier", "Craft 3 Armored Doors", 3,
				85,
				useAdvanceAward: true,
				advanceAward: ItemConf.CreateItem(1031, "rifle.ak", 1, 1230963555, "Talon AK-47")),

			[9] = MissionConfig.CreateDefault(MissionType.Fishing, "fish.herring", "Catch 10 herring", 10, 50,
				useSecondAward: true, secondAward: 25),

			[10] = MissionConfig.CreateDefault(MissionType.Swipe, "keycard_blue",
				"Swipe the Blue Keycard on monuments 10 times", 10, 50, useSecondAward: true, secondAward: 25),

			[11] = MissionConfig.CreateDefault(MissionType.RaidableBases, string.Empty,
				"Win the RaidableBases event 3 times", 3, 75,
				raidMode: "Easy"),

			[12] = MissionConfig.CreateDefault(MissionType.RecycleItem, "riflebody", "Recycle 15 Rifle Body", 15,
				50),

			[13] = MissionConfig.CreateDefault(MissionType.HackCrate, "codelockedhackablecrate",
				"Hack 3 locked crates", 3, 50),

			[14] = MissionConfig.CreateDefault(MissionType.PurchaseFromNpc, "scrap", "Buy 1000 scrap from bots",
				1000, 50),

			[15] = MissionConfig.CreateDefault(MissionType.ArcticBaseEvent, string.Empty,
				"Win the \"Artic Base Event\" 3 times", 3, 100, enabled: false),
			[16] = MissionConfig.CreateDefault(MissionType.GasStationEvent, string.Empty,
				"Win the \"GasStation Event\" 3 times", 3, 100, enabled: false),
			[17] = MissionConfig.CreateDefault(MissionType.SputnikEvent, string.Empty,
				"Win the \"Sputnik Event\" 3 times", 3, 100, enabled: false),
			[18] = MissionConfig.CreateDefault(MissionType.ShipwreckEvent, string.Empty,
				"Win the \"Shipwreck Event\" 3 times", 3, 100, enabled: false),
			[19] = MissionConfig.CreateDefault(MissionType.HarborEvent, string.Empty,
				"Win the \"Harbor Event\" 3 times", 3, 100, enabled: false),
			[20] = MissionConfig.CreateDefault(MissionType.JunkyardEvent, string.Empty,
				"Win the \"Junkyard Event\" 3 times", 3, 100, enabled: false),
			[21] = MissionConfig.CreateDefault(MissionType.SatDishEvent, string.Empty,
				"Win the \"Sat Dish Event\" 3 times", 3, 100, enabled: false),
			[22] = MissionConfig.CreateDefault(MissionType.WaterEvent, string.Empty,
				"Win the \"Water Event\" 3 times", 3, 100, enabled: false),
			[23] = MissionConfig.CreateDefault(MissionType.AirEvent, string.Empty,
				"Win the \"Air Event\" 3 times", 3, 100, enabled: false),
			[24] = MissionConfig.CreateDefault(MissionType.PowerPlantEvent, string.Empty,
				"Win the \"Power Plant Event\" 3 times", 3, 100, enabled: false),
			[25] = MissionConfig.CreateDefault(MissionType.ArmoredTrainEvent, string.Empty,
				"Win the \"Armored Train Event\" 3 times", 3, 100, enabled: false),
			[26] = MissionConfig.CreateDefault(MissionType.ConvoyEvent, string.Empty,
				"Win the \"Convoy Event\" 3 times", 3, 100, enabled: false),
			[27] = MissionConfig.CreateDefault(MissionType.SurvivalArena, string.Empty,
				"Win the \"Survival Arena\" 3 times", 3, 100, enabled: false),
			[28] = MissionConfig.CreateDefault(MissionType.KillBoss, string.Empty,
				"Kill the boss 5 times", 5, 75, enabled: false)
		};

		[JsonProperty(PropertyName = "Settings challenge of the day",
			ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public Dictionary<int, MissionConfig> PrivateMissions = new()
		{
			[1] = MissionConfig.CreateDefault(MissionType.Gather, "stones", "Collect 5000 stones", 5000, 50,
				useAdvanceAward: true,
				advanceAward: ItemConf.CreateItem(1031, "rifle.ak", 1, 1230963555, "Talon AK-47")),

			[2] = MissionConfig.CreateDefault(MissionType.Kill, "player", "Kill 3 players", 3, 90,
				useAdvanceAward: true,
				advanceAward: ItemConf.CreateItem(1031, "rifle.ak", 1, 1230963555, "Talon AK-47")),

			[3] = MissionConfig.CreateDefault(MissionType.Craft, "ammo.rocket.basic", "Craft 15 rockets", 15, 75,
				useAdvanceAward: true,
				advanceAward: ItemConf.CreateItem(1031, "rifle.ak", 1, 1230963555, "Talon AK-47")),

			[4] = MissionConfig.CreateDefault(MissionType.Look, "metalspring", "Loot 10 metal springs", 10, 50,
				useAdvanceAward: true,
				advanceAward: ItemConf.CreateItem(1031, "rifle.ak", 1, 1230963555, "Talon AK-47")),

			[5] = MissionConfig.CreateDefault(MissionType.Build, "wall.external.high.stone",
				"Build 25 high exterior stone walls to protect your home", 25, 100),

			[6] = MissionConfig.CreateDefault(MissionType.Upgrade, "foundation.prefab",
				"Upgrade 10 Foundations to Metal", 10, 60,
				grade: 3)
		};

		[JsonProperty(PropertyName = "Enable logging to the console?")]
		public bool LogToConsole = true;

		[JsonProperty(PropertyName = "Enable logging to the file?")]
		public bool LogToFile = true;

		[JsonProperty(PropertyName = "PlayerDatabase")]
		public PlayerDatabaseConf PlayerDatabase = new(false, "Battlepass");

		[JsonProperty(PropertyName = "Loot Settings (for storage containers)")]
		public LootSettings StorageLoot = new()
		{
			Enabled = true,
			Containers = new Dictionary<string, List<ulong>>
			{
				["box.wooden.large"] = new()
				{
					2764183607,
					624269671
				},
				["stocking_small_deployed"] = new()
				{
					0
				}
			}
		};

		[JsonProperty(PropertyName = "Cooldown After Wipe (0 - disable)")]
		public CooldownSettings WipeCooldown = new()
		{
			Inventory = 0
		};

		[JsonProperty(PropertyName = "Give out case items immediately to the player?")]
		public bool CaseItemsToPlayer = false;

		[JsonProperty(PropertyName = "Give an advance reward to the player's inventory?")]
		public bool AdRewardToInventory = false;

		[JsonProperty(PropertyName = "Notify the player when a mission is completed?")]
		public bool NotifyMissionCompleted = true;

		[JsonProperty(PropertyName = "Block (NoEscape)")]
		public bool BlockNoEscape = false;

		[JsonProperty(PropertyName = "Interface")]
		public UserInterface UI = new()
		{
			Header = new UserInterface.HeaderInfo(),
			Colors = new UserInterface.ColorsData(),
			Missions = new UserInterface.MissionsScreen(),
			Cases = new UserInterface.CasesScreen(),
			CaseInfo = new UserInterface.CasesInfoScreen(),
			Inventory = new UserInterface.InventoryScreen()
		};

		[JsonProperty(PropertyName = "Tabs")] public TabsSettings Tabs = new()
		{
			Margin = 0f,
			Missions = new TabInterface(true, 0, 256f, 256f,
				"https://i.ibb.co/C0jjqLB/f2GN8m7.png",
				new UserInterface.InterfacePosition
				{
					AnchorMin = "0.5 0.5",
					AnchorMax = "0.5 0.5",
					OffsetMin = "-105 -75",
					OffsetMax = "105 135"
				},
				new UserInterface.Button
				{
					AnchorMin = "0.5 0",
					AnchorMax = "0.5 0",
					OffsetMin = "-105 5",
					OffsetMax = "105 35",
					Align = TextAnchor.MiddleCenter,
					FontSize = 16,
					Font = "robotocondensed-bold.ttf",
					ButtonColor = new UserInterface.ColorsData.IColor("#000000",
						00),
					Color = "1 1 1 1",
					FadeIn = 0
				},
				"Mission btn",
				"UI_Battlepass missions",
				false),
			Cases = new TabInterface(true, 1, 256f, 256f,
				"https://i.ibb.co/HtFh68D/2lMM2bS.png",
				new UserInterface.InterfacePosition
				{
					AnchorMin = "0.5 0.5",
					AnchorMax = "0.5 0.5",
					OffsetMin = "-105 -75",
					OffsetMax = "105 135"
				},
				new UserInterface.Button
				{
					AnchorMin = "0.5 0",
					AnchorMax = "0.5 0",
					OffsetMin = "-105 5",
					OffsetMax = "105 35",
					Align = TextAnchor.MiddleCenter,
					Color = "1 1 1 1",
					FadeIn = 0,
					FontSize = 16,
					Font = "robotocondensed-bold.ttf",
					ButtonColor = new UserInterface.ColorsData.IColor("#000000",
						00)
				},
				"Cases btn",
				"UI_Battlepass cases",
				false),
			Inventory = new TabInterface(true, 2, 256f, 256f,
				"https://i.ibb.co/KLMwgHj/vvJe7KO.png",
				new UserInterface.InterfacePosition
				{
					AnchorMin = "0.5 0.5",
					AnchorMax = "0.5 0.5",
					OffsetMin = "-105 -75",
					OffsetMax = "105 135"
				},
				new UserInterface.Button
				{
					AnchorMin = "0.5 0",
					AnchorMax = "0.5 0",
					OffsetMin = "-105 5",
					OffsetMax = "105 35",
					Align = TextAnchor.MiddleCenter,
					Color = "1 1 1 1",
					FadeIn = 0,
					FontSize = 16,
					Font = "robotocondensed-bold.ttf",
					ButtonColor = new UserInterface.ColorsData.IColor("#000000",
						00)
				},
				"Inventory btn",
				"UI_Battlepass inventory",
				false),
			CustomTabs = new List<TabInterface>
			{
				new(false, 3, 256f, 256f,
					"https://i.ibb.co/xDsVSXY/C83Rprq.png",
					new UserInterface.InterfacePosition
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = "-105 -75",
						OffsetMax = "105 135"
					},
					new UserInterface.Button
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-105 5", OffsetMax = "105 35",
						Align = TextAnchor.MiddleCenter,
						FontSize = 16,
						ButtonColor = new UserInterface.ColorsData.IColor("#000000", 00)
					},
					"Leaders",
					"leaders",
					true)
			}
		};

		public VersionNumber Version;
	}

	private class TabsSettings
	{
		[JsonProperty(PropertyName = "Margin")]
		public float Margin;

		[JsonProperty(PropertyName = "Missions")]
		public TabInterface Missions;

		[JsonProperty(PropertyName = "Cases")] public TabInterface Cases;

		[JsonProperty(PropertyName = "Inventory")]
		public TabInterface Inventory;

		[JsonProperty(PropertyName = "Custom Tabs", ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public List<TabInterface> CustomTabs;

		[JsonIgnore] private List<TabInterface> _tabsList;

		[JsonIgnore] public List<TabInterface> Tabs => _tabsList ?? (_tabsList = GetTabs());

		private List<TabInterface> GetTabs()
		{
			var list = new List<TabInterface>
			{
				Missions,
				Cases,
				Inventory
			};

			list.AddRange(CustomTabs);

			list.Sort((x, y) => x.Index.CompareTo(y.Index));

			return list;
		}
	}

	private class TabInterface
	{
		[JsonProperty(PropertyName = "Enabled")]
		public bool Enabled;

		[JsonProperty(PropertyName = "Index")] public int Index;

		[JsonProperty(PropertyName = "Width")] public float Width;

		[JsonProperty(PropertyName = "Height")]
		public float Height;

		[JsonProperty(PropertyName = "Image")] public string Image;

		[JsonProperty(PropertyName = "Image Position")]
		public UserInterface.InterfacePosition ImagePosition;

		[JsonProperty(PropertyName = "Button")]
		public UserInterface.Button Button;

		[JsonProperty(PropertyName = "Lang Key")]
		public string LangKey;

		[JsonProperty(PropertyName = "Command")]
		public string Command;

		[JsonProperty(PropertyName = "Close Menu")]
		public bool CloseMenu;

		public TabInterface(bool enabled, int index, float width, float height, string image,
			UserInterface.InterfacePosition imagePosition, UserInterface.Button button,
			string langKey,
			string command,
			bool closeMenu)
		{
			Enabled = enabled;
			Index = index;
			Width = width;
			Height = height;
			Image = image;
			ImagePosition = imagePosition;
			Button = button;
			LangKey = langKey;
			Command = command;
			CloseMenu = closeMenu;
		}
	}

	private class WipeSettings
	{
		[JsonProperty(PropertyName = "Wipe Players?")]
		public bool Players;

		[JsonProperty(PropertyName = "Wipe Missions?")]
		public bool Missions;
	}

	private class UserInterface
	{
		[JsonProperty(PropertyName = "Header")]
		public HeaderInfo Header;

		[JsonProperty(PropertyName = "Colors")]
		public ColorsData Colors;

		[JsonProperty(PropertyName = "Missions")]
		public MissionsScreen Missions;

		[JsonProperty(PropertyName = "Cases")] public CasesScreen Cases;

		[JsonProperty(PropertyName = "Case Page")]
		public CasesInfoScreen CaseInfo;

		[JsonProperty(PropertyName = "Inventory")]
		public InventoryScreen Inventory;

		public class InterfacePosition
		{
			public string AnchorMin;

			public string AnchorMax;

			public string OffsetMin;

			public string OffsetMax;
		}

		public class Label : InterfacePosition
		{
			[JsonProperty("FontSize")] public int FontSize = 14;

			[JsonProperty("Font")] public string Font = "robotocondensed-bold.ttf";

			[JsonConverter(typeof(StringEnumConverter))] [JsonProperty("Align")]
			public TextAnchor Align = TextAnchor.UpperLeft;

			[JsonProperty("Color")] public string Color = "1 1 1 1";

			[JsonProperty("FadeIn")] public float FadeIn;
		}

		public class Button : Label
		{
			[JsonProperty(PropertyName = "Button Color")]
			public ColorsData.IColor ButtonColor;
		}

		public class OutlineInfo
		{
			[JsonProperty(PropertyName = "Color")] public ColorsData.IColor Color = new("#FFFFFF");

			[JsonProperty(PropertyName = "Size")] public string Size = "1.5";
		}

		public class ColorsData
		{
			[JsonProperty(PropertyName = "Background Color")]
			public IColor BackgroundColor = new("#FFFFFF");

			[JsonProperty(PropertyName = "Color 1")]
			public IColor Color1 = new("#E54D41");

			[JsonProperty(PropertyName = "Color 2")]
			public IColor Color2 = new("#BF2E24");

			[JsonProperty(PropertyName = "Color 3")]
			public IColor Color3 = new("#BD5C22");

			[JsonProperty(PropertyName = "Color 4")]
			public IColor Color4 = new("#BD5C22", 40);

			[JsonProperty(PropertyName = "Color 5")]
			public IColor Color5 = new("#000000", 00);

			public class IColor
			{
				[JsonProperty(PropertyName = "HEX")] public string Hex;

				[JsonProperty(PropertyName = "Opacity (0 - 100)")]
				public float Alpha;

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
		}

		public class HeaderInfo
		{
			[JsonProperty(PropertyName = "Height")]
			public float Height = 35f;

			[JsonProperty(PropertyName = "Logo Position")]
			public InterfacePosition Logo = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "-350 -9",
				OffsetMax = "-250 9"
			};

			[JsonProperty(PropertyName = "Line")] public InterfacePosition Line = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "-240.5 -9",
				OffsetMax = "-239.5 9"
			};

			[JsonProperty(PropertyName = "Player Name")]
			public InterfacePosition PlayerName = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "-230 -9",
				OffsetMax = "80 9"
			};

			[JsonProperty(PropertyName = "First Currency")]
			public InterfacePosition FirstCurrency = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "230 -10",
				OffsetMax = "250 10"
			};

			[JsonProperty(PropertyName = "Balance of First Currency")]
			public Label BalanceFirstCurrency = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "0 -10",
				OffsetMax = "225 10",
				FontSize = 12,
				Align = TextAnchor.MiddleRight,
				Color = "1 1 1 1",
				FadeIn = 0,
				Font = "robotocondensed-regular.ttf"
			};

			[JsonProperty(PropertyName = "Second Currency")]
			public InterfacePosition SecondCurrency = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "315 -10",
				OffsetMax = "335 10"
			};

			[JsonProperty(PropertyName = "Balance of Second Currency")]
			public Label BalanceSecondCurrency = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "0 -10",
				OffsetMax = "310 10",
				FontSize = 12,
				Align = TextAnchor.MiddleRight,
				Color = "1 1 1 1",
				FadeIn = 0,
				Font = "robotocondensed-regular.ttf"
			};

			[JsonProperty(PropertyName = "Exit Button")]
			public Button Exit = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "200 295",
				OffsetMax = "320 315",
				Align = TextAnchor.MiddleRight,
				Color = "1 1 1 1",
				FadeIn = 0,
				FontSize = 16,
				Font = "robotocondensed-bold.ttf",
				ButtonColor = new ColorsData.IColor("#000000",
					00)
			};
		}

		public class MissionsScreen
		{
			[JsonProperty(PropertyName = "Back")] public Button Back = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "200 295",
				OffsetMax = "320 315",
				Align = TextAnchor.MiddleRight, FontSize = 16,
				ButtonColor = new ColorsData.IColor("#000000", 00)
			};

			[JsonProperty(PropertyName = "Private Mission | Progress Bar")]
			public InterfacePosition ProgressBar = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "-320 230",
				OffsetMax = "-15 235"
			};

			[JsonProperty(PropertyName = "Private Mission | Progress Title")]
			public Label ProgressTitle = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "-320 240",
				OffsetMax = "0 255",
				Align = TextAnchor.MiddleLeft,
				FontSize = 11,
				Font = "robotocondensed-regular.ttf"
			};

			[JsonProperty(PropertyName = "Private Mission | Progress Amount")]
			public Label ProgressAmount = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "-320 240",
				OffsetMax = "-15 255",
				Align = TextAnchor.MiddleRight,
				FontSize = 11,
				Font = "robotocondensed-regular.ttf"
			};

			[JsonProperty(PropertyName = "Private Mission | Title")]
			public Label PMTitle = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "-320 260",
				OffsetMax = "0 290",
				Align = TextAnchor.MiddleLeft,
				FontSize = 16
			};

			[JsonProperty(PropertyName = "Private Mission | Title Description")]
			public Label PMTitleDescription = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "15 260",
				OffsetMax = "320 290",
				Align = TextAnchor.MiddleLeft,
				FontSize = 16
			};

			[JsonProperty(PropertyName = "Private Mission | Description")]
			public Label PMDescription = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "15 200",
				OffsetMax = "325 255",
				Align = TextAnchor.UpperLeft,
				FontSize = 11,
				Font = "robotocondensed-regular.ttf"
			};

			[JsonProperty(PropertyName = "Private Mission | Title Award")]
			public Label PMTitleAward = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "15 195",
				OffsetMax = "150 215",
				Align = TextAnchor.MiddleLeft,
				FontSize = 11,
				Font = "robotocondensed-regular.ttf"
			};

			[JsonProperty(PropertyName = "Private Mission | First Currency Image")]
			public InterfacePosition FirstCurrencyImage = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "85 195",
				OffsetMax = "105 215"
			};

			[JsonProperty(PropertyName = "Private Mission | First Currency")]
			public Label FirstCurrency = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "55 195",
				OffsetMax = "80 215",
				Align = TextAnchor.MiddleRight,
				FontSize = 11,
				Font = "robotocondensed-regular.ttf"
			};

			[JsonProperty(PropertyName = "Private Mission | Second Currency Image")]
			public InterfacePosition SecondCurrencyImage = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "135 195",
				OffsetMax = "155 215"
			};

			[JsonProperty(PropertyName = "Private Mission | Second Currency")]
			public Label SecondCurrency = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "100 195",
				OffsetMax = "130 215",
				Align = TextAnchor.MiddleRight,
				FontSize = 11,
				Font = "robotocondensed-regular.ttf"
			};

			[JsonProperty(PropertyName = "Private Mission | Line")]
			public InterfacePosition Line1 = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "-335 164.5",
				OffsetMax = "325 165.5"
			};

			[JsonProperty(PropertyName = "Title")] public Label Title = new()
			{
				AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-320 95", OffsetMax = "0 165",
				Align = TextAnchor.MiddleLeft,
				FontSize = 14
			};

			[JsonProperty(PropertyName = "Cooldown")]
			public Label Cooldown = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "-150 95",
				OffsetMax = "150 165",
				Align = TextAnchor.MiddleCenter,
				FontSize = 14,
				Font = "robotocondensed-regular.ttf"
			};

			[JsonProperty(PropertyName = "Background for missions")]
			public InterfacePosition Background = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "-355 55",
				OffsetMax = "310 55"
			};

			[JsonProperty(PropertyName = "Pages")] public InterfacePosition Pages = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "315 -115",
				OffsetMax = "325 95"
			};
		}

		public class CasesScreen
		{
			[JsonProperty(PropertyName = "Back")] public Button Back = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "200 295",
				OffsetMax = "320 315",
				Align = TextAnchor.MiddleRight, FontSize = 16,
				ButtonColor = new ColorsData.IColor("#000000", 00)
			};

			[JsonProperty(PropertyName = "Title")] public Label Title = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "-320 250",
				OffsetMax = "0 290",
				Align = TextAnchor.MiddleLeft,
				FontSize = 26
			};

			[JsonProperty(PropertyName = "Up Indent for cases")]
			public float CaseUpIdent = 335;

			[JsonProperty(PropertyName = "Left Indent for cases")]
			public float CaseLeftIdent = 40;

			[JsonProperty(PropertyName = "Case Height")]
			public float CaseHeight = 195f;

			[JsonProperty(PropertyName = "Case Width")]
			public float CaseWidth = 205f;

			[JsonProperty(PropertyName = "Cases Margin")]
			public float CaseMargin = 10f;

			[JsonProperty(PropertyName = "Case Image")]
			public InterfacePosition CaseImage = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "-70 -65",
				OffsetMax = "70 75"
			};

			[JsonProperty(PropertyName = "Case Look Button")]
			public Button CaseShow = new()
			{
				AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-58 15", OffsetMax = "58 35",
				Align = TextAnchor.MiddleCenter,
				FontSize = 12,
				ButtonColor = new ColorsData.IColor("#000000", 00)
			};
		}

		public class CasesInfoScreen
		{
			[JsonProperty(PropertyName = "Amount Awards On Page")]
			public int AwardsOnPage = 10;

			[JsonProperty(PropertyName = "Back")] public Button Back = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "200 295",
				OffsetMax = "320 315",
				Align = TextAnchor.MiddleRight, FontSize = 16,
				ButtonColor = new ColorsData.IColor("#000000", 00)
			};

			[JsonProperty(PropertyName = "Title")] public Label Title = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "-320 250",
				OffsetMax = "0 290",
				Align = TextAnchor.MiddleLeft,
				FontSize = 26
			};

			[JsonProperty(PropertyName = "Case Title")]
			public Label CaseTitle = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "-320 200",
				OffsetMax = "0 250",
				Align = TextAnchor.MiddleLeft, FontSize = 18
			};

			[JsonProperty(PropertyName = "Case Image")]
			public InterfacePosition CaseImage = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "-290 5",
				OffsetMax = "-110 185"
			};

			[JsonProperty(PropertyName = "Buy Amount")]
			public Label BuyAmount = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "-310 -25",
				OffsetMax = "-110 -5",
				Align = TextAnchor.MiddleLeft,
				FontSize = 12,
				Font = "robotocondensed-regular.ttf"
			};

			[JsonProperty(PropertyName = "First Currency")]
			public InterfacePosition FirstCurrency = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "-210 -30",
				OffsetMax = "-135 0"
			};

			[JsonProperty(PropertyName = "Second Currency")]
			public InterfacePosition SecondCurrency = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "-130 -30",
				OffsetMax = "-55 0"
			};

			[JsonProperty(PropertyName = "Awards Title")]
			public Label AwardsTitle = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "70 200",
				OffsetMax = "270 250",
				Align = TextAnchor.MiddleLeft,
				FontSize = 18
			};

			[JsonProperty(PropertyName = "Awards Background")]
			public InterfacePosition AwardsBackground = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "70 -70",
				OffsetMax = "320 195"
			};

			[JsonProperty(PropertyName = "Open Button")]
			public Button OpenButton = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "-205 -75",
				OffsetMax = "-50 -50",
				Align = TextAnchor.MiddleCenter,
				FontSize = 12,
				ButtonColor = new ColorsData.IColor("#000000", 00)
			};

			[JsonProperty(PropertyName = "Open Button | Outline")]
			public OutlineInfo OpenButtonOutline = new()
			{
				Color = new ColorsData.IColor("#FFFFFF"),
				Size = "1"
			};

			[JsonProperty(PropertyName = "Enter Amount")]
			public InterfacePosition EnterAmount = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "-235 -75",
				OffsetMax = "-210 -50"
			};
		}

		public class InventoryScreen
		{
			[JsonProperty(PropertyName = "Item Size")]
			public int ItemSize = 80;

			[JsonProperty(PropertyName = "Items Margin")]
			public int Margin = 5;

			[JsonProperty(PropertyName = "Items On String")]
			public int ItemsOnString = 8;

			[JsonProperty(PropertyName = "Lines for items")]
			public int Lines = 6;

			[JsonIgnore] public int MaxCount => _config.UI.Inventory.Lines * _config.UI.Inventory.ItemsOnString;

			[JsonProperty(PropertyName = "Back")] public Label Back = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "200 295",
				OffsetMax = "320 315",
				Align = TextAnchor.MiddleRight, FontSize = 16
			};

			[JsonProperty(PropertyName = "Title")] public Label Title = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "-320 250",
				OffsetMax = "0 290",
				Align = TextAnchor.MiddleLeft,
				FontSize = 26
			};

			[JsonProperty(PropertyName = "Pages")] public InterfacePosition Pages = new()
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = "-335 -275",
				OffsetMax = "340 -260"
			};
		}
	}


	private class CooldownSettings
	{
		[JsonProperty(PropertyName = "Inventory")]
		public float Inventory;
	}

	private class LootSettings
	{
		[JsonProperty(PropertyName = "Enabled")]
		public bool Enabled;

		[JsonProperty(PropertyName = "Containers (shortname - skins) [skin 0 = all skins]",
			ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public Dictionary<string, List<ulong>> Containers;
	}

	private class PlayerDatabaseConf
	{
		[JsonProperty(PropertyName = "Enabled")]
		public bool Enabled;

		[JsonProperty(PropertyName = "Table")] public string Field;

		public PlayerDatabaseConf(bool enabled, string field)
		{
			Enabled = enabled;
			Field = field;
		}
	}

	private class CurrencyClass
	{
		[JsonProperty(PropertyName = "Image")] public string Image;

		[JsonProperty(PropertyName = "Use embedded system?")]
		public bool UseDefaultCur;

		[JsonProperty(PropertyName = "Plugin name")]
		public string Plug;

		[JsonProperty(PropertyName = "Balance add hook")]
		public string AddHook;

		[JsonProperty(PropertyName = "Balance remove hook")]
		public string RemoveHook;

		[JsonProperty(PropertyName = "Balance show hook")]
		public string BalanceHook;
	}

	private class FirstCurrencyClass : CurrencyClass
	{
		[JsonProperty(PropertyName = "Rates for permissions",
			ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public Dictionary<string, float> Rates;

		public double ShowBalance(BasePlayer player)
		{
			if (UseDefaultCur) return _instance.GetFirstCurrency(player.userID);

			var plugin = _instance?.plugins?.Find(Plug);
			if (plugin == null) return 0;

			return Math.Round(Convert.ToDouble(plugin.Call(BalanceHook, player.UserIDString)));
		}

		public void AddBalance(BasePlayer player, int amount)
		{
			AddBalance(player.UserIDString, amount);
		}

		public void AddBalance(string player, int amount)
		{
			if (UseDefaultCur)
			{
				_instance.AddFirstCurrency(player, amount);
				return;
			}

			var plugin = _instance?.plugins.Find(Plug);
			if (plugin == null) return;

			switch (Plug)
			{
				case "Economics":
					plugin.Call(AddHook, player, (double) amount);
					break;
				default:
					plugin.Call(AddHook, player, amount);
					break;
			}
		}

		public bool RemoveBalance(BasePlayer player, int amount)
		{
			if (ShowBalance(player) < amount) return false;

			if (UseDefaultCur) return _instance.RemoveFirstCurrency(player, amount);

			var plugin = _instance?.plugins.Find(Plug);
			if (plugin == null) return false;

			switch (Plug)
			{
				case "Economics":
					plugin.Call(RemoveHook, player.UserIDString, (double) amount);
					break;
				default:
					plugin.Call(RemoveHook, player.UserIDString, amount);
					break;
			}

			return true;
		}
	}

	private class SecondCurrencyClass : CurrencyClass
	{
		[JsonProperty(PropertyName = "Permission (empty - all)")]
		public string Permission;

		public double ShowBalance(BasePlayer player)
		{
			if (UseDefaultCur) return _instance.GetSecondCurrency(player.userID);

			var plugin = _instance?.plugins.Find(Plug);
			if (plugin == null) return 0;

			return Math.Round(Convert.ToDouble(plugin.Call(BalanceHook, player.UserIDString)));
		}

		public void AddBalance(BasePlayer player, int amount)
		{
			AddBalance(player.UserIDString, amount);
		}

		public void AddBalance(string player, int amount)
		{
			if (UseDefaultCur)
			{
				_instance.AddSecondCurrency(player, amount);
				return;
			}

			var plugin = _instance?.plugins.Find(Plug);
			if (plugin == null) return;

			switch (Plug)
			{
				case "Economics":
					plugin.Call(AddHook, player, (double) amount);
					break;
				default:
					plugin.Call(AddHook, player, amount);
					break;
			}
		}

		public bool RemoveBalance(BasePlayer player, int amount)
		{
			if (ShowBalance(player) < amount) return false;

			if (UseDefaultCur) return _instance.RemoveSecondCurrency(player, amount);

			var plugin = _instance?.plugins.Find(Plug);
			if (plugin == null) return false;

			switch (Plug)
			{
				case "Economics":
					plugin.Call(RemoveHook, player.UserIDString, (double) amount);
					break;
				default:
					plugin.Call(RemoveHook, player.UserIDString, amount);
					break;
			}

			return true;
		}
	}

	private class MissionConfig
	{
		#region Fields

		[JsonProperty(PropertyName = "Enabled")]
		public bool Enabled = true;

		[JsonProperty(PropertyName = "Mission description")]
		public string Description;

		[JsonProperty(PropertyName = "Mission type")] [JsonConverter(typeof(StringEnumConverter))]
		public MissionType Type;

		[JsonProperty(PropertyName = "Shortname/prefab")]
		public string Shortname;

		[JsonProperty(PropertyName = "Skin (0 - any item)")]
		public ulong Skin;

		[JsonProperty(PropertyName =
			"Upgrade Level (for 'Upgrade' missions)")]
		public int Grade;

		[JsonProperty(PropertyName = "Display Name (for 'Kill' missions)")]
		public string DisplayName;

		[JsonProperty(PropertyName = "Amount")]
		public int Amount;

		[JsonProperty(PropertyName = "Amount of main reward")]
		public int MainAward;

		[JsonProperty(PropertyName = "Give extra reward?")]
		public bool UseAdvanceAward;

		[JsonProperty(PropertyName = "Settings extra reward")]
		public ItemConf AdvanceAward;

		[JsonProperty(PropertyName = "Give second currency?")]
		public bool UseSecondAward;

		[JsonProperty(PropertyName = "Amount of second currency")]
		public int SecondAward;

		[JsonProperty(PropertyName = "Mode (for 'RaidableBases' missions)")]
		public string Mode;

		#endregion

		#region Helpers

		private void GiveMainAward(BasePlayer player)
		{
			_config.FirstCurrency.AddBalance(player, (int) (MainAward * GetPlayerRates(player.UserIDString)));
		}

		public void GiveAwards(BasePlayer player)
		{
			GiveMainAward(player);

			if (UseAdvanceAward && AdvanceAward != null)
			{
				if (_config.AdRewardToInventory)
					AdvanceAward?.GetItem(player);
				else
					PlayerData.GetOrCreate(player.UserIDString).Items.Add(AdvanceAward.ID);
			}

			if (UseSecondAward) _config.SecondCurrency.AddBalance(player, SecondAward);
		}

		public JObject ToJObject()
		{
			return new JObject
			{
				["description"] = Description,
				["type"] = (int) Type,
				["shortname"] = Shortname,
				["skin"] = Skin,
				["grade"] = Grade,
				["amount"] = Amount,
				["mainaward"] = MainAward,
				["use_advanceaward"] = UseAdvanceAward,
				["advanceaward_image"] = AdvanceAward?.Image,
				["advanceaward_displayname"] = AdvanceAward?.DisplayName,
				["advanceaward_title"] = AdvanceAward?.Title,
				["advanceaward_shortname"] = AdvanceAward?.Shortname,
				["advanceaward_skin"] = AdvanceAward?.Skin,
				["advanceaward_amount"] = AdvanceAward?.Amount,
				["use_second_award"] = UseSecondAward,
				["second_award"] = SecondAward
			};
		}

		#endregion

		#region Constructors

		public static MissionConfig CreateDefault(
			MissionType type,
			string shortName, string description,
			int amount, int mainAward,
			ulong skinID = 0UL, string displayName = "",
			bool useAdvanceAward = false, ItemConf advanceAward = null,
			bool useSecondAward = false, int secondAward = 0, int grade = 0,
			string raidMode = "",
			bool enabled = true)
		{
			return new MissionConfig
			{
				Enabled = enabled,
				Description = description,
				Type = type,
				Shortname = shortName,
				Skin = skinID,
				Grade = grade,
				DisplayName = displayName ?? string.Empty,
				Amount = amount,
				MainAward = mainAward,
				UseAdvanceAward = useAdvanceAward,
				AdvanceAward = advanceAward ?? new ItemConf(),
				UseSecondAward = useSecondAward,
				SecondAward = secondAward,
				Mode = raidMode ?? string.Empty
			};
		}

		#endregion
	}

	private class CaseClass
	{
		[JsonProperty(PropertyName = "Case Display Name")]
		public string DisplayName;

		[JsonProperty(PropertyName = "Image")] public string Image;

		[JsonProperty(PropertyName = "Permission")]
		public string Permission;

		[JsonProperty(PropertyName = "Cost in currency 1")]
		public int FCost;

		[JsonProperty(PropertyName = "Cost in currency 2")]
		public int PCost;

		[JsonProperty(PropertyName = "Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public List<ItemConf> Items = new();
	}

	private class ItemConf
	{
		#region Fields

		[JsonProperty(PropertyName =
			"Display Name (for display in the interface)")]
		public string Title;

		[JsonProperty(PropertyName = "ID")] public int ID;

		[JsonProperty(PropertyName = "Chance")]
		public float Chance;

		[JsonProperty(PropertyName = "Item type")] [JsonConverter(typeof(StringEnumConverter))]
		public ItemType Type;

		[JsonProperty(PropertyName =
			"Image (if empty - the icon is taken by shortname) ")]
		public string Image;

		[JsonProperty(PropertyName =
			"Display name (for the item) (if empty - standard)")]
		public string DisplayName;

		[JsonProperty(PropertyName = "Shortname")]
		public string Shortname;

		[JsonProperty(PropertyName = "Skin")] public ulong Skin;

		[JsonProperty(PropertyName = "Amount (for item)")]
		public int Amount;

		[JsonProperty(PropertyName = "Command")]
		public string Command;

		[JsonProperty(PropertyName = "Plugin")]
		public PluginAward PluginAward;

		#endregion

		#region Helpers

		[JsonIgnore] private int _itemId = -1;

		[JsonIgnore]
		public int itemId
		{
			get
			{
				if (_itemId == -1)
					_itemId = ItemManager.FindItemDefinition(Shortname)?.itemid ?? -1;

				return _itemId;
			}
		}

		private void ToItem(BasePlayer player)
		{
			var newItem = ItemManager.CreateByName(Shortname, Amount, Skin);

			if (newItem == null)
			{
				_instance?.PrintError($"Error creating item with shortname '{Shortname}'");
				return;
			}

			if (!string.IsNullOrEmpty(DisplayName)) newItem.name = DisplayName;

			player.GiveItem(newItem, BaseEntity.GiveItemReason.PickedUp);
		}

		private void ToCommand(BasePlayer player)
		{
			var command = Command.Replace("\n", "|")
				.Replace("%steamid%", player.UserIDString, StringComparison.OrdinalIgnoreCase).Replace("%username%",
					player.displayName, StringComparison.OrdinalIgnoreCase);

			foreach (var check in command.Split('|')) _instance?.Server.Command(check);
		}

		public void GetItem(BasePlayer player)
		{
			if (player == null) return;

			switch (Type)
			{
				case ItemType.Command:
				{
					ToCommand(player);
					break;
				}
				case ItemType.Plugin:
				{
					PluginAward?.ToPluginAward(player);
					break;
				}
				case ItemType.Item:
				{
					ToItem(player);
					break;
				}
			}
		}

		public JObject ToJObject()
		{
			return new JObject
			{
				["title"] = Title,
				["id"] = ID,
				["chance"] = Chance,
				["type"] = (int) Type,
				["image"] = Image,
				["displayname"] = DisplayName,
				["shortname"] = Shortname,
				["skin"] = Skin,
				["amount"] = Amount,
				["command"] = Command,
				["plugin_hook"] = PluginAward?.Hook,
				["plugin_name"] = PluginAward?.Plugin,
				["plugin_amount"] = PluginAward?.Amount ?? 0
			};
		}

		#endregion

		#region Constructors

		public static ItemConf CreateItem(int ID, string shortName, int amount, ulong skinID, string title,
			string displayName = "",
			string image = "",
			float chance = 100f)
		{
			return new ItemConf
			{
				Title = title ?? string.Empty,
				ID = ID,
				Chance = chance,
				Type = ItemType.Item,
				Image = image ?? string.Empty,
				DisplayName = displayName ?? string.Empty,
				Shortname = shortName ?? string.Empty,
				Skin = skinID,
				Amount = amount,
				Command = string.Empty,
				PluginAward = new PluginAward
				{
					Hook = string.Empty,
					Plugin = string.Empty,
					Amount = 0
				}
			};
		}

		#endregion
	}

	private class PluginAward
	{
		[JsonProperty("Hook to call")] public string Hook = "Withdraw";

		[JsonProperty("Plugin name")] public string Plugin = "Economics";

		[JsonProperty("Amount")] public int Amount;

		public void ToPluginAward(BasePlayer player)
		{
			var plug = _instance?.plugins.Find(Plugin);
			if (plug == null)
			{
				_instance?.PrintError($"Economy plugin '{Plugin}' not found !!! ");
				return;
			}

			switch (Plugin)
			{
				case "Economics":
				{
					plug.Call(Hook, player.userID.Get(), (double) Amount);
					break;
				}
				default:
				{
					plug.Call(Hook, player.userID.Get(), Amount);
					break;
				}
			}
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
		Config.WriteObject(_config, true);
	}

	protected override void LoadDefaultConfig()
	{
		_config = new Configuration();
	}

	private void UpdateConfigValues()
	{
		PrintWarning("Config update detected! Updating config values...");

		// var baseConfig = new Configuration();

		if (_config.Version == default || _config.Version < new VersionNumber(1, 37, 0)) ConvertPlayerData();

		if (_config.Version != default)
			if (_config.Version < new VersionNumber(1, 37, 15))
			{
				if (_config.Background?.Equals("https://i.imgur.com/Duv8iVm.png") == true)
					_config.Background = "https://i.ibb.co/0JbMVnV/image.png";

				if (_config.Logo?.Equals("https://i.imgur.com/mhRO2AN.png") == true)
					_config.Logo = "https://i.ibb.co/GMMWyB4/image.png";

				if (_config.FirstCurrency?.Image?.Equals("https://i.imgur.com/qhPQblv.png") == true)
					_config.FirstCurrency.Image = "https://i.ibb.co/ThrdX3r/image.png";

				if (_config.SecondCurrency?.Image?.Equals("https://i.imgur.com/6aZllLI.png") == true)
					_config.SecondCurrency.Image = "https://i.ibb.co/gRbyTFW/image.png";

				if (_config.AdvanceAwardImg?.Equals("https://i.imgur.com/bUDH1sf.png") == true)
					_config.AdvanceAwardImg = "https://i.ibb.co/4jcqqhf/image.png";

				if (_config.CaseBG?.Equals("https://i.imgur.com/tlMMjqc.png") == true)
					_config.CaseBG = "https://i.ibb.co/kg8tR4K/image.png";

				_config.Cases?.ForEach(caseData =>
				{
					switch (caseData.Image)
					{
						case "https://i.imgur.com/tsPPUhg.png":
							caseData.Image = "https://i.ibb.co/M2bNGcM/tsPPUhg.png";
							break;
						case "https://i.imgur.com/3mtbqji.png":
							caseData.Image = "https://i.ibb.co/XLPg66N/3mtbqji.png";
							break;
						case "https://i.imgur.com/NvHk5Sw.png":
							caseData.Image = "https://i.ibb.co/FBpxMmK/NvHk5Sw.png";
							break;
						case "https://i.imgur.com/kZyZqy9.png":
							caseData.Image = "https://i.ibb.co/4Y4K8vm/kZyZqy9.png";
							break;
						case "https://i.imgur.com/5bur68a.png":
							caseData.Image = "https://i.ibb.co/9yMhCrR/5bur68a.png";
							break;
						case "https://i.imgur.com/9KIoJ2G.png":
							caseData.Image = "https://i.ibb.co/chhtKTK/9KIoJ2G.png";
							break;
					}
				});

				foreach (var check in _config.Missions)
					if (check.Value?.AdvanceAward?.Image?.Equals("https://i.imgur.com/IkEWGT8.png") == true)
						check.Value.AdvanceAward.Image = string.Empty;

				foreach (var check in _config.PrivateMissions)
					if (check.Value?.AdvanceAward?.Image?.Equals("https://i.imgur.com/IkEWGT8.png") == true)
						check.Value.AdvanceAward.Image = string.Empty;

				if (_config.Tabs?.Missions?.Image?.Equals("https://i.imgur.com/f2GN8m7.png") == true)
					_config.Tabs.Missions.Image = "https://i.ibb.co/C0jjqLB/f2GN8m7.png";

				if (_config.Tabs?.Cases?.Image?.Equals("https://i.imgur.com/2lMM2bS.png") == true)
					_config.Tabs.Cases.Image = "https://i.ibb.co/HtFh68D/2lMM2bS.png";

				if (_config.Tabs?.Inventory?.Image?.Equals("https://i.imgur.com/vvJe7KO.png") == true)
					_config.Tabs.Inventory.Image = "https://i.ibb.co/KLMwgHj/vvJe7KO.png";

				_config.Tabs?.CustomTabs?.ForEach(customTab =>
				{
					switch (customTab.Image)
					{
						case "https://i.imgur.com/C83Rprq.png":
							customTab.Image = "https://i.ibb.co/xDsVSXY/C83Rprq.png";
							break;
					}
				});
			}

		_config.Version = Version;
		PrintWarning("Config update completed!");
	}

	#endregion

	#region Data

	private MissionsData _missions;

	private void SaveMissions()
	{
		Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Missions", _missions);
	}

	private void SaveData(string userId, PlayerData data)
	{
		var serializeObject = JsonConvert.SerializeObject(data);
		if (serializeObject == null) return;

		PlayerDatabase?.Call("SetPlayerData", userId, _config.PlayerDatabase.Field, serializeObject);
	}

	private void LoadMissions()
	{
		try
		{
			_missions = Interface.Oxide.DataFileSystem.ReadObject<MissionsData>($"{Name}/Missions");
		}
		catch (Exception e)
		{
			PrintError(e.ToString());
		}

		if (_missions == null) _missions = new MissionsData();
	}

	private PlayerData LoadPlayerDatabaseData(string userId)
	{
		var success =
			PlayerDatabase?.Call<string>("GetPlayerDataRaw", userId, _config.PlayerDatabase.Field);
		if (string.IsNullOrEmpty(success))
		{
			var newData = new PlayerData();

			SaveData(userId, newData);
			return newData;
		}

		var data = JsonConvert.DeserializeObject<PlayerData>(success);
		if (data == null)
		{
			data = new PlayerData();
			SaveData(userId, data);
			return data;
		}

		return data;
	}

	private class MissionsData
	{
		[JsonProperty(PropertyName = "Date of last mission update")]
		public DateTime MissionsDate = _instance.epoch;

		[JsonProperty(PropertyName = "Missions", ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public List<int> Missions = new();
	}

	#endregion

	#region Hooks

	private void Init()
	{
		_instance = this;

		LoadMissions();

#if TESTING
		StopwatchWrapper.OnComplete = DebugMessage;
#endif
	}

	private void OnServerInitialized(bool initial)
	{
		CheckItems();
		LoadImages();
		RegisterPermissions();
		RegisterCommands();
		UpdateMissions();
	}

	private void OnServerSave()
	{
		timer.In(Random.Range(2f, 7f), PlayerData.Save);
	}

	private void Unload()
	{
		try
		{
			foreach (var player in BasePlayer.activePlayerList)
			{
				CuiHelper.DestroyUi(player, Layer);

				PlayerData.SaveAndUnload(player.UserIDString);
			}
		}
		finally
		{
			_config = null;

			_instance = null;
		}
	}

	private void OnPlayerDisconnected(BasePlayer player, string reason)
	{
		if (player == null) return;

		PlayerData.SaveAndUnload(player.UserIDString);

		_openedCaseItems.Remove(player.userID);
		_missionPlayers.Remove(player);
	}

	#region Wipe

	private void OnNewSave()
	{
		if (_config.Wipe.Missions) WipeMissions();
	}

	#endregion

	#region Missions

	#region Gather

	private void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player, bool isDeployed)
	{
		if (collectible == null || player == null || collectible.itemList == null) return;
		
		foreach (var item in collectible.itemList)
			if (item.itemDef != null)
				OnMissionsProgress(player, MissionType.Gather, 
					definition: item.itemDef, 
					itemAmount: (int)item.amount);
	}

	private void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
	{
		OnMissionsProgress(player, MissionType.Gather, item);
	}

	private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
	{
		OnMissionsProgress(player, MissionType.Gather, item);
	}

	#endregion

	#region Kill

	private void OnEntityDeath(BaseEntity entity, HitInfo info)
	{
		if (entity == null || entity is PatrolHelicopter) return;

		var player = info?.InitiatorPlayer;
		if (player == null) return;

		var suicide = player.Equals(entity);
		if (suicide) return;

		OnMissionsProgress(player, MissionType.Kill, entity: entity);
	}

	private readonly Dictionary<ulong, ulong> _heliAttackers = new();

	private void OnEntityTakeDamage(PatrolHelicopter heli, HitInfo info)
	{
		if (heli == null || info == null || heli.net == null || !heli.net.ID.IsValid) return;

		var player = info.InitiatorPlayer;
		if (player != null) _heliAttackers[heli.net.ID.Value] = player.userID;

		if (info.damageTypes.Total() >= heli.health)
		{
			if (player == null) player = BasePlayer.FindByID(GetLastAttacker(heli.net.ID.Value));

			if (player == null) return;

			OnMissionsProgress(player, MissionType.Kill, entity: heli);
		}
	}

	private ulong GetLastAttacker(ulong id)
	{
		return _heliAttackers.TryGetValue(id, out var attacker) ? attacker : 0;
	}

	#endregion

	#region Craft

	private void OnItemCraftFinished(ItemCraftTask task, Item item, ItemCrafter crafter)
	{
		if (crafter == null) return;

		OnMissionsProgress(crafter.owner, MissionType.Craft, item);
	}

	#endregion

	#region Loot

	#region Containers

	private readonly Dictionary<ulong, ulong> _lootedContainers = new();

	private void OnLootEntity(BasePlayer player, StorageContainer container)
	{
		if (player == null || container == null || container.net == null || !container.net.ID.IsValid) return;

		var lootContainer = container as LootContainer;
		if (lootContainer != null)
		{
			var netID = container.net.ID.Value;

			if (_lootedContainers.ContainsKey(netID)) return;

			_lootedContainers.Add(netID, player.userID);

			OnMissionsProgress(player, MissionType.LootCrate, entity: container);

			container.inventory?.itemList.ForEach(item => OnMissionsProgress(player, MissionType.Look, item));
			return;
		}

		if (_config.StorageLoot.Enabled)
		{
			if (!_config.StorageLoot.Containers.TryGetValue(container.ShortPrefabName, out var skins) ||
			    (!skins.Contains(0) && !skins.Contains(container.skinID))) return;

			var netID = container.net.ID.Value;

			if (_lootedContainers.ContainsKey(netID)) return;

			_lootedContainers.Add(netID, player.userID);

			container.inventory?.itemList.ForEach(item => OnMissionsProgress(player, MissionType.Look, item));
		}
	}

	#endregion

	#region Barrels

	private readonly List<ulong> _dropItems = new();

	private void OnContainerDropItems(ItemContainer container)
	{
		if (container == null) return;

		_dropItems.AddRange(container.itemList.Select(x => x.uid.Value));
	}

	private void OnItemPickup(Item item, BasePlayer player)
	{
		if (item == null || player == null) return;

		if (_dropItems.Contains(item.uid.Value)) return;
		{
			OnMissionsProgress(player, MissionType.Look, item);
			_dropItems.Remove(item.uid.Value);
		}
	}

	#endregion

	#endregion

	#region Fishing

	private void OnFishCatch(Item item, BaseFishingRod rod, BasePlayer player)
	{
		OnMissionsProgress(player, MissionType.Fishing, item);
	}

	#endregion

	#region Build

	private void OnEntitySpawned(BaseEntity entity)
	{
		if (entity == null || entity.OwnerID == 0) return;

		OnMissionsProgress(BasePlayer.FindByID(entity.OwnerID), MissionType.Build, entity: entity);
	}

	#endregion

	#region Grade

	private void OnPayForUpgrade(BasePlayer player, BuildingBlock block, ConstructionGrade gradeTarget)
	{
		if (player == null || block == null || gradeTarget == null || gradeTarget.gradeBase == null) return;

		OnMissionsProgress(player, MissionType.Upgrade, entity: block, grade: (int) gradeTarget.gradeBase.type);
	}

	private void OnBuildingUpgrade(BuildingBlock block, BuildingGrade.Enum grade, BasePlayer player)
	{
		if (player == null || block == null) return;

		OnMissionsProgress(player, MissionType.Upgrade, entity: block, grade: (int) grade);
	}

	#endregion

	#region Use Card

	private void OnCardSwipe(CardReader cardReader, Keycard card, BasePlayer player)
	{
		if (cardReader == null || card == null || player == null) return;

		var item = card.GetItem();
		if (item != null && card.accessLevel == cardReader.accessLevel && item.conditionNormalized > 0f)
			OnMissionsProgress(player, MissionType.Swipe, item);
	}

	#endregion

	#region Raidable Bases

	private enum RaidableMode
	{
		Disabled = -1,
		Easy = 0,
		Medium = 1,
		Hard = 2,
		Expert = 3,
		Nightmare = 4,
		Points = 8888,
		Random = 9999
	}

	private void OnRaidableBaseCompleted(Vector3 raidPos, int mode, bool allowPVP, string id, float spawnTime,
		float despawnTime, float loadTime, ulong ownerId, BasePlayer owner, List<BasePlayer> raiders,
		List<BasePlayer> intruders, List<BaseEntity> entities)
	{
#if TESTING
		SayDebug("[OnRaidableBaseCompleted] called");
#endif

		raiders?.ForEach(player =>
			OnMissionsProgress(player, MissionType.RaidableBases, mode: ((RaidableMode) mode).ToString()));
	}

	#endregion

	#region Recycle Item

	private readonly Dictionary<ulong, ulong> _recyclerToPlayer = new();

	private void OnRecyclerToggle(Recycler recycler, BasePlayer player)
	{
		if (recycler == null || recycler.net == null || !recycler.net.ID.IsValid) return;

		NextTick(() =>
		{
			if (recycler.IsOn())
				_recyclerToPlayer[recycler.net.ID.Value] = player.userID;
			else
				_recyclerToPlayer.Remove(recycler.net.ID.Value);
		});
	}

	private void OnItemRecycle(Item item, Recycler recycler)
	{
		ulong playerID;
		if (!_recyclerToPlayer.TryGetValue(recycler.net.ID.Value, out playerID)) return;

		var num2 = 1;
		if (item.amount > 1)
			num2 = Mathf.CeilToInt(Mathf.Min(item.amount, item.info.stackable * 0.1f));

		OnMissionsProgress(BasePlayer.FindByID(playerID), MissionType.RecycleItem, item, itemAmount: num2);
	}

	#endregion

	#region Hack Crate

	private void CanHackCrate(BasePlayer player, HackableLockedCrate crate)
	{
#if TESTING
		SayDebug($"[CanHackCrate] call with player={player}, crate={crate}");
#endif

		OnMissionsProgress(player, MissionType.HackCrate, entity: crate);
	}

	#endregion

	#region NPC Sold

	private void OnNpcGiveSoldItem(NPCVendingMachine machine, Item soldItem, BasePlayer buyer)
	{
		OnMissionsProgress(buyer, MissionType.PurchaseFromNpc, soldItem);
	}

	#endregion

	#region Events

	private void OnArcticBaseEventWinner(ulong winnerId)
	{
		OnMissionsProgress(BasePlayer.FindByID(winnerId), MissionType.ArcticBaseEvent);
	}

	private void OnGasStationEventWinner(ulong winnerId)
	{
		OnMissionsProgress(BasePlayer.FindByID(winnerId), MissionType.GasStationEvent);
	}

	private void OnSputnikEventWin(ulong winnerId)
	{
		OnMissionsProgress(BasePlayer.FindByID(winnerId), MissionType.SputnikEvent);
	}

	private void OnShipwreckEventWin(ulong winnerId)
	{
		OnMissionsProgress(BasePlayer.FindByID(winnerId), MissionType.ShipwreckEvent);
	}

	private void OnHarborEventWinner(ulong winnerId)
	{
		OnMissionsProgress(BasePlayer.FindByID(winnerId), MissionType.HarborEvent);
	}

	private void OnJunkyardEventWinner(ulong winnerId)
	{
		OnMissionsProgress(BasePlayer.FindByID(winnerId), MissionType.JunkyardEvent);
	}

	private void OnSatDishEventWinner(ulong winnerId)
	{
		OnMissionsProgress(BasePlayer.FindByID(winnerId), MissionType.SatDishEvent);
	}

	private void OnWaterEventWinner(ulong winnerId)
	{
		OnMissionsProgress(BasePlayer.FindByID(winnerId), MissionType.WaterEvent);
	}

	private void OnAirEventWinner(ulong winnerId)
	{
		OnMissionsProgress(BasePlayer.FindByID(winnerId), MissionType.AirEvent);
	}

	private void OnPowerPlantEventWinner(ulong winnerId)
	{
		OnMissionsProgress(BasePlayer.FindByID(winnerId), MissionType.PowerPlantEvent);
	}

	private void OnArmoredTrainEventWin(ulong winnerID)
	{
		OnMissionsProgress(BasePlayer.FindByID(winnerID), MissionType.ArmoredTrainEvent);
	}

	private void OnConvoyEventWin(ulong userId)
	{
		OnMissionsProgress(BasePlayer.FindByID(userId), MissionType.ConvoyEvent);
	}

	private void OnSurvivalArenaWin(BasePlayer player)
	{
		OnMissionsProgress(player, MissionType.SurvivalArena);
	}

	private void OnBossKilled(ScientistNPC boss, BasePlayer attacker)
	{
		if (boss == null || attacker == null) return;

		OnMissionsProgress(attacker, MissionType.KillBoss, entity: boss);
	}

	#endregion

	#endregion

	#region Image Library

#if !CARBON
	private void OnPluginLoaded(Plugin plugin)
	{
		if (plugin.Name == "ImageLibrary") _enabledImageLibrary = true;
	}

	private void OnPluginUnloaded(Plugin plugin)
	{
		if (plugin.Name == "ImageLibrary") _enabledImageLibrary = false;
	}
#endif

	#endregion

	#endregion

	#region Commands

	private void CmdChatOpen(IPlayer cov, string command, string[] args)
	{
		var player = cov?.Object as BasePlayer;
		if (player == null) return;

		if (_enabledImageLibrary == false)
		{
			SendNotify(player, NoILError, 1);

			BroadcastILNotInstalled();
			return;
		}

		if (!HasPermission(player, _config.Permission))
		{
			Reply(player, "NoPermission");
			return;
		}

		MainUI(player, true);
	}

	private void CmdPages(IPlayer cov, string command, string[] args)
	{
		var player = cov?.Object as BasePlayer;
		if (player == null) return;

		if (_enabledImageLibrary == false)
		{
			SendNotify(player, NoILError, 1);

			BroadcastILNotInstalled();
			return;
		}

		if (_config.MissionsCommands.Contains(command))
		{
			if (!_missionPlayers.Contains(player)) _missionPlayers.Add(player);

			MissionsUI(player, isFirst: true);
		}

		if (_config.CasesCommands.Contains(command)) CasesUI(player, true);

		if (_config.InventoryCommands.Contains(command))
		{
			_openedCaseItems.Remove(player.userID);

			InventoryUI(player, isFirst: true);
		}
	}

	[ConsoleCommand("battlepass.wipemissions")]
	private void CmdConsoleWipeData(ConsoleSystem.Arg arg)
	{
		if (!arg.IsAdmin) return;

		WipeMissions();

		PrintWarning("Missions data was wiped!");
	}

	private void CmdAddBalance(IPlayer player, string command, string[] args)
	{
		if (!player.IsAdmin) return;

		if (args.Length < 2)
		{
			player.Reply($"Use {command} [userid] [count]");
			return;
		}


		var nameOrIDOrIP = args[0];
		if (!nameOrIDOrIP.IsSteamId())
		{
			var target = BasePlayer.Find(nameOrIDOrIP);
			if (target == null)
			{
				player.Reply($"Player {nameOrIDOrIP} not found");
				return;
			}

			nameOrIDOrIP = target.UserIDString;
		}

		int amount;
		if (!int.TryParse(args[1], out amount))
		{
			player.Reply($"Use {command} [userid] [count]");
			return;
		}

		switch (command)
		{
			case "addfirstcurrency":
			{
				_config.FirstCurrency.AddBalance(nameOrIDOrIP, amount);
				break;
			}

			case "addsecondcurrency":
			{
				_config.SecondCurrency.AddBalance(nameOrIDOrIP, amount);
				break;
			}
		}
	}

	[ConsoleCommand("UI_Battlepass")]
	private void CmdConsole(ConsoleSystem.Arg arg)
	{
		var player = arg.Player();
		if (player == null) return;

		if (!arg.HasArgs()) return;
#if TESTING
		using (new StopwatchWrapper($"Main CMD with param '{arg.Args[0]}' took {{0}}ms."))
#endif
		{
			try
			{
				switch (arg.Args[0].ToLower())
				{
					case "closeui":
					{
						CuiHelper.DestroyUi(player, Layer);
						_openedCaseItems.Remove(player.userID);
						_missionPlayers.Remove(player);
						break;
					}
					case "main":
					{
						_missionPlayers.Remove(player);

						MainUI(player);
						break;
					}
					case "missions":
					{
						if (!_missionPlayers.Contains(player)) _missionPlayers.Add(player);

						MissionsUI(player);
						break;
					}
					case "cases":
					{
						CasesUI(player);
						break;
					}
					case "inventory":
					{
						_openedCaseItems.Remove(player.userID);

						InventoryUI(player);
						break;
					}
					case "showcase":
					{
						int caseID;
						if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out caseID) || caseID < 0 ||
						    _config.Cases.Count <= caseID)
							return;

						CaseUI(player, caseID);
						break;
					}
					case "tryopencase":
					{
						int caseID, count;
						bool isFreeCoin;
						if (!arg.HasArgs(4) || !int.TryParse(arg.Args[1], out caseID) ||
						    !bool.TryParse(arg.Args[2], out isFreeCoin) || !int.TryParse(arg.Args[3], out count))
							return;

						CaseModalUI(player, caseID, isFreeCoin, count);
						break;
					}
					case "opencase":
					{
						int caseID, count;
						bool isFirstCurrent;
						if (!arg.HasArgs(4) || !int.TryParse(arg.Args[1], out caseID) ||
						    !bool.TryParse(arg.Args[2], out isFirstCurrent) ||
						    !int.TryParse(arg.Args[3], out count))
							return;

						var _case = _config.Cases[caseID];
						if (_case == null) return;

						if (!HasPermission(player, _case.Permission))
						{
							Notification(player, "NoCasePermission");
							return;
						}

						var data = PlayerData.GetOrCreate(player.UserIDString);
						if (data == null) return;

						var cost = (isFirstCurrent ? _case.FCost : _case.PCost) * count;

						var remove = isFirstCurrent
							? _config.FirstCurrency.RemoveBalance(player, cost)
							: _config.SecondCurrency.RemoveBalance(player, cost);

						if (!remove)
						{
							Notification(player, "Not enough");
							return;
						}

						var items = GetRandom(_case, count);

						Log("opencase", "opencase", player.displayName, player.UserIDString, _case.DisplayName,
							string.Join(", ",
								items.Select(x =>
									$"item (title: {x.Title}, type: {x.Type.ToString()}, shortname: {x.Shortname}, amount: {x.Amount}, skin: {x.Skin}, command: {x.Command}, plugin amount: {x.PluginAward.Amount}")));

						if (_config.CaseItemsToPlayer)
							items.ForEach(item => item.GetItem(player));
						else
							items.ForEach(item => data.Items.Add(item.ID));

						RefreshBalance(player);

						_openedCaseItems[player.userID] = items;

						OpenCasesUI(player);
						break;
					}

					case "setvalue":
					{
						int caseID, page, count;
						bool isFreeCoin;
						if (!arg.HasArgs(5) ||
						    !int.TryParse(arg.Args[1], out caseID) ||
						    !bool.TryParse(arg.Args[2], out isFreeCoin) ||
						    !int.TryParse(arg.Args[3], out page) ||
						    !int.TryParse(arg.Args[4], out count))
							return;

						count = Mathf.Max(Mathf.Min(count, 5), 1);

						CaseUI(player, caseID, isFreeCoin, count, page);
						break;
					}

					case "changepage":
					{
						int page;
						if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out page)) return;

						OpenCasesUI(player, page);
						break;
					}
					case "invpage":
					{
						int page;
						if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out page)) return;

						InventoryUI(player, page);
						break;
					}
					case "giveitem":
					{
						int index, page;
						if (!arg.HasArgs(3) || !int.TryParse(arg.Args[1], out index) ||
						    !int.TryParse(arg.Args[2], out page))
							return;

						if (_config.WipeCooldown.Inventory > 0)
						{
							var seconds = SecondsFromWipe();
							if (seconds < _config.WipeCooldown.Inventory)
							{
								SendNotify(player, "WipeBlock.Inventory", 1, FormatShortTime(seconds));
								return;
							}
						}

						if (_config.BlockNoEscape && NoEscape != null)
						{
							var success = NoEscape?.Call("IsBlocked", player);
							if (success is bool && (bool) success)
							{
								SendNotify(player, "NoEscape.Inventory", 1);
								return;
							}
						}

						var data = PlayerData.GetOrCreate(player.UserIDString);
						if (data == null) return;

						var items = data.GetItems();

						var itemId = items[index];

						ItemConf item;
						if (!_itemById.TryGetValue(itemId, out item) || item == null) return;

						item.GetItem(player);

						data.Items.RemoveAt(index);

						Log("getitem", "getitem", player.displayName, player.UserIDString,
							$"item (title: {item.Title}, type: {item.Type.ToString()}, shortname: {item.Shortname}, amount: {item.Amount}, skin: {item.Skin}, command: {item.Command}, plugin amount: {item.PluginAward?.Amount ?? 0}");

						InventoryUI(player, page);
						break;
					}
					case "showaward":
					{
						if (!arg.HasArgs(3) || !int.TryParse(arg.Args[1], out var missionId) ||
						    !int.TryParse(arg.Args[2], out var ySwitch))
							return;

						ShowAward(player, _config.Missions[missionId], ySwitch);
						break;
					}
					case "mispage":
					{
						if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out var page)) return;

						MissionsUI(player, page);
						break;
					}

					case "sendcmd":
					{
						bool close;
						if (!arg.HasArgs(3) || !bool.TryParse(arg.Args[1], out close)) return;

						var command = string.Join(" ", arg.Args.Skip(2));
						if (string.IsNullOrEmpty(command)) return;

						if (close)
						{
							_openedCaseItems.Remove(player.userID);
							_missionPlayers.Remove(player);
						}

						foreach (var check in command.Split('|'))
							if (check.Contains("chat.say"))
							{
								var args = check.Split(' ');
								player.SendConsoleCommand(
									$"{args[0]}  \" {string.Join(" ", args.ToList().GetRange(1, args.Length - 1))}\" 0");
							}
							else
							{
								player.SendConsoleCommand(check);
							}

						break;
					}
				}
			}
			catch (Exception e)
			{
				PrintError(
					$"In the \"UI_Battlepass\" command at the parameter \"{arg.Args[0]}\" with the arguments: {string.Join(", ", arg.Args.Skip(1).ToArray())}. Error: {e.Message}");
				throw;
			}
		}
	}

	#endregion

	#region Interface

	private void MainUI(BasePlayer player, bool first = false)
	{
		var container = new CuiElementContainer();

		#region Background

		if (first)
		{
			container.Add(new CuiElement
			{
				Parent = "Overlay",
				Name = Layer,
				DestroyUi = Layer,
				Components =
				{
					new CuiRawImageComponent
					{
						Png = GetImage(_config.Background),
						Color = _config.UI.Colors.BackgroundColor.Get
					},
					new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"},
					new CuiNeedsCursorComponent()
				}
			});

			#region Header

			HeaderUi(ref container, player);

			#endregion
		}

		#endregion

		#region Main

		container.Add(
			new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"}, Image = {Color = "0 0 0 0"}
			}, Layer, Layer + ".Main", Layer + ".Main");

		#region Tabs

		var totalWidth = 0f;
		var totalCount = 0;

		var tabs = _config.Tabs.Tabs.FindAll(tab => tab.Enabled);

		tabs.ForEach(tab =>
		{
			totalWidth += _config.Tabs.Missions.Width;
			totalCount++;
		});

		var xSwitch = -(totalWidth + (totalCount - 1) * _config.Tabs.Margin) / 2f;

		tabs.ForEach(tab =>
		{
			container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
						OffsetMin = $"{xSwitch} {-tab.Height / 2f}",
						OffsetMax = $"{xSwitch + tab.Width} {tab.Height / 2f}"
					},
					Image = {Color = "0 0 0 0"}
				}, Layer + ".Main", Layer + $".Tabs.{xSwitch}");

			container.Add(new CuiElement
			{
				Parent = Layer + $".Tabs.{xSwitch}",
				Components =
				{
					new CuiRawImageComponent
					{
						Png = GetImage(tab.Image)
					},
					new CuiRectTransformComponent
					{
						AnchorMin = tab.ImagePosition.AnchorMin,
						AnchorMax = tab.ImagePosition.AnchorMax,
						OffsetMin = tab.ImagePosition.OffsetMin,
						OffsetMax = tab.ImagePosition.OffsetMax
					}
				}
			});

			if (tab.Button != null)
				container.Add(
					new CuiButton
					{
						RectTransform =
						{
							AnchorMin = _config.Tabs.Missions.Button.AnchorMin,
							AnchorMax = _config.Tabs.Missions.Button.AnchorMax,
							OffsetMin = _config.Tabs.Missions.Button.OffsetMin,
							OffsetMax = _config.Tabs.Missions.Button.OffsetMax
						},
						Button =
						{
							Command = $"UI_Battlepass sendcmd {tab.CloseMenu} {tab.Command}",
							Color = "0 0 0 0",
							Close = tab.CloseMenu ? Layer : string.Empty
						},
						Text =
						{
							Text = Msg($"{tab.LangKey}", player.UserIDString),
							Align = tab.Button.Align,
							FontSize = tab.Button.FontSize,
							Font = tab.Button.Font,
							Color = tab.Button.Color ?? string.Empty,
							FadeIn = tab.Button.FadeIn
						}
					}, Layer + $".Tabs.{xSwitch}", Layer + $".Tabs.{xSwitch}.Btn");

			Outline(ref container, Layer + $".Tabs.{xSwitch}.Btn");

			xSwitch += tab.Width + _config.Tabs.Margin;
		});

		#endregion

		#region Leave

		container.Add(
			new CuiButton
			{
				RectTransform =
				{
					AnchorMin = _config.UI.Header.Exit.AnchorMin,
					AnchorMax = _config.UI.Header.Exit.AnchorMax,
					OffsetMin = _config.UI.Header.Exit.OffsetMin,
					OffsetMax = _config.UI.Header.Exit.OffsetMax
				},
				Button = {Command = "UI_Battlepass closeui", Color = "0 0 0 0"},
				Text =
				{
					Text = Msg("Exit", player.UserIDString),
					Align = _config.UI.Header.Exit.Align,
					FontSize = _config.UI.Header.Exit.FontSize,
					Font = _config.UI.Header.Exit.Font,
					Color = _config.UI.Header.Exit.Color,
					FadeIn = _config.UI.Header.Exit.FadeIn
				}
			}, Layer + ".Main");

		#endregion

		#endregion

		CuiHelper.AddUi(player, container);
	}

	private void CasesUI(BasePlayer player, bool isFirst = false)
	{
		var container = new CuiElementContainer();

		#region Background

		if (isFirst)
		{
			container.Add(new CuiElement
			{
				Parent = "Overlay",
				Name = Layer,
				DestroyUi = Layer,
				Components =
				{
					new CuiRawImageComponent
					{
						Png = GetImage(_config.Background),
						Color = _config.UI.Colors.BackgroundColor.Get
					},
					new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"},
					new CuiNeedsCursorComponent()
				}
			});

			#region Header

			HeaderUi(ref container, player);

			#endregion
		}

		#endregion

		#region Main

		container.Add(
			new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"}, Image = {Color = "0 0 0 0"}
			}, Layer, Layer + ".Main", Layer + ".Main");

		#region Title

		container.Add(
			new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = _config.UI.Cases.Title.AnchorMin,
					AnchorMax = _config.UI.Cases.Title.AnchorMax,
					OffsetMin = _config.UI.Cases.Title.OffsetMin,
					OffsetMax = _config.UI.Cases.Title.OffsetMax
				},
				Text =
				{
					Text = Msg("Cases title", player.UserIDString),
					Align = _config.UI.Cases.Title.Align,
					FontSize = _config.UI.Cases.Title.FontSize,
					Font = _config.UI.Cases.Title.Font,
					Color = _config.UI.Cases.Title.Color,
					FadeIn = _config.UI.Cases.Title.FadeIn
				}
			}, Layer + ".Main");

		#endregion

		#region Back

		container.Add(
			new CuiButton
			{
				RectTransform =
				{
					AnchorMin = _config.UI.Cases.Back.AnchorMin,
					AnchorMax = _config.UI.Cases.Back.AnchorMax,
					OffsetMin = _config.UI.Cases.Back.OffsetMin,
					OffsetMax = _config.UI.Cases.Back.OffsetMax
				},
				Button = {Command = "UI_Battlepass main", Color = "0 0 0 0"},
				Text =
				{
					Text = Msg("Back", player.UserIDString),
					Align = _config.UI.Cases.Back.Align,
					FontSize = _config.UI.Cases.Back.FontSize,
					Font = _config.UI.Cases.Back.Font,
					Color = _config.UI.Cases.Back.Color,
					FadeIn = _config.UI.Cases.Back.FadeIn
				}
			}, Layer + ".Main");

		#endregion

		#region Cases

		var xSwitch = -_config.UI.Cases.CaseUpIdent;
		var ySwitch = _config.UI.Cases.CaseLeftIdent;

		for (var i = 1; i <= _config.Cases.Count; i++)
		{
			var caseConf = _config.Cases[i - 1];

			container.Add(
				new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = $"{xSwitch} {ySwitch}",
						OffsetMax =
							$"{xSwitch + _config.UI.Cases.CaseWidth} {ySwitch + _config.UI.Cases.CaseHeight}"
					},
					Image = {Color = "0 0 0 0"}
				}, Layer + ".Main", Layer + $".Case.{i}");

			container.Add(new CuiElement
			{
				Parent = Layer + $".Case.{i}",
				Components =
				{
					new CuiRawImageComponent
					{
						Png = GetImage(_config.CaseBG)
					},
					new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
				}
			});

			container.Add(
				new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -40", OffsetMax = "0 -5"
					},
					Text = {Text = caseConf.DisplayName, Align = TextAnchor.MiddleCenter, FontSize = 12}
				}, Layer + $".Case.{i}");

			container.Add(new CuiElement
			{
				Parent = Layer + $".Case.{i}",
				Components =
				{
					new CuiRawImageComponent
						{Png = GetImage(caseConf.Image)},
					new CuiRectTransformComponent
					{
						AnchorMin = _config.UI.Cases.CaseImage.AnchorMin,
						AnchorMax = _config.UI.Cases.CaseImage.AnchorMax,
						OffsetMin = _config.UI.Cases.CaseImage.OffsetMin,
						OffsetMax = _config.UI.Cases.CaseImage.OffsetMax
					}
				}
			});

			Outline(ref container, Layer + $".Case.{i}");

			container.Add(
				new CuiButton
				{
					RectTransform =
					{
						AnchorMin = _config.UI.Cases.CaseShow.AnchorMin,
						AnchorMax = _config.UI.Cases.CaseShow.AnchorMax,
						OffsetMin = _config.UI.Cases.CaseShow.OffsetMin,
						OffsetMax = _config.UI.Cases.CaseShow.OffsetMax
					},
					Button = {Command = $"UI_Battlepass showcase {i - 1}", Color = "0 0 0 0"},
					Text =
					{
						Text = Msg("Cases show", player.UserIDString),
						Align = _config.UI.Cases.CaseShow.Align,
						FontSize = _config.UI.Cases.CaseShow.FontSize,
						Font = _config.UI.Cases.CaseShow.Font,
						Color = _config.UI.Cases.CaseShow.Color,
						FadeIn = _config.UI.Cases.CaseShow.FadeIn
					}
				}, Layer + $".Case.{i}", Layer + $".Case.{i}.Btn");

			Outline(ref container, Layer + $".Case.{i}.Btn");

			if (i % 3 == 0)
			{
				ySwitch = ySwitch - _config.UI.Cases.CaseHeight - _config.UI.Cases.CaseMargin;
				xSwitch = -_config.UI.Cases.CaseUpIdent;
			}
			else
			{
				xSwitch += _config.UI.Cases.CaseWidth + _config.UI.Cases.CaseMargin;
			}
		}

		#endregion

		#endregion

		CuiHelper.AddUi(player, container);
	}

	private void CaseUI(BasePlayer player, int caseId, bool isFirstCurrent = true, int count = 1, int page = 0)
	{
		var Case = _config.Cases[caseId];

		var container = new CuiElementContainer();

		#region Main

		container.Add(
			new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"}, Image = {Color = "0 0 0 0"}
			}, Layer, Layer + ".Main", Layer + ".Main");

		#region Title

		container.Add(
			new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = _config.UI.CaseInfo.Title.AnchorMin,
					AnchorMax = _config.UI.CaseInfo.Title.AnchorMax,
					OffsetMin = _config.UI.CaseInfo.Title.OffsetMin,
					OffsetMax = _config.UI.CaseInfo.Title.OffsetMax
				},
				Text =
				{
					Text = Msg("Cases title", player.UserIDString),
					Align = _config.UI.CaseInfo.Title.Align,
					FontSize = _config.UI.CaseInfo.Title.FontSize,
					Font = _config.UI.CaseInfo.Title.Font,
					Color = _config.UI.CaseInfo.Title.Color,
					FadeIn = _config.UI.CaseInfo.Title.FadeIn
				}
			}, Layer + ".Main");

		#endregion

		#region Back

		container.Add(
			new CuiButton
			{
				RectTransform =
				{
					AnchorMin = _config.UI.CaseInfo.Back.AnchorMin,
					AnchorMax = _config.UI.CaseInfo.Back.AnchorMax,
					OffsetMin = _config.UI.CaseInfo.Back.OffsetMin,
					OffsetMax = _config.UI.CaseInfo.Back.OffsetMax
				},
				Button = {Command = "UI_Battlepass cases", Color = "0 0 0 0"},
				Text =
				{
					Text = Msg("Back", player.UserIDString),
					Align = _config.UI.CaseInfo.Back.Align,
					FontSize = _config.UI.CaseInfo.Back.FontSize,
					Font = _config.UI.CaseInfo.Back.Font,
					Color = _config.UI.CaseInfo.Back.Color,
					FadeIn = _config.UI.CaseInfo.Back.FadeIn
				}
			}, Layer + ".Main");

		#endregion

		#region Case

		container.Add(
			new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = _config.UI.CaseInfo.CaseTitle.AnchorMin,
					AnchorMax = _config.UI.CaseInfo.CaseTitle.AnchorMax,
					OffsetMin = _config.UI.CaseInfo.CaseTitle.OffsetMin,
					OffsetMax = _config.UI.CaseInfo.CaseTitle.OffsetMax
				},
				Text =
				{
					Text = $"<b>{Case.DisplayName}</b>",
					Align = _config.UI.CaseInfo.CaseTitle.Align,
					FontSize = _config.UI.CaseInfo.CaseTitle.FontSize,
					Font = _config.UI.CaseInfo.CaseTitle.Font,
					Color = _config.UI.CaseInfo.CaseTitle.Color,
					FadeIn = _config.UI.CaseInfo.CaseTitle.FadeIn
				}
			}, Layer + ".Main");

		container.Add(new CuiElement
		{
			Parent = Layer + ".Main",
			Components =
			{
				new CuiRawImageComponent {Png = GetImage(Case.Image)},
				new CuiRectTransformComponent
				{
					AnchorMin = _config.UI.CaseInfo.CaseImage.AnchorMin,
					AnchorMax = _config.UI.CaseInfo.CaseImage.AnchorMax,
					OffsetMin = _config.UI.CaseInfo.CaseImage.OffsetMin,
					OffsetMax = _config.UI.CaseInfo.CaseImage.OffsetMax
				}
			}
		});

		container.Add(
			new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = _config.UI.CaseInfo.BuyAmount.AnchorMin,
					AnchorMax = _config.UI.CaseInfo.BuyAmount.AnchorMax,
					OffsetMin = _config.UI.CaseInfo.BuyAmount.OffsetMin,
					OffsetMax = _config.UI.CaseInfo.BuyAmount.OffsetMax
				},
				Text =
				{
					Text = Msg("Case pick current", player.UserIDString),
					Align = _config.UI.CaseInfo.BuyAmount.Align,
					FontSize = _config.UI.CaseInfo.BuyAmount.FontSize,
					Font = _config.UI.CaseInfo.BuyAmount.Font,
					Color = _config.UI.CaseInfo.BuyAmount.Color,
					FadeIn = _config.UI.CaseInfo.BuyAmount.FadeIn
				}
			}, Layer + ".Main");

		#region FirstCurrent

		container.Add(
			new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = _config.UI.CaseInfo.FirstCurrency.AnchorMin,
					AnchorMax = _config.UI.CaseInfo.FirstCurrency.AnchorMax,
					OffsetMin = _config.UI.CaseInfo.FirstCurrency.OffsetMin,
					OffsetMax = _config.UI.CaseInfo.FirstCurrency.OffsetMax
				},
				Image = {Color = "0 0 0 0"}
			}, Layer + ".Main", Layer + ".FirstCurrent");

		Outline(ref container, Layer + ".FirstCurrent", "1 1 1 0.2");

		if (isFirstCurrent)
		{
			container.Add(
				new CuiPanel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"}, Image = {Color = "1 1 1 0.2"}
				}, Layer + ".FirstCurrent");

			container.Add(
				new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -5", OffsetMax = "2 0"
					},
					Image = {Color = _config.UI.Colors.Color3.Get}
				}, Layer + ".FirstCurrent");

			container.Add(
				new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -2", OffsetMax = "5 0"
					},
					Image = {Color = _config.UI.Colors.Color3.Get}
				}, Layer + ".FirstCurrent");
		}

		container.Add(new CuiElement
		{
			Parent = Layer + ".FirstCurrent",
			Components =
			{
				new CuiRawImageComponent
				{
					Png = GetImage(_config.FirstCurrency.Image)
				},
				new CuiRectTransformComponent
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "5 -10", OffsetMax = "25 10"
				}
			}
		});

		container.Add(
			new CuiLabel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "0.5 1"},
				Text =
				{
					Text = $"{Case.FCost * count}",
					Align = TextAnchor.MiddleRight,
					FontSize = 12,
					Font = "robotocondensed-regular.ttf"
				}
			}, Layer + ".FirstCurrent");

		container.Add(
			new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Button = {Command = $"UI_Battlepass setvalue {caseId} {true} {page} {count} ", Color = "0 0 0 0"},
				Text = {Text = ""}
			}, Layer + ".FirstCurrent");

		#endregion

		#region SecondCurrent

		if (_config.useSecondCur && HasPermission(player, _config.SecondCurrency.Permission))
		{
			container.Add(
				new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = _config.UI.CaseInfo.SecondCurrency.AnchorMin,
						AnchorMax = _config.UI.CaseInfo.SecondCurrency.AnchorMax,
						OffsetMin = _config.UI.CaseInfo.SecondCurrency.OffsetMin,
						OffsetMax = _config.UI.CaseInfo.SecondCurrency.OffsetMax
					},
					Image = {Color = "0 0 0 0"}
				}, Layer + ".Main", Layer + ".SecondCurrent");

			Outline(ref container, Layer + ".SecondCurrent", "1 1 1 0.2");

			if (!isFirstCurrent)
			{
				container.Add(
					new CuiPanel
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Image = {Color = "1 1 1 0.2"}
					}, Layer + ".SecondCurrent");

				container.Add(
					new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -5", OffsetMax = "2 0"
						},
						Image = {Color = _config.UI.Colors.Color3.Get}
					}, Layer + ".SecondCurrent");

				container.Add(
					new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -2", OffsetMax = "5 0"
						},
						Image = {Color = _config.UI.Colors.Color3.Get}
					}, Layer + ".SecondCurrent");
			}

			container.Add(new CuiElement
			{
				Parent = Layer + ".SecondCurrent",
				Components =
				{
					new CuiRawImageComponent
					{
						Png = GetImage(_config.SecondCurrency.Image)
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = "5 -10",
						OffsetMax = "25 10"
					}
				}
			});

			container.Add(
				new CuiLabel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "0.5 1"},
					Text =
					{
						Text = $"{Case.PCost * count}",
						Align = TextAnchor.MiddleRight,
						FontSize = 12,
						Font = "robotocondensed-regular.ttf"
					}
				}, Layer + ".SecondCurrent");

			container.Add(
				new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Button =
					{
						Command = $"UI_Battlepass setvalue {caseId} {false} {page} {count}", Color = "0 0 0 0"
					},
					Text = {Text = ""}
				}, Layer + ".SecondCurrent");
		}

		#endregion

		#region Items

		var title = Msg("Case awards", player.UserIDString);

		container.Add(
			new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = _config.UI.CaseInfo.AwardsTitle.AnchorMin,
					AnchorMax = _config.UI.CaseInfo.AwardsTitle.AnchorMax,
					OffsetMin = _config.UI.CaseInfo.AwardsTitle.OffsetMin,
					OffsetMax = _config.UI.CaseInfo.AwardsTitle.OffsetMax
				},
				Text =
				{
					Text = title,
					Align = _config.UI.CaseInfo.AwardsTitle.Align,
					FontSize = _config.UI.CaseInfo.AwardsTitle.FontSize,
					Font = _config.UI.CaseInfo.AwardsTitle.Font,
					Color = _config.UI.CaseInfo.AwardsTitle.Color,
					FadeIn = _config.UI.CaseInfo.AwardsTitle.FadeIn
				}
			}, Layer + ".Main");

		container.Add(
			new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = _config.UI.CaseInfo.AwardsBackground.AnchorMin,
					AnchorMax = _config.UI.CaseInfo.AwardsBackground.AnchorMax,
					OffsetMin = _config.UI.CaseInfo.AwardsBackground.OffsetMin,
					OffsetMax = _config.UI.CaseInfo.AwardsBackground.OffsetMax
				},
				Image = {Color = "0 0 0 0"}
			}, Layer + ".Main", Layer + ".Items");

		var ySwitch = 0;
		var index = 0;

		foreach (var caseItem in Case.Items
			         .Skip(page * _config.UI.CaseInfo.AwardsOnPage)
			         .Take(_config.UI.CaseInfo.AwardsOnPage))
		{
			container.Add(
				new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 1",
						AnchorMax = "1 1",
						OffsetMin = $"0 {ySwitch - 15}",
						OffsetMax = $"0 {ySwitch}"
					},
					Image = {Color = "0 0 0 0"}
				}, Layer + ".Items", Layer + $".Item.{index}");

			container.Add(
				new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = "0 0", OffsetMax = "2 0"
					},
					Image = {Color = _config.UI.Colors.Color3.Get}
				}, Layer + $".Item.{index}");

			container.Add(
				new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "10 0", OffsetMax = "0 0"
					},
					Text =
					{
						Text = caseItem.Title,
						Align = TextAnchor.MiddleLeft,
						FontSize = 12,
						Font = "robotocondensed-regular.ttf"
					}
				}, Layer + $".Item.{index}");

			ySwitch -= 20;
			index++;
		}

		#endregion

		#region Buttons

		container.Add(
			new CuiButton
			{
				RectTransform =
				{
					AnchorMin = _config.UI.CaseInfo.OpenButton.AnchorMin,
					AnchorMax = _config.UI.CaseInfo.OpenButton.AnchorMax,
					OffsetMin = _config.UI.CaseInfo.OpenButton.OffsetMin,
					OffsetMax = _config.UI.CaseInfo.OpenButton.OffsetMax
				},
				Button =
				{
					Command = $"UI_Battlepass tryopencase {caseId} {isFirstCurrent} {count}",
					Color = "0 0 0 0"
				},
				Text =
				{
					Text = Msg("Case open", player.UserIDString),
					Align = _config.UI.CaseInfo.OpenButton.Align,
					FontSize = _config.UI.CaseInfo.OpenButton.FontSize,
					Font = _config.UI.CaseInfo.OpenButton.Font,
					Color = _config.UI.CaseInfo.OpenButton.Color,
					FadeIn = _config.UI.CaseInfo.OpenButton.FadeIn
				}
			}, Layer + ".Main", Layer + ".Btn.Open.Case");

		Outline(ref container, Layer + ".Btn.Open.Case", "1 1 1 1", "1");

		container.Add(
			new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = _config.UI.CaseInfo.EnterAmount.AnchorMin,
					AnchorMax = _config.UI.CaseInfo.EnterAmount.AnchorMax,
					OffsetMin = _config.UI.CaseInfo.EnterAmount.OffsetMin,
					OffsetMax = _config.UI.CaseInfo.EnterAmount.OffsetMax
				},
				Image = {Color = "0 0 0 0"}
			}, Layer + ".Main", Layer + ".InputLayer");

		Outline(ref container, Layer + ".InputLayer", _config.UI.CaseInfo.OpenButtonOutline.Color.Get,
			_config.UI.CaseInfo.OpenButtonOutline.Size);

		container.Add(new CuiElement
		{
			Parent = Layer + ".InputLayer",
			Name = Layer + ".InputLayer.Value",
			Components =
			{
				new CuiInputFieldComponent
				{
					FontSize = 12,
					Align = TextAnchor.MiddleCenter,
					Command = $"UI_Battlepass setvalue {caseId} {isFirstCurrent} {page} ",
					Text = $"{count}",
					Color = "1 1 1 1",
					CharsLimit = 1,
					NeedsKeyboard = true
				},
				new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
			}
		});

		#endregion

		#endregion

		#region Pages

		var coordX = 70f + (title.Length + 1) * _config.UI.CaseInfo.AwardsTitle.FontSize * 0.35f;

		container.Add(new CuiButton
		{
			RectTransform =
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = $"{coordX} 215",
				OffsetMax = $"{coordX + 20} 235"
			},
			Text =
			{
				Text = Msg(player, "BackBtn"),
				Align = TextAnchor.MiddleCenter,
				FontSize = 14,
				Font = "robotocondensed-regular.ttf"
			},
			Button =
			{
				Color = _config.UI.Colors.Color5.Get,
				Command = page != 0 ? $"UI_Battlepass setvalue {caseId} {isFirstCurrent} {page - 1} {count}" : ""
			}
		}, Layer + ".Main");

		container.Add(new CuiButton
		{
			RectTransform =
			{
				AnchorMin = "0.5 0.5",
				AnchorMax = "0.5 0.5",
				OffsetMin = $"{coordX + 25} 215",
				OffsetMax = $"{coordX + 45} 235"
			},
			Text =
			{
				Text = Msg(player, "NextBtn"),
				Align = TextAnchor.MiddleCenter,
				FontSize = 14,
				Font = "robotocondensed-regular.ttf"
			},
			Button =
			{
				Color = _config.UI.Colors.Color5.Get,
				Command = Case.Items.Count > (page + 1) * _config.UI.CaseInfo.AwardsOnPage
					? $"UI_Battlepass setvalue {caseId} {isFirstCurrent} {page + 1} {count}"
					: ""
			}
		}, Layer + ".Main");

		#endregion

		#endregion

		CuiHelper.AddUi(player, container);
	}

	private void CaseModalUI(BasePlayer player, int caseId, bool isFreeCoin, int count)
	{
		var Case = _config.Cases[caseId];

		var container = new CuiElementContainer();

		container.Add(
			new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"}, Image = {Color = "0 0 0 0.6"}
			}, "Overlay", ModalLayer, ModalLayer);

		container.Add(
			new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5",
					AnchorMax = "0.5 0.5",
					OffsetMin = "-300 -200",
					OffsetMax = "300 200"
				},
				Image = {Color = "0 0 0 1"}
			}, ModalLayer, ModalLayer + ".Main");

		container.Add(
			new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0.5", AnchorMax = "1 0.5", OffsetMin = "50 -50", OffsetMax = "-50 170"
				},
				Text =
				{
					Text = Msg("Modal tryopen", player.UserIDString, Case.DisplayName),
					Align = TextAnchor.MiddleCenter,
					FontSize = 30
				}
			}, ModalLayer + ".Main");

		container.Add(
			new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-30 -30", OffsetMax = "0 0"
				},
				Button = {Close = ModalLayer, Color = "0 0 0 0"},
				Text = {Text = "X", Align = TextAnchor.MiddleCenter, FontSize = 24}
			}, ModalLayer + ".Main");

		container.Add(
			new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-150 80", OffsetMax = "150 110"
				},
				Button =
				{
					Command = $"UI_Battlepass opencase {caseId} {isFreeCoin} {count}",
					Close = ModalLayer,
					Color = "0 0 0 0"
				},
				Text =
				{
					Text = Msg("Modal accept", player.UserIDString),
					Align = TextAnchor.MiddleCenter,
					FontSize = 18
				}
			}, ModalLayer + ".Main", ModalLayer + ".Main.Accept");

		Outline(ref container, ModalLayer + ".Main.Accept");

		CuiHelper.AddUi(player, container);
	}

	private void RefreshBalance(BasePlayer player)
	{
		var container = new CuiElementContainer();

		container.Add(
			new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "0 -10", OffsetMax = "225 10"
				},
				Text =
				{
					Text = $"{_config.FirstCurrency.ShowBalance(player)}",
					FontSize = 12,
					Align = TextAnchor.MiddleRight,
					Font = "robotocondensed-regular.ttf"
				}
			}, Layer + ".Header", Layer + ".FreeCoins", Layer + ".FreeCoins");

		if (_config.useSecondCur && HasPermission(player, _config.SecondCurrency.Permission))
			container.Add(
				new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = "0 -10",
						OffsetMax = "310 10"
					},
					Text =
					{
						Text = $"{_config.SecondCurrency.ShowBalance(player)}",
						FontSize = 12,
						Align = TextAnchor.MiddleRight,
						Font = "robotocondensed-regular.ttf"
					}
				}, Layer + ".Header", Layer + ".PaidCoins", Layer + ".PaidCoins");

		CuiHelper.AddUi(player, container);
	}

	private void OpenCasesUI(BasePlayer player, int page = 0)
	{
		List<ItemConf> items;
		if (!_openedCaseItems.TryGetValue(player.userID, out items))
			return;

		var item = items?[page];
		if (item == null) return;

		var container = new CuiElementContainer();

		#region Main

		container.Add(
			new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"}, Image = {Color = "0 0 0 0"}
			}, Layer, Layer + ".Main", Layer + ".Main");

		#region Title

		container.Add(
			new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5",
					AnchorMax = "0.5 0.5",
					OffsetMin = "-320 250",
					OffsetMax = "0 290"
				},
				Text =
				{
					Text = Msg("Your award", player.UserIDString),
					Align = TextAnchor.MiddleLeft,
					FontSize = 26
				}
			}, Layer + ".Main");

		#endregion

		#region Close

		container.Add(
			new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5",
					AnchorMax = "0.5 0.5",
					OffsetMin = "200 295",
					OffsetMax = "320 315"
				},
				Button = {Command = "UI_Battlepass cases", Color = "0 0 0 0"},
				Text =
				{
					Text = Msg("Back", player.UserIDString), Align = TextAnchor.MiddleRight, FontSize = 16
				}
			}, Layer + ".Main");

		#endregion

		#region Selected item

		container.Add(
			new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5",
					AnchorMax = "0.5 0.5",
					OffsetMin = "-200 160",
					OffsetMax = "200 230"
				},
				Text = {Text = $"{item.Title}", Align = TextAnchor.UpperCenter, FontSize = 16}
			}, Layer + ".Main");

		if (!string.IsNullOrEmpty(item.Image))
			container.Add(new CuiElement
			{
				Parent = Layer + ".Main",
				Components =
				{
					new CuiRawImageComponent
					{
						Png = GetImage(item.Image)
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = "-95 -50",
						OffsetMax = "95 140"
					}
				}
			});
		else
			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5",
					AnchorMax = "0.5 0.5",
					OffsetMin = "-95 -50",
					OffsetMax = "95 140"
				},
				Image =
				{
					ItemId = item.itemId,
					SkinId = item.Skin
				}
			}, Layer + ".Main");

		#endregion

		#region Items

		var xSwitch = -(items.Count * 140 + (items.Count - 1) * 10) / 2;
		for (var i = 0; i < items.Count; i++)
		{
			var itemCase = items[i];

			container.Add(
				new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = $"{xSwitch} -180",
						OffsetMax = $"{xSwitch + 140} -100"
					},
					Image = {Color = "0 0 0 0"}
				}, Layer + ".Main", Layer + $".Item.{i}");

			Outline(ref container, Layer + $".Item.{i}");

			if (i == page)
				container.Add(
					new CuiPanel
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Image = {Color = "1 1 1 0.2"}
					}, Layer + $".Item.{i}");

			if (!string.IsNullOrEmpty(itemCase.Image))
				container.Add(new CuiElement
				{
					Parent = Layer + $".Item.{i}",
					Components =
					{
						new CuiRawImageComponent
						{
							Png = GetImage(itemCase.Image)
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0.5 0.5",
							AnchorMax = "0.5 0.5",
							OffsetMin = "-38 -38",
							OffsetMax = "38 38"
						}
					}
				});
			else
				container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 0.5",
							AnchorMax = "0.5 0.5",
							OffsetMin = "-38 -38",
							OffsetMax = "38 38"
						},
						Image =
						{
							ItemId = itemCase.itemId,
							SkinId = itemCase.Skin
						}
					}, Layer + $".Item.{i}");

			container.Add(
				new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Button = {Command = $"UI_Battlepass changepage {i}", Color = "0 0 0 0"},
					Text = {Text = ""}
				}, Layer + $".Item.{i}");

			xSwitch += 150;
		}

		#endregion

		#region Button

		if (!_config.CaseItemsToPlayer)
			container.Add(
				new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = "-100 -280",
						OffsetMax = "100 -250"
					},
					Button = {Command = "UI_Battlepass inventory", Color = "0 0 0 0"},
					Text =
					{
						Text = Msg("Go to inventory", player.UserIDString),
						Align = TextAnchor.MiddleCenter,
						FontSize = 12
					}
				}, Layer + ".Main", Layer + ".Inventory");

		Outline(ref container, Layer + ".Inventory");

		#endregion

		#endregion

		CuiHelper.AddUi(player, container);
	}

	private void InventoryUI(BasePlayer player, int page = 0, bool isFirst = false)
	{
		var data = PlayerData.GetOrCreate(player.UserIDString);
		if (data == null) return;

		var container = new CuiElementContainer();

		#region First

		if (isFirst)
		{
			#region BG

			container.Add(new CuiElement
			{
				Parent = "Overlay",
				Name = Layer,
				DestroyUi = Layer,
				Components =
				{
					new CuiRawImageComponent
					{
						Png = GetImage(_config.Background),
						Color = _config.UI.Colors.BackgroundColor.Get
					},
					new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"},
					new CuiNeedsCursorComponent()
				}
			});

			#endregion

			#region Header

			HeaderUi(ref container, player);

			#endregion
		}

		#endregion

		#region Main

		container.Add(
			new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"}, Image = {Color = "0 0 0 0"}
			}, Layer, Layer + ".Main", Layer + ".Main");

		#region Title

		container.Add(
			new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = _config.UI.Inventory.Title.AnchorMin,
					AnchorMax = _config.UI.Inventory.Title.AnchorMax,
					OffsetMin = _config.UI.Inventory.Title.OffsetMin,
					OffsetMax = _config.UI.Inventory.Title.OffsetMax
				},
				Text =
				{
					Text = Msg("Inventory title", player.UserIDString),
					Align = _config.UI.Inventory.Title.Align,
					FontSize = _config.UI.Inventory.Title.FontSize,
					Font = _config.UI.Inventory.Title.Font,
					Color = _config.UI.Inventory.Title.Color,
					FadeIn = _config.UI.Inventory.Title.FadeIn
				}
			}, Layer + ".Main");

		#endregion

		#region Close

		container.Add(
			new CuiButton
			{
				RectTransform =
				{
					AnchorMin = _config.UI.Inventory.Back.AnchorMin,
					AnchorMax = _config.UI.Inventory.Back.AnchorMax,
					OffsetMin = _config.UI.Inventory.Back.OffsetMin,
					OffsetMax = _config.UI.Inventory.Back.OffsetMax
				},
				Button = {Command = "UI_Battlepass main", Color = "0 0 0 0"},
				Text =
				{
					Text = Msg("Back", player.UserIDString),
					Align = _config.UI.Inventory.Back.Align,
					FontSize = _config.UI.Inventory.Back.FontSize,
					Font = _config.UI.Inventory.Back.Font,
					Color = _config.UI.Inventory.Back.Color,
					FadeIn = _config.UI.Inventory.Back.FadeIn
				}
			}, Layer + ".Main");

		#endregion

		#region Items

		var xSwitch = -335;
		var ySwitch = 170;

		for (var i = 0; i < _config.UI.Inventory.MaxCount; i++)
		{
			container.Add(
				new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = $"{xSwitch} {ySwitch}",
						OffsetMax =
							$"{xSwitch + _config.UI.Inventory.ItemSize} {ySwitch + _config.UI.Inventory.ItemSize}"
					},
					Image = {Color = "1 1 1 0.2"}
				}, Layer + ".Main", Layer + $".Items.{i}");

			Outline(ref container, Layer + $".Items.{i}", "1 1 1 0.2");

			if ((i + 1) % _config.UI.Inventory.ItemsOnString == 0)
			{
				xSwitch = -335;
				ySwitch = ySwitch - _config.UI.Inventory.ItemSize - _config.UI.Inventory.Margin;
			}
			else
			{
				xSwitch = xSwitch + _config.UI.Inventory.ItemSize + _config.UI.Inventory.Margin;
			}
		}

		var g = 0;
		foreach (var check in data.GetItems().Skip(page * _config.UI.Inventory.MaxCount)
			         .Take(_config.UI.Inventory.MaxCount))
		{
			var id = check.Value;

			ItemConf item;
			if (!_itemById.TryGetValue(id, out item) || item == null) continue;

			if (!string.IsNullOrEmpty(item.Image))
				container.Add(new CuiElement
				{
					Parent = Layer + $".Items.{g}",
					Components =
					{
						new CuiRawImageComponent
						{
							Png = GetImage(item.Image)
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"
						}
					}
				});
			else
				container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"
						},
						Image =
						{
							ItemId = item.itemId,
							SkinId = item.Skin
						}
					}, Layer + $".Items.{g}");

			container.Add(
				new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-50 2.5", OffsetMax = "-2.5 17.5"
					},
					Text =
					{
						Text = $"x{item.Amount}",
						Align = TextAnchor.LowerRight,
						FontSize = 10,
						Font = "robotocondensed-regular.ttf"
					}
				}, Layer + $".Items.{g}");

			container.Add(
				new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Button =
					{
						Command = $"UI_Battlepass giveitem {check.Key} {page}",
						Color = "0 0 0 0"
					},
					Text = {Text = ""}
				}, Layer + $".Items.{g}", Layer + $".Items.{g}.BtnBuy");

			g++;
		}

		#endregion

		#region Pages

		var pages = (int) Math.Ceiling((double) data.Items.Count / _config.UI.Inventory.MaxCount);

		if (pages > 1)
		{
			container.Add(
				new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = _config.UI.Inventory.Pages.AnchorMin,
						AnchorMax = _config.UI.Inventory.Pages.AnchorMax,
						OffsetMin = _config.UI.Inventory.Pages.OffsetMin,
						OffsetMax = _config.UI.Inventory.Pages.OffsetMax
					},
					Image = {Color = "0 0 0 0"}
				}, Layer + ".Main", Layer + ".Pages");

			var size = 1.0f / pages;

			var pSwitch = 0.0f;

			for (var i = 0; i < pages; i++)
			{
				container.Add(
					new CuiButton
					{
						RectTransform = {AnchorMin = $"{pSwitch} 0", AnchorMax = $"{pSwitch + size} 1"},
						Button =
						{
							Command = $"UI_Battlepass invpage {i}",
							Color = i == page ? "1 1 1 0.6" : "1 1 1 0.2"
						},
						Text = {Text = ""}
					}, Layer + ".Pages");

				pSwitch += size;
			}
		}

		#endregion

		#endregion

		CuiHelper.AddUi(player, container);
	}

	private void GiveUI(BasePlayer player, int itemId)
	{
		var container = new CuiElementContainer();

		container.Add(
			new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image =
				{
					FadeIn = 1f,
					Color = "0.5 1 0.5 0.2",
					Sprite = "assets/content/ui/ui.background.tile.psd"
				}
			}, Layer + $".Items.{itemId}", Layer + $".Items.{itemId}.Hover");

		container.Add(
			new CuiLabel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text =
				{
					Text = Msg("Item gived", player.UserIDString),
					Align = TextAnchor.MiddleCenter,
					Color = "0.7 1 0.7 1",
					FontSize = 14
				}
			}, Layer + $".Items.{itemId}.Hover");

		CuiHelper.AddUi(player, container);
	}

	private void MissionsUI(BasePlayer player, int page = 0, bool isFirst = false)
	{
		var data = PlayerData.GetOrCreate(player.UserIDString);
		if (data == null) return;

		var container = new CuiElementContainer();

		#region First

		if (isFirst)
		{
			#region BG

			container.Add(new CuiElement
			{
				Parent = "Overlay",
				Name = Layer,
				DestroyUi = Layer,
				Components =
				{
					new CuiRawImageComponent
					{
						Png = GetImage(_config.Background),
						Color = _config.UI.Colors.BackgroundColor.Get
					},
					new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"},
					new CuiNeedsCursorComponent()
				}
			});

			#endregion

			#region Header

			HeaderUi(ref container, player);

			#endregion
		}

		#endregion

		#region Main

		container.Add(
			new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"}, Image = {Color = "0 0 0 0"}
			}, Layer, Layer + ".Main", Layer + ".Main");

		#region Back

		container.Add(
			new CuiButton
			{
				RectTransform =
				{
					AnchorMin = _config.UI.Missions.Back.AnchorMin,
					AnchorMax = _config.UI.Missions.Back.AnchorMax,
					OffsetMin = _config.UI.Missions.Back.OffsetMin,
					OffsetMax = _config.UI.Missions.Back.OffsetMax
				},
				Button = {Command = "UI_Battlepass main", Color = "0 0 0 0"},
				Text =
				{
					Text = Msg("Back", player.UserIDString),
					Align = _config.UI.Missions.Back.Align,
					FontSize = _config.UI.Missions.Back.FontSize,
					Font = _config.UI.Missions.Back.Font,
					Color = _config.UI.Missions.Back.Color,
					FadeIn = _config.UI.Missions.Back.FadeIn
				}
			}, Layer + ".Main");

		#endregion

		#region Private Mission

		var privateMission = GetPrivateMission(data);
		if (privateMission != null)
		{
			var progress = data.MissionProgress > privateMission.Amount
				? privateMission.Amount
				: data.MissionProgress;

			container.Add(
				new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = _config.UI.Missions.ProgressBar.AnchorMin,
						AnchorMax = _config.UI.Missions.ProgressBar.AnchorMax,
						OffsetMin = _config.UI.Missions.ProgressBar.OffsetMin,
						OffsetMax = _config.UI.Missions.ProgressBar.OffsetMax
					},
					Image = {Color = _config.UI.Colors.Color4.Get}
				}, Layer + ".Main", Layer + ".ProgressBar");

			var progressLine = progress / (float) privateMission.Amount;
			container.Add(
				new CuiPanel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = $"{progressLine} 1"},
					Image = {Color = progressLine > 0f ? _config.UI.Colors.Color3.Get : "0 0 0 0"}
				}, Layer + ".ProgressBar");

			container.Add(
				new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = _config.UI.Missions.ProgressTitle.AnchorMin,
						AnchorMax = _config.UI.Missions.ProgressTitle.AnchorMax,
						OffsetMin = _config.UI.Missions.ProgressTitle.OffsetMin,
						OffsetMax = _config.UI.Missions.ProgressTitle.OffsetMax
					},
					Text =
					{
						Text = Msg("PM Progress", player.UserIDString),
						Align = _config.UI.Missions.ProgressTitle.Align,
						FontSize = _config.UI.Missions.ProgressTitle.FontSize,
						Font = _config.UI.Missions.ProgressTitle.Font,
						Color = _config.UI.Missions.ProgressTitle.Color,
						FadeIn = _config.UI.Missions.ProgressTitle.FadeIn
					}
				}, Layer + ".Main");

			container.Add(
				new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = _config.UI.Missions.ProgressAmount.AnchorMin,
						AnchorMax = _config.UI.Missions.ProgressAmount.AnchorMax,
						OffsetMin = _config.UI.Missions.ProgressAmount.OffsetMin,
						OffsetMax = _config.UI.Missions.ProgressAmount.OffsetMax
					},
					Text =
					{
						Text = $"{progress} / {privateMission.Amount}",
						Align = _config.UI.Missions.ProgressAmount.Align,
						FontSize = _config.UI.Missions.ProgressAmount.FontSize,
						Font = _config.UI.Missions.ProgressAmount.Font,
						Color = _config.UI.Missions.ProgressAmount.Color,
						FadeIn = _config.UI.Missions.ProgressAmount.FadeIn
					}
				}, Layer + ".Main");

			container.Add(
				new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = _config.UI.Missions.PMTitle.AnchorMin,
						AnchorMax = _config.UI.Missions.PMTitle.AnchorMax,
						OffsetMin = _config.UI.Missions.PMTitle.OffsetMin,
						OffsetMax = _config.UI.Missions.PMTitle.OffsetMax
					},
					Text =
					{
						Text = Msg("PM title", player.UserIDString),
						Align = _config.UI.Missions.PMTitle.Align,
						FontSize = _config.UI.Missions.PMTitle.FontSize,
						Font = _config.UI.Missions.PMTitle.Font,
						Color = _config.UI.Missions.PMTitle.Color,
						FadeIn = _config.UI.Missions.PMTitle.FadeIn
					}
				}, Layer + ".Main");

			container.Add(
				new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = _config.UI.Missions.PMTitleDescription.AnchorMin,
						AnchorMax = _config.UI.Missions.PMTitleDescription.AnchorMax,
						OffsetMin = _config.UI.Missions.PMTitleDescription.OffsetMin,
						OffsetMax = _config.UI.Missions.PMTitleDescription.OffsetMax
					},
					Text =
					{
						Text = Msg("PM description", player.UserIDString),
						Align = _config.UI.Missions.PMTitleDescription.Align,
						FontSize = _config.UI.Missions.PMTitleDescription.FontSize,
						Font = _config.UI.Missions.PMTitleDescription.Font,
						Color = _config.UI.Missions.PMTitleDescription.Color,
						FadeIn = _config.UI.Missions.PMTitleDescription.FadeIn
					}
				}, Layer + ".Main");

			container.Add(
				new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = _config.UI.Missions.PMDescription.AnchorMin,
						AnchorMax = _config.UI.Missions.PMDescription.AnchorMax,
						OffsetMin = _config.UI.Missions.PMDescription.OffsetMin,
						OffsetMax = _config.UI.Missions.PMDescription.OffsetMax
					},
					Text =
					{
						Text = $"{privateMission.Description}",
						Align = _config.UI.Missions.PMDescription.Align,
						FontSize = _config.UI.Missions.PMDescription.FontSize,
						Font = _config.UI.Missions.PMDescription.Font,
						Color = _config.UI.Missions.PMDescription.Color,
						FadeIn = _config.UI.Missions.PMDescription.FadeIn
					}
				}, Layer + ".Main");

			container.Add(
				new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = _config.UI.Missions.PMTitleAward.AnchorMin,
						AnchorMax = _config.UI.Missions.PMTitleAward.AnchorMax,
						OffsetMin = _config.UI.Missions.PMTitleAward.OffsetMin,
						OffsetMax = _config.UI.Missions.PMTitleAward.OffsetMax
					},
					Text =
					{
						Text = Msg("PM award", player.UserIDString),
						Align = _config.UI.Missions.PMTitleAward.Align,
						FontSize = _config.UI.Missions.PMTitleAward.FontSize,
						Font = _config.UI.Missions.PMTitleAward.Font,
						Color = _config.UI.Missions.PMTitleAward.Color,
						FadeIn = _config.UI.Missions.PMTitleAward.FadeIn
					}
				}, Layer + ".Main");

			container.Add(new CuiElement
			{
				Parent = Layer + ".Main",
				Components =
				{
					new CuiRawImageComponent
					{
						Png = GetImage(_config.FirstCurrency.Image)
					},
					new CuiRectTransformComponent
					{
						AnchorMin = _config.UI.Missions.FirstCurrencyImage.AnchorMin,
						AnchorMax = _config.UI.Missions.FirstCurrencyImage.AnchorMax,
						OffsetMin = _config.UI.Missions.FirstCurrencyImage.OffsetMin,
						OffsetMax = _config.UI.Missions.FirstCurrencyImage.OffsetMax
					}
				}
			});

			container.Add(
				new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = _config.UI.Missions.FirstCurrency.AnchorMin,
						AnchorMax = _config.UI.Missions.FirstCurrency.AnchorMax,
						OffsetMin = _config.UI.Missions.FirstCurrency.OffsetMin,
						OffsetMax = _config.UI.Missions.FirstCurrency.OffsetMax
					},
					Text =
					{
						Text = $"{privateMission.MainAward}",
						Align = _config.UI.Missions.FirstCurrency.Align,
						FontSize = _config.UI.Missions.FirstCurrency.FontSize,
						Font = _config.UI.Missions.FirstCurrency.Font,
						Color = _config.UI.Missions.FirstCurrency.Color,
						FadeIn = _config.UI.Missions.FirstCurrency.FadeIn
					}
				}, Layer + ".Main");

			if (privateMission.UseSecondAward && HasPermission(player, _config.SecondCurrency.Permission))
			{
				container.Add(new CuiElement
				{
					Parent = Layer + ".Main",
					Components =
					{
						new CuiRawImageComponent
						{
							Png = GetImage(_config.SecondCurrency.Image)
						},
						new CuiRectTransformComponent
						{
							AnchorMin = _config.UI.Missions.SecondCurrencyImage.AnchorMin,
							AnchorMax = _config.UI.Missions.SecondCurrencyImage.AnchorMax,
							OffsetMin = _config.UI.Missions.SecondCurrencyImage.OffsetMin,
							OffsetMax = _config.UI.Missions.SecondCurrencyImage.OffsetMax
						}
					}
				});

				container.Add(
					new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = _config.UI.Missions.SecondCurrency.AnchorMin,
							AnchorMax = _config.UI.Missions.SecondCurrency.AnchorMax,
							OffsetMin = _config.UI.Missions.SecondCurrency.OffsetMin,
							OffsetMax = _config.UI.Missions.SecondCurrency.OffsetMax
						},
						Text =
						{
							Text = $"{privateMission.SecondAward}",
							Align = _config.UI.Missions.SecondCurrency.Align,
							FontSize = _config.UI.Missions.SecondCurrency.FontSize,
							Font = _config.UI.Missions.SecondCurrency.Font,
							Color = _config.UI.Missions.SecondCurrency.Color,
							FadeIn = _config.UI.Missions.SecondCurrency.FadeIn
						}
					}, Layer + ".Main");
			}

			container.Add(
				new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = _config.UI.Missions.Line1.AnchorMin,
						AnchorMax = _config.UI.Missions.Line1.AnchorMax,
						OffsetMin = _config.UI.Missions.Line1.OffsetMin,
						OffsetMax = _config.UI.Missions.Line1.OffsetMax
					},
					Image = {Color = "1 1 1 0.2"}
				}, Layer + ".Main");
		}

		#endregion

		#region Titles

		container.Add(
			new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = _config.UI.Missions.Title.AnchorMin,
					AnchorMax = _config.UI.Missions.Title.AnchorMax,
					OffsetMin = _config.UI.Missions.Title.OffsetMin,
					OffsetMax = _config.UI.Missions.Title.OffsetMax
				},
				Text =
				{
					Text = Msg("Missions title", player.UserIDString),
					Align = _config.UI.Missions.Title.Align,
					FontSize = _config.UI.Missions.Title.FontSize,
					Font = _config.UI.Missions.Title.Font,
					Color = _config.UI.Missions.Title.Color,
					FadeIn = _config.UI.Missions.Title.FadeIn
				}
			}, Layer + ".Main");

		var leftTime = _nextTime.Subtract(DateTime.UtcNow);

		container.Add(
			new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = _config.UI.Missions.Cooldown.AnchorMin,
					AnchorMax = _config.UI.Missions.Cooldown.AnchorMax,
					OffsetMin = _config.UI.Missions.Cooldown.OffsetMin,
					OffsetMax = _config.UI.Missions.Cooldown.OffsetMax
				},
				Text =
				{
					Text = Msg("Mission tochange", player.UserIDString, FormatShortTime(leftTime)),
					Align = _config.UI.Missions.Cooldown.Align,
					FontSize = _config.UI.Missions.Cooldown.FontSize,
					Font = _config.UI.Missions.Cooldown.Font,
					Color = _config.UI.Missions.Cooldown.Color,
					FadeIn = _config.UI.Missions.Cooldown.FadeIn
				}
			}, Layer + ".Main", Layer + ".Cooldown");

		#endregion

		#region Missions Header

		#region Description

		container.Add(
			new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5",
					AnchorMax = "0.5 0.5",
					OffsetMin = "-355 65",
					OffsetMax = "-10 95"
				},
				Image = {Color = "1 1 1 0.2"}
			}, Layer + ".Main", Layer + ".Header.Description");
		Arrow(ref container, Layer + ".Header.Description");
		container.Add(
			new CuiLabel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "10 0", OffsetMax = "0 0"},
				Text =
				{
					Text = Msg("Mission description", player.UserIDString),
					Align = TextAnchor.MiddleLeft,
					FontSize = 11,
					Font = "robotocondensed-regular.ttf"
				}
			}, Layer + ".Header.Description");

		#endregion

		#region Progress

		container.Add(
			new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-5 65", OffsetMax = "75 95"
				},
				Image = {Color = "1 1 1 0.2"}
			}, Layer + ".Main", Layer + ".Header.Progress");
		Arrow(ref container, Layer + ".Header.Progress");
		container.Add(
			new CuiLabel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text =
				{
					Text = Msg("Mission progress", player.UserIDString),
					Align = TextAnchor.MiddleCenter,
					FontSize = 11,
					Font = "robotocondensed-regular.ttf"
				}
			}, Layer + ".Header.Progress");

		#endregion

		#region Main_award

		container.Add(new CuiPanel
		{
			RectTransform =
			{
				AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "80 65", OffsetMax = "150 95"
			},
			Image = {Color = "1 1 1 0.2"}
		}, Layer + ".Main", Layer + ".Header.Main_award");
		Arrow(ref container, Layer + ".Header.Main_award");
		container.Add(
			new CuiLabel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text =
				{
					Text = Msg("Mission mainaward", player.UserIDString),
					Align = TextAnchor.MiddleCenter,
					FontSize = 11,
					Font = "robotocondensed-regular.ttf"
				}
			}, Layer + ".Header.Main_award");

		#endregion

		#region Second_Award

		container.Add(new CuiPanel
		{
			RectTransform =
			{
				AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "155 65", OffsetMax = "230 95"
			},
			Image = {Color = "1 1 1 0.2"}
		}, Layer + ".Main", Layer + ".Header.Second_Award");
		Arrow(ref container, Layer + ".Header.Second_Award");
		container.Add(
			new CuiLabel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text =
				{
					Text = Msg("Mission secondaward", player.UserIDString),
					Align = TextAnchor.MiddleCenter,
					FontSize = 11,
					Font = "robotocondensed-regular.ttf"
				}
			}, Layer + ".Header.Second_Award");

		#endregion

		#region Advance_award

		container.Add(
			new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "235 65", OffsetMax = "310 95"
				},
				Image = {Color = "1 1 1 0.2"}
			}, Layer + ".Main", Layer + ".Header.Advance_award");
		Arrow(ref container, Layer + ".Header.Advance_award");
		container.Add(
			new CuiLabel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text =
				{
					Text = Msg("Mission adwaward", player.UserIDString),
					Align = TextAnchor.MiddleCenter,
					FontSize = 11,
					Font = "robotocondensed-regular.ttf"
				}
			}, Layer + ".Header.Advance_award");

		#endregion

		#endregion

		#region Missions

		container.Add(
			new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = _config.UI.Missions.Background.AnchorMin,
					AnchorMax = _config.UI.Missions.Background.AnchorMax,
					OffsetMin = _config.UI.Missions.Background.OffsetMin,
					OffsetMax = _config.UI.Missions.Background.OffsetMax
				},
				Image = {Color = "0 0 0 0"}
			}, Layer + ".Main", Layer + ".Missions");

		var missions = _generalMissions.Skip(page * 5).Take(5).ToList();

		var ySwitch = 0;

		for (var i = 0; i < missions.Count; i++)
		{
			var check = missions[i];

			container.Add(
				new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 1",
						AnchorMax = "1 1",
						OffsetMin = $"0 {ySwitch - 30}",
						OffsetMax = $"0 {ySwitch}"
					},
					Image = {Color = "0 0 0 0"}
				}, Layer + ".Missions", Layer + $".Mission.{i}");

			container.Add(
				new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = "5 0", OffsetMax = "345 0"
					},
					Text =
					{
						Text = $"{5 * page + i + 1}. {check.Mission.Description}",
						Align = TextAnchor.MiddleLeft,
						FontSize = 12,
						Font = "robotocondensed-regular.ttf"
					}
				}, Layer + $".Mission.{i}");

			container.Add(
				new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "360 0", OffsetMax = "420 1"
					},
					Image = {Color = _config.UI.Colors.Color3.Get}
				}, Layer + $".Mission.{i}");

			var progress = GetMissionProgress(player, check.ID);
			var progressAmount = progress > check.Mission.Amount ? check.Mission.Amount : progress;
			container.Add(
				new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "350 0", OffsetMax = "430 30"
					},
					Text =
					{
						Text = $"{progressAmount} / {check.Mission.Amount}",
						Align = TextAnchor.MiddleCenter,
						FontSize = 12,
						Font = "robotocondensed-regular.ttf"
					}
				}, Layer + $".Mission.{i}");

			#region Main Award

			container.Add(new CuiElement
			{
				Parent = Layer + $".Mission.{i}",
				Components =
				{
					new CuiRawImageComponent
					{
						Png = GetImage(_config.FirstCurrency.Image)
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "475 5", OffsetMax = "495 25"
					}
				}
			});

			container.Add(
				new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "435 5", OffsetMax = "470 25"
					},
					Text =
					{
						Text = $"{check.Mission.MainAward * GetPlayerRates(player.UserIDString)}",
						Align = TextAnchor.MiddleRight,
						FontSize = 12,
						Font = "robotocondensed-regular.ttf"
					}
				}, Layer + $".Mission.{i}");

			#endregion

			if (check.Mission.UseSecondAward)
			{
				container.Add(new CuiElement
				{
					Parent = Layer + $".Mission.{i}",
					Components =
					{
						new CuiRawImageComponent
						{
							Png = GetImage(_config.SecondCurrency.Image)
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "535 5", OffsetMax = "555 25"
						}
					}
				});

				container.Add(
					new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "500 5", OffsetMax = "535 25"
						},
						Text =
						{
							Text = $"{check.Mission.SecondAward}",
							Align = TextAnchor.MiddleRight,
							FontSize = 12,
							Font = "robotocondensed-regular.ttf"
						}
					}, Layer + $".Mission.{i}");
			}

			if (check.Mission.UseAdvanceAward)
			{
				container.Add(new CuiElement
				{
					Name = Layer + $".Mission.{i}.AdvanceAward",
					Parent = Layer + $".Mission.{i}",
					Components =
					{
						new CuiRawImageComponent
						{
							Png = GetImage(_config.AdvanceAwardImg)
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0",
							AnchorMax = "0 0",
							OffsetMin = "617.5 5",
							OffsetMax = "637.5 25"
						}
					}
				});

				container.Add(
					new CuiButton
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Button =
						{
							Command = $"UI_Battlepass showaward {check.ID} {ySwitch}", Color = "0 0 0 0"
						},
						Text = {Text = ""}
					}, Layer + $".Mission.{i}.AdvanceAward");
			}

			ySwitch -= 35;
		}

		#endregion

		#region Pages

		var pages = (int) Math.Ceiling((double) _generalMissions.Count / 5);

		if (pages > 1)
		{
			container.Add(
				new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = _config.UI.Missions.Pages.AnchorMin,
						AnchorMax = _config.UI.Missions.Pages.AnchorMax,
						OffsetMin = _config.UI.Missions.Pages.OffsetMin,
						OffsetMax = _config.UI.Missions.Pages.OffsetMax
					},
					Image = {Color = "0 0 0 0"}
				}, Layer + ".Main", Layer + ".Pages");

			var size = 1.0 / pages;

			var pSwitch = 0.0;

			for (var i = pages - 1; i >= 0; i--)
			{
				container.Add(
					new CuiButton
					{
						RectTransform = {AnchorMin = $"0 {pSwitch}", AnchorMax = $"1 {pSwitch + size}"},
						Button =
						{
							Command = $"UI_Battlepass mispage {i}",
							Color = i == page ? "1 1 1 0.6" : "1 1 1 0.2"
						},
						Text = {Text = ""}
					}, Layer + ".Pages");

				pSwitch += size;
			}
		}

		#endregion

		#endregion

		CuiHelper.AddUi(player, container);
	}

	private void ShowAward(BasePlayer player, MissionConfig mission, int ySwitch)
	{
		if (mission == null) return;

		ySwitch += 15;

		var container = new CuiElementContainer();

		var guid = container.Add(
			new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5",
					AnchorMax = "0.5 0.5",
					OffsetMin = $"135 {ySwitch - 95}",
					OffsetMax = $"310 {ySwitch}"
				},
				Image = {Color = "0 0 0 1"},
				FadeOut = 0.1f
			}, Layer + ".Main");

		Outline(ref container, guid, _config.UI.Colors.Color3.Get);

		if (!string.IsNullOrEmpty(mission.AdvanceAward.Image))
			container.Add(new CuiElement
			{
				Parent = guid,
				Components =
				{
					new CuiRawImageComponent
					{
						Png = GetImage(mission.AdvanceAward.Image)
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0.5 0.5",
						AnchorMax = "0.5 0.5",
						OffsetMin = "-35 -25",
						OffsetMax = "35 45"
					}
				},
				FadeOut = 0.1f
			});
		else
			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5",
					AnchorMax = "0.5 0.5",
					OffsetMin = "-35 -25",
					OffsetMax = "35 45"
				},
				Image =
				{
					ItemId = mission.AdvanceAward.itemId,
					SkinId = mission.AdvanceAward.Skin
				},
				FadeOut = 0.1f
			}, guid);

		container.Add(
			new CuiLabel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 20"},
				Text =
				{
					Text = mission.AdvanceAward.Title,
					Align = TextAnchor.MiddleCenter,
					FontSize = 12,
					Font = "robotocondensed-regular.ttf"
				},
				FadeOut = 0.1f
			}, guid);

		CuiHelper.AddUi(player, container);

		player.Invoke(() => CuiHelper.DestroyUi(player, guid), 2.5f);
	}

	private void RefreshCooldown()
	{
		var span = _nextTime.Subtract(DateTime.UtcNow);
		var shortTime = FormatShortTime(span);

		if (_needUpdate)
		{
			_needUpdate = false;

			foreach (var player in _missionPlayers.FindAll(player => player != null && player.IsConnected))
				MissionsUI(player);
		}
		else
		{
			foreach (var player in _missionPlayers)
			{
				if (player == null || !player.IsConnected) continue;

				Debug.Log($"[RefreshCooldown.{player.userID}] called");

				CuiHelper.AddUi(player, new CuiElementContainer
				{
					{
						new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0.5 0.5",
								AnchorMax = "0.5 0.5",
								OffsetMin = "-150 95",
								OffsetMax = "150 165"
							},
							Text =
							{
								Text = Msg("Mission tochange", player.UserIDString, shortTime),
								Align = TextAnchor.MiddleCenter,
								FontSize = 16,
								Font = "robotocondensed-regular.ttf"
							}
						},
						Layer + ".Main", Layer + ".Cooldown", Layer + ".Cooldown"
					}
				});
			}
		}
	}

	private void Notification(BasePlayer player, string key, params object[] obj)
	{
		var container = new CuiElementContainer();
		var guid = CuiHelper.GetGuid();

		container.Add(
			new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-320 10", OffsetMax = "-20 60"
				},
				Image = {Color = _config.UI.Colors.Color1.Get},
				FadeOut = 0.4f
			}, Layer + ".Main", guid);

		container.Add(
			new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = "0 0", OffsetMax = "3 0"},
				Image = {Color = _config.UI.Colors.Color2.Get},
				FadeOut = 0.4f
			}, guid);

		container.Add(
			new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "2.5 0", OffsetMax = "-20 0"
				},
				Text =
				{
					Text = Msg(key, player.UserIDString, obj),
					Align = TextAnchor.MiddleRight,
					FontSize = 12,
					Font = "robotocondensed-regular.ttf"
				},
				FadeOut = 0.4f
			}, guid);

		CuiHelper.AddUi(player, container);

		player.Invoke(() => CuiHelper.DestroyUi(player, guid), 2.5f);
	}

	private void HeaderUi(ref CuiElementContainer container, BasePlayer player)
	{
		container.Add(
			new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1",
					AnchorMax = "1 1",
					OffsetMin = $"0 -{_config.UI.Header.Height}",
					OffsetMax = "0 0"
				},
				Image = {Color = "0 0 0 0.6"}
			}, Layer, Layer + ".Header", Layer + ".Header");

		container.Add(new CuiElement
		{
			Parent = Layer + ".Header",
			Components =
			{
				new CuiRawImageComponent
				{
					Png = GetImage(_config.Logo)
				},
				new CuiRectTransformComponent
				{
					AnchorMin = _config.UI.Header.Logo.AnchorMin,
					AnchorMax = _config.UI.Header.Logo.AnchorMax,
					OffsetMin = _config.UI.Header.Logo.OffsetMin,
					OffsetMax = _config.UI.Header.Logo.OffsetMax
				}
			}
		});

		container.Add(
			new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = _config.UI.Header.Line.AnchorMin,
					AnchorMax = _config.UI.Header.Line.AnchorMax,
					OffsetMin = _config.UI.Header.Line.OffsetMin,
					OffsetMax = _config.UI.Header.Line.OffsetMax
				},
				Image = {Color = "1 1 1 0.5"}
			}, Layer + ".Header");

		container.Add(
			new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = _config.UI.Header.PlayerName.AnchorMin,
					AnchorMax = _config.UI.Header.PlayerName.AnchorMax,
					OffsetMin = _config.UI.Header.PlayerName.OffsetMin,
					OffsetMax = _config.UI.Header.PlayerName.OffsetMax
				},
				Text =
				{
					Text = player.displayName,
					Align = TextAnchor.MiddleLeft,
					FontSize = 12,
					Font = "robotocondensed-regular.ttf"
				}
			}, Layer + ".Header");

		container.Add(new CuiElement
		{
			Parent = Layer + ".Header",
			Components =
			{
				new CuiRawImageComponent
				{
					Png = GetImage(_config.FirstCurrency.Image)
				},
				new CuiRectTransformComponent
				{
					AnchorMin = _config.UI.Header.FirstCurrency.AnchorMin,
					AnchorMax = _config.UI.Header.FirstCurrency.AnchorMax,
					OffsetMin = _config.UI.Header.FirstCurrency.OffsetMin,
					OffsetMax = _config.UI.Header.FirstCurrency.OffsetMax
				}
			}
		});

		container.Add(
			new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = _config.UI.Header.BalanceFirstCurrency.AnchorMin,
					AnchorMax = _config.UI.Header.BalanceFirstCurrency.AnchorMax,
					OffsetMin = _config.UI.Header.BalanceFirstCurrency.OffsetMin,
					OffsetMax = _config.UI.Header.BalanceFirstCurrency.OffsetMax
				},
				Text =
				{
					Text = $"{_config.FirstCurrency.ShowBalance(player)}",
					Align = _config.UI.Header.BalanceFirstCurrency.Align,
					FontSize = _config.UI.Header.BalanceFirstCurrency.FontSize,
					Font = _config.UI.Header.BalanceFirstCurrency.Font,
					Color = _config.UI.Header.BalanceFirstCurrency.Color,
					FadeIn = _config.UI.Header.BalanceFirstCurrency.FadeIn
				}
			}, Layer + ".Header", Layer + ".FreeCoins");

		if (_config.useSecondCur && HasPermission(player, _config.SecondCurrency.Permission))
		{
			container.Add(new CuiElement
			{
				Parent = Layer + ".Header",
				Components =
				{
					new CuiRawImageComponent
					{
						Png = GetImage(_config.SecondCurrency.Image)
					},
					new CuiRectTransformComponent
					{
						AnchorMin = _config.UI.Header.SecondCurrency.AnchorMin,
						AnchorMax = _config.UI.Header.SecondCurrency.AnchorMax,
						OffsetMin = _config.UI.Header.SecondCurrency.OffsetMin,
						OffsetMax = _config.UI.Header.SecondCurrency.OffsetMax
					}
				}
			});

			container.Add(
				new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = _config.UI.Header.BalanceSecondCurrency.AnchorMin,
						AnchorMax = _config.UI.Header.BalanceSecondCurrency.AnchorMax,
						OffsetMin = _config.UI.Header.BalanceSecondCurrency.OffsetMin,
						OffsetMax = _config.UI.Header.BalanceSecondCurrency.OffsetMax
					},
					Text =
					{
						Text = $"{_config.SecondCurrency.ShowBalance(player)}",
						Align = _config.UI.Header.BalanceSecondCurrency.Align,
						FontSize = _config.UI.Header.BalanceSecondCurrency.FontSize,
						Font = _config.UI.Header.BalanceSecondCurrency.Font,
						Color = _config.UI.Header.BalanceSecondCurrency.Color,
						FadeIn = _config.UI.Header.BalanceSecondCurrency.FadeIn
					}
				}, Layer + ".Header", Layer + ".PaidCoins");
		}
	}

	#endregion

	#region Utils
    
	private string GetImage(string name)
	{
#if CARBON
		return imageDatabase.GetImageString(name);
#else
		return Convert.ToString(ImageLibrary?.Call("GetImage", name));
#endif
	}

	private void UpdateHooks()
	{
		Unsubscribe(nameof(OnCollectiblePickup));
		Unsubscribe(nameof(OnDispenserGather));
		Unsubscribe(nameof(OnDispenserBonus));
		Unsubscribe(nameof(OnEntityDeath));
		Unsubscribe(nameof(OnEntityTakeDamage));
		Unsubscribe(nameof(OnItemCraftFinished));
		Unsubscribe(nameof(OnLootEntity));
		Unsubscribe(nameof(OnContainerDropItems));
		Unsubscribe(nameof(OnItemPickup));
		Unsubscribe(nameof(OnFishCatch));
		Unsubscribe(nameof(OnEntitySpawned));
		Unsubscribe(nameof(OnPayForUpgrade));
		Unsubscribe(nameof(OnBuildingUpgrade));
		Unsubscribe(nameof(OnRaidableBaseCompleted));
		Unsubscribe(nameof(OnRecyclerToggle));
		Unsubscribe(nameof(OnItemRecycle));
		Unsubscribe(nameof(CanHackCrate));
		Unsubscribe(nameof(OnNpcGiveSoldItem));

		_missions.Missions.ForEach(id =>
		{
			MissionConfig mission;
			if (_config.Missions.TryGetValue(id, out mission) && mission != null)
				SubscribeMissionHooks(mission);
		});

		foreach (var mission in _config.PrivateMissions)
			SubscribeMissionHooks(mission.Value);
	}

	private void SubscribeMissionHooks(MissionConfig mission)
	{
		switch (mission.Type)
		{
			case MissionType.Gather:
			{
				Subscribe(nameof(OnCollectiblePickup));
				Subscribe(nameof(OnDispenserGather));
				Subscribe(nameof(OnDispenserBonus));
				break;
			}
			case MissionType.Kill:
			{
				Subscribe(nameof(OnEntityDeath));
				Subscribe(nameof(OnEntityTakeDamage));
				break;
			}
			case MissionType.Craft:
			{
				Subscribe(nameof(OnItemCraftFinished));
				break;
			}
			case MissionType.Look:
			{
				Subscribe(nameof(OnLootEntity));
				Subscribe(nameof(OnContainerDropItems));
				Subscribe(nameof(OnItemPickup));
				break;
			}
			case MissionType.Build:
			{
				Subscribe(nameof(OnEntitySpawned));
				break;
			}
			case MissionType.Upgrade:
			{
				Subscribe(nameof(OnPayForUpgrade));
				Subscribe(nameof(OnBuildingUpgrade));
				break;
			}
			case MissionType.Fishing:
			{
				Subscribe(nameof(OnFishCatch));
				break;
			}
			case MissionType.LootCrate:
			{
				Subscribe(nameof(OnLootEntity));
				break;
			}
			case MissionType.Swipe:
			{
				Subscribe(nameof(OnCardSwipe));
				break;
			}
			case MissionType.RaidableBases:
			{
				Subscribe(nameof(OnRaidableBaseCompleted));
				break;
			}
			case MissionType.RecycleItem:
			{
				Subscribe(nameof(OnRecyclerToggle));
				Subscribe(nameof(OnItemRecycle));
				break;
			}
			case MissionType.HackCrate:
			{
				Subscribe(nameof(CanHackCrate));
				break;
			}
			case MissionType.PurchaseFromNpc:
			{
				Subscribe(nameof(OnNpcGiveSoldItem));
				break;
			}
		}
	}

	private void CheckPlayersWipe()
	{
		if (_config.Wipe.Players && _config.PlayerDatabase.Enabled)
			PrintError("Wipe player data from PlayerDatabase is not available!");
	}

	private void WipeMissions()
	{
		if (_missions == null)
			LoadMissions();

		if (_missions != null)
			_missions.MissionsDate = new DateTime(1970, 1, 1, 0, 0, 0);

		UpdateMissions();

		SaveMissions();
	}

	private void RegisterPermissions()
	{
		_config.Cases.ForEach(check =>
		{
			if (!string.IsNullOrEmpty(check.Permission) && !permission.PermissionExists(check.Permission))
				permission.RegisterPermission(check.Permission, this);
		});

		foreach (var check in _config.FirstCurrency.Rates)
			if (!permission.PermissionExists(check.Key))
				permission.RegisterPermission(check.Key, this);

		if (_config.useSecondCur && !string.IsNullOrEmpty(_config.SecondCurrency.Permission) &&
		    !permission.PermissionExists(_config.SecondCurrency.Permission))
			permission.RegisterPermission(_config.SecondCurrency.Permission, this);

		if (!string.IsNullOrEmpty(_config.Permission) && !permission.PermissionExists(_config.Permission))
			permission.RegisterPermission(_config.Permission, this);
	}

	private void RegisterCommands()
	{
		AddCovalenceCommand(new[] {_config.Command},
			nameof(CmdChatOpen));

		AddCovalenceCommand(_config.MissionsCommands,
			nameof(CmdPages));

		AddCovalenceCommand(_config.CasesCommands,
			nameof(CmdPages));

		AddCovalenceCommand(_config.InventoryCommands,
			nameof(CmdPages));

		AddCovalenceCommand(new[] {"addfirstcurrency", "addsecondcurrency"}, nameof(CmdAddBalance));
	}

	private static bool HasPermission(BasePlayer player, string perm)
	{
		return string.IsNullOrEmpty(perm) || _instance.permission.UserHasPermission(player.UserIDString, perm);
	}

	private static int SecondsFromWipe()
	{
		return (int) DateTime.UtcNow.Subtract(SaveRestore.SaveCreatedTime).TotalSeconds;
	}

	private static string FormatShortTime(int seconds)
	{
		return TimeSpan.FromSeconds(seconds).ToShortString();
	}

	private void CheckItems()
	{
		var anySet = false;
		var items = _config.Cases.SelectMany(x => x.Items).ToList();
		items.AddRange(_config.Missions.Values.Where(x => x.UseAdvanceAward).Select(x => x.AdvanceAward).ToList());
		items.AddRange(_config.PrivateMissions.Values.Where(x => x.UseAdvanceAward).Select(x => x.AdvanceAward)
			.ToList());

		foreach (var item in items)
		{
			while (_itemById.ContainsKey(item.ID))
			{
				item.ID = Random.Range(0, int.MaxValue);

				if (_itemById.ContainsKey(item.ID)) continue;

				anySet = true;
			}

			_itemById[item.ID] = item;
		}

		if (anySet)
			SaveConfig();
	}

	private void OnMissionsProgress(BasePlayer player,
		MissionType type,
		Item item = null,
		BaseEntity entity = null,
		int grade = 0,
		int itemAmount = -1,
		string mode = null,
		ItemDefinition definition = null)
	{
#if !TESTING
		if (player == null || !HasPermission(player, _config.Permission))
		{
			return;
		}
#endif
		
#if TESTING
		SayDebug($"[OnMissionsProgress] player={player.displayName}, type={type}, " +
		         $"item={(item != null ? item.info.displayName : string.Empty)}, " +
		         $"entity={(entity != null ? entity.ShortPrefabName : string.Empty)}, " +
		         $"grade={grade}, itemAmount={itemAmount}, mode={mode}, " +
		         $"definition={(definition != null ? definition.shortname : string.Empty)}");
#endif

		var data = PlayerData.GetOrCreate(player.UserIDString);
		if (data == null)
		{
#if TESTING
			SayDebug($"[OnMissionsProgress] player={player.displayName} can't create data");
#endif
			return;
		}

		_generalMissions?.ForEach(check =>
			check.Check(player, ref data, type, item, entity, grade, itemAmount, mode, definition));

		data.CheckPrivateMission(player, type, item, entity, grade, itemAmount, mode, definition);
	}

	private static int GetMissionAmount(MissionConfig mission,
		Item item, BaseEntity entity, int grade,
		int itemAmount, string mode,
		ItemDefinition definition)
	{
		if (mission == null) return 0;

		var amount = 0;

		switch (mission.Type)
		{
			case MissionType.Build:
			{
#if TESTING
				Debug.Log(
					$"[GetMissionAmount] mission={mission.Type}, name={entity.name}, shortname={mission.Shortname}");
#endif

				if (entity == null || mission.Shortname.Split('|').All(name => !entity.name.Contains(name))) return 0;

				return 1;
			}

			case MissionType.Gather:
			{
				if (item != null && mission.Shortname.Split('|').Any(name => name == item.info.shortname))
					return item.amount;

				if (definition != null && mission.Shortname.Split('|').Any(name => name == definition.shortname)
				                       && itemAmount > 0)
					return itemAmount;

				return 0;
			}

			case MissionType.Fishing:
			{
				if (item == null || mission.Shortname.Split('|').All(name => name != item.info.shortname)) return 0;

				return item.amount;
			}

			case MissionType.Look:
			{
				if (item == null || mission.Shortname.Split('|').All(name => name != item.info.shortname) ||
				    (mission.Skin != 0 && item.skin != mission.Skin))
					return 0;

				return itemAmount == -1 ? item.amount : itemAmount;
			}

			case MissionType.Craft:
			{
				if (item == null || mission.Shortname.Split('|').All(name => name != item.info.shortname))
					return 0;

				return item.amount;
			}

			case MissionType.Kill:
			{
#if TESTING
				SayDebug($"[GetMissionAmount.Kill] entity={entity?.ShortPrefabName?? "null"}, mission shortname={mission.Shortname}");
#endif
				if (entity == null || mission.Shortname.Split('|').All(name => name != entity.ShortPrefabName))
					return 0;

				if (!string.IsNullOrEmpty(mission.DisplayName) &&
				    (entity as BasePlayer)?.displayName.Contains(mission.DisplayName) != true)
					return 0;

				return 1;
			}

			case MissionType.LootCrate:
			{
				if (entity == null || mission.Shortname.Split('|').All(name => name != entity.ShortPrefabName))
					return 0;

				return 1;
			}

			case MissionType.Upgrade:
			{
				if (entity == null || mission.Shortname.Split('|').All(name => !entity.name.Contains(name)) ||
				    mission.Grade != grade) return 0;

				return 1;
			}

			case MissionType.Swipe:
			{
				if (item == null || mission.Shortname.Split('|').All(name => name != item.info.shortname))
					return 0;

				return 1;
			}

			case MissionType.RaidableBases:
			{
#if TESTING
				SayDebug($"[GetMissionAmount.RaidableBases] mode={mode}, mission.Mode={mission.Mode}");
#endif

				if (string.IsNullOrEmpty(mode) || (!string.IsNullOrEmpty(mission.Mode) &&
				                                   mission.Mode.Split('|').All(name => name != mode)))
				{
#if TESTING
					SayDebug($"[GetMissionAmount.RaidableBases.{mode}] return 0");
#endif
					return 0;
				}

#if TESTING
				SayDebug($"[GetMissionAmount.RaidableBases.{mode}] return 1");
#endif
				return 1;
			}

			case MissionType.RecycleItem:
			{
				if (item == null || mission.Shortname.Split('|').All(name => name != item.info.shortname))
					return 0;

				return itemAmount == -1 ? item.amount : itemAmount;
			}
			case MissionType.HackCrate:
			{
#if TESTING
				SayDebug(
					$"[GetMissionAmount.HackCrate] ShortPrefabName={entity.ShortPrefabName}, missionShortnames={string.Join(", ", mission.Shortname.Split('|'))}");
#endif

				if (entity == null || mission.Shortname.Split('|').All(name => name != entity.ShortPrefabName))
					return 0;

				return 1;
			}
			case MissionType.PurchaseFromNpc:
			{
				if (item == null || mission.Shortname.Split('|').All(name => name != item.info.shortname))
					return 0;

				return item.amount;
			}

			case MissionType.ArcticBaseEvent:
			case MissionType.GasStationEvent:
			case MissionType.SputnikEvent:
			case MissionType.ShipwreckEvent:
			case MissionType.HarborEvent:
			case MissionType.JunkyardEvent:
			case MissionType.SatDishEvent:
			case MissionType.WaterEvent:
			case MissionType.AirEvent:
			case MissionType.PowerPlantEvent:
			case MissionType.ArmoredTrainEvent:
			case MissionType.ConvoyEvent:
			case MissionType.SurvivalArena:
			{
				return 1;
			}

			case MissionType.KillBoss:
			{
				if (entity == null || (!string.IsNullOrEmpty(mission.Shortname) &&
				                       mission.Shortname.Split('|').All(name => name != entity.ShortPrefabName)))
					return 0;

				return 1;
			}
		}

		return amount;
	}

	private static void CompleteMission(BasePlayer player, MissionConfig mission)
	{
		if (player == null || mission == null) return;

		if (_config.NotifyMissionCompleted)
			_instance.SendNotify(player, "Notify.Mission.Complete", 0, mission.Description);

		mission.GiveAwards(player);
	}

	private static string FormatShortTime(TimeSpan time)
	{
		var result = new List<int>();
		if (time.Days != 0) result.Add(time.Days);

		if (time.Hours != 0) result.Add(time.Hours);

		if (time.Minutes != 0) result.Add(time.Minutes);

		if (time.Seconds != 0) result.Add(time.Seconds);

		return string.Join(":", result.Take(2).Select(x => x.ToString()));
	}

	private static MissionConfig GetPrivateMission(PlayerData data)
	{
		return _config.PrivateMissions.GetValueOrDefault(data.MissionId);
	}

	private void UpdateMissions()
	{
		_updateMissions?.Destroy();

		_refreshCooldown?.Destroy();

		var now = DateTime.UtcNow;

		var lastWipe = _missions.MissionsDate;

		_privateMissions = _config.PrivateMissions.Keys.ToList();

		if (lastWipe == epoch || lastWipe.AddHours(_config.MissionHours) < now)
		{
			_generalMissions = GetRandomMissions();

			_missions.MissionsDate = now;

			_missions.Missions.Clear();

			_generalMissions.ForEach(mission => _missions.Missions.Add(mission.ID));

			foreach (var data in _usersData.Values.ToList())
			{
				data.ResetPrivateMission();

				data.ResetMissionsProgress();
			}

			SaveMissions();
		}
		else
		{
			_generalMissions.Clear();

			_missions.Missions.ForEach(id =>
			{
				MissionConfig mission;
				if (_config.Missions.TryGetValue(id, out mission) && mission != null)
					_generalMissions.Add(new GeneralMission(id, mission));
			});
		}

		_nextTime = lastWipe.AddHours(_config.MissionHours);

		var seconds = (int) _nextTime.Subtract(now).TotalSeconds;

		_needUpdate = true;

		_updateMissions = timer.In(seconds + 2.5f, UpdateMissions);

		_refreshCooldown = timer.Every(60, RefreshCooldown);

		UpdateHooks();
	}

	private void UpdatePlayerMission(BasePlayer player, DateTime now)
	{
		if (player == null) return;

		PlayerData.GetOrCreate(player.UserIDString)?.ResetPrivateMission();
	}

	private List<GeneralMission> GetRandomMissions()
	{
		var generalMissions = _config.Missions
			.Where(x => x.Value.Enabled)
			.Select(configQuest => new GeneralMission(configQuest.Key, configQuest.Value))
			.ToList();

		var seed = (uint) DateTime.UtcNow.Ticks;
		generalMissions.Shuffle(seed);

		return generalMissions.Take(_config.MissionsCount).ToList();
	}

	private int GetFirstCurrency(ulong userId)
	{
		return GetFirstCurrency(PlayerData.GetOrCreate(userId.ToString()));
	}

	private int GetFirstCurrency(PlayerData data)
	{
		return data?.FirstCurrency ?? 0;
	}

	private int GetSecondCurrency(ulong userId)
	{
		return GetSecondCurrency(PlayerData.GetOrCreate(userId.ToString()));
	}

	private int GetSecondCurrency(PlayerData data)
	{
		return data?.SecondCurrency ?? 0;
	}

	private bool RemoveFirstCurrency(ulong player, int amount)
	{
		return RemoveFirstCurrency(PlayerData.GetOrCreate(player.ToString()), amount);
	}

	private bool RemoveFirstCurrency(BasePlayer player, int amount)
	{
		return RemoveFirstCurrency(PlayerData.GetOrCreate(player.UserIDString), amount);
	}

	private bool RemoveFirstCurrency(PlayerData data, int amount)
	{
		if (data == null || data.FirstCurrency < amount) return false;
		data.FirstCurrency -= amount;
		return true;
	}

	private bool RemoveSecondCurrency(ulong player, int amount)
	{
		return RemoveSecondCurrency(PlayerData.GetOrCreate(player.ToString()), amount);
	}

	private bool RemoveSecondCurrency(BasePlayer player, int amount)
	{
		return RemoveSecondCurrency(PlayerData.GetOrCreate(player.UserIDString), amount);
	}

	private bool RemoveSecondCurrency(PlayerData data, int amount)
	{
		if (data == null || data.SecondCurrency < amount) return false;
		data.SecondCurrency -= amount;
		return true;
	}

	private bool AddFirstCurrency(ulong player, int amount)
	{
		return AddFirstCurrency(player.ToString(), amount);
	}

	private bool AddFirstCurrency(string player, int amount)
	{
		return AddFirstCurrency(PlayerData.GetOrCreate(player), amount);
	}

	private bool AddFirstCurrency(BasePlayer player, int amount)
	{
		return AddFirstCurrency(PlayerData.GetOrCreate(player.UserIDString), amount);
	}

	private bool AddFirstCurrency(PlayerData data, int amount)
	{
		if (data == null) return false;

		data.FirstCurrency += amount;
		return true;
	}

	private bool AddSecondCurrency(ulong player, int amount)
	{
		return AddSecondCurrency(player.ToString(), amount);
	}

	private bool AddSecondCurrency(string player, int amount)
	{
		return AddSecondCurrency(PlayerData.GetOrCreate(player), amount);
	}

	private bool AddSecondCurrency(BasePlayer player, int amount)
	{
		return AddSecondCurrency(PlayerData.GetOrCreate(player.UserIDString), amount);
	}

	private bool AddSecondCurrency(PlayerData data, int amount)
	{
		if (data == null) return false;
		data.SecondCurrency += amount;
		return true;
	}

	private static List<ItemConf> GetRandom(CaseClass Case, int count)
	{
		var result = new List<ItemConf>();

		for (var i = 0; i < count; i++)
		{
			ItemConf item = null;

			var iteration = 0;
			do
			{
				iteration++;

				var randomItem = Case.Items[Random.Range(0, Case.Items.Count)];

				if (randomItem.Chance < 1 || randomItem.Chance > 100) continue;

				if (Random.Range(0f, 100f) <= randomItem.Chance) item = randomItem;
			} while (item == null && iteration < 1000);

			if (item != null) result.Add(item);
		}

		return result;
	}

	private static void Outline(ref CuiElementContainer container, string parent, string color = "1 1 1 1",
		string size = "1.5")
	{
		container.Add(
			new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = $"0 {size}"
				},
				Image = {Color = color}
			}, parent);
		container.Add(
			new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -{size}", OffsetMax = "0 0"
				},
				Image = {Color = color}
			}, parent);
		container.Add(
			new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0",
					AnchorMax = "0 1",
					OffsetMin = $"0 {size}",
					OffsetMax = $"{size} -{size}"
				},
				Image = {Color = color}
			}, parent);
		container.Add(
			new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "1 0",
					AnchorMax = "1 1",
					OffsetMin = $"-{size} {size}",
					OffsetMax = $"0 -{size}"
				},
				Image = {Color = color}
			}, parent);
	}

	private void Arrow(ref CuiElementContainer container, string parent)
	{
		container.Add(
			new CuiPanel
			{
				RectTransform = {AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -5", OffsetMax = "2 0"},
				Image = {Color = _config.UI.Colors.Color3.Get}
			}, parent);
		container.Add(
			new CuiPanel
			{
				RectTransform = {AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -2", OffsetMax = "5 0"},
				Image = {Color = _config.UI.Colors.Color3.Get}
			}, parent);
	}

	private static int GetMissionProgress(BasePlayer player, int missionId)
	{
		var data = PlayerData.GetOrCreate(player.UserIDString);
		if (data == null) return 0;
		data.Missions.TryGetValue(missionId, out var progress);
		return progress;
	}

	private static float GetPlayerRates(string userId)
	{
		var result = 1f;

		foreach (var rate in _config.FirstCurrency.Rates)
			if (_instance.permission.UserHasPermission(userId, rate.Key) && rate.Value > result)
				result = rate.Value;

		return result;
	}

	private static string HexToCuiColor(string hex)
	{
		if (string.IsNullOrEmpty(hex)) hex = "#FFFFFFFF";

		var str = hex.Trim('#');

		if (str.Length == 6) str += "FF";

		if (str.Length != 8) throw new Exception(hex);

		var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
		var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
		var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
		var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);
		Color color = new Color32(r, g, b, a);
		return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
	}

	private void LoadImages()
	{
		_enabledImageLibrary = false;
#if CARBON
		imageDatabase = BaseModule.GetModule<ImageDatabaseModule>();
#endif

		var imagesList = new Dictionary<string, string>();

		var itemIcons = new List<KeyValuePair<string, ulong>>();

		LoadImage(ref imagesList, _config.Background);

		LoadImage(ref imagesList, _config.Logo);

		LoadImage(ref imagesList, _config.FirstCurrency.Image);

		LoadImage(ref imagesList, _config.CaseBG);

		LoadImage(ref imagesList, _config.AdvanceAwardImg);

		if (_config.useSecondCur)
			LoadImage(ref imagesList, _config.SecondCurrency.Image);

		_config.Cases.ForEach(caseEntry =>
		{
			LoadImage(ref imagesList, caseEntry.Image);

			caseEntry.Items.ForEach(item =>
			{
				LoadImage(ref imagesList, item.Image);

				itemIcons.Add(new KeyValuePair<string, ulong>(item.Shortname, item.Skin));
			});
		});

		#region Tabs

		foreach (var tabInfo in _config.Tabs.Tabs)
			if (tabInfo.Enabled)
				LoadImage(ref imagesList, tabInfo.Image);
        
		#endregion

		#region Missions

		foreach (var missions in _config.Missions.Values.Where(missions => !missions.UseAdvanceAward))
			LoadImage(ref imagesList, missions.AdvanceAward.Image);

		foreach (var missions in _config.PrivateMissions.Values.Where(missions => !missions.UseAdvanceAward))
			LoadImage(ref imagesList, missions.AdvanceAward.Image);

		#endregion

#if CARBON
		imageDatabase.Queue(true, imagesList);
		
		_enabledImageLibrary = true;
#else
		timer.In(1f, () =>
		{
			if (ImageLibrary is not {IsLoaded: true})
			{
				_enabledImageLibrary = false;

				BroadcastILNotInstalled();
				return;
			}

			_enabledImageLibrary = true;

			if (itemIcons.Count > 0) ImageLibrary?.Call("LoadImageList", Title, itemIcons, null);

			ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
		});
#endif
	}

	private void BroadcastILNotInstalled()
	{
		for (var i = 0; i < 5; i++) PrintError("IMAGE LIBRARY IS NOT INSTALLED.");
	}

	private void LoadImage(ref Dictionary<string, string> imagesList, string image)
	{
		if (!string.IsNullOrEmpty(image))
			imagesList.TryAdd(image, image);
	}

	#endregion

	#region Log

	private void Log(string filename, string key, params object[] obj)
	{
		var text = Msg(key, null, obj);
		if (_config.LogToConsole) Puts(text);

		if (_config.LogToFile) LogToFile(filename, $"[{DateTime.Now}] {text}", this);
	}

	#endregion

	#region Lang

	private const string
		NoILError = "NoILError",
		BackBtn = "BackBtn",
		NextBtn = "NextBtn",
		NotifyMissionComplete = "Notify.Mission.Complete",
		NoEscapeInventory = "NoEscape.Inventory",
		WipeBlockInventory = "WipeBlock.Inventory",
		NoCasePermission = "NoCasePermission",
		NoPermission = "NoPermission",
		MissionsSecondAward = "Mission secondaward",
		MissionsExtraAward = "Mission adwaward",
		MissionsMainAward = "Mission mainaward",
		MissionsProgress = "Mission progress",
		MissionsDescription = "Mission description",
		MissionsToChange = "Mission tochange",
		MissionsTitle = "Missions title",
		PMAward = "PM award",
		PMDescription = "PM description",
		PMTitle = "PM title",
		PMProgress = "PM Progress",
		MsgItemGived = "Item gived",
		InventoryTitle = "Inventory title",
		BtnToInventory = "Go to inventory",
		TitleYourAward = "Your award",
		BtnModalAccept = "Modal accept",
		BtnModalTryOpen = "Modal tryopen",
		CaseBtnOpen = "Case open",
		CaseAwards = "Case awards",
		CaseChangeCurrency = "Case pick current",
		CasesShow = "Cases show",
		CasesTitle = "Cases title",
		BtnBack = "Back",
		BtnExit = "Exit",
		BtnInventory = "Inventory btn",
		BtnCases = "Cases btn",
		BtnMission = "Mission btn",
		GiveMoneyLog = "givemoney",
		TakeItemLog = "getitem",
		OpenCaseLog = "opencase",
		MsgNotEnough = "Not enough";

	protected override void LoadDefaultMessages()
	{
		lang.RegisterMessages(new Dictionary<string, string>
		{
			[MsgNotEnough] = "Not enough coins",
			[OpenCaseLog] = "Player {0} ({1}) opened case {2} and received from there: {3}",
			[TakeItemLog] = "Player {0} ({1}) taked from inventory: {2}",
			[GiveMoneyLog] = "Player {0} ({1}) received {2} to the balance in {3}",
			[BtnMission] = "TESTING THE HORGON",
			[BtnCases] = "SEASONAL STORE",
			[BtnInventory] = "INVENTORY",
			[BtnExit] = "« QUIT",
			[BtnBack] = "« BACK",
			[CasesTitle] = "PRODUCTS",
			[CasesShow] = "LOOK",
			[CaseChangeCurrency] = "Choose currency",
			[CaseAwards] = "<b>Possible awards</b>",
			[CaseBtnOpen] = "OPEN",
			[BtnModalTryOpen] = "YOU ARE GOING TO OPEN\n'{0}'",
			[BtnModalAccept] = "CONFIRM",
			[TitleYourAward] = "YOUR AWARD",
			[BtnToInventory] = "GO TO INVENTORY",
			[InventoryTitle] = "INVENTORY",
			[MsgItemGived] = "SUCCESS\nRECEIVED",
			[PMProgress] = "Active mission progress",
			[PMTitle] = "CHALLENGE OF THE DAY",
			[PMDescription] = "DESCRIPTION",
			[PMAward] = "Award:",
			[MissionsTitle] = "CHALLENGES OF THE DAY",
			[MissionsToChange] = "Before the change of tasks left: <color=#bd5221>{0}</color>",
			[MissionsDescription] = "<b>Description</b>",
			[MissionsProgress] = "<b>Progress:</b>",
			[MissionsMainAward] = "<b>Main award</b>",
			[MissionsExtraAward] = "<b>Extra award</b>",
			[MissionsSecondAward] = "<b>Second award</b>",
			[NoPermission] = "You dont have permission to use this command!",
			[NoCasePermission] = "You have no permissions to open this case",
			[WipeBlockInventory] = "You cannot pick up items from your inventory for another {0}!",
			[NotifyMissionComplete] = "You have completed the mission '{0}'",
			[NoEscapeInventory] = "You cannot take item while blocked!",
			[NextBtn] = "▼",
			[BackBtn] = "▲",
			[NoILError] = "The plugin does not work correctly, contact the administrator!"
		}, this);

		lang.RegisterMessages(new Dictionary<string, string>
		{
			[MsgNotEnough] = "Недостаточно монет",
			[OpenCaseLog] = "Игрок {0} ({1}) открыл кейс {2} и получил оттуда: {3}",
			[TakeItemLog] = "Игрок {0} ({1}) забрал из инвентаря: {2}",
			[GiveMoneyLog] = "Игрок {0} ({1}) получил {2} на баланс в {3}",
			[BtnMission] = "ИСПЫТАНИЯ ГОРГОНЫ",
			[BtnCases] = "СЕЗОННЫЙ МАГАЗИН",
			[BtnInventory] = "ИНВЕНТАРЬ",
			[BtnExit] = "« ВЫЙТИ",
			[BtnBack] = "« НАЗАД",
			[CasesTitle] = "ТОВАРЫ",
			[CasesShow] = "ПОСМОТРЕТЬ",
			[CaseChangeCurrency] = "Выбери валюту",
			[CaseAwards] = "<b>Возможные призы</b>",
			[CaseBtnOpen] = "ОТКРЫТЬ",
			[BtnModalTryOpen] = "ВЫ СОБИРАЕТЕСЬ ОТКРЫТЬ\n'{0}'",
			[BtnModalAccept] = "ПОДТВЕРДИТЬ",
			[TitleYourAward] = "ВАША НАГРАДА",
			[BtnToInventory] = "ПЕРЕЙТИ В ИНВЕНТАРЬ",
			[InventoryTitle] = "ИНВЕНТАРЬ",
			[MsgItemGived] = "УСПЕШНО\nПОЛУЧЕНО",
			[PMProgress] = "Прогресс активного задания",
			[PMTitle] = "ВЫЗОВ ДНЯ",
			[PMDescription] = "ОПИСАНИЕ",
			[PMAward] = "Награда:",
			[MissionsTitle] = "ЗАДАЧИ ДНЯ",
			[MissionsToChange] = "До смены заданий осталось: <color=#bd5221>{0}</color>",
			[MissionsDescription] = "<b>Описание</b>",
			[MissionsProgress] = "<b>Прогресс:</b>",
			[MissionsMainAward] = "<b>Основная награда</b>",
			[MissionsExtraAward] = "<b>Доп. награда</b>",
			[MissionsSecondAward] = "<b>Вторая награда</b>",
			[NoPermission] = "У вас нет право на использование этой команды!",
			[NoCasePermission] = "У вас нет прав на открытие этого кейса!",
			[WipeBlockInventory] = "Вы не можете забрать предметы из своего инвентаря ещё {0}!",
			[NotifyMissionComplete] = "Вы выполнили миссию '{0}'",
			[NoEscapeInventory] = "Вы не можете взять предмет во время блокировки!",
			[NextBtn] = "▼",
			[BackBtn] = "▲",
			[NoILError] = "Плагин работает некорректно, свяжитесь с администратором!"
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

	#region Convert

	#region 1.37.0

	[ConsoleCommand("battlepass.convert.olddata")]
	private void CmdConvertOldData(ConsoleSystem.Arg arg)
	{
		if (!arg.IsAdmin) return;

		ConvertPlayerData();
	}

	private void ConvertPlayerData()
	{
		var data = LoadOldPlayerData();
		if (data != null)
			timer.In(0.3f, () =>
			{
				ConvertOldPlayerData(data);

				ClearDataCache();
			});
	}

	private OldPlayersData LoadOldPlayerData()
	{
		OldPlayersData data = null;
		try
		{
			data = Interface.Oxide.DataFileSystem.ReadObject<OldPlayersData>($"{Name}/Players");
		}
		catch (Exception e)
		{
			PrintError(e.ToString());
		}

		return data ?? new OldPlayersData();
	}

	private void ConvertOldPlayerData(OldPlayersData data)
	{
		data.Players.ToList().ForEach(playerData =>
		{
			var newData = PlayerData.GetOrCreate(playerData.Key.ToString());

			newData.LastWipe = DateTime.UtcNow;
			newData.MissionId = playerData.Value.MissionId;
			newData.MissionProgress = playerData.Value.MissionProgress;
			newData.FirstCurrency = playerData.Value.FirstCurrency;
			newData.SecondCurrency = playerData.Value.SecondCurrency;
			newData.Missions = playerData.Value.Missions;
			newData.Items = playerData.Value.Items;
		});
	}

	#region Classes

	private class OldPlayersData
	{
		[JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public Dictionary<ulong, OldPlayerData> Players = new();
	}

	private class OldPlayerData
	{
		[JsonProperty(PropertyName = "Mission ID")]
		public int MissionId;

		[JsonProperty(PropertyName = "Mission Progress")]
		public int MissionProgress;

		[JsonProperty(PropertyName = "Currency 1")]
		public int FirstCurrency;

		[JsonProperty(PropertyName = "Currency 2")]
		public int SecondCurrency;

		[JsonProperty(PropertyName = "General Mission Progress",
			ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public Dictionary<int, int> Missions = new();

		[JsonProperty(PropertyName = "Items List", ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public List<int> Items = new();
	}

	#endregion

	#endregion

	#endregion

	#region API

	private JObject GetItemById(int id)
	{
		ItemConf item;
		return _itemById.TryGetValue(id, out item) ? item.ToJObject() : null;
	}

	private JArray GetPlayerInventory(ulong member)
	{
		var data = PlayerData.GetOrCreate(member.ToString());
		return data == null ? null : new JArray(data.Items.Select(GetItemById));
	}

	private JArray GetGeneralMissions()
	{
		return new JArray(_generalMissions.Select(x => x.Mission.ToJObject()));
	}

	private int GetPlayerProgress(ulong member, int mission)
	{
		var data = PlayerData.GetOrCreate(member.ToString());
		if (data == null) return 0;

		int result;
		return data.Missions.TryGetValue(mission, out result) ? result : 0;
	}

	#endregion

	#region Data 2.0

	#region Player Data

	private Dictionary<string, PlayerData> _usersData = new();

	private class PlayerData
	{
		#region Main

		#region Fields

		[JsonProperty(PropertyName = "Mission ID")]
		public int MissionId;

		[JsonProperty(PropertyName = "Mission Progress")]
		public int MissionProgress;

		[JsonProperty(PropertyName = "Currency 1")]
		public int FirstCurrency;

		[JsonProperty(PropertyName = "Currency 2")]
		public int SecondCurrency;

		[JsonProperty(PropertyName = "General Mission Progress",
			ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public Dictionary<int, int> Missions = new();

		[JsonProperty(PropertyName = "Items List", ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public List<int> Items = new();

		[JsonProperty(PropertyName = "Last Missions Update")]
		public DateTime LastMissionsUpdate;

		#endregion

		#region Utils

		public Dictionary<int, int> GetItems()
		{
			var dict = new Dictionary<int, int>(); //totalIndex - item

			for (var index = 0; index < Items.Count; index++)
			{
				var id = Items[index];

				ItemConf itemConf;
				if (_instance._itemById.TryGetValue(id, out itemConf))
					dict[index] = id;
			}

			return dict;
		}

		public void ResetPrivateMission()
		{
			MissionId = _instance._privateMissions.GetRandom();
			MissionProgress = 0;
		}

		public void ResetMissionsProgress()
		{
			Missions.Clear();
		}

		public void CheckPrivateMission(BasePlayer player, MissionType type, Item item = null,
			BaseEntity entity = null,
			int grade = 0, int itemAmount = -1,
			string mode = null,
			ItemDefinition definition = null)
		{
			var privateMission = GetPrivateMission(this);
			if (privateMission == null) return;

			if (MissionProgress >= privateMission.Amount || privateMission.Type != type) return;

			var missionAmount = GetMissionAmount(privateMission, item, entity, grade, itemAmount, mode, definition);

#if TESTING
			SayDebug($"[CheckPrivateMission.{player.userID}] type: {type}, amount: {missionAmount}");
#endif
			
			MissionProgress += missionAmount;

			if (MissionProgress >= privateMission.Amount)
			{
				CompleteMission(player, privateMission);
				if (_config.ResetQuestAfterComplete) MissionProgress = 0;
			}
		}

		public void CheckPlayerMission(string userId)
		{
			if (GetPrivateMission(this) == null)
			{
				MissionId = _instance._privateMissions.GetRandom();
				MissionProgress = 0;

				Save(userId);
			}
		}

		public void CheckMissionsUpdate(string userId)
		{
			var now = DateTime.UtcNow;

			var lastWipe = _instance._missions.MissionsDate;

			if (lastWipe > LastMissionsUpdate || lastWipe == _instance.epoch ||
			    lastWipe.AddHours(_config.MissionHours) < now)
			{
				ResetPrivateMission();

				ResetMissionsProgress();

				LastMissionsUpdate = lastWipe;

				Save(userId);
			}
		}

		#endregion

		#endregion

		#region Helpers

		private static string BaseFolder()
		{
			return "Battlepass" + Path.DirectorySeparatorChar + "Players" + Path.DirectorySeparatorChar;
		}

		public static PlayerData GetOrLoad(string userId)
		{
			if (_config.PlayerDatabase.Enabled) return _instance.LoadPlayerDatabaseData(userId);

			var data = GetOrLoad(BaseFolder(), userId);

			TryToWipe(userId, ref data);

			data?.CheckMissionsUpdate(userId);

			data?.CheckPlayerMission(userId);

			return data;
		}

		public static PlayerData GetOrLoad(string baseFolder, string userId)
		{
			PlayerData data;
			if (_instance._usersData.TryGetValue(userId, out data))
				return data;

			try
			{
				data = ReadOnlyObject(baseFolder + userId);
			}
			catch (Exception e)
			{
				Interface.Oxide.LogError(e.ToString());
			}

			return _instance._usersData[userId] = data;
		}

		public static PlayerData GetOrCreate(string userId)
		{
			var data = GetOrLoad(userId);
			if (data != null)
				return data;

			data = _instance._usersData[userId] = new PlayerData();

			data.LastWipe = DateTime.UtcNow;

			data.CheckMissionsUpdate(userId);

			data.CheckPlayerMission(userId);

			return data;
		}

		public static void Save()
		{
			foreach (var userId in _instance._usersData.Keys)
				Save(userId);
		}

		public static void Save(string userId)
		{
			if (_config.PlayerDatabase.Enabled)
			{
				_instance.SaveData(userId, _instance.LoadPlayerDatabaseData(userId));
				return;
			}

			PlayerData data;
			if (!_instance._usersData.TryGetValue(userId, out data))
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

		private string[] GetFiles(string userId)
		{
			try
			{
				var json = ".json".Length;
				var paths = Interface.Oxide.DataFileSystem.GetFiles(userId);
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

		[JsonProperty(PropertyName = "Last Wipe")]
		public DateTime LastWipe;

		public static void TryToWipe(string userId, ref PlayerData data)
		{
			if (_config.Wipe.Players && data != null &&
			    SaveRestore.SaveCreatedTime.ToUniversalTime() > data.LastWipe.ToUniversalTime())
			{
				_instance._usersData[userId] = data = new PlayerData
				{
					LastWipe = DateTime.UtcNow
				};

				Save(userId);
			}
		}

		#endregion
	}

	#endregion

	#region Utils

	private void ClearDataCache()
	{
		var players = BasePlayer.activePlayerList.Select(x => x.UserIDString).ToList();

		_usersData.Where(x => !players.Contains(x.Key))
			.ToList()
			.ForEach(data => PlayerData.SaveAndUnload(data.Key));
	}

	#endregion

	#endregion

	#region Testing functions

#if TESTING
	private static void SayDebug(string message)
	{
		Debug.Log($"[Battlepass.Debug] {message}");
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

	private ulong GetRandomSteamID()
	{
		return ulong.Parse($"{76561197960265728UL}{Random.Range(0, 100)}");
	}
    
	#region Help functions

	[ConsoleCommand("bp.test.find.shortname.by.prefab")]
	private void TestFindShortnameByPrefab(ConsoleSystem.Arg arg)
	{
		if (!arg.IsServerside) return;

		var prefabName = arg.GetString(0);
		if (string.IsNullOrEmpty(prefabName))
		{
			SendReply(arg, $"Please provide a prefab name. Usage: /{arg.cmd.FullName} <name>");
			return;
		}

		var shortPrefabName = Path.GetFileNameWithoutExtension(prefabName);

		SendReply(arg, $"Short name of prefab: {shortPrefabName}");
	}

	[ConsoleCommand("bp.test.mission.by.name")]
	private void TestMissionByName(ConsoleSystem.Arg arg)
	{
		if (!arg.IsServerside) return;

		var entityName = arg.GetString(0);
		if (string.IsNullOrEmpty(entityName))
		{
			SendReply(arg, "Please provide an entity name.");
			return;
		}
        
		var spawnEntityFromName = ConVar.Entity.GetSpawnEntityFromName(entityName);
		if (!spawnEntityFromName.Valid)
		{
			SendReply(arg, spawnEntityFromName.Error);
			return;
		}
		
		var spawnPoint = ServerMgr.FindSpawnPoint();
		if (spawnPoint == null) return;
        
		var entity = GameManager.server.CreateEntity(spawnEntityFromName.PrefabName, spawnPoint.pos, spawnPoint.rot);
		if (entity == null)
		{
			SendReply(arg, $"Couldn't spawn entity: {entityName}");
			return;
		}
		entity.enableSaving = false;
		entity.Spawn();
		entity.Invoke(() =>
		{
			entity.Kill();
		}, 5f);

		SendReply(arg, "Entity spawned!");
		
		var steamID = GetRandomSteamID();
		
		OnMissionsProgress(new BasePlayer()
		{
			userID = steamID,
			UserIDString = steamID.ToString(),
			displayName = $"{steamID}",
		}, MissionType.Kill, entity: entity);
	}

	[ConsoleCommand("bp.test.mission.look.by.shortname")]
	private void TestMissionLookByShortname(ConsoleSystem.Arg arg)
	{
		if (!arg.IsServerside) return;

		var shortName = arg.GetString(0);
		if (string.IsNullOrEmpty(shortName))
		{
			SendReply(arg, "Please provide an entity name.");
			return;
		}
        
		var item = ItemManager.CreateByName(shortName, 1);
		if (item == null)
		{
			SendReply(arg, "Couldn't create item with name: " + shortName); return;
		}
		
		SendReply(arg, "Item created!");

		timer.In(5f, () =>
		{
			item.RemoveFromWorld();
			item.RemoveFromContainer();
			item.Remove();
			
			ItemManager.DoRemoves();
		});
		
		var steamID = GetRandomSteamID();

		OnMissionsProgress(new BasePlayer()
		{
			userID = steamID,
			UserIDString = steamID.ToString(),
			displayName = $"{steamID}",
		}, MissionType.Look, item);
	}

	#endregion

#endif

	#endregion
}