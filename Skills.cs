#define USE_HARMONY

// #define TESTING

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;
using Analytics = Facepunch.Rust.Analytics;
using Random = UnityEngine.Random;
using Time = UnityEngine.Time;
using HarmonyLib;

#if CARBON
using Carbon.Base;
using Carbon.Modules;
#endif

namespace Oxide.Plugins;

[Info("Skills", "Mevent", "1.32.11")]
[Description("Adds a system of skills")]
public class Skills : RustPlugin
{
	#region Fields

	[PluginReference] private Plugin
		ImageLibrary = null,
		Notifications = null,
		EventHelper = null,
		Battles = null,
		Duel = null,
		Duelist = null,
		ArenaTournament = null,
		Notify = null,
		UINotify = null;

	private const bool LangRu = false;

	private static Skills _instance;

#if CARBON
    private ImageDatabaseModule imageDatabase;
#endif

	private bool _enabledImageLibrary;

	private const string
		Layer = "Com.Mevent.Main",
		PERM_Bypass = "skills.bypass",
		PERM_Wipe = "skills.wipe";

	private HashSet<ulong> _lootedContainers = new();

	private readonly Dictionary<ulong, float> _timeSinceLastMetabolism = new();

	private enum SkillType
	{
		Wood,
		Stones,
		Metal,
		Sulfur,
		Attack,
		Secure,
		Regeneration,
		Metabolism,
		ComponentsRates,
		StandUpChance,
		CraftSpeed,
		FastOven,
		Kits,
		None,
		Cloth,
		Butcher,
		Scrap,
		RecyclerSpeed,
		TransferWipe,
		MixingTableSpeed,
		Gather,
		CraftRates,
		CombatMedic,
		SafeFall,
		Durability

		//RecyclerChance,
		//PowerEffieciency
	}

	private readonly Dictionary<ulong, ulong> _playerByOven = new();

	#endregion

	#region Config

	private static Configuration _config;

	private class Configuration
	{
		[JsonProperty(PropertyName = LangRu ? "Разрешение (пример: skills.use)" : "Permission (example: skills.use)")]
		public string Permission = string.Empty;

		[JsonProperty(PropertyName = LangRu ? "Команда для открытия меню с навыками" : "Command")]
		public string Command = "skills";

		[JsonProperty(PropertyName = LangRu ? "Автоматический сброс данных?" : "Auto wipe?")]
		public bool Wipe = true;

		[JsonProperty(PropertyName =
			LangRu ? "Разрешить использование оповещений через плагин Notify?" : "Work with Notify?")]
		public bool UseNotify = true;

		[JsonProperty(PropertyName =
			LangRu
				? "Сохранять информацию о луте в файлах данных?"
				: "Store the data on the looted containers in the data-files?")]
		public bool StoreContainers = false;

		[JsonProperty(PropertyName = LangRu ? "Настройки экономики" : "Economy")]
		public EconomyConf Economy = new()
		{
			Type = EconomyType.Plugin,
			AddHook = "Deposit",
			BalanceHook = "Balance",
			RemoveHook = "Withdraw",
			Plug = "Economics",
			ShortName = "scrap",
			DisplayName = string.Empty,
			Skin = 0
		};

		[JsonProperty(PropertyName = LangRu ? "Уведомление о максимальном уровне" : "Maximum level notify")]
		public INotify MaxLevel = new()
		{
			Image = "warning",
			Url = "https://i.ibb.co/wBn7JzM/image.png",
			Delay = 0.9f
		};

		[JsonProperty(PropertyName = LangRu ? "Уведомление о нехватке денег" : "Out of balance notify")]
		public INotify NotMoney = new()
		{
			Image = "warning",
			Url = "https://i.ibb.co/wBn7JzM/image.png",
			Delay = 0.9f
		};

		[JsonProperty(PropertyName = LangRu ? "Фон" : "Background")]
		public IPanel Background = new()
		{
			AnchorMin = "0 0", AnchorMax = "1 1",
			OffsetMin = "0 0", OffsetMax = "0 0",
			Image = string.Empty,
			Color = new IColor("#0D1F4E", 95),
			isRaw = false,
			Sprite = "Assets/Content/UI/UI.Background.Tile.psd",
			Material = "Assets/Icons/IconMaterial.mat"
		};

		[JsonProperty(PropertyName = LangRu ? "Заголовок" : "Title")]
		public IText Title = new()
		{
			AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
			OffsetMin = "-150 300", OffsetMax = "150 360",
			Font = "robotocondensed-bold.ttf",
			Align = TextAnchor.MiddleCenter,
			FontSize = 38,
			Color = new IColor("#FFFFFF", 100)
		};

		[JsonProperty(PropertyName = LangRu ? "Назад" : "Back")]
		public IText Back = new()
		{
			AnchorMin = "0 0.5", AnchorMax = "0 0.5",
			OffsetMin = "0 -40", OffsetMax = "65 40",
			Font = "robotocondensed-bold.ttf",
			Align = TextAnchor.MiddleCenter,
			FontSize = 60,
			Color = new IColor("#FFFFFF", 100)
		};

		[JsonProperty(PropertyName = LangRu ? "Далее" : "Next")]
		public IText Next = new()
		{
			AnchorMin = "1 0.5", AnchorMax = "1 0.5",
			OffsetMin = "-65 -40", OffsetMax = "0 40",
			Font = "robotocondensed-bold.ttf",
			Align = TextAnchor.MiddleCenter,
			FontSize = 60,
			Color = new IColor("#FFFFFF", 100)
		};

		[JsonProperty(PropertyName = LangRu ? "Баланс" : "Balance")]
		public IText BalanceText = new()
		{
			AnchorMin = "0.5 0", AnchorMax = "0.5 0",
			OffsetMin = "-150 0", OffsetMax = "150 75",
			Font = "robotocondensed-regular.ttf",
			Align = TextAnchor.MiddleCenter,
			FontSize = 24,
			Color = new IColor("#FFFFFF", 100)
		};

		[JsonProperty(PropertyName = LangRu ? "Включить иконку баланса" : "Enable Balance Icon")]
		public bool EnableBalanceIcon = false;

		[JsonProperty(PropertyName = LangRu ? "Иконка баланса" : "Balance Icon")]
		public BalanceIcon BalanceIcon = new()
		{
			OffsetMinY = 20,
			OffsetMaxY = 55,
			Length = 35
		};

		[JsonProperty(PropertyName = LangRu ? "Закрыть" : "Close")]
		public IText CloseLabel = new()
		{
			AnchorMin = "1 1", AnchorMax = "1 1",
			OffsetMin = "-35 -35", OffsetMax = "-5 -5",
			Font = "robotocondensed-bold.ttf",
			Align = TextAnchor.MiddleCenter,
			FontSize = 24,
			Color = new IColor("#FFFFFF", 100)
		};

		[JsonProperty(PropertyName = LangRu ? "Панель навыка" : "Skill Panel")]
		public SkillPanel SkillPanel = new()
		{
			Background = new SIPanel
			{
				Image = string.Empty,
				Color = new IColor("#1D3676", 98),
				isRaw = false,
				Sprite = "Assets/Content/UI/UI.Background.Tile.psd",
				Material = "Assets/Icons/IconMaterial.mat",
				HeightCorrect = 5
			},
			Image = new InterfacePosition
			{
				AnchorMin = "0 0", AnchorMax = "0 0",
				OffsetMin = "10 10", OffsetMax = "160 160"
			},
			Title = new IText
			{
				AnchorMin = "0 0", AnchorMax = "1 0",
				OffsetMin = "165 135", OffsetMax = "-5 160",
				Align = TextAnchor.MiddleLeft,
				FontSize = 20,
				Font = "robotocondensed-bold.ttf",
				Color = new IColor("#FFFFFF", 100)
			},
			Description = new IText
			{
				AnchorMin = "0 0", AnchorMax = "1 0",
				OffsetMin = "165 30", OffsetMax = "-5 130",
				Align = TextAnchor.UpperLeft,
				FontSize = 14,
				Font = "robotocondensed-regular.ttf",
				Color = new IColor("#FFFFFF", 100)
			},
			Button = new IButton
			{
				AnchorMin = "1 0", AnchorMax = "1 0",
				OffsetMin = "-105 10", OffsetMax = "-10 30",
				AColor = new IColor("#B5FFC9", 100),
				DColor = new IColor("#B5FFC9", 25),
				TextUi = new IText
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "0 0", OffsetMax = "0 0",
					Align = TextAnchor.MiddleCenter,
					FontSize = 14,
					Font = "robotocondensed-regular.ttf",
					Color = new IColor("#1D3676", 100)
				}
			},
			DescriptionButton = new DescriptionButton
			{
				Enabled = false,
				AnchorMin = "1 0", AnchorMax = "1 0",
				OffsetMin = "-105 35", OffsetMax = "-10 55",
				AColor = new IColor("#B5FFC9", 100),
				DColor = new IColor("#B5FFC9", 25),
				TextUi = new IText
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "0 0", OffsetMax = "0 0",
					Align = TextAnchor.MiddleCenter,
					FontSize = 14,
					Font = "robotocondensed-regular.ttf",
					Color = new IColor("#1D3676", 100)
				}
			},
			Cost = new IText
			{
				AnchorMin = "1 0", AnchorMax = "1 0",
				OffsetMin = "-150 10", OffsetMax = "-110 30",
				Align = TextAnchor.MiddleRight,
				FontSize = 14,
				Font = "robotocondensed-regular.ttf",
				Color = new IColor("#FFFFFF", 100)
			},
			AddCost = "$",
			EnableCostImage = false,
			AddCostImage = new IPanel
			{
				AnchorMin = "1 0", AnchorMax = "1 0",
				OffsetMin = "-130 10", OffsetMax = "-110 30",
				Color = new IColor("#FFFFFF", 100),
				isRaw = true,
				Image = "https://i.ibb.co/yfBv8F5/image.png",
				Sprite = string.Empty,
				Material = string.Empty
			},
			Stages = new IProgress
			{
				AnchorMin = "0 0", AnchorMax = "0 0",
				OffsetMin = "160 10", OffsetMax = "160 10",
				Height = 20,
				Width = 10,
				Margin = 5,
				AColor = new IColor("#8C70D6", 100),
				DColor = new IColor("#8C70D6", 25)
			},
			Count = 6,
			Height = 170,
			Width = 565,
			Margin = 20,
			ShowAllStages = true
		};

		[JsonProperty(PropertyName = LangRu ? "Навыки" : "Skills",
			ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public List<SkillEntry> Skills = new()
		{
			new SkillEntry.SkillBuilder()
				.Enabled(true)
				.Type(SkillType.Wood)
				.Image("https://gspics.org/images/2020/09/02/xz6Fy.png")
				.Title("Woodman", "Дровосек")
				.Description("It's in charge of the tree mining\nWhen you learn the skill, the rate increases by x1",
					"Отвечает за добычу дерева.\nПри изучении навыка рейт добычи увеличивается на 1.")
				.AddStage(1, new StageConf(string.Empty, 10, 133, 0, new List<RequiredSkillStage>()))
				.AddStage(2, new StageConf(string.Empty, 25, 164, 0, new List<RequiredSkillStage>()))
				.AddStage(3, new StageConf(string.Empty, 40, 200, 0, new List<RequiredSkillStage>()))
				.Build(),
			new SkillEntry.SkillBuilder()
				.Enabled(true)
				.Type(SkillType.Stones)
				.Image("https://gspics.org/images/2020/09/02/xz9mX.png")
				.Title("Stone Miner", "Камнедобыча")
				.Description("It's in charge of the stones mining\nWhen you learn the skill, the rate increases by x1",
					"Отвечает за добычу камня.\nПри изучении навыка рейт добычи увеличивается на 1.")
				.AddStage(1, new StageConf(string.Empty, 10, 133, 0, new List<RequiredSkillStage>()))
				.AddStage(2, new StageConf(string.Empty, 25, 164, 0, new List<RequiredSkillStage>()))
				.AddStage(3, new StageConf(string.Empty, 40, 200, 0, new List<RequiredSkillStage>()))
				.Build(),
			new SkillEntry.SkillBuilder()
				.Enabled(true)
				.Type(SkillType.Metal)
				.Image("https://gspics.org/images/2020/09/02/xznT3.png")
				.Title("Metal Miner", "Чернорабочий")
				.Description(
					"It's in charge of the metal ore mining\nWhen you learn the skill, the rate increases by x1"
					, "Отвечает за добычу железной руды.\nПри изучении навыка рейт добычи увеличивается на 1.")
				.AddStage(1, new StageConf(string.Empty, 10, 133, 0, new List<RequiredSkillStage>()))
				.AddStage(2, new StageConf(string.Empty, 25, 164, 0, new List<RequiredSkillStage>()))
				.AddStage(3, new StageConf(string.Empty, 40, 200, 0, new List<RequiredSkillStage>()))
				.Build(),
			new SkillEntry.SkillBuilder()
				.Enabled(true)
				.Type(SkillType.Sulfur)
				.Image("https://gspics.org/images/2020/09/02/xz4te.png")
				.Title("Sulfure Miner", "Серодобытчик")
				.Description(
					"It's in charge of the sulfur ore mining\nWhen you learn the skill, the rate increases by x1"
					, "Отвечает за добычу серной руды.\nПри изучении навыка рейт добычи увеличивается на 1.")
				.AddStage(1, new StageConf(string.Empty, 10, 133, 0, new List<RequiredSkillStage>()))
				.AddStage(2, new StageConf(string.Empty, 25, 164, 0, new List<RequiredSkillStage>()))
				.AddStage(3, new StageConf(string.Empty, 40, 200, 0, new List<RequiredSkillStage>()))
				.Build(),
			new SkillEntry.SkillBuilder()
				.Enabled(true)
				.Type(SkillType.Attack)
				.Image("https://gspics.org/images/2020/09/02/xzXza.png")
				.Title("Attack", "Нападение")
				.Description(
					"Changes the damage of any weapon\nWhen you learn the skill, you'll increase your damage by 15%."
					, "Изменяет величину урона любого оружия.\nПри изучении навыка урон увеличивается на 15%.")
				.AddStage(1, new StageConf(string.Empty, 20, 5, 0, new List<RequiredSkillStage>()))
				.AddStage(2, new StageConf(string.Empty, 35, 10, 0, new List<RequiredSkillStage>()))
				.AddStage(3, new StageConf(string.Empty, 50, 15, 0, new List<RequiredSkillStage>()))
				.Build(),
			new SkillEntry.SkillBuilder()
				.Enabled(true)
				.Type(SkillType.Secure)
				.Image("https://gspics.org/images/2020/09/02/xzAfi.png")
				.Title("Secure", "Защита")
				.Description(
					"Changes the damage of any weapon\nWhen you learn the skill, you'll increase your protection by 15%."
					, "Изменяет величину урона любого оружия.\nПри изучении навыка ваша защита увеличится на 15%.")
				.AddStage(1, new StageConf(string.Empty, 20, 5, 0, new List<RequiredSkillStage>()))
				.AddStage(2, new StageConf(string.Empty, 35, 10, 0, new List<RequiredSkillStage>()))
				.AddStage(3, new StageConf(string.Empty, 50, 15, 0, new List<RequiredSkillStage>()))
				.Build(),
			new SkillEntry.SkillBuilder()
				.Enabled(true)
				.Type(SkillType.Regeneration)
				.Image("https://gspics.org/images/2020/09/02/xzGrO.png")
				.Title("Regeneration", "Регенерация")
				.Description(
					"Speed health regeneration\nWhen you learn the skill, you'll increase the rate of regeneration by 50%."
					, "Скоростная регенерация здоровья.\nПри изучении навыка, скорость регенерации увеличится на 90%.")
				.AddStage(1, new StageConf(string.Empty, 10, 0.5f, 0, new List<RequiredSkillStage>()))
				.AddStage(2, new StageConf(string.Empty, 15, 1, 0, new List<RequiredSkillStage>()))
				.AddStage(3, new StageConf(string.Empty, 20, 2, 0, new List<RequiredSkillStage>()))
				.Build(),
			new SkillEntry.SkillBuilder()
				.Enabled(true)
				.Type(SkillType.Metabolism)
				.Image("https://gspics.org/images/2020/09/02/xzWdI.png")
				.Title("Metabolism", "Метаболизм")
				.Description(
					"Changes the restoration of thirst and hunger\nWhen you learn the skill, you will get calories and hydration"
					, "Изменяет восстановление жажды и голода.\nПри изучении навыка вы получите восполнение калорий и гидратации.")
				.AddStage(1, new StageConf(string.Empty, 10, 0.5f, 0, new List<RequiredSkillStage>()))
				.AddStage(2, new StageConf(string.Empty, 15, 1, 0, new List<RequiredSkillStage>()))
				.AddStage(3, new StageConf(string.Empty, 20, 2, 0, new List<RequiredSkillStage>()))
				.Build(),
			new SkillEntry.SkillBuilder()
				.Enabled(true)
				.Type(SkillType.ComponentsRates)
				.Image("https://gspics.org/images/2020/09/02/xz15L.png")
				.Title("Rates to components", "Рейты на компоненты")
				.Description(
					"Changes the rates of extractive components\nWhen we learn the skill, the components found will be x4"
					, "Изменяет рейты добываемых компонентов.\nПосле изучения этого навыка найденные компоненты будут в четыре раза больше.")
				.AddStage(1, new StageConf(string.Empty, 10, 2, 0, new List<RequiredSkillStage>()))
				.AddStage(2, new StageConf(string.Empty, 15, 3, 0, new List<RequiredSkillStage>()))
				.AddStage(3, new StageConf(string.Empty, 20, 4, 0, new List<RequiredSkillStage>()))
				.Build(),
			new SkillEntry.SkillBuilder()
				.Enabled(true)
				.Type(SkillType.StandUpChance)
				.Image("https://gspics.org/images/2020/09/02/xzTDD.png")
				.Title("Increased chance to stand", "Увеличение шанса встать")
				.Description(
					"It's a bigger chance to get up when you're wounded\nWhen you learn the skill, the chance goes up from standard 20% to 50%."
					, "Увеличивает шанс подняться, когда вы ранены.\nПосле изучения этой способности шанс встать увеличивается с 20% до 50%.")
				.AddStage(1, new StageConf(string.Empty, 10, 30, 40, new List<RequiredSkillStage>()))
				.AddStage(2, new StageConf(string.Empty, 15, 40, 50, new List<RequiredSkillStage>()))
				.AddStage(3, new StageConf(string.Empty, 20, 50, 60, new List<RequiredSkillStage>()))
				.Build(),
			new SkillEntry.SkillBuilder()
				.Enabled(true)
				.Type(SkillType.CraftSpeed)
				.Image("https://i.ibb.co/0fB889w/fAti1Cj.png")
				.Title("Craft Speed", "Скорость крафта")
				.Description("Speeds up your craft.\nWhen you learn the skill, you can craft items in almost instant",
					"Ускоряет процесс крафта.\nПосле изучения этого навыка вы сможете крафтить почти моментально.")
				.AddStage(1, new StageConf(string.Empty, 10, 30, 0, new List<RequiredSkillStage>()))
				.AddStage(2, new StageConf(string.Empty, 15, 40, 0, new List<RequiredSkillStage>()))
				.AddStage(3, new StageConf(string.Empty, 20, 50, 0, new List<RequiredSkillStage>()))
				.Build(),
			new SkillEntry.SkillBuilder()
				.Enabled(true)
				.Type(SkillType.Kits)
				.Image("https://i.ibb.co/Tt8KxCT/Log7sQR.png")
				.Title("Kits Speed", "Ускорение наборов")
				.Description("Accelerates the delay between receiving kits",
					"Уменьшает задержку между получением наборов.")
				.AddStage(1, new StageConf(string.Empty, 10, 30, 0, new List<RequiredSkillStage>()))
				.AddStage(2, new StageConf(string.Empty, 15, 40, 0, new List<RequiredSkillStage>()))
				.AddStage(3, new StageConf(string.Empty, 20, 50, 0, new List<RequiredSkillStage>()))
				.Build(),
			new SkillEntry.SkillBuilder()
				.Enabled(true)
				.Type(SkillType.FastOven)
				.Image("https://i.ibb.co/PMBpNyf/IifHk5l.png")
				.Title("Fast Oven", "Ускорение печей")
				.Description("Accelerating furnace melting", "Увеличивает скорость плавки в печах.")
				.Build(),
			new SkillEntry.SkillBuilder()
				.Enabled(true)
				.Type(SkillType.None)
				.Image("https://i.ibb.co/6wdxmN8/RqdcAm0.png")
				.Title("Sorting", "Сортировка")
				.Description("With each stage, you discover new types of sorting"
					, "На каждом этапе открываются новые виды сортировки.")
				.AddStage(1,
					new StageConf(string.Empty, 25, 0, 0, new List<string>(), new List<string>(),
						new List<string> {"furnacesplitter.use"}, new List<RequiredSkillStage>()))
				.AddStage(2,
					new StageConf(string.Empty, 40, 0, 0, new List<string>(), new List<string>(),
						new List<string> {"activesort.use"}, new List<RequiredSkillStage>()))
				.Build(),
			new SkillEntry.SkillBuilder()
				.Enabled(true)
				.Type(SkillType.None)
				.ID(1)
				.Image("https://i.ibb.co/Lv7DxLd/S4xulmj.png")
				.Title("Teleportation", "Телепортация")
				.Description("You teleport faster with each stage"
					, "Вы телепортируетесь быстрее с каждым этапом.")
				.AddStage(1, new StageConf(string.Empty, 30, 0, 0, new List<string>(), new List<string>(),
					new List<string>
					{
						"nteleportation.vip"
					}, new List<RequiredSkillStage>()))
				.AddStage(2, new StageConf(string.Empty, 50, 0, 0, new List<string>(), new List<string>(),
					new List<string>
					{
						"nteleportation.premium"
					}, new List<RequiredSkillStage>()))
				.AddStage(3, new StageConf(string.Empty, 50, 0, 0, new List<string>(), new List<string>(),
					new List<string>
					{
						"nteleportation.deluxe"
					}, new List<RequiredSkillStage>()))
				.Build(),
			new SkillEntry.SkillBuilder()
				.Enabled(true)
				.Type(SkillType.Cloth)
				.Image("https://i.ibb.co/6tvGfSP/5fixMch.png")
				.Title("Cannabis Picker", "Сборщик конопли")
				.Description(
					"It's in charge of the collection of cloth\nWhen you learn the skill, the rate increases by x1"
					, "Отвечает за сбор ткани.\nИзучив способность, рейт добычи увеличивается на x1.")
				.AddStage(1, new StageConf(string.Empty, 10, 133, 0, new List<RequiredSkillStage>()))
				.AddStage(2, new StageConf(string.Empty, 25, 164, 0, new List<RequiredSkillStage>()))
				.AddStage(3, new StageConf(string.Empty, 40, 200, 0, new List<RequiredSkillStage>()))
				.Build(),
			new SkillEntry.SkillBuilder()
				.Enabled(true)
				.Type(SkillType.Butcher)
				.Image("https://i.ibb.co/5jr1cXC/4WlHK7u.png")
				.Title("Butcher", "Мясник")
				.Description("It's in charge of the animals prey\nWhen you learn the skill, the rate increases by x1"
					, "Отвечает за добычу животных.\nИзучив способность, рейт добычи увеличивается на x1.")
				.AddStage(1, new StageConf(string.Empty, 10, 133, 0, new List<RequiredSkillStage>()))
				.AddStage(2, new StageConf(string.Empty, 25, 164, 0, new List<RequiredSkillStage>()))
				.AddStage(3, new StageConf(string.Empty, 40, 200, 0, new List<RequiredSkillStage>()))
				.Build(),
			new SkillEntry.SkillBuilder()
				.Enabled(true)
				.Type(SkillType.Scrap)
				.Image("https://i.ibb.co/0CsgThq/SEvG4EU.png")
				.Title("Scrap", "Черный копатель")
				.Description("Changes scrap mining rates\nWhen we learn the skill, the components found will be x4"
					, "Изменяет рейты добычи скрапа.\nИзучив способность, найденный скрап будет x4.")
				.AddStage(1, new StageConf(string.Empty, 10, 2, 0, new List<RequiredSkillStage>()))
				.AddStage(2, new StageConf(string.Empty, 25, 3, 0, new List<RequiredSkillStage>()))
				.AddStage(3, new StageConf(string.Empty, 40, 4, 0, new List<RequiredSkillStage>()))
				.Build(),
			new SkillEntry.SkillBuilder()
				.Enabled(true)
				.Type(SkillType.RecyclerSpeed)
				.Image("https://i.ibb.co/py5bK98/31k3bM0.png")
				.Title("Recycler Speed", "Скорость Переработчика")
				.Description(
					"Changes rate of recycling components\nWhen we learn the skill, the components will be recycler 2 times faster"
					, "Изменение скорости переработки компонентов.\nИзучив способность, компоненты будут перерабатываться в 2 раза быстрее.")
				.AddStage(1, new StageConf(string.Empty, 10, 4, 0, new List<RequiredSkillStage>()))
				.AddStage(2, new StageConf(string.Empty, 25, 3, 0, new List<RequiredSkillStage>()))
				.AddStage(3, new StageConf(string.Empty, 40, 2.5f, 0, new List<RequiredSkillStage>()))
				.Build(),
			new SkillEntry.SkillBuilder()
				.Enabled(true)
				.Type(SkillType.TransferWipe)
				.Image("https://i.ibb.co/Gs5XvvK/W23JZVo.png")
				.Title("Transfer Wipe", "Защита от вайпа")
				.Description(
					"Transferring Skills to the Next Wipe\nWhen we learn this skill, skills will be carried over to the next wipe"
					, "Перенос способностей на следующий вайп.\nИзучив способность, все способности будут перенесены в следующий вайп.")
				.AddStage(1, new StageConf(string.Empty, 100, 0, 0, new List<RequiredSkillStage>()))
				.Build(),
			new SkillEntry.SkillBuilder()
				.Enabled(true)
				.Type(SkillType.MixingTableSpeed)
				.Image("https://i.ibb.co/0JJyt4H/W6uHKeU.png")
				.Title("Mixing Table Speed", "Скорость стола для смешивания")
				.Description(
					"Changes rate of mixing recipes\nWhen we learn the skill, the recipes will be mixed instantly"
					, "Изменение скорости смешивания рецептов.\nИзучив способность, рецепты будут смешиваться моментально.")
				.AddStage(1, new StageConf(string.Empty, 10, 0.8f, 0, new List<RequiredSkillStage>()))
				.AddStage(2, new StageConf(string.Empty, 15, 0.45f, 0, new List<RequiredSkillStage>()))
				.AddStage(3, new StageConf(string.Empty, 20, 0f, 0, new List<RequiredSkillStage>()))
				.Build(),
			new SkillEntry.SkillBuilder()
				.Enabled(true)
				.Type(SkillType.Gather)
				.ShortName("blue.berry|red.berry|white.berry|yellow.berry|green.berry|black.berry")
				.ID(3490432)
				.Image("https://i.ibb.co/J26dBz9/D8RsOS3.png")
				.Title("Berry Hunter", "Охотник за ягодами")
				.Description("It's in charge of picking blueberries\nWhen you learn the skill, the rate increases by x1"
					, "Увеличивает количество собираемых ягод.\nКогда вы изучите этот навык, рейт увеличится на x1.")
				.AddStage(1, new StageConf(string.Empty, 10, 133f, 0, new List<RequiredSkillStage>()))
				.AddStage(2, new StageConf(string.Empty, 25, 164f, 0, new List<RequiredSkillStage>()))
				.AddStage(3, new StageConf(string.Empty, 40, 200f, 0, new List<RequiredSkillStage>()))
				.Build(),
			new SkillEntry.SkillBuilder()
				.Enabled(true)
				.Type(SkillType.CraftRates)
				.Image("https://i.ibb.co/G5r1dQs/jnH4ELO.png")
				.Title("Craft Rates", "Улучшенный крафт")
				.Description(
					"Item crafting rates\nIf you learn this skill, you will have an increased chance of getting an increased amount of an item when crafting"
					, "Рейты крафта предметов.\nЕсли вы изучите этот навык, у вас будет повышенный шанс получить увеличенное количество предмета при его крафте.")
				.AddStage(1, new StageConf(string.Empty, 10, 140f, 10, new List<RequiredSkillStage>()))
				.AddStage(2, new StageConf(string.Empty, 25, 160f, 20, new List<RequiredSkillStage>()))
				.AddStage(3, new StageConf(string.Empty, 40, 200f, 30, new List<RequiredSkillStage>()))
				.Build(),
			new SkillEntry.SkillBuilder()
				.Enabled(true)
				.Type(SkillType.CombatMedic)
				.Image("https://i.ibb.co/hyhpcfZ/combat-medic.png")
				.Title("Combat Medic", "Боевой медик")
				.Description(
					"Fast recovery of health in battle.\nBy learning this skill, you increase the amount of healing health you receive when using bandages."
					, "Быстрое восстановление здоровья в бою.\nИзучая этот навык, вы увеличиваете количество исцеляющего здоровья, которое получаете при использовании бинтов.")
				.AddStage(1, new StageConf(string.Empty, 10, 40f, 0, new List<RequiredSkillStage>()))
				.AddStage(2, new StageConf(string.Empty, 25, 60f, 0, new List<RequiredSkillStage>()))
				.AddStage(3, new StageConf(string.Empty, 60, 100f, 0, new List<RequiredSkillStage>()))
				.Build(),
			new SkillEntry.SkillBuilder()
				.Enabled(true)
				.Type(SkillType.SafeFall)
				.Image("https://i.ibb.co/0hJs2sJ/safe-fall.png")
				.Title("Safe Fall", "Безопасное падение")
				.Description(
					"Reduces fall damage.\nBy learning this skill, you reduce the likelihood of serious injury when falling from height."
					, "Уменьшает повреждения при падении.\nОвладев этим навыком, вы уменьшите вероятность серьезных травм при падении с высоты.")
				.AddStage(1, new StageConf(string.Empty, 10, 40f, 0, new List<RequiredSkillStage>()))
				.AddStage(2, new StageConf(string.Empty, 25, 60f, 0, new List<RequiredSkillStage>()))
				.AddStage(3, new StageConf(string.Empty, 60, 100f, 0, new List<RequiredSkillStage>()))
				.Build(),

			new SkillEntry.SkillBuilder()
				.Enabled(true)
				.Type(SkillType.Durability)
				.Image("https://i.ibb.co/QryPdjN/durability.png")
				.Title("Durability", "Стойкость")
				.Description(
					"Allows you to reduce the breakage of items during use.\nBy learning this skill, your items will break less when used."
					, "Позволяет уменьшить поломку предметов во время использования.\nОвладев этим навыком, ваши предметы будут меньше ломаться при использовании.")
				.AddStage(1, new StageConf(string.Empty, 10, 40f, 0, new List<RequiredSkillStage>()))
				.AddStage(2, new StageConf(string.Empty, 25, 60f, 0, new List<RequiredSkillStage>()))
				.AddStage(3, new StageConf(string.Empty, 60, 100f, 0, new List<RequiredSkillStage>()))
				.Build()
		};
		
		[JsonProperty(PropertyName = LangRu ? "Животные" : "Animals",
			ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public List<string> Animals = new()
		{
			"chicken.corpse",
			"boar.corpse",
			"bear.corpse",
			"wolf.corpse",
			"stag.corpse",
			"polarbear.corpse"
		};

		[JsonProperty(PropertyName = LangRu ? "Черный список печей" : "Ovens Black List",
			ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public List<string> OvensBlackList = new()
		{
			"entity shortnameprefab 1",
			"entity shortnameprefab 2"
		};

		[JsonProperty(PropertyName = LangRu ? "Черный список контейнеров" : "Containers Black List",
			ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public List<string> ContainersBlackList = new()
		{
			"entity prefab name 1",
			"entity prefab name 2"
		};

		[JsonProperty(PropertyName = LangRu ? "Черный список для навыка атаки" : "Blacklist for Attack skill",
			ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public List<string> AttackBlackList = new()
		{
			"entity prefab name 1",
			"entity prefab name 2"
		};

		[JsonProperty(PropertyName = LangRu ? "Черный список оружия для навыка атаки" : "Weapon blacklist for Attack skill",
			ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public List<string> WeaponAttackBlackList = new()
		{
			"entity shortname name 1",
			"entity shortname name 2"
		};

		[JsonProperty(PropertyName =
			LangRu
				? "Дополнительные настройки для навыка Быстроой Печи"
				: "Additional settings for the Fast Oven skill")]
		public FastOvenSkill FastOven = new()
		{
			Stages = new Dictionary<int, FastOvenStage>
			{
				[1] = new()
				{
					Permission = string.Empty,
					Cost = 10,
					Rates = new Dictionary<string, float>
					{
						{"ore", 1},
						{"meat", 1},
						{"hq.metal.ore", 1},
						{"wood", 1},
						{"*", 1}
					},
					RequiredSkillStages = new List<RequiredSkillStage>
					{
						new()
						{
							ID = 0,
							Type = SkillType.CraftSpeed,
							Stage = 1,
							RequireSkill = false
						}
					}
				},
				[2] = new()
				{
					Permission = string.Empty,
					Cost = 15,
					Rates = new Dictionary<string, float>
					{
						{"ore", 2},
						{"meat", 2},
						{"hq.metal.ore", 2},
						{"wood", 2},
						{"*", 2}
					},
					RequiredSkillStages = new List<RequiredSkillStage>()
				},
				[3] = new()
				{
					Permission = string.Empty,
					Cost = 20,
					Rates = new Dictionary<string, float>
					{
						{"ore", 3},
						{"meat", 3},
						{"hq.metal.ore", 3},
						{"wood", 3},
						{"*", 3}
					},
					RequiredSkillStages = new List<RequiredSkillStage>()
				}
			}
		};

		[JsonProperty(PropertyName =
			LangRu
				? "Дополнительные настройки для навыка Компоненты Рейты"
				: "Additional settings for the Components Rates skill")]
		public ComponentsRatesSkill ComponentsRates = new()
		{
			AllowedCategories = new List<ItemCategory>
			{
				ItemCategory.Component
			},
			AllowedItems = new List<string>
			{
				"targeting.computer",
				"cctv.camera"
			},
			BlockList = new List<string>
			{
				"glue",
				"techparts"
			}
		};

		[JsonProperty(PropertyName = LangRu ? "Настройка Дополнительных Кнопок" : "Additional Buttons Settings")]
		public AdditionalButtonsSettings AdditionalButtons = new()
		{
			Buttons = new List<AdditionalButtonsSettings.AdditinalButton>
			{
				new(false, "https://i.ibb.co/X8btTQV/C83Rprq.png",
					"leaders",
					true)
			},
			UI = new AdditionalButtonsSettings.ButtonsUI
			{
				SideIndent = 45,
				UpIndent = 5,
				Size = 30,
				Margin = 5
			}
		};

		[JsonProperty(PropertyName =
			LangRu
				? "Дополнительные настройки для скилла Transfer Wipe"
				: "Additional settings for the Transfer Wipe skill")]
		public TransferWipeSkill TransferWipe = new()
		{
			ResetSingle = false
		};

		[JsonProperty(PropertyName =
			LangRu
				? "Дополнительные настройки для скилла Craft Rates"
				: "Additional settings for the Craft Rates skill")]
		public CraftRatesSkill CraftRates = new()
		{
			AllowedList = new List<string>
			{
				"gunpowder",
				"lowgradefuel",
				"rock"
			}
		};

		[JsonProperty(PropertyName =
			LangRu
				? "Дополнительные настройки для скилла CombatMedic"
				: "Additional settings for the CombatMedic skill")]
		public CombatMedicSkill CombatMedic = new()
		{
			AllowedItems = new Dictionary<string, bool>
			{
				["bandage"] = true,
				["syringe.medical"] = true,
				["largemedkit"] = true
			}
		};

		[JsonProperty(PropertyName = LangRu ? "Игнорировать скилл Attack на дуэли?" : "Ignore Attack skill on duels?")]
		public bool IgnoreAttackSkillOnDuel = true;
		
		[JsonProperty(PropertyName = LangRu ? "Игнорировать скилл Secure на дуэли?" : "Ignore Secure skill on duels?")]
		public bool IgnoreSecureSkillOnDuel = true;
		
		[JsonProperty(PropertyName = LangRu ? "Настройка Оповещений" : "Broadcasting Settings")]
		public Broadcasting Broadcasting = new()
		{
			UseLearnSkill = true
		};

		[JsonProperty(PropertyName = LangRu ? "Настройка логирования" : "Logging Settings")]
		public LoggingSettings Logging = new()
		{
			UsePlayerActionLogging = true
		};
		
		[JsonProperty(PropertyName =
			LangRu ? "Использовать бонусный режим для навыков добычи?" : "Use bonus mode for mining skills?")]
		public bool UseBonusMode = false;

		[JsonProperty(
			PropertyName = LangRu
				? "Черный список предметов для навыка Durability"
				: "Blacklist of items for Durability skills",
			ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public HashSet<string> DurabilityBlacklist = new()
		{
			"keycard_blue",
			"keycard_red",
			"keycard_green"
		};

		public VersionNumber Version;
	}

	private class LoggingSettings
	{
		[JsonProperty(PropertyName = LangRu ? "Использовать логирование действий игороков?" : "Use player action logging?")]
		public bool UsePlayerActionLogging;
	}
	
	private class Broadcasting
	{
		[JsonProperty(PropertyName =
			LangRu ? "Включить оповещения при изучении навыка?" : "Enable broadcast when learning a skill?")]
		public bool UseLearnSkill;

		public void LearnSkill(BasePlayer player, SkillEntry skill, int stage)
		{
			if (!UseLearnSkill) return;

			foreach (var target in BasePlayer.activePlayerList)
			{
				if (target.EqualNetID(player))
					continue;

				target.ChatMessage(_instance.Msg(target, BroadcastLearnSkill)
					.Replace("{username}", player.displayName)
					.Replace("{skill_name}", skill.Title)
					.Replace("{stage}", stage.ToString()));
			}
		}
	}

	private class TransferWipeSkill
	{
		[JsonProperty(PropertyName = LangRu ? "Сброс после однократного использования?" : "Reset after a single use?")]
		public bool ResetSingle;
	}

	private class ComponentsRatesSkill
	{
		[JsonProperty(PropertyName = LangRu ? "Разрешенные категории" : "Allowed Categories",
			ObjectCreationHandling = ObjectCreationHandling.Replace,
			ItemConverterType = typeof(StringEnumConverter))]
		public List<ItemCategory> AllowedCategories = new();

		[JsonProperty(PropertyName = LangRu ? "Разрешенные предметы" : "Allowed Items",
			ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public List<string> AllowedItems = new();

		[JsonProperty(PropertyName = LangRu ? "Блок-лист компонентов (shortname)" : "Components Block List (shortname)",
			ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public List<string> BlockList = new();
	}

	private class AdditionalButtonsSettings
	{
		[JsonProperty(PropertyName = LangRu ? "Кнопки" : "Buttons",
			ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public List<AdditinalButton> Buttons = new();

		[JsonProperty(PropertyName = LangRu ? "Настройка Интерфейса" : "Interface Settings")]
		public ButtonsUI UI;

		public class AdditinalButton
		{
			[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = LangRu ? "Изображение" : "Image")]
			public string Image;

			[JsonProperty(PropertyName = LangRu ? "Команда" : "Command")]
			public string Command;

			[JsonProperty(PropertyName = LangRu ? "Закрыть меню" : "Close Menu")]
			public bool CloseMenu;

			public AdditinalButton(bool enabled, string image, string command, bool closeMenu)
			{
				Enabled = enabled;
				Image = image;
				Command = command;
				CloseMenu = closeMenu;
			}
		}

		public class ButtonsUI
		{
			[JsonProperty(PropertyName = LangRu ? "Боковой отступ" : "Side Indent")]
			public float SideIndent;

			[JsonProperty(PropertyName = LangRu ? "Отступ вверху" : "Up Indent")]
			public float UpIndent;

			[JsonProperty(PropertyName = LangRu ? "Расстояние между" : "Margin")]
			public float Margin;

			[JsonProperty(PropertyName = LangRu ? "Размер" : "Size")]
			public float Size;
		}
	}

	private class BalanceIcon
	{
		[JsonProperty(PropertyName = LangRu ? "Минимальное смещение по оси Y" : "Offset Min Y")]
		public float OffsetMinY;

		[JsonProperty(PropertyName = LangRu ? "Максимальное смещение по оси Y" : "Offset Max Y")]
		public float OffsetMaxY;

		[JsonProperty(PropertyName = LangRu ? "Длина" : "Length")]
		public float Length;
	}

	private class INotify
	{
		[JsonProperty(PropertyName = LangRu ? "Ключ изображения" : "Image Key")]
		public string Image;

		[JsonProperty(PropertyName = LangRu ? "Ссылка на изображение" : "Image Url")]
		public string Url;

		[JsonProperty(PropertyName = LangRu ? "Время показа" : "Show Time")]
		public float Delay;
	}

	private enum EconomyType
	{
		Plugin,
		Item
	}

	private class EconomyConf
	{
		#region Fields

		[JsonProperty(PropertyName = LangRu ? "Тип (Plugin/Item)" : "Type (Plugin/Item)")]
		[JsonConverter(typeof(StringEnumConverter))]
		public EconomyType Type;

		[JsonProperty(PropertyName = LangRu ? "Название используемого плагина экономики" : "Plugin name")]
		public string Plug;

		[JsonProperty(PropertyName = LangRu ? "Функция пополнения баланса" : "Balance add hook")]
		public string AddHook;

		[JsonProperty(PropertyName = LangRu ? "Функция снятия баланса" : "Balance remove hook")]
		public string RemoveHook;

		[JsonProperty(PropertyName = LangRu ? "Функция отображения баланса" : "Balance show hook")]
		public string BalanceHook;

		[JsonProperty(PropertyName = LangRu ? "Краткое имя предмета" : "ShortName")]
		public string ShortName;

		[JsonProperty(PropertyName =
			LangRu ? "Пользовательское название предмета (пусто - стандартное)" : "Display Name (empty - default)")]
		public string DisplayName;

		[JsonProperty(PropertyName = LangRu ? "Идентификатор скина предмета" : "Skin")]
		public ulong Skin;

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

					return Math.Round(Convert.ToDouble(plugin.Call(BalanceHook, player.userID.Get())), 2);
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
						case "Economics":
							plugin.Call(AddHook, player.userID.Get(), amount);
							break;
						default:
							plugin.Call(AddHook, player.userID.Get(), (int) amount);
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
						case "Economics":
							plugin.Call(RemoveHook, player.userID.Get(), amount);
							break;
						default:
							plugin.Call(RemoveHook, player.userID.Get(), (int) amount);
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

		private void Take(IEnumerable<Item> itemList, string shortname, ulong skinId, int iAmount)
		{
			var num1 = 0;
			if (iAmount == 0) return;

			var list = Pool.Get<List<Item>>();

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

		#endregion
	}

	private class CombatMedicSkill
	{
		[JsonProperty(PropertyName =
			LangRu ? "Список предметов, на которые будет действовать скилл" : "List of items affected by the skill", ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public Dictionary<string, bool> AllowedItems = new();
	}
	
	private class CraftRatesSkill
	{
		[JsonProperty(PropertyName = LangRu ? "Список разрешенных предметов" : "Allowed List",
			ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public List<string> AllowedList = new();
	}

	private class FastOvenSkill : ISkill
	{
		#region Fields

		[JsonProperty(PropertyName = LangRu ? "Работа с кострами" : "Work with campfires")]
		public bool WorkWithCampfires = true;

		[JsonProperty(PropertyName = LangRu ? "Множитель угля" : "Charcoal multiplier")]
		public float CharcoalMultiplier = 2;

		[JsonProperty(PropertyName = LangRu ? "Перестаньте сжигать пищу" : "Stop burning food")]
		public bool StopBurningFood = true;

		[JsonProperty(PropertyName = LangRu ? "Уровни" : "Stages",
			ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public Dictionary<int, FastOvenStage> Stages;

		#endregion

		#region Public Methods

		public FastOvenStage GetStage(BasePlayer player)
		{
			if (player == null) return null;

			var skillData = PlayerData.GetOrLoad(player.UserIDString)?.GetSkillData(SkillType.FastOven);
			if (skillData == null) return null;

			return Stages.GetValueOrDefault(skillData.Stage);
		}

		public Dictionary<int, IStage> GetStages(string userid)
		{
			return Stages.Where(x => x.Value.HasAccess(userid)).ToDictionary(x => x.Key, y => (IStage) y.Value);
		}

		#endregion
	}

	private class FastOvenStage : IStage
	{
		#region Fields

		[JsonProperty(PropertyName = LangRu ? "Разрешение (пример: skills.vip)" : "Permission (ex: skills.vip)")]
		public string Permission;

		[JsonProperty(PropertyName = LangRu ? "Стоимость" : "Cost")]
		public float Cost;

		[JsonProperty(PropertyName = LangRu ? "Рейты" : "Rates",
			ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public Dictionary<string, float> Rates = new();

		[JsonProperty(PropertyName = LangRu ? "Требуемые стадии скиллов" : "Required skill stages",
			ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public List<RequiredSkillStage> RequiredSkillStages = new();

		#endregion

		#region Public Methods

		public float GetCost()
		{
			return Cost;
		}

		public bool HasAccess(string userid)
		{
			return _config.SkillPanel.ShowAllStages || HasPermissions(userid);
		}

		public bool HasPermissions(string userid)
		{
			return string.IsNullOrEmpty(Permission) ||
			       _instance.permission.UserHasPermission(userid, Permission);
		}

		public List<RequiredSkillStage> GetRequiredSkillStages()
		{
			return RequiredSkillStages;
		}

		#endregion
	}

	private class SkillEntry : ISkill
	{
		#region Fields

		[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
		public bool Enabled;

		[JsonProperty(PropertyName = LangRu ? "Разрешение" : "Permission")]
		public string Permission = string.Empty;

		[JsonProperty(PropertyName = LangRu ? "Тип" : "Type")] [JsonConverter(typeof(StringEnumConverter))]
		public SkillType Type;

		[JsonProperty(PropertyName = LangRu ? "ИД (для None)" : "ID (for None)")]
		public int ID;

		[JsonProperty(PropertyName = LangRu ? "Изображение" : "Image")]
		public string Image;

		[JsonProperty(PropertyName = LangRu ? "Название" : "Title")]
		public string Title;

		[JsonProperty(PropertyName = LangRu? "Локализация названия" : "Title localization")]
		public Localization TitleLocalization = new();
		
		[JsonProperty(PropertyName = LangRu ? "Описание" : "Description")]
		public string Description;

		[JsonProperty(PropertyName = LangRu? "Локализация описания" : "Description localization")]
		public Localization DescriptionLocalization = new();

		[JsonProperty(PropertyName = LangRu ? "Короткое имя" : "Shortname")]
		public string ShortName;

		[JsonProperty(PropertyName = LangRu ? "Уровни" : "Stages",
			ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public Dictionary<int, StageConf> Stages;

		#endregion

		#region Public Methods

		public Dictionary<int, IStage> GetStages(string userid)
		{
			return Stages.Where(x => x.Value.HasAccess(userid)).ToDictionary(x => x.Key, y => (IStage) y.Value);
		}
		
		public string GetTitle(BasePlayer player)
		{
			if (TitleLocalization is {Enabled: true})
				return TitleLocalization.GetMessage(player);

			return Title;
		}
		
		public string GetDescription(BasePlayer player)
		{
			if (DescriptionLocalization is {Enabled: true})
				return DescriptionLocalization.GetMessage(player);

			return Description;
		}
		
		#endregion

		#region Builder

		public class SkillBuilder
		{
			private SkillEntry _skill = new()
			{
				Enabled = false,
				Permission = string.Empty,
				Type = SkillType.Wood,
				ID = 0,
				Image = string.Empty,
				Title = string.Empty,
				Description = string.Empty,
				ShortName = string.Empty,
				Stages = new Dictionary<int, StageConf>()
			};

			public SkillBuilder Enabled(bool enabled)
			{
				_skill.Enabled = enabled;
				return this;
			}

			public SkillBuilder Permission(string permission)
			{
				_skill.Permission = permission;
				return this;
			}

			public SkillBuilder Type(SkillType type)
			{
				_skill.Type = type;
				return this;
			}

			public SkillBuilder ID(int id)
			{
				_skill.ID = id;
				return this;
			}

			public SkillBuilder ShortName(string shortName)
			{
				_skill.ShortName = shortName;
				return this;
			}

			public SkillBuilder Image(string image)
			{
				_skill.Image = image;
				return this;
			}

			public SkillBuilder Title(string enTitle, string ruTitle)
			{
				_skill.Title = LangRu ? ruTitle : enTitle;
				_skill.TitleLocalization = new Localization
				{
					Enabled = false,
					Messages = new Dictionary<string, string>
					{
						["en"] = enTitle,
						["fr"] = enTitle,
						["ru"] = ruTitle,
					}
				};
				return this;
			}

			public SkillBuilder Description(string enDescription, string ruDescription)
			{
				_skill.Description = LangRu ? ruDescription : enDescription;
				_skill.DescriptionLocalization = new Localization
				{
					Enabled = false,
					Messages = new Dictionary<string, string>
					{
						["en"] = enDescription,
						["fr"] = enDescription,
						["ru"] = ruDescription,
					}
				};

				return this;
			}

			public SkillBuilder AddStage(int stage, StageConf stageConf)
			{
				_skill.Stages ??= new Dictionary<int, StageConf>();

				_skill.Stages.TryAdd(stage, stageConf);
				return this;
			}

			public SkillEntry Build()
			{
				return _skill;
			}
		}

		#endregion
	}
	
	private class Localization
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

	private class RequiredSkillStage
	{
		#region Fields

		[JsonProperty(PropertyName = LangRu ? "Тип" : "Type")] [JsonConverter(typeof(StringEnumConverter))]
		public SkillType Type;

		[JsonProperty(PropertyName = LangRu ? "ИД" : "ID")]
		public int ID;

		[JsonProperty(PropertyName = LangRu ? "Уровень" : "Stage")]
		public int Stage;

		[JsonProperty(PropertyName =
			LangRu
				? "Требуется наличие этого навыка? (в противном случае будет проверен только уровень)"
				: "Require the presence of this skill? (otherwise only the stage will be checked)")]
		public bool RequireSkill;

		#endregion

		#region Public Methods

		public bool? Has(BasePlayer player)
		{
			var skill = _config.Skills.Find(x => x.Type == Type && x.ID == ID);
			if (skill == null) return null;

			var dataSkill = PlayerData.GetOrLoad(player.UserIDString)?.GetSkillData(Type, ID);
			if (dataSkill == null)
				return RequireSkill ? false : null;

			return dataSkill.Stage >= Stage;
		}

		#endregion
	}

	private class StageConf : IStage
	{
		#region Fields

		[JsonProperty(PropertyName = LangRu ? "Разрешение (пример: skills.vip)" : "Permission (ex: skills.vip)")]
		public string Permission;

		[JsonProperty(PropertyName = LangRu ? "Стоимость" : "Cost")]
		public float Cost;

		[JsonProperty(PropertyName =
			LangRu
				? "Значение [метаболизм - значение, шанс встать - шанс, для всех остальных процент %]"
				: "Value [metabolism - value, for everyone else %]")]
		public float Value;

		[JsonProperty(PropertyName = LangRu ? "Значение 2" : "Value 2")]
		public float Value2;

		[JsonProperty(PropertyName = LangRu ? "Команды" : "Commands",
			ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public List<string> Commands;

		[JsonProperty(PropertyName = LangRu ? "Группы" : "Groups",
			ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public List<string> Groups;

		[JsonProperty(PropertyName = LangRu ? "Разрешения" : "Permissions",
			ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public List<string> Permissions;

		[JsonProperty(PropertyName = LangRu ? "Требуемые стадии скиллов" : "Required skill stages",
			ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public List<RequiredSkillStage> RequiredSkillStages;

		#endregion

		#region Constructors

		public StageConf(string permission = "", float cost = 0f, float value = 0f, float value2 = 0f,
			List<RequiredSkillStage> requiredSkillStages = null)
		{
			Permission = permission;
			Cost = cost;
			Value = value;
			Value2 = value2;
			RequiredSkillStages = requiredSkillStages ?? new List<RequiredSkillStage>();
			Commands = new List<string>();
			Groups = new List<string>();
			Permissions = new List<string>();
		}

		[JsonConstructor]
		public StageConf(string permission, float cost, float value, float value2, List<string> commands,
			List<string> groups,
			List<string> permissions, List<RequiredSkillStage> requiredSkillStages)
		{
			Permission = permission;
			Cost = cost;
			Value = value;
			Value2 = value2;
			Commands = commands;
			Groups = groups;
			Permissions = permissions;
			RequiredSkillStages = requiredSkillStages;
		}

		#endregion

		#region Public Methods

		public bool HasAccess(string userid)
		{
			return _config.SkillPanel.ShowAllStages || HasPermissions(userid);
		}

		public bool HasPermissions(string userid)
		{
			return string.IsNullOrEmpty(Permission) ||
			       _instance.permission.UserHasPermission(userid, Permission);
		}

		public float GetCost()
		{
			return Cost;
		}

		public List<RequiredSkillStage> GetRequiredSkillStages()
		{
			return RequiredSkillStages;
		}

		#endregion
	}

	private class SkillPanel
	{
		#region Fields

		[JsonProperty(PropertyName = LangRu ? "Фон" : "Background")]
		public SIPanel Background;

		[JsonProperty(PropertyName = LangRu ? "Изображение" : "Image")]
		public InterfacePosition Image;

		[JsonProperty(PropertyName = LangRu ? "Заглавие" : "Title")]
		public IText Title;

		[JsonProperty(PropertyName = LangRu ? "Описание" : "Description")]
		public IText Description;

		[JsonProperty(PropertyName = LangRu ? "Кнопка" : "Button")]
		public IButton Button;

		[JsonProperty(PropertyName = LangRu ? "Кнопка описания" : "Description Button")]
		public DescriptionButton DescriptionButton;

		[JsonProperty(PropertyName = LangRu ? "Стоимость" : "Cost")]
		public IText Cost;

		[JsonProperty(PropertyName = LangRu ? "Приписка к стоимости" : "Add to cost")]
		public string AddCost;

		[JsonProperty(PropertyName =
			LangRu ? "Включить пририску к стоимости (изображение)" : "Enable Add to cost (image)")]
		public bool EnableCostImage;

		[JsonProperty(PropertyName = LangRu ? "Приписка к стоимости (изображение)" : "Add to cost (image)")]
		public IPanel AddCostImage;

		[JsonProperty(PropertyName = LangRu ? "Уровни" : "Stages")]
		public IProgress Stages;

		[JsonProperty(PropertyName = LangRu ? "Количество на страницу" : "Count On Page")]
		public int Count;

		[JsonProperty(PropertyName = LangRu ? "Высота" : "Height")]
		public float Height;

		[JsonProperty(PropertyName = LangRu ? "Ширина" : "Width")]
		public float Width;

		[JsonProperty(PropertyName = LangRu ? "Отступ" : "Margin")]
		public float Margin;

		[JsonProperty(PropertyName = LangRu ? "Показывать все стадии скиллов" : "Show all stages of skills")]
		public bool ShowAllStages;

		#endregion

		#region Public Methods

		public void Get(ref CuiElementContainer container,
			PlayerData data,
			string layer,
			int page,
			string parent,
			int index,
			BasePlayer player,
			SkillEntry skill,
			float oMinX, float oMinY, float oMaxX, float oMaxY,
			bool hasBypass)
		{
			Background?.Get(ref container, parent, parent + $".Skill.{index}", oMinX, oMinY, oMaxX, oMaxY);

			container.Add(new CuiElement
			{
				Parent = parent + $".Skill.{index}",
				Components =
				{
					new CuiRawImageComponent
						{Png = _instance?.GetImage( skill.Image)},
					new CuiRectTransformComponent
					{
						AnchorMin = Image.AnchorMin, AnchorMax = Image.AnchorMax, OffsetMin = Image.OffsetMin,
						OffsetMax = Image.OffsetMax
					}
				}
			});

			Title?.Get(ref container, parent + $".Skill.{index}", parent + $".Skill.{index}.Title", skill.GetTitle(player));

			if (data?.CanSeeDescription(skill) == true)
				Description?.Get(ref container, parent + $".Skill.{index}", parent + $".Skill.{index}.Desciprition",
					skill.GetDescription(player));

			var cost = _instance?.GetNextPrice(player, skill) ?? -1;

			Button?.Get(ref container, player, parent + $".Skill.{index}", parent + $".Skill.{index}.Upgrade.Btn",
				$"UI_Skills upgrade {skill.Type} {page} {layer} {skill.ID}", "", cost > 0);

			if (DescriptionButton.Enabled)
				DescriptionButton?.Get(ref container, player, parent + $".Skill.{index}",
					parent + $".Skill.{index}.Description.Btn",
					$"UI_Skills changedescview {skill.Type} {page} {layer} {skill.ID}");

			if (cost > 0)
			{
				Cost?.Get(ref container, parent + $".Skill.{index}", parent + $".Skill.{index}.Cost",
					$"{cost}{AddCost}");

				if (EnableCostImage)
					AddCostImage?.Get(ref container, parent + $".Skill.{index}");
			}

			#region Stages

			container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = Stages.AnchorMin, AnchorMax = Stages.AnchorMax,
						OffsetMin = Stages.OffsetMin, OffsetMax = Stages.OffsetMax
					},
					Image = {Color = "0 0 0 0"}
				}, parent + $".Skill.{index}", parent + $".Skill.{index}.Stages");

			var skillData = PlayerData.GetOrCreate(player.UserIDString)?.Skills
				?.FirstOrDefault(x => x.Type == skill.Type && x.ID == skill.ID);

			var xSwitch = 0f;
			foreach (var stage in GetSkillStages(skill, player.UserIDString).Keys)
			{
				var color =
					hasBypass ? Stages.AColor
					: skillData != null && stage <= skillData.Stage ? Stages.AColor : Stages.DColor;

				container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "0 0",
							OffsetMin = $"{xSwitch} 0", OffsetMax = $"{xSwitch + Stages.Width} {Stages.Height}"
						},
						Image = {Color = color.Get()}
					}, parent + $".Skill.{index}.Stages", parent + $".Skill.{index}.Stages.{stage}");

				xSwitch += Stages.Width + Stages.Margin;
			}

			#endregion
		}

		#endregion
	}

	private class IProgress : InterfacePosition
	{
		[JsonProperty(PropertyName = LangRu ? "Высота" : "Height")]
		public float Height;

		[JsonProperty(PropertyName = LangRu ? "Ширина" : "Weidth")]
		public float Width;

		[JsonProperty(PropertyName = LangRu ? "Отступ" : "Margin")]
		public float Margin;

		[JsonProperty(PropertyName = LangRu ? "Активный цвет" : "Active Color")]
		public IColor AColor;

		[JsonProperty(PropertyName = LangRu ? "Не активный цвет" : "No Active Color")]
		public IColor DColor;
	}

	private class IButton : InterfacePosition
	{
		[JsonProperty(PropertyName = LangRu ? "Цвет" : "Color")]
		public IColor AColor;

		[JsonProperty(PropertyName = LangRu ? "Не активный цвет" : "No Active Color")]
		public IColor DColor;

		[JsonProperty(PropertyName = LangRu ? "Настройка текста" : "Text Setting")]
		public IText TextUi;

		public void Get(ref CuiElementContainer container, BasePlayer player, string parent, string name = null,
			string cmd = "",
			string close = "", bool color = true)
		{
			if (string.IsNullOrEmpty(name))
				name = CuiHelper.GetGuid();

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin,
					OffsetMax = OffsetMax
				},
				Image = {Color = color ? AColor.Get() : DColor.Get()}
			}, parent, name);

			TextUi?.Get(ref container, name, null, _instance.Msg(Upgrade, player.UserIDString));

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text = {Text = ""},
				Button =
				{
					Command = cmd,
					Close = close,
					Color = "0 0 0 0"
				}
			}, name);
		}
	}

	private class DescriptionButton : InterfacePosition
	{
		[JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
		public bool Enabled;

		[JsonProperty(PropertyName = LangRu ? "Цвет" : "Color")]
		public IColor AColor;

		[JsonProperty(PropertyName = LangRu ? "Не активный цвет" : "No Active Color")]
		public IColor DColor;

		[JsonProperty(PropertyName = LangRu ? "Настройки текста" : "Text Setting")]
		public IText TextUi;

		public void Get(ref CuiElementContainer container, BasePlayer player, string parent, string name = null,
			string cmd = "", bool active = true)
		{
			if (string.IsNullOrEmpty(name))
				name = CuiHelper.GetGuid();

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin,
					OffsetMax = OffsetMax
				},
				Image = {Color = active ? AColor.Get() : DColor.Get()}
			}, parent, name);

			TextUi?.Get(ref container, name, null, _instance.Msg(DescriptionBtn, player.UserIDString));

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text = {Text = ""},
				Button =
				{
					Command = cmd,
					Color = "0 0 0 0"
				}
			}, name);
		}
	}

	private class SIPanel
	{
		[JsonProperty(PropertyName = LangRu ? "Изображение" : "Image")]
		public string Image;

		[JsonProperty(PropertyName = LangRu ? "Цвет" : "Color")]
		public IColor Color;

		[JsonProperty(PropertyName = LangRu ? "Сохранять цвет изображения?" : "Save Image Color")]
		public bool isRaw;

		[JsonProperty(PropertyName = LangRu ? "Спрайт" : "Sprite")]
		public string Sprite;

		[JsonProperty(PropertyName = LangRu ? "Материал" : "Material")]
		public string Material;

		[JsonProperty(PropertyName = LangRu ? "Отклонение по высоте" : "Height Correction")]
		public float HeightCorrect;

		public void Get(ref CuiElementContainer container, string parent = "Hud", string name = null,
			float oMinX = 0, float oMinY = 0, float oMaxX = 0, float oMaxY = 0)
		{
			if (string.IsNullOrEmpty(name))
				name = CuiHelper.GetGuid();

			if (isRaw)
				container.Add(new CuiElement
				{
					Name = name,
					Parent = parent,
					Components =
					{
						new CuiRawImageComponent
						{
							Png = !string.IsNullOrEmpty(Image)
								? _instance.GetImage( Image)
								: null,
							Color = Color.Get(),
							Material = Material,
							Sprite = !string.IsNullOrEmpty(Sprite) ? Sprite : "Assets/Icons/rust.png"
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
							OffsetMin = $"{oMinX} {oMinY + HeightCorrect}",
							OffsetMax = $"{oMaxX} {oMaxY + HeightCorrect}"
						}
					}
				});
			else
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
						OffsetMin = $"{oMinX} {oMinY + HeightCorrect}",
						OffsetMax = $"{oMaxX} {oMaxY + HeightCorrect}"
					},
					Image =
					{
						Png = !string.IsNullOrEmpty(Image)
							? _instance.GetImage( Image)
							: null,
						Color = Color.Get(),
						Sprite =
							!string.IsNullOrEmpty(Sprite) ? Sprite : "Assets/Content/UI/UI.Background.Tile.psd",
						Material = !string.IsNullOrEmpty(Material) ? Material : "Assets/Icons/IconMaterial.mat"
					}
				}, parent, name);
		}
	}

	private class InterfacePosition
	{
		public string AnchorMin;

		public string AnchorMax;

		public string OffsetMin;

		public string OffsetMax;
	}

	private class IColor
	{
		[JsonProperty(PropertyName = "HEX")] public string HEX;

		[JsonProperty(PropertyName = LangRu ? "Непрозрачность (0 - 100)" : "Opacity (0 - 100)")]
		public float Alpha;

		public string Get()
		{
			if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

			var str = HEX.Trim('#');
			if (str.Length != 6) throw new Exception(HEX);
			var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
			var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
			var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

			return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100}";
		}

		public IColor(string hex, float alpha)
		{
			HEX = hex;
			Alpha = alpha;
		}
	}

	private class IPanel : InterfacePosition
	{
		[JsonProperty(PropertyName = LangRu ? "Изображение" : "Image")]
		public string Image;

		[JsonProperty(PropertyName = LangRu ? "Цвет" : "Color")]
		public IColor Color;

		[JsonProperty(PropertyName = LangRu ? "Сохранять цвет изображения?" : "Save Image Color")]
		public bool isRaw;

		[JsonProperty(PropertyName = LangRu ? "Спрайт" : "Sprite")]
		public string Sprite;

		[JsonProperty(PropertyName = LangRu ? "Материал" : "Material")]
		public string Material;

		public void Get(ref CuiElementContainer container, string parent = "Hud", string name = null,
			bool cursor = false)
		{
			if (string.IsNullOrEmpty(name))
				name = CuiHelper.GetGuid();

			if (isRaw)
			{
				var element = new CuiElement
				{
					Name = name,
					Parent = parent,
					Components =
					{
						new CuiRawImageComponent
						{
							Png = !string.IsNullOrEmpty(Image)
								? _instance.GetImage( Image)
								: null,
							Color = Color.Get(),
							Material = Material,
							Sprite = !string.IsNullOrEmpty(Sprite) ? Sprite : "Assets/Icons/rust.png"
						},
						new CuiRectTransformComponent
						{
							AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin,
							OffsetMax = OffsetMax
						}
					}
				};

				if (cursor) element.Components.Add(new CuiNeedsCursorComponent());

				container.Add(element);
			}
			else
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax
					},
					Image =
					{
						Png = !string.IsNullOrEmpty(Image)
							? _instance.GetImage( Image)
							: null,
						Color = Color.Get(),
						Sprite =
							!string.IsNullOrEmpty(Sprite) ? Sprite : "Assets/Content/UI/UI.Background.Tile.psd",
						Material = !string.IsNullOrEmpty(Material) ? Material : "Assets/Icons/IconMaterial.mat"
					},
					CursorEnabled = cursor
				}, parent, name);
			}
		}
	}

	private class IText : InterfacePosition
	{
		[JsonProperty(PropertyName = LangRu ? "Размер шрифта" : "Font Size")]
		public int FontSize;

		[JsonProperty(PropertyName = LangRu ? "Шрифт" : "Font")]
		public string Font;

		[JsonProperty(PropertyName = LangRu ? "Выравнивание" : "Align")] [JsonConverter(typeof(StringEnumConverter))]
		public TextAnchor Align;

		[JsonProperty(PropertyName = LangRu ? "Цвет текста" : "Text Color")]
		public IColor Color;

		public void Get(ref CuiElementContainer container, string parent = "Hud", string name = null,
			string text = "", bool enableIcon = false)
		{
			if (string.IsNullOrEmpty(name))
				name = CuiHelper.GetGuid();

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = AnchorMin,
					AnchorMax = AnchorMax,
					OffsetMin = OffsetMin,
					OffsetMax = OffsetMax
				},
				Text =
				{
					Text = $"{text}", Align = Align, FontSize = FontSize, Color = Color.Get(),
					Font = Font
				}
			}, parent, name);

			if (enableIcon)
			{
				var length = text.Length * FontSize * 0.225f;

				container.Add(new CuiElement
				{
					Parent = parent,
					Components =
					{
						new CuiRawImageComponent
						{
							Png = _instance.GetImage(
								_config.SkillPanel.AddCostImage.Image)
						},
						new CuiRectTransformComponent
						{
							AnchorMin = AnchorMin,
							AnchorMax = AnchorMax,
							OffsetMin = $"{length} {_config.BalanceIcon.OffsetMinY}",
							OffsetMax = $"{length + _config.BalanceIcon.Length} {_config.BalanceIcon.OffsetMaxY}"
						}
					}
				});
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

	#region Updater

	private void UpdateConfigValues()
	{
		PrintWarning("Config update detected! Updating config values...");

		if (_config.Version == default)
		{
			if (_config.Version < new VersionNumber(1, 27, 0))
				_config.Skills.ForEach(skill => skill.Enabled = true);
		}
		else
		{
			if (_config.Version < new VersionNumber(1, 31, 0))
			{
				_config.Skills.ForEach(skill =>
				{
					foreach (var stage in skill.Stages)
						stage.Value.RequiredSkillStages = new List<RequiredSkillStage>();
				});

				foreach (var stage in _config.FastOven.Stages)
					stage.Value.RequiredSkillStages = new List<RequiredSkillStage>();

				StartConvertOldData();
			}

			if (_config.Version < new VersionNumber(1, 31, 12))
			{
				_config.Skills.ForEach(skill => skill.ShortName = string.Empty);

				_config.Skills.Add(new SkillEntry
				{
					Enabled = false,
					Permission = string.Empty,
					Type = SkillType.Gather,
					ShortName = "blue.berry",
					ID = 3490432,
					Image = "https://i.imgur.com/D8RsOS3.png",
					Title = "Berry Hunter",
					Description =
						"It's in charge of picking blueberries\nWhen you learn the skill, the rate increases by x1",
					Stages = new Dictionary<int, StageConf>
					{
						[1] = new(string.Empty,
							10,
							133,
							0,
							new List<RequiredSkillStage>()),
						[2] = new(string.Empty, 25,
							164,
							0,
							new List<RequiredSkillStage>()),
						[3] = new(string.Empty, 40,
							200,
							0,
							new List<RequiredSkillStage>())
					}
				});

				_config.Skills.Add(new SkillEntry
				{
					Enabled = false,
					Permission = string.Empty,
					Type = SkillType.CraftRates,
					ShortName = string.Empty,
					ID = 0,
					Image = "https://i.imgur.com/jnH4ELO.png",
					Title = "Craft Rates",
					Description =
						"Item crafting rates\nIf you learn this skill, you will have an increased chance of getting an increased amount of an item when crafting",
					Stages = new Dictionary<int, StageConf>
					{
						[1] = new(string.Empty, 10,
							140,
							10,
							new List<RequiredSkillStage>()),
						[2] = new(string.Empty, 25,
							160,
							20,
							new List<RequiredSkillStage>()),
						[3] = new(string.Empty, 40,
							200,
							30,
							new List<RequiredSkillStage>())
					}
				});
			}

			if (_config.Version < new VersionNumber(1, 31, 13))
			{
				_config.Skills.ForEach(skill =>
				{
					foreach (var stage in skill.Stages) stage.Value.Permission = string.Empty;
				});

				foreach (var stage in _config.FastOven.Stages) stage.Value.Permission = string.Empty;
			}

			if (_config.Version < new VersionNumber(1, 31, 22))
			{
				foreach (var skill in _config.Skills)
				{
					if (skill.Enabled && skill.Type == SkillType.Gather &&
					    skill.ShortName == "blue.berry|red.berry|white.berry|black.berry")
						skill.ShortName =
							"blue.berry|red.berry|white.berry|yellow.berry|green.berry|black.berry";

					switch (skill.Image)
					{
						case "https://i.imgur.com/fAti1Cj.png":
						{
							skill.Image = "https://i.ibb.co/0fB889w/fAti1Cj.png";
							break;
						}
						case "https://i.imgur.com/Log7sQR.png":
						{
							skill.Image = "https://i.ibb.co/Tt8KxCT/Log7sQR.png";
							break;
						}
						case "https://i.imgur.com/IifHk5l.png":
						{
							skill.Image = "https://i.ibb.co/PMBpNyf/IifHk5l.png";
							break;
						}
						case "https://i.imgur.com/RqdcAm0.png":
						{
							skill.Image = "https://i.ibb.co/6wdxmN8/RqdcAm0.png";
							break;
						}
						case "https://i.imgur.com/S4xulmj.png":
						{
							skill.Image = "https://i.ibb.co/Lv7DxLd/S4xulmj.png";
							break;
						}
						case "https://i.imgur.com/5fixMch.png":
						{
							skill.Image = "https://i.ibb.co/6tvGfSP/5fixMch.png";
							break;
						}
						case "https://i.imgur.com/4WlHK7u.png":
						{
							skill.Image = "https://i.ibb.co/5jr1cXC/4WlHK7u.png";
							break;
						}
						case "https://i.imgur.com/SEvG4EU.png":
						{
							skill.Image = "https://i.ibb.co/0CsgThq/SEvG4EU.png";
							break;
						}
						case "https://i.imgur.com/31k3bM0.png":
						{
							skill.Image = "https://i.ibb.co/py5bK98/31k3bM0.png";
							break;
						}
						case "https://i.imgur.com/W23JZVo.png":
						{
							skill.Image = "https://i.ibb.co/Gs5XvvK/W23JZVo.png";
							break;
						}
						case "https://i.imgur.com/W6uHKeU.png":
						{
							skill.Image = "https://i.ibb.co/0JJyt4H/W6uHKeU.png";
							break;
						}
						case "https://i.imgur.com/D8RsOS3.png":
						{
							skill.Image = "https://i.ibb.co/J26dBz9/D8RsOS3.png";
							break;
						}
						case "https://i.imgur.com/jnH4ELO.png":
						{
							skill.Image = "https://i.ibb.co/G5r1dQs/jnH4ELO.png";
							break;
						}
					}
				}

				_config.Skills.Add(new SkillEntry
				{
					Enabled = false,
					Permission = string.Empty,
					Type = SkillType.CombatMedic,
					ShortName = string.Empty,
					ID = 0,
					Image = "https://i.ibb.co/hyhpcfZ/combat-medic.png",
					Title = "Combat Medic",
					Description =
						"Fast recovery of health in battle.\nBy learning this skill, you increase the amount of healing health you receive when using bandages.",
					Stages = new Dictionary<int, StageConf>
					{
						[1] = new(string.Empty, 10,
							40,
							0,
							new List<RequiredSkillStage>()),
						[2] = new(string.Empty, 25,
							60,
							0,
							new List<RequiredSkillStage>()),
						[3] = new(string.Empty, 40,
							100,
							0,
							new List<RequiredSkillStage>())
					}
				});

				_config.Skills.Add(new SkillEntry
				{
					Enabled = false,
					Permission = string.Empty,
					Type = SkillType.SafeFall,
					ShortName = string.Empty,
					ID = 0,
					Image = "https://i.ibb.co/0hJs2sJ/safe-fall.png",
					Title = "Safe Fall",
					Description =
						"Reduces fall damage.\nBy learning this skill, you reduce the likelihood of serious injury when falling from height.",
					Stages = new Dictionary<int, StageConf>
					{
						[1] = new(string.Empty, 10,
							40,
							0,
							new List<RequiredSkillStage>()),
						[2] = new(string.Empty, 25,
							60,
							0,
							new List<RequiredSkillStage>()),
						[3] = new(string.Empty, 40,
							100,
							0,
							new List<RequiredSkillStage>())
					}
				});

				_config.Skills.Add(new SkillEntry
				{
					Enabled = false,
					Permission = string.Empty,
					Type = SkillType.Durability,
					ShortName = string.Empty,
					ID = 0,
					Image = "https://i.ibb.co/QryPdjN/durability.png",
					Title = "Durability",
					Description =
						"Allows you to reduce the breakage of items during use.\nBy learning this skill, your items will break less when used.",
					Stages = new Dictionary<int, StageConf>
					{
						[1] = new(string.Empty, 10,
							40,
							0,
							new List<RequiredSkillStage>()),
						[2] = new(string.Empty, 25,
							60,
							0,
							new List<RequiredSkillStage>()),
						[3] = new(string.Empty, 40,
							100,
							0,
							new List<RequiredSkillStage>())
					}
				});

				if (_config.SkillPanel.AddCostImage.Image == "https://i.imgur.com/5GdD0cU.png")
					_config.SkillPanel.AddCostImage.Image = "https://i.ibb.co/yfBv8F5/image.png";

				if (_config.MaxLevel.Image == "https://i.imgur.com/p3tKXJV.png")
					_config.MaxLevel.Image = "https://i.ibb.co/wBn7JzM/image.png";

				if (_config.NotMoney.Image == "https://i.imgur.com/p3tKXJV.png")
					_config.NotMoney.Image = "https://i.ibb.co/wBn7JzM/image.png";

				foreach (var additinalButton in _config.AdditionalButtons.Buttons)
					switch (additinalButton.Image)
					{
						case "https://i.imgur.com/C83Rprq.png":
						{
							additinalButton.Image = "https://i.ibb.co/X8btTQV/C83Rprq.png";
							break;
						}
					}
			}
			
			if (_config.Version < new VersionNumber(1, 32, 4))
			{
				_config.Skills.ForEach(skill =>
				{
					if (skill.Image == "https://i.ibb.co/0QryPdjN/durability.png") 
						skill.Image = "https://i.ibb.co/QryPdjN/durability.png";
				});

				if (Config.Get(LangRu
						    ? "Дополнительные настройки для навыка Быстроой Печи"
						    : "Additional settings for the Fast Oven skill",
					    LangRu ? "Уровни" : "Stages") is Dictionary<int, object> fastOvenStages)
					foreach (var fastOvenStage in fastOvenStages)
						if (fastOvenStage.Value is Dictionary<string, object> fastOvenFields && fastOvenFields[LangRu ? "Рейты" : "Rates"] is Dictionary<string, int> fastOvenRates)
							foreach (var (shortName, itemRate) in fastOvenRates) 
								_config.FastOven.Stages[fastOvenStage.Key].Rates[shortName] = itemRate;
				
				foreach (var skill in _config.Skills)
				{
					skill.TitleLocalization = new Localization
					{
						Enabled = false,
						Messages = new Dictionary<string, string>
						{
							["en"] = skill.Title,
							["fr"] = skill.Title,
						}
					};
					
					if (LangRu)
						skill.TitleLocalization.Messages.Add("ru", skill.Title);
					
					skill.DescriptionLocalization = new Localization
					{
						Enabled = false,
						Messages = new Dictionary<string, string>
						{
							["en"] = skill.Description,
							["fr"] = skill.Description,
						}
					};
					
					if (LangRu)
						skill.DescriptionLocalization.Messages.Add("ru", skill.Description);
				}
			}
		}

		_config.Version = Version;
		PrintWarning("Config update completed!");
		SaveConfig();
	}

	#endregion

	#endregion

	#region Hooks

	private void Init()
	{
		_instance = this;

		if (_config.StoreContainers)
			LoadContainers();

		LoadSkills();

		RegisterPermissions();

		UnsubscribeHooks();
	}

	private void OnServerInitialized()
	{
		Notifications?.Call("AddImage", _config.MaxLevel.Image, _config.MaxLevel.Url);

		Notifications?.Call("AddImage", _config.NotMoney.Image, _config.NotMoney.Url);

		LoadImages();

		CheckOnDuplicates();

		RegisterCommands();

		SubscribeHooks();

		LoadPlayers();

		timer.In(5, ValidateContainers);
		
#if USE_HARMONY
		InitHarmonyPatches();
#endif
	}

	private void OnServerSave()
	{
		timer.In(Random.Range(2f, 7f), PlayerData.Save);
	}

	private void Unload()
	{
#if USE_HARMONY
		DisposeHarmonyPatches();
#endif

		if (_wipePlayers != null)
			ServerMgr.Instance.StopCoroutine(_wipePlayers);

		foreach (var player in BasePlayer.activePlayerList)
		{
			CuiHelper.DestroyUi(player, Layer);

			PlayerData.SaveAndUnload(player.UserIDString);
		}

		if (_metabolismEngine != null)
			_metabolismEngine.Kill();

		if (_config.StoreContainers)
			SaveContainers();

		_instance = null;
		_config = null;
	}

	#region Wipe

	private void OnNewSave()
	{
		_lootedContainers?.Clear();

		if (_config.StoreContainers)
			SaveContainers();

		if (_config.Wipe)
			DoWipePlayers();
	}

	#endregion

	#region Players

	private void OnPlayerConnected(BasePlayer player)
	{
		if (player == null) return;

		if (_metabolismEngine != null)
			_metabolismEngine.AddOrUpdate(player);
	}

	private void OnPlayerDisconnected(BasePlayer player)
	{
		if (player == null || !player.userID.IsSteamId()) return;

		if (_metabolismEngine != null)
			_metabolismEngine.Remove(player);

		PlayerData.SaveAndUnload(player.UserIDString);
	}

	#endregion

	#region Skills

	#region Gather

	private void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
	{
		if (collectible == null || collectible.itemList == null) return;

		foreach (var item in collectible.itemList) OnCollect(player, item);
	}

	private void OnGrowableGathered(GrowableEntity plant, Item item, BasePlayer player)
	{
		OnGather(player, item);
	}

	private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
	{
		OnGather(player, item, dispenser);
	}

	private void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
	{
		OnGather(player, item, dispenser);
	}

	#endregion

	#region Damage

	private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
	{
		if (entity == null ||
		    info == null ||
		    entity is BaseCorpse ||
		    (info.Initiator != null && info.Initiator == entity)) return;
		
		var damage = info.damageTypes?.GetMajorityDamageType();
		if (damage is null or DamageType.Decay) return;
		
		PlayerData data;
		SkillData skillData;

		switch (damage)
		{
			case DamageType.Fall:
			{
				var fall = 0f;

				var target = entity as BasePlayer;
				if (target != null)
				{
					data = PlayerData.GetOrLoad(target.UserIDString);

					if ((skillData = data?.GetSkillData(SkillType.SafeFall)) != null)
						fall = skillData.GetStageValue(target);
				}

				if (fall != 0)
				{
					fall = 1f - fall / 100f;

					info.damageTypes?.ScaleAll(fall);
				}

				break;
			}

			default:
			{	
				var attack = 0f;
				var secure = 0f;

				var attacker = info.InitiatorPlayer;
				if (attacker != null && (!_config.IgnoreAttackSkillOnDuel || !CheckIsDuelPlayer(attacker)))
				{
					data = PlayerData.GetOrLoad(attacker.UserIDString);
					if ((skillData = data?.GetSkillData(SkillType.Attack)) != null &&
					    !_config.AttackBlackList.Contains(entity.ShortPrefabName)&&
					    !_config.WeaponAttackBlackList.Contains(info.WeaponPrefab?.ShortPrefabName))
					{
						attack = skillData.GetStageValue(attacker);
					}
				}

				var target = entity as BasePlayer;
				if (target != null && (!_config.IgnoreSecureSkillOnDuel || !CheckIsDuelPlayer(target)))
				{
					data = PlayerData.GetOrLoad(target.UserIDString);

					if ((skillData = data?.GetSkillData(SkillType.Secure)) != null)
						secure = skillData.GetStageValue(target);
				}

				var result = attack - secure;
				if (result != 0)
				{
					result = 1f + result / 100f;

					info.damageTypes?.ScaleAll(result);
				}

				break;
			}
		}
	}

	#endregion

	#region Loot

	private void OnLootEntity(BasePlayer player, LootContainer container)
	{
		if (player == null || container == null || container.net == null || container.inventory == null ||
		    _config.ContainersBlackList.Contains(container.ShortPrefabName)) return;

		var id = container.net.ID.Value;
		if (_lootedContainers.Contains(id))
			return;

		var data = PlayerData.GetOrLoad(player.UserIDString);

		SkillData skillData;

		#region ComponentsRates
		
#if TESTING
		var sb = new StringBuilder();
		container.inventory.itemList.ForEach(item =>
		{
			sb.AppendLine($"item: {item.info.shortname}, amount: {item.amount}");
		});

		LogPlayerDebug(player, nameof(OnLootEntity), $"before ComponentsRates:\n{sb.ToString()}");
#endif
		
		if ((skillData = data?.GetSkillData(SkillType.ComponentsRates)) != null)
			container.inventory.itemList.ForEach(item =>
			{
				if (item != null && (_config.ComponentsRates.AllowedCategories.Contains(item.info.category) ||
				                     _config.ComponentsRates.AllowedItems.Contains(item.info.shortname)) &&
				    !_config.ComponentsRates.BlockList.Contains(item.info.shortname))
					item.amount = (int) (skillData.GetStageValue(player) * item.amount);
			});

#if TESTING
		sb.Clear();
		container.inventory.itemList.ForEach(item =>
		{
			sb.AppendLine($"item: {item.info.shortname}, amount: {item.amount}");
		});

		LogPlayerDebug(player, nameof(OnLootEntity), $"after ComponentsRates:\n{sb.ToString()}");
#endif
		#endregion

		#region Scrap

		if ((skillData = data?.GetSkillData(SkillType.Scrap)) != null)
			container.inventory.itemList.ForEach(item =>
			{
				if (item != null &&
				    item.info.shortname == "scrap")
					item.amount = (int) (skillData.GetStageValue(player) * item.amount);
			});

		#endregion

		_lootedContainers.Add(id);
	}

	private void OnContainerDropItems(ItemContainer container)
	{
		if (container == null) return;

		var entity = container.entityOwner as LootContainer;
		if (entity == null ||
		    entity.IsDestroyed ||
		    (!entity.ShortPrefabName.Contains("barrel") &&
		     !entity.ShortPrefabName.Contains("roadsign")) ||
		    _config.ContainersBlackList.Contains(entity.ShortPrefabName)) return;
		
		var player = entity.lastAttacker as BasePlayer;
		if (player == null) return;
		
		var data = PlayerData.GetOrLoad(player.UserIDString);

		SkillData skillData;

		#region ComponentsRates

#if TESTING
		var sb = new StringBuilder();
		entity.inventory.itemList.ForEach(item =>
		{
			sb.AppendLine($"item: {item.info.shortname}, amount: {item.amount}");
		});

		SayDebug(player.userID, nameof(OnContainerDropItems), $"before ComponentsRates:\n{sb.ToString()}");
#endif
		
		if ((skillData = data?.GetSkillData(SkillType.ComponentsRates)) != null)
			container.itemList.ForEach(item =>
			{
				if (item != null && (_config.ComponentsRates.AllowedCategories.Contains(item.info.category) ||
				                     _config.ComponentsRates.AllowedItems.Contains(item.info.shortname)) &&
				    !_config.ComponentsRates.BlockList.Contains(item.info.shortname))
				{
					item.amount = (int) (skillData.GetStageValue(player) * item.amount);
				}
			});

#if TESTING
		sb.Clear();
		entity.inventory.itemList.ForEach(item =>
		{
			sb.AppendLine($"item: {item.info.shortname}, amount: {item.amount}");
		});

		SayDebug(player.userID, nameof(OnContainerDropItems), $"after ComponentsRates:\n{sb.ToString()}");
#endif

		#endregion

		#region Scrap

		if ((skillData = data?.GetSkillData(SkillType.Scrap)) != null)
			container.itemList.ForEach(item =>
			{
				if (item != null && item.info.shortname == "scrap")
					item.amount = (int) (skillData.GetStageValue(player) * item.amount);
			});

		#endregion
	}

	private void OnEntityKill(LootContainer container)
	{
		if (container  == null || container.net  == null || !container.net.ID.IsValid) return;
		
		_lootedContainers.Remove(container.net.ID.Value);
	}
    
	#endregion

	#region Craft

	private void OnItemCraft(ItemCraftTask task, BasePlayer player, Item item)
	{
		if (task == null || player == null || player.inventory.crafting.queue.Count > 0) return;

		CraftingHandle(task, player);
	}

	private void OnItemCraftFinished(ItemCraftTask task, Item item, ItemCrafter crafter)
	{
		if (task == null || crafter == null) return;

		var player = crafter.owner;
		if (player == null) return;

		TryGetCraftBonus(player, item);

		if (task.amount == 0 && player.inventory.crafting.queue.Count > 1)
			task = player.inventory.crafting.queue.ElementAt(1);

		CraftingHandle(task, player);
	}

	private void CraftingHandle(ItemCraftTask task, BasePlayer player)
	{
		if (task == null || player == null) return;

		var skillData = PlayerData.GetOrLoad(player.UserIDString)?.GetSkillData(SkillType.CraftSpeed);
		if (skillData == null) return;

		var rate = 1f - skillData.GetStageValue(player) / 100f;
		if (rate < 0f) return;

		NextTick(() =>
		{
			var currentCraftLevel = player.currentCraftLevel;
			
			var duration = ItemCrafter.GetScaledDuration(task.blueprint, currentCraftLevel, player.IsInTutorial);
			var scaledDuration = duration * rate;
			
			task.endTime = Time.realtimeSinceStartup + scaledDuration;
			if (player == null)
				return;
			
			player.Command("note.craft_start", task.taskUID, scaledDuration,
				task.amount);

			/*
			if (!task.owner.IsAdmin || !Craft.instant)
				return;
			task.endTime = Time.realtimeSinceStartup + 1f;*/
		});
	}

	private void GiveItem(BasePlayer player, int amount, ItemCraftTask task)
	{
		var def = task.blueprint.targetItem;

		if (amount <= 0)
		{
			PrintWarning(
				$"Player \"{player.displayName}\" is about to create an item {def.shortname} with amount <= 0!\nReport to the developer!");
			return;
		}

		if (!player.IsConnected)
			return;

		var skin = ItemDefinition.FindSkin(def.itemid, task.skinID);

		var item = ItemManager.Create(def, amount, skin);

		// ReSharper disable CompareOfFloatsByEqualityOperator
		if (item.hasCondition && task.conditionScale != 1f)
		{
			item.maxCondition *= task.conditionScale;
			item.condition = item.maxCondition;
		}
		// ReSharper restore CompareOfFloatsByEqualityOperator

		item.OnVirginSpawn();

		Analytics.Server.Crafting(task.blueprint.targetItem.shortname, task.skinID);

		player.Command("note.craft_done", task.taskUID, 1, amount);

		Interface.CallHook("OnItemCraftFinished", task, item);

		if (task.instanceData != null)
			item.instanceData = task.instanceData;

		if (!string.IsNullOrEmpty(task.blueprint.UnlockAchievment) && ConVar.Server.official)
			player.ClientRPCPlayer(null, player, "RecieveAchievement", task.blueprint.UnlockAchievment);

		player.GiveItem(item, BaseEntity.GiveItemReason.Crafted);
	}
	
	private void TryGetCraftBonus(BasePlayer player, Item item)
	{
		if (player == null ||
		    item == null ||
		    !_config.CraftRates.AllowedList.Contains(item.info.shortname))
			return;

		var data = PlayerData.GetOrLoad(player.UserIDString);

		var skillData = data?.GetSkillData(SkillType.CraftRates);

		var rate = skillData?.GetStageValue(player) / 100f;
		if (rate < 0f) return;

		var chance = skillData?.GetStageSecondValue(player);
		if (Random.Range(0, 100) <= chance)
		{
			var amount = Mathf.CeilToInt((item.amount * rate - item.amount).Value);
			if (amount > 0)
			{
				var newItem = ItemManager.Create(item.info, amount, item.skin);
				if (newItem != null)
					player.GiveItem(newItem, BaseEntity.GiveItemReason.PickedUp);
			}
		}
	}

	#endregion

	#region Fast Oven

	private void OnOvenCook(BaseOven oven, Item burnable)
	{
		if (oven == null || oven.net == null)
			return;

		if (!_config.FastOven.WorkWithCampfires && oven.name.Contains("campfire"))
			return;

		var player = oven.ShortPrefabName.Contains("electricfurnace") ? RelationshipManager.FindByID(oven.OwnerID) : GetPlayerByOven(oven.net.ID.Value);
		if (player == null) return;
		
		var amount = 0.5f * oven.GetSmeltingSpeed();

		var list = Pool.Get<List<Item>>();

		oven.inventory.itemList.ForEach(item =>
		{
			if (item.HasFlag(global::Item.Flag.Cooking))
				list.Add(item);
		});
		
		list.ForEach(item =>
		{
			var rate = GetFastOvenItemRate(player, item.info.shortname);
			if (rate <= 0) return;
			
			if (_config.FastOven.StopBurningFood)
				if (item.info.TryGetComponent<ItemModCookable>(out var cookable))
				{
					var resultItem = cookable.becomeOnCooked;
					if (resultItem != null && resultItem.shortname.EndsWith(".burned"))
						return;
				}

			var delta = (amount * rate - amount) / list.Count;
			if (delta > 0.0)
			{
				item.OnCycle(delta);
			}
		});

		Pool.FreeUnmanaged(ref list);
	}

	private void OnOvenToggle(BaseOven oven, BasePlayer player)
	{
		if (oven == null || oven.net == null || player == null ||
		    _config.OvensBlackList.Exists(x => oven.name.Contains(x)))
			return;

		NextTick(() =>
		{
			if (oven.IsOn())
				_playerByOven[oven.net.ID.Value] = player.userID;
			else
				_playerByOven.Remove(oven.net.ID.Value);
		});
	}

	#endregion

	#region Recycler Speed

	private void OnRecyclerToggle(Recycler recycler, BasePlayer player)
	{
		if (recycler == null || player == null) return;

		var data = PlayerData.GetOrLoad(player.UserIDString);
		if (data == null) return;

		NextTick(() =>
		{
			if (recycler.IsOn())
			{
				var recyclerSpeed = data.GetSkillData(SkillType.RecyclerSpeed)
					?.GetStageValue(player) ?? 0f;

				if (recyclerSpeed >= 0.5f)
				{
					recycler.CancelInvoke(nameof(recycler.RecycleThink));
					timer.Once(0.1f,
						() => recycler.InvokeRepeating(recycler.RecycleThink, recyclerSpeed - 0.1f, recyclerSpeed));
				}
			}
		});
	}

	#endregion

	#region MixingTable Speed

	private void OnMixingTableToggle(MixingTable table, BasePlayer player)
	{
		if (table.IsOn())
			return;

		NextTick(() =>
		{
			var skillData = PlayerData.GetOrLoad(player.UserIDString)?.GetSkillData(SkillType.MixingTableSpeed);
			if (skillData == null) return;

			var mixingRate = skillData.GetStageValue(player) / 100f;
			if (mixingRate < 0f) return;

			table.RemainingMixTime *= mixingRate;
			table.TotalMixTime *= mixingRate;
			table.SendNetworkUpdateImmediate();

			if (table.RemainingMixTime < 1f)
			{
				table.CancelInvoke(table.TickMix);
				table.Invoke(table.TickMix, table.RemainingMixTime);
			}
		});
	}

	#endregion

	#region Kits

	private object OnKitCooldown(BasePlayer player, double cooldown)
	{
		var skillData = PlayerData.GetOrLoad(player.UserIDString)?.GetSkillData(SkillType.Kits);
		if (skillData == null) return null;

		var rate = (double) (1f - skillData.GetStageValue(player) / 100f);
		if (rate <= 0f) return null;

		return cooldown * rate;
	}

	#endregion

	#region Combat Medic

	private void OnHealingItemUse(MedicalTool tool, BasePlayer player)
	{
		if (player == null || player.IsNpc || !player.userID.IsSteamId()) return;
		
		var item = tool.GetItem();
		if (item == null || !_config.CombatMedic.AllowedItems.TryGetValue(item.info.shortname, out var allow) || !allow) return;
        
		var skillData = PlayerData.GetOrLoad(player.UserIDString)?.GetSkillData(SkillType.CombatMedic);
		if (skillData == null) return;

		var ownerItemDefinition = tool.GetOwnerItemDefinition();
		if (ownerItemDefinition == null) return;

		var modConsumable = ownerItemDefinition.GetComponent<ItemModConsumable>();
		if (modConsumable != null)
		{		
			NextTick(() =>
			{		
				var rate = skillData.GetStageValue(player) / 100f;

				foreach (var effect in modConsumable.effects)
				{
					var amount = effect.amount * rate;

					if (effect.type == MetabolismAttribute.Type.Health)
						player.health += amount;
					else
						player.metabolism.ApplyChange(effect.type, amount, effect.time);
				}
			});}
	}

	private void OnItemUse(Item item, int amountToUse)
	{
		if (item == null || !_config.CombatMedic.AllowedItems.TryGetValue(item.info.shortname, out var allow) || !allow) return;

		var player = item.GetOwnerPlayer();
		if (player == null || player.IsNpc || !player.userID.IsSteamId()) return;
		
		var skillData = PlayerData.GetOrLoad(player.UserIDString)?.GetSkillData(SkillType.CombatMedic);
		if (skillData == null) return;

		var modConsumable = item.info.GetComponent<ItemModConsumable>();
		if (modConsumable != null)
		{		
			NextTick(() =>
			{		
				var rate = skillData.GetStageValue(player) / 100f;

				foreach (var effect in modConsumable.effects)
				{
					var amount = effect.amount * rate;

					if (effect.type == MetabolismAttribute.Type.Health)
						player.health += amount;
					else
						player.metabolism.ApplyChange(effect.type, amount, effect.time);
				}
			});
		}
	}

	#endregion

	#region Durability

	private void OnLoseCondition(Item item, ref float amount)
	{
		if (item == null) return;

		var player = item.GetEntityOwner() as BasePlayer;
		if (player == null || player.IsNpc || !player.userID.IsSteamId() ||
		    _config.DurabilityBlacklist.Contains(item.info.shortname)) return;

#if TESTING
		LogPlayerDebug(player, nameof(OnLoseCondition), "called");
#endif
		var skillData = PlayerData.GetOrLoad(player.UserIDString)?.GetSkillData(SkillType.Durability);
		if (skillData == null) return;

#if TESTING
		LogPlayerDebug(player, nameof(OnLoseCondition), $"after get skill data: {skillData.GetStageValue(player)}");
#endif
		
		var rate = Mathf.Max(1f - skillData.GetStageValue(player) / 100f, 0);

#if TESTING
		LogPlayerDebug(player, nameof(OnLoseCondition), $"rate = {rate}, result = {amount * rate}");
#endif

		amount *= rate;
	}

	#endregion

	#endregion

	#region Images

#if !CARBON
	private void OnPluginLoaded(Plugin plugin)
	{
		switch (plugin.Name)
		{
			case "ImageLibrary":
				timer.In(1, LoadImages);
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
		}
	}
#endif

	#endregion

	#endregion

	#region Commands

	private void CmdOpenUi(IPlayer cov, string command, string[] args)
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
			SendNotify(player, NoPermissions, 1);
			return;
		}

		MainUi(player, first: true);
	}

	private void AdminCommands(IPlayer cov, string command, string[] args)
	{
		if (!cov.IsAdmin) return;

		if (args.Length == 0)
		{
			cov.Reply($"Use: /{command} name/steamid");
			return;
		}

		var target = BasePlayer.Find(args[0]);
		if (target == null)
		{
			cov.Reply($"Player {args[0]} not found!");
			return;
		}

		var data = PlayerData.GetOrCreate(target.UserIDString);
		if (data == null) return;

		switch (command)
		{
			case "giveallskills":
			{
				data.Skills.Clear();
				_config.Skills.ForEach(skill =>
				{
					var stages = GetSkillStages(skill, target.UserIDString);

					var stage = stages.Keys.Max();

					data.AddSkill(target, skill, stage);
				});

				cov.Reply($"Player {args[0]} give all skills!");
				break;
			}

			case "giveskill":
			{
				if (args.Length < 3)
				{
					cov.Reply($"Use: /{command} name/steamid [SkillType] [Stage] [ID - for None]");
					return;
				}

				if (!Enum.TryParse(args[1], out SkillType type) || !int.TryParse(args[2], out var stage))
				{
					cov.Reply("Error getting values");
					return;
				}

				var id = 0;
				if (args.Length > 3) int.TryParse(args[3], out id);

				var skill = _config.Skills.Find(x => x.Type == type && x.ID == id);
				if (skill == null)
				{
					cov.Reply($"Skill (type: {type} | id: {id}) not found!");
					return;
				}

				var dataSkill = data.GetSkillData(type, id);
				if (dataSkill != null)
					data.UpgradeSkill(dataSkill, target, skill, stage);
				else
					data.AddSkill(target, skill, stage);

				cov.Reply($"Player {args[0]} give skill: {type} (stage: {stage})!");
				break;
			}

			case "removeskill":
			{
				if (args.Length < 2)
				{
					cov.Reply($"Use: /{command} name/steamid [SkillType] [ID - for None]");
					return;
				}

				if (!Enum.TryParse(args[1], out SkillType type))
				{
					cov.Reply("Error getting values");
					return;
				}

				var id = 0;
				if (args.Length > 2) int.TryParse(args[2], out id);

				var skill = _config.Skills.Find(x => x.Type == type && x.ID == id);
				if (skill == null)
				{
					cov.Reply($"Skill (type: {type} | id: {id}) not found!");
					return;
				}

				var dataSkill = data.GetSkillData(type, id);
				if (dataSkill != null)
				{
					data.RemoveSkill(dataSkill, target, skill);

					cov.Reply(
						$"You removed the {skill.Type} (ID: {skill.ID}) skill from '{target.displayName}' ({target.UserIDString}).");
				}

				break;
			}
		}
	}

	[ConsoleCommand("UI_Skills")]
	private void CmdConsoleSkills(ConsoleSystem.Arg arg)
	{
		var player = arg?.Player();
		if (player == null || !arg.HasArgs()) return;

		switch (arg.Args[0])
		{
			case "close":
			{
				CuiHelper.DestroyUi(player, Layer);
				break;
			}
			case "page":
			{
				if (!arg.HasArgs(3) || !int.TryParse(arg.Args[1], out var page)) return;

				MainUi(player, arg.Args[2], page);
				break;
			}

			case "upgrade":
			{
				if (!arg.HasArgs(5) ||
				    !Enum.TryParse(arg.Args[1], out SkillType type) ||
				    !int.TryParse(arg.Args[2], out var page) ||
				    !int.TryParse(arg.Args[4], out var id)) return;

				var parent = arg.Args[3];

				var skill = _config.Skills.Find(x => x.Type == type && x.ID == id);
				if (skill == null) return;

				var cost = GetNextPrice(player, skill);
				if (cost <= 0)
				{
					if (_config.SkillPanel.ShowAllStages && cost == -2)
					{
						SendNotify(player, NoPermissions, 1);
						return;
					}

					return;
				}

				var data = PlayerData.GetOrCreate(player.UserIDString);
				if (data == null) return;

				var nextStage = 1;
				var dataSkill = data.GetSkillData(type, id);
				if (dataSkill != null)
				{
					nextStage = dataSkill.Stage + 1;

					var stages = GetSkillStages(skill, player.UserIDString);

					if (!stages.ContainsKey(nextStage))
					{
						SendNotify(player, _config.MaxLevel.Delay, MaxLevelTitle, MaxLevelDescription,
							_config.MaxLevel.Image);
						return;
					}
				}

				if (!_config.Economy.RemoveBalance(player, cost))
				{
					SendNotify(player, _config.NotMoney.Delay, NotMoneyTitle, NotMoneyDescription,
						_config.NotMoney.Image);
					return;
				}

				if (dataSkill != null)
					data.UpgradeSkill(dataSkill, player, skill, nextStage);
				else
					data.AddSkill(player, skill, nextStage);

				MainUi(player, parent, page);
				break;
			}

			case "changedescview":
			{
				if (!arg.HasArgs(5) ||
				    !Enum.TryParse(arg.Args[1], out SkillType type) ||
				    !int.TryParse(arg.Args[2], out var page) ||
				    !int.TryParse(arg.Args[4], out var id)) return;

				var parent = arg.Args[3];

				var skill = _config.Skills.Find(x => x.Type == type && x.ID == id);
				if (skill == null) return;

				var data = PlayerData.GetOrCreate(player.UserIDString);
				if (data == null) return;

				data.ChangeDescriptionView(skill);

				MainUi(player, parent, page);
				break;
			}

			case "sendcmd":
			{
				if (!arg.HasArgs(2)) return;

				var command = string.Join(" ", arg.Args.Skip(1));
				if (string.IsNullOrEmpty(command)) return;

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

	private void CmdWipeSkills(IPlayer cov, string command, string[] args)
	{
		if (!cov.IsServer && !cov.HasPermission(PERM_Wipe)) return;

		DoWipePlayers();

		cov.Reply("Players data have been wiped!");
	}

	[ConsoleCommand("skills.permissions.wipe")]
	private void CmdWipePermissions(ConsoleSystem.Arg arg)
	{
		if (!arg.IsServerside) return;

		var players = PlayerData.GetFiles();
		if (players is not {Length: > 0})
		{
			SendReply(arg, "No players data found!");
			return;
		}

		SendReply(arg, "Clearing player permissions...");

		ServerMgr.Instance.StartCoroutine(StartOnAllPlayers(players, userID =>
		{
			var data = PlayerData.GetOrLoad(userID);
			if (data == null || data.Permissions.Count <= 0) return;

			foreach (var perm in data.Permissions) permission.RevokeUserPermission(userID, perm);
		}, () => { Puts("Permissions cleared!"); }));
	}

	#endregion

	#region Interface

	private void MainUi(BasePlayer player, string parent = "Overlay", int page = 0, bool first = false)
	{
		if (string.IsNullOrEmpty(parent))
			parent = "Overlay";

		var container = new CuiElementContainer();

		var data = PlayerData.GetOrCreate(player.UserIDString);

		var hasBypass = _instance?.HasBypass(player) == true;

		#region Background

		if (first)
		{
			CuiHelper.DestroyUi(player, Layer);

			_config.Background.Get(ref container, parent, Layer, true);

			_config.Title.Get(ref container, Layer, null, Msg(MainTitle, player.UserIDString));
		}

		#endregion

		#region Main

		container.Add(new CuiPanel
		{
			RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
			Image = {Color = "0 0 0 0"}
		}, Layer, Layer + ".Main");


		var playerSkills = GetPlayerSkills(player);

		var skills = playerSkills
			.Skip(page * _config.SkillPanel.Count)
			.Take(_config.SkillPanel.Count)
			.ToList();

		var lines = (int) Math.Ceiling(skills.Count / 2f);
		var xMargin = _config.SkillPanel.Margin / 2f;

		var ySwitch = (lines * _config.SkillPanel.Height + (lines - 1) * _config.SkillPanel.Margin) / 2f;

		var i = 1;
		skills.ForEach(skill =>
		{
			var xSwitch = i % 2 != 0 && i == skills.Count && skills.Count != _config.SkillPanel.Count
				? -_config.SkillPanel.Width / 2f
				: i % 2 != 0
					? -_config.SkillPanel.Width - xMargin
					: xMargin;

			_config.SkillPanel.Get(ref container, data, parent, page, Layer + ".Main", i, player, skill, xSwitch,
				ySwitch - _config.SkillPanel.Height, xSwitch + _config.SkillPanel.Width, ySwitch, hasBypass);

			if (i % 2 == 0)
				ySwitch = ySwitch - _config.SkillPanel.Height - _config.SkillPanel.Margin;
			i++;
		});

		#endregion

		#region Pages

		if (playerSkills.Count > _config.SkillPanel.Count)
		{
			if (page != 0)
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin =
							_config.Back.AnchorMin,
						AnchorMax =
							_config.Back.AnchorMax,
						OffsetMin =
							_config.Back.OffsetMin,
						OffsetMax =
							_config.Back.OffsetMax
					},
					Text =
					{
						Text = Msg(player, BackBtn),
						Align =
							_config.Back.Align,
						FontSize =
							_config.Back.FontSize,
						Font =
							_config.Back.Font,
						Color =
							_config.Back.Color.Get()
					},
					Button =
					{
						Color = "0 0 0 0",
						Command = $"UI_Skills page {page - 1} {parent}"
					}
				}, Layer + ".Main");

			if (playerSkills.Count > (page + 1) * _config.SkillPanel.Count)
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin =
							_config.Next.AnchorMin,
						AnchorMax =
							_config.Next.AnchorMax,
						OffsetMin =
							_config.Next.OffsetMin,
						OffsetMax =
							_config.Next.OffsetMax
					},
					Text =
					{
						Text = Msg(player, NextBtn),
						Align =
							_config.Next.Align,
						FontSize =
							_config.Next.FontSize,
						Font =
							_config.Next.Font,
						Color =
							_config.Next.Color.Get()
					},
					Button =
					{
						Color = "0 0 0 0",
						Command = $"UI_Skills page {page + 1} {parent}"
					}
				}, Layer + ".Main");
		}

		#endregion

		#region Balance

		_config.BalanceText.Get(ref container, Layer + ".Main", null,
			Msg(Balance, player.UserIDString, _config.Economy.ShowBalance(player)), _config.EnableBalanceIcon);

		#endregion

		#region Close

		_config.CloseLabel.Get(ref container, Layer + ".Main", Layer + ".Close", Msg(player, Close));

		container.Add(new CuiButton
		{
			RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
			Text = {Text = ""},
			Button = {Color = "0 0 0 0", Command = "UI_Skills close"}
		}, Layer + ".Close");

		#endregion

		#region Additional Buttons

		if (_config.AdditionalButtons.Buttons.Count > 0)
		{
			var xSwitch = -_config.AdditionalButtons.UI.SideIndent;

			_config.AdditionalButtons.Buttons.ForEach(btn =>
			{
				if (!btn.Enabled) return;

				container.Add(new CuiElement
				{
					Name = Layer + $".Additional.Btn.{xSwitch}",
					Parent = Layer + ".Main",
					Components =
					{
						new CuiRawImageComponent
						{
							Png = GetImage( btn.Image) ?? string.Empty
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "1 1", AnchorMax = "1 1",
							OffsetMin =
								$"{xSwitch - _config.AdditionalButtons.UI.Size} -{_config.AdditionalButtons.UI.Size + _config.AdditionalButtons.UI.UpIndent}",
							OffsetMax =
								$"{xSwitch} -{_config.AdditionalButtons.UI.UpIndent}"
						}
					}
				});

				container.Add(new CuiButton
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text = {Text = ""},
						Button =
						{
							Command = $"UI_Skills sendcmd {btn.Command}",
							Color = "0 0 0 0",
							Close = btn.CloseMenu ? Layer : string.Empty
						}
					}, Layer + $".Additional.Btn.{xSwitch}");

				xSwitch = xSwitch - _config.AdditionalButtons.UI.Size - _config.AdditionalButtons.UI.Margin;
			});
		}

		#endregion

		CuiHelper.DestroyUi(player, Layer + ".Main");
		CuiHelper.AddUi(player, container);
	}

	#endregion

	#region Utils

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
	}

	private void DoWipePlayers()
	{
		try
		{
			var players = PlayerData.GetFiles();
			if (players is {Length: > 0})
			{
				_wipePlayers =
					ServerMgr.Instance.StartCoroutine(StartOnAllPlayers(players,
						PlayerData.DoWipe));
			}
		}
		catch (Exception e)
		{
			PrintError($"[On Server Wipe] in wipe players, error: {e.Message}");
		}
	}

	#endregion

	#region Skills.Cache

	private Dictionary<SkillType, SkillEntry> _skillByType = new();

	private Dictionary<int, SkillEntry> _skillByID = new();

	private SkillEntry FindSkill(SkillType type, int id = 0)
	{
		SkillEntry skill;

		switch (type)
		{
			case SkillType.Gather:
			case SkillType.None:
			{
				return _skillByID.TryGetValue(id, out skill) ? skill : null;
			}
			default:
			{
				return _skillByType.TryGetValue(type, out skill) ? skill : null;
			}
		}
	}

	private void LoadSkills()
	{
		_config.Skills.ForEach(skill =>
		{
			switch (skill.Type)
			{
				case SkillType.Gather:
				case SkillType.None:
				{
					if (!_skillByID.TryAdd(skill.ID, skill))
						for (var i = 0; i < 3; i++)
							PrintError($"DUBLICATE SKILL ID: {skill.ID} (TYPE: {skill.Type})");

					break;
				}

				default:
				{
					skill.ID = 0;

					if (!_skillByType.TryAdd(skill.Type, skill))
						for (var i = 0; i < 3; i++)
							PrintError($"DUBLICATE SKILL TYPE: {skill.Type}");

					break;
				}
			}
		});
	}

	#endregion

	#region Players

	private bool HasBypass(BasePlayer player)
	{
		return permission.UserHasPermission(player.UserIDString, PERM_Bypass);
	}

	private List<SkillEntry> GetPlayerSkills(BasePlayer player)
	{
		var playerData = PlayerData.GetOrCreate(player.UserIDString);
		return _config.Skills
			.FindAll(skill =>
			{
				if (!skill.Enabled)
					return false;

				var hasPermissions = string.IsNullOrEmpty(skill.Permission) ||
				                     permission.UserHasPermission(player.UserIDString, skill.Permission);
				if (!hasPermissions)
					return false;

				var dataSkill = playerData?.GetSkillData(skill.Type, skill.ID);
				if (dataSkill == null)
					if (GetSkillStages(skill, player.UserIDString)[1].GetRequiredSkillStages()
					    .Any(x => x.Has(player) == false))
						return false;

				return true;
			});
	}

	#endregion

	#region Plugin Loading

	private void UnsubscribeHooks()
	{
		Unsubscribe(nameof(OnCollectiblePickup));
		Unsubscribe(nameof(OnGrowableGathered));
		Unsubscribe(nameof(OnDispenserBonus));
		Unsubscribe(nameof(OnDispenserGather));
		Unsubscribe(nameof(OnEntityTakeDamage));
		Unsubscribe(nameof(OnContainerDropItems));
		Unsubscribe(nameof(OnLootEntity));
		Unsubscribe(nameof(OnItemCraft));
		Unsubscribe(nameof(OnItemCraftFinished));
		Unsubscribe(nameof(OnOvenCook));
		Unsubscribe(nameof(OnOvenToggle));
		Unsubscribe(nameof(OnKitCooldown));
		Unsubscribe(nameof(OnRecyclerToggle));
		Unsubscribe(nameof(OnMixingTableToggle));
		Unsubscribe(nameof(OnHealingItemUse));
		Unsubscribe(nameof(OnItemUse));
		Unsubscribe(nameof(OnLoseCondition));
	}

	private void SubscribeHooks()
	{
		var types = Pool.Get<List<SkillType>>();
		var hooks = new HashSet<string>();

		_config.Skills.ForEach(skill =>
		{
			if (!skill.Enabled || types.Contains(skill.Type)) return;

			switch (skill.Type)
			{
				case SkillType.Wood:
				case SkillType.Stones:
				case SkillType.Metal:
				case SkillType.Sulfur:
				case SkillType.Cloth:
				case SkillType.Butcher:
				case SkillType.Gather:
				{
					hooks.Add(nameof(OnCollectiblePickup));
					hooks.Add(nameof(OnGrowableGathered));
					hooks.Add(nameof(OnDispenserBonus));
					hooks.Add(nameof(OnDispenserGather));
					break;
				}
				case SkillType.Attack:
				case SkillType.Secure:
				case SkillType.SafeFall:
				{
					hooks.Add(nameof(OnEntityTakeDamage));
					break;
				}
				case SkillType.Regeneration:
				case SkillType.Metabolism:
				{
					InitMetabolism();
					break;
				}
				case SkillType.Scrap:
				case SkillType.ComponentsRates:
				{
					hooks.Add(nameof(OnContainerDropItems));
					hooks.Add(nameof(OnLootEntity));
					hooks.Add(nameof(OnEntityKill));
					break;
				}
				case SkillType.CraftSpeed:
				{
					hooks.Add(nameof(OnItemCraft));
					hooks.Add(nameof(OnItemCraftFinished));
					break;
				}
				case SkillType.FastOven:
				{
					hooks.Add(nameof(OnOvenCook));
					hooks.Add(nameof(OnOvenToggle));
					break;
				}
				case SkillType.Kits:
				{
					hooks.Add(nameof(OnKitCooldown));
					break;
				}
				case SkillType.RecyclerSpeed:
				{
					hooks.Add(nameof(OnRecyclerToggle));
					break;
				}
				case SkillType.MixingTableSpeed:
				{
					hooks.Add(nameof(OnMixingTableToggle));
					break;
				}
				case SkillType.CombatMedic:
				{
					hooks.Add(nameof(OnHealingItemUse));
					hooks.Add(nameof(OnItemUse));
					break;
				}
				case SkillType.Durability:
				{
					hooks.Add(nameof(OnLoseCondition));
					break;
				}
				default:
					return;
			}

			types.Add(skill.Type);
		});

		Pool.FreeUnmanaged(ref types);

		foreach (var hook in hooks)
			Subscribe(hook);

		hooks.Clear();
	}

	private void RegisterCommands()
	{
		AddCovalenceCommand(_config.Command, nameof(CmdOpenUi));

		AddCovalenceCommand(new[] {"giveallskills", "giveskill", "removeskill"}, nameof(AdminCommands));

		AddCovalenceCommand("skills.wipe", nameof(CmdWipeSkills));
	}

	private void RegisterPermissions()
	{
		permission.RegisterPermission(PERM_Bypass, this);

		permission.RegisterPermission(PERM_Wipe, this);

		permission.RegisterPermission(_config.Permission, this);

		_config.Skills.ForEach(skill =>
		{
			TryRegisterPermission(skill.Permission);

			foreach (var stage in skill.Stages.Values)
				TryRegisterPermission(stage.Permission);
		});

		foreach (var stage in _config.FastOven.Stages.Values)
			TryRegisterPermission(stage.Permission);
	}

	private void TryRegisterPermission(string perm)
	{
		if (!string.IsNullOrEmpty(perm) && !permission.PermissionExists(perm))
			permission.RegisterPermission(perm, this);
	}

	private void CheckOnDuplicates()
	{
		var duplicates = _config.Skills
			.FindAll(x => x.Type == SkillType.None || x.Type == SkillType.Gather)
			.GroupBy(x => x.ID)
			.Where(group => group.Count() > 1)
			.Select(group => group.Key)
			.ToArray();

		if (duplicates.Length > 0)
			PrintError(
				$"Matching item IDs found (None Type): {string.Join(", ", duplicates.Select(x => x.ToString()))}");
	}

	private void LoadPlayers()
	{
		for (var i = 0; i < BasePlayer.activePlayerList.Count; i++)
			OnPlayerConnected(BasePlayer.activePlayerList[i]);
	}

	#endregion

	#region Gather

	private void OnGather(BasePlayer player, Item item, ResourceDispenser dispenser = null)
	{
		if (player == null || item == null) return;

		var totalAmount = 0f;

		PlayerData.GetOrLoad(player.UserIDString)?.Skills?.ForEach(dataSkill =>
		{
			var skill = dataSkill?.Skill;
			if (skill == null) return;

			var amount = dataSkill.GetStageValue(player) / 100f;
			if (amount <= 0f) return;

			switch (skill.Type)
			{
				case SkillType.Wood:
				{
					if (item.info.shortname == "wood") totalAmount += amount;
					break;
				}
				case SkillType.Stones:
				{
					if (item.info.shortname == "stones") totalAmount += amount;
					break;
				}
				case SkillType.Sulfur:
				{
					if (item.info.shortname == "sulfur.ore") totalAmount += amount;
					break;
				}
				case SkillType.Metal:
				{
					if (item.info.shortname == "metal.ore" || item.info.shortname == "hq.metal.ore")
						totalAmount += amount;
					break;
				}
				case SkillType.Cloth:
				{
					if (item.info.shortname == "cloth")
					{
						if (dispenser != null && dispenser.gatherType == ResourceDispenser.GatherType.Flesh)
							return;

						totalAmount += amount;
					}

					break;
				}
				case SkillType.Butcher:
				{
					if (dispenser == null) return;

					var entity = dispenser.GetComponent<BaseEntity>();
					if (entity == null) return;

#if TESTING
					LogPlayerDebug(player, nameof(OnGather), $"Butcher entity: {entity.ShortPrefabName}");
#endif
					
					if (!_config.Animals.Contains(entity.ShortPrefabName)) return;

					totalAmount += amount;
					break;
				}

				case SkillType.Gather:
				{
					if (!string.IsNullOrEmpty(skill.ShortName) &&
					    skill.ShortName.Split('|').Contains(item.info.shortname))
						totalAmount += amount;

					break;
				}
			}
		});

		if (totalAmount > 0f)
		{
			if (_config.UseBonusMode)
			{
				var itemAmount = item.amount;

				itemAmount = Mathf.FloorToInt(itemAmount * totalAmount - itemAmount);

				if (itemAmount > 0)
				{
					var byItemId = ItemManager.Create(item.info, itemAmount, item.skin);
					if (byItemId != null)
						player.GiveItem(byItemId, BaseEntity.GiveItemReason.ResourceHarvested);
				}
			}
			else
			{
				item.amount = (int) (item.amount * totalAmount);
			}
		}
	}

	private void OnCollect(
		BasePlayer player,
		ItemAmount item,
		ResourceDispenser dispenser = null)
	{
		if (player == null || item == null || item.itemDef == null) return;

		var totalAmount = 0f;

		PlayerData.GetOrLoad(player.UserIDString)?.Skills?.ForEach(dataSkill =>
		{
			var skill = dataSkill?.Skill;
			if (skill == null) return;

			var amount = dataSkill.GetStageValue(player) / 100f;
			if (amount <= 0f) return;

			switch (skill.Type)
			{
				case SkillType.Wood:
				{
					if (item.itemDef.shortname == "wood") totalAmount += amount;
					break;
				}
				case SkillType.Stones:
				{
					if (item.itemDef.shortname == "stones") totalAmount += amount;
					break;
				}
				case SkillType.Sulfur:
				{
					if (item.itemDef.shortname == "sulfur.ore") totalAmount += amount;
					break;
				}
				case SkillType.Metal:
				{
					if (item.itemDef.shortname == "metal.ore" || item.itemDef.shortname == "hq.metal.ore")
						totalAmount += amount;
					break;
				}
				case SkillType.Cloth:
				{
					if (item.itemDef.shortname == "cloth")
					{
						if (dispenser != null && dispenser.gatherType == ResourceDispenser.GatherType.Flesh) return;

						totalAmount += amount;
					}

					break;
				}

				case SkillType.Butcher:
				{
					if (dispenser == null) return;

					var entity = dispenser.GetComponent<BaseEntity>();
					if (entity == null || !_config.Animals.Contains(entity.ShortPrefabName)) return;

					totalAmount += amount;
					break;
				}

				case SkillType.Gather:
				{
					if (!string.IsNullOrEmpty(skill.ShortName) &&
					    skill.ShortName.Split('|').Contains(item.itemDef.shortname))
						totalAmount += amount;
					break;
				}
			}
		});

		if (totalAmount > 0f)
		{
			if (_config.UseBonusMode)
			{
				var itemAmount = (int) item.amount;

				var byItemId = ItemManager.Create(item.itemDef,
					Mathf.FloorToInt(itemAmount * totalAmount - itemAmount));
				if (byItemId != null) player.GiveItem(byItemId, BaseEntity.GiveItemReason.ResourceHarvested);
			}
			else
			{
				item.amount = (int) (item.amount * totalAmount);
			}
		}
	}

	#endregion

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

		LoadImage(ref imagesList, _config.Background.Image);

		LoadImage(ref imagesList, _config.SkillPanel.Background.Image);

		LoadImage(ref imagesList, _config.SkillPanel.AddCostImage.Image);

		_config.Skills.ForEach(skill =>
		{
			if (skill.Enabled)
				LoadImage(ref imagesList, skill.Image);
		});

		_config.AdditionalButtons.Buttons.ForEach(btn =>
		{
			if (btn.Enabled)
				LoadImage(ref imagesList, btn.Image);
		});
		
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

	#region Fast Oven

	private BasePlayer GetPlayerByOven(ulong ovenID)
	{
		return _playerByOven.TryGetValue(ovenID, out var userID) ? RelationshipManager.FindByID(userID) : null;
	}

	private float GetFastOvenItemRate(BasePlayer player, string shortname)
	{
		var stage = _config.FastOven.GetStage(player);
		if (stage == null) return 0;

		return stage.Rates.TryGetValue(shortname, out var rate) ||
		       (shortname.Contains(".raw") && stage.Rates.TryGetValue("meat", out rate)) ||
		       (shortname.Contains(".ore") && stage.Rates.TryGetValue("ore", out rate)) ||
		       stage.Rates.TryGetValue("*", out rate)
			? rate
			: 0;
	}

	#endregion

	#region Stages

	private interface ISkill
	{
		Dictionary<int, IStage> GetStages(string userid);
	}

	private interface IStage
	{
		float GetCost();
		bool HasAccess(string userid);
		bool HasPermissions(string userid);
		List<RequiredSkillStage> GetRequiredSkillStages();
	}

	private static Dictionary<int, IStage> GetSkillStages(SkillEntry skill, string userid)
	{
		switch (skill.Type)
		{
			case SkillType.FastOven:
			{
				return _config.FastOven.GetStages(userid);
			}
			default:
			{
				return skill.GetStages(userid);
			}
		}
	}

	#endregion

	#region Duels

	private bool CheckIsDuelPlayer(BasePlayer player)
	{
		if (EventHelper) return Convert.ToBoolean(EventHelper.Call("EMAtEvent", player.userID.Get()));
		if (Battles) return Convert.ToBoolean(Battles?.Call("IsPlayerOnBattle", player.userID.Get()));
		if (Duel) return Convert.ToBoolean(Duel?.Call("IsPlayerOnActiveDuel", player));
		if (Duelist) return Convert.ToBoolean(Duelist?.Call("inEvent", player));
		if (ArenaTournament) return Convert.ToBoolean(ArenaTournament?.Call("IsOnTournament", player.userID.Get()));
		
        return false;
	}

	#endregion
	
	#endregion

	#region Lang

	private const string
		NoILError = "NoILError",
		DescriptionBtn = "DescriptionBtn",
		BroadcastLearnSkill = "BroadcastLearnSkill",
		NextBtn = "NextBtn",
		BackBtn = "BackBtn",
		NoPermissions = "NoPermissions",
		Upgrade = "Upgrade",
		MainTitle = "Title",
		Balance = "Balance",
		MaxLevelTitle = "MaxLevelTitle",
		MaxLevelDescription = "MaxLevelDescription",
		NotMoneyTitle = "NotMoneyTitle",
		NotMoneyDescription = "NotMoneyDescription",
		Close = "Close";

	protected override void LoadDefaultMessages()
	{
		lang.RegisterMessages(new Dictionary<string, string>
		{
			[MainTitle] = "SKILLS",
			[Upgrade] = "UPGRADE",
			[Balance] = "Balance: {0}$",
			[MaxLevelTitle] = "Warning",
			[MaxLevelDescription] = "You have the maximum level!!!",
			[NotMoneyTitle] = "Warning",
			[NotMoneyDescription] = "Not enough money!",
			[NoPermissions] = "You don't have permissions!",
			[Close] = "✕",
			[BackBtn] = "«",
			[NextBtn] = "»",
			[BroadcastLearnSkill] = "{username} just learned skill {skill_name} LVL {stage}",
			[DescriptionBtn] = "DESCRIPTION",
			[NoILError] = "The plugin does not work correctly, contact the administrator!"
		}, this);

		lang.RegisterMessages(new Dictionary<string, string>
		{
			[MainTitle] = "СПОСОБНОСТИ",
			[Upgrade] = "УЛУЧШИТЬ",
			[Balance] = "Баланс: {0}$",
			[MaxLevelTitle] = "Предупреждение",
			[MaxLevelDescription] = "У вас максимальный уровень!!!",
			[NotMoneyTitle] = "Предупреждение",
			[NotMoneyDescription] = "Недостаточно денег!",
			[Close] = "✕",
			[NoPermissions] = "У вас нет необходимого разрешения",
			[BackBtn] = "«",
			[NextBtn] = "»",
			[BroadcastLearnSkill] = "{username} изучил скилл {skill_name} уровня {stage}",
			[DescriptionBtn] = "ОПИСАНИЕ",
			[NoILError] = "Плагин работает некорректно, свяжитесь с администратором!"
		}, this, "ru");
	}

	private void SendNotify(BasePlayer player, float delay, string title, string description, string image)
	{
		if (Notifications)
			Notifications.Call("ShowNotify", player, delay, Msg(title, player.UserIDString),
				Msg(description, player.UserIDString), image);
		else if (_config.UseNotify && (Notify != null || UINotify != null))
			Interface.Oxide.CallHook("SendNotify", player, 0, Msg(player, description));
		else
			Reply(player, description);
	}

	private void SendNotify(BasePlayer player, string key, int type, params object[] obj)
	{
		if (_config.UseNotify && (Notify != null || UINotify != null))
			Interface.Oxide.CallHook("SendNotify", player, type, Msg(player, key, obj));
		else
			Reply(player, key, obj);
	}

	private void Reply(BasePlayer player, string key, params object[] obj)
	{
		SendReply(player, Msg(key, player.UserIDString, obj));
	}

	private string Msg(string key, string userid = null, params object[] obj)
	{
		return string.Format(lang.GetMessage(key, this, userid), obj);
	}

	private string Msg(BasePlayer player, string key)
	{
		return lang.GetMessage(key, this, player.UserIDString);
	}

	private string Msg(BasePlayer player, string key, params object[] obj)
	{
		return string.Format(lang.GetMessage(key, this, player.UserIDString), obj);
	}

	#endregion

	#region Convert

	#region Data 2.0

	[ConsoleCommand("skills.convert.olddata")]
	private void CmdConvertOldData(ConsoleSystem.Arg arg)
	{
		if (!arg.IsAdmin) return;

		StartConvertOldData();
	}

	private void StartConvertOldData()
	{
		var data = LoadOldData();
		if (data != null)
			timer.In(0.3f, () =>
			{
				CondertOldData(data);

				PrintWarning($"{data.Players.Count} players was converted!");
			});
	}

	private OldPluginData LoadOldData()
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

		return data ?? new OldPluginData();
	}

	private void CondertOldData(OldPluginData oldData)
	{
		if (oldData?.Players == null) return;

		foreach (var check in oldData.Players)
		{
			var userId = check.Key.ToString();

			var data = PlayerData.GetOrCreate(userId);

			foreach (var oldSkillData in check.Value.Skills)
				data.Skills.Add(new SkillData
				{
					Type = oldSkillData.Type,
					ID = oldSkillData.ID,
					Stage = oldSkillData.Stage
				});

			data.Permissions = check.Value.Permissions;
			data.Groups = check.Value.Groups;

			PlayerData.SaveAndUnload(userId);
		}
	}

	#region Classes

	private class OldPluginData
	{
		[JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public Dictionary<ulong, OldPlayerData> Players = new();
	}

	private class OldPlayerData
	{
		[JsonProperty(PropertyName = "Skills", ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public List<OldSkillData> Skills = new();

		[JsonProperty(PropertyName = "Permissions", ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public HashSet<string> Permissions = new();

		[JsonProperty(PropertyName = "Groups", ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public HashSet<string> Groups = new();
	}

	private class OldSkillData
	{
		[JsonProperty(PropertyName = "Type")] [JsonConverter(typeof(StringEnumConverter))]
		public SkillType Type;

		[JsonProperty(PropertyName = "ID")] public int ID;

		[JsonProperty(PropertyName = "Stage")] public int Stage;

		[JsonIgnore] public SkillEntry Skill;
	}

	#endregion

	#endregion

	#endregion

	#region Data

	#region Player

	private Dictionary<string, PlayerData> _usersData = new();

	private class PlayerData
	{
		#region Main

		#region Fields

		[JsonProperty(PropertyName = "UserID")]
		public string UserID;

		[JsonProperty(PropertyName = "Skills", ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public List<SkillData> Skills = new();

		[JsonProperty(PropertyName = "Permissions", ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public HashSet<string> Permissions = new();

		[JsonProperty(PropertyName = "Groups", ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public HashSet<string> Groups = new();

		[JsonProperty(PropertyName = "Opened Descriptions",
			ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public List<KeyValuePair<SkillType, int>> OpenedDescriptions = new();

		#endregion

		#region Utils

		public void CheckStages(string userid)
		{
			Skills?.ForEach(skill => skill?.CheckStage(userid));
		}

		public bool CanSeeDescription(SkillEntry skill)
		{
			return !_config.SkillPanel.DescriptionButton.Enabled || OpenedDescriptions.Exists(data => data.Key == skill.Type && data.Value == skill.ID);
		}

		public void ChangeDescriptionView(SkillEntry skill)
		{
			if (!_config.SkillPanel.DescriptionButton.Enabled) return;

			if (CanSeeDescription(skill))
				OpenedDescriptions.RemoveAll(data => data.Key == skill.Type && data.Value == skill.ID);
			else
				OpenedDescriptions.Add(new KeyValuePair<SkillType, int>(skill.Type, skill.ID));
		}

		public void AddSkill(BasePlayer player, SkillEntry skill, int stage)
		{
			if (Interface.Oxide.CallHook("CanSkillLearn", player, skill.Type.ToString(), skill.ID, stage) != null)
				return;

			Skills.Add(new SkillData
			{
				ID = skill.ID,
				Type = skill.Type,
				Stage = stage
			});

			CheckSkill(player, skill, stage);

			Interface.Oxide.CallHook("OnSkillLearned", player, skill.Type.ToString(), skill.ID, stage);
			
			_config.Broadcasting.LearnSkill(player, skill, stage);

			LoadSkillsCache();

			if (skill.Type is SkillType.Regeneration or SkillType.Metabolism &&
			    _instance._metabolismEngine != null)
				_instance._metabolismEngine.AddOrUpdate(player);
			
			if (_config.Logging.UsePlayerActionLogging)
				_instance?.LogToFile("player_action", $"Player {player.UserIDString} has successfully learned skill {skill.Type} with ID {skill.ID} at stage {stage}", _instance);
		}

		public void UpgradeSkill(SkillData data, BasePlayer player, SkillEntry skill, int stage)
		{
			if (Interface.Oxide.CallHook("CanSkillLearn", player, skill.Type.ToString(), skill.ID, stage) != null)
				return;

			data.Stage = stage;

			CheckSkill(player, skill, stage);

			data.UpdateStage();

			Interface.Oxide.CallHook("OnSkillLearned", player, skill.Type.ToString(), skill.ID, stage);

			_config.Broadcasting.LearnSkill(player, skill, stage);

			if (skill.Type is SkillType.Regeneration or SkillType.Metabolism &&
			    _instance._metabolismEngine != null)
				_instance._metabolismEngine.AddOrUpdate(player);
			
			if (_config.Logging.UsePlayerActionLogging)
				_instance?.LogToFile("player_action", $"Player {player.UserIDString} has successfully upgraded skill {skill.Type} with ID {skill.ID} at stage {stage}", _instance);
		}

		public void RemoveSkill(SkillData data, BasePlayer player, SkillEntry skill)
		{
			if (Interface.Oxide.CallHook("CanSkillRemove", player, skill.Type.ToString(), skill.ID) != null)
				return;

			Skills.Remove(data);

			LoadSkillsCache();

			Interface.Oxide.CallHook("OnSkillRemoved", player, skill.Type.ToString(), skill.ID);

			if (!HasSkill(this, SkillType.Regeneration) && !HasSkill(this, SkillType.Metabolism) &&
			    _instance._metabolismEngine != null)
				_instance._metabolismEngine.Remove(player);
			
			if (_config.Logging.UsePlayerActionLogging)
				_instance?.LogToFile("player_action", $"Player {player.UserIDString} has successfully removed skill {skill.Type} with ID {skill.ID}", _instance);
		}

		private void CheckSkill(IPlayer player)
		{
			Skills?.ForEach(skill => CheckSkill(player?.Object as BasePlayer, skill.Skill, skill.Stage));
		}

		private void CheckSkill(BasePlayer player, SkillEntry skill, int stage)
		{
			if (player == null || skill == null) return;

			GetCommands(player, skill, stage);

			GrantPermissions(player, skill, stage);

			GrantGroups(player, skill, stage);
		}

		private void GetCommands(BasePlayer player, SkillEntry skill, int stage)
		{
			if (skill.Stages.TryGetValue(stage, out var skillStage))
				skillStage?.Commands?.ForEach(command =>
				{
					command = command
						.Replace("\n", "|")
						.Replace("%steamid%", player.UserIDString, StringComparison.OrdinalIgnoreCase)
						.Replace("{steamid}", player.UserIDString, StringComparison.OrdinalIgnoreCase)
						.Replace("%username%", player.displayName, StringComparison.OrdinalIgnoreCase)
						.Replace("{username}", player.displayName, StringComparison.OrdinalIgnoreCase);

					foreach (var check in command.Split('|'))
						_instance?.Server.Command(check);
				});
		}

		#region Permissions

		private void GrantPermissions(BasePlayer player, SkillEntry skill, int stage)
		{
			if (skill.Stages.TryGetValue(stage, out var skillStage))
				skillStage.Permissions?.ForEach(command => Permissions.Add(command));
		}

		private void GrantGroups(BasePlayer player, SkillEntry skill, int stage)
		{
			if (skill.Stages.TryGetValue(stage, out var skillStage))
				skillStage.Groups?.ForEach(command => Groups.Add(command));
		}

		#endregion

		#region Skill Data.Caching

		[JsonIgnore] private Dictionary<int, SkillData> _skillDataByID = new();

		[JsonIgnore] private Dictionary<SkillType, SkillData> _skillDataByType = new();

		public SkillData GetSkillData(SkillType type, int id = 0)
		{
			if (type is SkillType.None or SkillType.Gather
				    ? _skillDataByID.TryGetValue(id, out var data)
				    : _skillDataByType.TryGetValue(type, out data))
				return data;

			return null;
		}

		private void LoadSkillsCache()
		{
			_skillDataByID.Clear();
			_skillDataByType.Clear();

			Skills?.ForEach(skillData =>
			{
				switch (skillData.Type)
				{
					case SkillType.Gather:
					case SkillType.None:
					{
						_skillDataByID[skillData.ID] = skillData;
						break;
					}

					default:
					{
						_skillDataByType[skillData.Type] = skillData;
						break;
					}
				}
			});
		}

		#endregion

		#endregion

		#endregion

		#region Helpers

		public static string BaseFolder()
		{
			return "Skills" + Path.DirectorySeparatorChar + "Players" + Path.DirectorySeparatorChar;
		}

		public static PlayerData GetOrLoad(string userId)
		{
			return GetOrLoad(BaseFolder(), userId);
		}

		public static PlayerData GetNotLoad(string userId)
		{
			if (!userId.IsSteamId()) return null;

			var data = GetOrLoad(BaseFolder(), userId, false);

			return data;
		}
		
		public static PlayerData GetOrLoad(string baseFolder, string userId, bool load = true)
		{
			if (_instance._usersData.TryGetValue(userId, out var data)) return data;

			if ((data = Load(baseFolder, userId, load)) != null)
			{
				data.CheckStages(userId);

				data.LoadSkillsCache();

				data.UserID = userId;
			}

			return data;
		}

		public static PlayerData Load(string baseFolder, string userId, bool load = true)
		{
			PlayerData data = null;
			try
			{
				data = ReadOnlyObject(baseFolder + userId);
			}
			catch (Exception e)
			{
				Interface.Oxide.LogError(e.ToString());
			}

			if (data  == null) return null;
			
			return load ? _instance._usersData[userId] = data : data;
		}

		public static PlayerData GetOrCreate(string userId)
		{
			return GetOrLoad(userId) ?? (_instance._usersData[userId] = new PlayerData
			{
				UserID = userId
			});
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

		public static void DoWipe(string userId)
		{
			var data = GetNotLoad(userId);
			if (data == null) return;
			
			if (HasSkill(data, SkillType.TransferWipe))
			{					
				if (_config.TransferWipe.ResetSingle)
				{
					data.Skills.RemoveAll(x => x.Type == SkillType.TransferWipe);

					data.LoadSkillsCache();
				}
				
				SaveAndUnload(userId);
			}
			else
			{				
				Unload(userId);
				
				Interface.Oxide.DataFileSystem.DeleteDataFile(BaseFolder() + userId);
			}
		}

		#endregion
	}

	private class SkillData
	{
		[JsonProperty(PropertyName = "Type")] [JsonConverter(typeof(StringEnumConverter))]
		public SkillType Type;

		[JsonProperty(PropertyName = "ID")] public int ID;

		[JsonProperty(PropertyName = "Stage")] public int Stage;

		[JsonIgnore] private SkillEntry _skill;

		[JsonIgnore] public SkillEntry Skill => _skill ??= _instance.FindSkill(Type, ID);

		[JsonIgnore] private StageConf _stageConf;

		public StageConf GetStage(BasePlayer player = null)
		{
			if (_stageConf == null) UpdateStage();

			if (player != null && _instance.HasBypass(player))
				return Skill != null && Skill.Stages.Count > 0 ? Skill.Stages[Skill.Stages.Max(x => x.Key)] : null;

			return _stageConf;
		}

		public float GetStageValue(BasePlayer player = null)
		{
			return GetStage(player)?.Value ?? 0f;
		}

		public float GetStageSecondValue(BasePlayer player = null)
		{
			return GetStage(player)?.Value2 ?? 0f;
		}

		public void CheckStage(string userid)
		{
			if (Skill == null) return;

			var stages = GetSkillStages(Skill, userid);
			if (!stages.ContainsKey(Stage))
				if (stages.Count > 0)
					Stage = stages.Max(x => x.Key);
		}

		public void UpdateStage()
		{
			Skill?.Stages.TryGetValue(Stage, out _stageConf);
		}
	}

	#region Utils

	private double GetNextPrice(BasePlayer player, SkillEntry skill)
	{
		var result = -1;

		var data = PlayerData.GetOrCreate(player.UserIDString);
		if (data == null) return result;

		var stages = GetSkillStages(skill, player.UserIDString);

		var skillData = data.GetSkillData(skill.Type, skill.ID);
		if (skillData == null)
		{
			if (stages[1].GetRequiredSkillStages().Any(x => x.Has(player) == false))
				return result;

			if (_config.SkillPanel.ShowAllStages && stages[1].HasAccess(player.UserIDString) == false)
				return -2;

			return stages[1].GetCost();
		}

		var nextStage = skillData.Stage + 1;

		if (!stages.ContainsKey(nextStage))
			return result;

		if (stages[nextStage].GetRequiredSkillStages().Any(x => x.Has(player) == false))
			return result;

		if (_config.SkillPanel.ShowAllStages && stages[nextStage].HasPermissions(player.UserIDString) == false)
			return -2;

		return stages[nextStage].GetCost();
	}

	#endregion

	#endregion

	#region Containers

	private void SaveContainers()
	{
		Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Containers", _lootedContainers);
	}

	private void LoadContainers()
	{
		try
		{
			_lootedContainers = Interface.Oxide.DataFileSystem.ReadObject<HashSet<ulong>>($"{Name}/Containers");
		}
		catch (Exception e)
		{
			PrintError(e.ToString());
		}

		_lootedContainers ??= new HashSet<ulong>();
	}

	private void ValidateContainers()
	{
		var removedContainer = _lootedContainers.RemoveWhere(lootedContainer =>
			!BaseNetworkable.serverEntities.Contains(new NetworkableId(lootedContainer)));
		
		if (removedContainer > 0)
			SaveContainers();
	}
	
	#endregion

	#endregion

	#region Components

	#region Regen&Metabolism

	private MetabolismComponent _metabolismEngine;

	private void InitMetabolism()
	{
		if (_metabolismEngine != null)
			return;

		_metabolismEngine = new GameObject()
			.AddComponent<MetabolismComponent>();
	}

	private class MetabolismData
	{
		[JsonIgnore] public BasePlayer Player;

		public bool HasMetabolism;

		public float MetabolismStageValue;

		public bool HasRegeneration;

		public float RegenerationStageValue;
	}

	private class MetabolismComponent : FacepunchBehaviour
	{
		private Dictionary<ulong, MetabolismData> Players = new();

		private Dictionary<ulong, float> _timeSinceLastMetabolism = new();

		private bool _started;

		private void Awake()
		{
			TryStartInvoking();
		}

		private void TryStartInvoking()
		{
			if (_started)
			{
				if (Players.Count == 0)
				{
					CancelInvoke(Handler);
					_started = false;
				}

				return;
			}

			if (Players.Count > 0)
			{
				InvokeRepeating(Handler, 0.75f, 0.75f);
				_started = true;
			}
		}

		private void Handler()
		{
			foreach (var player in Players) PlayerMetabolismHandler(player.Value);
		}

		private void PlayerMetabolismHandler(MetabolismData data)
		{
			var player = data.Player;

			if (data.HasMetabolism)
			{
				var value = data.MetabolismStageValue;
				if (value > 0.1f)
				{
					player.metabolism.hydration.Add(value);

					player.metabolism.calories.Add(value);
				}
			}

			if (data.HasRegeneration)
			{
				var nowTime = Time.time;

				if (_timeSinceLastMetabolism.TryGetValue(player.userID, out var oldTime))
				{
					var healAmount = data.RegenerationStageValue / 60f;

					if (nowTime - oldTime > 0.1f) player.Heal(healAmount);
				}

				_timeSinceLastMetabolism[player.userID] = nowTime;
			}
		}

		public void AddOrUpdate(BasePlayer player)
		{
			var playerData = PlayerData.GetOrLoad(player.UserIDString);
			if (playerData == null) return;

			var newValue = !Players.TryGetValue(player.userID, out var data);
			if (newValue)
				data = new MetabolismData();

			SkillData skillData;

			data.Player = player;

			data.HasMetabolism = (skillData = playerData.GetSkillData(SkillType.Metabolism)) != null;
			if (data.HasMetabolism)
				data.MetabolismStageValue = skillData?.GetStageValue(player) ?? 0f;

			data.HasRegeneration = (skillData = playerData.GetSkillData(SkillType.Regeneration)) != null;
			if (data.HasRegeneration)
				data.RegenerationStageValue = skillData?.GetStageValue(player) ?? 0f;

			if (newValue && (data.HasMetabolism || data.HasRegeneration)) Players[player.userID] = data;

			TryStartInvoking();
		}

		public void Remove(BasePlayer player)
		{
			if (Players.ContainsKey(player.userID))
			{
				Players.Remove(player.userID);

				TryStartInvoking();
			}
		}

		#region Destroy

		private void OnDestroy()
		{
			CancelInvoke();
		}

		public void Kill()
		{
			DestroyImmediate(this);
		}

		#endregion
	}

	#endregion

	#endregion

	#region Testing functions

#if TESTING
	private static void SayDebug(string message)
	{
		Debug.Log($"[Skills.Debug] {message}");

		_instance?.LogToFile("debug", message, _instance);
	}

	private static void SayDebug(ulong player, string hook, string message)
	{
		Debug.Log($"[Skills | {hook} | {player}] {message}");
	}
	
	private static void LogPlayerDebug(BasePlayer player, string hook, string message)
	{
		_instance?.LogToFile($"{player.UserIDString}", $"[{hook}] {message}", _instance);
	}
	
	[ConsoleCommand("skills.test.1")]
	private void CmdTestPermission(ConsoleSystem.Arg arg)
	{
		if (!arg.IsServerside) return;

		var userID = "76561198122331446";

		var check1 = permission.UserHasPermission(userID, "nteleportation.vip");

		SendReply(arg, $"Check 1: {check1}");

		var check2 = permission.UserHasPermission(userID, "activesort.use");

		SendReply(arg, $"Check 2: {check2}");

		var check3 = permission.UserHasPermission(userID, "furnacesplitter.use");

		SendReply(arg, $"Check 3: {check3}");

		var check4 = permission.UserHasPermission(userID, "kits.default");

		SendReply(arg, $"Check 4: {check4}");

		var check5 = permission.UserHasPermission(userID, "kits.admin");

		SendReply(arg, $"Check 5: {check5}");
	}

	[ConsoleCommand("skills.test.transfer.wipe")]
	private void CmdTestTransferWipe(ConsoleSystem.Arg arg)
	{
		if (!arg.IsAdmin) return;

		var player = arg.Player();
		if (player == null) return;
		
		var stage = arg.GetInt(0);
		if (stage < 0)
		{
			SendReply(arg, "Invalid stage");
			return;
		}
		
		var data = PlayerData.GetOrCreate(player.UserIDString);
		if (data == null) return;
		
		var skillType = SkillType.TransferWipe;
		
		var skill = FindSkill(skillType);
		if (skill == null)
		{
			SendReply(arg, $"Skill not found: {skillType}");
			return;
		}
		
		var dataSkill = data.GetSkillData(skillType);
		if (dataSkill == null)
			data.AddSkill(player, skill, stage);
		else
			data.UpgradeSkill(dataSkill, player, skill, stage);
		
		SayDebug($"Player {player.UserIDString} has transfer wipe skill: {HasSkill(data, SkillType.TransferWipe)}");
		
		var sb = new StringBuilder();
		
		data.Skills.ForEach(sd =>
		{
			sb.AppendLine($"{sd.Type}: {sd.Stage}");
		});
		
		SayDebug($"Player {player.UserIDString} has skills:\n{sb}");
		
		PlayerData.DoWipe(player.UserIDString);
		
		SayDebug($"Player {player.UserIDString} skills WIPED!!!!");
		
		SayDebug($"Player {player.UserIDString} has transfer wipe skill (after wipe): {HasSkill(data, SkillType.TransferWipe)}");

		sb.Clear();
		data.Skills.ForEach(sd =>
		{
			sb.AppendLine($"{sd.Type}: {sd.Stage}");
		});
		
		SayDebug($"Player {player.UserIDString} has skills (after wipe):\n{sb}");

		PlayerData.DoWipe(player.UserIDString);
		
		SayDebug($"Player {player.UserIDString} skills second WIPED!!!!");
		
		SayDebug($"Player {player.UserIDString} has transfer wipe skill (after SECOND wipe): {HasSkill(data, SkillType.TransferWipe)}");

		sb.Clear();
		data.Skills.ForEach(sd =>
		{
			sb.AppendLine($"{sd.Type}: {sd.Stage}");
		});
		
		SayDebug($"Player {player.UserIDString} has skills (after SECOND wipe):\n{sb}");

	}


	[ConsoleCommand("skills.test.loot.barrel")]
	private void CmdTestLootBarrel(ConsoleSystem.Arg arg)
	{
		if (!arg.IsServerside) return;

		var skillEntry = FindSkill(SkillType.ComponentsRates);

		var player = BasePlayer.FindAwakeOrSleepingByID(76561198122331446UL);
		if (player == null)
		{
			SendReply(arg, $"Player not found");
			return;
		}
		
		var data = PlayerData.GetOrCreate(player.UserIDString);
		if (data == null) return;
		
		data.AddSkill(player, skillEntry, 3);

		var barrel =
			GameManager.server.CreateEntity("assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab") as
				LootContainer;
		if (barrel  == null) return;

		barrel.enableSaving = false;
		
		barrel.Invoke(() =>
		{
			barrel.Kill();
		}, 5f);
		
		barrel.Spawn();
		
		barrel.Invoke(() =>
		{
			barrel.OnKilled(null);
		}, 1f);
		
#if TESTING
		var sb = new StringBuilder();
		barrel.inventory.itemList.ForEach(item =>
		{
			sb.AppendLine($"item: {item.info.shortname}, amount: {item.amount}");
		});

		SayDebug(0UL, nameof(CmdTestLootBarrel), $"has loot barrel items:\n{sb.ToString()}");
#endif

	}


#endif

	#endregion

	#region Harmony Patches

#if USE_HARMONY

	#region Load/Unload
	
	private Harmony _harmony;

	private void InitHarmonyPatches()
	{
		_harmony = new Harmony(Name + "Patch");

		_harmony?.PatchAll();
	}

	private void DisposeHarmonyPatches()
	{
		_harmony?.UnpatchAll(Name + "Patch");
	}

	#endregion

	#region Patches

	#region Permissions

	#region User Has Permission

	[HarmonyPatch(typeof(Permission), nameof(Permission.UserHasPermission))]
	private class PermissionUserHasPermissionPatch
	{
		public static bool Prefix(ref bool __result,
#if CARBON
			string id, string perm
#else
			string playerId, string permission
#endif
		)
		{
#if CARBON
			if (HasSkillsPlayerPermission(id, perm))
#else
			if (HasSkillsPlayerPermission(playerId, permission))
#endif
			{
				__result = true;
				return false;
			}

			return true;
		}
	}

	private static bool HasSkillsPlayerPermission(string playerId, string permission)
	{
		if (string.IsNullOrEmpty(playerId) || !playerId.IsSteamId()) return false;

		var data = PlayerData.GetOrLoad(playerId);
		if (data == null) return false;

		if (data.Permissions.Contains(permission)) return true;
		
		foreach (var group in data.Groups)
		{
			if (_instance.permission.GroupHasPermission(group, permission))
				return true;
		}

		return false;
	}

	#endregion

	#region User Has Group

	[HarmonyPatch(typeof(Permission), nameof(Permission.UserHasGroup))]
	public class PermissionUserHasGroupPatch
	{
		public static bool Prefix(ref bool __result,
#if CARBON
				string id, string name
#else
			string playerId, string groupName
#endif
		)
		{
#if CARBON
				if (HasSkillsPlayerGroup(id, name))
#else
			if (HasSkillsPlayerGroup(playerId, groupName))
#endif
			{
				__result = true;
				return false;
			}

			return true;
		}
	}

	private static bool HasSkillsPlayerGroup(string playerId, string groupName)
	{
		if (string.IsNullOrEmpty(playerId) || !playerId.IsSteamId()) return false;

		var data = PlayerData.GetOrLoad(playerId);
		return data != null && data.Groups.Contains(groupName);
	}

	#endregion
	
	#region Get User Groups

	[HarmonyPatch(typeof(Permission), nameof(Permission.GetUserGroups))]
	public class PermissionGetUserGroupsPatch
	{
		public static void Postfix(ref string[] __result,
#if CARBON
			string id
#else
			string playerId
#endif
		)
		{
#if CARBON
			if (TryGetSkillsPlayerGroups(id, ref __result, out var resultGroups))
#else
			if (TryGetSkillsPlayerGroups(playerId, ref __result, out var resultGroups))
#endif
			{
				__result = resultGroups;
			}
		}
	}

	private static bool TryGetSkillsPlayerGroups(string playerId, ref string[] playerGroups, out string[] resultGroups)
	{
		resultGroups = playerGroups;
		if (string.IsNullOrEmpty(playerId) || !playerId.IsSteamId()) return false;

		var data = PlayerData.GetOrLoad(playerId);
		if (data?.Groups is {Count: > 0})
		{
			var list  = new HashSet<string>(playerGroups);
			foreach (var group in data.Groups)
			{
				list.Add(group);
			}

			resultGroups = list.ToArray();
			return true;
		}

		return false;
	}
	
	#endregion
	
	#region Get User Permissions

	[HarmonyPatch(typeof(Permission), nameof(Permission.GetUserPermissions))]
	public class PermissionGetUserPermissionsPatch
	{
		public static void Postfix(ref string[] __result,
#if CARBON
			string id
#else
			string playerId
#endif
		)
		{
#if CARBON
			if (TryGetSkillsPlayerGroups(id, ref __result, out var resultGroups))
#else
			if (TryGetSkillsPlayerGroups(playerId, ref __result, out var resultGroups))
#endif
			{
				__result = resultGroups;
			}
		}
	}

	private static bool TryGetSkillsPlayerPermissions(string playerId, ref string[] playerPermissions, out string[] resultPermissions)
	{
		resultPermissions = playerPermissions;
		if (string.IsNullOrEmpty(playerId) || !playerId.IsSteamId()) return false;

		var data = PlayerData.GetOrLoad(playerId);
		if (data == null) return false;
		
		var list = new HashSet<string>();
		if (data?.Permissions is {Count: > 0})
			list.UnionWith(data.Permissions);
		
		if (data?.Groups is {Count: > 0})
			foreach (var group in data.Groups)
			{
				var groupPermissions = _instance.permission.GetGroupPermissions(group);
				if (groupPermissions  is  {Length:  >  0})
					list.UnionWith(groupPermissions);
			}

		if (list.Count > 0)
		{
			list.UnionWith(playerPermissions);

			resultPermissions = list.ToArray();
			return true;
		}
		
		return false;
	}
	
	#endregion

	#endregion
	
	#region Get Charcoal Rate

	[HarmonyPatch(typeof(BaseOven), nameof(BaseOven.GetCharcoalRate))]
	public class GetCharcoalRatePatch
	{
		public static void Postfix(ref BaseOven __instance, ref int __result)
		{
			if (__instance == null || __instance.net == null) return;

			var player = _instance?.GetPlayerByOven(__instance.net.ID.Value);
			if (player == null) return;

			var stage = _config.FastOven.GetStage(player);
			if (stage == null) return;

			__result = Mathf.Max(0, Mathf.CeilToInt(__result * _config.FastOven.CharcoalMultiplier));
		}
	}

	#endregion

	#region BasePlayer
	
	[HarmonyPatch(typeof(BasePlayer), nameof(BasePlayer.GetRecoveryChance))]
	public class BasePlayerGetRecoveryChancePatch
	{
		public static bool Prefix(ref float __result, BasePlayer __instance)
		{
			var stage = PlayerData.GetOrLoad(__instance.UserIDString)?.GetSkillData(SkillType.StandUpChance)
				?.GetStage(__instance);
			if (stage == null) return true;

			float finalRecoveryChance;
			if (__instance.IsIncapacitated())
				finalRecoveryChance = (float) Math.Round(stage.Value / 100f, 2);
			else
				finalRecoveryChance =  (float) Math.Round(stage.Value2 / 100f, 2);

			var hasMedKit = __instance.inventory.containerBelt.FindItemByItemID(ItemManager.FindItemDefinition("largemedkit").itemid) != null && 
			        !__instance.woundedByFallDamage;
			__result = hasMedKit ? 1f : finalRecoveryChance;
			
			return false;
		}
	}

	[HarmonyPatch(typeof(BasePlayer), nameof(BasePlayer.WoundingTick))]
	public class BasePlayerWoundingTickPatch
	{
		public static bool Prefix(BasePlayer __instance)
		{
			if (__instance.IsDead()) return true;
			
			var stage = PlayerData.GetOrLoad(__instance.UserIDString)?.GetSkillData(SkillType.StandUpChance)
				?.GetStage(__instance);
			if (stage == null) return true;

			using (TimeWarning.New(nameof(BasePlayer.WoundingTick)))
			{
				if (!ConVar.Player.woundforever &&
				    __instance.TimeSinceWoundedStarted >= (double) __instance.woundedDuration)
				{
					float finalRecoveryChance;
					if (__instance.IsIncapacitated())
						finalRecoveryChance = (float) Math.Round(stage.Value / 100f, 2);
					else
						finalRecoveryChance =  (float) Math.Round(stage.Value2 / 100f, 2);
					
					if (Mathf.Approximately(finalRecoveryChance, 1f) || Random.value < finalRecoveryChance)
					{
						__instance.RecoverFromWounded();
					}
					else if (__instance.woundedByFallDamage)
					{
						__instance.Die();
					}
					else
					{
						var itemByItemId =
							__instance.inventory.containerBelt.FindItemByItemID(ItemManager
								.FindItemDefinition("largemedkit").itemid);
						if (itemByItemId != null)
						{
							itemByItemId.UseItem();
							__instance.RecoverFromWounded();
						}
						else
						{
							__instance.Die();
						}
					}
				}
				else
				{
					if (__instance.IsSwimming() && __instance.IsCrawling())
						__instance.GoToIncapacitated(null);
					
					__instance.Invoke(__instance.WoundingTick, 1f);
				}
			}
			return false;
		}
	}
	
	#endregion
	
	#endregion

#endif

	#endregion

	#region API

	private static bool HasSkill(string userId, SkillType type)
	{
		return HasSkill(PlayerData.GetOrLoad(userId), type);
	}

	private static bool HasSkill(PlayerData data, SkillType type)
	{
		var skillData = data?.GetSkillData(type);
		if (skillData == null) return false;
		
		var skill = skillData.Skill;
		return skill != null && skill.Stages.ContainsKey(skillData.Stage);
	}

	#endregion
}